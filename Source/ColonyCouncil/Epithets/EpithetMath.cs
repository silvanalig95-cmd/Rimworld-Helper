using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules behind names per colony (Epithets.cs; stage 0, step 7b), with no game types so Tools/Tests can check them:
    /// the key a name is kept under (the name alone with one colony, as always; the name and its colony with more), which
    /// colony an older key belongs to (its holder's), when taking a name over makes a rival (only within one colony), and
    /// which colony the game's first founders and firstborn are (the one settled with the game, else the seat).
    /// </summary>
    public static class EpithetMath
    {
        public const char Sep = '|';

        /// <summary>The key a name is kept under for a colony: the name alone with one colony (or none given), else the name and the colony.</summary>
        public static string Key(string defName, int colony, bool several) =>
            several && colony != HomesMath.None ? defName + Sep + colony : defName;

        /// <summary>The name a key is for; <paramref name="colony"/> is its colony, or none for a key from one colony's days.</summary>
        public static string DefOf(string key, out int colony)
        {
            colony = HomesMath.None;
            if (key == null) return null;
            int i = key.LastIndexOf(Sep);
            if (i <= 0 || !int.TryParse(key.Substring(i + 1), out int c)) return key;
            colony = c;
            return key.Substring(0, i);
        }

        /// <summary>
        /// A key is <paramref name="colony"/>'s name: its own key, or a key from one colony's days whose holder belongs
        /// there (<paramref name="holderColony"/>).
        /// </summary>
        public static bool ForColony(string key, int colony, int holderColony)
        {
            DefOf(key, out int c);
            return c != HomesMath.None ? c == colony : holderColony == colony;
        }

        /// <summary>Taking a name over makes the one who had it a rival only when both are of the colony the name is.</summary>
        public static bool Rivals(int previousColony, int newColony, int nameColony) =>
            previousColony == nameColony && newColony == nameColony;

        /// <summary>
        /// The colony of the game's first days, whose founders and firstborn are the game's: the first home settled with the
        /// game (in founding order), else the seat (a colony that moved keeps its first settlers). None without a home.
        /// </summary>
        public static int FirstColony(IList<int> homes, IList<bool> settledWithGame, int seat)
        {
            if (homes == null) return seat;
            for (int i = 0; i < homes.Count && i < settledWithGame.Count; i++)
                if (settledWithGame[i]) return homes[i];
            return seat;
        }

        /// <summary>A founder's or firstborn's entry for a colony settled later: the colony and the person.</summary>
        public static string Entry(int colony, string thingId) => colony + Sep.ToString() + thingId;

        /// <summary>A colony settled later is still in its first days: its first settlers are its founders.</summary>
        public static bool FirstDays(int now, int founded, int ticksPerDay) => founded >= 0 && now - founded <= ColonyTraitMath.FounderDays * ticksPerDay;
    }
}
