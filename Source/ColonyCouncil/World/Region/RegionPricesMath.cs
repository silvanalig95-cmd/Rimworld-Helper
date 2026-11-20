using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a town in a hard or a good year pays more (or asks less) for.</summary>
    public enum RegionGoods : byte
    {
        None,
        Food,
        Medicine,
        Steel,
        Wood,
        /// <summary>Cloth and leather: the fabrics and the leathers, the game's and the mods'.</summary>
        Textiles
    }

    /// <summary>
    /// The region's years in the traders' prices (docs/content-update-proposals.md, B5): while a world event runs at a
    /// people's town, its traders (their caravans, their visitors' traders, their towns) pay more for what the town lacks and
    /// ask more for it too, and in a bumper year sell food cheaper. Food in a famine, a blight, a locust year, a year without a
    /// summer or a drought; medicine in a plague or with medicine gone scarce; steel with iron gone scarce or in a war
    /// between neighbours (both sides); wood after a great fire, a flood, during rebuilding or a great work; cloth and leather
    /// in a sickness of the herds or with the markets closed (both sides); and (World/Variety/) cloth and leather with cloth
    /// gone scarce, wood with timber gone scarce or a bridge down. The colony's buying and selling price move
    /// together, as with the moot's season prices (Peoples/Events10/, <see cref="PeopleEvents10Math.Priced"/>), and the two
    /// are one factor on the price, kept between half and one and a half. Pure (no game types): Tools/Tests checks it.
    /// </summary>
    public static class RegionPricesMath
    {
        /// <summary>What the town lacks costs a quarter more; a bumper year's food a fifth less.</summary>
        public const float Dear = 1.25f, Cheap = 0.8f;

        /// <summary>The prices, with the moot's season price on top, never past these.</summary>
        public const float Min = 0.5f, Max = 1.5f;

        private static readonly Dictionary<string, RegionGoods> Lacks = new Dictionary<string, RegionGoods>
        {
            ["Council_WS_Famine"] = RegionGoods.Food,
            ["Rimfolk_WS_Blight"] = RegionGoods.Food,
            ["Council_WS_Locusts"] = RegionGoods.Food,
            ["Rimfolk_WS_ColdYear"] = RegionGoods.Food,
            ["Council_WS_Drought"] = RegionGoods.Food,
            ["Council_WS_Plague"] = RegionGoods.Medicine,
            ["Rimfolk_WS_MedicineScarce"] = RegionGoods.Medicine,
            ["Council_WS_IronScarce"] = RegionGoods.Steel,
            ["Rimfolk_WS_NeighborsWar"] = RegionGoods.Steel,
            ["Council_WS_GreatFire"] = RegionGoods.Wood,
            ["Council_WS_Flood"] = RegionGoods.Wood,
            ["Council_WS_Rebuilding"] = RegionGoods.Wood,
            ["Rimfolk_WS_GreatWork"] = RegionGoods.Wood,
            ["Council_WS_Murrain"] = RegionGoods.Textiles,
            ["Council_WS_Embargo"] = RegionGoods.Textiles,
            // The content update's variety (World/Variety/): the two scarcities, and a bridge to build again.
            ["Rimfolk_WS_ClothScarce"] = RegionGoods.Textiles,
            ["Rimfolk_WS_TimberScarce"] = RegionGoods.Wood,
            ["Rimfolk_WS_BridgeDown"] = RegionGoods.Wood
        };

        /// <summary>The bumper year: food cheaper at their town.</summary>
        public const string Bumper = "Council_WS_Bumper";

        /// <summary>Every world event that moves a price (its defName), for the tests and the Almanac.</summary>
        public static IEnumerable<string> Events
        {
            get
            {
                foreach (var k in Lacks.Keys) yield return k;
                yield return Bumper;
            }
        }

        /// <summary>What a world event (its defName) makes dear or cheap at its town, and which way (<paramref name="dear"/>); None for one that moves no price.</summary>
        public static RegionGoods Goods(string situation, out bool dear)
        {
            dear = true;
            if (situation == null) return RegionGoods.None;
            if (situation == Bumper)
            {
                dear = false;
                return RegionGoods.Food;
            }
            return Lacks.TryGetValue(situation, out var g) ? g : RegionGoods.None;
        }

        /// <summary>A war between neighbours and closed markets hurt both peoples: the second people's towns too (the event's {ENEMY}).</summary>
        public static bool BothSides(string situation) => situation == "Rimfolk_WS_NeighborsWar" || situation == "Council_WS_Embargo";

        /// <summary>The factor a world event puts on the price of <paramref name="goods"/> at its people's towns (1 for none).</summary>
        public static float Factor(string situation, RegionGoods goods)
        {
            if (goods == RegionGoods.None) return 1f;
            var g = Goods(situation, out bool dear);
            return g != goods ? 1f : dear ? Dear : Cheap;
        }

        /// <summary>Several world events at one people's towns: their factors together, within <see cref="Min"/> and <see cref="Max"/>.</summary>
        public static float Factor(IEnumerable<string> situations, RegionGoods goods)
        {
            float f = 1f;
            if (situations != null)
                foreach (var s in situations)
                    f *= Factor(s, goods);
            return Clamp(f);
        }

        /// <summary>The moot's season price and the region's year as one factor on the price, within <see cref="Min"/> and <see cref="Max"/>.</summary>
        public static float Combined(float moot, float region) => Clamp((float.IsNaN(moot) ? 1f : moot) * (float.IsNaN(region) ? 1f : region));

        /// <summary>"25" for 1.25, "20" for 0.8: how much dearer or cheaper, in whole percent.</summary>
        public static int Percent(float factor) => (int)Math.Round(Math.Abs(factor - 1f) * 100f);

        /// <summary>
        /// What kind of goods a thing is, from what the game says of it: medicine first (a herbal medicine is no food), then
        /// food people eat (raw or cooked, not a drug, not a corpse, not only for the desperate), steel, wood (any woody stuff),
        /// cloth and leather (any fabric or leathery stuff).
        /// </summary>
        public static RegionGoods Kind(bool medicine, bool food, bool steel, bool woody, bool fabricOrLeather)
        {
            if (medicine) return RegionGoods.Medicine;
            if (food) return RegionGoods.Food;
            if (steel) return RegionGoods.Steel;
            if (woody) return RegionGoods.Wood;
            return fabricOrLeather ? RegionGoods.Textiles : RegionGoods.None;
        }

        private static float Clamp(float f) => f < Min ? Min : f > Max ? Max : f;
    }
}
