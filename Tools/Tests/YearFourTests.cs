using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The council and the colony's year, fourth round (the player's request, 6 November 2026, "another big content update to
/// existing mechanics"): ten council questions about the colony's own systems (the fields, the snow, the plans, the trees
/// by the wall, the herd's winter, an animal's training, the road home, a doubter and the faith's guide), four customs of
/// the autumn and the winter, five decrees (a hunting season, wood for the winter, foraging days, saving the power, every
/// faith its evening) and seven advisor checks. The rules without game types (Dilemmas/YearFourMath.cs), and the defs and
/// code: every age's forms, answers that play out on the map, nothing that asks more often or gathers people more, no hurt
/// of Rimfolk's own, the decrees each meaning one thing and the faith's two ways never together.
/// </summary>
public static class YearFourTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "year four: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(string s, float d) => s == null ? d : float.Parse(s, CultureInfo.InvariantCulture);
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        bool Plain(string s)
        {
            s = Regex.Replace(s ?? "", @"^\[[^\]]*\]\s*", "");
            return !s.Contains('—') && !s.Contains('!');
        }

        // ================= The rules =================

        C(YearFourMath.FieldEmpty(5, 40) && !YearFourMath.FieldEmpty(20, 40) && !YearFourMath.FieldEmpty(0, 10), "an empty field: big enough, and under two fifths sown");
        C(YearFourMath.MedicineShort(2, 6) && !YearFourMath.MedicineShort(6, 6) && !YearFourMath.MedicineShort(9, 20) && !YearFourMath.MedicineShort(0, 0),
            "medicine short: under a dose a colonist and under a handful");
        C(YearFourMath.ClothShort(50, 6) && !YearFourMath.ClothShort(200, 6), "cloth short: under a shirt or two each");
        C(YearFourMath.Snowed(40, 100, 0) && !YearFourMath.Snowed(10, 100, 0) && !YearFourMath.Snowed(40, 100, 60) && !YearFourMath.Snowed(0, 0, 0),
            "snow on the paths: deep over a third of the open ground, and next to nowhere set to clear");
        C(YearFourMath.ManyPlans(8) && !YearFourMath.ManyPlans(7), "the unfinished work: eight plans standing at least");
        C(YearFourMath.TreeLine(5) && !YearFourMath.TreeLine(4) && YearFourMath.TreeReach < 8f, "the trees by the wall: five at least, close to it");
        C(YearFourMath.AskedLately(0, 29 * 60000) && !YearFourMath.AskedLately(0, 30 * 60000) && !YearFourMath.AskedLately(-1, 0),
            "the wish about the same trees or snow waits 30 days after the question");
        string wishCode = Code("Wishes/WishWorkers_People4.cs"), dlmCode = Code("Dilemmas/DilemmaWorkers_YearFour.cs");
        C(wishCode.Contains("YearFour.AskedLately(map, \"Rimfolk_DlmY_TreeLine\")") && wishCode.Contains("YearFour.AskedLately(map, \"Rimfolk_DlmY_SnowPaths\")"),
            "the trees and snow wishes wait after the council's question");
        C(dlmCode.Contains("YearFour.WishOpen(map, \"Rimfolk_Wish_TreesByWalls\")") && dlmCode.Contains("YearFour.WishOpen(map, \"Rimfolk_Wish_SnowWays\")"),
            "the trees and snow questions wait while the colonist's wish is open");
        C(YearFourMath.Half(9) == 5 && YearFourMath.Half(8) == 4 && YearFourMath.Half(1) == 1 && YearFourMath.Half(0) == 0, "the nearest half, rounded up");
        C(Math.Abs(YearFourMath.FeedDays(100f, 10f) - 10f) < 0.01f && YearFourMath.FeedDays(100f, 0f) > 1000f && YearFourMath.FeedDays(-5f, 1f) == 0f,
            "the feed lasts what's in store over what the herd eats");
        C(YearFourMath.HerdTooBig(8, 10f, 30f) && !YearFourMath.HerdTooBig(8, 25f, 30f) && !YearFourMath.HerdTooBig(4, 5f, 30f) && !YearFourMath.HerdTooBig(8, 5f, 0f),
            "too many in the pens: a herd, a winter coming, and feed for well under it");
        C(YearFourMath.ToCull(9, 15f, 30f) == 3 && YearFourMath.ToCull(30, 0f, 30f) == YearFourMath.CullMost && YearFourMath.ToCull(6, 29f, 30f) == 1
          && YearFourMath.ToCull(6, 0f, 30f) == 2 && YearFourMath.ToCull(0, 0f, 30f) == 0,
            "marked so the feed lasts: at least one, at most four and a third of the herd");
        C(YearFourMath.FreshHome(6f, 0.5f) && !YearFourMath.FreshHome(3f, 0.5f) && !YearFourMath.FreshHome(6f, 2f), "home from the road: five days away, back within a day and a half");
        C(YearFourMath.Doubts(0.2f) && !YearFourMath.Doubts(0.5f) && YearFourMath.GuideSteadies > YearFourMath.AltarSteadies && YearFourMath.GuideSteadies <= 0.15f,
            "a doubter: under a third certain; the guide steadies more than the altar alone, within the shared core's cap");
        C(YearFourMath.WoodWanted(5, true) > YearFourMath.WoodWanted(5, false) && YearFourMath.WoodWanted(0, true) == YearFourMath.WoodEachLowTech,
            "wood for the winter: more for a colony that heats with it");
        C(YearFourMath.TreesToday(1000, 500, 25f, 0) == 0 && YearFourMath.TreesToday(0, 500, 25f, 0) == YearFourMath.TreesADay
          && YearFourMath.TreesToday(450, 500, 25f, 0) == 2 && YearFourMath.TreesToday(450, 500, 25f, 2) == 0,
            "trees a morning: none once the stores are full, a morning's at most, counting those already marked");
        C(YearFourMath.LightsOut(23) && YearFourMath.LightsOut(2) && !YearFourMath.LightsOut(5) && !YearFourMath.LightsOut(12), "the lamps out from eleven to five");
        C(YearFourMath.PowerShort(100f, 1000f) && !YearFourMath.PowerShort(500f, 1000f) && !YearFourMath.PowerShort(0f, 0f), "power short: batteries under a third full, and some batteries");
        C(YearFourMath.ToleranceTrust(0f) == 0.25f && YearFourMath.ToleranceTrust(1f) == 0.5f && YearFourMath.ToleranceTrust(9f) == 0.5f && YearFourMath.DevoutMinds < 0f,
            "every faith its evening: a quarter to a half a point of trust a morning, the devout a quarter less");
        C(YearFourMath.FuelLow(5f, 1f, 0) && !YearFourMath.FuelLow(5f, 1f, 10) && !YearFourMath.FuelLow(50f, 1f, 0) && !YearFourMath.FuelLow(5f, 0f, 0),
            "a generator running dry: under half a day's fuel, none in the stores");
        C(YearFourMath.TooCold(-20f, -10f) && !YearFourMath.TooCold(0f, -10f), "an animal out in the cold: colder than it can bear");

        // ================= The questions =================

        const string dlmFile = "Defs/DilemmaDefs/Rimfolk_Dilemmas_YearFour.xml";
        var workers = Code("Dilemmas/DilemmaWorkers_YearFour.cs");
        var onMap = new[] { "DilemmaEffect_WorkParty", "DilemmaEffect_GiveUpField", "DilemmaEffect_SetCrop", "DilemmaEffect_SnowArea", "DilemmaEffect_CancelPlans",
            "DilemmaEffect_MarkTrees", "DilemmaEffect_MarkSlaughter", "DilemmaEffect_SetTraining", "DilemmaEffect_RestTravellers", "DilemmaEffect_Visit",
            "DilemmaEffect_VisitAltar", "DilemmaEffect_NameGuide" };
        var own = new[] { "DilemmaEffect_GiveUpField", "DilemmaEffect_SetCrop", "DilemmaEffect_SnowArea", "DilemmaEffect_CancelPlans", "DilemmaEffect_MarkTrees",
            "DilemmaEffect_MarkSlaughter", "DilemmaEffect_SetTraining", "DilemmaEffect_NameGuide" };
        var gather = new[] { "DilemmaEffect_Gathering", "DilemmaEffect_Feast", "DilemmaEffect_Bonfire", "DilemmaEffect_Toast", "DilemmaEffect_Words", "DilemmaEffect_FaithWords",
            "DilemmaEffect_FaithEvening" };
        var questions = new[] { "Rimfolk_DlmY_EmptyField", "Rimfolk_DlmY_WhichCrop", "Rimfolk_DlmY_SnowPaths", "Rimfolk_DlmY_Unfinished", "Rimfolk_DlmY_TreeLine",
            "Rimfolk_DlmY_HerdTooBig", "Rimfolk_DlmY_TrainBeast", "Rimfolk_DlmY_HomeFromRoad", "Rimfolk_DlmY_Doubter", "Rimfolk_DlmY_NoGuide" };
        var allDlm = XDocument.Load(Path.Combine(root, dlmFile)).Root!.Elements("ColonyCouncil.DilemmaDef").ToList();
        C(allDlm.Count == questions.Length, $"ten questions ({allDlm.Count})");
        int outings = 0;
        foreach (var name in questions)
        {
            var d = allDlm.FirstOrDefault(e => (string)e.Element("defName") == name);
            C(d != null, name + ": there");
            if (d == null) continue;
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker.Length > 0 && workers.Contains("class " + worker + " : DilemmaWorker"), $"{name}: its worker ({worker}) is in the code");
            C(d.Element("timely") == null && d.Element("withinDaysOfThreat") == null && d.Element("seasons") == null && d.Element("traditionOption") == null,
                $"{name}: asked in the usual rhythm, never outside it, and never a custom (no more questions a year)");
            C(F((string)d.Element("weight"), 1f) <= 1.3f && F((string)d.Element("cooldownDays"), 40f) >= 20f, $"{name}: an ordinary weight in the pool, and not soon again");
            var texts = Lis(d, "texts");
            C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*\blowtech\b")) && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*!lowtech")), $"{name}: a form for a low-tech colony and one for the rest");
            C(texts.Any(t => !t.StartsWith("[")), $"{name}: a line for any colony");
            C(texts.All(Plain), $"{name}: plain prose in the letter");
            var options = d.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)d.Element("defaultOption") ?? "0");
            C(options.Count == 3 && def >= 0 && def < options.Count, $"{name}: three answers, the council's silence one of them");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(e => onMap.Contains(Cls(e))) == true) >= 2, $"{name}: two answers play out on the map");
            C(options[def].Element("effects")!.Elements("li").All(e => !onMap.Contains(Cls(e))), $"{name}: the council's silence sets nothing going");
            C(options.All(o => o.Element("effects")!.Elements("li").All(e => !gather.Contains(Cls(e)))), $"{name}: no answer gathers the colony (staged scenes a week don't rise)");
            if (options.Any(o => o.Element("effects")!.Elements("li").Any(e => Cls(e) == "DilemmaEffect_WorkParty"))) outings++;
            foreach (var o in options)
            {
                string label = (string)o.Element("label") ?? "?";
                C(o.Element("effects")?.Elements("li").Any() == true, $"{name} [{label}]: does something");
                C(o.Element("effects")!.Elements("li").Any(e => Cls(e) == "DilemmaEffect_Trust" || Cls(e) == "DilemmaEffect_Thought"), $"{name} [{label}]: costs or pleases someone");
                string words = string.Join(" ", new[] { label, (string)o.Element("outcome") ?? "" }.Concat(Lis(o, "outcomes")));
                C(Plain(words), $"{name} [{label}]: plain prose");
            }
        }
        C(outings <= 3, $"a work party on three questions at most ({outings}), each with an answer that stays at home");
        foreach (var effect in own)
        {
            var body = Regex.Match(workers, "class " + effect + @" : DilemmaEffect\s*\{(.*?)\n    \}", RegexOptions.Singleline).Groups[1].Value;
            C(body.Contains("public override void Describe(") && body.Contains("public override string Unavailable("), $"{effect}: says what it does under the answer, and greys it out when it can't");
            C(body.Contains("Tell.Message("), $"{effect}: tells what was done");
        }
        C(workers.Contains("z.Delete()") && workers.Contains("SetPlantDefToGrow") && workers.Contains("Sowing.MakeZone"), "the field is the game's own zone, given up, set to a crop, or split");
        C(workers.Contains("sowResearchPrerequisites") && workers.Contains("PlantUtility.CanSowOnGrower") && workers.Contains("GrowthSeasonNow"),
            "a crop only the colony can sow there now, in its season");
        C(workers.Contains("SnowOrSandClear") && workers.Contains("area[c] = true"), "the snow is cleared through the game's own snow-clearing area");
        C(workers.Contains("TotalMaterialCost()") && workers.Contains("DestroyMode.Cancel"), "the unpaid plans are the game's own, taken off with its cancel");
        C(workers.Contains("DesignationDefOf.CutPlant") && workers.Contains("TreeCategory.Super") && workers.Contains("Belief.NoTreeCutting"),
            "trees marked the game's way, never a sacred tree, the faith's view named");
        C(workers.Contains("DesignationDefOf.Slaughter") && workers.Contains("Bonded(a)") && workers.Contains("Pregnant(a)") && workers.Contains("SeasonUtil.Winter"),
            "the herd: the game's slaughter, never one bonded or carrying young, the winter by the advisor's reckoning");
        C(workers.Contains("SetWantedRecursive") && workers.Contains("CanAssignToTrain"), "training is the game's own");
        C(workers.Contains("cal.LastHome(") && workers.Contains("RestFor("), "home from the road: the calendar's homecoming look, and its rest timetable");
        C(workers.Contains("Faiths.Steady(") && workers.Contains("IdeoRole_Moralist") && workers.Contains("role.Assign(p, true)") && workers.Contains("RequirementsMet"),
            "the faith: steadied through the shared core, the guide the game's own role");
        C(!workers.Contains("Harm."), "the questions hurt nobody of Rimfolk's own");
        var watch = Code("Calendar/CalendarWatch.cs");
        C(watch.Contains("public bool LastHome(") && Regex.IsMatch(watch, @"\[Unsaved\] private List<string> lastHomeIds"), "the homecoming kept for the question, unsaved");

        // ================= The customs =================

        const string custFile = "Defs/DilemmaDefs/Rimfolk_Dilemmas_Customs_YearFour.xml";
        var customs = XDocument.Load(Path.Combine(root, custFile)).Root!.Elements("ColonyCouncil.DilemmaDef").ToList();
        C(customs.Count == 4, $"four customs ({customs.Count})");
        int fallWinter = 0;
        foreach (var d in customs)
        {
            string name = (string)d.Element("defName");
            var seasons = Lis(d, "seasons");
            C(seasons.Count > 0 && (string)d.Element("traditionOption") == "0" && !((string)d.Element("traditionLabel")).NullOrEmptyString(),
                $"{name}: a seasonal custom that can become a tradition (the one-a-season rule holds it)");
            if (seasons.All(s => s == "Fall" || s == "Winter" || s == "PermanentWinter")) fallWinter++;
            C(d.Element("timely") == null && F((string)d.Element("weight"), 1f) <= 0.8f && F((string)d.Element("cooldownDays"), 40f) >= 50f, $"{name}: a custom's weight and rest");
            var texts = Lis(d, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => t.StartsWith("[!lowtech]")) && texts.Any(t => !t.StartsWith("[")) && texts.Any(t => t.StartsWith("[again]")),
                $"{name}: every age's forms, and a line for a custom kept before");
            var options = d.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && (string)d.Element("defaultOption") == "1", $"{name}: the custom, a smaller form, and a refusal; silence the smaller form");
            C(options.Take(2).All(o => o.Element("effects")!.Elements("li").Any(e => PlayKinds.Contains(Cls(e)))), $"{name}: both keepings play out on the map");
            C(options[0].Element("effects")!.Elements("li").Any(e => Cls(e) == "DilemmaEffect_Thought" && (string)e.Element("thought") == "Rimfolk_Dlm_Trad_" + name.Replace("Rimfolk_Dlm_", "")),
                $"{name}: its own memory when it's kept");
            C(options[2].Element("effects")!.Elements("li").All(e => !PlayKinds.Contains(Cls(e))), $"{name}: the refusal sets nothing going");
            foreach (var o in options)
                C(Plain(string.Join(" ", new[] { (string)o.Element("label"), (string)o.Element("outcome") }.Concat(Lis(o, "outcomes")))), $"{name} [{(string)o.Element("label")}]: plain prose");
        }
        C(fallWinter == customs.Count, "the customs go to the seasons that had fewest (the autumn and the winter)");
        var woodpile = customs.First(d => (string)d.Element("defName") == "Rimfolk_Dlm_Woodpile");
        C(woodpile.Descendants("woodShort").Any(x => x.Value == "true") && Code("Dilemmas/DilemmaWorkers_Customs.cs").Contains("c.woodShort && !YearFour.WoodShort(map)")
          && File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs")).Contains("[\"woodShort\"]"),
            "the woodpile day only while the stores are short of the winter's wood (and the balance model knows it)");
        C(customs.First(d => (string)d.Element("defName") == "Rimfolk_Dlm_WinterTales").Element("roleA")?.Element("passion")?.Value == "Social", "the winter tales told by one who loves to talk");
        C(customs.All(d => d.Element("options")!.Elements("li").Skip(1).First().Element("effects")!.Elements("li")
              .Any(e => Cls(e) == "DilemmaEffect_Thought" && (string)e.Element("target") == "Colony" && e.Element("chance") != null)),
            "each custom's smaller form pleases most of the colony too, so the full one isn't plainly best");
        var tales = customs.First(d => (string)d.Element("defName") == "Rimfolk_Dlm_WinterTales").Descendants("lines").SelectMany(l => l.Elements("li")).Select(x => x.Value).ToList();
        C(tales.Count >= 4 && tales.All(t => Regex.Matches(t.TrimEnd('.'), @"[.?]\s").Count == 0), "the winter tales' lines are one sentence each");
        var timed = Code("PlayOut/OnMapTimed.cs");
        C(timed.Contains("if (where == \"graves\") return GraveCells(") && timed.Contains("g.Corpse.InnerPawn.Faction == Faction.OfPlayer"),
            "the night of lights: a light by each grave of the colony's own dead");
        C(File.ReadAllText(Path.Combine(root, custFile)).Contains("<at>graves</at>") && keyed.ContainsKey("CC_Impact_Lanterns_graves"), "the lights by the graves are named under the answer");

        // ================= The decrees =================

        const string decFile = "Defs/DecreeDefs/Rimfolk_Decrees_YearFour.xml";
        var decrees = XDocument.Load(Path.Combine(root, decFile)).Root!.Elements("ColonyCouncil.DecreeDef").ToList();
        C(decrees.Count == 5, $"five decrees ({decrees.Count})");
        var allDecrees = Directory.GetFiles(Path.Combine(root, "Defs", "DecreeDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DecreeDef")).ToList();
        var thoughtDefs = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Rimfolk_Thoughts_YearFour.xml")).Root!.Elements("ThoughtDef").ToList();
        foreach (var d in decrees)
        {
            string name = (string)d.Element("defName");
            string kind = (string)d.Element("kind");
            C(new[] { "work", "stores", "order", "spirit", "faith", "world" }.Contains(kind), $"{name}: listed under a part of the Decrees page");
            var th = thoughtDefs.FirstOrDefault(t => (string)t.Element("defName") == (string)d.Element("thought"));
            C(th != null && th.Element("stages")!.Elements("li").Count() == 4, $"{name}: its mood, hate to love");
            C(d.Element("hurts") == null, $"{name}: no hurt of Rimfolk's own");
            C(Plain((string)d.Element("description")) && Lis(d, "effects").All(Plain), $"{name}: plain prose");
            C(Lis(d, "effects").Count >= 3 && !Lis(d, "requestWhen").NullOrEmptyList(), $"{name}: what it does in lines, and when someone asks for it");
            C(d.Element("onEnact") == null, $"{name}: declared, it calls no gathering of its own (staged scenes a week don't rise)");
            bool distinct = d.Element("daily") != null || (string)d.Element("lightsOut") == "true" || (string)d.Element("everyFaith") == "true";
            C(distinct, $"{name}: means one thing on the map");
        }
        string daily(string name) => string.Join(",", decrees.First(d => (string)d.Element("defName") == name).Element("daily")!.Elements("li").Select(Cls));
        C(daily("Rimfolk_Decree_HuntingSeason") == "DilemmaEffect_Hunt" && daily("Rimfolk_Decree_WoodForWinter") == "DilemmaEffect_MarkTrees" && daily("Rimfolk_Decree_Foraging") == "DilemmaEffect_Forage",
            "the land's year: game, trees and wild plants marked each morning, one each");
        string desc(string name) => (string)decrees.First(d => (string)d.Element("defName") == name).Element("description");
        C(new[] { "Rimfolk_Decree_HuntingSeason", "Rimfolk_Decree_WoodForWinter", "Rimfolk_Decree_Foraging" }.All(x => Regex.IsMatch(desc(x), @"\btribe\b") && Regex.IsMatch(desc(x), @"\b(industrial|spacer)\b")),
            "each decree of the land's year says how a tribe and a later colony keep it");
        C(Regex.IsMatch(desc("Rimfolk_Decree_SavePower"), @"\btribe\b.*banks its fires"), "saving the power names a low-tech colony's form of it (banked fires)");
        foreach (var d in allDecrees.Where(d => (string)d.Element("label") != null))
            C(allDecrees.Count(x => (string)x.Element("label") == (string)d.Element("label")) == 1, $"{(string)d.Element("label")}: one decree of that name");
        C(DecreeMath.Excludes(DecreeMath.OneFaith, DecreeMath.EveryFaith) && DecreeMath.Excludes(DecreeMath.EveryFaith, DecreeMath.OneFaith), "one faith for the colony and every faith its evening never together");
        C(allDecrees.Any(d => (string)d.Element("defName") == DecreeMath.OneFaith) && allDecrees.Any(d => (string)d.Element("defName") == DecreeMath.EveryFaith), "both faith decrees are there");
        var work = Code("Decrees/DecreeWork.cs");
        C(work.Contains("(d.oneFaith || d.everyFaith) && DecreeFaith.Hidden"), "every faith its evening is listed only as one faith is (Ideology, more than one faith)");
        C(work.Contains("YearFourMath.LightsOut(GenLocalDate.HourOfDay(home))") && work.Contains("Sickroom(b)") && work.Contains("t.lampsOff.Clear()") && work.Contains("want(f) = on"),
            "saving the power: the lamps out at night but never by a patient, back as they were, with nobody sent to flick them");
        C(work.Contains("RunDaily(t, def, map)") && Regex.Matches(work, @"RunDaily\(t, def, map\)").Count == 2, "a decree's morning work, the day it's declared and each morning after");
        C(Regex.IsMatch(Code("Decrees/Decrees.cs"), @"Scribe_Collections\.Look\(ref lampsOff, ""lampsOff"""), "the lamps switched off are saved");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Decrees/Decrees.cs:lampsOff\""), "and modelled for the save's size");
        var sched = Code("Decrees/DecreeWork.cs") + Code("Dilemmas/DilemmaWorkers_YearFour.cs") + Code("Advisor/AdvisorSignals_YearFour.cs");
        C(!sched.Contains("Sched."), "no new slot in the schedule: the decrees' hour and day, the council's draw and the advisor's hour");

        // ================= The advisor =================

        var advCode = Code("Advisor/AdvisorSignals_YearFour.cs");
        var adv = XDocument.Load(Path.Combine(root, "Defs/AdvisorSignalDefs/Rimfolk_Advisor_YearFour.xml")).Root!.Elements("ColonyCouncil.AdvisorSignalDef").ToList();
        C(adv.Count == 7, $"seven advisor checks ({adv.Count})");
        foreach (var a in adv)
        {
            string name = (string)a.Element("defName");
            string worker = ((string)a.Element("workerClass")).Replace("ColonyCouncil.", "");
            C(advCode.Contains("class " + worker + " : AdvisorSignalWorker"), $"{name}: its worker is in the code");
            C(Lis(a, "tips").Count >= 2 && Lis(a, "tips").All(Plain), $"{name}: tips in plain prose");
            C((string)a.Element("article") != null, $"{name}: a page to read more");
        }
        C(adv.Select(a => (string)a.Element("category")).Distinct().Count() >= 5, "the checks fill the advisor's thin parts (power, temperature, defence and more)");
        C(advCode.Contains("Belief.NoTreeCutting"), "a colony whose faith holds trees sacred is never told to cut them");

        // ================= Words, memories and the audit =================

        foreach (Match m in Regex.Matches(workers + advCode + work, "\"(CC_[A-Za-z0-9_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value) || m.Groups[1].Value.EndsWith("_"), $"the words for {m.Groups[1].Value}");
        foreach (var k in keyed.Where(kv => kv.Key.StartsWith("CC_Y4_") || kv.Key.StartsWith("CC_Adv_HerdFeed") || kv.Key.StartsWith("CC_Adv_TreeCover")))
            C(Plain(k.Value), $"{k.Key}: plain prose");
        string allDefs = File.ReadAllText(Path.Combine(root, dlmFile)) + File.ReadAllText(Path.Combine(root, custFile)) + File.ReadAllText(Path.Combine(root, decFile));
        foreach (var th in thoughtDefs)
        {
            string name = (string)th.Element("defName");
            C(allDefs.Contains(">" + name + "<"), $"{name}: a mood the round gives");
            C(th.Descendants("baseMoodEffect").All(x => Math.Abs(F(x.Value, 0f)) <= 4f), $"{name}: small");
        }
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        foreach (var effect in own.Concat(new[] { "DilemmaEffect_Forage", "DilemmaEffect_RestTravellers", "DilemmaEffect_VisitAltar" }))
            C(audit.Contains("\"" + effect + "\""), $"{effect} counts as playing out on the map");
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        foreach (var w in new[] { "EmptyField", "WhichCrop", "SnowPaths", "Unfinished", "TreeLine", "HerdTooBig", "TrainBeast", "HomeFromRoad", "Doubter", "NoGuide" })
            C(rates.Contains("[\"DilemmaWorker_" + w + "\"]"), $"the balance model knows how often {w} can be asked");
        return n;
    }

    /// <summary>The effects that play out on the map, as the play-out audit counts them (the customs use these).</summary>
    private static readonly HashSet<string> PlayKinds = new HashSet<string>
    {
        "DilemmaEffect_WorkParty", "DilemmaEffect_Lanterns", "DilemmaEffect_Vigil", "DilemmaEffect_Lesson", "DilemmaEffect_Words", "DilemmaEffect_Visit"
    };

    private static bool NullOrEmptyString(this string s) => string.IsNullOrEmpty(s);

    private static bool NullOrEmptyList(this List<string> l) => l == null || l.Count == 0;
}
