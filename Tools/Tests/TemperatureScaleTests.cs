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

        // ---------- Readable: on Rimfolk's dark panels in both looks, and inside the thermometer's dark back over the map ----------
        // The game's look on the classic panels over a dark map (as HudTemperatureTests measured it), Rimfolk's own look's
        // cards (its panel for the dark look, its window ground for the darker and the clear ones), and black: the dark back
        // round the game's own readout.
        uint classic = 0x181a1c;
        var grounds = new (string name, uint rgb)[] { ("the classic panels", classic), ("the own look's dark card", SkinMath.Panel), ("the own look's darker card", SkinMath.WindowGround), ("the thermometer's dark back", 0x000000) };
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
        C(TemperatureScaleMath.ThermoBackAlpha >= 0.5f && TemperatureScaleMath.ThermoBackAlpha <= 0.8f, "the thermometer's dark back is solid enough to read over snow without looking heavy");

        // ---------- The game's words beside it: near the game's white, a touch of the colour ----------
        bool light = true, touched = true;
        for (float t = -80f; t <= 90f; t += 0.5f)
        {
            uint c = At(t), w = TemperatureScaleMath.TextTint(c);
            if (SkinMath.Luminance(w) < 0.5) light = false;
            if (c != neutral && w == 0xffffff) touched = false;
        }
        C(light, "the words stay light at every temperature, as readable over the map as the game's white");
        C(touched, "and take a touch of the colour wherever the thermometer isn't white");
        C(TemperatureScaleMath.TextTintShare > 0.2f && TemperatureScaleMath.TextTintShare < 0.5f, "a light touch: the thermometer carries the colour");

        // ---------- The field behind the line: the colour, the words dark on it, the thermometer darker ----------
        double worstWords = 99, worstThermo = 99;
        for (float t = -80f; t <= 90f; t += 0.5f)
        {
            uint c = At(t);
            worstWords = Math.Min(worstWords, SkinMath.Contrast(c, TemperatureScaleMath.FieldText(c)));
            worstThermo = Math.Min(worstThermo, SkinMath.Contrast(c, TemperatureScaleMath.FieldThermo(c)));
        }
        C(worstWords >= SkinMath.TextContrast, $"the words read on the field at every temperature (at worst {worstWords:0.0} to 1)");
        C(TemperatureScaleMath.FieldText(TemperatureScaleMath.Neutral) == TemperatureScaleMath.FieldInk && TemperatureScaleMath.FieldText(TemperatureScaleMath.DeepBlue) == TemperatureScaleMath.FieldInk
          && TemperatureScaleMath.FieldText(TemperatureScaleMath.DeepRed) == TemperatureScaleMath.FieldInk && TemperatureScaleMath.FieldText(0x202020) == 0xffffffu,
            "dark words on every colour of the scale, white only on a dark field");
        C(worstThermo >= 2.0, $"the thermometer stands out from a field of its own colour (at worst {worstThermo:0.0} to 1)");
        C(TemperatureScaleMath.FieldAlpha >= 0.75f && TemperatureScaleMath.FieldAlpha <= 1f && TemperatureScaleMath.FieldPadX >= 3f && TemperatureScaleMath.FieldPadX <= 8f,
            "the field is strong enough to read at a glance and only a little wider than the line");

        // ---------- The thermometer: filled to the temperature ----------
        C(TemperatureScaleMath.Level(-30f, lo, hi) == 0f && TemperatureScaleMath.Level(-60f, lo, hi) == 0f && TemperatureScaleMath.Level(50f, lo, hi) == 1f
          && TemperatureScaleMath.Level(80f, lo, hi) == 1f, "empty at killing cold, full at killing heat");
        C(Math.Abs(TemperatureScaleMath.Level(21f, lo, hi) - 51f / 80f) < 0.001f && Math.Abs(TemperatureScaleMath.Level(10f, lo, hi) - 0.5f) < 0.001f,
            "in a straight line between: 10 °C half full, 21 °C about two thirds");
        bool rising = true;
        float lastLevel = -1f;
        for (float t = -80f; t <= 90f; t += 0.5f)
        {
            float l = TemperatureScaleMath.Level(t, lo, hi);
            if (l < lastLevel) rising = false;
            lastLevel = l;
        }
        C(rising, "warmer is never lower");
        C(TemperatureScaleMath.Level(float.NaN, lo, hi) == TemperatureScaleMath.Level(21f, lo, hi) && TemperatureScaleMath.Level(0f, 30f, 10f) == TemperatureScaleMath.Level(0f, 10f, 30f),
            "an unknown temperature at comfortable, a range the wrong way round taken the right way");
        C(TemperatureScaleMath.Shown(0f) == TemperatureScaleMath.ThermoBulbTop && TemperatureScaleMath.Shown(1f) == TemperatureScaleMath.ThermoTubeTop
          && TemperatureScaleMath.Shown(-1f) == TemperatureScaleMath.ThermoBulbTop && TemperatureScaleMath.Shown(2f) == TemperatureScaleMath.ThermoTubeTop
          && TemperatureScaleMath.Shown(0.5f) > TemperatureScaleMath.ThermoBulbTop && TemperatureScaleMath.Shown(0.5f) < TemperatureScaleMath.ThermoTubeTop,
            "the bulb always full, the tube from its foot to its top by the level");
        C(TemperatureScaleMath.ThermoWidth * 2f == TemperatureScaleMath.ThermoHeight && TemperatureScaleMath.ThermoHeight <= 24f, "half as wide as tall, under the game's line's height");

        // ---------- The word ----------
        var feel = new (float t, TemperatureFeel f)[]
        {
            (-45f, TemperatureFeel.KillingCold), (-30f, TemperatureFeel.KillingCold), (-29f, TemperatureFeel.Cold), (5.9f, TemperatureFeel.Cold), (6f, TemperatureFeel.Chilly),
            (15.9f, TemperatureFeel.Chilly), (16f, TemperatureFeel.Comfortable), (26f, TemperatureFeel.Comfortable), (26.1f, TemperatureFeel.Warm), (36f, TemperatureFeel.Warm),
            (36.1f, TemperatureFeel.Hot), (49.9f, TemperatureFeel.Hot), (50f, TemperatureFeel.KillingHeat), (80f, TemperatureFeel.KillingHeat)
        };
        foreach (var (t, f) in feel) C(TemperatureScaleMath.Feel(t, lo, hi) == f, $"{t} °C is {f} (got {TemperatureScaleMath.Feel(t, lo, hi)})");
        C(TemperatureScaleMath.Feel(float.NaN, lo, hi) == TemperatureFeel.Comfortable && TemperatureScaleMath.Feel(20f, float.NaN, float.NaN) == TemperatureFeel.Comfortable,
            "an unknown temperature or range is not a warning");
        bool agrees = true;
        for (float t = -80f; t <= 90f; t += 0.25f)
        {
            var f = TemperatureScaleMath.Feel(t, lo, hi);
            uint c = At(t);
            if ((f == TemperatureFeel.Comfortable) != (c == neutral)) agrees = false;
            if (f == TemperatureFeel.KillingCold && c != TemperatureScaleMath.DeepBlue || f == TemperatureFeel.KillingHeat && c != TemperatureScaleMath.DeepRed) agrees = false;
        }
        C(agrees, "the word agrees with the colour: comfortable is white, killing cold deep blue, killing heat deep red");

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
        string thermo = File.ReadAllText(Path.Combine(src, "Hud", "Thermometer.cs"));
        string lines = File.ReadAllText(Path.Combine(src, "Hud", "HudLines.cs"));
        string linesLook = File.ReadAllText(Path.Combine(src, "Hud", "Look", "HudLinesLook.cs"));
        string script = File.ReadAllText(Path.Combine(root, "Tools", "make_thermometer.py"));
        C(patch.Contains("[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]")
          && patch.Contains("AccessTools.Method(typeof(GlobalControls), \"TemperatureString\")")
          && patch.Contains("AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })") && patch.Contains("list[j].operand = ours;"),
            "the game's own label for its temperature line, right after its words, is sent through Rimfolk's");
        C(patch.Contains("float c = (float)shown.GetValue(null);") && patch.Contains("lastRgb = TemperatureScale.Rgb(c);") && patch.Contains("\"cachedTemperatureStringForTemperature\""),
            "the readout's colour is the scale's, for the very temperature the game's words were made from");
        C(patch.Contains("Thermometer.Draw(at, lastLevel, TemperatureScale.ColorOf(TemperatureScaleMath.FieldThermo(rgb)), was.a);") && patch.Contains("lastLevel = TemperatureScale.Level(c);")
          && patch.Contains("rect.xMax - lastWidth - 3f - w"), "a thermometer, filled to that temperature, just left of the game's words, darker than the field");
        C(patch.Contains("UiKit.Round(field, new Color(fill.r, fill.g, fill.b, TemperatureScaleMath.FieldAlpha * was.a), SkinMath.RadiusSmall);")
          && patch.Contains("rect.xMax - at.x + TemperatureScaleMath.FieldPadX * 2f") && patch.IndexOf("UiKit.Round(field") < patch.IndexOf("Widgets.Label(rect, text);\n                GUI.color = was;\n                Thermometer.Draw"),
            "a field in the temperature's colour behind the thermometer and the words, a little wider than both, drawn under them");
        C(patch.Contains("TemperatureScale.ColorOf(TemperatureScaleMath.FieldText(rgb))") && patch.Contains("finally") && patch.Contains("GUI.color = was;"),
            "the words in the field's own ink, and the game's colour put back after");
        C(patch.Contains("lastTip = TemperatureScale.Tip(c);") && patch.Contains("if (lastTip != null && Mouse.IsOver(tip)) TooltipHandler.TipRegion(tip, lastTip);"),
            "hovering it says the temperature in a word (made when the words change, not each frame)");
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
        foreach (TemperatureFeel f in Enum.GetValues<TemperatureFeel>())
            C(System.Text.RegularExpressions.Regex.IsMatch(keyed, $"<CC_TempFeel_{f}>[a-z ]+</CC_TempFeel_{f}>"), $"the word for {f}, in lower case for the middle of the tooltip");
        C(keyed.Contains("<CC_TempReadout_Tip>{TEMP}: {FEEL}.") && scale.Contains("\"CC_TempFeel_\" + TemperatureScaleMath.Feel(celsius, HumanMin, HumanMax)"),
            "the tooltip: the temperature, its word, the colonists' comfortable range");

        // ---------- The thermometer's pictures and drawing ----------
        foreach (var name in new[] { "Back", "Fill", "Glass" })
            C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Thermometer", name + ".png")), $"its {name} picture is there");
        int Num(string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(script, key + @"\s*=\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }
        var tube = System.Text.RegularExpressions.Regex.Match(script, @"TUBE_X0, TUBE_X1, TUBE_TOP, TUBE_R = (\d+), (\d+), (\d+), (\d+)");
        var bulb = System.Text.RegularExpressions.Regex.Match(script, @"BULB_CX, BULB_CY, BULB_R = (\d+), (\d+), (\d+)");
        int glass = Num("GLASS");
        C(tube.Success && bulb.Success && glass > 0, "the script's shapes read");
        if (tube.Success && bulb.Success && glass > 0)
        {
            int tubeInner = int.Parse(tube.Groups[3].Value) + glass, bulbInner = int.Parse(bulb.Groups[2].Value) - (int.Parse(bulb.Groups[3].Value) - glass);
            C(Math.Abs(TemperatureScaleMath.ThermoTubeTop - (256f - tubeInner) / 256f) < 1e-4 && Math.Abs(TemperatureScaleMath.ThermoBulbTop - (256f - bulbInner) / 256f) < 1e-4,
                $"the fill's lines agree with the pictures (the tube's inside from {tubeInner}, the bulb's from {bulbInner})");
            C(int.Parse(bulb.Groups[2].Value) + int.Parse(bulb.Groups[3].Value) + Num("RIM") <= 256 && int.Parse(tube.Groups[3].Value) - Num("RIM") >= 0
              && int.Parse(tube.Groups[2].Value) - int.Parse(tube.Groups[1].Value) - 2 * glass >= 24, "the dark back fits the picture, and the tube's inside is wide enough to show its colour");
        }
        C(thermo.Contains("GUI.DrawTextureWithTexCoords(part, Fill, new Rect(0f, 0f, 1f, shown))") && thermo.Contains("TemperatureScaleMath.Shown(level)")
          && thermo.Contains("TemperatureScaleMath.ThermoBackAlpha") && thermo.Contains("\"ColonyCouncil/Thermometer/\""),
            "drawn as the dark back, the fill cut off at the level, the glass on top");
        C(lines.Contains("if (l.thermo >= 0f && Thermometer.Ready) Thermometer.Draw(") && linesLook.Contains("if (l.thermo >= 0f && Thermometer.Ready) Thermometer.Draw(")
          && vitals.Contains("line.thermo = TemperatureScale.Level(t);"), "the vitals' outdoor temperature has the same thermometer for its icon, in both looks");
        return n;
    }
}
