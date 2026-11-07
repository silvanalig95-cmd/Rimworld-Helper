using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town the colony was keeps the colony's founding day and the day of the hand-over (Colonies/TownsAfter.cs, release
    /// 2b), and invites the colony to one of them a year at most, by the invitation missions (Workers_MissionRemembered.cs):
    /// the same journey to their town, the same stay at the table, the same gift and memory, its words its own
    /// (Defs/MissionDefs/Rimfolk_Missions_TownDay.xml, the variants founding and handover). Never drawn by the storyteller
    /// (its base chance is 0): only the town's day sends it, for that town's people alone.
    /// </summary>
    public static class TownDayMission
    {
        public const string DefName = "Rimfolk_Mission_TownDay";

        /// <summary>While set, the town and the day an invitation is being made for (set and cleared around one firing; not saved).</summary>
        [ThreadStatic] internal static LedgerFormer making;
        [ThreadStatic] internal static TownDay day;

        /// <summary>The town invites the colony to its day: the mission offered for its people alone. False when it can't be now.</summary>
        public static bool Invite(LedgerFormer rec, TownDay which, Map home)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            if (def == null || rec?.people == null || home == null || which == TownDay.None) return false;
            MissionPeoples.Only = rec.people;
            making = rec;
            day = which;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, home);
                parms.forced = true;
                parms.faction = rec.people;
                return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: its day's invitation", e);
                return false;
            }
            finally
            {
                MissionPeoples.Only = null;
                making = null;
                day = TownDay.None;
            }
        }
    }

    /// <summary>The invitation to a town the colony was's day: the invitation's own journey and stay, for its founding day or the day of the hand-over.</summary>
    public class HappeningWorker_MissionTownDay : HappeningWorker_MissionInvitation
    {
        protected override string Kind => "townday";

        protected override float RestDays => 50f;

        /// <summary>The town may lie far off (the colony moved on): its day is worth a longer road.</summary>
        protected override float MaxTravelDays => 10f;

        protected override bool Possible(Map map) => TownDayMission.making != null && base.Possible(map);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var rec = TownDayMission.making;
            if (rec == null || !base.Setup(m, ctx, ext) || m.faction != rec.people) return false;
            m.variant = TownDayMission.day == TownDay.Founding ? "founding" : "handover";
            // Whose day it is: the colony it was ({DEAD}); the town's head as the ledger knows them gives it ({HOST}).
            m.name = rec.colonyName ?? m.place;
            m.rite = null;
            m.riteOfFaith = false;
            Fill(m, ctx);
            return true;
        }
    }
}
