using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dubs Bad Hygiene, read by name: its needs (Hygiene, Bladder), its fixtures by their classes (toilets, water tubs,
    /// basins, baths, showers), its sauna heaters, boilers and hot water tanks by their parts. Without the mod (or in its
    /// lite mode, which drops some of these) the needs and classes aren't there and every check here is false.
    /// </summary>
    public static class Hygiene
    {
        private const string Ns = "DubsBadHygiene";

        [Flags]
        private enum Kind
        {
            None = 0,
            Toilet = 1,
            WashTub = 2,
            Trough = 4,
            Basin = 8,
            Bath = 16,
            HotTub = 32,
            Shower = 64,
            BurnPit = 128,
            SaunaHeater = 256,
            Boiler = 512,
            HotWaterTank = 1024
        }

        private static readonly Dictionary<ThingDef, Kind> kinds = new Dictionary<ThingDef, Kind>();

        private static Kind KindOf(ThingDef d)
        {
            if (d == null) return Kind.None;
            if (kinds.TryGetValue(d, out var k)) return k;
            k = Kind.None;
            try
            {
                for (var t = d.thingClass; t != null && t != typeof(object); t = t.BaseType)
                {
                    if (t.Namespace != Ns) continue;
                    switch (t.Name)
                    {
                        // Every toilet (latrine, toilet, smart toilet, vacuum toilet) shares one base class.
                        case "bibblefuckwit":
                        case "Building_Latrine":
                        case "Building_toilet":
                        case "Building_AdvToilet":
                        case "Building_SpacerToilet": k |= Kind.Toilet; break;
                        case "Building_WaterTrough": k |= Kind.Trough; break;
                        case "Building_washbucket": k |= Kind.WashTub; break;
                        case "Building_basin": k |= Kind.Basin; break;
                        case "Building_bath": k |= Kind.Bath; break;
                        case "Building_HotTub": k |= Kind.HotTub; break;
                        case "Building_shower": k |= Kind.Shower; break;
                        case "Building_BurnPit": k |= Kind.BurnPit; break;
                    }
                }
                if (d.comps != null)
                {
                    foreach (var c in d.comps)
                    {
                        if (c == null) continue;
                        var type = c.GetType();
                        if (HomeLife.ClassIs(type, Ns, "CompProperties_SaunaHeater")) k |= Kind.SaunaHeater;
                        else if (HomeLife.ClassIs(type, Ns, "CompProperties_CompBoiler")) k |= Kind.Boiler;
                        else if (HomeLife.ClassIs(type, "DubCore", "CompProperties_HeatStore")
                                 && type.GetField("ForRunningWater")?.GetValue(c) is bool forWater && forWater) k |= Kind.HotWaterTank;
                    }
                }
                if (d.defName == "HotWaterTank") k |= Kind.HotWaterTank;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hygiene def " + d.defName, e);
            }
            kinds[d] = k;
            return k;
        }

        private static bool Has(ThingDef d, Kind k) => (KindOf(d) & k) != 0;

        /// <summary>A latrine or toilet of any kind.</summary>
        public static bool IsToilet(ThingDef d) => Has(d, Kind.Toilet);

        /// <summary>Somewhere to get clean: a water tub, a bathtub or a shower (not an animal's trough).</summary>
        public static bool IsWashPlace(ThingDef d) => (Has(d, Kind.WashTub) && !Has(d, Kind.Trough)) || Has(d, Kind.Bath | Kind.Shower);

        /// <summary>A bathtub or a shower: the room plans' "bath" slot.</summary>
        public static bool IsBathOrShower(ThingDef d) => Has(d, Kind.Bath | Kind.Shower);

        /// <summary>A basin or a water tub: the room plans' "wash" slot. Fountains and kitchen sinks are basins too, but not for a bathroom.</summary>
        public static bool IsWashStand(ThingDef d)
        {
            if (Has(d, Kind.WashTub)) return !Has(d, Kind.Trough);
            if (!Has(d, Kind.Basin)) return false;
            string name = (d.defName + " " + d.label).ToLowerInvariant();
            return !name.Contains("fountain") && !name.Contains("sink");
        }

        public static bool IsSaunaHeater(ThingDef d) => Has(d, Kind.SaunaHeater);

        public static bool IsBurnPit(ThingDef d) => Has(d, Kind.BurnPit);

        /// <summary>Heats water for the pipes: a log, gas or electric boiler, a solar or geothermal heater.</summary>
        public static bool IsBoiler(ThingDef d) => Has(d, Kind.Boiler);

        public static bool IsHotWaterTank(ThingDef d) => Has(d, Kind.HotWaterTank);

        /// <summary>
        /// Gives off heat, but isn't for warming a home: the burn pit (its smoke makes people sick) and the sauna heater
        /// (it drives a room to 60 °C). <see cref="Avail.IsHeater"/> leaves them out.
        /// </summary>
        public static bool NotAHomeHeater(ThingDef d) => Has(d, Kind.BurnPit | Kind.SaunaHeater);

        // ---------- Needs and people ----------

        private static bool needsLooked;
        private static NeedDef hygieneNeed, bladderNeed;

        private static void LookUpNeeds()
        {
            if (needsLooked) return;
            needsLooked = true;
            hygieneNeed = DefDatabase<NeedDef>.GetNamedSilentFail("Hygiene");
            bladderNeed = DefDatabase<NeedDef>.GetNamedSilentFail("Bladder");
        }

        public static NeedDef HygieneNeed
        {
            get
            {
                LookUpNeeds();
                return hygieneNeed;
            }
        }

        public static NeedDef BladderNeed
        {
            get
            {
                LookUpNeeds();
                return bladderNeed;
            }
        }

        /// <summary>Colonists on the map who have this need (some races and genes don't), babies aside.</summary>
        public static int Users(Map map, NeedDef need)
        {
            if (map == null || need == null) return 0;
            int n = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
                if (!p.IsQuestLodger() && !p.DevelopmentalStage.Baby() && p.needs?.TryGetNeed(need) != null) n++;
            return n;
        }

        /// <summary>A rule of thumb, not the mod's own number: one toilet for every six people keeps the queue short.</summary>
        public const int PeoplePerToilet = 6;

        public static int ToiletsWanted(int users) => users <= 0 ? 0 : Mathf.Max(1, Mathf.CeilToInt(users / (float)PeoplePerToilet));

        public static int Toilets(Map map) => map == null ? 0 : Avail.ColonyCount(map, IsToilet);

        public static int WashPlaces(Map map) => map == null ? 0 : Avail.ColonyCount(map, IsWashPlace);

        public static bool HasHotWater(Map map) => map != null && Avail.ColonyHas(map, IsHotWaterTank) && Avail.ColonyHas(map, IsBoiler);

        // What to suggest: the era's own fixture first (a toilet once it's researched, a latrine before).
        public static ThingDef BestToilet(Map map) => Avail.BestBuildable(map, IsToilet, "ToiletStuff", "PitLatrine");

        public static ThingDef BestWashPlace(Map map) => Avail.BestBuildable(map, IsWashPlace, "WashBucket");

        public static ThingDef BestHotWaterTank(Map map) => Avail.BestBuildable(map, IsHotWaterTank, "HotWaterTank");

        /// <summary>The bathroom plan, which loads only with the mod (the wishes always load, so they can't name it in XML).</summary>
        public static RoomTemplateDef BathroomPlan => DefDatabase<RoomTemplateDef>.GetNamedSilentFail("Council_Bathroom");

        private static List<HediffDef> waterSickness;

        /// <summary>The diseases Dubs Bad Hygiene spreads through dirty water and poor hygiene.</summary>
        public static List<HediffDef> WaterSickness =>
            waterSickness ??= new[] { "Cholera", "Dysentery", "Diarrhea" }.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(h => h != null).ToList();
    }

    // ===================== Advisor =====================

    /// <summary>No toilet at all, or too few for everyone who needs one.</summary>
    public class AdvisorWorker_Toilets : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var need = Hygiene.BladderNeed;
            if (need == null) yield break;
            int users = Hygiene.Users(map, need);
            if (users == 0) yield break;
            int have = Hygiene.Toilets(map);
            int wanted = Hygiene.ToiletsWanted(users);
            if (have >= wanted) yield break;
            var best = Hygiene.BestToilet(map);
            if (best == null) yield break;
            AdvisorSignal s;
            if (have == 0)
            {
                s = Signal(0.5f + 0.03f * users, "CC_Hyg_Adv_NoToilet_Title".Translate(),
                    "CC_Hyg_Adv_NoToilet_Detail".Translate(users, Avail.Indefinite(best)), 0.8f);
                s.key = "none";
            }
            else
            {
                s = Signal(0.2f + 0.08f * (wanted - have), "CC_Hyg_Adv_FewToilets_Title".Translate(),
                    "CC_Hyg_Adv_FewToilets_Detail".Translate(users, have, Avail.Indefinite(best)), 0.4f, 0.7f);
                s.look = new LookTargets(Avail.ColonyBuildings(map, Hygiene.IsToilet).Cast<Thing>());
                s.key = "few";
            }
            s.suggestThing = best;
            yield return s;
        }
    }

    /// <summary>Nowhere at all to get clean.</summary>
    public class AdvisorWorker_Washing : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var need = Hygiene.HygieneNeed;
            if (need == null) yield break;
            int users = Hygiene.Users(map, need);
            if (users == 0 || Hygiene.WashPlaces(map) > 0) yield break;
            var best = Hygiene.BestWashPlace(map);
            if (best == null) yield break;
            var s = Signal(0.4f, "CC_Hyg_Adv_NoWash_Title".Translate(), "CC_Hyg_Adv_NoWash_Detail".Translate(users, Avail.Indefinite(best)), 0.6f);
            s.suggestThing = best;
            yield return s;
        }
    }

    /// <summary>Colonists down with the diseases dirty water brings.</summary>
    public class AdvisorWorker_WaterSickness : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var diseases = Hygiene.WaterSickness;
            if (diseases.Count == 0) yield break;
            var sick = new List<Pawn>();
            bool cholera = false;
            foreach (var p in Colonists(map))
            {
                var hediffs = p.health?.hediffSet;
                if (hediffs == null) continue;
                foreach (var d in diseases)
                {
                    if (hediffs.GetFirstHediffOfDef(d) == null) continue;
                    sick.Add(p);
                    if (d.defName == "Cholera") cholera = true;
                    break;
                }
            }
            if (sick.Count == 0) yield break;
            var s = Signal((cholera ? 0.6f : 0.35f) + 0.05f * sick.Count, "CC_Hyg_Adv_SickWater_Title".Translate(sick.Count),
                "CC_Hyg_Adv_SickWater_Detail".Translate(TextUtil.JoinAnd(sick.Select(p => p.LabelShort).ToList())), 0.8f, 0.8f);
            s.look = new LookTargets(sick.Cast<Thing>());
            yield return s;
        }
    }

    // ===================== Wishes =====================

    /// <summary>Somewhere to go that isn't the open ground: a latrine or toilet, or one more when the queue gets long.</summary>
    public class WishWorker_Toilets : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var need = Hygiene.BladderNeed;
            if (map == null || need == null || pawn.needs?.TryGetNeed(need) == null || !P.Who(pawn)) yield break;
            int users = Hygiene.Users(map, need);
            int have = Hygiene.Toilets(map);
            if (have >= Hygiene.ToiletsWanted(users)) yield break;
            var best = Hygiene.BestToilet(map);
            if (best == null) yield break;
            float w = have == 0 ? 2f : 1f;
            if (HasMemory(pawn, "openDefecation") || HasMemory(pawn, "SoiledSelf")) w *= 2f;
            if (RoomUtil.HasActiveThought(pawn, "BowelLevel")) w *= 1.3f;
            string reason = have == 0
                ? "CC_Hyg_Reason_NoToilet".Translate(pawn.Named("PAWN")).Resolve()
                : "CC_Hyg_Reason_FewToilets".Translate(pawn.Named("PAWN"), users.Named("COUNT"), have.Named("HAVE")).Resolve();
            var c = Candidate(w * P.weight, reason);
            c.thingDef = best;
            c.baseline = have;
            c.targetValue = have + 1;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Hygiene.Toilets(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override RoomTemplateDef SuggestedTemplate(Wish wish) => Hygiene.BathroomPlan;

        public override bool StillObtainable(Wish wish)
        {
            if (!base.StillObtainable(wish)) return false;
            var map = wish.Beneficiary.MapHeld;
            return map == null || IsFulfilled(wish) || Avail.CanBuild(wish.thingDef, map) || Avail.AnyBuildable(map, Hygiene.IsToilet);
        }

        /// <summary>Promises kept: the toilets stay (without the hygiene mod there's nothing to keep).</summary>
        public override KeptCheck Holds(Wish wish) => Hygiene.BladderNeed == null ? KeptCheck.Over : base.Holds(wish);

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && Hygiene.IsToilet(td);

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Count".Translate(Hygiene.Toilets(wish.Beneficiary.MapHeld), wish.targetValue.ToString("0")).ToString();
    }

    /// <summary>Anywhere at all to wash, for someone who has gone grimy with nowhere to go.</summary>
    public class WishWorker_WashPlace : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var need = Hygiene.HygieneNeed;
            if (map == null || need == null || !P.Who(pawn)) yield break;
            var level = pawn.needs?.TryGetNeed(need);
            if (level == null || level.CurLevelPercentage > 0.4f) yield break;
            if (Hygiene.WashPlaces(map) > 0) yield break;
            var best = Hygiene.BestWashPlace(map);
            if (best == null) yield break;
            var c = Make(pawn, level.CurLevelPercentage < 0.2f ? 2f : 1f);
            c.thingDef = best;
            c.targetValue = 1;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Hygiene.WashPlaces(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override RoomTemplateDef SuggestedTemplate(Wish wish) => Hygiene.BathroomPlan;

        public override bool StillObtainable(Wish wish)
        {
            if (!base.StillObtainable(wish)) return false;
            var map = wish.Beneficiary.MapHeld;
            return map == null || IsFulfilled(wish) || Avail.CanBuild(wish.thingDef, map) || Avail.AnyBuildable(map, Hygiene.IsWashPlace);
        }

        /// <summary>Promises kept: somewhere to wash stays.</summary>
        public override KeptCheck Holds(Wish wish) => Hygiene.HygieneNeed == null ? KeptCheck.Over : base.Holds(wish);

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && Hygiene.IsWashPlace(td);
    }

    /// <summary>Hot water, once the colony knows how to heat it: after a cold bath or shower that should have been warm.</summary>
    public class WishWorker_HotWater : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || Hygiene.HygieneNeed == null || !P.Who(pawn)) yield break;
            if (!HasMemory(pawn, "ColdWater") || Hygiene.HasHotWater(map)) yield break;
            var tank = Hygiene.BestHotWaterTank(map);
            if (tank == null) yield break;
            if (!Avail.ColonyHas(map, Hygiene.IsBoiler) && !Avail.AnyBuildable(map, Hygiene.IsBoiler)) yield break;
            var c = Make(pawn, 1.5f);
            c.thingDef = tank;
            c.targetValue = 1;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Hygiene.HasHotWater(wish.Beneficiary.MapHeld);

        public override bool StillObtainable(Wish wish)
        {
            if (!base.StillObtainable(wish)) return false;
            var map = wish.Beneficiary.MapHeld;
            return map == null || IsFulfilled(wish) || Avail.CanBuild(wish.thingDef, map) || Avail.AnyBuildable(map, Hygiene.IsHotWaterTank);
        }

        /// <summary>Promises kept: the tank and the boiler that heats it stay.</summary>
        public override KeptCheck Holds(Wish wish) => Hygiene.HygieneNeed == null ? KeptCheck.Over : base.Holds(wish);

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && (Hygiene.IsHotWaterTank(td) || Hygiene.IsBoiler(td));
    }
}
