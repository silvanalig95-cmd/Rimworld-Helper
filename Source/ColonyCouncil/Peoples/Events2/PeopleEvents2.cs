using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The peoples' own events, round 2 (docs/peoples-events-proposals.md, items 14, 22, 26, 59, 12, 25, 3, 20, 40 and 63;
    // the defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents2.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents2.xml).
    // Each comes only from a people of its calling at peace with the colony, takes the form of the colony's age and theirs,
    // and plays out on the map, built on round 1's shapes (Peoples/Events/): the visitor who works on a colonist (the
    // enclave's bionic, the beast-doctor's leg, the guild's rework at the bench), goods held in trust (the society's books),
    // a camp kind with ways of its own (the herders' water, the enclave's pad). Round 2 adds the season away (SeasonAway.cs):
    // a colonist who leaves as a caravan of their own, stays a while somewhere on the world and comes home, for the
    // pilgrimage and the league's guard, and later the pit's cave-in and the plague town. Their visits are the events' own
    // ("pev:..."), dispatched from round 1's hour (PeopleEvents.Hour).

    /// <summary>Round 2's visits, by kind.</summary>
    public static class PeopleEvents2
    {
        public static int Now => Find.TickManager.TicksGame;

        /// <summary>An hour of one of round 2's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case SeasonAway.Kind: return SeasonAway.Hour(v, now);
                case RiverCrossing.LayKind: return RiverCrossing.LayHour(v, now);
                case RiverCrossing.KnotKind: return RiverCrossing.KnotHour(v, now);
                case EnclaveTerms.Kind: return EnclaveTerms.Hour(v, now);
                case ArchiveTrust.Kind: return ArchiveTrust.Hour(v, now);
                case DryWater.LostKind: return DryWater.LostHour(v, now);
            }
            // Round 3's (Peoples/Events3/): the litters, the sick room, the ward, a drove, the thieves' trail, a birth, a shrine.
            return PeopleEvents3.Hour(v, comp, now);
        }

        /// <summary>One of round 2's events fired from code when its day comes (an instalment due, the knot night): false when it can't be now.</summary>
        public static bool Fire(string defName, Map map, Faction f)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def == null || map == null) return false;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                parms.faction = f;
                return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: firing " + defName, e);
                return false;
            }
        }

        /// <summary>The colony's age now (0 tribal to 3 spacer), for "a tribal colony sees pods fall" and the like.</summary>
        public static int ColonyAge => (int)Ages.Ours;

        /// <summary>A grown colonist who could go away for a while: up and able, not in a caravan, never one of the colony's last two grown-ups.</summary>
        public static bool MayLeave(Pawn p, Map map)
        {
            if (p == null || map == null || p.Dead || !p.Spawned || p.Map != map || p.Downed || p.Drafted || p.InMentalState || p.DevelopmentalStage.Juvenile()) return false;
            if (p.GetCaravan() != null || SeasonAway.AwayNow(p)) return false;
            return PeoplesD.Able(map).Count(x => !x.DevelopmentalStage.Juvenile()) > 2;
        }
    }
}
