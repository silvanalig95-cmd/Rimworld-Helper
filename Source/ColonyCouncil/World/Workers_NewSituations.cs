using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // World events that come from what the colony has built (texts: Defs/WorldSituationDefs/Rimfolk_WS_New.xml): market
    // days at its RimMarket stalls, busy orbital lanes over a colony with a comms console, and an assize in the nearest
    // friendly town.

    /// <summary>
    /// Market days (RimMarket): for a fortnight the neighbours make the colony's market their own. Only while one of the
    /// colony's stalls has goods on its counter. Waves of buyers walk in to its stalls (VisitorTrade, "buyers"), a
    /// traders' camp asks for a pitch, a bulk order comes, and two buyers quarrel at the gate over a price.
    /// </summary>
    public class SituationWorker_MarketDays : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => VisitorTrade.On && RimMarketStall.StockedStalls(map).Count > 0;

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            var map = ctx.map;
            var stalls = RimMarketStall.StockedStalls(map);
            // Nothing on the counters any more: nobody comes for it.
            if (stalls.Count == 0) return false;
            if (beat.key == "pitch")
            {
                // Only when a traders' camp could really come up on the land now.
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail("Council_Camp_Market");
                return kind != null && kind.FitsEra() && kind.Possible && CampTracker.Enabled && CampTracker.Instance != null && CampTracker.VisitingAt(map) == null;
            }
            if (beat.key == "quarrel")
            {
                if (s.ctx.enemy == null || s.ctx.enemy.HostileTo(Faction.OfPlayer)) return false;
                // What they both want: an article on one of the counters.
                var t = stalls.SelectMany(RimMarketStall.Stock).Where(x => x != null && x.def != ThingDefOf.Silver).RandomElementWithFallback();
                ctx.thing = t == null ? null : RimMarketStall.StockDef(t) ?? t.def;
                return ctx.thing != null;
            }
            return true;
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            var map = s.ctx.map;
            if (map != null && RimMarketStall.StockedStalls(map).Count > 1) tags.Add("stalls");
        }
    }

    /// <summary>
    /// Busy orbital lanes: over a colony with a working comms console, orbital traders call more often, debris from the
    /// lanes comes down (the game's own ship chunks), a signal is picked up, and a trading house of a people the colony is
    /// at peace with sends an agent down to buy one thing the colony has. The agent walks up from where their shuttle set
    /// down beyond the map; what they buy is carried out to them and they pay at the gate.
    /// </summary>
    public class SituationWorker_OrbitalLanes : WorldSituationWorker
    {
        /// <summary>What an orbital agent would come down for, in order of how much they'd want it.</summary>
        private static readonly string[] Wanted =
        {
            "ComponentIndustrial", "Plasteel", "MedicineIndustrial", "Gold", "Uranium", "Neutroamine", "Hyperweave", "DevilstrandCloth",
            "Synthread", "Chemfuel", "Silver"
        };

        public static bool CommsOn(Map map)
        {
            if (map == null) return false;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b?.def == null || b.def != ThingDefOf.CommsConsole || !b.Spawned) continue;
                var power = b.TryGetComp<CompPowerTrader>();
                if (power == null || power.PowerOn) return true;
            }
            return false;
        }

        private static bool HighTech(Faction f) => f?.def != null && Peoples.TechOf(f) >= TechLevel.Industrial;

        protected override bool CanHappen(Map map) => CommsOn(map) && Candidates(def.faction, null, map).Any(HighTech);

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (!HighTech(s.ctx.faction))
            {
                var f = Candidates(def.faction, null, map).Where(HighTech).RandomElementWithFallback();
                if (f == null) return false;
                s.ctx.faction = f;
                s.At(NearestTown(f, map), f.Name);
            }
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "agent") return true;
            if (!CommsOn(ctx.map)) return false;
            // One thing the colony has enough of to be worth the trip down.
            foreach (var name in Wanted)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d == null || d == ThingDefOf.Silver) continue;
                int have = VarietyUtil.InStores(ctx.map, d);
                int n = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(Rand.Range(250f, 600f) * LivingEvents.WealthFactor(ctx.map) / Mathf.Max(1f, d.BaseMarketValue)), 1, have / 2));
                if (n <= 0 || n * d.BaseMarketValue < 100f || !Rand.Chance(0.6f)) continue;
                ctx.thing = d;
                ctx.count = n;
                ctx.item = VarietyUtil.Bundle(d, n);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The assize: a circuit judge holds court in the nearest friendly town for a few weeks. The court's clerk writes to ask
    /// whether the colony will hand over a prisoner for trial (the court's guards come for them, Couriers.Fetch, and the
    /// reward is set down when they have them) or bring its own grievance against another people (decided by the judge in
    /// a later beat, by the colony's speaker's skill and luck); later the judge stops at the colony for a night on the way
    /// on. Verdicts are told in words; nothing grim is shown.
    /// </summary>
    public class SituationWorker_Assize : WorldSituationWorker
    {
        /// <summary>A prisoner the court would try: one of the colony's, not one of the judge's own people, not a child.</summary>
        private static bool Triable(Pawn p, Faction court) =>
            p != null && !p.Dead && p.Spawned && p.IsPrisonerOfColony && !p.DevelopmentalStage.Juvenile() && p.Faction != court && !p.InMentalState
            && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p);

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "court")
            {
                ctx.stranger = ctx.map.mapPawns.PrisonersOfColonySpawned.Where(p => Triable(p, s.ctx.faction))
                    .OrderByDescending(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer) ? 1 : 0).FirstOrDefault();
                // Nothing to bring before the court: no prisoner, and no quarrel with anyone.
                if (ctx.stranger == null && ctx.enemy == null) return false;
                ctx.count = Silver(ctx.map, new IntRange(120, 260));
                ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Social) ?? ctx.a;
                return ctx.a != null;
            }
            if (beat.key == "verdict")
            {
                // The judge's finding, by how well the colony's speaker put the case.
                int level = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 4;
                if (Rand.Chance(Mathf.Clamp(0.35f + level * 0.035f, 0.35f, 0.85f))) s.Set("won");
                else s.Set("lost");
                ctx.count = Silver(ctx.map, new IntRange(150, 300));
                return true;
            }
            return true;
        }

        public override bool Offered(WorldSituation s, SituationBeat beat, int option)
        {
            if (!base.Offered(s, beat, option)) return false;
            if (beat.key != "court") return true;
            var o = beat.options[option];
            if (o.flags != null && o.flags.Contains("handed"))
                return s.ctx.map != null && s.ctx.map.mapPawns.PrisonersOfColonySpawned.Any(p => Triable(p, s.ctx.faction));
            if (o.flags != null && o.flags.Contains("grievance")) return s.ctx.enemy != null && !s.ctx.enemy.defeated;
            return true;
        }

        public override string Unavailable(WorldSituation s, SituationContext ctx, SituationBeat beat, int option)
        {
            if (beat.key != "court") return null;
            var o = beat.options[option];
            if (o.flags != null && o.flags.Contains("handed") && !Triable(ctx.stranger, s.ctx.faction))
                return "CC_St_NotPrisoner".Translate(ctx.stranger?.LabelShort ?? "?").ToString();
            return null;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key != "court" || byDefault) return outcome;
            var o = beat.options[option];
            var p = ctx.stranger;
            if (o.flags == null || !o.flags.Contains("handed") || !Triable(p, s.ctx.faction)) return outcome;
            var map = ctx.map;
            var near = VarietyUtil.Near(ctx.a, map);
            // The court's guards come for the prisoner and set the reward down once they have them (Couriers/); else out of
            // the colony's hands at once, as before.
            if (!Couriers.Fetch(map, p, s.ctx.faction, Couriers.Make(ThingDefOf.Silver, ctx.count), near, true))
            {
                VarietyUtil.PaySilver(map, near, ctx.count);
                p.DeSpawn();
                p.guest?.SetGuestStatus(null);
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Decide);
            }
            return outcome;
        }
    }
}
