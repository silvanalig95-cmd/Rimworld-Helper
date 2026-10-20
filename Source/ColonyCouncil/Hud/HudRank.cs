using System;

namespace ColonyCouncil
{
    /// <summary>How much a line on the on-screen panels matters, from its score (see <see cref="HudRank"/>).</summary>
    public enum HudLevel
    {
        /// <summary>Nothing to say: kept off the panels unless asked for.</summary>
        Quiet = 0,
        /// <summary>Merely worth knowing: shown plainly, one hover or click away when space is short.</summary>
        Worth = 1,
        /// <summary>Will soon be a problem: orange.</summary>
        Soon = 2,
        /// <summary>Life and limb, or a break now: red, and never cut for space.</summary>
        Life = 3
    }

    /// <summary>
    /// One ranking for everything the on-screen panels and the hover card show, so they agree with each other: urgency
    /// times impact, in three tiers that never overlap. Life and limb first (bleeding, a dangerous condition, starving,
    /// a mental break, right at the break line), then what will soon be a problem (falling towards a break, food running
    /// out, a wish about to lapse), then what's merely worth knowing (the moods dragging someone down, by size). Within a
    /// tier the score says how urgent: a colonist bleeding out in an hour outranks one with a day left. Pure (no game
    /// types): Tools/Tests checks the order.
    /// </summary>
    public static class HudRank
    {
        // Each tier's floor; a tier spans the hundred above it.
        public const float LifeFloor = 300f;
        public const float SoonFloor = 200f;
        public const float WorthFloor = 100f;

        /// <summary>The mood lines a colonist is "right at" when within this much above it: the same as under it.</summary>
        public const float AtTheLine = 0.02f;

        public static HudLevel LevelOf(float score) =>
            score >= LifeFloor ? HudLevel.Life : score >= SoonFloor ? HudLevel.Soon : score >= WorthFloor ? HudLevel.Worth : HudLevel.Quiet;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary><paramref name="span"/> points above <paramref name="floor"/>, by how far <paramref name="t"/> (0..1) has gone.</summary>
        private static float Within(float floor, float span, float t) => floor + span * Clamp01(t);

        // ---------------- Life and limb (300 and up) ----------------

        /// <summary>Bleeding: 380 with a day or more left, up to 399 when death is an hour or two away.</summary>
        public static float Bleeding(float hoursToDeath) => Within(380f, 19f, 1f - (hoursToDeath - 2f) / 22f);

        /// <summary>A condition that can kill (a life-threatening stage, not yet immune): 370 to 379 by how far it has gone (0..1).</summary>
        public static float Dangerous(float progress) => Within(370f, 9f, progress);

        /// <summary>Starving: 360 to 369 by how far the malnutrition has gone (0..1).</summary>
        public static float Starving(float malnutrition) => Within(360f, 9f, malnutrition);

        /// <summary>In a mental break now.</summary>
        public const float InBreak = 355f;

        /// <summary>
        /// Where a colonist's mood stands against their own break lines (0..1 each). In a break, below a line or right at
        /// the minor one: life (320 to 349). Heading below the minor line, or within <paramref name="margin"/> above it:
        /// soon (220 to 289). Otherwise a quiet score that still sorts the board: the closer to the line, the higher.
        /// </summary>
        public static float MoodRisk(float now, float heading, float minor, float major, float extreme, bool inBreak, float margin)
        {
            if (inBreak) return InBreak;
            if (now < extreme) return 349f;
            if (now < major) return Within(340f, 8f, (major - now) / Math.Max(0.01f, major - extreme));
            if (now < minor) return Within(325f, 14f, (minor - now) / Math.Max(0.01f, minor - major));
            float above = now - minor;
            if (above < AtTheLine) return Within(320f, 4f, 1f - above / AtTheLine);
            // Falling towards a break: where the mood is heading is under the line.
            if (heading < minor) return Within(260f, 29f, (minor - heading) / Math.Max(0.01f, minor) * 2f + (1f - above / 0.3f) * 0.5f);
            if (above < margin) return Within(220f, 39f, 1f - above / Math.Max(0.001f, margin));
            // Comfortable: below the worth tier, ordered by how close to the line (and slipping counts a little).
            float slipping = heading < now ? 0.1f : 0f;
            return 90f * Clamp01(1f - above / 0.65f + slipping);
        }

        // ---------------- Soon (200 to 299) ----------------

