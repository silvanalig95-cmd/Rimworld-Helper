using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A scenario's start for a new colony (Colonies/ScenarioStartMath.cs; docs/design/stage3-many-colonies.md, section 1,
/// "A scenario's start"; release 3, the player's decision 3A): which of a scenario's parts come for the new colony
/// alone and which are listed as not carried, Bad Blood's families never, a mod's part not carried, the parts that shape
/// people only with the deeper hooks; how the newcomers arrive and when the gravship is offered; the people's count and
/// candidates and what the scenario requires of them; the passage with the kit; Rimfolk's own parts at the new colony
/// (the Beholden without its warm start); the keys; every part of Rimfolk's seventeen scenarios; and the code's seams
/// with the game (never PostIdeoChosen, the pawn hooks routed only while the window is open, the start data cleared).
/// </summary>
public static class ScenarioStartTests
{
    private const string G = "RimWorld.", R = "ColonyCouncil.";

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "scenario start: " + what);
        }

        // ---------------- Which parts come ----------------

        foreach (var t in new[] { "ScenPart_ConfigPage_ConfigureStartingPawns", "ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs", "ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes",
                     "ScenPart_ConfigPage_ConfigureStartingPawns_Mutants" })
            C(ScenarioStartMath.Carry(G + t) == ScenCarry.People, "the people come: " + t);
        foreach (var t in new[] { "ScenPart_ForcedTrait", "ScenPart_ForcedHediff", "ScenPart_Naked", "ScenPart_SetNeedLevel", "ScenPart_PawnFilter_Age", "ScenPart_NoPossessions" })
            C(ScenarioStartMath.Carry(G + t) == ScenCarry.Shaping, "what shapes starting people comes: " + t);
        foreach (var t in new[] { "ScenPart_StartingThing_Defined", "ScenPart_StartingMech", "ScenPart_ScatterThingsNearPlayerStart", "ScenPart_ScatterThingsAnywhere" })
            C(ScenarioStartMath.Carry(G + t) == ScenCarry.Kit, "the kit comes: " + t);
        C(ScenarioStartMath.Carry(G + "ScenPart_StartingAnimal") == ScenCarry.Animals, "the starting animals come");
        C(ScenarioStartMath.Carry(G + "ScenPart_PlayerPawnsArriveMethod") == ScenCarry.Arrival, "the arrival comes");
        var rimfolk = new[] { "Seats", "Days", "Goal", "Ways", "Foe", "Bond", "Secret", "Event", "Wards", "Situation", "Debt", "Deserters", "Beholden" };
        foreach (var t in rimfolk) C(ScenarioStartMath.Carry(R + "ScenPart_Council" + t) == ScenCarry.Rimfolk, "Rimfolk's own part comes, on that colony's council, calendar and people: " + t);
        C(ScenarioStartMath.Carry(R + "ScenPart_CouncilFamilies") == ScenCarry.Never && !ScenarioStartMath.IsCarried(ScenCarry.Never),
            "Bad Blood's two quarrelling families never come (the colony is the family)");
        var whole = new Dictionary<string, ScenWhole>
        {
            ["ScenPart_PlayerFaction"] = ScenWhole.Faction, ["ScenPart_StartingResearch"] = ScenWhole.Research, ["ScenPart_Rule_DisallowDesignator"] = ScenWhole.Rules,
            ["ScenPart_DisallowBuilding"] = ScenWhole.Rules, ["ScenPart_StatFactor"] = ScenWhole.Rules, ["ScenPart_OnPawnDeathExplode"] = ScenWhole.Rules,
            ["ScenPart_DisableMapGen"] = ScenWhole.Rules, ["ScenPart_ForcedMap"] = ScenWhole.Map, ["ScenPart_GameCondition"] = ScenWhole.Conditions,
            ["ScenPart_PermaGameCondition"] = ScenWhole.Conditions, ["ScenPart_DisableIncident"] = ScenWhole.IncidentsOff, ["ScenPart_DisableQuest"] = ScenWhole.IncidentsOff,
            ["ScenPart_CreateIncident"] = ScenWhole.Events, ["ScenPart_CreateQuest"] = ScenWhole.Events, ["ScenPart_MonolithGeneration"] = ScenWhole.Events,
            ["ScenPart_AutoActivateMonolith"] = ScenWhole.Events, ["ScenPart_PursuingMechanoids"] = ScenWhole.Events, ["ScenPart_GameStartDialog"] = ScenWhole.Opening
        };
        foreach (var kv in whole)
            C(ScenarioStartMath.Carry(G + kv.Key) == ScenCarry.Whole && ScenarioStartMath.WholeOf(G + kv.Key) == kv.Value, $"what changes the whole game is listed, not carried: {kv.Key} ({kv.Value})");
        C(ScenarioStartMath.Carry(G + "ScenPart_PlanetLayer") == ScenCarry.Hidden, "the world's layers: nothing to say");
        C(ScenarioStartMath.Carry("SomeMod.ScenPart_StartWithCastle") == ScenCarry.Unknown && ScenarioStartMath.WholeOf("SomeMod.ScenPart_StartWithCastle") == ScenWhole.None
          && !ScenarioStartMath.IsCarried(ScenCarry.Unknown), "a mod's part the code doesn't know is listed as not carried");
        C(ScenarioStartMath.Carry("SomeMod.ScenPart_StartingThing_Defined") == ScenCarry.Unknown, "known by the game's full name only, not a look-alike of another mod's");
        C(ScenarioStartMath.Carry(null) == ScenCarry.Unknown, "no class, nothing carried");
        C(ScenarioStartMath.Carry(G + "ScenPart_ForcedTrait", false) == ScenCarry.Whole && ScenarioStartMath.WholeOf(G + "ScenPart_ForcedTrait", false) == ScenWhole.Hooks,
            "with the deeper hooks off, a forced trait can't reach the newcomers: listed as not carried");
        C(ScenarioStartMath.Carry(G + "ScenPart_NoPossessions", false) == ScenCarry.Shaping, "no possessions is read by the window itself, hooks or not");
        C(ScenarioStartMath.Carry(G + "ScenPart_StartingThing_Defined", false) == ScenCarry.Kit, "the kit needs no hook");
        C(ScenarioStartMath.FactionListed(false) && !ScenarioStartMath.FactionListed(true), "the scenario's own people listed only when it isn't the colony's kind already");
        C(ScenarioStartMath.CarriedOrder(new[] { ScenCarry.Rimfolk, ScenCarry.Kit, ScenCarry.People, ScenCarry.Kit, ScenCarry.Whole })
              .SequenceEqual(new[] { ScenCarry.People, ScenCarry.Kit, ScenCarry.Rimfolk }), "what is carried listed once each, in the window's order");
        C(ScenarioStartMath.WholeOrder(new[] { ScenWhole.Opening, ScenWhole.Faction, ScenWhole.None, ScenWhole.Opening }).SequenceEqual(new[] { ScenWhole.Faction, ScenWhole.Opening }),
            "what changes the whole game listed once each, in order");

        // ---------------- How they arrive ----------------

        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodPods, false, false, false) == StartArrival.Pods, "drop pods as the scenario says, whatever else");
        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodStanding, true, true, true) == StartArrival.Standing, "standing: on foot from the edge of the land");
        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodGravship, true, true, true) == StartArrival.Gravship, "the gravship with Odyssey, a map of their own and the old colony left behind");
        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodGravship, true, true, false) == StartArrival.Standing, "the old colony kept: no gravship, on foot instead");
        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodGravship, true, false, true) == StartArrival.Standing, "the colony's party there first: no gravship");
        C(ScenarioStartMath.Arrival(ScenarioStartMath.MethodGravship, false, true, true) == StartArrival.Standing, "no Odyssey: no gravship, as the game's own start falls back");
        C(ScenarioStartMath.GravshipRefused(ScenarioStartMath.MethodGravship, true, true, false) && !ScenarioStartMath.GravshipRefused(ScenarioStartMath.MethodGravship, true, true, true)
          && !ScenarioStartMath.GravshipRefused(ScenarioStartMath.MethodPods, true, true, false), "a gravship refused is said, one given or never asked for isn't");
        C(ScenarioStartMath.GravshipOffered(true, true, true) && !ScenarioStartMath.GravshipOffered(true, true, false), "the gravship offered only with the old colony left behind");

        // ---------------- The people ----------------

        C(ScenarioStartMath.Count(4) == 4 && ScenarioStartMath.Count(0) == 1 && ScenarioStartMath.Count(15) == ScenarioStartMath.MaxPeople, "the scenario's count, 1 to 10");
        C(ScenarioStartMath.Candidates(4, 8) == 8 && ScenarioStartMath.Candidates(5, 3) == 5 && ScenarioStartMath.Candidates(1, 20) == 10, "chosen from the scenario's choice, never fewer than come, at most 10");
        C(ScenarioStartMath.RequiredMet(StartRequired.Kind, 2, 3) && !ScenarioStartMath.RequiredMet(StartRequired.Kind, 2, 1), "a required kind: at least that many");
        C(ScenarioStartMath.RequiredMet(StartRequired.Xenotype, 2, 2) && !ScenarioStartMath.RequiredMet(StartRequired.Xenotype, 2, 3) && !ScenarioStartMath.RequiredMet(StartRequired.Mutant, 1, 0),
            "a required xenotype or mutant: exactly that many, as the game's page checks");
        const uint Child = 4u, Adult = 8u, Baby = 2u;
        C(ScenarioStartMath.StageAllowed(Adult, Adult | Child) && !ScenarioStartMath.StageAllowed(Baby, Adult | Child), "a life stage the scenario allows");
        C(ScenarioStartMath.AllBabies(3, 3) && !ScenarioStartMath.AllBabies(2, 3) && !ScenarioStartMath.AllBabies(0, 0), "babies alone can't found it");

        // ---------------- The passage ----------------

        C(ScenarioStartMath.Passage(new[] { 0f, 400f }, 2000f, 0, true) == 0, "free with the old colony left behind, kit and all");
        C(ScenarioStartMath.Passage(new[] { 0f, 400f }, 0f, 0, false) == FoundingMath.Passage(new[] { 0f, 400f }, false), "no kit: release 1's passage exactly");
        C(ScenarioStartMath.Passage(new[] { 0f }, 1234f, 0, false) == FoundingMath.MinPassage + 1230, "the kit's worth paid on top, rounded to ten");
        C(ScenarioStartMath.Passage(new float[0], 0f, 3, false) == 3 * FoundingMath.MinPassage, "the children in the colony's care pay a newcomer's least passage each");
        C(ScenarioStartMath.KitWorth(-5f) == 0 && ScenarioStartMath.KitWorth(4f) == 0 && ScenarioStartMath.KitWorth(6f) == 10, "the kit's worth never below nothing");

        // ---------------- Rimfolk's own parts ----------------

        C(ScenarioStartMath.WarmStart(true) && !ScenarioStartMath.WarmStart(false), "the Beholden's warm start only at a new game: goodwill is the whole colony's");
        C(!ScenarioStartMath.SetupDue(2, 3, 1000) && ScenarioStartMath.SetupDue(3, 3, 0), "set up once everyone who came is on the map");
        C(ScenarioStartMath.SetupDue(1, 3, ScenarioStartMath.DayTicks), "or a day after they came, with whoever is there");
        C(!ScenarioStartMath.SetupDue(0, 0, ScenarioStartMath.DayTicks * 3), "nobody came: nothing to set up");

        // ---------------- The keys ----------------

        foreach (var (from, name) in new[] { (ScenarioStartMath.FromDef, "Crashlanded"), (ScenarioStartMath.Local, "My start"), (ScenarioStartMath.Workshop, "Castle: a start") })
        {
            string key = ScenarioStartMath.Key(from, name);
            C(ScenarioStartMath.Parse(key, out int f, out string back) && f == from && back == name, $"a key read back as it was made: {key}");
        }
        C(ScenarioStartMath.Parse(ScenarioStartMath.Key(ScenarioStartMath.ThisGame, null), out int g, out _) && g == ScenarioStartMath.ThisGame, "this game's own scenario has a key of its own");
        C(ScenarioStartMath.Key(ScenarioStartMath.FromDef, "") == null && !ScenarioStartMath.Parse(null, out _, out _) && !ScenarioStartMath.Parse("def:", out _, out _) && !ScenarioStartMath.Parse("x", out _, out _),
            "no name, no key");

        Scenarios(C);
        Seams(C);
        return n;
    }

    /// <summary>Every part of Rimfolk's scenarios is one the code knows, and what each scenario carries.</summary>
    private static void Scenarios(Action<bool, string> C)
    {
        string root = Root();
        C(root != null, "the repository found");
        if (root == null) return;
        var scenarios = Directory.GetFiles(Path.Combine(root, "Defs", "Scenarios"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ScenarioDef")).ToList();
        C(scenarios.Count == 17, $"Rimfolk's seventeen scenarios ({scenarios.Count})");
        foreach (var s in scenarios)
        {
            string name = s.Element("defName")?.Value ?? "?";
            var types = s.Descendants("parts").Elements("li").Select(li => (string)li.Attribute("Class") ?? "").Select(c => c.Contains('.') ? c : G + c).ToList();
            C(types.All(t => ScenarioStartMath.Carry(t) != ScenCarry.Unknown), $"{name}: every part is one the code knows");
            C(types.Count(t => ScenarioStartMath.Carry(t) == ScenCarry.People) == 1 && types.Count(t => ScenarioStartMath.Carry(t) == ScenCarry.Arrival) == 1,
                $"{name}: its people and its arrival come");
            C(types.Any(t => ScenarioStartMath.Carry(t) == ScenCarry.Kit), $"{name}: its kit comes");
            var rim = types.Where(t => t.StartsWith(R)).ToList();
            C(rim.All(t => ScenarioStartMath.Carry(t) == ScenCarry.Rimfolk || t == R + "ScenPart_CouncilFamilies"), $"{name}: Rimfolk's own parts come ({rim.Count})");
            if (name == "Council_Scenario_BadBlood")
                C(rim.Contains(R + "ScenPart_CouncilFamilies") && ScenarioStartMath.Carry(R + "ScenPart_CouncilFamilies") == ScenCarry.Never, "Bad Blood: its two families are listed as never carried");
        }
    }

    private static void Seams(Action<bool, string> C)
    {
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
