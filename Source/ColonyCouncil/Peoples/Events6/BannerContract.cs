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
    // ===================== 55. A contract on the colony's enemy (the Ashen Banner) =====================

    /// <summary>
    /// A contract on the colony's enemy (item 55): a captain of the Ashen Banner offers to march against an enemy camp within
    /// reach (a hostile people's camp from the game's quests and sites, else one of their towns), enemies of the Banner too,
    /// at a price by the threat it is. The colony sends at least one of its own fighters there by caravan within the
    /// contract's days (the calls to arms' way: the game's own map of the site, made when the caravan arrives); the company
    /// marches in beside them when they get there and goes for the enemy there (the game's assault on them, their issued arms
    /// by the Banner's age). When the fight is over the silver found there is split by the contract, half to the company,
    /// unless the colony paid their share up front; the company's dead are named for the Roll; the contract is kept. Not
    /// reached in time, the company marches home with its pay (paid to the day, held to the day); the camp gone before then,
    /// half the price comes back by their courier.
    /// </summary>
    public static class BannerContract
    {
        public const string EventDef = "Rimfolk_Evt_BannerContract";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A people whose camp the company could march on: at war with the colony and the Banner both, of people.</summary>
        private static bool Enemy(Faction f, Faction banner) =>
            f != null && !f.IsPlayer && !f.defeated && !f.Hidden && f.def != null && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && banner != null && f != banner && f.HostileTo(banner);

        private static bool Near(Map home, PlanetTile tile) =>
            home != null && tile.Valid && tile.Layer == home.Tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(home.Tile, tile) <= PeopleEvents6Math.ContractTiles;

        /// <summary>The enemy camp within reach: a hostile site first (the nearest), else a hostile town; none with the colony there already.</summary>
        public static MapParent Target(Map home, Faction banner, out bool town)
        {
            town = false;
            if (home == null || banner == null) return null;
            var site = Find.WorldObjects.Sites.Where(s => s != null && !s.Destroyed && !s.HasMap && Enemy(s.Faction, banner) && Near(home, s.Tile))
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile)).FirstOrDefault();
            if (site != null) return site;
            var s2 = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && !s.HasMap && Enemy(s.Faction, banner) && Near(home, s.Tile))
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile)).FirstOrDefault();
            town = s2 != null;
            return s2;
        }

        /// <summary>The colony's people who could go: grown, free to leave, able to fight.</summary>
        public static List<Pawn> Fighters(Map map) => LeagueGuard.Fighters(map);

        private static WorldObject ById(int id) => id < 0 ? null : Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == id);

        /// <summary>The contract made: the record kept, the colony told where its caravan must go and by when.</summary>
        public static void Make(HappeningContext ctx, bool upFront)
        {
            var b = PeopleEvents6Book.Instance;
            var target = ById(ctx?.variant ?? -1) as MapParent;
            if (b == null || ctx?.map == null || target == null || ctx.faction == null) return;
            float points = StorytellerUtility.DefaultThreatPointsNow(ctx.map);
            b.AddContract(new HiredContract
            {
                faction = ctx.faction, enemy = ctx.enemy ?? target.Faction, home = ctx.map, target = target.ID, targetName = target.LabelCap, town = target is Settlement,
                price = ctx.count, upFront = upFront, due = Now + PeopleEvents6Math.ContractDays * GenDate.TicksPerDay, size = PeopleEvents6Math.Company(points)
            });
            Tell.Message(NewsKind.World, Importance.Important, ctx.map, "CC_PEv6_ContractMade".Translate(ctx.faction.Name.Named("FACTION"), target.LabelCap.Named("PLACE")).Resolve(),
                new LookTargets(target), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The events' hour: each contract waiting for the colony's people, or with the company there.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null) return;
            foreach (var r in b.contracts.Where(r => r != null && !r.Done).ToList())
            {
                try
                {
                    if (r.stage == "fighting") Fighting(r);
                    else Waiting(r, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: a contract", e);
                    r.stage = "done";
                }
            }
        }

        private static Map HomeOf(HiredContract r) => r.home != null && Find.Maps.Contains(r.home) ? r.home : Homes.Seat;

        private static void Waiting(HiredContract r, int now)
        {
            var target = ById(r.target) as MapParent;
            var home = HomeOf(r);
            if (target == null || target.Destroyed)
            {
                // Gone before the colony got there: the company marched for nothing, and half the price comes back.
                r.stage = "done";
                if (home != null && r.faction != null && !r.faction.defeated) Couriers.PaySilver(home, CampSite.ColonyCenter(home), r.price / 2, r.faction);
                Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv6_ContractGone".Translate((r.faction?.Name ?? "?").Named("FACTION"), (r.targetName ?? "?").Named("PLACE")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            var map = target.HasMap ? target.Map : null;
            if (map != null && map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed && !p.Dead))
            {
                Arrive(r, map);
                return;
            }
            if (now <= r.due) return;
            r.stage = "done";
            if (PeopleEvents6.Thanks(r.faction)) WorldSituationWorker.Goodwill(r.faction, PeopleEvents6Math.LapseGoodwill, GoodwillWhy.Slight);
            Tell.Message(NewsKind.World, Importance.Notable, home, "CC_PEv6_ContractLapsed".Translate((r.faction?.Name ?? "?").Named("FACTION"), (r.targetName ?? "?").Named("PLACE")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Those still fighting the colony there.</summary>
        private static List<Pawn> Hostiles(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p != null && !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer) && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid)).ToList();

        /// <summary>The colony's people are there: the company marches in beside them and goes for the enemy.</summary>
        private static void Arrive(HiredContract r, Map map)
        {
            r.stage = "fighting";
            var f = r.faction;
            if (f == null || f.defeated || f.HostileTo(Faction.OfPlayer))
            {
                r.stage = "done";
                return;
            }
            var anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Downed);
            var company = new List<Pawn>();
            for (int i = 0; i < r.size; i++)
            {
                var kind = PeoplesD.KindNamed(f, i == 0 ? "Rimfolk_AshenBanner_Sergeant" : i % 2 == 0 ? "Rimfolk_AshenBanner_Shot" : "Rimfolk_AshenBanner_Soldier");
                var p = Arrivals.Make(kind, f, new FloatRange(20f, 50f));
                if (p == null) continue;
                if (!Arrivals.Spawn(p, map, anchor?.Position))
                {
                    Arrivals.Discard(p);
                    continue;
                }
                company.Add(p);
            }
            if (company.Count == 0)
            {
                r.stage = "done";
                return;
            }
            r.company = company;
            var foes = Hostiles(map);
            try
            {
                if (foes.Count > 0) LordMaker.MakeNewLord(f, new LordJob_AssaultThings(f, foes.Cast<Thing>().ToList(), 1f, false), map, company);
                else LordMaker.MakeNewLord(f, new LordJob_AssistColony(f, anchor?.Position ?? map.Center), map, company);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the company's march", e);
            }
            // together: the sergeant's word to the colony's people they have just come in beside.
            PeoplesG.SayLine(company[0], DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "arrive");
            Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv6_ContractArrivedLabel".Translate().Resolve(),
                "CC_PEv6_ContractArrived".Translate(f.Name.Named("FACTION"), company.Count.Named("COUNT"), (r.targetName ?? "?").Named("PLACE")).Resolve(), LetterDefOf.PositiveEvent,
                new LookTargets(company), f);
        }

        /// <summary>The fight there: over when nobody is left fighting the colony (or the company is gone); then the split, the Roll and the march home.</summary>
        private static void Fighting(HiredContract r)
        {
            var target = ById(r.target) as MapParent;
            var map = target != null && target.HasMap ? target.Map : r.company.FirstOrDefault(p => p != null && p.Spawned)?.Map;
            foreach (var p in r.company.Where(p => p != null && p.Dead).ToList())
                if (!r.lost.Contains(p.LabelShort))
                    r.lost.Add(p.LabelShort);
            var up = r.company.Where(p => p != null && !p.Dead && p.Spawned && !p.Downed && p.Map == map).ToList();
            if (map != null && up.Count > 0 && Hostiles(map).Count > 0)
            {
                // Still at it: the assault kept on whoever is up of the enemy.
                if (!(up[0].GetLord()?.LordJob is LordJob_AssaultThings))
                    try
                    {
                        Lords.Leave(up);
                        LordMaker.MakeNewLord(r.faction, new LordJob_AssaultThings(r.faction, Hostiles(map).Cast<Thing>().ToList(), 1f, false), map, up);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 6: the company's fight", e);
                    }
                return;
            }
            Done(r, map, up);
        }

        /// <summary>The silver lying there, the most of it first (the colony's own caravan's packs are not lying there).</summary>
        private static List<Thing> Silver(Map map) =>
            map == null ? new List<Thing>() : map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned).OrderByDescending(t => t.stackCount).ToList();

        private static void Done(HiredContract r, Map map, List<Pawn> up)
        {
            r.stage = "done";
            var f = r.faction;
            var home = HomeOf(r);
            // The split by the contract: half the silver lying there counted out and taken by the company, unless paid up front.
            if (!r.upFront && map != null && up.Count > 0)
            {
                int share = PeopleEvents6Math.Share(Silver(map).Sum(t => t.stackCount));
                int left = share;
                foreach (var t in Silver(map))
                {
                    if (left <= 0) break;
                    int take = Mathf.Min(left, t.stackCount);
                    left -= take;
                    if (take >= t.stackCount) t.Destroy();
                    else t.SplitOff(take).Destroy();
                }
                r.took = share - left;
            }
            if (PeopleEvents6.Thanks(f))
            {
                WorldSituationWorker.Goodwill(f, PeopleEvents6Math.ContractGoodwill, GoodwillWhy.DealKept);
                FactionMemory.Note(f, "contractKept", r.targetName);
            }
            if (up.Count > 0) PeoplesG.SayLine(up[0], DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), r.lost.Count > 0 ? "roll" : "kept");
            string text = "CC_PEv6_ContractKept".Translate((f?.Name ?? "?").Named("FACTION"), (r.targetName ?? "?").Named("PLACE")).Resolve();
            if (r.took > 0) text += " " + "CC_PEv6_ContractShare".Translate(r.took.Named("COUNT")).Resolve();
            if (r.lost.Count > 0) text += " " + "CC_PEv6_ContractRoll".Translate(TextUtil.JoinAnd(r.lost).Named("NAMES")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Notable, map ?? home, "CC_PEv6_ContractKeptLabel".Translate().Resolve(), text, LetterDefOf.PositiveEvent,
                up.Count > 0 ? new LookTargets(up) : LookTargets.Invalid, f);
            if (map != null && up.Count > 0)
                try
                {
                    Lords.Leave(up);
                    LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, true), map, up);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the company's march home", e);
                }
            r.company.Clear();
        }

        /// <summary>Dev mode: the waiting contract's company marches in on the map here now (the colony's people on it), as if this were the target.</summary>
        public static string Here(Map map)
        {
            var r = PeopleEvents6Book.Instance?.contracts.FirstOrDefault(x => x != null && x.stage == "waiting");
            if (r == null) return "no contract waiting.";
            if (map == null || map.mapPawns.FreeColonistsSpawned.Count == 0) return "nobody of the colony here.";
            Arrive(r, map);
            return r.stage == "fighting" ? "the company marches in beside the colony's people here." : "the company couldn't come.";
        }
    }

    /// <summary>
    /// A captain of {FACTION} ({X}) at the gate: the company would march on {PLACE} of {ENEMY} for {COUNT} silver ({DEAD} with
    /// their share of the silver paid up front), if the colony sends at least one of its own fighters to meet them there.
    /// </summary>
    public class HappeningWorker_BannerContract : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            var b = PeopleEvents6Book.Instance;
            return f != null && b != null && !b.contracts.Any(r => r != null && !r.Done) && BannerContract.Target(map, f, out _) != null && BannerContract.Fighters(map).Count > 0
                   && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            var target = BannerContract.Target(ctx.map, ctx.faction, out bool town);
            if (ctx.faction == null || target == null) return false;
            ctx.enemy = target.Faction;
            ctx.variant = target.ID;
            ctx.placeName = target.LabelCap;
            ctx.count = PeopleEvents6Math.ContractPrice(StorytellerUtility.DefaultThreatPointsNow(ctx.map), town);
            ctx.dead = PeopleEvents6Math.UpFront(ctx.count).ToString();
            int days = Mathf.Max(1, Mathf.RoundToInt(SeasonAway.RoadDays(ctx.map, target.Tile)));
            ctx.tale = days == 1 ? "CC_PEv6_RoadDay".Translate().Resolve() : "CC_PEv6_RoadDays".Translate(days).Resolve();
            ctx.note = town ? "town" : "camp";
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Captain"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = LeagueGuard.BestFighter(BannerContract.Fighters(ctx.map));
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "town" ? "town" : "camp");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var target = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == ctx.variant);
            if (target == null || target.Destroyed) return "CC_PEv6_ContractNoTarget".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "hired" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The contract made (BannerContract.Make); with <see cref="upFront"/>, their share paid and the silver found there all the colony's.</summary>
    public class DilemmaEffect_BannerContract : DilemmaEffect
    {
        public bool upFront;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.Other, 0, "CC_PEv6_Impact_Contract".Translate(hc.placeName ?? "?", PeopleEvents6Math.ContractDays).Resolve(), "CC_PEv6_Impact_ContractBrief".Translate().Resolve(), "pev6contract");
            sheet.Add(ImpactKind.Other, upFront ? 1 : 0, (upFront ? "CC_PEv6_Impact_ContractAll" : "CC_PEv6_Impact_ContractHalf").Translate().Resolve(),
                "CC_PEv6_Impact_ContractSplitBrief".Translate().Resolve(), "pev6split");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.ContractGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            BannerContract.Make(hc, upFront);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }
}
