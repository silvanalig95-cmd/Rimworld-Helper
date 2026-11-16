using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Where a camp gets a material for its building from.</summary>
    public enum CampSource
    {
        /// <summary>Only what they carry (steel, cloth, chemfuel, components): the land doesn't give it.</summary>
        Carried,
        /// <summary>Logs of the wild trees they fell.</summary>
        Trees,
        /// <summary>Stone blocks dressed from chunks of the plain rock they mine.</summary>
        Rock
    }

    /// <summary>What one of them does next for pieces short of a material.</summary>
    public enum CampGatherStep
    {
        /// <summary>Nothing to gather: what they have is carried to the pieces.</summary>
        Bring,
        /// <summary>A wild tree felled for its logs.</summary>
        Fell,
        /// <summary>A chunk of stone of theirs dressed into blocks.</summary>
        Cut,
        /// <summary>A chunk of stone lying free on the land carried to their stash.</summary>
        Pick,
        /// <summary>Plain rock mined for a chunk.</summary>
        Mine,
        /// <summary>The land can't give it: the pieces that need it are given up.</summary>
        GiveUp
    }

    /// <summary>What there is to know about one material their pieces need (read off the map by Camps/CampGather.cs).</summary>
    public struct GatherFacts
    {
        public CampSource source;
        /// <summary>How much more of it the pieces need than is in them, on its way, in their goods and packs, and lying where their own work left it.</summary>
        public int shortBy;
        /// <summary>The logs of it the wild trees they may fell within their reach would give.</summary>
        public int treeYield;
        /// <summary>The blocks of it their own chunks would give, and the chunks lying free on the land that they may take.</summary>
        public int chunkYield, freeChunkYield;
        /// <summary>Cells of plain rock within their reach they may mine whose chunks give it.</summary>
        public int rockCells;
        /// <summary>They have a way to dress stone (<see cref="CampGatherMath.Cutter"/>).</summary>
        public bool cutter;
        /// <summary>One of them can fell trees, mine, carry.</summary>
        public bool feller, miner, hauler;
        /// <summary>They are packing up to go.</summary>
        public bool packing;
    }

    /// <summary>One material a piece could be made of, for the choice of it (<see cref="CampGatherMath.ChooseStuff"/>).</summary>
    public struct StuffOption
    {
        /// <summary>Listed for the piece by their kit or kind (a material not listed is only taken when the land gives it).</summary>
        public bool listed;
        /// <summary>The material's facts, its shortBy being what the pieces need of it less what they have.</summary>
        public GatherFacts facts;
    }

    /// <summary>What holds a piece up for good, for the choice to give it up (<see cref="CampGatherMath.GiveUpWhy"/>).</summary>
    public struct BuildFacts
    {
        public bool packing;
        /// <summary>One of them can build it: construction is open to them and they have the skill it needs.</summary>
        public bool builder;
        /// <summary>One of them can carry, where something has still to be carried to it.</summary>
        public bool hauler;
        public bool needsCarrying;
        /// <summary>Every material it is short of is one the land gives them (<see cref="CampGatherMath.LandGives"/>).</summary>
        public bool landGives;
        /// <summary>Ticks since anything last moved on it: material delivered or gathered, work done, a piece finished.</summary>
        public int sinceProgress;
    }

    /// <summary>
    /// What a camp lays out is built, and what it needs is gathered on the land (Camps/CampGather.cs; the player's report
    /// of 16 November 2026: stools laid out round a camp's fire and never built). A piece is built of what they have;
    /// when that runs short they gather the rest as a player's builders would: they fell wild trees for logs, and mine
    /// plain rock and dress its chunks into blocks where they have a stonecutter's table or, tribal and medieval, by hand
    /// at their crafting spot. What only a town makes (steel, cloth, chemfuel) is only what they carry. A piece is given up
    /// only when the land can't give what it needs, nobody of them can build it, or they are packing; and, as a last
    /// resort, when nothing has moved on it for two days. One of them gathers at a time (two in a camp of six), by day once
    /// the camp stands. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampGatherMath
    {
        /// <summary>Two days with nothing moving on a piece (no material, no work, nothing gathered): given up as out of their reach.</summary>
        public const int StallTicks = 2 * 60000;

        /// <summary>
        /// The blocks a cell of plain rock gives, as the game has it on average: a chunk of twenty blocks for every four
        /// cells mined (the game's rock drops a chunk one time in four).
        /// </summary>
        public const int BlocksPerRock = 5;

        /// <summary>
        /// The land gives it: they have enough already; or they can fell enough of the wild trees within their reach; or
        /// they can dress stone and have the chunks for it, or one of them can mine enough plain rock within their reach.
        /// </summary>
        public static bool LandGives(GatherFacts f)
        {
            if (f.shortBy <= 0) return true;
            switch (f.source)
            {
                case CampSource.Trees:
                    return f.feller && f.treeYield >= f.shortBy;
                case CampSource.Rock:
                    if (!f.cutter) return false;
                    int chunks = f.chunkYield + (f.hauler ? f.freeChunkYield : 0);
                    return chunks >= f.shortBy || (f.miner && chunks + f.rockCells * BlocksPerRock >= f.shortBy);
                default:
                    return false;
            }
        }

        /// <summary>
        /// What one of them does next for the material: carry what they have when it is enough; else fell a tree for wood,
        /// dress a chunk they have, fetch one lying free, mine plain rock for stone; give up when they are packing or the
        /// land can't give it.
        /// </summary>
        public static CampGatherStep Next(GatherFacts f)
        {
            if (f.packing || !LandGives(f)) return CampGatherStep.GiveUp;
            if (f.shortBy <= 0) return CampGatherStep.Bring;
            if (f.source == CampSource.Trees) return CampGatherStep.Fell;
            if (f.chunkYield > 0) return CampGatherStep.Cut;
            if (f.freeChunkYield > 0 && f.hauler) return CampGatherStep.Pick;
            return CampGatherStep.Mine;
        }

        /// <summary>
        /// The material a piece is made of: the first listed they have enough of (their goods and packs); else wood where the
        /// wild trees within their reach give enough of it; else stone where they can dress the rock round them; -1 when the
        /// land gives nothing they could make it of (the piece isn't laid out).
        /// </summary>
        public static int ChooseStuff(IList<StuffOption> options)
        {
            if (options == null) return -1;
            for (int i = 0; i < options.Count; i++)
                if (options[i].listed && options[i].facts.shortBy <= 0) return i;
            for (int i = 0; i < options.Count; i++)
                if (options[i].facts.source == CampSource.Trees && LandGives(options[i].facts)) return i;
            for (int i = 0; i < options.Count; i++)
                if (options[i].facts.source == CampSource.Rock && LandGives(options[i].facts)) return i;
            return -1;
        }

        /// <summary>
        /// Why a piece is given up (packing, builder, hauler, land, stalled), or null while it goes on: they are packing,
        /// nobody of them can build it or carry what it needs, the land can't give what it is short of, or (the last
        /// resort) nothing has moved on it for two days.
        /// </summary>
        public static string GiveUpWhy(BuildFacts f)
        {
            if (f.packing) return "packing";
            if (!f.builder) return "builder";
            if (f.needsCarrying && !f.hauler) return "hauler";
            if (!f.landGives) return "land";
            if (f.sinceProgress > StallTicks) return "stalled";
            return null;
        }

        /// <summary>How many of them gather for their building at once: one, two in a camp of six hands or more.</summary>
        public static int Gatherers(int hands) => hands >= 6 ? 2 : 1;

        /// <summary>
        /// When they build and gather: setting up the camp, at any waking hour; once it stands, in their working hours
        /// (seven to five), after meals and sleep, never in the evening by the fire.
        /// </summary>
        public static bool BuildHour(int hour, bool settingUp)
        {
            if (settingUp) return true;
            var h = CampDayMath.HourOf(hour);
            return h == CampHour.Work || h == CampHour.Noon;
        }

        /// <summary>Their age's way of dressing stone without a stonecutter's table: by hand at their crafting spot, for a tribal or medieval people.</summary>
        public static bool CutsByHand(string age) => age == "tribal" || age == "medieval";

        /// <summary>They can dress stone into blocks: at a stonecutter's table of theirs, or by hand at their crafting spot (tribal, medieval).</summary>
        public static bool Cutter(bool stonecutter, bool craftSpot, string age) => stonecutter || (craftSpot && CutsByHand(age));

        /// <summary>The trees a shortfall takes, at so many logs a tree (at least one tree for any shortfall).</summary>
        public static int TreesFor(int shortBy, int perTree) => shortBy <= 0 ? 0 : (int)Math.Ceiling(shortBy / (double)Math.Max(1, perTree));
    }
}
