// Hurts from everyday life over a colony-year (Harm/, DESIGN.md "Harm from everyday life"). Printed, nothing written:
//   dotnet run -c Release --project Tools/Balance -- --harm [--runs N] [--seed S]
//
// How often each kind of Rimfolk's hurts (Harm/HarmBudgetMath.cs, HarmKinds) hurts one of the colony, through the shared
// cap (one Rimfolk-made hurt in three days each), beside the game's own everyday hurts; the fights Rimfolk starts; and
// what the hurts and the fights add to the heart's strain and its mood. What a kind does once it comes (one bout in 25
// landing for real, the game's own revenge chance, one journey in six bringing a sickness home, a frayed temper's factor,
// a tending's one in 40) is the mod's own arithmetic, compiled in as it is (Harm/HarmMath.cs, HarmTwoMath.cs,
// HarmFourMath.cs, HarmBudgetMath.cs, Heart/HeartMath.cs, Beasts/BeastsMath.cs). How often a colony meets each (a drills
// decree, a camp at high tension, a journey to a sick town, a tree felled by an unskilled cutter, a hailstorm) is the
// table below, each an estimate that says where it comes from.
//
// Three runs of the same colonies, every kind drawing from its own generator so the lives are the same in all three:
//   before:     the kinds built before the shared budget (0.106.0 to 0.149.0), each at its own rate, as the game has them now;
//   no budget:  every kind on, harm parts 4 and 5 too, each at its own rate (what they would add without the budget);
//   the budget: every kind on, each at its rate times its pass (HarmBudgetMath.Passes: the budget shared out, a kind that
//               came lately passing less often).
// The budget's line holds the total at the "before" line's within noise. Each kind's base rate in the registry is its
// "no budget" rate in the average colony of six (printed under it, for a later kind's builder to copy). Grown-ups only
// (the cap keeps children from all but the mildest), the usual setting and storyteller (the harm scale 1); Alpha Skills,
// Giddy-Up, Hospitality and Odyssey's fishing loaded (their kinds on), PokéWorld and Dubs Bad Hygiene's thirst not (its
// Pokémon spar and the dirty water off, as in most games).

using ColonyCouncil;

namespace Balance
{
    public static class HarmSim
    {
        private const int Days = HeartState.Days;

        private enum Mode
        {
            Before,
            NoBudget,
            Budget,
            /// <summary>The budget with its variety left out (every kind weighed as if it never came): what the variety does.</summary>
            BudgetPlain
        }

        /// <summary>A play style's exposure to each kind over a colony-year of 60 days (estimates; where each comes from is beside it).</summary>
        private sealed class Exposure
        {
            public string key;
            // ---- Part 1 (0.106.0) ----
            /// <summary>Days a drills decree holds (the militia's in a tribal or medieval colony, the drills after): a five-day decree in most years.</summary>
            public float drillDays;
            /// <summary>Days a work decree holds (long hours, a harvest push, a building season), and the share of colonists under 30% rest then.</summary>
            public float workDays, lowRest;
            /// <summary>A strained back or a sprained ankle per colonist-year (the report's ailments: about 0.4 strained-back days, four days each), and the share worked hard through untended.</summary>
            public float strains, workedThrough;
            /// <summary>Dangers on the map a colony-year (six of Rimfolk's 140-odd events at their share of the pool), and the share that end in a hurt by the answer chosen.</summary>
            public float dangers, dangerHurt;
            /// <summary>The ailments of the land and the work that are Rimfolk-made hurts, per colonist-year in a temperate colony (a swim's itch, rabbit fever, a smith's fume fever, trench foot, the serious three now and then).</summary>
            public float landAilments;
            // ---- Parts 2 and 3 ----
            /// <summary>Share of camps whose tension reaches 70 (takings and arrests; most stay under 40), and of camps with the old quarrel answered badly.</summary>
            public float campHigh, campFeud;
            /// <summary>Journeys to another people's town a colony-year, the share with medicine packed, and with the quarantine decree for those back.</summary>
            public float journeys, medicine, quarantine;
            /// <summary>Hunts a colony-year: answers' hunts and hunting parties (the calendar's hunting day is beside them).</summary>
            public float hunts;
            /// <summary>Rimfolk's own sharp words with a fight's chance a colony of six meets in a year (a near fight, an answer's fight at its chance), and hard words with an outsider.</summary>
            public float sharpWords, hardWords;
            // ---- Built since (0.134.0 to 0.149.0), now in the budget ----
            /// <summary>Feasts with guests a colony-year (diplomacy's feasts: a guest people's visit most years in a colony that hosts).</summary>
            public float feasts;
            // ---- Harm, part 4 ----
            /// <summary>Tendings of a catching ailment a colonist-year (the catching clock's everyday ailment every 60 days, two or three tendings a case, the flu now and then); a sterile room; a quarantine or rest-when-ill decree; a doctor past two tendings of the sick in a day.</summary>
            public float tendings, sterile, sickDecree, tired;
            /// <summary>Visits of Rimfolk's visitors a colony-year, and the share staying the night inside.</summary>
            public float visits, stayInside;
            /// <summary>Hours a colonist stays out in a falling hailstorm, by how quickly they are called in.</summary>
            public float hailHours;
            /// <summary>Days of the banked fires decree a colony-year (winter), and the share of a frail one's nights in a cold bedroom (no heater kept up).</summary>
            public float bankedDays, coldRoom;
            /// <summary>Trees felled a colonist-year by cutters under Plants 5.</summary>
            public float unskilledTrees;
            /// <summary>Constructions failed on heavy work (a wall, big furniture, a bridge) a colonist-year.</summary>
            public float heavyFails;
            /// <summary>Hours a colonist spends in a pen near a rutting male each rut (the herdmaster's work, or someone else's).</summary>
            public float penHours;
            /// <summary>Dismounts a colonist-year by riders short of Animals 6 on a mount not trained to obey.</summary>
            public float greenRides;
            /// <summary>A colony with two groups bitter with each other, and digs between them at a table a colony-year then.</summary>
            public float bitterGroups, digs;
            /// <summary>Hospitality's guest nights a colony-year (a guest group in the guest beds most weeks).</summary>
            public float hospitalityNights;
            /// <summary>The calendar's games day kept with a gathering a colony-year.</summary>
            public float gamesDays;
            // ---- Harm, part 5 ----
            /// <summary>Petitions to rest when ill or for a quarantine let lapse a colony-year while one of the colony works on with something catching.</summary>
            public float sickPetitions;
            /// <summary>The plague's gate shut at the world event's asking, and the quarantine decree then.</summary>
            public float gateShut, plagueQuarantine;
            /// <summary>The camp's sick one seen to by the colony's doctor (the camp's ask) in time.</summary>
            public float campTended;
            /// <summary>The colony's best doctor short of Medicine 8 (a healer's bad batch not caught).</summary>
            public float lowDoctor;
            /// <summary>Worms a colonist-year (raw meat or fish eaten), and the share left untended three days; insect bites left a day untended.</summary>
            public float worms, wormsUntended, bitesUntended;
            /// <summary>Journeys whose travellers wait at the gate past the hour (someone not ready), of the colony-year's.</summary>
            public float gateWaits;
            /// <summary>Night watches on the walls a colony-year, and the share of lone watchers outside the walls (no wall round the colony's ground).</summary>
            public float watches, unwalled;
            /// <summary>A weather lover's coat or hood.</summary>
            public float covered;
            /// <summary>Nightmares that leave someone shaken a colonist-year.</summary>
            public float shaken;
            /// <summary>Dusks a colonist spends at the height of restlessness, of the year's.</summary>
            public float restlessDusks;
            /// <summary>Hours a colonist fishes below freezing a colony-year.</summary>
            public float coldFishing;
        }

