using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A council question's people and plants for a sickness of the wild plants (Rimfolk_Land_SickWood): {THING} is the plant
    /// it began in, {KIND} what to call the plants it takes ("oak trees", "grass", "berry bushes"); {COUNT} how many are sick.
    /// </summary>
    public class DilemmaContextLand : DilemmaContext
    {
        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            var k = (LandSickKind)Math.Max(0, LandSickness.KindFor(thing));
            args.Add(LandSickness.Kind(thing, k).Named("KIND"));
        }
    }

    /// <summary>
    /// A sickness has got into the wild plants (Nature/LandSickness.cs) and some are sick already: what the colony does about
    /// it. A is the colony's best grower, B another of the colony (the next best grower). Tags: "wilt", "rust", "rot" by the
    /// sickness, "near" when it is close to the colony's home.
    /// </summary>
    public class DilemmaWorker_LandSick : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && LandSickness.Askable(map) != null;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var o = LandSickness.Askable(map);
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (o == null || pawns.Count < def.minColonists) return false;
            var growers = pawns.Where(p => def.roleA.Allows(p, null) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).ToList();
            if (growers.Count == 0) return false;
            var a = growers[0];
            var b = growers.Skip(1).FirstOrDefault() ?? pawns.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).FirstOrDefault();
            if (b == null) return false;
            var thing = DefDatabase<ThingDef>.GetNamedSilentFail(o.def);
            if (thing == null) return false;
            ctx = new DilemmaContextLand { def = def, map = map, a = a, b = b, thing = thing, count = LandSickness.Living(map, o).Count };
            o.asked = true;
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var o = ctx?.map != null ? LandBook.Of(ctx.map)?.Outbreak : null;
            if (o == null) return;
            tags.Add(o.kind.ToString().ToLowerInvariant());
            var home = ctx.map.areaManager.Home;
            if (LandSickness.Living(ctx.map, o).Take(5).Any(p => GenRadial.RadialCellsAround(p.Position, 10f, true).Any(c => c.InBounds(ctx.map) && home[c]))) tags.Add("near");
        }
    }

    /// <summary>
    /// The sick plants and every plant of their kind beside them marked for cutting (the game's own cut marks; a work party in
    /// the same answer does the work). Can't be chosen once nothing is sick.
    /// </summary>
    public class DilemmaEffect_LandSickCut : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var o = ctx?.map != null ? LandBook.Of(ctx.map)?.Outbreak : null;
            return o == null || o.sick.Count == 0 ? "CC_Land_NoSickness".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var o = ctx?.map != null ? LandBook.Of(ctx.map)?.Outbreak : null;
            if (o == null) return;
            int sick = LandSickness.Living(ctx.map, o).Count;
            if (sick == 0) return;
            sheet.Play("CC_Impact_LandCut".Translate(sick, LandSickness.Kind(ctx.thing, o.kind).Named("KIND")).Resolve(), "CC_Impact_LandCutBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var o = LandBook.Of(map)?.Outbreak;
                int n = LandSickness.MarkCut(map);
                if (n == 0 || o == null) return;
                var first = LandSickness.Living(map, o).FirstOrDefault();
                Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_Land_CutMarked".Translate(n, LandSickness.Kind(ctx.thing, o.kind).Named("KIND")).Resolve(),
                    first != null ? new LookTargets(first) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: the sick marked", e);
            }
        }
    }

    /// <summary>
    /// The sickness burned out: A (or the best of the colony able then) walks out on the first dry daylight hour to a sick
    /// plant at the far edge, clear of the colony, and sets fire to it and a few sick beside it (JobDriver_LandBurn); the fire
    /// is the game's from there. Can't be chosen once nothing is sick, or when none of the sick stands clear of the colony.
    /// </summary>
    public class DilemmaEffect_LandSickBurn : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx?.map == null ? null : LandSickness.CantBurn(ctx.map);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || LandSickness.CantBurn(ctx.map) != null) return;
            var o = LandBook.Of(ctx.map)?.Outbreak;
            sheet.Play("CC_Impact_LandBurn".Translate((ctx.a?.LabelShort ?? "?").Named("PAWN"), LandSickness.Kind(ctx.thing, o?.kind ?? LandSickKind.Wilt).Named("KIND")).Resolve(),
                "CC_Impact_LandBurnBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                LandSickness.SetBurn(ctx.map, ctx.a);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: the burn set", e);
            }
        }
    }
}
