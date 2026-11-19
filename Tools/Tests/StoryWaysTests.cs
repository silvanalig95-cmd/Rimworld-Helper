using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The fourteen ways to play and the story direction (Source/ColonyCouncil/Story/, Defs/StorytellerDefs/Rimfolk_Storytellers.xml;
/// DESIGN.md "Ways to play"): the levers' math (the floor, harm never above 1, the shapes' means over a year and an arc, the
/// arc's two years, its phases and troubles, the deeds, the reasons for a raid, the tilt of the hurts within the same budget,
/// an event's kinds, the joiners' share); every storyteller def (its comps, every kind of event with a route, nothing shut
/// out, its plate, its description saying how it plays, its levers at the floor or above, its threats at the classic pace
/// or less; the six after the first eight at the game's own rate or more, on the classic cycle, with no shape, their picks
/// and quests real and soft); Rimfolk's events leaned by their kinds; every direction choice; and the seams where
/// Rimfolk's schedulers read the levers.
/// </summary>
public static class StoryWaysTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "ways to play: " + what);
        }

        // ================= The ways =================

        C(StoryWaysMath.Ways.Length == 14 && StoryWaysMath.Ways.Distinct().Count() == 14 && !StoryWaysMath.Ways.Contains(StoryWay.None),
            "fourteen ways, Small Hours and The Frontier left out");
        C(Enum.GetValues(typeof(StoryWay)).Length == 15, "the direction offers none and the fourteen");
        // Saves and settings keep a way by its number: the first eight keep theirs, the six after come at the end.
        C((int)StoryWay.Crossroads == 8 && (int)StoryWay.OpenGate == 9 && (int)StoryWay.FeastDays == 14 && StoryWaysMath.Ways.Take(8).Select(w => (int)w).SequenceEqual(Enumerable.Range(1, 8)),
            "the first eight keep their numbers, the six after come at the end");
        C(StoryWaysMath.AtBaseOrMore.Length == 6 && StoryWaysMath.AtBaseOrMore.SequenceEqual(StoryWaysMath.Ways.Skip(8)), "the six after the first eight are the ones at the game's rate or more");
        foreach (var w in StoryWaysMath.Ways) C(StoryWaysMath.WayOf(StoryWaysMath.DefName(w)) == w, w + ": its def name reads back");
        C(StoryWaysMath.DefName(StoryWay.None) == null && StoryWaysMath.WayOf("Cassandra") == StoryWay.None && StoryWaysMath.WayOf(null) == StoryWay.None,
            "none has no def; the game's storytellers are no way");
        C(StoryWaysMath.Active(StoryWay.Crossroads, StoryWay.Neighbours) == StoryWay.Crossroads && StoryWaysMath.Active(StoryWay.None, StoryWay.Neighbours) == StoryWay.Neighbours
          && StoryWaysMath.Active(StoryWay.None, StoryWay.None) == StoryWay.None, "the direction leans first, then the storyteller's own way");

        // ================= The levers: the floor and harm =================

        C(Math.Abs(StoryWaysMath.Floor - 0.25f) < 1e-6 && StoryWaysMath.MaxRaidReasons < 1f, "the floor is a quarter; the raids' reasons never take every raid");
        foreach (var key in StoryWaysMath.Keys)
        {
            var levers = new StoryLevers();
            C(Math.Abs(levers.Get(key) - StoryWaysMath.Neutral(key)) < 1e-6, key + ": a lever is neutral until a way sets it");
            if (key == StoryWaysMath.RaidReasons)
            {
                C(StoryWaysMath.Clamp(key, 1f) <= StoryWaysMath.MaxRaidReasons && StoryWaysMath.Clamp(key, -1f) == 0f, "the raids' reasons are a share, never all");
                continue;
            }
            C(StoryWaysMath.Clamp(key, 0f) >= StoryWaysMath.Floor && StoryWaysMath.Clamp(key, float.NaN) == 1f, key + ": never below the floor");
            C(StoryWaysMath.Clamp(key, 10f) <= (StoryWaysMath.IsHarm(key) ? 1f : StoryWaysMath.MaxLever), key + (StoryWaysMath.IsHarm(key) ? ": harm, never above 1" : ": never past three times"));
        }
        C(StoryWaysMath.HarmKeys.All(StoryWaysMath.IsKey) && StoryWaysMath.IsHarm(StoryWaysMath.Fights) && StoryWaysMath.IsHarm(StoryWaysMath.Schemes), "fights and schemes are the levers on harm");
        C(StoryWaysMath.Keys.Distinct().Count() == StoryWaysMath.Keys.Length && StoryWaysMath.LeanKeys.All(StoryWaysMath.IsKey) && !StoryWaysMath.LeanKeys.Any(StoryWaysMath.IsHarm)
          && !StoryWaysMath.IsLeanKey(StoryWaysMath.Fights) && !StoryWaysMath.IsLeanKey(StoryWaysMath.Joiners) && StoryWaysMath.IsKey(StoryWaysMath.Joiners),
            "an event's kinds are levers of the ways, none on harm; joiners read from who may join");

        // An event's kinds: its group's lever times the strongest of its kinds', within the floor and three times.
        C(StoryWaysMath.HappeningLean(1.3f, new float[0]) == 1.3f && StoryWaysMath.HappeningLean(1.3f, null) == 1.3f, "an event of no kind takes its group's lever");
        C(Math.Abs(StoryWaysMath.HappeningLean(1f, new[] { 2.5f }) - 2.5f) < 1e-6 && Math.Abs(StoryWaysMath.HappeningLean(1.2f, new[] { 2f, 1f }) - 2.4f) < 1e-5,
            "a kind's lever times the group's");
        C(Math.Abs(StoryWaysMath.HappeningLean(1f, new[] { 2f, 2f }) - 2f) < 1e-6, "an event of two kinds is not counted twice");
        C(StoryWaysMath.HappeningLean(2f, new[] { 3f }) == StoryWaysMath.MaxLever && StoryWaysMath.HappeningLean(0.25f, new[] { 0.25f }) == StoryWaysMath.Floor
          && StoryWaysMath.HappeningLean(float.NaN, new[] { float.NaN }) == 1f, "an event's lever stays within the floor and three times");
        // The joiners' share: the game's sense of how many people the colony should have thins them, never past every time.
        C(StoryWaysMath.PickAccept(new[] { 1f, 1f }, new[] { 1f, 1f }) == 1f && Math.Abs(StoryWaysMath.PickAccept(new[] { 1f, 1f }, new[] { 1f, 0.05f }) - 0.525f) < 1e-4
          && StoryWaysMath.PickAccept(new[] { 1f }, new[] { 3f }) == 1f && StoryWaysMath.PickAccept(new float[0], new float[0]) == 0f && StoryWaysMath.PickAccept(null, null) == 1f,
            "picks that bring people come less to a colony big enough, the rest as ever");

        // The shapes: a quarter at least, harm 1 or less over its cycle, the rest about 1.
        foreach (var key in StoryWaysMath.SeasonKeys)
        {
            float mean = StoryWaysMath.SeasonMean(key);
            C(StoryWaysMath.IsHarm(key) ? mean <= 1.0001f : Math.Abs(mean - 1f) < 0.03f, $"{key}: over a year the seasons average {mean:0.00}");
            for (int q = 0; q < 4; q++) C(StoryWaysMath.SeasonFactor(key, q) >= StoryWaysMath.Floor, $"{key}: season {q} keeps a chance");
        }
        foreach (var key in StoryWaysMath.ArcKeys)
        {
            foreach (ArcPhase p in Enum.GetValues(typeof(ArcPhase)))
                C(StoryWaysMath.ArcFactor(key, p) >= StoryWaysMath.Floor, $"{key}: the arc's {p} keeps a chance");
            if (StoryWaysMath.IsHarm(key)) C(StoryWaysMath.ArcMean(key) <= 1.0001f, $"{key}: harm over an arc averages {StoryWaysMath.ArcMean(key):0.00}, never more than 1");
        }
        foreach (var key in StoryWaysMath.DeedKeys)
        {
            C(StoryWaysMath.DeedFactor(key, 1f) >= StoryWaysMath.Floor && StoryWaysMath.DeedFactor(key, -1f) >= StoryWaysMath.Floor && Math.Abs(StoryWaysMath.DeedFactor(key, 0f) - 1f) < 1e-6,
                key + ": the deeds lean it both ways from 1, never below the floor");
            if (StoryWaysMath.IsHarm(key)) C(StoryWaysMath.DeedFactor(key, -1f) <= 1f && StoryWaysMath.DeedFactor(key, 1f) <= 1f, key + ": cruelty never brings more harm than the game's own rate");
        }
        C(StoryWaysMath.DeedsBalance(0f, 0f) == 0f && StoryWaysMath.DeedsBalance(30f, 0f) > 0.5f && StoryWaysMath.DeedsBalance(0f, 30f) < -0.5f && StoryWaysMath.DeedsBalance(float.NaN, 5f) < 0f,
            "the deeds: none is no name, good turns kind, wrongs cruel");
        C(StoryWaysMath.DeedThreatAccept(-1f) == 1f && StoryWaysMath.DeedThreatAccept(0f) == 1f && StoryWaysMath.DeedThreatAccept(1f) == 1f,
            "What You Sow: raids at the classic pace whatever the deeds; the deeds decide who comes");
        foreach (var lever in new[] { StoryWaysMath.Traders, StoryWaysMath.Visitors, StoryWaysMath.Allies })
            C(StoryWaysMath.DeedVisitAccept(lever, -1f) >= StoryWaysMath.Floor && StoryWaysMath.DeedVisitAccept(lever, 1f) <= 1f, lever + ": a cruel colony's roads thin, never close");
        for (int q = 0; q < 4; q++)
        {
            C(StoryWaysMath.SeasonThreatAccept(q) >= StoryWaysMath.Floor && StoryWaysMath.SeasonThreatAccept(q) <= 1f, $"the Turning Year's threats keep a chance in season {q}");
            foreach (var lever in new[] { StoryWaysMath.Traders, StoryWaysMath.Visitors, StoryWaysMath.Allies })
                C(StoryWaysMath.SeasonVisitAccept(lever, q) >= StoryWaysMath.Floor, $"{lever}: some in season {q}");
        }
        C(StoryWaysMath.SeasonThreatAccept(StoryWaysMath.Spring) < StoryWaysMath.SeasonThreatAccept(StoryWaysMath.Winter), "quiet springs, hungry winters");
        float seasonDisease = Enumerable.Range(0, 4).Average(q => StoryWaysMath.SeasonDisease(q));
        C(Math.Abs(seasonDisease - 1f) < 0.01f && StoryWaysMath.SeasonDisease(StoryWaysMath.Spring) > 1.5f, $"sickness with the thaw, the game's own rate over a year ({seasonDisease:0.00})");
        float arcDisease = StoryWaysMath.ArcMeanOver(StoryWaysMath.ArcDisease);
        C(arcDisease >= 0.9f && arcDisease <= 1.05f, $"Rise and Fall's sickness about the game's own over an arc ({arcDisease:0.00})");
        float arcThreats = StoryWaysMath.ArcMeanOver(StoryWaysMath.ArcThreatAccept);
        C(arcThreats > 0.4f && arcThreats < 0.6f, $"Rise and Fall's threats average {arcThreats:0.00} of their peak over an arc");
        foreach (ArcPhase p in Enum.GetValues(typeof(ArcPhase)))
        {
            C(StoryWaysMath.ArcVisitAccept(StoryWaysMath.Traders, p) >= StoryWaysMath.Floor && StoryWaysMath.ArcVisitAccept(StoryWaysMath.Allies, p) >= StoryWaysMath.Floor, $"traders and friends in the arc's {p}");
            foreach (StoryTrouble t in Enum.GetValues(typeof(StoryTrouble)))
                C(StoryWaysMath.ArcThreatAccept(p, t) >= StoryWaysMath.Floor && StoryWaysMath.ArcDisease(p, t) >= StoryWaysMath.Floor, $"threats and sickness keep a chance in the {p} of {t}");
        }

        // The arc's phases, troubles and omen.
        C(StoryWaysMath.PhaseAt(0f) == ArcPhase.Growth && StoryWaysMath.PhaseAt(59.9f) == ArcPhase.Growth && StoryWaysMath.PhaseAt(60f) == ArcPhase.Omen
          && StoryWaysMath.PhaseAt(80f) == ArcPhase.Trouble && StoryWaysMath.PhaseAt(100f) == ArcPhase.Rebuild && StoryWaysMath.PhaseAt(120f) == ArcPhase.Growth
          && StoryWaysMath.PhaseAt(float.NaN) == ArcPhase.Growth, "an arc: growth, omens, trouble, rebuilding, then the next");
        C(Enum.GetValues(typeof(ArcPhase)).Cast<ArcPhase>().Sum(p => StoryWaysMath.PhaseDays(p)) == StoryWaysMath.ArcDays, "the phases fill the arc");
        // The player, 15 November 2026: "rise and fall, just do 1 year rise and 1 year fall".
        bool riseYear = true, fallYear = true;
        for (float d = 0f; d < StoryWaysMath.ArcDays * 3f; d += 0.5f)
        {
            bool fall = StoryWaysMath.ArcDay(d) >= StoryWaysMath.YearDays;
            if (fall != StoryWaysMath.FallYear(d)) riseYear = false;
            if (!fall && StoryWaysMath.PhaseAt(d) != ArcPhase.Growth) riseYear = false;
            if (fall && StoryWaysMath.PhaseAt(d) == ArcPhase.Growth) fallYear = false;
        }
        C(StoryWaysMath.YearDays == 60f && StoryWaysMath.ArcDays == 2f * StoryWaysMath.YearDays && StoryWaysMath.PhaseDays(ArcPhase.Growth) == StoryWaysMath.YearDays && riseYear,
            "the rise: the arc's first year, all growth");
        C(StoryWaysMath.PhaseDays(ArcPhase.Omen) + StoryWaysMath.PhaseDays(ArcPhase.Trouble) + StoryWaysMath.PhaseDays(ArcPhase.Rebuild) == StoryWaysMath.YearDays && fallYear
          && StoryWaysMath.PhaseStart(ArcPhase.Rebuild) < StoryWaysMath.ArcDays && StoryWaysMath.PhaseDays(ArcPhase.Rebuild) > 0f,
            "the fall: the arc's second year, the omens, the trouble and the rebuilding at its end");
        bool varied = true, allFive = true;
        for (int seed = -20; seed < 60; seed++)
        {
            for (int c = 0; c < 20; c++)
                if (StoryWaysMath.TroubleOf(c, seed) == StoryWaysMath.TroubleOf(c + 1, seed))
                    varied = false;
            if (Enumerable.Range(0, 5).Select(c => StoryWaysMath.TroubleOf(c, seed)).Distinct().Count() != 5) allFive = false;
        }
        C(varied && allFive, "never the same trouble twice running; every trouble within five arcs");
        bool omenDays = true;
        for (int seed = -50; seed < 50; seed++)
        for (int c = 0; c < 10; c++)
        {
            int d = StoryWaysMath.OmenDay(c, seed);
            if (d < 2 || d > 10 || d >= StoryWaysMath.PhaseDays(ArcPhase.Omen)) omenDays = false;
        }
        C(omenDays, "the omen's word comes a few days into the omens, a different day each arc, never on their first");
        foreach (StoryTrouble t in Enum.GetValues(typeof(StoryTrouble))) C(StoryWaysMath.TroubleKinds(t).Length > 0, t + ": has kinds of world situation");

        // Variety, reasons, the tilt.
        C(Math.Abs(StoryWaysMath.PickRecency(0f) - 0.1f) < 1e-6 && StoryWaysMath.PickRecency(StoryWaysMath.PickVarietyDays) == 1f && StoryWaysMath.PickRecency(float.NaN) == 1f
          && StoryWaysMath.PickRecency(5f) < StoryWaysMath.PickRecency(10f), "the pick that came last is much less likely for a while");
        C(StoryWaysMath.Grievance(0f, 0f, 0) == 0f && StoryWaysMath.Grievance(5f, 0f, 0) == 5f && StoryWaysMath.Grievance(0f, 8f, 0) == 2f
          && Math.Abs(StoryWaysMath.Grievance(0f, 0f, -100) - 3f) < 1e-6 && StoryWaysMath.Grievance(float.NaN, 0f, -40) == 0f, "the reasons to raid: wrongs, an old enmity, a cold goodwill");
        C(StoryWaysMath.SituationFactor("war", new[] { "war" }, null) == StoryWaysMath.SituationLean && StoryWaysMath.SituationFactor("trade", new[] { "war" }, new[] { "trade" }) == StoryWaysMath.SituationLean
          && StoryWaysMath.SituationFactor("fair", new[] { "war" }, null) == 1f && StoryWaysMath.SituationFactor(null, null, null) == 1f, "a leaned kind of world situation weighs more, the rest as ever");
        C(StoryWaysMath.HarmTilt("coldNight", new[] { "coldNight" }, 2f) == 2f && StoryWaysMath.HarmTilt("drills", new[] { "coldNight" }, 2f) == 1f
          && StoryWaysMath.HarmTilt("coldNight", new[] { "coldNight" }, 50f) <= 3f, "the hurts' tilt");
        // The tilt keeps the budget: the founding kinds with a few leaned come to the same total.
        var founding = HarmKinds.All.Where(k => k.founding).ToList();
        var lean = new[] { "landAilments", "coldNight", "sabotage" };
        float budget = founding.Sum(k => Math.Max(0f, k.weight));
        float shared = founding.Sum(k => k.weight * StoryWaysMath.HarmTilt(k.key, lean, 2f));
        float total = founding.Sum(k => k.weight * HarmBudgetMath.PassFor(StoryWaysMath.HarmTilt(k.key, lean, 2f), budget, shared));
        C(Math.Abs(total - budget) < 1e-4f, $"a way moves the everyday hurts between kinds, the budget the same ({total:0.000} of {budget:0.000})");

        // ================= The storyteller defs =================

        string path = Path.Combine(root, "Defs", "StorytellerDefs", "Rimfolk_Storytellers.xml");
        C(File.Exists(path), "the ways' storytellers are defined");
        if (!File.Exists(path)) return n;
        var doc = XDocument.Load(path);
        var defs = doc.Root!.Elements("StorytellerDef").ToList();
        C(defs.Count == 14, "fourteen storyteller defs");
        var labels = new Dictionary<StoryWay, string>
        {
            [StoryWay.LongRoad] = "The Long Road", [StoryWay.Neighbours] = "Neighbours", [StoryWay.TurningYear] = "The Turning Year", [StoryWay.WhatYouSow] = "What You Sow",
            [StoryWay.OldPowers] = "Old Powers", [StoryWay.LeanYears] = "Lean Years", [StoryWay.RiseAndFall] = "Rise and Fall", [StoryWay.Crossroads] = "Crossroads",
            [StoryWay.OpenGate] = "Open Gate", [StoryWay.HomeAndHearth] = "Home and Hearth", [StoryWay.TheWilds] = "The Wilds", [StoryWay.Windfalls] = "Windfalls",
            [StoryWay.Sellswords] = "Sellswords", [StoryWay.FeastDays] = "Feast Days"
        };
        var pool = Pool(root);
        var categories = pool.Where(r => r.type == "IncidentCategoryDef" && r.active).Select(r => r.name).Distinct().ToList();
        C(categories.Contains("ThreatBig") && categories.Contains("FactionArrival") && categories.Count >= 12, $"the incident categories in the game and the player's mods ({categories.Count})");
        var situationKinds = Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements().Select(e => e.Element("kind")?.Value)).Where(k => !string.IsNullOrEmpty(k)).ToHashSet();
        string source = Path.Combine(root, "Source", "ColonyCouncil");
        string storyComps = File.ReadAllText(Path.Combine(source, "Story", "StoryComps.cs"));
        // What the categories covered by their own code are, and why: a test fails on a new category nobody has looked at.
        var byCode = new Dictionary<string, string>
        {
            ["Council_WorldEvent"] = "Rimfolk's own timer fires it (with Rimfolk's events off the storyteller)",
            ["GR_HybridRaid"] = "Vanilla Genetics Expanded's one event in it has no chance of its own; the mod's code fires it",
            ["DeepDrillInfestation"] = "the game's BaseStoryteller carries its comp, inherited",
            ["EndGame_ShipEscape"] = "the game's BaseStoryteller carries its quest, inherited",
            ["Special"] = "fired by name: the stranger in black here, the anima and polux trees, Gauranlen pods and the monolith in BaseStoryteller"
        };
        // The categories of the game's incidents each way names (read from the game's own defs).
        var incidentCategory = new Dictionary<string, string>
        {
            ["TraderCaravanArrival"] = "FactionArrival", ["VisitorGroup"] = "FactionArrival", ["TravelerGroup"] = "FactionArrival", ["RaidFriendly"] = "AllyAssistance",
            ["OrbitalTraderArrival"] = "OrbitalVisitor", ["StrangerInBlackJoin"] = "Special", ["AnimalInsanityMass"] = "ThreatBig", ["Infestation"] = "ThreatBig",
            ["SightstealerSwarm"] = "ThreatBig", ["UnnaturalDarkness"] = "ThreatBig", ["DeathPall"] = "ThreatBig", ["BloodRain"] = "ThreatBig", ["HateChanters"] = "ThreatBig", ["VREA_PsychicStorm"] = "ThreatBig"
        };
        // The game's and the mods' own threats and hurts by name, which the six after the first eight never name: they bring
        // more of their own kind of thing and leave the threats to the classic cycle.
        var threatPicks = new HashSet<string>
        {
            "ManhunterPack", "AnimalInsanityMass", "AnimalInsanitySingle", "Infestation", "RaidEnemy", "PsychicDrone", "MechCluster", "ToxicFallout",
            "VolcanicWinter", "Flashstorm", "SolarFlare", "HeatWave", "ColdSnap", "CropBlight", "Alphabeavers", "ShortCircuit", "VEE_WandererJoinsTraitor",
            "VRE_ChestburstWandererJoinQuest", "CreepJoinerJoin", "CreepJoinerJoin_Metalhorror", "WanderingLich", "RG_Rimclaw_RimclawsPasses", "VEE_ShuttleCrash"
        };
        // Fired by hand in the game, never by chance: no way names them.
        var manual = new[] { "Drought", "DroughtInitial", "WanderersSkylantern", "HarbingerTreeSpawn", "MechCluster", "Infestation_Jelly", "StrangerInBlackJoin" };
        // The classic storyteller's big threats a day: its cycle of 4.6 days on and 6 off, one or two each.
        const float classicBig = 1.5f / 10.6f;
        int order = -1;
        for (int i = 0; i < defs.Count; i++)
        {
            var d = defs[i];
            string defName = d.Element("defName")?.Value ?? "?";
            var way = StoryWaysMath.WayOf(defName);
            string w = defName;
            C(way != StoryWay.None && i < StoryWaysMath.Ways.Length && way == StoryWaysMath.Ways[i], w + ": a way, in the list's order");
            bool atBase = StoryWaysMath.AtBaseOrMore.Contains(way);
            C(d.Attribute("ParentName")?.Value == "BaseStoryteller", w + ": a whole storyteller on the game's own base (its endgame and DLC parts, its difficulty settings)");
            C(labels.TryGetValue(way, out var label) && d.Element("label")?.Value == label, w + ": named for what it does");
            int lo = int.Parse(d.Element("listOrder")?.Value ?? "0", Inv);
            C(lo > order && lo > 40, w + ": listed after the game's own, in order");
            order = lo;

            // The description says plainly how it plays, with no character.
            string desc = d.Element("description")?.Value ?? "";
            C(desc.Length >= 250 && (atBase ? desc.Contains("as in the base game") : desc.Contains("classic pace")) && desc.Contains("For a game "),
                w + (atBase ? ": the description says what comes more often and that the rest comes as in the base game" : ": the description says how it plays and its threats against the classic pace"));
            C(desc.EndsWith("\\n\\nIt shifts the odds this way but shuts nothing out: every kind of event can still come.", StringComparison.Ordinal), w + ": the description ends with the same last line as every way");
            C(!desc.Contains('\n'), w + ": its breaks written as the game reads them in a def");
            C(desc.Contains("shuts nothing out") && desc.Contains("every kind of event can still come"), w + ": the description says it leans, never shuts anything out");
            C(!Regex.IsMatch(desc, @"\b(she|he|her|his|him|hers)\b", RegexOptions.IgnoreCase), w + ": no character in the description");
            C(!desc.Contains('—') && !desc.Contains('!'), w + ": no em dash, no exclamation mark");
            C(!Regex.IsMatch(desc, @"\bgods?\b", RegexOptions.IgnoreCase), w + ": names no god");
            C(!Regex.IsMatch(desc, @"\b(in|after) \d+ days\b", RegexOptions.IgnoreCase), w + ": no countdown in its words");

            // The plates.
            string tail = defName.Substring(StoryWaysMath.DefPrefix.Length);
            C(d.Element("portraitLarge")?.Value == "ColonyCouncil/Storytellers/" + tail && d.Element("portraitTiny")?.Value == "ColonyCouncil/Storytellers/" + tail + "Tiny", w + ": its plates' paths");
            var large = Png(Path.Combine(root, "Textures", "ColonyCouncil", "Storytellers", tail + ".png"));
            var tiny = Png(Path.Combine(root, "Textures", "ColonyCouncil", "Storytellers", tail + "Tiny.png"));
            C(large == (580, 620), w + $": the large plate is there at the size the game draws it ({large.w}x{large.h})");
            C(tiny == (228, 244), w + $": the small plate is there at twice the size of the list's ({tiny.w}x{tiny.h})");

            // The comps.
            var comps = d.Element("comps")?.Elements("li").ToList() ?? new List<XElement>();
            string Cls(XElement c) => c.Attribute("Class")?.Value ?? "";
            string Cat(XElement c) => c.Element("category")?.Value ?? (c.Element("incident") is XElement inc && incidentCategory.TryGetValue(inc.Value, out var k) ? k : null);
            foreach (var c in comps)
            {
                string cls = Cls(c);
                if (cls.StartsWith("ColonyCouncil.", StringComparison.Ordinal))
                    C(storyComps.Contains("class " + cls.Substring("ColonyCouncil.".Length) + " "), w + ": " + cls + " is one of the ways' own parts");
                else C(cls.StartsWith("StorytellerCompProperties_", StringComparison.Ordinal), w + ": " + cls + " is one of the game's own");
                // Nothing shut out: no comp disallows an incident by def, tag or category; only the raid beacon's map keeps its own raids, as the game's own do.
                foreach (var e in c.Descendants().Where(e => e.Name.LocalName.StartsWith("disallowed", StringComparison.Ordinal)))
                    C(e.Name.LocalName == "disallowedTargetTags" && e.Elements("li").All(li => li.Value == "Map_RaidBeacon"), w + ": nothing disallowed but the raid beacon's own map");
                C(!c.Descendants().Any(e => e.Name.LocalName == "disableIfAnyModActive") || cls == "StorytellerCompProperties_RandomQuest", w + ": no part switched off by another mod but the game's quest pair");
                // The game's own spacing rule for a cycle.
                if (cls.EndsWith("_OnOffCycle") || cls.EndsWith("_WayCycle"))
                {
                    float on = F(c, "onDays"), spacing = F(c, "minSpacingDays");
                    var range = (c.Element("numIncidentsRange")?.Value ?? "0").Split('~').Select(x => float.Parse(x, Inv)).ToArray();
                    C(spacing * range.Max() <= on * 0.9f + 1e-4f, w + ": a cycle's spacing fits its days on");
                }
            }
            bool Has(string category) => comps.Any(c => Cat(c) == category || (category == "GiveQuest" && Cls(c).EndsWith("_RandomQuest")) || (category == "ShipChunkDrop" && Cls(c).EndsWith("_ShipChunkDrop")));
            foreach (var cat in categories)
            {
                if (byCode.TryGetValue(cat, out var why))
                {
                    C(cat != "Special" || comps.Any(c => c.Element("incident")?.Value == "StrangerInBlackJoin"), w + ": " + cat + ": " + why);
                    continue;
                }
                C(Has(cat), w + ": every kind of event keeps a route: " + cat);
            }
            C(comps.Any(c => Cls(c).EndsWith("_CategoryMTB") && Cat(c) == "Misc" && c.Element("allowedTargetTags")?.Elements("li").Any(li => li.Value == "Map_PlayerHome") == true),
                w + ": the home's own misc happenings, the DLCs' and mods' among them");
            C(comps.Count(c => Cls(c).EndsWith("_CategoryIndividualMTBByBiome")) == 3, w + ": caravans meet the land's happenings and threats as with any storyteller");
            C(comps.Any(c => Cls(c).EndsWith("_ThreatsGenerator")), w + ": the raid beacon's raids");
            C(comps.Any(c => Cat(c) == "LTS" && c.Element("enableIfAnyModActive") != null && c.Element("category")?.Attribute("MayRequire") != null), w + ": the Settlers' own category, soft");

            // Its threats at the classic pace or less over a year, an arc or the worst deeds.
            float big = 0f;
            foreach (var c in comps.Where(c => Cat(c) == "ThreatBig" && (Cls(c).EndsWith("_OnOffCycle") || Cls(c).EndsWith("_WayCycle"))))
            {
                var range = c.Element("numIncidentsRange")!.Value.Split('~').Select(x => float.Parse(x, Inv)).ToArray();
                float per = range.Average() / (F(c, "onDays") + F(c, "offDays"));
                string shape = c.Element("shape")?.Value ?? "None";
                float accept = shape == "Seasons" ? Enumerable.Range(0, 4).Average(q => Math.Max(StoryWaysMath.Floor, StoryWaysMath.SeasonThreatAccept(q)))
                    : shape == "Arc" ? StoryWaysMath.ArcMeanOver((p, t) => Math.Max(StoryWaysMath.Floor, StoryWaysMath.ArcThreatAccept(p, t)))
                    : shape == "Deeds" ? Math.Max(StoryWaysMath.DeedThreatAccept(-1f), StoryWaysMath.DeedThreatAccept(1f)) : 1f;
                big += per * accept;
            }
            // A way's picks: at each season, phase and trouble, the threats' share of those that fit then, by the time it lasts.
            foreach (var c in comps.Where(c => Cls(c).EndsWith("_WayPicks")))
            {
                var picks = c.Element("picks")!.Elements("li").ToList();
                float rate = 0f;
                for (int q = 0; q < 4; q++)
                foreach (ArcPhase ph in Enum.GetValues(typeof(ArcPhase)))
                foreach (StoryTrouble tr in Enum.GetValues(typeof(StoryTrouble)))
                {
                    var fit = picks.Where(p => Fits(p, q, ph, tr)).ToList();
                    float all = fit.Sum(p => F(p, "weight"));
                    float threats = fit.Where(p => incidentCategory.TryGetValue(p.Element("incident")!.Value, out var k) && k == "ThreatBig").Sum(p => F(p, "weight"));
                    if (all > 0f) rate += threats / all * (StoryWaysMath.PhaseDays(ph) / StoryWaysMath.ArcDays) / 20f;
                }
                big += rate / F(c, "mtbDays");
            }
            C(big > 0f && big <= classicBig * 1.02f, w + $": big threats {big * 60f:0.0} a year at most, the classic pace {classicBig * 60f:0.0}");

            // The way's own named happenings: real, active, and the DLC's guarded by its package.
            foreach (var p in comps.Where(c => Cls(c).EndsWith("_WayPicks")).SelectMany(c => c.Element("picks")!.Elements("li")))
            {
                var inc = p.Element("incident")!;
                string name = inc.Value, may = inc.Attribute("MayRequire")?.Value?.ToLowerInvariant();
                var rows = pool.Where(r => r.type == "IncidentDef" && r.name == name && r.active).ToList();
                C(rows.Count > 0, w + ": " + name + " is in the game or the player's active mods");
                C(rows.Any(r => may == null ? r.mod == "ludeon.rimworld" : r.mod == may), w + ": " + name + (may == null ? " is the base game's" : " is guarded by its own DLC"));
                C(!manual.Contains(name), w + ": " + name + " is one the game fires by chance");
                C(F(p, "weight") > 0f, w + ": " + name + " has a weight");
            }

            // The way's own named quests: the game's or an active mod's quest scripts, offered by chance, soft by their package.
            foreach (var c in comps.Where(c => Cls(c).EndsWith("_WayQuests")))
            {
                C(F(c, "mtbDays") > 0f && c.Element("allowedTargetTags")?.Elements("li").Select(x => x.Value).SequenceEqual(new[] { "World" }) == true,
                    w + ": its quests come to the world, as the game's own random quests do");
                foreach (var q in c.Element("quests")!.Elements("li"))
                {
                    var qe = q.Element("quest")!;
                    string name = qe.Value, may = qe.Attribute("MayRequire")?.Value?.ToLowerInvariant();
                    var rows = pool.Where(r => r.type == "QuestScriptDef" && r.name == name && r.active).ToList();
                    C(rows.Count > 0, w + ": the quest " + name + " is in the game or the player's active mods");
                    C(rows.Any(r => may == null ? r.mod == "ludeon.rimworld" : r.mod == may), w + ": the quest " + name + (may == null ? " is the base game's" : " is guarded by its own package"));
                    C(F(q, "weight") > 0f, w + ": the quest " + name + " has a weight");
                }
            }

            // The six after the first eight: the game's own rate or more, nothing longer than a season, the threats on the classic cycle.
            if (atBase)
            {
                C(!comps.Any(c => Cls(c).EndsWith("_WayCycle") || Cls(c).EndsWith("_WayDisease") || Cls(c).EndsWith("_WayVisits") || Cls(c).EndsWith("_WayOmen")),
                    w + ": no part shaped by the season, the deeds or the arc");
                var big2 = comps.Where(c => Cls(c).EndsWith("_OnOffCycle") && c.Element("category")?.Value == "ThreatBig").ToList();
                var small2 = comps.Where(c => Cls(c).EndsWith("_OnOffCycle") && c.Element("category")?.Value == "ThreatSmall").ToList();
                C(big2.Count == 1 && F(big2[0], "onDays") == 4.6f && F(big2[0], "offDays") == 6f && big2[0].Element("numIncidentsRange")?.Value == "1~2",
                    w + ": big threats on the classic cycle, no more and no fewer");
                C(small2.Count == 1 && F(small2[0], "onDays") == 4.6f && F(small2[0], "offDays") == 6f && small2[0].Element("numIncidentsRange")?.Value == "0.2~1",
                    w + ": small threats on the classic cycle");
                C(comps.Count(c => Cls(c).EndsWith("_Disease") && !Cls(c).Contains("Way")) == 2, w + ": the game's own sickness");
                float PerYear(string incident) => comps.Where(c => Cls(c).EndsWith("_FactionInteraction") && c.Element("incident")?.Value == incident).Sum(c => F(c, "baseIncidentsPerYear"));
                C(PerYear("TraderCaravanArrival") >= 5f && PerYear("VisitorGroup") >= 4f && PerYear("TravelerGroup") >= 6f && PerYear("RaidFriendly") >= 15f,
                    w + ": traders, visitors, travellers and friends at least as often as the classic storyteller's");
                C(comps.Any(c => Cls(c).EndsWith("_CategoryMTB") && c.Element("category")?.Value == "Misc" && F(c, "mtbDays") <= 4.8f
                                 && c.Element("allowedTargetTags")?.Elements("li").Any(li => li.Value == "Map_PlayerHome") == true),
                    w + ": the home's misc happenings at least as often as the classic storyteller's");
                foreach (var p in comps.Where(c => Cls(c).EndsWith("_WayPicks")).SelectMany(c => c.Element("picks")!.Elements("li")))
                {
                    string name = p.Element("incident")!.Value;
                    C(p.Element("seasons") == null && p.Element("phases") == null && p.Element("troubles") == null, w + ": " + name + " comes at any time of year");
                    C(!threatPicks.Contains(name) && !(incidentCategory.TryGetValue(name, out var cat) && cat.StartsWith("Threat", StringComparison.Ordinal)),
                        w + ": " + name + " is no threat (predators, maddened animals and raids come as in the base game)");
                }
            }

            // The levers.
            var ext = d.Element("modExtensions")?.Elements("li").FirstOrDefault(e => e.Attribute("Class")?.Value == "ColonyCouncil.StoryWayExtension");
            C(ext != null && ext.Element("way")?.Value == tail, w + ": its levers name its way");
            if (ext == null) continue;
            string extShape = ext.Element("shape")?.Value ?? "None";
            C(Enum.TryParse(extShape, out StoryShape shapeOf), w + ": a known shape");
            var leverEls = ext.Element("levers")?.Elements().ToList() ?? new List<XElement>();
            var values = new StoryLevers();
            foreach (var le in leverEls)
            {
                string key = le.Name.LocalName;
                C(StoryWaysMath.IsKey(key), w + ": the lever " + key + " is one of Rimfolk's");
                float v = float.Parse(le.Value, Inv);
                typeof(StoryLevers).GetField(key)?.SetValue(values, v);
                if (key == StoryWaysMath.RaidReasons) C(v >= 0f && v <= StoryWaysMath.MaxRaidReasons, w + ": the raids' reasons a share, never all");
                else C(v >= StoryWaysMath.Floor && v <= (StoryWaysMath.IsHarm(key) ? 1f : StoryWaysMath.MaxLever), w + $": {key} {v} within the floor and its ceiling");
            }
            // Every direction choice (the same levers) at every season, deed and phase: a quarter at least of everything.
            bool floored = true, harmOk = true;
            foreach (var key in StoryWaysMath.Keys)
            for (int q = 0; q < 4; q++)
            for (float deeds = -1f; deeds <= 1.001f; deeds += 0.25f)
            foreach (ArcPhase ph in Enum.GetValues(typeof(ArcPhase)))
            {
                float now = StoryWaysMath.Combine(key, values.Get(key), shapeOf, q, deeds, ph);
                if (key == StoryWaysMath.RaidReasons ? now > StoryWaysMath.MaxRaidReasons : now < StoryWaysMath.Floor) floored = false;
                if (StoryWaysMath.IsHarm(key) && shapeOf != StoryShape.Seasons && shapeOf != StoryShape.Arc && now > 1.0001f) harmOk = false;
            }
            C(floored, w + ": as a storyteller and as the direction, every lever keeps a quarter at least at every season, deed and phase");
            if (atBase)
            {
                C(shapeOf == StoryShape.None, w + ": no shape, so nothing runs longer than a season");
                C(StoryWaysMath.Keys.Where(k => k != StoryWaysMath.RaidReasons).All(k => values.Get(k) >= 1f), w + ": every lever at the game's own rate or more");
                C(StoryWaysMath.HarmKeys.All(k => values.Get(k) == 1f) && values.Get(StoryWaysMath.RaidReasons) == 0f, w + ": harm exactly at the game's rate, and no raid aimed");
                C(StoryWaysMath.Keys.Any(k => !StoryWaysMath.IsHarm(k) && k != StoryWaysMath.RaidReasons && values.Get(k) > 1f), w + ": it leans toward its own kind of thing");
            }
            C(harmOk, w + ": harm never above 1 but where a shape moves it about within its year or arc");
            foreach (var k in ext.Element("situationKinds")?.Elements("li").Select(x => x.Value) ?? Enumerable.Empty<string>())
                C(situationKinds.Contains(k), w + ": world situations of the kind " + k + " exist");
            foreach (var k in ext.Element("harmLean")?.Elements("li").Select(x => x.Value) ?? Enumerable.Empty<string>())
                C(HarmKinds.Of(k) != null, w + ": the hurt " + k + " is a kind of the shared budget");

            // The player, 19 November 2026 ("Some storytellers and fewer traders and fewer fights. That's also good. Depending,
            // you know, if it's fitting for the scenario, it should also just be reflected in the description"): a way brings
            // something less often than the base game only where that is its idea (KeptBelowBase), and its description says so.
            // A shaped way is taken over its year or its arc, and What You Sow at both ends of the deeds; Rimfolk's levers, then
            // the game's own traders, visitors, travellers, friends in a fight, orbital traders and everyday happenings.
            var kept = KeptBelowBase.TryGetValue(way, out var keptOf) ? keptOf : Array.Empty<(string key, string said)>();
            var below = new HashSet<string>();
            var phases = Enum.GetValues(typeof(ArcPhase)).Cast<ArcPhase>().ToList();
            foreach (var key in StoryWaysMath.Keys.Where(k => k != StoryWaysMath.RaidReasons))
            {
                float v = values.Get(key);
                float over = shapeOf == StoryShape.Seasons ? Enumerable.Range(0, 4).Average(q => StoryWaysMath.Combine(key, v, shapeOf, q, 0f, ArcPhase.Growth))
                    : shapeOf == StoryShape.Arc ? phases.Sum(p => StoryWaysMath.Combine(key, v, shapeOf, 0, 0f, p) * StoryWaysMath.PhaseDays(p)) / StoryWaysMath.ArcDays
                    : shapeOf == StoryShape.Deeds ? new[] { -1f, 0f, 1f }.Min(b => StoryWaysMath.Combine(key, v, shapeOf, 0, b, ArcPhase.Growth))
                    : StoryWaysMath.Clamp(key, v);
                if (over < AboutBase) below.Add(key);
                if (shapeOf == StoryShape.Deeds)
                    C(StoryWaysMath.Combine(key, v, shapeOf, 0, 0f, ArcPhase.Growth) >= 1f, w + ": " + key + " at the base rate or more for a colony nobody knows anything about");
            }
            float Yearly(string incident)
            {
                float sum = 0f;
                foreach (var c in comps.Where(c => c.Element("incident")?.Value == incident))
                {
                    float n0 = F(c, "baseIncidentsPerYear");
                    if (Cls(c).EndsWith("_FactionInteraction")) sum += n0;
                    else if (Cls(c).EndsWith("_WayVisits"))
                    {
                        string lever = c.Element("lever")?.Value ?? StoryWaysMath.Visitors, sh = c.Element("shape")?.Value ?? "None";
                        float share = sh == "Seasons" ? Enumerable.Range(0, 4).Average(q => Math.Max(StoryWaysMath.Floor, StoryWaysMath.SeasonVisitAccept(lever, q)))
                            : sh == "Arc" ? phases.Sum(p => Math.Max(StoryWaysMath.Floor, StoryWaysMath.ArcVisitAccept(lever, p)) * StoryWaysMath.PhaseDays(p)) / StoryWaysMath.ArcDays
                            : sh == "Deeds" ? StoryWaysMath.DeedVisitAccept(lever, 0f) : 1f;
                        sum += n0 * share;
                    }
                }
                return sum;
            }
            // The classic storyteller's (Tools/Balance/StorytellerSim.cs, from the game's def): traders 5 a year, visitors 4,
            // travellers 6, friends in a fight 15, an orbital trader every 15 days, everyday happenings about every 4.8 days.
            if (Yearly("TraderCaravanArrival") < 5f * AboutBase) below.Add(StoryWaysMath.Traders);
            if (Yearly("VisitorGroup") < 4f * AboutBase || Yearly("TravelerGroup") < 6f * AboutBase) below.Add(StoryWaysMath.Visitors);
            if (Yearly("RaidFriendly") < 15f * AboutBase) below.Add(StoryWaysMath.Allies);
            if (comps.Any(c => Cls(c).EndsWith("_WayVisits") && c.Element("shape")?.Value == "Deeds"))
                C(Yearly("TraderCaravanArrival") >= 5f && Yearly("VisitorGroup") >= 4f && Yearly("RaidFriendly") >= 15f,
                    w + $": a colony nobody knows anything about gets traders, visitors and friends as under the classic storyteller ({Yearly("TraderCaravanArrival"):0.0}, {Yearly("VisitorGroup"):0.0}, {Yearly("RaidFriendly"):0.0} a year)");
            foreach (var c in comps.Where(c => Cls(c).EndsWith("_OnOffCycle") && c.Element("incident")?.Value == "OrbitalTraderArrival"))
                if (F(c, "numIncidentsRange") / (F(c, "onDays") + F(c, "offDays")) < AboutBase / 15f) below.Add("orbital");
            float everyday = comps.Where(c => Cls(c).EndsWith("_CategoryMTB") && c.Element("category")?.Value == "Misc"
                                              && c.Element("allowedTargetTags")?.Elements("li").Any(li => li.Value == "Map_PlayerHome") == true).Select(c => F(c, "mtbDays")).DefaultIfEmpty(99f).Min();
            if (everyday > 4.8f / AboutBase) below.Add("everyday");
            foreach (var c in comps.Where(c => Cls(c).EndsWith("_WayVisits") && c.Element("lever")?.Value == StoryWaysMath.Allies && c.Element("shape")?.Value == "Arc"))
                C(phases.All(p => F(c, "baseIncidentsPerYear") * StoryWaysMath.ArcVisitAccept(StoryWaysMath.Allies, p) >= 15f),
                    w + ": friends in a fight at least as often as the classic storyteller's in every part of the arc");
            foreach (var key in below)
                C(kept.Any(x => x.key == key), w + ": " + key + " below the base rate only where the way is about it");
            foreach (var (key, said) in kept)
            {
                C(below.Contains(key), w + ": " + key + " is listed as brought less often, and is");
                C(desc.Contains(said), w + ": what it brings less often (" + key + ") is said in its description: \"" + said + "\"");
            }
            if (!atBase) C(kept.Length > 0 || desc.Contains("Nothing comes less often than in the base game."), w + ": a way with nothing below the base rate says so");
            if (QuietTimes.TryGetValue(way, out var quiet)) C(desc.Contains(quiet), w + ": its quiet season or time is said: \"" + quiet + "\"");
        }
        foreach (var kinds in Enumerable.Range(0, 4).Select(StoryWaysMath.SeasonSituations)
                     .Concat(Enum.GetValues(typeof(StoryTrouble)).Cast<StoryTrouble>().Select(StoryWaysMath.TroubleKinds))
                     .Concat(new[] { StoryWaysMath.ArcSituations(ArcPhase.Growth, StoryTrouble.War), StoryWaysMath.ArcSituations(ArcPhase.Rebuild, StoryTrouble.War) }))
        foreach (var k in kinds)
            C(situationKinds.Contains(k), "the shapes' world situations exist: " + k);
        // Every trouble has a situation that can foretell it (a bad one of its kinds).
        var badKinds = Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .Where(e => e.Element("bad")?.Value == "true").Select(e => e.Element("kind")?.Value).Where(k => k != null).ToHashSet();
        foreach (StoryTrouble t in Enum.GetValues(typeof(StoryTrouble)))
            C(StoryWaysMath.TroubleKinds(t).Any(k => t == StoryTrouble.Void ? situationKinds.Contains(k) : badKinds.Contains(k)), t + ": a world situation foretells it (a hard one, or a strange sky)");

        // ================= Rimfolk's events by their kinds =================

        var happenings = Directory.GetFiles(Path.Combine(root, "Defs", "IncidentDefs"), "*.xml").Concat(Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements().Where(e => e.Name.LocalName == "IncidentDef")).ToList();
        var leaned = new Dictionary<string, List<string>>();
        foreach (var h in happenings)
        {
            var he = h.Element("modExtensions")?.Elements("li").FirstOrDefault(e => e.Attribute("Class")?.Value == "ColonyCouncil.HappeningExtension");
            var leans = he?.Element("leans")?.Elements("li").Select(x => x.Value).ToList();
            if (leans == null) continue;
            string hn = h.Element("defName")?.Value ?? "?";
            foreach (var k in leans)
            {
                C(StoryWaysMath.IsLeanKey(k), hn + ": leans " + k + ", one of the ways' kinds");
                if (!leaned.ContainsKey(k)) leaned[k] = new List<string>();
                leaned[k].Add(hn);
            }
            C(he!.Element("battle")?.Value != "true" && he.Element("group")?.Value != "Battle", hn + ": an event that may end in a fight is never leaned by its kind (harm keeps the fights lever)");
            C(leans.Distinct().Count() == leans.Count, hn + ": each kind once");
        }
        foreach (var k in StoryWaysMath.LeanKeys)
            C(leaned.TryGetValue(k, out var hs) && hs.Count >= 5, $"the kind {k}: Rimfolk's events of it to lean ({(leaned.TryGetValue(k, out var l2) ? l2.Count : 0)})");
        C(happenings.Count(h => h.Descendants("addsPeople").Any(x => x.Value == "true")) >= 10, "the joiners: Rimfolk's events in which someone may join");

        // ================= The direction setting =================

        var row = SettingsCatalog.Find("storyDirection");
        C(row != null && row.page == SettingsPage.WhatsOn && row.notePrefix != null && row.options != null
          && row.options.SequenceEqual(Enum.GetNames(typeof(StoryWay))), "the story direction: none or one of the fourteen, with a note per choice");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Story.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var name in Enum.GetNames(typeof(StoryWay)))
        {
            C(keyed.ContainsKey("CC_StoryWay_" + name) && keyed.ContainsKey("CC_SetNote_StoryDirection_" + name), name + ": its label and note");
            if (keyed.TryGetValue("CC_SetNote_StoryDirection_" + name, out var note)) C(!Regex.IsMatch(note, @"\bgods?\b", RegexOptions.IgnoreCase), name + ": its note names no god");
        }
        foreach (var w in StoryWaysMath.Ways) C(keyed.TryGetValue("CC_StoryWay_" + w, out var l) && l == labels[w], w + ": the direction's name is the storyteller's");

        // ================= The seams =================

        var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.Combine("Story", "StoryWaysMath.cs")) && !f.Contains(Path.Combine("Story", "StoryLevers.cs")))
            .ToDictionary(f => f, File.ReadAllText);
        string all2 = string.Join("\n", files.Values);
        foreach (var f in typeof(StoryWaysMath).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string) && StoryWaysMath.IsKey((string)f.GetValue(null)!)))
            C(all2.Contains("StoryWaysMath." + f.Name + ")"), "the lever " + f.GetValue(null) + " is read at one of Rimfolk's schedulers");
        string Src(string rel) => File.ReadAllText(Path.Combine(source, rel));
        C(Src("Harm/Harm.cs").Contains("StoryDirection.HarmTilt") && Src("Harm/Harm.cs").Contains("* tiltNow[i], budget, shared"), "the hurts' tilt is in the shared weight and each pass");
        C(Src("Gods/World/GodHealth.cs").Contains("HarmKinds.GodBeasts, GodWorldMath.BeastsDayChance * GodWorld.Factor()") && Src("Gods/World/GodHealth.cs").Contains("GodWorldMath.LightningHourChance * GodWorld.Factor()"),
            "a god's beasts and lightning keep the game's own rate");
        string reasons = Src("Story/StoryReasons.cs");
        C(reasons.Contains("[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), \"TryResolveRaidFaction\")]") && reasons.Contains("RimfolkPatches.On") && reasons.Contains("parms.faction != null"),
            "the raids' reasons: a patch on the game's choice, with the deeper hooks, only when the raid's people is not set");
        C(Src("Happenings/Happenings.cs").Split("StoryDirection.HappeningFactor(ext)").Length == 3, "Rimfolk's events, in the storyteller's pools and on Rimfolk's own timer");
        C(Src("World/FactionMemory.cs").Contains("StoryDirection.Factor(StoryWaysMath.Memory)"), "the peoples' memory");
        string direction = Src("Story/StoryDirection.cs");
        C(direction.Contains("if (ext.addsPeople) kinds.Add(Factor(StoryWaysMath.Joiners))") && direction.Contains("StoryWaysMath.HappeningLean(group, kinds)")
          && StoryWaysMath.LeanKeys.All(k => direction.Contains("ext.leans.Contains(StoryWaysMath." + char.ToUpperInvariant(k[0]) + k.Substring(1) + ")")),
            "an event's kinds: who may join, and each kind its def leans");
        C(Src("Strangers/Encounters.cs").Contains("StoryDirection.Factor(StoryWaysMath.Beasts)") && Src("Strangers/Encounters.cs").Contains("k == StrangerKind.Pet || k == StrangerKind.Wild ? beasts : 1f"),
            "the colonists' moments with animals, by the beasts lever");
        C(Src("Diplomacy/Feasts.cs").Contains("Rand.Value / Math.Max(0.01f, StoryDirection.Factor(StoryWaysMath.Feasts))"), "the steward's feast, by the feasts lever");
        C(Src("Diplomacy/CallsToArms.cs").Contains("StoryDirection.Factor(StoryWaysMath.Favours) * StoryDirection.Factor(StoryWaysMath.Arms)"), "a people's call to arms, by the favours and arms levers");
        string comps2 = Src("Story/StoryComps.cs");
        C(comps2.Contains("IncidentChanceFactor_CurrentPopulation(def) * IncidentChanceFactor_PopulationIntent(def)") && comps2.Contains("StoryWaysMath.PickAccept(weights"),
            "a way's picks that bring people follow the game's sense of how many the colony should have");
        C(comps2.Contains("NaturalRandomQuestChooser.GetNaturalRandomSelectionWeight(q, parms.points, target.StoryState)") && comps2.Contains("parms.questScriptDef = pick.q")
          && comps2.Contains("IncidentDefOf.GiveQuest_Random"), "a way's quests: the game's own weight for each, offered through the game's own random quest");
        C(!Src("Lean/Sched.cs").Contains("StoryDirection") && !Src("Lean/Sched.cs").Contains("StoryWay"), "no new pass: the ways read the world when asked, and the storyteller's parts run on the game's own clock");
        return n;
    }

    /// <summary>Within this share of the base rate counts as at it ("about as often").</summary>
    private const float AboutBase = 0.98f;

    /// <summary>
    /// What a way brings less often than the base game, kept because it is the way's idea, and the words its description
    /// says it with (the player, 19 November 2026; DESIGN.md "The storytellers' levers below the base rate"). Keys are
    /// Rimfolk's levers (for the game's own parts: traders, visitors with travellers, allies for friends in a fight), and
    /// "orbital" and "everyday" for the game's orbital traders and the home's everyday happenings. Neighbours' fights and
    /// What You Sow's traders and friends for a colony of no name went back to the base rate; The Long Road, Neighbours and
    /// Crossroads bring nothing less often.
    /// </summary>
    private static readonly Dictionary<StoryWay, (string key, string said)[]> KeptBelowBase = new()
    {
        // The Turning Year: each season's own happenings take the place of some everyday ones.
        [StoryWay.TurningYear] = new[] { ("everyday", "in place of some of the everyday ones") },
        // What You Sow: the colony's deeds come back to it, fewer of everyone for cruelty, fewer fights for kindness.
        [StoryWay.WhatYouSow] = new[]
        {
            (StoryWaysMath.Traders, "fewer traders, visitors and camps"), (StoryWaysMath.Visitors, "fewer traders, visitors and camps"),
            (StoryWaysMath.Camps, "fewer traders, visitors and camps"), (StoryWaysMath.Letters, "fewer letters, favours and missions"),
            (StoryWaysMath.Favours, "fewer letters, favours and missions"), (StoryWaysMath.Missions, "fewer letters, favours and missions"),
            (StoryWaysMath.Allies, "fewer friends in a fight"),
            (StoryWaysMath.Fights, "Rimfolk's events that can end in a fight and schemes against the colony come less often"),
            (StoryWaysMath.Schemes, "Rimfolk's events that can end in a fight and schemes against the colony come less often")
        },
        // Old Powers: the danger is strange, not other people; omens in place of some everyday happenings.
        [StoryWay.OldPowers] = new[] { (StoryWaysMath.Fights, "Rimfolk's events that can end in a fight"), ("everyday", "in place of some of the everyday happenings") },
        // Lean Years: fewer people come by, fewer good things turn up, and the danger is the land more than other people.
        [StoryWay.LeanYears] = new[]
        {
            (StoryWaysMath.Traders, "traders about half as often"), (StoryWaysMath.Visitors, "visitors, travellers and camps a little less often"),
            (StoryWaysMath.Camps, "visitors, travellers and camps a little less often"), ("orbital", "orbital traders less often"),
            (StoryWaysMath.Fights, "Rimfolk's events that can end in a fight"), ("everyday", "everyday happenings also come less often")
        },
        // Rise and Fall: the good years are lighter, the trouble heavier and the region's own news.
        [StoryWay.RiseAndFall] = new[]
        {
            (StoryWaysMath.Fights, "Rimfolk's events that can end in a fight and schemes against the colony less often"),
            (StoryWaysMath.Schemes, "Rimfolk's events that can end in a fight and schemes against the colony less often"),
            (StoryWaysMath.Situations, "less other news from the region")
        }
    };

    /// <summary>The shaped ways' quiet times, which their descriptions say (about the base rate over the year or the arc).</summary>
    private static readonly Dictionary<StoryWay, string> QuietTimes = new()
    {
        [StoryWay.TurningYear] = "few traders or camps",
        [StoryWay.RiseAndFall] = "few traders, visitors or camps"
    };

    private static float F(XElement e, string name) => float.Parse(e.Element(name)?.Value ?? "0", Inv);

    /// <summary>A way's pick fits this season, phase and trouble (as WayPick.Fits).</summary>
    private static bool Fits(XElement p, int quarter, ArcPhase phase, StoryTrouble trouble)
    {
        var seasons = p.Element("seasons")?.Elements("li").Select(x => x.Value).ToList();
        var phases = p.Element("phases")?.Elements("li").Select(x => x.Value).ToList();
        var troubles = p.Element("troubles")?.Elements("li").Select(x => x.Value).ToList();
        string[] quarterNames = { "Spring", "Summer", "Fall", "Winter" };
        if (seasons != null && seasons.Count > 0 && !seasons.Contains(quarterNames[quarter])) return false;
        if (phases != null && phases.Count > 0 && !phases.Contains(phase.ToString())) return false;
        if (troubles != null && troubles.Count > 0 && !troubles.Contains(trouble.ToString())) return false;
        return true;
    }

    /// <summary>A PNG's size from its header, or (0, 0).</summary>
    private static (int w, int h) Png(string path)
    {
        if (!File.Exists(path)) return (0, 0);
        var b = File.ReadAllBytes(path);
        if (b.Length < 24) return (0, 0);
        int R(int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        return (R(16), R(20));
    }

    private static List<(string type, string name, string mod, bool active)> Pool(string root)
    {
        var list = new List<(string, string, string, bool)>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var sr = new StreamReader(gz);
        sr.ReadLine();
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var p = line.Split('\t');
            if (p.Length >= 4) list.Add((p[0], p[1], p[2], p[3] == "yes"));
        }
        return list;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root)!;
        return root!;
    }
}
