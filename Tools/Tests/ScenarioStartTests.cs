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
        string root = Root();
        if (root == null) return;
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        // ---------------- Rimfolk's own parts, every one known ----------------

        string scen = Code("Scenarios/Scenarios.cs") + Code("Scenarios/ScenarioParts.cs");
        var parts = Regex.Matches(scen, @"class (ScenPart_Council\w+) : ScenPart_Council\b").Select(m => m.Groups[1].Value).ToList();
        C(parts.Count == 14, $"Rimfolk's fourteen parts found ({parts.Count})");
        foreach (var p in parts) C(ScenarioStartMath.Carry(R + p) != ScenCarry.Unknown, "Rimfolk's part is one the start knows: " + p);
        C(scen.Contains("(ctx.scenario ?? Find.Scenario)?.name"), "a reputation's reason names the scenario it came from");
        C(scen.Contains("if (ctx.warmStart && f.PlayerGoodwill < goodwill)"), "the Beholden's warm start only when the context has one");
        C(scen.Contains("scenario = Find.Scenario };"), "the game's own start sets its scenario, with its warm start");

        // ---------------- The people, never through PostIdeoChosen ----------------

        string starts = Code("Colonies/ScenarioStarts.cs");
        string window = Code("Colonies/NewcomersWindow.cs");
        C(!(starts + window).Contains(".PostIdeoChosen("), "the people part's PostIdeoChosen is never called (it would clear the custom xenotypes and the drug policy)");
        C(starts.Contains("d.startingPawnsRequired = k.kindCounts.Where(") && starts.Contains("d.startingXenotypesRequired = x.xenotypeCounts.Where(")
          && starts.Contains("d.startingMutantsRequired = m.mutantCounts.Where(") && starts.Contains("d.allowedDevelopmentalStages = c.allowedDevelopmentalStages")
          && starts.Contains("d.startingSkillsRequired = c.requiredSkills"), "the start data set from the people part's own fields");
        C(starts.Contains("StartingPawnUtility.SetGenerationRequest(i, r)") && starts.Contains("StartingPawnUtility.WorkTypeRequirementsSatisfied()") && starts.Contains("tries <= 20"),
            "each place's request shaped and the people tried again as the game's own part does");
        C(starts.Contains("\"SelectedCharactersMustInclude\"") && starts.Contains("\"SelectedCharacterMustBeCapableOf\"") && starts.Contains("\"SelectedCharactersMustBeOfAllowedDevelopmentalStages\""),
            "Confirm held back in the game's own words for its own page's checks");
        C(starts.Contains("PawnUtility.TryDestroyStartingColonistFamily(p)") && starts.Contains("PawnDiscardDecideMode.Discard"),
            "those left behind let go, their families only when none of the chosen is kin");
        C(Regex.IsMatch(starts, @"static\s+List<Entry>\s+Loaded\(\)") && !starts.Contains("ScenarioFiles.RecacheData") && !starts.Contains("ScenarioLister."),
            "the loaded scenarios listed without reading the disk mid-game");

        // ---------------- The pawn hooks, only while the window is open ----------------

        string patches = Code("Colonies/ScenarioStartPatches.cs");
        foreach (var hook in new[] { "Notify_NewPawnGenerating", "Notify_PawnGenerated", "AllowPlayerStartingPawn" })
            C(patches.Contains($"[HarmonyPatch(typeof(Scenario), nameof(Scenario.{hook}))]"), "the shaping parts reach the newcomers through the game's own hook: " + hook);
        C(Regex.Matches(patches, @"ScenarioStarts\.Shaping == null \|\| (context|req\.Context) != PawnGenerationContext\.PlayerStarter \|\| !RimfolkPatches\.On \|\| !ScenarioStarts\.ShapingLive").Count == 3,
            "only a starting colonist made while the window is open, with the deeper hooks on; anyone else goes to the game's own scenario");
        C(starts.Contains("Current.ProgramState == ProgramState.Playing && Current.Game?.InitData != null") && starts.Contains("Shaping = null;"),
            "the scenario let go as soon as the window's start data is gone (a new game never routed)");
        int close = window.IndexOf("public override void PostClose()", StringComparison.Ordinal);
        C(close > 0 && window.IndexOf("ScenarioStarts.Shaping = null;", close, StringComparison.Ordinal) > 0 && window.IndexOf("Current.Game.InitData = null;", close, StringComparison.Ordinal) > 0,
            "however the window closes, the scenario shapes nobody else and the start data is cleared");
        C(window.Contains("if (start.hooks) ScenarioStarts.Shaping = scen;"), "the hooks routed only when all three are applied");

        // ---------------- The kit, made as they are chosen ----------------

        int confirm = window.IndexOf("private void Confirm(int price)", StringComparison.Ordinal);
        int letGo = window.IndexOf("ScenarioStarts.LetGoLeftBehind(chosen)", confirm, StringComparison.Ordinal);
        int kit = window.IndexOf("ScenarioStarts.MakeKit(", confirm, StringComparison.Ordinal);
        int clear = window.IndexOf("Pawns.Clear();", confirm, StringComparison.Ordinal);
        C(confirm > 0 && letGo > confirm && kit > letGo && clear > kit, "the kit made while the start data holds the chosen alone (animals bond to them as at a new game)");
        C(starts.Contains(".PlayerStartingThings()") && starts.Contains("PawnUtility.GiveAllStartingPlayerPawnsThought(ThoughtDefOf.CrashedTogether)"),
            "the kit is the scenario's own PlayerStartingThings; pods bring the game's own crashed together");
        C(starts.Contains("ScenarioStartMath.Arrival(st.method, ModsConfig.OdysseyActive, mapForThem, oldLeftBehind)") && starts.Contains("founding && Gravship(map, came, kit, out at)"),
            "the gravship only by the rule, and only on a map made for them");
        C(starts.Contains("GenStep_ReserveGravshipArea.SetStartSpot(") && starts.Contains("GravshipPlacementUtility.ClearAreaForGravship(") && starts.Contains("SketchResolverDefOf.Gravship"),
            "the gravship made and set down as the game's own start does");

        // ---------------- Rimfolk's own parts at the new colony ----------------

        C(starts.Contains("ScenarioStartMath.Carry(p.GetType().FullName) == ScenCarry.Rimfolk") && starts.Contains("warmStart = ScenarioStartMath.WarmStart(false)"),
            "Rimfolk's own parts set up for that colony, Bad Blood's families never, the Beholden without its warm start");
        C(starts.Contains("ScenarioStartMath.SetupDue(here.Count, came.Count, Now - plan.scenarioArrived)"), "set up once they are there, or a day after they came");
        string foundings = Code("Colonies/Foundings.cs");
        C(foundings.Contains("if (plan.scenarioPeople.Count > 0) ScenarioStarts.SetUpWhenHere(plan, map);") && foundings.Contains("newcomersDone && plan.scenarioPeople.Count == 0"),
            "in the founding's own hour, and the founding kept until it is done");
        C(!starts.Contains("Sched.") && !Regex.IsMatch(starts, @"\bvoid Tick\("), "no schedule slot and nothing per tick");
        string home = Code("Colonies/HomesFounding.cs");
        C(home.Contains("if (Scribe.mode != LoadSaveMode.Saving || scenarioPeople.Count > 0)"), "the scenario's people saved only while they wait");
        string saves = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(saves.Contains("Colonies/HomesFounding.cs:scenarioPeople"), "the scenario's people have their model in the save estimate");

        // ---------------- The words ----------------

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_ScenarioStart.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes and no exclamation marks");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\{DAYS\}|\bin \d+ (days?|hours?)\b|\bdays? (left|more)\b")), "no countdowns");
        foreach (var w in Enum.GetValues(typeof(ScenWhole)).Cast<ScenWhole>().Where(w => w != ScenWhole.None))
            C(keyed.ContainsKey("CC_ScenStart_Whole_" + w) && keyed.ContainsKey("CC_ScenStart_BriefWhole_" + w), "what changes the whole game said: " + w);
        foreach (var a in Enum.GetValues(typeof(StartArrival)).Cast<StartArrival>())
            C(keyed.ContainsKey("CC_ScenStart_Arrive_" + a) && keyed.ContainsKey("CC_ScenStart_Came_" + a), "the arrival said, before and as it happened: " + a);
        foreach (var c in new[] { ScenCarry.People, ScenCarry.Shaping, ScenCarry.Kit, ScenCarry.Animals, ScenCarry.Arrival, ScenCarry.Rimfolk })
            C(keyed.ContainsKey("CC_ScenStart_Brief_" + c), "what is carried said in the founding window: " + c);
        foreach (int f in new[] { ScenarioStartMath.FromDef, ScenarioStartMath.ThisGame, ScenarioStartMath.Local, ScenarioStartMath.Workshop })
            C(keyed.ContainsKey("CC_ScenStart_From_" + f), "the list's group named: " + f);
        C(keyed["CC_ScenStart_ListLine"].Contains("new game") && keyed["CC_ScenStart_NewGame"].Contains("new game"), "a whole new scenario on its own page: a new game, said plainly");
        C(keyed["CC_ScenStart_Never_Families"].Contains("family lines"), "the families' feud said for what it is");
        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_ScenarioStart.xml")).Root!.Elements().First();
        C(almanac.Element("label")?.Value == "a scenario's start" && !almanac.ToString().Contains("—") && !almanac.ToString().Contains("!"), "the Almanac page, a page of its own");
        C(almanac.ToString().Contains("new game") && almanac.ToString().Contains("family lines"), "the Almanac page says where a whole new scenario belongs, and why the families never come");
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