        private static readonly Exposure[] Exposures =
        {
            new Exposure
            {
                key = "well", drillDays = 3f, workDays = 5f, lowRest = 0.05f, strains = 0.12f, workedThrough = 0.15f, dangers = 0.4f, dangerHurt = 0.2f, landAilments = 0.12f,
                campHigh = 0.04f, campFeud = 0.03f, journeys = 1.5f, medicine = 0.8f, quarantine = 0.15f, hunts = 1.2f, sharpWords = 1f, hardWords = 0.5f, feasts = 0.8f,
                tendings = 0.7f, sterile = 0.4f, sickDecree = 0.3f, tired = 0.1f, visits = 4f, stayInside = 0.3f, hailHours = 0.1f, bankedDays = 3f, coldRoom = 0.2f,
                unskilledTrees = 3f, heavyFails = 0.3f, penHours = 1f, greenRides = 1f, bitterGroups = 0.1f, digs = 5f, hospitalityNights = 5f, gamesDays = 0.8f,
                sickPetitions = 0.05f, gateShut = 0.5f, plagueQuarantine = 0.15f, campTended = 0.8f, lowDoctor = 0.4f, worms = 0.03f, wormsUntended = 0.2f, bitesUntended = 0.3f,
                gateWaits = 0.15f, watches = 1f, unwalled = 0.3f, covered = 0.6f, shaken = 0.6f, restlessDusks = 0.03f, coldFishing = 3f
            },
            new Exposure
            {
                key = "avg", drillDays = 5f, workDays = 10f, lowRest = 0.15f, strains = 0.15f, workedThrough = 0.35f, dangers = 0.4f, dangerHurt = 0.3f, landAilments = 0.18f,
                campHigh = 0.12f, campFeud = 0.05f, journeys = 1.5f, medicine = 0.5f, quarantine = 0.05f, hunts = 1.5f, sharpWords = 1.5f, hardWords = 0.6f, feasts = 0.6f,
                tendings = 0.8f, sterile = 0.2f, sickDecree = 0.1f, tired = 0.25f, visits = 4f, stayInside = 0.35f, hailHours = 0.3f, bankedDays = 5f, coldRoom = 0.5f,
                unskilledTrees = 6f, heavyFails = 0.6f, penHours = 1.7f, greenRides = 2.5f, bitterGroups = 0.25f, digs = 6f, hospitalityNights = 6f, gamesDays = 0.8f,
                sickPetitions = 0.15f, gateShut = 0.25f, plagueQuarantine = 0.05f, campTended = 0.5f, lowDoctor = 0.6f, worms = 0.08f, wormsUntended = 0.5f, bitesUntended = 0.6f,
                gateWaits = 0.25f, watches = 1.5f, unwalled = 0.6f, covered = 0.4f, shaken = 0.8f, restlessDusks = 0.06f, coldFishing = 4f
            },
            new Exposure
            {
                key = "neglect", drillDays = 5f, workDays = 15f, lowRest = 0.35f, strains = 0.2f, workedThrough = 0.6f, dangers = 0.4f, dangerHurt = 0.45f, landAilments = 0.25f,
                campHigh = 0.25f, campFeud = 0.08f, journeys = 1f, medicine = 0.2f, quarantine = 0f, hunts = 1.2f, sharpWords = 2.5f, hardWords = 0.6f, feasts = 0.3f,
                tendings = 1f, sterile = 0.05f, sickDecree = 0f, tired = 0.5f, visits = 4f, stayInside = 0.35f, hailHours = 0.6f, bankedDays = 8f, coldRoom = 0.8f,
                unskilledTrees = 10f, heavyFails = 1f, penHours = 2.5f, greenRides = 5f, bitterGroups = 0.45f, digs = 7f, hospitalityNights = 6f, gamesDays = 0.6f,
                sickPetitions = 0.3f, gateShut = 0.1f, plagueQuarantine = 0f, campTended = 0.2f, lowDoctor = 0.8f, worms = 0.15f, wormsUntended = 0.8f, bitesUntended = 0.8f,
                gateWaits = 0.35f, watches = 2f, unwalled = 0.8f, covered = 0.2f, shaken = 1.2f, restlessDusks = 0.12f, coldFishing = 6f
            }
        };

        // ---- The same in every style ----

        /// <summary>Camps a colony-year (Camps/CampTimer.cs: 5% each open morning once 15 days have passed since the last; stays of 4 to 12 days).</summary>
        private const float CampsPerYear = 1.4f;
        /// <summary>Evenings a high-tension camp spends at 70 or more before it goes.</summary>
        private const int HighEveningsMin = 1, HighEveningsMax = 4;
        /// <summary>Someone of the colony at their fire of an evening (and the deeper hooks on, so the colony's side can come to blows).</summary>
        private const float ColonyAtFire = 0.6f;
        /// <summary>Another of the colony at the fire besides, each (who may join), and theirs free to join.</summary>
        private const float OthersAtFire = 0.08f;
        private const int TheirsFreeMin = 1, TheirsFreeMax = 4;
        /// <summary>A peacemaker in office and at home (Offices/).</summary>
        private const float Peacemaker = 0.4f;
        /// <summary>
        /// A journey's town under a plague or a famine (its people's, or one within twenty tiles): the region's world events
        /// come 1.0 to 1.2 a year, about a third of the plague, hunger or drought kinds, two or three weeks each, near one or
        /// two of the region's few towns. The plague share of those.
        /// </summary>
        private const float SickTown = 0.12f, PlagueShare = 0.5f;
        /// <summary>Hours a caravan stops at the town: a trade at the gate (under two), an afternoon, a night or more.</summary>
        private const float QuickShare = 0.4f, AfternoonShare = 0.35f;
        /// <summary>Industrial or spacer medicine (half of games).</summary>
        private const float Modern = 0.5f;
        /// <summary>Travellers on a journey.</summary>
        private const int TravellersMin = 2, TravellersMax = 4;
        /// <summary>A travelling champion's challenge a colony-year (and a rematch), and the share that are a bout of fighting (one skill of nine, more for a fighter known by name).</summary>
        private const float Challenges = 0.6f, BoutShare = 0.15f;
        /// <summary>The calendar's hunting day kept (a colony that put one on its calendar).</summary>
        private const float HuntingDay = 0.3f;
        /// <summary>A lone head of game that can turn among a hunt's (boars, elk and muffalo come in herds as often as alone).</summary>
        private const float LoneTurner = 0.35f;
        /// <summary>
        /// The game's own revenge chance for such a head (its kind's, 0.2 to 0.5 by the storyteller's factor), the game's
        /// distance factor at a hunter's range (3 at point blank to 1 at 30 cells: about 1.3 at 20 to 25), and the hits it
        /// takes before it's down, each a roll.
        /// </summary>
        private const float RevengeMin = 0.2f, RevengeMax = 0.5f, RangeFactor = 1.3f;
        private const int HitsMin = 2, HitsMax = 4;
        /// <summary>A turned beast wounds its hunter before it's shot down.</summary>
        private const float TurnedWounds = 0.65f;
        /// <summary>A pair of the colony who are rivals (Rates.RivalShare), bitterly (50 or more), and a petty or abrasive nature on one side (abrasive or jealous, not kind).</summary>
        private const float Bitter = 0.3f, Petty = 0.13f;
        /// <summary>The two at the same craft; a bench of the other's craft with its bills; the act done within its day; someone sees it; the two meet to have it out.</summary>
        private const float SameWork = 0.3f, Bench = 0.8f, Acts = 0.8f, Witness = 0.3f, Confronts = 0.9f;
        /// <summary>The election's hustings held (danger and weather call them off), heckles at them, the heckler backing another, the best-behavior decree, calm candidates.</summary>
        private const float HustingsHeld = 0.8f, BacksOther = 0.7f, Manners = 0.05f, CalmCandidates = 0.5f;
        private const int Heckles = 2;
        /// <summary>A sharp word's own chance of blows (a near fight's one in four, an answer's 0.2 to 0.5), and hard words with an outsider's.</summary>
        private const float SharpChance = 0.3f, HardChance = 0.2f;
        // ---- Built since (in the budget) ----
        /// <summary>The game's own herd migrations and Rimfolk's great herds stampeding a colony-year (BeastSim's 1.5), of a kind big enough to knock someone down, and one of the colony out in its way.</summary>
        private const float Stampedes = 1.5f, BigKind = 0.7f, InTheWay = 1f / 30f;
        /// <summary>A feast's wrestling bouts, its guest parties staying the night, and a guest's fighting temper (-1 to 3).</summary>
        private const int FeastBoutsMin = 1, FeastBoutsMax = 2, FeastPartiesMin = 1, FeastPartiesMax = 3;
        /// <summary>The pilgrims' night in the colony a colony-year (a people of release C at peace nearby, one of their bands passing; rare).</summary>
        private const float PilgrimNights = 0.2f;
        /// <summary>The cough caught by the one who sat up with them (PeoplesMath.CoughChance; the tests hold the two the same).</summary>
        private const float PilgrimCough = 0.1f;
        /// <summary>A home with a people bearing a grudge or a notable that cold within reach, and its schemes' fights a year then (SchemeSim: 0.16 to 0.21).</summary>
        private const float Schemer = 0.3f, SchemeFights = 0.18f;
        // ---- Harm, part 4 ----
        /// <summary>Of the catching tendings, the serious kind (tuberculosis, cholera, measles, the flu).</summary>
        private const float SeriousShare = 0.15f;
        /// <summary>A coughing guest: someone of the colony an hour near them, and the quarantine decree then.</summary>
        private const float CoughNear = 0.8f, CoughQuarantine = 0.05f;
        /// <summary>Hailstorms a colony-year (Council_Evt_Hail: spring to autumn, 30 days apart at least, among the nature events), and a helmet worn.</summary>
        private const float Hailstorms = 0.5f, Helmets = 0.2f;
        /// <summary>A hard frost on a banked fires night, and a frail one of the colony (old, a small child, frail or convalescent).</summary>
        private const float HardFrostNights = 0.4f, Frail = 0.15f;
        /// <summary>The colony's means: tribal, medieval, industrial, spacer, a quarter each.</summary>
        private static readonly WorldAge[] AgesAll = { WorldAge.Tribal, WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer };
        /// <summary>A heavy failure that is a wall or a bridge (the foot's crush), and the cap's room.</summary>
        private const float WallShare = 0.6f;
        /// <summary>A colony keeping a male of a herd's grazers penned with females (cows, sheep, muffalo, deer), and the herdmaster doing the pen's work themselves.</summary>
        private const float PennedMales = 0.5f;
        /// <summary>The herdmaster's lessons a colony-year (about once a week while there's a keen learner), and the share with a nervous animal handled beside the learner.</summary>
        private const float Lessons = 5f, NervousHandled = 0.4f;
        /// <summary>A herdmaster in office (offices on, five colonists or more).</summary>
        private const float Herdmaster = 0.6f;
        /// <summary>A colonist with Alpha Skills' drunken passion in cooking or crafting, and their drinks before hot work a year (at most one in eight hours, most work stretches).</summary>
        private const float DrunkenHot = 0.05f, HotDrinks = 25f;
        /// <summary>Contested seats at the year's election, a close race among them, a loser hot-tempered or bitter, a gracious winner who shook hands at the count.</summary>
        private const float Seats = 3f, Close = 0.4f, HotLoser = 0.3f, GraciousWinner = 0.4f;
        /// <summary>A guest's fighting temper, a drink at supper, the warden of guests in office, the best-behavior decree.</summary>
        private const float GuestDrink = 0.3f, Warden = 0.4f;
        /// <summary>Outsiders beaten by one of the colony in a duel or by a bout's hard blow a colony-year; a later visit of their people within the year; the colonist taking the rematch; a hurt in it (first blood, or a bout's blow).</summary>
        private const float Beaten = 0.5f, Revisit = 0.7f, Takes = 0.6f, RematchHurts = 0.45f;
        /// <summary>A games day's race on rough ground (most yards aren't paved), its runners and the bout pairs.</summary>
        private const float RoughCourse = 0.7f;
        // ---- Harm, part 5 ----
        /// <summary>Visitors' and traders' groups at the gate a colony-year (the game's and Rimfolk's), and the share whose people a plague lies over (SickTown's region, plague kind only).</summary>
        private const float GateGroups = 6f, GroupPlague = 0.06f;
        /// <summary>Couriers to the colony a colony-year (goods that come and go), and of a people under a plague.</summary>
        private const float CourierTrips = 3f, CourierPlague = 0.06f;
        /// <summary>Someone of the colony an hour near a carrier at the gate (the one who trades), a courier (who unloads), at the camp's fire in its first three days.</summary>
        private const float NearGate = 0.85f, NearCourier = 0.6f, NearCamp = 0.5f;
        /// <summary>A camp's trouble that is one of theirs falling ill.</summary>
        private const float CampSickShare = 0.3f;
        /// <summary>Hospital patients a colony-year, and a colonist asleep in the same room as one for a night.</summary>
        private const float Patients = 0.5f, SharedRoom = 0.2f;
        /// <summary>The travelling healer's visit a colony-year, and the colony paying for the doses.</summary>
        private const float HealerVisits = 0.5f, HealerPaid = 0.5f;
        /// <summary>A malaria country, and insect bites a colonist-year there; sleeves or a bed indoors.</summary>
        private const float MalariaLand = 0.25f, Bites = 1f, Netted = 0.6f;
        /// <summary>A hard frost when a journey waits at the gate past the hour, and the cold hours of it (two to four).</summary>
        private const float GateFrost = 0.15f;
        /// <summary>A night watch in a hard frost, its watchers (one to three), and a hungry predator in reach of a lone one.</summary>
        private const float WatchFrost = 0.2f, HungryPredator = 0.15f;
        /// <summary>Those who stand in the rain and those who love the snow, and their days out in it a colony-year (rain; falling snow in bitter cold).</summary>
        private const float RainLovers = 0.02f, SnowLovers = 0.01f, RainDays = 8f, SnowDays = 3f;
        /// <summary>A sleepwalk kept in (a bolted door, a roommate), and a cold night out (frostnip) of the rest.</summary>
        private const float KeptIn = 0.3f, ColdNightOut = 0.25f;
        /// <summary>A friend free to go along on a restless walk.</summary>
        private const float FriendAlong = 0.4f;
        /// <summary>Of cold fishing hours, those in a hard frost (twice the slips).</summary>
        private const float HardFrostFishing = 0.4f;

