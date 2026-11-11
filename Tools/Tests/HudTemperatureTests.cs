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

        // ---------- The colours: the one temperature scale (TemperatureScaleTests), the same as the game's readout ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string kit = File.ReadAllText(Path.Combine(src, "Hud", "HudKit.cs"));
        C(!kit.Contains("TemperatureScale =") && !kit.Contains("TemperatureColor("), "the panels keep no scale of their own: the value takes the temperature scale's colour");

        // ---------- On the panel ----------
        string vitals = File.ReadAllText(Path.Combine(src, "Hud", "HudVitals.cs"));
        string lines = File.ReadAllText(Path.Combine(src, "Hud", "HudLines.cs"));
        C(vitals.Contains("var band = HudRank.Band(t, TemperatureScale.HumanMin, TemperatureScale.HumanMax);") && vitals.Contains("line.valueColor = TemperatureScale.Of(t);"),
            "the value takes the temperature scale's colour, the tooltip its band's word");
        C(vitals.Contains("HudRank.Temperature(t)"), "the row still turns orange and red as before");
        string scaleSrc = File.ReadAllText(Path.Combine(src, "Hud", "TemperatureScale.cs"));
        C(scaleSrc.Contains("ThingDefOf.Human") && scaleSrc.Contains("StatDefOf.ComfyTemperatureMin") && scaleSrc.Contains("StatDefOf.ComfyTemperatureMax"),
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
