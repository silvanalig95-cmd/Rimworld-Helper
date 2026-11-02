using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// What each answer does (Dilemmas/ImpactMath.cs): chances told as small fractions, the same thing said once with its
/// amounts added up, lines in the order of their kind, and the short line under an answer cut at a whole part.
/// </summary>
public static class ImpactTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "answer impact: " + what);
        }

        // Chances as "n in d".
        C(ImpactMath.Odds(0.25f) == (1, 4, true), "a quarter is 1 in 4");
        C(ImpactMath.Odds(0.5f) == (1, 2, true), "a half is 1 in 2");
        C(ImpactMath.Odds(0.33f) == (1, 3, true), "0.33 reads as 1 in 3");
        C(ImpactMath.Odds(0.4f) == (2, 5, true), "0.4 is 2 in 5, reduced");
        C(ImpactMath.Odds(0.6f) == (3, 5, true), "0.6 is 3 in 5");
        C(ImpactMath.Odds(0.75f) == (3, 4, true), "three quarters is 3 in 4");
        C(ImpactMath.Odds(0.05f) == (1, 20, true), "a small chance is 1 in however many");
        C(ImpactMath.Odds(0.08f) == (1, 12, true), "0.08 is 1 in 12");
        C(ImpactMath.Odds(0.95f) == (19, 20, true), "near certain is one short of however many");
        var about = ImpactMath.Odds(0.35f);
        C(!about.exact && about.n == 1 && about.d == 3, $"0.35 is about 1 in 3, got {about}");
        C(ImpactMath.Odds(1f) == (1, 1, true) && ImpactMath.Odds(0f).n == 0, "sure and never");
        for (int i = 1; i < 100; i++)
        {
            float p = i / 100f;
            var (a, b, exact) = ImpactMath.Odds(p);
            C(a > 0 && a < b, $"{p}: a fraction strictly between nothing and certain ({a} in {b})");
            C(Math.Abs((float)a / b - p) < (exact ? 0.005f : 0.07f), $"{p}: {a} in {b} is close enough (exact {exact})");
        }

        // Signed numbers.
        C(ImpactMath.Signed(6f) == "+6" && ImpactMath.Signed(-4f) == "-4" && ImpactMath.Signed(0.5f) == "+0.5", "signed numbers");
        C(ImpactMath.Number(12.04f) == "12" && ImpactMath.Number(2.46f) == "2.5", "one decimal at most");
        C(ImpactMath.SignedRange(3f, 3.02f, " to ") == "+3" && ImpactMath.SignedRange(5f, 3f, " to ") == "+3 to +5", "a range only when people feel it differently");

        // Merging: the same key once, amounts added, nothing left drops out, kinds in order.
        string M = ImpactMath.Mark;
        var lines = new List<ImpactLine>
        {
            new ImpactLine { kind = ImpactKind.Trust, sign = 1, text = "Mara: " + M + " trust", key = "trust|1", amount = 6f, sum = true },
            new ImpactLine { kind = ImpactKind.Map, text = "A feast in the evening" },
            new ImpactLine { kind = ImpactKind.Trust, sign = 1, text = "Mara: " + M + " trust", key = "trust|1", amount = 2f, sum = true },
            new ImpactLine { kind = ImpactKind.Goods, sign = -1, text = M + " beer from the stores", key = "cost|Beer", amount = 5f, sum = true, signed = false },
            new ImpactLine { kind = ImpactKind.Goods, sign = -1, text = M + " beer from the stores", key = "cost|Beer", amount = 7f, sum = true, signed = false },
            new ImpactLine { kind = ImpactKind.Trust, sign = 1, text = "Tam: " + M + " trust", key = "trust|2", amount = 3f, sum = true },
            new ImpactLine { kind = ImpactKind.Trust, sign = 1, text = "Tam: " + M + " trust", key = "trust|2", amount = -3f, sum = true },
            new ImpactLine { kind = ImpactKind.Mood, sign = 1, text = "Mara: +5 mood for 4 days" },
            new ImpactLine { kind = ImpactKind.Mood, sign = 1, text = "Mara: +5 mood for 4 days" },
            new ImpactLine { kind = ImpactKind.Ways, text = "The colony grows more merciful" },
            new ImpactLine { kind = ImpactKind.Trust, text = "" },
            null
        };
        var merged = ImpactMath.Merge(lines);
        C(merged.Count == 5, $"five things once each, got {merged.Count}: {string.Join(" | ", merged.Select(l => l.Text))}");
        C(merged.Select(l => l.kind).SequenceEqual(new[] { ImpactKind.Map, ImpactKind.Mood, ImpactKind.Trust, ImpactKind.Goods, ImpactKind.Ways }), "grouped in the order of their kind");
        C(merged.Any(l => l.Text == "Mara: +8 trust"), "the same person's trust adds up");
        C(merged.Any(l => l.Text == "12 beer from the stores"), "a count is written without a sign and adds up");
        C(!merged.Any(l => l.Text.StartsWith("Tam")), "a gain and a loss that cancel leave nothing to say");
        C(merged.Count(l => l.kind == ImpactKind.Mood) == 1, "the same words are said once");
        C(lines[0].amount == 6f, "merging leaves the lines it was given alone");
        var flip = new ImpactLine { kind = ImpactKind.Trust, sign = 1, text = M, sum = true, amount = -2f };
        C(flip.Sign == -1 && flip.Text == "-2", "a negative total of a good thing is a cost");
        var surer = ImpactMath.Merge(new[]
        {
            new ImpactLine { kind = ImpactKind.Health, text = "a fight", key = "fight", chance = 0.25f },
            new ImpactLine { kind = ImpactKind.Health, text = "a fight", key = "fight", chance = 1f }
        });
        C(surer.Count == 1 && surer[0].chance == 1f, "the surer of two lines about one thing stands");

        // What the gamble adds.
        var plain = new List<ImpactLine> { new ImpactLine { text = "Mara: -9 mood", key = "mood|a" }, new ImpactLine { text = "Mara: " + M + " trust", key = "trust|1", amount = -12f, sum = true } };
        var risky = new List<ImpactLine>
        {
            new ImpactLine { text = "Mara: -9 mood", key = "mood|a" }, new ImpactLine { text = "Mara: " + M + " trust", key = "trust|1", amount = -12f, sum = true },
            new ImpactLine { text = "Mara and Tam come to blows", key = "fight" }
        };
        var more = ImpactMath.Besides(risky, plain);
        C(more.Count == 1 && more[0].text.Contains("blows"), "a gamble's lines leave out what already happens");

        // The short line: whole parts only, in order, with a word that there's more.
        var parts = new List<string> { "Mara +5 mood", "Tam -4 mood", "Mara +6 trust", "-12 beer", "the colony grows more merciful" };
        string all = ImpactMath.Compact(parts, 500, "more on hover");
        C(all == string.Join("; ", parts), "everything fits: all of it, joined");
        string cut = ImpactMath.Compact(parts, 40, "more on hover");
        C(cut.Length <= 40 && cut.EndsWith("more on hover") && cut.StartsWith("Mara +5 mood"), $"too long: cut at a whole part, saying there's more ({cut})");
        C(cut.Split(new[] { "; " }, StringSplitOptions.None).All(p => parts.Contains(p) || p == "more on hover"), "never a part cut mid-word");
        C(ImpactMath.Compact(new List<string> { "a very long first part that is longer than the limit" }, 10, "more") == "a very long first part that is longer than the limit",
            "the first part is always said in full");
        C(ImpactMath.Compact(new List<string> { "a", "a", " ", null }, 50, "more") == "a", "repeats and blanks are dropped");
        C(ImpactMath.Compact(null, 50, "more") == "", "nothing to say");
        return n;
    }
}
