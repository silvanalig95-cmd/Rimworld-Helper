using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The ways a step is taken back, for any source of suggestions: each saved with the step (SuggestDone.undo).

    /// <summary>Bills' "do until" counts put back (every bill Better Workbench Management links with it changed together).</summary>
    public class UndoBillTarget : SuggestUndo
    {
        public List<Bill_Production> bills = new List<Bill_Production>();
        public List<int> targets = new List<int>();

        public override bool Possible(Map map) => bills.Any(b => b != null && !b.DeletedOrDereferenced);

        public override void Undo(Map map)
        {
            for (int i = 0; i < bills.Count && i < targets.Count; i++)
                if (bills[i] != null && !bills[i].DeletedOrDereferenced) bills[i].targetCount = targets[i];
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                for (int i = 0; i < bills.Count; i++)
                    if (bills[i] != null && bills[i].DeletedOrDereferenced) bills[i] = null;
            Scribe_Collections.Look(ref bills, "bills", LookMode.Reference);
            Scribe_Collections.Look(ref targets, "targets", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bills ??= new List<Bill_Production>();
                targets ??= new List<int>();
            }
        }
    }

    /// <summary>Bills that were added, taken off their benches again.</summary>
    public class UndoBillsAdded : SuggestUndo
    {
        public List<Bill_Production> bills = new List<Bill_Production>();

        public override bool Possible(Map map) => bills.Any(b => b != null && !b.DeletedOrDereferenced);

        public override void Undo(Map map)
        {
            foreach (var b in bills)
                if (b != null && !b.DeletedOrDereferenced && b.billStack != null) b.billStack.Delete(b);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) bills.RemoveAll(b => b == null || b.DeletedOrDereferenced);
            Scribe_Collections.Look(ref bills, "bills", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bills ??= new List<Bill_Production>();
                bills.RemoveAll(b => b == null);
            }
        }
    }

    /// <summary>One setting of a bill put back: suspended again, its ingredient radius, or the one it was kept for.</summary>
    public class UndoBillSetting : SuggestUndo
    {
        public const string Suspend = "suspend", Radius = "radius", Worker = "worker";

        public Bill_Production bill;
        public string what;
        public float radius;
        public Pawn pawn;

        public override bool Possible(Map map) => bill != null && !bill.DeletedOrDereferenced;

        public override void Undo(Map map)
        {
            if (!Possible(map)) return;
            switch (what)
            {
                case Suspend:
                    bill.suspended = true;
                    break;
                case Radius:
                    bill.ingredientSearchRadius = radius;
                    break;
                case Worker:
                    if (pawn != null && !pawn.Dead) bill.SetPawnRestriction(pawn);
                    break;
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && bill != null && bill.DeletedOrDereferenced) bill = null;
            Scribe_References.Look(ref bill, "bill");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref radius, "radius");
            Scribe_References.Look(ref pawn, "pawn");
        }
    }

    /// <summary>A colonist's priority for a kind of work put back.</summary>
    public class UndoWorkPriority : SuggestUndo
    {
        public Pawn pawn;
        public WorkTypeDef work;
        public int priority;

        public override bool Possible(Map map) => pawn != null && !pawn.Dead && pawn.workSettings != null && work != null;

        public override void Undo(Map map)
        {
            if (Possible(map) && !pawn.WorkTypeIsDisabled(work)) pawn.workSettings.SetPriority(work, priority);
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            CouncilScribe.LookDef(ref work, "work");
            Scribe_Values.Look(ref priority, "priority");
        }
    }

    /// <summary>A thing forbidden again (or allowed again), as it was.</summary>
    public class UndoForbid : SuggestUndo
    {
        public Thing thing;
        public bool wasForbidden;

        public override bool Possible(Map map) => thing != null && thing.Spawned;

        public override void Undo(Map map)
        {
            if (Possible(map)) thing.SetForbidden(wasForbidden, false);
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref wasForbidden, "wasForbidden");
        }
    }

    /// <summary>
    /// A growing zone's change taken back: the cells added to it taken off again (the zone gone when nothing else is
    /// left of it), a zone made deleted, cells moved from another zone given back to it, its sowing switched off again.
    /// </summary>
    public class UndoZone : SuggestUndo
    {
        public const string Added = "added", Made = "made", Moved = "moved", Sowing = "sowing", Plant = "plant";

        public string what;
        public int zoneId = -1, fromId = -1;
        public List<IntVec3> cells = new List<IntVec3>();
        /// <summary>The crop it grew before (Plant).</summary>
        public ThingDef plant;

        private static Zone ZoneOf(Map map, int id) => id < 0 ? null : map?.zoneManager.AllZones.FirstOrDefault(z => z.ID == id);

        public override bool Possible(Map map) => ZoneOf(map, zoneId) != null;

        public override void Undo(Map map)
        {
            var zone = ZoneOf(map, zoneId);
            if (zone == null) return;
            switch (what)
            {
                case Added:
                    if (zone.Cells.Count > cells.Count(zone.ContainsCell))
                    {
                        foreach (var c in cells)
                            if (zone.ContainsCell(c)) zone.RemoveCell(c);
                    }
                    else zone.Delete();
                    break;
                case Made:
                    zone.Delete();
                    break;
                case Moved:
                {
                    var from = ZoneOf(map, fromId);
                    var back = cells.Where(zone.ContainsCell).ToList();
                    zone.Delete();
                    if (from != null)
                        foreach (var c in back)
                            if (map.zoneManager.ZoneAt(c) == null) from.AddCell(c);
                    break;
                }
                case Sowing:
                    if (zone is Zone_Growing g) g.allowSow = false;
                    break;
                case Plant:
                    if (zone is Zone_Growing grown && plant != null) grown.SetPlantDefToGrow(plant);
                    break;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref zoneId, "zoneId", -1);
            Scribe_Values.Look(ref fromId, "fromId", -1);
            CouncilScribe.LookDef(ref plant, "plant");
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cells ??= new List<IntVec3>();
        }
    }
}
