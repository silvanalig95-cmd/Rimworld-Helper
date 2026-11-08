using System;

namespace ColonyCouncil
{
    /// <summary>What a camper means to take or clear from the land.</summary>
    public enum TakeWhat
    {
        Tree,
        Plant,
        Animal,
        Mineable,
        Item,
        Building
    }

    /// <summary>What there is to know about a thing before a camper takes it (read off the map by CampTake).</summary>
    public struct TakeFacts
    {
        public TakeWhat what;
        /// <summary>The camp's own: their goods, or what their own work left (the logs of a tree they felled, a beast they killed, a chunk they mined).</summary>
        public bool theirs;
        /// <summary>The colony's: its building, machinery, furniture, its animal, an item it made or holds.</summary>
        public bool colony;
        /// <summary>Another people's (an animal of theirs, their building).</summary>
        public bool others;
        /// <summary>In the colony's home area, or within a few steps of it.</summary>
        public bool home;
        /// <summary>In one of the colony's zones (a growing zone, a stockpile), or with a plan or a mark on it.</summary>
        public bool zoned;
        /// <summary>In storage (a stockpile, a shelf).</summary>
        public bool stored;
        /// <summary>Forbidden by the colony (not by the camp itself).</summary>
        public bool forbidden;
        /// <summary>A plant someone sowed.</summary>
        public bool sown;
        /// <summary>A mineable the game counts as a resource (an ore lump).</summary>
        public bool resourceRock;
        /// <summary>A mineable's yield, or the item itself, is a chunk of plain stone (or a mineable yields nothing).</summary>
        public bool stoneChunk;
        /// <summary>What one of it is worth (a mineable's yield per piece, an item's value).</summary>
        public float value;
        /// <summary>What a piece of plain stone is worth: anything worth more is never theirs to take.</summary>
        public float stoneValue;
    }

    /// <summary>
    /// What a camp may take from the land (Camps/CampTake.cs): wood of any wild tree, wild animals, wild plants, plain rock
    /// and its chunks, all of it clear of the colony. Never anything of the colony's (its buildings, machinery and
    /// furniture, its stockpiled or forbidden things, its crops, its animals, whatever is in its home area or its zones),
    /// nor any mineable that isn't plain rock (decided by what it yields, so the mods' ores are covered too), nor anything
    /// worth more than plain stone. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampTakeMath
    {
        /// <summary>Why a camper may not take it (a word for the dev log: colony, others, home, zone, stored, forbidden, sown, ore, valuable, building), or null when they may.</summary>
        public static string Why(TakeFacts f)
        {
            // Their own goods, and what their own work brought down, are theirs to carry wherever it lies.
            if (f.theirs && !f.colony && !f.stored) return null;
            if (f.colony) return "colony";
            if (f.others) return "others";
            if (f.home) return "home";
            if (f.zoned) return "zone";
            if (f.stored) return "stored";
            if (f.forbidden) return "forbidden";
            switch (f.what)
            {
                case TakeWhat.Tree:
                case TakeWhat.Plant:
                    return f.sown ? "sown" : null;
                case TakeWhat.Animal:
                    return null;
                case TakeWhat.Mineable:
                    return PlainRock(f.resourceRock, f.stoneChunk, f.value, f.stoneValue) ? null : "ore";
                case TakeWhat.Item:
                    return f.stoneChunk || f.value <= Math.Max(0f, f.stoneValue) ? null : "valuable";
                default:
                    return "building";
            }
        }

        /// <summary>They may take it.</summary>
        public static bool May(TakeFacts f) => Why(f) == null;

        /// <summary>
        /// Plain rock, by what it yields: not a resource lump, and its yield a chunk of stone or nothing, or worth no more
        /// than plain stone. Steel, silver, gold, plasteel, uranium, jade, components and every mod's ore are not.
        /// </summary>
        public static bool PlainRock(bool resourceRock, bool yieldsStoneOrNothing, float yieldValue, float stoneValue) =>
            !resourceRock && (yieldsStoneOrNothing || yieldValue <= Math.Max(0f, stoneValue));
    }
}
