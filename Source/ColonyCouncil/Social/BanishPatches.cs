using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A social fight starting, noted for banishment by the council (Social/Banishment.cs): the one whose tracker starts it
    /// threw the first punch. Noted whatever the settings, so the trouble is known when the switch is turned on.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.StartSocialFight))]
    public static class Patch_BanishFightNoted
    {
        public static void Postfix(Pawn ___pawn, Pawn otherPawn)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                BanishTracker.Notify_SocialFight(___pawn, otherPawn);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("banishment: a fight noted", e);
            }
        }
    }

    /// <summary>A raid has just arrived (its pawns spawned, its letter still to come): one sent away with a grudge may ride with it.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_BanishRaidJoin
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns)
        {
            if (!RimfolkPatches.On || Raids.Sending) return;
            try
            {
                BanishTracker.Instance?.JoinArrivingRaid(parms, pawns);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("banishment: a raid arrives", e);
            }
        }
    }

    /// <summary>The raid's letter names the one the council sent away, riding with it.</summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "GetLetterText")]
    public static class Patch_BanishRaidLetter
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns, ref string __result)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                string note = BanishTracker.RaidNote(parms, pawns);
                if (!note.NullOrEmpty()) __result += "\n\n" + note;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("banishment: the raid's letter", e);
            }
        }
    }
}
