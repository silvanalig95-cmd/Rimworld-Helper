using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Quests at home, round 1, the second half (Q7, Q9 and Q10; Q1, Q2 and Q5 are in Workers_MissionHome.cs): an outlaw
    // hiding on the colony's land, a keepsake lost in a marked area of it, and a wild animal on it tamed for a neighbour.

    // ===================== Q7. An outlaw in the hills =====================

    /// <summary>
    /// An outlaw with a price on their head (of a people at war with the colony, scaled to it) hides somewhere on the
    /// colony's land: at the foot of the hills or near the edge, lying low. Once someone of the colony comes within sight
    /// of them, they fight (through the shared harm budget) or run for the edge; a real fight, the game's own. Captured,
    /// the neighbour's people come for them and pay half as much again; killed, they pay the price. Escaped, or never
    /// found, it ends badly.
    /// </summary>
    public class HappeningWorker_MissionOutlaw : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuestsMath.OutlawKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 3;

        /// <summary>Drawn within the shared harm budget: the setting, the storyteller's lean and the outlaw's turn in it.</summary>
        public override float ChanceFactorNow(IIncidentTarget target)
        {
            float f = base.ChanceFactorNow(target);
            if (f <= 0f) return f;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.Outlaw.system)) return 0f;
            return f * Mathf.Clamp(Harm.ScaleFor(HarmKinds.Outlaw), 0f, 2f);
        }

        /// <summary>A hiding place: at the foot of the hills, or near the edge of the map; far out from the colony.</summary>
        private static bool Hideout(Map map, out IntVec3 cell, out bool hills)
        {
            hills = true;
            if (HomeQuests.FarCell(map, HomeQuestsMath.HideMin, c => HomeQuests.Hills(map, c), out cell)) return true;
            hills = false;
            return HomeQuests.FarCell(map, HomeQuestsMath.HideMin, c => HomeQuests.NearEdge(map, c), out cell);
        }

        /// <summary>Someone of the colony who would go after an outlaw: able to fight.</summary>
        private static Pawn Hunter(Map map) =>
            HomeQuests.Officer(map, Interplay.Marshal, SkillDefOf.Shooting) is Pawn p && !p.WorkTagIsDisabled(WorkTags.Violent) ? p
                : StorytellerEvents.Adults(map).Where(c => !c.WorkTagIsDisabled(WorkTags.Violent)).OrderByDescending(c => c.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();

        protected override bool Possible(Map map) =>
            Harm.On && Harm.SystemOn(HarmKinds.Outlaw.system) && IncidentWorker_Happening.FightsPossible && Neighbor(map) != null && HomeQuests.Enemy(null) != null
            && Hunter(map) != null && Hideout(map, out _, out _);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null || !Hideout(map, out var cell, out bool hills)) return false;
            m.other = HomeQuests.Enemy(m.faction);
            if (m.other == null) return false;
            float power = HomeQuestsMath.OutlawPower(StorytellerUtility.DefaultThreatPointsNow(map));
            var x = HomeQuests.Person(m.other, new FloatRange(20f, 50f), HomeQuests.FighterKind(m.other, power));
            if (x == null) return false;
            m.x = x;
            m.name = x.LabelShort;
            m.cell = cell;
            m.variant = hills ? "hills" : "edge";
            m.tale = HomeQuests.Direction(map, cell);
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = Hunter(map);
            if (m.a == null) return false;
            m.days = HomeQuestsMath.Days(HomeQuest.Outlaw, Rand.Value);
            m.reward = HomeQuests.Reward(HomeQuest.Outlaw, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.Outlaw);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.Outlaw);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.enemy = m.other;
            return true;
        }

        /// <summary>The terms: {ITEM} the price alive and dead, {DAYS} the time to find them, {TALE} which way.</summary>
        protected override string Extra(HappeningContext ctx)
        {
            var m = Missions.Of(ctx) ?? Making;
            if (m == null) return null;
            string price = "CC_HomeQ_Outlaw_Price".Translate(HomeQuests.RewardWords(m, HomeQuestsMath.OutlawPay(m.reward, true)).Named("ALIVE"),
                HomeQuests.RewardWords(m, m.reward).Named("DEAD")).Resolve();
            return HomeQuests.Terms(this, ctx, m, mm => Mathf.RoundToInt(mm.days), mm => mm.reward, price);
        }

        public override bool Start(Mission m, Map map)
        {
            var x = m.x;
            if (x == null || x.Dead || x.Discarded) return false;
            if (!m.cell.IsValid || !m.cell.InBounds(map) || !m.cell.Standable(map) || map.areaManager.Home[m.cell])
            {
                if (!Hideout(map, out var cell, out bool hills)) return false;
                m.cell = cell;
                m.variant = hills ? "hills" : "edge";
                m.tale = HomeQuests.Direction(map, cell);
            }
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, m.cell, map);
            if (!x.Spawned) return false;
            // Their hiding place, kept close: they fight whoever comes near it.
            LordMaker.MakeNewLord(x.Faction, new LordJob_DefendPoint(m.cell, 4f, 12f, false, true), map, new List<Pawn> { x });
            LieLow(x);
            m.stage = 1;
            return true;
        }

        /// <summary>Lying low in the hiding place: asleep on the ground, no threat to anyone until someone comes.</summary>
        private static void LieLow(Pawn x)
        {
            try
            {
                if (x == null || !x.Spawned || x.Downed || x.InMentalState || x.CurJobDef == JobDefOf.LayDown) return;
                var job = JobMaker.MakeJob(JobDefOf.LayDown, x.Position);
                job.forceSleep = true;
                x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the outlaw lying low", e);
            }
        }

        /// <summary>Someone of the colony has come within sight of them, or they've been hurt.</summary>
        private static bool Found(Mission m, Pawn x, Map map)
        {
            if (x.mindState != null && x.mindState.lastHarmTick > m.started) return true;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (!c.Downed && c.Awake() && c.Position.InHorDistOf(x.Position, HomeQuestsMath.FoundRange) && GenSight.LineOfSight(c.Position, x.Position, map)) return true;
            return false;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 3)
            {
                Collect(m, map, now);
                return;
            }
            var end = HomeQuestsMath.OutlawOutcome(x.Dead, x.Faction == Faction.OfPlayer, x.IsPrisonerOfColony, x.Spawned && x.Map == map || x.IsPrisonerOfColony);
            switch (end)
            {
                case OutlawEnd.Killed:
                    Killed(m, map);
                    return;
                case OutlawEnd.Captured:
                    m.stage = 3;
                    m.lastLook = now;
                    if (!HomeQuests.FetchPrisoner(m, map, x, HomeQuestsMath.OutlawPay(m.reward, true))) Captured(m, map, true);
                    return;
                case OutlawEnd.Kept:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "kept", null, x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                    return;
                case OutlawEnd.Fled:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "fled", null);
                    return;
            }
            if (m.stage == 1)
            {
                if (Found(m, x, map))
                {
                    Discovered(m, map, now);
                    return;
                }
                if (now >= m.deadline)
                {
                    GetAway(x, map);
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "unfound", null);
                    return;
                }
                if (x.Awake() && !x.Downed) LieLow(x);
                return;
            }
            // Found, and not down yet when the time is up: they get away.
            if (now >= m.deadline && !x.Downed)
            {
                GetAway(x, map);
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "fled", null, new LookTargets(x));
            }
        }

        /// <summary>Found: up, and fighting where they stand (the shared harm budget's roll), or running for the edge.</summary>
        private void Discovered(Mission m, Map map, int now)
        {
            var x = m.x;
            bool fights = !Harm.On ? false : Harm.Roll(HarmKinds.Outlaw, HomeQuestsMath.FightChance);
            try
            {
                if (!x.Awake()) RestUtility.WakeUp(x);
                if (fights)
                {
                    HarmBook.Instance?.Came(HarmKinds.Outlaw, now);
                    Harm.BudgetChanged();
                }
                else GetAway(x, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the outlaw found", e);
            }
            HomeQuests.Say(x, "Rimfolk_Pool_HomeQ_OutlawFound", null, fights ? "fight" : "run");
            m.stage = 2;
            m.lastLook = now;
            m.target = fights ? 1 : 0;
            Tell.Message(NewsKind.World, Importance.Important, map, (fights ? "CC_HomeQ_Outlaw_Fights" : "CC_HomeQ_Outlaw_Runs").Translate(x.Named("X")).Resolve(),
                new LookTargets(x), MessageTypeDefOf.ThreatSmall);
        }

        /// <summary>Off toward the edge of the map, defending themselves if cornered.</summary>
        private static void GetAway(Pawn x, Map map)
        {
            if (x == null || !x.Spawned || x.Dead || x.Downed || x.IsPrisoner) return;
            try
            {
                if (!x.Awake()) RestUtility.WakeUp(x);
                Lords.Leave(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), map, new List<Pawn> { x });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the outlaw getting away", e);
            }
        }

        private void Killed(Mission m, Map map)
        {
            var x = m.x;
            var at = x?.Corpse != null && x.Corpse.Spawned ? x.Corpse.Position : IntVec3.Invalid;
            HomeQuests.Pay(m, map, at, null, m.reward);
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "killed", HomeQuests.RewardWords(m, m.reward), x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
        }

        private void Collect(Mission m, Map map, int now)
        {
            var x = m.x;
            if (HomeQuests.Collected(m, x))
            {
                Captured(m, map, false);
                return;
            }
            if (x.Dead)
            {
                Killed(m, map);
                return;
            }
            if (!x.IsPrisonerOfColony)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, x.Faction == Faction.OfPlayer ? "kept" : "fled", null);
                return;
            }
            if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Captured(m, map, true);
        }

        private void Captured(Mission m, Map map, bool now)
        {
            int pay = HomeQuestsMath.OutlawPay(m.reward, true);
            if (now)
            {
                HomeQuests.TakeNow(m.x);
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, pay);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "captured", HomeQuests.RewardWords(m, pay));
        }

        public override void Release(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return;
            if (!x.Spawned && m.started < 0) Arrivals.Discard(x);
            else Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
        }

        public override LookTargets Targets(Mission m)
        {
            // Hiding: only where the colony knows they are (the way their people pointed); found: them.
            if (m.stage >= 2 && m.x != null && m.x.Spawned) return new LookTargets(m.x);
            var map = m.Map;
            return map != null && m.cell.IsValid ? new LookTargets(new TargetInfo(m.cell, map)) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null) return null;
            string key = m.stage <= 1 ? "CC_HomeQ_Outlaw_Status1" : m.stage == 2 ? (x.Downed ? "CC_HomeQ_Outlaw_Down" : "CC_HomeQ_Outlaw_Status2") : "CC_HomeQ_Outlaw_Status3";
            return key.Translate(x.Named("X"), (m.tale ?? "?").Named("WAY"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "found", "captured", "killed", "fled", "unfound" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            int now = Find.TickManager.TicksGame;
            switch (outcome)
            {
                case "found":
                    if (m.stage == 1) Discovered(m, map, now);
                    return;
                case "captured":
                    Captured(m, map, true);
                    return;
                case "killed":
                    Killed(m, map);
                    return;
                default:
                    GetAway(m.x, map);
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, outcome, null);
                    return;
            }
        }
    }

    // ===================== Q9. A lost heirloom =====================

    /// <summary>
    /// Someone of a neighbour lost a keepsake crossing the colony's land (a painted hide, a family book, an album of
    /// photographs, by their people's age), and has a fair idea where: an area marked on the map, not the exact place.
    /// The colony's people search it, a spot at a time (right-click in the area with a colonist selected), until they find
    /// it; it lies in the open, and a few hours of rain or snow on it ruin it. Found, it is kept until its owner's people
    /// come for it and pay. Not found in time, their own people come and search for it themselves.
    /// </summary>
    public class HappeningWorker_MissionKeepsake : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuestsMath.KeepsakeKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 1;

        /// <summary>The spots looked at in each search this session (a reload starts the record again; nothing else depends on it).</summary>
        private static readonly Dictionary<int, List<IntVec3>> looked = new Dictionary<int, List<IntVec3>>();

        /// <summary>A place in the open far enough out, with room around it for the area.</summary>
        private static bool Place(Map map, out IntVec3 cell, out IntVec3 area)
        {
            area = IntVec3.Invalid;
            if (!HomeQuests.FarCell(map, 25f, c => !HomeQuests.NearEdge(map, c, (int)HomeQuestsMath.AreaRadius + 2), out cell)) return false;
            // The area's middle somewhere near it, so the keepsake is inside but not at the middle.
            float off = HomeQuestsMath.AreaRadius - HomeQuestsMath.AreaMargin;
            var c0 = cell;
            for (int i = 0; i < 12; i++)
            {
                var a = c0 + (Rand.InsideUnitCircleVec3 * off).ToIntVec3();
                if (a.InBounds(map) && a.InHorDistOf(c0, off)) return (area = a).IsValid;
            }
            area = cell;
            return true;
        }

        protected override bool Possible(Map map) => Neighbor(map) != null && Place(map, out _, out _);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null || !Place(map, out var cell, out var area)) return false;
            m.variant = HomeQuests.KeepsakeTag(m.faction);
            var def = HomeQuests.KeepsakeDef(m.variant);
            if (def == null) return false;
            m.name = InviteForms.Named(m.faction, new FloatRange(30f, 75f), null, out _);
            m.cell = cell;
            m.area = area;
            m.tale = def.label;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, null, SkillDefOf.Animals);
            if (m.a == null) return false;
            m.days = HomeQuestsMath.Days(HomeQuest.Keepsake, Rand.Value);
            m.reward = HomeQuests.Reward(HomeQuest.Keepsake, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.Keepsake);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.Keepsake);
            Fill(m, ctx);
            ctx.thing = def;
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => Mathf.RoundToInt(m.days), m => m.reward);

        /// <summary>The keepsake mission being searched for on this map (the area still marked), if any.</summary>
        public static bool Searching(Map map, out Mission m)
        {
            m = HomeQuests.Open(HomeQuestsMath.KeepsakeKind);
            return m != null && map != null && m.state == MissionState.Active && m.stage == 1 && m.area.IsValid && m.Map == map;
        }

        public static bool InArea(Mission m, IntVec3 c) => m != null && m.area.IsValid && c.InHorDistOf(m.area, HomeQuestsMath.AreaRadius);

        public static IEnumerable<IntVec3> AreaCells(Map map, IntVec3 center) =>
            GenRadial.RadialCellsAround(center, HomeQuestsMath.AreaRadius, true).Where(c => c.InBounds(map));

        public override bool Start(Mission m, Map map)
        {
            if (!m.cell.IsValid || !m.cell.InBounds(map) || !m.area.IsValid)
            {
                if (!Place(map, out var cell, out var area)) return false;
                m.cell = cell;
                m.area = area;
            }
            looked.Remove(m.id);
            m.stage = 1;
            m.lastLook = Find.TickManager.TicksGame;
            return true;
        }

        /// <summary>A colonist has looked around <paramref name="spot"/>: found, or on to the next spot not yet looked at.</summary>
        public static void Looked(Pawn p, IntVec3 spot)
        {
            if (p?.Map == null || !Searching(p.Map, out var m)) return;
            if (!looked.TryGetValue(m.id, out var list)) looked[m.id] = list = new List<IntVec3>();
            list.Add(spot);
            if (HomeQuestsMath.Covers(spot.x - m.cell.x, spot.z - m.cell.z))
            {
                (Missions.Worker(m) as HappeningWorker_MissionKeepsake)?.Found(m, p.Map, p);
                return;
            }
            var next = Next(m, p, list);
            var def = DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_SearchKeepsake.JobName);
            if (!next.IsValid || def == null)
            {
                // Looked everywhere they can get to (it lies somewhere they can't reach from here): told so.
                Tell.Message(NewsKind.World, Importance.Notable, p.Map, "CC_HomeQ_Keepsake_Searched".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                return;
            }
            p.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(def, next), JobTag.Misc);
        }

        /// <summary>The nearest spot in the area that no look has covered yet and that they can reach.</summary>
        private static IntVec3 Next(Mission m, Pawn p, List<IntVec3> done)
        {
            var map = p.Map;
            return AreaCells(map, m.area).Where(c => c.Standable(map) && !done.Any(d => HomeQuestsMath.Covers(c.x - d.x, c.z - d.z)))
                .OrderBy(c => c.DistanceToSquared(p.Position)).FirstOrDefault(c => p.CanReach(c, PathEndMode.OnCell, Danger.Some));
        }

        /// <summary>Found: set down where it lay, the area no longer marked, kept until their people come.</summary>
        private void Found(Mission m, Map map, Pawn finder)
        {
            var t = HomeQuests.Make(HomeQuests.KeepsakeDef(m.variant));
            if (t == null || !GenPlace.TryPlaceThing(t, m.cell, map, ThingPlaceMode.Near)) return;
            int now = Find.TickManager.TicksGame;
            m.thing = t;
            m.area = IntVec3.Invalid;
            m.stage = 2;
            m.a = finder;
            m.due = now + GenDate.TicksPerDay / 2;
            looked.Remove(m.id);
            HomeQuests.Say(finder, "Rimfolk_Pool_HomeQ_KeepsakeFound");
            string line = Line(More?.ready, m, map, null, null);
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(t, finder), MessageTypeDefOf.PositiveEvent);
        }

        public override void Check(Mission m, Map map, int now)
        {
            switch (m.stage)
            {
                case 1:
                    Weather(m, map, now);
                    if (m.state != MissionState.Active || !m.Open) return;
                    if (now >= m.deadline) TheirSearch(m, map, now);
                    return;
                case 2:
                    if (!HomeQuests.OnMap(m.thing, map))
                    {
                        Missions.Goodwill(m, -m.penalty);
                        Missions.Finish(m, MissionState.Failed, "lost", null);
                        return;
                    }
                    if (now < m.due || !m.thing.Spawned) return;
                    m.stage = 3;
                    m.lastLook = now;
                    if (!Couriers.CollectThings(map, new List<Thing> { m.thing }, m.faction, HomeQuests.PayThings(m, m.reward))) Returned(m, map, true);
                    return;
                case 3:
                    if (m.thing == null || m.thing.Destroyed || HomeQuests.HeldBy(m.thing, m.faction) || !HomeQuests.OnMap(m.thing, map)) Returned(m, map, false);
                    else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Returned(m, map, true);
                    return;
                default:
                    Searched(m, map, now);
                    return;
            }
        }

        /// <summary>Each hour of rain or snow on the open ground where it lies counts; enough of them, and it is ruined.</summary>
        private void Weather(Mission m, Map map, int now)
        {
            if (now - m.lastLook < GenDate.TicksPerHour) return;
            m.lastLook += GenDate.TicksPerHour;
            var w = map.weatherManager;
            if (!HomeQuestsMath.Wet(w.RainRate, w.SnowRate) || m.cell.Roofed(map)) return;
            m.tally++;
            if (m.misses == 0)
            {
                m.misses = 1;
                Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ_Keepsake_Wet".Translate((m.name ?? "?").Named("OWNER"),
                    (HomeQuests.KeepsakeDef(m.variant)?.label ?? "?").Named("THING"), HomeQuestsMath.WetHoursToSpoil.Named("MAX")).Resolve(), Targets(m), MessageTypeDefOf.NegativeEvent);
            }
            if (HomeQuestsMath.Spoiled(m.tally)) Spoiled(m, map);
        }

        /// <summary>Ruined where it lay: it is there to be found now, spoiled, and no use to them.</summary>
        private void Spoiled(Mission m, Map map)
        {
            var t = HomeQuests.Make(HomeQuests.KeepsakeDef(m.variant));
            if (t != null)
            {
                if (t.def.useHitPoints) t.HitPoints = Mathf.Max(1, t.MaxHitPoints / 5);
                if (!GenPlace.TryPlaceThing(t, m.cell, map, ThingPlaceMode.Near)) t = null;
            }
            m.area = IntVec3.Invalid;
            looked.Remove(m.id);
            Missions.Goodwill(m, -m.penalty);
            Missions.Finish(m, MissionState.Failed, "spoiled", null, t != null ? new LookTargets(t) : LookTargets.Invalid);
        }

        /// <summary>The time is up: someone of theirs comes to search the area themselves.</summary>
        private void TheirSearch(Mission m, Map map, int now)
        {
            m.area = IntVec3.Invalid;
            looked.Remove(m.id);
            var s = HomeQuests.Person(m.faction, new FloatRange(25f, 60f));
            if (s == null || Arrivals.Visit(new List<Pawn> { s }, map, m.faction, m.cell, 8f) == null)
            {
                if (s != null && !s.Spawned) Arrivals.Discard(s);
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "toolate", null);
                return;
            }
            Hosting.Provision(s, 0.5f);
            m.group.Add(s);
            m.stage = 4;
            m.due = now + GenDate.TicksPerHour * 6;
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_HomeQ_Keepsake_TheirSearch".Translate(s.Named("X"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(s), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Their searcher at the spot (or out of time): they have it in their pack and go home.</summary>
        private void Searched(Mission m, Map map, int now)
        {
            var s = m.group.FirstOrDefault();
            bool there = s != null && s.Spawned && s.Map == map && s.Position.InHorDistOf(m.cell, HomeQuestsMath.AreaRadius);
            if (!there && now < m.due) return;
            if (s == null || s.Dead || !s.Spawned)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "toolate", null);
                return;
            }
            if (there)
            {
                var t = HomeQuests.Make(HomeQuests.KeepsakeDef(m.variant));
                if (t != null) s.inventory?.innerContainer.TryAdd(t);
            }
            Arrivals.SendAway(new[] { s });
            Missions.Goodwill(m, -m.penalty);
            Missions.Finish(m, MissionState.Failed, there ? "theyfound" : "toolate", null, new LookTargets(s));
        }

        private void Returned(Mission m, Map map, bool now)
        {
            if (now)
            {
                if (m.thing != null && !m.thing.Destroyed) m.thing.Destroy();
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "returned", HomeQuests.RewardWords(m, m.reward));
        }

        public override void Release(Mission m)
        {
            looked.Remove(m.id);
            foreach (var p in m.group.Where(p => p != null && !p.Dead))
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        public override LookTargets Targets(Mission m)
        {
            var map = m.Map;
            if (m.thing != null && m.thing.SpawnedOrAnyParentSpawned) return new LookTargets(m.thing.SpawnedParentOrMe);
            if (map != null && m.area.IsValid && m.area.InBounds(map)) return new LookTargets(new TargetInfo(m.area, map));
            var s = m.group.FirstOrDefault(p => p != null && p.Spawned);
            return s != null ? new LookTargets(s) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            string thing = HomeQuests.KeepsakeDef(m.variant)?.label ?? "?";
            switch (m.stage)
            {
                case 0:
                case 1:
                    return "CC_HomeQ_Keepsake_Status1".Translate((m.name ?? "?").Named("OWNER"), thing.Named("THING"), m.tally.Named("WET"),
                        HomeQuestsMath.WetHoursToSpoil.Named("MAX")).Resolve();
                case 2:
                case 3:
                    var t = m.thing;
                    string where = t == null ? "CC_HomeQ_Thing_Missing" : t.IsInAnyStorage() ? "CC_HomeQ_Thing_Stored" : t.Spawned ? "CC_HomeQ_Thing_Lying" : "CC_HomeQ_Thing_Carried";
                    return "CC_HomeQ_Keepsake_Status2".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve() + " " + where.Translate(thing.Named("THING")).Resolve();
                default:
                    return "CC_HomeQ_Keepsake_Status4".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            }
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "found", "returned", "spoiled", "their search" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            int now = Find.TickManager.TicksGame;
            switch (outcome)
            {
                case "found":
                    if (m.stage == 1) Found(m, map, m.a != null && m.a.Spawned ? m.a : map.mapPawns.FreeColonistsSpawned.FirstOrDefault());
                    return;
                case "returned":
                    Returned(m, map, true);
                    return;
                case "spoiled":
                    if (m.stage == 1) Spoiled(m, map);
                    return;
                default:
                    if (m.stage == 1) TheirSearch(m, map, now);
                    return;
            }
        }
    }

    // ===================== Q10. Tame one for them =====================

    /// <summary>
    /// A neighbour wants one particular wild animal on the colony's land (one already there, or one that wanders in): a
    /// fine beast for their herds, their ring, their hunters or their collection. The colony tames it (the game's own
    /// taming), and their handler comes for it and pays. Bonded to someone of the colony, they leave it where it is loved
    /// and pay nothing. Killed, or wandered off before it was tamed, it ends badly.
    /// </summary>
    public class HappeningWorker_MissionTameFor : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuestsMath.TameKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 2;

        private static Pawn Tamer(Map map) => HomeQuests.Officer(map, Interplay.Herdmaster, SkillDefOf.Animals) is Pawn p && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) ? p
            : VarietyUtil.Best(map, SkillDefOf.Animals);

        private static int Skill(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;

        /// <summary>A wild animal of the colony's land the colony's tamer could win over (the game's own rules), and nobody else's quest.</summary>
        private static bool Fits(Pawn a, int skill) =>
            a != null && a.Spawned && !a.Dead && !a.Downed && a.Faction == null && a.RaceProps.Animal && !a.InMentalState && TameUtility.CanTame(a)
            && TrainableUtility.MinimumHandlingSkill(a) <= skill && !Missions.Holds(a) && !a.IsQuestLodger() && a.ageTracker.CurLifeStage?.reproductive != false;

        private static Pawn OnLand(Map map, int skill) =>
            map.mapPawns.AllPawnsSpawned.Where(a => Fits(a, skill)).OrderByDescending(a => a.MarketValue).Take(3).RandomElementWithFallback();

        /// <summary>A kind of this land's wild animals (from every mod's that lives here) that could wander in and be tamed.</summary>
        private static PawnKindDef Comer(Map map, int skill)
        {
            if (map?.Biome == null) return null;
            return PokeWorldLink.WildKinds(BiomeAnimals.Of(map.Biome))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && k.race.GetStatValueAbstract(StatDefOf.Wildness) < 0.9f && k.RaceProps.animalType != AnimalType.Dryad
                            && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f) && (k.race.GetStatValueAbstract(StatDefOf.MinimumHandlingSkill) <= skill))
                .RandomElementByWeightWithFallback(k => Mathf.Max(0.1f, k.race.BaseMarketValue));
        }

        protected override bool Possible(Map map)
        {
            var t = Tamer(map);
            if (t == null || Neighbor(map) == null) return false;
            int s = Skill(t);
            return OnLand(map, s) != null || Comer(map, s) != null;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            var tamer = Tamer(map);
            if (m.faction == null || tamer == null) return false;
            int skill = Skill(tamer);
            var here = OnLand(map, skill);
            if (here != null)
            {
                m.x = here;
                m.variant = "here";
            }
            else
            {
                var kind = Comer(map, skill);
                if (kind == null) return false;
                Pawn a = null;
                try
                {
                    float age = kind.RaceProps.lifeExpectancy * Rand.Range(0.2f, 0.4f);
                    a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        fixedBiologicalAge: age, fixedChronologicalAge: age));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the animal made", e);
                }
                if (a == null) return false;
                if (TrainableUtility.MinimumHandlingSkill(a) > skill)
                {
                    Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.Discard);
                    return false;
                }
                Arrivals.Hold(a);
                m.x = a;
                m.variant = "comes";
            }
            m.name = m.x.KindLabel;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = tamer;
            m.days = HomeQuestsMath.Days(HomeQuest.TameFor, Rand.Value);
            m.reward = HomeQuests.Reward(HomeQuest.TameFor, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.TameFor);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.TameFor);
            Fill(m, ctx);
            ctx.thing = m.x.def;
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => Mathf.RoundToInt(m.days), m => m.reward);

        public override bool Start(Mission m, Map map)
        {
            var x = m.x;
            if (x == null || x.Dead || x.Discarded) return false;
            if (x.Spawned) return x.Map == map && x.Faction == null;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal, false, null)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, cell, map);
            if (!x.Spawned) return false;
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_HomeQ_Tame_Comes".Translate(x.KindLabel.Named("THING")).Resolve(), new LookTargets(x), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 3)
            {
                // Their handler has come for it: theirs now, or gone off with them.
                if (x.Faction == m.faction || !x.Spawned && !x.Dead && x.Faction != Faction.OfPlayer) Handed(m, map, false);
                else if (x.Dead) Killed(m);
                else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Handed(m, map, true);
                return;
            }
            bool ours = x.Faction == Faction.OfPlayer;
            var end = HomeQuestsMath.TameOutcome(x.Dead, ours, x.Spawned && x.Map == map || ours && !x.Dead, ours && HappeningWorker_MissionRunaway.Bonded(x));
            switch (end)
            {
                case TameEnd.Killed:
                    Killed(m);
                    return;
                case TameEnd.Gone:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "gone", null);
                    return;
                case TameEnd.Wild:
                    if (now >= m.deadline)
                    {
                        Missions.Goodwill(m, -m.penalty);
                        Missions.Finish(m, MissionState.Failed, "untamed", null, x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                    }
                    return;
            }
            // Tamed: their handler is told and comes within half a day.
            if (m.stage < 2)
            {
                m.stage = 2;
                m.due = now + Mathf.RoundToInt(HomeQuestsMath.HandlerHours * GenDate.TicksPerHour);
                string line = Line(More?.ready, m, map, null, null);
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (now < m.due || !x.Spawned) return;
            if (end == TameEnd.Bonded)
            {
                // Bonded to one of the colony's: their handler leaves it where it is loved, and takes no pay.
                Missions.Goodwill(m, m.goodwill / 2);
                Missions.Finish(m, MissionState.Done, "bonded", null, new LookTargets(x));
                return;
            }
            m.stage = 3;
            m.lastLook = now;
            if (!Couriers.Fetch(map, x, m.faction, HomeQuests.PayThings(m, m.reward), x.Position, false)) Handed(m, map, true);
        }

        private static void Killed(Mission m)
        {
            var x = m.x;
            Missions.Goodwill(m, -m.penalty * 2);
            Missions.Finish(m, MissionState.Failed, "killed", null, x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
        }

        /// <summary>Handed to them: their handler walks it home (the runaway's way when nobody comes for it), and the reward is the colony's.</summary>
        private void Handed(Mission m, Map map, bool now)
        {
            var x = m.x;
            if (now && x != null && !x.Dead && x.Spawned && x.Faction == Faction.OfPlayer)
            {
                try
                {
                    Lords.Leave(x);
                    x.roping?.BreakAllRopes();
                    if (x.playerSettings != null) x.playerSettings.Master = null;
                    x.SetFaction(m.faction);
                    if (!x.Downed && ReachabilityUtility.CanReachMapEdge(x))
                        LordMaker.MakeNewLord(m.faction, new LordJob_ExitMapBest(Pace.Errand, false, false), x.Map, new List<Pawn> { x });
                    else
                    {
                        x.DeSpawn();
                        if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the animal handed over", e);
                }
            }
            if (now) HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "handed", HomeQuests.RewardWords(m, m.reward), x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
        }

        public override void Release(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return;
            if (!x.Spawned && x.Faction == null && m.started < 0) Arrivals.Discard(x);
            else Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            string kind = x.KindLabel;
            if (x.Faction == Faction.OfPlayer) return "CC_HomeQ_Tame_Tamed".Translate(kind.Named("THING"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            return "CC_HomeQ_Tame_Wild".Translate(kind.Named("THING")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "tamed now", "handed", "bonded", "killed", "gone" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            var x = m.x;
            switch (outcome)
            {
                case "tamed now":
                    if (x != null && x.Spawned && x.Faction == null) x.SetFaction(Faction.OfPlayer);
                    return;
                case "handed":
                    Handed(m, map, true);
                    return;
                case "bonded":
                    Missions.Goodwill(m, m.goodwill / 2);
                    Missions.Finish(m, MissionState.Done, "bonded", null);
                    return;
                case "killed":
                    Killed(m);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "gone", null);
                    return;
            }
        }
    }
}
