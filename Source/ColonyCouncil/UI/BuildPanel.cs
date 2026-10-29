using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Colony → Room plans: every room plan, grouped, with buttons to plan it by hand or at a good spot.</summary>
    public static class BuildPanel
    {
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static Vector2 scroll;
        private static float height = 1000f;
        private static bool buildOnlyAvailable = true;

        private const float IconW = 52f;
        private const float ButtonsW = 320f;

        /// <summary>A card's text and measurements, refreshed every couple of seconds (research and stock change what's available).</summary>
        private class Layout
        {
            public float width = -1f, at, titleH, descH, lineH, height;
            public string title, desc, line, reason;
        }

        private static readonly Dictionary<RoomTemplateDef, Layout> layouts = new Dictionary<RoomTemplateDef, Layout>();
        private static List<RoomTemplateDef> ordered;
        private static float orderedAt = -999f;
        private static Map orderedMap;

        private static int GroupOrder(string group) => MainTabWindow_Rooms.GroupOrder(group);

        public static void Draw(Rect rect, Map map)
        {
            if (map == null)
            {
                UiKit.EmptyState(rect, Icons.Rooms, "CC_NoHomeMap".Translate());
                return;
            }
            // One card per kind of room (its sizes are picked in the planner), refreshed as research and stock change.
            float now = Time.realtimeSinceStartup;
            if (ordered == null || orderedMap != map || now - orderedAt > 2f)
            {
                ordered = RoomMenu.Entries(map, false).OrderBy(d => GroupOrder(d.group)).ThenBy(d => d.order).ToList();
                orderedAt = now;
                orderedMap = map;
            }
            int available = UiCache.Get(map, 51, () => ordered.Count(t => t.UnavailableReason(map) == null), 2f);
            float top = UiKit.PageHeader(rect, Icons.Rooms, "CC_Build_Header".Translate(), "CC_Build_Sub".Translate(),
                "CC_UI_Stat_Plans".Translate(available).Resolve(), null, 270f);
            Widgets.CheckboxLabeled(new Rect(rect.xMax - 260f, rect.y + 6f, 260f, 24f), "CC_Build_OnlyAvailable".Translate(), ref buildOnlyAvailable);
            var saveRect = new Rect(rect.xMax - 260f, rect.y + 28f, 126f, 22f);
            if (Widgets.ButtonText(saveRect, "CC_Yours_Save".Translate())) Designator_SaveRoom.Begin();
            TooltipHandler.TipRegion(saveRect, "CC_Yours_SaveDesc".Translate());
            var tidyRect = new Rect(rect.xMax - 130f, rect.y + 28f, 130f, 22f);
            if (Widgets.ButtonText(tidyRect, "CC_Rearr_Button".Translate())) Designator_RearrangeRoom.Begin();
            TooltipHandler.TipRegion(tidyRect, "CC_Rearr_Desc".Translate());

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float width = view.width;
            float y = 0f;
            string lastGroup = null;
            int hidden = 0;
            foreach (var t in ordered)
            {
                var l = LayoutOf(t, map, width);
                if (buildOnlyAvailable && l.reason != null)
                {
                    hidden++;
                    continue;
                }
                if (t.group != lastGroup)
                {
                    lastGroup = t.group;
                    if (y > 0f) y += 8f;
                    string key = "CC_BuildGroup_" + t.group;
                    y += UiKit.SectionTitle(0f, y, width, key.CanTranslate() ? key.Translate().ToString() : t.group ?? "", GroupIcon(t.group));
                }
                // Only cards on screen are drawn.
                if (y + l.height >= scroll.y && y <= scroll.y + outRect.height) DrawCard(t, l, y, width);
                y += l.height + 6f;
            }
            if (hidden > 0) y += UiKit.Paragraph(0f, y + 6f, width, "CC_Build_Hidden".Translate(hidden), UiKit.Faint) + 10f;
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static Layout LayoutOf(RoomTemplateDef t, Map map, float width)
        {
            float now = Time.realtimeSinceStartup;
            if (layouts.TryGetValue(t, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 2f) return l;
            var furniture = t.ResolveCached(map, out string reason);
            l = new Layout
            {
                width = width,
                at = now,
                reason = reason,
                title = RoomMenu.Label(t) + "  (" + t.size.x + "×" + t.size.z + ")",
                desc = t.purpose ?? t.description
            };
            string sizes = RoomMenu.SizesLine(t);
            if (sizes != null) l.desc = (l.desc.NullOrEmpty() ? "" : l.desc + "\n") + sizes;
            l.line = reason ?? (furniture.Count == 0 ? "" : "CC_Build_Contains".Translate(string.Join(", ",
                furniture.GroupBy(f => f.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label))).Resolve());
            float textW = TextWidth(width);
            Text.Font = GameFont.Small;
            l.titleH = Text.CalcHeight(l.title, textW);
            l.descH = l.desc.NullOrEmpty() ? 0f : Text.CalcHeight(l.desc, textW);
            Text.Font = GameFont.Tiny;
            l.lineH = l.line.NullOrEmpty() ? 0f : Text.CalcHeight(l.line, textW);
            Text.Font = GameFont.Small;
            l.height = Mathf.Max(IconW + 12f, l.titleH + l.descH + l.lineH + 16f);
            layouts[t] = l;
            return l;
        }

        private static float TextWidth(float width) => Mathf.Max(80f, width - IconW - 20f - ButtonsW);

        private static void DrawCard(RoomTemplateDef t, Layout l, float y, float width)
        {
            var row = new Rect(0f, y, width, l.height);
            UiKit.Card(row, l.reason != null ? UiKit.WithAlpha(HighColor, 0.6f) : (Color?)null);
            var iconRect = new Rect(10f, y + 6f, IconW, IconW);
            Widgets.DrawBoxSolid(iconRect, new Color(0f, 0f, 0f, 0.25f));
            GUI.color = l.reason == null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (t.Icon != null) GUI.DrawTexture(iconRect.ContractedBy(4f), t.Icon);
            GUI.color = Color.white;

            float textX = IconW + 20f;
            float textW = TextWidth(width);
            float cy = y + 6f;
            Widgets.Label(new Rect(textX, cy, textW, l.titleH), l.title);
            cy += l.titleH;
            if (l.descH > 0f)
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, textW, l.descH), l.desc);
                GUI.color = Color.white;
                cy += l.descH;
            }
            if (l.lineH > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = l.reason != null ? HighColor : new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(textX, cy + 2f, textW, l.lineH), l.line);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            float bx = width - ButtonsW + 4f;
            float bw = (ButtonsW - 18f) / 2f;
            if (l.reason == null)
            {
                if (Widgets.ButtonText(new Rect(bx, y + 8f, bw, 30f), "CC_Build_Plan".Translate())) MainTabWindow_Council.StartPlanning(t, false);
                if (Widgets.ButtonText(new Rect(bx + bw + 6f, y + 8f, bw, 30f), "CC_Plan_FindSpot".Translate())) MainTabWindow_Council.StartPlanning(t, true);
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(new Rect(bx, y + 42f, bw, 22f), "CC_Upgrade_Button".Translate())) Designator_PlanRoom.Begin(t, false, true);
                Text.Font = GameFont.Small;
            }
            if (t.custom != null)
            {
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(new Rect(bx + bw + 6f, y + 42f, bw, 22f), "CC_Yours_Delete".Translate())) CustomRooms.Delete(t.custom);
                Text.Font = GameFont.Small;
            }
        }

        private static Texture2D GroupIcon(string group)
        {
            switch (group)
            {
                case "Living": return Icons.Colony;
                case "Food": return Icons.CatFood;
                case "Work": return Icons.CatManagement;
                case "Care": return Icons.CatHealth;
                case "Leisure": return Icons.Moods;
                case "Security": return Icons.CatDefense;
                default: return Icons.Rooms;
            }
        }
    }
}
