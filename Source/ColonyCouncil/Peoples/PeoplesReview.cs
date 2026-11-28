using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The review of the peoples' events (docs/review-peoples.md, Tools/peoples_impact_audit.py): the one door the peoples' jobs
    // go through for the colony's own people, and the pilgrim fellowship's footsore palmer, added where their set was thinnest.

    /// <summary>What the review of the peoples' events added to their code.</summary>
    public static class PeoplesReview
    {
        /// <summary>
        /// One held from the peoples' events' jobs: drafted, in a mental break, down, or on an order the player gave. Rounds 4 to
        /// 10's doors, the Choir's and the Vault's (PeoplesE.Start), the Circuit's bout, the broker's rescue and the tending of a
        /// visitor (OutsiderTend.Send) refuse them, and the scene waits or lets go as it does when someone is away. A visitor is
        /// never drafted and has no orders from the player, so the same check holds for theirs.
        /// </summary>
        public static bool Held(Pawn p) => p != null && (p.Drafted || p.InMentalState || p.Downed || (p.CurJob?.playerForced ?? false));
    }

    // ===================== The footsore palmer (the pilgrim fellowship) =====================

    /// <summary>
    /// A palmer of the pilgrim fellowship, on the long walk to one of their hostels, comes to the gate hurt by the road (blisters, a
    /// sprained ankle, sore knees: Rimfolk's own small ailments) and asks for the colony's doctor. {A} is the colony's best doctor,
    /// {X} the palmer, {TALE} what the road did, {PLACE} the hostel they walk to. The doctor sees to it at the gate with a medicine
    /// from the stores (OutsiderTend), or a medicine is carried out for the road, or the gate stays shut, and the palmer blesses it
    /// all the same, as the fellowship blesses every door. Only from a pilgrim people at peace with the colony; on foot in every age.
    /// </summary>
    public class HappeningWorker_PplR7Footsore : HappeningWorker_Visitors
    {
        /// <summary>What the road gives a palmer, the first of these that are in the game.</summary>
        private static readonly string[] Hurts = { "Council_Ail_Blisters", "Council_Ail_SprainedAnkle", "Council_Ail_SoreKnees" };

        /// <summary>The colony's best doctor at home, up and able to doctor.</summary>
        private static Pawn Doctor(Map map) =>
            PeoplesD.Able(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !PeoplesReview.Held(p))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();

        private static List<HediffDef> HurtDefs => Hurts.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Pilgrims) != null && HurtDefs.Count > 0 && Doctor(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            ctx.a = Doctor(ctx.map);
            if (ctx.faction == null || ctx.a == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Pilgrim"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.tale = Hurt(ctx.stranger);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return !ctx.tale.NullOrEmpty();
        }

        /// <summary>The road's hurt the palmer comes with (the event's own visitor, made for it), named; null when none could be given.</summary>
        private static string Hurt(Pawn p)
        {
            var def = HurtDefs.Where(d => !p.health.hediffSet.HasHediff(d)).RandomElementWithFallback();
            if (def == null) return null;
            try
            {
                var h = HediffMaker.MakeHediff(def, p);
                h.Severity = h.TryGetComp<HediffComp_Immunizable>() != null ? 0.3f : AilmentTracker.InfoOf(def)?.startSeverity.RandomInRange ?? 0.4f;
                p.health.AddHediff(h);
                return p.health.hediffSet.HasHediff(def) ? def.label : null;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("the footsore palmer's hurt", e);
                return null;
            }
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // The palmer's word at the gate: thanks for the care or the medicine, or the shut gate blessed all the same.
            PeoplesD.Say(ctx.stranger, Ext, ctx, option == 0 ? "tended" : option == 1 ? "herbs" : "shut");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
