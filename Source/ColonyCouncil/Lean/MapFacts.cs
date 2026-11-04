using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The facts about a map that many of Rimfolk's hourly passes ask for, worked out once and shared: whether a threat
    /// is on the map (every 250 ticks, like the heart always had it), and for the in-game hour the colonists at home,
    /// the days of food, the medicine and the colony's beds (and, through <see cref="Homes"/>, which map is the colony).
    /// Each fact is worked out by the first pass that asks for it in its stretch of time (see <see cref="Fresh"/>) and
    /// read by the others; nothing is worked out that nobody asks for. The heart's map context, decrees, the life checks behind dilemmas, autonomy,
    /// encounters, social events, nuisances, the calendar, Rimfolk's events, world situations, scenarios, story chains,
    /// nemeses, camps, the fights pass and the worn-out episode read from here.
    /// </summary>
    public sealed class MapFacts
    {
        /// <summary>The threat flag holds this long.</summary>
        public const int ThreatTicks = Sched.RareTicks;
        /// <summary>The other facts hold for the in-game hour.</summary>
        public const int HourTicks = Sched.HourTicks;

        public readonly Map map;

        private int threatAt = Fresh.Never;
        private bool threat;
        private int colonistsAt = Fresh.Never;
        private int colonists;
        private int foodAt = Fresh.Never;
        private float foodDays = 999f;
        private int medicineAt = Fresh.Never;
        private int medicine;
        private int bedsAt = Fresh.Never;
        private readonly List<Building_Bed> beds = new List<Building_Bed>();

        private MapFacts(Map map)
        {
            this.map = map;
        }

        private static readonly Dictionary<int, MapFacts> facts = new Dictionary<int, MapFacts>();
        private static Game game;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The facts kept for a map (null for no map).</summary>
        public static MapFacts Of(Map map)
        {
            if (map == null) return null;
            CheckGame();
            if (!facts.TryGetValue(map.uniqueID, out var f) || f.map != map)
            {
                // Maps come and go (a site, a raid on an outpost): those that are gone are let go.
                if (facts.Count > 8) facts.Clear();
                f = new MapFacts(map);
                facts[map.uniqueID] = f;
            }
            return f;
        }

        /// <summary>A new game or a load: the last game's facts are let go.</summary>
        private static void CheckGame()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            Clear();
        }

        /// <summary>Forget everything (a new game, a load).</summary>
        public static void Clear()
        {
            facts.Clear();
            Homes.Clear();
        }

        // ---------------- Every 250 ticks ----------------

        /// <summary>A hostile threat is active on the map (the game's own test), as of this 250-tick stretch.</summary>
        public bool Threat
        {
            get
            {
                int now = Now;
                if (!Fresh.Holds(threatAt, now, ThreatTicks))
                {
                    threatAt = now;
                    try
                    {
                        threat = GenHostility.AnyHostileActiveThreatToPlayer(map, false, false);
                    }
                    catch
                    {
                        threat = false;
                    }
                }
                return threat;
            }
        }

        /// <summary><see cref="Threat"/> for a map; false for no map.</summary>
        public static bool ThreatOn(Map map) => map != null && Of(map).Threat;

        // ---------------- Every hour ----------------

        /// <summary>Free colonists on the map, as of this hour.</summary>
        public int Colonists
        {
            get
            {
                int now = Now;
                if (!Fresh.Holds(colonistsAt, now, HourTicks))
                {
                    colonistsAt = now;
                    colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0;
                }
                return colonists;
            }
        }

        /// <summary>Days the food lasts (the advisor's count), as of this hour.</summary>
        public float FoodDays
        {
            get
            {
                int now = Now;
                if (!Fresh.Holds(foodAt, now, HourTicks))
                {
                    foodAt = now;
                    try
                    {
                        foodDays = AdvisorWorker_Food.DaysOfFood(map);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("map facts: food", e);
                        foodDays = 999f;
                    }
                }
                return foodDays;
            }
        }

        /// <summary>Medicine on the map and carried by the colonists, as of this hour.</summary>
        public int Medicine
        {
            get
            {
                int now = Now;
                if (!Fresh.Holds(medicineAt, now, HourTicks))
                {
                    medicineAt = now;
                    try
                    {
                        medicine = Avail.MedicineCount(map);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("map facts: medicine", e);
                        medicine = 0;
                    }
                }
                return medicine;
            }
        }

        /// <summary>
        /// The colony's beds on the map, as of this hour (from the game's list of beds, not every building). Read only;
        /// a bed may have gone since, so check it's still spawned.
        /// </summary>
        public List<Building_Bed> Beds
        {
            get
            {
                int now = Now;
                if (!Fresh.Holds(bedsAt, now, HourTicks))
                {
                    bedsAt = now;
                    beds.Clear();
                    var group = map.listerThings?.ThingsInGroup(ThingRequestGroup.Bed);
                    if (group != null)
                        for (int i = 0; i < group.Count; i++)
                            if (group[i] is Building_Bed b && b.Faction == Faction.OfPlayer)
                                beds.Add(b);
                }
                return beds;
            }
        }

        // ---------------- The colony ----------------

        /// <summary>
        /// The colony: the seat (<see cref="Homes.Seat"/>), the first home founded unless a seat is stored. It used to be
        /// the home with the most free colonists on it, which moved as people did; the name stays so its callers read as
        /// before, and with one colony it is the same map. New code asks <see cref="Homes"/> (the seat, a loop over the
        /// homes, or <see cref="Homes.Busiest"/> where most people are right now).
        /// </summary>
        public static Map BusiestHome => Homes.Seat;

        /// <summary>In space (Odyssey's orbit): nobody walks in there, and anyone set down outside dies.</summary>
        public static bool InVacuum(Map map) => map?.Biome != null && map.Biome.inVacuum;
    }
}
