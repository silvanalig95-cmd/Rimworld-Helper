using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Pawns turn to the way they walk (the player, 21 November 2026, of the thorn-guards running in to beat out a fire:
    /// "When they were walking, the pawns were not actually using the walk, animation ... Instead, they were kind of a
    /// static sprite, just looking in one direction, and even walking in another direction ... They should always, NPCs
    /// should actually use the walking animation or action, which is the same as when the player like right clicks it").
    /// The game's rotation tracker turns a walking pawn to its next step, unless the job's toil handles its own facing
    /// (Toil.handlingFacing): then it leaves the pawn as the toil turned it. A toil of Rimfolk's that handled its facing and
    /// also walked the pawn (the thorn-guards' firefight, an animal keeping at someone's side, a bout's fighters closing in,
    /// a walk together, keeping someone company) slid them across the map facing one way. Those toils now leave the
    /// facing to the game while the pawn walks (<see cref="FacingWhileStill"/>, or the job's own target to face), and
    /// <see cref="Patch_WalkFacesPath"/> turns any pawn on a job of Rimfolk's to its path while it walks, whatever the toil
    /// says. Tools/Tests (WalkFacingTests) fails if a toil of Rimfolk's that handles its facing can start a walk.
    /// </summary>
    public static class WalkFacing
    {
        /// <summary>
        /// The toil turns its pawn its own way only while the pawn stands (to whom they talk, the place they keep), and leaves
        /// the facing to the game while they walk, so they turn to their path as anyone does. In place of
        /// <c>toil.handlingFacing = true</c> on a toil that can also walk.
        /// </summary>
        public static Toil FacingWhileStill(this Toil toil)
        {
            toil.AddPreTickAction(() =>
            {
                var p = toil.actor;
                toil.handlingFacing = p?.pather == null || !p.pather.Moving;
            });
            return toil;
        }

        private static readonly Assembly Ours = typeof(WalkFacing).Assembly;

        /// <summary>The job driver is one of Rimfolk's own (its type in Rimfolk's assembly); the game's and other mods' never.</summary>
        public static bool Rimfolks(JobDriver driver) => driver.GetType().Assembly == Ours;

        /// <summary>
        /// A pawn on a job of Rimfolk's whose toil handles its facing, walking a path: turned to its next step, as the game
        /// turns anyone who walks. Not while it aims at something (the game faces that), stunned so it can't turn, or of a
        /// kind that always faces one way.
        /// </summary>
        public static void TurnToPath(Pawn p)
        {
            var driver = p?.jobs?.curDriver;
            if (driver == null || !driver.HandlingFacing || !Rimfolks(driver) || !RimfolkPatches.On) return;
            var pather = p.pather;
            if (pather == null || !pather.Moving || pather.curPath == null || pather.curPath.NodesLeftCount < 1 || p.Destroyed) return;
            if (p.kindDef != null && p.kindDef.useFixedRotation) return;
            var stances = p.stances;
            if (stances != null)
            {
                if (stances.stunner != null && stances.stunner.Stunned && stances.stunner.DisableRotation) return;
                if (stances.curStance is Stance_Busy busy && busy.focusTarg.IsValid) return;
            }
            var step = pather.nextCell - p.Position;
            int rot = WalkFacingMath.Toward(step.x, step.z);
            if (rot != WalkFacingMath.None && p.Rotation.AsInt != rot) p.Rotation = new Rot4(rot);
        }
    }

    /// <summary>
    /// The one safety net: after the game's rotation tracker has had its turn (it skips a pawn whose toil handles its
    /// facing), a pawn on a job of Rimfolk's walking a path is turned to its next step (<see cref="WalkFacing.TurnToPath"/>).
    /// The game's and other mods' jobs are left as they are. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.UpdateRotation))]
    public static class Patch_WalkFacesPath
    {
        public static void Postfix(Pawn ___pawn)
        {
            try
            {
                WalkFacing.TurnToPath(___pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("walking pawns turned to their path", e);
            }
        }
    }
}
