using System;

namespace ColonyCouncil
{
    /// <summary>The acts of diplomacy that go against someone's nature (docs/design/stage2-diplomacy.md, item 13).</summary>
    public enum NatureAct
    {
        /// <summary>Swearing fealty: the one who swears, if proud.</summary>
        SworeFealty,
        /// <summary>A dark scheme: the schemer, if warm or trusting (eased for the cold).</summary>
        DarkScheme,
        /// <summary>Breaking a truce or a pact: whoever carries it out, if trusting.</summary>
        BrokeTruce,
        /// <summary>Given as a hostage: the hostage, if timid.</summary>
        GivenHostage,
        /// <summary>One of their close kin given as a hostage: if warm.</summary>
        KinHostage,
        /// <summary>Their child sent away as a ward: the parents, if warm.</summary>
        WardSent,
        /// <summary>A match against their heart: someone who doesn't love the one chosen, or loves another.</summary>
        MatchAgainstHeart,
        /// <summary>Refusing a challenge: the hot-tempered and the proud.</summary>
        RefusedChallenge,
        /// <summary>Accepting one: the timid.</summary>
        AcceptedChallenge,
        /// <summary>A season away as envoy or hostage: the closed (eased for the open).</summary>
        SeasonAway,
        /// <summary>Haggling for a captive their family is waiting for: the warm.</summary>
        HaggledForKin,
        /// <summary>A truce with the people they swore vengeance on: the avenger, always.</summary>
        TruceWithFoe
    }

    /// <summary>One side of a person an act goes against: the side, which end of it (+1 high, -1 low), from how far it bites, and whether the other end is eased by it.</summary>
    public readonly struct NatureRule
    {
        public readonly NatureAct act;
        public readonly PersonaAxis axis;
        public readonly int sign;
        public readonly float threshold;
        /// <summary>Those at the other end of the side are eased by it instead (the cold by a dark scheme, the open by a season away).</summary>
        public readonly bool easesOther;
        /// <summary>Felt by anyone it concerns, whatever their nature (a match against the heart, a truce with one's foe).</summary>
        public readonly bool always;

        public NatureRule(NatureAct act, PersonaAxis axis, int sign, float threshold, bool easesOther = false, bool always = false)
        {
            this.act = act;
            this.axis = axis;
            this.sign = sign;
            this.threshold = threshold;
            this.easesOther = easesOther;
            this.always = always;
        }
    }

    /// <summary>
    /// Stress against one's nature (docs/design/stage2-diplomacy.md, item 13), with no game types so Tools/Tests can check
    /// it: each act, the sides of a person it goes against (PersonaAxis), and the strain it brings (up to 12, by how far
    /// beyond the threshold) or the relief (up to 6). The service (Nature.cs) is written now with every act listed and no
    /// effect; each round names its own acts as it ships, and round d switches it on (decision 19), through the heart's own
    /// strain and relief (new kinds at the end of their lists then).
    /// </summary>
    public static class NatureMath
    {
        /// <summary>Off until round d: the acts are listed and worked out, and nothing is felt.</summary>
        public static readonly bool SwitchedOn = false;

        public const float MaxStrain = 12f, MaxRelief = 6f;

        /// <summary>A side shows from 30 (PersonaMath.Shows): an act bites only past it.</summary>
        public const float Threshold = 30f;

        /// <summary>What an act always brings to whoever it concerns, whatever their nature.</summary>
        public const float AlwaysStrain = 8f;

        /// <summary>One act a day per colonist at most.</summary>
        public const int PerDay = 1;

        public static readonly NatureRule[] Rules =
        {
            new NatureRule(NatureAct.SworeFealty, PersonaAxis.Pride, 1, Threshold),
            new NatureRule(NatureAct.DarkScheme, PersonaAxis.Warmth, 1, Threshold, easesOther: true),
            new NatureRule(NatureAct.DarkScheme, PersonaAxis.Trust, 1, Threshold),
            new NatureRule(NatureAct.BrokeTruce, PersonaAxis.Trust, 1, Threshold),
            new NatureRule(NatureAct.GivenHostage, PersonaAxis.Nerve, -1, Threshold),
            new NatureRule(NatureAct.KinHostage, PersonaAxis.Warmth, 1, Threshold),
            new NatureRule(NatureAct.WardSent, PersonaAxis.Warmth, 1, Threshold),
            new NatureRule(NatureAct.MatchAgainstHeart, PersonaAxis.Warmth, 1, 0f, always: true),
            new NatureRule(NatureAct.RefusedChallenge, PersonaAxis.Temper, 1, Threshold),
            new NatureRule(NatureAct.RefusedChallenge, PersonaAxis.Pride, 1, Threshold),
            new NatureRule(NatureAct.AcceptedChallenge, PersonaAxis.Nerve, -1, Threshold),
            new NatureRule(NatureAct.SeasonAway, PersonaAxis.Openness, -1, Threshold, easesOther: true),
            new NatureRule(NatureAct.HaggledForKin, PersonaAxis.Warmth, 1, Threshold),
            new NatureRule(NatureAct.TruceWithFoe, PersonaAxis.Pride, 1, 0f, always: true)
        };

        /// <summary>How far past the threshold a value lies, 0 to 1, on the given end of the side.</summary>
        public static float Beyond(float value, int sign, float threshold)
        {
            float v = value * Math.Sign(sign);
            if (v <= threshold) return 0f;
            float span = 100f - threshold;
            return span <= 0f ? 1f : Math.Min(1f, (v - threshold) / span);
        }

        /// <summary>
        /// The strain an act brings someone of these sides (<paramref name="axes"/>, by PersonaAxis, each -100..100): the
        /// strongest of its rules (an act that goes against two sides counts the worse one), up to 12. Some always bring 8.
        /// </summary>
        public static float Strain(NatureAct act, float[] axes)
        {
            float best = 0f;
            foreach (var r in Rules)
            {
                if (r.act != act) continue;
                float s = r.always ? AlwaysStrain : 0f;
                float v = axes != null && (int)r.axis < axes.Length ? axes[(int)r.axis] : 0f;
                float beyond = Beyond(v, r.sign, r.threshold);
                if (beyond > 0f) s = Math.Max(s, r.always ? AlwaysStrain + (MaxStrain - AlwaysStrain) * beyond : MaxStrain * Math.Max(0.25f, beyond));
                best = Math.Max(best, s);
            }
            return Math.Min(MaxStrain, best);
        }

        /// <summary>The relief an act brings someone at the other end of a side it eases (the cold by a dark scheme), up to 6; 0 for anyone strained by it.</summary>
        public static float Relief(NatureAct act, float[] axes)
        {
            if (Strain(act, axes) > 0f) return 0f;
            float best = 0f;
            foreach (var r in Rules)
            {
                if (r.act != act || !r.easesOther) continue;
                float v = axes != null && (int)r.axis < axes.Length ? axes[(int)r.axis] : 0f;
                float beyond = Beyond(v, -r.sign, r.threshold);
                if (beyond > 0f) best = Math.Max(best, MaxRelief * Math.Max(0.25f, beyond));
            }
            return Math.Min(MaxRelief, best);
        }

        /// <summary>One act a day: a colonist last felt one at <paramref name="last"/> (-1: never).</summary>
        public static bool MayFeel(int last, int now, int dayTicks = 60000) => last < 0 || now - last >= dayTicks / PerDay;
    }
}
