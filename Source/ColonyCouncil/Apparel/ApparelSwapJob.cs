using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist takes a piece of clothing another carries unworn in their pack (a suggestion, Apparel/ApparelPick.cs):
    /// they walk over to the other, who hands it over, and they put it on there, taking as long as the game's own Wear takes
    /// (the piece's equip delay); what it takes the place of is dropped at their feet, as the game's Wear drops it. Called off
    /// if the other no longer carries it, is down, drafted, asleep or not themselves, or the one taking it may no longer wear
    /// it. Handed over, the piece is theirs: if they are stopped before it is on, it stays in their pack.
    /// </summary>
    public class JobDriver_TakeSpareApparel : JobDriver
    {
        private Pawn Giver => job.GetTarget(TargetIndex.A).Thing as Pawn;
        private Apparel Piece => job.GetTarget(TargetIndex.B).Thing as Apparel;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Piece == null || Piece.Destroyed);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !ApparelSwap.CanHandOver(Giver, pawn, Piece));
            var take = ToilMaker.MakeToil("TakeSpareApparel");
            take.initAction = () => ApparelSwap.HandOver(Giver, pawn, Piece);
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
            var wait = Toils_General.Wait(ApparelSwap.WearTicks(Piece));
            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.FailOn(() => pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.Contains(Piece));
            yield return wait;
            var on = ToilMaker.MakeToil("WearSpareApparel");
            on.initAction = () => ApparelSwap.PutOn(pawn, Piece, job.playerForced);
            on.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return on;
        }
    }

    /// <summary>
    /// Two colonists trade armour (a suggestion better for both, or for the colony, Apparel/ApparelPick.cs): the one ordered
    /// walks over to the other, and when they meet each takes theirs off and puts on the other's; the sound of the pieces and
    /// the game's equip mark show it on the map. Called off if either no longer wears what they wore, the other is down,
    /// drafted, asleep or not themselves, or either may no longer wear the other's.
    /// </summary>
    public class JobDriver_SwapApparel : JobDriver
    {
        private Pawn Other => job.GetTarget(TargetIndex.A).Thing as Pawn;
        private Apparel Theirs => job.GetTarget(TargetIndex.B).Thing as Apparel;
        private Apparel Mine => job.GetTarget(TargetIndex.C).Thing as Apparel;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !ApparelSwap.Can(pawn, Other, Mine, Theirs));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var trade = ToilMaker.MakeToil("SwapApparel");
            trade.initAction = () => ApparelSwap.Trade(pawn, Other, Mine, Theirs, job.playerForced);
            trade.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return trade;
        }
    }

    public static class ApparelSwap
    {
        /// <summary>How long putting a piece on takes: its equip delay, as the game's own Wear job takes it.</summary>
        public static int WearTicks(Apparel ap)
        {
            try
            {
                return ap == null ? 60 : Math.Max(1, (int)(ap.GetStatValue(StatDefOf.EquipDelay) * 60f));
            }
            catch
            {
                return 60;
            }
        }

        /// <summary>The giver still carries it in their pack and is up, awake, themselves and not drafted; the taker is up and may wear it.</summary>
        public static bool CanHandOver(Pawn giver, Pawn taker, Apparel ap)
        {
            if (giver == null || taker == null || ap == null || ap.Destroyed || !giver.Spawned || giver.Map != taker.Map) return false;
            if (giver.Dead || giver.Downed || giver.InMentalState || giver.Drafted || !giver.Awake() || taker.Downed || taker.InMentalState) return false;
            if (giver.inventory?.innerContainer == null || !giver.inventory.innerContainer.Contains(ap)) return false;
            return taker.apparel != null && ap.PawnCanWear(taker, true) && EquipmentUtility.CanEquip(ap, taker) && !taker.apparel.WouldReplaceLockedApparel(ap);
        }

        /// <summary>The giver hands it over, into the taker's pack until it is on.</summary>
        public static void HandOver(Pawn giver, Pawn taker, Apparel ap)
        {
            if (!CanHandOver(giver, taker, ap)) return;
            try
            {
                giver.inventory.innerContainer.TryTransferToContainer(ap, taker.inventory.innerContainer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist handing over a spare piece of clothing", e);
            }
            if (!ap.Destroyed && ap.ParentHolder == null && taker.Spawned) GenPlace.TryPlaceThing(ap, taker.Position, taker.Map, ThingPlaceMode.Near);
            ap.def.soundInteract?.PlayOneShot(new TargetInfo(taker.Position, taker.Map));
        }

        /// <summary>Puts a piece from their pack on, dropping what it takes the place of at their feet as the game's Wear does; kept on as an order of the player's is.</summary>
        public static void PutOn(Pawn p, Apparel ap, bool forced)
        {
            if (p?.apparel == null || ap == null || ap.Destroyed || p.inventory?.innerContainer == null || !p.inventory.innerContainer.Contains(ap)) return;
            try
            {
                p.inventory.innerContainer.Remove(ap);
                p.apparel.Wear(ap, true);
                if (forced && p.outfits != null) p.outfits.forcedHandler.SetForced(ap, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist putting on a piece of clothing handed over", e);
            }
            if (!ap.Destroyed && ap.ParentHolder == null && p.Spawned) GenPlace.TryPlaceThing(ap, p.Position, p.Map, ThingPlaceMode.Near);
            if (p.Spawned) FleckMaker.Static(p.DrawPos, p.Map, FleckDefOf.FeedbackEquip);
            ApparelPick.Dirty();
        }

        /// <summary>Both still wear what they wore, are up and themselves (the other awake and not drafted), neither piece locked, and each may wear the other's.</summary>
        public static bool Can(Pawn a, Pawn b, Apparel mine, Apparel theirs)
        {
            if (a == null || b == null || mine == null || theirs == null || !b.Spawned || b.Map != a.Map || a.apparel == null || b.apparel == null) return false;
            if (b.Dead || b.Downed || b.InMentalState || b.Drafted || !b.Awake() || a.Downed || a.InMentalState) return false;
            if (!a.apparel.WornApparel.Contains(mine) || !b.apparel.WornApparel.Contains(theirs)) return false;
            if (a.apparel.IsLocked(mine) || b.apparel.IsLocked(theirs)) return false;
            return theirs.PawnCanWear(a, true) && mine.PawnCanWear(b, true) && EquipmentUtility.CanEquip(theirs, a) && EquipmentUtility.CanEquip(mine, b);
        }

        /// <summary>Each takes theirs off and puts on the other's; whatever else either can't wear with it is dropped at their feet, as the game's Wear drops it.</summary>
        public static void Trade(Pawn a, Pawn b, Apparel mine, Apparel theirs, bool forced)
        {
            if (!Can(a, b, mine, theirs)) return;
            try
            {
                a.apparel.Remove(mine);
                b.apparel.Remove(theirs);
                a.apparel.Wear(theirs, true);
                b.apparel.Wear(mine, true);
                if (forced)
                {
                    a.outfits?.forcedHandler.SetForced(theirs, true);
                    b.outfits?.forcedHandler.SetForced(mine, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("two colonists trading armour", e);
            }
            // Anything that ended up with neither is set down beside them rather than lost.
            foreach (var (ap, p) in new[] { (mine, b), (theirs, a) })
                if (!ap.Destroyed && ap.ParentHolder == null && p.Spawned)
                    GenPlace.TryPlaceThing(ap, p.Position, p.Map, ThingPlaceMode.Near);
            if (a.Spawned) FleckMaker.Static(a.DrawPos, a.Map, FleckDefOf.FeedbackEquip);
            if (b.Spawned) FleckMaker.Static(b.DrawPos, b.Map, FleckDefOf.FeedbackEquip);
            theirs.def.soundInteract?.PlayOneShot(new TargetInfo(a.Position, a.Map));
            ApparelPick.Dirty();
        }
    }
}
