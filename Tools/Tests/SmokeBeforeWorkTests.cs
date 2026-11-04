using System;
using ColonyCouncil;

/// <summary>
/// A smoke before work for Alpha Skills' stoned passion, the second row beside the drink (Integrations/BeforeWork.cs,
/// DrinkBeforeWorkMath): the same rules, smokeleaf's own numbers from the game's 1.6 defs (a joint gives the high at
/// 0.5 and tolerance 0.03 a dose, falling 0.015 a day; addiction from tolerance 0.15; lung harm from 0.36 by the
/// carcinoma curve), a high that may slow the mind no more than smokeleaf's own, and the rules both rows share: a fight
/// no likelier than after one beer, and harm read from the game's curves.
/// </summary>
public static class SmokeBeforeWorkTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a smoke before work: " + what);
        }

        // ---------- Where a tolerance does harm, as the game says it ----------
        C(Math.Abs(DrinkBeforeWorkMath.HarmStarts(0f, true, 0.45f) - 0.45f) < 0.0001f, "a curve: harm only above its first point (minSeverity unused)");
        C(Math.Abs(DrinkBeforeWorkMath.HarmStarts(0.5f, false, 0f) - 0.5f) < 0.0001f, "no curve: from minSeverity, as older drugs say it");
        C(DrinkBeforeWorkMath.HarmStarts(0f, false, 0f) == 0f, "neither: harm from the start");
        C(Math.Abs(DrinkBeforeWorkMath.HarmStarts(0.3f, true, 0.4f) - 0.4f) < 0.0001f, "with a curve the game ignores minSeverity, so does this");
        // Beer by the game's own numbers: cirrhosis and carcinoma curves start at 0.45, addiction at 0.25.
        float beer = DrinkBeforeWorkMath.ToleranceLimit(0.25f, DrinkBeforeWorkMath.HarmStarts(0f, true, 0.45f));
        C(Math.Abs(beer - 0.15f) < 0.001f && DrinkBeforeWorkMath.ToleranceRoom(0f, 0.016f, beer), $"the game's beer fits a colonist with no tolerance ({beer})");

        // ---------- Well under smokeleaf's addiction point ----------
        float smoke = DrinkBeforeWorkMath.ToleranceLimit(0.15f, DrinkBeforeWorkMath.HarmStarts(0f, true, 0.36f));
        C(Math.Abs(smoke - 0.09f) < 0.001f, $"smokeleaf (addiction from 0.15, lung harm from 0.36): no higher than 0.09 ({smoke})");
        C(smoke <= 0.15f * 0.6f + 0.0001f, "well under the addiction point");
        C(DrinkBeforeWorkMath.ToleranceRoom(0f, 0.03f, smoke), "no tolerance yet: room for a joint");
        C(DrinkBeforeWorkMath.ToleranceRoom(0.05f, 0.03f, smoke), "a little tolerance: still room");
        C(!DrinkBeforeWorkMath.ToleranceRoom(0.07f, 0.03f, smoke), "near the limit: no joint");
        C(!DrinkBeforeWorkMath.ToleranceRoom(0.1f, 0f, smoke), "already over: no joint");
        C(DrinkBeforeWorkMath.ToleranceRoom(0.05f, DrinkBeforeWorkMath.Strength(0.03f, 1f), smoke)
          && !DrinkBeforeWorkMath.ToleranceRoom(0.05f, DrinkBeforeWorkMath.Strength(0.03f, 0.5f), smoke), "a small body takes the tolerance harder");
        // A joint a work stretch whenever there's room, twice a day, against tolerance falling 0.015 a day.
        float tolerance = 0f, worst = 0f;
        int smoked = 0;
        for (int day = 0; day < 120; day++)
        {
            for (int stretch = 0; stretch < 2; stretch++)
                if (DrinkBeforeWorkMath.ToleranceRoom(tolerance, 0.03f, smoke))
                {
                    tolerance += 0.03f;
                    smoked++;
                }
            worst = Math.Max(worst, tolerance);
            tolerance = Math.Max(0f, tolerance - 0.015f);
        }
        C(worst < 0.15f && worst < 0.36f, $"four months of it: tolerance never reaches addiction or harm ({worst:0.000})");
        C(smoked >= 55, $"and the passion still wakes about every other day ({smoked} joints)");

        // ---------- A mild high ----------
        C(Math.Abs(DrinkBeforeWorkMath.Slowing(-0.3f, 1f, 999f) - 0.3f) < 0.0001f, "smokeleaf's high: the mind 30% slower");
        C(DrinkBeforeWorkMath.SlowsWithin(DrinkBeforeWorkMath.Slowing(-0.3f, 1f, 999f), DrinkBeforeWorkMath.SmokeMaxSlowing), "smokeleaf's own high is mild enough");
        C(!DrinkBeforeWorkMath.SlowsWithin(DrinkBeforeWorkMath.Slowing(-0.5f, 1f, 999f), DrinkBeforeWorkMath.SmokeMaxSlowing), "a stronger high from another mod isn't");
        C(!DrinkBeforeWorkMath.SlowsWithin(DrinkBeforeWorkMath.Slowing(0f, 1f, 0.1f), DrinkBeforeWorkMath.SmokeMaxSlowing), "nor one that caps the mind");
        C(Math.Abs(DrinkBeforeWorkMath.Slowing(0f, 0.9f, 999f) - 0.1f) < 0.0001f && Math.Abs(DrinkBeforeWorkMath.Slowing(-0.02f, 1f, 999f) - 0.02f) < 0.0001f,
            "a factor and an offset count alike (the game's tipsy and warm stages)");
        C(DrinkBeforeWorkMath.Slowing(0.1f, 1f, 999f) == 0f, "a capacity raised slows nothing");
        C(DrinkBeforeWorkMath.SlowsWithin(0.9f, 1f), "the drink's row has no limit of its own");
        C(DrinkBeforeWorkMath.SmokeMaxSlowing < 1f && DrinkBeforeWorkMath.SmokeMaxSlowing >= 0.3f, "the smoke's limit lets smokeleaf through and nothing much stronger");

        // ---------- A fight no likelier than after one beer (both rows) ----------
        C(DrinkBeforeWorkMath.Calm(1f), "a high that touches nothing");
        C(DrinkBeforeWorkMath.Calm(1.5f), "the game's warm stage after one beer (fights 1.5 times as likely)");
        C(!DrinkBeforeWorkMath.Calm(2.5f), "tipsy (2.5 times): not before work");

        // ---------- The rules the drink keeps, the smoke keeps ----------
        C(!DrinkBeforeWorkMath.SafeSkill("Medicine", true, true) && !DrinkBeforeWorkMath.SafeSkill("Animals", true, true)
          && !DrinkBeforeWorkMath.SafeSkill("Shooting", true, true) && !DrinkBeforeWorkMath.SafeSkill("Melee", true, true), "never tending, surgery, hunting or animals");
        C(!DrinkBeforeWorkMath.SafeSkill("Cooking", false, false), "cooking only when the high makes food poisoning no likelier");
        C(!DrinkBeforeWorkMath.Due(1000, 1000 + DrinkBeforeWorkMath.CooldownTicks - 1) && DrinkBeforeWorkMath.Chance < 1f, "once in eight hours, not always yes");
        C(DrinkBeforeWorkMath.MinAge >= 18f, "adults only");
        return n;
    }
}