        // ---- The game's own, beside them ----

        /// <summary>A raid's wound for a colonist at home (HeartSim's model: in it four times in five, hurt one time in three of those).</summary>
        private const float RaidWound = 0.8f * 0.3f;

        /// <summary>The kinds the model has, in the registry's order (PokéWorld's spar and Dubs Bad Hygiene's dirty water left out: the model's game hasn't them).</summary>
        private static readonly HarmKind[] Modelled = HarmKinds.All.Where(k => k != HarmKinds.PokemonSpar && k != HarmKinds.DirtyWater).ToArray();

        private sealed class Person
        {
            public readonly HeartState heart = new HeartState();
            public int lastHurt = int.MinValue / 2;
            public bool tough;
        }

        private sealed class Tally
        {
            public int runs, colonists;
            public readonly Dictionary<string, double> hurts = new Dictionary<string, double>(), barred = new Dictionary<string, double>(), fights = new Dictionary<string, double>();
            public double wounds, hours, episodes, mood, raidWounds, passSum, passDays, repeats;
            public readonly Dictionary<string, double> passes = new Dictionary<string, double>();
            public readonly double[] bands = new double[4];
        }

        private sealed class Year
        {
            public Exposure x;
            public HeartLife life;
            public int n, hour, lastColonyEpisode = -999999;
            public Mode mode;
            public List<Person> people;
            public Tally t;
            /// <summary>Each source's own generator: the lives and each kind draw the same in every mode.</summary>
            public Dictionary<string, Random> rngs;
            /// <summary>The day's events, by hour.</summary>
            public readonly List<(int hour, Action act)> today = new List<(int, Action)>();
            // A camp under way: the days it stays, its quarrelling evenings, a brawl already this stay.
            public int campUntil = -1, campGap = -1;
            public HashSet<int> quarrelDays = new HashSet<int>();
            public bool campBrawled;
            // Bitter rivalries one way (the rival, the other, petty, at the same work) and the colony's peacemaker in office.
            public List<(Person rival, Person other, bool petty, bool same)> rivals = new List<(Person, Person, bool, bool)>();
            public bool peacemaker, herdmaster, penned, bitterGroups;
            public WorldAge age;
            public int electionDay;
            // Sicknesses carried home or caught: who, the day it shows, the kind (a lapse past three more days held back).
            public List<(Person p, int day, HarmKind kind)> onsets = new List<(Person, int, HarmKind)>();
            // The shared budget: each kind's last hurt (in the year's hours) and the passes now.
            public readonly Dictionary<HarmKind, int> last = new Dictionary<HarmKind, int>();
            public readonly float[] pass = new float[Modelled.Length];
            public readonly float[] since = new float[Modelled.Length];
            public bool passDirty = true;
        }

        public static void Run(string[] args)
        {
            int runs = 1500, seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--runs") runs = int.Parse(args[++i]);
                if (args[i] == "--seed") seed = int.Parse(args[++i]);
            }
            Console.WriteLine($"Hurts from everyday life: a colony-year ({Days} days), {runs} runs each, the setting at 100% and the usual storyteller (harm scale 1).");
            Console.WriteLine("Hurts are what the shared cap counts (a wound, an ailment, a fight's two, a bout's real blow, a hunt's game turning on its hunter),");
            Console.WriteLine($"per colonist-year; the cap holds one back if they had one in the last {HarmMath.GapDays:0} days. \"before\": the kinds built before the shared");
            Console.WriteLine("budget, each at its own rate (the game as it was); \"no budget\": every kind on, harm parts 4 and 5 too, each at its own rate;");
            Console.WriteLine("\"budget\": every kind on, each at its rate times its pass within the shared budget (a kind that came lately passing less often).");
            Console.WriteLine();
            var registry = new Dictionary<string, double>();
            foreach (var x in Exposures)
                foreach (int n in new[] { 6, 12 })
                {
                    var before = Play(x, n, Mode.Before, runs, seed);
                    var none = Play(x, n, Mode.NoBudget, runs, seed);
                    var budget = Play(x, n, Mode.Budget, runs, seed);
                    Report(x, n, before, none, budget);
                    if (x.key == "avg" && n == 6) VarietyReport(budget, Play(x, n, Mode.BudgetPlain, runs, seed));
                    if (x.key == "avg" && n == 6)
                        foreach (var k in Modelled)
                        {
                            // Its own rate before the shared cap holds any back: what it brings, the kinds beside it left out.
                            var at = k.founding ? before : none;
                            registry[k.key] = (at.hurts.GetValueOrDefault(k.label) + at.barred.GetValueOrDefault(k.label)) / at.colonists;
                        }
                }
            Console.WriteLine("== The registry's base rates (HarmKinds, weight) beside the model's own (an average colony of six, each kind at its own rate, before the cap):");
            foreach (var k in Modelled)
            {
                double m = registry.GetValueOrDefault(k.key);
                string flag = Math.Abs(m - k.weight) > Math.Max(0.002, 0.25 * Math.Max(m, k.weight)) ? "   <- update the registry" : "";
                Console.WriteLine($"   {k.label,-48} registry {k.weight,6:0.0000}  model {m,6:0.0000}{flag}");
            }
            Console.WriteLine($"   the budget (the founding kinds the model has): {HarmBudgetMath.Budget(Modelled):0.000}; every kind on, without it: {HarmBudgetMath.Unbudgeted(Modelled):0.000};");
            Console.WriteLine($"   so each kind passes at about {HarmBudgetMath.Budget(Modelled) / HarmBudgetMath.Unbudgeted(Modelled):P0} of its own rate, less just after it came and more while others rest.");
            Console.WriteLine();
        }

