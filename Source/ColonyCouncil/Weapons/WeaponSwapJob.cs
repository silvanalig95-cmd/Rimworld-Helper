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

    /// <summary>
    /// A colonist takes a weapon another carries beyond their own kit (a spare, to fill an empty slot of theirs,
    /// Weapons/WeaponKitMath.cs): they walk over to the other, who hands it over from their pack; it goes into an empty hand,
    /// else into their pack as a sidearm. Called off if the other no longer carries it, is down, drafted, asleep or not
    /// themselves, or the one taking it may no longer carry it. Simple Sidearms is told by name on both sides, so neither
    /// goes back for it.
    /// </summary>
    public class JobDriver_TakeSpareWeapon : JobDriver
    {
        private Pawn Giver => job.GetTarget(TargetIndex.A).Thing as Pawn;
        private ThingWithComps Spare => job.GetTarget(TargetIndex.B).Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !WeaponSwap.CanHandOver(Giver, pawn, Spare));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var take = ToilMaker.MakeToil("TakeSpareWeapon");
            take.initAction = () => WeaponSwap.HandOver(Giver, pawn, Spare);
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
        }
    }

    public static class WeaponSwap
    {
        /// <summary>The giver still carries it in their pack and is up, awake, themselves and not drafted; the taker is up and may carry it.</summary>
        public static bool CanHandOver(Pawn giver, Pawn taker, ThingWithComps spare)
        {
            if (giver == null || taker == null || spare == null || !giver.Spawned || giver.Map != taker.Map) return false;
            if (giver.Dead || giver.Downed || giver.InMentalState || giver.Drafted || !giver.Awake() || taker.Downed || taker.InMentalState) return false;
            if (giver.inventory?.innerContainer == null || !giver.inventory.innerContainer.Contains(spare)) return false;
            return EquipmentUtility.CanEquip(spare, taker);
        }

        /// <summary>The giver hands the spare over: into the taker's empty hand, else into their pack as a sidearm.</summary>
        public static void HandOver(Pawn giver, Pawn taker, ThingWithComps spare)
        {
            if (!CanHandOver(giver, taker, spare)) return;
            bool inHand = taker.equipment != null && taker.equipment.Primary == null;
            try
            {
                giver.inventory.innerContainer.Remove(spare);
                if (inHand) taker.equipment.AddEquipment(spare);
                else taker.inventory.innerContainer.TryAdd(spare);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist handing over a spare weapon", e);
            }
            // Whatever didn't end up with the taker is set down beside them rather than lost.
            if (!spare.Destroyed && spare.ParentHolder == null && taker.Spawned) GenPlace.TryPlaceThing(spare, taker.Position, taker.Map, ThingPlaceMode.Near);
            TellGiven(giver, taker, spare, inHand);
            if (taker.Spawned) FleckMaker.Static(taker.DrawPos, taker.Map, FleckDefOf.FeedbackEquip);
            spare.def.soundInteract?.PlayOneShot(new TargetInfo(taker.Position, taker.Map));
            WeaponPick.Dirty();
        }
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
                var memory = Memory_(p);
                if (memory == null) return;
                Dropped.Invoke(memory, new object[] { gave, true });
                AddedPrimary.Invoke(memory, new object[] { took });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("telling Simple Sidearms of a weapon traded", e);
            }
        }

        private static readonly MethodInfo AddedSidearm = Memory != null ? AccessTools.Method(Memory, "InformOfAddedSidearm") : null;

        private static object Memory_(Pawn p) => MemoryOf.Invoke(null, MemoryOf.GetParameters().Length == 2 ? new object[] { p, true } : new object[] { p });

        /// <summary>Simple Sidearms told of a spare handed over: the giver forgets it, the taker remembers it (in hand, or as a sidearm).</summary>
        private static void TellGiven(Pawn giver, Pawn taker, Thing spare, bool inHand)
        {
            if (MemoryOf == null || Dropped == null || AddedPrimary == null || AddedSidearm == null) return;
            try
            {
                var gm = Memory_(giver);
                if (gm != null) Dropped.Invoke(gm, new object[] { spare, true });
                var tm = Memory_(taker);
                if (tm != null) (inHand ? AddedPrimary : AddedSidearm).Invoke(tm, new object[] { spare });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("telling Simple Sidearms of a spare weapon handed over", e);
            }
        }
    }
}
