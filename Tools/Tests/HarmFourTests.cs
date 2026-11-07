using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Harm, part 4 (Harm/HarmFourMath.cs; docs/harm-review-proposals.md 1.1.1, 2.5.2, 2.2.3, 1.2.3, 4.3.1, 3.4.1, 4.7.2,
/// 4.5.3, 4.5.1, 4.9.1, 3.6.1, 1.4.2, 2.5.1, 2.8.1, 4.1.1): each rate set against the review's, each prevention easing it,
/// the ages' forms; and the mod's side: every hurt through the one service with its kind, hooked where its system lives,
/// named before the choice, seen on the map, messages only where the game gives one, said in one sentence.
/// </summary>
public static class HarmFourTests
{
    private const int D = HarmFourMath.DayTicks, H = HarmFourMath.HourTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm four: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= The physician catches what they tend (1.1.1) =================

        C(Near(HarmFourMath.PhysicianChance(false, false, false, false, 1), 1f / 40f), "one tending in 40 of a catching everyday ailment");
        C(Near(HarmFourMath.PhysicianChance(true, false, false, false, 1), 1f / 400f), "a serious one a tenth of that");
        C(Near(HarmFourMath.PhysicianChance(false, true, false, false, 1), 1f / 80f) && Near(HarmFourMath.PhysicianChance(false, false, true, false, 1), 1f / 160f)
          && Near(HarmFourMath.PhysicianChance(false, false, false, true, 1), 1f / 80f), "gloves and masks halve it, a sterile room quarters it, the quarantine or rest-when-ill decree halves it");
        C(Near(HarmFourMath.PhysicianChance(false, false, false, false, 2), 1f / 40f) && Near(HarmFourMath.PhysicianChance(false, false, false, false, 3), 1.5f / 40f),
            "a doctor run off their feet (a third tending of the sick in a day) half again: a second doctor sharing the rounds keeps under it");
        C(HarmFourMath.CaughtTicks(0f) == D && HarmFourMath.CaughtTicks(1f) == 2 * D, "it shows a day or two later");
        C(!HarmFourMath.OnsetLapsed(10 * D, 13 * D) && HarmFourMath.OnsetLapsed(10 * D, 13 * D + 1), "held back by the cap, it waits three days, then passes");

        // ================= The guest's cough (2.5.2) =================

        C(Near(HarmFourMath.GuestCoughVisit, 1f / 20f), "one visit in 20 brings a cough");
        C(HarmFourMath.GuestCoughCatch(0, 1f) == 0f && HarmFourMath.GuestCoughCatch(1, 1f) == 1f && Near(HarmFourMath.GuestCoughCatch(3, 0.25f), 0.25f),
            "the one who sat with them an hour or more catches it, unless a decree keeps the sick apart");
        C(HarmFourMath.CoughRange <= 5f && Near(HarmFourMath.GuestCoughDays, 1f), "near them means a few steps; it shows a day after they go");

        // ================= Caught in the hail (2.2.3) =================

        float hour = 1f - (float)Math.Pow(1f - HarmFourMath.HailHit, 10);
        C(hour > 0.25f && hour < 0.35f, $"about a third of an hour out under the falling hail brings a bruise ({hour:P0})");
        C(HarmFourMath.HailChance(true, false) == 0f && HarmFourMath.HailChance(false, true) == 0f && HarmFourMath.HailChance(false, false) > 0f, "a roof or a helmet turns it");
        C(HarmFourMath.HailMaxPerStorm == 2 && HarmFourMath.HailHigh <= 3f, "one or two a storm at most, a blunt hit of 1 to 3");

        // ================= A cold night for the frail (1.2.3) =================

        C(Near(HarmFourMath.ColdNightChance * 7f, 1f), "one chill per cold week for the frail");
        C(HarmFourMath.ColdNight(true, -6f, 3, true) && !HarmFourMath.ColdNight(false, -6f, 3, true) && !HarmFourMath.ColdNight(true, -4f, 3, true)
          && !HarmFourMath.ColdNight(true, -6f, 2, true) && !HarmFourMath.ColdNight(true, -6f, 5, false),
            "only under banked fires, in a hard frost, after three cold hours asleep, for the frail");

        // ================= A felling mishap (4.3.1) =================

        C(Near(HarmFourMath.Felling(4, WorldAge.Medieval), 1f / 300f) && HarmFourMath.Felling(5, WorldAge.Medieval) == 0f, "one tree in 300 for a cutter under Plants 5, none for the skilled");
        C(HarmFourMath.Felling(0, WorldAge.Tribal) > HarmFourMath.Felling(0, WorldAge.Medieval) && HarmFourMath.Felling(0, WorldAge.Industrial) < HarmFourMath.Felling(0, WorldAge.Medieval)
          && HarmFourMath.Felling(0, WorldAge.Spacer) < HarmFourMath.Felling(0, WorldAge.Industrial), "a stone axe more often, a steel one less");
        C(HarmFourMath.FellCrackShare <= 0.25f && HarmFourMath.FellBruiseHigh <= 6f, "a bruise on the leg, a crack now and then");

        // ================= A failed build that hurts (3.4.1) =================

        C(HarmFourMath.HeavyOf(true, false, 1, 0f) == HeavyBuild.Wall && HarmFourMath.HeavyOf(false, true, 1, 0f) == HeavyBuild.Bridge
          && HarmFourMath.HeavyOf(false, false, 2, 5f) == HeavyBuild.Furniture && HarmFourMath.HeavyOf(false, false, 1, 25f) == HeavyBuild.Furniture
          && HarmFourMath.HeavyOf(false, false, 1, 3f) == HeavyBuild.None, "heavy work: a wall, a bridge, furniture of two cells or a heavy piece");
        C(Near(HarmFourMath.FailedBuild(HeavyBuild.Wall), 0.1f) && HarmFourMath.FailedBuild(HeavyBuild.None) == 0f, "one failure in ten on heavy work, none on light");

        // ================= Burned at the bench (4.7.2) =================

        C(Near(HarmFourMath.HotBenchChance, 0.01f) && HarmFourMath.HotWork("Cooking") && HarmFourMath.HotWork("Smithing") && !HarmFourMath.HotWork("Crafting"),
            "one drink in 100 before hot work (the stove, the forge)");
        C(HarmFourMath.HotBenchKept(0, 3 * H) && !HarmFourMath.HotBenchKept(0, 4 * H) && !HarmFourMath.HotBenchKept(5 * H, 4 * H), "the drink's mark lasts four hours");

        // ================= The rut in the pens (4.5.3) =================

        C(HarmFourMath.Ruts(true, true, false, true, 1f) && !HarmFourMath.Ruts(false, true, false, true, 1f) && !HarmFourMath.Ruts(true, true, true, true, 1f)
          && !HarmFourMath.Ruts(true, false, false, true, 1f) && !HarmFourMath.Ruts(true, true, false, true, 0.5f), "a grown male of a herd's grazers (never a predator, never a small one)");
        C(HarmFourMath.RutSeason(3, 0) && !HarmFourMath.RutSeason(1, 2) && HarmFourMath.RutSeason(5, 2) && !HarmFourMath.RutSeason(5, 1), "the autumn rut, or the third quadrum where there are no seasons");
        C(HarmFourMath.RutCharge(true, 3) == 0f && Near(HarmFourMath.RutCharge(false, 3), 1f / 20f) && Near(HarmFourMath.RutCharge(false, 12), 1f / 40f),
            "never the herdmaster, half for a skilled handler");
        float rut = 15f * HarmFourMath.RutChargeHour * HarmFourMath.RutLands;
        C(rut > 0.2f && rut < 0.6f, $"about one charge a rut for a pen people go into, half of them landing ({rut:0.00} hurts for fifteen hours)");

        // ================= The herdmaster's lesson (4.5.1) =================

        C(Near(HarmFourMath.LessonKickChance, 1f / 30f), "one lesson in 30");
        C(HarmFourMath.Nervous(0.1f, true) && HarmFourMath.Nervous(0.5f, false) && !HarmFourMath.Nervous(0.1f, false), "only a nervous animal: wild at heart or timid");
        C(HarmFourMath.ChildHigh <= 3f, "a child takes only the mildest bruise");

        // ================= A fall from the saddle (4.9.1) =================

        C(Near(HarmFourMath.SaddleThrow(3, false), 1f / 50f) && HarmFourMath.SaddleThrow(6, false) == 0f && HarmFourMath.SaddleThrow(2, true) == 0f,
            "one ride in 50 for a rider short of Animals 6 on a mount not yet trained to obey");

        // ================= The groups come to blows (3.6.1) =================

        C(HarmFourMath.GroupBrawl(59f, true, false, false) == 0f && HarmFourMath.GroupBrawl(70f, false, false, false) == 0f, "only bitter groups, only at a table or a gathering");
        C(Near(HarmFourMath.GroupBrawl(70f, true, false, false), 1f / 8f) && Near(HarmFourMath.GroupBrawl(70f, true, true, true), 1f / 32f), "half with the peacemaker, half with shared meals");
        float year = 6f * HarmFourMath.GroupBrawlChance * 2f;
        C(year >= 1f && year <= 2f, $"once or twice a year where two groups are bitter (six digs at a table, {year:0.0} hurts)");
        C(HarmFourMath.GroupJoins(5, 5) == 2 && HarmFourMath.GroupJoins(1, 3) == 1 && HarmFourMath.GroupJoins(0, 3) == 0, "two or three pairs at once");
        C(Enum.GetValues(typeof(WorldAge)).Cast<WorldAge>().Select(HarmFourMath.GroupsForm).Distinct().Count() == 4, "the two lots by the age: clans, factions, shifts, departments");

        // ================= The sore loser (1.4.2) =================

        C(HarmFourMath.CloseRace(5, 4, 9) && HarmFourMath.CloseRace(12, 9, 21) && !HarmFourMath.CloseRace(12, 6, 18), "a close race: a vote in it, or a fifth of the votes");
        C(Near(HarmFourMath.SoreLoser(true, true, false, false), 0.1f) && Near(HarmFourMath.SoreLoser(true, false, true, true), 0.05f) && HarmFourMath.SoreLoser(true, false, false, false) == 0f
          && HarmFourMath.SoreLoser(false, true, true, false) == 0f, "one loss in ten among the hot-tempered or bitter, half for a gracious winner, none for a calm loser");
        C(HarmFourMath.SoreTicks(0f) == D && HarmFourMath.SoreTicks(1f) == 3 * D && Near(HarmFourMath.SoreBlows, 0.5f), "one to three days after, blows half the time");

        // ================= A guest's night goes sour (2.5.1) =================

        C(Near(HarmFourMath.GuestNight(0, false, false, false, false), 1f / 15f) && HarmFourMath.GuestNight(4, true, false, false, true) == 0f, "one guest night in 15, never a child");
        C(Near(HarmFourMath.GuestNight(0, true, false, false, false), 1.5f / 15f) && Near(HarmFourMath.GuestNight(0, false, true, true, false), 0.25f / 15f),
            "half again after a drink at supper; the warden of guests and the best-behavior decree half each");
        C(HarmFourMath.GuestNight(4, false, false, false, false) <= 2f / 15f + 0.0001f, "a hot temper at most twice the rate, as a feast's guest");
        C(HarmFourMath.GuestEvening(19) && HarmFourMath.GuestEvening(22) && !HarmFourMath.GuestEvening(18) && !HarmFourMath.GuestEvening(23), "after supper");

        // ================= The rematch (2.8.1) =================

        C(Near(HarmFourMath.RematchChance, 1f / 3f) && HarmFourMath.RematchKept(0, 59 * D) && !HarmFourMath.RematchKept(0, 60 * D), "one in three within the year");
        C(!HarmFourMath.RematchDue(0, 2 * D, true) && !HarmFourMath.RematchDue(0, 5 * D, false) && HarmFourMath.RematchDue(0, 3 * D, true), "on a later visit only");
        C(HarmFourMath.TakesRematch(1, 2, 9) && HarmFourMath.TakesRematch(0, 9, 7) && !HarmFourMath.TakesRematch(-1, 4, 8), "a hot temper takes it; a calm one only as the better fighter");

        // ================= Games with real contests (4.1.1) =================

        float games = HarmFourMath.GamesDayHurts(HarmFourMath.GamesPairs, HarmFourMath.GamesRunners, 0.8f, WorldAge.Medieval);
        C(games > 0.35f && games < 0.6f, $"about one small hurt in two games days ({games:0.00} a day)");
        C(HarmFourMath.RaceSprain(false, WorldAge.Tribal) == 0f && HarmFourMath.RaceSprain(true, WorldAge.Spacer) == 0f && Near(HarmFourMath.RaceSprain(true, WorldAge.Industrial), 0.125f),
            "rough ground only (a floor is safe), and a spacer's sim tourney has nothing to fear but a wrenched wrist");
        C(Near(HarmFourMath.GamesBlowChance, 1f / 25f) && HarmFourMath.ScrapeHigh <= 2f, "a bout's real blow one in 25; a child only scrapes a knee");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var mine = new Dictionary<string, string>
        {
            ["HarmFour"] = Code("Harm/HarmFour.cs"), ["Catching"] = Code("Harm/Catching.cs"), ["Mishaps"] = Code("Harm/Mishaps.cs"), ["AnimalHurts"] = Code("Harm/AnimalHurts.cs"),
            ["Quarrels"] = Code("Harm/Quarrels.cs"), ["GamesDay"] = Code("Harm/GamesDay.cs")
        };
        foreach (var kv in mine)
        {
            C(!Regex.IsMatch(kv.Value, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|MakeHediff\("), $"{kv.Key}: nobody is hurt but through the harm service");
            C(!Regex.IsMatch(kv.Value, @"Sched\.(Rare|Hourly|Every)"), $"{kv.Key}: no clock of its own (the harm book's hour and slices)");
            C(!kv.Value.Contains("—") && !kv.Value.Contains("Tell.Letter("), $"{kv.Key}: no em dashes, no letters");
        }
        string catching = mine["Catching"], mishaps = mine["Mishaps"], animals = mine["AnimalHurts"], quarrels = mine["Quarrels"], gamesDay = mine["GamesDay"], hub = mine["HarmFour"];
        C(Code("Harm/Harm.cs").Contains("HarmFour.Hourly(this, now);") && Code("Harm/Harm.cs").Contains("HarmFour.Slice(this, slice, now);"), "on the harm book's hour and its slices");
        C(Code("Health/HealthPatches.cs").Contains("Catching.Tended(doctor, patient);") && catching.Contains("Harm.Roll(doctor, HarmKinds.Physician, chance"), "a tending's one roll for the doctor");
        C(catching.Contains("Council_Decree_Quarantine") && catching.Contains("RoomStatDefOf.InfectionChanceFactor") && catching.Contains("Ages.Ours >= WorldAge.Industrial"),
            "a sterile room, gloves and masks, the decrees ease it");
        C(catching.Contains("Harm.Chance(HarmKinds.GuestCough, HarmFourMath.GuestCoughVisit)") && catching.Contains("DecreeWork.ContagionFactorFor(c)"), "a visit's cough, the quarantine decree");
        C(catching.Contains("Council_Decree_BankedFires") && catching.Contains("ComfortableTemperatureRange().min") && catching.Contains("AilmentTracker.Vulnerable(p)"),
            "a cold night: the decree, a bedroom below comfort, the frail");
        string decrees = File.ReadAllText(Path.Combine(root, "Defs", "DecreeDefs", "Council_Decrees3.xml"));
        C(decrees.Contains("<hurts>ColdNights</hurts>") && Code("Harm/HarmMath.cs").Contains("ColdNights"), "named on the banked fires decree's card");
        C(Code("Happenings/Workers_Nature.cs").Contains("Mishaps.HailStone(map, pawn, bruised)") && mishaps.Contains("HurtClass.Mild"), "the hail's stones in the storm's own look, the mildest hurt");
        C(mishaps.Contains("[HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]") && mishaps.Contains("PlantDestructionMode.Chop") && mishaps.Contains("HarmFourMath.Felling(plants"),
            "a felled tree, by the cutter's skill and the colony's means");
        C(mishaps.Contains("[HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]") && mishaps.Contains("HarmFourMath.FailedBuild(heavy)"), "a failed build on heavy work");
        string before = Code("Integrations/BeforeWork.cs");
        C(before.Contains("HotBench.Barred(work)") && before.Contains("Mishaps.DrankBeforeHotWork(p)") && mishaps.Contains("drinkBeforeHotWork"), "a drink before hot work, under its own switch");
        C(Code("CouncilSettings.cs").Contains("public bool drinkBeforeHotWork = true;") && Code("CouncilSettings.cs").Contains("\"drinkBeforeHotWork\", true")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"drinkBeforeHotWork\""), "the new switch, on by default, saved, on the settings page");
        C(animals.Contains("AnimalPenUtility.GetCurrentPenOf(") && animals.Contains("ContainsConnectedRegion(region)") && animals.Contains("AnimalFacts.IsHerdmaster(p)"),
            "the rut: in his own pen, never the herdmaster");
        C(animals.Contains("PlayKind.Lesson") && animals.Contains("WorkTypeDefOf.Handling") && animals.Contains("Temper.Timid"), "the lesson: the herdmaster handling a nervous animal");
        C(animals.Contains("EndsWith(\"Mounted\"") && animals.Contains("TrainableDefOf.Obedience") && animals.Contains("AnimalFacts.GiddyUp"), "the saddle: Giddy-Up's mounted job, by name; a mount trained to obey");
        C(Code("Social/Groups/GroupMoments.cs").Contains("Quarrels.Dig(a, b, ga, gb);") && quarrels.Contains("Council_Decree_SharedMeals"), "the groups' brawl at a dig, the shared meals decree");
        C(Code("Offices/OfficesElection.cs").Contains("Quarrels.Counted(count, atTable == null);") && Code("Offices/OfficesElection.cs").Contains("Rimfolk_Pool_GraciousWinner"),
            "the sore loser from the count, the gracious winner's handshake at it");
        C(quarrels.Contains("Outsiders.FeastBrawl(c, x)") && quarrels.Contains("GuestBeds.IsGuestBed(") && quarrels.Contains("Council_Decree_Manners") && quarrels.Contains("Interplay.Warden"),
            "a guest's night: the outsiders' own fistfight, Hospitality's guest beds, the warden and the best-behavior decree");
        C(Code("Visitors/VisitEffects.cs").Contains("if (inside) Quarrels.Describe(sheet);"), "named before the choice: the answer that gives a bed for the night");
        string outcomes = Code("Outsiders/OutsiderOutcomes.cs");
        C(outcomes.Contains("Quarrels.BoutOver(bout);") && Regex.Matches(outcomes, @"Quarrels\.Eased\(c, x\)").Count == 2 && quarrels.Contains("DuelTerms.FirstBlood"),
            "the rematch: from a bout's end, settled by a good word or a gift, to first blood");
        C(Code("Calendar/CalendarKeeping.cs").Contains("GamesDay.Kept(map, together ?? d.label);") && gamesDay.Contains("DrillBouts.Begin(map, a, b, ra, rb, false, false, true)"),
            "the games' contests from the kept day's gathering, its bout the drills' own");
        C(Code("Harm/DrillBouts.cs").Contains("bout.games ? HarmKinds.Games : HarmKinds.Drills"), "a games bout's blow is the games' kind");
        // Messages only where the game gives one for the same hurt: an illness caught, an animal turning on its handler.
        C(catching.Contains("HarmFour.TellHurt(") && animals.Contains("\"CC_HarmFour_Rut\"") && animals.Contains("CC_HarmFour_LessonKick"), "an illness caught and an animal's revenge are told");
        C(!mishaps.Contains("TellHurt(") && !gamesDay.Contains("TellHurt(") && !quarrels.Contains("TellHurt(") && !mishaps.Contains("Tell.Message("), "a bruise, a burn, a fall, a fight: no message of Rimfolk's own");
        string dev = Code("Harm/HarmFourDebug.cs");
        C(!Regex.IsMatch(dev, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|Harm\.(Note|Injure|Ail|Fight)\(") && Regex.Matches(dev, @"\[DebugAction\(""Rimfolk"", ""Harm four: ").Count == 17,
            "seventeen dev tools (the budget's log and one or more for each of the fifteen), each through the feature itself, never past the cap");

        // The words.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HarmFour.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_HarmFour_Physician", "CC_HarmFour_GuestCough", "CC_HarmFour_ColdNight", "CC_HarmFour_Rut", "CC_HarmFour_LessonKick", "CC_HarmFour_LessonBite" })
            C(keyed.ContainsKey(key) && keyed.ContainsKey(key + "_2") && keyed[key].Contains("{PAWN_") && keyed[key + "_2"].Contains("{PAWN_"), $"{key} has two wordings, naming who was hurt");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Impact_")).All(kv => !Regex.IsMatch(kv.Value, @"\d")), "the sheet's line is words, no numbers");
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
            C(keyed.ContainsKey("CC_HarmFour_Mote_Groups_" + HarmFourMath.GroupsForm(age)), $"the groups' brawl has its {age} words");
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var kv in mine.Concat(new[] { new KeyValuePair<string, string>("DrillBouts", Code("Harm/DrillBouts.cs")) }))
            foreach (Match m in Regex.Matches(kv.Value, "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{kv.Key}: {k} exists");
            }
        C(allKeys.Contains("CC_HarmFour_Mote_Groups_Tribal") && allKeys.Contains("CC_Harm_Decree_ColdNights"), "the age's words and the decree's line");

        // Said over a head: one short sentence each, and every pool asked for with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        var asked = mine.Values.Concat(new[] { Code("Harm/DrillBouts.cs"), Code("Offices/OfficesElection.cs") })
            .SelectMany(v => Regex.Matches(v, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value)).Distinct()
            .Where(p => File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_HarmFour_Lines.xml")).Contains("<defName>" + p + "</defName>")).ToList();
        C(asked.Count >= 29, $"the new pools are asked for ({asked.Count})");
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

        // The texts that said nobody is hurt say what happens now.
        string hail = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Council_WorldEvents_Misc.xml")) + File.ReadAllText(Path.Combine(root, "Defs", "IncidentDefs", "Council_Events_Nature.xml"));
        C(!hail.Contains("The hail won't injure anyone") && !hail.Contains("It won't hurt anyone and won't last long") && hail.Contains("a bruise from"), "the hail's words say a stone can bruise");
        string calendar = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Calendar.xml"));
        C(Regex.IsMatch(calendar, @"<CC_Cal_Way_Games_Letter>[^<]*turned ankle[^<]*</CC_Cal_Way_Games_Letter>"), "the games day's letter names its contests and their hurts before the choice");
        string outsiders = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Outsiders.xml"));
        C(Regex.IsMatch(outsiders, @"<CC_Out_TermsTitle>[^<]*rematch[^<]*</CC_Out_TermsTitle>"), "the duel's terms name the rematch before the choice");
        C(File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmFour.xml")).Contains("ColonyCouncil.JobDriver_GamesRace"), "the race's job");
        string review = File.ReadAllText(Path.Combine(root, "docs", "harm-review-proposals.md"));
        C(Regex.Matches(review, @"\*Built \(harm, part 4").Count == 15, "the review marks the fifteen built");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### Harm, part 4"), "DESIGN.md has the fifteen");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
