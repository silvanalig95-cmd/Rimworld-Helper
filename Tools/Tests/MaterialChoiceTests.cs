using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Choosing the material when ordering a piece from Nice Inventory Tab's gear tab (the player, 19 November 2026). The rule
/// (Integrations/MaterialChoiceMath.cs: which materials are offered, in what order, the counts) and the seams with the mod
/// (Integrations/MaterialChoice.cs).
/// </summary>
public static class MaterialChoiceTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "choosing the material: " + what);
        }

        C(MaterialChoiceMath.Enough(80, 80) && !MaterialChoiceMath.Enough(79, 80), "enough for one piece is at least what one piece takes");
        C(!MaterialChoiceMath.Enough(500, 0) && !MaterialChoiceMath.Enough(0, 0), "a material one piece takes none of is never offered");
        C(MaterialChoiceMath.Pieces(340, 80) == 4 && MaterialChoiceMath.Pieces(79, 80) == 0 && MaterialChoiceMath.Pieces(10, 0) == 0 && MaterialChoiceMath.Pieces(-5, 10) == 0,
            "how many pieces what is on hand makes, whole pieces only");

        var rows = new List<MaterialChoiceMath.Stuff>
        {
            new MaterialChoiceMath.Stuff("devilstrand", 120, 80),
            new MaterialChoiceMath.Stuff("thrumbo leather", 60, 80),
            new MaterialChoiceMath.Stuff("cloth", 340, 80),
            new MaterialChoiceMath.Stuff("synthread", 160, 80),
            new MaterialChoiceMath.Stuff("Hyperweave", 175, 80),
            new MaterialChoiceMath.Stuff("gold", 500, 800),
        };
        var offered = MaterialChoiceMath.Offered(rows);
        C(!offered.Contains(1) && !offered.Contains(5), "only materials with enough on hand for one piece are offered (a small-volume one counted in its own units)");
        C(offered.SequenceEqual(new[] { 2, 4, 3, 0 }), "the one that makes the most pieces first, then by name whatever the case: cloth, hyperweave, synthread, devilstrand");
        var same = new List<MaterialChoiceMath.Stuff> { new MaterialChoiceMath.Stuff("wool", 100, 50), new MaterialChoiceMath.Stuff("wool", 100, 50) };
        C(MaterialChoiceMath.Offered(same).SequenceEqual(new[] { 0, 1 }), "two of the same name and count keep their order");
        C(MaterialChoiceMath.Offered(new List<MaterialChoiceMath.Stuff>()).Count == 0 && MaterialChoiceMath.Offered(null).Count == 0,
            "nothing to offer from nothing");
        C(MaterialChoiceMath.Offered(new List<MaterialChoiceMath.Stuff> { new MaterialChoiceMath.Stuff("cloth", 10, 80) }).Count == 0,
            "none with enough: nothing offered (the menu then says so under \"Any material\")");

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Integrations", "MaterialChoice.cs"));
        C(src.Contains("AccessTools.TypeByName(\"NiceInventoryTab.CommandUtility\")")
          && src.Contains("AccessTools.Method(CommandType, \"CommandCreate\", new[] { typeof(Building_WorkTable), typeof(ThingDef), typeof(RecipeDef), typeof(ApparelLayerDef) })"),
            "the mod's order from the gear tab, by name, only with the mod");
        C(src.Contains("public static bool Prepare()") && src.Contains("return MaterialChoice.Create != null;") && src.Contains("CouncilLog.WarnOnce(\"Nice Inventory Tab is loaded, but its order"),
            "stands down without the mod, and says so once when the mod is there but its order isn't");
        C(src.Contains("if (MaterialChoice.making || broken || !MaterialChoice.On) return true;") && src.Contains("return !MaterialChoice.Offer(__0, __1, __2, __3);"),
            "the mod's own way when the step is off, after a failure, or when Rimfolk itself calls the order");
        C(src.Contains("RimfolkPatches.On && (ColonyCouncilMod.Settings?.materialChoice ?? true)"), "behind the deeper hooks and its setting");
        C(src.Contains("!product.MadeFromStuff || !recipe.productHasIngredientStuff") && src.Contains("if (ing == null) return false;"),
            "only a piece made from a material asks; any other goes straight on");
        C(src.Contains("if (ing?.filter == null || ing.IsFixedIngredient) continue;") && src.Contains("GenStuff.AllowedStuffsFor(product)")
          && src.Contains("recipe.fixedIngredientFilter.Allows(s)"),
            "the materials the recipe takes for the piece, from its material ingredient (never a fixed one like components)");
        C(src.Contains("t.Spawned && !t.Fogged() && (player == null || !t.IsForbidden(player))") && src.Contains("ing.CountRequiredOfFor(s, recipe, null)"),
            "counted on the map, seen and not forbidden; one piece's need by the game's own count for that material");
        int any = src.IndexOf("\"CC_MaterialChoice_Any\"", StringComparison.Ordinal), stuff = src.IndexOf("\"CC_MaterialChoice_Stuff\"", StringComparison.Ordinal);
        C(any > 0 && stuff > any && src.Contains("() => Make(bench, piece, recipe, layer, pawn, null, null)"), "\"Any material\" first, making the bill as the mod does");
        C(src.Contains("shownItemForIcon: stuff"), "each material with its own icon");
        C(src.Contains("Create.Invoke(null, new object[] { bench, piece, recipe, layer });") && src.Contains("finally") && src.Contains("making = false;"),
            "the bill made by the mod's own order (its wear request, name and message), and the way back always reset");
        C(src.Contains("if (swap) LastPawn.SetValue(null, pawn);") && src.Contains("if (swap) LastPawn.SetValue(null, was);"),
            "made for the colonist the menu was opened for, and the mod's own note of the tab put back");
        C(src.Contains("b.recipe == recipe && !before.Contains(b)") && src.Contains("if (d != stuff && filter.Allows(d)) filter.SetAllow(d, false);")
          && src.Contains("filter.SetAllow(stuff, true);"),
            "only the new bill, its other materials unticked and the chosen one allowed; the rest of its filter left alone");

        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_MaterialChoice.xml"));
        var used = Regex.Matches(src, "\"(CC_MaterialChoice_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 6 && used.All(k => keys.Contains("<" + k + ">")), "every word the menu uses is written");
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(catalog.Contains("Mod(Toggle(\"materialChoice\", \"CC_Settings_MaterialChoice\", \"CC_SetNote_MaterialChoice\", \"deepHooks\"), NiceInventoryTabId);")
          && keys.Contains("<CC_Settings_MaterialChoice>Choose the material when ordering a piece</CC_Settings_MaterialChoice>"),
            "the setting on the other mods' page under Nice Inventory Tab, shown with the mod");
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        C(settings.Contains("public bool materialChoice = true;") && settings.Contains("Scribe_Values.Look(ref materialChoice, \"materialChoice\", true);"), "the setting saved, on");
        return n;
    }
}
