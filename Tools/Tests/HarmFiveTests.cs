using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Harm, part 5 (Harm/HarmFiveMath.cs; docs/harm-review-proposals.md 1.2.5, 2.3.1, 2.4.2, 2.7.2, 3.2.2, 2.2.4, 3.2.3,
/// 4.9.3, 3.1.2, 4.2.2, 3.11.3, 3.12.1, 3.3.3, 4.9.2, and 4.4.2 outside the budget): each rate set against the review's,
/// each prevention easing it; and the mod's side: every hurt through the one service with its kind, hooked where its
/// system lives, named before the choice, seen on the map, messages only where the game gives one, said in one sentence.
/// </summary>
public static class HarmFiveTests
{
    private const int D = HarmFiveMath.DayTicks, H = HarmFiveMath.HourTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm five: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= Carriers =================

        C(Near(HarmFiveMath.CarrierChance(CarrierKind.SickWorker, false, false), 1f), "the sick work on: a lapsed petition brings one more case (1.2.5)");
        C(HarmFiveMath.SickPetition("Council_Decree_SickBay") && HarmFiveMath.SickPetition("Council_Decree_Quarantine") && !HarmFiveMath.SickPetition("Council_Decree_LongHours"),
            "only a petition to rest when ill or for a quarantine");
        C(Near(HarmFiveMath.SickWorkOnHours, 24f), "the sick one works on a day before it reaches whoever was nearest");
        C(Near(HarmFiveMath.CarrierChance(CarrierKind.Gate, false, false), 0.25f) && HarmFiveMath.CarrierChance(CarrierKind.Gate, true, false) == 0f
          && HarmFiveMath.CarrierChance(CarrierKind.Gate, false, true) == 0f, "the gate: one group in four from a plague's people, none with the gate shut or the quarantine (2.3.1)");
        C(Near(HarmFiveMath.CarrierChance(CarrierKind.Courier, false, false), 0.25f) && HarmFiveMath.CarrierChance(CarrierKind.Courier, false, true) == 0f,
            "a courier the same, the quarantine leaving the goods at the gate (2.7.2)");
        C(Near(HarmFiveMath.CarrierChance(CarrierKind.Camp, false, false), 0.2f) && Near(HarmFiveMath.CampFeverDays, 3f) && Near(HarmFiveMath.CampFluShare, 0.25f),
            "the camp's fire: one sickness in five passes on, a chest cold or the flu one time in four (2.4.2)");
        C(HarmFiveMath.CarrierOnsetTicks(CarrierKind.SickWorker, 0.5f) == D && HarmFiveMath.CarrierOnsetTicks(CarrierKind.Gate, 0f) == 2 * D
          && HarmFiveMath.CarrierOnsetTicks(CarrierKind.Courier, 1f) == 3 * D, "a cold shows a day after, a plague's two or three");
        C(HarmFiveMath.CarrierKeepDays(CarrierKind.Camp) >= 12f && HarmFiveMath.CarrierKeepDays(CarrierKind.Gate) <= 7f, "a camp's fever kept its whole stay, the rest a week");

        // ================= The patient (3.2.2), the bad batch (2.2.4) =================

        C(Near(HarmFiveMath.PatientCarrierChance, 1f / 20f) && HarmFiveMath.PatientTuberculosisShare > 0.5f, "one patient in 20 brings tuberculosis or cholera");
        C(HarmFiveMath.WardCatch(2, 1f) == 0f && Near(HarmFiveMath.WardCatch(3, 1f), 0.25f) && Near(HarmFiveMath.WardCatch(4, 0.2f), 0.05f),
            "a night in the same room passes it one time in four, less under the quarantine; a room of their own, never");
        C(Near(HarmFiveMath.BadBatchChance, 1f / 8f) && Near(HarmFiveMath.BadBatchSick, 1f / 3f), "one visit in eight a bad batch, a few of those dosed the worse for it");
        C(HarmFiveMath.BatchBad(true, 7) && !HarmFiveMath.BatchBad(true, 8) && !HarmFiveMath.BatchBad(false, 0), "a doctor of Medicine 8 looks it over and catches it");
        C(HarmFiveMath.DoseOnsetTicks(0f) == 10 * H && HarmFiveMath.DoseOnsetTicks(1f) == 16 * H, "queasy the next morning");
        C(HarmFiveMath.SoreArmHigh <= 2f, "a shot's bad batch is only a sore arm");

        // ================= Small things that grow (3.2.3), dirty water (4.9.3) =================

        C(Near(HarmFiveMath.SmallThingsChance, 1f / 20f) && Near(HarmFiveMath.WormDays, 3f), "worms left three days, one in 20 grows into gut worms");
        C(HarmFiveMath.AgeWindow(3 * D, 3f) && HarmFiveMath.AgeWindow(3 * D + H - 1, 3f) && !HarmFiveMath.AgeWindow(3 * D + H, 3f) && !HarmFiveMath.AgeWindow(3 * D - 1, 3f),
            "looked at in the one hour it turns that old, so nothing needs remembering");
        C(HarmFiveMath.BitesGrow(false, false, false) == 0f && Near(HarmFiveMath.BitesGrow(true, false, false), 0.05f) && Near(HarmFiveMath.BitesGrow(true, true, true), 0.0125f),
            "bites carry malaria only in a malaria country, sleeves and a bed indoors half each");
        C(HarmFiveMath.DirtyWater(true, true, true) == 0f && HarmFiveMath.DirtyWater(true, false, false) == 0f && HarmFiveMath.DirtyWater(false, true, false) == 0f
          && HarmFiveMath.DirtyWater(true, true, false) > 0f, "dirty water: a drink, untreated, and only while the mod's own illnesses are off");
        C(HarmFiveMath.DirtyWaterChance * 60f < 0.2f, "dirty water stays a small kind beside the rest");

        // ================= The cold =================

        C(HarmFiveMath.ColdOut(true, -6f, -6f, 0f) && !HarmFiveMath.ColdOut(false, -6f, -6f, 0f) && !HarmFiveMath.ColdOut(true, -4f, -6f, 0f) && !HarmFiveMath.ColdOut(true, -10f, 5f, -20f),
            "out in the cold: outdoors, a hard frost, colder than comfortable with what they wear");
        C(!HarmFiveMath.ColdRoll(0) && !HarmFiveMath.ColdRoll(1) && HarmFiveMath.ColdRoll(2) && !HarmFiveMath.ColdRoll(3) && HarmFiveMath.ColdRoll(4), "a roll every two hours in it");
        C(HarmFiveMath.GateCold(false, true) == 0f && Near(HarmFiveMath.GateCold(true, true), 0.05f), "the gate: only while waiting past the hour (3.1.2)");
        C(Near(HarmFiveMath.WatchCold(true, 1), 0.05f) && Near(HarmFiveMath.WatchCold(true, 2), 0.025f) && HarmFiveMath.WatchCold(false, 1) == 0f, "the watch: two on watch half it (4.2.2)");
        C(Near(HarmFiveMath.WatchPredator(1, true, true), 1f / 6f) && HarmFiveMath.WatchPredator(2, true, true) == 0f && HarmFiveMath.WatchPredator(1, false, true) == 0f
          && HarmFiveMath.WatchPredator(1, true, false) == 0f, "a predator only for a lone watcher outside the walls, with one hungry in reach");
        C(HarmFiveMath.WeatherLover(true, 1, false) == 0f && Near(HarmFiveMath.WeatherLover(true, 2, false), 1f / 150f) && Near(HarmFiveMath.WeatherLover(false, 3, true), 0.5f / 75f)
          && HarmFiveMath.WeatherLover(false, 2, false) == 0f, "weather lovers: hours out in it, a coat or a hood halves it (3.11.3)");
        C(Near(HarmFiveMath.SnowLoverChance * 3f, 0.04f, 0.001f), "the snow lover's frostnip a third more than the frostnip's own 0.04");
        C(HarmFiveMath.ColdWater(1f) == 0f && Near(HarmFiveMath.ColdWater(-2f), 1f / 200f) && Near(HarmFiveMath.ColdWater(-6f), 2f / 200f), "cold water: one hour in 200 below freezing, twice in a hard frost (4.9.2)");
        C(HarmFiveMath.SoakHigh <= 0.2f, "a soaking's hypothermia only to the shivering");

        // ================= Night walks =================

        C(Near(HarmFiveMath.SleepwalkChance, 1f / 15f) && HarmFiveMath.SmallHours(23) && HarmFiveMath.SmallHours(2) && !HarmFiveMath.SmallHours(4) && !HarmFiveMath.SmallHours(20),
            "one nightmare in 15 walks them in the small hours (3.12.1)");
        C(HarmFiveMath.SleepwalkEnds(-3f, true, 0.9f) == 2 && HarmFiveMath.SleepwalkEnds(5f, false, 0.2f) == 1 && HarmFiveMath.SleepwalkEnds(5f, false, 0.8f) == 0
          && HarmFiveMath.SleepwalkEnds(-3f, false, 0.8f) == 0, "frostnip in the cold, a stumble half the time otherwise");
        C(HarmFiveMath.StumbleHigh <= 3f, "a stumble is the mildest bruise");
        C(HarmFiveMath.RestlessAtHeight(true, 8f, 5f) && !HarmFiveMath.RestlessAtHeight(true, 7f, 5f) && !HarmFiveMath.RestlessAtHeight(false, 20f, 5f),
            "restlessness at its height: their strongest feeling, well past its threshold (3.3.3)");
        C(HarmFiveMath.Dusk(18) && !HarmFiveMath.Dusk(12) && Near(HarmFiveMath.RestlessWalkChance * 15f, 1f), "at dusk, about once a season");
        C(Near(HarmFiveMath.RestlessPredator(true, true, true), 0.25f) && HarmFiveMath.RestlessPredator(false, true, true) == 0f && HarmFiveMath.RestlessPredator(true, false, true) == 0f,
            "a friend along or walls round the ground keep the predators off");

        // ================= The forecast (4.4.2) =================

        C(HarmFiveMath.ForecastMissed(2f, -3f, -1f, 2f) && !HarmFiveMath.ForecastMissed(0f, -3f, -1f, 2f) && !HarmFiveMath.ForecastMissed(2f, 0f, -1f, 2f),
            "the forecast missed only when it had the hour safely above the plants' limit and the frost came under it");

        // ================= The registry =================

        var five = new[] { "1.2.5", "2.3.1", "2.4.2", "2.7.2", "3.2.2", "2.2.4", "3.2.3", "4.9.3", "3.1.2", "4.2.2", "3.11.3", "3.12.1", "3.3.3", "4.9.2" };
        foreach (var id in five)
            C(HarmKinds.All.Count(k => !k.founding && k.label.Contains("(" + id + ")")) == 1, $"the proposal {id} is a kind of its own");
        C(!HarmKinds.All.Any(k => k.label.Contains("(4.4.2)")), "the forecast hurts nobody and stays outside the budget");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var mine = new Dictionary<string, string>
        {
            ["HarmFive"] = Code("Harm/HarmFive.cs"), ["Carriers"] = Code("Harm/Carriers.cs"), ["Untended"] = Code("Harm/Untended.cs"), ["ColdHurts"] = Code("Harm/ColdHurts.cs"),
            ["NightWalks"] = Code("Harm/NightWalks.cs")
        };
        foreach (var kv in mine)
        {
            C(!Regex.IsMatch(kv.Value, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|MakeHediff\("), $"{kv.Key}: nobody is hurt but through the harm service");
            C(!Regex.IsMatch(kv.Value, @"Sched\.(Rare|Hourly|Every)"), $"{kv.Key}: no clock of its own (the harm book's hour and slices)");
            C(!kv.Value.Contains("—") && !kv.Value.Contains("Tell.Letter(") && !kv.Value.Contains("LetterStack"), $"{kv.Key}: no em dashes, no letters");
        }
        string carriers = mine["Carriers"], untended = mine["Untended"], cold = mine["ColdHurts"], walks = mine["NightWalks"], harm = Code("Harm/Harm.cs");
        C(harm.Contains("HarmFive.Hourly(this, now);") && harm.Contains("HarmFive.Slice(this, slice, now);") && harm.Contains("HarmFive.Reset();"), "on the harm book's hour and its slices");
        foreach (var system in new[] { "sickWorkOn", "gateSickness", "campFever", "courierSick", "patientIll", "badBatch", "smallThings", "dirtyWater", "gateCold", "nightWatch", "weatherLovers", "sleepwalk", "restlessWalk", "coldWater" })
            C(harm.Contains("case \"" + system + "\":"), $"{system} sits under its own system's switch");
        C(Regex.IsMatch(harm, @"case ""gateCold"": return [^;]*Interplay\.On;") && Regex.IsMatch(harm, @"case ""sickWorkOn"": return [^;]*Interplay\.On;"),
            "links between two systems behind Systems talk to each other");
        // Hooked where each system lives, one line each.
        C(Code("Decrees/Decrees.cs").Contains("Carriers.PetitionLapsed(r.def, r.pawn);") && Code("UI/DecreePanel.cs").Contains("Carriers.LapseLine(def)"),
            "the sick work on: from the petition's lapse, named under the petition on the decree page");
        C(carriers.Contains("lord.LordJob is LordJob_VisitColony") && carriers.Contains("LordJob_TradeWithColony") && carriers.Contains("w.Has(\"quarantine\")")
          && carriers.Contains("Council_Decree_Quarantine"), "the gate: visitors' and traders' groups, the shut gate and the quarantine decree");
        C(Code("Couriers/CourierTracker.cs").Contains("Carriers.CourierCame(f, people, map);"), "a courier's party as it comes onto the map");
        C(Code("Camps/CampStory.cs").Contains("Carriers.CampFell(who);") && Code("Camps/CampStory.cs").Contains("Carriers.DescribeCampTend(sheet, sick);") && carriers.Contains("flu.IsTended()"),
            "the camp's fever from its sick one, stopped once the doctor has seen to them, named under the ask");
        string hospital = Code("Health/HospitalPatient.cs");
        C(hospital.Contains("Carriers.PatientWorse(ctx.stranger)") && hospital.Contains("Carriers.WorseTale(worse)") && carriers.Contains("room.ContainedBeds"),
            "the patient: given in the event, named in its letter, passed to whoever sleeps in the same room");
        C(Code("Happenings/Workers_More.cs").Contains("Carriers.Batch(ctx.stranger, ctx.a,") && Code("Happenings/VisitorServices.cs").Contains("Carriers.Dosed(by, patient);")
          && Code("Happenings/Workers_More.cs").Contains("Carriers.Dosed(ctx.stranger, p);"), "the bad batch: from the paid doses, on the map or all at once");
        C(untended.Contains("\"GutWorms\"") && untended.Contains("\"Malaria\"") && untended.Contains("CommonalityOfDisease") && untended.Contains("IsTended()"),
            "small things: the game's own gut worms and malaria, a malaria country, untended only");
        C(untended.Contains("\"DBHThirst\"") && untended.Contains("DubsBadHygiene") && untended.Contains("return !found || on;"),
            "dirty water: Dubs Bad Hygiene's thirst by name, its own illnesses counted as on unless they read off");
        C(cold.Contains("JourneyDefOf.Rimfolk_JourneyWait") && Code("Journeys/JourneyRun.cs").Contains("ColdHurts.GateLetterLine(plan)"), "the gate: the wait at the gate past the hour, named in the letter");
        C(cold.Contains("PlayKind.Vigil") && cold.Contains("play.patrol") && Code("PlayOut/PlayOutGroupEffects.cs").Contains("ColdHurts.DescribeWatch(sheet, ctx?.map);"),
            "the watch: the night watch on the walls, named under its answer");
        C(cold.Contains("Council_Quirk_RainCleanses") && cold.Contains("Council_Quirk_SnowLover") && cold.Contains("ApparelLayerDefOf.Shell"), "weather lovers: by the quirks, a coat or a hood");
        C(cold.Contains("ModsConfig.OdysseyActive") && cold.Contains("VanillaExpanded.VCEF") && cold.Contains("\"Hypothermia\""), "cold water: the game's or the mod's fishing, the game's own hypothermia");
        C(walks.Contains("TimedKind.Bolt") && walks.Contains("Council_Dream_Shaken") && walks.Contains("room.ContainedBeds"), "the sleepwalk: after a nightmare, a bolted door and a roommate");
        C(walks.Contains("Council_Feeling_Restless") && walks.Contains("PlayVigils.Route(") && walks.Contains("OpinionOf(o) ?? 0) >= 20"), "the restless walk: the feeling at its height, a friend along");
        C(walks.Contains("JobDefOf.PredatorHunt") && walks.Contains("killIncappedTarget = false") && walks.Contains("FoodUtility.IsAcceptablePreyFor") && walks.Contains("TraverseMode.NoPassClosedDoors"),
            "a predator by the game's own hunt, never killing the downed, only outside the walls");
        string fields = Code("Fields/FieldWeather.cs");
        C(fields.Contains("HarmFiveMath.ForecastMissed(") && fields.Contains("CC_HarmFive_ForecastMissed") && !fields.Contains("HarmKinds."), "the forecast that missed: a field's note, outside the budget");
        // Messages only where the game gives one for the same hurt: an illness caught.
        C(carriers.Contains("HarmFour.TellHurt(") && untended.Contains("HarmFour.TellHurt(") && cold.Contains("HarmFour.TellHurt(") && walks.Contains("CC_HarmFive_SleepwalkFrost"),
            "an illness caught is told");
        C(!Regex.IsMatch(cold, @"TellHurt\([^;]*Splash") && Regex.Matches(walks, "TellHurt\\(").Count == 1, "a slip into the water, a stumble, a predator: no message of Rimfolk's own");
        string dev = Code("Harm/HarmFiveDebug.cs");
        C(!Regex.IsMatch(dev, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|Harm\.(Note|Injure|Ail|Fight)\(") && Regex.Matches(dev, @"\[DebugAction\(""Rimfolk"", ""Harm five: ").Count == 16,
            "sixteen dev tools (one or more for each, the forecast's note), each through the feature itself, never past the cap");

        // Named before the choice, in the defs.
        string plague = File.ReadAllText(Path.Combine(root, "Defs", "WorldSituationDefs", "Council_WS_Hardship.xml"));
        C(plague.Contains("<risk>gateSickness</risk>") && plague.Contains("<incident>Disease_Flu</incident><factor>1.5</factor><withCarriers>1.35</withCarriers>")
          && plague.Contains("<incident>Disease_Plague</incident><factor>2.5</factor><withCarriers>2.3</withCarriers>"),
            "the plague's open gate names the risk, and its raised flu and plague give up the carriers' share only while the carriers are on");
        C(Code("World/WorldSituationWorker.cs").Contains("f *= m.Factor;") && Code("World/WorldSituationDef.cs").Contains("withCarriers > 0f && Carriers.GateOn"),
            "the factor while the carriers are on, the old one without them");
        C(File.ReadAllText(Path.Combine(root, "Defs", "IncidentDefs", "Council_Events_More.xml")).Contains("<risk>badBatch</risk>"), "the healer's paid doses name the bad batch");
        C(Regex.Matches(File.ReadAllText(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_Hospital.xml")), "<risk>patientIll</risk>").Count == 3, "every answer to a patient names what they carry");
        var jobs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmFive.xml"));
        C(jobs.Contains("<defName>Rimfolk_Sleepwalk</defName>") && jobs.Contains("<defName>Rimfolk_RestlessWalk</defName>") && jobs.Contains("ColonyCouncil.JobDriver_HarmWalk"), "the two walks' jobs");

        // The words.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HarmFive.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_HarmFive_SickWorkOn", "CC_HarmFive_GateSick", "CC_HarmFive_CourierSick", "CC_HarmFive_CampFever", "CC_HarmFive_PatientIll", "CC_HarmFive_BadBatch",
                     "CC_HarmFive_GutWorms", "CC_HarmFive_Malaria", "CC_HarmFive_DirtyWater", "CC_HarmFive_GateCold", "CC_HarmFive_WatchCold", "CC_HarmFive_RainLover",
                     "CC_HarmFive_SnowLover", "CC_HarmFive_SleepwalkFrost" })
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2") && keyed[key].Contains("{PAWN_") && keyed[key + "_2"].Contains("{PAWN_"), $"{key} has two wordings, naming who was hurt");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Impact_")).All(kv => !Regex.IsMatch(Regex.Replace(kv.Value, @"\{\d+\}", ""), @"\d")), "the sheets' lines are words, no numbers");
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var kv in mine.Concat(new[] { new KeyValuePair<string, string>("FieldWeather", fields) }))
            foreach (Match m in Regex.Matches(kv.Value, "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{kv.Key}: {k} exists");
            }

        // Said over a head: one short sentence each, and every pool asked for with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        string linesFile = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_HarmFive_Lines.xml"));
        var asked = mine.Values.SelectMany(v => Regex.Matches(v, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value)).Distinct()
            .Where(p => linesFile.Contains("<defName>" + p + "</defName>")).ToList();
        C(asked.Count >= 22, $"the new pools are asked for ({asked.Count})");
        foreach (Match m in Regex.Matches(linesFile, "<defName>(Rimfolk_Pool_[A-Za-z]+)</defName>"))
            C(asked.Contains(m.Groups[1].Value), $"{m.Groups[1].Value} is said somewhere");
        foreach (var pool in asked)
        {
            C(pools.TryGetValue(pool, out var lines) && lines.Count >= 6, $"{pool} exists with six lines or more");
            if (lines == null) continue;
            foreach (var line in lines)
            {
                string body = Regex.Replace(line, @"^\s*\[[^\]]*\]\s*", "").Trim();
                int cut = -1;
                for (int i = 12; i < body.Length; i++)
                    if (body[i] == '.' || body[i] == '!' || body[i] == '?')
                    {
                        cut = i;
                        break;
                    }
                C(cut < 0 || cut == body.Length - 1, $"{pool}: '{body}' is one sentence");
                C(body.Length <= 90 && !body.Contains("!") && !body.Contains("—"), $"{pool}: '{body}' is short, with no exclamation mark or em dash");
            }
        }

        string review = File.ReadAllText(Path.Combine(root, "docs", "harm-review-proposals.md"));
        C(Regex.Matches(review, @"\*Built \(harm, part 5").Count == 15, "the review marks the fourteen and the forecast built");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### Harm, part 5"), "DESIGN.md has them");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
