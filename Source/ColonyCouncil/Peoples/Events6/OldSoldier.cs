using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 56. An old soldier settles (the Ashen Banner) =====================

    /// <summary>
    /// An old soldier settles (item 56): a veteran of the Ashen Banner, past the age for the march, comes to the gate with their
    /// war-horse and the issued kit of the Banner's age, and asks to settle with the colony. Taken in, they join with the horse
    /// bonded to them and trained to them (with Giddy-Up 2 a mount to ride); the Paragon horse of Vanilla Genetics Expanded in
    /// the spacer age, the game's horse in every other. Once a year (sixty days) the Banner calls them to the muster for a week
    /// at its nearest town (a caravan of one, round 2's season away), or the colony pays a muster fee; refusing both costs
    /// goodwill. The colony may buy the musters out for good when they settle (three years' fees).
    /// </summary>
    public static class OldSoldier
    {
        public const string EventDef = "Rimfolk_Evt_OldSoldier";
        public const string MusterDef = "Rimfolk_Evt_BannerMuster";
        public const string AwayKey = "muster";

        private static int Now => Find.TickManager.TicksGame;

        public static ThoughtDef Mustered => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_Mustered");

        /// <summary>The war-horse of the Banner's age: the first of the age's mounts the game has.</summary>
        public static PawnKindDef Mount(Faction f)
        {
            foreach (var n in PeopleEvents6Math.Mounts(PeopleEvents.AgeOf(f)))
            {
                var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
                if (k?.RaceProps?.Animal == true) return k;
            }
            return null;
        }

        /// <summary>The record of an old soldier of the colony's.</summary>
        public static VeteranRecord Of(Pawn p) => p == null ? null : PeopleEvents6Book.Instance?.veterans.FirstOrDefault(r => r != null && r.pawn == p);

        /// <summary>The veteran and their horse taken in: the horse the colony's, bonded and trained to them; the musters on record (or bought out).</summary>
        public static void Settle(HappeningContext ctx, bool buyOut)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (map == null || x == null || ctx.faction == null || !Arrivals.Join(x, map)) return;
            var horse = ctx.pet;
            if (horse != null && !horse.Dead && !horse.Discarded)
                try
                {
                    if (!horse.Spawned) Arrivals.Spawn(horse, map, x.Spawned ? x.Position : (IntVec3?)null);
                    Lords.Leave(horse);
                    if (horse.Faction != Faction.OfPlayer) horse.SetFaction(Faction.OfPlayer);
                    for (int round = 0; round < 3; round++)
                        foreach (var td in new[] { TrainableDefOf.Tameness, TrainableDefOf.Obedience })
                            if (td != null && !horse.training.HasLearned(td) && horse.training.CanAssignToTrain(td).Accepted)
                            {
                                horse.training.SetWantedRecursive(td, true);
                                horse.training.Train(td, x, true);
                            }
                    if (!horse.relations.DirectRelationExists(PawnRelationDefOf.Bond, x)) x.relations.AddDirectRelation(PawnRelationDefOf.Bond, horse);
                    if (horse.playerSettings != null) horse.playerSettings.Master = x;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the old soldier's horse", e);
                }
            PeopleEvents6Book.Instance?.AddVeteran(new VeteranRecord
            {
                pawn = x, faction = ctx.faction, bought = buyOut, done = buyOut, next = Now + PeopleEvents6Math.MusterYearDays * GenDate.TicksPerDay
            });
            if (PeopleEvents6.Thanks(ctx.faction)) WorldSituationWorker.Goodwill(ctx.faction, PeopleEvents6Math.SettleGoodwill, GoodwillWhy.Joined);
        }

        /// <summary>The events' hour: an old soldier's muster due brings the Banner's runner with the call (once a year); the dead and gone let go.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null) return;
            foreach (var r in b.veterans.Where(r => r != null && !r.done))
            {
                var p = r.pawn;
                if (p == null || p.Dead || p.Discarded || p.Faction != Faction.OfPlayer || r.faction == null || r.faction.defeated)
                {
                    r.done = true;
                    continue;
                }
                if (r.called >= 0 || now < r.next || SeasonAway.AwayNow(p) || !p.Spawned || !Homes.IsHome(p.Map) || !Tell.CanAsk || MapFacts.ThreatOn(p.Map)) continue;
                // The Banner at war with the colony calls no one; the muster waits a season.
                if (r.faction.HostileTo(Faction.OfPlayer) || !PeoplesD.Theirs(r.faction, PeoplesMath.Mercenaries))
                {
                    r.next = now + 15 * GenDate.TicksPerDay;
                    continue;
                }
                r.called = now;
                if (!PeopleEvents2.Fire(MusterDef, p.Map, r.faction)) r.called = -1;
            }
        }

        /// <summary>The muster answered (sent, paid or refused): the next is a year on.</summary>
        public static void Answered(Pawn p)
        {
            var r = Of(p);
            if (r == null) return;
            r.called = -1;
            r.next = Now + PeopleEvents6Math.MusterYearDays * GenDate.TicksPerDay;
        }

        /// <summary>The Banner's town the muster is at: its nearest within reach of the home.</summary>
        public static Settlement Town(Faction f, Map map) => f == null || map == null ? null : WorldSituationWorker.NearestTown(f, map);

        /// <summary>Dev mode: an old soldier's muster is due now.</summary>
        public static string Soon()
        {
            var r = PeopleEvents6Book.Instance?.veterans.FirstOrDefault(x => x != null && !x.done && x.pawn != null && !x.pawn.Dead);
            if (r == null) return "no old soldier of the Banner in the colony (or bought out).";
            r.called = -1;
            r.next = Now;
            return r.pawn.LabelShort + "'s muster is due; the runner comes within the hour (when a question may be asked).";
        }
    }

    /// <summary>
    /// A veteran of {FACTION} ({X}, with {BEAST}) at the gate, asking to settle with the colony; the Banner would call them to
    /// the muster once a year, or take a fee of {COUNT} silver, or {DEAD} to buy the musters out for good.
    /// </summary>
    public class HappeningWorker_OldSoldier : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Mercenaries) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Soldier"), ctx.faction,
                new FloatRange(PeopleEvents6Math.VeteranMinAge, PeopleEvents6Math.VeteranMaxAge));
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Melee, new IntRange(8, 13));
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Shooting, new IntRange(6, 11));
            var mount = OldSoldier.Mount(ctx.faction);
            if (mount != null)
                try
                {
                    var horse = PawnGenerator.GeneratePawn(new PawnGenerationRequest(mount, ctx.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        developmentalStages: DevelopmentalStage.Adult));
                    if (horse != null)
                    {
                        ctx.pet = horse;
                        ctx.group.Add(horse);
                        ctx.beast = horse.LabelShort;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the war-horse", e);
                }
            ctx.count = PeopleEvents6Math.MusterFee(LivingEvents.WealthFactor(ctx.map));
            ctx.dead = PeopleEvents6Math.BuyOut(ctx.count).ToString();
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.pet != null ? "horse" : "nohorse");
            if (ctx.pet != null && GiddyUpMounts.On && GiddyUpMounts.Ridable(ctx.pet.def)) tags.Add("ride");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "settle" : "later");
            string result = base.Resolved(ctx, option, risk, byDefault, outcome);
            // Sent on: their horse goes with them (the send-away takes people only).
            if (option > 1 || byDefault)
            {
                var horse = ctx.pet;
                var lord = ctx.stranger?.GetLord();
                if (horse != null && horse.Spawned && !horse.Dead && horse.Faction != Faction.OfPlayer && lord != null && horse.GetLord() != lord)
                    try
                    {
                        Lords.Leave(horse);
                        lord.AddPawn(horse);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 6: the horse led off", e);
                    }
            }
            return result;
        }
    }

    /// <summary>The veteran and their horse taken in (OldSoldier.Settle); with <see cref="buyOut"/>, the musters bought out for good.</summary>
    public class DilemmaEffect_OldSoldier : DilemmaEffect
    {
        public bool buyOut;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null) return;
            sheet.Add(ImpactKind.People, 1, (hc.pet != null ? "CC_PEv6_Impact_Settle" : "CC_PEv6_Impact_SettleAlone").Translate(hc.stranger.LabelShort, hc.beast ?? "").Resolve(),
                "CC_PEv6_Impact_SettleBrief".Translate().Resolve(), "pev6settle");
            sheet.Add(ImpactKind.Other, 0, (buyOut ? "CC_PEv6_Impact_MusterBought" : "CC_PEv6_Impact_Muster").Translate(hc.faction?.Name ?? "?", hc.count).Resolve(),
                "CC_PEv6_Impact_MusterBrief".Translate().Resolve(), "pev6muster");
        }

        public override void Apply(DilemmaContext ctx) => OldSoldier.Settle(ctx as HappeningContext, buyOut);
    }

    /// <summary>A runner of {FACTION} ({X}) at the gate: {A}, the colony's old soldier, is called to the muster at {PLACE} for a week, or the fee of {COUNT} silver.</summary>
    public class HappeningWorker_BannerMuster : HappeningWorker_Visitors
    {
        private static VeteranRecord Called(Map map) =>
            PeopleEvents6Book.Instance?.veterans.FirstOrDefault(r => r != null && !r.done && r.called >= 0 && r.pawn != null && r.pawn.Spawned && r.pawn.Map == map);

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Mercenaries) != null && Called(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = Called(ctx.map);
            ctx.faction = r?.faction;
            if (r == null || ctx.faction == null) return false;
            var town = OldSoldier.Town(ctx.faction, ctx.map);
            ctx.a = r.pawn;
            ctx.placeName = town?.Label ?? ctx.faction.Name;
            ctx.note = town != null ? town.Tile.ToString() : null;
            ctx.count = PeopleEvents6Math.MusterFee(LivingEvents.WealthFactor(ctx.map));
            ctx.pet = ctx.a.relations?.DirectRelations.Where(x => x.def == PawnRelationDefOf.Bond && x.otherPawn != null && x.otherPawn.Spawned && x.otherPawn.Map == ctx.map
                                                                && x.otherPawn.Faction == Faction.OfPlayer && !x.otherPawn.Dead).Select(x => x.otherPawn).FirstOrDefault();
            ctx.beast = ctx.pet?.LabelShort ?? "";
            ctx.stranger = Arrivals.Make(Arrivals.PersonKind(ctx.faction), ctx.faction, new FloatRange(16f, 30f));
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.pet != null) tags.Add("horse");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0 && (!PeopleEvents2.MayLeave(ctx.a, ctx.map) || ctx.note.NullOrEmpty())) return "CC_PEv6_VeteranCantGo".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "muster" : option == 1 ? "fee" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The muster answered: <see cref="how"/> "go" (A sets out for a week at the Banner's town, their horse with them), "fee" (paid), "refuse" (goodwill lost).</summary>
    public class DilemmaEffect_BannerMuster : DilemmaEffect
    {
        public string how = "go";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            switch (how)
            {
                case "go":
                    sheet.Play("CC_PEv6_Impact_MusterGo".Translate(hc.a.LabelShort, hc.placeName ?? "?", PeopleEvents6Math.MusterDays).Resolve(), "CC_PEv6_Impact_MusterGoBrief".Translate().Resolve());
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.MusterGoodwill);
                    break;
                case "refuse":
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.MusterRefusedGoodwill);
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            OldSoldier.Answered(hc.a);
            switch (how)
            {
                case "go":
                    if (hc.note != null && PlanetTile.TryParse(hc.note, out var tile) && hc.faction != null)
                    {
                        var beast = hc.pet != null && hc.pet.Spawned && !hc.pet.Dead && !hc.pet.Downed ? hc.pet : null;
                        var v = SeasonAway.Send(OldSoldier.AwayKey, hc.incident, hc.a, beast, hc.faction, tile, hc.placeName, PeopleEvents6Math.MusterDays, hc.map);
                        if (v != null) Arrivals.SendAway(PeoplesG.Party(hc));
                    }
                    return;
                case "refuse":
                    if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer)
                    {
                        WorldSituationWorker.Goodwill(hc.faction, PeopleEvents6Math.MusterRefusedGoodwill, GoodwillWhy.DutyRefused);
                        FactionMemory.Note(hc.faction, "refused", "CC_PEv6_MusterName".Translate().Resolve());
                    }
                    return;
            }
        }
    }

    /// <summary>The old soldier's week at the muster: the letter as they get there, and what the drill ground gave them at home.</summary>
    public class MusterAway : AwayKind
    {
        public override string Key => OldSoldier.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv6_MusterThere".Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            if (served)
            {
                try
                {
                    p.skills?.Learn(SkillDefOf.Melee, PeopleEvents6Math.MusterXp, true);
                    if (OldSoldier.Mustered != null) CouncilGameComponent.GiveThought(p, OldSoldier.Mustered, null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: home from the muster", e);
                }
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.MusterGoodwill, GoodwillWhy.DutyDone);
            }
            Tell.Message(NewsKind.World, Importance.Notable, home,
                (served ? "CC_PEv6_MusterHome" : "CC_PEv6_MusterHomeEarly").Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }
}
