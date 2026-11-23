using System;

namespace ColonyCouncil
{
    /// <summary>The setting "What they go through, on their portraits": off, the large portraits only (the one at the inspect pane and the colonist card's), or every portrait.</summary>
    public enum PortraitEffectsMode
    {
        Off,
        Large,
        Everywhere
    }

    /// <summary>What a person goes through now, as the game has it (read by <see cref="PortraitEffects"/> at most twice a second).</summary>
    public struct PortraitCondition
    {
        /// <summary>Bleeding, the game's rate (the share of their blood lost in a day), and blood loss's severity (0 to 1).</summary>
        public float bleed, bloodLoss;
        /// <summary>Pain (0 to 1) and their health as the game sums it up (1 whole).</summary>
        public float pain, health;
        /// <summary>Their mood and the minor and extreme break thresholds; in a mental break now.</summary>
        public float mood, minor, extreme;
        public bool breaking;
        /// <summary>The worst illness or infection, as its share of the way to death (0 to 1).</summary>
        public float sick;
        /// <summary>Hypothermia's, heatstroke's and malnutrition's severity (0 to 1).</summary>
        public float cold, heat, hunger;
        /// <summary>The rest need's level (1 rested).</summary>
        public float rest;
        public bool asleep, unconscious, dead;
        /// <summary>Alcohol's severity (0 to 1), and another drug's high.</summary>
        public float drunk;
        public bool high;

        /// <summary>Nothing at all: whole, rested, at ease.</summary>
        public static PortraitCondition Calm => new PortraitCondition { health = 1f, mood = 0.6f, minor = 0.35f, extreme = 0.05f, rest = 1f };
    }

    /// <summary>How strong each effect is on a portrait (0 to 1).</summary>
    public struct PortraitEffectLevels
    {
        public float bleed, hurt, stress, sick, cold, heat, tired, high, hunger, dim;
        /// <summary>In a mental break (a harsher tint over the dark edge).</summary>
        public float breaking;

        public bool Any => bleed + hurt + stress + sick + cold + heat + tired + high + hunger + dim + breaking > 0.005f;
    }

    /// <summary>
    /// What a person goes through, shown on their portraits (the player, 23 November 2026: "the image could have a bunch of
    /// also different effects or laid over it or applied to it. For example, if the pawn is bleeding, the image should kind
    /// of have a bloody effect. If they're super stressed, it should have like a stressed out effect. If they are sick or
    /// infected ... Just add more layers and immersion", then "effects sound great, go ahead now! should be toggleable in
    /// case of performance issues"). Pure: the game's side is <see cref="PortraitEffects"/>.
    /// <list type="bullet">
    /// <item>Bleeding: red at the edges, pulsing faster as it bleeds more, by the rate and the blood lost.</item>
    /// <item>Badly hurt or in pain: the colours drained a little and a red rim.</item>
    /// <item>Near a mental break: a dark edge closing in as the mood falls past the minor threshold toward the extreme one, a
    /// slight tremble from halfway; in a break, the whole edge and a harsher dark red over it.</item>
    /// <item>Sick or infected: a sickly yellow-green tint. Cold (hypothermia): frost from the corners. Heatstroke: a warm
    /// haze that shimmers. Exhausted: heavy shadows at the top and the foot. Drunk or high: drifting colours and a slow wobble. Starving
    /// (malnutrition): washed out. Unconscious: dimmed (closed eyes where the picture's expressions are painted).</item>
    /// </list>
    /// The dead show none of it (their picture is grey).
    /// </summary>
    public static class PortraitEffectsMath
    {
        /// <summary>How fast an effect comes and goes: its strength's change in a second.</summary>
        public const float FadePerSecond = 1.5f;

        /// <summary>A round portrait narrower than this shows no effects (too small to read).</summary>
        public const float RoundFrom = 28f;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : float.IsNaN(v) ? 0f : v;

