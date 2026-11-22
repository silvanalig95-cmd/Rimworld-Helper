using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How far from home the sites of the game's and the mods' quests may be (the setting "How far quest sites may be").</summary>
    public enum QuestSiteRange
    {
        /// <summary>The game's own distances.</summary>
        AsTheGame,
        /// <summary>The farthest at about three fifths of the game's (16 tiles of its 27).</summary>
        Nearer,
        /// <summary>The farthest at about a third of the game's (9 tiles), the nearest a little closer too.</summary>
        Near
    }

    /// <summary>
    /// Where a quest plays out, and how near its site may be (the player, 20 November 2026: "the missions that you get,
    /// especially the ones on the world map ... I rarely do because it's just a huge price to pay ... to travel on the world
    /// map ... if you could just reduce the chance in favor of other missions that might play out on the map"). Pure, so
    /// Tools/Tests checks the rule on the player's own quests.
    /// <para>
    /// A quest is out on the world map when its script makes or finds a place elsewhere for the colony to go to: a node of
    /// its tree (or a script it runs as a part of itself) picks a site's tile, makes a site or another world object, or
    /// finds a town to trade with; or the code of one of its nodes calls the game's own ways of doing so (a root node
    /// written in code, as Odyssey's, Biotech's, Ideology's and most mods' are). The node types and calls are read by name,
    /// so the game's, the DLCs' and every mod's quests are read the same way. Everything else plays out at home.
    /// </para>
    /// </summary>
    public static class QuestPlaceMath
    {
        /// <summary>The setting "Quests out on the world map": their weight when the storyteller's quest slot picks one, 100% as the game has it.</summary>
        public const float DefaultAwayShare = 0.5f, MinAwayShare = 0.1f, MaxAwayShare = 1f;

        /// <summary>The game's node types that send the colony out on the world map.</summary>
        public static readonly string[] AwayNodes =
        {
            "QuestNode_GetSiteTile", "QuestNode_GenerateSite", "QuestNode_SpawnWorldObjects", "QuestNode_GetNearbySettlement"
        };

        /// <summary>Parts of a node type's name that say the same of a mod's own nodes (a "GenerateSiteHive", a "GetSiteTileNear").</summary>
        public static readonly string[] AwayNameParts = { "SiteTile", "GenerateSite", "SpawnWorldObject", "NearbySettlement" };

        /// <summary>
        /// What a node's code may call, as "Type.Member", that makes or finds a place out on the world map: a site's tile, a
        /// site, a world object, a trade request at a town. An entry ending in a dot stands for any member of that type
        /// (its constructor too, ".ctor").
        /// </summary>
        public static readonly string[] AwayCalls =
        {
            "TileFinder.TryFindNewSiteTile",
            "SiteMaker.",
            "SiteMakerHelper.",
            "QuestGen_Sites.GenerateSite",
            "QuestGen_Sites.SpawnWorldObject",
            "QuestPart_SpawnWorldObject.",
            "QuestPart_InitiateTradeRequest.",
            "WorldObjectMaker.MakeWorldObject"
        };

        /// <summary>A node type (by its own name or one it is built on) that sends the colony out on the world map.</summary>
        public static bool AwayNode(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            foreach (var n in AwayNodes)
                if (typeName == n)
                    return true;
            foreach (var p in AwayNameParts)
                if (typeName.IndexOf(p, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }

        /// <summary>A call ("Type.Member") that makes or finds a place out on the world map.</summary>
        public static bool AwayCall(string call)
        {
            if (string.IsNullOrEmpty(call)) return false;
            foreach (var c in AwayCalls)
            {
                if (!call.StartsWith(c, StringComparison.Ordinal)) continue;
                // "SiteMaker." is a prefix of nothing else; "TileFinder.TryFindNewSiteTile" takes no more letters after it.
                if (c.EndsWith(".", StringComparison.Ordinal) || call.Length == c.Length) return true;
            }
            return false;
        }

        /// <summary>
        /// Why a quest is out on the world map, from what its tree holds (<paramref name="nodeTypes"/>: each node's type and
        /// the types it is built on) and what its nodes' code calls; null when it plays out at home.
        /// </summary>
        public static string Why(IEnumerable<string> nodeTypes, IEnumerable<string> calls)
        {
            if (nodeTypes != null)
                foreach (var t in nodeTypes)
                    if (AwayNode(t))
                        return "node " + t;
            if (calls != null)
                foreach (var c in calls)
                    if (AwayCall(c))
                        return "calls " + c;
            return null;
        }

        /// <summary>The setting's share, kept within its bounds (an unreadable one is the default).</summary>
        public static float Share(float share) =>
            float.IsNaN(share) ? DefaultAwayShare : share < MinAwayShare ? MinAwayShare : share > MaxAwayShare ? MaxAwayShare : share;

        /// <summary>The factor on a quest's weight in the storyteller's quest slot: the share for one out on the world map, 1 at home.</summary>
        public static float WeightFactor(bool away, float share) => away ? Share(share) : 1f;

        /// <summary>
        /// Of the quests the slot offers, the share out on the world map once the setting weighs them: <paramref name="awayBefore"/>
        /// of them as the game has it, their weight times <paramref name="share"/>, the rest's as it is.
        /// </summary>
        public static float AwayAfter(float awayBefore, float share)
        {
            float a = awayBefore < 0f ? 0f : awayBefore > 1f ? 1f : awayBefore;
            float s = Share(share);
            float w = a * s + (1f - a);
            return w <= 0f ? 0f : a * s / w;
        }

        // ---------------- How far a site may be ----------------

        /// <summary>A site the game already puts this close (in tiles) stays as it is, and a nearer range never ends closer.</summary>
        public const int CloseEnough = 6;

        /// <summary>The nearest a site may come by the setting, in tiles.</summary>
        public const int NearestTiles = 2;

        /// <summary>The game's own range for a quest's site, when the quest gives none of its own (TileFinder's defaults).</summary>
        public const int GameMin = 7, GameMax = 27;

        /// <summary>The farthest a site may be, as a share of the game's farthest, by the setting.</summary>
        public static float MaxFactor(QuestSiteRange r) => r == QuestSiteRange.Nearer ? 0.6f : r == QuestSiteRange.Near ? 0.35f : 1f;

        /// <summary>
        /// The range a quest's site is picked in by the setting, from the range the quest asked for: the farthest lowered to
        /// the setting's share (never below <see cref="CloseEnough"/>), the nearest kept unless it would not fit, then half the
        /// new farthest (never below <see cref="NearestTiles"/>). A range already close, or the game's own setting, stays.
        /// </summary>
        public static void SiteRange(int min, int max, QuestSiteRange r, out int newMin, out int newMax)
        {
            newMin = min;
            newMax = max;
            float f = MaxFactor(r);
            if (f >= 1f || max <= CloseEnough || max <= min) return;
            int far = Math.Max(CloseEnough, (int)Math.Round(max * f, MidpointRounding.AwayFromZero));
            if (far >= max) return;
            newMax = far;
            newMin = Math.Min(min, Math.Max(NearestTiles, far / 2));
            if (newMin >= newMax) newMin = Math.Max(0, newMax - 1);
        }

        /// <summary>The farthest a site of the game's own range may be by the setting (for the words: 27, 16, 9).</summary>
        public static int GameFarthest(QuestSiteRange r)
        {
            SiteRange(GameMin, GameMax, r, out _, out int max);
            return max;
        }
    }
}
