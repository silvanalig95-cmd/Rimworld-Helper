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
        /// <summary>What they build, in the order they build it.</summary>
        public List<CampStructure> structures = new List<CampStructure>();
        /// <summary>When they leave in peace, the buildings they can't carry are left for the colony.</summary>
        public bool leaveBuildings;
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
        /// A colony known for one of these (a colony trait's tag, or a way: Devout, LongMemoried, Generous) draws this
        /// kind of camp: it comes twice as often there. Pilgrims seek out a devout colony; players, one that keeps its days.
        /// </summary>
        public List<string> drawnBy;

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
        public float WeightNow()
        {
            float w = CampWords.Situation(this) != null ? weight * situationWeight : weight;
            var ch = drawnBy.NullOrEmpty() ? null : CouncilGameComponent.Instance?.character;
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

        public bool FitsEra()
        {
            if (eras.NullOrEmpty()) return true;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            return eras.Any(tags.Contains);
        }

        public bool FitsFaction(Faction f) =>
            f != null && f.def.techLevel >= minTech && f.def.techLevel <= maxTech && (memes.NullOrEmpty() || CampWords.HasMeme(f, memes));

        /// <summary>Only when it could come at all: a kind for one kind of faith needs Ideology.</summary>
        public bool Possible => (memes.NullOrEmpty() || ModsConfig.IdeologyActive) && (!pokemon || (PokeWorldLink.Active && PokeWorldLink.PokemonShare() > 0f));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "camp kind has no texts";
            if (titles.NullOrEmpty()) yield return "camp kind has no titles";
            if (people.min < 1 || people.max > 8) yield return "camp size must stay within 1 to 8";
            if (structures.NullOrEmpty() || !structures.Any(s => s.role == "fire")) yield return "camp kind needs a fire";
            if (animals != null && animals.Sum(a => a?.count.max ?? 0) > CampLookMath.MaxAnimals) yield return "camp kind brings more than " + CampLookMath.MaxAnimals + " animals";
            if (animals != null && animals.Any(a => a == null || (a.kinds.NullOrEmpty() && !a.pack))) yield return "camp animals need kinds (or pack)";
            if (campGoods != null && campGoods.Any(g => g == null || g.defs.NullOrEmpty())) yield return "camp goods need defs";
            if (leaveBehind != null && leaveBehind.Any(g => g == null || g.key.NullOrEmpty() || (g.defs.NullOrEmpty() && !g.wildTrees))) yield return "camp gifts need a key and defs (or wildTrees)";
            if (farewellWalk != null && farewellWalk.to != "sick" && farewellWalk.to != "gate") yield return "farewellWalk.to must be sick or gate";
            if (farewellWalk?.gift != null && (leaveBehind == null || !leaveBehind.Any(g => g?.key == farewellWalk.gift))) yield return "farewellWalk.gift names no leaveBehind entry";
        }
    }

    /// <summary>
    /// One kind of building in a camp. Roles: fire (the center), bed (one per person), wall (a windbreak), hut (walls,
    /// a door and a roof around one bed: a tent, with a tent mod's walls), stall, light, spot (a work or prayer spot),
    /// butcher. The first def that exists is used, built from the first material that fits it, so a tent mod's piece
    /// listed first stands in for the plain one only while that mod is loaded.
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
