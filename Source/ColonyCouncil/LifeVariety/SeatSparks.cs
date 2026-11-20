using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Life goals for the other seats (A16 of docs/content-update-proposals.md, the player's go of 20 November 2026): the
    /// reeve's, the peacemaker's, the keeper of days', the herdmaster's, the chaplain's, the warden of guests' and an elder's
    /// seat (Defs/AspirationDefs/Rimfolk_Aspirations_Seats.xml), beside the marshal's, physician's, steward's and
    /// quartermaster's that were there. Each is sparked by doing the seat's own work (a big harvest in, a quarrel mended, a
    /// day kept, an animal tamed, a rite led, a camp greeted, a student taught to stand alone: SeatSparkMath) and by losing
    /// an election; a goal of a seat already counts in the vote (BondsMath.SeatWant). Once a day the records behind each
    /// are read for everyone at home (the game's own or Rimfolk's), and what grew since yesterday sets the spark; the
    /// seat's holder is never set on the seat they already hold. The day before isn't saved: after a load, the first day
    /// only looks.
    /// </summary>
    public static class SeatSparks
    {
        private static readonly Dictionary<string, float> yesterday = new Dictionary<string, float>();
        private static Game game;

        public static void Daily(CouncilGameComponent comp)
        {
            if (comp == null || !ColonyCouncilMod.Settings.aspirations || !ColonyCouncilMod.Settings.offices) return;
            if (game != Current.Game)
            {
                yesterday.Clear();
                game = Current.Game;
            }
            var seen = new HashSet<string>();
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                if (p.records == null || p.DevelopmentalStage.Juvenile() || p.IsQuestLodger() || !p.Map.IsPlayerHome) continue;
                foreach (var spark in SeatSparkMath.All)
                {
                    var def = DefDatabase<RecordDef>.GetNamedSilentFail(SeatSparkMath.RecordOf(spark));
                    if (def == null) continue;
                    string key = p.thingIDNumber + "|" + spark;
                    seen.Add(key);
                    float now = p.records.GetValue(def);
                    if (yesterday.TryGetValue(key, out float before) && SeatSparkMath.Sparks(spark, now - before) && !Interplay.Holds(p, SeatSparkMath.SeatOf(spark)))
                        comp.aspirations.Notify_Event(p, spark);
                    yesterday[key] = now;
                }
            }
            foreach (var k in yesterday.Keys.Where(k => !seen.Contains(k)).ToList()) yesterday.Remove(k);
        }
    }

    /// <summary>
    /// A seat on the council as a life goal, for a seat the colony may not have (the chaplain's needs the colony's own faith,
    /// the elder's and the warden's a colony large enough): only wanted while the seat is open to the colony.
    /// </summary>
    public class AspirationWorker_SeatOpen : AspirationWorker_Office
    {
        public override bool CanHave(Pawn p) => base.CanHave(p) && (def.office == null || def.office.Available);

        public override bool StillPossible(Aspiration a) => base.StillPossible(a) && (def.office == null || def.office.Available);
    }
}
