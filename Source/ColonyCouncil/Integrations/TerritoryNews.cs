using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Faction Territories and Vassalage lets every people found new settlements at the edge of its territory, each about
    /// every 30 days, and tells each start and each completion as a message ("Settlement construction: ...", "Settlement
    /// completed: ..."). With some thirty peoples that is a message or two a day from all over the world, and the player
    /// switched the feature off for it. So: while that mod starts or completes a settlement, its message is let through
    /// only when the place lies within the region map's reach of one of the colony's homes; one further away goes to
    /// the colony news (the evening letter, by the "Colony news" setting) as world news, through Rimfolk's one voice to
    /// the player (<see cref="Tell.Minor"/>, with its caps when it comes as a message). Found by name;
    /// without the mod nothing is patched.
    /// </summary>
    public static class TerritoryNews
    {
        public const string PackageId = "jaeger972.factionterritories";
        public const string UtilityType = "FactionTerritories.Expansion.Utility";

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.territoryNewsQuiet ?? true);

        /// <summary>Inside the mod's settlement start or completion (its messages are the ones looked at).</summary>
        internal static int depth;
        /// <summary>Passing one on to the colony news (which may show it as a message itself).</summary>
        private static bool routing;

        public static MethodBase Target(string name)
        {
            if (!ModsConfig.IsActive(PackageId)) return null;
            var t = AccessTools.TypeByName(UtilityType);
            return t == null ? null : AccessTools.Method(t, name);
        }

        /// <summary>The place lies within the region map's reach of one of the colony's homes.</summary>
        public static bool Near(PlanetTile tile)
        {
            if (!tile.Valid) return true;
            int reach = ColonyCouncilMod.Settings?.regionMapRadius ?? 35;
            foreach (var m in Find.Maps)
                if (m.IsPlayerHome && Find.WorldGrid.ApproxDistanceInTiles(m.Tile, tile) <= reach) return true;
            return false;
        }

        /// <summary>A message from the mod's settlements, far away: to the colony news instead. True when it was taken.</summary>
        public static bool Take(string text, LookTargets look)
        {
            if (depth <= 0 || routing || !On || text.NullOrEmpty()) return false;
            var tile = look != null && look.IsValid ? look.PrimaryTarget.Tile : PlanetTile.Invalid;
            if (Near(tile)) return false;
            routing = true;
            try
            {
                Tell.Minor(NewsKind.World, Importance.Flavor, null, text, look, MessageTypeDefOf.NeutralEvent);
            }
            finally
            {
                routing = false;
            }
            return true;
        }
    }

    /// <summary>While Faction Territories and Vassalage starts a settlement (Integrations/TerritoryNews.cs).</summary>
    [HarmonyPatch]
    public static class Patch_TerritoryNewsStart
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => TerritoryNews.Target("TryCreateForFaction");

        public static void Prefix() => TerritoryNews.depth++;

        public static Exception Finalizer(Exception __exception)
        {
            if (TerritoryNews.depth > 0) TerritoryNews.depth--;
            return __exception;
        }
    }

    /// <summary>While Faction Territories and Vassalage completes a settlement (Integrations/TerritoryNews.cs).</summary>
    [HarmonyPatch]
    public static class Patch_TerritoryNewsComplete
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => TerritoryNews.Target("TryComplete");

        public static void Prefix() => TerritoryNews.depth++;

        public static Exception Finalizer(Exception __exception)
        {
            if (TerritoryNews.depth > 0) TerritoryNews.depth--;
            return __exception;
        }
    }

    /// <summary>The mod's settlement messages, far away, go to the colony news (Integrations/TerritoryNews.cs).</summary>
    [HarmonyPatch(typeof(Messages), nameof(Messages.Message), new[] { typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool) })]
    public static class Patch_TerritoryNewsMessage
    {
        public static bool Prepare() => TerritoryNews.Target("TryCreateForFaction") != null || TerritoryNews.Target("TryComplete") != null;

        public static bool Prefix(string text, LookTargets lookTargets)
        {
            if (TerritoryNews.depth <= 0) return true;
            try
            {
                return !TerritoryNews.Take(text, lookTargets);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("territory news", e);
                return true;
            }
        }
    }
}
