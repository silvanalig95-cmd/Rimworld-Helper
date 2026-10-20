using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Talks about faith that matter (Faith/, in the conversation system's faith topic). When two colonists talk about
    /// faith, how it went moves their certainty through the core (<see cref="Faiths.Sway"/>, <see cref="Faiths.Steady"/>):
    /// <list type="bullet">
    /// <item>Two of different faiths who agree, or have a friendly debate: the devout one who comes out of it well (in a
    /// debate, by their persuasion: the game's conversion power, how devout, how liked, with a roll) sways the other a
    /// little (<see cref="FaithPeopleMath.AgreeSway"/>, <see cref="FaithPeopleMath.DebateSway"/>), more for the open and
    /// for friends, less for the proud. A believer whose faith frowns on preaching takes it badly: a small opinion of the
    /// preacher, no mood.</item>
    /// <item>A heated argument across faiths sways nobody: both dig in a little.</item>
    /// <item>Two of one faith who agree or debate kindly are both a little steadier.</item>
    /// </list>
    /// One faith move a pair in two days, a sway at most once a day for anyone, a steadying once a day. Worked out before
    /// the talk's lines are picked (<see cref="Plan"/>), so the lines can show it (persuading, persuaded, unmoved,
    /// preachedat, digin, steadied, and the other's faith: otherfaith:devout, otherfaith:wavering), and done after the
    /// talk's own effects (<see cref="Apply"/>). Two lines in Talk.Run and one in the faith topic's tags; nothing
    /// without Faiths.On.
    /// </summary>
    public static class FaithTalk
    {
        private sealed class Planned
        {
            public Conversation c;
            public FaithTalkMove move;
            public Pawn persuader, listener;
            public float baseAmount;
            public bool visiblyMoved, preachedAt;
        }

        private static Planned plan;

        private static int Now => Find.TickManager.TicksGame;

        private static TopicDef FaithTopic => TalkDefs.Topic("Faith");

        private static int Opinion(Pawn of, Pawn about) => of?.relations?.OpinionOf(about) ?? 0;

        private static float Persuasion(Pawn speaker, Pawn other)
        {
            float power;
            try
            {
                power = speaker.GetStatValue(StatDefOf.ConversionPower);
            }
            catch (Exception)
            {
                power = 1f;
            }
            return FaithPeopleMath.Persuasion(power, Faiths.Devout(speaker), Opinion(other, speaker));
        }

        /// <summary>From Talk.Run, once a talk's outcome is known and before its lines: what it will do for their faith.</summary>
        public static void Plan(Conversation c)
        {
            plan = null;
            try
            {
                if (!Faiths.On || c == null || c.kind != TalkKind.Topic || c.topic == null || c.topic != FaithTopic) return;
                var a = c.a;
                var b = c.b;
                if (!Faiths.Holds(a) || !Faiths.Holds(b)) return;
                var tr = FaithPeopleTracker.Instance;
                if (tr == null) return;
                int now = Now;
                if (!tr.PairReady(a, b, now)) return;
                bool same = Faiths.Same(a, b);
                bool friendly = c.friends || c.lovers || Opinion(a, b) >= FaithPeopleMath.FriendlyOpinion || Opinion(b, a) >= FaithPeopleMath.FriendlyOpinion;
                var move = FaithPeopleMath.Move(c.outcome, same, friendly);
                if (move == FaithTalkMove.None) return;
                var p = new Planned { c = c, move = move };
                if (move == FaithTalkMove.Sway)
                {
                    // Who speaks for their faith: in agreement the devout one (the more persuasive, if both are); in a
                    // debate whoever comes out of it well. Only a devout winner sways the other.
                    bool da = Faiths.Devout(a), db = Faiths.Devout(b);
                    if (!da && !db) return;
                    Pawn winner;
                    if (c.outcome == TalkOutcome.Agree) winner = da && db ? (Persuasion(a, b) >= Persuasion(b, a) ? a : b) : da ? a : b;
                    else winner = Rand.Value < FaithPeopleMath.FirstWins(Persuasion(a, b), Persuasion(b, a)) ? a : b;
                    if (!Faiths.Devout(winner)) return;
                    var listener = winner == a ? b : a;
                    var rec = tr.Of(listener, false);
                    if (rec != null && !FaithPeopleMath.Ready(rec.lastSwayed, now, FaithPeopleMath.ListenerGapTicks)) return;
                    p.persuader = winner;
                    p.listener = listener;
                    p.baseAmount = FaithPeopleMath.SwayBase(c.outcome);
                    p.visiblyMoved = FaithPeople.Expected(listener, winner, p.baseAmount) >= FaithPeopleMath.VisiblyMovedAt;
                    p.preachedAt = Faiths.DislikesPreaching(listener);
                }
                plan = p;
            }
            catch (Exception e)
            {
                plan = null;
                CouncilLog.ErrorOnce("faith talk: plan", e);
            }
        }

        /// <summary>
        /// From the faith topic's tags, for each line of the talk: the other's faith as the speaker sees it, whose faith
        /// the talk is about (myfaith, theirfaith), a faith that frowns on preaching (nopreaching), and the part each
        /// plays in what the talk does.
        /// </summary>
        public static void AddTags(Pawn speaker, Pawn other, TalkSubject subject, HashSet<string> tags)
        {
            if (!Faiths.On || tags == null || !Faiths.Holds(speaker) || !Faiths.Holds(other)) return;
            try
            {
                if (Faiths.Devout(other)) tags.Add("otherfaith:devout");
                else if (Faiths.Wavering(other)) tags.Add("otherfaith:wavering");
                if (subject != null && !subject.name.NullOrEmpty()) tags.Add(subject.name == Faiths.Of(speaker)?.name ? "myfaith" : "theirfaith");
                if (Faiths.DislikesPreaching(speaker)) tags.Add("nopreaching");
                var p = plan;
                if (p == null || !(p.c.a == speaker && p.c.b == other || p.c.b == speaker && p.c.a == other)) return;
                switch (p.move)
                {
                    case FaithTalkMove.Sway:
                        if (speaker == p.persuader) tags.Add("persuading");
                        else if (speaker == p.listener)
                        {
                            tags.Add(p.visiblyMoved ? "persuaded" : "unmoved");
                            if (p.preachedAt) tags.Add("preachedat");
                        }
                        break;
                    case FaithTalkMove.DigIn:
                        tags.Add("digin");
                        break;
                    case FaithTalkMove.Steady:
                        tags.Add("steadied");
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith talk: tags", e);
            }
        }

        /// <summary>From Talk.Run, after the talk's own effects: the planned move, if the two still hold their faiths.</summary>
        public static void Apply(Conversation c)
        {
            // together: the talk's own effects (InteractionWorker_Talk.Interacted); the two are face to face.
            var p = plan;
            plan = null;
            if (p == null || p.c != c || !Faiths.On) return;
            try
            {
                var tr = FaithPeopleTracker.Instance;
                if (tr == null) return;
                int now = Now;
                tr.PairMoved(c.a, c.b, now);
                switch (p.move)
                {
                    case FaithTalkMove.Sway:
                    {
                        if (!Faiths.Holds(p.listener) || !Faiths.Holds(p.persuader) || Faiths.Same(p.listener, p.persuader)) return;
                        tr.Of(p.listener, true).lastSwayed = now;
                        string note = "CC_FaithT_NoteSwayed".Translate(p.persuader.Named("OTHER"), (Faiths.Of(p.persuader)?.name ?? "").Named("FAITH")).Resolve();
                        string cause = "CC_FaithT_ConvertCause".Translate(p.persuader.Named("OTHER")).Resolve();
                        FaithPeople.Sway(p.listener, p.persuader, p.baseAmount, cause, note);
                        if (p.preachedAt)
                        {
                            var def = DefRefs.Named<ThoughtDef>("Rimfolk_Faith_PreachedAt");
                            if (def != null) CouncilGameComponent.GiveThought(p.listener, def, p.persuader);
                        }
                        break;
                    }
                    case FaithTalkMove.DigIn:
                        Steady(tr, c.a, FaithPeopleMath.DigIn, now);
                        Steady(tr, c.b, FaithPeopleMath.DigIn, now);
                        break;
                    case FaithTalkMove.Steady:
                        Steady(tr, c.a, FaithPeopleMath.SameFaithSteady, now);
                        Steady(tr, c.b, FaithPeopleMath.SameFaithSteady, now);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith talk: effects", e);
            }
        }

        private static void Steady(FaithPeopleTracker tr, Pawn p, float amount, int now)
        {
            if (!Faiths.Holds(p)) return;
            var r = tr.Of(p, true);
            if (!FaithPeopleMath.Ready(r.lastSteadied, now, FaithPeopleMath.SteadyGapTicks)) return;
            r.lastSteadied = now;
            FaithPeople.Steady(p, amount);
        }
    }
}
