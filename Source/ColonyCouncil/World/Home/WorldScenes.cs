using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Encounters of the region's hard times (B25 of docs/content-update-proposals.md; Defs/EncounterDefs/
    /// Rimfolk_Encounters_WorldTimes.xml): moments that come only while a world event of their kind runs
    /// (<see cref="EncounterDef.situations"/>), some with a <see cref="EncounterDef.scene"/> that must already be true on the
    /// map, or that plays out as it is told:
    /// <list type="bullet">
    /// <item>pocket: food of the colony's stores close by the visitor; one meal (or a piece of food) goes into their pack.</item>
    /// <item>kneel: a holy place of the colony's (a god's place, a shrine, an altar); the visitor walks to it and kneels a while.</item>
    /// <item>warm: a lit fire or a heater running near the visitor; they go and warm their hands at it a while.</item>
    /// <item>sides: the visitor is of one side of a war between neighbours, and one of the other side ({B}) is near them at a
    /// fire; both sit a while at it.</item>
    /// <item>sky: night, and the colonist and the visitor both out under the open sky.</item>
    /// <item>dog: a dog of the visitor's group and the colony's hens on the map; the dog runs at the hens and they scatter.</item>
    /// <item>hum: the visitor hums (a note over their head).</item>
    /// </list>
    /// Each keeps to what is there; nobody is hurt.
    /// </summary>
    public static class WorldScenes
    {
        public const string Pocket = "pocket", Kneel = "kneel", Warm = "warm", Sides = "sides", Sky = "sky", Dog = "dog", Hum = "hum";

        public static readonly string[] All = { Pocket, Kneel, Warm, Sides, Sky, Dog, Hum };

        private static JobDef kneelJob, warmJob;

        private static JobDef KneelJob => kneelJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_WH_KneelAt");

        private static JobDef WarmJob => warmJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_WH_WarmAt");

        /// <summary>A holy place of the colony's: a god's place, a ritual focus, an altar or a shrine.</summary>
        public static bool Holy(Thing t) =>
            t is Building b && b.Spawned && b.Faction == Faction.OfPlayer && !b.def.IsFrame
            && (Shrines.GodAt(b) != null || b.def.ritualFocus != null || b.def.defName.IndexOf("Altar", StringComparison.OrdinalIgnoreCase) >= 0
                || b.def.defName.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>The colony's holy place nearest a spot, or null.</summary>
        public static Thing HolyPlace(Map map, IntVec3 near) =>
            map?.listerBuildings.allBuildingsColonist.Where(Holy).OrderBy(b => near.IsValid ? b.Position.DistanceToSquared(near) : 0).FirstOrDefault();

        /// <summary>A fire burning (a campfire, a hearth, a fireplace: fuel and heat or flames) or a heater running.</summary>
        private static bool Warmth(Building b)
        {
            if (b == null || !b.Spawned || b.def.IsFrame) return false;
            string n = b.def.defName;
            if (n.IndexOf("Torch", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (b.GetComp<CompRefuelable>() is CompRefuelable r && r.HasFuel && (b.GetComp<CompHeatPusher>() != null || b.GetComp<CompFireOverlay>() != null)) return true;
            return b.GetComp<CompHeatPusher>() != null && b.GetComp<CompPowerTrader>() is CompPowerTrader p && p.PowerOn;
        }

        private static bool Fire(Building b) => b?.GetComp<CompRefuelable>() != null;

        /// <summary>The colony's warmth within <paramref name="radius"/> of a spot, nearest first, or null.</summary>
        public static Building WarmthNear(Map map, IntVec3 at, float radius) =>
            map?.listerBuildings.allBuildingsColonist.Where(b => Warmth(b) && b.Position.InHorDistOf(at, radius)).OrderBy(b => b.Position.DistanceToSquared(at)).FirstOrDefault();

        /// <summary>Food of the colony's stores within reach of the visitor (a meal first), or null.</summary>
        private static Thing FoodNear(Pawn x)
        {
            var map = x?.Map;
            if (map == null) return null;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.IsIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug && !t.def.IsCorpse && t.IsInAnyStorage()
                            && !t.IsForbidden(Faction.OfPlayer) && t.Position.InHorDistOf(x.Position, 12f))
                .OrderByDescending(t => t.def.ingestible.IsMeal).ThenBy(t => t.Position.DistanceToSquared(x.Position)).FirstOrDefault();
        }

        private static bool DogLike(Pawn a)
        {
            string l = (a?.kindDef?.label ?? a?.def?.label ?? "").ToLowerInvariant();
            return a != null && a.RaceProps.Animal && new[] { "dog", "hound", "husky", "terrier", "retriever", "collie", "shepherd" }.Any(l.Contains);
        }

        private static Pawn DogOf(Pawn x) =>
            x?.Map?.mapPawns.SpawnedPawnsInFaction(x.Faction).Where(a => DogLike(a) && !a.Downed && !a.Dead).OrderBy(a => a.Position.DistanceToSquared(x.Position)).FirstOrDefault();

        private static List<Pawn> Hens(Map map) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => !a.Downed && !a.Dead && a.GetComp<CompEggLayer>() != null && a.RaceProps.baseBodySize < 0.6f).ToList() ?? new List<Pawn>();

        /// <summary>The visitor of the other side of a war, near this one at a fire: the second person of the "sides" scene.</summary>
        private static Pawn OtherSide(Pawn x, WorldSituation war, out Building fire)
        {
            fire = null;
            if (x?.Map == null || war?.ctx?.faction == null || war.ctx.enemy == null) return null;
            var mine = x.Faction;
            var other = mine == war.ctx.faction ? war.ctx.enemy : mine == war.ctx.enemy ? war.ctx.faction : null;
            if (other == null) return null;
            fire = WarmthNear(x.Map, x.Position, 10f);
            if (fire == null) return null;
            var f = fire;
            return x.Map.mapPawns.SpawnedPawnsInFaction(other).FirstOrDefault(y => y.RaceProps.Humanlike && !y.Downed && y.Awake() && y.Position.InHorDistOf(f.Position, 10f));
        }

        /// <summary>The scene is true on the map now, for this colonist and this visitor (Encounters.Cast).</summary>
        public static bool Fits(EncounterDef def, Pawn a, Pawn x, Map map)
        {
            if (def?.scene.NullOrEmpty() ?? true) return true;
            if (x == null || map == null) return false;
            switch (def.scene)
            {
                case Pocket: return x.inventory?.innerContainer != null && FoodNear(x) != null;
                case Kneel: return KneelJob != null && HolyPlace(map, x.Position) is Thing h && x.CanReach(h, PathEndMode.Touch, Danger.Some);
                case Warm: return WarmJob != null && WarmthNear(map, x.Position, 20f) is Building w && x.CanReach(w, PathEndMode.Touch, Danger.Some);
                case Sides: return WarmJob != null && OtherSide(x, WorldSeason.Of(def.situations), out _) != null;
                case Sky:
                    int hour = GenLocalDate.HourOfDay(map);
                    return (hour >= 21 || hour < 4) && !x.Position.Roofed(map) && a != null && !a.Position.Roofed(map);
                case Dog: return DogOf(x) != null && Hens(map).Count > 0;
                case Hum: return true;
                default: return false;
            }
        }

        /// <summary>Who else the words name ({B}: the other side's visitor) and the scene's tags ([fire], [heater]), before its words are picked.</summary>
        public static void Fill(EncounterDef def, DilemmaContext ctx, HashSet<string> tags)
        {
            if (def == null || def.scene.NullOrEmpty() || ctx?.stranger == null) return;
            var x = ctx.stranger;
            Building warmth = null;
            if (def.scene == Sides)
            {
                ctx.b = OtherSide(x, WorldSeason.Of(def.situations), out warmth);
                if (ctx.b != null) ctx.faction = x.Faction;
            }
            else if (def.scene == Warm) warmth = WarmthNear(x.Map, x.Position, 20f);
            if (warmth != null) tags?.Add(Fire(warmth) ? "fire" : "heater");
        }

        /// <summary>The scene plays out as it is told (Encounters.Told, before the words go out).</summary>
        public static void Play(EncounterDef def, DilemmaContext ctx)
        {
            if (def?.scene.NullOrEmpty() ?? true) return;
            var x = ctx?.stranger;
            var map = x?.Map;
            if (map == null) return;
            try
            {
                switch (def.scene)
                {
                    case Pocket:
                        var food = FoodNear(x);
                        if (food == null) return;
                        var one = food.SplitOff(1);
                        if (!x.inventory.innerContainer.TryAdd(one)) GenPlace.TryPlaceThing(one, food.Position, map, ThingPlaceMode.Near);
                        return;
                    case Kneel:
                        var holy = HolyPlace(map, x.Position);
                        if (holy != null) Sit(x, KneelJob, holy);
                        return;
                    case Warm:
                        var w = WarmthNear(map, x.Position, 20f);
                        if (w != null) Sit(x, WarmJob, w);
                        return;
                    case Sides:
                        var y = OtherSide(x, WorldSeason.Of(def.situations), out var fire);
                        if (fire == null) return;
                        Sit(x, WarmJob, fire);
                        if (y != null) Sit(y, WarmJob, fire);
                        return;
                    case Dog:
                        RunAtHens(DogOf(x), Hens(map));
                        return;
                    case Hum:
                        var note = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                        if (note != null && VisualTouches.On) FleckMaker.ThrowMetaIcon(x.Position, map, note);
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a world event's encounter scene " + def.scene, e);
            }
        }

        /// <summary>A visitor goes to a free cell beside the thing and stays a while facing it (the visitors' own work, JobDriver_VisitWork).</summary>
        private static void Sit(Pawn p, JobDef job, Thing at)
        {
            if (p == null || job == null || at == null || !p.Spawned) return;
            var map = p.Map;
            var cells = GenAdj.CellsAdjacent8Way(at).Where(c => c.InBounds(map) && c.Standable(map) && p.CanReach(c, PathEndMode.OnCell, Danger.Some)
                                                               && map.thingGrid.ThingsListAt(c).All(t => !(t is Pawn))).OrderBy(c => c.DistanceToSquared(p.Position)).ToList();
            if (cells.Count == 0) return;
            var j = JobMaker.MakeJob(job, cells[0], at);
            p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The dog runs at the hens, and those near them scatter.</summary>
        private static void RunAtHens(Pawn dog, List<Pawn> hens)
        {
            if (dog == null || hens.Count == 0) return;
            var target = hens.OrderBy(h => h.Position.DistanceToSquared(dog.Position)).First();
            var go = JobMaker.MakeJob(JobDefOf.Goto, target.Position);
            go.locomotionUrgency = LocomotionUrgency.Jog;
            dog.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
            foreach (var h in hens.Where(h => h.Position.InHorDistOf(target.Position, 8f)).Take(8))
                if (RCellFinder.TryFindDirectFleeDestination(h.Position, 8f, h, out var away))
                {
                    var flee = JobMaker.MakeJob(JobDefOf.Flee, away, dog);
                    h.jobs.StartJob(flee, JobCondition.InterruptForced, null, false, true);
                }
        }
    }
}
