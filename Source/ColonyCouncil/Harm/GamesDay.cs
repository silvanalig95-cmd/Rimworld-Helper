using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A games day's footrace (Harm/GamesDay.cs): who runs, from where to where, how it's going. Not saved: a reload ends it.</summary>
    public sealed class GamesRace
    {
        public Map map;
        public List<Pawn> runners = new List<Pawn>();
        public bool rough;
        public bool go;
        public int made;
        public Pawn winner;
        public bool over;
        public readonly HashSet<Pawn> turned = new HashSet<Pawn>();
        public readonly HashSet<Pawn> ready = new HashSet<Pawn>();

        public bool Has(Pawn p) => p != null && runners.Contains(p);
    }

    /// <summary>
    /// Games with real contests (the harm review's 4.1.1): a games day kept with a gathering (answers on the map, the
    /// calendar) gets its contests near where the colony gathers: up to two bouts between grown-ups who can fight (the
    /// drills' bout, six pulled swings each, one in 25 landing a real blow) and a footrace for up to four, children too,
    /// out to a mark fourteen cells off and back, one runner in eight turning an ankle on rough ground (a floor is safe; a
    /// child only scrapes a knee, the mildest). By the colony's means: wrestling and a footrace (tribal), blunt staves and a
    /// race (medieval), boxing and a three-legged race (industrial), a sim tourney with nothing to fear but a wrenched
    /// wrist and no race (spacer). About one small hurt in two games days. Seen on the map, no message (the game gives none
    /// for a bout's bruise); named in the day's letter. With the calendar and answers on the map.
    /// </summary>
    public static class GamesDay
    {
        private static readonly List<GamesRace> races = new List<GamesRace>();
        private static JobDef raceJob;
        private static bool debugSprain;

        public static JobDef RaceJob => raceJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_GamesRace");

        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            races.Clear();
            raceJob = null;
        }

        public static GamesRace RaceOf(Pawn p)
        {
            for (int i = 0; i < races.Count; i++)
                if (!races[i].over && races[i].Has(p)) return races[i];
            return null;
        }

        /// <summary>The calendar kept a games day with a gathering (CalendarKeeping.DoWay): its contests wait for it.</summary>
        public static void Kept(Map map, string label)
        {
            if (!Harm.On || map == null || !Harm.SystemOn(HarmKinds.Games.system)) return;
            HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Games, kind = HarmKinds.Games.key, id = map.uniqueID, name = label, made = Now });
        }

        /// <summary>The hour: once the day's gathering is under way, the contests are called near it. True when done with.</summary>
        public static bool Hour(HarmWaiting x, int now)
        {
            races.RemoveAll(r => r.over || now - r.made > 2 * HarmMath.HourTicks);
            var map = Find.Maps.FirstOrDefault(m => m.uniqueID == x.id);
            var tracker = PlayOutTracker.Instance;
            if (map == null || tracker == null || !PlayOuts.On) return true;
            var play = tracker.plays.FirstOrDefault(p => p != null && p.map == map && p.kind == PlayKind.Gathering && p.state == PlayState.Running && p.label == x.name);
            if (play == null) return false;
            var lord = map.lordManager.lords.FirstOrDefault(l => l?.LordJob is LordJob_PlayOut j && j.playId == play.id);
            if (lord == null || MapFacts.ThreatOn(map)) return false;
            var spot = ((LordJob_PlayOut)lord.LordJob).Spot;
            var people = lord.ownedPawns.Where(p => HarmFour.Ours(p) && Together.Able(p) && DrillBouts.Of(p) == null && RaceOf(p) == null).InRandomOrder().ToList();
            if (people.Count < 2 || !spot.IsValid) return true;
            Stage(map, spot, people);
            return true;
        }

        private static void Stage(Map map, IntVec3 spot, List<Pawn> people)
        {
            var age = Ages.Ours;
            var fighters = people.Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && !(p.skills?.GetSkill(SkillDefOf.Melee)?.TotallyDisabled ?? true))
                .Take(2 * HarmFourMath.GamesPairs).ToList();
            var taken = new List<IntVec3>();
            Pawn caller = null;
            var inBouts = new HashSet<Pawn>();
            for (int i = 0; i + 1 < fighters.Count; i += 2)
            {
                var a = fighters[i];
                var b = fighters[i + 1];
                if (!DrillBouts.Places(map, spot, a, b, taken, out var ra, out var rb) || !DrillBouts.Begin(map, a, b, ra, rb, false, false, true)) continue;
                inBouts.Add(a);
                inBouts.Add(b);
                caller ??= a;
            }
            // A spacer colony's sim tourney has no race.
            if (age != WorldAge.Spacer && RaceJob != null)
            {
                var runners = people.Where(p => !inBouts.Contains(p) && !p.DevelopmentalStage.Baby() && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                    .Take(HarmFourMath.GamesRunners).ToList();
                if (runners.Count >= 2 && Race(map, spot, runners)) caller ??= runners[0];
            }
            // apart: the call goes out to everyone at the gathering; they take their places.
            if (caller != null) HarmFour.Say(caller, "Rimfolk_Pool_GamesCall", null, Ages.Tag(age));
        }

        // ================= The race =================

        /// <summary>A course out in the open near the gathering: a line of fourteen clear cells, a lane for each runner beside it.</summary>
        private static bool Course(Map map, IntVec3 spot, int lanes, Pawn first, out IntVec3 start, out IntVec3 dir, out IntVec3 side, out bool rough)
        {
            start = IntVec3.Invalid;
            dir = side = IntVec3.Zero;
            rough = false;
            var dirs = new[] { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
            int looked = 0;
            foreach (var c in GenRadial.RadialCellsAround(spot, 16f, true))
            {
                if (++looked > 250) break;
                if (!c.InBounds(map) || c.Roofed(map) || !Open(c, map)) continue;
                foreach (var d in dirs)
                {
                    var s = new IntVec3(d.z, 0, -d.x);
                    int natural = 0, total = 0;
                    bool ok = true;
                    for (int lane = 0; lane < lanes && ok; lane++)
                        for (int k = 0; k <= HarmFourMath.RaceLength && ok; k++)
                        {
                            var cell = c + s * lane + d * k;
                            if (!cell.InBounds(map) || !Open(cell, map)) ok = false;
                            else
                            {
                                total++;
                                if (!(cell.GetTerrain(map)?.IsFloor ?? false)) natural++;
                            }
                        }
                    // Each lane's line and mark a runner can reach and may stand on (the course between is open ground).
                    for (int lane = 0; lane < lanes && ok; lane++)
                        if (!Clear(c + s * lane, map, first) || !Clear(c + s * lane + d * HarmFourMath.RaceLength, map, first)) ok = false;
                    if (!ok) continue;
                    start = c;
                    dir = d;
                    side = s;
                    rough = total > 0 && natural >= total * HarmFourMath.RoughShare;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Open ground to run over: standable, nothing built on it, not water.</summary>
        private static bool Open(IntVec3 c, Map map) => c.Standable(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false);

        private static bool Clear(IntVec3 c, Map map, Pawn p) => Open(c, map) && PlaySpots.Ok(c, map, p) && p.CanReach(c, PathEndMode.OnCell, Danger.None);

        private static bool Race(Map map, IntVec3 spot, List<Pawn> runners)
        {
            if (!Course(map, spot, runners.Count, runners[0], out var start, out var dir, out var side, out bool rough)) return false;
            var race = new GamesRace { map = map, rough = rough, made = Now };
            races.Add(race);
            for (int i = 0; i < runners.Count; i++)
            {
                var job = JobMaker.MakeJob(RaceJob, start + side * i, start + side * i + dir * HarmFourMath.RaceLength);
                runners[i].jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (runners[i].CurJobDef == RaceJob) race.runners.Add(runners[i]);
            }
            if (race.runners.Count >= 2) return true;
            foreach (var r in race.runners)
                if (r.CurJobDef == RaceJob)
                    r.jobs.EndCurrentJob(JobCondition.InterruptForced);
            race.over = true;
            return false;
        }

        /// <summary>A runner is at the line: once all are (or the wait is long), one of them calls the start.</summary>
        public static void AtLine(GamesRace race, Pawn p, int waited)
        {
            if (race == null || race.go) return;
            race.ready.Add(p);
            if (race.ready.Count < race.runners.Count(r => r.CurJobDef == RaceJob) && waited < 1500) return;
            race.go = true;
            // apart: the start is called to everyone on the line.
            HarmFour.Say(race.runners.FirstOrDefault(r => r.Spawned) ?? p, "Rimfolk_Pool_RaceGo", null, Ages.Tag(Ages.Ours));
        }

        /// <summary>
        /// A runner turns at the mark: on rough ground, the race's one roll for them (an ankle turned, or a child's scraped
        /// knee). True when it ended their race.
        /// </summary>
        public static bool Turned(GamesRace race, Pawn p)
        {
            if (race == null || !race.turned.Add(p)) return false;
            var cls = HarmFour.Child(p) ? HurtClass.Mild : HurtClass.Injury;
            bool debug = debugSprain;
            float chance = HarmFourMath.RaceSprain(race.rough || debug, Ages.Ours);
            bool hurt = debug ? Harm.May(p, cls) : chance > 0f && Harm.Roll(p, HarmKinds.Games, chance, cls);
            if (debug) debugSprain = false;
            if (!hurt) return false;
            bool scrape = cls == HurtClass.Mild;
            Hediff h = null;
            if (!scrape)
            {
                var sprain = HarmFour.Def("Council_Ail_SprainedAnkle");
                var info = AilmentTracker.InfoOf(sprain);
                if (sprain != null && AilmentTracker.On && !p.health.hediffSet.HasHediff(sprain))
                {
                    h = Harm.Ail(p, sprain, info?.startSeverity.RandomInRange ?? 0.4f, HarmKinds.Games, HurtClass.Injury);
                    if (h != null && info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
                }
            }
            if (h == null) h = Harm.Injure(p, scrape ? HurtKind.Scratch : HurtKind.Bruise, HurtWhere.Leg, HarmFourMath.ScrapeLow, scrape ? HarmFourMath.ScrapeHigh : 4f, HarmKinds.Games, cls);
            if (h == null) return false;
            HarmFour.Say(p, "Rimfolk_Pool_RaceTurned", null, scrape ? "scrape" : "ankle");
            HarmFour.Mote(p, scrape ? "CC_HarmFour_Mote_Scrape" : "CC_HarmFour_Mote_Ankle");
            return true;
        }

        /// <summary>A runner is back at the line: the first one home wins.</summary>
        public static void Back(GamesRace race, Pawn p)
        {
            if (race == null || race.winner != null) return;
            race.winner = p;
            HarmFour.Say(p, "Rimfolk_Pool_RaceWon", null, Ages.Tag(Ages.Ours));
            p.needs?.joy?.GainJoy(0.1f, JoyKindDefOf.Social);
        }

        /// <summary>A runner's race is done (finished or cut short): the race is over once all are.</summary>
        public static void Done(GamesRace race, Pawn p)
        {
            if (race == null) return;
            race.runners.Remove(p);
            if (race.runners.Count == 0) race.over = true;
        }

        /// <summary>Dev mode: the next race's first runner to turn at the mark turns an ankle (past its rate and its ground, never past the cap).</summary>
        public static string DebugSprain()
        {
            debugSprain = true;
            return "the next games day's race: the first runner at the mark turns an ankle (a child scrapes a knee).";
        }

        /// <summary>Dev mode: the contests called now on this map, among those free (as if the day's gathering were under way here).</summary>
        public static string DebugContests(Map map)
        {
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && DrillBouts.Of(p) == null && RaceOf(p) == null && PlayOuts.CanJoin(p)).InRandomOrder().ToList();
            if (people.Count < 2) return "needs two or more of the colony free.";
            var spot = PlaySpots.OpenCell(map, people[0]);
            if (!spot.IsValid) return "no open ground near the colony for the contests.";
            Stage(map, spot, people);
            return "the games' contests are called near " + spot + " (" + people.Count + " free).";
        }
    }

    /// <summary>
    /// A runner in a games day's footrace (GamesDay): to their lane's line, a wait for the others and the start, a sprint
    /// out to the mark (the turn: the race's one roll for them) and back. Danger ends it.
    /// </summary>
    public class JobDriver_GamesRace : JobDriver
    {
        private int waited;

        private GamesRace Race => GamesDay.RaceOf(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_HarmFour_Report_Race".Translate().Resolve();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waited, "waited");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Race == null || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            AddFinishAction(cond => GamesDay.Done(GamesDay.RaceOf(pawn), pawn));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var line = ToilMaker.MakeToil("GamesRaceLine");
            line.defaultCompleteMode = ToilCompleteMode.Never;
            line.handlingFacing = true;
            line.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetB.Cell);
                waited += delta;
                var race = Race;
                GamesDay.AtLine(race, pawn, waited);
                if (race != null && race.go) ReadyForNextToil();
            };
            yield return line;
            var outward = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            outward.AddPreInitAction(() => job.locomotionUrgency = LocomotionUrgency.Sprint);
            yield return outward;
            var turn = ToilMaker.MakeToil("GamesRaceTurn");
            turn.initAction = () =>
            {
                if (GamesDay.Turned(Race, pawn))
                {
                    HarmFour.SitDown(pawn, 400);
                    return;
                }
            };
            turn.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return turn;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var home = ToilMaker.MakeToil("GamesRaceHome");
            home.initAction = () => GamesDay.Back(Race, pawn);
            home.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return home;
        }
    }
}
