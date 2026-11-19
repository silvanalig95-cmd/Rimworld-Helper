using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 23. A contract to the last digit (the spacer enclave) =====================

    /// <summary>
    /// A contract to the last digit (item 23): the enclave's factor offers a contract for something the colony makes (its
    /// meals, cloth, beer, a drug it makes, its art, its components), due on a set day, at a fifth over its worth. On the day
    /// their lander comes down at the trade spot (in a tribal or medieval colony a runner and porters walk in) and the colony
    /// carries the goods out to their marshals. Delivered in full: paid, and a bigger contract follows. Short or late is a
    /// default: a fee, cooler terms for a while, and the next contract back to the first size.
    /// </summary>
    public static class EnclaveContract
    {
        public const string DueEvent = "Rimfolk_Evt_ContractDue";

        private static int Now => Find.TickManager.TicksGame;

        private static IEnumerable<Building_WorkTable> Benches(Map map) => map?.listerBuildings?.allBuildingsColonist.OfType<Building_WorkTable>() ?? Enumerable.Empty<Building_WorkTable>();

        /// <summary>The colony has a bench where a recipe it knows makes such a thing.</summary>
        private static bool MakesAt(Map map, Func<ThingDef, bool> product) =>
            Benches(map).Any(b => b.def.AllRecipes != null && b.def.AllRecipes.Any(r => r != null && r.AvailableNow && r.products != null && r.products.Any(p => p?.thingDef != null && product(p.thingDef))));

        /// <summary>The drug the colony makes (the game's or a mod's), the dearest it can.</summary>
        public static ThingDef Drug(Map map)
        {
            ThingDef best = null;
            foreach (var b in Benches(map))
                foreach (var r in b.def.AllRecipes ?? new List<RecipeDef>())
                {
                    if (r == null || !r.AvailableNow || r.products == null) continue;
                    foreach (var p in r.products)
                        if (p?.thingDef != null && p.thingDef.IsDrug && p.thingDef.BaseMarketValue > 1f && (best == null || p.thingDef.BaseMarketValue > best.BaseMarketValue))
                            best = p.thingDef;
                }
            return best;
        }

        /// <summary>
        /// What the colony makes of this kind: the errand's word for it (<paramref name="what"/>: "meals", "art", or a defName),
        /// the thing, and one's worth. False when it doesn't make it.
        /// </summary>
        public static bool Makes(Map map, string kind, out string what, out ThingDef def, out float unit)
        {
            what = null;
            def = null;
            unit = 0f;
            if (map == null) return false;
            switch (kind)
            {
                case "meals":
                    if (!MakesAt(map, d => d.IsNutritionGivingIngestible && d.ingestible.IsMeal)) return false;
                    what = "meals";
                    unit = ThingDefOf.MealSimple?.BaseMarketValue ?? 15f;
                    return true;
                case "cloth":
                    def = ThingDefOf.Cloth;
                    bool cotton = map.zoneManager.AllZones.OfType<Zone_Growing>().Any(z => z.GetPlantDefToGrow()?.plant?.harvestedThingDef == ThingDefOf.Cloth);
                    if (def == null || !cotton && VarietyUtil.InStores(map, def) < 40) return false;
                    break;
                case "beer":
                    def = DefDatabase<ThingDef>.GetNamedSilentFail("Beer");
                    if (def == null || !map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "FermentingBarrel")) return false;
                    break;
                case "drugs":
                    def = Drug(map);
                    if (def == null || def.defName == "Beer") return false;
                    break;
                case "art":
                    if (!MakesAt(map, d => d.HasComp(typeof(CompArt)) && d.category == ThingCategory.Building && d.Minifiable)) return false;
                    what = PlayCarry.Art;
                    unit = 250f;
                    return true;
                case "components":
                    def = ThingDefOf.ComponentIndustrial;
                    if (def == null || !MakesAt(map, d => d == ThingDefOf.ComponentIndustrial)) return false;
                    break;
                default:
                    return false;
            }
            what = def.defName;
            unit = def.BaseMarketValue;
            return unit > 0f;
        }

        /// <summary>The open contract (one at a time), if any.</summary>
        public static ContractRecord Open => PeopleEvents4Book.Instance?.contracts.FirstOrDefault(c => c != null && c.Open);

        public static ContractRecord ById(int id) => PeopleEvents4Book.Instance?.contracts.FirstOrDefault(c => c != null && c.id == id);

        /// <summary>The terms are cooler now (a default lately).</summary>
        public static bool Cool => PeopleEvents4Book.Instance is PeopleEvents4Book b && b.coolUntil >= 0 && Now < b.coolUntil;

        /// <summary>The goods as words ("forty meals" as "40 meals").</summary>
        public static string Words(string what, ThingDef def, int count) => def != null ? VarietyUtil.Bundle(def, count) : count + " " + PlayCarry.Label(what, def);

        /// <summary>
        /// The due day's hour: the lander's letter when it may be asked (the colony's people at home); a day past it with no
        /// letter, the contract goes by default.
        /// </summary>
        public static void Watch(int now)
        {
            var r = Open;
            if (r == null || r.due < 0 || now < r.due) return;
            if (r.faction == null || r.faction.defeated || r.faction.HostileTo(Faction.OfPlayer) || r.map == null || !Find.Maps.Contains(r.map))
            {
                r.done = true;
                r.state = "lapsed";
                return;
            }
            // Once their letter is out, its answer (or its running out) settles the contract.
            if (r.asked) return;
            if (Tell.CanAsk && PeopleEvents2.Fire(DueEvent, r.map, r.faction))
            {
                r.asked = true;
                return;
            }
            if (now - r.due > GenDate.TicksPerDay) Settle(r, "default", 0, null);
        }

        /// <summary>
        /// The contract settled: <paramref name="how"/> "kept" (paid in full, the next bigger), "short" (what came, at its
        /// worth, less the fee) or "default" (the fee owed, cooler terms); <paramref name="delivered"/> how many went aboard.
        /// </summary>
        public static void Settle(ContractRecord r, string how, int delivered, Pawn marshal)
        {
            var b = PeopleEvents4Book.Instance;
            if (r == null || b == null || r.done) return;
            r.done = true;
            r.state = how;
            var f = r.faction;
            var map = r.map;
            int fee = PeopleEvents4Math.DefaultFee(r.price);
            var near = marshal != null && marshal.Spawned ? marshal.Position : IntVec3.Invalid;
            if (how == "kept")
            {
                Couriers.PaySilver(map, near, r.price, f, PeoplesG.Up(marshal, map) ? marshal : null);
                b.contractScale = PeopleEvents4Math.NextScale(b.contractScale, true);
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.KeptGoodwill, GoodwillWhy.Paid);
                if (f != null) FactionMemory.Note(f, "contractKept", "CC_PEv4_ContractName".Translate().Resolve());
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv4_ContractKept".Translate((f?.Name ?? "?").Named("FACTION"), r.price.Named("COUNT")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                return;
            }
            int pay = how == "short" ? PeopleEvents4Math.ShortPay(delivered, r.unit, fee) : 0;
            if (pay > 0) Couriers.PaySilver(map, near, pay, f, PeoplesG.Up(marshal, map) ? marshal : null);
            // Nothing came and the fee wasn't carried out: it joins the colony's debts with them (a debt tie in the world's ledger).
            bool debt = how == "default" && !r.feePaid && Debt(r, fee);
            b.contractScale = PeopleEvents4Math.NextScale(b.contractScale, false);
            b.coolUntil = Now + PeopleEvents4Math.CoolDays * GenDate.TicksPerDay;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.DefaultGoodwill, GoodwillWhy.DealBroken);
            if (f != null) FactionMemory.Note(f, "contractBroken", "CC_PEv4_ContractName".Translate().Resolve());
            Tell.Message(NewsKind.World, Importance.Notable, map,
                (how == "short" ? "CC_PEv4_ContractShort" : debt ? "CC_PEv4_ContractDebt" : "CC_PEv4_ContractDefault").Translate((f?.Name ?? "?").Named("FACTION"), pay.Named("COUNT"), fee.Named("FEE")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>The default's fee owed to them, as the enclave's terms owe theirs (EnclaveTerms.Default).</summary>
        private static bool Debt(ContractRecord r, int fee)
        {
            if (r?.faction == null || r.map == null || fee <= 0) return false;
            try
            {
                var terms = new TieTerms { goods = "Silver", count = fee, key = "contractFee" };
                return WorldLedger.AddTie(Tie.Kind.Debt, Refs.Colony(r.map), Refs.Of(r.faction), terms, Tie.Never, "enclaveContract") != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the contract's fee owed", e);
                return false;
            }
        }

        /// <summary>Dev mode: the open contract is due now.</summary>
        public static bool DueNow()
        {
            var r = Open;
            if (r == null) return false;
            r.due = Now;
            r.asked = false;
            return true;
        }
    }

    /// <summary>
    /// The enclave's factor ({X}) at the gate with a contract: {TALE} by {DEAD}, for {COUNT} silver, a fifth over its worth
    /// (a tenth after a default lately).
    /// </summary>
    public class HappeningWorker_EnclaveContract : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Brokers);
            return f != null && !EnclaveTerms.InDefault(f) && EnclaveContract.Open == null && Colonists(map).Count > 0
                   && PeopleEvents4Math.ContractKinds.Any(k => EnclaveContract.Makes(map, k, out _, out _, out _));
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Brokers);
            if (ctx.faction == null) return false;
            var kinds = PeopleEvents4Math.ContractKinds.Where(k => EnclaveContract.Makes(ctx.map, k, out _, out _, out _)).ToList();
            if (kinds.Count == 0) return false;
            string kind = kinds.RandomElement();
            EnclaveContract.Makes(ctx.map, kind, out string what, out var def, out float unit);
            int worth = PeopleEvents4Math.ContractWorth(LivingEvents.WealthFactor(ctx.map), PeopleEvents4Book.Instance?.contractScale ?? 1f);
            int count = PeopleEvents4Math.ContractCount(worth, unit);
            if (count <= 0) return false;
            int days = Rand.RangeInclusive(PeopleEvents4Math.ContractMinDays, PeopleEvents4Math.ContractMaxDays);
            int due = Now + days * GenDate.TicksPerDay;
            ctx.thing = def;
            ctx.count = count;
            ctx.variant = PeopleEvents4Math.ContractPrice(count, unit, EnclaveContract.Cool);
            ctx.note = kind + "|" + what + "|" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + due;
            ctx.tale = EnclaveContract.Words(what, def, count);
            ctx.dead = GenDate.DateFullStringAt(GenDate.TickGameToAbs(due), Find.WorldGrid.LongLatOf(ctx.map.Tile));
            // {BEAST} names the price (the event has no beast of its own).
            ctx.beast = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Factor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var party = PeoplesG.Party(ctx);
            if (!EnclaveBody.ByPod || MapFacts.InVacuum(ctx.map)) return Arrivals.Visit(party, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;
            return ContractLander.Drop(ctx.map, ctx.faction, party, ext.stayHours.RandomInRange);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(EnclaveBody.ByPod ? "pod" : "walk");
            tags.Add((ctx.note ?? "").Split('|')[0]);
            if (EnclaveContract.Cool) tags.Add("cool");
            if ((PeopleEvents4Book.Instance?.contractScale ?? 1f) > 1.01f) tags.Add("bigger");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "signed" : "noted");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The enclave's people by pod at the colony's trade spot, and a stay at the gate (their lander, the enclave's bionic's way).</summary>
    public static class ContractLander
    {
        public static bool Drop(Map map, Faction f, List<Pawn> party, float hours)
        {
            try
            {
                var spot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(spot, map, party.Cast<Thing>(), 110, false, false, false, false);
                var chill = PeoplesD.GateSpot(map) ?? spot;
                LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, chill, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map, party);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the enclave's lander", e);
                return false;
            }
        }
    }

    /// <summary>The contract written in their manifest (half the size with <see cref="half"/>), due on its day.</summary>
    public class DilemmaEffect_EnclaveContract : DilemmaEffect
    {
        public bool half;

        public override bool ChanceEach => false;

        private static int Count(HappeningContext hc, bool half) => half ? Mathf.Max(1, hc.count / 2) : hc.count;

        private static int Price(HappeningContext hc, bool half) => half ? PeopleEvents4Math.Round10(hc.variant / 2f) : hc.variant;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            var parts = (hc.note ?? "").Split('|');
            string what = parts.Length > 1 ? parts[1] : null;
            sheet.Add(ImpactKind.Goods, 0, "CC_PEv4_Impact_Contract".Translate(EnclaveContract.Words(what, hc.thing, Count(hc, half)), hc.dead ?? "?", Price(hc, half)).Resolve(),
                "CC_PEv4_Impact_ContractBrief".Translate().Resolve(), "pev4contract");
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv4_Impact_ContractDefault".Translate(PeopleEvents4Math.DefaultFee(Price(hc, half))).Resolve(), "CC_PEv4_Impact_ContractDefaultBrief".Translate().Resolve(),
                "pev4contractdefault");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var b = PeopleEvents4Book.Instance;
            if (hc?.map == null || b == null || hc.faction == null) return;
            var parts = (hc.note ?? "").Split('|');
            if (parts.Length < 4) return;
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float unit);
            int.TryParse(parts[3], out int due);
            b.AddContract(new ContractRecord
            {
                faction = hc.faction, map = hc.map, kind = parts[0], def = parts[1], count = Count(hc, half), price = Price(hc, half), unit = unit, since = Find.TickManager.TicksGame, due = due
            });
        }
    }

    /// <summary>
    /// The enclave's marshals at the colony's gate on the contract's day ({X}, by their lander, or porters on foot): {TALE}
    /// is what is due, {COUNT} silver what it pays; {DEAD} how many the stores hold now.
    /// </summary>
    public class HappeningWorker_ContractDue : HappeningWorker_Visitors
    {
        private static ContractRecord Due(Map map) => EnclaveContract.Open is ContractRecord r && r.map == map && r.due >= 0 && Now >= r.due ? r : null;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Due(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = Due(ctx.map);
            if (r == null) return false;
            ctx.faction = r.faction;
            ctx.thing = r.def == "meals" || r.def == PlayCarry.Art ? null : DefDatabase<ThingDef>.GetNamedSilentFail(r.def);
            ctx.count = r.price;
            ctx.variant = r.count;
            ctx.note = r.id.ToString();
            ctx.tale = EnclaveContract.Words(r.def, ctx.thing, r.count);
            int have = PlayCarry.Have(ctx.map, ctx.thing == null ? r.def : null, ctx.thing);
            ctx.dead = Mathf.Min(have, r.count).ToString();
            // {BEAST} names the fee of a default.
            ctx.beast = PeopleEvents4Math.DefaultFee(r.price).ToString();
            bool pod = EnclaveBody.ByPod;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, pod ? "Rimfolk_Enclave_Marshal" : "Rimfolk_Enclave_Hand"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < (pod ? 1 : 2); i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, pod ? "Rimfolk_Enclave_Marshal" : "Rimfolk_Enclave_Hand"), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            ctx.a = PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var party = PeoplesG.Party(ctx);
            if (!EnclaveBody.ByPod || MapFacts.InVacuum(ctx.map)) return Arrivals.Visit(party, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;
            return ContractLander.Drop(ctx.map, ctx.faction, party, ext.stayHours.RandomInRange);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(EnclaveBody.ByPod ? "pod" : "walk");
            int.TryParse(ctx.dead ?? "0", out int have);
            tags.Add(have >= ctx.variant ? "ready" : have > 0 ? "short" : "none");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var r = EnclaveContract.ById(int.TryParse(ctx.note ?? "", out int id) ? id : -1);
            if (r == null || option > 1) return null;
            int have = PlayCarry.Have(ctx.map, ctx.thing == null ? r.def : null, ctx.thing);
            if (option == 1 && have <= 0) return "CC_PEv4_ContractNone".Translate().Resolve();
            if (option == 1 && have >= r.count) return "CC_PEv4_ContractAllThere".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "aboard" : option == 1 ? "short" : "default");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The contract's goods carried out to the marshals (the errand of the answer, its amount the contract's, all or what the stores hold).</summary>
    public class DilemmaEffect_ContractCarry : DilemmaEffect_CarryOut
    {
        public bool partial;

        private bool Set(DilemmaContext ctx)
        {
            var r = EnclaveContract.ById(int.TryParse((ctx as HappeningContext)?.note ?? "", out int id) ? id : -1);
            if (r == null || ctx.map == null) return false;
            what = ctx.thing == null ? r.def : null;
            int have = PlayCarry.Have(ctx.map, what, ctx.thing);
            count = partial ? Mathf.Clamp(have, 1, r.count) : r.count;
            steward = false;
            return true;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (Set(ctx)) base.Describe(ctx, sheet);
        }

        public override string Unavailable(DilemmaContext ctx) => Set(ctx) ? base.Unavailable(ctx) : null;

        public override void Apply(DilemmaContext ctx)
        {
            if (Set(ctx)) base.Apply(ctx);
        }
    }

    /// <summary>A default's fee carried out to the marshals when the stores hold it (else it joins the colony's debts: EnclaveContract.Settle).</summary>
    public class DilemmaEffect_ContractFee : DilemmaEffect_CarryOut
    {
        private ContractRecord Set(DilemmaContext ctx)
        {
            var r = EnclaveContract.ById(int.TryParse((ctx as HappeningContext)?.note ?? "", out int id) ? id : -1);
            if (r == null) return null;
            what = "silver";
            count = PeopleEvents4Math.DefaultFee(r.price);
            steward = false;
            ifHave = true;
            return r;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (Set(ctx) != null) base.Describe(ctx, sheet);
        }

        // The default answer is always open: the fee is owed if the stores can't pay it.
        public override string Unavailable(DilemmaContext ctx) => null;

        public override void Apply(DilemmaContext ctx)
        {
            var r = Set(ctx);
            if (r == null || ctx.map == null) return;
            if (PlayCarry.Have(ctx.map, "silver", null) < count) return;
            r.feePaid = true;
            base.Apply(ctx);
        }
    }

    /// <summary>The contract settled by the answer: kept (<see cref="how"/> "kept"), short, or a default.</summary>
    public class DilemmaEffect_ContractSettle : DilemmaEffect
    {
        public string how = "default";

        public override bool ChanceEach => false;

        private static ContractRecord Of(DilemmaContext ctx) => EnclaveContract.ById(int.TryParse((ctx as HappeningContext)?.note ?? "", out int id) ? id : -1);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = Of(ctx);
            if (r == null) return;
            int fee = PeopleEvents4Math.DefaultFee(r.price);
            if (how == "kept")
            {
                sheet.Silver(r.price);
                if (ctx.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents4Math.KeptGoodwill);
                sheet.Add(ImpactKind.Goods, 1, "CC_PEv4_Impact_Bigger".Translate().Resolve(), "CC_PEv4_Impact_BiggerBrief".Translate().Resolve(), "pev4bigger");
                return;
            }
            int have = PlayCarry.Have(ctx.map, ctx.thing == null ? r.def : null, ctx.thing);
            int pay = how == "short" ? PeopleEvents4Math.ShortPay(Mathf.Min(have, r.count), r.unit, fee) : 0;
            if (pay > 0) sheet.Silver(pay);
            if (ctx.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents4Math.DefaultGoodwill);
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv4_Impact_Default".Translate(fee, PeopleEvents4Math.CoolDays).Resolve(), "CC_PEv4_Impact_DefaultBrief".Translate().Resolve(), "pev4default");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = Of(ctx);
            if (r == null) return;
            int have = PlayCarry.Have(ctx.map, ctx.thing == null ? r.def : null, ctx.thing);
            EnclaveContract.Settle(r, how, Mathf.Min(have, r.count), ctx.stranger);
        }
    }
}
