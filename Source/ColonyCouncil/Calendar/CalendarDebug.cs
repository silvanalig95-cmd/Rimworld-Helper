using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode: bring a calendar day round now, read the chronicle again, or let the first snow fall (Debug actions, "Rimfolk").</summary>
    public partial class ColonyCalendar
    {
        [DebugAction("Rimfolk", "Calendar: a day comes round now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugComeRound()
        {
            var self = InView;
            var map = self?.HomeMap;
            if (self == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var d in self.days.OrderBy(x => x.day))
            {
                var day = d;
                options.Add(new FloatMenuOption(day.label.CapitalizeFirst() + " (" + day.kind + ", " + day.way + ")", () =>
                {
                    int year = GenLocalDate.Year(map);
                    day.lastYear = year - 1;
                    // A custom comes as its question (CalendarCustoms.cs), when it can be asked now.
                    if (day.Custom)
                    {
                        bool asked = day.CustomDef != null && (DilemmaTracker.For(map)?.AskCustom(day.CustomDef) ?? false);
                        if (asked) day.lastYear = year;
                        Messages.Message("Calendar: " + day.label + (asked ? " is asked now." : " can't be asked now (its season, its weather, nobody to ask for it, or a question waiting)."),
                            MessageTypeDefOf.SilentInput, false);
                        return;
                    }
                    self.ComeRound(day, year, map);
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(the calendar is empty)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Calendar: read the chronicle again", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBackfill()
        {
            var self = InView;
            var map = self?.HomeMap;
            if (self == null || map == null) return;
            int before = self.days.Count;
            self.Backfill(map, GenLocalDate.DayOfYear(map), GenLocalDate.Year(map));
            Messages.Message("Calendar: " + (self.days.Count - before) + " day(s) added from the chronicle, the dead and the nemeses.", MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction("Rimfolk", "Calendar: the first snow comes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSnow()
        {
            var self = InView;
            var map = self?.HomeMap;
            if (self == null || map == null) return;
            if (self.Get("snow") == null)
            {
                self.TryAdd(new CalendarDay
                {
                    id = "snow", kind = CalendarKind.FirstSnow, way = DayWay.Games, weight = CalendarMath.SnowWeight,
                    day = GenLocalDate.DayOfYear(map), sinceYear = GenLocalDate.Year(map) - 1, tick = Find.TickManager.TicksGame, lastYear = GenLocalDate.Year(map) - 1
                }, false);
                self.snowSeen = true;
            }
            var snow = self.Get("snow");
            if (snow != null) snow.lastYear = GenLocalDate.Year(map) - 1;
            self.snowPendingTick = Find.TickManager.TicksGame;
            Messages.Message("Calendar: the first snow will come round within the hour (in the daytime).", MessageTypeDefOf.SilentInput, false);
        }
    }
}
