using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 30. The boat-builders (the river folk) =====================

    /// <summary>The boat-builders' days: the hull on the bank and its stage, the day the water cost them, whether they may fell the colony's trees, the dinghy sold.</summary>
    public class BoatState : CampWorkState
    {
        public Thing hull;
        public IntVec3 hullCell = IntVec3.Invalid;
        public int stage, worked;
        public float lost;
        public bool trees = true, storm, sold;

        public override void ExposeData()
        {
            Scribe_References.Look(ref hull, "hull");
            Scribe_Values.Look(ref hullCell, "hullCell", IntVec3.Invalid);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref worked, "worked");
            Scribe_Values.Look(ref lost, "lost");
            Scribe_Values.Look(ref trees, "trees", true);
            Scribe_Values.Look(ref storm, "storm");
            Scribe_Values.Look(ref sold, "sold");
        }
    }

    /// <summary>
    /// The boat-builders (item 30): three or four of the river folk camp by the colony's water for a week, fell wild trees
    /// (the camps' own felling), and a hull grows on the bank day by day: the keel, the ribs, the planked hull. The colony's
    /// crafter may work beside them (a lesson). With Vehicle Framework and Vanilla Vehicles Expanded they offer the finished
    /// dinghy for wood and silver; otherwise they float it home, and leave their boat shed standing. It can turn: heavy rain
    /// lifts the water and costs them a day, or the colony refuses them its trees (they bring their own, and go cooler).
    /// </summary>
    public static class BoatBuilders
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_BoatBuilders");

        /// <summary>The hull's three stages on the bank.</summary>
        public static ThingDef HullDef(int stage) => stage <= 0 ? null : DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_BoatHull_" + Mathf.Clamp(stage, 1, PeopleEvents4Math.HullStages));

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static BoatState StateOf(Camp c) => c?.workState as BoatState;

        /// <summary>Grown wild trees on the land (none in a growing zone): enough for a hull.</summary>
        public static int WildTrees(Map map) =>
            map?.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Count(t => t is Plant p && p.def.plant != null && p.def.plant.IsTree && p.Growth >= 0.9f && !(map.zoneManager.ZoneAt(p.Position) is Zone_Growing))
            ?? 0;

        // ---------------- Vehicle Framework's dinghy (by name) ----------------

        private static bool looked;
        private static MethodInfo generate;

        /// <summary>Vanilla Vehicles Expanded's dinghy, when it and Vehicle Framework's way of making a vehicle are there.</summary>
        public static ThingDef Dinghy => Vehicle("VVE_Dinghy");

        /// <summary>A vehicle of Vehicle Framework's by name (the dinghy here, round 5's wagon), when it and the way of making one are there.</summary>
        public static ThingDef Vehicle(string defName)
        {
            {
                if (!looked)
                {
                    looked = true;
                    try
                    {
                        generate = GenTypes.GetTypeInAnyAssembly("Vehicles.VehicleSpawner")?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "GenerateVehicle" && m.GetParameters().Length == 2 && typeof(ThingDef).IsAssignableFrom(m.GetParameters()[0].ParameterType)
                                                 && m.GetParameters()[1].ParameterType == typeof(Faction));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 4: Vehicle Framework", e);
                        generate = null;
                    }
                }
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                return generate != null && def != null && generate.GetParameters()[0].ParameterType.IsInstanceOfType(def) ? def : null;
            }
        }

        /// <summary>A vehicle of that def made for the people (Vehicle Framework's own way), unspawned; null when it can't be.</summary>
        public static Thing MakeVehicle(ThingDef def, Faction f)
        {
            if (def == null || Vehicle(def.defName) == null) return null;
            try
            {
                return generate.Invoke(null, new object[] { def, f }) as Thing;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: a vehicle made", e);
                return null;
            }
        }

        /// <summary>The camp made by the colony's water (the herders' way), its builders walking in from the edge.</summary>
        public static Camp Start(HappeningContext ctx, bool trees)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var water = DryWater.Water(map);
                var center = water.IsValid ? CampLand.TryFindNear(map, water, 4f, 12f, hot) : IntVec3.Invalid;
                if (!center.IsValid) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new BoatState { trees = trees };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the boat-builders' camp", e);
                return null;
            }
        }

        private static bool Water(IntVec3 c, Map map) => c.InBounds(map) && (c.GetTerrain(map)?.IsWater ?? false);

        /// <summary>Where the hull lies: dry, open bank beside the water, near their camp.</summary>
        public static IntVec3 HullCell(Camp c)
        {
            var map = c.map;
            foreach (var x in GenRadial.RadialCellsAround(c.center, 14f, false))
            {
                if (!x.InBounds(map) || Water(x, map) || !x.Standable(map) || x.GetEdifice(map) != null || x.GetFirstItem(map) != null || x.GetFirstPawn(map) != null || x.Roofed(map)) continue;
                if (!GenAdj.CellsAdjacentCardinal(x, Rot4.North, IntVec2.One).Any(n => Water(n, map))) continue;
                return x;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The hull set at its stage: the one before taken up, the next laid where it lay.</summary>
        public static void Grow(Camp c, BoatState st, int stage)
        {
            var def = HullDef(stage);
            var map = c.map;
            if (def == null || map == null) return;
            if (!st.hullCell.IsValid) st.hullCell = HullCell(c);
            if (!st.hullCell.IsValid) return;
            try
            {
                if (st.hull != null && !st.hull.Destroyed) st.hull.Destroy();
                var t = ThingMaker.MakeThing(def);
                t.SetFaction(c.faction);
                st.hull = GenSpawn.Spawn(t, st.hullCell, map);
                st.stage = stage;
                FleckMaker.ThrowDustPuff(st.hullCell, map, 1.5f);
                var builder = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                if (builder != null) c.SayMoment(builder, "hull");
                c.Note(c.Text("CC_PEv4_Hull_" + stage, builder), MessageTypeDefOf.NeutralEvent, stage >= PeopleEvents4Math.HullStages ? Importance.Notable : Importance.Flavor, st.hull, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the hull", e);
            }
        }

        /// <summary>A builder's turn at the hull done: the work counted, a shaving or two.</summary>
        public static void Worked(Pawn builder, Thing hull)
        {
            var c = CampTracker.VisitorsAt(builder?.Map).FirstOrDefault(x => x.kind == Kind);
            var st = StateOf(c);
            if (st == null || hull == null) return;
            st.worked++;
            if (Rand.Chance(0.3f)) c.SayMoment(builder, "hull");
        }

        /// <summary>The finished dinghy, sold to the colony: Vehicle Framework's vehicle of it set on the water by the hull, the hull taken up.</summary>
        public static bool Sell(Camp c)
        {
            var st = StateOf(c);
            var def = Dinghy;
            var map = c?.map;
            if (st == null || def == null || map == null || st.sold || st.hull == null || st.hull.Destroyed) return false;
            var at = st.hull.Position;
            var water = GenAdj.CellsAdjacentCardinal(at, Rot4.North, IntVec2.One).FirstOrDefault(n => Water(n, map));
            try
            {
                var boat = generate.Invoke(null, new object[] { def, Faction.OfPlayer }) as Thing;
                if (boat == null) return false;
                st.hull.Destroy();
                GenSpawn.Spawn(boat, water.IsValid && water != IntVec3.Zero ? water : at, map, Rot4.East);
                st.sold = true;
                c.Note(c.Text("CC_PEv4_DinghySold", c.leader), MessageTypeDefOf.PositiveEvent, Importance.Notable, boat, true);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the dinghy", e);
                return false;
            }
        }
    }

    /// <summary>The boat-builders' camp: the hull by the day, the felling (or not), the water rising, the dinghy, the boat floated home.</summary>
    public class CampWorker_BoatBuilders : CampWorker
    {
        public override bool FitsMap(Map map) => DryWater.HasWater(map);

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var water = DryWater.Water(map);
            if (!water.IsValid) return usual;
            var c = CampLand.TryFindNear(map, water, 4f, 12f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is BoatState)) c.workState = new BoatState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is BoatState st))
            {
                Started(c);
                st = BoatBuilders.StateOf(c);
            }
            // The colony's trees refused: they work their own timber and fell nothing.
            if (!st.trees) c.chopsToday = 0;
            if (!c.Settled || st.sold) return;
            // Heavy rain lifts the water: a day's work lost, once a stay, and they stay a day longer for it.
            if (!st.storm && hour >= 8 && hour <= 17 && (c.map.weatherManager?.curWeather?.rainRate ?? 0f) >= PeopleEvents4Math.StormRain && st.stage > 0 && st.stage < PeopleEvents4Math.HullStages)
            {
                st.storm = true;
                st.lost += 1f;
                if (c.untilTick != int.MaxValue) c.untilTick += GenDate.TicksPerDay;
                var builder = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                if (builder != null) c.SayMoment(builder, "storm");
                c.Note(c.Text("CC_PEv4_HullStorm", builder), MessageTypeDefOf.NeutralEvent, Importance.Notable, st.hull, true);
            }
            int stage = PeopleEvents4Math.HullStage(c.DaysHere - st.lost);
            if (stage > st.stage) BoatBuilders.Grow(c, st, stage);
        }

        /// <summary>By day, the builders at the hull (the colony's crafter, lent, follows one of them there).</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = BoatBuilders.StateOf(c);
            var job = PeopleEvents4.HullJob;
            if (st?.hull == null || st.hull.Destroyed || !st.hull.Spawned || st.sold || job == null || !p.RaceProps.Humanlike) return null;
            if (st.stage >= PeopleEvents4Math.HullStages && !Rand.Chance(0.3f)) return null;
            if (!Rand.Chance(p == c.leader ? 0.8f : 0.55f) || !p.CanReach(st.hull, PathEndMode.Touch, Danger.Some)) return null;
            var j = JobMaker.MakeJob(job, st.hull);
            j.count = 1500;
            return j;
        }

        /// <summary>Going: the boat floated home (unless the colony bought it), the shed left standing; the boat's thanks.</summary>
        public override void Leaving(Camp c)
        {
            var st = BoatBuilders.StateOf(c);
            if (st == null || c.map == null) return;
            if (!st.sold && st.hull != null && !st.hull.Destroyed && st.hull.Spawned)
            {
                var at = st.hull.Position;
                for (int i = 0; i < 4; i++) FleckMaker.WaterSplash((at + GenAdj.CardinalDirections[i]).ToVector3Shifted(), c.map, 1.2f, 0.5f);
                st.hull.Destroy();
                if (st.stage >= PeopleEvents4Math.HullStages) c.Note(c.Text("CC_PEv4_HullFloated", c.leader), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
            }
            if (!c.Hostile && c.faction != null && c.faction.HasGoodwill && !c.faction.IsPlayer && st.trees && c.tension < 50f)
                WorldSituationWorker.Goodwill(c.faction, PeopleEvents4Math.BoatGoodwill, GoodwillWhy.Helped);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = BoatBuilders.StateOf(c);
            if (st == null) return;
            if (st.sold) marks.Add("sold");
            else if (st.stage >= PeopleEvents4Math.HullStages) marks.Add("floated");
            if (!st.trees) marks.Add("notrees");
            if (st.storm) marks.Add("storm");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = BoatBuilders.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "dinghy":
                    if (BoatBuilders.Dinghy == null) return "CC_PEv4_NoVehicles".Translate().Resolve();
                    if (st.sold) return "CC_PEv4_DinghyBought".Translate().Resolve();
                    if (st.stage < PeopleEvents4Math.HullStages) return "CC_PEv4_HullUnfinished".Translate().Resolve();
                    return null;
                case "crafter":
                    return st.hull == null || st.hull.Destroyed ? "CC_PEv4_NoHullYet".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A boatwright of the river folk ({X}) at the gate: they would build a boat on the colony's bank, from wild trees of its land.</summary>
    public class HappeningWorker_BoatBuilders : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var kind = BoatBuilders.Kind;
            return kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Fishers) != null && DryWater.HasWater(map) && BoatBuilders.On(map) == null
                   && BoatBuilders.WildTrees(map) >= PeopleEvents4Math.MinTrees && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Fishers);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Master"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Best(ctx.map, SkillDefOf.Crafting) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.count = BoatBuilders.WildTrees(ctx.map);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (BoatBuilders.Dinghy != null) tags.Add("dinghy");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (BoatBuilders.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "trees" : option == 1 ? "own" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The builders come (BoatBuilders.Start); with <see cref="trees"/> false they fell nothing of the colony's and go cooler.</summary>
    public class DilemmaEffect_BoatBuilders : DilemmaEffect
    {
        public bool trees = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv4_Impact_Boat".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv4_Impact_BoatBrief".Translate().Resolve(), "pev4boat");
            if (trees) sheet.Add(ImpactKind.Goods, -1, "CC_PEv4_Impact_Trees".Translate().Resolve(), "CC_PEv4_Impact_TreesBrief".Translate().Resolve(), "pev4trees");
            else if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.TreesRefusedGoodwill);
            if (BoatBuilders.Dinghy != null) sheet.Add(ImpactKind.Goods, 1, "CC_PEv4_Impact_DinghyOffer".Translate().Resolve(), "CC_PEv4_Impact_DinghyOfferBrief".Translate().Resolve(), "pev4dinghy");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (!trees && hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents4Math.TreesRefusedGoodwill, GoodwillWhy.TurnedAway);
            BoatBuilders.Start(hc, trees);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The finished dinghy bought (its silver paid by the ask, its wood carried out by the ask's errand): the boat the colony's.</summary>
    public class DilemmaEffect_BoatDinghy : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_BoatBuilders);
            if (c != null) BoatBuilders.Sell(c);
        }
    }
}
