using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A gift answered in kind (the player, 16 November 2026: "sometimes a return in kind sounds great"): now and then, after
    /// an offering is taken and someone sees it go or finds the place empty, something of the god's own lies where it lay,
    /// worth about what was given (OfferingGiftMath.ReturnChance, ReturnWorth: more often and more for a rich or well-liked
    /// gift, about half the time and up to half again for a precious one asked of the colony first), of the god's domain
    /// and the colony's age: Hollin's wild food of the land and herbs, Smoulder's ore (steel, silver, gold from the medieval
    /// age, uranium and plasteel later, the player's mods' metals mined from rock), Bellwether's hay, milk and wool, Morel's
    /// mushrooms (the player's mods' too), Ennu's food for the road and cloth, Pip's steel and, with machines, parts and
    /// chemfuel, Hush's hides of the land's animals, Selene's medicine by the age and silver, Murmur's fish (Odyssey's,
    /// Vanilla Fishing Expanded's) and rice, Ducat's silver and gold, Revel's drink and smoke of the colony's own kind (never a
    /// hard drug). Cirrus, Undertow, Helios and Tamiu give nothing back
    /// in kind. It is the god's answer to that gift: shown with the god's own look and sound, the camera on it, a message of
    /// what was found where, the finder's own words, and the colony's book (as those who saw it saw it, and on the gift's
    /// line). A god once in ten days at a home, any god once in three; a precious gift whenever.
    /// </summary>
    public static class GodTouchReturn
    {
        private static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

        /// <summary>Dev mode: the next taking is answered in kind, whatever its chance and wait.</summary>
        internal static bool Forced;

        /// <summary>The taking's answer in kind, when its chance and waits allow and the god has something to give; false when there is none.</summary>
        internal static bool Try(GodTouchBook b, GodTouch.Pending p, List<Pawn> ours, int home, int now)
        {
            var gift = p?.gift;
            if (b == null || gift == null || p.god == null || p.map == null) return false;
            try
            {
                string god = p.god.Key;
                if (!Forced)
                {
                    if (!OfferingGiftMath.ReturnAllowed(gift.precious, b.DaysSince("r|" + god + "|" + home, now), b.DaysSince("rc|" + home, now))) return false;
                    if (!Rand.Chance(OfferingGiftMath.ReturnChance(OfferingGiftMath.Lean(gift.worth, gift.fit, gift.precious), gift.precious))) return false;
                }
                var goods = Goods(p.god, p.map);
                if (goods.Count == 0) return false;
                float worth = OfferingGiftMath.ReturnWorth(gift.worth, gift.precious, Rand.Value);
                int i = OfferingGiftMath.PickReturn(goods.Select(x => x.def.BaseMarketValue).ToList(), goods.Select(x => x.weight).ToList(), worth, Rand.Value);
                if (i < 0) return false;
                var def = goods[i].def;
                var t = ThingMaker.MakeThing(def);
                t.stackCount = OfferingGiftMath.ReturnCount(worth, def.BaseMarketValue, def.stackLimit);
                var at = Place(p, ours);
                if (!GenPlace.TryPlaceThing(t, at, p.map, ThingPlaceMode.Near, out var placed) || placed == null) return false;
                Show(p, ours, gift, placed);
                GodPlaces.LeaveKeepsake(p.taken ?? p.god, p.map, placed.Position, placed.LabelShort);
                b.Set("r|" + god + "|" + home, now);
                b.Set("rc|" + home, now);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a gift answered in kind", e);
                return false;
            }
        }

        /// <summary>Where the gift lay, or where whoever saw it stands.</summary>
        private static IntVec3 Place(GodTouch.Pending p, List<Pawn> ours)
        {
            if (p.cell.IsValid && p.cell.InBounds(p.map)) return p.cell;
            var near = ours.FirstOrDefault(x => x.Spawned && x.Map == p.map);
            return near?.Position ?? p.map.Center;
        }

        /// <summary>The god's look and sound where it lies, the camera on it, what was found where, the finder's words, and the book.</summary>
        private static void Show(GodTouch.Pending p, List<Pawn> ours, TakenGift gift, Thing placed)
        {
            var map = p.map;
            var cell = placed.Position;
            var finder = ours.Where(x => x.Spawned && x.Map == map).OrderBy(x => x.Position.DistanceToSquared(cell)).FirstOrDefault();
            GodPresence.Look(map, cell);
            GodTouchFx.Show(p.god, map, cell, TouchTier.Notable, false);
            GodTouchFx.Sound(p.god, map, cell);
            string what = placed.Label;
            string place = Worship.ShrineOf(p.god);
            string text = (finder != null
                    ? "CC_GodGift_ReturnFound".Translate(finder.LabelShort.Named("PAWN"), what.Named("WHAT"), place.Named("PLACE"))
                    : "CC_GodGift_ReturnLeft".Translate(what.Named("WHAT"), place.Named("PLACE")))
                .Resolve().CapitalizeFirst();
            Tell.Message(NewsKind.Land, Importance.Important, map, text, new LookTargets(placed), MessageTypeDefOf.PositiveEvent);
            if (finder != null)
            {
                // Whose doing they take it for, in their own words: the place's god for one who holds it, as some say for the rest.
                string line = Worship.Line("Rimfolk_Pool_Worship_ReturnFound", finder, p.god, null,
                    Worship.GodOf(finder) == p.god ? "mine" : "theirs");
                if (!line.NullOrEmpty()) Speech.Say(finder, line);
            }
            var ctx = new TouchContext(p.god, map, cell, ours, p.taken, p.plain, TouchTier.Notable, false);
            GodTouch.Write(ctx, GodActKind.Blessing, text);
            GodLore.Returned(gift.lore, what);
        }

        // ================= Each god's goods =================

        /// <summary>
        /// What a god gives back in kind, with how much each is weighed, by the colony's age (0 tribal to 3 spacer) and the
        /// player's mods: none for a god that gives nothing back in kind.
        /// </summary>
        internal static List<(ThingDef def, float weight)> Goods(God g, Map map)
        {
            var list = new List<(ThingDef def, float weight)>();
            int age = (int)Ages.Ours;
            void Add(ThingDef d, float w)
            {
                if (d == null || w <= 0f || d.category != ThingCategory.Item || d.MadeFromStuff || d.BaseMarketValue <= 0f || list.Any(x => x.def == d)) return;
                list.Add((d, w));
            }
            switch (g?.Key)
            {
                case "Hollin":
                    foreach (var plant in map?.Biome?.AllWildPlants ?? Enumerable.Empty<ThingDef>())
                    {
                        var h = plant?.plant?.harvestedThingDef;
                        if (h != null && h.IsNutritionGivingIngestible && !h.IsDrug && h.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw)) Add(h, 1f);
                    }
                    Add(Thing("MedicineHerbal"), 1f);
                    break;
                case "Smoulder":
                    Add(ThingDefOf.Steel, 3f);
                    Add(ThingDefOf.Silver, 2f);
                    if (age >= 1) Add(ThingDefOf.Gold, 1f);
                    if (age >= 2) Add(ThingDefOf.Uranium, 0.5f);
                    if (age >= 3) Add(ThingDefOf.Plasteel, 0.5f);
                    foreach (var d in Ores())
                        if ((int)d.techLevel <= (int)TechLevel.Neolithic + age)
                            Add(d, 0.5f);
                    break;
                case "Bellwether":
                    Add(Thing("Hay"), 2f);
                    Add(Thing("Milk"), 1f);
                    foreach (var d in Wools()) Add(d, 0.5f);
                    break;
                case "Morel":
                    Add(Thing("RawFungus"), 2f);
                    foreach (var d in Mushrooms()) Add(d, 1f);
                    break;
                case "Ennu":
                    Add(age <= 1 ? Thing("Pemmican") : Thing("MealSurvivalPack"), 2f);
                    Add(Thing("Cloth"), 1f);
                    break;
                case "Pip":
                    Add(ThingDefOf.Steel, 2f);
                    if (age >= 2)
                    {
                        Add(ThingDefOf.ComponentIndustrial, 2f);
                        Add(Thing("Chemfuel"), 1f);
                    }
                    if (age >= 3) Add(ThingDefOf.ComponentSpacer, 0.4f);
                    break;
                case "Hush":
                    if (map?.Biome != null)
                        foreach (var k in BiomeAnimals.Of(map.Biome))
                            Add(k?.race?.race?.leatherDef, 1f);
                    break;
                case "Selene":
                    Add(Thing("MedicineHerbal"), age <= 1 ? 2f : 0.5f);
                    if (age >= 2) Add(Thing("MedicineIndustrial"), 1.5f);
                    if (age >= 3) Add(Thing("MedicineUltratech"), 0.3f);
                    Add(ThingDefOf.Silver, 1f);
                    break;
                case "Murmur":
                    foreach (var d in Fish()) Add(d, 1f);
                    Add(Thing("RawRice"), 1f);
                    break;
                case "Ducat":
                    Add(ThingDefOf.Silver, 3f);
                    if (age >= 1) Add(ThingDefOf.Gold, 1f);
                    break;
                case "Revel":
                    // Drink and smoke of the colony's own kind, every mod's, never a hard drug: what it can make again first.
                    foreach (var d in RevelGoods.All.Where(x => !RevelGoods.Hard(x) && RevelGoods.OfAge(x)))
                        Add(d, map != null && RevelGoods.Unlocked(d, map) ? 2f : RevelGoods.Drink(d) ? 0.6f : 0.4f);
                    Add(ThingDefOf.Beer, 1f);
                    break;
            }
            return list;
        }

        private static List<ThingDef> ores, wools, mushrooms, fish;

        /// <summary>The metals mined from rock, every mod's: what a mineable rock yields that is a raw metallic material.</summary>
        private static List<ThingDef> Ores() => ores ??= DefDatabase<ThingDef>.AllDefsListForReading
            .Select(d => d?.building?.mineableThing)
            .Where(d => d != null && d.IsStuff && d.stuffProps.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)
                        && d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw))
            .Distinct().ToList();

        /// <summary>Wool of every kind, every mod's.</summary>
        private static List<ThingDef> Wools() => wools ??= ThingCategoryDefOf.Wools == null
            ? new List<ThingDef>()
            : DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d != null && d.category == ThingCategory.Item && d.IsWithinCategory(ThingCategoryDefOf.Wools)).ToList();

        /// <summary>
        /// Raw mushrooms and fungus of every mod's, eaten as plant food (Alpha Biomes' and Alpha Animals' by their names), and
        /// Vanilla Plants Expanded - Mushrooms' raw blisscap and bileceps by name.
        /// </summary>
        private static List<ThingDef> Mushrooms()
        {
            if (mushrooms != null) return mushrooms;
            mushrooms = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d != null && d.category == ThingCategory.Item && d.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw) && !d.IsDrug
                            && (Has(d.defName, "fungus") || Has(d.defName, "mushroom") || Has(d.defName, "shroom")))
                .ToList();
            foreach (var n in new[] { "VPE_Blisscap", "VPE_Bileceps" })
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null && d.category == ThingCategory.Item && !mushrooms.Contains(d)) mushrooms.Add(d);
            }
            return mushrooms;
        }

        /// <summary>Fish, every mod's in the game's own category (Odyssey's, Vanilla Fishing Expanded's).</summary>
        private static List<ThingDef> Fish() => fish ??= ThingCategoryDefOf.Fish == null
            ? new List<ThingDef>()
            : DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d != null && d.category == ThingCategory.Item && d.IsWithinCategory(ThingCategoryDefOf.Fish)).ToList();

        private static bool Has(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
