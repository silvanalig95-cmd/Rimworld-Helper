using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The player's other mods: every name Rimfolk's defs give another mod's content by (Medieval Overhaul's furniture and
/// dishes, Alpha Genes' xenotypes, Alpha Memes' memes, Outland's peoples and tent flap, Alpha Props' lamps and lawns,
/// Vanilla Factions Expanded - Classical's tent and republics) is one that mod really has: ModDefNames.txt holds them,
/// read from the mods' own defs. A people named by its mod ("mod:athelas.medievaltotalrim") names a mod in one of the
/// player's modlists. And what Rimfolk makes of people who change in the middle of a game (ChangeMath).
/// </summary>
public static class ModsTests
{
    private static readonly Regex Name = new(@"\b(?:AG|AM|Outland|DankPyon|Dank|VFEC|VFEPD_AP|AP)_[A-Za-z0-9_]+");
    private static readonly Regex ModPeople = new(@"\bmod:([a-z0-9_.]+)");

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "mods: " + what);
        }

        // ---------- Every name of another mod's content is one it has ----------
        var known = new HashSet<string>();
        string list = Path.Combine(root, "Tools", "Tests", "ModDefNames.txt");
        Check(File.Exists(list), "ModDefNames.txt is there");
        if (File.Exists(list))
            foreach (var line in File.ReadAllLines(list))
                if (!line.StartsWith("#") && line.Trim().Length > 0) known.Add(line.Trim());
        var packages = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Modlists"), "*.txt"))
            foreach (var line in File.ReadAllLines(file))
                if (!line.StartsWith("#") && line.Trim().Length > 0) packages.Add(line.Trim().ToLowerInvariant());
        int named = 0;
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
            string rel = Path.GetFileName(file);
            // Element text and attribute values only: comments may speak of a family of names ("DankPyon_Royal...").
            foreach (var el in doc.Descendants())
            {
                var texts = el.Nodes().OfType<XText>().Select(t => t.Value).Concat(el.Attributes().Select(a => a.Value));
                foreach (var text in texts)
                {
                    foreach (Match m in Name.Matches(text))
                    {
                        named++;
                        Check(known.Contains(m.Value), $"{rel}: {m.Value} is in the mod's own defs (or add it to ModDefNames.txt from them)");
                    }
                    foreach (Match m in ModPeople.Matches(text))
                    {
                        string prefix = m.Groups[1].Value;
                        Check(packages.Any(p => p.StartsWith(prefix)), $"{rel}: people named by mod:{prefix} come from a mod in the modlists");
                    }
                }
            }
        }
        Check(named > 80, $"found the other mods' names in the defs ({named})");

        // ---------- People who change in the middle of a game ----------
        var before = new ChangeMath.Look
        {
            race = "Human", xenotype = "Baseliner", gender = "Female", stage = "Adult",
            traits = ChangeMath.TraitKey(new[] { "Kind:0", "Industriousness:1" }), name = "Ana Moss", size = 10, inner = true
        };
        Check(ChangeMath.Diff(before, before) == ChangeMath.Change.None, "nothing changed, nothing to do");
        var sameTraits = before;
        sameTraits.traits = ChangeMath.TraitKey(new[] { "Industriousness:1", "Kind:0" });
        Check(ChangeMath.Diff(before, sameTraits) == ChangeMath.Change.None, "traits in another order are the same traits");
        var raced = before;
        raced.race = "BS_Lamia";
        var c = ChangeMath.Diff(before, raced);
        Check(c == ChangeMath.Change.Race && ChangeMath.RecheckWishes(c) && ChangeMath.JournalTag(c, before, raced) == "race", "a new race: wishes asked again, a journal line");
        var xeno = before;
        xeno.xenotype = "AG_Nereid";
        c = ChangeMath.Diff(before, xeno);
        Check(ChangeMath.RecheckWishes(c) && ChangeMath.JournalTag(c, before, xeno) == "xeno", "a new xenotype: wishes asked again, a journal line");
        var renamed = before;
        renamed.name = "Ana Brook";
        c = ChangeMath.Diff(before, renamed);
        Check(c == ChangeMath.Change.Name && !ChangeMath.RecheckWishes(c) && ChangeMath.RereadWho(c) && ChangeMath.JournalTag(c, before, renamed) == null,
            "a new name: read again, nothing written");
        var traits = before;
        traits.traits = ChangeMath.TraitKey(new[] { "Kind:0" });
        c = ChangeMath.Diff(before, traits);
        Check(ChangeMath.RecheckWishes(c) && ChangeMath.JournalTag(c, before, traits) == null, "a lost trait: wishes asked again, nothing written");
        var nudged = before;
        nudged.size = 12;
        Check(ChangeMath.Diff(before, nudged) == ChangeMath.Change.None, "a small nudge in size isn't a new body");
        var grown = before;
        grown.size = 18;
        c = ChangeMath.Diff(before, grown);
        Check(c == ChangeMath.Change.Size && ChangeMath.JournalTag(c, before, grown) == "bigger", "a much bigger body is written down");
        var shrunk = before;
        shrunk.size = 5;
        Check(ChangeMath.JournalTag(ChangeMath.Diff(before, shrunk), before, shrunk) == "smaller", "a much smaller body too");
        Check(ChangeMath.SizeKey(1.04f) == 10 && ChangeMath.SizeKey(float.NaN) == 10 && ChangeMath.SizeKey(0.5f) == 5, "size in tenths, and never NaN");
        var lost = before;
        lost.inner = false;
        c = ChangeMath.Diff(before, lost);
        Check(ChangeMath.SetAside(c, lost) && ChangeMath.JournalTag(c, before, lost) == null, "no inner life left: wishes set aside, nothing written");
        Check(!ChangeMath.SetAside(ChangeMath.Diff(lost, before), before), "an inner life back sets nothing aside");
        var grownUp = before;
        grownUp.stage = "Child";
        c = ChangeMath.Diff(grownUp, before);
        Check(c == ChangeMath.Change.Stage && ChangeMath.RecheckWishes(c) && ChangeMath.JournalTag(c, grownUp, before) == null, "growing up asks the wishes again quietly");
        return checks;
    }
}
