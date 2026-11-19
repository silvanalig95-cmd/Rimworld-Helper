using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 10, the last (Peoples/Events10/; docs/peoples-events-proposals.md, items 96 to 100): their pure rules (the
/// hearing, the rulings and the season's prices, the bargain by the talker's Social, the toll and the Wheel's cart train, the days at a
/// plague town and the sickness brought home, the three days at the colony's table and its book, each calling's art: its class book, its
/// class and its skill), their defs and words, the player's mods they draw on, and the seams with the game and rounds 1 to 9.
/// </summary>
public static class PeopleEvents10Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 10: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        const int H = PeopleEvents10Math.HourTicks;

        // ================= 96. The moot over the old wood =================

        C(PeopleEvents10Math.HearTicks == 2 * H && PeopleEvents10Math.HearWaitHours >= 4 && PeopleEvents10Math.Rulings.SequenceEqual(new[] { "shaft", "wood", "bargain" }),
            "both envoys heard two hours at the colony's table, then a ruling for the shaft, for the wood, or a bargain");
        C(PeopleEvents10Math.MootGain == -PeopleEvents10Math.MootLoss && PeopleEvents10Math.MootGain > 0 && PeopleEvents10Math.BargainGain > 0 && PeopleEvents10Math.BargainLoss < 0
          && PeopleEvents10Math.BargainGain < PeopleEvents10Math.MootGain, "the side ruled for gains what the other loses; a bargain less either way");
        C(Math.Abs(PeopleEvents10Math.BargainChance(0) - 0.25f) < 0.001f && Math.Abs(PeopleEvents10Math.BargainChance(10) - 0.65f) < 0.001f
          && PeopleEvents10Math.BargainChance(20) == 0.85f && PeopleEvents10Math.BargainChance(-3) == 0.25f, "a bargain by the talker's Social, never certain");
        C(PeopleEvents10Math.Bargained(0.3f, 10) && !PeopleEvents10Math.Bargained(0.7f, 10) && !PeopleEvents10Math.Bargained(float.NaN, 20), "the roll against it");
        C(PeopleEvents10Math.PricedThing("shaft", false) == "Steel" && PeopleEvents10Math.PricedThing("wood", false) == "MedicineHerbal" && PeopleEvents10Math.PricedThing("bargain", true) == "Steel"
          && PeopleEvents10Math.PricedThing("bargain", false) == null, "the shaft makes steel cheaper, the wood the Mossborn's herbal medicine; a failed bargain nothing");
        C(PeopleEvents10Math.PriceFactor("shaft", false) == 0.8f && PeopleEvents10Math.PriceFactor("wood", false) == 0.8f && PeopleEvents10Math.PriceFactor("bargain", true) == 0.9f
          && PeopleEvents10Math.PriceFactor("bargain", false) == 1f && PeopleEvents10Math.SeasonDays == 15, "a fifth cheaper for a season (a tenth with the bargain)");
        C(Math.Abs(PeopleEvents10Math.Priced(100f, 0.8f) - 80f) < 0.01f && Math.Abs(PeopleEvents10Math.Priced(10f, 0.1f) - 5f) < 0.01f && float.IsNaN(PeopleEvents10Math.Priced(float.NaN, 0.8f))
          && PeopleEvents10Math.Priced(10f, float.NaN) == 10f, "a price moved within reason");
        float buy = 2.6f, sell = 1.2f;
        C(PeopleEvents10Math.Priced(buy, 0.8f) > PeopleEvents10Math.Priced(sell, 0.8f), "buying and selling move together, so buying never costs less than selling pays");
        C(PeopleEvents10Math.MaxPrices >= 2 && PeopleEvents10Math.HearSocialXp > 0f, "a few prices kept at once; the talker learns");

        // ================= 97. A toll on the road =================

        C(PeopleEvents10Math.TollGain == -PeopleEvents10Math.TollLoss && PeopleEvents10Math.FairGain > 0 && PeopleEvents10Math.FairGain < PeopleEvents10Math.TollGain,
            "the side taken gains what the other loses; a fair toll a little for both");
        C(PeopleEvents10Math.Toll(2) == 70 && PeopleEvents10Math.Toll(3) == 100 && PeopleEvents10Math.Toll(0) == 40 && PeopleEvents10Math.FairTicks == 2 * H,
            "a fair toll by the pack beasts, set in two hours at the gate");
        C(PeopleEvents10Math.WheelHandsMin >= 1 && PeopleEvents10Math.WheelHandsMax <= 2 && PeopleEvents10Math.WheelBeastsMin == 2 && PeopleEvents10Math.WheelBeastsMax == 3
          && PeopleEvents10Math.CartLoad > 0, "a wheelwright, a hand or two and two or three pack beasts, their packs loaded");

        // ================= 98. The lamp and the bell in a plague year =================

        C(PeopleEvents10Math.PlagueDays(0f) == 4 && PeopleEvents10Math.PlagueDays(0.99f) == 6 && PeopleEvents10Math.PlagueDays(float.NaN) == 4 && PeopleEvents10Math.PlagueTiles <= 20f,
            "four to six days at a plague town within reach");
        C(PeopleEvents10Math.DoctorXpPerDay > PeopleEvents10Math.DiggerXpPerDay && PeopleEvents10Math.DiggerXpPerDay > 0f && PeopleEvents10Math.Diggers == 2,
            "the doctor on the wards, two diggers at the graves, each learning");
        C(PeopleEvents10Math.DoctorGoodwill == PeopleEvents10Math.DiggerGoodwill * PeopleEvents10Math.Diggers && PeopleEvents10Math.PlagueDeed == "plagueYear",
            "the order thanks the colony for its doctor as the Bell for its two diggers; both sent, a deed of its own");
        float lodged = PeopleEvents10Math.LodgedHours(4f);
        C(lodged == 96f && PeopleEvents10Math.LodgedHours(float.NaN) == 0f, "their days lodged at the town, in hours");
        C(Math.Abs(HarmTwoMath.SickHome(SickTown.Plague, lodged, false, false, false) - 1f / 6f) < 0.001f
          && Math.Abs(HarmTwoMath.SickHome(SickTown.Plague, lodged, true, true, false) - 1f / 24f) < 0.001f,
            "the sickness comes home at the illness budget's own rate: one journey in six, a quarter with the sickhouse's care and modern medicine");

        // ================= 99. Learning traded through the colony =================

        C(PeopleEvents10Math.HostDays == 3 && PeopleEvents10Math.CopyTicks == 4 * H && PeopleEvents10Math.LessonTicks == 2 * H, "three days: four hours of copying each morning, two of lessons each afternoon");
        C(PeopleEvents10Math.CopyHour(8) && PeopleEvents10Math.CopyHour(11) && !PeopleEvents10Math.CopyHour(14) && PeopleEvents10Math.LessonHour(14) && !PeopleEvents10Math.LessonHour(9),
            "copying in the morning, lessons in the afternoon");
        C(PeopleEvents10Math.CopyWhole(3) && !PeopleEvents10Math.CopyWhole(2), "the copy whole after a morning for each day, and then the book");
        C(PeopleEvents10Math.LearnGoodwill > PeopleEvents10Math.CopyGoodwill && PeopleEvents10Math.CopyGoodwill > 0 && PeopleEvents10Math.CraftWorth > 0f && PeopleEvents10Math.LessonSocialXp > 0f,
            "more thanks with the lessons than for the copying only; the vault's craft as supply");

        // ================= 100. Their art handed on =================

        C(PeopleEvents10Math.ArtGoodwill >= 40f && PeopleEvents10Math.ArtTicks == 3 * H && PeopleEvents10Math.GreatLessonXp >= 4000f, "at good terms; an afternoon at the gate");
        C(PeopleEvents10Math.PassionAfter(0) == 1 && PeopleEvents10Math.PassionAfter(1) == 2 && PeopleEvents10Math.PassionAfter(2) == -1 && PeopleEvents10Math.PassionAfter(5) == -1,
            "none an interest, an interest a burning passion, a mod's own left alone");
        C(PeopleEvents10Math.ArtKind(true, false, 0) == "tome" && PeopleEvents10Math.ArtKind(false, true, 0) == "passion" && PeopleEvents10Math.ArtKind(false, true, 2) == "lesson"
          && PeopleEvents10Math.ArtKind(false, false, 0) == null, "the book where it can be read, else a passion, else a long lesson; nothing for a skill closed to them");
        C(PeopleEvents10Math.ReturnGift(1f) == 250 && PeopleEvents10Math.ReturnGift(0.1f) == 150 && PeopleEvents10Math.ReturnGift(9f) == 500 && PeopleEvents10Math.ReturnGift(float.NaN) == 250,
            "the colony's gift in return by its wealth");
        var arts = PeopleEvents10Math.Arts;
        C(PeoplesMath.Callings.All(arts.ContainsKey) && arts.Count == PeoplesMath.Callings.Length, $"every people's calling has its art ({arts.Count})");
        C(arts["healers"].tome == "BookOfPriest" && arts["herders"].tome == "BookOfDruid" && arts["hunters"].tome == "BookOfRanger" && arts["sextons"].tome == "BookOfEarth"
          && arts["mercenaries"].tome == "BookOfGladiator" && arts["artisans"].tome == "BookOfEnchanter" && arts["mechanitors"].tome == "BookOfMagitech" && arts["listeners"].tome == "BookOfBard",
            "the proposal's own: the order the Priest's, the herders the Nature tome, the Wardens the Ranger's, the Bell the Earth tome, the Banner the Gladiator's, the Guild the Enchanter's, the Lattice Technomancy, the Choir the Bard's");
        C(arts.Values.All(a => PeoplesGearMath.MagicSetting(a.trait) != null && PeoplesGearMath.Might(a.trait) == a.fighter),
            "each class is one A RimWorld of Magic's settings switch, a fighter's book for a fighter's class");
        C(arts.Values.All(a => PeopleEvents10Math.GiftFor(arts.First(kv => kv.Value.Equals(a)).Key) == (a.fighter ? "PhysicalProdigy" : "TM_Gifted")) && PeopleEvents10Math.GiftFor(null) == null,
            "a mage's tome needs the magically gifted, a fighter's book the physically adept");
        var skills = new[] { "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants", "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual" };
        C(arts.Values.All(a => skills.Contains(a.skill)), "each calling's skill is one of the game's");
        var peopleDefs = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").ToDictionary(f => f, File.ReadAllText);
        string kinds = string.Join("\n", peopleDefs.Values);
        C(arts.Values.All(a => kinds.Contains("<defName>" + a.teacher + "</defName>")), "every one of theirs who hands the art on is one of the people's own kinds");
        // A people's book is a class its own kinds have (the proposal's eight among them).
        foreach (var (calling, a) in arts)
        {
            var file = peopleDefs.FirstOrDefault(kv => kv.Value.Contains("<calling>" + calling + "</calling>")).Value;
            C(file != null && file.Contains("<trait>" + a.trait + "</trait>"), $"{calling}: {a.trait} is a class among its own people");
        }

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents10.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_WoodMoot", "Rimfolk_Evt_RoadToll", "Rimfolk_Evt_PlagueYear", "Rimfolk_Evt_LearningTraded", "Rimfolk_Evt_ArtHandedOn" }),
            "five events: four between peoples and every people's art");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events10");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents10Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var pairs = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_WoodMoot"] = "PeopleEvents10.Pair(PeoplesMath.Drillers, PeoplesMath.Grovekeepers", ["Rimfolk_Evt_RoadToll"] = "PeopleEvents10.Pair(PeoplesMath.Traders, PeoplesMath.Salvagers",
            ["Rimfolk_Evt_PlagueYear"] = "PeopleEvents10.Pair(PeoplesMath.Healers, PeoplesMath.Sextons", ["Rimfolk_Evt_LearningTraded"] = "PeopleEvents10.Pair(PeoplesMath.Scholars, PeoplesMath.Vaultfolk",
            ["Rimfolk_Evt_ArtHandedOn"] = "ArtHandedOn.Offers("
        };
        var answers = new Dictionary<string, int>
        {
            ["Rimfolk_Evt_WoodMoot"] = 4, ["Rimfolk_Evt_RoadToll"] = 4, ["Rimfolk_Evt_PlagueYear"] = 4, ["Rimfolk_Evt_LearningTraded"] = 3, ["Rimfolk_Evt_ArtHandedOn"] = 3
        };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            C(m.Success && m.Groups[1].Value.Contains(pairs[name]), $"{name}: only from its peoples at peace with the colony (and with each other)");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            C(chance >= 0.2f && chance <= 0.6f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors" && ext.Element("mods") == null, $"{name}: among the visitors; no DLC or mod needed for it to come");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            bool AgeText(int a) => texts.Any(t => Regex.Match(t, @"^\[([^\]]*)\]").Groups[1].Value.Split('|', ',').Contains("age" + a));
            C(texts.Count >= 5 && Enumerable.Range(0, 4).All(AgeText), $"{name}: five or more texts, every age in its own words");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == answers[name] && ext.Element("defaultOption")?.Value == (answers[name] - 1).ToString(), $"{name}: {answers[name]} answers, no answer the last");
            var last = options.Last().Descendants("li").Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway").ToList();
            bool two = name != "Rimfolk_Evt_ArtHandedOn";
            C(last.Count == (two ? 2 : 1) && (!two || last.Any(li => li.Element("ofGroup")?.Value == "true")), $"{name}: the last answer sends {(two ? "both peoples'" : "them")} on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
            var silver = options.SelectMany(o => o.Descendants("li")).Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver").ToList();
            C(silver.All(li => li.Element("steward")?.Value == "false"), $"{name}: the silver carried out is the sum the card says");
            C(!texts.Any(t => t.Contains("{ENEMY}")) || two, $"{name}: the second people named only where there is one");
        }
        var moot = Ext(events["Rimfolk_Evt_WoodMoot"], "HappeningExtension")!;
        var rulings = moot.Element("options")!.Elements("li").Select(o => o.Descendants("ruling").SingleOrDefault()?.Value).ToList();
        C(rulings.Take(3).SequenceEqual(PeopleEvents10Math.Rulings) && moot.Element("texts")!.Elements("li").All(t => t.Value.StartsWith("[table") || t.Value.StartsWith("[gate"))
          && code.Contains("ConsoleTalk.Table(map, ctx.a)"), "the moot: the three rulings in order; the words say where they are heard, at the table or the gate");
        var toll = Ext(events["Rimfolk_Evt_RoadToll"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(toll.Select(o => o.Descendants("side").SingleOrDefault()?.Value).Take(3).SequenceEqual(new[] { "cross", "back", "fair" })
          && toll[0].Descendants("enemy").Any(x => x.Value == "true") && toll[0].Descendants("amount").Select(a => a.Value).SequenceEqual(new[] { "5", "-5" })
          && toll[1].Descendants("amount").Select(a => a.Value).SequenceEqual(new[] { "5", "-5" }), "the toll: the Wheel up and the league down for the carts let through, the reverse turned back");
        C(code.Contains("new LordJob_ExitMapNear(edge, LocomotionUrgency.Walk") && code.Contains("FarEdge(map, from)") && code.Contains("Couriers.AnimalKind(f, map)")
          && code.Contains("k.RaceProps.packAnimal && !k.RaceProps.predator") && code.Contains("BiomeAnimals.LivesIn(biome, k)")
          && code.Contains("PeoplesD.KindNamed(ctx.enemy, \"Rimfolk_Wheel_Wheelwright\")"), "the Wheel's own cart train (its caravans' beasts, or any pack beast of the game's or the player's mods the land allows) walks across the colony's land to the far edge, or back the way it came");
        var plague = Ext(events["Rimfolk_Evt_PlagueYear"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(plague.Take(3).Select(o => (o.Descendants("doctor").Single().Value, o.Descendants("diggers").Single().Value))
            .SequenceEqual(new[] { ("true", "true"), ("true", "false"), ("false", "true") }), "the plague year: all three, the doctor only, or the two diggers only");
        var learn = Ext(events["Rimfolk_Evt_LearningTraded"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(learn.Take(2).Select(o => o.Descendants("teach").Single().Value).SequenceEqual(new[] { "true", "false" }), "the table: with the crafter's lessons, or the copying only");
        var art = Ext(events["Rimfolk_Evt_ArtHandedOn"], "HappeningExtension")!;
        var artTexts = art.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(new[] { "tome", "passion", "lesson" }.All(k => artTexts.Any(t => t.StartsWith("[" + k + ",deed")) && artTexts.Any(t => t.StartsWith("[" + k + ",nodeed")))
          && artTexts.Where(t => t.StartsWith("[tome")).All(t => t.Contains("{THING}")) && artTexts.Where(t => !t.StartsWith("[tome")).All(t => t.Contains("{SKILL}") && !t.Contains("{THING}"))
          && artTexts.Where(t => t.Contains(",deed")).All(t => t.Contains("{TALE}")) && artTexts.Where(t => t.Contains("nodeed")).All(t => !t.Contains("{TALE}")),
            "their art: the book by name, else the skill; what they remember the colonist for only when a deed names them");
        C(art.Element("options")!.Elements("li").ElementAt(1).Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("variant")?.Value == "true")
          && code.Contains("ctx.variant = PeopleEvents10Math.ReturnGift("), "the second answer carries out the colony's gift in return, the sum by its wealth");

        // Their jobs and moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents10_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 8 && jobs.All(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv9Work" && code.Contains("\"" + Dn(j) + "\"")
                                           && new[] { "look", "quiet", "sparks", "cross", "dust", "hum" }.Contains(j.Descendants("mark").Single().Value)),
            "eight jobs, round 9's work at a place, each given by its code, each with its mark");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Count == 2 && thoughts.Keys.All(t => code.Contains("\"" + t + "\"")) && thoughts.Values.All(t => F(t.Descendants("baseMoodEffect").Single().Value) is var v && v > 0f && v <= 5f),
            "two small good moods: a plague town helped through, an art handed on");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        bool From(string type, string def, string mod) => cat.Any(r => r.active && r.type == type && r.def == def && r.mod.Equals(mod, StringComparison.OrdinalIgnoreCase));
        C(arts.Values.All(a => From("ThingDef", a.tome, "torann.arimworldofmagic") && From("TraitDef", a.trait, "torann.arimworldofmagic"))
          && From("TraitDef", "TM_Gifted", "torann.arimworldofmagic") && From("TraitDef", "PhysicalProdigy", "torann.arimworldofmagic"),
            "the class books, their classes and the latent gifts are A RimWorld of Magic's, active in the player's list");
        var items = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 19).ToDictionary(c => c[0], c => c);
        C(arts.Values.All(a => items.TryGetValue(a.tome, out var row) && row[19].Contains("CompProperties_Usable") && row[19].Contains(a.fighter ? "CompUseEffect_LearnMight" : "CompUseEffect_LearnMagic")),
            "each book is read by the mod's own use: a fighter's teaches might, a mage's magic");
        C(code.Contains("PeoplesMagic.Loaded") && code.Contains("PeopleGifts.ClassOn(") && code.Contains("TryGetComp<CompUsable>()") && code.Contains("use.TryStartUseJob(p,"),
            "the book only where the mod is loaded and the class switched on, read by the game's use of the item");
        C(From("ThingDef", "Steel", "ludeon.rimworld") && From("ThingDef", "MedicineHerbal", "ludeon.rimworld") && From("ThingDef", "Schematic", "ludeon.rimworld")
          && From("ThingDef", "TextBook", "ludeon.rimworld"), "the season's steel and herbal medicine, the copy's book: the game's own");
        var named = Regex.Matches(code, "\"(Rimfolk_(Deepwell|Mossborn|League|Wheel|Order|QuietBell|Scholars|SunkenVault)_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(named.Count >= 8 && named.All(k => kinds.Contains("<defName>" + k + "</defName>")), "every kind of person named is one of the peoples' own");

        // ================= Their words =================

        var keyedFile = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents10.xml"));
        var keyed = XDocument.Parse(keyedFile).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv10_\\w+[A-Za-z])\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 80 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(new[] { "CC_Memory_Deed_plagueYear", "CC_Memory_Deed_plagueYear_Name", "CC_Memory_Deed_plagueYear_Many", "CC_Dip_For_plagueYear" }.All(keyed.Contains),
            "the plague year in the peoples' memory and as a debt of honour, in words");
        var texts10 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents10.xml"), Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents10.xml"),
                Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents10_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts10.Contains('—') && !texts10.Contains('!') && !Regex.IsMatch(texts10, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts10.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts10, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts10, @"\b(god|gods|goddess|divine)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(new[] { "the moot over the old wood", "a toll on the road", "the lamp and the bell", "learning traded", "their art handed on" }.All(peoples.Contains),
            "the peoples' Almanac page says what they do now");

        // ================= The seams =================

        string nine = Read("Peoples", "Events9", "PeopleEvents9.cs");
        C(nine.Contains("return PeopleEvents10.Hour(v, comp, now);") && nine.Contains("PeopleEvents10.Hourly(now)") && nine.Contains("else PeopleEvents10.WorkDone(pawn, job, target);"),
            "round 10's visits, watch and work hand on from round 9's, through rounds 1 to 8");
        var visitKinds = Regex.Matches(code, @"public const string (Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(visitKinds.Count == 4 && visitKinds.All(k => k.StartsWith("pev:")) && visitKinds.Distinct().Count() == 4, "their visits are the events' own kind (\"pev:\"), none twice");
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4", "Events5", "Events6", "Events7", "Events8", "Events9" }
            .SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs")).Select(File.ReadAllText));
        C(visitKinds.All(k => !earlier.Contains("\"" + k + "\"")), "none of their visits' kinds is an earlier round's");
        string season = Read("Peoples", "Events2", "SeasonAway.cs");
        C(season.Contains("[PlagueYear.AwayKey] = new PlagueAway()") && code.Contains("public const string AwayKey = \"plague\";") && code.Contains("SeasonAway.Send(AwayKey"),
            "the plague town is round 2's season away");
        string sick = Read("Harm", "SickHome.cs");
        C(sick.Contains("public static void Lodged(Pawn p, Settlement town, SickTown kind, float hours, bool medicine)") && sick.Contains("caravan = -1 - p.thingIDNumber")
          && code.Contains("SickHome.Lodged(p, town, SickTown.Plague,") && code.Contains("SickHome.TownSickness(x.s) == SickTown.Plague"),
            "lodged at the plague town for the illness budget's own sickness brought home, a journey of its own; only a town the region's plague reaches");
        string memory = Read("World", "FactionMemory.cs");
        C(Regex.IsMatch(memory, @"\[""plagueYear""\] = K\(DeedSide\.Favor, 25\)") && Read("Diplomacy", "FavourMath.cs").Contains("\"plagueYear\"")
          && code.Contains("KinMemory.Note(f, PeopleEvents10Math.PlagueDeed, p,"), "both sent: a lasting deed in both peoples' memories, naming the colonist, and a debt of honour");
        C(code.Contains("w.help++") && code.Contains("w.deeds.Add("), "the plague's world event counts the colony's help, so the town's own people remember it too");
        C(code.Contains("[HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]") && code.Contains("PeopleEvents10Book.AnyPrice") && code.Contains("RimfolkPatches.On")
          && Regex.Matches(code, @"\[HarmonyPatch\(").Count == 1, "one game hook: the season's prices, idle unless a ruling moved one, with the deeper hooks on");
        C(code.Contains("FactionMemory.Note(union, \"sidedWith\"") && code.Contains("FactionMemory.Note(moss, \"tookSides\"") && code.Contains("FactionMemory.Note(union, \"mediated\""),
            "the moot is remembered as the world events' moot remembers a side taken, or a quarrel mediated");
        C(code.Contains("VaultSupply.Noted(vault, PeopleEvents10Math.CraftWorth") && code.Contains("FactionMemory.Note(society, \"learnedHere\"") && code.Contains("FactionMemory.Note(vault, \"learnedHere\"")
          && Read("Peoples", "PeopleAgesMath.cs").Contains("LearnerDeeds = { \"learnedHere\""), "the vault's craft holds its age; both peoples' ages move by the teaching rule");
        C(code.Contains("HappeningWorker_ScholarCopyist.Book()") && code.Contains("Hosting.Sleep(x, bed)") && code.Contains("Hosting.Eat(x, meal, null)"),
            "the society's book of the colony's age; the guests fed and bedded as hosted guests are");
        C(code.Contains("KinMemory.Friend(f, out deed)") && code.Contains("PeopleEvents.MayGive(GiftKind, p)") && code.Contains("PeopleEventsBook.Gave(GiftKind, p)")
          && code.Contains("GaveArt(f)") && code.Contains("FactionMemory.Note(f, \"taught\""), "their art: to the colonist they remember best, once a game a people, under the peoples' gift rule, a lesson owed");
        C(Read("Peoples", "PeopleAgesMath.cs").Contains("LessonDeeds = { \"taught\" }"), "a lesson owed moves their age by the teaching rule");
        C(!Regex.IsMatch(code, @"Harm\.(Roll|Ail|Injure|Fight)\(") && !Regex.IsMatch(code, @"TakeDamage"), "no harm of their own: the plague town's sickness is the illness budget's own rule");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("PeoplesG.Say("), "they speak through the events' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents10Book.cs"));
        C(book.Contains("prices.Count > PeopleEvents10Math.MaxPrices") && book.Contains("artGiven.Count > PeopleEvents10Math.Arts.Count * 3"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 10: ").Count == 5, "five dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "prices", "artGiven" }.All(x => save.Contains("Peoples/Events10/PeopleEvents10Book.cs:" + x)), "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", ""))
                .Where(v => v != null && v.StartsWith("DilemmaEffect_") && Regex.IsMatch(code, @"class " + v + @"\b")).Distinct().All(v => audit.Contains("\"" + v + "\"")),
            "the play-out audit knows each answer of theirs that plays out on the map");
        string proposals = File.ReadAllText(Path.Combine(root, "docs", "peoples-events-proposals.md"));
        C(Enumerable.Range(96, 5).All(i => Regex.IsMatch(proposals, @"\n" + i + @"\. \*\*[^\n]*\*\*[^\n]*Built, round 10")), "each item marked built in the proposals");
        return n;
    }

    private static string Dn(XElement e) => e.Element("defName")?.Value ?? "";

    private static XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);

    private static List<(string type, string def, string mod, bool active)> catalogue;

    /// <summary>The player's mods' every def (docs/player-mods/defs.tsv.gz): type, name, package, active.</summary>
    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
