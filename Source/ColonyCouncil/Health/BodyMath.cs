using System;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's own body changes about Rimfolk's fixed lines (pure, BodyTests). The temperatures where the cold
    /// and heat ailments, the room nuisances and the "cold" and "hot" tags begin move with how far the colonist's genes,
    /// traits, health and race move their comfortable range (a fox tail's -10 °C, cold immunity's -273 °C: past
    /// <see cref="Never"/> the line is never reached). Someone whose hunger falls at under a tenth of an ordinary adult's
    /// barely eats. A fighter's armour is the better of what they wear and what grows in their skin. And how far each cell
    /// of a map lies from open water, for those who fear it.
    /// </summary>
    public static class BodyMath
    {
        /// <summary>A shift of the comfortable range this large (an immunity) puts the line out of reach.</summary>
        public const float Never = 100f;

        /// <summary>Hunger that falls slower than this share of an ordinary adult's is barely eating.</summary>
        public const float BarelyShare = 0.1f;

        /// <summary>The fixed lines, for an ordinary body (°C).</summary>
        public const float ColdWorkFrom = -10f, ColdWorkTo = 6f, BitterBelow = -10f, SwelterFrom = 32f, SwelterWetFrom = 27f;
        public const float ChillyFrom = 0f, ChillyTo = 12f, ChillyOutside = 5f, StuffyAbove = 29f, DraughtBelow = 5f, ColdDayAt = 0f, HotDayAt = 32f;

        private static bool Reach(float shift) => shift > -Never && shift < Never;

        /// <summary>Working out in the cold (chilblains, chapped hands): from -10 to 6 °C, moved by the cold shift.</summary>
        public static bool ColdWork(float temp, float cold) => Reach(cold) && temp >= ColdWorkFrom + cold && temp <= ColdWorkTo + cold;

        /// <summary>Out in bitter cold (frostnip, nosebleeds): below -10 °C, moved by the cold shift.</summary>
        public static bool BitterCold(float temp, float cold) => Reach(cold) && temp < BitterBelow + cold;

        /// <summary>Sweltering (heat rash, a fume headache): 32 °C, or 27 °C in a wet land, moved by the heat shift.</summary>
        public static bool Sweltering(float temp, bool wet, float heat) =>
            Reach(heat) && (temp >= SwelterFrom + heat || (wet && temp >= SwelterWetFrom + heat));

        /// <summary>A chilly room: 0 to 12 °C inside while it's 5 °C or less outside, moved by the cold shift.</summary>
        public static bool ChillyRoom(float room, float outdoor, float cold) =>
            Reach(cold) && outdoor <= ChillyOutside + cold && room >= ChillyFrom + cold && room < ChillyTo + cold;

        /// <summary>A stifling room: over 29 °C, moved by the heat shift.</summary>
        public static bool StuffyRoom(float room, float heat) => Reach(heat) && room > StuffyAbove + heat;

        /// <summary>A draught at the bed: below 5 °C outside, moved by the cold shift.</summary>
        public static bool Draughty(float outdoor, float cold) => Reach(cold) && outdoor < DraughtBelow + cold;

        /// <summary>A cold day for them (the "cold" tag): 0 °C or less outside, moved by the cold shift.</summary>
        public static bool ColdDay(float outdoor, float cold) => Reach(cold) && outdoor <= ColdDayAt + cold;

        /// <summary>A hot day for them (the "hot" tag): 32 °C or more outside, moved by the heat shift.</summary>
        public static bool HotDay(float outdoor, float heat) => Reach(heat) && outdoor >= HotDayAt + heat;

        /// <summary>Hunger that falls (a tick, fed) at under a tenth of an ordinary adult's: foodless, fed by light or by minerals.</summary>
        public static bool BarelyEats(float fallPerTick, float baseline) => baseline > 0f && fallPerTick >= 0f && fallPerTick < baseline * BarelyShare;

        /// <summary>The armour that counts: the best piece they wear, or what grows in their skin, whichever is more.</summary>
        public static float Armor(float worn, float natural) => Math.Max(Math.Max(0f, worn), Math.Max(0f, natural));

        /// <summary>Tenths of a cell for a step straight and a step on the diagonal.</summary>
        public const int Straight = 10, Diagonal = 14;

        /// <summary>
        /// How far each cell of a <paramref name="w"/> x <paramref name="h"/> grid (index z * w + x) lies from the nearest
        /// water cell, in tenths of a cell, never more than <paramref name="cap"/>: two passes over the grid, steps of 10
        /// straight and 14 on the diagonal (within a twentieth or so of the true distance).
        /// </summary>
        public static int[] WaterDistance(bool[] water, int w, int h, int cap)
        {
            var d = new int[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = water[i] ? 0 : cap;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    int v = d[i];
                    if (v == 0) continue;
                    if (x > 0) v = Math.Min(v, d[i - 1] + Straight);
                    if (z > 0)
                    {
                        v = Math.Min(v, d[i - w] + Straight);
                        if (x > 0) v = Math.Min(v, d[i - w - 1] + Diagonal);
                        if (x < w - 1) v = Math.Min(v, d[i - w + 1] + Diagonal);
                    }
                    d[i] = Math.Min(v, cap);
                }
            for (int z = h - 1; z >= 0; z--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = z * w + x;
                    int v = d[i];
                    if (v == 0) continue;
                    if (x < w - 1) v = Math.Min(v, d[i + 1] + Straight);
                    if (z < h - 1)
                    {
                        v = Math.Min(v, d[i + w] + Straight);
                        if (x < w - 1) v = Math.Min(v, d[i + w + 1] + Diagonal);
                        if (x > 0) v = Math.Min(v, d[i + w - 1] + Diagonal);
                    }
                    d[i] = Math.Min(v, cap);
                }
            return d;
        }

        /// <summary>Within so many cells of water, by a <see cref="WaterDistance"/> grid's tenths.</summary>
        public static bool WithinCells(int tenths, float cells) => tenths <= (int)Math.Round(cells * Straight);
    }
}
