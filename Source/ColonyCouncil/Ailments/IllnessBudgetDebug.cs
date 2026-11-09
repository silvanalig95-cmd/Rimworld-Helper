using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode (Debug actions, "Rimfolk"): a serious illness of Rimfolk's, the game's own disease as the storyteller sends it, and the budget's log.</summary>
    public static class IllnessBudgetDebug
    {
        [DebugAction("Rimfolk", "Illness budget: one of Rimfolk's serious illnesses now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RimfolkSerious()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var options = AilmentTracker.Incidents.Where(d => AilmentTracker.InfoOf(d.diseaseIncident)?.serious ?? false).ToList();
            foreach (var def in options.InRandomOrder())
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                parms.forced = true;
                if (!def.Worker.CanFireNow(parms)) continue;
                if (Find.Storyteller.TryFire(new FiringIncident(def, null, parms)))
                {
                    Messages.Message("Rimfolk's " + def.diseaseIncident.label + " now: this home owes the game one skip.", MessageTypeDefOf.NeutralEvent, false);
                    return;
                }
            }
            Messages.Message("None of Rimfolk's serious illnesses can start here now (nobody it could take).", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Illness budget: the game's own disease, as the storyteller sends it", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GameDisease()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var comp = Find.Storyteller?.storytellerComps?.FirstOrDefault(c => c?.props is StorytellerCompProperties_Disease p && p.category == IncidentCategoryDefOf.DiseaseHuman);
            if (comp == null)
            {
                Messages.Message("This storyteller sends none of the game's human diseases.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.DiseaseHuman, map);
            var usable = DefDatabase<IncidentDef>.AllDefsListForReading
                .Where(d => d.category == IncidentCategoryDefOf.DiseaseHuman && !(d.Worker is IncidentWorker_Ailment) && map.Biome.CommonalityOfDisease(d) > 0f && d.Worker.CanFireNow(parms))
                .ToList();
            if (!usable.TryRandomElementByWeight(d => map.Biome.CommonalityOfDisease(d), out var def))
            {
                Messages.Message("None of the game's diseases of this land can come now.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            int held = IllnessBudget.LedgerOf(map, false)?.held ?? 0, anyway = IllnessBudget.LedgerOf(map, false)?.cameAnyway ?? 0;
            bool fired = Find.Storyteller.TryFire(new FiringIncident(def, comp, parms));
            bool wasHeld = (IllnessBudget.LedgerOf(map, false)?.held ?? 0) > held, letGo = (IllnessBudget.LedgerOf(map, false)?.cameAnyway ?? 0) > anyway;
            Messages.Message("The game's own " + def.label + ": " + (wasHeld ? "held back by the budget." : fired ? (letGo ? "it came all the same (the one-in-four), and the debt was let go." : "it came.") : "it couldn't come."),
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Illness budget: the ledger of every home (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Log_() => Log.Message(IllnessBudget.Report());
    }
}
