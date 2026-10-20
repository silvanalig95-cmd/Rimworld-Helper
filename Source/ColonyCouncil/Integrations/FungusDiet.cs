using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A ready-made food policy for a colonist whose faith requires fungus (Alpha Memes' fungus diet, or any mod's
    /// precept named so): with it, eating anything but fungus or insect food makes them throw up at once, so a
    /// policy that allows only fungus meals, raw fungus and insect food keeps them fed. Made once per game, the first
    /// day such a colonist is in the colony, with a message naming who needs it; assigning it is the player's choice.
    /// </summary>
    public static class FungusDiet
    {
        public static void Daily(CouncilGameComponent comp)
        {
            if (!ModsConfig.IdeologyActive || comp == null || comp.fungusDietMade) return;
            var need = PawnsFinder.AllMaps_FreeColonists.Where(Requires).ToList();
            if (need.Count == 0) return;
            var db = Current.Game?.foodRestrictionDatabase;
            if (db == null) return;
            string label = "CC_FungusDiet_Label".Translate();
            var policy = db.AllFoodRestrictions.FirstOrDefault(x => x?.label == label) ?? db.MakeNewFoodRestriction();
            if (policy?.filter == null) return;
            policy.label = label;
            policy.filter.SetDisallowAll();
            int allowed = 0;
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!Fits(d)) continue;
                policy.filter.SetAllow(d, true);
                allowed++;
            }
            comp.fungusDietMade = true;
            if (allowed == 0) return;
            string names = TextUtil.JoinAnd(need.Select(p => p.LabelShort).ToList());
            Notify.Message("CC_FungusDiet_Made".Translate(names, label).Resolve(), need[0], MessageTypeDefOf.NeutralEvent, Importance.Important);
        }

        /// <summary>Their faith requires fungus: anything else comes back up.</summary>
        public static bool Requires(Pawn p)
        {
            var ideo = p?.Ideo;
            if (ideo == null) return false;
            foreach (var pr in ideo.PreceptsListForReading)
                if (pr?.def?.defName != null && pr.def.defName.Contains("FungusEating_Required")) return true;
            return false;
        }

        /// <summary>Fungus meals, raw fungus, insect jelly and insect meat: what such a faith lets them keep down.</summary>
        private static bool Fits(ThingDef d)
        {
            if (d?.ingestible == null || d.IsDrug || d.IsCorpse || !d.IsNutritionGivingIngestible) return false;
            if (d.defName == FungusMeals.MealName || FungusMeals.IsRawFungus(d)) return true;
            if (d == ThingDefOf.InsectJelly || d.defName.Contains("InsectJelly")) return true;
            return d.IsMeat && d.ingestible.sourceDef?.race?.FleshType == FleshTypeDefOf.Insectoid;
        }
    }
}
