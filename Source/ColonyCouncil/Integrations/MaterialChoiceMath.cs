using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Which materials the material step offers when a piece is ordered from Nice Inventory Tab's gear tab
    /// (Integrations/MaterialChoice.cs; the player, 19 November 2026: "it would just list or show materials that I have
    /// enough of for the job"). No game types, linked in the tests.
    /// </summary>
    public static class MaterialChoiceMath
    {
        /// <summary>One material the recipe takes: its name, how much of it is on the map, how much one piece takes of it.</summary>
        public struct Stuff
        {
            public string label;
            public int have;
            public int need;

            public Stuff(string label, int have, int need)
            {
                this.label = label;
                this.have = have;
                this.need = need;
            }
        }

        /// <summary>There is enough of it on the map for one piece.</summary>
        public static bool Enough(int have, int need) => need > 0 && have >= need;

        /// <summary>How many pieces what is on the map makes.</summary>
        public static int Pieces(int have, int need) => need > 0 && have > 0 ? have / need : 0;

        /// <summary>
        /// The materials offered, as indices into <paramref name="stuffs"/>: only those with enough for one piece, the one
        /// that makes the most pieces first, then by name.
        /// </summary>
        public static List<int> Offered(IReadOnlyList<Stuff> stuffs)
        {
            var list = new List<int>();
            if (stuffs == null) return list;
            for (int i = 0; i < stuffs.Count; i++)
                if (Enough(stuffs[i].have, stuffs[i].need)) list.Add(i);
            list.Sort((a, b) =>
            {
                int c = Pieces(stuffs[b].have, stuffs[b].need).CompareTo(Pieces(stuffs[a].have, stuffs[a].need));
                if (c != 0) return c;
                c = string.Compare(stuffs[a].label ?? "", stuffs[b].label ?? "", StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : a.CompareTo(b);
            });
            return list;
        }
    }
}
