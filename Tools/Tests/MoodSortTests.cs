using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The compact Mood tab (Moods/MoodSortMath.cs): which group each kind of mood goes in, the short line it shows, the
/// order within a group, the rows' layout and how many fit on small screens; and every mood hint's short action
/// (Defs/MoodHintDefs): one for every fix, short, one line, no full stop.
/// </summary>
public static class MoodSortTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "mood sorting: " + what);
        }

        const int memory = 30000, sit = MoodSortMath.WhileCause, perm = MoodSortMath.Permanent;
        MoodGroup G(float mood, int left, bool faith = false, bool nature = false, HintAction hint = HintAction.None) =>
            MoodSortMath.GroupOf(mood, left, faith, nature, hint);

        // ---------- The groups ----------
        Check(G(-3f, sit) == MoodGroup.Fix, "a mood while its cause lasts, without a hint, is something to fix");
        Check(G(-3f, memory) == MoodGroup.Pass, "a memory without a hint passes");
        Check(G(-3f, sit, hint: HintAction.Actionable) == MoodGroup.Fix && G(-3f, memory, hint: HintAction.Actionable) == MoodGroup.Fix,
            "a hint that says acting helps: you can fix it, memory or not (a table, the next meal)");
        Check(G(-3f, memory, hint: HintAction.Fades) == MoodGroup.Pass, "a memory whose hint only says it fades passes");
        Check(G(-3f, sit, hint: HintAction.Fades) == MoodGroup.Pass, "a lasting mood whose hint says it ends with its cause (a world event) passes");
        Check(G(-3f, sit, faith: true) == MoodGroup.Faith && G(-3f, memory, faith: true) == MoodGroup.Faith, "a precept's mood, without a fix: faith");
        Check(G(-3f, sit, nature: true) == MoodGroup.Faith, "a trait's or gene's mood, without a fix: who they are");
        Check(G(-3f, sit, faith: true, hint: HintAction.Fades) == MoodGroup.Faith, "faith beats a hint that only says it fades");
        Check(G(-3f, sit, faith: true, hint: HintAction.Actionable) == MoodGroup.Fix, "an altar the faith asks for can be built: you can fix it");
        Check(G(-3f, sit, nature: true, hint: HintAction.Actionable) == MoodGroup.Fix, "a greedy colonist's bedroom can be improved: you can fix it");
        Check(G(-3f, perm) == MoodGroup.Faith && G(-3f, perm, hint: HintAction.Actionable) == MoodGroup.Faith, "a mood that never fades is lasting, whatever its hint");
        foreach (int left in new[] { sit, perm, 0, memory })
        foreach (bool faith in new[] { false, true })
        foreach (bool nature in new[] { false, true })
        foreach (HintAction hint in Enum.GetValues(typeof(HintAction)))
        {
            Check(G(5f, left, faith, nature, hint) == MoodGroup.Lifting, $"a good mood lifts ({left}, {faith}, {nature}, {hint})");
            Check(G(-5f, left, faith, nature, hint) != MoodGroup.Lifting, $"a bad mood never lifts ({left}, {faith}, {nature}, {hint})");
        }

        // ---------- The short line ----------
        Check(MoodSortMath.FallbackOf(sit, false, false) == MoodFallback.WhileItLasts, "while it lasts");
        Check(MoodSortMath.FallbackOf(memory, false, false) == MoodFallback.Fades, "fades in");
        Check(MoodSortMath.FallbackOf(memory, true, false) == MoodFallback.Faith && MoodSortMath.FallbackOf(perm, true, true) == MoodFallback.Faith, "their faith first");
        Check(MoodSortMath.FallbackOf(perm, false, true) == MoodFallback.Lasting, "lasting before nature");
        Check(MoodSortMath.FallbackOf(sit, false, true) == MoodFallback.Nature, "their nature");
        Check(MoodSortMath.ShowsAct(MoodGroup.Fix, true, memory) && MoodSortMath.ShowsAct(MoodGroup.Fix, true, sit), "what to do, in the fix group");
        Check(!MoodSortMath.ShowsAct(MoodGroup.Pass, true, memory) && MoodSortMath.ShowsAct(MoodGroup.Pass, true, sit), "a fading memory says when; a lasting one what ends it");
        Check(MoodSortMath.ShowsAct(MoodGroup.Faith, true, sit) && !MoodSortMath.ShowsAct(MoodGroup.Lifting, true, memory), "faith says what it asks; good moods say when they end");
        Check(!MoodSortMath.ShowsAct(MoodGroup.Fix, false, sit), "no short action, no short action");
        Check(MoodSortMath.Gold(MoodGroup.Fix, true) && !MoodSortMath.Gold(MoodGroup.Faith, true) && !MoodSortMath.Gold(MoodGroup.Fix, false), "only something to do is gold");

        // ---------- Order and totals ----------
        var bad = new List<(float m, string l)> { (-2f, "b"), (-9f, "z"), (-2f, "a"), (-0.5f, "c") };
        MoodSortMath.Sort(bad, MoodGroup.Fix, x => x.m, x => x.l);
        Check(bad.Select(x => x.l).SequenceEqual(new[] { "z", "a", "b", "c" }), "the bad biggest first, then by name: " + string.Join(",", bad.Select(x => x.l)));
        var good = new List<(float m, string l)> { (2f, "b"), (9f, "z"), (2f, "a") };
        MoodSortMath.Sort(good, MoodGroup.Lifting, x => x.m, x => x.l);
        Check(good.Select(x => x.l).SequenceEqual(new[] { "z", "a", "b" }), "the good biggest first, then by name");
        Check(MoodSortMath.LastGone(new int[0]) == -1 && MoodSortMath.LastGone(new[] { 100, 900, 300 }) == 900 && MoodSortMath.LastGone(new[] { 100, sit }) == -1,
            "the last fading mood, unless one has no end in sight");
        Check(MoodSortMath.Largest(new[] { -3f, 12f, -15f }) == 15f && MoodSortMath.Largest(new float[0]) == 0f, "the largest either way");
        Check(MoodSortMath.BarFraction(-15f, 15f) == 1f && Math.Abs(MoodSortMath.BarFraction(3f, 15f) - 0.2f) < 1e-5f && MoodSortMath.BarFraction(4f, 0f) == 0f
              && MoodSortMath.BarFraction(20f, 15f) == 1f, "the faint bars");
        Check(MoodSortMath.ThirdOf(2, 0) == ThirdKind.Faith && MoodSortMath.ThirdOf(0, 2) == ThirdKind.Nature && MoodSortMath.ThirdOf(1, 1) == ThirdKind.Both
              && MoodSortMath.ThirdOf(0, 0) == ThirdKind.Nature, "the third group's name");
        Check(MoodSortMath.Named(0) == 0 && MoodSortMath.Named(1) == 1 && MoodSortMath.Named(4) == 4 && MoodSortMath.Named(5) == 3 && MoodSortMath.Named(9) == 3,
            "the folded good moods name three, never \"and 1 more\"");

        // ---------- A row's layout ----------
        for (float w = 200f; w <= 900f; w += 37f)
        for (float act = 0f; act <= 500f; act += 23f)
        {
            MoodSortMath.RowLayout(w, act, out float lx, out float lw, out float ax, out float aw);
            Check(lx >= MoodSortMath.NumberWidth, $"the label after the number ({w}, {act})");
            Check(aw >= 0f && aw <= act + 0.01f && ax + aw <= w + 0.01f, $"the action inside the row and no wider than it wants ({w}, {act})");
            Check(aw <= 0f || lx + lw + MoodSortMath.Gap <= ax + 0.01f, $"the label and the action don't overlap ({w}, {act})");
            float room = w - lx - 4f;
            Check(aw <= room * MoodSortMath.MaxActShare + 0.01f, $"the action takes at most half ({w}, {act})");
            if (room >= 2f * MoodSortMath.MinLabel + MoodSortMath.Gap) Check(lw >= MoodSortMath.MinLabel - 0.01f, $"the label keeps its room ({w}, {act})");
            if (act <= 0f) Check(aw == 0f && Math.Abs(lw - room) < 0.01f, $"no action: the label has the row ({w})");
        }
        // The Mood tab is 500 wide (480 inside, 464 in the scroll view): a 40-character action fits whole.
        MoodSortMath.RowLayout(464f, 210f, out _, out float labelW464, out _, out float actW464);
        Check(actW464 >= 200f && labelW464 >= 180f, $"in the Mood tab a long action fits beside a label ({actW464}, {labelW464})");
        Check(MoodSortMath.InView(100f, 24f, 90f, 50f) && !MoodSortMath.InView(200f, 24f, 0f, 150f) && MoodSortMath.InView(-10f, 24f, 0f, 100f), "rows in sight");

        // ---------- How many rows show, from a big screen to a small one (TabFitMath's cases) ----------
        var screens = new (float pixels, float scale, float pane, int least, string name)[]
        {
            (1080f, 1f, TabFitMath.VanillaPane, 14, "1080 pixels, plain pane"),
            (1080f, 1f, 400f, 14, "1080 pixels, RimHUD at 400"),
            (1080f, 1.5f, 232f, 9, "1080 pixels at scale 1.5, RimHUD"),
            (1440f, 2f, TabFitMath.VanillaPane, 9, "1440 pixels at scale 2"),
            (768f, 1f, TabFitMath.VanillaPane, 9, "768 pixels, plain pane"),
            (768f, 1f, 232f, 9, "768 pixels, RimHUD"),
            (768f, 1f, 400f, 6, "768 pixels, RimHUD at 400")
        };
        foreach (var s in screens)
        {
            float h = TabFitMath.Height(540f, TabFitMath.PaneTopY(s.pixels, s.scale, s.pane));
            // A two-line state and one row of chips.
            int rows = MoodSortMath.RowsInView(MoodSortMath.ScrollHeight(h, 40f, 22f));
            Check(rows >= s.least, $"{s.name}: {rows} rows in sight in a tab {h} tall (at least {s.least})");
        }
        Check(MoodSortMath.StateFixed(300f) && !MoodSortMath.StateFixed(299f), "the state stays put only with room");
        Check(MoodSortMath.ScrollHeight(100f, 40f, 22f) >= 0f, "never less than nothing");

        // ---------- The short actions ----------
        foreach (string good1 in new[] { "Build a dining table", "Give them a bedroom", "Vary the meals", "Light their workplaces", "Talk it through at a meeting",
                     "Passes with time", "Company helps", "Their faith asks it" })
            Check(MoodSortMath.ActProblem(good1) == null, $"\"{good1}\" is a fine short action");
        foreach (string bad1 in new[] { "", "Build a table.", "Build a table and chairs near where they all eat", "Build\na table", "Wait", "build a table",
                     " Build a table", "Build a table!", "Build a nice big table for them all to share" })
            Check(MoodSortMath.ActProblem(bad1) != null, $"\"{bad1.Replace("\n", "\\n")}\" isn't");

        // ---------- Every hint with a fix has one (Defs/MoodHintDefs) ----------
        int withFix = 0, actionable = 0, fades = 0;
        var defNames = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "MoodHintDefs"), "*.xml"))
        {
            var doc = XDocument.Load(file);
            foreach (var d in doc.Root.Elements().Where(e => e.Name.LocalName.EndsWith("MoodHintDef")))
            {
                string name = (string)d.Element("defName") ?? "?";
                Check(defNames.Add(name), $"{name}: one hint by that name");
                string fix = (string)d.Element("fix"), act = (string)d.Element("act"), flag = (string)d.Element("actionable");
                Check(d.Elements("act").Count() <= 1 && d.Elements("actionable").Count() <= 1, $"{name}: one act and one actionable at most");
                if (string.IsNullOrEmpty(fix))
                {
                    Check(act == null && flag == null, $"{name}: no fix, so no short action and no actionable");
                    continue;
                }
                withFix++;
                Check(act != null, $"{name}: a fix needs a short action (act)");
                if (act == null) continue;
                string problem = MoodSortMath.ActProblem(act);
                Check(problem == null, $"{name}: act \"{act}\" {problem}");
                Check(!Regex.IsMatch(act, @"\b(honour|colour|defence|neighbour|favour|armour|grey)\b", RegexOptions.IgnoreCase), $"{name}: American spelling in \"{act}\"");
                Check(flag == null || flag == "true" || flag == "false", $"{name}: actionable is true or false");
                if (flag == "false") fades++;
                else actionable++;
            }
        }
        Check(withFix >= 184, $"found the hints with a fix ({withFix})");
        Check(actionable >= 60 && fades >= 30, $"both kinds of hint: {actionable} you can act on, {fades} that pass");

        // ---------- The words the pages use ----------
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (var file in new[] { "MoodCompact.cs", "MoodGroups.cs", "ITab_Pawn_Mood.cs", "MoodsPanel.cs" })
        {
            string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Moods", file));
            foreach (Match m in Regex.Matches(src, "\"(CC_MoodC?_\\w+)\""))
                Check(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{file}: the key {m.Groups[1].Value} exists");
        }
        return cases;
    }
}
