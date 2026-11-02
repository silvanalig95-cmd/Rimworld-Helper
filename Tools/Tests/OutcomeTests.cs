using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// How an answer's outcome is told (OutcomeMath, shared by dilemmas, Rimfolk events and world situations): the risk's
/// words when the risk came true and it has some, else the plain ones, the single text first and then its variants, which
/// are picked by the same tag rules as every other line. World situations' answers have outcomes and riskOutcomes lists
/// too, and every variant written for them can be told.
/// </summary>
public static class OutcomeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "outcome variants: " + what);
        }

        // ---------- Which lines ----------
        var pool = OutcomeMath.Pool("Plain.", null, null, null, false, out string plain, out var more);
        C(pool.SequenceEqual(new[] { "Plain." }) && plain == "Plain." && more == null, "a single outcome is told as it is");
        var variants = new List<string> { "[winter] Cold one.", "", "Warm one." };
        pool = OutcomeMath.Pool("Plain.", variants, "Risky.", null, false, out plain, out more);
        C(pool.SequenceEqual(new[] { "Plain.", "[winter] Cold one.", "Warm one." }) && more == variants, "the single outcome first, then its variants, empty ones left out");
        pool = OutcomeMath.Pool("Plain.", variants, "Risky.", new List<string> { "[summer] Hot risk." }, true, out plain, out more);
        C(pool.SequenceEqual(new[] { "Risky.", "[summer] Hot risk." }) && plain == "Risky.", "a risk that came true is told in its own words");
        pool = OutcomeMath.Pool("Plain.", variants, null, new List<string> { "Only variants." }, true, out plain, out _);
        C(pool.SequenceEqual(new[] { "Only variants." }) && plain == null, "a risk with variants only is told from them");
        pool = OutcomeMath.Pool("Plain.", variants, null, null, true, out plain, out _);
        C(pool.SequenceEqual(new[] { "Plain.", "[winter] Cold one.", "Warm one." }), "a risk with no words of its own tells the plain outcome");
        C(OutcomeMath.Pool(null, null, null, null, false, out _, out _).Count == 0, "an answer with nothing to say tells nothing");

        // ---------- Picked by tags, as every line is (Flavor.Pick keeps the lines that fit) ----------
        List<string> Fits(List<string> lines, params string[] tags)
        {
            var context = new HashSet<string>(tags);
            return lines.Where(l => LineTags.Match(LineTags.Of(l), context) >= 0).Select(l => LineTags.Of(l).text).ToList();
        }
        var tagged = OutcomeMath.Pool("Plain.", new List<string> { "[winter] Snow.", "[helped,!winter] Thanks.", "[ignored] Nothing." }, null, null, false, out _, out _);
        C(Fits(tagged, "winter", "helped").SequenceEqual(new[] { "Plain.", "Snow." }), "in winter, having helped: the plain line and the winter one");
        C(Fits(tagged, "summer", "helped").SequenceEqual(new[] { "Plain.", "Thanks." }), "in summer, having helped: the thanks");
        C(Fits(tagged, "summer", "ignored").SequenceEqual(new[] { "Plain.", "Nothing." }), "having stayed out of it: the other line");

        // ---------- World situations use it ----------
        string def = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/World/WorldSituationDef.cs"));
        var option = Regex.Match(def, @"public class SituationOption\s*\{(.*?)\n    \}", RegexOptions.Singleline).Value;
        C(Regex.IsMatch(option, @"public List<string> outcomes;") && Regex.IsMatch(option, @"public List<string> riskOutcomes;"), "a situation's answer has outcomes and riskOutcomes lists");
        string resolve = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/World/WorldSituations.cs"));
        C(resolve.Contains("DilemmaChoices.Outcome(o.outcome, o.outcomes, o.riskOutcome, o.riskOutcomes, risk, worker.Tags(s))"),
            "a situation's answer is told the dilemmas' way, by the situation's own tags");
        string choices = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Dilemmas/DilemmaChoices.cs"));
        C(choices.Contains("OutcomeMath.Pool(") && choices.Contains("Flavor.PickFresh(lines, tags, more)"), "the variants are picked fresh, as the dilemmas' are");

        // ---------- Every variant written for a situation's answer can be told ----------
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml"))
            foreach (var o in XDocument.Load(file).Descendants("options").SelectMany(x => x.Elements("li")))
            {
                string where = Path.GetFileName(file) + ": " + ((string)o.Element("label") ?? "?");
                var lines = o.Element("outcomes")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
                var risky = o.Element("riskOutcomes")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
                foreach (var l in lines.Concat(risky)) C(!string.IsNullOrWhiteSpace(LineTags.Of(l).text), $"{where}: no empty variant");
                if (risky.Count > 0)
                    C(float.TryParse((string)o.Element("riskChance"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c) && c > 0f,
                        $"{where}: risk variants only on an answer with a risk");
            }
        return n;
    }
}
