using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // S5, the pilgrims' relic (docs/quests-proposals.md; the rules in Flows/RelicFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Relic.xml). Pilgrims of a faith (the pilgrim fellowship first, else any people at
    // peace) carry a relic (Rimfolk_PilgrimRelic, a small carved reliquary) and ask to rest at the colony two or three days.
    // Part 1: they stay as hosting guests, and set the relic down by where they sleep. On their second night it is taken,
    // by one of their own or by a stranger who came to share their fire that evening: the thief carries it off for the edge
    // of the map, an outlaw now (one of the region's raiding peoples), so the colony can stop them. Caught (downed or taken
    // prisoner), a card asks the colony to judge them: hand them over to the pilgrims, punish them here (the colony's
    // prisoner), or let them go. Killed, or the relic dropped, the relic is back too. Got away with it: part 2, the pilgrims
    // blame the colony. If a place for the thief's camp can be found within three tiles, the colony may go there (the game's
    // own bandit camp of the thief's people, the relic and the thief inside; the one away part of the storylines); else the
    // pilgrims go after the thief themselves and ask the colony for food for the road. With the relic back: part 3, they ask
    // to leave it at the colony's shrine for a year; pilgrims of theirs come to see it once a season, and then its keeper
    // takes it home.

    /// <summary>The pilgrims' relic beyond its parts: who asks, the relic, the thief, the camp, the relic at the shrine.</summary>
    public class RelicRules : StorylineRules
    {
        public override string Kind => RelicFlow.Key;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The pilgrim fellowship (any people of the pilgrims' calling); else any people at peace.</summary>
        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Pilgrims)
            ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        public static ThingDef RelicDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_PilgrimRelic");

        /// <summary>The people an outlaw thief runs to: a raiding people at war with the colony, nearest the pilgrims' age.</summary>
        public static Faction Outlaws(Faction like) =>
            HomeQuests.Enemy(like) ?? Find.FactionManager.RandomEnemyFaction(false, false, false);

        /// <summary>The relic is on this map (lying, carried or in a pack), or null.</summary>
        public static Thing RelicOn(Storyline s, Map map) => s?.thing != null && !s.thing.Destroyed && s.thing.MapHeld == map ? s.thing : null;

        /// <summary>The colony's own place for a god (Gods/Worship/Shrines.cs: an altar, a shrine, a ritual focus, a work of art) nearest its middle.</summary>
        public static Thing Shrine(Map map)
        {
            if (map == null) return null;
            var center = CampSite.ColonyCenter(map);
            return map.listerBuildings.allBuildingsColonist.Where(Shrines.Is).OrderBy(b => center.IsValid ? b.Position.DistanceToSquared(center) : 0).FirstOrDefault();
        }

        /// <summary>The relic out of whoever holds it (a pack, someone's hands), set down near a spot on the map.</summary>
        public static bool SetDown(Thing relic, Map map, IntVec3 near, bool forbid)
        {
            if (relic == null || relic.Destroyed || map == null || !near.IsValid) return false;
            try
            {
                if (relic.Spawned) relic.DeSpawn();
                relic.holdingOwner?.Remove(relic);
                if (!GenPlace.TryPlaceThing(relic, near, map, ThingPlaceMode.Near)) return false;
                relic.SetForbidden(forbid, false);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the relic set down", e);
                return false;
            }
        }

        /// <summary>The relic into someone's pack (the keeper who carries it).</summary>
        public static void Pack(Thing relic, Pawn p)
        {
            if (relic == null || relic.Destroyed || p?.inventory == null) return;
            try
            {
                if (relic.Spawned) relic.DeSpawn();
                relic.holdingOwner?.Remove(relic);
                p.inventory.innerContainer.TryAdd(relic);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the relic packed", e);
            }
        }

        // ---------------- The thief's camp (the one away part) ----------------

        /// <summary>The thief's camp's map made (the colony's caravan came to it): the relic set down in the middle of it, the thief there among their people.</summary>
        public static void MapMade(Site site)
        {
            var s = Storylines.Running.FirstOrDefault(x => x.kind == RelicFlow.Key && x.site >= 0 && x.site == site?.ID);
            var map = site?.Map;
            if (s == null || map == null) return;
            var hostiles = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == s.other && p.RaceProps.Humanlike).ToList();
            var middle = hostiles.Count > 0 ? hostiles[hostiles.Count / 2].Position : map.Center;
            var thief = s.y;
            if (thief != null && !thief.Dead && !thief.Spawned && thief.Faction == s.other) Arrivals.Spawn(thief, map, middle);
            if (s.thing != null && !s.thing.Destroyed && s.thing.MapHeld != map) SetDown(s.thing, map, middle, false);
        }

        // ---------------- The relic at the shrine ----------------

        /// <summary>
        /// Every hour of the year the relic stays at the colony's shrine (a StorylineAfter "relic"): once a season two or
        /// three of its pilgrims come to see it and leave an offering; after the last, its keeper comes and carries it home.
        /// The relic gone from the colony meanwhile: the pilgrims who come find it gone, and that is the end of it.
        /// </summary>
        public override bool After(StorylineAfter a, int now)
        {
            if (a.kind != "relic") return false;
            if (now < a.next || a.left <= 0) return true;
            var map = HappeningTracker.HomeMap();
            var f = a.faction;
            if (map == null || f == null || f.defeated || f.HostileTo(Faction.OfPlayer))
            {
                a.left = 0;
                a.until = now - 1;
                return true;
            }
            if (MapFacts.ThreatOn(map))
            {
                a.next = now + GenDate.TicksPerHour * 6;
                return true;
            }
            var relic = a.thing != null && !a.thing.Destroyed && a.thing.MapHeld == map ? a.thing : null;
            if (relic == null)
            {
                // Gone from the colony: their pilgrims find it gone, and the colony hears of it.
                Tell.Message(NewsKind.World, Importance.Important, map, "CC_Storyline_Relic_Missing".Translate(f.Name.Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NegativeEvent);
                StoryChains.Goodwill(f, -15, GoodwillWhy.Of(GoodwillReasonMath.Relic));
                a.left = 0;
                a.until = now - 1;
                return true;
            }
            var at = relic.PositionHeld;
            if (a.left > 1)
            {
                // Pilgrims come to see it, and leave an offering at the shrine.
                var kind = Arrivals.PersonKind(f);
                var party = new List<Pawn>();
                for (int i = 0; kind != null && i < RelicFlow.VisitCount(Rand.Value); i++)
                {
                    var p = Arrivals.Make(kind, f, new FloatRange(18f, 70f));
                    if (p != null) party.Add(p);
                }
                if (party.Count > 0 && Arrivals.Visit(party, map, f, at, 6f) != null)
                {
                    int silver = StoryChains.Silver(map, RelicFlow.OfferingMin, RelicFlow.OfferingMax);
                    Couriers.PaySilver(map, at, silver, f, party[0]);
                    Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Storyline_Relic_Visit".Translate(party.Count.Named("COUNT"), f.Name.Named("FACTION"), silver.Named("SILVER")).Resolve(),
                        new LookTargets(party), MessageTypeDefOf.PositiveEvent);
                }
                a.left--;
                a.next = now + Mathf.RoundToInt(RelicFlow.VisitEvery * GenDate.TicksPerDay);
                return true;
            }
            // The year is out: its keeper comes and carries it home.
            var keeper = a.pawn != null && StoryChains.CanVisit(a.pawn) ? a.pawn : Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(30f, 65f));
            bool came = keeper != null && Arrivals.Visit(new List<Pawn> { keeper }, map, f, at, 8f) != null;
            if (came && relic.Spawned && RCellFinder.TryFindBestExitSpot(keeper, out var exit, TraverseMode.ByPawn))
            {
                relic.SetForbidden(false, false);
                var job = JobMaker.MakeJob(JobDefOf.Steal, relic, exit);
                job.count = 1;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            else if (relic.Spawned) relic.Destroy(DestroyMode.Vanish);
            StoryChains.Goodwill(f, 10, GoodwillWhy.Of(GoodwillReasonMath.Relic));
            FactionMemory.Note(f, "keptWord", "CC_Storyline_Relic_Deed".Translate().Resolve());
            Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_Storyline_Relic_HomeTitle".Translate().Resolve(),
                "CC_Storyline_Relic_Home".Translate((came ? keeper.LabelShort : a.who ?? f.Name).Named("WHO"), f.Name.Named("FACTION")).Resolve(), LetterDefOf.PositiveEvent,
                came ? new LookTargets(keeper) : LookTargets.Invalid);
            a.left = 0;
            a.until = now + GenDate.TicksPerDay;
            var kept = a.pawn;
            a.pawn = null;
            Storylines.Release(kept);
            return true;
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var f = s.giver;
            switch (key)
            {
                case "shrine":
                {
                    // A year at the colony's shrine, with pilgrims coming to see it once a season.
                    var relic = RelicOn(s, map);
                    if (relic == null) return true;
                    Storylines.AddAfter(new StorylineAfter
                    {
                        kind = "relic", faction = f, pawn = s.x != null && !s.x.Dead && s.x.Faction == f ? s.x : null, who = s.xName, thing = relic, left = RelicFlow.Visits + 1,
                        next = Now + Mathf.RoundToInt(RelicFlow.VisitEvery * GenDate.TicksPerDay), until = Now + Mathf.RoundToInt((RelicFlow.ShrineDays + 10f) * GenDate.TicksPerDay)
                    });
                    StoryChains.Goodwill(f, 8, GoodwillWhy.Of(GoodwillReasonMath.Relic));
                    return true;
                }
                case "home":
                    StoryChains.Goodwill(f, 4, GoodwillWhy.Of(GoodwillReasonMath.Relic));
                    return true;
                case "lost":
                    FactionMemory.Note(f, "letDown", Storylines.Label(s));
                    return true;
                case "harmed":
                    FactionMemory.Note(f, "prisonerDied", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        /// <summary>Their people at war with the colony now: the pilgrims still here go on; the relic, if here, stays with the colony.</summary>
        public override void Gone(Storyline s, Map map)
        {
            var here = new[] { s.x }.Concat(s.group).Where(p => p != null && p.Spawned && p.Map == map && p.Faction == s.giver).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
            if (RelicOn(s, map) != null) s.Set("ours");
        }

        public override void DebugEnded(Storyline s, string part, string outcome)
        {
            if (outcome == "escaped") s.Set("escaped");
        }
    }

    // ===================== Part 1: the pilgrims rest at the colony =====================

    /// <summary>
    /// The pilgrims (their keeper with the relic and two or three more) rest two or three days, as hosting guests, and the
    /// keeper sets the relic down where they sleep. On their second night it is taken and the thief runs for the edge of the
    /// map with it. Caught: the judgement (a card). Killed, or the relic left behind: back. Off the map with it: escaped.
    /// </summary>
    public class HappeningWorker_RelicRest : HappeningWorker_StorylineHost
    {
        public const string Card = "Rimfolk_Storyline_RelicJudge";

        protected override bool Possible(Map map) => Storylines.Rules(RelicFlow.Key).Giver(map) != null && RelicRules.RelicDef != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(RelicFlow.Key).Giver(map);
            if (s == null || f == null || RelicRules.RelicDef == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var keeper = Make(f, new FloatRange(35f, 70f));
            if (keeper == null) return false;
            int n = RelicFlow.Pilgrims(IncidentWorker_Happening.Colonists(map).Count, Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var p = Make(f, new FloatRange(16f, 65f));
                if (p != null) m.group.Add(p);
            }
            var relic = ThingMaker.MakeThing(RelicRules.RelicDef);
            RelicRules.Pack(relic, keeper);
            // The thief: one of their own, or a stranger who comes to share their fire (made now, kept until then).
            bool own = m.group.Count > 0 && Rand.Chance(RelicFlow.OwnThiefChance);
            Pawn thief;
            if (own) thief = m.group.RandomElement();
            else
            {
                // Of a people at peace with the pilgrims' too, so the two don't come to blows at their fire.
                var other = Arrivals.Friendly(x => x != f && VarietyUtil.OnGoodTerms(x) && !x.HostileTo(f)) ?? f;
                thief = Make(other, new FloatRange(18f, 45f));
                if (thief == null)
                {
                    own = m.group.Count > 0;
                    thief = own ? m.group.RandomElement() : null;
                }
            }
            s.giver = f;
            s.x = keeper;
            s.xName = keeper.LabelShort;
            s.group.AddRange(m.group);
            s.thing = relic;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.tale = relic.LabelNoCount;
            if (thief != null)
            {
                s.y = thief;
                s.yName = thief.LabelShort;
            }
            s.Set(own ? "own" : "stranger");
            if (Peoples.CallingOf(f) == PeoplesMath.Pilgrims) s.Set("fellowship");
            m.faction = f;
            m.place = s.place;
            m.x = keeper;
            m.target = m.group.Count + 1;
            s.count = m.target;
            m.wait = Rand.Range(0.3f, 0.6f);
            float stay = Rand.Range(RelicFlow.RestMinDays, RelicFlow.RestMaxDays);
            m.days = m.wait + stay;
            m.goals.Add(new MissionGoal { what = "meals", count = Mathf.CeilToInt(2.5f * m.target * stay) });
            m.goodwill = 10;
            m.penalty = 8;
            Fill(m, ctx);
            ctx.stranger = keeper;
            ctx.count = m.target;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        private static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        /// <summary>The pilgrims as a day's message names them ("The pilgrims from Larkwell").</summary>
        private static string Who(Storyline s) => "CC_Storyline_Relic_Pilgrims".Translate((s.place ?? s.giver?.Name ?? "?").Named("PLACE")).Resolve();

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "harmed");
                return;
            }
            if (s.thing == null || s.thing.Destroyed)
            {
                // The relic itself destroyed in the colony's care.
                Missions.Goodwill(m, -m.penalty * 2);
                SeeOff(m, map);
                End(m, MissionState.Failed, "harmed");
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    Remind(m, map, now);
                    if (now < m.due) return;
                    var spot = Hosting.Spot(Beds(m, map), map);
                    float hours = (m.deadline - now) / (float)GenDate.TicksPerHour + 30f;
                    if (!Arrive(m, map, spot.IsValid ? spot : PeoplesD.GateSpot(map), hours))
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    m.stage = 1;
                    m.lastLook = now;
                    m.lacks = 0;
                    m.cell = spot.IsValid ? spot : m.x.Position;
                    // m.due stays the hour they came: their nights are counted from it.
                    m.due = now;
                    Note(m, map, "came", Targets(m));
                    return;
                }
                case 1:
                {
                    // Their stay; the relic set down where they sleep once the keeper is there.
                    Stay(m, map, now, Who(s));
                    var keeper = m.x;
                    if (keeper != null && keeper.Spawned && !keeper.Dead && s.thing.ParentHolder is Pawn_InventoryTracker inv && inv.pawn == keeper
                        && (keeper.Position.InHorDistOf(m.cell, 8f) || now - m.due > GenDate.TicksPerHour * 4))
                        RelicRules.SetDown(s.thing, map, keeper.Position, true);
                    int arrived = m.due;
                    int hour = Hour(map);
                    var thief = s.y;
                    // The stranger comes to share their fire on the evening before the theft (the night's small hours are at most six hours on).
                    if (s.Has("stranger") && !s.Has("strangercame") && thief != null && !thief.Spawned && !thief.Dead && hour >= RelicFlow.StrangerHour
                        && now - arrived + GenDate.TicksPerHour * 6 >= GenDate.TicksPerDay)
                    {
                        s.Set("strangercame");
                        if (Arrivals.Visit(new List<Pawn> { thief }, map, thief.Faction, m.x?.Position ?? m.cell, 14f) != null)
                        {
                            Hosting.Provision(thief, 1f);
                            Note(m, map, "stranger", new LookTargets(thief));
                        }
                    }
                    // The theft: their second night, once everyone is asleep.
                    if (!s.Has("stolen") && now - arrived >= GenDate.TicksPerDay && hour >= RelicFlow.TheftHour && hour <= 4 && s.thing.Spawned)
                    {
                        Steal(m, s, map, now);
                        return;
                    }
                    if (now < m.deadline) return;
                    // Gone without a theft (the thief never came near it): the keeper takes it home.
                    RelicRules.Pack(s.thing, keeper ?? m.x);
                    SeeOff(m, map);
                    End(m, MissionState.Done, StorylineMath.Ended);
                    return;
                }
                case 2:
                    Chase(m, s, map, now);
                    return;
                case 3:
                    // The judgement: answered (Answered moves on), or lost without an answer: handed over.
                    Stay(m, map, now, Who(s));
                    if (!StorylineKit.CardOut(s) && now - s.baseA > GenDate.TicksPerHour * 2)
                    {
                        if (now - s.baseA > GenDate.TicksPerDay / 2 || !Ask(m, s, map, now))
                            Answered(m, s, StorylineKit.Context(s, m, map), 0, true);
                    }
                    return;
                default:
                {
                    // Back: the keeper takes the relic again, and they rest out their stay.
                    var keeper = m.x;
                    // A thief let go walks off once they are up again.
                    var gone = s.y;
                    if (m.variant == "letgo" && gone != null && gone.Spawned && !gone.Dead && !gone.Downed && !gone.IsPrisoner && gone.GetLord() == null && gone.Faction != null)
                        LordMaker.MakeNewLord(gone.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), map, new List<Pawn> { gone });
                    if (keeper != null && keeper.Spawned && s.thing.Spawned && keeper.CurJobDef != JobDefOf.TakeInventory && keeper.Position.InHorDistOf(s.thing.Position, 30f))
                    {
                        var job = JobMaker.MakeJob(JobDefOf.TakeInventory, s.thing);
                        job.count = 1;
                        keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                    if (Stay(m, map, now, Who(s))) return;
                    RelicRules.Pack(s.thing, keeper != null && keeper.Spawned ? keeper : m.x);
                    var thief = s.y;
                    // A thief handed to them still unable to walk is taken away with them.
                    if (m.variant == "handed" && thief != null && thief.Spawned && thief.Downed)
                    {
                        thief.DeSpawn();
                        if (!Find.WorldPawns.Contains(thief)) Find.WorldPawns.PassToWorld(thief, PawnDiscardDecideMode.Decide);
                    }
                    // Killed with it, or gone without it: thanked for the relic kept (a judgement was thanked when given).
                    if (!s.Has("judged")) Missions.Goodwill(m, RelicFlow.JudgeGoodwill(m.variant ?? "dropped", s.Has("own")));
                    SeeOff(m, map);
                    End(m, MissionState.Done, m.variant ?? "dropped", Targets(m));
                    return;
                }
            }
        }

        /// <summary>Dev mode: the theft now, whatever the hour (the stranger brought in first if they are the thief).</summary>
        public bool DebugSteal(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s?.y == null || s.thing == null || !s.thing.Spawned || map == null || s.Has("stolen")) return false;
            if (!s.y.Spawned && Arrivals.Visit(new List<Pawn> { s.y }, map, s.y.Faction, m.x?.Position ?? m.cell, 14f) == null) return false;
            s.Set("strangercame");
            Steal(m, s, map, Find.TickManager.TicksGame);
            return s.Has("stolen");
        }

        /// <summary>The theft: the thief, an outlaw now, takes the relic and runs for the nearest edge of the map with it.</summary>
        private void Steal(Mission m, Storyline s, Map map, int now)
        {
            var thief = s.y;
            var outlaws = RelicRules.Outlaws(s.giver);
            s.Set("stolen");
            if (thief == null || thief.Dead || !thief.Spawned || thief.Map != map || thief.Downed || outlaws == null) return;
            try
            {
                Hosting.GetUp(thief);
                Hosting.GiveBack(thief, map);
                m.group.Remove(thief);
                s.group.Remove(thief);
                Lords.Leave(thief);
                if (thief.guest != null && thief.HostFaction != null) thief.guest.SetGuestStatus(null);
                thief.SetFaction(outlaws);
                s.other = outlaws;
                LordMaker.MakeNewLord(outlaws, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), map, new List<Pawn> { thief });
                if (RCellFinder.TryFindBestExitSpot(thief, out var exit, TraverseMode.ByPawn))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Steal, s.thing, exit);
                    job.count = 1;
                    job.locomotionUrgency = LocomotionUrgency.Jog;
                    thief.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the relic stolen", e);
            }
            m.stage = 2;
            s.baseB = now;
            StorylineKit.NoteLetter(P, s, m, map, "stolen", new LookTargets(thief), LetterDefOf.ThreatSmall);
            Say(m.x, m, "stolen");
        }

        /// <summary>The chase: caught (down or a prisoner), killed, the relic left behind, or off the map with it.</summary>
        private void Chase(Mission m, Storyline s, Map map, int now)
        {
            Stay(m, map, now, Who(s));
            var thief = s.y;
            var relic = s.thing;
            bool relicHere = RelicRules.RelicOn(s, map) != null;
            if (thief == null || thief.Dead)
            {
                if (!relicHere) Escaped(m, s, map);
                else Back(m, s, map, now, "killed");
                return;
            }
            if (thief.Spawned && (thief.Downed || thief.IsPrisonerOfColony))
            {
                // Caught: the relic out of their hands, and the pilgrims ask the colony to judge them.
                if (relic.ParentHolder is Pawn_CarryTracker || relic.ParentHolder is Pawn_InventoryTracker inv && inv.pawn == thief)
                    RelicRules.SetDown(relic, map, thief.Position, true);
                m.stage = 3;
                Note(m, map, "caught", new LookTargets(thief), MessageTypeDefOf.PositiveEvent);
                Ask(m, s, map, now);
                return;
            }
            if (!thief.Spawned)
            {
                if (relicHere) Back(m, s, map, now, "dropped");
                else Escaped(m, s, map);
                return;
            }
            // Still running with it after a day: they slip away.
            if (now - s.baseB > GenDate.TicksPerDay)
            {
                if (relic.ParentHolder is Pawn_CarryTracker || relic.ParentHolder is Pawn_InventoryTracker) RelicRules.Pack(relic, thief);
                thief.DeSpawn();
                if (!Find.WorldPawns.Contains(thief)) Find.WorldPawns.PassToWorld(thief, PawnDiscardDecideMode.KeepForever);
                if (RelicRules.RelicOn(s, map) == null) Escaped(m, s, map);
                else Back(m, s, map, now, "dropped");
            }
        }

        private void Back(Mission m, Storyline s, Map map, int now, string how)
        {
            m.variant = how;
            m.stage = 4;
            if (s.thing.Spawned) s.thing.SetForbidden(true, false);
            Note(m, map, "back", s.thing.SpawnedOrAnyParentSpawned ? new LookTargets(s.thing.SpawnedParentOrMe) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Off the map with the relic: the pilgrims blame the colony and go on.</summary>
        private void Escaped(Mission m, Storyline s, Map map)
        {
            Missions.Goodwill(m, -m.penalty);
            Say(m.x, m, "escaped");
            SeeOff(m, map);
            End(m, MissionState.Failed, "escaped", Targets(m));
        }

        /// <summary>The card: the thief judged. False: nobody of the colony to answer now.</summary>
        private bool Ask(Mission m, Storyline s, Map map, int now)
        {
            bool asked = StorylineKit.Ask(Card, s, m, map, c =>
            {
                c.stranger = s.y;
                c.faction = s.giver;
            });
            s.baseA = now;
            if (asked) Say(m.x, m, "judge");
            return asked;
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            var thief = s.y;
            string how = option == 1 ? "punished" : option == 2 ? "letgo" : "handed";
            int now = Find.TickManager.TicksGame;
            try
            {
                if (thief != null && !thief.Dead && thief.Spawned)
                {
                    switch (how)
                    {
                        case "punished":
                            // The colony's prisoner, its wardens taking them to a cell as they do any prisoner.
                            if (!thief.IsPrisonerOfColony)
                            {
                                Lords.Leave(thief);
                                thief.guest?.CapturedBy(Faction.OfPlayer);
                            }
                            break;
                        case "handed":
                            // To the pilgrims: one of theirs again, and they take the thief with them when they go.
                            if (thief.IsPrisoner) thief.guest?.SetGuestStatus(null);
                            Lords.Leave(thief);
                            if (thief.Faction != s.giver) thief.SetFaction(s.giver);
                            if (!m.group.Contains(thief)) m.group.Add(thief);
                            break;
                        default:
                            // Let go: free to walk off once they can.
                            if (thief.IsPrisoner) thief.guest?.SetGuestStatus(null);
                            if (thief.GetLord() == null && !thief.Downed && thief.Faction != null)
                                LordMaker.MakeNewLord(thief.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), map, new List<Pawn> { thief });
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the thief judged", e);
            }
            Missions.Goodwill(m, RelicFlow.JudgeGoodwill(how, s.Has("own")));
            s.Set("judged");
            Back(m, s, map, now, how);
            Say(m.x, m, how);
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            bool own = s.Has("own");
            switch (choice)
            {
                case "hand":
                    sheet.Goodwill(s.giver, RelicFlow.JudgeGoodwill("handed", own));
                    sheet.Play("CC_Storyline_Impact_HandThief".Translate((s.yName ?? "?").Named("RIVAL")).Resolve(), "CC_Storyline_Impact_HandThiefBrief".Translate().Resolve());
                    break;
                case "punish":
                    sheet.Goodwill(s.giver, RelicFlow.JudgeGoodwill("punished", own));
                    sheet.Play("CC_Storyline_Impact_PunishThief".Translate((s.yName ?? "?").Named("RIVAL")).Resolve(), "CC_Storyline_Impact_PunishThiefBrief".Translate().Resolve());
                    break;
                case "letgo":
                    sheet.Goodwill(s.giver, RelicFlow.JudgeGoodwill("letgo", own));
                    sheet.Play("CC_Storyline_Impact_LetThiefGo".Translate((s.yName ?? "?").Named("RIVAL")).Resolve(), "CC_Storyline_Impact_LetThiefGoBrief".Translate().Resolve());
                    break;
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var list = Guests(m).Where(p => p.Spawned).Cast<Thing>().ToList();
            if (s?.y != null && s.y.Spawned && !list.Contains(s.y)) list.Add(s.y);
            if (s?.thing != null && s.thing.Spawned) list.Add(s.thing);
            return list.Count > 0 ? new LookTargets(list) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            int beds = Hosting.Slots(Beds(m, map));
            string roof = "CC_Mission_Refugees_Beds".Translate(beds, m.target).Resolve();
            switch (m.stage)
            {
                case 0: return "CC_Storyline_Relic_Coming".Translate(m.target.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() + " " + roof;
                case 2: return "CC_Storyline_Relic_Chase".Translate((s.yName ?? "?").Named("RIVAL")).Resolve();
                case 3: return "CC_Storyline_Relic_Judging".Translate((s.yName ?? "?").Named("RIVAL")).Resolve();
                default: return "CC_Mission_Refugees_Staying".Translate(m.tally, m.misses).Resolve() + " " + roof + " " + MealsText(m);
            }
        }
    }

    // ===================== Part 2: after the thief =====================

    /// <summary>
    /// The thief got away with the relic, and the pilgrims blame the colony. Where a place for the thief's camp can be found
    /// within three tiles of the colony, it is there (the game's own bandit camp of the thief's people, the thief and the
    /// relic inside) for eight days, and the colony may send people to bring the relic back home (the one away part of the
    /// storylines). Where there is none, the pilgrims go after the thief themselves and ask the colony for food for the road;
    /// they find the thief more often with it, and send word some days later.
    /// </summary>
    public class HappeningWorker_RelicTrail : HappeningWorker_StorylinePart
    {
        protected override int Rating => 3;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && Storylines.current.thing != null;

        private static bool CampTile(Map map, out PlanetTile tile) =>
            TileFinder.TryFindNewSiteTile(out tile, map.Tile, RelicFlow.CampMinTiles, RelicFlow.CampMaxTiles, false, null, 0f);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Shooting) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            bool camp = s.other != null && !s.other.defeated && SitePartDefOf.BanditCamp != null && CampTile(map, out _);
            s.flags.Remove("camp");
            if (camp) s.Set("camp");
            m.faction = s.giver;
            m.place = s.place;
            m.x = s.x;
            m.variant = camp ? "camp" : "alone";
            m.days = camp ? RelicFlow.CampDays : RelicFlow.SearchMaxDays + 2f;
            if (!camp) m.goals.Add(new MissionGoal { what = "meals", count = RelicFlow.SearchMeals(s.group.Count + 1) });
            m.goodwill = 8;
            Fill(m, ctx);
            ctx.stranger = s.x;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            int now = Find.TickManager.TicksGame;
            if (m.variant == "camp")
            {
                Site site = null;
                try
                {
                    if (CampTile(map, out var tile))
                    {
                        site = SiteMaker.MakeSite(SitePartDefOf.BanditCamp, tile, s.other, true, StorytellerUtility.DefaultThreatPointsNow(map) * 0.6f);
                        if (site != null)
                        {
                            site.customLabel = "CC_Storyline_Relic_Camp".Translate((s.yName ?? "?").Named("RIVAL")).Resolve();
                            site.GetComponent<TimeoutComp>()?.StartTimeout(Mathf.RoundToInt(RelicFlow.CampDays * GenDate.TicksPerDay));
                            Find.WorldObjects.Add(site);
                        }
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: the thief's camp", e);
                    site = null;
                }
                if (site == null)
                {
                    // No camp to be had after all: the pilgrims go themselves, unhelped.
                    m.variant = "alone";
                    s.flags.Remove("camp");
                }
                else
                {
                    s.site = site.ID;
                    m.stage = 0;
                    StorylineKit.Accepted(P, s, m, map, new LookTargets(site));
                    return true;
                }
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            m.stage = 0;
            m.due = -1;
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
            if (m.variant == "camp")
            {
                // Back home: the relic on the colony's map, brought by its caravan.
                if (RelicRules.RelicOn(s, map) != null)
                {
                    s.site = -1;
                    Missions.Goodwill(m, m.goodwill);
                    End(m, MissionState.Done, "recovered", new LookTargets(s.thing.SpawnedParentOrMe ?? s.thing));
                    return;
                }
                var site = Find.WorldObjects.Sites.FirstOrDefault(x => x.ID == s.site);
                // On its way home in the packs of the colony's caravan: a few days more for the road.
                bool homeward = !s.thing.Destroyed && s.thing.ParentHolder is Pawn_InventoryTracker inv && inv.pawn.Faction == Faction.OfPlayer && inv.pawn.GetCaravan() != null;
                if (homeward && now < m.deadline + GenDate.TicksPerDay * 3) return;
                // The camp gone (moved on, or left behind) without it, the relic destroyed, or the time out.
                if (s.thing.Destroyed || now >= m.deadline || site == null && !homeward && s.thing.MapHeld == null)
                {
                    s.site = -1;
                    End(m, MissionState.Failed, "notfound");
                }
                return;
            }
            // The pilgrims' own search: food for the road first, then word some days later.
            if (m.stage == 0)
            {
                foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
                bool all = m.goals.All(g => g.have >= g.count);
                if (all || now - m.started > GenDate.TicksPerDay * 3 / 2)
                {
                    if (all)
                    {
                        if (!Couriers.CollectGoals(map, m.goals.Select(g => new KeyValuePair<string, int>(g.what, g.count)).ToList(), s.giver, 0))
                            foreach (var g in m.goals)
                                Missions.Take(map, g.what, g.count);
                        s.Set("supplied");
                    }
                    m.stage = 1;
                    m.due = now + Mathf.RoundToInt(Rand.Range(RelicFlow.SearchMinDays, RelicFlow.SearchMaxDays) * GenDate.TicksPerDay);
                    m.deadline = Mathf.Max(m.deadline, m.due + GenDate.TicksPerDay);
                    Note(m, map, all ? "setout" : "setoutshort", LookTargets.Invalid);
                }
                return;
            }
            if (now < m.due) return;
            if (Rand.Chance(RelicFlow.FindChance(s.Has("supplied"))) && s.x != null && !s.x.Dead)
            {
                // Found: the relic back in its keeper's pack, out in the world.
                RelicRules.Pack(s.thing, s.x);
                Missions.Goodwill(m, m.goodwill / 2);
                End(m, MissionState.Done, "recovered");
                return;
            }
            End(m, MissionState.Failed, "notfound");
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var site = s == null ? null : Find.WorldObjects.Sites.FirstOrDefault(x => x.ID == s.site);
            return site != null ? new LookTargets(site) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            if (m.variant == "camp") return "CC_Storyline_Relic_CampStatus".Translate((s.yName ?? "?").Named("RIVAL")).Resolve();
            return m.stage == 0 ? null : "CC_Storyline_Relic_Searching".Translate().Resolve();
        }
    }

    // ===================== Part 3: the relic at the colony's shrine =====================

    /// <summary>
    /// The relic is back, and the pilgrims ask to leave it at the colony's shrine for a year (Gods/Worship/Shrines.cs: an
    /// altar, a shrine, a ritual focus or a work of art the colony keeps). Taken on, its keeper comes within a day and sets it
    /// down beside the shrine; with no shrine on the colony's land within three days, they take it home.
    /// </summary>
    public class HappeningWorker_RelicShrine : HappeningWorker_StorylinePart
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && Storylines.current.thing != null && !Storylines.current.thing.Destroyed;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            s.flags.Remove("noshrine");
            if (RelicRules.Shrine(map) == null) s.Set("noshrine");
            m.faction = s.giver;
            m.place = s.place;
            m.x = s.x;
            m.days = 3f;
            m.goodwill = 6;
            Fill(m, ctx);
            ctx.stranger = s.x;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.4f, 0.9f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || s.thing == null || s.thing.Destroyed)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var shrine = RelicRules.Shrine(map);
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    if (shrine == null)
                    {
                        if (now >= m.deadline) End(m, MissionState.Failed, "noshrine");
                        return;
                    }
                    if (MapFacts.ThreatOn(map) && now < m.deadline) return;
                    // The keeper comes with the relic in their pack (it may be lying here still, from the night of the theft).
                    var keeper = s.x != null && StoryChains.CanVisit(s.x) ? s.x : Arrivals.Make(Arrivals.PersonKind(s.giver), s.giver, new FloatRange(30f, 65f));
                    if (keeper == null || Arrivals.Visit(new List<Pawn> { keeper }, map, s.giver, shrine.Position, 8f) == null)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    if (RelicRules.RelicOn(s, map) == null) RelicRules.Pack(s.thing, keeper);
                    m.x = keeper;
                    m.stage = 1;
                    m.lastLook = now;
                    Note(m, map, "came", new LookTargets(keeper));
                    return;
                }
                default:
                {
                    var keeper = m.x;
                    bool there = keeper != null && keeper.Spawned && shrine != null && keeper.Position.InHorDistOf(shrine.Position, 6f);
                    if (!there && now - m.lastLook < GenDate.TicksPerHour * 4) return;
                    if (shrine == null)
                    {
                        End(m, MissionState.Failed, "noshrine");
                        return;
                    }
                    var at = shrine.InteractionCell.IsValid && shrine.def.hasInteractionCell ? shrine.InteractionCell : shrine.Position;
                    if (!RelicRules.SetDown(s.thing, map, at, true))
                    {
                        End(m, MissionState.Failed, "noshrine");
                        return;
                    }
                    Say(keeper, m, "laid");
                    Missions.Goodwill(m, m.goodwill);
                    End(m, MissionState.Done, "kept", new LookTargets(s.thing), shrine.LabelShortCap);
                    return;
                }
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var shrine = RelicRules.Shrine(m.Map);
            var list = new List<Thing>();
            if (m.x != null && m.x.Spawned) list.Add(m.x);
            if (shrine != null) list.Add(shrine);
            if (s?.thing != null && s.thing.Spawned) list.Add(s.thing);
            return list.Count > 0 ? new LookTargets(list) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var shrine = RelicRules.Shrine(m.Map);
            return shrine != null
                ? "CC_Storyline_Relic_ShrineAt".Translate(shrine.LabelShortCap.Named("SHRINE")).Resolve()
                : "CC_Storyline_Relic_NoShrine".Translate().Resolve();
        }
    }

    /// <summary>The thief's camp's map made (the colony's caravan came to it): the relic and the thief there (RelicRules.MapMade). Only adds them.</summary>
    [HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]
    public static class Patch_StorylineRelicCamp
    {
        public static void Postfix(Site __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                RelicRules.MapMade(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storylines: the thief's camp's map", e);
            }
        }
    }
}
