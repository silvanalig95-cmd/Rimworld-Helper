using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The hamlet as the colony's vassal (Diplomacy/Vassals.cs; docs/design/stage2-diplomacy.md, item 10): once sworn, its
    // head may ask to be released after a season of low regard. Released, it gives back its oath and stands under the
    // colony's protection as before it swore; refused, it breaks away, as a hamlet refused twice in a hard season does.
    public static partial class Hamlets
    {
        /// <summary>The oath given back: the hamlet stands under the colony's protection again, and asks to swear again in time.</summary>
        internal static void ReleaseOath(Map home, HamletHome h)
        {
            if (h == null || h.stage != HamletStage.Sworn) return;
            h.stage = HamletStage.Standing;
            h.oathAt = Now;
            h.highSince = -1;
            var tie = TieOf(h);
            if (tie != null && tie.Live)
            {
                tie.terms.key = "protection";
                WorldLedger.Touch(tie);
            }
        }

        /// <summary>
        /// Release refused: the hamlet breaks away, its tie broken and its record over, as a hamlet whose asks were refused
        /// twice; the letter is the vassals' own (the reason differs), so the hamlet's end is told by them.
        /// </summary>
        internal static void BreakAway(Map home, HamletHome h)
        {
            if (h == null || !HamletMath.Stands(h.stage)) return;
            // No home given: the hamlet's own end letter and history line stay unsaid; Vassals.BreakAway tells it.
            End(null, h, HamletEnd.Broken, null);
        }
    }
}
