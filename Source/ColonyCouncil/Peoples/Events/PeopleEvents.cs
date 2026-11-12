using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The peoples' own events, round 1 (docs/peoples-events-proposals.md, approved in full by the player on 11 November 2026:
    // "I'd first like more faction specific events, interactions etc. like this event i had with these gene guys who gave my
    // pawn a new gene"; the defs in Defs/IncidentDefs/Rimfolk_Events_PeopleEvents.xml and
    // Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents.xml). Each comes only from a people of its calling at peace with the
    // colony, takes the form of the colony's age and theirs, and plays out on the map. Their shared shapes, for the later
    // rounds: the visitor who works on a colonist (PeopleWorkOn.cs), goods held in trust (PeopleTrust.cs), a reading that
    // says exactly what the map holds (OldPlaces.cs), the legend beast on the land (LegendBeasts, here), a camp kind with
    // ways of its own (Camps/CampWorker.cs). What waits on the map waits as one of the events' visits, kinds "pev:...".

    /// <summary>The peoples' events' own visits, by kind, and what they share.</summary>
    public static class PeopleEvents
    {
        public const string Prefix = "pev:";

        public static bool Owns(string kind) => kind != null && kind.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>An hour of one of their visits (HappeningTracker.VisitsHour): false when it is over.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case PeopleWorkOn.Kind: return PeopleWorkOn.Hour(v, comp, now);
                case PeopleTrust.VisitKind: return PeopleTrust.Hour(v, now);
                case HerdersBeast.SupperKind: return HerdersBeast.SupperHour(v, now);
                case HuntOrTame.TameKind: return HuntOrTame.TameHour(v, now);
                case HuntOrTame.CoachKind: return HuntOrTame.CoachHour(v, now);
            }
            // Round 2's (Peoples/Events2/): a season away, a crossing laid and its knot night, the enclave's terms, an archive's fetch, the road's news.
            return PeopleEvents2.Hour(v, comp, now);
        }

        /// <summary>The people's age (0 tribal to 3 spacer), for the tags "age0" to "age3" in their lines.</summary>
        public static int AgeOf(Faction f) => f == null ? 1 : Peoples.AgeOf(f);

        /// <summary>A gift of this kind could come to this colonist now: never had it, the gap since the last gift past.</summary>
        public static bool MayGive(string kind, Pawn p) => p != null && !PeopleEventsBook.Given(kind, p) && PeopleEventsBook.GiftOpen;

        /// <summary>The colony's grown people up and about who could have a gift of this kind.</summary>
        public static IEnumerable<Pawn> Takers(Map map, string kind) => PeoplesG.Grown(map).Where(p => MayGive(kind, p));

        /// <summary>Tags for their lines: the people's age and the colony's.</summary>
        public static void AgeTags(HashSet<string> tags, Faction f)
        {
            tags.Add("age" + AgeOf(f));
        }
    }

    /// <summary>
    /// The legend beast trigger (round 1's shared shape, the Lantern Wardens' post and the Wardens against the Circuit): a
    /// great beast on the colony's land, wild and up and seen, the biggest story first: a legend of the region (the great
    /// beast's mark), a beast nemesis, a pack's alpha (Beasts/). The Wardens follow all three; the Circuit would win over
    /// a legend or an alpha, never a beast that took a person.
    /// </summary>
    public static class LegendBeasts
    {
        private static HediffDef legendDef;

        /// <summary>The great beast on the map, or null; <paramref name="kind"/> "legend", "nemesis" or "alpha".</summary>
        public static Pawn OnLand(Map map, out string kind, bool nemeses = true)
        {
            kind = null;
            if (map == null) return null;
            legendDef ??= DefDatabase<HediffDef>.GetNamedSilentFail("Council_Legendary");
            var book = CouncilGameComponent.Instance?.nemeses;
            var alphas = BeastBook.Instance?.alphas;
            Pawn best = null;
            int rank = 0;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || p.Downed || p.Faction != null || p.RaceProps?.Animal != true || p.Position.Fogged(map)) continue;
                bool legend = legendDef != null && p.health?.hediffSet?.HasHediff(legendDef) == true;
                bool nemesis = nemeses && book != null && book.IsNemesis(p);
                bool alpha = alphas != null && alphas.Any(a => a != null && a.Open && a.state == "wild" && a.alpha == p);
                int r = PeoplesDMath.QuarryRank(legend, nemesis, alpha);
                if (r <= rank) continue;
                rank = r;
                best = p;
                kind = legend ? "legend" : nemesis ? "nemesis" : "alpha";
            }
            return best;
        }

        /// <summary>Still the beast it was: alive, up, on this map.</summary>
        public static bool Here(Pawn beast, Map map) => beast != null && !beast.Dead && !beast.Destroyed && beast.Spawned && beast.Map == map;

        /// <summary>The game would let it be tamed at all (not a dryad, not wild past taming, no scaria), and it isn't hunting anyone now.</summary>
        public static bool Tameable(Pawn beast) => beast != null && TameUtility.CanTame(beast) && !PackAlphas.IsManhunter(beast.MentalStateDef);

        /// <summary>The cell at the edge of the map nearest the beast (where the Wardens make their post).</summary>
        public static IntVec3 EdgeToward(Map map, IntVec3 at)
        {
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            int x = at.x, z = at.z, w = map.Size.x - 1, h = map.Size.z - 1;
            int dl = x, dr = w - x, db = z, dt = h - z;
            int m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
            if (m == dl) return new IntVec3(6, 0, z);
            if (m == dr) return new IntVec3(w - 6, 0, z);
            if (m == db) return new IntVec3(x, 0, 6);
            return new IntVec3(x, 0, h - 6);
        }
    }
}
