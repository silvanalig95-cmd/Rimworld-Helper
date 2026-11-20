using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Every word a camp kind has for its good turns and its moments is one the code says (20 November 2026: the camps' own
/// lines for medicine, wood, mending and meat were never shown, the shared ones always were, and moment lines sat in the
/// defs with no code to say them). Here are the good turns the code does, each told through Camp.HelpText, and the moments
/// it says (Camp.SayMoment), each with what a kind needs for it to happen; both lists are checked against the code, and
/// every camp kind's help lines and moment lines against them.
/// </summary>
public static class CampWordsSaidTests
{
    /// <summary>
    /// The good turns Camp.DoHelp does (CampLife.cs; MoreHelp in CampKinds.cs, LandHelp in CampKindsLand.cs, SeekHelp in
    /// CampSeekers.cs), each told through HelpText in the kind's own words ([meat], [wood]...) or the shared line.
    /// </summary>
    private static readonly string[] Helps = { "meat", "wood", "medicine", "mend", "heal", "show", "blessing", "song", "milk", "goods", "tales", "repair", "survey", "fish", "scrap", "book" };

    /// <summary>A good turn that may come as another: a healer with nobody to see to gathers herbs and carries them over ([herbs]).</summary>
    private static readonly Dictionary<string, string[]> HelpAlso = new Dictionary<string, string[]> { ["heal"] = new[] { "herbs" } };

    /// <summary>
    /// The moments the camps' own code says (Camps/), each with what the kind needs for it to happen. A [carry] is said
    /// for every kind, as they carry in what their felling, mining or gathering left lying (CampWork.Fetch), and for a
    /// porter's load (CampWork.Carry); every camp fishes a little where the game has fishing (CampDay.CommonFishJob).
    /// </summary>
    private static bool Said(string tag, XElement k)
    {
        List<string> L(string list) => k.Element(list)?.Elements("li").Select(li => li.Value.Trim()).ToList() ?? new List<string>();
        var help = L("help");
        string work = (string)k.Element("work");
        switch (tag)
        {
            case "kill":
            case "butcher":
                return float.Parse((string)k.Element("huntsPerDay") ?? "0", CultureInfo.InvariantCulture) > 0f || L("works").Contains("Hunt");
            case "carry":
            case "fish":
                return true;
            case "herd": return (string)k.Element("taskAt") == "herd";
            case "repair": return help.Contains("repair") || help.Contains("mend");
            case "round": return (string)k.Element("farewellWalk")?.Element("to") == "sick";
            case "gift": return k.Element("leaveBehind")?.Elements("li").Any() == true;
            case "blessing": return help.Contains("blessing");
            case "watch": return work == "contract" || work == "watch";
            case "ice": return work == "icefish";
            case "strip":
            case "reap":
            case "hull":
            case "cut":
            case "shear":
            case "catch":
                return work == tag;
        }
        return tag.StartsWith("seek:", StringComparison.Ordinal) && L("seeks").Contains(tag.Substring(5));
    }

    private static readonly string[] General =
        { "kill", "butcher", "carry", "fish", "herd", "repair", "round", "gift", "blessing", "watch", "ice", "strip", "reap", "hull", "cut", "shear", "catch" };

    /// <summary>What a [carry] says it carries (SayMoment's what): a porter's goods, or what their own work left lying.</summary>
    private static readonly string[] CarryWhat = { "goods", "wood", "stone", "gathered" };

    /// <summary>The moments each kind with ways of its own says (its CampWorker, in the file that declares it).</summary>
    private static readonly Dictionary<string, string[]> WorkerMoments = new Dictionary<string, string[]>
    {
        ["CampWorker_Hostel"] = new[] { "quarrel", "gift" },
        ["CampWorker_LanternPost"] = new[] { "lantern", "warn", "trail", "track" },
        ["CampWorker_Measure"] = new[] { "read", "measure" },
        ["CampWorker_DryWater"] = new[] { "stray" },
        ["CampWorker_EnclavePad"] = new[] { "offpad", "stock" },
        ["CampWorker_RoadGang"] = new[] { "road" },
        ["CampWorker_Sickhouse"] = new[] { "round" },
        ["CampWorker_Calving"] = new[] { "born", "lost", "found", "search" },
        ["CampWorker_SummerSchool"] = new[] { "settled", "sulk", "dispute" },
        ["CampWorker_FloodedOut"] = new string[0],
        ["CampWorker_BoatBuilders"] = new[] { "hull", "storm" },
        ["CampWorker_FieldOfFallen"] = new[] { "dig", "bury", "ring", "stirs" },
        ["CampWorker_CircuitRing"] = new[] { "challenge", "spar", "won", "lost", "champion" },
        ["CampWorker_WheelYard"] = new[] { "haggle", "stay" },
        ["CampWorker_StormTower"] = new[] { "mast", "storm", "strike", "watch" },
        ["CampWorker_AfterFire"] = new[] { "cut", "plant", "sing", "felled", "fire" },
        ["CampWorker_CompanyMarch"] = new[] { "drill", "caught", "bet" },
        ["CampWorker_WorkThatStays"] = new[] { "short", "crack", "bad", "carve", "done" },
        ["CampWorker_LineCrew"] = new[] { "raise", "wire", "climb", "struck", "flags", "radio" },
        ["CampWorker_Picket"] = new[] { "settled", "vote", "breakers", "song" },
        ["CampWorker_RationPost"] = new[] { "ration", "short", "turned" },
        ["CampWorker_LandingParty"] = new[] { "bell", "deserter", "survey", "raiders" },
        ["CampWorker_FieldLab"] = new[] { "loose", "sample", "gene", "drawn" },
        ["CampWorker_MechYard"] = new[] { "tend", "feral", "band" },
        ["CampWorker_ListeningRing"] = new[] { "strange", "loud", "song" },
        ["CampWorker_SurfaceScouts"] = new[] { "skysick", "out", "learned" },
        ["CampWorker_GodPilgrims"] = new[] { "grief", "pray" },
        ["CampWorker_GodSeer"] = new[] { "foretell" },
        ["CampWorker_Beekeepers"] = new string[0],
        ["CampWorker_MushroomPickers"] = new string[0],
        ["CampWorker_StarWatchers"] = new string[0],
    };

    /// <summary>A line's tags, each with its | alternatives ("[carry,stone|wood] ..." gives carry and stone|wood).</summary>
    private static List<string[]> Tags(string line)
    {
        var m = Regex.Match(line ?? "", @"^\s*\[([^\]]*)\]");
        return m.Success
            ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Select(t => t.Split('|').Select(a => a.Trim()).ToArray()).ToList()
            : new List<string[]>();
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camps' words said: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var files = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);
        string camps = string.Join("\n", files.Where(f => f.Key.Contains(Path.DirectorySeparatorChar + "Camps" + Path.DirectorySeparatorChar)).Select(f => f.Value));
        string all = string.Join("\n", files.Values);
        string Code(string rel) => files[Path.Combine(src, rel)];

        // ================= The good turns, told in the kind's own words =================

        foreach (var h in Helps)
            C(camps.Contains("case \"" + h + "\":") || camps.Contains("= \"" + h + "\";"), $"the code does the good turn [{h}]");
        C(Code("Camps/CampCarry.cs").Contains("StartHerbs(\"herbs\")") || Code("Camps/CampKinds.cs").Contains("StartHerbs(\"herbs\")"), "a healer with nobody to see to carries herbs over [herbs]");
        // The shared lines only through HelpText: every good turn is told in the kind's own words when it has them.
        var shared = Regex.Matches(all, "\"CC_Camp_Help_(?!Title)").Count;
        C(shared == 1 && Code("Camps/CampKinds.cs").Contains("return Text(\"CC_Camp_Help_\" + help, x, a, count, thing);"),
            $"the shared help lines are reached only through HelpText, after the kind's own ({shared} places name them)");
        C(Regex.IsMatch(Code("Camps/CampLife.cs"), @"HelpText\(""meat"",") && Regex.IsMatch(Code("Camps/CampLife.cs"), @"HelpText\(""wood"",")
          && Regex.IsMatch(Code("Camps/CampCarry.cs"), @"HelpText\(""medicine"",") && Regex.IsMatch(Code("Camps/CampRequests.cs"), @"HelpText\(""mend"","),
            "meat, wood, medicine and mending are told through HelpText");

        // ================= The moments, said where the code says them =================

        foreach (var g in General.Where(g => g != "blessing"))
            C(Regex.IsMatch(camps, @"SayMoment\([^;]*""" + g + @""""), $"the camps' code says [{g}]");
        C(Code("Camps/CampKinds.cs").Contains("HasTag(l, \"blessing\")"), "[blessing] lines are the words of the blessing errand");
        C(Code("Camps/CampSeekers.cs").Contains("HasTag(l, \"seek:\" + seeking)"), "[seek:REASON] lines are why they came");
        C(Code("Camps/CampWork.cs").Contains("SayMoment(pawn, \"carry\", 0.3f, \"goods\")"), "a porter's load is [carry,goods]");
        C(Regex.IsMatch(Code("Camps/CampWork.cs"), @"SayMoment\(pawn, ""carry"", [^;]*""wood""[^;]*""gathered""[^;]*""stone""\)"),
            "what their own work left lying is [carry] with wood, gathered or stone");

        // Every camp worker is listed, and says what its list says (in the file that declares it).
        var workerFile = new Dictionary<string, string>();
        foreach (var f in files)
            foreach (Match m in Regex.Matches(f.Value, @"class (CampWorker_\w+) : CampWorker\b"))
                workerFile[m.Groups[1].Value] = f.Key;
        foreach (var w in workerFile.Keys)
            C(WorkerMoments.ContainsKey(w), $"{w} is listed with the moments it says");
        foreach (var (w, tags) in WorkerMoments)
        {
            C(workerFile.ContainsKey(w), $"{w} is a camp worker in the code");
            if (!workerFile.TryGetValue(w, out var wf)) continue;
            foreach (var t in tags) C(files[wf].Contains("\"" + t + "\"") && files[wf].Contains("SayMoment("), $"{w} says [{t}]");
        }
        // And nothing is said that the lists don't know: each moment named in a SayMoment is a camp moment or the worker's own.
        foreach (var f in files)
        {
            var known = new HashSet<string>(General.Concat(CarryWhat));
            foreach (var w in workerFile.Where(x => x.Value == f.Key)) known.UnionWith(WorkerMoments.TryGetValue(w.Key, out var t) ? t : new string[0]);
            foreach (Match m in Regex.Matches(f.Value, @"SayMoment\(([^;]*?)\);"))
                foreach (Match lit in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                    C(known.Contains(lit.Groups[1].Value), $"{Path.GetFileName(f.Key)} says [{lit.Groups[1].Value}], a moment the lists know");
        }

        // ================= Every camp kind's words =================

        var docs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        var kinds = docs.SelectMany(d => d.Root!.Elements("ColonyCouncil.CampKindDef")).Where(k => k.Element("defName") != null).ToList();
        // Words in a file of their own (a FlavorExtensionDef with camp) are joined to the kind's (CampKindDef.AddWords).
        var extra = docs.SelectMany(d => d.Root!.Elements("ColonyCouncil.FlavorExtensionDef")).Where(e => e.Element("camp") != null)
            .GroupBy(e => (string)e.Element("camp")).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Element("momentLines")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select(li => li.Value).ToList());
        C(kinds.Count >= 70, $"every camp kind is read ({kinds.Count})");
        foreach (var k in kinds)
        {
            string name = (string)k.Element("defName");
            var help = k.Element("help")?.Elements("li").Select(li => li.Value.Trim()).ToList() ?? new List<string>();
            foreach (var h in help) C(Helps.Contains(h), $"{name}: its good turn [{h}] is one the code does");
            var helpTags = new HashSet<string>(help.Concat(help.SelectMany(h => HelpAlso.TryGetValue(h, out var also) ? also : new string[0])));
            foreach (var l in k.Element("helpTexts")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>())
                C(Tags(l).Any(t => t.Any(helpTags.Contains)), $"{name}: a help line for a good turn it does: {l}");

            string worker = ((string)k.Element("workerClass"))?.Replace("ColonyCouncil.", "");
            var own = worker != null && WorkerMoments.TryGetValue(worker, out var wt) ? wt : new string[0];
            bool porters = k.Element("campGoods")?.Elements("li").Any(g => g.Element("carryTo") != null) == true;
            var moments = (k.Element("momentLines")?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>())
                .Concat(extra.TryGetValue(name, out var ext) ? ext : new List<string>());
            foreach (var l in moments)
            {
                var tags = Tags(l);
                C(tags.Any(t => t.Any(a => own.Contains(a) || Said(a, k))), $"{name}: a moment line said where the code says it: {l}");
                if (!tags.Any(t => t.Contains("carry"))) continue;
                // A [carry] says what it carries where it matters: a porter's goods only with porters, and a porters' kind
                // says which of its lines are the porters' (else they would be said over the logs it carries in).
                var what = tags.Where(t => t.All(CarryWhat.Contains)).SelectMany(t => t).ToList();
                C(!what.Contains("goods") || porters, $"{name}: [carry,goods] only with porters' goods: {l}");
                C(!porters || what.Count > 0, $"{name}: a porters' kind says what its [carry] line is about: {l}");
            }
        }
        return n;
    }
}
