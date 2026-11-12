using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic's classes and spells among Rimfolk's peoples, seen (the player's request, 12 November 2026;
    /// DESIGN.md "Magic among the peoples, seen"; the rules in PeoplesMagicMath). A caster of one of the peoples looks like
    /// one (their class's own hood, helm, cloak or wand from the mod, by their people's age, given as they are made); says
    /// so (a visitor names their craft to the first of the colony they meet, the colony remarks on it, and names a raider's
    /// craft when one is seen); is known for it (the people's card and their letters: "a few among them are druids");
    /// now and then casts a helpful spell for the colony in peace (Peoples/Magic/PeoplesMagicWatch); and in a fight casts by the
    /// mod's own AI and settings, with the mod's most broken spells kept out of the peoples' hands
    /// (Peoples/Magic/PeoplesMagicPatches.cs). Everything of the mod is read by name: without it nothing here finds anything.
    /// </summary>
    public static class PeoplesMagic
    {
        private static bool looked, loaded;

        /// <summary>A RimWorld of Magic is loaded (its magic component is there to be read).</summary>
        public static bool Loaded
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    loaded = AccessTools.TypeByName("TorannMagic.CompAbilityUserMagic") != null;
                }
                return loaded;
            }
        }

        // ================= Who is a caster, and of what =================

        /// <summary>Their class of A RimWorld of Magic (its trait's defName, one Rimfolk knows), or null.</summary>
        public static string Craft(Pawn p)
        {
            var traits = p?.story?.traits?.allTraits;
            if (traits == null) return null;
            foreach (var t in traits)
                if (t?.def != null && PeoplesGearMath.MagicClasses.ContainsKey(t.def.defName))
                    return t.def.defName;
            return null;
        }

        /// <summary>A caster of one of Rimfolk's peoples, not of the colony: their class, or null.</summary>
        public static string TheirCraft(Pawn p) =>
            p?.Faction == null || p.Faction.IsPlayer || !Peoples.IsOurs(p.Faction) || p.RaceProps?.Humanlike != true ? null : Craft(p);

        /// <summary>The craft in words, one ("druid") or many ("druids"); the trait's own label where Rimfolk has no word for it.</summary>
        public static string Word(string trait, bool plural)
        {
            if (trait == null) return "";
            string key = (plural ? "CC_PeoplesMagic_Crafts_" : "CC_PeoplesMagic_Craft_") + trait;
            if (key.CanTranslate()) return key.Translate().Resolve();
            string label = DefDatabase<TraitDef>.GetNamedSilentFail(trait)?.degreeDatas?.FirstOrDefault()?.label ?? trait;
            return label.ToLowerInvariant();
        }

        /// <summary>One of them with its article ("a druid", "an arcanist").</summary>
        public static string WordWithArticle(string trait) => Find.ActiveLanguageWorker.WithIndefiniteArticle(Word(trait, false));

        // ================= A caster looks like one =================

        /// <summary>
        /// A new pawn of the peoples with a class: their class's own piece from the mod for their people's age
        /// (PeoplesMagicMath.Garb), worn in place of whatever it can't be worn with and dyed the people's colour when it
        /// takes a signature piece's place; a wand only for one already armed (in place of their weapon), else the class's
        /// hood. Nothing for a class with no piece of its own, or without the mod.
        /// </summary>
        public static void Dress(Pawn p, int age)
        {
            if (!Loaded) return;
            try
            {
                string trait = Craft(p);
                string name = PeoplesMagicMath.Garb(trait, age);
                if (name == null) return;
                bool armed = p.equipment?.Primary != null && !p.WorkTagIsDisabled(WorkTags.Violent);
                if (PeoplesMagicMath.Wand(name) && !armed) name = PeoplesMagicMath.Garb(trait, 3);
                var def = name == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) return;
                if (PeoplesMagicMath.Wand(name)) Arm(p, def);
                else Wear(p, def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a caster's look", e);
            }
        }

        private static void Wear(Pawn p, ThingDef def)
        {
            if (p.apparel == null || def.apparel == null || !def.apparel.PawnCanWear(p)) return;
            ThingDef stuff = null;
            if (def.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, Peoples.TechOf(p.Faction))) return;
            if (!(ThingMaker.MakeThing(def, stuff) is Apparel a)) return;
            a.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
            // The people's colour: the class's piece taking a signature piece's place (a druid's hood for the Mossborn's
            // green wreath, a priest's for the Bell's grey veil) wears its colour.
            Color? colour = null;
            foreach (var worn in p.apparel.WornApparel.ToList())
            {
                if (ApparelUtility.CanWearTogether(def, worn.def, p.RaceProps.body)) continue;
                var mark = p.kindDef.specificApparelRequirements?.FirstOrDefault(r => r != null && r.ApparelDef == worn.def && r.Color != default);
                if (mark != null && colour == null) colour = mark.Color;
                p.apparel.Remove(worn);
                worn.Destroy();
            }
            if (colour.HasValue) a.SetColor(colour.Value, false);
            p.apparel.Wear(a, false);
        }

        private static void Arm(Pawn p, ThingDef def)
        {
            if (!(ThingMaker.MakeThing(def) is ThingWithComps wand)) return;
            wand.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
            p.equipment.DestroyAllEquipment();
            p.equipment.AddEquipment(wand);
        }

        // ================= Known for it: the card and the letters =================

        private static readonly Dictionary<Faction, (int at, List<string> crafts)> among = new Dictionary<Faction, (int, List<string>)>();

        /// <summary>
        /// The crafts a people of Rimfolk's is known for (their classes), the commonest first: from its kinds' gifts that
        /// fit the age it lives in, each class only while the mod has it switched on and its own share for the people isn't
        /// none. Worked out once an hour for each people. Empty without the mod.
        /// </summary>
        public static List<string> Among(Faction f)
        {
            if (!Loaded || f?.def == null || !Peoples.IsOurs(f)) return new List<string>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (among.TryGetValue(f, out var kept) && now - kept.at < Sched.HourTicks && now >= kept.at) return kept.crafts;
            var list = new List<string>();
            try
            {
                int age = Peoples.AgeOf(f);
                var gifts = new List<(string, float)>();
                foreach (var kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
                {
                    if (kind?.defaultFactionDef != f.def) continue;
                    var ext = kind.GetModExtension<PeopleKindExtension>();
                    if (ext?.gifts == null) continue;
                    foreach (var g in ext.gifts)
                        if (g != null && !g.trait.NullOrEmpty() && PeoplesDMath.AgeFits(age, g.ages.min, g.ages.max)
                            && DefDatabase<TraitDef>.GetNamedSilentFail(g.trait) != null && PeopleGifts.ClassOn(g.trait, f))
                            gifts.Add((g.trait, g.chance));
                }
                list = PeoplesMagicMath.Among(gifts);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: what a people is known for", e);
            }
            among[f] = (now, list);
            return list;
        }

        /// <summary>Their crafts in words, many of each ("druids and rangers"), or null when they have none.</summary>
        public static string AmongWords(Faction f)
        {
            var crafts = Among(f);
            return crafts.Count == 0 ? null : TextUtil.JoinAnd(crafts.Select(t => Word(t, true)).ToList());
        }

        /// <summary>The people's card: "A few among them are druids and rangers." (many, for a people built on magic), or null.</summary>
        public static List<string> CardLines(Faction f)
        {
            try
            {
                string words = AmongWords(f);
                if (words == null) return null;
                string key = PeoplesMagicMath.BuiltOnMagic(Peoples.CallingOf(f)) ? "CC_PeoplesMagic_Card_Many" : "CC_PeoplesMagic_Card_Few";
                return new List<string> { key.Translate(words.Named("CRAFTS")).Resolve() };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: their card", e);
                return null;
            }
        }

        /// <summary>A letter from one of the peoples: their crafts for its lines to mention now and then ({AMONG}, among:magic).</summary>
        public static void LetterTags(Faction f, ICollection<string> tags, IDictionary<string, string> values)
        {
            if (tags == null || values == null) return;
            try
            {
                string words = AmongWords(f);
                if (words == null) return;
                tags.Add("among:magic");
                values["AMONG"] = words;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: their letters", e);
            }
        }

        /// <summary>Forget what was worked out (a new game, a load).</summary>
        public static void Reset() => among.Clear();

        // ================= Kept out =================

        /// <summary>
        /// A spell or technique the peoples' casters never use (PeoplesMagicMath.KeptOut): any of Rimfolk's peoples' pawns
        /// not of the colony (a visitor, an ally, a raider, a town's defender). One of them who joins the colony is the
        /// colony's own and the mod's own rules hold for them.
        /// </summary>
        public static bool KeptFrom(Pawn p, Def ability)
        {
            if (p?.Faction == null || ability == null || p.Faction.IsPlayer) return false;
            return PeoplesMagicMath.IsKeptOut(ability.defName) && Peoples.IsOurs(p.Faction);
        }
    }

    /// <summary>The people's card: what crafts are among them (Peoples/Magic/PeoplesMagic.cs).</summary>
    [StaticConstructorOnStartup]
    public static class PeoplesMagicCard
    {
        static PeoplesMagicCard()
        {
            CardSections.RegisterLines(CardKind.People, CardSlot.Story, 11f, r => PeoplesMagic.CardLines(ThingRefs.Faction(r)), null, "peoples-magic");
        }
    }

    /// <summary>
    /// A RimWorld of Magic's casting, read by name (its own entry points, so its effects, sounds, mana and cooldowns
    /// apply): a pawn's magic component (TorannMagic.CompAbilityUserMagic: IsMagicUser, AbilityData.Powers), an ability's
    /// def and cooldown (AbilityUser.PawnAbility: Def, CooldownTicksLeft), the mod's own check that it can be cast now
    /// (CanCastPowerCheck: mana, cooldown) and its job to cast it (GetJob, as the mod's own casting for those not of the
    /// colony asks it, AbilityContext 1). Anything not where it is expected: nothing is cast.
    /// </summary>
    internal static class RomSpells
    {
        private static bool looked;
        private static Type magicComp, abilityType, contextType;
        private static PropertyInfo isMagicUser, abilityData, powers, abilityDef, cooldown;
        private static MethodInfo canCast, getJob;
        private static object aiContext;
        private static PropertyInfo aiCasting;
        private static FieldInfo aiCastingField, settingsInstance;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                magicComp = AccessTools.TypeByName("TorannMagic.CompAbilityUserMagic");
                abilityType = AccessTools.TypeByName("AbilityUser.PawnAbility");
                contextType = AccessTools.TypeByName("AbilityUser.AbilityContext");
                if (magicComp == null || abilityType == null || contextType == null || !contextType.IsEnum) return;
                isMagicUser = AccessTools.Property(magicComp, "IsMagicUser");
                abilityData = AccessTools.Property(magicComp, "AbilityData");
                powers = abilityData == null ? null : AccessTools.Property(abilityData.PropertyType, "Powers");
                abilityDef = AccessTools.Property(abilityType, "Def");
                cooldown = AccessTools.Property(abilityType, "CooldownTicksLeft");
                canCast = AccessTools.Method(abilityType, "CanCastPowerCheck", new[] { contextType, typeof(string).MakeByRefType() });
                getJob = AccessTools.Method(abilityType, "GetJob", new[] { contextType, typeof(LocalTargetInfo) });
                aiContext = Enum.ToObject(contextType, 1);
                var settings = AccessTools.TypeByName("TorannMagic.ModOptions.Settings");
                settingsInstance = settings == null ? null : AccessTools.Field(settings, "Instance");
                aiCastingField = settings == null ? null : AccessTools.Field(settings, "AICasting");
                if (isMagicUser?.PropertyType != typeof(bool) || cooldown?.PropertyType != typeof(int) || canCast?.ReturnType != typeof(bool) || getJob?.ReturnType != typeof(Job)
                    || powers == null || abilityDef == null)
                    getJob = null;
            }
            catch (Exception e)
            {
                getJob = null;
                CouncilLog.ErrorOnce("peoples' magic: reading A RimWorld of Magic's casting", e);
            }
        }

        /// <summary>The mod's casting can be reached.</summary>
        public static bool Ready
        {
            get
            {
                Look();
                return getJob != null;
            }
        }

        /// <summary>
        /// The mod lets those not of the colony cast (its setting "AI can cast"; on as the mod ships it, and when it can't
        /// be read): Rimfolk asks no visitor to cast with it off.
        /// </summary>
        public static bool AiMayCast
        {
            get
            {
                Look();
                try
                {
                    var s = settingsInstance?.GetValue(null);
                    return s == null || aiCastingField == null || !(aiCastingField.GetValue(s) is bool on) || on;
                }
                catch
                {
                    return true;
                }
            }
        }

        private static ThingComp CompOf(Pawn p)
        {
            if (p?.AllComps == null || magicComp == null) return null;
            foreach (var c in p.AllComps)
                if (c != null && magicComp.IsInstanceOfType(c))
                    return c;
            return null;
        }

        /// <summary>A caster's ability of this name (any of its levels), as the mod holds it, or null.</summary>
        public static object Ability(Pawn p, string spell)
        {
            if (!Ready || spell == null) return null;
            try
            {
                var comp = CompOf(p);
                if (comp == null || !(isMagicUser.GetValue(comp) is bool user) || !user) return null;
                var data = abilityData.GetValue(comp);
                if (data == null || !(powers.GetValue(data) is IEnumerable list)) return null;
                foreach (var a in list)
                    if (a != null && abilityDef.GetValue(a) is Def d && PeoplesMagicMath.BaseSpell(d.defName) == spell)
                        return a;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a caster's abilities", e);
            }
            return null;
        }

        /// <summary>Every ability a caster holds, by the mod's names (for the dev log).</summary>
        public static List<string> Abilities(Pawn p)
        {
            var names = new List<string>();
            if (!Ready) return names;
            try
            {
                var comp = CompOf(p);
                if (comp == null || !(isMagicUser.GetValue(comp) is bool user) || !user) return names;
                if (abilityData.GetValue(comp) is object data && powers.GetValue(data) is IEnumerable list)
                    foreach (var a in list)
                        if (a != null && abilityDef.GetValue(a) is Def d)
                            names.Add(d.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a caster's abilities", e);
            }
            return names;
        }

        /// <summary>The mod's own check that the ability can be cast now (mana, cooldown).</summary>
        public static bool CanCast(object ability)
        {
            if (!Ready || ability == null) return false;
            try
            {
                var args = new[] { aiContext, null };
                return canCast.Invoke(ability, args) is bool ok && ok;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: can it be cast", e);
                return false;
            }
        }

        /// <summary>The ability's cooldown left, in ticks (above 0 once it has been cast), or -1.</summary>
        public static int Cooldown(object ability)
        {
            if (!Ready || ability == null) return -1;
            try
            {
                return cooldown.GetValue(ability) is int t ? t : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>The mod's job to cast the ability on the target (it walks into range and casts), or null.</summary>
        public static Job JobFor(object ability, LocalTargetInfo target)
        {
            if (!Ready || ability == null) return null;
            try
            {
                return getJob.Invoke(ability, new[] { aiContext, (object)target }) as Job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: the job to cast", e);
                return null;
            }
        }

        /// <summary>
        /// The pawn and def of one of the mod's abilities (AbilityUser.PawnAbility.Pawn and Def), for the guard that keeps
        /// spells out of the peoples' hands; null when they can't be read.
        /// </summary>
        public static (Pawn pawn, Def def) Of(object ability)
        {
            if (ability == null) return (null, null);
            try
            {
                var type = ability.GetType();
                if (!owners.TryGetValue(type, out var props))
                {
                    props = (AccessTools.Property(type, "Pawn"), AccessTools.Property(type, "Def"));
                    owners[type] = props;
                }
                return (props.pawn?.GetValue(ability) as Pawn, props.def?.GetValue(ability) as Def);
            }
            catch
            {
                return (null, null);
            }
        }

        private static readonly Dictionary<Type, (PropertyInfo pawn, PropertyInfo def)> owners = new Dictionary<Type, (PropertyInfo, PropertyInfo)>();
    }
}
