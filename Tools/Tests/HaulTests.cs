using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Carrying and hauling (Hauling/HaulMath.cs; DESIGN.md "Carrying and hauling"): how much fits, when a load is worth a
/// trip and when it has been carried long enough, the long material run (far, the build area, how much more, the other
/// builders' cover, its end), what is on the way, what goes to storage and what the pawn keeps, the hand-over from the
/// standalone Harvest and Haul (its settings file and the loads in a save); and the mod's side: the settings as the
/// standalone mod had them, the hooks (plain prefixes and postfixes, the standalone's own ones standing down while it is
/// loaded), the job, the clock, the words, the Almanac and the README.
/// </summary>
public static class HaulTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "carrying and hauling: " + what);
        }

        // ================= How much fits =================

        C(Near(HaulMath.FreeMass(35f, 1f, 10f), 25f) && Near(HaulMath.FreeMass(35f, 0.5f, 10f), 7.5f) && Near(HaulMath.FreeMass(35f, 2f, 0f), 35f),
            "free mass is the share of the capacity that may be filled, less what is carried (never past all of it)");
        C(HaulMath.CountThatFits(75, 0.5f, 10f) == 20 && HaulMath.CountThatFits(10, 0.5f, 100f) == 10, "as many as the free mass holds, no more than the stack");
        C(HaulMath.CountThatFits(30, 0f, 0f) == 30 && HaulMath.CountThatFits(0, 1f, 10f) == 0 && HaulMath.CountThatFits(5, 1f, -3f) == 0,
            "a weightless thing always fits; an empty stack or a full pawn takes none");
        C(HaulMath.CountThatFits(10, 0.1f, 0.3f) == 3, "no rounding down a whole unit away (0.3 kg holds three of 0.1)");
        C(HaulMath.WorthATrip(5f, 5f) && !HaulMath.WorthATrip(4.9f, 5f) && HaulMath.WorthATrip(0f, 0f), "a trip is worth it from the minimum mass on");
        C(!HaulMath.HoldExpired(-1, 999999, 8f) && !HaulMath.HoldExpired(0, 8 * 2500, 8f) && HaulMath.HoldExpired(0, 8 * 2500 + 1, 8f),
            "a load carried along goes to storage once it has been carried longer than the hours allowed");

        // ================= The long material run =================

        C(HaulMath.Far(30f, 30f) && !HaulMath.Far(29.9f, 30f) && !HaulMath.Far(100f, 0f), "far from the setting's distance on (straight); a distance of nothing is never far");
        C(Near(HaulMath.RunRadius(30f), 15f) && Near(HaulMath.RunRadius(15f), 8f) && Near(HaulMath.RunRadius(80f), 15f) && Near(HaulMath.RunRadius(22f), 11f),
            "the build area is half the run's distance, never less than the game's 8 cells or more than 15");
        for (float d = HaulMath.RunDistanceMin; d <= HaulMath.RunDistanceMax; d += 5f)
            C(HaulMath.RunRadius(d) < d, $"at {d} cells the build area never reaches back to the stockpile");
        C(HaulMath.Extra(200, 75, 0) == 125 && HaulMath.Extra(200, 75, 50) == 75 && HaulMath.Extra(60, 75, 0) == 0 && HaulMath.Extra(100, -5, -5) == 100,
            "more to take: what the area needs past the hands and the stock already carried");
        C(HaulMath.Covered(80, 80) && HaulMath.Covered(100, 0) && !HaulMath.Covered(79, 80) && !HaulMath.Covered(0, 0),
            "a runner covers a material for the others only while its stock holds all the area still needs");
        C(HaulMath.RunOver(0, 1000, 1000) && !HaulMath.RunOver(5, 1000, 1000 + HaulMath.RunIdleTicks) && HaulMath.RunOver(5, 1000, 1001 + HaulMath.RunIdleTicks)
          && HaulMath.RunOver(5, -1, 0), "a run is over when its area needs none, or after two hours unused");
        C(HaulMath.RunIdleTicks == 2 * 2500 && HaulMath.CoverTicks == 2500 && HaulMath.NearbyNeeders == 8f, "two hours unused end a run; an hour's use keeps the cover; the game's 8 cells");
        C(HaulMath.RunDistanceDefault == 30f && HaulMath.RunDistanceMin < HaulMath.RunDistanceDefault && HaulMath.RunDistanceDefault < HaulMath.RunDistanceMax,
            "about 30 cells by default, on the slider");

        // ================= On the way =================

        C(Near(HaulMath.Detour(10f, 20f, 25f), 5f) && Near(HaulMath.Detour(0f, 30f, 30f), 0f), "the detour is the walk there and on, less the straight walk");
        C(HaulMath.OnTheWay(10f, 20f, 25f) && !HaulMath.OnTheWay(10f, 22f, 25f) && !HaulMath.OnTheWay(2f, 8f, 10f),
            "on the way: six cells out of the way at most, and only on a walk of twelve or more");
        C(HaulMath.MaxOnTheWay == 6 && HaulMath.MaxDetour == 6f && HaulMath.MinWalk == 12f && HaulMath.OnTheWayCooldownTicks == 600, "six things, six cells, twelve cells, ten seconds");

        // ================= To storage, and what is kept =================

        C(HaulMath.Deliverable(10, 15, 5) == 10 && HaulMath.Deliverable(10, 12, 5) == 7 && HaulMath.Deliverable(10, 4, 5) == 0 && HaulMath.Deliverable(10, 8, 0) == 8,
            "what goes to storage: what is owed, never more than the pawn has, never what it keeps");
        C(HaulMath.WhollyOurs(20, 20, 0) && HaulMath.WhollyOurs(25, 20, 0) && !HaulMath.WhollyOurs(10, 20, 0) && !HaulMath.WhollyOurs(20, 20, 1) && !HaulMath.WhollyOurs(5, 0, 0),
            "a stack goes to Pick Up And Haul's unloading only when all of it is Rimfolk's and none of it kept");
        C(HaulMath.FoodKept(10, 0.5f, 0f, 1f) == 2 && HaulMath.FoodKept(10, 0.5f, 0.6f, 1f) == 0 && HaulMath.FoodKept(1, 0.5f, 0f, 2f) == 1 && HaulMath.FoodKept(5, 0f, 0f, 1f) == 0,
            "packed food kept as the game keeps it: what fits the food need's top past the other packed food");

        // ================= The standalone mod's settings file =================

        string file = @"<?xml version=""1.0"" encoding=""utf-8""?>
