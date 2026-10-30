using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Animals that come with a camp (CampKindDef.animals): a caravan's pack beasts tethered by the stash, a drover's
    /// herd grazing beyond the camp, the dog at a drover's heels. Real animals of the camp's people, in the camp's group:
    /// they walk in with them, are kept fed by them (never from the colony's fields) and leave with them. When things go
    /// wrong (the colony harms or arrests one of them, or their people go to war with it) the animals are walked off the
    /// map at once, so they never fight anyone. One kind per entry; all of a camp's entries together come to at most
    /// <see cref="CampLookMath.MaxAnimals"/>.
    /// </summary>
    public class CampAnimals
    {
        /// <summary>PawnKindDef names to choose from: those that can stand this weather, the ones that also live wild here likelier.</summary>
        public List<string> kinds = new List<string>();
        public IntRange count = new IntRange(1, 1);
        /// <summary>The pack beasts their own people's caravans use, if one suits this land and weather (else from <see cref="kinds"/>).</summary>
        public bool pack;
        /// <summary>Where they're kept: "picket" (tethered beside the stash, where porters load them) or "pasture" (grazing beyond the camp).</summary>
        public string at = "pasture";
        /// <summary>Stays at the heels of one of the camp's people (a dog), and sleeps by the fire.</summary>
        public bool follow;
        /// <summary>Named, as a dog is.</summary>
        public bool named;
        /// <summary>Pokémon (with PokéWorld): one of the roaming kinds, the ones the colony hasn't caught likelier (Integrations/PokeWorld.cs).</summary>
        public bool pokemon;

        public bool Matches(Pawn a)
        {
            if (a?.kindDef == null) return false;
            if (kinds != null && kinds.Contains(a.kindDef.defName)) return true;
            if (pokemon && PokeWorldLink.IsPokemon(a)) return true;
            return pack && a.RaceProps.packAnimal;
        }
    }

    /// <summary>
    /// Goods a camp puts out once it stands (CampKindDef.campGoods): bales by the pack beasts, a woodpile by a hut's door,
    /// washing by the windbreak. Theirs: kept out of the colony's hands and packed up when they go, or left where they lie
    /// when they flee. Porters carry them now and then between two spots (<see cref="carryTo"/>).
    /// </summary>
    public class CampGoods
    {
        /// <summary>ThingDef names, the first that exists (an item).</summary>
        public List<string> defs = new List<string>();
        /// <summary>For things made of material (clothes): the first listed that fits.</summary>
        public List<string> stuff;
        /// <summary>How many: one stack of this size (things that don't stack come as this many pieces).</summary>
        public IntRange count = new IntRange(1, 1);
        /// <summary>Where they go: stash, fire, stall, picket, pasture, door (outside a hut's door), wall (the fire side of the windbreak), or any building role.</summary>
        public string at = "stash";
        /// <summary>Porters carry them now and then from where they lie to here, and back again.</summary>
        public string carryTo;
        /// <summary>Left for the colony when they pack up in peace (the hermit's woodpile, left with the hut).</summary>
        public bool leftForColony;

        public ThingDef Def => defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Item);

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

    /// <summary>
    /// What a camp leaves behind when it goes in peace at the end of its stay (CampKindDef.leaveBehind), as its farewell
    /// says: a ring of planted seeds where the fire was, a painted backdrop on the old stage, a carved bird by the
    /// colony's door, a sprig of green planted by its gate. Nobody's: it stays for the colony to keep or clear. Lines
    /// tagged [left:key] tell it.
    /// </summary>
    public class CampGift
    {
        public string key;
        /// <summary>
        /// Where: "ring" (round where their fire was), "spot" (where the building of their daily task stood: the stage),
        /// "gate" (the colony's door nearest the camp, laid there at the end of a farewell walk, see <see cref="CampWalk.gift"/>).
        /// </summary>
        public string at = "ring";
        /// <summary>ThingDef names, the first that exists and can go there: a work of art, or a plant.</summary>
        public List<string> defs;
        public List<string> stuff;
        public IntRange count = new IntRange(1, 1);
        /// <summary>Saplings of the trees that grow wild on this land (instead of <see cref="defs"/>).</summary>
        public bool wildTrees;
        /// <summary>How far grown a plant is when it goes in.</summary>
        public float growth = 0.05f;
        /// <summary>Packed in a crate, for the colony to set up where it likes (a carving).</summary>
        public bool minified;
        /// <summary>A work of art's title (one of these).</summary>
        [MustTranslate] public List<string> titles;
        /// <summary>
        /// The day it marks on the colony's calendar, as its name ("the hermit's bird"), and what the calendar's lines
        /// call the gift ("the carved bird"). Without them, the calendar names the day after the camp.
        /// </summary>
        [MustTranslate] public string dayName;
        [MustTranslate] public string named;

        public IEnumerable<ThingDef> Defs => defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null) ?? Enumerable.Empty<ThingDef>();
    }

    /// <summary>
    /// A last walk before a camp goes at the end of its stay (CampKindDef.farewellWalk): the healer's round past the
    /// colony's sick and those they treated, or one of them going to the colony's gate to leave a gift. They go once it's
    /// done, or after a few hours whatever happens. Lines tagged [round] or [left:key] tell it in the farewell.
    /// </summary>
    public class CampWalk
    {
        /// <summary>"sick": the colony's sick and hurt, then those they've spent time with; "gate": the colony's door nearest the camp.</summary>
        public string to = "sick";
        /// <summary>How many people they look in on, at most.</summary>
        public int count = 4;
        /// <summary>Who walks: "leader", or "healer" (the best at medicine).</summary>
        public string who = "leader";
        /// <summary>The key of the gift (leaveBehind) laid at the end of the walk.</summary>
        public string gift;
    }
}
