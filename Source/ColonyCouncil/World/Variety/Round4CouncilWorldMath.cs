using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the content round's fourth part, its council questions and world events (docs/content-round-4-council-world.md;
    /// Dilemmas/DilemmaWorkers_Round4.cs, World/Variety/WorldRound4.cs), without game types so the tests can check them: how much
    /// of a thing is worth a question, who a decree falls hard on, how far a visitor must be ahead, how cold a cell, how worn a
    /// slave, how much dry growth, what the world events ask and pay.
    /// </summary>
    public static class Round4CouncilWorldMath
    {
        private static int Tens(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        // ================= R4C-1. The stone by the walls =================

        /// <summary>At least this many stone chunks lying loose in the home area are worth a question.</summary>
        public const int StoneFrom = 30;

        public static bool StoneWorth(int chunks) => chunks >= StoneFrom;

        /// <summary>The blocks bill: one block-cutting for each chunk lying loose, forty at most, one at least.</summary>
        public static int BlockBills(int chunks) => Math.Max(1, Math.Min(40, chunks));

        // ================= R4C-2. The night owl =================

        /// <summary>The night owl's mood is under this share of the bar.</summary>
        public const float OwlMoodUnder = 0.5f;

        public static bool OwlLow(float mood) => mood >= 0f && mood < OwlMoodUnder;

        // ================= R4C-4. Too hard on one =================

        /// <summary>A decree of hard work: one that strains this much a day, or more.</summary>
        public const float HardStrainFrom = 2f;

        /// <summary>At least this many days of the decree left, for the question to be worth asking.</summary>
        public const float DecreeDaysFrom = 1f;

        /// <summary>The colony's elders, as it counts them: sixty or older.</summary>
        public const float ElderAge = 60f;

        /// <summary>Worn, as the heart's bands count it.</summary>
        public const float WornStrain = 55f;

        public static bool HardDecree(float strainPerDay, float daysLeft) => strainPerDay >= HardStrainFrom && daysLeft >= DecreeDaysFrom;

        /// <summary>Who a hard decree falls hard on: an elder, the ill, one troubled by an old injury, or one worn.</summary>
        public static bool FallsHard(float age, bool ill, bool oldInjury, float strain) => age >= ElderAge || ill || oldInjury || strain >= WornStrain;

        /// <summary>The work handed over while the decree lasts: its days left, rounded up, one at least and its longest at most.</summary>
        public static int HandOverDays(float daysLeft) => Math.Max(1, Math.Min(15, (int)Math.Ceiling(daysLeft)));

        // ================= R4C-5. The visiting master =================

        /// <summary>A visitor this good at a skill, or better.</summary>
        public const int MasterFrom = 10;

        /// <summary>And this many levels above the colony's best at it, or more.</summary>
        public const int MasterGap = 5;

        public static bool Master(int theirs, int oursBest) => theirs >= MasterFrom && theirs - Math.Max(0, oursBest) >= MasterGap;

        /// <summary>The visitor stays on this many hours for the lesson or the supper.</summary>
        public const float StayHours = 14f;

        // ================= R4C-6. The cold cell =================

        /// <summary>The cell is colder than this (degrees), with the prisoner already chilled.</summary>
        public const float ColdCellBelow = 10f;

        // ================= R4C-7. A rest for a slave =================

        public const float SlaveJoyUnder = 0.2f, SlaveMoodUnder = 0.35f, SlaveHeldFrom = 0.4f;

        /// <summary>The rest eases the slave's fear by this much of the bar (a twentieth).</summary>
        public const float SlaveEase = -0.05f;

        public static bool SlaveWorn(float joy, float mood, float suppression) => joy >= 0f && joy < SlaveJoyUnder && mood < SlaveMoodUnder && suppression >= SlaveHeldFrom;

        /// <summary>The one who asks for them thinks this well of them, or better.</summary>
        public const int SlaveFondFrom = 10;

        // ================= R4C-8. A bed for the animal =================

        /// <summary>An animal small enough for a bedroom: a dog's size, a little more.</summary>
        public const float PetBodyMax = 1.3f;

        // ================= R4C-9. The dry grass by the walls =================

        /// <summary>Hot enough outside (degrees) for the dry grass to be a danger.</summary>
        public const float DryHeatFrom = 25f;

        /// <summary>At least this many dry wild plants at the edge of the home ground.</summary>
        public const int DryGrassFrom = 30;

        /// <summary>How far out from the home ground's edge the firebreak reaches (cells).</summary>
        public const float FirebreakReach = 2f;

        /// <summary>At most this many plants marked for the firebreak.</summary>
        public const int FirebreakMark = 80;

        public static bool DryDanger(float outdoorTemp, bool raining, int dryPlants) => outdoorTemp >= DryHeatFrom && !raining && dryPlants >= DryGrassFrom;

        // ================= R4W-1. The warm springs =================

        /// <summary>Days at the springs (the road both ways besides).</summary>
        public const float SpringsDays = 3f;

        /// <summary>The ache eased to this share of what it was.</summary>
        public const float SpringsEase = 0.3f;

        /// <summary>The strain eased by the days there.</summary>
        public const float SpringsRelief = 20f;

        /// <summary>The ailments the warm water eases: aches and stiffness, never a sickness (Rimfolk's own ailments).</summary>
        public static readonly string[] Aches =
        {
            "Council_Ail_StrainedBack", "Council_Ail_StiffNeck", "Council_Ail_SoreHands", "Council_Ail_Chilblains", "Council_Ail_Limp", "Council_Ail_SprainedAnkle"
        };

        /// <summary>Who is sent: one with an ache first; else one worn (the heart's band) is; else the eldest, from this age.</summary>
        public static bool SpringsFor(bool aching, float strain) => aching || strain >= WornStrain;

        /// <summary>Old enough for warm water to do them good, when nobody aches or is worn.</summary>
        public const float SpringsOldFrom = 50f;

        // ================= R4W-3. The timber rafts =================

        /// <summary>How many logs their foreman sells the colony.</summary>
        public const int RaftLogs = 100;

        /// <summary>The logs they sell are a third of their worth, in tens, ten at least.</summary>
        public static int RaftPrice(int logs, float valueEach) => logs <= 0 ? 0 : Math.Max(10, Tens(logs * valueEach / 3f));

        /// <summary>A raft broken on the rocks: this many logs wash up on the colony's bank, by a roll (fifteen to thirty-five).</summary>
        public static int DriftLogs(float roll) => 15 + Math.Max(0, Math.Min(20, (int)(roll * 21f)));

        /// <summary>A raft breaks up at this chance (settled when it ends).</summary>
        public const float BreakChance = 0.35f;

        // ================= R4W-5. The arms sold off =================

        /// <summary>How many of the weapons the colony may buy at once.</summary>
        public const int ArmsCount = 3;

        /// <summary>A weapon of a fair price for the cart: worth this much, at least and at most (silver).</summary>
        public const float ArmsValueFrom = 40f, ArmsValueTo = 900f;

        public static bool ArmsFair(float value) => value >= ArmsValueFrom && value <= ArmsValueTo;

        /// <summary>Their price: half the worth of the weapons, in tens, thirty at least.</summary>
        public static int ArmsPrice(float valueEach, int count) => Math.Max(30, Tens(valueEach * Math.Max(1, count) * 0.5f));

        /// <summary>The tech level of a people's age tag, for its weapons: tribal Neolithic, medieval Medieval, industrial Industrial, spacer Spacer.</summary>
        public static int ArmsTech(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return 2;
                case "medieval": return 3;
                case "spacer": return 5;
                default: return 4;
            }
        }

        // ================= R4W-6. The missing caravan =================

        /// <summary>The caravan is found at this chance (settled at the start, out of sight).</summary>
        public const float FoundChance = 0.55f;

        public static bool Found(float roll) => roll < FoundChance;
    }
}
