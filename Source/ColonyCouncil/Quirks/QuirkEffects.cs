using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a quirk does, in words, for its line on the Heart section: its knack (the stat it moves), its mood and when,
    /// anything else it does (QuirkDef.effect), or that it does nothing to play; and how long it lasts (for good, while
    /// they keep at its work, or fading with time; never a count of days).
    /// </summary>
    public static class QuirkEffects
    {
        /// <summary>"Cleaning speed +10%, mood -2 in a messy room, tidies up in their free time" (no full stop), or the "no effect" words.</summary>
        public static string Effects(Quirk q)
        {
            if (q?.def == null) return null;
            var parts = new List<string>();
            var s = ColonyCouncilMod.Settings;
            // The knack, at the stage they have (a deep-rooted habit's second).
            var stages = q.def.hediff?.stages;
            if (!stages.NullOrEmpty())
            {
                var stage = stages[q.Deep && stages.Count > 1 ? 1 : 0];
                if (stage.statOffsets != null)
                    foreach (var m in stage.statOffsets)
                        if (m?.stat != null) parts.Add(m.stat.LabelCap + " " + m.stat.ValueToString(m.value, ToStringNumberSense.Offset));
                if (stage.statFactors != null)
                    foreach (var m in stage.statFactors)
                        if (m?.stat != null) parts.Add(m.stat.LabelCap + " " + m.value.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
            }
            string mood = s == null || s.MoodEffects ? Mood(q) : null;
            if (!mood.NullOrEmpty()) parts.Add(parts.Count == 0 ? mood.CapitalizeFirst() : mood);
            if (!q.def.effect.NullOrEmpty())
            {
                string e = q.def.effect.Formatted(q.pawn.Named("PAWN")).Resolve();
                parts.Add(parts.Count == 0 ? e.CapitalizeFirst() : e);
            }
            return parts.Count == 0 ? "CC_QuirkFx_None".Translate().ToString() : string.Join(", ", parts);
        }

        /// <summary>How long it lasts: "for good", "stays while they keep at cleaning", "deep-rooted", "fades with time".</summary>
        public static string Lasts(Quirk q)
        {
            if (q?.def == null) return null;
            if (q.expiresTick <= 0) return "CC_QuirkFx_ForGood".Translate();
            string kept = q.def.Practised ? Phrase("CC_QuirkFx_Kept", q.def.practice[0]) : null;
            if (q.Deep) return kept == null ? "CC_QuirkFx_Deep".Translate().ToString() : "CC_QuirkFx_DeepKept".Translate(kept).Resolve();
            return kept ?? "CC_QuirkFx_Fades".Translate().ToString();
        }

        /// <summary>
        /// The line for the Heart section: "Quirk: can't stand a mess (from all the cleaning). Cleaning speed +10%, mood
        /// -2 in a messy room. Stays while they keep at cleaning."
        /// </summary>
        public static string Line(Quirk q)
        {
            string from = From(q);
            string name = from.NullOrEmpty() ? q.Label : "CC_QuirkFx_NameFrom".Translate(q.Label, from).Resolve();
            return "CC_QuirkFx_Line".Translate(name, Effects(q), Lasts(q).CapitalizeFirst()).Resolve();
        }

        /// <summary>How it came, in a few words ("from all the cleaning", "simply who they are"), or null when unknown (older saves).</summary>
        public static string From(Quirk q)
        {
            if (q?.cause == null) return null;
            if (q.cause == QuirkCause.Practice && q.def.Practised) return Phrase("CC_QuirkFrom_practice", q.def.practice[0]);
            string key = "CC_QuirkFrom_" + q.cause;
            return key.CanTranslate() ? key.Translate().ToString() : null;
        }

        /// <summary>A phrase about a practice: its own words for "work", "night", "morning" and "outdoors", else the work's name in the general one.</summary>
        private static string Phrase(string key, string practice)
        {
            string own = key + "_" + practice;
            if (Array.IndexOf(Practice.Special, practice) >= 0 && own.CanTranslate()) return own.Translate();
            return key.Translate(Practice.Words(practice)).Resolve();
        }

        private static string Mood(Quirk q)
        {
            var thought = q.def.thought;
            if (thought?.stages == null || thought.stages.Count == 0) return null;
            float First(int i) => i < thought.stages.Count && thought.stages[i] != null ? PracticeMath.Mood(thought.stages[i].baseMoodEffect, q.Deep) : 0f;
            float a = First(0);
            if (q.def.condition == QuirkCondition.LuckyItem)
                return "CC_QuirkFx_MoodLucky".Translate(Signed(a), Signed(First(1))).Resolve();
            if (a == 0f) return null;
            string first = "CC_QuirkFx_Mood".Translate(Signed(a), When(q.def.condition, q.def)).Resolve();
            if (q.def.elseCondition == QuirkCondition.None || thought.stages.Count < 2) return first;
            return "CC_QuirkFx_MoodElse".Translate(first, Signed(First(1)), When(q.def.elseCondition, q.def)).Resolve();
        }

        private static string When(QuirkCondition c, QuirkDef def)
        {
            if (c == QuirkCondition.WorkingAt) return "CC_QuirkWhen_WorkingAt".Translate(def.workType?.gerundLabel ?? def.workType?.label ?? "").Resolve();
            string key = "CC_QuirkWhen_" + c;
            return key.CanTranslate() ? key.Translate().ToString() : "";
        }

        private static string Signed(float v)
        {
            int n = (int)System.Math.Round(v);
            return n > 0 ? "+" + n : n.ToString();
        }
    }
}
