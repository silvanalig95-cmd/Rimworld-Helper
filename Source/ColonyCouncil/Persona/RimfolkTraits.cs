using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own traits (Defs/TraitDefs/Rimfolk_Traits.xml): never on a new arrival (commonality 0), only earned in
    /// the colony, each the end of a habit lived for months (Defs/PersonaDefs/Rimfolk_TraitChanges_Habits.xml). Where
    /// another mod already has the trait, its own is used: Vanilla Traits Expanded's by name in the rules (insteadOf),
    /// and any other by label here, so no colonist ends up with two of the same.
    /// </summary>
    public static class RimfolkTraits
    {
        private static readonly Dictionary<TraitDef, bool> doubled = new Dictionary<TraitDef, bool>();

        public static bool Ours(TraitDef td) => td?.defName != null && td.defName.StartsWith("Rimfolk_", StringComparison.Ordinal);

        /// <summary>One of ours that another mod's trait already is (the same label): ours stays out of the game.</summary>
        public static bool Doubled(TraitDef td)
        {
            if (!Ours(td)) return false;
            if (doubled.TryGetValue(td, out bool d)) return d;
            d = false;
            var mine = new HashSet<string>();
            foreach (var dd in td.degreeDatas ?? new List<TraitDegreeData>())
                if (!dd.label.NullOrEmpty()) mine.Add(dd.label.ToLowerInvariant());
            foreach (var other in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                if (other == td || Ours(other) || other.degreeDatas == null) continue;
                foreach (var dd in other.degreeDatas)
                    if (!dd.label.NullOrEmpty() && mine.Contains(dd.label.ToLowerInvariant())) d = true;
                if (d) break;
            }
            doubled[td] = d;
            return d;
        }
    }

    /// <summary>The tidy in a filthy room (the habit's messy room, ThoughtWorker_Quirk.Holds).</summary>
    public class ThoughtWorker_TidyMess : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.MoodEffects || !s.Negatives) return ThoughtState.Inactive;
            return ThoughtWorker_Quirk.Holds(QuirkCondition.MessyRoom, null, p) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
