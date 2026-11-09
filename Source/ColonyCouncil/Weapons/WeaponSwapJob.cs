using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Two colonists trade weapons (a suggestion better for both, Weapons/WeaponPick.cs): the one ordered walks over to the
    /// other, and when they meet, each hands the other what they hold; the sound of the weapons and the game's equip mark
    /// show it on the map. Called off if either stops holding what they held, the other is down, drafted, asleep or not
    /// themselves, or either may no longer carry the other's (coded, bonded). Simple Sidearms is told, by name, as its own
    /// equip job tells it, so it doesn't send them back for the weapon they gave away.
    /// </summary>
    public class JobDriver_SwapWeapons : JobDriver
    {
        private Pawn Other => job.GetTarget(TargetIndex.A).Thing as Pawn;
        private ThingWithComps Theirs => job.GetTarget(TargetIndex.B).Thing as ThingWithComps;
        private ThingWithComps Mine => job.GetTarget(TargetIndex.C).Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !WeaponSwap.Can(pawn, Other, Mine, Theirs));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var trade = ToilMaker.MakeToil("SwapWeapons");
            trade.initAction = () => WeaponSwap.Trade(pawn, Other, Mine, Theirs);
            trade.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return trade;
        }
    }

    public static class WeaponSwap
    {
        /// <summary>Both still hold what they held, are up and themselves (the other awake and not drafted), and may carry each other's.</summary>
        public static bool Can(Pawn a, Pawn b, ThingWithComps mine, ThingWithComps theirs)
        {
            if (a == null || b == null || mine == null || theirs == null || !b.Spawned || b.Map != a.Map) return false;
            if (b.Dead || b.Downed || b.InMentalState || b.Drafted || !b.Awake() || a.Downed || a.InMentalState) return false;
            if (a.equipment?.Primary != mine || b.equipment?.Primary != theirs) return false;
            return EquipmentUtility.CanEquip(theirs, a) && EquipmentUtility.CanEquip(mine, b);
        }

        /// <summary>Each hands the other what they hold.</summary>
        public static void Trade(Pawn a, Pawn b, ThingWithComps mine, ThingWithComps theirs)
        {
            if (!Can(a, b, mine, theirs)) return;
            try
            {
                a.equipment.Remove(mine);
                b.equipment.Remove(theirs);
                a.equipment.AddEquipment(theirs);
                b.equipment.AddEquipment(mine);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("two colonists trading weapons", e);
            }
            // Whatever didn't end up in a hand is set down beside them rather than lost.
            foreach (var (p, t) in new[] { (a, theirs), (b, mine) })
                if (t != null && !t.Destroyed && t.ParentHolder == null && p.Spawned)
                    GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
            Told(a, mine, theirs);
            Told(b, theirs, mine);
            if (a.Spawned) FleckMaker.Static(a.DrawPos, a.Map, FleckDefOf.FeedbackEquip);
            theirs.def.soundInteract?.PlayOneShot(new TargetInfo(a.Position, a.Map));
            WeaponPick.Dirty();
        }

        private static readonly Type Memory = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");
        private static readonly MethodInfo MemoryOf = Memory != null ? AccessTools.Method(Memory, "GetMemoryCompForPawn") : null;
        private static readonly MethodInfo Dropped = Memory != null ? AccessTools.Method(Memory, "InformOfDroppedSidearm") : null;
        private static readonly MethodInfo AddedPrimary = Memory != null ? AccessTools.Method(Memory, "InformOfAddedPrimary") : null;

        /// <summary>Simple Sidearms' memory told, as its own equip job tells it: the weapon given away forgotten, the one taken their own.</summary>
        private static void Told(Pawn p, Thing gave, Thing took)
        {
            if (MemoryOf == null || Dropped == null || AddedPrimary == null) return;
            try
            {
                var args = MemoryOf.GetParameters().Length == 2 ? new object[] { p, true } : new object[] { p };
                var memory = MemoryOf.Invoke(null, args);
                if (memory == null) return;
                Dropped.Invoke(memory, new object[] { gave, true });
                AddedPrimary.Invoke(memory, new object[] { took });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("telling Simple Sidearms of a weapon traded", e);
            }
        }
    }
}
