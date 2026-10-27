using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Strangers the colony feeds (<see cref="DilemmaEffect_CarryOut.eat"/>): once the food is handed over, the one who
    /// took it and their people with them eat it there and then, a share each from what was given. With
    /// <see cref="DilemmaEffect_CarryOut.night"/> they stay until first light: their visit (or the courier trip) lasts
    /// until dawn, and from nightfall each lies down to sleep in the lee of the colony's wall by its gate, where the watch
    /// can see them. Sent away by the same answer (DilemmaEffect_SendAway, held by the errand: PlayDepart.cs), they go on
    /// their way once they've eaten. The night and the going are kept for the session: after a reload they still stay
    /// until dawn, but go about it as visitors do.
    /// </summary>
    public static class PlayStrangers
    {
        private class Night
        {
            public List<Pawn> pawns;
            public Map map;
            public IntVec3 near;
            public int until;
        }

        private static readonly List<Night> nights = new List<Night>();

        private class Going
        {
            public List<Pawn> pawns;
            public int since;
        }

        /// <summary>Fed and sent on: they go once they've eaten.</summary>
        private static readonly List<Going> going = new List<Going>();

        /// <summary>Night falls at this hour for those staying, and they go at first light (CourierMath's day).</summary>
        public const int Dusk = 21, Dawn = 6;

        public static void Reset()
        {
            nights.Clear();
            going.Clear();
        }

        /// <summary>They go on their way once they've eaten (at most a couple of hours on).</summary>
        public static void SendOnAfterMeal(Pawn to)
        {
            var party = Party(to);
            if (party.Count > 0) going.Add(new Going { pawns = party, since = Find.TickManager.TicksGame });
        }

        /// <summary>The one it was handed to and their people with them here (their group), who are up to eating or sleeping.</summary>
        public static List<Pawn> Party(Pawn to)
        {
            var list = new List<Pawn>();
            if (!Able(to, to?.Map)) return list;
            list.Add(to);
            var lord = to.GetLord();
            if (lord != null) list.AddRange(lord.ownedPawns.Where(o => o != to && o.RaceProps.Humanlike && Able(o, to.Map)));
            return list;
        }

        private static bool Able(Pawn o, Map map) =>
            o != null && !o.Dead && o.Spawned && o.Map == map && !o.Downed && !o.InMentalState && o.RaceProps.Humanlike && o.Faction != Faction.OfPlayer
            && !o.HostileTo(Faction.OfPlayer) && !o.IsPrisoner;

        /// <summary>
        /// What was handed to <paramref name="to"/> is eaten there and then: by them and their people, a share each. Meals
        /// are eaten, one each while they last; of raw food they eat half, and keep the rest for the road.
        /// </summary>
        public static void Eat(Pawn to, ThingDef def)
        {
            if (to?.inventory == null || def == null || !def.IsNutritionGivingIngestible) return;
            var party = Party(to);
            int total = to.inventory.innerContainer.Where(t => t.def == def).Sum(t => t.stackCount);
            int budget = def.ingestible?.IsMeal ?? false ? total : Math.Max(1, total / 2);
            int each = Math.Max(1, budget / Math.Max(1, party.Count));
            foreach (var m in party)
            {
                if (budget <= 0) break;
                var stack = to.inventory.innerContainer.FirstOrDefault(t => t.def == def && t.stackCount > 0);
                if (stack == null) break;
                if (!m.RaceProps.CanEverEat(stack) || !FoodUtility.WillEat(m, stack, m)) continue;
                int count = Math.Max(1, Math.Min(Math.Min(stack.stackCount, each), FoodUtility.WillIngestStackCountOf(m, def, FoodUtility.NutritionForEater(m, stack))));
                budget -= count;
                var food = stack;
                if (m != to)
                {
                    // Passed round: their share from the one who took it.
                    if (to.inventory.innerContainer.TryTransferToContainer(stack, m.inventory.innerContainer, count, out Thing share) <= 0 || share == null) continue;
                    food = share;
                }
                var job = JobMaker.MakeJob(JobDefOf.Ingest, food);
                job.count = Math.Min(count, food.stackCount);
                // together: they eat what was carried out to them, there and then, standing where they are.
                m.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }

        /// <summary>
        /// They stay the night: their visit lasts until first light, and from nightfall they sleep by the colony's wall.
        /// Never cut short: a visit already longer keeps its time.
        /// </summary>
        public static void StayTheNight(Pawn to)
        {
            var party = Party(to);
            if (party.Count == 0) return;
            var map = to.Map;
            int now = Find.TickManager.TicksGame;
            float hour = GenLocalDate.HourFloat(map);
            float hoursLeft = hour < Dawn ? Dawn - hour : 24f - hour + Dawn;
            int until = now + (int)((hoursLeft + 0.5f) * GenDate.TicksPerHour);
            if (nights.Any(n => n.pawns.Contains(to))) return;
            var lord = to.GetLord();
            Hold(lord, until - now);
            Couriers.StayUntil(lord, until);
            nights.Add(new Night { pawns = party, map = map, near = PlaySpots.GateCell(map, to), until = until });
        }

        // The game keeps a visit's length and a timed trigger's length to itself: found by name, once.
        private static bool looked;
        private static AccessTools.FieldRef<LordJob_VisitColony, int?> visitLength;
        private static AccessTools.FieldRef<Trigger_TicksPassed, int> triggerLength;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                visitLength = AccessTools.FieldRefAccess<LordJob_VisitColony, int?>("durationTicks");
                triggerLength = AccessTools.FieldRefAccess<Trigger_TicksPassed, int>("duration");
            }
            catch (Exception e)
            {
                visitLength = null;
                triggerLength = null;
                Log.Warning("[Rimfolk] A visit's length isn't kept the way Rimfolk knows, so strangers who stay the night may leave in the dark: " + e.Message);
            }
        }

        /// <summary>A visit's time is stretched to at least <paramref name="ticks"/> from now (its own trigger, and the visit's length for a reload).</summary>
        private static void Hold(Lord lord, int ticks)
        {
            Look();
            if (lord?.Graph == null || triggerLength == null) return;
            var visit = lord.LordJob as LordJob_VisitColony;
            int? visitTicks = visit != null && visitLength != null ? visitLength(visit) : null;
            foreach (var tr in lord.Graph.transitions)
                foreach (var trig in tr.triggers)
                {
                    if (!(trig is Trigger_TicksPassed passed)) continue;
                    ref int length = ref triggerLength(passed);
                    // Only a time that sends them off: their visit's own length, or a courier's long wait.
                    if (!(tr.target is LordToil_ExitMap) && !(visitTicks.HasValue && length == visitTicks.Value)) continue;
                    int passedNow = tr.sources.Contains(lord.CurLordToil) ? length - passed.TicksLeft : 0;
                    length = Math.Max(length, passedNow + ticks);
                }
            if (visit != null && visitTicks.HasValue) visitLength(visit) = Math.Max(visitTicks.Value, ticks + lord.ticksInToil);
        }

        /// <summary>Each look: those staying lie down by the wall once night falls, until first light.</summary>
        public static void Tick(int now)
        {
            foreach (var g in going.ToList())
            {
                g.pawns.RemoveAll(o => o == null || o.Dead || !o.Spawned);
                if (g.pawns.Count > 0 && g.pawns.Any(o => o.CurJobDef == JobDefOf.Ingest) && now - g.since < GenDate.TicksPerHour * 2) continue;
                going.Remove(g);
                try
                {
                    // apart: fed, they walk on; the leaving is the game's own walk to the edge.
                    Arrivals.SendAway(g.pawns.Where(o => Able(o, o.Map)).ToList());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("strangers sent on", e);
                }
            }
            if (nights.Count == 0) return;
            foreach (var n in nights.ToList())
            {
                try
                {
                    if (now > n.until || n.map == null || !Find.Maps.Contains(n.map))
                    {
                        nights.Remove(n);
                        continue;
                    }
                    int hour = GenLocalDate.HourOfDay(n.map);
                    if (hour < Dusk && hour >= Dawn) continue;
                    n.pawns.RemoveAll(o => o == null || o.Dead || !o.Spawned || o.Map != n.map);
                    if (n.pawns.Count == 0)
                    {
                        nights.Remove(n);
                        continue;
                    }
                    foreach (var o in n.pawns)
                    {
                        if (!Able(o, n.map) || !o.Awake() || o.CurJobDef == JobDefOf.LayDown || o.CurJobDef == JobDefOf.Ingest) continue;
                        // On their way out after all (hurt by the colony, their people turned hostile): let them go.
                        var duty = o.mindState?.duty?.def;
                        if (duty != null && duty.defName.StartsWith("ExitMap")) continue;
                        var cell = Bed(n.map, n.near, o);
                        if (!cell.IsValid) continue;
                        var job = JobMaker.MakeJob(JobDefOf.LayDown, cell);
                        // together: they bed down in the lee of the colony's wall, where the watch can see them.
                        o.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("strangers staying the night", e);
                    nights.Remove(n);
                }
            }
        }

        /// <summary>A place to sleep by the gate: beside the colony's wall where there is one, else on open ground near it.</summary>
        private static IntVec3 Bed(Map map, IntVec3 near, Pawn o)
        {
            if (!near.IsValid) near = o.Position;
            bool Free(IntVec3 c) =>
                c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && !c.Fogged(map)
                && !map.pawnDestinationReservationManager.IsReserved(c) && o.CanReach(c, PathEndMode.OnCell, Danger.Some);
            bool ByWall(IntVec3 c) => GenAdj.CardinalDirections.Any(d => (c + d).InBounds(map) && (c + d).GetEdifice(map) is Building b
                && b.Faction == Faction.OfPlayer && b.def.passability == Traversability.Impassable);
            if (CellFinder.TryFindRandomCellNear(near, map, 10, c => Free(c) && ByWall(c), out var wall)) return wall;
            return CellFinder.TryFindRandomCellNear(near, map, 6, Free, out var open) ? open : IntVec3.Invalid;
        }
    }
}
