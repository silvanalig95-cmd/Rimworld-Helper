using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The weapon rating at the top of the game's own info card (its own stat category, sorted first): the rating, the
    /// sub-ratings, each tag with why, the line in plain words, the effects counted or named, and the rating for the colonist
    /// who holds it. Each entry's explanation is the card's report pane. It shows wherever the game's card opens: a weapon on
    /// the map, the gear tab, Nice Inventory Tab, a trader's goods, a def from the research tab (its default stuff then).
    /// A button at the card's foot opens Rimfolk's own weapon card.
    /// </summary>
    public static class WeaponInfoCard
    {
        private static StatCategoryDef category;
        private static bool looked;

        /// <summary>The category the entries go in (Defs/Misc/Rimfolk_WeaponStats.xml), or null.</summary>
        public static StatCategoryDef Category
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    category = DefDatabase<StatCategoryDef>.GetNamedSilentFail("Rimfolk_WeaponRating");
                }
                return category;
            }
        }

        public static bool On => RimfolkPatches.On && WeaponLive.On;

        private static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>The colonist who holds or carries a weapon, or null.</summary>
        public static Pawn Holder(Thing t)
        {
            if (t == null) return null;
            if (t.ParentHolder is Pawn_EquipmentTracker eq) return eq.pawn;
            if (t.ParentHolder is Pawn_InventoryTracker inv) return inv.pawn;
            return null;
        }

        /// <summary>The entries for a weapon, its thing or its def in a stuff and at a quality.</summary>
        public static List<StatDrawEntry> Entries(ThingDef def, StatRequest req)
        {
            var cat = Category;
            if (cat == null) return null;
            var r = req.HasThing ? WeaponLive.Of(req.Thing) : WeaponLive.Of(def, req.StuffDef, req.QualityCategory);
            if (r == null) return null;
            var list = new List<StatDrawEntry>();
            int prio = 5000;
            var g = r.Rating;
            string band = WeaponWords.Quality(g.Overall);
            var expl = new StringBuilder();
            expl.AppendLine("CC_Weapon_Info_RatingExpl".Translate());
            expl.AppendLine();
            expl.AppendLine("CC_Weapon_Info_RatedAs".Translate(RoleWords(g.Role).Named("ROLE"), (g.Kind == "ranged" ? "Shooting" : "Melee").Named("SKILL")).Resolve());
            if (!string.IsNullOrEmpty(r.StuffLabel)) expl.AppendLine("CC_Weapon_Info_Stuff".Translate(r.StuffLabel.Named("STUFF")).Resolve());
            if (r.Rough)
            {
                expl.AppendLine();
                expl.AppendLine("CC_Weapon_Info_Rough".Translate());
                foreach (var u in r.Unsure) expl.AppendLine("  - " + u);
            }
            list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_Rating".Translate(), F1(g.Overall) + " (" + band + (r.Rough ? ", " + "CC_Weapon_Rough".Translate() : "") + ")",
                expl.ToString().TrimEnd(), prio--));

            // The sub-ratings.
            foreach (var k in WeaponForMath.SubKeys(g))
            {
                if (!g.Sub.TryGetValue(k, out var v)) continue;
                list.Add(new StatDrawEntry(cat, ("CC_Weapon_Sub_" + k.CapitalizeFirst()).Translate(), F1(v), ("CC_Weapon_SubExpl_" + k.CapitalizeFirst()).Translate(), prio--));
            }

            // The line in plain words.
            if (!string.IsNullOrEmpty(r.Plain))
                list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_Plain".Translate(), r.Plain, "CC_Weapon_Info_PlainExpl".Translate() + "\n\n" + r.Plain, prio--));

            // Each tag, coloured by its category, with why.
            foreach (var t in r.Tags)
            {
                var style = WeaponTagMath.Style(t.Category);
                var def0 = WeaponTagMath.Def(t.Id);
                string why = t.Why.CapitalizeFirst() + "." + (def0 != null ? "\n\n" + "CC_Weapon_Info_TagRule".Translate(def0.Rule.Named("RULE")).Resolve() : "");
                list.Add(new StatDrawEntry(cat, t.Label.Colorize(WeaponUi.Colour(style.Colour)), CategoryWord(t.Category), why, prio--));
            }

            // The effects: counted or only named.
            var counted = g.Specials.Where(s => s.Counted).ToList();
            var named = g.Specials.Where(s => !s.Counted).ToList();
            if (g.Specials.Count > 0)
            {
                var sb = new StringBuilder();
                if (counted.Count > 0)
                {
                    sb.AppendLine("CC_Weapon_Info_Counted".Translate());
                    foreach (var s in counted) sb.AppendLine("  - " + s.Label);
                }
                if (named.Count > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("CC_Weapon_Info_Named".Translate());
                    foreach (var s in named) sb.AppendLine("  - " + s.Label);
                }
                list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_Effects".Translate(),
                    "CC_Weapon_Info_EffectsValue".Translate(counted.Count.Named("COUNTED"), named.Count.Named("NAMED")).Resolve(), sb.ToString().TrimEnd(), prio--));
            }

            // For the colonist who holds or carries it.
            var who = req.HasThing ? Holder(req.Thing) : null;
            if (who != null && who.RaceProps.Humanlike)
            {
                string cant = WeaponLive.CantUse(who, req.Thing, r);
                var fc = cant == null ? WeaponLive.ForColonist(who, r) : null;
                if (cant != null)
                    list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_For".Translate(who.Named("PAWN")).Resolve(), "CC_Weapon_CantUse".Translate(), cant, prio--));
                else if (fc != null)
                {
                    var why = WeaponForMath.Telling(fc);
                    string text = "CC_Weapon_Info_ForExpl".Translate(who.Named("PAWN"), F1(fc.Rating).Named("RATING"), F1(fc.Reference).Named("REFERENCE")).Resolve() +
                                  (why.Count > 0 ? "\n\n" + string.Join("\n", why.Select(s => "  " + WeaponUi.StepWords(s.why) + ": " + WeaponForMath.Delta(s.delta))) : "");
                    list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_For".Translate(who.Named("PAWN")).Resolve(), F1(fc.Rating), text, prio--));
                }
            }
            return list;
        }

        public static string CategoryWord(TagCategory c) => ("CC_Weapon_Cat_" + c).Translate();

        public static string RoleWords(string role) => ("CC_Weapon_Role_" + (role ?? "").Replace("-", "").Replace(" ", "")).Translate();

        // ---------------------------------------------------------------- the button at the card's foot

        private static readonly AccessTools.FieldRef<Dialog_InfoCard, Thing> CardThing = Ref<Thing>("thing");
        private static readonly AccessTools.FieldRef<Dialog_InfoCard, Def> CardDef = Ref<Def>("def");
        private static readonly AccessTools.FieldRef<Dialog_InfoCard, ThingDef> CardStuff = Ref<ThingDef>("stuff");

        private static AccessTools.FieldRef<Dialog_InfoCard, T> Ref<T>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<Dialog_InfoCard, T>(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>"Rimfolk's weapon card" at the foot of the game's card, left of its close button, for a weapon the rating reads.</summary>
        public static void Button(Dialog_InfoCard card, Rect inRect)
        {
            if (!WeaponCardWindow.On || CardThing == null || CardDef == null) return;
            var thing = CardThing(card);
            var def = thing?.def ?? CardDef(card) as ThingDef;
            if (!WeaponLive.Rateable(def)) return;
            var r = thing != null ? WeaponLive.Of(thing) : WeaponLive.Of(def, CardStuff?.Invoke(card));
            if (r == null) return;
            string label = "CC_Weapon_OpenCard".Translate();
            Text.Font = GameFont.Small;
            float w = Mathf.Min(260f, Text.CalcSize(label).x + 54f);
            var b = new Rect(inRect.x, inRect.yMax - 36f, w, 34f);
            if (UiKit.Button(b, ""))
            {
                if (thing != null) WeaponCardWindow.Open(thing, Holder(thing));
                else WeaponCardWindow.Open(def, CardStuff?.Invoke(card), null);
                // With Rimfolk's card opening first, the two cards are two ways of looking: going over closes the game's.
                if (WeaponCardFirst.On) card.Close(false);
            }
            WeaponUi.Badge(new Rect(b.x + 6f, b.center.y - 9f, 32f, 18f), r.Rating.Overall, null);
            UiKit.LabelFit(new Rect(b.x + 44f, b.y, b.width - 48f, b.height), label, Color.white);
            TooltipHandler.TipRegion(b, "CC_Weapon_OpenCard_Tip".Translate());
        }
    }

    /// <summary>The weapon rating's entries at the top of the game's info card (WeaponInfoCard.Entries).</summary>
    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
    public static class Patch_WeaponInfoCardStats
    {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance, StatRequest req)
        {
            if (__result != null)
                foreach (var e in __result)
                    yield return e;
            List<StatDrawEntry> more = null;
            if (WeaponInfoCard.On && WeaponLive.Rateable(__instance))
                try
                {
                    more = WeaponInfoCard.Entries(__instance, req);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the weapon rating on the info card", e);
                }
            if (more != null)
                foreach (var e in more)
                    yield return e;
        }
    }

    /// <summary>The button to Rimfolk's weapon card at the foot of the game's info card (WeaponInfoCard.Button).</summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    public static class Patch_WeaponInfoCardButton
    {
        private static bool broken;

        public static void Postfix(Dialog_InfoCard __instance, Rect inRect)
        {
            if (broken || !WeaponInfoCard.On) return;
            try
            {
                WeaponInfoCard.Button(__instance, inRect);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the weapon card's button on the info card", e);
            }
        }
    }
}
