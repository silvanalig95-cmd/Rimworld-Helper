using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// So many more of something the game counts for each colonist (a RecordDef by name): animals hunted, plants
    /// harvested, meals cooked, things built, or Rimfolk's own count of trades made. Only the colony's people count, and
    /// only what they do while the project runs: a newcomer's past doesn't count, and neither does anything done before.
    /// The count so far is the goal's baseline (saved with the project); each colonist's figure an hour ago is kept here
    /// (not saved: after a load, the first hour only looks).
    /// </summary>
    public class Goal_Record : AmbitionGoal
    {
        public string record;
        public int count = 10;
        /// <summary>Only when someone at home does this work (a WorkTypeDef name: Hunting, Cooking...).</summary>
        public string needsWork;
        /// <summary>Only with wild game on the map, at least half as many head as the count.</summary>
        public bool needsGame;

        private readonly Dictionary<int, int> last = new Dictionary<int, int>();

        private RecordDef Def => record.NullOrEmpty() ? null : DefDatabase<RecordDef>.GetNamedSilentFail(record);

        public override bool Possible(Map map)
        {
            if (Def == null) return false;
            if (!needsWork.NullOrEmpty())
            {
                var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(needsWork);
                if (work == null || !map.mapPawns.FreeColonistsSpawned.Any(p => !p.DevelopmentalStage.Juvenile() && p.workSettings != null && p.workSettings.WorkIsActive(work)))
                    return false;
            }
            if (needsGame && map.mapPawns.AllPawnsSpawned.Count(a => a.RaceProps.Animal && a.Faction == null && !a.RaceProps.predator && !a.Position.Fogged(map)) < Mathf.Max(1, count / 2))
                return false;
            return true;
        }

        public override float Baseline(Map map)
        {
            last.Clear();
            Look(map, false);
            return 0f;
        }

        /// <summary>Each colonist's figure now; with <paramref name="add"/>, what they did since the last look.</summary>
        private float Look(Map map, bool add)
        {
            var def = Def;
            if (def == null) return 0f;
            float gained = 0f;
            var seen = new HashSet<int>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
            {
                if (p.records == null || p.IsQuestLodger()) continue;
                int n = p.records.GetAsInt(def);
                if (add && last.TryGetValue(p.thingIDNumber, out int before) && n > before) gained += n - before;
                last[p.thingIDNumber] = n;
                seen.Add(p.thingIDNumber);
            }
            foreach (var k in last.Keys.Where(k => !seen.Contains(k)).ToList()) last.Remove(k);
            return gained;
        }

        public override float Update(Map map, float baseline) => baseline + Look(map, true);

        public override float Progress(Map map, float baseline) => Ratio(baseline, count);

        public override string Describe(Map map, float baseline)
        {
            string key = "CC_Goal_Record_" + record;
            string what = key.CanTranslate() ? key.Translate().Resolve() : Def?.label ?? record;
            return "CC_Goal_Record".Translate(count, what, Mathf.FloorToInt(baseline));
        }
    }

    /// <summary>
    /// So many days in a row with at least this much in store: a season of hot meals, a season of plenty. The kinds are
    /// Goal_Stock's (meals, finemeals, medicine, firewood, silver, alcohol, a thing by defName) and "fooddays" (days of
    /// food for everyone). The baseline is when the streak began; falling short starts it again.
    /// </summary>
    public class Goal_StockStreak : AmbitionGoal
    {
        public string kind = "meals";
        public int count = 10;
        /// <summary>This many per colonist, when that comes to more than <see cref="count"/>.</summary>
        public float perColonist;
        public float days = 15f;

        private float Have(Map map) => kind == "fooddays" ? AdvisorWorker_Food.DaysOfFood(map) : Goal_Stock.Count(map, kind);

        private float Wanted(Map map) => kind == "fooddays" ? count : Mathf.Max(count, Mathf.CeilToInt(perColonist * RoomUtil.ColonistCount(map)));

        public override bool Possible(Map map)
        {
            if (kind == "fooddays") return true;
            return new Goal_Stock { kind = kind, count = count }.Possible(map);
        }

        public override float Baseline(Map map) => Find.TickManager.TicksGame;

        public override float Update(Map map, float baseline) => Have(map) < Wanted(map) ? Find.TickManager.TicksGame : baseline;

        private float Days(float baseline) => Mathf.Max(0f, (Find.TickManager.TicksGame - baseline) / GenDate.TicksPerDay);

        public override float Progress(Map map, float baseline) => Have(map) < Wanted(map) ? 0f : Ratio(Days(baseline), days);

        public override string Describe(Map map, float baseline)
        {
            string label = kind == "fooddays" ? "CC_Stock_fooddays".Translate().Resolve()
                : ("CC_Stock_" + kind).CanTranslate() ? ("CC_Stock_" + kind).Translate().Resolve() : DefDatabase<ThingDef>.GetNamedSilentFail(kind)?.label ?? kind;
            return "CC_Goal_StockStreak".Translate(days.ToString("0"), Mathf.CeilToInt(Wanted(map)), label, Days(baseline).ToString("0.#"), Mathf.FloorToInt(Have(map)));
        }
    }

    /// <summary>
    /// Keep so many of the colony's own days (on the calendar, not birthdays or arrivals) as they come round while the
    /// project runs. Only pitched when the calendar is on and enough days fall within the next forty.
    /// </summary>
    public class Goal_DaysKept : AmbitionGoal
    {
        public int count = 2;

        private static int Kept()
        {
            var cal = CalendarTracker.Instance;
            return cal == null ? 0 : cal.days.Where(d => d != null && !d.Personal).Sum(d => d.timesKept);
        }

        public override bool Possible(Map map)
        {
            var cal = CalendarTracker.Instance;
            if (cal == null || !CalendarTracker.On || map == null) return false;
            int today = GenLocalDate.DayOfYear(map);
            return cal.days.Count(d => d != null && !d.Personal && d.kind != CalendarKind.FirstSnow && CalendarMath.DaysUntil(today, d.day) >= 1 && CalendarMath.DaysUntil(today, d.day) <= 40) >= count;
        }

        public override float Baseline(Map map) => Kept();

        public override float Progress(Map map, float baseline) => Ratio(Kept() - baseline, count);

        public override string Describe(Map map, float baseline) => "CC_Goal_DaysKept".Translate(count, Mathf.Max(0, Kept() - (int)baseline));
    }

    /// <summary>Hospitality: so many guest beds in rooms indoors. Never pitched without Hospitality.</summary>
    public class Goal_GuestBeds : AmbitionGoal
    {
        public int count = 3;

        private static int Count(Map map) => Guests.Beds(map).Count(b => b.GetRoom() is Room r && !r.PsychologicallyOutdoors);

        public override bool Possible(Map map) =>
            People.ModActive(Guests.Mod) && Count(map) < count && Avail.BestBuildable(map, Avail.IsSingleBed, "Bed") != null;

        public override float Progress(Map map, float baseline) => Ratio(Count(map), count);

        public override string Describe(Map map, float baseline) => "CC_Goal_GuestBeds".Translate(count, Count(map));
    }

    /// <summary>Hospitality: a room with a guest bed in it at least this impressive. Never pitched without Hospitality.</summary>
    public class Goal_GuestRoom : AmbitionGoal
    {
        public float impressiveness = 50f;

        public override bool Possible(Map map)
        {
            if (!People.ModActive(Guests.Mod)) return false;
            float best = WishWorker_GuestRoom.BestRoom(map, out bool any);
            return !any || best < impressiveness;
        }

        public override float Progress(Map map, float baseline)
        {
            float best = WishWorker_GuestRoom.BestRoom(map, out bool any);
            return any ? Ratio(Mathf.Max(0f, best), impressiveness) : 0f;
        }

        public override string Describe(Map map, float baseline)
        {
            float best = WishWorker_GuestRoom.BestRoom(map, out bool any);
            return "CC_Goal_GuestRoom".Translate(impressiveness.ToString("0"), any ? best.ToString("0") : "-");
        }
    }
}
