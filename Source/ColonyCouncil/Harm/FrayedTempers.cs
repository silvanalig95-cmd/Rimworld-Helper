using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Frayed tempers (the harm review's 3.3.1): worn down, a sharp word comes to blows a quarter more often, near breaking
    /// half again (a beer's, never more), in Rimfolk's own moments that can end in the game's social fight: a near fight
    /// and the other moments with a fight's chance (DilemmaEffect_Fight), hard words with an outsider, the hustings'
    /// heckle, a camp's quarrel. Never the game's own insults and never the break picker; no mood. The part of a fight
    /// only the strain made goes through the harm service (the cap for both, Harm.Fight's record) and is told with the
    /// strain behind it; the heart's page says so in the band's own words. Pure part: HarmTwoMath.FrayedFactor.
    /// </summary>
    public static class FrayedTempers
    {
        /// <summary>With the heart and hurts from everyday life on.</summary>
        public static bool On => Harm.On && Heart.On;

        /// <summary>How much likelier a fight is for them now (1: no likelier).</summary>
        public static float Factor(Pawn p)
        {
            if (!On || p == null || !Heart.Eligible(p)) return 1f;
            return HarmTwoMath.FrayedFactor(Heart.BandOf(p), Harm.ScaleFor(HarmKinds.Frayed));
        }

        /// <summary>The moment's factor: the more frayed of the two.</summary>
        public static float Factor(Pawn a, Pawn b) => HarmTwoMath.Frayed(Factor(a), Factor(b));

        /// <summary>The band's line on the heart's page adds a short fuse where it has one.</summary>
        public static string BandNote(StrainBand band) =>
            On && HarmTwoMath.FrayedFactor(band, Harm.Scale) > 1.001f ? " " + "CC_HarmTwo_FrayedBand".Translate().Resolve() : "";

        /// <summary>
        /// A moment's fight chance rolled with a frayed temper between <paramref name="a"/> and <paramref name="b"/>: true
        /// for a fight. <paramref name="frayed"/>: only the strain made it one, and the cap lets both be hurt (if it
        /// doesn't, that part doesn't come).
        /// </summary>
        public static bool Roll(Pawn a, Pawn b, float chance, out bool frayed)
        {
            frayed = false;
            float f = Factor(a, b);
            if (f <= 1.001f) return Rand.Chance(chance);
            bool fight = HarmTwoMath.FrayedRoll(Rand.Value, chance, f, out frayed);
            if (frayed && (!Harm.May(a, HurtClass.Fight) || b != null && !Harm.May(b, HurtClass.Fight)))
            {
                frayed = false;
                return false;
            }
            return fight;
        }

        /// <summary>The fight only the strain brought about has begun: counted for the cap and told, the strain behind it named.</summary>
        public static void Began(Pawn a, Pawn b)
        {
            try
            {
                if (a == null || b == null) return;
                Harm.Note(a, HarmKinds.Frayed);
                Harm.Note(b, HarmKinds.Frayed);
                // The one worn down the more is named.
                var who = Factor(b) > Factor(a) ? b : a;
                var other = who == a ? b : a;
                string band = HeartView.BandLabel(Heart.BandOf(who));
                string text = TextVariants.Vary("CC_HarmTwo_Frayed").Translate(who.Named("PAWN"), other.Named("OTHER"), band.Named("BAND")).Resolve();
                Tell.Message(NewsKind.Colony, Importance.Important, who.MapHeld, text, new LookTargets(new[] { who, other }), MessageTypeDefOf.NegativeEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: frayed tempers", e);
            }
        }
    }
}
