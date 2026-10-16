namespace Balance
{
    /// <summary>
    /// The one table of numbers the simulator can't read from the defs or the code: how often the game itself does
    /// things (the storyteller, visitors, raids), and how a player of each kind plays. Every number here is an estimate
    /// and says where it comes from. Rates read from Rimfolk's own code are in <see cref="Code"/> (Program.cs reads
    /// them from the source); the defs give every mood's size and length.
    /// </summary>
    public static class Rates
    {
        // ---------------- The game around Rimfolk ----------------

        /// <summary>The storyteller's mean days between two everyday (Misc) events at home: Cassandra and Phoebe, 4.8.</summary>
        public const float StorytellerMiscMtbDays = 4.8f;

        /// <summary>
        /// The base game's own Misc events, as weight (traders, travelers, wanderers, a ship chunk, the DLCs' visitors):
        /// about 20 in a game with the DLCs. Rimfolk's events share the pool up to <see cref="Code.EventMaxShare"/>.
        /// </summary>
        public const float VanillaMiscWeight = 20f;

        /// <summary>Days between big threats (raids, sieges, infestations) in a settled colony on Cassandra, middle difficulty.</summary>
        public const float BigThreatDays = 9f;

        /// <summary>Share of hours a kind of stranger is at home: visitors, traders, a lodger, a prisoner, the downed after a fight.</summary>
        public static readonly Dictionary<string, float> Present = new Dictionary<string, float>
        {
            ["Visitor"] = 0.12f, ["Trader"] = 0.08f, ["Lodger"] = 0.03f, ["Prisoner"] = 0.2f, ["Fallen"] = 0.02f, ["Pet"] = 0.85f, ["Wild"] = 0.95f
        };

        /// <summary>Share of picked events that can be cast (each event's worker has its own conditions the defs don't show).</summary>
        public const float Castable = 0.6f;

        /// <summary>Share of picked dilemmas, encounters and social events whose worker's own checks pass, on top of the conditions they name (Gates).</summary>
        public const float WorkerCasts = 0.8f;

        /// <summary>Share of colonists with any one trait a filter names (onlyTrait / exceptTrait), and with a degree trait in range.</summary>
        public const float TraitShare = 0.07f;
        public const float DegreeTraitShare = 0.3f;

        /// <summary>
        /// A colonist's social interactions started per waking hour: the game's own rhythm is one every 6,600 ticks (2.6 hours)
        /// at work and one every 550 in company (Pawn_InteractionsTracker), so about 0.6 an hour over a day.
        /// </summary>
        public const float InteractionsPerHour = 0.6f;

        /// <summary>The chitchat that moments compete with (the game's own weights near 1).</summary>
        public const float VanillaInteractionWeight = 1f;

        /// <summary>Waking hours in a colonist's day.</summary>
        public const int WakingFrom = 6, WakingTo = 22;

        /// <summary>Days on a settled colony's calendar (founding, harvest, snow, battles, deaths, weddings, named days).</summary>
        public static int CalendarDays(int colonists) => colonists >= 10 ? 11 : 8;

        /// <summary>How long a personal story runs, in days, and the share that end well (the rest fade without a letter).</summary>
        public const float StoryDaysMin = 8f, StoryDaysMax = 20f, StoryEndsWell = 0.75f;

        /// <summary>A camp's letters per day while it's here (help, terms, a fee, someone asking to join), and the share that ask something.</summary>
        public const float CampLettersPerDay = 0.35f, CampQuestionShare = 0.5f;

        /// <summary>Share of a world event's beats that come (each has its own conditions).</summary>
        public const float SituationBeatShare = 0.7f;

        /// <summary>An ambition's run, in days, from the meeting that chose it.</summary>
        public const float AmbitionDaysMin = 8f, AmbitionDaysMax = 18f;

        /// <summary>Proposals at a meeting (one is chosen), and the share of voters who backed another.</summary>
        public const int MeetingProposals = 3;
        public const float OutVotedShare = 0.3f;

        /// <summary>A decree request's chance to find something to ask for, on a day it's allowed to try.</summary>
        public const float DecreeRequestFits = 0.5f;

        /// <summary>Share of days a lasting discomfort is felt (the thought that triggers an event wish is there that day).</summary>
        public const float DiscomfortDays = 0.7f;

        /// <summary>
        /// How often each kind of condition a def can name holds, as a factor on how often the def can be cast (DefSet.Gate).
        /// Ties and passions are common, memories of a death and illnesses rare, a camp or a world event rarer.
        /// </summary>
        public static readonly Dictionary<string, float> Gates = new Dictionary<string, float>
        {
            ["tie"] = 0.6f, ["mutualFriends"] = 0.5f, ["sharedPassion"] = 0.4f, ["roleA"] = 0.75f, ["roleB"] = 0.8f, ["colonist"] = 0.75f, ["dreamer"] = 0.75f,
            ["initiatorMemories"] = 0.15f, ["recipientMemories"] = 0.15f, ["aMemories"] = 0.15f, ["bMemories"] = 0.15f, ["afterThoughts"] = 0.2f,
            ["afterThreat"] = 0.15f, ["aFeelings"] = 0.35f, ["bFeelings"] = 0.35f, ["feelings"] = 0.35f, ["aFeelingAboutB"] = 0.25f,
            ["aAiling"] = 0.2f, ["bAiling"] = 0.2f, ["bSerious"] = 0.02f, ["sharedAilment"] = 0.05f, ["hediffs"] = 0.08f, ["unconscious"] = 0.03f,
            ["needsGrief"] = 0.12f, ["need"] = 0.35f, ["needsDefs"] = 0.5f, ["camp"] = 0.05f, ["situations"] = 0.1f, ["colonyIs"] = 0.4f,
            ["colonyIsNot"] = 0.85f, ["colonyHas"] = 0.5f, ["lands"] = 0.6f, ["biomes"] = 0.4f, ["sharedVendetta"] = 0.05f, ["bKeepsSecretOfA"] = 0.1f,
            ["aHasQuirk"] = 0.5f, ["aHasGoal"] = 0.4f, ["secrets"] = 0.2f, ["ways"] = 0.35f, ["sameIdeo"] = 0.8f, ["otherIdeo"] = 0.25f,
            ["bondedPet"] = 0.3f, ["nemesisVictim"] = 0.03f, ["recipientBestCook"] = 0.5f, ["moreSkilled"] = 0.7f, ["initiatorTags"] = 0.4f,
            ["recipientTags"] = 0.75f, ["stillSore"] = 0.15f, ["moment"] = 0.5f, ["nightOnly"] = 0.3f, ["petOfA"] = 0.7f, ["busyTime"] = 0.6f,
            ["minMedicine"] = 0.8f, ["maxMedicine"] = 0.4f, ["minMood"] = 0.7f, ["needsMechanoids"] = 0.1f
        };

        /// <summary>The same for who the stranger in an encounter or a stranger's question must be.</summary>
        public static readonly Dictionary<string, float> StrangerGates = new Dictionary<string, float>
        {
            ["ofA"] = 0.7f, ["bondedToA"] = 0.3f, ["nemesis"] = 0.05f, ["nemesisOfA"] = 0.03f, ["majestic"] = 0.15f, ["named"] = 0.4f, ["predator"] = 0.4f,
            ["royal"] = 0.1f, ["child"] = 0.2f, ["anyTrait"] = 0.3f
        };

        /// <summary>
        /// How often a wish can be voiced at all (its worker looks for a reason: a prisoner to recruit, lovers to marry, a
        /// lost limb, a camp on the map, a child). Wishes about the home and the work can come any time (1).
        /// </summary>
        public static float WishGate(string defName, string worker)
        {
            switch (defName)
            {
                case "Council_Wish_Crib": return 0.1f;
                case "Council_Wish_Sarcophagus":
                case "Council_Wish_Biosculpter": return 0.3f;
            }
            switch (worker)
            {
                case "WishWorker_Rescue": return 0.02f;
                case "WishWorker_Quit":
                case "WishWorker_Prosthetic":
                case "WishWorker_RoyalStandards":
                case "WishWorker_CampEvening": return 0.05f;
                case "WishWorker_HurtAnimal":
                case "WishWorker_Wedding":
                case "WishWorker_Memorial":
                case "WishWorker_Beast":
                case "WishWorker_TraderGoods": return 0.1f;
                case "WishWorker_Birthday":
                case "WishWorker_ShareBed":
                case "WishWorker_Justice":
                case "WishWorker_Mercy":
                case "WishWorker_Separate": return 0.15f;
                case "WishWorker_Sickbed":
                case "WishWorker_SickbedVisit":
                case "WishWorker_ChildPet":
                case "WishWorker_ChildPlay": return 0.2f;
                case "WishWorker_Recruit": return 0.25f;
                case "WishWorker_Outside": return 0.3f;
                case "WishWorker_Apprentice":
                case "WishWorker_Fix": return 0.4f;
                case "WishWorker_Meeting":
                case "WishWorker_Companion":
                case "WishWorker_Travel":
                case "WishWorker_Hunt": return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>Names (epithets) given in a year, and the share of others who are a given colonist's rival.</summary>
        public const float EpithetsPerYear = 3f, RivalShare = 0.12f;

        /// <summary>Close friends per colonist (opinion 40+ or a friendship tie), for friends minding a lapsed wish or an illness.</summary>
        public const float FriendsPerColonist = 1.3f;

        /// <summary>Notable share of encounters and social events (the rest are flavour, which the default message level leaves out).</summary>
        public const float NotableShare = 0.25f;

        // ---------------- Ailments: a colonist's day ----------------

        /// <summary>What colonists do all day, and how many of them: the hours that bring ailments on.</summary>
        public static readonly (string role, float share)[] Roles =
        {
            ("Builder", 0.25f), ("Grower", 0.2f), ("Crafter", 0.17f), ("Cook", 0.1f), ("Researcher", 0.1f), ("Hunter", 0.1f), ("Doctor", 0.08f)
        };

        /// <summary>
        /// The share of days a colonist of this role reaches the cause's hours (before the season and the play style).
        /// Triggers not listed for a role come from <see cref="Home"/> (where they sleep and eat) or the season.
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, float>> RoleDays = new Dictionary<string, Dictionary<string, float>>
        {
            ["Builder"] = new Dictionary<string, float> { ["HeavyWork"] = 0.7f, ["Chopping"] = 0.15f, ["Walking"] = 0.1f, ["ColdWork"] = 0.7f, ["SunWork"] = 0.5f, ["AncientWork"] = 0.01f },
            ["Grower"] = new Dictionary<string, float> { ["PlantWork"] = 0.8f, ["Chopping"] = 0.3f, ["SunWork"] = 0.7f, ["ColdWork"] = 0.5f, ["HeavyWork"] = 0.1f },
            ["Crafter"] = new Dictionary<string, float> { ["HandWork"] = 0.75f, ["HeavyWork"] = 0.05f },
            ["Cook"] = new Dictionary<string, float> { ["HandWork"] = 0.1f },
            ["Researcher"] = new Dictionary<string, float> { ["HandWork"] = 0.05f },
            ["Hunter"] = new Dictionary<string, float> { ["Walking"] = 0.5f, ["RoughGround"] = 0.35f, ["ColdWork"] = 0.6f, ["SunWork"] = 0.3f, ["WetFeet"] = 0.15f },
            ["Doctor"] = new Dictionary<string, float> { ["HeavyWork"] = 0.2f }
        };

        /// <summary>Outdoor work needs the season: cold work in autumn and winter, sun in summer, plants above 10 °C.</summary>
        public static float Season(string trigger, int quadrum)
        {
            // Quadrums of a temperate map: 0 spring, 1 summer, 2 autumn, 3 winter.
            switch (trigger)
            {
                case "ColdWork": return quadrum == 3 ? 0.8f : quadrum == 2 ? 0.35f : quadrum == 0 ? 0.2f : 0f;
                case "SunWork": return quadrum == 1 ? 0.7f : quadrum == 0 ? 0.15f : quadrum == 2 ? 0.1f : 0f;
                case "PlantWork": return quadrum == 3 ? 0.05f : quadrum == 2 ? 0.6f : 1f;
                case "BitterCold": return quadrum == 3 ? 0.2f : 0f;
                case "Sweltering": return quadrum == 1 ? 0.12f : 0f;
                case "RoughGround": return quadrum == 3 ? 1.3f : 1f;
                default: return 1f;
            }
        }

        // ---------------- How the player plays ----------------

        public sealed class Style
        {
            public string name;
            public string key;
            /// <summary>The game's own moods (rooms, meals, recreation, comfort, relations): good and bad, before Rimfolk.</summary>
            public float vanillaGood, vanillaBad, vanillaSpread;

            // Wishes
            /// <summary>Share of wishes granted, and when (share of the wish's time); promises made, and the share kept.</summary>
            public float grant, grantFrom, grantTo, promise, promiseKept;
            /// <summary>Lasting discomforts per colonist that make them ask (barracks, eating off the floor, the dark): see DiscomfortDays.</summary>
            public float discomforts;

            // Answers (dilemmas, events, world events, camps)
            /// <summary>Share of answers that are the best for the colony's mood and trust, and that fall to the default (no answer).</summary>
            public float best, byDefault;
            /// <summary>Share of calendar days let pass.</summary>
            public float calendarPass;
            /// <summary>Share of ambitions reached.</summary>
            public float ambitionDone;

            // Ailments
            public float tendShare, tendQuality, bedShare;
            /// <summary>Share of days that hold each home cause: a hard bed, damp, a dirty crowded room, smoke, the dark, hunger, late nights.</summary>
            public Dictionary<string, float> home;

            // Pressures: the share of colonists at each stage on a given day (stage 0 = none).
            public float[] sameMeal, sameWork, crowded, unfinished, missing;
            public float variedTable;

            // Setbacks: a factor on the table's per-day rates, and the situational ones' share of colonists.
            public float setbackFactor, quietDays, researchIdle, bedroomEnvy, worries;

            // Small irritations
            public float nuisanceFits, frictionFactor;

            // Steady moods: the colony's ways (share at -4, -2, +2, +4), strain (share at -3, -5, -8), others.
            public float[] ways, strain;
            public float aspirationsPerYear, convalescencePerYear;
            public float pets;
            /// <summary>Share of personal touches built; colonists proud of the colony's trait; carrying an heirloom.</summary>
            public float touchBuilt, traitPride, heirloom;
            /// <summary>Share of pairs who share a bedroom (couples, and barracks).</summary>
            public float sharedBedrooms;
        }

        public static readonly Style WellRun = new Style
        {
            name = "well run", key = "well",
            vanillaGood = 34f, vanillaBad = -8f, vanillaSpread = 6f,
            grant = 0.8f, grantFrom = 0.1f, grantTo = 0.55f, promise = 0.2f, promiseKept = 0.95f, discomforts = 0.1f,
            best = 0.7f, byDefault = 0.05f, calendarPass = 0.05f, ambitionDone = 0.75f,
            tendShare = 0.85f, tendQuality = 0.85f, bedShare = 0.5f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0f, ["DampSleep"] = 0f, ["CrowdedDirty"] = 0f, ["SmokyRoom"] = 0f, ["DarkWork"] = 0.02f, ["Hungry"] = 0.01f, ["NightWork"] = 0.08f,
                ["Firefighting"] = 0.004f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f
            },
            sameMeal = new[] { 0.88f, 0.12f, 0f, 0f }, sameWork = new[] { 0.82f, 0.13f, 0.05f, 0f }, crowded = new[] { 0.97f, 0.03f, 0f },
            unfinished = new[] { 0.9f, 0.1f, 0f, 0f }, missing = new[] { 0.95f, 0.03f, 0.02f, 0f }, variedTable = 0.55f,
            setbackFactor = 0.6f, quietDays = 0f, researchIdle = 0f, bedroomEnvy = 0.05f, worries = 0.01f,
            nuisanceFits = 0.12f, frictionFactor = 0.7f,
            ways = new[] { 0f, 0.08f, 0.35f, 0.1f }, strain = new[] { 0.02f, 0f, 0f },
            aspirationsPerYear = 0.5f, convalescencePerYear = 0.4f, pets = 0.9f, touchBuilt = 0.9f, traitPride = 0.15f, heirloom = 0.05f, sharedBedrooms = 0.15f
        };

        public static readonly Style Average = new Style
        {
            name = "average", key = "avg",
            vanillaGood = 26f, vanillaBad = -13f, vanillaSpread = 7f,
            grant = 0.5f, grantFrom = 0.2f, grantTo = 0.85f, promise = 0.12f, promiseKept = 0.7f, discomforts = 0.5f,
            best = 0.35f, byDefault = 0.2f, calendarPass = 0.15f, ambitionDone = 0.5f,
            tendShare = 0.55f, tendQuality = 0.65f, bedShare = 0.3f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0.1f, ["DampSleep"] = 0.05f, ["CrowdedDirty"] = 0.05f, ["SmokyRoom"] = 0.1f, ["DarkWork"] = 0.1f, ["Hungry"] = 0.05f, ["NightWork"] = 0.12f,
                ["Firefighting"] = 0.006f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f
            },
            sameMeal = new[] { 0.55f, 0.32f, 0.13f, 0f }, sameWork = new[] { 0.6f, 0.25f, 0.1f, 0.05f }, crowded = new[] { 0.85f, 0.12f, 0.03f },
            unfinished = new[] { 0.7f, 0.2f, 0.1f, 0f }, missing = new[] { 0.94f, 0.04f, 0.02f, 0f }, variedTable = 0.2f,
            setbackFactor = 1f, quietDays = 0.15f, researchIdle = 0.05f, bedroomEnvy = 0.15f, worries = 0.02f,
            nuisanceFits = 0.4f, frictionFactor = 1f,
            ways = new[] { 0.02f, 0.13f, 0.2f, 0.04f }, strain = new[] { 0.08f, 0.02f, 0f },
            aspirationsPerYear = 0.3f, convalescencePerYear = 0.6f, pets = 0.8f, touchBuilt = 0.6f, traitPride = 0.1f, heirloom = 0.05f, sharedBedrooms = 0.3f
        };

        public static readonly Style Neglectful = new Style
        {
            name = "neglectful", key = "neglect",
            vanillaGood = 18f, vanillaBad = -22f, vanillaSpread = 8f,
            grant = 0.12f, grantFrom = 0.4f, grantTo = 1f, promise = 0.05f, promiseKept = 0.3f, discomforts = 1.8f,
            best = 0.1f, byDefault = 0.65f, calendarPass = 0.1f, ambitionDone = 0.15f,
            tendShare = 0.2f, tendQuality = 0.5f, bedShare = 0.1f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0.6f, ["DampSleep"] = 0.3f, ["CrowdedDirty"] = 0.4f, ["SmokyRoom"] = 0.4f, ["DarkWork"] = 0.4f, ["Hungry"] = 0.25f, ["NightWork"] = 0.2f,
                ["Firefighting"] = 0.01f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f
            },
            sameMeal = new[] { 0.35f, 0.45f, 0.2f, 0f }, sameWork = new[] { 0.4f, 0.3f, 0.2f, 0.1f }, crowded = new[] { 0.6f, 0.3f, 0.1f },
            unfinished = new[] { 0.4f, 0.3f, 0.2f, 0.1f }, missing = new[] { 0.93f, 0.04f, 0.03f, 0f }, variedTable = 0.02f,
            setbackFactor = 1.6f, quietDays = 0.6f, researchIdle = 0.15f, bedroomEnvy = 0.25f, worries = 0.04f,
            nuisanceFits = 0.75f, frictionFactor = 1.5f,
            ways = new[] { 0.05f, 0.2f, 0.1f, 0f }, strain = new[] { 0.2f, 0.08f, 0.02f },
            aspirationsPerYear = 0.1f, convalescencePerYear = 0.9f, pets = 0.5f, touchBuilt = 0.2f, traitPride = 0.03f, heirloom = 0.05f, sharedBedrooms = 0.75f
        };

        public static readonly Style[] Styles = { WellRun, Average, Neglectful };

        // ---------------- Setbacks that come from what happens in play ----------------

        /// <summary>
        /// Per colonist per day, at the average play style's rate (the style's setbackFactor scales it, and the role
        /// listed, if any, takes it alone). The council's own setbacks (a lapsed wish, an idea passed over, being out-voted,
        /// not agreeing with an answer) come from the simulated wishes, meetings and answers instead.
        /// </summary>
        public static readonly (string thought, int stage, float perDay, string role)[] Setbacks =
        {
            ("Council_Setback_MissedGathering", 0, 0.015f, null),
            ("Council_Setback_NoPlate", 0, 0.004f, null),
            ("Council_Setback_RainedOut", 0, 0.004f, null),
            ("Council_Setback_Homesick", 0, 0.01f, null),
            ("Council_Setback_WorkTornDown", 0, 0.03f, "Builder"),
            ("Council_Setback_WorkWrecked", 0, 0.03f, "Builder"),
            ("Council_Setback_CropLost", 0, 0.05f, "Grower"),
            ("Council_Setback_BotchedBuild", 0, 0.12f, "Builder"),
            ("Council_Setback_ShoddyWork", 0, 0.15f, "Crafter"),
            ("Council_Setback_MakingDo", 0, 0.2f, "Crafter"),
            ("Council_Setback_BotchedSurgery", 0, 0.02f, "Doctor"),
            ("Council_Setback_LongHauls", 0, 0.06f, null),
            ("Council_Setback_LeftWaiting", 0, 0.012f, null),
            ("Council_Setback_ColdMeal", 0, 0.05f, null),
            ("Council_Setback_ArtSold", 0, 0.02f, "Crafter"),
            ("Council_Setback_AnimalSold", 0, 0.03f, "Hunter"),
            ("Council_Setback_Outshone", 0, 0.012f, null),
            ("Council_Setback_DriftedApart", 0, 0.008f, null),
            ("Council_Setback_NoisyNeighbour", 0, 0.04f, null)
        };

        /// <summary>Setbacks a well-run colony mostly avoids and a neglected one keeps having: their rate follows the style squared.</summary>
        public static readonly HashSet<string> CareSetbacks = new HashSet<string>
        {
            "Council_Setback_MakingDo", "Council_Setback_LongHauls", "Council_Setback_LeftWaiting", "Council_Setback_ColdMeal",
            "Council_Setback_NoisyNeighbour", "Council_Setback_NoPlate"
        };
    }
}
