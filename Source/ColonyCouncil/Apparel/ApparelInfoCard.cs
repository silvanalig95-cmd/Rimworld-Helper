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
    /// The apparel rating at the top of the game's own info card (its own stat category, sorted first, as the weapons'):
    /// the rating with whom it is rated for, a shooter's and a melee fighter's where they differ, the sub-ratings, the line
    /// in plain words, each tag with why, the effects counted or named, the piece as it is (its wear and the moods it gives),
    /// and the rating for the colonist who wears or carries it. Each entry's explanation is the card's report pane. A button
    /// at the card's foot opens Rimfolk's apparel card.
    /// </summary>
    public static class ApparelInfoCard
    {
        private static StatCategoryDef category;
        private static bool looked;

        /// <summary>The category the entries go in (Defs/Misc/Rimfolk_ApparelStats.xml), or null.</summary>
        public static StatCategoryDef Category
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    category = DefDatabase<StatCategoryDef>.GetNamedSilentFail("Rimfolk_ApparelRating");
                }
                return category;
            }
        }

        public static bool On => RimfolkPatches.On && ApparelLive.On;

        private static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>The entries for a piece, its thing or its kind in a stuff and at a quality.</summary>
        public static List<StatDrawEntry> Entries(ThingDef def, StatRequest req)
        {
            var cat = Category;
            if (cat == null) return null;
            var r = req.HasThing ? ApparelLive.Of(req.Thing) : ApparelLive.Of(def, req.StuffDef, req.QualityCategory);
            if (r?.Rating == null) return null;
            var list = new List<StatDrawEntry>();
            int prio = 5000;
            var g = r.Rating;
            var expl = new StringBuilder();
            expl.AppendLine("CC_Apparel_Info_RatingExpl".Translate());
            expl.AppendLine();
            expl.AppendLine("CC_Apparel_Info_RatedAs".Translate(RoleOf(g).Named("ROLE"), ApparelUi.ForWords(g.For).Named("FOR")).Resolve());
            if (!r.StuffLabel.NullOrEmpty()) expl.AppendLine("CC_Weapon_Info_Stuff".Translate(r.StuffLabel.Named("STUFF")).Resolve());
            if (r.Rough)
            {
                expl.AppendLine();
                expl.AppendLine("CC_Weapon_Info_Rough".Translate());
                foreach (var u in r.Facts.Unsure) expl.AppendLine("  - " + u);
            }
            list.Add(new StatDrawEntry(cat, "CC_Apparel_Info_Rating".Translate(),
                F1(g.Overall) + " (" + WeaponWords.Quality(g.Overall) + (r.Rough ? ", " + "CC_Weapon_Rough".Translate() : "") + ")", expl.ToString().TrimEnd(), prio--));

            // A shooter's and a melee fighter's, where they differ.
            if (ApparelForMath.BothDiffer(g))
                list.Add(new StatDrawEntry(cat, "CC_Apparel_Info_Both".Translate(), F1(g.OverallShooter) + " / " + F1(g.OverallMelee), "CC_Apparel_Info_BothExpl".Translate(), prio--));

            // The sub-ratings.
            foreach (var k in ApparelForMath.SubKeys(g))
                list.Add(new StatDrawEntry(cat, ApparelUi.SubWords(k), F1(g.Sub[k]), ApparelUi.SubTip(k), prio--));

            // The line in plain words.
            if (!r.Plain.NullOrEmpty())
                list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_Plain".Translate(), r.Plain, "CC_Apparel_Info_PlainExpl".Translate() + "\n\n" + r.Plain, prio--));

            // Each tag, coloured by its category, with why.
            foreach (var t in r.Tags)
            {
                var style = WeaponTagMath.Style(t.Category);
                var def0 = ApparelTagMath.Def(t.Id);
                string why = t.Why.CapitalizeFirst() + "." + (def0 != null ? "\n\n" + "CC_Weapon_Info_TagRule".Translate(def0.Rule.Named("RULE")).Resolve() : "");
                list.Add(new StatDrawEntry(cat, t.Label.Colorize(WeaponUi.Colour(style.Colour)), WeaponInfoCard.CategoryWord(t.Category), why, prio--));
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
                    sb.AppendLine("CC_Apparel_Info_Named".Translate());
                    foreach (var s in named) sb.AppendLine("  - " + s.Label);
                }
                list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_Effects".Translate(),
                    "CC_Weapon_Info_EffectsValue".Translate(counted.Count.Named("COUNTED"), named.Count.Named("NAMED")).Resolve(), sb.ToString().TrimEnd(), prio--));
            }

            // The piece as it is: its wear.
            if (req.HasThing && req.Thing.def.useHitPoints && req.Thing.MaxHitPoints > 0)
            {
                float share = (float)req.Thing.HitPoints / req.Thing.MaxHitPoints;
                list.Add(new StatDrawEntry(cat, "CC_Apparel_Info_Wear".Translate(), share.ToStringPercent(), WearExpl(r, req.Thing), prio--));
            }

            // For the colonist who wears or carries it.
            var who = req.HasThing ? ApparelLive.Holder(req.Thing) : null;
            if (who != null && who.RaceProps.Humanlike)
            {
                string cant = ApparelLive.CantWear(who, req.Thing, req.Thing.def);
                if (cant != null)
                    list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_For".Translate(who.Named("PAWN")).Resolve(), "CC_Apparel_CantWear".Translate(), cant, prio--));
                else
                {
                    var (_, fc) = ApparelLive.ForColonist(who, r, req.Thing);
                    if (fc != null)
                    {
                        var steps = ApparelForMath.Telling(fc);
                        string text = "CC_Apparel_Info_ForExpl".Translate(who.Named("PAWN"), F1(fc.Rating).Named("RATING"), F1(fc.Reference).Named("REFERENCE")).Resolve() +
                                      (steps.Count > 0 ? "\n\n" + string.Join("\n", steps.Select(s => "  " + ApparelUi.StepWords(s.why) + ": " + WeaponForMath.Delta(s.delta))) : "");
                        list.Add(new StatDrawEntry(cat, "CC_Weapon_Info_For".Translate(who.Named("PAWN")).Resolve(), F1(fc.Rating), text, prio--));
                    }
                }
            }
            return list;
        }

        /// <summary>The role, or what a piece made as armour is named for (armour for the hands, an armoured cloak).</summary>
        public static string RoleOf(ApparelRating g) => ApparelUi.RoleWords(g.Role);

        /// <summary>What its wear means: the game's thresholds for the apparel-damaged mood, how many days of wear to them.</summary>
        public static string WearExpl(ApparelReport r, Thing t)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CC_Apparel_WearExpl".Translate(t.HitPoints.Named("HP"), t.MaxHitPoints.Named("MAX")).Resolve());
            if (!(t.def.apparel?.careIfDamaged ?? true)) sb.AppendLine("CC_Apparel_WearNoCare".Translate().Resolve());
            else
            {
                sb.AppendLine("CC_Apparel_WearThresholds".Translate(ApparelRead.FrayedBelow.ToStringPercent().Named("FRAYED"), ApparelRead.TatteredBelow.ToStringPercent().Named("TATTERED")).Resolve());
                var days = ApparelRead.DaysOfWear(t.HitPoints, t.MaxHitPoints, t.def.apparel?.wearPerDay ?? 0f, ApparelRead.FrayedBelow);
                if (days.HasValue) sb.AppendLine("CC_Apparel_WearDays".Translate(Mathf.RoundToInt(days.Value).Named("DAYS"), ApparelRead.FrayedBelow.ToStringPercent().Named("AT")).Resolve());
            }
            return sb.ToString().TrimEnd();
        }

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

        /// <summary>"Rimfolk's apparel card" at the foot of the game's card, left of its close button, for a piece the rating reads.</summary>
        public static void Button(Dialog_InfoCard card, Rect inRect)
        {
            if (!ApparelCardWindow.On || CardThing == null || CardDef == null) return;
            var thing = CardThing(card);
            var def = thing?.def ?? CardDef(card) as ThingDef;
            if (!ApparelLive.Rateable(def) || thing is Pawn) return;
            var r = thing != null ? ApparelLive.Of(thing) : ApparelLive.Of(def, CardStuff?.Invoke(card));
            if (r?.Rating == null) return;
            string label = "CC_Apparel_OpenCard".Translate();
            Text.Font = GameFont.Small;
            float w = Mathf.Min(260f, Text.CalcSize(label).x + 54f);
            var b = new Rect(inRect.x, inRect.yMax - 36f, w, 34f);
            if (UiKit.Button(b, ""))
            {
                if (thing != null) ApparelCardWindow.Open(thing, ApparelLive.Holder(thing));
                else ApparelCardWindow.Open(def, CardStuff?.Invoke(card), null);
                // With Rimfolk's card opening first, the two cards are two ways of looking: going over closes the game's.
                if (ApparelCardFirst.On) card.Close(false);
            }
            WeaponUi.Badge(new Rect(b.x + 6f, b.center.y - 9f, 32f, 18f), r.Rating.Overall, null);
            UiKit.LabelFit(new Rect(b.x + 44f, b.y, b.width - 48f, b.height), label, Color.white);
            TooltipHandler.TipRegion(b, "CC_Apparel_OpenCard_Tip".Translate());
        }
    }

    /// <summary>The apparel rating's entries at the top of the game's info card (ApparelInfoCard.Entries).</summary>
    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
    public static class Patch_ApparelInfoCardStats
    {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance, StatRequest req)
        {
            if (__result != null)
                foreach (var e in __result)
                    yield return e;
            List<StatDrawEntry> more = null;
            if (ApparelInfoCard.On && ApparelLive.Rateable(__instance))
                try
                {
                    more = ApparelInfoCard.Entries(__instance, req);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the apparel rating on the info card", e);
                }
            if (more != null)
                foreach (var e in more)
                    yield return e;
        }
    }

    /// <summary>The button to Rimfolk's apparel card at the foot of the game's info card (ApparelInfoCard.Button).</summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    public static class Patch_ApparelInfoCardButton
    {
        private static bool broken;

        public static void Postfix(Dialog_InfoCard __instance, Rect inRect)
        {
            if (broken || !ApparelInfoCard.On) return;
            try
            {
                ApparelInfoCard.Button(__instance, inRect);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the apparel card's button on the info card", e);
            }
        }
    }
}
