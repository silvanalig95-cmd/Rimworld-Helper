using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Two councils become one on the calendars (Colonies/Merges.cs; the player's decision 12A): the traditions of the colony
    // whose people came join this colony's (one only they kept at half its strength, one both kept the stronger of the two),
    // and their old colony's founding day is kept here as a remembrance while some of them live here, inside the calendar's
    // cap of twelve days it writes down.
    public partial class ColonyCalendar
    {
        /// <summary>
        /// The traditions of <paramref name="theirs"/> (the calendar of the colony that came, kept since its home was lost)
        /// joined to this one, by ManyColoniesMath.Tradition: the names of those that joined or grew stronger, for the letter.
        /// </summary>
        public List<string> MergeFrom(ColonyCalendar theirs, List<Pawn> newcomers, Map map)
        {
            var joined = new List<string>();
            if (theirs == null || theirs == this || theirs.days == null) return joined;
            var ids = (newcomers ?? new List<Pawn>()).Where(p => p != null).Select(p => p.ThingID).ToList();
            foreach (var d in theirs.days.ToList())
            {
                // A tradition of theirs: never their founding day (kept as a remembrance below) nor someone's own day.
                if (d == null || !d.tradition || d.kind == CalendarKind.Founding || d.Personal || d.id.NullOrEmpty()) continue;
                var ours = Get(d.id);
                switch (ManyColoniesMath.Tradition(true, ours != null && ours.tradition, d.Standing, ours?.Standing ?? 0f))
                {
                    case TraditionMerge.Join:
                    {
                        int kept = d.keptYears;
                        float weight = d.weight;
                        ManyColoniesMath.Halve(ref kept, ref weight);
                        if (ours != null)
                        {
                            // Theirs, kept here before but never a tradition: a tradition now, at the stronger of the two.
                            ours.tradition = true;
                            ours.keptYears = Math.Max(ours.keptYears, kept);
                            ours.weight = Math.Max(ours.weight, weight);
                            ours.knew = ours.knew.Union(ids).ToList();
                            joined.Add(ours.label);
                            break;
                        }
                        var copy = Copy(d);
                        copy.keptYears = kept;
                        copy.weight = weight;
                        copy.knew = d.knew.Union(ids).ToList();
                        if (TryAdd(copy, false) == copy) joined.Add(copy.label);
                        break;
                    }
                    case TraditionMerge.Theirs:
                        // Both kept it, theirs the stronger: the colony's keeps theirs.
                        ours.keptYears = d.keptYears;
                        ours.weight = Math.Max(ours.weight, d.weight);
                        ours.breaks = Math.Min(ours.breaks, d.breaks);
                        ours.knew = ours.knew.Union(ids).ToList();
                        joined.Add(ours.label);
                        break;
                }
            }
            return joined.Where(l => !l.NullOrEmpty()).Distinct().ToList();
        }

        /// <summary>A day of another colony's calendar, as this one writes it down: every field, its lists its own.</summary>
        private static CalendarDay Copy(CalendarDay d) => new CalendarDay
        {
            id = d.id, kind = d.kind, way = d.way, label = d.label, day = d.day, sinceYear = d.sinceYear, tick = d.tick, pawnId = d.pawnId, pawnName = d.pawnName,
            otherId = d.otherId, otherName = d.otherName, subject = d.subject, about = d.about, things = d.things.ToList(), place = IntVec3.Invalid, enemy = d.enemy,
            kills = d.kills, fallen = d.fallen.ToList(), knew = d.knew.ToList(), kin = d.kin.ToList(), weight = d.weight, byPlayer = false, lastYear = d.lastYear,
            keptYears = d.keptYears, timesKept = d.timesKept, tradition = d.tradition, breaks = d.breaks, lastKeptYear = d.lastKeptYear, moment = d.moment
        };

        /// <summary>
        /// Their old colony's founding day on this colony's calendar, its own date, known by the newcomers: a remembrance while
        /// some of them live here (the calendar's letting go, ManyColoniesMath.KeepRemembrance). True when it was written down.
        /// </summary>
        public static bool Notify_Merged(Map home, string oldColony, int foundedTick, List<Pawn> newcomers)
        {
            var self = For(home);
            var map = self?.HomeMap;
            if (map == null || oldColony.NullOrEmpty() || newcomers.NullOrEmpty()) return false;
            string id = "oldfounding|" + oldColony;
            if (self.Get(id) != null) return true;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var d = Dated(new CalendarDay
            {
                id = id, kind = CalendarKind.Parting, way = DayWay.Remembrance, subject = oldColony, knew = newcomers.Where(p => p != null).Select(p => p.ThingID).ToList(),
                weight = PartingWeight, label = "CC_Cal_Name_OldFounding".Translate(oldColony.Named("PLACE")).Resolve()
            }, Math.Max(0, foundedTick), map, today, year, false);
            return self.TryAdd(d, false) != null;
        }

        /// <summary>An old colony's founding day kept here stays while some of its people live here (one who knew it, still of this colony).</summary>
        private bool KeepsOldFounding(CalendarDay d, Dictionary<string, Pawn> known)
        {
            int here = 0;
            foreach (var k in d.knew)
                if (Living(k, known) is Pawn p && Belongs(p))
                    here++;
            return ManyColoniesMath.KeepRemembrance(here);
        }
    }
}
