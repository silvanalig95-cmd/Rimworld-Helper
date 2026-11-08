using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What there is to know about an old camp's site, for whether a new camp moves in.</summary>
    public struct GroundFacts
    {
        /// <summary>The constructions it had (walls, doors, a hut's or tents' walls, the fire pit, spots), and how many still stand, nobody's.</summary>
        public int pieces, standing;
        /// <summary>The fire pit stands; any of a hut's or tents' walls or doors, or a windbreak, stands.</summary>
        public bool fireStands, shelterStands;
        /// <summary>Beds it had room for (the beds themselves went with the camp that left).</summary>
        public int beds;
        /// <summary>Any of its pieces is the colony's now (claimed), or the colony's land, zones or buildings have come up to it.</summary>
        public bool claimed, colonyNear;
        /// <summary>Another camp stands too close to it, or holds it now.</summary>
        public bool campNear;
    }

    /// <summary>One who held an old camp's site, for its history's rules.</summary>
    public struct HoldFacts
    {
        /// <summary>Their people (any number that tells peoples apart).</summary>
        public int people;
        /// <summary>How they left it: left, driven (a band drove them off), gaveway, held (a band took and held it a while).</summary>
        public string how;
    }

    /// <summary>Two camps came for the same old site at once: how it is worked out on the map.</summary>
    public enum TogetherEnd
    {
        /// <summary>Nothing comes of it (no second party).</summary>
        None,
        /// <summary>The second people gives way and camps elsewhere on the land.</summary>
        GivesWay,
        /// <summary>Their peoples are at war: it comes to blows (the game's own fight, warned and shown).</summary>
        Blows
    }

    /// <summary>
    /// Old camps' sites (the player's request, 8 November 2026: "they could even move into structures from previous camps
    /// and these places become contested spots"): since 0.167.0 a camp leaves its walls, huts, fire pit and spots standing,
    /// nobody's. A new camp may move into such a site when enough of it stands, it isn't the colony's and no other camp is
    /// near; a site several peoples held, or one someone was driven off, is contested, and a rival may come for it, or two
    /// may arrive for it together. Pure (no game types), so Tools/Tests checks it.
    /// </summary>
    public static class CampGroundMath
    {
        /// <summary>Old sites a home keeps, the newest; the oldest is forgotten past this.</summary>
        public const int MostSites = 6;
        /// <summary>Holders a site remembers, the newest.</summary>
        public const int MostHolders = 6;

        /// <summary>
        /// Worth moving into: none of it the colony's, nothing of the colony's near, no other camp near; its fire pit or some
        /// shelter standing and at least four in ten of its constructions; beds for at least <paramref name="fewest"/> of
        /// them (the kind's fewest).
        /// </summary>
        public static bool Worth(GroundFacts g, int fewest)
        {
            if (g.claimed || g.colonyNear || g.campNear || g.standing <= 0 || g.pieces <= 0) return false;
            if (!g.fireStands && !g.shelterStands) return false;
            if (g.standing * 10 < g.pieces * 4) return false;
            return g.beds >= Math.Max(1, fewest);
        }

        /// <summary>Still a site for camps to come back to: something stands, and the colony hasn't claimed it or built up to it.</summary>
        public static bool StillASite(int standing, bool claimed, bool colonyNear) => standing > 0 && !claimed && !colonyNear;

        /// <summary>How many come to it: as many as were coming, no more than its beds take, never fewer than the kind's fewest.</summary>
        public static int People(int coming, int beds, int fewest) => Math.Max(Math.Max(1, fewest), Math.Min(coming, beds));

        /// <summary>A new camp takes a site worth it rather than a fresh spot: likelier the more camps the land takes (the good spots go first).</summary>
        public static float ReuseChance(CampCrowd c)
        {
            switch (c)
            {
                case CampCrowd.Rare: return 0.5f;
                case CampCrowd.Many: return 0.7f;
                case CampCrowd.Crowded: return 0.8f;
                default: return 0.6f;
            }
        }

        /// <summary>Contested: two peoples or more have held it, or someone was driven off it.</summary>
        public static bool Contested(IList<HoldFacts> held)
        {
            if (held == null || held.Count == 0) return false;
            int first = held[0].people;
            for (int i = 0; i < held.Count; i++)
                if (held[i].people != first || held[i].how == "driven") return true;
            return false;
        }

        /// <summary>
        /// The chance a second party comes for the same site the day a camp moves into it: never on a land of rare camps,
        /// a little on the usual one (only to come to blows: no room for a second camp), more the more camps the land
        /// takes; half as much again for a contested site.
        /// </summary>
        public static float TogetherChance(CampCrowd c, bool contested)
        {
            float p;
            switch (c)
            {
                case CampCrowd.Rare: return 0f;
                case CampCrowd.Many: p = 0.12f; break;
                case CampCrowd.Crowded: p = 0.18f; break;
                default: p = 0.05f; break;
            }
            return contested ? p * 1.5f : p;
        }

        /// <summary>
        /// Two parties at the same site: peoples at war come to blows; otherwise the second gives way and camps elsewhere,
        /// when the land has room for another camp; with none, nothing comes of it.
        /// </summary>
        public static TogetherEnd Together(bool atWar, bool roomElsewhere) => atWar ? TogetherEnd.Blows : roomElsewhere ? TogetherEnd.GivesWay : TogetherEnd.None;

        /// <summary>A people that held the site before, other than the one holding it now: their claim, for the rival who comes.</summary>
        public static bool HasClaim(IList<HoldFacts> held, int people, int holder)
        {
            if (held == null || people == holder) return false;
            for (int i = 0; i < held.Count; i++)
                if (held[i].people == people) return true;
            return false;
        }

        /// <summary>How a camp's going reads in the site's history, by its ending (CampLife: time, asked, raided...).</summary>
        public static string HowLeft(string ending)
        {
            switch (ending)
            {
                case "raided": return "driven";
                case "forced":
                case "fallout":
                case "harmed":
                case "arrest":
                case "fight":
                case "brawl":
                    return "sent";
                default:
                    return "left";
            }
        }

        /// <summary>The newest <see cref="MostHolders"/> are kept: how many to drop from the front of a history this long.</summary>
        public static int Drop(int count) => Math.Max(0, count - MostHolders);
    }
}
