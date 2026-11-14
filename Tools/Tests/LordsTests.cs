using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Groups left with nobody in them (a bug of Rimfolk's, found in the player's save of 13 November 2026: two errand visits
/// about 32 days old and two war bands, empty and ticking; Lean/LordsMath.cs, Lean/Lords.cs): a lord Rimfolk empties by
/// moving its people out is ended the game's way, Rimfolk's own lords found empty are ended on loading and once a day,
/// the game's and other mods' left as they are, and no place moves pawns out of a lord without the helper.
/// </summary>
public static class LordsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "empty groups: " + what);
        }

        // ---------------- The game's rule ----------------

        C(LordsMath.ShouldExist(1, 0, false, false), "a group with someone in it goes on");
        C(LordsMath.ShouldExist(3, 2, false, false), "a group with people and buildings goes on");
        C(!LordsMath.ShouldExist(0, 0, false, false), "nobody in it and a job that needs people: ended");
        C(LordsMath.ShouldExist(0, 0, true, false), "a gathering others can still join may stand empty");
        C(LordsMath.ShouldExist(0, 2, false, true), "a job kept for its buildings goes on while it has them");
        C(!LordsMath.ShouldExist(0, 2, false, false), "buildings alone don't keep a job that isn't kept for them (a camp's group, as the game ends it)");
        C(!LordsMath.ShouldExist(0, 0, false, true), "kept for buildings, with none left: ended");

        // ---------------- Found in a save ----------------

        const string Own = LordsMath.OwnNamespace;
        C(Own == "ColonyCouncil", "Rimfolk's own lord jobs are told by their namespace");
        // The player's save: LordJob_VisitOnErrand (loadIDs 79, 80) and LordJob_WarBand (32, 131), no pawns, no buildings.
        C(LordsMath.Swept(Own, 0, 0, false), "the save's empty errand visits and war bands are ended");
        C(!LordsMath.Swept(Own, 1, 0, false), "one of Rimfolk's with someone in it is left");
        C(!LordsMath.Swept(Own, 0, 3, false), "one with buildings of its own (a camp still standing) is left to its camp");
        C(!LordsMath.Swept(Own, 0, 0, true), "a gathering of Rimfolk's that may stand empty is left");
        foreach (string ns in new[] { "RimWorld", "Verse.AI.Group", "Hospitality", "VFECore", "", null })
            C(!LordsMath.Swept(ns, 0, 0, false), $"another's empty lord is left as it is ({ns ?? "none"})");

        Seams(root, C);
        return cases;
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string lords = Code("Lean/Lords.cs");

        C(lords.Contains("LordsMath.ShouldExist(lord.ownedPawns.Count, lord.ownedBuildings.Count, job.ShouldExistWithoutPawns, job.KeepExistingWhileHasAnyBuilding)")
          && lords.Contains("manager.RemoveLord(lord);") && lords.Contains("!manager.lords.Contains(lord)"),
            "a lord left empty is ended the game's way (LordManager.RemoveLord, by Lord.ShouldExist's rule), never twice");
        C(lords.Contains("LordsMath.Swept(job.GetType().Namespace, lord.ownedPawns.Count, lord.ownedBuildings.Count, job.ShouldExistWithoutPawns)")
          && lords.Contains("Log.Message(\"[Rimfolk] Ended "), "the sweep: Rimfolk's own empty lords by the pure rule, each named in the log");
        C(Code("CouncilGameComponent.cs").Contains("Safe(\"empty groups left in the save\", () => Lords.Sweep());")
          && Code("CouncilDynamics.cs").Contains("Safe(\"empty groups\", () => Lords.Sweep());"), "swept on loading and once a day");
        C(Code("Camps/WarCamp.cs").Contains("Lords.Leave(b.lord, here);") && Code("Peoples/Events/PeopleWorkOn.cs").Contains("Lords.Leave(x);")
          && Code("Happenings/HappeningHelpers.cs").Contains("Lords.Leave(list);"),
            "the war band settling into a camp, the worker sent to a colonist and anyone sent away end the group they leave empty");
        string camp = Code("Camps/CampLife.cs");
        C(Regex.Matches(camp, Regex.Escape("if (LordAlive) Lords.EndIfEmpty(lord);")).Count == 2, "a camp's group, empty, ends with the camp (both of Finish's ways)");
        C(!Code("World/Region/HamletEscort.cs").Contains("RemoveLord("), "the hamlet's camp no longer ends its group by hand");
        C(Code("Visitors/VisitorTracker.cs").Contains("Lords.EndIfEmpty(t.lord);"), "a visit sent off ends its group once they're out");

        // Every RemovePawn on a lord outside the helper is one of these, each with its reason beside it.
        var allowed = new Dictionary<string, int>
        {
            // Caravans, not lords.
            ["Diplomacy/Hostages.cs|c"] = 1, ["Diplomacy/WardsAbroad.cs|c"] = 1, ["Diplomacy/EnvoysAbroad.cs|c"] = 1,
            ["Peoples/Events2/SeasonAway.cs|c"] = 2, ["Peoples/PeoplesF2.cs|c"] = 1,
            // The Wardens' hunters out on the trail: the camp's group waits for them (Recall).
            ["Peoples/Events/PeopleCamps.cs|h.GetLord()?"] = 1,
            // A camp sent off (ended with the camp's books) and its beasts led off inside the group's own turn.
            ["Camps/CampLife.cs|lord"] = 1, ["Camps/CampLook.cs|lord"] = 1,
            // A gathering anyone may join stands empty by the game's rule and ends by its clock.
            ["PlayOut/PlayOutLord.cs|lord"] = 1,
            // A visit sent off: ended once they're all out.
            ["Visitors/VisitorTracker.cs|t.lord"] = 1,
        };
        var found = new Dictionary<string, int>();
        var call = new Regex(@"([\w.]+(?:\(\))?\??)\.RemovePawn\(");
        foreach (var path in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, path).Replace('\\', '/');
            if (rel == "Lean/Lords.cs" || rel.StartsWith("obj/") || rel.StartsWith("bin/")) continue;
            foreach (Match m in call.Matches(File.ReadAllText(path)))
            {
                string recv = m.Groups[1].Value;
                if (recv.EndsWith("WorldPawns")) continue;
                string key = rel + "|" + recv;
                found[key] = found.TryGetValue(key, out int n) ? n + 1 : 1;
            }
        }
        var stray = found.Where(kv => !allowed.TryGetValue(kv.Key, out int n) || kv.Value > n).Select(kv => kv.Key + " x" + kv.Value).ToList();
        C(stray.Count == 0, "no place takes someone out of a lord without the helper (Lean/Lords.cs)" + (stray.Count == 0 ? "" : ": " + string.Join(", ", stray)));
        C(found.Values.Sum() == allowed.Values.Sum(), $"each kept RemovePawn is still where its reason says ({found.Values.Sum()} of {allowed.Values.Sum()})");
        C(Regex.Matches(string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)), @"\bLords\.(Leave|EndIfEmpty)\(").Count >= 80,
            "the helper is used where the groups were emptied (80 places or more)");
    }
}
