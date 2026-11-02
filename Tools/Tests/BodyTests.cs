using System;
using ColonyCouncil;

/// <summary>
/// What a colonist's own body changes about Rimfolk's fixed lines (Health/BodyMath.cs): the cold and heat lines of the
/// ailments, the room nuisances and the "cold" and "hot" tags moved by their comfortable range (and never reached past an
/// immunity), who barely eats, the armour that counts, the dining row without those who barely eat, and the distance
/// from open water a thalassophobe is kept beyond.
/// </summary>
public static class BodyTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "body: " + what);
        }

        // ---------- An ordinary body keeps the old lines exactly ----------
        for (float t = -40f; t <= 50f; t += 0.5f)
        {
            C(BodyMath.ColdWork(t, 0f) == (t >= -10f && t <= 6f), $"cold work at {t} °C as before");
            C(BodyMath.BitterCold(t, 0f) == (t < -10f), $"bitter cold at {t} °C as before");
            C(BodyMath.Sweltering(t, false, 0f) == (t >= 32f) && BodyMath.Sweltering(t, true, 0f) == (t >= 27f), $"sweltering at {t} °C as before");
            C(BodyMath.StuffyRoom(t, 0f) == (t > 29f), $"stifling room at {t} °C as before");
            C(BodyMath.Draughty(t, 0f) == (t < 5f), $"draught at {t} °C outside as before");
            C(BodyMath.ColdDay(t, 0f) == (t <= 0f) && BodyMath.HotDay(t, 0f) == (t >= 32f), $"cold and hot days at {t} °C as before");
            C(BodyMath.ChillyRoom(t, 0f, 0f) == (t >= 0f && t < 12f) && !BodyMath.ChillyRoom(t, 6f, 0f), $"chilly room at {t} °C as before");
        }

        // ---------- A body that takes the cold or the heat better moves them ----------
        C(BodyMath.ColdWork(-5f, 0f) && !BodyMath.ColdWork(-5f, 10f) && BodyMath.ColdWork(-15f, -10f), "a fox tail (-10) works on at -5 °C and feels it at -15 °C");
        C(!BodyMath.BitterCold(-15f, -10f) && BodyMath.BitterCold(-25f, -10f), "a fox tail's bitter cold starts at -20 °C");
        C(!BodyMath.Sweltering(35f, false, 10f) && BodyMath.Sweltering(43f, false, 10f), "ten degrees more heat: sweltering from 42 °C");
        C(BodyMath.ColdDay(5f, 10f) && !BodyMath.ColdDay(5f, 0f), "a body that feels the cold sooner (+10): a cold day at 5 °C");
        C(BodyMath.ChillyRoom(-5f, 0f, -10f) && !BodyMath.ChillyRoom(5f, 0f, -10f), "a fox tail's chilly room is -10 to 2 °C");

        // ---------- An immunity never reaches the line ----------
        for (float t = -270f; t <= 60f; t += 5f)
        {
            C(!BodyMath.ColdWork(t, -273f) && !BodyMath.BitterCold(t, -273f) && !BodyMath.ChillyRoom(t, t, -273f), $"cold immunity: no cold ailment or chill at {t} °C");
            C(!BodyMath.Draughty(t, -273f) && !BodyMath.ColdDay(t, -273f), $"cold immunity: no draught or cold day at {t} °C");
        }
        for (float t = -20f; t <= 200f; t += 5f)
            C(!BodyMath.Sweltering(t, true, 232f) && !BodyMath.StuffyRoom(t, 232f) && !BodyMath.HotDay(t, 232f), $"heat immunity (efreet): no heat at {t} °C");
        C(!BodyMath.ColdWork(0f, BodyMath.Never) && !BodyMath.ColdWork(0f, -BodyMath.Never), "a shift of a hundred degrees either way is out of reach");

        // ---------- Who barely eats ----------
        const float baseline = 2.6666667E-05f;
        C(!BodyMath.BarelyEats(baseline, baseline) && !BodyMath.BarelyEats(baseline * 0.5f, baseline) && !BodyMath.BarelyEats(baseline * 0.1f, baseline),
            "an ordinary adult, a child, a tenth of an adult's hunger: they eat");
        C(BodyMath.BarelyEats(baseline * 0.0001f, baseline) && BodyMath.BarelyEats(baseline * 0.05f, baseline), "foodless (a ten-thousandth), fed by light or minerals: barely eats");
        C(BodyMath.BarelyEats(0f, baseline) && !BodyMath.BarelyEats(0f, 0f) && !BodyMath.BarelyEats(-1f, baseline), "nothing at all counts; no baseline or a broken rate doesn't");
        C(!BodyMath.BarelyEats(baseline * 5f, baseline), "a mineral absorber out of minerals, hungry five times over: eats");

        // ---------- The armour that counts ----------
        C(BodyMath.Armor(0f, 0f) == 0f && BodyMath.Armor(0.4f, 0f) == 0.4f, "nothing, or worn armour alone");
        C(BodyMath.Armor(0f, 0.4f) == 0.4f && BodyMath.Armor(0.1f, 0.5f) == 0.5f, "plasteel skin, fungal skin over a shirt: the skin");
        C(BodyMath.Armor(1.1f, 0.2f) == 1.1f && BodyMath.Armor(-1f, -1f) == 0f, "marine armour over bark skin: the armour; never below nothing");

        // ---------- The table: those who barely eat need no seat ----------
        var x = new GrowthInput { colonists = 6, seats = 5 };
        C(GrowthMath.Eaters(x) == 6 && GrowthMath.DiningRow(x).Short == 0, "six eaters and five seats: enough (one is always out)");
        x.seats = 3;
        C(GrowthMath.DiningRow(x).Short == 2, "six eaters and three seats: two short");
        x.fasting = 2;
        C(GrowthMath.Eaters(x) == 4 && GrowthMath.DiningRow(x).Short == 0, "two of the six barely eat: three seats do");
        x = new GrowthInput { colonists = 2, slaves = 1, guests = 1, fasting = 3 };
        C(GrowthMath.Eaters(x) == 1 && GrowthMath.DiningRow(x).need == 0, "one left at the table: no row at all");
        x.fasting = 9;
        C(GrowthMath.Eaters(x) == 0, "never fewer than none at the table");

        // ---------- Open water, as far as a thalassophobe minds it ----------
        int w = 50, h = 40;
        int cap = 20 * BodyMath.Straight + BodyMath.Diagonal;
        var rng = new Random(7);
        for (int round = 0; round < 6; round++)
        {
            var water = new bool[w * h];
            int pools = 1 + round;
            for (int k = 0; k < pools * 6; k++) water[rng.Next(w * h)] = true;
            // A river along one edge in some rounds.
            if (round % 2 == 1)
                for (int z = 0; z < h; z++) water[z * w + 2] = true;
            var d = BodyMath.WaterDistance(water, w, h, cap);
            for (int z = 0; z < h; z++)
                for (int xx = 0; xx < w; xx++)
                {
                    double best = double.MaxValue;
                    for (int i = 0; i < water.Length; i++)
                    {
                        if (!water[i]) continue;
                        int dx = i % w - xx, dz = i / w - z;
                        best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
                    }
                    int got = d[z * w + xx];
                    C(got >= 0 && got <= cap, $"round {round}: ({xx},{z}) within the cap");
                    if (water[z * w + xx]) C(got == 0, $"round {round}: water is no way from water");
                    if (best <= 20.0)
                        C(Math.Abs(got / 10.0 - best) <= best * 0.09 + 0.01, $"round {round}: ({xx},{z}) {got / 10.0} cells against the true {best:0.00}");
                    // The 20-cell line: never wrong by more than a cell and a half either way.
                    if (best <= 18.5) C(BodyMath.WithinCells(got, 20f), $"round {round}: ({xx},{z}) at {best:0.0} cells is near water");
                    if (best >= 21.5) C(!BodyMath.WithinCells(got, 20f), $"round {round}: ({xx},{z}) at {best:0.0} cells is clear of it");
                }
        }
        var dry = BodyMath.WaterDistance(new bool[w * h], w, h, cap);
        C(Array.TrueForAll(dry, v => v == cap) && !BodyMath.WithinCells(cap, 20f), "a map without water: never near it");
        var one = new bool[9];
        one[4] = true;
        var tiny = BodyMath.WaterDistance(one, 3, 3, cap);
        C(tiny[4] == 0 && tiny[1] == 10 && tiny[0] == 14 && tiny[8] == 14, "one pool: a step straight is 10, on the diagonal 14");
        return n;
    }
}
