using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The animals on the colony calendar (Animals/AnimalLife.cs tells it what happened): the first foal born to the
    // colony's herd, and a beloved animal's day, the day a bonded animal died that was long with the colony or fought for
    // its person, kept as a remembrance by the one who loved it.
    public partial class CalendarTracker
    {
        /// <summary>The first animal born to the colony's own herd: written down once, the day it happened.</summary>
        public static void Notify_FirstFoal(Pawn newborn, Pawn mother, Pawn keeper)
        {
            var self = Instance;
            if (self == null || newborn == null || self.Get("foal") != null) return;
            try
            {
                var map = Home;
                if (map == null) return;
                int today = GenLocalDate.DayOfYear(map);
                int year = GenLocalDate.Year(map);
                var d = Dated(new CalendarDay
                {
                    id = "foal", kind = CalendarKind.Foal, way = DefaultWay(CalendarKind.Foal), subject = AnimalFacts.KindLabel(mother ?? newborn),
                    pawnId = keeper?.ThingID, pawnName = keeper?.LabelShort, otherName = newborn.LabelShort, weight = AnimalMath.FoalWeight
                }, Find.TickManager.TicksGame, map, today, year, true);
                if (keeper != null) d.knew.Add(keeper.ThingID);
                if (self.TryAdd(d, true) != null && keeper != null) self.JournalNew(keeper, "Council_Journal_Cal_Foal", d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar first foal", e);
            }
        }

        /// <summary>
        /// A bonded animal has died (Strangers/Pets.cs): long with the colony (<see cref="AnimalMath.CompanionYears"/>) or
        /// killed fighting for its person, its day is kept by the one who loved it, and by whoever else was fond of it.
        /// </summary>
        public static void Notify_Companion(Pawn pet, Pawn person, float years, bool bonded, bool hero)
        {
            var self = Instance;
            if (self == null || pet == null || person == null || !AnimalMath.CompanionDay(years, bonded, hero)) return;
            if (self.Get("pet|" + pet.ThingID) != null) return;
            try
            {
                var map = Home;
                if (map == null) return;
                var knew = new List<string> { person.ThingID };
                // Others it was bonded to or followed, and the herdmaster who looked after it.
                foreach (var o in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                {
                    if (o == person || !People.HasInnerLife(o) || o.DevelopmentalStage.Baby()) continue;
                    if (AnimalFacts.Bonded(pet, o) || pet.playerSettings?.Master == o || AnimalFacts.Herdmaster == o) knew.Add(o.ThingID);
                }
                int today = GenLocalDate.DayOfYear(map);
                int year = GenLocalDate.Year(map);
                var d = Dated(new CalendarDay
                {
                    id = "pet|" + pet.ThingID, kind = CalendarKind.Companion, way = DayWay.Remembrance, pawnId = pet.ThingID, pawnName = pet.LabelShort,
                    otherId = person.ThingID, otherName = person.LabelShort, subject = AnimalFacts.KindLabel(pet), knew = knew,
                    kin = new List<string> { person.ThingID }, weight = AnimalMath.CompanionWeight(years, bonded, hero)
                }, Find.TickManager.TicksGame, map, today, year, true);
                // Personal enough not to be announced in the news: their own grief is the news.
                self.TryAdd(d, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar companion", e);
            }
        }
    }
}
