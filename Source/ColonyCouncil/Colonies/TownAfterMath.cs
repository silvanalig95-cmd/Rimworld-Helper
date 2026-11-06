using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony as it was left, kept at the hand-over (the player's decision 7A; Colonies/TownLayout.cs keeps and places
    /// it): over the built area (the home area's bounds and a margin), per cell the building whose origin is there (an
    /// edifice in <see cref="things"/>, anything else, a conduit or a lamp, in <see cref="under"/>; each its def and stuff
    /// by a number in <see cref="table"/> and its rotation), the floor laid, the foundation (a bridge, a substructure), a
    /// built roof and the rock that is gone (mined out, or never there). No game types, so Tools/Tests can check it.
    /// </summary>
    public sealed class LayoutGrid
    {
        /// <summary>The kept rectangle: its corner on the map and its size, and the size of the map it was kept from.</summary>
        public int originX, originZ, width, height, mapX, mapZ;

        /// <summary>The defs by number, from 1: "b:Wall|WoodLog" for a building and its stuff, "t:WoodPlankFloor" for a terrain.</summary>
        public List<string> table = new List<string>();

        /// <summary>Per cell (z * width + x): the edifice whose origin is there and its rotation (LayoutMath.Pack), or 0.</summary>
        public ushort[] things;

        /// <summary>Per cell: another building whose origin is there (a conduit, a lamp, a vent), or 0.</summary>
        public ushort[] under;

        /// <summary>Per cell: the floor laid there (a terrain's number), or 0.</summary>
        public ushort[] floors;

        /// <summary>Per cell: the foundation under it (a bridge, a substructure), or 0.</summary>
        public ushort[] bases;

        /// <summary>Per cell: LayoutMath.Roof (a roof the colony built), LayoutMath.Clear (no natural rock there).</summary>
        public byte[] flags;

        /// <summary>The cells (local index) of the beds of those who stayed, in the order of the people who sleep in them.</summary>
        public List<int> beds = new List<int>();

        public int Cells => Math.Max(0, width) * Math.Max(0, height);

        public int Index(int x, int z) => z * width + x;

        /// <summary>A grid of this size, every layer empty.</summary>
        public static LayoutGrid Make(int originX, int originZ, int width, int height, int mapX, int mapZ)
        {
            var g = new LayoutGrid { originX = originX, originZ = originZ, width = Math.Max(0, width), height = Math.Max(0, height), mapX = mapX, mapZ = mapZ };
            int n = g.Cells;
            g.things = new ushort[n];
            g.under = new ushort[n];
            g.floors = new ushort[n];
            g.bases = new ushort[n];
            g.flags = new byte[n];
            return g;
        }
    }

    /// <summary>
    /// The rules of the kept layout (docs/design/stage3-many-colonies.md, section 2, "Keeping the layout"; release 2b):
    /// the rectangle kept, the numbered grids against a table of def names, the bytes they are saved as (deflated, as the
    /// game saves its own grids), the size cap and what is dropped past it (roofs, then floors, then the lesser
    /// buildings), and where the layout goes on a map of another size.
    /// </summary>
    public static class LayoutMath
    {
        /// <summary>Cells kept beyond the home area's bounds on every side.</summary>
        public const int Margin = 6;

        /// <summary>The most a kept layout may take in the save: base64 of the deflated grids, with the table.</summary>
        public const int MaxBytes = 64 * 1024;

        /// <summary>A mid-game base takes about this much (for the design's word, and the save model).</summary>
        public const int TypicalBytes = 20480;

        /// <summary>The flags of a cell: a roof the colony built, no natural rock there (mined out, or open ground).</summary>
        public const byte Roof = 1, Clear = 2;

        /// <summary>A building's number takes 14 bits and its rotation 2: at most this many entries in the table.</summary>
        public const int MaxTable = 16383;

        public const string BuildingPrefix = "b:", TerrainPrefix = "t:";

        /// <summary>The kept rectangle: the home area's bounds (min and max, inclusive) and a margin, inside the map. Width or height 0: nothing to keep.</summary>
        public static void Rect(int minX, int minZ, int maxX, int maxZ, int margin, int mapX, int mapZ, out int x, out int z, out int w, out int h)
        {
            if (maxX < minX || maxZ < minZ || mapX <= 0 || mapZ <= 0)
            {
                x = z = w = h = 0;
                return;
            }
            int x0 = Math.Max(0, minX - margin), z0 = Math.Max(0, minZ - margin);
            int x1 = Math.Min(mapX - 1, maxX + margin), z1 = Math.Min(mapZ - 1, maxZ + margin);
            x = x0;
            z = z0;
            w = Math.Max(0, x1 - x0 + 1);
            h = Math.Max(0, z1 - z0 + 1);
        }

        // ---------------- The table ----------------

        public static string BuildingEntry(string def, string stuff) => BuildingPrefix + def + (string.IsNullOrEmpty(stuff) ? "" : "|" + stuff);

        public static string TerrainEntry(string def) => TerrainPrefix + def;

        public static bool ParseBuilding(string entry, out string def, out string stuff)
        {
            def = stuff = null;
            if (entry == null || !entry.StartsWith(BuildingPrefix, StringComparison.Ordinal)) return false;
            string body = entry.Substring(BuildingPrefix.Length);
            int bar = body.IndexOf('|');
            def = bar < 0 ? body : body.Substring(0, bar);
            stuff = bar < 0 ? null : body.Substring(bar + 1);
            return def.Length > 0;
        }

        public static bool ParseTerrain(string entry, out string def)
        {
            def = null;
            if (entry == null || !entry.StartsWith(TerrainPrefix, StringComparison.Ordinal)) return false;
            def = entry.Substring(TerrainPrefix.Length);
            return def.Length > 0;
        }

        /// <summary>The entry's number in the table (from 1), added when new; 0 when the table is full. <paramref name="index"/> speeds the look-up.</summary>
        public static int Add(LayoutGrid g, string entry, Dictionary<string, int> index)
        {
            if (g == null || string.IsNullOrEmpty(entry)) return 0;
            if (index != null && index.TryGetValue(entry, out int known)) return known;
            int i = g.table.IndexOf(entry);
            if (i >= 0)
            {
                if (index != null) index[entry] = i + 1;
                return i + 1;
            }
            if (g.table.Count >= MaxTable) return 0;
            g.table.Add(entry);
            if (index != null) index[entry] = g.table.Count;
            return g.table.Count;
        }

        /// <summary>The entry a number names, or null.</summary>
        public static string Entry(LayoutGrid g, int number) => g != null && number >= 1 && number <= g.table.Count ? g.table[number - 1] : null;

        /// <summary>A building's number and rotation (0 north, 1 east, 2 south, 3 west) in one value.</summary>
        public static ushort Pack(int number, int rot) => (ushort)((Math.Max(0, Math.Min(MaxTable, number)) << 2) | (rot & 3));

        public static int NumberOf(ushort packed) => packed >> 2;

        public static int RotOf(ushort packed) => packed & 3;

        // ---------------- The bytes ----------------

        /// <summary>A layer as the bytes it is saved as (two a cell, low byte first), or null for none.</summary>
        public static byte[] Bytes(ushort[] layer)
        {
            if (layer == null) return null;
            var b = new byte[layer.Length * 2];
            for (int i = 0; i < layer.Length; i++)
            {
                b[i * 2] = (byte)(layer[i] & 0xFF);
                b[i * 2 + 1] = (byte)(layer[i] >> 8);
            }
            return b;
        }

        /// <summary>A layer back from its bytes, <paramref name="cells"/> long (short bytes leave the rest empty), or null.</summary>
        public static ushort[] Shorts(byte[] b, int cells)
        {
            if (b == null || cells <= 0) return null;
            var s = new ushort[cells];
            for (int i = 0; i < cells && i * 2 + 1 < b.Length; i++) s[i] = (ushort)(b[i * 2] | (b[i * 2 + 1] << 8));
            return s;
        }

        /// <summary>A layer holds anything.</summary>
        public static bool Any(ushort[] layer)
        {
            if (layer == null) return false;
            for (int i = 0; i < layer.Length; i++)
                if (layer[i] != 0)
                    return true;
            return false;
        }

        public static bool Any(byte[] flags, byte bit)
        {
            if (flags == null) return false;
            for (int i = 0; i < flags.Length; i++)
                if ((flags[i] & bit) != 0)
                    return true;
            return false;
        }

        /// <summary>Nothing worth keeping: no building at all.</summary>
        public static bool Empty(LayoutGrid g) => g == null || g.Cells == 0 || !Any(g.things) && !Any(g.under);

        /// <summary>
        /// What one layer takes in the save, the way the game saves a grid (DataExposeUtility.LookByteArray): the deflated
        /// bytes when they are smaller, else the bytes as they are, as base64 text.
        /// </summary>
        public static int SavedSize(byte[] raw, Func<byte[], byte[]> deflate)
        {
            if (raw == null) return 0;
            int n = raw.Length;
            if (deflate != null)
            {
                var d = deflate(raw);
                if (d != null && d.Length < n) n = d.Length;
            }
            return 4 * ((n + 2) / 3);
        }

        /// <summary>The whole layout in the save: its five layers, the beds and the table's names.</summary>
        public static int SavedSize(LayoutGrid g, Func<byte[], byte[]> deflate)
        {
            if (g == null) return 0;
            int size = SavedSize(Bytes(g.things), deflate) + SavedSize(Bytes(g.under), deflate) + SavedSize(Bytes(g.floors), deflate) + SavedSize(Bytes(g.bases), deflate)
                       + SavedSize(g.flags, deflate);
            foreach (var e in g.table) size += (e?.Length ?? 0) + 9;
            size += g.beds.Count * 12;
            return size + 200;
        }

        /// <summary>What is let go past the cap, in order.</summary>
        public const string DropRoofs = "roofs", DropFloors = "floors", DropUnder = "under", DropAll = "all";

        /// <summary>
        /// Fits the layout under the cap: roofs go first, then the floors and foundations, then the lesser buildings
        /// (conduits, lamps); if it still doesn't fit, nothing is kept (the game makes its ordinary base). Returns what was
        /// let go, in order.
        /// </summary>
        public static List<string> Fit(LayoutGrid g, Func<byte[], byte[]> deflate, int cap = MaxBytes)
        {
            var dropped = new List<string>();
            if (g == null) return dropped;
            if (SavedSize(g, deflate) <= cap) return dropped;
            if (g.flags != null)
                for (int i = 0; i < g.flags.Length; i++)
                    g.flags[i] = (byte)(g.flags[i] & ~Roof);
            dropped.Add(DropRoofs);
            if (SavedSize(g, deflate) <= cap) return dropped;
            g.floors = null;
            g.bases = null;
            dropped.Add(DropFloors);
            if (SavedSize(g, deflate) <= cap) return dropped;
            g.under = null;
            dropped.Add(DropUnder);
            if (SavedSize(g, deflate) <= cap) return dropped;
            g.things = null;
            g.flags = null;
            g.beds.Clear();
            dropped.Add(DropAll);
            return dropped;
        }

        // ---------------- Where it goes ----------------

        /// <summary>
        /// Where the kept rectangle's corner goes on a map of another size: where it was when it fits, else moved in just
        /// enough to fit (a map too small for it keeps its corner at 0).
        /// </summary>
        public static int Shift(int origin, int length, int mapSize)
        {
            if (origin + length <= mapSize) return origin;
            return Math.Max(0, mapSize - length);
        }
    }

    /// <summary>What a town the colony was keeps of its days with the colony: none, its founding day, the day of the hand-over.</summary>
    public enum TownDay
    {
        None,
        Founding,
        HandOver
    }

    /// <summary>How a town the colony was stopped being its people's: taken back by the colony's letter, beaten by force, settled again after.</summary>
    public enum TownEnd
    {
        None,
        TakenBack,
        Beaten,
        Settled
    }

    /// <summary>
    /// The rules of the town the colony was, after the hand-over (docs/design/stage3-many-colonies.md, section 2, "What it
    /// does afterwards" and "Taking it back"; release 2b, the player's decisions 7A and 10A), with no game types so
    /// Tools/Tests can check them: its head's letters, its days and the invitation to one, who may ask to come home and
    /// how often, who may ask to go and live there, the answer to the colony's letter asking for it back and who comes
    /// with it, and what taking it by force costs.
    /// </summary>
    public static class FormerMath
    {
        public const int DayTicks = 60000, SeasonDays = 15, YearDays = 60;

        public const int SeasonTicks = SeasonDays * DayTicks, YearTicks = YearDays * DayTicks;

        /// <summary>Whole years since a tick.</summary>
        public static int YearsSince(int tick, int now) => tick < 0 || now < tick ? 0 : (now - tick) / YearTicks;

        /// <summary>Years since, with the part of a year.</summary>
        public static float YearsSinceExact(int tick, int now) => tick < 0 || now < tick ? 0f : (now - tick) / (float)YearTicks;

        // ---------------- The head's letters ----------------

        /// <summary>The first letter comes no sooner than this many days after the hand-over, and the next a season after the last at the soonest.</summary>
        public const int FirstLetterDays = 5, LetterGapDays = SeasonDays;

        /// <summary>A letter's chance a day once it may come (about a week and a half's wait on average).</summary>
        public const float LetterChance = 0.1f;

        /// <summary>The head writes today: past the first days, a season since the last letter, and the day's roll.</summary>
        public static bool LetterDue(int handedAt, int lastLetter, int now, float roll)
        {
            if (handedAt < 0 || now - handedAt < FirstLetterDays * DayTicks) return false;
            if (lastLetter >= 0 && now - lastLetter < LetterGapDays * DayTicks) return false;
            return roll < LetterChance;
        }

        /// <summary>What a letter of theirs is about, as the tag its lines are chosen by: their town's fortune band, or the years since the hand-over on its day.</summary>
        public static string LetterTag(float prosperity) =>
            prosperity < 0f ? "steady" : prosperity < 20f ? "struggling" : prosperity < 40f ? "poor" : prosperity < 60f ? "steady" : prosperity < 80f ? "thriving" : "flourishing";

        // ---------------- Its days ----------------

        /// <summary>At most one invitation a year from the town, this many days before its day.</summary>
        public const int InviteGapDays = YearDays, InviteLeadDays = 5;

        /// <summary>The town's day is this many days ahead (0 to <see cref="InviteLeadDays"/>): the next anniversary of <paramref name="day"/>, or -1 if not soon.</summary>
        public static int DaysToAnniversary(int day, int now)
        {
            if (day < 0 || now < day) return -1;
            int into = (now - day) % YearTicks;
            int left = into == 0 ? 0 : YearTicks - into;
            int days = left / DayTicks;
            return days <= InviteLeadDays ? days : -1;
        }

        /// <summary>
        /// The day the town invites the colony to now, or none: its founding day (the colony's) or the day of the hand-over,
        /// whichever comes up first, never in the hand-over's first year for the hand-over's day nor the first season for the
        /// founding day, and at most once a year.
        /// </summary>
        public static TownDay DayComing(int founded, int handedAt, int now, int lastInvite)
        {
            if (handedAt < 0 || now < handedAt) return TownDay.None;
            if (lastInvite >= 0 && now - lastInvite < InviteGapDays * DayTicks) return TownDay.None;
            int toHanded = now - handedAt >= YearTicks - InviteLeadDays * DayTicks ? DaysToAnniversary(handedAt, now) : -1;
            int toFounded = founded >= 0 && now - handedAt >= SeasonTicks ? DaysToAnniversary(founded, now) : -1;
            if (toHanded < 0 && toFounded < 0) return TownDay.None;
            if (toFounded >= 0 && (toHanded < 0 || toFounded <= toHanded)) return TownDay.Founding;
            return TownDay.HandOver;
        }

        // ---------------- Homecomings ----------------

        /// <summary>No homecoming asked in the town's first season, and at most one a season from each town.</summary>
        public const int HomecomingFirstDays = SeasonDays, HomecomingGapDays = SeasonDays;

        /// <summary>A day's chance that someone who would ask, asks: with their spouse or child in the colony, a close friend, or a town struggling.</summary>
        public const float HomeKinChance = 0.05f, HomeFriendChance = 0.03f, HomeHardChance = 0.04f;

        /// <summary>
        /// Someone of the colony's former people may ask to come home: never the head, only one of the colony's own, and
        /// only for a reason that is there (their spouse, child or close friend in the colony, or their town struggling).
        /// </summary>
        public static bool WouldAsk(bool head, bool former, bool kin, bool friend, bool struggling) => !head && former && (kin || friend || struggling);

        /// <summary>The day's chance of a homecoming asked from this town, by the best reason among those who would ask (0 for none).</summary>
        public static float HomecomingChance(bool kin, bool friend, bool struggling) =>
            kin ? HomeKinChance : friend ? HomeFriendChance : struggling ? HomeHardChance : 0f;

        /// <summary>A homecoming may be asked from this town today: past its first season, a season since the last, and the day's roll.</summary>
        public static bool HomecomingDue(int handedAt, int lastAsk, int now, float chance, float roll)
        {
            if (handedAt < 0 || now - handedAt < HomecomingFirstDays * DayTicks) return false;
            if (lastAsk >= 0 && now - lastAsk < HomecomingGapDays * DayTicks) return false;
            return roll < chance;
        }

        /// <summary>A close friend for a homecoming or a move: one they think this well of, both ways.</summary>
        public const int CloseFriendOpinion = 40;

        public static bool CloseFriends(int opinionAB, int opinionBA) => opinionAB >= CloseFriendOpinion && opinionBA >= CloseFriendOpinion;

        // ---------------- Moving there ----------------

        /// <summary>A colonist's own people in the town: a spouse, a child or a close friend among those who stayed.</summary>
        public static bool KinThere(bool spouse, bool child, bool closeFriend) => spouse || child || closeFriend;

        /// <summary>How much they want to go, 0 to 1: a spouse most, a child next, a close friend by how well they think of them.</summary>
        public static float MoveStrength(bool spouse, bool child, int opinion)
        {
            float s = spouse ? 0.8f : child ? 0.65f : 0.35f + Math.Max(0, Math.Min(100, opinion)) / 400f;
            return Math.Max(0f, Math.Min(1f, s));
        }

        // ---------------- Taking it back peacefully ----------------

        /// <summary>The colony asks for it back at most once a year: a letter refused waits this long.</summary>
        public const int AskAgainDays = YearDays;

        /// <summary>
        /// How much it has grown since the hand-over, 0 to 1: its fortune risen over what it started with, and towns of its
        /// own besides (a quarter each).
        /// </summary>
        public static float Grown(float seed, float prosperity, int otherTowns)
        {
            float rise = seed >= 0f && prosperity >= 0f ? Math.Max(0f, prosperity - seed) / 50f : 0f;
            return Math.Max(0f, Math.Min(1f, rise + Math.Max(0, otherTowns) * 0.25f));
        }

        /// <summary>The most and least likely a yes ever is.</summary>
        public const float TakeBackMost = 0.9f, TakeBackLeast = 0.02f;

        /// <summary>
        /// The head's answer to the colony's letter asking for the town back, as a chance of yes: their regard of the colony
        /// (-100 to 100), their people's goodwill, the town's fortune (a struggling town is readier, a flourishing one less),
        /// the years since (it gets used to standing on its own) and how much it has grown.
        /// </summary>
        public static float TakeBackChance(int regard, int goodwill, float prosperity, float years, float grown)
        {
            float p = 0.15f + 0.30f * Clamp(regard, -100, 100) / 100f + 0.25f * Clamp(goodwill, -100, 100) / 100f;
            if (prosperity >= 0f) p += 0.30f * (50f - Math.Max(0f, Math.Min(100f, prosperity))) / 50f;
            p -= 0.10f * Math.Max(0f, Math.Min(5f, years));
            p -= 0.35f * Math.Max(0f, Math.Min(1f, grown));
            return Math.Max(TakeBackLeast, Math.Min(TakeBackMost, p));
        }

        /// <summary>Up to this many of the town's other people choose to come with it.</summary>
        public const int MaxJoiners = 3;

        /// <summary>The least that brings one of them along: their regard of the colony's people, their kin among them, the town's hard times.</summary>
        public const float JoinLine = 25f;

        /// <summary>One of the town's other people's wish to join the colony: their best regard of someone of the colony's, kin among them, the town's hard times.</summary>
        public static float JoinScore(int bestOpinion, bool kin, float prosperity) =>
            Clamp(bestOpinion, -100, 100) + (kin ? 30f : 0f) + (prosperity >= 0f && prosperity < 30f ? 15f : 0f);

        /// <summary>Who of them comes: the best at or past the line, <see cref="MaxJoiners"/> at most, in order.</summary>
        public static List<int> Joiners(IReadOnlyList<float> scores)
        {
            var list = new List<int>();
            if (scores == null) return list;
            for (int i = 0; i < scores.Count; i++)
                if (scores[i] >= JoinLine)
                    list.Add(i);
            list.Sort((a, b) => scores[b].CompareTo(scores[a]) != 0 ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
            if (list.Count > MaxJoiners) list.RemoveRange(MaxJoiners, list.Count - MaxJoiners);
            return list;
        }

        /// <summary>Its former colonists who come back with it: those who are its people still and free to (not the dead, not held by others).</summary>
        public const int MaxFormersOnMap = 12;

        // ---------------- Taking it by force ----------------

        /// <summary>The deed the town's people remember of a town the colony was taken by force, and every people of the same faith (a heavy wrong).</summary>
        public const string DeedTaken = "townTaken", DeedTakenFaith = "townTakenFaith";

        /// <summary>Its weight: theirs as heavy as a town destroyed, the faith's lighter.</summary>
        public const int TakenWeight = 45, TakenFaithWeight = 20;

        /// <summary>The ledger's grudge of a town taken by force: its origin.</summary>
        public const string GrudgeOrigin = "townTaken";

        /// <summary>
        /// What became of a town the colony was that is no longer its people's, read once from what is on the world: taken
        /// back peacefully (the colony's again by their word), beaten (its settlement gone, the game's ruin of a beaten town
        /// at its place), settled (the colony's home again on that place after being beaten), or none.
        /// </summary>
        public static TownEnd Became(TownEnd was, bool settlementStands, bool settlementOurs, bool destroyedHere, bool homeHere)
        {
            if (was == TownEnd.TakenBack || was == TownEnd.Settled) return was;
            if (was == TownEnd.Beaten) return homeHere ? TownEnd.Settled : TownEnd.Beaten;
            if (settlementStands) return TownEnd.None;
            if (destroyedHere) return TownEnd.Beaten;
            return homeHere ? TownEnd.Settled : TownEnd.None;
        }

        private static float Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
