// The colony-year balance simulator (see Balance.csproj for how to run it, and docs/balance-report.md for what it says).
//   dotnet run -c Release --project Tools/Balance -- [repo root] [--runs N] [--out report.md] [--json summary.json] [--before summary.json]

using System.Globalization;
using System.Text.Json;
using Balance;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string root = ".";
int runs = 120;
string outPath = null, jsonPath = null, beforePath = null;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs": runs = int.Parse(args[++i]); break;
        case "--out": outPath = args[++i]; break;
        case "--json": jsonPath = args[++i]; break;
        case "--before": beforePath = args[++i]; break;
        default: root = args[i]; break;
    }
}
outPath ??= Path.Combine(root, "docs", "balance-report.md");

var defs = new DefSet(root);
var src = new SourceConstants(root);
Code.Read(src);
var defaults = src.SettingDefaults();

var results = new List<Result>();
foreach (int n in new[] { 6, 12 })
    foreach (var style in Rates.Styles)
        results.Add(Runner.Run(defs, new Settings(defaults), style, n, runs, null));
foreach (var preset in new[] { "lightTouch", "storyRich", "demanding" })
    foreach (var style in Rates.Styles)
        results.Add(Runner.Run(defs, new Settings(defaults, preset), style, 6, runs, preset));
// Without the shared budget and fading: what the rules take off.
var noRules = new Dictionary<string, string>(defaults) { ["smallJoysDiminish"] = "false", ["goodTimesFade"] = "false" };
foreach (var style in Rates.Styles)
    results.Add(Runner.Run(defs, new Settings(noRules), style, 6, runs, "no budget or fading"));

Dictionary<string, Summary> before = null;
if (beforePath != null && File.Exists(beforePath))
    before = JsonSerializer.Deserialize<Dictionary<string, Summary>>(File.ReadAllText(beforePath));

var summaries = results.ToDictionary(r => r.Key, r => r.summary);
if (jsonPath != null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath)));
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
File.WriteAllText(outPath, Report.Write(defs, src, results, before, runs));
Console.WriteLine("wrote " + outPath);
foreach (var r in results)
    Console.WriteLine($"{r.Key,-34} net {r.summary.net,6:0.0}  good {r.summary.good,5:0.0} (raw {r.summary.goodRaw,5:0.0})  bad {r.summary.bad,6:0.0}  steady {r.summary.steadyGood,4:0.0}/{r.summary.steadyBad,5:0.0}  mood {r.summary.mood:P0}  decisions/day {r.summary.decisionsPerDay:0.00}  ill {r.summary.illShare:P0}");
