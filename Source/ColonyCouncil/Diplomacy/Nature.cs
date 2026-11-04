using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Stress against one's nature (docs/design/stage2-diplomacy.md, item 13): the service each release of stage 2 calls
    /// when a colonist does, or is made to do, one of its acts (NatureAct). Written now with every act listed and no effect:
    /// it works out what the act would bring (NatureMath) and, until round d switches it on (decision 19,
    /// NatureMath.SwitchedOn), does nothing with it. Switched on, strain goes through the heart's own strain and relief
    /// (new kinds at the end of their lists, so older saves read as before), one act a day per colonist, scaled as the
    /// pressures' bad side is, and repeated acts may leave a mark.
    /// </summary>
    public static class Nature
    {
        public static bool On => NatureMath.SwitchedOn;

        /// <summary>The person's eight sides (PersonaAxis order, each -100..100), or null when Rimfolk keeps none for them.</summary>
        public static float[] Sides(Pawn p)
        {
            if (p == null) return null;
            try
            {
                return Persona.Values(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nature: a person's sides", e);
                return null;
            }
        }

        /// <summary>
        /// <paramref name="p"/> did (or was made to do) <paramref name="act"/>. Returns the strain it would bring (negative:
        /// the relief), which nothing applies yet.
        /// </summary>
        public static float Felt(Pawn p, NatureAct act)
        {
            if (p == null || !KinMemory.Ours(p)) return 0f;
            var sides = Sides(p);
            float strain = NatureMath.Strain(act, sides);
            float relief = strain > 0f ? 0f : NatureMath.Relief(act, sides);
            // Round d applies it through the heart (decision 19); until then it is only worked out.
            return strain > 0f ? strain : -relief;
        }
    }
}
