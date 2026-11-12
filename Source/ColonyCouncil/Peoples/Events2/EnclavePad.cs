using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 25. A pad by the colony (the spacer enclave): their camp =====================

    public class PadState : CampWorkState
    {
        /// <summary>The pad's cells, as they planned them, and how many are laid.</summary>
        public List<IntVec3> pad = new List<IntVec3>();
        /// <summary>The morning's stock come down by pod, waiting at the pad to be taken into their stalls.</summary>
        public List<Thing> stock = new List<Thing>();
        public int stockDay = -1, stockLanded = -1, offDay = -1;
        /// <summary>The colony agreed to keep its people off the pad; the band their stock drew is rolled; asked once each.</summary>
        public bool keepOff, bandRolled, asked, mealsAsked, raised, rentFree;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref pad, "pad", LookMode.Value);
            Scribe_Collections.Look(ref stock, "stock", LookMode.Reference);
            Scribe_Values.Look(ref stockDay, "stockDay", -1);
            Scribe_Values.Look(ref stockLanded, "stockLanded", -1);
            Scribe_Values.Look(ref offDay, "offDay", -1);
            Scribe_Values.Look(ref keepOff, "keepOff");
            Scribe_Values.Look(ref bandRolled, "bandRolled");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref raised, "raised");
            Scribe_Values.Look(ref rentFree, "rentFree");
            Scribe_Values.Look(ref mealsAsked, "mealsAsked");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pad ??= new List<IntVec3>();
                stock ??= new List<Thing>();
                stock.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>
    /// The enclave's pad (item 25): two factors and two marshals rent ground at the colony's edge for ten to fifteen days,
    /// paying rent every five days at an exact figure (the camps' own fee, Camp.termFee), and lay a small pad (a marshal at
    /// work on it cell by cell) by their stalls. A pod drops fresh stock at the pad each morning, so the colony can trade every
    /// day (the game's own trading, with their factor). They ask the colony to keep its people off the pad, and to sell them
    /// its meals. It can turn: they try to shave the rent (the camps' terms bent), a band comes for the stock (bands against a
    /// camp, likelier for what the pods bring), or the colony raises the rent (they pay, or pack up and go). Their spacer kit
    /// whatever the colony's age; a tribal colony sees pods fall from the sky at dawn.
    /// </summary>
    public static class EnclavePad
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_EnclavePad");

        public static TerrainDef PadFloor => DefDatabase<TerrainDef>.GetNamedSilentFail("MetalTile") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("PavedTile");

        public static JobDef PadJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv2_LayPad");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        /// <summary>The pad's cells: a square beside their camp on ground that bears the floor, nothing built on it.</summary>
        public static List<IntVec3> PadCells(Camp c)
        {
            var map = c.map;
            var floor = PadFloor;
            var list = new List<IntVec3>();
            if (map == null || floor == null) return list;
            int half = PeopleEvents2Math.PadSize / 2;
            foreach (var dir in new[] { new IntVec3(7, 0, 0), new IntVec3(-7, 0, 0), new IntVec3(0, 0, 7), new IntVec3(0, 0, -7) })
            {
                var mid = c.center + dir;
                var rect = CellRect.CenteredOn(mid, half);
                if (!rect.InBounds(map)) continue;
                var cells = rect.Cells.Where(x => x.InBounds(map) && x.GetEdifice(map) == null && !x.GetTerrain(map).IsWater && x.SupportsStructureType(map, floor.terrainAffordanceNeeded)).ToList();
                if (cells.Count >= PeopleEvents2Math.PadSize * PeopleEvents2Math.PadSize - 4)
                {
                    list = cells;
                    break;
                }
            }
            return list;
        }

        /// <summary>
        /// The camp made and set going: two factors and two marshals of the enclave, walking in from where they set down, its
        /// rent an exact figure every five days (or nothing, as friends).
        /// </summary>
        public static Camp Start(HappeningContext ctx, bool free)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            var f = ctx?.faction;
            if (map == null || tracker == null || kind == null || f == null || !CampTracker.Enabled) return null;
            try
            {
                var people = new List<Pawn>();
                foreach (var k in new[] { "Rimfolk_Enclave_Factor", "Rimfolk_Enclave_Factor", "Rimfolk_Enclave_Marshal", "Rimfolk_Enclave_Marshal" })
                {
                    var p = Arrivals.Make(PeoplesD.KindNamed(f, k), f, kind.ages);
                    if (p != null) people.Add(p);
                }
                if (people.Count < 2) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 12);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                center = CampLand.SiteFor(kind, map, center, hot);
                if (!center.IsValid) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, f, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                var st = new PadState { rentFree = free };
                c.workState = st;
                if (!free)
                {
                    c.termFee = true;
                    c.fee = ctx.count;
                    c.nextFeeTick = Now + PeopleEvents2Math.RentDays * GenDate.TicksPerDay;
                }
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the enclave's pad", e);
                return null;
            }
        }

        /// <summary>The colony raises the rent: they pay half again, or (by their pride and their mood) pack up and go. What happened, told.</summary>
        public static void Raise(Camp c)
        {
            var st = c?.workState as PadState;
            if (c == null || st == null || st.raised) return;
            st.raised = true;
            int now = Mathf.Max(c.fee, 1);
            if (Rand.Chance(PeopleEvents2Math.PaysRaise(c.kind?.pride ?? 0.3f, c.tension)))
            {
                c.termFee = true;
                c.fee = PeopleEvents2Math.Raised(now);
                c.AddTension(8f);
                c.Note(c.Text("CC_PEv2_PadRaisePaid", null, null, c.fee), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            else
            {
                c.Note(c.Text("CC_PEv2_PadRaiseGone"), MessageTypeDefOf.NegativeEvent, Importance.Notable);
                c.Leave("rent");
            }
        }
    }

    /// <summary>The pad's days: the pad laid, the morning's pod, the stock taken in, the colony kept off it, the band their stock draws.</summary>
    public class CampWorker_EnclavePad : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public static PadState StateOf(Camp c) => c?.workState as PadState;

        public override bool FitsMap(Map map) => !MapFacts.InVacuum(map);

        public override void Started(Camp c)
        {
            if (!(c.workState is PadState)) c.workState = new PadState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is PadState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            if (st.pad.Count == 0 && c.DaysHere >= 0.05f) st.pad = EnclavePad.PadCells(c);
            // Once a stay: the stock draws a band against them, likelier than for a camp of plain goods.
            if (!st.bandRolled && c.Settled && c.raidRolled)
            {
                st.bandRolled = true;
                if (c.raidAt < 0 && Rand.Chance(PeopleEvents2Math.PadBandChance)) c.raidAt = CampRaidMath.AttackTick(c.arrivedTick, c.untilTick, Rand.Value);
            }
            // Their asks, once, on their first morning: keep off the pad; sell them meals.
            if (!st.asked && c.DaysHere >= 0.4f && hour >= 8 && hour <= 12 && Tell.CanAsk)
            {
                var ask = c.AskByKey("pad");
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.asked = true;
            }
            else if (st.asked && !st.mealsAsked && c.DaysHere >= 1.4f && hour >= 11 && hour <= 14 && Tell.CanAsk)
            {
                var ask = c.AskByKey("meals");
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.mealsAsked = true;
            }
            // The morning's pod at dawn.
            if (hour == PeopleEvents2Math.StockHour && st.stockDay != GenDate.DaysPassed && st.pad.Count > 0 && c.trader != null && c.Here.Contains(c.trader))
            {
                st.stockDay = GenDate.DaysPassed;
                Stock(c, st);
            }
            // The stock that came down taken into their stalls once it has landed.
            if (st.stockLanded >= 0 && Now - st.stockLanded >= GenDate.TicksPerHour) TakeIn(c, st);
            // The colony's people on the pad, when it agreed to keep them off: the factors notice.
            if (st.keepOff && st.offDay != GenDate.DaysPassed && st.pad.Count > 0)
            {
                var on = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Drafted && st.pad.Contains(p.Position));
                if (on != null)
                {
                    st.offDay = GenDate.DaysPassed;
                    c.AddTension(3f);
                    var speaker = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                    if (speaker != null) c.SayMoment(speaker, "offpad");
                    c.Note(c.Text("CC_PEv2_PadTrespass", null, on), MessageTypeDefOf.NeutralEvent, Importance.Flavor, on);
                }
            }
        }

        /// <summary>A pod with fresh stock at the pad: their trader's kind's own goods, a smaller lot each morning.</summary>
        private static void Stock(Camp c, PadState st)
        {
            var map = c.map;
            var tk = c.trader?.trader?.traderKind;
            if (tk == null) return;
            try
            {
                var made = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = tk, tile = map.Tile, makingFaction = c.faction });
                var lot = new List<Thing>();
                float budget = Rand.Range(250f, 450f) * LivingEvents.WealthFactor(map), value = 0f;
                foreach (var t in made.InRandomOrder())
                {
                    if (t is Pawn p)
                    {
                        if (!p.Destroyed && !Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                        continue;
                    }
                    if (t.def == ThingDefOf.Silver || t.MarketValue <= 0f || value + t.MarketValue * t.stackCount > budget)
                    {
                        if (!t.Destroyed) t.Destroy();
                        continue;
                    }
                    value += t.MarketValue * t.stackCount;
                    lot.Add(t);
                }
                if (lot.Count == 0) return;
                var at = st.pad[st.pad.Count / 2];
                DropPodUtility.DropThingsNear(at, map, lot, 110, false, false, true, true);
                st.stock.AddRange(lot);
                st.stockLanded = Now;
                string key = PeopleEvents2.ColonyAge == 0 ? "CC_PEv2_PadPodTribal" : "CC_PEv2_PadPod";
                c.Note(c.Text(key), MessageTypeDefOf.NeutralEvent, Importance.Flavor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the morning's pod", e);
            }
        }

        /// <summary>The stock that landed, still lying at the pad, carried into their trader's stall.</summary>
        private static void TakeIn(Camp c, PadState st)
        {
            st.stockLanded = -1;
            var trader = c.trader;
            if (trader?.inventory == null || !c.Here.Contains(trader)) return;
            int taken = 0;
            foreach (var t in st.stock.ToList())
            {
                if (t == null || t.Destroyed || !t.Spawned || t.Map != c.map || st.pad.Count > 0 && !t.Position.InHorDistOf(st.pad[st.pad.Count / 2], 9f)) continue;
                t.DeSpawn();
                if (trader.inventory.innerContainer.TryAdd(t)) taken++;
            }
            st.stock.Clear();
            if (taken > 0) c.SayMoment(trader, "stock", 0.5f);
        }

        /// <summary>By day, a marshal lays the pad cell by cell until it is down.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            var job = EnclavePad.PadJob;
            var floor = EnclavePad.PadFloor;
            if (st == null || job == null || floor == null || p == c.trader || p == c.leader || st.pad.Count == 0) return null;
            if (c.Here.Any(o => o != p && o.CurJobDef == job)) return null;
            var map = c.map;
            var left = st.pad.Where(x => x.InBounds(map) && x.GetTerrain(map) != floor && x.GetEdifice(map) == null && p.CanReach(x, PathEndMode.Touch, Danger.Some)).Take(1).ToList();
            if (left.Count == 0) return null;
            var j = JobMaker.MakeJob(job, left[0]);
            j.count = 600;
            return j;
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.raised) marks.Add("raised");
            if (st.keepOff) marks.Add("keptoff");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StateOf(c);
            if (a?.key == "raise" && (st == null || st.raised || st.rentFree)) return "CC_CampAsk_Done".Translate().Resolve();
            return null;
        }

        /// <summary>The colony agreed to keep its people off the pad.</summary>
        public static void KeepOff(Camp c)
        {
            var st = StateOf(c);
            if (st != null) st.keepOff = true;
        }
    }

    /// <summary>A marshal lays one cell of their pad: to it, a bar of work, the floor laid. TargetA the cell, count the ticks.</summary>
    public class JobDriver_LayPad : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("LayPad");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
                if (pawn.IsHashIntervalTick(240, delta)) FleckMaker.ThrowMicroSparks(job.targetA.Cell.ToVector3Shifted(), pawn.Map);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("PadLaid");
            done.initAction = () =>
            {
                var c = job.targetA.Cell;
                var floor = EnclavePad.PadFloor;
                if (floor != null && c.InBounds(pawn.Map) && c.GetEdifice(pawn.Map) == null && c.SupportsStructureType(pawn.Map, floor.terrainAffordanceNeeded)) pawn.Map.terrainGrid.SetTerrain(c, floor);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// A factor of {FACTION} at the gate, asking to rent ground at the colony's edge for a pad and stalls, ten to fifteen
    /// days, at {COUNT} silver every five days (an exact figure, to the last digit).
    /// </summary>
    public class HappeningWorker_EnclavePad : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Brokers);
            return EnclavePad.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && f != null && !EnclaveTerms.InDefault(f) && EnclavePad.On(map) == null
                   && !MapFacts.InVacuum(map) && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Brokers);
            if (ctx.faction == null) return false;
            ctx.count = PeopleEvents2Math.PadRent(LivingEvents.WealthFactor(ctx.map), Rand.RangeInclusive(1, 9));
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Factor"), ctx.faction, ext.ages);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PeopleEvents2.ColonyAge == 0) tags.Add("tribal");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (EnclavePad.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "rent" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The ground rented: the camp comes (EnclavePad.Start), at their rent or, with <see cref="free"/>, for nothing as friends.</summary>
    public class DilemmaEffect_EnclavePad : DilemmaEffect
    {
        public bool free;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 0, "CC_PEv2_Impact_Pad".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_PadBrief".Translate().Resolve(), "pev2pad");
            if (!free) sheet.Add(ImpactKind.Goods, 1, "CC_PEv2_Impact_PadRent".Translate(hc.count, PeopleEvents2Math.RentDays).Resolve(), "CC_PEv2_Impact_PadRentBrief".Translate().Resolve(), "pev2padrent");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv2_Impact_PadTrade".Translate().Resolve(), "CC_PEv2_Impact_PadTradeBrief".Translate().Resolve(), "pev2padtrade");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv2_Impact_PadBand".Translate().Resolve(), "CC_PEv2_Impact_PadBandBrief".Translate().Resolve(), "pev2padband");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            var c = EnclavePad.Start(hc, free);
            if (c != null) Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The pad's asks answered: the colony keeps its people off it; sells them meals (paid at their worth and a tenth); raises the rent.</summary>
    public class DilemmaEffect_PadAsk : DilemmaEffect
    {
        /// <summary>"keepoff", "meals" or "raise".</summary>
        public string what;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            what == "meals" && PlayCarry.Have(ctx?.map, "meals", null) < PeopleEvents2Math.MealsAsked
                ? "CC_Dilemma_NotEnough".Translate(PeopleEvents2Math.MealsAsked, PlayCarry.Label("meals", null)).ToString()
                : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_EnclavePad);
            if (c == null) return;
            switch (what)
            {
                case "meals":
                    sheet.Goods(-1, PlayCarry.Label("meals", null), PeopleEvents2Math.MealsAsked, "meals");
                    sheet.Silver(MealsPay(c.map));
                    break;
                case "raise":
                    int pays = Mathf.RoundToInt(PeopleEvents2Math.PaysRaise(c.kind?.pride ?? 0.3f, c.tension) * 100f);
                    sheet.Add(ImpactKind.Goods, 0, "CC_PEv2_Impact_PadRaise".Translate(PeopleEvents2Math.Raised(Mathf.Max(1, c.fee)), pays).Resolve(), "CC_PEv2_Impact_PadRaiseBrief".Translate().Resolve(), "pev2padraise");
                    break;
                default:
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv2_Impact_PadOff".Translate().Resolve(), "CC_PEv2_Impact_PadOffBrief".Translate().Resolve(), "pev2padoff");
                    break;
            }
        }

        /// <summary>What ten of the colony's meals fetch from them: their worth and a tenth.</summary>
        private static int MealsPay(Map map)
        {
            var meals = PlayCarry.Stock(map, "meals", null).Take(PeopleEvents2Math.MealsAsked).ToList();
            float worth = meals.Sum(t => t.def.BaseMarketValue) / Mathf.Max(1, meals.Count) * PeopleEvents2Math.MealsAsked;
            return PeopleEvents2Math.MealsPrice(worth);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_EnclavePad);
            if (c == null) return;
            switch (what)
            {
                case "meals":
                {
                    int pay = MealsPay(c.map);
                    CampAnswers.Carry(c, "meals", PeopleEvents2Math.MealsAsked, null, "CC_PEv2_PadMealsLabel".Translate(c.Label.Named("NAME")).Resolve());
                    // Paid at the camp's edge, where the colony takes up what they leave for it.
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = pay;
                    var at = c.share.IsValid ? c.share : c.center;
                    if (GenPlace.TryPlaceThing(silver, at, c.map, ThingPlaceMode.Near, out var placed)) placed?.SetForbidden(false, false);
                    c.Note(c.Text("CC_PEv2_PadMealsPaid", null, null, pay), MessageTypeDefOf.PositiveEvent, Importance.Notable, placed);
                    break;
                }
                case "raise":
                    EnclavePad.Raise(c);
                    break;
                default:
                    CampWorker_EnclavePad.KeepOff(c);
                    break;
            }
        }
    }
}
