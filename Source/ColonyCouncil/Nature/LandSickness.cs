using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// B3, sicknesses of the wild plants (docs/design/living-land-land.md; no mod for 1.6 or 1.7 has them). Now and then, in a
    /// growing day, one gets into the wild plants of a home map: a wilt that goes from tree to tree among trees of one kind,
    /// a rust that thins the grass (and the wild grazers move off it), or a rot in the wild berry bushes. A sick plant wears
    /// the game's own blighted look (the game's blight on it, which no longer spreads from a wild plant on its own:
    /// Patch_LandWildBlight); a wilting tree stands bare after two days. It spreads a little each day to its kind within reach,
    /// more after rain for the rust and the rot, not at all in the cold; a sick plant dies after some days (a tree ten), and
    /// what it killed comes back as a patch (Nature/LandRegrowth.cs). The colony is asked (Rimfolk_Land_SickWood): cut out the
    /// sick and those beside them (the game's cut marks and a work party), burn it out (one of them sets fire to it at the
    /// far edge on a dry day, and the fire is the game's), or let it run. It ends when nothing sick is left, after 45 days, or
    /// after three cold days; the plants still sick then pull through. The region's blight (a world event) makes one three
    /// times as likely, and a believer may take it for Morel's doing (LandGods).
    /// </summary>
    public static class LandSickness
    {
        public const string Question = "Rimfolk_Land_SickWood";

        /// <summary>The setting, and the deeper game hooks (without them the game's blight on a wild plant would spread on its own: Patch_LandWildBlight).</summary>
        public static bool On => (ColonyCouncilMod.Settings?.landSickness ?? true) && RimfolkPatches.On;

        public static JobDef BurnJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Land_Burn");

        private static int Now => Find.TickManager.TicksGame;

        // ================= Hosts =================

        /// <summary>A wild plant this outbreak can take: not sown, not in a field, of its kind (a wilt's or a rot's own kind; any grass for a rust).</summary>
        public static bool Host(Plant p, LandOutbreak o, Map map)
        {
            if (p == null || o == null || !p.Spawned || p.sown || p.def?.plant == null || !LandPlants.Wildling(p.def) || p.Position.GetZone(map) is Zone_Growing) return false;
            switch (o.kind)
            {
                case LandSickKind.Rust: return LandPlants.ClassOf(p.def) == LandPlants.Grass;
                default: return p.def.defName == o.def;
            }
        }

        /// <summary>What a wild plant could catch, by its kind: a tree the wilt, a grass the rust, a berry bush the rot; -1 for none.</summary>
        public static int KindFor(ThingDef d)
        {
            if (!LandPlants.Wildling(d)) return -1;
            int cls = LandPlants.ClassOf(d);
            if (cls == LandPlants.Tree) return (int)LandSickKind.Wilt;
            if (cls == LandPlants.Grass) return (int)LandSickKind.Rust;
            return LandPlants.Berry(d) ? (int)LandSickKind.Rot : -1;
        }

        /// <summary>What to call the plants of an outbreak in the words: "oak trees", "grass", "berry bushes".</summary>
        public static string Kind(ThingDef d, LandSickKind k) =>
            d == null ? "" : k == LandSickKind.Rust ? "CC_Land_GrassWord".Translate().Resolve() : Find.ActiveLanguageWorker.Pluralize(d.label);

        /// <summary>What to call the sickness: "the wilt in the oak trees", "the grass rust", "the rot in the berry bushes".</summary>
        public static string Name(ThingDef d, LandSickKind k) =>
            ("CC_Land_Sick_" + k).Translate((d?.label ?? "").Named("THING"), Kind(d, k).Named("KIND")).Resolve();

        // ================= The day's pass =================

        internal static void Pass(Map map, LandBook b)
        {
            int today = b.Today;
            bool growing = LandBook.GrowingToday(map);
            var o = b.Outbreak;
            if (o == null)
            {
                if (!On) return;
                float chance = LandMath.SickChance(growing, today - b.firstDay, today - b.lastSickEnd, LandWet.Running("Rimfolk_WS_Blight"), WetWarm(map));
                if (Rand.Chance(chance)) Start(map, b, -1);
                return;
            }
            Progress(map, b, o, today, growing);
        }

        /// <summary>A wet, warm spell: a wet day lately and yesterday 12 °C or warmer on average.</summary>
        private static bool WetWarm(Map map)
        {
            var d = WeatherBook.Of(map)?.DaysBefore?.LastOrDefault(x => x != null && x.Counted);
            return d != null && d.Mean >= 12f && LandBook.WetLately(map);
        }

        /// <summary>
        /// A sickness begins (<paramref name="force"/> a kind, or -1 for one the land has hosts for): the map's plants
        /// sampled for its hosts, one of them the first sick, the news and a word from whoever is nearest; null when none can.
        /// </summary>
        internal static LandOutbreak Start(Map map, LandBook b, int force)
        {
            var all = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
            if (all.Count == 0) return null;
            int samples = Math.Min(LandMath.StartSamples, all.Count);
            var found = new List<Plant>[3] { new List<Plant>(), new List<Plant>(), new List<Plant>() };
            var trees = new Dictionary<ThingDef, int>();
            var berries = new Dictionary<ThingDef, int>();
            var home = map.areaManager.Home;
            for (int i = 0; i < samples; i++)
            {
                if (!(all[Rand.Range(0, all.Count)] is Plant p) || !p.Spawned || p.sown || p.Growth < 0.3f || p.Blighted || p.Position.Fogged(map) || p.Position.GetZone(map) != null) continue;
                int k = KindFor(p.def);
                if (k < 0) continue;
                found[k].Add(p);
                if (k == (int)LandSickKind.Wilt) trees[p.def] = trees.TryGetValue(p.def, out int t) ? t + 1 : 1;
                if (k == (int)LandSickKind.Rot) berries[p.def] = berries.TryGetValue(p.def, out int r) ? r + 1 : 1;
            }
            float scale = all.Count / (float)samples;
            var tree = trees.OrderByDescending(x => x.Value).FirstOrDefault();
            var berry = berries.OrderByDescending(x => x.Value).FirstOrDefault();
            var hosts = new[] { (int)(tree.Value * scale), (int)(found[1].Count * scale), (int)(berry.Value * scale) };
            int kind = force >= 0 ? force : LandMath.PickKind(hosts, Rand.Value);
            if (kind < 0) return null;
            var pool = found[kind].Where(p => kind == (int)LandSickKind.Rust || p.def == (kind == (int)LandSickKind.Wilt ? tree.Key : berry.Key)).ToList();
            if (pool.Count == 0) return null;
            // Out on the land rather than in the yard, where there is any.
            var first = pool.Where(p => !home[p.Position]).RandomElementWithFallback() ?? pool.RandomElement();
            var o = new LandOutbreak { kind = (LandSickKind)kind, def = first.def.defName, start = map.cellIndices.CellToIndex(first.Position), day = b.Today };
            b.Outbreak = o;
            Infect(map, o, first, o.day);
            string name = Name(first.def, o.kind);
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                ("CC_Land_Begins_" + o.kind).Translate(name.Named("SICKNESS"), Kind(first.def, o.kind).Named("KIND"), first.def.label.Named("THING"),
                    StorytellerEvents.Direction(map, first.Position).Named("PLACE")).Resolve(), new LookTargets(first), MessageTypeDefOf.NegativeEvent);
            // The nearest of the colony who can see to it says so; a believer may say whose doing they take it for.
            var near = map.mapPawns.FreeColonistsSpawned.Where(x => !x.Downed && !x.InMentalState && x.Awake() && x.Position.InHorDistOf(first.Position, 30f))
                .OrderBy(x => x.Position.DistanceToSquared(first.Position)).FirstOrDefault();
            if (near != null) Say(near, "found:" + o.kind.ToString().ToLowerInvariant(), first.def.label.Named("THING"), Kind(first.def, o.kind).Named("KIND"));
            LandGods.Said(map, o.kind.ToString().ToLowerInvariant(), LandGods.Sickness, first.Position);
            return o;
        }

        /// <summary>A line from the land's pool over someone's head (one sentence), with {PAWN} and the arguments given.</summary>
        internal static bool Say(Pawn p, string tag, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned) return false;
            string line = Flavor.FromPool(LandPool, p, null, null, tag);
            if (line.NullOrEmpty()) return false;
            var args = new List<NamedArgument> { p.Named("PAWN") };
            args.AddRange(more);
            return Speech.Say(p, line.Formatted(args.ToArray()).Resolve());
        }

        public const string LandPool = "Rimfolk_Land_Pool_Said";

        /// <summary>A plant falls sick: the game's blight on it (its look), and kept by its cell.</summary>
        private static void Infect(Map map, LandOutbreak o, Plant p, int today)
        {
            if (!p.Blighted) p.CropBlighted();
            if (!p.Blighted) GenSpawn.Spawn(ThingDefOf.Blight, p.Position, map);
            o.sick.Add(new LandSickPlant(map.cellIndices.CellToIndex(p.Position), today));
            o.ever++;
        }

        /// <summary>The plant of an outbreak's entry, if it still stands there and is still of the outbreak's kind.</summary>
        private static Plant At(Map map, LandOutbreak o, LandSickPlant e)
        {
            if (e.cell < 0 || e.cell >= map.cellIndices.NumGridCells) return null;
            var p = map.cellIndices.IndexToCell(e.cell).GetPlant(map);
            return Host(p, o, map) ? p : null;
        }

        /// <summary>The sick plants still standing.</summary>
        public static List<Plant> Living(Map map, LandOutbreak o)
        {
            var list = new List<Plant>();
            if (map == null || o == null) return list;
            foreach (var e in o.sick)
            {
                var p = At(map, o, e);
                if (p != null) list.Add(p);
            }
            return list;
        }

        /// <summary>The day of an outbreak: the sick looked at (dead, bare or gone), the end, the spread, the grazers, the council.</summary>
        private static void Progress(Map map, LandBook b, LandOutbreak o, int today, bool growing)
        {
            o.cold = LandMath.ColdRun(o.cold, growing);
            var keep = new List<LandSickPlant>();
            foreach (var e in o.sick)
            {
                var p = At(map, o, e);
                if (p == null)
                {
                    // Cut out, burnt, eaten or trampled while sick: gone.
                    o.cut++;
                    continue;
                }
                if (LandMath.Dies(o.kind, e.day, today))
                {
                    Die(map, b, o, p);
                    continue;
                }
                if (LandMath.Bare(o.kind, e.day, today) && !p.LeaflessNow) p.MakeLeafless(Plant.LeaflessCause.Poison, false);
                keep.Add(e);
            }
            o.sick = keep;
            if (LandMath.Over(keep.Count, today - o.day, o.cold))
            {
                End(map, b, o, today);
                return;
            }
            if (growing && On) Spread(map, o, today);
            if (!On) return;
            if (o.kind == LandSickKind.Rust && !o.grazersLeft && o.sick.Count >= LandMath.GrazersLeaveAt) GrazersLeave(map, o);
            if (LandMath.Ask(o.sick.Count, o.asked, today - o.day) && (ColonyCouncilMod.Settings?.enableDilemmas ?? false))
            {
                var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
                if (tracker != null && tracker.TryFire(true, d => d.defName == Question)) o.asked = true;
            }
        }

        /// <summary>
        /// At each of the land's passes (four a day): the wilting trees past their first days kept bare, as the game lets a
        /// leafless tree green again after a day. Only the sick of a wilt, forty at most.
        /// </summary>
        internal static void Keep(Map map, LandBook b)
        {
            var o = b.Outbreak;
            if (o == null || o.kind != LandSickKind.Wilt) return;
            int today = b.Today;
            foreach (var e in o.sick)
            {
                if (!LandMath.Bare(o.kind, e.day, today)) continue;
                var p = At(map, o, e);
                if (p != null && !p.LeaflessNow) p.MakeLeafless(Plant.LeaflessCause.Poison, false);
            }
        }

        /// <summary>A sick plant dies of it: a tree leaves its stump, and what it killed is kept as a patch to come back.</summary>
        private static void Die(Map map, LandBook b, LandOutbreak o, Plant p)
        {
            o.died++;
            if (o.kind != LandSickKind.Rot) b.Lost(o.kind == LandSickKind.Wilt ? LandCause.Wilt : LandCause.Rust, p.Position, p.def.plant.IsTree, p.def);
            p.Kill();
        }

        /// <summary>The day's spread: round each sick plant, a few cells within reach; one of its kind there may catch it.</summary>
        private static void Spread(Map map, LandOutbreak o, int today)
        {
            int room = LandMath.Room(o.kind, 0, o.ever);
            if (room <= 0) return;
            float chance = LandMath.SpreadChance(o.kind, LandBook.WetLately(map), false);
            int reach = LandMath.Reach(o.kind), looks = 0, made = 0;
            var sick = new HashSet<int>(o.sick.Select(x => x.cell));
            foreach (var e in o.sick.InRandomOrder().ToList())
            {
                var from = map.cellIndices.IndexToCell(e.cell);
                for (int t = 0; t < LandMath.TriesEach; t++)
                {
                    if (looks++ >= LandMath.SpreadLooks || made >= room) return;
                    var c = from + new IntVec3(Rand.RangeInclusive(-reach, reach), 0, Rand.RangeInclusive(-reach, reach));
                    if (!c.InBounds(map) || c == from) continue;
                    var p = c.GetPlant(map);
                    if (!Host(p, o, map) || p.Blighted || p.Growth < 0.2f || sick.Contains(map.cellIndices.CellToIndex(c)) || !Rand.Chance(chance)) continue;
                    Infect(map, o, p, today);
                    sick.Add(map.cellIndices.CellToIndex(c));
                    made++;
                }
            }
        }

        /// <summary>The rust has thinned the grass enough: a few wild grazers move off the land.</summary>
        private static void GrazersLeave(Map map, LandOutbreak o)
        {
            o.grazersLeft = true;
            var leaving = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps?.Animal == true && !p.RaceProps.predator && p.RaceProps.Eats(FoodTypeFlags.Plant)
                                                                  && !p.Dead && !p.Downed && !p.InMentalState && p.mindState != null && p.mindState.exitMapAfterTick < 0)
                .InRandomOrder().Take(LandMath.GrazersLeaving).ToList();
            if (leaving.Count == 0) return;
            foreach (var p in leaving) p.mindState.exitMapAfterTick = Now + Rand.Range(GenDate.TicksPerHour, GenDate.TicksPerHour * 8);
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Land_GrazersLeave".Translate(leaving[0].def.label.Named("ANIMAL"), leaving.Count.Named("COUNT")).Resolve(),
                new LookTargets(leaving.Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The outbreak is over: those still sick pull through (the blight taken off them), the news and the chronicle say how it went.</summary>
        private static void End(Map map, LandBook b, LandOutbreak o, int today)
        {
            var survivors = Living(map, o);
            foreach (var p in survivors) p.Blight?.Destroy();
            b.Outbreak = null;
            b.lastSickEnd = today;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(o.def);
            string name = Name(def, o.kind);
            string text = "CC_Land_Over".Translate(name.CapitalizeFirst().Named("SICKNESS"), o.died.Named("DIED"), o.cut.Named("GONE"), survivors.Count.Named("LIVED"), Kind(def, o.kind).Named("KIND")).Resolve();
            Tell.Minor(NewsKind.Land, Importance.Notable, map, text, survivors.Count > 0 ? new LookTargets(survivors.Take(10).Cast<Thing>()) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            if (o.died + o.cut >= 5) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Land_OverChronicle".Translate(name.Named("SICKNESS"), (o.died + o.cut).Named("COUNT"), Kind(def, o.kind).Named("KIND")).Resolve().CapitalizeFirst()));
        }

        // ================= The council's answers =================

        /// <summary>The outbreak the council may be asked about now: some sick, not asked yet.</summary>
        public static LandOutbreak Askable(Map map)
        {
            var o = LandBook.Of(map)?.Outbreak;
            return o != null && !o.asked && On && Living(map, o).Count >= LandMath.AskAt ? o : null;
        }

        /// <summary>The sick and every plant of their kind beside them (within two cells) marked for cutting; how many were marked.</summary>
        public static int MarkCut(Map map)
        {
            var o = LandBook.Of(map)?.Outbreak;
            if (o == null) return 0;
            var marks = new HashSet<Plant>();
            foreach (var p in Living(map, o))
            {
                marks.Add(p);
                foreach (var c in GenRadial.RadialCellsAround(p.Position, 2f, false))
                    if (c.InBounds(map) && c.GetPlant(map) is Plant x && Host(x, o, map))
                        marks.Add(x);
            }
            int n = 0;
            foreach (var p in marks)
            {
                if (p.IsForbidden(Faction.OfPlayer)) continue;
                FieldBlight.Mark(map, p, DesignationDefOf.CutPlant);
                n++;
            }
            return n;
        }

        /// <summary>Cells round <paramref name="c"/> are clear of the colony: no home area, building of its own, zone or blueprint within <see cref="LandMath.BurnClear"/> (looked at every third cell).</summary>
        public static bool Clear(Map map, IntVec3 c)
        {
            int r = (int)LandMath.BurnClear;
            var home = map.areaManager.Home;
            for (int dx = -r; dx <= r; dx += 3)
                for (int dz = -r; dz <= r; dz += 3)
                {
                    var x = new IntVec3(c.x + dx, 0, c.z + dz);
                    if (!x.InBounds(map)) continue;
                    if (home[x] || x.GetZone(map) != null) return false;
                    var b = x.GetEdifice(map);
                    if (b != null && b.Faction == Faction.OfPlayer) return false;
                }
            return true;
        }

        private static int spotTick = -99999, spotMap = -1;
        private static Plant spot;

        /// <summary>Where a burn could be set: a sick plant clear of the colony (the one furthest from it), or null; looked at once an hour at most.</summary>
        public static Plant BurnSpot(Map map)
        {
            if (map == null) return null;
            if (spotMap == map.uniqueID && Now - spotTick < GenDate.TicksPerHour && (spot == null || spot.Spawned)) return spot;
            spotMap = map.uniqueID;
            spotTick = Now;
            spot = null;
            var o = LandBook.Of(map)?.Outbreak;
            if (o == null) return null;
            var center = StorytellerEvents.Center(map);
            foreach (var p in Living(map, o).OrderByDescending(p => p.Position.DistanceToSquared(center)).Take(8))
                if (Clear(map, p.Position))
                {
                    spot = p;
                    break;
                }
            return spot;
        }

        /// <summary>Why a burn can't be chosen now (the sickness gone, nowhere clear of the colony), or null.</summary>
        public static string CantBurn(Map map)
        {
            var o = LandBook.Of(map)?.Outbreak;
            if (o == null || o.sick.Count == 0) return "CC_Land_NoSickness".Translate().Resolve();
            return BurnSpot(map) == null ? "CC_Land_BurnTooNear".Translate().Resolve() : null;
        }

        /// <summary>The council chose the burn: it waits for a dry day's hour, set by <paramref name="by"/> (or the best able then).</summary>
        public static void SetBurn(Map map, Pawn by)
        {
            var o = LandBook.Of(map)?.Outbreak;
            if (o == null) return;
            o.burnWaiting = true;
            o.burner = by?.thingIDNumber ?? -1;
            o.burnSince = LandBook.Of(map).Today;
        }

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !OwnHappenings.Held(p) && !p.DevelopmentalStage.Juvenile()
                                            && !p.WorkTagIsDisabled(WorkTags.Firefighting) && People.HasInnerLife(p);

        /// <summary>
        /// A burn waiting (checked at each of the land's passes): in daylight, dry, no danger, one of the colony walks to a sick
        /// plant clear of the colony with fire (JobDriver_LandBurn); given up after a few days.
        /// </summary>
        internal static void BurnHour(Map map, LandBook b)
        {
            var o = b.Outbreak;
            if (o == null || !o.burnWaiting) return;
            if (b.Today - o.burnSince > LandMath.BurnGiveUpDays)
            {
                o.burnWaiting = false;
                Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Land_BurnGivenUp".Translate(Name(DefDatabase<ThingDef>.GetNamedSilentFail(o.def), o.kind).Named("SICKNESS")).Resolve(), LookTargets.Invalid);
                return;
            }
            var job = BurnJob;
            int hour = GenLocalDate.HourOfDay(map);
            if (job == null || hour < 7 || hour > 17 || map.weatherManager.RainRate > 0.05f || (map.dangerWatcher?.DangerRating ?? StoryDanger.None) != StoryDanger.None) return;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            if (pawns.Any(p => p.CurJobDef == job)) return;
            var target = BurnSpot(map);
            if (target == null) return;
            var burner = pawns.FirstOrDefault(p => p.thingIDNumber == o.burner && Able(p))
                         ?? pawns.Where(Able).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).FirstOrDefault();
            if (burner == null || !burner.CanReach(target, PathEndMode.Touch, Danger.Some)) return;
            var j = JobMaker.MakeJob(job, target);
            j.count = LandMath.BurnTicks;
            j.locomotionUrgency = LocomotionUrgency.Walk;
            if (OwnHappenings.Job(burner, j)) o.burner = burner.thingIDNumber;
        }

        /// <summary>The fire put to the sick plant (the job done), and to a few of the sick beside it; the fire is the game's from there.</summary>
        public static void Lit(Pawn by, Thing target)
        {
            if (by?.Map == null || target == null || !target.Spawned) return;
            var map = by.Map;
            var b = LandBook.Of(map);
            var o = b?.Outbreak;
            int lit = FireUtility.TryStartFireIn(target.Position, map, 0.5f, by) ? 1 : 0;
            if (o != null)
                foreach (var p in Living(map, o).Where(p => p != target && p.Position.InHorDistOf(target.Position, 4f)).Take(LandMath.BurnCells - 1))
                    if (FireUtility.TryStartFireIn(p.Position, map, 0.5f, by))
                        lit++;
            if (lit == 0) return;
            if (o != null) o.burnWaiting = false;
            var def = o != null ? DefDatabase<ThingDef>.GetNamedSilentFail(o.def) : (target as Plant)?.def;
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                "CC_Land_BurnLit".Translate(by.Named("PAWN"), Kind(def, o?.kind ?? LandSickKind.Wilt).Named("KIND")).Resolve(), new LookTargets(target.Position, map),
                MessageTypeDefOf.NeutralEvent);
            Say(by, "burn", Kind(def, o?.kind ?? LandSickKind.Wilt).Named("KIND"));
        }

        // ================= Dev =================

        internal static string DevStart(Map map, int kind)
        {
            var b = LandBook.Of(map);
            if (b == null) return "no land record here.";
            if (b.Outbreak != null) return "a sickness is already going on here: " + b.Describe().Split('\n')[1];
            var o = Start(map, b, kind);
            return o == null ? "nothing here it could get into (it needs its plants growing wild on the map)." : "begun: " + b.Describe().Split('\n')[1];
        }

        internal static string DevDays(Map map, int days)
        {
            var b = LandBook.Of(map);
            var o = b?.Outbreak;
            if (o == null) return "no sickness going on here.";
            for (int i = 0; i < days && b.Outbreak != null; i++)
            {
                o.day--;
                for (int k = 0; k < o.sick.Count; k++) o.sick[k] = new LandSickPlant(o.sick[k].cell, o.sick[k].day - 1);
                Progress(map, b, o, b.Today, true);
            }
            return b.Describe().Split('\n')[1];
        }
    }

    /// <summary>
    /// One of the colony at a sick plant with fire (Nature/LandSickness.cs, the council's burn): up to it, a while with the
    /// torch to it (a glow and smoke, a bar), then the fire put to it and to a few sick beside it.
    /// </summary>
    public class JobDriver_LandBurn : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("LandBurn");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.Thing?.Position ?? pawn.Position;
                pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(200, delta)) return;
                FleckMaker.ThrowFireGlow(at.ToVector3Shifted(), pawn.Map, 0.6f);
                FleckMaker.ThrowSmoke(at.ToVector3Shifted(), pawn.Map, 0.6f);
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("LandBurnDone");
            done.initAction = () => LandSickness.Lit(pawn, job.targetA.Thing);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