        /// <summary>Each effect's strength for what the person goes through now.</summary>
        public static PortraitEffectLevels Levels(PortraitCondition c)
        {
            var e = new PortraitEffectLevels();
            if (c.dead) return e;
            // Bleeding: from a small cut's trickle (the game's rate about 0.05 a day) to a bad wound's (1.2 and more); blood
            // already lost (from the game's minor stage, 0.15, to its extreme one, 0.6) adds to it.
            float flow = c.bleed > 0.02f ? 0.3f + 0.45f * Clamp01(c.bleed / 1.2f) : 0f;
            float lost = c.bloodLoss > 0.15f ? 0.5f * Clamp01((c.bloodLoss - 0.15f) / 0.45f) : 0f;
            e.bleed = Clamp01(flow + lost);
            // Badly hurt: pain from 0.15 (the game's "little pain" ends there) to 0.75, or health from 80% down to 30%.
            e.hurt = Math.Max(Clamp01((c.pain - 0.15f) / 0.6f), Clamp01((0.8f - c.health) / 0.5f));
            // Near a break: nothing until the mood is a little above the minor threshold, all at the extreme one.
            if (c.breaking)
            {
                e.stress = 1f;
                e.breaking = 1f;
            }
            else
            {
                float from = c.minor + 0.06f, span = from - c.extreme;
                e.stress = span > 0.01f ? 0.85f * Clamp01((from - c.mood) / span) : 0f;
            }
            e.sick = c.sick > 0.001f ? 0.25f + 0.75f * Clamp01(c.sick) : 0f;
            // Hypothermia and heatstroke: from the game's first stage (0.04) to its extreme one (0.62).
            e.cold = Clamp01((c.cold - 0.04f) / 0.58f);
            e.heat = Clamp01((c.heat - 0.04f) / 0.58f);
            // Tired from the game's "tired" (0.28) down to nothing left; never while asleep or out.
            e.tired = c.asleep || c.unconscious ? 0f : Clamp01((0.28f - c.rest) / 0.28f);
            // Drunk from warm (0.1) to hammered (0.7); another drug's high half way.
            e.high = Math.Max(Clamp01(c.drunk / 0.7f), c.high ? 0.5f : 0f);
            // Malnutrition from its first stage on (the game's minor one is 0.2, extreme 0.8).
            e.hunger = Clamp01((c.hunger - 0.05f) / 0.75f);
            e.dim = c.unconscious ? 1f : 0f;
            return e;
        }

        /// <summary>The levels shown moved toward the ones wanted by <paramref name="step"/> (a second's fade times the time passed), so an effect comes and goes softly.</summary>
        public static PortraitEffectLevels Toward(PortraitEffectLevels shown, PortraitEffectLevels want, float step)
        {
            float T(float a, float b) => a < b ? Math.Min(b, a + step) : Math.Max(b, a - step);
            return new PortraitEffectLevels
            {
                bleed = T(shown.bleed, want.bleed), hurt = T(shown.hurt, want.hurt), stress = T(shown.stress, want.stress), sick = T(shown.sick, want.sick),
                cold = T(shown.cold, want.cold), heat = T(shown.heat, want.heat), tired = T(shown.tired, want.tired), high = T(shown.high, want.high),
                hunger = T(shown.hunger, want.hunger), dim = T(shown.dim, want.dim), breaking = T(shown.breaking, want.breaking)
            };
        }

        /// <summary>The bleeding's pulse at <paramref name="clock"/> seconds: between 0.7 and 1, a little under a beat a second, faster as it bleeds more.</summary>
        public static float Pulse(float clock, float bleed) => 0.85f + 0.15f * (float)Math.Sin(clock * 2.0 * Math.PI * (0.8 + 0.6 * Clamp01(bleed)));

        /// <summary>The heat haze's shimmer at <paramref name="clock"/> seconds: between 0.8 and 1.2.</summary>
        public static float Shimmer(float clock) => 1f + 0.2f * (float)Math.Sin(clock * 3.1) * (float)Math.Sin(clock * 1.7 + 0.6);

        /// <summary>How far the drifting colours of a high have gone from the first to the second (0 to 1), slowly back and forth.</summary>
        public static float Drift(float clock, int seed) => 0.5f + 0.5f * (float)Math.Sin(clock * 0.45 + seed % 7);

