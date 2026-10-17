using ColonyCouncil;

/// <summary>
/// The colony's animals (AnimalMath): when an animal counts as old, the herd a keeper wants, how many head a hungry
/// colony culls, a barn's beds, training, a beloved animal's day on the calendar, and with PokéWorld the level
/// milestones, the Pokédex goal, a rare sighting and an evolution seen between two looks.
/// </summary>
public static class AnimalTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "animals: " + what);
        }

        // ---------- Age ----------
        Check(AnimalMath.LifeShare(5f, 10f) == 0.5f, "halfway through a ten-year life at five");
        Check(AnimalMath.LifeShare(5f, 0f) == 0f && AnimalMath.LifeShare(-1f, 10f) == 0f, "an unknown life or age is no share at all");
        Check(!AnimalMath.IsOld(7.9f, 10f) && AnimalMath.IsOld(8f, 10f), "old from four fifths of its life");
        Check(AnimalMath.IsOld(20f, 10f), "past its expected life is old");

        // ---------- The herd ----------
        Check(AnimalMath.HerdTarget(0) == 8 && AnimalMath.HerdTarget(3) == 8, "a small herd aims for eight head");
        Check(AnimalMath.HerdTarget(6) == 10 && AnimalMath.HerdTarget(20) == 24, "a bigger herd aims for four more than now");
        Check(AnimalMath.HerdTarget(-5) == 8, "a negative count is no herd");
        for (int herd = 0; herd <= 40; herd++)
        {
            int cull = AnimalMath.CullCount(herd);
            Check(cull >= 0 && cull <= 4, $"a cull of a herd of {herd} is 0 to 4 head");
            Check(herd - cull >= Math.Min(herd, 2), $"a cull of a herd of {herd} leaves at least two");
            if (herd >= 3) Check(cull >= 1, $"a herd of {herd} can spare one");
        }
        Check(AnimalMath.CullCount(2) == 0 && AnimalMath.CullCount(3) == 1, "the last two are never culled");
        Check(AnimalMath.CullCount(8) == 2 && AnimalMath.CullCount(16) == 4 && AnimalMath.CullCount(40) == 4, "about a quarter, at most four");
        Check(AnimalMath.Famine(3f, 4) && !AnimalMath.Famine(3f, 3), "a famine needs a herd of four");
        Check(!AnimalMath.Famine(6f, 10) && AnimalMath.Famine(5.9f, 10), "a famine is under six days of food");
        Check(!AnimalMath.Famine(-1f, 10), "unknown food is no famine");
        Check(AnimalMath.BarnBeds(1) == 2 && AnimalMath.BarnBeds(4) == 4 && AnimalMath.BarnBeds(30) == 6, "a barn has beds for two to six");

        // ---------- Training ----------
        Check(AnimalMath.TrainedShare(0, 0) == 1f, "nothing to learn is fully trained");
        Check(AnimalMath.TrainedShare(2, 4) == 0.5f && AnimalMath.TrainedShare(5, 4) == 1f && AnimalMath.TrainedShare(-1, 4) == 0f, "a share of what it can learn, 0 to 1");

        // ---------- The calendar ----------
        Check(!AnimalMath.CompanionDay(5f, false, true), "only a bonded animal has a day");
        Check(!AnimalMath.CompanionDay(0.5f, true, false) && AnimalMath.CompanionDay(1f, true, false), "a bonded animal a year with the colony has a day");
        Check(AnimalMath.CompanionDay(0.1f, true, true), "a bonded animal that fought for its person has a day at once");
        Check(AnimalMath.CompanionWeight(1f, true, false) < AnimalMath.CompanionWeight(1f, true, true), "fighting for its person weighs more");
        Check(AnimalMath.CompanionWeight(5f, true, false) == AnimalMath.CompanionWeight(9f, true, false), "years count up to five");
        Check(AnimalMath.CompanionWeight(1f, true, false) > AnimalMath.CompanionWeight(1f, false, false), "a bond weighs more");
        Check(AnimalMath.CompanionWeight(-3f, true, false) == AnimalMath.CompanionWeight(0f, true, false), "no negative years");

        // ---------- PokéWorld ----------
        Check(AnimalMath.MilestonePassed(24, 25) == 25 && AnimalMath.MilestonePassed(49, 51) == 50, "a milestone passed is marked");
        Check(AnimalMath.MilestonePassed(20, 80) == 75, "several at once mark the highest");
        Check(AnimalMath.MilestonePassed(25, 30) == 0 && AnimalMath.MilestonePassed(30, 30) == 0, "no milestone between them");
        Check(AnimalMath.MilestonePassed(0, 50) == 0 && AnimalMath.MilestonePassed(-1, 100) == 0, "an unknown level before marks nothing");
        Check(AnimalMath.MilestonePassed(99, 100) == 100, "level 100 is marked");
        Check(AnimalMath.MilestonePassed(60, 40) == 0, "a level going down marks nothing");
        Check(AnimalMath.DexShare(0, 0) == 0f && AnimalMath.DexShare(50, 100) == 0.5f && AnimalMath.DexShare(200, 100) == 1f, "the Pokédex share runs 0 to 1");
        Check(AnimalMath.DexGoal(10, 493, 10) == 20, "ten more species");
        Check(AnimalMath.DexGoal(488, 493, 10) == 493, "no more than are left");
        Check(AnimalMath.DexGoal(10, 493, 0) == 11, "always at least one more");
        Check(AnimalMath.DexGoal(-1, 493, 5) == 5, "an unknown count starts from none");
        for (int r = 0; r <= 11; r++)
            Check(AnimalMath.RareSighting(r) == (r >= 6 && r <= 9), $"rarity {r} is {(r >= 6 && r <= 9 ? "" : "not ")}a rare sighting");
        Check(AnimalMath.Evolved("Charmander", "Charmeleon", true), "a new kind, the old one gone alive, is an evolution");
        Check(!AnimalMath.Evolved("Charmander", "Charmeleon", false), "the old one dead is no evolution");
        Check(!AnimalMath.Evolved("Charmander", "Charmander", true), "the same kind is no evolution");
        Check(!AnimalMath.Evolved(null, "Charmeleon", true) && !AnimalMath.Evolved("Charmander", "", true), "an unknown kind is no evolution");
        Check(AnimalMath.BondFriendship > 0 && AnimalMath.BondFriendship <= 255, "the bond's friendship is on PokéWorld's scale");
        return cases;
    }
}
