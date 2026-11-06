using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Small letdowns and small joys (Moods/SmallMomentMath.cs, Moods/SmallMoments.cs, phase 3 of the review): each is a
/// short, small moment; what it weighs beyond goes to the heart; a lasting one comes back as a moment; every small mood,
/// anyone's, counts once they pile up, the heavy kinds left to their own sources; a row each on the Mood tab under its own name; the
/// heart's new kinds at the end of their lists; and the game's moods are never changed.
/// </summary>
public static class SmallMomentTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "small moments: " + what);
        }
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        // ---------- The moment ----------
        C(SmallMomentMath.MomentMood(-2f) == -2f && SmallMomentMath.MomentMood(1.5f) == 1.5f, "a small one keeps its size");
        C(SmallMomentMath.MomentMood(-6f) == -3f && SmallMomentMath.MomentMood(8f) == 3f, "a big one is three points at most at the moment");
        C(SmallMomentMath.MomentMood(0f) == 0f && SmallMomentMath.MomentMood(float.NaN) == 0f, "nothing, nothing");
        C(SmallMomentMath.MomentLength(5f) == 0.5f && SmallMomentMath.MomentLength(0.25f) == 0.25f && SmallMomentMath.MomentLength(-1f) == 0.5f,
            "half a day at most, shorter if written shorter; a lasting one's half a day");
        // What moves to the heart: the mood-days beyond the moment.
        C(Math.Abs(SmallMomentMath.Moved(-2f, 3f) - 5f) < 0.001f, "my idea turned down (-2, 3 days): 5 mood-days beyond the moment");
        C(Math.Abs(SmallMomentMath.Moved(-6f, 5f) - 28.5f) < 0.001f, "work wrecked (-6, 5 days): 28.5 beyond a -3 half day");
        C(SmallMomentMath.Moved(-1f, 0.5f) == 0f && SmallMomentMath.Moved(-1f, 0.25f) == 0f, "a cold meal for half a day: all in the moment, nothing beyond");
        C(SmallMomentMath.Moved(-2f, 0f) == 0f && SmallMomentMath.Moved(float.NaN, 2f) == 0f, "no time, or no size: nothing moves");
        C(SmallMomentMath.MomentStrain(-2f, 3f) == SmallMomentMath.Moved(-2f, 3f) * SmallMomentMath.StrainPerMoodDay
          && SmallMomentMath.MomentRelief(2f, 1.5f) == SmallMomentMath.Moved(2f, 1.5f) * SmallMomentMath.ReliefPerMoodDay, "strain and relief at their rates");
        C(SmallMomentMath.MomentStrain(-1f, 3f) < SmallMomentMath.MomentStrain(-2f, 3f) && SmallMomentMath.MomentStrain(-2f, 1f) < SmallMomentMath.MomentStrain(-2f, 3f),
            "bigger or longer weighs more");
        C(Math.Abs(SmallMomentMath.LastingPerHour(-2f, false) * 24f - 2f * SmallMomentMath.StrainPerMoodDay) < 0.0001f
          && Math.Abs(SmallMomentMath.LastingPerHour(2f, true) * 24f - 2f * SmallMomentMath.ReliefPerMoodDay) < 0.0001f, "a lasting one: its size a day, at the same rates");
        // A lasting one comes back as a moment.
        C(SmallMomentMath.MomentDue(1, -1, float.NaN), "it has just begun: a moment");
        C(SmallMomentMath.MomentDue(2, 1, 0.1f), "it got worse: a moment");
        C(!SmallMomentMath.MomentDue(1, 1, 0.5f) && SmallMomentMath.MomentDue(1, 1, SmallMomentMath.AgainDays), "the same, again after a while");
        C(!SmallMomentMath.MomentDue(1, 2, 0.2f), "better again: no new moment for it");

        // ---------- Every small mood, anyone's ----------
        C(SmallMomentMath.IsSmall(-3f, 1f) && SmallMomentMath.IsSmall(5f, -1f) && SmallMomentMath.IsSmall(-6f, 3f), "a few points, a few days or lasting: small");
        C(!SmallMomentMath.IsSmall(-7f, 1f) && !SmallMomentMath.IsSmall(-2f, 5f) && !SmallMomentMath.IsSmall(0f, 1f) && !SmallMomentMath.IsSmall(float.NaN, 1f),
            "a big one, a long one or nothing: not small");
        C(SmallMomentMath.PileStrainPerHour(SmallMomentMath.PileFrom) == 0f && SmallMomentMath.PileStrainPerHour(SmallMomentMath.PileFrom + 10f) > 0f,
            "a few small worries are just life; past them, they wear");
        C(SmallMomentMath.JoyReliefPerHour(SmallMomentMath.JoyPileFrom) == 0f && SmallMomentMath.JoyReliefPerHour(SmallMomentMath.JoyPileFrom + 10f) > 0f,
            "a few small joys are just life; past them, they ease");
        C(SmallMomentMath.PileStrainPerHour(30f) > SmallMomentMath.PileStrainPerHour(20f), "more small worries wear more");
        C(SmallMomentMath.WithinJoyCap(2f, 0f) == 2f && SmallMomentMath.WithinJoyCap(5f, SmallMomentMath.JoyReliefCap - 1f) == 1f
          && SmallMomentMath.WithinJoyCap(1f, SmallMomentMath.JoyReliefCap) == 0f && SmallMomentMath.WithinJoyCap(-1f, 0f) == 0f, "joys ease within their day's cap");

        // ---------- The heart's caps and kinds ----------
        C(StrainSource.Letdowns == Enum.GetValues(typeof(StrainSource)).Cast<StrainSource>().Last() && (int)StrainSource.Letdowns == 17,
            "small letdowns: a new source at the end of the list (saved by position)");
        C(StrainRelief.Joys == Enum.GetValues(typeof(StrainRelief)).Cast<StrainRelief>().Last() && (int)StrainRelief.Joys == 9, "small joys: a new relief at the end of the list");
        C(HeartMath.SourceCap(StrainSource.Letdowns) > 0f && HeartMath.SourceCap(StrainSource.Letdowns) <= 3f, "small letdowns build a little a day at most");
        float today = 0f, total = 0f, built = 0f;
        for (int h = 0; h < 24; h++) built += HeartMath.Add(1f, 1f, 1f, ref today, HeartMath.SourceCap(StrainSource.Letdowns), ref total, HeartMath.DailyCap);
        C(Math.Abs(built - HeartMath.SourceCap(StrainSource.Letdowns)) < 0.001f, "a day of them: never past the source's cap");
        C(SmallMomentMath.JoyReliefCap > 0f && SmallMomentMath.JoyReliefCap <= HeartLogicCaps.Gathering, "small joys ease less in a day than a gathering can");

        // ---------- The defs ----------
        var doc = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_SmallMoments.xml"));
        foreach (var name in new[] { "Rimfolk_SmallLetdowns", "Rimfolk_SmallJoys" })
        {
            var el = doc.Root!.Elements("ThoughtDef").FirstOrDefault(d => d.Element("defName")?.Value == name);
            C(el != null, $"{name} is there");
            if (el == null) continue;
            C(el.Element("thoughtClass")?.Value == "ColonyCouncil.Thought_SmallMoment" && el.Element("workerClass") == null, $"{name} is a moment's memory");
            C(el.Element("stackedEffectMultiplier")?.Value == "1", $"{name}: the row adds them up as they are");
            C(float.Parse(el.Element("durationDays")!.Value, System.Globalization.CultureInfo.InvariantCulture) == SmallMomentMath.MomentDays, $"{name} lasts the moment's time");
            C(int.Parse(el.Element("stackLimit")!.Value) >= 20, $"{name}: room for many at once");
        // A row each (the player's request, 6 November 2026): its own name, and only the same one shares a row.
        string momentCode = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Moods", "SmallMoments.cs"));
        C(momentCode.Contains("public override string LabelCap => label.NullOrEmpty() ? base.LabelCap : label.CapitalizeFirst();"), "each small moment shows under its own name");
        C(momentCode.Contains("base.GroupsWith(other) && other is Thought_SmallMoment m && string.Equals(m.label ?? \"\", label ?? \"\", StringComparison.OrdinalIgnoreCase)"),
            "only the same small thing shares a row");
        }

        // ---------- The code keeps the rules ----------
        string moments = Code("Moods/SmallMoments.cs");
        C(moments.Contains("t.moodPowerFactor = 0f;") && moments.Contains("if (p == null || t?.def == null || t is Thought_SmallMoment"),
            "the memory goes on without a mood; a moment is never taken again");
        C(Regex.IsMatch(moments, @"public static SmallKind LetdownKind\(ThoughtDef def\)[\s\S]{0,400}GoodLife\.IsRimfolk\(def\)"), "only Rimfolk's own become moments");
        C(moments.Contains("HeartLogic.Build(p, StrainSource.Letdowns,") && moments.Contains("HeartLogic.EaseJoys(p,"), "strain and relief through the heart's own path");
        C(moments.Contains("HeartLogic.HasRule(def)") && moments.Contains("Feelings.FeedsWearingFeeling(def)") && moments.Contains("\"NeedFood\""),
            "the heavy kinds stay with their own sources (not counted twice)");
        string logic = Code("Heart/HeartLogic.cs");
        C(logic.Contains("if (SmallMoments.Converted(mem)) continue;"), "a memory turned into a moment brings no strain or relief of its own");
        C(logic.Contains("+= SmallMoments.Hour(p, h, moodGroups, hours, now,") && logic.Contains("out float comforts"), "the hour reads the lasting ones and the pile");
        string patch = Code("Patches/Patch_GoodTimes.cs");
        C(patch.Contains("SmallMoments.Take(__instance, newThought, otherPawn);"), "every memory passes the moments' door");
        // The game's moods: never scaled or regrouped here.
        C(!moments.Contains("MoodOffsetOfGroup(g) *") && !Regex.IsMatch(moments, @"moodPowerFactor\s*=\s*0f;[\s\S]{0,50}IsRimfolk") , "the game's moods are read, never changed");
        string pressures = Code("Pressures/PressureThoughts.cs"), setbacks = Code("Setbacks/SetbackThoughts.cs");
        C(pressures.Contains("public float Felt()") && setbacks.Contains("public float Felt()"), "a lasting one still knows its size, for the heart");
        string data = Code("Heart/HeartData.cs");
        C(new[] { "todayJoys", "letdownsWhy", "joysWhy" }.All(f => data.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\"")),
            "the heart keeps values, not a new list (the day's joys, and what they were in words)");
        return n;
    }

    /// <summary>The heart's daily cap for gatherings (HeartLogic.GatheringCap), as written there.</summary>
    private static class HeartLogicCaps
    {
        public const float Gathering = 12f;
    }
}
