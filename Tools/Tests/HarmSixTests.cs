using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Harm, part 6 (Harm/HarmSixMath.cs; docs/harm-review-proposals.md 2.1.2, 2.1.3, 1.5.2, 3.3.2, 3.4.2, 4.3.2, 4.4.1, 3.11.1,
/// 3.11.2, 4.7.1, 2.6.3; 2.4.3, 2.10.2, 3.11.4, 4.5.2, 4.6.1, 4.6.2; 4.1.2, 4.2.1, 4.8.2, 3.6.2, 3.7.2, 3.8.2): each rate set
/// against the review's, each prevention easing it; and the mod's side: every hurt through the one service with its kind,
/// hooked where its system lives, named before the choice, seen on the map, messages only where the game gives one, said in
/// one sentence.
/// </summary>
public static class HarmSixTests
{
    private const int D = HarmSixMath.DayTicks, H = HarmSixMath.HourTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm six: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= The work's accidents =================

        C(Near(HarmSixMath.HotHeavy(false), 1f / 40f) && Near(HarmSixMath.HotHeavy(true), 1f / 80f), "hot and heavy answers: one play-out in 40, skilled hands half (2.1.2)");
        C(HarmSixMath.Hot(AnswerWork.Fire) && HarmSixMath.Hot(AnswerWork.Pyre) && !HarmSixMath.Hot(AnswerWork.Carry) && !HarmSixMath.Hot(AnswerWork.Dig),
            "the fire and the pyre are hot work, the dead and the graves heavy");
        C(HarmSixMath.HotHigh <= 4f && HarmSixMath.HeavyHigh <= 5f, "a small injury: a burn or a bruise of a few points");
        C(Near(HarmSixMath.BackTooSoonChance, 1f / 3f) && Near(HarmSixMath.FallMoreDays, 2f) && Near(HarmSixMath.BackTooSoonKeepDays, 1f),
            "back too soon: a third of the times they overdo it, two more days in bed, within the day (2.1.3)");
        C(HarmSixMath.LastPush(10 * D, 8 * D) && !HarmSixMath.LastPush(10 * D, 6 * D) && !HarmSixMath.LastPush(10 * D, 11 * D),
            "the project's last push: its last three days, not after it's due (1.5.2)");
        C(Near(HarmSixMath.ProjectPush(false, false, false), HarmMath.TiredHands(1, false, false, false)) && Near(HarmSixMath.ProjectPush(true, false, false), 2f / 150f),
            "the last push is tired hands' own rule");
        C(HarmSixMath.Clumsy(false, false, false, false) == 0f && Near(HarmSixMath.Clumsy(true, false, false, false), 0.5f / 150f),
            "clumsy when worn: half the tired-hands rate in the high band, nothing out of it (3.3.2)");
        C(Near(HarmSixMath.Clumsy(true, false, false, false) + HarmMath.TiredHands(1, false, false, false), 1.5f / 150f),
            "with a work decree, tired hands and clumsy together come to one and a half times the rate");
        C(Near(HarmSixMath.Makeshift(true, true, true), 1f / 200f) && HarmSixMath.Makeshift(true, false, true) == 0f && HarmSixMath.Makeshift(false, true, true) == 0f
          && HarmSixMath.Makeshift(true, true, false) == 0f, "the makeshift spot: one bill in 200, only while a proper bench could be built (3.4.2)");
        C(Near(HarmSixMath.TakingDown(true, 4), 0.01f) && HarmSixMath.TakingDown(true, 5) == 0f && HarmSixMath.TakingDown(false, 0) == 0f,
            "taking down: one wall or door in 100 under Construction 5 (4.3.2)");
        C(Near(HarmSixMath.Hurried(true, false, 0), 1f / 200f) && Near(HarmSixMath.Hurried(true, true, 0), 2f / 200f) && Near(HarmSixMath.Hurried(true, false, 8), 0.5f / 200f)
          && HarmSixMath.Hurried(false, true, 0) == 0f, "hurried hands: one hour in 200, twice after dark, half for a skilled grower, nothing unhurried (4.4.1)");
        C(Near(HarmSixMath.StrongBackStrain, 0.0025f) && Near(HarmSixMath.StrongBackDrop, 1f / 150f) && Near(HarmSixMath.HeavyDayHours, 6f),
            "carries more than they should: the strain's 0.005 half again, a dropped load now and then (3.11.1)");
        C(Near(HarmSixMath.NickChance, 0.1f) && HarmSixMath.NickHigh <= 2f, "sharp habits: a nick one day in ten at it, the mildest (3.11.2)");
        C(HarmSixMath.Streak(4, 2, 5) == 3 && HarmSixMath.Streak(3, 2, 5) == 1 && HarmSixMath.Streak(5, 2, 5) == 2 && HarmSixMath.SecondCupDue(3) && !HarmSixMath.SecondCupDue(2)
          && Near(HarmSixMath.SecondCupChance, 0.2f), "the second drink: three days running, then one day in five (4.7.1)");
        C(HarmSixMath.StaggerKept(0, 3 * H) && !HarmSixMath.StaggerKept(0, 4 * H) && !HarmSixMath.StaggerKept(5, 0), "the stagger looked for four hours after the cup");
        C(Near(HarmSixMath.SoreAll, 0.0975f) && Near(HarmSixMath.HandsSore(1f, true), 0.0975f / 2f) && Near(HarmSixMath.HandsSore(0.5f, false), 0.0975f / 2f),
            "the hands come home sore: a day and a half of the work ailments' own rates, half for the tough or half the work (2.6.3)");
        C(HarmSixMath.SoreWhich(0f) == 0 && HarmSixMath.SoreWhich(0.5f) == 1 && HarmSixMath.SoreWhich(0.99f) == 2, "a strained back, blisters or a sunburn, by their own rates");

        // ================= The animals =================

        C(Near(HarmSixMath.DroversChance, 1f / 30f) && HarmSixMath.BullCrackShare < 0.2f && HarmSixMath.BullChildHigh <= 3f,
            "the drovers' bull: one hour in 30 among the herd, rarely a crack, a child's small bruise (2.4.3)");
        C(Near(HarmSixMath.TooClose(0.1f, false), 0.05f) && Near(HarmSixMath.TooClose(0f, false), 0.025f) && Near(HarmSixMath.TooClose(0.5f, false), 0.1f)
          && HarmSixMath.TooClose(0.3f, true) == 0f, "the wild one too close: about one in 20 by the kind's taming revenge, never the cautious (2.10.2)");
        C(HarmSixMath.NipHigh < 2f, "a child's nip stays under the size a bite carries anything");
        C(HarmSixMath.Dawn(5) && HarmSixMath.Dawn(7) && !HarmSixMath.Dawn(9) && Near(HarmSixMath.DawnPredator(true, true, true), 1f / 8f)
          && HarmSixMath.DawnPredator(false, true, true) == 0f && HarmSixMath.DawnPredator(true, false, true) == 0f, "walks the fields at dawn: alone, beyond walls, a predator near (3.11.4)");
        C(HarmSixMath.Bold(true, true, 3f) && !HarmSixMath.Bold(true, true, 2f) && !HarmSixMath.Bold(false, true, 9f) && !HarmSixMath.Bold(true, false, 9f),
            "the fed wolf grows bold: a predator fed some days, never a grazer (4.5.2)");
        C(Near(HarmSixMath.BoldNightChance * 15f, 0.375f) && HarmSixMath.Night(22) && HarmSixMath.Night(2) && !HarmSixMath.Night(12),
            "one night in 40 with someone alone near the stores: about one fed predator in four over a season");
        C(Near(HarmSixMath.CatchFight(false), 0.25f) && HarmSixMath.CatchFight(true) == 0f, "a catch that fights back: one try in four, a partner takes it (4.6.1)");
        C(Near(HarmSixMath.StoresVisitChance * HarmSixMath.StoresSwipeChance * 15f, 0.5f) && HarmSixMath.SwipeHigh <= 4f,
            "the visitor at the stores: a few visits a season, one shooing in ten a swipe (4.6.2)");

        // ================= The contests and the fights =================

        C(Near(HarmSixMath.Spark(true, false, 1.2f, true), 0.05f) && HarmSixMath.Spark(true, true, 2f, true) == 0f && HarmSixMath.Spark(false, false, 2f, true) == 0f
          && HarmSixMath.Spark(true, false, 0.5f, true) == 0f && HarmSixMath.Spark(true, false, 2f, false) == 0f, "the bonfire's spark: one in 20 on a dry, windy summer night in the open (4.1.2)");
        C(HarmSixMath.SingeHigh <= 2f && HarmSixMath.SparkNear >= 2f, "a singe is the mildest, the grass a few steps off");
        C(Near(HarmSixMath.Muster(false), 1f / 30f) && Near(HarmSixMath.Muster(true), 1f / 60f), "musters at the line: one in 30, half with a marshal of Shooting 10 (4.2.1)");
        int musterEvenings = Enumerable.Range(0, 60).Count(HarmSixMath.MusterEvening), boutEvenings = Enumerable.Range(0, 60).Count(HarmMath.MusterEvening);
        C(musterEvenings == 15 && musterEvenings + boutEvenings == 60, "the muster at the line takes the militia's evening off its bouts, one in four");
        C(HarmSixMath.MishapOf(true, false, 0.9f) == MusterMishap.Bowstring && HarmSixMath.MishapOf(false, true, 0.1f) == MusterMishap.Charge
          && HarmSixMath.MishapOf(false, false, 0.1f) == MusterMishap.Powder && HarmSixMath.MishapOf(false, false, 0.9f) == MusterMishap.Graze, "the mishap by the weapon's age");
        C(Near(HarmSixMath.SparBlowChance, 1f / 25f) && HarmSixMath.SparDay(0) && !HarmSixMath.SparDay(1) && HarmSixMath.SparDay(3),
            "fighters spar: one bout in 25, one evening in three (4.8.2)");
        C(HarmSixMath.Consent(0, 5, false, false) && !HarmSixMath.Consent(-1, 5, false, false) && !HarmSixMath.Consent(10, 10, true, false), "only with consent: neither thinks ill of the other, neither worn down");
        C(Near(HarmSixMath.DareChance * 15f, 1f) && Near(HarmSixMath.TalkedOut, 0.5f) && Near(HarmSixMath.DareHurtChance, 1f / 3f),
            "brave to a fault: once a season, a cautious one talks them out half the time, one in three hurt (3.6.2)");
        C(HarmSixMath.DareOf(true, 5f) == DareKind.Swim && HarmSixMath.DareOf(true, 20f) == DareKind.Wrestle && HarmSixMath.DareOf(false, 0f) == DareKind.Wrestle,
            "a cold swim where there's water and it's cold, else wrestling the strongest");
        C(HarmSixMath.CowardFormed(0, -1) && HarmSixMath.CowardFormed(1, -2) && !HarmSixMath.CowardFormed(-1, -2) && !HarmSixMath.CowardFormed(0, 1),
            "a coward's name: the courage side newly a coward's (3.7.2)");
        C(Near(HarmSixMath.CowardChance, 1f) && Near(HarmSixMath.FuryChance, 1f / 3f), "once after the name forms; fury once a battle at most, one in three (3.8.2)");

        // ================= The registry =================

        var six = new[] { "2.1.2", "2.1.3", "1.5.2", "3.3.2", "3.4.2", "4.3.2", "4.4.1", "3.11.1", "3.11.2", "4.7.1", "2.6.3", "2.4.3", "2.10.2", "3.11.4", "4.5.2", "4.6.1", "4.6.2",
            "4.1.2", "4.2.1", "4.8.2", "3.6.2", "3.7.2", "3.8.2" };
        foreach (var id in six)
            C(HarmKinds.All.Count(k => !k.founding && k.label.Contains("(" + id + ")")) == 1, $"the proposal {id} is a kind of its own");
        var sixKinds = HarmKinds.All.Skip(Array.IndexOf(HarmKinds.All, HarmKinds.TamerBout) + 1).ToList();
        C(sixKinds.Count == 23 && sixKinds.All(k => !k.founding && k.weight > 0f && k.weight < 0.05f), "the twenty-three come after the rest, none founding, each a small share");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var mine = new Dictionary<string, string>
        {
            ["HarmSix"] = Code("Harm/HarmSix.cs"), ["AnswerHurts"] = Code("Harm/AnswerHurts.cs"), ["WorkSlips"] = Code("Harm/WorkSlips.cs"), ["WildHurts"] = Code("Harm/WildHurts.cs"),
            ["Contests"] = Code("Harm/Contests.cs")
        };
        foreach (var kv in mine)
        {
            C(!Regex.IsMatch(kv.Value, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|MakeHediff\("), $"{kv.Key}: nobody is hurt but through the harm service");
            C(!Regex.IsMatch(kv.Value, @"Sched\.(Rare|Hourly|Every)"), $"{kv.Key}: no clock of its own (the harm book's hour and slices)");
            C(!kv.Value.Contains("—") && !kv.Value.Contains("Tell.Letter(") && !kv.Value.Contains("LetterStack"), $"{kv.Key}: no em dashes, no letters");
        }
        string answers = mine["AnswerHurts"], slips = mine["WorkSlips"], wild = mine["WildHurts"], contests = mine["Contests"], harm = Code("Harm/Harm.cs");
        C(harm.Contains("HarmSix.Hourly(this, now);") && harm.Contains("HarmSix.Slice(this, slice, now);") && harm.Contains("HarmSix.Reset();"), "on the harm book's hour and its slices");
        C(Code("Harm/HarmFive.cs").Contains("default: return HarmSix.Due(x, now);") && Code("Harm/HarmFive.cs").Contains("default: return HarmSix.Lapsed(x, now);"),
            "what part 6 keeps waiting goes in the harm book's own list, no new saved collection");
        foreach (var system in new[] { "hotHeavy", "backTooSoon", "projectPush", "clumsyWorn", "makeshift", "takingDown", "hurriedHarvest", "strongBack", "sharpHabits", "secondDrink",
                     "handsSore", "droversBull", "tooClose", "dawnWalk", "boldWolf", "pokemonCatch", "pokemonStores", "bonfireSpark", "lineMuster", "magicSpar", "braveDare",
                     "cowardName", "vendettaFury" })
            C(harm.Contains("case \"" + system + "\":"), $"{system} sits under its own system's switch");
        C(Regex.IsMatch(harm, @"case ""strongBack"": return [^;]*Interplay\.On;") && Regex.IsMatch(harm, @"case ""handsSore"": return [^;]*Interplay\.On;"),
            "links between two systems behind Systems talk to each other");
        C(Regex.IsMatch(harm, @"case ""pokemonCatch"": return PokeWorldLink\.Active") && Regex.IsMatch(harm, @"case ""magicSpar"": return Contests\.MagicLoaded")
          && Regex.IsMatch(harm, @"case ""secondDrink"": return WorkSlips\.SecondDrinkOn"), "the soft ones only with their mods");
        // Hooked where each system lives, one line each.
        C(Code("PlayOut/PlayFire.cs").Contains("AnswerHurts.Lit(pawn, AnswerWork.Fire,") && Code("PlayOut/PlayDead.cs").Contains("AnswerHurts.Lit(pawn, AnswerWork.Pyre,")
          && answers.Contains("PlayKind.Haul") && answers.Contains("PlayHaul.OnIt(p, x)"), "hot and heavy: the fire laid, the pyre lit, the dead carried and the graves dug");
        C(Code("PlayOut/OnMapPlays.cs").Contains("AnswerHurts.Describe(sheet, Mode == PlayHaul.Pyre, true);") && Code("PlayOut/PlayOutEffects.cs").Contains("AnswerHurts.Describe(sheet, build && !woodPaid, false);"),
            "named under the answers that carry the dead out and lay a fire");
        string health = File.ReadAllText(Path.Combine(root, "Defs", "DilemmaDefs", "Council_Dilemmas_Health.xml"));
        C(Regex.IsMatch(health, @"<riskEffects>(?:(?!</riskEffects>).)*DilemmaEffect_BackTooSoonFall", RegexOptions.Singleline) && answers.Contains("CarriedThing == null"),
            "back too soon: under the answer's own risk, the fall when they're found carrying something");
        C(Code("Setbacks/SetbackHooks.cs").Contains("AnswerHurts.MakeshiftBill(worker, recipeDef, bench);") && answers.Contains("Avail.CanBuild(u, bench.Map)"),
            "the makeshift spot: from the bill, only while a better bench can be built");
        C(answers.Contains("[HarmonyPatch(typeof(JobDriver_Deconstruct), \"FinishedRemoving\")]") && answers.Contains("d.IsDoor"), "taking down: the game's own deconstruction, a wall or a door");
        C(Code("Missions/Workers_MissionRoad.cs").Contains("AnswerHurts.HandsBack(hands, share, m.place);") && answers.Contains("p.Map.IsPlayerHome"),
            "the hands: from the town's harvest, shown once they're home");
        C(Code("Ambitions/Ambitions.cs").Contains("public bool OwnWork(ThingDef thing)") && slips.Contains("a.OwnWork(def)") && slips.Contains("Driven(p, work)"),
            "the last push: the project's own building, not on top of a work decree's own roll");
        C(slips.Contains("Heart.BandOf(p) < StrainBand.Worn"), "clumsy: the heart's two high bands");
        C(slips.Contains("WorkTask.Harvest") && slips.Contains("FieldWeather.FrostMarked(plant)") && Code("Fields/FieldWeather.cs").Contains("public static bool FrostMarked(Plant plant)")
          && slips.Contains("WorkHurtKind.Field"), "hurried hands: the all hands, a frost harvest's marks, the harvest push");
        C(slips.Contains("Council_Quirk_StrongBack") && slips.Contains("Council_Quirk_Whittler") && slips.Contains("Council_Quirk_SharpensBlades") && slips.Contains("HabitKind.Whittle"),
            "the quirks by name, the whittling and the whetstone as they're done");
        C(Code("Integrations/BeforeWork.cs").Contains("if (r == Drink) WorkSlips.DrankBefore(p, drug);") && slips.Contains("entry.allowedForJoy") && slips.Contains("\"AlcoholHigh\""),
            "the second drink: from Alpha Skills' drink before work, only as the policy allows, the game's own drunkenness");
        C(wild.Contains("c.herd") && wild.Contains("HarmSixMath.HerdRange"), "the drovers' bull: a camp's own herd");
        string creatures = File.ReadAllText(Path.Combine(root, "Defs", "EncounterDefs", "Council_Encounters_Creatures.xml"));
        C(Regex.Matches(creatures, "DilemmaEffect_TooClose").Count == 3 && wild.Contains("manhunterOnTameFailChance") && wild.Contains("HurtKind.Bite"),
            "too close: three encounters, the game's taming revenge, a bite named for the animal");
        C(Code("Harm/Harm.cs").Contains("by?.def, DamageInfo.SourceCategory") && Code("Harm/HarmMath.cs").Contains("Bite"), "a bite carries what bites carry, by the ailments' own roll");
        C(wild.Contains("Council_Quirk_WalksFields") && wild.Contains("NightWalks.OutsideWalls") && wild.Contains("NightWalks.Hunt(predator, p, HarmKinds.DawnWalk)"),
            "the dawn walk: the quirk, beyond the walls, the game's own hunt");
        C(wild.Contains("rb.stance == \"fed\"") && wild.Contains("rb.Predator") && Code("Animals/ResidentBeast.cs").Contains("WildHurts.DescribeStance(ctx, sheet, stance);"),
            "the fed wolf: the beast that stays, a predator fed, named under the answer");
        C(wild.Contains("rb.stance != \"catch\"") && wild.Contains("playerSettings?.Master == trainer") && Code("Integrations/PokeWorldCamp.cs").Contains("WildHurts.DescribeCatch(sheet);"),
            "the catch: a partner at the trainer's side takes it, named under the answer");
        C(wild.Contains("Rimfolk_ShooVisitor") && wild.Contains("rb.pokemon"), "the visitor at the stores: a fed Pokémon, shooed");
        C(contests.Contains("PlayKind.Bonfire") && contests.Contains("FireUtility.TryStartFireIn") && contests.Contains("JobDefOf.BeatFire") && contests.Contains("WindSpeed"),
            "the spark: a bonfire, the game's own fire, beaten out with the game's own firefighting");
        C(contests.Contains("DecreeHurt.Muster") && contests.Contains("Rimfolk_Amb_HoldTheWayIn") && contests.Contains("DefenseLines.Of(map)") && contests.Contains("SkillDefOf.Shooting"),
            "the muster: the militia or the project, at the defense line, a little Shooting");
        C(contests.Contains("PeopleTags.Has(p, \"fighter\")") && contests.Contains("HarmSixMath.Consent(") && Code("Harm/DrillBouts.cs").Contains("Contests.BoutBlow(bout, attacker, defender);"),
            "the spar: A RimWorld of Magic's fighters, with consent, the drills' own bout");
        C(contests.Contains("Rimfolk_Norm_Brave") && contests.Contains("Rimfolk_Group_Bold") && contests.Contains("GroupRole.Worrier") && contests.Contains("Council_Ail_SprainedAnkle"),
            "the dare: a brave or bold group, a cautious one at hand, a sprain or a chill");
        C(Code("Social/Gossip/ReputationTracker.cs").Contains("Contests.CowardFormed(p);") && contests.Contains("p.Drafted") && contests.Contains("Council_Office_Marshal"),
            "the coward's name: from the name's forming, never while drafted, the marshal's word");
        C(contests.Contains("v.faction != battle.faction") && contests.Contains("furyLooked"), "the fury: the sworn people's fight, once a battle");
        // Messages only where the game gives one for the same hurt: an ailment caught, an animal turning on someone.
        C(Regex.Matches(answers, "TellHurt\\(").Count == 1 && Regex.Matches(slips, "TellHurt\\(").Count == 1 && Regex.Matches(wild, "TellHurt\\(").Count == 1
          && !contests.Contains("TellHurt("), "a message only for an ailment caught and an animal's charge");
        string dev = Code("Harm/HarmSixDebug.cs");
        C(!Regex.IsMatch(dev, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|Harm\.(Note|Injure|Ail|Fight)\(") && Regex.Matches(dev, @"\[DebugAction\(""Rimfolk"", ""Harm six: ").Count >= 23,
            "dev tools for each, through the feature itself, never past the cap");
        var jobs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmSix.xml"));
        foreach (var j in new[] { "Rimfolk_DawnWalk", "Rimfolk_ShooVisitor", "Rimfolk_MusterPost", "Rimfolk_DareWalk" })
            C(jobs.Contains("<defName>" + j + "</defName>"), $"the job {j}");
        C(Regex.Matches(jobs, "ColonyCouncil.JobDriver_HarmWalk").Count == 4 && Code("Harm/NightWalks.cs").Contains("HarmSix.Arrived(p, def)"), "each a walk out, a while there, an end");

        // The words.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HarmSix.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_HarmSix_HandsSore", "CC_HarmSix_StrongBack", "CC_HarmSix_DroversBull" })
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2") && keyed[key].Contains("{PAWN_") && keyed[key + "_2"].Contains("{PAWN_"), $"{key} has two wordings, naming who was hurt");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Impact_")).All(kv => !Regex.IsMatch(Regex.Replace(kv.Value, @"\{\d+\}", ""), @"\d")), "the sheets' lines are words, no numbers");
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var kv in mine)
            foreach (Match m in Regex.Matches(kv.Value, "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{kv.Key}: {k} exists");
            }
        foreach (var w in new[] { "Bench", "Hot", "Heavy", "Load", "Field" })
            C(allKeys.Contains("CC_HarmSix_Mote_Slip_" + w), $"a slip's few words at {w}");
        foreach (var a in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
            C(allKeys.Contains("CC_HarmSix_Mote_Sickle_" + a), $"the sickle by the age: {a}");
        foreach (var m in new[] { "Bowstring", "Powder", "Graze", "Charge" })
            C(allKeys.Contains("CC_HarmSix_Mote_Muster_" + m), $"the muster's mishap: {m}");
        C(allKeys.Contains("CC_HarmSix_Report_spar") && allKeys.Contains("CC_HarmSix_Report_dare"), "the bouts' reports");

        // Said over a head: one short sentence each, and every pool asked for with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        string linesFile = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_HarmSix_Lines.xml"));
        var asked = mine.Values.Concat(new[] { Code("Harm/DrillBouts.cs") }).SelectMany(v => Regex.Matches(v, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value)).Distinct()
            .Where(p => linesFile.Contains("<defName>" + p + "</defName>")).ToList();
        C(asked.Count >= 40, $"the new pools are asked for ({asked.Count})");
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
        C(Regex.Matches(review, @"\*Built \(harm, part 6").Count == 23, "the review marks the twenty-three built");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### Harm, part 6"), "DESIGN.md has them");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
