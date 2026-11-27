using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the content round's third set of council questions (docs/content-round-3-council.md;
    /// Dilemmas/DilemmaWorkers_Round3.cs), without game types so the tests can check them: how lately something must have
    /// happened, how far from the door a bed is quiet, how much of a thing is worth a question, who eats first, how long
    /// work is handed over.
    /// </summary>
    public static class Round3CouncilMath
    {
        // ---------------- The light sleeper ----------------

        /// <summary>Woken in the last day (the game's own "disturbed sleep").</summary>
        public const float DisturbedDays = 1f;

        /// <summary>The other bed is this many steps farther from the room's door, or more.</summary>
        public const float FartherBy = 2f;

        /// <summary>The other bed is far enough from the door to be quieter: both distances known, the other's at least <see cref="FartherBy"/> more.</summary>
        public static bool Quieter(float mine, float theirs) => mine >= 0f && theirs >= 0f && theirs - mine >= FartherBy;

        // ---------------- A grave for an animal ----------------

        /// <summary>The bonded animal died in the last two days.</summary>
        public const float AnimalDays = 2f;

        // ---------------- What the insects left ----------------

        /// <summary>Enough insect meat for this many simple meals or more is worth a question.</summary>
        public const int InsectMealsFrom = 8;

        public static bool InsectWorth(int meals) => meals >= InsectMealsFrom;

        /// <summary>Bills of a recipe that takes <paramref name="perBill"/> nutrition of the meat: as many as the meat stored makes, one at least, thirty at most.</summary>
        public static int Bills(float nutrition, float perBill) => perBill <= 0f ? 1 : Math.Max(1, Math.Min(30, (int)Math.Floor(nutrition / perBill)));

        // ---------------- The picking day ----------------

        /// <summary>At least this many wild food plants ripe within reach, for the day to be asked for.</summary>
        public const int PickingFrom = 6;

        /// <summary>Marked for picking with everyone out, and with a morning of it.</summary>
        public const int PickingAll = 24, PickingMorning = 12;

        // ---------------- The hungry gap ----------------

        /// <summary>Fewer days of food than this in the stores.</summary>
        public const float HungryDays = 4f;

        public static bool Hungry(float days) => days >= 0f && days < HungryDays;

        /// <summary>Old enough to eat first: sixty, as the colony counts its elders.</summary>
        public const float ElderAge = 60f;

        /// <summary>Who needs food most and eats first: a child, an elder, the ill.</summary>
        public static bool EatsFirst(bool child, float age, bool ill) => child || age >= ElderAge || ill;

        // ---------------- A friend among the visitors ----------------

        /// <summary>Each thinks this well of the other, or better.</summary>
        public const int FriendFrom = 15;

        // ---------------- The forgetting ----------------

        /// <summary>A skilled work is one they are this good at, or better.</summary>
        public const int SkilledFrom = 4;

        /// <summary>The one closest to them thinks this well of them, or better, to be asked first.</summary>
        public const int CloseFrom = 10;

        /// <summary>Work handed over for a season.</summary>
        public const float SeasonDays = 15f;

        // ---------------- The crying in the night ----------------

        /// <summary>The crying in the last day.</summary>
        public const float CryingDays = 1f;

        /// <summary>Tired: their rest under half.</summary>
        public const float TiredBelow = 0.5f;

        public static bool Tired(float rest) => rest >= 0f && rest < TiredBelow;

        /// <summary>The baby seen to by another for a few days.</summary>
        public const float ChildcareDays = 5f;

        // ---------------- After the tantrum ----------------

        /// <summary>The tantrum in the last two days.</summary>
        public const float TantrumDays = 2f;

        public static bool Lately(int now, int tick, float days) => tick >= 0 && now >= tick && now - tick <= days * 60000f;

        // ---------------- The scoffer's grave ----------------

        /// <summary>Died in the last six days.</summary>
        public const float ScofferDays = 6f;

        /// <summary>The friend who speaks for them thought this well of them, or better.</summary>
        public const int FondFrom = 20;

        // ---------------- The table without meat ----------------

        /// <summary>The meat eaten in the last two days.</summary>
        public const float MeatDays = 2f;

        /// <summary>Meals without meat for each of the faithful: two days of it.</summary>
        public const int MealsEach = 4;

        /// <summary>The meals the bill makes: <see cref="MealsEach"/> for each of the faithful, four at least, thirty at most.</summary>
        public static int MealsFor(int faithful) => Math.Max(4, Math.Min(30, faithful * MealsEach));
    }
}
