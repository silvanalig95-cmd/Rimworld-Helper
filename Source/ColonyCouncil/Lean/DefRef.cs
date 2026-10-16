using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A def looked up by name the first time it's asked for, and kept: for a def that may not be there (another mod's
    /// trait, a DLC's thing), asked for in a pass that runs often. A def that isn't there stays null without being looked
    /// up again. Defs don't change while a game is played; a dev-mode reload of the defs is the one exception, and
    /// <see cref="ResetAll"/> covers it.
    /// <code>private static readonly DefRef&lt;TraitDef&gt; NightOwl = new DefRef&lt;TraitDef&gt;("NightOwl");
    /// ... if (NightOwl.Def != null &amp;&amp; traits.HasTrait(NightOwl.Def)) ...</code>
    /// </summary>
    public sealed class DefRef<T> where T : Def
    {
        public readonly string name;
        private T def;
        private int lookedAt = -1;

        public DefRef(string name)
        {
            this.name = name;
        }

        /// <summary>The def, or null if no loaded mod has it.</summary>
        public T Def
        {
            get
            {
                if (lookedAt != DefRefs.Generation)
                {
                    def = name.NullOrEmpty() ? null : DefDatabase<T>.GetNamedSilentFail(name);
                    lookedAt = DefRefs.Generation;
                }
                return def;
            }
        }

        public static implicit operator T(DefRef<T> r) => r?.Def;
    }

    /// <summary>Defs looked up by a name that isn't known in advance (a def's own field), each once and kept, misses too.</summary>
    public static class DefRefs
    {
        /// <summary>Counts up when every kept def must be looked up again.</summary>
        public static int Generation;

        private static class ByName<T> where T : Def
        {
            public static readonly Dictionary<string, T> kept = new Dictionary<string, T>();
            public static int generation = -1;
        }

        /// <summary>The def of this type with this name, or null; looked up once per name.</summary>
        public static T Named<T>(string name) where T : Def
        {
            if (name.NullOrEmpty()) return null;
            var kept = ByName<T>.kept;
            if (ByName<T>.generation != Generation)
            {
                kept.Clear();
                ByName<T>.generation = Generation;
            }
            if (!kept.TryGetValue(name, out var def))
            {
                def = DefDatabase<T>.GetNamedSilentFail(name);
                if (kept.Count < 4000) kept[name] = def;
            }
            return def;
        }

        /// <summary>Every kept def is looked up again on its next use (after the defs were reloaded).</summary>
        public static void ResetAll() => Generation++;
    }
}
