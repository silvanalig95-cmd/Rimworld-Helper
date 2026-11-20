using ColonyCouncil;

/// <summary>
/// A piece made for someone with Nice Inventory Tab, put on when they are back (the player, 18 November 2026). The rule
/// (Integrations/MadeForWearMath.cs) and the seams with the mod and with Rimfolk's carrying (Integrations/MadeForWear.cs).
/// </summary>
public static class MadeForWearTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a piece made for someone: " + what);
        }

        C(MadeForWearMath.GoNow(true, true, true, 120f), "back in the home area with the piece in storage there: they go");
        C(MadeForWearMath.GoNow(true, true, false, 30f) && !MadeForWearMath.GoNow(true, true, false, 60f), "the piece outside the home area: only within a short walk");
        C(!MadeForWearMath.GoNow(true, false, true, 10f), "out of the home area (at work far off, on a trip): they keep working");
        C(!MadeForWearMath.GoNow(false, true, true, 5f), "drafted, downed, in a break, starving or under threat: not now");
        C(!MadeForWearMath.Stale(0, 14 * 60000) && MadeForWearMath.Stale(0, 16 * 60000), "let go of after fifteen days");

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Integrations", "MadeForWear.cs"));
        C(src.Contains("AccessTools.TypeByName(\"NiceInventoryTab.BillFinished_Patch\")") && src.Contains("AccessTools.Method(MadeForWear.PatchType, \"ExecuteFinishRecipeLogic\")"),
            "the mod's own finishing of a bill made for someone, by name, only with the mod");
        C(src.Contains("public static void Prefix(Pawn __2, out Pawn __state)") && src.Contains("public static Exception Finalizer(Exception __exception, Pawn __state)"),
            "whom it is for, set while the mod finishes and always put back");
        string pockets = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Hauling", "WorkProductPatches.cs"));
        C(pockets.Contains("if (MadeForWear.finishingFor != null) return;"), "the crafter keeps nothing of it, so the mod places it and gives its order as before");
        C(src.Contains("typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), new[] { typeof(Job), typeof(JobTag?), typeof(bool) }")
          && src.Contains("if (MadeForWear.Near(target, piece)) return true;"), "the mod's order held only for one who isn't near; one nearby goes at once");
        C(src.Contains("typeof(Messages), nameof(Messages.Message), new[] { typeof(string), typeof(MessageTypeDef), typeof(bool) }")
          && src.Contains("!(MadeForWear.held && MadeForWear.finishingFor != null)"), "the mod's message left out only after Rimfolk held the order and said so");
        C(src.Contains("[HarmonyPatch(typeof(Pawn_JobTracker), \"DetermineNextJob\")]") && src.Contains("__instance?.curJob != null")
          && src.Contains("job.playerForced = true;"), "put on between jobs, forced as the mod's own order was");
        C(src.Contains("!EquipmentUtility.CanEquip(piece, p) || p.apparel.WouldReplaceLockedApparel(piece)") && src.Contains("piece.ParentHolder is Pawn_ApparelTracker")
          && !src.Contains("CanWearWithoutDroppingAnything"),
            "let go of once worn, or when they can't wear it or it would take off something locked; not because they wear something in its place");
        C(MadeForWearMath.KeptFrom(true, false, false), "kept from everyone else, the crafter too, while it waits");
        C(!MadeForWearMath.KeptFrom(true, true, false), "not kept from the one it was made for");
        C(!MadeForWearMath.KeptFrom(false, false, false) && !MadeForWearMath.KeptFrom(true, false, true), "free for anyone once the one it was for is dead or the wait has run out");
        C(MadeForWearMath.NotForThem < 0f, "worth nothing to the others looking for better clothes");
        C(src.Contains("[HarmonyPatch(typeof(JobGiver_OptimizeApparel), \"ApparelScoreGain\")]") && src.Contains("if (book.KeptFromOthers(ap, pawn)) __result = MadeForWearMath.NotForThem;"),
            "the game's look for better clothes passes over a piece kept for someone else");
        C(src.Contains("RimfolkPatches.On && (ColonyCouncilMod.Settings?.madeForWearLater ?? true)"), "the holding behind the deeper hooks and its setting");
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(catalog.Contains("Mod(Toggle(\"madeForWearLater\", \"CC_Settings_MadeForWearLater\", \"CC_SetNote_MadeForWearLater\", \"deepHooks\"), NiceInventoryTabId);"),
            "the setting on the other mods' page, shown with the mod");
        return n;
    }
}
