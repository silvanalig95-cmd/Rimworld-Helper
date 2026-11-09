using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Time off work, for the heart (docs/design/heart-felt.md, release 2, points 11 and 14; the player's words of 9
    /// November 2026: "i could for example also just grant a single stained pawn a break day or stuff like this"):
    /// <list type="bullet">
    /// <item>A day off the player gives one strained colonist (from strained on; from the heart's page, their card, the
    /// advice or a button on them): for a day their work hours are recreation and their first waking hours a lie-in, so
    /// they rest, take their recreation, walk and see friends, and do no ordinary work; it eases strain faster while it runs
    /// and more when it runs its course. Once in five days for each colonist.</item>
    /// <item>A bad afternoon from worn down, now and then (HeartCountMath.BadDayChancePerHour): off work for a few hours, a
    /// while in their room, a walk, a drink if they drink, a visit to a grave; it eases a good share of strain.</item>
    /// </list>
    /// Both played out on the map through their timetable (the hours changed are saved on the heart and put back after,
    /// only where nobody changed them since) and a node of Rimfolk's own in the game's think tree
    /// (Defs/HeartDefs/Rimfolk_Heart_TimeOff.xml), which stands down whenever they are drafted, need a doctor or danger
    /// comes, and lets emergencies (a fire, someone bleeding) come first. Under the day off at 85's safety rules (no
    /// danger, enough hands, nobody left uncared for); it ends early when danger comes, so they can be drafted at once.
    /// Never a mental state, never the game's break picker.
    /// </summary>
    public static class HeartTimeOff
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The player may give a day off (the setting "A day off for one colonist").</summary>
        public static bool DayOffOn => Heart.On && (S?.heartDayOff ?? true);

        /// <summary>A bad afternoon may come (the setting "A bad day from worn").</summary>
        public static bool BadDaysOn => Heart.On && (S?.heartBadDays ?? true);

        /// <summary>Time off of any kind is under way for them.</summary>
        public static bool Running(Pawn p)
        {
            var h = Heart.Of(p);
            return h != null && h.AnyOff;
        }

        public static TimeOffKind KindOf(PawnHeart h) => h != null && h.AnyOff ? (TimeOffKind)h.offKind : TimeOffKind.None;

        // ---------------- Who may ----------------

        /// <summary>
        /// The day off at 85's safety rules (Episodes.Blocked), as a key saying why not, or null: at home, one of the colony's
        /// free adults, up and about, no danger, not needing a doctor or nursing a baby, and enough others to carry on
        /// (<paramref name="others"/> adults who can work besides them).
        /// </summary>
        public static string Unsafe(Pawn p, HeartMapContext m, int others)
        {
            if (p == null || !p.Spawned || p.Map == null || !p.Map.IsPlayerHome) return "CC_Count_WhyNot_Away";
            if (!p.IsFreeNonSlaveColonist || p.IsQuestLodger() || p.timetable == null) return "CC_Count_WhyNot_NotFree";
            if (!p.DevelopmentalStage.Adult()) return "CC_Count_WhyNot_Child";
            if (p.Drafted || p.Downed || p.InMentalState) return "CC_Count_WhyNot_Busy";
            if ((m != null && m.threat) || MapFacts.ThreatOn(p.Map)) return "CC_Count_WhyNot_Danger";
            if (HealthAIUtility.ShouldSeekMedicalRest(p)) return "CC_Count_WhyNot_Care";
            if (Episodes.Nursing(p)) return "CC_Count_WhyNot_Nursing";
            int able = 0;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.Downed || o.InMentalState || o.IsQuestLodger() || !o.DevelopmentalStage.Adult()) continue;
                if (o.workSettings == null || !o.workSettings.EverWork) continue;
                if (Heart.Of(o)?.AnyOff ?? false) continue;
                able++;
            }
            return able < others ? "CC_Count_WhyNot_TooFew" : null;
        }

        /// <summary>Why the player can't give them a day off now (in words), or null when they can.</summary>
        public static string WhyNotDayOff(Pawn p, PawnHeart h, int now)
        {
            if (!DayOffOn) return "CC_Count_WhyNot_Off".Translate().Resolve();
            if (h == null) return "CC_Count_WhyNot_NoHeart".Translate(p.Named("PAWN")).Resolve();
            if (h.AnyOff) return "CC_Count_WhyNot_Already".Translate(p.Named("PAWN")).Resolve();
            if (h.strain < HeartCountMath.DayOffFrom) return "CC_Count_WhyNot_NotStrained".Translate(p.Named("PAWN")).Resolve();
            float readyIn = HeartCountMath.DayOffReadyIn((now - h.lastDayOffTick) / (float)GenDate.TicksPerDay);
            if (readyIn > 0f)
                return "CC_Count_WhyNot_Cooldown".Translate(p.Named("PAWN"), TextUtil.DaysString(UnityEngine.Mathf.CeilToInt(readyIn * GenDate.TicksPerDay)).Named("DAYS")).Resolve();
            string key = Unsafe(p, HeartMapContext.For(p.MapHeld, now), 1);
            return key?.Translate(p.Named("PAWN")).Resolve();
        }

        /// <summary>The day off's button: "A day off", or "A day off (in 3 days)" while it waits.</summary>
        public static string DayOffLabel(Pawn p, PawnHeart h, int now)
        {
            if (h != null && h.Off(TimeOffKind.DayOff)) return "CC_Count_DayOff_End".Translate().Resolve();
            float readyIn = h == null ? 0f : HeartCountMath.DayOffReadyIn((now - h.lastDayOffTick) / (float)GenDate.TicksPerDay);
            return readyIn > 0f
                ? "CC_Count_DayOff_ButtonIn".Translate(TextUtil.DaysString(UnityEngine.Mathf.CeilToInt(readyIn * GenDate.TicksPerDay)).Named("DAYS")).Resolve()
                : "CC_Count_DayOff_Button".Translate().Resolve();
        }

        /// <summary>The day off's button pressed: given, or (while one runs) ended early at the player's word.</summary>
        public static void DayOffButton(Pawn p)
        {
            var h = Heart.Of(p);
            if (h == null) return;
            if (h.Off(TimeOffKind.DayOff)) End(p, h, Find.TickManager.TicksGame, false, "CC_Count_Cut_Player");
            else GiveDayOff(p);
        }

        // ---------------- A day off ----------------

        /// <summary>Gives them a day off, if they may have one now. True when it began.</summary>
        public static bool GiveDayOff(Pawn p)
        {
            var h = Heart.Of(p);
            int now = Find.TickManager.TicksGame;
            if (h == null || WhyNotDayOff(p, h, now) != null) return false;
            Begin(p, h, TimeOffKind.DayOff, now, now + HeartCountMath.DayOffHours * GenDate.TicksPerHour);
            h.lastDayOffTick = now;
            string tag = "band:" + h.Band.ToString().ToLowerInvariant();
            if (!HeartActs.Say(p, null, "act:dayoff", tag)) HeartActs.Say(p, null, "act:dayoff", null);
            Notify.Message("CC_Count_DayOff_Given".Translate(p.Named("PAWN")).Resolve(), p, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            return true;
        }

        // ---------------- A bad afternoon ----------------

        /// <summary>
        /// The heart's hour (HeartLogic.Evaluate): from worn down, in the afternoon, now and then a bad afternoon begins, under
        /// the day off's safety rules and its own waits (a week for each colonist, two days in the colony).
        /// </summary>
        public static void Hour(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (!BadDaysOn || h == null || h.AnyOff || h.strain < HeartCountMath.BadDayFrom || !p.Spawned || p.InMentalState) return;
            int hour = GenLocalDate.HourOfDay(p.Map);
            if (hour < HeartCountMath.BadDayStartHour || hour > HeartCountMath.BadDayLastHour) return;
            if (!Rand.Chance(HeartCountMath.BadDayChancePerHour(h.strain))) return;
            TryBadDay(p, h, m, now, false);
        }

        /// <summary>A bad afternoon now, if it's safe and the waits are past (<paramref name="force"/>: dev mode, the waits aside). True when it began.</summary>
        public static bool TryBadDay(Pawn p, PawnHeart h, HeartMapContext m, int now, bool force)
        {
            var tracker = HeartTracker.Instance;
            if (h == null || tracker == null || h.AnyOff) return false;
            if (!force && (now - h.lastBadDayTick < HeartCountMath.BadDayPawnDays * GenDate.TicksPerDay
                           || now - tracker.lastColonyBadDayTick < HeartCountMath.BadDayColonyDays * GenDate.TicksPerDay)) return false;
            if (Unsafe(p, m, 2) != null || !RestUtility.Awake(p) || p.GetLord() != null || Episodes.Busy(p)) return false;
            int hours = Rand.RangeInclusive(HeartCountMath.BadDayMinHours, HeartCountMath.BadDayMaxHours);
            Begin(p, h, TimeOffKind.BadDay, now, now + hours * GenDate.TicksPerHour);
            h.lastBadDayTick = now;
            tracker.lastColonyBadDayTick = now;
            string tag = "band:" + h.Band.ToString().ToLowerInvariant();
            if (!HeartActs.Say(p, null, "act:badday", tag)) HeartActs.Say(p, null, "act:badday", null);
            string why = h.offWhy;
            string text = why.NullOrEmpty() ? "CC_Count_BadDay_StartNoWhy".Translate(p.Named("PAWN")).Resolve()
                : "CC_Count_BadDay_Start".Translate(p.Named("PAWN"), why.Named("WHY")).Resolve();
            Notify.Message(text, p, MessageTypeDefOf.NegativeEvent, Importance.Notable);
            return true;
        }

        // ---------------- Begin and end ----------------

        /// <summary>Time off begins: what wore them down to it in words, their timetable changed, and their work put down.</summary>
        private static void Begin(Pawn p, PawnHeart h, TimeOffKind kind, int now, int until)
        {
            h.offKind = (int)kind;
            h.offStart = now;
            h.offUntil = until;
            h.offWhy = HeartFelt.StrainWhy(h);
            h.offActTick = -999999;
            h.offHours ??= new List<string>();
            h.offHours.Clear();
            Timetable(p, h, kind, (until - now) / GenDate.TicksPerHour);
            // They put down their work now (never an order of the player's, nor someone carried).
            var job = p.CurJob;
            if (job != null && job.workGiverDef != null && !job.playerForced && !(p.carryTracker?.CarriedThing is Pawn))
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            HeartView.Refresh(p, h);
        }

        /// <summary>
        /// Their timetable for the time off: the work hours it covers become recreation (a day off: all of a day's, and the
        /// first two hours after they wake become a lie-in), each change saved as "hour=Was>Now". An hour the calendar's
        /// rest day had already given is taken over from it (ColonyCalendar.TakeRestHour), so each is put back once.
        /// </summary>
        private static void Timetable(Pawn p, PawnHeart h, TimeOffKind kind, int hours)
        {
            var tt = p.timetable;
            var joy = TimeAssignmentDefOf.Joy;
            var sleep = TimeAssignmentDefOf.Sleep;
            if (tt == null || joy == null) return;
            int now = GenLocalDate.HourOfDay(p.Map);
            int span = kind == TimeOffKind.DayOff ? GenDate.HoursPerDay : Math.Max(1, Math.Min(GenDate.HoursPerDay, hours + 1));
            var was = new TimeAssignmentDef[GenDate.HoursPerDay];
            var cal = p.Map != null ? ColonyCalendar.For(p.Map) : null;
            for (int i = 0; i < was.Length; i++)
            {
                // Only the hours the time off covers are taken over from the calendar's rest day; the rest stay its own.
                bool covered = (i - now + GenDate.HoursPerDay) % GenDate.HoursPerDay < span;
                string restWas = covered ? cal?.TakeRestHour(p, i) : null;
                var rest = restWas != null ? DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(restWas) : null;
                if (rest != null && (tt.GetAssignment(i) == joy || tt.GetAssignment(i) == sleep)) tt.SetAssignment(i, rest);
                was[i] = tt.GetAssignment(i);
            }
            var to = (TimeAssignmentDef[])was.Clone();
            for (int k = 0; k < span; k++)
            {
                int i = (now + k) % GenDate.HoursPerDay;
                if (was[i] == TimeAssignmentDefOf.Work || was[i] == TimeAssignmentDefOf.Anything) to[i] = joy;
            }
            // A lie-in: the first hours after their night's sleep, slept in.
            if (kind == TimeOffKind.DayOff && sleep != null)
                for (int k = 1; k <= GenDate.HoursPerDay; k++)
                {
                    int i = (now + k) % GenDate.HoursPerDay, before = (i + GenDate.HoursPerDay - 1) % GenDate.HoursPerDay;
                    if (was[before] != sleep || was[i] == sleep) continue;
                    for (int l = 0; l < HeartCountMath.LieInHours; l++) to[(i + l) % GenDate.HoursPerDay] = sleep;
                    break;
                }
            for (int i = 0; i < was.Length; i++)
            {
                if (to[i] == was[i] || to[i] == null || was[i] == null) continue;
                h.offHours.Add(i + "=" + was[i].defName + ">" + to[i].defName);
                tt.SetAssignment(i, to[i]);
            }
        }

        /// <summary>Their timetable put back: each hour changed goes back to what it was, unless someone changed it since.</summary>
        private static void PutBack(Pawn p, PawnHeart h)
        {
            var tt = p?.timetable;
            if (tt == null || h.offHours == null) return;
            foreach (var part in h.offHours)
            {
                int eq = part.IndexOf('='), gt = part.IndexOf('>');
                if (eq <= 0 || gt <= eq || !int.TryParse(part.Substring(0, eq), out int hour) || hour < 0 || hour >= GenDate.HoursPerDay) continue;
                var was = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(part.Substring(eq + 1, gt - eq - 1));
                var set = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(part.Substring(gt + 1));
                if (was != null && set != null && tt.GetAssignment(hour) == set) tt.SetAssignment(hour, was);
            }
            h.offHours.Clear();
        }

        /// <summary>
        /// Time off ends: their timetable put back, and strain eased (in full when it ran its course, half when cut short;
        /// nothing when the setting or the heart was switched off). <paramref name="cut"/> is why it was cut short (a key), or
        /// null.
        /// </summary>
        public static void End(Pawn p, PawnHeart h, int now, bool full, string cut, bool quiet = false)
        {
            if (h == null || !h.AnyOff) return;
            var kind = (TimeOffKind)h.offKind;
            try
            {
                PutBack(p, h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's time off: the timetable put back", e);
            }
            h.offKind = 0;
            h.offStart = h.offUntil = -1;
            if (p == null || p.Dead || quiet || !Heart.Eligible(p)) return;
            float eased = kind == TimeOffKind.DayOff ? HeartCountMath.DayOffEnd(full) : HeartCountMath.BadDayRelief(h.strain, full);
            if (eased > 0f) HeartLogic.Relieve(p, eased, StrainRelief.Episode);
            string key;
            if (full) key = kind == TimeOffKind.DayOff ? "CC_Count_DayOff_Over" : "CC_Count_BadDay_Over";
            else key = kind == TimeOffKind.DayOff ? "CC_Count_DayOff_Cut" : "CC_Count_BadDay_Cut";
            string why = cut != null ? cut.Translate(p.Named("PAWN")).Resolve() : "";
            Notify.Message(key.Translate(p.Named("PAWN"), why.Named("WHY")).Resolve(), p, full ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, Importance.Notable);
            HeartView.Refresh(p, h);
        }

        /// <summary>
        /// Every slice of the heart's hour (HeartTracker): time off under way ends when its hours are up, and is cut short
        /// when danger comes, they are downed, break, or leave the map; with the heart or its setting switched off, their
        /// timetable is put back quietly.
        /// </summary>
        public static void Check(HeartTracker tracker, int now)
        {
            if (tracker == null) return;
            List<(Pawn, PawnHeart)> due = null;
            foreach (var kv in tracker.All)
                if (kv.Value != null && kv.Value.AnyOff) (due ??= new List<(Pawn, PawnHeart)>()).Add((kv.Key, kv.Value));
            if (due == null) return;
            foreach (var (p, h) in due)
            {
                var kind = (TimeOffKind)h.offKind;
                bool on = kind == TimeOffKind.DayOff ? DayOffOn : BadDaysOn;
                if (!on || p == null || p.Dead) End(p, h, now, false, null, true);
                else if (!p.Spawned || p.Map == null) End(p, h, now, false, "CC_Count_Cut_Away");
                else if (MapFacts.ThreatOn(p.Map)) End(p, h, now, false, "CC_Count_Cut_Danger");
                else if (p.Downed) End(p, h, now, false, "CC_Count_Cut_Downed");
                else if (p.InMentalState && p.MentalStateDef != Episodes.Def) End(p, h, now, false, "CC_Count_Cut_Break");
                else if (now >= h.offUntil) End(p, h, now, true, null);
            }
        }

        // ---------------- What they do ----------------

        /// <summary>Between the acts of their time off: a bad afternoon's every hour or so, a day off's every three.</summary>
        public static int ActGapTicks(TimeOffKind kind) => kind == TimeOffKind.BadDay ? GenDate.TicksPerHour : GenDate.TicksPerHour * 3;

        private static readonly List<(float weight, Func<Job> make)> options = new List<(float, Func<Job>)>(6);

        /// <summary>
        /// One act of their time off, as a job (or null, and recreation and a wander fill the time): a bad afternoon's a while
        /// in their room, a while looking out, a drink if they drink, the grave or the bed of someone they grieve; a day off's
        /// a walk, their partner's company, the fire, or time with a friend (set going for when the two meet).
        /// </summary>
        public static Job ActFor(Pawn p, PawnHeart h)
        {
            var kind = KindOf(h);
            if (kind == TimeOffKind.None || p.Map == null) return null;
            string tag = "band:" + h.Band.ToString().ToLowerInvariant();
            bool visit = false;
            options.Clear();
            if (kind == TimeOffKind.BadDay)
            {
                bool grieving = (WeightKind)h.weightKind == WeightKind.Loss && h.weight >= 10f;
                if (grieving)
                {
                    options.Add((2f, () => HeartActs.GraveJob(p)));
                    options.Add((1.2f, () => HeartActs.LostBedJob(p, tag)));
                }
                options.Add((1.2f, () => HeartActs.CryJob(p, tag)));
                options.Add((1f, () => HeartActs.LookOutJob(p, tag)));
                options.Add((0.8f, () => HeartActs.DrinkJob(p)));
            }
            else
            {
                options.Add((1f, () => HeartActs.WalkJob(p, "act:dayoff")));
                options.Add((1f, () => HeartActs.SitWithJob(p, "act:dayoff")));
                options.Add((0.8f, () => HeartActs.FireJob(p, "act:dayoff")));
                // Time with a friend has no job of its own: it is set going for when the two meet.
                options.Add((1.2f, () =>
                {
                    visit = HeartActs.FriendVisit(p);
                    return null;
                }));
            }
            try
            {
                while (options.Count > 0)
                {
                    var o = options.RandomElementByWeight(x => x.weight);
                    options.Remove(o);
                    var job = o.make();
                    if (job != null || visit) return job;
                }
                return null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's time off: an act", e);
                return null;
            }
            finally
            {
                options.Clear();
            }
        }

        /// <summary>In words for the page and the summary: "a day off, until 14:00 tomorrow", "a bad afternoon, two hours more".</summary>
        public static string Line(Pawn p, PawnHeart h, int now)
        {
            var kind = KindOf(h);
            if (kind == TimeOffKind.None) return null;
            string left = TextUtil.DaysString(Math.Max(GenDate.TicksPerHour / 2, h.offUntil - now));
            return (kind == TimeOffKind.DayOff ? "CC_Count_Off_DayOff" : "CC_Count_Off_BadDay").Translate(p.Named("PAWN"), left.Named("LEFT")).Resolve();
        }
    }

    /// <summary>
    /// The heart's time off in the game's think tree (Defs/HeartDefs/Rimfolk_Heart_TimeOff.xml, before the colonist's main
    /// work): while a day off or a bad afternoon runs and their timetable says recreation, they don't take up ordinary work.
    /// Stands down when they are drafted, downed, need a doctor or danger comes (the game's own then).
    /// </summary>
    public class ThinkNode_ConditionalHeartTimeOff : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn == null || pawn.Drafted || pawn.Downed || pawn.Map == null) return false;
            var h = Heart.Of(pawn);
            if (h == null || !h.AnyOff) return false;
            if (pawn.timetable == null || pawn.timetable.CurrentAssignment != TimeAssignmentDefOf.Joy) return false;
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn) || MapFacts.ThreatOn(pawn.Map)) return false;
            return true;
        }
    }

    /// <summary>One act of their time off (HeartTimeOff.ActFor), at most every hour or so; null between them.</summary>
    public class JobGiver_HeartTimeOff : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var h = Heart.Of(pawn);
            if (h == null || !h.AnyOff) return null;
            int now = Find.TickManager.TicksGame;
            if (now - h.offActTick < HeartTimeOff.ActGapTicks(HeartTimeOff.KindOf(h))) return null;
            // Looked for once a gap whether it finds something or not: no search every time the think tree asks.
            h.offActTick = now;
            return HeartTimeOff.ActFor(pawn, h);
        }
    }

    /// <summary>The day off's button on a selected colonist, from strained on (or while their day off runs, to end it).</summary>
    [HarmonyLib.HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_HeartDayOffGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (!RimfolkPatches.On || !HeartTimeOff.DayOffOn) yield break;
            Gizmo c = null;
            try
            {
                c = GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the heart's day off: the button", e);
            }
            if (c != null) yield return c;
        }

        /// <summary>The button's words for the one selected, worked out again once a second of game time or when it changes.</summary>
        private static Pawn cachedFor;
        private static int cachedAt = -1;
        private static bool cachedRunning;
        private static string cachedLabel, cachedDesc, cachedWhy;

        private static Gizmo GizmoFor(Pawn p)
        {
            if (p == null || !p.IsColonistPlayerControlled || Find.Selector.NumSelected != 1) return null;
            var h = Heart.Of(p);
            if (h == null) return null;
            bool running = h.Off(TimeOffKind.DayOff);
            if (!running && h.strain < HeartCountMath.DayOffFrom) return null;
            int now = Find.TickManager.TicksGame;
            if (cachedFor != p || running != cachedRunning || now - cachedAt >= 60 || now < cachedAt)
            {
                cachedFor = p;
                cachedAt = now;
                cachedRunning = running;
                cachedLabel = HeartTimeOff.DayOffLabel(p, h, now);
                cachedDesc = running ? "CC_Count_DayOff_EndDesc".Translate(p.Named("PAWN")).Resolve() : "CC_Count_DayOff_Desc".Translate(p.Named("PAWN")).Resolve();
                cachedWhy = running ? null : HeartTimeOff.WhyNotDayOff(p, h, now);
            }
            var cmd = new Command_Action
            {
                defaultLabel = cachedLabel,
                defaultDesc = cachedDesc,
                icon = Icons.Heart,
                action = () =>
                {
                    HeartTimeOff.DayOffButton(p);
                    cachedAt = -1;
                }
            };
            if (cachedWhy != null) cmd.Disable(cachedWhy);
            return cmd;
        }
    }
}
