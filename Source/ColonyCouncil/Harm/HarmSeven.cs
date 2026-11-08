using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public static class HarmSevenDefOf
    {
        public static JobDef Rimfolk_PracticeWalk;
        public static JobDef Rimfolk_StoresSneak;

        static HarmSevenDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HarmSevenDefOf));
    }

    /// <summary>
    /// Harm, part 7: the last of the three batches of the review's remaining proposals the player took on 7 November 2026
    /// ("all suggestions please"), tempers in the colony, visitors and outsiders, and personal stories
    /// (docs/harm-review-proposals.md, each marked built there): after it every proposal of the review is built. Each goes
    /// through the harm service with its kind, so the shared budget, the setting, the storyteller and the cap hold for all
    /// of them (Harm/Harm.cs, HarmBudgetMath.cs; the rates in HarmSevenMath.cs); the three that hurt nobody of the colony
    /// (the nemesis's aim, the lost child, the courier at the edge) take the setting and the lean alone and stay outside
    /// the budget. This is the hub, on the harm book's hour (what is waiting, the visitors at the gate, the evenings) and
    /// its 250-tick slices (each colonist's hour; a fight going on; the moments set for a little later); nothing here has
    /// a clock of its own. The rest is in Tempers.cs, GateTroubles.cs and Stories.cs.
    /// </summary>
    public static class HarmSeven
    {
        /// <summary>
        /// Waiting in the harm book (HarmWaiting.what): a bitter one's walk to the speaker, a week's grudge over what went
        /// to someone else, the wronged one's and the betrayed keeper's walks, the one faith's evening, the open gate's
        /// stranger, the gambler's games, the guest of rank's enemy, a challenge written, an heir's grudge, a rush's brawl.
        /// </summary>
        public const string Speaker = "speaker", Passed = "passed", Wronged = "wronged", Leak = "leak", Altar = "altar", Gates = "gates", Gamble = "gamble",
            Rank = "rank", Challenge = "challenge", Heir = "heir", Creek = "creek";

        /// <summary>Part 7's waiting things that need nobody of the colony (a decree's or a rush's one look, an heir's grudge, a guest of rank's visit).</summary>
        public static bool Unpeopled(string what) => what == Altar || what == Gates || what == Creek || what == Heir || what == Rank;

        private static readonly List<Pawn> slice = new List<Pawn>();

        /// <summary>Moments set for a little later (the blows after the words): session memory, a reload drops them.</summary>
        private static readonly List<(int at, Action act)> later = new List<(int, Action)>();

        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            later.Clear();
            Tempers.Reset();
            GateTroubles.Reset();
            Stories.Reset();
        }

        /// <summary>Something to do a little later (at the next 250-tick slice past <paramref name="ticks"/>).</summary>
        public static void Later(int ticks, Action act)
        {
            if (act == null || Current.Game == null) return;
            if (later.Count > 60) later.RemoveAt(0);
            later.Add((Now + Math.Max(0, ticks), act));
        }

        /// <summary>The harm book's hour (its first slice).</summary>
        public static void Hourly(HarmBook book, int now)
        {
            Safe("the tempers", () => Tempers.Hourly(now));
            Safe("the visitors", () => GateTroubles.Hourly(book, now));
            Safe("the stories", () => Stories.Hourly(now));
        }

        /// <summary>One slice of the colony's hour (every 250 ticks): each colonist in it once an hour; a fight going on and what was set for later every time.</summary>
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
                    Tempers.Hour(p, now);
                    Stories.Hour(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm seven: the hour of " + p.LabelShort, e);
                }
            }
            slice.Clear();
            Safe("the social fights", () => Tempers.Fights(now));
            Safe("a fight going on", () => Stories.Battles(now));
            Safe("hungry strangers", () => GateTroubles.HungrySlice(now));
            if (later.Count > 0) Safe("what was set for later", () => RunLater(now));
        }

        private static void RunLater(int now)
        {
            for (int i = 0; i < later.Count; i++)
            {
                if (later[i].at > now) continue;
                var act = later[i].act;
                later.RemoveAt(i--);
                try
                {
                    act();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm seven: a moment set for later", e);
                }
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: " + what, e);
            }
        }

        /// <summary>Part 7's waiting things, at the hour (HarmSix.Due's own): true when done with.</summary>
        public static bool Due(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                case Speaker: return Tempers.SpeakerHour(x, now);
                case Passed: return Tempers.PassedHour(x, now);
                case Wronged: return Tempers.WrongedHour(x, now);
                case Leak: return Tempers.LeakHour(x, now);
                // Markers: looked at where they happen (an evening of faith, a decree's visitors, a rush), kept so each comes once.
                case Altar: return false;
                case Gates: return false;
                case Gamble: return GateTroubles.GambleHour(x, now);
                case Rank: return GateTroubles.RankHour(x, now);
                case Challenge: return GateTroubles.ChallengeHour(x, now);
                case Heir: return false;
                case Creek: return false;
                default: return true;
            }
        }

        /// <summary>Part 7's waiting things whose time has passed without them coming.</summary>
        public static bool Lapsed(HarmWaiting x, int now)
        {
            int age = now - x.made;
            switch (x.what)
            {
                case Speaker: return age > HarmSevenMath.SpeakerHours * HarmMath.HourTicks;
                case Passed: return !HarmSevenMath.PassedOverKept(x.made, now);
                case Wronged:
                case Leak: return age > HarmSevenMath.FindHours * HarmMath.HourTicks;
                case Altar: return age > HarmSevenMath.AltarKeepDays * HarmMath.DayTicks;
                case Gates: return age > HarmSevenMath.GatesKeepDays * HarmMath.DayTicks;
                case Gamble: return age > HarmSevenMath.GamblerKeepDays * HarmMath.DayTicks;
                case Rank: return age > (HarmSevenMath.RankEnemyDays + 2f) * HarmMath.DayTicks;
                case Challenge: return age > HarmSevenMath.ChallengeKeepDays * HarmMath.DayTicks;
                case Heir: return age > HarmSevenMath.HeirKeepDays * HarmMath.DayTicks;
                case Creek: return age > HarmSevenMath.CreekKeepDays * HarmMath.DayTicks;
                default: return true;
            }
        }

        /// <summary>A waiting thing of this kind matching, or null.</summary>
        public static HarmWaiting WaitingOf(string what, Predicate<HarmWaiting> match)
        {
            var list = HarmBook.Instance?.waiting;
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].what == what && (match == null || match(list[i]))) return list[i];
            return null;
        }

        /// <summary>A walk of part 7's reached its place (JobDriver_HarmWalk): a mage's practice, a thief at the stores.</summary>
        public static void Arrived(Pawn p, JobDef def)
        {
            try
            {
                if (def == HarmSevenDefOf.Rimfolk_PracticeWalk) Stories.Practising(p);
                else if (def == HarmSevenDefOf.Rimfolk_StoresSneak) GateTroubles.AtStores(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: a walk's arrival", e);
            }
        }

        /// <summary>A walk of part 7's is over (JobDriver_HarmWalk): the practice done, the thief's look about the stores.</summary>
        public static void Ended(Pawn p, JobDef def)
        {
            try
            {
                if (def == HarmSevenDefOf.Rimfolk_PracticeWalk) Stories.PracticeDone(p);
                else if (def == HarmSevenDefOf.Rimfolk_StoresSneak) GateTroubles.SneakDone(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: a walk's end", e);
            }
        }
    }
}
