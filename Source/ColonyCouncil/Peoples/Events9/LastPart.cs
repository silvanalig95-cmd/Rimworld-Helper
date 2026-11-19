using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 92. The last part (the Sunken Vault) =====================

    /// <summary>
    /// The last part (item 92, a gift): when a colonist has lost an eye, an arm, a hand, a leg or a foot and the colony has no part of any
    /// kind for it, the vault's medic comes with one of the last bionics the vault kept (an eye, an arm or a leg, the game's own; the vault
    /// can't make them, so in every age it is one kept from before) and trades it for a large supply of components and food, which holds the
    /// vault's age for a year (a memory of supply of that weight, PeopleEvents9Math.YearOfSupplyWeight). It is fitted by the game's own
    /// surgery: by the vault's medic at the colony's bed (round 1's limb fitting: the game's chance, a failure its own mishap, on the card),
    /// or by the colony's own doctor (the part handed over and the game's surgery bill set on the colonist).
    /// </summary>
    public static class LastPart
    {
        public const string GiftKind = "vaultPart";

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Bionic");

        private static bool Below(BodyPartRecord part, BodyPartRecord top)
        {
            for (var p = part; p != null; p = p.parent)
                if (p == top) return true;
            return false;
        }

        /// <summary>The vault's bionic for one of this colonist's missing parts, and the part it goes on.</summary>
        public static bool Need(Pawn p, out RecipeDef recipe, out BodyPartRecord part)
        {
            recipe = null;
            part = null;
            if (p?.health?.hediffSet == null || !p.RaceProps.Humanlike) return false;
            try
            {
                foreach (var missing in p.health.hediffSet.GetMissingPartsCommonAncestors())
                {
                    var gone = missing?.Part;
                    var r = DefDatabase<RecipeDef>.GetNamedSilentFail(PeopleEvents9Math.BionicRecipe(PeopleEvents9Math.PartKind(gone?.def?.defName)) ?? "");
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
                CouncilLog.ErrorOnce("peoples' events 9: the vault's part", e);
            }
            return false;
        }

        private static readonly Dictionary<ThingDef, List<BodyPartDef>> fits = new Dictionary<ThingDef, List<BodyPartDef>>();

        /// <summary>The body parts an item of this kind goes on, by the install recipes that use it (any mod's).</summary>
        private static List<BodyPartDef> Fits(ThingDef d)
        {
            if (fits.TryGetValue(d, out var l)) return l;
            l = DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r?.addsHediff != null && r.appliedOnFixedBodyParts != null && r.IsIngredient(d))
                .SelectMany(r => r.appliedOnFixedBodyParts).Distinct().ToList();
            fits[d] = l;
            return l;
        }

        /// <summary>The colony holds a part of any kind (a mod's, a peg leg) that goes on what this part hangs from.</summary>
        public static bool HasPart(Map map, BodyPartRecord part)
        {
            if (map == null || part == null) return false;
            var wanted = new HashSet<BodyPartDef>();
            for (var p = part; p != null; p = p.parent) wanted.Add(p.def);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (t?.def == null || !t.def.isTechHediff && t.def.thingCategories?.Any(c => c.defName.StartsWith("BodyParts")) != true) continue;
                if (t.IsForbidden(Faction.OfPlayer) || t.Fogged()) continue;
                if (Fits(t.def).Any(wanted.Contains)) return true;
            }
            return false;
        }

        public static IEnumerable<Pawn> Takers(Map map) =>
            PeopleEvents.Takers(map, GiftKind).Where(p => Need(p, out _, out var part) && !HasPart(map, part) && OrderLimb.Bed(p) != null);

        /// <summary>The supply the colony gave: a year of the vault's age held.</summary>
        public static void Supplied(Faction f, int components, int food)
        {
            if (f == null || f.IsPlayer || f.defeated) return;
            FactionMemory.Note(f, PeopleAgeGifts.DeedKind, "CC_PeopleAge_Gift_components".Translate().Resolve(), PeopleEvents9Math.YearOfSupplyWeight);
        }

        /// <summary>The fitting done at the bed: the game's own surgery on the part (its chance; a failure its own mishap), the doctor's lesson, told.</summary>
        public static void Fitted(Visit v, Pawn medic, Pawn on)
        {
            var parts = (PeopleWorkOn.Note(v) ?? "").Split(';');
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(parts.Length > 0 ? parts[0] : "");
            int docId = parts.Length > 1 && int.TryParse(parts[1], out int d) ? d : -1;
            var map = on?.MapHeld ?? v.map;
            if (r == null || on == null || on.Dead || !Need(on, out var now, out var part) || now != r) return;
            bool fitted = false;
            try
            {
                r.Worker.ApplyOnPawn(on, part, medic, new List<Thing>(), null);
                fitted = !on.Dead && on.health.hediffSet.hediffs.Any(h => h.def == r.addsHediff && h.Part == part);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the vault's surgery", e);
            }
            var doc = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == docId);
            if (doc != null && !doc.Dead)
            {
                doc.skills?.Learn(SkillDefOf.Medicine, PeopleEventsMath.AssistXp);
                PeopleWorkOn.Release(doc);
            }
            PeopleEventsBook.Gave(GiftKind, on);
            if (fitted)
            {
                PeoplesG.SayLine(medic, v.incident, "fitted");
                Tell.Message(NewsKind.Health, Importance.Notable, map,
                    "CC_PEv9_PartFitted".Translate((medic?.LabelShort ?? "?").Named("MEDIC"), on.Named("PAWN"), r.addsHediff.label.Named("PART")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else if (!on.Dead)
            {
                PeoplesG.SayLine(medic, v.incident, "sorry");
                Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv9_PartFailed".Translate((medic?.LabelShort ?? "?").Named("MEDIC"), on.Named("PAWN")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.NegativeHealthEvent);
            }
        }

        /// <summary>The part handed over at the gate and the game's own surgery bill set on the colonist, for the colony's doctor.</summary>
        public static void HandOver(HappeningContext ctx, RecipeDef r, BodyPartRecord part)
        {
            var map = ctx.map;
            var item = r?.ingredients?.SelectMany(i => i.filter?.AllowedThingDefs ?? Enumerable.Empty<ThingDef>()).FirstOrDefault(t => t.isTechHediff || t.IsWithinCategory(ThingCategoryDefOf.BodyParts));
            if (map == null || item == null) return;
            var x = ctx.stranger;
            Couriers.Hand(item, 1, map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, ctx.faction, x);
            try
            {
                if (ctx.b?.BillStack != null && !ctx.b.BillStack.Bills.Any(b => b.recipe == r)) ctx.b.BillStack.AddBill(new Bill_Medical(r, null) { Part = part });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the surgery bill", e);
            }
            PeopleEventsBook.Gave(GiftKind, ctx.b);
            Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv9_PartHanded".Translate((x?.LabelShort ?? "?").Named("MEDIC"), ctx.b.Named("PAWN"), item.label.Named("PART")).Resolve(),
                ctx.b.Spawned ? new LookTargets(ctx.b) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// The vault's medic of {FACTION} ({X}) at the gate with one of the last bionics the vault kept ({TALE}) for {B}, who is missing their
    /// {RELATION} and for whom the colony has no part: for {COUNT} components and some food, fitted by the game's own surgery.
    /// </summary>
    public class HappeningWorker_VaultLastPart : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && LastPart.Takers(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            if (ctx.faction == null) return false;
            ctx.b = LastPart.Takers(ctx.map).RandomElementWithFallback();
            if (ctx.b == null || !LastPart.Need(ctx.b, out var recipe, out var part)) return false;
            ctx.note = recipe.defName;
            ctx.tale = recipe.addsHediff.label;
            // {RELATION} names the part (the event has no relation of its own to name).
            ctx.relation = part.Label;
            float wf = LivingEvents.WealthFactor(ctx.map);
            ctx.count = PeopleEvents9Math.PartComponents(wf);
            ctx.variant = PeopleEvents9Math.PartFood(wf);
            ctx.thing = ThingDefOf.ComponentIndustrial;
            ctx.place = OrderLimb.Bed(ctx.b);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Medic"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(8, 13));
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Dweller"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => p != ctx.b && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PeopleEvents.AgeOf(ctx.faction) < 3) tags.Add("fallen");
            if (ctx.a == null) tags.Add("nodoctor");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_PatientGone".Translate().Resolve();
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(ctx.note ?? "");
            if (r == null || !LastPart.Need(ctx.b, out var now, out _) || now != r) return "CC_PEv_LimbFitted".Translate().Resolve();
            if (option == 0 && OrderLimb.Bed(ctx.b) == null) return "CC_PEv_NoBed".Translate().Resolve();
            if (option == 1 && ctx.a == null) return "CC_PEv9_NoDoctor".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "fit" : option == 1 ? "hand" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The trade made (the components and food carried out by the event's own carry-outs; a year of the vault's age held), and the part
    /// fitted: by the vault's medic at the colony's bed (PeopleWorkOn, Rimfolk_PEv9_Bionic, the colony's doctor beside them), or with
    /// <see cref="ours"/> handed over and the game's surgery bill set for the colony's own doctor.
    /// </summary>
    public class DilemmaEffect_VaultLastPart : DilemmaEffect
    {
        public bool ours;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(hc.note ?? "");
            if (r == null || !LastPart.Need(hc.b, out _, out var part)) return;
            sheet.Add(ImpactKind.Health, 1, "CC_PEv9_Impact_Part".Translate(hc.b.LabelShort, hc.tale ?? "?", part.Label).Resolve(), "CC_PEv9_Impact_PartBrief".Translate().Resolve(), "pev9part");
            var surgeon = ours ? hc.a : hc.stranger;
            int chance = OrderLimb.Chance(r, surgeon, hc.b, part);
            sheet.Add(ImpactKind.Health, -1, "CC_PEv9_Impact_PartRisk".Translate(hc.b.LabelShort, (surgeon?.LabelShort ?? "?").Named("SURGEON"), (100 - chance).Named("FAIL"), chance.Named("CHANCE")).Resolve(),
                "CC_PEv_Impact_LimbRiskBrief".Translate().Resolve(), "pev9partrisk");
            if (!ours && hc.a != null) sheet.Play("CC_PEv_Impact_Assist".Translate(hc.a.LabelShort, hc.b.LabelShort).Resolve(), "CC_PEv_Impact_AssistBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PEv9_Impact_PartYear".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "pev9partyear");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.PartGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            var x = hc?.stranger;
            if (map == null || x == null || hc.b == null || hc.faction == null) return;
            LastPart.Supplied(hc.faction, hc.count, hc.variant);
            PeopleEvents9.Goodwill(hc.faction, PeopleEvents9Math.PartGoodwill, GoodwillWhy.Paid);
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(hc.note ?? "");
            if (r == null || !LastPart.Need(hc.b, out _, out var part)) return;
            if (ours)
            {
                LastPart.HandOver(hc, r, part);
                return;
            }
            if (LastPart.Job == null) return;
            var bed = OrderLimb.Bed(hc.b) ?? hc.place as Building_Bed;
            var doc = hc.a;
            if (doc != null && doc != hc.b && PlayOuts.On && bed != null && PeoplesG.Up(doc, map)) PeopleWorkOn.Bring(doc, bed, "beside", PeopleEventsMath.FitTicks + 2 * GenDate.TicksPerHour);
            PeopleWorkOn.Begin(hc, x, hc.b, LastPart.Job, bed != null ? new LocalTargetInfo(bed) : LocalTargetInfo.Invalid, hc.note + ";" + (doc?.thingIDNumber ?? -1), 0, PeopleEventsMath.FitTicks, 8f);
        }
    }

    /// <summary>The fitting at the bed done: the game's own surgery (LastPart.Fitted).</summary>
    public class LastPartDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => LastPart.Fitted(v, worker, on);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Health, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
