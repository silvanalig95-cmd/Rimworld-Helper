using System.Globalization;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The vitals' outdoor temperature in colour (the player's request, 7 November 2026: "could the temperature counter also
/// be color coded so i quickly see how the temperature is overall"): the band rule (HudRank.Band), its agreement with the
/// row's orange and red, the scale's colours, the tooltip's words.
/// </summary>
public static class HudTemperatureTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the outdoor temperature's colour: " + what);
        }

        const float lo = HudRank.HumanComfyMin, hi = HudRank.HumanComfyMax;
        TemperatureBand B(float t) => HudRank.Band(t, lo, hi);

        // ---------- The bands around the human comfort range (16 to 26 °C) ----------
        var expect = new (float t, TemperatureBand band)[]
        {
            (-45f, TemperatureBand.DeadlyCold), (-30f, TemperatureBand.DeadlyCold), (-29.9f, TemperatureBand.Freezing), (-15f, TemperatureBand.Freezing),
            (-14.9f, TemperatureBand.Cold), (-0.1f, TemperatureBand.Cold), (0f, TemperatureBand.Chilly), (5.9f, TemperatureBand.Chilly), (6f, TemperatureBand.Cool),
            (15.9f, TemperatureBand.Cool), (16f, TemperatureBand.Comfortable), (21f, TemperatureBand.Comfortable), (26f, TemperatureBand.Comfortable),
            (26.1f, TemperatureBand.Warm), (36f, TemperatureBand.Warm), (36.1f, TemperatureBand.Hot), (39.9f, TemperatureBand.Hot), (40f, TemperatureBand.VeryHot),
            (49.9f, TemperatureBand.VeryHot), (50f, TemperatureBand.DeadlyHeat), (70f, TemperatureBand.DeadlyHeat)
        };
        foreach (var (t, band) in expect) C(B(t) == band, $"{t} °C is {band} (got {B(t)})");
        C(lo == 16f && hi == 26f, "the base game's human comfort range, 16 to 26 °C, when the game can't be asked");
        C(HudRank.SafeMargin == 10f, "chilly and hot start ten degrees outside the comfort range, where the game's safe range ends");

        // ---------- In order, every band reachable, agreeing with the row's colour, for any comfort range ----------
        var ranges = new (float lo, float hi)[] { (lo, hi), (10f, 30f), (-20f, 5f), (25f, 45f), (-50f, 100f), (30f, 10f), (16f, 16f), (float.NaN, 26f), (80f, 90f) };
        foreach (var (rlo, rhi) in ranges)
        {
            var last = TemperatureBand.DeadlyCold;
            bool ordered = true, agrees = true;
            for (float t = -80f; t <= 80f; t += 0.25f)
            {
                var b = HudRank.Band(t, rlo, rhi);
                if (b < last) ordered = false;
                last = b;
                var level = HudRank.LevelOf(HudRank.Temperature(t));
                bool red = b == TemperatureBand.DeadlyCold || b == TemperatureBand.DeadlyHeat;
                bool orange = b == TemperatureBand.Freezing || b == TemperatureBand.VeryHot;
                if ((level == HudLevel.Life) != red || (level == HudLevel.Soon) != orange) agrees = false;
            }
            C(ordered, $"colder is never a warmer band (comfort {rlo} to {rhi})");
            C(agrees, $"deadly cold and heat are the red row, freezing and very hot the orange one (comfort {rlo} to {rhi})");
        }
        var seen = new HashSet<TemperatureBand>();
        for (float t = -80f; t <= 80f; t += 0.25f) seen.Add(B(t));
        C(seen.Count == Enum.GetValues<TemperatureBand>().Length, "every band comes somewhere on the human scale");
        C(HudRank.Band(float.NaN, lo, hi) == TemperatureBand.Comfortable, "an unknown temperature is not coloured as a danger");
        C(HudRank.Band(20f, float.NaN, float.NaN) == TemperatureBand.Comfortable, "an unknown comfort range is the human one");

        // ---------- The colours ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string kit = File.ReadAllText(Path.Combine(src, "Hud", "HudKit.cs"));
        var scale = Regex.Match(kit, @"TemperatureScale =\s*\{(?<body>[^}]*)\}");
        C(scale.Success, "the scale is one list in HudKit");
        var entries = scale.Success ? Regex.Split(scale.Groups["body"].Value.Trim(), @",\s*(?=new Color|UiKit\.)") : Array.Empty<string>();
        C(entries.Length == Enum.GetValues<TemperatureBand>().Length, $"a colour for each band ({entries.Length})");
        if (entries.Length == 10)
        {
            C(entries[5].Trim() == "UiKit.Warm" && entries[8].Trim() == "UiKit.Soon" && entries[9].Trim() == "UiKit.Sour",
                "comfortable is the interface's green, very hot and deadly heat the orange and red the row itself turns");
            // The dark look (HudKit.Background) over a dark map, as text on it: at least 4.5 to 1.
            double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            double Lum(double[] c) => 0.2126 * Lin(c[0]) + 0.7152 * Lin(c[1]) + 0.0722 * Lin(c[2]);
            var panel = new[] { 0.1 * 0.74 + 0.08 * 0.26, 0.11 * 0.74 + 0.08 * 0.26, 0.12 * 0.74 + 0.08 * 0.26 };
            var rgb = new List<double[]>();
            for (int i = 0; i < 10; i++)
            {
                var m = Regex.Match(entries[i], @"new Color\((?<r>[\d.]+)f, (?<g>[\d.]+)f, (?<b>[\d.]+)f\)");
                if (!m.Success) continue;
                var c = new[] { double.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture),
                    double.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture) };
                double ratio = (Lum(c) + 0.05) / (Lum(panel) + 0.05);
                C(ratio >= 4.5, $"{(TemperatureBand)i} reads on the dark look over a dark map ({ratio:0.0} to 1)");
                rgb.Add(c);
            }
            C(rgb.Count == 7, "seven colours of the scale's own, three the interface's");
            if (rgb.Count == 7)
            {
                C(rgb.Take(4).All(c => c[2] > c[0] + 0.4), "the four coldest are blues and cyan (much more blue than red)");
                C(rgb[0][1] < rgb[1][1] && rgb[1][1] < rgb[2][1] && rgb[2][1] < rgb[3][1], "from deep blue to cyan, lighter each step");
                C(rgb[4][1] > rgb[4][0] + 0.4 && rgb[4][2] > rgb[4][0], "cool is a blue-green, between the cold and the green");
                C(rgb[5][0] > 0.75 && rgb[5][1] > 0.75 && rgb[5][2] < 0.5 && rgb[6][0] > rgb[6][1] && rgb[6][2] < 0.3, "warm a yellow-green, hot a yellow");
            }
        }

        // ---------- On the panel ----------
        string vitals = File.ReadAllText(Path.Combine(src, "Hud", "HudVitals.cs"));
        string lines = File.ReadAllText(Path.Combine(src, "Hud", "HudLines.cs"));
        C(vitals.Contains("var band = HudRank.Band(t, humanMin, humanMax);") && vitals.Contains("line.valueColor = HudKit.TemperatureColor(band);"),
            "the value takes its band's colour");
        C(vitals.Contains("HudRank.Temperature(t)"), "the row still turns orange and red as before");
        C(vitals.Contains("ThingDefOf.Human") && vitals.Contains("StatDefOf.ComfyTemperatureMin") && vitals.Contains("StatDefOf.ComfyTemperatureMax"),
            "the scale is set around the game's own human comfort range");
        C(vitals.Contains("p.ComfortableTemperatureRange()") && vitals.Contains("FreeColonistsSpawned"), "the tooltip asks the game how comfortable each colonist is, dressed as they are");
        C(vitals.Contains("t.ToStringTemperature(\"F0\")"), "the value in the player's own unit");
        C(vitals.Contains("map != seasonMap"), "the next season is the map's own, also right after moving to another home");
        C(lines.Contains("l.ValueColor, main, TextAnchor.MiddleRight") && lines.Contains("l.value != null ? l.ValueColor"), "the value's colour in the full rows and the compact ones");

        // ---------- The words ----------
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Panels.xml"));
        foreach (TemperatureBand b in Enum.GetValues<TemperatureBand>())
            C(Regex.IsMatch(keyed, $"<CC_Pnl_TempBand_{b}>[a-z ]+</CC_Pnl_TempBand_{b}>"), $"the band {b} has a word, in lower case for the middle of the tooltip's line");
        C(keyed.Contains("<CC_Pnl_TempTip>Outdoors now: {0}, {1}.</CC_Pnl_TempTip>"), "the tooltip names the band beside the temperature");
        foreach (var k in new[] { "CC_Pnl_TempComfy", "CC_Pnl_TempColdAll", "CC_Pnl_TempColdSome", "CC_Pnl_TempHotAll", "CC_Pnl_TempHotSome" })
            C(Regex.IsMatch(keyed, $"<{k}>[^<]*dressed as they are\\.</{k}>"), $"{k} says it is for the colonists as they are dressed");
        return n;
    }
}