        /// <summary>
        /// The movement the effects add to a moving portrait: the tremble of someone near a break (from halfway, quick and
        /// small) and the slow wobble of the drunk or high; <paramref name="lean"/> in degrees about the foot,
        /// <paramref name="dx"/> as a share of the portrait's width.
        /// </summary>
        public static void Shake(PortraitEffectLevels e, float clock, int seed, out float lean, out float dx)
        {
            float s = seed % 11;
            float tremble = Clamp01((e.stress - 0.5f) / 0.5f);
            lean = tremble * 0.35f * (float)(Math.Sin(clock * 31.0 + s) + 0.6 * Math.Sin(clock * 47.0 + 2.0 * s));
            dx = tremble * 0.0025f * (float)Math.Sin(clock * 37.0 + s);
            lean += e.high * 1.6f * (float)Math.Sin(clock * 1.1 + s);
            dx += e.high * 0.006f * (float)Math.Sin(clock * 0.7 + s);
        }

        // ---------------- The textures' shapes (each made once) ----------------

        private static float Smooth(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>How far a point of the texture (0 to 1 across and down) is from its middle: 0 there, 1 at the middle of an edge (an oval to the edges of a rectangle, a circle in a round one).</summary>
        public static float FromMiddle(float u, float v)
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            return (float)Math.Sqrt(x * x + y * y);
        }

        /// <summary>A soft dark or red edge: clear in the middle, strongest in the corners (a round one strongest at its rim, nothing outside it).</summary>
        public static float Edge(float u, float v, bool round)
        {
            float d = FromMiddle(u, v);
            if (round) return d > 1f ? 0f : Smooth(0.45f, 1f, d);
            return Smooth(0.5f, 1.25f, d);
        }

        /// <summary>Heavy shadows at the top and the foot, as tired eyes droop (a round one's inside its circle only).</summary>
        public static float Bands(float u, float v, bool round)
        {
            float b = Smooth(0.35f, 1f, Math.Abs(v - 0.5f) * 2f);
            return round && FromMiddle(u, v) > 1f ? 0f : b;
        }

        /// <summary>A narrow rim along the edges (a round one along its circle).</summary>
        public static float Rim(float u, float v, bool round)
        {
            if (round)
            {
                float d = FromMiddle(u, v);
                return d > 1f ? 0f : Smooth(0.8f, 0.98f, d);
            }
            float e = Math.Max(Math.Abs(u - 0.5f), Math.Abs(v - 0.5f)) * 2f;
            return Smooth(0.84f, 1f, e);
        }

        /// <summary>Inside a round portrait's circle (its edge softened by a pixel of a texture <paramref name="size"/> wide); a rectangle's every point.</summary>
        public static float Inside(float u, float v, bool round, int size)
        {
            if (!round) return 1f;
            float d = FromMiddle(u, v), px = 2f / Math.Max(2, size);
            return Clamp01((1f - d) / px + 0.5f);
        }

        /// <summary>
        /// Frost: thick in the corners (a round one's rim, more at its upper left and lower right) and thinning toward the
        /// middle, broken by a crystal pattern (lines of frost from the corners, a fine grain).
        /// </summary>
        public static float Frost(float u, float v, bool round, int seed)
        {
            float reach, ox = 0.5f, oy = 0.5f;
            if (round)
            {
                float d = FromMiddle(u, v);
                if (d > 1f) return 0f;
                float diag = 0.5f + 0.5f * Math.Abs((u - 0.5f) + (v - 0.5f)) * 1.4f;
                reach = Smooth(0.5f, 1f, d) * Clamp01(diag);
            }
            else
            {
                float cu = Math.Min(u, 1f - u), cv = Math.Min(v, 1f - v);
                float corner = (float)Math.Sqrt(cu * cu + cv * cv);
                reach = Smooth(0.55f, 0.05f, corner);
                // The lines grow from the nearest corner.
                ox = u < 0.5f ? 0f : 1f;
                oy = v < 0.5f ? 0f : 1f;
            }
            if (reach <= 0f) return 0f;
            float grain = Noise(u * 23f, v * 23f, seed) * 0.6f + Noise(u * 61f, v * 61f, seed + 7) * 0.4f;
            // Lines of frost: thin bright streaks at angles of their own.
            float a = (float)Math.Atan2(v - oy, u - ox);
            float streak = (float)Math.Pow(Math.Abs(Math.Sin(a * 14.0 + Noise(u * 6f, v * 6f, seed + 3) * 3.0)), 18.0);
            return Clamp01(reach * (0.45f + 0.45f * grain) + reach * reach * 0.5f * streak);
        }

        /// <summary>A smooth value noise, 0 to 1, from a lattice hashed by <paramref name="seed"/>.</summary>
        public static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