        private static Tally Play(Exposure x, int n, Mode mode, int runs, int seed)
        {
            var t = new Tally();
            var life = HeartState.For(x.key);
            for (int r = 0; r < runs; r++)
            {
                var y = new Year { x = x, life = life, n = n, mode = mode, t = t, rngs = new Dictionary<string, Random>() };
                int baseSeed = seed * 7919 + r * 31 + n;
                Random Rng(string name)
                {
                    if (!y.rngs.TryGetValue(name, out var g)) y.rngs[name] = g = new Random(baseSeed * 131 + Stable(name));
                    return g;
                }
                var lifeRng = new Random(baseSeed);
                var setup = Rng("setup");
                y.people = Enumerable.Range(0, n).Select(_ => new Person { tough = setup.NextDouble() < 0.07 }).ToList();
                // The heart's good side on, as by default, with its own draws.
                foreach (var p in y.people)
                {
                    p.heart.goodOn = true;
                    p.heart.goodRng = new Random(baseSeed * 17 + y.people.IndexOf(p));
                }
                y.peacemaker = setup.NextDouble() < Peacemaker;
                y.electionDay = 1 + setup.Next(Days - 1);
                for (int a = 0; a < n; a++)
                    for (int b = a + 1; b < n; b++)
                    {
                        if (setup.NextDouble() >= Rates.RivalShare * Bitter) continue;
                        bool same = setup.NextDouble() < SameWork;
                        y.rivals.Add((y.people[a], y.people[b], setup.NextDouble() < Petty, same));
                        y.rivals.Add((y.people[b], y.people[a], setup.NextDouble() < Petty, same));
                    }
                // Part 4's colony, drawn apart so the rest of the setup is the same as before.
                var four = Rng("four setup");
                y.herdmaster = four.NextDouble() < Herdmaster;
                y.penned = four.NextDouble() < PennedMales;
                y.bitterGroups = four.NextDouble() < x.bitterGroups;
                y.age = AgesAll[four.Next(AgesAll.Length)];
                y.rngs["life"] = lifeRng;
                Rng("pain");
                PlayYear(y, Rng);
                t.runs++;
                t.colonists += n;
            }
            return t;
        }

        private static int Stable(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }

        private static void PlayYear(Year y, Func<string, Random> rng)
        {
            var life = y.life;
            var lifeRng = rng("life");
            for (int day = 0; day < Days; day++)
            {
                bool raid = lifeRng.NextDouble() < life.raid, death = lifeRng.NextDouble() < life.deathsPerYear / Days;
                int raidHour = lifeRng.Next(6, 20), deathHour = lifeRng.Next(0, 24);
                bool party = lifeRng.NextDouble() < life.party;
                foreach (var p in y.people) p.heart.StartDay(life, lifeRng, day, raid, raidHour, death, deathHour);
                if (raid) y.t.raidWounds += y.n * RaidWound;
                y.today.Clear();
                y.passDirty = true;
                PartOneDay(y, day, rng);
                // The day's frayed tempers roll in every mode (only after "before" does the strain count).
                Frayed(y, rng);
                PartTwoDay(y, day, rng);
                SinceDay(y, day, rng);
                if (y.mode != Mode.Before) PartFourDay(y, day, rng);
                if (y.mode != Mode.Before) PartFiveDay(y, day, rng);
                Onsets(y, day, rng);
                if (y.mode == Mode.Budget || y.mode == Mode.BudgetPlain)
                {
                    Passes(y);
                    for (int i = 0; i < Modelled.Length; i++) y.t.passes[Modelled[i].key] = y.t.passes.GetValueOrDefault(Modelled[i].key) + y.pass[i];
                    y.t.passDays++;
                }
                for (int h = 0; h < 24; h++)
                {
                    foreach (var e in y.today.Where(e => e.hour == h).ToList()) e.act();
                    foreach (var p in y.people)
                    {
                        if (p.heart.Hour(life, lifeRng, day, h, party, y.hour, ref y.lastColonyEpisode)) y.t.episodes++;
                        var band = HeartMath.BandFor(p.heart.strain);
                        y.t.bands[(int)band]++;
                        y.t.mood += HeartMath.MoodOffset(band);
                        y.t.hours++;
                    }
                    y.hour++;
                }
            }
        }

        // ---------------- The shared budget ----------------

        /// <summary>The kind's pass now: 1 before the budget and without it; else HarmBudgetMath's share for it.</summary>
        private static float P(Year y, HarmKind k)
        {
            if (y.mode != Mode.Budget && y.mode != Mode.BudgetPlain) return 1f;
            Passes(y);
            int i = Array.IndexOf(Modelled, k);
            return i >= 0 ? y.pass[i] : 1f;
        }

        private static void Passes(Year y)
        {
            if (!y.passDirty) return;
            y.passDirty = false;
            for (int i = 0; i < Modelled.Length; i++)
                y.since[i] = y.mode != Mode.BudgetPlain && y.last.TryGetValue(Modelled[i], out int at) ? (y.hour - at) / 24f : HarmBudgetMath.LongAgo;
            HarmBudgetMath.Passes(Modelled, y.since, y.pass);
        }

        /// <summary>A kind's chance of coming, with its pass: whether it comes.</summary>
        private static bool Comes(Year y, HarmKind k, Random g, double chance) => g.NextDouble() < chance * P(y, k);

        // ---------------- The hurt, through the cap ----------------

        private static void Add(Dictionary<string, double> d, string key, double v = 1) => d[key] = d.GetValueOrDefault(key) + v;

        /// <summary>The cap lets them be hurt now (one in three days).</summary>
        private static bool May(Year y, Person p) => y.hour - p.lastHurt >= HarmMath.GapDays * 24;

        private static void Came(Year y, HarmKind k)
        {
            if (y.last.TryGetValue(k, out int before) && y.hour - before < HarmBudgetMath.VarietyDays * 24) y.t.repeats++;
            y.last[k] = y.hour;
            y.passDirty = true;
        }

        /// <summary>A Rimfolk-made hurt (counted for the cap and its kind's turn), its pain on the heart when it wounds; false when the cap held it back.</summary>
        private static bool Hurt(Year y, Person p, HarmKind k, bool wounds = true)
        {
            if (p == null) return false;
            if (!May(y, p))
            {
                Add(y.t.barred, k.label);
                return false;
            }
            p.lastHurt = y.hour;
            Add(y.t.hurts, k.label);
            Came(y, k);
            if (wounds)
            {
                y.t.wounds++;
                p.heart.Build(StrainSource.Pain, 2f + (float)y.rngs["pain"].NextDouble() * 4f, "hurts");
            }
            return true;
        }

        /// <summary>A fight between two: an hour of combat and a bruise's pain for each; under the cap when it is a kind's (both counted), or plain.</summary>
        private static bool Fight(Year y, Person a, Person b, HarmKind k, string plain = null)
        {
            bool capped = k != null;
            string label = k?.label ?? plain;
            if (capped && (!May(y, a) || b != null && !May(y, b)))
            {
                Add(y.t.barred, label, b != null ? 2 : 1);
                return false;
            }
            Add(y.t.fights, label);
            if (capped) Came(y, k);
            foreach (var p in new[] { a, b })
            {
                if (p == null) continue;
                if (capped)
                {
                    p.lastHurt = y.hour;
                    Add(y.t.hurts, label);
                }
                p.heart.Build(StrainSource.Combat, 4f, "fights");
                p.heart.Build(StrainSource.Pain, 1f + (float)y.rngs["pain"].NextDouble() * 2f, "fights");
            }
            return true;
        }

        private static Person Any(Year y, Random g, Person not = null)
        {
            var pool = not == null ? y.people : y.people.Where(p => p != not).ToList();
            return pool.Count == 0 ? null : pool[g.Next(pool.Count)];
        }

        private static float Factor(Year y, Person p) => p == null ? 1f : HarmTwoMath.FrayedFactor(HeartMath.BandFor(p.heart.strain), P(y, HarmKinds.Frayed));

        // ---------------- Part 1 (0.106.0) ----------------

