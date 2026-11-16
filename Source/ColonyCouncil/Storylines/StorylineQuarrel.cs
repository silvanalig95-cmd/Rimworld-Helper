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
    // S7, two towns' quarrel (docs/quests-proposals.md; the rules in Flows/QuarrelFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Quarrel.xml). Two peoples at peace with the colony and at odds with each other (at
    // war, or their goodwill toward each other below nothing). Part 1: their envoys come to the colony to make a truce with
    // someone of the colony as witness (the witness mission's custom, at home: the colony's best speaker sits with them for
    // two hours); made, their goodwill toward each other rises to peace (the game's own relations between peoples, shown on
    // the World page). Part 2: the first people leave someone at the colony as a pledge for some days, a hosting guest kept
    // fed and housed. Part 3: fighters of the other people, who don't hold with the truce, cross the colony's land to strike
    // the first (the game's travellers' crossing), and a card asks: let them pass (they strike, and the two go to war),
    // warn the first (the strike fails: the first's goodwill toward the other falls, the first thanks the colony and the
    // other resents it), or stop them at the gate (they turn back and the peace holds, unless they force their way in: a
    // fight, cast out by their own people). Fighters scaled to the colony, like the beast.

    /// <summary>Two towns' quarrel beyond its parts: the two peoples, their relations, the fighters, the endings.</summary>
    public class QuarrelRules : StorylineRules
    {
        public override string Kind => QuarrelFlow.Key;

        /// <summary>At odds: at war with each other, or their goodwill toward each other below nothing (and the game lets it change).</summary>
        public static bool AtOdds(Faction a, Faction b) =>
            a != null && b != null && a != b && a.HasGoodwill && b.HasGoodwill && a.CanChangeGoodwillFor(b, 10) && QuarrelFlow.AtOdds(a.GoodwillWith(b), a.HostileTo(b));

        /// <summary>The second people: at peace with the colony and at odds with the first, those at war likelier.</summary>
        public static Faction Rival(Faction a) => Arrivals.Friendly(b => VarietyUtil.OnGoodTerms(b) && AtOdds(a, b), b => a.HostileTo(b) ? 2f : 1f);

        /// <summary>A people at peace with the colony that is at odds with another people at peace with it.</summary>
        public override Faction Giver(Map map) => Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Rival(f) != null);

        /// <summary>The second people gone, at war with the colony, or hidden: it can't go on with them.</summary>
        public static bool OtherGone(Storyline s) => s.other == null || s.other.defeated || s.other.Hidden || s.other.HostileTo(Faction.OfPlayer);

        /// <summary>Their goodwill toward each other changed by <paramref name="change"/> (the game's relations between peoples: war at -75).</summary>
        public static void Between(Faction a, Faction b, int change, string reason)
        {
            if (a == null || b == null || change == 0 || !a.CanChangeGoodwillFor(b, change)) return;
            try
            {
                a.TryAffectGoodwillWith(b, change, canSendMessage: false, canSendHostilityLetter: false, reason: GoodwillWhy.Of(reason));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: two peoples' goodwill", e);
            }
        }

        /// <summary>The colony's thanks and resentment for an ending (QuarrelFlow.Thanks), each with the game's message and its reason.</summary>
        private static void Thanks(Storyline s, string ending, string firstWhy, string otherWhy)
        {
            var (first, other) = QuarrelFlow.Thanks(ending);
            if (first != 0) StoryChains.Goodwill(s.giver, first, GoodwillWhy.Of(firstWhy));
            if (other != 0) StoryChains.Goodwill(s.other, other, GoodwillWhy.Of(otherWhy));
        }

        /// <summary>Every hour: the second people gone or at war with the colony ends it plainly, as for the first.</summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            if (!OtherGone(s)) return;
            s.Set(s.other == null || s.other.defeated ? "otherdestroyed" : "otherwar");
            if (s.missionId >= 0)
            {
                var m = Missions.ById(s.missionId);
                if (m != null && m.Open) Missions.Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "cancelled", null);
                return;
            }
            if (s.Running) Storylines.End(s, StorylineMath.Gone, map);
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var a = s.giver;
            var b = s.other;
            switch (key)
            {
                case "war":
                    // The strike made: the two go to war with each other.
                    if (a != null && b != null) Between(a, b, QuarrelFlow.ChangeTo(a.GoodwillWith(b), QuarrelFlow.WarGoodwill), GoodwillReasonMath.Truce);
                    FactionMemory.Note(a, "tookSides", Storylines.Label(s));
                    return true;
                case "warned":
                    if (a != null && b != null) Between(a, b, -QuarrelFlow.WarnedLoss, GoodwillReasonMath.Truce);
                    Thanks(s, key, GoodwillReasonMath.Helped, GoodwillReasonMath.Judged);
                    FactionMemory.Note(a, "warned", Storylines.Label(s));
                    return true;
                case "peace":
                    Thanks(s, key, GoodwillReasonMath.Peace, GoodwillReasonMath.Judged);
                    FactionMemory.Note(a, "madePeace", Storylines.Label(s));
                    FactionMemory.Note(b, "madePeace", Storylines.Label(s));
                    return true;
                case "fought":
                    Thanks(s, key, GoodwillReasonMath.Helped, GoodwillReasonMath.Judged);
                    FactionMemory.Note(a, "protected", Storylines.Label(s));
                    return true;
                case "blamed":
                    if (a != null && b != null) Between(a, b, -QuarrelFlow.BlamedLoss, GoodwillReasonMath.Truce);
                    Thanks(s, key, GoodwillReasonMath.PromiseBroken, GoodwillReasonMath.PromiseBroken);
                    FactionMemory.Note(a, "prisonerDied", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        /// <summary>Either people at war with the colony now: whoever of theirs is here goes on.</summary>
        public override void Gone(Storyline s, Map map)
        {
            var here = new[] { s.x, s.y }.Concat(s.group).Where(p => p != null && p.Spawned && p.Map == map && p.Faction != Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer)).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
        }

        /// <summary>The colony's people at home who could stand against fighters: grown, up and able to do violence.</summary>
        public static int Defenders(Map map) =>
            IncidentWorker_Happening.Colonists(map).Count(p => !p.Downed && !p.Dead && !p.WorkTagIsDisabled(WorkTags.Violent) && p.DevelopmentalStage.Adult());
    }

    // ===================== Part 1: the truce, with the colony as witness =====================

    /// <summary>
    /// The two peoples' envoys come to the colony's gate within a day to make a truce, and ask for a witness of the colony:
    /// two hours spent with the two of them (the colony's best speaker sits down with them; anyone of the colony near both
    /// counts). Made: their goodwill toward each other rises to peace, and both think better of the colony. Not witnessed
    /// within a day: they go home without it.
    /// </summary>
    public class HappeningWorker_QuarrelTruce : HappeningWorker_StorylinePart
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.Rules(QuarrelFlow.Key).Giver(map) != null && VarietyUtil.Best(map, SkillDefOf.Social) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var a = Storylines.Rules(QuarrelFlow.Key).Giver(map);
            var b = QuarrelRules.Rival(a);
            if (s == null || a == null || b == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            var x = Arrivals.Make(Arrivals.PersonKind(a), a, new FloatRange(35f, 65f));
            var y = Arrivals.Make(Arrivals.PersonKind(b), b, new FloatRange(35f, 65f));
            if (x == null || y == null)
            {
                Arrivals.Discard(x);
                Arrivals.Discard(y);
                return false;
            }
            Arrivals.Hold(x);
            Arrivals.Hold(y);
            s.giver = a;
            s.other = b;
            s.x = x;
            s.y = y;
            s.xName = x.LabelShort;
            s.yName = y.LabelShort;
            s.place = LivingEvents.TownOf(a, map) ?? a.Name;
            s.tale = LivingEvents.TownOf(b, map) ?? b.Name;
            if (a.HostileTo(b)) s.Set("atwar");
            m.faction = a;
            m.place = s.place;
            m.x = x;
            m.group.Add(y);
            m.days = 1.5f;
            m.goodwill = QuarrelFlow.WitnessGoodwill;
            m.penalty = 4;
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.enemy = b;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s?.x == null || s.y == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.2f, 0.4f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        private static List<Pawn> Envoys(Storyline s) => new[] { s.x, s.y }.Where(p => p != null && p.Spawned && !p.Dead).ToList();

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                // At war with each other, their envoys come under a ceasefire, or they would fight at the gate: their
                // goodwill toward each other at nothing while they meet, and back where it was if no truce is made.
                if (s.giver != null && s.other != null && s.giver.HostileTo(s.other) && !s.Has("ceasefire"))
                {
                    s.goalA = s.giver.GoodwillWith(s.other);
                    s.Set("ceasefire");
                    QuarrelRules.Between(s.giver, s.other, QuarrelFlow.ChangeTo(s.goalA, 0), GoodwillReasonMath.Truce);
                }
                var gate = PlaySpots.GateCell(map, null);
                IntVec3? spot = gate.IsValid ? gate : (IntVec3?)null;
                float hours = Mathf.Max(8f, (m.deadline - now) / (float)GenDate.TicksPerHour + 2f);
                bool came = Arrivals.Visit(new List<Pawn> { s.x }, map, s.giver, spot, hours) != null;
                came &= Arrivals.Visit(new List<Pawn> { s.y }, map, s.other, spot, hours) != null;
                if (!came)
                {
                    Arrivals.SendAway(Envoys(s));
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.tally = 0;
                Note(m, map, "came", new LookTargets(Envoys(s)));
                return;
            }
            var envoys = Envoys(s);
            if (envoys.Count < 2 || envoys.Any(p => p.Downed || p.IsPrisoner))
            {
                Arrivals.SendAway(envoys);
                Missions.Goodwill(m, -m.penalty);
                EndCeasefire(s);
                End(m, MissionState.Failed, "missed");
                return;
            }
            // The colony's witness: its best speaker sits with them, once they are both at the gate.
            if (m.stage == 1 && envoys.All(p => p.Position.InHorDistOf(envoys[0].Position, 10f)))
            {
                m.stage = 2;
                SendWitness(m, s, map);
            }
            int dt = Mathf.Max(0, now - m.lastLook);
            m.lastLook = now;
            var witness = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => !c.Downed && c.DevelopmentalStage.Adult() && envoys.All(e => c.Position.InHorDistOf(e.Position, 8f)));
            if (witness != null)
            {
                m.tally += dt;
                m.a = witness;
            }
            if (m.tally >= QuarrelFlow.WitnessHours * GenDate.TicksPerHour)
            {
                Made(m, s, map, witness ?? m.a, envoys);
                return;
            }
            if (now < m.deadline) return;
            Arrivals.SendAway(envoys);
            Missions.Goodwill(m, -m.penalty);
            Say(s.x, m, "missed");
            EndCeasefire(s);
            End(m, MissionState.Failed, "missed", new LookTargets(envoys));
        }

        /// <summary>No truce: the ceasefire for the meeting is over, and their goodwill toward each other back where it was.</summary>
        private static void EndCeasefire(Storyline s)
        {
            if (!s.Has("ceasefire") || s.giver == null || s.other == null) return;
            s.flags.Remove("ceasefire");
            QuarrelRules.Between(s.giver, s.other, QuarrelFlow.ChangeTo(s.giver.GoodwillWith(s.other), s.goalA), GoodwillReasonMath.Truce);
        }

        /// <summary>The colony's best speaker goes out to the envoys at the gate and stays with them (the play-out's visit); with answers not played out, someone has to be sent.</summary>
        private void SendWitness(Mission m, Storyline s, Map map)
        {
            var who = VarietyUtil.Best(map, SkillDefOf.Social);
            if (who == null || !PlayOuts.On) return;
            var ctx = new HappeningContext { map = map, a = who, stranger = s.x, faction = s.giver, incident = def };
            new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.X, duration = new FloatRange(QuarrelFlow.WitnessHours + 0.5f, QuarrelFlow.WitnessHours + 1f),
                hours = new IntRange(6, 22)
            }.Begin(ctx);
        }

        /// <summary>The truce made: their goodwill toward each other up to peace, both thank the colony, and the witness remembers it.</summary>
        private void Made(Mission m, Storyline s, Map map, Pawn witness, List<Pawn> envoys)
        {
            var a = s.giver;
            var b = s.other;
            // The truce from where they stood before any ceasefire for the meeting.
            int before = s.Has("ceasefire") ? s.goalA : a?.GoodwillWith(b) ?? 0;
            s.flags.Remove("ceasefire");
            if (a != null && b != null) QuarrelRules.Between(a, b, QuarrelFlow.ChangeTo(a.GoodwillWith(b), QuarrelFlow.TruceGoodwill(before)), GoodwillReasonMath.Truce);
            StoryChains.Goodwill(a, QuarrelFlow.WitnessGoodwill, GoodwillWhy.Of(GoodwillReasonMath.Truce));
            StoryChains.Goodwill(b, QuarrelFlow.WitnessGoodwill, GoodwillWhy.Of(GoodwillReasonMath.Truce));
            FactionMemory.Note(a, "mediated", Storylines.Label(s));
            FactionMemory.Note(b, "mediated", Storylines.Label(s));
            var thought = StorytellerEvents.Thought("Rimfolk_Mission_StoodWitness");
            if (thought != null && witness != null && !witness.Dead) CouncilGameComponent.GiveThought(witness, thought);
            if (witness != null) m.a = witness;
            Say(s.x, m, "made");
            Arrivals.SendAway(envoys);
            End(m, MissionState.Done, "witnessed", new LookTargets(envoys), witness?.LabelShort);
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var here = s == null ? new List<Pawn>() : Envoys(s);
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            if (m.stage == 0) return "CC_Storyline_Truce_Coming".Translate(TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            float hours = m.tally / (float)GenDate.TicksPerHour;
            return "CC_Storyline_Truce_Witness".Translate(hours.ToString("0.0").Named("HOURS"), QuarrelFlow.WitnessHours.ToString("0").Named("NEED")).Resolve();
        }
    }

    // ===================== Part 2: the pledge =====================

    /// <summary>
    /// The first people leave one of theirs at the colony as a pledge of the truce for four to six days: a hosting guest
    /// (a bed and meals, every day counted). Kept: every day fed and housed; poorly: some days short. Dead or taken
    /// prisoner in the colony's care: harmed.
    /// </summary>
    public class HappeningWorker_QuarrelPledge : HappeningWorker_StorylineHost
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && !QuarrelRules.OtherGone(Storylines.current);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var pledge = Make(s.giver, new FloatRange(16f, 28f));
            if (pledge == null) return false;
            s.group.Clear();
            s.group.Add(pledge);
            s.name = pledge.LabelShort;
            m.x = pledge;
            m.faction = s.giver;
            m.place = s.place;
            m.target = 1;
            m.wait = Rand.Range(0.4f, 0.8f);
            float stay = Rand.Range(QuarrelFlow.PledgeMinDays, QuarrelFlow.PledgeMaxDays);
            m.days = m.wait + stay;
            m.goals.Add(new MissionGoal { what = "meals", count = Mathf.CeilToInt(2.5f * stay) });
            m.goodwill = 8;
            m.penalty = 8;
            Fill(m, ctx);
            ctx.stranger = pledge;
            ctx.enemy = s.other;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.x == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage >= 1 && (m.x.Dead || m.x.IsPrisonerOfColony))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "harmed", m.x.Spawned ? new LookTargets(m.x) : null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                var spot = Hosting.Spot(Beds(m, map), map);
                if (!Arrive(m, map, spot.IsValid ? spot : PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour + 6f))
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.lacks = 0;
                Note(m, map, "came", new LookTargets(m.x));
                return;
            }
            if (Stay(m, map, now, s.name ?? m.x.LabelShort)) return;
            string how = QuarrelFlow.PledgeOutcome(m.tally, m.misses);
            Missions.Goodwill(m, how == "kept" ? m.goodwill : m.goodwill / 4);
            Say(m.x, m, how);
            SeeOff(m, map);
            End(m, MissionState.Done, how, m.x.Spawned ? new LookTargets(m.x) : null);
        }

        public override string Status(Mission m)
        {
            var map = m.Map;
            if (map == null || m.x == null) return null;
            int beds = Hosting.Slots(Beds(m, map));
            string roof = "CC_Mission_Refugees_Beds".Translate(beds, m.target).Resolve();
            if (m.stage == 0) return "CC_Storyline_Pledge_Coming".Translate(m.x.Named("NAME"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() + " " + roof;
            return "CC_Mission_Refugees_Staying".Translate(m.tally, m.misses).Resolve() + " " + roof + " " + MealsText(m);
        }
    }

    // ===================== Part 3: the fighters cross the land =====================

    /// <summary>
    /// Fighters of the second people (three to six, by the threat the storyteller would send now) come onto the colony's
    /// land within a day on their way to strike the first, and rest a while at its edge; the card asks: let them pass, warn
    /// the first people, or stop them at the gate. Passing or warned, they cross the land as the game's travellers cross a
    /// map. Stopped, they wait at the gate a couple of hours and turn back, or force their way in (at a chance by how many
    /// of the colony could stand against them): then they are cast out by their own people, and it is a fight.
    /// </summary>
    public class HappeningWorker_QuarrelFighters : HappeningWorker_StorylinePart
    {
        public const string Card = "Rimfolk_Storyline_QuarrelCard";

        protected override int Rating => 3;

        protected override bool Possible(Map map) =>
            Storylines.current?.giver != null && !QuarrelRules.OtherGone(Storylines.current) && IncidentWorker_Happening.FightsPossible;

        private static List<Pawn> Fighters(Mission m, Map map) => m.group.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map).ToList();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            var b = s?.other;
            if (s?.giver == null || b == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            int n = QuarrelFlow.Fighters(points);
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(b, new FloatRange(18f, 45f), HomeQuests.FighterKind(b, points / n));
                if (p != null) m.group.Add(p);
            }
            if (m.group.Count == 0) return false;
            s.group.Clear();
            s.group.AddRange(m.group);
            s.count = m.group.Count;
            m.faction = s.giver;
            m.place = s.place;
            m.days = 2f;
            m.goodwill = 6;
            Fill(m, ctx);
            ctx.enemy = b;
            ctx.count = m.group.Count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
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
            var fighters = Fighters(m, map);
            switch (m.stage)
            {
                case 0:
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay) return;
                    Cross(m, s, map, now);
                    return;
                case 1:
                    // The card: answered (Answered moves on), or lost without an answer: they pass.
                    if (!StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
                    {
                        if (now - m.lastLook > GenDate.TicksPerHour * 6 || !Ask(m, s, map, now))
                            Answered(m, s, StorylineKit.Context(s, m, map), 0, true);
                    }
                    return;
                case 2:
                {
                    // At the gate: after a couple of hours they turn back, or force their way in.
                    var gate = PeoplesD.GateSpot(map);
                    bool there = fighters.Any(p => gate == null || p.Position.InHorDistOf(gate.Value, 12f));
                    if (fighters.Count == 0)
                    {
                        End(m, MissionState.Done, "stopped");
                        return;
                    }
                    if (!there && now - m.lastLook < GenDate.TicksPerHour * 4) return;
                    if (now - m.lastLook < QuarrelFlow.GateHours * GenDate.TicksPerHour) return;
                    if (Rand.Chance(QuarrelFlow.ForceChance(QuarrelRules.Defenders(map), fighters.Count))) Force(m, s, map, fighters, now);
                    else
                    {
                        Lords.Leave(fighters);
                        LordMaker.MakeNewLord(s.other, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), map, fighters);
                        Say(fighters.FirstOrDefault(), m, "turnback");
                        End(m, MissionState.Done, "stopped", new LookTargets(fighters));
                    }
                    return;
                }
                case 3:
                    // The fight at the gate: over once none of them is up and on the land.
                    if (fighters.Any(p => !p.Downed && p.HostileTo(Faction.OfPlayer)) && now < m.deadline + GenDate.TicksPerDay) return;
                    End(m, MissionState.Done, "fought", fighters.Count > 0 ? new LookTargets(fighters) : null);
                    return;
                default:
                    // Crossing: over once they have left the land (or a day on).
                    if (fighters.Count > 0 && now - m.lastLook < GenDate.TicksPerDay) return;
                    End(m, MissionState.Done, m.variant == "warned" ? "warned" : "passed", null);
                    return;
            }
        }

        /// <summary>They walk in from one edge and rest there a while on their way to the far one, and the card asks.</summary>
        private void Cross(Mission m, Storyline s, Map map, int now)
        {
            var b = s.other;
            var fighters = m.group.Where(p => p != null && !p.Dead && !p.Discarded && !p.Spawned).ToList();
            if (b == null || fighters.Count == 0 || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly))
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            foreach (var p in fighters) Arrivals.Spawn(p, map, entry);
            var here = fighters.Where(p => p.Spawned).ToList();
            if (here.Count == 0)
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            var rest = CellFinder.RandomClosewalkCellNear(entry, map, 8);
            LordMaker.MakeNewLord(b, new LordJob_VisitOnErrand(b, rest, GenDate.TicksPerHour * 8), map, here);
            m.cell = entry;
            m.stage = 1;
            m.lastLook = now;
            StorylineKit.NoteLetter(P, s, m, map, "came", new LookTargets(here), LetterDefOf.NeutralEvent);
            Ask(m, s, map, now);
        }

        /// <summary>The card: pass, warn, or stop them at the gate. False: nobody of the colony to answer now.</summary>
        private bool Ask(Mission m, Storyline s, Map map, int now)
        {
            var fighters = Fighters(m, map);
            bool asked = StorylineKit.Ask(Card, s, m, map, c =>
            {
                c.stranger = fighters.FirstOrDefault();
                c.faction = s.giver;
                c.enemy = s.other;
                c.count = fighters.Count;
            });
            m.stage = 1;
            m.lastLook = now;
            return asked;
        }

        /// <summary>They go on across the land to its far edge (the game's travellers' crossing).</summary>
        private static void GoOn(Mission m, Storyline s, Map map, List<Pawn> fighters)
        {
            if (fighters.Count == 0) return;
            Lords.Leave(fighters);
            var from = fighters[0].Position;
            if (RCellFinder.TryFindTravelDestFrom(m.cell.IsValid ? m.cell : from, map, out var dest)) LordMaker.MakeNewLord(s.other, new LordJob_TravelAndExit(dest), map, fighters);
            else LordMaker.MakeNewLord(s.other, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), map, fighters);
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            int now = Find.TickManager.TicksGame;
            var fighters = Fighters(m, map);
            // Gone on already: they can't be stopped any more.
            if (fighters.Count == 0 && option == 2) option = 0;
            switch (option)
            {
                case 1:
                    // Word sent to the first people: they are ready, and the strike fails. The fighters cross on as before.
                    s.Set("warned");
                    m.variant = "warned";
                    m.stage = 4;
                    m.lastLook = now;
                    GoOn(m, s, map, fighters);
                    Note(m, map, "warnsent", fighters.Count > 0 ? new LookTargets(fighters) : LookTargets.Invalid);
                    return;
                case 2:
                {
                    // Stopped at the gate: they are asked to wait there, and the colony's speaker goes out to them.
                    var gate = PeoplesD.GateSpot(map) ?? fighters.FirstOrDefault()?.Position ?? map.Center;
                    if (fighters.Count > 0)
                    {
                        Lords.Leave(fighters);
                        LordMaker.MakeNewLord(s.other, new LordJob_VisitOnErrand(s.other, gate, Mathf.RoundToInt((QuarrelFlow.GateHours + 4f) * GenDate.TicksPerHour)), map, fighters);
                        var speaker = VarietyUtil.Best(map, SkillDefOf.Social);
                        if (speaker != null && PlayOuts.On)
                        {
                            var c = new HappeningContext { map = map, a = speaker, stranger = fighters[0], faction = s.other, incident = def };
                            new DilemmaEffect_Visit { who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.X, duration = new FloatRange(1f, 2f), hours = new IntRange(0, 23) }.Begin(c);
                        }
                    }
                    s.Set("stop");
                    m.stage = 2;
                    m.lastLook = now;
                    Say(fighters.FirstOrDefault(), m, "gate");
                    return;
                }
                default:
                    // Let pass: they cross the land and go on to strike.
                    s.Set("passed");
                    m.variant = "passed";
                    m.stage = 4;
                    m.lastLook = now;
                    GoOn(m, s, map, fighters);
                    return;
            }
        }

        /// <summary>They force their way in: cast out by their own people for breaking the truce, an outlaw band, and a fight at the gate.</summary>
        private void Force(Mission m, Storyline s, Map map, List<Pawn> fighters, int now)
        {
            var outlaws = HomeQuests.Enemy(s.other) ?? Find.FactionManager.RandomEnemyFaction(false, false, false);
            if (outlaws == null)
            {
                Lords.Leave(fighters);
                LordMaker.MakeNewLord(s.other, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), map, fighters);
                End(m, MissionState.Done, "stopped", new LookTargets(fighters));
                return;
            }
            Lords.Leave(fighters);
            foreach (var p in fighters)
            {
                try
                {
                    if (p.Faction != outlaws) p.SetFaction(outlaws);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: the fighters cast out", e);
                }
            }
            LordMaker.MakeNewLord(outlaws, new LordJob_AssaultColony(outlaws, false, true, false, false, false), map, fighters);
            s.Set("forced");
            m.stage = 3;
            m.lastLook = now;
            StorylineKit.NoteLetter(P, s, m, map, "forced", new LookTargets(fighters), LetterDefOf.ThreatBig);
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "pass":
                    sheet.Play("CC_Storyline_Impact_Pass".Translate((s.other?.Name ?? "?").Named("OTHERPEOPLE"), (s.giver?.Name ?? "?").Named("FACTION")).Resolve(),
                        "CC_Storyline_Impact_PassBrief".Translate().Resolve());
                    break;
                case "warn":
                {
                    var (first, other) = QuarrelFlow.Thanks("warned");
                    sheet.Goodwill(s.giver, first);
                    sheet.Goodwill(s.other, other);
                    sheet.Play("CC_Storyline_Impact_Warn".Translate((s.other?.Name ?? "?").Named("OTHERPEOPLE"), (s.giver?.Name ?? "?").Named("FACTION")).Resolve(),
                        "CC_Storyline_Impact_WarnBrief".Translate().Resolve());
                    break;
                }
                case "stop":
                {
                    var (first, other) = QuarrelFlow.Thanks("peace");
                    sheet.Goodwill(s.giver, first);
                    sheet.Goodwill(s.other, other);
                    var map = m?.Map ?? ctx.map;
                    float chance = map == null ? 0.2f : QuarrelFlow.ForceChance(QuarrelRules.Defenders(map), Fighters(m, map).Count);
                    sheet.Play("CC_Storyline_Impact_Stop".Translate(Mathf.RoundToInt(chance * 100f).Named("CHANCE")).Resolve(), "CC_Storyline_Impact_StopBrief".Translate().Resolve());
                    break;
                }
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var map = m.Map;
            var here = map == null ? new List<Pawn>() : Fighters(m, map);
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            switch (m.stage)
            {
                case 0: return "CC_Storyline_Fighters_Coming".Translate((s.other?.Name ?? "?").Named("OTHERPEOPLE"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
                case 1: return "CC_Storyline_Fighters_Asking".Translate().Resolve();
                case 2: return "CC_Storyline_Fighters_Gate".Translate().Resolve();
                case 3: return "CC_Storyline_Fighters_Fight".Translate().Resolve();
                default: return "CC_Storyline_Fighters_Crossing".Translate().Resolve();
            }
        }
    }
}
