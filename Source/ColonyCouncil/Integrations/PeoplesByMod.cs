using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Naming a people in Rimfolk's XML. Most are named by their FactionDef (Empire, Outland_VathunNobles), but some mods
    /// add a nation per package and keep their def names to themselves (Medieval Total Rim's eight nations): those are
    /// named "mod:" and the start of the package id, "mod:athelas.medievaltotalrim" for any of them, or
    /// "mod:athelas.medievaltotalrim.mongols" for one. Used by encounters' strangers (<see cref="ModStrangers"/>) and world
    /// events about a particular people (<see cref="WorldSituationWorker_ModFaction"/>). A name whose mod isn't loaded
    /// never matches anyone.
    /// </summary>
    public static class PeoplesByMod
    {
        public const string Prefix = "mod:";

        private static readonly Dictionary<FactionDef, string> packages = new Dictionary<FactionDef, string>();

        /// <summary>
        /// The package id of the mod a people comes from, lowercase and without a local copy's "_steam" ending; "" for
        /// the base game's own and anything that can't be read.
        /// </summary>
        public static string PackageOf(FactionDef def)
        {
            if (def == null) return "";
            if (packages.TryGetValue(def, out var p)) return p;
            p = "";
            try
            {
                var pack = def.modContentPack;
                if (pack != null && !pack.IsOfficialMod && !pack.IsCoreMod)
                {
                    p = (pack.PackageId ?? "").ToLowerInvariant();
                    if (p.EndsWith("_steam", StringComparison.Ordinal)) p = p.Substring(0, p.Length - "_steam".Length);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("people's mod", e);
                p = "";
            }
            packages[def] = p;
            return p;
        }

        /// <summary>One of these names fits the people: its FactionDef name, or "mod:" and the start of its mod's package id.</summary>
        public static bool Matches(FactionDef def, List<string> names)
        {
            if (def == null || names == null) return false;
            foreach (var n in names)
            {
                if (n.NullOrEmpty()) continue;
                if (n == def.defName) return true;
                if (!n.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string pkg = PackageOf(def);
                if (pkg.Length > 0 && pkg.StartsWith(n.Substring(Prefix.Length).Trim().ToLowerInvariant(), StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
