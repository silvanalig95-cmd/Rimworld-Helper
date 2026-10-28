using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Stones set where the words put them, a chunk of the rock under this land: at the head of each of the colony's
    /// graves, cut by the one the answer names (DilemmaEffect_MarkGraves, OnMapTimed's Marking); at the head of fresh
    /// ground where old bones were laid again (Rebury); flat over something buried where a walk went (Stone). Fresh
    /// ground shows as turned earth. A walk sets its stone when it gets there (PlayGroups.StepWalk), and if it never
    /// does, the stone is set anyway, as the answer always had it (PlayAfter).
    /// </summary>
    public static class PlayMarkers
    {
        public const string Rebury = "rebury", Stone = "stone";

        /// <summary>A chunk of the rock that lies under this land, or null.</summary>
        public static ThingDef StoneFor(Map map) => map == null ? null : DilemmaEffect_Plant.MarkerStone(map);

        private static bool Free(Map map, IntVec3 c) =>
            c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null && c.GetZone(map) == null
            && !(c.GetPlant(map) is Plant p && p.def.plant.IsTree) && !(c.GetTerrain(map)?.IsWater ?? false);

        /// <summary>A stone set at the cell, or on the nearest free cell beside it. Null when there's no room.</summary>
        public static Thing SetStone(Map map, IntVec3 at, ThingDef stone)
        {
            stone ??= StoneFor(map);
            if (map == null || stone == null || !at.IsValid) return null;
            foreach (var c in GenRadial.RadialCellsAround(at, 1.9f, true))
            {
                if (!Free(map, c)) continue;
                return GenSpawn.Spawn(ThingMaker.MakeThing(stone), c, map);
            }
            return null;
        }

        /// <summary>The head of a grave: the free cell beside it at the end it faces, or invalid.</summary>
        public static IntVec3 HeadCell(Thing grave)
        {
            var map = grave?.Map;
            if (map == null) return IntVec3.Invalid;
            var face = grave.Rotation.FacingCell;
            var best = IntVec3.Invalid;
            int bestDot = int.MinValue;
            foreach (var c in GenAdj.CellsAdjacentCardinal(grave))
            {
                if (!Free(map, c)) continue;
                var d = c - grave.Position;
                int dot = d.x * face.x + d.z * face.z;
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = c;
            }
            return best;
        }

        /// <summary>The colony's graves with someone in them, the nearest the middle of the colony first, a dozen at most.</summary>
        public static List<Building_Grave> Graves(Map map)
        {
            if (map == null) return new List<Building_Grave>();
            var center = TemplatePlanner.ColonyCenter(map);
            return map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned && g.HasCorpse && !g.Position.Fogged(map))
                .OrderBy(g => g.Position.DistanceToSquared(center)).Take(12).ToList();
        }

        /// <summary>
        /// The ground marked near <paramref name="at"/>: for a reburial, turned earth with a stone at its head; for something
        /// buried, a flat stone over it in the turned earth. The stone, or null.
        /// </summary>
        public static Thing MarkGround(Map map, IntVec3 at, string kind)
        {
            if (map == null || !at.IsValid) return null;
            if (!CellFinder.TryFindRandomCellNear(at, map, 3, c => Free(map, c) && !c.Roofed(map), out var spot)) spot = at;
            FilthMaker.TryMakeFilth(spot, map, ThingDefOf.Filth_Dirt, 2);
            Thing stone;
            if (kind == Rebury)
            {
                var head = GenAdj.CellsAdjacentCardinal(spot, Rot4.North, IntVec2.One).FirstOrDefault(c => Free(map, c));
                if (head.IsValid) FilthMaker.TryMakeFilth(spot + IntVec3.South, map, ThingDefOf.Filth_Dirt, 1);
                stone = SetStone(map, head.IsValid ? head : spot, null);
            }
            else stone = SetStone(map, spot, null);
            if (stone != null) Messages.Message((kind == Rebury ? "CC_Markers_Reburied" : "CC_Markers_Buried").Translate().Resolve(), stone, MessageTypeDefOf.SilentInput, false);
            return stone;
        }

        /// <summary>A walk that has got where it was going: the ground it went for is marked now.</summary>
        public static void Arrived(PlayOut p)
        {
            if (p == null || p.afterward != Rebury && p.afterward != Stone) return;
            string kind = p.afterward;
            p.afterward = null;
            try
            {
                MarkGround(p.map, p.cell, kind);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ground marked", e);
            }
        }

        /// <summary>It never got there: the ground is marked anyway, where it was going, else by the colony's graves or just outside its walls.</summary>
        public static void Fallback(PlayOut p, string kind) => MarkGround(p.map, p.cell.IsValid ? p.cell : FallbackCell(p.map), kind);

        public static IntVec3 FallbackCell(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var grave = Graves(map).FirstOrDefault();
            if (grave != null) return grave.Position;
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 40f, true))
                if (c.InBounds(map) && !map.areaManager.Home[c] && Free(map, c) && !c.Roofed(map) && !c.Fogged(map)) return c;
            return center;
        }
    }

    /// <summary>
    /// Ground marked with a stone (<see cref="kind"/>: "rebury", turned earth and a stone at its head; "stone", a flat
    /// stone over something buried), where the walk the answer set going gets to, or now without one. After the walk.
    /// </summary>
    public class DilemmaEffect_MarkGround : DilemmaEffect
    {
        public string kind = PlayMarkers.Rebury;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            var walk = PlayOuts.On ? PlayOuts.Collected().FirstOrDefault(p => p.kind == PlayKind.Procession && p.state == PlayState.Pending && p.afterward.NullOrEmpty()) : null;
            if (walk != null)
            {
                walk.afterward = kind;
                return;
            }
            PlayMarkers.MarkGround(map, PlayMarkers.FallbackCell(map), kind);
        }
    }

    /// <summary>
    /// A stone at the head of each of the colony's graves, cut and set one after another by <see cref="who"/> (the
    /// stonecutter X by default; OnMapTimed's Marking). Nobody to cut them, or "Answers play out on the map" off: set now.
    /// </summary>
    public class DilemmaEffect_MarkGraves : DilemmaEffect
    {
        public PlayWho who = PlayWho.X;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var heads = PlayMarkers.Graves(map).Select(PlayMarkers.HeadCell).Where(c => c.IsValid).Distinct().ToList();
                var stone = PlayMarkers.StoneFor(map);
                if (heads.Count == 0 || stone == null) return;
                var x = PlayOuts.Who(who, ctx);
                if (PlayOuts.On && OnMapTimed.Instance != null && x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed
                    && x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    OnMapTimed.Instance.AddMarking(map, heads, x, stone);
                    return;
                }
                var set = heads.Select(h => PlayMarkers.SetStone(map, h, stone)).Where(t => t != null).ToList();
                if (set.Count > 0) Messages.Message("CC_Markers_Set".Translate(set.Count).Resolve(), new LookTargets(set), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("grave markers", e);
            }
        }
    }

    /// <summary>Cutting a grave's stone (OnMapTimed's Marking): to the head of the grave, a while at the stone, and it's set.</summary>
    public class JobDriver_PlayOutMark : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var cut = ToilMaker.MakeToil("PlayOutMark");
            cut.defaultCompleteMode = ToilCompleteMode.Delay;
            cut.defaultDuration = 900;
            cut.handlingFacing = true;
            cut.tickIntervalAction = _ => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            cut.WithProgressBarToilDelay(TargetIndex.A);
            var chisel = DefDatabase<EffecterDef>.GetNamedSilentFail("Smooth") ?? DefDatabase<EffecterDef>.GetNamedSilentFail("Mine");
            if (chisel != null) cut.WithEffect(chisel, TargetIndex.A);
            yield return cut;
            var done = ToilMaker.MakeToil("PlayOutMarked");
            done.initAction = () =>
            {
                try
                {
                    OnMapTimed.Marked(pawn, job.targetA.Cell);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("grave stone set", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