<SettingsBlock>
  <ModSettings Class=""HarvestAndHaul.HarvestAndHaulSettings"">
    <collectChunks>True</collectChunks>
    <collectFishing>False</collectFishing>
    <fillPercent>0.75</fillPercent>
    <minTripMass>12.5</minTripMass>
    <maxHoldHours>nonsense</maxHoldHours>
    <verboseLogging>True</verboseLogging>
    <somethingElse>3</somethingElse>
  </ModSettings>
</SettingsBlock>";
        var read = HaulMath.ReadStandaloneSettings(file);
        C(read.Count == 5 && read["haulChunks"] == "True" && read["haulFishing"] == "False" && read["haulFill"] == "0.75" && read["haulMinTrip"] == "12.5" && read["haulLog"] == "True",
            "the standalone settings file is read by Rimfolk's settings: only its own keys, only values that read as what they are");
        C(HaulMath.ReadStandaloneSettings(file.Replace("HarvestAndHaul.HarvestAndHaulSettings", "Other.Settings")).Count == 0
          && HaulMath.ReadStandaloneSettings("<not xml").Count == 0 && HaulMath.ReadStandaloneSettings(null).Count == 0, "nothing from another mod's file or one that isn't XML");
        C(Near(HaulMath.ImportNumber("haulFill", 0.02f), 0.1f) && Near(HaulMath.ImportNumber("haulMinTrip", 50f), 30f) && Near(HaulMath.ImportNumber("haulMaxHours", 0f), 1f)
          && Near(HaulMath.ImportNumber("haulFill", 0.6f), 0.6f), "a standalone value lands on Rimfolk's slider");
        // Every setting the standalone mod saves (its HarvestAndHaulSettings.ExposeData, 1.2.0) has a Rimfolk setting.
        string[] standalone = { "enabled", "collectPlants", "collectMining", "collectDeepDrill", "collectAnimalProducts", "collectDeconstruction", "haulDeconstructionLeftovers",
            "collectFishing", "collectChunks", "fillPercent", "minTripMass", "deliverBeforeNeeds", "maxHoldHours", "includeMechanoids", "verboseLogging" };
        C(standalone.All(HaulMath.StandaloneKeys.ContainsKey) && HaulMath.StandaloneKeys.Count == standalone.Length, "each of the standalone mod's settings has a Rimfolk setting");
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        foreach (var kv in HaulMath.StandaloneKeys)
            C(Regex.IsMatch(settings, @"public (bool|float) " + kv.Value + @" = "), $"{kv.Key} becomes {kv.Value}, a field of the settings");
        // The defaults are the standalone mod's.
        foreach (var (f, v) in new[] { ("haulOwnWork", "true"), ("haulPlants", "true"), ("haulChunks", "false"), ("haulLeftoversNow", "true"), ("haulFill", "1f"), ("haulMinTrip", "5f"),
                     ("haulMaxHours", "8f"), ("haulBeforeNeeds", "true"), ("haulMechs", "true"), ("haulLog", "false"), ("materialRun", "true"), ("materialRunDistance", "30f"), ("haulOnTheWay", "true") })
            C(Regex.IsMatch(settings, @"public \w+ " + f + " = " + Regex.Escape(v) + ";"), $"{f} is {v} by default, as the standalone mod had it (or the player asked)");

        // ================= The standalone mod's loads in a save =================

        string save = @"<game><components>
  <li Class=""ColonyCouncil.CouncilGameComponent"" />
  <li Class=""HarvestAndHaul.HarvestTracker"">
    <loads>
      <keys><li>Thing_Human311</li><li>null</li><li>Thing_Human77</li></keys>
      <values>
        <li><owed><keys><li>RawPotatoes</li><li>WoodLog</li></keys><values><li>35</li><li>20</li></values></owed><skills><li>Plants</li></skills><startedTick>1200</startedTick><full>True</full></li>
        <li><owed><keys><li>Steel</li></keys><values><li>5</li></values></owed></li>
        <li><owed><keys><li>Jade</li></keys><values><li>0</li></values></owed></li>
      </values>
    </loads>
  </li>
