using ColonyCouncil;

/// <summary>
/// Vanilla Factions Expanded - Empire's ballroom only where the room is for music (the player, 17 November 2026: a drum
/// turned their dining room with an altar into a ballroom; "ballroom seems to override temple, which is not correct").
/// The rule (Integrations/BallroomMath.cs), the patches and the setting (Integrations/BallroomByUse.cs).
/// </summary>
public static class BallroomTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the ballroom: " + what);
        }

        C(BallroomMath.StandsDown(temple: true, diningTables: false, instruments: 1, drums: 0), "a temple with a harp stays a temple");
        C(BallroomMath.StandsDown(false, true, 1, 0), "a dining room with a piano stays a dining room");
        C(BallroomMath.StandsDown(true, true, 1, 1), "the player's room (tables, altar, drum) isn't a ballroom");
        C(BallroomMath.StandsDown(false, false, 1, 1) && BallroomMath.StandsDown(false, false, 2, 2), "drums alone make no ballroom");
        C(!BallroomMath.StandsDown(false, false, 2, 1), "a drum with a harp in a room for music is a ballroom");
        C(!BallroomMath.StandsDown(false, false, 1, 0), "a harp in a rec room is still the mod's ballroom");
        C(!BallroomMath.StandsDown(false, false, 0, 0), "no instrument, nothing to stand down from");

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Integrations", "BallroomByUse.cs"));
        C(src.Contains("\"VFEEmpire.RoomRoleWorker_Ballroom\"") && src.Contains("\"GetScore\"") && src.Contains("\"GetScoreDeltaIfBuildingPlaced\""),
            "VFE Empire's room role, found by name, its score and its placing note");
        C(src.Contains("if (__result <= 0f || !BallroomByUse.On) return;") && src.Contains("RimfolkPatches.On && (ColonyCouncilMod.Settings?.ballroomByUse ?? true)"),
            "only where the mod gives a ballroom, with the deeper hooks and the setting");
        C(src.Contains("GetNamedSilentFail(\"WorshipRoom\")") && src.Contains("d.surfaceType == SurfaceType.Eat"), "the temple by the game's own worship room, the dining room by its tables");
        C(src.Contains("StandsDownWhy()"), "without the mod, the hooks stand down quietly");
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(catalog.Contains("Mod(Toggle(\"ballroomByUse\", \"CC_Settings_BallroomByUse\", \"CC_SetNote_BallroomByUse\", \"deepHooks\"), VfeEmpireId);"), "the setting on the other mods' page");
        return n;
    }
}
