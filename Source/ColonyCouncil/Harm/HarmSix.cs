using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public static class HarmSixDefOf
    {
        public static JobDef Rimfolk_DawnWalk;
        public static JobDef Rimfolk_MusterPost;
        public static JobDef Rimfolk_DareWalk;
        public static JobDef Rimfolk_ShooVisitor;

        static HarmSixDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HarmSixDefOf));
    }

    /// <summary>
    /// Harm, part 6: the second batch of the review's remaining proposals the player took on 7 November 2026 ("all
    /// suggestions please"), the work's accidents, the animals and the contests and festivals (docs/harm-review-proposals.md,
    /// each marked built there), each through the harm service with its kind, so the shared budget, the setting, the
    /// storyteller and the cap hold for all of them (Harm/Harm.cs, HarmBudgetMath.cs; the rates in HarmSixMath.cs). This is
    /// the hub, on the harm book's hour (the answers' work, the animals at the stores, the evening's contests, what is
    /// waiting) and its 250-tick slices (each colonist's hour; a fight going on); nothing here has a clock of its own. The
    /// rest is in AnswerHurts.cs, WorkSlips.cs, WildHurts.cs and Contests.cs.
    /// </summary>
    public static class HarmSix
    {
        /// <summary>Waiting in the harm book (HarmWaiting.what): a fall owed by one let back too soon, a field hand's soreness on the way home, a coward's name waiting for the next fight.</summary>
        public const string Fall = "fall", Hands = "hands", Coward = "coward";

        private static readonly List<Pawn> slice = new List<Pawn>();

        public static void Reset()
        {
            AnswerHurts.Reset();
            WorkSlips.Reset();
            WildHurts.Reset();
            Contests.Reset();
        }

        /// <summary>The harm book's hour (its first slice).</summary>
        public static void Hourly(HarmBook book, int now)
        {
            Safe("the answers' work", () => AnswerHurts.Hourly(now));
            Safe("the animals", () => WildHurts.Hourly(now));
            Safe("the contests", () => Contests.Hourly(now));
        }

        /// <summary>One slice of the colony's hour (every 250 ticks): each colonist in it once an hour; a fight going on every time.</summary>
        public static void Slice(HarmBook book, int which, int now)
        {
            slice.Clear();
            var all = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (Sched.SliceOf(all[i].thingIDNumber, Sched.Slices) == which) slice.Add(all[i]);
            for (int i = 0; i < slice.Count; i++)
            {
                var p = slice[i];
                if (p?.Map == null || p.Dead || p.RaceProps?.Humanlike != true || p.health?.hediffSet == null) continue;
                try
                {
                    WorkSlips.Hour(book, p, now);
                    WildHurts.Hour(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm six: the hour of " + p.LabelShort, e);
                }
            }
            slice.Clear();
            Safe("a fight going on", () => Contests.Battles(book, now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: " + what, e);
            }
        }

        /// <summary>Part 6's waiting things, at the hour (HarmFive.Due's own): true when done with.</summary>
        public static bool Due(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                // The fall and the coward's name are looked at where they happen (a colonist's hour, a fight going on).
                case Fall: return false;
                case Coward: return false;
                case Hands: return AnswerHurts.HandsHome(x, now);
                default: return true;
            }
        }

        /// <summary>Part 6's waiting things whose time has passed without them coming.</summary>
        public static bool Lapsed(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                case Fall: return now - x.made > HarmSixMath.BackTooSoonKeepDays * HarmMath.DayTicks;
                case Hands: return now - x.made > HarmSixMath.HandsKeepDays * HarmMath.DayTicks;
                case Coward: return now - x.made > HarmSixMath.CowardKeepDays * HarmMath.DayTicks;
                default: return true;
            }
        }

        /// <summary>Someone's waiting thing of this kind, or null.</summary>
        public static HarmWaiting WaitingFor(HarmBook book, Pawn p, string what)
        {
            var list = book?.waiting;
            if (list == null || p == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].what == what && list[i].pawn == p) return list[i];
            return null;
        }

        /// <summary>A walk of part 6's reached its place (JobDriver_HarmWalk): the dawn walk's look about, a shooer at the visitor, a shooter at the post.</summary>
        public static void Arrived(Pawn p, JobDef def)
        {
            try
            {
                if (def == HarmSixDefOf.Rimfolk_DawnWalk) WildHurts.DawnArrived(p);
                else if (def == HarmSixDefOf.Rimfolk_ShooVisitor) WildHurts.Shooed(p);
                else if (def == HarmSixDefOf.Rimfolk_MusterPost) Contests.AtPost(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: a walk's arrival", e);
            }
        }

        /// <summary>A walk of part 6's is over (JobDriver_HarmWalk): a dare's swim, a muster's practice.</summary>
        public static void Ended(Pawn p, JobDef def)
        {
            try
            {
                if (def == HarmSixDefOf.Rimfolk_DareWalk) Contests.SwimDone(p);
                else if (def == HarmSixDefOf.Rimfolk_MusterPost) Contests.PostDone(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: a walk's end", e);
            }
        }
    }
}
