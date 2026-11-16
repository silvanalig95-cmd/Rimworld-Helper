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
    // Quests at home, round 1 (the player's go of 16 November 2026: docs/quests-proposals.md, Q1, Q2 and Q5 here; Q7, Q9
    // and Q10 in Workers_MissionHome2.cs; texts in Defs/MissionDefs/Rimfolk_Missions_Home.xml; the pure rules in
    // HomeQuestsMath.cs). Each plays out on the colony's own map: the people come to the colony, and what they leave or
    // want is collected by them.

    // ===================== Q1. Their prisoner kept =====================

    /// <summary>
    /// A neighbour has caught someone (a raider, a thief, a deserter) and has nowhere safe to hold them until their own
    /// people can deal with them. Two of theirs walk the prisoner in to a bed in the colony's prison and hand them over;
    /// the colony keeps them there for the days agreed, as the game keeps any prisoner (they may try to break out at the
    /// game's own odds); then their people come back, collect the prisoner and pay. Escaped, dead, let go or taken into the
    /// colony, it ends badly.
    /// </summary>
    public class HappeningWorker_MissionPrisoner : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuestsMath.PrisonerKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 2;

        /// <summary>A bed in a prison cell that nobody else has (or that is already <paramref name="x"/>'s).</summary>
        public static Building_Bed PrisonBed(Map map, Pawn x = null) =>
            map?.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().FirstOrDefault(b => b.Spawned && b.ForPrisoners && !b.Medical
                && b.def.building?.bed_humanlike == true && (b.OwnersForReading.Count < b.SleepingSlotsCount || x != null && b.OwnersForReading.Contains(x))
                && (b.GetRoom()?.IsPrisonCell ?? false));

        protected override bool Possible(Map map) => PrisonBed(map) != null && Neighbor(map) != null && HomeQuests.Enemy(null) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.other = HomeQuests.Enemy(m.faction);
            if (m.other == null) return false;
            var x = HomeQuests.Person(m.other, new FloatRange(18f, 50f), HomeQuests.FighterKind(m.other, 80f));
            if (x == null) return false;
            HomeQuests.Disarm(x);
            m.x = x;
            m.name = x.LabelShort;
            for (int i = 0; i < 2; i++)
            {
                var g = HomeQuests.Person(m.faction, new FloatRange(22f, 55f), Couriers.PersonKind(m.faction));
                if (g != null) m.group.Add(g);
            }
            if (m.group.Count == 0) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, Interplay.Warden, SkillDefOf.Social);
            if (m.a == null) return false;
            m.target = HomeQuestsMath.Days(HomeQuest.Prisoner, Rand.Value);
            m.wait = Rand.Range(HomeQuestsMath.ComeMin, HomeQuestsMath.ComeMax);
            // Time for them to come, the days held, and a day for their people to come back.
            m.days = Mathf.Ceil(m.wait + m.target + 1f);
            m.reward = HomeQuests.Reward(HomeQuest.Prisoner, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.Prisoner);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.Prisoner);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.enemy = m.other;
            return true;
        }

        /// <summary>The terms: {DAYS} is the days held, {ITEM} the reward in words, {ENEMY} the prisoner's own people.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => m.target, m => m.reward);

        public override bool Start(Mission m, Map map)
        {
            if (m.x == null || m.x.Dead || m.x.Discarded || !m.group.Any(p => p != null && !p.Dead)) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
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
            switch (m.stage)
            {
                case 0:
                    if (now >= m.due) Bring(m, map, now);
                    return;
                case 1:
                    Walk(m, map, now);
                    return;
                case 2:
                    Hold(m, map, now);
                    return;
                default:
                    Collect(m, map, now);
                    return;
            }
        }

        /// <summary>Their people walk in with the prisoner, making for the prison: the prisoner one of theirs on the way, so the colony's doors open for them.</summary>
        private void Bring(Mission m, Map map, int now)
        {
            var x = m.x;
            var bed = PrisonBed(map);
            if (bed == null)
            {
                // Nowhere to hold them now: they keep the prisoner themselves.
                Missions.Goodwill(m, -m.penalty / 2);
                Missions.Finish(m, MissionState.Failed, "noprison", null);
                return;
            }
            var room = bed.GetRoom();
            var spot = room?.Cells.Where(c => c.Standable(map) && c.GetEdifice(map) == null).RandomElementWithFallback(bed.Position) ?? bed.Position;
            var people = m.group.Where(p => p != null && !p.Dead && !p.Discarded && !p.Spawned).ToList();
            if (x.Dead || people.Count == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            try
            {
                if (x.Faction != m.faction) x.SetFaction(m.faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the prisoner on the road", e);
            }
            people.Insert(0, x);
            if (Arrivals.Visit(people, map, m.faction, spot, 14f) == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            m.stage = 1;
            m.lastLook = now;
            var escort = m.group.FirstOrDefault(p => p.Spawned);
            Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ_Prisoner_Coming".Translate((escort?.LabelShort ?? "?").Named("ESCORT"), x.Named("X"),
                (m.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(people), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>On their way to the prison: handed over inside it, or where they stand once they have been at it half a day.</summary>
        private void Walk(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x.Dead || x.Downed || !x.Spawned || x.Map != map)
            {
                // Hurt or killed on the way in, or gone off with it.
                if (x.Dead || x.Downed) Missions.Goodwill(m, -m.penalty);
                Arrivals.SendAway(m.group);
                Missions.Finish(m, MissionState.Failed, x.Dead || x.Downed ? "harmed" : "ended", null);
                return;
            }
            var room = PrisonBed(map, x)?.GetRoom();
            bool inside = room != null && x.GetRoom() == room;
            if (!inside && now - m.lastLook < GenDate.TicksPerHour * 12) return;
            HandOver(m, map, now, inside);
        }

        /// <summary>The prisoner is the colony's now: held as the game holds any prisoner, kept, not to be recruited.</summary>
        private void HandOver(Mission m, Map map, int now, bool inside)
        {
            var x = m.x;
            try
            {
                Lords.Leave(x);
                x.jobs?.StopAll();
                var own = m.other != null && !m.other.defeated ? m.other : HomeQuests.Enemy(m.faction);
                if (own != null && x.Faction != own) x.SetFaction(own);
                x.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                x.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.MaintainOnly);
                var bed = PrisonBed(map, x);
                if (bed != null) x.ownership?.ClaimBedIfNonMedical(bed);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the prisoner handed over", e);
            }
            var escort = m.group.FirstOrDefault(p => p != null && p.Spawned && !p.Dead);
            HomeQuests.Say(escort, "Rimfolk_Pool_HomeQ_PrisonerHanded");
            Arrivals.SendAway(m.group);
            m.stage = 2;
            m.lastLook = now;
            m.due = now + m.target * GenDate.TicksPerDay;
            string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.target * GenDate.TicksPerDay));
            if (!inside) line = (line.NullOrEmpty() ? "" : line + " ") + "CC_HomeQ_Prisoner_AtGate".Translate(x.Named("X")).Resolve();
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Where the prisoner stands now (HomeQuestsMath.PrisonerOutcome).</summary>
        private static PrisonerEnd Where(Mission m, Map map, int now)
        {
            var x = m.x;
            bool ours = x.Faction == Faction.OfPlayer;
            bool here = x.Spawned && x.Map == map || x.IsPrisonerOfColony && x.GetCaravan() != null;
            return HomeQuestsMath.PrisonerOutcome(x.Dead, ours, x.guest?.Released ?? false, here, now >= m.due && x.IsPrisonerOfColony && x.Spawned && x.Map == map);
        }

        private void Hold(Mission m, Map map, int now)
        {
            var end = Where(m, map, now);
            if (end == PrisonerEnd.Holding) return;
            if (end == PrisonerEnd.Held)
            {
                Collecting(m, map, now);
                return;
            }
            Ended(m, end);
        }

        /// <summary>A stay that ended badly: escaped, dead, let go, or taken into the colony.</summary>
        private static void Ended(Mission m, PrisonerEnd end)
        {
            var x = m.x;
            switch (end)
            {
                case PrisonerEnd.Died:
                    Missions.Goodwill(m, -m.penalty * 2);
                    Missions.Finish(m, MissionState.Failed, "died", null, x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
                    return;
                case PrisonerEnd.Kept:
                    Missions.Goodwill(m, -m.penalty * 2);
                    Missions.Finish(m, MissionState.Failed, "kept", null, x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                    return;
                case PrisonerEnd.Released:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "released", null);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "escaped", null);
                    return;
            }
        }

        /// <summary>The days are up: their people come for the prisoner and pay (Couriers/), else it's done at once.</summary>
        private void Collecting(Mission m, Map map, int now)
        {
            m.stage = 3;
            m.lastLook = now;
            if (!HomeQuests.FetchPrisoner(m, map, m.x, m.reward)) Done(m, map, true);
        }

        private void Collect(Mission m, Map map, int now)
        {
            var x = m.x;
            if (HomeQuests.Collected(m, x))
            {
                Done(m, map, false);
                return;
            }
            var end = Where(m, map, now);
            if (end != PrisonerEnd.Held && end != PrisonerEnd.Holding)
            {
                Ended(m, end);
                return;
            }
            // Nobody came for them after all: taken now.
            if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Done(m, map, true);
        }

        private void Done(Mission m, Map map, bool now)
        {
            if (now)
            {
                HomeQuests.TakeNow(m.x);
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "held", HomeQuests.RewardWords(m, m.reward));
        }

        public override void Release(Mission m)
        {
            foreach (var p in m.group.Prepend(m.x).Where(p => p != null && !p.Dead))
            {
                if (!p.Spawned && m.started < 0 && p.GetCaravan() == null) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            switch (m.stage)
            {
                case 0:
                    return "CC_HomeQ_Prisoner_Status0".Translate(x.Named("X"),
                        (PrisonBed(m.Map) != null ? "CC_HomeQ_Prisoner_BedYes" : "CC_HomeQ_Prisoner_BedNo").Translate().Named("BED")).Resolve();
                case 1:
                    return "CC_HomeQ_Prisoner_Status1".Translate(x.Named("X")).Resolve();
                case 2:
                    int held = Mathf.Clamp((Find.TickManager.TicksGame - m.lastLook) / GenDate.TicksPerDay, 0, m.target);
                    return "CC_HomeQ_Prisoner_Status2".Translate(x.Named("X"), held.Named("HELD"), m.target.Named("DAYS")).Resolve();
                default:
                    return "CC_HomeQ_Prisoner_Status3".Translate(x.Named("X"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            }
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "held", "escaped", "died", "kept", "released" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            switch (outcome)
            {
                case "held":
                    Done(m, map, true);
                    return;
                case "escaped":
                    Ended(m, PrisonerEnd.Escaped);
                    return;
                case "died":
                    Ended(m, PrisonerEnd.Died);
                    return;
                case "kept":
                    Ended(m, PrisonerEnd.Kept);
                    return;
                default:
                    Ended(m, PrisonerEnd.Released);
                    return;
            }
        }
    }

    // ===================== Q2. A dispute judged =====================

    /// <summary>
    /// Two people of a neighbour can't settle a quarrel between themselves (a calf both claim, a field's edge, a debt, a
    /// salvage claim) and ask the colony to judge it. They come to the gate together; the colony's peacemaker (or its best
    /// talker) hears them, and a card asks for the judgement: for one, for the other, split between them, or none. The
    /// judge tells them face to face; the one who won leaves content, the one who lost leaves angry and may say so with the
    /// game's own insult (and its own odds of a fight); a split sends both home grumbling but settled. Their town pays for
    /// the judging.
    /// </summary>
    public class HappeningWorker_MissionDispute : HappeningWorker_Mission
    {
        public const string CardDef = "Rimfolk_Mission_DisputeJudgement";

        protected override string Kind => HomeQuestsMath.DisputeKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 1;

        protected override bool Possible(Map map) => Neighbor(map) != null && StorytellerEvents.Adults(map).Any();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var x = HomeQuests.Person(m.faction, new FloatRange(25f, 65f));
            var y = HomeQuests.Person(m.faction, new FloatRange(25f, 65f));
            if (x == null || y == null)
            {
                Arrivals.Discard(x);
                Arrivals.Discard(y);
                return false;
            }
            m.x = x;
            m.group.Add(y);
            m.name = y.LabelShort;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, Interplay.Peacemaker, SkillDefOf.Social);
            if (m.a == null) return false;
            m.wait = Rand.Range(HomeQuestsMath.ComeMin, HomeQuestsMath.ComeMax);
            m.days = Mathf.Ceil(m.wait + HomeQuestsMath.Days(HomeQuest.Dispute, Rand.Value));
            m.reward = HomeQuests.Reward(HomeQuest.Dispute, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.Dispute);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.Dispute);
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => 0, m => m.reward);

        private static Pawn Second(Mission m) => m.group.FirstOrDefault(p => p != null);

        private static IEnumerable<Pawn> Both(Mission m) => new[] { m.x, Second(m) }.Where(p => p != null);

        public static Ruling RulingOf(Mission m) =>
            m.variant == "first" ? Ruling.First : m.variant == "second" ? Ruling.Second : m.variant == "split" ? Ruling.Split : Ruling.None;

        public override bool Start(Mission m, Map map)
        {
            if (Both(m).Count() < 2 || Both(m).Any(p => p.Dead || p.Discarded)) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var both = Both(m).ToList();
            if (both.Count < 2 || both.Any(p => p.Discarded))
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && m.stage < 4 && both.Any(p => p.Dead || p.IsPrisonerOfColony))
            {
                Missions.Goodwill(m, -m.penalty * 3);
                Arrivals.SendAway(both);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            switch (m.stage)
            {
                case 0:
                    if (now >= m.due) Come(m, map, now);
                    return;
                case 1:
                    // At the gate (or long enough on the way): the question is put to the colony as soon as no other waits.
                    var flag = m.x.GetLord()?.CurLordToil?.FlagLoc ?? IntVec3.Invalid;
                    bool there = m.x.Spawned && flag.IsValid && m.x.Position.InHorDistOf(flag, 10f);
                    if (!there && now - m.lastLook < GenDate.TicksPerHour * 3) return;
                    if (Ask(m, map))
                    {
                        m.stage = 2;
                        m.lastLook = now;
                    }
                    else if (now - m.lastLook > Mathf.RoundToInt(HomeQuestsMath.JudgeHours * GenDate.TicksPerHour)) Judged(m, map, Ruling.None);
                    return;
                case 2:
                    // The card went unanswered somehow (dismissed, lost to a reload's letter stack): they give up waiting.
                    if (!Missions.LetterOut(m) && now - m.lastLook > GenDate.TicksPerHour) Judged(m, map, Ruling.None);
                    return;
                case 3:
                    // The judge gives it face to face (Together.When); if that hasn't come within a few hours, they hear it all the same.
                    if (now >= m.due) React(m, map, false);
                    return;
                default:
                    Quarrel(m, map, now);
                    return;
            }
        }

        /// <summary>Both walk in to the gate, each with food enough for the day; they don't like each other just now.</summary>
        private void Come(Mission m, Map map, int now)
        {
            var both = Both(m).Where(p => !p.Dead && !p.Spawned).ToList();
            var gate = PlaySpots.GateCell(map, null);
            if (both.Count < 2 || Arrivals.Visit(both, map, m.faction, gate.IsValid ? gate : (IntVec3?)null, 24f) == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var p in both) Hosting.Provision(p, 1f);
            var quarrel = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_HomeQ_Quarrel");
            if (quarrel != null)
            {
                // apart: the quarrel they bring with them, from before they came.
                both[0].needs?.mood?.thoughts?.memories?.TryGainMemory(quarrel, both[1]);
                both[1].needs?.mood?.thoughts?.memories?.TryGainMemory(quarrel, both[0]);
            }
            m.stage = 1;
            m.lastLook = now;
            Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ_Dispute_Here".Translate(m.x.Named("X"), (m.name ?? "?").Named("OTHER"),
                (m.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(both), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The card asking for the judgement, as soon as no other question waits.</summary>
        private static bool Ask(Mission m, Map map)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(CardDef);
            if (def == null || HappeningTracker.WaitingAt(map)) return false;
            HappeningWorker_MissionJudgement.current = m;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                return def.Worker.TryExecute(parms);
            }
            finally
            {
                HappeningWorker_MissionJudgement.current = null;
            }
        }

        /// <summary>The colony's judgement (or none): the judge goes to tell them, and they take it.</summary>
        public static void Judged(Mission m, Map map, Ruling r)
        {
            if (m == null || m.stage >= 3 || !m.Open) return;
            m.variant = HomeQuestsMath.Tag(r);
            m.stage = 3;
            int now = Find.TickManager.TicksGame;
            m.due = now + GenDate.TicksPerHour * 3;
            var judge = m.a;
            if (r == Ruling.None || judge == null || judge.Dead || !judge.IsColonist || map == null)
            {
                React(m, map, false);
                return;
            }
            // The judge walks over to them in their free time, and says it to their faces.
            Together.When("homequest:dispute|" + m.id, judge, m.x, () => React(m, map, true), 3f, 0f);
        }

        /// <summary>How each takes it, on the map: the judge's words, the winner's thanks, the loser's anger, the town's fee.</summary>
        private static void React(Mission m, Map map, bool judgeThere)
        {
            if (m == null || !m.Open || m.stage != 3 || map == null) return;
            var r = RulingOf(m);
            var x = m.x;
            var y = Second(m);
            int now = Find.TickManager.TicksGame;
            Together.Forget("homequest:dispute|" + m.id);
            if (judgeThere && r != Ruling.None) HomeQuests.Say(m.a, "Rimfolk_Pool_HomeQ_Ruling", r == Ruling.Second ? y : x, HomeQuestsMath.Tag(r));
            Pawn won = r == Ruling.First ? x : r == Ruling.Second ? y : null;
            Pawn lost = r == Ruling.First ? y : r == Ruling.Second ? x : null;
            switch (r)
            {
                case Ruling.First:
                case Ruling.Second:
                    HomeQuests.Say(won, "Rimfolk_Pool_HomeQ_Won");
                    // together: both stand at the gate, where the judgement was given.
                    HomeQuests.Say(lost, "Rimfolk_Pool_HomeQ_Lost", Together.Can(lost, won) ? won : null);
                    break;
                case Ruling.Split:
                    HomeQuests.Say(x, "Rimfolk_Pool_HomeQ_Split");
                    HomeQuests.Say(y, "Rimfolk_Pool_HomeQ_Split");
                    break;
                default:
                    HomeQuests.Say(x, "Rimfolk_Pool_HomeQ_Unjudged");
                    break;
            }
            if (r != Ruling.None) HomeQuests.Pay(m, map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, x, m.reward);
            m.tally = HomeQuestsMath.Insults(r);
            m.stage = 4;
            m.lastLook = now;
            m.due = now + GenDate.TicksPerHour * 2;
        }

        /// <summary>
        /// The hour or two before they leave: the one the judgement went against has a few more words for the other, the
        /// game's own insult each time (and the game's own odds that it comes to blows). Then each goes home.
        /// </summary>
        private void Quarrel(Mission m, Map map, int now)
        {
            var r = RulingOf(m);
            var x = m.x;
            var y = Second(m);
            Pawn won = r == Ruling.Second ? y : x;
            Pawn lost = r == Ruling.Second ? x : y;
            if (x.MentalStateDef == MentalStateDefOf.SocialFighting || y?.MentalStateDef == MentalStateDefOf.SocialFighting) m.misses = 1;
            bool fighting = x.InMentalState || (y?.InMentalState ?? false);
            if (m.tally > 0 && now - m.lastLook >= 600 && lost != null && won != null && Together.Can(lost, won))
            {
                m.lastLook = now;
                m.tally--;
                try
                {
                    // together: Together.Can just now; the game's own insult, and its own odds of a fight.
                    if (lost.interactions != null && lost.interactions.CanInteractNowWith(won, InteractionDefOf.Insult)) lost.interactions.TryInteractWith(won, InteractionDefOf.Insult);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the quarrel", e);
                }
                if (lost.MentalStateDef == MentalStateDefOf.SocialFighting || won.MentalStateDef == MentalStateDefOf.SocialFighting) m.misses = 1;
                return;
            }
            if (fighting && now < m.due + GenDate.TicksPerHour) return;
            if (m.tally > 0 && now < m.due) return;
            End(m, map);
        }

        private static void End(Mission m, Map map)
        {
            var r = RulingOf(m);
            var both = Both(m).ToList();
            // Each goes home on their own, the one who won first.
            Arrivals.SendAway(both);
            int gw = HomeQuestsMath.RulingGoodwill(r, m.goodwill, m.penalty);
            Missions.Goodwill(m, gw);
            var look = new LookTargets(both.Where(p => p.Spawned));
            if (r == Ruling.None) Missions.Finish(m, MissionState.Failed, "none", null, look);
            else Missions.Finish(m, MissionState.Done, HomeQuestsMath.Tag(r), HomeQuests.RewardWords(m, m.reward), look, r == Ruling.Split ? null : LetterDefOf.NeutralEvent);
        }

        public override void Release(Mission m)
        {
            Together.Forget("homequest:dispute|" + m.id);
            foreach (var p in Both(m).Where(p => !p.Dead))
            {
                if (!p.Spawned && m.started < 0) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var here = Both(m).Where(p => p.Spawned).ToList();
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            if (m.x == null) return null;
            string key = m.stage == 0 ? "CC_HomeQ_Dispute_Status0" : m.stage <= 2 ? "CC_HomeQ_Dispute_Status1" : "CC_HomeQ_Dispute_Status3";
            return key.Translate(m.x.Named("X"), (m.name ?? "?").Named("OTHER")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "first", "second", "split", "none", "card now" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            if (outcome == "card now")
            {
                if (m.stage == 1 && Ask(m, map)) m.stage = 2;
                return;
            }
            var r = outcome == "first" ? Ruling.First : outcome == "second" ? Ruling.Second : outcome == "split" ? Ruling.Split : Ruling.None;
            if (m.stage < 3)
            {
                m.stage = 2;
                Judged(m, map, r);
            }
            if (m.stage == 3) React(m, map, false);
            if (m.stage == 4) End(m, map);
        }
    }

    /// <summary>The card for a quarrel brought to the colony: judge for one, for the other, split it, or send them away unjudged.</summary>
    public class HappeningWorker_MissionJudgement : IncidentWorker_Happening
    {
        internal static Mission current;

        protected override bool CanHappen(Map map, HappeningExtension ext) => current?.x != null && current.x.Spawned;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = current;
            if (m?.x == null) return false;
            var c = Missions.Context(m, ctx.map);
            ctx.a = c.a;
            ctx.faction = m.faction;
            ctx.placeName = m.place;
            ctx.stranger = m.x;
            ctx.dead = m.name;
            ctx.count = m.reward;
            ctx.note = Missions.Note(m);
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var m = current ?? Missions.Of(ctx);
            if (m != null)
                foreach (var t in Missions.Tags(m))
                    tags.Add(t);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var m = Missions.Of(ctx);
            var map = ctx.map != null && Find.Maps.Contains(ctx.map) ? ctx.map : m?.Map;
            if (m != null && m.Open && m.stage == 2)
            {
                var r = byDefault ? Ruling.None : option == 0 ? Ruling.First : option == 1 ? Ruling.Second : option == 2 ? Ruling.Split : Ruling.None;
                HappeningWorker_MissionDispute.Judged(m, map, r);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The two stay the mission's: nothing to let go of.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    // ===================== Q5. A wounded messenger =====================

    /// <summary>
    /// A neighbour's messenger was attacked on the road (by raiders, or by wild animals) and makes for the colony's gate
    /// with real wounds, the game's own. They hand over the letter they carry, and are taken in as the colony's guest: a bed,
    /// a doctor, food while they mend (the game's own care for a guest), then a place at the colony until their people come
    /// for the letter some days later. The letter is the colony's to keep safe until then. The messenger dying, or the
    /// letter lost, ends it badly.
    /// </summary>
    public class HappeningWorker_MissionMessenger : HappeningWorker_MissionHost
    {
        protected override string Kind => HomeQuestsMath.MessengerKind;

        protected override float RestDays => HomeQuestsMath.RestDays;

        protected override int Rating => 2;

        public override bool OwnMemory => false;

        /// <summary>A bed the colony could put a patient in.</summary>
        private static bool AnyBed(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Any(b => b.def.building?.bed_humanlike == true && !b.ForPrisoners && !b.ForSlaves);

        protected override bool Possible(Map map) => Neighbor(map) != null && AnyBed(map) && VarietyUtil.Best(map, SkillDefOf.Medicine) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var letter = HomeQuests.LetterDef(m.faction);
            if (letter == null) return false;
            m.x = HomeQuests.Person(m.faction, new FloatRange(20f, 45f));
            if (m.x == null) return false;
            m.name = m.x.LabelShort;
            m.tale = letter.label;
            m.variant = Rand.Chance(0.3f) ? "beasts" : "raiders";
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, Interplay.Physician, SkillDefOf.Medicine);
            if (m.a == null) return false;
            m.target = HomeQuestsMath.Days(HomeQuest.Messenger, Rand.Value);
            // They are already on their way when their people's word comes.
            m.wait = Rand.Range(0.1f, 0.25f);
            m.days = Mathf.Ceil(m.wait + m.target + 1f);
            m.reward = HomeQuests.Reward(HomeQuest.Messenger, m.faction, map);
            m.goodwill = HomeQuestsMath.Goodwill(HomeQuest.Messenger);
            m.penalty = HomeQuestsMath.Penalty(HomeQuest.Messenger);
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, m => m.target, m => m.reward);

        private static Thing Letter(Mission m) => m.thing;

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && x.Dead)
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "died", null, x.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
                return;
            }
            if (m.stage >= 2 && m.stage < 4 && !HomeQuests.OnMap(Letter(m), map))
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", null, x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                return;
            }
            switch (m.stage)
            {
                case 0:
                    if (now >= m.due) Come(m, map, now);
                    return;
                case 1:
                {
                    if (!x.Spawned || x.Map != map)
                    {
                        Missions.Finish(m, MissionState.Failed, "ended", null);
                        return;
                    }
                    var flag = x.GetLord()?.CurLordToil?.FlagLoc ?? IntVec3.Invalid;
                    bool there = flag.IsValid && x.Position.InHorDistOf(flag, 8f);
                    if (there || x.Downed || now - m.lastLook > GenDate.TicksPerHour * 4) Arrived(m, map, now);
                    return;
                }
                case 2:
                case 3:
                    Care(m, map, now);
                    if (now >= m.due) Collecting(m, map, now);
                    return;
                default:
                    Collect(m, map, now);
                    return;
            }
        }

        /// <summary>They come in from the edge toward the gate, hurt the way the attack on the road hurt them.</summary>
        private void Come(Mission m, Map map, int now)
        {
            var x = m.x;
            HomeQuests.Wound(x, m.variant == "beasts", HomeQuests.AgeOf(m.faction));
            var gate = PlaySpots.GateCell(map, null);
            if (Arrivals.Visit(new List<Pawn> { x }, map, m.faction, gate.IsValid ? gate : (IntVec3?)null, Mathf.Max(6f, (m.deadline - now) / (float)GenDate.TicksPerHour)) == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            // A little food of their own for the road; the colony feeds them once they're in its care.
            Hosting.Provision(x, 0.5f);
            m.stage = 1;
            m.lastLook = now;
            Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ_Messenger_Coming".Translate(x.Named("X"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(x), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>At the gate: the letter handed over, and the messenger taken in as the colony's guest, to a bed.</summary>
        private void Arrived(Mission m, Map map, int now)
        {
            var x = m.x;
            var letter = HomeQuests.Make(HomeQuests.LetterDef(m.faction));
            if (letter != null && GenPlace.TryPlaceThing(letter, x.Position, map, ThingPlaceMode.Near)) m.thing = letter;
            if (m.thing == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            HomeQuests.Say(x, "Rimfolk_Pool_HomeQ_MessengerHands");
            TakeIn(x);
            m.stage = 2;
            m.lastLook = now;
            m.due = now + m.target * GenDate.TicksPerDay;
            string line = Line(More?.ready, m, map, null, null);
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(new Thing[] { x, letter }), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The colony's guest while they mend: in a bed, tended and fed by its doctors as the game does a guest.</summary>
        private static void TakeIn(Pawn x)
        {
            try
            {
                if (!x.Spawned) return;
                if (x.HostFaction != Faction.OfPlayer)
                {
                    Lords.Leave(x);
                    x.guest?.SetGuestStatus(Faction.OfPlayer);
                }
                if (x.InBed() || x.CurJobDef == JobDefOf.LayDown || x.Downed) return;
                var bed = RestUtility.FindPatientBedFor(x);
                if (bed != null) x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the messenger taken in", e);
            }
        }

        /// <summary>Hurt: kept in a bed; mended: up again, a visitor near the gate, eating and sleeping as the colony's guests do.</summary>
        private void Care(Mission m, Map map, int now)
        {
            var x = m.x;
            if (!x.Spawned || x.Map != map) return;
            bool hurt = x.Downed || HealthAIUtility.ShouldSeekMedicalRest(x) || x.health.HasHediffsNeedingTend();
            if (m.stage == 2)
            {
                if (hurt)
                {
                    TakeIn(x);
                    return;
                }
                // Mended: up, and waiting at the colony for their people.
                try
                {
                    Hosting.GetUp(x);
                    if (x.HostFaction == Faction.OfPlayer) x.guest?.SetGuestStatus(null);
                    Lords.Leave(x);
                    var gate = PlaySpots.GateCell(map, x);
                    float hours = Mathf.Max(6f, (m.deadline - now) / (float)GenDate.TicksPerHour + 6f);
                    LordMaker.MakeNewLord(m.faction, new LordJob_VisitOnErrand(m.faction, gate.IsValid ? gate : x.Position, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map,
                        new List<Pawn> { x });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the messenger up", e);
                }
                m.stage = 3;
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_HomeQ_Messenger_Mended".Translate(x.Named("X")).Resolve(), new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            // Up and about: hurt again (a raid), back to bed; else the colony's table and a bed at night.
            if (hurt)
            {
                m.stage = 2;
                TakeIn(x);
                return;
            }
            if (x.GetLord() == null && x.HostFaction == null)
            {
                var gate = PlaySpots.GateCell(map, x);
                LordMaker.MakeNewLord(m.faction, new LordJob_VisitOnErrand(m.faction, gate.IsValid ? gate : x.Position, GenDate.TicksPerDay * 2), map, new List<Pawn> { x });
            }
            Tend(m, map);
        }

        /// <summary>Their people come for the letter and bring the colony's reward (Couriers/); else it's taken and paid at once.</summary>
        private void Collecting(Mission m, Map map, int now)
        {
            var letter = Letter(m);
            if (letter == null || !HomeQuests.OnMap(letter, map)) return;
            // Being carried this moment: looked at again shortly.
            if (!letter.Spawned) return;
            m.stage = 4;
            m.lastLook = now;
            if (!Couriers.CollectThings(map, new List<Thing> { letter }, m.faction, HomeQuests.PayThings(m, m.reward))) Done(m, map, true);
        }

        private void Collect(Mission m, Map map, int now)
        {
            var letter = Letter(m);
            if (letter == null || letter.Destroyed || HomeQuests.HeldBy(letter, m.faction) || !HomeQuests.OnMap(letter, map))
            {
                Done(m, map, false);
                return;
            }
            if (m.x.Spawned && m.x.Map == map && !m.x.Dead) Tend(m, map);
            if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Done(m, map, true);
        }

        private void Done(Mission m, Map map, bool now)
        {
            var letter = Letter(m);
            if (now)
            {
                if (letter != null && !letter.Destroyed) letter.Destroy();
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            var x = m.x;
            // Mended, they go home now; still in bed, they stay the colony's guest until they can walk, as any guest does.
            if (x != null && x.Spawned && !x.Dead && x.HostFaction == null && !x.Downed) SeeOff(m, map);
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "delivered", HomeQuests.RewardWords(m, m.reward), x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
        }

        /// <summary>The colony's plainer food for them (they ask to be looked after, not feasted), else raw food.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override void Release(Mission m)
        {
            var x = m.x;
            // Still in a bed of the colony's as its guest: they stay until they can walk, and leave then (the game's own care).
            if (x != null && !x.Dead && x.Spawned && x.HostFaction == Faction.OfPlayer)
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
                return;
            }
            base.Release(m);
        }

        public override LookTargets Targets(Mission m)
        {
            var list = new List<GlobalTargetInfo>();
            if (m.x != null && m.x.Spawned) list.Add(m.x);
            var t = Letter(m);
            if (t != null && t.SpawnedOrAnyParentSpawned) list.Add(t.SpawnedParentOrMe);
            return list.Count > 0 ? new LookTargets(list) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            var map = m.Map;
            string who;
            switch (m.stage)
            {
                case 0:
                case 1:
                    who = "CC_HomeQ_Messenger_Status0".Translate(x.Named("X")).Resolve();
                    break;
                case 2:
                    who = (x.InBed() ? "CC_HomeQ_Messenger_InBed" : "CC_HomeQ_Messenger_NoBed").Translate(x.Named("X")).Resolve();
                    break;
                default:
                    who = "CC_HomeQ_Messenger_Up".Translate(x.Named("X")).Resolve();
                    break;
            }
            var t = Letter(m);
            if (t == null) return who;
            string where = t.IsInAnyStorage() ? "CC_HomeQ_Thing_Stored" : t.Spawned ? "CC_HomeQ_Thing_Lying" : HomeQuests.OnMap(t, map) ? "CC_HomeQ_Thing_Carried" : "CC_HomeQ_Thing_Missing";
            return who + " " + where.Translate((m.tale ?? t.LabelNoCount).Named("THING")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "mended", "delivered", "died", "lost" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            switch (outcome)
            {
                case "mended":
                    if (m.x != null && m.x.Spawned) HealthUtility.HealNonPermanentInjuriesAndRestoreLegs(m.x);
                    return;
                case "delivered":
                    Done(m, map, true);
                    return;
                case "died":
                    Missions.Goodwill(m, -m.penalty * 2);
                    Missions.Finish(m, MissionState.Failed, "died", null);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "lost", null);
                    return;
            }
        }
    }
}
