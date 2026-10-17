using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Marks, what happens to people and how it changes them (Persona/MarksMath.cs, Persona/Marks.cs and the mark defs):
/// a mark's conditions and leans are read right, the mark a person gets depends on their nature (the bold harden, the
/// timid are shaken), the limits keep marks rare (one small mark per few days, a turning point never twice within its
/// keeping, one happening one mark), the soft factors stay small, and the defs hang together: every family a hook
/// names exists, every family has a mark anyone can get, every turning point has journal lines in several voices, and
/// the traits read from other mods are the mods' own.
/// </summary>
public static class MarkTests
{
    private const int Day = MarksMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "marks: " + what);
        }

        // ---------- Reading a mark's conditions ----------
        Check(MarksMath.TryParseWhen("nerve>=10, warmth<=-20", out var conds, out _) && conds.Count == 2
              && conds[0].axis == 1 && conds[0].atLeast && conds[0].value == 10f && conds[1].axis == 0 && !conds[1].atLeast && conds[1].value == -20f,
            "conditions read side, direction and number");
        Check(MarksMath.TryParseWhen("", out var none, out _) && none.Count == 0, "no condition is a condition anyone meets");
        Check(!MarksMath.TryParseWhen("courage>=10", out _, out string e1) && e1 != null, "a side that doesn't exist is an error");
        Check(!MarksMath.TryParseWhen("nerve=10", out _, out _) && !MarksMath.TryParseWhen("nerve>=ten", out _, out _), "a condition without > or < or a number is an error");
        Check(MarksMath.TryParseLean("nerve:-0.6, humor:0.2", out var lean, out _) && lean.Count == 2 && lean[1].axis == 3, "leans read, 'humor' as humour");
        Check(!MarksMath.TryParseLean("nerve:5", out _, out _) && !MarksMath.TryParseLean("nerve", out _, out _), "a lean is side:number within -2..2");

        // ---------- Choosing by nature ----------
        var bold = new float[] { 0, 40, 0, 0, 0, 0, 0, 0 };
        var timid = new float[] { 0, -40, 0, 0, 0, 0, 0, 0 };
        MarksMath.TryParseWhen("nerve>=10", out var hardened, out _);
        var downed = new List<List<MarkCond>> { hardened, new List<MarkCond>() };
        Check(MarksMath.Choose(downed, bold) == 0 && MarksMath.Choose(downed, timid) == 1, "two people downed in one raid: the bold harden, the timid are shaken");
        Check(MarksMath.Choose(new List<List<MarkCond>> { hardened }, timid) == -1, "a family without a mark for someone gives them none");
        MarksMath.TryParseLean("nerve:-0.5", out var shaken, out _);
        float t = MarksMath.Scale(shaken, timid), b = MarksMath.Scale(shaken, bold);
        Check(Math.Abs(t - 1.2f) < 0.001f && Math.Abs(b - 0.8f) < 0.001f, "a lean makes the timid take a fright harder than the bold");
        MarksMath.TryParseLean("nerve:2", out var huge, out _);
        Check(MarksMath.Scale(huge, new float[] { 0, 100, 0, 0, 0, 0, 0, 0 }) == MarksMath.MaxScale
              && MarksMath.Scale(huge, new float[] { 0, -100, 0, 0, 0, 0, 0, 0 }) == MarksMath.MinScale, "nature scales a mark only so far");

        // ---------- The limits ----------
        int now = 100 * Day;
        MarkRecord Rec(string def, bool major, int daysAgo, string about = "x", string story = null) =>
            new MarkRecord { def = def, major = major, tick = now - daysAgo * Day, about = about, story = story ?? def };
        var minorNow = Rec("B", false, 0);
        Check(MarksMath.Blocked(new[] { Rec("A", false, 1) }, minorNow, 20, now) == "minor", "one small mark per few days");
        Check(MarksMath.Blocked(new[] { Rec("A", false, 4) }, minorNow, 20, now) == null, "a small mark can come once the gap is over");
        Check(MarksMath.Blocked(new[] { Rec("A", false, 1) }, Rec("C", true, 0), 120, now) == null, "a turning point doesn't wait for a small mark");
        Check(MarksMath.Blocked(new[] { Rec("C", true, 50, "y") }, Rec("C", true, 0), 120, now) == "repeat", "a turning point never twice within its keeping");
        Check(MarksMath.Blocked(new[] { Rec("C", true, 130, "y") }, Rec("C", true, 0), 120, now) == null, "after its keeping it can come again");
        Check(MarksMath.Blocked(new[] { Rec("C", true, 300) }, Rec("C", true, 0), 120, now) == "same", "the same mark about the same thing never twice while it's on them");
        var friend = Rec("LostFriend", true, 0, "#5", "Loss");
        Check(MarksMath.Blocked(new[] { Rec("LostKin", true, 0, "#5", "Loss") }, friend, 180, now) == "story", "one death leaves one mark, however the game tells it");
        Check(MarksMath.Blocked(new[] { Rec("LostKin", true, 3, "#6", "Loss") }, friend, 180, now) == null, "another death is another story");
        Check(MarksMath.Blocked(new[] { Rec("P", true, 0), Rec("Q", true, 0) }, Rec("R", true, 0), 120, now) == "day", "at most two turning points in a day");
        Check(MarksMath.Blocked(new[] { Rec("C", true, 0) }, Rec("C", true, 0, "z"), 100000, now) == "repeat", "a mark kept for good never repeats");
        Check(MarksMath.CountWithin(new List<int> { now - 70 * Day, now - 50 * Day, now - 2 * Day, now }, now, 60f) == 3, "fights counted within the last year only");

        // ---------- Soft factors ----------
        Check(MarksMath.MomentLean(100f, 100f, 0f, false, false) > 1.2f && MarksMath.MomentLean(-100f, -100f, 0f, false, false) < 0.8f,
            "the warm and playful start more warm moments, the cold and dour fewer");
        Check(MarksMath.MomentLean(0f, 0f, 100f, true, false) > 1.2f && MarksMath.MomentLean(0f, 0f, -100f, true, false) < 0.8f, "the hot-tempered start more frictions");
        Check(MarksMath.MomentLean(80f, 0f, 0f, false, true) > MarksMath.MomentLean(80f, 0f, 0f, false, false), "the warm comfort more in grief");
        bool small = true;
        for (float v = -100f; v <= 100f; v += 10f)
        {
            foreach (var f in new[] { MarksMath.MomentLean(v, v, v, false, true), MarksMath.MomentLean(v, v, v, true, false), MarksMath.ConfideFactor(v), MarksMath.MentorWeight(v) })
                small &= f >= 0.4f && f <= 1.6f;
        }
        Check(small, "character only ever nudges the other systems");
        Check(MarksMath.ConfideFactor(-80f) < MarksMath.ConfideFactor(0f) && MarksMath.ConfideFactor(0f) == 1f, "the suspicious confide later");
        Check(MarksMath.MentorWeight(70f) > MarksMath.MentorWeight(0f), "the warm take on students sooner");

        // ---------- The defs ----------
        var marks = new List<XElement>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "PersonaDefs"), "*Marks*.xml"))
            marks.AddRange(XDocument.Load(file).Root!.Elements("ColonyCouncil.PersonaMarkDef"));
        Check(marks.Count >= 35 && marks.Count <= 60, $"about forty marks ({marks.Count})");
        var families = new Dictionary<string, List<XElement>>();
        var thoughtsUsed = new HashSet<string>();
        foreach (var m in marks)
        {
            string name = m.Element("defName")?.Value ?? "?";
            var x = m.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.PersonaMarkExtension");
            Check(x != null, name + " says when it comes");
            if (x == null) continue;
            string fam = x.Element("family")?.Value ?? name;
            if (!families.TryGetValue(fam, out var list)) families[fam] = list = new List<XElement>();
            list.Add(m);
            Check(MarksMath.TryParseWhen(x.Element("when")?.Value, out _, out _) && MarksMath.TryParseLean(x.Element("lean")?.Value, out _, out _), name + " has conditions and leans that read");
            string shaped = m.Element("shaped")?.Value ?? "";
            Check(shaped.Length > 0 && !shaped.Contains("{OTHER_"), name + ": what shaped them names the other person by name only ({OTHER})");
            Check(!shaped.Contains("{OTHER}") || x.Element("needsOther")?.Value == "true" || CodeGivesOther(fam), name + ": {OTHER} only where there is someone");
            foreach (var li in x.Element("thoughts")?.Elements("li") ?? Enumerable.Empty<XElement>()) thoughtsUsed.Add(li.Value);
            if (m.Element("major")?.Value == "true")
            {
                var j = x.Element("journal");
                int plain = j?.Element("plain")?.Elements("li").Count() ?? 0;
                int voices = j?.Elements().Count(e => e.Elements("li").Any()) ?? 0;
                Check(plain >= 3 && voices >= 4, name + ": a turning point is written in their journal, in several voices");
            }
        }
        foreach (var (fam, list) in families)
        {
            var last = list.OrderBy(m => int.TryParse(Ext(m)?.Element("order")?.Value, out int o) ? o : 0).Last();
            Check(string.IsNullOrEmpty(Ext(last)?.Element("when")?.Value) && (Ext(last)?.Element("ages")?.Value ?? "any") == "any",
                "family " + fam + " ends with a mark anyone can get");
        }

        // Every family the code names is one the defs have.
        string code = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Persona", "Marks.cs"));
        foreach (Match mt in Regex.Matches(code, @"Give\([^,]+, ""([A-Za-z]+)"""))
            Check(families.ContainsKey(mt.Groups[1].Value), "the hooks' family " + mt.Groups[1].Value + " has marks");
        foreach (Match mt in Regex.Matches(code, @"\? ""([A-Za-z]+)"" : ""([A-Za-z]+)"""))
            Check(families.ContainsKey(mt.Groups[1].Value) && families.ContainsKey(mt.Groups[2].Value), "the hooks' families " + mt.Value + " have marks");

        // Rimfolk's own memories a mark listens for are Rimfolk's.
        var rimfolkThoughts = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (Match mt in Regex.Matches(File.ReadAllText(file), @"<defName>(Council_[A-Za-z0-9_]+)</defName>"))
                rimfolkThoughts.Add(mt.Groups[1].Value);
        foreach (var th in thoughtsUsed.Where(n => n.StartsWith("Council_")))
            Check(rimfolkThoughts.Contains(th), "the memory " + th + " exists");

        // Other mods' traits are names those mods really have.
        var known = new HashSet<string>(File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "ModDefNames.txt")).Where(l => !l.StartsWith("#")).Select(l => l.Trim()));
        int modded = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "PersonaDefs"), "Rimfolk_PersonaNature_*.xml"))
            foreach (var li in XDocument.Load(file).Descendants("trait"))
            {
                modded++;
                Check(known.Contains(li.Value), "the trait " + li.Value + " is in its mod's own defs (ModDefNames.txt)");
            }
        Check(modded >= 40, $"characters read the traits of the player's trait mods ({modded})");
        return cases;
    }

    private static XElement Ext(XElement mark) =>
        mark.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.PersonaMarkExtension");

    /// <summary>Families whose hooks always pass someone along (Persona/Marks.cs).</summary>
    private static bool CodeGivesOther(string family) =>
        family is "SavedSomeone" or "BetrayedSomeone" or "NursedBack" or "BecameParent" or "LostLeader" or "LostFriend" or "SworeVengeance";
}
