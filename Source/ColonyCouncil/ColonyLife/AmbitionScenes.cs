using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A14: an ambition reached, seen on the map and kept on the calendar (the content update, the player's go of 20 November
    /// 2026). Nine ambitions did something when reached (a feast, a toast, a games night); now every one does, by its
    /// <see cref="AmbitionDef.achievedEffects"/>, led by the one who proposed it or by the seat whose work it is
    /// (<see cref="AmbitionDef.sceneOffice"/>: the physician shows the colony round a new ward, the marshal walks the walls at
    /// dusk). What was built is stood by (<see cref="AmbitionGoal.Showpiece"/>: the new room's best piece, the newest of the
    /// things built), so the words name it as it stands. The day goes on the colony calendar as a day of its own kind
    /// (<see cref="CalendarKind.Ambition"/>, "the day the hall was finished"), kept by walking out to what was built (or a
    /// supper, when nothing was), toward a tradition as the other days are, and it ends quietly when what was built is gone
    /// from the map. The year read aloud names it (A29).
    /// </summary>
    public static class AmbitionScenes
    {
        /// <summary>The thing to stand by: the first goal's that has one.</summary>
        public static Thing Showpiece(AmbitionState a, Map map)
        {
            if (a?.def?.goals == null || map == null) return null;
            foreach (var g in a.def.goals)
            {
                try
                {
                    // Rooms of their own: the one who proposed it has everyone over to their own new room.
                    var t = g is Goal_AllOwnRooms ? a.proposer?.ownership?.OwnedBed : g.Showpiece(map);
                    if (t != null && t.Spawned && t.Map == map) return t;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ambition showpiece " + a.def.defName, e);
                }
            }
            return null;
        }

        /// <summary>A room's own piece: the thing its goal asks for, else its finest building (no walls, doors or lights).</summary>
        public static Thing PieceIn(Room room, Func<ThingDef, bool> pred)
        {
            if (room == null || room.PsychologicallyOutdoors) return null;
            Thing best = null;
            float bestValue = -1f;
            bool bestFits = false;
            foreach (var t in room.ContainedAndAdjacentThings)
            {
                if (!(t is Building) || t.def.building == null || !room.ContainsCell(t.Position)) continue;
                if (t.def.building.isWall || t.def.IsDoor) continue;
                if (t.def.category != ThingCategory.Building || t.def.altitudeLayer == AltitudeLayer.FloorEmplacement) continue;
                bool fits = pred != null && pred(t.def);
                float value = t.MarketValue;
                if (best == null || (fits && !bestFits) || (fits == bestFits && value > bestValue))
                {
                    best = t;
                    bestValue = value;
                    bestFits = fits;
                }
            }
            return best;
        }

        /// <summary>The newest of the colony's buildings that fit.</summary>
        public static Thing Newest(Map map, Func<ThingDef, bool> pred)
        {
            if (map == null || pred == null) return null;
            Thing best = null;
            foreach (var b in Avail.ColonyBuildings(map, pred))
                if (b != null && b.Spawned && (best == null || b.thingIDNumber > best.thingIDNumber)) best = b;
            return best;
        }

        /// <summary>
        /// Sets the scene going (with "Answers play out on the map"): A is the one who proposed it, B the holder of the
        /// ambition's seat when at home (else A), and the place is what was built. Returns a word on when it will be, for the
        /// letter, or null when nothing was set going.
        /// </summary>
        public static string Run(AmbitionState a, Map map, Thing piece)
        {
            if (a?.def == null || map == null || a.def.achievedEffects.NullOrEmpty() || !PlayOuts.On) return null;
            var proposer = a.proposer != null && !a.proposer.Dead && a.proposer.MapHeld == map ? a.proposer : null;
            var lead = Lead(a.def, map) ?? proposer;
            var ctx = new HappeningContext { map = map, a = proposer ?? lead, b = lead, place = piece, why = a.def.LabelCap };
            List<PlayOut> plays;
            using (PlayOuts.Collect())
            {
                foreach (var e in PlayOuts.Ordered(a.def.achievedEffects))
                {
                    try
                    {
                        e.Apply(ctx);
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce("ambition " + a.def.defName + " effect " + e.GetType().Name, ex);
                    }
                }
                plays = PlayOuts.Collected();
            }
            var first = plays.FirstOrDefault(p => p != null && p.state != PlayState.Done);
            return first == null ? null : PlayWords.Planned(first);
        }

        /// <summary>The holder of the ambition's seat, at home and up to it; null when there is none.</summary>
        private static Pawn Lead(AmbitionDef def, Map map)
        {
            if (def.sceneOffice.NullOrEmpty() || !(ColonyCouncilMod.Settings?.offices ?? false)) return null;
            var p = OfficeTracker.For(map)?.HolderOf(def.sceneOffice);
            return p != null && !p.Dead && p.MapHeld == map && PlayOuts.OurPerson(p) ? p : null;
        }

        /// <summary>The day on the calendar, and the year's reading.</summary>
        public static void Remember(AmbitionState a, Map map, Thing piece, float effort, float days)
        {
            if (a?.def == null || map == null) return;
            LifeBook.Instance?.NoteBuilt(a.def.label);
            LifeBook.Safe("ambition's day", () => ColonyCalendar.Notify_AmbitionReached(a.def, a.proposer, map, piece, effort, days));
        }
    }

    public partial class ColonyCalendar
    {
        /// <summary>
        /// An ambition reached (A14): its day goes on the calendar of the colony that reached it, kept by walking out to what
        /// was built (a supper, when nothing was), and the one who proposed it writes it down.
        /// </summary>
        public static void Notify_AmbitionReached(AmbitionDef def, Pawn proposer, Map map, Thing piece, float effort, float days)
        {
            var self = map != null && Homes.IsHome(map) ? For(map) : null;
            if (self == null || def == null) return;
            int now = Find.TickManager.TicksGame;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            var home = AtHome(map);
            bool thing = piece != null && piece.Spawned && piece.Map == map;
            var d = Dated(new CalendarDay
            {
                id = "ambition|" + def.defName + "|" + now, kind = CalendarKind.Ambition, way = thing ? DayWay.Walk : DayWay.Feast, about = def.defName,
                subject = def.label, pawnId = proposer?.ThingID, pawnName = proposer?.LabelShort, kills = Mathf.RoundToInt(days),
                knew = home.Select(p => p.ThingID).ToList(), things = thing ? new List<string> { piece.ThingID } : new List<string>(),
                place = thing ? piece.Position : IntVec3.Invalid, weight = AmbitionDayMath.Weight(effort, days)
            }, now, map, today, year, true);
            if (!def.dayLabel.NullOrEmpty()) d.label = def.dayLabel;
            if (self.TryAdd(d, true) != null && proposer != null && !proposer.Dead && home.Contains(proposer)) self.JournalNew(proposer, "Council_Journal_Cal_Ambition", d);
        }
    }
}
