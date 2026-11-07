using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: things by what they are for (Debug actions, "Rimfolk", "Things by role: ..."). Every role's things for
    /// each age in this game (the base game's and the mods'), what the colony here would mind, and a few picks of each as
    /// the colony's and its neighbours' would come.
    /// </summary>
    public static class ThingRolesDebug
    {
        private static void Say(string text) => Messages.Message("Things by role: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Things by role: each role's things by age (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugList()
        {
            Log.Message("[Rimfolk] Things by what they are for:\n" + ThingRoles.Explain(Find.CurrentMap));
            Say("each role's things for each age are in the log; [the colony minds it] marks what most here would not have.");
        }

        [DebugAction("Rimfolk", "Things by role: five picks of each, ours and each neighbour's (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPicks()
        {
            var map = Find.CurrentMap;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ours (" + Ages.Of(map) + "):");
            foreach (ThingRole role in System.Enum.GetValues(typeof(ThingRole)))
                sb.AppendLine("  " + role + ": " + string.Join(", ", Enumerable.Range(0, 5).Select(_ => ThingRoles.Ours(role, map)?.label ?? "none")));
            foreach (var f in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.defeated).Take(8))
            {
                sb.AppendLine(f.Name + " (" + Ages.Of(f) + (Ages.IsEmpire(f) ? ", the Empire" : "") + "):");
                foreach (var role in new[] { ThingRole.Gift, ThingRole.Provisions, ThingRole.Medicine, ThingRole.Drink, ThingRole.Carving, ThingRole.Produce })
                    sb.AppendLine("  " + role + ": " + string.Join(", ", Enumerable.Range(0, 5).Select(_ => ThingRoles.From(role, f, map)?.label ?? "none")));
            }
            Log.Message("[Rimfolk] Things by role, picks:\n" + sb);
            Say("five picks of each role, the colony's and each neighbour's, are in the log.");
        }
    }
}
