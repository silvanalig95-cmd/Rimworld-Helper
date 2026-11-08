using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a kind of camp does with its constructions when it goes (CampKindDef.leaves).</summary>
    public enum CampLeaves
    {
        /// <summary>Left standing, nobody's, for the colony to use, claim or take down: what most people do with walls and a fire pit.</summary>
        Standing,
        /// <summary>Taken down so nothing tells they were there: a people whose ways leave no trace, or who don't want to be followed.</summary>
        NoTrace,
        /// <summary>Handed to the colony: a camp hired by contract, whose post is its employer's.</summary>
        Employer
    }

    /// <summary>What becomes of one building of theirs as they go.</summary>
    public enum PackFate
    {
        /// <summary>Uninstalled and carried off: bedrolls, tents, stools, tables, their other furniture.</summary>
        Pack,
        /// <summary>Left standing, nobody's.</summary>
        Leave,
        /// <summary>Taken down, its materials carried off.</summary>
        TakeDown,
        /// <summary>Left standing and the colony's.</summary>
        Hand
    }

    /// <summary>Something a camp leaves behind, from what happened in its stay (CampLeftovers.cs).</summary>
    public enum Leftover
    {
        /// <summary>Logs they cut and couldn't carry, stacked by the old fire.</summary>
        Woodpile,
        /// <summary>Hides of their own kills, cured at the camp, left at the colony's edge.</summary>
        Hides,
        /// <summary>Stone they cut and couldn't carry, stacked as a cairn by the old fire.</summary>
        Cairn,
        /// <summary>A gift at the colony's gate, of their people's own goods, when the stay was warm.</summary>
        Gift,
        /// <summary>Refuse and filth round the old fire: a long, crowded or sour stay.</summary>
        Refuse,
        /// <summary>Offal and bones of their butchering, left: it draws a predator in (the game's own beast).</summary>
        Offal,
        /// <summary>A fire not quite out in hot, dry weather: the grass catches (the game's own fire).</summary>
        Embers,
        /// <summary>A beast of theirs too lame for the road, left behind.</summary>
        LameBeast
    }

    /// <summary>What there is to know about a stay as it ends, for what it leaves behind.</summary>
    public struct StayFacts
    {
        /// <summary>They went in their own time (not driven off, fled or fighting).</summary>
        public bool orderly;
        public float days;
        public int people;
        /// <summary>The camp's unease at the end (0 to 100).</summary>
        public float tension;
        /// <summary>How warm the colony was to them: trades, helps, gifts, evenings at their fire.</summary>
        public int warmth;
        /// <summary>What their work brought in: beasts killed and dressed, trees felled, rock cut.</summary>
        public int butchered, felled, mined;
        /// <summary>Logs, hides and stone of theirs left at the stash once the porters have taken what they can.</summary>
        public int woodLeft, hidesLeft, stoneLeft;
        /// <summary>The stay's weather: hot and dry for days (for embers).</summary>
        public bool hotDry;
        /// <summary>They had beasts with them, and one is lame or old.</summary>
        public bool lameBeast;
        /// <summary>The kind's calling allows it: hunters, herders, stonecutters... (each leftover's kinds, CampKindDef.leftovers).</summary>
        public Func<Leftover, bool> calling;
    }

    /// <summary>
    /// Packing up and what stays (Camps/CampPack.cs, CampLeftovers.cs): the portable things they own go with them,
    /// uninstalled on the map in the last hours of the stay; constructions stay standing, nobody's, unless the kind has a
    /// reason of its own; what they leave behind, good or bad, comes from what they did on the map. Pure (no game types),
    /// so Tools/Tests can check it.
    /// </summary>
    public static class CampPackMath
    {
        /// <summary>
        /// They leave in their own time: the end of their stay, asked to go, moved on, the colony's terms declined, hunger,
        /// crowding, a contract unpaid, a quarrel of their own. Driven off, fleeing a raid or a hurt, the weather's danger,
        /// a fallout, a fight, lost: they go with what they carry.
        /// </summary>
        public static bool Orderly(string ending)
        {
            switch (ending)
            {
                case "time":
                case "asked":
                case "moved":
                case "declined":
                case "hunger":
                case "crowded":
                case "unpaid":
                case "brawlTheirs":
                case "weather":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>How long packing takes before they set off: a few hours at the end of a stay, one in bad weather, none in flight.</summary>
        public static int PackHours(string ending) => !Orderly(ending) ? 0 : ending == "weather" || ending == "hunger" ? 1 : 3;

        /// <summary>
        /// What becomes of a building of theirs: <paramref name="portable"/> (furniture that can be uninstalled) goes with
        /// them when they leave in their own time and have hands to carry it, else it's left; a construction (a wall, a door,
        /// a fire pit, a spot) stands, nobody's, unless the kind takes its own down or hands it to its employer.
        /// </summary>
        public static PackFate Fate(CampLeaves leaves, bool portable, bool orderly, bool hands)
        {
            if (portable) return orderly && hands ? PackFate.Pack : PackFate.Leave;
            if (!orderly) return PackFate.Leave;
            switch (leaves)
            {
                case CampLeaves.NoTrace: return hands ? PackFate.TakeDown : PackFate.Leave;
                case CampLeaves.Employer: return PackFate.Hand;
                default: return PackFate.Leave;
            }
        }

        // ---------------- What they leave behind ----------------

        /// <summary>Logs left at the stash worth a woodpile (fewer go in their packs).</summary>
        public const int WoodpileFrom = 25;
        public const int HidesFrom = 10;
        public const int CairnFrom = 4;

        /// <summary>
        /// What a stay leaves behind, from what happened on the map (each only where the kind's calling has it): good things
        /// when they leave in their own time from a stay that went well (a woodpile of what they cut, hides of what they
        /// hunted, a cairn of the stone they cut, a gift when the colony was warm to them), and bad ones from a stay that
        /// went sour, ran long and crowded, or ended in a hurry (refuse; offal of what they butchered; embers in hot dry
        /// weather; a lame beast left). <paramref name="roll"/> gives a 0..1 number for each leftover.
        /// </summary>
        public static List<Leftover> Leaves(StayFacts f, Func<Leftover, double> roll)
        {
            var list = new List<Leftover>();
            bool Can(Leftover l) => f.calling == null || f.calling(l);
            void Maybe(Leftover l, double chance)
            {
                if (Can(l) && chance > 0 && roll(l) < chance) list.Add(l);
            }
            bool sour = f.tension >= 50f;
            bool warm = f.tension < 35f && f.warmth >= 3;
            if (f.orderly && !sour)
            {
                if (f.woodLeft >= WoodpileFrom) Maybe(Leftover.Woodpile, 0.8);
                if (f.hidesLeft >= HidesFrom && f.butchered > 0) Maybe(Leftover.Hides, warm ? 0.8 : 0.4);
                if (f.stoneLeft >= CairnFrom && f.mined > 0) Maybe(Leftover.Cairn, 0.6);
                if (warm) Maybe(Leftover.Gift, Math.Min(0.6, 0.25 + 0.05 * f.warmth));
            }
            // Refuse: a stay gone sour, or a long, crowded one.
            if (sour || (f.days >= 6f && f.people >= 6)) Maybe(Leftover.Refuse, sour ? 0.6 : 0.3);
            // Offal of their butchering, left by people in a hurry or who didn't care.
            if (f.butchered >= 2 && (!f.orderly || sour)) Maybe(Leftover.Offal, 0.5);
            // A fire not quite out: hot, dry weather and a hurried or careless going.
            if (f.hotDry && (!f.orderly || sour)) Maybe(Leftover.Embers, 0.25);
            if (f.lameBeast && f.orderly) Maybe(Leftover.LameBeast, 0.35);
            return list;
        }

        /// <summary>Offal left: the chance (through the shared budget) that it draws a predator of the land in.</summary>
        public const float OffalDrawChance = 0.5f;
        /// <summary>Embers left in hot, dry weather: the chance (through the shared budget) that the grass by the fire catches.</summary>
        public const float EmbersCatchChance = 0.5f;

        /// <summary>Whether a leftover can hurt anyone (it goes through the shared harm budget: Harm/HarmBudgetMath.cs, campLeft).</summary>
        public static bool CanHurt(Leftover l) => l == Leftover.Offal || l == Leftover.Embers;

        /// <summary>The leftovers' word for the camp's leaving message ("CC_CampLeft_" + this).</summary>
        public static string Key(Leftover l) => l.ToString();
    }
}
