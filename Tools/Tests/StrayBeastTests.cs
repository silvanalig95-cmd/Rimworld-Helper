using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The neighbors' stray beast (Source/ColonyCouncil/Happenings/StrayBeast.cs), read from the defs: it comes with the
/// letter and the letter says where it stands (every line names the barn, the pen or the yard it is really in, and
/// every place has a line for every season, era and land); given back, their drover comes for it before the gift or the
/// fee is sent, so it is one visit; kept, it is the same beast, not a new one.
/// </summary>
public static class StrayBeastTests
{
    private const string Event = "Council_Evt_StrayBeast";

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the neighbors' stray: " + what);
        }

        // The places, as the code names them.
        string src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Happenings/StrayBeast.cs"));
        var m = Regex.Match(src, @"public const string Barn = ""([a-z]+)"", Pen = ""([a-z]+)"", Yard = ""([a-z]+)""");
        C(m.Success, "the places are read from the code");
        if (!m.Success) return n;
        string barn = m.Groups[1].Value, pen = m.Groups[2].Value, yard = m.Groups[3].Value;
        var places = new[] { barn, pen, yard };

        var file = Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_Neighbors.xml");
        var def = XDocument.Load(file).Root!.Elements("IncidentDef").FirstOrDefault(d => (string)d.Element("defName") == Event);
        C(def != null, "the event is there");
        if (def == null) return n;
        var ext = def.Descendants("li").First(e => (string)e.Attribute("Class") == "ColonyCouncil.HappeningExtension");
        var setting = def.Descendants("li").FirstOrDefault(e => (string)e.Attribute("Class") == "ColonyCouncil.HappeningSetting");
        C(setting?.Element("stray")?.Elements("li").Any() ?? false, "the beast comes with the letter (the setting's stray kinds)");

        var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        var variants = XDocument.Load(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_Neighbors_Variants.xml")).Root!
            .Elements("ColonyCouncil.LetterVariantsDef").FirstOrDefault(v => (string)v.Element("incident") == Event);
        texts.AddRange(variants?.Element("texts")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>());

        // Every line names where it stands, and says no other place.
        foreach (var t in texts)
        {
            var p = LineTags.Of(t);
            var at = (p.tags ?? Array.Empty<string>()).Where(places.Contains).ToList();
            string shortText = p.text.Length <= 50 ? p.text : p.text.Substring(0, 50) + "...";
            C(at.Count == 1, $"'{shortText}' is for one place");
            if (at.Count != 1) continue;
            bool saysBarn = Regex.IsMatch(p.text, @"\bbarn\b", RegexOptions.IgnoreCase), saysPen = Regex.IsMatch(p.text, @"\bpens?\b", RegexOptions.IgnoreCase);
            C(saysBarn == (at[0] == barn), $"'{shortText}' speaks of the barn only where the beast is in one");
            C(saysPen == (at[0] == pen), $"'{shortText}' speaks of the pen only where the beast is in one");
        }

        // Wherever it stands, whatever the season, era and land, a line fits.
        var eras = new[] { new[] { "tribal", "lowtech" }, new[] { "medieval", "lowtech" }, new[] { "industrial" }, new[] { "spacer" } };
        foreach (var place in places)
        foreach (var season in new[] { "spring", "summer", "fall", "winter" })
        foreach (var era in eras)
        foreach (var land in new[] { "river", "cold" })
        {
            var context = new HashSet<string>(era) { place, season, land };
            C(texts.Any(t => LineTags.Match(LineTags.Of(t), context) >= 0), $"a letter for a beast in the {place}, {season}, {era[0]}, {land}");
        }

        // The answers.
        var options = ext.Element("options")!.Elements("li").ToList();
        C(options.Count == 3, "three answers");
        if (options.Count != 3) return n;
        List<string> Effects(XElement o) => o.Element("effects")?.Elements("li").Select(e => (string)e.Attribute("Class") ?? "").ToList() ?? new List<string>();
        foreach (int i in new[] { 0, 1 })
        {
            var fx = Effects(options[i]);
            C(fx.FirstOrDefault() == "ColonyCouncil.DilemmaEffect_StrayHome", $"answer {i}: their drover comes for it first, so the gift or the fee comes with them");
            C(Regex.IsMatch((string)options[i].Element("outcome") ?? "", @"\bdrover\b"), $"answer {i}: the words say a drover came for it");
        }
        var keep = Effects(options[2]);
        C(keep.Contains("ColonyCouncil.DilemmaEffect_KeepStray") && !keep.Contains("ColonyCouncil.DilemmaEffect_SpawnAnimal"), "answer 2: the colony keeps that beast, not a new one");
        C(src.Contains("public override void Describe(DilemmaContext ctx, ImpactSheet sheet)"), "the answers say what they do with it");
        return n;
    }
}
