using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// This week, the council window's first view and the one inbox (docs/design/interface.md, "This week: one inbox"):
    /// Following (what the player follows, each with its newest change), Needs you (wishes and petitions, letters waiting
    /// for an answer, the region's and diplomacy's asks, promises at risk), Under way (letters, caravans, missions,
    /// settlers, in steps, never a countdown), Coming (by date), How people are (strain, illness, trust, the mood
    /// balance) and the plan (the advisor's two most pressing steps). Each row is one sentence with its names as links,
    /// its act on the right; a click goes to its page, the colonist or the place; "and 3 more" opens the rest in place.
    /// Features add rows through Inbox.Register (UI/Inbox.cs). The model (<see cref="WeekModel"/>) is built at most once a
    /// second, so drawing asks nothing of the game and allocates nothing per frame.
    /// </summary>
    public static class WeekPanel
    {
        /// <summary>A row with a second line (what it is, its steps) under its sentence.</summary>
        public const float TallRow = 40f;

        private static Vector2 scroll;
        private static float height = 600f;
        private static WeekModel model;
        private static float modelAt = -1f;
        private static float placedWidth = -1f;
        private static WeekModel placedModel;
        /// <summary>The sections opened in place ("and 3 more"), kept for the session.</summary>
        private static readonly HashSet<InboxSection> opened = new HashSet<InboxSection>();

        /// <summary>Something changed (a pin, letters put away, a section opened): the next draw builds the page and the count again.</summary>
        public static void Dirty()
        {
            modelAt = -1f;
            Inbox.Dirty();
        }

        public static WeekModel Model(CouncilGameComponent comp, Map map)
        {
            if (model == null || modelAt < 0f || Time.realtimeSinceStartup - modelAt > 1f)
            {
                modelAt = Time.realtimeSinceStartup;
                try
                {
                    model = WeekModel.Build(comp, map, opened);
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
            float top = UiKit.PageHeader(rect, Icons.Inbox ?? Icons.Week ?? Icons.Overview, m.title, m.sub, m.stat, m.statColor, 0f, m.headerTip);
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

        private static float RowHeight(WeekRow r) => r.Tall ? TallRow : CouncilLayoutMath.RowHeight;

        /// <summary>A section's height: its heading, the rows that show, "and 3 more" (or "Show fewer"), its footer, or one line saying it's empty.</summary>
        private static float CardHeight(WeekCard c)
        {
            float h = CouncilLayoutMath.CardHeader + CouncilLayoutMath.CardPad;
            if (c.rows.Count == 0 && c.footer.Count == 0) return h + CouncilLayoutMath.RowHeight;
            for (int i = 0; i < c.shown; i++) h += RowHeight(c.rows[i]);
            if (c.more != null || c.less != null) h += CouncilLayoutMath.RowHeight;
            foreach (var r in c.footer) h += RowHeight(r);
            return h;
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
                c.shown = InboxMath.Shown(c.section, c.rows.Count, c.opened);
                c.height = CardHeight(c);
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
            UiKit.Card(r, null, false, false);
            // The section's heading: its icon in the section's colour, its name and its count; a click opens its view.
            var titleRect = new Rect(r.x + UiKit.CardInset - 2f, r.y + 4f, Mathf.Max(0f, r.width - 2f * UiKit.CardInset + 4f), 24f);
            UiKit.HeadingLine(titleRect.x, titleRect.y, titleRect.width, c.title, c.count > 0 ? c.count : -1, null, c.icon, c.color, false, false);
            if (c.page != null)
            {
                if (Mouse.IsOver(titleRect)) Widgets.DrawHighlight(titleRect);
                TooltipHandler.TipRegion(titleRect, c.pageName);
                if (Widgets.ButtonInvisible(titleRect)) MainTabWindow_Council.OpenTab(c.page);
            }

            float y = r.y + CouncilLayoutMath.CardHeader;
            if (c.rows.Count == 0 && c.footer.Count == 0)
            {
                UiKit.EmptyLine(new Rect(r.x + 8f, y + 1f, r.width - 16f, CouncilLayoutMath.RowHeight - 4f), c.empty);
                return;
            }
            int index = 0;
            for (int i = 0; i < c.shown && i < c.rows.Count; i++)
            {
                float h = RowHeight(c.rows[i]);
                DrawRow(new Rect(r.x + 4f, y, r.width - 8f, h), c.rows[i], index++);
                y += h;
            }
            string fold = c.more ?? c.less;
            if (fold != null)
            {
                // "and 3 more" opens the rest in place; "Show fewer" folds them again.
                var mr = new Rect(r.x + 4f, y, r.width - 8f, CouncilLayoutMath.RowHeight);
                Widgets.DrawHighlightIfMouseover(mr);
                UiKit.Icon(new Rect(mr.x + 8f, mr.y + (mr.height - 10f) / 2f, 10f, 10f), c.more != null ? TexButton.Reveal : TexButton.Collapse, UiKit.Dim);
                UiKit.LabelFit(new Rect(mr.x + 26f, mr.y, mr.width - 30f, mr.height), fold, UiKit.Faint, GameFont.Tiny);
                if (Widgets.ButtonInvisible(mr))
                {
                    if (!opened.Remove(c.section)) opened.Add(c.section);
                    (c.more != null ? SoundDefOf.TabOpen : SoundDefOf.TabClose).PlayOneShotOnCamera();
                    modelAt = -1f;
                }
                y += CouncilLayoutMath.RowHeight;
            }
            foreach (var row in c.footer)
            {
                float h = RowHeight(row);
                DrawRow(new Rect(r.x + 4f, y, r.width - 8f, h), row, index++);
                y += h;
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
            bool tall = row.Tall;
            float lineH = CouncilLayoutMath.RowHeight;
            float x = r.x + 2f;
            float pic = tall ? 30f : r.height - 4f;
            var face = new Rect(x, r.y + (r.height - pic) / 2f, pic, pic);
            if (row.pawn != null || row.faction != null)
            {
                UiKit.Picture(face, row.pawn, row.faction, row.icon, UiKit.WithAlpha(row.color, 0.9f), row.pawn != null ? UiKit.TrustColor(TrustOf(row.pawn)) : (Color?)null);
                // A portrait opens their card in the drawer beside the week; the rest of the line does what it says.
                var who = row.pawn != null ? ThingRefs.Of(row.pawn) : ThingRefs.Of(row.faction);
                if (!who.IsNone)
                {
                    if (Mouse.IsOver(face)) Previews.Hover(who, face);
                    if (Widgets.ButtonInvisible(face, false))
                    {
                        Cards.Click();
                        Cards.Open(who);
                        return;
                    }
                }
                x += pic + 4f;
            }
            else if (row.icon != null)
            {
                UiKit.Icon(new Rect(x + 3f, r.y + (tall ? 11f : (r.height - UiKit.IconRow) / 2f), UiKit.IconRow, UiKit.IconRow), row.icon, UiKit.WithAlpha(row.color, 0.9f));
                x += 26f;
            }
            float right = r.xMax - 2f;
            // The pin: always when followed, else under the mouse.
            if (row.Pinnable)
            {
                if (PinButton(new Rect(right - 17f, r.y + (tall ? 5f : (r.height - 16f) / 2f), 16f, 16f), row.pinned, over))
                {
                    TogglePin(row);
                    return;
                }
                right -= 22f;
            }
            // Its act on the right: "Answer", "Decide", "Look", "Pay".
            if (!row.act.NullOrEmpty() && row.actRun != null)
            {
                if (row.actWidth < 0f) row.actWidth = Mathf.Min(UiKit.RowActWidth(row.act), 110f);
                var ar = new Rect(right - row.actWidth, r.y + (r.height - 22f) / 2f, row.actWidth, 22f);
                if (UiKit.RowAct(ar, row.act, row.actWhy, row.actTip))
                {
                    row.actRun();
                    WeekPanel.Dirty();
                    return;
                }
                right = ar.x - 6f;
            }
            float textTop = tall ? r.y + 2f : r.y;
            // The second line runs under the note, up to the act.
            float secondRight = right;
            if (!row.note.NullOrEmpty())
            {
                if (row.noteWidth < 0f) row.noteWidth = UiKit.KeptWidth(row.note, GameFont.Tiny) + 4f;
                float nw = Mathf.Min(row.noteWidth, (right - x) * 0.45f);
                UiKit.LabelFit(new Rect(right - nw, textTop, nw, lineH), row.note, row.noteColor, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= nw + 6f;
            }
            var textRect = new Rect(x, textTop, Mathf.Max(0f, right - x), lineH);
            LinkText.Line(textRect, row.text, row.color);
            if (tall)
            {
                var second = new Rect(x, r.y + 21f, Mathf.Max(0f, secondRight - x), 17f);
                if (row.steps != null) UiKit.StepsLine(second, row.steps, row.step);
                else LinkText.Line(second, row.sub, row.subColor, GameFont.Tiny);
            }
            // The line's tooltip leaves the pin and the act to their own.
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(r.x, r.y, Mathf.Max(0f, right - r.x), r.height), row.tip);
            // A right click offers the pin and the card.
            if (over && Event.current.type == EventType.MouseDown && Event.current.button == 1 && row.Pinnable)
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
            UiKit.LabelFit(new Rect(r.x + 4f, r.y, labelW, r.height), row.balanceBad, UiKit.Sour, GameFont.Tiny);
            UiKit.LabelFit(new Rect(r.xMax - labelW - 4f, r.y, labelW, r.height), row.balanceGood, UiKit.Warm, GameFont.Tiny, TextAnchor.MiddleRight);
            var bar = new Rect(r.x + labelW + 10f, r.y + (r.height - 10f) / 2f, Mathf.Max(10f, r.width - labelW * 2f - 20f), 10f);
            Widgets.DrawBoxSolidWithOutline(bar, UiKit.MeterBack, UiKit.MeterEdge, 1);
            float split = bar.width * (1f - Mathf.Clamp01(row.balance));
            if (split > 1f) Widgets.DrawBoxSolid(new Rect(bar.x + 1f, bar.y + 1f, split - 1f, bar.height - 2f), UiKit.WithAlpha(UiKit.Sour, 0.75f));
            if (bar.width - split > 1f) Widgets.DrawBoxSolid(new Rect(bar.x + split, bar.y + 1f, bar.width - split - 1f, bar.height - 2f), UiKit.WithAlpha(UiKit.Warm, 0.75f));
            Widgets.DrawBoxSolid(new Rect(bar.center.x - 0.5f, bar.y - 2f, 1f, bar.height + 4f), UiKit.WithAlpha(UiKit.Ink, 0.5f));
            if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, row.tip);
            if (row.click != null && Widgets.ButtonInvisible(r)) row.click();
        }

        private static float TrustOf(Pawn p) => CouncilGameComponent.Instance?.standings.TrustOf(p) ?? 0f;

        /// <summary>
        /// The follow pin, for other pages (a wish card, the People table, a card's head): gold when followed, faint under
        /// the mouse, hidden otherwise. Returns true when clicked; the caller toggles it.
        /// </summary>
        public static bool PinButton(Rect r, bool pinned, bool show)
        {
            if (!pinned && !show) return false;
            UiKit.Icon(r, Icons.Pin ?? Icons.Promise, pinned ? UiKit.Gold : Mouse.IsOver(r) ? UiKit.Ink : UiKit.Dim);
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
            else memory.ToggleFollow(row.follow);
            Dirty();
        }

        private static void Menu(WeekRow row)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(row.pinned ? PinTips.Unpin : PinTips.Pin, () => TogglePin(row))
            };
            var thing = !row.follow.IsNone ? row.follow : ThingRefs.Of(row.pinWish?.pawn);
            if (!thing.IsNone) options.Add(new FloatMenuOption("CC_Week_OpenPage".Translate(ThingRefs.Name(thing)).Resolve(), () => Cards.Open(thing)));
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
