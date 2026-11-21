using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Pawns turn to the way they walk (the player, 21 November 2026, of the thorn-guards at a fire: "the pawns were not
/// actually using the walk, animation ... just looking in one direction, and even walking in another direction"). The
/// game's rotation tracker leaves a pawn as its toil turned it while the toil handles its own facing, so a toil of
/// Rimfolk's that handles its facing must never start a walk: the source is read for every toil with
/// <c>handlingFacing = true</c>, and none of its actions may start a path, directly or through a method that does. The
/// safety net (Lean/WalkFacing.cs) and the way a walking pawn faces its step are checked too.
/// </summary>
public static class WalkFacingTests
{
    /// <summary>A walk started for a pawn on the map (a caravan's StartPath takes a tile and no PathEndMode).</summary>
    private static readonly Regex Move = new Regex(@"pather\??\.StartPath\([^;]*PathEndMode", RegexOptions.Compiled);

    private static readonly Regex Decl = new Regex(
        @"^\s*(?:\[[^\]]*\]\s*)*(?:(?:private|public|internal|protected|static|override|virtual|new|sealed)\s+)+[\w<>\[\],.? ]+?\s+(\w+)\s*(?:<[^>]*>)?\s*\([^;]*$",
        RegexOptions.Compiled);

    private static readonly Regex ClassDecl = new Regex(@"\b(?:class|struct)\s+(\w+)", RegexOptions.Compiled);

    private static readonly Regex Facing = new Regex(@"\b(\w+)\.handlingFacing\s*=\s*true\s*;", RegexOptions.Compiled);

    private sealed class Method : IEquatable<Method>
    {
        public string file, cls, name;
        public bool Equals(Method o) => o != null && o.file == file && o.cls == cls && o.name == name;
        public override bool Equals(object o) => Equals(o as Method);
        public override int GetHashCode() => (file + "|" + cls + "|" + name).GetHashCode();
        public override string ToString() => Path.GetFileName(file) + " " + cls + "." + name;
    }

    private static string LineAt(string text, int pos)
    {
        int s = text.LastIndexOf('\n', Math.Max(0, pos - 1)) + 1;
        int e = text.IndexOf('\n', pos);
        return text.Substring(s, (e < 0 ? text.Length : e) - s);
    }

    /// <summary>The method a place in the source is in: the nearest declaration above it, and its class.</summary>
    private static Method Enclosing(string file, string text, int pos)
    {
        string name = null;
        int end = text.LastIndexOf('\n', Math.Max(0, pos - 1));
        for (int lines = 0; end > 0 && lines < 2000 && name == null; lines++)
        {
            int start = text.LastIndexOf('\n', end - 1) + 1;
            var m = Decl.Match(text.Substring(start, end - start));
            if (m.Success && !m.Value.Split('(')[0].Contains("=")) name = m.Groups[1].Value;
            end = start - 1;
        }
        if (name == null) return null;
        string cls = null;
        foreach (Match c in ClassDecl.Matches(text.Substring(0, pos))) cls = c.Groups[1].Value;
        return new Method { file = file, cls = cls, name = name };
    }

    /// <summary>Every method that starts a pawn's walk, or calls one that does (by name in its own file, or as Class.Method).</summary>
    private static HashSet<Method> Movers(Dictionary<string, string> files)
    {
        var movers = new HashSet<Method>();
        foreach (var kv in files)
            foreach (Match m in Move.Matches(kv.Value))
            {
                var e = Enclosing(kv.Key, kv.Value, m.Index);
                if (e != null) movers.Add(e);
            }
        var frontier = new HashSet<Method>(movers);
        for (int depth = 0; depth < 4 && frontier.Count > 0; depth++)
        {
            var found = new HashSet<Method>();
            foreach (var mv in frontier)
            {
                if (mv.name == "MakeNewToils") continue;
                var qualified = mv.cls != null ? new Regex(@"\b" + Regex.Escape(mv.cls) + @"\." + Regex.Escape(mv.name) + @"\s*\(") : null;
                var local = new Regex(@"(?<![\w.])" + Regex.Escape(mv.name) + @"\s*(?:\(|;)");
                foreach (var kv in files)
                {
                    var pats = new List<Regex>();
                    if (qualified != null && kv.Value.Contains(mv.cls + "." + mv.name)) pats.Add(qualified);
                    if (kv.Key == mv.file) pats.Add(local);
                    foreach (var p in pats)
                        foreach (Match m in p.Matches(kv.Value))
                        {
                            if (Decl.IsMatch(LineAt(kv.Value, m.Index))) continue;
                            var e = Enclosing(kv.Key, kv.Value, m.Index);
                            if (e != null && e.name != mv.name && !movers.Contains(e)) found.Add(e);
                        }
                }
            }
            movers.UnionWith(found);
            frontier = found;
        }
        return movers;
    }

    /// <summary>The right side of each action given to a toil (its init, its tick, a pre-action, a finish action).</summary>
    private static List<string> Actions(string region, string toil)
    {
        var list = new List<string>();
        var re = new Regex(@"\b" + Regex.Escape(toil) + @"\.(?:initAction|tickAction|tickIntervalAction)\s*=|\b" + Regex.Escape(toil)
                           + @"\.(?:AddPreInitAction|AddPreTickAction|AddPreTickIntervalAction|AddFinishAction)\s*\(");
        foreach (Match a in re.Matches(region))
        {
            int depth = 0, k = a.Index + a.Length;
            for (; k < region.Length; k++)
            {
                char c = region[k];
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']')
                {
                    if (--depth < 0) break;
                }
                else if (c == ';' && depth == 0) break;
            }
            list.Add(region.Substring(a.Index + a.Length, k - a.Index - a.Length));
        }
        return list;
    }

    /// <summary>The toils of a file that handle their facing and can start a walk: where, and what walks.</summary>
    private static List<string> Walkers(string file, string text, HashSet<Method> movers, out int toils)
    {
        var bad = new List<string>();
        toils = 0;
        var local = movers.Where(m => m.file == file).Select(m => m.name).Where(n => n != "MakeNewToils").Distinct().ToList();
        var qualified = movers.Where(m => m.cls != null).Select(m => m.cls + "." + m.name).Distinct().ToList();
        foreach (Match m in Facing.Matches(text))
        {
            toils++;
            string toil = m.Groups[1].Value;
            int from = Math.Max(0, m.Index - 20000);
            var decls = Regex.Matches(text.Substring(from, m.Index - from), @"\b(?:var|Toil)\s+" + Regex.Escape(toil) + @"\s*=");
            int start = decls.Count > 0 ? from + decls[decls.Count - 1].Index : Math.Max(0, m.Index - 3000);
            var y = new Regex(@"yield return\s+" + Regex.Escape(toil) + @"\b").Match(text, m.Index);
            int end = y.Success ? y.Index + y.Length : Math.Min(text.Length, m.Index + 3000);
            var walks = new List<string>();
            foreach (var act in Actions(text.Substring(start, end - start), toil))
            {
                if (Move.IsMatch(act)) walks.Add("StartPath");
                walks.AddRange(local.Where(n => Regex.IsMatch(act, @"(?<![\w.])" + Regex.Escape(n) + @"\b")));
                walks.AddRange(qualified.Where(q => Regex.IsMatch(act, @"\b" + Regex.Escape(q) + @"\s*\(")));
            }
            if (walks.Count > 0)
                bad.Add(Path.GetFileName(file) + " line " + (text.Substring(0, m.Index).Count(ch => ch == '\n') + 1) + " (" + toil + ": " + string.Join(", ", walks.Distinct()) + ")");
        }
        return bad;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pawns turn to the way they walk: " + what);
        }

        // ---------- The way a walking pawn faces its next step, as the game's own ----------
        C(WalkFacingMath.Toward(1, 0) == WalkFacingMath.East && WalkFacingMath.Toward(-1, 0) == WalkFacingMath.West
          && WalkFacingMath.Toward(0, 1) == WalkFacingMath.North && WalkFacingMath.Toward(0, -1) == WalkFacingMath.South,
            "a step east, west, north or south faces that way");
        C(WalkFacingMath.Toward(1, 1) == WalkFacingMath.East && WalkFacingMath.Toward(-1, -1) == WalkFacingMath.West && WalkFacingMath.Toward(-1, 1) == WalkFacingMath.West,
            "a step aslant faces east or west, as the game turns a walker (sideways first)");
        C(WalkFacingMath.Toward(0, 0) == WalkFacingMath.None, "no step, no turn");
        C(WalkFacingMath.North == 0 && WalkFacingMath.East == 1 && WalkFacingMath.South == 2 && WalkFacingMath.West == 3, "the game's Rot4 numbers");

        // ---------- Every toil of Rimfolk's that handles its facing: none starts a walk ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var files = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            files[f] = File.ReadAllText(f);
        }
        var movers = Movers(files);
        string Rel(string f) => f.Substring(src.Length + 1).Replace('\\', '/');
        bool Mover(string file, string cls, string name) => movers.Any(m => Rel(m.file) == file && m.cls == cls && m.name == name);
        C(Mover("PlayOut/PlayOutJobs.cs", "JobDriver_PlayOutVisit", "Stay") && Mover("PlayOut/PlayOutGroupJobs.cs", "JobDriver_PlayOutGroup", "Tick")
          && Mover("Visitors/Borne.cs", "JobDriver_VisitBear", "Step") && Mover("Animals/AnimalDayJobs.cs", "JobDriver_AnimalPlay", "Bound"),
            "the walks are found, in the toils and in the methods they call (" + movers.Count + " methods)");
        C(!movers.Any(m => Rel(m.file) == "Diplomacy/WardsAbroad.cs"), "a caravan's journey is no pawn's walk");
        int toils = 0;
        var bad = new List<string>();
        foreach (var kv in files)
        {
            bad.AddRange(Walkers(kv.Key, kv.Value, movers, out int t));
            toils += t;
        }
        C(toils >= 100, "the toils that handle their facing were found (" + toils + ")");
        C(bad.Count == 0, "no toil that handles its facing can start a walk" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));

        // The check itself catches one that does, through its own tick or a method it calls.
        string sample = "class JobDriver_X {\n    protected override IEnumerable<Toil> MakeNewToils()\n    {\n        var go = ToilMaker.MakeToil(\"X\");\n"
                        + "        go.tickIntervalAction = delta =>\n        {\n            if (!pawn.pather.Moving) pawn.pather.StartPath(cell, PathEndMode.OnCell);\n        };\n"
                        + "        go.handlingFacing = true;\n        yield return go;\n        var keep = ToilMaker.MakeToil(\"Y\");\n        keep.tickIntervalAction = Follow;\n"
                        + "        keep.handlingFacing = true;\n        yield return keep;\n    }\n\n    private void Follow(int delta)\n    {\n"
                        + "        pawn.pather.StartPath(other, PathEndMode.Touch);\n    }\n}\n";
        var sampleFiles = new Dictionary<string, string> { ["sample.cs"] = sample };
        var sampleBad = Walkers("sample.cs", sample, Movers(sampleFiles), out int sampleToils);
        C(sampleToils == 2 && sampleBad.Count == 2 && sampleBad[1].Contains("Follow"), "the check finds a toil that walks and handles its facing (" + string.Join("; ", sampleBad) + ")");
        string still = sample.Replace("go.handlingFacing = true;", "go.FacingWhileStill();").Replace("keep.handlingFacing = true;", "keep.FacingWhileStill();");
        C(Walkers("sample.cs", still, Movers(new Dictionary<string, string> { ["sample.cs"] = still }), out _).Count == 0,
            "a toil that faces its way only while the pawn stands passes");

        // ---------- The five that walked, now apart ----------
        string Code(string rel) => files[Path.Combine(src, rel.Replace('/', Path.DirectorySeparatorChar))];
        string fire = Code("Peoples/Events6/PeopleEvents6.cs");
        int fight = fire.IndexOf("class JobDriver_PEv6Firefight", StringComparison.Ordinal);
        C(fight > 0 && fire.IndexOf("rotateToFace = TargetIndex.B;", fight, StringComparison.Ordinal) > 0 && fire.IndexOf("job.targetB = fire.Position;", fight, StringComparison.Ordinal) > 0
          && fire.IndexOf("handlingFacing", fight, StringComparison.Ordinal) < 0,
            "the thorn-guards run to a fire turned to their path, and face the fire's cell (their target to face) beating it");
        string animals = Code("Animals/AnimalDayJobs.cs");
        int meet = animals.IndexOf("MakeToil(\"AnimalRunToMeet\")", StringComparison.Ordinal);
        C(meet > 0 && animals.IndexOf("meet.handlingFacing", meet, StringComparison.Ordinal) < 0, "an animal keeping at someone's side faces them (target A) and its path, the game's way");
        string outsiders = Code("Outsiders/OutsiderJobs.cs");
        int bout = outsiders.IndexOf("MakeToil(\"OutsiderBoutFight\")", StringComparison.Ordinal);
        C(bout > 0 && outsiders.IndexOf("fight.handlingFacing", bout, StringComparison.Ordinal) < 0, "a bout's fighters close in turned to their path");
        C(Code("PlayOut/PlayOutJobs.cs").Contains("stay.FacingWhileStill();") && Code("PlayOut/PlayOutGroupJobs.cs").Contains("toil.FacingWhileStill();"),
            "keeping someone company and a walk together face their way only while they stand");

        // ---------- The safety net ----------
        string net = Code("Lean/WalkFacing.cs");
        C(net.Contains("[HarmonyPatch(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.UpdateRotation))]") && net.Contains("public static void Postfix(Pawn ___pawn)"),
            "a postfix on the game's rotation tracker, after its own turn");
        C(net.Contains("driver.GetType().Assembly == Ours") && net.Contains("!driver.HandlingFacing || !Rimfolks(driver)"),
            "only for a job of Rimfolk's whose toil handles its facing; the game's and other mods' jobs never");
        C(net.Contains("pather.curPath.NodesLeftCount < 1") && net.Contains("useFixedRotation") && net.Contains("DisableRotation") && net.Contains("busy.focusTarg.IsValid"),
            "only while walking a path, and not while aiming, stunned so it can't turn, or of a kind that always faces one way");
        C(net.Contains("WalkFacingMath.Toward(step.x, step.z)") && net.Contains("!RimfolkPatches.On"), "turned to the next step, as the game turns a walker; off with the deeper hooks");
        C(net.Contains("toil.AddPreTickAction(") && net.Contains("toil.handlingFacing = p?.pather == null || !p.pather.Moving;"),
            "facing its own way only while still: handled each tick, given to the game while the pawn walks");
        return n;
    }
}
