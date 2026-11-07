using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What one of a scenario's parts does for a new colony's start (the player's decision 3A): carried for that colony
    /// alone (its people, the parts that shape starting people, its kit, its arrival, its animals, Rimfolk's own parts),
    /// not carried because it changes the whole game (listed plainly), never carried (Bad Blood's two families), a part of
    /// another mod's the code doesn't know (listed as not carried), or nothing to say (the world's layers).
    /// </summary>
    public enum ScenCarry
    {
        People,
        Shaping,
        Kit,
        Arrival,
        Animals,
        Rimfolk,
        Whole,
        Never,
        Unknown,
        Hidden
    }

    /// <summary>Why a part that changes the whole game isn't carried: what it changes, as the window lists it.</summary>
    public enum ScenWhole
    {
        None,
        Faction,
        Research,
        Rules,
        Map,
        Conditions,
        IncidentsOff,
        Events,
        Opening,
        /// <summary>A part that shapes starting people, with the deeper game hooks off: its pawn hooks can't be reached.</summary>
        Hooks
    }

    /// <summary>How a scenario's newcomers arrive at the new colony: on foot from the edge, in drop pods, or in their gravship.</summary>
    public enum StartArrival
    {
        Standing,
        Pods,
        Gravship
    }

    /// <summary>What a scenario's people part requires among those who come: a kind of person, a xenotype, or a mutant.</summary>
    public enum StartRequired
    {
        Kind,
        Xenotype,
        Mutant
    }

    /// <summary>
    /// The rules of a scenario's start for a new colony (docs/design/stage3-many-colonies.md, section 1, "A scenario's
    /// start"; release 3), with no game types so Tools/Tests can check them: which of a scenario's parts come (by the
    /// part's class, as the game names it), how the newcomers arrive and when the gravship is offered, the people's count
    /// and the candidates to choose from, what the scenario requires of them, the passage with the scenario's kit, when
    /// Rimfolk's own parts are set up at the new colony, and the keys a chosen scenario is kept by.
    /// </summary>
    public static class ScenarioStartMath
    {
        public const int DayTicks = 60000;

        private const string G = "RimWorld.", R = "ColonyCouncil.";

        private static readonly Dictionary<string, (ScenCarry carry, ScenWhole whole)> Known = new Dictionary<string, (ScenCarry, ScenWhole)>
        {
            // The people: count and choice, required kinds, xenotypes and mutants, ages and skills, read from the fields.
            [G + "ScenPart_ConfigPage_ConfigureStartingPawns"] = (ScenCarry.People, ScenWhole.None),
            [G + "ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs"] = (ScenCarry.People, ScenWhole.None),
            [G + "ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes"] = (ScenCarry.People, ScenWhole.None),
            [G + "ScenPart_ConfigPage_ConfigureStartingPawns_Mutants"] = (ScenCarry.People, ScenWhole.None),
            // What shapes starting people, through their own pawn hooks (and no possessions, read by the window).
            [G + "ScenPart_ForcedTrait"] = (ScenCarry.Shaping, ScenWhole.None),
            [G + "ScenPart_ForcedHediff"] = (ScenCarry.Shaping, ScenWhole.None),
            [G + "ScenPart_Naked"] = (ScenCarry.Shaping, ScenWhole.None),
            [G + "ScenPart_SetNeedLevel"] = (ScenCarry.Shaping, ScenWhole.None),
            [G + "ScenPart_PawnFilter_Age"] = (ScenCarry.Shaping, ScenWhole.None),
            [G + "ScenPart_NoPossessions"] = (ScenCarry.Shaping, ScenWhole.None),
            // The kit: what it starts with, and what it lays about the start.
            [G + "ScenPart_StartingThing_Defined"] = (ScenCarry.Kit, ScenWhole.None),
            [G + "ScenPart_StartingMech"] = (ScenCarry.Kit, ScenWhole.None),
            [G + "ScenPart_ScatterThingsNearPlayerStart"] = (ScenCarry.Kit, ScenWhole.None),
            [G + "ScenPart_ScatterThingsAnywhere"] = (ScenCarry.Kit, ScenWhole.None),
            [G + "ScenPart_StartingAnimal"] = (ScenCarry.Animals, ScenWhole.None),
            [G + "ScenPart_PlayerPawnsArriveMethod"] = (ScenCarry.Arrival, ScenWhole.None),
            // What changes the whole game.
            [G + "ScenPart_PlayerFaction"] = (ScenCarry.Whole, ScenWhole.Faction),
            [G + "ScenPart_StartingResearch"] = (ScenCarry.Whole, ScenWhole.Research),
            [G + "ScenPart_Rule_DisallowDesignator"] = (ScenCarry.Whole, ScenWhole.Rules),
            [G + "ScenPart_DisallowBuilding"] = (ScenCarry.Whole, ScenWhole.Rules),
            [G + "ScenPart_StatFactor"] = (ScenCarry.Whole, ScenWhole.Rules),
            [G + "ScenPart_OnPawnDeathExplode"] = (ScenCarry.Whole, ScenWhole.Rules),
            [G + "ScenPart_DisableMapGen"] = (ScenCarry.Whole, ScenWhole.Rules),
            [G + "ScenPart_ForcedMap"] = (ScenCarry.Whole, ScenWhole.Map),
            [G + "ScenPart_GameCondition"] = (ScenCarry.Whole, ScenWhole.Conditions),
            [G + "ScenPart_PermaGameCondition"] = (ScenCarry.Whole, ScenWhole.Conditions),
            [G + "ScenPart_DisableIncident"] = (ScenCarry.Whole, ScenWhole.IncidentsOff),
            [G + "ScenPart_DisableQuest"] = (ScenCarry.Whole, ScenWhole.IncidentsOff),
            [G + "ScenPart_CreateIncident"] = (ScenCarry.Whole, ScenWhole.Events),
            [G + "ScenPart_CreateQuest"] = (ScenCarry.Whole, ScenWhole.Events),
            [G + "ScenPart_MonolithGeneration"] = (ScenCarry.Whole, ScenWhole.Events),
            [G + "ScenPart_AutoActivateMonolith"] = (ScenCarry.Whole, ScenWhole.Events),
            [G + "ScenPart_PursuingMechanoids"] = (ScenCarry.Whole, ScenWhole.Events),
            [G + "ScenPart_GameStartDialog"] = (ScenCarry.Whole, ScenWhole.Opening),
            [G + "ScenPart_PlanetLayer"] = (ScenCarry.Hidden, ScenWhole.None),
            [G + "ScenPart_PlanetLayerFixed"] = (ScenCarry.Hidden, ScenWhole.None),
            // Rimfolk's own, on that colony's council, calendar and people.
            [R + "ScenPart_CouncilSeats"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilDays"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilGoal"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilWays"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilFoe"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilBond"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilSecret"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilEvent"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilWards"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilSituation"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilDebt"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilDeserters"] = (ScenCarry.Rimfolk, ScenWhole.None),
            [R + "ScenPart_CouncilBeholden"] = (ScenCarry.Rimfolk, ScenWhole.None),
            // The colony is the family: a feud along family lines is never carried.
            [R + "ScenPart_CouncilFamilies"] = (ScenCarry.Never, ScenWhole.None)
        };

        /// <summary>The part classes this knows, by the game's full name (for the tests).</summary>
        public static IEnumerable<string> KnownTypes => Known.Keys;

        /// <summary>
        /// What a part does for a new colony's start, by its class's full name. A part this doesn't know (another mod's,
        /// even one built on the game's) is listed as not carried; the parts that shape starting people through their pawn
        /// hooks need the deeper game hooks (<paramref name="hooksOn"/>), else they are listed as not carried too.
        /// </summary>
        public static ScenCarry Carry(string type, bool hooksOn = true)
        {
            if (type == null || !Known.TryGetValue(type, out var k)) return ScenCarry.Unknown;
            if (k.carry == ScenCarry.Shaping && !hooksOn && NeedsHooks(type)) return ScenCarry.Whole;
            return k.carry;
        }

        /// <summary>What a part not carried changes, for the window's list (None for a part that is carried, never carried, or another mod's).</summary>
        public static ScenWhole WholeOf(string type, bool hooksOn = true)
        {
            if (type == null || !Known.TryGetValue(type, out var k)) return ScenWhole.None;
            if (k.carry == ScenCarry.Shaping && !hooksOn && NeedsHooks(type)) return ScenWhole.Hooks;
            return k.whole;
        }

        /// <summary>Shapes people through the game's pawn hooks (every shaping part but "no possessions", which the window reads itself).</summary>
        public static bool NeedsHooks(string type) => type != G + "ScenPart_NoPossessions" && Known.TryGetValue(type ?? "", out var k) && k.carry == ScenCarry.Shaping;

        /// <summary>Carried for the new colony alone.</summary>
        public static bool IsCarried(ScenCarry c) => c == ScenCarry.People || c == ScenCarry.Shaping || c == ScenCarry.Kit || c == ScenCarry.Arrival || c == ScenCarry.Animals
                                                    || c == ScenCarry.Rimfolk;

        /// <summary>
        /// The player's people (the scenario's own faction) is listed as not carried only when it isn't the colony's kind
        /// already: the colony stays the people it is.
        /// </summary>
        public static bool FactionListed(bool sameAsColony) => !sameAsColony;

        /// <summary>The order the window lists what is carried in (each kind once).</summary>
        public static List<ScenCarry> CarriedOrder(IEnumerable<ScenCarry> carries)
        {
            var have = new HashSet<ScenCarry>(carries ?? Array.Empty<ScenCarry>());
            var list = new List<ScenCarry>();
            foreach (var c in new[] { ScenCarry.People, ScenCarry.Shaping, ScenCarry.Kit, ScenCarry.Animals, ScenCarry.Arrival, ScenCarry.Rimfolk })
                if (have.Contains(c))
                    list.Add(c);
            return list;
        }

        /// <summary>The order the window lists what changes the whole game in (each kind once).</summary>
        public static List<ScenWhole> WholeOrder(IEnumerable<ScenWhole> wholes)
        {
            var have = new HashSet<ScenWhole>(wholes ?? Array.Empty<ScenWhole>());
            var list = new List<ScenWhole>();
            foreach (var w in new[] { ScenWhole.Faction, ScenWhole.Research, ScenWhole.Rules, ScenWhole.Map, ScenWhole.Conditions, ScenWhole.IncidentsOff, ScenWhole.Events,
                         ScenWhole.Opening, ScenWhole.Hooks })
                if (have.Contains(w))
                    list.Add(w);
            return list;
        }

        // ---------------- How they arrive ----------------

        /// <summary>The game's arrival by its number (PlayerPawnsArriveMethod): standing, drop pods, the gravship.</summary>
        public const int MethodStanding = 0, MethodPods = 1, MethodGravship = 2;

        /// <summary>
        /// The gravship start is offered only when the old colony is left behind (abandoned or handed over in the same
        /// act), with Odyssey, and on a map made for the newcomers alone (nobody of the colony there before them).
        /// </summary>
        public static bool GravshipOffered(bool odyssey, bool mapForThem, bool oldLeftBehind) => odyssey && mapForThem && oldLeftBehind;

        /// <summary>
        /// How they arrive, by the scenario's way: drop pods as it says; the gravship when it is offered; else on foot from
        /// the edge of the land (the standing start mid-game, and the game's own fallback for a gravship it can't give).
        /// </summary>
        public static StartArrival Arrival(int method, bool odyssey, bool mapForThem, bool oldLeftBehind)
        {
            if (method == MethodPods) return StartArrival.Pods;
            if (method == MethodGravship && GravshipOffered(odyssey, mapForThem, oldLeftBehind)) return StartArrival.Gravship;
            return StartArrival.Standing;
        }

        /// <summary>The gravship asked for but not given (the old colony kept, someone of the colony there first, or no Odyssey): said in the window.</summary>
        public static bool GravshipRefused(int method, bool odyssey, bool mapForThem, bool oldLeftBehind) =>
            method == MethodGravship && !GravshipOffered(odyssey, mapForThem, oldLeftBehind);

        // ---------------- The people ----------------

        /// <summary>The most a scenario's people part asks for (the game's own most for a start).</summary>
        public const int MaxPeople = 10;

        /// <summary>How many come: the scenario's count, 1 to 10.</summary>
        public static int Count(int partCount) => Math.Max(1, Math.Min(MaxPeople, partCount));

        /// <summary>How many to choose them from: the scenario's choice, never fewer than come, at most 10.</summary>
        public static int Candidates(int count, int choice) => Math.Max(Count(count), Math.Min(MaxPeople, choice));

        /// <summary>
        /// Whether the chosen hold what the scenario requires, as the game's own page checks it: at least as many of a
        /// required kind, exactly as many of a required xenotype or mutant.
        /// </summary>
        public static bool RequiredMet(StartRequired kind, int need, int have) => kind == StartRequired.Kind ? have >= need : have == need;

        /// <summary>A life stage the scenario allows (the game's DevelopmentalStage flags).</summary>
        public static bool StageAllowed(uint stage, uint allowed) => (stage & allowed) != 0;

        /// <summary>Babies alone can't found a colony (the game's own check with Biotech).</summary>
        public static bool AllBabies(int babies, int count) => count > 0 && babies >= count;

        // ---------------- The passage (decision 4A) ----------------

        /// <summary>The kit's own worth in a passage, rounded to ten.</summary>
        public static int KitWorth(float worth) => worth <= 0f ? 0 : (int)Math.Round(worth / 10f) * 10;

        /// <summary>
        /// A scenario's start with the old colony kept: each newcomer's passage as release 1 has it (about their
        /// possessions' worth, at least 150), each person the kit brings (a ward) at the least passage, and the kit's own
        /// worth (its things, what it lays about, its animals and mechs). Free with the old colony left behind.
        /// </summary>
        public static int Passage(IEnumerable<float> possessions, float kitWorth, int kitPeople, bool free)
        {
            if (free) return 0;
            return FoundingMath.Passage(possessions, false) + Math.Max(0, kitPeople) * FoundingMath.MinPassage + KitWorth(kitWorth);
        }

        // ---------------- Rimfolk's own parts at the new colony ----------------

        /// <summary>
        /// The Beholden's warm start (their goodwill raised) comes only with a new game: goodwill belongs to the whole
        /// colony, so a new colony's grant is its tie alone.
        /// </summary>
        public static bool WarmStart(bool newGame) => newGame;

        /// <summary>
        /// Rimfolk's parts are set up at the new colony once everyone who came is on the map (out of their pods), or a day
        /// after they arrived with whoever is there.
        /// </summary>
        public static bool SetupDue(int here, int came, int ticksSinceArrival) => came > 0 && (here >= came || ticksSinceArrival >= DayTicks);

        // ---------------- The chosen scenario, kept by a key ----------------

        /// <summary>Where a scenario comes from: the game's, its DLCs', Rimfolk's and other mods' defs; this game's own; the player's own files; the workshop's.</summary>
        public const int FromDef = 0, ThisGame = 1, Local = 2, Workshop = 3;

        private static readonly string[] Prefixes = { "def:", "game", "file:", "workshop:" };

        /// <summary>The key a chosen scenario is kept by on the founding plan.</summary>
        public static string Key(int from, string name) => from == ThisGame ? Prefixes[ThisGame] : from < 0 || from >= Prefixes.Length || string.IsNullOrEmpty(name) ? null : Prefixes[from] + name;

        /// <summary>A key read back: where it comes from and its name (a def's name, or a scenario's own); false for no key.</summary>
        public static bool Parse(string key, out int from, out string name)
        {
            from = -1;
            name = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (key == Prefixes[ThisGame])
            {
                from = ThisGame;
                return true;
            }
            for (int i = 0; i < Prefixes.Length; i++)
            {
                if (i == ThisGame || !key.StartsWith(Prefixes[i], StringComparison.Ordinal) || key.Length == Prefixes[i].Length) continue;
                from = i;
                name = key.Substring(Prefixes[i].Length);
                return true;
            }
            return false;
        }
    }
}
