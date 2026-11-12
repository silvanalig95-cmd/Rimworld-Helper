using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 3. One guard, one wagon (the road league) =====================

    /// <summary>
    /// One guard, one wagon (item 3): while a great caravan or a trading season runs within reach (the world events), a
    /// waymaster of the league asks for one fighter and one pack beast to walk under the colony's own flag, as every
    /// waymaster did for Edda Vane. The colonist leaves as a caravan of their own with the beast (SeasonAway), ridden with
    /// Giddy-Up, walks a few days with the caravan at its town, and comes home with a share of the profit and a trade lesson;
    /// the league thinks better of the colony, and its caravans come half again as often for a year (Peoples.CaravanFactor).
    /// The roads' ambushes and weather are the world's own. A pack beast in every age.
    /// </summary>
    public static class LeagueGuard
    {
        public const string AwayKey = "guard";

        /// <summary>The world events the guard walks with: a great caravan, a trading season.</summary>
        public static readonly string[] Situations = { "Rimfolk_WS_GreatCaravan", "Council_WS_TradeBoom" };

        /// <summary>The great caravan or trading season running now, and the town it gathers at (within a few days' walk).</summary>
        public static WorldSituation Running(Map map, Faction league, out Settlement town)
        {
            town = null;
            var all = WorldSituations.Instance?.Running;
            if (all == null || map == null) return null;
            foreach (var s in all)
            {
                if (s?.def == null || !Situations.Contains(s.def.defName)) continue;
                var f = s.ctx?.faction;
                var t = f != null && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) ? WorldSituationWorker.NearestTown(f, map) : null;
                t ??= league != null ? WorldSituationWorker.NearestTown(league, map) : null;
                if (t == null || t.Tile.Layer != map.Tile.Layer || SeasonAway.RoadDays(map, t.Tile) > 8f) continue;
                town = t;
                return s;
            }
            return null;
        }

        /// <summary>A pack beast of the colony's for the road: grown, up, the strongest first, one to ride first with Giddy-Up.</summary>
        public static Pawn Beast(Map map)
        {
            if (map == null) return null;
            bool gu = GiddyUpMounts.On;
            return map.mapPawns.SpawnedColonyAnimals.Where(a => a != null && !a.Dead && a.RaceProps?.Animal == true && !SeasonAway.AwayNow(a)
                                                                && PeopleEvents2Math.PackBeast(a.RaceProps.packAnimal, a.ageTracker?.Adult ?? false, a.Downed))
                .OrderByDescending(a => gu && GiddyUpMounts.Ridable(a.def) && a.RaceProps.baseBodySize >= OwnRiders.MinMountSize ? 1 : 0)
                .ThenByDescending(a => a.RaceProps.baseBodySize).FirstOrDefault();
        }

        /// <summary>The colony's people who could walk as its guard: free to leave and able to fight.</summary>
        public static List<Pawn> Fighters(Map map) =>
            PeoplesD.Able(map).Where(p => PeopleEvents2.MayLeave(p, map) && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();

        private static int Fight(Pawn p) => (p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0);

        private static int Social(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;

        public static Pawn BestFighter(List<Pawn> fighters) => fighters.OrderByDescending(Fight).FirstOrDefault();

        /// <summary>The colony's best talker who can still fight, for a bigger share and a better lesson.</summary>
        public static Pawn BestTrader(List<Pawn> fighters, Pawn not) => fighters.Where(p => p != not).OrderByDescending(Social).FirstOrDefault();

        /// <summary>The caravans' weighting while the league's thanks last: half again.</summary>
        public static float CaravanFactor(Faction f)
        {
            var b = PeopleEvents2Book.Instance;
            if (b == null || f == null || b.guardUntil < 0 || Find.TickManager.TicksGame > b.guardUntil) return 1f;
            return Peoples.CallingOf(f) == PeoplesMath.Traders ? PeopleEvents2Math.GuardCaravans : 1f;
        }
    }

    /// <summary>
    /// A waymaster of {FACTION} at the gate: {TALE} gathers at {PLACE}, and the league asks for one fighter and one pack beast
    /// ({PET}) to walk with it under the colony's flag: {A_nameDef}, the colony's best fighter, or {B_nameDef}, its best talker.
    /// </summary>
    public class HappeningWorker_LeagueGuard : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Traders);
            return f != null && SeasonAway.Running(LeagueGuard.AwayKey) == null && LeagueGuard.Running(map, f, out _) != null && LeagueGuard.Beast(map) != null
                   && LeagueGuard.Fighters(map).Count > 0 && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Traders);
            var s = LeagueGuard.Running(ctx.map, ctx.faction, out var town);
            if (ctx.faction == null || s == null || town == null) return false;
            var fighters = LeagueGuard.Fighters(ctx.map);
            ctx.a = LeagueGuard.BestFighter(fighters);
            ctx.b = LeagueGuard.BestTrader(fighters, ctx.a);
            ctx.pet = LeagueGuard.Beast(ctx.map);
            if (ctx.a == null || ctx.pet == null) return false;
            float days = Rand.Range(PeopleEvents2Math.GuardMinDays, PeopleEvents2Math.GuardMaxDays);
            ctx.count = Mathf.RoundToInt(2f * SeasonAway.RoadDays(ctx.map, town.Tile) + days);
            ctx.placeName = town.Name;
            ctx.tale = s.def.label;
            ctx.note = town.Tile + "|" + days.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_League_Outrider"), ctx.faction, ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.pet != null && GiddyUpMounts.On && GiddyUpMounts.Ridable(ctx.pet.def) && ctx.pet.RaceProps.baseBodySize >= OwnRiders.MinMountSize) tags.Add("ride");
            if (ctx.tale != null && ctx.tale.Contains("season")) tags.Add("season");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var who = option == 0 ? ctx.a : ctx.b;
            if (who == null) return "CC_PEv2_NoSecond".Translate().Resolve();
            if (!PeopleEvents2.MayLeave(who, ctx.map)) return "CC_PEv2_CantLeave".Translate(who.LabelShort.Named("PAWN")).Resolve();
            if (ctx.pet == null || ctx.pet.Dead || ctx.pet.Downed || !ctx.pet.Spawned || ctx.pet.Map != ctx.map) return "CC_PEv2_BeastGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "flag" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>{A} (or {B} with <see cref="second"/>) and the pack beast set out under the colony's flag (SeasonAway, "guard").</summary>
    public class DilemmaEffect_LeagueGuard : DilemmaEffect
    {
        public bool second;

        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => second ? ctx?.b : ctx?.a;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            var who = Who(ctx);
            if (hc == null || who == null || hc.pet == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_PEv2_Impact_Guard".Translate(who.LabelShort, hc.pet.LabelShort, TextUtil.DaysString(hc.count * GenDate.TicksPerDay), hc.placeName ?? "?").Resolve(),
                "CC_PEv2_Impact_AwayBrief".Translate().Resolve(), "pev2away");
            bool ride = GiddyUpMounts.On && GiddyUpMounts.Ridable(hc.pet.def) && hc.pet.RaceProps.baseBodySize >= OwnRiders.MinMountSize;
            sheet.Play((ride ? "CC_PEv2_Impact_GuardRide" : "CC_PEv2_Impact_GuardWalk").Translate(who.LabelShort, hc.pet.LabelShort).Resolve(), "CC_PEv2_Impact_WalkOutBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, -1, "CC_PEv2_Impact_Road".Translate(who.LabelShort).Resolve(), "CC_PEv2_Impact_RoadBrief".Translate().Resolve(), "pev2road");
            var bits = (hc.note ?? "").Split('|');
            float days = bits.Length > 1 && float.TryParse(bits[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 5f;
            int share = PeopleEvents2Math.GuardShare(days, who.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0, PeopleEvents.AgeOf(hc.faction));
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv2_Impact_Share".Translate(who.LabelShort, share).Resolve(), "CC_PEv2_Impact_ShareBrief".Translate().Resolve(), "pev2share");
            if (hc.faction != null)
                sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv2_Impact_GuardThanks".Translate(hc.faction.Name, PeopleEvents2Math.GuardGoodwill).Resolve(), "CC_PEv2_Impact_GuardThanksBrief".Translate().Resolve(), "pev2guardthanks");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var who = Who(ctx);
            if (hc?.map == null || who == null || hc.faction == null) return;
            var bits = (hc.note ?? "").Split('|');
            if (bits.Length < 2 || !PlanetTile.TryParse(bits[0], out var tile)) return;
            float days = float.TryParse(bits[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 5f;
            var beast = hc.pet != null && hc.pet.Spawned && !hc.pet.Dead && !hc.pet.Downed ? hc.pet : null;
            var v = SeasonAway.Send(LeagueGuard.AwayKey, hc.incident, who, beast, hc.faction, tile, hc.placeName, days, hc.map);
            if (v != null) Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The guard's season: the letter as they join the caravan, and the share, the lesson and the league's thanks at home.</summary>
    public class GuardAway : AwayKind
    {
        public override string Key => LeagueGuard.AwayKey;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Letter(NewsKind.World, Importance.Notable, home, "CC_PEv2_GuardThereLabel".Translate(p.LabelShort).Resolve(),
                (v.a != null ? "CC_PEv2_GuardThere" : "CC_PEv2_GuardThereAlone").Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION"),
                    (v.a?.LabelShort ?? "").Named("BEAST"), Mathf.RoundToInt(SeasonAway.DaysOf(v)).Named("DAYS")).Resolve(), LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            int share = 0;
            if (served)
            {
                share = PeopleEvents2Math.GuardShare(SeasonAway.DaysOf(v), p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0, PeopleEvents.AgeOf(f));
                // The share set down at their feet as they come in, and the trade lesson of the road.
                if (p.Spawned)
                    foreach (var t in Couriers.Make(ThingDefOf.Silver, share))
                        GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near);
                try
                {
                    p.skills?.Learn(SkillDefOf.Social, PeopleEvents2Math.GuardLessonXp, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 2: the guard's lesson", e);
                }
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.GuardGoodwill);
                FactionMemory.Note(f, "escorted", p.LabelShort);
                var b = PeopleEvents2Book.Instance;
                if (b != null) b.guardUntil = Find.TickManager.TicksGame + PeopleEvents2Math.GuardYearDays * GenDate.TicksPerDay;
            }
            // The beast stayed home (it didn't keep up at the edge), died on the road, or came home with them.
            string key = !served ? "CC_PEv2_GuardHomeEarly" : v.a == null ? "CC_PEv2_GuardHomeAlone" : v.a.Dead ? "CC_PEv2_GuardHomeBeastLost" : "CC_PEv2_GuardHome";
            Tell.Message(NewsKind.World, Importance.Notable, home, key.Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE"), share.Named("SHARE"), (f?.Name ?? "?").Named("FACTION"),
                (v.a?.LabelShort ?? "").Named("BEAST")).Resolve(), p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }
}
