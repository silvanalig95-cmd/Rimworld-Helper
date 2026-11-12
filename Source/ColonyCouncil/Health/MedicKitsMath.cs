using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How many of a home's medics carry medicine and how much (the player, 12 November 2026: "my medics should always carry
    /// a few medicines with them for spontaniouis battles"). Pure, for the tests.
    /// </summary>
    public static class MedicKitsMath
    {
        /// <summary>The medics who carry: one for a home of up to four, two up to eight, three beyond.</summary>
        public static int Medics(int colonists) => colonists <= 0 ? 0 : colonists <= 4 ? 1 : colonists <= 8 ? 2 : 3;

        /// <summary>What stays in the home's stores of a kind for its sick: one a colonist (four at least) and two for each one sick or hurt now.</summary>
        public static int Reserve(int colonists, int sick) => Math.Max(4, colonists) + 2 * Math.Max(0, sick);

        /// <summary>
        /// How many each medic carries of a kind the home holds <paramref name="stock"/> of (stored, and what the medics carry
        /// already): three when there's plenty, two when there's enough, one when just a little spare, none when the sick
        /// would go short.
        /// </summary>
        public static int Each(int stock, int medics, int reserve)
        {
            if (medics <= 0) return 0;
            int per = (stock - reserve) / medics;
            return per >= 4 ? 3 : per >= 2 ? 2 : per >= 1 ? 1 : 0;
        }

        /// <summary>A record of what Rimfolk set for a colonist: "id|def|count"; count -1 marks one the player took over.</summary>
        public static string Record(int id, string def, int count) => id + "|" + (def ?? "") + "|" + count;

        public static bool Parse(string s, out int id, out string def, out int count)
        {
            id = 0;
            def = null;
            count = 0;
            var p = s?.Split('|');
            return p != null && p.Length == 3 && int.TryParse(p[0], out id) && int.TryParse(p[2], out count) && (def = p[1]) != null;
        }
    }
}
