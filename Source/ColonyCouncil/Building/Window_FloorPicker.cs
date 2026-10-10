using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The floor chooser for the room planner. Instead of one endless menu, floors are grouped into families
    /// (stone tiles, carpets, ...) with one row each; the materials of a family are small buttons on that row.
    /// By default it only shows what the colony has the materials for, enough for the whole room.
    /// </summary>
    public class Window_FloorPicker : Window
    {
        private readonly RoomTemplateDef template;
        private readonly RoomPlanConfig cfg;
        private readonly Map map;
        private readonly Rect near;
        private readonly int area;
        private readonly List<Family> families;
        private static bool onlyAffordable = true;
        private Vector2 scroll;
        private float contentHeight = 300f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Short = new Color(1f, 0.5f, 0.45f);
        private static readonly Color Chosen = new Color(0.55f, 0.85f, 0.55f);

        private class Family
        {
            public string label;
            /// <summary>Most of the materials in stock first.</summary>
            public List<TerrainDef> members;
            /// <summary>What tells the members apart ("granite", "marble"), or the full label for one-member families.</summary>
            public Dictionary<TerrainDef, string> shortLabels;
        }

        public Window_FloorPicker(RoomTemplateDef template, RoomPlanConfig cfg, Map map, Rect near)
        {
            this.template = template;
            this.cfg = cfg;
            this.map = map;
            this.near = near;
            area = Mathf.Max(1, template.size.x * template.size.z);
            families = BuildFamilies(RoomPlanConfig.FloorOptions(map), map);
            layer = WindowLayer.GameUI;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            draggable = true;
            soundAppear = null;
        }

        protected override float Margin => 10f;

        public override Vector2 InitialSize => new Vector2(430f, 520f);

        protected override void SetInitialSizeAndPosition()
        {
            var size = InitialSize;
            float x = near.width > 0f ? near.xMax + 6f : (UI.screenWidth - size.x) / 2f;
            float y = near.height > 0f ? Mathf.Min(near.y, UI.screenHeight - 35f - size.y - 6f) : (UI.screenHeight - size.y) / 2f;
            windowRect = new Rect(Mathf.Clamp(x, 0f, UI.screenWidth - size.x), Mathf.Max(0f, y), size.x, size.y);
        }

        public static void Open(RoomTemplateDef t, RoomPlanConfig cfg, Map map, Rect near)
        {
            var existing = Find.WindowStack.WindowOfType<Window_FloorPicker>();
            if (existing != null)
            {
                existing.Close(false);
                if (existing.template == t) return;
            }
            Find.WindowStack.Add(new Window_FloorPicker(t, cfg, map, near));
        }

        public override void DoWindowContents(Rect inRect)
        {
            var current = cfg.Floor(template, map);
            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width - 30f, 32f), "CC_Floor_Title".Translate(template.label));
            Text.Font = GameFont.Small;
            y += 34f;

            // Quick picks: none, and what the plan would use on its own.
            float bx = inRect.x;
            if (QuickButton(ref bx, y, "CC_Planner_NoFloor".Translate(), current == null)) Pick(null);
            var suggested = template.ResolveFloor(map);
            if (suggested != null && QuickButton(ref bx, y, "CC_Floor_Suggested".Translate(suggested.label), current == suggested)) Pick(suggested);
            y += 30f;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, inRect.width, 24f), "CC_Floor_OnlyAffordable".Translate(area), ref onlyAffordable);
            y += 28f;

            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 22f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float cy = 0f;
            int hidden = 0;
            foreach (var fam in families)
            {
                var shown = onlyAffordable ? fam.members.Where(f => Affordable(f) || f == current).ToList() : fam.members;
                if (shown.Count == 0)
                {
                    hidden++;
                    continue;
                }
                cy += DrawFamily(fam, shown, current, viewRect.width, cy) + 4f;
            }
            if (hidden > 0)
            {
                GUI.color = Faint;
                Text.Font = GameFont.Tiny;
                string note = "CC_Floor_Hidden".Translate(hidden);
                float h = Text.CalcHeight(note, viewRect.width);
                Widgets.Label(new Rect(0f, cy + 2f, viewRect.width, h), note);
                cy += h + 6f;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
            contentHeight = Mathf.Max(cy, 40f);
            Widgets.EndScrollView();

            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x, inRect.yMax - 20f, inRect.width, 20f), "CC_Floor_Hint".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private float DrawFamily(Family fam, List<TerrainDef> shown, TerrainDef current, float width, float y)
        {
            bool hasCurrent = current != null && fam.members.Contains(current);
            var rep = hasCurrent ? current : shown[0];
            const float iconSize = 36f;
            float textX = iconSize + 8f;
            float textW = width - textX;

            // Chips for families with several materials; a whole-row click for the rest.
            bool chips = fam.members.Count > 1;
            float chipsHeight = chips ? ChipsHeight(fam, shown, textW) : 0f;
            float h = Mathf.Max(iconSize + 8f, 46f + chipsHeight);
            var row = new Rect(0f, y, width, h);
            if (hasCurrent) Widgets.DrawBoxSolid(row, new Color(0.3f, 0.45f, 0.3f, 0.25f));
            else Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.DefIcon(new Rect(4f, y + 4f, iconSize, iconSize), rep);
            Widgets.Label(new Rect(textX, y + 2f, textW, 24f), fam.label.CapitalizeFirst());
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textX, y + 22f, textW, 20f), Stats(rep));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (chips)
            {
                float cx = textX;
                float cyy = y + 44f;
                foreach (var f in shown)
                {
                    string label = fam.shortLabels[f];
                    float w = Text.CalcSize(label).x + 14f;
                    if (cx + w > width && cx > textX)
                    {
                        cx = textX;
                        cyy += 26f;
                    }
                    var r = new Rect(cx, cyy, w, 24f);
                    bool affordable = Affordable(f);
                    GUI.color = f == current ? Chosen : affordable ? Color.white : Short;
                    if (Widgets.ButtonText(r, label)) Pick(f);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(r, Tip(f));
                    cx += w + 4f;
                }
            }
            else
            {
                var f = shown[0];
                if (!Affordable(f))
                {
                    GUI.color = Short;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(width - 90f, y + 4f, 86f, 20f), "CC_Floor_Short".Translate());
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                }
                TooltipHandler.TipRegion(row, Tip(f));
                if (Widgets.ButtonInvisible(row)) Pick(f);
            }
            return h;
        }

        private static float ChipsHeight(Family fam, List<TerrainDef> shown, float width)
        {
            float cx = 0f;
            int lines = 1;
            foreach (var f in shown)
            {
                float w = Text.CalcSize(fam.shortLabels[f]).x + 14f;
                if (cx + w > width && cx > 0f)
                {
                    cx = 0f;
                    lines++;
                }
                cx += w + 4f;
            }
            return lines * 26f;
        }

        private static bool QuickButton(ref float x, float y, string label, bool active)
        {
            float w = Mathf.Min(Text.CalcSize(label).x + 20f, 260f);
            var r = new Rect(x, y, w, 26f);
            x += w + 6f;
            GUI.color = active ? Chosen : Color.white;
            bool clicked = Widgets.ButtonText(r, label);
            GUI.color = Color.white;
            return clicked;
        }

        private void Pick(TerrainDef f)
        {
            cfg.floorChosen = true;
            cfg.floor = f;
            cfg.Changed();
            Close();
        }

        private bool Affordable(TerrainDef f)
        {
            foreach (var c in f.CostListAdjusted(null, false))
                if (map.resourceCounter.GetCount(c.thingDef) < c.count * area) return false;
            return true;
        }

        private static string Stats(TerrainDef f)
        {
            var parts = new List<string>();
            float beauty = f.GetStatValueAbstract(StatDefOf.Beauty);
            float clean = f.GetStatValueAbstract(StatDefOf.Cleanliness);
            if (beauty != 0f) parts.Add("CC_Floor_Beauty".Translate(beauty.ToStringByStyle(ToStringStyle.FloatMaxOne, ToStringNumberSense.Offset)));
            if (clean != 0f) parts.Add("CC_Floor_Clean".Translate(clean.ToStringByStyle(ToStringStyle.FloatMaxOne, ToStringNumberSense.Offset)));
            float work = f.GetStatValueAbstract(StatDefOf.WorkToBuild);
            parts.Add(work <= 300f ? "CC_Floor_Quick".Translate() : work >= 1500f ? "CC_Floor_Slow".Translate() : "CC_Floor_Medium".Translate());
            return string.Join("  ·  ", parts);
        }

        private string Tip(TerrainDef f)
        {
            var lines = new List<string> { f.LabelCap.ToString() };
            if (!f.description.NullOrEmpty()) lines.Add(f.description);
            var cost = f.CostListAdjusted(null, false);
            if (cost.Count > 0)
            {
                lines.Add("");
                foreach (var c in cost)
                    lines.Add("CC_Floor_CostLine".Translate(c.count * area, c.thingDef.label, map.resourceCounter.GetCount(c.thingDef)));
            }
            return string.Join("\n", lines);
        }

        private static List<Family> BuildFamilies(List<TerrainDef> floors, Map map)
        {
            var result = new List<Family>();
            foreach (var group in floors.GroupBy(f => f.designatorDropdown?.defName ?? "floor:" + f.defName))
            {
                var members = group
                    .OrderByDescending(f => f.CostListAdjusted(null, false).Select(c => map.resourceCounter.GetCount(c.thingDef) / Mathf.Max(1, c.count)).DefaultIfEmpty(int.MaxValue).Min())
                    .ThenBy(f => f.label)
                    .ToList();
                var fam = new Family { members = members, shortLabels = new Dictionary<TerrainDef, string>() };
                if (members.Count == 1)
                {
                    fam.label = members[0].label;
                    fam.shortLabels[members[0]] = members[0].LabelCap;
                }
                else
                {
                    var dd = members[0].designatorDropdown;
                    string suffix = CommonSuffix(members.Select(m => m.label).ToList());
                    fam.label = !dd.label.NullOrEmpty() ? dd.label : !suffix.NullOrEmpty() ? suffix : members[0].label;
                    foreach (var m in members)
                    {
                        string s = m.label;
                        if (!suffix.NullOrEmpty() && s.Length > suffix.Length && s.EndsWith(suffix)) s = s.Substring(0, s.Length - suffix.Length).Trim();
                        fam.shortLabels[m] = s.CapitalizeFirst();
                    }
                }
                result.Add(fam);
            }
            return result
                .OrderBy(f => f.members.Min(m => m.GetStatValueAbstract(StatDefOf.WorkToBuild)))
                .ThenBy(f => f.label)
                .ToList();
        }

        /// <summary>The words all labels end with: "granite tile" + "marble tile" give "tile".</summary>
        private static string CommonSuffix(List<string> labels)
        {
            var split = labels.Select(l => (l ?? "").Split(' ')).ToList();
            int n = split.Min(s => s.Length);
            int k = 0;
            while (k < n)
            {
                string word = split[0][split[0].Length - 1 - k];
                if (!split.All(s => s[s.Length - 1 - k] == word)) break;
                k++;
            }
            // Keep at least one distinguishing word in every label.
            if (k == 0 || split.Any(s => s.Length <= k)) return "";
            return string.Join(" ", split[0].Skip(split[0].Length - k));
        }
    }
}
