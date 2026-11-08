using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning → Rooms, At a glance (what the "rooms" button showed): every room plan at a glance, architect-style,
    /// grouped into columns. Click a room to plan it, right-click to plan it at a good spot. Rooms the advisor is
    /// currently asking for are marked.
    /// </summary>
    public static class RoomsPanel
    {
        private static bool onlyAvailable = true;
        private static Vector2 scroll;

        private const int MaxColumns = 4;
        private const float ColW = 210f;
        private const float Gap = 10f;
        private const float RowH = 28f;
        private const float HeaderH = 26f;
        private const float BottomH = 34f;

        // The interface's tokens (UI/UiKit.cs): what the council suggests gold, what can't be built faint.
        private static Color Faint => UiKit.Faint;
        private static Color Suggested => UiKit.Gold;

        private struct Block
        {
            public string group;
            public List<RoomTemplateDef> rooms;
            public float Height => HeaderH + rooms.Count * RowH + 6f;
        }

        /// <summary>As many columns as fit the page, three to four in the usual window.</summary>
        private static int ColumnsFor(float width) => Mathf.Clamp(Mathf.FloorToInt((width - 16f + Gap) / (ColW + Gap)), 1, MaxColumns);

        public static void Draw(Rect inRect, Map map)
        {
            if (map == null)
            {
                UiKit.EmptyState(inRect, Icons.Rooms, "CC_NoHomeMap".Translate());
                return;
            }
            // The view's header, with the same buttons as the list of room plans beside it.
            float topH = UiKit.PageHeader(inRect, Icons.Rooms, "CC_Rooms_Title".Translate(), "CC_Rooms_Sub".Translate(), null, null, 270f);
            Text.Font = GameFont.Small;
            Widgets.CheckboxLabeled(new Rect(inRect.xMax - 260f, inRect.y + 6f, 260f, 24f), "CC_Build_OnlyAvailable".Translate(), ref onlyAvailable);
            if (UiKit.RowAct(new Rect(inRect.xMax - 260f, inRect.y + 30f, 126f, 22f), "CC_Yours_Save".Translate(), null, "CC_Yours_SaveDesc".Translate())) Designator_SaveRoom.Begin();
            if (UiKit.RowAct(new Rect(inRect.xMax - 130f, inRect.y + 30f, 130f, 22f), "CC_Rearr_Button".Translate(), null, "CC_Rearr_Desc".Translate())) Designator_RearrangeRoom.Begin();

            // The advisor's suggestions and the group headings, worked out once a second rather than every frame.
            var suggested = UiCache.Get(map, 6101, () => SuggestedRooms(map));
            int cols = ColumnsFor(inRect.width);
            var heights = Layout(map, cols, out var columns);
            var outRect = new Rect(inRect.x, inRect.y + topH, inRect.width, inRect.height - topH - BottomH);
            float tallest = heights.Length > 0 ? heights.Max() : 0f;
            var viewRect = new Rect(0f, 0f, outRect.width - (tallest > outRect.height ? 16f : 0f), tallest);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            for (int c = 0; c < columns.Length; c++)
            {
                float x = c * (ColW + Gap);
                float y = 0f;
                foreach (var block in columns[c])
                {
                    // Each group under a section heading with how many rooms it has.
                    UiKit.HeadingLine(x, y, ColW, GroupLabel(block.group), block.rooms.Count, null, null, UiKit.Heading, false, false);
                    y += HeaderH;
                    foreach (var t in block.rooms)
                    {
                        DrawRoom(new Rect(x, y, ColW, RowH - 2f), t, map, suggested.TryGetValue(t, out var why) ? why : null);
                        y += RowH;
                    }
                    y += 6f;
                }
            }
            Widgets.EndScrollView();

            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x, inRect.yMax - BottomH + 4f, inRect.width, BottomH), "CC_Rooms_Hint".Translate());
            Text.Font = GameFont.Small;
            GUI.color = UiKit.Ink;
        }

        private static void DrawRoom(Rect r, RoomTemplateDef t, Map map, string suggestedBecause)
        {
            t.ResolveCached(map, out string reason);
            bool ok = reason == null;
            Widgets.DrawHighlightIfMouseover(r);
            if (suggestedBecause != null) Widgets.DrawBoxSolid(r, UiKit.WithAlpha(Suggested, 0.12f));
            GUI.color = ok ? UiKit.Ink : UiKit.WithAlpha(UiKit.Ink, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.height - 4f, r.height - 4f), t.Icon);
            GUI.color = !ok ? Faint : suggestedBecause != null ? Suggested : UiKit.Ink;
            Text.Anchor = TextAnchor.MiddleLeft;
            var type = RoomTypeDef.Of(t);
            int sizes = type?.Variants.Count ?? 1;
            float labelW = r.width - r.height - 4f - (sizes > 1 ? 18f : 0f);
            Widgets.Label(new Rect(r.x + r.height + 4f, r.y, labelW, r.height), RoomMenu.Label(t).Truncate(labelW - 4f));
            if (sizes > 1)
            {
                // How many sizes it comes in, small and faint at the right.
                Text.Font = GameFont.Tiny;
                GUI.color = Faint;
                Widgets.Label(new Rect(r.xMax - 18f, r.y, 18f, r.height), sizes.ToString());
                Text.Font = GameFont.Small;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UiKit.Ink;
            TooltipHandler.TipRegion(r, () => Tip(t, map, reason, suggestedBecause), t.GetHashCode());
            if (!Widgets.ButtonInvisible(r)) return;
            if (!ok && t.custom == null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (Event.current.button == 1)
            {
                Find.WindowStack.Add(new FloatMenu(RoomMenu.Options(t)));
                return;
            }
            if (!ok)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Designator_PlanRoom.Begin(t, false);
        }

        private static string Tip(RoomTemplateDef t, Map map, string reason, string suggestedBecause)
        {
            var lines = new List<string> { t.FullLabel.CapitalizeFirst() + "  (" + t.size.x + "×" + t.size.z + ")" };
            string desc = t.purpose ?? t.description;
            if (!desc.NullOrEmpty()) lines.Add(desc);
            string sizesLine = RoomMenu.SizesLine(t);
            if (sizesLine != null) lines.Add(sizesLine);
            var furniture = t.ResolveCached(map, out _);
            if (furniture.Count > 0)
                lines.Add("CC_Build_Contains".Translate(string.Join(", ", furniture.GroupBy(f => f.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label))));
            if (suggestedBecause != null) lines.Add("\n" + "CC_Rooms_Suggested".Translate(suggestedBecause).Colorize(Suggested));
            if (reason != null) lines.Add("\n" + reason.Colorize(UiKit.Sour));
            string tip = Flavor.PickTip(t.tips, 0);
            if (!tip.NullOrEmpty()) lines.Add("\n" + tip);
            return string.Join("\n", lines);
        }

        private static readonly Dictionary<string, string> groupLabels = new Dictionary<string, string>();

        /// <summary>A group's heading ("CC_BuildGroup_Living"), translated once.</summary>
        private static string GroupLabel(string group)
        {
            string g = group ?? "";
            if (!groupLabels.TryGetValue(g, out var label))
            {
                string key = "CC_BuildGroup_" + group;
                groupLabels[g] = label = key.CanTranslate() ? key.Translate().ToString() : group ?? "";
            }
            return label;
        }

        /// <summary>Rooms the advisor's current warnings point at, with the warning that asks for them.</summary>
        private static Dictionary<RoomTemplateDef, string> SuggestedRooms(Map map)
        {
            var result = new Dictionary<RoomTemplateDef, string>();
            var signals = CouncilMapComponent.For(map)?.Signals;
            if (signals == null) return result;
            foreach (var s in signals.OrderByDescending(s => s.Score))
            {
                var t = s.template ?? s.def?.template;
                if (t == null || s.Level == AdviceLevel.Idea) continue;
                // Whichever size of the room the menu shows, it's the one the council means.
                foreach (var sibling in RoomTypeDef.SiblingsOf(t))
                    if (!result.ContainsKey(sibling)) result[sibling] = s.title ?? s.def.label;
            }
            return result;
        }

        // The last layout, kept for a second: working out which rooms and sizes the colony can plan is too much for every frame.
        private static List<Block>[] lastColumns;
        private static float[] lastHeights;
        private static float lastAt = -999f;
        private static int lastMapId = -1, lastSaved = -1, lastCols = -1;
        private static bool lastOnly;

        /// <summary>Groups packed into columns, each group going to the shortest column so far.</summary>
        private static float[] Layout(Map map, int cols, out List<Block>[] columns)
        {
            float now = Time.realtimeSinceStartup;
            int mapId = map?.uniqueID ?? -1, saved = CustomRooms.All.Count;
            if (lastColumns != null && mapId == lastMapId && saved == lastSaved && cols == lastCols && onlyAvailable == lastOnly && now >= lastAt && now - lastAt < 1f)
            {
                columns = lastColumns;
                return lastHeights;
            }
            lastHeights = BuildLayout(map, cols, out lastColumns);
            lastAt = now;
            lastMapId = mapId;
            lastSaved = saved;
            lastCols = cols;
            lastOnly = onlyAvailable;
            columns = lastColumns;
            return lastHeights;
        }

        private static float[] BuildLayout(Map map, int cols, out List<Block>[] columns)
        {
            columns = new List<Block>[cols];
            for (int i = 0; i < cols; i++) columns[i] = new List<Block>();
            var heights = new float[cols];
            if (map == null) return heights;
            var blocks = RoomMenu.Entries(map, onlyAvailable)
                .GroupBy(t => t.group)
                .OrderBy(g => GroupOrder(g.Key))
                .Select(g => new Block { group = g.Key, rooms = g.OrderBy(t => t.order).ThenBy(t => t.label).ToList() })
                .ToList();
            foreach (var b in blocks)
            {
                int best = 0;
                for (int i = 1; i < cols; i++)
                    if (heights[i] < heights[best] - 0.1f) best = i;
                columns[best].Add(b);
                heights[best] += b.Height;
            }
            return heights;
        }

        public static int GroupOrder(string group)
        {
            switch (group)
            {
                case "Living": return 0;
                case "Food": return 1;
                case "Work": return 2;
                case "Care": return 3;
                case "Leisure": return 4;
                case "Security": return 5;
                case CustomRooms.Group: return 7;
                default: return 6;
            }
        }
    }
}
