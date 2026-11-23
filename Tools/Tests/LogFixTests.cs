using System.Text.RegularExpressions;

/// <summary>
/// What the player's log of 23 November 2026 showed (a new colony, "New Arrivals"): Rimfolk's peoples without a leader,
/// the bed rest asking for the colony while the world was made, the heart's tooltip on the world map before a game, and a
/// deed dated before the game's calendar began.
/// </summary>
public static class LogFixTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the log's fixes: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // Every people of Rimfolk's names the leader the game makes for it (the game looks only in the fight group), and
        // that kind is one of the people's own leaders.
        var defs = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml");
        int peoples = 0;
        foreach (var f in defs)
        {
            string t = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(t, @"<FactionDef\b[^>]*>(.*?)</FactionDef>", RegexOptions.Singleline))
            {
                string body = m.Groups[1].Value;
                if (!body.Contains("ColonyCouncil.PeopleExtension")) continue;
                peoples++;
                string def = Regex.Match(body, @"<defName>([^<]+)</defName>").Groups[1].Value;
                var fixedKinds = Regex.Match(body, @"<fixedLeaderKinds>(.*?)</fixedLeaderKinds>", RegexOptions.Singleline);
                var kinds = fixedKinds.Success ? Regex.Matches(fixedKinds.Groups[1].Value, @"<li>([^<]+)</li>").Select(x => x.Groups[1].Value).ToList() : new List<string>();
                bool leaderKind = kinds.Count > 0 && kinds.All(k => Regex.IsMatch(t, @"<defName>" + Regex.Escape(k) + @"</defName>\s*(?:<[^>]+>[^<]*</[^>]+>\s*){0,8}?<factionLeader>true</factionLeader>")
                                                                   || Regex.IsMatch(t, @"<defName>" + Regex.Escape(k) + @"</defName>(?:(?!</PawnKindDef>).)*<factionLeader>true</factionLeader>", RegexOptions.Singleline));
                C(leaderKind, def + " names its leader kind in fixedLeaderKinds, a kind marked factionLeader");
            }
        }
        C(peoples >= 23, "every people of Rimfolk's checked (" + peoples + ")");
        string peoplesCs = Read("Peoples/Peoples.cs"), arising = Read("Peoples/ArisingBook.cs");
        C(peoplesCs.Contains("public static int GiveLeaders()") && peoplesCs.Contains("f.leader != null || !IsOurs(f) || f.def.fixedLeaderKinds.NullOrEmpty()")
          && peoplesCs.Contains("Arising.Making") && arising.Contains("Peoples.GiveLeaders();"),
            "a world made before gets its peoples' leaders when it loads (not while a people is being made)");

        // Asked only in play: the colony, the selector and the calendar aren't there before the game starts.
        C(Read("Health/BedRest.cs").Contains("p.Faction != null && p.Faction.IsPlayer && DecreeTracker.InForceFor(p, sickBay)")
          && !Read("Health/BedRest.cs").Contains("Faction.OfPlayer ")
          && Read("Health/BedRestPatches.cs").Contains("!BedRest.On || Current.ProgramState != ProgramState.Playing"),
            "bed rest: no asking for the colony while the world is made (583 errors in the log)");
        C(Read("Patches/Patch_HeartInspectTip.cs").Contains("Current.ProgramState != ProgramState.Playing) return;"),
            "the heart's tooltip: none on the world map before a game");
        C(Read("Social/Gossip/Deeds.cs").Contains("holder == other || Current.ProgramState != ProgramState.Playing) return;")
          && Read("Social/FightNames.cs").Contains("GenDate.QuadrumDateStringAt(GenTicks.TicksAbs, longitude)"),
            "deeds: none recorded while the starting pawns are made, and the date read the game's safe way");
        return n;
    }
}
