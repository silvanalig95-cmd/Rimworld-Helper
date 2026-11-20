using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Who the comms console reaches (the player, 20 November 2026: "if they are still on older technology, it doesn't make
    /// sense if they are all reachable with the comms console"; Peoples/ConsoleRadio.cs; DESIGN.md "The comms console by
    /// radio"): a people with a radio, that is a people of the industrial age or later by the age it lives in now
    /// (NotableMath.Callable, the same rule as a call in the letters, a pact's call, a ransom's or a favour's), or a people
    /// with a town within reach of the Signal Line's masts (PostMath.RadioReaches, as the letters' calls have it). Pure, so
    /// Tools/Tests (ForeignAgesTests) checks it.
    /// </summary>
    public static class ConsoleRadioMath
    {
        /// <summary>
        /// The console reaches a people of this tech level (as the game numbers them) whose towns lie so many tiles from the
        /// nearest Signal Line mast (-1: none within reach): its age has the radio, or a mast carries the call to a town.
        /// </summary>
        public static bool Reaches(int theirTech, IEnumerable<float> townMastTiles)
        {
            if (PostMath.RadioReaches(theirTech, -1f)) return true;
            if (townMastTiles == null) return false;
            foreach (float t in townMastTiles)
                if (PostMath.RadioReaches(theirTech, t))
                    return true;
            return false;
        }

        /// <summary>
        /// The console's own call to a people is greyed: the setting and the deeper game hooks on, a people that isn't the
        /// colony, and no radio to answer it.
        /// </summary>
        public static bool Greyed(bool settingOn, bool hooksOn, bool isPlayer, bool reaches) => settingOn && hooksOn && !isPlayer && !reaches;

        /// <summary>The words of a greyed call (CC_ConsoleRadio_*): how a letter reaches them while the letters are on, else only that the call can't.</summary>
        public static string Key(bool letters) => letters ? "CC_ConsoleRadio_Letter" : "CC_ConsoleRadio_None";
    }
}
