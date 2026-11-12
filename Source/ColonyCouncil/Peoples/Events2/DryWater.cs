using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 12. Water in the dry year (the herding folk): their camp =====================

    public class DryState : CampWorkState
    {
        /// <summary>The colony gave hay, or its own grazing; the herd strayed once, a predator followed once; when the strayed herd goes back.</summary>
        public bool hay, grazing, asked, strayed, predator;
        public int strayBack = -1, grazeDay = -1, strayDay = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref hay, "hay");
            Scribe_Values.Look(ref grazing, "grazing");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref strayed, "strayed");
            Scribe_Values.Look(ref predator, "predator");
            Scribe_Values.Look(ref strayBack, "strayBack", -1);
            Scribe_Values.Look(ref grazeDay, "grazeDay", -1);
            Scribe_Values.Look(ref strayDay, "strayDay", -1);
        }
    }

    /// <summary>
    /// Water in the dry year (item 12): in a drought (Odyssey's, Vanilla Events Expanded's, or the region's long drought), the
    /// herders bring up to a dozen head and their dogs to the colony's water for five to eight days, camped by it. They ask for
    /// hay carried out, or (with none to spare) the colony's grazing; they offer milk, a hand with the colony's beasts, and a
    /// young beast at the gate after a good stay, and their people remember it (+10, and the region hears). It can turn: the
    /// herd strays into the colony's fields (the camps' own straying, the crops they eat told), or a predator follows it in
    /// (the game's own beast, through the harm budget). Turned away at the gate, a later news line says how many head they lost
    /// on the road (DryWater.LostKind).
    /// </summary>
    public static class DryWater
    {
        public const string LostKind = "pev:lost";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_DryWater");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The drought on: Odyssey's or Vanilla Events Expanded's on this map, or the region's long drought running.</summary>
        public static string Drought(Map map)
        {
            if (map?.gameConditionManager != null)
                foreach (var n in new[] { "Drought", "VEE_Drought" })
                {
                    var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(n);
                    if (def != null && map.gameConditionManager.ConditionIsActive(def)) return n == "Drought" ? "odyssey" : "vee";
                }
            var all = WorldSituations.Instance?.Running;
            return all != null && all.Any(s => s?.def?.defName == "Council_WS_Drought") ? "region" : null;
        }

        /// <summary>Fresh water on the colony's land (a river, a lake, a marsh; never the sea): the nearest to the colony, some way off it.</summary>
        public static IntVec3 Water(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            return map.AllCells.Where(c => (c.x + c.z) % 4 == 0 && c.GetTerrain(map) is TerrainDef t && t.IsWater && !t.IsOcean && !c.Fogged(map) && !c.InHorDistOf(centre, 18f))
                .OrderBy(c => c.DistanceToSquared(centre)).FirstOrDefault(c => c.IsValid && c != IntVec3.Zero);
        }

        public static bool HasWater(Map map) => Water(map) is IntVec3 w && w.IsValid && w != IntVec3.Zero;

        /// <summary>The camp on the map now, if any.</summary>
        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        /// <summary>
        /// The camp made and set going toward the colony's water: their own people and herd walk in from the edge (the one
        /// who asked goes back for them). <paramref name="toll"/>: what they paid at the gate.
        /// </summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 6);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var water = Water(map);
                var center = water.IsValid ? CampLand.TryFindNear(map, water, 3f, 14f, hot) : IntVec3.Invalid;
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new DryState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the herders' camp by the water", e);
                return null;
            }
        }

        /// <summary>Turned away: a news line some days on of the head they lost on the road to the next water.</summary>
        public static void TurnedAway(HappeningContext ctx)
        {
            var map = ctx?.map;
            var f = ctx?.faction;
            var t = CouncilGameComponent.Instance?.happenings;
            if (map == null || f == null || t == null) return;
            var v = t.AddVisit(LostKind, ctx.incident, map, null, null, null, Now + (PeopleEvents2Math.LostNewsMaxDays + 2) * GenDate.TicksPerDay);
            v.text = f.GetUniqueLoadID();
            v.stake = PeopleEvents2Math.LostOnRoad(Mathf.Max(6, ctx.count), Rand.Value);
            v.due = Now + Rand.RangeInclusive(PeopleEvents2Math.LostNewsMinDays, PeopleEvents2Math.LostNewsMaxDays) * GenDate.TicksPerDay;
        }

        /// <summary>The road's news when its day comes: the head they lost, told.</summary>
        public static bool LostHour(Visit v, int now)
        {
            if (v.done) return false;
            if (now < v.due) return true;
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            News.Post("CC_PEv2_DryLost".Translate((f?.Name ?? "?").Named("FACTION"), v.stake.Named("COUNT")).Resolve(), LookTargets.Invalid, NewsKind.World);
            return false;
        }
    }

    /// <summary>The herders' camp by the colony's water: the hay or grazing asked, the herd's day on the colony's ground, and what can turn.</summary>
    public class CampWorker_DryWater : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public static DryState StateOf(Camp c) => c?.workState as DryState;

        public override bool FitsMap(Map map) => DryWater.HasWater(map);

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var water = DryWater.Water(map);
            if (!water.IsValid) return usual;
            var c = CampLand.TryFindNear(map, water, 3f, 14f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is DryState)) c.workState = new DryState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is DryState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var map = c.map;
            // Hay asked for once, their first morning; the colony's grazing when it has none to give.
            if (!st.asked && c.DaysHere >= 0.3f && hour >= 7 && hour <= 11 && Tell.CanAsk)
            {
                string key = PlayCarry.Have(map, "Hay", ThingDefOf.Hay) >= PeopleEvents2Math.HayAsked ? "hay" : "grazing";
                var ask = c.AskByKey(key);
                if (ask != null && c.AskBlocked(ask) == null && c.Request(ask)) st.asked = true;
            }
            // On the colony's grazing, the herd is out on its ground each morning.
            if (st.grazing && hour == 8 && st.grazeDay != GenDate.DaysPassed)
            {
                st.grazeDay = GenDate.DaysPassed;
                Graze(c);
            }
            // The strayed herd eats its fill, and goes back.
            if (st.strayBack >= 0 && Now >= st.strayBack)
            {
                st.strayBack = -1;
                if (c.Strayed)
                {
                    int eaten = c.EatCrops(Rand.RangeInclusive(4, 9));
                    if (eaten > 0)
                        Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv2_DryAte".Translate((c.faction?.Name ?? "?").Named("FACTION"), eaten.Named("COUNT")).Resolve(),
                            new LookTargets(new TargetInfo(c.errandCell, map)), MessageTypeDefOf.NegativeEvent);
                    c.EndErrand();
                }
            }
            // Once a stay, the herd strays into the colony's fields (twice as likely out on its grazing).
            if (!st.strayed && hour >= 10 && hour <= 15 && st.strayDay != GenDate.DaysPassed && c.herd.Count > 0 && !c.ErrandSet)
            {
                st.strayDay = GenDate.DaysPassed;
                if (Rand.Chance(PeopleEvents2Math.StrayChance(st.grazing))) Stray(c, st);
            }
            // Once a stay, a predator of the land follows the herd in at night (the harm budget's camp draw).
            if (!st.predator && hour == 2 && c.DaysHere >= 1f)
            {
                st.predator = true;
                Predator(c);
            }
        }

        private static void Graze(Camp c)
        {
            var map = c.map;
            foreach (var a in c.herd.Where(h => h != null && h.Spawned && !h.Dead && !h.Downed && h.Map == map && h.jobs != null).Take(8))
            {
                try
                {
                    var cell = PlaySpots.OpenCell(map, null);
                    if (!cell.IsValid || !CellFinder.TryFindRandomCellNear(cell, map, 12, x => x.Standable(map) && !x.Roofed(map) && a.CanReach(x, PathEndMode.OnCell, Danger.Some), out var spot)) continue;
                    var job = JobMaker.MakeJob(JobDefOf.GotoWander, spot);
                    job.locomotionUrgency = LocomotionUrgency.Walk;
                    a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 2: the herd out grazing", e);
                    return;
                }
            }
            c.Note(c.Text("CC_PEv2_DryGrazing"), MessageTypeDefOf.NeutralEvent, Importance.Flavor);
        }

        private static void Stray(Camp c, DryState st)
        {
            var map = c.map;
            var field = PlaySpots.FieldCell(map, null);
            if (!field.IsValid || !c.StrayHerd(field)) return;
            st.strayed = true;
            st.strayBack = Now + 3 * GenDate.TicksPerHour;
            var beast = c.herd.FirstOrDefault(h => h != null && h.Spawned);
            if (beast != null) c.SayMoment(c.Here.FirstOrDefault(p => p.RaceProps.Humanlike) ?? beast, "stray", 1f);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv2_DryStray".Translate(c.Label.Named("NAME"), (c.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(new TargetInfo(field, map)), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A predator of the land, drawn by the herd: in at the edge nearest the camp, after one of the herd (the game's own hunt).</summary>
        private static void Predator(Camp c)
        {
            var map = c.map;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, PeopleEvents2Math.PredatorChance)) return;
            var biome = map.Biome;
            var kind = biome == null ? null : BiomeAnimals.Of(biome).Where(k => k.RaceProps.predator && k.RaceProps.baseBodySize >= 0.6f && k.RaceProps.baseBodySize <= 2.4f
                                                                        && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race))
                .RandomElementByWeightWithFallback(k => BiomeAnimals.Commonality(biome, k));
            var prey = c.herd.Where(h => h != null && h.Spawned && !h.Dead && h.Map == map).RandomElementWithFallback();
            if (kind == null || prey == null) return;
            try
            {
                var edge = LegendBeasts.EdgeToward(map, c.center);
                if (!CellFinder.TryFindRandomCellNear(edge, map, 8, x => x.Standable(map) && !x.Fogged(map), out var entry) && !RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f)) return;
                var beast = PawnGenerator.GeneratePawn(kind);
                GenSpawn.Spawn(beast, entry, map);
                if (beast.needs?.food != null) beast.needs.food.CurLevelPercentage = 0.15f;
                var hunt = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
                hunt.killIncappedTarget = true;
                beast.jobs.StartJob(hunt, JobCondition.InterruptForced, null, false, true);
                HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
                c.Note(c.Text("CC_PEv2_DryPredator", null, null, 0, beast.LabelIndefinite()), MessageTypeDefOf.ThreatSmall, Importance.Notable, beast, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: a predator after the herd", e);
            }
        }

        /// <summary>Gone after a good stay: their people remember the water (+10), and the region hears of it.</summary>
        public override void Leaving(Camp c)
        {
            var f = c.faction;
            if (f == null || c.Hostile || c.tension >= 40f) return;
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.DryGoodwill, false);
            FactionMemory.Note(f, "campHelped", "CC_PEv2_DryName".Translate().Resolve());
            News.Post("CC_PEv2_DryRegion".Translate(f.Name.Named("FACTION")).Resolve(), LookTargets.Invalid, NewsKind.World);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st == null) return;
            if (st.hay) marks.Add("hay");
            if (st.grazing) marks.Add("grazing");
            if (st.strayed) marks.Add("strayed");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.needs != "herdHere") return null;
            return c.herd.Any(h => h != null && h.Spawned && !h.Dead) ? null : "CC_CampErrand_NoHerd".Translate().Resolve();
        }

        /// <summary>The hay given (the ask's effect): noted for their farewell.</summary>
        public static void Hay(Camp c)
        {
            var st = StateOf(c);
            if (st != null) st.hay = true;
        }

        /// <summary>The colony's grazing given: the herd out on its ground each morning from now (the first this morning).</summary>
        public static void Grazing(Camp c)
        {
            var st = StateOf(c);
            if (st == null) return;
            st.grazing = true;
            st.grazeDay = GenDate.DaysPassed;
            Graze(c);
        }
    }

    /// <summary>
    /// An elder of {FACTION}'s herders at the gate in the drought ({TALE}): their herd, {COUNT} head and their dogs, would come
    /// to the colony's water and camp by it for some days; or for a toll ({THING}... told on the card); or turned away.
    /// </summary>
    public class HappeningWorker_HerdersDrought : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var kind = DryWater.Kind;
            return kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Herders) != null && DryWater.Drought(map) != null && DryWater.HasWater(map)
                   && DryWater.On(map) == null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Herders);
            if (ctx.faction == null) return false;
            ctx.note = DryWater.Drought(ctx.map);
            ctx.count = Rand.RangeInclusive(8, PeopleEvents2Math.HerdMax);
            ctx.variant = PeopleEvents2Math.Toll(ctx.count, LivingEvents.WealthFactor(ctx.map));
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Elder"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            // {BEAST} names the toll (the event has no beast of its own to name).
            ctx.beast = ctx.variant.ToString();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (DryWater.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "water" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The camp comes to the colony's water (DryWater.Start); with <see cref="toll"/> they pay at the gate first, and think less of it.</summary>
    public class DilemmaEffect_DryWaterCamp : DilemmaEffect
    {
        public bool toll;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 0, "CC_PEv2_Impact_DryCamp".Translate(hc.faction?.Name ?? "?", hc.count).Resolve(), "CC_PEv2_Impact_DryCampBrief".Translate().Resolve(), "pev2dry");
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv2_Impact_DryRisk".Translate().Resolve(), "CC_PEv2_Impact_DryRiskBrief".Translate().Resolve(), "pev2dryrisk");
            if (toll)
            {
                sheet.Silver(hc.variant);
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents2Math.TollGoodwill);
            }
            else if (hc.faction != null)
                sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv2_Impact_DryThanks".Translate(hc.faction.Name, PeopleEvents2Math.DryGoodwill).Resolve(), "CC_PEv2_Impact_DryThanksBrief".Translate().Resolve(), "pev2drythanks");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (toll)
            {
                var x = hc.stranger;
                Couriers.PaySilver(hc.map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, hc.variant, hc.faction, PeoplesG.Up(x, hc.map) ? x : null);
                if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents2Math.TollGoodwill, false);
            }
            DryWater.Start(hc);
            // The elder goes back to bring the herd in.
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>Turned away: they think less of the colony, and the road's news follows (DryWater.TurnedAway).</summary>
    public class DilemmaEffect_DryTurnedAway : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents2Math.TurnedAwayGoodwill);
            sheet.Add(ImpactKind.Other, -1, "CC_PEv2_Impact_DryTurned".Translate(ctx?.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_DryTurnedBrief".Translate().Resolve(), "pev2dryturned");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents2Math.TurnedAwayGoodwill, false);
            DryWater.TurnedAway(hc);
        }
    }

    /// <summary>The herders' asks answered: the hay noted (carried out by the ask's own effect), or the colony's grazing given.</summary>
    public class DilemmaEffect_DryAsk : DilemmaEffect
    {
        public bool grazing;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (grazing) sheet.Add(ImpactKind.Goods, -1, "CC_PEv2_Impact_DryGrazing".Translate().Resolve(), "CC_PEv2_Impact_DryGrazingBrief".Translate().Resolve(), "pev2drygrazing");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_DryWater);
            if (c == null) return;
            if (grazing) CampWorker_DryWater.Grazing(c);
            else CampWorker_DryWater.Hay(c);
        }
    }
}
