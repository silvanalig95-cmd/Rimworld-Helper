using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// One kind of hurt from everyday life, as the shared budget knows it (HarmBudgetMath): registered once, in
    /// <see cref="HarmKinds"/>, with the system it sits under and its base rate. Every roll and every hurt through the harm
    /// service names its kind (Harm/Harm.cs).
    /// </summary>
    public sealed class HarmKind
    {
        /// <summary>Saved (the budget's memory of when each kind last came): never changed once a kind has shipped.</summary>
        public readonly string key;
        /// <summary>For the balance model's report and the dev tools.</summary>
        public readonly string label;
        /// <summary>The system it sits under: Harm.SystemOn tells whether that system is on in this game (its setting, its mod).</summary>
        public readonly string system;
        /// <summary>
        /// Its base rate: hurts the shared cap counts per colonist-year, at 100%, the usual storyteller, in an average colony
        /// of six, coming on its own (no budget). The balance model's harm mode measures it (Tools/Balance -- --harm).
        /// </summary>
        public readonly float weight;
        /// <summary>Built before the budget (0.106.0 to 0.149.0): their base rates together are the budget's size.</summary>
        public readonly bool founding;

        public HarmKind(string key, string label, string system, float weight, bool founding)
        {
            this.key = key;
            this.label = label;
            this.system = system;
            this.weight = weight;
            this.founding = founding;
        }

        public override string ToString() => key;
    }

    /// <summary>
    /// Every kind of Rimfolk-made hurt, registered once. A later kind is one line here (its key, its label, its system and
    /// its base rate from the balance model, founding false) and one case in Harm.SystemOn when its system is new; it then
    /// takes its turn within the budget, which it shares with whatever kinds are switched on.
    /// </summary>
    public static class HarmKinds
    {
        // ---------------- Built before the budget: their total is the budget ----------------

        public static readonly HarmKind Drills = new HarmKind("drills", "drills that bruise (1.2.1)", "drills", 0.036f, true);
        public static readonly HarmKind TiredHands = new HarmKind("tiredHands", "tired hands (1.2.2)", "decrees", 0.0385f, true);
        public static readonly HarmKind WorkedThrough = new HarmKind("workedThrough", "working through it (3.2.1)", "ailments", 0.005f, true);
        public static readonly HarmKind Dangers = new HarmKind("dangers", "dangers on the map", "dangers", 0.020f, true);
        public static readonly HarmKind LandAilments = new HarmKind("landAilments", "ailments of the land and the work", "ailments", 0.178f, true);
        public static readonly HarmKind CampBrawl = new HarmKind("campBrawl", "a camp's brawl (2.4.1)", "camps", 0.0083f, true);
        public static readonly HarmKind SickHome = new HarmKind("sickHome", "sickness brought home (3.1.1)", "journeys", 0.0006f, true);
        public static readonly HarmKind ChampionBout = new HarmKind("championBout", "the champion's bout (2.2.1)", "champion", 0.0003f, true);
        public static readonly HarmKind HuntTurn = new HarmKind("huntTurn", "hunts that turn (2.1.1)", "hunts", 0.087f, true);
        public static readonly HarmKind Sabotage = new HarmKind("sabotage", "a rival's sabotage (3.5.1)", "rivals", 0.018f, true);
        public static readonly HarmKind Hustings = new HarmKind("hustings", "the hustings' blows (1.4.1)", "elections", 0.0055f, true);
        public static readonly HarmKind Frayed = new HarmKind("frayed", "frayed tempers (3.3.1)", "heart", 0.0017f, true);
        public static readonly HarmKind Stampede = new HarmKind("stampede", "a stampede (great herds)", "herds", 0.020f, true);
        public static readonly HarmKind FeastBout = new HarmKind("feastBout", "a feast's wrestling", "feasts", 0.0034f, true);
        public static readonly HarmKind FeastSour = new HarmKind("feastSour", "a guest's night gone sour at a feast", "feasts", 0.019f, true);
        public static readonly HarmKind PilgrimCough = new HarmKind("pilgrimCough", "a pilgrim's cough", "peoples", 0.0037f, true);
        public static readonly HarmKind SchemeFight = new HarmKind("schemeFight", "a scheme's fight", "schemes", 0.017f, true);
        public static readonly HarmKind PokemonSpar = new HarmKind("pokemonSpar", "a Pokémon's spar", "pokemon", 0.003f, true);

        // ---------------- Harm, part 4 (the fifteen the player took, 7 November 2026): turns within the budget ----------------

        public static readonly HarmKind Physician = new HarmKind("physician", "the physician catches what they tend (1.1.1)", "physician", 0.0098f, false);
        public static readonly HarmKind GuestCough = new HarmKind("guestCough", "the guest's cough (2.5.2)", "guestCough", 0.024f, false);
        public static readonly HarmKind Hail = new HarmKind("hail", "caught in the hail (2.2.3)", "hail", 0.040f, false);
        public static readonly HarmKind ColdNight = new HarmKind("coldNight", "a cold night for the frail (1.2.3)", "coldNight", 0.020f, false);
        public static readonly HarmKind Felling = new HarmKind("felling", "a felling mishap (4.3.1)", "felling", 0.018f, false);
        public static readonly HarmKind FailedBuild = new HarmKind("failedBuild", "a failed build that hurts (3.4.1)", "failedBuild", 0.059f, false);
        public static readonly HarmKind HotBench = new HarmKind("hotBench", "burned at the bench (4.7.2)", "hotBench", 0.0125f, false);
        public static readonly HarmKind Rut = new HarmKind("rut", "the rut in the pens (4.5.3)", "rut", 0.0167f, false);
        public static readonly HarmKind HerdLesson = new HarmKind("herdLesson", "the herdmaster's lesson (4.5.1)", "herdLesson", 0.0068f, false);
        public static readonly HarmKind Saddle = new HarmKind("saddle", "a fall from the saddle (4.9.1)", "saddle", 0.049f, false);
        public static readonly HarmKind GroupBrawl = new HarmKind("groupBrawl", "the groups come to blows (3.6.1)", "groups", 0.068f, false);
        public static readonly HarmKind SoreLoser = new HarmKind("soreLoser", "the sore loser (1.4.2)", "elections", 0.0038f, false);
        public static readonly HarmKind GuestNight = new HarmKind("guestNight", "a guest's night goes sour (2.5.1)", "guestNight", 0.021f, false);
        public static readonly HarmKind GuestNightHospitality = new HarmKind("guestNightHospitality", "a guest's night goes sour, Hospitality's guests (2.5.1)", "guestNightHospitality", 0.083f, false);
        public static readonly HarmKind Rematch = new HarmKind("rematch", "the rematch (2.8.1)", "rematch", 0.0042f, false);
        public static readonly HarmKind Games = new HarmKind("games", "games with real contests (4.1.1)", "games", 0.027f, false);

        // ---------------- Harm, part 5 (sickness that spreads, the weather and the cold, 7 November 2026) ----------------

        public static readonly HarmKind SickWorkOn = new HarmKind("sickWorkOn", "the sick work on (1.2.5)", "sickWorkOn", 0.020f, false);
        public static readonly HarmKind GateSickness = new HarmKind("gateSickness", "the sickness at the gate (2.3.1)", "gateSickness", 0.0085f, false);
        public static readonly HarmKind CampFever = new HarmKind("campFever", "the fever at their fire (2.4.2)", "campFever", 0.0034f, false);
        public static readonly HarmKind CourierSick = new HarmKind("courierSick", "a courier who arrives sick (2.7.2)", "courierSick", 0.0026f, false);
        public static readonly HarmKind PatientIll = new HarmKind("patientIll", "the patient who brings something worse (3.2.2)", "patientIll", 0.0016f, false);
        public static readonly HarmKind BadBatch = new HarmKind("badBatch", "the hedge healer's bad batch (2.2.4)", "badBatch", 0.007f, false);
        public static readonly HarmKind SmallThings = new HarmKind("smallThings", "small things that grow (3.2.3)", "smallThings", 0.008f, false);
        public static readonly HarmKind DirtyWater = new HarmKind("dirtyWater", "dirty water (4.9.3)", "dirtyWater", 0.05f, false);
        public static readonly HarmKind GateCold = new HarmKind("gateCold", "waiting at the gate in the cold (3.1.2)", "gateCold", 0.0018f, false);
        public static readonly HarmKind NightWatch = new HarmKind("nightWatch", "the night watch in the cold (4.2.2)", "nightWatch", 0.009f, false);
        public static readonly HarmKind WeatherLovers = new HarmKind("weatherLovers", "weather lovers (3.11.3)", "weatherLovers", 0.0005f, false);
        public static readonly HarmKind Sleepwalk = new HarmKind("sleepwalk", "sleepwalking after a nightmare (3.12.1)", "sleepwalk", 0.024f, false);
        public static readonly HarmKind RestlessWalk = new HarmKind("restlessWalk", "a restless walk at dusk (3.3.3)", "restlessWalk", 0.0035f, false);
        public static readonly HarmKind ColdWater = new HarmKind("coldWater", "a slip into cold water (4.9.2)", "coldWater", 0.028f, false);

        // The peoples, release D (Peoples/PeoplesD2.cs): a tamer's friendly bout by the Fighting-type spar's rules, the
        // companion's blow one bout in 25 (Tools/Balance/HarmSim.cs, TamerBouts).
        public static readonly HarmKind TamerBout = new HarmKind("tamerBout", "a tamer's bout (peoples)", "peoples", 0.009f, false);

        // ---------------- Harm, part 6 (the work's accidents, the animals, the contests and festivals, 7 November 2026) ----------------

        public static readonly HarmKind HotHeavy = new HarmKind("hotHeavy", "hot and heavy answers (2.1.2)", "hotHeavy", 0.0097f, false);
        public static readonly HarmKind BackTooSoon = new HarmKind("backTooSoon", "back too soon, for real (2.1.3)", "backTooSoon", 0.0041f, false);
        public static readonly HarmKind ProjectPush = new HarmKind("projectPush", "the project's last push (1.5.2)", "projectPush", 0.005f, false);
        public static readonly HarmKind ClumsyWorn = new HarmKind("clumsyWorn", "clumsy when worn (3.3.2)", "clumsyWorn", 0.0023f, false);
        public static readonly HarmKind Makeshift = new HarmKind("makeshift", "the makeshift spot's burn (3.4.2)", "makeshift", 0.0075f, false);
        public static readonly HarmKind TakingDown = new HarmKind("takingDown", "taking down hurts too (4.3.2)", "takingDown", 0.021f, false);
        public static readonly HarmKind HurriedHarvest = new HarmKind("hurriedHarvest", "hurried hands at the harvest (4.4.1)", "hurriedHarvest", 0.035f, false);
        public static readonly HarmKind StrongBack = new HarmKind("strongBack", "carries more than they should (3.11.1)", "strongBack", 0.0052f, false);
        public static readonly HarmKind SharpHabits = new HarmKind("sharpHabits", "sharp habits (3.11.2)", "sharpHabits", 0.036f, false);
        public static readonly HarmKind SecondDrink = new HarmKind("secondDrink", "the second drink (4.7.1)", "secondDrink", 0.025f, false);
        public static readonly HarmKind HandsSore = new HarmKind("handsSore", "the hands come home sore (2.6.3)", "handsSore", 0.0078f, false);
        public static readonly HarmKind DroversBull = new HarmKind("droversBull", "the drovers' bull (2.4.3)", "droversBull", 0.007f, false);
        public static readonly HarmKind TooClose = new HarmKind("tooClose", "the wild one too close (2.10.2)", "tooClose", 0.013f, false);
        public static readonly HarmKind DawnWalk = new HarmKind("dawnWalk", "walks the fields at dawn (3.11.4)", "dawnWalk", 0.009f, false);
        public static readonly HarmKind BoldWolf = new HarmKind("boldWolf", "the fed wolf grows bold (4.5.2)", "boldWolf", 0.0047f, false);
        public static readonly HarmKind CatchFights = new HarmKind("catchFights", "a catch that fights back (4.6.1)", "pokemonCatch", 0.006f, false);
        public static readonly HarmKind StoresVisitor = new HarmKind("storesVisitor", "the visitor at the stores turns (4.6.2)", "pokemonStores", 0.012f, false);
        public static readonly HarmKind BonfireSpark = new HarmKind("bonfireSpark", "the bonfire's spark (4.1.2)", "bonfireSpark", 0.00015f, false);
        public static readonly HarmKind LineMuster = new HarmKind("lineMuster", "musters at the line (4.2.1)", "lineMuster", 0.0067f, false);
        public static readonly HarmKind MagicSpar = new HarmKind("magicSpar", "fighters spar (4.8.2)", "magicSpar", 0.010f, false);
        public static readonly HarmKind BraveDare = new HarmKind("braveDare", "brave to a fault (3.6.2)", "braveDare", 0.039f, false);
        public static readonly HarmKind CowardName = new HarmKind("cowardName", "a coward's name (3.7.2)", "cowardName", 0.0044f, false);
        public static readonly HarmKind VendettaFury = new HarmKind("vendettaFury", "fury that forgets cover (3.8.2)", "vendettaFury", 0.003f, false);

        // ---------------- Harm, part 7 (tempers in the colony, visitors and outsiders, personal stories, 7 November 2026) ----------------
        // The last of the review's proposals. Three of them hurt nobody of the colony and stay outside the budget: the
        // nemesis seeking the one it hurt (2.10.1: the raid's or the beast's own attack, aimed), the lost child found hurt
        // (2.6.1: a neighbour's child) and the courier set upon at the edge (2.7.1: their courier, in place of the cut on
        // the road they always came in with).

        public static readonly HarmKind SitDown = new HarmKind("sitDown", "the peacemaker's sit-down goes wrong (1.1.2)", "sitDown", 0.021f, false);
        public static readonly HarmKind SpeakerWords = new HarmKind("speakerWords", "words with the speaker (1.3.1)", "speakerWords", 0.0035f, false);
        public static readonly HarmKind PassedOver = new HarmKind("passedOver", "the thing that went to someone else (1.3.2)", "passedOver", 0.0054f, false);
        public static readonly HarmKind MeetingBoils = new HarmKind("meetingBoils", "a meeting that boils over (1.5.1)", "meetingBoils", 0.0081f, false);
        public static readonly HarmKind FriendsStand = new HarmKind("friendsStand", "friends stand by (3.5.2)", "friendsStand", 0.0145f, false);
        public static readonly HarmKind SaidToFace = new HarmKind("saidToFace", "said to their face (3.7.1)", "saidToFace", 0.020f, false);
        public static readonly HarmKind WrongedOne = new HarmKind("wrongedOne", "found out by the wronged one (3.9.1)", "wrongedOne", 0.0043f, false);
        public static readonly HarmKind ToldByFriend = new HarmKind("toldByFriend", "told by the wrong friend (3.9.2)", "toldByFriend", 0.0056f, false);
        public static readonly HarmKind LoveRivals = new HarmKind("loveRivals", "a rival in love comes to blows (3.10.1)", "loveRivals", 0.0138f, false);
        public static readonly HarmKind AltarWords = new HarmKind("altarWords", "words at the altar (3.13.1)", "altarWords", 0.0031f, false);
        public static readonly HarmKind FeastFists = new HarmKind("feastFists", "the feast that ends in a fistfight (4.1.3)", "feastFists", 0.0149f, false);
        public static readonly HarmKind SecondRound = new HarmKind("secondRound", "the toast's second round (4.7.3)", "secondRound", 0.0052f, false);
        public static readonly HarmKind SoldierNight = new HarmKind("soldierNight", "the soldier's nightmare (3.12.2)", "soldierNight", 0.0023f, false);
        public static readonly HarmKind OpenGates = new HarmKind("openGates", "open gates, and who walks in (1.2.4)", "openGates", 0.018f, false);
        public static readonly HarmKind GamblerTurns = new HarmKind("gamblerTurns", "the gambler's game turns (2.2.2)", "gamblerTurns", 0.0019f, false);
        public static readonly HarmKind BarberNick = new HarmKind("barberNick", "the barber's nick (2.5.3)", "barberNick", 0.0105f, false);
        public static readonly HarmKind RankEnemy = new HarmKind("rankEnemy", "the guest of rank's enemy (2.6.2)", "rankEnemy", 0.0005f, false);
        public static readonly HarmKind CrowdFight = new HarmKind("crowdFight", "a fistfight that draws a crowd (2.8.2)", "crowdFight", 0.0018f, false);
        public static readonly HarmKind PastFirstBlood = new HarmKind("pastFirstBlood", "first blood, not stopped (2.8.3)", "pastFirstBlood", 0.0003f, false);
        public static readonly HarmKind LetterChallenge = new HarmKind("letterChallenge", "a challenge by letter (2.9.1)", "letterChallenge", 0.0014f, false);
        public static readonly HarmKind HeirGrudge = new HarmKind("heirGrudge", "the heir's grudge (2.9.2)", "heirGrudge", 0.0008f, false);
        public static readonly HarmKind HatedTrader = new HarmKind("hatedTrader", "a trader of the hated people (3.8.1)", "hatedTrader", 0.0035f, false);
        public static readonly HarmKind GateZeal = new HarmKind("gateZeal", "zeal at the gate (3.13.2)", "gateZeal", 0.0026f, false);
        public static readonly HarmKind HungryHands = new HarmKind("hungryHands", "hungry hands in the fields (2.3.2)", "hungryHands", 0.0039f, false);
        public static readonly HarmKind CreekBrawl = new HarmKind("creekBrawl", "a brawl at the creek (2.3.3)", "creekBrawl", 0.0002f, false);
        public static readonly HarmKind WorstNight = new HarmKind("worstNight", "the worst night, alone (3.10.2)", "worstNight", 0.0021f, false);
        public static readonly HarmKind FirstSteps = new HarmKind("firstSteps", "first steps alone (3.10.3)", "firstSteps", 0.0014f, false);
        public static readonly HarmKind WorkingWrong = new HarmKind("workingWrong", "a working gone wrong (4.8.1)", "workingWrong", 0.0058f, false);

        // ---------------- Camps that build and leave things behind (8 November 2026) ----------------

        /// <summary>What a camp left behind: offal that draws a predator in, embers in dry grass (Camps/CampPack.cs), the game's own beast or fire.</summary>
        public static readonly HarmKind CampLeft = new HarmKind("campLeft", "what a camp left behind (offal, embers)", "campLeft", 0.0015f, false);

        // ---------------- The peoples, release F (8 November 2026) ----------------

        /// <summary>
        /// A mining accident at the Deepwell Union's drill (Peoples/PeoplesF.cs): the colony's hand who works the drill
        /// beside the crew, one shift in twenty (PeoplesFMath.AccidentChance; Tools/Balance/HarmSim.cs, UnionDrills).
        /// </summary>
        public static readonly HarmKind UnionDrill = new HarmKind("unionDrill", "a mining accident at the union's drill (peoples)", "peoples", 0.005f, false);

        // ---------------- The gods in the world (11 November 2026) ----------------

        /// <summary>A god's beasts sent after the colonist who killed its sacred animal (Gods/World/GodHealth.cs): the game's wolves, manhunting.</summary>
        public static readonly HarmKind GodBeasts = new HarmKind("godBeasts", "a god's beasts sent after an offender (gods)", "gods", 0.012f, false);

        /// <summary>A god's lightning on the roof over an offender in a thunderstorm (Gods/World/GodHealth.cs): the game's own strike, clear of anyone.</summary>
        public static readonly HarmKind GodLightning = new HarmKind("godLightning", "a god's lightning on an offender's roof (gods)", "gods", 0.004f, false);

        /// <summary>A gift's price paid in blood (Gods/World/GodWorldGifts.cs): a cut on the arm, taken there and then.</summary>
        public static readonly HarmKind GodPrice = new HarmKind("godPrice", "a god's gift paid for in blood (gods)", "gods", 0.008f, false);

        /// <summary>The great cat's cuff of the paw for a wrong answer to her riddle (Gods/OnMap/MogsWays.cs): a scratch on the arm.</summary>
        public static readonly HarmKind GodRiddle = new HarmKind("godRiddle", "a great cat's cuff for a wrong answer (gods)", "gods", 0.004f, false);

        /// <summary>The wrapped dead standing guard over a home's tombs in a raid (Gods/OnMap/MogsWays.cs): the game's shamblers, kept to the graves.</summary>
        public static readonly HarmKind GodDead = new HarmKind("godDead", "the wrapped dead standing guard in a raid (gods)", "gods", 0.004f, false);

        /// <summary>A miner waking from a warm dream with a burn across the hand, for silver kept from the deep (Gods/OnMap/OwnSmoulder.cs).</summary>
        public static readonly HarmKind GodBurn = new HarmKind("godBurn", "a warm dream's burn for silver kept from the deep (gods)", "gods", 0.003f, false);

        /// <summary>Every kind, in the order they were built.</summary>
        public static readonly HarmKind[] All =
        {
            Drills, TiredHands, WorkedThrough, Dangers, LandAilments, CampBrawl, SickHome, ChampionBout, HuntTurn, Sabotage, Hustings, Frayed, Stampede,
            FeastBout, FeastSour, PilgrimCough, SchemeFight, PokemonSpar,
            Physician, GuestCough, Hail, ColdNight, Felling, FailedBuild, HotBench, Rut, HerdLesson, Saddle, GroupBrawl, SoreLoser, GuestNight, GuestNightHospitality, Rematch, Games,
            SickWorkOn, GateSickness, CampFever, CourierSick, PatientIll, BadBatch, SmallThings, DirtyWater, GateCold, NightWatch, WeatherLovers, Sleepwalk, RestlessWalk, ColdWater,
            TamerBout,
            HotHeavy, BackTooSoon, ProjectPush, ClumsyWorn, Makeshift, TakingDown, HurriedHarvest, StrongBack, SharpHabits, SecondDrink, HandsSore,
            DroversBull, TooClose, DawnWalk, BoldWolf, CatchFights, StoresVisitor,
            BonfireSpark, LineMuster, MagicSpar, BraveDare, CowardName, VendettaFury,
            SitDown, SpeakerWords, PassedOver, MeetingBoils, FriendsStand, SaidToFace, WrongedOne, ToldByFriend, LoveRivals, AltarWords, FeastFists, SecondRound,
            SoldierNight,
            OpenGates, GamblerTurns, BarberNick, RankEnemy, CrowdFight, PastFirstBlood, LetterChallenge, HeirGrudge, HatedTrader, GateZeal, HungryHands, CreekBrawl,
            WorstNight, FirstSteps, WorkingWrong,
            CampLeft,
            UnionDrill,
            GodBeasts, GodLightning, GodPrice, GodRiddle, GodDead, GodBurn
        };

        public static HarmKind Of(string key)
        {
            if (key == null) return null;
            for (int i = 0; i < All.Length; i++)
                if (All[i].key == key) return All[i];
            return null;
        }
    }

    /// <summary>
    /// The shared budget of hurts from everyday life (the player's choice, 7 November 2026: "yes, a shared budget please").
    /// Rimfolk's everyday hurts together stay at what the kinds built before it came to (their base rates summed), however
    /// many kinds come after: each kind's chance is multiplied by its pass, and the passes share the budget out among the
    /// kinds switched on, by their base rates. A kind that came lately passes less often than one that hasn't, and the rest
    /// a little more, so the mix varies and the commonest kind doesn't eat the budget. The pass knows nothing of the setting
    /// or the storyteller: "Hurts from everyday life" and the storyteller's lean scale every kind exactly as before, and the
    /// shared cap and its exclusions stand as they were (HarmMath.Bar).
    /// <list type="bullet">
    /// <item>The budget: the founding kinds switched on, their base rates together (<see cref="Budget"/>).</item>
    /// <item>Variety (<see cref="Variety"/>): a kind just come weighs 0.4 of its rate, back to all of it over fifteen days
    /// (a quadrum) since it last hurt someone.</item>
    /// <item>The pass (<see cref="Passes"/>): its variety times the budget over the kinds on, each its base rate times its
    /// variety; so the kinds' base rates times their passes always add up to the budget. Never more than twice a kind's
    /// own rate.</item>
    /// </list>
    /// Pure, so Tools/Tests and Tools/Balance read the same rules; the game's side is Harm.PassOf (Harm/Harm.cs).
    /// </summary>
    public static class HarmBudgetMath
    {
        public const int DayTicks = HarmMath.DayTicks;

        /// <summary>Days over which a kind that came gets back its whole share: a quadrum.</summary>
        public const float VarietyDays = 30f;

        /// <summary>A kind just come weighs this much less (0.6 less: 0.4 of its rate), easing off over <see cref="VarietyDays"/>.</summary>
        public const float VarietyDip = 0.75f;

        /// <summary>No kind passes at more than twice its own rate, however many others rest.</summary>
        public const float MaxPass = 2f;

        /// <summary>A kind that never came, or came long ago.</summary>
        public const float LongAgo = 9999f;

        /// <summary>
        /// How much a kind weighs now by when it last came: 0.4 the day it came, rising evenly to 1 over fifteen days;
        /// 1 for one that never came (or a time from a save played on: <paramref name="daysSince"/> below nothing).
        /// </summary>
        public static float Variety(float daysSince)
        {
            if (daysSince < 0f || daysSince >= VarietyDays) return 1f;
            return 1f - VarietyDip * (1f - daysSince / VarietyDays);
        }

        /// <summary>Days since a kind last came, from its tick (int.MinValue: never), for <see cref="Variety"/>.</summary>
        public static float DaysSince(int lastTick, int now)
        {
            if (lastTick == HarmMath.Never || lastTick > now) return LongAgo;
            return (now - lastTick) / (float)DayTicks;
        }

        /// <summary>The kind's memory can go: it has its whole share back.</summary>
        public static bool Forgotten(int lastTick, int now) => DaysSince(lastTick, now) >= VarietyDays;

        /// <summary>The budget: the founding kinds among those on, their base rates together.</summary>
        public static float Budget(IList<HarmKind> on)
        {
            float b = 0f;
            if (on == null) return b;
            for (int i = 0; i < on.Count; i++)
                if (on[i] != null && on[i].founding)
                    b += Math.Max(0f, on[i].weight);
            return b;
        }

        /// <summary>The kinds on, their base rates together: what they would come to with no budget.</summary>
        public static float Unbudgeted(IList<HarmKind> on)
        {
            float w = 0f;
            if (on == null) return w;
            for (int i = 0; i < on.Count; i++)
                if (on[i] != null)
                    w += Math.Max(0f, on[i].weight);
            return w;
        }

        /// <summary>
        /// Every kind's pass now (into <paramref name="pass"/>, beside <paramref name="on"/>): its variety times the budget
        /// over the kinds on weighed by their variety, never more than <see cref="MaxPass"/>. With no new kinds on and none
        /// come lately, every pass is 1: the hurts as they were.
        /// </summary>
        public static void Passes(IList<HarmKind> on, IList<float> daysSince, float[] pass)
        {
            if (on == null || pass == null) return;
            float budget = 0f, shared = 0f;
            for (int i = 0; i < on.Count; i++)
            {
                var k = on[i];
                if (k == null) continue;
                float w = Math.Max(0f, k.weight);
                if (k.founding) budget += w;
                shared += w * Variety(i < daysSince.Count ? daysSince[i] : LongAgo);
            }
            for (int i = 0; i < on.Count && i < pass.Length; i++)
                pass[i] = PassFor(Variety(i < daysSince.Count ? daysSince[i] : LongAgo), budget, shared);
        }

        /// <summary>One kind's pass by its variety, the budget, and the kinds on weighed by their variety.</summary>
        public static float PassFor(float variety, float budget, float shared)
        {
            if (shared <= 0f || budget <= 0f) return 1f;
            return HarmMath.Clamp(variety * budget / shared, 0f, MaxPass);
        }

        /// <summary>One kind's pass among those on (it needn't be one of them: a kind asked for while its system is off).</summary>
        public static float Pass(HarmKind kind, IList<HarmKind> on, IList<float> daysSince)
        {
            if (kind == null || on == null) return 1f;
            float budget = 0f, shared = 0f, mine = LongAgo;
            for (int i = 0; i < on.Count; i++)
            {
                var k = on[i];
                if (k == null) continue;
                float d = i < daysSince.Count ? daysSince[i] : LongAgo;
                float w = Math.Max(0f, k.weight);
                if (k.founding) budget += w;
                shared += w * Variety(d);
                if (k == kind) mine = d;
            }
            return PassFor(Variety(mine), budget, shared);
        }

        /// <summary>What the kinds on come to with their passes now (for the tests and the docs): the budget, while no pass reaches its ceiling.</summary>
        public static float Expected(IList<HarmKind> on, IList<float> daysSince)
        {
            if (on == null) return 0f;
            var pass = new float[on.Count];
            Passes(on, daysSince, pass);
            float t = 0f;
            for (int i = 0; i < on.Count; i++)
                if (on[i] != null)
                    t += Math.Max(0f, on[i].weight) * pass[i];
            return t;
        }
    }
}
