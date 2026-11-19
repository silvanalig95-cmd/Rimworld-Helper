using System.Xml.Linq;

/// <summary>A day off lifts the mood while it lasts (the player, 19 November 2026). The mood's def and its worker.</summary>
public static class HeartDayOffMoodTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a day off's mood: " + what);
        }

        var def = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_DayOff.xml")).Root!.Elements("ThoughtDef").Single();
        C((string?)def.Element("workerClass") == "ColonyCouncil.ThoughtWorker_HeartDayOff", "situational, read from the heart each time");
        var stage = def.Element("stages")!.Elements("li").Single();
        C((string?)stage.Element("label") == "a day off" && (int?)stage.Element("baseMoodEffect") == 6, "\"a day off\", +6, about a party's");
        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Heart", "HeartTimeOff.cs"));
        C(src.Contains("h.Off(TimeOffKind.DayOff) ? RimWorld.ThoughtState.ActiveAtStage(0)") && src.Contains("!HeartTimeOff.DayOffOn"),
            "only while the day off the player gave runs, and with its setting");
        C(src.Contains("!ColonyCouncilMod.Settings.MoodEffects"), "never with Rimfolk's moods off");
        return n;
    }
}
