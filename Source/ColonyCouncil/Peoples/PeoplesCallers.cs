using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release C's peoples at the colony's gate (docs/design/peoples.md, "Release C"; Defs/IncidentDefs/Rimfolk_Events_PeoplesC.xml):
    // a band of the pilgrim fellowship asks for a night's shelter, a scholar of the learned society asks to copy the
    // colony's records, the spacer enclave's factor asks leave to come down by drop pod with goods, and a boat-trader of
    // the river folk brings a catch to the gate. Each comes only from a people of its calling, at peace with the colony.

    // ===================== The pilgrims' night =====================

    /// <summary>
    /// At dusk a band of three to five pilgrims of the fellowship asks for a night's shelter on the road between their
    /// shrines. Taken in, each sleeps the night in a warm corner indoors (VisitorServices.Lodge) and the band goes on at
    /// first light, its eldest blessing the gate; the colony is glad of it. One night in ten (the harm setting's scale)
    /// one of them has brought a cough in off the road, and the colonist who sat up with them catches it, said over their
    /// head and told in the morning's word. Bedded down outside the gate by their own fire, they go on in the morning with
    /// their thanks. Or the gate stays shut. The morning is a memo of the living events (LivingEvents.Shelter).
    /// </summary>
    public class HappeningWorker_PilgrimShelter : HappeningWorker_Visitors
    {
        private static bool Theirs(Faction f) => Peoples.CallingOf(f) == PeoplesMath.Pilgrims && Arrivals.Usable(f);

        private static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesMath.ShelterHour(Hour(map)) && Arrivals.Friendly(Theirs) != null && Colonists(map).Count > 0 && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(Theirs);
            if (ctx.faction == null) return false;
            int n = Mathf.Clamp(ext.count.RandomInRange, PeoplesMath.BandMin, PeoplesMath.BandMax);
            MakeGroup(ctx, ext, n, ext.ages, null, false);
            if (ctx.stranger == null) return false;
            ctx.count = n;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed)
                .RandomElementByWeightWithFallback(p => (p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 2f : 1f) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) * 0.1f);
            // {PLACE}: the shrine they are walking to, their own hostel nearest the colony.
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>They wait just outside the gate until it's answered, and stay the night if taken in or let bed down.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(Band(ctx), ctx.map, ctx.faction, null, PeoplesMath.HoursToLight(Hour(ctx.map), VisitorServices.Dawn) + 3f) != null;

        private static List<Pawn> Band(HappeningContext ctx) => ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map != null && ctx.map.mapTemperature.OutdoorTemp < 0f) tags.Add("cold");
            if (ctx.map != null && ctx.map.weatherManager.RainRate > 0.1f) tags.Add("rain");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            if (option <= 1 && x != null && x.Spawned && ctx.map != null)
            {
                // Indoors: a warm corner each for the night, until first light. Outside: by their own fire at the gate.
                if (option == 0)
                    foreach (var p in Band(ctx).Where(p => p.Spawned && p.Map == ctx.map))
                        VisitorServices.Lodge(ctx.map, p, false);
                LivingEvents.Schedule(LivingEvents.Shelter, x, ctx.faction, null, option == 0 ? "in" : "out", ctx.a, PeoplesMath.HoursToLight(Hour(ctx.map), VisitorServices.Dawn) / 24f, ctx.map);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>
        /// First light: the band goes on, its eldest blessing the gate (a line over their head); those who slept indoors
        /// leave the colony glad of it, and maybe one of the colony with their cough. Nothing to tell when something
        /// befell them in the night.
        /// </summary>
        public static void Morning(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.Shelter);
            var ext = def?.GetModExtension<HappeningExtension>();
            var more = LivingEvents.More(def);
            var map = HappeningTracker.MapFor(m);
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            var x = m.pawn;
            if (x == null || x.Dead || !x.Spawned || x.Map != map || x.Faction == null || x.Faction.IsPlayer || x.HostileTo(Faction.OfPlayer)) return;
            bool indoors = m.text == "in";
            var band = x.GetLord()?.ownedPawns?.Where(p => p != null && p.Spawned && !p.Dead && p.Map == map).ToList() ?? new List<Pawn> { x };
            var tags = new List<string> { indoors ? "in" : "out" };
            Pawn coughing = null;
            var host = m.other;
            if (indoors && AilmentTracker.On && host != null && !host.Dead && host.Spawned && host.Map == map && host.IsColonist)
            {
                var cold = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_HeadCold");
                if (cold != null && Harm.Roll(host, HarmKinds.PilgrimCough, PeoplesMath.CoughChance, HurtClass.Mild)
                    && Harm.Ail(host, cold, Mathf.Max(0.1f, cold.initialSeverity), HarmKinds.PilgrimCough, HurtClass.Mild) != null)
                    coughing = host;
            }
            if (coughing != null) tags.Add("cough");
            if (indoors)
            {
                var glad = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Evt_ShelteredPilgrims");
                if (glad != null)
                    foreach (var p in Colonists(map).Where(p => p.story?.traits?.HasTrait(TraitDefOf.Psychopath) != true))
                        CouncilGameComponent.GiveThought(p, glad);
            }
            // The eldest's blessing over the gate as they go; the one who caught the cough says so.
            string bless = VarietyUtil.Line(ext?.lines, ctx, new[] { "bless" });
            if (!bless.NullOrEmpty()) Speech.Say(x, bless);
            if (coughing != null)
            {
                string line = VarietyUtil.Line(ext?.lines, ctx, new[] { "cough" });
                if (!line.NullOrEmpty()) Speech.Say(coughing, line);
            }
            Arrivals.SendAway(band);
            LivingEvents.Say(ctx, more?.done, tags, null, coughing != null ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.PositiveEvent, new LookTargets(coughing ?? x));
        }
    }

    // ===================== The scholars' copyist =====================

    /// <summary>
    /// A scholar of the learned society asks to copy the colony's records for its archive (a data event: its setting
    /// brings the scholar, who works the day where the colony gathers: its party spot, its fire or its table). {THING} is the book they leave in return, one of the
    /// society's own of the colony's age: a textbook for a tribal or medieval colony, a schematic for a later one.
    /// </summary>
    public class HappeningWorker_ScholarCopyist : HappeningWorker_Setting
    {
        /// <summary>The society's book for the colony's age, or null when the game has none.</summary>
        public static ThingDef Book() =>
            DefDatabase<ThingDef>.GetNamedSilentFail(Avail.IsPreIndustrial ? "TextBook" : "Schematic") ?? DefDatabase<ThingDef>.GetNamedSilentFail("TextBook");

        /// <summary>The society's book to leave, and a place where the colony gathers for the copyist to work (a party spot, a fire, a table).</summary>
        protected override bool CanHappen(Map map, HappeningExtension ext) => Book() != null && GatheringSpot(map) != null && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.thing = Book();
            ctx.count = 1;
            return ctx.thing != null;
        }
    }

    // ===================== The river folk's catch =====================

    /// <summary>
    /// A boat-trader of the river folk brings a catch to the gate to sell: fish of the colony's own waters where the land
    /// has them, else any of the game's (Odyssey's fish; without it this never comes). {THING} the fish, {COUNT} how many
    /// (by the colony's wealth).
    /// </summary>
    public class HappeningWorker_RiverCatch : HappeningWorker_Setting
    {
        /// <summary>A fish the river folk would bring here: the land's own fresh-water kind first.</summary>
        public static ThingDef Fish(Map map)
        {
            var own = map?.Biome?.fishTypes?.freshwater_Common?.Where(f => f?.fishDef != null).Select(f => f.fishDef).ToList();
            if (!own.NullOrEmpty()) return own.RandomElement();
            if (ThingCategoryDefOf.Fish == null) return null;
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.thingCategories != null && d.thingCategories.Contains(ThingCategoryDefOf.Fish) && d.ingestible != null)
                .RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Fish(map) != null && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.thing = Fish(ctx.map);
            return ctx.thing != null && ctx.count > 0;
        }
    }

    // ===================== The enclave's lander =====================

    /// <summary>
    /// An industrial or spacer colony (one that knows what comes down out of the sky): the spacer enclave's factor asks by
    /// the colony's radio for leave to come down by the gate with goods. Bidden, they come down by drop pod
    /// (DilemmaEffect_EnclaveLanding), trade at the gate as a visiting trader does, and walk off the map when they are done.
    /// </summary>
    public class HappeningWorker_EnclaveLander : IncidentWorker_Happening
    {
        private static bool Theirs(Faction f) => Peoples.CallingOf(f) == PeoplesMath.Brokers && Arrivals.Usable(f) && EnclaveLanding.CanTrade(f);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesMath.LanderAge((int)Ages.Ours) && map.IsPlayerHome && !MapFacts.InVacuum(map) && map.dangerWatcher.DangerRating == StoryDanger.None
            && Arrivals.Friendly(f => Theirs(f) && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f)) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly(f => Theirs(f) && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f));
            if (ctx.faction == null) return false;
            // The one who answers the call: the colony's best talker.
            ctx.a = Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            ctx.count = PeoplesMath.LanderGuards(LivingEvents.WealthFactor(map));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map != null && ctx.map.listerBuildings.ColonistsHaveBuildingWithPowerOn(ThingDefOf.OrbitalTradeBeacon)) tags.Add("beacon");
        }
    }

    /// <summary>
    /// The enclave's trade party comes down by drop pod by the colony's gate: its factor with the goods (one of the
    /// enclave's caravan stocks), its marshals ({COUNT}) and any beasts for sale. They stay the day at a spot just outside
    /// the colony and trade from the factor's own pack, as the game's visiting traders do (LordJob_VisitColony: a
    /// caravan's lord sells only from its pack beasts), and walk off the map when they are done.
    /// </summary>
    public class DilemmaEffect_EnclaveLanding : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            return f == null || f.defeated || f.HostileTo(Faction.OfPlayer) ? "CC_PeoplesC_LanderGone".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction == null || ctx.map == null) return;
            sheet.Play("CC_PeoplesC_Impact_Lander".Translate(ctx.faction.Name, Mathf.Max(1, ctx.count)).Resolve(), "CC_PeoplesC_Impact_LanderBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null || ctx.faction == null) return;
            try
            {
                EnclaveLanding.Land(ctx.map, ctx.faction, Mathf.Max(1, ctx.count));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the enclave's lander", e);
            }
        }
    }

    /// <summary>The enclave's trade party by drop pod (DilemmaEffect_EnclaveLanding).</summary>
    public static class EnclaveLanding
    {
        private static PawnGroupMaker Group(Faction f) => f?.def?.pawnGroupMakers?.FirstOrDefault(g => g.kindDef == PawnGroupKindDefOf.Trader);

        /// <summary>A people that can send a trade party this way: caravan stocks, and a trader of its own in its caravans.</summary>
        public static bool CanTrade(Faction f) =>
            f?.def != null && !f.def.caravanTraderKinds.NullOrEmpty() && Group(f)?.traders?.Any(o => o?.kind != null && o.kind.trader) == true;

        /// <summary>How long the party stays at the gate to trade.</summary>
        public static readonly FloatRange StayHours = new FloatRange(10f, 14f);

        /// <summary>
        /// The party made and dropped by the colony's trade drop spot: the factor holding the goods, <paramref name="guards"/>
        /// of the caravans' guards, and the beasts for sale. False when it couldn't be made.
        /// </summary>
        public static bool Land(Map map, Faction f, int guards)
        {
            if (map == null || f == null || f.HostileTo(Faction.OfPlayer) || !CanTrade(f)) return false;
            var group = Group(f);
            var kind = f.def.caravanTraderKinds.RandomElementByWeightWithFallback(t => t.CalculatedCommonality);
            var traderKind = group.traders.Where(o => o?.kind != null && o.kind.trader).RandomElementByWeightWithFallback(o => o.selectionWeight)?.kind;
            if (kind == null || traderKind == null) return false;
            var pawns = new List<Pawn>();
            var trader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(traderKind, f, PawnGenerationContext.NonPlayer, map.Tile));
            trader.mindState.wantsToTradeWithColony = true;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(trader, true);
            if (trader.trader == null) return false;
            trader.trader.traderKind = kind;
            pawns.Add(trader);
            // The goods in the factor's own pack (the game's caravan stock for the trader kind), the beasts for sale beside.
            var stock = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = kind, tile = map.Tile, makingFaction = f });
            foreach (var t in stock)
            {
                if (t is Pawn beast)
                {
                    if (beast.Faction != f) beast.SetFaction(f);
                    pawns.Add(beast);
                }
                else if (!trader.inventory.innerContainer.TryAdd(t) && !t.Destroyed) t.Destroy();
            }
            for (int i = 0; i < guards && !group.guards.NullOrEmpty(); i++)
            {
                var g = group.guards.RandomElementByWeightWithFallback(o => o.selectionWeight)?.kind;
                if (g == null) break;
                pawns.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(g, f, PawnGenerationContext.NonPlayer, map.Tile, mustBeCapableOfViolence: true)));
            }
            foreach (var p in pawns)
                if (p.needs?.food != null)
                    p.needs.food.CurLevel = p.needs.food.MaxLevel;
            var spot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(spot, map, pawns.Cast<Thing>(), 110, false, false, false, false);
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(spot, map, out var chill)) chill = spot;
            LordMaker.MakeNewLord(f, new LordJob_VisitColony(f, chill, Mathf.RoundToInt(StayHours.RandomInRange * GenDate.TicksPerHour)), map, pawns);
            if (Prefs.DevMode) Log.Message("[Rimfolk] " + f.Name + "'s lander: " + pawns.Count + " by drop pod at " + spot + ", " + kind.defName + ".");
            return true;
        }
    }
}
