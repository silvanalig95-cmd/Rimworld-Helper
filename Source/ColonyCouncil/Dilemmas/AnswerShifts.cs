using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One colonist's hours or work an answer changed for a while: what changed, until when, and what ends it sooner.</summary>
    public class AnswerShift : IExposable
    {
        public Pawn pawn;
        public Map map;
        /// <summary>The changes (AnswerShiftMath.Write): each hour or work place, what it was and what it was set to.</summary>
        public string changes;
        /// <summary>Put back at this tick at the latest.</summary>
        public int until;
        /// <summary>Put back sooner, when this game condition is no longer on the map (a heat wave), if given.</summary>
        public string condition;
        /// <summary>What it was for ("the heat wave's hours"), for the word when it's put back.</summary>
        public string label;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref changes, "changes");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref condition, "condition");
            Scribe_Values.Look(ref label, "label");
        }
    }

    /// <summary>
    /// What an answer changes in the colony's own settings for a while and then puts back: hours on a colonist's
    /// timetable (work by night through a heat wave, a night at the bench, a late start after a night under the stars)
    /// and someone's work handed to another for a few days (the cooking, after a bad batch). Each is put back when its
    /// time is up or what it was for has ended (the heat wave is over), and only where nobody has changed it since: an
    /// hour the player set to something else stays as the player set it. Cheap when nothing is changed (one list check
    /// a tick), at most <see cref="MaxShifts"/> at once.
    /// </summary>
    public class AnswerShifts : GameComponent
    {
        public List<AnswerShift> shifts = new List<AnswerShift>();

        /// <summary>More than this at once and the oldest is put back.</summary>
        public const int MaxShifts = 40;

        private static AnswerShifts cached;
        private static Game cachedGame;

        public static AnswerShifts Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<AnswerShifts>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public AnswerShifts(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Someone's hours already changed by an answer and not yet put back (a plan isn't laid over another).</summary>
        public bool Shifted(Pawn p) => p != null && shifts.Any(s => s.pawn == p && AnswerShiftMath.Read(s.changes).Any(c => !c.work));

        /// <summary>
        /// A timetable plan (AnswerShiftMath.Plan) for each of these colonists, until its time is up or
        /// <paramref name="condition"/> ends. Those whose hours an answer already changed are left as they are. Returns
        /// who got it.
        /// </summary>
        public List<Pawn> Hours(Map map, IEnumerable<Pawn> who, string plan, string condition, string label)
        {
            var given = new List<Pawn>();
            if (map == null || who == null) return given;
            float hours = AnswerShiftMath.Hours(plan, GenLocalDate.HourFloat(map));
            int until = Now + (int)(hours * GenDate.TicksPerHour);
            foreach (var p in who.Distinct())
            {
                if (p?.timetable == null || p.Dead || Shifted(p)) continue;
                var cal = CalendarTracker.Instance;
                var changes = AnswerShiftMath.Plan(plan, h => p.timetable.GetAssignment(h)?.defName);
                if (changes.Count == 0) continue;
                var done = new List<AnswerShiftMath.Change>();
                foreach (var c in changes)
                {
                    var set = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(c.set);
                    if (set == null) continue;
                    // An hour today's rest day had turned goes back, later, to what it was before that.
                    string was = cal?.TakeRestHour(p, c.hour) ?? c.was;
                    p.timetable.SetAssignment(c.hour, set);
                    done.Add(AnswerShiftMath.Hour(c.hour, was, c.set));
                }
                if (done.Count == 0) continue;
                Add(new AnswerShift { pawn = p, map = map, changes = AnswerShiftMath.Write(done), until = until, condition = condition, label = label });
                given.Add(p);
            }
            return given;
        }

        /// <summary>
        /// A kind of work handed over for <paramref name="days"/> days: <paramref name="from"/> stops doing it, and
        /// <paramref name="to"/> takes it at the place it had (or first, when it had none). Both are put back after.
        /// </summary>
        public bool HandOver(Map map, Pawn from, Pawn to, WorkTypeDef work, float days, string label)
        {
            if (map == null || work == null || from?.workSettings == null || to?.workSettings == null || from == to) return false;
            if (to.WorkTypeIsDisabled(work)) return false;
            int was = from.workSettings.GetPriority(work);
            if (was <= 0) return false;
            int until = Now + (int)(days * GenDate.TicksPerDay);
            from.workSettings.SetPriority(work, 0);
            Add(new AnswerShift { pawn = from, map = map, changes = AnswerShiftMath.Write(new[] { AnswerShiftMath.WorkPlace(work.defName, was, 0) }), until = until, label = label });
            int toWas = to.workSettings.GetPriority(work);
            if (toWas <= 0 || toWas > was)
            {
                to.workSettings.SetPriority(work, was);
                Add(new AnswerShift { pawn = to, map = map, changes = AnswerShiftMath.Write(new[] { AnswerShiftMath.WorkPlace(work.defName, toWas, was) }), until = until, label = label });
            }
            return true;
        }

        private void Add(AnswerShift s)
        {
            shifts.Add(s);
            while (shifts.Count > MaxShifts)
            {
                var old = shifts[0];
                shifts.RemoveAt(0);
                PutBack(old, false);
            }
        }

        public override void GameComponentTick()
        {
            if (shifts.Count == 0) return;
            int now = Now;
            // On the tick of the things answers keep on the map (OnMapTimed): light, and only while something is changed.
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareTimed)) return;
            using var clock = PassClock.Time(Pass.RareAnswerShifts);
            List<AnswerShift> over = null;
            foreach (var s in shifts)
            {
                bool done;
                try
                {
                    done = s.pawn == null || s.pawn.Dead || now >= s.until || !Still(s);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("answer shift", e);
                    done = true;
                }
                if (done) (over ??= new List<AnswerShift>()).Add(s);
            }
            if (over == null) return;
            var told = new List<Pawn>();
            string label = null;
            foreach (var s in over)
            {
                shifts.Remove(s);
                if (PutBack(s, true) && s.pawn != null && !s.pawn.Dead)
                {
                    told.Add(s.pawn);
                    label ??= s.label;
                }
            }
            if (told.Count > 0 && !label.NullOrEmpty())
                Messages.Message("CC_Shift_Back".Translate(label.CapitalizeFirst(), TextUtil.JoinAnd(told.Select(p => p.LabelShort).Distinct().ToList())).Resolve(),
                    new LookTargets(told.Where(p => p.Spawned)), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>What it was for is still going on (the heat wave isn't over).</summary>
        private static bool Still(AnswerShift s)
        {
            if (s.condition.NullOrEmpty()) return true;
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(s.condition);
            var map = s.map ?? s.pawn?.MapHeld;
            if (def == null || map == null || !Find.Maps.Contains(map)) return false;
            return map.gameConditionManager.ConditionIsActive(def);
        }

        /// <summary>Every hour and work place still as the answer set it goes back to what it was. True when anything did.</summary>
        private static bool PutBack(AnswerShift s, bool tell)
        {
            var p = s?.pawn;
            if (p == null || p.Dead) return false;
            bool any = false;
            try
            {
                foreach (var c in AnswerShiftMath.Read(s.changes))
                {
                    if (c.work)
                    {
                        var w = DefDatabase<WorkTypeDef>.GetNamedSilentFail(c.key);
                        if (w == null || p.workSettings == null || p.WorkTypeIsDisabled(w) || !int.TryParse(c.was, out int was) || !int.TryParse(c.set, out int set)) continue;
                        if (p.workSettings.GetPriority(w) != set) continue;
                        p.workSettings.SetPriority(w, was);
                        any = true;
                        continue;
                    }
                    if (p.timetable == null) continue;
                    var to = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(c.was);
                    if (to == null || p.timetable.GetAssignment(c.hour)?.defName != c.set) continue;
                    p.timetable.SetAssignment(c.hour, to);
                    any = true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("answer shift put back", e);
            }
            return any && tell;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref shifts, "shifts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (shifts == null) shifts = new List<AnswerShift>();
                shifts.RemoveAll(s => s == null || s.pawn == null);
            }
        }
    }
}
