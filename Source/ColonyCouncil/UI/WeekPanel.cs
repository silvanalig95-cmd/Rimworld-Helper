using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → This week, the council window's first page: what needs the player (wishes and petitions, letters
    /// waiting for an answer, promises at risk), what's coming (gatherings and plans, days on the calendar, fights,
    /// missions, a camp), and how people are (strain and breaks, serious illness and recovery, trust, the mood balance
    /// of Rimfolk's own moods). Each line goes to its page, or to the colonist or the place on the map; colonists and
    /// wishes can be pinned to the top. The model (<see cref="WeekModel"/>) is built at most once a second, so drawing
    /// asks nothing of the game and allocates nothing per frame.
    /// </summary>
    public static class WeekPanel
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static WeekModel model;
        private static float modelAt = -1f;
        private static float placedWidth = -1f;
        private static WeekModel placedModel;

        /// <summary>Something changed (a pin, letters put away): the next draw builds the page again.</summary>
        public static void Dirty() => modelAt = -1f;

        public static WeekModel Model(CouncilGameComponent comp, Map map)
        {
            if (model == null || modelAt < 0f || Time.realtimeSinceStartup - modelAt > 1f)
            {
                modelAt = Time.realtimeSinceStartup;
                try
                {
                    model = WeekModel.Build(comp, map);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("this week", e);
                    model ??= new WeekModel { title = "CC_Week_Header".Translate(), sub = "CC_UI_Sub_Week".Translate() };
                }
            }
            return model;
        }

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            var m = Model(comp, map);
            float top = UiKit.PageHeader(rect, Icons.Week ?? Icons.Overview, m.title, m.sub, m.stat, m.statColor, 0f, m.headerTip);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                Place(m, w);
                int cols = CouncilLayoutMath.Columns(w);
                float cw = CouncilLayoutMath.ColumnWidth(w, cols);
                float bottom = 0f;
                var cards = m.cards;
                for (int i = 0; i < cards.Count; i++)
                {
                    var c = cards[i];
                    float x = c.column * (cw + CouncilLayoutMath.ColumnGap);
                    // Only cards in sight are drawn.
                    if (c.y + c.height >= scroll.y && c.y <= scroll.y + outRect.height) DrawCard(new Rect(x, c.y, cw, c.height), c);
                    bottom = Mathf.Max(bottom, c.y + c.height);
                }
                height = Mathf.Max(bottom + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>Each card into the shorter column, worked out again only when the model or the width changes.</summary>
        private static void Place(WeekModel m, float width)
        {
            if (placedModel == m && Mathf.Abs(placedWidth - width) < 0.5f) return;
            placedModel = m;
            placedWidth = width;
            int cols = CouncilLayoutMath.Columns(width);
            var heights = new float[m.cards.Count];
            for (int i = 0; i < heights.Length; i++)
            {
                var c = m.cards[i];
                c.shown = CouncilLayoutMath.ShownRows(c.rows.Count);
                c.height = CouncilLayoutMath.CardHeight(c.rows.Count);
                heights[i] = c.height;
            }
            var into = CouncilLayoutMath.Place(heights, cols);
            var used = new float[cols];
            for (int i = 0; i < heights.Length; i++)
            {
                var c = m.cards[i];
                c.column = into[i];
                c.y = used[c.column];
                used[c.column] += c.height + CouncilLayoutMath.ColumnGap;
            }
        }

        private static void DrawCard(Rect r, WeekCard c)
        {
            UiKit.Card(r, UiKit.WithAlpha(c.color, 0.55f), false, false);
            // The heading: icon, title, a note on the right, and the card's action; a click opens its page.
            float right = r.xMax - 8f;
            if (c.action != null && c.actionLabel != null)
            {
                Text.Font = GameFont.Tiny;
                float bw = Text.CalcSize(c.actionLabel).x + 16f;
                Text.Font = GameFont.Small;
                var br = new Rect(right - bw, r.y + 5f, bw, 20f);
                Text.Font = GameFont.Tiny;
                bool clicked = Widgets.ButtonText(br, c.actionLabel);
                Text.Font = GameFont.Small;
                if (c.actionTip != null) TooltipHandler.TipRegion(br, c.actionTip);
                if (clicked) c.action();
                right = br.x - 6f;
            }
            if (!c.right.NullOrEmpty())
            {
                float rw = Mathf.Min(150f, (right - r.x) * 0.4f);
                UiKit.LabelFit(new Rect(right - rw, r.y + 4f, rw, 22f), c.right, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= rw + 6f;
            }
            var titleRect = new Rect(r.x + 8f, r.y, Mathf.Max(0f, right - r.x - 8f), CouncilLayoutMath.CardHeader);
            UiKit.Icon(new Rect(titleRect.x + 2f, r.y + 7f, 16f, 16f), c.icon, c.color);
            UiKit.LabelFit(new Rect(titleRect.x + 24f, r.y + 3f, Mathf.Max(0f, titleRect.width - 24f), 24f), c.title, c.color);
            UiKit.Divider(r.x + 6f, r.y + CouncilLayoutMath.CardHeader - 2f, r.width - 12f);
            if (c.page != null)
            {
                if (Mouse.IsOver(titleRect)) Widgets.DrawHighlight(titleRect);
                TooltipHandler.TipRegion(titleRect, c.pageName);
                if (Widgets.ButtonInvisible(titleRect)) MainTabWindow_Council.OpenTab(c.page);
            }

            float y = r.y + CouncilLayoutMath.CardHeader;
            if (c.rows.Count == 0)
            {
                UiKit.LabelFit(new Rect(r.x + 12f, y, r.width - 20f, CouncilLayoutMath.RowHeight), c.empty, UiKit.Dim, GameFont.Tiny);
                return;
            }
            for (int i = 0; i < c.shown; i++)
            {
                DrawRow(new Rect(r.x + 4f, y, r.width - 8f, CouncilLayoutMath.RowHeight), c.rows[i], i);
                y += CouncilLayoutMath.RowHeight;
            }
            if (c.more != null)
            {
                var mr = new Rect(r.x + 4f, y, r.width - 8f, CouncilLayoutMath.RowHeight);
                if (c.page != null) Widgets.DrawHighlightIfMouseover(mr);
                UiKit.LabelFit(new Rect(mr.x + 26f, mr.y, mr.width - 30f, mr.height), c.more, UiKit.Faint, GameFont.Tiny);
                if (c.page != null)
                {
                    TooltipHandler.TipRegion(mr, c.pageName);
                    if (Widgets.ButtonInvisible(mr)) MainTabWindow_Council.OpenTab(c.page);
                }
            }
        }

        private static void DrawRow(Rect r, WeekRow row, int index)
        {
            UiKit.Zebra(r, index);
            bool over = Mouse.IsOver(r);
            if (over && row.click != null) Widgets.DrawHighlight(r);
            if (row.balance >= 0f)
            {
                DrawBalance(r, row);
                return;
            }
            float x = r.x + 2f;
            if (row.pawn != null)
            {
                UiKit.Portrait(new Rect(x, r.y + 2f, r.height - 4f, r.height - 4f), row.pawn, UiKit.TrustColor(TrustOf(row.pawn)));
                x += r.height + 2f;
            }
            else if (row.icon != null)
            {
                UiKit.Icon(new Rect(x + 3f, r.y + 5f, 16f, 16f), row.icon, UiKit.WithAlpha(row.color, 0.9f));
                x += 24f;
            }
            float right = r.xMax - 2f;
            // The pin: always when pinned, else under the mouse.
            bool pinnable = row.pinPawn != null || row.pinWish != null;
            if (pinnable)
            {
                if (PinButton(new Rect(right - 17f, r.y + 5f, 16f, 16f), row.pinned, over))
                {
                    TogglePin(row);
                    return;
                }
                right -= 22f;
            }
            if (!row.note.NullOrEmpty())
            {
                if (row.noteWidth < 0f)
                {
                    Text.Font = GameFont.Tiny;
                    row.noteWidth = Mathf.Ceil(Text.CalcSize(row.note).x) + 4f;
                    Text.Font = GameFont.Small;
                }
                float nw = Mathf.Min(row.noteWidth, (right - x) * 0.45f);
                UiKit.LabelFit(new Rect(right - nw, r.y, nw, r.height), row.note, row.noteColor, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= nw + 6f;
            }
            UiKit.LabelFit(new Rect(x, r.y, Mathf.Max(0f, right - x), r.height), row.text, row.color);
            // The line's tooltip leaves the pin to its own.
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(pinnable ? new Rect(r.x, r.y, r.width - 22f, r.height) : r, row.tip);
            // A right click offers the pin and the colonist's page.
            if (over && Event.current.type == EventType.MouseDown && Event.current.button == 1 && pinnable)
            {
                Event.current.Use();
                Menu(row);
                return;
            }
            if (row.click != null && Widgets.ButtonInvisible(r)) row.click();
        }

        /// <summary>Rimfolk's good moods against its bad ones, as one bar, with the sums either side.</summary>
        private static void DrawBalance(Rect r, WeekRow row)
        {
            float labelW = Mathf.Min(120f, r.width * 0.28f);
            UiKit.LabelFit(new Rect(r.x + 4f, r.y, labelW, r.height), row.balanceBad, MoodView.BadColor, GameFont.Tiny);
            UiKit.LabelFit(new Rect(r.xMax - labelW - 4f, r.y, labelW, r.height), row.balanceGood, MoodView.GoodColor, GameFont.Tiny, TextAnchor.MiddleRight);
            var bar = new Rect(r.x + labelW + 10f, r.y + 8f, Mathf.Max(10f, r.width - labelW * 2f - 20f), 10f);
            Widgets.DrawBoxSolidWithOutline(bar, UiKit.MeterBack, new Color(1f, 1f, 1f, 0.1f), 1);
            float split = bar.width * (1f - Mathf.Clamp01(row.balance));
            if (split > 1f) Widgets.DrawBoxSolid(new Rect(bar.x + 1f, bar.y + 1f, split - 1f, bar.height - 2f), UiKit.WithAlpha(MoodView.BadColor, 0.75f));
            if (bar.width - split > 1f) Widgets.DrawBoxSolid(new Rect(bar.x + split, bar.y + 1f, bar.width - split - 1f, bar.height - 2f), UiKit.WithAlpha(MoodView.GoodColor, 0.75f));
            Widgets.DrawBoxSolid(new Rect(bar.center.x - 0.5f, bar.y - 2f, 1f, bar.height + 4f), new Color(1f, 1f, 1f, 0.5f));
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, row.tip);
            if (row.click != null && Widgets.ButtonInvisible(r)) row.click();
        }

        private static float TrustOf(Pawn p) => CouncilGameComponent.Instance?.standings.TrustOf(p) ?? 0f;

        /// <summary>
        /// The pin to This week, for other pages (a wish card, the roster): gold when pinned, faint under the mouse,
        /// hidden otherwise. Returns true when clicked; the caller toggles the pin.
        /// </summary>
        public static bool PinButton(Rect r, bool pinned, bool show)
        {
            if (!pinned && !show) return false;
            UiKit.Icon(r, Icons.Pin ?? Icons.Promise, pinned ? UiKit.Gold : Mouse.IsOver(r) ? Color.white : UiKit.Dim);
            TooltipHandler.TipRegion(r, pinned ? PinTips.Unpin : PinTips.Pin);
            if (!Widgets.ButtonInvisible(r)) return false;
            (pinned ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn).PlayOneShotOnCamera();
            return true;
        }

        private static void TogglePin(WeekRow row)
        {
            var memory = WeekMemory.Instance;
            if (memory == null) return;
            if (row.pinWish != null) memory.TogglePin(row.pinWish);
            else memory.TogglePin(row.pinPawn);
        }

        private static void Menu(WeekRow row)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(row.pinned ? PinTips.Unpin : PinTips.Pin, () => TogglePin(row))
            };
            var p = row.pinPawn ?? row.pinWish?.pawn;
            if (p != null) options.Add(new FloatMenuOption("CC_Week_OpenPage".Translate(p.LabelShort).Resolve(), () => MainTabWindow_Council.OpenPerson(p)));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    /// <summary>The pin's words, translated once.</summary>
    internal static class PinTips
    {
        private static string pin, unpin;
        public static string Pin => pin ??= "CC_Week_Pin".Translate();
        public static string Unpin => unpin ??= "CC_Week_Unpin".Translate();
    }
}
