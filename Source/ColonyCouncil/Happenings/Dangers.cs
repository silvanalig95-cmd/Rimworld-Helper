using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Dangers on the map (Defs/IncidentDefs/Rimfolk_Events_Dangers.xml): things that can go wrong where the colony lives,
    // seen coming, asked about, and played out with the game's own: a creak at a rock face someone is digging under the
    // mountain, a tree leaning toward the colony's walls after the rain, smoke in the dry grass on a hot day, an old shell
    // turned up in a field, a mechanoid's remains with charge still in them, the ground by a door going soft after the rains.
    // They are Rimfolk events, drawn from the same pool at the same rate, and come only with "Hurts from everyday life" above
    // nothing. Every hurt goes through the harm service (Harm/Harm.cs: its scale, its cap, its wounds that never kill), is
    // named under the answer before it's chosen, is shown where it happens and told through the teller. What the danger
    // keeps on the map while it waits and after the answer is one of the things OnMapTimed keeps (PlayOut/OnMapTimed.cs);
    // the steps are here. The rules without game types are in VarietyThreeMath.

    /// <summary>The dangers' part of what is kept on the map: their signs, their watches and what happens at the end.</summary>
    public static class MapDangers
    {
        private static int Now => Find.TickManager.TicksGame;

        private static int Ticks(float hours) => Mathf.RoundToInt(hours * GenDate.TicksPerHour);

        /// <summary>The dangers come only when everyday hurts happen at all.</summary>
        public static bool On => Harm.On;

        /// <summary>A step's share of an hour (each step is one 250-tick pass).</summary>
        private const float StepOfHour = Sched.RareTicks / (float)Sched.HourTicks;

        // ---------------- Kept on the map ----------------

        /// <summary>
        /// The danger's sign kept on the map from the moment it is seen, until the answer: past the letter's time to answer
        /// by an hour, and <see cref="VarietyThreeMath.SignHours"/> at least.
        /// </summary>
        public static TimedThing Sign(TimedKind kind, HappeningContext ctx, Thing thing = null, List<IntVec3> cells = null)
        {
            var timed = OnMapTimed.Instance;
            if (timed == null || ctx?.map == null) return null;
            float hours = Mathf.Max(VarietyThreeMath.SignHours, (ctx.Ext?.answerDays ?? 0f) * 24f + 1f);
            var k = new TimedThing { kind = kind, map = ctx.map, cell = ctx.spot, pawn = ctx.a, where = ctx.incident?.defName, until = Now + Ticks(hours) };
            if (thing != null) k.things.Add(thing);
            if (cells != null) k.cells.AddRange(cells);
            timed.Add(k);
            return k;
        }

        /// <summary>The danger an answer is about, kept on the map (made again if a load or the cap lost it).</summary>
        public static TimedThing Of(TimedKind kind, DilemmaContext ctx)
        {
            var h = ctx as HappeningContext;
            var timed = OnMapTimed.Instance;
            if (h?.map == null || timed == null) return null;
            var k = timed.kept.FirstOrDefault(x => x.kind == kind && x.map == h.map && (h.place != null && x.things.Contains(h.place) || h.spot.IsValid && x.cell == h.spot));
            return k ?? Sign(kind, h, h.place);
        }

        private static List<string> Lines(TimedThing k) => DefDatabase<IncidentDef>.GetNamedSilentFail(k?.where ?? "")?.GetModExtension<HappeningExtension>()?.lines;

        /// <summary>One sentence over <paramref name="p"/>'s head for the moment (the event's lines tagged with it, in the colony's age).</summary>
        public static void Say(TimedThing k, Pawn p, string moment)
        {
            try
            {
                var lines = Lines(k);
                if (p == null || !p.Spawned || lines.NullOrEmpty()) return;
                var tags = new HashSet<string> { moment };
                Flavor.AddEraTags(tags);
                string line = Flavor.PickTagged(lines, tags);
                if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(p.Named("PAWN")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("danger: a line", e);
            }
        }

        /// <summary>
        /// A wound of the harm service's (under its cap, never more than the part has left), told at once in one of its two
        /// wordings (<paramref name="key"/>, <paramref name="key"/>_2). Null when nothing was given (the cap held them back).
        /// </summary>
        public static Hediff_Injury Wound(Pawn p, HurtKind kind, HurtWhere where, float low, float high, string key)
        {
            if (p == null || !p.Spawned) return null;
            var h = Harm.Injure(p, kind, where, low, high, key, HurtClass.Injury);
            if (h == null) return null;
            string text = TextVariants.Vary(key).Translate(p.Named("PAWN"), (h.Part?.Label ?? "").Named("PART")).Resolve();
            Tell.Message(NewsKind.Health, Importance.Important, p.Map, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
            return h;
        }

        private static void Notable(Map map, string text, LookTargets look) => Tell.Message(NewsKind.Colony, Importance.Notable, map, text, look, MessageTypeDefOf.NeutralEvent);

        private static bool Free(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Juvenile() && !PlayOuts.NeedsPress(p);

        /// <summary>Whoever of the colony can go now, the nearest to <paramref name="at"/> first (<paramref name="first"/> if they can).</summary>
        private static Pawn Goer(Map map, IntVec3 at, Pawn first)
        {
            if (Free(first) && first.Map == map) return first;
            return map.mapPawns.FreeColonistsSpawned.Where(p => Free(p) && !p.IsQuestLodger()).OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();
        }

        /// <summary>Sends them to stand by <paramref name="at"/>, unless they are on their way already.</summary>
        private static void SendTo(Pawn p, IntVec3 at)
        {
            if (p == null || !at.IsValid) return;
            if (p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell == at) return;
            if (!p.CanReach(at, PathEndMode.OnCell, Danger.Some)) return;
            p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, at), JobTag.Misc, false);
        }

        /// <summary>A standable cell beside <paramref name="c"/>, the nearest to <paramref name="from"/>.</summary>
        private static IntVec3 Beside(IntVec3 c, Map map, IntVec3 from) =>
            GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Where(x => x.InBounds(map) && x.Standable(map)).OrderBy(x => x.DistanceToSquared(from)).DefaultIfEmpty(IntVec3.Invalid).First();

        private static void Pause(Pawn p, int ticks)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed) return;
            var job = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            job.expiryInterval = ticks;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        // ---------------- The steps ----------------

        public static bool Step(TimedThing k, int now)
        {
            if (!On) return true;
            switch (k.kind)
            {
                case TimedKind.DigFace: return StepDig(k, now);
                case TimedKind.Leaning: return StepTree(k, now);
                case TimedKind.Smoulder: return StepSmoulder(k, now);
                case TimedKind.Shell: return StepShell(k, now);
                case TimedKind.Scrap: return StepScrap(k, now);
                case TimedKind.SoftGround: return StepGround(k, now);
                default: return true;
            }
        }

        /// <summary>
        /// It's over. A creaking face never answered keeps its digging stopped (as the silence has it); an old shell never
        /// answered stays where it lies, kept from the haulers.
        /// </summary>
        public static void End(TimedThing k, bool early)
        {
        }

        // ================= The creak at the dig =================

        /// <summary>
        /// Waiting: grit keeps dropping at the face. Dug on: the first time someone digs at that face again, the rock may come
        /// down (once): rubble and a chunk where it fell, and a crush for whoever was under it, unless the cap keeps them clear.
        /// </summary>
        private static bool StepDig(TimedThing k, int now)
        {
            var map = k.map;
            if (k.count == 0)
            {
                if (Rand.Chance(0.25f)) FleckMaker.ThrowDustPuff(k.cell, map, 0.8f);
                return now > k.until;
            }
            if (k.count != 1) return true;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.CurJobDef != JobDefOf.Mine || p.CurJob.targetA.Cell.DistanceTo(k.cell) > 3.9f) continue;
                if (Rand.Chance(Harm.Chance(VarietyThreeMath.DigOnChance))) Rockfall(k, p);
                return true;
            }
            return now > k.until;
        }

        private static void Rockfall(TimedThing k, Pawn p)
        {
            var map = k.map;
            var rock = p.CurJob?.targetA.Thing;
            var chunk = rock?.def.building?.mineableThing;
            p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            foreach (var c in GenRadial.RadialCellsAround(p.Position, 1.5f, true).Where(c => c.InBounds(map) && c.Standable(map)).InRandomOrder().Take(5))
                FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleRock, 1);
            FleckMaker.ThrowDustPuffThick(p.DrawPos, map, 2f, new Color(0.8f, 0.75f, 0.65f));
            if (chunk != null && chunk.thingCategories != null && chunk.thingCategories.Contains(ThingCategoryDefOf.StoneChunks))
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(chunk), p.Position, map, ThingPlaceMode.Near);
            var h = Wound(p, HurtKind.Crush, Rand.Bool ? HurtWhere.Torso : HurtWhere.Arm, VarietyThreeMath.RockfallLow, VarietyThreeMath.RockfallHigh, "CC_Danger_Rockfall");
            if (h != null) Say(k, p, "hurt");
            else
            {
                Say(k, p, "clear");
                Notable(map, "CC_Danger_RockfallClear".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
            }
        }

        // ================= The leaning tree =================

        /// <summary>Waiting: the ground at its foot lifts. Left standing: a wind may bring it down onto the wall, for some days.</summary>
        private static bool StepTree(TimedThing k, int now)
        {
            var tree = k.things.FirstOrDefault() as Plant;
            if (tree == null || !tree.Spawned || tree.Map != k.map) return true;
            bool windy = k.map.windManager.WindSpeed >= 1.5f;
            if (k.count == 0)
            {
                if (Rand.Chance(0.1f)) FleckMaker.ThrowDustPuff(tree.Position, k.map, 0.7f);
                return now > k.until;
            }
            if (k.count != 1) return true;
            if (windy && Rand.Chance(0.15f)) FleckMaker.ThrowDustPuff(tree.Position, k.map, 0.9f);
            if (Rand.Chance(VarietyThreeMath.TreeFallPerHour(windy) * StepOfHour))
            {
                Fall(k, tree);
                return true;
            }
            return now > k.until;
        }

        private static void Fall(TimedThing k, Plant tree)
        {
            var map = k.map;
            var from = tree.Position;
            var wall = k.cell.IsValid ? k.cell.GetEdifice(map) : null;
            int wood = tree.def.plant?.harvestedThingDef != null ? Mathf.RoundToInt(tree.def.plant.harvestYield * tree.Growth) : 0;
            var woodDef = tree.def.plant?.harvestedThingDef;
            // Whoever stands under it on the tree's side of the wall: the wall stands, so nobody behind it is reached.
            var side = from.GetRoom(map);
            var under = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.GetRoom() == side
                                                                && (p.Position.DistanceTo(k.cell) <= 1.5f || p.Position.DistanceTo(from) <= 1.5f)).ToList();
            tree.Destroy(DestroyMode.Vanish);
            FleckMaker.ThrowDustPuffThick(from.ToVector3Shifted(), map, 2.5f, new Color(0.6f, 0.55f, 0.45f));
            FilthMaker.TryMakeFilth(from, map, ThingDefOf.Filth_LooseGround, 2);
            if (wall != null && wall.Spawned)
            {
                int dmg = VarietyThreeMath.TreeWallDamage(wall.HitPoints, wall.MaxHitPoints);
                if (dmg > 0) wall.TakeDamage(new DamageInfo(DamageDefOf.Crush, dmg));
                FilthMaker.TryMakeFilth(Beside(k.cell, map, from).IsValid ? Beside(k.cell, map, from) : from, map, ThingDefOf.Filth_RubbleBuilding, 2);
            }
            if (woodDef != null && wood > 0)
            {
                var logs = ThingMaker.MakeThing(woodDef);
                logs.stackCount = Mathf.Clamp(wood, 1, woodDef.stackLimit);
                GenPlace.TryPlaceThing(logs, Beside(k.cell, map, from).IsValid ? Beside(k.cell, map, from) : from, map, ThingPlaceMode.Near);
            }
            Pawn hurt = null;
            foreach (var p in under.Where(p => p.Faction == Faction.OfPlayer && p.Spawned))
                if (Wound(p, HurtKind.Crush, HurtWhere.Any, VarietyThreeMath.TreeCrushLow, VarietyThreeMath.TreeCrushHigh, "CC_Danger_TreeCrush") != null)
                {
                    hurt = p;
                    Say(k, p, "hurt");
                    break;
                }
            if (hurt != null) return;
            var seer = map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(from)).FirstOrDefault();
            Say(k, seer, "fell");
            Tell.Message(NewsKind.Colony, Importance.Important, map, TextVariants.Vary("CC_Danger_TreeFell").Translate((tree.def.label ?? "").Named("TREE")).Resolve(),
                new LookTargets(new TargetInfo(k.cell.IsValid ? k.cell : from, map)), MessageTypeDefOf.NegativeEvent);
        }

        // ================= Smoke in the dry grass =================

        /// <summary>
        /// Smoke keeps rising from the spot. Stamped out: whoever goes stamps it out, and may burn a hand or a foot doing it;
        /// nobody there in time, it catches. Left: it catches within a few hours, a grass fire of the game's own.
        /// </summary>
        private static bool StepSmoulder(TimedThing k, int now)
        {
            var map = k.map;
            FleckMaker.ThrowSmoke(k.cell.ToVector3Shifted(), map, 1.1f);
            if (k.count == 0) return now > k.until;
            if (k.count == 2)
            {
                if (now < k.done) return false;
                Flare(k);
                return true;
            }
            if (k.count != 1) return true;
            var p = Goer(map, k.cell, k.pawn);
            if (p == null)
            {
                if (now <= k.until) return false;
                Flare(k);
                return true;
            }
            k.pawn = p;
            if (p.Position.DistanceTo(k.cell) <= 1.5f)
            {
                FilthMaker.TryMakeFilth(k.cell, map, ThingDefOf.Filth_Ash, 1);
                Pause(p, 180);
                if (Rand.Chance(Harm.Chance(VarietyThreeMath.StampBurnChance))
                    && Wound(p, HurtKind.Burn, Rand.Bool ? HurtWhere.Hand : HurtWhere.Foot, VarietyThreeMath.StampBurnLow, VarietyThreeMath.StampBurnHigh, "CC_Danger_StampBurn") != null)
                    Say(k, p, "hurt");
                else
                {
                    Say(k, p, "out");
                    Notable(map, "CC_Danger_SmoulderOut".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
                }
                return true;
            }
            if (!k.on)
            {
                Say(k, p, "go");
                k.on = true;
            }
            SendTo(p, Beside(k.cell, map, p.Position));
            if (now <= k.until) return false;
            Flare(k);
            return true;
        }

        private static void Flare(TimedThing k)
        {
            var map = k.map;
            var cells = new[] { k.cell }.Concat(GenAdj.CellsAdjacent8Way(new TargetInfo(k.cell, map))).Where(c => c.InBounds(map));
            bool caught = cells.Any(c => FireUtility.TryStartFireIn(c, map, 0.3f, null));
            if (!caught) return;
            Tell.Message(NewsKind.Colony, Importance.Important, map, TextVariants.Vary("CC_Danger_SmoulderCaught").Translate().Resolve(), new LookTargets(new TargetInfo(k.cell, map)), MessageTypeDefOf.NegativeEvent);
        }

        // ================= The old shell =================

        /// <summary>
        /// Carried out: whoever carries it sets it down far past the fields, and it is kept from the haulers there. Set off:
        /// once nobody is near it and the one setting it off stands well back, its fuse is lit (the game's own blast), and
        /// a stone thrown up may cut them.
        /// </summary>
        private static bool StepShell(TimedThing k, int now)
        {
            var map = k.map;
            var shell = k.things.FirstOrDefault();
            if (k.count == 2 && k.on)
            {
                if (shell != null && !shell.Destroyed) return now > k.until;
                var p = k.pawn;
                if (p != null && p.Spawned && Rand.Chance(Harm.Chance(VarietyThreeMath.BlastStoneChance))
                    && Wound(p, HurtKind.Cut, Rand.Bool ? HurtWhere.Arm : HurtWhere.Any, VarietyThreeMath.BlastStoneLow, VarietyThreeMath.BlastStoneHigh, "CC_Danger_BlastStone") != null)
                    Say(k, p, "hurt");
                else Say(k, p, "gone");
                return true;
            }
            if (shell == null || shell.Destroyed) return true;
            if (k.count == 0) return now > k.until;
            if (k.count == 1) return StepCarry(k, shell, now);
            if (k.count != 2) return true;
            var setter = Goer(map, k.cell, k.pawn);
            if (setter == null) return now > k.until;
            k.pawn = setter;
            if (setter.Position.DistanceTo(k.cell) > 1.5f)
            {
                SendTo(setter, k.cell);
                return now > k.until;
            }
            if (map.mapPawns.AllPawnsSpawned.Any(x => x != setter && !x.Dead && x.Position.DistanceTo(shell.Position) <= VarietyThreeMath.ShellClear))
            {
                if (Rand.Chance(0.1f)) Say(k, setter, "clear");
                return now > k.until;
            }
            var fuse = shell.TryGetComp<CompExplosive>();
            if (fuse == null) return true;
            Pause(setter, 400);
            Say(k, setter, "fire");
            fuse.StartWick(setter);
            k.on = true;
            k.until = now + Ticks(2f);
            return false;
        }

        private static bool StepCarry(TimedThing k, Thing shell, int now)
        {
            var map = k.map;
            if (shell.ParentHolder is Pawn_CarryTracker) return false;
            if (!shell.Spawned) return now > k.until;
            if (shell.Position.DistanceTo(k.cell) <= 2f)
            {
                shell.SetForbidden(true, false);
                Notable(map, "CC_Danger_ShellCarried".Translate().Resolve(), new LookTargets(shell));
                return true;
            }
            if (now > k.until)
            {
                shell.SetForbidden(true, false);
                return true;
            }
            var p = Goer(map, shell.Position, k.pawn);
            if (p == null || p.CurJobDef == JobDefOf.HaulToCell) return false;
            k.pawn = p;
            shell.SetForbidden(false, false);
            if (!p.CanReserveAndReach(shell, PathEndMode.ClosestTouch, Danger.Some) || !p.CanReach(k.cell, PathEndMode.OnCell, Danger.Some)) return false;
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, shell, k.cell);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            p.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
            if (!k.on)
            {
                Say(k, p, "carry");
                k.on = true;
            }
            return false;
        }

        // ================= The twitching scrap =================

        /// <summary>
        /// Sparks keep jumping from it. Broken up: whoever does it works at it a while, may be burned by the last of its
        /// charge, and leaves steel and bits. Left: for some days the first who handles it may be burned.
        /// </summary>
        private static bool StepScrap(TimedThing k, int now)
        {
            var map = k.map;
            var body = k.things.FirstOrDefault();
            if (body == null || body.Destroyed) return true;
            if (body.Spawned && Rand.Chance(0.3f)) FleckMaker.ThrowMicroSparks(body.DrawPos, map);
            if (k.count == 0) return now > k.until;
            if (k.count == 2)
            {
                var near = body.ParentHolder is Pawn_CarryTracker carry ? carry.pawn
                    : body.Spawned ? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.Position.DistanceTo(body.Position) <= 1.5f && !p.Downed) : null;
                if (near == null) return now > k.until;
                Spark(k, near, VarietyThreeMath.ScrapSparkLeft);
                return true;
            }
            if (k.count != 1 || !body.Spawned) return now > k.until;
            var w = Goer(map, body.Position, k.pawn);
            if (w == null) return now > k.until;
            k.pawn = w;
            if (w.Position.DistanceTo(body.Position) > 1.5f)
            {
                k.on = false;
                SendTo(w, Beside(body.Position, map, w.Position));
                return now > k.until;
            }
            if (!k.on)
            {
                k.on = true;
                k.done = now + Ticks(0.5f);
                Pause(w, Ticks(0.5f));
                Say(k, w, "work");
                return false;
            }
            if (now < k.done) return false;
            Spark(k, w, VarietyThreeMath.ScrapSparkBreak);
            var at = body.Position;
            float size = (body as Corpse)?.InnerPawn?.BodySize ?? 1f;
            body.Destroy(DestroyMode.Vanish);
            FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_MachineBits, 2);
            StorytellerEvents.Hand(ThingDefOf.Steel, Mathf.Clamp(Mathf.RoundToInt(20f * size), 10, 60), map, at);
            Notable(map, "CC_Danger_ScrapBroken".Translate(w.Named("PAWN")).Resolve(), new LookTargets(new TargetInfo(at, map)));
            return true;
        }

        private static void Spark(TimedThing k, Pawn p, float chance)
        {
            if (p == null) return;
            if (p.Spawned) FleckMaker.ThrowMicroSparks(p.DrawPos, p.Map);
            if (Rand.Chance(Harm.Chance(chance)) && Wound(p, HurtKind.Burn, Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm, VarietyThreeMath.ScrapBurnLow, VarietyThreeMath.ScrapBurnHigh, "CC_Danger_ScrapBurn") != null)
                Say(k, p, "hurt");
        }

        // ================= The ground giving =================

        /// <summary>
        /// Left: each of the first few who cross the soft ground may put a foot through; when it gives, it's mud from then on,
        /// and the foot takes a bruise (or a crack one time in four), unless the cap keeps them on their feet.
        /// </summary>
        private static bool StepGround(TimedThing k, int now)
        {
            if (k.count == 0) return now > k.until;
            if (k.count != 1) return true;
            var map = k.map;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!k.cells.Contains(p.Position) || !p.pather.Moving || k.pawns.Contains(p)) continue;
                if (Rand.Chance(Harm.Chance(VarietyThreeMath.GroundGiveChance)))
                {
                    Give(k, p);
                    return true;
                }
                k.pawns.Add(p);
                if (k.pawns.Count >= VarietyThreeMath.GroundCrossings) return true;
            }
            return now > k.until;
        }

        private static void Give(TimedThing k, Pawn p)
        {
            var map = k.map;
            var mud = TerrainDefOf.Mud;
            foreach (var c in k.cells.Where(c => c.InBounds(map)))
            {
                var t = c.GetTerrain(map);
                if (mud != null && t != mud && !t.IsWater && t.natural) map.terrainGrid.SetTerrain(c, mud);
                FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_LooseGround, 1);
            }
            FleckMaker.ThrowDustPuff(p.Position, map, 1f);
            bool crack = Rand.Range(0, VarietyThreeMath.GroundCrackOneIn) == 0;
            var h = crack
                ? Wound(p, HurtKind.Crack, HurtWhere.Foot, VarietyThreeMath.GroundBruiseLow * 0.6f, VarietyThreeMath.GroundBruiseHigh * 0.6f, "CC_Danger_GroundCrack")
                : Wound(p, HurtKind.Bruise, HurtWhere.Foot, VarietyThreeMath.GroundBruiseLow, VarietyThreeMath.GroundBruiseHigh, "CC_Danger_GroundGave");
            Pause(p, 150);
            if (h != null) Say(k, p, "hurt");
            else
            {
                Say(k, p, "clear");
                Notable(map, "CC_Danger_GroundCaught".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
            }
        }
    }

    // ===================================== The events =====================================

    /// <summary>Shared by the dangers' workers: only with everyday hurts on, and never while a threat is on the map.</summary>
    public abstract class HappeningWorker_Danger : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => MapDangers.On && !MapFacts.ThreatOn(map) && Found(map);

        protected abstract bool Found(Map map);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    // ---------------- The creak at the dig ----------------

    /// <summary>
    /// Someone of the colony is digging at a rock face under a roof of rock (A), and the rock above creaks and drops grit.
    /// The digging at that face stops until the council says. B is the colony's best builder. {THING}: the rock.
    /// </summary>
    public class HappeningWorker_DigCreak : HappeningWorker_Danger
    {
        public static Pawn Miner(Map map, out Thing face)
        {
            face = null;
            foreach (var p in Colonists(map))
            {
                if (!StorytellerEvents.Able(p) || p.CurJobDef != JobDefOf.Mine || !(p.CurJob.targetA.Thing is Thing t) || !t.Spawned) continue;
                if (!map.roofGrid.Roofed(t.Position) || !map.roofGrid.Roofed(p.Position) || !Harm.May(p)) continue;
                face = t;
                return p;
            }
            return null;
        }

        protected override bool Found(Map map) => Miner(map, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Miner(ctx.map, out var face);
            if (ctx.a == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.b == null) return false;
            ctx.spot = face.Position;
            ctx.thing = face.def;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, face.Position);
            return true;
        }

        /// <summary>The creak: grit and dust at the face, the digging there stopped (its marks kept to put back), the digger stepping back.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var dm = map.designationManager;
            var stopped = new List<IntVec3>();
            foreach (var c in GenRadial.RadialCellsAround(ctx.spot, 3.9f, true).Where(c => c.InBounds(map)))
            {
                var d = dm.DesignationAt(c, DesignationDefOf.Mine) ?? dm.DesignationAt(c, DesignationDefOf.MineVein);
                if (d == null) continue;
                stopped.Add(c);
                dm.RemoveDesignation(d);
            }
            FleckMaker.ThrowDustPuff(ctx.spot, map, 1.2f);
            var under = MapDangersCells.Open(ctx.spot, map, ctx.a.Position);
            if (under.IsValid) FilthMaker.TryMakeFilth(under, map, ThingDefOf.Filth_RubbleRock, 1);
            ctx.a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            var back = MapDangersCells.StepBack(ctx.a, ctx.spot, map);
            if (back.IsValid) ctx.a.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc, false);
            var k = MapDangers.Sign(TimedKind.DigFace, ctx, null, stopped);
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }
    }

    /// <summary>Cells for the dangers: open ground beside a face, a step back from it.</summary>
    public static class MapDangersCells
    {
        public static IntVec3 Open(IntVec3 c, Map map, IntVec3 from) =>
            GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Where(x => x.InBounds(map) && x.Standable(map)).OrderBy(x => x.DistanceToSquared(from)).DefaultIfEmpty(IntVec3.Invalid).First();

        public static IntVec3 StepBack(Pawn p, IntVec3 from, Map map) =>
            GenRadial.RadialCellsAround(p.Position, 4f, true).Where(c => c.InBounds(map) && c.Standable(map) && c.DistanceTo(from) >= 3f && p.CanReach(c, PathEndMode.OnCell, Danger.Some))
                .OrderBy(c => c.DistanceToSquared(p.Position)).DefaultIfEmpty(IntVec3.Invalid).First();
    }

    /// <summary>
    /// What the council says about the creaking face: <c>prop</c> (a column marked to be built beside it, the digging put
    /// back), <c>dig</c> (the digging put back as it was, and the rock may come down on whoever digs there next), or
    /// <c>leave</c> (that face is left: its digging stays stopped).
    /// </summary>
    public class DilemmaEffect_DigFace : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        /// <summary>The cheapest stuff the colony has enough of for a column.</summary>
        private static ThingDef PropStuff(Map map)
        {
            var col = ThingDefOf.Column;
            if (col == null || !col.IsResearchFinished) return null;
            int cost = Math.Max(1, col.costStuffCount);
            return map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= cost && kv.Key.IsStuff && GenStuff.AllowedStuffsFor(col).Contains(kv.Key))
                .OrderBy(kv => kv.Key.BaseMarketValue).Select(kv => kv.Key).FirstOrDefault();
        }

        private static IntVec3 PropCell(HappeningContext h, ThingDef stuff)
        {
            if (h?.map == null || !h.spot.IsValid || stuff == null) return IntVec3.Invalid;
            return GenAdj.CellsAdjacent8Way(new TargetInfo(h.spot, h.map))
                .Where(c => c.InBounds(h.map) && c.Standable(h.map) && h.map.roofGrid.Roofed(c) && GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Column, c, Rot4.North, h.map, false, null, null, stuff).Accepted)
                .DefaultIfEmpty(IntVec3.Invalid).First();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (mode != "prop" || !(ctx is HappeningContext h) || h.map == null) return null;
            var stuff = PropStuff(h.map);
            if (stuff == null) return "CC_Danger_NoPropStuff".Translate().ToString();
            return PropCell(h, stuff).IsValid ? null : "CC_Danger_NoPropRoom".Translate().ToString();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            switch (mode)
            {
                case "prop":
                    var stuff = PropStuff(h.map);
                    if (stuff == null || !PropCell(h, stuff).IsValid) return;
                    sheet.Play("CC_Impact_DigProp".Translate(stuff.label).Resolve(), "CC_Impact_DigPropBrief".Translate().Resolve());
                    break;
                case "dig":
                    sheet.Play("CC_Impact_DigOn".Translate().Resolve(), "CC_Impact_DigOnBrief".Translate().Resolve());
                    if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_DigRisk".Translate().Resolve(), "CC_Impact_DigRiskBrief".Translate().Resolve(), "digrisk");
                    break;
                default:
                    sheet.Play("CC_Impact_DigLeave".Translate().Resolve(), "CC_Impact_DigLeaveBrief".Translate().Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            try
            {
                var k = MapDangers.Of(TimedKind.DigFace, ctx);
                if (k == null) return;
                if (mode == "leave")
                {
                    k.count = 2;
                    return;
                }
                var dm = h.map.designationManager;
                foreach (var c in k.cells)
                    if (c.InBounds(h.map) && c.GetEdifice(h.map) is Building b && b.def.mineable && dm.DesignationAt(c, DesignationDefOf.Mine) == null)
                        dm.AddDesignation(new Designation(c, DesignationDefOf.Mine));
                if (mode == "prop")
                {
                    var stuff = PropStuff(h.map);
                    var cell = PropCell(h, stuff);
                    if (cell.IsValid) GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Column, cell, h.map, Rot4.North, Faction.OfPlayer, stuff);
                    k.count = 2;
                    return;
                }
                k.count = 1;
                k.until = Find.TickManager.TicksGame + GenDate.TicksPerDay * 2;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dig face", e);
            }
        }
    }

    // ---------------- The leaning tree ----------------

    /// <summary>
    /// After rain, a grown tree beside one of the colony's walls is leaning toward it, the ground at its foot heaved up. A
    /// is the colony's best at felling, B someone who'd sooner it stood. {THING}: the tree; the spot: the wall it leans to.
    /// </summary>
    public class HappeningWorker_LeaningTree : HappeningWorker_Danger
    {
        private static bool Wall(Thing b) => b is Building w && w.Faction == Faction.OfPlayer && w.def.building != null && (w.def.IsWall || w.def.IsDoor) && w.def.useHitPoints;

        public static bool TryFind(Map map, out Plant tree, out Building wall)
        {
            tree = null;
            wall = null;
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().InRandomOrder())
            {
                if (!t.Spawned || t.def.plant == null || !t.def.plant.IsTree || t.def.plant.treeCategory != TreeCategory.Full || t.Growth < 0.9f || !home[t.Position]) continue;
                if (t.Position.Fogged(map) || map.designationManager.DesignationOn(t) != null) continue;
                var w = GenAdj.CellsAdjacent8Way(t).Where(c => c.InBounds(map)).Select(c => c.GetEdifice(map)).FirstOrDefault(Wall);
                if (w == null) continue;
                tree = t;
                wall = (Building)w;
                return true;
            }
            return false;
        }

        protected override bool Found(Map map) => TryFind(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!TryFind(ctx.map, out var tree, out var wall)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = VarietyThree.Best(adults, SkillDefOf.Plants, WorkTypeDefOf.PlantCutting) ?? adults.FirstOrDefault();
            ctx.b = adults.Where(p => p != ctx.a).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            if (ctx.a == null || ctx.b == null) return false;
            ctx.place = tree;
            ctx.thing = tree.def;
            ctx.spot = wall.Position;
            ctx.tale = wall.def.IsDoor ? "door" : "wall";
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.tale == "door") tags.Add("door");
        }

        /// <summary>The lean: the ground at the tree's foot heaved up, and the tree kept on the map to be watched.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var tree = ctx.place;
            foreach (var c in GenAdj.CellsAdjacent8Way(tree).Where(c => c.InBounds(ctx.map) && c.Standable(ctx.map)).InRandomOrder().Take(3))
                FilthMaker.TryMakeFilth(c, ctx.map, ThingDefOf.Filth_LooseGround, 1);
            FleckMaker.ThrowDustPuff(tree.Position, ctx.map, 1f);
            MapDangers.Say(MapDangers.Sign(TimedKind.Leaning, ctx, tree), ctx.a, "warn");
            return true;
        }
    }

    /// <summary>
    /// The leaning tree: <see cref="fell"/>, marked to be cut (the game's own felling, safe, the logs the colony's); else
    /// left standing, and for some days a wind may bring it down onto the wall, broken but standing, and onto anyone
    /// beside it. Can't be chosen once the tree is gone.
    /// </summary>
    public class DilemmaEffect_LeaningTree : DilemmaEffect
    {
        public bool fell;

        public override bool ChanceEach => false;

        private static Plant Tree(DilemmaContext ctx) => (ctx as HappeningContext)?.place is Plant t && t.Spawned && t.Map == ctx.map ? t : null;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx is HappeningContext h && h.place != null && Tree(ctx) == null ? "CC_Danger_TreeGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var tree = Tree(ctx);
            if (tree == null) return;
            if (fell)
            {
                sheet.Play("CC_Impact_FellTree".Translate(tree.def.label).Resolve(), "CC_Impact_FellTreeBrief".Translate(tree.def.label).Resolve());
                return;
            }
            sheet.Add(ImpactKind.Map, -1, "CC_Impact_TreeMayFall".Translate(tree.def.label).Resolve(), "CC_Impact_TreeMayFallBrief".Translate().Resolve(), "treefall");
            if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_TreeRisk".Translate().Resolve(), "CC_Impact_TreeRiskBrief".Translate().Resolve(), "treerisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var tree = Tree(ctx);
            if (tree == null) return;
            try
            {
                var k = MapDangers.Of(TimedKind.Leaning, ctx);
                if (fell)
                {
                    FieldBlight.Mark(ctx.map, tree, DesignationDefOf.CutPlant);
                    if (k != null) k.count = 2;
                    return;
                }
                if (k == null) return;
                k.count = 1;
                k.until = Find.TickManager.TicksGame + Mathf.RoundToInt(VarietyThreeMath.TreeWatchDays * GenDate.TicksPerDay);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("leaning tree", e);
            }
        }
    }

    // ---------------- Smoke in the dry grass ----------------

    /// <summary>
    /// A hot, dry summer's day after days without rain, and smoke rising from the dry grass a little way past the colony's
    /// edge. A is whoever saw it (the nearest), B someone else. The spot is where it smoulders.
    /// </summary>
    public class HappeningWorker_Smoulder : HappeningWorker_Danger
    {
        private static bool Dry(IntVec3 c, Map map) =>
            c.InBounds(map) && !c.Fogged(map) && !c.Roofed(map) && !map.areaManager.Home[c] && c.GetZone(map) == null && c.GetEdifice(map) == null
            && c.GetPlant(map) is Plant p && !p.def.plant.IsTree && p.def.plant.sowTags.NullOrEmpty() && p.FlammableNow && FireUtility.ChanceToStartFireIn(c, map) > 0f;

        public static bool TryFind(Map map, out IntVec3 spot)
        {
            spot = IntVec3.Invalid;
            if (map.weatherManager.RainRate > 0.01f || map.mapTemperature.OutdoorTemp < 26f) return false;
            var center = StorytellerEvents.Center(map);
            for (int i = 0; i < 40; i++)
            {
                var c = center + GenRadial.RadialPattern[Rand.Range(GenRadial.NumCellsInRadius(10f), GenRadial.NumCellsInRadius(28f))];
                if (!Dry(c, map)) continue;
                spot = c;
                return true;
            }
            return false;
        }

        protected override bool Found(Map map) => TryFind(map, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!TryFind(ctx.map, out var spot)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).OrderBy(p => p.Position.DistanceToSquared(spot)).ToList();
            ctx.a = adults.FirstOrDefault();
            ctx.b = adults.Skip(1).FirstOrDefault();
            if (ctx.a == null || ctx.b == null) return false;
            ctx.spot = spot;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            FleckMaker.ThrowSmoke(ctx.spot.ToVector3Shifted(), ctx.map, 1.5f);
            MapDangers.Say(MapDangers.Sign(TimedKind.Smoulder, ctx), ctx.a, "warn");
            return true;
        }
    }

    /// <summary>
    /// The smoulder: <see cref="stamp"/>, someone goes out to stamp it out (A, or the nearest free), and may burn a hand or a
    /// foot doing it; else it's left, and within a few hours it catches: a grass fire of the game's own, which the colony's
    /// firefighters meet as they meet any fire.
    /// </summary>
    public class DilemmaEffect_Smoulder : DilemmaEffect
    {
        public bool stamp;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext h) || !h.spot.IsValid) return;
            if (stamp)
            {
                sheet.Play("CC_Impact_Stamp".Translate(h.a?.LabelShort ?? "").Resolve(), "CC_Impact_StampBrief".Translate().Resolve());
                if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_StampRisk".Translate().Resolve(), "CC_Impact_StampRiskBrief".Translate().Resolve(), "stamprisk");
                return;
            }
            sheet.Add(ImpactKind.Map, -1, "CC_Impact_SmoulderCatches".Translate().Resolve(), "CC_Impact_SmoulderCatchesBrief".Translate().Resolve(), "flare");
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_FireRisk".Translate().Resolve(), "CC_Impact_FireRiskBrief".Translate().Resolve(), "firerisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            try
            {
                var k = MapDangers.Of(TimedKind.Smoulder, ctx);
                if (k == null) return;
                int now = Find.TickManager.TicksGame;
                if (stamp)
                {
                    k.count = 1;
                    k.pawn = ctx.a;
                    k.until = now + GenDate.TicksPerHour * 6;
                    return;
                }
                k.count = 2;
                k.done = now + Mathf.RoundToInt(Rand.Range(VarietyThreeMath.FlareLowHours, VarietyThreeMath.FlareHighHours) * GenDate.TicksPerHour);
                k.until = k.done + GenDate.TicksPerHour;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("smoulder", e);
            }
        }
    }

    // ---------------- The old shell ----------------

    /// <summary>
    /// Someone of the colony working a field (sowing or bringing in the crop, A) turns up an old shell from some war before
    /// the colony's time: the game's own high-explosive shell, set down where it was found and kept from the haulers. B is
    /// the colony's best shot (who could set it off from a distance). {THING}: the shell.
    /// </summary>
    public class HappeningWorker_OldShell : HappeningWorker_Danger
    {
        public static Pawn Worker(Map map, out IntVec3 at)
        {
            at = IntVec3.Invalid;
            foreach (var p in Colonists(map))
            {
                var d = p.CurJobDef;
                if (!StorytellerEvents.Able(p) || d != JobDefOf.Sow && d != JobDefOf.Harvest && d != JobDefOf.HarvestDesignated) continue;
                var c = p.CurJob.targetA.Cell;
                if (!c.IsValid || !(c.GetZone(map) is Zone_Growing)) continue;
                var free = GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Where(x => x.InBounds(map) && x.Standable(map) && x.GetFirstItem(map) == null && x.GetEdifice(map) == null).DefaultIfEmpty(IntVec3.Invalid).First();
                if (!free.IsValid) continue;
                at = free;
                return p;
            }
            return null;
        }

        protected override bool Found(Map map) => ThingDefOf.Shell_HighExplosive != null && Worker(map, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Worker(ctx.map, out var at);
            if (ctx.a == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.b = VarietyThree.Best(adults.Where(p => !p.WorkTagIsDisabled(WorkTags.Violent)), SkillDefOf.Shooting, null, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Shooting);
            if (ctx.b == null) return false;
            ctx.spot = at;
            ctx.thing = ThingDefOf.Shell_HighExplosive;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var shell = ThingMaker.MakeThing(ThingDefOf.Shell_HighExplosive);
            shell.stackCount = 1;
            if (!GenPlace.TryPlaceThing(shell, ctx.spot, ctx.map, ThingPlaceMode.Direct, out var placed) || placed == null) return false;
            placed.SetForbidden(true, false);
            FilthMaker.TryMakeFilth(ctx.spot, ctx.map, ThingDefOf.Filth_Dirt, 1);
            ctx.place = placed;
            ctx.a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            MapDangers.Say(MapDangers.Sign(TimedKind.Shell, ctx, placed), ctx.a, "warn");
            return true;
        }
    }

    /// <summary>
    /// What becomes of the old shell: <c>carry</c> (carried far past the fields and kept from the haulers there),
    /// <c>blast</c> (set off where it lies, from a safe distance: the game's own blast in the field, and a stone thrown up
    /// may cut whoever set it off), <c>stores</c> (to the stores for the colony's mortars: only with one built) or
    /// <c>leave</c> (where it lies, kept from the haulers). A fire that reaches it sets it off wherever it is, as the game has it.
    /// </summary>
    public class DilemmaEffect_OldShell : DilemmaEffect
    {
        public string mode = "leave";

        public override bool ChanceEach => false;

        private static Thing Shell(DilemmaContext ctx) => (ctx as HappeningContext)?.place is Thing t && t.Spawned && t.Map == ctx.map ? t : null;

        public static bool Mortar(Map map) => map.listerBuildings.allBuildingsColonist.Any(b => b.Spawned && b.TryGetComp<CompChangeableProjectile>() != null);

        /// <summary>Far past the fields: open ground well away from the colony's buildings and fields.</summary>
        private static IntVec3 FarCell(Map map) =>
            StorytellerEvents.TryOpenSpot(map, 25f, 60f, out var c, x => !GenRadial.RadialCellsAround(x, 8f, true).Any(y => y.InBounds(map) && (y.GetEdifice(map)?.Faction == Faction.OfPlayer || y.GetZone(map) != null)))
                ? c : IntVec3.Invalid;

        /// <summary>Where the one setting it off stands: well back, with a clear line to it.</summary>
        private static IntVec3 StandOff(Thing shell, Pawn from)
        {
            var map = shell.Map;
            return GenRadial.RadialCellsAround(shell.Position, VarietyThreeMath.ShellStandOff + 2f, false)
                .Where(c => c.InBounds(map) && c.DistanceTo(shell.Position) >= VarietyThreeMath.ShellStandOff && c.Standable(map) && GenSight.LineOfSight(c, shell.Position, map, true))
                .OrderBy(c => from != null ? c.DistanceToSquared(from.Position) : 0).DefaultIfEmpty(IntVec3.Invalid).First();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.place == null) return null;
            var shell = Shell(ctx);
            if (shell == null) return "CC_Danger_ShellGone".Translate().ToString();
            switch (mode)
            {
                case "stores": return Mortar(h.map) ? null : "CC_Danger_NoMortar".Translate().ToString();
                case "carry": return FarCell(h.map).IsValid ? null : "CC_Danger_NoFarCell".Translate().ToString();
                case "blast": return shell.TryGetComp<CompExplosive>() != null && StandOff(shell, h.b).IsValid ? null : "CC_Danger_NoStandOff".Translate().ToString();
                default: return null;
            }
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var h = ctx as HappeningContext;
            if (h == null || Shell(ctx) == null) return;
            switch (mode)
            {
                case "carry":
                    sheet.Play("CC_Impact_ShellCarry".Translate().Resolve(), "CC_Impact_ShellCarryBrief".Translate().Resolve());
                    sheet.Add(ImpactKind.Map, -1, "CC_Impact_ShellStill".Translate().Resolve(), "CC_Impact_ShellStillBrief".Translate().Resolve(), "shellstill");
                    break;
                case "blast":
                    sheet.Play("CC_Impact_ShellBlast".Translate(h.b?.LabelShort ?? "").Resolve(), "CC_Impact_ShellBlastBrief".Translate().Resolve());
                    if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_ShellRisk".Translate(h.b?.LabelShort ?? "").Resolve(), "CC_Impact_ShellRiskBrief".Translate().Resolve(), "shellrisk");
                    break;
                case "stores":
                    if (!Mortar(h.map)) return;
                    sheet.Play("CC_Impact_ShellStores".Translate().Resolve(), "CC_Impact_ShellStoresBrief".Translate().Resolve());
                    sheet.Add(ImpactKind.Map, -1, "CC_Impact_ShellStill".Translate().Resolve(), "CC_Impact_ShellStillBrief".Translate().Resolve(), "shellstill");
                    break;
                default:
                    sheet.Add(ImpactKind.Map, -1, "CC_Impact_ShellStill".Translate().Resolve(), "CC_Impact_ShellStillBrief".Translate().Resolve(), "shellstill");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var shell = Shell(ctx);
            if (!(ctx is HappeningContext h) || shell == null) return;
            try
            {
                var k = MapDangers.Of(TimedKind.Shell, ctx);
                int now = Find.TickManager.TicksGame;
                switch (mode)
                {
                    case "carry":
                        if (k == null) return;
                        k.cell = FarCell(h.map);
                        k.count = 1;
                        k.pawn = h.a;
                        k.until = now + GenDate.TicksPerDay;
                        return;
                    case "blast":
                        if (k == null) return;
                        k.cell = StandOff(shell, h.b);
                        k.count = 2;
                        k.pawn = h.b;
                        k.until = now + GenDate.TicksPerDay;
                        return;
                    case "stores":
                        shell.SetForbidden(false, false);
                        if (k != null) k.count = 3;
                        return;
                    default:
                        if (k != null) k.count = 3;
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("old shell", e);
            }
        }
    }

    // ---------------- The twitching scrap ----------------

    /// <summary>
    /// The remains of a mechanoid lie out on the colony's land, and as someone passed, it jerked and spat sparks: there's
    /// charge in it still. A saw it, B is the colony's best crafter, who'd break it up. {BEAST}: what it was.
    /// </summary>
    public class HappeningWorker_TwitchingScrap : HappeningWorker_Danger
    {
        public static Corpse Scrap(Map map)
        {
            var center = StorytellerEvents.Center(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                .Where(c => c.Spawned && c.InnerPawn != null && c.InnerPawn.RaceProps.IsMechanoid && !c.Position.Fogged(map) && !c.IsInAnyStorage() && c.Position.DistanceTo(center) <= 60f)
                .OrderBy(c => c.Position.DistanceToSquared(center)).FirstOrDefault();
        }

        protected override bool Found(Map map) => Scrap(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var scrap = Scrap(ctx.map);
            if (scrap == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).OrderBy(p => p.Position.DistanceToSquared(scrap.Position)).ToList();
            ctx.a = adults.FirstOrDefault();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Crafting, null, ctx.a) ?? adults.Skip(1).FirstOrDefault();
            if (ctx.a == null || ctx.b == null) return false;
            ctx.place = scrap;
            ctx.beast = scrap.InnerPawn.KindLabel;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, scrap.Position);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var scrap = ctx.place;
            for (int i = 0; i < 3; i++) FleckMaker.ThrowMicroSparks(scrap.DrawPos, ctx.map);
            FilthMaker.TryMakeFilth(scrap.Position, ctx.map, ThingDefOf.Filth_MachineBits, 1);
            MapDangers.Say(MapDangers.Sign(TimedKind.Scrap, ctx, scrap), ctx.a, "warn");
            return true;
        }
    }

    /// <summary>
    /// The twitching scrap: <see cref="breakUp"/>, B goes and breaks it up where it lies, and may be burned by the last of its
    /// charge; it leaves steel and bits. Else it's left, and for some days the first who handles it (a hauler carrying it
    /// off, anyone at it) may be burned. Can't be chosen once it's gone.
    /// </summary>
    public class DilemmaEffect_Scrap : DilemmaEffect
    {
        public bool breakUp;

        public override bool ChanceEach => false;

        private static Thing Body(DilemmaContext ctx) => (ctx as HappeningContext)?.place is Thing t && !t.Destroyed ? t : null;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx is HappeningContext h && h.place != null && Body(ctx) == null ? "CC_Danger_ScrapGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext h) || Body(ctx) == null) return;
            if (breakUp)
            {
                sheet.Play("CC_Impact_ScrapBreak".Translate(h.b?.LabelShort ?? "").Resolve(), "CC_Impact_ScrapBreakBrief".Translate().Resolve());
                if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_ScrapRisk".Translate(h.b?.LabelShort ?? "").Resolve(), "CC_Impact_ScrapRiskBrief".Translate().Resolve(), "scraprisk");
                return;
            }
            if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_ScrapLeft".Translate().Resolve(), "CC_Impact_ScrapLeftBrief".Translate().Resolve(), "scrapleft");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || Body(ctx) == null) return;
            try
            {
                var k = MapDangers.Of(TimedKind.Scrap, ctx);
                if (k == null) return;
                int now = Find.TickManager.TicksGame;
                k.count = breakUp ? 1 : 2;
                k.pawn = breakUp ? h.b : null;
                k.on = false;
                k.until = now + (breakUp ? GenDate.TicksPerDay : Mathf.RoundToInt(VarietyThreeMath.ScrapLiveDays * GenDate.TicksPerDay));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("twitching scrap", e);
            }
        }
    }

    // ---------------- The ground giving ----------------

    /// <summary>
    /// After days of rain, the ground just outside one of the colony's doors has gone soft and is slumping: natural ground,
    /// unroofed, no floor laid on it. A uses that door most (the nearest), B is the colony's best builder. The cells are
    /// kept on the map.
    /// </summary>
    public class HappeningWorker_SoftGround : HappeningWorker_Danger
    {
        private static bool Soft(IntVec3 c, Map map)
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || c.GetEdifice(map) != null || c.Fogged(map)) return false;
            var t = c.GetTerrain(map);
            return t != null && t.natural && !t.IsWater && !t.IsFloor && (t.fertility > 0f || t == TerrainDefOf.Mud || t == TerrainDefOf.Marsh || t == TerrainDefOf.SoftSand || t == TerrainDefOf.Sand);
        }

        public static bool TryFind(Map map, out Building door, out List<IntVec3> cells)
        {
            door = null;
            cells = null;
            foreach (var d in map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.IsDoor).InRandomOrder())
            {
                var soft = GenRadial.RadialCellsAround(d.Position, 2.9f, false).Where(c => Soft(c, map)).ToList();
                if (soft.Count < 3) continue;
                door = d;
                cells = soft.OrderBy(c => c.DistanceToSquared(d.Position)).Take(5).ToList();
                return true;
            }
            return false;
        }

        protected override bool Found(Map map) => TryFind(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!TryFind(ctx.map, out var door, out var cells)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = adults.OrderBy(p => p.Position.DistanceToSquared(door.Position)).FirstOrDefault();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.a == null || ctx.b == null) return false;
            ctx.place = door;
            ctx.spot = cells[0];
            ctx.count = cells.Count;
            ctx.tale = cells[0].GetTerrain(ctx.map)?.label;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!TryFind(ctx.map, out var door, out var cells) || door != ctx.place) cells = GenRadial.RadialCellsAround(ctx.spot, 1.5f, true).Where(c => c.InBounds(ctx.map) && c.Standable(ctx.map)).Take(4).ToList();
            foreach (var c in cells) FilthMaker.TryMakeFilth(c, ctx.map, ThingDefOf.Filth_LooseGround, 1);
            MapDangers.Say(MapDangers.Sign(TimedKind.SoftGround, ctx, null, cells), ctx.a, "warn");
            return true;
        }
    }

    /// <summary>
    /// The soft ground: <see cref="lay"/>, a floor marked over it (stone flags of a stone the colony has, else planks; the
    /// game's own floor, laid by the builders), which firms it; else it's left, and each of the first few who cross it may
    /// put a foot through. Can't lay a floor the colony can't build or pay for.
    /// </summary>
    public class DilemmaEffect_SoftGround : DilemmaEffect
    {
        public bool lay;

        public override bool ChanceEach => false;

        /// <summary>The floor to lay: one the colony can build now and pay for over these cells, stone first, the cheapest.</summary>
        public static TerrainDef Floor(Map map, int cells)
        {
            if (map == null || cells <= 0) return null;
            return DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(t => t.BuildableByPlayer && t.IsFloor && !t.IsWater && t.IsResearchFinished && !t.CostList.NullOrEmpty() && t.CostList.All(c => map.resourceCounter.GetCount(c.thingDef) >= c.count * cells))
                .OrderByDescending(t => t.CostList.Any(c => c.thingDef.stuffProps?.categories?.Contains(StuffCategoryDefOf.Stony) == true))
                .ThenBy(t => t.CostList.Sum(c => c.thingDef.BaseMarketValue * c.count)).FirstOrDefault();
        }

        /// <summary>The soft cells a floor of that kind can be laid on now.</summary>
        private static List<IntVec3> Layable(DilemmaContext ctx, TerrainDef floor)
        {
            var map = ctx?.map;
            var k = map == null || floor == null ? null : OnMapTimed.Instance?.kept.FirstOrDefault(x => x.kind == TimedKind.SoftGround && x.map == map && (ctx as HappeningContext)?.spot == x.cell);
            if (k == null) return new List<IntVec3>();
            return k.cells.Where(c => c.InBounds(map) && GenConstruct.CanPlaceBlueprintAt(floor, c, Rot4.North, map).Accepted).ToList();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!lay || ctx?.map == null || !(ctx is HappeningContext)) return null;
            var floor = Floor(ctx.map, Math.Max(1, ctx.count));
            return floor == null || Layable(ctx, floor).Count == 0 ? "CC_Danger_NoFloor".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext) || ctx.map == null) return;
            if (lay)
            {
                var floor = Floor(ctx.map, Math.Max(1, ctx.count));
                var layable = Layable(ctx, floor);
                if (floor == null || layable.Count == 0) return;
                sheet.Play("CC_Impact_LayFloor".Translate(floor.label).Resolve(), "CC_Impact_LayFloorBrief".Translate().Resolve());
                int soft = OnMapTimed.Instance?.kept.FirstOrDefault(x => x.kind == TimedKind.SoftGround && x.map == ctx.map && (ctx as HappeningContext)?.spot == x.cell)?.cells.Count ?? 0;
                if (layable.Count < soft && MapDangers.On)
                    sheet.Add(ImpactKind.Health, -1, "CC_Impact_GroundRisk".Translate().Resolve(), "CC_Impact_GroundRiskBrief".Translate().Resolve(), "groundrisk");
                return;
            }
            if (MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_Impact_GroundRisk".Translate().Resolve(), "CC_Impact_GroundRiskBrief".Translate().Resolve(), "groundrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            try
            {
                var k = MapDangers.Of(TimedKind.SoftGround, ctx);
                if (k == null) return;
                if (!lay)
                {
                    k.count = 1;
                    k.until = Find.TickManager.TicksGame + Mathf.RoundToInt(VarietyThreeMath.GroundWatchDays * GenDate.TicksPerDay);
                    return;
                }
                var floor = Floor(h.map, Math.Max(1, k.cells.Count));
                var cells = Layable(ctx, floor);
                foreach (var c in cells) GenConstruct.PlaceBlueprintForBuild(floor, c, h.map, Rot4.North, Faction.OfPlayer, null);
                // Where no floor could go, the ground is still soft: it is watched as if left.
                k.cells.RemoveAll(cells.Contains);
                k.count = k.cells.Count > 0 ? 1 : 2;
                k.until = Find.TickManager.TicksGame + Mathf.RoundToInt(VarietyThreeMath.GroundWatchDays * GenDate.TicksPerDay);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("soft ground", e);
            }
        }
    }
}
