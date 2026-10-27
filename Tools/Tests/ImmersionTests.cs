using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// What colonists say about themselves and the weather, and the small habits (Source/ColonyCouncil/Immersion/): every
/// line said over a head is one sentence of 90 characters at most and doesn't start with a quote (the floating text
/// keeps only the first sentence and trims quotes from the ends); every tag naming a feeling, an ailment, a setback or
/// a personal story names one that exists, so a typo can't leave a line unsaid; every pool the code and the habits'
/// jobs name exists, and each of the twelve habits has a job of its own. How many feelings, ailments, setbacks and stories have lines of their own is reported, not
/// required: the pools' untagged lines cover the rest, and a feeling without lines is simply not said.
/// </summary>
public static class ImmersionTests
{
    private static readonly string[] SpokenPools =
    {
        "Council_Pool_SayFeeling", "Council_Pool_SayAilment", "Council_Pool_SayConvalescing", "Council_Pool_SayStory", "Council_Pool_SayTomorrow",
        "Council_Pool_SaySetback", "Council_Pool_SayTakenBack", "Council_Pool_SayNemesis", "Council_Pool_SayWish", "Council_Pool_SayFirstSnow",
        "Council_Pool_SayHeatWave", "Council_Pool_SayColdSnap", "Council_Pool_SayStorm", "Council_Pool_HabitGrave", "Council_Pool_HabitLookOut",
        "Council_Pool_HabitFire", "Council_Pool_HabitRemember", "Council_Pool_HabitStars", "Council_Pool_HabitPlants", "Council_Pool_HabitAnimals",
        "Council_Pool_HabitStores", "Council_Pool_HabitCompany", "Council_Pool_HabitStories", "Council_Pool_SayDream", "Council_Pool_TouchCampComing",
        "Council_Pool_TouchCouncilWedding", "Council_Pool_GuestSays", "Council_Pool_SayFamilyYears", "Council_Pool_SayReunion", "Council_Pool_SayKinRaider",
        // The family's life together (Social/FamilyLife.cs).
        "Council_Pool_SayNewCouple", "Council_Pool_SayMatchDoubt", "Council_Pool_SayParentNewPartner", "Council_Pool_SayEngaged", "Council_Pool_SayProposalComfort", "Council_Pool_SayExpecting",
        "Council_Pool_SayGrandchild", "Council_Pool_PlayGrowth",
        // The family's days together (Social/FamilyBonds.cs).
        "Council_Pool_PlayFamilySupper", "Council_Pool_SaySiblingClose", "Council_Pool_SaySiblingRival",
        // Habits shown on the map (Immersion/QuirkHabits.cs, HabitBubbles.cs).
        "Council_Pool_HabitTidy", "Council_Pool_HabitSunrise", "Council_Pool_HabitStones", "Council_Pool_HabitBirds", "Council_Pool_HabitWhittle",
        "Council_Pool_HabitWhetstone", "Council_Pool_HabitTreeLine", "Council_Pool_HabitPatient", "Council_Pool_HabitBirthday",
        "Council_Pool_BubbleHums", "Council_Pool_BubbleStove", "Council_Pool_BubbleSleepTalk", "Council_Pool_BubbleBestBite", "Council_Pool_BubbleMeasure",
        "Council_Pool_BubbleHaul", "Council_Pool_BubbleLateHours", "Council_Pool_BubbleFidgets", "Council_Pool_BubbleReads", "Council_Pool_BubbleWarden",
        "Council_Pool_BubbleMends", "Council_Pool_BubbleDoodles", "Council_Pool_BubbleLightSleep",
        // The quirks the colony's events leave behind (Quirks/QuirkEvents.cs).
        "Council_Pool_BubbleLamp", "Council_Pool_BubbleSmoke", "Council_Pool_BubbleCoins", "Council_Pool_BubbleFields", "Council_Pool_BubbleDreamBook",
        "Council_Pool_BubbleSalve", "Council_Pool_BubbleJuggles", "Council_Pool_BubbleColdTrick"
    };

    private static readonly string[] JournalPools =
    {
        "Council_Journal_FirstSnowSeason", "Council_Journal_HeatWave", "Council_Journal_ColdSnap", "Council_Journal_Storm", "Council_Journal_Changed",
        "Council_Journal_NewLove", "Council_Journal_Engaged", "Council_Journal_ProposalRefused", "Council_Journal_Expecting", "Council_Journal_Loss", "Council_Journal_ChildGrew",
        "Council_Journal_TookIn", "Council_Journal_TakenIn", "Council_Journal_InheritedRoom", "Council_Journal_OldWeddingDay",
        "Council_Journal_TookName", "Council_Journal_LastSeason"
    };

