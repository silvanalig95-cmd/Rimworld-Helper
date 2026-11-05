using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's own houses on the world ledger (stage 2, release 2d.1: house renown; Diplomacy/Renown.cs keeps them):
    /// <see cref="LedgerHouse"/> records beside the towns' houses, marked <see cref="LedgerHouse.ours"/>, with no town,
    /// keyed by their family name, twelve at most (RenownMath.MaxOurs). They are cards and links as every house is.
    /// </summary>
    public partial class WorldLedger
    {
        /// <summary>The colony's houses (read only, a fresh list).</summary>
        public static List<LedgerHouse> OurHouses()
        {
            var list = new List<LedgerHouse>();
            var l = Instance;
            if (l == null) return list;
            foreach (var h in l.houses)
                if (h != null && h.ours)
                    list.Add(h);
            return list;
        }

        /// <summary>The colony's house of this family name (RenownMath.Key), or null.</summary>
        public static LedgerHouse OurHouse(string key)
        {
            var l = Instance;
            if (l == null || key == null) return null;
            foreach (var h in l.houses)
                if (h != null && h.ours && RenownMath.Key(h.name) == key)
                    return h;
            return null;
        }

        /// <summary>A house of the colony's made for a family now known by name; null without a game or a name, or with twelve already.</summary>
        public static LedgerHouse AddOurHouse(string name, int seat)
        {
            var l = Instance;
            if (l == null || name.NullOrEmpty() || OurHouses().Count >= RenownMath.MaxOurs) return null;
            var h = new LedgerHouse { house = NewHouse(), name = name, ours = true, seat = seat, since = Now, lastSeen = Now };
            l.houses.Add(h);
            Changed();
            return h;
        }

        /// <summary>One of the colony's houses let go (nobody of it left for two years, and no tie naming them).</summary>
        public static void DropOurHouse(LedgerHouse h)
        {
            var l = Instance;
            if (l == null || h == null || !h.ours) return;
            if (l.houses.Remove(h)) Changed();
        }

        /// <summary>A house's record by its ref, ours or a town's, or null.</summary>
        public static LedgerHouse HouseRecord(Ref r)
        {
            var l = Instance;
            if (l == null || r.IsNone) return null;
            foreach (var h in l.houses)
                if (h != null && h.house == r)
                    return h;
            return null;
        }
    }
}
