using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Phase 4 of the review, part 2: a fairer council (the player's decisions 8, 9, 10 and 13 of 6 November 2026). The seats'
/// names by the colony's age (Offices/OfficeNamesMath.cs): every seat named in every age, a shipped line said with the
/// age's name and its article put right, and nothing else in it touched.
/// </summary>
public static class FairCouncilTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "fair council: " + what);
        }

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        Names(defs, Check);
        return cases;
    }

    // ---------------- Decision 10: office names by the colony's age ----------------

    private static void Names(List<XElement> defs, Action<bool, string> Check)
    {
        var offices = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")).Select(o => o.Element("defName")!.Value).ToList();
        var words = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeWordsDef")).ToList();
        foreach (var o in offices)
        {
            var w = words.FirstOrDefault(x => (string?)x.Element("office") == o);
            Check(w != null, $"{o} has its words");
            if (w == null) continue;
            var names = new[] { "tribal", "medieval", "industrial", "spacer" }.Select(a => (string?)w.Element(a) ?? "").ToList();
            Check(names.All(n => n.Length > 0 && n == n.ToLowerInvariant() && !n.Contains('!')), $"{o} has a name in every age, plain and lower case ({string.Join(", ", names)})");
            Check(names[0] != names[3] || o.EndsWith("Chaplain"), $"{o}: a tribal colony and a spacer one don't call the seat the same");
            string label = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")).First(x => x.Element("defName")!.Value == o).Element("label")!.Value;
            Check(names[1] == label, $"{o}: the medieval name is the seat's own label, so a medieval colony reads what it always did");
        }
        var spacer = words.Select(w => (string?)w.Element("spacer")).ToList();
        Check(!spacer.Contains("reeve"), "a spacer colony doesn't elect a reeve");
        Check(spacer.Distinct().Count() == spacer.Count, "no two seats share a name in the spacer age");
        var industrial = words.Select(w => (string?)w.Element("industrial")).ToList();
        Check(industrial.Distinct().Count() == industrial.Count, "no two seats share a name in the industrial age");
        var tribal = words.Select(w => (string?)w.Element("tribal")).ToList();
        Check(tribal.Distinct().Count() == tribal.Count, "no two seats share a name in the tribal age");

        var names4 = new List<string> { "seed-keeper", "reeve", "farm foreman", "agronomist" };
        Check(OfficeNamesMath.NameIn(names4, WorldAge.Spacer, "reeve") == "agronomist" && OfficeNamesMath.NameIn(names4, WorldAge.Tribal, "reeve") == "seed-keeper",
            "the age's name");
        Check(OfficeNamesMath.NameIn(new List<string> { "", "reeve", "farm foreman", "" }, WorldAge.Spacer, "x") == "farm foreman", "an age with no name takes the nearest age's");
        Check(OfficeNamesMath.NameIn(null, WorldAge.Spacer, "reeve") == "reeve", "a seat with no words keeps its own label");

        var pairs = new List<(string, string)> { ("reeve", "agronomist"), ("speaker", "coordinator"), ("keeper of days", "ceremonies officer"), ("elder", "senior councillor"), ("chaplain", "chaplain") };
        Check(OfficeNamesMath.Rename("Next time I'll be reeve, and there will be a plan.", pairs) == "Next time I'll be agronomist, and there will be a plan.", "a seat's old name in a line takes the age's");
        Check(OfficeNamesMath.Rename("I'd make a good reeve.", pairs) == "I'd make a good agronomist.", "a word between the article and the seat is left alone");
        Check(OfficeNamesMath.Rename("I'd make a reeve of anyone.", pairs) == "I'd make an agronomist of anyone.", "a, an: the article fits the new name");
        Check(OfficeNamesMath.Rename("Reeve. Good.", pairs) == "Agronomist. Good.", "a capital stays a capital");
        Check(OfficeNamesMath.Rename("{PAWN_nameDef}, one of the colony's elders, sat down.", pairs) == "{PAWN_nameDef}, one of the colony's senior councillors, sat down.", "the plural too");
        Check(OfficeNamesMath.Rename("The keeper of days says so.", pairs) == "The ceremonies officer says so.", "a name of several words, whole");
        Check(OfficeNamesMath.Rename("The speakers' corner, the loudspeaker, {SPEAKER_nameDef} and the chaplain.", pairs)
              == "The coordinators' corner, the loudspeaker, {SPEAKER_nameDef} and the chaplain.", "only whole words: never part of a word or a placeholder");
        Check(OfficeNamesMath.Rename("A reeve came.", pairs) == "An agronomist came.", "a capital article fits too");
        Check(OfficeNamesMath.Rename("The reeve's friends eat better.", pairs) == "The agronomist's friends eat better.", "a seat's possessive");
        Check(OfficeNamesMath.Plural("captain of the guard") == "captains of the guard" && OfficeNamesMath.Plural("head stockman") == "head stockmen"
              && OfficeNamesMath.Plural("security chief") == "security chiefs", "plurals of the seats' names");
        Check(OfficeNamesMath.Article("elder") == "an" && OfficeNamesMath.Article("reeve") == "a", "a and an");
    }
}
