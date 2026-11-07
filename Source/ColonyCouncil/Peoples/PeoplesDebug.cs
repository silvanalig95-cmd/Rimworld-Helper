using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: Rimfolk's peoples (Debug actions, "Rimfolk", "Peoples: ..."). Which of them this world has and how Rimfolk
    /// reads each; a people of a template made now with a town a few days from the home (for trying them in a save made
    /// before they were there: release B brings them into a running game in earnest); one of the camps a people favours or
    /// sends alone (the herders' winter herd) here now; a town's plan laid at the mouse, to see it without attacking a town.
    /// </summary>
    public static class PeoplesDebug
    {
        private static void Say(string text) => Messages.Message("Peoples: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples: Rimfolk's peoples in this world", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugList()
        {
            var sb = new StringBuilder("[Rimfolk] Peoples: templates ");
            sb.Append(string.Join(", ", Peoples.Templates.Select(d => d.defName + " (" + Peoples.Of(d).calling + ")")));
            sb.AppendLine(".");
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(Peoples.IsOurs))
            {
                var ext = Peoples.Of(f);
                int towns = Find.WorldObjects.Settlements.Count(s => s.Faction == f);
                sb.AppendLine($"  {f.Name} ({f.def.label}, {ext.calling}): {towns} town(s), goodwill {f.PlayerGoodwill}{(f.HostileTo(Faction.OfPlayer) ? ", at war" : "")}, "
                              + $"leader {f.leader?.LabelShort ?? "none"} ({f.LeaderTitle}), head {ext.head}, messengers {ext.couriers}, caravans x{Peoples.CaravanFactor(f):0.##}, "
                              + $"faith {(ModsConfig.IdeologyActive ? f.ideos?.PrimaryIdeo?.name ?? "none" : "no Ideology")}.");
            }
            Log.Message(sb.ToString().TrimEnd());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples: a people of a template now, near this home (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMake()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var def in Peoples.Templates)
            {
                var d = def;
                options.Add(new FloatMenuOption(d.label + " (" + Peoples.Of(d).calling + ")", () =>
                {
                    var f = Peoples.Make(d, map);
                    if (f == null)
                    {
                        Say("couldn't make one.");
                        return;
                    }
                    var town = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == f);
                    Say(f.Name + " made" + (town != null ? ", their " + (Peoples.TownWord(f) ?? "town") + " " + town.Name + " on the world map" : "") + ".");
                }));
            }
            if (options.Count == 0) Say("no templates loaded.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(x => Peoples.IsOurs(x) && Arrivals.Usable(x)))
            {
                var people = f;
                var kinds = (Peoples.Of(people).camps ?? new List<CampKindDef>()).Where(k => k != null).Distinct().ToList();
                foreach (var kind in kinds)
                {
                    var k = kind;
                    options.Add(new FloatMenuOption(people.Name + ": " + k.label, () =>
                    {
                        var tracker = CampTracker.Instance;
                        if (tracker == null || !CampTracker.Enabled)
                        {
                            Say("camps are off.");
                            return;
                        }
                        var why = new List<string>();
                        var c = tracker.StartKind(map, k, people, why);
                        Say(c != null ? k.label + " of " + people.Name + " on the way." : "no camp: " + string.Join("; ", why));
                    }));
                }
            }
            if (options.Count == 0) Say("none of Rimfolk's peoples is at peace with the colony.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples: lay a town's plan at the mouse (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPlan()
        {
            var map = Find.CurrentMap;
            var at = UI.MouseCell();
            var options = new List<FloatMenuOption>();
            foreach (var def in DefDatabase<TownPlanDef>.AllDefsListForReading)
            {
                var plan = def;
                options.Add(new FloatMenuOption(plan.label ?? plan.defName, () =>
                {
                    var keys = plan.Words(plan.wallStuff?.FirstOrDefault(d => d != null));
                    PeoplesMath.Corner(at.x, at.z, plan.Width, plan.Height, map.Size.x, map.Size.z, out int x, out int z);
                    var grid = PeoplesMath.Grid(plan.rows, keys, x, z, map.Size.x, map.Size.z, null);
                    var rect = grid == null ? null : TownLayouts.Place(map, new LedgerLayout { grid = grid }, Faction.OfPlayer, true);
                    Say(rect == null ? "the plan couldn't be laid." : plan.label + " laid at " + rect.Value + " (the colony's own, to look at; deconstruct it after).");
                }));
            }
            if (options.Count == 0) Say("no plans loaded.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
