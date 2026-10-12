using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A raid has just arrived (its pawns spawned, its letter and plan still to come): sometimes the nemesis who got
    /// away walks in with it, so the game's own letter shows them and they join the attack.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_RaidArrives
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns)
        {
            if (!RimfolkPatches.On || Raids.Sending) return;
            try
            {
                var comp = CouncilGameComponent.Instance;
                var n = comp?.nemeses.JoinArrivingRaid(parms, pawns);
                if (n != null) comp.nemeses.Notify_LedRaid(comp, n);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("raid arrives hook", e);
            }
        }
    }

    /// <summary>The raid letter names the nemesis riding with it, or the colonist sworn to avenge someone on these people.</summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "GetLetterText")]
    public static class Patch_RaidLetter
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns, ref string __result)
        {
            try
            {
                string note = LetterNotes.Raid(parms, pawns);
                if (!note.NullOrEmpty()) __result += "\n\n" + note;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("raid letter hook", e);
            }
        }
    }

    /// <summary>A mood break's letter also says what Rimfolk knows was weighing on them (a lapsed wish, a hated decree...).</summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_BreakLetter
    {
        public static void Prefix(Pawn ___pawn, ref string reason, bool causedByMood, bool transitionSilently)
        {
            if (!causedByMood || transitionSilently || ___pawn == null || ___pawn.InMentalState) return;
            try
            {
                string note = LetterNotes.Break(___pawn);
                if (!note.NullOrEmpty()) reason = reason.NullOrEmpty() ? note : reason + "\n\n" + note;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("break letter hook", e);
            }
        }
    }

    /// <summary>While the game writes a colonist's death letter, remembers who it's about (see <see cref="Patch_DeathLetterText"/>).</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.NotifyPlayerOfKilled))]
    public static class Patch_DeathLetter
    {
        internal static Pawn dying;

        public static void Prefix(Pawn ___pawn)
        {
            dying = RimfolkPatches.On ? ___pawn : null;
        }

        public static void Finalizer()
        {
            dying = null;
        }
    }

    /// <summary>The death letter ends with a line about who they were: years here, a goal never reached, a seat or a name.</summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_DeathLetterText
    {
        public static void Prefix(Letter let)
        {
            var p = Patch_DeathLetter.dying;
            if (p == null || let == null || let.def != LetterDefOf.Death || !(let is ChoiceLetter letter)) return;
            Patch_DeathLetter.dying = null;
            try
            {
                string note = LetterNotes.Death(p);
                if (!note.NullOrEmpty()) letter.Text = letter.Text + "\n\n" + note;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("death letter hook", e);
            }
        }
    }
}
