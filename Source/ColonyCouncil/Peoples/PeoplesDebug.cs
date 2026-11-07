using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: Rimfolk's peoples (Debug actions, "Rimfolk", "Peoples: ..."). Which of them this world has and how Rimfolk
    /// reads each; a people of a template made now with a town a few days from the home (for trying them in a save made
    /// before they were there: release B brings them into a running game in earnest); one of the camps a people favours or
    /// sends alone (the herders' winter herd) here now; a town's plan laid at the mouse, to see it without attacking a town;
    /// new pawns of each of a people's kinds, to see their gear, kinds of people and gifts (in the log, or here as visitors).
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

        /// <summary>Every kind of a people's pawns (their defs' kinds: its fighters, its civilians, its traders and its leader).</summary>
        private static List<PawnKindDef> KindsOf(FactionDef def) =>
            DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.defaultFactionDef == def && k.race != null).OrderBy(k => k.defName).ToList();

        /// <summary>What a new pawn of one of these kinds came with: kind of people, clothes and their colours, weapon, what they carry, a class or a psylink.</summary>
        private static string Describe(Pawn p)
        {
            var sb = new StringBuilder("    " + p.kindDef.label + ": " + (p.genes?.Xenotype?.label ?? "baseliner"));
            if (p.apparel != null)
                sb.Append("; wears " + string.Join(", ", p.apparel.WornApparel.Select(a => a.LabelNoCount + (a.DrawColor != Color.white ? " #" + ColorUtility.ToHtmlStringRGB(a.DrawColor) : ""))));
            if (p.equipment?.Primary != null) sb.Append("; carries " + p.equipment.Primary.LabelNoCount);
            if (p.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0)
                sb.Append("; in the pack " + string.Join(", ", p.inventory.innerContainer.Select(t => t.LabelCap)));
            var classes = p.story?.traits?.allTraits.Where(t => PeoplesGearMath.MagicSetting(t.def.defName) != null).Select(t => t.LabelCap).ToList();
            if (!classes.NullOrEmpty()) sb.Append("; class " + string.Join(", ", classes));
            var link = p.GetMainPsylinkSource();
            if (link != null) sb.Append("; psylink " + link.level + (p.abilities != null ? " (" + string.Join(", ", p.abilities.AllAbilitiesForReading.Select(a => a.def.label)) + ")" : ""));
            return sb.ToString();
        }

        private static Pawn NewOf(PawnKindDef kind, Faction f, Map map) =>
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, map?.Tile, forceGenerateNewPawn: true));

        [DebugAction("Rimfolk", "Peoples: their gear, kinds of people and gifts (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGear()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var def in Peoples.Templates)
            {
                var d = def;
                options.Add(new FloatMenuOption(d.label, () =>
                {
                    var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.def == d);
                    var sb = new StringBuilder("[Rimfolk] Peoples: four new pawns of each kind of the " + d.label
                                               + (f == null ? " (none in this world: made without a people, so no kinds of people or faith of theirs)" : " (" + f.Name + ")") + ", not kept:");
                    sb.AppendLine();
                    foreach (var kind in KindsOf(d))
                        for (int i = 0; i < 4; i++)
                        {
                            var p = NewOf(kind, f, map);
                            if (p == null) continue;
                            sb.AppendLine(Describe(p));
                            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                        }
                    Log.Message(sb.ToString().TrimEnd());
                    Say("listed in the log.");
                }));
            }
            if (options.Count == 0) Say("no templates loaded.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples: one of each of their kinds here now, as visitors (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGroup()
        {
            var map = Find.CurrentMap;
            var at = UI.MouseCell();
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(x => Peoples.IsOurs(x) && !x.defeated && !x.HostileTo(Faction.OfPlayer)))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name, () =>
                {
                    var pawns = new List<Pawn>();
                    var sb = new StringBuilder("[Rimfolk] Peoples: one of each kind of " + people.Name + " at " + at + ":");
                    sb.AppendLine();
                    foreach (var kind in KindsOf(people.def))
                    {
                        var p = NewOf(kind, people, map);
                        if (p == null) continue;
                        if (!CellFinder.TryFindRandomCellNear(at, map, 6, c => c.Standable(map) && !c.Fogged(map), out var cell)) cell = at;
                        GenSpawn.Spawn(p, cell, map);
                        pawns.Add(p);
                        sb.AppendLine(Describe(p));
                    }
                    if (pawns.Count == 0)
                    {
                        Say("no one could be made.");
                        return;
                    }
                    LordMaker.MakeNewLord(people, new LordJob_VisitColony(people, at, GenDate.TicksPerDay / 2), map, pawns);
                    Log.Message(sb.ToString().TrimEnd());
                    Say(pawns.Count + " of " + people.Name + " here as visitors for half a day (each described in the log).");
                }));
            }
            if (options.Count == 0) Say("none of Rimfolk's peoples at peace with the colony is in this world.");
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
                    var marks = new Dictionary<string, List<int>>();
                    var grid = PeoplesMath.Grid(plan.rows, keys, x, z, map.Size.x, map.Size.z, marks);
                    var rect = grid == null ? null : TownLayouts.Place(map, new LedgerLayout { grid = grid }, Faction.OfPlayer, true);
                    // A landing's docks: a jetty where they reach into water, planks on dry ground.
                    if (rect != null && marks.TryGetValue("dock", out var dock)) PeopleTowns.LayDocks(map, dock.Select(i => TownLayouts.CellOf(map, grid, i)).ToList());
                    Say(rect == null ? "the plan couldn't be laid." : plan.label + " laid at " + rect.Value + " (the colony's own, to look at; deconstruct it after).");
                }));
            }
            if (options.Count == 0) Say("no plans loaded.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
