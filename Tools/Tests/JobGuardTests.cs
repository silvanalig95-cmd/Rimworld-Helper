using System.Text.RegularExpressions;

/// <summary>
/// No job of no kind (the player, 20 November 2026: the map grey while time ran after a medal at the gate, and a pawn drawn
/// as its name only). Three of Rimfolk's lists of its own jobs lacked the game's [DefOf] mark, so their fields stayed null
/// and the jobs made from them had no kind. Every class that asks the game to fill it (EnsureInitializedInCtor) carries the
/// mark, every field of Rimfolk's own defs in such a class names a def in Defs/, and the guard and the mending at load are
/// in place (Lean/JobGuard.cs).
/// </summary>
public static class JobGuardTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "jobs of no kind: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var defNames = new HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"<defName>([^<]+)</defName>"))
                defNames.Add(m.Groups[1].Value.Trim());

        int classes = 0;
        var unmarked = new List<string>();
        var missing = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            string text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"EnsureInitializedInCtor\(typeof\((\w+)\)\)"))
            {
                string cls = m.Groups[1].Value;
                classes++;
                var decl = Regex.Match(text, @"(\[DefOf\]\s*)?public static class " + cls + @"\b");
                if (!decl.Success || !decl.Groups[1].Success)
                {
                    unmarked.Add(cls + " (" + Path.GetFileName(f) + ")");
                    continue;
                }
                // The class body: its fields up to the static constructor.
                int start = decl.Index, end = text.IndexOf("static " + cls + "()", start, StringComparison.Ordinal);
                if (end < 0) continue;
                var lines = text.Substring(start, end - start).Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var fm = Regex.Match(lines[i], @"public static \w+Def (\w+);");
                    if (!fm.Success) continue;
                    string name = fm.Groups[1].Value;
                    bool soft = i > 0 && lines[i - 1].Contains("MayRequire");
                    bool ours = name.StartsWith("Rimfolk_") || name.StartsWith("Council_") || name.StartsWith("CC_");
                    if (ours && !soft && !defNames.Contains(name)) missing.Add(cls + "." + name);
                }
            }
        }
        C(classes >= 60, "the classes that ask the game to fill their defs were found (" + classes + ")");
        C(unmarked.Count == 0, "every one of them carries [DefOf]" + (unmarked.Count > 0 ? ": missing on " + string.Join(", ", unmarked) : ""));
        C(missing.Count == 0, "every field of Rimfolk's own defs in them names a def" + (missing.Count > 0 ? ": none for " + string.Join(", ", missing) : ""));

        string guard = File.ReadAllText(Path.Combine(src, "Lean", "JobGuard.cs"));
        C(guard.Contains("[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]") && guard.Contains("if (__0 == null || __0.def != null) return true;"),
            "a job of no kind is refused when it would start");
        C(guard.Contains("jobs.curJob.def == null || jobs.curDriver == null") && guard.Contains("jobs.jobQueue.RemoveAll(p, j => j == null || j.def == null)"),
            "a pawn stuck with one, or with one queued, is mended");
        C(guard.Contains("p.GetLord() == null") && guard.Contains("Arrivals.SendAway(new[] { p });"), "a visitor mended with no group to leave with goes home");
        string comp = File.ReadAllText(Path.Combine(src, "CouncilGameComponent.cs"));
        C(comp.Contains("JobGuard.Repair();"), "mended when a game loads");
        return n;
    }
}
