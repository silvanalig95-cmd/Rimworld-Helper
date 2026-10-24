using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // What the wider world events need beyond their data (Defs/WorldSituationDefs/Rimfolk_WS_Wider.xml): an ice road
    // only where winter freezes the lakes hard, an old beast only where one could walk, and a loan to a failing merchant
    // house that is paid back (or not) when it ends.

    /// <summary>Only in a hard freeze (the ice road): it's well below freezing outside, and the season with it.</summary>
    public class SituationWorker_DeepCold : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => map.mapTemperature.OutdoorTemp < -2f && map.mapTemperature.SeasonalTemp < -4f;
    }

    /// <summary>
    /// Only where the old beast could really walk past (the white thrumbo): the game has the animal, and the season and
    /// the weather at the colony suit it, as they must for the game's own "thrumbo passes".
    /// </summary>
    public class SituationWorker_OldBeast : WorldSituationWorker
    {
        private static PawnKindDef Beast => DefDatabase<PawnKindDef>.GetNamedSilentFail("Thrumbo");

        protected override bool CanHappen(Map map)
        {
            var kind = Beast;
            if (kind?.race == null) return false;
            try
            {
                return map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(kind.race);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("old beast weather", e);
                return false;
            }
        }
    }

    /// <summary>
    /// Silver lent out of the stores ({COUNT} times <see cref="WorldEffect_Pay.factor"/>, rounded to tens): paid like any
    /// payment (to the borrower at the gate, or fetched by their courier) and remembered as the situation's loan, which
    /// <see cref="WorldEffect_Repay"/> pays back when it ends. Unavailable without enough silver.
    /// </summary>
    public class WorldEffect_Lend : WorldEffect_Pay
    {
        public const string Key = "lent";

        public override void Apply(DilemmaContext ctx)
        {
            var s = SituationEffectUtil.Of(ctx);
            int amount = Amount(ctx.count, factor);
            base.Apply(ctx);
            s?.AddNum(Key, amount);
        }
    }

    /// <summary>
    /// The loan comes back, in an ending: what was lent (<see cref="WorldEffect_Lend"/>) times <see cref="factor"/> (a third
    /// more when the house is saved, a share of it when it's wound up), rounded to tens and carried in by the borrowers'
    /// courier. The letter tells it as {COUNT}. Nothing was lent: nothing comes.
    /// </summary>
    public class WorldEffect_Repay : WorldEffect
    {
        public float factor = 1f;

        public override void Apply(DilemmaContext ctx)
        {
            var s = SituationEffectUtil.Of(ctx);
            var map = SituationEffectUtil.MapOf(ctx);
            if (s == null || map == null || !Gate(ctx)) return;
            int lent = s.Num(WorldEffect_Lend.Key);
            if (lent <= 0) return;
            int back = Mathf.Max(10, Mathf.RoundToInt(lent * Mathf.Max(0f, factor) / 10f) * 10);
            ctx.count = back;
            s.SetNum(WorldEffect_Lend.Key, 0);
            Couriers.PaySilver(map, IntVec3.Invalid, back, Couriers.PeopleOf(ctx));
        }
    }
}
