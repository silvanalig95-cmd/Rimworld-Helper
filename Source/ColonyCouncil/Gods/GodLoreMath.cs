using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A story of the gods as the rules need it: its key, how many pieces it is told in, and the gods it is of.</summary>
    public readonly struct TaleSketch
    {
        public readonly string key;
        public readonly int pieces;
        public readonly string[] gods;

        public TaleSketch(string key, int pieces, params string[] gods)
        {
            this.key = key;
            this.pieces = pieces;
            this.gods = gods ?? new string[0];
        }
    }

    /// <summary>What a bit of talk about the gods is: the next piece of a story, a saying, or a word on two gods.</summary>
    public enum GodTalkKind
    {
        Piece,
        Saying,
        Tie
    }

    /// <summary>
    /// The pure rules of the gods as people know them (docs/design/gods.md, "the contract"): the fourteen's keys, the kinds
    /// of peoples that revere a god (by age, raiders, the Empire, cannibals), who reveres whom, which name a speaker uses
    /// for a god (the short name or their age's), how the stories reach the colony (only through people, a piece at a time
    /// and in order, at the evening fire, from a visitor, in a neighbour's letter or a priest's word, never all at once),
    /// who believes, whose hand a believer takes something for (maybe wrongly), when two names are worked out to be one,
    /// and the voice the colony's book is kept in. No game types, so Tools/Tests checks them.
    /// </summary>
    public static class GodLoreMath
    {
        /// <summary>The gods, by the short names every part keys them by: the nine, and the four added after (Helios and Selene, the Gift and the Return; Mog, Murmur, Ducat).</summary>
        public static readonly string[] Keys = { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" };

        /// <summary>The kinds of people a god's def can name: by age, and three ways apart from it.</summary>
        public const string Tribal = "Tribal", Medieval = "Medieval", Industrial = "Industrial", Spacer = "Spacer", Raiders = "Raiders", Empire = "Empire", Cannibal = "Cannibal";

        public static readonly string[] Kinds = { Tribal, Medieval, Industrial, Spacer, Raiders, Empire, Cannibal };

        /// <summary>A people's kinds: its age's (0 tribal to 3 spacer), and raiders, the Empire and cannibals where it is one.</summary>
        public static List<string> KindsOf(int age, bool raiders, bool empire, bool cannibal)
        {
            var list = new List<string>();
            switch (Math.Max(0, Math.Min(3, age)))
            {
                case 0: list.Add(Tribal); break;
                case 1: list.Add(Medieval); break;
                case 2: list.Add(Industrial); break;
                default: list.Add(Spacer); break;
            }
            if (raiders) list.Add(Raiders);
            if (empire) list.Add(Empire);
            if (cannibal) list.Add(Cannibal);
            return list;
        }

        /// <summary>
        /// A people reveres a god: one a god's def names by its own name (Rimfolk's peoples and some of the player's mods')
        /// reveres only the gods that name it (<paramref name="namedAnywhere"/>: some god's def names it); any other by its kinds.
        /// The Empire keeps its own ways: it reveres by its Empire kind alone, never by its age.
        /// </summary>
        public static bool Reveres(ICollection<string> godPeoples, ICollection<string> godKinds, string people, ICollection<string> peopleKinds, bool namedAnywhere)
        {
            if (namedAnywhere) return people != null && godPeoples != null && godPeoples.Contains(people);
            if (godKinds == null || peopleKinds == null) return false;
            bool empire = peopleKinds.Contains(Empire);
            foreach (var k in peopleKinds)
            {
                if (empire && k != Empire) continue;
                if (godKinds.Contains(k)) return true;
            }
            return false;
        }

        /// <summary>
        /// The name a speaker uses for a god: about half of all speakers say its short name, the rest their age's own
        /// (the first of the def's names for it), the same speaker always the same way. No age name: the short name.
        /// </summary>
        public static string NameFor(string shortName, IList<string> ageNames, int speaker, int god)
        {
            if (ageNames == null || ageNames.Count == 0) return shortName;
            uint h = (uint)(speaker * 73856093) ^ (uint)(god * 19349663);
            return h % 2 == 0 ? shortName : ageNames[(int)(h / 2 % (uint)ageNames.Count)];
        }

        // ================= Stories, a piece at a time =================

        /// <summary>
        /// The next piece of a story someone tells: a story already begun with the colony is carried on first (its next piece,
        /// in order), else one of the teller's own gods not yet begun, else any not yet begun; when the colony has heard them
        /// all, an old one is told again from its start. <paramref name="told"/>: how many pieces of each the colony has
        /// heard. <paramref name="tellerGods"/>: the gods the teller's people revere (empty: anyone's). False with no stories.
        /// </summary>
        public static bool Next(IList<TaleSketch> stories, IDictionary<string, int> told, ICollection<string> tellerGods, float roll, out string key, out int piece)
        {
            key = null;
            piece = 0;
            if (stories == null || stories.Count == 0) return false;
            int Told(string k) => told != null && told.TryGetValue(k, out int n) ? n : 0;
            bool Mine(TaleSketch s)
            {
                if (tellerGods == null || tellerGods.Count == 0) return true;
                foreach (var g in s.gods)
                    if (tellerGods.Contains(g))
                        return true;
                return false;
            }
            var begun = new List<TaleSketch>();
            var mineNew = new List<TaleSketch>();
            var anyNew = new List<TaleSketch>();
            foreach (var s in stories)
            {
                if (s.pieces <= 0 || string.IsNullOrEmpty(s.key)) continue;
                int t = Told(s.key);
                if (t > 0 && t < s.pieces)
                {
                    if (Mine(s)) begun.Add(s);
                }
                else if (t == 0)
                {
                    if (Mine(s)) mineNew.Add(s);
                    anyNew.Add(s);
                }
            }
            var from = begun.Count > 0 ? begun : mineNew.Count > 0 ? mineNew : anyNew;
            if (from.Count == 0)
            {
                // All heard: an old one told again, from the start.
                var all = new List<TaleSketch>();
                foreach (var s in stories)
                    if (s.pieces > 0 && Mine(s))
                        all.Add(s);
                if (all.Count == 0) return false;
                var again = all[Pick(roll, all.Count)];
                key = again.key;
                piece = 0;
                return true;
            }
            var chosen = from[Pick(roll, from.Count)];
            key = chosen.key;
            piece = Math.Min(Told(chosen.key), chosen.pieces - 1);
            return true;
        }

        private static int Pick(float roll, int count) => Math.Max(0, Math.Min(count - 1, (int)(Math.Max(0f, roll) * count)));

        /// <summary>The colony's count of a story's pieces after one more is told (an old one told again counts nothing new).</summary>
        public static int AfterTelling(int told, int piece, int pieces) => piece == told && told < pieces ? told + 1 : told;

        /// <summary>What a bit of evening talk about the gods is: mostly the next piece of a story, else a saying, now and then a word on two gods.</summary>
        public static GodTalkKind Talk(float roll) => roll < 0.6f ? GodTalkKind.Piece : roll < 0.85f ? GodTalkKind.Saying : GodTalkKind.Tie;

        // ================= How often =================

        /// <summary>At each home, in each evening hour (from <see cref="EveningFrom"/> to <see cref="EveningTo"/>), the chance someone talks of the gods.</summary>
        public const float EveningChance = 0.06f;
        public const int EveningFrom = 17, EveningTo = 22;

        /// <summary>A visitor on the colony's land, in an hour near a colonist, tells something of their gods this often.</summary>
        public const float VisitorChance = 0.05f;

        /// <summary>A home hears of the gods at most once in this many hours (so a busy evening doesn't become a sermon).</summary>
        public const int GapHours = 20;

        /// <summary>A neighbour's letter retells a story of their gods this often, when it has one the colony hasn't had from them.</summary>
        public const float LetterTaleChance = 0.2f;

        /// <summary>A neighbour's letter speaks of a god's hand at the colony this often, within the days after it (GodHandsMath.Writable).</summary>
        public const float LetterHandChance = 0.35f;

        /// <summary>Talk of the gods at a home now: an evening hour, the last talk long enough ago, and the chance.</summary>
        public static bool TalkNow(int hour, int hoursSinceLast, float chance, float roll) =>
            hour >= EveningFrom && hour <= EveningTo && (hoursSinceLast < 0 || hoursSinceLast >= GapHours) && roll < chance;

        // ================= Who believes =================

        /// <summary>
        /// How readily someone believes the gods are at work (about -1 a scoffer to 1 a believer): their psychic sensitivity
        /// (1 is ordinary), how sure they are of their faith (-1: no Ideology), old age, being too clever for it, what they
        /// have seen for themselves, and a lean of their own. The same person comes out the same.
        /// </summary>
        public static float Credence(float psychic, float certainty, bool elder, bool tooSmart, int seen, int seed)
        {
            float c = 0.6f * (psychic - 1f);
            if (certainty >= 0f) c += 0.6f * (certainty - 0.5f);
            if (elder) c += 0.2f;
            if (tooSmart) c -= 0.6f;
            c += Math.Min(0.4f, 0.08f * Math.Max(0, seen));
            uint h = (uint)seed * 2654435761u;
            c += (h % 1000) / 1000f * 0.8f - 0.4f;
            return Math.Max(-1f, Math.Min(1f, c));
        }

        /// <summary>A believer will name a god in what they saw; below this, someone answers it as a scoffer.</summary>
        public const float BelieverFrom = -0.1f, ScofferBelow = 0f;

        // ================= The book's keeper and voice =================

        /// <summary>The voice the book is written in: by its keeper (a priest or the devout, the learned, an ordinary hand), or bare with none.</summary>
        public enum Voice
        {
            Bare,
            Plain,
            Devout,
            Learned
        }

        /// <summary>The keeper's voice: a priest or one picked for devotion writes devoutly, one with the mind for it learnedly, anyone else plainly; nobody, barely.</summary>
        public static Voice VoiceOf(bool keeper, bool priest, bool devout, int intellect)
        {
            if (!keeper) return Voice.Bare;
            if (priest || devout) return Voice.Devout;
            return intellect >= LearnedFrom ? Voice.Learned : Voice.Plain;
        }

        /// <summary>Picked as keeper for devotion from this credence; for letters from this Intellectual skill; a learned hand from this.</summary>
        public const float DevoutFrom = 0.5f;
        public const int LiterateFrom = 3, LearnedFrom = 8;

        // ================= What a believer takes it for =================

        /// <summary>Gods people confuse, by what their hands look like (a short circuit could be the mountain's heat or the sky's lightning).</summary>
        public static readonly Dictionary<string, string[]> Confused = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Hollin"] = new[] { "Morel", "Bellwether", "Murmur" },
            ["Smoulder"] = new[] { "Helios", "Hush", "Ducat" },
            ["Bellwether"] = new[] { "Hollin", "Mog" },
            ["Morel"] = new[] { "Hollin", "Hush", "Murmur" },
            ["Ennu"] = new[] { "Mog", "Ducat" },
            ["Pip"] = new[] { "Smoulder", "Helios" },
            ["Cirrus"] = new[] { "Helios", "Selene", "Murmur", "Hush" },
            ["Hush"] = new[] { "Cirrus", "Morel" },
            ["Undertow"] = new[] { "Hush", "Cirrus" },
            ["Helios"] = new[] { "Selene", "Smoulder", "Murmur" },
            ["Selene"] = new[] { "Helios", "Hush", "Ennu" },
            ["Mog"] = new[] { "Ennu", "Bellwether" },
            ["Murmur"] = new[] { "Cirrus", "Morel", "Helios" },
            ["Ducat"] = new[] { "Smoulder", "Ennu" }
        };

        /// <summary>A believer takes the wrong god for it this often, even when they know the right one.</summary>
        public const float MistakeChance = 0.2f;

        /// <summary>
        /// Whose hand a believer takes it for: the god whose hand it is, if they know of it and don't mistake it
        /// (<see cref="MistakeChance"/>); else one they confuse it with that they do know of; else none they can name (null:
        /// "something"). The truth is never marked: what they take it for is what is written.
        /// </summary>
        public static string Attribute(string truth, ICollection<string> knownOf, float roll)
        {
            bool knows = truth != null && knownOf != null && knownOf.Contains(truth);
            if (knows && roll >= MistakeChance) return truth;
            if (truth != null && Confused.TryGetValue(truth, out var alike) && knownOf != null)
            {
                int start = (int)(Math.Max(0f, roll) * 1000f) % alike.Length;
                for (int i = 0; i < alike.Length; i++)
                {
                    string g = alike[(start + i) % alike.Length];
                    if (knownOf.Contains(g)) return g;
                }
            }
            return knows ? truth : null;
        }

        // ================= Names worked out =================

        /// <summary>
        /// The chance in a day that someone at the colony works out that two names in the book are one god: more with a sharp
        /// mind on it (the best Intellectual skill) and the more is written under both.
        /// </summary>
        public static float ConnectChance(int intellect, int entriesUnderBoth) =>
            Math.Min(0.12f, 0.01f + 0.004f * Math.Max(0, intellect) + 0.004f * Math.Min(10, Math.Max(0, entriesUnderBoth)));

        /// <summary>A teller using a name the colony knows the god by under another says so this often.</summary>
        public const float TellerConnects = 0.25f;

        /// <summary>The book keeps at most this many entries (the oldest let go, never a page's first) and this many names.</summary>
        public const int MaxEntries = 600, MaxNames = 60;
    }
}
