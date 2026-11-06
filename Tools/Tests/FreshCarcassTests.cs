using System;
using System.IO;
using ColonyCouncil;

/// <summary>The alert for fresh animal carcasses lying out (the player's request, 6 November 2026).</summary>
public static class FreshCarcassTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "fresh carcasses: " + what);
        }

        int hour = FreshCarcassMath.LyingTicks;
        C(FreshCarcassMath.LyingOut(true, true, false, false, false, hour), "an animal's fresh carcass, out an hour, with nobody on it, shows");
        C(!FreshCarcassMath.LyingOut(false, true, false, false, false, hour), "a person's body doesn't");
        C(!FreshCarcassMath.LyingOut(true, false, false, false, false, hour), "one that has started to rot doesn't");
        C(!FreshCarcassMath.LyingOut(true, true, true, false, false, hour), "one under the fog doesn't");
        C(!FreshCarcassMath.LyingOut(true, true, false, true, false, hour), "one in a stockpile or on a shelf doesn't");
        C(!FreshCarcassMath.LyingOut(true, true, false, false, true, hour), "one someone is on their way to doesn't");
        C(!FreshCarcassMath.LyingOut(true, true, false, false, false, hour - 1), "a kill just made doesn't flash up");
        C(hour == 2500, "an hour of the game");

        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string code = src("Animals/FreshCarcasses.cs"), settings = src("CouncilSettings.cs"), catalog = src("UI/SettingsCatalog.cs");
        C(code.Contains("class Alert_FreshCarcass : Alert") && code.Contains("AlertReport.CulpritsAre(culprits)"), "it's one of the game's alerts, and a click goes to the carcasses");
        C(code.Contains("freshCarcassAlert") && settings.Contains("public bool freshCarcassAlert = true;")
          && settings.Contains("Scribe_Values.Look(ref freshCarcassAlert, \"freshCarcassAlert\", true);"), "its setting, on by default, is saved");
        C(catalog.Contains("Toggle(\"freshCarcassAlert\""), "its row is with the alerts");
        C(code.Contains("map.IsPlayerHome") && code.Contains("rot.Stage == RotStage.Fresh") && code.Contains("IsReservedByAnyoneOf(c, Faction.OfPlayer)"),
            "the colony's maps only, fresh only, and not while someone of the colony is on it");
        C(code.Contains("now / Sched.RareTicks != lookedAt"), "looked at once a rare tick, not every frame");
        return n;
    }
}
