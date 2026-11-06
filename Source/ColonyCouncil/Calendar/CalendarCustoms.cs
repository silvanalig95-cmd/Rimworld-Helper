using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The festive year (phase 5 of the review, the player's decision 7 of 6 November 2026): every custom the council keeps
    /// lives on the calendar with the colony's other days, under one tradition rule. A custom asked as a council question
    /// (a midsummer bonfire, the harvest dance, the planting day) goes on the calendar the first time the colony keeps it:
    /// a seasonal one on the day it was kept, its question coming round on that date each year (or within a few days of
    /// it, when its weather or someone to ask for it isn't there; past those its year is spent, with no blame); one asked
    /// in its moment (the fields opening, a victory, news for the bees) with its occasion, as it always came. Kept three
    /// years running it becomes a tradition (CalendarMath.KeepInYear, once a year); let pass before that, it comes off the
    /// calendar and is a question again; a tradition let pass costs as it always did and lapses the second time in a row.
    /// A season that holds a custom's day is asked no other custom (the colony's pace: one a season). A custom's count and
    /// its tradition live on its day, and the council's questions read them from here (Dilemmas/Dilemmas.cs); an older
    /// save's counts are carried over once (DilemmaTracker.MoveCustoms). With the calendar off, its customs come as the
    /// council's questions in their season, as they did before.
    /// </summary>
    public partial class ColonyCalendar
    {
        public static string CustomId(DilemmaDef def) => "custom|" + def.defName;

        /// <summary>A custom that keeps a date: one of a season, not one asked in its moment (the planting day as the fields open).</summary>
        public static bool KeepsDate(DilemmaDef def) => def != null && !def.seasons.NullOrEmpty() && !def.timely;

        public CalendarDay CustomOf(DilemmaDef def) => def == null ? null : Get(CustomId(def));

        /// <summary>The custom's question comes from the calendar on its day (the calendar on, and the custom on it with a date).</summary>
        public bool BringsCustom(DilemmaDef def)
        {
            if (def == null || def.traditionOption < 0 || !On) return false;
            var d = CustomOf(def);
            return d != null && d.Dated;
        }

        /// <summary>A custom's day on the calendar falls in the season of this day of the year: no other custom is asked in it.</summary>
        public bool CustomSeasonHeld(int dayOfYear)
        {
            if (!On) return false;
            int season = StageMath.SeasonOf(CalendarMath.Wrap(dayOfYear));
            return days.Any(d => d.Custom && d.Dated && StageMath.SeasonOf(d.day) == season);
        }

        /// <summary>The customs on the calendar that are traditions, as their questions.</summary>
        public IEnumerable<DilemmaDef> CustomTraditions => days.Where(d => d.Custom && d.tradition).Select(d => d.CustomDef).Where(def => def != null);

        private static CalendarDay NewCustom(DilemmaDef def, Map map, int tick)
        {
            int year = YearOf(tick, map);
            return new CalendarDay
            {
                id = CustomId(def), kind = CalendarKind.Custom, way = DayWay.Custom, label = def.traditionLabel.NullOrEmpty() ? def.label : def.traditionLabel,
                about = def.defName, moment = !KeepsDate(def), day = CalendarMath.Wrap(DayOf(tick, map)), sinceYear = year, tick = tick, lastYear = year,
                weight = CalendarMath.CustomWeight
            };
        }

        /// <summary>The colony kept a custom that wasn't on its calendar: written down, on today's date or with its occasion.</summary>
        internal CalendarDay AddCustom(DilemmaDef def, Map map)
        {
            var existing = CustomOf(def);
            if (existing != null || def == null || map == null) return existing;
            var d = NewCustom(def, map, Find.TickManager.TicksGame);
            days.Add(d);
            if (On)
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, (d.moment ? "CC_Fest_CustomOnMoment" : "CC_Fest_CustomOn")
                    .Translate(d.label.CapitalizeFirst().Named("CUSTOM"), DateText(d.day, map).Named("DATE")).Resolve(), null, MessageTypeDefOf.NeutralEvent);
            return d;
        }

        /// <summary>An older save's count for a custom, carried onto the calendar (once, DilemmaTracker.MoveCustoms): dated the last time it was asked.</summary>
        internal void CarryCustom(DilemmaDef def, Map map, int count, bool wasTradition, int breaks, int askedTick)
        {
            if (def == null || map == null || CustomOf(def) != null) return;
            var d = NewCustom(def, map, Mathf.Max(0, askedTick));
            CalendarMath.CarryCustom(count, wasTradition, breaks, out d.keptYears, out d.tradition, out d.breaks);
            d.timesKept = d.keptYears;
            if (count > 0) d.lastKeptYear = d.lastYear;
            days.Add(d);
        }

        /// <summary>A custom let pass before it became a tradition, or one that lapsed: off the calendar, a question again.</summary>
        internal void LetCustomGo(CalendarDay d, Map map, bool lapsed)
        {
            if (d == null || !days.Remove(d) || lapsed || !On) return;
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Fest_CustomOff".Translate(d.label.CapitalizeFirst().Named("CUSTOM")).Resolve(), null,
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// A custom whose day has come: its question is asked (the council's own, with its own answers), today or, when its
        /// weather or someone to ask for it isn't there, at a later hour within its grace days. True when one was asked.
        /// </summary>
        private bool AskCustoms(Map map, int today, int year)
        {
            if (!(S?.enableDilemmas ?? false)) return false;
            var tracker = DilemmaTracker.For(map);
            if (tracker == null) return false;
            foreach (var d in days.Where(x => x.Custom && x.Dated).OrderByDescending(x => x.Standing).ToList())
            {
                if (!CalendarMath.CustomDue(today, year, d.day, d.lastYear, out int occurrence)) continue;
                var def = d.CustomDef;
                if (def == null || !tracker.AskCustom(def)) continue;
                d.lastYear = occurrence;
                return true;
            }
            return false;
        }

        /// <summary>Once a day: a custom whose grace days ran out without its question is spent for the year, without blame.</summary>
        private void MarkCustomMissed(CalendarDay d, int today, int year)
        {
            if (!d.Dated || !CalendarMath.CustomMissed(today, d.day)) return;
            int occurrence = CalendarMath.LastOccurrence(today, year, d.day);
            if (d.lastYear >= occurrence) return;
            d.lastYear = occurrence;
            if (On && occurrence > d.sinceYear && (S?.enableDilemmas ?? false))
                Tell.Minor(NewsKind.Colony, Importance.Notable, HomeMap, "CC_Cal_Missed".Translate(d.label.CapitalizeFirst()).Resolve(), null, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Once a day: a custom whose question is gone (a mod's, removed) leaves the calendar; one whose kind changed keeps up with it.</summary>
        private void PruneCustom(CalendarDay d)
        {
            var def = d.CustomDef;
            if (def == null || def.traditionOption < 0)
            {
                days.Remove(d);
                return;
            }
            d.moment = !KeepsDate(def);
        }

        /// <summary>The answer that keeps a custom, as its question words it, when it names nobody ("Make a day of the planting"); else null.</summary>
        public static string KeepingAnswer(DilemmaDef def)
        {
            if (def == null || def.traditionOption < 0 || def.traditionOption >= def.options.Count) return null;
            string label = def.options[def.traditionOption].label;
            return label.NullOrEmpty() || label.Contains("{") ? null : label;
        }
    }
}
