// The colony-year balance simulator (see Balance.csproj for how to run it, and docs/balance-report.md for what it says).
//   dotnet run -c Release --project Tools/Balance -- [repo root] [--runs N] [--out report.md] [--json summary.json] [--before summary.json]
// For tuning, the default with six colonists and the same with small letdowns and joys as moods, printed, nothing written
// (BALANCE_PRESET=demanding, lightTouch or storyRich for that preset instead):
//   BALANCE_QUICK=1 dotnet run -c Release --project Tools/Balance -- . --runs 40
// How bonds come about over two years (SocialSim.cs, printed, nothing written):
//   dotnet run -c Release --project Tools/Balance -- --social [--runs N] [--seed S]
// How much acts against their nature add to the heart's strain over a colony-year (NatureSim.cs, printed, nothing written):
//   dotnet run -c Release --project Tools/Balance -- --nature [--runs N] [--seed S]
// How often Rimfolk stages something on the map, by system, and what a weekly allowance would leave (Scenes.cs, printed):
//   dotnet run -c Release --project Tools/Balance -- --scenes [repo root] [--runs N] [--colonists N] [--gap HOURS] [--week N]
// What reaches the player in a day, the old way and by the teller with the colony's pace (Telling.cs, printed):
//   dotnet run -c Release --project Tools/Balance -- --tell [repo root] [--runs N] [--colonists N]
// Hurts from everyday life by source, the fights Rimfolk starts and what they add to strain, before and after harm parts
// 2 and 3 (HarmSim.cs), then what the wild's alphas and bosses add (BeastSim.cs), printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --harm [--runs N] [--seed S]
// Schemes against the colony over a home's year, by whether a grudge or a cold notable is in reach (SchemeSim.cs, printed):
//   dotnet run -c Release --project Tools/Balance -- --schemes [--runs N] [--seed S]
// Serious illness a colony-year, Rimfolk's and the game's own, with the shared illness budget and without (IllnessSim.cs, printed):
//   dotnet run -c Release --project Tools/Balance -- --illness [repo root] [--runs N] [--interval F]

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
if (args.Contains("--tell"))
{
    TellingSim.Run(args);
    return;
}
if (args.Contains("--scenes"))
{
    ScenesSim.Run(args);
    return;
}
if (args.Contains("--harm"))
{
    HarmSim.Run(args);
    BeastSim.Run(args);
    return;
}
if (args.Contains("--schemes"))
{
    SchemeSim.Run(args);
    return;
}
if (args.Contains("--illness"))
{
    IllnessSim.Run(args);
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
    // BALANCE_PRESET=demanding (or lightTouch, storyRich): that preset instead of the default and the moments off.
    string quickPreset = Environment.GetEnvironmentVariable("BALANCE_PRESET");
    foreach (var style in Rates.Styles)
    {
        if (quickPreset != null)
        {
            results.Add(Runner.Run(defs, new Settings(defaults, quickPreset), style, 6, runs, quickPreset));
            continue;
        }
        results.Add(Runner.Run(defs, new Settings(defaults), style, 6, runs, null));
        // Without the heart's good side, on the same seeds (an empty label: the seed counts the label's length).
        results.Add(Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["heartGoodSide"] = "false" }), style, 6, runs, ""));
        // The bands felt mildly, as before the heart was made to count.
        results.Add(Runner.Run(defs, new Settings(new Dictionary<string, string>(defaults) { ["heartBands"] = "Mild" }), style, 6, runs, "mild bands"));
    }
    foreach (var r in results)
    {
        var x = r.summary;
        Console.WriteLine($"{r.Key,-42} net {x.net,6:0.0} good {x.good,5:0.0} bad {x.bad,6:0.0} steady {x.steadyGood,4:0.0}/{x.steadyBad,5:0.0} mood {x.mood:P0} p10 {x.moodP10:P0} minor {x.underMinor:P1} strain {x.strained:P1}/{x.worn:P1}/{x.breaking:P1} ep {x.episodesPerYear:0.00} gameOrd {x.gameOrdinary:0.0} P99 {x.goodP99:0.0} high12 {x.goodHigh:P0}");
        Console.WriteLine($"   breaks: under major {x.underMajor:P2}, extreme {x.underExtreme:P2}, {x.breaksPerYear:0.00} a colonist-year; bad afternoons {x.badDaysPerYear:0.00} a colonist-year");
        Console.WriteLine($"   good side: strain at nothing {x.noStrain:P0}, steady {x.steady:P1}, in good heart {x.goodHeart:P1}, in high spirits {x.highSpirits:P1}; inspirations {x.inspirationsPerYear:0.00}, kind words {x.kindWordsPerYear:0.00} a colonist-year");
        if (x.goodTenths != null) Console.WriteLine("   good side by tenths: " + string.Join(" ", x.goodTenths.Select(v => $"{v:P0}")));
        Console.WriteLine("   filled: " + string.Join(", ", x.goodBy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0}")) + "; spent: " + string.Join(", ", x.goodSpentBy.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0}")));
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
// The heart's bands felt mildly (as before the heart was made to count): what the stronger bands change.
var mildBands = new Dictionary<string, string>(defaults) { ["heartBands"] = "Mild" };
foreach (var style in Rates.Styles)
    results.Add(Runner.Run(defs, new Settings(mildBands), style, 6, runs, "mild bands"));
// Without the heart's good side (its setting off): what it changes. On the default's seeds (an empty label: the seed counts its length).
var noGood = new Dictionary<string, string>(defaults) { ["heartGoodSide"] = "false" };
foreach (var style in Rates.Styles)
    results.Add(Runner.Run(defs, new Settings(noGood), style, 6, runs, ""));

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
