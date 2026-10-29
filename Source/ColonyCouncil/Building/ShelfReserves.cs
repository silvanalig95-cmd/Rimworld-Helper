using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// One kind a shelf keeps slots for: <see cref="kind"/> is "def:Name" for an item, "cat:Name" for any of a group
    /// (meals, drugs...), "best:Name" for the best of a group the colony has at hand (a lavish meal before a fine one,
    /// glitterworld medicine first). <see cref="slots"/> slots are kept for it; with <see cref="only"/> it takes no more
    /// than those; <see cref="perStack"/> (0: a full stack) is the most of it in one stack on this shelf; and while
    /// <see cref="when"/> doesn't hold ("patient": someone lies ill or hurt in the shelf's room), it isn't kept there at all.
    /// </summary>
    public class ShelfKind : IExposable
    {
        public const string WhenPatient = "patient";

        public string kind;
        public int slots = 1;
        public bool only;
        public int perStack;
        public string when = "";

        public ShelfKind Copy() => (ShelfKind)MemberwiseClone();

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref slots, "slots", 1);
            Scribe_Values.Look(ref only, "only");
            Scribe_Values.Look(ref perStack, "perStack");
            Scribe_Values.Look(ref when, "when", "");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                slots = Math.Max(1, slots);
                perStack = Math.Max(0, perStack);
                when ??= "";
            }
        }
    }

    /// <summary>One shelf's reserved kinds. Saves from 0.23.0 and 0.23.1 kept a name per slot ("kinds"); they load as entries.</summary>
    public class ShelfReserve : IExposable
    {
        public Building_Storage shelf;
        public List<ShelfKind> entries = new List<ShelfKind>();

        public void ExposeData()
        {
            Scribe_References.Look(ref shelf, "shelf");
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<string> kinds = null;
                Scribe_Collections.Look(ref kinds, "kinds", LookMode.Value);
                if (kinds != null && entries.NullOrEmpty())
                    entries = kinds.Where(k => !k.NullOrEmpty()).GroupBy(k => k).Select(g => new ShelfKind { kind = g.Key, slots = g.Count() }).ToList();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                entries ??= new List<ShelfKind>();
                entries.RemoveAll(e => e == null || e.kind.NullOrEmpty());
            }
        }
    }

    /// <summary>
    /// Reserved slots on the colony's shelves (the setting "Reserved slots on shelves"): a shelf keeps the slots set with
    /// its "Reserved slots" button (ShelfKind: an item, any of a group, or the best of a group at hand), each for a stack
    /// of its own, as long as the colony has some of that kind on the map; the other slots take whatever comes. Haulers
    /// put a new stack of anything else on it only while a slot stays free for every reserved one still empty
    /// (ShelfReserveMath.MayTakeSlot); a kind kept to its slots takes no more; a kind held to a number per stack is
    /// topped up to that and no further (the hauler carries no more than the cell has room for); and a kind whose
    /// condition doesn't hold is kept off the shelf. What must go (to make room, past a cap, or kept off for now) counts
    /// as not belonging there, so a hauler takes it to the next place that will have it (MoveOut). Looked at again at
    /// most every two seconds, and at once when what the shelf holds changes. Works the same with LWM's Deep Storage,
    /// whose shelves count their stacks through the game's own number per cell.
    /// </summary>
    public class ShelfReserves : MapComponent
    {
        private List<ShelfReserve> reserves = new List<ShelfReserve>();

        /// <summary>Every shelf with a reserved kind, on any map: the game's hooks look here first, so they cost nothing otherwise.</summary>
        internal static readonly HashSet<Building_Storage> Reserved = new HashSet<Building_Storage>();

        private readonly Dictionary<Building_Storage, State> states = new Dictionary<Building_Storage, State>();

        public ShelfReserves(Map map) : base(map)
        {
        }

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.shelfReserves ?? true);

        public static ShelfReserves Of(Map map) => map?.GetComponent<ShelfReserves>();

        /// <summary>The shelf's entries (a copy to change and give back to <see cref="Set"/>).</summary>
        public List<ShelfKind> EntriesOf(Building_Storage shelf) =>
            reserves.FirstOrDefault(r => r.shelf == shelf)?.entries.Select(e => e.Copy()).ToList() ?? new List<ShelfKind>();

        public void Set(Building_Storage shelf, List<ShelfKind> entries)
        {
            if (shelf == null) return;
            reserves.RemoveAll(r => r.shelf == shelf);
            var list = entries?.Where(e => e != null && !e.kind.NullOrEmpty()).Select(e => e.Copy()).ToList();
            if (list != null && list.Count > 0) reserves.Add(new ShelfReserve { shelf = shelf, entries = list });
            states.Remove(shelf);
            RebuildAll();
        }

        /// <summary>Every shelf given gets the same entries.</summary>
        public static void SetAll(List<Building_Storage> shelves, List<ShelfKind> entries)
        {
            foreach (var s in shelves)
                if (s != null && !s.Destroyed) Of(s.Map)?.Set(s, entries);
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) reserves.RemoveAll(r => r?.shelf == null || r.shelf.Destroyed || r.entries.NullOrEmpty());
            Scribe_Collections.Look(ref reserves, "reserves", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                reserves ??= new List<ShelfReserve>();
                reserves.RemoveAll(r => r?.shelf == null || r.entries.NullOrEmpty());
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildAll();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            reserves.Clear();
            RebuildAll();
        }

        private static void RebuildAll()
        {
            Reserved.Clear();
            if (Current.Game?.Maps == null) return;
            foreach (var m in Current.Game.Maps)
            {
                var c = Of(m);
                if (c == null) continue;
                foreach (var r in c.reserves)
                    if (r?.shelf != null && !r.shelf.Destroyed && r.entries.Count > 0) Reserved.Add(r.shelf);
            }
        }

        // ---------------- What a shelf holds, and what it is waiting for ----------------

        /// <summary>A reserved kind, read from its name.</summary>
        public class Kind
        {
            public string raw;
            public ThingDef def;
            public ThingCategoryDef cat;
            /// <summary>The best of the group at hand; <see cref="targets"/> are those kinds, worked out when the shelf is looked at.</summary>
            public bool best;
            public HashSet<ThingDef> targets;

            public bool Matches(ThingDef d)
            {
                if (d == null) return false;
                if (def != null) return d == def;
                if (best) return targets != null && targets.Contains(d);
                return cat != null && d.IsWithinCategory(cat);
            }

            public string Label =>
                def != null ? def.LabelCap.Resolve()
                : cat == null ? raw
                : (best ? "CC_Shelf_BestOf" : "CC_Shelf_AnyOf").Translate(cat.label.Named("GROUP")).Resolve().CapitalizeFirst();

            public Texture2D Icon => def?.uiIcon ?? cat?.icon;

            public IEnumerable<ThingDef> Defs =>
                def != null ? new[] { def } : best ? (IEnumerable<ThingDef>)targets ?? Enumerable.Empty<ThingDef>() : cat?.DescendantThingDefs ?? Enumerable.Empty<ThingDef>();

            /// <summary>The kind, or null for a name that isn't one (or names a def or group no longer loaded).</summary>
            public static Kind Parse(string raw)
            {
                if (raw.NullOrEmpty()) return null;
                Kind k = null;
                if (raw.StartsWith("def:")) k = new Kind { raw = raw, def = DefDatabase<ThingDef>.GetNamedSilentFail(raw.Substring(4)) };
                else if (raw.StartsWith("cat:")) k = new Kind { raw = raw, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(4)) };
                else if (raw.StartsWith("best:")) k = new Kind { raw = raw, best = true, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(5)) };
                return k != null && (k.def != null || k.cat != null) ? k : null;
            }
        }

        // ---------------- The best at hand ----------------

        /// <summary>Food (not a drug) and medicine can be ranked: a meal by how much it's liked, then its worth; medicine by its potency.</summary>
        public static bool Rankable(ThingDef d) =>
            d != null && (d.IsMedicine || d.ingestible != null && !d.IsDrug && d.ingestible.preferability >= FoodPreferability.RawBad);

        public static float RankOf(ThingDef d)
        {
            if (d == null) return float.MinValue;
            if (d.IsMedicine) return d.GetStatValueAbstract(StatDefOf.MedicalPotency);
            return (int)d.ingestible.preferability * 100000f + Mathf.Min(d.BaseMarketValue, 99999f);
        }

        /// <summary>A group whose kinds the shelf may hold are all food, or all medicine, and two or more: "the best at hand" makes sense for it.</summary>
        public static bool RankableGroup(List<ThingDef> allowed)
        {
            if (allowed == null || allowed.Count < 2 || !allowed.All(Rankable)) return false;
            return allowed.All(d => d.IsMedicine) || allowed.All(d => !d.IsMedicine);
        }

        /// <summary>The kinds of the group the shelf may hold that rank highest among those on the shelf or on the map.</summary>
        private HashSet<ThingDef> BestAtHand(ThingCategoryDef cat, Building_Storage shelf, HashSet<ThingDef> onShelf)
        {
            var defs = cat.DescendantThingDefs.Distinct().Where(d => Rankable(d) && shelf.settings.AllowedToAccept(d)).ToList();
            var ranks = defs.Select(RankOf).ToList();
            var present = defs.Select(d => onShelf.Contains(d) || OnMapDef(d, shelf)).ToList();
            return new HashSet<ThingDef>(ShelfReserveMath.Best(ranks, present).Select(i => defs[i]));
        }

        // ---------------- Conditions ----------------

        /// <summary>Someone lies ill or hurt in a bed in this room: a patient being treated or recovering (animals in their beds too).</summary>
        private bool PatientIn(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors) return false;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || !p.InBed()) continue;
                var bed = p.CurrentBed();
                if (bed == null || bed.GetRoom() != room) continue;
                if (p.Downed || HealthAIUtility.ShouldSeekMedicalRest(p) || p.health.HasHediffsNeedingTend()) return true;
            }
            return false;
        }

        public class State
        {
            public int tick, held, capacity, waiting;
            public List<ShelfKind> entries = new List<ShelfKind>();
            /// <summary>Per entry: its kind (null if it names nothing loaded), whether it counts now (its condition holds), and its stacks on the shelf.</summary>
            public Kind[] kindOf;
            public bool[] active;
            public int[] stacksOf;
            /// <summary>One per reserved slot of the entries that count now, and the entry each belongs to.</summary>
            public List<Kind> kinds = new List<Kind>();
            public List<int> slotEntry = new List<int>();
            /// <summary>Per slot: filled by a stack on the shelf now; empty but some on the map (waiting for room).</summary>
            public bool[] here, onMap;
            public HashSet<Thing> moveOut = new HashSet<Thing>();
            /// <summary>The shelf isn't in a room, so "while a patient lies in this room" never holds.</summary>
            public bool noRoom;

            public bool FillsMissing(ThingDef d)
            {
                for (int k = 0; k < kinds.Count; k++)
                    if (!here[k] && kinds[k].Matches(d)) return true;
                return false;
            }

            /// <summary>Of a kind kept off the shelf for now (its condition doesn't hold) and of no kind that counts.</summary>
            public bool Barred(ThingDef d)
            {
                bool off = false;
                for (int e = 0; e < entries.Count; e++)
                {
                    if (kindOf[e] == null || !kindOf[e].Matches(d)) continue;
                    if (active[e]) return false;
                    off = true;
                }
                return off;
            }

            /// <summary>Of a kind kept to its slots that has as many stacks on the shelf as it has slots.</summary>
            public bool CapReached(ThingDef d)
            {
                for (int e = 0; e < entries.Count; e++)
                    if (active[e] && entries[e].only && kindOf[e] != null && kindOf[e].Matches(d) && stacksOf[e] >= Math.Max(1, entries[e].slots)) return true;
                return false;
            }

            /// <summary>The most of it in one stack on this shelf (0: a full stack), the lowest of the kinds that count for it.</summary>
            public int LimitFor(ThingDef d)
            {
                int limit = 0;
                for (int e = 0; e < entries.Count; e++)
                {
                    int l = entries[e].perStack;
                    if (l <= 0 || !active[e] || kindOf[e] == null || !kindOf[e].Matches(d)) continue;
                    limit = limit == 0 ? l : Math.Min(limit, l);
                }
                return limit;
            }
        }

        private const int StateTicks = 120;

        public State StateOf(Building_Storage shelf)
        {
            if (shelf == null) return null;
            var group = shelf.GetSlotGroup();
            if (shelf.Destroyed || !shelf.Spawned || group == null) return null;
            int now = Find.TickManager.TicksGame;
            int held = group.HeldThingsCount;
            if (states.TryGetValue(shelf, out var st) && now - st.tick < StateTicks && st.held == held) return st;
            st = Look(shelf, group, now);
            states[shelf] = st;
            return st;
        }

        private State Look(Building_Storage shelf, SlotGroup group, int now)
        {
            var st = new State { tick = now };
            st.entries = reserves.FirstOrDefault(r => r.shelf == shelf)?.entries.ToList() ?? new List<ShelfKind>();
            int n = st.entries.Count;
            var things = group.HeldThings.ToList();
            st.held = things.Count;
            var onShelf = new HashSet<ThingDef>(things.Select(t => t.def));
            st.kindOf = new Kind[n];
            st.active = new bool[n];
            st.stacksOf = new int[n];
            var room = shelf.GetRoom();
            st.noRoom = room == null || room.PsychologicallyOutdoors;
            bool patient = st.entries.Any(e => e.when == ShelfKind.WhenPatient) && PatientIn(room);
            for (int e = 0; e < n; e++)
            {
                var kind = Kind.Parse(st.entries[e].kind);
                if (kind != null && kind.best) kind.targets = BestAtHand(kind.cat, shelf, onShelf);
                st.kindOf[e] = kind;
                string when = st.entries[e].when ?? "";
                st.active[e] = kind != null && (when.Length == 0 || when == ShelfKind.WhenPatient && patient);
                if (!st.active[e] || kind == null) continue;
                for (int j = 0; j < Math.Max(1, st.entries[e].slots); j++)
                {
                    st.kinds.Add(kind);
                    st.slotEntry.Add(e);
                }
            }
            foreach (var c in shelf.AllSlotCellsList()) st.capacity += c.GetMaxItemsAllowedInCell(map);
            var caps = new int[n];
            for (int e = 0; e < n; e++) caps[e] = st.active[e] && st.entries[e].only ? Math.Max(1, st.entries[e].slots) : -1;
            var stacks = new List<ShelfReserveMath.Stack>();
            foreach (var t in things)
            {
                var covers = new List<int>();
                for (int k = 0; k < st.kinds.Count; k++)
                    if (st.kinds[k].Matches(t.def)) covers.Add(k);
                var groups = new List<int>();
                for (int e = 0; e < n; e++)
                {
                    if (!st.active[e] || st.kindOf[e] == null || !st.kindOf[e].Matches(t.def)) continue;
                    st.stacksOf[e]++;
                    if (caps[e] >= 0) groups.Add(e);
                }
                stacks.Add(new ShelfReserveMath.Stack
                {
                    key = t.def.defName, count = t.stackCount, covers = covers.ToArray(), barred = st.Barred(t.def), groups = groups.ToArray()
                });
            }
            var missing = ShelfReserveMath.Missing(st.kinds.Count, stacks);
            st.here = new bool[st.kinds.Count];
            st.onMap = new bool[st.kinds.Count];
            for (int k = 0; k < st.kinds.Count; k++) st.here[k] = !missing.Contains(k);
            var onMapOf = new Dictionary<int, bool>();
            foreach (int k in missing)
            {
                int e = st.slotEntry[k];
                if (!onMapOf.TryGetValue(e, out bool some)) onMapOf[e] = some = OnMap(st.kinds[k], shelf);
                if (!some) continue;
                st.onMap[k] = true;
                st.waiting++;
            }
            foreach (int i in ShelfReserveMath.MoveOut(st.capacity, stacks, st.kinds.Count, st.waiting, caps)) st.moveOut.Add(things[i]);
            return st;
        }

        /// <summary>Some of this kind the colony could put here: on the map, not forbidden, kept in storage or in the home area, and allowed on this shelf.</summary>
        private bool OnMap(Kind kind, Building_Storage shelf)
        {
            foreach (var d in kind.Defs)
                if (OnMapDef(d, shelf)) return true;
            return false;
        }

        private bool OnMapDef(ThingDef d, Building_Storage shelf)
        {
            var rect = shelf.OccupiedRect();
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsOfDef(d))
            {
                if (!t.Spawned || rect.Contains(t.Position) || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
                if (!t.IsInAnyStorage() && (home == null || !home[t.Position])) continue;
                if (shelf.settings.AllowedToAccept(t)) return true;
            }
            return false;
        }

        // ---------------- The game's hooks ----------------

        private static State StateAt(IntVec3 c, Map map, out Building_Storage shelf)
        {
            shelf = map.haulDestinationManager.SlotGroupAt(c)?.parent as Building_Storage;
            if (shelf == null || !Reserved.Contains(shelf) || !On) return null;
            var st = Of(map)?.StateOf(shelf);
            return st == null || st.entries.Count == 0 ? null : st;
        }

        /// <summary>This thing may go into this cell of a shelf with reserved kinds (Patch_ShelfReserveCell).</summary>
        internal static bool MayStore(IntVec3 c, Map map, Thing thing)
        {
            var st = StateAt(c, map, out var shelf);
            if (st == null) return true;
            // Already on the shelf: it stays, unless it's a stack that must go (not to another cell of it), and two of its
            // stacks are put together (the game's merging) only while the one made stays within its number per stack.
            if (thing.Spawned && thing.Map == map && shelf.OccupiedRect().Contains(thing.Position))
            {
                if (st.moveOut.Contains(thing)) return false;
                int most = st.LimitFor(thing.def);
                if (most <= 0) return true;
                foreach (var t in map.thingGrid.ThingsListAt(c))
                    if (t != thing && t.CanStackWith(thing) && t.stackCount + thing.stackCount > most) return false;
                return true;
            }
            if (st.Barred(thing.def)) return false;
            // Added to a stack already in the cell, it takes no slot: up to its number per stack, if it has one.
            int limit = st.LimitFor(thing.def);
            var here = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < here.Count; i++)
            {
                var t = here[i];
                if (t == thing || !t.def.EverStorable(false) || !t.CanStackWith(thing)) continue;
                if (limit > 0 && t.stackCount >= limit) return false;
                if (t.stackCount < t.def.stackLimit) return true;
            }
            bool fills = st.FillsMissing(thing.def);
            if (!fills && st.CapReached(thing.def)) return false;
            // The game says this cell has room; if the count disagrees (another mod's rule), there's room for one.
            int capacity = Math.Max(st.capacity, st.held + 1);
            return ShelfReserveMath.MayTakeSlot(capacity, st.held, fills, st.waiting);
        }

        /// <summary>The most of this thing one haul brings to this cell (0: no limit), for a kind held to a number per stack (Patch_ShelfReserveHaulCount).</summary>
        internal static int MostInto(IntVec3 c, Map map, Thing thing)
        {
            var st = StateAt(c, map, out _);
            if (st == null) return 0;
            int limit = st.LimitFor(thing.def);
            if (limit <= 0) return 0;
            int existing = -1;
            foreach (var t in map.thingGrid.ThingsListAt(c))
                if (t != thing && t.CanStackWith(thing))
                {
                    existing = t.stackCount;
                    break;
                }
            return Math.Max(1, ShelfReserveMath.SpaceFor(limit, thing.def.stackLimit, existing));
        }

        /// <summary>A stack on the shelf that must go (to make room, past a cap, or kept off for now) no longer belongs there (Patch_ShelfReserveAccepts).</summary>
        internal static bool MovingOut(Building_Storage shelf, Thing t)
        {
            if (t == null || !Reserved.Contains(shelf) || !On) return false;
            var st = Of(shelf.Map)?.StateOf(shelf);
            return st != null && st.moveOut.Contains(t);
        }

        // ---------------- Words ----------------

        public static Gizmo GizmoFor(Building_Storage shelf)
        {
            if (!On || shelf?.Map == null || shelf.Faction != Faction.OfPlayer || shelf.settings == null) return null;
            // A hopper or a one-stack box has nothing to share out.
            if (shelf.AllSlotCellsList().Count * shelf.MaxItemsInCell < 2) return null;
            var c = Of(shelf.Map);
            if (c == null) return null;
            return new Command_ShelfReserve(shelf, c.EntriesOf(shelf).Sum(e => Math.Max(1, e.slots)));
        }

        /// <summary>Each reserved kind of the shelf, how it's kept, and where it stands, one line each.</summary>
        public static string Summary(Building_Storage shelf)
        {
            var st = Of(shelf?.Map)?.StateOf(shelf);
            if (st == null || st.entries.Count == 0) return "CC_Shelf_None".Translate().Resolve();
            var lines = new List<string>();
            for (int e = 0; e < st.entries.Count; e++)
            {
                string label = st.kindOf[e]?.Label ?? st.entries[e].kind;
                string how = HowKept(st.entries[e]);
                lines.Add("- " + label + (how.NullOrEmpty() ? "" : " (" + how + ")") + ": " + StatusOf(st, e));
            }
            return string.Join("\n", lines);
        }

        /// <summary>"2 slots, no more, 4 each, while a patient lies here", leaving out what is as usual.</summary>
        public static string HowKept(ShelfKind entry)
        {
            var parts = new List<string>();
            if (entry.slots > 1) parts.Add("CC_Shelf_HowSlots".Translate(entry.slots).Resolve());
            if (entry.only) parts.Add("CC_Shelf_HowOnly".Translate().Resolve());
            if (entry.perStack > 0) parts.Add("CC_Shelf_HowEach".Translate(entry.perStack).Resolve());
            if (entry.when == ShelfKind.WhenPatient) parts.Add("CC_Shelf_HowPatient".Translate().Resolve());
            return string.Join(", ", parts);
        }

        /// <summary>Where an entry stands: kept off for now, on the shelf, coming, a stack moving out for it, or none on the map; with several slots, how many are filled.</summary>
        public static string StatusOf(State st, int e)
        {
            if (e < 0 || e >= st.entries.Count || st.kindOf[e] == null) return "CC_Shelf_Unknown".Translate().Resolve();
            if (!st.active[e]) return (st.noRoom ? "CC_Shelf_WhenNoRoom" : "CC_Shelf_WhenOff").Translate().Resolve();
            var slots = new List<int>();
            for (int k = 0; k < st.kinds.Count; k++)
                if (st.slotEntry[k] == e) slots.Add(k);
            if (slots.Count == 0) return "CC_Shelf_Unknown".Translate().Resolve();
            int filled = slots.Count(k => st.here[k]);
            int open = slots.FirstOrDefault(k => !st.here[k]);
            string rest = filled == slots.Count ? "CC_Shelf_Here".Translate().Resolve()
                : !st.onMap[open] ? "CC_Shelf_NoneOnMap".Translate().Resolve()
                : (st.moveOut.Count > 0 ? "CC_Shelf_MakingRoom" : "CC_Shelf_Coming").Translate().Resolve();
            string status = slots.Count > 1 && filled < slots.Count
                ? "CC_Shelf_SomeHere".Translate(filled, slots.Count, rest.Named("REST")).Resolve()
                : rest;
            // The best at hand says which it is now.
            var kind = st.kindOf[e];
            if (kind.best && kind.targets != null && kind.targets.Count > 0)
                status = "CC_Shelf_BestNow".Translate(string.Join(", ", kind.targets.Select(d => d.label)).Named("KIND"), status.Named("STATUS")).Resolve();
            return status;
        }

        public static bool AllHere(State st, int e)
        {
            if (st == null || e < 0 || e >= st.entries.Count || !st.active[e]) return false;
            for (int k = 0; k < st.kinds.Count; k++)
                if (st.slotEntry[k] == e && !st.here[k]) return false;
            return true;
        }
    }

    /// <summary>A shelf with reserved kinds keeps to them (ShelfReserves.MayStore): checked wherever the game asks if a cell can take a thing.</summary>
    [HarmonyPatch(typeof(StoreUtility), "NoStorageBlockersIn")]
    public static class Patch_ShelfReserveCell
    {
        private static bool broken;

        public static void Postfix(IntVec3 c, Map map, Thing thing, ref bool __result)
        {
            if (!__result || broken || ShelfReserves.Reserved.Count == 0 || thing == null || map == null) return;
            try
            {
                if (!ShelfReserves.MayStore(c, map, thing)) __result = false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: a cell", e);
            }
        }
    }

    /// <summary>A haul to a shelf brings no more of a kind held to a number per stack than the cell has room for (ShelfReserves.MostInto).</summary>
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToCellStorageJob))]
    public static class Patch_ShelfReserveHaulCount
    {
        private static bool broken;

        public static void Postfix(Pawn p, Thing t, IntVec3 storeCell, ref Job __result)
        {
            if (__result == null || broken || ShelfReserves.Reserved.Count == 0 || t == null || p?.Map == null) return;
            try
            {
                int most = ShelfReserves.MostInto(storeCell, p.Map, t);
                if (most > 0 && __result.count > most) __result.count = most;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: how many to bring", e);
            }
        }
    }

    /// <summary>A stack that must go (to make room, past a cap, or kept off for now) no longer belongs on the shelf, so a hauler takes it on.</summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Accepts))]
    public static class Patch_ShelfReserveAccepts
    {
        private static bool broken;

        public static void Postfix(Building_Storage __instance, Thing t, ref bool __result)
        {
            if (!__result || broken || ShelfReserves.Reserved.Count == 0) return;
            try
            {
                if (ShelfReserves.MovingOut(__instance, t)) __result = false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: what belongs", e);
            }
        }
    }

    /// <summary>The "Reserved slots" button on the colony's shelves.</summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.GetGizmos))]
    public static class Patch_ShelfReserveGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Storage __instance)
        {
            foreach (var g in __result) yield return g;
            Gizmo c = null;
            try
            {
                c = ShelfReserves.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reserved shelf slots: the button", e);
            }
            if (c != null) yield return c;
        }
    }
}
