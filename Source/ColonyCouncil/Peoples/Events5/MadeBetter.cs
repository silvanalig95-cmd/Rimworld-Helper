using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 44. Made better (the Wheel) =====================

    /// <summary>
    /// Made better (item 44, a gift): a fixer of the Wheel takes one colonist's own weapon or armour (what they carry or wear,
    /// below masterwork) out to their cart beyond the map's edge for a day. The colonist hands it over at the gate (round 1's
    /// visitor who works on a colonist: Peoples/Events/PeopleWorkOn.cs), the fixer walks off with it, and a day later walks
    /// back in and hands it back: a quality higher (masterwork at most) and fully mended. Paid in silver and steel, or in
    /// silver alone. Should the fixer not come back (dead, or their people at war), a runner of the Wheel brings it to the
    /// gate made better all the same.
    /// </summary>
    public static class MadeBetter
    {
        public const string GiftKind = "better";
        public const string EventDef = "Rimfolk_Evt_MadeBetter";

        public static JobDef TakeJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_HandOver");
        public static JobDef BackJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_HandBack");

        private static int Now => Find.TickManager.TicksGame;

        public static int QualityOf(Thing t) => t != null && t.TryGetQuality(out var q) ? (int)q : -1;

        /// <summary>A piece worth the cart: one with a quality below masterwork; a weapon, or clothing that guards against a blade.</summary>
        public static bool Fits(Thing t)
        {
            if (t == null || t.Destroyed || !PeopleEvents5Math.Betterable(QualityOf(t))) return false;
            if (t.def.IsWeapon) return true;
            return t is Apparel a && a.GetStatValue(StatDefOf.ArmorRating_Sharp) >= PeopleEvents5Math.MinArmour;
        }

        /// <summary>The colonist's own pieces that fit, the most worth first.</summary>
        public static List<Thing> Pieces(Pawn p)
        {
            var list = new List<Thing>();
            if (p?.equipment?.Primary != null && Fits(p.equipment.Primary)) list.Add(p.equipment.Primary);
            if (p?.apparel != null) list.AddRange(p.apparel.WornApparel.Where(a => !p.apparel.IsLocked(a) && Fits(a)));
            return list.OrderByDescending(t => t.MarketValue).ToList();
        }

        /// <summary>Whose piece: a grown colonist up and about with one that fits, who has not had a gift of this kind; the one whose piece is worth the most.</summary>
        public static Pawn Owner(Map map, out Thing piece)
        {
            piece = null;
            var p = PeoplesG.Grown(map).Where(x => PeopleEvents.MayGive(GiftKind, x) && Pieces(x).Count > 0).OrderByDescending(x => Pieces(x)[0].MarketValue).FirstOrDefault();
            if (p != null) piece = Pieces(p)[0];
            return p;
        }

        /// <summary>The piece handed over (the fixer at the colonist's side): out of their hands into the fixer's pack, and the fixer off to their cart.</summary>
        public static void Taken(Pawn fixer, Pawn owner, string recordId)
        {
            var b = PeopleEvents5Book.Instance;
            var r = b?.betters.FirstOrDefault(x => x != null && !x.done && x.id.ToString() == recordId);
            if (r == null || fixer == null || owner == null || r.item == null || r.item.Destroyed) return;
            // together: the fixer is at the colonist's side at the gate (PeopleWorkOn brought them there).
            // Off every map while it is at their cart: kept by the record (PeopleEvents5Book.held), so a fixer lost on the
            // road never takes it with them.
            try
            {
                var cart = b.held;
                if (cart == null) return;
                if (r.item is ThingWithComps eq && owner.equipment?.Primary == eq) owner.equipment.TryTransferEquipmentToContainer(eq, cart);
                else if (r.item is Apparel ap && owner.apparel != null && owner.apparel.Wearing(ap))
                {
                    owner.apparel.Remove(ap);
                    if (!cart.TryAdd(ap)) GenPlace.TryPlaceThing(ap, owner.Position, owner.Map, ThingPlaceMode.Near);
                }
                else return;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the piece handed over", e);
                return;
            }
            r.stage = "away";
            r.due = Now + PeopleEvents5Math.BetterHours * GenDate.TicksPerHour;
            PeoplesG.SayLine(fixer, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "taken");
            Tell.Message(NewsKind.People, Importance.Notable, owner.Map, "CC_PEv5_BetterTaken".Translate(owner.Named("PAWN"), fixer.Named("FIXER"), r.item.LabelNoCount.Named("THING")).Resolve(),
                new LookTargets(fixer), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The piece made better: a quality higher (masterwork at most), and whole.</summary>
        public static void Improve(Thing t)
        {
            if (t == null || t.Destroyed) return;
            var comp = t.TryGetComp<CompQuality>();
            int q = QualityOf(t);
            if (comp != null && q >= 0) comp.SetQuality((QualityCategory)PeopleEvents5Math.Better(q), ArtGenerationContext.Outsider);
            if (t.def.useHitPoints) t.HitPoints = t.MaxHitPoints;
        }

        /// <summary>The piece handed back (the fixer at the colonist's side again): made better, worn or carried as before.</summary>
        public static void Back(Pawn fixer, Pawn owner, string recordId)
        {
            var r = PeopleEvents5Book.Instance?.betters.FirstOrDefault(x => x != null && !x.done && x.id.ToString() == recordId);
            if (r == null || r.item == null || r.item.Destroyed) return;
            Improve(r.item);
            r.done = true;
            r.stage = "done";
            var t = r.item;
            // together: the fixer is back at the colonist's side (PeopleWorkOn brought them there).
            bool worn = false;
            try
            {
                t.holdingOwner?.Remove(t);
                if (owner != null && owner.Spawned && !owner.Dead)
                {
                    if (t is Apparel ap && owner.apparel != null && ApparelUtility.HasPartsToWear(owner, ap.def))
                    {
                        owner.apparel.Wear(ap, true, false);
                        worn = true;
                    }
                    else if (t is ThingWithComps eq && t.def.IsWeapon && owner.equipment != null && owner.equipment.Primary == null)
                    {
                        owner.equipment.AddEquipment(eq);
                        worn = true;
                    }
                }
                if (!worn) GenPlace.TryPlaceThing(t, owner != null && owner.Spawned ? owner.Position : fixer.Position, fixer.Map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the piece handed back", e);
                if (!t.Spawned && t.holdingOwner == null && fixer?.Map != null) GenPlace.TryPlaceThing(t, fixer.Position, fixer.Map, ThingPlaceMode.Near);
            }
            PeopleEventsBook.Gave(GiftKind, owner);
            var f = fixer?.Faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.BetterGoodwill, GoodwillWhy.Helped);
            if (fixer != null) PeoplesG.SayLine(fixer, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "back");
            Tell.Message(NewsKind.People, Importance.Notable, fixer?.Map ?? owner?.MapHeld,
                "CC_PEv5_BetterBack".Translate((owner?.LabelShort ?? "?").Named("PAWN"), t.LabelNoCount.Named("THING"), (fixer?.LabelShort ?? "?").Named("FIXER")).Resolve(),
                t.Spawned ? new LookTargets(t) : owner != null && owner.Spawned ? new LookTargets(owner) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// The events' hour: the fixer off the map with the piece kept in the world; a day on, back in from the edge to hand it
        /// over; should they not be able to (dead, gone, their people at war), the piece comes back by a runner, made better.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null) return;
            foreach (var r in b.betters.Where(x => x != null && !x.done).ToList())
            {
                if (r.item == null || r.item.Destroyed)
                {
                    r.done = true;
                    continue;
                }
                switch (r.stage)
                {
                    case "taking":
                        // The hand-over never happened (given up): the piece is still the colonist's.
                        if (r.item.ParentHolder is Pawn_EquipmentTracker || r.item.ParentHolder is Pawn_ApparelTracker)
                        {
                            if (now - r.since > 12 * GenDate.TicksPerHour) r.done = true;
                            continue;
                        }
                        break;
                    case "away":
                        if (r.fixer != null && !r.fixer.Dead && !r.fixer.Spawned) Arrivals.Hold(r.fixer);
                        if (now < r.due) continue;
                        if (r.fixer != null && r.fixer.Spawned) continue;
                        Return(r);
                        continue;
                    case "back":
                        // Back on the map: if the hand-back was given up, or they left again, the piece set down at the gate.
                        if (now - r.due > 12 * GenDate.TicksPerHour) ByRunner(r);
                        continue;
                }
            }
        }

        /// <summary>The fixer back in from the edge to the colonist, the hand-back begun; else the piece by a runner.</summary>
        private static void Return(BetterRecord r)
        {
            var map = r.owner?.MapHeld ?? r.map;
            var fixer = r.fixer;
            var job = BackJob;
            bool can = map != null && Homes.IsHome(map) && fixer != null && !fixer.Dead && !fixer.Destroyed && !fixer.Discarded && r.faction != null && !r.faction.HostileTo(Faction.OfPlayer) && job != null
                       && r.owner != null && r.owner.Spawned && r.owner.Map == map && !r.owner.Dead && !MapFacts.ThreatOn(map);
            if (!can)
            {
                ByRunner(r);
                return;
            }
            if (!Arrivals.Spawn(fixer, map))
            {
                ByRunner(r);
                return;
            }
            r.stage = "back";
            var ctx = new HappeningContext { incident = DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), map = map, faction = r.faction, stranger = fixer, a = r.owner };
            if (!PeopleWorkOn.Begin(ctx, fixer, r.owner, job, LocalTargetInfo.Invalid, r.id.ToString(), 0, PeopleEvents5Math.HandTicks, 8f)) Back(fixer, r.owner, r.id.ToString());
        }

        /// <summary>A runner of the Wheel brings the piece to the gate, made better.</summary>
        private static void ByRunner(BetterRecord r)
        {
            var map = r.owner?.MapHeld ?? r.map;
            if (map == null || !Homes.IsHome(map)) map = Homes.Seat;
            r.done = true;
            r.stage = "done";
            var t = r.item;
            if (t == null || t.Destroyed || map == null) return;
            Improve(t);
            try
            {
                if (t.Spawned) t.DeSpawn();
                t.holdingOwner?.Remove(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the piece by a runner", e);
            }
            if (r.fixer != null && !r.fixer.Dead && !r.fixer.Spawned) Arrivals.Release(r.fixer);
            Couriers.Loot(new List<Thing> { t }, map, PeoplesD.GateSpot(map) ?? map.Center, r.faction, null);
            PeopleEventsBook.Gave(GiftKind, r.owner);
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv5_BetterByRunner".Translate(t.LabelNoCount.Named("THING"), (r.owner?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Dev mode: the fixer comes back now.</summary>
        public static bool Hasten()
        {
            var r = PeopleEvents5Book.Instance?.betters.FirstOrDefault(x => x != null && !x.done && x.stage == "away");
            if (r == null) return false;
            r.due = Now;
            return true;
        }
    }

    /// <summary>
    /// A fixer of {FACTION} ({X}) at the gate: they would take {A}'s {THING_label} out to their cart for a day and bring it back
    /// a quality better and whole ({COUNT} silver and {DEAD} steel, or {TALE} silver alone).
    /// </summary>
    public class HappeningWorker_MadeBetter : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Salvagers) != null && PeopleEventsBook.GiftOpen && MadeBetter.Owner(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Salvagers);
            ctx.a = MadeBetter.Owner(ctx.map, out var piece);
            if (ctx.faction == null || ctx.a == null || piece == null) return false;
            ctx.place = piece;
            ctx.thing = piece.def;
            ctx.beast = piece.LabelNoCount;
            float worth = piece.MarketValue;
            ctx.count = PeopleEvents5Math.BetterSilver(worth);
            ctx.variant = PeopleEvents5Math.BetterSteel(worth);
            ctx.dead = ctx.variant.ToString();
            ctx.tale = PeopleEvents5Math.BetterSilverOnly(worth).ToString();
            ctx.note = PeopleEvents5Math.BetterSilverOnly(worth).ToString();
            var next = (QualityCategory)PeopleEvents5Math.Better(MadeBetter.QualityOf(piece));
            ctx.relation = next.GetLabel();
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Crafting, new IntRange(12, 18));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.thing != null && ctx.thing.IsWeapon ? "weapon" : "armour");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv5_OwnerGone".Translate().Resolve();
            var piece = ctx.place;
            if (piece == null || piece.Destroyed || !(piece.ParentHolder is Pawn_EquipmentTracker || piece.ParentHolder is Pawn_ApparelTracker)) return "CC_PEv5_PieceGone".Translate(ctx.beast ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "cart" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The piece's hand-over at the gate (PeopleWorkOn, Rimfolk_PEv5_HandOver), the record of its day at the cart.</summary>
    public class DilemmaEffect_MadeBetter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.place == null) return;
            sheet.Play("CC_PEv5_Impact_Better".Translate(hc.a.LabelShort, hc.beast ?? "?", hc.relation ?? "?").Resolve(), "CC_PEv5_Impact_BetterBrief".Translate().Resolve());
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents5Math.BetterGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var b = PeopleEvents5Book.Instance;
            if (hc?.a == null || hc.place == null || b == null || hc.stranger == null) return;
            var r = b.AddBetter(new BetterRecord { faction = hc.faction, map = hc.map, fixer = hc.stranger, owner = hc.a, item = hc.place, quality = MadeBetter.QualityOf(hc.place), since = Find.TickManager.TicksGame });
            var job = MadeBetter.TakeJob;
            if (job == null || !PeopleWorkOn.Begin(hc, hc.stranger, hc.a, job, LocalTargetInfo.Invalid, r.id.ToString(), 0, PeopleEvents5Math.HandTicks, 6f))
            {
                // "Answers play out on the map" off: taken now, and back in a day as ever.
                MadeBetter.Taken(hc.stranger, hc.a, r.id.ToString());
                Arrivals.SendAway(PeoplesG.Party(hc));
            }
        }
    }

    /// <summary>The piece handed over at the gate (MadeBetter.Taken); then the fixer walks off to their cart.</summary>
    public class MadeBetterTakenDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => MadeBetter.Taken(worker, on, PeopleWorkOn.Note(v));

        public override void GaveUp(Visit v)
        {
            var r = PeopleEvents5Book.Instance?.betters.FirstOrDefault(x => x != null && !x.done && x.id.ToString() == PeopleWorkOn.Note(v));
            if (r != null && r.stage == "taking") r.done = true;
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>The piece handed back made better (MadeBetter.Back); given up, it comes by a runner.</summary>
    public class MadeBetterBackDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => MadeBetter.Back(worker, on, PeopleWorkOn.Note(v));

        public override void GaveUp(Visit v)
        {
            var r = PeopleEvents5Book.Instance?.betters.FirstOrDefault(x => x != null && !x.done && x.id.ToString() == PeopleWorkOn.Note(v));
            if (r != null) r.due = Find.TickManager.TicksGame - 13 * GenDate.TicksPerHour;
        }
    }
}
