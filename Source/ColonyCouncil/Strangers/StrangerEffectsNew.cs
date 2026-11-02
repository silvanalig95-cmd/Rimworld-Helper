using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A slave's suppression rises or falls (Ideology, the game's own need): a hard word from the overseer holds them
    /// down, a kind one eases the fear that does. Only for one of the colony's slaves.
    /// </summary>
    public class DilemmaEffect_Suppression : DilemmaEffect
    {
        /// <summary>A share of the bar: 0.1 is a tenth.</summary>
        public float amount;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (!ModsConfig.IdeologyActive || x == null || x.Dead || !x.IsSlaveOfColony || amount == 0f) return;
            string pct = ImpactMath.Signed(Mathf.Round(amount * 100f)) + "%";
            sheet.Add(ImpactKind.People, amount > 0f ? 1 : -1, "Rimfolk_Impact_Suppression".Translate(x.LabelShort, pct).Resolve(), "Rimfolk_Impact_SuppressionBrief".Translate(pct).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (!ModsConfig.IdeologyActive || x == null || x.Dead || !x.IsSlaveOfColony || amount == 0f) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var need = x.needs?.TryGetNeed<Need_Suppression>();
            if (need != null) need.CurLevel = Mathf.Clamp01(need.CurLevel + amount);
        }
    }
}
