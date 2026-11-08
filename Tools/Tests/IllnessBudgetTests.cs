using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The shared illness budget (Source/ColonyCouncil/Ailments/IllnessBudgetMath.cs; the player's choice of 8 November 2026):
/// which incidents count, the game's own rate, each case of Rimfolk's owing a skip, the cap, the game's disease putting
/// Rimfolk's back by the gap, debts that lapse, and played out over many years the total staying at the game's own rate;
/// and the mod's side: the hook, the setting, the saved ledgers and an older save, the words and the docs.
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
        C(Math.Exp(-IllnessBudgetMath.KeepDays(50f) / 50f) < 0.06, "on a temperate map nineteen debts in twenty are paid before they lapse");

        // ================= The share =================

        float share = IllnessBudgetMath.MaxShare(50f), shareAdv = IllnessBudgetMath.MaxShare(75f);
        C(share > 0.3f && share < 0.4f && shareAdv > share && shareAdv < 0.45f, $"Rimfolk's share at most a third on a temperate map ({share:P0}), {shareAdv:P0} at Adventure story's");
        bool belowHalf = true;
        for (float mean = 5f; mean < 2000f; mean *= 1.3f)
            if (IllnessBudgetMath.MaxShare(mean) > 0.5f + 1e-5f || IllnessBudgetMath.MaxShare(mean) <= 0f) belowHalf = false;
        C(belowHalf && Near(IllnessBudgetMath.MaxShare(IllnessBudgetMath.NoGameDiseases), 0.5f), "never more than half, at any rate of the game's");

        // ================= The debt =================

        float keep = IllnessBudgetMath.KeepDays(50f);
        var b = new IllnessBudgetBook();
        int t0 = 10 * D;
        C(IllnessBudgetMath.Room(b, t0, keep) == BudgetRoom.Open && IllnessBudgetMath.Owing(b, t0, keep) == 0, "a new home owes nothing and has room");
        C(!IllnessBudgetMath.Hold(b, t0, keep, out _), "with nothing owed, the game's disease comes as it would have");
        IllnessBudgetMath.Owe(b, t0, IllnessBudgetMath.Join("Council_Ail_Tuberculosis", "Ana"));
        C(IllnessBudgetMath.Owing(b, t0 + D, keep) == 1 && b.owedFor.Count == 1, "a serious case of Rimfolk's owes one skip");
        C(IllnessBudgetMath.Room(b, t0 + D, keep) == BudgetRoom.Owed, "while it's owed, Rimfolk starts no other there");
        int t1 = t0 + 20 * D;
        C(IllnessBudgetMath.Hold(b, t1, keep, out string paid) && paid == "Council_Ail_Tuberculosis|Ana", "the game's next disease is held back, paying the debt it was for");
        IllnessBudgetMath.Split(paid, out string ill, out string who);
        C(ill == "Council_Ail_Tuberculosis" && who == "Ana", "the records know the illness and who had it");
        C(IllnessBudgetMath.Owing(b, t1, keep) == 0 && b.lastSkip == t1 && b.held == 1, "paid: nothing owed, the skip remembered and counted");
        C(!IllnessBudgetMath.Hold(b, t1 + D, keep, out _), "one skip a case: the next comes");
        C(IllnessBudgetMath.Counted(b, "Council_Ail_Tuberculosis|Ana") && !IllnessBudgetMath.Counted(b, "Council_Ail_Typhus|Ana"),
            "the letter knows the case was counted (the skip paid for it), and one that never was");
        var owing = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(owing, t0, IllnessBudgetMath.Join("Council_Ail_Cholera", null));
        C(IllnessBudgetMath.Counted(owing, "Council_Ail_Cholera|") && !IllnessBudgetMath.Counted(null, "x|"), "and one still owed");

        // ================= The cap =================

        C(IllnessBudgetMath.Room(b, t1 + D, keep) == BudgetRoom.GameTurn, "after a skip the game's own comes before Rimfolk takes another");
        C(IllnessBudgetMath.Room(b, t1 + 100 * D, keep) == BudgetRoom.GameTurn, "however long, while a debt from then would still be kept");
        C(IllnessBudgetMath.Room(b, t1 + (int)(keep * D), keep) == BudgetRoom.Open, "but not for ever: a skip as old as a lapsed debt no longer waits");
        var two = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(two, t0, "a|b");
        IllnessBudgetMath.Owe(two, t0, "c|d");
        C(IllnessBudgetMath.Owing(two, t0, keep) == 2 && IllnessBudgetMath.Hold(two, t0 + D, keep, out string first) && first == "a|b"
          && IllnessBudgetMath.Owing(two, t0 + D, keep) == 1, "two owed (a forced case on top): paid one at a time, the oldest first");
        C(IllnessBudgetMath.MaxOwed == 1, "at most one owed before Rimfolk starts another");

        // ================= The reset: the game's diseases wait Rimfolk too =================

        int t2 = t1 + 5 * D;
        IllnessBudgetMath.GameCame(b, t2);
        C(b.lastGame == t2 && IllnessBudgetMath.Room(b, t2 + D, keep) == BudgetRoom.Waiting, "the game's own came: Rimfolk's serious illnesses wait");
        C(IllnessBudgetMath.Room(b, t2 + 29 * D, keep) == BudgetRoom.Waiting && IllnessBudgetMath.Room(b, t2 + 30 * D, keep) == BudgetRoom.Open,
            "for the gap, thirty days, then there's room again");
        C(Near(IllnessBudgetMath.WaitDays, 30f), "the gap is the serious illnesses' own");
        var fresh = new IllnessBudgetBook();
        IllnessBudgetMath.GameCame(fresh, t0);
        C(IllnessBudgetMath.Room(fresh, t0 + 10 * D, keep) == BudgetRoom.Waiting && IllnessBudgetMath.Room(fresh, t0 + 31 * D, keep) == BudgetRoom.Open,
            "a home that never owed anything waits the gap after the game's own too");

        // ================= Expiry =================

        var old = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(old, t0, "x|y");
        int lapse = t0 + (int)(keep * D);
        C(IllnessBudgetMath.Owing(old, lapse - 1, keep) == 1 && IllnessBudgetMath.Owing(old, lapse, keep) == 0, $"a debt lapses unpaid after {keep:0} days");
        C(IllnessBudgetMath.Room(old, lapse, keep) == BudgetRoom.Open && !IllnessBudgetMath.Hold(old, lapse, keep, out _), "lapsed: room again, and nothing held back for it");
        C(old.owed.Count == 0 && old.owedFor.Count == 0, "the hold let it go, and its record beside it");
        var ahead = new IllnessBudgetBook();
        IllnessBudgetMath.Owe(ahead, 50 * D, "x|y");
        C(IllnessBudgetMath.Prune(ahead, 10 * D, keep) == 1 && ahead.owed.Count == 0, "a debt from a tick ahead of now (a save played on elsewhere) is let go");
        var uneven = new IllnessBudgetBook { owed = new List<int> { t0, t0 }, owedFor = new List<string> { "a|b" } };
        IllnessBudgetMath.Prune(uneven, t0 + D, keep);
        C(uneven.owed.Count == 2 && uneven.owedFor.Count == 2, "the debts and what they were for stay side by side");
        C(IllnessBudgetMath.Lapsed(Never, t0, keep) && !IllnessBudgetMath.Within(Never, t0, 30f) && !IllnessBudgetMath.Within(t0 + D, t0, 30f), "never, or ahead of now, is no time at all");

        // ================= Saying it, and what's let go =================

        var say = new IllnessBudgetBook();
        C(IllnessBudgetMath.SayPutOff(say, t0), "a case put off is said");
        say.lastPutOff = t0;
        C(!IllnessBudgetMath.SayPutOff(say, t0 + 29 * D) && IllnessBudgetMath.SayPutOff(say, t0 + 30 * D), "at most once a gap a home, the rest counted only");
        C(IllnessBudgetMath.Forgettable(new IllnessBudgetBook(), t0, keep), "a home with nothing in its ledger is let go");
        C(!IllnessBudgetMath.Forgettable(b, t2 + D, keep), "one the game's disease came to lately is kept (the gap and the turn depend on it)");
        C(IllnessBudgetMath.Forgettable(b, t2 + (int)(keep * D) + 61 * D, keep), "and let go once all of it is older than a debt is kept");
        IllnessBudgetMath.Split("", out string e1, out string e2);
        IllnessBudgetMath.Split("Council_Ail_Measles", out string e3, out string e4);
        C(e1 == "" && e2 == "" && e3 == "Council_Ail_Measles" && e4 == "" && IllnessBudgetMath.Join("a|x", null) == "ax|", "what a debt was for reads back whatever was saved");

        // ================= Played out over the years =================

        // Each case: the game's own every 50 days (a temperate forest at Strive to survive), Rimfolk's serious ones wanted at
        // this many a year, the colony's own gap of thirty days between them; with the budget and without.
        foreach (float wanted in new[] { 0.3f, 0.6f, 2f, 8f })
        {
            var off = Play(50f, wanted, false, 4000, 11);
            var on = Play(50f, wanted, true, 4000, 11);
            double gameRate = 1.2;
            // Without the budget Rimfolk's come as wanted, less the colony's own thirty days after each: w / (1 + w/2) a year.
            double alone = wanted / (1.0 + wanted / 2.0);
            C(Math.Abs(off.game - gameRate) < 0.08 && Math.Abs(off.rimfolk - alone) / alone < 0.1,
                $"without the budget the game's own come at their rate ({off.game:0.00}) and Rimfolk's on top ({off.rimfolk:0.00}), {wanted} wanted a year");
            double total = on.game + on.rimfolk;
            C(Math.Abs(total - gameRate) / gameRate < 0.08, $"with it serious illness in all stays at the game's own rate: {total:0.00} a year against {gameRate:0.00}, {wanted} of Rimfolk's wanted");
            double rimShare = on.rimfolk / Math.Max(1e-9, total);
            C(rimShare <= share + 0.03, $"Rimfolk's share stays under its cap: {rimShare:P0}, at most {share:P0}");
            C(on.game >= gameRate * (1.0 - share) - 0.06, $"the game's own still come: {on.game:0.00} a year");
            C(Math.Abs(on.held - on.rimfolk) / Math.Max(0.05, on.rimfolk) < 0.08, $"every case of Rimfolk's is paid for by a skip, near enough ({on.held:0.00} held back, {on.rimfolk:0.00} cases)");
            // Few wanted: the game's own leave Rimfolk's about half their turns (the thirty days after each, e^(-30/50) of the time free).
            if (wanted <= 0.3f)
                C(on.rimfolk > 0.3 * off.rimfolk && on.rimfolk < 0.7 * off.rimfolk, $"few wanted: about half of Rimfolk's still come, the rest waiting out the game's own ({on.rimfolk:0.00} of {off.rimfolk:0.00})");
            if (wanted >= 8f) C(rimShare > share - 0.06, $"many wanted: Rimfolk takes nearly all of its share ({rimShare:P0})");
        }

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
        C(budget.Contains("fi.def.Worker.CanFireNow(fi.parms)"), "only a disease that could really have come is held back");
        var m = Regex.Match(tracker, @"SeriousGapDays = ([\d.]+)f;");
        C(m.Success && Near(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), IllnessBudgetMath.WaitDays), "the gap after the game's own is the serious illnesses' own gap");
        C(tracker.Contains("SeriousRoom(def) && IllnessBudget.Allows(IllnessBudget.HomeOf(p), def, p)"), "a case from a colonist's day asks the budget once the colony's own room is open");
        C(tracker.Contains("if (serious && !IllnessBudget.Allows(map, pick.def.diseaseIncident, null)) return false;"), "the catching clock's serious one asks it too");
        C(Regex.IsMatch(tracker, @"if \(!spreading\)\s*\{[^}]*IllnessBudget\.Owe\(IllnessBudget\.HomeOf\(p\), def, p\);"), "a new case owes its skip, one passed on doesn't");
        C(worker.Contains("if (Serious) IllnessBudget.Owe("), "the catching clock's serious case owes one, once however many it took");
        C(tracker.Contains("IllnessBudget.LetterLine(IllnessBudget.HomeOf(p), h.def, p)") && worker.Contains("IllnessBudget.LetterLine(") && Code("Ailments/LifeRisks.cs").Contains("IllnessBudget.RecordLine(map)"),
            "the serious illness's letters and the advisor's sickness line say so");

        // Saved, and an older save.
        C(tracker.Contains("Scribe_Collections.Look(ref budget, \"illnessBudget\", LookMode.Value, LookMode.Deep") && tracker.Contains("budget ??= new Dictionary<int, IllnessLedger>();"),
            "the ledgers are saved per home, and an older save without them starts owing nothing");
        C(tracker.Contains("PruneBudget();") && tracker.Contains("b.owed.Clear();"), "gone homes' ledgers are let go, and with the budget off nothing is owed");
        C(Regex.IsMatch(budget, "Scribe_Values.Look\\(ref lastGame, \"lastGame\", IllnessBudgetMath.Never\\)") && budget.Contains("owed ??= new List<int>();"), "a ledger loads whole");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(model.Contains("\"Ailments/AilmentTracker.cs:budget\"") && model.Contains("\"Ailments/IllnessBudget.cs:owed\"") && model.Contains("\"Ailments/IllnessBudget.cs:owedFor\""),
            "the save model has the ledgers");

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
        var lines = Regex.Matches(keyed, @"<(CC_IllBudget_\w+)>([^<]*)</").Cast<Match>().ToList();
        C(lines.All(l => !l.Groups[2].Value.Contains("—") && !l.Groups[2].Value.Contains("!")), "no dashes or exclamation marks in its lines");
        C(lines.Where(l => l.Groups[1].Value.Contains("Held")).All(l => l.Groups[2].Value.Contains("{DISEASE}") && l.Groups[2].Value.Contains("{ILLNESS}")),
            "a disease held back names the game's and Rimfolk's that took its place");
        C(lines.Where(l => l.Groups[1].Value.StartsWith("CC_IllBudget_Held") && !l.Groups[1].Value.Contains("Any")).All(l => l.Groups[2].Value.Contains("{WHO}"))
          && lines.Where(l => l.Groups[1].Value.Contains("Any")).All(l => !l.Groups[2].Value.Contains("{WHO}") && !l.Groups[2].Value.Contains("{PAWN")),
            "with someone to name, it names them; without, it names nobody");
        C(lines.Where(l => l.Groups[1].Value.Contains("PutOff") && !l.Groups[1].Value.Contains("Any")).All(l => l.Groups[2].Value.Contains("{PAWN_nameDef}")), "a case put off names who was spared");

        // The docs.
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### The shared illness budget"), "DESIGN.md has the budget");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Ailments.xml"));
        C(almanac.Contains("shares the game's own rate"), "the Almanac's illness page says so");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Balance.csproj")).Contains("IllnessBudgetMath.cs"), "the balance model plays the same rules");
        return n;
    }

    /// <summary>
    /// Years of one home: the game's own diseases every <paramref name="meanDays"/> on average, a serious case of Rimfolk's
    /// wanted <paramref name="wantedPerYear"/> times a year, the colony's own thirty days between Rimfolk's; per year, what came.
    /// </summary>
    public static (double rimfolk, double game, double held, double putOff) Play(float meanDays, float wantedPerYear, bool budget, int years, int seed)
    {
        var rng = new Random(seed);
        var b = new IllnessBudgetBook();
        const int hour = D / 24;
        float keep = IllnessBudgetMath.KeepDays(meanDays);
        double pGame = 1.0 / (meanDays * 24.0), pWanted = wantedPerYear / (IllnessBudgetMath.YearDays * 24.0);
        int rim = 0, game = 0, held = 0, put = 0, last = Never;
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
                if (budget && IllnessBudgetMath.Hold(b, now, keep, out _)) held++;
                else
                {
                    game++;
                    if (budget) IllnessBudgetMath.GameCame(b, now);
                }
            }
            if (rng.NextDouble() < pWanted)
            {
                if (last != Never && now - last < 30 * D) continue;
                if (budget && IllnessBudgetMath.Room(b, now, keep) != BudgetRoom.Open)
                {
                    put++;
                    continue;
                }
                rim++;
                last = now;
                if (budget) IllnessBudgetMath.Owe(b, now, "x|y");
            }
        }
        return (rim / (double)years, game / (double)years, held / (double)years, put / (double)years);
    }
}