        private static void PartOneDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // Drills that bruise: the evening's bouts while a drills decree holds (the militia's one pair three evenings in four in half the colonies).
            var gd = rng("drills");
            if (gd.NextDouble() < x.drillDays / Days)
            {
                bool muster = gd.NextDouble() < 0.5, marshal = gd.NextDouble() < 0.4;
                int pairs = muster ? (HarmMath.MusterEvening(day) ? 1 : 0) : HarmMath.DrillPairs((int)(y.n * 0.7f), false);
                for (int i = 0; i < pairs; i++)
                {
                    if (!Comes(y, HarmKinds.Drills, gd, HarmMath.DrillBlow(marshal))) continue;
                    var p = Any(y, gd);
                    y.today.Add((18, () => Hurt(y, p, HarmKinds.Drills)));
                }
            }
            // Tired hands: a long day at the work a work decree drives, one roll each (half of them work such a day).
            var gw = rng("work");
            if (gw.NextDouble() < x.workDays / Days)
                foreach (var p in y.people)
                {
                    if (gw.NextDouble() >= 0.5) continue;
                    bool low = gw.NextDouble() < x.lowRest;
                    if (Comes(y, HarmKinds.TiredHands, gw, HarmMath.TiredHands(1, low, p.tough, false))) y.today.Add((15, () => Hurt(y, p, HarmKinds.TiredHands)));
                }
            // Working through it: a strain or a sprain worked hard through untended turns worse one time in ten.
            var gt = rng("through");
            foreach (var p in y.people)
                if (gt.NextDouble() < x.strains / Days * x.workedThrough && Comes(y, HarmKinds.WorkedThrough, gt, HarmMath.WorkedThroughChance))
                    y.today.Add((14, () => Hurt(y, p, HarmKinds.WorkedThrough)));
            // Dangers on the map: one of Rimfolk's events, its hurt by the answer.
            var gm = rng("dangers");
            if (gm.NextDouble() < x.dangers / Days && Comes(y, HarmKinds.Dangers, gm, x.dangerHurt))
            {
                var p = Any(y, gm);
                y.today.Add((12, () => Hurt(y, p, HarmKinds.Dangers)));
            }
            // The ailments of the land and the work that are Rimfolk-made hurts (they ask the cap before they come).
            var ga = rng("ailments");
            foreach (var p in y.people)
                if (Comes(y, HarmKinds.LandAilments, ga, x.landAilments / Days))
                    y.today.Add((10, () => Hurt(y, p, HarmKinds.LandAilments)));
        }

        // ---------------- Frayed tempers (3.3.1) ----------------

        /// <summary>Rimfolk's own sharp words with a fight's chance: the strain's part only after (the cap for both), the rest a fight as before.</summary>
        private static void Frayed(Year y, Func<string, Random> rng)
        {
            var g = rng("frayed");
            if (g.NextDouble() < y.x.sharpWords * y.n / 6f / Days)
            {
                var a = Any(y, g);
                var b = Any(y, g, a);
                float roll = (float)g.NextDouble();
                y.today.Add((13, () => Words(y, a, b, roll, SharpChance)));
            }
            if (g.NextDouble() < y.x.hardWords / Days)
            {
                var c = Any(y, g);
                float roll = (float)g.NextDouble();
                y.today.Add((16, () => Words(y, c, null, roll, HardChance)));
            }
        }

        private static void Words(Year y, Person a, Person b, float roll, float chance)
        {
            float factor = HarmTwoMath.Frayed(Factor(y, a), Factor(y, b));
            if (!HarmTwoMath.FrayedRoll(roll, chance, factor, out bool frayed)) return;
            if (!frayed)
            {
                Fight(y, a, b, null, "Rimfolk's moments, as before");
                return;
            }
            Fight(y, a, b, HarmKinds.Frayed);
        }

        // ---------------- Parts 2 and 3 ----------------

        private static void PartTwoDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // A camp's quarrel that becomes a brawl: an evening's one roll while it runs high or soured, once a stay.
            var gc = rng("camp");
            if (y.campUntil < day && y.campGap < day && gc.NextDouble() < CampsPerYear / Days)
            {
                int stay = gc.Next(4, 13);
                y.campUntil = day + stay;
                y.campGap = y.campUntil + 15;
                y.campBrawled = false;
                y.quarrelDays.Clear();
                if (gc.NextDouble() < x.campHigh)
                {
                    int evenings = gc.Next(HighEveningsMin, HighEveningsMax + 1);
                    for (int i = 0; i < evenings; i++) y.quarrelDays.Add(y.campUntil - i);
                }
                if (gc.NextDouble() < x.campFeud)
                {
                    int from = day + gc.Next(1, Math.Max(2, stay - 2));
                    for (int i = 0; i < HarmTwoMath.SourQuarrelDays; i++) y.quarrelDays.Add(from + i);
                }
            }
            if (day <= y.campUntil && y.quarrelDays.Contains(day) && !y.campBrawled)
                y.today.Add((19, () => CampEvening(y, gc)));

            // Sickness brought home: a journey's one roll, the sickness showing two to four days after.
            var gs = rng("sick");
            if (gs.NextDouble() < x.journeys / Days && gs.NextDouble() < SickTown)
            {
                double h = gs.NextDouble();
                float hours = h < QuickShare ? 1f : h < QuickShare + AfternoonShare ? 2f + (float)gs.NextDouble() * 6f : 8f + (float)gs.NextDouble() * 16f;
                var kind = gs.NextDouble() < PlagueShare ? ColonyCouncil.SickTown.Plague : ColonyCouncil.SickTown.Famine;
                float chance = HarmTwoMath.SickHome(kind, hours, gs.NextDouble() < x.medicine, gs.NextDouble() < Modern, gs.NextDouble() < x.quarantine);
                int travellers = gs.Next(TravellersMin, TravellersMax + 1);
                var who = y.people.OrderBy(_ => gs.Next()).Take(travellers).ToList();
                int back = day + gs.Next(3, 9);
                if (Comes(y, HarmKinds.SickHome, gs, chance))
                {
                    var p = who[gs.Next(who.Count)];
                    y.onsets.Add((p, back + (int)(HarmTwoMath.OnsetTicks((float)gs.NextDouble()) / HarmTwoMath.DayTicks), HarmKinds.SickHome));
                }
            }

            // The champion's bout: one bout in 25 lands a real blow, on either of the two.
            var gb = rng("champion");
            if (gb.NextDouble() < Challenges * BoutShare / Days)
            {
                var p = Any(y, gb);
                y.today.Add((15, () =>
                {
                    if (!May(y, p)) return;
                    if (!Comes(y, HarmKinds.ChampionBout, gb, HarmTwoMath.ChampionBlowChance)) return;
                    if (gb.NextDouble() < 0.5) Hurt(y, p, HarmKinds.ChampionBout);
                }));
            }

            // Hunts that turn: a lone beast that can turn among the game, the game's own chance with each hit.
            var gh = rng("hunt");
            // Its turn within the budget is one roll a hunt, at the marking (HuntsBite.Marked).
            if (gh.NextDouble() < (x.hunts + HuntingDay) / Days && gh.NextDouble() < LoneTurner && gh.NextDouble() < P(y, HarmKinds.HuntTurn))
            {
                var p = Any(y, gh);
                y.today.Add((10, () =>
                {
                    // Never on a hunter the cap keeps from a hurt (their own shot at it not counted as fighting).
                    if (!May(y, p))
                    {
                        Add(y.t.barred, HarmKinds.HuntTurn.label);
                        return;
                    }
                    float revenge = Math.Min(1f, (RevengeMin + (float)gh.NextDouble() * (RevengeMax - RevengeMin)) * RangeFactor);
                    float each = HarmTwoMath.TurnChance(revenge, 1f, true);
                    int hits = gh.Next(HitsMin, HitsMax + 1);
                    bool turned = false;
                    for (int i = 0; i < hits && !turned; i++) turned = gh.NextDouble() < each;
                    if (!turned) return;
                    Add(y.t.fights, "a hunt's game turning on its hunter");
                    Hurt(y, p, HarmKinds.HuntTurn, gh.NextDouble() < TurnedWounds);
                }));
            }

            // A rival's sabotage, found out: a day's chance each way for a bitter rivalry of a petty nature.
            var gr = rng("sabotage");
            foreach (var (rival, other, petty, same) in y.rivals)
            {
                float chance = HarmTwoMath.SabotageChance(60f, petty, same, y.peacemaker);
                if (chance <= 0f || !Comes(y, HarmKinds.Sabotage, gr, chance)) continue;
                if (gr.NextDouble() >= Bench * Acts) continue;
                bool seen = gr.NextDouble() < Witness;
                if (gr.NextDouble() >= HarmTwoMath.FoundOut(seen)) continue;
                if (gr.NextDouble() >= Confronts) continue;
                y.today.Add((17, () =>
                {
                    if (!Fight(y, other, rival, HarmKinds.Sabotage)) Add(y.t.fights, "a sabotage had out in words");
                }));
            }

            // Hustings that come to blows: the first heckle with a rival among the speaker's supporters, once a hustings.
            var gu = rng("hustings");
            if (day == y.electionDay && gu.NextDouble() < HustingsHeld)
            {
                double fans = Math.Max(1.0, y.n / 3.0);
                double q = (1.0 - Math.Pow(1.0 - Rates.RivalShare, fans)) * BacksOther;
                if (gu.NextDouble() < 1.0 - Math.Pow(1.0 - q, Heckles))
                {
                    var a = Any(y, gu);
                    var b = Any(y, gu, a);
                    bool calm = gu.NextDouble() < CalmCandidates, manners = gu.NextDouble() < Manners, pm = gu.NextDouble() < Peacemaker;
                    y.today.Add((19, () =>
                    {
                        float chance = HarmTwoMath.HustingsBrawl(pm, manners, calm, HarmTwoMath.Frayed(Factor(y, a), Factor(y, b)));
                        if (Comes(y, HarmKinds.Hustings, gu, chance)) Fight(y, a, b, HarmKinds.Hustings);
                    }));
                }
            }
        }

        /// <summary>An evening at a camp's fire while the quarrel can come to blows: one roll, the colony's side if one of them is there.</summary>
        private static void CampEvening(Year y, Random g)
        {
            if (y.campBrawled) return;
            bool colony = g.NextDouble() < ColonyAtFire;
            var at = colony ? y.people.Where(p => May(y, p)).OrderBy(_ => g.Next()).ToList() : new List<Person>();
            if (colony && at.Count == 0) colony = false;
            float frayed = colony ? at.Take(1 + y.people.Count(_ => g.NextDouble() < OthersAtFire)).Max(p => Factor(y, p)) : 1f;
            if (!Comes(y, HarmKinds.CampBrawl, g, HarmTwoMath.CampBrawlChance(y.peacemaker, frayed))) return;
            y.campBrawled = true;
            if (!colony)
            {
                Add(y.t.fights, "a camp's brawl among themselves");
                return;
            }
            var first = at[0];
            int others = y.people.Count(p => p != first && May(y, p) && g.NextDouble() < OthersAtFire);
            int join = HarmTwoMath.JoinPairs(others, g.Next(TheirsFreeMin, TheirsFreeMax + 1));
            foreach (var p in new[] { first }.Concat(at.Skip(1).Take(join)))
                Fight(y, p, null, HarmKinds.CampBrawl);
        }

        // ---------------- Built since (0.134.0 to 0.149.0) ----------------

        private static void SinceDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // A stampede: a herd of a big kind spooked, someone of the colony out in its way, knocked down by the game's chance.
            var gp = rng("stampede");
            if (gp.NextDouble() < Stampedes / Days && gp.NextDouble() < BigKind)
            {
                int down = 0;
                foreach (var p in y.people)
                {
                    if (down >= BeastsMath.MaxTrampled || gp.NextDouble() >= InTheWay) continue;
                    if (!Comes(y, HarmKinds.Stampede, gp, BeastsMath.TrampleChance(2.4f, 1f))) continue;
                    var who = p;
                    down++;
                    y.today.Add((11, () => Hurt(y, who, HarmKinds.Stampede)));
                }
            }
            // A feast with guests: its wrestling (one in 25 a hard blow, on either of the two) and its guests' night (a sour night with one of ours).
            var gf = rng("feast");
            if (gf.NextDouble() < x.feasts / Days)
            {
                int bouts = gf.Next(FeastBoutsMin, FeastBoutsMax + 1);
                for (int i = 0; i < bouts; i++)
                {
                    var p = Any(y, gf);
                    if (Comes(y, HarmKinds.FeastBout, gf, FeastMath.HardBlowChance) && gf.NextDouble() < 0.5) y.today.Add((15, () => Hurt(y, p, HarmKinds.FeastBout)));
                }
                int parties = gf.Next(FeastPartiesMin, FeastPartiesMax + 1);
                for (int i = 0; i < parties; i++)
                {
                    var p = Any(y, gf);
                    if (Comes(y, HarmKinds.FeastSour, gf, FeastMath.SourNightChance(gf.Next(-1, 4), false))) y.today.Add((21, () => Hurt(y, p, HarmKinds.FeastSour, false)));
                }
            }
            // The pilgrims' night: the one who sat up with them may catch their cough.
            var gi = rng("pilgrims");
            if (gi.NextDouble() < PilgrimNights / Days && Comes(y, HarmKinds.PilgrimCough, gi, PilgrimCough))
            {
                var p = Any(y, gi);
                y.today.Add((7, () => Hurt(y, p, HarmKinds.PilgrimCough)));
            }
            // A scheme's fight: a lie in a hot temper's ear, had out with another of the colony.
            var gm = rng("schemes");
            if (gm.NextDouble() < Schemer * SchemeFights / Days * P(y, HarmKinds.SchemeFight))
            {
                var a = Any(y, gm);
                var b = Any(y, gm, a);
                y.today.Add((14, () => Fight(y, a, b, HarmKinds.SchemeFight)));
            }
        }

        // ---------------- Harm, part 4 ----------------

        private static void PartFourDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // The physician catches what they tend: a tending of a catching ailment, the doctor's chance, showing a day or two after.
            var gd = rng("physician");
            if (gd.NextDouble() < x.tendings * y.n / Days)
            {
                bool serious = gd.NextDouble() < SeriousShare;
                float chance = HarmFourMath.PhysicianChance(serious, gd.NextDouble() < Modern, gd.NextDouble() < x.sterile, gd.NextDouble() < x.sickDecree,
                    gd.NextDouble() < x.tired ? 3 : 1);
                var doctor = Any(y, gd);
                if (Comes(y, HarmKinds.Physician, gd, chance)) y.onsets.Add((doctor, day + 1 + gd.Next(0, 2), HarmKinds.Physician));
            }
            // The guest's cough: a visit in 20 coughs; whoever sat with them longest catches it a day after they go.
            var gc = rng("cough");
            if (gc.NextDouble() < x.visits / Days && Comes(y, HarmKinds.GuestCough, gc, HarmFourMath.GuestCoughVisit)
                && gc.NextDouble() < CoughNear * (gc.NextDouble() < CoughQuarantine ? 0.25 : 1))
                y.onsets.Add((Any(y, gc), day + 2, HarmKinds.GuestCough));
            // Caught in the hail: each colonist's hours out under it, a stone's chance each 250 ticks, two a storm at most.
            var gh = rng("hail");
            if (gh.NextDouble() < Hailstorms / Days)
            {
                int bruised = 0;
                foreach (var p in y.people)
                {
                    if (gh.NextDouble() < Helmets) continue;
                    double ticks = gh.NextDouble() * 2 * x.hailHours * 10;
                    for (int k = 0; k < (int)Math.Round(ticks) && bruised < HarmFourMath.HailMaxPerStorm; k++)
                    {
                        if (!Comes(y, HarmKinds.Hail, gh, HarmFourMath.HailChance(false, false))) continue;
                        bruised++;
                        var who = p;
                        y.today.Add((13, () => Hurt(y, who, HarmKinds.Hail)));
                        break;
                    }
                }
            }
            // A cold night for the frail: banked fires in a hard frost, a frail one in a cold bedroom, one night in seven.
            var gn = rng("cold");
            if (gn.NextDouble() < x.bankedDays / Days && gn.NextDouble() < HardFrostNights)
                foreach (var p in y.people)
                    if (gn.NextDouble() < Frail && gn.NextDouble() < x.coldRoom && Comes(y, HarmKinds.ColdNight, gn, HarmFourMath.ColdNightChance))
                    {
                        var who = p;
                        y.today.Add((7, () => Hurt(y, who, HarmKinds.ColdNight)));
                    }
            // A felling mishap: a tree felled by an unskilled cutter, one in 300 by the colony's means.
            var gf = rng("felling");
            foreach (var p in y.people)
                if (gf.NextDouble() < x.unskilledTrees / Days && Comes(y, HarmKinds.Felling, gf, HarmFourMath.Felling(0, y.age)))
                {
                    var who = p;
                    y.today.Add((11, () => Hurt(y, who, HarmKinds.Felling)));
                }
            // A failed build that hurts: a heavy construction failing on its builder, one in ten.
            var gb = rng("build");
            foreach (var p in y.people)
                if (gb.NextDouble() < x.heavyFails / Days && Comes(y, HarmKinds.FailedBuild, gb, HarmFourMath.FailedBuild(gb.NextDouble() < WallShare ? HeavyBuild.Wall : HeavyBuild.Furniture)))
                {
                    var who = p;
                    y.today.Add((12, () => Hurt(y, who, HarmKinds.FailedBuild)));
                }
            // Burned at the bench: a drink before hot work, one in 100 (Alpha Skills' drunken passion, few have it).
            var gw = rng("hot bench");
            foreach (var p in y.people)
                if (gw.NextDouble() < DrunkenHot * HotDrinks / Days && Comes(y, HarmKinds.HotBench, gw, HarmFourMath.HotBenchChance))
                {
                    var who = p;
                    y.today.Add((10, () => Hurt(y, who, HarmKinds.HotBench)));
                }
            // The rut in the pens: autumn's fifteen days, hours in a rutting male's pen, one charge in twenty hours, half landing.
            var gr = rng("rut");
            if (y.penned && day >= 30 && day < 45)
                foreach (var p in y.people)
                {
                    double hours = x.penHours / 15.0;
                    float charge = HarmFourMath.RutCharge(y.herdmaster && gr.NextDouble() < 0.3, 5);
                    if (gr.NextDouble() < hours * charge * P(y, HarmKinds.Rut) && gr.NextDouble() < HarmFourMath.RutLands)
                    {
                        var who = p;
                        y.today.Add((9, () => Hurt(y, who, HarmKinds.Rut)));
                    }
                }
            // The herdmaster's lesson: a nervous animal handled beside the learner, one lesson in 30.
            var gl = rng("lesson");
            if (y.herdmaster && gl.NextDouble() < Lessons / Days && gl.NextDouble() < NervousHandled && Comes(y, HarmKinds.HerdLesson, gl, HarmFourMath.LessonKickChance))
            {
                var p = Any(y, gl);
                y.today.Add((10, () => Hurt(y, p, HarmKinds.HerdLesson)));
            }
            // A fall from the saddle: a dismount by a rider short of Animals 6 on a green mount, one in 50 (Giddy-Up).
            var gs = rng("saddle");
            foreach (var p in y.people)
                if (gs.NextDouble() < x.greenRides / Days && Comes(y, HarmKinds.Saddle, gs, HarmFourMath.SaddleThrow(3, false)))
                {
                    var who = p;
                    y.today.Add((16, () => Hurt(y, who, HarmKinds.Saddle)));
                }
            // The groups come to blows: a dig at a table between two bitter groups, one in eight, others joining.
            var gg = rng("groups");
            if (y.bitterGroups && gg.NextDouble() < x.digs / Days
                && Comes(y, HarmKinds.GroupBrawl, gg, HarmFourMath.GroupBrawl(70f, true, y.peacemaker, gg.NextDouble() < 0.2)))
            {
                var a = Any(y, gg);
                var b = Any(y, gg, a);
                int join = HarmFourMath.GroupJoins(gg.Next(0, 3), gg.Next(0, 3));
                y.today.Add((19, () =>
                {
                    if (!Fight(y, a, b, HarmKinds.GroupBrawl)) return;
                    for (int i = 0; i < join; i++)
                    {
                        var c = Any(y, gg, a);
                        var d = Any(y, gg, c);
                        Fight(y, c, d, HarmKinds.GroupBrawl);
                    }
                }));
            }
            // The sore loser: a close race's hot or bitter loser, one in ten (half for a gracious winner), blows half the time.
            var go = rng("sore");
            if (day == y.electionDay && go.NextDouble() < HustingsHeld)
                for (int s = 0; s < (int)Seats; s++)
                {
                    float chance = HarmFourMath.SoreLoser(go.NextDouble() < Close, go.NextDouble() < HotLoser, false, go.NextDouble() < GraciousWinner);
                    if (!Comes(y, HarmKinds.SoreLoser, go, chance) || go.NextDouble() >= HarmFourMath.SoreBlows) continue;
                    var a = Any(y, go);
                    var b = Any(y, go, a);
                    y.today.Add((18, () => Fight(y, a, b, HarmKinds.SoreLoser)));
                }
            // A guest's night goes sour: Rimfolk's visitors staying inside, and Hospitality's guest nights, one in 15 by temper.
            var gq = rng("guest night");
            foreach (var (nights, kind) in new[] { (x.visits * x.stayInside, HarmKinds.GuestNight), (x.hospitalityNights, HarmKinds.GuestNightHospitality) })
            {
                if (gq.NextDouble() >= nights / Days) continue;
                float chance = HarmFourMath.GuestNight(gq.Next(-1, 4), gq.NextDouble() < GuestDrink, gq.NextDouble() < Warden, gq.NextDouble() < Manners, false);
                var p = Any(y, gq);
                var k = kind;
                if (Comes(y, k, gq, chance)) y.today.Add((21, () => Hurt(y, p, k, false)));
            }
            // The rematch: one beaten in three holds it; their people back within the year; the colonist takes it; a hurt in it.
            var gm = rng("rematch");
            if (gm.NextDouble() < Beaten / Days && Comes(y, HarmKinds.Rematch, gm, HarmFourMath.RematchChance)
                && gm.NextDouble() < Revisit && gm.NextDouble() < Takes && gm.NextDouble() < RematchHurts)
            {
                var p = Any(y, gm);
                int at = day + gm.Next(3, 30);
                y.onsets.Add((p, at, HarmKinds.Rematch));
            }
            // Games with real contests: the bouts' real blows and the race's turned ankles.
            var gt = rng("games");
            if (gt.NextDouble() < x.gamesDays / Days)
            {
                var order = y.people.OrderBy(_ => gt.Next()).ToList();
                int pairs = Math.Min(HarmFourMath.GamesPairs, order.Count / 2);
                for (int i = 0; i < pairs; i++)
                    if (Comes(y, HarmKinds.Games, gt, HarmFourMath.GamesBlowChance))
                    {
                        var who = order[2 * i + gt.Next(2)];
                        y.today.Add((15, () => Hurt(y, who, HarmKinds.Games)));
                    }
                bool rough = gt.NextDouble() < RoughCourse;
                foreach (var runner in order.Skip(2 * pairs).Take(HarmFourMath.GamesRunners))
                    if (Comes(y, HarmKinds.Games, gt, HarmFourMath.RaceSprain(rough, y.age)))
                    {
                        var who = runner;
                        y.today.Add((16, () => Hurt(y, who, HarmKinds.Games)));
                    }
            }
        }

        // ---------------- Harm, part 5 ----------------

        private static void PartFiveDay(Year y, int day, Func<string, Random> rng)
        {
            var x = y.x;
            // The sick work on: a petition to rest lapses while one of the colony works on with something catching.
            var gw = rng("sick worker");
            if (gw.NextDouble() < x.sickPetitions / Days && Comes(y, HarmKinds.SickWorkOn, gw, HarmFiveMath.CarrierChance(CarrierKind.SickWorker, false, false))
                && gw.NextDouble() < NearGate)
                y.onsets.Add((Any(y, gw), day + 2, HarmKinds.SickWorkOn));
            // The sickness at the gate: a group of a people under a plague, one in four carrying it (none with the gate shut or the quarantine).
            var gg = rng("gate sickness");
            if (gg.NextDouble() < GateGroups * GroupPlague / Days)
            {
                float chance = HarmFiveMath.CarrierChance(CarrierKind.Gate, gg.NextDouble() < x.gateShut, gg.NextDouble() < x.plagueQuarantine);
                if (Comes(y, HarmKinds.GateSickness, gg, chance) && gg.NextDouble() < NearGate)
                    y.onsets.Add((Any(y, gg), day + 3, HarmKinds.GateSickness));
            }
            // A courier who arrives sick: the same rule, the courier unloading beside one of the colony.
            var gc = rng("courier sick");
            if (gc.NextDouble() < CourierTrips * CourierPlague / Days)
            {
                float chance = HarmFiveMath.CarrierChance(CarrierKind.Courier, gc.NextDouble() < x.gateShut, gc.NextDouble() < x.plagueQuarantine);
                if (Comes(y, HarmKinds.CourierSick, gc, chance) && gc.NextDouble() < NearCourier)
                    y.onsets.Add((Any(y, gc), day + 3, HarmKinds.CourierSick));
            }
            // The fever at their fire: a camp's sick one, one in five catching, unless seen to; someone at their fire an hour.
            var gf = rng("camp fever");
            if (gf.NextDouble() < CampsPerYear * CampSickShare / Days && Comes(y, HarmKinds.CampFever, gf, HarmFiveMath.CarrierChance(CarrierKind.Camp, false, false))
                && gf.NextDouble() >= x.campTended && gf.NextDouble() < NearCamp)
                y.onsets.Add((Any(y, gf), day + 4, HarmKinds.CampFever));
            // The patient who brings something worse: one in 20, a night in the same room, one in four.
            var gp = rng("patient");
            if (gp.NextDouble() < Patients / Days && Comes(y, HarmKinds.PatientIll, gp, HarmFiveMath.PatientCarrierChance))
                foreach (var p in y.people)
                    if (gp.NextDouble() < SharedRoom && gp.NextDouble() < HarmFiveMath.WardCatch(HarmFiveMath.WardHours, 1f))
                        y.onsets.Add((p, day + 2, HarmKinds.PatientIll));
            // The hedge healer's bad batch: one paid visit in eight, uncaught by a doctor short of Medicine 8, one dosed in three.
            var gb = rng("bad batch");
            if (gb.NextDouble() < HealerVisits * HealerPaid / Days && Comes(y, HarmKinds.BadBatch, gb, HarmFiveMath.BadBatchChance)
                && HarmFiveMath.BatchBad(true, gb.NextDouble() < x.lowDoctor ? 5 : 10))
                foreach (var p in y.people)
                    if (gb.NextDouble() < HarmFiveMath.BadBatchSick)
                        y.onsets.Add((p, day + 1, HarmKinds.BadBatch));
            // Small things that grow: worms left three days, bites a day in a malaria country.
            var gs = rng("small things");
            foreach (var p in y.people)
            {
                if (gs.NextDouble() < x.worms / Days && gs.NextDouble() < x.wormsUntended && Comes(y, HarmKinds.SmallThings, gs, HarmFiveMath.SmallThingsChance))
                {
                    var who = p;
                    y.today.Add((8, () => Hurt(y, who, HarmKinds.SmallThings)));
                }
                bool netted = gs.NextDouble() < Netted;
                if (gs.NextDouble() < MalariaLand && gs.NextDouble() < Bites / Days && gs.NextDouble() < x.bitesUntended
                    && Comes(y, HarmKinds.SmallThings, gs, HarmFiveMath.BitesGrow(true, netted, false)))
                {
                    var who = p;
                    y.today.Add((8, () => Hurt(y, who, HarmKinds.SmallThings)));
                }
            }
            // Waiting at the gate in the cold: a journey's wait past the hour in a hard frost, a roll each two cold hours a traveller.
            var gq = rng("gate cold");
            if (gq.NextDouble() < x.journeys * x.gateWaits / Days && gq.NextDouble() < GateFrost)
            {
                int travellers = gq.Next(TravellersMin, TravellersMax + 1), hours = gq.Next(2, 5);
                foreach (var p in y.people.OrderBy(_ => gq.Next()).Take(travellers))
                    for (int h = 1; h <= hours; h++)
                        if (HarmFiveMath.ColdRoll(h) && Comes(y, HarmKinds.GateCold, gq, HarmFiveMath.GateCold(true, true)))
                        {
                            var who = p;
                            y.today.Add((9, () => Hurt(y, who, HarmKinds.GateCold)));
                            break;
                        }
            }
            // The night watch in the cold: a watch's frost (half with two or more), and a lone watcher outside the walls drawing a predator.
            var gn = rng("night watch");
            if (gn.NextDouble() < x.watches / Days)
            {
                int watchers = gn.Next(1, 4);
                bool frost = gn.NextDouble() < WatchFrost;
                foreach (var p in y.people.OrderBy(_ => gn.Next()).Take(watchers))
                {
                    var who = p;
                    if (frost)
                        for (int h = 1; h <= 6; h++)
                            if (HarmFiveMath.ColdRoll(h) && Comes(y, HarmKinds.NightWatch, gn, HarmFiveMath.WatchCold(true, watchers)))
                            {
                                y.today.Add((2, () => Hurt(y, who, HarmKinds.NightWatch)));
                                break;
                            }
                    if (Comes(y, HarmKinds.NightWatch, gn, HarmFiveMath.WatchPredator(watchers, gn.NextDouble() < x.unwalled, gn.NextDouble() < HungryPredator)))
                        y.today.Add((1, () => Hurt(y, who, HarmKinds.NightWatch)));
                }
            }
            // Weather lovers: a rain lover's day in the rain, a snow lover's in falling snow in bitter cold.
            var gl = rng("weather lovers");
            foreach (var p in y.people)
            {
                bool rain = gl.NextDouble() < RainLovers, snow = !rain && gl.NextDouble() < SnowLovers;
                if (!rain && !snow || gl.NextDouble() >= (rain ? RainDays : SnowDays) / Days) continue;
                float chance = HarmFiveMath.WeatherLover(rain, rain ? HarmFiveMath.RainLoverHours : HarmFiveMath.SnowLoverHours, gl.NextDouble() < x.covered);
                if (!Comes(y, HarmKinds.WeatherLovers, gl, chance)) continue;
                var who = p;
                y.today.Add((15, () => Hurt(y, who, HarmKinds.WeatherLovers)));
            }
            // Sleepwalking after a nightmare: one shaken in 15 walks; kept in by a bolt or a roommate, else frostnip in the cold or a stumble half the time.
            var gk = rng("sleepwalk");
            foreach (var p in y.people)
            {
                if (gk.NextDouble() >= x.shaken / Days || !Comes(y, HarmKinds.Sleepwalk, gk, HarmFiveMath.SleepwalkChance) || gk.NextDouble() < KeptIn) continue;
                bool cold = gk.NextDouble() < ColdNightOut;
                if (HarmFiveMath.SleepwalkEnds(cold ? -5f : 5f, cold, (float)gk.NextDouble()) == 0) continue;
                var who = p;
                y.today.Add((2, () => Hurt(y, who, HarmKinds.Sleepwalk)));
            }
            // A restless walk at dusk: about once a season at the height of restlessness; alone and beyond the walls, a hungry predator one walk in four.
            var gr = rng("restless");
            foreach (var p in y.people)
            {
                if (gr.NextDouble() >= x.restlessDusks || gr.NextDouble() >= HarmFiveMath.RestlessWalkChance) continue;
                float chance = HarmFiveMath.RestlessPredator(gr.NextDouble() >= FriendAlong, gr.NextDouble() < x.unwalled, gr.NextDouble() < HungryPredator);
                if (!Comes(y, HarmKinds.RestlessWalk, gr, chance)) continue;
                var who = p;
                y.today.Add((19, () => Hurt(y, who, HarmKinds.RestlessWalk)));
            }
            // A slip into cold water: an hour fishing below freezing, one in 200 (twice in a hard frost).
            var gx = rng("cold water");
            foreach (var p in y.people)
            {
                if (gx.NextDouble() >= x.coldFishing / Days) continue;
                float chance = HarmFiveMath.ColdWater(gx.NextDouble() < HardFrostFishing ? -8f : -2f);
                if (!Comes(y, HarmKinds.ColdWater, gx, chance)) continue;
                var who = p;
                y.today.Add((11, () => Hurt(y, who, HarmKinds.ColdWater)));
            }
        }

        /// <summary>Sicknesses, and the rematch's day, waiting to show: each comes the first morning the cap lets it, or passes after three days.</summary>
        private static void Onsets(Year y, int day, Func<string, Random> rng)
        {
            foreach (var o in y.onsets.Where(o => o.day <= day).ToList())
            {
                if (day - o.day > HarmTwoMath.OnsetGraceDays)
                {
                    y.onsets.Remove(o);
                    continue;
                }
                var p = o.p;
                y.today.Add((9, () =>
                {
                    if (!y.onsets.Contains(o)) return;
                    if (!May(y, p)) return;
                    y.onsets.Remove(o);
                    Hurt(y, p, o.kind);
                }));
            }
        }

        // ---------------- The report ----------------

        /// <summary>The variety's work in the average colony of six: the commonest kinds' shares and a kind coming again within fifteen days, with and without it.</summary>
        private static void VarietyReport(Tally with, Tally plain)
        {
            double tw = with.hurts.Values.Sum(), tp = plain.hurts.Values.Sum();
            Console.WriteLine("   the variety (the average colony of six, the budget with it and without it):");
            Console.WriteLine($"     in all {tw / with.colonists:0.000} and {tp / plain.colonists:0.000} a colonist-year; a kind coming again within {HarmBudgetMath.VarietyDays:0} days of its last "
                              + $"{with.repeats / with.runs:0.00} and {plain.repeats / plain.runs:0.00} times a colony-year");
            foreach (var k in new[] { HarmKinds.LandAilments, HarmKinds.HuntTurn, HarmKinds.GuestNightHospitality, HarmKinds.FailedBuild })
                Console.WriteLine($"     {k.label}: {with.hurts.GetValueOrDefault(k.label) / tw:P1} of the hurts with it, {plain.hurts.GetValueOrDefault(k.label) / tp:P1} without");
            Console.WriteLine();
        }

        private static void Report(Exposure x, int n, Tally before, Tally none, Tally budget)
        {
            double by = before.colonists, ny = none.colonists, cy = budget.colonists, years = budget.runs;
            string name = HeartState.For(x.key).name;
            Console.WriteLine($"== {name}, {n} colonists");
            Console.WriteLine($"   {"kind",-48} {"before",8} {"no budget",10} {"budget",8} {"its pass",9} {"held back",10} {"a colony-year",14}");
            double tb = 0, tn = 0, ta = 0, newTn = 0, newTa = 0;
            foreach (var k in Modelled)
            {
                double b = before.hurts.GetValueOrDefault(k.label) / by, u = none.hurts.GetValueOrDefault(k.label) / ny, a = budget.hurts.GetValueOrDefault(k.label) / cy;
                double held = budget.barred.GetValueOrDefault(k.label) / cy, pass = budget.passDays > 0 ? budget.passes.GetValueOrDefault(k.key) / budget.passDays : 1;
                tb += b;
                tn += u;
                ta += a;
                if (!k.founding)
                {
                    newTn += u;
                    newTa += a;
                }
                Console.WriteLine($"   {k.label,-48} {b,8:0.000} {u,10:0.000} {a,8:0.000} {pass,9:0.00} {held,10:0.000} {budget.hurts.GetValueOrDefault(k.label) / years,14:0.000}");
            }
            Console.WriteLine($"   {"in all",-48} {tb,8:0.000} {tn,10:0.000} {ta,8:0.000}   (budget/before {ta / Math.Max(1e-9, tb):0.000}; the later kinds, parts 4 and 5, {newTn:0.000} -> {newTa:0.000})");
            Console.WriteLine($"   the game's own beside it: a raid's wound {budget.raidWounds / cy:0.00} a colonist-year (HeartSim's raids), a food poisoning 0.3 to 1 (two meals a day,");
            Console.WriteLine("   a middling cook), the storyteller's diseases a few a colony-year.");
            Console.Write("   fights a colony-year:");
            foreach (var k in budget.fights.Keys.Union(before.fights.Keys).OrderBy(k => k))
                Console.Write($" {k} {before.fights.GetValueOrDefault(k) / before.runs:0.00} -> {budget.fights.GetValueOrDefault(k) / years:0.00};");
            Console.WriteLine();
            string Bands(Tally t) => $"strained {t.bands[1] / t.hours:P1}, worn {t.bands[2] / t.hours:P1}, near breaking {t.bands[3] / t.hours:P1}; "
                                     + $"episodes {t.episodes / t.colonists:0.000} a colonist-year; strain's mood {t.mood / t.hours:0.00}";
            Console.WriteLine("   heart before:     " + Bands(before));
            Console.WriteLine("   heart, budget:    " + Bands(budget));
            Console.WriteLine();
        }
    }
}
