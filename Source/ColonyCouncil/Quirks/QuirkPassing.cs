using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Habits pass between people who spend their days together (the arithmetic is <see cref="QuirkPassMath"/>): once a
    /// day, a colonist may pick up a habit (QuirkDef.passes) of someone close to them on the same map: a child from a
    /// parent, a student from their mentor, a lover, family by blood, a good friend; the closer, the likelier. One that
    /// comes only from its work passes only to someone who does a fair share of that work too, and nothing passes that
    /// doesn't suit them (its own who). It's theirs from then on like any other: it lasts, deepens or fades by how they
    /// live it, and its line says whom they had it from. At most one a month each, and never past their two.
    /// </summary>
    public static class QuirkPassing
    {
        public static void Daily(QuirkTracker t, CouncilGameComponent comp)
        {
            try
            {
                int now = Find.TickManager.TicksGame;
                foreach (var learner in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                {
                    if (!Eligible(learner) || t.Of(learner).Count() >= QuirkTracker.MaxPerPawn) continue;
                    if (t.Of(learner).Any(q => q.cause == QuirkCause.Passed && now - q.gainedTick < QuirkPassMath.RestDays * GenDate.TicksPerDay)) continue;
                    TryPickUp(t, comp, learner);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("habits passed on", e);
            }
        }

        private static bool Eligible(Pawn p) =>
            p != null && p.Spawned && !p.Dead && People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        private static void TryPickUp(QuirkTracker t, CouncilGameComponent comp, Pawn learner)
        {
            foreach (var teacher in learner.Map.mapPawns.FreeColonistsSpawned.InRandomOrder().ToList())
            {
                if (teacher == learner || !Eligible(teacher)) continue;
                var bond = BondOf(comp, learner, teacher);
                if (bond == QuirkPassMath.Bond.None) continue;
                foreach (var q in t.Of(teacher).ToList())
                {
                    var d = q?.def;
                    if (d == null || !d.passes || t.Get(learner, d) != null) continue;
                    // Anyone it suits, a child too (the child-only and adult-only ones still hold).
                    if (!DilemmaRole.Fits(d.who, learner, true)) continue;
                    if (d.onlyFromPractice && d.Practised && !QuirkPassMath.AtTheWork(Practice.Hours(learner, d.practice), d.practiceHours)) continue;
                    if (!Rand.Chance(QuirkPassMath.Chance(bond, q.Deep))) continue;
                    t.Gain(comp, learner, d, QuirkCause.Passed, teacher);
                    return;
                }
            }
        }

        /// <summary>The closest way they're bound, or None (not close enough to pick anything up from them).</summary>
        public static QuirkPassMath.Bond BondOf(CouncilGameComponent comp, Pawn learner, Pawn teacher)
        {
            var rel = learner.relations;
            if (rel == null) return QuirkPassMath.Bond.None;
            int opinion = rel.OpinionOf(teacher);
            QuirkPassMath.Bond bond;
            if (learner.DevelopmentalStage.Juvenile() && rel.DirectRelationExists(PawnRelationDefOf.Parent, teacher)) bond = QuirkPassMath.Bond.Parent;
            else if (comp?.social?.Get(teacher, learner, TieKind.Mentor) is SocialTie m && m.a == teacher) bond = QuirkPassMath.Bond.Mentor;
            else if (LovePartnerRelationUtility.LovePartnerRelationExists(learner, teacher)) bond = QuirkPassMath.Bond.Lover;
            else if (rel.FamilyByBlood.Contains(teacher)) bond = QuirkPassMath.Bond.Family;
            else if (comp?.social?.Get(learner, teacher, TieKind.Friends) != null || opinion >= 60) bond = QuirkPassMath.Bond.Friend;
            else return QuirkPassMath.Bond.None;
            return opinion >= QuirkPassMath.MinOpinion(bond) ? bond : QuirkPassMath.Bond.None;
        }
    }
}
