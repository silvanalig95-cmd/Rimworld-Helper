using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// One kind of group that makes camp on the colony's map: who they are, how many, how long they stay, what they
    /// build, what they do with the land around them, and what they say. Texts may use {FACTION}, {COUNT} (how many
    /// came), {DAYS} (how long they mean to stay), {PLACE} (where they settle), {X_nameDef} (whoever speaks for them),
    /// {A_nameDef} (the colonist who met them), {IDEO} (their faith, with Ideology) and {SITUATION} (the world event
    /// that brought them); lines can carry era and season tags ([lowtech], [!lowtech], [winter]...), [ideology] and
    /// [sameFaith], and [ws:kind] for a world event of that kind (see <see cref="CampWords"/>).
    /// </summary>
    public class CampKindDef : Def
    {
        /// <summary>
        /// The hours this kind of camp most likely arrives (local, from~to, across midnight when from is the later): the
        /// drovers by day, the minstrels toward evening, deserters by night. Unset (-1~-1): any hour alike.
        /// </summary>
        public IntRange arrives = new IntRange(-1, -1);

        /// <summary>How fitting the hour is for this kind's arrival (CampStoryMath.ArrivalFit).</summary>
        public float HourFit(int hour) => CampStoryMath.ArrivalFit(hour, arrives.min, arrives.max);

        /// <summary>The colony's era must be one of these (tribal, medieval, industrial, spacer); empty = any.</summary>
        public List<string> eras = new List<string>();
        /// <summary>
        /// A war party (WarCamp.cs): never comes on its own, only to a fight; a threat on the map sends them after it
        /// rather than away, and they don't fall ill or come back of their own accord.
        /// </summary>
        public bool fights;
        /// <summary>At the parting of a good stay they may ask to stay on the colony's land for good, as its neighbours (Holding.cs).</summary>
        public bool canSettle;
        /// <summary>A camp about Pokémon (traveling trainers): only with PokéWorld's Pokémon in play (Integrations/PokeWorld.cs).</summary>
        public bool pokemon;
        /// <summary>
        /// What the land must have for them to come, and what they pitch near (Camps/CampKindsLand.cs): "fish" (water with
        /// fish in it, and fishing open to the colony), "ruins" (ancient pieces to strip outside the home area), "harvest"
        /// (more ripe crop than the growers can bring in before the season ends), "edge" (a craft come down by the map's
        /// edge), "monolith" (Anomaly's monolith woken on this map), "woods" (stands of grown wild trees clear of the
        /// colony, for woodcutters), "stone" (rock chunks lying clear of the colony, for stonecutters), "fleece" (the
        /// colony's woolly flock with wool to shear, for shearers), "wildHerd" (a wild herd that can be tamed, clear of the
        /// colony, for beast-catchers), "children" (the colony's children, for a travelling teacher). None: any land.
        /// </summary>
        public string needs;
        /// <summary>Their own work on the land by day (Camps/CampKindsLand.cs): "fish", "strip", "reap", "hull", "watch"; "cut", "shear", "catch" (CampKindsLand3.cs).</summary>
        public string work;
        /// <summary>Settled as neighbours, they bring word of the road when one comes up to the gate: a FlavorPoolDef of lines (Camps/RoadPost.cs).</summary>
        public string roadWord;
        /// <summary>The people they come from must be at least / at most this advanced.</summary>
        public TechLevel minTech = TechLevel.Undefined;
        public TechLevel maxTech = TechLevel.Archotech;
        public float weight = 1f;
        public IntRange people = new IntRange(3, 6);
        public FloatRange days = new FloatRange(4f, 10f);
        public FloatRange ages = new FloatRange(18f, 60f);
        /// <summary>A family: one last name, children when the game has them.</summary>
        public bool family;
        /// <summary>One of them trades from the camp (the game's own trading).</summary>
        public bool trader;
        /// <summary>A word their trader's kind should contain ("Combat"), or none for an ordinary one.</summary>
        public string traderKind;
        /// <summary>Animals and trees they take from the land around the camp, per day (fractions are a chance).</summary>
        public float huntsPerDay;
        public float chopsPerDay;
        /// <summary>They carry bows or rifles, so they can hunt.</summary>
        public bool armedHunters;
        /// <summary>Mourners carrying their dead home: the body comes in in someone's arms, lies by their lamp, and goes home with them (CampDead.cs).</summary>
        public bool bearsDead;
        /// <summary>
        /// How many of them come hurt, the first time they come (the wounded of a war): a cut already bandaged on the road
        /// and a fresh bruise the colony's doctor could see to, the game's own injuries (CampKindsLand3.cs).
        /// </summary>
        public IntRange wounded = IntRange.Zero;
        /// <summary>A daytime task of their own at one of their buildings (praying, prospecting, smithing, keeping the stall).</summary>
        public JobDef task;
        /// <summary>The role of the building the task is done at (see <see cref="CampStructure.role"/>); "herd": out among their animals; none = near the fire.</summary>
        public string taskAt;
        /// <summary>Chance that each grown-up is willing to work for the colony, if asked (and paid).</summary>
        public float hireChance = 0.3f;
        /// <summary>Chance per day that one of them asks to join, once things are going well.</summary>
        public float joinChance = 0.05f;
        /// <summary>Chance per day that they do the colony a good turn, and what kinds of help they give.</summary>
        public float helpChance = 0.15f;
        public List<string> help = new List<string>();
        /// <summary>What one of them walks over to buy at the colony's market stalls (with RimMarket), first first: Feed, Meal, RoadFood, RawFood, Medicine, Drink, Clothes, Textiles, Metal, Art. None: what anyone on the road needs (Visitors/VisitorTrade.cs).</summary>
        public List<string> buys;
        /// <summary>How proud they are: refusing terms, refusing to leave, taking offense (0..1).</summary>
        public float pride = 0.2f;
        /// <summary>Chance per day that they bend the terms they agreed to.</summary>
        public float breakTerms = 0.1f;
        /// <summary>Unease that grows each day they're here, before anything else.</summary>
        public float tensionPerDay = 1f;
        /// <summary>They can pay a fee in silver.</summary>
        public bool canPay = true;
        /// <summary>When their time is up they may ask to stay a little longer.</summary>
        public float extendChance = 0.3f;
        /// <summary>
        /// What they build of their own (Camps/CampKit.cs): the fire, and a kind's own pieces (a shrine's spot, a stall, a
        /// stage). The rest (beds, the hut or tents, tables and seats, lights, the work spots, the picket, the stash) is
        /// their people's age's kit (Defs/CampDefs/Rimfolk_CampKits.xml), laid out by CampBuildMath.
        /// </summary>
        public List<CampStructure> structures = new List<CampStructure>();
        /// <summary>
        /// How they sleep: round the fire (a war party, mourners, pilgrims), in one hut for all (settlers, a hermit, a
        /// wardens' post) or in tents (traders, road folk: a mod's tent that is a bed, else small rooms of tent walls).
        /// </summary>
        public CampSleepForm sleep = CampSleepForm.Ring;
        /// <summary>
        /// What they do with their constructions when they go (CampPackMath.Fate): left standing, nobody's (the default),
        /// taken down (a people whose ways leave no trace, or who don't want to be followed), or handed to the colony (a
        /// camp hired by contract). Portable furniture they own (bedrolls, tents, stools) is always packed and carried.
        /// </summary>
        public CampLeaves leaves = CampLeaves.Standing;
        /// <summary>Why this kind takes its constructions down or hands them over (for DESIGN.md and the tests; not shown).</summary>
        public string leavesWhy;
        /// <summary>Kept for old defs: what they leave standing is the colony's to use either way (see <see cref="leaves"/>).</summary>
        public bool leaveBuildings;
        /// <summary>Their callings: the day's work they come to do, first first (CampDayMath.Order); the camp's common work (wood, gathering, a catch) comes after.</summary>
        public List<CampLabour> works;
        /// <summary>
        /// What they may leave behind (CampPackMath.Leaves), each only when the stay made it: none listed, any that their stay
        /// brings about.
        /// </summary>
        public List<Leftover> leftovers;
        /// <summary>They set up tables and seats to eat at, for a stay of a day and a half or more.</summary>
        public bool dines = true;
        /// <summary>
        /// World events that put this kind of camp on the road (a famine, a plague, a war: their kinds): while one
        /// runs, the camp comes <see cref="situationWeight"/> times as often, and its lines can ask for [ws:kind] and
        /// name it ({SITUATION}).
        /// </summary>
        public List<string> situationKinds;
        public float situationWeight = 3f;
        /// <summary>The good turns they do, told their own way: lines tagged with the help ([heal], [show]...).</summary>
        [MustTranslate] public List<string> helpTexts;
        /// <summary>What the colony can ask of this kind of camp, beyond terms and gifts (see <see cref="CampAsk"/>): on the camp's page.</summary>
        public List<CampAsk> asks;
        /// <summary>
        /// Only from a people whose faith holds one of these memes (MemeDef names), and so only with Ideology: pilgrims of
        /// one kind of faith. Empty: any people.
        /// </summary>
        public List<string> memes;
        /// <summary>
        /// Only from one of Rimfolk's peoples of this calling (Peoples/: traders, healers, herders): the herding folk's
        /// winter grazing. Empty: any people. Such a kind comes only while a people of it is at peace with the colony.
        /// </summary>
        public string calling;
        /// <summary>Only in these seasons (the herders' winter grazing: fall and winter). Empty: any season.</summary>
        public List<Season> seasons;
        /// <summary>
        /// Winter folk: people who travel or work in deep cold (ice fishers, trappers, a caravan come in out of the snow).
        /// The flat floor of the cold that keeps every other camp away (CampWeatherMath.ExtremeCold) isn't theirs: whether
        /// they come, and stay, is the game's own limit for each of them as they're dressed, in their own winter clothes
        /// (<see cref="wears"/>).
        /// </summary>
        public bool winter;
        /// <summary>Only while the land stays this cold: the coming week's warmest hour below this (°C). Unset: any weather.</summary>
        public float coldBelow = CampWeatherMath.NoColdMark;
        /// <summary>Only while snow is falling (a caravan come in out of the snow).</summary>
        public bool inSnow;
        /// <summary>
        /// What they wear against the cold, put on first whenever their stay is colder than they're comfortable in (Camps/
        /// CampWeather.cs): each entry the first that exists, fits them (a kid's parka for a child) and their people's age,
        /// of the first material listed that it takes. The player's mods' winter clothes first (MayRequire), the game's own
        /// last. Anything still short is made up as for any camp.
        /// </summary>
        public List<CampWear> wears;
        /// <summary>A wardrobe shared by several kinds (a CampWearDef: the winter folk's), when they have no <see cref="wears"/> of their own.</summary>
        public CampWearDef wearSet;

        /// <summary>What they wear against the cold: their own list, else the shared wardrobe, else nothing.</summary>
        public List<CampWear> Wears => !wears.NullOrEmpty() ? wears : wearSet?.wears;
        /// <summary>How many animals the camp brings at most (a handful, CampLookMath.MaxAnimals; the herders' winter herd more, up to CampLookMath.MaxHerd).</summary>
        public int maxAnimals = CampLookMath.MaxAnimals;
        /// <summary>
        /// A colony known for one of these (a colony trait's tag, or a way: Devout, LongMemoried, Generous) draws this
        /// kind of camp: it comes twice as often there. Pilgrims seek out a devout colony; players, one that keeps its days.
        /// </summary>
        public List<string> drawnBy;
        /// <summary>
        /// What brings this kind to a home, read from the land near it, strongest first (World/Region/TravelMath.cs:
        /// relic, wonder, trees, ruins, ruinRegion, library, history, ore, rush, grazing, road, far). When the land has one,
        /// the kind weighs more the more remote the home (TravelMath.SeekWeight) and says why it came; with none, it comes at
        /// its base weight and says nothing of it.
        /// </summary>
        public List<string> seeks;
        /// <summary>Why they came, in their own words: lines tagged [seek:REASON], with {SEEK} (what they came for). Without one for the reason, the general line.</summary>
        [MustTranslate] public List<string> seekTexts;
        /// <summary>Comes only when the land near the home has one of <see cref="seeks"/> (the scholars: ruins, books, a history to hear).</summary>
        public bool seekOnly;

        // What makes the camp look lived-in (CampLook.cs): what they bring, put out, leave behind, and how they say goodbye.

        /// <summary>Animals they bring: pack beasts, a herd, a dog (see <see cref="CampAnimals"/>).</summary>
        public List<CampAnimals> animals;
        /// <summary>Goods they put out once the camp stands: bales, a woodpile, washing (see <see cref="CampGoods"/>).</summary>
        public List<CampGoods> campGoods;
        /// <summary>What they leave behind when they go at the end of their stay (see <see cref="CampGift"/>).</summary>
        public List<CampGift> leaveBehind;
        /// <summary>A last walk before they go: a healer's round, a gift laid at the colony's gate (see <see cref="CampWalk"/>).</summary>
        public CampWalk farewellWalk;
        /// <summary>
        /// Short lines said at moments of the camp's life, tagged with the moment: [kill] (a hunter's shot), [butcher] (a
        /// kill dressed at the camp), [carry] (a porter with a load), [herd] (seeing to the herd), [repair] (mending the
        /// colony's building), [round] (the farewell round), [gift] (a gift left behind).
        /// </summary>
        [MustTranslate] public List<string> momentLines;

        /// <summary>How likely this kind is to be the one that comes now.</summary>
        public float WeightNow(Map map = null)
        {
            float w = CampWords.Situation(this) != null ? weight * situationWeight : weight;
            // What the home it comes to is known for (each colony its own character).
            var ch = drawnBy.NullOrEmpty() ? null : CharacterTracker.For(map);
            if (ch != null && drawnBy.Any(ch.Is)) w *= 2f;
            return w;
        }

        [MustTranslate] public List<string> titles = new List<string>();
        [MustTranslate] public List<string> texts = new List<string>();
        /// <summary>Told once the camp is built.</summary>
        [MustTranslate] public List<string> settled = new List<string>();
        /// <summary>Someone of theirs asks to join the colony ({X} is the one asking).</summary>
        [MustTranslate] public List<string> joinTexts = new List<string>();
        /// <summary>They leave in peace when their time is up.</summary>
        [MustTranslate] public List<string> farewell = new List<string>();
        /// <summary>Short lines shown above heads now and then.</summary>
        [MustTranslate] public List<string> lines = new List<string>();

        /// <summary>Words for this kind written in a file of their own (a FlavorExtensionDef with camp), joined to its lists at startup.</summary>
        public void AddWords(FlavorExtensionDef ext)
        {
            if (ext == null) return;
            void Join(ref List<string> to, List<string> from)
            {
                if (from.NullOrEmpty()) return;
                to ??= new List<string>();
                to.AddRange(from.Where(l => !l.NullOrEmpty()));
            }
            Join(ref titles, ext.titles);
            Join(ref texts, ext.texts);
            Join(ref settled, ext.settled);
            Join(ref joinTexts, ext.joinTexts);
            Join(ref farewell, ext.farewell);
            Join(ref lines, ext.campLines);
            Join(ref momentLines, ext.momentLines);
        }

        public bool FitsEra()
        {
            if (eras.NullOrEmpty()) return true;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            return eras.Any(tags.Contains);
        }

        public bool FitsFaction(Faction f) =>
            f != null && Peoples.TechOf(f) >= minTech && Peoples.TechOf(f) <= maxTech && (memes.NullOrEmpty() || CampWords.HasMeme(f, memes)) && Peoples.Fits(calling, f);

        /// <summary>The season at the home suits it (<see cref="seasons"/>).</summary>
        public bool FitsSeason(Map map) => seasons.NullOrEmpty() || map != null && seasons.Contains(GenLocalDate.Season(map));

        /// <summary>
        /// The weather at the home suits it: not past the flat floor of the cold (unless they're winter folk) nor the heat,
        /// cold enough for a kind that comes only in the cold (<see cref="coldBelow"/>), snow falling for one that comes out
        /// of the snow (<see cref="inSnow"/>).
        /// </summary>
        public bool FitsWeather(Map map)
        {
            if (map == null) return false;
            if (CampWeatherMath.TooExtremeToCome(map.mapTemperature.OutdoorTemp, winter)) return false;
            if (inSnow && !CampLand.SnowNow(map)) return false;
            return coldBelow >= CampWeatherMath.NoColdMark || CampWeatherMath.ColdEnough(CampWeather.Usual(map).high, coldBelow);
        }

        /// <summary>Only when it could come at all: a kind for one kind of faith needs Ideology.</summary>
        public bool Possible => (memes.NullOrEmpty() || ModsConfig.IdeologyActive) && (!pokemon || (PokeWorldLink.Active && PokeWorldLink.PokemonShare() > 0f));

        /// <summary>The land has what they come for (see <see cref="needs"/>).</summary>
        public bool FitsMap(Map map) => (needs.NullOrEmpty() || CampLand.Fits(this, map)) && (Worker?.FitsMap(map) ?? true);

        /// <summary>A kind with ways of its own (Camps/CampWorker.cs): the peoples' own camps (Peoples/Events/). None: the usual camp.</summary>
        public System.Type workerClass;

        [Unsaved] private CampWorker workerInt;

        public CampWorker Worker
        {
            get
            {
                if (workerInt == null && workerClass != null && typeof(CampWorker).IsAssignableFrom(workerClass))
                {
                    workerInt = (CampWorker)System.Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "camp kind has no texts";
            if (titles.NullOrEmpty()) yield return "camp kind has no titles";
            if (people.min < 1 || people.max > 12) yield return "camp size must stay within 1 to 12";
            if (leaves != CampLeaves.Standing && leavesWhy.NullOrEmpty()) yield return "a camp that doesn't leave its constructions standing says why (leavesWhy)";
            if (structures.NullOrEmpty() || !structures.Any(s => s.role == "fire")) yield return "camp kind needs a fire";
            if (animals != null && animals.Sum(a => a?.count.max ?? 0) > CampLookMath.MaxAnimals) yield return "camp kind brings more than " + CampLookMath.MaxAnimals + " animals";
            if (animals != null && animals.Any(a => a == null || (a.kinds.NullOrEmpty() && !a.pack))) yield return "camp animals need kinds (or pack)";
            if (campGoods != null && campGoods.Any(g => g == null || g.defs.NullOrEmpty())) yield return "camp goods need defs";
            if (leaveBehind != null && leaveBehind.Any(g => g == null || g.key.NullOrEmpty() || (g.defs.NullOrEmpty() && !g.wildTrees && !g.young))) yield return "camp gifts need a key and defs (or wildTrees, or young)";
            if (leaveBehind != null && leaveBehind.Any(g => g != null && g.young && (g.at != "gate" || animals.NullOrEmpty()))) yield return "a young beast is handed over at the gate, from a camp that brings animals";
            if (farewellWalk != null && farewellWalk.to != "sick" && farewellWalk.to != "gate") yield return "farewellWalk.to must be sick or gate";
            if (farewellWalk?.gift != null && (leaveBehind == null || !leaveBehind.Any(g => g?.key == farewellWalk.gift))) yield return "farewellWalk.gift names no leaveBehind entry";
            if (buys != null)
                foreach (var b in buys.Where(b => VisitorTradeMath.Parse(b) == TradeGoods.None))
                    yield return "buys names no kind of goods: " + b;
            if (seekOnly && seeks.NullOrEmpty()) yield return "seekOnly without seeks";
            if (workerClass != null && !typeof(CampWorker).IsAssignableFrom(workerClass)) yield return "workerClass is no CampWorker";
            if (wears != null && wears.Any(w => w == null || w.defs.NullOrEmpty())) yield return "camp wears need defs";
            if (!Wears.NullOrEmpty() && !winter) yield return "only winter folk bring winter clothes of their own (wears without winter)";
            if (seeks != null)
                foreach (var s in seeks.Where(s => !TravelMath.Known(s)))
                    yield return "seeks names no reason the land can give: " + s;
        }
    }

    /// <summary>
    /// One kind of building in a camp, by its role (CampKit.Roles, and a kind's own "spot"): fire, bed, tent (a tent that
    /// is a bed), hut and door (a hut's walls and door), tentwall and tentdoor (a tent's), wall (a windbreak), table, seat,
    /// light, butcher, craft, stall, picket, stash, spot. The first def that exists is used, built from the first material
    /// that fits it, so a mod's piece listed first stands in for the plain one only while that mod is loaded.
    /// </summary>
    public class CampStructure
    {
        public string role;
        public List<string> defs = new List<string>();
        public List<string> stuff = new List<string>();
        public int count = 1;
        /// <summary>A hut's door, best first (a tent's flap); the plain door when none of them exists.</summary>
        public List<string> doors;

        public ThingDef Def => defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building);

        /// <summary>The hut's door: the first of <see cref="doors"/> that exists, else the plain door.</summary>
        public ThingDef DoorDef => doors?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.IsDoor) ?? ThingDefOf.Door;

        /// <summary>The first listed material this building can be made of (null for things made of no material).</summary>
        public ThingDef StuffFor(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            var allowed = GenStuff.AllowedStuffsFor(def).ToList();
            foreach (var n in stuff ?? new List<string>())
            {
                var s = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (s != null && allowed.Contains(s)) return s;
            }
            return GenStuff.DefaultStuffFor(def);
        }
    }

    /// <summary>A wardrobe against the cold several kinds of camp share (CampKindDef.wearSet): the winter folk's.</summary>
    public class CampWearDef : Def
    {
        public List<CampWear> wears = new List<CampWear>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (wears.NullOrEmpty() || wears.Any(w => w == null || w.defs.NullOrEmpty())) yield return "a wardrobe needs wears, each with defs";
        }
    }

    /// <summary>
    /// One thing a kind of camp wears against the cold (CampKindDef.wears): the first of <see cref="defs"/> that exists
    /// and fits the wearer, for people from <see cref="minTech"/> to <see cref="maxTech"/>, of the first of
    /// <see cref="stuff"/> it can be made of.
    /// </summary>
    public class CampWear
    {
        public List<string> defs = new List<string>();
        public List<string> stuff;
        public TechLevel minTech = TechLevel.Undefined;
        public TechLevel maxTech = TechLevel.Archotech;

        public bool ForAge(TechLevel tech) => tech >= minTech && tech <= maxTech;

        public IEnumerable<ThingDef> Defs => defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && d.IsApparel) ?? Enumerable.Empty<ThingDef>();

        public ThingDef StuffFor(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            var allowed = GenStuff.AllowedStuffsFor(def).ToList();
            foreach (var n in stuff ?? new List<string>())
            {
                var s = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (s != null && allowed.Contains(s)) return s;
            }
            return GenStuff.DefaultStuffFor(def);
        }
    }

    [DefOf]
    public static class CampDefOf
    {
        public static DutyDef Council_CampLife;
        public static JobDef Council_CampEat;
        public static JobDef Council_CampCook;
        public static JobDef Council_CampButcher;
        public static JobDef Council_CampTend;
        public static JobDef Council_CampSit;
        public static JobDef Council_CampFarewell;
        public static JobDef Council_CampLayGift;
        public static JobDef Council_CampRepair;
        public static JobDef Council_CampErrand;
        public static JobDef Council_CampErrandTo;
        public static JobDef Council_CampErrandDig;
        public static JobDef Council_CampGather;
        public static JobDef Council_CampMilk;
        public static JobDef Council_CampShear;
        public static JobDef Council_CampBring;
        public static JobDef Council_CampCarryHome;
        public static DutyDef Council_CampBeasts;
        public static LetterDef Council_CampLetter;
        public static ThoughtDef Council_Camp_GoodNeighbors;
        public static ThoughtDef Council_Camp_Crowded;
        public static ThoughtDef Council_Camp_DroveThemOff;
        public static ThoughtDef Council_Camp_Helped;
        public static ThoughtDef Council_Camp_FriendLeft;
        public static ThoughtDef Council_Camp_FriendBack;
        public static ThoughtDef Council_Camp_StoodWithUs;

        static CampDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampDefOf));
        }
    }
}
