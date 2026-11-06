using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // A fresh start on the calendars (Colonies/Foundings.cs): the parting day on the old colony's calendar, the day its
    // people set out to found the new one, which it may keep as a tradition; and, with the old colony abandoned in the same
    // act, its founding day on the new colony's calendar as a remembrance (their old home's day). Both are kept quietly,
    // as a remembrance, by the way's own words.
    public partial class ColonyCalendar
    {
        /// <summary>A parting day is worth about a homecoming: kept while those who knew it live, and may become a tradition.</summary>
        public const float PartingWeight = 34f;

        /// <summary>This colony's founding, as a game tick (0, the game's start, for the colony settled first).</summary>
        public int FoundedTick => FoundedAt;

        /// <summary>The parting day on the old colony's calendar: today, named for the new colony, known by those who went and those who stayed.</summary>
        public static void Notify_Parting(Map mother, string newColony, List<Pawn> went)
        {
            var self = For(mother);
            var map = self?.HomeMap;
            if (map == null) return;
            string id = "parting|" + newColony;
            if (self.Get(id) != null) return;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var knew = Homes.People(map).Select(p => p.ThingID).ToList();
            knew.AddRange(went.Where(p => p != null).Select(p => p.ThingID));
            var d = Dated(new CalendarDay
            {
                id = id, kind = CalendarKind.Parting, way = DayWay.Remembrance, subject = newColony, otherName = Foundings.Names(went), knew = knew.Distinct().ToList(),
                weight = PartingWeight, label = "CC_Cal_Name_Parting".Translate(newColony.Named("PLACE")).Resolve()
            }, Find.TickManager.TicksGame, map, today, year, true);
            self.TryAdd(d, true);
        }

        /// <summary>The old colony's founding day on the new colony's calendar, its own date, once the old colony is abandoned in the same act.</summary>
        public static void Notify_OldFounding(Map home, string oldColony, int foundedTick)
        {
            var self = For(home);
            var map = self?.HomeMap;
            if (map == null || oldColony.NullOrEmpty()) return;
            string id = "oldfounding|" + oldColony;
            if (self.Get(id) != null) return;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var d = Dated(new CalendarDay
            {
                id = id, kind = CalendarKind.Parting, way = DayWay.Remembrance, subject = oldColony, knew = Homes.People(map).Select(p => p.ThingID).ToList(),
                weight = PartingWeight, label = "CC_Cal_Name_OldFounding".Translate(oldColony.Named("PLACE")).Resolve()
            }, Math.Max(0, foundedTick), map, today, year, false);
            self.TryAdd(d, true);
        }

        /// <summary>Where a parting day comes from, in a line under its name.</summary>
        public static string PartingOrigin(CalendarDay d) =>
            d.id != null && d.id.StartsWith("oldfounding|")
                ? "CC_Cal_Origin_OldFounding".Translate((d.subject ?? "?").Named("PLACE"), d.sinceYear.Named("YEAR")).Resolve()
                : "CC_Cal_Origin_Parting".Translate((d.otherName ?? "?").Named("NAMES"), (d.subject ?? "?").Named("PLACE"), d.sinceYear.Named("YEAR")).Resolve();
    }
}
