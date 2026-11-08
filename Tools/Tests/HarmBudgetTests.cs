using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The shared budget of hurts from everyday life (Harm/HarmBudgetMath.cs; the player's choice of 7 November 2026): every
/// kind registered once, the passes sharing the founding kinds' total among the kinds on, a kind that came lately passing
/// less often, nothing of the setting or the storyteller in a pass; and the mod's side: every roll and every hurt names its
/// kind, every system has its switch, the memory saved and modelled, the words that explain it.
/// </summary>
public static class HarmBudgetTests
{
    private const int D = HarmMath.DayTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm budget: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) <= eps * Math.Max(1f, Math.Abs(b));

        // ================= The variety =================

        C(Near(HarmBudgetMath.Variety(HarmBudgetMath.LongAgo), 1f) && Near(HarmBudgetMath.Variety(-3f), 1f), "a kind that never came, or came long ago, weighs its whole rate");
        C(Near(HarmBudgetMath.Variety(0f), 1f - HarmBudgetMath.VarietyDip) && HarmBudgetMath.VarietyDip > 0.5f, $"just come, it weighs {1f - HarmBudgetMath.VarietyDip:0.00} of its rate");
        C(Near(HarmBudgetMath.Variety(HarmBudgetMath.VarietyDays), 1f) && Near(HarmBudgetMath.VarietyDays, 30f), "back to all of it after thirty days (two quadrums)");
        float prev = 0f;
        bool rising = true;
        for (float d = 0f; d <= 40f; d += 0.5f)
        {
            float v = HarmBudgetMath.Variety(d);
            if (v < prev - 1e-6f || v < 0f || v > 1f) rising = false;
            prev = v;
        }
        C(rising, "it rises evenly with the days, never past its whole rate");
        C(HarmBudgetMath.DaysSince(HarmMath.Never, 5 * D) == HarmBudgetMath.LongAgo && HarmBudgetMath.DaysSince(9 * D, 5 * D) == HarmBudgetMath.LongAgo
          && Near(HarmBudgetMath.DaysSince(3 * D, 5 * D), 2f), "days since by the tick; never, or a tick from a save played on, is long ago");
        C(!HarmBudgetMath.Forgotten(0, 29 * D) && HarmBudgetMath.Forgotten(0, 30 * D) && HarmBudgetMath.Forgotten(HarmMath.Never, 0), "its memory goes once it has its whole share back");

        // ================= The registry =================

        var all = HarmKinds.All;
        C(all.Select(k => k.key).Distinct().Count() == all.Length && all.Select(k => k.label).Distinct().Count() == all.Length, "every kind registered once, by its own key and label");
        C(all.All(k => k.weight > 0f && k.weight < 0.25f && !k.system.NullOrEmptyText()), "every kind has its base rate and its system");
        var founding = all.Where(k => k.founding).ToList();
        var laterKinds = all.Where(k => !k.founding).ToList();
        var four = laterKinds.Where(k => Array.IndexOf(all, k) < Array.IndexOf(all, HarmKinds.SickWorkOn)).ToList();
        int tamer = Array.IndexOf(all, HarmKinds.TamerBout);
        var five = laterKinds.Where(k => Array.IndexOf(all, k) >= Array.IndexOf(all, HarmKinds.SickWorkOn) && Array.IndexOf(all, k) < tamer).ToList();
        int seven0 = Array.IndexOf(all, HarmKinds.SitDown);
        var six = laterKinds.Where(k => Array.IndexOf(all, k) > tamer && Array.IndexOf(all, k) < seven0).ToList();
        var seven = laterKinds.Where(k => Array.IndexOf(all, k) >= seven0).ToList();
        C(founding.Count == 18 && four.Count == 16 && five.Count == 14 && six.Count == 23 && seven.Count == 28 && laterKinds.Count == 82 && all.Last() == HarmKinds.WorkingWrong,
            $"the kinds built before the budget ({founding.Count}), harm part 4's ({four.Count}: the fifteen, a guest's night in two), part 5's ({five.Count}), the tamers' bout (peoples, release D), part 6's ({six.Count}) and part 7's ({seven.Count})");
        foreach (var id in new[] { "1.1.1", "2.5.2", "2.2.3", "1.2.3", "4.3.1", "3.4.1", "4.7.2", "4.5.3", "4.5.1", "4.9.1", "3.6.1", "1.4.2", "2.5.1", "2.8.1", "4.1.1" })
            C(four.Any(k => k.label.Contains("(" + id + ")")), $"the proposal {id} is a kind of part 4");
        foreach (var id in new[] { "1.2.1", "1.2.2", "3.2.1", "2.4.1", "3.1.1", "2.2.1", "2.1.1", "3.5.1", "1.4.1", "3.3.1" })
            C(founding.Any(k => k.label.Contains("(" + id + ")")), $"the proposal {id} is a founding kind");
        float budget = HarmBudgetMath.Budget(all), unbudgeted = HarmBudgetMath.Unbudgeted(all);
        C(budget > 0.40f && budget < 0.50f, $"the budget is what the founding kinds come to, about today's total ({budget:0.000} a colonist-year)");
        C(unbudgeted > budget * 1.5f, $"without it, every kind on would come to more ({unbudgeted:0.000})");
        C(HarmKinds.Of("hail") == HarmKinds.Hail && HarmKinds.Of("none of these") == null && HarmKinds.Of(null) == null, "a kind is found by its saved key");

        // ================= The passes =================

        float[] Days(IList<HarmKind> on, float d) => on.Select(_ => d).ToArray();
        var onlyFounding = founding.ToArray();
        var passes = new float[onlyFounding.Length];
        HarmBudgetMath.Passes(onlyFounding, Days(onlyFounding, HarmBudgetMath.LongAgo), passes);
        C(passes.All(p => Near(p, 1f)), "with only the founding kinds on and none come lately, every kind at its own rate: the hurts as they were");
        C(Near(HarmBudgetMath.Expected(onlyFounding, Days(onlyFounding, HarmBudgetMath.LongAgo)), HarmBudgetMath.Budget(onlyFounding)), "and they come to the budget");
        passes = new float[all.Length];
        HarmBudgetMath.Passes(all, Days(all, HarmBudgetMath.LongAgo), passes);
        float share = budget / unbudgeted;
        C(passes.All(p => Near(p, share)), $"every kind on, none come lately: each at the same share of its rate ({share:P0})");
        C(Near(HarmBudgetMath.Expected(all, Days(all, HarmBudgetMath.LongAgo)), budget), "and together they come to the budget, not more");
        // However the kinds' last comings fall, the kinds on come to the budget while no pass reaches its ceiling.
        var rng = new Random(7);
        int trials = 0, held = 0;
        for (int t = 0; t < 2000; t++)
        {
            var on = all.Where(_ => rng.NextDouble() < 0.8).ToList();
            if (!on.Any(k => k.founding)) continue;
            var days = on.Select(_ => rng.NextDouble() < 0.3 ? (float)(rng.NextDouble() * 35) : HarmBudgetMath.LongAgo).ToArray();
            var ps = new float[on.Count];
            HarmBudgetMath.Passes(on, days, ps);
            if (ps.Max() >= HarmBudgetMath.MaxPass - 1e-4f) continue;
            trials++;
            if (Near(HarmBudgetMath.Expected(on, days), HarmBudgetMath.Budget(on), 0.0005f)) held++;
        }
        C(trials > 1000 && held == trials, $"whatever is on and whenever each came, they come to the budget ({held} of {trials})");
        // A kind that came lately passes less often than one that hasn't, the rest a little more.
        var lately = Days(all, HarmBudgetMath.LongAgo);
        int iAil = Array.IndexOf(all, HarmKinds.LandAilments);
        lately[iAil] = 0f;
        var after = new float[all.Length];
        HarmBudgetMath.Passes(all, lately, after);
        C(after[iAil] < passes[iAil] * 0.5f && after.Where((p, i) => i != iAil).All(p => p > share), "a kind just come passes less, the others a little more");
        C(Near(after[iAil], (1f - HarmBudgetMath.VarietyDip) * after[0]), "just come, it passes at its variety's share of the others' pass");
        // A pass never past its ceiling, whatever rests.
        var heavy = Days(onlyFounding, 0f);
        heavy[Array.IndexOf(onlyFounding, HarmKinds.Drills)] = HarmBudgetMath.LongAgo;
        var capped = new float[onlyFounding.Length];
        HarmBudgetMath.Passes(onlyFounding, heavy, capped);
        C(capped.All(p => p <= HarmBudgetMath.MaxPass + 1e-5f) && Near(HarmBudgetMath.MaxPass, 2f), "no kind ever comes at more than twice its own rate");
        C(Near(HarmBudgetMath.PassFor(0.5f, 0f, 1f), 1f) && Near(HarmBudgetMath.PassFor(0.5f, 1f, 0f), 1f), "nothing on, no budget: the pass leaves it be");
        // A later kind plugs in by its base rate alone: the rest make room, the total stays.
        var later = new HarmKind("aLaterKind", "a later kind", "later", 0.05f, false);
        var withLater = all.Concat(new[] { later }).ToArray();
        C(Near(HarmBudgetMath.Expected(withLater, Days(withLater, HarmBudgetMath.LongAgo)), budget), "a later kind registered with its base rate shares the budget, the total unchanged");
        C(HarmBudgetMath.Pass(later, withLater, Days(withLater, HarmBudgetMath.LongAgo)) < share, "and the rest pass a little less to make room");

        // ================= The setting and the storyteller, as before =================

        float pass = passes[0];
        foreach (float scale in new[] { 0.25f, 0.5f, 1f, 1.5f, 2f })
        {
            float with = HarmMath.Chance(0.01f, scale * pass), without = HarmMath.Chance(0.01f, scale);
            C(Near(with, without * pass, 1e-5f), $"the setting and the storyteller scale a kind as before, its pass on top ({scale:0.00})");
        }
        C(HarmMath.Chance(0.5f, HarmMath.Scale(0f, 1f, 1f) * HarmBudgetMath.MaxPass) == 0f, "the setting at nothing: nothing, whatever the pass");
        C(HarmMath.Bar(1f, true, false, false, false, false, false, HarmMath.Never, 0, HurtClass.Mild) == HurtBar.Baby
          && HarmMath.Bar(1f, false, true, false, false, false, false, HarmMath.Never, 0, HurtClass.Injury) == HurtBar.Child
          && HarmMath.Bar(1f, false, false, false, false, false, false, 0, D, HurtClass.Injury) == HurtBar.Lately, "the cap and its exclusions stand as they were (the pass is only on the chance)");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string harm = Code("Harm/Harm.cs");
        C(!harm.Contains("public static bool Roll(float") && !harm.Contains("public static float Chance(float") && !harm.Contains("public static bool Roll(Pawn p, float"),
            "no roll without a kind: every chance names what it is");
        C(harm.Contains("public static float PassOf(HarmKind kind)") && harm.Contains("public static float ScaleFor(HarmKind kind) => Scale * PassOf(kind)")
          && harm.Contains("HarmBudgetMath.PassFor(") && harm.Contains("public static bool SystemOn(string system)"), "the budget lives in the one service");
        C(harm.Contains("public static void Note(Pawn p, HarmKind kind)") && harm.Contains("HarmKind why, HurtClass c = HurtClass.Injury, Pawn by = null)") && harm.Contains("public static bool Fight(Pawn a, Pawn b, HarmKind why)"),
            "every hurt the service gives or counts names its kind");
        var defaults = new HashSet<string> { "decrees", "dangers", "hail", "peoples" };
        foreach (var system in all.Select(k => k.system).Distinct())
            C(defaults.Contains(system) || harm.Contains("case \"" + system + "\":"), $"the system '{system}' has its switch (Harm.SystemOn)");
        // Every roll and every hurt in the mod names its kind.
        var calls = new Regex(@"Harm\.(Roll|Chance|Injure|Ail|Fight|Note|ScaleFor)\(");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            string text = File.ReadAllText(file);
            foreach (Match m in calls.Matches(text))
            {
                int depth = 0, end = m.Index + m.Length - 1;
                for (int i = end; i < text.Length; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')' && --depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
                string call = text.Substring(m.Index, end - m.Index + 1);
                bool named = call.Contains("HarmKinds.") || Regex.IsMatch(call, @"\bkind\b|\bwhy\b|\.Kind\b|, null\)");
                if (!named) C(false, $"{Path.GetFileName(file)}: '{call.Replace("\n", " ")}' names its kind");
            }
        }
        C(harm.Contains("Scribe_Collections.Look(ref kindLast, \"kindLast\"") && harm.Contains("Scribe_Collections.Look(ref waiting, \"waiting\", LookMode.Deep)")
          && harm.Contains("Scribe_Collections.Look(ref calm, \"calm\""), "the budget's memory, what's waiting and the calm heads saved in the harm book");
        C(harm.Contains("HarmBudgetMath.Forgotten(kv.Value, now)"), "a kind's memory pruned once it has its share back");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(model.Contains("\"Harm/Harm.cs:kindLast\"") && model.Contains("\"Harm/Harm.cs:waiting\"") && model.Contains("\"Harm/Harm.cs:calm\""), "each with a save model");
        C(Code("Ailments/AilmentTracker.cs").Contains("Harm.PassOf(HarmKinds.LandAilments)"), "the ailments of the land and the work take their turn, without the setting they never had");
        C(Code("Harm/HuntsBite.cs").Contains("Harm.PassOf(HarmKinds.HuntTurn)") && Code("Harm/HuntsBite.cs").Contains("book?.Calm(a)"),
            "a hunt's head that can turn takes its turn once a hunt, so the pass is the hunt's and not each shot's");
        string balance = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Balance.csproj"));
        string sim = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "HarmSim.cs"));
        C(balance.Contains("Harm/HarmBudgetMath.cs") && balance.Contains("Harm/HarmFourMath.cs") && sim.Contains("HarmBudgetMath.Passes(Modelled") && sim.Contains("budget/before"),
            "the balance model's harm mode reads the very rules and prints the before and after line");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Harm.xml"));
        C(Regex.IsMatch(keyed, @"<CC_Settings_EverydayHurts_Tip>[^<]*one budget[^<]*</CC_Settings_EverydayHurts_Tip>"), "the setting's tooltip explains the budget");
        string almanac = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml").Select(File.ReadAllText));
        C(almanac.Contains("one budget"), "the Almanac's harm text explains the budget");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### The shared budget"), "DESIGN.md has the budget");
        return n;
    }

    private static bool NullOrEmptyText(this string s) => string.IsNullOrEmpty(s);

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