        /// <summary>Downed (and not bleeding): someone has to fetch them.</summary>
        public const float Downed = 285f;
        /// <summary>Strain breaking: an episode is near.</summary>
        public const float StrainBreaking = 250f;
        /// <summary>A wish at its last call, or with under a quarter of its time left.</summary>
        public const float WishLapsing = 245f;
        /// <summary>Urgently hungry.</summary>
        public const float Hungry = 240f;
        /// <summary>Exhausted.</summary>
        public const float Exhausted = 235f;
        /// <summary>Strain worn: worth easing soon.</summary>
        public const float StrainWorn = 215f;

        // ---------------- Worth knowing (100 to 199) ----------------

        /// <summary>A wish waiting more than half its time.</summary>
        public const float WishWaiting = 140f;
        /// <summary>Faith wavering.</summary>
        public const float FaithWavering = 130f;
        /// <summary>Strained (the first band of strain).</summary>
        public const float Strained = 120f;

        /// <summary>
        /// A mood dragging someone down, by its size (a positive number of mood points). One that lasts while its cause
        /// does, or that acting on helps, counts in full; one passing on its own counts for 0.4 of its size, so "-8 no
        /// bedroom" comes before "-2 ate raw food" and before a fading "-6 saw a corpse".
        /// </summary>
        public static float Drag(float size, bool fixable)
        {
            if (size <= 0f) return 0f;
            float weight = fixable ? 1f : 0.4f;
            return Math.Min(199f, WorthFloor + size * 3f * weight);
        }

        // ---------------- The colony's vitals ----------------

        /// <summary>Days of food: life (red) below <paramref name="bad"/>, soon (orange) below <paramref name="warn"/>.</summary>
        public static float Food(float days, float warn, float bad)
        {
            if (days < bad) return Within(300f, 49f, 1f - days / Math.Max(0.1f, bad));
            if (days < warn) return Within(200f, 49f, 1f - (days - bad) / Math.Max(0.1f, warn - bad));
            return 10f;
        }

        /// <summary>
        /// Medicine against the colony's size (the advisor's line is two for each colonist): none at all is red when
        /// someone needs tending and orange otherwise; under the line is orange.
        /// </summary>
        public static float Medicine(int count, int colonists, bool someoneNeedsTending, int perColonist)
        {
            if (colonists <= 0) return 0f;
            if (count <= 0) return someoneNeedsTending ? 330f : 230f;
            if (count < colonists * perColonist) return Within(200f, 25f, 1f - count / (float)(colonists * perColonist));
            return 10f;
        }

        /// <summary>
        /// Power: a blackout (more used than made, nothing stored) is red; running on batteries that last under a day is
        /// orange. Watts and watt-days, as the game counts them.
        /// </summary>
        public static float Power(float netWatts, float storedWattDays)
        {
            if (netWatts >= 0f) return 10f;
            if (storedWattDays <= 1f) return 320f;
            float daysLeft = storedWattDays / (-netWatts);
            return daysLeft < 1f ? Within(200f, 40f, 1f - daysLeft) : 20f;
        }

        /// <summary>The outdoor temperature: hard frost or fierce heat is orange, deadly cold or heat red (Celsius).</summary>
        public static float Temperature(float celsius)
        {
            if (celsius <= -30f || celsius >= 50f) return 310f;
            if (celsius <= -15f || celsius >= 40f) return 210f;
            return 10f;
        }

        /// <summary>No research project chosen while there's something to research: worth knowing.</summary>
        public static float Research(bool hasProject, bool anyAvailable) => !hasProject && anyAvailable ? 150f : 10f;

        // ---------------- The council ----------------

        /// <summary>
        /// A wish by how much of its time is left (0..1) and its stage (2 = last call): lapsing soon, waiting long, or
        /// just made.
        /// </summary>
        public static float Wish(float fractionLeft, int stage)
        {
            if (stage >= 2 || fractionLeft < 0.25f) return Within(WishLapsing, 14f, 1f - fractionLeft * 4f);
            if (fractionLeft < 0.5f) return WishWaiting;
            return 110f;
        }

        /// <summary>A letter waiting for an answer: soon when it will go unanswered within a day, worth knowing otherwise.</summary>
        public static float Question(float hoursLeft) => hoursLeft >= 0f && hoursLeft < 24f ? Within(230f, 40f, 1f - hoursLeft / 24f) : 160f;
    }
}