</components></game>";
        var doc = new XmlDocument();
        doc.LoadXml(save);
        XmlNode game = doc.DocumentElement!;
        XmlNode list = game.SelectSingleNode("components")!;
        XmlNode tracker = HaulMath.FindStandaloneTracker(game);
        C(tracker != null && HaulMath.FindStandaloneTracker(list) == tracker && HaulMath.FindStandaloneTracker(list.FirstChild) == tracker,
            "the standalone tracker is found from the game, from its components or from one of them");
        var loads = HaulMath.ReadStandaloneLoads(tracker);
        C(loads.Count == 1 && loads[0].pawnId == "Thing_Human311" && loads[0].owed["RawPotatoes"] == 35 && loads[0].owed["WoodLog"] == 20
          && loads[0].skills.SequenceEqual(new[] { "Plants" }) && loads[0].startedTick == 1200 && loads[0].full,
            "its loads are read: the pawn, what is owed and the work it came from (no pawn, or nothing owed, none)");
        var doc2 = new XmlDocument();
        doc2.LoadXml("<game><components><li Class=\"ColonyCouncil.CouncilGameComponent\" /></components></game>");
        C(HaulMath.FindStandaloneTracker(doc2.DocumentElement) == null && HaulMath.ReadStandaloneLoads(null).Count == 0, "a save without the mod has none");

        // ================= The mod's side =================

        string dir = Path.Combine(root, "Source", "ColonyCouncil", "Hauling");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").OrderBy(f => f).Select(File.ReadAllText));
        string patches = File.ReadAllText(Path.Combine(dir, "HaulPatches.cs"));
        var classes = Regex.Matches(patches, @"\[HarmonyPatch\(typeof\((\w+)\), (?:nameof\(\w+\.(\w+)\)|""(\w+)"")[^\n]*\]\s*\n\s*public static class (\w+)\s*\{(.*?)\n    \}", RegexOptions.Singleline)
            .Cast<Match>().ToList();
        C(classes.Count == 12, $"twelve hooks: the standalone mod's ten and the material run's two ({classes.Count})");
        C(!Regex.IsMatch(code, @"Transpiler|HarmonyTranspiler|CodeInstruction"), "no transpilers: plain prefixes, postfixes and a finalizer");
        string[] standaloneHooks = { "Patch_HaulChoosing", "Patch_HaulNextJob", "Patch_HaulKeepOwed", "Patch_HaulPlantCollected", "Patch_HaulMined", "Patch_HaulDeepDrill",
            "Patch_HaulGathered", "Patch_HaulDeconstructed", "Patch_HaulFloorRemoved", "Patch_HaulFish" };
        foreach (var m in classes)
        {
            string name = m.Groups[4].Value, body = m.Groups[5].Value;
            bool mine = standaloneHooks.Contains(name);
            C(mine == (body.Contains("Prepare() => HaulHook.Mine") && body.Contains("StandsDownWhy() => HaulLinks.StandsDownWhy")),
                $"{name}: {(mine ? "the standalone mod's own hook stands down while it is loaded, saying why" : "works beside the standalone mod")}");
            C(body.Contains("RimfolkPatches.On") || name == "Patch_HaulChoosing", $"{name}: does nothing while the deeper game hooks are off");
        }
        C(standaloneHooks.All(h => classes.Any(m => m.Groups[4].Value == h)), "every hook of the standalone mod has its Rimfolk copy");
        string rimfolkPatches = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Patches", "RimfolkPatches.cs"));
        C(rimfolkPatches.Contains("StoodDown[type.Name] = why") && File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SelfCheck.cs")).Contains("RimfolkPatches.StoodDown"),
            "a hook that stands down on purpose is listed as such in the self-check, not as one that failed");
        C(code.Contains("public static bool Works => RimfolkPatches.On && S != null && S.haulOwnWork && !HaulLinks.Standalone && ChoiceHooked"),
            "collecting only with the hooks on, the setting on, the standalone mod gone and the job choice hooked (no hoarding without it)");
        C(code.Contains("People.ModActive(StandaloneId) || GenTypes.GetTypeInAnyAssembly(StandaloneType) != null") && code.Contains("\"silvanalig95.harvestandhaul\"")
          && code.Contains("\"HarvestAndHaul.HarvestTracker\""), "the standalone mod is found by its package id or its tracker's class");
        C(Regex.IsMatch(code, @"Log\.Message\(""\[Rimfolk\] Harvest and Haul is loaded on its own\.[^;]*can be removed now"), "once at startup the log says the standalone mod can be removed");
        C(code.Contains("\"PickUpAndHaul.CompHauledToInventory\"") && code.Contains("\"RegisterHauledItem\"") && code.Contains("\"UnloadYourHauledInventory\""),
            "Pick Up And Haul's comp and unloading are found by name");
        C(code.Contains("\"CombatExtended.CompInventory\"") && code.Contains("\"CanFitInInventory\""), "Combat Extended's limits, read by reflection, as the standalone mod did");
        C(code.Contains("HasAnyUnpackedCaravanItems") && code.Contains("Kept(pawn, def)") && code.Contains("takeToInventory") && code.Contains("inventoryStock"),
            "a caravan's packs and what the pawn's drug policy, inventory stock and packed food keep are left alone");
        C(code.Contains("pawn.jobs.jobQueue.Count > 0) return false") && code.Contains("jobQueue.EnqueueFirst(job"), "a delivery is queued only when nothing the player ordered waits");
        C(code.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareHauling)") && code.Contains("if (loads.Count == 0) return;"), "its pass runs on a 250-tick slot of its own, only while someone carries");
        C(code.Contains("Scribe_Collections.Look(ref loads, \"loads\"") && Regex.Matches(code, @"Scribe_Collections\.Look").Count == 4, "one book saved: the loads (what is owed, the skills, the run)");

        // The job and the words.
        var job = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Hauling.xml")).Root!.Elements("JobDef").Single();
        C((string?)job.Element("defName") == "Rimfolk_CarryToStorage" && (string?)job.Element("driverClass") == "ColonyCouncil.JobDriver_CarryToStorage", "the delivery job and its driver");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Hauling.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (Match m in Regex.Matches(code, @"""(CC_Haul_\w+)""\.Translate"))
            C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} is in the words");
        C(keyed.ContainsKey("CC_SetSec_Hauling_Standalone") && keyed["CC_SetSec_Hauling_Standalone"].Contains("can be removed"), "the settings say the standalone mod can be removed");
        foreach (var kv in keyed)
            C(!kv.Value.Contains('—') && !kv.Value.Contains('!'), $"{kv.Key}: no em dash, no exclamation mark");

        // The Almanac and the README.
        var alm = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Hauling.xml")).Root!.Elements().Single();
        string almText = string.Concat(alm.Descendants().Select(e => e.HasElements ? "" : e.Value + " "));
        C((string?)alm.Element("defName") == "Council_Alm_C_Hauling" && almText.Contains("can be removed") && almText.Contains("Pick Up And Haul") && almText.Contains("30"),
            "the Almanac's page: the run, Pick Up And Haul, the standalone mod that can be removed");
        string modPage = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Mods_F_QualityOfLife.xml"));
        C(Regex.IsMatch(modPage, @"Council_AlmMod_F_HarvestAndHaul.*?can be removed", RegexOptions.Singleline), "the standalone mod's own page says so too");
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        C(readme.Contains("### Carrying and hauling") && Regex.IsMatch(readme, @"Harvest and Haul.{0,300}can be removed", RegexOptions.Singleline), "the README says the standalone mod can be removed");
        return n;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
