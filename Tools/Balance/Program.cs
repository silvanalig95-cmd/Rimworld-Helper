// The colony-year balance simulator (see Balance.csproj for how to run it, and docs/balance-report.md for what it says).
//   dotnet run -c Release --project Tools/Balance -- [repo root] [--runs N] [--out report.md] [--json summary.json] [--before summary.json]
// How bonds come about over two years (SocialSim.cs, printed, nothing written):
//   dotnet run -c Release --project Tools/Balance -- --social [--runs N] [--seed S]
// How much acts against their nature add to the heart's strain over a colony-year (NatureSim.cs, printed, nothing written):
//   dotnet run -c Release --project Tools/Balance -- --nature [--runs N] [--seed S]
// How often Rimfolk stages something on the map, by system, and what a weekly allowance would leave (Scenes.cs, printed):
//   dotnet run -c Release --project Tools/Balance -- --scenes [repo root] [--runs N] [--colonists N] [--gap HOURS] [--week N]

using System.Globalization;
using System.Text.Json;
using Balance;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

if (args.Contains("--social"))
{
    SocialSim.Run(args);
    return;
}
if (args.Contains("--nature"))
{
    NatureSim.Run(args);
    return;
}
if (args.Contains("--scenes"))
{
    ScenesSim.Run(args);
    return;
}

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
// BALANCE_QUICK=1: only the default settings with six colonists, and the moments off, for tuning (no report written).
bool quick = Environment.GetEnvironmentVariable("BALANCE_QUICK") == "1";
if (quick)
{
    foreach (var style in Rates.Styles)
    {
        results.Add(Runner.Run(defs, new Settings(defaults), style, 6, runs, null));
        results.Add(Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["smallMomentsOff"] = "true" }), style, 6, runs, "letdowns and joys as moods"));
    }
    foreach (var r in results)
    {
        var x = r.summary;
        Console.WriteLine($"{r.Key,-42} net {x.net,6:0.0} good {x.good,5:0.0} bad {x.bad,6:0.0} steady {x.steadyGood,4:0.0}/{x.steadyBad,5:0.0} mood {x.mood:P0} p10 {x.moodP10:P0} minor {x.underMinor:P1} strain {x.strained:P1}/{x.worn:P1}/{x.breaking:P1} ep {x.episodesPerYear:0.00} gameOrd {x.gameOrdinary:0.0} P99 {x.goodP99:0.0}");
        Console.WriteLine("   built: " + string.Join(", ", x.strainBy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0}")));
        Console.WriteLine("   eased: " + string.Join(", ", x.reliefBy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0}")));
        Console.WriteLine("   parts: " + string.Join(", ", x.parts.Where(kv => Math.Abs(kv.Value) >= 0.3).OrderBy(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0.0}")));
    }
    return;
}
foreach (int n in new[] { 6, 12 })
    foreach (var style in Rates.Styles)
        results.Add(Runner.Run(defs, new Settings(defaults), style, n, runs, null));
foreach (var preset in new[] { "lightTouch", "storyRich", "demanding" })
    foreach (var style in Rates.Styles)
        results.Add(Runner.Run(defs, new Settings(defaults, preset), style, 6, runs, preset));
// Without good things growing ordinary: what the one rule takes off.
var noRules = new Dictionary<string, string>(defaults) { ["goodGrowsOrdinary"] = "false" };
foreach (var style in Rates.Styles)
    results.Add(Runner.Run(defs, new Settings(noRules), style, 6, runs, "without growing ordinary"));

// Small letdowns and joys as moods of their own, nothing to the heart (as before phase 3): what the moments change.
var asMoods = new Dictionary<string, string>(defaults) { ["smallMomentsOff"] = "true" };
foreach (var style in Rates.Styles)
    results.Add(Runner.Run(defs, new Settings(asMoods), style, 6, runs, "letdowns and joys as moods"));

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
{
    var x = r.summary;
    double built = x.strainBy.Values.Sum(), game = x.strainBy.GetValueOrDefault("small moods: the game's");
    Console.WriteLine($"{r.Key,-42} net {x.net,6:0.0}  good {x.good,5:0.0}  bad {x.bad,6:0.0}  steady {x.steadyGood,4:0.0}/{x.steadyBad,5:0.0}  mood {x.mood:P0} ({x.moodP10:P0}-{x.moodP90:P0}, top {x.pinned:P0}, under minor {x.underMinor:P1})"
        + $"  strain {x.strained:P1}/{x.worn:P1}/{x.breaking:P1} episodes {x.episodesPerYear:0.000}  built {built:0} (game's small {game / Math.Max(1, built):P0})  decisions/day {x.decisionsPerDay:0.00}  ill {x.illShare:P0}");
}
