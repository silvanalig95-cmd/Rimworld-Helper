using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): see why a colonist's traits would or wouldn't change, make the first change
    /// that holds happen, give a trait as Rimfolk gives it (so its way out can be tried), or make a way out of a bad
    /// trait happen now, whatever its evidence.
    /// </summary>
    public static class TraitChangeDebug
    {
        [DebugAction("Rimfolk", "Traits that change: why or why not", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WhyNot(Pawn p)
        {
            Log.Message("[Rimfolk] " + TraitChanges.DebugFor(p, false));
            Messages.Message("Traits that change: the reasons are in the log for " + p.LabelShort + ".", p, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Traits that change: the first that holds, now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChangeNow(Pawn p) => Log.Message("[Rimfolk] " + TraitChanges.DebugFor(p, true));

        [DebugAction("Rimfolk", "Traits that change: give a trait as Rimfolk would...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GainNow(Pawn p) => Menu(p, false, "(no gain the gate allows for " + p.LabelShort + ")");

        [DebugAction("Rimfolk", "Traits that change: a way out of a bad trait, now...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WayOutNow(Pawn p) => Menu(p, true, "(no way out for " + p.LabelShort + ": no bad trait a rule leads out of, or one from a gene, a backstory or the scenario)");

        private static void Menu(Pawn p, bool waysOut, string none)
        {
            var options = new List<FloatMenuOption>();
            foreach (var (def, lacks) in TraitChanges.DebugRules(p, waysOut).OrderBy(x => x.def.defName))
            {
                var rule = def;
                string label = def.defName + (lacks == null ? " (holds)" : " (lacks: " + lacks + ")");
                options.Add(new FloatMenuOption(label, () =>
                {
                    string said = TraitChanges.DebugApply(p, rule);
                    Log.Message("[Rimfolk] " + said);
                    Messages.Message("Traits that change: " + said + ".", p, MessageTypeDefOf.NeutralEvent, false);
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
