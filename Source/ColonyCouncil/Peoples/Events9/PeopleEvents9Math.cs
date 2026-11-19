using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 9 (docs/peoples-events-proposals.md, items 84 to 94; Peoples/Events9/): the
    /// Lattice's link for a mechanitor of the colony's, its mech gone feral and its mech yard; the Choir's hum deepened, the void that
    /// listens back, the mind that shouts and its listening ring; the Sunken Vault's broken seal, its last part, its console that talks
    /// to nobody and its surface scouts. Shared with the tests (Tools/Tests/PeopleEvents9Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents9Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        private static float Wealth(float wealthFactor) => float.IsNaN(wealthFactor) || float.IsInfinity(wealthFactor) ? 1f : wealthFactor;

        // ================= 84. Linked into the Lattice =================

        /// <summary>The link put in where the mechanitor stands: two hours with the mech-tender's array.</summary>
        public const int LinkTicks = 2 * HourTicks;

        /// <summary>The bandwidth the Lattice lends: the whole link two, a link for work only one.</summary>
        public static int Bandwidth(bool whole) => whole ? 2 : 1;

        /// <summary>The Lattice's thanks for a mind linked in (more for the whole link, which they hold dear).</summary>
        public const int LinkGoodwill = 3, WorkLinkGoodwill = 2;

        /// <summary>A linked mind dreams another's dream on about one night in seven; half of them good, half not.</summary>
        public const float DreamChance = 0.15f, GoodDreamShare = 0.5f;

        /// <summary>The dream is told at dawn, when they wake from it.</summary>
        public static bool DreamHour(int hour) => hour == 6;

        /// <summary>A night's dream: 1 a good one, -1 a bad one, 0 none, from two rolls in 0..1.</summary>
        public static int Dream(float roll, float kind) => float.IsNaN(roll) || roll >= DreamChance ? 0 : kind < GoodDreamShare ? 1 : -1;

        // ================= 85. A mech gone feral =================

        /// <summary>The feral mech is no stronger than this much of the home's threat points (the weakest kind when none is).</summary>
        public const float FeralPoints = 0.35f;

        /// <summary>The Lattice's light war mechs that may slip their link: Biotech's, and Alpha Mechs' of the same weight.</summary>
        public static readonly string[] FeralKinds = { "Mech_Militor", "Mech_Scorcher", "Mech_Legionary", "AM_Mech_Bellicor", "AM_Daggersnout", "AM_Blitzkrieg" };

        /// <summary>The kind fit for these points: the strongest of those within them, else the weakest (-1 for none).</summary>
        public static int FeralPick(float[] powers, float points)
        {
            if (powers == null || powers.Length == 0) return -1;
            int best = -1, weakest = -1;
            for (int i = 0; i < powers.Length; i++)
            {
                if (float.IsNaN(powers[i]) || powers[i] <= 0f) continue;
                if (weakest < 0 || powers[i] < powers[weakest]) weakest = i;
                if (powers[i] <= points && (best < 0 || powers[i] > powers[best])) best = i;
            }
            return best >= 0 ? best : weakest;
        }

        /// <summary>The link-wardens' pay for the trouble: a tenth of the mech's worth, to the nearest ten, 60 to 300.</summary>
        public static int FeralPay(float mechWorth) => Clamp(Round10((float.IsNaN(mechWorth) || float.IsInfinity(mechWorth) ? 0f : Math.Max(0f, mechWorth)) * 0.1f), 60, 300);

        /// <summary>The Lattice's thanks: relinked +4; taken down together, nothing either way (better destroyed than feral).</summary>
        public const int RelinkGoodwill = 4, DownGoodwill = 0;

        /// <summary>Left to itself, it roams near where it came in this long, then wanders off the map.</summary>
        public const int FeralRoamHours = 36;

        /// <summary>The relink at the gate: half an hour, sparks and a bar.</summary>
        public const int RelinkTicks = 1250;

        /// <summary>How often it comes when the event is drawn: the drovers' bull's kind in the shared harm budget, at this chance.</summary>
        public const float FeralChance = 0.8f;

        // ================= 86. A mech yard =================

        /// <summary>A mech-tender and two link-hands, four to six mechs, five to seven days.</summary>
        public const int YardPeople = 3, YardMechsMin = 4, YardMechsMax = 6, YardMinDays = 5, YardMaxDays = 7;

        /// <summary>Their labour mechs: the constructoids cut plain rock, the lifters haul it.</summary>
        public static readonly string[] YardMiners = { "Mech_Constructoid" }, YardHaulers = { "Mech_Lifter" };

        /// <summary>How many of the mechs cut rock: half, at least one.</summary>
        public static int Miners(int mechs) => mechs <= 0 ? 0 : Math.Max(1, mechs / 2);

        /// <summary>Rock cut a day by the yard, at most, and an hour's cutting a cell.</summary>
        public const int RockPerDay = 6, RockTicks = HourTicks;

        /// <summary>The mechs at work by day.</summary>
        public static bool YardHour(int hour) => hour >= 7 && hour <= 18;

        /// <summary>The mechs hired for a day: a day long; a mend a building half an hour.</summary>
        public const int HireHours = 24, MendTicks = 1250;

        /// <summary>A building of the colony's worth mending: below nine tenths of its strength.</summary>
        public static bool WantsMending(int hp, int max) => max > 0 && hp < max * 0.9f;

        /// <summary>Once a stay each, by day: a mech of theirs goes feral (the drovers' bull's kind), a band of a people at war with them comes.</summary>
        public const float YardFeralChance = 0.25f, YardBandChance = 0.3f;

        /// <summary>The Lattice's thanks for a stay that went well.</summary>
        public const int YardGoodwill = 3;

        // ================= 87. The hum deepened =================

        /// <summary>The cantor sits through the night: five hours at the colonist's bed.</summary>
        public const int HumTicks = 5 * HourTicks;

        /// <summary>The cantor comes in the evening, to sit through the night.</summary>
        public static bool HumHour(int hour) => hour >= 16 && hour <= 21;

        /// <summary>A colonist without a psylink wakes a first level only when this sensitive (the psychically sensitive trait or more).</summary>
        public const float SensitiveFrom = 1.3f;

        /// <summary>One who can take the hum: a psylink below six levels, or none and sensitive enough.</summary>
        public static bool HumTaker(int psylink, float sensitivity) =>
            psylink > 0 ? psylink < 6 : !float.IsNaN(sensitivity) && sensitivity >= SensitiveFrom;

        /// <summary>The art they would take: the colony's dearest piece, worth this much at least.</summary>
        public const float HumArtMinWorth = 200f;

        public const int HumGoodwill = 3;

        // ================= 88. The void listens back =================

        /// <summary>The warden of the quiet stays at their side through the day; the warning alone is an hour at the colony's edge.</summary>
        public const int VoidSitTicks = 8 * HourTicks, VoidListenTicks = HourTicks;

        /// <summary>Shaken by the void: their sanity (Vanilla Anomaly Expanded - Insanity) below half, or a memory of the void on them.</summary>
        public static bool Shaken(float sanity, bool voidMemory) => voidMemory || !float.IsNaN(sanity) && sanity >= 0f && sanity < 0.5f;

        /// <summary>What a day with the warden gives back of their sanity, of its whole.</summary>
        public const float SanityBack = 0.3f;

        public static float SanityAfter(float sanity, float max) => float.IsNaN(sanity) ? sanity : Math.Min(Math.Max(0f, max), Math.Max(0f, sanity) + SanityBack * Math.Max(0f, max));

        /// <summary>The warden's word holds for the void's next threat within this many days.</summary>
        public const int ForetoldDays = 30;

        public const int VoidGoodwill = 3;

        // ================= 89. A mind that shouts =================

        /// <summary>A long break: one that has lasted three hours, not one that turns on others.</summary>
        public const int ShoutAfterTicks = 3 * HourTicks;

        public static bool LongBreak(int ageTicks, bool aggro) => !aggro && ageTicks >= ShoutAfterTicks;

        /// <summary>The listener sits beside them two hours, and the break ends.</summary>
        public const int ShoutSitTicks = 2 * HourTicks;

        /// <summary>Once a break, at a chance, not soon after the last.</summary>
        public const float ShoutChance = 0.5f;

        public const int ShoutGapDays = 20;

        /// <summary>What the colonist who sat with them learns of the Choir's way.</summary>
        public const float ShoutSocialXp = 1500f;

        public const int ShoutGoodwill = 2;

        /// <summary>The breaks rolled for, kept.</summary>
        public const int MaxShoutsRolled = 8;

        // ================= 90. A listening ring =================

        public const int RingMin = 3, RingMax = 5, RingMinDays = 3, RingMaxDays = 5;

        /// <summary>They sing at dusk; at home it is felt late in the evening.</summary>
        public static bool SingHour(int hour) => hour == 19;

        public static bool FeltHour(int hour) => hour == 22;

        /// <summary>Loud work at night within this many cells of the ring is heard there.</summary>
        public const float LoudRadius = 25f;

        public static bool NightHour(int hour) => hour >= 22 || hour < 6;

        /// <summary>A night's loud work: the ring's tension, and the colony that promised quiet thanked less.</summary>
        public const float LoudTension = 3f;

        /// <summary>A psycaster's evening with them: two hours, and their focus full.</summary>
        public const int RingSitTicks = 2 * HourTicks;

        /// <summary>With the void stirring, their song turns strange once a stay at this chance, and they leave.</summary>
        public const float StrangeChance = 0.35f;

        public const int RingGoodwill = 3;

        // ================= 91. The seal broken =================

        /// <summary>The vault's town within this many tiles of the home.</summary>
        public const float SealTiles = 30f;

        /// <summary>Days to set out; and the call waits while a caravan is on the road.</summary>
        public const int SealDays = 5;

        /// <summary>What came up from the lower level: insects with this chance (Insectoids 2's hives among them), else mechs.</summary>
        public const float InsectChance = 0.6f;

        /// <summary>The fight's points: of the home's threat points; the vault's security beside the colony's, four at most.</summary>
        public const float SealPoints = 0.6f, SecurityPoints = 0.3f;

        public const int SecurityMax = 4;

        /// <summary>The colony's share of the vault's stores, and its thanks.</summary>
        public const int SealGoodwill = 15, SealComponents = 2;

        /// <summary>Or the colony sends its security components and this much medicine instead, and the vault's thanks for it.</summary>
        public const int SealMedicine = 5, SuppliesGoodwill = 3;

        /// <summary>The fight is won when none of what came up is left on its feet while some of the colony's still are.</summary>
        public static bool Won(int enemiesStanding, int oursStanding) => enemiesStanding == 0 && oursStanding > 0;

        // ================= 92. The last part =================

        /// <summary>The missing parts the vault's last bionics are for, and their install recipes.</summary>
        public static string PartKind(string bodyPart)
        {
            switch (bodyPart)
            {
                case "Eye": return "eye";
                case "Leg":
                case "Foot": return "leg";
                case "Hand":
                case "Arm":
                case "Shoulder": return "arm";
            }
            return null;
        }

        public static string BionicRecipe(string kind)
        {
            switch (kind)
            {
                case "eye": return "InstallBionicEye";
                case "leg": return "InstallBionicLeg";
                case "arm": return "InstallBionicArm";
            }
            return null;
        }

        /// <summary>A large supply of components and food: 15 to 30 components and 200 to 500 raw food, by the colony's wealth.</summary>
        public static int PartComponents(float wealthFactor) => (int)Math.Round(Clamp(20f * Wealth(wealthFactor), 15f, 30f));

        public static int PartFood(float wealthFactor) => Round10(Clamp(300f * Wealth(wealthFactor), 200f, 500f));

        /// <summary>
        /// The supply holds the vault's age a year: a memory of this weight stays above a year's full supply (PeopleAgesMath.SupplyFull,
        /// at PeopleAgesMath.GiftSilverPerWeight a point) for a year (MemoryMath's half-life).
        /// </summary>
        public const int YearOfSupplyWeight = 30;

        public const int PartGoodwill = 5;

        // ================= 93. The console that talks to nobody =================

        /// <summary>Three evenings, an hour's talk each, at dusk.</summary>
        public const int ConsoleEvenings = 3, ConsoleTalkTicks = HourTicks;

        public static bool ConsoleHour(int hour) => hour >= 18 && hour <= 21;

        /// <summary>How the talk ends: the overseer's ask (components or medicine) or an old supply cache, half and half.</summary>
        public static bool TellsOfCache(float roll) => !float.IsNaN(roll) && roll < 0.5f;

        /// <summary>The ask: components or medicine by the colony's wealth.</summary>
        public static int AskComponents(float wealthFactor) => (int)Math.Round(Clamp(6f * Wealth(wealthFactor), 4f, 10f));

        public static int AskMedicine(float wealthFactor) => (int)Math.Round(Clamp(5f * Wealth(wealthFactor), 3f, 8f));

        /// <summary>The cache a few tiles off, kept on the world map this long.</summary>
        public const int CacheTilesMin = 3, CacheTilesMax = 9, CacheDays = 25;

        /// <summary>What the talker learns of talking, and the vault's thanks: the whole talk, one evening.</summary>
        public const float ConsoleSocialXp = 400f;

        public const int ConsoleGoodwill = 4, OneEveningGoodwill = 1, AskGoodwill = 5;

        // ================= 94. Surface scouts =================

        public const int ScoutPeople = 3, ScoutDays = 7;

        /// <summary>The food the colony may send with them from the gate.</summary>
        public const int ScoutFood = 80;

        /// <summary>They ask to be taught in the afternoon; two hours' teaching.</summary>
        public static bool LessonHour(int hour) => hour >= 12 && hour <= 15;

        public const int LessonTicks = 2 * HourTicks;

        /// <summary>The afternoon's subject: plants, animals or shooting, in turn.</summary>
        public static readonly string[] Lessons = { "Plants", "Animals", "Shooting" };

        public static string LessonOn(int day) => Lessons[((day % Lessons.Length) + Lessons.Length) % Lessons.Length];

        /// <summary>What each scout learns, and the vault remembers, of an afternoon's teaching.</summary>
        public const float LessonXp = 1500f, TeacherXp = 250f, LessonWorth = 150f;

        /// <summary>Their last odds and ends: one to three of the vault's goods, at their worth.</summary>
        public const int OddsMax = 3;

        public static int OddsPrice(float worth) => Math.Max(10, Round10(Math.Max(0f, float.IsNaN(worth) ? 0f : worth)));

        /// <summary>Once a stay each: one sky-sick in the shelter, one lost at night, one mauled by a beast they didn't know to fear (the wild one too close).</summary>
        public const float SkySickChance = 0.4f, LostChance = 0.3f, MaulChance = 0.25f;

        public const int ScoutsGoodwill = 3;
    }
}
