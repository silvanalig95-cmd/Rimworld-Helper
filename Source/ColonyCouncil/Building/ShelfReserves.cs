using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// One shelf's reserved kinds: "def:Name" for an item, "cat:Name" for any of a group (meals, drugs...), "best:Name"
    /// for the best of a group the colony has at hand (a lavish meal before a fine one, glitterworld medicine first). Each
    /// entry is one slot: a kind named twice keeps two.
    /// </summary>
    public class ShelfReserve : IExposable
    {
        public Building_Storage shelf;
        public List<string> kinds = new List<string>();

        public void ExposeData()
        {
            Scribe_References.Look(ref shelf, "shelf");
            Scribe_Collections.Look(ref kinds, "kinds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) kinds ??= new List<string>();
        }
    }

    /// <summary>
    /// Reserved slots on the colony's shelves (the setting "Reserved slots on shelves"): a shelf keeps one slot for each
    /// kind picked with its "Reserved slots" button (an item, or any of a group like meals), as long as the colony has
    /// some of it on the map; the other slots take whatever comes. Haulers put a new stack of anything else on it only
    /// while a slot stays free for every reserved kind still missing (ShelfReserveMath.MayTakeSlot), and when one is
    /// missing and the shelf is full, a stack of what it holds most of is moved out to make room (MoveOut): it counts as
    /// not belonging there, so a hauler takes it to the next place that will have it. Looked at again at most every two
    /// seconds, and at once when what the shelf holds changes. Works the same with LWM's Deep Storage, whose shelves
    /// count their stacks through the game's own number per cell.
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

        public List<string> KindsOf(Building_Storage shelf) => reserves.FirstOrDefault(r => r.shelf == shelf)?.kinds ?? new List<string>();

        public void Set(Building_Storage shelf, List<string> kinds)
        {
            if (shelf == null) return;
            reserves.RemoveAll(r => r.shelf == shelf);
            if (kinds != null && kinds.Count > 0) reserves.Add(new ShelfReserve { shelf = shelf, kinds = kinds.ToList() });
            states.Remove(shelf);
            RebuildAll();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) reserves.RemoveAll(r => r?.shelf == null || r.shelf.Destroyed || r.kinds.NullOrEmpty());
            Scribe_Collections.Look(ref reserves, "reserves", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                reserves ??= new List<ShelfReserve>();
                reserves.RemoveAll(r => r?.shelf == null || r.kinds.NullOrEmpty());
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
                    if (r?.shelf != null && !r.shelf.Destroyed && r.kinds.Count > 0) Reserved.Add(r.shelf);
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

            public IEnumerable<ThingDef> Defs =>
                def != null ? new[] { def } : best ? (IEnumerable<ThingDef>)targets ?? Enumerable.Empty<ThingDef>() : cat?.DescendantThingDefs ?? Enumerable.Empty<ThingDef>();

            public static Kind Parse(string raw)
            {
                if (raw.NullOrEmpty()) return null;
                if (raw.StartsWith("def:")) return new Kind { raw = raw, def = DefDatabase<ThingDef>.GetNamedSilentFail(raw.Substring(4)) };
                if (raw.StartsWith("cat:")) return new Kind { raw = raw, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(4)) };
                if (raw.StartsWith("best:")) return new Kind { raw = raw, best = true, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(5)) };
                return null;
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

        public class State
        {
            public int tick, held, capacity, waiting;
            /// <summary>One per reserved slot (a kind named twice is in here twice).</summary>
            public List<Kind> kinds = new List<Kind>();
            /// <summary>Per slot: filled by a stack on the shelf now; empty but some on the map (waiting for room).</summary>
            public bool[] here, onMap;
            public HashSet<Thing> moveOut = new HashSet<Thing>();

            public bool FillsMissing(ThingDef d)
            {
                for (int k = 0; k < kinds.Count; k++)
                    if (!here[k] && kinds[k].Matches(d)) return true;
                return false;
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
            st.kinds = KindsOf(shelf).Select(Kind.Parse).Where(k => k != null && (k.def != null || k.cat != null)).ToList();
            var things = group.HeldThings.ToList();
            st.held = things.Count;
            var onShelf = new HashSet<ThingDef>(things.Select(t => t.def));
            var bestOf = new Dictionary<string, HashSet<ThingDef>>();
            foreach (var k in st.kinds)
                if (k.best)
                {
                    if (!bestOf.TryGetValue(k.raw, out var targets)) bestOf[k.raw] = targets = BestAtHand(k.cat, shelf, onShelf);
                    k.targets = targets;
                }
            foreach (var c in shelf.AllSlotCellsList()) st.capacity += c.GetMaxItemsAllowedInCell(map);
            var stacks = new List<ShelfReserveMath.Stack>();
            foreach (var t in things)
            {
                var covers = new List<int>();
                for (int k = 0; k < st.kinds.Count; k++)
                    if (st.kinds[k].Matches(t.def)) covers.Add(k);
                stacks.Add(new ShelfReserveMath.Stack { key = t.def.defName, count = t.stackCount, covers = covers.ToArray() });
            }
            var missing = ShelfReserveMath.Missing(st.kinds.Count, stacks);
            st.here = new bool[st.kinds.Count];
            st.onMap = new bool[st.kinds.Count];
            for (int k = 0; k < st.kinds.Count; k++) st.here[k] = !missing.Contains(k);
            var onMapOf = new Dictionary<string, bool>();
            foreach (int k in missing)
            {
                var kind = st.kinds[k];
                if (!onMapOf.TryGetValue(kind.raw, out bool some)) onMapOf[kind.raw] = some = OnMap(kind, shelf);
                if (!some) continue;
                st.onMap[k] = true;
                st.waiting++;
            }
            foreach (int i in ShelfReserveMath.MoveOut(st.capacity, stacks, st.kinds.Count, st.waiting)) st.moveOut.Add(things[i]);
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

        /// <summary>A new stack of this thing may go into this cell of a shelf with reserved kinds (Patch_ShelfReserveCell).</summary>
        internal static bool MayStore(IntVec3 c, Map map, Thing thing)
        {
            var shelf = map.haulDestinationManager.SlotGroupAt(c)?.parent as Building_Storage;
            if (shelf == null || !Reserved.Contains(shelf) || !On) return true;
            // Already on the shelf: it stays, unless it's the stack moving out to make room (not to another cell of it).
            if (thing.Spawned && thing.Map == map && shelf.OccupiedRect().Contains(thing.Position))
                return !(Of(map)?.StateOf(shelf)?.moveOut.Contains(thing) ?? false);
            // Added to a stack already in the cell, it takes no slot.
            var here = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < here.Count; i++)
            {
                var t = here[i];
                if (t != thing && t.def.EverStorable(false) && t.CanStackWith(thing) && t.stackCount < t.def.stackLimit) return true;
            }
            var st = Of(map)?.StateOf(shelf);
            if (st == null || st.kinds.Count == 0) return true;
            // The game says this cell has room; if the count disagrees (another mod's rule), there's room for one.
            int capacity = Math.Max(st.capacity, st.held + 1);
            return ShelfReserveMath.MayTakeSlot(capacity, st.held, st.FillsMissing(thing.def), st.waiting);
        }

        /// <summary>A stack on the shelf that is to make room for a missing reserved kind no longer belongs there (Patch_ShelfReserveAccepts).</summary>
        internal static bool MovingOut(Building_Storage shelf, Thing t)
        {
            if (t == null || !Reserved.Contains(shelf) || !On) return false;
            var st = Of(shelf.Map)?.StateOf(shelf);
            return st != null && st.moveOut.Contains(t);
        }

        // ---------------- The button and the window ----------------

        public static Gizmo GizmoFor(Building_Storage shelf)
        {
            if (!On || shelf?.Map == null || shelf.Faction != Faction.OfPlayer || shelf.settings == null) return null;
            // A hopper or a one-stack box has nothing to share out.
            if (shelf.AllSlotCellsList().Count * shelf.MaxItemsInCell < 2) return null;
            var c = Of(shelf.Map);
            if (c == null) return null;
            return new Command_ShelfReserve(shelf, c.KindsOf(shelf).Count);
        }

        /// <summary>Each reserved kind of the shelf, how many slots, and where it stands, one line each.</summary>
        public static string Summary(Building_Storage shelf)
        {
            var st = Of(shelf?.Map)?.StateOf(shelf);
            if (st == null || st.kinds.Count == 0) return "CC_Shelf_None".Translate().Resolve();
            var lines = new List<string>();
            foreach (var raw in st.kinds.Select(k => k.raw).Distinct())
            {
                var kind = st.kinds.First(k => k.raw == raw);
                int n = st.kinds.Count(k => k.raw == raw);
                lines.Add("- " + kind.Label + (n > 1 ? " x" + n : "") + ": " + StatusOf(st, raw));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Where a reserved kind stands: on the shelf, coming, a stack moving out for it, or none on the map; with several slots, how many are filled.</summary>
        public static string StatusOf(State st, string raw)
        {
            var slots = new List<int>();
            for (int k = 0; k < st.kinds.Count; k++)
                if (st.kinds[k].raw == raw) slots.Add(k);
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
            var kind = st.kinds[slots[0]];
            if (kind.best && kind.targets != null && kind.targets.Count > 0)
                status = "CC_Shelf_BestNow".Translate(string.Join(", ", kind.targets.Select(d => d.label)).Named("KIND"), status.Named("STATUS")).Resolve();
            return status;
        }

        /// <summary>Every shelf given gets the same reserved kinds.</summary>
        public static void SetAll(List<Building_Storage> shelves, List<string> kinds)
        {
            foreach (var s in shelves)
                if (s != null && !s.Destroyed) Of(s.Map)?.Set(s, kinds.ToList());
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }
    }

    /// <summary>The button on a selected shelf: its reserved slots, in a window for every shelf selected.</summary>
    public class Command_ShelfReserve : Command_Action
    {
        private const int GroupKey = 0x5E1F_0A11;

        private readonly Building_Storage shelf;

        public Command_ShelfReserve(Building_Storage shelf, int count)
        {
            this.shelf = shelf;
            groupKey = GroupKey;
            icon = Icons.Pin;
            defaultLabel = (count > 0 ? "CC_Shelf_ButtonCount" : "CC_Shelf_Button").Translate(count).Resolve();
            defaultDesc = "CC_Shelf_ButtonDesc".Translate().Resolve() + "\n\n" + ShelfReserves.Summary(shelf);
            action = () => Open(new List<Building_Storage> { shelf });
        }

        public override void ProcessGroupInput(Event ev, List<Gizmo> group)
        {
            var shelves = new List<Building_Storage> { shelf };
            if (group != null)
                foreach (var g in group)
                    if (g is Command_ShelfReserve c && c.shelf != null && !shelves.Contains(c.shelf) && c.shelf.Map == shelf.Map)
                        shelves.Add(c.shelf);
            Open(shelves);
        }

        private static void Open(List<Building_Storage> shelves)
        {
            if (Find.WindowStack.IsOpen(typeof(Dialog_ShelfReserve))) return;
            Find.WindowStack.Add(new Dialog_ShelfReserve(shelves));
        }
    }

    /// <summary>
    /// The reserved kinds of a shelf (or of every shelf selected, set alike): each with where it stands, a button to take
    /// it off, and "Reserve a slot for..." (Dialog_ShelfReservePick).
    /// </summary>
    public class Dialog_ShelfReserve : Window
    {
        private readonly List<Building_Storage> shelves;
        private Vector2 scroll;
        private float height = 200f;

        public Dialog_ShelfReserve(List<Building_Storage> shelves)
        {
            this.shelves = shelves;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            forcePause = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(520f, UI.screenWidth - 40f), Mathf.Min(560f, UI.screenHeight - 40f));

        private Building_Storage First => shelves.FirstOrDefault(s => s != null && !s.Destroyed && s.Map != null);

        public override void DoWindowContents(Rect inRect)
        {
            var first = First;
            var comp = ShelfReserves.Of(first?.Map);
            if (first == null || comp == null)
            {
                Close();
                return;
            }
            string sub = shelves.Count > 1 ? "CC_Shelf_SubMany".Translate(shelves.Count).Resolve() : first.LabelCap;
            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Pin, "CC_Shelf_Title".Translate(), sub);
            float y = inRect.y + top;
            y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_Shelf_Explain".Translate(), UiKit.Faint) + 8f;
            if (!ShelfReserves.On)
                y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_Shelf_Off".Translate(), UiKit.Warn) + 6f;

            var kinds = comp.KindsOf(first).ToList();
            var st = comp.StateOf(first);
            int capacity = st?.capacity ?? 0;
            var distinct = kinds.Distinct().ToList();
            const float rowH = 30f, bottom = 44f;
            if (capacity > 0)
            {
                UiKit.LabelFit(new Rect(inRect.x, y, inRect.width, 22f), "CC_Shelf_SlotsUsed".Translate(kinds.Count, capacity),
                    kinds.Count > capacity ? UiKit.Warn : UiKit.Faint, GameFont.Tiny);
                y += 24f;
            }
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - bottom - 42f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float ry = 0f;
            List<string> next = null;
            for (int i = 0; i < distinct.Count; i++)
            {
                string raw = distinct[i];
                int n = kinds.Count(s => s == raw);
                var kind = ShelfReserves.Kind.Parse(raw);
                var row = new Rect(0f, ry, view.width, rowH);
                UiKit.Zebra(row, i);
                var icon = kind?.def?.uiIcon ?? kind?.cat?.icon;
                if (icon != null) GUI.DrawTexture(new Rect(4f, ry + 3f, 24f, 24f), icon);
                string label = kind?.Label ?? raw;
                bool allHere = st != null && Enumerable.Range(0, st.kinds.Count).Where(k => st.kinds[k].raw == raw).All(k => st.here[k]);
                string status = st != null ? ShelfReserves.StatusOf(st, raw) : "CC_Shelf_Unknown".Translate().Resolve();
                UiKit.LabelFit(new Rect(34f, ry, view.width * 0.4f - 34f, rowH), label, Color.white);
                UiKit.LabelFit(new Rect(view.width * 0.4f, ry, view.width * 0.6f - 118f, rowH), status, allHere ? UiKit.Good : UiKit.Faint, GameFont.Tiny);
                // How many slots: fewer, the number, more.
                var less = new Rect(view.width - 112f, ry + 4f, 22f, 22f);
                var count = new Rect(view.width - 88f, ry, 30f, rowH);
                var more = new Rect(view.width - 56f, ry + 4f, 22f, 22f);
                if (Widgets.ButtonText(less, "-", true, false, n > 1) && n > 1)
                {
                    next = kinds.ToList();
                    next.RemoveAt(next.LastIndexOf(raw));
                }
                UiKit.LabelFit(count, "x" + n, Color.white, GameFont.Small, TextAnchor.MiddleCenter);
                bool room = capacity <= 0 || kinds.Count < capacity;
                if (Widgets.ButtonText(more, "+", true, false, room) && room)
                {
                    next = kinds.ToList();
                    next.Insert(next.LastIndexOf(raw) + 1, raw);
                }
                TooltipHandler.TipRegion(less, "CC_Shelf_LessTip".Translate());
                TooltipHandler.TipRegion(more, room ? "CC_Shelf_MoreTip".Translate() : "CC_Shelf_FullTip".Translate());
                var x = new Rect(view.width - 28f, ry + 4f, 22f, 22f);
                TooltipHandler.TipRegion(x, "CC_Shelf_RemoveTip".Translate());
                if (Widgets.ButtonImage(x, TexButton.Delete)) next = kinds.Where(s => s != raw).ToList();
                ry += rowH;
            }
            if (distinct.Count == 0)
            {
                UiKit.LabelFit(new Rect(4f, ry, view.width - 8f, rowH), "CC_Shelf_None".Translate(), UiKit.Faint);
                ry += rowH;
            }
            height = Mathf.Max(ry, 40f);
            Widgets.EndScrollView();
            if (next != null) SetAll(next);

            var add = new Rect(inRect.x, inRect.yMax - bottom - 36f, 260f, 32f);
            if (Widgets.ButtonText(add, UiKit.Fit("CC_Shelf_Add".Translate(), add.width - 12f)) && !Find.WindowStack.IsOpen(typeof(Dialog_ShelfReservePick)))
                Find.WindowStack.Add(new Dialog_ShelfReservePick(shelves));
            // One slot for each kind on the shelf now that has none yet.
            var keep = new Rect(add.xMax + 8f, add.y, Mathf.Min(220f, inRect.xMax - add.xMax - 8f), 32f);
            var onShelf = first.GetSlotGroup()?.HeldThings.Select(th => "def:" + th.def.defName).Distinct().Where(raw => !kinds.Contains(raw)).ToList() ?? new List<string>();
            int roomLeft = capacity > 0 ? capacity - kinds.Count : onShelf.Count;
            TooltipHandler.TipRegion(keep, "CC_Shelf_KeepTip".Translate());
            if (keep.width > 60f && Widgets.ButtonText(keep, UiKit.Fit("CC_Shelf_Keep".Translate(), keep.width - 12f), true, true, onShelf.Count > 0 && roomLeft > 0)
                && onShelf.Count > 0 && roomLeft > 0)
                SetAll(kinds.Concat(onShelf.Take(roomLeft)).ToList());
        }

        private void SetAll(List<string> kinds) => ShelfReserves.SetAll(shelves, kinds);
    }

    /// <summary>A shelf with reserved kinds keeps a slot for each (ShelfReserves.MayStore): checked wherever the game asks if a cell can take a thing.</summary>
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

    /// <summary>A stack moving out to make room for a reserved kind no longer belongs on the shelf, so a hauler takes it on.</summary>
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
