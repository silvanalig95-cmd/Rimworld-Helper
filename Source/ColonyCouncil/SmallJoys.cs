using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mod's memories: the mood each gives on its own, before good moods are weighed against each other. Every
    /// memory class in Rimfolk's defs implements it (Tools/Tests checks), so every good memory shares the budget.
    /// </summary>
    public interface ICouncilMood
    {
        float OwnOffset();

        /// <summary>Set when the council's scale (reward strength, standards) was already applied as it was given.</summary>
        bool Scaled { set; }
    }

    /// <summary>
    /// Good moods add up slowly. Everything good Rimfolk gives a colonist (a wish granted, a wedding, a laugh, a good
    /// talk, a contest won, an animal asleep on someone's feet, a sweet dream) shares one budget, so a pile of them
    /// can't carry a colonist's mood: the two biggest count in full, each one after that half as much as the one
    /// before (GoodTimesMath.RankFactor). Every bad memory counts as it is, and so does everything the game and other
    /// mods give (high standards take their own share of those, GoodTimes). The class keeps its old name, as does the
    /// setting (smallJoysDiminish): it began with the small moments only.
    /// </summary>
    public static class SmallJoys
    {
        private static Pawn cachedPawn;
        private static int cachedTick = -1;
        private static int cachedCount = -1;
        private static readonly Dictionary<Thought_Memory, float> factors = new Dictionary<Thought_Memory, float>();
        private static readonly List<(Thought_Memory thought, float own, int index)> joys = new List<(Thought_Memory, float, int)>();

        /// <summary>How much of its own mood this memory gives right now (1 unless it's one of many good moods).</summary>
        public static float Factor(Thought_Memory t)
        {
            if (GoodTimes.Raw || !ColonyCouncilMod.Settings.smallJoysDiminish || t?.pawn == null) return 1f;
            var memories = t.pawn.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return 1f;
            int tick = Find.TickManager?.TicksGame ?? 0;
            // The game adds up one colonist's thoughts in a row, so one colonist's ranking is kept for the tick.
            if (cachedPawn != t.pawn || cachedTick != tick || cachedCount != memories.Count) Rank(t.pawn, memories, tick);
            return factors.TryGetValue(t, out float f) ? f : 1f;
        }

        /// <summary>
        /// A good mood that now counts for less than half a point: the Needs tab would show it as 0, so it's left off
        /// the list (its little bit still counts toward mood).
        /// </summary>
        public static bool Faded(Thought_Memory t)
        {
            if (!(t is ICouncilMood mood)) return false;
            float f = Factor(t);
            if (f >= 1f) return false;
            float own = mood.OwnOffset();
            return own > 0f && own * f < 0.5f;
        }

        /// <summary>The tooltip line for a good mood that counts for less.</summary>
        public static string Note(Thought_Memory t, string description)
        {
            float f = Factor(t);
            return f >= 1f ? description : description + "\n\n" + "CC_SmallJoy_Less".Translate(f.ToStringPercent());
        }

        private static void Rank(Pawn pawn, List<Thought_Memory> memories, int tick)
        {
            cachedPawn = pawn;
            cachedTick = tick;
            cachedCount = memories.Count;
            factors.Clear();
            joys.Clear();
            for (int i = 0; i < memories.Count; i++)
            {
                if (!(memories[i] is ICouncilMood mood)) continue;
                float own = mood.OwnOffset();
                if (own > 0f) joys.Add((memories[i], own, i));
            }
            if (joys.Count > 2)
            {
                joys.Sort((a, b) => a.own != b.own ? b.own.CompareTo(a.own) : a.index.CompareTo(b.index));
                for (int r = 2; r < joys.Count; r++) factors[joys[r].thought] = GoodTimesMath.RankFactor(r);
            }
            joys.Clear();
        }

        /// <summary>
        /// The council's scale for a good memory that didn't go through the council (a moment in conversation): the
        /// reward strength setting and the colonist's standards, as for everything the council gives.
        /// </summary>
        public static float LiveScale(Pawn p) => ColonyCouncilMod.Settings.rewardFactor * Standards.MoodFactor(p, true);
    }
}
