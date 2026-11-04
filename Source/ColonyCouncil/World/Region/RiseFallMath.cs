using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of towns that rise and fall (docs/design/stage1-region.md, item 2; decisions 2, 5 and 14): when a
    /// struggling town is given up, when a ruin is settled again, when a flourishing town sends settlers out to found a
    /// town of Rimfolk's own, where such a town may stand, how long a ruin Rimfolk made lasts, the pace of change in a
    /// home's reach, and what a town's fortune does to its caravans and its asks. Pure (no game types): Tools/Tests
    /// (RiseFallTests) checks them.
    /// </summary>
    public static class RiseFallMath
    {
        public const int DayTicks = 60000;

        // ---------------- The pace of change (decision 14) ----------------

        /// <summary>At most one town given up in a home's reach in so many days.</summary>
        public const int GiveUpGapDays = 60;
        /// <summary>At most one ruin settled again in a home's reach in so many days.</summary>
        public const int ResettleGapDays = 120;
        /// <summary>At most one town founded by Rimfolk in a home's reach in so many days (decision 2: beside another mod's builders).</summary>
        public const int FoundGapDays = 90;

        /// <summary>A home's clock lets another change of its kind come: none at that home in the last <paramref name="gapDays"/> (-1: never).</summary>
        public static bool GapPassed(int last, int now, int gapDays) => last < 0 || now - last >= (long)gapDays * DayTicks;

        /// <summary>
        /// A change may come at a place: every home whose reach holds it (their clocks of that kind, in
        /// <paramref name="lastAtHomes"/>) has had none in the gap. A place within no home's reach: never (beyond the reach
        /// nothing changes).
        /// </summary>
        public static bool PaceAllows(IList<int> lastAtHomes, int now, int gapDays)
        {
            if (lastAtHomes == null || lastAtHomes.Count == 0) return false;
            for (int i = 0; i < lastAtHomes.Count; i++)
                if (!GapPassed(lastAtHomes[i], now, gapDays))
                    return false;
            return true;
        }

        /// <summary>A town's own roll comes every <paramref name="every"/> days, each town on a day of its own (by its id).</summary>
        public static bool RollDay(int day, int id, int every) => every <= 1 || (day + (id & 0x7fffffff) % every) % every == 0;

        // ---------------- A town given up ----------------

        /// <summary>Under this a town is low (RegionMath.Low); low for <see cref="LowDays"/>, it may be given up.</summary>
        public const float Low = 15f;
        public const int LowDays = 30;
        /// <summary>Under this, a town struck by <see cref="Blows"/> bad events of its people's in <see cref="BlowDays"/> may be given up at once.</summary>
        public const float BlowLow = 25f;
        public const int Blows = 2;
        public const int BlowDays = 60;
        /// <summary>A new town that meets a hard first season (under this in its first <see cref="FirstSeasonDays"/>) is given up too.</summary>
        public const float NewTownFails = 10f;
        public const int FirstSeasonDays = 15;
        /// <summary>A town that could be given up is looked at every so many days, and given up on this roll.</summary>
        public const int GiveUpEveryDays = 5;
        public const float GiveUpChance = 0.07f;
        /// <summary>Its people keep at least this many towns besides it (the game's own rule keeps one; Rimfolk keeps two).</summary>
        public const int TownsLeft = 2;
        /// <summary>Given up quietly (no world event's slot free), the ruin stands after so many days.</summary>
        public const int QuietDays = 5;

        /// <summary>
        /// Struggling long enough to be given up: under <see cref="Low"/> for <see cref="LowDays"/>; or under
        /// <see cref="BlowLow"/> when a second bad event (or more) of its people's struck within <see cref="BlowDays"/>;
        /// or a new town under <see cref="NewTownFails"/> in its first season. <paramref name="lowSince"/> and
        /// <paramref name="founded"/> are ticks, -1 for never or not known.
        /// </summary>
        public static bool Struggling(float prosperity, int lowSince, int blows, int founded, int now)
        {
            if (prosperity < 0f) return false;
            if (prosperity < Low && lowSince >= 0 && now - lowSince >= (long)LowDays * DayTicks) return true;
            if (prosperity < BlowLow && blows >= Blows) return true;
            return founded >= 0 && now >= founded && now - founded < (long)FirstSeasonDays * DayTicks && prosperity < NewTownFails;
        }

        /// <summary>Its people keep <see cref="TownsLeft"/> towns or more besides it (<paramref name="peopleTowns"/> counts it too).</summary>
        public static bool KeepsEnough(int peopleTowns) => peopleTowns - 1 >= TownsLeft;

        /// <summary>
        /// A struggling town may be given up: not its people's seat, not the Empire's, its people keep two other towns or
        /// more, and safe to take off the map (<paramref name="safe"/>: no map of it, no quest about it, no caravan or pods
        /// on their way, no caravan request open).
        /// </summary>
        public static bool MayGiveUp(bool struggling, int peopleTowns, bool seat, bool empire, bool safe) =>
            struggling && !seat && !empire && safe && KeepsEnough(peopleTowns);

        // ---------------- Ruins ----------------

        /// <summary>A ruin Rimfolk made that nobody settles again goes after so many days ("in ten years a story").</summary>
        public const int RuinDays = 300;
        /// <summary>Ruins kept in a home's reach at most: the oldest goes first.</summary>
        public const int MaxRuinsPerHome = 12;
        /// <summary>A town destroyed in a fight: its ruin waits for the game's destroyed settlement to go, so long at most.</summary>
        public const int FightWaitDays = 30;
        /// <summary>Fights' ruins waiting at once, at most.</summary>
        public const int MaxWaiting = 6;

        /// <summary>A ruin made at <paramref name="since"/> has had its days.</summary>
        public static bool RuinFaded(int since, int now) => since >= 0 && now - since >= (long)RuinDays * DayTicks;

        /// <summary>A fight's ruin waiting since <paramref name="since"/> waits no longer.</summary>
        public static bool WaitOver(int since, int now) => since >= 0 && now - since >= (long)FightWaitDays * DayTicks;

        /// <summary>
        /// Which ruins in a home's reach go so that <paramref name="max"/> stay: indexes into <paramref name="since"/> (when
        /// each was made), the oldest first; equals in the order they came.
        /// </summary>
        public static List<int> OverCap(IList<int> since, int max = MaxRuinsPerHome)
        {
            var gone = new List<int>();
            int n = since?.Count ?? 0;
            if (n <= max) return gone;
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => since[a] != since[b] ? since[a].CompareTo(since[b]) : a.CompareTo(b));
            for (int i = 0; i < n - Math.Max(0, max); i++) gone.Add(order[i]);
            return gone;
        }

        // ---------------- A ruin settled again ----------------

        /// <summary>A ruin this old may be settled again.</summary>
        public const int ResettleRuinDays = 120;
        /// <summary>By a people at peace with the colony whose town within this many tiles stands at <see cref="ResettleAt"/> or more (their own people at <see cref="ResettleOwnAt"/>).</summary>
        public const float ResettleTiles = 25f;
        public const float ResettleAt = 60f;
        public const float ResettleOwnAt = 50f;
        public const int ResettleEveryDays = 15;
        public const float ResettleChance = 0.10f;
        /// <summary>Newcomers (not the ruin's own people) give the town a name of their own this often.</summary>
        public const float NewNameChance = 0.5f;

        public static bool RuinReady(int since, int now) => since >= 0 && now - since >= (long)ResettleRuinDays * DayTicks;

        /// <summary>A people may settle the ruin: at peace with the colony, its best town within reach of the ruin at 60 or more (the ruin's own people at 50).</summary>
        public static bool MayResettle(bool atPeace, bool ownPeople, float bestNearby) => atPeace && bestNearby >= (ownPeople ? ResettleOwnAt : ResettleAt);

        /// <summary>
        /// Who settles the ruin, of those that may (<paramref name="may"/>): its own people first, else the one whose town
        /// nearby stands best (the first of equals). -1 for nobody.
        /// </summary>
        public static int Settlers(IList<bool> may, IList<bool> own, IList<float> bestNearby)
        {
            int n = may?.Count ?? 0, pick = -1;
            for (int i = 0; i < n; i++)
            {
                if (!may[i]) continue;
                bool o = own != null && i < own.Count && own[i];
                if (o) return i;
                float b = bestNearby != null && i < bestNearby.Count ? bestNearby[i] : 0f;
                if (pick < 0 || b > (bestNearby != null && pick < bestNearby.Count ? bestNearby[pick] : 0f)) pick = i;
            }
            return pick;
        }

        // ---------------- Rimfolk's own new towns (decision 2) ----------------

        /// <summary>A town flourishing (this or more) for <see cref="FoundDays"/> may send settlers out.</summary>
        public const float FoundAt = 80f;
        public const int FoundDays = 60;
        public const int FoundEveryDays = 15;
        public const float FoundChance = 0.15f;
        /// <summary>The new town stands this far from the town its settlers came from.</summary>
        public const float FoundMinTiles = 6f, FoundMaxTiles = 15f;
        /// <summary>Only while fewer towns than this stand in a home's reach.</summary>
        public const int FoundRoom = 10;

        /// <summary>Since when a town is flourishing: kept while it stays at <see cref="FoundAt"/> or more, from now when it gets there, -1 once it isn't.</summary>
        public static int HighSince(float prosperity, int was, int now) => prosperity >= FoundAt ? (was >= 0 ? was : now) : -1;

        /// <summary>Flourishing for <see cref="FoundDays"/>: it may send settlers out.</summary>
        public static bool MaySendSettlers(float prosperity, int highSince, int now) =>
            prosperity >= FoundAt && highSince >= 0 && now - highSince >= (long)FoundDays * DayTicks;

        /// <summary>A home's reach has room for one more town: fewer than <see cref="FoundRoom"/> stand in it.</summary>
        public static bool RoomInReach(int towns) => towns < FoundRoom;

        // ---------------- Where a town Rimfolk places may stand ----------------

        /// <summary>
        /// Far enough from every home that the game holds the town's nearness against nobody: it takes goodwill every
        /// season from a people with a town within <paramref name="goodwillTiles"/> of a player's home (5), so what Rimfolk
        /// places stands more than that and a tile and a half in a straight line (6 or more on foot). <paramref name="nearestHome"/>:
        /// -1 for no home on that layer.
        /// </summary>
        public static bool ClearOfHomes(float nearestHome, int goodwillTiles) => nearestHome < 0f || nearestHome > goodwillTiles + 1.5f;

        /// <summary>
        /// A tile a new town of Rimfolk's may stand on: the game holds it valid for a town (<paramref name="valid"/>: its
        /// biome can hold one, not impassable, no town on it or next to it, nothing else on it), 6 to 15 tiles from the
        /// town its settlers came from, within a home's reach and clear of every home, on land where the game would put a
        /// town at all (its biome's settlement weight above 0).
        /// </summary>
        public static bool FoundingTile(bool valid, float fromMother, float nearestHome, float reach, int goodwillTiles, float settlementWeight) =>
            valid && fromMother >= FoundMinTiles && fromMother <= FoundMaxTiles && nearestHome >= 0f && nearestHome <= reach
            && ClearOfHomes(nearestHome, goodwillTiles) && settlementWeight > 0f;

        /// <summary>A ruin's tile may hold a town again: still valid for one, within a home's reach and clear of every home.</summary>
        public static bool ResettleTile(bool valid, float nearestHome, float reach, int goodwillTiles) =>
            valid && nearestHome >= 0f && nearestHome <= reach && ClearOfHomes(nearestHome, goodwillTiles);

        /// <summary>
        /// One of the tiles, weighed by the game's own sense of where towns stand (each biome's settlement weight: none on
        /// an ice sheet, little in a desert, most in a temperate forest), by a roll of 0 to 1. -1 when none weighs anything.
        /// </summary>
        public static int PickTile(IList<float> weights, float roll)
        {
            int n = weights?.Count ?? 0;
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Math.Max(0f, weights[i]);
            if (sum <= 0f) return -1;
            float at = Math.Max(0f, Math.Min(0.999999f, roll)) * sum;
            for (int i = 0; i < n; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f) continue;
                if (at < w) return i;
                at -= w;
            }
            for (int i = n - 1; i >= 0; i--)
                if (weights[i] > 0f)
                    return i;
            return -1;
        }

        // ---------------- What a fortune does ----------------

        /// <summary>A people's trader caravans come this much more often while their town nearest the home thrives or flourishes.</summary>
        public const float CaravansThriving = 1.25f;
        /// <summary>And this much as often while it struggles.</summary>
        public const float CaravansStruggling = 0.6f;
        /// <summary>A struggling town asks for help (provisions, rebuilding, sheltering its refugees) this much more often, and its hungry come to the gate as often.</summary>
        public const float HardTimes = 2f;
        /// <summary>When a struggling town's hungry come to the gate (a refugee family's camp), how much likelier they are its people than another's.</summary>
        public const float HardTimesPeople = 6f;

        /// <summary>How often a people's trader caravans come, by their town nearest the home: 1.25 thriving or flourishing, 0.6 struggling, else as the game has it.</summary>
        public static float CaravanFactor(Fortune? nearest)
        {
            if (nearest == null) return 1f;
            if (nearest == Fortune.Struggling) return CaravansStruggling;
            return nearest >= Fortune.Thriving ? CaravansThriving : 1f;
        }
    }
}
