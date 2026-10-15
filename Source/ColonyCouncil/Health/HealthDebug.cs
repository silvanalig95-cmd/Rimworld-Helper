using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode (Debug actions, "Rimfolk"): start a convalescence and give a doctor a patient pulled through, to test health stories.</summary>
    public static class HealthDebug
    {
        [DebugAction("Rimfolk", "Health: convalesce (after a long time in bed)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Convalesce(Pawn p)
        {
            var t = HealthTracker.Instance;
            if (t == null || !HealthTracker.ConvalescenceOn)
            {
                Messages.Message("Convalescence is switched off.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            t.Convalesce(p, 0.5f, "Bed");
        }

        [DebugAction("Rimfolk", "Health: pull a patient through (as doctor)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PulledThrough(Pawn doctor)
        {
            var t = HealthTracker.Instance;
            if (t == null || doctor?.Map == null) return;
            var patient = doctor.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(o => o != doctor);
            if (patient == null) return;
            t.PulledThrough(doctor, patient, "flu", 0.8f, false);
            var rec = t.RecordOf(doctor);
            Messages.Message(doctor.LabelShort + ": pulled through " + (rec?.Saved ?? 0) + ", lost " + (rec?.lost ?? 0) + ", level " + (rec?.Level ?? 0) + ".",
                doctor, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
