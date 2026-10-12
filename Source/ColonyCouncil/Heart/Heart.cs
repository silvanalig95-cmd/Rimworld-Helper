using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart layer's face to the rest of Rimfolk: who has a heart, how strained they are, what they feel, and the
    /// small nudges feelings and needs give wishes, life goals, dreams and moments. Every hook returns 1 (no change)
    /// while the feature is off, so callers never need to check.
    /// </summary>
    public static class Heart
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The feature is on and a game is running.</summary>
        public static bool On => S != null && S.heart && Current.Game != null;

        /// <summary>
        /// Someone the heart layer looks after: one of the colony's own people (free colonists and slaves), humanlike, with
        /// an inner life, not a baby, not a quest lodger. Prisoners, guests and visitors have none.
        /// </summary>
        public static bool Eligible(Pawn p, bool checkAlive = true)
        {
            if (p == null || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            if (checkAlive && (p.Dead || p.Destroyed)) return false;
            if (!(p.IsFreeColonist || p.IsSlaveOfColony) || p.IsQuestLodger()) return false;
            if (p.DevelopmentalStage.Baby()) return false;
            return People.HasInnerLife(p);
        }

        /// <summary>The colonist's heart while the feature is on, else null.</summary>
        public static PawnHeart Of(Pawn p) => On && p != null ? HeartTracker.Instance?.For(p) : null;

        public static float StrainOf(Pawn p) => Of(p)?.strain ?? 0f;

        public static StrainBand BandOf(Pawn p) => HeartMath.BandFor(StrainOf(p));

        /// <summary>The one or two feelings that stand out (the strongest, and the next if it's at least half as strong).</summary>
        public static void Dominant(PawnHeart h, List<FeelingNow> into)
        {
            into.Clear();
            if (h?.feelings == null || h.feelings.Count == 0) return;
            into.Add(h.feelings[0]);
            if (h.feelings.Count > 1 && h.feelings[1].intensity >= h.feelings[0].intensity * 0.55f) into.Add(h.feelings[1]);
        }

        /// <summary>The average strain of these colonists (for the colony page and the rest decree's petition).</summary>
        public static float ColonyStrain(IEnumerable<Pawn> pawns)
        {
            if (!On) return 0f;
            float sum = 0f;
            int n = 0;
            foreach (var p in pawns)
            {
                var h = Of(p);
                if (h == null) continue;
                sum += h.strain;
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        // ---------------- Hooks for the rest of Rimfolk ----------------

        /// <summary>
        /// How much likelier a moment between two colonists is, from what the one starting it feels: anger, envy, disgust
        /// and frustration raise frictions; gratitude, love, joy and loneliness raise warm moments; shame makes them start
        /// fewer warm ones; grief raises moments of shared grief. 1 when the feature is off.
        /// </summary>
        public static float MomentWeightFactor(Pawn initiator, Pawn recipient, bool friction) => MomentWeightFactor(initiator, recipient, friction, null);

        /// <summary>
        /// As above; <paramref name="kind"/> "grief" marks a moment of grief shared (it then also counts as warm, so
        /// grief makes it likelier).
        /// </summary>
        public static float MomentWeightFactor(Pawn initiator, Pawn recipient, bool friction, string kind)
        {
            var h = Of(initiator);
            if (h == null || h.feelings.Count == 0) return 1f;
            float f = 1f;
            bool grief = kind == "grief";
            for (int i = 0; i < h.feelings.Count; i++)
            {
                var fn = h.feelings[i];
                var d = fn.def;
                float share = fn.Share;
                f *= HeartMath.Nudge(friction ? d.friction : d.warm, share);
                if (grief && !friction) f *= HeartMath.Nudge(d.griefSharing, share);
                if (fn.target != null && fn.target == recipient && d.targetFriction == friction) f *= HeartMath.Nudge(d.towardTarget, share);
            }
            return Mathf.Clamp(f, 0.5f, 2f);
        }

        /// <summary>
        /// How much likelier a wish is for this colonist, from their feelings and the heart's need they lack most. Never
        /// less likely, and at most half again as likely.
        /// </summary>
        public static float WishFactor(Pawn p, WishDef def)
        {
            var h = Of(p);
            if (h == null || def == null) return 1f;
            float f = 1f;
            for (int i = 0; i < h.feelings.Count; i++)
            {
                var d = h.feelings[i].def;
                if ((d.wishes != null && d.wishes.Contains(def)) || (d.wishCategories != null && d.wishCategories.Contains(def.category)))
                    f = Mathf.Max(f, HeartMath.Nudge(d.wishFactor, h.feelings[i].Share));
            }
            var lack = LowestNeed(h, out float value);
            if (lack != null && ((lack.wishes != null && lack.wishes.Contains(def)) || (lack.wishCategories != null && lack.wishCategories.Contains(def.category))))
                f *= NeedNudge(value);
            return Mathf.Clamp(f, 1f, 1.5f);
        }

        /// <summary>How much likelier a life goal is for someone lacking the need it answers (at most half again).</summary>
        public static float GoalFactor(Pawn p, AspirationDef def)
        {
            var h = Of(p);
            if (h == null || def == null) return 1f;
            var lack = LowestNeed(h, out float value);
            if (lack?.goals == null || !lack.goals.Contains(def)) return 1f;
            return Mathf.Clamp(NeedNudge(value) * 1.15f, 1f, 1.5f);
        }

        /// <summary>A need below 45 makes what answers it up to 35% likelier.</summary>
        private static float NeedNudge(float value) => value >= 45f ? 1f : 1f + 0.35f * (45f - value) / 45f;

        /// <summary>The need of the heart they lack most, if any is below 45.</summary>
        public static HeartNeedDef LowestNeed(PawnHeart h, out float value)
        {
            value = 100f;
            if (h?.needs == null) return null;
            int best = -1;
            for (int i = 0; i < h.needs.Length; i++)
                if (h.needs[i].value < value)
                {
                    value = h.needs[i].value;
                    best = i;
                }
            if (best < 0 || value >= 45f) return null;
            return NeedDef((HeartNeed)best);
        }

        private static Dictionary<HeartNeed, HeartNeedDef> needDefs;

        public static HeartNeedDef NeedDef(HeartNeed need)
        {
            if (needDefs == null)
            {
                needDefs = new Dictionary<HeartNeed, HeartNeedDef>();
                foreach (var d in DefDatabase<HeartNeedDef>.AllDefsListForReading) needDefs[d.need] = d;
            }
            return needDefs.TryGetValue(need, out var def) ? def : null;
        }

        /// <summary>How a dream is colored: nightmares come more often to the worn down and the afraid, sweet dreams to the joyful.</summary>
        public static float DreamFactor(Pawn p, DreamKind kind)
        {
            var h = Of(p);
            if (h == null) return 1f;
            float f = kind == DreamKind.Nightmare ? HeartMath.NightmareFactor(h.Band) : 1f;
            for (int i = 0; i < h.feelings.Count; i++)
            {
                var d = h.feelings[i].def;
                if (d.dreams == null) continue;
                foreach (var k in d.dreams)
                    if (k.kind == kind) f *= HeartMath.Nudge(k.factor, h.feelings[i].Share);
            }
            return Mathf.Clamp(f, 0.4f, 3f);
        }

        /// <summary>How patient they are with a wish (frustration makes them less so).</summary>
        public static float PatienceFactor(Pawn p)
        {
            var h = Of(p);
            if (h == null) return 1f;
            float f = 1f;
            for (int i = 0; i < h.feelings.Count; i++) f *= HeartMath.Nudge(h.feelings[i].def.patience, h.feelings[i].Share);
            return Mathf.Clamp(f, 0.75f, 1.25f);
        }

        /// <summary>How fast strain drains, from feelings (joy, pride, hope and calm speed it, despair slows it).</summary>
        public static float FeelingRelief(PawnHeart h)
        {
            float f = 1f;
            for (int i = 0; i < h.feelings.Count; i++) f *= HeartMath.Nudge(h.feelings[i].def.relief, h.feelings[i].Share);
            return Mathf.Clamp(f, 0.6f, 1.6f);
        }

        public static float EpisodeFactor(PawnHeart h)
        {
            float f = 1f;
            for (int i = 0; i < h.feelings.Count; i++) f *= HeartMath.Nudge(h.feelings[i].def.episode, h.feelings[i].Share);
            return Mathf.Clamp(f, 0.5f, 1.5f);
        }

        // ---------------- Traits ----------------

        private static bool traitsLooked;
        private static TraitDef nerves, naturalMood, masochist, tough;

        /// <summary>The traits that change how strain builds and drains, as flags.</summary>
        public static StrainTraits TraitsOf(Pawn p)
        {
            var set = p?.story?.traits;
            if (set == null) return StrainTraits.None;
            if (!traitsLooked)
            {
                traitsLooked = true;
                nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                naturalMood = DefDatabase<TraitDef>.GetNamedSilentFail("NaturalMood");
                masochist = DefDatabase<TraitDef>.GetNamedSilentFail("Masochist");
                tough = DefDatabase<TraitDef>.GetNamedSilentFail("Tough");
            }
            var t = StrainTraits.None;
            if (nerves != null && set.HasTrait(nerves))
            {
                int d = set.DegreeOfTrait(nerves);
                t |= d >= 2 ? StrainTraits.IronWilled : d == 1 ? StrainTraits.Steadfast : d == -1 ? StrainTraits.Nervous : d <= -2 ? StrainTraits.Volatile : 0;
            }
            if (naturalMood != null && set.HasTrait(naturalMood))
            {
                int d = set.DegreeOfTrait(naturalMood);
                t |= d >= 2 ? StrainTraits.Sanguine : d == 1 ? StrainTraits.Optimist : d == -1 ? StrainTraits.Pessimist : d <= -2 ? StrainTraits.Depressive : 0;
            }
            if (set.HasTrait(TraitDefOf.Psychopath)) t |= StrainTraits.Psychopath;
            if (set.HasTrait(TraitDefOf.Bloodlust)) t |= StrainTraits.Bloodlust;
            if (set.HasTrait(TraitDefOf.Wimp)) t |= StrainTraits.Wimp;
            if (set.HasTrait(TraitDefOf.Brawler)) t |= StrainTraits.Brawler;
            if (set.HasTrait(TraitDefOf.Undergrounder)) t |= StrainTraits.Undergrounder;
            if (set.HasTrait(TraitDefOf.Ascetic)) t |= StrainTraits.Ascetic;
            if (masochist != null && set.HasTrait(masochist)) t |= StrainTraits.Masochist;
            if (tough != null && set.HasTrait(tough)) t |= StrainTraits.Tough;
            return t;
        }

        /// <summary>Whether someone has a trait by defName, at a degree ("Nerves:2") or any.</summary>
        public static bool HasTrait(Pawn p, string spec)
        {
            var set = p?.story?.traits;
            if (set == null || spec.NullOrEmpty()) return false;
            string name = spec;
            int degree = int.MinValue;
            int colon = spec.IndexOf(':');
            if (colon > 0)
            {
                name = spec.Substring(0, colon);
                if (!int.TryParse(spec.Substring(colon + 1), out degree)) degree = int.MinValue;
            }
            return HasTrait(p, name, degree);
        }

        /// <summary>Whether someone has a trait by defName, at this degree (int.MinValue: any).</summary>
        public static bool HasTrait(Pawn p, string name, int degree)
        {
            var set = p?.story?.traits;
            if (set == null || name.NullOrEmpty()) return false;
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(name);
            if (def == null || !set.HasTrait(def)) return false;
            return degree == int.MinValue || set.DegreeOfTrait(def) == degree;
        }

        // ---------------- The rest decree ----------------

        private static DecreeDef restDecree;
        private static bool restLooked;

        /// <summary>The decree that speeds recovery (its relief factor is above 1), if any.</summary>
        public static DecreeDef RestDecree
        {
            get
            {
                if (!restLooked)
                {
                    restLooked = true;
                    foreach (var d in DefDatabase<DecreeDef>.AllDefsListForReading)
                        if (d.strainRelief > 1f)
                        {
                            restDecree = d;
                            break;
                        }
                }
                return restDecree;
            }
        }

        /// <summary>The factor on strain relief from decrees in force (days of rest).</summary>
        public static float DecreeRelief()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.decrees?.active == null) return 1f;
            float f = 1f;
            foreach (var a in comp.decrees.active)
                if (a?.def != null && a.def.strainRelief > 0f) f *= a.def.strainRelief;
            return f;
        }

        /// <summary>The work cost of decrees in force that the heart layer carries (days of rest), as a factor.</summary>
        public static float DecreeWorkFactor(out DecreeDef which)
        {
            which = null;
            var comp = CouncilGameComponent.Instance;
            if (comp?.decrees?.active == null) return 1f;
            float f = 1f;
            foreach (var a in comp.decrees.active)
                if (a?.def != null && a.def.workFactor > 0f && a.def.workFactor != 1f)
                {
                    f *= a.def.workFactor;
                    which = a.def;
                }
            return f;
        }

        public static void ResetCaches()
        {
            needDefs = null;
            restLooked = false;
            restDecree = null;
            traitsLooked = false;
        }
    }
}
