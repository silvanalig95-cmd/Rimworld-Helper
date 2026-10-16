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
    /// Good moods add up slowly. Everything good Rimfolk gives a colonist as a mood (a wish granted, a wedding, a
    /// dilemma that went their way, a reunion, a feast) shares one budget, so a pile of them can't carry a colonist's
    /// mood: the two biggest count in full, the next ones a half, a third and a quarter, and every one after that a
    /// fifth (GoodTimesMath.RankFactor). Each stays on the Needs tab at what it counts for now. Every bad memory counts
    /// as it is, and so does everything the game and other mods give (high standards take their own share of those,
    /// GoodTimes). Many small moments give something else instead of mood (RewardInstead), so they never join the
    /// pile. The class keeps its old name, as does the setting (smallJoysDiminish): it began with the small moments only.
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

        /// <summary>The tooltip line for a good mood that counts for less: its share, and what it counts for now.</summary>
        public static string Note(Thought_Memory t, string description)
        {
            float f = Factor(t);
            if (f >= 1f || !(t is ICouncilMood mood)) return description;
            float own = mood.OwnOffset();
            return description + "\n\n" + "CC_SmallJoy_Less".Translate(f.ToStringPercent(), MoodView.Number(own * f), MoodView.Number(own));
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
