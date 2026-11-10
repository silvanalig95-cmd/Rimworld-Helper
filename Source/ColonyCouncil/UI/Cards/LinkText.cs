using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Text with names that lead somewhere, drawn in Rimfolk's own windows (docs/design/interface.md, "Links and
    /// previews"). A text without markers draws as a plain label, as before. One with markers is read into runs and laid
    /// out for its width once (LinkTextMath.Lay, one Text.CalcSize per word, kept), then drawn piece by piece: plain words
    /// in the text's colour, a link in link ink with a fine dotted line under it, solid and brighter under the mouse. A
    /// click on a link opens its card (Cards.Open); resting on one asks for its preview (Previews). Callers draw only what
    /// is in sight, as before.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LinkText
    {
        private static readonly Dictionary<string, float>[] widths = { new Dictionary<string, float>(), new Dictionary<string, float>(), new Dictionary<string, float>() };
        private static readonly float[] spaces = { -1f, -1f, -1f };
        private static readonly float[] pads = { -1f, -1f, -1f };
        private static GameFont measuring;
        private static readonly Func<string, float> measure = Measure;

        private readonly struct Key : IEquatable<Key>
        {
            private readonly string text;
            private readonly int width;
            private readonly byte font;

            public Key(string text, int width, GameFont font)
            {
                this.text = text;
                this.width = width;
                this.font = (byte)font;
            }

            public bool Equals(Key o) => width == o.width && font == o.font && string.Equals(text, o.text, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode() => unchecked((text?.GetHashCode() ?? 0) * 397 ^ width * 31 ^ font);
        }

        private static readonly Dictionary<Key, LinkTextMath.Layout> layouts = new Dictionary<Key, LinkTextMath.Layout>();

        private static void Metrics(GameFont f)
        {
            int i = (int)f;
            if (spaces[i] >= 0f) return;
            var old = Text.Font;
            Text.Font = f;
            float x = Text.CalcSize("x").x, xx = Text.CalcSize("xx").x;
            pads[i] = Mathf.Max(0f, 2f * x - xx);
            spaces[i] = Mathf.Max(1f, Text.CalcSize("x x").x - xx);
            Text.Font = old;
        }

        private static float Measure(string word)
        {
            var d = widths[(int)measuring];
            if (d.TryGetValue(word, out float w)) return w;
            if (d.Count > 6000) d.Clear();
            w = Mathf.Max(0f, Text.CalcSize(word).x - pads[(int)measuring]);
            d[word] = w;
            return w;
        }

        /// <summary>The text's runs laid out for a width (a negative width: one long line), kept until the text or width changes.</summary>
        public static LinkTextMath.Layout LayoutOf(string text, float width, GameFont font)
        {
            int w = width < 0f ? -1 : Mathf.RoundToInt(width);
            var key = new Key(text, w, font);
            if (layouts.TryGetValue(key, out var l)) return l;
            if (layouts.Count > 3000) layouts.Clear();
            Metrics(font);
            var old = Text.Font;
            Text.Font = font;
            measuring = font;
            try
            {
                l = LinkTextMath.Lay(LinkTextMath.Parse(text), w < 0 ? 100000f : w, measure, spaces[(int)font]);
            }
            finally
            {
                Text.Font = old;
            }
            layouts[key] = l;
            return l;
        }

        private static float LineH(GameFont f) => Text.LineHeightOf(f);

        /// <summary>From one line of wrapped text to the next, as the game's labels set them.</summary>
        private static float Pitch(GameFont f)
        {
            var old = Text.Font;
            Text.Font = f;
            float p = Text.LineHeight + Text.SpaceBetweenLines;
            Text.Font = old;
            return p;
        }

        /// <summary>How tall the text is, wrapped to a width.</summary>
        public static float Height(string text, float width, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            if (!LinkTextMath.HasMarks(text))
            {
                var old = Text.Font;
                Text.Font = font;
                float h = Text.CalcHeight(text, width);
                Text.Font = old;
                return h;
            }
            var l = LayoutOf(text, width, font);
            return LineH(font) + Mathf.Max(0, l.lines - 1) * Pitch(font);
        }

        /// <summary>The text wrapped in the rect's width, from its top; returns the height used.</summary>
        public static float Draw(Rect r, string text, Color color, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            if (!LinkTextMath.HasMarks(text)) return UiKit.Paragraph(r.x, r.y, r.width, text, color, font);
            var l = LayoutOf(text, r.width, font);
            float h = LineH(font) + Mathf.Max(0, l.lines - 1) * Pitch(font);
            Pieces(r.x, r.y, l, color, font, -1f);
            return h;
        }

        /// <summary>
        /// The text on one line in the rect, cut with "..." where it doesn't fit (the whole of it then in a tooltip), like
        /// UiKit.LabelFit; only what fits takes the mouse.
        /// </summary>
        public static void Line(Rect r, string text, Color color, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return;
            if (!LinkTextMath.HasMarks(text))
            {
                UiKit.LabelFit(r, text, color, font);
                return;
            }
            var l = LayoutOf(text, -1f, font);
            float y = r.y + (r.height - LineH(font)) / 2f;
            bool cut = Pieces(r.x, y, l, color, font, r.width);
            if (cut) TooltipHandler.TipRegion(r, LinkTextMath.Plain(text));
        }

        /// <summary>How wide the text is on one line.</summary>
        public static float Width(string text, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            var l = LayoutOf(text, -1f, font);
            float w = 0f;
            foreach (var p in l.pieces)
                if (p.line == 0)
                    w = Mathf.Max(w, p.x + p.w);
            return w + pads[(int)font];
        }

        private static readonly Texture2D Dots = MakeDots();

        private static Texture2D MakeDots()
        {
            try
            {
                var t = new Texture2D(4, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "RimfolkLinkDots" };
                t.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0) });
                t.Apply(false, true);
                return t;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A link's line under its words: fine and dotted, solid and brighter when hot.</summary>
        public static void Underline(float x, float y, float w, bool hot)
        {
            if (Event.current.type != EventType.Repaint || w <= 0f) return;
            if (hot || Dots == null)
            {
                Widgets.DrawBoxSolid(new Rect(x, y, w, 1f), hot ? UiKit.LinkHot : UiKit.LinkLine);
                return;
            }
            GUI.color = UiKit.LinkLine;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, w, 1f), Dots, new Rect(0f, 0f, w / 4f, 1f));
            GUI.color = Color.white;
        }

        /// <summary>Draws the laid-out pieces from (x, y), the links live; with a width, only line one up to it. Returns whether it cut.</summary>
        private static bool Pieces(float x, float y, LinkTextMath.Layout l, Color color, GameFont font, float clip)
        {
            float pitch = Pitch(font), lineH = LineH(font);
            float pad = pads[(int)font], padL = pad / 2f;
            float under = lineH - (font == GameFont.Tiny ? 3f : 4f);
            bool one = clip >= 0f, cut = false;
            // Which link the mouse is on: all its pieces light together.
            int hot = -1;
            var hotRect = default(Rect);
            for (int i = 0; i < l.pieces.Count; i++)
            {
                var p = l.pieces[i];
                if (p.link < 0 || one && (p.line > 0 || p.x >= clip)) continue;
                var pr = new Rect(x + p.x, y + p.line * pitch, one ? Mathf.Min(p.w, clip - p.x) : p.w, lineH);
                if (!Mouse.IsOver(pr)) continue;
                hot = p.link;
                hotRect = pr;
                break;
            }
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = font;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = false;
            try
            {
                for (int i = 0; i < l.pieces.Count; i++)
                {
                    var p = l.pieces[i];
                    string s = p.text;
                    float w = p.w;
                    if (one)
                    {
                        if (p.line > 0 || p.x >= clip)
                        {
                            cut = true;
                            break;
                        }
                        if (p.x + p.w > clip + 0.5f)
                        {
                            s = UiKit.Fit(p.text, clip - p.x + pad);
                            w = clip - p.x;
                            cut = true;
                        }
                    }
                    float py = y + p.line * pitch;
                    var lr = new Rect(x + p.x - padL, py, w + pad + 6f, lineH + 2f);
                    bool link = p.link >= 0;
                    GUI.color = !link ? color : p.link == hot ? UiKit.LinkHot : UiKit.Link;
                    Widgets.Label(lr, s);
                    if (link)
                    {
                        var hit = new Rect(x + p.x, py, w, lineH);
                        Underline(hit.x, py + under, w, p.link == hot);
                        var thing = l.things[p.link];
                        if (!thing.IsNone && Widgets.ButtonInvisible(hit, false))
                        {
                            Cards.Click();
                            // "Show on map" (World/HeardPlaces.cs): the place on the region map, else its card.
                            if (p.link < l.onMap.Count && l.onMap[p.link]) Cards.ShowOnMap(thing);
                            else Cards.Open(thing);
                        }
                    }
                    if (cut) break;
                }
            }
            finally
            {
                Text.WordWrap = true;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
                GUI.color = Color.white;
            }
            if (hot >= 0 && !l.things[hot].IsNone) Previews.Hover(l.things[hot], hotRect);
            return cut;
        }

        /// <summary>Forget the layouts and widths (the interface scale or the fonts changed).</summary>
        public static void Reset()
        {
            layouts.Clear();
            foreach (var d in widths) d.Clear();
            for (int i = 0; i < spaces.Length; i++) spaces[i] = pads[i] = -1f;
        }
    }
}
