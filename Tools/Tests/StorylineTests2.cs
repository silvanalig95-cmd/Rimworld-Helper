using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Storylines, round 2 (docs/quests-proposals.md S2, S3, S5, S7; Storylines/; DESIGN.md "Storylines, round 2"): the
/// sickness in the next town, the strayed herd, the pilgrims' relic and two towns' quarrel. Each one's branches, its
/// numbers, one path played out to its ending, and the mod's side: its cards and their answers, the illness caught through
/// the harm and illness budgets, the herd's animals from the player's mods, the one away part within three tiles, the two
/// peoples' relations, the relic at the shrine, the dev actions and the spoken lines. Every storyline's shape (each part
/// and outcome leading on, its event, terms and words) is checked with round 1's in StorylineTests.
/// </summary>
public static class StorylineTests2
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "storylines, round 2: " + what);
        }

        StorylineMath.Step Next(string kind, string part, string outcome, int times = 1, float roll = 0.5f) => StorylineMath.Next(kind, part, outcome, times, roll);

        // ================= S2, the sickness in the next town =================

        const string Sick = SicknessFlow.Key;
        C(StorylineMath.Parts(Sick).SequenceEqual(new[] { "medicine", "sick", "healer", "kin" }), "the sickness: medicine, the sick, then the healer or the families");
        foreach (var o in new[] { "sent", "partly" })
        {
            var s = Next(Sick, "medicine", o);
            C(s.part == "sick" && s.flag == o && s.min >= 2f, "medicine " + o + ": the sick come next, a few days later");
        }
        C(Next(Sick, "medicine", "none").ending == "nohelp" && Next(Sick, "medicine", "declined").ending == "declined", "no medicine, or turned down: it ends");
        C(Next(Sick, "sick", "recovered").part == "healer", "all the sick recovered: the order's healer comes to teach");
        C(Next(Sick, "sick", "somedied") is var died && died.part == "kin" && died.flag == "died" && died.max <= 2f, "some died: their families come for the bodies within two days");
        C(Next(Sick, "sick", "harmed").ending == "harmed" && Next(Sick, "sick", "declined").ending == "turnedaway", "the sick harmed, or not taken in");
        C(Next(Sick, "healer", "taught").ending == "taught" && Next(Sick, "healer", "fewlessons").ending == "taught" && Next(Sick, "healer", "declined") is var th
          && th.ending == "thanked" && th.max > 0f, "the healer taught, or turned down and the order thanks the colony a few days later");
        C(new[] { "handed", "buried", "nobodies" }.All(o => Next(Sick, "kin", o).ending == o) && Next(Sick, "kin", "declined").ending == "refused", "the dead handed over, buried here, gone, or the families turned away");
        C(StorylineMath.EndingHasLetter(Sick, "thanked") && !StorylineMath.EndingHasLetter(Sick, "taught"), "the order's thanks come in a letter of their own");
        C(Enumerable.Range(0, 30).All(c => SicknessFlow.MedicineCount(c) >= 6 && SicknessFlow.MedicineCount(c) <= 14) && SicknessFlow.MedicineCount(30) > SicknessFlow.MedicineCount(2),
            "six to fourteen medicine, more for a bigger colony");
        C(SicknessFlow.MedicinePay(10, 15f) == 230 && SicknessFlow.MedicinePay(0, 15f) == 60 && SicknessFlow.PartPay(5, 15f) == 80, "half again over its worth for all of it; part of it, its plain worth");
        C(SicknessFlow.MedicineOutcome(10, 10) == "sent" && SicknessFlow.MedicineOutcome(5, 10) == "partly" && SicknessFlow.MedicineOutcome(4, 10) == "none", "all, half or more, or less than half");
        C(Enumerable.Range(0, 100).Select(i => SicknessFlow.SickCount(3, i / 100f)).All(x => x >= 3 && x <= 5) && SicknessFlow.SickCount(12, 0.99f) == 6, "three to six sick");
        var ills = new[] { 0, 1, 2, 3 }.Select(age => Enumerable.Range(0, 100).Count(i => SicknessFlow.Illness(age, false, i / 100f) == "Plague")).ToList();
        C(ills[0] == 40 && ills[1] == 40 && ills[2] == 15 && ills[3] == 0, "the game's plague likelier for the tribal and medieval ages, rare in the industrial, never the spacer; else the flu (" + string.Join(",", ills) + ")");
        C(Enumerable.Range(0, 100).Count(i => SicknessFlow.Illness(1, true, i / 100f) == "Plague") == 70, "a plague among the region's world events makes it likelier");
        C(SicknessFlow.ImmunityMin > SicknessFlow.SeverityMax && SicknessFlow.SeverityMax < 0.5f, "they come well along, the body already fighting it, so with care they mend");
        C(SicknessFlow.LessonXp(15, 3) == 320f && SicknessFlow.LessonXp(8, 8) == 150f && SicknessFlow.LessonXp(5, 10) == 150f, "a lesson's experience by how far the healer leads: 150 to 320 an hour");
        C(SicknessFlow.Taught(3, 5) && !SicknessFlow.Taught(2, 5) && !SicknessFlow.Taught(0, 1), "taught: a lesson on at least half the days");
        C(SicknessFlow.KinCount(1) == 2 && SicknessFlow.KinCount(4) == 4 && SicknessFlow.KinCount(9) == 5, "one of the family to carry each of the dead, two to five");

        // ================= S3, the strayed herd =================

        const string Herd = HerdFlow.Key;
        C(StorylineMath.Parts(Herd).SequenceEqual(new[] { "stray", "winter", "spring" }), "the herd: the strays penned, part of the herd kept, the herders in spring");
        C(Next(Herd, "stray", "penned").part == "winter" && Next(Herd, "stray", "most").part == "winter" && Next(Herd, "stray", "lost").ending == "lost",
            "penned or most kept: the herders ask the colony to keep part of the herd; most lost: it ends");
        C(Next(Herd, "winter", "kept").part == "spring" && Next(Herd, "winter", "somedied").flag == "died" && Next(Herd, "winter", "declined").ending == "notkept",
            "the keeping ends at spring and the herders come; not kept, it ends");
        C(new[] { "young", "paid", "bought", "unpaid" }.All(o => Next(Herd, "spring", o).ending == o) && Next(Herd, "spring", "declined").ending == "taken",
            "young left, the dead paid for, the rest bought, or not paid; not answered, the herders take their animals anyway");
        C(StorylineMath.EndingHasLetter(Herd, "taken") && !StorylineMath.EndingHasLetter(Herd, "young"), "taken without a word: a letter of its own");
        C(Enumerable.Range(0, 100).Select(i => HerdFlow.StrayCount(3, i / 100f)).All(x => x >= 5 && x <= 8) && HerdFlow.StrayCount(10, 0.99f) == 9, "five to nine strays");
        C(HerdFlow.StrayOutcome(6, 6, 6) == "penned" && HerdFlow.StrayOutcome(2, 6, 6) == "most" && HerdFlow.StrayOutcome(3, 3, 6) == "most" && HerdFlow.StrayOutcome(2, 2, 6) == "lost"
          && HerdFlow.StrayOutcome(0, 0, 0) == "lost", "every one penned, half or more still here, or fewer");
        C(HerdFlow.KeepCount(5) == 3 && HerdFlow.KeepCount(8) == 4 && HerdFlow.KeepCount(20) == 5, "three to five kept over the winter");
        C(HerdFlow.KeepDays(20f, true) == 20f && HerdFlow.KeepDays(3f, true) == 10f && HerdFlow.KeepDays(50f, true) == 32f && HerdFlow.KeepDays(float.NaN, true) == 15f
          && HerdFlow.KeepDays(20f, false) == 15f, "until spring, ten to 32 days; a season where the year has none");
        C(HerdFlow.AskSeason("summer") && HerdFlow.AskSeason("fall") && HerdFlow.AskSeason("none") && !HerdFlow.AskSeason("spring") && !HerdFlow.AskSeason("winter"),
            "first asked in summer or autumn, so the keeping is a winter");
        C(HerdFlow.FodderNeed(4f, 10f) == 48f && HerdFlow.WinterOutcome(4, 4) == "kept" && HerdFlow.WinterOutcome(3, 4) == "somedied", "the fodder the days ask for, and kept or some died");
        C(HerdFlow.YoungCount(5) == 2 && HerdFlow.YoungCount(3) == 1 && HerdFlow.DeadPay(95f) == 100 && HerdFlow.BuyPrice(500f) == 450, "the young they leave, what the dead are paid, what the rest cost");
        // The herd's animals named by age are the game's and the player's mods' (docs/player-mods: active rows).
        var kinds = ReadActive(Path.Combine(root, "docs", "player-mods", "pawnkinds.tsv"));
        foreach (int age in new[] { 0, 1, 2 })
            foreach (var k in HerdFlow.Kinds(age))
                C(kinds.Contains(k), "the herd's " + k + " is in the player's enabled mods");

        // ================= S5, the pilgrims' relic =================

        const string Relic = RelicFlow.Key;
        C(StorylineMath.Parts(Relic).SequenceEqual(new[] { "rest", "trail", "shrine" }), "the relic: the pilgrims rest, after the thief, the shrine");
        foreach (var o in RelicFlow.Caught)
            C(Next(Relic, "rest", o) is var b && b.part == "shrine" && b.flag == o, "the relic back (" + o + "): they ask to leave it at the shrine");
        C(Next(Relic, "rest", "escaped").part == "trail" && Next(Relic, "rest", "escaped").max <= 2f, "the thief got away: after the thief within two days");
        C(Next(Relic, "rest", "harmed").ending == "harmed" && Next(Relic, "rest", "declined").ending == "declined", "the pilgrims harmed, or turned down");
        C(Next(Relic, "trail", "recovered").part == "shrine" && Next(Relic, "trail", "notfound").ending == "lost" && Next(Relic, "trail", "declined").ending == "lost",
            "the relic recovered, or lost");
        C(Next(Relic, "shrine", "kept").ending == "shrine" && Next(Relic, "shrine", "noshrine").ending == "home" && Next(Relic, "shrine", "declined").ending == "home",
            "left at the shrine, or taken home");
        C(RelicFlow.CampMaxTiles <= 3 && RelicFlow.CampMinTiles >= 1, "the one away part: the thief's camp only within three tiles (docs/quests-proposals.md rule 2)");
        C(RelicFlow.FindChance(true) > RelicFlow.FindChance(false) && RelicFlow.SearchMeals(4) == 16 && RelicFlow.SearchMeals(1) == 6, "the pilgrims' own search: the colony's food makes it likelier");
        C(RelicFlow.Visits == 3 && RelicFlow.Visits * RelicFlow.VisitEvery <= RelicFlow.ShrineDays, "a year at the shrine, pilgrims once a season");
        C(RelicFlow.JudgeGoodwill("handed", true) > RelicFlow.JudgeGoodwill("punished", true) && RelicFlow.JudgeGoodwill("letgo", false) < 0 && RelicFlow.JudgeGoodwill("killed", true) < 0,
            "the judgement as the pilgrims see it: handed over best; letting a stranger go, or killing one of their own, sits badly");
        C(RelicFlow.TheftNight == 2 && RelicFlow.TheftHour >= 0 && RelicFlow.TheftHour <= 4 && RelicFlow.RestMinDays >= 2f, "the theft on their second night, when everyone is asleep");

        // ================= S7, two towns' quarrel =================

        const string Quarrel = QuarrelFlow.Key;
        C(StorylineMath.Parts(Quarrel).SequenceEqual(new[] { "truce", "pledge", "fighters" }), "the quarrel: the truce, the pledge, the fighters");
        C(Next(Quarrel, "truce", "witnessed").part == "pledge" && Next(Quarrel, "truce", "missed").ending == "nopeace" && Next(Quarrel, "truce", "declined").ending == "declined",
            "the truce witnessed, missed or turned down");
        C(Next(Quarrel, "pledge", "kept").part == "fighters" && Next(Quarrel, "pledge", "poorly").flag == "poorly" && Next(Quarrel, "pledge", "harmed").ending == "blamed"
          && Next(Quarrel, "pledge", "declined") is var np && np.part == "fighters" && np.flag == "nopledge", "the pledge kept or not, the fighters still come; harmed, the colony is blamed");
        C(Next(Quarrel, "fighters", "warned").ending == "warned" && Next(Quarrel, "fighters", "stopped").ending == "peace" && Next(Quarrel, "fighters", "fought").ending == "fought",
            "warned, stopped at the gate, or fought off");
        C(Next(Quarrel, "fighters", "passed") is var war && war.ending == "war" && war.min >= 1f && Next(Quarrel, "fighters", "declined").ending == "war",
            "let pass or no answer: the two go to war a day or two later");
        C(StorylineMath.EndingHasLetter(Quarrel, "war") && !StorylineMath.EndingHasLetter(Quarrel, "peace"), "the war comes in a letter of its own");
        C(QuarrelFlow.AtOdds(-5, false) && QuarrelFlow.AtOdds(10, true) && !QuarrelFlow.AtOdds(0, false), "at odds: at war with each other, or their goodwill below nothing");
        C(QuarrelFlow.TruceGoodwill(-80) == 10 && QuarrelFlow.TruceGoodwill(-5) == 10 && QuarrelFlow.TruceGoodwill(95) == 100, "the truce: at least peace between them");
        C(QuarrelFlow.ChangeTo(10, QuarrelFlow.WarGoodwill) == -90 && QuarrelFlow.WarGoodwill <= -75, "let pass, their goodwill falls to the game's war");
        C(QuarrelFlow.Fighters(100f) == 3 && QuarrelFlow.Fighters(600f) == 5 && QuarrelFlow.Fighters(5000f) == 6, "three to six fighters, by the threat now");
        C(QuarrelFlow.ForceChance(6, 3) == 0.05f && QuarrelFlow.ForceChance(1, 6) == 0.5f && QuarrelFlow.ForceChance(4, 4) > 0.1f, "they force the gate more often the fewer stand against them, never past half");
        C(QuarrelFlow.Thanks("warned").first > 0 && QuarrelFlow.Thanks("warned").other < 0 && QuarrelFlow.Thanks("peace").first > 0 && QuarrelFlow.Thanks("blamed").first < 0,
            "thanked by one and resented by the other");
        C(QuarrelFlow.PledgeOutcome(5, 0) == "kept" && QuarrelFlow.PledgeOutcome(4, 1) == "poorly", "the pledge kept, or poorly");

        // ================= Each played out =================

        // The sickness: medicine sent, every one of the sick recovered, the healer taught.
        StorylineTests.PlayOut(C, Sick, new[] { ("medicine", "sent", 7f), ("sick", "recovered", 6f), ("healer", "taught", 6f) }, "taught", 0.5f, 20f, 35f);
        // Some died, and the families take them home.
        StorylineTests.PlayOut(C, Sick, new[] { ("medicine", "partly", 7f), ("sick", "somedied", 6f), ("kin", "handed", 1f) }, "handed", 0.5f, 15f, 25f);
        // The herd: penned, kept the winter, young left in spring.
        StorylineTests.PlayOut(C, Herd, new[] { ("stray", "penned", 4f), ("winter", "kept", 25f), ("spring", "young", 1f) }, "young", 0.5f, 30f, 40f);
        // The relic: stolen, the thief caught and handed over, left at the shrine.
        StorylineTests.PlayOut(C, Relic, new[] { ("rest", "handed", 3f), ("shrine", "kept", 1f) }, "shrine", 0.5f, 5f, 12f);
        // Got away; the pilgrims' own search found it.
        StorylineTests.PlayOut(C, Relic, new[] { ("rest", "escaped", 3f), ("trail", "recovered", 5f), ("shrine", "kept", 1f) }, "shrine", 0.5f, 10f, 20f);
        // The quarrel: witnessed, the pledge kept, the fighters stopped at the gate.
        StorylineTests.PlayOut(C, Quarrel, new[] { ("truce", "witnessed", 1f), ("pledge", "kept", 5f), ("fighters", "stopped", 1f) }, "peace", 0.5f, 10f, 20f);
        StorylineTests.PlayOut(C, Quarrel, new[] { ("truce", "witnessed", 1f), ("pledge", "kept", 5f), ("fighters", "passed", 1f) }, "war", 0.5f, 10f, 22f);

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        var incidents = defs.SelectMany(d => d.Root!.Elements("IncidentDef")).ToList();
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil." + cls);

        void Card(string name, string story, string[] choices, int def)
        {
            var d = incidents.FirstOrDefault(i => (string?)i.Element("defName") == name);
            C(d != null && (string?)Ext(d, "StorylineCard")?.Element("story") == story && (string?)d.Element("workerClass") == "ColonyCouncil.HappeningWorker_StorylineCard"
              && (string?)d.Element("baseChance") == "0", name + ": a card of " + story + ", never drawn on its own");
            if (d == null) return;
            var h = Ext(d, "HappeningExtension")!;
            var opts = h.Element("options")!.Elements("li").ToList();
            C(opts.Count == choices.Length && (string?)h.Element("defaultOption") == def.ToString(), name + ": " + choices.Length + " answers, answer " + def + " if none is given");
            for (int i = 0; i < Math.Min(opts.Count, choices.Length); i++)
            {
                string got = opts[i].Element("effects")?.Elements("li").FirstOrDefault(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_StorylineChoice")?.Element("choice")?.Value ?? "";
                C(got == choices[i], name + ": answer " + i + " is " + choices[i]);
            }
        }
        Card("Rimfolk_Storyline_SicknessDead", Sick, new[] { "hand", "bury" }, 0);
        Card("Rimfolk_Storyline_HerdDead", Herd, new[] { "pay", "buy", "refuse" }, 2);
        Card("Rimfolk_Storyline_RelicJudge", Relic, new[] { "hand", "punish", "letgo" }, 0);
        Card("Rimfolk_Storyline_QuarrelCard", Quarrel, new[] { "pass", "warn", "stop" }, 0);
        var sickness = Code("Storylines/StorylineSickness.cs");
        var herd = Code("Storylines/StorylineHerd.cs");
        var relic = Code("Storylines/StorylineRelic.cs");
        var quarrel = Code("Storylines/StorylineQuarrel.cs");
        C(sickness.Contains("Card = \"Rimfolk_Storyline_SicknessDead\"") && herd.Contains("Card = \"Rimfolk_Storyline_HerdDead\"") && relic.Contains("Card = \"Rimfolk_Storyline_RelicJudge\"")
          && quarrel.Contains("Card = \"Rimfolk_Storyline_QuarrelCard\""), "the code asks the cards by their names");
        var herdCard = incidents.First(i => (string?)i.Element("defName") == "Rimfolk_Storyline_HerdDead").ToString();
        C(Regex.Matches(herdCard, "DilemmaEffect_CarryOut").Count == 2 && herdCard.Contains("<what>silver</what>"), "what the colony pays the herders is carried out to them on the map");

        // The sickness: the game's own illness, cared for as the game cares for a guest, caught through both budgets.
        C(sickness.Contains("SicknessFlow.Illness(") && sickness.Contains("DefDatabase<HediffDef>.GetNamedSilentFail(SicknessFlow.Flu)") && !sickness.Contains("Rimfolk_Ail"),
            "the illness is the game's own flu or plague, never one made up");
        C(sickness.Contains("guest?.SetGuestStatus(Faction.OfPlayer)") && sickness.Contains("RestUtility.FindPatientBedFor"), "the sick are the colony's guests in its beds, the game's own care");
        C(sickness.Contains("Harm.Roll(doctor, HarmKinds.Physician") && sickness.Contains("IllnessBudget.Allows(") && sickness.Contains("IllnessBudget.Owe(") && sickness.Contains("HarmFour.Wait("),
            "a colonist who tends them may catch it: the physician's chance, through the harm and illness budgets, shown a day or two later");
        C(Code("Health/HealthPatches.cs").Contains("if (!SicknessRules.Tended(doctor, patient)) Catching.Tended(doctor, patient);"), "the game's tending reaches it, and the everyday rule leaves the town's sick alone");
        C(sickness.Contains("new DilemmaEffect_Lesson") && sickness.Contains("skill = SkillDefOf.Medicine"), "the healer teaches by the play-out's lesson: the doctor works at their side and learns at the game's pace");
        C(sickness.Contains("JobDefOf.Steal") && sickness.Contains("PlayBurial.Plot("), "the families carry their dead off the map, or graves are laid out for the colony to dig");

        // The herd: the game's own pen animals, from every mod's herd animals, led out through fences.
        C(herd.Contains("rp.herdAnimal") && herd.Contains("rp.roamMtbDays == null") && herd.Contains("PokeWorldLink.IsPokemon(k)") && herd.Contains("DefDatabase<PawnKindDef>.AllDefsListForReading"),
            "the herd is any herd animal the game or the player's mods add that a pen holds (never a Pokémon)");
        C(herd.Contains("a.SetFaction(Faction.OfPlayer)") && herd.Contains("AnimalPenUtility.GetCurrentPenOf"), "the strays are the colony's to rope into a pen, and penned is the game's pen");
        var ledOut = defs.SelectMany(d => d.Root!.Elements("JobDef")).FirstOrDefault(j => (string?)j.Element("defName") == "Rimfolk_Storyline_LedOut");
        C(ledOut != null && (string?)ledOut.Element("ignoreFenceBlocked") == "true", "the animals are led out of their pens to the herders as a roped animal is");
        C(herd.Contains("SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay"), "the winter's fodder measured as the game's pens do");

        // The relic: the thing of their faith, the theft on the map, the camp within three tiles.
        var relicDef = defs.SelectMany(d => d.Root!.Elements("ThingDef")).FirstOrDefault(t => (string?)t.Element("defName") == "Rimfolk_PilgrimRelic");
        C(relicDef != null && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "PilgrimRelic.png")), "the relic is Rimfolk's own, with its picture");
        C(relic.Contains("JobDefOf.Steal") && relic.Contains("LordJob_ExitMapBest(LocomotionUrgency.Jog"), "the thief carries it off for the edge of the map");
        C(relic.Contains("RelicFlow.CampMinTiles, RelicFlow.CampMaxTiles") && relic.Contains("SitePartDefOf.BanditCamp"), "the camp is the game's bandit camp, only within three tiles");
        C(relic.Contains("[HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]") && relic.Contains("RelicRules.MapMade(__instance)"), "the relic and the thief are in the camp when the colony gets there");
        C(relic.Contains("kind = \"relic\"") && relic.Contains("public override bool After(StorylineAfter a, int now)"), "the relic at the shrine brings pilgrims once a season, then goes home");
        C(Code("Storylines/Storylines.cs").Contains("Scribe_References.Look(ref thing, \"thing\")") && Code("Storylines/Storylines.cs").Contains("Scribe_Values.Look(ref site, \"site\", -1)"),
            "the relic kept at the shrine and the thief's camp are saved");

        // The quarrel: the game's relations between the two, with a reason; the crossing the game's travellers'.
        C(quarrel.Contains("a.TryAffectGoodwillWith(b, change, canSendMessage: false, canSendHostilityLetter: false, reason: GoodwillWhy.Of(reason))"),
            "the two peoples' goodwill toward each other changes in the game's own relations (war at -75), with a reason");
        C(quarrel.Contains("new LordJob_TravelAndExit(dest)") && quarrel.Contains("QuarrelFlow.ForceChance(QuarrelRules.Defenders(map)"), "the fighters cross as the game's travellers; forcing the gate a chance");
        C(quarrel.Contains("Rimfolk_Mission_StoodWitness"), "the witness remembers it as the witness mission's witness does");

        // Dev mode.
        var dbg = Code("Storylines/StorylinesDebug.cs");
        C(new[] { "start the sickness in the next town now", "start the strayed herd now", "start the pilgrims' relic now", "start two towns' quarrel now", "the pilgrims' relic stolen tonight, now" }
            .All(a => dbg.Contains("Storylines: " + a)), "the dev actions to start each, and the theft at once");

        // Spoken lines: one sentence each (ImmersionTests' rule for bubbles), plain.
        foreach (var name in new[] { "Sickness", "Herd", "Relic", "Quarrel" })
        {
            var doc = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Storyline_" + name + ".xml"));
            foreach (var line in doc.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")))
                C(Regex.Matches(line, @"[.?]").Count == 1 && (line.EndsWith(".") || line.EndsWith("?")), name + ": a spoken line is one sentence (" + line + ")");
        }
        return n;
    }

    /// <summary>The defNames of a player-mods table whose mod is in the player's list (active yes).</summary>
    private static HashSet<string> ReadActive(string path)
    {
        var set = new HashSet<string>();
        if (!File.Exists(path)) return set;
        foreach (var row in File.ReadLines(path).Skip(1))
        {
            var cols = row.Split('\t');
            if (cols.Length > 2 && cols[2] == "yes") set.Add(cols[0]);
        }
        return set;
    }
}
