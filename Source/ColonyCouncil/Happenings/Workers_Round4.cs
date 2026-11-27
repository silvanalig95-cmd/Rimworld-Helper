using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content round 4, its eight events of everyday life on the map (docs/content-round-4-camps-events.md, R4-E1 to R4-E8;
    // Defs/IncidentDefs/Rimfolk_Round4_Events.xml): crows on the new field, meltwater in the deep rooms, midges at dusk, what
    // the storm threw up on the shore, ice on the paths, the old tree by the house, stones in the field and washing day. Each
    // in every age, each answer playing out on the map. What an event keeps on the map is one of the things OnMapTimed keeps
    // (PlayOut/OnMapTimed.cs: R4ECrows, R4EMelt, R4EMidges, R4EShore, R4EIce); the steps are here (R4EMap), the shared helpers
    // are rounds 2 and 3's (Round2Map, Round3Map). Every hurt goes through the shared harm budget (a midge's bites, the land's
    // ailments; a slip on the ice, the dangers on the map), is named under the answer before it's chosen and is told when it
    // happens. The rules without game types are in Round4CampsEventsMath. Event cards, so plain words; the spoken lines are
    // tagged with their moment.

    /// <summary>The fourth round's part of what is kept on the map: the steps of each, and what is left when it ends.</summary>
    public static class R4EMap
    {
        /// <summary>The round's events, for dev mode.</summary>
        public static readonly string[] Events =
        {
            "Rimfolk_Evt_R4ECrows", "Rimfolk_Evt_R4EMeltwater", "Rimfolk_Evt_R4EMidges", "Rimfolk_Evt_R4EStormShore", "Rimfolk_Evt_R4EIcyPaths",
            "Rimfolk_Evt_R4EOldTree", "Rimfolk_Evt_R4EFieldStones", "Rimfolk_Evt_R4EWashingDay"
        };

        private static int Now => Find.TickManager.TicksGame;

        public static int Ticks(float hours) => Mathf.RoundToInt(hours * GenDate.TicksPerHour);

        public static float HoursSince(int tick) => (Now - tick) / (float)GenDate.TicksPerHour;

        public static bool Step(TimedThing k, int now)
        {
            switch (k.kind)
            {
                case TimedKind.R4ECrows: return R4ECrowsMap.Step(k, now);
                case TimedKind.R4EMelt: return R4EMeltMap.Step(k, now);
                case TimedKind.R4EMidges: return R4EMidgesMap.Step(k, now);
                case TimedKind.R4EShore: return R4EShoreMap.Step(k, now);
                case TimedKind.R4EIce: return R4EIceMap.Step(k, now);
                default: return true;
            }
        }

        public static void End(TimedThing k, bool early)
        {
            switch (k.kind)
            {
                case TimedKind.R4ECrows:
                    R4ECrowsMap.End(k);
                    break;
                case TimedKind.R4EMelt:
                    R4EMeltMap.End(k);
                    break;
                case TimedKind.R4EShore:
                    // Over before the sea took it back (the cap, a lost map): what still lies there is no longer held from the haulers.
                    if (early)
                        foreach (var t in k.things.Where(t => t != null && t.Spawned && !t.Destroyed))
                            t.SetForbidden(false, false);
                    break;
            }
        }

        /// <summary>The thing kept on the map for this event (made again if a load or the cap lost it).</summary>
        public static TimedThing Of(TimedKind kind, DilemmaContext ctx, float hours = 2f)
        {
            var h = ctx as HappeningContext;
            var timed = OnMapTimed.Instance;
            if (h?.map == null || timed == null) return null;
            var k = timed.kept.FirstOrDefault(x => x.kind == kind && x.map == h.map && (h.spot.IsValid && x.cell == h.spot || h.a != null && x.pawn == h.a));
            if (k != null) return k;
            k = Round2Map.Keep(kind, h, h.a, null, hours);
            k?.things.AddRange(h.things.Where(t => t != null && t.Spawned));
            return k;
        }

        /// <summary>The colony's biggest field.</summary>
        public static Zone_Growing Field(Map map, int atLeast = 9) =>
            map?.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count >= atLeast).OrderByDescending(z => z.Cells.Count).FirstOrDefault();

        /// <summary>Its middle cell.</summary>
        public static IntVec3 Middle(Zone z) => z == null || z.Cells.Count == 0 ? IntVec3.Invalid : z.Cells.OrderBy(c => c.DistanceToSquared(z.Cells[z.Cells.Count / 2])).First();

        /// <summary>The weather of the last days had rain (or snow) in it.</summary>
        public static bool Wet(Map map, bool snowToo)
        {
            var book = WeatherBook.Of(map);
            if (book == null) return false;
            return book.Fits(new List<string> { "rain:1/2" }) || snowToo && book.Fits(new List<string> { "snow:1/2" });
        }

        /// <summary>A plain message about the event (Round2Map.Told).</summary>
        public static void Told(Map map, string text, LookTargets look, MessageTypeDef type = null) => Round2Map.Told(map, text, look, type);
    }

    // ===================================================================================================
    // R4-E1. Crows on the new field
    // ===================================================================================================

    /// <summary>
    /// In spring, the colony's biggest field freshly sown: a flock of crows (Odyssey's) or else geese (the game's) comes in from
    /// the edge and walks about the field pulling up the seedlings. A is the one who saw them, B one who could keep watch.
    /// {TALE}: the field.
    /// </summary>
    public class HappeningWorker_R4ECrows : IncidentWorker_Happening
    {
        public static PawnKindDef Kind(Map map) => map == null ? null : Round3Map.Kind(map, "Crow", "Goose");

        /// <summary>The young plants of the field's crop (just sown and coming up).</summary>
        public static List<Plant> Seedlings(Map map, Zone_Growing z)
        {
            var list = new List<Plant>();
            if (map == null || z == null) return list;
            var want = z.GetPlantDefToGrow();
            foreach (var c in z.Cells)
                if (c.GetPlant(map) is Plant p && p.def == want && p.LifeStage == PlantLifeStage.Growing && p.Growth < Round4CampsEventsMath.YoungBelow)
                    list.Add(p);
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R4ECrows)) return false;
            var z = R4EMap.Field(map);
            return Round4CampsEventsMath.CrowsFit(GenLocalDate.Season(map) == Season.Spring, Seedlings(map, z).Count, Kind(map) != null) && StorytellerEvents.Adults(map).Count() >= 2;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = R4EMap.Field(map);
            var young = Seedlings(map, z);
            if (young.Count < Round4CampsEventsMath.SeedlingsFrom) return false;
            ctx.spot = R4EMap.Middle(z);
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = Round3Map.Another(map, ctx.spot, ctx.a) ?? adults.FirstOrDefault(p => p != ctx.a);
            if (ctx.b == null) return false;
            var kind = Kind(map);
            if (kind == null) return false;
            ctx.note = kind.defName;
            ctx.beast = kind.GetLabelPlural();
            ctx.tale = z.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ctx.note ?? "") ?? Kind(map);
            if (kind == null) return false;
            bool In(IntVec3 c) => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, ctx.spot, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors));
            if (!CellFinder.TryFindRandomEdgeCellNearWith(ctx.spot, 70f, map, In, out var edge) && !CellFinder.TryFindRandomEdgeCellWith(In, map, CellFinder.EdgeRoadChance_Animal, out edge))
                return false;
            int n = Rand.RangeInclusive(Round4CampsEventsMath.CrowsMin, Round4CampsEventsMath.CrowsMax);
            for (int i = 0; i < n; i++)
            {
                var x = Round3Map.Spawn(kind, CellFinder.RandomClosewalkCellNear(edge, map, 3), map);
                if (x == null) continue;
                ctx.things.Add(x);
                Round2Map.GoTo(x, Round2Map.Near(map, ctx.spot, 6f, x));
            }
            ctx.stranger = ctx.things.OfType<Pawn>().FirstOrDefault();
            if (ctx.stranger == null) return false;
            var k = Round2Map.Keep(TimedKind.R4ECrows, ctx, null, ctx.stranger);
            if (k == null) return false;
            k.things.AddRange(ctx.things);
            MapDangers.Say(k, ctx.a, "seen");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var t in ctx.things.OfType<Pawn>().Where(p => p.Spawned)) t.Destroy(DestroyMode.Vanish);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.note == "Goose" ? "geese" : "crows");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>watch</c>: B walks out and goes about the field until dusk, the crows near them run off the land, nothing is pulled up
    /// while B is there; <c>shoot</c>: the two nearest marked for the hunters, the rest leave once one falls; <c>let</c>: they peck
    /// about until dusk and then go.
    /// </summary>
    public class DilemmaEffect_R4ECrows : DilemmaEffect
    {
        public string mode = "let";

        public override bool ChanceEach => false;

        private static TimedThing Kept(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return null;
            var k = OnMapTimed.Instance?.kept.FirstOrDefault(x => x.kind == TimedKind.R4ECrows && x.map == h.map && (x.animal == h.stranger || h.stranger != null && x.things.Contains(h.stranger)));
            if (k != null || h.stranger == null || h.things.Count == 0) return k;
            k = Round2Map.Keep(TimedKind.R4ECrows, h, null, h.stranger);
            k?.things.AddRange(h.things.OfType<Pawn>().Where(p => p.Spawned && !p.Dead));
            return k;
        }

        private static List<Pawn> Here(DilemmaContext ctx)
        {
            var k = Kept(ctx);
            return k != null ? Round3Map.Animals(k) : new List<Pawn>();
        }

        private static List<Pawn> Marks(DilemmaContext ctx, IntVec3 from) =>
            Here(ctx).Where(x => x.Map.designationManager.DesignationOn(x) == null && !x.Downed).OrderBy(x => from.IsValid ? x.Position.DistanceToSquared(from) : 0).Take(Round4CampsEventsMath.CrowsShot).ToList();

        private static string Label(DilemmaContext ctx) => (ctx as HappeningContext)?.beast ?? "";

        public override string Unavailable(DilemmaContext ctx) => Here(ctx).Count == 0 ? "CC_R4E_Crows_Gone".Translate(Label(ctx)).Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || Here(ctx).Count == 0) return;
            string field = (ctx as HappeningContext)?.tale ?? "";
            switch (mode)
            {
                case "watch":
                    var b = ctx.b ?? ctx.a;
                    sheet.Play("CC_R4E_Impact_CrowsWatch".Translate(b?.LabelShort ?? "", field, Label(ctx)).Resolve(), "CC_R4E_Impact_CrowsWatchBrief".Translate(Label(ctx)).Resolve());
                    break;
                case "shoot":
                    sheet.Play("CC_R4E_Impact_CrowsShoot".Translate(Marks(ctx, (ctx as HappeningContext)?.spot ?? IntVec3.Invalid).Count, Label(ctx)).Resolve(), "CC_R4E_Impact_CrowsShootBrief".Translate(Label(ctx)).Resolve());
                    break;
                default:
                    sheet.Play("CC_R4E_Impact_CrowsLet".Translate(Label(ctx), Round4CampsEventsMath.PeckMost).Resolve(), "CC_R4E_Impact_CrowsLetBrief".Translate(Label(ctx)).Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Kept(ctx);
            if (k == null) return;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.pawns.Clear();
            k.on = false;
            k.until = now + GenDate.TicksPerDay;
            switch (mode)
            {
                case "watch":
                    var b = ctx.b != null && Round2Map.Free(ctx.b) ? ctx.b : Round3Map.Another(ctx.map, k.cell);
                    if (b != null) k.pawns.Add(b);
                    k.count = b != null ? 1 : 3;
                    break;
                case "shoot":
                    var marks = Marks(ctx, k.cell);
                    foreach (var x in marks)
                    {
                        try
                        {
                            x.Map.designationManager.AddDesignation(new Designation(x, DesignationDefOf.Hunt));
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("round 4: a crow marked", e);
                        }
                    }
                    if (marks.Count > 0) R4EMap.Told(ctx.map, "CC_R4E_Crows_Marked".Translate(marks.Count, Label(ctx)).Resolve(), new LookTargets(marks.Cast<Thing>()));
                    k.count = 2;
                    break;
                default:
                    k.count = 3;
                    break;
            }
        }
    }

    /// <summary>
    /// The crows on the field (TimedKind.R4ECrows): k.count 0 waiting for the answer, 1 B keeping watch, 2 two marked for the
    /// hunters, 3 let be. k.cell is the field's middle; k.done the ticks since they last pulled up a seedling, k.nights how many
    /// they pulled up. They keep to the field (walking about it) until they go.
    /// </summary>
    public static class R4ECrowsMap
    {
        private static bool Marked(Pawn x) => x.Map?.designationManager.DesignationOn(x) != null;

        private static Zone_Growing FieldOf(TimedThing k) => k.cell.IsValid && k.map != null ? k.cell.GetZone(k.map) as Zone_Growing ?? R4EMap.Field(k.map) : R4EMap.Field(k.map);

        private static void Go(IEnumerable<Pawn> crows, LocomotionUrgency urgency)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var x in crows.ToList())
            {
                Round2Map.Leave(x, urgency);
                if (x.mindState != null) x.mindState.exitMapAfterTick = now + GenDate.TicksPerHour;
            }
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            var crows = Round3Map.Animals(k);
            var watcher = k.count == 1 ? k.pawns.FirstOrDefault(p => Round2Map.Here(p, map) && !p.Drafted && !p.InMentalState) : null;
            if (crows.Count == 0)
            {
                // All chased off the land before evening: the one watching walks home.
                if (watcher != null && !k.on)
                {
                    Round2Map.GoTo(watcher, Round2Map.Home(map, watcher));
                    R4EMap.Told(map, "CC_R4E_Crows_Watched".Translate(k.animal?.kindDef?.GetLabelPlural() ?? "", k.nights).Resolve(), new LookTargets(watcher));
                }
                return true;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (k.count == 1 && watcher == null) k.count = 3;
            // Let be or watched, they go at dusk; marked, the rest go once one has fallen, or when the day is out.
            bool dusk = hour >= Round4CampsEventsMath.CrowsLeaveHour && Round3Map.HoursSince(k.start) >= 1f;
            if (k.count == 2 && k.things.OfType<Pawn>().Any(x => x.Dead) || k.count >= 1 && (dusk || now > k.until))
            {
                Go(crows.Where(x => !Marked(x)), LocomotionUrgency.Walk);
                if (watcher != null) Round2Map.GoTo(watcher, Round2Map.Home(map, watcher));
                string key = k.count == 2 ? "CC_R4E_Crows_Scared" : k.count == 1 ? "CC_R4E_Crows_Watched" : "CC_R4E_Crows_Left";
                R4EMap.Told(map, key.Translate(crows[0].kindDef.GetLabelPlural(), k.nights).Resolve(), new LookTargets(crows[0]));
                k.on = true;
                return true;
            }
            if (k.count == 0 && now > k.until) return true;
            var field = FieldOf(k);
            if (watcher != null)
            {
                Watch(k, watcher, crows);
                return false;
            }
            foreach (var x in crows)
            {
                if (Marked(x) || x.Downed || x.InMentalState || x.CurJobDef == JobDefOf.Goto || x.CurJobDef == JobDefOf.GotoWander) continue;
                if (!x.Position.InHorDistOf(k.cell, 12f)) Round2Map.GoTo(x, Round2Map.Near(map, k.cell, 8f, x));
                else if (Rand.Chance(0.4f)) Round2Map.Wander(x, k.cell, 8f);
            }
            // Every couple of hours they pull up a seedling or two.
            k.done += Sched.RareTicks;
            if (k.done >= R4EMap.Ticks(Round4CampsEventsMath.PeckEveryHours))
            {
                k.done = 0;
                Peck(k, field, crows);
            }
            return false;
        }

        /// <summary>One or two seedlings nearest a crow pulled up (the young plant gone, a puff of dust).</summary>
        private static void Peck(TimedThing k, Zone_Growing field, List<Pawn> crows)
        {
            var map = k.map;
            int n = Round4CampsEventsMath.Peck(k.nights, Rand.Value);
            if (n <= 0 || field == null) return;
            var young = HappeningWorker_R4ECrows.Seedlings(map, field);
            foreach (var x in crows.Where(c => !Marked(c)).InRandomOrder())
            {
                if (n <= 0) break;
                var p = young.Where(y => y.Spawned).OrderBy(y => y.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (p == null || !p.Position.InHorDistOf(x.Position, 6f)) continue;
                try
                {
                    FleckMaker.ThrowDustPuff(p.Position, map, 0.8f);
                    p.Destroy(DestroyMode.Vanish);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4: a seedling pulled up", e);
                    return;
                }
                young.Remove(p);
                k.nights++;
                n--;
            }
        }

        /// <summary>B goes about the field; the crows near B run off the land, the rest kept off the seedlings while B is there.</summary>
        private static void Watch(TimedThing k, Pawn watcher, List<Pawn> crows)
        {
            var map = k.map;
            var near = crows.Where(x => x.Position.InHorDistOf(watcher.Position, 7f)).ToList();
            if (near.Count > 0)
            {
                MapDangers.Say(k, watcher, "shoo");
                Go(near, LocomotionUrgency.Jog);
            }
            var next = crows.Except(near).Where(x => x.CurJob == null || !x.CurJob.exitMapOnArrival).OrderBy(x => x.Position.DistanceToSquared(watcher.Position)).FirstOrDefault();
            if (next != null && !(watcher.CurJobDef == JobDefOf.Goto && watcher.CurJob.targetA.Cell.InHorDistOf(next.Position, 4f)))
                Round2Map.GoTo(watcher, Round2Map.Near(map, next.Position, 3f, watcher));
            else if (next == null && watcher.CurJobDef != JobDefOf.Goto && watcher.CurJobDef != JobDefOf.GotoWander)
                Round2Map.Wander(watcher, k.cell, 8f);
        }

        public static void End(TimedThing k)
        {
            // Over before their time (the cap, a load): they don't stay on.
            Go(Round3Map.Animals(k).Where(x => !Marked(x)), LocomotionUrgency.Walk);
        }
    }

    // ===================================================================================================
    // R4-E2. Meltwater in the deep rooms
    // ===================================================================================================

    /// <summary>
    /// In spring, with the snow on the land melting, meltwater runs in under the outer door of a room dug under the mountain:
    /// puddles (the game's own) spread over its floor every few hours while it lasts. A saw it, B could see to it. {TALE}: the
    /// room.
    /// </summary>
    public class HappeningWorker_R4EMeltwater : IncidentWorker_Happening
    {
        /// <summary>A room under thick rock (every cell), a proper room, and one of its doors with the open air on its other side.</summary>
        public static bool Locate(Map map, out Room room, out Building_Door door, out IntVec3 inside)
        {
            room = null;
            door = null;
            inside = IntVec3.Invalid;
            foreach (var r in map.regionGrid.AllRooms)
            {
                if (r.PsychologicallyOutdoors || !r.ProperRoom || r.CellCount < 9 || r.CellCount > 400) continue;
                if (r.Cells.Any(c => !(map.roofGrid.RoofAt(c)?.isThickRoof ?? false))) continue;
                foreach (var d in r.ContainedAndAdjacentThings.OfType<Building_Door>())
                {
                    if (d.Faction != Faction.OfPlayer || !d.Spawned) continue;
                    IntVec3 inCell = IntVec3.Invalid;
                    bool outside = false;
                    foreach (var dir in GenAdj.CardinalDirections)
                    {
                        var c = d.Position + dir;
                        if (!c.InBounds(map)) continue;
                        var cr = c.GetRoom(map);
                        if (cr == r && c.Standable(map)) inCell = c;
                        else if (cr != null && cr.PsychologicallyOutdoors && !c.Roofed(map)) outside = true;
                    }
                    if (!outside || !inCell.IsValid) continue;
                    room = r;
                    door = d;
                    inside = inCell;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The room has a way in besides this door.</summary>
        public static bool OtherWay(Room r, Building_Door d) => r != null && r.ContainedAndAdjacentThings.OfType<Building_Door>().Any(x => x != d && x.Spawned);

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R4EMelt)) return false;
            bool snowy = CampWorker_R4KSnowCarvers.SnowAround(map) >= 0.08f;
            return Round4CampsEventsMath.MeltFits(GenLocalDate.Season(map) == Season.Spring, map.mapTemperature.OutdoorTemp, snowy) && Locate(map, out _, out _, out _);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            if (!Locate(map, out var room, out var door, out var inside)) return false;
            ctx.place = door;
            ctx.spot = inside;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(inside)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, null, ctx.a) ?? adults.FirstOrDefault(p => p != ctx.a) ?? ctx.a;
            ctx.tale = room.GetRoomRoleLabel();
            ctx.placeName = StorytellerEvents.Direction(map, inside);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.R4EMelt, ctx, ctx.a, null, Round4CampsEventsMath.MeltHours);
            if (k == null) return false;
            k.things.Add(ctx.place);
            R4EMeltMap.Water(k, 2);
            MapDangers.Say(k, ctx.a, "water");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>mop</c>: cleaned as it comes (the answer's work party, the game's own cleaning) while it keeps running in; <c>shut</c>:
    /// the door forbidden until it stops, and no more water comes in; <c>leave</c>: it keeps coming in until it stops.
    /// </summary>
    public class DilemmaEffect_R4EMelt : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        private static Building_Door Door(DilemmaContext ctx) => (ctx as HappeningContext)?.place as Building_Door;

        public override string Unavailable(DilemmaContext ctx)
        {
            var d = Door(ctx);
            if (d == null || !d.Spawned) return "CC_R4E_Melt_DoorGone".Translate().Resolve();
            if (mode == "shut" && !HappeningWorker_R4EMeltwater.OtherWay((ctx as HappeningContext)?.spot.GetRoom(ctx.map), d)) return "CC_R4E_Melt_OnlyDoor".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var d = Door(ctx);
            if (d == null || !d.Spawned) return;
            string room = (ctx as HappeningContext)?.tale ?? "";
            sheet.Play(("CC_R4E_Impact_Melt_" + mode).Translate(room).Resolve(), ("CC_R4E_Impact_Melt_" + mode + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = R4EMap.Of(TimedKind.R4EMelt, ctx, Round4CampsEventsMath.MeltHours);
            var d = Door(ctx);
            if (k == null || d == null) return;
            if (!k.things.Contains(d)) k.things.Add(d);
            k.count = mode == "shut" ? 2 : mode == "mop" ? 1 : 3;
            if (mode != "shut") return;
            try
            {
                d.SetForbidden(true, false);
                k.on = true;
                R4EMap.Told(ctx.map, "CC_R4E_Melt_Shut".Translate(d.LabelShort).Resolve(), new LookTargets(d));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: the door shut", e);
            }
        }
    }

    /// <summary>
    /// The meltwater (TimedKind.R4EMelt): k.count 0 waiting, 1 mopped, 2 the door kept shut (k.on: forbidden by the answer), 3
    /// left. Every few hours a puddle or two (the game's water) inside the door, unless the door is kept shut; it stops when its
    /// time is up or it freezes again.
    /// </summary>
    public static class R4EMeltMap
    {
        private static Building_Door Door(TimedThing k) => k.things.OfType<Building_Door>().FirstOrDefault(d => d.Spawned && d.Map == k.map);

        /// <summary>A puddle or two on the floor inside the door (at most so many at once).</summary>
        public static void Water(TimedThing k, int n)
        {
            var map = k.map;
            if (map == null || !k.cell.IsValid) return;
            var room = k.cell.GetRoom(map);
            var water = ThingDefOf.Filth_Water;
            if (room == null || water == null) return;
            var cells = GenRadial.RadialCellsAround(k.cell, Round4CampsEventsMath.MeltReach, true).Where(c => c.InBounds(map) && c.GetRoom(map) == room && c.Standable(map)).ToList();
            int lying = cells.Sum(c => c.GetThingList(map).Count(t => t.def == water));
            for (int i = 0; i < n && lying < Round4CampsEventsMath.MeltMost && cells.Count > 0; i++)
            {
                var c = cells.OrderBy(x => x.DistanceToSquared(k.cell) + Rand.Range(0, 6)).First();
                if (FilthMaker.TryMakeFilth(c, map, water)) lying++;
            }
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            var door = Door(k);
            if (door == null) return true;
            if (now > k.until || map.mapTemperature.OutdoorTemp < 0f)
            {
                R4EMap.Told(map, "CC_R4E_Melt_Stopped".Translate((k.cell.GetRoom(map)?.GetRoomRoleLabel() ?? "").Named("ROOM")).Resolve(), new LookTargets(door));
                return true;
            }
            // Shut, the water stays outside.
            if (k.count == 2 && door.IsForbidden(Faction.OfPlayer)) return false;
            k.done += Sched.RareTicks;
            if (k.done < R4EMap.Ticks(Round4CampsEventsMath.MeltEveryHours)) return false;
            k.done = 0;
            Water(k, Rand.RangeInclusive(1, 2));
            return false;
        }

        public static void End(TimedThing k)
        {
            var door = Door(k);
            if (k.count == 2 && k.on && door != null) door.SetForbidden(false, false);
        }
    }

    // ===================================================================================================
    // R4-E3. Midges at dusk
    // ===================================================================================================

    /// <summary>
    /// A warm summer afternoon by a field with wet ground near it (marsh, mud, shallow water): the midges come up at dusk. A,
    /// working there, says so. {TALE}: the field.
    /// </summary>
    public class HappeningWorker_R4EMidges : IncidentWorker_Happening
    {
        /// <summary>Wet ground: marsh and mud (round 3's), and shallow water.</summary>
        public static bool WetCell(IntVec3 c, Map map)
        {
            var t = c.GetTerrain(map);
            return t != null && (HappeningWorker_R3MarshPath.Wet(t) || t.IsWater && t.passability != Traversability.Impassable);
        }

        public static int WetNear(Map map, IntVec3 at) =>
            !at.IsValid ? 0 : GenRadial.RadialCellsAround(at, Round4CampsEventsMath.WetReach, true).Count(c => c.InBounds(map) && WetCell(c, map));

        /// <summary>The colony's biggest field (where the smudge fires go), and the wet ground near its middle.</summary>
        public static Zone_Growing Field(Map map, out int wet)
        {
            var z = R4EMap.Field(map);
            wet = z == null ? 0 : WetNear(map, R4EMap.Middle(z));
            return z;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R4EMidges)) return false;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 13 || hour > 16) return false;
            var s = GenLocalDate.Season(map);
            Field(map, out int wet);
            return Round4CampsEventsMath.MidgesFit(s == Season.Summer || s == Season.PermanentSummer, map.mapTemperature.OutdoorTemp, wet);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = Field(map, out int wet);
            if (z == null || wet < Round4CampsEventsMath.WetCellsFrom) return false;
            ctx.spot = R4EMap.Middle(z);
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = adults.FirstOrDefault(p => p != ctx.a) ?? ctx.a;
            ctx.tale = z.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.R4EMidges, ctx, ctx.a, null, 2f);
            if (k == null) return false;
            MapDangers.Say(k, ctx.a, "midges");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>smoke</c>: with the answer's smudge fires lit along the field, no bites while they burn; <c>in</c>: for two evenings
    /// those out by the wet ground walk in before dusk; <c>bear</c>: for two evenings those out there at dusk are bitten (a bad
    /// mood, and a small chance of insect bites through the shared budget).
    /// </summary>
    public class DilemmaEffect_R4EMidges : DilemmaEffect
    {
        public string mode = "bear";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            string field = (ctx as HappeningContext)?.tale ?? "";
            if (mode == "in") sheet.Play("CC_R4E_Impact_MidgesIn".Translate(field, Round4CampsEventsMath.Evenings).Resolve(), "CC_R4E_Impact_MidgesInBrief".Translate().Resolve());
            if (mode != "bear") return;
            sheet.Play("CC_R4E_Impact_MidgesBear".Translate(field, Round4CampsEventsMath.Evenings).Resolve(), "CC_R4E_Impact_MidgesBearBrief".Translate().Resolve());
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_R4E_Impact_MidgesRisk".Translate().Resolve(), "CC_R4E_Impact_MidgesRiskBrief".Translate().Resolve(), "r4ebites");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = R4EMap.Of(TimedKind.R4EMidges, ctx);
            if (k == null) return;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.count = mode == "smoke" ? 1 : mode == "in" ? 2 : 3;
            k.nights = 0;
            k.done = -1;
            k.pawns.Clear();
            // Two evenings from now: until the end of the second.
            k.until = now + GenDate.TicksPerDay * Round4CampsEventsMath.Evenings;
        }
    }

    /// <summary>
    /// The midges (TimedKind.R4EMidges): k.count 0 waiting, 1 smudge fires, 2 walked in before dusk, 3 put up with; k.done the
    /// day of the evening under way, k.nights the evenings had, k.pawns those bitten this evening. At dusk those out by the wet
    /// ground slap at them; with the smudge fires burning, or walked in, they aren't bitten.
    /// </summary>
    public static class R4EMidgesMap
    {
        private static bool Smoke(Map map) => OnMapTimed.Instance?.kept.Any(x => x.kind == TimedKind.Smudge && x.map == map) ?? false;

        /// <summary>Those of the colony out in the open by the wet ground now.</summary>
        private static List<Pawn> Out(TimedThing k, float reach)
        {
            var map = k.map;
            return map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.Dead && !p.Downed && !p.Position.Roofed(map) && p.Position.InHorDistOf(k.cell, reach)).ToList();
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            if (k.count == 0) return now > k.until;
            if (now > k.until || k.nights >= Round4CampsEventsMath.Evenings && !Round4CampsEventsMath.Dusk(GenLocalDate.HourOfDay(map))) return true;
            int hour = GenLocalDate.HourOfDay(map);
            float reach = Round4CampsEventsMath.WetReach + 8f;
            if (k.count == 2 && hour == Round4CampsEventsMath.InHour)
            {
                // In before dusk: those still out there walk home.
                foreach (var p in Out(k, reach).Where(p => Round2Map.Free(p) && !(p.CurJobDef == JobDefOf.Goto && !p.CurJob.targetA.Cell.InHorDistOf(k.cell, reach))))
                {
                    MapDangers.Say(k, p, "in");
                    Round2Map.GoTo(p, Round2Map.Home(map, p));
                }
                return false;
            }
            if (!Round4CampsEventsMath.Dusk(hour)) return false;
            if (k.done != GenDate.DaysPassed)
            {
                k.done = GenDate.DaysPassed;
                k.nights++;
                k.pawns.Clear();
            }
            if (k.count == 1 && Smoke(map)) return false;
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R4E_Midges");
            foreach (var p in Out(k, reach).Where(p => !k.pawns.Contains(p)))
            {
                k.pawns.Add(p);
                MapDangers.Say(k, p, "bite");
                if (thought != null) CouncilGameComponent.GiveThought(p, thought);
                Bite(p);
            }
            return false;
        }

        /// <summary>A small chance of insect bites (the land's ailments), through the shared budget, told at once.</summary>
        private static void Bite(Pawn p)
        {
            if (!Harm.Roll(p, HarmKinds.LandAilments, Round4CampsEventsMath.BiteChance, HurtClass.Mild)) return;
            var def = HarmFour.Def("Council_Ail_InsectBites");
            if (def == null || p.health.hediffSet.HasHediff(def)) return;
            var info = AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.LandAilments, HurtClass.Mild);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            Tell.Message(NewsKind.Health, Importance.Important, p.Map, "CC_R4E_Midges_Bitten".Translate(p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS")).Resolve(),
                new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }
    }

    // ===================================================================================================
    // R4-E4. What the storm threw up
    // ===================================================================================================

    /// <summary>
    /// After rain, on land by the sea: crabs and lobsters (Vanilla Fishing Expanded's) and seaweed (Vanilla Cooking Expanded -
    /// Sushi's) thrown up along the shore near the colony, else Odyssey's sea fish; held back from the haulers until the answer.
    /// A found it.
    /// </summary>
    public class HappeningWorker_R4EStormShore : IncidentWorker_Happening
    {
        public static readonly string[] Catch = { "VCEF_RawLobster", "Fish_Cod", "Fish_Flounder", "Fish_Bluefish" };
        public const string Weed = "VCE_RawNoriSeaweed";

        public static ThingDef CatchDef => Catch.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Item);

        private static bool Sea(TerrainDef t) => t != null && t.IsWater && t.defName.IndexOf("Ocean", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Open shore beside the sea near the colony, not under a roof, reachable; spread along it.</summary>
        public static List<IntVec3> Shore(Map map)
        {
            var list = new List<IntVec3>();
            var mid = StorytellerEvents.Center(map);
            if (!mid.IsValid) return list;
            foreach (var c in GenRadial.RadialCellsAround(mid, GenRadial.MaxRadialPatternRadius - 1f, true))
            {
                if (list.Count >= 40) break;
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetTerrain(map).IsWater || c.GetFirstItem(map) != null || c.GetEdifice(map) != null) continue;
                if (!GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && Sea(n.GetTerrain(map)))) continue;
                if (list.Any(x => x.InHorDistOf(c, 3f))) continue;
                list.Add(c);
            }
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R4EShore) || CatchDef == null) return false;
            return R4EMap.Wet(map, false) && Shore(map).Count >= Round4CampsEventsMath.HeapsMin && StorytellerEvents.Adults(map).Any(p => p.Awake());
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var shore = Shore(map);
            if (shore.Count < Round4CampsEventsMath.HeapsMin) return false;
            ctx.spot = shore[0];
            ctx.a = StorytellerEvents.Adults(map).Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = Round3Map.Another(map, ctx.spot, ctx.a) ?? ctx.a;
            var c = CatchDef;
            ctx.thing = c;
            ctx.beast = c?.label;
            ctx.note = DefDatabase<ThingDef>.GetNamedSilentFail(Weed) != null ? "weed" : null;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var shore = Shore(map);
            var food = CatchDef;
            var weed = ctx.note == "weed" ? DefDatabase<ThingDef>.GetNamedSilentFail(Weed) : null;
            if (food == null || shore.Count < Round4CampsEventsMath.HeapsMin) return false;
            int n = Math.Min(shore.Count, Rand.RangeInclusive(Round4CampsEventsMath.HeapsMin, Round4CampsEventsMath.HeapsMax));
            int i = 0;
            foreach (var c in shore.Take(n))
            {
                var def = weed != null && i % 3 == 2 ? weed : food;
                i++;
                try
                {
                    var t = ThingMaker.MakeThing(def);
                    t.stackCount = Mathf.Clamp(Rand.RangeInclusive(Round4CampsEventsMath.HeapFrom, Round4CampsEventsMath.HeapTo), 1, def.stackLimit);
                    var put = GenSpawn.Spawn(t, c, map);
                    put.SetForbidden(true, false);
                    ctx.things.Add(put);
                    FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.2f, 2f);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4: the storm's leavings", e);
                }
            }
            if (ctx.things.Count == 0) return false;
            ctx.count = ctx.things.Count;
            var k = Round2Map.Keep(TimedKind.R4EShore, ctx, ctx.a, null, Round4CampsEventsMath.SeaTakesMostHours);
            if (k == null) return false;
            k.things.AddRange(ctx.things);
            foreach (var t in ctx.things) k.cells.Add(t.Position);
            MapDangers.Say(k, ctx.a, "found");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var t in ctx.things.Where(t => t.Spawned)) t.Destroy(DestroyMode.Vanish);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "weed") tags.Add("weed");
            if (ctx.thing?.defName == "VCEF_RawLobster") tags.Add("lobster");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>all</c>: everything let go for the answer's work party to carry in; <c>near</c>: the nearer half let go, the rest goes
    /// back to the sea at night; <c>sea</c>: all of it goes back to the sea at night.
    /// </summary>
    public class DilemmaEffect_R4EShore : DilemmaEffect
    {
        public string mode = "sea";

        public override bool ChanceEach => false;

        private static List<Thing> Lying(DilemmaContext ctx) =>
            (ctx as HappeningContext)?.things.Where(t => t != null && t.Spawned && !t.Destroyed && t.Map == ctx.map).ToList() ?? new List<Thing>();

        private List<Thing> Nearest(DilemmaContext ctx)
        {
            var mid = StorytellerEvents.Center(ctx.map);
            var all = Lying(ctx);
            return all.OrderBy(t => t.Position.DistanceToSquared(mid)).Take(mode == "all" ? all.Count : Round4CampsEventsMath.Nearest(all.Count)).ToList();
        }

        public override string Unavailable(DilemmaContext ctx) => Lying(ctx).Count == 0 ? "CC_R4E_Shore_Gone".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var all = Lying(ctx);
            if (all.Count == 0) return;
            if (mode == "sea")
            {
                sheet.Play("CC_R4E_Impact_ShoreSea".Translate().Resolve(), "CC_R4E_Impact_ShoreSeaBrief".Translate().Resolve());
                return;
            }
            var take = Nearest(ctx);
            foreach (var g in take.GroupBy(t => t.def)) sheet.Goods(1, g.Key, g.Sum(t => t.stackCount));
            if (mode == "near") sheet.Play("CC_R4E_Impact_ShoreNear".Translate(take.Count, all.Count).Resolve(), "CC_R4E_Impact_ShoreNearBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = R4EMap.Of(TimedKind.R4EShore, ctx, Round4CampsEventsMath.SeaTakesMostHours);
            var map = ctx.map;
            if (k == null || map == null) return;
            if (mode != "sea")
                foreach (var t in Nearest(ctx)) t.SetForbidden(false, false);
            // The sea takes back what is still lying there tonight.
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            int toNight = ((Round4CampsEventsMath.SeaTakesHour - hour) + 24) % 24;
            if (toNight < 3) toNight += 24;
            k.until = now + Mathf.Min(R4EMap.Ticks(toNight), R4EMap.Ticks(Round4CampsEventsMath.SeaTakesMostHours));
            k.count = 1;
        }
    }

    /// <summary>What the storm threw up (TimedKind.R4EShore): at its time, what still lies on the shore where it was left goes back to the sea.</summary>
    public static class R4EShoreMap
    {
        public static bool Step(TimedThing k, int now)
        {
            if (now <= k.until) return false;
            var map = k.map;
            int gone = 0;
            foreach (var t in k.things.Where(t => t != null && t.Spawned && !t.Destroyed && t.Map == map && !t.IsInAnyStorage() && k.cells.Any(c => c.InHorDistOf(t.Position, 1.5f))).ToList())
            {
                try
                {
                    FleckMaker.WaterSplash(t.Position.ToVector3Shifted(), map, 1.4f, 2.5f);
                    t.Destroy(DestroyMode.Vanish);
                    gone++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4: the sea takes it back", e);
                    break;
                }
            }
            if (gone > 0) R4EMap.Told(map, "CC_R4E_Shore_Taken".Translate(gone).Resolve(), k.cells.Count > 0 ? new LookTargets(new TargetInfo(k.cells[0], map)) : LookTargets.Invalid);
            return true;
        }
    }

    // ===================================================================================================
    // R4-E5. Ice on the paths
    // ===================================================================================================

    /// <summary>
    /// In winter, freezing hard after rain or snow, with two doors or more to the outside: the ground outside them is icy. A
    /// nearly fell outside a door; B could scatter ash. {COUNT}: the doors.
    /// </summary>
    public class HappeningWorker_R4EIcyPaths : IncidentWorker_Happening
    {
        /// <summary>The colony's doors to the outside, each with the open cell outside it, nearest the colony's middle first.</summary>
        public static List<(Building_Door door, IntVec3 outside)> Doors(Map map)
        {
            var list = new List<(Building_Door, IntVec3)>();
            var mid = StorytellerEvents.Center(map);
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Door>())
            {
                if (!b.Spawned) continue;
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var c = b.Position + dir;
                    if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map)) continue;
                    var r = c.GetRoom(map);
                    if (r == null || !r.PsychologicallyOutdoors) continue;
                    list.Add((b, c));
                    break;
                }
            }
            return list.OrderBy(d => mid.IsValid ? d.Item2.DistanceToSquared(mid) : 0).Take(10).ToList();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R4EIce)) return false;
            var s = GenLocalDate.Season(map);
            return Round4CampsEventsMath.IceFits(s == Season.Winter || s == Season.PermanentWinter, map.mapTemperature.OutdoorTemp, R4EMap.Wet(map, true), Doors(map).Count)
                   && StorytellerEvents.Adults(map).Count() >= 2;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var doors = Doors(map);
            if (doors.Count < 2) return false;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).OrderBy(p => doors.Min(d => p.Position.DistanceToSquared(d.outside))).FirstOrDefault();
            if (ctx.a == null) return false;
            var at = doors.OrderBy(d => d.outside.DistanceToSquared(ctx.a.Position)).First();
            ctx.place = at.door;
            ctx.spot = at.outside;
            ctx.b = Round3Map.Another(map, ctx.spot, ctx.a) ?? adults.FirstOrDefault(p => p != ctx.a) ?? ctx.a;
            ctx.count = doors.Count;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.R4EIce, ctx, ctx.a, null, Round4CampsEventsMath.IceMostHours);
            if (k == null) return false;
            foreach (var d in Doors(ctx.map)) k.cells.Add(d.outside);
            MapDangers.Say(k, ctx.a, "slip");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>paths</c>: B walks from door to door scattering ash, and while the ice lasts nobody slips; <c>doors</c>: B does the two
    /// doors nearest the middle of the colony, and away from them the risk stays; <c>leave</c>: until it thaws, anyone walking
    /// out on the colony's ground may slip (a sprained ankle, through the shared budget).
    /// </summary>
    public class DilemmaEffect_R4EIce : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var b = ctx.b ?? ctx.a;
            switch (mode)
            {
                case "paths":
                    sheet.Play("CC_R4E_Impact_IcePaths".Translate(b?.LabelShort ?? "", ctx.count).Resolve(), "CC_R4E_Impact_IcePathsBrief".Translate().Resolve());
                    break;
                case "doors":
                    sheet.Play("CC_R4E_Impact_IceDoors".Translate(b?.LabelShort ?? "", Round4CampsEventsMath.BusyDoors).Resolve(), "CC_R4E_Impact_IceDoorsBrief".Translate().Resolve());
                    if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_R4E_Impact_IceRiskSome".Translate().Resolve(), "CC_R4E_Impact_IceRiskBrief".Translate().Resolve(), "r4eslip");
                    break;
                default:
                    if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_R4E_Impact_IceRisk".Translate().Resolve(), "CC_R4E_Impact_IceRiskBrief".Translate().Resolve(), "r4eslip");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = R4EMap.Of(TimedKind.R4EIce, ctx, Round4CampsEventsMath.IceMostHours);
            if (k == null) return;
            if (k.cells.Count == 0) foreach (var d in HappeningWorker_R4EIcyPaths.Doors(ctx.map)) k.cells.Add(d.outside);
            k.pawns.Clear();
            k.done = 0;
            k.on = false;
            if (mode == "leave")
            {
                k.count = 3;
                return;
            }
            // The paths: every door's ground; the doors: the two nearest the middle (the first of the list).
            if (mode == "doors" && k.cells.Count > Round4CampsEventsMath.BusyDoors) k.cells.RemoveRange(Round4CampsEventsMath.BusyDoors, k.cells.Count - Round4CampsEventsMath.BusyDoors);
            var b = ctx.b != null && Round2Map.Free(ctx.b) ? ctx.b : Round3Map.Another(ctx.map, k.cells.FirstOrDefault());
            if (b == null)
            {
                k.count = 3;
                return;
            }
            k.pawns.Add(b);
            k.count = mode == "doors" ? 2 : 1;
        }
    }

    /// <summary>
    /// The ice (TimedKind.R4EIce): k.count 0 waiting, 1 the paths gritted, 2 the busiest doors gritted, 3 left; k.cells the
    /// ground outside the doors (the ones to grit), k.done how many of them are done, k.on once all are, k.nights the slips had.
    /// It goes when it thaws or its time is up.
    /// </summary>
    public static class R4EIceMap
    {
        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            if (Round4CampsEventsMath.IceGone(map.mapTemperature.OutdoorTemp, Round3Map.HoursSince(k.start)) || now > k.until)
            {
                if (k.count >= 1) R4EMap.Told(map, "CC_R4E_Ice_Thawed".Translate().Resolve(), LookTargets.Invalid);
                return true;
            }
            if (k.count == 0) return false;
            if ((k.count == 1 || k.count == 2) && !k.on) Grit(k);
            if (k.count == 1 && k.on) return false;
            Slips(k);
            return false;
        }

        /// <summary>B walks from door to door scattering ash: a puff at each, the next once they're there.</summary>
        private static void Grit(TimedThing k)
        {
            var map = k.map;
            var b = k.pawns.FirstOrDefault();
            if (!Round2Map.Here(b, map) || b.Drafted || b.InMentalState)
            {
                // Nobody to do it: left as it is.
                k.count = 3;
                return;
            }
            if (k.done >= k.cells.Count)
            {
                k.on = true;
                Round2Map.Give(b, "Rimfolk_R4E_Gritted");
                R4EMap.Told(map, (k.count == 1 ? "CC_R4E_Ice_Gritted" : "CC_R4E_Ice_GrittedDoors").Translate(b.LabelShort.Named("PAWN"), k.cells.Count).Resolve(), new LookTargets(b));
                return;
            }
            var at = k.cells[k.done];
            if (b.Position.InHorDistOf(at, 1.5f))
            {
                foreach (var c in GenRadial.RadialCellsAround(at, 1.5f, true).Where(c => c.InBounds(map))) FleckMaker.ThrowDustPuff(c, map, 0.6f);
                if (k.done == 0) MapDangers.Say(k, b, "ash");
                k.done++;
                return;
            }
            if (!(b.CurJobDef == JobDefOf.Goto && b.CurJob.targetA.Cell == at)) Round2Map.GoTo(b, at);
        }

        /// <summary>Anyone walking out on the colony's ground away from the gritted ground may slip, at most twice in a spell.</summary>
        private static void Slips(TimedThing k)
        {
            if (k.nights >= 2 || !MapDangers.On) return;
            var map = k.map;
            var safe = k.count == 2 && k.on ? k.cells : null;
            float chance = Round4CampsEventsMath.SlipChancePerHour * Sched.RareTicks / GenDate.TicksPerHour;
            var def = HarmFour.Def("Council_Ail_SprainedAnkle");
            if (def == null) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && !p.Downed && p.pather != null && p.pather.Moving && !p.Position.Roofed(map) && map.areaManager.Home[p.Position]))
            {
                if (safe != null && safe.Any(c => c.InHorDistOf(p.Position, 3f))) continue;
                if (p.health.hediffSet.HasHediff(def) || !Harm.Roll(p, HarmKinds.Dangers, chance, HurtClass.Mild)) continue;
                var info = AilmentTracker.InfoOf(def);
                var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.Dangers, HurtClass.Mild);
                if (h == null) continue;
                if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
                k.nights++;
                MapDangers.Say(k, p, "fell");
                Tell.Message(NewsKind.Health, Importance.Important, map, "CC_R4E_Ice_Slipped".Translate(p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
                if (k.nights >= 2) return;
            }
        }
    }

    // ===================================================================================================
    // R4-E6. The old tree by the house
    // ===================================================================================================

    /// <summary>
    /// The colony's wood runs short, and a big grown tree stands close to its buildings, outside them; A likes to sit under it
    /// (and walks out to it as the card comes: the event's own visit). B is the one who would fell it. {BEAST}: the tree.
    /// </summary>
    public class HappeningWorker_R4EOldTree : IncidentWorker_Happening
    {
        /// <summary>The big grown trees near the colony's buildings, on its home ground, out in the open.</summary>
        public static Plant Tree(Map map)
        {
            var mid = StorytellerEvents.Center(map);
            if (!mid.IsValid) return null;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(p => p.Spawned && p.def.plant != null && p.def.plant.IsTree && p.LifeStage == PlantLifeStage.Mature && p.def.plant.harvestYield >= Round4CampsEventsMath.TreeYieldFrom
                            && p.Position.InHorDistOf(mid, 30f) && map.areaManager.Home[p.Position] && !p.Position.Roofed(map) && map.designationManager.DesignationOn(p) == null
                            && GenRadial.RadialDistinctThingsAround(p.Position, map, Round4CampsEventsMath.TreeNear, true).Any(t => t is Building b && b.Faction == Faction.OfPlayer && b.def.building != null && !b.def.building.isNaturalRock))
                .OrderBy(p => p.Position.DistanceToSquared(mid)).FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Round4CampsEventsMath.WoodShort(map.resourceCounter.GetCount(ThingDefOf.WoodLog), Colonists(map).Count) && StorytellerEvents.Adults(map).Count() >= 2
            && Tree(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var tree = Tree(map);
            if (tree == null) return false;
            ctx.place = tree;
            ctx.spot = tree.Position;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => Round2Map.Free(p)).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Plants, WorkTypeDefOf.PlantCutting, ctx.a) ?? adults.FirstOrDefault(p => p != ctx.a);
            if (ctx.b == null) return false;
            ctx.beast = tree.def.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // A says so; the event's own visit takes them out to stand under it.
            Round3Map.Say(ctx.incident?.defName, ctx.a, "tree");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>fell</c>: the tree marked for cutting (the answer's work party cuts it, the logs the colony's), A sad about it;
    /// <c>seat</c>: a stool of wood marked beside the trunk for the builders, A glad; <c>leave</c>: it stands, A a little glad.
    /// </summary>
    public class DilemmaEffect_R4EOldTree : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        private static Plant Tree(DilemmaContext ctx) => (ctx as HappeningContext)?.place is Plant p && p.Spawned && !p.Destroyed ? p : null;

        public static ThingDef Seat => DefDatabase<ThingDef>.GetNamedSilentFail("Stool");

        /// <summary>A cell beside the trunk the stool can be marked on.</summary>
        public static IntVec3 SeatCell(Map map, Plant tree)
        {
            var seat = Seat;
            if (seat == null || tree == null) return IntVec3.Invalid;
            var stuff = Round3Map.Stuff(map, seat);
            foreach (var c in GenAdj.CellsAdjacent8Way(tree).OrderBy(c => c.DistanceToSquared(StorytellerEvents.Center(map))))
                if (c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetZone(map) == null
                    && GenConstruct.CanPlaceBlueprintAt(seat, c, Rot4.South, map, false, null, null, stuff).Accepted)
                    return c;
            return IntVec3.Invalid;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var t = Tree(ctx);
            if (t == null) return "CC_R4E_Tree_Gone".Translate().Resolve();
            return mode == "seat" && !SeatCell(ctx.map, t).IsValid ? "CC_R4E_Tree_NoRoom".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var t = Tree(ctx);
            if (t == null) return;
            switch (mode)
            {
                case "fell":
                    sheet.Play("CC_R4E_Impact_TreeFell".Translate(t.LabelShort).Resolve(), "CC_R4E_Impact_TreeFellBrief".Translate().Resolve());
                    var yield = Mathf.RoundToInt(t.def.plant.harvestYield);
                    if (t.def.plant.harvestedThingDef != null && yield > 0) sheet.Goods(1, t.def.plant.harvestedThingDef, yield);
                    break;
                case "seat":
                    sheet.Play("CC_R4E_Impact_TreeSeat".Translate(t.LabelShort, Seat?.label ?? "").Resolve(), "CC_R4E_Impact_TreeSeatBrief".Translate().Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var t = Tree(ctx);
            var map = ctx.map;
            if (t == null || map == null) return;
            try
            {
                switch (mode)
                {
                    case "fell":
                        map.designationManager.RemoveAllDesignationsOn(t);
                        map.designationManager.AddDesignation(new Designation(t, DesignationDefOf.CutPlant));
                        R4EMap.Told(map, "CC_R4E_Tree_Marked".Translate(t.LabelShort).Resolve(), new LookTargets(t));
                        Round2Map.Give(ctx.a, "Rimfolk_R4E_TreeFelled");
                        break;
                    case "seat":
                        var c = SeatCell(map, t);
                        if (!c.IsValid) return;
                        var seat = Seat;
                        GenConstruct.PlaceBlueprintForBuild(seat, c, map, Rot4.South, Faction.OfPlayer, Round3Map.Stuff(map, seat));
                        R4EMap.Told(map, "CC_R4E_Tree_SeatMarked".Translate(seat.label, t.LabelShort).Resolve(), new LookTargets(new TargetInfo(c, map)));
                        Round2Map.Give(ctx.a, "Rimfolk_R4E_TreeKept");
                        break;
                    default:
                        Round2Map.Give(ctx.a, "Rimfolk_R4E_TreeStands");
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: the old tree", e);
            }
        }
    }

    // ===================================================================================================
    // R4-E7. Stones in the field
    // ===================================================================================================

    /// <summary>
    /// In spring, on land where the winter freezes: the frost has pushed stones up through the colony's biggest field, chunks
    /// of the land's own rock lying on its open ground. A, sowing, found them. {TALE}: the field, {THING}: the stone.
    /// </summary>
    public class HappeningWorker_R4EFieldStones : IncidentWorker_Happening
    {
        /// <summary>The coldest month of the year at the colony.</summary>
        public static float Coldest(Map map)
        {
            float lo = map.mapTemperature.OutdoorTemp;
            try
            {
                foreach (Twelfth tw in Enum.GetValues(typeof(Twelfth)))
                {
                    if (tw == Twelfth.Undefined) continue;
                    lo = Math.Min(lo, GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, tw));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: the year's cold", e);
            }
            return lo;
        }

        /// <summary>The land's own rock, as chunks lying in a field.</summary>
        public static ThingDef Chunk(Map map)
        {
            try
            {
                foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                {
                    var chunk = rock?.building?.mineableThing;
                    if (chunk != null && chunk.category == ThingCategory.Item) return chunk;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: the land's rock", e);
            }
            return DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
        }

        /// <summary>The field's open ground: nothing growing there yet (or just come up), nothing lying or built there.</summary>
        public static List<IntVec3> Open(Map map, Zone_Growing z) =>
            z == null ? new List<IntVec3>() : z.Cells.Where(c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null && c.GetFirstPawn(map) == null
                                                                    && (!(c.GetPlant(map) is Plant p) || p.Growth < 0.15f)).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Chunk(map) == null) return false;
            var z = R4EMap.Field(map, Round4CampsEventsMath.FieldFrom);
            return z != null && Round4CampsEventsMath.StonesFit(GenLocalDate.Season(map) == Season.Spring, Coldest(map), z.Cells.Count, Open(map, z).Count);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = R4EMap.Field(map, Round4CampsEventsMath.FieldFrom);
            if (z == null) return false;
            ctx.spot = R4EMap.Middle(z);
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = VarietyThree.Best(adults, SkillDefOf.Plants, WorkTypeDefOf.Growing) ?? adults.FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = adults.FirstOrDefault(p => p != ctx.a) ?? ctx.a;
            ctx.thing = Chunk(map);
            ctx.tale = z.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = R4EMap.Field(map, Round4CampsEventsMath.FieldFrom);
            var chunk = ctx.thing ?? Chunk(map);
            var open = Open(map, z);
            if (chunk == null || open.Count < Round4CampsEventsMath.StonesMax) return false;
            int n = Rand.RangeInclusive(Round4CampsEventsMath.StonesMin, Round4CampsEventsMath.StonesMax);
            foreach (var c in open.InRandomOrder())
            {
                if (ctx.things.Count >= n) break;
                if (ctx.things.Any(t => t.Position.InHorDistOf(c, 2.5f))) continue;
                try
                {
                    if (c.GetPlant(map) is Plant p) p.Destroy(DestroyMode.Vanish);
                    var t = GenSpawn.Spawn(ThingMaker.MakeThing(chunk), c, map);
                    FleckMaker.ThrowDustPuff(c, map, 1f);
                    ctx.things.Add(t);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 4: a stone pushed up", e);
                    break;
                }
            }
            if (ctx.things.Count == 0) return false;
            ctx.count = ctx.things.Count;
            if (Round2Map.Free(ctx.a)) Round2Map.GoTo(ctx.a, Round2Map.Near(map, ctx.things[0].Position, 2f, ctx.a));
            Round3Map.Say(ctx.incident?.defName, ctx.a, "stones");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var t in ctx.things.Where(t => t.Spawned)) t.Destroy(DestroyMode.Vanish);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>carry</c>: the stones let go for the answer's work party to carry off (the game's own hauling); <c>blocks</c>: a bill at
    /// the colony's stonecutter's table for blocks of that stone, as many as the stones; <c>leave</c>: they stay where they lie.
    /// </summary>
    public class DilemmaEffect_R4EStones : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        private static List<Thing> Lying(DilemmaContext ctx) => (ctx as HappeningContext)?.things.Where(t => t != null && t.Spawned && !t.Destroyed).ToList() ?? new List<Thing>();

        /// <summary>The colony's bench that cuts this stone into blocks, and its recipe.</summary>
        public static RecipeDef Recipe(Map map, ThingDef chunk, out Building_WorkTable bench)
        {
            bench = null;
            if (map == null || chunk == null) return null;
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                if (!b.Spawned || b.def.AllRecipes == null) continue;
                var r = b.def.AllRecipes.FirstOrDefault(x => x.ProducedThingDef != null && x.ProducedThingDef.IsStuff && x.ProducedThingDef.stuffProps?.categories?.Contains(StuffCategoryDefOf.Stony) == true
                                                               && x.ingredients.Any(i => i.filter.Allows(chunk)) && x.AvailableNow);
                if (r == null) continue;
                bench = b;
                return r;
            }
            return null;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var lying = Lying(ctx);
            if (lying.Count == 0) return "CC_R4E_Stones_Gone".Translate().Resolve();
            return mode == "blocks" && Recipe(ctx.map, lying[0].def, out _) == null ? "CC_R4E_Stones_NoBench".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var lying = Lying(ctx);
            if (lying.Count == 0) return;
            if (mode == "carry") sheet.Play("CC_R4E_Impact_StonesCarry".Translate(lying.Count).Resolve(), "CC_R4E_Impact_StonesCarryBrief".Translate().Resolve());
            if (mode != "blocks") return;
            var r = Recipe(ctx.map, lying[0].def, out var bench);
            if (r == null) return;
            sheet.Play("CC_R4E_Impact_StonesBlocks".Translate(lying.Count, r.ProducedThingDef.label, bench.LabelShort).Resolve(), "CC_R4E_Impact_StonesBlocksBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var lying = Lying(ctx);
            var map = ctx.map;
            if (lying.Count == 0 || map == null) return;
            foreach (var t in lying) t.SetForbidden(false, false);
            if (mode != "blocks") return;
            try
            {
                var r = Recipe(map, lying[0].def, out var bench);
                if (r == null || !(r.MakeNewBill() is Bill_Production bill)) return;
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = lying.Count;
                VarietyThree.First(bench, bill);
                R4EMap.Told(map, "CC_R4E_Stones_Bill".Translate(bill.repeatCount, r.ProducedThingDef.label, bench.LabelShort).Resolve(), new LookTargets(bench));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: the stones' bill", e);
            }
        }
    }

    // ===================================================================================================
    // R4-E8. Washing day
    // ===================================================================================================

    /// <summary>A warm, dry morning with shallow water near the colony: A offers a washing day. The answers are the play-outs' own (a gathering at the water, a visit to it).</summary>
    public class HappeningWorker_R4EWashingDay : IncidentWorker_Happening
    {
        public static IntVec3 Water(Map map) => HappeningWorker_WaterForFields.Water(map, StorytellerEvents.Center(map));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Round4CampsEventsMath.WashingFits(map.mapTemperature.OutdoorTemp, (map.weatherManager?.curWeather?.rainRate ?? 0f) > 0.05f, GenLocalDate.HourOfDay(map))
            && StorytellerEvents.Adults(map).Count() >= 2 && Water(map).IsValid;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var w = Water(map);
            if (!w.IsValid) return false;
            ctx.spot = w;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.b = adults.Where(p => p != ctx.a).RandomElementWithFallback() ?? ctx.a;
            ctx.placeName = StorytellerEvents.Direction(map, w);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            Round3Map.Say(ctx.incident?.defName, ctx.a, "wash");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }
}
