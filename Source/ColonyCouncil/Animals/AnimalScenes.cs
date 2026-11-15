using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
    /// The scenes the colony's animals play out for encounters and personal stories, A the colonist and X the animal:
    /// <list type="bullet">
    /// <item>Greet: X runs to A and nuzzles them (a welcome);</item>
    /// <item>FootOfBed: that night X sleeps at the foot of A's bed, told when A wakes;</item>
    /// <item>Plate: at A's next meal X sits at their elbow, then is off at a run with a bite;</item>
    /// <item>ChildPlay: A (a child) and X play chase;</item>
    /// <item>Shadow: X follows A about a while (at work, from room to room);</item>
    /// <item>Comfort: X comes to A, low as they are, nuzzles them and stays at their side;</item>
    /// <item>Trick: X bounds about in front of A, who stops to watch when they're free;</item>
    /// <item>Larder: X gets into the stores and eats (as a greedy one would);</item>
    /// <item>Confide: in A's free time, A goes and sits with X a while, talking to it;</item>
    /// <item>Pester: X runs at A's heels and off again (a feud's ambush).</item>
    /// </list>
    /// A scene that can't begin now waits (a day and a half at most) for its moment; one that never comes is never told.
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

        /// <summary>
        /// Strangers/Encounters.cs, as an encounter fires: one with an animal's scene plays it out first, and
        /// <paramref name="tell"/> (its words, its moods) runs once it has. False for an encounter without one, or with the
        /// colony's animals' moments off (then it is told as it always was).
        /// </summary>
        public static bool Begin(EncounterDef def, DilemmaContext ctx, Action tell)
        {
            var e = def?.effects?.OfType<DilemmaEffect_AnimalScene>().FirstOrDefault();
            if (e == null || e.scene == AnimalScene.None || !AnimalLife.On || ctx?.a == null || ctx.stranger == null) return false;
            Play(e.scene, ctx.a, ctx.stranger, tell);
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

        private static bool Up(Pawn x) => MoreHabits.Calm(x) && x.GetLord() == null && !(x.roping?.IsRoped ?? false) && x.Awake();

        /// <summary>Plays the scene for <paramref name="a"/> and <paramref name="x"/> now, or waits for its moment; tells it once it has played.</summary>
        internal static void Play(AnimalScene scene, Pawn a, Pawn x, Action tell)
        {
            Fresh();
            if (a == null || x == null || scene == AnimalScene.None) return;
            try
            {
                bool now = Try(scene, a, x, tell, 0);
                // The night at the foot of the bed is told in the morning; the meal, the night and a free hour are waited for.
                if (scene == AnimalScene.FootOfBed) Wait(scene, a, x, tell, now ? 1 : 0);
                else if (!now && (scene == AnimalScene.Plate || scene == AnimalScene.Confide)) Wait(scene, a, x, tell, 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal scenes: " + scene, e);
            }
        }

        private static void Wait(AnimalScene scene, Pawn a, Pawn x, Action tell, int stage)
        {
            waiting.RemoveAll(w => w.x == x);
            waiting.Add(new Waiting { scene = scene, a = a, x = x, tell = tell, stage = stage, until = Now + GenDate.TicksPerDay * 3 / 2 });
            if (waiting.Count > 30) waiting.RemoveAt(0);
        }

        /// <summary>The scene's moment now, if it has come: true when it played (and was told) or, for the night's, began.</summary>
        private static bool Try(AnimalScene scene, Pawn a, Pawn x, Action tell, int stage)
        {
            if (x.Dead || a.Dead || !x.Spawned || !a.Spawned || x.Map != a.Map) return false;
            var map = a.Map;
            if (map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            switch (scene)
            {
                case AnimalScene.Greet:
                    if (!AnimalDays.Greet(x, a)) return false;
                    break;
                case AnimalScene.FootOfBed:
                    // Its night: A asleep in their own bed, at bedtime; told when they wake.
                    if (!AnimalDaysMath.BedHour(GenLocalDate.HourOfDay(map)) || a.Awake() || !MoreHabits.Calm(x) || x.GetLord() != null) return false;
                    return AnimalDays.SleepAtFoot(x, a);
                case AnimalScene.Plate:
                    if (!AnimalDays.Eating(a) || !Up(x) || !Snatch(x, a)) return false;
                    break;
                case AnimalScene.ChildPlay:
                    if (!Up(x) || !AnimalDays.Play(a, x, AnimalDaysMath.PlayTicks)) return false;
                    break;
                case AnimalScene.Shadow:
                    if (!Up(x) || !a.Position.InHorDistOf(x.Position, 40f) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
                    if (!AnimalDays.Start(x, AnimalDays.FollowJob(a, 2.5f, AnimalDaysMath.SideTicks))) return false;
                    AnimalTempers.Noted(x, "CC_AnimalScene_Shadow", a.Named("PAWN"));
                    break;
                case AnimalScene.Comfort:
                    if (!Up(x) || !Comfort(x, a)) return false;
                    break;
                case AnimalScene.Trick:
                    if (!Up(x) || !Trick(a, x)) return false;
                    break;
                case AnimalScene.Larder:
                    if (!Up(x) || !AnimalTempers.RaidStores(x)) return false;
                    break;
                case AnimalScene.Confide:
                    if (!Up(x) || !Together.Able(a) || !Together.FreeTime(a) || a.GetLord() != null || !SitWith(a, x)) return false;
                    break;
                case AnimalScene.Pester:
                    if (!Up(x) || !Pester(x, a)) return false;
                    break;
                default:
                    return false;
            }
            Tell(tell);
            return true;
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
                        // The night is over once they're up again: told then.
                        if (!Together.Able(w.a)) continue;
                        waiting.Remove(w);
                        Tell(w.tell);
                        continue;
                    }
                    if (!Try(w.scene, w.a, w.x, w.tell, 0)) continue;
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

        // ---------------- The scenes ----------------

        /// <summary>It sits at their elbow a moment, then is off at a run a few steps away, as with a bite in its mouth.</summary>
        private static bool Snatch(Pawn x, Pawn a)
        {
            if (!AnimalDays.Beg(x, a, 400)) return false;
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

        /// <summary>It comes to them and nuzzles them, then keeps at their side a while.</summary>
        private static bool Comfort(Pawn x, Pawn a)
        {
            if (!Together.Able(a) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            bool nuzzle = x.interactions != null;
            var job = nuzzle ? JobMaker.MakeJob(JobDefOf.Nuzzle, a) : AnimalDays.FollowJob(a, 1.5f, AnimalDaysMath.SideTicks);
            if (!AnimalDays.Start(x, job)) return false;
            if (nuzzle) x.jobs.jobQueue.EnqueueLast(AnimalDays.FollowJob(a, 1.5f, AnimalDaysMath.SideTicks));
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

        /// <summary>They go and sit with it a while; it stays put for them, facing them.</summary>
        private static bool SitWith(Pawn a, Pawn x)
        {
            if (!a.CanReach(x, PathEndMode.Touch, Danger.None)) return false;
            var sit = JobMaker.MakeJob(AnimalDaysDefOf.Rimfolk_SitWithAnimal, x);
            sit.count = 1000;
            if (!AnimalDays.Start(a, sit)) return false;
            AnimalDays.Start(x, AnimalTempers.Linger(AnimalNatureDefOf.Rimfolk_AnimalLieBy, x.Position, a, 1600, a));
            AnimalTempers.Noted(x, "CC_AnimalScene_Confide", a.Named("PAWN"));
            return true;
        }

        /// <summary>It runs at their heels a moment, then off and away (a feud's ambush; nobody is hurt).</summary>
        private static bool Pester(Pawn x, Pawn a)
        {
            if (!Together.Able(a) || !a.Position.InHorDistOf(x.Position, 40f) || !x.CanReach(a, PathEndMode.Touch, Danger.Some)) return false;
            if (!AnimalDays.Start(x, AnimalDays.FollowJob(a, 1.2f, 360, LocomotionUrgency.Jog))) return false;
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

        /// <summary>The dev action: the scene now for this colonist and animal, past its moment where it can be (the night's waits for the night).</summary>
        public static string DebugPlay(AnimalScene scene, Pawn a, Pawn x)
        {
            if (a == null || x == null) return "pick a colonist and an animal.";
            Fresh();
            bool told = false;
            Play(scene, a, x, () => told = true);
            bool waits = waiting.Any(w => w.x == x && w.a == a);
            return told ? "the scene played; its words would be told now." : waits ? "the scene waits for its moment (" + scene + ")." : "the scene couldn't play now (" + scene + ").";
        }
    }
}
