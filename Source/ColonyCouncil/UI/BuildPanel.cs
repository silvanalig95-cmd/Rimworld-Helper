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
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static Vector2 scroll;
        private static float lastHeight = 1000f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static float BeginScroll(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 18f, lastHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            return view.width;
        }

        private static void EndScroll(float height)
        {
            lastHeight = Mathf.Max(height + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small) =>
            MainTabWindow_Council.WrappedLabel(x, y, width, text, color, font);

        public static void Draw(Rect rect, Map map)
        {
            if (map == null)
            {
                Widgets.Label(rect, "CC_NoHomeMap".Translate());
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Build_Header".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Faint;
            Widgets.Label(new Rect(rect.x, rect.y + 28f, rect.width, 22f), "CC_Build_Sub".Translate());
            GUI.color = Color.white;
            Widgets.CheckboxLabeled(new Rect(rect.xMax - 260f, rect.y + 4f, 260f, 24f), "CC_Build_OnlyAvailable".Translate(), ref buildOnlyAvailable);
            var outRect = new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 56f);
            float width = BeginScroll(outRect);
            float y = 0f;
            string lastGroup = null;
            int hidden = 0;
            foreach (var t in DefDatabase<RoomTemplateDef>.AllDefsListForReading.OrderBy(d => GroupOrder(d.group)).ThenBy(d => d.order))
            {
                var furniture = t.ResolveCached(map, out string reason);
                if (buildOnlyAvailable && reason != null)
                {
                    hidden++;
                    continue;
                }
                if (t.group != lastGroup)
                {
                    lastGroup = t.group;
                    y += 4f;
                    string key = "CC_BuildGroup_" + t.group;
                    Text.Font = GameFont.Medium;
                    Widgets.Label(new Rect(0f, y, width, 30f), key.CanTranslate() ? key.Translate().ToString() : t.group);
                    Text.Font = GameFont.Small;
                    y += 32f;
                }
                string furnitureText = furniture.Count == 0 ? "" : "CC_Build_Contains".Translate(string.Join(", ", furniture.GroupBy(f => f.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label)));
                float textW = width - 330f;
                string title = t.LabelCap + "  (" + t.size.x + "×" + t.size.z + ")";
                string desc = t.purpose ?? t.description;
                float h = Mathf.Max(60f, Text.CalcHeight(title, textW) + Text.CalcHeight(desc ?? "", textW) + Text.CalcHeight(furnitureText, textW) + 14f);
                var row = new Rect(0f, y, width, h);
                Widgets.DrawLightHighlight(row);
                float cy = y + 6f;
                cy += WrappedLabel(8f, cy, textW, title);
                cy += WrappedLabel(8f, cy, textW, desc, Faint);
                WrappedLabel(8f, cy, textW, reason ?? furnitureText, reason != null ? HighColor : Color.white);

                float bx = width - 310f;
                if (reason == null)
                {
                    if (Widgets.ButtonText(new Rect(bx, y + 8f, 150f, 30f), "CC_Build_Plan".Translate())) MainTabWindow_Council.StartPlanning(t, false);
                    if (Widgets.ButtonText(new Rect(bx + 156f, y + 8f, 150f, 30f), "CC_Plan_FindSpot".Translate())) MainTabWindow_Council.StartPlanning(t, true);
                }
                y += h + 6f;
            }
            if (hidden > 0) y += WrappedLabel(0f, y + 6f, width, "CC_Build_Hidden".Translate(hidden), Faint) + 10f;
            EndScroll(y);
        }

        private static bool buildOnlyAvailable = true;

        private static int GroupOrder(string group) => MainTabWindow_Rooms.GroupOrder(group);
    }
}
