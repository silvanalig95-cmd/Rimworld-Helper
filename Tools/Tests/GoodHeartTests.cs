using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The heart's good side (Heart/HeartMath.cs "The good side", Heart/GoodHeart.cs, GoodRelease.cs; the player's request
/// of 6 November 2026): one heart with two sides, never both at once; hardship spends the good side before strain
/// builds; it fills slowly within daily caps, mostly from what can't be bought, the comforts only a little; it fades
/// slowly; its bands give a small good mood, brighter dreams and a quicker recovery, and in high spirits now and then a
/// release through the game's own inspirations or a kind word face to face. No work speed; a setting, on by default;
/// old saves start at nothing.
/// </summary>
public static class GoodHeartTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the heart's good side: " + what);
        }
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        bool Near(float a, float b, float tol = 0.001f) => Math.Abs(a - b) <= tol;

        // ---------- The bands ----------
        C(HeartMath.GoodBandFor(0f) == GoodBand.Steady && HeartMath.GoodBandFor(HeartMath.GoodHeartAt - 0.1f) == GoodBand.Steady, "steady below good heart");
        C(HeartMath.GoodBandFor(HeartMath.GoodHeartAt) == GoodBand.GoodHeart && HeartMath.GoodBandFor(HeartMath.HighSpiritsAt - 0.1f) == GoodBand.GoodHeart, "in good heart");
        C(HeartMath.GoodBandFor(HeartMath.HighSpiritsAt) == GoodBand.HighSpirits && HeartMath.GoodBandFor(100f) == GoodBand.HighSpirits, "in high spirits");
        C(HeartMath.GoodHeartAt > 0f && HeartMath.GoodHeartAt < HeartMath.HighSpiritsAt && HeartMath.HighSpiritsAt < HeartMath.Max, "the bands in order, within the range");
        foreach (GoodBand b in Enum.GetValues(typeof(GoodBand)))
            C(HeartMath.GoodBandFor(HeartMath.GoodBandStart(b)) == b, "band start " + b);
        C(HeartMath.GoodMood(GoodBand.Steady) == 0f && HeartMath.GoodMood(GoodBand.GoodHeart) > 0f && HeartMath.GoodMood(GoodBand.HighSpirits) > HeartMath.GoodMood(GoodBand.GoodHeart),
            "steady gives nothing; the bands a small good mood, more in high spirits");
        C(HeartMath.GoodMood(GoodBand.GoodHeart) < -HeartMath.MoodOffset(StrainBand.Strained) && HeartMath.GoodMood(GoodBand.HighSpirits) < -HeartMath.MoodOffset(StrainBand.Worn),
            "smaller than strain's on the other side");
        // The thought gives what the rule says (one situational thought reading the band, like strain's).
        var defs = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_GoodHeart.xml"));
        var band = defs.Root.Elements("ThoughtDef").FirstOrDefault(e => (string)e.Element("defName") == "Council_Heart_GoodSide");
        var stages = band?.Element("stages")?.Elements("li").Select(li => float.Parse((string)li.Element("baseMoodEffect"), System.Globalization.CultureInfo.InvariantCulture)).ToList();
        C(band != null && (string)band.Element("workerClass") == "ColonyCouncil.ThoughtWorker_HeartGood" && (string)band.Element("thoughtClass") == "ColonyCouncil.Thought_HeartGood",
            "the band's mood is a situational thought of its own");
        C(stages != null && stages.Count == 2 && stages[0] == HeartMath.GoodMood(GoodBand.GoodHeart) && stages[1] == HeartMath.GoodMood(GoodBand.HighSpirits),
            "its stages are the rule's moods");
        C(band != null && ((string)band.Element("defName")).StartsWith("Council_Heart_"), "named with the heart's own, so the pile of small moods leaves it out (SmallMoments.Heavy)");
        C(Code("Moods/SmallMoments.cs").Contains("n.StartsWith(\"Council_Heart_\", StringComparison.Ordinal)"), "the pile leaves the heart's own moods out");

        // ---------- What fills it ----------
        C((int)GoodSource.Gathering == 0 && (int)GoodSource.Friends == 1 && (int)GoodSource.Joys == 2 && (int)GoodSource.Belonging == 3 && (int)GoodSource.Purpose == 4
          && (int)GoodSource.Deeds == 5 && (int)GoodSource.Comforts == 6 && HeartMath.GoodSourceCount == 7, "its sources keep their places (saved by position)");
        foreach (GoodSource s in Enum.GetValues(typeof(GoodSource)))
        {
            C(HeartMath.GoodCap(s) > 0f && HeartMath.GoodCap(s) <= HeartMath.GoodDailyCap, s + ": a daily cap within the day's");
            C(HeartMath.GoodShare(s) > 0f && HeartMath.GoodShare(s) <= 1f, s + ": a share at most in full");
            if (s != GoodSource.Comforts)
                C(HeartMath.GoodCap(GoodSource.Comforts) < HeartMath.GoodCap(s), s + ": the comforts of home fill less a day than " + s);
        }
        C(new[] { GoodSource.Gathering, GoodSource.Friends, GoodSource.Joys }.All(s => HeartMath.GoodShare(GoodSource.Comforts) < HeartMath.GoodShare(s)),
            "and a smaller share of what they ease (a rich colony doesn't simply buy it)");
        C(Enum.GetValues(typeof(GoodSource)).Cast<GoodSource>().Sum(HeartMath.GoodCap) > HeartMath.GoodDailyCap, "the day's cap binds before every source is full");
        C(HeartMath.GoodDailyCap < HeartMath.DailyCap / 4f, "it builds slowly: a few points a day at most, against strain's 25");
        float good = 0f, st = 0f, tt = 0f;
        float a1 = HeartMath.Fill(1f, ref good, ref st, 1.2f, ref tt, HeartMath.GoodDailyCap);
        float a2 = HeartMath.Fill(1f, ref good, ref st, 1.2f, ref tt, HeartMath.GoodDailyCap);
        C(Near(a1, 1f) && Near(a2, 0.2f) && Near(good, 1.2f) && Near(st, 1.2f) && Near(tt, 1.2f), "within the source's cap");
        good = 0f;
        st = 0f;
        tt = HeartMath.GoodDailyCap - 0.5f;
        C(Near(HeartMath.Fill(2f, ref good, ref st, 2.5f, ref tt, HeartMath.GoodDailyCap), 0.5f), "within the day's cap");
        good = 99.5f;
        st = tt = 0f;
        C(Near(HeartMath.Fill(2f, ref good, ref st, 2.5f, ref tt, HeartMath.GoodDailyCap), 0.5f) && Near(good, 100f), "never past 100");
        C(HeartMath.Fill(float.NaN, ref good, ref st, 2.5f, ref tt, 4f) == 0f && HeartMath.Fill(-1f, ref good, ref st, 2.5f, ref tt, 4f) == 0f, "nothing from nothing");
        C(HeartMath.NeedFillPerHour(59f) == 0f && HeartMath.NeedFillPerHour(60f) > 0f && HeartMath.NeedFillPerHour(90f) > HeartMath.NeedFillPerHour(60f)
          && HeartMath.NeedFillPerHour(100f) == HeartMath.NeedFillPerHour(90f) && HeartMath.NeedFillPerHour(float.NaN) == 0f,
            "belonging and purpose fill it only well met, more the better they're met");
        C(HeartMath.NeedFillPerHour(100f) * 24f >= HeartMath.GoodCap(GoodSource.Belonging), "a day of friends close by can reach its cap");
        C(HeartMath.DeedFillFor(1f) == HeartMath.DeedFill && HeartMath.DeedFillFor(5f) == 2f * HeartMath.DeedFill && HeartMath.DeedFillFor(0f) == 0f, "a deed by its size, twice at most");
        C(HeartMath.GoalMovedFill > 0f && HeartMath.GoalMovedFill <= HeartMath.GoodCap(GoodSource.Purpose), "a life goal moving, within purpose's cap");

        // ---------- What spends it ----------
        good = 30f;
        float rest = HeartMath.Spend(10f, ref good, out float spent);
        C(rest == 0f && Near(spent, 10f * HeartMath.SpendShare) && Near(good, 30f - spent), "a hard knock spends it before any strain builds");
        good = 3f;
        rest = HeartMath.Spend(10f, ref good, out spent);
        C(good == 0f && Near(spent, 3f) && Near(rest, 10f - 3f / HeartMath.SpendShare), "what it can't cover builds strain");
        good = 0f;
        C(HeartMath.Spend(10f, ref good, out spent) == 10f && spent == 0f, "with nothing on the good side, strain builds as before");
        good = 20f;
        C(HeartMath.Spend(float.NaN, ref good, out spent) == 0f && HeartMath.Spend(-2f, ref good, out spent) == 0f && good == 20f, "nothing spends nothing");
        C(HeartMath.SpendShare > 0f && HeartMath.SpendShare <= 1f, "taken in their stride: never more than a point a point");
        // Never both at once: whatever is left of the good side, strain gets nothing.
        for (float g = 0f; g <= 50f; g += 5f)
            for (float hard = 0.5f; hard <= 40f; hard += 3.5f)
            {
                float gg = g;
                float r = HeartMath.Spend(hard, ref gg, out _);
                C(gg <= 0f || r == 0f, $"one side at a time ({g}, {hard})");
            }

        // ---------- It fades slowly ----------
        C(HeartMath.Faded(50f, 24f) < 50f && HeartMath.Faded(50f, 24f) > 50f * 0.95f, "a few per cent a day");
        C(HeartMath.Faded(100f, 24f * 14f) > 50f && HeartMath.Faded(100f, 24f * 35f) < 50f, "halving in two to five weeks with nothing to fill it");
        C(HeartMath.Faded(0f, 10f) == 0f && HeartMath.Faded(float.NaN, 1f) == 0f, "nothing stays nothing");
        C(HeartMath.Lately(10f, 50f, 1f) == 50f && HeartMath.Lately(60f, 10f, 1f) > 10f && HeartMath.Lately(60f, 10f, 1f) < 60f, "how high it stood lately: never below now, fading");

        // ---------- What the bands do ----------
        C(HeartMath.RecoveryFactor(GoodBand.Steady) == 1f && HeartMath.RecoveryFactor(GoodBand.GoodHeart) > 1f && HeartMath.RecoveryFactor(GoodBand.HighSpirits) > HeartMath.RecoveryFactor(GoodBand.GoodHeart),
            "a quicker recovery from strain, quicker in high spirits");
        C(HeartMath.SweetDreamFactor(GoodBand.Steady) == 1f && HeartMath.SweetDreamFactor(GoodBand.HighSpirits) > HeartMath.SweetDreamFactor(GoodBand.GoodHeart)
          && HeartMath.SweetDreamFactor(GoodBand.GoodHeart) > 1f, "brighter dreams: sweet ones likelier");
        C(HeartMath.GoodNightmareFactor(GoodBand.Steady) == 1f && HeartMath.GoodNightmareFactor(GoodBand.HighSpirits) < HeartMath.GoodNightmareFactor(GoodBand.GoodHeart)
          && HeartMath.GoodNightmareFactor(GoodBand.GoodHeart) < 1f, "and nightmares rarer");
        C(Code("Heart/Heart.cs").Contains("HeartMath.SweetDreamFactor(good)") && Code("Heart/Heart.cs").Contains("HeartMath.GoodNightmareFactor(good)"), "the dreams ask for it");
        C(Code("Heart/HeartLogic.cs").Contains("factor *= HeartMath.RecoveryFactor(HeartMath.GoodBandFor(h.goodLately))"), "strain's relief asks how high it stood lately");
        string effects = Code("Heart/HeartEffects.cs");
        C(!effects.Contains("GoodHeart") && !effects.Contains("good"), "no work speed: the stat part knows nothing of it");

        // ---------- The release ----------
        C(HeartMath.ReleaseChancePerHour(HeartMath.HighSpiritsAt - 0.1f) == 0f && HeartMath.ReleaseChancePerHour(HeartMath.HighSpiritsAt) > 0f, "only in high spirits");
        C(HeartMath.ReleasePerHour * 24f < 0.5f, "now and then, not every day");
        C(HeartMath.ReleasePawnDays >= 7 && HeartMath.ReleaseColonyDays >= 1, "once in a while for each, once a day at most in the colony");
        C(HeartMath.InspirationSpends > HeartMath.KindWordSpends && HeartMath.KindWordSpends > 0f, "it spends a little of the good side, an inspiration more");
        string release = Code("Heart/GoodRelease.cs");
        C(release.Contains("handler.GetRandomAvailableInspirationDef()") && release.Contains("handler.TryStartInspiration(def, reason)"),
            "the game's own inspirations, by their passions and its own rules, with its own letter");
        C(Regex.IsMatch(release, @"Together\.When\(key, p, other, \(\) =>[\s\S]*?Speech\.Say\(p, line, other\)[\s\S]*?TryGainMemory\(t, p\)[\s\S]*?\}, 10f, 0f\);"),
            "a kind word said face to face when the two are together, one walking over to the other");
        C(Regex.IsMatch(release, @"public static string Blocked\([\s\S]{0,1200}MapFacts\.ThreatOn\(p\.Map\)"), "only when it's safe");
        C(Regex.IsMatch(release, @"public static void MaybeStart\([^)]*\)\s*\{\s*if \(!GoodHeart\.On"), "nothing with the good side off");

        // ---------- Through the heart ----------
        string logic = Code("Heart/HeartLogic.cs"), goodCode = Code("Heart/GoodHeart.cs");
        C(Regex.IsMatch(logic, @"float Build\(StrainSource s, float raw\)[\s\S]{0,700}added = GoodHeart\.Spend\(h, s, added\);"), "the hour's hardship spends it first");
        C(Regex.IsMatch(logic, @"public static float Build\(Pawn p, StrainSource source, float raw, string why = null\)[\s\S]{0,1200}added = GoodHeart\.Spend\(h, source, added\);"),
            "so does hardship from outside the hour");
        C(logic.Contains("if (amount > 0f) amount = GoodHeart.Spend(h, buildSource, amount);"), "and strain added by an answer");
        C(Regex.IsMatch(logic, @"float eased = Mathf\.Min\(left, amount\);[\s\S]{0,700}h\.strain = 0f;\s*GoodHeart\.Overflow\(h, \(GoodSource\)i, amount - eased, now\)"),
            "good things ease strain first, and what is beyond fills the good side");
        C(logic.Contains("if (joys > eased) used += GoodHeart.Overflow(h, GoodSource.Joys, joys - eased, now) / HeartMath.GoodShare(GoodSource.Joys);")
          && logic.Contains("JoysWithinCap(h, Mathf.Min(joys, used), why);"), "a small joy from outside the hour the same way, its cap counting what it eased or filled");
        C(Regex.IsMatch(goodCode, @"public static float Fill\([^)]*\)\s*\{\s*if \(!On \|\| h == null \|\| amount <= 0f \|\| h\.strain > 0f\) return 0f;"), "it fills only while strain is at nothing");
        C(goodCode.Contains("public static bool On => Heart.On && (S?.heartGoodSide ?? true);"), "with the heart and its own setting");
        C(Regex.IsMatch(goodCode, @"if \(!On\)\s*\{\s*if \(h\.good > 0f \|\| h\.goodLately > 0f\) Clear\(h\);"), "off: nothing builds, and what there was is gone");
        C(Regex.IsMatch(goodCode, @"int since = h\.deedTick;\s*h\.deedTick = now;[\s\S]{0,200}since < 0\) return;"), "the first look at their deeds only takes note (an old save's deeds bring nothing)");
        C(Regex.IsMatch(goodCode, @"goal\.startedTick == h\.goalTick && progress > h\.goalProgress"), "a life goal moving: the same goal, further along");
        C(goodCode.Contains("RepMath.Valence(d.def.facets.ToArray()) <= 0") && goodCode.Contains("k.id != d.doer"), "a deed the colony speaks well of, that someone else knows of");

        // ---------- Saved, and old saves ----------
        string data = Code("Heart/HeartData.cs");
        C(new[] { "good", "goodTodayTotal", "goodLately", "goalProgress", "releaseWhy" }.All(f => data.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\");")),
            "its values saved on the heart");
        C(data.Contains("Scribe_Values.Look(ref deedTick, \"deedTick\", -1);") && data.Contains("Scribe_Values.Look(ref goalTick, \"goalTick\", -1);")
          && data.Contains("Scribe_Values.Look(ref lastReleaseTick, \"lastReleaseTick\", -999999);"), "an old save loads them as never looked at");
        C(new[] { "goodToday", "goodRecent", "goodSpent" }.All(f => data.Contains("Scribe_Collections.Look(ref " + f + ", \"" + f + "\", LookMode.Value);")),
            "three short lists, one entry a source");
        C(data.Contains("Pad(goodToday, HeartMath.GoodSourceCount);") && data.Contains("Pad(goodSpent, HeartMath.SourceCount);") && data.Contains("goodToday ??= new List<float>();"),
            "an old save's missing lists start at nothing");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "goodToday", "goodRecent", "goodSpent" }.All(f => estimate.Contains("\"Heart/HeartData.cs:" + f + "\"")), "each list has its model in the save estimate");
        C(Code("Heart/HeartTracker.cs").Contains("Scribe_Values.Look(ref lastColonyReleaseTick, \"lastColonyReleaseTick\", -999999);"), "the colony's last release saved");

        // ---------- The setting and the words ----------
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool heartGoodSide = true;") && settings.Contains("Scribe_Values.Look(ref heartGoodSide, \"heartGoodSide\", true);"), "a setting, on by default");
        C(Regex.IsMatch(Code("UI/SettingsCatalog.cs"), @"Toggle\(""heartEpisodes""[^\n]*\n[^\n]*\n\s*Toggle\(""heartGoodSide"", ""CC_Settings_HeartGoodSide"", ""CC_SetNote_HeartGoodSide"", ""heart""\);"),
            "on the heart's page in settings, beside the heart's own, needing the heart");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GoodHeart.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (GoodBand b in Enum.GetValues(typeof(GoodBand)))
            C(keyed.ContainsKey("CC_Good_Band_" + b) && keyed.ContainsKey("CC_Good_Band_" + b + "_Desc"), "words for " + b);
        C(keyed["CC_Good_Band_Steady"] == "steady" && keyed["CC_Good_Band_GoodHeart"] == "in good heart" && keyed["CC_Good_Band_HighSpirits"] == "in high spirits", "the player's names for the bands");
        foreach (GoodSource s in Enum.GetValues(typeof(GoodSource)))
            C(keyed.ContainsKey("CC_Good_Source_" + s), "words for what fills it: " + s);
        foreach (var k in new[] { "CC_Settings_HeartGoodSide", "CC_Settings_HeartGoodSide_Tip", "CC_SetNote_HeartGoodSide" })
            C(keyed.ContainsKey(k), k + " is written");
        string desc = keyed["CC_Good_Band_GoodHeart_Desc"] + keyed["CC_Good_Band_HighSpirits_Desc"];
        C(desc.Contains("+" + HeartMath.GoodMood(GoodBand.GoodHeart).ToString("0")) && desc.Contains("+" + HeartMath.GoodMood(GoodBand.HighSpirits).ToString("0")),
            "the bands' words say their moods");
        string explain = keyed["CC_Good_Explain"];
        C(explain.Contains(HeartMath.GoodHeartAt.ToString("0")) && explain.Contains(HeartMath.HighSpiritsAt.ToString("0")) && explain.Contains(HeartMath.GoodDailyCap.ToString("0") + " a day"),
            "the page's words say where the bands start and the day's cap");
        return n;
    }
}
