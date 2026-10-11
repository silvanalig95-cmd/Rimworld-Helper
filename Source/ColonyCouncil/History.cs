using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One day in the colony's life, in numbers (for the overview's graphs).</summary>
    public class DaySample : IExposable
    {
        public int day;
        /// <summary>Average mood, 0..1.</summary>
        public float mood;
        /// <summary>Average trust in the council, -100..100.</summary>
        public float trust;
        public float wealth;
        public int colonists;
        public int wishes;

        public void ExposeData()
        {
            Scribe_Values.Look(ref day, "day");
            Scribe_Values.Look(ref mood, "mood");
            Scribe_Values.Look(ref trust, "trust");
            Scribe_Values.Look(ref wealth, "wealth");
            Scribe_Values.Look(ref colonists, "colonists");
            Scribe_Values.Look(ref wishes, "wishes");
        }
    }

    public partial class CouncilGameComponent
    {
        public const int HistoryDays = 120;

        public List<DaySample> history = new List<DaySample>();

        /// <summary>Takes today's numbers (once a day).</summary>
        private void RecordDay()
        {
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && p.needs?.mood != null).Distinct().ToList();
            if (pawns.Count == 0) return;
            int day = GenDate.DaysPassed;
            history.RemoveAll(h => h.day == day);
            history.Add(new DaySample
            {
                day = day,
                mood = pawns.Average(p => p.needs.mood.CurLevelPercentage),
                trust = pawns.Average(p => standings.TrustOf(p)),
                wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal),
                colonists = pawns.Count,
                wishes = OpenWishes.Count()
            });
            while (history.Count > HistoryDays) history.RemoveAt(0);
        }
    }
}
