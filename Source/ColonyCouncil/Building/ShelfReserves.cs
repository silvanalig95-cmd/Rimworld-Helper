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
    /// <summary>One shelf's reserved kinds: "def:Name" for an item, "cat:Name" for any of a group (meals, drugs...).</summary>
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
            if (kinds != null && kinds.Count > 0) reserves.Add(new ShelfReserve { shelf = shelf, kinds = kinds.Distinct().ToList() });
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

            public bool Matches(ThingDef d) => d != null && (def != null ? d == def : cat != null && d.IsWithinCategory(cat));

            public string Label => def != null ? def.LabelCap.Resolve() : cat != null ? "CC_Shelf_AnyOf".Translate(cat.label.Named("GROUP")).Resolve() : raw;

            public IEnumerable<ThingDef> Defs => def != null ? new[] { def } : cat?.DescendantThingDefs ?? Enumerable.Empty<ThingDef>();

            public static Kind Parse(string raw)
            {
                if (raw.NullOrEmpty()) return null;
                if (raw.StartsWith("def:")) return new Kind { raw = raw, def = DefDatabase<ThingDef>.GetNamedSilentFail(raw.Substring(4)) };
                if (raw.StartsWith("cat:")) return new Kind { raw = raw, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(4)) };
                return null;
            }
        }

        public class State
        {
            public int tick, held, capacity, waiting;
            public List<Kind> kinds = new List<Kind>();
            /// <summary>Per kind: on the shelf now; missing but on the map (waiting for a slot).</summary>
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
            foreach (int k in missing)
                if (OnMap(st.kinds[k], shelf))
                {
                    st.onMap[k] = true;
                    st.waiting++;
                }
            foreach (int i in ShelfReserveMath.MoveOut(st.capacity, stacks, st.kinds.Count, st.waiting)) st.moveOut.Add(things[i]);
            return st;
        }

        /// <summary>Some of this kind the colony could put here: on the map, not forbidden, kept in storage or in the home area, and allowed on this shelf.</summary>
        private bool OnMap(Kind kind, Building_Storage shelf)
        {
            var rect = shelf.OccupiedRect();
            var home = map.areaManager.Home;
            foreach (var d in kind.Defs)
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

        /// <summary>Each reserved kind of the shelf, and where it stands, one line each.</summary>
        public static string Summary(Building_Storage shelf)
        {
            var st = Of(shelf?.Map)?.StateOf(shelf);
            if (st == null || st.kinds.Count == 0) return "CC_Shelf_None".Translate().Resolve();
            var lines = new List<string>();
            for (int k = 0; k < st.kinds.Count; k++) lines.Add("- " + st.kinds[k].Label + ": " + StatusOf(st, k));
            return string.Join("\n", lines);
        }

        public static string StatusOf(State st, int k)
        {
            if (st.here[k]) return "CC_Shelf_Here".Translate().Resolve();
            if (!st.onMap[k]) return "CC_Shelf_NoneOnMap".Translate().Resolve();
            return (st.moveOut.Count > 0 ? "CC_Shelf_MakingRoom" : "CC_Shelf_Coming").Translate().Resolve();
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
    /// it off, and "Reserve a slot for..." with what the colony has on this map that the shelf may hold.
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
            const float rowH = 30f, bottom = 44f;
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - bottom - 42f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float ry = 0f;
            string remove = null;
            for (int i = 0; i < kinds.Count; i++)
            {
                var kind = ShelfReserves.Kind.Parse(kinds[i]);
                var row = new Rect(0f, ry, view.width, rowH);
                UiKit.Zebra(row, i);
                var icon = kind?.def?.uiIcon ?? kind?.cat?.icon;
                if (icon != null) GUI.DrawTexture(new Rect(4f, ry + 3f, 24f, 24f), icon);
                string label = kind?.Label ?? kinds[i];
                int k = st?.kinds.FindIndex(x => x.raw == kinds[i]) ?? -1;
                string status = k >= 0 ? ShelfReserves.StatusOf(st, k) : "CC_Shelf_Unknown".Translate().Resolve();
                UiKit.LabelFit(new Rect(34f, ry, view.width * 0.45f - 34f, rowH), label, Color.white);
                UiKit.LabelFit(new Rect(view.width * 0.45f, ry, view.width * 0.55f - 34f, rowH), status, k >= 0 && st.here[k] ? UiKit.Good : UiKit.Faint, GameFont.Tiny);
                var x = new Rect(view.width - 28f, ry + 4f, 22f, 22f);
                TooltipHandler.TipRegion(x, "CC_Shelf_RemoveTip".Translate());
                if (Widgets.ButtonImage(x, TexButton.Delete)) remove = kinds[i];
                ry += rowH;
            }
            if (kinds.Count == 0)
            {
                UiKit.LabelFit(new Rect(4f, ry, view.width - 8f, rowH), "CC_Shelf_None".Translate(), UiKit.Faint);
                ry += rowH;
            }
            height = Mathf.Max(ry, 40f);
            Widgets.EndScrollView();
            if (remove != null) SetAll(kinds.Where(s => s != remove).ToList());

            var add = new Rect(inRect.x, inRect.yMax - bottom - 36f, 260f, 32f);
            if (Widgets.ButtonText(add, UiKit.Fit("CC_Shelf_Add".Translate(), add.width - 12f)))
            {
                var options = Candidates(first, kinds);
                if (options.Count == 0) options.Add(new FloatMenuOption("CC_Shelf_NothingToAdd".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void SetAll(List<string> kinds)
        {
            foreach (var s in shelves)
                if (s != null && !s.Destroyed) ShelfReserves.Of(s.Map)?.Set(s, kinds.ToList());
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        /// <summary>What the colony has on this map that every selected shelf may hold: each item, and each group with two or more of them.</summary>
        private List<FloatMenuOption> Candidates(Building_Storage first, List<string> kinds)
        {
            var map = first.Map;
            var defs = new HashSet<ThingDef>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (t?.def == null || defs.Contains(t.def) || !t.Spawned || t.Position.Fogged(map) || t is Corpse || t is MinifiedThing) continue;
                if (shelves.All(s => s?.settings != null && s.settings.AllowedToAccept(t))) defs.Add(t.def);
            }
            var options = new List<FloatMenuOption>();
            var cats = new Dictionary<ThingCategoryDef, int>();
            foreach (var d in defs)
            {
                var cat = d.FirstThingCategory;
                if (cat != null) cats[cat] = cats.TryGetValue(cat, out int n) ? n + 1 : 1;
            }
            foreach (var kv in cats.Where(kv => kv.Value >= 2).OrderBy(kv => kv.Key.label))
            {
                string raw = "cat:" + kv.Key.defName;
                if (kinds.Contains(raw)) continue;
                var cat = kv.Key;
                options.Add(new FloatMenuOption("CC_Shelf_AnyOf".Translate(cat.label.Named("GROUP")).Resolve().CapitalizeFirst(), () => SetAll(kinds.Concat(new[] { raw }).ToList()),
                    cat.icon ?? BaseContent.ClearTex, Color.white));
            }
            foreach (var d in defs.OrderBy(d => d.label))
            {
                string raw = "def:" + d.defName;
                if (kinds.Contains(raw)) continue;
                var def = d;
                options.Add(new FloatMenuOption(def.LabelCap, () => SetAll(kinds.Concat(new[] { raw }).ToList()), def));
            }
            return options;
        }
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
