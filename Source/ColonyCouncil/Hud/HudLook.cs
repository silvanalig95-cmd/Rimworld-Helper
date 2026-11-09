using System;
using System.Collections.Generic;
using System.Diagnostics;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The on-screen panels in Rimfolk's own look (docs/design/rimfolk-look.md, stage 3, point 3): whether they draw so, and
    /// the parts they draw with. The panels as they were stay behind "Classic on-screen panels" (the player's backup), and
    /// they are always drawn so with "Rimfolk's own look" off; each panel keeps its old drawing as it was and branches here.
    ///
    /// The parts: the card (the look's rounded fill and faint edge, as solid as the panels' look and the panel's own
    /// opacity say), a row's stripe and wash for what is urgent, flat bars (a bar this small shows no corner, and a rounded
    /// one would cost nine draws), the mood bar with its break lines and where it is heading, the heart's line in the colours
    /// and at the marks of the heart's meters (Heart/HeartMeter.cs), icons on whole pixels and square, the rating's badge,
    /// and words drawn only on repaint, their width measured once and kept. Nothing here makes a string or an object while
    /// drawing: every word a panel draws was made when it gathered.
    ///
    /// The clock times the painting in either look, and the debug action "On-screen panels: time the classic and refined
    /// drawing" sets them side by side: so many frames refined, then as many classic, then one line in the log.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HudLook
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ---------------- The switch ----------------

        /// <summary>Frames painted in a timing run, or -1 when none runs.</summary>
        private static int measuring = -1;

        /// <summary>
        /// Whether the panels draw in Rimfolk's own look now: "Rimfolk's own look" on and "Classic on-screen panels" off (a
        /// timing run decides it while it lasts).
        /// </summary>
        public static bool Refined
        {
            get
            {
                if (measuring >= 0)
                {
                    var m = HudLookMath.MeasureLook(measuring);
                    if (m.HasValue) return m.Value;
                }
                var s = S;
                return s != null && s.ownLook && !s.hudClassic;
            }
        }

        private static bool? was;

        /// <summary>Once a frame (HudManager.Update): when the look changed, every panel gathers and measures itself again.</summary>
        public static void Watch(IEnumerable<HudPanel> panels)
        {
            bool now = Refined;
            if (was == now) return;
            bool first = !was.HasValue;
            was = now;
            if (first) return;
            foreach (var p in panels) p?.MarkDirty();
            HudHoverCard.Reset();
        }

        /// <summary>The look's colours for what a panel draws (the look's tokens), without the scrollbars' styles: returns how it was.</summary>
        public static bool Begin() => UiKit.BeginOwnColours();

        public static void End(bool wasOwn) => UiKit.EndOwnColours(wasOwn);

        // ---------------- The card ----------------

        private static HudTheme Theme => S?.hudTheme ?? HudTheme.Dark;

        /// <summary>The card's fill: the look's panel (its window ground for the darker look), as solid as the look and the panel's opacity say.</summary>
        public static Color CardFill(float opacity, bool hovered)
        {
            var theme = Theme;
            uint c = HudLookMath.CardColour(theme);
            return new Color(SkinMath.R(c), SkinMath.G(c), SkinMath.B(c), HudLookMath.CardAlpha(theme, opacity, hovered));
        }

        /// <summary>
        /// A panel's card: the rounded fill, and its edge while the mouse is on it. The edge is left off otherwise: nine more
        /// draws for every panel every frame, where the rounded fill already gives the card its shape.
        /// </summary>
        public static void Card(Rect r, float opacity, bool hovered)
        {
            if (Event.current.type != EventType.Repaint) return;
            UiKit.Round(r, CardFill(opacity, hovered), SkinMath.RadiusCard);
            if (hovered) UiKit.RoundEdge(r, UiKit.EdgeStrong, SkinMath.RadiusCard);
        }

        /// <summary>The edge round every panel while they are being arranged.</summary>
        public static void Arranging(Rect r)
        {
            if (Event.current.type == EventType.Repaint) UiKit.RoundEdge(r.ContractedBy(1f), UiKit.WithAlpha(UiKit.Accent, 0.7f), SkinMath.RadiusCard);
        }

        // ---------------- Rows ----------------

        /// <summary>
        /// The stripe down an urgent row's left edge, in the colour of how much it matters, pulsing for life and limb (so what
        /// is urgent shows by its shape before its colour is read); nothing for a calm row. An urgent row has no wash over it:
        /// the danger colour's words would fall under 4.5:1 on a reddened row over a bright map.
        /// </summary>
        public static void Stripe(Rect row, HudLevel level, float s)
        {
            if (level < HudLevel.Soon || Event.current.type != EventType.Repaint) return;
            float w = Mathf.Max(2f, HudLookMath.At(HudLookMath.Stripe, s));
            var c = LevelColor(level);
            if (level == HudLevel.Life) c = UiKit.WithAlpha(c, 0.6f + 0.4f * HudKit.Pulse);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y + 2f, w, Mathf.Max(2f, row.height - 4f)), c);
        }

        /// <summary>A stripe in a colour of its own (an alert's priority).</summary>
        public static void Stripe(Rect row, Color c, float s)
        {
            if (Event.current.type != EventType.Repaint) return;
            float w = Mathf.Max(2f, HudLookMath.At(HudLookMath.Stripe, s));
            Widgets.DrawBoxSolid(new Rect(row.x, row.y + 2f, w, Mathf.Max(2f, row.height - 4f)), c);
        }

        /// <summary>The row under the mouse.</summary>
        public static void Hover(Rect r)
        {
            if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(r, UiKit.WithAlpha(UiKit.RaisedHover, 0.55f));
        }

        /// <summary>Life and limb in the danger colour, soon in amber, the rest in <paramref name="quiet"/>.</summary>
        public static Color LevelColor(HudLevel level, Color? quiet = null) =>
            level == HudLevel.Life ? UiKit.Danger : level == HudLevel.Soon ? UiKit.Soon : quiet ?? UiKit.Faint;

        /// <summary>A mood said in words: the bar's colour at full strength, so the number reads at 4.5:1 (the bar's own good colour is see-through).</summary>
        public static Color MoodInk(float now, float minor, float major, float extreme) =>
            now < extreme ? UiKit.Danger : now < major ? UiKit.Sour : now < minor ? UiKit.Soon : UiKit.Good;

        /// <summary>The icon for how much something matters where it has none of its own: the warning for life and limb and for soon.</summary>
        public static Texture2D LevelIcon(HudLevel level) => level >= HudLevel.Soon ? Icons.Warning : null;

        // ---------------- Icons, on whole pixels and square ----------------

        /// <summary>A rect on whole pixels, so an icon is never blurred between two.</summary>
        public static Rect Snap(Rect r) => new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));

        /// <summary>A square of <paramref name="size"/> centred on a line from <paramref name="y"/> <paramref name="h"/> tall.</summary>
        public static Rect IconAt(float x, float y, float h, float size) => new Rect(Mathf.Round(x), Mathf.Round(y + (h - size) / 2f), size, size);

        public static void Icon(Rect r, Texture2D tex, Color c)
        {
            if (tex == null || Event.current.type != EventType.Repaint) return;
            GUI.color = c;
            GUI.DrawTexture(Snap(r), tex, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }

        /// <summary>A row's icon size at a scale: one size for every panel, on whole pixels.</summary>
        public static float IconSize(float s) => Mathf.Max(10f, HudLookMath.At(HudLookMath.RowIcon, s));

        /// <summary>A mark's size (the small icons at the end of a colonist's row).</summary>
        public static float MarkSize(float s) => Mathf.Max(9f, HudLookMath.At(HudLookMath.Mark, s));

        // ---------------- Words ----------------

        private static readonly Dictionary<string, float> tinyWidths = new Dictionary<string, float>(), smallWidths = new Dictionary<string, float>(),
            mediumWidths = new Dictionary<string, float>();

        /// <summary>A text's width in a font, measured once and kept (the panels' words are made when they gather).</summary>
        public static float Width(string text, GameFont font)
        {
            if (text.NullOrEmpty()) return 0f;
            var d = font == GameFont.Tiny ? tinyWidths : font == GameFont.Small ? smallWidths : mediumWidths;
            if (d.TryGetValue(text, out float w)) return w;
            if (d.Count > 2000) d.Clear();
            var old = Text.Font;
            Text.Font = font;
            w = Text.CalcSize(text).x;
            Text.Font = old;
            d[text] = w;
            return w;
        }

        /// <summary>Words in a colour, cut to fit with "..." only when they don't; drawn on repaint only (nothing else needs them).</summary>
        public static void Label(Rect r, string text, Color c, GameFont font, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (text.NullOrEmpty() || r.width <= 2f || Event.current.type != EventType.Repaint) return;
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = c;
            Widgets.Label(r, Width(text, font) <= r.width ? text : UiKit.Fit(text, r.width));
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        // ---------------- Bars ----------------

        /// <summary>A flat bar: its dark track and the share filled in a colour.</summary>
        public static void Bar(Rect r, float fill, Color c)
        {
            if (Event.current.type != EventType.Repaint || r.width < 1f) return;
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            fill = Mathf.Clamp01(fill);
            if (fill > 0f) Widgets.DrawBoxSolid(new Rect(r.x, r.y, Mathf.Max(1f, Mathf.Round(r.width * fill)), r.height), c);
        }

        /// <summary>A bar filled from one share to another (trust either side of nothing).</summary>
        public static void Span(Rect r, float from, float to, Color c)
        {
            if (Event.current.type != EventType.Repaint || r.width < 1f) return;
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            float a = Mathf.Round(r.width * Mathf.Clamp01(from)), b = Mathf.Round(r.width * Mathf.Clamp01(to));
            if (b - a >= 1f) Widgets.DrawBoxSolid(new Rect(r.x + a, r.y, b - a, r.height), c);
            Widgets.DrawBoxSolid(new Rect(r.x + Mathf.Round(r.width / 2f), r.y - 1f, 1f, r.height + 2f), UiKit.WithAlpha(UiKit.Ink, 0.5f));
        }

        /// <summary>A line across a bar where something begins (a break line, the warning line), a little taller than the bar.</summary>
        public static void Tick(Rect bar, float at, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            float x = Mathf.Round(bar.x + bar.width * Mathf.Clamp01(at));
            Widgets.DrawBoxSolid(new Rect(x - 1f, bar.y - 1f, 2f, bar.height + 2f), c);
        }

        /// <summary>
        /// The mood bar: its track, the mood in the colour of the line it is under (as the classic bar), the minor and major
        /// break lines, and a mark where it is heading, in the good or the bad colour.
        /// </summary>
        public static void MoodBar(Rect r, float now, float heading, float minor, float major, float extreme)
        {
            if (Event.current.type != EventType.Repaint || r.width < 4f) return;
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            now = Mathf.Clamp01(now);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, Mathf.Max(1f, Mathf.Round(r.width * now)), r.height), HudKit.MoodColor(now, minor, major, extreme));
            Tick(r, major, HudKit.MajorLine);
            Tick(r, minor, HudKit.MinorLine);
            if (Mathf.Abs(heading - now) >= 0.01f)
            {
                float hx = Mathf.Round(r.x + r.width * Mathf.Clamp01(heading));
                Widgets.DrawBoxSolid(new Rect(hx - 1f, r.y - 2f, 2f, r.height + 4f), heading > now ? UiKit.Good : UiKit.Bad);
            }
        }

        /// <summary>
        /// The heart's line under the mood bar, as the heart's meters show it: strain filled in its band's colour (strained's
        /// faintly below it), the lines where strained, worn down and near breaking begin (once strain has reached the first:
        /// a calm heart needs no marks, and they would be three more draws a row); under it the good side in its green.
        /// </summary>
        public static void HeartLine(Rect r, StrainBand band, float strain, float good, bool goodOn)
        {
            if (Event.current.type != EventType.Repaint || r.width < 8f || r.height < 1f) return;
            bool both = goodOn && good >= 1f;
            float h = Mathf.Max(1f, Mathf.Floor((r.height - 1f) / 2f));
            if (strain >= 1f || !both)
            {
                var sr = new Rect(r.x, r.y, r.width, h);
                Widgets.DrawBoxSolid(sr, UiKit.MeterBack);
                var c = band >= StrainBand.Strained ? HeartView.BandColor(band) : UiKit.WithAlpha(HeartView.StrainedColor, 0.55f);
                float fw = HeartMeterMath.At(strain, sr.width);
                if (fw >= 1f) Widgets.DrawBoxSolid(new Rect(sr.x, sr.y, Mathf.Round(fw), sr.height), c);
                var ticks = HeartMeterMath.StrainTicks;
                for (int i = 0; i < ticks.Length && i < 3 && band >= StrainBand.Strained; i++)
                {
                    float tx = Mathf.Round(sr.x + HeartMeterMath.At(ticks[i], sr.width));
                    Widgets.DrawBoxSolid(new Rect(tx, sr.y, 1f, sr.height), UiKit.WithAlpha(HeartView.BandColor((StrainBand)(i + 1)), 0.9f));
                }
            }
            if (!both) return;
            var gr = new Rect(r.x, r.y + h + 1f, r.width, h);
            Widgets.DrawBoxSolid(gr, UiKit.MeterBack);
            float gw = HeartMeterMath.At(good, gr.width);
            if (gw >= 1f) Widgets.DrawBoxSolid(new Rect(gr.x, gr.y, Mathf.Round(gw), gr.height), HeartView.GoodColor);
        }

        /// <summary>A weapon's rating as the weapon card draws it (Weapons/WeaponUi.Badge): the number in its band's colour, framed in it; the words made when gathered.</summary>
        public static void Badge(Rect r, Color c, string text)
        {
            if (Event.current.type != EventType.Repaint || text.NullOrEmpty()) return;
            UiKit.RoundBox(r, UiKit.WithAlpha(UiKit.WindowGround, 0.94f), c, SkinMath.RadiusSmall);
            Label(r, text, c, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        /// <summary>A small rounded chip in a colour, washed and edged (an idle colonist's mark).</summary>
        public static void Chip(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            UiKit.RoundBox(r, UiKit.WithAlpha(c, 0.14f), UiKit.WithAlpha(c, 0.55f), SkinMath.RadiusSmall);
        }

        // ---------------- The clock ----------------

        private static readonly HudLookMath.Clock refinedClock = new HudLookMath.Clock(), classicClock = new HudLookMath.Clock();

        /// <summary>Whether the panels' painting is being timed: always, it costs two readings of the stopwatch a panel.</summary>
        public static long Start() => Stopwatch.GetTimestamp();

        /// <summary>One panel painted in a look.</summary>
        public static void Painted(bool refined, long start) => (refined ? refinedClock : classicClock).Add(Stopwatch.GetTimestamp() - start, 1);

        /// <summary>A frame painted (HudPaint, after every panel): counted for the look it was in, and a timing run moved on.</summary>
        public static void Frame(bool any)
        {
            if (!any) return;
            (Refined ? refinedClock : classicClock).Frame();
            if (measuring < 0) return;
            measuring++;
            if (HudLookMath.MeasureLook(measuring).HasValue) return;
            measuring = -1;
            Log.Message("[Rimfolk] " + Report());
        }

        /// <summary>The two looks' figures in a line.</summary>
        public static string Report()
        {
            long f = Stopwatch.Frequency;
            return string.Format("On-screen panels, painting time: refined {0:0.0} µs a frame ({1:0.0} a panel, {2} frames), classic {3:0.0} µs a frame ({4:0.0} a panel, {5} frames).",
                refinedClock.PerFrame(f), refinedClock.PerPanel(f), refinedClock.frames, classicClock.PerFrame(f), classicClock.PerPanel(f), classicClock.frames);
        }

        /// <summary>A timing run: the clocks start again, then so many frames refined and as many classic, then the line in the log.</summary>
        public static void StartTiming()
        {
            refinedClock.Clear();
            classicClock.Clear();
            measuring = 0;
            Messages.Message("Timing the on-screen panels: refined, then classic, then a line in the log.", MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction("Rimfolk", "On-screen panels: time the classic and refined drawing (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TimePanels()
        {
            if (!S.hudShown) S.hudShown = true;
            StartTiming();
        }

        [DebugAction("Rimfolk", "On-screen panels: painting time so far (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PanelsTime() => Log.Message("[Rimfolk] " + Report());
    }
}
