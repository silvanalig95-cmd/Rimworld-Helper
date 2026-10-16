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
    /// The engine behind work parties, walks together, vigils, searches, errands and hunts (PlayOutGroupEffects.cs).
    /// The tracker asks it every 250 ticks: to start a plan that's waiting (its hours, no danger, enough people free and
    /// able, no more than two such plans under way at once), and to follow one that's going (who's still at it, the
    /// walk's stages, the work party's next jobs, the hunt's game). Everyone is on a job of their own, never in a lord,
    /// so the game's own gatherings and rituals can always take them; anyone drafted, downed, called to a gathering,
    /// hungry or tired, or at the first sign of danger, is let go. When it's over, the answer's held moods go to those
    /// who took part, a word goes out, and one or two of them write it in their journals.
    /// </summary>
    public static class PlayGroups
    {
        // ---------------- Who goes ----------------

        /// <summary>How many of a kind go at most.</summary>
        public static int Cap(PlayKind k)
        {
            switch (k)
            {
                case PlayKind.Carry: return 1;
                case PlayKind.Hunt: return 2;
                case PlayKind.Search: return 4;
                case PlayKind.Vigil: return 4;
                case PlayKind.WorkParty: return 6;
                default: return PlayOutMath.MaxGroup;
            }
        }

        /// <summary>Able for it at all: the work it needs, hands to carry, a hunting weapon, legs to walk.</summary>
        public static bool CanDo(PlayOut p, Pawn x)
        {
            if (x == null || x.Dead || x.health?.capacities == null) return false;
            if (!x.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
            switch (p.kind)
            {
                case PlayKind.WorkParty: return PlayWork.CanDo(p, x);
                case PlayKind.Carry: return x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !x.DevelopmentalStage.Baby();
                case PlayKind.Hunt: return PlayHunt.CanHunt(x);
                default: return true;
            }
        }

        /// <summary>Free to go now: here and up, not in any lord (the game's gatherings come first), on no other plan, doing nothing that matters more, and not hungry or tired.</summary>
        public static bool Free(PlayOutTracker t, PlayOut p, Pawn x) =>
            PlayOuts.CanTakePart(x, p.map) && x.GetLord() == null && !t.BusyWith(x, p) && PlayOuts.Interruptible(x) && !PlayOuts.NeedsPress(x);

        /// <summary>Still on it: the job this plan gave them (or, for a work party, the step between two of its jobs).</summary>
        public static bool OnIt(PlayOut p, Pawn x)
        {
            var job = x?.CurJob;
            if (job == null || x.Map != p.map) return false;
            switch (p.kind)
            {
                case PlayKind.WorkParty: return PlayWork.Ours(p, x);
                case PlayKind.Hunt: return PlayHunt.OnIt(p, x);
                case PlayKind.Carry: return job.def == PlayOutDefOf.Council_PlayOutCarry;
                default: return job.def == JobOf(p.kind);
            }
        }

        private static JobDef JobOf(PlayKind k) =>
            k == PlayKind.Procession ? PlayOutDefOf.Council_PlayOutWalk : k == PlayKind.Vigil ? PlayOutDefOf.Council_PlayOutVigil : PlayOutDefOf.Council_PlayOutSearch;

        // ---------------- Starting ----------------

        public static void TryStart(PlayOutTracker t, PlayOut p, int now)
        {
            var map = p.map;
            bool together = p.kind == PlayKind.Procession || p.kind == PlayKind.Vigil;
            string why = PlayOuts.WhyNotNow(map, together);
            if (why != null)
            {
                p.waitReason = why;
                return;
            }
            if (t.GroupsRunning(map) >= PlayOutMath.MaxGroupsRunning)
            {
                p.waitReason = "CC_Play_Why_Gathering".Translate();
                return;
            }
            p.invited.RemoveAll(x => x == null || x.Dead || x.Faction != Faction.OfPlayer);
            if (p.invited.Count == 0)
            {
                t.Fallback(p, null, true);
                return;
            }
            var capable = p.invited.Where(x => CanDo(p, x)).ToList();
            if (capable.Count == 0)
            {
                t.Fallback(p, (p.kind == PlayKind.Hunt ? "CC_Play_Why_NoHunter" : "CC_Play_Why_NoOneAble").Translate(), false);
                return;
            }
            if (p.kind == PlayKind.Hunt)
                capable = capable.OrderByDescending(x => x == p.organizer)
                    .ThenByDescending(x => x.workSettings != null && x.workSettings.WorkIsActive(WorkTypeDefOf.Hunting))
                    .ThenByDescending(x => x.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).ToList();
            int cap = Cap(p.kind);
            int wanted = PlayOutMath.GroupSize(capable.Count, p.count, cap);
            var able = capable.Where(x => Free(t, p, x)).ToList();
            if (!PlayOutMath.GroupReady(able.Count, wanted))
            {
                var missing = capable.FirstOrDefault(x => !able.Contains(x));
                p.waitReason = wanted <= 2 && missing != null ? "CC_Play_Why_NotFree".Translate(missing.Named("PAWN")).Resolve() : "CC_Play_Why_Busy".Translate().ToString();
                return;
            }
            var chosen = able.Take(PlayOutMath.GroupSize(able.Count, p.count, cap)).ToList();
            // Running before the jobs start, so each job finds its plan from its first tick.
            p.state = PlayState.Running;
            p.started = now;
            p.hands = chosen.Select(x => new PlayHand { pawn = x }).ToList();
            string where = null;
            bool ok;
            try
            {
                switch (p.kind)
                {
                    case PlayKind.WorkParty:
                        ok = StartWork(p);
                        break;
                    case PlayKind.Procession:
                        ok = StartWalk(p, now, out where);
                        break;
                    case PlayKind.Vigil:
                        ok = StartVigil(p, out where);
                        break;
                    case PlayKind.Search:
                        ok = StartSearch(p);
                        break;
                    case PlayKind.Carry:
                        ok = StartCarry(t, p, out where);
                        break;
                    case PlayKind.Hunt:
                        ok = StartHunt(p);
                        break;
                    default:
                        ok = false;
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out start " + p.kind, e);
                ok = false;
            }
            if (p.state == PlayState.Done) return;
            p.hands.RemoveAll(h => !OnIt(p, h.pawn));
            if (!ok || p.hands.Count == 0)
            {
                foreach (var h in p.hands) Stop(p, h.pawn, true);
                p.state = PlayState.Pending;
                p.started = -1;
                p.hands.Clear();
                if (p.waitReason.NullOrEmpty()) p.waitReason = "CC_Play_Why_Busy".Translate();
                return;
            }
            p.waitReason = null;
            if (p.organizer == null || p.Hand(p.organizer) == null) p.organizer = p.hands[0].pawn;
            Messages.Message(PlayWords.BeginText(p, where), new LookTargets(p.hands.Select(h => h.pawn).Where(x => x.Spawned)), MessageTypeDefOf.NeutralEvent, false);
            PlayWords.Invite(p);
        }

        private static bool StartWork(PlayOut p)
        {
            foreach (var h in p.hands.ToList()) PlayWork.Give(p, h.pawn);
            if (p.hands.Any(h => OnIt(p, h.pawn))) return true;
            p.waitReason = "CC_Play_Why_NoWork".Translate();
            return false;
        }

        private static bool StartWalk(PlayOut p, int now, out string where)
        {
            var lead = p.hands[0].pawn;
            p.cell = PlaySpots.FocusCell(p, p.to, lead, out where);
            if (!p.cell.IsValid)
            {
                p.waitReason = (p.to == PlaySpot.Walk || p.to == PlaySpot.Fields || p.to == PlaySpot.Water ? "CC_Play_Why_Weather" : "CC_Play_Why_Place").Translate();
                return false;
            }
            p.home = PlaySpots.MeetCell(p, lead);
            if (!p.home.IsValid) p.home = lead.Position;
            p.phase = 0;
            p.phaseStarted = now;
            foreach (var h in p.hands) StartGroupJob(p, h.pawn, LocomotionUrgency.Walk);
            return true;
        }

        private static bool StartVigil(PlayOut p, out string where)
        {
            where = null;
            var lead = p.hands[0].pawn;
            if (p.to == PlaySpot.Person)
            {
                var o = p.other;
                if (o == null || o.Dead)
                {
                    p.waitReason = "CC_Play_Why_Place".Translate();
                    return false;
                }
                if (!o.Spawned || o.Map != p.map)
                {
                    p.waitReason = "CC_Play_Why_Away".Translate(o.Named("OTHER")).Resolve();
                    return false;
                }
                p.cell = IntVec3.Invalid;
                where = "CC_Play_Where_Beside".Translate(o.Named("OTHER")).Resolve();
            }
            else
            {
                p.cell = PlaySpots.FocusCell(p, p.to, lead, out where);
                if (!p.cell.IsValid)
                {
                    p.waitReason = (p.to == PlaySpot.Grave ? "CC_Play_Why_NoGrave" : "CC_Play_Why_Place").Translate();
                    return false;
                }
            }
            foreach (var h in p.hands) StartGroupJob(p, h.pawn, LocomotionUrgency.Walk);
            return true;
        }

        private static bool StartSearch(PlayOut p)
        {
            int n = p.hands.Count;
            for (int i = 0; i < n; i++)
            {
                var h = p.hands[i];
                h.route = PlaySpots.SearchRoute(p, h.pawn, i, n);
                if (h.route.Count > 0) StartGroupJob(p, h.pawn, LocomotionUrgency.Jog);
            }
            if (p.hands.Any(h => OnIt(p, h.pawn))) return true;
            p.waitReason = "CC_Play_Why_Place".Translate();
            return false;
        }

        private static void StartGroupJob(PlayOut p, Pawn x, LocomotionUrgency pace)
        {
            var job = JobMaker.MakeJob(JobOf(p.kind));
            job.locomotionUrgency = pace;
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        private static bool StartCarry(PlayOutTracker t, PlayOut p, out string where)
        {
            where = null;
            var carrier = p.hands[0].pawn;
            var stack = PlayCarry.StackFor(p, carrier);
            if (stack == null)
            {
                // Nothing of it left to carry: the stores have been used up meanwhile.
                if (PlayCarry.Have(p.map, p.what, p.thing) <= 0) t.Fallback(p, "CC_Play_Why_NothingLeft".Translate(), false);
                else p.waitReason = "CC_Play_Why_Place".Translate();
                return false;
            }
            LocalTargetInfo to = LocalTargetInfo.Invalid;
            var x = p.other;
            if (p.to != PlaySpot.Gate && p.to != PlaySpot.Camp && x != null && !x.Dead && x.Spawned && x.Map == p.map && x.Faction != Faction.OfPlayer
                && carrier.CanReach(x, PathEndMode.Touch, Danger.Some))
            {
                to = x;
                where = x.LabelShort;
            }
            else
            {
                IntVec3 c = IntVec3.Invalid;
                if (p.to == PlaySpot.Camp)
                {
                    c = PlaySpots.CampCell(p.map, carrier);
                    if (c.IsValid) where = "CC_Play_To_Camp".Translate();
                }
                if (!c.IsValid)
                {
                    c = PlaySpots.GateCell(p.map, carrier);
                    if (c.IsValid) where = "CC_Play_To_Gate".Translate();
                }
                if (!c.IsValid)
                {
                    p.waitReason = "CC_Play_Why_Place".Translate();
                    return false;
                }
                to = c;
            }
            int n = PlayOutMath.CarryCount(p.amount - p.tally, stack.stackCount, carrier.carryTracker.AvailableStackSpace(stack.def));
            if (n <= 0)
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return false;
            }
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutCarry, stack, to);
            job.count = n;
            carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }

        private static bool StartHunt(PlayOut p)
        {
            foreach (var h in p.hands)
            {
                var job = PlayHunt.JobFor(p, h.pawn);
                if (job != null) h.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            if (p.hands.Any(h => OnIt(p, h.pawn))) return true;
            p.waitReason = (p.game.Any(a => PlayHunt.Safe(a, p.map)) ? "CC_Play_Why_NoHunter" : "CC_Play_Why_GameGone").Translate();
            return false;
        }

        // ---------------- Under way ----------------

        public static void Step(PlayOutTracker t, PlayOut p, int now)
        {
            var map = p.map;
            int elapsed = now - p.started;
            if (PlayOuts.WhyNotNow(map, false) != null)
            {
                // An errand waits for the danger to pass (what's carried is set down, still the colony's); anything else breaks up.
                if (p.kind == PlayKind.Carry)
                {
                    foreach (var h in p.hands) Stop(p, h.pawn, true);
                    StepCarry(t, p, now);
                    if (p.state == PlayState.Pending) p.waitReason = "CC_Play_Why_Danger".Translate();
                    return;
                }
                Finish(t, p, true, "CC_Play_Why_Danger".Translate());
                return;
            }
            foreach (var h in p.hands)
            {
                var x = h.pawn;
                if (h.left) continue;
                if (x == null || x.Dead || !x.Spawned || x.Map != map || x.Drafted || x.Downed || x.GetLord() != null || x.InMentalState)
                {
                    h.left = true;
                    Stop(p, x, false);
                }
            }
            switch (p.kind)
            {
                case PlayKind.WorkParty:
                    StepWork(t, p, elapsed);
                    break;
                case PlayKind.Procession:
                    StepWalk(t, p, now);
                    break;
                case PlayKind.Vigil:
                case PlayKind.Search:
                    StepStay(t, p, elapsed);
                    break;
                case PlayKind.Carry:
                    StepCarry(t, p, now);
                    break;
                case PlayKind.Hunt:
                    StepHunt(t, p, elapsed);
                    break;
            }
            if (p.state == PlayState.Running && elapsed > PlayOutMath.RunCap(p.durationTicks)) Finish(t, p, false, null);
        }

        private static void StepWork(PlayOutTracker t, PlayOut p, int elapsed)
        {
            bool timeUp = elapsed >= p.durationTicks;
            int on = 0;
            bool anyWork = false;
            foreach (var h in p.hands)
            {
                if (h.left) continue;
                var x = h.pawn;
                if (OnIt(p, x))
                {
                    on++;
                    h.ticks += 250;
                    anyWork = true;
                    continue;
                }
                PlayWork.Settle(p, x, h);
                if (timeUp || !Free(t, p, x)) continue;
                // Back to it after seeing to something of their own (or after a load).
                if (PlayWork.Give(p, x))
                {
                    on++;
                    anyWork = true;
                }
            }
            if (on > 0 && Rand.Chance(0.12f)) PlayWords.During(p, p.hands.Where(h => !h.left && OnIt(p, h.pawn)).Select(h => h.pawn).RandomElementWithFallback());
            // Over when the time is up and the last jobs are done, or early when there's nothing more to do.
            if (on == 0 && (timeUp || !anyWork && elapsed > PlayOutMath.TicksPerHour / 4)) Finish(t, p, false, null);
        }

        private static void StepWalk(PlayOutTracker t, PlayOut p, int now)
        {
            var going = p.hands.Where(h => !h.left && OnIt(p, h.pawn)).ToList();
            foreach (var h in p.hands)
                if (!h.left && !going.Contains(h) && now - p.started > 500)
                    h.left = true;
            int arrived = going.Count(h => h.phase == p.phase && h.cell.IsValid && h.pawn.Position.InHorDistOf(h.cell, 2.5f));
            int next = PlayOutMath.NextWalkPhase(p.phase, arrived, going.Count, now - p.phaseStarted, PlayOutMath.WalkStay(p.durationTicks));
            if (next != p.phase)
            {
                p.phase = next;
                p.phaseStarted = now;
                if (next == 2 && going.Count > 0) PlayWords.During(p, going.RandomElement().pawn);
            }
            if (p.phase >= 4 || going.Count == 0) Finish(t, p, false, null);
        }

        private static void StepStay(PlayOutTracker t, PlayOut p, int elapsed)
        {
            var going = p.hands.Where(h => !h.left && OnIt(p, h.pawn)).ToList();
            foreach (var h in p.hands)
                if (!h.left && !going.Contains(h) && elapsed > 500)
                    h.left = true;
            if (p.kind == PlayKind.Vigil && going.Count > 0 && Rand.Chance(0.06f)) PlayWords.During(p, going.RandomElement().pawn);
            if (going.Count == 0 || (p.kind == PlayKind.Vigil && elapsed >= p.durationTicks)) Finish(t, p, false, null);
        }

        private static void StepCarry(PlayOutTracker t, PlayOut p, int now)
        {
            var h = p.hands.FirstOrDefault();
            if (h != null && !h.left && OnIt(p, h.pawn))
            {
                h.ticks += 250;
                return;
            }
            // Stopped before it was handed over: tried again while there's time, else it goes the old way.
            if (now < p.deadline && PlayOuts.On)
            {
                p.state = PlayState.Pending;
                p.started = -1;
                p.hands.Clear();
                p.waitReason = "CC_Play_Why_CutShort".Translate();
                return;
            }
            t.Fallback(p, "CC_Play_Why_CutShort".Translate(), false);
        }

        private static void StepHunt(PlayOutTracker t, PlayOut p, int elapsed)
        {
            int alive = p.game.Count(a => PlayHunt.Safe(a, p.map) && p.map.designationManager.DesignationOn(a, DesignationDefOf.Hunt) != null);
            bool over = PlayOutMath.HuntOver(alive, elapsed, p.durationTicks);
            foreach (var h in p.hands)
            {
                if (h.left) continue;
                if (OnIt(p, h.pawn))
                {
                    h.ticks += 250;
                    if (Rand.Chance(0.04f)) PlayWords.During(p, h.pawn);
                    continue;
                }
                if (over || !Free(t, p, h.pawn)) continue;
                var job = PlayHunt.JobFor(p, h.pawn);
                if (job != null) h.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            if (over || !p.hands.Any(h => !h.left && OnIt(p, h.pawn)) && elapsed > PlayOutMath.TicksPerHour) Finish(t, p, false, null);
        }

        // ---------------- Where each one is ----------------

        /// <summary>
        /// Where this one should be now, and what to face: a walk's gathering place or its far end, their place round a
        /// vigil, a searcher's next place. False when it's over for them.
        /// </summary>
        public static bool Where(PlayOut p, Pawn x, PlayHand h, out IntVec3 cell, out LocalTargetInfo face)
        {
            cell = IntVec3.Invalid;
            face = LocalTargetInfo.Invalid;
            var map = p.map;
            int i = p.hands.IndexOf(h);
            switch (p.kind)
            {
                case PlayKind.Procession:
                {
                    if (p.phase >= 4) return false;
                    var center = p.phase == 1 || p.phase == 2 ? p.cell : p.home;
                    if (!center.IsValid) return false;
                    if (h.phase != p.phase || !h.cell.IsValid)
                    {
                        var taken = p.hands.Where(o => o != h && o.phase == p.phase && o.cell.IsValid).Select(o => o.cell).ToList();
                        h.cell = PlaySpots.RingCell(map, center, i, x, taken);
                        h.phase = p.phase;
                    }
                    cell = h.cell;
                    face = p.phase == 2 && p.place != null && p.place.Spawned ? new LocalTargetInfo(p.place) : new LocalTargetInfo(center);
                    return cell.IsValid;
                }
                case PlayKind.Vigil:
                {
                    var center = p.to == PlaySpot.Person ? (p.other != null && p.other.Spawned && p.other.Map == map ? p.other.Position : IntVec3.Invalid) : p.cell;
                    if (!center.IsValid) return false;
                    if (!h.cell.IsValid || !h.cell.InHorDistOf(center, 4.5f))
                    {
                        var taken = p.hands.Where(o => o != h && o.cell.IsValid).Select(o => o.cell).ToList();
                        h.cell = PlaySpots.RingCell(map, center, i, x, taken);
                    }
                    cell = h.cell;
                    face = p.to == PlaySpot.Person ? new LocalTargetInfo(p.other) : new LocalTargetInfo(center);
                    return cell.IsValid;
                }
                case PlayKind.Search:
                {
                    if (h.done >= h.route.Count) return false;
                    cell = h.route[h.done];
                    return true;
                }
            }
            return false;
        }

        /// <summary>They're where they should be: time at the far end of a walk, at the vigil, a searcher looking about.</summary>
        public static void Arrived(PlayOut p, Pawn x, PlayHand h, int delta)
        {
            switch (p.kind)
            {
                case PlayKind.Procession:
                    if (p.phase == 2) h.ticks += delta;
                    break;
                case PlayKind.Vigil:
                {
                    h.ticks += delta;
                    // After dark, a low glow at the grave or the bed they keep watch by (Immersion/Glows.cs).
                    if (p.hands.IndexOf(h) == 0)
                    {
                        var at = p.to == PlaySpot.Person ? p.other?.Position ?? IntVec3.Invalid : p.cell;
                        if (at.IsValid) VisualTouches.Candle(p.map, at, Find.TickManager.TicksGame + 300);
                    }
                    break;
                }
                case PlayKind.Search:
                {
                    h.waited += delta;
                    // Looking about: a turn now and then.
                    if (h.waited % 180 < delta) x.Rotation = Rot4.Random;
                    if (h.waited >= PlayOutMath.SearchWait(p.durationTicks, Math.Max(1, h.route.Count)))
                    {
                        h.done++;
                        h.waited = 0;
                    }
                    break;
                }
            }
        }

        /// <summary>Their place can't be reached safely: a searcher skips it, anyone else gives it up.</summary>
        public static void CantReach(PlayOut p, Pawn x, PlayHand h)
        {
            if (p.kind == PlayKind.Search)
            {
                h.done++;
                h.waited = 0;
                return;
            }
            h.left = true;
        }

        /// <summary>The errand's things are handed over: to the visitor (into their pack) or to whoever waits at the gate; the rest goes on the cart.</summary>
        public static bool HandOver(PlayOut p, Pawn carrier, Thing carried, Pawn to)
        {
            var t = PlayOutTracker.Instance;
            if (p == null || t == null || p.kind != PlayKind.Carry || p.state != PlayState.Running || carried == null) return false;
            int n = carried.stackCount;
            string what = carried.LabelNoCount;
            bool given = false;
            if (to != null && !to.Dead && to.Spawned && to.inventory != null && to.Faction != Faction.OfPlayer)
                given = carrier.carryTracker.innerContainer.TryTransferToContainer(carried, to.inventory.innerContainer, n) > 0;
            if (!given) carrier.carryTracker.DestroyCarriedThing();
            p.tally += n;
            int rest = p.amount - p.tally;
            if (rest > 0) PlayCarry.Take(p.map, p.what, p.thing, rest);
            p.tally = Math.Max(p.tally, p.amount);
            var h = p.Hand(carrier);
            if (h != null) h.counted = true;
            PlayWords.During(p, carrier);
            p.note = "CC_Play_Note_Carry".Translate(p.amount, PlayCarry.Label(p.what, p.thing)).Resolve();
            Finish(t, p, false, null, to != null && given ? to : null, what);
            return true;
        }

        // ---------------- Over ----------------

        /// <summary>Lets someone go from it: a work party's queue is cleared and, with <paramref name="now"/>, a hunt or work job in hand is stopped.</summary>
        private static void Stop(PlayOut p, Pawn x, bool now)
        {
            if (x == null) return;
            try
            {
                switch (p.kind)
                {
                    case PlayKind.WorkParty:
                        PlayWork.Release(x, now);
                        break;
                    case PlayKind.Hunt:
                        if (now && x.Spawned && PlayHunt.OnIt(p, x)) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        break;
                    default:
                        if (now && x.Spawned && OnIt(p, x)) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out stop " + p.kind, e);
            }
        }

        /// <summary>Took part, by the kind's own measure.</summary>
        private static bool Counts(PlayOut p, PlayHand h)
        {
            if (h.counted) return true;
            switch (p.kind)
            {
                case PlayKind.WorkParty: return PlayOutMath.WorkCounts(h.done, h.ticks, p.durationTicks);
                case PlayKind.Procession: return PlayOutMath.WalkCounts(h.ticks, PlayOutMath.WalkStay(p.durationTicks));
                case PlayKind.Vigil: return PlayOutMath.VigilCounts(h.ticks, p.durationTicks);
                case PlayKind.Search: return PlayOutMath.SearchCounts(h.done, h.route.Count);
                case PlayKind.Hunt: return p.tally > 0 && h.ticks >= 250;
                default: return false;
            }
        }

        /// <summary>It's over (or broken up by danger): the moods to those who took part, a word, the journals, a search's find.</summary>
        public static void Finish(PlayOutTracker t, PlayOut p, bool early, string why, Pawn to = null, string carried = null)
        {
            if (p == null || p.state == PlayState.Done) return;
            var map = p.map;
            if (p.kind == PlayKind.Hunt) p.tally = p.game.Count(a => a != null && a.Dead);
            foreach (var h in p.hands)
            {
                if (p.kind == PlayKind.WorkParty) PlayWork.Settle(p, h.pawn, h);
                Stop(p, h.pawn, early);
            }
            var took = p.hands.Where(h => h.pawn != null && !h.pawn.Dead && Counts(p, h)).Select(h => h.pawn).Distinct().ToList();
            if (p.note.NullOrEmpty()) p.note = PlayWords.Note(p, took);
            if (took.Count == 0)
            {
                t.Fallback(p, why ?? PlayWords.WhyNot(p), false);
                return;
            }
            t.Done(p, took, false);
            List<Thing> found = null;
            if (p.kind == PlayKind.Search && !early) found = GiveFind(p, took);
            string text = PlayWords.EndText(p, early, took, found, to, carried);
            if (!text.NullOrEmpty())
            {
                var look = new List<Thing>();
                foreach (var x in took)
                    if (x.Spawned) look.Add(x);
                if (found != null)
                    foreach (var f in found)
                        if (f.Spawned) look.Add(f);
                News.Post(text, look.Count > 0 ? new LookTargets(look) : LookTargets.Invalid, NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
            }
            PlayWords.After(p, took);
        }

        /// <summary>A search's find, at the feet of whoever looked longest (the first of them): the thing promised, or things of the colony's own era.</summary>
        private static List<Thing> GiveFind(PlayOut p, List<Pawn> took)
        {
            var finder = took.FirstOrDefault(x => x.Spawned && x.Map == p.map);
            if (finder == null) return null;
            var things = new List<Thing>();
            if (p.thing != null && p.amount > 0)
            {
                int left = p.amount;
                while (left > 0)
                {
                    var t = ThingMaker.MakeThing(p.thing, p.thing.MadeFromStuff ? GenStuff.DefaultStuffFor(p.thing) : null);
                    t.stackCount = Math.Min(left, Math.Max(1, p.thing.stackLimit));
                    left -= t.stackCount;
                    things.Add(t);
                }
            }
            else if (p.findValue > 0f) things.AddRange(Loot.Make(p.findValue));
            var placed = new List<Thing>();
            foreach (var t in things)
                if (GenPlace.TryPlaceThing(t, finder.Position, p.map, ThingPlaceMode.Near, out var lying))
                    placed.Add(lying ?? t);
            if (placed.Count > 0) p.note = "CC_Play_Note_Found".Translate(TextUtil.JoinAnd(placed.Select(x => x.LabelCap.ToString()).ToList())).Resolve();
            return placed;
        }

        /// <summary>
        /// It never came about (called by the tracker's fallback): anyone still on it is let go, an errand's things leave
        /// the stores as the answer always took them, and if the colony was waiting for it, a word and a line in a journal.
        /// </summary>
        public static void FellThrough(PlayOutTracker t, PlayOut p, string why, bool quiet)
        {
            foreach (var h in p.hands) Stop(p, h.pawn, true);
            if (p.kind == PlayKind.Carry) PlayCarry.Settle(p);
            if (quiet || p.label.NullOrEmpty()) return;
            string text = why.NullOrEmpty()
                ? "CC_Play_FellThroughPlain".Translate(p.label.CapitalizeFirst()).Resolve()
                : "CC_Play_FellThrough".Translate(p.label.CapitalizeFirst(), why).Resolve();
            if (p.kind == PlayKind.Carry) text += " " + "CC_Play_FellThrough_Carry".Translate(p.amount, PlayCarry.Label(p.what, p.thing)).Resolve();
            News.Post(text, null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            PlayWords.Missed(p);
        }
    }
}
