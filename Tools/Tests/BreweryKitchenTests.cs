using ColonyCouncil;

/// <summary>
/// A brewery at home in the kitchen (the player, 18 November 2026: "i think the brewery should also be able to be placed
/// in the kitchen"). The rules (Building/BreweryKitchenMath.cs), the hooks and the setting (Building/BreweryKitchen.cs).
/// </summary>
public static class BreweryKitchenTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the brewery in the kitchen: " + what);
        }

        C(BreweryKitchenMath.CookingBench(new[] { "Cooking" }), "the brewery's one recipe, wort, is cooking work");
        C(BreweryKitchenMath.CookingBench(new[] { "Cooking", null, "Cooking" }), "recipes without a skill don't change it");
        C(!BreweryKitchenMath.CookingBench(new[] { "Cooking", "Crafting" }) && !BreweryKitchenMath.CookingBench(new[] { "Crafting" }),
            "a bench with any other kind of work stays the workshop's only");
        C(!BreweryKitchenMath.CookingBench(new string[0]) && !BreweryKitchenMath.CookingBench(new string[] { null }) && !BreweryKitchenMath.CookingBench(null),
            "no recipes, or none naming a skill: not a cooking bench");

        // The game (1.6): a kitchen 28 a stove, a workshop 27 a bench, the workshop first on a tie.
        C(BreweryKitchenMath.WorkshopScore(54f, 2, 2, true) == 0f, "a stove and two breweries: the breweries count for the kitchen, so it stays one (56 against 0, not 28 against 54)");
        C(BreweryKitchenMath.WorkshopScore(54f, 2, 1, true) == 27f, "a stove, a brewery and a tailor's bench: only the tailor's bench counts for the workshop");
        C(BreweryKitchenMath.WorkshopScore(54f, 2, 2, false) == 54f, "breweries with no stove: still the game's workshop");
        C(BreweryKitchenMath.WorkshopScore(81f, 3, 0, true) == 81f, "a workshop with no brewery is left as it is");
        C(BreweryKitchenMath.WorkshopScore(0f, 0, 0, true) == 0f && BreweryKitchenMath.WorkshopScore(27f, 1, 5, true) == 0f, "never below nothing");

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Building", "BreweryKitchen.cs"));
        C(src.Contains("[HarmonyPatch(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.Applies))]")
          && src.Contains("[HarmonyPatch(typeof(StatPart_WorkTableRoomRole), nameof(StatPart_WorkTableRoomRole.WouldApplyToBuildingIfPlaced))]"),
            "the game's one check of a bench in the wrong room, and its check while placing: speed, the stat's line and the note");
        C(src.Contains("[HarmonyPatch(typeof(RoomRoleWorker_Workshop), nameof(RoomRoleWorker_Workshop.GetScore))]")
          && src.Contains("[HarmonyPatch(typeof(RoomRoleWorker_Workshop), nameof(RoomRoleWorker_Workshop.GetScoreDeltaIfBuildingPlaced))]"),
            "the workshop's score, and its score while placing");
        C(src.Contains("d.building.workTableRoomRole != RoomUtil.Workshop") && src.Contains("r?.workSkill?.defName"),
            "a cooking bench: a workshop bench whose recipes are cooking, read from its def, so mods' brewing benches count too");
        C(src.Contains("kitchen.Worker.GetScore(room) > 0f"), "a stove, as the game's kitchen counts it");
        C(src.Contains("RimfolkPatches.On && (ColonyCouncilMod.Settings?.breweryInKitchen ?? true)"), "behind the deeper hooks and its setting");
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(catalog.Contains("Toggle(\"breweryInKitchen\", \"CC_Settings_BreweryInKitchen\", \"CC_SetNote_BreweryInKitchen\", \"deepHooks\");"), "the setting, under the deeper hooks");
        return n;
    }
}
