using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Content round 5, the colony out in the world (docs/content-round-5-away.md; texts in
    // Defs/MissionDefs/Rimfolk_Round5_Missions.xml, the pure rules in Round5AwayMath.cs). Five of its six missions are told
    // from data on the frames there are (hands lent to a town: the round-up, the carver, the cook; a captive walked home;
    // the bell, a delivery). The sixth, the order from a neighbour's merchants, is a delivery whose goods are picked here
    // from what the colony already keeps in its stores, of the game's or any mod's; its carts, checks and endings are the
    // delivery's own (Workers_MissionDelivery.cs).

    /// <summary>One of the colony's goods an order may be for: a goal's group or a thing's defName, how many it keeps, what one is worth.</summary>
    public struct R5AGood
    {
        public string what;
        public int have;
        public float unit;

        public R5AGood(string what, int have, float unit)
        {
            this.what = what;
            this.have = have;
            this.unit = unit;
        }
    }

    /// <summary>What a neighbour's merchants could order from the colony: what it keeps enough of in its stores.</summary>
    public static class R5AOrders
    {
        /// <summary>Everything a recipe of the game's or a mod's makes, worked out once.</summary>
        private static HashSet<ThingDef> made;

        private static HashSet<ThingDef> Made
        {
            get
            {
                if (made != null) return made;
                made = new HashSet<ThingDef>();
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (r?.products == null) continue;
                    foreach (var p in r.products)
                        if (p?.thingDef != null)
                            made.Add(p.thingDef);
                }
                return made;
            }
        }

        /// <summary>
        /// One thing the colony makes that merchants buy by the crate: an item a recipe makes, that stacks and can be sold.
        /// Never a weapon, clothes, a drug, silver or a raw resource, nor what one of the order's groups already counts
        /// (meals, cloth and leather, medicine, drink, stone blocks).
        /// </summary>
        public static bool Sellable(ThingDef d)
        {
            if (d == null || d.category != ThingCategory.Item || d.stackLimit <= 1 || d.BaseMarketValue < 0.5f || d == ThingDefOf.Silver) return false;
            if (d.IsWeapon || d.IsApparel || d.IsDrug || d.IsCorpse || d.IsMedicine || d.destroyOnDrop || !d.PlayerAcquirable || !d.tradeability.PlayerCanSell()) return false;
            var cats = d.thingCategories;
            if (cats != null && (cats.Contains(ThingCategoryDefOf.ResourcesRaw) || cats.Contains(ThingCategoryDefOf.Textiles) || cats.Contains(ThingCategoryDefOf.StoneBlocks))) return false;
            if (d.ingestible != null && d.IsNutritionGivingIngestible && d.ingestible.preferability >= FoodPreferability.MealSimple) return false;
            if (ThingRoles.Fits(ThingRole.Drink, d) || ThingRoles.Fits(ThingRole.SoftDrink, d)) return false;
            return Made.Contains(d);
        }

        /// <summary>What the colony keeps enough of for an order: the groups first, then each thing it makes, in its stores and not forbidden.</summary>
        public static List<R5AGood> Goods(Map map)
        {
            var list = new List<R5AGood>();
            if (map == null) return list;
            try
            {
                foreach (var g in Round5AwayMath.OrderGroups)
                {
                    int have = Missions.Count(map, g);
                    float unit = Missions.UnitValue(g);
                    if (Round5AwayMath.OrderWorth(have, unit)) list.Add(new R5AGood(g, have, unit));
                }
                var counts = new Dictionary<ThingDef, int>();
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                {
                    if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || !Sellable(t.def) || !t.IsInAnyStorage()) continue;
                    counts[t.def] = (counts.TryGetValue(t.def, out int c) ? c : 0) + t.stackCount;
                }
                foreach (var kv in counts)
                {
                    float unit = Missions.UnitValue(kv.Key.defName);
                    if (Round5AwayMath.OrderWorth(kv.Value, unit)) list.Add(new R5AGood(kv.Key.defName, kv.Value, unit));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 5 order goods", e);
            }
            return list;
        }
    }

    /// <summary>
    /// R5A-M3, an order from their merchants: a friendly people's merchants have heard what the colony keeps and order about
    /// as much of it as it has, give or take a third, at half again its plain worth. Their carts come to the gate for it once
    /// it is all in the stores, or when the time is up, as for every delivery (all of it well paid, half of it at its plain
    /// worth, less and they remember).
    /// </summary>
    public class HappeningWorker_R5AOrder : HappeningWorker_MissionDelivery
    {
        private MissionDelivery D => def.GetModExtension<MissionDelivery>();

        protected override bool Possible(Map map) => D != null && Neighbor(map) != null && R5AOrders.Goods(map).Count > 0;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var d = D;
            var map = ctx.map;
            if (d == null || map == null) return false;
            var goods = R5AOrders.Goods(map);
            if (goods.Count == 0) return false;
            var pick = goods.RandomElementByWeight(g => Mathf.Max(0.01f, Round5AwayMath.OrderWeight(g.have, g.unit)));
            int n = Round5AwayMath.OrderCount(pick.have, pick.unit, Rand.Value, LivingEvents.WealthFactor(map));
            if (n <= 0) return false;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            // In the name of the town's head, by their title, when the region's ledger knows them ({HOST}, [host]).
            m.host = InviteForms.Host(MissionPeoples.Town(m.faction, map), out m.royalHost);
            // The reeve deals with neighbours who want goods, when there is one (Interplay/).
            m.a = Interplay.MissionHead(map) ?? VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.variant = Round5AwayMath.OrderTag(pick.what);
            m.goals.Add(new MissionGoal { what = pick.what, count = n });
            m.reward = Round5AwayMath.OrderPay(n * pick.unit);
            m.goodwill = d.goodwill;
            m.penalty = d.penalty;
            m.days = d.days.RandomInRange;
            Fill(m, ctx);
            return true;
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? base.Status(m) : "CC_R5A_Order_Waiting".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
    }
}
