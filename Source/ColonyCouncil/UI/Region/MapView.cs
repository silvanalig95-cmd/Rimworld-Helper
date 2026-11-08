using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Region → Map (docs/design/interface.md, "The region map as the hub"): the lens bar (Land, Stance and ties,
    /// Fortunes, Threats, Trade, Ours) and Map or List along the top, then the region map read through the lens
    /// (UI/RegionMapView.cs) or the lens's List: its rows in groups, nearest first within each (or all in one, closest
    /// first, with its chip), every row opening its card in the drawer, its names links, "Show" picking it on the map, and
    /// what came before at the foot. The lens's model
    /// (LensModel) is built at most once a second; rows are laid out once per width and drawn only in sight.
    /// </summary>
    public static class MapView
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static readonly Folds folds = new Folds("past");
        private const float RowH = 44f;
        private static AlmanacArticleDef article;

        /// <summary>The view in its rect: the lens bar and the modes, then the map or the List.</summary>
        public static void Draw(Rect rect, RegionMapView map)
        {
            var model = LensModel.Get(RegionView.lens);
            // The "?" to the region's Almanac article at the right, as on every view.
            float right = rect.xMax;
            article ??= DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(PagesMath.ArticleOf(RegionView.Map) ?? "");
            if (article != null)
            {
                UiKit.HelpButton(new Rect(right - 24f, rect.y + 1f, 24f, 24f), article);
                right -= 30f;
            }
            float mw = Mathf.Min(UiKit.ModesWidth(RegionView.MapModes, RegionView.MapModeIcons), rect.width * 0.4f);
            int mode = UiKit.Modes(new Rect(right - mw, rect.y, mw, UiKit.ModesHeight), RegionView.MapModes, RegionView.list ? 1 : 0, RegionView.MapModeIcons,
                RegionView.MapModeTips);
            if (mode >= 0)
            {
                RegionView.list = mode == 1;
                scroll = Vector2.zero;
            }
            int lens = UiKit.LensBar(new Rect(rect.x, rect.y, right - mw - 12f - rect.x, UiKit.ModesHeight), RegionView.LensLabels, RegionView.LensIcons, (int)RegionView.lens,
                RegionView.LensTips);
            if (lens >= 0)
            {
                RegionView.lens = RegionViewsMath.Lenses[lens];
                model = LensModel.Get(RegionView.lens);
                scroll = Vector2.zero;
            }
            var body = new Rect(rect.x, rect.y + UiKit.ModesHeight + 6f, rect.width, rect.height - UiKit.ModesHeight - 6f);
            if (RegionView.list) List(body, model);
            else map.Draw(body, model);
        }

        // ================= The List =================

        /// <summary>The List's rows all in one, closest first, made once for each model (the "Closest first" chip).</summary>
        private static readonly List<LensRow> closest = new List<LensRow>();
        private static readonly List<LensGroup> NoGroups = new List<LensGroup>();
        private static LensModel closestFor;
        private static string closestLabel, closestTip;

        private static List<LensRow> Closest(LensModel m)
        {
            if (closestFor == m) return closest;
            closestFor = m;
            closest.Clear();
            foreach (var g in m.groups) closest.AddRange(g.rows);
            closest.Sort((a, b) => RegionViewsMath.CompareClosest(a.order, b.order));
            return closest;
        }

        /// <summary>
        /// A lens's List: its groups with their counts, or (the "Closest first" chip) all its rows in one, nearest first; each
        /// row a thing with its card one click away; what came before at the foot.
        /// </summary>
        public static void List(Rect rect, LensModel m)
        {
            Text.Font = GameFont.Small;
            closestLabel ??= "CC_Region_ClosestFirst".Translate();
            closestTip ??= "CC_Region_ClosestFirst_Tip".Translate();
            float cw = UiKit.FilterChipWidth(closestLabel);
            if (UiKit.FilterChip(new Rect(rect.xMax - cw, rect.y, cw, 18f), closestLabel, RegionView.closest, closestTip))
            {
                RegionView.closest = !RegionView.closest;
                scroll = Vector2.zero;
            }
            UiKit.LabelFit(new Rect(rect.x, rect.y, rect.width - cw - 8f, 18f), m.line, UiKit.Faint, GameFont.Tiny);
            var outRect = new Rect(rect.x, rect.y + 22f, rect.width, rect.height - 22f);
            if (m.rows == 0 && m.past.Count == 0)
            {
                UiKit.EmptyState(new Rect(outRect.x, outRect.y, outRect.width, Mathf.Min(140f, outRect.height)), RegionView.LensIcons[(int)m.lens], m.empty);
                return;
            }
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f, w = view.width;
                if (m.rows == 0) y += UiKit.EmptyState(0f, y, w, RegionView.LensIcons[(int)m.lens], m.empty, 70f);
                int i = 0;
                if (RegionView.closest && m.rows > 0)
                {
                    foreach (var r in Closest(m))
                    {
                        // Only the rows in sight are drawn.
                        if (y + RowH >= scroll.y && y <= scroll.y + outRect.height) Row(new Rect(0f, y, w, RowH), r, i);
                        y += RowH;
                        i++;
                    }
                    y += 10f;
                }
                foreach (var g in RegionView.closest && m.rows > 0 ? NoGroups : m.groups)
                {
                    y += UiKit.SectionHeading(0f, y, w, g.title, g.rows.Count, folds, g.key, out bool open);
                    if (!open) continue;
                    foreach (var r in g.rows)
                    {
                        // Only the rows in sight are drawn.
                        if (y + RowH >= scroll.y && y <= scroll.y + outRect.height) Row(new Rect(0f, y, w, RowH), r, i);
                        y += RowH;
                        i++;
                    }
                    y += 10f;
                }
                if (m.past.Count > 0)
                {
                    y += UiKit.SectionHeading(0f, y, w, m.pastTitle, m.past.Count, folds, "past", out bool open);
                    if (open)
                        foreach (var line in m.past)
                            y += LinkText.Draw(new Rect(4f, y, w - 8f, 999f), line, Color.white) + 4f;
                }
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// A row: its picture, its name as a link with its state, a second line, a note on the right with its act and "Show";
        /// a click anywhere else on it opens its card in the drawer.
        /// </summary>
        public static void Row(Rect r, LensRow row, int index)
        {
            UiKit.Zebra(r, index);
            bool open = !row.thing.IsNone && Drawer.Current(RimfolkWindow.World) == row.thing;
            if (open) Widgets.DrawBoxSolid(r, UiKit.CardSelected);
            else Widgets.DrawHighlightIfMouseover(r);
            var pic = new Rect(r.x + 6f, r.y + 6f, 32f, 32f);
            UiKit.Picture(pic, row.pawn, row.faction, row.icon, row.tone);
            float right = r.xMax - 4f;
            if (row.place != null)
            {
                var show = new Rect(right - 26f, r.y + 9f, 26f, 26f);
                if (UiKit.IconButton(show, Icons.ModeMap, "CC_Lens_ShowTip".Translate())) Cards.ShowOnMap(row.place);
                right = show.x - 6f;
            }
            if (row.act != null)
            {
                float aw = UiKit.ActButtonWidth(row.act.label);
                if (UiKit.ActButton(new Rect(right - aw, r.y + 9f, aw, 26f), row.act.label, row.act.why, row.act.tip))
                {
                    try
                    {
                        row.act.run?.Invoke();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("region list: an act", e);
                    }
                    LensModel.Dirty();
                }
                right -= aw + 8f;
            }
            if (!row.distanceDone)
            {
                row.distanceDone = true;
                row.distance = row.note ?? (row.tile.Valid ? CardWords.DistanceShort(row.tile) : null);
            }
            if (!row.distance.NullOrEmpty())
            {
                float nw = Mathf.Min(UiKit.KeptWidth(row.distance, GameFont.Tiny) + 6f, (right - r.x) * 0.3f);
                UiKit.LabelFit(new Rect(right - nw, r.y, nw, r.height), row.distance, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= nw + 8f;
            }
            float x = pic.xMax + 10f, tw = Mathf.Max(0f, right - x);
            float nameW = Mathf.Min(LinkText.Width(row.name) + 4f, tw * 0.6f);
            LinkText.Line(new Rect(x, r.y + 3f, nameW, 22f), row.name, UiKit.HeaderColor);
            float cx = x + nameW + 6f;
            if (!row.state.NullOrEmpty() && cx < right - 30f)
                cx += UiKit.StateChip(cx, r.y + 4f, row.state, row.stateColor, right - cx) + 4f;
            if (row.moved != 0 && cx < right - 14f)
                UiKit.Icon(new Rect(cx, r.y + 8f, 12f, 12f), row.moved > 0 ? Icons.TrendUp : Icons.TrendDown, row.moved > 0 ? UiKit.Good : UiKit.Bad);
            if (!row.line.NullOrEmpty()) LinkText.Line(new Rect(x, r.y + 24f, tw, 18f), row.line, UiKit.Faint, GameFont.Tiny);
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(x, r.y + 22f, tw, 20f), row.tip);
            // Last, so the links, the act and "Show" keep their own clicks.
            if (!row.thing.IsNone && Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Drawer.Open(RimfolkWindow.World, row.thing, true);
            }
        }
    }
}
