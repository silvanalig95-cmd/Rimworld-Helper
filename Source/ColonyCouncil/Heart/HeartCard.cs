using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart on the colonist's card (UI/Cards, the Now slot), both sides at once: a line while strain is strained or
    /// worse, with why ("Worn down: the plague and losing Ana."); a line while the good side is steady, in good heart or in
    /// high spirits, with what filled it ("In good heart: friends close by and a purpose."); and the last release while it's
    /// recent. Nothing for a heart with nothing to speak of on either side.
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
            var h = p == null ? null : Heart.Of(p);
            if (h == null) return null;
            var list = new List<string>(3);
            if (h.Band >= StrainBand.Strained)
            {
                string why = HeartFelt.StrainWhy(h);
                string band = HeartView.BandLabel(h.Band).CapitalizeFirst();
                list.Add(why.NullOrEmpty() ? "CC_Good_Line".Translate(band.Named("BAND"), p.Named("PAWN")).Resolve()
                    : "CC_Good_LineWhy".Translate(band.Named("BAND"), why.Named("WHAT"), p.Named("PAWN")).Resolve());
            }
            string line = GoodHeart.On ? GoodHeart.Line(p, h) : null;
            if (line == null) return list.Count == 0 ? null : list;
            list.Add(line);
            if (!h.releaseWhy.NullOrEmpty() && Find.TickManager.TicksGame - h.lastReleaseTick < GenDate.TicksPerDay * 5)
                list.Add("CC_Good_CardRelease".Translate(h.releaseWhy.Named("WHAT"), GoodHeart.Ago(h.lastReleaseTick).Named("WHEN"), p.Named("PAWN")).Resolve());
            return list;
        }
    }
}
