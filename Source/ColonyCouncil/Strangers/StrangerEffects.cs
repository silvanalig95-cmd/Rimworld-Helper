using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The stranger joins the colony: a visitor who stays, a prisoner won over, a wild animal that follows A home.</summary>
    public class DilemmaEffect_Recruit : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned) return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            if (Strangers.IsHospitalityGuest(ctx.stranger)) return "CC_Stranger_HospitalityRecruit".Translate(ctx.stranger.LabelShort).ToString();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Faction == Faction.OfPlayer || Strangers.IsHospitalityGuest(x)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var recruiter = ctx.a != null && !ctx.a.Dead ? ctx.a : ctx.Colonists.FirstOrDefault();
            if (recruiter == null) return;
            if (x.Downed && x.RaceProps.Humanlike) return;
            InteractionWorker_RecruitAttempt.DoRecruit(recruiter, x, true);
        }
    }

    /// <summary>The stranger's group leaves the map now (thrown out, or sent on their way with thanks).</summary>
    public class DilemmaEffect_SendAway : DilemmaEffect
    {
        /// <summary>Hospitality's guests leave when their visit ends, not when the council says: the answer can't be given.</summary>
        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && !x.Dead && x.Spawned && Strangers.IsHospitalityGuest(x)) return "CC_Stranger_HospitalitySendAway".Translate(x.LabelShort).ToString();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Faction == null || x.Faction.IsPlayer || x.IsPrisoner) return;
            var map = x.Map;
            var lord = x.GetLord();
            // Hospitality runs its own visits; leave its guests to it rather than break its bookkeeping.
            if (lord?.LordJob != null && lord.LordJob.GetType().Namespace == "Hospitality") return;
            var group = (lord != null ? lord.ownedPawns.ToList() : new List<Pawn> { x }).Where(p => p.Spawned && !p.Dead && !p.Downed).ToList();
            if (group.Count == 0) return;
            if (lord != null) map.lordManager.RemoveLord(lord);
            LordMaker.MakeNewLord(x.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), map, group);
        }
    }

    /// <summary>Sets what the wardens do with the prisoner: Release, Execution, ReduceResistance, AttemptRecruit...</summary>
    public class DilemmaEffect_PrisonerOrder : DilemmaEffect
    {
        public PrisonerInteractionModeDef mode;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (mode == null || x == null || x.Dead || !x.IsPrisonerOfColony || x.guest == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            x.guest.SetExclusiveInteraction(mode);
        }
    }

    /// <summary>Changes the prisoner's resistance to recruitment (and will, for enslaving). Negative softens them.</summary>
    public class DilemmaEffect_Resistance : DilemmaEffect
    {
        public float resistance;
        public float will;

        public override void Apply(DilemmaContext ctx)
        {
            var g = ctx.stranger?.guest;
            if (g == null || ctx.stranger.Dead || !ctx.stranger.IsPrisonerOfColony) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            g.resistance = Mathf.Max(0f, g.resistance + resistance);
            g.will = Mathf.Max(0f, g.will + will);
        }
    }

    /// <summary>A ends the stranger's life: a mercy for a dying enemy, or not.</summary>
    public class DilemmaEffect_EndIt : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            if (ctx.a != null && !ctx.a.Dead && ctx.a.Spawned && x.RaceProps.Humanlike) ExecutionUtility.DoExecutionByCut(ctx.a, x, 8, true);
            else x.Kill(null);
        }
    }

    /// <summary>A carries the downed stranger to a prisoner bed (if there's one free).</summary>
    public class DilemmaEffect_Capture : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            var a = ctx.a;
            if (x == null || a == null || x.Dead || !x.Spawned || !x.Downed || !a.Spawned || a.Downed || a.Map != x.Map) return;
            var bed = RestUtility.FindBedFor(x, a, false, false, GuestStatus.Prisoner);
            if (bed == null)
            {
                Messages.Message("CC_Stranger_NoPrisonBed".Translate(a.Named("PAWN"), x.Named("X")).Resolve(), x, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var job = JobMaker.MakeJob(JobDefOf.Capture, x, bed);
            job.count = 1;
            a.jobs?.TryTakeOrderedJob(job, JobTag.Misc, false);
        }
    }

    /// <summary>Marks the animal for hunting or taming.</summary>
    public class DilemmaEffect_Designate : DilemmaEffect
    {
        public DesignationDef designation;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (designation == null || x == null || x.Dead || !x.Spawned) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var dm = x.Map.designationManager;
            if (dm.DesignationOn(x) != null) return;
            dm.AddDesignation(new Designation(x, designation));
        }
    }

    /// <summary>A's secret comes out (told by the stranger: a visitor who knew them before).</summary>
    public class DilemmaEffect_RevealSecret : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var s = comp?.secrets.Of(ctx.a);
            if (s == null || s.revealed) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            comp.secrets.Reveal(comp, s, null);
        }
    }

    /// <summary>A sells the heirloom they carry to the stranger (a trader) for its worth in silver, times a factor.</summary>
    public class DilemmaEffect_SellHeirloom : DilemmaEffect
    {
        public float priceFactor = 1.5f;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned || ctx.stranger.Map != ctx.map)
                return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            if (ctx.a == null || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_Stranger_Away".Translate(ctx.a?.LabelShort ?? "?").ToString();
            return CouncilGameComponent.Instance?.legacy.HeirloomCarriedBy(ctx.a) == null ? "CC_Stranger_NoHeirloom".Translate(ctx.a?.LabelShort ?? "?").ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var a = ctx.a;
            var h = comp?.legacy.HeirloomCarriedBy(a);
            if (h?.thing == null || h.thing.Destroyed || a == null || !a.Spawned || ctx.stranger == null || !ctx.stranger.Spawned) return;
            var thing = h.thing;
            int price = Mathf.Max(1, Mathf.RoundToInt(thing.MarketValue * priceFactor));
            comp.legacy.Forget(h);
            if (thing is ThingWithComps eq && a.equipment?.Primary == eq) a.equipment.Remove(eq);
            else if (thing is Apparel ap && (a.apparel?.WornApparel.Contains(ap) ?? false)) a.apparel.Remove(ap);
            else a.inventory?.innerContainer?.Remove(thing);
            var buyer = ctx.stranger;
            if (buyer?.inventory?.innerContainer == null || buyer.Dead || !buyer.inventory.innerContainer.TryAdd(thing)) thing.Destroy();
            int left = price;
            while (left > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Mathf.Min(left, ThingDefOf.Silver.stackLimit);
                left -= silver.stackCount;
                GenPlace.TryPlaceThing(silver, a.PositionHeld, a.MapHeld ?? ctx.map, ThingPlaceMode.Near);
            }
            ctx.count = price;
        }
    }

    /// <summary>A's animal goes with the stranger: given or sold to a visitor, it leaves with their group.</summary>
    public class DilemmaEffect_GivePet : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned || ctx.stranger.Map != ctx.map)
                return "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            if (ctx.pet == null || ctx.pet.Dead || ctx.pet.Faction != Faction.OfPlayer) return "CC_Stranger_PetGone".Translate().ToString();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var pet = ctx.pet;
            var x = ctx.stranger;
            if (pet == null || pet.Dead || pet.Faction != Faction.OfPlayer) return;
            if (x == null || x.Dead || !x.Spawned || x.Faction == null || x.Faction.IsPlayer)
            {
                // Nobody to take it: it's turned loose instead.
                if (pet.Spawned) pet.jobs?.StopAll();
                pet.SetFaction(null);
                return;
            }
            if (pet.Spawned) pet.jobs?.StopAll();
            pet.SetFaction(x.Faction);
            var lord = x.GetLord();
            if (pet.Spawned && pet.Map == x.Map && lord != null && lord.CanAddPawn(pet)) lord.AddPawn(pet);
        }
    }

    /// <summary>Something a life goal can grow from (a spark: battle, loss, news...) or count (a counter).</summary>
    public class DilemmaEffect_LifeEvent : DilemmaEffect
    {
        public string spark;
        public string counter;

        public override void Apply(DilemmaContext ctx)
        {
            var asp = CouncilGameComponent.Instance?.aspirations;
            if (asp == null) return;
            foreach (var p in Targets(ctx))
            {
                if (!spark.NullOrEmpty()) asp.Notify_Event(p, spark);
                if (!counter.NullOrEmpty()) asp.Notify(p, counter);
            }
        }
    }
}
