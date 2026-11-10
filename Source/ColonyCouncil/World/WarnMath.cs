using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a warning to a people is about.</summary>
    public enum ThreatKind
    {
        /// <summary>Raiders of another people who struck a home lately, or are coming (a raid on its way, a camp of theirs standing).</summary>
        Raiders,
        /// <summary>A hostile site near one of their towns (a camp, an outpost of a people at war with them).</summary>
        Site,
        /// <summary>A war or trouble on the roads near them: a warlord, the clans gathering, a bandit road, the warlord's son's band, a war between neighbours.</summary>
        War,
        /// <summary>A hardship spreading near them (a plague, a sickness of the herds, locusts, a blight, a great fire): a lesser warning.</summary>
        Hardship
    }

    /// <summary>Why a threat the colony knows of is no warning to a people now.</summary>
    public enum WarnWhy
    {
        None,
        /// <summary>Its people are no enemy of theirs (at peace with them), or it is their own.</summary>
        NotTheirEnemy,
        /// <summary>Nothing of it lies near enough to any of their towns.</summary>
        TooFar,
        /// <summary>The raid is too long ago and nothing of theirs is coming.</summary>
        TooOld,
        /// <summary>The threat is over (the raiders gone, the site cleared, the event ended).</summary>
        Ended,
        /// <summary>They were warned of it already (by any letter to any of them).</summary>
        Warned,
        /// <summary>The colony has neither seen it nor heard of it.</summary>
        Unknown,
        /// <summary>It is their own trouble: a hardship that falls on them, they know of already.</summary>
        Theirs
    }

    /// <summary>
    /// The pure rules of warning a people of a threat (the player, 10 November 2026: "warning about raiders should only be
    /// an option if the target colony is actually a potential target for these raiders and not too much time has
    /// expired", and the debt "appropriate to the debt i actually owe and not be repeatable all the time"). Which threats
    /// count for them (World/Warnings.cs gathers them from the map), what a warning heeded warmly is worth to them (the
    /// size of the debt of honour it makes, or a memory only), once per threat per people, and one debt from warnings a
    /// season. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class WarnMath
    {
        public const int DayTicks = 60000;

        // ---------------- Within reach of their town ----------------

        /// <summary>Raiders whose town lies this near one of theirs, or the home they raided this near, are a threat to them (DealingMath.WarnTiles).</summary>
        public const float ReachTiles = 20f;

        /// <summary>A camp or a site of the raiders, or a hostile site, this near one of their towns.</summary>
        public const float NearTiles = 12f;

        /// <summary>The raiders' own land (Faction Territories) this near one of their towns (RaidersCampMath.TerritoryTiles).</summary>
        public const float TerritoryTiles = 30f;

        /// <summary>Raiders whose town lies this near: a real debt (10); between this and <see cref="ReachTiles"/>, a memory only (8).</summary>
        public const float CloseTiles = 10f;

        /// <summary>A war or a road trouble whose place lies this near one of their towns.</summary>
        public const float WarTiles = 20f;

        /// <summary>A hardship spreading this near one of their towns.</summary>
        public const float SpreadTiles = 20f;

        /// <summary>A raid on a home counts this long; a threat still coming counts while it lasts.</summary>
        public const float RaidDays = 5f;

        /// <summary>A raid struck this long ago or less (days; negative: none).</summary>
        public static bool Recent(float daysSince) => daysSince >= 0f && daysSince <= RaidDays;

        /// <summary>
        /// What raiders are to them, by how near anything of theirs lies to one of their towns (tiles; negative: none): a camp
        /// or site of theirs within 12 tiles, 12; a town of theirs within 10, 10; a town within 20, their land within 30, or the
        /// home they raided within 20, 8 (remembered, never a debt); else 0, no threat to them.
        /// </summary>
        public static int RaidersSize(float townTiles, float campTiles, float territoryTiles, float homeTiles)
        {
            if (Within(campTiles, NearTiles)) return SizeNear;
            if (Within(townTiles, CloseTiles)) return SizeClose;
            if (Within(townTiles, ReachTiles) || Within(territoryTiles, TerritoryTiles) || Within(homeTiles, ReachTiles)) return SizeFar;
            return 0;
        }

        private static bool Within(float tiles, float max) => tiles >= 0f && tiles <= max;

        /// <summary>
        /// Why raiders the colony knows of are no warning to them, or None: their own people, at peace with them, gone, nothing
        /// recent or coming, out of reach. A people at war with everyone counts by the same rules.
        /// </summary>
        public static WarnWhy RaidersWhy(bool theirOwn, bool hostileToThem, bool defeated, bool recentOrComing, int size)
        {
            if (theirOwn || !hostileToThem) return WarnWhy.NotTheirEnemy;
            if (defeated) return WarnWhy.Ended;
            if (!recentOrComing) return WarnWhy.TooOld;
            return size <= 0 ? WarnWhy.TooFar : WarnWhy.None;
        }

        /// <summary>A world event that is a war or trouble on the roads: a bad one of kind war (a warlord, the clans gathering, a war between neighbours), roads (a bandit road) or son (the warlord's son's band).</summary>
        public static bool WarOrRoad(string kind, bool bad) => bad && (kind == "war" || kind == "roads" || kind == "son");

        /// <summary>
        /// A hardship that spreads to its neighbours: sickness (a plague, a murrain), fire, and the hungers that move (locusts,
        /// a blight). A famine, a winter, a flood or a drought is felt where it falls; they know their own weather.
        /// </summary>
        public static bool Spreading(string defName, string kind, bool bad) =>
            bad && (kind == "sickness" || kind == "fire" || defName == "Council_WS_Locusts" || defName == "Rimfolk_WS_Blight");

        /// <summary>A war or road trouble near them: its attacker is at war with them, not them, and its place within <see cref="WarTiles"/>.</summary>
        public static WarnWhy WarWhy(bool attackerTheirs, bool attackerHostile, float tiles)
        {
            if (attackerTheirs || !attackerHostile) return WarnWhy.NotTheirEnemy;
            return Within(tiles, WarTiles) ? WarnWhy.None : WarnWhy.TooFar;
        }

        /// <summary>A hardship near them: never to the people it falls on, or one suffering the same; within <see cref="SpreadTiles"/>.</summary>
        public static WarnWhy HardshipWhy(bool falls, bool sufferingOne, float tiles)
        {
            if (falls || sufferingOne) return WarnWhy.Theirs;
            return Within(tiles, SpreadTiles) ? WarnWhy.None : WarnWhy.TooFar;
        }

        /// <summary>A hostile site near them: its people at war with them, not them, within <see cref="NearTiles"/>.</summary>
        public static WarnWhy SiteWhy(bool theirs, bool hostile, float tiles)
        {
            if (theirs || !hostile) return WarnWhy.NotTheirEnemy;
            return Within(tiles, NearTiles) ? WarnWhy.None : WarnWhy.TooFar;
        }

        // ---------------- What a warning is worth ----------------

        /// <summary>The sizes: a war or a warlord's band near them; a camp or a hostile site near them; raiders whose town is close; raiders further off.</summary>
        public const int SizeWar = 14, SizeNear = 12, SizeClose = 10, SizeFar = 8;

        /// <summary>More when the threat strikes them within <see cref="StruckDays"/> of the warning heeded.</summary>
        public const int StruckBonus = 4;

        public const float StruckDays = 15f;

        /// <summary>A lesser warning (a hardship spreading): remembered with this weight, and a little goodwill, never a debt.</summary>
        public const int HardshipWeight = 6;

        /// <summary>The size of a warning of this kind (for raiders, by their reach: <see cref="RaidersSize"/>).</summary>
        public static int Size(ThreatKind k, int raidersSize)
        {
            switch (k)
            {
                case ThreatKind.War: return SizeWar;
                case ThreatKind.Site: return SizeNear;
                case ThreatKind.Hardship: return HardshipWeight;
                default: return Math.Max(0, raidersSize);
            }
        }

        /// <summary>A warning of this size heeded warmly makes a debt of honour (FavourMath.MinSize); a hardship never does.</summary>
        public static bool MakesDebt(ThreatKind k, int size) => k != ThreatKind.Hardship && size >= FavourMath.MinSize;

        /// <summary>The threat struck them within the days after the warning was heeded: the size grows by <see cref="StruckBonus"/>.</summary>
        public static bool Struck(int heededTick, int now) => heededTick >= 0 && now >= heededTick && now - heededTick <= StruckDays * DayTicks;

        // ---------------- Once per threat, a debt a season ----------------

        /// <summary>One debt from warnings per people a season.</summary>
        public const float DebtGapDays = 15f;

        /// <summary>A debt from a warning may be made now: none from them (-1), or the last a season ago or more.</summary>
        public static bool DebtAllowed(int lastDebt, int now) => lastDebt < 0 || now - lastDebt >= DebtGapDays * DayTicks;

        /// <summary>A people's record of a threat warned of is kept this long at least, and while the threat lasts.</summary>
        public const float KeepDays = 30f;

        /// <summary>Records kept at most, all peoples together (the oldest go).</summary>
        public const int MaxKept = 60;

        /// <summary>Threats the colony heard of (point 10), kept this long, so many at most.</summary>
        public const float KnownDays = 30f;

        public const int MaxKnown = 40;

        /// <summary>A record past its days and no longer standing is let go.</summary>
        public static bool LetGo(int tick, int now, bool stillStanding, float keepDays) => !stillStanding && now - tick > keepDays * DayTicks;

        // ---------------- The keys ----------------

        /// <summary>A threat's key, the same for every notable of a people: "raid:12" (raiders of people 12), "site:345", "sit:7" (a world event).</summary>
        public const string RaidPrefix = "raid:", SitePrefix = "site:", SitPrefix = "sit:";

        public static string RaidKey(int people) => RaidPrefix + people;

        public static string SiteKey(int site) => SitePrefix + site;

        public static string SitKey(int situation) => SitPrefix + situation;

        /// <summary>A warning's key from before 10 November 2026, or now: one of these three.</summary>
        public static bool IsWarningKey(string key) =>
            key != null && (key.StartsWith(RaidPrefix, StringComparison.Ordinal) || key.StartsWith(SitePrefix, StringComparison.Ordinal) || key.StartsWith(SitPrefix, StringComparison.Ordinal));

        /// <summary>The key heard news names a threat by (OutsiderTalk: the fact behind its placeholder, "ws:7", "site:345"), as a warning's key; null for none.</summary>
        public static string FromFact(string fact)
        {
            if (fact == null) return null;
            if (fact.StartsWith("ws:", StringComparison.Ordinal)) return SitPrefix + fact.Substring(3);
            if (fact.StartsWith(SitePrefix, StringComparison.Ordinal)) return fact;
            return null;
        }

        /// <summary>The weightiest of what the colony knows makes the topic: the raiders' size, else the kind's.</summary>
        public static List<int> Order(IList<int> sizes)
        {
            var idx = new List<int>();
            for (int i = 0; i < (sizes?.Count ?? 0); i++) idx.Add(i);
            idx.Sort((a, b) => sizes[b] != sizes[a] ? sizes[b].CompareTo(sizes[a]) : a.CompareTo(b));
            return idx;
        }
    }
}
