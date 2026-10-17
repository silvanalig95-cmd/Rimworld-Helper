using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The sides of a person that Rimfolk keeps, each from -100 to 100. The game's traits, skills and age set where a
    /// colonist starts (their nature); what happens to them moves them from there (their marks), slowly and only so far.
    /// </summary>
    public enum PersonaAxis
    {
        /// <summary>Cold and distant (-) to warm and big-hearted (+).</summary>
        Warmth,
        /// <summary>Timid (-) to bold and fearless (+).</summary>
        Nerve,
        /// <summary>Even-tempered (-) to short-fused (+).</summary>
        Temper,
        /// <summary>Dour and serious (-) to playful, the one who makes the jokes (+).</summary>
        Humour,
        /// <summary>Suspicious (-) to trusting (+).</summary>
        Trust,
        /// <summary>Self-effacing (-) to proud (+).</summary>
        Pride,
        /// <summary>Set in their ways (-) to curious, after the new (+).</summary>
        Openness,
        /// <summary>Bleak (-) to hopeful (+).</summary>
        Outlook
    }

    /// <summary>
    /// The arithmetic of a persona, with no game types, so the tests can check it: how nature, lasting marks and fading
    /// drift add up, how drift fades, what a value is called, and which side of a person shows most.
    /// </summary>
    public static class PersonaMath
    {
        public const int Axes = 8;
        public const float Max = 100f;
        /// <summary>How far what happens can move a person, on top of their nature: lasting marks and fading drift each.</summary>
        public const float MaxLasting = 40f, MaxDrift = 50f;
        /// <summary>Drift halves in this many days without anything renewing it: a hard season is felt for a year or so.</summary>
        public const float DriftHalfLifeDays = 240f;
        /// <summary>Where a side starts to show (a tag, a word) and where it's strong.</summary>
        public const float Shows = 30f, Strong = 60f;
        /// <summary>A stance on a topic, from against (-1) to for (+1).</summary>
        public const float StanceMax = 1f;

        public static readonly PersonaAxis[] All =
        {
            PersonaAxis.Warmth, PersonaAxis.Nerve, PersonaAxis.Temper, PersonaAxis.Humour,
            PersonaAxis.Trust, PersonaAxis.Pride, PersonaAxis.Openness, PersonaAxis.Outlook
        };

        public static float Clamp(float v, float max) => v < -max ? -max : v > max ? max : v;

        /// <summary>Nature, lasting marks and fading drift together, kept within -100..100.</summary>
        public static float Total(float nature, float lasting, float drift) =>
            Clamp(nature + Clamp(lasting, MaxLasting) + Clamp(drift, MaxDrift), Max);

        /// <summary>What's left of a drift after <paramref name="days"/> days.</summary>
        public static float Faded(float drift, float days)
        {
            if (days <= 0f || drift == 0f) return drift;
            float left = drift * (float)Math.Pow(0.5, days / DriftHalfLifeDays);
            return Math.Abs(left) < 0.05f ? 0f : left;
        }

        /// <summary>
        /// Adds to a drift or lasting value, more slowly the further it has already gone the same way (someone already
        /// hardened hardens less with each new blow), and never past its cap.
        /// </summary>
        public static float Add(float current, float amount, float cap)
        {
            if (amount == 0f) return current;
            bool sameWay = (current >= 0f) == (amount >= 0f);
            float room = sameWay ? Math.Max(0f, 1f - Math.Abs(current) / cap) : 1f;
            return Clamp(current + amount * (0.35f + 0.65f * room), cap);
        }

        /// <summary>-2 strongly against, -1 leaning against, 0 neither, 1 leaning for, 2 strongly for.</summary>
        public static int Degree(float v) => v <= -Strong ? -2 : v <= -Shows ? -1 : v >= Strong ? 2 : v >= Shows ? 1 : 0;

        /// <summary>The tag a side gives a person once it shows (for lines written for, say, the warm or the hot-tempered).</summary>
        public static string Tag(PersonaAxis axis, float v)
        {
            int d = Degree(v);
            if (d == 0) return null;
            switch (axis)
            {
                case PersonaAxis.Warmth: return d > 0 ? "persona:warm" : "persona:cold";
                case PersonaAxis.Nerve: return d > 0 ? "persona:bold" : "persona:timid";
                case PersonaAxis.Temper: return d > 0 ? "persona:hothead" : "persona:calm";
                case PersonaAxis.Humour: return d > 0 ? "persona:joker" : "persona:serious";
                case PersonaAxis.Trust: return d > 0 ? "persona:trusting" : "persona:wary";
                case PersonaAxis.Pride: return d > 0 ? "persona:proud" : "persona:humble";
                case PersonaAxis.Openness: return d > 0 ? "persona:curious" : "persona:traditional";
                default: return d > 0 ? "persona:hopeful" : "persona:gloomy";
            }
        }

        /// <summary>
        /// The sides that show most, strongest first (at most <paramref name="count"/>, only those past <see cref="Shows"/>).
        /// Ties go to the axis order, so the same person is always described the same way.
        /// </summary>
        public static List<int> Strongest(float[] values, int count)
        {
            var idx = new List<int>();
            for (int i = 0; i < values.Length; i++)
                if (Math.Abs(values[i]) >= Shows) idx.Add(i);
            idx.Sort((x, y) =>
            {
                int c = Math.Abs(values[y]).CompareTo(Math.Abs(values[x]));
                return c != 0 ? c : x.CompareTo(y);
            });
            if (idx.Count > count) idx.RemoveRange(count, idx.Count - count);
            return idx;
        }

        /// <summary>
        /// A small, stable lean on each side for someone the game gave no traits for it, from their ID, so two plain
        /// colonists still differ and each stays themselves (-<paramref name="spread"/>..<paramref name="spread"/>).
        /// </summary>
        public static float Seed(int id, PersonaAxis axis, float spread)
        {
            unchecked
            {
                uint h = (uint)id * 2654435761u ^ ((uint)axis + 1u) * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return ((h % 2001u) / 1000f - 1f) * spread;
            }
        }

        /// <summary>
        /// How alike two people are, 0 (opposites) to 1 (the same), on the sides that matter between people. Warmth,
        /// humour and openness count double: they decide who enjoys whose company.
        /// </summary>
        public static float Likeness(float[] a, float[] b)
        {
            float sum = 0f, weight = 0f;
            for (int i = 0; i < Axes && i < a.Length && i < b.Length; i++)
            {
                float w = i == (int)PersonaAxis.Warmth || i == (int)PersonaAxis.Humour || i == (int)PersonaAxis.Openness ? 2f : 1f;
                sum += w * (1f - Math.Abs(a[i] - b[i]) / (2f * Max));
                weight += w;
            }
            return weight > 0f ? sum / weight : 0.5f;
        }

        /// <summary>A stance moved toward another's: a little, more when the other is trusted and the listener open.</summary>
        public static float Persuaded(float stance, float other, float pull)
        {
            pull = Clamp(pull, 0.5f);
            if (pull < 0f) pull = 0f;
            return Clamp(stance + (other - stance) * pull, StanceMax);
        }

        /// <summary>Order-free key for a pair of people.</summary>
        public static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }
}
