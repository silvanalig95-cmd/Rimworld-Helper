using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The seven peoples brought to the level of the new ones (docs/design/peoples.md, "Their stories, sayings and
    // touchpoints"; docs/design/peoples-next.md): what they honour, fear and say on their card, and their own touchpoints at
    // the colony's gate (Defs/IncidentDefs/Rimfolk_Events_PeoplesLore.xml), each only from a people of its calling at peace
    // with the colony. The league's winter quarters and the herders' young beast at the gate are camps
    // (Defs/CampDefs/Rimfolk_CampKinds_PeoplesLore.xml, Rimfolk_CampKinds_Peoples.xml); the society's disputation is told
    // from data alone. Nothing here is saved and nothing runs on a clock of its own.

    // ===================== Their card =====================

    /// <summary>
    /// On a people's card, for one of Rimfolk's peoples (or one that arose in its ways): what they honour and fear and one
    /// of their sayings, from their template (PeopleExtension.honours, fears, sayings).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PeopleLore
    {
        static PeopleLore()
        {
            CardSections.RegisterLines(CardKind.People, CardSlot.Story, 10f, r => Lines(ThingRefs.Faction(r)), null, "people-lore");
        }

        /// <summary>Their honour and fear, and a saying (the same one each time for a people), or null for any other people.</summary>
        public static List<string> Lines(Faction f)
        {
            var ext = Peoples.Of(f);
            if (ext == null) return null;
            try
            {
                var lines = new List<string>();
                string form = PeoplesLoreMath.CardForm(ext.honours, ext.fears);
                if (form != null)
                    lines.Add(("CC_PeoplesLore_Card_" + form).Translate((ext.honours ?? "").Trim().Named("HONOURS"), (ext.fears ?? "").Trim().Named("FEARS")).Resolve());
                string saying = PeoplesLoreMath.Saying(ext.sayings, f.loadID);
                if (!saying.NullOrEmpty()) lines.Add("CC_PeoplesLore_Card_Saying".Translate(saying.Named("SAYING")).Resolve());
                return lines.Count > 0 ? lines : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: their card", e);
                return null;
            }
        }
    }

    // ===================== The order's night nurse =====================

    /// <summary>
    /// A colonist is down with a fever (an illness the body has to fight off: the game's flu, plague, malaria, an
    /// infection), and the hospitaller order, which keeps its lamp lit for whoever is sick, sends one of its hospitallers to
    /// the gate. Let in, the hospitaller goes from bed to bed seeing to every fevered colonist (four at most, the sickest
    /// first) with the order's care, which helps the body fight it off (Rimfolk_OrderNursed), and stays the night; the
    /// colony's doctor may work at the hospitaller's elbow and send medicine back for the sickhouse. {TALE} is the sickest
    /// one's illness. The rounds go by the visitors' service (VisitorServices.Dose), so the hospitaller is seen at each
    /// bedside, and the answer's words are told when the last has been seen to.
    /// </summary>
    public class HappeningWorker_OrderNurse : HappeningWorker_Visitors
    {
        private static bool Theirs(Faction f) => Peoples.CallingOf(f) == PeoplesMath.Healers && Arrivals.Usable(f);

        /// <summary>A colonist's fever, or null: an illness with an immunity still to win, not one of Rimfolk's everyday ailments.</summary>
        public static Hediff Fever(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            Hediff best = null;
            foreach (var h in hediffs)
            {
                if (h?.def == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                bool immunizable = h is HediffWithComps hc && hc.TryGetComp<HediffComp_Immunizable>() != null;
                if (!PeoplesLoreMath.Fever(immunizable, h.FullyImmune(), info != null && !info.serious)) continue;
                if (best == null || h.Severity > best.Severity) best = h;
            }
            return best;
        }

        /// <summary>The colony's fevered at home, the sickest first.</summary>
        public static List<Pawn> Fevered(Map map) =>
            map == null ? new List<Pawn>() : Colonists(map).Where(p => Fever(p) != null).OrderByDescending(p => Fever(p).Severity).ToList();

        /// <summary>A kind of theirs who heals (the hospitaller), else their ordinary member.</summary>
        public static PawnKindDef HealerKind(Faction f) =>
            f?.def?.pawnGroupMakers?.Where(m => m?.options != null).SelectMany(m => m.options).Select(o => o.kind)
                .FirstOrDefault(k => k?.RaceProps?.Humanlike == true && k.skills != null && k.skills.Any(s => s?.Skill == SkillDefOf.Medicine))
            ?? Arrivals.PersonKind(f);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Arrivals.Friendly(Theirs) != null && Fevered(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(Theirs);
            var fevered = Fevered(ctx.map);
            if (ctx.faction == null || fevered.Count == 0) return false;
            var kind = HealerKind(ctx.faction);
            if (kind == null) return false;
            ctx.stranger = Arrivals.Make(kind, ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(10, 16));
            ctx.a = fevered[0];
            ctx.count = PeoplesLoreMath.NurseCount(fevered.Count);
            ctx.tale = Fever(ctx.a)?.LabelBase ?? "";
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.count > 1) tags.Add("many");
            var fever = Fever(ctx.a);
            if (fever?.def != null) tags.Add("fever:" + fever.def.defName);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var hediff = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_OrderNursed");
            if (option > 1 || ctx.map == null || hediff == null) return base.Resolved(ctx, option, risk, byDefault, outcome);
            // The hospitaller goes from bed to bed, the sickest first, and the answer's words are told when the last has
            // been seen to; with the setting off, or the hospitaller gone, everyone has the order's care at once.
            var who = Fevered(ctx.map).Where(p => p.health?.hediffSet?.HasHediff(hediff) == false).Take(PeoplesLoreMath.NurseMax).ToList();
            outcome = base.Resolved(ctx, option, risk, byDefault, outcome);
            var x = ctx.stranger;
            string line = VarietyUtil.Line(Ext?.lines, ctx, new[] { "nurse" });
            if (x != null && x.Spawned && !line.NullOrEmpty()) Speech.Say(x, line);
            if (who.Count > 0 && VisitorServices.Dose(ctx.map, x, who, hediff, ctx.Format(outcome))) return "";
            foreach (var p in who) p.health.AddHediff(hediff);
            return outcome;
        }
    }

    // ===================== The pilgrims' Long Walk =====================

    /// <summary>
    /// On the fellowship's walking day a long file of its pilgrims comes along the road by the colony, singing, and stops to
    /// pray at the colony's graves (or at its gate, where it has none) before going on. The colony may walk out with them,
    /// give them bread for the road, or watch them pass. The eldest says a word over the place as the colony answers.
    /// </summary>
    public class HappeningWorker_LongWalk : HappeningWorker_Setting
    {
        /// <summary>{COUNT}: how many walk in the file (the eldest and those with them).</summary>
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext) || ctx.stranger == null) return false;
            ctx.count = 1 + ctx.group.Count(p => p != null);
            return ctx.count >= PeoplesLoreMath.WalkMin;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            string line = VarietyUtil.Line(Ext?.lines, ctx, new[] { "walk" });
            if (x != null && x.Spawned && !x.Dead && !line.NullOrEmpty()) Speech.Say(x, line);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The river folk's nets =====================

    /// <summary>
    /// Boatmen of the river folk ask to spread their nets to dry and mend them on the colony's bank for the day. They stay by
    /// the water (the dry bank nearest the colony's middle): {THING} is what they leave in thanks, fish of the colony's own
    /// waters with Odyssey, else net cloth.
    /// </summary>
    public class HappeningWorker_RiverNets : HappeningWorker_Setting
    {
        /// <summary>Dry, open ground beside the water nearest the colony's middle, or an invalid cell where there is no water in reach.</summary>
        public static IntVec3 Bank(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 50f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.Fogged(map) || (c.GetTerrain(map)?.IsWater ?? true)) continue;
                if (c.GetEdifice(map) != null || (c.GetRoom(map)?.IsPrisonCell ?? false)) continue;
                foreach (var n in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One))
                    if (n.InBounds(map) && (n.GetTerrain(map)?.IsWater ?? false))
                        return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>What they leave in thanks: fish of the colony's waters (Odyssey), else net cloth.</summary>
        public static ThingDef Thanks(Map map) => HappeningWorker_RiverCatch.Fish(map) ?? ThingDefOf.Cloth;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Bank(map).IsValid && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.thing = Thanks(ctx.map);
            ctx.spot = Bank(ctx.map);
            return ctx.thing != null && ctx.spot.IsValid && ctx.count > 0;
        }

        /// <summary>They walk in and settle on the bank for the day.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var people = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
            return people.Count > 0 && Arrivals.Visit(people, ctx.map, ctx.faction, ctx.spot, ext.stayHours.RandomInRange) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.thing != null && ctx.thing != ThingDefOf.Cloth) tags.Add("fish");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            string line = VarietyUtil.Line(Ext?.lines, ctx, new[] { option == 2 ? "refused" : "nets" });
            if (x != null && x.Spawned && !x.Dead && !line.NullOrEmpty()) Speech.Say(x, line);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The enclave's salvage =====================

    /// <summary>
    /// The spacer enclave has goods from above it won't lift back to orbit (steel, components, medicine; plasteel and
    /// chemfuel for a later colony), and its factor offers them to the colony for a share of their worth ({COUNT} silver,
    /// by the colony's wealth). Word comes by the colony's radio or comms for an industrial or spacer colony, and by one of
    /// their runners walking up to the gate for a tribal or medieval one. Bought, the salvage comes down by drop pod by the
    /// colony's trade drop spot (DilemmaEffect_EnclaveSalvage), and the silver goes to the runner or to a courier of theirs.
    /// </summary>
    public class HappeningWorker_EnclaveSalvage : HappeningWorker_Visitors
    {
        private static bool Theirs(Faction f) => Peoples.CallingOf(f) == PeoplesMath.Brokers && Arrivals.Usable(f);

        /// <summary>A runner walks up to the gate with the word: a colony with no radio to call.</summary>
        private static bool OnFoot => !PeoplesMath.LanderAge((int)Ages.Ours);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            map.IsPlayerHome && !MapFacts.InVacuum(map) && map.dangerWatcher.DangerRating == StoryDanger.None && ThingDefOf.Steel != null
            && Arrivals.Friendly(Theirs) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly(Theirs);
            if (ctx.faction == null) return false;
            // The one who answers: the colony's best talker.
            ctx.a = Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.count = PeoplesLoreMath.Price(PeoplesLoreMath.Worth(LivingEvents.WealthFactor(map)));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            if (OnFoot)
            {
                MakeGroup(ctx, ext, 1, ext.ages, null, false);
                if (ctx.stranger == null) return false;
            }
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            ctx.stranger == null || Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.map != null && ctx.map.listerBuildings.ColonistsHaveBuildingWithPowerOn(ThingDefOf.OrbitalTradeBeacon)) tags.Add("beacon");
            if (ctx.stranger != null) tags.Add("runner");
        }
    }

    /// <summary>
    /// The enclave's salvage comes down by drop pod by the colony's trade drop spot: what the colony's age can use, worth
    /// two and a half times the price paid ({COUNT}).
    /// </summary>
    public class DilemmaEffect_EnclaveSalvage : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The salvage of this price, as things the game has, by the colony's age.</summary>
        public static List<Thing> Make(int price)
        {
            float worth = price / PeoplesLoreMath.SalvagePriceShare;
            var things = new List<Thing>();
            foreach (var (name, share) in PeoplesLoreMath.SalvageGoods((int)Ages.Ours))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null || def.MadeFromStuff) continue;
                int n = PeoplesLoreMath.Count(worth, share, def.BaseMarketValue);
                while (n > 0)
                {
                    var t = ThingMaker.MakeThing(def);
                    t.stackCount = Mathf.Min(n, def.stackLimit);
                    n -= t.stackCount;
                    things.Add(t);
                }
            }
            return things;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            return f == null || f.defeated || f.HostileTo(Faction.OfPlayer) ? "CC_PeoplesLore_SalvageGone".Translate().Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || ctx.count <= 0) return;
            sheet.Play("CC_PeoplesLore_Impact_Salvage".Translate(ctx.faction?.Name ?? "?").Resolve(), "CC_PeoplesLore_Impact_SalvageBrief".Translate().Resolve());
            float worth = ctx.count / PeoplesLoreMath.SalvagePriceShare;
            foreach (var (name, share) in PeoplesLoreMath.SalvageGoods((int)Ages.Ours))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null || def.MadeFromStuff) continue;
                int n = PeoplesLoreMath.Count(worth, share, def.BaseMarketValue);
                if (n > 0) sheet.Goods(1, def, n);
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.faction == null || ctx.count <= 0) return;
            try
            {
                var things = Make(ctx.count);
                if (things.Count == 0) return;
                var spot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(spot, map, things, 110, false, false, false, false);
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PeoplesLore_SalvageFalls".Translate(ctx.faction.Name).Resolve(), new LookTargets(spot, map),
                    MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the enclave's salvage", e);
            }
        }
    }
}
