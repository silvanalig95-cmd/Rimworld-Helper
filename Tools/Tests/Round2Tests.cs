using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content update's second round, storyteller events and council questions (docs/content-update-proposals-2.md, D16 to
/// D32; the player's go of 27 November 2026: content inside the mechanics there are). The rules without game types
/// (Happenings/Round2Math.cs), and the defs and code: each event a Rimfolk event with its worker and its answers playing out
/// on the map, its age forms, its hurts through the shared budget and named before the answer, what it keeps on the map one
/// of OnMapTimed's, the walled den and the late feast the storyteller's things on their way (no new saved collection), the
/// gods unnamed on the cards, no countdowns, the mods' defs by name from the player's active mods.
/// </summary>
public static class Round2Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 2: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();

        // ================= The rules =================

        C(Round2Math.AtDusk(17) && Round2Math.AtDusk(21) && !Round2Math.AtDusk(16) && !Round2Math.AtDusk(22), "dusk is the evening's hours");
        C(Round2Math.StillNight(23) && Round2Math.StillNight(3) && !Round2Math.StillNight(Round2Math.Dawn) && !Round2Math.StillNight(12), "a night out lasts until first light");
        C(Math.Abs(Round2Math.ColdChance - 0.05f) < 0.0001f && Math.Abs(Round2Math.ColdEveryHours - 2f) < 0.0001f && Round2Math.ColdDue(2f) && !Round2Math.ColdDue(1.9f),
            "the night's cold at the cold's own rate: one chance in twenty every two hours out in a frost");
        C(Round2Math.DenOpens(true, Round2Math.DenWarm) && !Round2Math.DenOpens(false, 20f) && !Round2Math.DenOpens(true, Round2Math.DenWarm - 1f), "the den opens in spring's first warm days only");
        C(Round2Math.DenFirstLookDays >= 5f && Round2Math.DenLookAgainDays <= 1f, "the den is looked at after a while, then daily until it's warm");
        C(Round2Math.DayGrowth(10f, 1f) > 0.09f && Round2Math.DayGrowth(10f, 1f) < 0.11f && Round2Math.DayGrowth(0f, 1f) == 0f && Round2Math.DayGrowth(0.5f, 1f) == 1f,
            "a watered field grows a day's growth at its full rate, never past grown");
        C(Round2Math.LetGoGrowthDays < Round2Math.WaterGrowthDays, "the fields that share the water get less than one watered by hand");
        C(Round2Math.GasFor(1f) == Round2Math.GasPerCell && Round2Math.GasFor(0f) > 0 && Round2Math.GasFor(0f) < Round2Math.GasPerCell && Round2Math.GasFor(5f) <= Round2Math.GasPerCell * 1.5f + 1,
            "the bad air follows the harm budget's scale, a little at least so the sign is there");
        C(Round2Math.Truthful(0f, 3f, false) == 0 && Round2Math.Truthful(4f, 1f, true) == 1 && Round2Math.Truthful(2f, 2f, true) == 0 && Round2Math.Truthful(2f, 2f, false) == 1,
            "the one less burned is taken for the truthful, a toss when alike");
        C(Round2Math.OrdealBurnHigh <= 9f && Round2Math.OrdealBurnChance <= 0.5f, "the ordeal's burn is a small one, before the budget's scale");
        C(Round2Math.MadChance < 1f && Round2Math.SanguineChance <= 0.25f, "the mad animal comes most times it is let be, the sanguine deer one in four");
        C(Round2Math.RaidFits(1f) && !Round2Math.RaidFits(0.2f) && !Round2Math.RaidFits(3f), "the one who hid is found a day or so after the raid");
        C(Round2Math.NewAgeCooldownDays >= 600f, "the new age is asked once for each age");

        // ================= The events =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Round2_Events.xml")).Root!;
        var council = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Round2_Council.xml")).Root!;
        string workers = Code("Happenings/Workers_Round2.cs");
        string dlm = Code("Dilemmas/DilemmaWorkers_Round2.cs");
        string timed = Code("PlayOut/OnMapTimed.cs");
        string story = Code("Happenings/Workers_Storyteller.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));

        var evNames = new[] { "NotInByDark", "FishRunning", "BearDen", "WaterForFields", "WrongAnimal", "OneWhoHid", "StuckInBog", "BadAir" };
        foreach (var name in evNames)
        {
            var d = events.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_" + name);
            C(d != null, $"{name}: a Rimfolk event");
            if (d == null) continue;
            C(d.Attribute("ParentName")?.Value == "Council_EventBase", $"{name}: in the Rimfolk events' pool");
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(worker == "HappeningWorker_" + name && workers.Contains("class " + worker + " "), $"{name}: its worker in Workers_Round2.cs");
            var ext = d.Element("modExtensions")?.Elements("li").FirstOrDefault(e => Cls(e) == "HappeningExtension");
            C(ext != null, $"{name}: a happening");
            if (ext == null) continue;
            var options = ext.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            // Every answer but a plain refusal plays out with an effect the play-out audit knows.
            int played = options.Count(o => (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"")));
            C(played >= 2, $"{name}: its answers play out on the map ({played} of 3)");
            var texts = Lis(ext, "texts");
            C(texts.Count >= 3, $"{name}: three wordings or more");
            C(texts.Any(t => t.Contains("[lowtech]") || t.Contains("[!lowtech]")) || name == "WaterForFields" && texts.Any(t => t.Contains("[lowtech]")) || options.Any(o => Lis(o, "outcomes").Any(t => Regex.IsMatch(t, @"\[(tribal|medieval|lowtech|industrial|spacer)"))),
                $"{name}: its age forms in the words");
            // The gods are unseen: an event card never names one.
            var gods = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
            string words = string.Join(" ", texts.Concat(Lis(ext, "titles")).Concat(options.SelectMany(o => new[] { o.Element("label")?.Value ?? "", o.Element("outcome")?.Value ?? "" }.Concat(Lis(o, "outcomes")))));
            C(!gods.Any(g => Regex.IsMatch(words, @"\b" + g + @"\b")), $"{name}: no god named on the card");
            C(!Regex.IsMatch(words, @"\bin (\d+|a|two|three|four|five) (days?|hours?)\b") && !Regex.IsMatch(words, @"\d+ (days?|hours?)\b"), $"{name}: no countdown in the words");
            string bare = Regex.Replace(words, @"\[[^\]]*\]", "");
            C(!bare.Contains('—') && !bare.Contains('!'), $"{name}: plain words, no em dashes or exclamations on the card");
        }

        // What each keeps on the map is one of OnMapTimed's, dispatched both ways.
        foreach (var kind in new[] { "NightOut", "FishRun", "BearAsleep", "WaterChain", "WrongBeast", "Hidden", "Bog", "BadAir", "Ordeal" })
        {
            C(Regex.IsMatch(timed, @"\b" + kind + @",?\s") && Regex.Matches(timed, @"case TimedKind\." + kind + ":").Count == 2, $"{kind}: kept on the map by OnMapTimed, its step and its end");
            C(workers.Contains("case TimedKind." + kind + ":"), $"{kind}: its step in Round2Map");
        }
        // The walled den and the late feast wait as the storyteller's things on their way: no new saved collection.
        C(story.Contains("Round2Map.Den, Round2Map.LateFeast") && story.Contains("case Round2Map.Den: HappeningWorker_BearDen.Spring(m)") && story.Contains("case Round2Map.LateFeast: DilemmaEffect_LateFeast.Held(m)"),
            "the walled den and the late feast are things on their way, come due in the storyteller's hour");
        C(!Regex.IsMatch(workers + dlm, @"Scribe_Collections"), "no saved collection of its own");
        C(Code("Social/FamilyInheritance.cs").Contains("Round2Council.RoomLeft(dead, bed);"), "a room left at a death is kept for the council's question as a memo");

        // Every hurt through the shared budget, named under its answer.
        C(workers.Contains("Harm.Roll(p, HarmKinds.ColdNight, Round2Math.ColdChance, HurtClass.Mild)") && workers.Contains("Harm.Ail(p, def"), "the night's cold through the budget (the cold's own ailment)");
        C(workers.Contains("Harm.Roll(HarmKinds.TooClose, Round2Math.MadChance)") && workers.Contains("MentalStateDefOf.Manhunter"), "the mad animal through the budget, the game's own mad animal");
        C(workers.Contains("Harm.ScaleFor(HarmKinds.Dangers)") && workers.Contains("GasType.ToxGas") && workers.Contains("ModsConfig.BiotechActive"), "the bad air the game's tox gas, by the budget's scale, with Biotech");
        C(dlm.Contains("Harm.Roll(b, HarmKinds.GodBurn") && dlm.Contains("Harm.Injure(b, HurtKind.Burn, HurtWhere.Hand"), "the ordeal's burn through the budget (GodBurn), only in the older ages");
        C(dlm.Contains("on = Avail.IsPreIndustrial"), "the later ages swear an oath, no burn");
        C(workers.Contains("\"CC_R2_Impact_LostCold\"") && workers.Contains("\"CC_R2_Impact_Mad_\"") && workers.Contains("\"CC_R2_Impact_AirRisk\"") && dlm.Contains("\"CC_R2_Impact_OrdealBurn\"") && workers.Contains("\"CC_R2_Impact_HidFight\""),
            "each hurt named under its answer before it's chosen");

        // ================= The council questions =================

        var dilemmas = council.Elements("ColonyCouncil.DilemmaDef").Where(d => d.Attribute("Abstract")?.Value != "True").ToList();
        C(dilemmas.Count == 11, $"eleven council questions ({dilemmas.Count}): nine items, the new age once for each later age");
        foreach (var d in dilemmas)
        {
            string name = d.Element("defName")?.Value ?? "?";
            string worker = (d.Element("workerClass")?.Value ?? (d.Attribute("ParentName")?.Value == "Rimfolk_Dlm_NewAgeBase" ? "DilemmaWorker_NewAge" : "")).Replace("ColonyCouncil.", "");
            C(dlm.Contains("class " + worker + " "), $"{name}: its worker in DilemmaWorkers_Round2.cs");
            var options = d.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            C(Lis(d, "texts").Count >= 2, $"{name}: two wordings or more");
            string words = string.Join(" ", Lis(d, "texts").Concat(options.SelectMany(o => new[] { o.Element("outcome")?.Value ?? "" })));
            C(!Regex.IsMatch(words, @"\bin (\d+|a|two|three|four|five) (days?|hours?)\b"), $"{name}: no countdown in the words");
        }
        C(dilemmas.Count(d => (d.Element("defName")?.Value ?? "").StartsWith("Rimfolk_Dlm_NewAge_")) == 3 && dlm.Contains("Avail.ResearchedTech == Age && Avail.PlayerTech < Age"),
            "the new age: one for each later age, asked when the colony has researched into it");
        C(dlm.Contains("cal.AddNamed(label, DayWay.Feast, GenLocalDate.DayOfYear(ctx.map))") && council.Descendants("li").Count(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_NewAgeDay") == 3
          && !council.Descendants("traditionOption").Any(), "marking the new age puts a day on the colony's calendar, kept each year, and never asks the question again");
        C(dlm.Contains("case \"Mog\": return \"whole\"") && dlm.Contains("case \"Morel\": return \"soil\"") && dlm.Contains("case \"Helios\": return \"fire\""), "each god's way with the dead");
        C(dlm.Contains("AnswerShifts.Instance.HandOver(ctx.map, ctx.a, ctx.b, w, Round2Math.ExcusedDays"), "excused for a season: the work handed over and put back after");
        C(dlm.Contains("GodLore.Believes(asker, named ? g : null, words)"), "the ordeal kept in the book as the asker's belief, unnamed where the colony has no name");
        C(dlm.Contains("PawnGods.Festival("), "the late feast kept as the believers' festival");

        // ================= The text =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round2_Events.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (Match m in Regex.Matches(workers + dlm, "\"(CC_R2_[A-Za-z_]+)\""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_")) continue;
            C(keyed.ContainsKey(key), $"{key} is in the keyed words");
        }
        foreach (var prefix in new[] { "CC_R2_Impact_Mad_", "CC_R2_Impact_Hid_", "CC_R2_Impact_Air_", "CC_R2_Impact_Room_", "CC_R2_Impact_Lay_", "CC_R2_Impact_Camp_", "CC_R2_Camp_Carried_", "CC_R2_Impact_ByAge_" })
            C(keyed.Keys.Count(k => k.StartsWith(prefix)) >= 2, $"{prefix}*: each mode's words");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\bin \d+ (days?|hours?)\b") && !v.Contains('—') && !v.Contains('!')), "the messages plain, no countdowns");

        // ================= The mods, by name, from the player's active mods =================

        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string? line;
            while ((line = r.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length >= 4 && cols[3] == "yes") active.Add(cols[0] + "|" + cols[1]);
            }
        }
        foreach (var (type, name) in new[] { ("ThingDef", "Fish_Salmon"), ("ThingDef", "Fish_Cod"), ("ThingDef", "VCEF_RawTrout"), ("ThingDef", "VCEF_RawHerring"), ("PawnKindDef", "Bear_Grizzly"),
                     ("PawnKindDef", "Bear_Polar"), ("PawnKindDef", "AEXP_BlackBear"), ("PawnKindDef", "pphhyy_SanguineDeer"), ("GameConditionDef", "Drought"), ("GameConditionDef", "VEE_Drought"),
                     ("RecipeDef", "Make_Pemmican"), ("NeedDef", "Hygiene"), ("ThingDef", "Sarcophagus") })
        {
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");
            C((workers + dlm).Contains("\"" + name + "\""), $"{name} used by name, softly");
        }
        return n;
    }
}
