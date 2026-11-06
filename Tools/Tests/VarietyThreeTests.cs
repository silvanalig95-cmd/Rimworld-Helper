using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The third round of variety (the player's request, 6 November 2026): eight council questions about what the colony
/// meets (the inspired hands, the shakes, the food that won't keep, the stores in the rain, the old walls, the new room,
/// what to learn next, the hurt beast), four events (the wild one, the fence down, the ambrosia ripe, a clutch in the
/// grass) and six dangers on the map (the creak at the dig, the leaning tree, smoke in the dry grass, the old shell, the
/// twitching scrap, the ground giving). The rules without game types (Dilemmas/VarietyThreeMath.cs), and the defs and code:
/// every age's forms, the land's, answers that play out on the map, every hurt through the harm service and named before
/// the answer, told in two wordings, the dangers only with hurts on, and nothing that asks more often.
/// </summary>
public static class VarietyThreeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "variety three: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);

        // ================= The rules =================

        C(VarietyThreeMath.Fine(60f, 5f) && !VarietyThreeMath.Fine(12f, 5f) && !VarietyThreeMath.Fine(0f, 5f), "the fine stuff is worth three times the plain one or more");
        C(VarietyThreeMath.SpoilsSoon(0.5f) && VarietyThreeMath.SpoilsSoon(VarietyThreeMath.SpoilsWithinDays) && !VarietyThreeMath.SpoilsSoon(3f) && !VarietyThreeMath.SpoilsSoon(-1f),
            "food that won't keep rots within a day and a half, never food already rotten");
        C(!VarietyThreeMath.SpoilingWorth(2f) && VarietyThreeMath.SpoilingWorth(VarietyThreeMath.SpoilingWorthNutrition), "a little food going off isn't worth the council's time");
        C(VarietyThreeMath.Meals(10f, 0.5f) == 20 && VarietyThreeMath.Meals(30f, 0.5f) == 20 && VarietyThreeMath.Meals(0.6f, 0.5f) == 2 && VarietyThreeMath.Meals(0f, 0.5f) == 0,
            "a cooking bill of the food's meals, two to twenty");
        C(VarietyThreeMath.RainWorth(VarietyThreeMath.OpenCells, VarietyThreeMath.WornStacks) && !VarietyThreeMath.RainWorth(2, 10) && !VarietyThreeMath.RainWorth(20, 1),
            "the stores in the rain: open ground, and things there the weather wears");
        C(VarietyThreeMath.RuinWorth(VarietyThreeMath.RuinPieces) && !VarietyThreeMath.RuinWorth(2), "a ruin is a few walls, not a stone");
        C(VarietyThreeMath.BetterRoom(40f, 20f) && !VarietyThreeMath.BetterRoom(25f, 20f), "a new room is clearly better than where they sleep");

        // The dangers' hurts: the game's rate, small wounds, the harm service's scale on every chance.
        var chances = new[] { VarietyThreeMath.DigOnChance, VarietyThreeMath.StampBurnChance, VarietyThreeMath.BlastStoneChance, VarietyThreeMath.ScrapSparkBreak,
            VarietyThreeMath.ScrapSparkLeft, VarietyThreeMath.GroundGiveChance };
        C(chances.All(c => c > 0f && c <= 0.3f), "each danger's hurt comes at most three times in ten, before the scale");
        var highs = new[] { VarietyThreeMath.RockfallHigh, VarietyThreeMath.StampBurnHigh, VarietyThreeMath.TreeCrushHigh, VarietyThreeMath.BlastStoneHigh,
            VarietyThreeMath.ScrapBurnHigh, VarietyThreeMath.GroundBruiseHigh };
        C(highs.All(h => h <= 9f), "a danger's wound is a small one (the size of a drill's bad blow at most)");
        C(VarietyThreeMath.TreeFallPerHour(true) > VarietyThreeMath.TreeFallPerHour(false) * 10f, "a leaning tree comes down in a wind, rarely in still air");
        C(VarietyThreeMath.TreeWallDamage(300, 300) == 180 && VarietyThreeMath.TreeWallDamage(50, 300) == 49 && VarietyThreeMath.TreeWallDamage(1, 300) == 0,
            "the falling tree breaks the wall and never brings it down");
        C(VarietyThreeMath.ShellStandOff > VarietyThreeMath.ShellClear && VarietyThreeMath.ShellClear >= 5f, "the shell goes up with nobody near it, and its setter stands well back");
        C(VarietyThreeMath.AmbrosiaForANight(10, 4) == 4 && VarietyThreeMath.AmbrosiaForANight(2, 6) == 2 && !VarietyThreeMath.AmbrosiaWorth(2), "one night's ambrosia is one a person, no more than is ripe");
        C(VarietyThreeMath.EggsLow >= 2 && VarietyThreeMath.EggsHigh <= 8, "a clutch is a few eggs");
        // At most once or twice a year a danger comes, its risky answer chosen half the time: a fraction of a hurt a year.
        C(chances.All(c => VarietyThreeMath.HurtsAYear(1.5f, 0.5f, c, 1f) <= 0.25f), "a danger hurts someone a quarter of a time a year at most, at the default");
        C(VarietyThreeMath.HurtsAYear(1.5f, 0.5f, 0.25f, 0f) == 0f, "nothing at all with hurts off");

        // ================= The questions =================

        const string dlmFile = "Defs/DilemmaDefs/Rimfolk_Dilemmas_Variety3.xml";
        var workers = Code("Dilemmas/DilemmaWorkers_Variety3.cs");
        var onMap = new[] { "DilemmaEffect_ArtBill", "DilemmaEffect_Dose", "DilemmaEffect_RideItOut", "DilemmaEffect_CookIt", "DilemmaEffect_RoofStores", "DilemmaEffect_GiveUpOpen",
            "DilemmaEffect_OldWalls", "DilemmaEffect_ClaimBed", "DilemmaEffect_Study", "DilemmaEffect_MarkBeast" };
        var questions = new[] { "Rimfolk_DlmE_Inspired", "Rimfolk_DlmE_Withdrawal", "Rimfolk_DlmE_Spoiling", "Rimfolk_DlmE_StoresInRain", "Rimfolk_DlmE_OldWalls",
            "Rimfolk_DlmE_NewRoom", "Rimfolk_DlmE_NextStudy", "Rimfolk_DlmE_HurtBeast" };
        var allDlm = XDocument.Load(Path.Combine(root, dlmFile)).Root!.Elements("ColonyCouncil.DilemmaDef").ToList();
        C(allDlm.Count == questions.Length, $"eight questions ({allDlm.Count})");
        foreach (var name in questions)
        {
            var d = allDlm.FirstOrDefault(e => (string)e.Element("defName") == name);
            C(d != null, name + ": there");
            if (d == null) continue;
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker.Length > 0 && workers.Contains("class " + worker + " : DilemmaWorker"), $"{name}: its worker ({worker}) is in the code");
            C(d.Element("timely") == null && d.Element("withinDaysOfThreat") == null && d.Element("seasons") == null,
                $"{name}: asked in the usual rhythm, never outside it (no more questions a year)");
            C(float.Parse((string)d.Element("weight") ?? "1", System.Globalization.CultureInfo.InvariantCulture) <= 1.5f, $"{name}: an ordinary weight in the pool");
            var texts = Lis(d, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => t.StartsWith("[!lowtech]")), $"{name}: a form for a low-tech colony and one for the rest");
            C(texts.Any(t => !t.StartsWith("[")), $"{name}: a line for any colony");
            var options = d.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)d.Element("defaultOption") ?? "0");
            C(options.Count == 3 && def >= 0 && def < options.Count, $"{name}: three answers, the council's silence one of them");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(e => onMap.Contains(Cls(e))) == true) >= 2, $"{name}: two answers play out on the map");
            foreach (var o in options)
            {
                string label = (string)o.Element("label") ?? "?";
                C(o.Element("effects")?.Elements("li").Any() == true, $"{name} [{label}]: does something");
                string words = (string)o.Element("outcome") ?? "";
                C(!words.Contains('—') && !words.Contains('!'), $"{name} [{label}]: plain prose");
            }
            C(options[def].Element("effects")!.Elements("li").All(e => !onMap.Contains(Cls(e))), $"{name}: the council's silence sets nothing going");
        }
        foreach (var effect in onMap)
        {
            var body = Regex.Match(workers, "class " + effect + @" : DilemmaEffect\s*\{(.*?)\n    \}", RegexOptions.Singleline).Groups[1].Value;
            C(body.Contains("public override void Describe(") && body.Contains("public override string Unavailable("), $"{effect}: says what it does under the answer, and greys it out when it can't");
        }
        C(workers.Contains("InspirationDefOf.Inspired_Creativity") && workers.Contains("SetPawnRestriction(ctx.a)") && workers.Contains("ingredientFilter.SetAllow(stuff, true)"),
            "the inspired hands: the game's inspiration, a bill for them alone in the stuff chosen");
        C(workers.Contains("Hediff_Addiction") && workers.Contains("\"Luciferium\"") && workers.Contains("lethalSeverity <= 0f"), "the shakes: the game's withdrawal, never one that can kill");
        C(workers.Contains("JobDefOf.Ingest") && workers.Contains("drugs.CurrentPolicy = policy"), "a dose is the game's own job; riding it out is the colony's own drug policy");
        C(workers.Contains("TicksUntilRotAtCurrentTemp") && workers.Contains("\"CookMealSimple\""), "the food that won't keep: the game's own rot and its simple meal");
        C(workers.Contains("areaManager.BuildRoof[c] = true") && workers.Contains("WithinRangeOfRoofHolder") && workers.Contains("z.RemoveCell(c)"),
            "the stores in the rain: the game's roof area where a roof can hold, or the stockpile drawn in");
        C(workers.Contains("DesignationDefOf.Deconstruct") && workers.Contains("SetFaction(Faction.OfPlayer)") && workers.Contains("b.Faction == null"), "the old walls: nobody's ruin, pulled down or claimed the game's way");
        C(workers.Contains("ClaimBedIfNonMedical") && workers.Contains("SetCurrentProject") && workers.Contains("Studies.Idle"), "the new room is the game's ownership; a study only when none is chosen");
        C(workers.Contains("DesignationDefOf.Tame") && workers.Contains("GetManhunterOnTameFailChance") && workers.Contains("CC_Impact_TameRisk"),
            "taming is the game's, and its own risk is named before the answer");
        C(!workers.Contains("Harm."), "the questions hurt nobody of Rimfolk's own");

        // ================= The events =================

        const string evtFile = "Defs/IncidentDefs/Rimfolk_Events_Variety3.xml";
        var evtCode = Code("Happenings/Workers_Variety3.cs");
        var events = XDocument.Load(Path.Combine(root, evtFile)).Root!.Elements("IncidentDef").ToList();
        C(events.Count == 4, $"four events ({events.Count})");
        foreach (var e in events)
        {
            string name = (string)e.Element("defName");
            var ext = e.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var texts = Lis(ext, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => t.StartsWith("[!lowtech]")) && texts.Any(t => !t.StartsWith("[")), $"{name}: every age's forms");
            C(ext.Element("battle") == null && (string)ext.Element("group") == "Nature", $"{name}: the land's, never a fight of its own");
            C(float.Parse((string)e.Element("baseChance"), System.Globalization.CultureInfo.InvariantCulture) <= 0.8f
              && int.Parse((string)e.Element("minRefireDays")) >= 30, $"{name}: an ordinary share of the pool, and not soon again");
            var options = ext.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)ext.Element("defaultOption") ?? "0");
            C(options.Count >= 2 && def < options.Count, $"{name}: a real choice");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(x => Cls(x) != "DilemmaEffect_Thought") == true) >= 1, $"{name}: something plays out on the map");
            string worker = ((string)e.Element("workerClass")).Replace("ColonyCouncil.", "");
            C(evtCode.Contains("class " + worker + " : IncidentWorker_Happening"), $"{name}: its worker is in the code");
        }
        C(evtCode.Contains("IsWildMan()") && evtCode.Contains("TameUtility.CanTame"), "the wild one is the game's own, and can be tamed");
        C(evtCode.Contains("CompAnimalPenMarker") && evtCode.Contains("PlaceBlueprintForBuild") && evtCode.Contains("DestroyMode.Vanish"),
            "the fence: one of the pen's own, and mended with the game's blueprint (no rebuild of the game's own behind it)");
        C(evtCode.Contains("Plant_Ambrosia") && evtCode.Contains("DesignationDefOf.HarvestPlant") && evtCode.Contains("CC_Impact_AmbrosiaPull"), "the ambrosia is the game's own, and its pull is named");
        C(evtCode.Contains("CompProperties_Hatcher") && evtCode.Contains("hatcheeFaction = Faction.OfPlayer") && evtCode.Contains("AllWildAnimals"),
            "a clutch of a bird that lives on this land, hatched the game's way for the colony");
        C(!evtCode.Contains("Harm."), "the events hurt nobody of Rimfolk's own");

        // ================= The dangers =================

        const string dngFile = "Defs/IncidentDefs/Rimfolk_Events_Dangers.xml";
        var dngCode = Code("Happenings/Dangers.cs");
        var dangers = XDocument.Load(Path.Combine(root, dngFile)).Root!.Elements("IncidentDef").ToList();
        C(dangers.Count == 6, $"six dangers ({dangers.Count})");
        int landForms = 0;
        foreach (var e in dangers)
        {
            string name = (string)e.Element("defName");
            var ext = e.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var texts = Lis(ext, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => Regex.IsMatch(t, @"^\[(!lowtech|industrial|spacer)\]")) && texts.Count >= 4, $"{name}: every age's forms");
            if (texts.Any(t => t.StartsWith("[land:"))) landForms++;
            C(texts.All(t => Regex.IsMatch(t, @"\b(may|might|would|will)\b")), $"{name}: every letter names what can happen before the answer");
            C(ext.Element("battle") == null && (string)ext.Element("group") == "Nature", $"{name}: the land's, never a fight");
            C(float.Parse((string)e.Element("baseChance"), System.Globalization.CultureInfo.InvariantCulture) <= 0.7f
              && int.Parse((string)e.Element("minRefireDays")) >= 40, $"{name}: an ordinary share of the pool at most, and not soon again");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count >= 2 && options.All(o => o.Element("effects")?.Elements("li").Any() == true), $"{name}: each answer plays out on the map");
            var lines = Lis(ext, "lines");
            C(lines.Any(l => l.StartsWith("[warn")) && lines.Any(l => l.StartsWith("[hurt")), $"{name}: a word when it's seen, and when it hurts");
            float answer = float.Parse((string)ext.Element("answerDays") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            C(answer <= 1f, $"{name}: asked quickly: a danger waits for nobody");
            string worker = ((string)e.Element("workerClass")).Replace("ColonyCouncil.", "");
            C(dngCode.Contains("class " + worker + " : HappeningWorker_Danger"), $"{name}: a danger's worker");
        }
        C(landForms >= 4, $"the dangers take the form of the land ({landForms} of six with a land's own lines)");
        C(dngCode.Contains("protected override bool CanHappen(Map map, HappeningExtension ext) => MapDangers.On && !MapFacts.ThreatOn(map)") && dngCode.Contains("public static bool On => Harm.On"),
            "the dangers come only with everyday hurts on, never in a raid");
        C(!dngCode.Contains("health.AddHediff") && !Regex.IsMatch(dngCode, @"\bp\.TakeDamage\(") && Regex.Matches(dngCode, @"Harm\.Injure\(").Count == 1,
            "every hurt is the harm service's wound, in one place");
        foreach (var c in new[] { "DigOnChance", "StampBurnChance", "BlastStoneChance", "ScrapSparkLeft", "ScrapSparkBreak", "GroundGiveChance" })
            C(Regex.IsMatch(dngCode, @"Harm\.Chance\(VarietyThreeMath\." + c + @"\)|Spark\(k, \w+, VarietyThreeMath\." + c + @"\)"), $"{c}: on the harm service's scale");
        C(dngCode.Contains("Rand.Chance(Harm.Chance(chance))"), "a spark's chance is on the scale too");
        foreach (Match m in Regex.Matches(dngCode, @"Wound\([^;]*?""(CC_Danger_\w+)""\)"))
        {
            string key = m.Groups[1].Value;
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2"), $"{key}: told in two wordings");
        }
        foreach (var effect in new[] { "DilemmaEffect_DigFace", "DilemmaEffect_LeaningTree", "DilemmaEffect_Smoulder", "DilemmaEffect_OldShell", "DilemmaEffect_Scrap", "DilemmaEffect_SoftGround" })
        {
            var body = Regex.Match(dngCode, "class " + effect + @" : DilemmaEffect\s*\{(.*?)\n    \}", RegexOptions.Singleline).Groups[1].Value;
            C(body.Contains("public override void Describe(") && body.Contains("ImpactKind.Health, -1"), $"{effect}: names the hurt under the answer before it's chosen");
        }
        C(dngCode.Contains("FireUtility.TryStartFireIn") && dngCode.Contains("CompExplosive") && dngCode.Contains("ThingDefOf.Shell_HighExplosive") && dngCode.Contains("TerrainDefOf.Mud"),
            "the game's own fire, shell, blast and mud");
        C(dngCode.Contains("Filth_RubbleRock") && dngCode.Contains("Filth_LooseGround") && dngCode.Contains("Filth_MachineBits") && dngCode.Contains("ThrowSmoke") && dngCode.Contains("ThrowMicroSparks"),
            "seen coming: grit, heaved ground, sparks and smoke, the game's own");
        C(dngCode.Contains("VarietyThreeMath.TreeWallDamage(") && !dngCode.Contains("wall.Destroy("), "the falling tree breaks a wall, never brings it down");
        var timed = Code("PlayOut/OnMapTimed.cs");
        foreach (var kind in new[] { "DigFace", "Leaning", "Smoulder", "Shell", "Scrap", "SoftGround" })
            C(Regex.Matches(timed, @"case TimedKind\." + kind + ":").Count == 2, $"what the {kind} keeps on the map is stepped and ended by the dangers");
        foreach (Match m in Regex.Matches(workers + evtCode + dngCode, "\"(CC_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"the words for {m.Groups[1].Value}");

        // ================= Memories, and the audit =================

        var thoughts = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Rimfolk_Thoughts_Variety3.xml")).Root!.Elements("ThoughtDef").ToList();
        string allDefs = File.ReadAllText(Path.Combine(root, dlmFile)) + File.ReadAllText(Path.Combine(root, evtFile));
        foreach (var th in thoughts)
        {
            string name = (string)th.Element("defName");
            C(allDefs.Contains(">" + name + "<"), $"{name}: a memory an answer gives");
            float mood = float.Parse(th.Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture);
            C(Math.Abs(mood) <= 4f, $"{name}: small");
        }
        C(!File.ReadAllText(Path.Combine(root, dngFile)).Contains("DilemmaEffect_Thought"), "the dangers give no mood: a hurt is the game's pain, nothing on top");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        foreach (var effect in onMap.Concat(new[] { "DilemmaEffect_MendFence", "DilemmaEffect_PickAmbrosia", "DilemmaEffect_Clutch", "DilemmaEffect_DigFace", "DilemmaEffect_LeaningTree",
                     "DilemmaEffect_Smoulder", "DilemmaEffect_OldShell", "DilemmaEffect_Scrap", "DilemmaEffect_SoftGround" }))
            C(audit.Contains("\"" + effect + "\""), $"{effect} counts as playing out on the map");
        return n;
    }
}
