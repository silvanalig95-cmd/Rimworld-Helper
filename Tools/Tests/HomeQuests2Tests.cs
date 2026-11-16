using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The seven quests at home of round 2 (the player's "great, do some more", 16 November 2026: docs/quests-proposals.md Q3,
/// Q4, Q6, Q8, Q11, Q12 and Q13): the pure rules (Missions/HomeQuests2Math.cs: the reward, the days, the hours and the odds,
/// what counts as done), their share of the missions' one draw, and the defs and code: every age's words and the Empire's,
/// plain words, an ending for every way each can end, everything on the colony's own map, the game's own training, quality,
/// illness, fight, cold, research bench and party, the camp's night within the shared harm budget, and the dev actions.
/// </summary>
public static class HomeQuests2Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "quests at home, round 2: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        bool Plain(string t)
        {
            string body = Regex.Replace(t ?? "", @"^\[[^\]]*\]\s*", "");
            return !body.Contains('—') && !body.Contains('!');
        }
        const int Hour = 2500;

        // ================= The reward and the days =================

        int T = HomeQuestsMath.Tribal, M = HomeQuestsMath.Medieval, S = HomeQuestsMath.Spacer;
        foreach (HomeQuest2 q in Enum.GetValues(typeof(HomeQuest2)))
        {
            HomeQuests2Math.Base(q, out float min, out float max);
            int lo = HomeQuests2Math.Reward(q, M, false, 1f, 0f), hi = HomeQuests2Math.Reward(q, M, false, 1f, 1f);
            C(min >= 60f && max > min && max <= 240f && lo == HomeQuestsMath.Tens(min) && hi == HomeQuestsMath.Tens(max) && lo % 10 == 0, $"{q}: its own range, {lo} to {hi} silver from a medieval people, rounded to tens");
            C(HomeQuests2Math.Reward(q, T, false, 1f, 0.5f) < HomeQuests2Math.Reward(q, S, false, 1f, 0.5f) && HomeQuests2Math.Reward(q, M, true, 1f, 0.5f) == HomeQuests2Math.Reward(q, S, false, 1f, 0.5f),
                $"{q}: by the people's age (round 1's scale), the Empire as a spacer people");
            C(HomeQuests2Math.Reward(q, M, false, 9f, 1f) == HomeQuests2Math.Reward(q, M, false, 2f, 1f) && HomeQuests2Math.Reward(q, M, false, 0.1f, 0f) == HomeQuests2Math.Reward(q, M, false, 0.6f, 0f),
                $"{q}: the colony's wealth counts between 0.6 and 2");
            C(HomeQuests2Math.Goodwill(q) >= 8 && HomeQuests2Math.Goodwill(q) <= 12 && HomeQuests2Math.Penalty(q) >= 4 && HomeQuests2Math.Penalty(q) <= 8, $"{q}: goodwill for it, a cost for failing it");
            int d0 = HomeQuests2Math.Days(q, 0f), d1 = HomeQuests2Math.Days(q, 0.9999f);
            C(d0 >= 1 && d1 <= 14 && d0 <= d1, $"{q}: whole days, {d0} to {d1}");
            C(HomeQuests2Math.KindOf(q) == HomeQuests2Math.Kinds[(int)q], $"{q}: its kind");
        }
        C(HomeQuests2Math.Kinds.Length == 7 && HomeQuests2Math.Kinds.Distinct().Count() == 7 && !HomeQuests2Math.Kinds.Intersect(HomeQuestsMath.Kinds).Any(),
            "seven kinds of their own, none of round 1's");
        C(HomeQuests2Math.Days(HomeQuest2.Trainee, 0f) == 3 && HomeQuests2Math.Days(HomeQuest2.Trainee, 0.9999f) == 5 && HomeQuests2Math.Days(HomeQuest2.CampGuard, 0.5f) == 1
          && HomeQuests2Math.Days(HomeQuest2.Musicians, 0.5f) == 1 && HomeQuests2Math.Days(HomeQuest2.SeedGrain, 0f) == 10 && HomeQuests2Math.Days(HomeQuest2.WeddingPiece, 0f) == 6,
            "the young fighter stays three to five days, the caravan and the musicians one night, the seed ten days at least, the wedding six days off at least");

        // ================= Q3. A young fighter trained =================

        C(HomeQuests2Math.TrainHoursPerDay == 2 && HomeQuests2Math.TrainTarget(4) == 8 && HomeQuests2Math.TrainTarget(0) == 2, "two hours a day of the stay");
        C(HomeQuests2Math.TrainHour(17) && HomeQuests2Math.TrainHour(HomeQuests2Math.TrainFrom) && !HomeQuests2Math.TrainHour(HomeQuests2Math.TrainTo) && !HomeQuests2Math.TrainHour(10)
          && HomeQuests2Math.TrainFrom <= HarmMath.DrillFrom && HomeQuests2Math.TrainTo >= HarmMath.DrillTo, "in the evening, around the colony's own drill hours");
        C(HomeQuests2Math.TrainOutcome(8 * Hour, 8, Hour) == TrainEnd.Full && HomeQuests2Math.TrainOutcome(4 * Hour, 8, Hour) == TrainEnd.Half
          && HomeQuests2Math.TrainOutcome(4 * Hour - 1, 8, Hour) == TrainEnd.Little, "all the hours, half of them, or less");
        C(HomeQuests2Math.TrainPay(200, TrainEnd.Full) == 200 && HomeQuests2Math.TrainPay(200, TrainEnd.Half) == 100 && HomeQuests2Math.TrainPay(200, TrainEnd.Little) == 0, "half the pay for half the hours");
        C(HomeQuests2Math.Shoots(true, true, 8, 8) && !HomeQuests2Math.Shoots(false, true, 12, 3) && !HomeQuests2Math.Shoots(true, false, 12, 3) && !HomeQuests2Math.Shoots(true, true, 5, 9),
            "at the target when the colony has one, the young one has a ranged weapon and its best shot is as good as its best fighter; else sparring");
        C(HomeQuests2Math.TraineeLevel(10, 8) == 5 && HomeQuests2Math.TraineeLevel(2, 12) == 2 && HomeQuests2Math.TraineeLevel(5, 1) == 0, "the young one comes knowing less than the teacher");
        C(HomeQuests2Math.TraineeXpPerTick > HomeQuests2Math.TrainerXpPerTick && HomeQuests2Math.TraineeXpPerTick <= 0.2f && HomeQuests2Math.TrainerXpPerTick >= 0.03f,
            "both learn about what the game gives for research, the one taught more");

        // ================= Q4. A piece for a wedding =================

        C(HomeQuests2Math.MinQuality == 3 && HomeQuests2Math.GoodEnough(3) && HomeQuests2Math.GoodEnough(6) && !HomeQuests2Math.GoodEnough(2), "good quality or better (the game's own quality)");
        int piece = HomeQuests2Math.PiecePay(100f, M, false, 1f, 0f);
        C(piece == HomeQuestsMath.Tens(100f * HomeQuests2Math.GoodPriceFactor * 1.5f + HomeQuests2Math.Reward(HomeQuest2.WeddingPiece, M, false, 1f, 0f)) && piece > 100f * 1.25f,
            $"the piece paid well over its worth at good quality ({piece} for a piece worth 100)");

        // ================= Q6. A sick animal tended =================

        C(HomeQuests2Math.Illness(10, 0.1f) == HomeQuests2Math.Plague && HomeQuests2Math.Illness(3, 0.1f) == HomeQuests2Math.Flu && HomeQuests2Math.Illness(10, 0.9f) == HomeQuests2Math.Thin
          && HomeQuests2Math.Illness(3, 0.5f) == HomeQuests2Math.Flu, "the plague only with a doctor good enough for it; flu most often; sometimes underfed");
        C(new[] { HomeQuests2Math.Flu, HomeQuests2Math.Plague, HomeQuests2Math.Thin }.All(i => HomeQuests2Math.Severity(i) > 0.1f && HomeQuests2Math.Severity(i) < 0.5f),
            "the illness well under way, never near its end");
        C(HomeQuests2Math.SickOutcome(true, true, true, false, false) == SickEnd.Died && HomeQuests2Math.SickOutcome(false, false, false, false, false) == SickEnd.Lost
          && HomeQuests2Math.SickOutcome(false, true, false, false, false) == SickEnd.Well && HomeQuests2Math.SickOutcome(false, true, false, true, false) == SickEnd.Bonded
          && HomeQuests2Math.SickOutcome(false, true, true, false, true) == SickEnd.StillSick && HomeQuests2Math.SickOutcome(false, true, true, false, false) == SickEnd.Sick,
            "the animal: dead, gone, well (bonded or not), still sick when the days are up, else sick");

        // ================= Q8. Guard their camp for a night =================

        C(HomeQuests2Math.AttackChance > 0f && HomeQuests2Math.AttackChance <= 0.4f && HomeQuests2Math.RaiderChance > 0.3f && HomeQuests2Math.RaiderChance < 1f,
            "something comes one night in three at most, raiders or predators");
        C(HomeQuests2Math.AttackHour(0f) == 22 && HomeQuests2Math.AttackHour(0.9999f) == 26 && HomeQuests2Math.CampHour == 18 && HomeQuests2Math.DawnHour == 6,
            "camp from six, what comes between ten and three, gone at dawn");
        C(HomeQuests2Math.AttackPoints(0f) == 60f && HomeQuests2Math.AttackPoints(10000f) == 300f && HomeQuests2Math.Attackers(300f, 100f) == 3 && HomeQuests2Math.Attackers(60f, 200f) == 1
          && HomeQuests2Math.Attackers(5000f, 10f) == 3, "a small band at most, scaled to the colony");
        C(HomeQuests2Math.CampOutcome(0, false) == NightEnd.Quiet && HomeQuests2Math.CampOutcome(0, true) == NightEnd.Defended && HomeQuests2Math.CampOutcome(1, true) == NightEnd.Lost,
            "a quiet night, a night defended, or one of theirs dead");
        C(HomeQuests2Math.CampPay(200, NightEnd.Quiet) == 200 && HomeQuests2Math.CampPay(200, NightEnd.Defended) == 250 && HomeQuests2Math.CampPay(200, NightEnd.Lost) == 0,
            "a quarter more for a night fought through");
        C(HomeQuests2Math.CampMin >= 25f, "camped at the edge of the land, well away from the colony");

        // ================= Q11. Seed grain kept cold =================

        C(HomeQuests2Math.Cold(0f) && HomeQuests2Math.Cold(-10f) && !HomeQuests2Math.Cold(0.5f) && HomeQuests2Math.ColdMax == 0f, "cold is freezing or colder");
        C(HomeQuests2Math.SeedCount(75, 0f) == 50 && HomeQuests2Math.SeedCount(75, 0.9999f) == 75 && HomeQuests2Math.SeedCount(200, 0.9999f) == 75 && HomeQuests2Math.SeedCount(40, 0.5f) == 40,
            "50 to 75 of it, never more than one stack");
        C(HomeQuests2Math.SeedOutcome(true, 0, 60, false, 0, false) == SeedEnd.Lost && HomeQuests2Math.SeedOutcome(false, 59, 60, false, 0, false) == SeedEnd.Eaten
          && HomeQuests2Math.SeedOutcome(false, 60, 60, true, 0, false) == SeedEnd.Spoiled && HomeQuests2Math.SeedOutcome(false, 60, 60, false, HomeQuests2Math.WarmHoursMax + 1, false) == SeedEnd.Warm
          && HomeQuests2Math.SeedOutcome(false, 60, 60, false, HomeQuests2Math.WarmHoursMax, true) == SeedEnd.Kept && HomeQuests2Math.SeedOutcome(false, 70, 60, false, 0, false) == SeedEnd.Keeping,
            "gone, some used, rotten, too many warm hours, else kept at the sowing");
        C(HomeQuests2Math.WarmHoursMax == 12 && HomeQuests2Math.CarryHours == 6, "twelve warm hours in all, six to carry it in (the terms say so)");

        // ================= Q12. Their book copied =================

        C(HomeQuests2Math.CopyHours(0f) == 10 && HomeQuests2Math.CopyHours(0.9999f) == 16, "ten to sixteen hours of work at the bench");
        C(HomeQuests2Math.CopyXpPerTick == 0.1f, "the game's own learning for research");
        C(HomeQuests2Math.CopyOutcome(10 * Hour, 10, Hour, false) == CopyEnd.Done && HomeQuests2Math.CopyOutcome(5 * Hour, 10, Hour, false) == CopyEnd.Copying
          && HomeQuests2Math.CopyOutcome(5 * Hour, 10, Hour, true) == CopyEnd.Half && HomeQuests2Math.CopyOutcome(4 * Hour, 10, Hour, true) == CopyEnd.Little,
            "done, still copying, half done or less when the scholar leaves");
        C(HomeQuests2Math.BookTag(T, false) == "stories" && HomeQuests2Math.BookTag(M, false) == "learning" && HomeQuests2Math.BookTag(S, false) == "manual" && HomeQuests2Math.BookTag(S, true) == "learning",
            "the book by their age: their stories, their learning, a manual; the Empire's learning");

        // ================= Q13. Musicians for a night =================

        C(HomeQuests2Math.Musicians(0f) == 2 && HomeQuests2Math.Musicians(0.9f) == 3, "two or three of them");
        C(HomeQuests2Math.ArriveHour < HomeQuests2Math.PlayFrom && HomeQuests2Math.PlayFrom < HomeQuests2Math.PartyBy && HomeQuests2Math.PartyBy < HomeQuests2Math.PlayUntil
          && HomeQuests2Math.MorningHour < HomeQuests2Math.ArriveHour, "they come in the afternoon, play in the evening and leave in the morning");
        C(HomeQuests2Math.MusicGoodwill(8, true) == 8 && HomeQuests2Math.MusicGoodwill(8, false) == 4, "a night without a bed for one of them, half the goodwill");
        C(Math.Abs(HomeQuests2Math.HoursUntil(17f, 18, 0f) - 1f) < 0.001f && Math.Abs(HomeQuests2Math.HoursUntil(19f, 18, 0f) - 23f) < 0.001f && Math.Abs(HomeQuests2Math.HoursUntil(17.9f, 18, 0.25f) - 24.1f) < 0.01f,
            "the next hour of the clock, at least so long from now");

        // ================= Their share of the missions' one draw =================

        string file = Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_Home2.xml");
        var defs = XDocument.Load(file).Root!.Elements("IncidentDef").ToList();
        var quests = defs.Where(d => F((string)d.Element("baseChance")) > 0f).ToList();
        C(quests.Count == 7, $"seven quests drawn by the storyteller ({quests.Count})");
        C(quests.All(d => F((string)d.Element("baseChance")) == HomeQuests2Math.BaseChance && int.Parse((string)d.Element("minRefireDays")) == HomeQuests2Math.RestDays)
          && HomeQuests2Math.BaseChance == HomeQuestsMath.BaseChance, "each a small share of the draw, round 1's own, and not again for a season");
        float total = 0f;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
                if (((string)d.Element("workerClass") ?? "").Contains("HappeningWorker_Mission")) total += F((string)d.Element("baseChance") ?? "0");
        C(Math.Abs(NeighboursFourMath.PoolFactor(total) * total - NeighboursFourMath.MissionPoolWeight) < 0.001f && Math.Abs(quests.Count * HomeQuests2Math.BaseChance - 2.1f) < 0.001f,
            $"they take 2.1 of the one draw, and the missions still weigh {NeighboursFourMath.MissionPoolWeight} in all ({total:0.0} scaled): no more missions than before");
        var allNames = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("IncidentDef")).Select(d => (string)d.Element("defName")).ToList();
        C(defs.All(d => allNames.Count(x => x == (string)d.Element("defName")) == 1), "every def name its own");
        var kindsElsewhere = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Descendants("kind")).Select(k => k.Value).ToList();
        C(!HomeQuests2Math.Kinds.Intersect(kindsElsewhere).Any(), "no mission elsewhere goes by one of their kinds (each rests on its own)");

        // ================= The defs =================

        var code3 = Code("Missions/Workers_MissionHome3.cs");
        var code4 = Code("Missions/Workers_MissionHome4.cs");
        var shared = Code("Missions/HomeQuests2.cs");
        var code = code3 + code4;
        var outcomesOf = new Dictionary<string, (string[] done, string[] fail)>
        {
            ["HappeningWorker_MissionTrainee"] = (new[] { "trained", "half" }, new[] { "untrained", "died", "harmed", "ended" }),
            ["HappeningWorker_MissionWeddingPiece"] = (new[] { "delivered" }, new[] { "missed", "ended" }),
            ["HappeningWorker_MissionSickAnimal"] = (new[] { "well", "stillsick", "bonded" }, new[] { "died", "lost", "ended" }),
            ["HappeningWorker_MissionCampGuard"] = (new[] { "quiet", "defended" }, new[] { "lost", "ended" }),
            ["HappeningWorker_MissionSeedGrain"] = (new[] { "kept" }, new[] { "warm", "eaten", "spoiled", "lost", "ended" }),
            ["HappeningWorker_MissionBookCopy"] = (new[] { "copied", "copiednobook", "half" }, new[] { "uncopied", "died", "harmed", "ended" }),
            ["HappeningWorker_MissionMusicians"] = (new[] { "party", "noparty" }, new[] { "harmed", "ended" }),
        };
        var noDays = new[] { "HappeningWorker_MissionCampGuard", "HappeningWorker_MissionMusicians" };
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        var ages = new[] { "tribal", "medieval", "industrial", "spacer" };
        foreach (var d in quests)
        {
            string name = (string)d.Element("defName");
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(outcomesOf.ContainsKey(worker) && code.Contains("class " + worker + " : "), $"{name}: its worker ({worker})");
            var ext = d.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var who = d.Descendants("li").FirstOrDefault(x => Cls(x) == "MissionPeople");
            C(who != null && PeoplesMath.IsCalling((string)who.Element("prefers")) && who.Element("only") == null,
                $"{name}: any people at peace may ask, one of a fitting calling likelier ({(string)who?.Element("prefers")})");
            var text = d.Descendants("li").First(x => Cls(x) == "MissionText");
            var texts = Lis(ext, "texts");
            foreach (var age in ages)
            {
                bool lowtech = age == "tribal" || age == "medieval";
                C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*their:" + age + @"\b") || Regex.IsMatch(t, @"^\[[^\]]*their:" + (lowtech ? "lowtech" : "hightech") + @"\b")
                                 || !Regex.IsMatch(t, @"^\[[^\]]*their:")), $"{name}: asked in a {age} people's own form");
            }
            C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*their:empire")), $"{name}: the Empire asks in its own way");
            C(texts.Where(t => Regex.IsMatch(t, @"^\[[^\]]*their:(spacer|hightech)")).All(t => t.Contains("!their:empire")), $"{name}: a spacer people's words never the Empire's");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 2 && (string)ext.Element("defaultOption") == "1", $"{name}: taken on or declined, declined unanswered");
            var terms = Lis(text, "terms");
            C(terms.Count > 0 && terms.All(t => t.Contains("{ITEM}") && Regex.IsMatch(t, @"\{FACTION\} will (think less|hold it|be disappointed)")),
                $"{name}: its terms say what the colony gets and what failing costs");
            C(terms.All(t => t.Contains("{DAYS}")) || noDays.Contains(worker), $"{name}: the days stated in its terms");
            // The order the player asked for: who and what first (the offer's text), then in the terms by when, what the colony gets, what failing costs.
            C(terms.All(t => t.IndexOf("{ITEM}", StringComparison.Ordinal) < Regex.Match(t, @"\{FACTION\} will (think less|hold it|be disappointed)").Index),
                $"{name}: what the colony gets before what failing costs");
            var all = texts.Concat(terms).Concat(Lis(text, "accepted")).Concat(Lis(text, "ready")).Concat(Lis(text, "done")).Concat(Lis(text, "fail")).Concat(Lis(text, "cancelled"))
                .Concat(Lis(text, "reminder")).Concat(Lis(ext, "titles")).Concat(Lis(text, "doneTitles")).Concat(Lis(text, "failTitles"))
                .Concat(options.Select(o => (string)o.Element("outcome") ?? "")).ToList();
            C(all.All(Plain), $"{name}: plain prose, no em dashes or exclamation marks");
            C(Lis(text, "accepted").Concat(Lis(text, "done")).Concat(Lis(text, "fail")).Concat(Lis(text, "cancelled")).Concat(Lis(text, "reminder")).Concat(texts).All(t => !t.Contains("{DAYS}")),
                $"{name}: no countdown outside the terms and the arrival (the Quests tab shows the time)");
            var ends = Lis(text, "done").Concat(Lis(text, "fail")).ToList();
            var (done, fail) = outcomesOf.GetValueOrDefault(worker, (Array.Empty<string>(), Array.Empty<string>()));
            foreach (var o in done.Concat(fail))
            {
                C(ends.Any(t => Regex.IsMatch(t, @"^\[([^\]]*,)?" + o + @"(,[^\]]*)?\]")), $"{name}: words for how it ended ({o})");
                C(code.Contains("\"" + o + "\""), $"{name}: the code ends it as {o}");
            }
            foreach (var o in done) C(Lis(text, "done").Where(t => Regex.IsMatch(t, @"^\[([^\]]*,)?" + o + @"(,[^\]]*)?\]")).All(t => t.Contains("{ITEM}") || o == "bonded"), $"{name}: a done ending says what was paid ({o})");
            C(Lis(text, "cancelled").Count > 0 && Lis(text, "accepted").Count > 0 && Lis(text, "ready").Count > 0, $"{name}: its taking on, its moment on the map and its calling off");
        }

        // ================= Numbers the words state, as the rules have them =================

        string termsOf(string def) => string.Join(" ", defs.Where(d => (string)d.Element("defName") == def).SelectMany(d => d.Descendants("terms").Elements("li")).Select(x => x.Value));
        C(termsOf("Rimfolk_Mission_Trainee").Contains("about two hours") && HomeQuests2Math.TrainHoursPerDay == 2 && termsOf("Rimfolk_Mission_Trainee").Contains("half of that for half the hours"),
            "the young fighter: two hours an evening, half the pay for half the hours, as the rules have it");
        C(termsOf("Rimfolk_Mission_SeedKept").Contains("within six hours") && termsOf("Rimfolk_Mission_SeedKept").Contains("more than 12 hours") && termsOf("Rimfolk_Mission_SeedKept").Contains("freezing or colder"),
            "the seed: six hours to carry it in, twelve warm hours, freezing or colder, as the rules have it");
        C(termsOf("Rimfolk_Mission_CampGuard").Contains("a quarter more") && termsOf("Rimfolk_Mission_CampGuard").Contains("late afternoon"), "the camp: a quarter more when defended; they come in the late afternoon");
        C(termsOf("Rimfolk_Mission_WeddingPiece").Contains("good quality or better") && termsOf("Rimfolk_Mission_SickAnimal").Contains("pay half") && termsOf("Rimfolk_Mission_BookCopy").Contains("{COUNT} hours"),
            "the piece good or better, the animal still sick for half, the copy's hours stated");

        // ================= What happens on the map =================

        C(!Regex.IsMatch(code + shared, @"CaravansAt|MissionJourney|FormCaravan|CaravanMaker"), "nothing sends the colony's people away: every quest plays out on its own map");
        C(code3.Contains("Harm.Roll(HarmKinds.Drills, HarmMath.DrillBlow(false))") && code3.Contains("Harm.Injure(defender, kind, where, HarmMath.DrillLow, HarmMath.DrillHigh, HarmKinds.Drills, HurtClass.Bout")
          && code3.Contains("Harm.May(p, HurtClass.Bout)"), "sparring hurts only at the colony's own drills' rate, within the shared budget and its cap");
        C(code3.Contains("skills?.Learn(skill, HomeQuests2Math.TraineeXpPerTick * delta)") && code3.Contains("skills?.Learn(skill, HomeQuests2Math.TrainerXpPerTick * delta)"),
            "both learn the game's own way, as they train");
        C(code3.Contains("\"ShootingRangeTarget\", \"ArcheryTarget\", \"VFEM2_ArcheryTarget\"") && shared.Contains("verb?.verbProps?.soundCast?.PlayOneShot") && code3.Contains("DrillBouts.Places("),
            "at the colony's own target (Misc. Training's, Medieval 2's) with the weapon's own sound, else sparring on open ground");
        C(shared.Contains("HomeQuests2Math.GoodEnough((int)q)") && shared.Contains("InstallBlueprintUtility.ExistingBlueprintFor(t)") && code3.Contains("Couriers.CollectThings(map, new List<Thing> { m.thing }"),
            "the wedding's piece: the game's own quality, a building packed, collected by their people");
        C(shared.Contains("ThingRoles.ColonyCanMake(d)") && shared.Contains("Avail.CanBuild(d, map)") && shared.Contains("\"VFEM2_Apparel_DameDress\"") && shared.Contains("\"Apparel_TribalHeaddress\""),
            "a piece every age has, from the mod pool, that the colony can make");
        C(shared.Contains("\"Animal_Flu\"") && shared.Contains("\"Animal_Plague\"") && shared.Contains("HediffDefOf.Malnutrition") && code3.Contains("x.SetFaction(Faction.OfPlayer)")
          && code3.Contains("Couriers.Fetch(map, x, m.faction,"), "the sick animal: the game's own illness, the colony's to tend, fetched back by their drover");
        C(code3.Contains("Harm.Roll(HarmKinds.CampNight, HomeQuests2Math.AttackChance)") && code3.Contains("IncidentWorker_Happening.FightsPossible") && code3.Contains("new LordJob_AssaultColony(enemy")
          && code3.Contains("MentalStateDefOf.Manhunter") && code3.Contains("ThingDefOf.Campfire"), "the camp's night: within the shared harm budget, a real fight the game's own, by a fire of theirs");
        C(HarmKinds.All.Contains(HarmKinds.CampNight) && HarmKinds.CampNight.system == "quests" && !HarmKinds.CampNight.founding, "what comes for the camp is a kind of the shared harm budget, off with the world's battles");
        C(code4.Contains("GenTemperature.GetTemperatureForCell(t.PositionHeld, map)") && code4.Contains("HomeQuests2Math.Cold(") && shared.Contains("room.Temperature")
          && code4.Contains("ThingRole.Produce"), "the seed: a crop of their age, looked at every hour where it lies, cold or not");
        C(code4.Contains("KeepOut.IsKept(r)") && code4.Contains("JobDefOf.HaulToCell") && code4.Contains("HaulMode.ToCellNonStorage") && code4.Contains("t.SetForbidden(true, false)"),
            "at the sowing, one of the colony's own carries the seed out of the cold store (visitors keep out of it) for their carts to collect");
        C(shared.Contains("class WorkGiver_HomeQuestCopy : WorkGiver_Scanner") && shared.Contains("ThingRequestGroup.ResearchBench") && shared.Contains("EffecterDefOf.Research")
          && shared.Contains("Learn(SkillDefOf.Intellectual, HomeQuests2Math.CopyXpPerTick * delta)"), "the copy: real work at the research bench, a job of its own, the game's own learning");
        C(code4.Contains("\"Novel\"") && code4.Contains("\"Schematic\"") && code4.Contains("\"TextBook\"") && code4.Contains("GenerateQualityCreatedByPawn(copier, SkillDefOf.Intellectual, false)"),
            "the colony's copy: a book of the game's own, its quality the copier's");
        C(code4.Contains("party.Worker.TryExecute(map, org)") && shared.Contains("m.StartPlaying(pawn)") && shared.Contains("soundPlayInstrument.TrySpawnSustainer"),
            "the musicians: the game's own party, the colony's instrument played or their own heard");
        C(shared.Contains("GetNamedSilentFail(\"Drum\")") && shared.Contains("GetNamedSilentFail(\"Harp\")"), "their own: drums for a tribe, harps for the rest, by the game's own instruments' sound");
        C(!Regex.IsMatch(code, @"\bTakeDamage\("), "no hurt of their own beyond the sparring blow and the game's own fight");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Missions_Home2_Misc.xml")).Root!;
        var wg = misc.Elements("WorkGiverDef").FirstOrDefault();
        C(wg != null && (string)wg.Element("workType") == "Research" && ((string)wg.Element("giverClass") ?? "").EndsWith("WorkGiver_HomeQuestCopy")
          && int.Parse((string)wg.Element("priorityInType")) > 0, "the copying is research work, before the research");
        foreach (var j in new[] { "Rimfolk_HomeQ_Train", "Rimfolk_HomeQ_Copy", "Rimfolk_HomeQ_Play" })
            C(misc.Elements("JobDef").Any(e => (string)e.Element("defName") == j) && shared.Contains("public static JobDef " + j + ";"), $"the job {j}");

        // ================= Words, lines and the dev tools =================

        foreach (Match k in Regex.Matches(code + shared, "\"(CC_HomeQ2_[A-Za-z0-9_]*[A-Za-z0-9])\""))
            C(keyed.ContainsKey(k.Groups[1].Value), $"{k.Groups[1].Value} is in the words");
        foreach (var tag in new[] { "stories", "learning", "manual" }) C(keyed.ContainsKey("CC_HomeQ2_Book_" + tag), $"the book: {tag}");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_HomeQ2_")).All(kv => Plain(kv.Value)), "the messages in plain words");
        string poolsFile = Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_HomeQuests2.xml");
        var pools = XDocument.Load(poolsFile).Root!.Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
        C(pools.Count >= 30 && pools.All(l => Regex.Matches(l, @"[.?]").Count == 1 && (l.EndsWith(".") || l.EndsWith("?")) && Plain(l)), "every line over a head one plain sentence");
        string poolsText = File.ReadAllText(poolsFile);
        foreach (Match p in Regex.Matches(code + shared, "\"(Rimfolk_Pool_HomeQ2_[A-Za-z]+)\""))
            C(poolsText.Contains(">" + p.Groups[1].Value + "<"), $"{p.Groups[1].Value} has its lines");
        // A line to someone only where the code names them.
        foreach (var pool in new[] { "Rimfolk_Pool_HomeQ2_SickHanded", "Rimfolk_Pool_HomeQ2_CampAlarm", "Rimfolk_Pool_HomeQ2_CampLeave", "Rimfolk_Pool_HomeQ2_CopyDone", "Rimfolk_Pool_HomeQ2_MusicStart", "Rimfolk_Pool_HomeQ2_MusicLeave" })
        {
            var el = XDocument.Load(poolsFile).Root!.Elements().FirstOrDefault(e => (string)e.Element("defName") == pool);
            C(el != null && el.Descendants("li").All(l => !l.Value.Contains("{OTHER")), $"{pool}: said to nobody in particular, so it names nobody");
        }
        var debug = Code("Missions/HomeQuestsDebug.cs");
        foreach (var w in outcomesOf.Keys) C(debug.Contains(w + ".DebugOutcomes"), $"dev mode: {w}'s outcomes");
        foreach (var d in quests) C(debug.Contains("\"" + (string)d.Element("defName") + "\""), $"dev mode: offer {(string)d.Element("defName")}");
        C(Code("Missions/HomeQuests.cs").Contains("HomeQuests2.IsOurs(m)"), "round 1's tags and dev tools take round 2's quests in (a small change to the shared file)");
        var missions = Code("Missions/Missions.cs");
        C(!missions.Contains("HomeQuests2"), "nothing of the missions themselves changed for them");
        return n;
    }
}
