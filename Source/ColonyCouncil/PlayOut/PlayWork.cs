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
    /// A work party's work (PlayKind.WorkParty): the game's own jobs, found by the game's own work givers (cleaning,
    /// hauling, repairing, building, carrying the dead, clearing snow, sowing, harvesting, cutting), on what the colony
    /// really has to do and nothing else, and only work each one is able for. Each job is followed in the queue by a
    /// short step (JobDriver_PlayOutWorkNext) that counts what was done and finds the next, so they go from one job to
    /// the next as a party would, until the time is up, danger comes, they're drafted, called to something of the
    /// game's own (a gathering, a ritual) or need to eat or sleep. Nothing is held: the jobs are ordinary ones.
    /// </summary>
    public static class PlayWork
    {
        private static Dictionary<WorkTask, List<WorkGiverDef>> givers;

        /// <summary>
        /// The job each one was last given (with its id: the game hands a finished job's object out again for another
        /// job, perhaps theirs, so the object alone doesn't say it's still ours), and what it was on (not saved: after a
        /// load the party simply hands out fresh work).
        /// </summary>
        private static readonly Dictionary<int, (Job job, int id)> issued = new Dictionary<int, (Job, int)>();
        private static readonly Dictionary<int, (WorkTask task, Thing target, IntVec3 cell)> last = new Dictionary<int, (WorkTask, Thing, IntVec3)>();
        /// <summary>Guards against a job that ends the moment it starts: the next step runs at most a few times in one tick.</summary>
        private static readonly Dictionary<int, (int tick, int n)> steps = new Dictionary<int, (int, int)>();

        private static bool Matches(WorkTask t, string name)
        {
            switch (t)
            {
                case WorkTask.Clean: return name == "WorkGiver_CleanFilth";
                case WorkTask.Haul: return name == "WorkGiver_HaulGeneral";
                case WorkTask.Repair: return name == "WorkGiver_Repair";
                case WorkTask.Build:
                    return name == "WorkGiver_ConstructDeliverResourcesToFrames" || name == "WorkGiver_ConstructDeliverResourcesToBlueprints"
                           || name == "WorkGiver_ConstructFinishFrames";
                case WorkTask.Corpses: return name == "WorkGiver_HaulCorpses";
                case WorkTask.Snow: return name.IndexOf("Snow", StringComparison.Ordinal) >= 0 && name.StartsWith("WorkGiver_", StringComparison.Ordinal);
                case WorkTask.Sow: return name == "WorkGiver_GrowerSow";
                case WorkTask.Harvest: return name == "WorkGiver_GrowerHarvest" || name == "WorkGiver_PlantsCut";
                case WorkTask.Cut: return name == "WorkGiver_PlantsCut";
                case WorkTask.Tend: return name == "WorkGiver_Shear" || name == "WorkGiver_Milk";
                default: return false;
            }
        }

        /// <summary>The game's work givers for a task (one of each kind), found by their class so other mods' and the DLCs' own variants don't matter.</summary>
        public static List<WorkGiverDef> Givers(WorkTask t)
        {
            if (givers == null)
            {
                givers = new Dictionary<WorkTask, List<WorkGiverDef>>();
                foreach (WorkTask k in Enum.GetValues(typeof(WorkTask)))
                {
                    var list = new List<WorkGiverDef>();
                    var seen = new HashSet<Type>();
                    foreach (var w in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                    {
                        if (w?.giverClass == null || w.workType == null || w.emergency || !Matches(k, w.giverClass.Name) || !seen.Add(w.giverClass)) continue;
                        list.Add(w);
                    }
                    givers[k] = list;
                }
            }
            return givers.TryGetValue(t, out var l) ? l : new List<WorkGiverDef>();
        }

        /// <summary>Able for at least one of the party's kinds of work, and none their beliefs rule out.</summary>
        public static bool CanDo(PlayOut p, Pawn x)
        {
            if (x == null) return false;
            foreach (var t in p.tasks)
            {
                if (!Allowed(t, x)) continue;
                // The herd isn't the game's work: it's walked home on a rope, by anyone who can handle animals.
                if (t == WorkTask.Herd)
                {
                    if (HerdHome.CanHerd(x)) return true;
                    continue;
                }
                foreach (var w in Givers(t))
                    if (!x.WorkTypeIsDisabled(w.workType) && w.Worker?.MissingRequiredCapacity(x) == null)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Work their beliefs allow (Wishes/Beliefs.cs): no felling trees for those who think it wrong, nothing out in the
        /// open for those whose faith keeps them indoors.
        /// </summary>
        public static bool Allowed(WorkTask t, Pawn x)
        {
            if (t == WorkTask.Cut && Beliefs.Forbids(x, Belief.NoTreeCutting)) return false;
            if ((t == WorkTask.Sow || t == WorkTask.Harvest || t == WorkTask.Cut || t == WorkTask.Snow || t == WorkTask.Herd || t == WorkTask.Tend)
                && Beliefs.Forbids(x, Belief.Indoors)) return false;
            if ((t == WorkTask.Herd || t == WorkTask.Tend) && Beliefs.Forbids(x, Belief.NoRanching)) return false;
            return true;
        }

        /// <summary>Where the work is looked for from: near the place the answer names (the dining room after a feast), else the middle of the colony.</summary>
        private static IntVec3 Focus(PlayOut p, Pawn x)
        {
            foreach (var s in p.spots)
            {
                var c = PlaySpots.FocusCell(p, s, x, out _);
                if (c.IsValid) return c;
            }
            return TemplatePlanner.ColonyCenter(p.map);
        }

        /// <summary>Work of the colony's own, close to home: the home area, or near the middle of the colony (fields anywhere for sowing and the harvest).</summary>
        private static bool InScope(WorkTask t, IntVec3 c, Map map, IntVec3 center)
        {
            if (!c.InBounds(map) || c.Fogged(map)) return false;
            // The fields and the pens are wherever the colony keeps them.
            if (t == WorkTask.Sow || t == WorkTask.Harvest || t == WorkTask.Tend) return true;
            return map.areaManager.Home[c] || c.InHorDistOf(center, 40f);
        }

        /// <summary>The next job of the party's work for this one, nearest to where the work is first: null when there's nothing they can do.</summary>
        public static Job Next(PlayOut p, Pawn x, out WorkTask task, out Thing target, out IntVec3 cell)
        {
            task = WorkTask.Clean;
            target = null;
            cell = IntVec3.Invalid;
            var map = p?.map;
            if (map == null || x == null || x.Map != map) return null;
            var focus = Focus(p, x);
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var t in p.tasks)
            {
                if (!Allowed(t, x)) continue;
                if (t == WorkTask.Herd)
                {
                    if (!HerdHome.CanHerd(x)) continue;
                    Job herd = null;
                    try
                    {
                        herd = HerdHome.NextJob(x, out target);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("work party: the herd", e);
                    }
                    if (herd == null) continue;
                    task = t;
                    return herd;
                }
                foreach (var wg in Givers(t))
                {
                    if (x.WorkTypeIsDisabled(wg.workType) || !(wg.Worker is WorkGiver_Scanner sc)) continue;
                    if (wg.Worker.MissingRequiredCapacity(x) != null) continue;
                    Job job = null;
                    try
                    {
                        if (sc.ShouldSkip(x, false)) continue;
                        var danger = Danger.Some;
                        var things = sc.PotentialWorkThingsGlobal(x);
                        if (things == null && !sc.PotentialWorkThingRequest.IsUndefined) things = map.listerThings.ThingsMatching(sc.PotentialWorkThingRequest);
                        if (things != null)
                        {
                            foreach (var th in things.Where(th => th != null && th.Spawned && InScope(t, th.Position, map, center)
                                                                  && (t != WorkTask.Harvest || wg.giverClass.Name != "WorkGiver_PlantsCut"
                                                                      || map.designationManager.DesignationOn(th, DesignationDefOf.HarvestPlant) != null))
                                         .OrderBy(th => th.Position.DistanceToSquared(focus)).Take(40).ToList())
                            {
                                if (th.IsForbidden(x) || !sc.HasJobOnThing(x, th, false) || !x.CanReach(th, sc.PathEndMode, danger)) continue;
                                job = sc.JobOnThing(x, th, false);
                                if (job == null) continue;
                                target = th;
                                break;
                            }
                        }
                        if (job == null)
                        {
                            var cells = sc.PotentialWorkCellsGlobal(x);
                            if (cells != null)
                                foreach (var c in cells.Where(c => InScope(t, c, map, center)).OrderBy(c => c.DistanceToSquared(focus)).Take(40).ToList())
                                {
                                    if (c.IsForbidden(x) || !sc.HasJobOnCell(x, c, false) || !x.CanReach(c, sc.PathEndMode, danger)) continue;
                                    job = sc.JobOnCell(x, c, false);
                                    if (job == null) continue;
                                    cell = c;
                                    break;
                                }
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("work party " + wg.defName, e);
                        job = null;
                    }
                    if (job == null) continue;
                    job.workGiverDef = wg;
                    job.playerForced = false;
                    task = t;
                    return job;
                }
            }
            return null;
        }

        /// <summary>One of the party's jobs (or the step between two), for this play.</summary>
        public static bool Ours(PlayOut p, Pawn x)
        {
            var job = x?.CurJob;
            if (job == null) return false;
            if (job.def == PlayOutDefOf.Council_PlayOutWorkNext) return true;
            return issued.TryGetValue(x.thingIDNumber, out var j) && j.job == job && j.id == job.loadID;
        }

        private static void Record(Pawn x, Job job, WorkTask task, Thing target, IntVec3 cell)
        {
            issued[x.thingIDNumber] = (job, job.loadID);
            last[x.thingIDNumber] = (task, target, cell);
        }

        /// <summary>Starts this one on the party's next job now (interrupting their own work or idling): false when there's nothing for them.</summary>
        public static bool Give(PlayOut p, Pawn x)
        {
            var job = Next(p, x, out var task, out var target, out var cell);
            if (job == null) return false;
            ClearQueued(x);
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (x.CurJob != job) return false;
            Record(x, job, task, target, cell);
            x.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutWorkNext));
            return true;
        }

        /// <summary>
        /// Between two jobs (JobDriver_PlayOutWorkNext): counts the last one if it got done, and queues the next unless
        /// the party is over for them.
        /// </summary>
        public static void Continue(Pawn x)
        {
            var tracker = PlayOutTracker.Instance;
            var p = tracker?.GroupFor(x);
            if (p == null || p.kind != PlayKind.WorkParty || p.state != PlayState.Running) return;
            var h = p.Hand(x);
            if (h == null || h.left) return;
            Settle(p, x, h);
            int now = Find.TickManager.TicksGame;
            if (steps.TryGetValue(x.thingIDNumber, out var s) && s.tick == now)
            {
                if (s.n >= 3) return;
                steps[x.thingIDNumber] = (now, s.n + 1);
            }
            else steps[x.thingIDNumber] = (now, 1);
            if (now - p.started >= p.durationTicks) return;
            if (PlayOuts.WhyNotNow(p.map, false) != null || !PlayOuts.CanJoin(x) || PlayOuts.NeedsPress(x) || x.GetLord() != null) return;
            var job = Next(p, x, out var task, out var target, out var cell);
            if (job == null) return;
            Record(x, job, task, target, cell);
            x.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutWorkNext));
            x.jobs.jobQueue.EnqueueFirst(job);
        }

        /// <summary>Counts the last job this one was given, if what it was for is done (the filth gone, the thing stored, the wall mended).</summary>
        public static void Settle(PlayOut p, Pawn x, PlayHand h)
        {
            if (x == null || h == null || !last.TryGetValue(x.thingIDNumber, out var l)) return;
            last.Remove(x.thingIDNumber);
            if (!LooksDone(l.task, l.target, l.cell, p.map)) return;
            h.done++;
            p.tally++;
        }

        private static bool LooksDone(WorkTask t, Thing target, IntVec3 cell, Map map)
        {
            switch (t)
            {
                case WorkTask.Repair:
                    return target != null && (target.Destroyed || target.HitPoints >= target.MaxHitPoints);
                case WorkTask.Haul:
                case WorkTask.Corpses:
                    return target != null && (target.Destroyed || !target.Spawned || target.IsInAnyStorage());
                case WorkTask.Sow:
                    return map != null && cell.IsValid && cell.InBounds(map) && cell.GetPlant(map) != null;
                case WorkTask.Snow:
                    return cell.IsValid;
                case WorkTask.Herd:
                    return target is Pawn a && (a.Destroyed || a.Dead || HerdHome.Home(a));
                case WorkTask.Tend:
                    // Sheared or milked: whatever it carried is no longer full.
                    return target is Pawn animal && (animal.Destroyed || animal.Dead || !animal.AllComps.OfType<CompHasGatherableBodyResource>().Any(c => c.ActiveAndFull));
                default:
                    return target != null && (target.Destroyed || !target.Spawned);
            }
        }

        private static void ClearQueued(Pawn x)
        {
            try
            {
                x?.jobs?.jobQueue?.RemoveAll(x, j => j?.def == PlayOutDefOf.Council_PlayOutWorkNext);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("work party queue", e);
            }
        }

        /// <summary>The party is over for this one: the step between jobs is taken out of their queue; with <paramref name="now"/> (danger), the job in hand is stopped too.</summary>
        public static void Release(Pawn x, bool now)
        {
            if (x == null) return;
            ClearQueued(x);
            if (now && issued.TryGetValue(x.thingIDNumber, out var j) && x.CurJob == j.job && j.job.loadID == j.id && x.jobs != null)
                x.jobs.EndCurrentJob(JobCondition.InterruptForced);
            issued.Remove(x.thingIDNumber);
            last.Remove(x.thingIDNumber);
            steps.Remove(x.thingIDNumber);
        }
    }

    /// <summary>The step between a work party's jobs: counts the last and queues the next (see <see cref="PlayWork.Continue"/>).</summary>
    public class JobDriver_PlayOutWorkNext : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var t = ToilMaker.MakeToil("PlayOutWorkNext");
            t.initAction = () =>
            {
                try
                {
                    PlayWork.Continue(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("work party next", e);
                }
            };
            t.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return t;
        }
    }
}
