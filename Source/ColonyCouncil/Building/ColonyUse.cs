using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the colony actually used and made over the last few days, for the Making page's chains (Building/Chains.cs):
    /// what its people and animals ate at home, the medicine its doctors used, what the bills took and gave at each
    /// bench and who worked them, the fuel put into its stoves and generators, and the clothes worn out. A small rolling
    /// record (<see cref="UseLog"/>), counted by the game's own events through the deeper game hooks; without them
    /// nothing is counted and the chains go by their estimates.
    /// </summary>
    public class ColonyUse : IExposable
    {
        public UseLog log = new UseLog();

        /// <summary>The record read for one map: what was used a day, what each bench made a day, and who worked each bench.</summary>
        public class Read
        {
            /// <summary>The record covers enough time for its rates to go by.</summary>
            public bool measured;
            /// <summary>The days the record covers.</summary>
            public float days;
            public Dictionary<ThingDef, float> used = new Dictionary<ThingDef, float>();
            /// <summary>By bench id: what it made a day.</summary>
            public Dictionary<int, Dictionary<ThingDef, float>> made = new Dictionary<int, Dictionary<ThingDef, float>>();
            /// <summary>By bench id: the batches each worker (by pawn id) did there lately.</summary>
            public Dictionary<int, Dictionary<int, float>> doers = new Dictionary<int, Dictionary<int, float>>();
        }

        private const string UsedKey = "u", MadeKey = "p", DoneKey = "w";

        public static ColonyUse Instance => CouncilGameComponent.Instance?.use;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Whether anything is being counted: only through the deeper game hooks.</summary>
        public static bool Recording => RimfolkPatches.On && Instance != null;

        /// <summary>So many of a thing used up at home.</summary>
        public static void Used(Map map, ThingDef def, float count)
        {
            var me = Instance;
            if (me == null || map == null || !map.IsPlayerHome || def == null || count <= 0f) return;
            me.log.Add(Now, UseLog.Key(UsedKey, map.uniqueID, def.defName), count);
        }

        /// <summary>A bill's batch done: its ingredients used, its products made at the bench, the batch the worker's there.</summary>
        public static void BillDone(Bill_Production bill, Pawn doer, List<Thing> ingredients)
        {
            var me = Instance;
            if (me == null || bill?.recipe == null) return;
            var bench = bill.billStack?.billGiver as Building_WorkTable;
            var map = bench?.Map ?? doer?.MapHeld;
            if (map == null || !map.IsPlayerHome) return;
            if (ingredients != null)
                foreach (var t in ingredients)
                    if (t?.def != null && !(t is Corpse)) Used(map, t.def, t.stackCount);
            if (bench == null) return;
            int now = Now;
            if (doer != null) me.log.Add(now, UseLog.Key(DoneKey, bench.thingIDNumber, doer.thingIDNumber), 1f);
            if (bill.recipe.products != null)
                foreach (var p in bill.recipe.products)
                    if (p?.thingDef != null && p.count > 0) me.log.Add(now, UseLog.Key(MadeKey, bench.thingIDNumber, p.thingDef.defName), p.count);
        }

        /// <summary>The record for one map, with its rates a day; null without a game.</summary>
        public static Read ReadFor(Map map)
        {
            var me = Instance;
            if (me == null || map == null) return null;
            int now = Now;
            var r = new Read { days = me.log.Covered(now) };
            r.measured = RimfolkPatches.On && me.log.Enough(now);
            float days = Math.Max(r.days, 0.01f);
            string mapId = map.uniqueID.ToString();
            foreach (var kv in me.log.Totals(now))
            {
                var parts = UseLog.Parts(kv.Key);
                if (parts.Length != 3) continue;
                switch (parts[0])
                {
                    case UsedKey:
                    {
                        if (parts[1] != mapId) continue;
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(parts[2]);
                        if (def != null) r.used[def] = kv.Value / days;
                        break;
                    }
                    case MadeKey:
                    {
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(parts[2]);
                        if (def == null || !int.TryParse(parts[1], out int bench)) continue;
                        if (!r.made.TryGetValue(bench, out var m)) r.made[bench] = m = new Dictionary<ThingDef, float>();
                        m[def] = kv.Value / days;
                        break;
                    }
                    case DoneKey:
                    {
                        if (!int.TryParse(parts[1], out int bench) || !int.TryParse(parts[2], out int pawn)) continue;
                        if (!r.doers.TryGetValue(bench, out var d)) r.doers[bench] = d = new Dictionary<int, float>();
                        d[pawn] = kv.Value;
                        break;
                    }
                }
            }
            return r;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) log.Prune(Now);
            Scribe_Collections.Look(ref log.days, "days", LookMode.Value);
            Scribe_Collections.Look(ref log.keys, "keys", LookMode.Value);
            Scribe_Collections.Look(ref log.amounts, "amounts", LookMode.Value);
            Scribe_Values.Look(ref log.since, "since", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) log.Repair();
        }
    }

    // The game's events for the record of use (ColonyUse). Each class is its own small hook: one that can't be applied
    // is skipped alone (RimfolkPatches), and the "deeper game hooks" setting switches them off. They only read.

    /// <summary>Something eaten at home by one of the colony's people, its prisoners or its animals: so many of it used.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_UseEaten
    {
        public static void Prefix(Thing __instance, out int __state)
        {
            __state = __instance?.stackCount ?? 0;
        }

        public static void Postfix(Thing __instance, Pawn ingester, int __state)
        {
            // Items only: a plant grazed or a corpse fed on isn't the colony's stores.
            if (!RimfolkPatches.On || ingester == null || __instance?.def == null || __instance.def.category != ThingCategory.Item || __instance is Corpse) return;
            if (!(ingester.Faction != null && ingester.Faction == Faction.OfPlayerSilentFail) && !ingester.IsPrisonerOfColony) return;
            try
            {
                int taken = __instance.Destroyed ? __state : __state - __instance.stackCount;
                if (taken > 0) ColonyUse.Used(ingester.MapHeld, __instance.def, taken);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("use: eaten", e);
            }
        }
    }

    /// <summary>A colony doctor's tending with medicine: one used.</summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_UseTended
    {
        public static void Prefix(Pawn doctor, Pawn patient, Medicine medicine)
        {
            if (!RimfolkPatches.On || medicine?.def == null) return;
            try
            {
                ColonyUse.Used(patient?.MapHeld ?? doctor?.MapHeld, medicine.def, 1f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("use: medicine", e);
            }
        }
    }

    /// <summary>A bill's batch done at a bench: what it took, what it gave, and who did it.</summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class Patch_UseBillDone
    {
        public static void Postfix(Bill_Production __instance, Pawn billDoer, List<Thing> ingredients)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                ColonyUse.BillDone(__instance, billDoer, ingredients);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("use: bill done", e);
            }
        }
    }

    /// <summary>Fuel put into a stove, a generator or a torch by hand: what of it went in.</summary>
    [HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.Refuel), new[] { typeof(List<Thing>) })]
    public static class Patch_UseFuel
    {
        public static void Prefix(List<Thing> fuelThings, out List<(Thing thing, int count)> __state)
        {
            __state = null;
            if (!RimfolkPatches.On || fuelThings == null) return;
            __state = new List<(Thing, int)>(fuelThings.Count);
            foreach (var t in fuelThings)
                if (t?.def != null) __state.Add((t, t.stackCount));
        }

        public static void Postfix(CompRefuelable __instance, List<(Thing thing, int count)> __state)
        {
            if (__state == null || __state.Count == 0) return;
            try
            {
                var map = __instance?.parent?.MapHeld;
                foreach (var (thing, count) in __state)
                {
                    int used = thing.Destroyed ? count : count - thing.stackCount;
                    if (used > 0) ColonyUse.Used(map, thing.def, used);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("use: fuel", e);
            }
        }
    }

    /// <summary>A colonist's clothes worn through: one piece used up.</summary>
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "TakeWearoutDamageForDay")]
    public static class Patch_UseWornOut
    {
        public static void Postfix(Pawn_ApparelTracker __instance, Thing ap)
        {
            if (!RimfolkPatches.On || ap?.def == null || !ap.Destroyed) return;
            try
            {
                var p = __instance?.pawn;
                if (p?.Faction == null || p.Faction != Faction.OfPlayerSilentFail) return;
                ColonyUse.Used(p.MapHeld, ap.def, 1f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("use: worn out", e);
            }
        }
    }
}
