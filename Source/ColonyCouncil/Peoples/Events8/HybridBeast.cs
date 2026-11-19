using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 81. A hybrid beast (the Helix Works) =====================

    /// <summary>
    /// A hybrid beast (item 81): with Vanilla Genetics Expanded, a vat-keeper of the Helix Works comes to the gate leading a young hybrid
    /// from the Works' vats (one of the mod's beasts of the farm and the road, never its odd ones, one that can stand the weather here),
    /// as round 5's egg-trader came with an egg, and sells it for its worth and a fifth. Bought, it is the colony's, its handler its
    /// master; with the second answer the vat-keeper also shows the handler how it is kept (an Animals lesson at the gate). Without the
    /// mod the event doesn't come.
    /// </summary>
    public static class HybridBeast
    {
        public const string EventDef = "Rimfolk_Evt_HybridBeast";

        /// <summary>The hybrids the Works could bring here: of the mod, animals, able to stand this home's weather.</summary>
        public static List<PawnKindDef> Kinds(Map map) =>
            PeopleEvents8Math.Hybrids.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k?.race != null && k.RaceProps.Animal
                                                                                                             && (map == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race))).ToList();

        /// <summary>The colony's handler: its best with animals who can tame and train them.</summary>
        public static Pawn Handler(Map map) => PeoplesD.Best(map, SkillDefOf.Animals, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && !p.DevelopmentalStage.Juvenile());

        /// <summary>A young one of the kind, the Works', past its first life stage.</summary>
        public static Pawn Young(PawnKindDef kind, Faction f, int tile)
        {
            if (kind == null) return null;
            var stages = kind.RaceProps.lifeStageAges;
            float age = stages != null && stages.Count > 1 ? Mathf.Max(0.1f, stages[1].minAge + 0.05f) : 0.5f;
            var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true, fixedBiologicalAge: age, fixedChronologicalAge: age);
            return PawnGenerator.GeneratePawn(req);
        }

        /// <summary>Bought: the beast the colony's, its handler its master where it can be trained, out of the vat-keeper's party; told.</summary>
        public static void Bought(HappeningContext ctx)
        {
            var map = ctx?.map;
            var beast = ctx?.group.FirstOrDefault(p => p != null && p.RaceProps.Animal && !p.Dead);
            if (map == null || beast == null) return;
            try
            {
                Lords.Leave(beast);
                if (beast.Faction != Faction.OfPlayer) beast.SetFaction(Faction.OfPlayer, ctx.a);
                var handler = ctx.a;
                if (handler != null && beast.playerSettings != null && beast.training != null && beast.RaceProps.trainability != null && !handler.Dead)
                    beast.playerSettings.Master = handler;
                if (beast.Spawned) FleckMaker.ThrowMetaIcon(beast.Position, map, FleckDefOf.Heart);
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv8_HybridBought".Translate(beast.LabelShort.Named("BEAST"), (handler?.LabelShort ?? "?").Named("PAWN"),
                    (ctx.faction?.Name ?? "?").Named("FACTION")).Resolve(), beast.Spawned ? new LookTargets(beast) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                PeopleEvents8.Goodwill(ctx.faction, PeopleEvents8Math.HybridGoodwill, GoodwillWhy.Paid);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the hybrid bought", e);
            }
        }
    }

    /// <summary>A vat-keeper of {FACTION} ({X}) at the gate leading a young {TALE} from the Works' vats, for {COUNT} silver; {A} the colony's handler.</summary>
    public class HappeningWorker_HybridBeast : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Editors) != null && HybridBeast.Kinds(map).Count > 0 && HybridBeast.Handler(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Editors);
            var kind = HybridBeast.Kinds(ctx.map).RandomElementWithFallback();
            if (ctx.faction == null || kind == null) return false;
            var beast = HybridBeast.Young(kind, ctx.faction, ctx.map.Tile);
            if (beast == null) return false;
            ctx.group.Add(beast);
            ctx.thing = kind.race;
            ctx.tale = kind.label;
            ctx.count = PeopleEvents8Math.HybridPrice(kind.race.BaseMarketValue);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HelixWorks_VatKeeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Animals, new IntRange(9, 14));
            ctx.a = HybridBeast.Handler(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var beast = ctx.group.FirstOrDefault(p => p != null && p.RaceProps.Animal);
            return beast == null || beast.Dead || !beast.Spawned || beast.Map != ctx.map ? "CC_PEv8_HybridGone".Translate(ctx.tale ?? "?").Resolve() : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sold" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The young hybrid handed over to the colony's handler (HybridBeast.Bought), paid for by the answer's own carry-out.</summary>
    public class DilemmaEffect_HybridBeast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.thing == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv8_Impact_Hybrid".Translate(hc.tale ?? "?", hc.a?.LabelShort ?? "?").Resolve(), "CC_PEv8_Impact_HybridBrief".Translate().Resolve(), "pev8hybrid");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.HybridGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => HybridBeast.Bought(ctx as HappeningContext);
    }
}
