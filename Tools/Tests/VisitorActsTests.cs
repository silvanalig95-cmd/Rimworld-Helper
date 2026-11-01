using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// What visitors do with the colony's land and things (Source/ColonyCouncil/Visitors/VisitorActs.cs and VisitorActsMath):
/// hunters take only fair game and never shoot near anyone, a hunting party takes a beast or two, the name of the dead
/// says who they were; and from the defs, the jobs and lines are there, mourners who carry their dead are said to carry
/// a body (no cart, no drum, no ashes), and hunters warned off the land go.
/// </summary>
public static class VisitorActsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors' acts: " + what);
        }

        // ---------------- Fair game ----------------

        C(VisitorActsMath.Fits(true, false, 1f, true, false, false, 0f, false), "a wild deer of this land is fair game");
        C(!VisitorActsMath.Fits(false, false, 1f, true, false, false, 0f, true), "never the colony's or anyone's animal");
        C(!VisitorActsMath.Fits(true, true, 1f, true, false, false, 0f, true), "never a predator");
        C(!VisitorActsMath.Fits(true, false, 4f, true, false, false, 0f, true), "nothing as big as a thrumbo or an elephant");
        C(!VisitorActsMath.Fits(true, false, 1f, false, false, false, 0f, true), "nothing rare that doesn't live wild here");
        C(!VisitorActsMath.Fits(true, false, 1f, true, true, false, 0f, true), "never one the colony marked for its hunters or tamers");
        C(!VisitorActsMath.Fits(true, false, 1f, true, false, true, 0f, true), "never one that was tame once");
        C(!VisitorActsMath.Fits(true, false, 1f, true, false, false, 0.1f, false), "one that might turn on them, only when it's kept from it");
        C(VisitorActsMath.Fits(true, false, 1f, true, false, false, 0.5f, true), "kept from turning on them, a touchier beast is fair game too");
        C(VisitorActsMath.Fits(true, false, VisitorActsMath.MaxPreySize, true, false, false, 0f, false) && VisitorActsMath.MaxPreySize <= 2.5f, "a deer-sized beast at most");

        // ---------------- The line of fire ----------------

        C(VisitorActsMath.InLineOfFire(0, 0, 30, 0, 32, 5), "someone near the beast is in the way");
        C(VisitorActsMath.InLineOfFire(0, 0, 30, 0, 15, 2), "someone on the line between them is in the way");
        C(!VisitorActsMath.InLineOfFire(0, 0, 30, 0, 15, 10), "someone well to the side isn't");
        C(!VisitorActsMath.InLineOfFire(0, 0, 30, 0, -20, 0), "someone far behind the hunter isn't");
        C(VisitorActsMath.InLineOfFire(5, 5, 5, 5, 6, 6), "a hunter on top of the beast: anyone near it is in the way");
        C(VisitorActsMath.ClearOfTarget >= 10f && VisitorActsMath.ClearOfLine >= 3f, "the clearance is wide enough for a missed shot");

        // ---------------- How many ----------------

        C(VisitorActsMath.KillsAllowed(0) == 1, "a party passing takes one beast");
        C(VisitorActsMath.KillsAllowed(2) == 3, "one more for each night they stay");
        C(VisitorActsMath.KillsAllowed(-1) == 1, "odd nights count as none");
        C(VisitorActsMath.HoldHours > 0 && VisitorActsMath.HoldHours <= 4, "a hunt holds them a little, never long");

        // ---------------- The dead's name ----------------

        C(VisitorActsMath.GenderOfName("Grandfather Tobin") == 1, "a grandfather is a man");
        C(VisitorActsMath.GenderOfName("Mother Aldry") == 2, "a mother is a woman");
        C(VisitorActsMath.GenderOfName("old Maren") == 0 && VisitorActsMath.GenderOfName("the singer Corwen") == 0, "a name that doesn't say, doesn't");
        C(VisitorActsMath.GenderOfName(null) == 0 && VisitorActsMath.GenderOfName("") == 0, "no name says nothing");

        if (root == null) return n;

        // ---------------- The defs ----------------

        var acts = XDocument.Load(Path.Combine(root, "Defs/Misc/Rimfolk_VisitorActs.xml"));
        foreach (var (def, driver) in new[] { ("Rimfolk_VisitPlay", "JobDriver_VisitPlay"), ("Rimfolk_VisitDress", "JobDriver_VisitDress"), ("Rimfolk_VisitBear", "JobDriver_VisitBear") })
        {
            var j = acts.Root!.Elements("JobDef").FirstOrDefault(e => e.Element("defName")?.Value == def);
            C(j != null && j.Element("driverClass")?.Value == "ColonyCouncil." + driver, $"{def} is there with its driver");
        }
        var src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorHunt.cs"));
        foreach (var def in new[] { "Rimfolk_VisitPlay", "Rimfolk_VisitDress", "Rimfolk_VisitBear" }) C(src.Contains("public static JobDef " + def + ";"), $"{def} is in the code's DefOf");

        var pool = XDocument.Load(Path.Combine(root, "Defs/FlavorPoolDefs/Rimfolk_VisitorActLines.xml")).Descendants("li").Select(l => l.Value).ToList();
        foreach (var moment in new[] { "play", "kill", "dress" })
            C(pool.Any(l => Regex.IsMatch(l, @"^\[([a-zA-Z]+,)*" + moment + @"(,[a-zA-Z]+)*\]")), $"visitors have lines for {moment}");
        C(Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorActs.cs")), @"LinesPool = ""Rimfolk_VisitorActLines"""), "the code says its lines from that pool");
        C(File.ReadAllText(Path.Combine(root, "Languages/English/Keyed/Rimfolk_VisitorActs.xml")).Contains("<CC_VisitAct_Kill>"), "the message for a kill is there");

        // Mourners who carry their dead are said to carry a body: no cart, no drum, no ashes.
        int bearers = 0;
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
            foreach (var e in doc.Descendants("li").Where(l => (string)l.Attribute("Class") == "ColonyCouncil.VisitEffect_Come" && l.Element("bearDead")?.Value == "true"))
            {
                bearers++;
                C(e.Element("what")?.Value == "mourners", $"{Path.GetFileName(file)}: only mourners carry their dead");
                var beat = e.Parent?.Parent;
                string words = string.Join(" ", beat?.Descendants().Where(d => d.Name == "li" && d.Parent?.Name == "texts" || d.Name == "outcome").Select(d => d.Value) ?? Enumerable.Empty<string>());
                C(!Regex.IsMatch(words, @"\b(cart|drum|ashes)\b"), $"{Path.GetFileName(file)}: the passing mourners' words show no cart, drum or ashes");
            }
            foreach (var k in doc.Root!.Elements("ColonyCouncil.CampKindDef").Where(k => k.Element("bearsDead")?.Value == "true"))
            {
                bearers++;
                string words = string.Join(" ", k.Descendants("li").Select(l => l.Value));
                C(!Regex.IsMatch(words, @"\bashes\b"), $"{k.Element("defName")?.Value}: a camp that carries a body says nothing of ashes");
            }
        }
        C(bearers >= 2, $"mourners carrying their dead are found ({bearers})");
        var thoughts = File.ReadAllText(Path.Combine(root, "Defs/WorldSituationDefs/Rimfolk_WS_Beyond_Thoughts.xml"));
        var stood = Regex.Match(thoughts, @"<defName>Council_WS_StoodWithMourners</defName>.*?</ThoughtDef>", RegexOptions.Singleline).Value;
        C(stood.Length > 0 && !Regex.IsMatch(stood, @"\b(cart|drum)\b"), "standing by the road remembers no cart and no drum");

        // Hunters warned off the colony's land go.
        var world = XDocument.Load(Path.Combine(root, "Defs/WorldSituationDefs/Rimfolk_WS_World.xml"));
        var warned = world.Descendants("li").FirstOrDefault(o => o.Element("label")?.Value == "Warn them off the colony's land");
        C(warned != null && warned.Descendants("li").Any(l => (string)l.Attribute("Class") == "ColonyCouncil.VisitEffect_Go" && l.Element("what")?.Value == "hunters"),
            "hunters warned off the land are sent home");
        return n;
    }
}
