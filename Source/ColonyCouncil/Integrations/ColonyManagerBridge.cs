using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony Manager (the fork the player runs, heaven.colonymanagerfork; namespace FluffyManager), read by name and
    /// changed only at the player's click. Its manager is a map component (FluffyManager.Manager) with a stack of jobs
    /// (JobStack.FullStack()): hunting, forestry, mining and foraging keep a stock target (their Trigger, a
    /// Trigger_Threshold: TargetCount against CurrentCount, of what its ThresholdFilter allows, MaxUpperThreshold the
    /// slider's top); livestock keeps herd sizes (Trigger_PawnKind: CountTargets against Counts). Each job is Managed,
    /// may be Suspended, and is Completed at its target. The jobs hide the base class's Trigger with their own, so the
    /// field is read from the job's own type down. The manager's work needs its Managing work type and a manager desk
    /// (FluffyManager.Building_ManagerStation). Nothing here makes a manager where the map has none; without the mod
    /// nothing is found, and anything that can't be read leaves the bridge quiet.
    /// </summary>
    public static class ColonyManagerBridge
    {
        /// <summary>One job as read now: the job itself, its trigger, what it counts, and its numbers.</summary>
        public class Job
        {
            public object job, trigger;
            public string typeName;
            /// <summary>Its place among the jobs of its kind, in the manager's order.</summary>
            public int index;
            public ManagerJobInfo info = new ManagerJobInfo();
            /// <summary>What its stock target counts (meat, wood, the minerals it mines).</summary>
            public HashSet<ThingDef> goods = new HashSet<ThingDef>();
            /// <summary>Mining: the rock it may mine.</summary>
            public HashSet<ThingDef> minerals = new HashSet<ThingDef>();
        }

        private static bool looked, broken;
        private static Type managerType, jobType, stationType, thresholdType, pawnKindType;
        private static PropertyInfo stackProp, suspendedProp, managedProp, completedProp, labelProp, currentProp, countsProp, forestryTypeProp;
        private static MethodInfo fullStack;
        private static FieldInfo targetField, maxField, filterField, countTargetsField, pawnKindField, mineralsField;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                managerType = GenTypes.GetTypeInAnyAssembly("FluffyManager.Manager");
                if (managerType == null) return;
                const BindingFlags I = BindingFlags.Public | BindingFlags.Instance;
                jobType = GenTypes.GetTypeInAnyAssembly("FluffyManager.ManagerJob");
                stationType = GenTypes.GetTypeInAnyAssembly("FluffyManager.Building_ManagerStation");
                thresholdType = GenTypes.GetTypeInAnyAssembly("FluffyManager.Trigger_Threshold");
                pawnKindType = GenTypes.GetTypeInAnyAssembly("FluffyManager.Trigger_PawnKind");
                stackProp = managerType.GetProperty("JobStack", I);
                fullStack = stackProp?.PropertyType.GetMethod("FullStack", I, null, Type.EmptyTypes, null);
                suspendedProp = jobType?.GetProperty("Suspended", I);
                managedProp = jobType?.GetProperty("Managed", I);
                completedProp = jobType?.GetProperty("Completed", I);
                labelProp = jobType?.GetProperty("Label", I);
                targetField = thresholdType?.GetField("TargetCount", I);
                maxField = thresholdType?.GetField("MaxUpperThreshold", I);
                filterField = thresholdType?.GetField("ThresholdFilter", I);
                currentProp = thresholdType?.GetProperty("CurrentCount", I);
                countTargetsField = pawnKindType?.GetField("CountTargets", I);
                pawnKindField = pawnKindType?.GetField("pawnKind", I);
                countsProp = pawnKindType?.GetProperty("Counts", I);
                forestryTypeProp = GenTypes.GetTypeInAnyAssembly("FluffyManager.ManagerJob_Forestry")?.GetProperty("Type", I);
                mineralsField = GenTypes.GetTypeInAnyAssembly("FluffyManager.ManagerJob_Mining")?.GetField("AllowedMinerals", I);
                if (fullStack == null || jobType == null || suspendedProp == null || targetField == null || currentProp == null)
                {
                    broken = true;
                    Log.Message("[Rimfolk] Colony Manager's jobs couldn't be read; planning ahead leaves its targets alone.");
                }
            }
            catch (Exception e)
            {
                broken = true;
                Log.Message("[Rimfolk] Colony Manager's jobs couldn't be read (" + e.GetType().Name + "); planning ahead leaves its targets alone.");
            }
        }

        /// <summary>The mod is here and its jobs can be read.</summary>
        public static bool Present
        {
            get
            {
                Look();
                return managerType != null && !broken;
            }
        }

        /// <summary>The Managing work type the mod adds, if it is loaded.</summary>
        public static WorkTypeDef ManagingWork => Present ? DefDatabase<WorkTypeDef>.GetNamedSilentFail("Managing") : null;

        /// <summary>The map's manager, without making one where there is none.</summary>
        private static object ManagerOf(Map map)
        {
            if (!Present || map?.components == null) return null;
            foreach (var c in map.components)
                if (c != null && managerType.IsInstanceOfType(c)) return c;
            return null;
        }

        private static object Read(PropertyInfo p, object o)
        {
            try
            {
                return p == null || o == null ? null : p.GetValue(o, null);
            }
            catch
            {
                return null;
            }
        }

        private static T ReadField<T>(FieldInfo f, object o, T fallback)
        {
            try
            {
                return f != null && o != null && f.GetValue(o) is T v ? v : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>The job's own trigger: the field the job's type declares (it hides the base class's), read from the job's type down.</summary>
        private static object TriggerOf(object job)
        {
            for (var t = job.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var f = t.GetField("Trigger", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f == null) continue;
                var v = f.GetValue(job);
                if (v != null) return v;
            }
            return null;
        }

        private static string KindOf(string typeName)
        {
            switch (typeName)
            {
                case "ManagerJob_Hunting": return AheadMoreMath.Hunting;
                case "ManagerJob_Forestry": return AheadMoreMath.Forestry;
                case "ManagerJob_Mining": return AheadMoreMath.Mining;
                case "ManagerJob_Livestock": return AheadMoreMath.Livestock;
                case "ManagerJob_Foraging": return AheadMoreMath.Foraging;
            }
            return null;
        }

        private static IEnumerable<object> RawJobs(Map map)
        {
            var manager = ManagerOf(map);
            var stack = manager == null ? null : Read(stackProp, manager);
            if (stack == null) yield break;
            IEnumerable list;
            try
            {
                list = fullStack.Invoke(stack, null) as IEnumerable;
            }
            catch
            {
                yield break;
            }
            if (list == null) yield break;
            foreach (var j in list)
                if (j != null)
                    yield return j;
        }

        /// <summary>The manager's jobs on a map, in its order, as numbers; none without the mod or a manager.</summary>
        public static List<Job> Jobs(Map map)
        {
            var result = new List<Job>();
            if (!Present) return result;
            try
            {
                var seen = new Dictionary<string, int>();
                foreach (var raw in RawJobs(map))
                {
                    string typeName = raw.GetType().Name;
                    string kind = KindOf(typeName);
                    if (kind == null) continue;
                    seen.TryGetValue(typeName, out int index);
                    seen[typeName] = index + 1;
                    var j = new Job { job = raw, typeName = typeName, index = index, trigger = TriggerOf(raw) };
                    var info = j.info;
                    info.kind = kind;
                    info.key = kind + ":" + index;
                    info.label = Read(labelProp, raw) as string ?? kind;
                    info.suspended = Read(suspendedProp, raw) as bool? ?? false;
                    info.managed = Read(managedProp, raw) as bool? ?? true;
                    info.completed = Read(completedProp, raw) as bool? ?? false;
                    if (thresholdType != null && thresholdType.IsInstanceOfType(j.trigger))
                    {
                        info.threshold = true;
                        info.target = ReadField(targetField, j.trigger, 0);
                        info.max = ReadField(maxField, j.trigger, 0);
                        info.current = Read(currentProp, j.trigger) as int? ?? 0;
                        if (ReadField<ThingFilter>(filterField, j.trigger, null) is ThingFilter filter)
                            foreach (var d in filter.AllowedThingDefs)
                                if (d != null) j.goods.Add(d);
                        info.goods = GoodsWords(j.goods);
                        // Logging keeps a stock of wood; clearing jobs only clear.
                        bool logging = kind != AheadMoreMath.Forestry || forestryTypeProp == null || (Read(forestryTypeProp, raw)?.ToString() ?? "Logging") == "Logging";
                        info.threshold = logging;
                        info.raises = logging && (kind == AheadMoreMath.Hunting || kind == AheadMoreMath.Forestry || kind == AheadMoreMath.Mining);
                    }
                    else if (pawnKindType != null && pawnKindType.IsInstanceOfType(j.trigger))
                    {
                        if (ReadField<IDictionary>(countTargetsField, j.trigger, null) is IDictionary targets)
                            foreach (var v in targets.Values)
                                if (v is int n) info.target += n;
                        if (Read(countsProp, j.trigger) is int[] counts) info.current = counts.Sum();
                        var kindDef = ReadField<PawnKindDef>(pawnKindField, j.trigger, null);
                        info.goods = kindDef?.label ?? info.label;
                    }
                    if (kind == AheadMoreMath.Mining && ReadField<IDictionary>(mineralsField, raw, null) is IDictionary minerals)
                        foreach (DictionaryEntry e in minerals)
                            if (e.Key is ThingDef rock && e.Value is bool on && on) j.minerals.Add(rock);
                    result.Add(j);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony manager jobs", e);
            }
            return result;
        }

        /// <summary>A few of the goods a job counts, in words ("raw meat"; "steel, gold and 4 more").</summary>
        private static string GoodsWords(HashSet<ThingDef> goods)
        {
            if (goods.Count == 0) return "-";
            var labels = goods.OrderBy(d => d.label).Select(d => d.label).ToList();
            if (labels.Count <= 3) return TextUtil.JoinAnd(labels);
            return "CC_AheadMore_GoodsMore".Translate(string.Join(", ", labels.Take(2)), labels.Count - 2).Resolve();
        }

        private static readonly Dictionary<int, (float time, List<Job> jobs)> kept = new Dictionary<int, (float, List<Job>)>();

        /// <summary>The jobs as read within the last two seconds (for what is asked every frame, such as whether a step can still be undone).</summary>
        private static List<Job> JobsKept(Map map)
        {
            if (map == null) return new List<Job>();
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (kept.TryGetValue(map.uniqueID, out var k) && now - k.time < 2f && now >= k.time) return k.jobs;
            var jobs = Jobs(map);
            kept[map.uniqueID] = (now, jobs);
            return jobs;
        }

        /// <summary>
        /// The job of this kind at this place on the map (falling back to the one whose target is <paramref name="target"/>),
        /// else null; read afresh, or as read within the last two seconds when <paramref name="recent"/>.
        /// </summary>
        public static Job Find(Map map, string typeName, int index, int target = -1, bool recent = false)
        {
            var all = (recent ? JobsKept(map) : Jobs(map)).Where(j => j.typeName == typeName).ToList();
            var at = all.FirstOrDefault(j => j.index == index);
            if (target >= 0 && (at == null || at.info.target != target)) at = all.FirstOrDefault(j => j.info.target == target) ?? at;
            return at;
        }

        /// <summary>Sets a stock target; false when it couldn't.</summary>
        public static bool SetTarget(Job j, int to)
        {
            if (j?.trigger == null || targetField == null || !thresholdType.IsInstanceOfType(j.trigger)) return false;
            try
            {
                targetField.SetValue(j.trigger, to);
                kept.Clear();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony manager target", e);
                return false;
            }
        }

        /// <summary>Suspends a job or resumes it; false when it couldn't.</summary>
        public static bool SetSuspended(Job j, bool suspended)
        {
            if (j?.job == null || suspendedProp == null || !suspendedProp.CanWrite) return false;
            try
            {
                suspendedProp.SetValue(j.job, suspended, null);
                kept.Clear();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony manager suspend", e);
                return false;
            }
        }

        /// <summary>The manager desks on the map (the colony's), when the mod has them.</summary>
        public static List<Building> Desks(Map map)
        {
            var list = new List<Building>();
            if (!Present || stationType == null || map == null) return list;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b != null && b.Spawned && stationType.IsInstanceOfType(b)) list.Add(b);
            return list;
        }

        /// <summary>The work is done at a desk (the mod has them).</summary>
        public static bool DeskNeeded => Present && stationType != null;

        /// <summary>A desk a manager can work at now: allowed, powered where it takes power, not broken down.</summary>
        public static bool DeskWorks(Building b)
        {
            if (b == null || !b.Spawned) return false;
            var player = Faction.OfPlayerSilentFail;
            if (player != null && b.IsForbidden(player)) return false;
            if (b.GetComp<CompPowerTrader>() is CompPowerTrader power && !power.PowerOn) return false;
            if (b.GetComp<CompBreakdownable>() is CompBreakdownable broke && broke.BrokenDown) return false;
            return true;
        }

        /// <summary>A desk the colony could build now: one of the mod's desks whose research is done, the plainest first.</summary>
        public static ThingDef BuildableDesk()
        {
            if (!DeskNeeded) return null;
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d?.thingClass != null && stationType.IsAssignableFrom(d.thingClass) && d.BuildableByPlayer && (d.researchPrerequisites == null || d.researchPrerequisites.All(r => r == null || r.IsFinished)))
                .OrderBy(d => d.researchPrerequisites?.Count ?? 0).ThenBy(d => d.defName).FirstOrDefault();
        }
    }
}
