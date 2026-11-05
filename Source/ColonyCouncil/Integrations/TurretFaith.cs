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
    /// A faith that scorns automated turrets (an ideology mod's precept, such as "Has automated turrets: scorned") gives
    /// its mood to everyone while the colony owns a turret anywhere: on another home, or in a hatch's dungeon below the
    /// home, where a hacked terminal hands the colony the ancient turrets of that small map. The player's word: the mood
    /// should come only where a turret stands. So the precepts about turrets are found by name when the game starts, and
    /// their situational mood holds only for someone on a map where the colony has an automated turret (one nobody has to
    /// man; a mortar doesn't count). It never gives the mood where the precept wouldn't; it only takes it away elsewhere.
    /// </summary>
    public static class TurretFaith
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.faithTurretsHere ?? true);

        private static readonly Dictionary<int, (int tick, bool has)> cache = new Dictionary<int, (int, bool)>();

        /// <summary>The situational moods of every precept about turrets (its issue or its own name says turrets).</summary>
        public static List<ThoughtDef> Thoughts()
        {
            var list = new List<ThoughtDef>();
            foreach (var p in DefDatabase<PreceptDef>.AllDefsListForReading)
            {
                if (!About(p)) continue;
                if (p.comps == null) continue;
                foreach (var c in p.comps)
                    if (c is PreceptComp_SituationalThought s && s.thought != null && !list.Contains(s.thought))
                        list.Add(s.thought);
            }
            return list;
        }

        private static bool About(PreceptDef p) =>
            p != null && (Has(p.defName) || Has(p.issue?.defName));

        private static bool Has(string name) => name != null && name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The colony has an automated turret on this map (checked at most once a 250-tick stretch per map).</summary>
        public static bool TurretOn(Map map)
        {
            if (map == null) return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(map.uniqueID, out var c) && now - c.tick < 250 && now >= c.tick) return c.has;
            bool has = false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b is Building_Turret && b.TryGetComp<CompMannable>() == null)
                {
                    has = true;
                    break;
                }
            cache[map.uniqueID] = (now, has);
            return has;
        }
    }

    /// <summary>
    /// The turret precepts' moods hold only on a map with one of the colony's automated turrets
    /// (Integrations/TurretFaith.cs). The methods are the precepts' own thought workers, found when the game starts.
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
                if (p == null) return;
                if (!TurretFaith.TurretOn(p.MapHeld)) __result = ThoughtState.Inactive;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("turret faith", e);
            }
        }
    }
}
