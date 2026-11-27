using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round's third set of council questions (docs/content-round-3-council.md; the player's go of 27 November 2026:
/// content inside the mechanics there are). The rules without game types (Dilemmas/Round3CouncilMath.cs), and the defs and
/// code: twelve questions, each with its worker (or the stranger's or the world's own), three answers that mostly play out
/// on the map with effects the play-out audit knows, age forms in the words, no countdowns, no god named but by the colony's
/// own placeholder, plain messages, the game's and the player's mods' defs read by name from the active rows, no saved
/// collection and no hurt.
/// </summary>
public static class Round3CouncilTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 3 council: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();

        // ================= The rules =================

        C(Round3CouncilMath.Quieter(1f, 3f) && !Round3CouncilMath.Quieter(1f, 2.5f) && !Round3CouncilMath.Quieter(-1f, 5f) && !Round3CouncilMath.Quieter(2f, -1f),
            "the other bed is quieter only two steps or more farther from the door, and only when both are known");
        C(Round3CouncilMath.InsectWorth(8) && !Round3CouncilMath.InsectWorth(7), "insect meat for eight meals is worth asking about");
        C(Round3CouncilMath.Bills(5f, 2.5f) == 2 && Round3CouncilMath.Bills(0f, 2.5f) == 1 && Round3CouncilMath.Bills(1000f, 2.5f) == 30 && Round3CouncilMath.Bills(5f, 0f) == 1,
            "the kibble bills are as many as the stored meat makes, one at least, thirty at most");
        C(Round3CouncilMath.Hungry(3.9f) && !Round3CouncilMath.Hungry(4f) && !Round3CouncilMath.Hungry(-1f), "the hungry gap: fewer than four days of food");
        C(Round3CouncilMath.EatsFirst(true, 8f, false) && Round3CouncilMath.EatsFirst(false, 60f, false) && Round3CouncilMath.EatsFirst(false, 30f, true) && !Round3CouncilMath.EatsFirst(false, 30f, false),
            "those who eat first: a child, one sixty or older, the ill");
        C(Round3CouncilMath.Tired(0.4f) && !Round3CouncilMath.Tired(0.5f) && !Round3CouncilMath.Tired(-1f), "tired: rest under half");
        C(Round3CouncilMath.Lately(120000, 0, 2f) && !Round3CouncilMath.Lately(120001, 0, 2f) && !Round3CouncilMath.Lately(5, 10, 2f) && !Round3CouncilMath.Lately(5, -1, 2f),
            "lately: within the days, never in the future");
        C(Round3CouncilMath.MealsFor(1) == 4 && Round3CouncilMath.MealsFor(3) == 12 && Round3CouncilMath.MealsFor(100) == 30, "meals without meat: four for each of the faithful, four to thirty");
        C(Round3CouncilMath.PickingAll > Round3CouncilMath.PickingMorning && Round3CouncilMath.PickingFrom >= 1, "the picking day marks more with everyone out than in a morning");
        C(Round3CouncilMath.SeasonDays >= 10f && Round3CouncilMath.ChildcareDays < Round3CouncilMath.SeasonDays, "work handed over for a season; the baby for a few days");

        // ================= The questions =================

        var council = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Round3_Council.xml")).Root!;
        string dlm = Code("Dilemmas/DilemmaWorkers_Round3.cs");
        string strangers = Code("Strangers/Strangers.cs");
        string world = Code("World/Home/WorldQuestions.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        var allSource = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        var names = new[]
        {
            "LightSleeper", "AnimalGrave", "HiveMeat", "PickingDay", "HungryGap", "VisitorFriend", "OurVerse", "Forgetting", "CryingNight", "AfterTantrum", "ScofferGrave", "NoMeatTable"
        };
        var dilemmas = council.Elements("ColonyCouncil.DilemmaDef").ToList();
        C(dilemmas.Count == 12, $"twelve council questions ({dilemmas.Count})");
        var gods = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        foreach (var name in names)
        {
            var d = dilemmas.FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_" + name);
            C(d != null, $"{name}: a council question");
            if (d == null) continue;
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            bool shared = worker == "DilemmaWorker_Stranger" && strangers.Contains("class DilemmaWorker_Stranger ") || worker == "DilemmaWorker_WorldSeason" && world.Contains("class DilemmaWorker_WorldSeason ");
            C(shared || worker.StartsWith("DilemmaWorker_R3") && dlm.Contains("class " + worker + " "), $"{name}: its worker ({worker})");
            var options = d.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            int def = int.TryParse(d.Element("defaultOption")?.Value, out int dv) ? dv : -1;
            C(def >= 0 && def < options.Count, $"{name}: its default answer is one of them");
            var texts = Lis(d, "texts");
            C(texts.Count >= 3, $"{name}: three wordings or more");
            C(texts.Any(t => Regex.IsMatch(t, @"^\[(lowtech|theirLowtech|land:desert)\]")), $"{name}: an age form in the words");
            // Every answer but a plain refusal plays out with an effect the play-out audit knows.
            int played = options.Count(o => (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"")));
            C(played >= 2, $"{name}: its answers play out on the map ({played} of 3)");
            foreach (var e in d.Descendants("li").Where(x => x.Attribute("Class") != null))
                C(allSource.Contains("class " + Cls(e) + " ") || allSource.Contains("class " + Cls(e) + "\n") || allSource.Contains("class " + Cls(e) + ":"), $"{name}: the effect {Cls(e)} exists");
            string words = string.Join(" ", texts.Concat(options.SelectMany(o => new[] { o.Element("label")?.Value ?? "", o.Element("outcome")?.Value ?? "" }.Concat(Lis(o, "outcomes")))));
            C(!Regex.IsMatch(words, @"\bin (\d+|a|two|three|four|five) (days?|hours?)\b") && !Regex.IsMatch(words, @"\d+ (days?|hours?)\b"), $"{name}: no countdown in the words");
            string bare = Regex.Replace(words, @"\[[^\]]*\]", "");
            C(!bare.Contains('—') && !bare.Contains('!'), $"{name}: no em dashes or exclamations");
            C(!gods.Any(g => Regex.IsMatch(words, @"\b" + g + @"\b")), $"{name}: no god named but by the colony's own name for it ({{GOD}})");
            C(d.Element("label")?.Value is string label && !Regex.IsMatch(label, @"[\[\]{}]"), $"{name}: a plain label");
        }

        // The moods they give exist.
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(t => t.Element("defName")?.Value ?? ""));
        foreach (var t in council.Descendants("thought").Select(x => x.Value).Distinct())
            C(thoughts.Contains(t), $"the mood {t} exists");
        C(council.Elements("ThoughtDef").All(t => t.Attribute("ParentName")?.Value == "Council_MemoryBase"), "its moods are council memories");

        // Their frames: a custom of the calendar, the seasons, the world's season of songs, a friend among the visitors.
        var picking = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_PickingDay");
        C(picking.Element("traditionOption")?.Value == "0" && !string.IsNullOrEmpty(picking.Element("traditionLabel")?.Value) && Lis(picking, "seasons").Contains("Summer")
          && Lis(picking, "texts").Any(t => t.StartsWith("[again]")) && Lis(picking, "texts").Any(t => t.StartsWith("[tradition]")), "the picking day is a summer custom of the calendar");
        C(dlm.Contains("class DilemmaWorker_R3PickingDay : DilemmaWorker_Custom") && dlm.Contains("YearFour.Forage(map, Round3CouncilMath.PickingFrom)"), "asked only while wild food is ripe within reach");
        C(picking.Descendants("li").Where(e => Cls(e) == "DilemmaEffect_Forage").Select(e => e.Element("count")?.Value).SequenceEqual(new[] { Round3CouncilMath.PickingAll.ToString(), Round3CouncilMath.PickingMorning.ToString() }),
            "the picking day marks as the rules say");
        var gap = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_HungryGap");
        C(Lis(gap, "seasons").Contains("Winter") && Lis(gap, "seasons").Contains("Spring") && !Lis(gap, "seasons").Contains("Summer"), "the hungry gap in winter and spring");
        C(dlm.Contains("AdvisorWorker_Food.DaysOfFood(map)") && dlm.Contains("PlayRounds.MealIn(map, null)"), "the hungry gap by the advisor's own count, with meals in the stores");
        var verse = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_OurVerse");
        string songsDefs = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").Select(File.ReadAllText));
        C(Lis(verse, "situations").SequenceEqual(new[] { "Council_WS_Songs" }) && songsDefs.Contains("<defName>Council_WS_Songs</defName>"), "a verse of our own comes with the season of songs");
        var friend = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_VisitorFriend");
        C(friend.Element("stranger")?.Element("kind")?.Value == "Visitor" && friend.Element("stranger")?.Element("minOpinionOfA")?.Value == Round3CouncilMath.FriendFrom.ToString()
          && friend.Element("stranger")?.Element("minOpinionFromA")?.Value == Round3CouncilMath.FriendFrom.ToString(), "a friend among the visitors: each thinks well of the other");
        var forgetting = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_Forgetting");
        C(forgetting.Descendants("li").Any(e => Cls(e) == "Round3CouncilHandOver" && e.Element("days")?.Value == ((int)Round3CouncilMath.SeasonDays).ToString()), "the forgetting: the work handed over for a season");
        var crying = dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR3_CryingNight");
        C(crying.Descendants("li").Any(e => Cls(e) == "DilemmaEffect_HandOver" && e.Element("work")?.Value == "Childcare" && e.Element("days")?.Value == ((int)Round3CouncilMath.ChildcareDays).ToString()),
            "the crying in the night: the childcare handed over for a few days");
        C(dlm.Contains("ModsConfig.BiotechActive") && dlm.Contains("ModsConfig.IdeologyActive"), "the baby's question with Biotech, the faith's table with Ideology");
        C(dlm.Contains("Worship.On") && dlm.Contains("Worship.Scoffer(dead)") && dlm.Contains("DilemmaWorker_R3ScofferGrave : DilemmaWorker_Gods"), "the scoffer's grave with the gods' worship on, for one who never held a god");
        C(dlm.Contains("Reputations.On ? Reputations.Tracker : null") && dlm.Contains("listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer)"), "after the tantrum: the colony's deeds keep it, and there is damage to mend");

        // The effects are thin pieces over the game's own ways, and nothing is saved of their own.
        C(dlm.Contains("ownership.ClaimBedIfNonMedical(bedB)") && dlm.Contains("Put back as it was"), "beds traded by the game's own ownership, put back if it fails");
        C(dlm.Contains("assign.TryAssignPawn(body.InnerPawn)"), "an empty grave assigned to the animal, the game's own burying");
        C(dlm.Contains("r.ProducedThingDef == ThingDefOf.Kibble") && dlm.Contains("r.fixedIngredientFilter.Allows(meat)"), "kibble by any recipe that makes it from that meat, the game's or a mod's");
        C(dlm.Contains("FleshTypeDefOf.Insectoid"), "insect meat by its source, the game's or a mod's");
        C(dlm.Contains("class Round3CouncilFeastFirst : DilemmaEffect_Feast") && dlm.Contains("class Round3CouncilHandOver : DilemmaEffect_HandOver"), "the supper and the handing over through the engines that exist");
        C(!Regex.IsMatch(dlm, @"Scribe_Collections|Scribe_Values|Scribe_References|Sched\."), "no saved collection, no saved field and no schedule of its own");
        C(!Regex.IsMatch(dlm, @"\bHarm\.|Injure\(|TakeDamage\("), "nobody is hurt");
        C(Regex.Matches(dlm, @"// apart:").Count >= 1, "the beds' message says why it names two people apart");

        // ================= The text =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round3_Council.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (Match m in Regex.Matches(dlm, "\"(CC_R3C_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} is in the keyed words");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\bin \d+ (days?|hours?)\b") && !v.Contains('—') && !v.Contains('!')), "the messages plain, no countdowns");

        // ================= The defs read by name, from the player's active mods =================

        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length >= 4 && cols[3] == "yes") active.Add(cols[0] + "|" + cols[1]);
            }
        }
        foreach (var (type, name) in new[]
                 {
                     ("ThoughtDef", "SleepDisturbed"), ("ThoughtDef", "BondedAnimalDied"), ("ThoughtDef", "CryingBaby"), ("ThoughtDef", "MyCryingBaby"),
                     ("ThoughtDef", "AteMeat_Abhorrent"), ("ThoughtDef", "AteMeat_Horrible"), ("ThoughtDef", "AteMeat_Disapproved"), ("HediffDef", "Dementia"),
                     ("HediffDef", "Alzheimers"), ("WorkTypeDef", "Childcare"), ("RecipeDef", "Make_Kibble")
                 })
        {
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");
            C(dlm.Contains("\"" + name + "\"") || council.ToString().Contains(">" + name + "<"), $"{name} read by name, softly");
        }
        string food = File.ReadAllText(Path.Combine(root, "Defs", "Food", "Rimfolk_DietSimpleMeals.xml"));
        C(food.Contains("<defName>Rimfolk_CookMealSimple_Veg</defName>"), "the meal without meat is Rimfolk's own");
        string deeds = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "DeedDefs"), "*.xml").Select(File.ReadAllText));
        C(deeds.Contains("<defName>Rimfolk_Deed_Tantrum</defName>"), "the tantrum is one of the colony's deeds");
        return n;
    }
}
