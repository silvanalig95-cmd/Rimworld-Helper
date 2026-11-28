using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ground of one home map (docs/design/living-land-ground.md): what it remembers between looks, and the look itself,
    /// every 250 ticks (Sched.RareGround). Each hour it watches for a quake going on (Seasonal Weather's earthquake, or any
    /// other condition by that name), leaves the mark a quake noted earlier may leave (Nature/GroundMarks.cs), opens a
    /// sinkhole whose sign was seen, and turns the soil where ash lay; each day it asks whether the rain brings a hillside
    /// down, whether the season that ended was a wet one, and whether the mountain wakes (Nature/GroundWaking.cs). While the
    /// mountain is awake, each look may bring a tremor, and smoke over it while the ash comes down. Kept small: a few
    /// numbers, and the cells where the ash lies thickest until they turn (GroundMath.MaxCells at most).
    /// </summary>
    public class GroundMap : MapComponent
    {
        // ---------------- Marks ----------------

        /// <summary>The tick the last mark was left here (any kind), -1 for none.</summary>
        public int lastMark = -1;

        /// <summary>A quake noted and waiting to leave its mark: when it came, what it was, where it was felt most (Invalid: anywhere), when the mark may come.</summary>
        public int quakeNoted = -1, quakeDue = -1;
        public QuakeSource quakeSource;
        public IntVec3 quakeAt = IntVec3.Invalid;

        /// <summary>The last day the rain was asked whether it brings a hillside down, and the last season looked at for a wet one.</summary>
        public int slideDay = -1, seasonSeen = -1;

        /// <summary>A sinkhole whose sign is on the ground: its middle, its cells, when it opens (-1: none).</summary>
        public IntVec3 sinkAt = IntVec3.Invalid;
        public int sinkCells, sinkDue = -1;

        // ---------------- The mountain ----------------

        public WakeStage stage = WakeStage.Quiet;

        /// <summary>When the stage ends, when the mountain last woke (-1: never), the last day it was asked whether it wakes.</summary>
        public int stageUntil = -1, lastWake = -1, wakeDay = -1;

        /// <summary>The warm vents opened at the end of the tremors (1: done).</summary>
        public int vents;

        /// <summary>Where the mountain is: a rock face of the map's own rock, or (none on the map) the edge it lies beyond.</summary>
        public IntVec3 wakeFrom = IntVec3.Invalid;
        public bool wakeOnRock;

        /// <summary>The cells where the ash lies thickest, which turn rich when <see cref="ashDue"/> comes (GroundMath.MaxCells at most).</summary>
        public List<IntVec3> ash = new List<IntVec3>();
        public int ashDue = -1;

        /// <summary>Odyssey's volcanic ash in the air at the last look (not saved: a load looks again).</summary>
        [Unsaved] internal bool odysseyAshOn;

        /// <summary>A quake condition going on at the last look (not saved).</summary>
        [Unsaved] internal bool quakeOn;

        public GroundMap(Map map) : base(map)
        {
        }

        public static GroundMap Of(Map map) => map?.GetComponent<GroundMap>();

        /// <summary>Either part is on.</summary>
        public static bool AnyOn => GroundMarks.On || GroundWaking.On;

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareGround) || !AnyOn) return;
            if (!map.IsPlayerHome || MapFacts.InVacuum(map) || !Homes.IsHome(map)) return;
            using var clock = PassClock.Time(Pass.RareGround);
            try
            {
                Look(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground", e);
            }
        }

        private void Look(int now)
        {
            // Every look: the mountain awake.
            if (stage == WakeStage.Tremors || stage == WakeStage.Ash) GroundWaking.Look(this, map, now);
            // Once an hour (the hour's first look).
            if (Sched.Mod(now, Sched.HourTicks) >= Sched.RareTicks) return;
            GroundMarks.Hourly(this, map, now);
            GroundWaking.Hourly(this, map, now);
            // Once a day, at the first hour's look; the rain whenever it falls.
            GroundMarks.RainHour(this, map, now);
            if (!Sched.FirstHourOfDay(now)) return;
            GroundMarks.Daily(this, map, now);
            GroundWaking.Daily(this, map, now);
        }

        /// <summary>The ash lying here is forgotten: its cells and its time.</summary>
        internal void ClearAsh()
        {
            ash.Clear();
            ashDue = -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastMark, "lastMark", -1);
            Scribe_Values.Look(ref quakeNoted, "quakeNoted", -1);
            Scribe_Values.Look(ref quakeDue, "quakeDue", -1);
            Scribe_Values.Look(ref quakeSource, "quakeSource");
            Scribe_Values.Look(ref quakeAt, "quakeAt", IntVec3.Invalid);
            Scribe_Values.Look(ref slideDay, "slideDay", -1);
            Scribe_Values.Look(ref seasonSeen, "seasonSeen", -1);
            Scribe_Values.Look(ref sinkAt, "sinkAt", IntVec3.Invalid);
            Scribe_Values.Look(ref sinkCells, "sinkCells");
            Scribe_Values.Look(ref sinkDue, "sinkDue", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref stageUntil, "stageUntil", -1);
            Scribe_Values.Look(ref lastWake, "lastWake", -1);
            Scribe_Values.Look(ref wakeDay, "wakeDay", -1);
            Scribe_Values.Look(ref vents, "vents");
            Scribe_Values.Look(ref wakeFrom, "wakeFrom", IntVec3.Invalid);
            Scribe_Values.Look(ref wakeOnRock, "wakeOnRock");
            Scribe_Collections.Look(ref ash, "ash", LookMode.Value);
            Scribe_Values.Look(ref ashDue, "ashDue", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ash ??= new List<IntVec3>();
                if (ash.Count > GroundMath.MaxCells) ash.RemoveRange(GroundMath.MaxCells, ash.Count - GroundMath.MaxCells);
            }
        }
    }

    /// <summary>
    /// One of the game's events happened at a home: if it is a quake (an earthquake by name, Rimfolk's rockslide or warm
    /// vent), the ground notes it, and a mark may follow once the shaking is over (Nature/GroundMarks.cs). The event and its
    /// letter are untouched. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_Ground_TryExecute
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !RimfolkPatches.On || __instance?.def == null) return;
            try
            {
                if (parms?.target is Map map) GroundMarks.Noted(map, __instance.def.defName, __instance.GetType().Name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground: noting a quake", e);
            }
        }
    }
}
