using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum StandardsLevel
    {
        Modest,
        Comfortable,
        Particular,
        Spoiled
    }

    /// <summary>
    /// The hedonic treadmill. Each colonist's standards creep up while they're happy and fall in hard times.
    /// High standards shrink the council's mood rewards, make letdowns sting more, and make wishes come
    /// more often, ask for more and run out of patience sooner. A colony that is always content slowly
    /// becomes a colony that is hard to please.
    /// </summary>
    public static class Standards
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static float Of(Pawn p)
        {
            if (!S.risingStandards || p == null) return 0f;
            return CouncilGameComponent.Instance?.standings.For(p, false)?.standards ?? 0f;
        }

        public static StandardsLevel LevelFor(float v) =>
            v < 0.25f ? StandardsLevel.Modest : v < 0.5f ? StandardsLevel.Comfortable : v < 0.75f ? StandardsLevel.Particular : StandardsLevel.Spoiled;

        public static StandardsLevel LevelOf(Pawn p) => LevelFor(Of(p));

        /// <summary>Council mood effects: rewards shrink by up to half, penalties grow by up to 40%.</summary>
        public static float MoodFactor(Pawn p, bool positive)
        {
            float s = Of(p);
            return positive ? 1f - 0.5f * s : 1f + 0.4f * s;
        }

        /// <summary>Patience for a new wish: the spoiled wait up to 30% less.</summary>
        public static float PatienceFactor(Pawn p) => 1f - 0.3f * Of(p);

        /// <summary>How much more often the colony voices wishes: up to 60% more when everyone is spoiled.</summary>
        public static float WishRateFactor(System.Collections.Generic.IEnumerable<Pawn> pawns)
        {
            float sum = 0f;
            int n = 0;
            foreach (var p in pawns)
            {
                sum += Of(p);
                n++;
            }
            return n == 0 ? 1f : 1f + 0.6f * sum / n;
        }

        /// <summary>Once a day: drift up while happy, down in hard times; decrees can push them either way.</summary>
        public static void Daily(StandingTracker tracker, float decreeShift)
        {
            if (!S.risingStandards) return;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
            {
                if (p.IsQuestLodger() || p.needs?.mood == null || p.DevelopmentalStage.Baby()) continue;
                var st = tracker.For(p);
                float mood = p.needs.mood.CurLevelPercentage;
                float rate;
                if (mood >= 0.7f)
                {
                    rate = (mood >= 0.85f ? 0.06f : 0.04f) * (CouncilGameComponent.Instance?.offices.StandardsFactor ?? 1f);
                    var traits = p.story?.traits;
                    if (traits != null)
                    {
                        if (traits.HasTrait(TraitDefOf.Ascetic)) rate *= 0.3f;
                        if (traits.HasTrait(TraitDefOf.Greedy) || traits.HasTrait(TraitDefOf.Jealous)) rate *= 1.5f;
                    }
                }
                else if (mood < 0.4f) rate = -0.06f;
                else rate = -0.01f;
                rate += decreeShift;
                st.standards = Mathf.Clamp01(st.standards + rate);
            }
        }
    }
}
