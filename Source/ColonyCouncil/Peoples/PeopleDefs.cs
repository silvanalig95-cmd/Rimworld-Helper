using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The template of one of Rimfolk's own peoples, on its FactionDef (Defs/PeopleDefs/): what it lives by and what its
    /// people are called in Rimfolk's words, which of Rimfolk's camps and asks come from it more often, and its towns'
    /// plan and stores. The faction def itself holds the rest the game reads (its gear, colours, faith, kind of people,
    /// traders and names). Release B (peoples that arise during play) makes new peoples from these templates.
    /// </summary>
    public class PeopleExtension : DefModExtension
    {
        /// <summary>What they live by: traders, healers or herders (PeoplesMath.Callings): the tag their lines are written for.</summary>
        public string calling;
        /// <summary>One of their towns and many ("waystation", "waystations").</summary>
        public string townWord, townWords;
        /// <summary>Their notables' titles: the town's head (and a woman who heads it), the one to follow, their envoy, their trader.</summary>
        public string head, headFemale, heir, heirFemale, envoy, trader;
        /// <summary>Their messengers, one and many ("road rider", "road riders"): the colony's letters' carriers' word for them.</summary>
        public string courier, couriers;
        /// <summary>How much likelier their trader caravans are the ones that come when the game sends one (the league's 3: many caravans).</summary>
        public float caravanWeight = 1f;
        /// <summary>Kinds of Rimfolk's camps they send more often (their drovers, their healers on the road).</summary>
        public List<CampKindDef> camps;
        /// <summary>Their towns' plan, placed when the game makes a town's map; none: the game's own base.</summary>
        public TownPlanDef plan;
        /// <summary>What their towns keep in their stores besides the game's own loot (the order's medicine, the herders' hay).</summary>
        public List<ThingDefCountRangeClass> townGoods;
        /// <summary>The beasts in their towns' pens, and how many.</summary>
        public List<PawnKindDef> herd;
        public IntRange herdCount = IntRange.Zero;
        /// <summary>
        /// The ages (0 tribal to 3 spacer) a people of this template arises in during play (Peoples/Arising.cs), when it
        /// is wider than its own: the river folk "0~2", the pilgrims and the scholars "1~3". Unset: its own age.
        /// </summary>
        public IntRange ariseAges = new IntRange(-1, -1);
        /// <summary>Their towns stand by a river where the world has one (the river folk's landings), and its docks reach into the water.</summary>
        public bool byWater;
        /// <summary>Fresh fish of the land's own waters in their towns' stores, this many (Odyssey's fish; none without it).</summary>
        public IntRange fish = IntRange.Zero;
        /// <summary>
        /// What they honour and what they fear, in a few plain words each ("the spared, clean hands and the night watch"), and
        /// their sayings (one sentence each): shown on their card (Peoples/PeoplesLore.cs) and the voice their lines are
        /// written in. Their founding story is their faction's description.
        /// </summary>
        [MustTranslate] public string honours, fears;
        [MustTranslate] public List<string> sayings;

        /// <summary>The ages a people of this template arises in, for its def's own age (PeoplesMath.AgeSpan).</summary>
        public void AriseSpan(int own, out int lo, out int hi) => PeoplesMath.AgeSpan(own, ariseAges.min, ariseAges.max, out lo, out hi);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!PeoplesMath.IsCalling(calling)) yield return "a people's calling is one of " + string.Join(", ", PeoplesMath.Callings) + ", not '" + calling + "'";
            if (head.NullOrEmpty() || courier.NullOrEmpty() || couriers.NullOrEmpty() || townWord.NullOrEmpty()) yield return "a people needs its head's title, its town's word and its messengers' word";
            if (caravanWeight <= 0f) yield return "caravanWeight must be above 0";
            if (ariseAges.min > ariseAges.max || ariseAges.max > 3) yield return "ariseAges is a span of the ages 0 (tribal) to 3 (spacer)";
            if (sayings != null && sayings.Any(s => s.NullOrEmpty())) yield return "a people's saying is a sentence, not empty";
        }
    }

    /// <summary>
    /// What a share of one of Rimfolk's peoples' pawn kinds is born or trained to, beyond their gear (Peoples/PeopleGifts.cs,
    /// given as a new pawn of the kind is made, with the deeper game hooks on): a class of A RimWorld of Magic (a
    /// hospitaller's priest, a warden's paladin, a river folk's druid), a psylink (Royalty; with Vanilla Psycasts
    /// Expanded, one of its paths), or a gene or two put in (Biotech, for the peoples whose age has the means). At most one
    /// gift a pawn; the shares never add up to the whole kind. Named by their defNames, so a gift whose mod isn't loaded is
    /// simply never given. Pawns already in a game keep what they have.
    /// </summary>
    public class PeopleKindExtension : DefModExtension
    {
        public List<PeopleGift> gifts;

        /// <summary>
        /// What a kind carries in another age than its people's own (release D: the Wardens' hunting rifles in the
        /// industrial age, the Quiet Bell's flamers for what will not stay down): the first set whose ages hold the age
        /// its people lives in (Peoples.AgeOf: a people that arose keeps the age it came from) takes the place of the
        /// weapon the game gave it, from these weapon tags within the kind's means. A people in its own age keeps the
        /// game's choice.
        /// </summary>
        public List<PeopleAgeArms> ageArms;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (gifts.NullOrEmpty() && ageArms.NullOrEmpty()) yield return "a people's kind with gifts names at least one, or arms for another age";
            // A set's tags may all be a mod's (MayRequire) and so none without it: the set then arms nobody, no error.
            if (ageArms != null)
                foreach (var a in ageArms.Where(a => a != null))
                    if (a.weaponTags == null || PeoplesDMath.AnyAge(a.ages.min, a.ages.max) || a.ages.max > 3) yield return "arms for another age name its ages (0 to 3) and their weapon tags";
            if (gifts.NullOrEmpty()) yield break;
            if (!PeoplesGearMath.Sound(gifts.Select(g => g?.chance ?? 0f).ToList()))
                yield return "each gift's chance is above 0, and together no more than " + PeoplesGearMath.MaxShare;
            if (gifts != null)
                foreach (var g in gifts.Where(g => g != null))
                {
                    if (PeoplesGearMath.GiftKinds(!g.trait.NullOrEmpty(), g.psylink.max > 0, !g.genes.NullOrEmpty()) != 1)
                        yield return "a gift is one of a class (trait), a psylink (psylink levels) or genes, not two or none";
                    if (!g.trait.NullOrEmpty() && PeoplesGearMath.MagicSetting(g.trait) == null) yield return "'" + g.trait + "' isn't one of A RimWorld of Magic's classes Rimfolk gives";
                }
        }
    }

    /// <summary>One gift of a pawn kind (<see cref="PeopleKindExtension"/>).</summary>
    public class PeopleGift
    {
        /// <summary>A RimWorld of Magic's class, its trait's defName (PeoplesGearMath.MagicClasses).</summary>
        public string trait;
        /// <summary>A psylink of these levels (Royalty), or none.</summary>
        public IntRange psylink = IntRange.Zero;
        /// <summary>With Vanilla Psycasts Expanded: the paths a psycaster of this kind follows (by defName), one taken.</summary>
        public List<string> paths;
        /// <summary>Genes put in (Biotech, by defName, the game's or a mod's), given to a baseliner as xenogenes; or none.</summary>
        public List<string> genes;
        /// <summary>The share of the kind given this, 0 to 1.</summary>
        public float chance;
        /// <summary>Only in these ages of its people (0 tribal to 3 spacer: the tamers' Wildspeakers in the spacer age); unset, every age.</summary>
        public IntRange ages = new IntRange(-1, -1);
    }

    /// <summary>Arms for another age (<see cref="PeopleKindExtension.ageArms"/>): the ages, and the weapon tags they choose from.</summary>
    public class PeopleAgeArms
    {
        public IntRange ages = new IntRange(-1, -1);
        public List<string> weaponTags;

        /// <summary>
        /// Release E's issued kit of that age (the Ashen Banner's flak and helmets in the industrial age, recon armour in the
        /// spacer age): each piece worn in place of whatever it can't be worn with, in the kind's colour for it. A piece
        /// of a missing mod (MayRequire) is simply not in the list.
        /// </summary>
        public List<ThingDef> apparel;

        /// <summary>The most a weapon of this set may be worth; unset, the kind's own weapon budget and a quarter more.</summary>
        public float money = -1f;
    }

    /// <summary>
    /// A town's plan (Defs/PeopleDefs/Rimfolk_TownPlans.xml): rows of characters from the north, each character a key
    /// (a building, its material and turn, a floor, a roof, what the cell is for). Read into the kept-layout grid and placed
    /// by the same code that places a town the colony was (PeoplesMath.Grid, TownLayouts.Place). An underscore is the
    /// ground as it is.
    /// </summary>
    public class TownPlanDef : Def
    {
        public List<string> rows = new List<string>();
        public List<TownPlanKey> keys = new List<TownPlanKey>();
        /// <summary>What a key's "wall" material is, the first the game has: stone blocks, logs, steel.</summary>
        public List<ThingDef> wallStuff;

        public int Width => rows.NullOrEmpty() ? 0 : rows[0].Length;

        public int Height => rows?.Count ?? 0;

        /// <summary>The plan's keys as plain words, with the town's wall material for the keys made of it.</summary>
        public List<PlanKey> Words(ThingDef wall) =>
            keys.Where(k => k != null && !k.key.NullOrEmpty()).Select(k => new PlanKey
            {
                key = k.key[0], thing = k.thing?.defName, stuff = k.wall ? wall?.defName : k.stuff?.defName, rot = k.rot.AsInt,
                sizeX = k.thing?.size.x ?? 1, sizeZ = k.thing?.size.z ?? 1, floor = k.floor?.defName, roof = k.roof, mark = k.mark
            }).ToList();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            foreach (var k in keys)
                if (k?.key == null || k.key.Length != 1) yield return "a plan's key is one character";
            foreach (var e in PeoplesMath.Check(rows, Words(wallStuff?.FirstOrDefault()))) yield return e;
        }
    }

    /// <summary>One character of a town's plan.</summary>
    public class TownPlanKey
    {
        public string key;
        public ThingDef thing;
        public ThingDef stuff;
        /// <summary>Made of the town's wall material (TownPlanDef.wallStuff) rather than <see cref="stuff"/>.</summary>
        public bool wall;
        public Rot4 rot = Rot4.North;
        public TerrainDef floor;
        public bool roof;
        /// <summary>"store", "pen", "plant:DEFNAME" or "dock" (a jetty over the water where the river runs, a landing of planks on dry ground: PlanKey.mark).</summary>
        public string mark;
    }
}
