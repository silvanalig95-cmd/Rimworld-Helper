using ColonyCouncil;

/// <summary>
/// The temperature in colour (the player's request, 10 November 2026: "can the temperature meter be clearly color coded
/// from blue to red, so i can at immediate glance see how warm or cold it is"): the one scale (TemperatureScaleMath) from
/// deep blue to white to deep red by the colonists' comfort, readable on every ground, and the seams: the game's readout
/// at the bottom right and the vitals' outdoor temperature both take it.
/// </summary>
public static class TemperatureScaleTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the temperature in colour: " + what);
        }

        const float lo = HudRank.HumanComfyMin, hi = HudRank.HumanComfyMax;
        uint At(float t) => TemperatureScaleMath.Rgb(t, lo, hi);
        float R(uint c) => ((c >> 16) & 0xff) / 255f;
        float G(uint c) => ((c >> 8) & 0xff) / 255f;
        float B(uint c) => (c & 0xff) / 255f;

        // ---------- The steps, around the human comfort range (16 to 26 °C) ----------
        var steps = new (float t, uint rgb, string name)[]
        {
            (-80f, TemperatureScaleMath.DeepBlue, "deep blue"), (-30f, TemperatureScaleMath.DeepBlue, "deep blue"), (-15f, TemperatureScaleMath.Blue, "blue"),
            (-4f, TemperatureScaleMath.LightBlue, "light blue"), (6f, TemperatureScaleMath.PaleBlue, "pale blue"), (16f, TemperatureScaleMath.Neutral, "white"),
            (21f, TemperatureScaleMath.Neutral, "white"), (26f, TemperatureScaleMath.Neutral, "white"), (32f, TemperatureScaleMath.PaleYellow, "pale yellow"),
            (36f, TemperatureScaleMath.Yellow, "yellow"), (40f, TemperatureScaleMath.Orange, "orange"), (50f, TemperatureScaleMath.DeepRed, "deep red"),
            (90f, TemperatureScaleMath.DeepRed, "deep red")
        };
        foreach (var (t, rgb, name) in steps) C(At(t) == rgb, $"{t} °C is {name} ({TemperatureScaleMath.Hex(At(t))})");
        C(TemperatureScaleMath.DeadlyBelow == HudRank.HumanComfyMin - HudRank.DeadlyCold && TemperatureScaleMath.FrostBelow == HudRank.HumanComfyMin - HudRank.HardFrost
          && TemperatureScaleMath.FierceAbove == HudRank.FierceHeat - HudRank.HumanComfyMax && TemperatureScaleMath.DeadlyAbove == HudRank.DeadlyHeat - HudRank.HumanComfyMax,
            "with the human range, deep blue and deep red fall on the vitals' red lines, blue and orange on its orange ones");
        C(TemperatureScaleMath.SafeMargin == 10f, "pale blue and yellow sit at the game's safe line, ten degrees outside the comfort range");
        C(At(15.5f) != TemperatureScaleMath.Neutral && At(26.5f) != TemperatureScaleMath.Neutral, "a half degree outside the comfort range already leaves white");

        // ---------- Blue to red: the cold side blue, the warm side warm, lightest where it's comfortable ----------
        bool blue = true, warm = true, lighter = true, darker = true, smooth = true;
        double lastLum = 0;
        uint last = At(-80f);
        for (float t = -80f; t <= 90f; t += 0.25f)
        {
            uint c = At(t);
            double lum = SkinMath.Luminance(c);
            if (t < lo - 0.5f && !(B(c) > R(c))) blue = false;
            if (t > hi + 0.5f && !(R(c) > B(c))) warm = false;
            // A channel's rounding to whole steps may wobble the lightness by a hair; never more.
            if (t > -80f && t <= lo && lum + 0.002 < lastLum) lighter = false;
            if (t > hi && lum > lastLum + 0.002) darker = false;
            if (Math.Abs(R(c) - R(last)) > 0.03f || Math.Abs(G(c) - G(last)) > 0.03f || Math.Abs(B(c) - B(last)) > 0.03f) smooth = false;
            lastLum = lum;
            last = c;
        }
        C(blue, "everything colder than comfortable is a blue (more blue than red)");
        C(warm, "everything hotter than comfortable is warm (more red than blue)");
        C(lighter, "from killing cold up to comfortable each degree is lighter");
        C(darker, "from comfortable up to killing heat each degree is deeper");
        C(SkinMath.Luminance(At(-30f)) < SkinMath.Luminance(At(-15f)) && SkinMath.Luminance(At(-15f)) < SkinMath.Luminance(At(6f)) && SkinMath.Luminance(At(6f)) < SkinMath.Luminance(At(21f))
          && SkinMath.Luminance(At(21f)) > SkinMath.Luminance(At(36f)) && SkinMath.Luminance(At(36f)) > SkinMath.Luminance(At(40f)) && SkinMath.Luminance(At(40f)) > SkinMath.Luminance(At(50f)),
            "step by step, the lightest is comfortable and the deepest the killing ends");
        C(smooth, "the colour goes over smoothly from step to step (no jump at a quarter degree)");
        C(B(TemperatureScaleMath.DeepBlue) == 1f && R(TemperatureScaleMath.DeepBlue) < 0.45f, "deep blue is a strong blue");
        C(R(TemperatureScaleMath.DeepRed) == 1f && G(TemperatureScaleMath.DeepRed) < 0.4f && B(TemperatureScaleMath.DeepRed) < 0.4f, "deep red is a strong red");
        C(G(TemperatureScaleMath.Orange) > G(TemperatureScaleMath.DeepRed) + 0.2f && G(TemperatureScaleMath.Yellow) > G(TemperatureScaleMath.Orange) + 0.15f,
            "yellow, then orange, then red as the heat grows");
        var neutral = TemperatureScaleMath.Neutral;
        C(R(neutral) == G(neutral) && G(neutral) == B(neutral) && R(neutral) > 0.9f, "comfortable is white");

        // ---------- Readable: on Rimfolk's dark panels in both looks, and over the game's map behind the dark edge ----------
        // The game's look on the classic panels over a dark map (as HudTemperatureTests measured it), Rimfolk's own look's
        // cards (its panel for the dark look, its window ground for the darker and the clear ones), and black: the dark edge
        // round the game's own readout.
        uint classic = 0x181a1c;
        var grounds = new (string name, uint rgb)[] { ("the classic panels", classic), ("the own look's dark card", SkinMath.Panel), ("the own look's darker card", SkinMath.WindowGround), ("the readout's dark edge", 0x000000) };
        foreach (var (name, ground) in grounds)
        {
            double worst = 99;
            float worstAt = 0;
            for (float t = -80f; t <= 90f; t += 0.25f)
            {
                double ratio = SkinMath.Contrast(At(t), ground);
                if (ratio < worst)
                {
                    worst = ratio;
                    worstAt = t;
                }
            }
            C(worst >= SkinMath.TextContrast, $"every temperature reads on {name} (at worst {worst:0.0} to 1, at {worstAt} °C)");
        }
        C(TemperatureScaleMath.EdgeAlpha >= 0.5f && TemperatureScaleMath.EdgeAlpha <= 0.9f, "the readout's dark edge is solid enough to read over snow without looking heavy");

        // ---------- Another comfort range, the edges ----------
        C(TemperatureScaleMath.Rgb(20f, 10f, 30f) == neutral && TemperatureScaleMath.Rgb(0f, 10f, 30f) == TemperatureScaleMath.PaleBlue
          && TemperatureScaleMath.Rgb(40f, 10f, 30f) == TemperatureScaleMath.Yellow, "a comfort range a mod moved moves the scale with it");
        C(TemperatureScaleMath.Rgb(5f, 30f, 10f) == TemperatureScaleMath.Rgb(5f, 10f, 30f), "a comfort range given the wrong way round is taken the right way");
        C(TemperatureScaleMath.Rgb(16f, 16f, 16f) == neutral && TemperatureScaleMath.Rgb(6f, 16f, 16f) == TemperatureScaleMath.PaleBlue, "a comfort range of one degree still works");
        C(TemperatureScaleMath.Rgb(float.NaN, lo, hi) == neutral && TemperatureScaleMath.Rgb(float.PositiveInfinity, lo, hi) == neutral, "an unknown temperature is white, never a warning");
        C(TemperatureScaleMath.Rgb(-30f, float.NaN, 26f) == TemperatureScaleMath.DeepBlue && TemperatureScaleMath.Rgb(50f, float.NaN, float.NaN) == TemperatureScaleMath.DeepRed,
            "an unknown comfort range is the human one");
        C(TemperatureScaleMath.Mix(0x000000, 0xffffff, 0f) == 0x000000 && TemperatureScaleMath.Mix(0x000000, 0xffffff, 1f) == 0xffffff
          && TemperatureScaleMath.Mix(0x000000, 0xffffff, 0.5f) == 0x808080 && TemperatureScaleMath.Mix(0x102030, 0x405060, 2f) == 0x405060, "two colours mixed channel by channel, never past either");
        C(TemperatureScaleMath.Hex(0x5e87ff) == "5e87ff" && TemperatureScaleMath.Hex(0x0000ff) == "0000ff", "the colour as rich text's six hex digits");
        C(TemperatureScaleMath.Wrap("Indoors 21C", 0xf2f2f2) == "<color=#f2f2f2>Indoors 21C</color>" && TemperatureScaleMath.Wrap("", 0xf2f2f2) == "",
            "the game's words wrapped in their colour");

        // ---------- The seams ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string patch = File.ReadAllText(Path.Combine(src, "Hud", "Patch_TemperatureReadout.cs"));
        string scale = File.ReadAllText(Path.Combine(src, "Hud", "TemperatureScale.cs"));
        string vitals = File.ReadAllText(Path.Combine(src, "Hud", "HudVitals.cs"));
        string kit = File.ReadAllText(Path.Combine(src, "Hud", "HudKit.cs"));
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs"));
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Panels.xml"));
        C(patch.Contains("[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]")
          && patch.Contains("AccessTools.Method(typeof(GlobalControls), \"TemperatureString\")")
          && patch.Contains("AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })") && patch.Contains("list[j].operand = ours;"),
            "the game's own label for its temperature line, right after its words, is sent through Rimfolk's");
        C(patch.Contains("TemperatureScale.Rgb((float)shown.GetValue(null))") && patch.Contains("\"cachedTemperatureStringForTemperature\""),
            "the readout's colour is the scale's, for the very temperature the game's words were made from");
        C(patch.Contains("TemperatureScale.ColorOf(rgb)") && patch.Contains("TemperatureScaleMath.EdgeAlpha") && patch.Contains("finally") && patch.Contains("GUI.color = was;"),
            "drawn in that colour over a dark edge, and the game's colour put back after");
        C(patch.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tempReadoutColour ?? true);") && patch.Contains("Widgets.Label(rect, text);\n                return;"),
            "behind the deeper game hooks and its own setting; off, the game's line as it was");
        C(patch.Contains("[HarmonyPatch(typeof(GlobalControls), \"TemperatureString\")]") && patch.Contains("if (Patch_TemperatureReadout.swapped")
          && patch.Contains("TemperatureScaleMath.Wrap(__result, rgb)"), "should the label not be found, the game's words come back in their colour instead");
        C(patch.Contains("ReferenceEquals(text, lastText)") && patch.Contains("ReferenceEquals(__result, from)"), "worked out again only when the game's words change, nothing made each frame");
        C(patch.Contains("CouncilLog.ErrorOnce(\"the game's temperature in colour\""), "a failure is logged once and the line drawn as the game draws it");
        C(scale.Contains("TemperatureScaleMath.Rgb(celsius, HumanMin, HumanMax)") && scale.Contains("ThingDefOf.Human") && scale.Contains("StatDefOf.ComfyTemperatureMin")
          && scale.Contains("StatDefOf.ComfyTemperatureMax"), "the scale is set around the game's own comfort range for people");
        C(vitals.Contains("line.valueColor = TemperatureScale.Of(t);") && vitals.Contains("HudRank.Band(t, TemperatureScale.HumanMin, TemperatureScale.HumanMax)"),
            "the vitals' outdoor temperature takes the same colours, its tooltip the same range");
        C(!kit.Contains("TemperatureScale") && !kit.Contains("TemperatureColor"), "the vitals' old green-in-the-middle scale is gone");
        C(settings.Contains("public bool tempReadoutColour = true;") && settings.Contains("Scribe_Values.Look(ref tempReadoutColour, \"tempReadoutColour\", true);")
          && catalog.Contains("Toggle(\"tempReadoutColour\", \"CC_Settings_TempReadoutColour\", \"CC_SetNote_TempReadoutColour\", \"deepHooks\");"),
            "the setting, on by default, under what shows on the map, with the deeper game hooks");
        C(keyed.Contains("<CC_Settings_TempReadoutColour>") && keyed.Contains("<CC_Settings_TempReadoutColour_Tip>") && keyed.Contains("<CC_SetNote_TempReadoutColour>"),
            "the setting's words");
        return n;
    }
}
