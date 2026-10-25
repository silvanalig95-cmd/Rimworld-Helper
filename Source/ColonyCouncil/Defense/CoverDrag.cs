using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Taking cover with a drag (the arithmetic is in CoverDragMath): with drafted colonists selected, Ctrl and a
    /// right-drag sends them to cover. The cell pressed is where they gather and the drag points at the enemy; a
    /// Ctrl and right-click without a drag takes cover from the nearest enemies in sight. It runs on the game's own
    /// drag of several colonists (the one a plain right-drag starts), so pressing, dragging and letting go work as
    /// they always do: the game shows where each one will stand, and Rimfolk only chooses those cells and gives the
    /// orders. Each cell's cover shows under it while dragging. A right-click on something with its own orders (an
    /// enemy, an item, a bed) is left to the game, and so is everything without Ctrl. A setting, with the deep hooks.
    /// </summary>
    public static class CoverDrag
    {
        private static bool on;
        private static bool aimedByDrag;
        private static readonly List<float> shownCover = new List<float>();

        // The cells read for the drag as it stands, kept a moment: the game asks again every few ticks while it runs.
        private static readonly List<CoverCell> cells = new List<CoverCell>();
        private static readonly List<IntVec3> spots = new List<IntVec3>();
        private static IntVec3 readStart = IntVec3.Invalid, readEnd = IntVec3.Invalid;
        private static int readTick = -9999, readCount;
        private static bool readAimed;
        private static float readDx, readDz;
        private const int ReadTicks = 60;

        private static readonly AccessTools.FieldRef<MultiPawnGotoController, IntVec3> StartOf = AccessTools.FieldRefAccess<MultiPawnGotoController, IntVec3>("start");
        private static readonly AccessTools.FieldRef<MultiPawnGotoController, IntVec3> EndOf = AccessTools.FieldRefAccess<MultiPawnGotoController, IntVec3>("end");
        private static readonly AccessTools.FieldRef<MultiPawnGotoController, List<Pawn>> PawnsOf = AccessTools.FieldRefAccess<MultiPawnGotoController, List<Pawn>>("pawns");
        private static readonly AccessTools.FieldRef<MultiPawnGotoController, List<IntVec3>> DestsOf = AccessTools.FieldRefAccess<MultiPawnGotoController, List<IntVec3>>("dests");

        /// <summary>The setting is on and every hook it needs was applied (else a Ctrl and right-drag is the game's own).</summary>
        public static bool Enabled =>
            (ColonyCouncilMod.Settings?.coverDrag ?? false) && RimfolkPatches.On
            && RimfolkPatches.Applied.Contains(nameof(Patch_CoverDragStart)) && RimfolkPatches.Applied.Contains(nameof(Patch_CoverDragCells))
            && RimfolkPatches.Applied.Contains(nameof(Patch_CoverDragOrders)) && RimfolkPatches.Applied.Contains(nameof(Patch_CoverDragEnd));

        /// <summary>The drag going on now is one to cover.</summary>
        public static bool Active => on;

        private static bool CtrlHeld(Event e) => e.control || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        /// <summary>
        /// A right press with Ctrl held, before the game reads it: with drafted colonists selected on the map in view,
        /// on open ground, the game's drag of several colonists starts with them (marked as one to cover), and the press
        /// is used up.
        /// </summary>
        public static void TryStart(Selector selector)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 1 || !CtrlHeld(e) || !Enabled) return;
            var map = Find.CurrentMap;
            if (map == null || WorldRendererUtility.WorldSelected || selector?.gotoController == null) return;
            var cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) return;
            var selected = selector.SelectedPawns;
            var drafted = new List<Pawn>();
            foreach (var p in selected)
                if (p != null && p.Spawned && p.Map == map && p.Drafted && FloatMenuMakerMap.ShouldGenerateFloatMenuForPawn(p).Accepted) drafted.Add(p);
            if (drafted.Count == 0 || HasOwnOrders(selected, UI.MouseMapPosition())) return;
            var gather = CellFinder.StandableCellNear(cell, map, 2.9f);
            if (!gather.IsValid) return;
            var controller = selector.gotoController;
            controller.StartInteraction(gather);
            foreach (var p in drafted) controller.AddPawn(p);
            on = true;
            e.Use();
        }

        /// <summary>The press is on something with orders of its own (attack, pick up, rescue...): the game handles it.</summary>
        private static bool HasOwnOrders(List<Pawn> selected, Vector3 at)
        {
            try
            {
                var options = FloatMenuMakerMap.GetOptions(selected, at, out _);
                if (options.NullOrEmpty()) return false;
                foreach (var o in options)
                    if (!o.isGoto) return true;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The cells for the drag as it stands (instead of the game's line): each colonist's cover, and what it's worth.</summary>
        public static void Choose(MultiPawnGotoController controller)
        {
            var pawns = PawnsOf(controller);
            var dests = DestsOf(controller);
            for (int i = 0; i < dests.Count; i++) dests[i] = IntVec3.Invalid;
            shownCover.Clear();
            for (int i = 0; i < dests.Count; i++) shownCover.Add(-1f);
            if (pawns.Count == 0 || pawns[0] == null || !pawns[0].Spawned) return;
            var map = pawns[0].Map;
            IntVec3 start = StartOf(controller), end = EndOf(controller);
            int now = Find.TickManager.TicksGame;
            if (start != readStart || end != readEnd || pawns.Count != readCount || now - readTick > ReadTicks || now < readTick)
            {
                Read(map, start, end, pawns);
                readTick = now;
            }
            bool aimed = readAimed;
            float dx = readDx, dz = readDz;
            var ranges = new List<float>();
            var laterals = new List<float>();
            foreach (var p in pawns)
            {
                ranges.Add(Reach(p));
                laterals.Add(p.Spawned ? CoverDragMath.Frame(p.Position.x, p.Position.z, start.x, start.z, dx, dz).lateral : 0f);
            }
            var chosen = CoverDragMath.Assign(cells, ranges, laterals, aimed, (p, c) => pawns[p].Spawned && FloatMenuOptionProvider_DraftedMove.PawnCanGoto(pawns[p], spots[c]).Accepted);
            for (int i = 0; i < pawns.Count && i < dests.Count; i++)
            {
                if (chosen[i] < 0) continue;
                dests[i] = spots[chosen[i]];
                shownCover[i] = aimed ? cells[chosen[i]].cover : -1f;
            }
            // Anyone left without a cell gathers near the cell pressed, as in the game's own drag.
            for (int i = 0; i < pawns.Count && i < dests.Count; i++)
            {
                var p = pawns[i];
                if (dests[i].IsValid || !p.Spawned) continue;
                var near = RCellFinder.BestOrderedGotoDestNear(start, p, c => !dests.Contains(c) && !map.exitMapGrid.IsExitCell(c) && FloatMenuOptionProvider_DraftedMove.PawnCanGoto(p, c).Accepted);
                if (near.IsValid) dests[i] = near;
            }
        }

        /// <summary>The cells around where they gather, with their cover from the enemy's side and whether a shooter there sees that way.</summary>
        private static void Read(Map map, IntVec3 start, IntVec3 end, List<Pawn> pawns)
        {
            cells.Clear();
            spots.Clear();
            readStart = start;
            readEnd = end;
            readCount = pawns.Count;
            bool aimed = Threat(map, start, end, out float dx, out float dz, out IntVec3 threat, out IntVec3 sight);
            aimedByDrag = aimed && (end - start).LengthHorizontalSquared >= 4;
            if (!aimed)
            {
                dx = 0f;
                dz = 1f;
            }
            readAimed = aimed;
            readDx = dx;
            readDz = dz;
            int half = CoverDragMath.HalfWidth(pawns.Count);
            int reach = Mathf.Max(half, CoverDragMath.RearDepth) + 2;
            foreach (var c in CellRect.CenteredOn(start, reach).ClipInsideMap(map))
            {
                var (lateral, depth) = CoverDragMath.Frame(c.x, c.z, start.x, start.z, dx, dz);
                if (Mathf.Abs(lateral) > half) continue;
                if (aimed ? depth < -CoverDragMath.RearDepth || depth > CoverDragMath.ForwardDepth : (c - start).LengthHorizontal > CoverDragMath.RearDepth) continue;
                if (!Open(c, map, pawns)) continue;
                float cover = aimed ? CoverUtility.CalculateOverallBlockChance(new LocalTargetInfo(c), threat, map) : Mathf.Min(1f, CoverUtility.TotalSurroundingCoverScore(c, map) / 3f);
                bool sees = !aimed || ShootLeanUtility.CellCanSeeCell(c, sight, map);
                cells.Add(new CoverCell { x = c.x, z = c.z, lateral = lateral, depth = depth, cover = cover, sees = sees });
                spots.Add(c);
            }
        }

        /// <summary>
        /// Where the enemy is: the way the drag points (from two cells), else the nearest enemies in sight within 70
        /// cells of where they gather. <paramref name="threat"/> is where cover is measured against, <paramref name="sight"/>
        /// where the shooters must be able to shoot toward. False when there's no enemy side.
        /// </summary>
        private static bool Threat(Map map, IntVec3 start, IntVec3 end, out float dx, out float dz, out IntVec3 threat, out IntVec3 sight)
        {
            dx = dz = 0f;
            threat = sight = IntVec3.Invalid;
            float len = (end - start).LengthHorizontal;
            if (len >= 2f)
            {
                dx = (end.x - start.x) / len;
                dz = (end.z - start.z) / len;
                float far = Mathf.Max(len, 15f);
                threat = new IntVec3(Mathf.RoundToInt(start.x + dx * far), 0, Mathf.RoundToInt(start.z + dz * far)).ClampInsideMap(map);
                sight = len >= 3f ? end : threat;
                return true;
            }
            float sx = 0f, sz = 0f;
            int n = 0;
            IntVec3 nearest = IntVec3.Invalid;
            float nearestDist = float.MaxValue;
            foreach (var t in map.attackTargetsCache.TargetsHostileToColony)
            {
                if (!(t.Thing is Pawn p) || !p.Spawned || p.Downed || p.Position.Fogged(map) || !GenHostility.IsActiveThreatToPlayer(t)) continue;
                float d = (p.Position - start).LengthHorizontal;
                if (d > 70f || d < 2f) continue;
                sx += p.Position.x;
                sz += p.Position.z;
                n++;
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = p.Position;
                }
            }
            if (n == 0) return false;
            var centre = new IntVec3(Mathf.RoundToInt(sx / n), 0, Mathf.RoundToInt(sz / n));
            float cl = (centre - start).LengthHorizontal;
            if (cl < 1f) return false;
            dx = (centre.x - start.x) / cl;
            dz = (centre.z - start.z) / cl;
            threat = centre.ClampInsideMap(map);
            sight = nearest;
            return true;
        }

        /// <summary>A cell someone can stand in to take cover: open ground in sight, no door, fire or way off the map, nobody else standing there.</summary>
        private static bool Open(IntVec3 c, Map map, List<Pawn> moving)
        {
            if (!c.Standable(map) || c.Fogged(map) || c.GetDoor(map) != null || map.exitMapGrid.IsExitCell(c) || c.ContainsStaticFire(map)) return false;
            var standing = c.GetFirstPawn(map);
            return standing == null || moving.Contains(standing);
        }

        /// <summary>How far a colonist's weapon reaches (a melee weapon or none: 1).</summary>
        private static float Reach(Pawn p)
        {
            var verb = p.equipment?.PrimaryEq?.PrimaryVerb ?? p.VerbTracker?.PrimaryVerb;
            if (verb?.verbProps == null || verb.verbProps.IsMeleeAttack) return 1f;
            return Mathf.Max(1f, verb.EffectiveRange);
        }

        /// <summary>Letting go: everyone is ordered to their cell (never off the map, wherever the drag ended).</summary>
        public static void Order(MultiPawnGotoController controller)
        {
            readTick = -9999;
            Choose(controller);
            var pawns = PawnsOf(controller);
            var dests = DestsOf(controller);
            bool any = false;
            for (int i = 0; i < pawns.Count && i < dests.Count; i++)
            {
                if (!dests[i].IsValid || !pawns[i].Spawned) continue;
                FloatMenuOptionProvider_DraftedMove.PawnGotoAction(dests[i], pawns[i], dests[i]);
                any = true;
            }
            if (any) SoundDefOf.ColonistOrdered.PlayOneShotOnCamera();
            controller.Deactivate();
        }

        public static void Ended()
        {
            on = false;
            aimedByDrag = false;
            shownCover.Clear();
            cells.Clear();
            spots.Clear();
            readStart = readEnd = IntVec3.Invalid;
        }

        private static readonly Color Good = new Color(0.55f, 0.9f, 0.5f), Fair = new Color(0.95f, 0.85f, 0.4f), Poor = new Color(0.95f, 0.5f, 0.45f);

        /// <summary>While dragging: each cell's cover from the enemy's side under the colonist's name, and a word at the drag's end.</summary>
        public static void Labels(MultiPawnGotoController controller)
        {
            var pawns = PawnsOf(controller);
            var dests = DestsOf(controller);
            for (int i = 0; i < pawns.Count && i < dests.Count && i < shownCover.Count; i++)
            {
                var c = dests[i];
                if (!c.IsValid || shownCover[i] < 0f || !pawns[i].Spawned || c.Fogged(pawns[i].Map)) continue;
                float cover = shownCover[i];
                var rect = c.ToUIRect();
                GenMapUI.DrawThingLabel(new Vector2(rect.center.x, rect.yMax + 19f), "CC_CoverDrag_Cover".Translate(Mathf.RoundToInt(cover * 100f)).Resolve(), cover >= 0.5f ? Good : cover >= 0.2f ? Fair : Poor);
            }
            if (aimedByDrag)
            {
                var end = EndOf(controller).ToUIRect();
                GenMapUI.DrawThingLabel(new Vector2(end.center.x, end.yMax + 5f), "CC_CoverDrag_EnemySide".Translate().Resolve(), Fair);
            }
        }
    }

    /// <summary>Ctrl and a right press with drafted colonists starts a drag to cover (Defense/CoverDrag.cs), before the game reads the press.</summary>
    [HarmonyPatch(typeof(Selector), nameof(Selector.SelectorOnGUI))]
    public static class Patch_CoverDragStart
    {
        public static void Prefix(Selector __instance)
        {
            try
            {
                CoverDrag.TryStart(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cover drag: start", e);
            }
        }
    }

    /// <summary>A drag to cover chooses its own cells (the game's line otherwise); if that fails, the game's.</summary>
    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.RecomputeDestinations))]
    public static class Patch_CoverDragCells
    {
        public static bool Prefix(MultiPawnGotoController __instance)
        {
            if (!CoverDrag.Active) return true;
            try
            {
                CoverDrag.Choose(__instance);
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cover drag: cells", e);
                CoverDrag.Ended();
                return true;
            }
        }
    }

    /// <summary>Letting go of a drag to cover gives the orders (the game's, with its line, otherwise).</summary>
    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.FinalizeInteraction))]
    public static class Patch_CoverDragOrders
    {
        public static bool Prefix(MultiPawnGotoController __instance)
        {
            if (!CoverDrag.Active) return true;
            try
            {
                CoverDrag.Order(__instance);
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cover drag: orders", e);
                CoverDrag.Ended();
                __instance.Deactivate();
                return false;
            }
        }
    }

    /// <summary>The drag is over (let go, cancelled or the selection cleared).</summary>
    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.Deactivate))]
    public static class Patch_CoverDragEnd
    {
        public static void Postfix() => CoverDrag.Ended();
    }

    /// <summary>Each cell's cover while dragging to cover.</summary>
    [HarmonyPatch(typeof(MultiPawnGotoController), nameof(MultiPawnGotoController.OnGUI))]
    public static class Patch_CoverDragLabels
    {
        public static void Postfix(MultiPawnGotoController __instance)
        {
            if (!CoverDrag.Active) return;
            try
            {
                CoverDrag.Labels(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cover drag: labels", e);
            }
        }
    }
}
