using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart as two meters, drawn like the mood bar above them (the player, 9 November 2026: "the strain level etc.
    /// should be graphically more easier to see, perhaps also like a level bar or so. the "give day off" should also be in
    /// this menu and not in the pawn bar"):
    /// <list type="bullet">
    /// <item><b>Strain</b>, 0 to 100, filled in its band's colour (the colonist bar's heart line), its band and value on the
    /// bar, faint washes where each band lies, ticks with small labels where strained (30), worn down (55) and near breaking
    /// (75) begin and where a day to themselves may come (85), a mark under it for the lasting weight rest can't take it
    /// below (the part it holds shaded), and beside it how it moved today ("-7 today" with its arrow).</item>
    /// <item><b>The good side</b> the same way in green, ticks at steady (20), in good heart (45) and in high spirits (65),
    /// shown at any strain since both sides can be up at once.</item>
    /// <item>On the Mood tab, <b>the day off's place</b> at their right, always in the same spot: "Give a day off", greyed
    /// with its reason in a few words under it ("again in 3 days"); while a day off or a bad afternoon runs, when it ends
    /// ("Day off until 9h tomorrow") and "End it early".</item>
    /// </list>
    /// Hovering a meter tells its band, what it does to them now, what built it lately and what eases it; a tick says what
    /// begins there, the mark what the weight is. Drawn on the Mood tab's summary (Moods/MoodGlance.cs) with the day off's
    /// place, the heart's section below it (HeartView.DrawHeart) and the colonist's card (HeartCard), in the game's look or
    /// Rimfolk's own. Everything drawn is built at most twice a second for each colonist (and again after a click), never
    /// per frame; the layout is HeartMeterMath's.
    /// </summary>
    public static class HeartMeter
    {
        /// <summary>One colonist's meters and day off, in words and numbers, built at most twice a second.</summary>
        public sealed class Model
        {
            public float builtAt;
            public float strain, floor, good;
            public int moved;
            public bool goodOn;
            public Color strainColor, goodColor;
            public string strainText, goodText, trend, trendTip, strainTip, goodTip, floorTip;
            public float[] strainTicks;
            public string[] strainLabels, strainTickTips, goodLabels, goodTickTips;
            /// <summary>The day off's place: whether it shows, whether time off runs, the line under the button and the button.</summary>
            public bool slot, running;
            public string line, lineTip, act, actTip, whyNot;
        }

        private static readonly Dictionary<int, Model> models = new Dictionary<int, Model>();

        /// <summary>A colonist's meters are built again at the next draw (a button was used).</summary>
        public static void Stale(Pawn p)
        {
            if (p != null) models.Remove(p.thingIDNumber);
        }

        public static void Reset() => models.Clear();

        public static Model For(Pawn p, PawnHeart h)
        {
            float now = Time.realtimeSinceStartup;
            if (models.TryGetValue(p.thingIDNumber, out var m) && now - m.builtAt < 0.5f && now >= m.builtAt) return m;
            if (models.Count > 60) models.Clear();
            m = Build(p, h);
            m.builtAt = now;
            models[p.thingIDNumber] = m;
            return m;
        }

        // ---------------- Building it ----------------

        private static Model Build(Pawn p, PawnHeart h)
        {
            int now = Find.TickManager.TicksGame;
            var m = new Model { strain = h.strain, floor = HeartFelt.Floor(h), good = h.good };
            var band = h.Band;
            // The colonist bar's heart line: a band's colour from strained, below it strained's faintly.
            m.strainColor = band >= StrainBand.Strained ? HeartView.BandColor(band) : UiKit.WithAlpha(HeartView.StrainedColor, 0.55f);
            m.strainText = "CC_Meter_Strain".Translate(HeartView.BandLabel(band).Named("BAND"), h.strain.ToString("0").Named("VALUE")).Resolve();
            if (h.strainDayAgoTick >= 0)
            {
                m.moved = HeartMeterMath.Moved(h.strain, h.strainDayAgo);
                if (m.moved != 0) m.trend = "CC_Meter_Today".Translate(m.moved.ToString("+0;-0")).Resolve();
                m.trendTip = "CC_Glance_StrainToday".Translate(h.strain.ToString("0").Named("NOW"), h.strainDayAgo.ToString("0").Named("THEN")).Resolve();
            }
            if (m.floor >= HeartMeterMath.FloorFrom)
                m.floorTip = HeartFelt.WeightLine(p, h) + "\n\n" + "CC_Meter_FloorNote".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
            m.strainTip = StrainTip(p, h, m);
            bool episodes = ColonyCouncilMod.Settings?.heartEpisodes ?? true;
            m.strainTicks = episodes ? HeartMeterMath.StrainTicks : new[] { HeartMeterMath.StrainTicks[0], HeartMeterMath.StrainTicks[1], HeartMeterMath.StrainTicks[2] };
            m.strainLabels = new string[m.strainTicks.Length];
            m.strainTickTips = new string[m.strainTicks.Length];
            for (int i = 0; i < m.strainTicks.Length; i++)
            {
                m.strainLabels[i] = m.strainTicks[i].ToString("0");
                if (i < 3)
                {
                    var b = (StrainBand)(i + 1);
                    m.strainTickTips[i] = TickTip(m.strainTicks[i], HeartView.BandLabel(b), HeartEffects.DoingStrain(b));
                }
                else m.strainTickTips[i] = "CC_Meter_TickEpisode".Translate(m.strainTicks[i].ToString("0").Named("AT")).Resolve();
            }

            m.goodOn = GoodHeart.On;
            if (m.goodOn)
            {
                var good = GoodHeart.Band(h);
                m.goodColor = good != GoodBand.None ? HeartView.GoodColor : UiKit.WithAlpha(HeartView.GoodColor, 0.55f);
                m.goodText = "CC_Meter_Good".Translate(GoodHeart.BandLabel(good).Named("BAND"), h.good.ToString("0").Named("VALUE")).Resolve();
                m.goodTip = GoodHeart.Tip(p, h);
                var ticks = HeartMeterMath.GoodTicks;
                m.goodLabels = new string[ticks.Length];
                m.goodTickTips = new string[ticks.Length];
                for (int i = 0; i < ticks.Length; i++)
                {
                    var b = (GoodBand)(i + 1);
                    m.goodLabels[i] = ticks[i].ToString("0");
                    m.goodTickTips[i] = TickTip(ticks[i], GoodHeart.BandLabel(b), HeartEffects.DoingGood(b));
                }
            }
            Slot(p, h, now, m);
            return m;
        }

        /// <summary>"From 55, worn down: -8 mood, work -7%, ...", by the settings as they are.</summary>
        private static string TickTip(float at, string band, string effects) =>
            effects.NullOrEmpty()
                ? "CC_Meter_TickTipPlain".Translate(at.ToString("0").Named("AT"), band.Named("BAND")).Resolve()
                : "CC_Meter_TickTip".Translate(at.ToString("0").Named("AT"), band.Named("BAND"), effects.Named("EFFECTS")).Resolve();

        /// <summary>
        /// The strain meter's hover: its band and value, what it does to them now, what built it lately and what eased it, the
        /// weight under it, and what would help ease it.
        /// </summary>
        public static string StrainTip(Pawn p, PawnHeart h, Model m)
        {
            var sb = new StringBuilder();
            sb.Append("CC_Heart_StrainTipHead".Translate(HeartView.BandLabel(h.Band), h.strain.ToString("0")).Resolve().Colorize(HeartView.BandColor(h.Band)));
            string doing = HeartEffects.Doing(h, false);
            sb.Append("\n").Append(doing != null
                ? "CC_Count_Doing".Translate(HeartView.BandLabel(h.Band).CapitalizeFirst().Named("BAND"), doing.Named("WHAT")).Resolve()
                : HeartView.BandDesc(h.Band));
            if (m?.trendTip != null) sb.Append("\n").Append(m.trendTip);
            string built = HeartView.SourcesDetailed(h, 4);
            sb.Append("\n\n").Append(built == null ? "CC_Heart_NothingBuilt".Translate().ToString() : "CC_Heart_BuiltBy".Translate(built).Resolve());
            string eased = HeartView.ReliefDetailed(h, 3);
            if (eased != null) sb.Append("\n").Append("CC_Heart_EasedBy".Translate(eased).Resolve());
            string weight = HeartFelt.WeightLine(p, h);
            if (weight != null) sb.Append("\n").Append(weight);
            // What eases it: the advice while strained (with its buttons beside the meters and below), else the general words.
            string advice = h.strain >= HeartMath.BandStart(StrainBand.Strained) && HeartAdvice.On ? HeartAdvice.Tip(p, h, 3) : null;
            if (advice != null) sb.Append("\n\n").Append(advice.Colorize(MoodView.AdviceColor));
            else
            {
                string help = h.strain >= HeartMath.BandStart(StrainBand.Strained) ? HeartView.HelpText(p, h, 3) : null;
                sb.Append("\n\n").Append(help.NullOrEmpty() ? "CC_Meter_EasesBy".Translate().Resolve() : "CC_Heart_WouldHelp".Translate(help).Resolve());
            }
            return sb.ToString();
        }

        /// <summary>The day off's place: "Give a day off" (greyed with why not), or time off under way with "End it early".</summary>
        private static void Slot(Pawn p, PawnHeart h, int now, Model m)
        {
            if (p == null || !p.IsFreeNonSlaveColonist) return;
            var kind = HeartTimeOff.KindOf(h);
            if (kind != TimeOffKind.None)
            {
                m.slot = m.running = true;
                m.line = HeartTimeOff.UntilText(p, h, now);
                m.lineTip = HeartTimeOff.Line(p, h, now);
                m.act = "CC_Meter_EndEarly".Translate().Resolve();
                m.actTip = (kind == TimeOffKind.DayOff ? "CC_Count_DayOff_EndDesc" : "CC_Meter_EndBadDayDesc").Translate(p.Named("PAWN")).Resolve();
                return;
            }
            string key = HeartTimeOff.WhyNotDayOffKey(p, h, now, out float readyIn);
            string shortKey = HeartMeterMath.ShortWhy(key);
            if (key != null && shortKey == null) return;
            m.slot = true;
            m.act = "CC_Count_DayOff_Button".Translate().Resolve();
            m.actTip = "CC_Count_DayOff_Desc".Translate(p.Named("PAWN")).Resolve();
            if (key == null) return;
            m.whyNot = HeartTimeOff.WhyNotDayOff(p, h, now);
            m.line = shortKey.Translate(HeartTimeOff.ReadyInText(readyIn).Named("DAYS"), p.Named("PAWN")).Resolve();
            m.lineTip = m.whyNot;
        }

        // ---------------- Drawing ----------------

        private static float slotWidth = -1f;

        /// <summary>The day off's place's width: its widest button, so it stays in one spot whatever it says.</summary>
        private static float SlotWidth()
        {
            if (slotWidth < 0f)
            {
                float a = UiKit.KeptWidth("CC_Count_DayOff_Button".Translate().Resolve(), GameFont.Small);
                float b = UiKit.KeptWidth("CC_Meter_EndEarly".Translate().Resolve(), GameFont.Small);
                slotWidth = Mathf.Max(a, b) + 24f;
            }
            return slotWidth;
        }

        /// <summary>
        /// Both meters from (x, y) across <paramref name="w"/>, with the day off's place at their right (or under them where the
        /// width is short) when <paramref name="withSlot"/>; returns the height used. Nothing for a heart not yet worked out.
        /// </summary>
        public static float Draw(Pawn p, PawnHeart h, float x, float y, float w, bool withSlot)
        {
            if (p == null || h == null || !h.evaluated) return 0f;
            try
            {
                return DrawBoth(p, h, x, y, w, withSlot);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("the heart's meters", e);
                return 0f;
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static float DrawBoth(Pawn p, PawnHeart h, float x, float y, float w, bool withSlot)
        {
            var m = For(p, h);
            var lay = HeartMeterMath.Lay(w, withSlot && m.slot ? SlotWidth() : 0f);
            float top = y;
            // Room above the first bar for its ticks, which stand a little over it as the mood bar's lines do.
            y += 2f;
            int id = p.thingIDNumber;
            Meter(new Rect(x, y, lay.bar, HeartMeterMath.BarHeight), m.strain, m.strainColor, m.strainText, m.strainTicks, m.strainLabels, m.strainTickTips,
                StrainZone, m.floor, m.floorTip, m.trend, m.moved, m.strainTip, 0x4E3A11D7 ^ id);
            y += HeartMeterMath.MeterHeight;
            if (m.goodOn)
            {
                y += HeartMeterMath.MeterGap;
                Meter(new Rect(x, y, lay.bar, HeartMeterMath.BarHeight), m.good, m.goodColor, m.goodText, HeartMeterMath.GoodTicks, m.goodLabels, m.goodTickTips,
                    GoodZone, -1f, null, null, 0, m.goodTip, 0x4E3A11D8 ^ id);
                y += HeartMeterMath.MeterHeight;
            }
            if (lay.slotW > 0f)
            {
                float sy = lay.under ? y + 4f : top;
                float sh = DrawSlot(p, m, new Rect(x + lay.slotX, sy, lay.slotW, 0f));
                y = lay.under ? sy + sh : Mathf.Max(y, top + sh);
            }
            return y - top + 4f;
        }

        /// <summary>The day off's button and the line under it; returns the height used.</summary>
        private static float DrawSlot(Pawn p, Model m, Rect r)
        {
            const float buttonH = 24f;
            if (UiKit.ActButton(new Rect(r.x, r.y, r.width, buttonH), m.act, m.whyNot, m.actTip))
            {
                try
                {
                    if (m.running) HeartTimeOff.EndEarly(p);
                    else HeartTimeOff.GiveDayOff(p);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("the heart's meters: the day off's button", e);
                }
                HeartAdvice.Stale(Heart.Of(p));
                Stale(p);
            }
            float y = r.y + buttonH + 2f;
            if (!m.line.NullOrEmpty())
            {
                float lh = UiKit.Paragraph(r.x, y, r.width, m.line, m.running ? UiKit.Info : UiKit.Faint, GameFont.Tiny);
                if (!m.lineTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(r.x, y, r.width, lh), m.lineTip);
                y += lh;
            }
            return y - r.y;
        }

        /// <summary>The tick labels' widths and places while a meter is drawn (at most four ticks), kept so no array is made a frame.</summary>
        private static readonly float[] labelWidths = new float[4], labelLefts = new float[4];

        /// <summary>Strain's washes: strained, worn down, near breaking, each in its band's colour, faintly (made once, not a frame).</summary>
        private static readonly System.Func<int, Color> StrainZone = i => UiKit.WithAlpha(HeartView.BandColor((StrainBand)(i + 1)), 0.13f);

        /// <summary>The good side's: steady, in good heart, in high spirits, greener as it goes.</summary>
        private static readonly System.Func<int, Color> GoodZone = i => UiKit.WithAlpha(HeartView.GoodColor, 0.05f + 0.04f * i);

        /// <summary>
        /// One meter: the track with its washes, the fill, the ticks and their labels, the weight's mark, the words on the bar,
        /// the trend beside it, and one tooltip at a time (a tick's, the mark's, else the meter's).
        /// </summary>
        private static void Meter(Rect r, float value, Color fill, string text, float[] ticks, string[] labels, string[] tickTips, System.Func<int, Color> zone,
            float floor, string floorTip, string trend, int moved, string tip, int tipId)
        {
            if (r.width < 8f) return;
            // The track, and a faint wash where each band lies (bands end where the next begins; the last at 100).
            UiKit.Bar(r, 0f, fill);
            float iw = r.width - 2f, ix = r.x + 1f;
            for (int i = 0; i < ticks.Length && i < 3; i++)
            {
                float a = HeartMeterMath.At(ticks[i], iw), b = i + 1 < ticks.Length && i < 2 ? HeartMeterMath.At(ticks[i + 1], iw) : iw;
                if (b > a) Widgets.DrawBoxSolid(new Rect(ix + a, r.y + 1f, b - a, r.height - 2f), zone(i));
            }
            // The fill, in the band's colour.
            float fw = HeartMeterMath.At(value, iw);
            if (fw >= 1f)
            {
                var f = new Rect(ix, r.y + 1f, fw, r.height - 2f);
                if (UiKit.OwnLook) UiKit.Round(new Rect(f.x, f.y, Mathf.Max(f.width, Mathf.Min(f.height, iw)), f.height), UiKit.WithAlpha(fill, fill.a * 0.9f), SkinMath.RadiusSmall);
                else
                {
                    Widgets.DrawBoxSolid(f, UiKit.WithAlpha(fill, fill.a * 0.85f));
                    Widgets.DrawBoxSolid(new Rect(f.x, f.y, f.width, 1f), UiKit.WithAlpha(UiKit.Ink, 0.2f));
                }
            }
            // The weight under it: the part it holds shaded, and a mark with a foot at its place.
            Rect mark = default;
            bool hasFloor = floor >= HeartMeterMath.FloorFrom;
            if (hasFloor)
            {
                float at = HeartMeterMath.At(floor, iw);
                float held = Mathf.Min(at, fw);
                if (held >= 1f) Widgets.DrawBoxSolid(new Rect(ix, r.y + 1f, held, r.height - 2f), UiKit.WithAlpha(UiKit.Shade, 0.3f));
                Widgets.DrawBoxSolid(new Rect(ix + at - 1f, r.y - 1f, 2f, r.height + 5f), UiKit.Ink);
                Widgets.DrawBoxSolid(new Rect(ix + at - 4f, r.yMax + 3f, 8f, 2f), UiKit.Ink);
                mark = new Rect(ix + at - 5f, r.y - 2f, 10f, r.height + 8f);
            }
            // The ticks, in the colour of what begins there, and their small labels under the bar.
            var old = Text.Font;
            Text.Font = GameFont.Tiny;
            var widths = labelWidths;
            var lefts = labelLefts;
            for (int i = 0; i < ticks.Length && i < widths.Length; i++) widths[i] = UiKit.KeptWidth(labels[i], GameFont.Tiny);
            HeartMeterMath.LabelLefts(ticks, widths, iw, lefts);
            int overTick = -1;
            for (int i = 0; i < ticks.Length; i++)
            {
                float tx = ix + HeartMeterMath.At(ticks[i], iw);
                var c = zone(Mathf.Min(i, 2));
                Widgets.DrawBoxSolid(new Rect(tx - 1f, r.y - 2f, 2f, r.height + 4f), UiKit.WithAlpha(c, 0.95f));
                if (!float.IsNaN(lefts[i]))
                {
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(ix + lefts[i], r.yMax + 1f, widths[i] + 2f, 16f), labels[i]);
                }
                if (overTick < 0 && Mouse.IsOver(new Rect(tx - 4f, r.y - 2f, 8f, HeartMeterMath.MeterHeight + 2f))) overTick = i;
            }
            GUI.color = Color.white;
            // The words on the bar, as the mood bar has its number: the band and the value (cut, never wrapped, where it's short).
            string shown = UiKit.Fit(text, r.width - 6f);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UiKit.Shade;
            Widgets.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), shown);
            GUI.color = UiKit.Ink;
            Widgets.Label(r, shown);
            // Beside it, how strain moved today: its arrow, up in sour, down in warm.
            var trendRect = new Rect(r.xMax + 6f, r.y, HeartMeterMath.TrendWidth - 6f, r.height);
            if (trend != null)
            {
                var tc = moved > 0 ? UiKit.Sour : UiKit.Warm;
                UiKit.Icon(new Rect(trendRect.x, r.y + 3f, 12f, 12f), moved > 0 ? Icons.TrendUp : Icons.TrendDown, tc);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = tc;
                Widgets.Label(new Rect(trendRect.x + 15f, r.y, trendRect.width - 15f, r.height), UiKit.Fit(trend, trendRect.width - 15f));
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
            // One tooltip at a time: a tick's, the weight's mark, else the meter's (its trend's with it).
            var whole = new Rect(r.x, r.y - 2f, r.width + (trend != null ? HeartMeterMath.TrendWidth : 0f), HeartMeterMath.MeterHeight + 2f);
            if (overTick >= 0 && tickTips != null && overTick < tickTips.Length && !tickTips[overTick].NullOrEmpty())
                TooltipHandler.TipRegion(whole, new TipSignal(tickTips[overTick], tipId * 31 + overTick));
            else if (hasFloor && floorTip != null && Mouse.IsOver(mark)) TooltipHandler.TipRegion(whole, new TipSignal(floorTip, tipId * 31 + 17));
            else if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(whole, new TipSignal(tip, tipId));
        }
    }
}
