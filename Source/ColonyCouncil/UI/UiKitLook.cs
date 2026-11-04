using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>The fold's words, translated once.</summary>
    internal static class FoldTips
    {
        private static string fold, unfold;
        public static string Fold => fold ??= "CC_UI_FoldTip".Translate();
        public static string Unfold => unfold ??= "CC_UI_UnfoldTip".Translate();
    }

    /// <summary>
    /// The rest of the design's components (docs/design/interface.md, "The look"), which every page draws the same way:
    /// a section heading's line (the one drawing behind SectionHeading, SectionTitle and FoldTitle), the row (component
    /// 5), a row's small act, the line of why not under a card's acts (component 9), the line of days (component 11) and
    /// the dashed frame of an empty state. Each takes a rect and plain data, so any window can draw it; widths are
    /// measured once per label and kept, so nothing is made each frame.
    /// </summary>
    public static partial class UiKit
    {
        // ---------------- A section heading's line ----------------

        // Each count's words, made once.
        private static readonly string[] countWords = new string[1000];

        private static string CountWord(int n) => n >= 0 && n < countWords.Length ? countWords[n] ??= n.ToString() : n.ToString();

        /// <summary>
        /// A section heading's line, 22 high with a line under it: a fold caret, an icon, the name in heading ink, its count
        /// on a small plate, a note on the right in small print. The one drawing behind every section heading.
        /// </summary>
        internal static void HeadingLine(float x, float y, float width, string label, int count, string right, Texture2D icon, Color iconColor, bool caret,
            bool open)
        {
            float tx = x;
            if (caret)
            {
                Icon(new Rect(x + 1f, y + 6f, 10f, 10f), open ? TexButton.Collapse : TexButton.Reveal, Dim);
                tx += 15f;
            }
            if (icon != null)
            {
                Icon(new Rect(tx, y + 4f, IconDense, IconDense), icon, iconColor);
                tx += IconDense + 6f;
            }
            float rw = 0f;
            if (!right.NullOrEmpty())
            {
                rw = Mathf.Min(KeptWidth(right, GameFont.Tiny) + 4f, width * 0.45f);
                LabelFit(new Rect(x + width - rw, y, rw, 22f), right, Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                rw += 6f;
            }
            string cw = count >= 0 ? CountWord(count) : null;
            float plate = cw == null ? 0f : KeptWidth(cw, GameFont.Tiny) + 12f;
            float lw = Mathf.Min(KeptWidth(label ?? "", GameFont.Small) + 2f, Mathf.Max(0f, x + width - rw - tx - plate - 6f));
            LabelFit(new Rect(tx, y, lw, 22f), label, Heading);
            if (cw != null && lw > 0f)
            {
                var pr = new Rect(tx + lw + 5f, y + 4f, plate, 15f);
                Widgets.DrawBoxSolid(pr, CardHover);
                LabelFit(pr, cw, Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
            Divider(x, y + 22f, width);
            Text.Font = GameFont.Small;
        }

        // ---------------- Filter chips with a count ----------------

        /// <summary>A filter chip's width with its count beside its word ("All 9").</summary>
        public static float FilterChipWidth(string label, int count) => FilterChipWidth(label) + (count >= 0 ? KeptWidth(CountWord(count), GameFont.Tiny) + 6f : 0f);

        /// <summary>
        /// A filter chip (component 15) with how many it shows beside its word, in faint print: lit while on. Returns true
        /// when clicked.
        /// </summary>
        public static bool FilterChip(Rect r, string label, int count, bool on, string tip = null)
        {
            if (count < 0) return FilterChip(r, label, on, tip);
            bool over = Mouse.IsOver(r);
            Widgets.DrawBoxSolidWithOutline(r, on ? CardSelected : over ? CardHover : CardFill, on ? WithAlpha(Gold, 0.6f) : CardBorder, 1);
            string cw = CountWord(count);
            float nw = KeptWidth(cw, GameFont.Tiny) + 2f;
            LabelFit(new Rect(r.x + 10f, r.y, Mathf.Max(0f, r.width - 18f - nw), r.height), label, on ? Gold : over ? Ink : Faint, GameFont.Tiny);
            LabelFit(new Rect(r.xMax - 8f - nw, r.y, nw, r.height), cw, on ? Ink : Dim, GameFont.Tiny, TextAnchor.MiddleRight);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        // ---------------- The row ----------------

        /// <summary>
        /// A row (component 5): its back (every other row a shade lighter, lit under the mouse when it does something), its
        /// picture (a portrait, a people's icon in their colour, or an icon), its line with its names as links, a second
        /// line in small print under it when the row is tall enough (<see cref="RowPortrait"/>), and a note on the right.
        /// <paramref name="rightReserve"/> leaves room at the right for the caller's act. Returns true when the row was
        /// clicked (only with <paramref name="clickable"/>; a link or the portrait takes its own click first).
        /// </summary>
        public static bool Row(Rect r, int index, string line, Color lineColor, string sub = null, Pawn pawn = null, Faction faction = null, Texture2D icon = null,
            Color? iconColor = null, string note = null, Color? noteColor = null, string tip = null, bool clickable = false, float rightReserve = 0f)
        {
            Zebra(r, index);
            bool over = Mouse.IsOver(r);
            if (clickable && over) Widgets.DrawHighlight(r);
            bool tall = r.height >= RowPortrait - 0.5f && !sub.NullOrEmpty();
            float x = r.x + 4f;
            if (pawn != null || faction != null)
            {
                float ps = Mathf.Min(r.height - 6f, tall ? 30f : 22f);
                var face = new Rect(x, r.y + (r.height - ps) / 2f, ps, ps);
                Picture(face, pawn, faction, icon, iconColor ?? Faint, pawn != null ? TrustRing(pawn) : (Color?)null);
                var who = pawn != null ? ThingRefs.Of(pawn) : ThingRefs.Of(faction);
                if (!who.IsNone)
                {
                    if (Mouse.IsOver(face)) Previews.Hover(who, face);
                    if (Widgets.ButtonInvisible(face, false))
                    {
                        Cards.Click();
                        Cards.Open(who);
                        return false;
                    }
                }
                x += ps + 6f;
            }
            else if (icon != null)
            {
                float isz = r.height <= RowDense + 0.5f ? IconDense : IconRow;
                Icon(new Rect(x, r.y + (tall ? 6f : (r.height - isz) / 2f), isz, isz), icon, iconColor ?? Faint);
                x += isz + 6f;
            }
            float right = r.xMax - 4f - rightReserve;
            if (!note.NullOrEmpty())
            {
                float nw = Mathf.Min(KeptWidth(note, GameFont.Tiny) + 4f, Mathf.Max(0f, (right - x) * 0.45f));
                LabelFit(new Rect(right - nw, r.y + (tall ? 2f : 0f), nw, tall ? 20f : r.height), note, noteColor ?? Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= nw + 6f;
            }
            if (tall)
            {
                LinkText.Line(new Rect(x, r.y + 2f, Mathf.Max(0f, right - x), 20f), line, lineColor);
                LinkText.Line(new Rect(x, r.y + 21f, Mathf.Max(0f, r.xMax - 4f - rightReserve - x), 17f), sub, Faint, GameFont.Tiny);
            }
            else LinkText.Line(new Rect(x, r.y, Mathf.Max(0f, right - x), r.height), line, lineColor);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(r.x, r.y, Mathf.Max(0f, r.width - rightReserve), r.height), tip);
            return clickable && Widgets.ButtonInvisible(new Rect(r.x, r.y, Mathf.Max(0f, r.width - rightReserve), r.height));
        }

        /// <summary>
        /// A name on its own drawn as a link (component 8, where a name stands alone rather than in a sentence): link ink
        /// with its dotted line, a preview after a pause, its card on a click. Without a thing it is plain ink. Returns the
        /// width the name took.
        /// </summary>
        public static float NameLink(Rect r, string name, ThingRef thing, GameFont font = GameFont.Small)
        {
            if (name.NullOrEmpty()) return 0f;
            float w = Mathf.Min(KeptWidth(name, font) + 2f, r.width);
            if (thing.IsNone)
            {
                LabelFit(r, name, Ink, font);
                return w;
            }
            var hit = new Rect(r.x, r.y, w, r.height);
            bool over = Mouse.IsOver(hit);
            LabelFit(r, name, over ? LinkHot : Link, font);
            float lineH = Text.LineHeightOf(font);
            LinkText.Underline(r.x, r.y + (r.height + lineH) / 2f - (font == GameFont.Tiny ? 3f : 4f), w - 2f, over);
            if (over) Previews.Hover(thing, hit);
            if (Widgets.ButtonInvisible(hit, false))
            {
                Cards.Click();
                Cards.Open(thing);
            }
            return w;
        }

        /// <summary>A colonist's ring in their trust's colour; anyone else's quiet.</summary>
        public static Color TrustRing(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            return comp != null && p != null && p.IsColonist ? TrustColor(comp.standings.TrustOf(p)) : Faint;
        }

        /// <summary>
        /// A row's act, small (the act on the right of a row, a heading's act): greyed with why not as its tooltip. Returns
        /// true when used.
        /// </summary>
        public static bool RowAct(Rect r, string label, string whyNot, string tip = null)
        {
            bool can = whyNot == null;
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            bool clicked = Widgets.ButtonText(r, Fit(label, r.width - 6f), true, true, can);
            Text.Font = old;
            string t = can ? tip : whyNot;
            if (!t.NullOrEmpty()) TooltipHandler.TipRegion(r, t);
            return clicked && can;
        }

        /// <summary>A row's act's width for its label: its word and a little room, kept within reason.</summary>
        public static float RowActWidth(string label) => Mathf.Clamp(KeptWidth(label ?? "", GameFont.Tiny) + 18f, 54f, 140f);

        // ---------------- Why not, under a card's acts ----------------

        private static readonly Dictionary<string, Dictionary<string, string>> whyLines = new Dictionary<string, Dictionary<string, string>>();
        private static int whyCount;

        /// <summary>"Ask a favour: they already owe the colony one.", made once per act and reason.</summary>
        public static string WhyNotText(string act, string whyNot)
        {
            if (whyNot.NullOrEmpty()) return null;
            act ??= "";
            if (!whyLines.TryGetValue(act, out var d)) whyLines[act] = d = new Dictionary<string, string>();
            if (d.TryGetValue(whyNot, out var s)) return s;
            if (whyCount++ > 600)
            {
                whyLines.Clear();
                whyCount = 0;
                whyLines[act] = d = new Dictionary<string, string>();
            }
            s = act.NullOrEmpty() ? whyNot : "CC_Card_WhyLine".Translate(act.Named("ACT"), whyNot.Named("WHY")).Resolve();
            d[whyNot] = s;
            return s;
        }

        /// <summary>
        /// In a card, one line under its acts saying why one can't be done now (component 9), in quiet small print, the
        /// whole of it as a tooltip when cut. Returns the height used (0 when it can be done).
        /// </summary>
        public static float WhyNotLine(float x, float y, float width, string act, string whyNot)
        {
            string s = WhyNotText(act, whyNot);
            if (s == null) return 0f;
            LabelFit(new Rect(x, y, width, 18f), s, Dim, GameFont.Tiny);
            return 18f;
        }

        // ---------------- The line of days ----------------

        /// <summary>
        /// A line of days (component 11): a small line over the last 7 or 30 days with the area under it filled and its last
        /// point marked, in the colour of where it is heading. <paramref name="values"/> are the days' values, oldest first;
        /// <paramref name="count"/> of them are drawn, the newest last. Repaint only; a few dozen draws.
        /// </summary>
        public static void DaysLine(Rect r, IList<float> values, int count, float min, float max, Color color)
        {
            if (values == null || Event.current.type != EventType.Repaint) return;
            int n = Mathf.Min(count, values.Count);
            if (n < 2 || max - min < 0.0001f) return;
            int from = values.Count - n;
            float step = r.width / (n - 1);
            Vector2 At(int i) => new Vector2(r.x + i * step, r.yMax - Mathf.Clamp01((values[from + i] - min) / (max - min)) * r.height);
            var fill = WithAlpha(color, 0.16f);
            for (int i = 0; i < n; i++)
            {
                var p = At(i);
                float x0 = Mathf.Max(r.x, p.x - step / 2f), x1 = Mathf.Min(r.xMax, p.x + step / 2f);
                if (x1 > x0 && r.yMax > p.y) Widgets.DrawBoxSolid(new Rect(x0, p.y, x1 - x0, r.yMax - p.y), fill);
            }
            for (int i = 1; i < n; i++) Widgets.DrawLine(At(i - 1), At(i), color, 1.4f);
            var last = At(n - 1);
            DiscFill(new Rect(last.x - 2.5f, last.y - 2.5f, 5f, 5f), color);
        }

        /// <summary>Where the last days are heading: +1 rising, -1 falling, 0 steady (a change under <paramref name="threshold"/>).</summary>
        public static int DaysHeading(IList<float> values, int count, float threshold)
        {
            if (values == null) return 0;
            int n = Mathf.Min(count, values.Count);
            if (n < 2) return 0;
            float d = values[values.Count - 1] - values[values.Count - n];
            return d >= threshold ? 1 : d <= -threshold ? -1 : 0;
        }

        private static string rising, falling, steady;

        /// <summary>The word beside a line of days: "rising", "falling", "steady".</summary>
        public static string DaysWord(int dir) =>
            dir > 0 ? rising ??= "CC_Look_Rising".Translate() : dir < 0 ? falling ??= "CC_Look_Falling".Translate() : steady ??= "CC_Look_Steady".Translate();

        /// <summary>The colour of where it is heading, for a value where more is better (warm rising, sour falling, faint steady).</summary>
        public static Color DaysColor(int dir) => dir > 0 ? Warm : dir < 0 ? Sour : Faint;

        /// <summary>
        /// A line of days with its word beside it ("rising"), the word in the line's colour: the whole of component 11 in a
        /// rect (the line takes what the word leaves). Returns the direction.
        /// </summary>
        public static int DaysLineWithWord(Rect r, IList<float> values, int count, float min, float max, float threshold)
        {
            int dir = DaysHeading(values, count, threshold);
            var c = DaysColor(dir);
            string word = DaysWord(dir);
            float ww = KeptWidth(word, GameFont.Tiny) + 6f;
            DaysLine(new Rect(r.x, r.y + 2f, Mathf.Max(10f, r.width - ww), r.height - 4f), values, count, min, max, c);
            LabelFit(new Rect(r.xMax - ww + 4f, r.y, ww, r.height), word, c, GameFont.Tiny);
            return dir;
        }

        // ---------------- A dashed frame ----------------

        private static readonly Texture2D DashTex = MakeDash();

        private static Texture2D MakeDash()
        {
            try
            {
                var t = new Texture2D(6, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "RimfolkDash" };
                var on = new Color32(255, 255, 255, 255);
                var off = new Color32(255, 255, 255, 0);
                t.SetPixels32(new[] { on, on, on, off, off, off });
                t.Apply(false, true);
                return t;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>A frame of short dashes (an empty state's block). Repaint only.</summary>
        public static void DashedBox(Rect r, Color color)
        {
            if (Event.current.type != EventType.Repaint || r.width < 2f || r.height < 2f) return;
            if (DashTex == null)
            {
                Widgets.DrawBoxSolidWithOutline(r, Color.clear, color, 1);
                return;
            }
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width, 1f), DashTex, new Rect(0f, 0f, r.width / 6f, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - 1f, r.width, 1f), DashTex, new Rect(0f, 0f, r.width / 6f, 1f));
            // The sides: the same dashes turned, drawn as short boxes.
            for (float y = r.y; y < r.yMax; y += 6f)
            {
                float h = Mathf.Min(3f, r.yMax - y);
                GUI.DrawTexture(new Rect(r.x, y, 1f, h), BaseContent.WhiteTex);
                GUI.DrawTexture(new Rect(r.xMax - 1f, y, 1f, h), BaseContent.WhiteTex);
            }
            GUI.color = Ink;
        }

        /// <summary>
        /// An empty state in one row (a section of a card or of This week with nothing in it): its sentence in small print
        /// in a dashed frame, the same block as <see cref="EmptyState"/>, only short.
        /// </summary>
        public static void EmptyLine(Rect r, string text)
        {
            if (text.NullOrEmpty()) return;
            DashedBox(r, CardBorder);
            LabelFit(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), text, Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
        }
    }
}
