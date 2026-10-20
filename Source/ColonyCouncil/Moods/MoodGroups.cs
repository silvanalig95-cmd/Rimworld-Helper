using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Which group a mood goes in (MoodSortMath has the rules) and its short line: the hint's short action, or for
    /// moods without one, what kind of mood it is ("While it lasts", "Fades in 2 days", "Their faith", "Lasting").
    /// </summary>
    public static class MoodGroups
    {
        /// <summary>Every thought a precept gives (its comps), found once per game load; a def missing here still counts by its thought's precept.</summary>
        private static HashSet<ThoughtDef> fromPrecepts;

        public static void ResetCache() => fromPrecepts = null;

        /// <summary>Whether a mood comes from their faith: the thought names its precept, or a precept gives that kind of thought.</summary>
        public static bool FromFaith(Thought t, ThoughtDef def)
        {
            try
            {
                if (t?.sourcePrecept != null) return true;
                if (def == null) return false;
                if (fromPrecepts == null)
                {
                    var set = new HashSet<ThoughtDef>();
                    foreach (var pr in DefDatabase<PreceptDef>.AllDefsListForReading)
                    {
                        if (pr?.comps == null) continue;
                        foreach (var comp in pr.comps)
                            if (comp is PreceptComp_Thought pt && pt.thought != null) set.Add(pt.thought);
                    }
                    fromPrecepts = set;
                }
                return fromPrecepts.Contains(def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood from faith", e);
                fromPrecepts ??= new HashSet<ThoughtDef>();
                return false;
            }
        }

        /// <summary>Whether a mood comes from who they are: only colonists with certain traits or genes have it.</summary>
        public static bool FromNature(ThoughtDef def) => def != null && (!def.requiredTraits.NullOrEmpty() || !def.requiredGenes.NullOrEmpty());

        public static HintAction ActionOf(MoodHintDef hint) =>
            hint == null || hint.fix.NullOrEmpty() ? HintAction.None : hint.Actionable ? HintAction.Actionable : HintAction.Fades;

        public static MoodGroup GroupOf(MoodLine l)
        {
            var hint = l.mood < 0f ? MoodInsight.HintFor(l.def) : null;
            return MoodSortMath.GroupOf(l.mood, l.ticksLeft, FromFaith(l.thought, l.def), FromNature(l.def), ActionOf(hint));
        }

        /// <summary>A kind of mood across the colony: grouped by its biggest case, fading when the last of them does.</summary>
        public static MoodGroup GroupOf(ColonyMoodLine c, out int ticksLeft)
        {
            ticksLeft = TicksLeft(c);
            bool faith = false;
            foreach (var l in c.lines)
                if (FromFaith(l.thought, l.def))
                {
                    faith = true;
                    break;
                }
            var hint = c.good ? null : MoodInsight.HintFor(c.def);
            return MoodSortMath.GroupOf(c.good ? Math.Max(0.01f, c.total) : Math.Min(-0.01f, c.total), ticksLeft, faith, FromNature(c.def), ActionOf(hint));
        }

        /// <summary>For a kind of mood across the colony: never fading if one of them never does, lasting while its cause does if one does, otherwise the latest.</summary>
        public static int TicksLeft(ColonyMoodLine c)
        {
            int left = -3;
            foreach (var l in c.lines)
            {
                if (l.ticksLeft == MoodSortMath.Permanent) return MoodSortMath.Permanent;
                if (l.ticksLeft == MoodSortMath.WhileCause) left = MoodSortMath.WhileCause;
                else if (left != MoodSortMath.WhileCause) left = Math.Max(left, l.ticksLeft);
            }
            return left == -3 ? MoodSortMath.WhileCause : left;
        }

        /// <summary>
        /// The short line for a mood in its group: its hint's short action where that's what the row shows, otherwise when
        /// it ends or what kind of mood it is. <paramref name="gold"/>: written in the accent colour (something to do).
        /// </summary>
        public static string Right(MoodGroup g, ThoughtDef def, float mood, int ticksLeft, bool faith, out bool gold)
        {
            var hint = mood < 0f ? MoodInsight.HintFor(def) : null;
            string act = hint != null && !hint.fix.NullOrEmpty() ? hint.act : null;
            bool showsAct = MoodSortMath.ShowsAct(g, !act.NullOrEmpty(), ticksLeft);
            gold = MoodSortMath.Gold(g, showsAct);
            if (showsAct) return act;
            // Only the third group says where a mood comes from; elsewhere it's when it ends.
            bool third = g == MoodGroup.Faith;
            return Fallback(MoodSortMath.FallbackOf(ticksLeft, third && faith, third && FromNature(def)), ticksLeft);
        }

        public static string Right(MoodLine l, MoodGroup g, out bool gold) =>
            Right(g, l.def, l.mood, l.ticksLeft, g == MoodGroup.Faith && FromFaith(l.thought, l.def), out gold);

        public static string Fallback(MoodFallback f, int ticksLeft)
        {
            switch (f)
            {
                case MoodFallback.Fades: return "CC_Mood_FadesIn".Translate(TextUtil.DaysString(Math.Max(1, ticksLeft))).Resolve();
                case MoodFallback.Faith: return "CC_MoodC_Faith".Translate().Resolve();
                case MoodFallback.Nature: return "CC_MoodC_Nature".Translate().Resolve();
                case MoodFallback.Lasting: return "CC_MoodC_Lasting".Translate().Resolve();
                default: return "CC_MoodC_WhileItLasts".Translate().Resolve();
            }
        }
    }
}
