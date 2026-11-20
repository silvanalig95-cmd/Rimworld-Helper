using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The new head comes to the gate (the content update's B4, 20 November 2026): after "a new leader" or "a contested
    /// seat" at a people at peace with the colony, the one who now leads them rides out to see the region a few days later
    /// (NewHeadMath: three to eight) and stops at the colony's gate with two to four of their people. The colony can sit
    /// them at its table (a meal from the stores, the best talker hosting, a bed for the night), give a gift from the
    /// stores, or say a word at the gate. Their temper, as the succession read it from their traits, shows in what they do:
    /// a kind head leaves a gift of their own, a greedy one asks the colony's trade terms (a caravan of theirs follows), a
    /// harsh one wants a token of respect (a word alone offends), a steady one opens talk of a trade pact where goodwill
    /// allows. What passed moves the head's regard (World/Notables.cs), and a head sat at the table or given a gift is asked
    /// to the colony's next feast with guests (a promise on the world ledger the feast dialog reads). It is a follow-up of
    /// the world events (World/Region/FollowUps.cs, kind "head"), started when a world event's slot is free, and let go
    /// when the head is no longer the head or after thirty days. The def: Defs/WorldSituationDefs/Rimfolk_WS_NewHead.xml.
    /// </summary>
    public static class NewHeads
    {
        public const string Kind = "head";
        /// <summary>The colony's word that the head is asked to its next feast with guests (a Promise tie, from the colony to the head).</summary>
        public const string PromiseKey = "headfeast";

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static WorldSituationDef Def => DefDatabase<WorldSituationDef>.GetNamedSilentFail("Rimfolk_WS_NewHead");

        // ================= Remembered when the seat is settled =================

        /// <summary>
        /// A new leader took the seat (SituationWorker_Succession.Ended, SituationWorker_Claimants.Ended): they will ride out
        /// to the colony's gate, by their temper (<paramref name="temper"/>: kind, greedy, harsh or steady).
        /// </summary>
        public static void Remember(WorldSituation s, Pawn head, string temper)
        {
            try
            {
                string key = s?.endingKey;
                var f = s?.ctx?.faction;
                if (s?.ctx == null || key.NullOrEmpty() || key == WorldSituations.Quietly || Def == null) return;
                if (f == null || f.defeated || !FactionMemory.Remembers(f) || f.HostileTo(Faction.OfPlayer)) return;
                if (head == null || head.Dead || head.Discarded || head.Faction != f) return;
                int now = Now;
                var fu = new FollowUp
                {
                    kind = Kind, people = f, ending = key, endTick = now, due = NewHeadMath.DueTick(now, Rand.Value), tale = s.ctx.name,
                    who = head, whoName = head.LabelShort, whoLast = (head.Name as NameTriple)?.Last, whoGender = head.gender,
                    whoAge = head.ageTracker?.AgeBiologicalYearsFloat ?? -1f,
                    flags = new List<string> { temper ?? "steady", s.def?.defName == "Council_WS_Claimants" ? "claimed" : "succeeded" }
                };
                FollowUps.Add(FollowUps.HomeOf(s.ctx.map), fu);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the new head: remembered", e);
            }
        }

        /// <summary>The temper the succession reads from traits (kind, greedy, harsh, steady), for a claimant who won.</summary>
        public static string TemperOf(Pawn p)
        {
            var t = p?.story?.traits;
            if (t == null) return "steady";
            var abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
            if (t.HasTrait(TraitDefOf.Kind)) return "kind";
            if (t.HasTrait(TraitDefOf.Bloodlust) || t.HasTrait(TraitDefOf.Psychopath) || abrasive != null && t.HasTrait(abrasive)) return "harsh";
            if (t.HasTrait(TraitDefOf.Greedy)) return "greedy";
            return "steady";
        }

        /// <summary>The one remembered still leads their people, free in the world, and their people are at peace with the colony.</summary>
        public static bool StillHead(FollowUp f)
        {
            var p = f?.who;
            var people = f?.people;
            return p != null && !p.Dead && !p.Discarded && !p.Spawned && people != null && !people.defeated && people.leader == p
                   && !people.HostileTo(Faction.OfPlayer) && WorldSituationWorker.FreeInWorld(p);
        }

        /// <summary>Starts the visit at this home now; null when it can't (no slot, the head gone, nobody can come).</summary>
        public static WorldSituation Start(Map home, FollowUp f)
        {
            var ws = WorldSituations.Instance;
            var def = Def;
            if (ws == null || home == null || def == null || !StillHead(f)) return null;
            SituationWorker_NewHead.pending = f;
            try
            {
                return ws.TryStart(home, d => d == def, false);
            }
            finally
            {
                SituationWorker_NewHead.pending = null;
            }
        }

        // ================= The head as the colony's books know them =================

        /// <summary>The head's notable, when the colony knows them as one.</summary>
        public static Notable NotableOf(Pawn head) => head == null ? null : NotableBook.Instance?.notables.FirstOrDefault(n => n != null && n.pawn == head);

        /// <summary>The head is asked to the colony's next feast with guests (a year's promise on the ledger).</summary>
        public static void Promise(Pawn head)
        {
            if (head == null || Promised(head)) return;
            try
            {
                WorldLedger.AddTie(Tie.Kind.Promise, Refs.Us, WorldLedger.PersonOf(head), new TieTerms { key = PromiseKey }, Now + GenDate.TicksPerYear, PromiseKey);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the new head: asked to the feast", e);
            }
        }

        private static Tie PromiseTo(Pawn head)
        {
            if (head == null) return null;
            var r = WorldLedger.PersonOf(head);
            return WorldLedger.OwnTies(Tie.Kind.Promise).FirstOrDefault(t => t != null && t.Live && t.origin == PromiseKey && t.to == r);
        }

        /// <summary>The colony asked this head to its next feast with guests and hasn't held it yet.</summary>
        public static bool Promised(Pawn head) => PromiseTo(head) != null;

        public static bool Promised(Notable n) => n?.pawn != null && Promised(n.pawn);

        /// <summary>The feast with guests is planned: a head among its guests who was promised it, the promise kept.</summary>
        public static void Invited(IEnumerable<Notable> invited)
        {
            foreach (var n in invited ?? Enumerable.Empty<Notable>())
            {
                var t = PromiseTo(n?.pawn);
                if (t != null) WorldLedger.Set(t, Tie.State.Kept);
            }
        }
    }

    /// <summary>
    /// The new head at the gate as a world event. It never starts by itself: the follow-ups start it (NewHeads.Start).
    /// {X} the head, {A} the colony's best talker (who hosts and speaks), {COUNT} the gift's silver, {FACTION} their people,
    /// {PLACE} their town, {TITLE} their leader's title. Flags: kind, greedy, harsh or steady (and sameCreed), claimed or
    /// succeeded (how they came to the seat), pactTalk (a steady head with goodwill enough for a trade pact), and the
    /// answer's table, gift or word.
    /// </summary>
    public class SituationWorker_NewHead : WorldSituationWorker
    {
        internal static FollowUp pending;

        public override bool CanStart(Map map) => pending != null && base.CanStart(map);

        protected override bool CanHappen(Map map) => pending?.people != null && NewHeads.StillHead(pending) && Visitors.Usable(pending.people);

        private static Pawn Head(WorldSituation s) => s.ctx.stranger;

        private static HeadTemper Temper(WorldSituation s) =>
            s.Has("kind") ? HeadTemper.Kind : s.Has("greedy") ? HeadTemper.Greedy : s.Has("harsh") ? HeadTemper.Harsh : HeadTemper.Steady;

        public override bool Cast(WorldSituation s, Map map)
        {
            var f = pending;
            if (f?.people == null || map == null || !NewHeads.StillHead(f)) return false;
            var ctx = s.ctx;
            ctx.map = map;
            ctx.faction = f.people;
            s.At(NearestTown(f.people, map), f.people.Name);
            ctx.stranger = f.who;
            string temper = f.flags?.FirstOrDefault(x => x == "kind" || x == "greedy" || x == "harsh" || x == "steady") ?? NewHeads.TemperOf(f.who);
            s.flags.Add(temper);
            if (f.flags != null && f.flags.Contains("claimed")) s.flags.Add("claimed");
            else s.flags.Add("succeeded");
            if (ModsConfig.IdeologyActive && f.who.Ideo != null && f.who.Ideo == Faction.OfPlayer.ideos?.PrimaryIdeo) s.flags.Add("sameCreed");
            // The colony's best talker hosts them, or speaks for the colony at the gate.
            ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(SkillDefOf.Social)?.TotallyDisabled == false)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.count = Silver(map, new IntRange(60, 120));
            if (Pacts.On && NewHeadMath.OpensPact(Temper(s), f.people.PlayerGoodwill, Pacts.WhyNot(f.people, map, PactKind.Trade) == null)) s.flags.Add("pactTalk");
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>Their people gone or at war with the colony, or the head gone: it ends without a word.</summary>
        protected override bool StillPossible(WorldSituation s)
        {
            var f = s.ctx.faction;
            var head = Head(s);
            return f != null && !f.defeated && !f.HostileTo(Faction.OfPlayer) && head != null && !head.Dead && !head.Discarded;
        }

        protected override string GoneEnding(WorldSituation s) => WorldSituations.Quietly;

        public override LookTargets StartLook(WorldSituation s)
        {
            var head = Head(s);
            if (head != null && head.Spawned) return new LookTargets(head);
            return base.StartLook(s);
        }

        /// <summary>They came to the gate (the beat's letter is out).</summary>
        public override void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "gate") s.Set("came");
        }

        /// <summary>Time ran out before they ever reached the gate: it ends without a word.</summary>
        public override SituationEnding TimeoutEnding(WorldSituation s) => !s.Has("came") ? null : base.TimeoutEnding(s);

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key != "gate") return outcome;
            // Unanswered, the default answer is played out all the same (the word at the gate), so it counts as given.
            var o = option < beat.options.Count ? beat.options[option] : null;
            string answer = o?.flags?.FirstOrDefault(x => x == "table" || x == "gift" || x == "word") ?? "none";
            var head = Head(s);
            var f = s.ctx.faction;
            var map = ctx.map ?? s.ctx.map;
            var temper = Temper(s);
            try
            {
                // What passed at the gate moves the head's regard; the host's talk counts at the table.
                int social = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                float r = NewHeadMath.Regard(temper, answer, social);
                var n = NewHeads.NotableOf(head);
                if (n != null)
                {
                    n.personal += r;
                    n.Remember(("CC_NewHead_Note_" + answer).Translate().Resolve(), r);
                    if (answer == "table" && KinMemory.Ours(ctx.a)) n.RegardOf(ctx.a, true).personal += NewHeadMath.Personal(social);
                    NotableBook.Changed();
                }
                // Asked to the colony's next feast with guests, once sat at its table or given its gift.
                if (answer == "table" || answer == "gift")
                {
                    NewHeads.Promise(head);
                    s.Set("invited");
                }
                // A kind head leaves a gift of their own, handed over at the gate.
                if (temper == HeadTemper.Kind && map != null && f != null)
                {
                    int silver = Silver(map, new IntRange(80, 160));
                    var gate = PlaySpots.GateCell(map, head);
                    if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
                    Couriers.PaySilver(map, gate, silver, f, head != null && head.Spawned ? head : null);
                    s.SetNum("kindGift", silver);
                    s.Set("kindGift");
                }
                // A steady head opens talk of a trade pact, where goodwill allows: their offer comes as a letter of its own.
                if (s.Has("pactTalk") && answer != "none" && f != null && map != null && Pacts.WhyNot(f, map, PactKind.Trade) == null)
                {
                    Pacts.Offer(f, map, PactKind.Trade, null, null);
                    s.Set("pactOffered");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the new head at the gate", e);
            }
            return outcome;
        }
    }
}
