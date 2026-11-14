using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The peoples' own events, round 3 (docs/peoples-events-proposals.md, items 2, 4, 6, 7, 8, 9, 11, 13, 15 and 16; the defs
    // in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents3.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents3.xml): the road
    // league's red ink and its road gang, the hospitaller order's litters after a raid, its sick room and its field sickhouse,
    // the herding folk's drove across the land, their riders on a thief's trail and their spring calving, the pilgrim
    // fellowship's old shrine and its palmer at a table of two faiths. Each comes only from a people of its calling at peace
    // with the colony, takes the form of the colony's age and theirs, and plays out on the map, built on rounds 1 and 2's
    // shapes: the visitor at the gate, the camp kind with ways of its own, the caravan of one, the events' visits ("pev:...").

    /// <summary>Round 3's visits, by kind, and its watch on the homes (a raid's end, a theft, the shrines' seasons).</summary>
    public static class PeopleEvents3
    {
        /// <summary>An hour of one of round 3's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case Litters.Kind: return Litters.Hour(v, now);
                case SickRoom.Kind: return SickRoom.Hour(v, now);
                case FieldSickhouse.WardKind: return FieldSickhouse.WardHour(v, now);
                case FieldSickhouse.BellKind: return FieldSickhouse.BellHour(v, now);
                case Drove.Kind: return Drove.Hour(v, now);
                case ThiefTrail.AloneKind: return ThiefTrail.AloneHour(v, now);
                case ThiefTrail.RideKind: return ThiefTrail.RideHour(v, now);
                case SpringCalving.BirthKind: return SpringCalving.BirthHour(v, now);
                case OldShrine.WorkKind: return OldShrine.WorkHour(v, now);
                case OldShrine.StopKind: return OldShrine.StopHour(v, now);
                case TwoFaiths.BlessKind: return TwoFaiths.BlessHour(v, now);
                case RoadGang.HandKind: return RoadGang.HandHour(v, now);
            }
            return false;
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly): raiders lying downed after a fight (the order's litters), raiders
        /// driving off the colony's beasts and the herders' riders on their trail, the old shrines in their seasons, the
        /// league's pages put right some other way.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the litters' watch", () => Litters.Watch(map, now));
                Safe("the thieves", () => ThiefTrail.Watch(map, now));
                Safe("the old shrines", () => OldShrine.Watch(map, now));
            }
            Safe("the thieves' trail", () => ThiefTrail.Hourly(now));
            Safe("the palmer's table", TwoFaiths.Tidy);
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Safe("the league's pages", LeagueRedInk.Tidy);
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
            Litters.Forget();
            ThiefTrail.Forget();
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: " + what, e);
            }
        }
    }
}
