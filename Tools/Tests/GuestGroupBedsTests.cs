using ColonyCouncil;

/// <summary>
/// Hospitality's guest groups fit the guest beds (the player's report, 7 November 2026: "why do 4-5 people visitor
/// groups stay in my colony if i only have one reserved visitor bed?"): the size rule
/// (Integrations/GuestGroupBedsMath.cs) and that the patch and its setting are wired.
/// </summary>
public static class GuestGroupBedsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "guest groups and guest beds: " + what);
        }

        C(GuestGroupBedsMath.Size(5, 1, 1) == 1, "five guests and one free guest bed: one comes");
        C(GuestGroupBedsMath.Size(4, 6, 6) == 4, "fewer guests than free beds: all of them come");
        C(GuestGroupBedsMath.Size(3, 3, 3) == 3, "as many guests as free beds: all of them come");
        C(GuestGroupBedsMath.Size(5, 4, 2) == 2, "beds partly taken by guests already here: only the free ones count");
        C(GuestGroupBedsMath.Size(5, 2, 0) == 0, "every guest bed taken: no new group for now");
        C(GuestGroupBedsMath.Size(5, 0, 0) == 5, "no guest bed at all: Hospitality's own rule stands");
        C(GuestGroupBedsMath.Size(0, 3, 3) == 0, "nobody to send: nobody comes");
        C(GuestGroupBedsMath.Size(-2, 3, 3) == 0, "a nonsense size: nobody comes");
        C(GuestGroupBedsMath.Size(5, 3, -1) == 0, "a nonsense free count: nobody comes rather than a negative group");
        for (int wanted = 0; wanted <= 8; wanted++)
            for (int places = 0; places <= 6; places++)
                for (int free = 0; free <= places; free++)
                {
                    int s = GuestGroupBedsMath.Size(wanted, places, free);
                    C(s >= 0 && s <= wanted && (places == 0 || s <= free), $"never more than wanted or than free beds ({wanted}, {places}, {free})");
                }

        string code = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Integrations/GuestGroupBeds.cs"));
        C(code.Contains("\"Hospitality.IncidentWorker_VisitorGroup\"") && code.Contains("\"GetPawnsToSpawn\""), "the patch finds Hospitality's own list of guests to send");
        C(code.Contains("__0.pawnCount > 0"), "a size asked for outright is left alone");
        string settings = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/UI/SettingsCatalog.cs"));
        C(settings.Contains("\"guestGroupsFitBeds\""), "the setting is on the Hospitality section of the settings");
        return n;
    }
}
