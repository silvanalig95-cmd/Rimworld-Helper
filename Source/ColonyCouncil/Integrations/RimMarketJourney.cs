using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A journey's goods off a colony stall's counter (the player's request: "items from the trade stall should also be
    /// listed and pawns should take it out of the stock when required for travel"). What lies on the colony's RimMarket
    /// stalls is listed in the journey planner and its item picker beside the stores, counted in its amounts and the load,
    /// the stores first where both have it (JourneyAdviceMath.TakeFrom). In the last hours a traveller walks to the stall
    /// and takes the count still wanted out of its stock into their pack (<see cref="JobDriver_JourneyStall"/>); the line
    /// it lay under is lowered by as many, the way a sale takes it off the line (a line that refills keeps its count, and
    /// the stall's haulers fill it again from the stores), but no takings, demand or sale are recorded. Every member is
    /// RimMarket's, found by name; without it, or with one not found, stall goods simply aren't listed.
    /// </summary>
    public static partial class RimMarketStall
    {
        private static bool journeyLooked, journeyReady;
        private static PropertyInfo journeyOwner;

        /// <summary>RimMarket is loaded and its stalls' stock and lines were found: the journey planner lists their goods.</summary>
        public static bool JourneyReady
        {
            get
            {
                LookJourney();
                return journeyReady;
            }
        }

        private static void LookJourney()
        {
            if (journeyLooked) return;
            journeyLooked = true;
            Look();
            try
            {
                if (stallType == null || colonyStall == null || stock == null || stockDef == null) return;
                journeyOwner = AccessTools.Property(stallType, "StockOwner");
                journeyReady = journeyOwner != null && orders != null && orderDef != null && orderToSell != null && orderPrice != null && orderSold != null
                               && orderQuality != null && orderQuality.FieldType == typeof(QualityRange) && (setOrder != null || setOrderKept != null);
                if (!journeyReady) Log.Warning("[Rimfolk] RimMarket's stalls aren't the ones Rimfolk knows, so the journey planner doesn't list their goods.");
            }
            catch (Exception e)
            {
                journeyReady = false;
                CouncilLog.ErrorOnce("RimMarket stalls for journeys", e);
            }
        }

        /// <summary>The colony's own stalls on the map, nearest the middle of the colony first.</summary>
        public static List<Thing> JourneyStalls(Map map)
        {
            var list = new List<Thing>();
            if (map == null || !JourneyReady) return list;
            try
            {
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                    if (stallType.IsInstanceOfType(b) && b.Spawned && !b.Position.Fogged(map) && IsColonyStall(b))
                        list.Add(b);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stalls for journeys", e);
                list.Clear();
            }
            return list;
        }

        /// <summary>
        /// What lies on the colony's stalls' counters, by article (a packed-up building as what it is): each stall with how
        /// many it holds. Empty without RimMarket.
        /// </summary>
        public static Dictionary<ThingDef, List<(Thing stall, int count)>> JourneyStock(Map map)
        {
            var held = new Dictionary<ThingDef, List<(Thing, int)>>();
            foreach (var stall in JourneyStalls(map))
            {
                try
                {
                    var counts = new Dictionary<ThingDef, int>();
                    foreach (var t in Stock(stall))
                    {
                        if (t == null || t.Destroyed || t.stackCount <= 0 || t is Pawn) continue;
                        var def = StockDef(t);
                        if (def == null || def.IsCorpse) continue;
                        counts[def] = (counts.TryGetValue(def, out int n) ? n : 0) + t.stackCount;
                    }
                    foreach (var kv in counts)
                    {
                        if (!held.TryGetValue(kv.Key, out var list)) held[kv.Key] = list = new List<(Thing, int)>();
                        list.Add((stall, kv.Value));
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("RimMarket stall stock for journeys", e);
                }
            }
            return held;
        }

        /// <summary>How many of an article lie on one stall's counter now.</summary>
        public static int OnStall(Thing stall, ThingDef def)
        {
            if (stall == null || def == null || !JourneyReady) return 0;
            int n = 0;
            try
            {
                foreach (var t in Stock(stall))
                    if (t != null && !t.Destroyed && StockDef(t) == def)
                        n += t.stackCount;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket stall stock for journeys", e);
                return 0;
            }
            return n;
        }

        private static readonly List<Line> journeyLines = new List<Line>();

        /// <summary>
        /// A traveller takes <paramref name="count"/> of an article out of the stall's stock into their pack. Its sale line is
        /// lowered by as many (taken off the stall's table at none), as a sale lowers it; a line that refills keeps its count.
        /// No takings, no demand and no sale are recorded. Returns how many were taken.
        /// </summary>
        public static int TakeForJourney(Pawn p, Thing stall, ThingDef def, int count)
        {
            if (p?.inventory == null || stall == null || !stall.Spawned || def == null || count <= 0 || !JourneyReady) return 0;
            int taken = 0;
            try
            {
                if (!(journeyOwner.GetValue(stall) is ThingOwner owner)) return 0;
                foreach (var t in Stock(stall).ToList())
                {
                    if (taken >= count) break;
                    if (t == null || t.Destroyed || StockDef(t) != def) continue;
                    taken += owner.TryTransferToContainer(t, p.inventory.innerContainer, Math.Min(count - taken, t.stackCount));
                }
                if (taken <= 0) return 0;
                Lines(stall, journeyLines);
                foreach (var l in journeyLines)
                {
                    if (l.def != def || l.refill) continue;
                    SetLine(stall, l, Math.Max(0, l.toSell - taken), l.quality);
                    break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimMarket: a journey's goods off a stall", e);
            }
            return taken;
        }
    }

    /// <summary>
    /// A traveller in a journey's last hours takes goods out of a colony stall's stock (Integrations/RimMarketJourney.cs):
    /// walks to the stall, a moment at the counter, and the count is in their pack. A: the stall; the job's
    /// thingDefToCarry is the article and its count how many.
    /// </summary>
    public class JobDriver_JourneyStall : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job.thingDefToCarry == null || job.count <= 0);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, 90, true, false, false, TargetIndex.A);
            var take = ToilMaker.MakeToil("TakeFromStall");
            take.initAction = () => JourneyRun.TookFromStall(pawn, job.targetA.Thing, job.thingDefToCarry,
                RimMarketStall.TakeForJourney(pawn, job.targetA.Thing, job.thingDefToCarry, job.count));
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
        }
    }
}
