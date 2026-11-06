using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart's good side on the colonist's card (UI/Cards, the Now slot): one line while they're in good heart or in
    /// high spirits, with what filled it ("In good heart: friends close by and a purpose."), and the last release while
    /// it's recent. Nothing while steady, on strain's side, or with the good side off.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HeartCard
    {
        static HeartCard()
        {
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 20f, Lines, null, "heart-good");
        }

        private static List<string> Lines(ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            var h = p == null || !GoodHeart.On ? null : Heart.Of(p);
            if (h == null) return null;
            string line = GoodHeart.Line(p, h);
            if (line == null) return null;
            var list = new List<string> { line };
            if (!h.releaseWhy.NullOrEmpty() && Find.TickManager.TicksGame - h.lastReleaseTick < GenDate.TicksPerDay * 5)
                list.Add("CC_Good_CardRelease".Translate(h.releaseWhy.Named("WHAT"), GoodHeart.Ago(h.lastReleaseTick).Named("WHEN"), p.Named("PAWN")).Resolve());
            return list;
        }
    }
}
