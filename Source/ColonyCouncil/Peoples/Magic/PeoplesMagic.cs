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

        /// <summary>
        /// A people whose casters Rimfolk knows: one of Rimfolk's own, or another mod's it gives classes to (Peoples/Foreign/
        /// ForeignPeoples.cs: the Stormriders' dark elves).
        /// </summary>
        public static bool Ours(Faction f) => Peoples.IsOurs(f) || ForeignPeoples.Has(f);

        /// <summary>A people built on magic: by its calling (PeoplesMagicMath.BuiltOnMagic), or another mod's people that is (a coven).</summary>
        public static bool OnMagic(Faction f) => PeoplesMagicMath.BuiltOnMagic(Peoples.CallingOf(f)) || ForeignPeoples.Of(f)?.builtOnMagic == true;

        /// <summary>A caster of one of those peoples (<see cref="Ours"/>), not of the colony: their class, or null.</summary>
        public static string TheirCraft(Pawn p) =>
            p?.Faction == null || p.Faction.IsPlayer || !Ours(p.Faction) || p.RaceProps?.Humanlike != true ? null : Craft(p);

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
            if (!Loaded || f?.def == null || !Ours(f)) return new List<string>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (among.TryGetValue(f, out var kept) && now - kept.at < Sched.HourTicks && now >= kept.at) return kept.crafts;
            var list = new List<string>();
            try
            {
                int age = Peoples.AgeOf(f);
                var gifts = new List<(string, float)>();
                if (Peoples.IsOurs(f))
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
                else
                    // Another mod's people: its roles' classes (Peoples/Foreign/ForeignPeoples.cs).
                    foreach (var (trait, share) in ForeignPeoples.ClassShares(f))
                        if (DefDatabase<TraitDef>.GetNamedSilentFail(trait) != null && PeopleGifts.ClassOn(trait, f))
                            gifts.Add((trait, share));
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
                string key = OnMagic(f) ? "CC_PeoplesMagic_Card_Many" : "CC_PeoplesMagic_Card_Few";
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
        /// A spell or technique the peoples' casters never use (PeoplesMagicMath.KeptOut): any pawn of Rimfolk's peoples, or of
        /// another mod's people Rimfolk gives classes to, not of the colony (a visitor, an ally, a raider, a town's defender). One of them who joins the colony is the
        /// colony's own and the mod's own rules hold for them.
        /// </summary>
        public static bool KeptFrom(Pawn p, Def ability)
        {
            if (p?.Faction == null || ability == null || p.Faction.IsPlayer) return false;
            return PeoplesMagicMath.IsKeptOut(ability.defName) && Ours(p.Faction);
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

        // ================= For the colony's own casters (Integrations/MagicAutoCast.cs) =================

        private static bool lookedOwn;
        private static Type mightComp, tmAbilityDef;
        private static PropertyInfo isMightUser, mightAbilityData, verbOf, manaNeed, staminaNeed;
        private static MethodInfo manaCostOf, staminaCostOf;
        private static FieldInfo defManaCost, defStaminaCost;

        /// <summary>
        /// The rest of what the colony's own casters need, read by name: the might component (TorannMagic.CompAbilityUserMight:
        /// IsMightUser, AbilityData, Stamina, ActualStaminaCost), the magic component's Mana and ActualManaCost, an
        /// ability's verb (AbilityUser.PawnAbility.Verb) and a TMAbilityDef's manaCost and staminaCost.
        /// </summary>
        private static void LookOwn()
        {
            if (lookedOwn) return;
            lookedOwn = true;
            Look();
            try
            {
                tmAbilityDef = AccessTools.TypeByName("TorannMagic.TMAbilityDef");
                // The might component on its own: if it can't be read, the spells (mana) still can.
                try
                {
                    mightComp = AccessTools.TypeByName("TorannMagic.CompAbilityUserMight");
                    if (mightComp != null)
                    {
                        isMightUser = AccessTools.Property(mightComp, "IsMightUser");
                        mightAbilityData = AccessTools.Property(mightComp, "AbilityData");
                        staminaNeed = AccessTools.Property(mightComp, "Stamina");
                        if (tmAbilityDef != null) staminaCostOf = AccessTools.Method(mightComp, "ActualStaminaCost", new[] { tmAbilityDef });
                        if (isMightUser?.PropertyType != typeof(bool)) isMightUser = null;
                    }
                }
                catch (Exception e)
                {
                    isMightUser = null;
                    mightAbilityData = staminaNeed = null;
                    staminaCostOf = null;
                    CouncilLog.ErrorOnce("casting on their own: reading A RimWorld of Magic's might", e);
                }
                if (magicComp != null)
                {
                    manaNeed = AccessTools.Property(magicComp, "Mana");
                    if (tmAbilityDef != null) manaCostOf = AccessTools.Method(magicComp, "ActualManaCost", new[] { tmAbilityDef });
                }
                if (abilityType != null) verbOf = AccessTools.Property(abilityType, "Verb");
                if (verbOf != null && !typeof(Verb).IsAssignableFrom(verbOf.PropertyType)) verbOf = null;
                if (tmAbilityDef != null)
                {
                    defManaCost = AccessTools.Field(tmAbilityDef, "manaCost");
                    defStaminaCost = AccessTools.Field(tmAbilityDef, "staminaCost");
                }
                if (manaCostOf != null && manaCostOf.ReturnType != typeof(float)) manaCostOf = null;
                if (staminaCostOf != null && staminaCostOf.ReturnType != typeof(float)) staminaCostOf = null;
            }
            catch (Exception e)
            {
                verbOf = null;
                CouncilLog.ErrorOnce("casting on their own: reading A RimWorld of Magic", e);
            }
        }

        /// <summary>The colony's own casters can be served: the mod's casting, the abilities' verbs and the pools can be read.</summary>
        public static bool OwnReady
        {
            get
            {
                LookOwn();
                return Ready && verbOf != null && (manaNeed != null || staminaNeed != null);
            }
        }

        private static ThingComp CompOf(Pawn p, Type type)
        {
            if (p?.AllComps == null || type == null) return null;
            foreach (var c in p.AllComps)
                if (c != null && type.IsInstanceOfType(c))
                    return c;
            return null;
        }

        /// <summary>One ability a caster holds: the mod's object, its def, and whether the might component holds it (stamina) or the magic one (mana).</summary>
        public struct Held
        {
            public object ability;
            public Def def;
            public bool might;
        }

        /// <summary>Every ability a caster holds in the mod's magic and might components, the magic ones first; empty without the mod.</summary>
        public static List<Held> HeldBy(Pawn p)
        {
            var list = new List<Held>();
            if (!OwnReady || p == null) return list;
            try
            {
                Add(list, CompOf(p, magicComp), isMagicUser, abilityData, false);
                Add(list, CompOf(p, mightComp), isMightUser, mightAbilityData, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("casting on their own: a caster's abilities", e);
            }
            return list;
        }

        private static void Add(List<Held> list, ThingComp comp, PropertyInfo user, PropertyInfo data, bool might)
        {
            if (comp == null || user == null || data == null || !(user.GetValue(comp) is bool on) || !on) return;
            var d = data.GetValue(comp);
            if (d == null) return;
            var powersOf = powers != null && powers.DeclaringType != null && powers.DeclaringType.IsInstanceOfType(d) ? powers : AccessTools.Property(d.GetType(), "Powers");
            if (!(powersOf?.GetValue(d) is IEnumerable all)) return;
            foreach (var a in all)
                if (a != null && abilityDef.GetValue(a) is Def def)
                    list.Add(new Held { ability = a, def = def, might = might });
        }

        /// <summary>The ability's verb (its range and line of sight are the mod's own), or null.</summary>
        public static Verb VerbOf(object ability)
        {
            if (!OwnReady || ability == null) return null;
            try
            {
                return verbOf.GetValue(ability) as Verb;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The caster's mana (magic) or stamina (might), or null.</summary>
        public static Need Pool(Pawn p, bool might)
        {
            if (!OwnReady || p == null) return null;
            try
            {
                var prop = might ? staminaNeed : manaNeed;
                var comp = CompOf(p, might ? mightComp : magicComp);
                return comp == null || prop == null ? null : prop.GetValue(comp) as Need;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// What casting the ability costs this caster in mana (magic) or stamina (might), as the mod reckons it (its
        /// efficiency skills and modifiers), else the def's own cost; -1 when it can't be read.
        /// </summary>
        public static float Cost(Pawn p, Def def, bool might)
        {
            if (!OwnReady || p == null || def == null || tmAbilityDef == null || !tmAbilityDef.IsInstanceOfType(def)) return -1f;
            try
            {
                var comp = CompOf(p, might ? mightComp : magicComp);
                var method = might ? staminaCostOf : manaCostOf;
                if (comp != null && method != null && method.Invoke(comp, new object[] { def }) is float c) return c;
                var field = might ? defStaminaCost : defManaCost;
                return field?.GetValue(def) is float f ? f : -1f;
            }
            catch
            {
                var field = might ? defStaminaCost : defManaCost;
                try
                {
                    return field?.GetValue(def) is float f ? f : -1f;
                }
                catch
                {
                    return -1f;
                }
            }
        }

        private static List<JobDef> castJobs;

        /// <summary>The jobs the mod and its ability framework cast with (TMCastAbilityVerb, TMCastAbilitySelf, CastAbilityVerb, CastAbilitySelf), those loaded.</summary>
        public static List<JobDef> CastJobs
        {
            get
            {
                if (castJobs != null) return castJobs;
                castJobs = new List<JobDef>();
                foreach (var name in new[] { "TMCastAbilityVerb", "TMCastAbilitySelf", "CastAbilityVerb", "CastAbilitySelf" })
                {
                    var d = DefDatabase<JobDef>.GetNamedSilentFail(name);
                    if (d != null) castJobs.Add(d);
                }
                return castJobs;
            }
        }
    }
}
