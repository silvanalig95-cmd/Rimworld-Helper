using System;

namespace ColonyCouncil
{
    /// <summary>What kind of small town a town is, for its map (decision 4).</summary>
    public enum SmallTown : byte
    {
        /// <summary>An ordinary town: its map as the game (or a base generation mod) makes it.</summary>
        None,
        /// <summary>A hamlet founded under the colony's protection.</summary>
        Hamlet,
        /// <summary>A trading post.</summary>
        Post,
        /// <summary>A town in its first year.</summary>
        NewTown
    }

    /// <summary>
    /// Small towns look small (docs/design/stage1-region.md, decision 4): when the map of a hamlet, a trading post or a
    /// town in its first year is made (attacked or entered), its base is smaller and fewer defend it, so what the words call
    /// a hamlet is a hamlet. The base's ground is shrunk around its middle by a factor (never under <see cref="MinSide"/>
    /// cells a side), and the defenders' points and the stockpile's worth by theirs. Whoever builds the base (the game, or
    /// Vanilla Base Generation Expanded's layouts) builds it inside the smaller ground. Pure: Tools/Tests checks it.
    /// </summary>
    public static class SmallTownsMath
    {
        /// <summary>A side is never shrunk under this many cells (a hamlet still has room for a few houses).</summary>
        public const int MinSide = 18;

        /// <summary>The kind of small town a town of the ledger is: its record's kind, or new in its first year.</summary>
        public static SmallTown Of(TownKind kind, bool isNew)
        {
            switch (kind)
            {
                case TownKind.Hamlet: return SmallTown.Hamlet;
                case TownKind.Post: return SmallTown.Post;
                case TownKind.Town: return isNew ? SmallTown.NewTown : SmallTown.None;
                default: return SmallTown.None;
            }
        }

        /// <summary>How much of a side of the base's ground is kept: a hamlet 0.6, a post 0.75, a new town 0.8.</summary>
        public static float SizeFactor(SmallTown t)
        {
            switch (t)
            {
                case SmallTown.Hamlet: return 0.6f;
                case SmallTown.Post: return 0.75f;
                case SmallTown.NewTown: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>How many defend it, as a share of a town's points: a hamlet 0.35, a post 0.5, a new town 0.65.</summary>
        public static float PointsFactor(SmallTown t)
        {
            switch (t)
            {
                case SmallTown.Hamlet: return 0.35f;
                case SmallTown.Post: return 0.5f;
                case SmallTown.NewTown: return 0.65f;
                default: return 1f;
            }
        }

        /// <summary>What its stores hold, as a share of a town's: a hamlet 0.4, a post 0.75 (it is there to trade), a new town 0.6.</summary>
        public static float LootFactor(SmallTown t)
        {
            switch (t)
            {
                case SmallTown.Hamlet: return 0.4f;
                case SmallTown.Post: return 0.75f;
                case SmallTown.NewTown: return 0.6f;
                default: return 1f;
            }
        }

        /// <summary>A hamlet has no walls or barricades round its edge; a post and a new town keep what the builder gives them.</summary>
        public static bool NoEdgeDefense(SmallTown t) => t == SmallTown.Hamlet;

        /// <summary>A side of the base's ground, shrunk by the factor, never under <see cref="MinSide"/> (nor above what it was).</summary>
        public static int Side(int side, float factor)
        {
            if (side <= MinSide || factor >= 1f) return side;
            return Math.Max(MinSide, Math.Min(side, (int)Math.Round(side * factor)));
        }

        /// <summary>Where a shrunk side starts, so the smaller ground keeps the middle of the old.</summary>
        public static int Start(int min, int side, int newSide) => min + (side - newSide) / 2;

        /// <summary>The defenders' points for a small town.</summary>
        public static float Points(float points, SmallTown t) => Math.Max(0f, points) * PointsFactor(t);
    }
}
