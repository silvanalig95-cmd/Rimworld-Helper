using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColonyCouncil
{
    /// <summary>
    /// One storyline's pure rules (docs/quests-proposals.md; the frame's shared rules are StorylineMath): its parts in the
    /// order they can come, how each part can end, its endings, and what follows a part's outcome. Each storyline has a class
    /// of its own in this folder, found by the frame (<see cref="StorylineFlows"/>), so a new storyline adds a file here and
    /// touches nothing shared. No game types, so Tools/Tests checks every one of them (the folder is linked whole).
    /// </summary>
    public abstract class StorylineFlow
    {
        /// <summary>Its key ("factor", "sickness"): the StorylineDef's kind, the StorylinePart's story.</summary>
        public abstract string Kind { get; }

        /// <summary>Its number in docs/quests-proposals.md (S1, S2...): the order storylines are listed in.</summary>
        public abstract int Number { get; }

        /// <summary>Every part, in the order they can come, the first first.</summary>
        public abstract string[] Parts { get; }

        /// <summary>Every ending, with declined, gone and faded among them.</summary>
        public abstract string[] Endings { get; }

        /// <summary>The outcomes a part can end with besides declined, gone and ended (the dev menu, the tests).</summary>
        public abstract string[] Outcomes(string part);

        /// <summary>
        /// What follows <paramref name="part"/> ended with <paramref name="outcome"/> (never "gone": the frame ends those
        /// itself). <paramref name="times"/>: how often this part has been played; <paramref name="roll"/> a number in
        /// [0, 1) for what is left to chance. Every part and outcome leads somewhere.
        /// </summary>
        public abstract StorylineMath.Step Next(string part, string outcome, int times, float roll);

        /// <summary>Endings that show a letter of their own (the rest were told by the part that led to them).</summary>
        public virtual bool EndingHasLetter(string ending) => ending == StorylineMath.Gone;

        /// <summary>An outcome the mission ends as failed (the dev menu ends a part with it so).</summary>
        public virtual bool Failed(string outcome) => false;

        public string FirstPart => Parts.Length > 0 ? Parts[0] : null;

        // ---------------- Helpers for the rules ----------------

        protected static StorylineMath.Step To(string part, float min, float max, string flag = null, bool quiet = false) =>
            new StorylineMath.Step { part = part, min = min, max = max, flag = flag, quiet = quiet };

        protected static StorylineMath.Step End(string ending, float min = 0f, float max = 0f, string flag = null) =>
            new StorylineMath.Step { ending = ending, min = min, max = max, flag = flag };

        protected static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        protected static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        protected static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : Math.Max(0f, v);
    }

    /// <summary>Every storyline's rules, found once: each class of <see cref="StorylineFlow"/> in this assembly, by its number.</summary>
    public static class StorylineFlows
    {
        private static StorylineFlow[] all;

        public static StorylineFlow[] All => all ??= Find();

        public static StorylineFlow Of(string kind)
        {
            if (kind == null) return null;
            foreach (var f in All)
                if (f.Kind == kind)
                    return f;
            return null;
        }

        private static StorylineFlow[] Find()
        {
            Type[] types;
            try
            {
                types = typeof(StorylineFlow).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            var list = new List<StorylineFlow>();
            foreach (var t in types)
            {
                if (t == null || t.IsAbstract || !typeof(StorylineFlow).IsAssignableFrom(t) || t.GetConstructor(Type.EmptyTypes) == null) continue;
                if (Activator.CreateInstance(t) is StorylineFlow f && !string.IsNullOrEmpty(f.Kind) && list.All(x => x.Kind != f.Kind)) list.Add(f);
            }
            return list.OrderBy(f => f.Number).ThenBy(f => f.Kind, StringComparer.Ordinal).ToArray();
        }
    }
}
