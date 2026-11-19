using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 73. A fugitive of the Republic =====================

    /// <summary>
    /// A fugitive of the Republic (item 73): a deserter of the Last Republic's Civic Guard comes to the gate and asks to join the
    /// colony, and a detail of the Guard (two or three) follows close behind. The colony may pay the deserter's fine to the detail
    /// (the deserter joins, the detail goes, the Republic a little warmer); shelter them (they join, the Republic is angry and strikes
    /// the colony's citizenship papers if it holds them, and should that turn it against the colony the detail at the gate fights,
    /// as the game's own raiders do); or hand them over (the detail leads them away, and the Republic thanks the colony).
    /// </summary>
    public static class RepublicFugitive
    {
        public const string EventDef = "Rimfolk_Evt_RepublicFugitive";

        /// <summary>The detail's leader: the first of them still up, who speaks for them.</summary>
        public static Pawn Leader(HappeningContext ctx) => ctx?.group.FirstOrDefault(p => p != null && p.Spawned && !p.Dead && !p.Downed);

        /// <summary>The deserter and the detail walk off together, the deserter in their keeping.</summary>
        public static void LedAway(HappeningContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map;
            var f = ctx?.faction;
            var all = ctx == null ? new List<Pawn>() : ctx.group.Prepend(x).Where(p => p != null && p.Spawned && !p.Dead && p.Map == map).ToList();
            if (map == null || f == null || all.Count == 0) return;
            try
            {
                Lords.Leave(all);
                LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, true), map, all);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the deserter led away", e);
                Arrivals.SendAway(all);
            }
        }

        /// <summary>The detail turns on the colony, the Republic being at war with it now: the game's own assault, told.</summary>
        public static void Fight(HappeningContext ctx)
        {
            var map = ctx?.map;
            var f = ctx?.faction;
            var detail = ctx?.group.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList() ?? new List<Pawn>();
            if (map == null || f == null || detail.Count == 0) return;
            try
            {
                Lords.Leave(detail);
                LordMaker.MakeNewLord(f, new LordJob_AssaultColony(f, false, true, false, false, false), map, detail);
                Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PEv8_DetailFightsLabel".Translate().Resolve(), "CC_PEv8_DetailFights".Translate(f.Name.Named("FACTION")).Resolve(),
                    LetterDefOf.ThreatSmall, new LookTargets(detail), f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the detail fights", e);
            }
        }
    }

    /// <summary>
    /// A deserter of {FACTION}'s Civic Guard ({X}) at the gate asking to join, and a detail of the Guard close behind: the deserter's
    /// fine is {COUNT} silver.
    /// </summary>
    public class HappeningWorker_RepublicFugitive : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Citizens) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Citizens);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = Rand.RangeInclusive(PeopleEvents8Math.DetailMin, PeopleEvents8Math.DetailMax);
            for (int i = 0; i < n; i++)
            {
                var g = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Republic_Guard"), ctx.faction, new FloatRange(24f, 50f));
                if (g != null) ctx.group.Add(g);
            }
            if (ctx.group.Count == 0) return false;
            ctx.count = PeopleEvents8Math.Fine(LivingEvents.WealthFactor(ctx.map));
            ctx.variant = ctx.group.Count;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var gate = PeoplesD.GateSpot(ctx.map);
            float hours = ext.stayHours.RandomInRange;
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, gate, hours) == null) return false;
            // The detail close behind: their own group, at the gate too.
            if (Arrivals.Visit(ctx.group.ToList(), ctx.map, ctx.faction, gate, hours) == null) ctx.group.Clear();
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (Papers.Holds(ctx.faction)) tags.Add("papers");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Map != ctx.map) return "CC_Stranger_Gone".Translate(x?.LabelShort ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "fine" : option == 1 ? "shelter" : "handover");
            var lead = RepublicFugitive.Leader(ctx);
            if (lead != null) PeoplesG.Say(lead, Ext, ctx, option == 0 ? "paid" : option == 1 ? "angry" : "taken");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The fine paid to the detail (carried out by the answer's own carry-out): the deserter joins, the detail goes.</summary>
    public class DilemmaEffect_FugitiveFine : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_Impact_Join".Translate(hc.stranger.LabelShort).Resolve(), "CC_Impact_JoinBrief".Translate(hc.stranger.LabelShort).Resolve(), "pev8fugitive");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.FineGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null || hc.map == null) return;
            Arrivals.SendAway(hc.group.ToList());
            Arrivals.Join(hc.stranger, hc.map);
            PeopleEvents8.Goodwill(hc.faction, PeopleEvents8Math.FineGoodwill, GoodwillWhy.Paid);
        }
    }

    /// <summary>
    /// The deserter sheltered: they join; the Republic angry (and the papers struck, if the colony holds them); turned against the colony,
    /// the detail at the gate fights.
    /// </summary>
    public class DilemmaEffect_FugitiveShelter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The Republic's goodwill after the shelter and the papers struck, as it would stand.</summary>
        private static int After(Faction f) =>
            (f != null && f.HasGoodwill ? f.PlayerGoodwill : 0) + PeopleEvents8Math.ShelterGoodwill + (Papers.Holds(f) ? PeoplesFMath.StruckGoodwill : 0);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_Impact_Join".Translate(hc.stranger.LabelShort).Resolve(), "CC_Impact_JoinBrief".Translate(hc.stranger.LabelShort).Resolve(), "pev8fugitive");
            if (hc.faction == null) return;
            sheet.Goodwill(hc.faction, PeopleEvents8Math.ShelterGoodwill);
            if (Papers.Holds(hc.faction))
                sheet.Add(ImpactKind.Other, -1, "CC_PEv8_Impact_PapersStruck".Translate(hc.faction.Name).Resolve(), "CC_PEv8_Impact_PapersStruckBrief".Translate().Resolve(), "pev8papers");
            if (After(hc.faction) <= ArisingMath.Hostile)
                sheet.Add(ImpactKind.Health, -1, "CC_PEv8_Impact_DetailFights".Translate(hc.variant).Resolve(), "CC_PEv8_Impact_DetailFightsBrief".Translate().Resolve(), "pev8detail");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var f = hc?.faction;
            if (hc?.stranger == null || hc.map == null || f == null) return;
            Arrivals.Join(hc.stranger, hc.map);
            if (Papers.Holds(f)) Papers.Strike(f, hc.map);
            if (f.HasGoodwill && !f.IsPlayer && !f.defeated) WorldSituationWorker.Goodwill(f, PeopleEvents8Math.ShelterGoodwill, GoodwillWhy.Deserter);
            if (f.HostileTo(Faction.OfPlayer)) RepublicFugitive.Fight(hc);
            else Arrivals.SendAway(hc.group.ToList());
        }
    }

    /// <summary>The deserter handed over: the detail leads them away, and the Republic thanks the colony.</summary>
    public class DilemmaEffect_FugitiveHandOver : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null) return;
            sheet.Play("CC_PEv8_Impact_LedAway".Translate(hc.stranger.LabelShort).Resolve(), "CC_PEv8_Impact_LedAwayBrief".Translate().Resolve());
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.HandOverGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            RepublicFugitive.LedAway(hc);
            PeopleEvents8.Goodwill(hc.faction, PeopleEvents8Math.HandOverGoodwill, GoodwillWhy.Returned);
        }
    }
}
