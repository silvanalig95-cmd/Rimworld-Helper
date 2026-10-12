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
    /// came), {DAYS} (how long they mean to stay), {PLACE} (where they settle), {X_nameDef} (whoever speaks for them)
    /// and {A_nameDef} (the colonist who met them); lines can carry era tags ([lowtech], [!lowtech]...).
    /// </summary>
    public class CampKindDef : Def
    {
        /// <summary>The colony's era must be one of these (tribal, medieval, industrial, spacer); empty = any.</summary>
        public List<string> eras = new List<string>();
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
        /// <summary>The role of the building the task is done at (see <see cref="CampStructure.role"/>); none = near the fire.</summary>
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
            f != null && f.def.techLevel >= minTech && f.def.techLevel <= maxTech;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "camp kind has no texts";
            if (titles.NullOrEmpty()) yield return "camp kind has no titles";
            if (people.min < 1 || people.max > 8) yield return "camp size must stay within 1 to 8";
            if (structures.NullOrEmpty() || !structures.Any(s => s.role == "fire")) yield return "camp kind needs a fire";
        }
    }

    /// <summary>
    /// One kind of building in a camp. Roles: fire (the center), bed (one per person), wall (a windbreak), hut (walls,
    /// a door and a roof around one bed), stall, light, spot (a work or prayer spot), butcher. The first def that exists
    /// is used, built from the first material that fits it.
    /// </summary>
    public class CampStructure
    {
        public string role;
        public List<string> defs = new List<string>();
        public List<string> stuff = new List<string>();
        public int count = 1;

        public ThingDef Def => defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building);

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
        public static LetterDef Council_CampLetter;
        public static ThoughtDef Council_Camp_GoodNeighbors;
        public static ThoughtDef Council_Camp_Crowded;
        public static ThoughtDef Council_Camp_DroveThemOff;
        public static ThoughtDef Council_Camp_Helped;
        public static ThoughtDef Council_Camp_FriendLeft;

        static CampDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampDefOf));
        }
    }
}