    private static readonly string[] Prefixes = { "Council_Setback_", "Council_Friction_", "Council_Nuisance_", "Council_Arc_" };

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "immersion: " + what);
        }

        // ---------- Every pool's lines, its own and those added to it from other files ----------
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
        {
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                string name = el.Name.LocalName == "ColonyCouncil.FlavorPoolDef" ? el.Element("defName")?.Value
                    : el.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" ? el.Element("pool")?.Value : null;
                if (name == null) continue;
                if (!pools.TryGetValue(name, out var lines)) pools[name] = lines = new List<string>();
                var block = el.Element("lines");
                if (block != null) lines.AddRange(block.Descendants("li").Select(li => li.Value));
            }
        }
        foreach (var pool in SpokenPools.Concat(JournalPools)) Check(pools.TryGetValue(pool, out var l) && l.Count >= 6, $"{pool} exists with lines");

        // ---------- Said over a head: one short sentence each ----------
        foreach (var pool in SpokenPools)
        {
            if (!pools.TryGetValue(pool, out var lines)) continue;
            foreach (var line in lines)
            {
                string body = Strip(line, out _);
                Check(!body.StartsWith("\"") && !body.EndsWith("\""), $"{pool}: '{body}' doesn't start or end with a quote");
                Check(FirstSentence(body) == body, $"{pool}: '{body}' is one sentence");
                Check(body.Length <= 90, $"{pool}: '{body}' is 90 characters at most ({body.Length})");
                Check(body.Count(c => c == '!') <= 1, $"{pool}: '{body}' has one '!' at most");
            }
        }

        // ---------- Tags name real things ----------
        var feelings = Keys(root, "HeartDefs", "key");
        var ailments = Keys(root, "HediffDefs", "key");
        // Setbacks, and the frictions and nuisances that may be said too (those with lines of their own).
        var setbacks = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "ThoughtDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                string name = el.Element("defName")?.Value ?? "";
                if (el.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.SetbackExtension")
                    || name.StartsWith("Council_Friction_") || name.StartsWith("Council_Nuisance_"))
                    setbacks.Add(Short(name));
            }
        var stories = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "ArcDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                if (el.Name.LocalName == "ColonyCouncil.ArcDef" && el.Element("stages") != null)
                    stories.Add(Short(el.Element("defName")?.Value ?? ""));
        Check(feelings.Count >= 20 && ailments.Count >= 40 && setbacks.Count >= 30 && stories.Count >= 10, "found the feelings, ailments, setbacks and stories");
        var said = new Dictionary<string, HashSet<string>> { ["feeling"] = new(), ["ail"] = new(), ["setback"] = new(), ["story"] = new() };
        var known = new Dictionary<string, HashSet<string>> { ["feeling"] = feelings, ["ail"] = ailments, ["setback"] = setbacks, ["story"] = stories };
        foreach (var pool in SpokenPools)
        {
            if (!pools.TryGetValue(pool, out var lines)) continue;
            foreach (var line in lines)
            {
                Strip(line, out var tags);
                foreach (var alt in tags.SelectMany(t => t.Split('|')))
                {
                    string tag = alt.Trim().TrimStart('!');
                    int colon = tag.IndexOf(':');
                    if (colon < 0) continue;
                    string kind = tag.Substring(0, colon), value = tag.Substring(colon + 1);
                    if (!known.TryGetValue(kind, out var names)) continue;
                    Check(names.Contains(value), $"{pool}: [{tag}] names a {kind} that exists");
                    said[kind].Add(value);
                }
            }
        }
        foreach (var kind in said.Keys)
            Console.WriteLine($"  Lines of their own for {said[kind].Count(known[kind].Contains)} of {known[kind].Count} {(kind == "ail" ? "ailment" : kind)} kinds.");

        // ---------- The habits' jobs name pools that exist ----------
        var habits = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_SmallHabits.xml")).Root!;
        foreach (var ext in habits.Descendants("li").Where(li => (string)li.Attribute("Class") == "ColonyCouncil.HabitExtension"))
        {
            string pool = ext.Element("pool")?.Value;
            Check(pool != null && pools.ContainsKey(pool), $"a small habit's pool {pool} exists");
        }
        Check(habits.Elements("JoyGiverDef").Count() == 12, "twelve small habits");
        // Every habit's job has a kind of its own, and every kind the code knows has a job.
        var kinds = habits.Descendants("li").Where(li => (string)li.Attribute("Class") == "ColonyCouncil.HabitExtension").Select(li => li.Element("kind")?.Value).ToList();
        Check(kinds.Count == kinds.Distinct().Count(), "each habit's job has a kind of its own");
        foreach (var k in new[] { "Grave", "LookOut", "Fire", "Remember", "Stars", "Plants", "Animals", "Stores", "Company", "Stories" })
            Check(kinds.Contains(k), $"a small habit's job for {k}");
        // Each joy giver names a job that exists here or in the game (the snowman is the game's own).
        var jobDefs = new HashSet<string>(habits.Elements("JobDef").Select(j => j.Element("defName")?.Value ?? ""));
        foreach (var giver in habits.Elements("JoyGiverDef"))
        {
            string job = giver.Element("jobDef")?.Value ?? "";
            Check(jobDefs.Contains(job) || job == "BuildSnowman", $"joy giver {giver.Element("defName")?.Value} names a job that exists ({job})");
        }
        return checks;
    }

    private static HashSet<string> Keys(string root, string folder, string element)
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", folder), "*.xml"))
            foreach (var el in XDocument.Load(file).Descendants(element))
                if (!string.IsNullOrWhiteSpace(el.Value)) keys.Add(el.Value.Trim());
        return keys;
    }

    private static string Short(string defName)
    {
        foreach (var p in Prefixes)
            if (defName.StartsWith(p)) return defName.Substring(p.Length);
        return defName;
    }

    /// <summary>"[a,b] text" into its tags and the text.</summary>
    private static string Strip(string line, out string[] tags)
    {
        tags = Array.Empty<string>();
        string t = line.TrimStart();
        if (!t.StartsWith("[")) return t.Trim();
        int end = t.IndexOf(']');
        if (end < 0) return t.Trim();
        tags = t.Substring(1, end - 1).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        return t.Substring(end + 1).Trim();
    }

    /// <summary>The first sentence: up to the first '.', '!' or '?' from the 13th character on (what always shows; SpeechMath.Shorten).</summary>
    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }
}
