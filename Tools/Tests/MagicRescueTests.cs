/// <summary>
/// A RimWorld of Magic under Harmony 3 (the player's logs, 7 and 17 November 2026): its patching done in HugsLib's place
/// so the two patches this Harmony refuses don't stop the rest or log an error, and those two run by Rimfolk
/// (Integrations/MagicPatchRescue.cs). Source checks: the mod's types aren't in the tests.
/// </summary>
public static class MagicRescueTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "A RimWorld of Magic's patches: " + what);
        }

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Integrations", "MagicPatchRescue.cs"));
        C(src.Contains("return !MagicPatchRescue.TryApplyInstead(__instance);"), "a prefix on HugsLib's step skips it only when the work was done in its place");
        C(src.Contains("public static void Postfix(object __instance)") && src.Contains("MagicPatchRescue.TryRescue(true)"), "the rescue afterwards stays, for when HugsLib ran after all");
        C(src.Contains("\"HarmonyAutoPatch\"") && src.Contains("\"ShouldHarmonyAutoPatch\"") && src.Contains("\"HarmonyInst\"") && src.Contains("\"ModContentPack\"")
          && src.Contains("harmonyInst.SetValue(modBase, new Harmony(id))"), "what HugsLib 12 does there is done: its checks, the mod's Harmony instance under its own id");
        C(src.Contains("broken == null && caravanBroken == null) return false;"), "with nothing of the mod refused, HugsLib runs as before");
        C(src.Contains("if (broken != null) skip.Add(broken);") && src.Contains("if (caravanBroken != null) skip.Add(caravanBroken);"), "both refused classes are left out of the one-at-a-time apply");
        C(src.Contains("ps[2].ParameterType != typeof(PlanetLayer)") && src.Contains("\"CostToMove_Caravan_Postfix\""), "the Wanderer's patch is known by the shape Harmony refuses");
        C(src.Contains("wanderer?.Invoke(null, caravan, null, end, ref result, ticksAbs)"), "the Wanderer's own postfix runs with no start layer (its body never reads it)");
        C(src.Contains("if (!MagicPatchRescue.CaravanOurs || caravan == null) return;") && src.Contains("!TheirsApplied(Patch_MagicWandererTravel.TargetMethod(), caravanPostfix)"),
            "the Wanderer's travel runs here only while the mod's own patch isn't on");
        C(src.Contains("typeof(Caravan), typeof(PlanetTile), typeof(PlanetTile), typeof(int?)"), "the game's move cost, by its own parameter types");
        C(src.Contains("public static bool TryApplyInstead(object modBase)") && src.Contains("catch (Exception e)"), "never throws into HugsLib's start");
        return n;
    }
}
