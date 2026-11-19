using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 27. Fouled water (the river folk) =====================

    /// <summary>One fouled spot by the water: a body lying in it, poisoned ground beside it (Biotech), a wastepack by it.</summary>
    public struct FoulSpot
    {
        public IntVec3 cell;
        public string kind;
        public Thing thing;
    }

    /// <summary>
    /// Fouled water (item 27): when the colony fouls water (a body rotting in a river or lake, the land poisoned beside it or
    /// a wastepack by it, Biotech's), a river warden walks to the spot and asks for it to be cleaned. Cleaned with the colony's
    /// hands (the warden and a colonist at work, a bar at each spot): +5, and their catches come again. The warden alone: +2.
    /// Left: "never foul the water another drinks", a wrong in their memory, and the boat-trader and the nets stop coming
    /// until it is clean (or a season and more has passed). Bodies in any age; poison and wastepacks from the industrial age.
    /// </summary>
    public static class FouledWater
    {
        public const string CleanKind = "pev:clean";

        private static int Now => Find.TickManager.TicksGame;

        private static bool Water(IntVec3 c, Map map) => c.InBounds(map) && (c.GetTerrain(map)?.IsWater ?? false);

        /// <summary>Water this close to the cell (a wastepack or poisoned ground by it).</summary>
        public static bool ByWater(IntVec3 c, Map map) => GenRadial.RadialCellsAround(c, PeopleEvents4Math.FoulReach, true).Any(x => Water(x, map));

        /// <summary>Everything on the map fouling water now, the nearest the colony first, so many at most.</summary>
        public static List<FoulSpot> Spots(Map map, IntVec3 near = default, float within = 0f)
        {
            var list = new List<FoulSpot>();
            if (map == null) return list;
            bool In(IntVec3 c) => within <= 0f || c.InHorDistOf(near, within);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(t is Corpse c) || !c.Spawned || c.Position.Fogged(map) || !Water(c.Position, map) || !In(c.Position)) continue;
                if (c.InnerPawn?.RaceProps?.IsFlesh != true || c.Age < PeopleEvents4Math.CorpseDays * GenDate.TicksPerDay) continue;
                list.Add(new FoulSpot { cell = c.Position, kind = "corpse", thing = c });
            }
            var waste = DefDatabase<ThingDef>.GetNamedSilentFail("Wastepack");
            if (waste != null)
                foreach (var t in map.listerThings.ThingsOfDef(waste))
                    if (t.Spawned && !t.Position.Fogged(map) && In(t.Position) && ByWater(t.Position, map))
                        list.Add(new FoulSpot { cell = t.Position, kind = "wastepack", thing = t });
            if (ModsConfig.BiotechActive && map.pollutionGrid != null && map.pollutionGrid.TotalPollution > 0)
            {
                // Poisoned ground on the bank: one spot for each stretch of it.
                var taken = new List<IntVec3>();
                foreach (var c in map.AllCells)
                {
                    if ((c.x + c.z) % 2 != 0 || !In(c) || !map.pollutionGrid.IsPolluted(c) || Water(c, map) || c.Fogged(map) || !ByWater(c, map)) continue;
                    if (taken.Any(x => x.InHorDistOf(c, 6f))) continue;
                    taken.Add(c);
                    list.Add(new FoulSpot { cell = c, kind = "pollution" });
                    if (taken.Count >= PeopleEvents4Math.MaxSpots) break;
                }
            }
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            return list.OrderBy(s => s.cell.DistanceToSquared(centre)).Take(PeopleEvents4Math.MaxSpots).ToList();
        }

        /// <summary>The boat-trader and the nets stop coming: fouled water of the colony's left, and not yet put right.</summary>
        public static bool CatchesStopped(Map map) => PeopleEvents4Book.Instance?.fouled.Any(r => r != null && !r.done && (map == null || r.map == map)) ?? false;

        /// <summary>The day's look at what was left: put right (nothing fouling there now) or past its time, the catches come again.</summary>
        public static void Tidy()
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null) return;
            int now = Now;
            foreach (var r in b.fouled.Where(r => r != null && !r.done))
            {
                if (r.map == null || !Find.Maps.Contains(r.map) || r.faction == null || r.faction.defeated)
                {
                    r.done = true;
                    continue;
                }
                var spots = Spots(r.map);
                bool clean = !r.cells.Any(c => spots.Any(s => s.cell.InHorDistOf(c, 6f)));
                if (!clean && now < r.until) continue;
                r.done = true;
                if (clean)
                    Tell.Message(NewsKind.World, Importance.Notable, r.map, "CC_PEv4_WaterClean".Translate(r.faction.Name.Named("FACTION")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>The cleaning set going: the warden (and, <paramref name="together"/>, the colonist) at the spots near where the warden stands.</summary>
        public static Visit Begin(HappeningContext ctx, bool together)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            if (t == null || x == null || ctx.map == null) return null;
            var v = t.AddVisit(CleanKind, ctx.incident, ctx.map, x, together ? ctx.a : null, ctx.group, Now + GenDate.TicksPerDay);
            v.cell = ctx.spot;
            v.text = (ctx.faction?.GetUniqueLoadID() ?? "") + "|" + (together ? "together" : "alone");
            Lords.Leave(x);
            Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, ctx.spot, GenDate.TicksPerDay), ctx.map, new List<Pawn> { x });
            return v;
        }

        private static bool Busy(Pawn p) => p?.CurJobDef != null && p.CurJobDef == PeopleEvents4.CleanJob;

        /// <summary>The cleaning's hour: anyone free takes the next spot; when none is left (or the day is up), the river folk's thanks.</summary>
        public static bool CleanHour(Visit v, int now)
        {
            if (v.done) return false;
            // The colony's helper only while awake and not eating or in bed; the warden whenever free.
            var workers = new[] { v.x, v.a }.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map && !p.Drafted && !p.InMentalState
                                                       && (!p.IsColonist || (p.Awake() && !p.InBed() && p.CurJobDef != JobDefOf.Ingest))).ToList();
            var spots = Spots(v.map, v.cell, 20f);
            var taken = new HashSet<IntVec3>(workers.Where(Busy).Select(p => p.CurJob.targetA.HasThing ? p.CurJob.targetA.Thing.Position : p.CurJob.targetA.Cell));
            foreach (var w in workers.Where(p => !Busy(p)))
            {
                var s = spots.Where(x => !taken.Contains(x.cell)).OrderBy(x => x.cell.DistanceToSquared(w.Position)).FirstOrDefault();
                if (!s.cell.IsValid || s.kind == null) break;
                LocalTargetInfo target = s.thing != null ? new LocalTargetInfo(s.thing) : new LocalTargetInfo(s.cell);
                if (PeopleEvents4.Send(w, PeopleEvents4.CleanJob, target, PeopleEvents4Math.CleanTicks)) taken.Add(s.cell);
            }
            if ((spots.Count == 0 && !workers.Any(Busy)) || now > v.until || v.x == null || v.x.Dead || !v.x.Spawned) Finish(v, spots.Count == 0);
            return !v.done;
        }

        /// <summary>A spot cleaned (the job done): the body or the wastepack carried up onto dry ground away from the water, the poisoned bank cleared.</summary>
        public static void Cleaned(Pawn pawn, LocalTargetInfo target)
        {
            var map = pawn?.Map;
            if (map == null) return;
            if (target.Thing is Thing t && t.Spawned)
            {
                var from = t.Position;
                if (!CellFinder.TryFindRandomCellNear(from, map, 14, c => c.Standable(map) && !Water(c, map) && !ByWater(c, map) && c.GetFirstItem(map) == null, out var dry)) return;
                t.DeSpawn();
                GenPlace.TryPlaceThing(t, dry, map, ThingPlaceMode.Near);
                FleckMaker.WaterSplash(from.ToVector3Shifted(), map, 1.5f, 0.5f);
                return;
            }
            if (!ModsConfig.BiotechActive || map.pollutionGrid == null) return;
            foreach (var c in GenRadial.RadialCellsAround(target.Cell, 2.9f, true))
                if (c.InBounds(map) && map.pollutionGrid.IsPolluted(c))
                    map.pollutionGrid.SetPolluted(c, false);
        }

        private static void Finish(Visit v, bool clean)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            bool together = (v.text ?? "").EndsWith("together");
            if (clean && f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer))
            {
                WorldSituationWorker.Goodwill(f, together ? PeopleEvents4Math.CleanGoodwill : PeopleEvents4Math.AloneGoodwill, GoodwillWhy.Helped);
                if (together) FactionMemory.Note(f, "favorDone", "CC_PEv4_WaterName".Translate().Resolve());
                foreach (var r in PeopleEvents4Book.Instance?.fouled.Where(r => r != null && !r.done && r.map == v.map) ?? Enumerable.Empty<FoulRecord>()) r.done = true;
                if (v.x != null && v.x.Spawned) PeoplesG.SayLine(v.x, v.incident, "clean");
                Tell.Message(NewsKind.World, Importance.Notable, v.map, (together ? "CC_PEv4_WaterCleaned" : "CC_PEv4_WaterCleanedAlone").Translate(f.Name.Named("FACTION"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                    v.x != null && v.x.Spawned ? new LookTargets(v.x) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(new[] { v.x });
        }

        /// <summary>Left: a wrong in their memory, the catches stopped until it is clean.</summary>
        public static void Left(HappeningContext ctx)
        {
            var f = ctx?.faction;
            var b = PeopleEvents4Book.Instance;
            if (f == null || b == null) return;
            FactionMemory.Note(f, "polluted", "CC_PEv4_WaterName".Translate().Resolve(), PeopleEvents4Math.LeftWeight);
            b.AddFoul(new FoulRecord
            {
                faction = f, map = ctx.map, cells = Spots(ctx.map).Select(s => s.cell).ToList(), since = Now, until = Now + PeopleEvents4Math.StopDays * GenDate.TicksPerDay
            });
        }
    }

    /// <summary>A river warden of {FACTION} ({X}) at the fouled water ({TALE}; {COUNT} spots), asking for it to be cleaned; {A} the colonist who would help.</summary>
    public class HappeningWorker_FouledWater : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Fishers) != null && !FouledWater.CatchesStopped(map) && FouledWater.Spots(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Fishers);
            var spots = FouledWater.Spots(ctx.map);
            if (ctx.faction == null || spots.Count == 0) return false;
            var first = spots[0];
            ctx.spot = first.cell;
            ctx.note = first.kind;
            ctx.count = spots.Count;
            ctx.tale = ("CC_PEv4_Foul_" + first.kind).Translate().Resolve();
            if (first.thing is Corpse c) ctx.dead = c.InnerPawn?.LabelShort ?? c.LabelShort;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Warden"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Able(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)).OrderBy(p => p.Position.DistanceToSquared(first.cell)).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>The warden walks to the spot and waits there for the answer.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var near = CellFinder.TryFindRandomCellNear(ctx.spot, ctx.map, 4, c => c.Standable(ctx.map) && !(c.GetTerrain(ctx.map)?.IsWater ?? true), out var bank) ? bank : ctx.spot;
            return Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, near, ext.stayHours.RandomInRange) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.count > 1) tags.Add("many");
        }

        protected override LookTargets Look(HappeningContext ctx) =>
            ctx.spot.IsValid ? new LookTargets(new[] { new TargetInfo(ctx.spot, ctx.map) }) : base.Look(ctx);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (FouledWater.Spots(ctx.map, ctx.spot, 20f).Count == 0) return "CC_PEv4_WaterAlreadyClean".Translate().Resolve();
            if (option == 0 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Downed)) return "CC_PEv4_HelperGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "clean" : "left");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The cleaning (FouledWater.Begin): the warden, and with <see cref="together"/> the colony's hands beside them.</summary>
    public class DilemmaEffect_FouledClean : DilemmaEffect
    {
        public bool together;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            if (PlayOuts.On)
                sheet.Play((together ? "CC_PEv4_Impact_Clean" : "CC_PEv4_Impact_CleanAlone").Translate(hc.stranger?.LabelShort ?? "?", hc.a?.LabelShort ?? "?", hc.count).Resolve(),
                    "CC_PEv4_Impact_CleanBrief".Translate().Resolve());
            if (hc.faction != null) sheet.Goodwill(hc.faction, together ? PeopleEvents4Math.CleanGoodwill : PeopleEvents4Math.AloneGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            FouledWater.Begin(hc, together);
        }
    }

    /// <summary>Left as it is: a wrong in their memory, and the boat-trader and the nets stop until it is clean.</summary>
    public class DilemmaEffect_FouledLeft : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Other, -1, "CC_PEv4_Impact_Fouled".Translate(ctx?.faction?.Name ?? "?").Resolve(), "CC_PEv4_Impact_FouledBrief".Translate().Resolve(), "pev4fouled");
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv4_Impact_CatchStop".Translate().Resolve(), "CC_PEv4_Impact_CatchStopBrief".Translate().Resolve(), "pev4catchstop");
        }

        public override void Apply(DilemmaContext ctx) => FouledWater.Left(ctx as HappeningContext);
    }
}
