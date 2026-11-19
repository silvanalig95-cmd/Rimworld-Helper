using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // S10, the town's well ran dry (docs/quests-proposals.md; the rules in Flows/WellFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Well.xml). The Deepwell Union's town (the drillers), else any people with a town near
    // the colony. Part 1: the well ran dry, and the town asks for water (Dubs Bad Hygiene's bottled water, where the colony
    // keeps it) or meals, collected by its carts and paid a little over their worth. Part 2: it asks the colony to lend a
    // builder or a miner (Construction or Mining 6 or more) to dig a new one: their foreman comes to the gate, and the colonist
    // walks off the map with them and is away four to six days (kept in the world as the game keeps lent colonists; no
    // caravan of the colony's goes anywhere), then walks home more skilled, with a wage. Whether the new well is found is the
    // colonist's skill's. Part 3, the well not found: two to four of the town's people come to the colony as refugees, and
    // the colony takes them in (they join) or turns them away. The well found, the town grows and remembers the help.

    /// <summary>The town's well beyond its parts: the town, the lent colonist away and home again, the endings.</summary>
    public class WellRules : StorylineRules
    {
        public override string Kind => WellFlow.Key;

        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Drillers && LivingEvents.TownOf(f, map) != null)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null);

        public static JobDef OutJob => DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_StorylineLentOut.JobName);

        /// <summary>The skill a colonist would be lent for: their better of Mining and Construction.</summary>
        public static SkillDef SkillOf(Pawn p)
        {
            int mining = p?.skills?.GetSkill(SkillDefOf.Mining)?.Level ?? 0, building = p?.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0;
            return mining >= building ? SkillDefOf.Mining : SkillDefOf.Construction;
        }

        public static int Level(Pawn p) => p?.skills?.GetSkill(SkillOf(p))?.Level ?? 0;

        /// <summary>
        /// The colonist the town could have: free, grown, able to dig or build, Mining or Construction 6 or more; never the
        /// colony's last two hands. The best first.
        /// </summary>
        public static Pawn Digger(Map map)
        {
            var all = IncidentWorker_Happening.Colonists(map);
            if (all.Count < 3) return null;
            return all.Where(p => StorylineRound3.Free(p) && Level(p) >= WellFlow.MinSkill && !p.WorkTypeIsDisabled(WorkTypeDefOf.Mining) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                                  && !SeasonAway.AwayNow(p)).OrderByDescending(Level).FirstOrDefault();
        }

        /// <summary>The colonist lent, away now (kept in the world).</summary>
        public static bool Away(Pawn p) => p != null && !p.Dead && !p.Spawned && p.Faction == Faction.OfPlayer && Find.WorldPawns.Contains(p);

        /// <summary>At the map's edge (JobDriver_StorylineLentOut): the lent colonist goes on with the town's foreman, and is kept in the world while away.</summary>
        public static void AtEdge(Pawn p)
        {
            var s = Storylines.Running.FirstOrDefault(x => x.kind == WellFlow.Key && x.group.Contains(p));
            if (s == null || p == null || !p.Spawned) return;
            try
            {
                if (p.Drafted) p.drafter.Drafted = false;
                p.DeSpawn();
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the lent hand sets out", e);
            }
        }

        /// <summary>Home again: walking in from the edge of the map, with what they learned. False when they couldn't be brought back now.</summary>
        public static bool Home(Pawn p, Map map, float days)
        {
            if (p == null || p.Dead || p.Discarded || map == null) return false;
            if (p.Spawned) return true;
            try
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var edge, map, CellFinder.EdgeRoadChance_Friendly)) edge = CellFinder.RandomEdgeCell(map);
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                GenSpawn.Spawn(p, edge, map);
                var skill = SkillOf(p);
                var other = skill == SkillDefOf.Mining ? SkillDefOf.Construction : SkillDefOf.Mining;
                p.skills?.Learn(skill, WellFlow.XpPerDay * Mathf.Max(0f, days), true);
                p.skills?.Learn(other, WellFlow.OtherXpPerDay * Mathf.Max(0f, days), true);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the lent hand home", e);
                return false;
            }
        }

        /// <summary>Its people at war with the colony or gone: a lent colonist away comes home at once.</summary>
        public override void Gone(Storyline s, Map map)
        {
            foreach (var p in s.group.Where(p => p != null && p.Faction == Faction.OfPlayer).ToList())
                if (Away(p))
                    Home(p, map, 0f);
        }

        /// <summary>Every hour: never a colonist left away with nothing to bring them home (a part that ended some other way).</summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            if (s.missionId >= 0 && Missions.ById(s.missionId)?.Open == true) return;
            foreach (var p in s.group.Where(p => p != null && p.Faction == Faction.OfPlayer).ToList())
            {
                if (Away(p)) Home(p, map, 0f);
                s.group.Remove(p);
            }
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            Hourly(s, map, Find.TickManager.TicksGame);
            switch (key)
            {
                case "grew":
                    StoryChains.Goodwill(s.giver, WellFlow.GrewGoodwill, GoodwillWhy.Of(GoodwillReasonMath.Helped));
                    FactionMemory.Note(s.giver, "rebuilt", Storylines.Label(s));
                    return true;
                case "turnedaway":
                    StoryChains.Goodwill(s.giver, WellFlow.TurnedAwayGoodwill, GoodwillWhy.Of(GoodwillReasonMath.TurnedAway));
                    FactionMemory.Note(s.giver, "refused", Storylines.Label(s));
                    return true;
                case "takenin":
                    FactionMemory.Note(s.giver, "refugees", Storylines.Label(s));
                    return true;
            }
            return true;
        }
    }

    // ===================== Part 1: water or food =====================

    /// <summary>
    /// The town's well ran dry. It asks for water (Dubs Bad Hygiene's bottled water, where the colony keeps it) or meals within
    /// four to six days, collected by its carts and paid a fifth over their worth; part of it (half or more), its plain worth;
    /// less, none.
    /// </summary>
    public class HappeningWorker_WellWater : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        public const string WaterDef = "DBH_WaterBottle";

        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.Rules(WellFlow.Key).Giver(map) != null;

        protected override string Silver(Mission m, Storyline s) => s?.amount.ToString();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(WellFlow.Key).Giver(map);
            if (s == null || f == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var x = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(30f, 60f));
            if (x == null) return false;
            Arrivals.Hold(x);
            int colonists = IncidentWorker_Happening.Colonists(map).Count;
            // Water where the colony keeps it (Dubs Bad Hygiene), else meals.
            bool water = DefDatabase<ThingDef>.GetNamedSilentFail(WaterDef) != null && Missions.Count(map, WaterDef) > 0;
            string what = water ? WaterDef : "meals";
            int n = water ? WellFlow.Water(colonists) : WellFlow.Meals(colonists);
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.count = n;
            s.amount = WellFlow.Pay(n, Missions.UnitValue(what));
            s.Set(water ? "water" : "food");
            if (Peoples.CallingOf(f) == PeoplesMath.Drillers) s.Set("union");
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.goals.Add(new MissionGoal { what = what, count = n });
            m.reward = s.amount;
            m.goodwill = 10;
            m.penalty = 6;
            m.days = Rand.Range(WellFlow.WaterMinDays, WellFlow.WaterMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.count = n;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            StorylineKit.Accepted(P, Story(m), m, map, null, Story(m)?.amount.ToString());
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.goals.Count == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var g = m.goals[0];
            g.have = Missions.Count(map, g.what);
            bool all = g.have >= g.count;
            StorylineKit.Remind(P, s, m, map, now, s.amount.ToString());
            if (m.stage == 0 && all)
            {
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    Note(m, map, "ready", LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, null, s.amount.ToString(), TextUtil.DaysString(m.due - now));
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due || now >= m.deadline && all)
            {
                StorylineRound3.Collect(map, s.giver, g.what, g.count, s.amount);
                Missions.Goodwill(m, m.goodwill);
                End(m, MissionState.Done, "sent", null, null, s.amount.ToString());
                return;
            }
            if (now < m.deadline) return;
            if (WellFlow.WaterOutcome(g.have, g.count) == "partly")
            {
                int pay = WellFlow.PartPay(g.have, Missions.UnitValue(g.what));
                StorylineRound3.Collect(map, s.giver, g.what, g.have, pay);
                Missions.Goodwill(m, m.goodwill / 2);
                End(m, MissionState.Done, "partly", null, Missions.Need(new MissionGoal { what = g.what, count = g.have }), pay.ToString());
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            End(m, MissionState.Failed, "none");
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Storyline_Pay_Coming".Translate((m.faction?.Name ?? "?").Named("FACTION"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() : null;

        public string DebugNow(Mission m, Map map)
        {
            m.deadline = Find.TickManager.TicksGame;
            return "the day is now: judged as the stores stand.";
        }
    }

    // ===================== Part 2: a hand lent to dig =====================

    /// <summary>
    /// The town asks the colony to lend a builder or a miner to dig a new well. Their foreman comes to the gate within a day,
    /// and the colony's best at Mining or Construction (6 or more) walks off the map with them; four to six days later they
    /// walk home, more skilled, with a day's wage for each day. Whether the well was found: their skill's chance.
    /// </summary>
    public class HappeningWorker_WellDigger : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && WellRules.Digger(map) != null && WellRules.OutJob != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            var p = WellRules.Digger(map);
            if (s?.giver == null || p == null) return false;
            var foreman = s.x != null && StoryChains.CanVisit(s.x) ? s.x : Arrivals.Make(Arrivals.PersonKind(s.giver), s.giver, new FloatRange(35f, 60f));
            if (foreman == null) return false;
            Arrivals.Hold(foreman);
            s.y = foreman;
            s.yName = foreman.LabelShort;
            s.name = p.LabelShort;
            m.a = p;
            m.faction = s.giver;
            m.place = s.place;
            m.x = foreman;
            m.wait = Rand.Range(WellFlow.AwayMinDays, WellFlow.AwayMaxDays);
            m.days = m.wait + 3f;
            m.reward = WellFlow.Wage(m.wait, WellRules.Level(p));
            m.goodwill = 10;
            Fill(m, ctx);
            ctx.stranger = foreman;
            ctx.count = Mathf.RoundToInt(m.wait);
            return true;
        }

        protected override string Silver(Mission m, Storyline s) => m?.reward.ToString();

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || m.x == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.8f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null, m.reward.ToString());
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var digger = m.a;
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay * 2) return;
                    // The colony's best hand now (the one named may be hurt or busy): free, and able.
                    if (!StorylineRound3.Free(digger) || digger.Map != map) digger = m.a = WellRules.Digger(map);
                    var gate = PeoplesD.GateSpot(map);
                    if (digger == null || m.x == null || Arrivals.Visit(new List<Pawn> { m.x }, map, s.giver, gate, 8f) == null)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    s.name = digger.LabelShort;
                    m.stage = 1;
                    m.lastLook = now;
                    Note(m, map, "came", new LookTargets(m.x, digger));
                    return;
                }
                case 1:
                {
                    // The foreman at the gate: the colonist walks out to them and the two leave together.
                    var foreman = m.x;
                    bool here = foreman != null && foreman.Spawned && foreman.Map == map;
                    if (digger == null || digger.Dead)
                    {
                        if (here) Arrivals.SendAway(new[] { foreman });
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    // Gone off the map with the foreman: away for their days.
                    if (WellRules.Away(digger))
                    {
                        m.stage = 2;
                        m.due = now + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
                        m.target = WellRules.Level(digger);
                        Tell.Message(NewsKind.World, Importance.Notable, map,
                            "CC_Story3_Well_Left".Translate(digger.Named("PAWN"), (s.place ?? "?").Named("PLACE"), TextUtil.DaysString(Mathf.RoundToInt(m.wait * GenDate.TicksPerDay)).Named("DAYS"))
                                .Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                        return;
                    }
                    // On the way out to the edge.
                    if (digger.Spawned && digger.CurJobDef == WellRules.OutJob) return;
                    // Not set out: the foreman waits at the gate for most of a day (the colony kept them busy, drafted or
                    // at other work), then goes home without them.
                    if (!here || now - m.lastLook > GenDate.TicksPerHour * 10)
                    {
                        if (!here && now - m.lastLook < GenDate.TicksPerDay / 2) return;
                        if (here) Arrivals.SendAway(new[] { foreman });
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    if (StorylineRound3.Free(digger) && foreman.Position.InHorDistOf(PeoplesD.GateSpot(map) ?? foreman.Position, 12f)) SetOut(m, s, digger, foreman, map);
                    return;
                }
                default:
                {
                    if (digger == null || digger.Discarded || digger.Destroyed)
                    {
                        End(m, MissionState.Failed, "lost");
                        return;
                    }
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.due + GenDate.TicksPerDay) return;
                    if (!WellRules.Home(digger, map, m.wait))
                    {
                        End(m, MissionState.Failed, "lost");
                        return;
                    }
                    s.group.Remove(digger);
                    foreach (var t in Couriers.Make(ThingDefOf.Silver, m.reward))
                        GenPlace.TryPlaceThing(t, digger.Position, map, ThingPlaceMode.Near);
                    FactionMemory.Note(s.giver, "lentHands", digger.LabelShort);
                    bool found = Rand.Chance(WellFlow.FoundChance(m.target));
                    Missions.Goodwill(m, found ? m.goodwill : m.goodwill / 2);
                    End(m, MissionState.Done, found ? "found" : "notfound", new LookTargets(digger), digger.LabelShort, m.reward.ToString());
                    return;
                }
            }
        }

        /// <summary>The lent colonist walks out with the foreman to the edge of the map (JobDriver_StorylineLentOut), the foreman going home.</summary>
        private static void SetOut(Mission m, Storyline s, Pawn digger, Pawn foreman, Map map)
        {
            var job = WellRules.OutJob;
            if (job == null || !RCellFinder.TryFindBestExitSpot(digger, out var edge, TraverseMode.ByPawn)) return;
            if (!s.group.Contains(digger)) s.group.Add(digger);
            Arrivals.SendAway(new[] { foreman });
            var walk = JobMaker.MakeJob(job, edge);
            walk.locomotionUrgency = Pace.Errand;
            digger.jobs.StartJob(walk, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Never a colonist left away by a part that ended: home at once.</summary>
        public override void Release(Mission m)
        {
            var s = Story(m);
            if (m.a != null && WellRules.Away(m.a)) WellRules.Home(m.a, m.Map, 0f);
            s?.group.Remove(m.a);
        }

        public override LookTargets Targets(Mission m)
        {
            var list = new List<Pawn>();
            if (m.x != null && m.x.Spawned) list.Add(m.x);
            if (m.a != null && m.a.Spawned) list.Add(m.a);
            return list.Count > 0 ? new LookTargets(list) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            switch (m.stage)
            {
                case 0: return "CC_Story3_Well_Coming".Translate((s.yName ?? "?").Named("RIVAL"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
                case 1: return "CC_Story3_Well_Leaving".Translate((m.a?.LabelShort ?? "?").Named("PAWN"), (s.yName ?? "?").Named("RIVAL")).Resolve();
                default:
                    return "CC_Story3_Well_Away".Translate((m.a?.LabelShort ?? "?").Named("PAWN"), (s.place ?? "?").Named("PLACE"),
                        TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            }
        }

        public string DebugNow(Mission m, Map map)
        {
            m.due = Find.TickManager.TicksGame;
            return m.stage == 0 ? "the foreman comes now." : m.stage == 2 ? "the lent hand comes home now." : "the two are on their way out.";
        }
    }

    // ===================== Part 3: refugees from the town =====================

    /// <summary>
    /// The new well came up dry, and two to four of the town's people are on the road to the colony as refugees. Taken in, they
    /// come to the gate within half a day and join the colony. Turned away, they go on to another town.
    /// </summary>
    public class HappeningWorker_WellRefugees : HappeningWorker_StorylinePart
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            int n = WellFlow.Refugees(Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(s.giver, new FloatRange(16f, 60f));
                if (p != null) m.group.Add(p);
            }
            if (m.group.Count == 0) return false;
            s.count = m.group.Count;
            m.faction = s.giver;
            m.place = s.place;
            m.x = m.group[0];
            m.days = 1.5f;
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.count = m.group.Count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || m.group.Count == 0) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.2f, 0.5f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (now < m.due) return;
            if (MapFacts.ThreatOn(map) && now < m.deadline) return;
            var gate = PeoplesD.GateSpot(map);
            var joined = new List<Pawn>();
            foreach (var p in m.group.Where(p => p != null && !p.Dead && !p.Discarded).ToList())
                if (Arrivals.Join(p, map, gate))
                    joined.Add(p);
            m.group.Clear();
            if (joined.Count == 0)
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            s.count = joined.Count;
            Say(joined[0], m, "joined");
            End(m, MissionState.Done, "takenin", new LookTargets(joined), joined.Count.ToString());
        }

        /// <summary>Turned away, or never come: they were only ever names.</summary>
        public override void Release(Mission m)
        {
            foreach (var p in m.group.Where(p => p != null && !p.Spawned && !p.Dead).ToList()) Arrivals.Discard(p);
            m.group.Clear();
        }

        public override string Status(Mission m) =>
            "CC_Story3_Well_Refugees".Translate(m.group.Count.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
    }

    /// <summary>The lent colonist walks to the edge of the map (target A) and sets out with the town's foreman (WellRules.AtEdge).</summary>
    public class JobDriver_StorylineLentOut : JobDriver
    {
        public const string JobName = "Rimfolk_Storyline_LentOut";

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("StorylineLentOut");
            go.initAction = () => WellRules.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
