using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A match the colonist is against (docs/design/stage2-diplomacy.md, item 6: "a match pressed on a colonist who doesn't
    /// want it comes to the council with their objection"): once their people have said yes, the colonist has their say
    /// before the council (Rimfolk_DlmMatch_Against, as a guest to be taken in comes with the one who objects). Pressed all
    /// the same, the match is agreed and it weighs on them (the council's thought, their trust, and their nature once round
    /// d switches it on); withdrawn, their people feel turned down a little. Asked when no other question of the council's is
    /// open at the home, else the hour after.
    /// </summary>
    public static class MatchObjection
    {
        /// <summary>The match the council is being asked about, while the question is cast.</summary>
        internal static MatchPlan asking;

        /// <summary>The council's question about this match is still open in the letters.</summary>
        public static bool Open(int plan)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && d.ctx is MatchContext c && c.plan == plan)
                    return true;
            return false;
        }

        /// <summary>Another question of the council's for this home still open in the letters.</summary>
        private static bool CouncilBusy(Map home)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && (d.ctx?.map == null || d.ctx.map == home || !Homes.IsHome(d.ctx.map)))
                    return true;
            return false;
        }

        /// <summary>The colonist's objection comes before the council now, or the hour after (another question first).</summary>
        public static void Ask(DiplomacyHome h, MatchPlan p)
        {
            var home = h?.Map;
            if (p == null || home == null || p.stage != MatchStage.Asking || Open(p.id) || CouncilBusy(home)) return;
            asking = p;
            try
            {
                var tracker = DilemmaTracker.For(home) ?? CouncilGameComponent.Instance?.dilemmas;
                tracker?.TryFire(true, d => d.Worker is DilemmaWorker_MatchAgainst);
            }
            finally
            {
                asking = null;
            }
        }

        /// <summary>The hour (a match waiting on the council): asked when it can be; the colonist now glad or willing (feelings change), agreed.</summary>
        public static void Hour(DiplomacyHome h, MatchPlan p)
        {
            if (Open(p.id)) return;
            if (Matches.ConsentOf(p.ours, p.theirs, p.lives, out _, out _) != Consent.Unwilling)
            {
                Matches.Agree(h, p);
                return;
            }
            Ask(h, p);
        }
    }

    /// <summary>The objection's question: A the colonist, X the one of theirs, B whom A loves instead; {LIVES} where they would live, {TOWN} theirs.</summary>
    public class MatchContext : DilemmaContext
    {
        public int plan = -1;
        public string lives, town, reason;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((lives ?? "").Named("LIVES"));
            args.Add((town ?? "").Named("TOWN"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref plan, "plan", -1);
            Scribe_Values.Look(ref lives, "lives");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref reason, "reason");
        }
    }

    /// <summary>
    /// Rimfolk_DlmMatch_Against: only from MatchObjection.Ask, once their people said yes. Tags, why the colonist is against
    /// it: "another" (they love B), "children" (they would leave their children), "dislike" (they can't stand X); "away"
    /// (they would go to live at X's town), "empire" (X is of the Empire).
    /// </summary>
    public class DilemmaWorker_MatchAgainst : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var p = MatchObjection.asking;
            if (p?.ours == null || p.theirs == null) return false;
            Matches.ConsentOf(p.ours, p.theirs, p.lives, out var why, out var other);
            ctx = new MatchContext
            {
                def = def, map = map, a = p.ours, b = other ?? p.ours, stranger = p.theirs, faction = p.people, plan = p.id, reason = why.ToString().ToLowerInvariant(),
                town = p.townName ?? MatchWords.People(p.people), lives = MatchWords.Lives(p.lives, p.ours.LabelShort, p.theirs.LabelShort, p.townName)
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is MatchContext c)) return;
            if (!c.reason.NullOrEmpty() && c.reason != "none") tags.Add(c.reason);
            var p = Matches.Get(c.plan, out _);
            if (p != null && p.lives == MatchHome.Theirs) tags.Add("away");
            if (Ages.IsEmpire(c.faction)) tags.Add("empire");
        }
    }

    /// <summary>The match is pressed over the objection: agreed, and it weighs on the colonist.</summary>
    public class DilemmaEffect_MatchPress : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is MatchContext c) || c.a == null) return;
            sheet.Play(MatchWords.P("CC_Impact_MatchPress", c.a.Named("OURS"), (c.stranger?.LabelShort ?? "?").Named("THEIRS")), MatchWords.P("CC_Impact_MatchPressBrief"));
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var p = ctx is MatchContext c ? Matches.Get(c.plan, out _) : null;
            return p == null || p.stage != MatchStage.Asking ? MatchWords.P("CC_Match_Why_Over") : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer; the wedding or their kin's coming plays out on the map on its day.
            if (ctx is MatchContext c) Matches.Pressed(c.plan);
        }
    }

    /// <summary>The match is withdrawn: the colonist was heard, and their people feel turned down a little.</summary>
    public class DilemmaEffect_MatchWithdraw : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is MatchContext c) || c.faction == null) return;
            sheet.Add(ImpactKind.People, -1, MatchWords.P("CC_Impact_MatchWithdraw", MatchWords.People(c.faction).Named("PEOPLE")), MatchWords.P("CC_Impact_MatchWithdrawBrief"));
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer, sent to their people by word.
            if (ctx is MatchContext c) Matches.Withdrawn(c.plan);
        }
    }
}
