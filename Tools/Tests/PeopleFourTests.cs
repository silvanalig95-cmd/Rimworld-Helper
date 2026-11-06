using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The colony's own people, the fourth round (the player's request, 6 November 2026: "do another big content update to
/// existing mechanics"): twelve life goals, seven wishes, eight quirks, eight names and six deeds in the existing
/// frameworks. The rules without game types (Lives/PeopleFourMath.cs, GoalMath's war), the defs and their words, the code
/// they hook into, and the rates: life goals a colonist-year and their share from fights on a model of the real pool
/// before and after, the wishes a share of the same roll, the quirks' and names' pace.
/// </summary>
public static class PeopleFourTests
{
    private static readonly string[] Tones = { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "people four: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Root(string rel) => XDocument.Load(Path.Combine(root, rel)).Root!;
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "ThoughtDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(t => (string)t.Element("defName") ?? ""));
        var records = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "Misc"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("RecordDef")).Select(t => (string)t.Element("defName") ?? ""));
        // The game's own records the new goals and names count (RecordDefOf has them all).
        foreach (var r in new[] { "ThingsRepaired" }) records.Add(r);
        // The game's own memories the new goals, quirks and deeds read (each used elsewhere in Rimfolk already).
        foreach (var t in new[] { "HadAngeringFight", "GotMarried", "RejectedMyProposal" }) thoughts.Add(t);

        // Every line of a ToneLines, with the tone it is in.
        IEnumerable<(string tone, string line)> Lines(XElement e)
        {
            if (e == null) yield break;
            foreach (var tone in Tones)
                foreach (var li in e.Element(tone)?.Elements("li") ?? Enumerable.Empty<XElement>())
                    yield return (tone, li.Value);
        }
        string Body(string line) => Regex.Replace(line, @"^\[[^\]]*\]\s*", "");
        void Words(string where, string text)
        {
            C(!text.Contains('—') && !text.Contains('–'), $"{where}: no dashes ({text})");
            C(!text.Contains('!'), $"{where}: no exclamation marks ({text})");
        }

        // ================= The rules =================

        C(PeopleFourMath.CleanDays(10 * 60000, 0, -1, -1, 60000) == 10f, "ten days clean since the goal began");
        C(PeopleFourMath.CleanDays(10 * 60000, 0, 4 * 60000, -1, 60000) == 6f && PeopleFourMath.CleanDays(10 * 60000, 0, 2 * 60000, 7 * 60000, 60000) == 3f,
            "the clock starts again at a hurt, or a fall, after the goal began");
        C(PeopleFourMath.CleanDays(5 * 60000, 3 * 60000, 1 * 60000, -1, 60000) == 2f, "a hurt before the goal began doesn't count against it");
        C(PeopleFourMath.CleanDays(0, 5, -1, -1, 60000) == 0f, "never fewer than none");
        C(PeopleFourMath.Share(15f, 30f) == 0.5f && PeopleFourMath.Share(45f, 30f) == 1f && PeopleFourMath.Share(-1f, 30f) == 0f, "the share of a month");
        C(PeopleFourMath.SureFootedDays == 30f && PeopleFourMath.RecentHurtDays < PeopleFourMath.SureFootedDays, "a month clean, for someone hurt lately");
        C(PeopleFourMath.HomeTarget(3, 8) == 8 && PeopleFourMath.HomeTarget(7, 8) == 9, "a new colony to its number, or two more than now");
        C(PeopleFourMath.Visited(5, 9, -1) && !PeopleFourMath.Visited(5, 5, -1) && !PeopleFourMath.Visited(5, -1, -1) && !PeopleFourMath.Visited(-1, 9, -1),
            "the journey is made on another of the colony's homes");
        C(!PeopleFourMath.TreesByWallsWorth(2) && PeopleFourMath.TreesByWallsWorth(3), "three trees against the walls are worth a word");
        C(PeopleFourMath.SnowWorth(12, 0) && !PeopleFourMath.SnowWorth(11, 0) && !PeopleFourMath.SnowWorth(30, 6), "deep snow by the doors, none of it marked yet");
        C(PeopleFourMath.SnowCleared(1, 12) && !PeopleFourMath.SnowCleared(2, 12) && !PeopleFourMath.SnowCleared(0, 0), "the ways are clear at nine in ten");
        C(PeopleFourMath.ClearedSnow < PeopleFourMath.DeepSnow, "cleared is shallower than deep");

        // A war among the neighbours: a spark of fighting, under the one-at-a-time rule; a hurt is not.
        C(GoalMath.FightSpark("war") && GoalMath.FightSpark("battle") && !GoalMath.FightSpark("hurt") && !GoalMath.FightSpark(null), "a war is a spark of fighting, a hurt is not");
        C(!GoalMath.FightMaySpark("war", 1) && GoalMath.FightMaySpark("war", 0) && GoalMath.FightMaySpark("hurt", 3), "a war sets nobody on a fighting goal another took up after a fight or a war");
        C(GoalMath.SparkPull("war") < GoalMath.SparkPull("battle") && GoalMath.SparkPull("hurt") == 1f, "a war pulls like a foe, less than a fight; a hurt like the rest of life");
        C(!GoalMath.Big("war") && !GoalMath.Big("hurt"), "neither changes a goal held");

        // ================= Life goals =================

        var goalsFile = Root("Defs/AspirationDefs/Rimfolk_Aspirations_People4.xml");
        var goals = goalsFile.Elements("ColonyCouncil.AspirationDef").ToList();
        C(goals.Count == 12, $"twelve life goals ({goals.Count})");
        string aspCode = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Aspirations"), "*.cs").Select(File.ReadAllText));
        var knownSparks = new HashSet<string> { "battle", "downed", "loss", "birth", "omen", "fulfilled", "nemesis", "hurt", "war", "grewup" };
        var features = new HashSet<string> { "camps", "worldSituations", "colonyCalendar", "socialWeb", "ideology" };
        foreach (var g in goals)
        {
            string name = (string)g.Element("defName")!;
            string worker = ((string)g.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(aspCode.Contains("class " + worker + " "), $"{name}: its worker {worker} is one");
            string goal = (string)g.Element("goal") ?? "";
            C(goal.Length > 0 && char.IsLower(goal[0]) && !goal.EndsWith("."), $"{name}: the goal reads after 'set their heart on' ({goal})");
            C(((string)g.Element("deed") ?? "").Length > 0, $"{name}: what they did, once done");
            var sparks = Lis(g, "sparks");
            foreach (var s in sparks)
            {
                bool thought = s.StartsWith("thought:");
                C(thought ? thoughts.Contains(s.Substring(8)) : knownSparks.Contains(s), $"{name}: the spark {s} is one");
                C(keyed.ContainsKey(thought ? "CC_Asp_Spark_thought_" + s.Substring(8) : "CC_Asp_Spark_" + s), $"{name}: the spark {s} has its words");
            }
            C(!sparks.Contains("battle"), $"{name}: no new goal comes from a fight, so the share from fights doesn't rise");
            if (worker == "AspirationWorker_FeatureRecord") C(features.Contains((string)g.Element("counter") ?? ""), $"{name}: its feature is one");
            string record = (string)g.Element("record");
            if (record != null) C(records.Contains(record), $"{name}: its record {record} is one");
            var chosen = Lines(g.Element("chosen")).ToList();
            var achieved = Lines(g.Element("achieved")).ToList();
            C(chosen.Count >= 8 && chosen.Select(x => x.tone).Distinct().Count() >= 5, $"{name}: chosen in its own words, in five tones or more ({chosen.Count})");
            C(achieved.Count >= 4 && achieved.Select(x => x.tone).Distinct().Count() >= 4, $"{name}: reached in its own words ({achieved.Count})");
            foreach (var s in sparks.Where(s => s != "fulfilled"))
            {
                string tag = "[spark:" + (s.StartsWith("thought:") ? s.Substring(8) : s) + "]";
                C(chosen.Any(x => x.line.StartsWith(tag)), $"{name}: a line for what sparked it, {tag}");
            }
            foreach (var (tone, line) in chosen.Concat(achieved)) Words(name, line);
            float w = float.Parse((string)g.Element("weight") ?? "1", CultureInfo.InvariantCulture);
            C(w > 0f && w <= 1.2f, $"{name}: an ordinary weight ({w})");
        }
        // Every age's form: the goals whose work differs by the colony's means say so in the age's own words.
        foreach (var name in new[] { "Rimfolk_Asp_Nurse", "Rimfolk_Asp_ReadyHands" })
        {
            var g = goals.First(x => (string)x.Element("defName") == name);
            var tags = Lines(g.Element("chosen")).Select(x => x.line).ToList();
            foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
                C(tags.Any(l => l.StartsWith("[" + age + "]")), $"{name}: a line in the {age} age's form");
        }
        C(Code("Aspirations/Aspirations.cs").Contains("spark == GoalSparks.Hurt") && Code("Aspirations/Aspirations.cs").Contains("spark == GoalSparks.War")
          && Code("Aspirations/Aspirations.cs").Contains("GoalMath.FightSpark(a.spark)"), "the tracker reads a hurt and a war, and holds a war to the fighting rule");
        C(aspCode.Contains("Harm.DaysSince(p) < days") && aspCode.Contains("s.def.kind == \"war\" && s.def.bad"), "a hurt from the harm service's book, a war from the world events running");

        // ================= Rates: life goals on a model of the real pool =================

        var pool = LoadGoals(root);
        var before = pool.Where(g => !g.mine).ToList();
        var after = pool;
        var rb = SimulateMany(before, 60);
        var ra = SimulateMany(after, 60);
        Console.WriteLine($"  people four: life goals {pool.Count(g => !g.mine)} before, {pool.Count} after; started a colonist-year {rb.PerYear:0.00} before, {ra.PerYear:0.00} after; " +
                          $"from fights {rb.FightShare:P1} and {ra.FightShare:P1}; from a war {ra.WarShare:P1}; from a hurt {ra.HurtShare:P1}; at most {ra.mostOnOneFightingGoal} on one fighting goal from fights at once");
        C(ra.PerYear <= rb.PerYear * 1.05f, $"life goals don't start more often ({rb.PerYear:0.00} and {ra.PerYear:0.00} a colonist-year)");
        C(ra.FightShare <= rb.FightShare + 0.01f, $"the share from fights doesn't rise ({rb.FightShare:P1} and {ra.FightShare:P1})");
        C(rb.mostOnOneFightingGoal <= 1 && ra.mostOnOneFightingGoal <= 1, "never two on one fighting goal from fights or a war at once");

        // ================= Wishes =================

        var wishes = Root("Defs/WishDefs/Rimfolk_Wishes_People4.xml").Elements("ColonyCouncil.WishDef").ToList();
        C(wishes.Count == 7, $"seven wishes ({wishes.Count})");
        string wishCode = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Wishes"), "*.cs").Select(File.ReadAllText));
        var allWishWeights = Directory.GetFiles(Path.Combine(root, "Defs", "WishDefs"), "*.xml").Where(f => !f.Contains("People4"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WishDef"))
            .Select(w => float.Parse((string)w.Element("weight") ?? "1", CultureInfo.InvariantCulture)).OrderBy(x => x).ToList();
        float median = allWishWeights[allWishWeights.Count / 2];
        foreach (var w in wishes)
        {
            string name = (string)w.Element("defName")!;
            string worker = ((string)w.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(wishCode.Contains("class " + worker + " "), $"{name}: its worker {worker} is one");
            C(thoughts.Contains((string)w.Element("grantedThought") ?? ""), $"{name}: its thanks are a mood that exists");
            C(w.Element("triggers") == null, $"{name}: no trigger, so it takes a share of the wish roll and never comes on its own");
            float weight = float.Parse((string)w.Element("weight") ?? "1", CultureInfo.InvariantCulture);
            C(weight <= Math.Max(1f, median), $"{name}: weighs no more than the usual wish ({weight}, the median {median})");
            var request = Lines(w.Element("request")).ToList();
            C(request.Count >= 8 && request.Select(x => x.tone).Distinct().Count() == 6, $"{name}: asked for in all six tones ({request.Count})");
            C(Lines(w.Element("thanks")).Count() >= 4 && Lines(w.Element("expired")).Any(), $"{name}: thanks and a lapse in its own words");
            foreach (var (tone, line) in request.Concat(Lines(w.Element("thanks"))).Concat(Lines(w.Element("expired")))) Words(name, line);
            Words(name, (string)w.Element("fulfilledWhen") ?? "");
            Words(name, (string)w.Element("description") ?? "");
            if (worker == "WishWorker_Stock" || worker == "WishWorker_Have")
            {
                var wants = w.Element("wants");
                C(wants != null && ((string)wants.Element("reason") ?? "").Contains("{PAWN_"), $"{name}: why they want it");
            }
        }
        // The two with workers of their own play out on the map when promised, the game's own work doing it.
        string own = Code("Wishes/WishWorkers_People4.cs");
        C(own.Contains("DesignationDefOf.CutPlant") && own.Contains("areaManager.SnowOrSandClear"), "promised, the trees are marked for cutting and the ways for snow clearing");
        foreach (var k in new[] { "TreesByWalls", "SnowWays" })
            foreach (var key in new[] { "CC_Reason_" + k, "CC_Promise_" + k, "CC_SelfHelp_" + k, "CC_Wish_" + k + "_Progress" })
            {
                C(keyed.ContainsKey(key), $"{key} has its words");
                if (keyed.TryGetValue(key, out var text)) Words(key, text);
            }
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        C(rates.Contains("\"WishWorker_TreesByWalls\"") && rates.Contains("\"WishWorker_SnowWays\""), "the balance model knows how seldom the two can be asked");
        var treesWish = wishes.First(w => (string)w.Element("defName") == "Rimfolk_Wish_TreesByWalls");
        C(Lis(treesWish, "forbiddenBy").Contains("NoTreeCutting"), "never asked by one whose faith minds felling trees");

        // ================= Quirks =================

        var quirks = Root("Defs/QuirkDefs/Rimfolk_Quirks_People4.xml").Elements("ColonyCouncil.QuirkDef").ToList();
        C(quirks.Count == 8, $"eight quirks ({quirks.Count})");
        string bubbles = Code("Immersion/HabitBubbles.cs");
        var pools = new HashSet<string>(Root("Defs/FlavorPoolDefs/Rimfolk_Quirks_People4Bubbles.xml").Elements("ColonyCouncil.FlavorPoolDef").Select(p => (string)p.Element("defName") ?? ""));
        var hediffs = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "HediffDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("HediffDef")).Select(h => (string)h.Element("defName") ?? ""));
        float yearly = 0f;
        foreach (var q in quirks)
        {
            string name = (string)q.Element("defName")!;
            bool hurt = (string)q.Element("afterHurt") == "true";
            var ill = Lis(q, "whileIll");
            var memories = Lis(q, "afterThoughts");
            C(hurt || ill.Count > 0 || memories.Count > 0, $"{name}: comes from something on the map");
            foreach (var h in ill) C(hediffs.Contains(h), $"{name}: the illness {h} is one");
            foreach (var t in memories) C(thoughts.Contains(t), $"{name}: the memory {t} is one");
            string cond = (string)q.Element("condition") ?? "None";
            if (cond != "None") C(thoughts.Contains((string)q.Element("thought") ?? ""), $"{name}: its mood exists");
            var m = Regex.Match(bubbles, "Add\\(\"" + name + "\", \"([A-Za-z_]+)\"");
            C(m.Success && pools.Contains(m.Groups[1].Value), $"{name}: shows over a head now and then, from a pool that exists");
            C(Lines(q.Element("gained")).Select(x => x.tone).Distinct().Count() == 6 && Lines(q.Element("faded")).Any(), $"{name}: gained in six tones, and a word when it fades");
            foreach (var (tone, line) in Lines(q.Element("gained")).Concat(Lines(q.Element("faded")))) Words(name, line);
            float chance = float.Parse((string)q.Element("chance") ?? "0.1", CultureInfo.InvariantCulture);
            float window = float.Parse((string)q.Element("withinDays") ?? "2", CultureInfo.InvariantCulture);
            // How often its moment comes to a colonist in a year of sixty days (the model's own, on the generous side): a hurt
            // from everyday life twice, a serious illness or an ailment of the land once in five years, a founding's night or
            // a parting once in twenty, an evening at a camp's fire once or twice.
            float moments = hurt ? 2f : ill.Count > 0 ? 0.2f : memories.Any(t => t.StartsWith("Council_Ail")) ? 0.2f
                : memories.Any(t => t.StartsWith("Rimfolk_")) ? 0.05f : 1.5f;
            float days = ill.Count > 0 ? (ill[0].Contains("Trench") ? 10f : 3f) : window;
            // Taps the roof comes only to a miner at the face (one colonist in five): its chance at a full share of practice.
            float who = 1f;
            if (Lis(q, "practice").Count > 0)
            {
                chance *= 10f;
                who = 0.2f;
            }
            yearly += who * moments * (1f - (float)Math.Pow(1f - chance, days));
        }
        // Set against the quirks a fight already brings (each fight a colonist comes through, seven a colonist-year: nine of
        // twelve fight every six days), the same way: most colonists already hold the two they can, so the new ones take a
        // share of the places that come free.
        float fights = 0f;
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "QuirkDefs"), "*.xml").Where(f => !f.Contains("People4")))
            foreach (var q in XDocument.Load(file).Root!.Elements("ColonyCouncil.QuirkDef").Where(q => (string)q.Element("afterBattle") == "true"))
            {
                float chance = float.Parse((string)q.Element("chance") ?? "0.1", CultureInfo.InvariantCulture);
                float window = float.Parse((string)q.Element("withinDays") ?? "2", CultureInfo.InvariantCulture);
                fights += 7f * (1f - (float)Math.Pow(1f - chance, window));
            }
        Console.WriteLine($"  people four: the new quirks' moments press {yearly:0.00} quirks a colonist-year, the fights' {fights:0.00}, under the cap of two");
        C(yearly <= 0.1f * fights, $"the new quirks come seldom beside a fight's ({yearly:0.00} and {fights:0.00} a colonist-year, before the cap of two)");
        string quirkCode = Code("Quirks/Quirks.cs");
        C(quirkCode.Contains("d.afterHurt && HurtWithin(p, d.withinDays)") && quirkCode.Contains("IllWith(p, d.whileIll)") && quirkCode.Contains("!p.Downed && Harm.DaysSince(p) < days"),
            "a quirk after a hurt comes once they're up again; one while ill, while they are");
        C(keyed.ContainsKey("CC_QuirkFrom_hurt") && keyed.ContainsKey("CC_QuirkFrom_ill"), "how they came has its words");

        // ================= Names =================

        var names = Root("Defs/EpithetDefs/Rimfolk_Epithets_People4.xml").Elements("ColonyCouncil.EpithetDef").ToList();
        C(names.Count == 8, $"eight names ({names.Count})");
        var oldLabels = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "EpithetDefs"), "*.xml").Where(f => !f.Contains("People4"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.EpithetDef")).Select(e => (string)e.Element("label") ?? ""));
        var oldRecords = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "EpithetDefs"), "*.xml").Where(f => !f.Contains("People4"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.EpithetDef")).Select(e => (string)e.Element("record") ?? ""));
        string renown = Code("Diplomacy/RenownMath.cs"), reps = Code("Social/Gossip/Reputations.cs");
        foreach (var e in names)
        {
            string name = (string)e.Element("defName")!;
            string record = (string)e.Element("record") ?? "";
            C(records.Contains(record), $"{name}: its record {record} is one");
            C(!oldRecords.Contains(record), $"{name}: a name for work no other name counts");
            C(!oldLabels.Contains((string)e.Element("label") ?? ""), $"{name}: a name of its own");
            float threshold = float.Parse((string)e.Element("threshold") ?? "10", CultureInfo.InvariantCulture);
            C(threshold >= 2f && threshold <= 4f, $"{name}: earned after a few of its deeds, not the first ({threshold})");
            string earned = (string)e.Element("earned") ?? "";
            C(earned.Contains("{PAWN_nameDef}") && earned.Contains((string)e.Element("label")!), $"{name}: the words that give it say it");
            Words(name, earned);
            if ((string)e.Element("dubious") != "true") C(reps.Contains("[\"" + record + "\"]"), $"{name}: earned a little sooner by one already known for its side");
        }
        foreach (var name in new[] { "Rimfolk_Epithet_Shield", "Rimfolk_Epithet_NightWatch", "Rimfolk_Epithet_MasterHand", "Rimfolk_Epithet_Lamp", "Rimfolk_Epithet_OpenHand" })
            C(renown.Contains("[\"" + name + "\"]"), $"{name}: what it shows the colony is known for");
        // The one-name rules hold for the new names as for the old: untouched.
        C(NameMath.HoldDays == 20 && NameMath.ChallengeDays == 7 && NameMath.ClearLead == 1.2f && NameMath.ReturnDays == 30, "a name holds 20 days and passes only to a clear surpasser over a week");

        // ================= Deeds =================

        var deedFile = Root("Defs/DeedDefs/Rimfolk_Deeds_People4.xml");
        var deeds = deedFile.Elements("ColonyCouncil.DeedDef").ToList();
        C(deeds.Count == 6, $"six deeds ({deeds.Count})");
        var topics = new HashSet<string>(deedFile.Elements("RulePackDef").Select(r => (string)r.Element("defName") ?? ""));
        var themes = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "DeedDefs"), "*.xml").Where(f => !f.Contains("People4"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DeedDef")).Select(d => (string)d.Element("theme") ?? ""));
        foreach (var d in deeds)
        {
            string name = (string)d.Element("defName")!;
            string text = (string)d.Element("text") ?? "";
            C(text.Length > 0 && text.Length <= 45 && !text.Contains("SUBJECT_"), $"{name}: a short text said after a name ({text.Length})");
            foreach (Match m in Regex.Matches(text + " " + ((string)d.Element("textAlone") ?? ""), @"\{([A-Z_a-z]+)\}"))
                C(m.Groups[1].Value is "VICTIM" or "DETAIL", $"{name}: only {{VICTIM}} and {{DETAIL}} in its text");
            C(topics.Contains((string)d.Element("logTopic") ?? ""), $"{name}: the social log has words for it");
            C(!themes.Contains((string)d.Element("theme") ?? ""), $"{name}: a theme of its own");
            C(Lis(d, "memories").Count + Lis(d, "mentalStates").Count > 0, $"{name}: met where the game already notices it");
            foreach (var t in Lis(d, "memories")) C(thoughts.Contains(t), $"{name}: the memory {t} is one");
            var f = d.Element("facets");
            C(f != null && f.Elements().Any(x => float.Parse(x.Value, CultureInfo.InvariantCulture) != 0f), $"{name}: says something about the doer");
        }
        string heart = Code("Heart/HeartLogic.cs");
        foreach (var s in deeds.SelectMany(d => Lis(d, "mentalStates"))) C(heart.Contains("\"" + s + "\""), $"the break {s} is one of the game's");
        // The six deeds that count for a name, each its own record.
        var counted = new Dictionary<string, string>
        {
            ["Rimfolk_Deed_Rescue"] = "Council_Rescues", ["Rimfolk_Deed_ContestWon"] = "Council_ContestsWon", ["Rimfolk_Deed_Masterwork"] = "Council_Masterworks",
            ["Rimfolk_Deed_StrangerKindness"] = "Council_KindToStrangers", ["Rimfolk_Deed_NightWatch"] = "Council_NightWatches", ["Rimfolk_Deed_Rounds"] = "Council_RoundsMade"
        };
        var allDeeds = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "DeedDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DeedDef"))
            .Select(x => (string)x.Element("defName") ?? ""));
        foreach (var kv in counted)
            C(allDeeds.Contains(kv.Key) && PeopleFourMath.DeedRecord(kv.Key) == kv.Value && records.Contains(kv.Value), $"{kv.Key}: counts {kv.Value} for the names");
        C(PeopleFourMath.DeedRecord("Rimfolk_Deed_Fled") == null && PeopleFourMath.DeedRecord(null) == null, "a deed that counts for no name counts nothing");
        C(Code("Social/Gossip/Deeds.cs").Contains("string record = PeopleFourMath.DeedRecord(def.defName);"), "a deed's record counts each time it's done");
        C(Code("Social/SocialWeb.cs").Contains("CouncilRecords.Add(t.a, CouncilRecords.StudentsTaught);"), "a student standing alone counts for the teacher");

        // ================= Docs =================

        string design = File.ReadAllText(Path.Combine(root, "DESIGN.md")), handoff = File.ReadAllText(Path.Combine(root, "docs", "HANDOFF.md"));
        C(design.Contains("People4") || design.Contains("the colony's own people, the fourth round"), "DESIGN.md says what was added");
        C(handoff.Contains("new wishes, goals and names (the player's request, 6 November 2026)"), "the handoff has its entry");
        return n;
    }

    // ---------------- The life goals' model ----------------

    private sealed class GoalInfo
    {
        public string name;
        public float weight;
        public float share = 1f;
        public List<string> sparks = new List<string>();
        public bool afterHurt, mine;
        public float sparkDays = 4f;
    }

    /// <summary>
    /// The real pool for grown-ups in a colony of one home with no mods: each goal's weight as the tracker gives it (twice
    /// for one asking a passion or a trait), the share of colonists it fits (a passion, an age, a rival, a faith), its
    /// sparks. Children's, teenagers' and other mods' goals are left out; the journey to another colony and a new colony's
    /// growth need a second home.
    /// </summary>
    private static List<GoalInfo> LoadGoals(string root)
    {
        var list = new List<GoalInfo>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "AspirationDefs"), "*.xml"))
        {
            if (file.Contains("_Mods_")) continue;
            foreach (var g in XDocument.Load(file).Root!.Elements("ColonyCouncil.AspirationDef"))
            {
                string name = (string)g.Element("defName") ?? "";
                string worker = ((string)g.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
                var who = g.Element("who");
                if (who?.Element("child")?.Value == "true" || g.Element("growsInto") != null || (string)who?.Element("maxAge") != null) continue;
                if (worker is "AspirationWorker_Visit" or "AspirationWorker_HomeGrows" or "AspirationWorker_GrowUp" or "AspirationWorker_Pokedex") continue;
                var info = new GoalInfo
                {
                    name = name,
                    mine = file.Contains("People4"),
                    weight = float.Parse((string)g.Element("weight") ?? "1", CultureInfo.InvariantCulture),
                    sparks = g.Element("sparks")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>(),
                    sparkDays = float.Parse((string)g.Element("sparkDays") ?? "4", CultureInfo.InvariantCulture),
                    afterHurt = worker == "AspirationWorker_SureFooted"
                };
                bool asks = who?.Element("passion") != null || who?.Element("anyTrait") != null;
                if (asks) info.weight *= 2f;
                if (who?.Element("passion") != null) info.share *= 0.35f;
                if (who?.Element("anyTrait") != null) info.share *= 0.3f;
                if (float.TryParse((string)g.Element("minAge") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out float age) && age >= 50f) info.share *= 0.2f;
                list.Add(info);
            }
        }
        return list;
    }

    private sealed class GoalRun
    {
        public int all, battle, war, hurt, colonistDays, mostOnOneFightingGoal;
        public float PerYear => colonistDays == 0 ? 0f : all * 60f / colonistDays;
        public float FightShare => all == 0 ? 0f : battle / (float)all;
        public float WarShare => all == 0 ? 0f : war / (float)all;
        public float HurtShare => all == 0 ? 0f : hurt / (float)all;
    }

    private static GoalRun SimulateMany(List<GoalInfo> pool, int runs)
    {
        var sum = new GoalRun();
        for (int seed = 0; seed < runs; seed++)
        {
            var r = Simulate(pool, seed);
            sum.all += r.all;
            sum.battle += r.battle;
            sum.war += r.war;
            sum.hurt += r.hurt;
            sum.colonistDays += r.colonistDays;
            sum.mostOnOneFightingGoal = Math.Max(sum.mostOnOneFightingGoal, r.mostOnOneFightingGoal);
        }
        return sum;
    }

    /// <summary>
    /// Twelve grown colonists over 240 days (four years of sixty), the tracker's own rules (GoalMath): a goal reached in 15
    /// to 90 days, then 20 days before the next; choosing at once on a spark at 0.6 times its pull, else one day in five.
    /// What happens, on the generous side and the same on every run before and after (the same seeds): a fight every six
    /// days (nine fight, the three who brought down the most and one downed and one carrying them out sparked), a death every
    /// 40 days (three grieve), a birth every 60 (all), a dream come true once in 40 days each, each memory a goal names once in
    /// 30 days each, a hurt from everyday life once in 25 days each, and a war among the neighbours 18 days in every 150.
    /// </summary>
    private static GoalRun Simulate(List<GoalInfo> pool, int seed)
    {
        var rng = new Random(seed * 7919 + 17);
        const int people = 12, days = 240, fighters = 9;
        // Who each goal fits: drawn once, on the same seed, so the same colonists fit the same old goals before and after.
        var fits = new bool[people, pool.Count];
        var byName = pool.Select((g, i) => (g, i)).ToList();
        for (int p = 0; p < people; p++)
            foreach (var (g, i) in byName)
                fits[p, i] = new Random(seed * 1000 + p * 37 + StableHash(g.name)).NextDouble() < g.share;
        var goal = new int[people];
        var why = new string[people];
        var started = new int[people];
        var length = new int[people];
        var doneAt = new int[people];
        var last = new Dictionary<(int, string), int>();
        for (int p = 0; p < people; p++)
        {
            goal[p] = -1;
            doneAt[p] = -999;
        }
        var run = new GoalRun();
        var thoughtNames = pool.SelectMany(g => g.sparks).Where(s => s.StartsWith("thought:")).Distinct().ToList();
        for (int day = 1; day <= days; day++)
        {
            if (day % 6 == 0)
            {
                var kills = Enumerable.Range(0, fighters).Select(_ => rng.Next(0, 3)).ToList();
                int down = rng.Next(0, fighters), carrier = (down + 1 + rng.Next(0, fighters - 1)) % fighters;
                foreach (int i in GoalMath.Hardest(kills, Enumerable.Range(0, fighters).Select(i => i == down).ToList(), Enumerable.Range(0, fighters).Select(i => i == carrier).ToList()))
                    last[(i, "battle")] = day;
                last[(down, "downed")] = day;
            }
            if (day % 40 == 0)
                for (int k = 0; k < 3; k++) last[(rng.Next(0, people), "loss")] = day;
            if (day % 60 == 0)
                for (int p = 0; p < people; p++) last[(p, "birth")] = day;
            bool war = day % 150 < 18 && day >= 150;
            for (int p = 0; p < people; p++)
            {
                if (rng.NextDouble() < 1.0 / 40) last[(p, "omen")] = day;
                if (rng.NextDouble() < 1.0 / 25) last[(p, "hurt")] = day;
                foreach (var t in thoughtNames)
                    if (rng.NextDouble() < 1.0 / 30) last[(p, t)] = day;
            }
            for (int p = 0; p < people; p++)
            {
                run.colonistDays++;
                if (goal[p] >= 0)
                {
                    if (day - started[p] >= length[p])
                    {
                        goal[p] = -1;
                        why[p] = null;
                        doneAt[p] = day;
                    }
                    continue;
                }
                if (day - doneAt[p] < 20) continue;
                bool Recent(string s, float within) => s == "war" ? war : last.TryGetValue((p, s), out int d) && day - d < within;
                int HeldAfterAFight(int g) => Enumerable.Range(0, people).Count(j => j != p && goal[j] == g && GoalMath.FightSpark(why[j]));
                int Held(int g) => Enumerable.Range(0, people).Count(j => j != p && goal[j] == g);
                var options = new List<(int g, string spark, float weight)>();
                for (int g = 0; g < pool.Count; g++)
                {
                    var info = pool[g];
                    if (!fits[p, g]) continue;
                    if (info.afterHurt && !Recent("hurt", PeopleFourMath.RecentHurtDays)) continue;
                    string best = null;
                    float bestPull = 0f;
                    foreach (var s in info.sparks)
                    {
                        if (s == "fulfilled" || s == "nemesis" || s == "grewup" || !GoalMath.FightMaySpark(s, HeldAfterAFight(g))) continue;
                        if (!Recent(s, info.sparkDays)) continue;
                        float pull = GoalMath.Big(s) ? 2f : GoalMath.SparkPull(s);
                        if (best == null || pull > bestPull)
                        {
                            best = s;
                            bestPull = pull;
                        }
                    }
                    options.Add((g, best, info.weight * GoalMath.Shared(Held(g))));
                }
                if (options.Count == 0) continue;
                var sparked = options.Where(o => o.spark != null).ToList();
                (int g, string spark, float weight)? pick = null;
                if (sparked.Count > 0 && rng.NextDouble() < GoalMath.SparkedChooseChance * sparked.Max(o => GoalMath.SparkPull(o.spark)))
                    pick = Weighted(rng, sparked, o => o.weight * GoalMath.SparkPull(o.spark));
                else if (rng.NextDouble() < 0.2)
                    pick = Weighted(rng, options, o => o.weight * (o.spark != null ? 6f * GoalMath.SparkPull(o.spark) : 1f));
                if (pick == null) continue;
                var c = pick.Value;
                goal[p] = c.g;
                why[p] = c.spark;
                started[p] = day;
                length[p] = rng.Next(15, 91);
                run.all++;
                if (c.spark == "battle") run.battle++;
                if (c.spark == "war") run.war++;
                if (c.spark == "hurt") run.hurt++;
                if (GoalMath.FightSpark(c.spark)) run.mostOnOneFightingGoal = Math.Max(run.mostOnOneFightingGoal, 1 + HeldAfterAFight(c.g));
            }
        }
        return run;
    }

    private static T Weighted<T>(Random rng, List<T> from, Func<T, float> weight)
    {
        float total = from.Sum(weight), r = (float)rng.NextDouble() * total;
        foreach (var o in from)
        {
            r -= weight(o);
            if (r <= 0f) return o;
        }
        return from[from.Count - 1];
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
