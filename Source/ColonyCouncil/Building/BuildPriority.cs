using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Build priorities on one map (the setting "Build priorities"): each of the colony's blueprints and frames can be
    /// set low, normal, high or urgent with a button when it's selected (normal unless set). Builders take on nothing
    /// at a level while something at a higher one can go ahead: its materials are on the map (not forbidden), the
    /// colony can reach it and someone here has the skill for it. What can't go ahead (materials missing, forbidden,
    /// walled off, beyond everyone's skill) holds nothing up, and a job given by hand ("prioritize") is always taken. The level goes with a blueprint when
    /// it becomes a frame, and shows on the map as a light tint (BuildPriorityText.Tint). Looked at every 250 ticks while
    /// any level is set, and at once when one is changed.
    /// </summary>
    public class BuildPriorities : MapComponent
    {
        private Dictionary<Thing, int> levels = new Dictionary<Thing, int>();
        private List<Thing> keys;
        private List<int> values;

        /// <summary>The highest level with something that can be built now (from the last look); int.MinValue for none.</summary>
        private int top = int.MinValue;

        public BuildPriorities(Map map) : base(map)
        {
        }

        public static bool On => ColonyCouncilMod.Settings?.buildPriorities ?? true;

        public static BuildPriorities Of(Map map) => map?.GetComponent<BuildPriorities>();

        public int LevelOf(Thing t) => t != null && levels.TryGetValue(t, out int l) ? l : BuildPriorityMath.Normal;

        public void Set(Thing t, int level)
        {
            if (t == null) return;
            level = BuildPriorityMath.Clamp(level);
            if (level == BuildPriorityMath.Normal) levels.Remove(t);
            else levels[t] = level;
            Look();
        }

        /// <summary>Whether a builder leaves this for now: something at a higher level can go ahead. Never for a job given by hand.</summary>
        public static bool Waits(Thing t, bool forced)
        {
            if (forced || t?.Map == null || !On || !RimfolkPatches.On) return false;
            var c = Of(t.Map);
            return c != null && c.levels.Count > 0 && BuildPriorityMath.Waits(c.LevelOf(t), c.top);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Look();
        }

        public override void MapComponentTick()
        {
            if (levels.Count == 0) return;
            if (!Sched.Every(Find.TickManager.TicksGame, Sched.RareTicks, Sched.RareBuildPriority)) return;
            try
            {
                Look();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("build priorities", e);
                top = int.MinValue;
            }
        }

        /// <summary>What can go ahead now, level by level from the top.</summary>
        private void Look()
        {
            levels.RemoveAll(kv => kv.Key == null || kv.Key.Destroyed || !kv.Key.Spawned || kv.Key.Map != map);
            top = int.MinValue;
            if (levels.Count == 0) return;
            int skill = BestSkill();
            var stock = new Dictionary<ThingDef, int>();
            bool anyLow = false;
            foreach (var kv in levels)
            {
                if (kv.Value < BuildPriorityMath.Normal)
                {
                    anyLow = true;
                    continue;
                }
                if (kv.Value > top && CanGoAhead(kv.Key, skill, stock)) top = kv.Value;
            }
            // Only something set low can wait on the ordinary work: is any of that able to go ahead?
            if (top == int.MinValue && anyLow && AnyNormal(skill, stock)) top = BuildPriorityMath.Normal;
        }

        private bool AnyNormal(int skill, Dictionary<ThingDef, int> stock)
        {
            foreach (var group in new[] { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame })
                foreach (var t in map.listerThings.ThingsInGroup(group))
                    if (t.Faction == Faction.OfPlayer && !levels.ContainsKey(t) && CanGoAhead(t, skill, stock))
                        return true;
            return false;
        }

        /// <summary>The best construction skill among those here who build.</summary>
        private int BestSkill()
        {
            int best = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) continue;
                int level = p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0;
                if (level > best) best = level;
            }
            return best;
        }

        /// <summary>Not forbidden, reachable from the colony, within someone's skill, and every material it still needs is on the map, not forbidden.</summary>
        private bool CanGoAhead(Thing t, int skill, Dictionary<ThingDef, int> stock)
        {
            if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.Position.Fogged(map) || !map.reachability.CanReachColony(t.Position)) return false;
            var entity = t.def?.entityDefToBuild;
            if (entity != null && entity.constructionSkillPrerequisite > skill) return false;
            if (!(t is IConstructible c)) return true;
            var cost = c.TotalMaterialCost();
            if (cost == null) return true;
            foreach (var need in cost)
            {
                if (need?.thingDef == null) continue;
                int n = c.ThingCountNeeded(need.thingDef);
                if (n > 0 && Stock(need.thingDef, stock) < n) return false;
            }
            return true;
        }

        private int Stock(ThingDef def, Dictionary<ThingDef, int> stock)
        {
            if (stock.TryGetValue(def, out int n)) return n;
            n = 0;
            foreach (var t in map.listerThings.ThingsOfDef(def))
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer))
                    n += t.stackCount;
            stock[def] = n;
            return n;
        }

        /// <summary>A light tint over each prioritized blueprint and frame in view: red for urgent, amber for high, blue for low.</summary>
        public override void MapComponentUpdate()
        {
            if (levels.Count == 0 || !On || Find.CurrentMap != map || WorldRendererUtility.WorldSelected || Find.UIRoot?.screenshotMode?.Active == true) return;
            try
            {
                var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
                foreach (var kv in levels)
                {
                    var t = kv.Key;
                    if (t == null || !t.Spawned) continue;
                    var mat = BuildPriorityText.Tint(kv.Value);
                    if (mat == null) continue;
                    foreach (var c in t.OccupiedRect())
                        if (view.Contains(c))
                            Graphics.DrawMesh(MeshPool.plane10, c.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays), Quaternion.identity, mat, 0);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("build priority tints", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Nothing that's gone is written down (a reference to it couldn't be found again on loading).
            if (Scribe.mode == LoadSaveMode.Saving) levels.RemoveAll(kv => kv.Key == null || kv.Key.Destroyed);
            Scribe_Collections.Look(ref levels, "levels", LookMode.Reference, LookMode.Value, ref keys, ref values);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                levels ??= new Dictionary<Thing, int>();
                levels.RemoveAll(kv => kv.Key == null);
            }
        }
    }

    /// <summary>The words and colors for each level.</summary>
    public static class BuildPriorityText
    {
        public static string Name(int level) => ("CC_BuildPrio_" + Key(level)).Translate();

        private static string Key(int level) =>
            level >= BuildPriorityMath.Urgent ? "Urgent" : level == BuildPriorityMath.High ? "High" : level <= BuildPriorityMath.Low ? "Low" : "Normal";

        public static Color Color(int level) =>
            level >= BuildPriorityMath.Urgent ? new Color(1f, 0.45f, 0.32f)
            : level == BuildPriorityMath.High ? new Color(1f, 0.82f, 0.32f)
            : level <= BuildPriorityMath.Low ? new Color(0.5f, 0.68f, 1f)
            : UnityEngine.Color.white;

        /// <summary>How strongly the tint shows over a blueprint or frame: enough to tell apart, not enough to hide it.</summary>
        public const float TintAlpha = 0.22f;

        private static Material urgent, high, low;

        /// <summary>The see-through tint for a level on the map (made on first use, on the main thread); null for normal.</summary>
        public static Material Tint(int level)
        {
            if (level >= BuildPriorityMath.Urgent) return urgent ??= Make(level);
            if (level == BuildPriorityMath.High) return high ??= Make(level);
            if (level <= BuildPriorityMath.Low) return low ??= Make(level);
            return null;
        }

        private static Material Make(int level)
        {
            var c = Color(level);
            c.a = TintAlpha;
            return SolidColorMaterials.SimpleSolidColorMaterial(c);
        }
    }

    /// <summary>
    /// The button on a selected blueprint or frame: a click steps the level (normal, high, urgent, low) for every one
    /// selected; a right-click picks one from a menu.
    /// </summary>
    public class Command_BuildPriority : Command_Action
    {
        private const int GroupKey = 0x5B1D_7A11;

        private readonly Thing thing;
        private readonly int level;

        public Command_BuildPriority(Thing thing, int level)
        {
            this.thing = thing;
            this.level = level;
            groupKey = GroupKey;
            icon = Icons.Priorities;
            defaultLabel = "CC_BuildPrio_Button".Translate(BuildPriorityText.Name(level)).Resolve();
            defaultDesc = "CC_BuildPrio_ButtonDesc".Translate().Resolve();
            defaultIconColor = BuildPriorityText.Color(level);
            action = () => Apply(new[] { thing }, BuildPriorityMath.Next(level));
        }

        /// <summary>Clicked with others selected: all of them go to the level after this one's, so they end up alike.</summary>
        public override void ProcessGroupInput(Event ev, List<Gizmo> group)
        {
            var things = new List<Thing> { thing };
            if (group != null)
                foreach (var g in group)
                    if (g is Command_BuildPriority c && c.thing != null && !things.Contains(c.thing))
                        things.Add(c.thing);
            Apply(things, BuildPriorityMath.Next(level));
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                foreach (int l in BuildPriorityMath.Levels)
                {
                    int to = l;
                    yield return new FloatMenuOption(BuildPriorityText.Name(to).CapitalizeFirst(), () => Apply(Selected(), to));
                }
            }
        }

        /// <summary>Every blueprint and frame of the colony's that's selected (this one if none).</summary>
        private List<Thing> Selected()
        {
            var list = Find.Selector.SelectedObjects.OfType<Thing>().Where(t => (t is Blueprint_Build || t is Frame) && t.Faction == Faction.OfPlayer).ToList();
            if (list.Count == 0 && thing != null) list.Add(thing);
            return list;
        }

        private static void Apply(IEnumerable<Thing> things, int to)
        {
            foreach (var t in things)
                BuildPriorities.Of(t?.Map)?.Set(t, to);
        }

        public static Command_BuildPriority For(Thing t)
        {
            if (!BuildPriorities.On || !RimfolkPatches.On || t?.Map == null || t.Faction != Faction.OfPlayer) return null;
            var c = BuildPriorities.Of(t.Map);
            return c == null ? null : new Command_BuildPriority(t, c.LevelOf(t));
        }
    }

    // ---------------- The game's hooks ----------------

    /// <summary>Delivering materials to a blueprint waits for what's set higher (BuildPriorities).</summary>
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), nameof(WorkGiver_ConstructDeliverResourcesToBlueprints.HasJobOnThing))]
    public static class Patch_BuildPriority_BlueprintsHas
    {
        public static void Postfix(Thing t, bool forced, ref bool __result)
        {
            if (__result && BuildPriorities.Waits(t, forced)) __result = false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), nameof(WorkGiver_ConstructDeliverResourcesToBlueprints.JobOnThing))]
    public static class Patch_BuildPriority_BlueprintsJob
    {
        public static void Postfix(Thing t, bool forced, ref Job __result)
        {
            if (__result != null && BuildPriorities.Waits(t, forced)) __result = null;
        }
    }

    /// <summary>Delivering materials to a frame waits for what's set higher.</summary>
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToFrames), nameof(WorkGiver_ConstructDeliverResourcesToFrames.HasJobOnThing))]
    public static class Patch_BuildPriority_FramesHas
    {
        public static void Postfix(Thing t, bool forced, ref bool __result)
        {
            if (__result && BuildPriorities.Waits(t, forced)) __result = false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToFrames), nameof(WorkGiver_ConstructDeliverResourcesToFrames.JobOnThing))]
    public static class Patch_BuildPriority_FramesJob
    {
        public static void Postfix(Thing t, bool forced, ref Job __result)
        {
            if (__result != null && BuildPriorities.Waits(t, forced)) __result = null;
        }
    }

    /// <summary>Finishing a frame waits for what's set higher (its HasJobOnThing asks this).</summary>
    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), nameof(WorkGiver_ConstructFinishFrames.JobOnThing))]
    public static class Patch_BuildPriority_FinishJob
    {
        public static void Postfix(Thing t, bool forced, ref Job __result)
        {
            if (__result != null && BuildPriorities.Waits(t, forced)) __result = null;
        }
    }

    /// <summary>A blueprint that becomes a frame keeps its level.</summary>
    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryReplaceWithSolidThing))]
    public static class Patch_BuildPriority_Carry
    {
        public static void Prefix(Blueprint __instance, out int __state)
        {
            __state = BuildPriorities.Of(__instance?.Map)?.LevelOf(__instance) ?? BuildPriorityMath.Normal;
        }

        public static void Postfix(bool __result, ref Thing createdThing, int __state)
        {
            if (!__result || createdThing == null || __state == BuildPriorityMath.Normal) return;
            BuildPriorities.Of(createdThing.Map)?.Set(createdThing, __state);
        }
    }

    [HarmonyPatch(typeof(Blueprint_Build), nameof(Blueprint_Build.GetGizmos))]
    public static class Patch_BuildPriority_BlueprintGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Blueprint_Build __instance)
        {
            foreach (var g in __result) yield return g;
            var c = Command_BuildPriority.For(__instance);
            if (c != null) yield return c;
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.GetGizmos))]
    public static class Patch_BuildPriority_FrameGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Frame __instance)
        {
            foreach (var g in __result) yield return g;
            var c = Command_BuildPriority.For(__instance);
            if (c != null) yield return c;
        }
    }

    /// <summary>The inspect pane says a level that isn't normal.</summary>
    [HarmonyPatch(typeof(Blueprint_Build), nameof(Blueprint_Build.GetInspectString))]
    public static class Patch_BuildPriority_BlueprintInspect
    {
        public static void Postfix(Blueprint_Build __instance, ref string __result) => __result = BuildPriorityInspect.Add(__instance, __result);
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.GetInspectString))]
    public static class Patch_BuildPriority_FrameInspect
    {
        public static void Postfix(Frame __instance, ref string __result) => __result = BuildPriorityInspect.Add(__instance, __result);
    }

    public static class BuildPriorityInspect
    {
        public static string Add(Thing t, string text)
        {
            if (!BuildPriorities.On || t?.Map == null) return text;
            int level = BuildPriorities.Of(t.Map)?.LevelOf(t) ?? BuildPriorityMath.Normal;
            if (level == BuildPriorityMath.Normal) return text;
            string line = "CC_BuildPrio_Inspect".Translate(BuildPriorityText.Name(level)).Resolve();
            return text.NullOrEmpty() ? line : text + "\n" + line;
        }
    }
}
