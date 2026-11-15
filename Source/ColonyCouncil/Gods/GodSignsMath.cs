using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the gods' signs (Gods/GodSigns.cs draws them; Tools/make_god_signs.py makes them): the colony's own
    /// holy sign for each god, what its people draw, carve or stitch for it, never a portrait of the god. Where each texture
    /// is (the sign in the god's colours, and the unknown seal), which god a chapter of the colony's book holds itself to be
    /// about (its plain line follows what was told under that name, not the truth), and which sign each chapter shows. No
    /// game types, so Tools/Tests checks them.
    /// </summary>
    public static class GodSignsMath
    {
        /// <summary>The folder under Textures the signs are in.</summary>
        public const string Dir = "ColonyCouncil/Gods/Signs/";

        /// <summary>The plain sign of a god by its key ("Hollin"): ColonyCouncil/Gods/Signs/Hollin.</summary>
        public static string PlainPath(string key) => Dir + key;

        /// <summary>
        /// The seal of a god the colony can't yet say what it is (ColonyCouncil/Gods/Signs/Unknown): one plain seal for every
        /// such chapter, so every name in the book has a picture and none gives away which names are one god.
        /// </summary>
        public const string UnknownPath = Dir + "Unknown";

        /// <summary>
        /// A verse that tells the colony what a god is: a line of what it is as someone told it (a doubt doesn't), or the god
        /// seen plainly by the colony's own people (GodLore.Sighted). Once a chapter has one, it shows its plain line and its
        /// sign; before, neither.
        /// </summary>
        public static bool Tells(BookVerse v) => v != null && v.god >= 0 && (v.part == BookPart.What && !v.against || v.part == BookPart.Known && v.key == "plain");

        /// <summary>
        /// The god a chapter of the colony's book is about, as the colony holds it: the god most of its verses that tell what
        /// it is speak of (<see cref="Tells"/>), the first one told on a tie; -1 while nobody has told what it is under that
        /// name. Its plain line and its sign show for that god, so a chapter told of wrongly shows the wrong one, as believed.
        /// </summary>
        public static int Believed(IEnumerable<BookVerse> verses)
        {
            if (verses == null) return -1;
            var count = new Dictionary<int, int>();
            var first = new Dictionary<int, int>();
            foreach (var v in verses)
            {
                if (!Tells(v)) continue;
                count[v.god] = (count.TryGetValue(v.god, out int n) ? n : 0) + 1;
                if (!first.TryGetValue(v.god, out int t) || v.tick < t) first[v.god] = v.tick;
            }
            int best = -1;
            foreach (var kv in count)
                if (best < 0 || kv.Value > count[best] || kv.Value == count[best] && first[kv.Key] < first[best])
                    best = kv.Key;
            return best;
        }

        /// <summary>
        /// The god whose sign each chapter of the book shows (the player, 15 November 2026, of chapters left with the seal:
        /// "god's book still shows the same icon for many"), by chapter: one told what its god is shows the god it was told
        /// of (<paramref name="believed"/>, from <see cref="Believed"/>); any other shows the sign of the god its name
        /// belongs to (<paramref name="named"/>), the earliest heard (<paramref name="heard"/>) first, unless that god's sign
        /// already shows on a chapter told of it or on an earlier name, so two untold names never show they are one god;
        /// -1, the unknown seal, for the rest.
        /// </summary>
        public static int[] SignGods(IList<int> believed, IList<int> named, IList<int> heard)
        {
            int n = believed?.Count ?? 0;
            var result = new int[n];
            var taken = new HashSet<int>();
            var rest = new List<int>();
            for (int i = 0; i < n; i++)
            {
                result[i] = believed[i];
                if (believed[i] >= 0) taken.Add(believed[i]);
                else rest.Add(i);
            }
            rest.Sort((a, b) =>
            {
                int ha = heard != null && a < heard.Count ? heard[a] : int.MaxValue, hb = heard != null && b < heard.Count ? heard[b] : int.MaxValue;
                return ha != hb ? ha.CompareTo(hb) : a.CompareTo(b);
            });
            foreach (int i in rest)
            {
                int g = named != null && i < named.Count ? named[i] : -1;
                result[i] = g >= 0 && taken.Add(g) ? g : -1;
            }
            return result;
        }
    }
}
