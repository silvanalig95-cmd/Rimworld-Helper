using System;
using System.Collections.Generic;

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
    /// Stress against one's nature (docs/design/stage2-diplomacy.md, item 13; the player's decision 19), with no game types so
    /// Tools/Tests and the balance simulator can check it: each act, the sides of a person it goes against (PersonaAxis), the
    /// strain it brings (up to 12, by how far beyond the threshold) or the relief (up to 6), scaled as the pressures' bad side
    /// is (the "Life's ups and downs" setting and hard times), one act a day per colonist, who it touches, the words' keys,
    /// and when a repeated act may leave a mark. Written in round a with every act listed and no effect, switched on in
    /// round d (release 2d.2) through the heart's own strain and relief (Diplomacy/Nature.cs).
    /// </summary>
    public static class NatureMath
    {
        /// <summary>On since release 2d.2 (decision 19); the "Against their nature" setting still turns it off.</summary>
        public static readonly bool SwitchedOn = true;

        public const float MaxStrain = 12f, MaxRelief = 6f;

        /// <summary>A side shows from 30 (PersonaMath.Shows): an act bites only past it.</summary>
        public const float Threshold = 30f;

        /// <summary>What an act always brings to whoever it concerns, whatever their nature.</summary>
        public const float AlwaysStrain = 8f;

        /// <summary>Just past the threshold an act still brings this share of the most it can (about 5 of 12: tuned with the balance simulator, Tools/Balance --nature, so it shows on the heart's page for days).</summary>
        public const float Floor = 0.4f;

        /// <summary>One act a day per colonist at most.</summary>
        public const int PerDay = 1;

        public const int DayTicks = 60000;

        /// <summary>A repeated act counts toward a mark when the same act comes again within a year (60 days).</summary>
        public const int YearDays = 60;

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

        /// <summary>The acts wired to where they happen in release 2d.2: every one but the dark scheme (release 2c.3's schemes are not built yet).</summary>
        public static bool Wired(NatureAct a) => a != NatureAct.DarkScheme;

        /// <summary>How far past the threshold a value lies, 0 to 1, on the given end of the side.</summary>
        public static float Beyond(float value, int sign, float threshold)
        {
            float v = value * Math.Sign(sign);
            if (v <= threshold) return 0f;
            float span = 100f - threshold;
            return span <= 0f ? 1f : Math.Min(1f, (v - threshold) / span);
        }

        private static float Value(float[] axes, PersonaAxis a) => axes != null && (int)a < axes.Length ? axes[(int)a] : 0f;

        private static float RuleStrain(NatureRule r, float[] axes)
        {
            float s = r.always ? AlwaysStrain : 0f;
            float beyond = Beyond(Value(axes, r.axis), r.sign, r.threshold);
            if (beyond > 0f) s = Math.Max(s, r.always ? AlwaysStrain + (MaxStrain - AlwaysStrain) * beyond : MaxStrain * Math.Max(Floor, beyond));
            return Math.Min(MaxStrain, s);
        }

        private static float RuleRelief(NatureRule r, float[] axes)
        {
            if (!r.easesOther) return 0f;
            float beyond = Beyond(Value(axes, r.axis), -r.sign, r.threshold);
            return beyond > 0f ? Math.Min(MaxRelief, MaxRelief * Math.Max(Floor, beyond)) : 0f;
        }

        /// <summary>
        /// The strain an act brings someone of these sides (<paramref name="axes"/>, by PersonaAxis, each -100..100): the
        /// strongest of its rules (an act that goes against two sides counts the worse one), up to 12. Some always bring 8.
        /// </summary>
        public static float Strain(NatureAct act, float[] axes)
        {
            float best = 0f;
            foreach (var r in Rules)
                if (r.act == act)
                    best = Math.Max(best, RuleStrain(r, axes));
            return best;
        }

        /// <summary>The relief an act brings someone at the other end of a side it eases (the cold by a dark scheme), up to 6; 0 for anyone strained by it.</summary>
        public static float Relief(NatureAct act, float[] axes)
        {
            if (Strain(act, axes) > 0f) return 0f;
            float best = 0f;
            foreach (var r in Rules)
                if (r.act == act)
                    best = Math.Max(best, RuleRelief(r, axes));
            return best;
        }

        /// <summary>What an act brings: its strain (above 0), else its relief (below 0), else 0.</summary>
        public static float Felt(NatureAct act, float[] axes)
        {
            float s = Strain(act, axes);
            return s > 0f ? s : -Relief(act, axes);
        }

        /// <summary>
        /// Two acts at once (a hostage given is also a season away): one act a day, so the one that weighs most counts, and
        /// only if none weighs on them the one that eases them most. Returns what it brings (as <see cref="Felt"/>).
        /// </summary>
        public static float Pick(IList<NatureAct> acts, float[] axes, out NatureAct which)
        {
            which = acts != null && acts.Count > 0 ? acts[0] : NatureAct.SworeFealty;
            if (acts == null) return 0f;
            float strain = 0f, relief = 0f;
            NatureAct eased = which;
            foreach (var a in acts)
            {
                float s = Strain(a, axes);
                if (s > strain)
                {
                    strain = s;
                    which = a;
                }
                float r = Relief(a, axes);
                if (r > relief)
                {
                    relief = r;
                    eased = a;
                }
            }
            if (strain > 0f) return strain;
            if (relief <= 0f) return 0f;
            which = eased;
            return -relief;
        }

        /// <summary>
        /// The rule behind what an act brings this person (the one that weighs most, or eases most), for its words: false when
        /// it brings them nothing.
        /// </summary>
        public static bool RuleOf(NatureAct act, float[] axes, out NatureRule rule)
        {
            rule = default;
            float best = 0f;
            bool found = false;
            foreach (var r in Rules)
            {
                if (r.act != act) continue;
                float s = RuleStrain(r, axes);
                if (s > best)
                {
                    best = s;
                    rule = r;
                    found = true;
                }
            }
            if (found) return true;
            foreach (var r in Rules)
            {
                if (r.act != act) continue;
                float s = RuleRelief(r, axes);
                if (s > best)
                {
                    best = s;
                    rule = r;
                    found = true;
                }
            }
            return found;
        }

        // ---------------- Scaled as the pressures' bad side is ----------------

        /// <summary>
        /// The strain as it is felt: the "Life's ups and downs" setting (PressureMath.Factor: gentle half, normal 0.85,
        /// demanding half again) and hard times (down to three quarters at the break line), as a pressure's bad mood is.
        /// Relief is not scaled, as a pressure's good side isn't.
        /// </summary>
        public static float Scale(float strain, LifePressure mode, float hard) =>
            strain <= 0f ? 0f : strain * PressureMath.Factor(mode) * PressureMath.HardTimesPressure(Math.Max(0f, Math.Min(1f, hard)));

        /// <summary>One act a day: a colonist last felt one at <paramref name="last"/> (-1: never).</summary>
        public static bool MayFeel(int last, int now, int dayTicks = DayTicks) => last < 0 || now - last >= dayTicks / PerDay || now < last;

        // ---------------- Who it touches ----------------

        /// <summary>The game's relations (PawnRelationDef names) that are close kin for a hostage given: parents, children, brothers and sisters, a spouse, a betrothed, a lover.</summary>
        public static readonly string[] CloseKinRelations = { "Parent", "Child", "Sibling", "Spouse", "Fiance", "Lover" };

        public static bool CloseKin(string relation) => relation != null && Array.IndexOf(CloseKinRelations, relation) >= 0;

        /// <summary>A ward's parents feel their child sent away (the game's Parent relation, read from the child's side).</summary>
        public static bool WardParent(string relationOfChildToThem) => relationOfChildToThem == "Parent";

        /// <summary>
        /// Who feels a broken truce or pact: whoever carried out the deed that broke it, when the game names them (the blow's
        /// own hand, the one who took a captive, the seller at the trade, the caravan's first who attacked one of theirs), one
        /// of the colony's; nobody when the deed was the whole colony's (a town attacked, the game names nobody).
        /// </summary>
        public static bool BreakerFeels(bool named, bool ours) => named && ours;

        // ---------------- Marks for repeated acts ----------------

        /// <summary>The families of marks a nature act may leave (Defs/PersonaDefs/Rimfolk_PersonaMarks_Nature.xml).</summary>
        public const string MarkOath = "NatureOath", MarkWordBroken = "NatureWordBroken", MarkNerve = "NatureNerve", MarkAbroad = "NatureAbroad";

        /// <summary>
        /// The family of marks this act may leave, or null: an oath sworn against their pride may humble a proud one (each
        /// oath); the same act again within a year of the last (<paramref name="lastAct"/> at <paramref name="lastTick"/>, -1:
        /// none) may change the side it went against: a word broken twice makes a trusting one warier, a challenge faced twice
        /// makes a timid one bolder, a second season away makes a closed one more open. Only for an act that went against them
        /// (<paramref name="strained"/>); the mark's own conditions and chance decide the rest.
        /// </summary>
        public static string MarkFamily(NatureAct act, bool strained, int lastAct, int lastTick, int now)
        {
            if (!strained) return null;
            if (act == NatureAct.SworeFealty) return MarkOath;
            bool again = lastAct == (int)act && lastTick >= 0 && now - lastTick >= 0 && now - lastTick < (long)YearDays * DayTicks;
            if (!again) return null;
            switch (act)
            {
                case NatureAct.BrokeTruce: return MarkWordBroken;
                case NatureAct.AcceptedChallenge: return MarkNerve;
                case NatureAct.SeasonAway: return MarkAbroad;
                default: return null;
            }
        }

        // ---------------- The words' keys ----------------

        /// <summary>What they did, as the heart's page says it: CC_Nature_Act_ACT (a role of its own: CC_Nature_Act_ACT_ROLE).</summary>
        public static string ActKey(NatureAct act, string role = null) => "CC_Nature_Act_" + act + (string.IsNullOrEmpty(role) ? "" : "_" + role);

        /// <summary>
        /// The side of them it went against or suited ("her pride", "his settled ways"): CC_Nature_Side_AXIS_High or _Low by
        /// the end it touched (the other end for relief); an act felt whatever their nature has its own (CC_Nature_Side_ACT).
        /// </summary>
        public static string SideKey(NatureRule r, bool eased)
        {
            if (r.always && !eased) return "CC_Nature_Side_" + r.act;
            int end = eased ? -r.sign : r.sign;
            return "CC_Nature_Side_" + r.axis + (end > 0 ? "_High" : "_Low");
        }

        /// <summary>Every side key a wired rule can name (for the tests and the def validator).</summary>
        public static IEnumerable<string> SideKeys()
        {
            var seen = new HashSet<string>();
            foreach (var r in Rules)
            {
                if (!Wired(r.act)) continue;
                if (seen.Add(SideKey(r, false))) yield return SideKey(r, false);
                if (r.easesOther && seen.Add(SideKey(r, true))) yield return SideKey(r, true);
            }
        }
    }
}
