using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The workers of the council's questions about the camp on the colony's land (Defs/DilemmaDefs/Rimfolk_Dilemmas_Camps.xml).
    // Like DilemmaWorker_Camp they need a camp settled on the home map and at peace (X: one of them, {FACTION}: their
    // people), and each asks for something more: one of their ordinary people rather than whoever speaks for them, one
    // a colonist has grown close to, a child of theirs and one of the colony's, their herd and the colony's ripe fields,
    // a fee they agreed to, or another people with a quarrel against them ({RIVAL}).

    /// <summary>X is one of the camp's grown-ups, not the one who speaks for them nor their trader.</summary>
    public class DilemmaWorker_CampMember : DilemmaWorker_Camp
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = CampOnMap.Of(ctx.map);
            var x = c?.Here.Where(p => p != c.leader && p != c.trader && !p.Downed && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (x == null) return false;
            ctx.stranger = x;
            return true;
        }
    }

    /// <summary>
    /// A has grown close to one of the camp (time at their fire, or a good opinion): X is that one, a grown-up who
    /// doesn't speak for them.
    /// </summary>
    public class DilemmaWorker_CampFriend : DilemmaWorker_Camp
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || ctx.a?.relations == null) return false;
            var c = CampOnMap.Of(ctx.map);
            if (c == null) return false;
            var x = c.Here.Where(p => p != c.leader && p != c.trader && !p.Downed && !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => ctx.a.relations.OpinionOf(p)).FirstOrDefault();
            if (x == null) return false;
            int warm = c.warmth.TryGetValue(ctx.a, out int w) ? w : 0;
            if (warm < 3 && ctx.a.relations.OpinionOf(x) < 15) return false;
            ctx.stranger = x;
            return true;
        }
    }

    /// <summary>X is a child of the camp, A a child of the colony (Biotech): the dilemma's role asks for the child.</summary>
    public class DilemmaWorker_CampChild : DilemmaWorker_Camp
    {
        public override bool CanHappenNow(Map map) => ModsConfig.BiotechActive && base.CanHappenNow(map);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = CampOnMap.Of(ctx.map);
            var x = c?.Here.Where(p => p.DevelopmentalStage.Child() && !p.Downed).RandomElementWithFallback();
            if (x == null) return false;
            ctx.stranger = x;
            return true;
        }
    }

    /// <summary>
    /// The camp's herd got into the colony's fields: they have animals, the colony has ripe crops, and nothing else of
    /// theirs is under way. When it's asked, the beasts are there among the crops (until an answer sends them home, or a
    /// day goes by): X is one of them.
    /// </summary>
    public class DilemmaWorker_CampHerd : DilemmaWorker_Camp
    {
        public override bool CanHappenNow(Map map)
        {
            var c = CampOnMap.Of(map);
            return c != null && c.herd.Count > 0 && !c.ErrandSet && base.CanHappenNow(map);
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = CampOnMap.Of(ctx.map);
            var beast = c?.herd.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed).RandomElementWithFallback();
            if (beast == null) return false;
            var field = PlaySpots.FieldCell(ctx.map, null);
            if (!field.IsValid || !RipeNear(ctx.map, field)) return false;
            if (!c.StrayHerd(field)) return false;
            ctx.stranger = beast;
            return true;
        }

        private static bool RipeNear(Map map, IntVec3 at)
        {
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(at, 9f, true))
            {
                if (!c.InBounds(map) || !(c.GetZone(map) is Zone_Growing)) continue;
                var p = c.GetPlant(map);
                if (p?.def.plant != null && p.def.plant.Harvestable && p.Growth > 0.2f && ++n >= 4) return true;
            }
            return false;
        }
    }

    /// <summary>The camp took the colony's terms with a fee, and it falls due: X speaks for them.</summary>
    public class DilemmaWorker_CampFee : DilemmaWorker_Camp
    {
        public override bool CanHappenNow(Map map)
        {
            var c = CampOnMap.Of(map);
            return c != null && c.termFee && c.fee > 0 && base.CanHappenNow(map);
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            ctx.count = CampOnMap.Of(ctx.map)?.fee ?? 0;
            return ctx.count > 0;
        }
    }

    /// <summary>
    /// Another people with a quarrel against the camp's ({RIVAL}): any at peace with the colony, those at odds with the
    /// camp's own people first.
    /// </summary>
    public class DilemmaWorker_CampRival : DilemmaWorker_Camp
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!base.TryCast(map, out var plain) || plain?.faction == null) return false;
            var rival = Find.FactionManager.AllFactionsVisible
                .Where(f => f != plain.faction && !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction)
                .OrderByDescending(f => f.HostileTo(plain.faction) ? 1 : 0).ThenBy(_ => Rand.Value).FirstOrDefault();
            if (rival == null) return false;
            ctx = new CampRivalContext
            {
                def = plain.def, map = plain.map, a = plain.a, b = plain.b, count = plain.count, faction = plain.faction, stranger = plain.stranger,
                thing = plain.thing, rival = rival
            };
            return true;
        }
    }

    /// <summary>A camp dilemma's context with the other people of the quarrel ({RIVAL}).</summary>
    public class CampRivalContext : DilemmaContext
    {
        public Faction rival;

        protected override void AddArgs(List<NamedArgument> args) => args.Add((rival?.Name ?? "").Named("RIVAL"));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref rival, "rival");
        }
    }

    /// <summary>The colony's standing with the other people of a camp's quarrel ({RIVAL}); a loss stops short of enmity.</summary>
    public class DilemmaEffect_RivalGoodwill : DilemmaEffect
    {
        public int amount;

        public override void Apply(DilemmaContext ctx)
        {
            var rival = (ctx as CampRivalContext)?.rival;
            if (rival == null || (chance < 1f && !Rand.Chance(chance))) return;
            WorldSituationWorker.Goodwill(rival, amount);
        }
    }
}
