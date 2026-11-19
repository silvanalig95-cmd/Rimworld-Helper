using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 97. A toll on the road (the road league and the Wheel) =====================

    /// <summary>
    /// A toll on the road (item 97): the road league tolls the road past the colony, and a cart train of the Wheel (its moving town's
    /// wheelwright, a hand or two and their pack beasts loaded with salvage) won't pay. The league's factor comes to the gate; the carts
    /// stop at the edge of the colony's land. The colony can let the carts cross its land to the far edge (the Wheel thinks better of it,
    /// the league worse), turn them back the way they came (the reverse), or host both at the gate, where its best talker sits with them
    /// two hours over a meal and sets a fair toll, which the wheelwright pays the factor before the carts go on by the road (both a
    /// little better). Or it stays out of it, and the carts go the long way round. The region hears of it. The fair toll is kept as one of
    /// the events' visits (kind "pev:toll"): x the factor, group the wheelwright, touched the rest of the cart train, a the talker, cell the
    /// gate, text "league|wheel|toll".
    /// </summary>
    public static class RoadToll
    {
        public const string Kind = "pev:toll";

        private static int Now => Find.TickManager.TicksGame;

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        public static Faction League(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(0));

        public static Faction Wheel(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(1));

        private static int TollOf(Visit v) => int.TryParse(Parts(v).ElementAtOrDefault(2), out int n) ? n : PeopleEvents10Math.Toll(2);

        /// <summary>The Wheel's cart train of the event: its people and beasts (the wheelwright first).</summary>
        public static List<Pawn> Carts(HappeningContext ctx) =>
            ctx?.group == null ? new List<Pawn>() : ctx.group.Where(p => p != null && ctx.enemy != null && p.Faction == ctx.enemy).ToList();

        public static Pawn Wheelwright(HappeningContext ctx) => Carts(ctx).FirstOrDefault(p => p.RaceProps.Humanlike);

        public static bool Here(List<Pawn> carts, Map map) => carts.Any(p => p.RaceProps.Humanlike && PeoplesG.Up(p, map));

        /// <summary>
        /// A pack beast for the cart train: half the time one of the Wheel's own caravans' beasts that suits the land and the season, else any
        /// pack beast of the game's or the player's mods (a grown one big enough to carry, never a predator, an insect, a dryad or a
        /// Pokémon) that the land allows as a pack beast or that lives wild here, and that can stand the season.
        /// </summary>
        public static PawnKindDef BeastKind(Faction f, Map map)
        {
            if (map == null) return null;
            var own = Couriers.AnimalKind(f, map);
            if (own != null && Rand.Bool) return own;
            var biome = map.Biome;
            var pool = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k?.race?.race != null && k.RaceProps.Animal && k.RaceProps.packAnimal && !k.RaceProps.predator
                && !k.RaceProps.Insect && !k.RaceProps.Dryad && k.RaceProps.baseBodySize >= 1f && !PokeWorldLink.IsPokemon(k)
                && (biome == null || biome.IsPackAnimalAllowed(k.race) || BiomeAnimals.LivesIn(biome, k)) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f))
                .ToList();
            return pool.RandomElementWithFallback() ?? own;
        }

        /// <summary>A pack beast of theirs, its packs loaded with salvage so they show.</summary>
        public static Pawn Beast(PawnKindDef kind, Faction f, Map map)
        {
            if (kind == null || f == null || map == null) return null;
            try
            {
                var b = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, map.Tile));
                var load = ThingMaker.MakeThing(ThingDefOf.Steel);
                load.stackCount = PeopleEvents10Math.CartLoad;
                if (b.inventory != null && !b.inventory.innerContainer.TryAdd(load)) load.Destroy();
                return b;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 10: the Wheel's pack beast", e);
                return null;
            }
        }

        /// <summary>The farthest edge the carts can walk to from where they came in (a great herd's crossing).</summary>
        public static IntVec3 FarEdge(Map map, IntVec3 from)
        {
            var end = IntVec3.Invalid;
            if (map == null || !from.IsValid) return end;
            for (int i = 0; i < 8; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReach(from, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)), map,
                        CellFinder.EdgeRoadChance_Friendly, out var c2)) break;
                if (!end.IsValid || c2.DistanceToSquared(from) > end.DistanceToSquared(from)) end = c2;
            }
            return end;
        }

        /// <summary>They walk off the map near this edge cell, together.</summary>
        private static void Off(List<Pawn> pawns, Faction f, IntVec3 edge)
        {
            var list = pawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Faction == f).Distinct().ToList();
            if (list.Count == 0 || f == null) return;
            try
            {
                Lords.Leave(list);
                if (edge.IsValid) LordMaker.MakeNewLord(f, new LordJob_ExitMapNear(edge, LocomotionUrgency.Walk, 12f, false, false), list[0].Map, list);
                else LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, true), list[0].Map, list);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 10: the carts on their way", e);
            }
        }

        /// <summary>Let through: the carts walk across the colony's land and off at the far edge; the factor goes home.</summary>
        public static void Cross(HappeningContext ctx)
        {
            var map = ctx?.map;
            var carts = Carts(ctx);
            if (map == null || !Here(carts, map)) return;
            var from = carts.FirstOrDefault(p => p.Spawned)?.Position ?? ctx.spot;
            Off(carts, ctx.enemy, FarEdge(map, from));
            PeoplesG.SayLine(Wheelwright(ctx), ctx.incident, "through");
            PeoplesG.Home(PeoplesG.Party(ctx).Where(p => p.Faction == ctx.faction));
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv10_TollCross".Translate((ctx.enemy?.Name ?? "?").Named("WHEEL"), (ctx.faction?.Name ?? "?").Named("LEAGUE")).Resolve(),
                new LookTargets(carts.Where(p => p.Spawned)), MessageTypeDefOf.NeutralEvent);
            PeopleEvents10.Heard("CC_PEv10_HeardCross".Translate((ctx.enemy?.Name ?? "?").Named("WHEEL"), (ctx.faction?.Name ?? "?").Named("LEAGUE")).Resolve(), LookTargets.Invalid);
        }

        /// <summary>Turned back: the carts go back out the way they came; the factor thanks the colony and goes home.</summary>
        public static void Back(HappeningContext ctx)
        {
            var map = ctx?.map;
            var carts = Carts(ctx);
            if (map == null) return;
            PeoplesG.SayLine(Wheelwright(ctx), ctx.incident, "turned");
            PeoplesG.SayLine(ctx.stranger, ctx.incident, "thanks");
            if (Here(carts, map)) Off(carts, ctx.enemy, ctx.spot.IsValid ? ctx.spot : IntVec3.Invalid);
            PeoplesG.Home(PeoplesG.Party(ctx).Where(p => p.Faction == ctx.faction));
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv10_TollBack".Translate((ctx.enemy?.Name ?? "?").Named("WHEEL"), (ctx.faction?.Name ?? "?").Named("LEAGUE")).Resolve(),
                new LookTargets(carts.Where(p => p.Spawned)), MessageTypeDefOf.NeutralEvent);
            PeopleEvents10.Heard("CC_PEv10_HeardBack".Translate((ctx.enemy?.Name ?? "?").Named("WHEEL"), (ctx.faction?.Name ?? "?").Named("LEAGUE")).Resolve(), LookTargets.Invalid);
        }

        /// <summary>A fair toll: the talker sits with the factor and the wheelwright at the gate; set when the talker's two hours are done.</summary>
        public static void Fair(HappeningContext ctx)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var wright = Wheelwright(ctx);
            if (map == null || t == null || ctx.a == null || ctx.faction == null || ctx.enemy == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, ctx.a, wright != null ? new List<Pawn> { wright } : null,
                Now + PeopleEvents10Math.HearWaitHours * GenDate.TicksPerHour + PeopleEvents10Math.FairTicks + 2 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + ctx.enemy.GetUniqueLoadID() + "|" + ctx.count;
            v.touched.AddRange(Carts(ctx).Where(p => p != wright));
            v.cell = PeoplesD.GateSpot(map) ?? ctx.a.Position;
            v.stake = 0;
            if (!PlayOuts.On)
            {
                Set(v, true);
                return;
            }
            TryHear(v);
        }

        private static void TryHear(Visit v)
        {
            var map = v.map;
            var talker = v.a;
            if (map == null || !PeoplesG.Up(talker, map) || talker.Drafted || MapFacts.ThreatOn(map) || !v.cell.IsValid) return;
            if (!PeopleEvents10.Send(talker, PeopleEvents10.HearJob, v.cell, PeopleEvents10Math.FairTicks)) return;
            v.stake = 1;
            float hours = PeopleEvents10Math.FairTicks / (float)GenDate.TicksPerHour + 3f;
            foreach (var x in PeopleEvents10.Up(v)) PeopleEvents10.Errand(x, map, v.cell, PeopleEvents10.PleadJob, PeopleEvents10Math.FairTicks + GenDate.TicksPerHour, hours);
            PeoplesG.SayLine(v.x, v.incident, "tollcase");
            PeoplesG.SayLine(v.group.FirstOrDefault(), v.incident, "cartcase");
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            // Waited too long for the talker, or neither of them still at the gate: they settled it between themselves.
            if (now > v.until || PeopleEvents10.Up(v).Count == 0)
            {
                Set(v, false);
                return false;
            }
            if (v.stake == 0) TryHear(v);
            else if (v.a == null || !v.a.Spawned || v.a.CurJobDef != PeopleEvents10.HearJob) v.stake = 0;
            return !v.done;
        }

        /// <summary>The talker's two hours at the gate done: the toll set. False when it wasn't this hearing.</summary>
        public static bool Heard(Pawn talker)
        {
            var v = PeopleEvents10.OfColonist(Kind, talker);
            if (v == null) return false;
            Set(v, true);
            return true;
        }

        /// <summary>
        /// The fair toll set and paid at the gate, both a little better; the carts go on by the road the way they came, the factor home.
        /// <paramref name="sat"/> false: the talker never sat with them, and they settled it between themselves at the gate.
        /// </summary>
        private static void Set(Visit v, bool sat)
        {
            v.done = true;
            var map = v.map;
            var league = League(v);
            var wheel = Wheel(v);
            if (league == null || wheel == null) return;
            if (sat && v.a != null && !v.a.Dead) v.a.skills?.Learn(SkillDefOf.Social, PeopleEvents10Math.FairSocialXp, true);
            PeopleEvents10.Goodwill(league, PeopleEvents10Math.FairGain, GoodwillWhy.Judged);
            PeopleEvents10.Goodwill(wheel, PeopleEvents10Math.FairGain, GoodwillWhy.Judged);
            if (sat)
            {
                FactionMemory.Note(league, "mediated", wheel.Name);
                FactionMemory.Note(wheel, "mediated", league.Name);
            }
            var wright = v.group.FirstOrDefault();
            PeoplesG.SayLine(wright, v.incident, "paid");
            PeoplesG.SayLine(v.x, v.incident, "fair");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                (sat ? "CC_PEv10_TollFair" : "CC_PEv10_TollFairAlone").Translate((v.a?.LabelShort ?? "?").Named("TALKER"), wheel.Name.Named("WHEEL"), league.Name.Named("LEAGUE"),
                    TollOf(v).Named("COUNT")).Resolve(), wright != null && wright.Spawned ? new LookTargets(wright) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            PeopleEvents10.Heard("CC_PEv10_HeardFair".Translate(wheel.Name.Named("WHEEL"), league.Name.Named("LEAGUE")).Resolve(), LookTargets.Invalid);
            var carts = v.touched.Concat(v.group).Where(p => p != null && p.Spawned && p.Faction == wheel).ToList();
            Off(carts, wheel, IntVec3.Invalid);
            PeoplesG.Home(new List<Pawn> { v.x });
        }
    }

    /// <summary>
    /// The league's factor of {FACTION} ({X}) at the gate, and a cart train of the Wheel ({ENEMY}) stopped at the edge of the colony's land:
    /// the league tolls the road past the colony and the Wheel's carts won't pay. {COUNT} is the fair toll the colony would set.
    /// </summary>
    public class HappeningWorker_RoadToll : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeopleEvents10.Pair(PeoplesMath.Traders, PeoplesMath.Salvagers, out _) != null && RoadToll.BeastKind(PeoplesD.Friendly(PeoplesMath.Salvagers), map) != null
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeopleEvents10.Pair(PeoplesMath.Traders, PeoplesMath.Salvagers, out var wheel);
            ctx.enemy = wheel;
            if (ctx.faction == null || ctx.enemy == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Factor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Guard"), ctx.faction, ext.ages));
            // The Wheel's cart train: the wheelwright first, a hand or two, and their pack beasts.
            var wright = Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_Wheel_Wheelwright"), ctx.enemy, ext.ages);
            if (wright == null) return false;
            ctx.group.Insert(0, wright);
            int hands = Rand.RangeInclusive(PeopleEvents10Math.WheelHandsMin, PeopleEvents10Math.WheelHandsMax);
            for (int i = 0; i < hands; i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_Wheel_Hand"), ctx.enemy, ext.ages));
            var kind = RoadToll.BeastKind(ctx.enemy, ctx.map);
            int beasts = Rand.RangeInclusive(PeopleEvents10Math.WheelBeastsMin, PeopleEvents10Math.WheelBeastsMax);
            for (int i = 0; i < beasts; i++) ctx.group.Add(RoadToll.Beast(kind, ctx.enemy, ctx.map));
            ctx.group.RemoveAll(p => p == null);
            ctx.count = PeopleEvents10Math.Toll(ctx.group.Count(p => p.RaceProps.Animal));
            ctx.pet = ctx.group.FirstOrDefault(p => p.RaceProps.Animal);
            ctx.beast = ctx.pet?.kindDef?.GetLabelPlural();
            ctx.a = WoodMoot.Talker(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.enemy, ctx.map) ?? ctx.enemy.Name;
            return ctx.pet != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            float hours = ext.stayHours.RandomInRange;
            var league = PeoplesG.Party(ctx).Where(p => p.Faction == ctx.faction).ToList();
            if (Arrivals.Visit(league, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), hours) == null) return false;
            // The carts stop just inside the edge they came in by.
            var carts = RoadToll.Carts(ctx);
            if (!Arrivals.TryEntryCell(ctx.map, out var entry)) return false;
            foreach (var p in carts)
                if (!Arrivals.Spawn(p, ctx.map, entry)) return false;
            ctx.spot = entry;
            var inward = (entry.ToVector3() + (ctx.map.Center - entry).ToVector3().normalized * 8f).ToIntVec3().ClampInsideMap(ctx.map);
            var wait = CellFinder.RandomClosewalkCellNear(inward, ctx.map, 4);
            LordMaker.MakeNewLord(ctx.enemy, new LordJob_VisitOnErrand(ctx.enemy, wait.IsValid ? wait : entry, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), ctx.map, carts);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        protected override LookTargets Look(HappeningContext ctx) => new LookTargets(PeoplesG.Party(ctx).Where(p => p.Spawned && (p == ctx.stranger || p.Faction == ctx.enemy && p.RaceProps.Humanlike)));

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 2) return null;
            if (!RoadToll.Here(RoadToll.Carts(ctx), ctx.map)) return "CC_PEv10_CartsGone".Translate().Resolve();
            if (option == 2 && (ctx.a == null || ctx.a.Dead || ctx.a.MapHeld != ctx.map || !PeoplesG.Up(ctx.stranger, ctx.map)))
                return "CC_PEv10_TalkerGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && option > 2) PeoplesG.Say(ctx.stranger, Ext, ctx, "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's answer: <see cref="side"/> "cross" (the carts across its land), "back" (turned back), "fair" (a fair toll set at the gate).</summary>
    public class DilemmaEffect_RoadToll : DilemmaEffect
    {
        public string side = "fair";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction == null || hc.enemy == null) return;
            switch (side)
            {
                case "cross":
                    sheet.Play("CC_PEv10_Impact_Cross".Translate(hc.enemy.Name).Resolve(), "CC_PEv10_Impact_CrossBrief".Translate().Resolve());
                    break;
                case "back":
                    sheet.Play("CC_PEv10_Impact_Back".Translate(hc.enemy.Name).Resolve(), "CC_PEv10_Impact_BackBrief".Translate().Resolve());
                    break;
                default:
                    if (hc.a != null)
                        sheet.Play("CC_PEv10_Impact_Fair".Translate(hc.a.LabelShort, hc.count).Resolve(), "CC_PEv10_Impact_FairBrief".Translate().Resolve());
                    sheet.Goodwill(hc.faction, PeopleEvents10Math.FairGain);
                    sheet.Goodwill(hc.enemy, PeopleEvents10Math.FairGain);
                    break;
            }
            sheet.Add(ImpactKind.Other, 0, "CC_PEv10_Impact_RegionHears".Translate().Resolve(), "CC_PEv10_Impact_RegionHearsBrief".Translate().Resolve(), "pev10heard");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            switch (side)
            {
                case "cross":
                    RoadToll.Cross(hc);
                    break;
                case "back":
                    RoadToll.Back(hc);
                    break;
                default:
                    RoadToll.Fair(hc);
                    break;
            }
        }
    }
}
