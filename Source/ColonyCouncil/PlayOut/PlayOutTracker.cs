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
    /// Answers waiting to play out, and those under way. Every few seconds of game time each waiting plan looks at the
    /// hour, at danger and at who is free, and starts if it can: a gathering (<see cref="LordJob_PlayOut"/>) or a visit
    /// (<see cref="JobDriver_PlayOutVisit"/>). A plan that can't start within its hours and the wait it allows falls back
    /// to what answers always did: its moods, for everyone it was meant for. Cheap when nothing is waiting (one list
    /// check every 250 ticks).
    /// </summary>
    public class PlayOutTracker : GameComponent
    {
        public List<PlayOut> plays = new List<PlayOut>();
        private int nextId = 1;

        /// <summary>More than this waiting at once and the oldest gives up (its moods are given).</summary>
        public const int MaxWaiting = 10;

        private static PlayOutTracker cached;
        private static Game cachedGame;

        public static PlayOutTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PlayOutTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public PlayOutTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public PlayOut Get(int id)
        {
            foreach (var p in plays)
                if (p.id == id)
                    return p;
            return null;
        }

        /// <summary>The visit or lesson this colonist is on now, if any.</summary>
        public PlayOut RunningFor(Pawn pawn)
        {
            if (pawn == null) return null;
            foreach (var p in plays)
                if (p.state == PlayState.Running && !p.IsGathering && p.visitor == pawn)
                    return p;
            return null;
        }

        public PlayOut Add(PlayOut p)
        {
            p.id = nextId++;
            p.state = PlayState.Pending;
            plays.Add(p);
            var waiting = plays.Where(x => x.state == PlayState.Pending).ToList();
            if (waiting.Count > MaxWaiting) Fallback(waiting[0], null, true);
            return p;
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            if (plays.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RarePlayOut)) return;
            foreach (var p in plays.ToList())
            {
                try
                {
                    Step(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("play out " + p.kind, e);
                    Fallback(p, null, true);
                }
            }
            plays.RemoveAll(p => p.state == PlayState.Done);
        }

        private void Step(PlayOut p, int now)
        {
            if (p.state == PlayState.Done) return;
            if (p.map == null || !Find.Maps.Contains(p.map))
            {
                Fallback(p, null, true);
                return;
            }
            if (p.state == PlayState.Pending)
            {
                // Switched off meanwhile: the answer's moods, as it always was.
                if (!PlayOuts.On)
                {
                    Fallback(p, null, true);
                    return;
                }
                if (now > p.deadline)
                {
                    Fallback(p, p.waitReason, false);
                    return;
                }
                if (!PlayOutMath.InHours(GenLocalDate.HourOfDay(p.map), p.hourFrom, p.hourTo)) return;
                if (p.IsGathering) TryStartGathering(p, now);
                else TryStartVisit(p, now);
                return;
            }
            // Under way. A gathering ends through its lord; one whose lord has gone without a word is let go.
            if (p.IsGathering)
            {
                if (now - p.started > PlayOutMath.TicksPerHour / 2 && !LordAlive(p)) Fallback(p, null, true);
                else if (now - p.started > p.durationTicks + PlayOutMath.TicksPerHour * 6) Fallback(p, null, true);
                return;
            }
            var v = p.visitor;
            if (v == null || v.Dead || !(v.CurJob?.def == PlayOutDefOf.Council_PlayOutVisit))
            {
                // The job ended without telling us (a load, a mod): judged by how long it's been.
                Done(p, PlayOutMath.VisitCounts(now - p.started, p.durationTicks) ? new List<Pawn> { p.visitor, p.other } : null, false);
            }
        }

        private bool LordAlive(PlayOut p)
        {
            var lords = p.map?.lordManager?.lords;
            if (lords == null) return false;
            foreach (var l in lords)
                if (l?.LordJob is LordJob_PlayOut j && j.playId == p.id)
                    return true;
            return false;
        }

        // ---------------- Starting ----------------

        private void TryStartGathering(PlayOut p, int now)
        {
            var map = p.map;
            string why = PlayOuts.WhyNotNow(map, true);
            if (why != null)
            {
                p.waitReason = why;
                return;
            }
            p.invited.RemoveAll(x => x == null || x.Dead || x.Faction != Faction.OfPlayer);
            var able = p.invited.Where(x => PlayOuts.CanTakePart(x, map)).ToList();
            if (p.invited.Count == 0)
            {
                Fallback(p, null, true);
                return;
            }
            if (!PlayOutMath.EnoughToGather(able.Count, p.invited.Count))
            {
                p.waitReason = "CC_Play_Why_Busy".Translate();
                return;
            }
            // Words need their speaker (an apology, a reading); anything else can be led by whoever is free.
            if (p.words && p.organizer != null && !able.Contains(p.organizer))
            {
                p.waitReason = "CC_Play_Why_NotFree".Translate(p.organizer.Named("PAWN")).Resolve();
                return;
            }
            var organizer = p.organizer != null && able.Contains(p.organizer) ? p.organizer
                : able.OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First();
            if (!PlaySpots.TryFind(p, organizer, out var spot, out var where, out var laid, out int wood))
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return;
            }
            var gd = PlayOutDefOf.Council_PlayOutGathering;
            var job = new LordJob_PlayOut(spot, organizer, gd, p) { laidFire = laid };
            int meals = -1;
            if (p.kind == PlayKind.Feast)
            {
                meals = PlaySpots.CountMeals(map);
                job.serve = meals > 0;
            }
            LordMaker.MakeNewLord(Faction.OfPlayer, job, map, new List<Pawn> { organizer });
            p.state = PlayState.Running;
            p.started = now;
            p.organizer = organizer;
            Messages.Message(BeginText(p, where, meals, able.Count, laid != null ? wood : -1), new LookTargets(new TargetInfo(spot, map)), MessageTypeDefOf.PositiveEvent, false);
        }

        private static string BeginText(PlayOut p, string where, int meals, int guests, int wood)
        {
            string label = (p.label ?? "").CapitalizeFirst();
            string key;
            switch (p.kind)
            {
                case PlayKind.Feast:
                    key = meals > 0 ? "CC_Play_Begin_Feast" : "CC_Play_Begin_Gathering";
                    break;
                case PlayKind.Bonfire:
                    key = "CC_Play_Begin_Fire";
                    break;
                case PlayKind.Toast:
                    key = p.words ? "CC_Play_Begin_Words" : "CC_Play_Begin_Toast";
                    break;
                default:
                    key = p.music ? "CC_Play_Begin_Song" : "CC_Play_Begin_Gathering";
                    break;
            }
            string text = key.Translate(label, where ?? "").Resolve();
            if (p.kind == PlayKind.Feast)
            {
                var plan = PlayOutMath.PlanFeast(meals, guests);
                if (plan == FeastPlan.Partial) text += " " + "CC_Play_Feast_Partial".Translate(meals).Resolve();
                else if (plan == FeastPlan.Party) text += " " + "CC_Play_Feast_None".Translate().Resolve();
            }
            // A fire laid for it: from the stores' wood, or (wood 0) from what the answer set aside.
            if (wood > 0) text += " " + "CC_Play_Fire_Laid".Translate(wood).Resolve();
            else if (wood == 0) text += " " + "CC_Play_Fire_LaidPaid".Translate().Resolve();
            return text;
        }

        private void TryStartVisit(PlayOut p, int now)
        {
            var map = p.map;
            string why = PlayOuts.WhyNotNow(map, false);
            if (why != null)
            {
                p.waitReason = why;
                return;
            }
            var v = p.visitor;
            if (v == null || v.Dead || v.Faction != Faction.OfPlayer)
            {
                Fallback(p, null, true);
                return;
            }
            // One thing at a time: a second visit for the same colonist waits for the first.
            if (RunningFor(v) != null) return;
            if (!PlayOuts.CanTakePart(v, map))
            {
                p.waitReason = "CC_Play_Why_NotFree".Translate(v.Named("PAWN")).Resolve();
                return;
            }
            LocalTargetInfo target;
            LocalTargetInfo face = LocalTargetInfo.Invalid;
            string begin;
            switch (p.to)
            {
                case PlaySpot.Grave:
                {
                    var grave = PlaySpots.GraveFor(p);
                    if (grave == null)
                    {
                        Fallback(p, "CC_Play_Why_NoGrave".Translate(), false);
                        return;
                    }
                    target = grave;
                    begin = "CC_Play_Begin_Grave".Translate(v.Named("PAWN"), PlaySpots.GraveWords(grave).Named("WHERE")).Resolve();
                    break;
                }
                case PlaySpot.Camp:
                {
                    var c = PlaySpots.CampCell(map, v);
                    if (!c.IsValid)
                    {
                        if (CampTracker.Instance?.camp == null) Fallback(p, "CC_Play_Why_NoCamp".Translate(), false);
                        else p.waitReason = "CC_Play_Why_Place".Translate();
                        return;
                    }
                    target = c;
                    face = CampTracker.Instance.camp.FireCell;
                    begin = "CC_Play_Begin_Camp".Translate(v.Named("PAWN")).Resolve();
                    break;
                }
                case PlaySpot.Walk:
                {
                    var c = PlaySpots.WalkCell(map, v);
                    if (!c.IsValid)
                    {
                        p.waitReason = "CC_Play_Why_Weather".Translate();
                        return;
                    }
                    target = c;
                    begin = "CC_Play_Begin_Walk".Translate(v.Named("PAWN")).Resolve();
                    break;
                }
                default:
                {
                    var o = p.other;
                    if (o == null || o.Dead || o.Destroyed)
                    {
                        Fallback(p, null, true);
                        return;
                    }
                    if (!o.Spawned || o.Map != map)
                    {
                        p.waitReason = "CC_Play_Why_Away".Translate(o.Named("OTHER")).Resolve();
                        return;
                    }
                    if (!v.CanReach(o, PathEndMode.Touch, Danger.Some))
                    {
                        p.waitReason = "CC_Play_Why_Place".Translate();
                        return;
                    }
                    target = o;
                    begin = (p.kind == PlayKind.Lesson ? "CC_Play_Begin_Lesson" : "CC_Play_Begin_Visit")
                        .Translate(v.Named("PAWN"), o.Named("OTHER"), (p.skill?.label ?? "").Named("SKILL")).Resolve();
                    break;
                }
            }
            if (!target.IsValid || (!target.HasThing && !v.CanReach(target, PathEndMode.OnCell, Danger.Some)))
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return;
            }
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutVisit, target, face);
            job.count = p.durationTicks;
            // Running before the job starts, so the job finds its plan from its first tick.
            p.state = PlayState.Running;
            p.started = now;
            v.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (v.CurJob?.def != PlayOutDefOf.Council_PlayOutVisit)
            {
                // It didn't take (something more pressing came first): try again a little later.
                p.state = PlayState.Pending;
                p.started = -1;
                return;
            }
            Messages.Message(begin, new LookTargets(v), MessageTypeDefOf.NeutralEvent, false);
        }

        // ---------------- Ending ----------------

        /// <summary>A visit or lesson's job is over: counted if they stayed long enough, else tried again while there's time.</summary>
        public void VisitEnded(Pawn pawn, int stayed)
        {
            var p = RunningFor(pawn);
            if (p == null) return;
            if (PlayOutMath.VisitCounts(stayed, p.durationTicks))
            {
                Done(p, new List<Pawn> { p.visitor, p.other }, false);
                return;
            }
            if (Find.TickManager.TicksGame < p.deadline && PlayOuts.On)
            {
                p.state = PlayState.Pending;
                p.started = -1;
                p.waitReason = "CC_Play_Why_CutShort".Translate();
                return;
            }
            Fallback(p, "CC_Play_Why_CutShort".Translate(), false);
        }

        /// <summary>A gathering is over (called by its lord). Nobody there long enough: as if it never happened.</summary>
        public void GatheringEnded(int id, List<Pawn> attended, bool early, int ate, bool fire, string label)
        {
            var p = Get(id);
            if (p == null || p.state == PlayState.Done) return;
            if (attended.NullOrEmpty())
            {
                Fallback(p, early ? "CC_Play_Why_CutShort".Translate() : "CC_Play_Why_Busy".Translate(), false);
                return;
            }
            Done(p, attended, true);
            string name = (label ?? p.label ?? "").CapitalizeFirst();
            string text = early ? "CC_Play_End_Early".Translate(name).Resolve()
                : ate >= 0 ? "CC_Play_End_Feast".Translate(name, ate).Resolve()
                : "CC_Play_End".Translate(name).Resolve();
            if (fire) text += " " + "CC_Play_End_Fire".Translate().Resolve();
            News.Post(text, new LookTargets(attended.Where(x => x != null && x.Spawned)), NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
        }

        /// <summary>It happened: the moods go to those who took part.</summary>
        private void Done(PlayOut p, List<Pawn> took, bool gathering)
        {
            if (p.state == PlayState.Done) return;
            p.state = PlayState.Done;
            if (took == null)
            {
                GiveAll(p);
                return;
            }
            foreach (var m in p.moods)
            {
                if (!took.Contains(m.pawn)) continue;
                try
                {
                    m.Give();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("play out mood", e);
                }
            }
        }

        /// <summary>
        /// It never happened (or the setting is off): the moods go to everyone they were meant for, as answers always
        /// did, and if it was waited for, a word on why.
        /// </summary>
        public void Fallback(PlayOut p, string why, bool quiet)
        {
            if (p == null || p.state == PlayState.Done) return;
            p.state = PlayState.Done;
            GiveAll(p);
            // A visit or a lesson that never came about passes quietly; a gathering the colony was waiting for gets a word.
            if (quiet || p.label.NullOrEmpty() || !p.IsGathering) return;
            string text = why.NullOrEmpty()
                ? "CC_Play_FallbackPlain".Translate(p.label.CapitalizeFirst()).Resolve()
                : "CC_Play_Fallback".Translate(p.label.CapitalizeFirst(), why).Resolve();
            News.Post(text, null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        private static void GiveAll(PlayOut p)
        {
            foreach (var m in p.moods)
            {
                try
                {
                    m.Give();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("play out mood", e);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref plays, "plays", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (plays == null) plays = new List<PlayOut>();
                plays.RemoveAll(p => p == null || p.state == PlayState.Done);
            }
        }
    }
}
