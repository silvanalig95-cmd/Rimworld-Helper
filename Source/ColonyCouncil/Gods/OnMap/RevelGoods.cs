using System;
using System.Collections.Generic;
using System.Linq;
using Verse.AI;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Revel's things, every age in its own form (the player's rules: "every age its form" and "always draw from the full mod
    /// pool"): the colony's own drink and drugs, read from every def loaded (the game's and the DLCs' beer, smokeleaf,
    /// psychite tea and the rest; Vanilla Brewing Expanded's spirits, coffees and teas; Drugs+; Vanilla Factions' opium,
    /// mead, wine, rum and chemshine; Odyssey's psilocap; and whatever else the player's mods add), never by a list of names.
    /// What the colony has unlocked (made at its own benches and barrels with the research done, or grown) comes first, then
    /// what its age knows (a drug's tech level no higher than the colony's), beer last of all. Medicine is never his.
    /// Who is high now, who is addicted and craving, and what each of them craves, by the game's own hediffs and needs.
    /// </summary>
    public static class RevelGoods
    {
        private static List<ThingDef> all;
        private static HashSet<HediffDef> highs;

        /// <summary>A drink or a drug of his: a drug of the game's kind that isn't medicine alone (social or hard).</summary>
        public static bool His(ThingDef d) => d?.ingestible != null && d.IsDrug && d.ingestible.drugCategory != DrugCategory.Medical && !d.IsMedicine;

        /// <summary>A drink (taken as a fluid: beer, wine, spirits, coffee, tea), not a smoke or a pill.</summary>
        public static bool Drink(ThingDef d) => His(d) && (d.ingestible.foodType & FoodTypeFlags.Fluid) != 0;

        /// <summary>A hard drug (the game's own sorting): never left lying as a find, never a second dose from a craving.</summary>
        public static bool Hard(ThingDef d) => d?.ingestible != null && d.ingestible.drugCategory == DrugCategory.Hard;

        /// <summary>Every drink and drug of his loaded, the game's and every mod's.</summary>
        public static List<ThingDef> All => all ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(His).ToList();

        /// <summary>The drug's own chemical (alcohol, smokeleaf, psychite ...), or null.</summary>
        public static ChemicalDef Chemical(ThingDef d) => d?.GetCompProperties<CompProperties_Drug>()?.chemical;

        /// <summary>The drug's addiction (the game's own hediff), or null for one that has none.</summary>
        public static HediffDef Addiction(ThingDef d) => Chemical(d)?.addictionHediff;

        /// <summary>Its age: its own tech level, else the age of the research its recipes need, else the oldest.</summary>
        public static TechLevel AgeOf(ThingDef d)
        {
            if (d == null) return TechLevel.Undefined;
            if (d.techLevel > TechLevel.Animal) return d.techLevel;
            var research = d.recipeMaker?.researchPrerequisite ?? d.recipeMaker?.researchPrerequisites?.FirstOrDefault();
            return research != null ? research.techLevel : TechLevel.Neolithic;
        }

        /// <summary>The colony's age now (its own means, Avail.EffectiveTech).</summary>
        public static TechLevel ColonyAge => Avail.EffectiveTech;

        /// <summary>Of its age: no newer than the colony, and not a rare luxury (worth more than this a piece).</summary>
        public const float DearFrom = 70f;

        public static bool OfAge(ThingDef d) => His(d) && AgeOf(d) <= ColonyAge && d.BaseMarketValue <= DearFrom;

        /// <summary>The colony can make it again here now (OfferingGifts' reading of its benches, barrels, research and fields).</summary>
        public static bool Unlocked(ThingDef d, Map map) => His(d) && OfferingGifts.MadeAgain(d, map);

        /// <summary>
        /// A drink or a smoke of the colony's kind for a find, a stash or a reveller's cup: one it has unlocked (three times as
        /// likely), else one of its age, never a hard drug; a drink when <paramref name="drink"/>; beer when nothing else fits.
        /// </summary>
        public static ThingDef Pick(Map map, bool drink)
        {
            var fit = All.Where(d => !Hard(d) && (!drink || Drink(d)) && OfAge(d) && d.stackLimit > 1).ToList();
            var chosen = fit.RandomElementByWeightWithFallback(d => map != null && Unlocked(d, map) ? 3f : 1f);
            return chosen ?? ThingDefOf.Beer;
        }

        /// <summary>The stacks of his in the colony's stores at a home, not forbidden.</summary>
        public static IEnumerable<Thing> Held(Map map) =>
            map == null ? Enumerable.Empty<Thing>() : map.listerThings.ThingsInGroup(ThingRequestGroup.Drug).Where(t => t.Spawned && His(t.def) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer));

        /// <summary>A drink in the stores, the largest stack first, or null.</summary>
        public static Thing HeldDrink(Map map) => Held(map).Where(t => Drink(t.def)).OrderByDescending(t => t.stackCount).FirstOrDefault();

        // ================= Who is high, who craves =================

        /// <summary>The hediffs his drugs give (their highs), not their tolerances or addictions.</summary>
        private static HashSet<HediffDef> Highs
        {
            get
            {
                if (highs != null) return highs;
                highs = new HashSet<HediffDef>();
                var chemical = new HashSet<HediffDef>();
                foreach (var c in DefDatabase<ChemicalDef>.AllDefsListForReading)
                {
                    if (c.addictionHediff != null) chemical.Add(c.addictionHediff);
                    if (c.toleranceHediff != null) chemical.Add(c.toleranceHediff);
                }
                foreach (var d in All)
                foreach (var o in d.ingestible.outcomeDoers ?? new List<IngestionOutcomeDoer>())
                    if (o is IngestionOutcomeDoer_GiveHediff give && give.hediffDef != null && !chemical.Contains(give.hediffDef))
                        highs.Add(give.hediffDef);
                return highs;
            }
        }

        /// <summary>The high on someone now (the game's own: drunk, smokeleaf, psychite and every mod's), or null.</summary>
        public static Hediff High(Pawn p)
        {
            var list = p?.health?.hediffSet?.hediffs;
            if (list == null) return null;
            var h = Highs;
            for (int i = 0; i < list.Count; i++)
                if (h.Contains(list[i].def)) return list[i];
            return null;
        }

        /// <summary>Someone's addictions (the game's own), the worst need first.</summary>
        public static IEnumerable<Hediff_Addiction> Addictions(Pawn p) =>
            p?.health?.hediffSet?.hediffs?.OfType<Hediff_Addiction>().OrderBy(a => a.Need?.CurLevel ?? 1f) ?? Enumerable.Empty<Hediff_Addiction>();

        /// <summary>The worst of someone's addictions and how it stands: in withdrawal, craving, or satisfied.</summary>
        public static (Hediff_Addiction addiction, bool withdrawal, bool craving) Craving(Pawn p)
        {
            var a = Addictions(p).FirstOrDefault();
            if (a?.Need == null) return (a, false, false);
            var cat = a.Need.CurCategory;
            return (a, cat == DrugDesireCategory.Withdrawal, cat != DrugDesireCategory.Satisfied);
        }

        /// <summary>A stack in the stores of a drug that answers an addiction (the same chemical), the nearest first, or null.</summary>
        public static Thing For(Map map, Hediff_Addiction a, Pawn near)
        {
            var chem = a?.Chemical;
            if (chem == null) return null;
            return Held(map).Where(t => Chemical(t.def) == chem && (near == null || near.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)))
                .OrderBy(t => near == null ? 0 : t.Position.DistanceToSquared(near.Position)).FirstOrDefault();
        }

        /// <summary>Their drug policy lets them take it for fun (the player's choice for them), or they have none.</summary>
        public static bool AllowedForJoy(Pawn p, ThingDef d)
        {
            if (p?.drugs?.CurrentPolicy == null || d == null) return true;
            try
            {
                return p.drugs.CurrentPolicy[d]?.allowedForJoy ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Their drug policy lets them take it for an addiction.</summary>
        public static bool AllowedForAddiction(Pawn p, ThingDef d)
        {
            if (p?.drugs?.CurrentPolicy == null || d == null) return true;
            try
            {
                return p.drugs.CurrentPolicy[d]?.allowedForAddiction ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Someone who won't touch it: a teetotaler (the game's trait), a child, or one their drug policy keeps from it for fun.</summary>
        public static bool WontTake(Pawn p, ThingDef d)
        {
            if (p == null) return true;
            if (!p.DevelopmentalStage.Adult()) return true;
            var trait = DefDatabase<TraitDef>.GetNamedSilentFail("DrugDesire");
            if (trait != null && p.story?.traits?.DegreeOfTrait(trait) < 0) return true;
            return !AllowedForJoy(p, d);
        }

        /// <summary>A drug binge of the game's own on someone now (the major or the extreme one, and any mod's like it).</summary>
        public static bool Binging(Pawn p)
        {
            var s = p?.MentalStateDef;
            return s != null && s.defName.StartsWith("Binging_Drug", StringComparison.Ordinal);
        }
    }
}
