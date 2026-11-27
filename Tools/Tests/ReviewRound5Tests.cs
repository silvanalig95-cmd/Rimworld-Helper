using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of content round 5 before it meets the player (docs/review-round-5.md): the bugs found in what would show in
/// play, each kept fixed. A mission's kind is one of its lines' tags, so it is never one of its forms' tags (the round-up's
/// autumn words in spring, the bell's words and title for a drum, a siren or a beacon); cloth or leather counts the leathers
/// and wools the colony keeps; and the merchants' order never asks for mortar shells.
/// </summary>
public static class ReviewRound5Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of round 5: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        // ---- A mission's kind is a tag of its lines (Missions.Tags adds it beside the form's), so never a form's own tag ----
        // Else the lines of that one form are open to every form: the round-up's autumn words ("before the snow") in spring,
        // the bell's words and title for a tribal colony's drum.
        C(Code("Missions/Missions.cs").Contains("if (!m.kind.NullOrEmpty()) tags.Add(m.kind);"), "a mission's kind is one of the tags its lines are picked by");
        var kinds = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
            {
                string name = (string)d.Element("defName") ?? "?";
                string worker = (string)d.Element("workerClass") ?? "";
                foreach (var li in d.Descendants("li").Where(x => x.Attribute("Class") != null && x.Element("kind") != null))
                {
                    string kind = (string)li.Element("kind");
                    kinds[name] = kind;
                    // The merchants' order picks its goods and their words itself (Round5AwayMath.OrderTag); its one variant
                    // is the frame's placeholder, never given to a mission.
                    if (worker.EndsWith("HappeningWorker_R5AOrder")) continue;
                    var tags = new List<string>();
                    foreach (var list in new[] { li.Element("forms"), li.Element("variants") })
                        if (list != null)
                            foreach (var x in list.Elements("li"))
                                tags.Add(((string)x.Element("tag") ?? (x.HasElements ? "" : x.Value)).Trim());
                    C(!tags.Contains(kind), $"{name}: its kind ({kind}) is not one of its forms' tags");
                }
            }
        C(kinds.TryGetValue("Rimfolk_R5A_Mission_RoundUp", out var ru) && ru != "roundup" && kinds.TryGetValue("Rimfolk_R5A_Mission_Bell", out var bk) && bk != "bell",
            "R5A-M1 and R5A-M6: the autumn's round-up and the medieval colony's bell are forms, not the missions' kinds");
        // The order's words by what is ordered never the order's kind (the pure rule).
        string orderKind = kinds.TryGetValue("Rimfolk_R5A_Mission_Order", out var ordered) ? ordered : "order";
        C(Round5AwayMath.OrderGroups.Concat(new[] { "ComponentIndustrial", "Chemfuel" }).All(g => Round5AwayMath.OrderTag(g) != orderKind),
            "R5A-M3: the order's words by what is ordered are never its kind");

        // ---- Cloth or leather: the textiles' own sub-categories (leathers, wools) count for a delivery ----
        var missions = Code("Missions/Missions.cs");
        var textiles = Between(missions, "case \"textiles\":", "}");
        C(textiles.Contains("d.IsWithinCategory(ThingCategoryDefOf.Textiles)") && !textiles.Contains("thingCategories.Contains(ThingCategoryDefOf.Textiles)"),
            "cloth or leather: the leathers and wools in the stores count for a delivery, as its words say (the drum's hide)");

        // ---- The merchants' order: never a mortar shell, and the groups' goods by their categories and those under them ----
        var sellable = Between(Code("Missions/Round5Away.cs"), "public static bool Sellable(ThingDef d)", "public static List<R5AGood> Goods(");
        C(sellable.Contains("d.IsShell"), "R5A-M3: the merchants never order mortar shells (never weapons)");
        C(sellable.Contains("d.IsWithinCategory(ThingCategoryDefOf.Textiles)") && sellable.Contains("d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)")
          && sellable.Contains("d.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)") && !sellable.Contains("cats.Contains("),
            "R5A-M3: one thing of the colony's is never what a group already counts, by its category or one under it");

        return n;
    }
}
