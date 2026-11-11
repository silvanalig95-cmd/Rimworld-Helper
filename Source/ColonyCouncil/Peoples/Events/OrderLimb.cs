using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 5. A limb fitted (the hospitaller order) =====================

    /// <summary>
    /// The order's limb for a colonist missing a hand, a foot, an arm or a leg: a peg leg or a wooden hand from the colony's
    /// wood before the industrial age, the game's simple prostheses after (PeopleEventsMath.LimbRecipe), never bionics,
    /// fitted at the colony's bed by the order's surgeon with the game's own surgery and its own chance.
    /// </summary>
    public static class OrderLimb
    {
        public const string GiftKind = "limb";

        private static bool Below(BodyPartRecord part, BodyPartRecord top)
        {
            for (var p = part; p != null; p = p.parent)
                if (p == top) return true;
            return false;
        }

        /// <summary>The order's limb for one of this colonist's missing parts in the order's age, and the part it goes on.</summary>
        public static bool Need(Pawn p, int age, out RecipeDef recipe, out BodyPartRecord part)
        {
            recipe = null;
            part = null;
            if (p?.health?.hediffSet == null || !p.RaceProps.Humanlike) return false;
            try
            {
                foreach (var missing in p.health.hediffSet.GetMissingPartsCommonAncestors())
                {
                    var gone = missing?.Part;
                    string kind = PeopleEventsMath.PartKind(gone?.def?.defName);
                    var r = DefDatabase<RecipeDef>.GetNamedSilentFail(PeopleEventsMath.LimbRecipe(kind, age) ?? "");
                    if (r?.Worker == null || r.addsHediff == null) continue;
                    var on = r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault(x => x != null && Below(gone, x));
                    if (on == null) continue;
                    recipe = r;
                    part = on;
                    return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the order's limb", e);
            }
            return false;
        }

        /// <summary>How much of the colony's wood a wooden limb takes (the recipe's own), else none.</summary>
        public static int Wood(RecipeDef r)
        {
            if (r == null || !PeopleEventsMath.Wooden(r.defName)) return 0;
            var ing = r.ingredients?.FirstOrDefault(i => i?.filter != null && i.filter.Allows(ThingDefOf.WoodLog));
            return ing != null ? Mathf.Max(1, Mathf.RoundToInt(ing.GetBaseCount())) : 20;
        }

        /// <summary>A bed of the colony's for the fitting: a free medical bed first, else the colonist's own.</summary>
        public static Building_Bed Bed(Pawn p)
        {
            var map = p?.Map;
            if (map == null) return null;
            var beds = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => b != null && b.Spawned && b.def.building.bed_humanlike && !b.ForPrisoners && !b.IsBurning() && (b.Medical || b.OwnersForReading.Contains(p))
                            && b.CurOccupants.All(o => o == p) && p.CanReach(b, PathEndMode.OnCell, Danger.Some))
                .OrderByDescending(b => b.Medical ? 1 : 0).ThenBy(b => b.Position.DistanceToSquared(p.Position)).ToList();
            return beds.FirstOrDefault();
        }

        /// <summary>The game's own chance of the fitting, as the card shows it: its surgery quality with their surgeon.</summary>
        public static int Chance(RecipeDef r, Pawn surgeon, Pawn patient, BodyPartRecord part)
        {
            if (r?.surgeryOutcomeEffect == null || surgeon == null || patient == null) return 100;
            try
            {
                return PeopleEventsMath.ChancePercent(r.surgeryOutcomeEffect.GetQuality(r, surgeon, patient, new List<Thing>(), part, null));
            }
            catch
            {
                return 0;
            }
        }

        public static IEnumerable<Pawn> Takers(Map map, int age) =>
            PeopleEvents.Takers(map, GiftKind).Where(p => Need(p, age, out _, out _) && Bed(p) != null);
    }

    /// <summary>
    /// A hospitaller surgeon of the order comes with a limb of the order's age ({TALE}) for {B}, who is missing their {RELATION}:
    /// fitted at the colony's bed with the game's own surgery, the colony's doctor {A} at the bedside, learning. No silver:
    /// "we were spared, and so we owe". {B}'s name goes into the Book of the Spared, and the colony owes the order a favour,
    /// unless it sends medicine back for their sickhouse.
    /// </summary>
    public class HappeningWorker_OrderLimb : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Healers);
            return f != null && OrderLimb.Takers(map, PeopleEvents.AgeOf(f)).Any() && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Healers);
            if (ctx.faction == null) return false;
            int age = PeopleEvents.AgeOf(ctx.faction);
            ctx.b = OrderLimb.Takers(ctx.map, age).RandomElementWithFallback();
            if (ctx.b == null || !OrderLimb.Need(ctx.b, age, out var recipe, out var part)) return false;
            ctx.note = recipe.defName;
            ctx.tale = recipe.addsHediff.label;
            // {RELATION} names the part (the event has no relation of its own to name).
            ctx.relation = part.Label;
            ctx.count = OrderLimb.Wood(recipe);
            ctx.place = OrderLimb.Bed(ctx.b);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Healer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(PeopleEventsMath.SurgeonMin, PeopleEventsMath.SurgeonMax));
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Brother"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => p != ctx.b && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEventsMath.Wooden(ctx.note) ? "wooden" : "prosthetic");
            if (ctx.a == null) tags.Add("nodoctor");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_PatientGone".Translate().Resolve();
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(ctx.note ?? "");
            if (r == null || !OrderLimb.Need(ctx.b, PeopleEvents.AgeOf(ctx.faction), out var now, out _) || now != r) return "CC_PEv_LimbFitted".Translate().Resolve();
            if (OrderLimb.Bed(ctx.b) == null) return "CC_PEv_NoBed".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "fit" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The fitting: the wood carried out for a wooden limb, {B} laid in the bed, the doctor {A} at the bedside, and the
    /// surgeon at work (PeopleWorkOn, Rimfolk_PEv_Limb); then the game's own surgery (OrderLimbDone).
    /// <see cref="repaid"/>: the colony sends medicine back for the sickhouse, and owes nothing.
    /// </summary>
    public class DilemmaEffect_OrderLimb : DilemmaEffect
    {
        public bool repaid;

        public override bool ChanceEach => false;

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Limb");

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            int wood = hc?.count ?? 0;
            if (wood > 0 && PlayCarry.Have(ctx.map, "wood", null) < wood) return "CC_Dilemma_NotEnough".Translate(wood, ThingDefOf.WoodLog.label).ToString();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(hc.note ?? "");
            if (r == null || !OrderLimb.Need(hc.b, PeopleEvents.AgeOf(hc.faction), out _, out var part)) return;
            if (hc.count > 0) sheet.Goods(-1, ThingDefOf.WoodLog, hc.count);
            sheet.Add(ImpactKind.Health, 1, "CC_PEv_Impact_Limb".Translate(hc.b.LabelShort, hc.tale ?? "?", part.Label).Resolve(), "CC_PEv_Impact_LimbBrief".Translate().Resolve(), "pevlimb");
            int chance = OrderLimb.Chance(r, hc.stranger, hc.b, part);
            sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_LimbRisk".Translate(hc.b.LabelShort, (100 - chance).Named("FAIL"), chance.Named("CHANCE")).Resolve(),
                "CC_PEv_Impact_LimbRiskBrief".Translate().Resolve(), "pevlimbrisk");
            if (hc.a != null) sheet.Play("CC_PEv_Impact_Assist".Translate(hc.a.LabelShort, hc.b.LabelShort).Resolve(), "CC_PEv_Impact_AssistBrief".Translate().Resolve());
            if (!repaid && FavoursOwed.On) sheet.Add(ImpactKind.Goodwill, 0, "CC_PEv_Impact_Owe".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv_Impact_OweBrief".Translate().Resolve(), "pevowe");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            var x = hc?.stranger;
            if (hc == null || map == null || x == null || hc.b == null || Job == null) return;
            var bed = OrderLimb.Bed(hc.b) ?? hc.place as Building_Bed;
            // The wood for a wooden limb, carried out to the surgeon.
            if (hc.count > 0)
            {
                // apart: a colonist carries the wood out to the surgeon at the gate, where one of the order takes it.
                new DilemmaEffect_CarryOut { what = "wood", count = hc.count, steward = false, who = PlayWho.None, label = "CC_PEv_WoodLabel".Translate(x.LabelShort.Named("X")).Resolve() }.Apply(ctx);
            }
            // The doctor at the bedside for the fitting.
            var doc = hc.a;
            if (doc != null && doc != hc.b && PlayOuts.On && bed != null && PeoplesG.Up(doc, map)) PeopleWorkOn.Bring(doc, bed, "beside", PeopleEventsMath.FitTicks + 2 * GenDate.TicksPerHour);
            string note = hc.note + ";" + (repaid ? 1 : 0) + ";" + (doc?.thingIDNumber ?? -1);
            PeopleWorkOn.Begin(hc, x, hc.b, Job, bed != null ? new LocalTargetInfo(bed) : LocalTargetInfo.Invalid, note, 0, PeopleEventsMath.FitTicks, 8f);
        }
    }

    /// <summary>The fitting done: the game's own surgery on the part (success by its chance, a failure its own mishap), the doctor's lesson, the Book of the Spared.</summary>
    public class OrderLimbDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on)
        {
            var parts = (PeopleWorkOn.Note(v) ?? "").Split(';');
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(parts.Length > 0 ? parts[0] : "");
            bool repaid = parts.Length > 1 && parts[1] == "1";
            int docId = parts.Length > 2 && int.TryParse(parts[2], out int d) ? d : -1;
            var map = on?.MapHeld ?? v.map;
            var f = worker?.Faction;
            if (r == null || on == null || on.Dead || !OrderLimb.Need(on, PeopleEvents.AgeOf(f), out var now, out var part) || now != r) return;
            bool fitted = false;
            try
            {
                r.Worker.ApplyOnPawn(on, part, worker, new List<Thing>(), null);
                fitted = !on.Dead && on.health.hediffSet.hediffs.Any(h => h.def == r.addsHediff && h.Part == part);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the order's surgery", e);
            }
            var doc = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == docId);
            if (doc != null && !doc.Dead)
            {
                doc.skills?.Learn(SkillDefOf.Medicine, PeopleEventsMath.AssistXp);
                PeopleWorkOn.Release(doc);
            }
            PeopleEventsBook.Gave(OrderLimb.GiftKind, on);
            if (fitted)
            {
                if (f != null && !repaid) FactionMemory.Note(f, "aid", on.LabelShort, PeopleEventsMath.SparedWeight);
                else if (f != null) FactionMemory.Note(f, "gift", on.LabelShort);
                PeoplesG.SayLine(worker, v.incident, "spared");
                Tell.Message(NewsKind.Health, Importance.Notable, map,
                    "CC_PEv_LimbDone".Translate(worker.LabelShort.Named("SURGEON"), on.Named("PAWN"), r.addsHediff.label.Named("LIMB"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else if (!on.Dead)
            {
                PeoplesG.SayLine(worker, v.incident, "sorry");
                Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv_LimbFailed".Translate(worker.LabelShort.Named("SURGEON"), on.Named("PAWN")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.NegativeHealthEvent);
            }
        }

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Health, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
