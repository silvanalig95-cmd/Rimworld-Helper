using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    // The content round 3, its eight events of everyday life on the map (docs/content-round-3-events-gods.md, R1 to R8;
    // Defs/IncidentDefs/Rimfolk_Round3_Events.xml): wild boar rooting under the trees in a mast year, a snake coiled in the
    // woodpile in the heat, a roof creaking under deep snow, the way across the marsh to the fields, a cold room under the rock
    // for the summer's food, seals at the fishing ground, dinner carried out to a far field, clothes worn through before winter.
    // Each in every age, each answer playing out on the map. What an event keeps on the map is one of the things OnMapTimed
    // keeps (PlayOut/OnMapTimed.cs: R3Boar, R3Snake, R3Roof, R3Seals, R3Dinner); the steps are here (Round3Map), and the
    // shared helpers are round 2's (Round2Map). Every hurt goes through the shared harm budget (a snake's bite on the stick,
    // HarmKinds.Dangers), is named under the answer before it's chosen and is shown; a hunted beast's revenge is the game's
    // own and named too. The rules without game types are in Round3EventsMath. Event cards, so plain words; the spoken lines
    // are tagged with their moment.

    /// <summary>The third round's part of what is kept on the map: the steps of each, and what is left when it ends.</summary>
    public static class Round3Map
    {
        private static int Now => Find.TickManager.TicksGame;

        public static int Ticks(float hours) => Mathf.RoundToInt(hours * GenDate.TicksPerHour);

        public static float HoursSince(int tick) => (Now - tick) / (float)GenDate.TicksPerHour;

        public static bool Step(TimedThing k, int now)
        {
            switch (k.kind)
            {
                case TimedKind.R3Boar:
                case TimedKind.R3Seals:
                    return Round3Beasts.Step(k, now);
                case TimedKind.R3Snake: return Round3Snake.Step(k, now);
                case TimedKind.R3Roof: return Round3Roof.Step(k, now);
                case TimedKind.R3Dinner: return Round3Dinner.Step(k, now);
                default: return true;
            }
        }

        public static void End(TimedThing k, bool early)
        {
            switch (k.kind)
            {
                case TimedKind.R3Boar:
                case TimedKind.R3Seals:
                    Round3Beasts.End(k, early);
                    break;
                case TimedKind.R3Snake:
                    Round3Snake.End(k);
                    break;
            }
        }

        // ---------------- Shared ----------------

        /// <summary>One sentence over <paramref name="p"/>'s head for the moment (the event's lines tagged with it, in the colony's age).</summary>
        public static void Say(string eventDef, Pawn p, string moment)
        {
            try
            {
                var lines = DefDatabase<IncidentDef>.GetNamedSilentFail(eventDef ?? "")?.GetModExtension<HappeningExtension>()?.lines;
                if (p == null || !p.Spawned || lines.NullOrEmpty()) return;
                var tags = new HashSet<string> { moment };
                Flavor.AddEraTags(tags);
                string line = Flavor.PickTagged(lines, tags);
                if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(p.Named("PAWN")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: a line", e);
            }
        }

        /// <summary>Something of this kind is already kept on this map.</summary>
        public static bool Kept(Map map, TimedKind kind) => OnMapTimed.Instance?.kept.Any(k => k.kind == kind && k.map == map) ?? false;

        /// <summary>The first of these animals the game has loaded that can live here in this season, or null.</summary>
        public static PawnKindDef Kind(Map map, params string[] names)
        {
            foreach (var n in names)
            {
                var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
                if (def?.race != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(def.race, 0f)) return def;
            }
            return null;
        }

        /// <summary>A wild animal of <paramref name="kind"/> made and set down at <paramref name="at"/>, or null.</summary>
        public static Pawn Spawn(PawnKindDef kind, IntVec3 at, Map map)
        {
            if (kind == null || map == null || !at.IsValid) return null;
            try
            {
                var x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                return (Pawn)GenSpawn.Spawn(x, at, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: an animal", e);
                return null;
            }
        }

        /// <summary>The animals the thing kept is about, still on the map and about.</summary>
        public static List<Pawn> Animals(TimedThing k) =>
            k.things.OfType<Pawn>().Where(x => x != null && !x.Dead && x.Spawned && x.Map == k.map && x.Faction == null).ToList();

        /// <summary>The game's own chance that a hunted one of these turns on its hunter (none when it can't be read).</summary>
        public static float Revenge(IEnumerable<Pawn> xs, Pawn hunter)
        {
            try
            {
                var list = xs.Where(x => x != null).ToList();
                return list.Count == 0 ? 0f : list.Max(x => PawnUtility.GetManhunterOnDamageChance(x, hunter));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: a hunt's risk", e);
                return 0f;
            }
        }

        /// <summary>A sound of the game's at a place, by name, when it is loaded.</summary>
        public static void Sound(Map map, IntVec3 at, string name)
        {
            var def = DefDatabase<SoundDef>.GetNamedSilentFail(name);
            if (def == null || map == null || !at.IsValid) return;
            try
            {
                def.PlayOneShot(new TargetInfo(at, map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: a sound", e);
            }
        }

        /// <summary>The cheapest stuff for <paramref name="def"/> the colony has enough of now, else wood.</summary>
        public static ThingDef Stuff(Map map, ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            int cost = Math.Max(1, def.costStuffCount);
            var allowed = GenStuff.AllowedStuffsFor(def).ToList();
            return map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= cost && kv.Key.IsStuff && allowed.Contains(kv.Key))
                       .OrderBy(kv => kv.Key.BaseMarketValue).Select(kv => kv.Key).FirstOrDefault()
                   ?? (allowed.Contains(ThingDefOf.WoodLog) ? ThingDefOf.WoodLog : allowed.FirstOrDefault());
        }

        /// <summary>One of the colony who can be sent now (not <paramref name="not"/>), nearest <paramref name="at"/> first.</summary>
        public static Pawn Another(Map map, IntVec3 at, params Pawn[] not) =>
            StorytellerEvents.Adults(map).Where(p => !not.Contains(p) && Round2Map.Free(p)).OrderBy(p => at.IsValid ? p.Position.DistanceToSquared(at) : 0).FirstOrDefault();

        /// <summary>A cell beside <paramref name="at"/> that <paramref name="p"/> can stand on and reach.</summary>
        public static IntVec3 Beside(Map map, IntVec3 at, Pawn p)
        {
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(at, map)).OrderBy(c => p == null ? 0 : c.DistanceToSquared(p.Position)))
                if (c.InBounds(map) && c.Standable(map) && (p == null || p.CanReach(c, PathEndMode.OnCell, Danger.Some)))
                    return c;
            return IntVec3.Invalid;
        }
    }

    // ===================================================================================================
    // R1. The mast year, and R6. Seals at the fishing ground: wild animals by the colony, hunted, driven off or let be
    // ===================================================================================================

    /// <summary>
    /// In autumn, with many trees near the colony, a group of wild boar comes in from the edge to the trees nearest it and roots
    /// there for the fallen nuts (Vanilla Animals Expanded - Wastes' wasteboar on a polluted map). A is the one who saw them,
    /// B the colony's best shot.
    /// </summary>
    public class HappeningWorker_R3MastYear : IncidentWorker_Happening
    {
        public static PawnKindDef Boar(Map map)
        {
            bool fouled = ModsConfig.BiotechActive && (map.pollutionGrid?.TotalPollutionPercent ?? 0f) >= 0.3f;
            return fouled ? Round3Map.Kind(map, "VAEWaste_Wasteboar", "WildBoar") : Round3Map.Kind(map, "WildBoar");
        }

        /// <summary>The trees near the colony, out of its home ground.</summary>
        public static List<Thing> Trees(Map map)
        {
            var c = CampSite.ColonyCenter(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .Where(t => t is Plant p && p.def.plant != null && p.def.plant.IsTree && p.Spawned && !map.areaManager.Home[p.Position] && !p.Position.Fogged(map)
                            && p.Position.InHorDistOf(c, Round3EventsMath.TreesWithin))
                .ToList();
        }

        /// <summary>The thickest part of the trees near the colony: the tree with the most others round it.</summary>
        public static IntVec3 Grove(List<Thing> trees)
        {
            IntVec3 best = IntVec3.Invalid;
            int most = -1;
            foreach (var t in trees.Take(80))
            {
                int n = trees.Count(o => o.Position.InHorDistOf(t.Position, 6f));
                if (n <= most) continue;
                most = n;
                best = t.Position;
            }
            return best;
        }

        private static Pawn Shot(Map map, Pawn not) =>
            StorytellerEvents.Adults(map).Where(p => p != not && !p.WorkTagIsDisabled(WorkTags.Violent)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && !Round3Map.Kept(map, TimedKind.R3Boar) && StorytellerEvents.Adults(map).Any(p => p.Awake())
            && Round3EventsMath.MastFits(GenLocalDate.Season(map) == Season.Fall, Trees(map).Count, Boar(map) != null);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var grove = Grove(Trees(map));
            if (!grove.IsValid) return false;
            ctx.spot = Round2Map.Near(map, grove, 4f, null);
            if (!ctx.spot.IsValid) return false;
            ctx.a = StorytellerEvents.Adults(map).Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = Shot(map, ctx.a) ?? ctx.a;
            var kind = Boar(map);
            if (kind == null) return false;
            ctx.beast = kind.GetLabelPlural();
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var kind = Boar(map);
            if (kind == null) return false;
            // In from the edge nearest the trees, else from any edge they can walk in from.
            bool In(IntVec3 c) => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, ctx.spot, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors));
            if (!CellFinder.TryFindRandomEdgeCellNearWith(ctx.spot, 60f, map, In, out var edge)
                && !CellFinder.TryFindRandomEdgeCellWith(In, map, CellFinder.EdgeRoadChance_Animal, out edge))
                return false;
            int n = Rand.RangeInclusive(Round3EventsMath.BoarMin, Round3EventsMath.BoarMax);
            for (int i = 0; i < n; i++)
            {
                var x = Round3Map.Spawn(kind, CellFinder.RandomClosewalkCellNear(edge, map, 3), map);
                if (x == null) continue;
                ctx.things.Add(x);
                Round2Map.GoTo(x, Round2Map.Near(map, ctx.spot, 5f, x));
            }
            ctx.stranger = ctx.things.OfType<Pawn>().FirstOrDefault();
            if (ctx.stranger == null) return false;
            var k = Round2Map.Keep(TimedKind.R3Boar, ctx, null, ctx.stranger);
            if (k == null) return false;
            k.things.AddRange(ctx.things);
            Round3Map.Sound(map, edge, "Pawn_Wildboar_Call");
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
            if (ctx.map != null && HerdHome.Out(ctx.map).Any()) tags.Add("herd");
            if (ctx.stranger?.kindDef?.defName == "VAEWaste_Wasteboar") tags.Add("fouled");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// In winter or spring a few seals (Odyssey's seals and sea lions on the coast, its otters on a lake or river) come up on
    /// the shore by the colony's fishing ground, lie there and go into the water after the fish. A is the fisher who saw them,
    /// B one who could go down and drive them off.
    /// </summary>
    public class HappeningWorker_R3Seals : IncidentWorker_Happening
    {
        private static bool Sea(TerrainDef t) => t != null && t.IsWater && t.defName.IndexOf("Ocean", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The fishing ground is at the sea.</summary>
        public static bool AtSea(Map map, Zone z) => z != null && z.Cells.Take(60).Any(c => Sea(c.GetTerrain(map)));

        public static PawnKindDef Kind(Map map, Zone z) =>
            AtSea(map, z) ? (Rand.Chance(0.35f) ? Round3Map.Kind(map, "SeaLion", "Seal") : Round3Map.Kind(map, "Seal", "SeaLion")) : Round3Map.Kind(map, "Otter");

        /// <summary>Open shore by the fishing ground, beside the water, out of the colony's home ground; a dozen at most.</summary>
        public static List<IntVec3> Shore(Map map, Zone z)
        {
            var list = new List<IntVec3>();
            if (z == null || z.Cells.Count == 0) return list;
            var mid = z.Cells[z.Cells.Count / 2];
            foreach (var c in GenRadial.RadialCellsAround(mid, 12f, true))
            {
                if (list.Count >= 12) break;
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetTerrain(map).IsWater || map.areaManager.Home[c] || c.GetFirstPawn(map) != null) continue;
                if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetTerrain(map).IsWater)) list.Add(c);
            }
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Round3Map.Kept(map, TimedKind.R3Seals) || !DilemmaWorker_CustomMore.Fishing(map)) return false;
            var z = HappeningWorker_FishRunning.Ground(map);
            return z != null && Kind(map, z) != null && Shore(map, z).Count >= 3 && StorytellerEvents.Adults(map).Count() >= 2;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = HappeningWorker_FishRunning.Ground(map);
            var shore = Shore(map, z);
            if (shore.Count < 3) return false;
            ctx.spot = shore[0];
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = adults.Where(p => p.Awake()).OrderBy(p => (p.CurJobDef?.defName.IndexOf("Fish", StringComparison.Ordinal) ?? -1) >= 0 ? 0 : 1)
                .ThenBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = adults.Where(p => p != ctx.a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0).FirstOrDefault();
            if (ctx.b == null) return false;
            var kind = Kind(map, z);
            if (kind == null) return false;
            ctx.note = kind.defName;
            ctx.beast = kind.GetLabelPlural();
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = HappeningWorker_FishRunning.Ground(map);
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ctx.note ?? "") ?? Kind(map, z);
            var shore = Shore(map, z);
            if (kind == null || shore.Count < 3) return false;
            int n = Math.Min(shore.Count, Rand.RangeInclusive(Round3EventsMath.SealsMin, Round3EventsMath.SealsMax));
            foreach (var c in shore.Take(n))
            {
                var x = Round3Map.Spawn(kind, c, map);
                if (x == null) continue;
                ctx.things.Add(x);
                FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.2f, 2f);
            }
            ctx.stranger = ctx.things.OfType<Pawn>().FirstOrDefault();
            if (ctx.stranger == null) return false;
            var k = Round2Map.Keep(TimedKind.R3Seals, ctx, null, ctx.stranger);
            if (k == null) return false;
            k.things.AddRange(ctx.things);
            foreach (var c in z.Cells.InRandomOrder().Take(20)) k.cells.Add(c);
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
            tags.Add(ctx.stranger?.kindDef?.defName == "Otter" ? "fresh" : "sea");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// What the council says about the wild animals by the colony (the boar in the mast, the seals at the fishing ground, by
    /// <see cref="of"/>): <c>hunt</c> (the two biggest marked for the hunters, the game's own revenge chance named), <c>drive</c>
    /// (B and one more walk out at them shouting, and they run off the land) or <c>let</c> (they stay a day or two by the trees
    /// or the water and then go).
    /// </summary>
    public class DilemmaEffect_R3Beasts : DilemmaEffect
    {
        public string mode = "let";
        /// <summary>"boar" (the mast year) or "seals" (the fishing ground).</summary>
        public string of = "boar";

        public override bool ChanceEach => false;

        private TimedKind Kind => of == "seals" ? TimedKind.R3Seals : TimedKind.R3Boar;

        private int Taken => of == "seals" ? Round3EventsMath.SealsTaken : Round3EventsMath.BoarTaken;

        private float StayDays => of == "seals" ? Round3EventsMath.SealsStayDays : Round3EventsMath.BoarStayDays;

        private TimedThing Kept(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return null;
            var timed = OnMapTimed.Instance;
            if (timed == null) return null;
            var k = timed.kept.FirstOrDefault(x => x.kind == Kind && x.map == h.map && (x.animal == h.stranger || h.stranger != null && x.things.Contains(h.stranger)));
            if (k != null || h.stranger == null || h.things.Count == 0) return k;
            // A load or the cap lost it: kept again from the event's own animals.
            k = Round2Map.Keep(Kind, h, null, h.stranger);
            k?.things.AddRange(h.things.OfType<Pawn>().Where(p => p.Spawned && !p.Dead));
            return k;
        }

        private List<Pawn> Here(DilemmaContext ctx)
        {
            var k = Kept(ctx);
            return k != null ? Round3Map.Animals(k) : (ctx as HappeningContext)?.things.OfType<Pawn>().Where(p => p.Spawned && !p.Dead && p.Faction == null).ToList() ?? new List<Pawn>();
        }

        private List<Pawn> Marks(DilemmaContext ctx) =>
            Here(ctx).Where(x => x.Map.designationManager.DesignationOn(x) == null && !x.Downed).OrderByDescending(x => x.BodySize).ThenByDescending(x => x.ageTracker.AgeBiologicalYearsFloat).Take(Taken).ToList();

        private string Label(DilemmaContext ctx) => (ctx as HappeningContext)?.beast ?? ctx?.stranger?.kindDef?.GetLabelPlural() ?? "";

        public override string Unavailable(DilemmaContext ctx)
        {
            if (Here(ctx).Count == 0) return "CC_R3_Beasts_Gone".Translate(Label(ctx)).Resolve();
            if (mode == "hunt" && Marks(ctx).Count == 0) return "CC_R3_Beasts_Gone".Translate(Label(ctx)).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || Here(ctx).Count == 0) return;
            string kind = Label(ctx);
            switch (mode)
            {
                case "hunt":
                    var marks = Marks(ctx);
                    if (marks.Count == 0) return;
                    sheet.Play("CC_R3_Impact_Hunt".Translate(marks.Count, kind).Resolve(), "CC_R3_Impact_HuntBrief".Translate(kind).Resolve());
                    if (Round3Map.Revenge(marks, ctx.b ?? ctx.a) >= 0.01f)
                        sheet.Add(ImpactKind.Health, -1, "CC_R3_Impact_HuntRisk".Translate(kind).Resolve(), "CC_R3_Impact_HuntRiskBrief".Translate().Resolve(), "r3huntrisk");
                    break;
                case "drive":
                    var other = Round3Map.Another(ctx.map, ctx.b?.Position ?? IntVec3.Invalid, ctx.b);
                    sheet.Play("CC_R3_Impact_Drive".Translate((ctx.b ?? ctx.a)?.LabelShort ?? "", other?.LabelShort ?? "", kind).Resolve(), "CC_R3_Impact_DriveBrief".Translate(kind).Resolve());
                    break;
                default:
                    sheet.Play(("CC_R3_Impact_Let_" + of).Translate(kind).Resolve(), "CC_R3_Impact_LetBrief".Translate(kind).Resolve());
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
            switch (mode)
            {
                case "hunt":
                    var marks = Marks(ctx);
                    foreach (var x in marks)
                    {
                        try
                        {
                            x.Map.designationManager.AddDesignation(new Designation(x, DesignationDefOf.Hunt));
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("round 3: a hunt marked", e);
                        }
                    }
                    if (marks.Count > 0)
                        Round2Map.Told(ctx.map, "CC_R3_Beasts_Marked".Translate(marks.Count, Label(ctx)).Resolve(), new LookTargets(marks.Cast<Thing>()));
                    k.count = 1;
                    k.until = now + Round3Map.Ticks(StayDays * 24f);
                    break;
                case "drive":
                    var b = ctx.b != null && Round2Map.Free(ctx.b) ? ctx.b : Round3Map.Another(ctx.map, k.cell);
                    if (b != null) k.pawns.Add(b);
                    var other = Round3Map.Another(ctx.map, b?.Position ?? k.cell, b);
                    if (other != null) k.pawns.Add(other);
                    k.count = k.pawns.Count > 0 ? 2 : 3;
                    k.until = now + Round3Map.Ticks(k.count == 2 ? 8f : StayDays * 24f);
                    break;
                default:
                    k.count = 3;
                    k.until = now + Round3Map.Ticks(StayDays * 24f);
                    break;
            }
        }
    }

    /// <summary>
    /// The wild animals by the colony (TimedKind.R3Boar, R3Seals): k.count 0 waiting for the answer, 1 two marked for the hunters
    /// (the rest stay), 2 driven off, 3 let be. Those not marked stay by the place (the boar rooting among the trees, the seals
    /// going into the water after the fish) until their time is up, and then walk off the land.
    /// </summary>
    public static class Round3Beasts
    {
        private static bool Seals(TimedThing k) => k.kind == TimedKind.R3Seals;

        private static bool Marked(Pawn x) => x.Map?.designationManager.DesignationOn(x) != null;

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            var beasts = Round3Map.Animals(k);
            if (beasts.Count == 0) return true;
            if (k.count == 2) return Drive(k, beasts, now);
            if (now > k.until)
            {
                Go(beasts.Where(x => !Marked(x)), LocomotionUrgency.Walk);
                if (beasts.Any(x => !Marked(x))) Round2Map.Told(map, (Seals(k) ? "CC_R3_Seals_Gone" : "CC_R3_Boar_Gone").Translate(beasts[0].kindDef.GetLabelPlural()).Resolve(), new LookTargets(beasts[0]));
                k.on = true;
                return true;
            }
            foreach (var x in beasts)
            {
                if (Marked(x) || x.Downed || x.InMentalState) continue;
                Stay(k, x);
            }
            return false;
        }

        /// <summary>One of them keeps to the place: back to it when it strays, else about it, rooting or splashing.</summary>
        private static void Stay(TimedThing k, Pawn x)
        {
            var map = k.map;
            if (x.CurJobDef == JobDefOf.Goto || x.CurJobDef == JobDefOf.GotoWander) return;
            if (x.Position.DistanceTo(k.cell) > 9f) Round2Map.GoTo(x, Round2Map.Near(map, k.cell, 6f, x));
            else if (Rand.Chance(0.35f)) Round2Map.Wander(x, k.cell, 6f);
            if (Seals(k))
            {
                if (k.cells.Count > 0 && Rand.Chance(0.3f))
                {
                    var c = k.cells.RandomElement();
                    if (c.InBounds(map) && !c.Fogged(map)) FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1f, 1.8f);
                }
            }
            else if (Rand.Chance(0.3f)) FleckMaker.ThrowDustPuff(x.Position, map, 0.6f);
        }

        private static void Go(IEnumerable<Pawn> beasts, LocomotionUrgency urgency)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var x in beasts.ToList())
            {
                Round2Map.Leave(x, urgency);
                if (x.mindState != null) x.mindState.exitMapAfterTick = now + GenDate.TicksPerHour;
            }
        }

        /// <summary>Two walk out at them, shouting; once they're close, the animals run off the land and the two walk home.</summary>
        private static bool Drive(TimedThing k, List<Pawn> beasts, int now)
        {
            var map = k.map;
            var go = k.pawns.Where(p => Round2Map.Here(p, map) && !p.Drafted && !p.InMentalState).ToList();
            if (go.Count == 0)
            {
                // Nobody left to go: they stay a while and then go by themselves.
                k.count = 3;
                k.until = now + Round3Map.Ticks((Seals(k) ? Round3EventsMath.SealsStayDays : Round3EventsMath.BoarStayDays) * 24f);
                return false;
            }
            if (go.Any(p => beasts.Any(x => p.Position.InHorDistOf(x.Position, 8f))))
            {
                MapDangers.Say(k, go[0], "drive");
                Go(beasts, LocomotionUrgency.Jog);
                foreach (var p in go) Round2Map.GoTo(p, Round2Map.Home(map, p));
                Round2Map.Told(map, (Seals(k) ? "CC_R3_Seals_Driven" : "CC_R3_Boar_Driven").Translate(beasts[0].kindDef.GetLabelPlural()).Resolve(), new LookTargets(beasts[0]));
                k.on = true;
                return true;
            }
            var near = beasts.OrderBy(x => x.Position.DistanceToSquared(go[0].Position)).First();
            foreach (var p in go)
            {
                if (p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell.InHorDistOf(near.Position, 5f)) continue;
                Round2Map.GoTo(p, Round2Map.Near(map, near.Position, 4f, p), LocomotionUrgency.Jog);
            }
            if (Rand.Chance(0.15f)) MapDangers.Say(k, go.RandomElement(), "shout");
            return now > k.until;
        }

        public static void End(TimedThing k, bool early)
        {
            // Over before their time (the cap, a load): they don't stay on.
            if (early) Go(Round3Map.Animals(k).Where(x => !Marked(x)), LocomotionUrgency.Walk);
        }
    }

    // ===================================================================================================
    // R2. A snake in the woodpile
    // ===================================================================================================

    /// <summary>
    /// In the summer heat a snake (Vanilla Animals Expanded's rattlesnake on dry land, else the game's cobra) lies coiled among
    /// the logs of the colony's woodpile, in their shade. A is the one who nearly put a hand on it, B the best handler.
    /// </summary>
    public class HappeningWorker_R3SnakeWoodpile : IncidentWorker_Happening
    {
        public static PawnKindDef Snake(Map map) =>
            Lands.Of(map).Contains("desert") ? Round3Map.Kind(map, "AEXP_Rattlesnake", "Cobra") : Round3Map.Kind(map, "Cobra", "AEXP_Rattlesnake");

        /// <summary>The colony's woodpile: its biggest stack of logs on its home ground, with nobody on it.</summary>
        public static Thing Pile(Map map) =>
            map.listerThings.ThingsOfDef(ThingDefOf.WoodLog)
                .Where(t => t.Spawned && t.stackCount >= Round3EventsMath.PileFrom && map.areaManager.Home[t.Position] && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer)
                            && t.Position.GetFirstPawn(map) == null)
                .OrderByDescending(t => t.stackCount).FirstOrDefault();

        public static Pawn Handler(Map map, Pawn not) =>
            VarietyThree.Best(StorytellerEvents.Adults(map), SkillDefOf.Animals, WorkTypeDefOf.Handling, not) ?? VarietyThree.Best(StorytellerEvents.Adults(map), SkillDefOf.Animals);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && !Round3Map.Kept(map, TimedKind.R3Snake) && map.mapTemperature.OutdoorTemp >= Round3EventsMath.SnakeHeatFrom
            && Snake(map) != null && Pile(map) != null && Handler(map, null) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.place = Pile(map);
            if (ctx.place == null) return false;
            ctx.spot = ctx.place.Position;
            ctx.a = StorytellerEvents.Adults(map).Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = Handler(map, ctx.a) ?? ctx.a;
            var kind = Snake(map);
            if (kind == null) return false;
            ctx.beast = kind.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var x = Round3Map.Spawn(Snake(map), ctx.spot, map);
            if (x == null) return false;
            ctx.stranger = x;
            ctx.things.Add(x);
            Round2Map.Hold(x, 6f);
            var k = Round2Map.Keep(TimedKind.R3Snake, ctx, ctx.a, x);
            if (k == null) return false;
            k.things.Add(ctx.place);
            Round3Map.Sound(map, ctx.spot, "HissSmall");
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var t in ctx.things.OfType<Pawn>().Where(p => p.Spawned)) t.Destroy(DestroyMode.Vanish);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger?.kindDef?.defName == "AEXP_Rattlesnake") tags.Add("rattle");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>lift</c>: B lifts the snake out on a long stick and walks it off ahead of them past the edge of the colony, where it is
    /// let go (a small chance of a bite on the hand or arm, through the shared budget); <c>kill</c>: marked for the hunters
    /// (the game's own revenge chance); <c>leave</c>: the logs there forbidden until dusk, when it moves off by itself.
    /// </summary>
    public class DilemmaEffect_R3Snake : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        private static bool Here(Pawn x, Map map) => x != null && !x.Dead && x.Spawned && x.Map == map && x.Faction == null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x != null && !Here(x, ctx.map)) return "CC_DlmE_BeastGone".Translate(x.LabelShort).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (!Here(x, ctx?.map)) return;
            string who = (ctx.b ?? ctx.a)?.LabelShort ?? "";
            switch (mode)
            {
                case "lift":
                    sheet.Play("CC_R3_Impact_Lift".Translate(who, x.LabelShort).Resolve(), "CC_R3_Impact_LiftBrief".Translate(x.LabelShort).Resolve());
                    if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_R3_Impact_LiftRisk".Translate(x.LabelShort, who).Resolve(), "CC_R3_Impact_LiftRiskBrief".Translate().Resolve(), "r3bite");
                    break;
                case "kill":
                    sheet.Play("CC_Impact_MarkHunt".Translate(x.LabelShort).Resolve(), "CC_Impact_MarkHuntBrief".Translate(x.LabelShort).Resolve());
                    if (Round3Map.Revenge(new[] { x }, ctx.b ?? ctx.a) >= 0.01f)
                        sheet.Add(ImpactKind.Health, -1, "CC_R3_Impact_SnakeRisk".Translate(x.LabelShort).Resolve(), "CC_R3_Impact_HuntRiskBrief".Translate().Resolve(), "r3huntrisk");
                    break;
                default:
                    sheet.Play("CC_R3_Impact_LeaveSnake".Translate(x.LabelShort).Resolve(), "CC_R3_Impact_LeaveSnakeBrief".Translate().Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Round2Map.Of(TimedKind.R3Snake, ctx);
            var x = ctx?.stranger;
            if (k == null || !Here(x, ctx.map)) return;
            if (k.animal == null) k.animal = x;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.pawns.Clear();
            k.on = false;
            switch (mode)
            {
                case "lift":
                    var b = ctx.b != null && !ctx.b.Dead && ctx.b.Spawned ? ctx.b : HappeningWorker_R3SnakeWoodpile.Handler(ctx.map, null);
                    if (b == null)
                    {
                        Round3Snake.Shut(k);
                        return;
                    }
                    k.pawns.Add(b);
                    k.count = 1;
                    k.until = now + Round3Map.Ticks(6f);
                    break;
                case "kill":
                    try
                    {
                        var dm = x.Map.designationManager;
                        dm.RemoveAllDesignationsOn(x);
                        dm.AddDesignation(new Designation(x, DesignationDefOf.Hunt));
                        Round2Map.Told(ctx.map, "CC_DlmE_MarkedHunt".Translate(x.Named("X")).Resolve(), new LookTargets(x));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("round 3: the snake marked", e);
                    }
                    k.count = 2;
                    break;
                default:
                    Round3Snake.Shut(k);
                    break;
            }
        }
    }

    /// <summary>The snake in the woodpile (TimedKind.R3Snake): 0 waiting, 1 lifted out on a stick and walked off, 2 hunted, 3 left until dusk.</summary>
    public static class Round3Snake
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The logs there forbidden until the snake has gone at dusk.</summary>
        public static void Shut(TimedThing k)
        {
            var map = k.map;
            var x = k.animal;
            var at = x != null && x.Spawned ? x.Position : k.cell;
            foreach (var t in GenRadial.RadialDistinctThingsAround(at, map, 2.5f, true).Where(t => t.def == ThingDefOf.WoodLog && !t.IsForbidden(Faction.OfPlayer)).ToList())
            {
                t.SetForbidden(true, false);
                if (!k.things.Contains(t)) k.things.Add(t);
            }
            k.count = 3;
            k.until = Now + Round3Map.Ticks(30f);
        }

        private static void Open(TimedThing k)
        {
            foreach (var t in k.things.Where(t => t != null && t.Spawned && t.def == ThingDefOf.WoodLog)) t.SetForbidden(false, false);
        }

        /// <summary>Out past the colony's edge, on the far side from its middle: where the snake is let go.</summary>
        public static IntVec3 Away(Map map, IntVec3 from, Pawn x)
        {
            var mid = CampSite.ColonyCenter(map);
            var dir = (from - mid).ToVector3();
            if (dir.sqrMagnitude < 1f) dir = new Vector3(1f, 0f, 0f);
            dir.Normalize();
            var guess = (mid.ToVector3() + dir * (Round3EventsMath.LeadFar + 6f)).ToIntVec3().ClampInsideMap(map);
            if (CellFinder.TryFindRandomCellNear(guess, map, 10, c => c.Standable(map) && !map.areaManager.Home[c] && c.DistanceTo(mid) >= Round3EventsMath.LeadFar
                                                                       && (x == null || x.CanReach(c, PathEndMode.OnCell, Danger.Some)), out var cell))
                return cell;
            return IntVec3.Invalid;
        }

        public static bool Step(TimedThing k, int now)
        {
            var x = k.animal;
            var map = k.map;
            if (x == null || x.Dead || !x.Spawned || x.Map != map || x.Faction != null) return true;
            switch (k.count)
            {
                case 0:
                    if (!x.Downed && x.CurJobDef != JobDefOf.Wait_MaintainPosture) Round2Map.Hold(x, 4f);
                    return now > k.until;
                case 1:
                    return Lift(k, x, map, now);
                case 2:
                    return true;
                default:
                    if (Round3EventsMath.SnakeGoes(GenLocalDate.HourOfDay(map)) && Round3Map.HoursSince(k.start) >= 1f)
                    {
                        Open(k);
                        Round2Map.Leave(x);
                        if (x.mindState != null) x.mindState.exitMapAfterTick = now + GenDate.TicksPerHour;
                        Round2Map.Told(map, "CC_R3_Snake_Dusk".Translate(x.LabelShort.Named("X")).Resolve(), new LookTargets(x));
                        k.on = true;
                        return true;
                    }
                    if (!x.Downed && x.CurJobDef != JobDefOf.Wait_MaintainPosture) Round2Map.Hold(x, 4f);
                    return now > k.until;
            }
        }

        /// <summary>The handler goes to it, lifts it out on the stick and walks it off ahead of them; past the edge it is let go.</summary>
        private static bool Lift(TimedThing k, Pawn x, Map map, int now)
        {
            var h = k.pawns.FirstOrDefault();
            if (!Round2Map.Here(h, map) || h.Drafted || h.InMentalState)
            {
                // Nobody to lift it: the woodpile is left alone until dusk instead.
                Shut(k);
                return false;
            }
            if (now > k.until)
            {
                Round2Map.Leave(x);
                return true;
            }
            if (!k.on)
            {
                if (h.Position.InHorDistOf(x.Position, 1.6f))
                {
                    var to = Away(map, x.Position, x);
                    if (!to.IsValid)
                    {
                        Shut(k);
                        return false;
                    }
                    k.on = true;
                    k.cells.Clear();
                    k.cells.Add(to);
                    MapDangers.Say(k, h, "lift");
                    if (Harm.Roll(h, HarmKinds.Dangers, Round3EventsMath.BiteChance))
                        MapDangers.Wound(h, HurtKind.Bite, Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm, Round3EventsMath.BiteLow, Round3EventsMath.BiteHigh, "CC_R3_Snake_Bite");
                    Round2Map.GoTo(x, to);
                    return false;
                }
                if (!x.Downed && x.CurJobDef != JobDefOf.Wait_MaintainPosture) Round2Map.Hold(x, 2f);
                if (!(h.CurJobDef == JobDefOf.Goto && h.CurJob.targetA.Cell.InHorDistOf(x.Position, 1.6f)))
                {
                    var c = Round3Map.Beside(map, x.Position, h);
                    if (c.IsValid) Round2Map.GoTo(h, c);
                }
                return false;
            }
            var target = k.cells.FirstOrDefault();
            if (!target.IsValid || x.Position.InHorDistOf(target, 2.5f))
            {
                Round2Map.Leave(x);
                if (x.mindState != null) x.mindState.exitMapAfterTick = now + GenDate.TicksPerHour;
                Round2Map.Told(map, "CC_R3_Snake_LetGo".Translate(h.LabelShort.Named("PAWN"), x.LabelShort.Named("X")).Resolve(), new LookTargets(x));
                Round2Map.Give(h, "Rimfolk_R3_SnakeLifted");
                Round2Map.GoTo(h, Round2Map.Home(map, h));
                return true;
            }
            if (x.CurJobDef != JobDefOf.Goto) Round2Map.GoTo(x, target);
            // Close behind it with the stick.
            if (!h.Position.InHorDistOf(x.Position, 2.5f) && !(h.CurJobDef == JobDefOf.Goto && h.CurJob.targetA.Cell.InHorDistOf(x.Position, 2f)))
            {
                var c = Round3Map.Beside(map, x.Position, h);
                if (c.IsValid) Round2Map.GoTo(h, c);
            }
            return false;
        }

        public static void End(TimedThing k)
        {
            if (k.count == 3) Open(k);
        }
    }

    // ===================================================================================================
    // R3. Snow on the roof
    // ===================================================================================================

    /// <summary>
    /// In winter, with deep snow lying round one of the colony's big rooms under a built roof, the roof creaks and dust falls
    /// inside it. A is the one inside, B the colony's best builder. {TALE}: what the room is.
    /// </summary>
    public class HappeningWorker_R3RoofSnow : IncidentWorker_Happening
    {
        /// <summary>Every cell of the room under a built roof (not rock).</summary>
        public static bool Built(Room r, Map map)
        {
            foreach (var c in r.Cells)
            {
                var roof = map.roofGrid.RoofAt(c);
                if (roof == null || roof.isThickRoof) return false;
            }
            return true;
        }

        /// <summary>How deep the snow lies on the open ground round the room (of the game's 0 to 1).</summary>
        public static float SnowRound(Room r, Map map)
        {
            var rect = r.ExtentsClose.ExpandedBy(3).ClipInsideMap(map);
            float sum = 0f;
            int n = 0;
            foreach (var c in rect)
            {
                if (map.roofGrid.Roofed(c) || c.GetEdifice(map) != null) continue;
                sum += map.snowGrid.GetDepth(c);
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        public static bool Heavy(Room r, Map map) =>
            r != null && !r.PsychologicallyOutdoors && r.ProperRoom && r.CellCount <= 400 && Round3EventsMath.RoofHeavy(Built(r, map), r.CellCount, SnowRound(r, map));

        /// <summary>A room heavy with snow with someone of the colony awake inside it.</summary>
        public static Room FindRoom(Map map, out Pawn inside)
        {
            inside = null;
            foreach (var p in StorytellerEvents.Adults(map).Where(p => p.Awake() && !p.Position.Fogged(map)).InRandomOrder())
            {
                var r = p.GetRoom();
                if (r == null || r.CellCount < Round3EventsMath.RoomCellsFrom || !Heavy(r, map)) continue;
                inside = p;
                return r;
            }
            return null;
        }

        public static string Label(Room r) => r?.Role != null && r.Role != RoomRoleDefOf.None ? r.Role.label : "CC_R3_Roof_Room".Translate().ToString();

        /// <summary>The room's middle: its standable cell nearest the centre of its walls.</summary>
        public static IntVec3 Middle(Room r, Map map)
        {
            var mid = r.ExtentsClose.CenterCell;
            return r.Cells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(mid)).FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && !Round3Map.Kept(map, TimedKind.R3Roof) && FindRoom(map, out _) != null && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var r = FindRoom(map, out var inside);
            if (r == null || inside == null) return false;
            ctx.a = inside;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.b == null) return false;
            ctx.spot = Middle(r, map);
            if (!ctx.spot.IsValid) return false;
            ctx.tale = Label(r);
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.R3Roof, ctx, ctx.a, null);
            if (k == null) return false;
            k.text = ctx.tale;
            Round3Roof.Creak(k);
            MapDangers.Say(k, ctx.a, "creak");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>shovel</c>: B and two more work along the room's outside walls for about an hour, throwing the snow down from the roof;
    /// <c>prop</c>: a column marked in the middle of the room for the builders; <c>leave</c>: if the snow still lies deep later
    /// that day, the roof comes down over a few cells nobody stands under (the game's own roof collapse), leaving a hole.
    /// </summary>
    public class DilemmaEffect_R3Roof : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        public static ThingDef Column => DefDatabase<ThingDef>.GetNamedSilentFail("Column");

        private static Room RoomOf(DilemmaContext ctx) => ctx is HappeningContext h && h.map != null && h.spot.IsValid ? h.spot.GetRoom(h.map) : null;

        /// <summary>Where a column can stand: a free cell near the room's middle, off its walls and doors.</summary>
        public static IntVec3 PropCell(Room r, Map map, ThingDef stuff)
        {
            var col = Column;
            if (r == null || col == null) return IntVec3.Invalid;
            var mid = r.ExtentsClose.CenterCell;
            foreach (var c in r.Cells.OrderBy(c => c.DistanceToSquared(mid)).Take(40))
            {
                if (c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null) continue;
                if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetDoor(map) != null)) continue;
                if (GenConstruct.CanPlaceBlueprintAt(col, c, Rot4.North, map, false, null, null, stuff).Accepted) return c;
            }
            return IntVec3.Invalid;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var r = RoomOf(ctx);
            if (r == null || r.PsychologicallyOutdoors) return "CC_R3_Roof_Gone".Translate().Resolve();
            if (mode == "prop" && !PropCell(r, ctx.map, Round3Map.Stuff(ctx.map, Column)).IsValid) return "CC_R3_Roof_NoProp".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = RoomOf(ctx);
            if (r == null || !(ctx is HappeningContext h)) return;
            string room = h.tale ?? HappeningWorker_R3RoofSnow.Label(r);
            switch (mode)
            {
                case "shovel":
                    sheet.Play("CC_R3_Impact_Shovel".Translate((ctx.b ?? ctx.a)?.LabelShort ?? "", room).Resolve(), "CC_R3_Impact_ShovelBrief".Translate().Resolve());
                    break;
                case "prop":
                    var stuff = Round3Map.Stuff(ctx.map, Column);
                    if (stuff == null) return;
                    sheet.Goods(-1, stuff, Math.Max(1, Column.costStuffCount));
                    sheet.Play("CC_R3_Impact_Prop".Translate(stuff.label, room).Resolve(), "CC_R3_Impact_PropBrief".Translate().Resolve());
                    break;
                default:
                    sheet.Add(ImpactKind.Other, -1, "CC_R3_Impact_RoofLeave".Translate(room).Resolve(), "CC_R3_Impact_RoofLeaveBrief".Translate().Resolve(), "r3roof");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Round2Map.Of(TimedKind.R3Roof, ctx);
            var r = RoomOf(ctx);
            if (k == null || r == null) return;
            var map = ctx.map;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.done = 0;
            k.pawns.Clear();
            k.cells.Clear();
            if (k.text.NullOrEmpty()) k.text = HappeningWorker_R3RoofSnow.Label(r);
            switch (mode)
            {
                case "shovel":
                    var first = ctx.b != null && Round2Map.Free(ctx.b) ? ctx.b : Round3Map.Another(map, k.cell);
                    if (first != null) k.pawns.Add(first);
                    while (k.pawns.Count < Round3EventsMath.Shovellers)
                    {
                        var more = Round3Map.Another(map, k.cell, k.pawns.ToArray());
                        if (more == null) break;
                        k.pawns.Add(more);
                    }
                    k.cells.AddRange(Round3Roof.WallSpots(r, map, Math.Max(1, k.pawns.Count)));
                    if (k.pawns.Count == 0 || k.cells.Count == 0)
                    {
                        Leave(k, now);
                        return;
                    }
                    k.count = 1;
                    k.until = now + Round3Map.Ticks(8f);
                    break;
                case "prop":
                    var stuff = Round3Map.Stuff(map, Column);
                    var cell = PropCell(r, map, stuff);
                    if (!cell.IsValid || stuff == null)
                    {
                        Leave(k, now);
                        return;
                    }
                    GenConstruct.PlaceBlueprintForBuild(Column, cell, map, Rot4.North, Faction.OfPlayer, stuff);
                    k.cells.Add(cell);
                    k.count = 2;
                    k.until = now + GenDate.TicksPerDay * 2;
                    Round2Map.Told(map, "CC_R3_Roof_PropMarked".Translate(stuff.label, k.text).Resolve(), new LookTargets(cell, map));
                    break;
                default:
                    Leave(k, now);
                    break;
            }
        }

        private static void Leave(TimedThing k, int now)
        {
            k.count = 3;
            k.nights = now + Round3Map.Ticks(Rand.Range(Round3EventsMath.CollapseAfterMin, Round3EventsMath.CollapseAfterMax));
            k.until = k.nights + Round3Map.Ticks(2f);
        }
    }

    /// <summary>The roof under the snow (TimedKind.R3Roof): 0 waiting, 1 shovelled off, 2 propped, 3 left (it may come down over a few cells).</summary>
    public static class Round3Roof
    {
        /// <summary>A creak: dust falling from the roof inside, and the sound of it.</summary>
        public static void Creak(TimedThing k)
        {
            var map = k.map;
            var r = k.cell.IsValid ? k.cell.GetRoom(map) : null;
            if (r == null) return;
            foreach (var c in r.Cells.InRandomOrder().Take(2)) FleckMaker.ThrowDustPuff(c, map, 0.8f);
            Round3Map.Sound(map, k.cell, "Roof_Start");
        }

        /// <summary>Open ground along the outside of the room's walls, spread round it, for <paramref name="n"/> to stand at.</summary>
        public static List<IntVec3> WallSpots(Room r, Map map, int n)
        {
            var rect = r.ExtentsClose.ExpandedBy(2).ClipInsideMap(map);
            var ring = rect.EdgeCells.Where(c => c.InBounds(map) && c.Standable(map) && !map.roofGrid.Roofed(c) && c.GetRoom(map) != r).ToList();
            var list = new List<IntVec3>();
            if (ring.Count == 0) return list;
            int stepBy = Math.Max(1, ring.Count / Math.Max(1, n));
            for (int i = 0; i < ring.Count && list.Count < n; i += stepBy) list.Add(ring[i]);
            return list;
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            var r = k.cell.IsValid ? k.cell.GetRoom(map) : null;
            if (r == null || r.PsychologicallyOutdoors) return true;
            switch (k.count)
            {
                case 0:
                    if (Rand.Chance(0.06f)) Creak(k);
                    return now > k.until;
                case 1:
                    return Shovel(k, map, now);
                case 2:
                    var c = k.cells.FirstOrDefault();
                    if (!c.IsValid) return true;
                    if (c.GetEdifice(map)?.def == DilemmaEffect_R3Roof.Column)
                    {
                        Round2Map.Told(map, "CC_R3_Roof_Propped".Translate(k.text ?? "").Resolve(), new LookTargets(c, map));
                        return true;
                    }
                    // The blueprint taken off by hand: nothing more is kept.
                    if (!c.GetThingList(map).Any(t => t is Blueprint || t is Frame)) return true;
                    return now > k.until;
                default:
                    if (now < k.nights)
                    {
                        if (Rand.Chance(0.04f)) Creak(k);
                        return false;
                    }
                    Fall(k, r, map);
                    return true;
            }
        }

        private static bool Shovel(TimedThing k, Map map, int now)
        {
            int at = 0;
            for (int i = 0; i < k.pawns.Count; i++)
            {
                var p = k.pawns[i];
                if (!Round2Map.Here(p, map) || p.Drafted || p.InMentalState || PlayOuts.NeedsPress(p)) continue;
                var spot = k.cells[i % k.cells.Count];
                if (!p.Position.InHorDistOf(spot, 1.5f))
                {
                    if (!(p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell == spot)) Round2Map.GoTo(p, spot);
                    continue;
                }
                at++;
                Round2Map.Hold(p, 0.5f);
                // The snow thrown down off the roof: puffs of it, and the snow heaped against the wall.
                FleckMaker.ThrowAirPuffUp(p.DrawPos + new Vector3(0f, 0f, 0.6f), map);
                foreach (var n in GenAdj.CardinalDirections.Select(d => spot + d).Where(n => n.InBounds(map) && !map.roofGrid.Roofed(n)))
                    map.snowGrid.AddDepth(n, 0.03f);
                if (Rand.Chance(0.08f)) MapDangers.Say(k, p, "shovel");
            }
            k.done += Sched.RareTicks * at;
            if (k.done >= Round3EventsMath.ShovelHours * GenDate.TicksPerHour * Math.Max(1, Math.Min(k.pawns.Count, Round3EventsMath.Shovellers)))
            {
                Round2Map.Told(map, "CC_R3_Roof_Shovelled".Translate(k.text ?? "").Resolve(), new LookTargets(k.cell, map), MessageTypeDefOf.PositiveEvent);
                foreach (var p in k.pawns.Where(p => p != null && !p.Dead && p.Spawned)) Round2Map.GoTo(p, Round2Map.Home(map, p));
                return true;
            }
            return now > k.until;
        }

        /// <summary>Left with the snow still deep: the roof comes down over a few cells where nobody and nothing is (the game's own collapse).</summary>
        private static void Fall(TimedThing k, Room r, Map map)
        {
            bool winter = GenLocalDate.Season(map) == Season.Winter || GenLocalDate.Season(map) == Season.PermanentWinter;
            if (!winter || HappeningWorker_R3RoofSnow.SnowRound(r, map) < Round3EventsMath.DeepSnow * 0.8f)
            {
                Round2Map.Told(map, "CC_R3_Roof_Held".Translate(k.text ?? "").Resolve(), new LookTargets(k.cell, map));
                return;
            }
            var mid = r.ExtentsClose.CenterCell;
            var cells = r.Cells.Where(c => map.roofGrid.RoofAt(c) is RoofDef roof && !roof.isThickRoof && c.GetFirstPawn(map) == null && c.GetThingList(map).All(t => t is Filth))
                .OrderBy(c => c.DistanceToSquared(mid)).Take(Round3EventsMath.CollapseMost).ToList();
            if (cells.Count == 0)
            {
                Round2Map.Told(map, "CC_R3_Roof_Held".Translate(k.text ?? "").Resolve(), new LookTargets(k.cell, map));
                return;
            }
            try
            {
                RoofCollapserImmediate.DropRoofInCells(cells, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: the roof under the snow", e);
                return;
            }
            Round2Map.Told(map, "CC_R3_Roof_Fell".Translate(k.text ?? "", cells.Count).Resolve(), new LookTargets(cells[0], map), MessageTypeDefOf.NegativeEvent);
        }
    }

    // ===================================================================================================
    // R4. The way across the marsh
    // ===================================================================================================

    /// <summary>
    /// The straight way from the colony to its biggest field (else its fishing ground) crosses wet ground, and whoever comes
    /// back from it is muddy to the knees. A is the one who came back (the best grower), B the colony's best builder.
    /// {TALE}: the field; {COUNT}: the wet cells on the way.
    /// </summary>
    public class HappeningWorker_R3MarshPath : IncidentWorker_Happening
    {
        public static TerrainDef Bridge => DefDatabase<TerrainDef>.GetNamedSilentFail("Bridge");

        /// <summary>Marsh and mud (the game's, and any mod's of those names), not water.</summary>
        public static bool Wet(TerrainDef t) =>
            t != null && !t.IsWater && (t.defName.IndexOf("Marsh", StringComparison.Ordinal) >= 0 || t.defName.IndexOf("Mud", StringComparison.Ordinal) >= 0);

        public static Zone Field(Map map) =>
            (Zone)map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count >= 9).OrderByDescending(z => z.Cells.Count).FirstOrDefault() ?? HappeningWorker_FishRunning.Ground(map);

        /// <summary>The wet cells on the straight way from the colony's middle to the field's nearest cell, where a bridge can go.</summary>
        public static List<IntVec3> Way(Map map, out Zone field)
        {
            var list = new List<IntVec3>();
            field = Field(map);
            var bridge = Bridge;
            if (field == null || bridge == null || field.Cells.Count == 0) return list;
            var from = CampSite.ColonyCenter(map);
            var to = field.Cells.OrderBy(c => c.DistanceToSquared(from)).First();
            foreach (var c in GenSight.PointsOnLineOfSight(from, to))
            {
                if (!c.InBounds(map) || c.Fogged(map) || !Wet(c.GetTerrain(map))) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(bridge, c, Rot4.North, map).Accepted) continue;
                list.Add(c);
            }
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Round3EventsMath.PathFits(Way(map, out _).Count, map.mapTemperature.OutdoorTemp) && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var way = Way(map, out var field);
            if (way.Count < Round3EventsMath.WetFrom || field == null) return false;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = field is Zone_Growing ? VarietyThree.Best(adults, SkillDefOf.Plants, WorkTypeDefOf.Growing) : adults.OrderBy(p => p.Position.DistanceToSquared(way[0])).FirstOrDefault();
            ctx.a ??= adults.FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.b == null) return false;
            ctx.spot = way[way.Count / 2];
            ctx.count = way.Count;
            ctx.tale = field.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // Muddy to the knees: mud where they stand, and a word about it.
            if (ctx.a != null && ctx.a.Spawned) FilthMaker.TryMakeFilth(ctx.a.Position, ctx.map, ThingDefOf.Filth_Dirt, 2);
            Round3Map.Say(ctx.incident?.defName, ctx.a, "mud");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map != null && !(Field(ctx.map) is Zone_Growing)) tags.Add("fishing");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>all</c>: a bridge (the game's own, of planks) marked over every wet cell on the way to the field, for the builders;
    /// <c>worst</c>: only over the wettest half of them. The building is the answer's own work party.
    /// </summary>
    public class DilemmaEffect_R3MarshPath : DilemmaEffect
    {
        public string mode = "all";

        public override bool ChanceEach => false;

        private List<IntVec3> Cells(DilemmaContext ctx)
        {
            if (ctx?.map == null) return new List<IntVec3>();
            var way = HappeningWorker_R3MarshPath.Way(ctx.map, out _);
            if (mode != "worst") return way;
            var map = ctx.map;
            var keep = new HashSet<IntVec3>(way.OrderByDescending(c => c.GetTerrain(map).pathCost).Take(Round3EventsMath.Worst(way.Count)));
            return way.Where(keep.Contains).ToList();
        }

        private static ThingDefCountClass Cost => HappeningWorker_R3MarshPath.Bridge?.costList?.FirstOrDefault();

        public override string Unavailable(DilemmaContext ctx) => Cells(ctx).Count == 0 ? "CC_R3_Marsh_Gone".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var cells = Cells(ctx);
            if (cells.Count == 0) return;
            var cost = Cost;
            if (cost != null) sheet.Goods(-1, cost.thingDef, cost.count * cells.Count);
            sheet.Play(("CC_R3_Impact_Marsh_" + mode).Translate(cells.Count, (ctx as HappeningContext)?.tale ?? "").Resolve(), ("CC_R3_Impact_Marsh_" + mode + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var bridge = HappeningWorker_R3MarshPath.Bridge;
            var map = ctx?.map;
            if (bridge == null || map == null) return;
            int n = 0;
            IntVec3 first = IntVec3.Invalid;
            foreach (var c in Cells(ctx))
            {
                try
                {
                    GenConstruct.PlaceBlueprintForBuild(bridge, c, map, Rot4.North, Faction.OfPlayer, null);
                    if (!first.IsValid) first = c;
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 3: the way across the marsh", e);
                    break;
                }
            }
            if (n > 0) Round2Map.Told(map, "CC_R3_Marsh_Marked".Translate(n, (ctx as HappeningContext)?.tale ?? "").Resolve(), new LookTargets(first, map));
        }
    }

    // ===================================================================================================
    // R5. The cold room under the rock
    // ===================================================================================================

    /// <summary>
    /// In a warm summer the colony's food lies in a warm store, and a room dug under the mountain stays cold. A is the cook,
    /// who went down into it and felt the cold; B one who would carry the food down. {COUNT}: the free cells of its floor.
    /// </summary>
    public class HappeningWorker_R3ColdRoom : IncidentWorker_Happening
    {
        private static bool Food(Thing t) =>
            t.def.IsNutritionGivingIngestible && t.def.ingestible != null && t.def.ingestible.HumanEdible && t.TryGetComp<CompRottable>() != null && t.IsInAnyStorage();

        /// <summary>The room the most of the colony's stored food lies in.</summary>
        public static Room Store(Map map)
        {
            return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(t => t.Spawned && Food(t)).Select(t => t.GetRoom()).Where(r => r != null)
                .GroupBy(r => r).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        }

        /// <summary>Food already kept in the frost somewhere (a freezer): nothing to ask.</summary>
        private static bool Freezer(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Any(t => t.Spawned && Food(t) && t.GetRoom() is Room r && !r.PsychologicallyOutdoors && r.Temperature < 0f);

        /// <summary>A room's floor free for a store: standable, nothing built, no zone, nothing lying there.</summary>
        public static List<IntVec3> Free(Room r, Map map) =>
            r.Cells.Where(c => c.Standable(map) && c.GetEdifice(map) == null && c.GetZone(map) == null && c.GetFirstItem(map) == null && !c.Fogged(map)).ToList();

        /// <summary>A room under thick rock, kept for nothing in particular (no beds, no benches), colder than the store.</summary>
        public static Room Deep(Map map, Room store)
        {
            if (store == null) return null;
            foreach (var r in map.regionGrid.AllRooms)
            {
                if (r == store || r.PsychologicallyOutdoors || !r.ProperRoom || r.CellCount > 300 || r.CellCount < Round3EventsMath.StoreCellsFrom) continue;
                if (r.Cells.Any(c => !(map.roofGrid.RoofAt(c)?.isThickRoof ?? false))) continue;
                if (r.ContainedBeds.Any() || r.ContainedAndAdjacentThings.Any(t => t is Building_WorkTable && t.Faction == Faction.OfPlayer)) continue;
                if (!Round3EventsMath.ColdRoomFits(store.Temperature, r.Temperature, Free(r, map).Count)) continue;
                return r;
            }
            return null;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || Freezer(map)) return false;
            return Deep(map, Store(map)) != null && VarietyThree.Best(StorytellerEvents.Adults(map), SkillDefOf.Cooking, VarietyThree.Work("Cooking")) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var deep = Deep(map, Store(map));
            if (deep == null) return false;
            var adults = StorytellerEvents.Adults(map).ToList();
            ctx.a = VarietyThree.Best(adults, SkillDefOf.Cooking, VarietyThree.Work("Cooking"));
            if (ctx.a == null) return false;
            ctx.b = VarietyThree.Other(adults, ctx.a, null) ?? ctx.a;
            ctx.spot = Free(deep, map).OrderBy(c => c.DistanceToSquared(deep.ExtentsClose.CenterCell)).FirstOrDefault();
            if (!ctx.spot.IsValid) return false;
            ctx.count = Free(deep, map).Count;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // The cook goes down into the cold room and says so.
            if (ctx.a != null && Round2Map.Free(ctx.a)) Round2Map.GoTo(ctx.a, Round2Map.Near(ctx.map, ctx.spot, 2f, ctx.a));
            Round3Map.Say(ctx.incident?.defName, ctx.a, "cold");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>all</c>: a stockpile for food marked over the cold room's free floor, preferred to the others, so the haulers carry the
    /// food down (the answer's work party, the game's own hauling); <c>meat</c>: the same for raw meat and fish only.
    /// </summary>
    public class DilemmaEffect_R3ColdRoom : DilemmaEffect
    {
        public string mode = "all";

        public override bool ChanceEach => false;

        private static List<IntVec3> Floor(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null || !h.spot.IsValid) return new List<IntVec3>();
            var r = h.spot.GetRoom(h.map);
            return r == null || r.PsychologicallyOutdoors ? new List<IntVec3>() : HappeningWorker_R3ColdRoom.Free(r, h.map);
        }

        public override string Unavailable(DilemmaContext ctx) => Floor(ctx).Count < Round3EventsMath.StoreCellsFrom ? "CC_R3_Cold_Gone".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var cells = Floor(ctx);
            if (cells.Count == 0) return;
            sheet.Play(("CC_R3_Impact_Cold_" + mode).Translate(cells.Count, (ctx as HappeningContext)?.placeName ?? "").Resolve(), ("CC_R3_Impact_Cold_" + mode + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var cells = Floor(ctx);
            var map = ctx?.map;
            if (map == null || cells.Count == 0) return;
            try
            {
                var z = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
                map.zoneManager.RegisterZone(z);
                foreach (var c in cells) z.AddCell(c);
                z.label = "CC_R3_Cold_Zone".Translate().Resolve();
                z.settings.filter.SetDisallowAll();
                if (mode == "meat")
                {
                    z.settings.filter.SetAllow(ThingCategoryDefOf.MeatRaw, true);
                    var fish = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Fish");
                    if (fish != null) z.settings.filter.SetAllow(fish, true);
                }
                else z.settings.filter.SetAllow(ThingCategoryDefOf.Foods, true);
                z.settings.Priority = StoragePriority.Important;
                Round2Map.Told(map, ("CC_R3_Cold_Marked_" + mode).Translate(cells.Count).Resolve(), new LookTargets(cells[0], map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3: the cold store", e);
            }
        }
    }

    // ===================================================================================================
    // R7. Dinner in the field
    // ===================================================================================================

    /// <summary>
    /// In the late morning of summer or autumn two or more of the colony are at work in a field far from the dining table; the
    /// cook (A) offers to carry their dinner out. B is one of those in the field. {TALE}: the field; {COUNT}: those in it.
    /// </summary>
    public class HappeningWorker_R3FieldDinner : IncidentWorker_Happening
    {
        private static bool FieldJob(Pawn p) =>
            p.CurJobDef == JobDefOf.Sow || p.CurJobDef == JobDefOf.Harvest || p.CurJobDef == JobDefOf.CutPlant || p.CurJobDef == JobDefOf.HarvestDesignated;

        /// <summary>The field with the most of the colony at work in it now, and who.</summary>
        public static Zone_Growing Field(Map map, out List<Pawn> workers)
        {
            workers = new List<Pawn>();
            Zone_Growing best = null;
            var at = StorytellerEvents.Adults(map).Where(p => FieldJob(p) && p.CurJob.targetA.IsValid).ToList();
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                var here = at.Where(p => z.ContainsCell(p.CurJob.targetA.Cell)).ToList();
                if (here.Count <= workers.Count) continue;
                best = z;
                workers = here;
            }
            workers = workers.Take(Round3EventsMath.WorkersMost).ToList();
            return best;
        }

        public static Thing Table(Map map, IntVec3 near) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.def.surfaceType == SurfaceType.Eat && b.Spawned).OrderBy(b => b.Position.DistanceToSquared(near)).FirstOrDefault();

        /// <summary>Meals in the colony's stores, free to take.</summary>
        public static List<Thing> Meals(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.ingestible != null && t.def.ingestible.HumanEdible && t.def.ingestible.preferability >= FoodPreferability.MealSimple
                            && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer))
                .ToList();

        public static int MealCount(Map map) => Meals(map).Sum(t => t.stackCount);

        /// <summary>The field's edge nearest the colony, where dinner is set down.</summary>
        public static IntVec3 Edge(Zone_Growing z, Map map)
        {
            var mid = CampSite.ColonyCenter(map);
            return z.Cells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(mid)).FirstOrDefault();
        }

        private static Pawn Cook(Map map, List<Pawn> not) =>
            VarietyThree.Best(StorytellerEvents.Adults(map).Where(p => !not.Contains(p) && Round2Map.Free(p)), SkillDefOf.Cooking, VarietyThree.Work("Cooking"));

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map)) return false;
            var z = Field(map, out var workers);
            if (z == null) return false;
            var edge = Edge(z, map);
            var table = edge.IsValid ? Table(map, edge) : null;
            return table != null && Round3EventsMath.DinnerFits(GenLocalDate.HourOfDay(map), workers.Count, table.Position.DistanceTo(edge), MealCount(map)) && Cook(map, workers) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = Field(map, out var workers);
            if (z == null || workers.Count < Round3EventsMath.WorkersFrom) return false;
            ctx.a = Cook(map, workers);
            if (ctx.a == null) return false;
            ctx.b = workers[0];
            ctx.group.Clear();
            ctx.group.AddRange(workers);
            ctx.spot = Edge(z, map);
            if (!ctx.spot.IsValid) return false;
            ctx.count = workers.Count;
            ctx.tale = z.label;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// <c>carry</c>: at midday the cook carries a meal for each out to the field's edge (the game's own carrying), and those at
    /// work come over and eat there together; <c>table</c>: at midday they come in to the table and eat (the game's own eating).
    /// </summary>
    public class DilemmaEffect_R3Dinner : DilemmaEffect
    {
        public string mode = "carry";

        public override bool ChanceEach => false;

        private static List<Pawn> Workers(DilemmaContext ctx) =>
            (ctx as HappeningContext)?.group.Where(p => p != null && !p.Dead && p.Spawned && p.Map == ctx.map).ToList() ?? new List<Pawn>();

        public override string Unavailable(DilemmaContext ctx)
        {
            if (Workers(ctx).Count == 0) return "CC_R3_Dinner_Gone".Translate().Resolve();
            if (HappeningWorker_R3FieldDinner.MealCount(ctx.map) < Workers(ctx).Count) return "CC_R3_Dinner_NoMeals".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var w = Workers(ctx);
            if (w.Count == 0) return;
            string field = (ctx as HappeningContext)?.tale ?? "";
            if (mode == "carry") sheet.Play("CC_R3_Impact_Dinner_carry".Translate(ctx.a?.LabelShort ?? "", w.Count, field).Resolve(), "CC_R3_Impact_Dinner_carryBrief".Translate().Resolve());
            else sheet.Play("CC_R3_Impact_Dinner_table".Translate(w.Count, field).Resolve(), "CC_R3_Impact_Dinner_tableBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            var k = Round2Map.Keep(TimedKind.R3Dinner, h, h.a, null, 12f);
            if (k == null) return;
            k.pawns.Clear();
            k.pawns.AddRange(Workers(ctx));
            k.text = mode;
            k.count = 0;
            k.nights = 0;
            k.until = Find.TickManager.TicksGame + Round3Map.Ticks(10f);
        }
    }

    /// <summary>Dinner in the field (TimedKind.R3Dinner): 0 waiting for midday, 1 carried out, 2 eaten at the field's edge, 3 eaten at the table.</summary>
    public static class Round3Dinner
    {
        private static bool Fit(Pawn p, Map map) => Round2Map.Here(p, map) && !p.Drafted && !p.InMentalState;

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            int hour = GenLocalDate.HourOfDay(map);
            if (MapFacts.ThreatOn(map)) return now > k.until;
            switch (k.count)
            {
                case 0:
                    if (hour > 14) return true;
                    if (hour < Round3EventsMath.DinnerAt) return now > k.until;
                    return Start(k, map, now);
                case 1:
                    return Carried(k, map, now);
                case 2:
                    return Eat(k, map, now);
                default:
                    if (Round3Map.HoursSince(k.start) < 2f) return false;
                    foreach (var p in k.pawns.Where(p => p != null && !p.Dead)) Round2Map.Give(p, "Rimfolk_R3_DinnerAtTable");
                    return true;
            }
        }

        private static bool Start(TimedThing k, Map map, int now)
        {
            k.start = now;
            var meals = HappeningWorker_R3FieldDinner.Meals(map);
            if (k.text == "table")
            {
                // They come in from the field and eat at the table, the game's own eating.
                foreach (var p in k.pawns.Where(p => Fit(p, map) && !PlayOuts.NeedsPress(p)))
                {
                    var meal = meals.Where(t => t.Spawned && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)).OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                    if (meal == null) continue;
                    var job = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                    job.count = 1;
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                var first = k.pawns.FirstOrDefault(p => Fit(p, map));
                if (first != null) MapDangers.Say(k, first, "table");
                k.count = 3;
                return false;
            }
            var cook = k.pawn;
            int n = k.pawns.Count(p => Fit(p, map));
            var stack = cook == null ? null : meals.Where(t => t.stackCount >= n && cook.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)).OrderBy(t => t.Position.DistanceToSquared(cook.Position)).FirstOrDefault();
            if (!Fit(cook, map) || stack == null || n == 0) return true;
            if (!OwnHappenings.Carry(cook, stack, k.cell, n)) return true;
            k.things.Clear();
            k.things.Add(stack);
            MapDangers.Say(k, cook, "carry");
            k.count = 1;
            return false;
        }

        /// <summary>The meals set down at the field's edge (of the kind carried, within a step of it).</summary>
        private static Thing Set(TimedThing k, Map map)
        {
            var def = k.things.FirstOrDefault()?.def;
            if (def == null) return null;
            return GenRadial.RadialDistinctThingsAround(k.cell, map, 2.5f, true).FirstOrDefault(t => t.def == def && t.Spawned && !t.IsInAnyStorage());
        }

        private static bool Carried(TimedThing k, Map map, int now)
        {
            var meal = Set(k, map);
            if (meal != null)
            {
                k.count = 2;
                k.start = now;
                return false;
            }
            return Round3Map.HoursSince(k.start) > 2.5f;
        }

        private static bool Eat(TimedThing k, Map map, int now)
        {
            var meal = Set(k, map);
            var cook = k.pawn;
            if (Fit(cook, map) && cook.Position.InHorDistOf(k.cell, 3f)) Round2Map.Hold(cook, 0.5f);
            for (int i = 0; i < k.pawns.Count; i++)
            {
                var p = k.pawns[i];
                if (!Fit(p, map) || (k.nights & (1 << i)) != 0) continue;
                if (!p.Position.InHorDistOf(k.cell, 3f))
                {
                    if (!(p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell.InHorDistOf(k.cell, 3f))) Round2Map.GoTo(p, Round2Map.Near(map, k.cell, 2.5f, p));
                    continue;
                }
                if (meal == null || !meal.Spawned) continue;
                // A meal eaten sitting at the field's edge with the others.
                var one = meal.stackCount > 1 ? meal.SplitOff(1) : meal;
                float food = one.GetStatValue(StatDefOf.Nutrition);
                if (p.needs?.food != null) p.needs.food.CurLevel = Mathf.Min(p.needs.food.MaxLevel, p.needs.food.CurLevel + food);
                one.def.ingestible?.ingestSound?.PlayOneShot(new TargetInfo(p.Position, map));
                if (!one.Destroyed) one.Destroy(DestroyMode.Vanish);
                Round2Map.Hold(p, Round3EventsMath.EatHours);
                k.nights |= 1 << i;
                if (Rand.Chance(0.3f)) MapDangers.Say(k, p, "eat");
                meal = Set(k, map);
            }
            bool all = k.pawns.Select((p, i) => (p, i)).All(x => !Fit(x.p, map) || (k.nights & (1 << x.i)) != 0);
            if (!all && Round3Map.HoursSince(k.start) < 1.5f) return false;
            // together: those who ate sat side by side at the field's edge, the cook with them.
            for (int i = 0; i < k.pawns.Count; i++)
                if ((k.nights & (1 << i)) != 0)
                    Round2Map.Give(k.pawns[i], "Rimfolk_R3_FieldDinner");
            if (k.nights != 0 && cook != null && !cook.Dead) Round2Map.Give(cook, "Rimfolk_R3_FieldDinner");
            // Whatever was carried out and not eaten goes back to the stores with the game's own hauling.
            return true;
        }
    }

    // ===================================================================================================
    // R8. Clothes worn through
    // ===================================================================================================

    /// <summary>
    /// In autumn three or more of the colony wear something worn past half its life, and the colony has a bench that makes
    /// clothes. A is its best tailor, B one in worn clothes. {COUNT}: those in worn clothes; {THING}: the warmest garment the
    /// bench can make (any mod's); {TALE}: the bench.
    /// </summary>
    public class HappeningWorker_R3WornClothes : IncidentWorker_Happening
    {
        /// <summary>Those of the colony in worn clothes, each with their most worn piece.</summary>
        public static List<(Pawn p, Apparel worn)> Ragged(Map map)
        {
            var list = new List<(Pawn, Apparel)>();
            foreach (var p in Colonists(map))
            {
                var worst = p.apparel?.WornApparel.Where(a => a.def.useHitPoints && Round3EventsMath.Worn(a.HitPoints, a.MaxHitPoints)).OrderBy(a => a.HitPoints / (float)a.MaxHitPoints).FirstOrDefault();
                if (worst != null) list.Add((p, worst));
            }
            return list;
        }

        /// <summary>The colony's bench for clothes and the warmest garment for the body it can make now (any mod's recipe it lists).</summary>
        public static Building_WorkTable Bench(Map map, out RecipeDef recipe)
        {
            recipe = null;
            Building_WorkTable best = null;
            float warmest = 0f;
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>())
            {
                if (!b.Spawned || !b.CurrentlyUsableForBills() || b.def.AllRecipes == null) continue;
                foreach (var r in b.def.AllRecipes)
                {
                    var d = r.ProducedThingDef;
                    if (d == null || !d.IsApparel || !r.AvailableNow || d.apparel.bodyPartGroups == null || !d.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)) continue;
                    float warm;
                    try
                    {
                        warm = d.GetStatValueAbstract(StatDefOf.Insulation_Cold, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (warm <= warmest) continue;
                    warmest = warm;
                    best = b;
                    recipe = r;
                }
            }
            return best;
        }

        public static Pawn Tailor(Map map, Pawn not) =>
            VarietyThree.Best(StorytellerEvents.Adults(map), SkillDefOf.Crafting, VarietyThree.Work("Tailoring"), not) ?? VarietyThree.Best(StorytellerEvents.Adults(map), SkillDefOf.Crafting);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Ragged(map).Count >= Round3EventsMath.RaggedFrom && Bench(map, out _) != null && Tailor(map, null) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var ragged = Ragged(map);
            var bench = Bench(map, out var recipe);
            if (ragged.Count < Round3EventsMath.RaggedFrom || bench == null || recipe == null) return false;
            ctx.a = Tailor(map, null);
            if (ctx.a == null) return false;
            ctx.b = ragged.Select(x => x.p).FirstOrDefault(p => p != ctx.a) ?? ragged[0].p;
            ctx.place = bench;
            ctx.spot = bench.Position;
            ctx.thing = recipe.ProducedThingDef;
            ctx.tale = bench.LabelShort;
            ctx.count = ragged.Count;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            Round3Map.Say(ctx.incident?.defName, ctx.b, "worn");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map != null && ctx.map.mapTemperature.OutdoorTemp < 5f) tags.Add("cold");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>bill</c>: a bill at the colony's bench for clothes for the warmest garment it can make, one for each in worn clothes,
    /// four at most (the game's own tailoring); <c>stores</c>: each one in worn clothes goes to the stores and puts on a better
    /// piece of the same kind lying there unused and allowed them (the game's own "wear").
    /// </summary>
    public class DilemmaEffect_R3Clothes : DilemmaEffect
    {
        public string mode = "bill";

        public override bool ChanceEach => false;

        /// <summary>For each in worn clothes, the best spare piece in the stores that takes the place of the worn one.</summary>
        public static List<(Pawn p, Apparel spare)> Spares(Map map)
        {
            var list = new List<(Pawn, Apparel)>();
            if (map == null) return list;
            var taken = new HashSet<Apparel>();
            var stored = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>()
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && !t.WornByCorpse && Round3EventsMath.Spare(t.HitPoints, t.MaxHitPoints)).ToList();
            foreach (var (p, worn) in HappeningWorker_R3WornClothes.Ragged(map))
            {
                // Only those who can walk to the stores now: not downed, drafted by the player or out of their mind.
                if (p.DevelopmentalStage.Baby() || !p.Spawned || p.Downed || p.Drafted || p.InMentalState) continue;
                var best = stored.Where(t => !taken.Contains(t) && !ApparelUtility.CanWearTogether(worn.def, t.def, p.RaceProps.body) && ApparelUtility.HasPartsToWear(p, t.def)
                                             && EquipmentUtility.CanEquip(t, p) && (p.outfits?.CurrentApparelPolicy?.filter?.Allows(t) ?? true)
                                             && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderByDescending(t => t.GetStatValue(StatDefOf.Insulation_Cold)).FirstOrDefault();
                if (best == null) continue;
                taken.Add(best);
                list.Add((p, best));
            }
            return list;
        }

        private static RecipeDef Recipe(DilemmaContext ctx, out Building_WorkTable bench)
        {
            bench = (ctx as HappeningContext)?.place as Building_WorkTable;
            if (bench == null || !bench.Spawned) bench = HappeningWorker_R3WornClothes.Bench(ctx.map, out var r) ?? bench;
            var want = ctx.thing;
            var recipe = bench?.def.AllRecipes?.FirstOrDefault(x => x.ProducedThingDef == want && x.AvailableNow);
            if (recipe == null && bench != null) HappeningWorker_R3WornClothes.Bench(ctx.map, out recipe);
            return bench != null && bench.Spawned ? recipe : null;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.map == null) return null;
            if (mode == "bill") return Recipe(ctx, out _) == null ? "CC_R3_Clothes_NoBench".Translate().Resolve() : null;
            return Spares(ctx.map).Count == 0 ? "CC_R3_Clothes_NoSpares".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            if (mode == "bill")
            {
                var recipe = Recipe(ctx, out var bench);
                if (recipe == null) return;
                int n = Round3EventsMath.BillCount(HappeningWorker_R3WornClothes.Ragged(ctx.map).Count);
                sheet.Play("CC_R3_Impact_ClothesBill".Translate(n, recipe.ProducedThingDef.label, bench.LabelShort).Resolve(), "CC_R3_Impact_ClothesBillBrief".Translate().Resolve());
                return;
            }
            var spares = Spares(ctx.map);
            if (spares.Count == 0) return;
            sheet.Play("CC_R3_Impact_ClothesStores".Translate(TextUtil.JoinAnd(spares.Select(x => x.p.LabelShort).ToList())).Resolve(), "CC_R3_Impact_ClothesStoresBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            if (mode == "bill")
            {
                var recipe = Recipe(ctx, out var bench);
                if (recipe == null || !(recipe.MakeNewBill() is Bill_Production bill)) return;
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = Round3EventsMath.BillCount(HappeningWorker_R3WornClothes.Ragged(map).Count);
                VarietyThree.First(bench, bill);
                Round2Map.Told(map, "CC_R3_Clothes_Bill".Translate(bill.repeatCount, recipe.ProducedThingDef.label, bench.LabelShort).Resolve(), new LookTargets(bench));
                return;
            }
            var done = new List<string>();
            foreach (var (p, spare) in Spares(map))
            {
                try
                {
                    var job = JobMaker.MakeJob(JobDefOf.Wear, spare);
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, true, true);
                    done.Add(p.LabelShort);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("round 3: clothes from the stores", e);
                }
            }
            if (done.Count > 0) Round2Map.Told(map, "CC_R3_Clothes_Stores".Translate(TextUtil.JoinAnd(done)).Resolve(), LookTargets.Invalid);
        }
    }
}
