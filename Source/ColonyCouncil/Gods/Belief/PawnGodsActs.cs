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
    /// What a colonist's belief does on the map (Gods/Belief/PawnGods.cs; the player: "gods effect should be noticeable"):
    /// <list type="bullet">
    /// <item>Their own small rite (<see cref="Rites"/>): each god's devotees keep one, short and seen (Hollin's touch a tree,
    /// Smoulder's drop a coin into the rock, Bellwether's stand with the grazing beasts, Morel's bury scraps, Ennu's set an
    /// extra bowl, Pip's tap a machine twice, Cirrus's look up at dusk, Hush's keep a silence in the cold, Undertow's whisper
    /// into the dark at night, Helios's face the sunrise, Selene's look at the moon, Tamiu's sit by a grave at dusk or feed a
    /// cat, Murmur's hum at the water, Ducat's turn a coin over at the silver), with one sentence over their head.</item>
    /// <item>Won over (<see cref="MaybeWonOver"/>): after a strong moment a god not their first becomes it, said in a bubble,
    /// their journal, the news and the colony's book; a scoffer won over scoffs no more.</item>
    /// <item>Dread: one who fears a god turns away from its place (<see cref="KeepAway"/>) and flinches at its signs (<see cref="Flinch"/>).</item>
    /// <item>A god slighted: its place gone (<see cref="ShrinesLost"/>), its feast day let pass (<see cref="FeastPassed"/>): a
    /// small bad mood for those who hold it.</item>
    /// <item>Rival devotees bicker in the game's own talk (<see cref="InteractionWorker_RivalGods"/>).</item>
    /// </list>
    /// No harm here, but for the rare scuffle the game's own talk allows at its insult's rate.
    /// </summary>
    public static class PawnGodsActs
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Won over =================

        /// <summary>
        /// A strong moment of a god (seen plainly, a gift, an answer, a kindness seen, something lived through) may win them over to
        /// it (PawnGodsMath.WonOver): it is their first god from now on, said over their head and in their journal, told, and
        /// set down in the colony's book as their belief. Once in twenty days a person at most.
        /// </summary>
        public static void MaybeWonOver(Pawn p, God g, BeliefEvent e, string why, bool force = false)
        {
            var r = PawnGods.Of(p);
            if (r == null || !PawnGods.Usable(g)) return;
            var h = r.Of(g.id);
            if (h == null && force) h = PawnGods.Move(p, g, BeliefEvent.Told);
            if (h == null) return;
            try
            {
                float other = r.gods.Where(x => x.god != g.id).Select(x => x.hold).DefaultIfEmpty(0f).Max();
                bool scoffer = Worship.Scoffer(p);
                if (!force && !PawnGodsMath.WonOver(h.hold, h.dread, other, PawnGodsMath.Strength(e), scoffer, Rand.Value)) return;
                if (!Interplay.Ready("belief-won|" + p.thingIDNumber, force ? 0f : 20f)) return;
                if (force) h.dread = 0f;
                // Their words, said as they were until now (a scoffer's own).
                string won = "won:" + e.ToString().ToLowerInvariant();
                string line = Worship.Line("Rimfolk_Pool_Belief_WonOver", p, g, null, won);
                string journal = Worship.Line("Rimfolk_Pool_Belief_WonOverJournal", p, g, null, won);
                h.hold = PawnGodsMath.WonOverHold(h.hold, other);
                h.grew = Now;
                h.why = PawnGodsMath.Short("CC_Belief_Why_WonOver".Translate((why ?? "").Named("WHY")).Resolve());
                r.wonOver = Now;
                if (p.Spawned && !line.NullOrEmpty()) Speech.Say(p, line);
                if (!journal.NullOrEmpty()) CouncilGameComponent.Instance?.journals.WriteText(p, journal);
                Tell.Minor(NewsKind.People, Importance.Notable, p.MapHeld, "CC_Belief_WonOver_News".Translate(p.LabelShort.Named("PAWN"), PawnGods.NameOf(g, p).Named("GOD")).Resolve(), p);
                if (!line.NullOrEmpty()) GodLore.Believes(p, g, line);
                CouncilGameComponent.GiveThought(p, PawnGodsDefOf.Rimfolk_Belief_WonOver);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: won over", ex);
            }
        }

        // ================= Dread =================

        /// <summary>One who dreads a god sees its sign: a word over their head, a small bad mood, and a step back from it when they're free.</summary>
        public static void Flinch(Pawn p, God g, IntVec3 at)
        {
            if (p == null || g == null || !Interplay.Ready("belief-flinch|" + p.thingIDNumber, PawnGodsMath.FlinchGapDays)) return;
            string line = Worship.Line("Rimfolk_Pool_Belief_Flinch", p, g);
            if (p.Spawned && !line.NullOrEmpty()) Speech.Say(p, line);
            using (Causes.Push("CC_Belief_Cause_Flinched".Translate(PawnGods.NameOf(g, p).Named("GOD")).Resolve()))
                CouncilGameComponent.GiveThought(p, PawnGodsDefOf.Rimfolk_Belief_Flinched);
            if (!p.Spawned || p.Map == null || !WorshipJobs.Free(p)) return;
            try
            {
                var map = p.Map;
                IntVec3 to = IntVec3.Invalid;
                if (at.IsValid && at.InBounds(map))
                    CellFinder.TryFindRandomCellNear(p.Position, map, 5, c => c.Standable(map) && c.DistanceTo(at) > p.Position.DistanceTo(at) + 1.5f
                                                                             && p.CanReach(c, PathEndMode.OnCell, Danger.None), out to);
                Job job;
                if (to.IsValid)
                {
                    job = JobMaker.MakeJob(JobDefOf.Goto, to);
                    job.locomotionUrgency = LocomotionUrgency.Jog;
                }
                else
                {
                    job = JobMaker.MakeJob(JobDefOf.Wait);
                    job.expiryInterval = 120;
                }
                WorshipJobs.Start(p, job);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a flinch", e);
            }
        }

        /// <summary>The region's hour: one who dreads a god and finds themselves near its place, in their free time, turns and walks away from it, saying why.</summary>
        public static void KeepAway(Map map, int now)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                var r = PawnGods.Of(p, false);
                if (r == null || !Worship.Ours(p)) continue;
                foreach (var h in r.gods)
                {
                    if (!PawnGodsMath.Dreads(h.hold, h.dread)) continue;
                    var g = Gods.ById(h.god);
                    if (g == null || !Gods.Known(g)) continue;
                    var shrine = Shrines.Of(map, g).Select(s => s.thing).FirstOrDefault(t => t.Position.InHorDistOf(p.Position, PawnGodsMath.KeepAwayRange));
                    if (shrine == null || !WorshipJobs.Free(p)) continue;
                    if (!CellFinder.TryFindRandomCellNear(p.Position, map, 22, c => c.Standable(map) && !c.InHorDistOf(shrine.Position, PawnGodsMath.KeepAwayDistance)
                                                                                    && p.CanReach(c, PathEndMode.OnCell, Danger.None), out var to)) continue;
                    if (!Interplay.Ready("belief-away|" + p.thingIDNumber, PawnGodsMath.KeepAwayGapDays)) continue;
                    p.rotationTracker?.FaceTarget(shrine);
                    string line = Worship.Line("Rimfolk_Pool_Belief_KeepAway", p, g);
                    if (!line.NullOrEmpty()) Speech.Say(p, line);
                    WorshipJobs.Start(p, JobMaker.MakeJob(JobDefOf.Goto, to));
                    break;
                }
            }
        }

        // ================= A god slighted =================

        private static readonly HashSet<ShrineRecord> lost = new HashSet<ShrineRecord>();

        /// <summary>
        /// The region's hour: a place given to a god is gone (torn down, burned, broken in a raid): those who hold that god are
        /// hurt by it, and the one who holds it most says so.
        /// </summary>
        public static void ShrinesLost()
        {
            var b = Worship.Book;
            if (b == null || !PawnGods.On) return;
            if (lost.Count > 200) lost.Clear();
            foreach (var s in b.shrines.ToList())
            {
                if (s == null || s.pending || s.thing == null || !s.thing.Destroyed || lost.Contains(s)) continue;
                lost.Add(s);
                var g = Gods.ById(s.god);
                if (g == null || !Gods.Known(g)) continue;
                var hurt = Homes.All.SelectMany(Homes.People).Distinct().Where(p => Holds(p, g)).OrderByDescending(p => PawnGods.HoldOf(p, g)).ToList();
                string cause = "CC_Belief_Cause_ShrineLost".Translate(Worship.ShrineOf(g).Named("SHRINE")).Resolve();
                foreach (var p in hurt)
                    using (Causes.Push(cause))
                        CouncilGameComponent.GiveThought(p, PawnGodsDefOf.Rimfolk_Belief_ShrineLost);
                var first = hurt.FirstOrDefault(p => p.Spawned && Together.Able(p));
                if (first != null) Worship.Say(first, "Rimfolk_Pool_Belief_ShrineLost", g);
            }
        }

        /// <summary>A god's feast day let pass at a home (Festivals.Passed): those there who hold that god feel it.</summary>
        public static void FeastPassed(Map map, God g, string name)
        {
            if (map == null || g == null || !PawnGods.On || !Gods.Known(g)) return;
            string cause = "CC_Belief_Cause_FeastPassed".Translate((name ?? "").Named("NAME")).Resolve();
            foreach (var p in Homes.People(map).Where(p => Holds(p, g)).ToList())
                using (Causes.Push(cause))
                    CouncilGameComponent.GiveThought(p, PawnGodsDefOf.Rimfolk_Belief_FeastPassed);
        }

        /// <summary>Holds this god at least as their own (holding to it, not in dread of it, not a scoffer).</summary>
        private static bool Holds(Pawn p, God g) => Worship.Ours(p) && !Worship.Scoffer(p) && !PawnGods.Dreads(p, g) && PawnGods.HoldOf(p, g) >= PawnGodsMath.HoldsFrom;

        // ================= The small rite =================

        /// <summary>
        /// The region's hour at a home: now and then, by how they hold it (PawnGodsMath.RiteChance) and at its hour
        /// (PawnGodsMath.RiteHour), someone free keeps their first god's small rite (a job: walk there, a moment there, one
        /// sentence over their head). At most PawnGodsMath.RitesAnHour begun an hour a home, once a day a person.
        /// </summary>
        public static void Rites(Map map, int now)
        {
            var def = PawnGodsDefOf.Rimfolk_Belief_Rite;
            if (def == null) return;
            int hour = GenLocalDate.HourOfDay(map);
            float temp = map.mapTemperature?.OutdoorTemp ?? 15f;
            int started = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder().ToList())
            {
                if (started >= PawnGodsMath.RitesAnHour) return;
                if (!Worship.Ours(p) || Worship.Scoffer(p) || p.DevelopmentalStage.Baby()) continue;
                var g = PawnGods.First(p);
                var rite = PawnGodsMath.RiteOf(g?.Key);
                if (rite == null || !PawnGodsMath.RiteHour(rite.Value, hour, temp)) continue;
                if (!Rand.Chance(PawnGodsMath.RiteChance(PawnGods.WordOf(p, g))) || !WorshipJobs.Free(p)) continue;
                var target = Target(p, rite.Value);
                if (!target.IsValid || !Interplay.Ready("belief-rite|" + p.thingIDNumber, PawnGodsMath.RiteGapDays)) continue;
                var job = JobMaker.MakeJob(def, target);
                // The job keeps which god it is for in its count.
                job.count = g.id;
                if (WorshipJobs.Start(p, job)) started++;
            }
        }

        /// <summary>Dev: someone keeps their first god's small rite now, wherever it can be kept.</summary>
        public static bool DevRite(Pawn p)
        {
            var g = PawnGods.First(p);
            var rite = PawnGodsMath.RiteOf(g?.Key);
            var def = PawnGodsDefOf.Rimfolk_Belief_Rite;
            if (rite == null || def == null || p?.Map == null) return false;
            var target = Target(p, rite.Value);
            if (!target.IsValid) return false;
            var job = JobMaker.MakeJob(def, target);
            job.count = g.id;
            return WorshipJobs.Start(p, job);
        }

        private const float Near = 30f;

        /// <summary>Where a rite is kept for someone now: the thing or the cell; Invalid where there is none near (no tree, no machine, no water).</summary>
        public static LocalTargetInfo Target(Pawn p, BeliefRite rite)
        {
            var map = p?.Map;
            if (map == null) return LocalTargetInfo.Invalid;
            try
            {
                var tp = TraverseParms.For(p, Danger.None);
                Thing Closest(ThingRequest req, float range, Predicate<Thing> ok) =>
                    GenClosest.ClosestThingReachable(p.Position, map, req, PathEndMode.Touch, tp, range, t => t.Spawned && !t.Position.Fogged(map) && ok(t));
                switch (rite)
                {
                    case BeliefRite.Tree:
                        return Target(Closest(ThingRequest.ForGroup(ThingRequestGroup.Plant), Near, t => t is Plant pl && pl.def.plant != null && pl.def.plant.IsTree && pl.Growth > 0.3f && !pl.IsBurning()));
                    case BeliefRite.Rock:
                        return Target(NaturalRock(p, map, 25f));
                    case BeliefRite.Grazing:
                        return Target(Closest(ThingRequest.ForGroup(ThingRequestGroup.Pawn), Near, t => t is Pawn a && a.RaceProps.Animal && !a.Dead && !a.Downed && a.Awake()
                                                                                                        && !a.InAggroMentalState && !GodAvatar.Is(a) && !a.HostileTo(Faction.OfPlayer)
                                                                                                        && (a.Faction == Faction.OfPlayer || a.Faction == null && !a.RaceProps.predator)));
                    case BeliefRite.Soil:
                        return Cell(p, map, 15f, c => !c.Roofed(map) && c.GetEdifice(map) == null && (c.GetTerrain(map)?.fertility ?? 0f) >= 0.7f);
                    case BeliefRite.Table:
                        return Target(Closest(ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), 40f, t => t.def.surfaceType == SurfaceType.Eat && t.Faction == Faction.OfPlayer && !t.IsForbidden(p)));
                    case BeliefRite.Machine:
                        return Target(Closest(ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), 40f, t => t.Faction == Faction.OfPlayer && !t.IsForbidden(p)
                                                                                                                    && t.TryGetComp<CompPowerTrader>() is CompPowerTrader c && c.PowerOn));
                    case BeliefRite.Silver:
                        return Target(Closest(ThingRequest.ForDef(ThingDefOf.Silver), 50f, t => t.IsInAnyStorage() && !t.IsForbidden(p)));
                    case BeliefRite.Water:
                    {
                        var well = Closest(ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), 40f, t => t.def.defName == "WaterWellInlet" || t.def.defName == "DeepWaterWellInlet");
                        if (well != null) return well;
                        return Cell(p, map, 25f, c => GenAdj.AdjacentCells.Any(d => (c + d).InBounds(map) && ((c + d).GetTerrain(map)?.IsWater ?? false)));
                    }
                    case BeliefRite.Dark:
                        return Cell(p, map, 15f, c => map.glowGrid.GroundGlowAt(c) < 0.3f);
                    case BeliefRite.Grave:
                    {
                        var grave = Closest(ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), 50f, t => t is Building_Grave);
                        if (grave != null) return grave;
                        return Target(Closest(ThingRequest.ForGroup(ThingRequestGroup.Pawn), Near, t => t is Pawn a && PawnGods.IsCat(a) && a.Faction == Faction.OfPlayer && !a.Dead && !a.Downed));
                    }
                    default:
                        // Out under the sky: the sky at dusk, the cold, the sunrise, the moon.
                        return Cell(p, map, 18f, c => !c.Roofed(map));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: where a rite is kept", e);
                return LocalTargetInfo.Invalid;
            }
        }

        private static LocalTargetInfo Target(Thing t) => t != null ? new LocalTargetInfo(t) : LocalTargetInfo.Invalid;

        /// <summary>A cell near them for a rite: where they stand when it fits, else one that fits within <paramref name="range"/> they can walk to.</summary>
        private static LocalTargetInfo Cell(Pawn p, Map map, float range, Func<IntVec3, bool> ok)
        {
            bool Fits(IntVec3 c) => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && ok(c);
            if (Fits(p.Position)) return p.Position;
            if (CellFinder.TryFindRandomReachableNearbyCell(p.Position, map, range, TraverseParms.For(p, Danger.None), Fits, null, out var cell)) return cell;
            return LocalTargetInfo.Invalid;
        }

        /// <summary>The nearest face of natural rock they can reach and touch, within <paramref name="range"/>.</summary>
        private static Thing NaturalRock(Pawn p, Map map, float range)
        {
            foreach (var c in GenRadial.RadialCellsAround(p.Position, range, false))
            {
                if (!c.InBounds(map) || c.Fogged(map)) continue;
                var e = c.GetEdifice(map);
                if (e?.def?.building == null || !e.def.building.isNaturalRock) continue;
                if (p.CanReach(e, PathEndMode.Touch, Danger.None)) return e;
            }
            return null;
        }

        /// <summary>Their rite is done: a little more sure of their god.</summary>
        public static void RiteKept(Pawn p, God g) => PawnGods.Move(p, g, BeliefEvent.OwnRite);

        // ================= Rival devotees =================

        private static readonly Dictionary<long, int> bickered = new Dictionary<long, int>();

        private static long Pair(Pawn a, Pawn b) => a.thingIDNumber < b.thingIDNumber ? ((long)a.thingIDNumber << 32) | (uint)b.thingIDNumber : ((long)b.thingIDNumber << 32) | (uint)a.thingIDNumber;

        /// <summary>Two devotees of rival gods (GodTiesMath.Hostile), both of the colony, both known gods, not lately: their weight in the game's own talk.</summary>
        public static float BickerWeight(Pawn a, Pawn b)
        {
            // The game asks this for every pair at every turn of talk: the cheap refusals first, each one's first god kept a while.
            if (!PawnGods.On || a == null || b == null || !a.IsColonist || !b.IsColonist) return 0f;
            if (bickered.TryGetValue(Pair(a, b), out int at) && at <= Now && Now - at < PawnGodsMath.BickerGapDays * GenDate.TicksPerDay) return 0f;
            var ga = FirstKept(a);
            var gb = ga == null ? null : FirstKept(b);
            if (ga == null || gb == null || ga == gb) return 0f;
            float ha = PawnGods.HoldOf(a, ga), hb = PawnGods.HoldOf(b, gb);
            if (ha < PawnGodsMath.HoldsFrom || hb < PawnGodsMath.HoldsFrom || !Gods.Rivals(ga).Contains(gb)) return 0f;
            if (!Worship.Ours(a) || !Worship.Ours(b) || Worship.Scoffer(a) || Worship.Scoffer(b)) return 0f;
            return PawnGodsMath.BickerWeight(ha, hb, true, false);
        }

        private static readonly Dictionary<int, (int at, God god)> firstKept = new Dictionary<int, (int, God)>();

        /// <summary>Someone's first god, worked out at most once in 250 ticks (for the game's talk, which asks often).</summary>
        private static God FirstKept(Pawn p)
        {
            int now = Now;
            if (firstKept.TryGetValue(p.thingIDNumber, out var c) && c.at <= now && now - c.at < 250) return c.god;
            if (firstKept.Count > 400) firstKept.Clear();
            var g = PawnGods.First(p);
            firstKept[p.thingIDNumber] = (now, g);
            return g;
        }

        /// <summary>They bicker: each says a word for their own god and against the other's (one sentence each, face to face), and holds their own the harder.</summary>
        public static void Bickered(Pawn a, Pawn b)
        {
            var ga = PawnGods.First(a);
            var gb = PawnGods.First(b);
            if (ga == null || gb == null) return;
            if (bickered.Count > 300) bickered.Clear();
            bickered[Pair(a, b)] = Now;
            // together: the game's own talk brought them face to face, and Speech.Say checks it again.
            string line = Worship.LineWith("Rimfolk_Pool_Belief_Rival", a, ga, b, new[] { PawnGods.NameOf(gb, a).Named("RIVAL") });
            if (!line.NullOrEmpty() && Speech.Say(a, line, b))
            {
                string reply = Worship.LineWith("Rimfolk_Pool_Belief_RivalReply", b, gb, a, new[] { PawnGods.NameOf(ga, b).Named("RIVAL") });
                if (!reply.NullOrEmpty()) Speech.Say(b, reply, a);
            }
            PawnGods.Move(a, ga, BeliefEvent.Bickered);
            PawnGods.Move(b, gb, BeliefEvent.Bickered);
        }
    }

    /// <summary>
    /// Two devotees of rival gods bicker now and then in the game's own talk (Defs/GodDefs/Rimfolk_Belief.xml): its weight
    /// beside the game's chit-chat from PawnGodsMath.BickerWeight, once in a few days a pair; the log line says what it was,
    /// their words over their heads say whose gods (PawnGodsActs.Bickered).
    /// </summary>
    public class InteractionWorker_RivalGods : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                return PawnGodsActs.BickerWeight(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: rival devotees' weight", e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef,
            out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                PawnGodsActs.Bickered(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: rival devotees", e);
            }
        }
    }

    /// <summary>
    /// A colonist's small rite for their god (PawnGodsActs.Rites): walk to its place (a tree, the rock, a beast, the soil, the
    /// table, a machine, the open sky, a dark corner, a grave or a cat, the water, the silver), stay a moment there facing it,
    /// one sentence over their head as they arrive; then they hold their god a little more surely. The god is the job's count.
    /// Danger, drafting or the player's word ends it.
    /// </summary>
    public class JobDriver_BeliefRite : JobDriver
    {
        private bool arrived;

        private God God => Gods.ById(job.count) ?? PawnGods.First(pawn);

        private BeliefRite? Rite => PawnGodsMath.RiteOf(God?.Key);

        /// <summary>Tamiu's rite kept with a cat, not at a grave.</summary>
        private bool WithCat => Rite == BeliefRite.Grave && job.targetA.Thing is Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!job.targetA.HasThing && job.targetA.IsValid && job.targetA.Cell != pawn.Position)
                pawn.Map?.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            return true;
        }

        public override string GetReport()
        {
            var g = God;
            var r = Rite;
            if (g == null || r == null) return "CC_Belief_Report_None".Translate();
            return ("CC_Belief_Report_" + r.Value + (WithCat ? "_Cat" : "")).Translate(PawnGods.NameOf(g, pawn).Named("GOD")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !PawnGods.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else if (job.targetA.IsValid && job.targetA.Cell != pawn.Position)
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("BeliefRite");
            stay.initAction = Arrive;
            stay.tickIntervalAction = delta =>
            {
                if (job.targetA.HasThing) pawn.rotationTracker.FaceTarget(job.targetA);
                else if (Rite == BeliefRite.Sunrise) pawn.rotationTracker.FaceCell(pawn.Position + IntVec3.East * 5);
            };
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = 240 + (pawn.thingIDNumber & 3) * 50;
            stay.handlingFacing = true;
            stay.socialMode = RandomSocialMode.Off;
            yield return stay;
            yield return Toils_General.Do(() =>
            {
                try
                {
                    PawnGodsActs.RiteKept(pawn, God);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a rite kept", e);
                }
            });
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            try
            {
                var g = God;
                var r = Rite;
                if (g == null || r == null) return;
                string tag = "rite:" + (WithCat ? "cat" : r.Value.ToString().ToLowerInvariant());
                Worship.Say(pawn, "Rimfolk_Pool_Belief_Rite", g, null, tag);
                var map = pawn.Map;
                var at = job.targetA.HasThing ? job.targetA.Thing.Position : job.targetA.Cell;
                if (map == null || !at.IsValid) return;
                switch (r.Value)
                {
                    case BeliefRite.Machine:
                        FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map);
                        break;
                    case BeliefRite.Soil:
                    case BeliefRite.Rock:
                        FleckMaker.ThrowDustPuff(at, map, 0.8f);
                        break;
                    default:
                        FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a rite begun", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }
}
