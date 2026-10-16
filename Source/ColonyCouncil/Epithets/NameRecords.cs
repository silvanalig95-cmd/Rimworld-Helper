using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The records behind the names that come from offices, faith and the colony's years (Defs/Misc/Rimfolk_Records_Names.xml,
    /// Defs/EpithetDefs/Rimfolk_Epithets_Years.xml): days in the physician's and the marshal's seats and as the faith's
    /// moral guide, terms won as speaker and votes lost, rituals led, annals written. Also who founded the colony and who
    /// was the first child born in it, kept with the names (<see cref="EpithetTracker"/>), for the names that ask for that.
    /// </summary>
    public static class NameRecords
    {
        public const string DaysAsPhysician = "Council_DaysAsPhysician";
        public const string DaysAsMarshal = "Council_DaysAsMarshal";
        public const string DaysAsGuide = "Council_DaysAsGuide";
        public const string SpeakerTerms = "Council_SpeakerTerms";
        public const string VotesLost = "Council_VotesLost";
        public const string RitualsLed = "Council_RitualsLed";
        public const string AnnalsWritten = "Council_AnnalsWritten";

        /// <summary>The faith's moral guide (Ideology), who holds the role now.</summary>
        public static bool IsMoralGuide(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p?.Ideo == null || PreceptDefOf.IdeoRole_Moralist == null) return false;
            try
            {
                return p.Ideo.GetRole(p)?.def == PreceptDefOf.IdeoRole_Moralist;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names: moral guide", e);
                return false;
            }
        }

        /// <summary>Once a day, before the names are given: a day more in the seats and roles, and who founded the colony.</summary>
        public static void Daily(EpithetTracker t, List<Pawn> colonists)
        {
            if (t == null || colonists == null) return;
            var comp = CouncilGameComponent.Instance;
            if (comp != null && ColonyCouncilMod.Settings.offices)
            {
                foreach (var p in comp.offices.HoldersOf("Council_Office_Physician")) CouncilRecords.Add(p, DaysAsPhysician);
                foreach (var p in comp.offices.HoldersOf("Council_Office_Marshal")) CouncilRecords.Add(p, DaysAsMarshal);
            }
            foreach (var p in colonists)
                if (IsMoralGuide(p)) CouncilRecords.Add(p, DaysAsGuide);
            t.NoteFounders(colonists);
        }

        // ---------------- Elections ----------------

        /// <summary>
        /// An election is over (OfficeTracker.Hold): a term won as speaker for the winner, by the colony's vote (a ruler
        /// naming officers isn't a vote), and a vote lost for everyone who stood and lost, or was voted out of their seat.
        /// </summary>
        public static void Notify_Election(OfficeDef office, Pawn winner, IEnumerable<Pawn> lost, bool byRuler)
        {
            if (byRuler || office == null) return;
            try
            {
                if (winner != null && office.defName == "Council_Office_Speaker") CouncilRecords.Add(winner, SpeakerTerms);
                if (lost == null) return;
                foreach (var p in lost.Distinct())
                    if (p != null && p != winner) CouncilRecords.Add(p, VotesLost);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names: election", e);
            }
        }

        // ---------------- Rituals ----------------

        /// <summary>Rituals already counted (a ritual's outcome may be applied by more than one override).</summary>
        private static readonly List<LordJob> counted = new List<LordJob>();

        /// <summary>
        /// A ritual of the colony's faith was held to its end (Ideology): one more for whoever led it, and the colony's
        /// tally of rituals kept (a devout colony, Character/).
        /// </summary>
        public static void Notify_RitualKept(LordJob_Ritual job, bool cancelled)
        {
            if (cancelled || job == null || !ModsConfig.IdeologyActive || counted.Contains(job)) return;
            counted.Add(job);
            if (counted.Count > 20) counted.RemoveAt(0);
            var ideo = job.Ritual?.ideo;
            if (ideo == null || Faction.OfPlayerSilentFail?.ideos == null || !Faction.OfPlayer.ideos.Has(ideo)) return;
            var leader = job.Organizer ?? job.assignments?.FirstAssignedPawn("organizer");
            if (leader != null && leader.IsColonist) CouncilRecords.Add(leader, RitualsLed);
            ColonyTraits.Tally("ritual");
        }
    }

    /// <summary>
    /// A ritual's outcome was applied: counted for the names and the colony's character (NameRecords). Only counts; the
    /// ritual and its outcome are the game's own. The speech's own override is hooked too, and each ritual counts once.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RitualKept
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var types = new[] { typeof(LordJob_Ritual), typeof(LordJob_Joinable_Speech) };
            foreach (var t in types)
            {
                var m = AccessTools.DeclaredMethod(t, nameof(LordJob_Ritual.ApplyOutcome));
                if (m != null) yield return m;
            }
        }

        public static void Postfix(LordJob_Ritual __instance, bool cancelled)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                NameRecords.Notify_RitualKept(__instance, cancelled);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ritual hook", e);
            }
        }
    }
}
