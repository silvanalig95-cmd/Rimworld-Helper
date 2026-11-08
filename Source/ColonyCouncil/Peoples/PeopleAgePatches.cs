using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's gifts of learning to a people (Peoples/PeopleAges.cs): books, techprints, components and tools in a gift
    /// the game gives (by caravan, in the trade window's gift mode, or by transport pod), remembered by that people as a
    /// deed of their own ("gaveLearning": what was given, weighed by its worth), which the World page lists among what they
    /// remember and their age reads. The game's own goodwill for the gift is as it always was.
    /// </summary>
    public static class PeopleAgeGifts
    {
        public const string DeedKind = "gaveLearning";

        private static float value;
        private static readonly List<string> kinds = new List<string>();
        private static int depth;

        /// <summary>What kind of learning a thing is (PeopleAgesMath.LearnedKind), or null.</summary>
        public static string KindOf(ThingDef d)
        {
            if (d == null) return null;
            bool book = d.thingClass != null && typeof(Book).IsAssignableFrom(d.thingClass);
            bool techprint = d.GetCompProperties<CompProperties_Techprint>() != null;
            return PeopleAgesMath.LearnedKind(d.defName, book, techprint, d.thingCategories?.Select(c => c?.defName), d.weaponTags);
        }

        public static void Begin()
        {
            if (depth++ > 0) return;
            value = 0f;
            kinds.Clear();
        }

        /// <summary>One thing of a gift: counted when it is learning.</summary>
        public static void Thing(Thing t, int count)
        {
            if (depth <= 0 || t == null || count <= 0) return;
            var inner = t.GetInnerIfMinified();
            string kind = KindOf(inner?.def);
            if (kind == null) return;
            value += inner.MarketValue * count;
            if (!kinds.Contains(kind)) kinds.Add(kind);
        }

        /// <summary>The gift is given: the people remembers what of it was learning.</summary>
        public static void End(Faction f)
        {
            if (--depth > 0) return;
            depth = 0;
            try
            {
                int weight = PeopleAgesMath.GiftWeight(value);
                if (weight <= 0 || f == null || kinds.Count == 0) return;
                string what = TextUtil.JoinAnd(kinds.Select(k => ("CC_PeopleAge_Gift_" + k).Translate().Resolve()).ToList());
                FactionMemory.Note(f, DeedKind, what, weight);
            }
            finally
            {
                value = 0f;
                kinds.Clear();
            }
        }
    }

    /// <summary>Each thing of a gift the game hands over (its one private step for every thing of both kinds of gift).</summary>
    [HarmonyPatch(typeof(FactionGiftUtility), "GiveGiftInternal")]
    public static class Patch_PeopleAgeGiftThing
    {
        public static void Prefix(Thing thing, int count)
        {
            if (!RimfolkPatches.On || !PeopleAges.On) return;
            try
            {
                PeopleAgeGifts.Thing(thing, count);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: a gift's thing", e);
            }
        }
    }

    /// <summary>A gift by caravan or in the trade window's gift mode.</summary>
    [HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new[] { typeof(List<Tradeable>), typeof(Faction), typeof(GlobalTargetInfo) })]
    public static class Patch_PeopleAgeGiftTraded
    {
        public static void Prefix()
        {
            if (RimfolkPatches.On && PeopleAges.On) PeopleAgeGifts.Begin();
        }

        public static void Finalizer(Faction giveTo)
        {
            if (!RimfolkPatches.On || !PeopleAges.On) return;
            try
            {
                PeopleAgeGifts.End(giveTo);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: a gift", e);
            }
        }
    }

    /// <summary>A gift by transport pod to one of their towns.</summary>
    [HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new[] { typeof(List<ActiveTransporterInfo>), typeof(Settlement) })]
    public static class Patch_PeopleAgeGiftPods
    {
        public static void Prefix()
        {
            if (RimfolkPatches.On && PeopleAges.On) PeopleAgeGifts.Begin();
        }

        public static void Finalizer(Settlement giveTo)
        {
            if (!RimfolkPatches.On || !PeopleAges.On) return;
            try
            {
                PeopleAgeGifts.End(giveTo?.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: a gift by pod", e);
            }
        }
    }

    /// <summary>
    /// The traders' goods of a people whose age moved (the game reads a trader's caps, set for the people's own age, as it
    /// makes the stock): while the stock of one of Rimfolk's peoples is made, each stock generator's cap on what it makes
    /// moves as far as the people's age moved (PeopleAgesMath.ShiftedCap: a medieval people risen to the industrial age
    /// sells the industrial age's clothes and arms; one fallen back no longer sells what it lost), and is put back at once.
    /// What they buy keeps its caps. With the setting or the deeper game hooks off, nothing changes.
    /// </summary>
    [HarmonyPatch(typeof(ThingSetMaker_TraderStock), "Generate", new[] { typeof(ThingSetMakerParams), typeof(List<Thing>) })]
    public static class Patch_PeopleAgeStock
    {
        public static void Prefix(ThingSetMakerParams parms, out List<(StockGenerator gen, TechLevel cap)> __state)
        {
            __state = null;
            if (!RimfolkPatches.On || !PeopleAges.On) return;
            try
            {
                var f = parms.makingFaction;
                var gens = parms.traderDef?.stockGenerators;
                if (f?.def == null || gens == null || !Peoples.IsOurs(f)) return;
                int defTech = (int)f.def.techLevel, ageTech = (int)Peoples.TechOf(f);
                if (defTech == ageTech) return;
                foreach (var g in gens)
                {
                    if (g == null) continue;
                    int cap = PeopleAgesMath.ShiftedCap((int)g.maxTechLevelGenerate, defTech, ageTech);
                    if (cap == (int)g.maxTechLevelGenerate) continue;
                    (__state ??= new List<(StockGenerator, TechLevel)>()).Add((g, g.maxTechLevelGenerate));
                    g.maxTechLevelGenerate = (TechLevel)cap;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: the traders' goods", e);
            }
        }

        public static void Finalizer(List<(StockGenerator gen, TechLevel cap)> __state)
        {
            if (__state == null) return;
            foreach (var (gen, cap) in __state)
                if (gen != null)
                    gen.maxTechLevelGenerate = cap;
        }
    }
}
