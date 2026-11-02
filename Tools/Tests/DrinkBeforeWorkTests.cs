using System;
using ColonyCouncil;

/// <summary>
/// A drink before work for Alpha Skills' drunken passion (Integrations/DrinkBeforeWorkMath): once a work stretch, most
/// of the time, a short walk away and near the work, only before work a little drink can't make dangerous, and never
/// near the tolerance where an addiction or harm can start.
/// </summary>
public static class DrinkBeforeWorkTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a drink before work: " + what);
        }

        // ---------- Once a work stretch ----------
        C(DrinkBeforeWorkMath.CooldownTicks == 8 * 2500, "the wait between choices is eight hours");
        C(DrinkBeforeWorkMath.Due(-1, 100), "no choice made yet: time for one");
        C(!DrinkBeforeWorkMath.Due(1000, 1000 + DrinkBeforeWorkMath.CooldownTicks - 1), "within eight hours: no second choice");
        C(DrinkBeforeWorkMath.Due(1000, 1000 + DrinkBeforeWorkMath.CooldownTicks), "after eight hours: the next stretch");
        C(DrinkBeforeWorkMath.Due(50000, 10), "a clock that went back (another game loaded) starts over");
        C(DrinkBeforeWorkMath.Chance >= 0.6f && DrinkBeforeWorkMath.Chance <= 0.8f, "most of the time, not always");

        // ---------- Near, not across the map ----------
        C(DrinkBeforeWorkMath.Near(10f, 12f), "a stockpile on the way to the bench");
        C(!DrinkBeforeWorkMath.Near(DrinkBeforeWorkMath.MaxDistance + 1f, 5f), "too far from the colonist");
        C(!DrinkBeforeWorkMath.Near(10f, DrinkBeforeWorkMath.MaxDetour + 1f), "near the colonist but far from the work: a detour");
        C(DrinkBeforeWorkMath.Near(10f, -1f), "work with no place of its own: only the walk to the drink counts");

        // ---------- Only work a little drink can't make dangerous ----------
        foreach (var s in new[] { "Crafting", "Construction", "Mining", "Plants", "Intellectual", "Artistic" })
            C(DrinkBeforeWorkMath.SafeSkill(s, false, false), s + " may be done a little drunk");
        foreach (var s in new[] { "Medicine", "Shooting", "Melee", "Animals" })
            C(!DrinkBeforeWorkMath.SafeSkill(s, true, true), s + " never: someone could get hurt");
        C(!DrinkBeforeWorkMath.SafeSkill("SomeModSkill", true, true), "a skill another mod adds: not without knowing what it does");
        C(DrinkBeforeWorkMath.SafeSkill("Cooking", true, false) && !DrinkBeforeWorkMath.SafeSkill("Cooking", false, false),
            "cooking only when the drink makes food poisoning no likelier");
        C(DrinkBeforeWorkMath.SafeSkill("Social", false, true) && !DrinkBeforeWorkMath.SafeSkill("Social", true, false),
            "social work only as a talk with a prisoner");

        // ---------- Well under the tolerance where addiction or harm begins ----------
        float beer = DrinkBeforeWorkMath.ToleranceLimit(0.25f, 0.5f);
        C(Math.Abs(beer - 0.15f) < 0.001f, $"beer (addiction from tolerance 0.25, harm from 0.5): no higher than 0.15 ({beer})");
        C(DrinkBeforeWorkMath.ToleranceLimit(0f, 0f) == DrinkBeforeWorkMath.ToleranceCap, "nothing known: the cap");
        C(DrinkBeforeWorkMath.ToleranceLimit(0.1f, 0f) < 0.1f, "an earlier addiction point lowers the limit under it");
        C(DrinkBeforeWorkMath.ToleranceLimit(0.9f, 0.9f) == DrinkBeforeWorkMath.ToleranceCap, "a mild drink still never goes over the cap");
        C(DrinkBeforeWorkMath.ToleranceRoom(0.05f, 0.016f, beer), "a low tolerance has room for a beer");
        C(!DrinkBeforeWorkMath.ToleranceRoom(0.14f, 0.016f, beer), "near the limit: no drink");
        C(!DrinkBeforeWorkMath.ToleranceRoom(0.2f, 0f, beer), "already over: no drink");
        // A beer a work stretch, twice a day, against the game's tolerance falling 0.015 a day: the limit holds it.
        float tolerance = 0f;
        int drunk = 0;
        for (int day = 0; day < 120; day++)
        {
            for (int stretch = 0; stretch < 2; stretch++)
                if (DrinkBeforeWorkMath.ToleranceRoom(tolerance, 0.016f, beer))
                {
                    tolerance += 0.016f;
                    drunk++;
                }
            tolerance = Math.Max(0f, tolerance - 0.015f);
        }
        C(tolerance < 0.25f && drunk > 100, $"four months of it: tolerance stays under where addiction starts ({tolerance:0.000}, {drunk} drinks)");

        // ---------- The drink's strength in this body ----------
        C(Math.Abs(DrinkBeforeWorkMath.Strength(0.15f, 1f) - 0.15f) < 0.001f, "one beer in a grown body");
        C(DrinkBeforeWorkMath.Strength(0.15f, 0.5f) > 0.25f, "the same beer in a small body is stronger");
        C(!float.IsInfinity(DrinkBeforeWorkMath.Strength(0.15f, 0f)), "never divided by nothing");
        C(DrinkBeforeWorkMath.MildStages == 2 && DrinkBeforeWorkMath.MinAge >= 18f, "warm or tipsy at most, and adults only");
        return n;
    }
}
