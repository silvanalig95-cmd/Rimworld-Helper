using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// What a view's header carries besides what its page says of itself: its modes (the same things shown another
    /// way), the one lit, what a click on another does, and its Almanac article for the "?". A window makes one per view
    /// and keeps it; its labels change only when their counts do.
    /// </summary>
    public sealed class ViewHeadModel
    {
        public readonly List<string> labels = new List<string>();
        public readonly List<string> tips = new List<string>();
        public int lit = -1;
        public Action<int> pick;
        /// <summary>The Almanac article the "?" opens (its defName), or null for no "?".</summary>
        public string article;

        public bool HasModes => labels.Count > 1;
    }

    /// <summary>
    /// The view header's extras on their way to the page: a window says what its view's header carries
    /// (<see cref="Draw"/>), and the page's own header (UiKit.PageHeader) takes them, so every page keeps its own title,
    /// line and stat and gains the modes and the "?" without knowing of them. A page that draws no header (an empty
    /// state) gets one from the window above it from the next frame on, so its modes stay in reach. Nothing here knows
    /// of Rimfolk's tab windows: any window can draw a view this way.
    /// </summary>
    public static class ViewHead
    {
        private static ViewHeadModel pending;
        private static bool taken;
        /// <summary>A header drawn by the window already: a page's own header takes this and adds nothing.</summary>
        private static readonly ViewHeadModel Nothing = new ViewHeadModel();
        /// <summary>The pages that drew no header of their own last time (the window draws one for them).</summary>
        private static readonly HashSet<string> headless = new HashSet<string>();

        /// <summary>The extras for the next page header drawn, or null; taking them is noted.</summary>
        internal static ViewHeadModel Take()
        {
            var m = pending;
            pending = null;
            if (m != null) taken = true;
            return m == Nothing ? null : m;
        }

        /// <summary>
        /// Draws a page as a view: the page in <paramref name="rect"/> with its header's extras, or, when it drew no
        /// header last time, a header from <paramref name="title"/> and <paramref name="icon"/> above it.
        /// </summary>
        public static void Draw(Rect rect, string key, string title, Texture2D icon, ViewHeadModel extras, Action<Rect> draw)
        {
            bool own = key != null && headless.Contains(key);
            var body = rect;
            if (own)
            {
                float h = UiKit.ViewHeader(rect, icon, title, null, null, null, 0f, null, extras);
                body = new Rect(rect.x, rect.y + h, rect.width, rect.height - h);
                pending = Nothing;
            }
            else pending = extras;
            taken = false;
            try
            {
                draw(body);
            }
            finally
            {
                // Settled once a frame, when it is painted: the page drew a header of its own, or it didn't.
                if (key != null && Event.current.type == EventType.Repaint)
                {
                    if (own && taken) headless.Remove(key);
                    else if (!own && !taken) headless.Add(key);
                }
                pending = null;
            }
        }
    }

    public static partial class UiKit
    {
        /// <summary>
        /// A view's header (docs/design/interface.md, "The look", component 1): its icon, title, one line saying what it
        /// is, the stat on the right and a "?" to its Almanac article beside it, then its modes as a row of small buttons
        /// (component 2), one lit. <paramref name="reserveRight"/> leaves the page's own controls their room on the
        /// right. Returns its height with the space below it.
        /// </summary>
        public static float ViewHeader(Rect rect, Texture2D icon, string title, string subtitle, string stat, Color? statColor, float reserveRight, string tip,
            ViewHeadModel extras)
        {
            float x = rect.x;
            if (icon != null)
            {
                Icon(new Rect(rect.x + 1f, rect.y + 6f, 32f, 32f), icon, Gold);
                x += ViewHeadMath.IconSpace;
            }
            float right = Mathf.Max(x, rect.xMax - reserveRight);
            var article = extras?.article == null ? null : DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(extras.article);
            if (article != null)
            {
                float hx = rect.x + ViewHeadMath.HelpX(rect.width, reserveRight, x - rect.x, out float r);
                HelpButton(new Rect(hx, rect.y + 6f, ViewHeadMath.Help, ViewHeadMath.Help), article);
                right = rect.x + r;
            }
            float statW = 0f;
            if (!stat.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                statW = Mathf.Min(Text.CalcSize(stat).x + 6f, (right - x) * 0.45f);
                LabelFit(new Rect(right - statW, rect.y + 4f, statW, 26f), stat, statColor ?? Gold, GameFont.Small, TextAnchor.MiddleRight);
                statW += 10f;
            }
            LabelFit(new Rect(x, rect.y + 1f, Mathf.Max(0f, right - statW - x), 32f), title, Color.white, GameFont.Medium);
            float lineRight = article != null ? right + ViewHeadMath.HelpGap + ViewHeadMath.Help : right;
            // With more than one colony, the colony in view in a chip at the end of the line, a click away from another (UI/ColonyView.cs).
            float chip = ColonyView.Chip(lineRight, rect.y + 30f, Mathf.Max(0f, (lineRight - x) * 0.45f));
            if (chip > 0f) lineRight -= chip + 8f;
            if (!subtitle.NullOrEmpty()) LabelFit(new Rect(x, rect.y + 29f, Mathf.Max(0f, lineRight - x), 22f), subtitle, Faint);
            bool modes = extras != null && extras.HasModes;
            float height = ViewHeadMath.Height(modes);
            if (modes) ModeButtons(x, rect.y + ViewHeadMath.ModesY, rect.xMax - x, extras);
            float lineY = rect.y + height - 5f;
            Divider(rect.x, lineY, rect.width);
            Widgets.DrawBoxSolid(new Rect(rect.x, lineY - 1f, Mathf.Min(rect.width, 34f), 2f), WithAlpha(Gold, 0.8f));
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(rect.x, rect.y, Mathf.Max(0f, right - rect.x), 50f), tip);
            Text.Font = GameFont.Small;
            return height;
        }

        // Each mode's measured width, by its label (measured once).
        private static readonly Dictionary<string, float> modeWidths = new Dictionary<string, float>();
        private static float[] modeNatural = new float[0], modeX = new float[0], modeW = new float[0];

        /// <summary>The modes as a row of small buttons from (x, y) in a width: the lit one gold and underlined, a click on another picks it.</summary>
        public static void ModeButtons(float x, float y, float width, ViewHeadModel m)
        {
            int n = m.labels.Count;
            if (modeNatural.Length < n)
            {
                modeNatural = new float[n];
                modeX = new float[n];
                modeW = new float[n];
            }
            var natural = modeNatural;
            Text.Font = GameFont.Small;
            for (int i = 0; i < n; i++)
            {
                string label = m.labels[i] ?? "";
                if (!modeWidths.TryGetValue(label, out float w))
                {
                    if (modeWidths.Count > 200) modeWidths.Clear();
                    modeWidths[label] = w = ViewHeadMath.ModeWidth(Text.CalcSize(label).x);
                }
                natural[i] = w;
            }
            ViewHeadMath.Modes(x, width, natural, n, modeX, modeW);
            for (int i = 0; i < n; i++)
            {
                if (modeW[i] < 8f) continue;
                var r = new Rect(modeX[i], y, modeW[i], ViewHeadMath.ModeH);
                bool lit = i == m.lit;
                bool over = Mouse.IsOver(r);
                Widgets.DrawBoxSolid(r, lit ? CardSelected : over ? CardHover : CardFill);
                if (lit) Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 2f), Gold);
                LabelFit(r, m.labels[i], lit ? Gold : over ? Color.white : Faint, GameFont.Small, TextAnchor.MiddleCenter);
                string tip = i < m.tips.Count ? m.tips[i] : null;
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
                if (!lit && Widgets.ButtonInvisible(r, false))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    m.pick?.Invoke(i);
                }
            }
        }

        private static string helpTipFor;
        private static string helpTip;

        /// <summary>The "?" that opens an Almanac article in the Almanac's own window, beside what it explains.</summary>
        public static void HelpButton(Rect r, AlmanacArticleDef article)
        {
            bool over = Mouse.IsOver(r);
            Widgets.DrawBoxSolidWithOutline(r, over ? CardHover : CardFill, over ? WithAlpha(Gold, 0.7f) : CardBorder);
            LabelFit(r, "?", over ? Gold : Faint, GameFont.Small, TextAnchor.MiddleCenter);
            if (helpTipFor != article.defName)
            {
                helpTipFor = article.defName;
                helpTip = "CC_View_HelpTip".Translate(article.LabelCap.Named("PAGE")).Resolve();
            }
            TooltipHandler.TipRegion(r, helpTip);
            if (!Widgets.ButtonInvisible(r, false)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            Window_Almanac.Open(article);
        }
    }
}
