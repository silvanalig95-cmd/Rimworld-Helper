using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// An animal's scene (<see cref="AnimalScene"/>) in an encounter or a personal story's step: in an encounter
    /// (Defs/EncounterDefs/Council_Encounters_Creatures.xml) it is played out on the map first and its words are told once it
    /// has (Strangers/Encounters.cs calls <see cref="AnimalScenes.Begin"/>), so what the news says is what was seen; in a
    /// story's step it simply plays out as the step begins.
    /// </summary>
    public class DilemmaEffect_AnimalScene : DilemmaEffect
    {
        public AnimalScene scene = AnimalScene.None;

        public override void Apply(DilemmaContext ctx)
        {
            // An encounter's scene was played before its words were told (AnimalScenes.Begin): not a second time.
            if (ctx == null || AnimalScenes.Telling) return;
            AnimalScenes.Play(scene, ctx.a, ctx.pet ?? ctx.stranger, null);
        }
    }

    /// <summary>
    /// The scenes the colony's animals play out for encounters and personal stories, A the colonist and X the animal. Each
    /// is told as the player can see it: as X reaches A, as A sits down by X, or as the scene starts where it is all at once.
    /// <list type="bullet">
    /// <item>Greet: X runs to A and nuzzles them (a welcome), told as it reaches them;</item>
    /// <item>SlowGreet: an old one walks over to A, slowly, and stays at their side, told as it reaches them;</item>
    /// <item>FootOfBed: that night X sleeps at the foot of A's bed, told when A wakes;</item>
    /// <item>Plate: at A's next meal X sits at their elbow, then is off at a run, told as it sits down;</item>
    /// <item>ChildPlay: A (a child) and X play chase, told as they start;</item>
    /// <item>Shadow: X keeps at A's side a while (at work, from room to room), told as it reaches them;</item>
    /// <item>Comfort: X comes to A, low as they are, nuzzles them and stays at their side, told as it reaches them;</item>
    /// <item>Trick: X bounds about in front of A, who stops to watch when they're free, told as it starts;</item>
    /// <item>Larder: X makes for the stores and eats there (as a greedy one would), told as it sets off;</item>
    /// <item>Confide: in A's free time, A goes and sits with X a while, told as A sits down;</item>
    /// <item>Pester: X runs at A's heels and off again (a grudge, a feud's ambush), told as it reaches them;</item>
    /// <item>Litter: X lies down by its newborns, which keep close to it, and A comes over in their free time to look at
    /// them, told as A gets there;</item>
    /// <item>Guard: a stranger near A, X goes and stands between them, facing the stranger, told as it takes its place;</item>
    /// <item>Cry: at night X goes out a little way, stands facing the dark and cries out again and again, told at its first
    /// cry.</item>
    /// </list>
    /// A scene that can't begin now waits (a day and a half at most) for its moment; one that never comes is never told. A
    /// scene whose words need something that isn't there (newborns, a stranger) is not cast at all (<see cref="Fits"/>).
    /// What waits is not saved.
    /// </summary>
    public static class AnimalScenes
    {
        private sealed class Waiting
        {
            public AnimalScene scene;
            public Pawn a, x;
            public Action tell;
            public int until;
            /// <summary>0: waiting for its moment; 1: begun (X asleep at the foot of the bed), waiting for its end.</summary>
            public int stage;
        }

        private static readonly List<Waiting> waiting = new List<Waiting>();
        private static Game game;

        /// <summary>An encounter's words are being told now, after its scene (its own effect does nothing then).</summary>
        public static bool Telling { get; private set; }

        private static int Now => Find.TickManager.TicksGame;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            waiting.Clear();
        }

        private static DilemmaEffect_AnimalScene SceneOf(EncounterDef def) => def?.effects?.OfType<DilemmaEffect_AnimalScene>().FirstOrDefault();

        /// <summary>
        /// Strangers/Encounters.cs, as an encounter fires: one with an animal's scene plays it out first, and
        /// <paramref name="tell"/> (its words, its moods) runs once it has. False for an encounter without one, or with the
        /// colony's animals' moments off (then it is told as it always was).
        /// </summary>
        public static bool Begin(EncounterDef def, DilemmaContext ctx, Action tell)
        {
            var e = SceneOf(def);
            if (e == null || e.scene == AnimalScene.None || !AnimalLife.On || ctx?.a == null || ctx.stranger == null) return false;
            Play(e.scene, ctx.a, ctx.stranger, tell);
            return true;
        }

        /// <summary>
        /// Strangers/Encounters.cs, as it casts an encounter: false when its words need something that isn't there, whether
        /// or not the scene plays (X's newborns for the new litter, a stranger near A for the one on guard), so it is never
        /// told without it.
        /// </summary>
        public static bool Fits(EncounterDef def, Pawn a, Pawn x)
        {
            var e = SceneOf(def);
            if (e == null || a == null || x == null) return true;
            try
            {
                switch (e.scene)
                {
                    case AnimalScene.Litter: return YoungOf(x).Count > 0;
                    case AnimalScene.Guard: return StrangerNear(a) != null;
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("animal scenes: fits", ex);
                return false;
            }
            return true;
        }

        private static void Tell(Action tell)
        {
            if (tell == null) return;
            Telling = true;
            try
            {
                tell();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal scenes: telling", e);
            }
            finally
            {
                Telling = false;
            }
        }

        /// <summary>Its words, told once at most: as the scene is seen, or never.</summary>
        private static Action Once(Action tell)
        {
            if (tell == null) return null;
            bool told = false;
            return () =>
            {
                if (told) return;
                told = true;
                Tell(tell);
            };
        }

        private static bool Up(Pawn x) => MoreHabits.Calm(x) && x.GetLord() == null && !(x.roping?.IsRoped ?? false) && x.Awake() && !AnimalDays.Ridden(x);

        /// <summary>Plays the scene for <paramref name="a"/> and <paramref name="x"/> now, or waits for its moment; tells it once it is seen.</summary>
        internal static void Play(AnimalScene scene, Pawn a, Pawn x, Action tell)
        {
            Fresh();
            if (a == null || x == null || scene == AnimalScene.None) return;
            try
            {
                var once = Once(tell);
                bool now = Try(scene, a, x, once);
                // The night at the foot of the bed is told in the morning; a meal, a night and a free hour are waited for.
                if (scene == AnimalScene.FootOfBed) Wait(scene, a, x, once, now ? 1 : 0);
                else if (!now && Waits(scene)) Wait(scene, a, x, once, 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal scenes: " + scene, e);
            }
        }

        /// <summary>The scenes that wait for their moment: a meal, a free hour, the night.</summary>
        private static bool Waits(AnimalScene scene) => scene == AnimalScene.Plate || scene == AnimalScene.Confide || scene == AnimalScene.Litter || scene == AnimalScene.Cry;

        private static void Wait(AnimalScene scene, Pawn a, Pawn x, Action tell, int stage)
        {
            waiting.RemoveAll(w => w.x == x);
            waiting.Add(new Waiting { scene = scene, a = a, x = x, tell = tell, stage = stage, until = Now + GenDate.TicksPerDay * 3 / 2 });
            if (waiting.Count > 30) waiting.RemoveAt(0);
        }

        /// <summary>
        /// The scene's moment now, if it has come: true when it began. <paramref name="tell"/> is told as it is seen: at once
        /// for those seen as they start, as X reaches A (or A sits down by X) for the rest; the night's at the foot of the bed
        /// is told by <see cref="Hourly"/> in the morning.
        /// </summary>
        private static bool Try(AnimalScene scene, Pawn a, Pawn x, Action tell)
        {
            if (x.Dead || a.Dead || !x.Spawned || !a.Spawned || x.Map != a.Map) return false;
            var map = a.Map;
            if (map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            switch (scene)
            {
                case AnimalScene.Greet:
                    return AnimalDays.Greet(x, a, tell, false, "CC_AnimalScene_Greet");
                case AnimalScene.SlowGreet:
                    return AnimalDays.Greet(x, a, tell, true, "CC_AnimalScene_SlowGreet");
                case AnimalScene.FootOfBed:
                    // Its night: A asleep in their own bed, at bedtime; told when they wake.
                    if (!AnimalDaysMath.BedHour(GenLocalDate.HourOfDay(map)) || a.Awake() || !MoreHabits.Calm(x) || x.GetLord() != null || AnimalDays.Ridden(x)) return false;
                    return AnimalDays.SleepAtFoot(x, a);
                case AnimalScene.Plate:
                    return AnimalDays.Eating(a) && Up(x) && Snatch(x, a, tell);
                case AnimalScene.ChildPlay:
                    if (!Up(x) || !AnimalDays.Play(a, x, AnimalDaysMath.PlayTicks)) return false;
                    tell?.Invoke();
                    return true;
                case AnimalScene.Shadow:
                    return Up(x) && Shadow(x, a, tell);
                case AnimalScene.Comfort:
                    return Up(x) && Comfort(x, a, tell);
                case AnimalScene.Trick:
                    if (!Up(x) || !Trick(a, x)) return false;
                    tell?.Invoke();
                    return true;
                case AnimalScene.Larder:
                    // Only the stores, as its words say (not a table).
                    if (!Up(x) || !AnimalTempers.RaidStores(x)) return false;
                    tell?.Invoke();
                    return true;
                case AnimalScene.Confide:
                    return Up(x) && Together.Able(a) && Together.FreeTime(a) && a.GetLord() == null && SitWith(a, x, tell);
                case AnimalScene.Pester:
                    return Up(x) && Pester(x, a, tell);
                case AnimalScene.Litter:
                    return Up(x) && Together.Able(a) && Together.FreeTime(a) && a.GetLord() == null && Litter(a, x, tell);
                case AnimalScene.Guard:
                    return Up(x) && Guard(x, a, tell);
                case AnimalScene.Cry:
                    return AnimalDaysMath.CryHour(GenLocalDate.HourOfDay(map)) && Up(x) && Cry(x, a, tell);
                default:
                    return false;
            }
        }

        /// <summary>Once an hour (AnimalLife.Hourly): the scenes waiting for their moment, and the nights at the foot of a bed waiting for the morning.</summary>
        public static void Hourly()
        {
            if (!AnimalLife.On) return;
            Fresh();
            foreach (var w in waiting.ToList())
            {
                try
                {
                    if (w.a == null || w.x == null || w.a.Dead || w.x.Dead || Now > w.until)
                    {
                        waiting.Remove(w);
                        continue;
                    }
                    if (w.stage == 1)
                    {
                        // The night is over once they're up again in the morning (not woken in the small hours): told then.
                        var at = w.a.MapHeld;
                        int hour = at != null ? GenLocalDate.HourOfDay(at) : 12;
                        if (!Together.Able(w.a) || hour < 5 || hour >= 22) continue;
                        waiting.Remove(w);
                        w.tell?.Invoke();
                        continue;
                    }
                    if (!Try(w.scene, w.a, w.x, w.scene == AnimalScene.FootOfBed ? null : w.tell)) continue;
                    if (w.scene == AnimalScene.FootOfBed) w.stage = 1;
                    else waiting.Remove(w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("animal scenes: waiting", e);
                    waiting.Remove(w);
                }
            }
        }

        // ---------------- What the scenes need ----------------

        /// <summary>X's own newborns on its map: young of its kind born within AnimalDaysMath.LitterDays, its mother or father X.</summary>
        internal static List<Pawn> YoungOf(Pawn x)
        {
            var list = new List<Pawn>();
            if (x?.Map == null) return list;
            foreach (var y in x.Map.mapPawns.SpawnedColonyAnimals)
            {
                if (y == x || y.ageTracker == null || !AnimalFacts.Young(y)) continue;
                if (!AnimalDaysMath.Newborn(y.ageTracker.AgeBiologicalTicks / (float)GenDate.TicksPerDay, y.def == x.def)) continue;
                if (y.GetMother() == x || y.GetFather() == x) list.Add(y);
            }
            return list;
        }

        /// <summary>
        /// Someone not of the colony close to A and in sight of them (a visitor, a trader, a guest), up and not hostile:
        /// nearest first. Null when there is none.
        /// </summary>
        internal static Pawn StrangerNear(Pawn a)
        {
            if (a?.Map == null || !a.Spawned) return null;
            return a.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.Dead && !p.Downed && p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony && !p.IsSlaveOfColony
                            && !p.HostileTo(Faction.OfPlayer) && p.Position.InHorDistOf(a.Position, AnimalDaysMath.GuardRange)
                            && GenSight.LineOfSight(a.Position, p.Position, a.Map, true))
                .OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
        }

        // ---------------- The scenes ----------------

        /// <summary>It sits at their elbow a moment, then is off at a run a few steps away, as with a bite in its mouth; told as it sits down.</summary>
        private static bool Snatch(Pawn x, Pawn a, Action tell)
        {
            if (!AnimalDays.Beg(x, a, 400, tell)) return false;
            var map = x.Map;
            if (CellFinder.TryFindRandomCellNear(a.Position, map, 12, c => c.Standable(map) && !c.InHorDistOf(a.Position, 6f) && x.CanReach(c, PathEndMode.OnCell, Danger.Some), out var away))
            {
                var run = JobMaker.MakeJob(JobDefOf.Goto, away);
                run.locomotionUrgency = LocomotionUrgency.Jog;
                x.jobs.jobQueue.EnqueueLast(run);
            }
            AnimalTempers.Noted(x, "CC_AnimalScene_Plate", a.Named("PAWN"));
            return true;
        }

        /// <summary>It goes to them and keeps at their side a while, wherever they go; told as it reaches them.</summary>
        private static bool Shadow(Pawn x, Pawn a, Action tell)
        {
            if (!a.Position.InHorDistOf(x.Position, 40f) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            var job = AnimalDays.RunToJob(AnimalDaysDefOf.Rimfolk_AnimalKeepClose, a, LocomotionUrgency.Walk, AnimalDaysMath.SideTicks);
            if (!AnimalDays.StartThen(x, job, tell)) return false;
            AnimalTempers.Noted(x, "CC_AnimalScene_Shadow", a.Named("PAWN"));
            return true;
        }

        /// <summary>It comes to them and nuzzles them, then keeps at their side a while; told as it reaches them.</summary>
        private static bool Comfort(Pawn x, Pawn a, Action tell)
        {
            if (!Together.Able(a) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            var job = AnimalDays.RunToJob(AnimalDaysDefOf.Rimfolk_AnimalGreet, a, LocomotionUrgency.Walk, AnimalDaysMath.GreetTicks);
            if (!AnimalDays.StartThen(x, job, tell)) return false;
            x.jobs.jobQueue.EnqueueLast(AnimalDays.RunToJob(AnimalDaysDefOf.Rimfolk_AnimalKeepClose, a, LocomotionUrgency.Walk, AnimalDaysMath.SideTicks));
            AnimalTempers.Noted(x, "CC_AnimalScene_Comfort", a.Named("PAWN"));
            return true;
        }

        /// <summary>It bounds about in front of them; in their free time they stop and watch it.</summary>
        private static bool Trick(Pawn a, Pawn x)
        {
            if (!Together.Able(a) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            var bound = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_AnimalPlay, a, LocalTargetInfo.Invalid, a);
            bound.count = 900;
            bound.locomotionUrgency = LocomotionUrgency.Jog;
            if (!AnimalDays.Start(x, bound)) return false;
            if (Together.FreeTime(a) && a.GetLord() == null)
            {
                var watch = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_SitWithAnimal, x);
                watch.count = 800;
                AnimalDays.Start(a, watch);
            }
            AnimalTempers.Noted(x, "CC_AnimalScene_Trick", a.Named("PAWN"));
            return true;
        }

        /// <summary>They go and sit with it a while; it lies where it is for them, facing them. Told as they sit down.</summary>
        private static bool SitWith(Pawn a, Pawn x, Action tell)
        {
            if (!a.CanReach(x, PathEndMode.Touch, Danger.None)) return false;
            var sit = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_SitWithAnimal, x);
            sit.count = 1000;
            if (!AnimalDays.StartThen(a, sit, tell)) return false;
            AnimalDays.Start(x, AnimalTempers.Linger(AnimalNatureDefOf.Rimfolk_AnimalLieBy, x.Position, a, 1600, a));
            AnimalTempers.Noted(x, "CC_AnimalScene_Confide", a.Named("PAWN"));
            return true;
        }

        /// <summary>It runs at their heels a moment, then off and away (a grudge, a feud's ambush; nobody is hurt); told as it reaches them.</summary>
        private static bool Pester(Pawn x, Pawn a, Action tell)
        {
            if (!Together.Able(a) || !a.Position.InHorDistOf(x.Position, 40f) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            if (!AnimalDays.StartThen(x, AnimalDays.RunToJob(AnimalDaysDefOf.Rimfolk_AnimalPester, a, LocomotionUrgency.Jog, 90), tell)) return false;
            var map = x.Map;
            if (CellFinder.TryFindRandomCellNear(a.Position, map, 14, c => c.Standable(map) && !c.InHorDistOf(a.Position, 7f) && x.CanReach(c, PathEndMode.OnCell, Danger.Some), out var away))
            {
                var run = JobMaker.MakeJob(JobDefOf.Goto, away);
                run.locomotionUrgency = LocomotionUrgency.Jog;
                x.jobs.jobQueue.EnqueueLast(run);
            }
            AnimalTempers.Noted(x, "CC_AnimalScene_Pester", a.Named("PAWN"));
            return true;
        }

        /// <summary>
        /// It lies down by its newborns (four of them at most keep close to it), and A comes over in their free time to look
        /// at them, told as A gets there.
        /// </summary>
        private static bool Litter(Pawn a, Pawn x, Action tell)
        {
            var young = YoungOf(x);
            if (young.Count == 0 || !a.CanReach(x, PathEndMode.Touch, Danger.None)) return false;
            var first = young.OrderBy(y => y.Position.DistanceToSquared(x.Position)).First();
            if (!AnimalTempers.CellNear(x, first.Position, 1, out var cell)) return false;
            // The colonist first: without them going over, there is nothing to tell, and it waits.
            var look = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_SitWithAnimal, x);
            look.count = AnimalDaysMath.WatchTicks;
            if (!AnimalDays.StartThen(a, look, tell)) return false;
            AnimalDays.Start(x, AnimalTempers.Linger(AnimalNatureDefOf.Rimfolk_AnimalLieBy, cell, first, AnimalDaysMath.LitterTicks, first));
            foreach (var y in young.Take(4))
                if (MoreHabits.Calm(y) && y.GetLord() == null && y.CanReach(x, PathEndMode.Touch, Danger.Some))
                    AnimalDays.Start(y, AnimalDays.FollowJob(x, 1.9f, AnimalDaysMath.LitterTicks));
            AnimalTempers.Noted(x, "CC_AnimalScene_Litter", a.Named("PAWN"));
            return true;
        }

        /// <summary>A stranger near A: it goes and stands between them, facing the stranger, a while; told as it takes its place.</summary>
        private static bool Guard(Pawn x, Pawn a, Action tell)
        {
            var s = StrangerNear(a);
            if (s == null || !Together.Able(a) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            var from = a.Position.ToVector3Shifted();
            var dir = s.Position.ToVector3Shifted() - from;
            if (dir.sqrMagnitude < 1f) return false;
            dir.Normalize();
            var at = (from + dir * 1.6f).ToIntVec3();
            if (!AnimalTempers.CellNear(x, at, 1, out var cell)) return false;
            if (!AnimalDays.StartThen(x, AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalGuard, cell, s, AnimalDaysMath.GuardTicks, a), tell)) return false;
            AnimalTempers.Noted(x, "CC_AnimalScene_Guard", a.Named("PAWN"), s.Named("OTHER"));
            return true;
        }

        /// <summary>
        /// At night it goes a few steps further out from A, under the open sky, stands facing the dark and cries out (its kind's
        /// own call, Rimfolk_AnimalCry) again and again; told at its first cry.
        /// </summary>
        private static bool Cry(Pawn x, Pawn a, Action tell)
        {
            var map = x.Map;
            var away = x.Position.ToVector3Shifted() - a.Position.ToVector3Shifted();
            if (away.sqrMagnitude < 1f) away = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            away.Normalize();
            var at = (x.Position.ToVector3Shifted() + away * 6f).ToIntVec3();
            if (!AnimalTempers.CellNear(x, at.ClampInsideMap(map), 4, out var cell, c => !c.Roofed(map))
                && !AnimalTempers.CellNear(x, x.Position, 3, out cell, c => !c.Roofed(map))) return false;
            var facing = (cell.ToVector3Shifted() + away * 20f).ToIntVec3().ClampInsideMap(map);
            if (!AnimalDays.StartThen(x, AnimalTempers.Linger(AnimalDaysDefOf.Rimfolk_AnimalCry, cell, facing, AnimalDaysMath.CryTicks), tell)) return false;
            AnimalTempers.Noted(x, "CC_AnimalScene_Cry");
            return true;
        }

        /// <summary>The dev action: the scene now for this colonist and animal, past its moment where it can be (the night's waits for the night).</summary>
        public static string DebugPlay(AnimalScene scene, Pawn a, Pawn x)
        {
            if (a == null || x == null) return "pick a colonist and an animal.";
            Fresh();
            bool told = false;
            Play(scene, a, x, () => told = true);
            bool waits = waiting.Any(w => w.x == x && w.a == a);
            bool going = x.CurJob != null && (x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalGreet || x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalKeepClose
                                              || x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalPester || x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalGuard
                                              || x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalCry || x.CurJobDef == AnimalDaysDefOf.Rimfolk_AnimalBeg)
                         || a.CurJobDef == AnimalDaysDefOf.Rimfolk_SitWithAnimal;
            return told ? "the scene played; its words would be told now."
                : going ? "the scene began; its words would be told as it is seen (" + scene + ")."
                : waits ? "the scene waits for its moment (" + scene + ")."
                : "the scene couldn't play now (" + scene + "; newborns of its own for a litter, a stranger near for the guard, the night for a cry).";
        }
    }
}
