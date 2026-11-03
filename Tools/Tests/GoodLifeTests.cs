using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Used to the good life and the quiet troubles (Moods/GoodLifeMath.cs, Defs/ThoughtDefs/Rimfolk_Thoughts_GoodLife.xml):
/// the take-back never makes more good moods worse and never touches bad ones, its line moves with standards, a richly
/// comfortable colonist settles below the top of the bar, the troubles' chance rises with content days and stays
/// capped, and every trouble is a moderate "Life's ups and downs" memory with a hint and words of its own.
/// </summary>
public static class GoodLifeTests
{
    private static readonly string[] Troubles = { "Worry", "Anniversary", "FormerLife", "Restless", "Aches", "BadNight", "Envy" };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "good life: " + what);
        }

        // ---------- The line moves with standards ----------
        C(GoodLifeMath.LineFor(0) == 45f && GoodLifeMath.LineFor(1) == 40f && GoodLifeMath.LineFor(2) == 35f && GoodLifeMath.LineFor(3) == 30f,
            "the line: 45 modest, 40 comfortable, 35 particular, 30 spoiled");
        C(GoodLifeMath.LineFor(-2) == 45f && GoodLifeMath.LineFor(9) == 30f, "levels out of range stay at the ends");
        C(GoodLifeMath.Level(0f) == 0 && GoodLifeMath.Level(0.24f) == 0 && GoodLifeMath.Level(0.25f) == 1 && GoodLifeMath.Level(0.5f) == 2
          && GoodLifeMath.Level(0.75f) == 3 && GoodLifeMath.Level(1f) == 3 && GoodLifeMath.Level(float.NaN) == 0, "levels as Standards.LevelFor has them");
        for (float s = 0f; s < 1f; s += 0.01f)
            C(GoodLifeMath.Line(s + 0.01f) <= GoodLifeMath.Line(s), $"higher standards never raise the line ({s:0.00})");
        for (int lvl = 0; lvl < 3; lvl++)
            C(GoodLifeMath.TakeBack(80f, GoodLifeMath.LineFor(lvl + 1)) > GoodLifeMath.TakeBack(80f, GoodLifeMath.LineFor(lvl)),
                $"the same good moods: more taken back one level up ({lvl})");

        // ---------- Each good mood's share ----------
        C(GoodLifeMath.Share(50f, 0f) == 1f && GoodLifeMath.Share(0f, 5f) == 1f && GoodLifeMath.Share(-10f, 5f) == 1f, "nothing taken: each good mood in full");
        C(GoodLifeMath.Share(float.NaN, 5f) == 1f && GoodLifeMath.Share(50f, float.NaN) == 1f, "an unknown sum: in full");
        C(Math.Abs(GoodLifeMath.Share(50f, GoodLifeMath.TakeBack(50f, 30f)) - 0.8f) < 0.001f, "+50 for the spoiled: each felt at 80%");
        C(Math.Abs(GoodLifeMath.Share(80f, GoodLifeMath.TakeBack(80f, 30f)) - 47.5f / 80f) < 0.001f, "+80 for the spoiled: each felt at 59%");
        C(GoodLifeMath.Share(10f, 50f) == GoodLifeMath.MinShare, "never under the least share");
        for (int lvl = 0; lvl <= 3; lvl++)
            for (float p = 1f; p <= 250f; p += 1f)
            {
                float line = GoodLifeMath.LineFor(lvl);
                float share = GoodLifeMath.Share(p, GoodLifeMath.TakeBack(p, line));
                C(Math.Abs(p * share - GoodLifeMath.Kept(p, line)) < 0.01f, $"the shrunk good moods add up to what is kept (level {lvl}, +{p})");
            }

        // ---------- Everyday comforts lose most, something new least ----------
        C(GoodLifeMath.Weight(true, 0f, 1, 0f, 5f) == 1f && GoodLifeMath.Weight(false, 1f, 3, 0f, 5f) == 1f, "a lasting mood, or several of a kind at once: everyday");
        C(GoodLifeMath.Weight(false, 1f, 1, 0f, 5f) == GoodLifeMath.FreshWeight, "once lately and brand new: new");
        C(GoodLifeMath.Weight(false, 3f, 1, 0f, 1f) == 1f && GoodLifeMath.Weight(false, 9f, 1, 0f, 1f) == 1f, "three times or more lately: everyday, however new");
        C(GoodLifeMath.Weight(false, -1f, 1, 0f, 1f) == GoodLifeMath.UnknownWeight, "a small pleasure not known since loading: in between");
        C(GoodLifeMath.Weight(false, 2f, 1, 0f, 5f) > GoodLifeMath.FreshWeight && GoodLifeMath.Weight(false, 2f, 1, 0f, 5f) < 1f, "twice lately: in between");
        // A wedding: new when it happens, whether or not its count is known (it was given before loading), then ordinary.
        C(GoodLifeMath.Weight(false, -1f, 1, 0f, 10f) == GoodLifeMath.FreshWeight, "a wedding just attended is new, loaded or not");
        C(GoodLifeMath.Weight(false, -1f, 1, 1.1f, 10f) < 0.35f, "a day later it is still nearly new");
        C(GoodLifeMath.Weight(false, -1f, 1, 6f, 10f) == 1f, "after six days it is ordinary");
        for (float d = 0f; d < 8f; d += 0.25f)
            C(GoodLifeMath.AgeWeight(d + 0.25f, 10f) >= GoodLifeMath.AgeWeight(d, 10f), $"it only grows more ordinary with age ({d})");
        C(Math.Abs(GoodLifeMath.AgeWeight(0.5f, 1f) - (GoodLifeMath.FreshWeight + (1f - GoodLifeMath.FreshWeight) * 0.5f)) < 0.001f,
            "a one-day memory settles over a day");
        C(GoodLifeMath.AgeWeight(3f, 0f) < 1f && GoodLifeMath.AgeWeight(6f, 0f) == 1f, "one without an end settles over six days");
        // Standards' cut: in full on everyday comforts, hardly on something new.
        C(GoodLifeMath.ComfortFor(0.7f, 1f) == 0.7f && GoodLifeMath.ComfortFor(1f, 0.2f) == 1f, "everyday comforts: the whole cut");
        C(Math.Abs(GoodLifeMath.ComfortFor(0.7f, GoodLifeMath.FreshWeight) - (1f - 0.3f * GoodLifeMath.FreshWeight)) < 0.001f, "something new: hardly any cut");
        C(Math.Abs(GoodLifeMath.Decayed(4f, GoodLifeMath.RecentHalfLifeTicks) - 2f) < 0.001f && GoodLifeMath.Decayed(4f, 0) == 4f
          && GoodLifeMath.Decayed(-1f, 10) == 0f, "the count halves every two days");
        {
            // A meal a day settles at about three and a half: everyday.
            float c = 0f;
            for (int d = 0; d < 30; d++) c = GoodLifeMath.Decayed(c, 60000) + 1f;
            C(c > 3f && c < 4f && GoodLifeMath.Weight(false, c, 1, 0f, 1f) == 1f, $"a meal a day becomes everyday ({c:0.00})");
        }
        {
            // A spoiled colonist with +80 of good moods: +60 everyday, a wedding (+20 as felt) brand new, then six days on.
            var v = new List<float> { 60f, 20f };
            var sh = new List<float> { 0f, 0f };
            float take = GoodLifeMath.TakeBack(80f, GoodLifeMath.LineFor(3));
            GoodLifeMath.Spread(v, new List<float> { 1f, GoodLifeMath.AgeWeight(0f, 10f) }, take, sh);
            C(sh[1] >= 0.85f, $"a wedding just attended keeps most of its lift ({sh[1]:0.00})");
            float fresh = sh[1];
            GoodLifeMath.Spread(v, new List<float> { 1f, GoodLifeMath.AgeWeight(2f, 10f) }, take, sh);
            float later = sh[1];
            GoodLifeMath.Spread(v, new List<float> { 1f, GoodLifeMath.AgeWeight(6f, 10f) }, take, sh);
            C(later < fresh && sh[1] < later && Math.Abs(sh[1] - sh[0]) < 0.001f, $"and drifts down to the everyday share ({fresh:0.00}, {later:0.00}, {sh[1]:0.00})");
        }
        var vals = new List<float> { 20f, 30f, 10f, 15f, -5f, 0f };
        var wts = new List<float> { 1f, 1f, GoodLifeMath.FreshWeight, 0.65f, 1f, 1f };
        var shs = new List<float> { 0f, 0f, 0f, 0f, 0f, 0f };
        foreach (float take in new[] { 0f, 5f, 20f, 40f, 60f, 70f, 200f })
        {
            GoodLifeMath.Spread(vals, wts, take, shs);
            float taken = 0f;
            for (int i = 0; i < 4; i++) taken += vals[i] * (1f - shs[i]);
            float most = (20f + 30f + 10f + 15f) * (1f - GoodLifeMath.MinShare);
            C(Math.Abs(taken - Math.Min(take, most)) < 0.05f, $"the spread takes back just what is due ({take}: {taken:0.00})");
            C(shs.All(x => x >= GoodLifeMath.MinShare - 0.0001f && x <= 1f), $"every share between the least and all ({take})");
            C(shs[4] == 1f && shs[5] == 1f, $"bad and empty moods untouched ({take})");
            C(shs[0] <= shs[3] + 0.0001f && shs[3] <= shs[2] + 0.0001f, $"everyday loses most, new least ({take})");
            C(Math.Abs(shs[0] - shs[1]) < 0.0001f, $"as ordinary, the same share ({take})");
            if (take == 0f) C(shs.All(x => x == 1f), "nothing to take: all in full");
        }
        {
            var even = new List<float> { 1f, 1f, 1f };
            var v3 = new List<float> { 20f, 30f, 50f };
            var s3 = new List<float> { 0f, 0f, 0f };
            GoodLifeMath.Spread(v3, even, 25f, s3);
            C(s3.All(x => Math.Abs(x - GoodLifeMath.Share(100f, 25f)) < 0.001f), "equally ordinary: each at the common share");
        }

        // ---------- The take-back ----------
        float spoiled = GoodLifeMath.LineFor(3);
        C(GoodLifeMath.TakeBack(30f, spoiled) == 0f && GoodLifeMath.TakeBack(10f, spoiled) == 0f, "nothing at or below the line");
        C(GoodLifeMath.TakeBack(-20f, spoiled) == 0f && GoodLifeMath.TakeBack(0f, spoiled) == 0f, "no good moods: nothing (bad moods are never touched)");
        C(GoodLifeMath.TakeBack(float.NaN, spoiled) == 0f, "an unknown sum takes nothing");
        C(Math.Abs(GoodLifeMath.TakeBack(50f, spoiled) - 10f) < 0.001f, "half of the first 20 over the line");
        C(Math.Abs(GoodLifeMath.TakeBack(80f, spoiled) - 32.5f) < 0.001f, "and three quarters of the rest");
        C(Math.Abs(GoodLifeMath.TakeBack(120f, spoiled) - 62.5f) < 0.001f, "+120 for the spoiled: 62.5 taken back");
        for (int lvl = 0; lvl <= 3; lvl++)
        {
            float line = GoodLifeMath.LineFor(lvl);
            float prevKept = float.NegativeInfinity, prevMood = -1f;
            for (float p = 0f; p <= 250f; p += 0.25f)
            {
                float kept = GoodLifeMath.Kept(p, line);
                C(kept > prevKept, $"more good moods are always worth more (level {lvl}, +{p})");
                if (p > 0f) C(kept - prevKept >= 0.25f * 0.25f - 0.0001f, $"each point still adds at least a quarter (level {lvl}, +{p})");
                C(kept >= Math.Min(p, line) - 0.0001f, $"never less than the line, or all of them below it (level {lvl}, +{p})");
                C(GoodLifeMath.TakeBack(p, line) >= 0f && GoodLifeMath.TakeBack(p, line) <= Math.Max(0f, p - line) * GoodLifeMath.BeyondShare + 0.0001f,
                    $"never more than three quarters of the excess (level {lvl}, +{p})");
                float mood = GoodLifeMath.Mood(p, -10f, line);
                C(mood >= prevMood - 0.0001f, $"the mood never falls as good moods grow (level {lvl}, +{p})");
                prevKept = kept;
                prevMood = mood;
            }
        }
        // Hard times: it fades, to nothing at the break line.
        C(GoodLifeMath.InHardTimes(20f, 0f) == 20f && GoodLifeMath.InHardTimes(20f, 0.5f) == 10f && GoodLifeMath.InHardTimes(20f, 1f) == 0f,
            "in full while life is good, half halfway, nothing at the break line");
        C(GoodLifeMath.InHardTimes(20f, 3f) == 0f && GoodLifeMath.InHardTimes(20f, -1f) == 20f && GoodLifeMath.InHardTimes(-5f, 0f) == 0f
          && GoodLifeMath.InHardTimes(20f, float.NaN) == 20f, "hard times out of range, and nothing to take");
        // Bad moods count in full, whatever the good ones.
        C(Math.Abs(GoodLifeMath.Mood(80f, -10f, spoiled) - GoodLifeMath.Mood(80f, 0f, spoiled) + 0.1f) < 0.0001f, "a bad mood counts in full on top of the take-back");
        C(GoodLifeMath.Mood(80f, 5f, spoiled) == GoodLifeMath.Mood(80f, 0f, spoiled), "a positive 'bad' sum is ignored");

        // ---------- Where a good life settles ----------
        C(GoodLifeMath.Mood(30f, -10f, spoiled) == 0.7f, "+30 with -10: 70%, untouched");
        float m50 = GoodLifeMath.Mood(50f, -10f, spoiled), m80 = GoodLifeMath.Mood(80f, -10f, spoiled), m100 = GoodLifeMath.Mood(100f, -10f, spoiled);
        C(Math.Abs(m50 - 0.8f) < 0.001f, $"+50 with -10, spoiled: 80% ({m50:P1})");
        C(m80 >= 0.75f && m80 <= 0.9f, $"+80 with -10, spoiled: between 75% and 90% ({m80:P1}), not pinned at the top");
        C(m100 < 0.95f, $"+100 with -10, spoiled: still under 95% ({m100:P1})");
        C(GoodLifeMath.Mood(80f, -10f, GoodLifeMath.LineFor(0)) < 1f, "+80 with -10, modest: under the top too");

        // ---------- The troubles' chance ----------
        C(GoodLifeMath.TroubleChance(0) == 0f && GoodLifeMath.TroubleChance(2) == 0f, "nothing in the first days of contentment");
        C(Math.Abs(GoodLifeMath.TroubleChance(3) - 0.02f) < 0.0001f, "2% on the third day");
        float last = 0f;
        for (int d = 0; d <= 400; d++)
        {
            float c = GoodLifeMath.TroubleChance(d);
            C(c >= last - 0.00001f, $"the chance never falls as content days grow ({d})");
            C(c <= GoodLifeMath.TroubleCap + 0.00001f && c <= 0.12f + 0.00001f, $"the chance stays capped ({d})");
            last = c;
        }
        C(Math.Abs(GoodLifeMath.TroubleChance(13) - 0.12f) < 0.0001f && GoodLifeMath.TroubleChance(365) == GoodLifeMath.TroubleCap, "12% from the 13th day on");
        C(GoodLifeMath.TroubleChance(-5) == 0f, "no days: no chance");
        C(GoodLifeMath.TroubleGapDays >= 10f, "never within ten days of the last");
        C(GoodLifeMath.NextContentDays(4, 0.7f) == 5 && GoodLifeMath.NextContentDays(4, 0.69f) == 0 && GoodLifeMath.NextContentDays(-3, 0.9f) == 1
          && GoodLifeMath.NextContentDays(7, float.NaN) == 0, "content days: one more at 70%, none below");
        // A colonist content all year, from day one: how often a trouble comes, on average (the chance and the gap).
        var rng = new Random(7);
        int troubles = 0;
        for (int run = 0; run < 200; run++)
        {
            int lastAt = -999;
            for (int day = 0; day < 60; day++)
            {
                if (lastAt >= 0 && day - lastAt < GoodLifeMath.TroubleGapDays) continue;
                if (rng.NextDouble() < GoodLifeMath.TroubleChance(day + 1))
                {
                    troubles++;
                    lastAt = day;
                }
            }
        }
        float perYear = troubles / 200f;
        C(perYear >= 1.5f && perYear <= 4.5f, $"a content colonist has a few quiet troubles a year, not one a week ({perYear:0.0} in 60 days)");

        // ---------- Company eases a trouble ----------
        C(GoodLifeMath.EasedAge(0, 60000) == 30000 && GoodLifeMath.EasedAge(30000, 60000) == 45000, "half of what is left goes");
        C(GoodLifeMath.EasedAge(60000, 60000) == 60000 && GoodLifeMath.EasedAge(70000, 60000) == 70000 && GoodLifeMath.EasedAge(10, 0) == 10, "an ended one stays ended");

        // ---------- What the troubles read ----------
        C(GoodLifeMath.WorryStage(0) == 1 && GoodLifeMath.WorryStage(2) == 1 && GoodLifeMath.WorryStage(3) == 1 && GoodLifeMath.WorryStage(6) == 1,
            "a partner, a parent or a child: the bigger worry");
        C(GoodLifeMath.WorryStage(4) == 0 && GoodLifeMath.WorryStage(8) == 0, "a brother, a sister or a grandchild: the smaller one");
        C(GoodLifeMath.WorryStage(9) == -1 && GoodLifeMath.WorryStage(-1) == -1 && GoodLifeMath.WorryStage(12) == -1, "step-family, in-laws and further: none");
        C(GoodLifeMath.AchesWeight(true) > GoodLifeMath.AchesWeight(false), "aches come readier in the cold and the wet");
        C(GoodLifeMath.FormerLifeWeight(5f) > GoodLifeMath.FormerLifeWeight(40f) && GoodLifeMath.FormerLifeWeight(40f) > GoodLifeMath.FormerLifeWeight(100f)
          && GoodLifeMath.FormerLifeWeight(100f) > GoodLifeMath.FormerLifeWeight(400f) && GoodLifeMath.FormerLifeWeight(400f) > 0f, "missing the old life fades with the years");
        C(GoodLifeMath.SameRecreation(new float[] { 0f, 0.4f, 0.01f }), "one kind of recreation, well worn");
        C(!GoodLifeMath.SameRecreation(new float[] { 0.2f, 0.4f, 0f }), "two kinds lately: not the same");
        C(!GoodLifeMath.SameRecreation(new float[] { 0f, 0.05f, 0f }), "one kind, barely used: not worn");
        C(!GoodLifeMath.SameRecreation(new float[] { 0f, 0f }) && !GoodLifeMath.SameRecreation(null), "no recreation at all is the game's own trouble");
        C(!GoodLifeMath.Restless(4, true) && GoodLifeMath.Restless(5, true) && !GoodLifeMath.Restless(7, false) && GoodLifeMath.Restless(8, false), "restless: 5 days with the same fun, 8 without");

        // ---------- The defs ----------
        XElement Def(string defName)
        {
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }
                foreach (var el in doc.Root!.Elements())
                    if (el.Element("defName")?.Value == defName) return el;
            }
            return null;
        }
        var hints = new HashSet<string>();
        foreach (var el in XDocument.Load(Path.Combine(root, "Defs", "MoodHintDefs", "Rimfolk_MoodHints_GoodLife.xml")).Root!.Elements())
            foreach (var t in el.Element("thoughts")?.Elements("li") ?? Enumerable.Empty<XElement>())
                hints.Add(t.Value.Trim());
        // Used to the good life shrinks each good mood (the player found one big bad mood for it unconvincing).
        C(Def("Rimfolk_UsedToGoodLife") == null, "used to the good life has no bad mood of its own");
        C(!File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Moods", "GoodLife.cs")).Contains(": Thought_Situational"),
            "used to the good life is no lasting mood");
        C(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Patches", "Patch_GoodTimes.cs")).Contains("GoodLife.ShareOf("),
            "each good mood group is felt at its kind's share");
        foreach (var t in Troubles)
        {
            string name = "Rimfolk_Trouble_" + t;
            var el = Def(name);
            C(el != null, $"{name} is there");
            if (el == null) continue;
            C(el.Element("thoughtClass")?.Value == "ColonyCouncil.Thought_Trouble" && el.Element("workerClass") == null, $"{name} is a quiet trouble's memory");
            var ext = el.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.SetbackExtension");
            C(ext?.Element("group")?.Value == "Life", $"{name} follows Life's ups and downs");
            var moods = el.Element("stages")!.Elements("li").Select(li => float.Parse(li.Element("baseMoodEffect")!.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            C(moods.Count > 0 && moods.All(m => m <= -5f && m >= -9f), $"{name} is modest (-5 to -9 as written)");
            float days = float.Parse(el.Element("durationDays")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            C(days >= 1f && days <= 3f, $"{name} lasts a day to three");
            C(hints.Contains(name), $"{name} has a hint");
            C(Def("Rimfolk_Pool_Trouble_" + t) != null, $"{name} has words to say");
            if (t != "BadNight") C(Def("Rimfolk_Journal_Trouble_" + t) != null, $"{name} has journal lines");
        }
        C(Def("Rimfolk_Trouble_Envy")?.Descendants("envy").Any(e => e.Value == "true") == true, "envy also follows the jealousy setting");
        return n;
    }
}
