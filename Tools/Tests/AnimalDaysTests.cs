using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The animals' days, made seen (Animals/AnimalDaysMath.cs, AnimalDays.cs, AnimalScenes.cs): whom an animal with no bond and
/// no master is fond of, fixed for the pair and only ever won from the rest by a newcomer; how often each moment comes; who
/// begs, sleeps at the foot of a bed or plays with a child; the foot of a bed; home again; which two may be friends; the
/// cold that makes a herd huddle; and the mod's side: the hooks (the animals' hour, the temperaments' person, the fight's
/// look, the encounters told after their scene), no letter or message, nobody hurt, every job and its words, every pool and
/// tag, the encounters and personal stories that now play out, the card's line, the dev actions, the settings' words.
/// </summary>
public static class AnimalDaysTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "animal days: " + what);
        }

        // ================= Whom it is fond of =================

        C(AnimalDaysMath.FondWeight(10, true, false, false, false) > AnimalDaysMath.FondWeight(10, false, false, false, false)
          && AnimalDaysMath.FondWeight(0, false, true, false, false) > AnimalDaysMath.FondWeight(0, false, false, false, false)
          && AnimalDaysMath.FondWeight(0, false, false, true, false) > AnimalDaysMath.FondWeight(0, false, false, false, false)
          && AnimalDaysMath.FondWeight(15, false, false, false, false) > AnimalDaysMath.FondWeight(2, false, false, false, false),
            "a love of animals, a child, a kind soul and a good hand each count for more");
        C(AnimalDaysMath.FondWeight(10, false, false, false, true) < AnimalDaysMath.FondWeight(10, false, false, false, false)
          && AnimalDaysMath.FondWeight(0, false, true, false, true) == AnimalDaysMath.FondWeight(0, false, true, false, false),
            "someone who never handles animals hardly at all, a child all the same");
        var ids = new List<int> { 101, 202, 303, 404 };
        var weights = new List<float> { 1f, 2f, 1.5f, 3f };
        int first = AnimalDaysMath.Favourite(7777, ids, weights);
        C(ids.Contains(first) && AnimalDaysMath.Favourite(7777, ids, weights) == first, "the same animal is fond of the same one, every time");
        C(AnimalDaysMath.Favourite(7777, new List<int> { 404, 303, 202, 101 }, new List<float> { 3f, 1.5f, 2f, 1f }) == first, "whatever order the colony is counted in");
        bool stable = true;
        for (int seed = 1; seed < 400; seed++)
        {
            int before = AnimalDaysMath.Favourite(seed, ids, weights);
            int after = AnimalDaysMath.Favourite(seed, ids.Concat(new[] { 505 }).ToList(), weights.Concat(new[] { 2f }).ToList());
            if (after != before && after != 505) stable = false;
        }
        C(stable, "a newcomer only ever wins an animal from the rest: nobody else's change hands");
        int newcomerWins = Enumerable.Range(1, 400).Count(seed => AnimalDaysMath.Favourite(seed, ids.Concat(new[] { 505 }).ToList(), weights.Concat(new[] { 2f }).ToList()) == 505);
        C(newcomerWins > 20 && newcomerWins < 200, $"and does win some ({newcomerWins} of 400)");
        int heavy = Enumerable.Range(1, 2000).Count(seed => AnimalDaysMath.Favourite(seed, new List<int> { 1, 2 }, new List<float> { 1f, 3f }) == 2);
        C(heavy > 1300 && heavy < 1700, $"three times the weight, about three animals in four ({heavy} of 2000)");
        C(AnimalDaysMath.Favourite(5, new List<int>(), new List<float>()) == -1 && AnimalDaysMath.Favourite(5, new List<int> { 1 }, new List<float> { 0f }) == -1,
            "nobody, or nobody with any weight: none");

        // ================= How often =================

        foreach (AnimalDay d in Enum.GetValues(typeof(AnimalDay)))
            C(AnimalDaysMath.Days(d) > 0f && AnimalDaysMath.HoursOf(d) > 0, $"{d} has a pace and hours");
        C(Math.Abs(AnimalDaysMath.HourChance(AnimalDay.FootOfBed, 1f, AnimalDaysMath.HoursOf(AnimalDay.FootOfBed)) * AnimalDaysMath.HoursOf(AnimalDay.FootOfBed)
                   * AnimalDaysMath.Days(AnimalDay.FootOfBed) - 1f) < 0.001f, "at 100%, once in its days over its hours");
        C(AnimalDaysMath.HourChance(AnimalDay.Beg, 0f, 15) == 0f && AnimalDaysMath.HourChance(AnimalDay.Beg, 2f, 15) <= 1f, "nothing at the pace's nothing, never above certain");
        C(AnimalDaysMath.Days(AnimalDay.FootOfBed) == 3f && AnimalDaysMath.Days(AnimalDay.Beg) == 2f && AnimalDaysMath.Days(AnimalDay.ChildPlay) == 1f
          && AnimalDaysMath.Days(AnimalDay.Rounds) == 2f, "the foot of the bed every three nights, a meal every two days, play daily, the rounds every two days");
        C(AnimalDaysMath.BedHour(23) && AnimalDaysMath.BedHour(0) && !AnimalDaysMath.BedHour(12) && AnimalDaysMath.HerdHour(7) && !AnimalDaysMath.HerdHour(13)
          && AnimalDaysMath.WakingHour(7) && AnimalDaysMath.NightHour(3) && !AnimalDaysMath.NightHour(14), "each in its own part of the day");

        // ================= Who =================

        C(AnimalDaysMath.MayBeg(0.86f, false) && !AnimalDaysMath.MayBeg(2.4f, false) && !AnimalDaysMath.MayBeg(0.8f, true), "a dog begs; a horse and the herd's ones don't");
        C(AnimalDaysMath.MaySleepAtFoot(0.32f, false) && !AnimalDaysMath.MaySleepAtFoot(2.4f, false) && !AnimalDaysMath.MaySleepAtFoot(1f, true), "a cat at the foot of the bed; not a horse, not the herd");
        C(AnimalDaysMath.PlaysWithChild(1.1f, false) && AnimalDaysMath.PlaysWithChild(0.86f, true) && !AnimalDaysMath.PlaysWithChild(1.5f, true) && !AnimalDaysMath.PlaysWithChild(3.5f, false),
            "a child plays with a hen or a dog; not a great hunter or a beast of three tons");
        C(AnimalDaysMath.GreetChance(true) > AnimalDaysMath.GreetChance(false), "pets come running oftener than the herd's ones");
        C(AnimalDaysMath.MayBefriend(true, true, true) && AnimalDaysMath.MayBefriend(false, false, false) && AnimalDaysMath.MayBefriend(true, false, true) == false
          && !AnimalDaysMath.MayBefriend(false, false, true), "friends: two pets, or two of different kinds; never two of the herd's own kind");

        // ================= Where and when =================

        C(AnimalDaysMath.FootCell(10, 10, 0, 1) == (10, 9) && AnimalDaysMath.FootCell(4, 7, 1, 0) == (3, 7), "the foot of the bed: past the sleeper, away from the head");
        C(AnimalDaysMath.Homecoming(6f, 1f) && !AnimalDaysMath.Homecoming(1f, 0.5f) && !AnimalDaysMath.Homecoming(24f, 5f), "home after hours away, greeted soon after; not after a stroll, not long after");
        C(AnimalDaysMath.Huddles(-15f) && !AnimalDaysMath.Huddles(0f), "a bitter night, not a cool one");
        C(AnimalDaysMath.Shelters(0.8f, 0f) && AnimalDaysMath.Shelters(0f, 0.8f) && !AnimalDaysMath.Shelters(0.1f, 0f), "rain or snow, not a drizzle");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string days = Read("Animals/AnimalDays.cs"), scenes = Read("Animals/AnimalScenes.cs"), jobs = Read("Animals/AnimalDayJobs.cs"), debug = Read("Animals/AnimalDaysDebug.cs"),
            facts = Read("Animals/AnimalFacts.cs"), tempers = Read("Animals/AnimalTempers.cs"), life = Read("Animals/AnimalLife.cs"), bonds = Read("Animals/AnimalBonds.cs"),
            encounters = Read("Strangers/Encounters.cs"), inspect = Read("Animals/AnimalNatureJobs.cs");
        C(days.Length > 0 && scenes.Length > 0 && jobs.Length > 0 && debug.Length > 0, "the four files are there");
        var popups = new Regex(@"\bTell\.(Letter|Message|Minor)\(|\bNews\.Post\(|\bMessages\.Message\(|\bLetterStack\b|\bLetterMaker\b");
        foreach (var (name, text) in new[] { ("AnimalDays.cs", days), ("AnimalScenes.cs", scenes), ("AnimalDayJobs.cs", jobs) })
            C(!popups.IsMatch(text), $"{name}: no letter and no message");
        C(!Regex.IsMatch(days + scenes + jobs, @"Harm\.(Injure|Roll|Fight|Note)|TakeDamage|DamageInfo"), "nobody is hurt");
        C(!Regex.IsMatch(days + scenes, @"MoteMaker\.ThrowText") && Regex.Matches(days, @"Speech\.Say\(").Count >= 1, "what is said goes through Speech.Say");
        C(facts.Contains("public static Pawn PersonOf(Pawn a) => PetTracker.PersonOf(a) ?? FondOf(a);") && facts.Contains("AnimalDaysMath.Favourite("),
            "every named animal has its person: bonded, its master, else the one it is fond of");
        C(tempers.Contains("var person = AnimalFacts.PersonOf(a);") && Regex.Matches(tempers, @"PetTracker\.PersonOf\(a\)").Count == 1,
            "the temperaments show with that person, the fight's stand alone kept to a bond or a master");
        C(tempers.Contains("AnimalDays.Notify_PersonDied(a, before)") && tempers.Contains("AnimalDays.Alarm(map, hostiles)"), "a death and a raid reach the animals' days");
        C(life.Contains("AnimalDays.Hourly") && life.Contains("AnimalScenes.Hourly") && life.Contains("AnimalFacts.PersonOf(a)"), "the animals' hour runs them, and a pet follows its person");
        C(bonds.Contains("AnimalDaysMath.MayBefriend(") && bonds.Contains("Nights(book, animals,"), "friends across kinds in the barn");
        C(encounters.Contains("AnimalScenes.Begin(def, ctx, () => Told(def, comp, map, a, x, ctx, text))"), "an encounter with a scene is told once it has played");
        C(scenes.Contains("AnimalScenes.Telling") && scenes.Contains("public class DilemmaEffect_AnimalScene"), "the scene's own effect does nothing while its words are told");
        C(inspect.Contains("AnimalDays.InspectPart(a)"), "the card says whom it is fond of");
        C(Regex.Matches(debug, @"\[DebugAction\(""Rimfolk"", ""Animals: ").Count == 3, "the dev actions: whom each is with, a moment now, a scene now");
        C(!Regex.IsMatch(days + scenes, @"Sched\.|Rare[A-Z]\w+ ="), "no Sched slot of its own (the animals' hour and the fight's look)");

        // The defs: the jobs and their words.
        string defs = Path.Combine(root, "Defs");
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var jobDoc = XDocument.Load(Path.Combine(defs, "Misc", "Rimfolk_AnimalDays_Jobs.xml"));
        var jobNames = jobDoc.Root!.Elements("JobDef").Select(e => e.Element("defName")?.Value).ToList();
        foreach (var j in Regex.Matches(jobs, @"public static JobDef (\w+);").Select(m => m.Groups[1].Value))
            C(jobNames.Contains(j), $"the job {j} has its def");
        foreach (var r in jobDoc.Descendants("report").Select(e => e.Value))
            C(keyed.Contains("<" + r + ">"), $"the job's words {r} exist");
        foreach (var k in Regex.Matches(days + scenes + jobs, @"""(CC_Animal(?:Day|Scene|Job)_\w+)""").Select(m => m.Groups[1].Value).Distinct())
            C(keyed.Contains("<" + k + ">"), $"the words {k} exist");

        // The pools: every moment has its lines, each tagged, one plain sentence, never starting with the animal.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "FlavorPoolDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[el.Element("defName")!.Value] = el.Descendants("li").Select(li => li.Value).ToList();
        var lines = pools.GetValueOrDefault("Rimfolk_Pool_AnimalDays") ?? new List<string>();
        foreach (var moment in Regex.Matches(days, @"""(day:\w+)""").Select(m => m.Groups[1].Value).Distinct())
            C(lines.Count(l => Regex.IsMatch(l, @"^\[" + moment + @"[,\]]")) >= 3, $"lines for {moment}");
        C(lines.Count > 60 && lines.All(l => l.StartsWith("[day:")), "every line carries its moment, so none is said in another's place");
        C(lines.All(l => !Regex.IsMatch(l, @"^\[[^\]]*\]\s*\{PET")), "no line starts with the animal (one of the herd with no name is \"the muffalo\")");
        C(lines.All(l => !l.Contains("{GOD") && !Regex.IsMatch(l, @"\bgods?\b", RegexOptions.IgnoreCase)), "the gods are never named by an uneasy animal's onlookers");
        C(pools.ContainsKey("Rimfolk_Journal_AnimalDays") && days.Contains("\"Rimfolk_Journal_AnimalDays\""), "the journal's pool exists and is used");
        string immersion = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "ImmersionTests.cs"));
        C(immersion.Contains("\"Rimfolk_Pool_AnimalDays\""), "its lines are held to one sentence of ninety characters (ImmersionTests)");

        // The encounters that play out now, and their words.
        var enc = XDocument.Load(Path.Combine(defs, "EncounterDefs", "Council_Encounters_Creatures.xml")).Root!.Elements("ColonyCouncil.EncounterDef").ToList();
        var withScene = enc.Where(e => e.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_AnimalScene")).ToList();
        C(withScene.Count >= 10, $"ten of the animals' encounters play out first ({withScene.Count})");
        foreach (var e in withScene)
        {
            string name = e.Element("defName")!.Value;
            string scene = e.Descendants("scene").First().Value;
            C(Enum.TryParse<AnimalScene>(scene, out var s) && s != AnimalScene.None, $"{name}: its scene {scene} is one");
            C(e.Element("texts")!.Elements("li").All(t => !t.Value.StartsWith("{X_")), $"{name}: no text starts with the animal");
            C(!e.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Consume"), $"{name}: nothing taken from the stores besides what is eaten on the map");
        }
        var arcs = XDocument.Load(Path.Combine(defs, "ArcDefs", "Rimfolk_Arcs_Animals.xml")).Root!.Elements("ColonyCouncil.ArcDef").ToList();
        string[] play = { "DilemmaEffect_Visit", "DilemmaEffect_Follow", "DilemmaEffect_AnimalScene", "DilemmaEffect_Rest" };
        foreach (var a in arcs)
        {
            var stages = a.Element("stages")!.Elements("li").ToList();
            int seen = stages.Take(stages.Count - 1).Count(st => st.Element("effects")?.Elements("li").Any(li => play.Any(p => ((string)li.Attribute("Class") ?? "").EndsWith(p))) == true);
            C(seen == stages.Count - 1, $"{a.Element("defName")!.Value}: every step before the last is seen on the map ({seen} of {stages.Count - 1})");
        }
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(audit.Contains("\"DilemmaEffect_AnimalScene\""), "the play-out audit counts an animal's scene");

        // The settings' words.
        C(Regex.IsMatch(keyed, @"<CC_Settings_PetMoments_Tip>[^<]*foot of their bed[^<]*</CC_Settings_PetMoments_Tip>")
          && Regex.IsMatch(keyed, @"<CC_Settings_AnimalMoments_Tip>[^<]*daily moments[^<]*</CC_Settings_AnimalMoments_Tip>"), "the settings say what they now cover");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
