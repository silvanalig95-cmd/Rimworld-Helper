using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One line of the vitals or the council panel: an icon, what it is, its value, how much it matters, a click.</summary>
    public sealed class HudLine
    {
        /// <summary>The part's name, to switch it off (HudPanelState.hidden).</summary>
        public string part;
        public Texture2D icon;
        public string text;
        public string value;
        public string tip;
        public float score;
        public Action click;
        public Color quiet = UiKit.Faint;

        public HudLevel Level => HudRank.LevelOf(score);
        public Color Color => HudKit.ColorOf(score, quiet);
    }

    /// <summary>
    /// A panel of short lines (the vitals, the council at a glance): each with a small icon, turning orange or red by the
    /// shared ranking, the ones that matter moved to the top when something does, and cut to the panel's rows with
    /// "and 3 more" (never the critical ones). Compact: icons and values only, two to a row.
    /// </summary>
    public abstract class HudLinesPanel : HudPanel
    {
        protected readonly List<HudLine> lines = new List<HudLine>();
        private readonly List<HudLine> shown = new List<HudLine>();
        private int hidden;
        private string moreText;

        protected HudLinesPanel(string id) : base(id)
        {
        }

        /// <summary>Fill <see cref="lines"/> (every part, the switched-off ones included).</summary>
        protected abstract void Gather();

        /// <summary>A quiet line shown when there's nothing to say, or null.</summary>
        protected virtual string Empty => null;

        /// <summary>Every part this panel has, for its options.</summary>
        public abstract string[] PartNames { get; }

        protected override void Refresh()
        {
            lines.Clear();
            Gather();
            var st = St;
            var visible = lines.Where(l => l != null && st.Shows(l.part)).ToList();
            // Calm: the panel's own order. Something wrong: what matters most goes first.
            if (visible.Any(l => l.score >= HudRank.SoonFloor)) visible = visible.OrderByDescending(l => l.score >= HudRank.SoonFloor ? l.score : 0f).ToList();
            var keep = HudMath.Keep(visible.Select(l => l.score).ToList(), st.maxRows, HudRank.LifeFloor, out hidden);
            shown.Clear();
            foreach (int i in keep) shown.Add(visible[i]);
            moreText = hidden > 0 ? "CC_Pnl_AndMore".Translate(hidden).Resolve() : null;
        }

        private static float RowH(float s) => Mathf.Round(20f * s);
        private static float CellW(float s) => Mathf.Round(96f * s);
        protected virtual float FullWidth => 232f;

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (shown.Count == 0) return new Vector2(Mathf.Round(190f * s), pad * 2f + RowH(s));
            if (St.compact)
            {
                int rows = (shown.Count + 1) / 2;
                return new Vector2(pad * 2f + CellW(s) * 2f + 2f, pad * 2f + rows * RowH(s) + more);
            }
            return new Vector2(Mathf.Round(FullWidth * s), pad * 2f + shown.Count * RowH(s) + more);
        }

        protected override void DrawBody(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            float rh = RowH(s);
            if (shown.Count == 0)
            {
                HudKit.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, rh), Empty ?? "", UiKit.Faint, HudKit.Minor(s));
                return;
            }
            var main = HudKit.Main(s);
            float icon = Mathf.Round(14f * s);
            float y = r.y + pad;
            bool compact = St.compact;
            for (int i = 0; i < shown.Count; i++)
            {
                var l = shown[i];
                Rect row;
                if (compact) row = new Rect(r.x + pad + (i % 2) * (CellW(s) + 2f), y + (i / 2) * rh, CellW(s), rh);
                else row = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var level = l.Level;
                if (level == HudLevel.Life) Widgets.DrawBoxSolid(row, UiKit.WithAlpha(UiKit.Bad, 0.12f + 0.1f * HudKit.Pulse));
                else if (level == HudLevel.Soon) Widgets.DrawBoxSolid(row, UiKit.WithAlpha(UiKit.Warn, 0.12f));
                if (Mouse.IsOver(row)) Widgets.DrawBoxSolid(row, UiKit.CardHover);
                var c = l.Color;
                float x = row.x + 2f;
                if (l.icon != null) UiKit.Icon(new Rect(x, row.y + (rh - icon) / 2f, icon, icon), l.icon, level >= HudLevel.Soon ? c : UiKit.WithAlpha(UiKit.HeaderColor, 0.85f));
                x += icon + Mathf.Round(5f * s);
                if (compact)
                {
                    HudKit.Label(new Rect(x, row.y, row.xMax - x - 2f, rh), l.value ?? l.text, level >= HudLevel.Soon ? c : Color.white, GameFont.Tiny);
                }
                else
                {
                    float vw = l.value.NullOrEmpty() ? 0f : Mathf.Min(HudKit.Width(l.value, main) + 4f, row.width * 0.45f);
                    if (vw > 0f) HudKit.Label(new Rect(row.xMax - vw - 2f, row.y, vw, rh), l.value, level >= HudLevel.Soon ? c : Color.white, main, TextAnchor.MiddleRight);
                    HudKit.Label(new Rect(x, row.y, Mathf.Max(0f, row.xMax - vw - 6f - x), rh), l.text, level >= HudLevel.Soon ? c : l.quiet, main);
                }
                var line = l;
                string full = compact ? l.text + (l.tip.NullOrEmpty() ? "" : "\n\n" + l.tip) : l.tip;
                if (!full.NullOrEmpty()) TooltipHandler.TipRegion(row, full);
                if (Widgets.ButtonInvisible(row, false))
                {
                    if (Event.current.button == 1) OpenMenu();
                    else
                        try
                        {
                            line.click?.Invoke();
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("on-screen panel click", e);
                        }
                }
            }
            if (moreText != null)
            {
                float my = compact ? y + (shown.Count + 1) / 2 * rh : y + shown.Count * rh;
                var mr = new Rect(r.x + pad, my, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(mr, HudKit.T("CC_Pnl_MoreLinesTip"));
            }
        }

        /// <summary>A line, with its click; null values are fine.</summary>
        protected HudLine Add(string part, Texture2D icon, string text, string value, float score, string tip, Action click)
        {
            var l = new HudLine { part = part, icon = icon, text = text, value = value, score = score, tip = tip, click = click };
            lines.Add(l);
            return l;
        }

        /// <summary>The click line under a tooltip, in grey.</summary>
        protected static string Tip(string body, string click) =>
            click.NullOrEmpty() ? body : (body.NullOrEmpty() ? "" : body + "\n\n") + click.Colorize(ColoredText.SubtleGrayColor);
    }
}
