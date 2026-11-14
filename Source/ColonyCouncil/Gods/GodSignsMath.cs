using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the gods' signs (Gods/GodSigns.cs draws them; Tools/make_god_signs.py makes them): the colony's own
    /// holy sign for each god, what its people draw, carve or stitch for it, never a portrait of the god. Where each texture
    /// is (the plain sign, and the sign in each age's form: scratched on a stone, painted on a shield, stamped on a badge,
    /// lit in a ring), which form the colony's own age takes, and which god a chapter of the colony's book holds itself to
    /// be about (its plain line and its sign follow what was told under that name, not the truth). No game types, so
    /// Tools/Tests checks them.
    /// </summary>
    public static class GodSignsMath
    {
        /// <summary>The folder under Textures the signs are in.</summary>
        public const string Dir = "ColonyCouncil/Gods/Signs/";

        /// <summary>The ages' forms, by the colony's age (0 tribal to 3 spacer, WorldAge): the textures' endings.</summary>
        public static readonly string[] AgeNames = { "Tribal", "Medieval", "Industrial", "Spacer" };

        /// <summary>An age's form (0 tribal to 3 spacer; anything past either end is the nearest).</summary>
        public static string AgeName(int age) => AgeNames[age < 0 ? 0 : age > 3 ? 3 : age];

        /// <summary>The plain sign of a god by its key ("Hollin"): ColonyCouncil/Gods/Signs/Hollin.</summary>
        public static string PlainPath(string key) => Dir + key;

        /// <summary>A god's sign in an age's form: ColonyCouncil/Gods/Signs/Hollin_Medieval.</summary>
        public static string AgePath(string key, int age) => Dir + key + "_" + AgeName(age);

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
    }
}
