using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The review of the colonists' own lives (docs/review-lives.md, the player's pick of 28 November 2026): what the review
    // needed in code beyond its fixes in place. No new system: an effect of the play-out engine's own kind.

    /// <summary>
    /// Friends over the hills (Rimfolk_R4L_Arc_OverTheHills), its second stage: the people the colonist got on with are on
    /// the map again, and the words say they are going over to see them. They walk over to one of that people (the one they
    /// knew first, if here) and spend an hour with them, a visit of the play-out engine; the story's own gate waits for the
    /// two near each other, as before. Nobody of that people here, or play-outs off: nothing is set going, and the gate
    /// waits as it did.
    /// </summary>
    public class LivesReviewEffect_SeeTheirPeople : DilemmaEffect_Visit
    {
        public LivesReviewEffect_SeeTheirPeople()
        {
            who = PlayWho.A;
            to = PlaySpot.Stranger;
            hours = new IntRange(7, 21);
            duration = new FloatRange(1f, 1.5f);
        }

        protected override PlayOut Create(DilemmaContext ctx)
        {
            var arc = (ctx as ArcEffectContext)?.arc;
            var p = ctx?.a;
            if (p == null || !p.Spawned || ctx.faction == null) return null;
            ctx.stranger = Round4Lives.PeopleHere(p.Map, ctx.faction)
                .OrderByDescending(x => arc != null && x.ThingID == arc.key)
                .ThenBy(x => x.Position.DistanceToSquared(p.Position))
                .FirstOrDefault();
            return ctx.stranger == null ? null : base.Create(ctx);
        }
    }
}
