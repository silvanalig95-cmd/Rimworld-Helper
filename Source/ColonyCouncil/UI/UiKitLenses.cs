using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>One entry of a legend: a dot in its tone and a word, or a sample of a line and a word.</summary>
    public sealed class LegendItem
    {
        public string word;
        public Color color;
        /// <summary>A line's sample instead of a dot.</summary>
        public LineLook? line;
        /// <summary>A square swatch instead of a dot (the land's colours).</summary>
        public bool swatch;
        /// <summary>An icon instead of a dot (an arrow for a fortune that moved).</summary>
        public Texture2D icon;
        /// <summary>A ring instead of a dot (a town's head's regard around its mark).</summary>
        public bool ring;
        internal float width = -1f;
    }

    /// <summary>
    /// The interface design's map components (docs/design/interface.md, "The look"; the view's header and its modes are
    /// UI/UiKitViews.cs): a segmented Map or List switch, the lens bar, a legend at a map's foot or as a list, filter
    /// chips, the tones as colours, and a line drawn in its dashes. Each takes a rect and plain data, so any window can
    /// draw it (the region window's views are the first); widths are measured once per label and kept.
    /// </summary>
    public static partial class UiKit
    {
        // ---------------- Tones ----------------

        /// <summary>A tone's colour: warm, cool and sour stance, gold for ours, soon, dim, the world's.</summary>
        public static Color ToneColor(Hue t)
        {
            switch (t)
            {
                case Hue.Warm: return Good;
                case Hue.Cool: return Cool;
                case Hue.Sour: return Bad;
                case Hue.Gold: return Gold;
                case Hue.Soon: return Warn;
                case Hue.Dim: return WithAlpha(Dim, 0.55f);
                case Hue.World: return WorldColor;
                default: return Color.white;
            }
        }

        // ---------------- Widths, kept ----------------

        private static readonly Dictionary<string, float> smallWidths = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> tinyWidths = new Dictionary<string, float>();

        /// <summary>A label's width in a font, measured once and kept (labels are a short, fixed set).</summary>
        public static float KeptWidth(string label, GameFont font)
        {
            if (label.NullOrEmpty()) return 0f;
            var d = font == GameFont.Tiny ? tinyWidths : smallWidths;
            if (d.TryGetValue(label, out float w)) return w;
            var old = Text.Font;
            Text.Font = font;
            w = Text.CalcSize(label).x;
            Text.Font = old;
            if (d.Count > 400) d.Clear();
            d[label] = w;
            return w;
        }

        // ---------------- Modes ----------------

        /// <summary>Height of the modes' row and of the lens bar.</summary>
        public const float ModesHeight = 26f;

        /// <summary>How wide the modes are, each with its icon and word.</summary>
        public static float ModesWidth(IList<string> labels, IList<Texture2D> icons = null)
        {
            float w = 0f;
            for (int i = 0; i < labels.Count; i++) w += ModeWidth(labels[i], icons != null && i < icons.Count ? icons[i] : null);
            return w;
        }

        private static float ModeWidth(string label, Texture2D icon) => KeptWidth(label, GameFont.Small) + 18f + (icon != null ? 18f : 0f);

        /// <summary>
        /// A view's modes (the same things shown another way): small buttons side by side in one frame, the one showing lit.
        /// Returns the index clicked, or -1.
        /// </summary>
        public static int Modes(Rect r, IList<string> labels, int lit, IList<Texture2D> icons = null, IList<string> tips = null)
        {
            if (labels == null || labels.Count == 0) return -1;
            int clicked = -1;
            Widgets.DrawBoxSolidWithOutline(r, CardFill, CardBorder, 1);
            float x = r.x;
            float full = ModesWidth(labels, icons);
            float scale = full > r.width ? r.width / full : 1f;
            for (int i = 0; i < labels.Count; i++)
            {
                var icon = icons != null && i < icons.Count ? icons[i] : null;
                float w = ModeWidth(labels[i], icon) * scale;
                var b = new Rect(x, r.y, w, r.height);
                bool on = i == lit;
                bool over = Mouse.IsOver(b);
                if (on) Widgets.DrawBoxSolid(b.ContractedBy(1f), CardSelected);
                else if (over) Widgets.DrawBoxSolid(b.ContractedBy(1f), CardHover);
                float tx = b.x + 9f;
                var c = on ? Gold : over ? Color.white : Faint;
                if (icon != null)
                {
                    Icon(new Rect(tx, b.y + (b.height - 14f) / 2f, 14f, 14f), icon, c);
                    tx += 18f;
                }
                LabelFit(new Rect(tx, b.y, b.xMax - tx - 4f, b.height), labels[i], c);
                if (i > 0) Widgets.DrawBoxSolid(new Rect(b.x, b.y + 4f, 1f, b.height - 8f), CardBorder);
                if (tips != null && i < tips.Count && !tips[i].NullOrEmpty()) TooltipHandler.TipRegion(b, tips[i]);
                if (!on && Widgets.ButtonInvisible(b, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    clicked = i;
                }
                x += w;
            }
            return clicked;
        }

        // ---------------- The lens bar ----------------

        /// <summary>
        /// The lens bar: an icon and a name for each lens, the one in use lit with a line under it. Where the names don't all
        /// fit, the lens in use keeps its name and the others show their icon (their name on hover); narrower still, icons
        /// only. Returns the index clicked, or -1.
        /// </summary>
        public static int LensBar(Rect r, IList<string> labels, IList<Texture2D> icons, int lit, IList<string> tips = null)
        {
            if (labels == null || labels.Count == 0) return -1;
            const float pad = 10f, iconW = 16f, gap = 4f;
            float IconOnly() => pad * 2f + iconW;
            float Named(int i) => pad * 2f + iconW + 5f + KeptWidth(labels[i], GameFont.Small);
            float all = 0f, mixed = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                all += Named(i) + gap;
                mixed += (i == lit ? Named(i) : IconOnly()) + gap;
            }
            int mode = all <= r.width ? 0 : mixed <= r.width ? 1 : 2;
            int clicked = -1;
            float x = r.x;
            for (int i = 0; i < labels.Count; i++)
            {
                bool named = mode == 0 || mode == 1 && i == lit;
                float w = named ? Named(i) : mode == 2 ? Mathf.Min(IconOnly(), (r.width - gap * labels.Count) / labels.Count) : IconOnly();
                var b = new Rect(x, r.y, w, r.height);
                bool on = i == lit;
                bool over = Mouse.IsOver(b);
                if (on) Widgets.DrawBoxSolid(b, CardSelected);
                else if (over) Widgets.DrawBoxSolid(b, CardHover);
                if (on) Widgets.DrawBoxSolid(new Rect(b.x + 3f, b.yMax - 2f, b.width - 6f, 2f), Gold);
                var c = on ? Gold : over ? Color.white : Faint;
                var icon = icons != null && i < icons.Count ? icons[i] : null;
                float ix = named ? b.x + pad : b.x + (b.width - iconW) / 2f;
                if (icon != null) Icon(new Rect(ix, b.y + (b.height - iconW) / 2f, iconW, iconW), icon, c);
                if (named) LabelFit(new Rect(ix + iconW + 5f, b.y, b.xMax - ix - iconW - 5f, b.height), labels[i], c);
                string tip = tips != null && i < tips.Count ? tips[i] : null;
                if (!named) tip = tip.NullOrEmpty() ? labels[i] : labels[i] + "\n\n" + tip;
                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(b, tip);
                if (!on && Widgets.ButtonInvisible(b, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    clicked = i;
                }
                x += w + gap;
            }
            return clicked;
        }

        // ---------------- Legend ----------------

        private static float LegendWidth(LegendItem it)
        {
            if (it.width < 0f) it.width = (it.line.HasValue ? 22f : 14f) + 4f + KeptWidth(it.word, GameFont.Tiny) + 12f;
            return it.width;
        }

        /// <summary>
        /// A legend in one row at the foot of a map: as many entries as fit, on a dark strip, the whole of it in the strip's
        /// tooltip. Returns the strip's height (0 with nothing to say).
        /// </summary>
        public static float LegendStrip(Rect r, List<LegendItem> items, string tip = null)
        {
            if (items == null || items.Count == 0) return 0f;
            float used = 8f;
            int fit = 0;
            for (int i = 0; i < items.Count; i++)
            {
                float w = LegendWidth(items[i]);
                if (used + w > r.width) break;
                used += w;
                fit++;
            }
            var strip = new Rect(r.x, r.yMax - 22f, Mathf.Min(r.width, used + 4f), 22f);
            Widgets.DrawBoxSolid(strip, Shade);
            float x = strip.x + 8f;
            for (int i = 0; i < fit; i++)
            {
                LegendEntry(x, strip.y + 3f, items[i]);
                x += LegendWidth(items[i]);
            }
            if (!tip.NullOrEmpty() || fit < items.Count) TooltipHandler.TipRegion(strip, tip ?? LegendText(items));
            return strip.height;
        }

        /// <summary>A legend as a list, one entry a line (the map's panel with nothing picked). Returns the height used.</summary>
        public static float LegendList(float x, float y, float width, List<LegendItem> items)
        {
            float y0 = y;
            if (items == null) return 0f;
            foreach (var it in items)
            {
                LegendEntry(x + 2f, y, it, width - 4f);
                y += 18f;
            }
            return y - y0;
        }

        private static void LegendEntry(float x, float y, LegendItem it, float width = -1f)
        {
            float sw = it.line.HasValue ? 22f : 14f;
            if (it.line.HasValue)
            {
                if (Event.current.type == EventType.Repaint) DashedLine(new Vector2(x, y + 8f), new Vector2(x + sw, y + 8f), it.line.Value);
            }
            else if (it.icon != null) Icon(new Rect(x + 1f, y + 2f, 12f, 12f), it.icon, it.color);
            else if (it.swatch) Widgets.DrawBoxSolid(new Rect(x + 1f, y + 3f, 12f, 10f), it.color);
            else if (it.ring) RingDraw(new Rect(x + 1f, y + 2f, 12f, 12f), it.color);
            else DiscFill(new Rect(x + 3f, y + 4f, 8f, 8f), it.color);
            float w = width > 0f ? width - sw - 4f : KeptWidth(it.word, GameFont.Tiny) + 6f;
            LabelFit(new Rect(x + sw + 4f, y, w, 16f), it.word, Faint, GameFont.Tiny);
        }

        private static string LegendText(List<LegendItem> items)
        {
            var parts = new List<string>();
            foreach (var it in items) parts.Add(it.word);
            return string.Join("\n", parts);
        }

        // ---------------- Lines ----------------

        /// <summary>A line from a to b in its dashes, colour and width (repaint only; a few dozen draws).</summary>
        public static void DashedLine(Vector2 a, Vector2 b, LineLook look, float alpha = 1f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            var dir = d / len;
            var color = WithAlpha(ToneColor(look.tone), alpha);
            if (look.dash == Dash.Double)
            {
                var n = new Vector2(-dir.y, dir.x) * 1.6f;
                Widgets.DrawLine(a + n, b + n, color, look.width);
                Widgets.DrawLine(a - n, b - n, color, look.width);
                return;
            }
            for (int i = 0; RegionViewsMath.DashAt(look.dash, len, i, out float s, out float e); i++)
            {
                if (look.dash == Dash.Dotted)
                {
                    var c = a + dir * ((s + e) / 2f);
                    float r = look.width * 0.75f;
                    DiscFill(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), color);
                    continue;
                }
                Widgets.DrawLine(a + dir * s, a + dir * e, color, look.width);
            }
        }

        // ---------------- Filter chips ----------------

        public static float FilterChipWidth(string label) => KeptWidth(label, GameFont.Tiny) + 20f;

        /// <summary>A filter chip: a word in a small frame, lit while on. Returns true when clicked.</summary>
        public static bool FilterChip(Rect r, string label, bool on, string tip = null)
        {
            bool over = Mouse.IsOver(r);
            Widgets.DrawBoxSolidWithOutline(r, on ? CardSelected : over ? CardHover : CardFill, on ? WithAlpha(Gold, 0.6f) : CardBorder, 1);
            LabelFit(new Rect(r.x + 10f, r.y, r.width - 14f, r.height), label, on ? Gold : over ? Color.white : Faint, GameFont.Tiny);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }
    }
}
