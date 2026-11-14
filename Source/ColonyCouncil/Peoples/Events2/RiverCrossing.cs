using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 26. A crossing on the colony's river (the river folk) =====================

    /// <summary>
    /// The river folk's crossing (item 26): where a river or a stream cuts the colony's map, two or three boatmen spend a day
    /// laying the game's bridge over the water that takes one, and planks on the banks, with wood the colony gives; their
    /// own town's dock-laying (PeopleTowns.LayDocks), cell by cell, a bar for each. Rope and plank (the game's bridge) in
    /// the tribal and medieval ages, a heavy bridge on piles (Odyssey's) after. The crossing is the colony's. Once a
    /// year, on their knot night, they ask to rest at it (Rimfolk_Evt_KnotNight).
    /// </summary>
    public static class RiverCrossing
    {
        public const string LayKind = "pev:crossing";
        public const string KnotKind = "pev:knot";

        public static JobDef LayJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv2_LayCrossing");

        private static int Now => Find.TickManager.TicksGame;

        private static bool Bridgeable(TerrainDef t) => t != null && t.IsWater && t.affordances != null && t.affordances.Contains(TerrainAffordanceDefOf.Bridgeable);

        private static bool Bank(IntVec3 c, Map map) =>
            c.InBounds(map) && c.GetTerrain(map) is TerrainDef t && !t.IsWater && t.passability != Traversability.Impassable && c.GetEdifice(map) == null
            && t.affordances != null && t.affordances.Contains(TerrainAffordanceDefOf.Light);

        /// <summary>The bridge of their age: the game's, or Odyssey's heavy bridge from the industrial age (the game's without it).</summary>
        public static TerrainDef BridgeFor(int age) => DefDatabase<TerrainDef>.GetNamedSilentFail(PeopleEvents2Math.BridgeFor(age)) ?? TerrainDefOf.Bridge;

        public static TerrainDef Planks => DefDatabase<TerrainDef>.GetNamedSilentFail("WoodPlankFloor");

        /// <summary>
        /// The crossing's line: from a bank across the river to the other, straight (along a row or a column),
        /// every water cell one a bridge stands in and nothing built there, two planks on each bank; the narrowest near the
        /// colony. Water cells first-to-last from one bank, then the planks. Null when the map has no river it can cross.
        /// </summary>
        public static List<IntVec3> Line(Map map, out int water)
        {
            water = 0;
            if (map == null) return null;
            try
            {
                var centre = CampSite.ColonyCenter(map);
                if (!centre.IsValid) centre = map.Center;
                var river = map.AllCells.Where(c => (c.x + c.z) % 3 == 0 && c.GetTerrain(map) is TerrainDef t && t.IsRiver && Bridgeable(t) && !c.Fogged(map))
                    .OrderBy(c => c.DistanceToSquared(centre)).Take(60).ToList();
                List<IntVec3> best = null;
                float bestScore = float.MaxValue;
                int bestWater = 0;
                // Along a row or a column only: nobody walks a diagonal of spans over deep water (the game's corners).
                var dirs = new[] { new IntVec3(1, 0, 0), new IntVec3(0, 0, 1) };
                foreach (var s in river)
                    foreach (var d in dirs)
                    {
                        var cells = Across(map, s, d, out int w);
                        if (cells == null) continue;
                        float score = w + Mathf.Sqrt(s.DistanceToSquared(centre)) * 0.08f;
                        if (score >= bestScore) continue;
                        bestScore = score;
                        best = cells;
                        bestWater = w;
                    }
                water = bestWater;
                return best;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the crossing's line", e);
                return null;
            }
        }

        private static List<IntVec3> Across(Map map, IntVec3 s, IntVec3 d, out int water)
        {
            water = 0;
            var a = s;
            while ((a - d).InBounds(map) && Bridgeable((a - d).GetTerrain(map))) a -= d;
            var b = s;
            while ((b + d).InBounds(map) && Bridgeable((b + d).GetTerrain(map))) b += d;
            var cells = new List<IntVec3>();
            for (var c = a; ; c += d)
            {
                if (!Bridgeable(c.GetTerrain(map)) || c.GetEdifice(map) != null || map.terrainGrid.FoundationAt(c) != null) return null;
                cells.Add(c);
                if (cells.Count > PeopleEvents2Math.CrossingMaxWater) return null;
                if (c == b) break;
            }
            water = cells.Count;
            // Both banks dry ground that bears a floor, two planks each.
            var banks = new List<IntVec3>();
            for (int i = 1; i <= PeopleEvents2Math.BankPlanks; i++)
            {
                var near = a - d * i;
                var far = b + d * i;
                if (!Bank(near, map) || !Bank(far, map)) return i == 1 ? null : cells.Concat(banks).ToList();
                banks.Add(near);
                banks.Add(far);
            }
            return cells.Concat(banks).ToList();
        }

        /// <summary>The wood the colony gives: each span's and plank's own cost (the bridge's, the floor's), at least twenty.</summary>
        public static int Wood(int water, int planks, TerrainDef bridge)
        {
            int perSpan = bridge?.CostList?.FirstOrDefault(c => c.thingDef == ThingDefOf.WoodLog)?.count ?? 0;
            if (perSpan <= 0) perSpan = 6;
            int perPlank = Planks?.CostList?.FirstOrDefault(c => c.thingDef == ThingDefOf.WoodLog)?.count ?? 3;
            return PeopleEvents2Math.CrossingWood(water, planks, perSpan, perPlank);
        }

        /// <summary>A crossing laid already on this map.</summary>
        public static CrossingRecord On(Map map) => PeopleEvents2Book.Instance?.crossings?.FirstOrDefault(c => c != null && c.map == map);

        public static string Encode(List<IntVec3> cells) => string.Join(";", cells.Select(c => c.x + "," + c.z));

        public static List<IntVec3> Decode(string s)
        {
            var list = new List<IntVec3>();
            foreach (var part in (s ?? "").Split(';'))
            {
                var xz = part.Split(',');
                if (xz.Length == 2 && int.TryParse(xz[0], out int x) && int.TryParse(xz[1], out int z)) list.Add(new IntVec3(x, 0, z));
            }
            return list;
        }

        private static List<IntVec3> CellsOf(Visit v)
        {
            var bar = (v?.text ?? "").IndexOf('|');
            return bar < 0 ? new List<IntVec3>() : Decode(v.text.Substring(bar + 1));
        }

        private static TerrainDef BridgeOf(Visit v)
        {
            var bar = (v?.text ?? "").IndexOf('|');
            return (bar < 0 ? null : DefDatabase<TerrainDef>.GetNamedSilentFail(v.text.Substring(0, bar))) ?? TerrainDefOf.Bridge;
        }

        /// <summary>A cell already laid: the bridge stands in its water, or the planks are down on the bank.</summary>
        public static bool Laid(IntVec3 c, Map map) =>
            c.InBounds(map) && (map.terrainGrid.FoundationAt(c) != null || c.GetTerrain(map) == Planks || !c.GetTerrain(map).IsWater && !Bank(c, map));

        /// <summary>
        /// The boatmen set to work (one of the events' visits): their own lord at the crossing for the day, each given the
        /// next cell from their bank, the line laid from both ends toward the middle.
        /// </summary>
        public static void Begin(HappeningContext ctx, List<IntVec3> cells, TerrainDef bridge)
        {
            var map = ctx?.map;
            var boatmen = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            var tracker = CouncilGameComponent.Instance?.happenings;
            if (map == null || cells.NullOrEmpty() || boatmen.Count == 0 || tracker == null) return;
            var v = tracker.AddVisit(LayKind, ctx.incident, map, boatmen[0], null, boatmen.Skip(1), Now + 2 * GenDate.TicksPerDay);
            v.text = bridge.defName + "|" + Encode(cells);
            try
            {
                Lords.Leave(boatmen);
                var mid = cells[cells.Count / 2];
                if (!CellFinder.TryFindRandomCellNear(cells.Last(), map, 3, c => c.Standable(map), out var rest)) rest = mid;
                LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, rest, 2 * GenDate.TicksPerDay), map, boatmen);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the boatmen at the river", e);
            }
            if (!PlayOuts.On)
            {
                PeopleTowns.LayDocks(map, cells, bridge);
                Finish(v);
                return;
            }
            for (int i = 0; i < boatmen.Count; i++) Next(v, boatmen[i]);
        }

        /// <summary>The visit a boatman works for.</summary>
        public static Visit Of(Pawn p) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == LayKind && !v.done && (v.x == p || v.group.Contains(p)));

        /// <summary>The next cell for this boatman: the first not yet laid from their bank (every other one starts from the far end), not taken by another.</summary>
        public static bool Next(Visit v, Pawn p, bool queued = false)
        {
            var map = v?.map;
            var job = LayJob;
            if (map == null || job == null || !PeoplesG.Up(p, map) || p.InMentalState) return false;
            var cells = CellsOf(v);
            var water = cells.Where(c => c.InBounds(map) && c.GetTerrain(map).IsWater).ToList();
            var taken = v.group.Prepend(v.x).Where(o => o != null && o != p && o.CurJobDef == job).Select(o => o.CurJob.targetA.Cell).ToList();
            bool far = v.group.IndexOf(p) % 2 == 0 && p != v.x;
            IntVec3 next = IntVec3.Invalid;
            // The banks' planks first, then the water from that bank toward the middle.
            foreach (var c in cells.Where(c => !water.Contains(c)))
                if (!Laid(c, map) && !taken.Contains(c) && p.CanReach(c, PathEndMode.Touch, Danger.Some))
                {
                    next = c;
                    break;
                }
            if (!next.IsValid)
            {
                var order = far ? Enumerable.Reverse(water).ToList() : water;
                foreach (var c in order)
                    if (!Laid(c, map) && !taken.Contains(c) && p.CanReach(c, PathEndMode.Touch, Danger.Some))
                    {
                        next = c;
                        break;
                    }
            }
            if (!next.IsValid) return false;
            try
            {
                var j = JobMaker.MakeJob(job, next);
                j.count = next.GetTerrain(map).IsWater ? PeopleEvents2Math.SpanTicks : PeopleEvents2Math.PlankTicks;
                j.locomotionUrgency = Pace.Errand;
                // From inside a cell just laid, the next waits its turn in their queue; else it starts now.
                if (queued)
                {
                    p.jobs.jobQueue.EnqueueFirst(j);
                    return true;
                }
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return p.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the boatmen's next cell", e);
                return false;
            }
        }

        /// <summary>A cell's bar done (JobDriver_LayCrossing): laid as their docks are, then the next.</summary>
        public static void LaidBy(Pawn p, IntVec3 c)
        {
            var v = Of(p);
            if (v?.map == null) return;
            PeopleTowns.LayDocks(v.map, new List<IntVec3> { c }, BridgeOf(v));
            FleckMaker.ThrowDustPuff(c, v.map, 1f);
            if (CellsOf(v).All(x => Laid(x, v.map)))
            {
                Finish(v);
                return;
            }
            if (Rand.Chance(0.15f)) PeoplesG.SayLine(p, v.incident, "lay");
            Next(v, p, true);
        }

        /// <summary>The crossing's hour: anyone idle given the next cell; done when every cell is laid, or given up when the boatmen are gone.</summary>
        public static bool LayHour(Visit v, int now)
        {
            if (v.done) return false;
            var map = v.map;
            var boatmen = v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, map)).ToList();
            if (CellsOf(v).All(c => Laid(c, map)) || boatmen.Count == 0 || now > v.until)
            {
                Finish(v);
                return false;
            }
            if (map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            foreach (var p in boatmen)
                if (p.CurJobDef != LayJob)
                    Next(v, p);
            return true;
        }

        /// <summary>Done (or as far as they got): the crossing the colony's, the record kept, the boatmen home.</summary>
        private static void Finish(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var map = v.map;
            var cells = CellsOf(v);
            int laid = cells.Count(c => map.terrainGrid.FoundationAt(c) != null);
            var f = v.x?.Faction;
            if (laid > 0 && f != null)
            {
                var r = new CrossingRecord { map = map, faction = f, cells = cells.Where(c => Laid(c, map)).ToList(), bridge = BridgeOf(v).defName, laid = Now };
                PeopleEvents2Book.Instance?.AddCrossing(r);
                var k = CouncilGameComponent.Instance?.happenings?.AddVisit(KnotKind, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_KnotNight"), map, null, null, null, Now + 30 * GenDate.TicksPerDay);
                if (k != null) k.stake = f.loadID;
                if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.CrossingGoodwill, false);
                FactionMemory.Note(f, "builtTogether", "CC_PEv2_CrossingName".Translate().Resolve());
                bool whole = cells.All(c => Laid(c, map));
                Tell.Message(NewsKind.Colony, Importance.Notable, map, (whole ? "CC_PEv2_CrossingDone" : "CC_PEv2_CrossingPart").Translate(f.Name.Named("FACTION")).Resolve(),
                    new LookTargets(cells.Select(c => new TargetInfo(c, map))), MessageTypeDefOf.PositiveEvent);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv2_CrossingChronicle".Translate(f.Name.Named("FACTION")).Resolve(), null));
            }
            if (v.x != null) PeoplesG.SayLine(v.x, v.incident, "done");
            PeoplesG.Home(v.group.Prepend(v.x));
        }

        // ================= Their knot night =================

        /// <summary>The crossing still stands: half its water spans or more still bridged.</summary>
        public static bool Stands(CrossingRecord r) =>
            r?.map != null && Find.Maps.Contains(r.map) && r.cells.Count(c => c.InBounds(r.map) && r.map.terrainGrid.FoundationAt(c) != null) * 2 >= Math.Max(1, r.cells.Count(c => c.InBounds(r.map) && c.GetTerrain(r.map).IsWater));

        /// <summary>Dev mode: it is their knot night now, whatever the day.</summary>
        public static bool forceKnot;

        /// <summary>The crossing whose people's knot night it is now, or null.</summary>
        public static CrossingRecord KnotNow()
        {
            var all = PeopleEvents2Book.Instance?.crossings;
            if (all == null) return null;
            foreach (var r in all)
            {
                if (r?.map == null || r.faction == null || r.faction.HostileTo(Faction.OfPlayer) || r.faction.defeated || !Find.Maps.Contains(r.map) || !Stands(r)) continue;
                if (forceKnot) return r;
                if (GenLocalDate.HourOfDay(r.map) < 15) continue;
                if (PeopleEvents2Math.KnotNight(GenLocalDate.DayOfYear(r.map), PeopleEvents2Math.KnotDay(r.faction.loadID), GenLocalDate.Year(r.map), r.knotYear)) return r;
            }
            return null;
        }

        /// <summary>The crossing's year: on their knot night, their letter asking to rest at it; while it stands, the visit goes on.</summary>
        public static bool KnotHour(Visit v, int now)
        {
            var r = PeopleEvents2Book.Instance?.crossings?.FirstOrDefault(c => c != null && c.map == v.map && c.faction != null && c.faction.loadID == v.stake);
            if (r == null || r.faction == null || r.faction.defeated) return false;
            v.until = now + 30 * GenDate.TicksPerDay;
            if (KnotNow() != r || !Tell.CanAsk) return true;
            if (PeopleEvents2.Fire("Rimfolk_Evt_KnotNight", r.map, r.faction)) r.knotYear = GenLocalDate.Year(r.map);
            return true;
        }

        /// <summary>Where they rest by it: a dry cell at one end of the crossing.</summary>
        public static IntVec3 RestSpot(CrossingRecord r)
        {
            var map = r?.map;
            if (map == null || r.cells.NullOrEmpty()) return IntVec3.Invalid;
            foreach (var c in r.cells)
                if (c.InBounds(map) && !c.GetTerrain(map).IsWater && c.Standable(map))
                    return c;
            return CellFinder.TryFindRandomCellNear(r.cells[0], map, 4, x => x.Standable(map), out var near) ? near : IntVec3.Invalid;
        }
    }

    /// <summary>
    /// Boatmen of {FACTION} at the gate: the colony's river ({COUNT} wood for {TALE}) could have a crossing, laid in a day,
    /// and the colony's builder {A_nameDef} may work beside them.
    /// </summary>
    public class HappeningWorker_RiverCrossing : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Fishers);
            return f != null && RiverCrossing.LayJob != null && RiverCrossing.On(map) == null && RiverCrossing.Line(map, out _) != null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Fishers);
            var cells = RiverCrossing.Line(ctx.map, out int water);
            if (ctx.faction == null || cells == null) return false;
            var bridge = RiverCrossing.BridgeFor(PeopleEvents.AgeOf(ctx.faction));
            ctx.note = bridge.defName + "|" + RiverCrossing.Encode(cells);
            ctx.count = RiverCrossing.Wood(water, cells.Count - water, bridge);
            ctx.variant = water;
            ctx.spot = cells[Mathf.Clamp(water / 2, 0, cells.Count - 1)];
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Ferryman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < Rand.RangeInclusive(1, 2); i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Fisher"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Construction, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            ctx.tale = ("CC_PEv2_Bridge_" + (bridge == TerrainDefOf.Bridge ? "rope" : "piles")).Translate().Resolve();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add((ctx.note ?? "").StartsWith("Bridge|") ? "rope" : "piles");
            if (ctx.a == null) tags.Add("nobuilder");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 1 && ctx.a == null) return "CC_PEv2_NoBuilder".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "lay" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The boatmen lay the crossing (RiverCrossing.Begin), the wood carried out to them first.</summary>
    public class DilemmaEffect_RiverCrossing : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv2_Impact_Crossing".Translate(hc.stranger?.LabelShort ?? "?", hc.variant, hc.tale ?? "?").Resolve(), "CC_PEv2_Impact_CrossingBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PEv2_Impact_CrossingOurs".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_CrossingOursBrief".Translate().Resolve(), "pev2crossing");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            int bar = (hc.note ?? "").IndexOf('|');
            if (bar < 0) return;
            var bridge = DefDatabase<TerrainDef>.GetNamedSilentFail(hc.note.Substring(0, bar)) ?? TerrainDefOf.Bridge;
            RiverCrossing.Begin(hc, RiverCrossing.Decode(hc.note.Substring(bar + 1)), bridge);
        }
    }

    /// <summary>A boatman lays one cell of the crossing: to it, a bar of work with the dust of it, then laid (RiverCrossing.LaidBy). TargetA the cell, count the ticks.</summary>
    public class JobDriver_LayCrossing : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Laid by another of them meanwhile: on to the next.
            this.FailOn(() => RiverCrossing.Laid(job.targetA.Cell, pawn.Map));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("LayCrossing");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
                if (pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowDustPuff(job.targetA.Cell, pawn.Map, 0.6f);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("LaidCrossing");
            done.initAction = () => RiverCrossing.LaidBy(pawn, job.targetA.Cell);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// {FACTION}'s boatmen at the gate on their knot night, asking to rest at the crossing they laid ({PLACE} is it). Fired
    /// only on the night itself (RiverCrossing.KnotHour).
    /// </summary>
    public class HappeningWorker_KnotNight : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => RiverCrossing.KnotNow() is CrossingRecord r && r.map == map;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = RiverCrossing.KnotNow();
            if (r == null || r.map != ctx.map) return false;
            ctx.faction = r.faction;
            ctx.spot = RiverCrossing.RestSpot(r);
            if (!ctx.spot.IsValid) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Ferryman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < Rand.RangeInclusive(1, 3); i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Fisher"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.count = ctx.group.Count + 1;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "rest" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>They rest the night at the crossing: their own stay by it until the morning, a knot tied for the colony.</summary>
    public class DilemmaEffect_KnotRest : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PEv2_Impact_KnotRest".Translate(ctx.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_KnotRestBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            if (map == null || !hc.spot.IsValid) return;
            var party = PeoplesG.Party(hc).Where(p => PeoplesG.Up(p, map)).ToList();
            if (party.Count == 0) return;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = (24 - hour + 7) % 24;
            if (hours < 6) hours += 24;
            try
            {
                Lords.Leave(party);
                LordMaker.MakeNewLord(hc.faction, new LordJob_VisitOnErrand(hc.faction, hc.spot, hours * GenDate.TicksPerHour), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the knot night's rest", e);
            }
            if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents2Math.KnotGoodwill, false);
        }
    }
}
