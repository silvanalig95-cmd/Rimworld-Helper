using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of peoples that arise during play (release B: docs/design/peoples.md, "Release B"; DESIGN.md
    /// "Rimfolk's peoples"), with no game types so Tools/Tests can check them: the four causes and when each is due, the
    /// pace (at most one new people every 40 days at the setting's 100%, each cause once in 90, six standing at most),
    /// which of Rimfolk's templates a cause takes by its age, the goodwill a new people starts with, the league's towns,
    /// and its emblem's colour.
    /// </summary>
    public static class ArisingMath
    {
        public const int DayTicks = 60000;

        // ================= The causes =================

        /// <summary>
        /// Survivors of a town that fell in a fight settle together; merchants (or herders) of prospering towns band
        /// together; those the council sent away gather round one of them; faithful break away after a war of words over
        /// their faith (Ideology).
        /// </summary>
        public const string Refugees = "refugees", League = "league", Exiles = "exiles", Splinter = "splinter";

        public static readonly string[] Kinds = { Refugees, League, Exiles, Splinter };

        public static bool IsKind(string kind) => kind != null && Array.IndexOf(Kinds, kind) >= 0;

        /// <summary>The key a cause is kept under, so it gives one people at most ("refugees:4211").</summary>
        public static string Key(string kind, long id) => kind + ":" + id;

        /// <summary>A league's key: its towns' ids in order ("league:12,40,77").</summary>
        public static string LeagueKey(IEnumerable<int> towns) => League + ":" + string.Join(",", (towns ?? Enumerable.Empty<int>()).OrderBy(t => t));

        /// <summary>The towns of a league's key; none for any other key.</summary>
        public static List<int> LeagueTowns(string key)
        {
            var list = new List<int>();
            if (key == null || !key.StartsWith(League + ":", StringComparison.Ordinal)) return list;
            foreach (var part in key.Substring(League.Length + 1).Split(','))
                if (int.TryParse(part, out int id))
                    list.Add(id);
            return list;
        }

        // ================= The pace =================

        /// <summary>No new people before the world's 15th day.</summary>
        public const int EarliestDay = 15;
        /// <summary>At the setting's 100%: at least 40 days between two new peoples (one or two a year), 90 between two of the same cause.</summary>
        public const float GapDays = 40f, KindGapDays = 90f;
        /// <summary>At most six peoples that arose stand at once.</summary>
        public const int MaxStanding = 6;
        /// <summary>The records kept, the oldest of a people no longer standing first.</summary>
        public const int MaxRecords = 24;
        /// <summary>The setting's range (0 switches it off; 2 halves the gaps).</summary>
        public const float MaxSetting = 2f;

        /// <summary>The days a gap lasts at this setting; never at 0.</summary>
        public static float Gap(float setting, float days)
        {
            if (float.IsNaN(setting) || setting <= 0.001f) return float.PositiveInfinity;
            return days / Math.Min(MaxSetting, setting);
        }

        /// <summary>
        /// A new people of this cause may arise now: the setting is on, the world is old enough, fewer than six that arose
        /// stand, and the gaps have passed since the last of any cause and the last of this one (-1: none yet).
        /// </summary>
        public static bool PaceAllows(float setting, int now, int lastAny, int lastOfKind, int standing)
        {
            if (float.IsNaN(setting) || setting <= 0.001f || now < (long)EarliestDay * DayTicks || standing >= MaxStanding) return false;
            if (lastAny >= 0 && now - lastAny < Gap(setting, GapDays) * DayTicks) return false;
            if (lastOfKind >= 0 && now - lastOfKind < Gap(setting, KindGapDays) * DayTicks) return false;
            return true;
        }

        /// <summary>Each cause is looked at every five days (spread by its id), on a roll.</summary>
        public const int LookEveryDays = 5;

        public static bool LookDay(int day, int id, int every = LookEveryDays) => every <= 1 || (day + (id & 0x7fffffff) % every) % every == 0;

        /// <summary>
        /// Whether a cause can give a people at all, the same every time it is asked (so no roll is kept): two fallen towns
        /// in five, half the war of words and of the people sent away. <paramref name="salt"/> keeps the causes apart.
        /// </summary>
        public static bool Fated(long id, int salt, float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            unchecked
            {
                uint h = (uint)id * 2654435761u ^ (uint)(id >> 32) * 2246822519u ^ (uint)salt * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h % 10000u) / 10000f < chance;
            }
        }

        public const int SaltRefugees = 101, SaltExiles = 202, SaltSplinter = 303;

        /// <summary>Two fallen towns in five leave survivors who settle as a people (the rest scatter to their kin).</summary>
        public const float RefugeesChance = 0.4f;
        /// <summary>From three days after the town fell (its ruin on the map) to sixty days.</summary>
        public const int RefugeesFromDays = 3, RefugeesWithinDays = 60;

        public static bool RefugeesDue(int fell, int now) => fell >= 0 && now - fell >= (long)RefugeesFromDays * DayTicks && now - fell <= (long)RefugeesWithinDays * DayTicks;

        /// <summary>Half the people the council sent away gather a band, from 15 days after they went to 300; a fifth of the looks when due.</summary>
        public const float ExilesChance = 0.5f, ExilesLookChance = 0.2f;
        public const int ExilesFromDays = 15, ExilesWithinDays = 300;

        public static bool ExilesDue(int banishedTick, bool cameBack, bool rode, int now) =>
            banishedTick >= 0 && !cameBack && !rode && now - banishedTick >= (long)ExilesFromDays * DayTicks && now - banishedTick <= (long)ExilesWithinDays * DayTicks;

        /// <summary>Half the wars of words over a faith leave faithful who break away, within 30 days of its end.</summary>
        public const float SplinterChance = 0.5f;
        public const int SplinterWithinDays = 30;

        public static bool SplinterDue(int ended, int now) => ended >= 0 && now - ended >= 0 && now - ended <= (long)SplinterWithinDays * DayTicks;

        /// <summary>A league of prospering towns is looked for every five days and forms on a fifth of the looks.</summary>
        public const float LeagueChance = 0.2f;
        /// <summary>Three towns (four at most) of two peoples or more, each at 62 or more, within 18 tiles of the most prosperous.</summary>
        public const float LeagueAt = 62f, LeagueTiles = 18f;
        public const int LeagueMin = 3, LeagueMost = 4, LeaguePeoples = 2;

        /// <summary>
        /// The towns of a league, by their index: the most prosperous town at <see cref="LeagueAt"/> or more and the most
        /// prosperous others within <see cref="LeagueTiles"/> of it, none of a people hostile to another's, up to four, when
        /// there are three or more of two peoples or more. Empty for none. <paramref name="tiles"/> is the distance between
        /// two towns, <paramref name="hostile"/> whether their peoples are at war.
        /// </summary>
        public static List<int> LeagueOf(IList<float> prosperity, IList<int> people, Func<int, int, float> tiles, Func<int, int, bool> hostile)
        {
            var none = new List<int>();
            if (prosperity == null || people == null || prosperity.Count != people.Count || tiles == null) return none;
            var order = Enumerable.Range(0, prosperity.Count).Where(i => prosperity[i] >= LeagueAt).OrderByDescending(i => prosperity[i]).ThenBy(i => i).ToList();
            foreach (int core in order)
            {
                var members = new List<int> { core };
                foreach (int i in order)
                {
                    if (i == core || members.Count >= LeagueMost || tiles(core, i) > LeagueTiles) continue;
                    if (hostile != null && members.Any(m => people[m] != people[i] && hostile(m, i))) continue;
                    members.Add(i);
                }
                if (members.Count >= LeagueMin && members.Select(m => people[m]).Distinct().Count() >= LeaguePeoples) return members;
            }
            return none;
        }

        /// <summary>The age of a league of towns of several peoples: the one most of them are of, the higher on a tie (0 tribal to 3 spacer).</summary>
        public static int MeanAge(IList<int> ages)
        {
            if (ages == null || ages.Count == 0) return 2;
            return ages.GroupBy(a => a).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
        }

        // ================= The template =================

        /// <summary>
        /// The callings a cause's people may take, the likeliest first: survivors take to the herds, a sickhouse or the
        /// road; a league of merchants is a road league, of herders a herding folk; a band of the banished herds on the
        /// margins or hires out on the roads; faithful who broke away keep a sickhouse or follow their herds.
        /// </summary>
        public static string[] Callings(string kind)
        {
            switch (kind)
            {
                case Refugees: return new[] { PeoplesMath.Herders, PeoplesMath.Healers, PeoplesMath.Traders };
                case League: return new[] { PeoplesMath.Traders, PeoplesMath.Herders };
                case Exiles: return new[] { PeoplesMath.Herders, PeoplesMath.Traders };
                case Splinter: return new[] { PeoplesMath.Healers, PeoplesMath.Herders };
                default: return new string[0];
            }
        }

        /// <summary>
        /// The template a cause's people takes (by index), for the cause's age (0 tribal to 3 spacer): among the templates
        /// of a calling the cause may take, the one nearest its age, the likelier calling on a tie. -1 for none.
        /// </summary>
        public static int Template(string kind, int age, IList<string> callings, IList<int> ages)
        {
            if (callings == null || ages == null || callings.Count != ages.Count) return -1;
            var wanted = Callings(kind);
            int best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < callings.Count; i++)
            {
                int rank = Array.IndexOf(wanted, callings[i]);
                if (rank < 0) continue;
                int score = Math.Abs(ages[i] - age) * 10 + rank;
                if (score >= bestScore) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }

        // ================= How they feel =================

        /// <summary>The game's own thresholds: at -75 a people turns hostile, at 75 it is an ally.</summary>
        public const int Hostile = -75, Ally = 75;

        /// <summary>Survivors and their old people, a league and its towns' peoples, faithful who broke away and those they left.</summary>
        public const int WithKin = 40, WithMembers = 50, WithParent = -30;

        /// <summary>Under this a new people's goodwill is told as cool, from this on as warm.</summary>
        public const int Cool = -15, Warm = 15;

        private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

        /// <summary>
        /// Survivors of a town toward the colony: cool (-40) when the colony's own people took it; else half the colony's
        /// goodwill with their old people, between -20 and 40, and never warm toward a colony their old people were at
        /// war with.
        /// </summary>
        public static int RefugeesGoodwill(bool byColony, int colonyWithOld, bool oldHostile)
        {
            if (byColony) return -40;
            int g = Clamp(colonyWithOld / 2, -20, 40);
            return oldHostile ? Math.Min(g, 0) : g;
        }

        /// <summary>A league toward the colony: 10 and half the average goodwill of its towns' peoples with it, between -10 and 50.</summary>
        public static int LeagueGoodwill(IList<int> membersWithColony)
        {
            if (membersWithColony == null || membersWithColony.Count == 0) return 10;
            return Clamp(10 + (int)Math.Round(membersWithColony.Average() / 2.0), -10, 50);
        }

        /// <summary>
        /// A band of the banished toward the colony, by how the banishment went: cool (-30) when they went with a grudge;
        /// else 10, and 8 for each of the colony who stood by them, less 8 for each who was glad to see them go, between
        /// -20 and 40.
        /// </summary>
        public static int ExilesGoodwill(bool grudge, int stoodBy, int glad)
        {
            if (grudge) return -30;
            return Clamp(10 + 8 * Math.Max(0, stoodBy) - 8 * Math.Max(0, glad), -20, 40);
        }

        /// <summary>Faithful who broke away toward the colony: warm (25) when the colony kept a fair table in the war of words, else 10.</summary>
        public static int SplinterGoodwill(bool debate) => debate ? 25 : 10;

        /// <summary>How a goodwill is told: -1 cool, 0 plain, 1 warm.</summary>
        public static int Feeling(int goodwill) => goodwill <= Cool ? -1 : goodwill >= Warm ? 1 : 0;

        /// <summary>Every starting goodwill stays clear of the game's thresholds: no new people is an enemy or an ally from its first day.</summary>
        public static bool AtPeace(int goodwill) => goodwill > Hostile && goodwill < Ally;

        // ================= The splinter's creed =================

        /// <summary>
        /// How the splinter's creed differs from their old faith, from its memes before and after (the game's own way of
        /// reforming a faith drops one or adds one): "dropped", "added", or null when nothing changed.
        /// </summary>
        public static string CreedChange(int dropped, int added) => dropped > 0 && added == 0 ? "dropped" : added > 0 && dropped == 0 ? "added" : dropped > 0 ? "changed" : null;

        // ================= Their names and colour =================

        /// <summary>A name Rimfolk gives (a town's name in it) is kept when it is this short and no other people has it.</summary>
        public const int MaxName = 24;

        public static bool NameFits(string name, IEnumerable<string> taken) =>
            !string.IsNullOrEmpty(name) && name.Length <= MaxName && (taken == null || !taken.Contains(name));

        /// <summary>
        /// The emblem colours a new people may take: twelve that stand apart from each other and from the three peoples'
        /// own (teal, ivory, russet).
        /// </summary>
        public static readonly float[][] Palette =
        {
            new[] { 0.58f, 0.24f, 0.56f }, // plum
            new[] { 0.29f, 0.47f, 0.82f }, // cornflower
            new[] { 0.86f, 0.68f, 0.18f }, // ochre
            new[] { 0.36f, 0.60f, 0.26f }, // moss
            new[] { 0.76f, 0.24f, 0.24f }, // madder
            new[] { 0.93f, 0.58f, 0.66f }, // rose
            new[] { 0.40f, 0.30f, 0.66f }, // indigo
            new[] { 0.66f, 0.78f, 0.32f }, // lichen
            new[] { 0.36f, 0.42f, 0.50f }, // slate
            new[] { 0.90f, 0.46f, 0.12f }, // saffron
            new[] { 0.16f, 0.36f, 0.22f }, // pine
            new[] { 0.72f, 0.62f, 0.88f }  // lavender
        };

        /// <summary>The palette's colour farthest from every colour the world's peoples have (the first on a tie).</summary>
        public static int Colour(IList<float[]> taken)
        {
            int best = 0;
            float bestD = -1f;
            for (int i = 0; i < Palette.Length; i++)
            {
                float d = float.MaxValue;
                foreach (var c in taken ?? new List<float[]>())
                {
                    if (c == null || c.Length < 3) continue;
                    float dr = Palette[i][0] - c[0], dg = Palette[i][1] - c[1], db = Palette[i][2] - c[2];
                    d = Math.Min(d, dr * dr + dg * dg + db * db);
                }
                if (d > bestD + 1e-6f)
                {
                    best = i;
                    bestD = d;
                }
            }
            return best;
        }
    }
}
