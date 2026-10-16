using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The floor chooser for the room planner. Floors are grouped into families (stone tiles, carpets, plank floors,
    /// ...) with one row each and every design of the family as a small button on that row. A floor that can be made
    /// of different materials (wood, stone, metal) gets a material row once chosen. By default it only shows what the
    /// colony has the materials for; designs that still need research can be shown greyed out.
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
        private static bool showLocked;
        private Vector2 scroll;
        private float contentHeight = 300f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Short = new Color(1f, 0.5f, 0.45f);
        private static readonly Color Chosen = new Color(0.55f, 0.85f, 0.55f);
        private static readonly Color LockedColor = new Color(0.55f, 0.55f, 0.55f);

        private class Family
        {
            public string label;
            /// <summary>Most of the materials in stock first.</summary>
            public List<TerrainDef> members;
            /// <summary>What tells the members apart ("granite", "parquet"), or the full label for one-member families.</summary>
            public Dictionary<TerrainDef, string> shortLabels;
        }

        public Window_FloorPicker(RoomTemplateDef template, RoomPlanConfig cfg, Map map, Rect near)
        {
            this.template = template;
            this.cfg = cfg;
            this.map = map;
            this.near = near;
            area = Mathf.Max(1, template.size.x * template.size.z);
            families = BuildFamilies(RoomPlanConfig.FloorOptions(map, true), map);
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

        public override Vector2 InitialSize => new Vector2(460f, 580f);

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

        // ---------------- Materials ----------------

        // ---------------- What each floor shows, kept a second ----------------

        // The window is drawn every frame, and a floor mod brings hundreds of floors: whether each is researched, has
        // enough material, which material it would take, its stats line and its tooltip are worked out once a second and
        // kept, and at once again after a pick, a material or a checkbox (the plan's version, the filters, the floor).
        private float keptAt = -1f;
        private int keptVersion = int.MinValue;
        private bool keptOnly, keptShowLocked;
        private TerrainDef keptCurrent;
        private readonly Dictionary<TerrainDef, bool> lockedKept = new Dictionary<TerrainDef, bool>();
        private readonly Dictionary<TerrainDef, ThingDef> stuffKept = new Dictionary<TerrainDef, ThingDef>();
        private readonly Dictionary<(TerrainDef, ThingDef), bool> affordableKept = new Dictionary<(TerrainDef, ThingDef), bool>();
        private readonly Dictionary<(TerrainDef, ThingDef), string> statsKept = new Dictionary<(TerrainDef, ThingDef), string>();
        private readonly Dictionary<(TerrainDef, ThingDef), string> tipKept = new Dictionary<(TerrainDef, ThingDef), string>();
        private readonly Dictionary<Family, List<TerrainDef>> shownKept = new Dictionary<Family, List<TerrainDef>>();
        private TerrainDef suggestedKept;

        private void Keep(TerrainDef current)
        {
            float now = Time.realtimeSinceStartup;
            if (now - keptAt <= 1f && now >= keptAt && keptVersion == cfg.version && keptCurrent == current && keptOnly == onlyAffordable
                && keptShowLocked == showLocked)
                return;
            keptAt = now;
            keptVersion = cfg.version;
            keptCurrent = current;
            keptOnly = onlyAffordable;
            keptShowLocked = showLocked;
            lockedKept.Clear();
            stuffKept.Clear();
            affordableKept.Clear();
            statsKept.Clear();
            tipKept.Clear();
            shownKept.Clear();
            suggestedKept = template.FloorFor(cfg.style, map, cfg.theme) ?? template.ResolveFloor(map);
        }

        private ThingDef StuffFor(TerrainDef f)
        {
            if (f == null || !f.MadeFromStuff) return null;
            if (!stuffKept.TryGetValue(f, out var stuff))
                stuffKept[f] = stuff = f == cfg.Floor(template, map) ? cfg.FloorStuff(f, map, area) : MaterialPicker.Pick(f, map, area, MaterialUse.Floor);
            return stuff;
        }

        private bool Locked(TerrainDef f)
        {
            if (!lockedKept.TryGetValue(f, out bool locked)) lockedKept[f] = locked = !Avail.CanBuild(f, map);
            return locked;
        }

        private string StatsOf(TerrainDef f, ThingDef stuff)
        {
            if (!statsKept.TryGetValue((f, stuff), out var text)) statsKept[(f, stuff)] = text = Stats(f, stuff);
            return text;
        }

        private string TipOf(TerrainDef f, ThingDef stuff)
        {
            if (!tipKept.TryGetValue((f, stuff), out var text)) tipKept[(f, stuff)] = text = Tip(f, stuff);
            return text;
        }

        /// <summary>Stored plus lying around, the same count the planner and the build menu use.</summary>
        private int Have(ThingDef d) => MaterialCounts.Have(map, d);

        private bool Affordable(TerrainDef f, ThingDef stuff)
        {
            if (affordableKept.TryGetValue((f, stuff), out bool ok)) return ok;
            ok = true;
            if (f.MadeFromStuff && stuff == null) ok = false;
            else
                foreach (var c in f.CostListAdjusted(stuff, false))
                    if (Have(c.thingDef) < c.count * area)
                    {
                        ok = false;
                        break;
                    }
            affordableKept[(f, stuff)] = ok;
            return ok;
        }

        private bool Affordable(TerrainDef f) => Affordable(f, StuffFor(f));

        // ---------------- Drawing ----------------

        public override void DoWindowContents(Rect inRect)
        {
            var current = cfg.Floor(template, map);
            Keep(current);
            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width - 30f, 32f), "CC_Floor_Title".Translate(template.label));
            Text.Font = GameFont.Small;
            y += 34f;

            // Quick picks: none, and what the plan would use on its own.
            float bx = inRect.x;
            if (QuickButton(ref bx, y, "CC_Planner_NoFloor".Translate(), current == null)) Pick(null, true);
            var suggested = suggestedKept;
            if (suggested != null && QuickButton(ref bx, y, "CC_Floor_Suggested".Translate(suggested.label), current == suggested)) Pick(suggested, !suggested.MadeFromStuff);
            y += 30f;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, inRect.width, 24f), "CC_Floor_OnlyAffordable".Translate(area), ref onlyAffordable);
            y += 26f;
            Widgets.CheckboxLabeled(new Rect(inRect.x, y, inRect.width, 24f), "CC_Floor_ShowLocked".Translate(), ref showLocked);
            y += 28f;

            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 22f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float cy = 0f;
            // The floor chosen now, with its material if it has one.
            if (current != null && current.MadeFromStuff)
            {
                cy += DrawMaterials(current, viewRect.width, cy) + 8f;
            }
            int hidden = 0;
            foreach (var fam in families)
            {
                if (!shownKept.TryGetValue(fam, out var shown))
                    shownKept[fam] = shown = fam.members.Where(f => f == current || ((showLocked || !Locked(f)) && (!onlyAffordable || Locked(f) || Affordable(f)))).ToList();
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

        /// <summary>The material row for a floor made of wood, stone or metal: every allowed material, stock first.</summary>
        // The chosen floor's material row (its material, the choices in stock order and their chips), kept with the rest.
        private TerrainDef materialsFor;
        private ThingDef materialsChosen;
        private List<ThingDef> materialsOptions;
        private List<string> materialsLabels;
        private float materialsAt = -1f;

        private float DrawMaterials(TerrainDef floor, float width, float y)
        {
            int need = floor.CostStuffCount * area;
            if (materialsFor != floor || materialsOptions == null || materialsAt != keptAt)
            {
                materialsFor = floor;
                materialsAt = keptAt;
                materialsChosen = cfg.FloorStuff(floor, map, area);
                var picked = materialsChosen;
                materialsOptions = Avail.AllowedStuffs(floor)
                    .Where(s => MaterialPicker.Allowed(s, MaterialUse.Floor) && (s == picked || !onlyAffordable || Have(s) >= need))
                    .OrderByDescending(s => Have(s))
                    .ThenBy(s => s.label)
                    .ToList();
                materialsLabels = materialsOptions.Select(s => "CC_Floor_MaterialChip".Translate(s.LabelAsStuff.CapitalizeFirst(), Have(s)).ToString()).ToList();
            }
            var chosen = materialsChosen;
            var options = materialsOptions;
            var labels = materialsLabels;
            const float iconSize = 36f;
            float textX = iconSize + 8f;
            float textW = width - textX;
            float chipsH = ChipsHeight(labels, textW);
            float h = 48f + chipsH;
            var row = new Rect(0f, y, width, h);
            Widgets.DrawBoxSolid(row, new Color(0.3f, 0.45f, 0.3f, 0.25f));
            Widgets.DefIcon(new Rect(4f, y + 4f, iconSize, iconSize), floor, chosen);
            Widgets.Label(new Rect(textX, y + 2f, textW, 24f), "CC_Floor_MaterialOf".Translate(GenLabel.ThingLabel(floor, chosen).CapitalizeFirst()));
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textX, y + 22f, textW, 20f), options.Count > 0 ? "CC_Floor_MaterialHint".Translate(need).ToString() : "CC_Floor_NoMaterial".Translate().ToString());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            float cx = textX, cyy = y + 44f;
            for (int i = 0; i < options.Count; i++)
            {
                var s = options[i];
                float w = Text.CalcSize(labels[i]).x + 14f;
                if (cx + w > width && cx > textX)
                {
                    cx = textX;
                    cyy += 26f;
                }
                var r = new Rect(cx, cyy, w, 24f);
                GUI.color = s == chosen ? Chosen : Have(s) >= need ? Color.white : Short;
                if (Widgets.ButtonText(r, labels[i])) cfg.PickFloorStuff(s);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(r, TipOf(floor, s));
                cx += w + 4f;
            }
            return h;
        }

        private float DrawFamily(Family fam, List<TerrainDef> shown, TerrainDef current, float width, float y)
        {
            bool hasCurrent = current != null && fam.members.Contains(current);
            var rep = hasCurrent ? current : shown[0];
            const float iconSize = 36f;
            float textX = iconSize + 8f;
            float textW = width - textX;

            // Chips for families with several designs; a whole-row click for the rest.
            bool chips = fam.members.Count > 1;
            float chipsHeight = chips ? ChipsHeight(shown.Select(f => fam.shortLabels[f]).ToList(), textW) : 0f;
            float h = Mathf.Max(iconSize + 8f, 46f + chipsHeight);
            var row = new Rect(0f, y, width, h);
            if (hasCurrent) Widgets.DrawBoxSolid(row, new Color(0.3f, 0.45f, 0.3f, 0.25f));
            else Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            Widgets.DefIcon(new Rect(4f, y + 4f, iconSize, iconSize), rep, StuffFor(rep));
            string title = fam.label.CapitalizeFirst();
            if (fam.members.Any(m => m.MadeFromStuff)) title += "  " + "CC_Floor_HasMaterials".Translate();
            Widgets.Label(new Rect(textX, y + 2f, textW, 24f), title);
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textX, y + 22f, textW, 20f), StatsOf(rep, StuffFor(rep)));
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
                    bool locked = Locked(f);
                    GUI.color = f == current ? Chosen : locked ? LockedColor : Affordable(f) ? Color.white : Short;
                    if (Widgets.ButtonText(r, label, true, true, !locked) && !locked) Pick(f, !f.MadeFromStuff);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(r, TipOf(f, StuffFor(f)));
                    cx += w + 4f;
                }
            }
            else
            {
                var f = shown[0];
                bool locked = Locked(f);
                string flag = locked ? "CC_Floor_Locked".Translate().ToString() : !Affordable(f) ? "CC_Floor_Short".Translate().ToString() : null;
                if (flag != null)
                {
                    GUI.color = locked ? LockedColor : Short;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(width - 110f, y + 4f, 106f, 20f), flag);
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                }
                TooltipHandler.TipRegion(row, TipOf(f, StuffFor(f)));
                if (!locked && Widgets.ButtonInvisible(row)) Pick(f, !f.MadeFromStuff);
            }
            return h;
        }

        private static float ChipsHeight(List<string> labels, float width)
        {
            float cx = 0f;
            int lines = 1;
            foreach (var l in labels)
            {
                float w = Text.CalcSize(l).x + 14f;
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

        /// <summary>Chooses a floor. One that comes in several materials keeps the window open so the material can be picked.</summary>
        private void Pick(TerrainDef f, bool close)
        {
            cfg.floorChosen = true;
            cfg.floor = f;
            if (f != null && f.MadeFromStuff && cfg.floorStuff != null && !Avail.StuffAllowed(f, cfg.floorStuff)) cfg.floorStuff = null;
            cfg.Changed();
            if (close) Close();
            else scroll = Vector2.zero;
        }

        private static string Stats(TerrainDef f, ThingDef stuff)
        {
            var parts = new List<string>();
            float beauty = f.GetStatValueAbstract(StatDefOf.Beauty, stuff);
            float clean = f.GetStatValueAbstract(StatDefOf.Cleanliness, stuff);
            if (beauty != 0f) parts.Add("CC_Floor_Beauty".Translate(beauty.ToStringByStyle(ToStringStyle.FloatMaxOne, ToStringNumberSense.Offset)));
            if (clean != 0f) parts.Add("CC_Floor_Clean".Translate(clean.ToStringByStyle(ToStringStyle.FloatMaxOne, ToStringNumberSense.Offset)));
            float work = f.GetStatValueAbstract(StatDefOf.WorkToBuild, stuff);
            parts.Add(work <= 300f ? "CC_Floor_Quick".Translate() : work >= 1500f ? "CC_Floor_Slow".Translate() : "CC_Floor_Medium".Translate());
            return string.Join("  ·  ", parts);
        }

        private string Tip(TerrainDef f, ThingDef stuff)
        {
            var lines = new List<string> { GenLabel.ThingLabel(f, stuff).CapitalizeFirst() };
            if (!f.description.NullOrEmpty()) lines.Add(f.description);
            if (Locked(f))
            {
                var missing = f.researchPrerequisites?.Where(r => !r.IsFinished).Select(r => r.LabelCap.ToString()).ToList();
                lines.Add("");
                lines.Add(missing != null && missing.Count > 0 ? "CC_Floor_NeedsResearch".Translate(string.Join(", ", missing)).ToString() : "CC_Floor_CantBuild".Translate().ToString());
            }
            if (f.MadeFromStuff && stuff == null) return string.Join("\n", lines);
            var cost = f.CostListAdjusted(stuff, false);
            if (cost.Count > 0)
            {
                lines.Add("");
                foreach (var c in cost)
                    lines.Add("CC_Floor_CostLine".Translate(c.count * area, c.thingDef.label, Have(c.thingDef)));
            }
            return string.Join("\n", lines);
        }

        // ---------------- Families ----------------

        private static List<Family> BuildFamilies(List<TerrainDef> floors, Map map)
        {
            // Designs the game groups in one Architect dropdown belong together; so do floors without a dropdown
            // whose labels differ only in their first word ("granite tile", "marble tile"; "oak plank floor", ...).
            string Key(TerrainDef f)
            {
                if (f.designatorDropdown != null) return "dd:" + f.designatorDropdown.defName;
                string stem = Stem(f.label);
                if (stem != null && floors.Count(o => o.designatorDropdown == null && Stem(o.label) == stem) > 1) return "stem:" + stem;
                return "floor:" + f.defName;
            }
            var result = new List<Family>();
            foreach (var group in floors.GroupBy(Key))
            {
                var members = group
                    .OrderByDescending(f => Avail.CanBuild(f, map) ? 1 : 0)
                    .ThenByDescending(f => Stock(f, map))
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
                    fam.label = dd != null && !dd.label.NullOrEmpty() ? dd.label : !suffix.NullOrEmpty() ? suffix : members[0].label;
                    foreach (var m in members)
                    {
                        string s = m.label;
                        if (!suffix.NullOrEmpty() && s.Length > suffix.Length && s.EndsWith(suffix)) s = s.Substring(0, s.Length - suffix.Length).Trim();
                        fam.shortLabels[m] = s.CapitalizeFirst();
                    }
                    // Two designs that would read the same: fall back to their full labels.
                    if (fam.shortLabels.Values.Distinct().Count() < members.Count)
                        foreach (var m in members) fam.shortLabels[m] = m.LabelCap;
                }
                result.Add(fam);
            }
            return result
                .OrderByDescending(f => f.members.Any(m => Avail.CanBuild(m, map)) ? 1 : 0)
                .ThenBy(f => f.members.Min(m => m.GetStatValueAbstract(StatDefOf.WorkToBuild)))
                .ThenBy(f => f.label)
                .ToList();
        }

        /// <summary>How many rooms' worth of the scarcest material is in stock (best material for floors made of one).</summary>
        private static int Stock(TerrainDef f, Map map)
        {
            var stuff = f.MadeFromStuff ? MaterialPicker.Pick(f, map, 1, MaterialUse.Floor) : null;
            if (f.MadeFromStuff && stuff == null) return 0;
            return f.CostListAdjusted(stuff, false).Select(c => MaterialCounts.Have(map, c.thingDef) / Mathf.Max(1, c.count)).DefaultIfEmpty(int.MaxValue).Min();
        }

        /// <summary>The label without its first word, if what's left is specific enough to group by ("tile" yes, "floor" no).</summary>
        private static string Stem(string label)
        {
            if (label.NullOrEmpty()) return null;
            var words = label.ToLowerInvariant().Split(' ');
            if (words.Length < 2) return null;
            string stem = string.Join(" ", words.Skip(1));
            return stem == "floor" || stem == "floors" || stem == "flooring" ? null : stem;
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
