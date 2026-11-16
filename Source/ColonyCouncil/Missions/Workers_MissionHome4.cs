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
    // Quests at home, round 2, the second half (Q11, Q12 and Q13; Q3, Q4, Q6 and Q8 are in Workers_MissionHome3.cs): their
    // seed kept cold in the colony's cold store, their book copied at the colony's research bench, and their musicians for
    // a night at the colony's party.

    // ===================== Q11. Seed grain kept cold =====================

    /// <summary>
    /// A neighbour asks the colony to keep the seed for their spring sowing over the cold months (a stack of a crop of their
    /// own age, every enabled mod's): their carts set it down at the gate, and the colony carries it into its cold store (a
    /// cold room, an ice house, a freezer, by its means) and keeps it there, at freezing or colder and untouched, until the
    /// sowing. Every hour the stack is looked at where it lies: warmer than freezing for more than half a day in all, some
    /// of it used, rotten or gone, it is no good to them, and the colony may keep what is left. Kept, their carts come for it
    /// and pay.
    /// </summary>
    public class HappeningWorker_MissionSeedGrain : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuests2Math.SeedKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 1;

        private static ThingDef Crop(Mission m) => m.goals.Count == 0 ? null : DefDatabase<ThingDef>.GetNamedSilentFail(m.goals[0].what);

        /// <summary>The cold months: autumn, winter, or a land always in winter.</summary>
        private static bool ColdSeason(Map map)
        {
            var s = GenLocalDate.Season(map);
            return s == Season.Fall || s == Season.Winter || s == Season.PermanentWinter;
        }

        private static ThingDef PickCrop(Faction f, Map map) =>
            ThingRoles.From(ThingRole.Produce, f, map, 0, d => d.stackLimit >= 50 && d.GetCompProperties<CompProperties_Rottable>() is CompProperties_Rottable r && r.daysToRotStart > 0f)
            ?? ThingDefOf.RawPotatoes;

        protected override bool Possible(Map map) => Neighbor(map) != null && ColdSeason(map) && HomeQuests2.ColdStore(map, out _);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var crop = PickCrop(m.faction, map);
            if (crop == null) return false;
            int n = HomeQuests2Math.SeedCount(crop.stackLimit, Rand.Value);
            m.goals.Add(new MissionGoal { what = crop.defName, count = n });
            m.target = n;
            m.tale = HomeQuests2.ColdStoreWord(map);
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, null, SkillDefOf.Plants);
            if (m.a == null) return false;
            m.days = HomeQuests2Math.Days(HomeQuest2.SeedGrain, Rand.Value);
            m.reward = HomeQuests2.Reward(HomeQuest2.SeedGrain, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.SeedGrain);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.SeedGrain);
            Fill(m, ctx);
            // The offer's words: {COUNT} {THING} is the seed.
            ctx.count = n;
            ctx.thing = crop;
            return true;
        }

        /// <summary>The terms: {COUNT} {THING} their seed, {TALE} the colony's cold store, {DAYS} until the sowing, {ITEM} the pay.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => Mathf.RoundToInt(mm.days), mm => mm.reward);

        /// <summary>Taken on: their carts bring the seed to the gate (Couriers/); else it is set down where traders leave goods.</summary>
        public override bool Start(Mission m, Map map)
        {
            var crop = Crop(m);
            if (crop == null) return false;
            var stack = ThingMaker.MakeThing(crop);
            stack.stackCount = m.target;
            m.thing = stack;
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            int now = Find.TickManager.TicksGame;
            m.lastLook = now;
            if (Couriers.Deliver(map, new List<Thing> { stack }, m.faction, gate))
            {
                m.stage = 0;
                m.due = now + GenDate.TicksPerDay * 3 / 2;
                return true;
            }
            if (!GenPlace.TryPlaceThing(stack, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near, out var placed)) return false;
            m.thing = placed ?? stack;
            SetDown(m, map, now);
            return true;
        }

        /// <summary>The seed is on the colony's land: it has six hours to be carried into the cold store before the cold is counted.</summary>
        private void SetDown(Mission m, Map map, int now)
        {
            m.stage = 1;
            m.due = now + HomeQuests2Math.CarryHours * GenDate.TicksPerHour;
            m.lastLook = now;
            m.goals[0].have = m.thing?.stackCount ?? 0;
            string line = Line(More?.ready, m, map, Missions.GoodsText(m), null);
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, Targets(m), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The seed as it is now: the stack it was, or, if that was put with the colony's own of the same crop, the stack it
        /// went into (the biggest of that crop on the map, as long as it holds as much). Null when there's none.
        /// </summary>
        private static Thing Seed(Mission m, Map map)
        {
            var t = m.thing;
            if (t != null && !t.Destroyed && t.MapHeld == map) return t;
            var crop = Crop(m);
            if (crop == null) return null;
            var best = map.listerThings.ThingsOfDef(crop).Where(x => x.Spawned).OrderByDescending(x => x.stackCount).FirstOrDefault();
            if (best != null && best.stackCount >= m.target) m.thing = best;
            return best;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var crop = Crop(m);
            if (crop == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    // On their carts: set down once they reach the gate (put with a stack of the same lying there, that one).
                    var t = m.thing;
                    if (t != null && t.Destroyed && Seed(m, map) is Thing put && put.stackCount >= m.target) t = put;
                    if (t != null && t.Spawned && t.Map == map)
                    {
                        SetDown(m, map, now);
                        return;
                    }
                    if (now < m.due) return;
                    // Nobody brought it after all: set down where traders leave goods.
                    if (t != null && !t.Destroyed && !t.Spawned && t.holdingOwner == null && GenPlace.TryPlaceThing(t, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near))
                        SetDown(m, map, now);
                    else if (now > m.due + GenDate.TicksPerDay) Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                case 1:
                    Keep(m, map, now);
                    return;
                case 2:
                    CarryOut(m, map, now);
                    return;
                default:
                    Collect(m, map, now);
                    return;
            }
        }

        /// <summary>Every look: where it stands; every hour after the carrying time, whether it is cold where it lies.</summary>
        private void Keep(Mission m, Map map, int now)
        {
            var t = Seed(m, map);
            int have = t != null && t.def == Crop(m) ? Mathf.Min(t.stackCount, m.target) : 0;
            m.goals[0].have = have;
            bool gone = t == null;
            bool rotten = t?.TryGetComp<CompRottable>() is CompRottable rot && rot.Stage != RotStage.Fresh;
            if (!gone && now - m.lastLook >= GenDate.TicksPerHour)
            {
                m.lastLook += GenDate.TicksPerHour;
                if (now >= m.due && !HomeQuests2Math.Cold(GenTemperature.GetTemperatureForCell(t.PositionHeld, map)))
                {
                    m.tally++;
                    if (m.misses == 0)
                    {
                        // Told once, the first warm hour.
                        m.misses = 1;
                        Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ2_Seed_Warm".Translate((m.faction?.Name ?? "?").Named("FACTION"), Missions.GoodsText(m).Named("ITEM"),
                            (m.tale ?? "?").Named("STORE"), HomeQuests2Math.WarmHoursMax.Named("MAX")).Resolve(), new LookTargets(t.SpawnedParentOrMe), MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
            var end = HomeQuests2Math.SeedOutcome(gone, have, m.target, rotten, m.tally, now >= m.deadline);
            switch (end)
            {
                case SeedEnd.Keeping:
                    return;
                case SeedEnd.Kept:
                    Kept(m, map, t, now);
                    return;
                default:
                {
                    // No good to them: the colony may keep what is left.
                    string tag = end == SeedEnd.Lost ? "lost" : end == SeedEnd.Eaten ? "eaten" : end == SeedEnd.Spoiled ? "spoiled" : "warm";
                    if (t != null && t.Spawned) t.SetForbidden(false, false);
                    Missions.Goodwill(m, end == SeedEnd.Warm ? -m.penalty / 2 : -m.penalty);
                    Missions.Finish(m, MissionState.Failed, tag, Missions.GoodsText(m), t != null && t.Spawned ? new LookTargets(t) : LookTargets.Invalid);
                    return;
                }
            }
        }

        /// <summary>
        /// The sowing is near: their seed (just their own, if it was put with the colony's) is carried out of the cold store by
        /// one of the colony's people, since visitors keep out of it (Visitors/KeepOut.cs), and their carts come for it and pay.
        /// </summary>
        private void Kept(Mission m, Map map, Thing t, int now)
        {
            try
            {
                // Put with the colony's own: the rest is left where it lies.
                if (t.stackCount > m.target && t.Spawned)
                {
                    var rest = t.SplitOff(t.stackCount - m.target);
                    GenPlace.TryPlaceThing(rest, t.Position, map, ThingPlaceMode.Near);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the seed set apart", e);
            }
            m.thing = t;
            m.stage = 2;
            m.lastLook = now;
            m.due = now;
            CarryOut(m, map, now);
        }

        /// <summary>Lying where visitors keep out (a cold store with food in it): their carts couldn't reach it there.</summary>
        private static bool KeptIn(Thing t) => t.Spawned && t.GetRoom() is Room r && KeepOut.IsKept(r);

        /// <summary>
        /// Out to the gate for their carts: one of the colony's haulers carries it out of the cold store to a free place by
        /// where traders leave goods; out, it is forbidden again and their carts are sent for. Nobody to carry it within
        /// half a day, their carts come all the same.
        /// </summary>
        private void CarryOut(Mission m, Map map, int now)
        {
            var t = m.thing;
            if (t == null || t.Destroyed || !HomeQuests.OnMap(t, map))
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", Missions.GoodsText(m));
                return;
            }
            // In someone's hands on the way out: looked at again when it is set down.
            if (!t.Spawned) return;
            if (!KeptIn(t) || now - m.lastLook > GenDate.TicksPerDay / 2)
            {
                t.SetForbidden(true, false);
                Collecting(m, map, now);
                return;
            }
            if (now < m.due) return;
            m.due = now + GenDate.TicksPerHour;
            var spot = DropCellFinder.TradeDropSpot(map);
            if (!CellFinder.TryFindRandomCellNear(spot, map, 6, c => c.Standable(map) && c.GetFirstItem(map) == null && !c.Roofed(map) && !(c.GetRoom(map) is Room r && KeepOut.IsKept(r)),
                    out var cell)) return;
            var hauler = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.Drafted && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p)
                                                                       && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some) && p.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(t.Position)).FirstOrDefault();
            if (hauler == null) return;
            t.SetForbidden(false, false);
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, cell);
            job.count = t.stackCount;
            job.haulMode = HaulMode.ToCellNonStorage;
            job.haulOpportunisticDuplicates = false;
            hauler.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (hauler.CurJob != job) t.SetForbidden(true, false);
        }

        /// <summary>Their carts are sent for it, and bring the pay (Couriers/); else it's taken and paid at once.</summary>
        private void Collecting(Mission m, Map map, int now)
        {
            m.stage = 3;
            m.lastLook = now;
            var t = m.thing;
            if (!t.Spawned || !Couriers.CollectThings(map, new List<Thing> { t }, m.faction, HomeQuests.PayThings(m, m.reward))) Done(m, map, true);
        }

        private void Collect(Mission m, Map map, int now)
        {
            var t = m.thing;
            if (t == null || t.Destroyed || HomeQuests.HeldBy(t, m.faction) || !HomeQuests.OnMap(t, map)) Done(m, map, false);
            else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Done(m, map, true);
        }

        private void Done(Mission m, Map map, bool now)
        {
            if (now)
            {
                if (m.thing != null && !m.thing.Destroyed) m.thing.Destroy();
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "kept", HomeQuests.RewardWords(m, m.reward));
        }

        public override LookTargets Targets(Mission m)
        {
            var t = m.thing;
            return t != null && !t.Destroyed && t.SpawnedOrAnyParentSpawned ? new LookTargets(t.SpawnedParentOrMe) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var map = m.Map;
            string goods = Missions.GoodsText(m);
            if (m.stage == 0) return "CC_HomeQ2_Seed_Status0".Translate((m.faction?.Name ?? "?").Named("FACTION"), goods.Named("ITEM")).Resolve();
            if (m.stage == 2) return "CC_HomeQ2_Seed_StatusOut".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            if (m.stage >= 3) return "CC_HomeQ2_Seed_Status2".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            var t = map == null ? null : Seed(m, map);
            if (t == null) return null;
            float temp = GenTemperature.GetTemperatureForCell(t.PositionHeld, map);
            string where = HomeQuests2Math.Cold(temp) ? "CC_HomeQ2_Seed_Cold" : "CC_HomeQ2_Seed_NotCold";
            return where.Translate(temp.ToStringTemperature("F0").Named("TEMP")).Resolve() + " "
                   + "CC_HomeQ2_Seed_Warmth".Translate(m.tally.Named("WARM"), HomeQuests2Math.WarmHoursMax.Named("MAX")).Resolve()
                   + (t.IsForbidden(Faction.OfPlayer) ? "" : " " + "CC_HomeQ2_Seed_NotForbidden".Translate().Resolve());
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "set down now", "kept", "warm", "eaten" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            int now = Find.TickManager.TicksGame;
            switch (outcome)
            {
                case "set down now":
                {
                    // Out of their carts' goods and at the gate now.
                    var t = m.thing;
                    if (m.stage != 0 || t == null || t.Destroyed) return;
                    if (!t.Spawned)
                    {
                        t.holdingOwner?.Remove(t);
                        if (!GenPlace.TryPlaceThing(t, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near, out var placed)) return;
                        if (placed != null) m.thing = placed;
                    }
                    SetDown(m, map, now);
                    return;
                }
                case "kept":
                    if (m.stage == 1) m.deadline = now;
                    return;
                case "warm":
                    if (m.stage == 1) m.tally = HomeQuests2Math.WarmHoursMax + 1;
                    return;
                default:
                    if (m.stage == 1 && Seed(m, map) is Thing s && s.stackCount > 1) s.SplitOff(1).Destroy();
                    return;
            }
        }
    }

    // ===================== Q12. Their book copied =====================

    /// <summary>
    /// A neighbour's scholar brings a book of their people's (their stories, their learning, a manual, by their age) and
    /// stays at the colony as its guest while its researchers copy it at a research bench: hours of real work there (a job
    /// of its own under their research work, done before the research), with the game's own learning from it. Copied in
    /// time, the scholar pays and leaves the colony the copy, a book of the game's own kind; half copied, half the pay and
    /// no copy.
    /// </summary>
    public class HappeningWorker_MissionBookCopy : HappeningWorker_MissionHost
    {
        protected override string Kind => HomeQuests2Math.CopyKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 1;

        public override bool OwnMemory => false;

        private static bool Researcher(Pawn p) =>
            p != null && !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Research) && !(p.skills?.GetSkill(SkillDefOf.Intellectual)?.TotallyDisabled ?? true);

        private static bool HasBench(Map map) => map.listerBuildings.allBuildingsColonist.Any(b => b is Building_ResearchBench && b.Spawned);

        protected override bool Possible(Map map) => Neighbor(map) != null && HasBench(map) && IncidentWorker_Happening.Colonists(map).Any(Researcher);

        /// <summary>The days the scholar stays (kept in the mission's <see cref="Mission.misses"/>, which this quest has no other use for).</summary>
        private static int StayDays(Mission m) => Mathf.Max(1, m.misses);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.x = HomeQuests.Person(m.faction, new FloatRange(35f, 70f));
            if (m.x == null) return false;
            m.name = m.x.LabelShort;
            m.variant = HomeQuests2Math.BookTag(HomeQuests.AgeOf(m.faction), Ages.IsEmpire(m.faction));
            m.tale = ("CC_HomeQ2_Book_" + m.variant).Translate().Resolve();
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = IncidentWorker_Happening.Colonists(map).Where(Researcher).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level).FirstOrDefault();
            if (m.a == null) return false;
            m.target = HomeQuests2Math.CopyHours(Rand.Value);
            m.wait = Rand.Range(HomeQuestsMath.ComeMin, HomeQuestsMath.ComeMax);
            m.misses = HomeQuests2Math.Days(HomeQuest2.BookCopy, Rand.Value);
            m.days = Mathf.Ceil(m.wait + m.misses + 1f);
            m.reward = HomeQuests2.Reward(HomeQuest2.BookCopy, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.BookCopy);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.BookCopy);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.count = m.target;
            return true;
        }

        /// <summary>The terms: {COUNT} the hours of work, {DAYS} the scholar's stay, {TALE} the book, {ITEM} the pay.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, StayDays, mm => mm.reward);

        /// <summary>The copying the quest asks for is under way on this map: the scholar is here and the hours aren't done.</summary>
        public static bool Copying(Map map, out Mission m)
        {
            m = HomeQuests2.Open(HomeQuests2Math.CopyKind);
            return m != null && map != null && m.state == MissionState.Active && m.stage == 1 && m.Map == map && m.tally < m.target * GenDate.TicksPerHour;
        }

        /// <summary>Some work on the copy at the bench, by <paramref name="p"/>: true once it's done.</summary>
        public static bool Copied(Pawn p, int delta)
        {
            if (!Copying(p?.Map, out var m)) return true;
            m.tally += delta;
            m.a = p;
            return m.tally >= m.target * GenDate.TicksPerHour;
        }

        /// <summary>How far along the copy is, 0 to 1.</summary>
        public static float Progress(Map map)
        {
            var m = HomeQuests2.Open(HomeQuests2Math.CopyKind);
            return m == null || m.target <= 0 ? 0f : Mathf.Clamp01(m.tally / (float)(m.target * GenDate.TicksPerHour));
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, x.Dead ? "died" : "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                var spot = PlaySpots.GateCell(map, null);
                if (!Arrive(m, map, spot.IsValid ? spot : (IntVec3?)null, StayDays(m) * 24f + 12f))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                m.due = now + StayDays(m) * GenDate.TicksPerDay;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(StayDays(m) * GenDate.TicksPerDay));
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            Tend(m, map);
            bool gone = !x.Spawned || x.Map != map;
            var end = HomeQuests2Math.CopyOutcome(m.tally, m.target, GenDate.TicksPerHour, gone || now >= m.due);
            if (end == CopyEnd.Copying) return;
            End(m, map, end);
        }

        /// <summary>Done (the colony keeps a copy), half done (half the pay), or not.</summary>
        private void End(Mission m, Map map, CopyEnd end)
        {
            var x = m.x;
            bool here = x != null && x.Spawned && x.Map == map && !x.Dead;
            var look = here ? new LookTargets(x) : LookTargets.Invalid;
            if (end == CopyEnd.Done)
            {
                var book = Copy(m, map);
                HomeQuests2.PayInPerson(m, map, here ? x : null, m.reward);
                HomeQuests2.Say(x, "Rimfolk_Pool_HomeQ2_CopyDone", m.a != null && m.a.Spawned && Together.Can(x, m.a) ? m.a : null);
                SeeOff(m, map);
                Missions.Goodwill(m, m.goodwill);
                Missions.Finish(m, MissionState.Done, book != null ? "copied" : "copiednobook", HomeQuests.RewardWords(m, m.reward), book != null ? new LookTargets(book) : look);
                return;
            }
            SeeOff(m, map);
            if (end == CopyEnd.Half)
            {
                int pay = HomeQuestsMath.Tens(m.reward / 2f);
                HomeQuests2.PayInPerson(m, map, here ? x : null, pay);
                Missions.Goodwill(m, m.goodwill / 2);
                Missions.Finish(m, MissionState.Done, "half", HomeQuests.RewardWords(m, pay), look, LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            Missions.Finish(m, MissionState.Failed, "uncopied", null, look);
        }

        /// <summary>
        /// The colony's copy: a book of the game's own (a novel for a tribe's stories, a textbook for learning, a schematic for a
        /// manual), its quality by the copier's skill, written by the scholar, set down by the bench it was copied at.
        /// </summary>
        private static Thing Copy(Mission m, Map map)
        {
            string name = m.variant == "stories" ? "Novel" : m.variant == "manual" ? "Schematic" : "TextBook";
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name) ?? DefDatabase<ThingDef>.GetNamedSilentFail("TextBook");
            if (def == null || map == null) return null;
            try
            {
                var copier = m.a != null && !m.a.Dead ? m.a : null;
                var book = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.RandomStuffFor(def) : null);
                var q = copier != null ? QualityUtility.GenerateQualityCreatedByPawn(copier, SkillDefOf.Intellectual, false) : QualityCategory.Normal;
                book.TryGetComp<CompQuality>()?.SetQuality(q, ArtGenerationContext.Colony);
                if (book is Book b && m.x != null && !m.x.Dead) b.GenerateBook(m.x);
                var at = copier != null && copier.Spawned && copier.Map == map ? copier.Position : DropCellFinder.TradeDropSpot(map);
                return GenPlace.TryPlaceThing(book, at, map, ThingPlaceMode.Near, out var placed) ? placed ?? book : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the copy of the book", e);
                return null;
            }
        }

        /// <summary>The colony's plainer food for them, else raw food.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            if (m.stage == 0) return "CC_HomeQ2_Copy_Status0".Translate(x.Named("X")).Resolve();
            int hours = Mathf.Min(m.target, m.tally / GenDate.TicksPerHour);
            return "CC_HomeQ2_Copy_Status1".Translate(x.Named("X"), hours.Named("HOURS"), m.target.Named("TARGET")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "copied", "half", "uncopied" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            if (m.stage != 1) return;
            int need = m.target * GenDate.TicksPerHour;
            m.tally = outcome == "copied" ? need : outcome == "half" ? need / 2 : 0;
            End(m, map, outcome == "copied" ? CopyEnd.Done : outcome == "half" ? CopyEnd.Half : CopyEnd.Little);
        }
    }

    // ===================== Q13. Musicians for a night =====================

    /// <summary>
    /// A neighbour's musicians, on their way somewhere, ask to stay one night at the colony and play for it. They come in the
    /// afternoon; in the evening the colony throws a party (the game's own gathering, which its people join), and they play
    /// at it: at the colony's own instrument where it has one near the party (the game's own playing), else on their own
    /// (drums for a tribe, harps for the rest, the sound of that instrument and the notes over them). They eat at the
    /// colony's table and sleep in its beds, and leave in the morning, paying for their keep.
    /// </summary>
    public class HappeningWorker_MissionMusicians : HappeningWorker_MissionHost
    {
        protected override string Kind => HomeQuests2Math.MusicKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 1;

        public override bool OwnMemory => false;

        protected override bool Possible(Map map) =>
            Neighbor(map) != null && GatheringDefOf.Party?.Worker != null && map.gatherSpotLister.activeSpots.Count > 0 && StorytellerEvents.Adults(map).Count() >= 3;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            int n = HomeQuests2Math.Musicians(Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(m.faction, new FloatRange(18f, 60f));
                if (p == null) continue;
                if (m.x == null) m.x = p;
                else m.group.Add(p);
            }
            if (m.x == null) return false;
            m.name = m.x.LabelShort;
            bool tribal = HomeQuests.AgeOf(m.faction) <= HomeQuestsMath.Tribal && !Ages.IsEmpire(m.faction);
            m.tale = (tribal ? "CC_HomeQ2_Music_Drums" : "CC_HomeQ2_Music_Harps").Translate().Resolve();
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.days = 2f;
            m.reward = HomeQuests2.Reward(HomeQuest2.Musicians, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.Musicians);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.Musicians);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.count = Guests(m).Count();
            return true;
        }

        /// <summary>The terms: {COUNT} how many come, {TALE} what they play, {ITEM} the pay for their keep.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => 0, mm => mm.reward);

        public override bool Start(Mission m, Map map)
        {
            if (!Guests(m).Any(p => !p.Dead && !p.Discarded)) return false;
            // They come in the afternoon, for the evening.
            m.due = Find.TickManager.TicksGame + HomeQuests2.TicksUntilHour(map, HomeQuests2Math.ArriveHour, 0.25f);
            return true;
        }

        /// <summary>The party the colony threw for their evening, while it lasts.</summary>
        private static Lord Party(Mission m, Map map)
        {
            if (map == null || !m.cell.IsValid || m.misses == 0) return null;
            return map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_Joinable_Party p && p.Spot == m.cell);
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            // Any of them killed or taken prisoner while here.
            if (m.stage >= 1 && Guests(m).Any(p => p.Dead || p.IsPrisonerOfColony))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    var spot = map.gatherSpotLister.activeSpots.FirstOrDefault()?.parent?.Position ?? PlaySpots.OpenCell(map, null);
                    float hours = HomeQuests2Math.HoursUntil(GenLocalDate.HourFloat(map), HomeQuests2Math.MorningHour, 6f) + 4f;
                    if (!Arrive(m, map, spot.IsValid ? spot : (IntVec3?)null, hours))
                    {
                        Missions.Finish(m, MissionState.Failed, "ended", null);
                        return;
                    }
                    m.stage = 1;
                    m.lastLook = now;
                    string line = Line(More?.ready, m, map, null, null);
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, Targets(m), MessageTypeDefOf.PositiveEvent);
                    return;
                }
                case 1:
                    Lacks(m, Tend(m, map));
                    if (hour >= HomeQuests2Math.PlayFrom && hour < HomeQuests2Math.PlayUntil) Evening(m, map, hour);
                    else if (hour < HomeQuests2Math.ArriveHour && now - m.lastLook > GenDate.TicksPerHour * 8) End(m, map);
                    return;
                case 2:
                    Play(m, map, now, hour);
                    return;
                default:
                    // The night: they eat and sleep as the colony's guests, and leave in the morning.
                    Lacks(m, Tend(m, map));
                    if (hour >= HomeQuests2Math.MorningHour && hour < 14 && now - m.lastLook > GenDate.TicksPerHour * 4) End(m, map);
                    return;
            }
        }

        private static void Lacks(Mission m, Hosting.Lack lack) => m.lacks |= (int)lack;

        /// <summary>The evening: the colony's party for them (the game's own), or, if it can't be thrown by ten, they play for whoever comes.</summary>
        private void Evening(Mission m, Map map, int hour)
        {
            var org = m.a != null && m.a.Spawned && m.a.Map == map && !m.a.Dead && GatheringsUtility.PawnCanStartOrContinueGathering(m.a) ? m.a : null;
            var party = GatheringDefOf.Party;
            bool started = false;
            try
            {
                if (party?.Worker != null && map.dangerWatcher.DangerRating == StoryDanger.None)
                {
                    var before = map.lordManager.lords.ToList();
                    if (org != null ? party.Worker.CanExecute(map, org) && party.Worker.TryExecute(map, org) : party.Worker.CanExecute(map) && party.Worker.TryExecute(map))
                    {
                        var lord = map.lordManager.lords.FirstOrDefault(l => !before.Contains(l) && l.LordJob is LordJob_Joinable_Party);
                        if (lord?.LordJob is LordJob_Joinable_Party p)
                        {
                            m.cell = p.Spot;
                            m.misses = 1;
                            started = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the musicians' party", e);
            }
            if (!started && hour < HomeQuests2Math.PartyBy) return;
            if (!started) m.cell = map.gatherSpotLister.activeSpots.FirstOrDefault()?.parent?.Position ?? PlaySpots.OpenCell(map, m.x);
            m.stage = 2;
            m.due = Find.TickManager.TicksGame + HomeQuests2.TicksUntilHour(map, HomeQuests2Math.PlayUntil, 0.5f);
            HomeQuests2.Say(m.x, "Rimfolk_Pool_HomeQ2_MusicStart", null, started ? "party" : "noparty");
        }

        /// <summary>Playing: each musician free plays a stint, at the colony's instrument near the party if there's one for them, else on their own by it.</summary>
        private void Play(Mission m, Map map, int now, int hour)
        {
            var lord = Party(m, map);
            bool over = now >= m.due || hour >= HomeQuests2Math.PlayUntil || hour < 6 || m.misses == 1 && lord == null;
            var playing = Here(m, map).Where(p => p.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Play).ToList();
            if (over)
            {
                foreach (var p in playing) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                m.stage = 3;
                m.lastLook = now;
                return;
            }
            var own = HomeQuests2.OwnInstrument(m.faction);
            foreach (var p in Here(m, map).ToList())
            {
                if (p.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Play || !Hosting.Free(p)) continue;
                if ((p.needs?.food?.CurLevelPercentage ?? 1f) < Hosting.EatBelow) continue;
                Job job;
                if (HomeQuests2.Instrument(p, m.cell, 14f, out var instrument, out var seat)) job = JobMaker.MakeJob(HomeQuests2DefOf.Rimfolk_HomeQ_Play, instrument, seat, m.cell);
                else
                {
                    var cell = PlaySpots.FreeCellNear(m.cell, map, p);
                    if (!cell.IsValid) continue;
                    job = JobMaker.MakeJob(HomeQuests2DefOf.Rimfolk_HomeQ_Play, LocalTargetInfo.Invalid, cell, m.cell);
                    job.thingDefToCarry = own;
                }
                job.count = GenDate.TicksPerHour;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            // The hungry still eat, at the colony's table.
            Lacks(m, Tend(m, map) & Hosting.Lack.Food);
        }

        /// <summary>The morning: they pay for their keep and go.</summary>
        private void End(Mission m, Map map)
        {
            var x = m.x;
            bool here = x != null && x.Spawned && x.Map == map && !x.Dead;
            bool slept = (m.lacks & (int)Hosting.Lack.Bed) == 0;
            HomeQuests2.PayInPerson(m, map, here ? x : null, m.reward);
            HomeQuests2.Say(x, "Rimfolk_Pool_HomeQ2_MusicLeave", null, slept ? "slept" : "rough");
            SeeOff(m, map);
            Missions.Goodwill(m, HomeQuests2Math.MusicGoodwill(m.goodwill, slept));
            string outcome = m.misses == 1 ? "party" : "noparty";
            Missions.Finish(m, MissionState.Done, outcome, HomeQuests.RewardWords(m, m.reward), here ? new LookTargets(x) : LookTargets.Invalid,
                m.misses == 1 && slept ? null : LetterDefOf.NeutralEvent);
        }

        /// <summary>The colony's plainer food for them, else raw food.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => Ours(m, o), p).Where(b => Hosting.CanShare(b, p)).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override void Release(Mission m)
        {
            foreach (var p in Guests(m).Where(p => p.Spawned && p.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Play).ToList()) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            base.Release(m);
        }

        public override string Status(Mission m)
        {
            int n = Guests(m).Count();
            switch (m.stage)
            {
                case 0:
                    return "CC_HomeQ2_Music_Status0".Translate((m.name ?? "?").Named("NAME"), n.Named("COUNT")).Resolve();
                case 1:
                    return "CC_HomeQ2_Music_Status1".Translate(HomeQuests2Math.PlayFrom.Named("HOUR")).Resolve();
                case 2:
                    return (m.misses == 1 ? "CC_HomeQ2_Music_Status2" : "CC_HomeQ2_Music_Status2NoParty").Translate().Resolve();
                default:
                    return "CC_HomeQ2_Music_Status3".Translate().Resolve() + ((m.lacks & (int)Hosting.Lack.Bed) != 0 ? " " + "CC_HomeQ2_Music_NoBed".Translate().Resolve() : "");
            }
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "evening now", "morning now", "party", "noparty" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            int now = Find.TickManager.TicksGame;
            switch (outcome)
            {
                case "evening now":
                    if (m.stage == 1) Evening(m, map, HomeQuests2Math.PartyBy);
                    return;
                case "morning now":
                    if (m.stage >= 1) End(m, map);
                    return;
                case "party":
                    m.misses = 1;
                    End(m, map);
                    return;
                default:
                    m.misses = 0;
                    End(m, map);
                    return;
            }
        }
    }
}
