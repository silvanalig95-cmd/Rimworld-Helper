using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A faith that scorns automated turrets (an ideology mod's precept, such as Genetic Rim's "autonomous weapons:
    /// scorned") gives its mood to everyone while the colony owns a turret anywhere: on another home, or in a hatch's
    /// dungeon below the home, where a hacked terminal hands the colony the ancient turrets of that small map, which the
    /// game keeps loaded once entered. The player's word: the mood should come only where a turret stands. So the moods
    /// the game's own check gives (ThoughtWorker_Precept_HasAutomatedTurrets, whatever precept carries it), and those of
    /// precepts whose name says turrets, hold only for one of the colony's people on a map where the colony has what that
    /// check counts: an automated turret (one nobody has to man; a mortar doesn't count) or, with Biotech, one of its
    /// mechanoids. It never gives the mood where the precept wouldn't; it only takes it away elsewhere.
    /// </summary>
    public static class TurretFaith
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.faithTurretsHere ?? true);

        private static readonly Dictionary<int, (int tick, bool has)> cache = new Dictionary<int, (int, bool)>();

        /// <summary>
        /// The moods about turrets: every thought the game's own turret check gives (Genetic Rim's precept is named for
        /// autonomous weapons, not turrets, and uses it), and the situational moods of precepts whose issue, own name or
        /// thought says turrets (a mod's own check).
        /// </summary>
        public static List<ThoughtDef> Thoughts()
        {
            var list = new List<ThoughtDef>();
            foreach (var t in DefDatabase<ThoughtDef>.AllDefsListForReading)
                if (t?.workerClass != null && typeof(ThoughtWorker_Precept_HasAutomatedTurrets).IsAssignableFrom(t.workerClass) && !list.Contains(t))
                    list.Add(t);
            foreach (var p in DefDatabase<PreceptDef>.AllDefsListForReading)
            {
                if (p?.comps == null) continue;
                foreach (var c in p.comps)
                    if (c is PreceptComp_SituationalThought s && s.thought != null && (About(p) || Has(s.thought.defName)) && !list.Contains(s.thought))
                        list.Add(s.thought);
            }
            return list;
        }

        private static bool About(PreceptDef p) =>
            p != null && (Has(p.defName) || Has(p.issue?.defName));

        private static bool Has(string name) => name != null && name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The colony has on this map what the game's turret check counts (checked at most once a 250-tick stretch per map):
        /// an automated turret (a turret building nobody has to man), or, with Biotech, a mechanoid it controls.
        /// </summary>
        public static bool TurretOn(Map map)
        {
            if (map == null) return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(map.uniqueID, out var c) && now - c.tick < 250 && now >= c.tick) return c.has;
            bool has = false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b?.def?.building != null && b.def.building.IsTurret && !b.def.HasComp(typeof(CompMannable)))
                {
                    has = true;
                    break;
                }
            if (!has && ModsConfig.BiotechActive)
                foreach (var m in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                    if (m.IsColonyMechPlayerControlled)
                    {
                        has = true;
                        break;
                    }
            cache[map.uniqueID] = (now, has);
            return has;
        }
    }

    /// <summary>
    /// The turret precepts' moods hold only on a map with one of the colony's automated turrets or mechanoids
    /// (Integrations/TurretFaith.cs). The methods are the moods' own thought workers, found once the defs have loaded.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_TurretFaith
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var seen = new HashSet<MethodBase>();
            List<ThoughtDef> thoughts;
            try
            {
                thoughts = TurretFaith.Thoughts();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("turret faith: reading the precepts", e);
                yield break;
            }
            foreach (var t in thoughts)
            {
                var type = t.workerClass;
                if (type == null || type.IsAbstract) continue;
                // The worker's own check: a precept's (ShouldHaveThought) where it has one, else the thought's state.
                var m = AccessTools.Method(type, "ShouldHaveThought", new[] { typeof(Pawn) })
                        ?? AccessTools.Method(type, "CurrentStateInternal", new[] { typeof(Pawn) });
                if (m == null || m.IsAbstract || m.DeclaringType == typeof(ThoughtWorker) || m.DeclaringType == typeof(ThoughtWorker_Precept)) continue;
                if (seen.Add(m)) yield return m;
            }
        }

        public static void Postfix(object[] __args, ref ThoughtState __result)
        {
            if (!__result.Active || !TurretFaith.On) return;
            try
            {
                var p = __args != null && __args.Length > 0 ? __args[0] as Pawn : null;
                // The colony's people only: the game weighs another people's against their own turrets, left as it is.
                if (p == null || p.Faction != Faction.OfPlayer) return;
                if (!TurretFaith.TurretOn(p.MapHeld)) __result = ThoughtState.Inactive;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("turret faith", e);
            }
        }
    }
}
