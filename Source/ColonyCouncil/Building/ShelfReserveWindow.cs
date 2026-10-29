using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
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
    /// The reserved kinds of a shelf (or of every shelf selected, set alike), two lines each: the kind and where it
    /// stands; then how many slots, "no more than that", the most per stack, and when it's kept. Below, "Reserve a slot
    /// for..." (Dialog_ShelfReservePick) and "Keep what's on it now".
    /// </summary>
    public class Dialog_ShelfReserve : Window
    {
        private readonly List<Building_Storage> shelves;
        private Vector2 scroll;
        private float height = 200f;

        /// <summary>The steps of "most per stack", below the kind's own stack size; past the last is a full stack (0).</summary>
        private static readonly int[] PerStackSteps = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 75, 100, 150, 200, 300, 500 };

        /// <summary>A full stack of the kind: its own, or the biggest in the group.</summary>
        private static int StackSize(ShelfReserves.Kind kind)
        {
            if (kind?.def != null) return Math.Max(1, kind.def.stackLimit);
            var defs = kind?.cat?.DescendantThingDefs;
            return defs != null && defs.Any() ? Math.Max(1, defs.Max(d => d.stackLimit)) : 75;
        }

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

        public override Vector2 InitialSize => new Vector2(Mathf.Min(640f, UI.screenWidth - 40f), Mathf.Min(620f, UI.screenHeight - 40f));

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
            y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_Shelf_Explain".Translate(), UiKit.Faint) + 6f;
            if (!ShelfReserves.On)
                y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_Shelf_Off".Translate(), UiKit.Warn) + 6f;

            var entries = comp.EntriesOf(first);
            var st = comp.StateOf(first);
            int capacity = st?.capacity ?? 0;
            int reserved = entries.Sum(e => Math.Max(1, e.slots));
            const float rowH = 56f, bottom = 44f;
            if (capacity > 0)
            {
                UiKit.LabelFit(new Rect(inRect.x, y, inRect.width, 22f), "CC_Shelf_SlotsUsed".Translate(reserved, capacity),
                    reserved > capacity ? UiKit.Warn : UiKit.Faint, GameFont.Tiny);
                y += 24f;
            }
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - bottom - 42f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float ry = 0f;
            bool changed = false;
            int remove = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var kind = ShelfReserves.Kind.Parse(entry.kind);
                var row = new Rect(0f, ry, view.width, rowH);
                UiKit.Zebra(row, i);
                // The kind and where it stands.
                var icon = kind?.Icon;
                if (icon != null) GUI.DrawTexture(new Rect(4f, ry + 4f, 24f, 24f), icon);
                UiKit.LabelFit(new Rect(34f, ry, view.width * 0.4f - 34f, 30f), kind?.Label ?? entry.kind, Color.white);
                string status = st != null ? ShelfReserves.StatusOf(st, i) : "CC_Shelf_Unknown".Translate().Resolve();
                UiKit.LabelFit(new Rect(view.width * 0.4f, ry, view.width * 0.6f - 34f, 30f), status, ShelfReserves.AllHere(st, i) ? UiKit.Good : UiKit.Faint, GameFont.Tiny);
                var x = new Rect(view.width - 28f, ry + 4f, 22f, 22f);
                TooltipHandler.TipRegion(x, "CC_Shelf_RemoveTip".Translate());
                if (Widgets.ButtonImage(x, TexButton.Delete)) remove = i;

                // How it's kept.
                float ly = ry + 30f, lx = 34f;
                // Slots: fewer, the number, more.
                UiKit.LabelFit(new Rect(lx, ly, 40f, 22f), "CC_Shelf_Slots".Translate(), UiKit.Faint, GameFont.Tiny);
                lx += 40f;
                bool room = capacity <= 0 || reserved < capacity;
                if (Step(new Rect(lx, ly, 20f, 20f), "-", entry.slots > 1, "CC_Shelf_LessTip".Translate()))
                {
                    entry.slots--;
                    changed = true;
                }
                UiKit.LabelFit(new Rect(lx + 22f, ly, 24f, 22f), entry.slots.ToString(), Color.white, GameFont.Small, TextAnchor.MiddleCenter);
                if (Step(new Rect(lx + 48f, ly, 20f, 20f), "+", room, room ? "CC_Shelf_MoreTip".Translate() : "CC_Shelf_FullTip".Translate()))
                {
                    entry.slots++;
                    changed = true;
                }
                lx += 80f;
                // No more than those.
                var only = new Rect(lx, ly - 1f, 118f, 24f);
                bool wasOnly = entry.only;
                Widgets.CheckboxLabeled(only, "CC_Shelf_Only".Translate(), ref entry.only, false, null, null, true);
                TooltipHandler.TipRegion(only, "CC_Shelf_OnlyTip".Translate());
                if (entry.only != wasOnly) changed = true;
                lx += 128f;
                // Most per stack.
                UiKit.LabelFit(new Rect(lx, ly, 62f, 22f), "CC_Shelf_PerStack".Translate(), UiKit.Faint, GameFont.Tiny);
                lx += 62f;
                int full = StackSize(kind);
                var steps = PerStackSteps.Where(v => v < full).ToArray();
                bool isFull = entry.perStack <= 0 || entry.perStack >= full;
                int at = isFull ? steps.Length : steps.Count(v => v < entry.perStack);
                if (Step(new Rect(lx, ly, 20f, 20f), "-", at > 0, "CC_Shelf_PerStackTip".Translate()))
                {
                    entry.perStack = steps[at - 1];
                    changed = true;
                }
                string each = isFull ? "CC_Shelf_FullStack".Translate().Resolve() : entry.perStack.ToString();
                UiKit.LabelFit(new Rect(lx + 22f, ly, 40f, 22f), each, Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
                if (Step(new Rect(lx + 64f, ly, 20f, 20f), "+", !isFull, "CC_Shelf_PerStackTip".Translate()))
                {
                    int nextAt = steps.Count(v => v <= entry.perStack);
                    entry.perStack = nextAt < steps.Length ? steps[nextAt] : 0;
                    changed = true;
                }
                lx += 94f;
                // When.
                var whenRect = new Rect(lx, ly - 1f, Mathf.Max(80f, view.width - lx - 4f), 22f);
                string whenLabel = (entry.when == ShelfKind.WhenPatient ? "CC_Shelf_WhenPatient" : "CC_Shelf_WhenAlways").Translate().Resolve();
                TooltipHandler.TipRegion(whenRect, "CC_Shelf_WhenTip".Translate());
                if (Widgets.ButtonText(whenRect, UiKit.Fit(whenLabel, whenRect.width - 10f)))
                {
                    var e = entry;
                    var options = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("CC_Shelf_WhenAlways".Translate(), () => SetWhen(e.kind, "")),
                        new FloatMenuOption("CC_Shelf_WhenPatient".Translate(), () => SetWhen(e.kind, ShelfKind.WhenPatient))
                    };
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                ry += rowH;
            }
            if (entries.Count == 0)
            {
                UiKit.LabelFit(new Rect(4f, ry, view.width - 8f, 30f), "CC_Shelf_None".Translate(), UiKit.Faint);
                ry += 30f;
            }
            height = Mathf.Max(ry, 40f);
            Widgets.EndScrollView();
            if (remove >= 0)
            {
                entries.RemoveAt(remove);
                changed = true;
            }
            if (changed) SetAll(entries);

            var add = new Rect(inRect.x, inRect.yMax - bottom - 36f, 260f, 32f);
            if (Widgets.ButtonText(add, UiKit.Fit("CC_Shelf_Add".Translate(), add.width - 12f)) && !Find.WindowStack.IsOpen(typeof(Dialog_ShelfReservePick)))
                Find.WindowStack.Add(new Dialog_ShelfReservePick(shelves));
            // One slot for each kind on the shelf now that has none yet.
            var keep = new Rect(add.xMax + 8f, add.y, Mathf.Min(240f, inRect.xMax - add.xMax - 8f), 32f);
            var onShelf = first.GetSlotGroup()?.HeldThings.Select(th => "def:" + th.def.defName).Distinct()
                .Where(raw => entries.All(en => en.kind != raw)).ToList() ?? new List<string>();
            int roomLeft = capacity > 0 ? capacity - reserved : onShelf.Count;
            TooltipHandler.TipRegion(keep, "CC_Shelf_KeepTip".Translate());
            bool canKeep = onShelf.Count > 0 && roomLeft > 0;
            if (keep.width > 60f && Widgets.ButtonText(keep, UiKit.Fit("CC_Shelf_Keep".Translate(), keep.width - 12f), true, true, canKeep) && canKeep)
                SetAll(entries.Concat(onShelf.Take(roomLeft).Select(raw => new ShelfKind { kind = raw })).ToList());
        }

        private static bool Step(Rect r, string label, bool active, string tip)
        {
            TooltipHandler.TipRegion(r, tip);
            return Widgets.ButtonText(r, label, true, false, active) && active;
        }

        private void SetWhen(string kind, string when)
        {
            var first = First;
            var comp = ShelfReserves.Of(first?.Map);
            if (comp == null) return;
            var entries = comp.EntriesOf(first);
            foreach (var e in entries)
                if (e.kind == kind) e.when = when;
            SetAll(entries);
        }

        private void SetAll(List<ShelfKind> entries) => ShelfReserves.SetAll(shelves, entries);
    }
}
