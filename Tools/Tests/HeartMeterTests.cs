using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The heart's meters and the day off beside them (the player, 9 November 2026: "the strain level etc. should be
/// graphically more easier to see, perhaps also like a level bar or so. the "give day off" should also be in this menu and
/// not in the pawn bar"), and RimHUD's strain line made to fit its cell ("Strain  jown (60) · fighting ..."). The pure
/// rules first (ticks, labels that fit, the layout at the inspect pane's width and narrower, the trend, the end of time off
/// in the day's hours, the day off's reasons in a few words, the HUD line cut to its width), then the seams in the code.
/// </summary>
public static class HeartMeterTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the heart's meters: " + what);
        }

        Rules(C);
        Seams(root, C);
        return n;
    }

    private static void Rules(Action<bool, string> C)
    {
        // The ticks: where the bands begin, and where a day to themselves may come.
        C(HeartMeterMath.StrainTicks.SequenceEqual(new[] { 30f, 55f, 75f, 85f }), "strain's ticks at 30, 55, 75 and 85");
        C(HeartMeterMath.StrainTicks[0] == HeartMath.BandStart(StrainBand.Strained) && HeartMeterMath.StrainTicks[1] == HeartMath.BandStart(StrainBand.Worn)
          && HeartMeterMath.StrainTicks[2] == HeartMath.BandStart(StrainBand.Breaking) && HeartMeterMath.StrainTicks[3] == HeartMath.EpisodeAt,
            "strain's ticks are the bands' own starts and the day to themselves");
        C(HeartMeterMath.GoodTicks.SequenceEqual(new[] { 20f, 45f, 65f }) && HeartMath.GoodBandFor(HeartMeterMath.GoodTicks[0]) == GoodBand.Steady
          && HeartMath.GoodBandFor(HeartMeterMath.GoodTicks[1]) == GoodBand.GoodHeart && HeartMath.GoodBandFor(HeartMeterMath.GoodTicks[2]) == GoodBand.HighSpirits,
            "the good side's ticks at steady 20, in good heart 45, in high spirits 65");
        for (int i = 0; i < 3; i++)
            C(HeartMath.BandFor(HeartMeterMath.StrainTicks[i]) == (StrainBand)(i + 1) && HeartMath.BandFor(HeartMeterMath.StrainTicks[i] - 0.01f) == (StrainBand)i,
                "a tick sits exactly where band " + (StrainBand)(i + 1) + " begins");

        // A place on the bar.
        C(Math.Abs(HeartMeterMath.At(60f, 200f) - 120f) < 0.01f && HeartMeterMath.At(-5f, 200f) == 0f && HeartMeterMath.At(140f, 200f) == 200f, "a value's place, kept on the bar");

        // The trend: to the point, said from 2.
        C(HeartMeterMath.Moved(60f, 67f) == -7, "the player's Avila: 60 now, 67 this morning, \"-7 today\"");
        C(HeartMeterMath.Moved(60f, 61f) == 0 && HeartMeterMath.Moved(60f, 58.6f) == 0, "a move under 2 isn't said");
        C(HeartMeterMath.Moved(61.6f, 60f) == 2 && HeartMeterMath.Moved(50f, 52.5f) == -3, "rounded, halves away from nothing");

        // The small labels under the ticks: at the tab's width all four; where it's short the later ones give way.
        float[] two = { 11f, 11f, 11f, 11f };
        var wide = HeartMeterMath.LabelLefts(HeartMeterMath.StrainTicks, two, 275f);
        C(wide.All(l => !float.IsNaN(l)), "at the Mood tab's width every tick has its label");
        for (int i = 0; i < wide.Length; i++)
            C(Math.Abs(wide[i] + 5.5f - HeartMeterMath.At(HeartMeterMath.StrainTicks[i], 275f)) < 0.01f, "each label centred on its tick (" + HeartMeterMath.StrainTicks[i] + ")");
        var narrow = HeartMeterMath.LabelLefts(HeartMeterMath.StrainTicks, two, 120f);
        C(!float.IsNaN(narrow[0]) && !float.IsNaN(narrow[1]) && !float.IsNaN(narrow[2]) && float.IsNaN(narrow[3]),
            "narrow, the bands' starts keep their labels and 85, too close to 75, gives way");
        for (float w = 60f; w <= 500f; w += 7f)
        {
            var lefts = HeartMeterMath.LabelLefts(HeartMeterMath.StrainTicks, two, w);
            float last = float.NegativeInfinity;
            bool ok = true;
            foreach (var l in lefts)
            {
                if (float.IsNaN(l)) continue;
                ok &= l >= 0f && l + 11f <= w + 0.01f && l >= last + 3f - 0.01f;
                last = l + 11f;
            }
            C(ok && !float.IsNaN(lefts[0]), $"at {w:0} wide the labels stay on the bar and never touch, the first always shown");
        }
        var edge = HeartMeterMath.LabelLefts(new[] { 2f, 99f }, new[] { 20f, 20f }, 200f);
        C(edge[0] == 0f && Math.Abs(edge[1] - 180f) < 0.01f, "a label at either end is kept on the bar");

        // The layout: at the inspect pane's width the day off's place sits beside the meters; narrow, under them.
        float tab = 500f - 20f - 16f;
        var at = HeartMeterMath.Lay(tab, 109f);
        C(!at.under && at.bar >= HeartMeterMath.MinBar && Math.Abs(at.bar + HeartMeterMath.TrendWidth + HeartMeterMath.SlotGap + at.slotW - tab) < 0.01f && at.slotX + at.slotW <= tab + 0.01f,
            $"the Mood tab ({tab:0} wide): bars {at.bar:0}, the trend, the day off's place at the right ({at.slotW:0})");
        var wider = HeartMeterMath.Lay(tab, 150f);
        C(!wider.under && wider.bar >= HeartMeterMath.MinBar, "with the widest button (a longer translation) still beside them");
        var small = HeartMeterMath.Lay(300f, 109f);
        C(small.under && small.slotX == 0f && small.bar == 300f - HeartMeterMath.TrendWidth, "narrow, the day off's place goes under the meters and the bars take the width");
        var none = HeartMeterMath.Lay(tab, 0f);
        C(!none.under && none.slotW == 0f && none.bar == tab - HeartMeterMath.TrendWidth, "with no day off's place (a prisoner, days off switched off), the bars take the width");
        C(HeartMeterMath.Lay(tab, 40f).slotW == HeartMeterMath.SlotMin && HeartMeterMath.Lay(tab, 400f).slotW == HeartMeterMath.SlotMax, "the place kept between its least and its most");
        for (float w = 120f; w <= 900f; w += 13f)
        {
            var l = HeartMeterMath.Lay(w, 120f);
            C(l.bar >= 0f && l.slotX >= 0f && l.slotX + l.slotW <= w + 0.01f && (l.under || l.bar + HeartMeterMath.TrendWidth + HeartMeterMath.SlotGap <= l.slotX + 0.01f),
                $"at {w:0} wide nothing overlaps or runs off");
        }

        // When time off ends, in the day's hours.
        HeartMeterMath.Until(9.3f, 23.7f, out int hour, out int days);
        C(hour == 9 && days == 1, "a day off given at 9h: until 9h tomorrow");
        HeartMeterMath.Until(13.5f, 2.4f, out hour, out days);
        C(hour == 16 && days == 0, "a bad afternoon from half past one, 2.4 hours left: until 16h today");
        HeartMeterMath.Until(23f, 2f, out hour, out days);
        C(hour == 1 && days == 1, "past midnight is tomorrow");
        HeartMeterMath.Until(5f, -3f, out hour, out days);
        C(hour == 5 && days == 0, "nothing left: now");

        // The day off's reasons in a few words: every reason has them, or the place doesn't show.
        foreach (var hide in new[] { "Off", "NoHeart", "NotFree" })
            C(HeartMeterMath.ShortWhy("CC_Count_WhyNot_" + hide) == null, hide + ": the day off's place doesn't show");
        foreach (var why in HeartMeterMath.ShortWhys)
            C(HeartMeterMath.ShortWhy("CC_Count_WhyNot_" + why) == "CC_Meter_Why_" + why, why + ": its few words");
        C(HeartMeterMath.ShortWhy(null) == null && HeartMeterMath.ShortWhy("CC_Other") == null, "no reason, no words");

        // RimHUD's line: the colours don't count, the first part always stays, the rest as they fit.
        C(HeartMeterMath.StripTags("<color=#ff8800>Worn 60</color> · <color=#999999>fighting</color>") == "Worn 60 · fighting", "colour tags aren't measured");
        C(HeartMeterMath.StripTags("a < b") == "a < b" && HeartMeterMath.StripTags(null) == "", "a lone < is text");
        Func<string, float> w6 = s => s.Length * 6f;
        string old = "Worn down (60) · fighting";
        C(w6(old) > 112f, "the line the player saw (\"Worn down (60) · fighting\") was wider than its cell");
        string fit = HeartMeterMath.Fit(new[] { "<color=#ff8800>Worn 60</color>", "fighting", "good heart 48" }, w6, 112f);
        C(HeartMeterMath.StripTags(fit) == "Worn 60 · fighting" && fit.StartsWith("<color=#ff8800>Worn 60</color>"), "now \"Worn 60 · fighting\", in its colour, the good side left for the tooltip");
        C(w6(HeartMeterMath.StripTags(fit)) <= 112f, "and it fits");
        C(HeartMeterMath.StripTags(HeartMeterMath.Fit(new[] { "Worn 60", "a very long cause that cannot fit", "good 4" }, w6, 112f)) == "Worn 60 · good 4",
            "a part too long is left out, a shorter one after it still comes");
        C(HeartMeterMath.Fit(new[] { "Near breaking and far too long a word" }, w6, 50f) == "Near breaking and far too long a word", "the first part stays even when long");
        C(HeartMeterMath.Fit(new string[0], w6, 50f) == "" && HeartMeterMath.Fit(new[] { "", "Worn 60" }, w6, 112f) == "Worn 60", "nothing, and empty parts, skipped");
    }

    private static void Seams(string root, Action<bool, string> C)
    {
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string meter = Code("Heart/HeartMeter.cs"), glance = Code("Moods/MoodGlance.cs"), view = Code("Heart/HeartView.cs"), card = Code("Heart/HeartCard.cs");
        string off = Code("Heart/HeartTimeOff.cs"), hud = Code("Integrations/RimHud.cs"), settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);

        // The Mood tab: the meters first, right under the mood bar, with the day off's place; the "Heart: ..." line gone.
        int meters = glance.IndexOf("HeartMeter.Draw(pawn, h, x, y, w, true)", StringComparison.Ordinal);
        int sentence = glance.IndexOf("UiKit.Paragraph(x, y, w, mood, Color.white)", StringComparison.Ordinal);
        C(meters > 0 && sentence > meters, "the summary opens on the meters, with the day off's place, then the mood's sentence");
        string draw = glance.Substring(glance.IndexOf("public float Draw(", StringComparison.Ordinal));
        C(!draw.Contains("HeartLine(") && !glance.Contains("g.heart =") && !glance.Contains("CC_Glance_Weight"), "the \"Heart: ...\" line and the weight's line give way to the meters");
        C(draw.Contains("weighs") && draw.Contains("lifts") && draw.Contains("doing") && draw.Contains("HeartAdvice.Draw(pawn, advice, x, y, w, 3)"),
            "what weighs and lifts, what the bands do and the advice stay");
        // The same meters in the heart's section and on the card (no day off's place there: the summary and the card's acts have it).
        C(view.Contains("y += HeartMeter.Draw(p, h, 0f, y, w, false);") && !Regex.IsMatch(view, @"UiKit\.Meter\([^;]*m\.(fill|goodFill)"), "the heart's section draws the same meters");
        C(card.Contains("CardSections.Register(CardKind.Colonist, CardSlot.Now, 19.5f, Meters, null, \"heart-meters\");")
          && card.Contains("HeartMeter.Draw(p, h, rect.x, rect.y + 2f, rect.width, false)"), "the colonist's card draws them before its lines");

        // Drawn from what is kept: built at most twice a second, again after a click.
        C(meter.Contains("now - m.builtAt < 0.5f") && meter.Contains("Stale(p);") && meter.Contains("models.Remove(p.thingIDNumber)"), "the meters are built twice a second at most, and after a click");
        C(!Regex.IsMatch(meter.Substring(meter.IndexOf("private static void Meter(", StringComparison.Ordinal)), @"\.Translate\("), "nothing translated while a meter is drawn");
        C(meter.Contains("HeartMeterMath.LabelLefts(") && meter.Contains("HeartMeterMath.Lay(") && meter.Contains("HeartFelt.Floor(h)"), "the layout, the labels and the weight from the pure rules");
        C(meter.Contains("band >= StrainBand.Strained ? HeartView.BandColor(band) : UiKit.WithAlpha(HeartView.StrainedColor, 0.55f)")
          && Code("ColonistBar/BarBadges.cs").Contains("e.band >= StrainBand.Strained ? HeartView.BandColor(e.band) : new Color(HeartView.StrainedColor.r, HeartView.StrainedColor.g, HeartView.StrainedColor.b, 0.55f)"),
            "strain filled in the colonist bar's heart line colours");
        C(meter.Contains("UiKit.OwnLook") && meter.Contains("UiKit.Bar(r, 0f, fill);"), "in the game's look and Rimfolk's own");
        C(meter.Contains("m.goodOn = GoodHeart.On;"), "the good side shows at any strain, whenever it is on");
        C(meter.Contains("if (overTick >= 0") && meter.Contains("else if (hasFloor && floorTip != null && Mouse.IsOver(mark))") && meter.Contains("else if (!tip.NullOrEmpty())"),
            "one tooltip at a time: a tick's, the weight's mark, else the meter's");
        string strainTip = meter.Substring(meter.IndexOf("public static string StrainTip(", StringComparison.Ordinal));
        strainTip = strainTip.Substring(0, strainTip.IndexOf("private static void Slot(", StringComparison.Ordinal));
        C(strainTip.Contains("CC_Heart_StrainTipHead") && strainTip.Contains("HeartEffects.Doing(h, false)") && strainTip.Contains("HeartView.SourcesDetailed(h, 4)")
          && strainTip.Contains("HeartView.ReliefDetailed(h, 3)") && strainTip.Contains("HeartAdvice.Tip(p, h, 3)"),
            "the meter's hover: the band, what it does now, what built it lately and what eases it");
        C(meter.Contains("m.goodTip = GoodHeart.Tip(p, h);"), "the good side's hover, its own tooltip");
        C(meter.Contains("?.heartEpisodes ?? true") , "the 85 tick only while a day to themselves can come");

        // The day off's place: given, or ended early; greyed with its reason; the same spot whatever it says.
        C(meter.Contains("if (m.running) HeartTimeOff.EndEarly(p);") && meter.Contains("else HeartTimeOff.GiveDayOff(p);"), "its button gives the day off or ends time off early");
        C(meter.Contains("UiKit.ActButton(new Rect(r.x, r.y, r.width, buttonH), m.act, m.whyNot, m.actTip)") && meter.Contains("HeartTimeOff.WhyNotDayOff(p, h, now)"),
            "greyed with why not, its reason in a few words under it");
        C(meter.Contains("Mathf.Max(a, b) + 24f") && meter.Contains("\"CC_Count_DayOff_Button\"") && meter.Contains("\"CC_Meter_EndEarly\""),
            "the place as wide as its widest button, so it never moves");
        C(meter.Contains("if (p == null || !p.IsFreeNonSlaveColonist) return;"), "only for the colony's free people");
        C(off.Contains("public static void EndEarly(Pawn p)") && Regex.IsMatch(off, @"public static void EndEarly\(Pawn p\)[\s\S]{0,300}End\(p, h, Find\.TickManager\.TicksGame, false, ""CC_Count_Cut_Player""\)"),
            "ending early counts for half, called back to work, a day off or a bad afternoon");
        C(off.Contains("public static string WhyNotDayOffKey(") && off.Contains("string key = WhyNotDayOffKey(p, h, now, out float readyIn);"), "the reason's key and its words from one place");
        foreach (Match m in Regex.Matches(off, "\"(CC_Count_WhyNot_\\w+)\""))
        {
            string k = m.Groups[1].Value;
            string s = HeartMeterMath.ShortWhy(k);
            C(s == null ? new[] { "Off", "NoHeart", "NotFree" }.Contains(k.Substring("CC_Count_WhyNot_".Length)) : keyed.ContainsKey(s), k + ": the place hides or says it in a few words");
        }
        foreach (var kind in new[] { "DayOff", "BadDay" })
        foreach (var when in new[] { "Today", "Tomorrow", "Later" })
            C(keyed.ContainsKey("CC_Meter_Off_" + kind + "_" + when) && keyed["CC_Meter_Off_" + kind + "_" + when].Contains("{HOUR}h"), kind + " " + when + ": until when, in the day's hours");
        C(keyed.TryGetValue("CC_Meter_Why_Cooldown", out var cool) && cool == "again in {DAYS}", "on its wait: \"again in 3 days\"");

        // The button on the colonist's command bar: only with its setting, off by default.
        C(settings.Contains("public bool heartDayOffGizmo = false;") && settings.Contains("Scribe_Values.Look(ref heartDayOffGizmo, \"heartDayOffGizmo\", false);"), "the colonist's button: a setting, off");
        C(catalog.Contains("Toggle(\"heartDayOffGizmo\", \"CC_Settings_HeartDayOffGizmo\", \"CC_SetNote_HeartDayOffGizmo\", \"heart,heartDayOff,deepHooks\");"), "on the heart's settings, under the day off");
        C(keyed.TryGetValue("CC_Settings_HeartDayOffGizmo", out var label) && label == "Day off button on the colonist too" && keyed.ContainsKey("CC_Settings_HeartDayOffGizmo_Tip")
          && keyed.ContainsKey("CC_SetNote_HeartDayOffGizmo"), "its words");
        C(off.Contains("if (!RimfolkPatches.On || !Wanted) yield break;") && off.Contains("public static bool Wanted => HeartTimeOff.DayOffOn && (ColonyCouncilMod.Settings?.heartDayOffGizmo ?? false);"),
            "the gizmo stands down without it");
        C(Code("Heart/HeartAdvice.cs").Contains("item.run = () => HeartTimeOff.GiveDayOff(p);"), "the advice's day off button stays");

        // Every key the meters name is there.
        foreach (Match m in Regex.Matches(meter, "\"(CC_Meter_\\w+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), "the key " + m.Groups[1].Value + " exists");
        foreach (var w in HeartMeterMath.ShortWhys)
            C(keyed.ContainsKey("CC_Meter_Why_" + w), "the few words for " + w);

        // RimHUD: short enough for its cell, the whole line in the tooltip; the bar's value the number alone.
        C(hud.Contains("value = HeartMeterMath.Fit(Parts(h), Width, Budget);") && hud.Contains("ReferenceEquals(f.from, from)"), "the strain line fitted once each time the heart changes");
        C(hud.Contains("(cut ? h.hudStrain + \"\\n\\n\" : \"\")"), "the whole line leads the tooltip when it was cut");
        foreach (StrainBand b in Enum.GetValues(typeof(StrainBand)))
            C(keyed.ContainsKey("CC_Hud_StrainShort_" + b), "strain's band in a word: " + b);
        foreach (GoodBand b in Enum.GetValues(typeof(GoodBand)))
            if (b != GoodBand.None) C(keyed.ContainsKey("CC_Hud_GoodShort_" + b), "the good side's band in a word: " + b);
        C(hud.Contains("return (\"CC_Hud_Strain\".Translate(), h.strain.ToString(\"0\"), Mathf.Clamp01(h.strain / HeartMath.Max), Bands,")
          && hud.Contains("Array.ConvertAll(HeartMeterMath.StrainTicks, t => t / HeartMath.Max)"), "the strain bar: the number alone, its marks the meter's ticks");
        C(!hud.Contains("using RimHUD"), "still no reference to RimHUD's own code");
    }
}
