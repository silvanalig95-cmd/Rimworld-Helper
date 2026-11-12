using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 2. Red ink (the road league) =====================

    /// <summary>
    /// Red ink (item 2): after the colony robbed or killed traders of any people (the wrong in the peoples' memory, "robbed",
    /// "caravanRaided", and a killing that came with them), the league's factor comes to the gate with the Waystone Book open
    /// at the colony's page. The colony pays the wronged people back (silver by the deed's size, carried out; the deed is
    /// settled and part of their goodwill returns), or sends its best talker to argue the entry (a Social roll on the card:
    /// struck out, or written twice), or refuses: every league trades with it at worse terms until it pays, and the factor
    /// comes back to ask again. The same book in every age.
    /// </summary>
    public static class RedInk
    {
        private static int Now => Find.TickManager.TicksGame;

        private static bool Wrong(FactionDeed d) => d != null && Array.IndexOf(PeopleEvents3Math.RedInkKinds, d.kind) >= 0;

        /// <summary>
        /// The colony's wrong against traders the league writes in red: an unsettled "robbed" or "caravanRaided" deed of any
        /// people the league isn't, within the book's reach in age (a "killed" deed that came with one stands for it when it is
        /// the bigger), not one the league has a page for already.
        /// </summary>
        public static FactionDeed Find(Faction league)
        {
            var mem = FactionMemory.Instance;
            var b = PeopleEvents3Book.Instance;
            if (mem == null || !FactionMemory.On) return null;
            int now = Now;
            FactionDeed best = null;
            // The crates held in trust broken (round 1) are the league's own red ink already, its season without caravans.
            string trust = "CC_PEv_TrustName".Translate().Resolve();
            foreach (var d in mem.deeds)
            {
                if (d?.faction == null || d.settled || d.faction == league || d.faction.defeated || !Wrong(d) || d.name == trust) continue;
                if (!PeopleEvents3Math.InBook((now - d.tick) / (float)GenDate.TicksPerDay)) continue;
                if (b != null && b.redInk.Any(r => r.deed == d.id)) continue;
                if (best == null || d.StrengthAt(now) > best.StrengthAt(now)) best = d;
            }
            if (best == null) return null;
            // The killing that came with the robbing is the bigger wrong: the page names it.
            var killed = mem.deeds.Where(k => k != null && k.faction == best.faction && !k.settled && k.kind == "killed" && PeopleEvents3Math.KilledInRobbing(k.tick, best.tick))
                .OrderByDescending(k => k.weight).FirstOrDefault();
            return killed != null && killed.weight > best.weight && (b == null || !b.redInk.Any(r => r.deed == killed.id)) ? killed : best;
        }

        /// <summary>The page the league keeps open for the colony (refused, or written twice), if any.</summary>
        public static RedInkRecord OpenPage => PeopleEvents3Book.Instance?.redInk.FirstOrDefault(r => r.Open);

        /// <summary>A page whose wrong was put right some other way (the deed settled or forgotten, the people gone): closed.</summary>
        public static void Tidy()
        {
            var b = PeopleEvents3Book.Instance;
            var mem = FactionMemory.Instance;
            if (b == null) return;
            foreach (var r in b.redInk.Where(r => !r.done))
            {
                var d = mem?.ById(r.deed);
                if (r.wronged == null || r.wronged.defeated || d == null || d.settled)
                {
                    r.done = true;
                    r.state = "lapsed";
                }
            }
        }

        /// <summary>
        /// Worse terms with every league while a page stands open (on the Waystone Book's own price patch, round 1's good ink:
        /// PeopleTrust.Terms).
        /// </summary>
        public static float Terms(Faction f)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || f == null || f.IsPlayer || Peoples.CallingOf(f) != PeoplesMath.Traders) return 0f;
            for (int i = 0; i < b.redInk.Count; i++)
                if (b.redInk[i] != null && b.redInk[i].Open)
                    return PeopleEvents3Math.WorseTerms;
            return 0f;
        }

        /// <summary>The page for this answer: the one open, or a new one for the wrong the factor came about.</summary>
        public static RedInkRecord PageFor(HappeningContext ctx)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || ctx == null) return null;
            int.TryParse(ctx.note ?? "", out int deed);
            var r = b.redInk.FirstOrDefault(x => !x.done && x.deed == deed);
            if (r != null) return r;
            var d = FactionMemory.Instance?.ById(deed);
            return b.AddRedInk(new RedInkRecord { league = ctx.faction, wronged = ctx.enemy, deed = deed, kind = d?.kind, owed = ctx.count, since = Now });
        }
    }

    /// <summary>
    /// The league's factor at the gate ({X}, of {FACTION}), the Waystone Book open at the colony's page: the colony {TALE}
    /// ({ENEMY}'s), written in red; {COUNT} silver pays them back. {A} is the colony's best talker. Again, while a page stands
    /// open: the factor back to ask for what is owed.
    /// </summary>
    public class HappeningWorker_RedInk : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Traders);
            if (f == null || Colonists(map).Count == 0) return false;
            RedInk.Tidy();
            return RedInk.OpenPage != null || RedInk.Find(f) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Traders);
            if (ctx.faction == null) return false;
            var open = RedInk.OpenPage;
            var mem = FactionMemory.Instance;
            if (open != null)
            {
                var d = mem?.ById(open.deed);
                if (d == null) return false;
                ctx.enemy = open.wronged;
                ctx.count = open.owed;
                ctx.note = open.deed.ToString();
                ctx.tale = FactionMemory.Text(d);
                ctx.variant = open.state == "twice" ? 2 : 1;
            }
            else
            {
                var d = RedInk.Find(ctx.faction);
                if (d == null) return false;
                ctx.enemy = d.faction;
                ctx.count = PeopleEvents3Math.Restitution(d.weight, d.count, LivingEvents.WealthFactor(ctx.map));
                ctx.note = d.id.ToString();
                ctx.tale = FactionMemory.Text(d);
                ctx.variant = 0;
            }
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Factor"), ctx.faction, ext.ages);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null && ctx.enemy != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant == 0 ? "first" : ctx.variant == 2 ? "twice" : "again");
            if (ctx.enemy != null && ctx.enemy.HostileTo(Faction.OfPlayer)) tags.Add("war");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            // The entry is argued once: written twice, it is not argued again.
            if (option == 1 && ctx.variant == 2) return "CC_PEv3_ArguedOnce".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "paid" : option == 1 ? (risk ? "twice" : "struck") : "red");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Paid back: the silver goes to the wronged people by the league's hand (carried out to the factor), their wrong settled, part of their goodwill back.</summary>
    public class DilemmaEffect_RedInkPay : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            var d = FactionMemory.Instance?.ById(int.TryParse(hc?.note ?? "", out int id) ? id : -1);
            if (hc?.enemy == null || d == null) return;
            sheet.Goodwill(hc.enemy, PeopleEvents3Math.GoodwillBack(d.weight));
            sheet.Add(ImpactKind.Other, 1, "CC_PEv3_Impact_Settled".Translate(hc.enemy.Name).Resolve(), "CC_PEv3_Impact_SettledBrief".Translate().Resolve(), "pev3settled");
            if (hc.variant > 0) sheet.Add(ImpactKind.Goods, 1, "CC_PEv3_Impact_TermsBack".Translate().Resolve(), "CC_PEv3_Impact_TermsBackBrief".Translate().Resolve(), "pev3termsback");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var r = RedInk.PageFor(hc);
            var mem = FactionMemory.Instance;
            if (r == null || mem == null) return;
            var d = mem.ById(r.deed);
            if (d != null)
            {
                FactionMemory.Settle(d);
                if (r.wronged != null && r.wronged.HasGoodwill && !r.wronged.IsPlayer) WorldSituationWorker.Goodwill(r.wronged, PeopleEvents3Math.GoodwillBack(d.weight), false);
            }
            r.state = "paid";
            r.done = true;
            if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents3Math.PaidGoodwill, false);
        }
    }

    /// <summary>The talker argued it and won: the entry struck out, no worse terms (the wronged people's own memory is theirs).</summary>
    public class DilemmaEffect_RedInkStruck : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Add(ImpactKind.Other, 1, "CC_PEv3_Impact_Struck".Translate(hc.a.LabelShort).Resolve(), "CC_PEv3_Impact_StruckBrief".Translate().Resolve(), "pev3struck");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = RedInk.PageFor(ctx as HappeningContext);
            if (r == null) return;
            r.state = "struck";
            r.done = true;
        }
    }

    /// <summary>The talker argued it and lost: written twice, the sum owed doubled and worse terms with every league until it is paid.</summary>
    public class DilemmaEffect_RedInkTwice : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv3_Impact_Twice".Translate(PeopleEvents3Math.Twice(hc.count)).Resolve(), "CC_PEv3_Impact_TwiceBrief".Translate().Resolve(), "pev3twice");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = RedInk.PageFor(ctx as HappeningContext);
            if (r == null) return;
            r.owed = PeopleEvents3Math.Twice(r.owed);
            r.state = "twice";
        }
    }

    /// <summary>Refused (or left unanswered): the page stays red, every league trades at worse terms until the colony pays.</summary>
    public class DilemmaEffect_RedInkRefuse : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Goods, -1, "CC_PEv3_Impact_Worse".Translate(Mathf.RoundToInt(-PeopleEvents3Math.WorseTerms * 100f)).Resolve(), "CC_PEv3_Impact_WorseBrief".Translate().Resolve(), "pev3worse");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = RedInk.PageFor(ctx as HappeningContext);
            if (r == null || r.state == "twice") return;
            r.state = "refused";
        }
    }
}
