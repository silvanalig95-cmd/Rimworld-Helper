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

        /// <summary>
        /// The game's own human diseases on a temperate forest (Core's Biomes_Temperate.xml: diseaseMtbDays 50, rolled by the
        /// storyteller's StorytellerComp_Disease for DiseaseHuman), times the storyteller's disease interval: 1 at Strive to
        /// survive (Rough), 1.5 at Adventure story (Medium), 0.9 to 0.95 harder, 2.5 to 3 at the two gentlest. --illness
        /// --interval sets it.
        /// </summary>
        public const float GameDiseaseMtbDays = 50f;
        public static float GameDiseaseInterval = 1f;

        /// <summary>
        /// Of the game's human diseases there, those that can kill: the flu, the plague and malaria (100 each of the land's 460
        /// commonality; gut worms, muscle parasites and the two mechanites can't).
        /// </summary>
        public const float GameDiseaseDeadly = 300f / 460f;

        /// <summary>
        /// The game's own human diseases there as the colony meets them, for --illness's count of a colonist with two serious
        /// illnesses at once: the land's commonality, whether it can kill, the share of the colony it takes (at least one, at
        /// most so many) and how long it lasts with the usual tending. A rough model of the game's own (the flu and the plague
        /// a week or so, malaria two, the worms, parasites and mechanites a month or so), not read from its defs.
        /// </summary>
        public static readonly (string name, float weight, bool deadly, float shareMin, float shareMax, int most, float daysMin, float daysMax)[] GameDiseaseKinds =
        {
            ("flu", 100f, true, 0.2f, 0.4f, 5, 5f, 9f),
            ("plague", 100f, true, 0.2f, 0.4f, 5, 7f, 12f),
            ("malaria", 100f, true, 0.2f, 0.4f, 5, 10f, 16f),
            ("gut worms", 50f, false, 0.1f, 0.25f, 3, 15f, 30f),
            ("muscle parasites", 50f, false, 0.1f, 0.25f, 3, 15f, 30f),
            ("fibrous mechanites", 30f, false, 0.05f, 0.15f, 1, 15f, 30f),
            ("sensory mechanites", 30f, false, 0.05f, 0.15f, 1, 15f, 30f)
        };

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

        /// <summary>The game's minor-break line for most colonists (the MentalBreakThreshold stat's base).</summary>
        public const float MinorBreak = 0.35f;

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
        /// The share of colonists a side of a name shows for (rep: tags, Social/Gossip/): a few in a colony, after a
        /// story has gone round. On top of the tag condition's own factor.
        /// </summary>
        public const float RepTagShare = 0.2f;

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
            ["minMedicine"] = 0.8f, ["maxMedicine"] = 0.4f, ["minMood"] = 0.7f, ["needsMechanoids"] = 0.1f,
            // Life together (Social/SocialEvents.cs): the memory is about the other one (the fight was with them), two at
            // the same work side by side, a fine piece finished in the last day or two, a hurt of everyday life that shows.
            ["bMemoryAboutA"] = 0.5f, ["sameWork"] = 0.15f, ["bMadeFineDays"] = 0.04f, ["bHurtDays"] = 0.03f
        };

        /// <summary>
        /// A role whose tags name the moment (the weather, the hour, at work or at the table: TogetherFourMath.NeedsMoment):
        /// rain or snow on about one waking hour in five, two of them at supper or at the same work for a while each day.
        /// </summary>
        public const float MomentTagGate = 0.25f;

        /// <summary>
        /// Group moments (Social/Groups/, GroupMomentExtension): who they are between. Most pairs in a colony with groups
        /// don't share one; few pairs are across two groups at odds, fewer still a member and a newcomer (welcomed, spoken
        /// for, or shut out), or one who is out of step with their group's ways. A role named for the one who starts it
        /// (the joker, the worrier...) is one person in a group of four or five.
        /// </summary>
        public static readonly Dictionary<string, float> GroupGates = new Dictionary<string, float>
        {
            ["sameGroup"] = 0.35f, ["rivalGroups"] = 0.1f, ["welcome"] = 0.06f, ["sponsor"] = 0.03f, ["closeRanks"] = 0.02f, ["outOfStep"] = 0.06f
        };

        public const float GroupRoleGate = 0.25f;

        /// <summary>
        /// The conditions of a custom of the land or the faith (CustomConditions on a dilemma): the land it needs (a
        /// colony is one or two of a dozen lands), rain falling that day, and a faith of the colony's own with the right
        /// memes (Ideology, and one of a few kinds of belief).
        /// </summary>
        public static readonly Dictionary<string, float> CustomGates = new Dictionary<string, float>
        {
            ["lands"] = 0.3f, ["raining"] = 0.3f, ["faith"] = 0.4f, ["memes"] = 0.15f,
            // The woodpile day: the stores short of the winter's wood in the autumn (most colonies, not all).
            ["woodShort"] = 0.6f
        };

        /// <summary>The same for who the stranger in an encounter or a stranger's question must be.</summary>
        public static readonly Dictionary<string, float> StrangerGates = new Dictionary<string, float>
        {
            ["ofA"] = 0.7f, ["bondedToA"] = 0.3f, ["nemesis"] = 0.05f, ["nemesisOfA"] = 0.03f, ["majestic"] = 0.15f, ["named"] = 0.4f, ["predator"] = 0.4f,
            ["royal"] = 0.1f, ["child"] = 0.2f, ["anyTrait"] = 0.3f, ["withDog"] = 0.05f
        };

        /// <summary>
        /// Dilemmas whose worker looks for something the defs don't name (Animals/AnimalDilemmas.cs): a hungry colony with
        /// a herd, a grazer in the crops, a sick animal, a predator near the pens, a bond a faith abhors, the herd's season;
        /// and the council's faith questions (Dilemmas/DilemmaWorkers_Faith.cs): faiths at odds (Ideology, a split colony,
        /// half the time unless sharpened) and a devout believer asking for an evening of faith (one can be held now).
        /// </summary>
        public static readonly Dictionary<string, float> WorkerGates = new Dictionary<string, float>
        {
            ["DilemmaWorker_Famine"] = 0.1f, ["DilemmaWorker_CropRaider"] = 0.3f, ["DilemmaWorker_SickAnimal"] = 0.25f,
            ["DilemmaWorker_PenStalker"] = 0.3f, ["DilemmaWorker_BondFaith"] = 0.05f, ["DilemmaWorker_HerdCustom"] = 0.4f,
            ["DilemmaWorker_FaithClash"] = FaithSplitColony * 0.7f, ["DilemmaWorker_FaithEvening"] = FaithSplitColony * 0.6f,
            // An animal in decline: A's own animal, old for its kind or with a lasting condition (Animals/FadingPet.cs).
            ["DilemmaWorker_FadingPet"] = 0.25f,
            // The fourth round (Dilemmas/DilemmaWorkers_YearFour.cs), each only while the colony is as its letter says: a
            // field more than half empty in the growing season; medicine or cloth short with food plenty; deep snow over the
            // home and nowhere set to clear (a winter land); eight plans standing, some unpaid; trees by the outer wall; the
            // herd's hay short of a winter coming; an animal that could learn to guard and to haul; travellers home within a
            // day and a half; a doubter of the colony's faith, and the faith with its guide's role empty (Ideology).
            ["DilemmaWorker_EmptyField"] = 0.25f, ["DilemmaWorker_WhichCrop"] = 0.2f, ["DilemmaWorker_SnowPaths"] = 0.08f,
            ["DilemmaWorker_Unfinished"] = 0.2f, ["DilemmaWorker_TreeLine"] = 0.3f, ["DilemmaWorker_HerdTooBig"] = 0.08f,
            ["DilemmaWorker_TrainBeast"] = 0.3f, ["DilemmaWorker_HomeFromRoad"] = 0.05f,
            ["DilemmaWorker_Doubter"] = FaithSplitColony * 0.3f, ["DilemmaWorker_NoGuide"] = FaithSplitColony * 0.2f
        };

        // ---------------- Faith and the council (Faith/, with Ideology) ----------------

        /// <summary>Share of colonies (runs) with Ideology and more than one faith at home, where faith and the council come into play.</summary>
        public const float FaithSplitColony = 0.35f;

        /// <summary>
        /// Evenings of faith in such a colony: about one in nine days (the council calls one every five days at most, a
        /// faith every ten); the share of colonists who come; and how they go (poor, good, fine).
        /// </summary>
        public const float EveningEveryDays = 9f, EveningComes = 0.6f, EveningPoor = 0.25f, EveningFine = 0.2f;

        /// <summary>
        /// One faith for the colony: the share of days the decree holds in such a colony, the share of colonists of another
        /// faith, and the trust they lose a day on average (FaithCouncilMath.DecreeTrustPerDay: a quarter to a whole point).
        /// </summary>
        public const float OneFaithDays = 0.12f, OneFaithOutsiders = 0.6f, OneFaithTrustPerDay = -0.55f;

        // ---------------- What stages something on the map (--scenes, Scenes.cs) ----------------

        /// <summary>
        /// A settled colony's calendar in its third year, from the player's own calendar at day 128 (13 days written down:
        /// founding, harvest, two weddings and two battles won kept as feasts; two deaths, a pet and a battle with the fallen
        /// remembered; the first trade's gifts; a faith's day; a wedding of one now dead kept alone), with one game day of
        /// the first snow. Birthdays and arrivals are the person's own and stage nothing; the cap is 12 days of the
        /// colony's own and 8 named by the player (CalendarMath).
        /// </summary>
        public static readonly string[] SceneCalendar =
        {
            "FeastDay", "FeastDay", "FeastDay", "FeastDay", "FeastDay", "FeastDay", "GamesDay", "Remembered", "Remembered", "Remembered", "Remembered",
            "GiftDay", "FaithDay"
        };

        /// <summary>A remembered day's dead has a grave on the map (a battle's day has none): those who knew them stand at it.</summary>
        public const float SceneRemembranceGrave = 0.6f;

        /// <summary>
        /// Groups (Social/Groups/): about one for every five colonists (the player's colony of eleven had one of seven), and
        /// the share close enough for their anchor to call them (cohesion 50 or more; the player's stood at 92).
        /// </summary>
        public const float SceneGroupsPerColonist = 0.2f, SceneGroupClose = 0.75f;

        /// <summary>
        /// The seasonal customs a settled colony keeps by its third year: the player's game asked one about every eight days,
        /// one a season since the colony's pace, and a council keeps about half of them; each comes round once a year.
        /// </summary>
        public const int SceneKeptCustoms = 2;

        /// <summary>Families of three or more living at home (parents and a grown or growing child, or kin), per colonist.</summary>
        public const float SceneSupperFamilies = 0.04f;

        /// <summary>Weddings a year per colonist (the player's colony of about ten: three in its first 128 days).</summary>
        public const float SceneWeddingsPerColonist = 0.1f;

        /// <summary>A child's growth birthday or eighteenth with a parent at home, a year per colonist (about one child in ten).</summary>
        public const float SceneChildToastsPerColonist = 0.03f;

        /// <summary>Decrees a year that gather people when declared (the marshal's address for drills, a feast day, holy days): the player's game had three in 60 days.</summary>
        public const float SceneDecreeStagesPerYear = 2.5f;

        /// <summary>The year's election held a short ceremony for a seat nobody stood for (the player's 5502 did).</summary>
        public const float SceneElectionCeremony = 0.5f;

        /// <summary>Share of ambitions that set something going when reached (9 of the 66 AmbitionDefs: a feast, a toast, a fire).</summary>
        public const float SceneAmbitionStages = 0.136f;

        /// <summary>
        /// A personal story's stages that play out, per story (ArcDef stages' effects: 149 stages in 38 stories, of which
        /// 4.7% gather people, 2% go out together and 9.4% are a visit, a lesson or an errand; about four stages each).
        /// </summary>
        public const float SceneStoryGathering = 0.19f, SceneStoryOuting = 0.08f, SceneStorySmall = 0.37f;

        /// <summary>
        /// A camp's stay in days (CampKindDef.days), the share of its days' help that is a show or a song the colony walks
        /// over to (two of the help kinds of seven), and the share of its ask (a day and a half in) that sets something going.
        /// </summary>
        public const float SceneCampDaysMin = 4f, SceneCampDaysMax = 10f, SceneCampShowShare = 0.3f, SceneCampAskPlays = 0.5f;

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
                case "WishWorker_LearnFaith": return FaithSplitColony * 0.25f;
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
                case "WishWorker_Barn":
                case "WishWorker_ChildPlay": return 0.2f;
                case "WishWorker_Recruit": return 0.25f;
                case "WishWorker_OwnAnimal": return 0.3f;
                case "WishWorker_Outside": return 0.3f;
                case "WishWorker_Apprentice":
                case "WishWorker_Fix": return 0.4f;
                // People4: trees grown against the walls now and then; deep snow by the doors on cold land in winter.
                case "WishWorker_TreesByWalls": return 0.2f;
                case "WishWorker_SnowWays": return 0.1f;
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
            // Stone dust: mining under the mountain, or the stone saw in a small closed room; kneeling: sowing and cleaning;
            // handling: milking, shearing, training and taming (the hunter here is the colony's animal hand too). Small game:
            // two hours butchering hares and squirrels (the cook, and a hunter who butchers their own); fumes: the forge or
            // the smelter in a small closed room; cold wet feet: four hours on wet ground below 10 °C (by the season);
            // a bird barn: sweeping out a closed coop. The ailments of deserts and jungles, of old limbs and of mechanites
            // never come in this temperate colony of the model.
            ["Builder"] = new Dictionary<string, float> { ["HeavyWork"] = 0.7f, ["Chopping"] = 0.15f, ["Walking"] = 0.1f, ["ColdWork"] = 0.7f, ["SunWork"] = 0.5f, ["AncientWork"] = 0.01f, ["StoneDust"] = 0.15f, ["ColdWetFeet"] = 0.03f },
            ["Grower"] = new Dictionary<string, float> { ["PlantWork"] = 0.8f, ["Chopping"] = 0.3f, ["SunWork"] = 0.7f, ["ColdWork"] = 0.5f, ["HeavyWork"] = 0.1f, ["Kneeling"] = 0.3f },
            ["Crafter"] = new Dictionary<string, float> { ["HandWork"] = 0.75f, ["HeavyWork"] = 0.05f, ["StoneDust"] = 0.1f, ["MetalFumes"] = 0.08f },
            ["Cook"] = new Dictionary<string, float> { ["HandWork"] = 0.1f, ["Kneeling"] = 0.15f, ["SmallGame"] = 0.1f, ["BirdBarn"] = 0.05f },
            ["Researcher"] = new Dictionary<string, float> { ["HandWork"] = 0.05f, ["Kneeling"] = 0.1f, ["BirdBarn"] = 0.03f },
            ["Hunter"] = new Dictionary<string, float> { ["Walking"] = 0.5f, ["RoughGround"] = 0.35f, ["ColdWork"] = 0.6f, ["SunWork"] = 0.3f, ["WetFeet"] = 0.15f, ["Handling"] = 0.3f, ["SmallGame"] = 0.03f, ["ColdWetFeet"] = 0.1f },
            ["Doctor"] = new Dictionary<string, float> { ["HeavyWork"] = 0.2f, ["Kneeling"] = 0.1f, ["BirdBarn"] = 0.03f }
        };

        /// <summary>The share of colonists at or above this age (human years): about three in ten are 45 or older.</summary>
        public static float ShareFromAge(float age) => age >= 60f ? 0.1f : age >= 45f ? 0.3f : age >= 30f ? 0.55f : 1f;

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
                case "SnowGlare": return quadrum == 3 ? 1f : 0f;
                case "RoughGround": return quadrum == 3 ? 1.3f : 1f;
                // Sowing is spring to autumn; the floors need scrubbing all year.
                case "Kneeling": return quadrum == 3 ? 0.5f : 1f;
                // A swim in summer, now and then in spring and autumn; the ground is cold and wet from autumn to spring.
                case "StillSwim": return quadrum == 1 ? 1f : quadrum == 3 ? 0f : 0.3f;
                case "ColdWetFeet": return quadrum == 3 ? 0.9f : quadrum == 2 ? 0.6f : quadrum == 0 ? 0.4f : 0.05f;
                default: return 1f;
            }
        }

        // ---------------- The game's own moods and the heart (phase 3 of the review) ----------------

        /// <summary>
        /// How ordinary the game's good moods are to a colonist (GoodLifeMath.Weight, 0.15 new to 1 everyday): mostly
        /// lasting comforts (a room, beauty, a fine meal had every day), now and then something new (a party, a gift).
        /// </summary>
        public const float GameOrdinaryWeight = 0.9f;

        /// <summary>
        /// The share of the game's bad and good moods that the heart's pile counts as small (Moods/SmallMoments.cs: six points
        /// or less and three days or less, or lasting; the game's heavy kinds, losses, bad nights, hunger, pain, the dark and
        /// the rest of the heart's own sources, left out). A neglected colony's bad side is mostly small things (an ugly room,
        /// eating without a table, a cramped bedroom, the same old clothes) next to the heavy ones; the good side's small part
        /// is a fine meal, a nice room, some recreation done well.
        /// </summary>
        public const float GameSmallBad = 0.55f, GameSmallGood = 0.45f;

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
            /// <summary>Share of days a colonist eats raw meat or fish, has a smoke, and has had fruit or vegetables in the last days.</summary>
            public float rawFood, smokes, freshShare;
            /// <summary>Share of days that hold each home cause: a hard bed, damp, a dirty crowded room, smoke, the dark, hunger, late nights.</summary>
            public Dictionary<string, float> home;
            // ... and what ailments follow (AilmentTracker.Afters): the day they got over the flu or the plague, a break or
            // a burn healed, the night after a fight at home (those hurt in it count three times over), a horror; with
            // polluted ground and snow glare (winter only). The flu and the plague, and a broken leg and a burn, are each
            // one trigger here though they never come together, so those two are about half the days with either.

            // Pressures: the share of colonists at each stage on a given day (stage 0 = none).
            public float[] sameMeal, sameWork, crowded, unfinished, missing;
            public float variedTable;

            // Setbacks: a factor on the table's per-day rates, and the situational ones' share of colonists.
            public float setbackFactor, quietDays, researchIdle, bedroomEnvy, worries;

            // Small irritations
            public float nuisanceFits, frictionFactor;

            // Steady moods: the colony's ways (share at -4, -2, +2, +4), others. Strain's share at -3, -5, -8 (strained, worn, near
            // breaking) from life alone: the heart's life (HeartSim.cs) is set to give it, and the colony-year plays the heart.
            public float[] ways, strain;
            public float aspirationsPerYear, convalescencePerYear;
            public float pets;
            /// <summary>Share of personal touches built; colonists proud of the colony's trait; carrying an heirloom.</summary>
            public float touchBuilt, traitPride, heirloom;
            /// <summary>Share of pairs who share a bedroom (couples, and barracks).</summary>
            public float sharedBedrooms;
            /// <summary>
            /// Share of colonists sleeping in barracks or without a bed, and the dirtiest kitchen's cleanliness: how much likelier
            /// serious illness is caught (LifeRiskMath.SicknessFactor, the catching clock's serious one).
            /// </summary>
            public float barracks, kitchen;
        }

        public static readonly Style WellRun = new Style
        {
            name = "well run", key = "well",
            vanillaGood = 34f, vanillaBad = -8f, vanillaSpread = 6f,
            grant = 0.8f, grantFrom = 0.1f, grantTo = 0.55f, promise = 0.2f, promiseKept = 0.95f, discomforts = 0.1f,
            best = 0.7f, byDefault = 0.05f, calendarPass = 0.05f, ambitionDone = 0.75f,
            tendShare = 0.85f, tendQuality = 0.85f, bedShare = 0.5f, rawFood = 0.005f, smokes = 0.15f, freshShare = 0.9f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0f, ["DampSleep"] = 0f, ["CrowdedDirty"] = 0f, ["SmokyRoom"] = 0f, ["DarkWork"] = 0.02f, ["Hungry"] = 0.01f, ["NightWork"] = 0.08f,
                ["Firefighting"] = 0.004f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f,
                ["AfterIllness"] = 0.0012f, ["Healed"] = 0.0012f, ["AfterFight"] = 0.08f, ["Horror"] = 0.004f, ["Polluted"] = 0.01f, ["SnowGlare"] = 0.03f,
                ["StillSwim"] = 0.02f, ["BadCarcass"] = 0.0005f
            },
            // Fine meals never count as the same meal, work streaks start at 4 days (a passion takes a step off), and
            // only frames left standing count as unfinished: a well-run colony cooks fine meals and rotates work.
            sameMeal = new[] { 0.94f, 0.06f, 0f, 0f }, sameWork = new[] { 0.88f, 0.09f, 0.03f, 0f }, crowded = new[] { 0.97f, 0.03f, 0f },
            unfinished = new[] { 0.95f, 0.05f, 0f, 0f }, missing = new[] { 0.95f, 0.03f, 0.02f, 0f }, variedTable = 0.55f,
            setbackFactor = 0.6f, quietDays = 0f, researchIdle = 0f, bedroomEnvy = 0.05f, worries = 0.01f,
            nuisanceFits = 0.12f, frictionFactor = 0.7f,
            ways = new[] { 0f, 0.08f, 0.35f, 0.1f }, strain = new[] { 0.02f, 0f, 0f },
            aspirationsPerYear = 0.5f, convalescencePerYear = 0.45f, pets = 0.9f, touchBuilt = 0.9f, traitPride = 0.15f, heirloom = 0.05f, sharedBedrooms = 0.15f,
            barracks = 0f, kitchen = 0.5f
        };

        public static readonly Style Average = new Style
        {
            name = "average", key = "avg",
            vanillaGood = 26f, vanillaBad = -13f, vanillaSpread = 7f,
            grant = 0.5f, grantFrom = 0.2f, grantTo = 0.85f, promise = 0.12f, promiseKept = 0.7f, discomforts = 0.5f,
            best = 0.35f, byDefault = 0.2f, calendarPass = 0.15f, ambitionDone = 0.5f,
            tendShare = 0.55f, tendQuality = 0.65f, bedShare = 0.3f, rawFood = 0.03f, smokes = 0.2f, freshShare = 0.7f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0.1f, ["DampSleep"] = 0.05f, ["CrowdedDirty"] = 0.05f, ["SmokyRoom"] = 0.1f, ["DarkWork"] = 0.1f, ["Hungry"] = 0.05f, ["NightWork"] = 0.12f,
                ["Firefighting"] = 0.006f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f,
                ["AfterIllness"] = 0.0015f, ["Healed"] = 0.0015f, ["AfterFight"] = 0.1f, ["Horror"] = 0.005f, ["Polluted"] = 0.02f, ["SnowGlare"] = 0.05f,
                ["StillSwim"] = 0.03f, ["BadCarcass"] = 0.002f
            },
            sameMeal = new[] { 0.62f, 0.28f, 0.1f, 0f }, sameWork = new[] { 0.72f, 0.18f, 0.07f, 0.03f }, crowded = new[] { 0.85f, 0.12f, 0.03f },
            unfinished = new[] { 0.8f, 0.15f, 0.05f, 0f }, missing = new[] { 0.94f, 0.04f, 0.02f, 0f }, variedTable = 0.2f,
            setbackFactor = 1f, quietDays = 0.15f, researchIdle = 0.05f, bedroomEnvy = 0.15f, worries = 0.02f,
            nuisanceFits = 0.4f, frictionFactor = 1f,
            ways = new[] { 0.02f, 0.13f, 0.2f, 0.04f }, strain = new[] { 0.08f, 0.02f, 0f },
            aspirationsPerYear = 0.3f, convalescencePerYear = 0.7f, pets = 0.8f, touchBuilt = 0.6f, traitPride = 0.1f, heirloom = 0.05f, sharedBedrooms = 0.3f,
            barracks = 0.15f, kitchen = -0.5f
        };

        public static readonly Style Neglectful = new Style
        {
            name = "neglectful", key = "neglect",
            vanillaGood = 18f, vanillaBad = -22f, vanillaSpread = 8f,
            grant = 0.12f, grantFrom = 0.4f, grantTo = 1f, promise = 0.05f, promiseKept = 0.3f, discomforts = 1.8f,
            best = 0.1f, byDefault = 0.65f, calendarPass = 0.1f, ambitionDone = 0.15f,
            tendShare = 0.2f, tendQuality = 0.5f, bedShare = 0.1f, rawFood = 0.12f, smokes = 0.3f, freshShare = 0.4f,
            home = new Dictionary<string, float>
            {
                ["HardBed"] = 0.6f, ["DampSleep"] = 0.3f, ["CrowdedDirty"] = 0.4f, ["SmokyRoom"] = 0.4f, ["DarkWork"] = 0.4f, ["Hungry"] = 0.25f, ["NightWork"] = 0.2f,
                ["Firefighting"] = 0.01f, ["CaravanTravel"] = 0.01f, ["Insects"] = 0f, ["Ashfall"] = 0f,
                ["AfterIllness"] = 0.0018f, ["Healed"] = 0.0018f, ["AfterFight"] = 0.12f, ["Horror"] = 0.006f, ["Polluted"] = 0.05f, ["SnowGlare"] = 0.06f,
                ["StillSwim"] = 0.03f, ["BadCarcass"] = 0.006f
            },
            sameMeal = new[] { 0.35f, 0.45f, 0.2f, 0f }, sameWork = new[] { 0.52f, 0.27f, 0.14f, 0.07f }, crowded = new[] { 0.6f, 0.3f, 0.1f },
            unfinished = new[] { 0.55f, 0.25f, 0.15f, 0.05f }, missing = new[] { 0.93f, 0.04f, 0.03f, 0f }, variedTable = 0.02f,
            setbackFactor = 1.6f, quietDays = 0.6f, researchIdle = 0.15f, bedroomEnvy = 0.25f, worries = 0.04f,
            nuisanceFits = 0.75f, frictionFactor = 1.5f,
            ways = new[] { 0.05f, 0.2f, 0.1f, 0f }, strain = new[] { 0.2f, 0.08f, 0.02f },
            aspirationsPerYear = 0.1f, convalescencePerYear = 1.1f, pets = 0.5f, touchBuilt = 0.2f, traitPride = 0.03f, heirloom = 0.05f, sharedBedrooms = 0.75f,
            barracks = 0.6f, kitchen = -2.5f
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
            ("Council_Setback_NoisyNeighbour", 0, 0.04f, null),
            // The newer systems (Setbacks/SetbackTimes.cs): about 8 calendar days kept a year, one camp a month, a mission
            // or two a season, a world event every six weeks.
            ("Council_Setback_DayRainedOut", 0, 0.003f, null),
            ("Council_Setback_BurnedDish", 0, 0.005f, null),
            ("Council_Setback_BurnedDish", 1, 0.006f, "Cook"),
            ("Council_Setback_TableQuarrel", 0, 0.003f, null),
            ("Council_Setback_EmptyChair", 0, 0.006f, null),
            ("Council_Setback_EmptyChair", 1, 0.002f, null),
            ("Council_Setback_CampSoured", 0, 0.003f, null),
            ("Council_Setback_CampOwed", 0, 0.002f, null),
            ("Council_Setback_GuestsEat", 0, 0.006f, null),
            ("Council_Setback_MissionLetDown", 1, 0.003f, null),
            ("Council_Setback_WorldTurn", 0, 0.004f, null),
            ("Council_Setback_StoodBy", 0, 0.0015f, null)
        };

        /// <summary>Setbacks a well-run colony mostly avoids and a neglected one keeps having: their rate follows the style squared.</summary>
        public static readonly HashSet<string> CareSetbacks = new HashSet<string>
        {
            "Council_Setback_MakingDo", "Council_Setback_LongHauls", "Council_Setback_LeftWaiting", "Council_Setback_ColdMeal",
            "Council_Setback_NoisyNeighbour", "Council_Setback_NoPlate", "Council_Setback_GuestsEat", "Council_Setback_BurnedDish"
        };

        // ---------------- Quiet troubles (Setbacks/Troubles.cs) ----------------

        /// <summary>
        /// When a quiet trouble's chance comes up (GoodLifeMath.TroubleChance), the share of days on which each cause is in
        /// a settled colonist's life, and its weight in the code's pick. Estimates: family out in the world is common for
        /// colonists who joined (the game makes relatives for most of them); a death day matches about one day in sixty
        /// for those who knew someone who died; most came from elsewhere, a year or more ago (weight 0.6 to 0.3); old
        /// injuries and bad backs are common in a settled colony, and a cold or wet day doubles the weight; the chance comes
        /// at the first waking hour, so most mornings follow a night's sleep, and a nightmare fits about two in three; a
        /// rival with a seat, a name or a title is less usual. Restless (-1) follows the style's work streaks: a run of
        /// seven days or more, or a third of the shorter runs (with only one kind of recreation).
        /// </summary>
        public static readonly (string thought, int stage, float share, float weight)[] Troubles =
        {
            ("Rimfolk_Trouble_Worry", 0, 0.3f, 1f),
            ("Rimfolk_Trouble_Worry", 1, 0.25f, 1f),
            ("Rimfolk_Trouble_Anniversary", 0, 0.008f, 3f),
            ("Rimfolk_Trouble_Anniversary", 1, 0.004f, 3f),
            ("Rimfolk_Trouble_FormerLife", 0, 0.9f, 0.45f),
            ("Rimfolk_Trouble_Restless", 0, -1f, 1f),
            ("Rimfolk_Trouble_Aches", 0, 0.45f, 1.3f),
            ("Rimfolk_Trouble_BadNight", 0, 0.55f, 1f),
            ("Rimfolk_Trouble_Envy", 0, 0.15f, 1f)
        };
    }
}
