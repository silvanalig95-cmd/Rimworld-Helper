using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The "rooms" button in the bottom bar: every room plan in one compact, architect-style popup, grouped
    /// into columns. Click a room to plan it, right-click to plan it at a good spot. Rooms the advisor is
    /// currently asking for are marked.
    /// </summary>
    public class MainTabWindow_Rooms : MainTabWindow
    {
        private static bool onlyAvailable = true;
        private Vector2 scroll;

        private const int Columns = 3;
        private const float ColW = 210f;
        private const float Gap = 10f;
        private const float RowH = 28f;
        private const float HeaderH = 26f;
        private const float TopH = 34f;
        private const float BottomH = 22f;

        private static readonly Color Faint = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color Suggested = new Color(1f, 0.85f, 0.4f);

        private struct Block
        {
            public string group;
            public List<RoomTemplateDef> rooms;
            public float Height => HeaderH + rooms.Count * RowH + 6f;
        }

        protected override float Margin => 10f;

        public override Vector2 RequestedTabSize
        {
            get
            {
                float h = Layout(Find.CurrentMap, out _).Max();
                return new Vector2(Columns * ColW + (Columns - 1) * Gap + Margin * 2f + 16f, Mathf.Clamp(TopH + h + BottomH + Margin * 2f, 200f, UI.screenHeight - 35f - 80f));
            }
        }

        /// <summary>Opens on the side of the screen its button is on.</summary>
        public override MainTabWindowAnchor Anchor
        {
            get
            {
                var visible = DefDatabase<MainButtonDef>.AllDefsListForReading.Where(b => b.buttonVisible).OrderBy(b => b.order).ToList();
                int i = def != null ? visible.IndexOf(def) : -1;
                return i >= 0 && i < visible.Count / 2 ? MainTabWindowAnchor.Left : MainTabWindowAnchor.Right;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 300f, 32f), "CC_Rooms_Title".Translate());
            Text.Font = GameFont.Small;
            bool before = onlyAvailable;
            Widgets.CheckboxLabeled(new Rect(inRect.xMax - 250f, inRect.y + 4f, 250f, 24f), "CC_Build_OnlyAvailable".Translate(), ref onlyAvailable);
            if (before != onlyAvailable) SetInitialSizeAndPosition();

            var suggested = SuggestedRooms(map);
            var heights = Layout(map, out var columns);
            var outRect = new Rect(inRect.x, inRect.y + TopH, inRect.width, inRect.height - TopH - BottomH);
            var viewRect = new Rect(0f, 0f, outRect.width - (heights.Max() > outRect.height ? 16f : 0f), heights.Max());
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            for (int c = 0; c < Columns; c++)
            {
                float x = c * (ColW + Gap);
                float y = 0f;
                foreach (var block in columns[c])
                {
                    string key = "CC_BuildGroup_" + block.group;
                    GUI.color = Faint;
                    Widgets.Label(new Rect(x, y, ColW, HeaderH), key.CanTranslate() ? key.Translate().ToString() : block.group ?? "");
                    GUI.color = Color.white;
                    Widgets.DrawLineHorizontal(x, y + HeaderH - 3f, ColW);
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
            GUI.color = Color.white;
        }

        private static void DrawRoom(Rect r, RoomTemplateDef t, Map map, string suggestedBecause)
        {
            t.ResolveCached(map, out string reason);
            bool ok = reason == null;
            Widgets.DrawHighlightIfMouseover(r);
            if (suggestedBecause != null) Widgets.DrawBoxSolid(r, new Color(1f, 0.85f, 0.4f, 0.12f));
            GUI.color = ok ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.height - 4f, r.height - 4f), t.Icon);
            GUI.color = !ok ? Faint : suggestedBecause != null ? Suggested : Color.white;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + r.height + 4f, r.y, r.width - r.height - 4f, r.height), t.LabelCap.ToString().Truncate(r.width - r.height - 8f));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(r, () => Tip(t, map, reason, suggestedBecause), t.GetHashCode());
            if (!Widgets.ButtonInvisible(r)) return;
            if (!ok)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (Event.current.button == 1)
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("CC_Build_Plan".Translate(), () => Designator_PlanRoom.Begin(t, false)),
                    new FloatMenuOption("CC_Plan_FindSpot".Translate(), () => Designator_PlanRoom.Begin(t, true))
                }));
                return;
            }
            Designator_PlanRoom.Begin(t, false);
        }

        private static string Tip(RoomTemplateDef t, Map map, string reason, string suggestedBecause)
        {
            var lines = new List<string> { t.LabelCap + "  (" + t.size.x + "×" + t.size.z + ")" };
            string desc = t.purpose ?? t.description;
            if (!desc.NullOrEmpty()) lines.Add(desc);
            var furniture = t.ResolveCached(map, out _);
            if (furniture.Count > 0)
                lines.Add("CC_Build_Contains".Translate(string.Join(", ", furniture.GroupBy(f => f.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label))));
            if (suggestedBecause != null) lines.Add("\n" + "CC_Rooms_Suggested".Translate(suggestedBecause).Colorize(Suggested));
            if (reason != null) lines.Add("\n" + reason.Colorize(new Color(1f, 0.5f, 0.45f)));
            string tip = Flavor.PickTip(t.tips, 0);
            if (!tip.NullOrEmpty()) lines.Add("\n" + tip);
            return string.Join("\n", lines);
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
                if (t != null && s.Level != AdviceLevel.Idea && !result.ContainsKey(t)) result[t] = s.title ?? s.def.label;
            }
            return result;
        }

        /// <summary>Groups packed into columns, each group going to the shortest column so far.</summary>
        private static float[] Layout(Map map, out List<Block>[] columns)
        {
            columns = new List<Block>[Columns];
            for (int i = 0; i < Columns; i++) columns[i] = new List<Block>();
            var heights = new float[Columns];
            if (map == null) return heights;
            var blocks = DefDatabase<RoomTemplateDef>.AllDefsListForReading
                .Where(t => t.architect && (!onlyAvailable || t.UnavailableReason(map) == null))
                .GroupBy(t => t.group)
                .OrderBy(g => GroupOrder(g.Key))
                .Select(g => new Block { group = g.Key, rooms = g.OrderBy(t => t.order).ThenBy(t => t.label).ToList() })
                .ToList();
            foreach (var b in blocks)
            {
                int best = 0;
                for (int i = 1; i < Columns; i++)
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
                default: return 6;
            }
        }
    }
}
