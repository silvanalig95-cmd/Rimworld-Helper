using ColonyCouncil;

/// <summary>
/// Hospitality's guest groups fit the guest beds (the player's report, 7 November 2026: "why do 4-5 people visitor
/// groups stay in my colony if i only have one reserved visitor bed?"): the size rule
/// (Integrations/GuestGroupBedsMath.cs), now and then one or two more, the rest walking past, and that the patch and its
/// settings are wired.
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

        // Now and then one or two more (the player: "i don't mind if sometimes one or two too many visitors arrive, but not like four").
        C(GuestGroupBedsMath.Size(5, 1, 1, 2) == 3, "one free bed and two extra allowed: three come");
        C(GuestGroupBedsMath.Size(5, 2, 0, 1) == 1, "every bed taken but one extra this time: one comes");
        C(GuestGroupBedsMath.Size(2, 4, 4, 2) == 2, "extra never makes a group bigger than it was");
        C(GuestGroupBedsMath.Size(5, 0, 0, 2) == 5, "no guest bed at all: the extra doesn't matter");
        C(GuestGroupBedsMath.Extra(0f, 2) == 0 && GuestGroupBedsMath.Extra(0.49f, 2) == 0, "half the groups bring nobody extra");
        C(GuestGroupBedsMath.Extra(0.5f, 2) == 1 && GuestGroupBedsMath.Extra(0.74f, 2) == 1, "a quarter bring one more");
        C(GuestGroupBedsMath.Extra(0.75f, 2) == 2 && GuestGroupBedsMath.Extra(0.999f, 2) == 2 && GuestGroupBedsMath.Extra(1f, 2) == 2, "a quarter bring two more");
        C(GuestGroupBedsMath.Extra(0.9f, 0) == 0 && GuestGroupBedsMath.Extra(0.9f, -1) == 0, "at 0 nobody extra ever comes");
        for (int most = 0; most <= 4; most++)
            for (int r = 0; r <= 100; r++)
            {
                int e = GuestGroupBedsMath.Extra(r / 100f, most);
                C(e >= 0 && e <= most, $"extra never above the setting ({most}, {r})");
            }
        for (int wanted = 0; wanted <= 8; wanted++)
            for (int free = 0; free <= 4; free++)
                for (int extra = 0; extra <= 4; extra++)
                {
                    int s = GuestGroupBedsMath.Size(wanted, 4, free, extra);
                    C(s >= 0 && s <= wanted && s <= free + extra, $"never more than the free beds and the extra ({wanted}, {free}, {extra})");
                }

        string code = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Integrations/GuestGroupBeds.cs"));
        C(code.Contains("\"Hospitality.IncidentWorker_VisitorGroup\"") && code.Contains("\"GetPawnsToSpawn\""), "the patch finds Hospitality's own list of guests to send");
        C(code.Contains("__0.pawnCount > 0"), "a size asked for outright is left alone");
        // The ones left out walk past ("otherwise they should just walk by basically").
        C(code.Contains("GuestGroupBeds.WalkPast(group.Skip(size)"), "the guests left out of a group walk past");
        C(code.Contains("new LordJob_TravelAndExit(dest)") && code.Contains("RCellFinder.TryFindTravelDestFrom(parms.spawnCenter"),
            "they cross the map from where the group came in, like the game's own travellers");
        C(code.Contains("WorldPawnSituation.Free") && code.Contains("faction.HostileTo(Faction.OfPlayer)"), "only free pawns of a friendly people walk past");
        C(code.Contains("\"CC_GuestsWalkPast\"") && code.Contains("\"CC_GuestsWalkPastOne\"") && !code.Contains("Letter"), "a message says so, no letter");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages/English/Keyed/Rimfolk_Guests.xml"));
        C(keyed.Contains("<CC_GuestsWalkPast>") && keyed.Contains("<CC_GuestsWalkPastOne>") && keyed.Contains("<CC_Settings_GuestBedsExtra>"), "the walk-past and extra texts exist");
        string settings = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/UI/SettingsCatalog.cs"));
        C(settings.Contains("\"guestGroupsFitBeds\""), "the setting is on the Hospitality section of the settings");
        C(settings.Contains("Slider(\"guestBedsExtra\"") && settings.Contains("0f, 4f, 1f, \"0\", \"guestGroupsFitBeds\""), "the extra-guests slider, 0 to 4, under the setting");
        string cfg = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/CouncilSettings.cs"));
        C(cfg.Contains("public int guestBedsExtra = 2;") && cfg.Contains("Scribe_Values.Look(ref guestBedsExtra, \"guestBedsExtra\", 2);"), "two extra by default, saved");
        return n;
    }
}
