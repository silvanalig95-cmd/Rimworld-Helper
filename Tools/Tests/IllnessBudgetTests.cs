using System.Globalization;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The shared illness budget (Source/ColonyCouncil/Ailments/IllnessBudgetMath.cs; the player's choice of 8 November 2026,
/// made soft on 9 November 2026): which incidents count, the game's own rate, each case of Rimfolk's owing a skip, the soft
/// hold and the debt let go, one debt at a time, the pause and the weeks of easing after the game's own, the readiness and
/// the overlap factor, debts that lapse, and played out over many years the total staying near the game's own rate; the
/// balance model's tables against the targets; and the mod's side: the hook, the setting, the saved ledgers and an older
/// save, the words and the docs.
/// </summary>
public static class IllnessBudgetTests
{
    private const int D = IllnessBudgetMath.DayTicks;
    private const int Never = IllnessBudgetMath.Never;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "illness budget: " + what);
        }

        bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps * Math.Max(1f, Math.Abs(b));

        // ================= Which incidents count =================

        C(IllnessBudgetMath.Which(true, true, false, false, false, true) == BudgetFiring.Counts, "the storyteller's own human disease aimed at a home counts");
        C(IllnessBudgetMath.Which(true, true, true, false, false, true) == BudgetFiring.Rimfolk, "Rimfolk's own catching ailments never count against themselves");
        C(IllnessBudgetMath.Which(true, false, false, false, false, true) == BudgetFiring.NotHumanDisease, "an animal disease, or a mod's sickness filed under another category, is left alone");
        C(IllnessBudgetMath.Which(false, true, false, false, false, true) == BudgetFiring.NotStoryteller, "a disease not sent by the storyteller (a script, another system) is left alone");
        C(IllnessBudgetMath.Which(true, true, false, true, false, true) == BudgetFiring.Forced, "a forced one (dev mode, a script that insists) is left alone");
        C(IllnessBudgetMath.Which(true, true, false, false, true, true) == BudgetFiring.Quest, "a quest's outbreak is left alone");
        C(IllnessBudgetMath.Which(true, true, false, false, false, false) == BudgetFiring.NotHome, "one aimed at a caravan, a camp or a site is left alone");
        C(IllnessBudgetMath.Which(false, false, true, true, true, false) == BudgetFiring.Rimfolk, "Rimfolk's own is named first, whatever else holds");

        // ================= The game's own rate =================

        const float Cutoff = 100f;
        float temperate = IllnessBudgetMath.GameMeanDays(50f, 1f, true, Cutoff);
        C(Near(temperate, 50f) && Near(IllnessBudgetMath.GamePerYear(temperate), 1.2f), "a temperate forest (50 days) at Strive to survive: the game's own every 50 days, 1.2 a year");
        C(Near(IllnessBudgetMath.GameMeanDays(50f, 1.5f, true, Cutoff), 75f) && Near(IllnessBudgetMath.GamePerYear(75f), 0.8f), "at Adventure story's interval (1.5): every 75 days, 0.8 a year");
        C(float.IsInfinity(IllnessBudgetMath.GameMeanDays(50f, Cutoff, true, Cutoff)) && float.IsInfinity(IllnessBudgetMath.GameMeanDays(50f, 1f, false, Cutoff)),
            "diseases off, or a storyteller with nothing to send them: no game's rate");
        C(IllnessBudgetMath.GamePerYear(IllnessBudgetMath.NoGameDiseases) == 0f && IllnessBudgetMath.GamePerYear(0f) == 0f, "no rate, none a year");
        C(Near(IllnessBudgetMath.GameMeanDays(0f, 0f, true, Cutoff), 60f), "a land or a storyteller without its numbers counts as the game's usual 60 days");

        // ================= How long a debt is kept =================

        C(Near(IllnessBudgetMath.KeepDays(50f), 150f), "kept three of the game's gaps: 150 days on a temperate map");
        C(Near(IllnessBudgetMath.KeepDays(20f), IllnessBudgetMath.MinKeepDays) && Near(IllnessBudgetMath.KeepDays(500f), IllnessBudgetMath.MaxKeepDays),
            "two years at least, five at most");
        C(Near(IllnessBudgetMath.KeepDays(IllnessBudgetMath.NoGameDiseases), IllnessBudgetMath.MinKeepDays) && Near(IllnessBudgetMath.KeepDays(float.NaN), IllnessBudgetMath.MinKeepDays),
            "no game's rate: two years, so a debt never waits for ever");
        C(Math.Exp(-IllnessBudgetMath.KeepDays(50f) / 50f) < 0.06, "on a temperate map nineteen debts in twenty meet one of the game's diseases before they lapse");

        // ================= The soft rules' numbers =================

        C(IllnessBudgetMath.HoldChance >= 0.7f && IllnessBudgetMath.HoldChance <= 0.8f, $"the game's next disease is held back most of the time, not always ({IllnessBudgetMath.HoldChance:P0})");
        C(IllnessBudgetMath.MaxOwed == 1, "one debt at a time");
        C(IllnessBudgetMath.PauseDays >= 7f && IllnessBudgetMath.PauseDays <= 10f, $"after the game's own a short firm pause ({IllnessBudgetMath.PauseDays:0} days), not thirty days");
        C(IllnessBudgetMath.EaseFrom > 0f && IllnessBudgetMath.EaseFrom <= 0.5f && IllnessBudgetMath.EaseDays >= 14f && IllnessBudgetMath.EaseDays <= 35f,
            $"then a lowered chance ({IllnessBudgetMath.EaseFrom:P0}) that comes back over the following weeks ({IllnessBudgetMath.EaseDays:0} days)");
        C(IllnessBudgetMath.OverlapFactor > 0f && IllnessBudgetMath.OverlapFactor <= 1f / 3f, $"someone already seriously ill: a third of the usual chance or less ({IllnessBudgetMath.OverlapFactor:P0}), never none");
        C(Near(IllnessBudgetMath.Overlap(true), IllnessBudgetMath.OverlapFactor) && IllnessBudgetMath.Overlap(false) == 1f, "the overlap factor only for someone already seriously ill");
        C(IllnessBudgetMath.Readiness > 1f && IllnessBudgetMath.Readiness <= 3f, $"with the budget on Rimfolk's come more readily ({IllnessBudgetMath.Readiness:0.00} times), most of them taking a turn");
        C(Near(IllnessBudgetMath.MostInAll, 2f - IllnessBudgetMath.HoldChance) && IllnessBudgetMath.MostInAll <= 1.3f, $"in all at most about a quarter above the game's own ({IllnessBudgetMath.MostInAll - 1f:P0})");
        C(Near(IllnessBudgetMath.GapDays, 30f), "a case put off is said at most once in the serious illnesses' own gap");

        // The readiness leans with the storyteller's disease interval.
        C(Near(IllnessBudgetMath.ReadinessAt(1f), IllnessBudgetMath.Readiness), "at the usual interval, the readiness as it is");
        C(Near(IllnessBudgetMath.ReadinessAt(1.5f), IllnessBudgetMath.Readiness / (float)Math.Pow(1.5, 1.5), 0.01f), $"at Adventure story's 1.5, less ({IllnessBudgetMath.ReadinessAt(1.5f):0.00})");
        bool leansDown = true;
        for (float i = 0.3f; i < 6f; i += 0.1f)
            if (IllnessBudgetMath.ReadinessAt(i + 0.1f) > IllnessBudgetMath.ReadinessAt(i) + 1e-5f) leansDown = false;
        C(leansDown && IllnessBudgetMath.ReadinessAt(0.9f) > IllnessBudgetMath.Readiness, "the more the storyteller spaces its diseases out, the less readily; harder ones a little more");
        C(Near(IllnessBudgetMath.IntervalLean(10f), 0.25f) && Near(IllnessBudgetMath.IntervalLean(0.1f), 2f) && Near(IllnessBudgetMath.IntervalLean(float.NaN), 1f) && Near(IllnessBudgetMath.IntervalLean(0f), 1f),
            "the lean is one over the interval, a quarter to double, whole without a number");

        // ================= The debt and the soft hold =================

        float keep = IllnessBudgetMath.KeepDays(50f);
        var b = new IllnessBudgetBook();
        int t0 = 10 * D;
        C(IllnessBudgetMath.Room(b, t0, keep) == BudgetRoom.Open && IllnessBudgetMath.Chance(b, t0, keep) == 1f && IllnessBudgetMath.Owing(b, t0, keep) == 0, "a new home owes nothing and has room");
        C(!IllnessBudgetMath.Hold(b, t0, keep, 0f, out _), "with nothing owed, the game's disease comes as it would have");
        IllnessBudgetMath.Owe(b, t0, IllnessBudgetMath.Join("Council_Ail_Tuberculosis", "Ana"));
        C(IllnessBudgetMath.Owing(b, t0 + D, keep) == 1 && b.owedFor.Count == 1, "a serious case of Rimfolk's owes one skip");
        C(IllnessBudgetMath.Room(b, t0 + D, keep) == BudgetRoom.Owed && IllnessBudgetMath.Chance(b, t0 + D, keep) == 0f && !IllnessBudgetMath.Allows(b, t0 + D, keep, 0f),
            "while it's owed, Rimfolk starts no other there");
        int t1 = t0 + 20 * D;
        C(IllnessBudgetMath.Hold(b, t1, keep, 0.5f, out string paid) && paid == "Council_Ail_Tuberculosis|Ana", "the game's next disease, its roll under the hold's chance, is held back, paying the debt it was for");
        IllnessBudgetMath.Split(paid, out string ill, out string who);
        C(ill == "Council_Ail_Tuberculosis" && who == "Ana", "the records know the illness and who had it");
        C(IllnessBudgetMath.Owing(b, t1, keep) == 0 && b.lastSkip == t1 && b.held == 1, "paid: nothing owed, the skip remembered and counted");
        C(!IllnessBudgetMath.Hold(b, t1 + D, keep, 0f, out _), "one skip a case: the next comes");
        C(IllnessBudgetMath.Counted(b, "Council_Ail_Tuberculosis|Ana") && !IllnessBudgetMath.Counted(b, "Council_Ail_Typhus|Ana"),
            "the letter knows the case was counted (the skip paid for it), and one that never was");
        var owing = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(owing, t0, IllnessBudgetMath.Join("Council_Ail_Cholera", null));
        C(IllnessBudgetMath.Counted(owing, "Council_Ail_Cholera|") && !IllnessBudgetMath.Counted(null, "x|"), "and one still owed");
        C(IllnessBudgetMath.Room(b, t1 + D, keep) == BudgetRoom.Open && IllnessBudgetMath.Chance(b, t1 + D, keep) == 1f,
            "after a skip Rimfolk may take another at once: the game's own needn't come between");

        // Not always: the fourth time the game's own comes all the same, and the debt is let go as it lands.
        var soft = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(soft, t0, "Council_Ail_Typhus|Bo");
        C(!IllnessBudgetMath.Hold(soft, t1, keep, IllnessBudgetMath.HoldChance, out string none) && none == null && IllnessBudgetMath.Owing(soft, t1, keep) == 1 && soft.held == 0,
            "a roll at the hold's chance or over: the game's disease comes, nothing paid yet");
        C(!IllnessBudgetMath.Hold(soft, t1, keep, 0.99f, out _), "and so for any roll above it");
        IllnessBudgetMath.GameCame(soft, t1, keep);
        C(IllnessBudgetMath.Owing(soft, t1, keep) == 0 && soft.cameAnyway == 1 && soft.lastGame == t1 && soft.lastSkip == Never,
            "it came: the debt is let go and counted, the game's own remembered, no skip");
        C(IllnessBudgetMath.Room(soft, t1 + D, keep) == BudgetRoom.Waiting, "and Rimfolk's rest there");
        var fresh0 = new IllnessBudgetBook();
        IllnessBudgetMath.GameCame(fresh0, t0, keep);
        C(fresh0.cameAnyway == 0 && fresh0.lastGame == t0, "the game's own coming where nothing was owed lets nothing go");
        int heldBack = 0, rolls = 0;
        for (float r = 0.005f; r < 1f; r += 0.01f, rolls++)
        {
            var one = new IllnessBudgetBook();
            IllnessBudgetMath.Owe(one, t0, "a|b");
            if (IllnessBudgetMath.Hold(one, t1, keep, r, out _)) heldBack++;
        }
        C(Math.Abs(heldBack / (float)rolls - IllnessBudgetMath.HoldChance) < 0.02f, $"over even rolls, held back {heldBack} in {rolls}");

        var two = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(two, t0, "a|b");
        IllnessBudgetMath.Owe(two, t0, "c|d");
        C(IllnessBudgetMath.Owing(two, t0, keep) == 2 && IllnessBudgetMath.Hold(two, t0 + D, keep, 0f, out string first) && first == "a|b"
          && IllnessBudgetMath.Owing(two, t0 + D, keep) == 1, "two owed (a forced case on top): paid one at a time, the oldest first");
        IllnessBudgetMath.GameCame(two, t0 + 2 * D, keep);
        C(IllnessBudgetMath.Owing(two, t0 + 2 * D, keep) == 0 && two.owedFor.Count == 0, "and one let go at a time, its record beside it");

        // ================= The pause and the weeks after the game's own =================

        int t2 = t1 + 5 * D;
        IllnessBudgetMath.GameCame(b, t2, keep);
        C(b.lastGame == t2 && IllnessBudgetMath.Room(b, t2 + D, keep) == BudgetRoom.Waiting && IllnessBudgetMath.Chance(b, t2 + D, keep) == 0f,
            "the game's own came: Rimfolk's serious illnesses pause");
        int pauseEnd = t2 + (int)(IllnessBudgetMath.PauseDays * D);
        C(IllnessBudgetMath.Room(b, pauseEnd - 1, keep) == BudgetRoom.Waiting && IllnessBudgetMath.Room(b, pauseEnd, keep) == BudgetRoom.Easing,
            $"for {IllnessBudgetMath.PauseDays:0} days, then they ease back");
        C(Near(IllnessBudgetMath.Chance(b, pauseEnd, keep), IllnessBudgetMath.EaseFrom), "from a lowered chance");
        int easeEnd = pauseEnd + (int)(IllnessBudgetMath.EaseDays * D);
        float mid = IllnessBudgetMath.Chance(b, pauseEnd + (int)(IllnessBudgetMath.EaseDays / 2f * D), keep);
        C(mid > IllnessBudgetMath.EaseFrom && mid < 1f && Near(mid, (IllnessBudgetMath.EaseFrom + 1f) / 2f, 0.01f), $"halfway through the weeks, halfway back ({mid:P0})");
        C(IllnessBudgetMath.Room(b, easeEnd, keep) == BudgetRoom.Open && IllnessBudgetMath.Chance(b, easeEnd, keep) == 1f, "and whole again once they're over");
        bool rises = true;
        for (float d = 0f; d < 60f; d += 0.5f)
            if (IllnessBudgetMath.Eased(d + 0.5f) < IllnessBudgetMath.Eased(d)) rises = false;
        C(rises && IllnessBudgetMath.Eased(0f) == 0f && IllnessBudgetMath.Eased(IllnessBudgetMath.PauseDays - 0.01f) == 0f && IllnessBudgetMath.Eased(1000f) == 1f,
            "the chance only comes back, never dips: nothing in the pause, whole long after");
        C(IllnessBudgetMath.Eased(float.NaN) == 1f && IllnessBudgetMath.Eased(-3f) == 1f && IllnessBudgetMath.Eased(float.PositiveInfinity) == 1f, "no time, or a time ahead of now: whole");
        C(!IllnessBudgetMath.Allows(b, pauseEnd, keep, IllnessBudgetMath.EaseFrom) && IllnessBudgetMath.Allows(b, pauseEnd, keep, IllnessBudgetMath.EaseFrom - 0.01f)
          && IllnessBudgetMath.Allows(b, easeEnd, keep, 0.999f), "a case starts when its roll falls under the chance");
        var fresh = new IllnessBudgetBook();
        IllnessBudgetMath.GameCame(fresh, t0, keep);
        C(IllnessBudgetMath.Room(fresh, t0 + D, keep) == BudgetRoom.Waiting && IllnessBudgetMath.Room(fresh, t0 + (int)((IllnessBudgetMath.PauseDays + IllnessBudgetMath.EaseDays + 1f) * D), keep) == BudgetRoom.Open,
            "a home that never owed anything rests after the game's own too");
        var both = new IllnessBudgetBook();
        IllnessBudgetMath.GameCame(both, t0, keep);
        IllnessBudgetMath.Owe(both, t0 + 20 * D, "x|y");
        C(IllnessBudgetMath.Room(both, t0 + 20 * D, keep) == BudgetRoom.Owed && IllnessBudgetMath.Chance(both, t0 + 20 * D, keep) == 0f, "a debt owed outweighs the easing");

        // ================= Expiry =================

        var old = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(old, t0, "x|y");
        int lapse = t0 + (int)(keep * D);
        C(IllnessBudgetMath.Owing(old, lapse - 1, keep) == 1 && IllnessBudgetMath.Owing(old, lapse, keep) == 0, $"a debt lapses unpaid after {keep:0} days");
        C(IllnessBudgetMath.Room(old, lapse, keep) == BudgetRoom.Open && !IllnessBudgetMath.Hold(old, lapse, keep, 0f, out _), "lapsed: room again, and nothing held back for it");
        C(old.owed.Count == 0 && old.owedFor.Count == 0, "the hold let it go, and its record beside it");
        var ahead = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(ahead, 50 * D, "x|y");
        C(IllnessBudgetMath.Prune(ahead, 10 * D, keep) == 1 && ahead.owed.Count == 0, "a debt from a tick ahead of now (a save played on elsewhere) is let go");
        var uneven = new IllnessBudgetBook { owed = new List<int> { t0, t0 }, owedFor = new List<string> { "a|b" } };
        IllnessBudgetMath.Prune(uneven, t0 + D, keep);
        C(uneven.owed.Count == 2 && uneven.owedFor.Count == 2, "the debts and what they were for stay side by side");
        C(IllnessBudgetMath.Lapsed(Never, t0, keep) && !IllnessBudgetMath.Within(Never, t0, 30f) && !IllnessBudgetMath.Within(t0 + D, t0, 30f), "never, or ahead of now, is no time at all");
        var later = new IllnessBudgetBook { lastGame = 50 * D };
        C(IllnessBudgetMath.Room(later, 10 * D, keep) == BudgetRoom.Open, "the game's own from a tick ahead of now holds nothing back");

        // ================= Saying it, and what's let go =================

        var say = new IllnessBudgetBook();
        C(IllnessBudgetMath.SayPutOff(say, t0), "a case put off is said");
        say.lastPutOff = t0;
        C(!IllnessBudgetMath.SayPutOff(say, t0 + 29 * D) && IllnessBudgetMath.SayPutOff(say, t0 + 30 * D), "at most once a gap a home, the rest counted only");
        C(IllnessBudgetMath.Forgettable(new IllnessBudgetBook(), t0, keep), "a home with nothing in its ledger is let go");
        C(!IllnessBudgetMath.Forgettable(b, t2 + D, keep), "one the game's disease came to lately is kept (the pause and the easing depend on it)");
        C(IllnessBudgetMath.Forgettable(b, t2 + (int)(keep * D) + 61 * D, keep), "and let go once all of it is older than a debt is kept");
        IllnessBudgetMath.Split("", out string e1, out string e2);
        IllnessBudgetMath.Split("Council_Ail_Measles", out string e3, out string e4);
        C(e1 == "" && e2 == "" && e3 == "Council_Ail_Measles" && e4 == "" && IllnessBudgetMath.Join("a|x", null) == "ax|", "what a debt was for reads back whatever was saved");

        // ================= Played out over the years =================

        // Each case: the game's own every 50 days (a temperate forest at Strive to survive), Rimfolk's serious ones wanted at
        // this many a year (times the readiness with the budget on), the colony's own gap of thirty days between them; with
        // the budget and without.
        foreach (float wanted in new[] { 0.15f, 0.3f, 0.6f, 2f, 8f })
        {
            var off = Play(50f, wanted, false, 4000, 11);
            var on = Play(50f, wanted, true, 4000, 11);
            double gameRate = 1.2;
            // Without the budget Rimfolk's come as wanted, less the colony's own thirty days after each: w / (1 + w/2) a year.
            double alone = wanted / (1.0 + wanted / 2.0);
            C(Math.Abs(off.game - gameRate) < 0.08 && Math.Abs(off.rimfolk - alone) / alone < 0.1,
                $"without the budget the game's own come at their rate ({off.game:0.00}) and Rimfolk's on top ({off.rimfolk:0.00}), {wanted} wanted a year");
            double total = on.game + on.rimfolk;
            C(total <= gameRate * IllnessBudgetMath.MostInAll * 1.04, $"with it serious illness in all stays within about a quarter of the game's own rate: {total:0.00} a year against {gameRate:0.00}, {wanted} of Rimfolk's wanted");
            C(total < off.game + off.rimfolk + 0.02, $"and below what it was without the budget ({off.game + off.rimfolk:0.00})");
            if (wanted <= 0.6f)
                C(total <= gameRate * (1f + IllnessBudgetMath.TargetAbove), $"few or some of Rimfolk's wanted: at most {IllnessBudgetMath.TargetAbove:P0} above the game's own ({total / gameRate - 1.0:P0})");
            C(on.game >= gameRate * (1f - IllnessBudgetMath.HoldChance) - 0.05, $"the game's own still come: {on.game:0.00} a year");
            double met = on.held + on.came;
            if (met > 0.05)
                C(Math.Abs(on.held / met - IllnessBudgetMath.HoldChance) < 0.05, $"of the game's diseases that met a debt, {IllnessBudgetMath.HoldChance:P0} were held back ({on.held / met:P0})");
            C(met <= on.rimfolk + 0.02 && met >= on.rimfolk * 0.85 - 0.02, $"nearly every case of Rimfolk's meets one of the game's: held back or let go ({on.held:0.00} + {on.came:0.00}, {on.rimfolk:0.00} cases)");
            if (wanted >= 2f) C(on.rimfolk < off.rimfolk * IllnessBudgetMath.Readiness, $"many wanted: the debts hold Rimfolk's back ({on.rimfolk:0.00} a year)");
        }
        var slow = Play(75f, 0.6f, true, 4000, 12);
        C(slow.game + slow.rimfolk <= 0.8 * IllnessBudgetMath.MostInAll * 1.06, $"at Adventure story's rate likewise ({slow.game + slow.rimfolk:0.00} against 0.80)");

        // ================= The balance model's tables against the targets =================

        string report = File.ReadAllText(Path.Combine(root, "docs", "balance-report.md"));
        var (main, twos) = Tables(report);
        C(main.Count == 12 && twos.Count == 12, $"the balance report has the budget's two tables at both intervals ({main.Count} rows, {twos.Count} rows of two at once)");
        foreach (var r in main)
        {
            string label = $"{r.name} at {r.game:0.00} a year";
            if (r.name.EndsWith("average"))
                C(r.share >= IllnessBudgetMath.TargetShareMin - 0.005 && r.share <= IllnessBudgetMath.TargetShareMax + 0.005,
                    $"{label}: Rimfolk's share of the serious cases {r.share:P0}, within {IllnessBudgetMath.TargetShareMin:P0} to {IllnessBudgetMath.TargetShareMax:P0}");
            if (!r.name.EndsWith("neglectful"))
                C(r.with <= r.game * (1 + IllnessBudgetMath.TargetAbove) + 0.005, $"{label}: in all {r.with:0.00}, at most {IllnessBudgetMath.TargetAbove:P0} above the game's own");
            else
                C(r.with <= r.without * 0.75, $"{label}: in all {r.with:0.00}, well below what it was without the budget ({r.without:0.00})");
            C(r.with >= r.game * 0.98 - 0.01 && r.with <= r.game * (IllnessBudgetMath.MostInAll + 0.05), $"{label}: never below the game's own nor much over a quarter above it");
            C(r.twoWith <= r.twoWithout + 0.005, $"{label}: two serious illnesses at once no more often than without the budget ({r.twoWith:0.00} against {r.twoWithout:0.00})");
            C(r.twoWith <= IllnessBudgetMath.TargetTwoAtOnce, $"{label}: two at once rare ({r.twoWith:0.00} a colony-year)");
        }
        foreach (var (name, rimfolkOnGame, gameOnRimfolk) in twos)
            C(rimfolkOnGame + gameOnRimfolk <= IllnessBudgetMath.TargetTwoAtOnceRimfolk, $"{name}: two at once with one of them Rimfolk's, {rimfolkOnGame + gameOnRimfolk:0.000} a colony-year");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string budget = Code("Ailments/IllnessBudget.cs"), tracker = Code("Ailments/AilmentTracker.cs"), worker = Code("Ailments/IncidentWorker_Ailment.cs");
        C(Regex.IsMatch(budget, @"\[HarmonyPatch\(typeof\(Storyteller\), nameof\(Storyteller\.TryFire\)\)\]\s*public static class Patch_IllnessBudget"),
            "the hook sits where the storyteller fires an incident");
        C(budget.Contains("RimfolkPatches.Applied.Contains(nameof(Patch_IllnessBudget))") && budget.Contains("RimfolkPatches.On"), "the budget works only with its hook in place and the deeper hooks on");
        C(budget.Contains("fi?.source != null") && budget.Contains("IncidentCategoryDefOf.DiseaseHuman") && budget.Contains("typeof(IncidentWorker_Ailment)")
          && budget.Contains("parms?.forced") && budget.Contains("parms?.quest != null || fi?.sourceQuestPart != null") && budget.Contains("m.IsPlayerHome"),
            "the game's side asks each of the rules of what counts");
        C(budget.Contains("fi.def.Worker.CanFireNow(fi.parms)"), "only a disease that could really have come is asked");
        C(budget.Contains("IllnessBudgetMath.Hold(b, now, keep, Rand.Value, out string paidFor)") && budget.Contains("IllnessBudgetMath.GameCame(LedgerOf(map, true), Now, KeepDays(map))"),
            "the game's side rolls the hold, and lets the debt go when the disease comes all the same");
        C(budget.Contains("IllnessBudgetMath.Allows(b, now, keep, Rand.Value)"), "a case asks the room's chance, the easing's included");
        var m = Regex.Match(tracker, @"SeriousGapDays = ([\d.]+)f;");
        C(m.Success && Near(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), IllnessBudgetMath.GapDays), "the gap a put-off line waits is the serious illnesses' own gap");
        C(tracker.Contains("SeriousRoom(def) && IllnessBudget.Allows(IllnessBudget.HomeOf(p), def, p)"), "a case from a colonist's day asks the budget once the colony's own room is open");
        C(tracker.Contains("if (serious && !IllnessBudget.Allows(map, pick.def.diseaseIncident, null)) return false;"), "the catching clock's serious one asks it too");
        C(Regex.IsMatch(tracker, @"if \(!spreading\)\s*\{[^}]*IllnessBudget\.Owe\(IllnessBudget\.HomeOf\(p\), def, p\);"), "a new case owes its skip, one passed on doesn't");
        C(worker.Contains("if (Serious) IllnessBudget.Owe("), "the catching clock's serious case owes one, once however many it took");
        C(tracker.Contains("IllnessBudget.LetterLine(IllnessBudget.HomeOf(p), h.def, p)") && worker.Contains("IllnessBudget.LetterLine(") && Code("Ailments/LifeRisks.cs").Contains("IllnessBudget.RecordLine(map)"),
            "the serious illness's letters and the advisor's sickness line say so");

        // The readiness and two at once.
        C(tracker.Contains("return f * SeriousFactor(p, info);") && tracker.Contains("info.serious ? IllnessBudget.SeriousFactor(p) : 1f"),
            "every new serious case's chance from a colonist's day goes through the budget's factor");
        C(tracker.Contains("Rand.Chance(diet.chance * Freq * SeriousFactor(p, info))") && tracker.Contains("Rand.Chance(cause.chance * vulnerable * Freq * SeriousFactor(p, info))"),
            "the two causes without a person's factor too (scurvy, pneumonia after a cold or the flu)");
        int personRolls = Regex.Matches(tracker, @"Rand\.Chance\([^;]*PersonFactor\(").Count;
        C(personRolls >= 8, $"the person's factor is in every other roll for a new ailment ({personRolls})");
        C(tracker.Contains("IllnessBudget.SpreadFactor(other)"), "passed on between roommates: far less to someone already seriously ill");
        C(tracker.Contains("LifeRisks.SicknessFactor(map) * IllnessBudget.Readiness"), "the catching clock's serious one comes more readily as the rest do");
        C(worker.Contains("IllnessBudget.Spare(chosen, PotentialVictims(parms.target))"), "the catching clock's serious one mostly spares those already seriously ill");
        C(budget.Contains("IllnessBudgetMath.ReadinessAt(Find.Storyteller?.difficulty?.diseaseIntervalFactor ?? 1f)"), "the readiness leans with the storyteller's disease interval");
        C(budget.Contains("inc.category == IncidentCategoryDefOf.DiseaseHuman") && budget.Contains("gameDiseases.Add(inc.diseaseIncident)") && budget.Contains("def.lethalSeverity > 0f")
          && budget.Contains("AilmentTracker.InfoOf(def)"), "seriously ill: Rimfolk's serious ones, anything the game's disease events give, or another that can kill");
        C(Regex.IsMatch(budget, @"public static float SeriousFactor\(Pawn p\) => On \?") && Regex.IsMatch(budget, @"public static float SpreadFactor\(Pawn p\) => On \?")
          && Regex.IsMatch(budget, @"public static float Readiness => On \?") && budget.Contains("if (!On || chosen == null"), "with the budget off, all of it as before");

        // Saved, and an older save.
        C(tracker.Contains("Scribe_Collections.Look(ref budget, \"illnessBudget\", LookMode.Value, LookMode.Deep") && tracker.Contains("budget ??= new Dictionary<int, IllnessLedger>();"),
            "the ledgers are saved per home, and an older save without them starts owing nothing");
        C(tracker.Contains("PruneBudget();") && tracker.Contains("b.owed.Clear();"), "gone homes' ledgers are let go, and with the budget off nothing is owed");
        C(Regex.IsMatch(budget, "Scribe_Values.Look\\(ref lastGame, \"lastGame\", IllnessBudgetMath.Never\\)") && budget.Contains("owed ??= new List<int>();"), "a ledger loads whole");
        C(budget.Contains("Scribe_Values.Look(ref cameAnyway, \"cameAnyway\");"), "the diseases that came all the same are saved, and an older save starts them at none");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(model.Contains("\"Ailments/AilmentTracker.cs:budget\"") && model.Contains("\"Ailments/IllnessBudget.cs:owed\"") && model.Contains("\"Ailments/IllnessBudget.cs:owedFor\"")
          && model.Contains("came all the same"), "the save model has the ledgers");

        // The setting.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool illnessBudget = true;") && settings.Contains("Scribe_Values.Look(ref illnessBudget, \"illnessBudget\", true);"), "the setting is on by default and saved");
        C(Regex.IsMatch(catalog, "Toggle\\(\"seriousIllnesses\"[^\\n]*\\n[^\\n]*\\n\\s*Toggle\\(\"illnessBudget\", \"CC_Settings_IllnessBudget\", \"CC_SetNote_IllnessBudget\", \"seriousIllnesses,deepHooks\"\\);"),
            "it sits with the ailments on How hard, under serious illnesses, needing the deeper hooks");

        // The words.
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_IllnessBudget.xml"));
        foreach (var k in new[]
                 {
                     "CC_Settings_IllnessBudget", "CC_Settings_IllnessBudget_Tip", "CC_SetNote_IllnessBudget", "CC_Adv_Sickness_DetailShared", "CC_IllBudget_Letter", "CC_IllBudget_AnIllness",
                     "CC_IllBudget_Held", "CC_IllBudget_Held_2", "CC_IllBudget_HeldAny", "CC_IllBudget_HeldAny_2",
                     "CC_IllBudget_PutOff_Waiting", "CC_IllBudget_PutOff_Waiting_2", "CC_IllBudget_PutOff_WaitingAny", "CC_IllBudget_PutOff_WaitingAny_2",
                     "CC_IllBudget_PutOff_Share", "CC_IllBudget_PutOff_Share_2", "CC_IllBudget_PutOff_ShareAny", "CC_IllBudget_PutOff_ShareAny_2"
                 })
            C(keyed.Contains("<" + k + ">"), "the words have " + k);
        var lines = Regex.Matches(keyed, @"<(CC_\w+)>([^<]*)</").Cast<Match>().ToList();
        C(lines.All(l => !l.Groups[2].Value.Contains("—") && !l.Groups[2].Value.Contains("!")), "no dashes or exclamation marks in its lines");
        C(lines.Where(l => l.Groups[1].Value.Contains("Held")).All(l => l.Groups[2].Value.Contains("{DISEASE}") && l.Groups[2].Value.Contains("{ILLNESS}")),
            "a disease held back names the game's and Rimfolk's that took its place");
        C(lines.Where(l => l.Groups[1].Value.StartsWith("CC_IllBudget_Held") && !l.Groups[1].Value.Contains("Any")).All(l => l.Groups[2].Value.Contains("{WHO}"))
          && lines.Where(l => l.Groups[1].Value.Contains("Any")).All(l => !l.Groups[2].Value.Contains("{WHO}") && !l.Groups[2].Value.Contains("{PAWN")),
            "with someone to name, it names them; without, it names nobody");
        C(lines.Where(l => l.Groups[1].Value.Contains("PutOff") && !l.Groups[1].Value.Contains("Any")).All(l => l.Groups[2].Value.Contains("{PAWN_nameDef}")), "a case put off names who was spared");
        var placeWords = new Regex(@"\b(takes?|took|taking) (its|the|their) place|in (its|their) place", RegexOptions.IgnoreCase);
        C(lines.Where(l => placeWords.IsMatch(l.Groups[2].Value)).All(l => l.Groups[1].Value.StartsWith("CC_IllBudget_Held")),
            "only the line said when a disease was held back says a case took its place; the letter, the setting and the advisor never promise it");
        string tip = lines.First(l => l.Groups[1].Value == "CC_Settings_IllnessBudget_Tip").Groups[2].Value;
        C(tip.Contains("three times in four") && tip.Contains("week") && tip.Contains("month") && tip.Contains("One is owed at a time") && tip.Contains("rarely catches another"),
            "the setting's note gives the soft rules in plain words");

        // The docs.
        string design = File.ReadAllText(Path.Combine(root, "DESIGN.md"));
        C(design.Contains("### The shared illness budget") && design.Contains("three times in four") && !design.Contains("Rimfolk's wait 30 days there"), "DESIGN.md has the soft budget");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Ailments.xml"));
        C(almanac.Contains("shares the game's own rate") && almanac.Contains("three times in four") && !almanac.Contains("Rimfolk's wait 30 days there"), "the Almanac's illness page says so");
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        C(readme.Contains("three times in four") && !readme.Contains("Rimfolk's wait 30 days\nthere") && !readme.Contains("Rimfolk's wait 30 days there"), "the README says so");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Balance.csproj")).Contains("IllnessBudgetMath.cs"), "the balance model plays the same rules");
        return n;
    }

    /// <summary>One row of the balance report's budget table.</summary>
    public sealed class Row
    {
        public string name;
        public double game, without, with, share, twoWithout, twoWith;
    }

    /// <summary>
    /// The balance report's budget tables (Tools/Balance --illness, written in by hand): the main one's rows, and of the
    /// table of two at once, each row's "with" column: Rimfolk's on the game's and the game's on Rimfolk's.
    /// </summary>
    public static (List<Row> main, List<(string name, double rimfolkOnGame, double gameOnRimfolk)> twos) Tables(string report)
    {
        var main = new List<Row>();
        var twos = new List<(string, double, double)>();
        string header = null;
        double Num(string s) => double.Parse(s.Trim().TrimEnd('%').Trim().TrimStart('+'), CultureInfo.InvariantCulture);
        foreach (var raw in report.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("|"))
            {
                header = null;
                continue;
            }
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length > 0 && cells[0] == "Case")
            {
                header = string.Join("|", cells);
                continue;
            }
            if (header == null || cells.Length == 0 || !Regex.IsMatch(cells[0], @"^\d+ colonists, ")) continue;
            if (header.StartsWith("Case|The game's own|Without: Rimfolk's|Without: in all|With: Rimfolk's|With: the game's|With: in all|Above the game's own|Rimfolk's share") && cells.Length >= 14)
            {
                var two = cells[12].Split('/');
                main.Add(new Row
                {
                    name = cells[0], game = Num(cells[1]), without = Num(cells[3]), with = Num(cells[6]), share = Num(cells[8]) / 100.0,
                    twoWithout = Num(two[0]), twoWith = Num(two[1])
                });
            }
            else if (header.StartsWith("Case|Without: Rimfolk's on the game's|Without: the game's on Rimfolk's|Without: the game's own two|With: Rimfolk's on the game's|With: the game's on Rimfolk's")
                     && cells.Length >= 7)
                twos.Add((cells[0], Num(cells[4]), Num(cells[5])));
        }
        return (main, twos);
    }

    /// <summary>
    /// Years of one home: the game's own diseases every <paramref name="meanDays"/> on average, a serious case of Rimfolk's
    /// wanted <paramref name="wantedPerYear"/> times a year (times the readiness with the budget), the colony's own thirty days
    /// between Rimfolk's; per year, what came.
    /// </summary>
    public static (double rimfolk, double game, double held, double came, double putOff) Play(float meanDays, float wantedPerYear, bool budget, int years, int seed)
    {
        var rng = new Random(seed);
        var b = new IllnessBudgetBook();
        const int hour = D / 24;
        float keep = IllnessBudgetMath.KeepDays(meanDays);
        double pGame = 1.0 / (meanDays * 24.0), pWanted = wantedPerYear * (budget ? IllnessBudgetMath.ReadinessAt(meanDays / 50f) : 1f) / (IllnessBudgetMath.YearDays * 24.0);
        int rim = 0, game = 0, held = 0, came = 0, put = 0, last = Never;
        int hours = (int)(years * IllnessBudgetMath.YearDays * 24);
        for (int h = 0; h < hours; h++)
        {
            // Ticks pass int's reach after about 35,000 days: a fresh start each five hundred years.
            int now = (h % (500 * 60 * 24)) * hour + D;
            if (h % (500 * 60 * 24) == 0)
            {
                b = new IllnessBudgetBook();
                last = Never;
            }
            if (rng.NextDouble() < pGame)
            {
                if (budget && IllnessBudgetMath.Hold(b, now, keep, (float)rng.NextDouble(), out _)) held++;
                else
                {
                    game++;
                    if (budget)
                    {
                        int before = b.cameAnyway;
                        IllnessBudgetMath.GameCame(b, now, keep);
                        came += b.cameAnyway - before;
                    }
                }
            }
            if (rng.NextDouble() < pWanted)
            {
                if (last != Never && now - last < 30 * D) continue;
                if (budget && !IllnessBudgetMath.Allows(b, now, keep, (float)rng.NextDouble()))
                {
                    put++;
                    continue;
                }
                rim++;
                last = now;
                if (budget) IllnessBudgetMath.Owe(b, now, "x|y");
            }
        }
        return (rim / (double)years, game / (double)years, held / (double)years, came / (double)years, put / (double)years);
    }
}
