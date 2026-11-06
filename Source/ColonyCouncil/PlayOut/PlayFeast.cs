using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Why a feast is held: besides its guests, what sets one feast apart from another.</summary>
    public enum FeastWhy
    {
        /// <summary>An answer of the council's, an event's, a decree's or an ambition's (DilemmaEffect_Feast in a def).</summary>
        Answer,
        /// <summary>A day on the colony's calendar kept with a feast (Calendar/CalendarKeeping.cs).</summary>
        Day,
        /// <summary>A feast with guests from the neighbours (Diplomacy/Feasts.cs), the steward's suggestion among them.</summary>
        Guests,
        /// <summary>The feast after a wedding the council planned (Social/WeddingPlans.cs).</summary>
        Wedding,
        /// <summary>A family's supper: a meal at the table, with no cup raised (Social/FamilyBonds.cs).</summary>
        Supper
    }

    /// <summary>One feast to set going: its reason and name, who sits down and who leads, its guests, its hours, its words and what it leaves.</summary>
    public class FeastOrder
    {
        public FeastWhy why = FeastWhy.Answer;
        public Map map;
        /// <summary>What it's called on the map ("the harvest", "the feast for the Lenlia").</summary>
        public string label;
        public List<Pawn> invited;
        public Pawn organizer;
        /// <summary>Guests from outside the colony (PlayGuests.cs: "feast", "stranger", "camp", "kin", "visitors"), and how many at most.</summary>
        public string guestsFrom;
        public int guestCount = 6;
        public IntRange hours = new IntRange(PlayOutMath.DayFrom, PlayOutMath.DayTo);
        public float waitHours = PlayOutMath.DefaultWaitHours;
        public FloatRange duration = new FloatRange(2.5f, 3.5f);
        public List<PlaySpot> spots;
        public string pool;
        public List<string> lines;
        public bool music;
        /// <summary>The host raises a cup a while in; <see cref="toastLine"/> is what they say (else the reason's own words, else a toast's).</summary>
        public bool toast = true;
        public string toastLine;
        /// <summary>The good memory each who sits down has once it's over (held until it has happened: nobody's if it never does), and why.</summary>
        public ThoughtDef mood;
        public string moodCause;
        /// <summary>The colony's own doing: it waits for room in the colony's week (PlayOut/Stage.cs).</summary>
        public bool routine;
    }

    /// <summary>
    /// The one feast engine (phase 5 of the review): every feast the colony holds is set going here, so a calendar feast,
    /// an answered feast and a feast with guests differ only in their reason and their guests. On the map it is one
    /// gathering of the kind Feast (PlayOutTracker, LordJob_PlayOut): at the dining table unless told otherwise; the raw
    /// food the answer spent cooked into meals first (PlayCook.cs); a cooked meal from the stores served to everyone who
    /// sits down, the finest each may eat (Wishes/Beliefs.cs); the guests at their places round it (PlayGuests.cs); a
    /// while in, the host raises a cup and the table raises theirs (Immersion/GatheringTouches.cs), with the reason's
    /// words; and when it's over, the reason's moods to those who were there. What differs by the reason stays with its
    /// caller: a feast with guests keeps its contests, its champion and its welcome (Diplomacy/Feasts.cs), a calendar day
    /// its letter and tradition, an answer its outcome; and so does what happens when no feast can be set going.
    /// </summary>
    public static class PlayFeast
    {
        /// <summary>What's said at a feast, unless the order says.</summary>
        public const string Pool = "Council_Pool_PlayFeast";

        /// <summary>At the table first; the rec room, a gathering spot or the open air when there's no table to sit at.</summary>
        public static List<PlaySpot> TableFirst => new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather, PlaySpot.Outdoors };

        /// <summary>
        /// Sets the feast going: the plan, or null when nothing was (answers not playing out on the map, nobody to sit down).
        /// <paramref name="fill"/>: an answer's own way of naming who comes and where (DilemmaEffect_Gathering.Fill), in
        /// place of the order's; false when nobody can.
        /// </summary>
        public static PlayOut Stage(FeastOrder o, Func<PlayOut, bool> fill = null)
        {
            var tracker = PlayOutTracker.Instance;
            if (!PlayOuts.On || tracker == null || o?.map == null) return null;
            try
            {
                int now = Find.TickManager.TicksGame;
                var p = new PlayOut
                {
                    kind = PlayKind.Feast,
                    map = o.map,
                    created = now,
                    label = o.label,
                    hourFrom = o.hours.min,
                    hourTo = o.hours.max,
                    durationTicks = PlayOutMath.DurationTicks(o.duration.RandomInRange),
                    routine = o.routine
                };
                if (fill != null)
                {
                    if (!fill(p)) return null;
                }
                else
                {
                    p.invited = (o.invited ?? new List<Pawn>()).Where(PlayOuts.OurPerson).Distinct().ToList();
                    p.organizer = o.organizer != null && p.invited.Contains(o.organizer) ? o.organizer : null;
                    p.spots = o.spots?.ToList();
                    p.pool = o.pool;
                    p.lines = o.lines?.Where(l => !l.NullOrEmpty()).ToList() ?? new List<string>();
                    p.music = o.music;
                    p.guestsFrom = o.guestsFrom;
                    p.guestCount = o.guestCount;
                }
                if (p.invited == null || p.invited.Count == 0) return null;
                if (p.spots.NullOrEmpty()) p.spots = TableFirst;
                if (p.pool.NullOrEmpty()) p.pool = Pool;
                // The host's cup, with the reason's words; a family's supper has none.
                p.toast = o.toast && o.why != FeastWhy.Supper;
                p.toastLine = p.toast ? o.toastLine ?? ToastFor(o) : null;
                if (o.mood != null)
                    foreach (var x in p.invited)
                        p.moods.Add(new HeldMood { pawn = x, thought = o.mood, cause = o.moodCause ?? Causes.Current });
                p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(o.map), p.hourFrom, p.hourTo, o.waitHours);
                tracker.Add(p);
                PlayOuts.Register(p);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feast " + o.why, e);
                return null;
            }
        }

        /// <summary>The reason's own words for the cup: to the day kept, to the guests; else null (a toast's own words, by whoever raises it).</summary>
        private static string ToastFor(FeastOrder o)
        {
            switch (o.why)
            {
                case FeastWhy.Day:
                    return o.label.NullOrEmpty() ? null : "CC_Fest_Toast_Day".Translate(o.label.Named("DAY")).Resolve();
                case FeastWhy.Guests:
                    return "CC_Fest_Toast_Guests".Translate().Resolve();
                default:
                    return null;
            }
        }

        /// <summary>What the host says as the cups go up: the feast's own words, else a toast's.</summary>
        public static string ToastWords(PlayOut p, Pawn host) =>
            !(p?.toastLine).NullOrEmpty() ? p.toastLine : Flavor.FromPool("Council_Pool_PlayToast", host, null, null, "raise");
    }
}
