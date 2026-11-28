using System;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Dynamic Atmosphere's sky kept out of roofed rooms (Source/ColonyCouncil/Integrations/SkyIndoors.cs, SkyIndoorsMath.cs):
/// which of its materials are the sky's, the open cells as runs along each row, the whole-map plane's texture coordinates
/// kept, the mesh's index size; and the seams (found by name and soft, its own drawing left when anything differs, the
/// setting and its words, the roof patch).
/// </summary>
public static class SkyIndoorsTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the sky over roofed rooms: " + what);
        }

        // ================= Which layers =================

        C(SkyIndoorsMath.SkyMaterial("DA_MapCloudShadow") && SkyIndoorsMath.SkyMaterial("DA_MapHaze")
          && SkyIndoorsMath.SkyMaterial("DA_MapEvent0") && SkyIndoorsMath.SkyMaterial("DA_MapEventAdd3"),
            "the cloud shadows, the haze and the event tints are the sky's");
        C(!SkyIndoorsMath.SkyMaterial("DA_MapPrecip0") && !SkyIndoorsMath.SkyMaterial("DA_MapRipples") && !SkyIndoorsMath.SkyMaterial("")
          && !SkyIndoorsMath.SkyMaterial(null), "rain and ripples are left to the mod, which keeps them from under roofs itself");

        // ================= The open cells as runs =================

        bool[] Grid(params string[] rows)
        {
            int w = rows[0].Length;
            var g = new bool[w * rows.Length];
            for (int z = 0; z < rows.Length; z++)
                for (int x = 0; x < w; x++)
                    g[z * w + x] = rows[z][x] == '.';
            return g;
        }

        var open = SkyIndoorsMath.Runs(Grid("....", "....", "...."), 4, 3);
        C(open.Count == 3 && open.All(r => r.x0 == 0 && r.x1 == 3), "a map with no roofs is one run a row, the whole width");
        var house = SkyIndoorsMath.Runs(Grid("......", ".##...", ".##..#"), 6, 3);
        C(house.Count == 5 && house[0].x0 == 0 && house[0].x1 == 5 && house[1].x0 == 0 && house[1].x1 == 0 && house[2].x0 == 3 && house[2].x1 == 5
          && house[3].z == 2 && house[4].x0 == 3 && house[4].x1 == 4, "roofed cells split a row into the runs either side of them");
        C(SkyIndoorsMath.Runs(Grid("##", "##"), 2, 2).Count == 0, "all roofed: no runs, nothing of the sky drawn");
        C(SkyIndoorsMath.Runs(null, 3, 3).Count == 0 && SkyIndoorsMath.Runs(new bool[2], 3, 3).Count == 0, "a grid too short reads as no runs");
        var every = SkyIndoorsMath.Runs(Grid(".#.#.#", "#.#.#."), 6, 2);
        C(every.Count == 6 && every.All(r => r.x0 == r.x1), "a chequer of roofs: a run for each open cell");
        C(SkyIndoorsMath.Vertices(250 * 125) > SkyIndoorsMath.MaxVertices16 && SkyIndoorsMath.Vertices(1000) < SkyIndoorsMath.MaxVertices16,
            "a mesh past 65,000 vertices takes 32-bit indices; a map's usual few hundred runs don't need them");

        // ================= The plane's texture coordinates =================

        C(Math.Abs(SkyIndoorsMath.PlaneUv(125.5f, 125.5f) - 100f) < 1e-4 && Math.Abs(SkyIndoorsMath.PlaneUv(135.5f, 125.5f) - 101f) < 1e-4
          && Math.Abs(SkyIndoorsMath.PlaneUv(-874.5f, 125.5f)) < 1e-3, "the open-sky mesh gives each point the whole-map plane's coordinate (0 to 200 across 2000 cells round the centre)");
        C(SkyIndoorsMath.RebuildEvery == 0.5f, "built again at most twice a second, as the mod's own roof mask");

        // ================= The seams =================

        string code = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Integrations", "SkyIndoors.cs"));
        C(code.Contains("\"DynamicAtmosphere.MapComponent_Atmosphere\"") && code.Contains("\"DrawQuad\"") && code.Contains("GenTypes.GetTypeInAnyAssembly")
          && code.Contains("public static bool Prepare() => SkyIndoors.Target() != null"), "found by name; without the mod no patch is made");
        C(code.Contains("if (!RimfolkPatches.On || !SkyIndoors.On) return true;") && code.Contains("return !SkyIndoors.Draw(mat, center, altitude);"),
            "its own drawing stands with the setting or the deeper hooks off, or whenever Rimfolk doesn't draw");
        C(code.Contains("failed = true;") && code.Contains("CouncilLog.ErrorOnce"), "an error draws as the mod does from then on, logged once");
        C(code.Contains("!roofs.Roofed(i) && !fog.IsFogged(i)"), "open means not roofed and not fogged, as the mod's own roof mask reads it");
        C(code.Contains("[HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]") && code.Contains("CellFogChanged") && code.Contains("MapFogged"),
            "a roof or the fog changing builds the mesh again");
        C(!code.Contains("Messages.Message"), "no messages");
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        C(settings.Contains("public bool skyIndoorsClear = true;") && settings.Contains("Scribe_Values.Look(ref skyIndoorsClear, \"skyIndoorsClear\", true);")
          && catalog.Contains("Mod(Toggle(\"skyIndoorsClear\", \"CC_Settings_SkyIndoors\", \"CC_SetNote_SkyIndoors\", \"deepHooks\"), DynamicAtmosphereId)")
          && catalog.Contains("DynamicAtmosphereId = \"astryl.dynamicatmosphere\""), "a setting, on by default, on the other mods' page under Dynamic Atmosphere");
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        C(new[] { "CC_Settings_SkyIndoors", "CC_Settings_SkyIndoors_Tip", "CC_SetNote_SkyIndoors", "CC_SetSec_Mod_DynamicAtmosphere" }.All(k => keyed.Contains("<" + k + ">")),
            "its words are there");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
