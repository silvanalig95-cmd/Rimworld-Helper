using ColonyCouncil;

/// <summary>
/// The realistic ways to die (Source/ColonyCouncil/Ailments/LifeRiskMath.cs): a crowded, dirty colony catches serious
/// illness more often, within bounds; the frail fight an illness slower, and with the illnesses' own numbers
/// (AilmentMath.Simulate) steady good care still saves them while gaps or poor medicine can kill them where a healthy
/// adult would pull through; the end of a long life comes only to the old, likelier with age, over weeks and slower
/// with care; a wound left untended in the dirt is likelier to turn bad, never less likely than the game has it.
/// </summary>
public static class LifeRiskTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "life risks: " + what);
        }

        // ---------- Sickness in a crowded, dirty colony ----------
        Check(Math.Abs(LifeRiskMath.SicknessFactor(8, 0, 0f) - 1f) < 0.001f, "eight people in their own rooms with a clean kitchen: as the illness's own numbers say");
        Check(LifeRiskMath.CrowdFactor(4, 10) > LifeRiskMath.CrowdFactor(1, 10) && LifeRiskMath.CrowdFactor(10, 10) <= 1.6f + 1e-4f, "more crowded, more sickness, within bounds");
        Check(LifeRiskMath.CrowdFactor(0, 0) == 1f && LifeRiskMath.SizeFactor(0) == 1f, "nobody, nothing");
        Check(LifeRiskMath.KitchenFactor(0f) == 1f && LifeRiskMath.KitchenFactor(-1f) == 1f, "a kitchen that's only a little dirty doesn't count");
        Check(LifeRiskMath.KitchenFactor(-2f) > 1f && LifeRiskMath.KitchenFactor(-5f) == LifeRiskMath.KitchenFactor(-3f), "a dirty kitchen counts, up to a point");
        Check(LifeRiskMath.SizeFactor(10) < 1.2f && LifeRiskMath.SizeFactor(30) > LifeRiskMath.SizeFactor(10) && LifeRiskMath.SizeFactor(200) <= 1.6f, "size counts gently");
        Check(LifeRiskMath.SicknessFactor(10, 0, 0f) < LifeRiskMath.SicknessWorthSaying, "a well-kept colony of ten isn't warned about");
        Check(LifeRiskMath.SicknessFactor(500, 500, -10f) <= LifeRiskMath.MaxSicknessFactor, "never past the cap");
        Check(LifeRiskMath.SicknessFactor(10, 0, null) == LifeRiskMath.SicknessFactor(10, 0, 0f), "no kitchen: nothing for it");
        for (int people = 1; people <= 40; people += 3)
            for (int crowded = 0; crowded <= people; crowded += 2)
                Check(LifeRiskMath.SicknessFactor(people, crowded, -2f) >= 1f * LifeRiskMath.SizeFactor(people) - 1e-4f, $"crowding and dirt only ever add ({people}, {crowded})");

        // ---------- The frail ----------
        Check(LifeRiskMath.Frailty(30f, 0f, 1f) == 1f && LifeRiskMath.Frailty(60f, 0f, 1f) == 1f, "a healthy adult fights at full strength");
        Check(LifeRiskMath.AgeFrailty(70f) > LifeRiskMath.AgeFrailty(80f) && LifeRiskMath.AgeFrailty(80f) > LifeRiskMath.AgeFrailty(90f) && LifeRiskMath.AgeFrailty(120f) == 0.7f, "slower with age, down to a floor");
        Check(LifeRiskMath.ChildFrailty(2f) < LifeRiskMath.ChildFrailty(4f) && LifeRiskMath.ChildFrailty(8f) == 1f, "small children slower, older ones not");
        Check(LifeRiskMath.HungerFrailty(0f) == 1f && LifeRiskMath.HungerFrailty(1f) < LifeRiskMath.HungerFrailty(0.5f), "the hungrier, the slower");
        Check(LifeRiskMath.HurtFrailty(0.4f) < 1f && LifeRiskMath.HurtFrailty(0.9f) == 1f, "badly hurt, slower");
        Check(LifeRiskMath.Frailty(95f, 1f, 0.1f) == LifeRiskMath.MinFrailty, "never below the floor");

        var serious = AilmentTests.Load(Path.Combine(root, "Defs", "HediffDefs", "Council_Hediffs_Illnesses.xml")).Where(s => s.lethal && s.immunizable).ToList();
        Check(serious.Count >= 5, "the lethal illnesses are found");
        float old = LifeRiskMath.Frailty(90f, 0f, 1f);
        int frailOnly = 0;
        foreach (var s in serious)
        {
            // Steady good care with industrial medicine saves even the frailest.
            var best = AilmentMath.Simulate(s.course, 0.8f, s.industrial, 1f, LifeRiskMath.MinFrailty);
            Check(!best.died, $"{s.name}: steady good care saves even the frailest (peak {best.peak:0.00})");
            // Where a healthy adult pulls through steady herbal care and a ninety-year-old doesn't, frailty is a real risk.
            var healthy = AilmentMath.Simulate(s.course, 0.7f, s.basic, 1f);
            var frail = AilmentMath.Simulate(s.course, 0.7f, s.basic, 1f, old);
            if (!healthy.died && frail.died) frailOnly++;
            Check(frail.peak >= healthy.peak - 1e-4f, $"{s.name}: never easier for the frail");
        }
        Check(frailOnly >= 3, $"a few illnesses kill the frail under care a healthy adult survives ({frailOnly})");

        // ---------- The end of a long life ----------
        Check(LifeRiskMath.YearlyChance(60f) == 0f && LifeRiskMath.YearlyChance(LifeRiskMath.LastSeasonFrom) == 0f, "not before old age");
        Check(Math.Abs(LifeRiskMath.YearlyChance(80f) - 0.1f) < 1e-4f && LifeRiskMath.YearlyChance(90f) > 0.5f && LifeRiskMath.YearlyChance(150f) < 1f, "likelier with age, never certain");
        for (float a = 70f; a < 110f; a += 0.5f)
            Check(LifeRiskMath.YearlyChance(a + 0.5f) >= LifeRiskMath.YearlyChance(a), $"never less likely older ({a})");
        float daily = LifeRiskMath.DailyChance(0.3f);
        Check(Math.Abs(1f - Math.Pow(1f - daily, 60) - 0.3f) < 1e-3f, "a day's chance adds up to the year's");
        Check(LifeRiskMath.DailyChance(0f) == 0f && LifeRiskMath.DailyChance(1f) == 1f, "none and certain stay so");
        float plain = 1f / LifeRiskMath.DeclinePerDay(0.5f, false, false);
        float cared = 1f / LifeRiskMath.DeclinePerDay(0.8f, true, true);
        float alone = 1f / LifeRiskMath.DeclinePerDay(0.2f, false, false);
        Check(plain >= 15f && plain <= 25f, $"about a season ({plain:0} days)");
        Check(cared > plain && alone < plain, "care slows it, misery hastens it");
        Check(cared <= 40f && alone >= 10f, $"weeks either way ({alone:0} to {cared:0} days)");
        Check(LifeRiskMath.TurnsBack(70f) && !LifeRiskMath.TurnsBack(LifeRiskMath.LastSeasonFrom), "made young again, it turns back");

        // ---------- Wounds left untended ----------
        Check(LifeRiskMath.DirtFactor(2f, false) == 1f && LifeRiskMath.DirtFactor(0f, false) == 1f, "clean ground: as the game has it");
        Check(LifeRiskMath.DirtFactor(-2f, false) > 1f && LifeRiskMath.DirtFactor(-6f, false) == 1.5f && LifeRiskMath.DirtFactor(5f, true) > 1f, "dirt and the open make it likelier");
        Check(LifeRiskMath.ExtraInfectionChance(0.3f, 1f) == 0f && LifeRiskMath.ExtraInfectionChance(0f, 1.5f) == 0f, "nothing extra without dirt or a chance");
        foreach (float b in new[] { 0.05f, 0.2f, 0.5f, 0.8f })
            foreach (float dirt in new[] { 1.1f, 1.35f, 1.5f })
            {
                float extra = LifeRiskMath.ExtraInfectionChance(b, dirt);
                float total = b + (1f - b) * extra;
                Check(Math.Abs(total - Math.Min(1f, b * dirt)) < 1e-4f, $"the whole comes to the game's chance times the dirt ({b}, {dirt})");
            }
        return cases;
    }
}
