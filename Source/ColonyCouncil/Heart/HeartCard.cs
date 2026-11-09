using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart on the colonist's card (UI/Cards, the Now slot), both sides at once: a line while strain is strained or
    /// worse, with why ("Worn down: the plague and losing Ana."), what the band does to them now and time off under way; a
    /// line while the good side is steady, in good heart or in high spirits, with what filled it ("In good heart: friends
    /// close by and a purpose."); and the last release while it's recent. Under them, what would help with its buttons
    /// (Heart/HeartAdvice.cs), and the day off among the card's acts at its foot (Heart/HeartTimeOff.cs). Nothing for a
    /// heart with nothing to speak of on either side.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HeartCard
    {
        static HeartCard()
        {
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 20f, Lines, null, "heart-good");
            CardSections.Register(CardKind.Colonist, CardSlot.Now, 20.5f, Advice, null, "heart-advice");
            CardActs.Register(CardKind.Colonist, DayOffLabel, DayOffWhyNot, r =>
            {
                var p = ThingRefs.Pawn(r);
                if (p != null) HeartTimeOff.DayOffButton(p);
            }, DayOffShows, 45f, "CC_Count_DayOff_CardTip".Translate().Resolve(), "heart-dayoff");
        }

        private static List<string> Lines(ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            var h = p == null ? null : Heart.Of(p);
            if (h == null) return null;
            var list = new List<string>(5);
            if (h.Band >= StrainBand.Strained)
            {
                string why = HeartFelt.StrainWhy(h);
                string band = HeartView.BandLabel(h.Band).CapitalizeFirst();
                list.Add(why.NullOrEmpty() ? "CC_Good_Line".Translate(band.Named("BAND"), p.Named("PAWN")).Resolve()
                    : "CC_Good_LineWhy".Translate(band.Named("BAND"), why.Named("WHAT"), p.Named("PAWN")).Resolve());
                string doing = HeartEffects.Doing(h, false);
                if (doing != null) list.Add("CC_Count_CardDoing".Translate(doing.Named("WHAT")).Resolve());
            }
            string off = HeartTimeOff.Line(p, h, Find.TickManager.TicksGame);
            if (off != null) list.Add(off);
            string line = GoodHeart.On ? GoodHeart.Line(p, h) : null;
            if (line == null) return list.Count == 0 ? null : list;
            list.Add(line);
            if (!h.releaseWhy.NullOrEmpty() && Find.TickManager.TicksGame - h.lastReleaseTick < GenDate.TicksPerDay * 5)
                list.Add("CC_Good_CardRelease".Translate(h.releaseWhy.Named("WHAT"), GoodHeart.Ago(h.lastReleaseTick).Named("WHEN"), p.Named("PAWN")).Resolve());
            return list;
        }

        /// <summary>What would help them, each with its button where Rimfolk can act (built hourly, Heart/HeartAdvice.cs).</summary>
        private static float Advice(Rect rect, ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            var h = p == null ? null : Heart.Of(p);
            var items = h == null ? null : HeartAdvice.For(p, h);
            if (items == null || items.Count == 0) return 0f;
            float y = rect.y;
            UiKit.LabelFit(new Rect(rect.x, y, rect.width, 18f), "CC_Advice_Head".Translate(p.Named("PAWN")).Resolve(), MoodView.AdviceColor, GameFont.Tiny);
            y += 18f;
            y += HeartAdvice.Draw(p, items, rect.x, y, rect.width, 3);
            return y - rect.y + 2f;
        }

        private static bool DayOffShows(ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            var h = p == null || !HeartTimeOff.DayOffOn ? null : Heart.Of(p);
            return h != null && p.IsColonist && (h.strain >= HeartCountMath.DayOffFrom || h.Off(TimeOffKind.DayOff));
        }

        private static string DayOffLabel(ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            return p == null ? "" : HeartTimeOff.DayOffLabel(p, Heart.Of(p), Find.TickManager.TicksGame);
        }

        private static string DayOffWhyNot(ThingRef r)
        {
            var p = ThingRefs.Pawn(r);
            var h = p == null ? null : Heart.Of(p);
            if (h != null && h.Off(TimeOffKind.DayOff)) return null;
            return p == null ? "" : HeartTimeOff.WhyNotDayOff(p, h, Find.TickManager.TicksGame);
        }
    }
}
