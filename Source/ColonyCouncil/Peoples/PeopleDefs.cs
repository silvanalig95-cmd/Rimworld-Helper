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

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!PeoplesMath.IsCalling(calling)) yield return "a people's calling is one of " + string.Join(", ", PeoplesMath.Callings) + ", not '" + calling + "'";
            if (head.NullOrEmpty() || courier.NullOrEmpty() || couriers.NullOrEmpty() || townWord.NullOrEmpty()) yield return "a people needs its head's title, its town's word and its messengers' word";
            if (caravanWeight <= 0f) yield return "caravanWeight must be above 0";
        }
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
        /// <summary>"store", "pen" or "plant:DEFNAME" (PlanKey.mark).</summary>
        public string mark;
    }
}
