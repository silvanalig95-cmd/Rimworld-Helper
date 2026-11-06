using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of small letdowns and small joys (Moods/SmallMoments.cs, phase 3 of the review), free of game types
    /// so Tools/Tests and Tools/Balance use it as it is. A small thing that goes wrong or right is felt as a short, small
    /// mood at the moment it happens; what it would have weighed beyond that moment goes to the heart (Heart/), strain for
    /// a letdown and relief for a joy, so a run of them wears or eases the heart while a single one passes. Every small
    /// mood on a colonist's mind, whoever made it (the game, another mod, Rimfolk), also counts toward the heart once they
    /// pile up, by its size and how long it lasts.
    /// </summary>
    public static class SmallMomentMath
    {
        // ---------------- The moment ----------------

        /// <summary>A small letdown or joy is felt for half a day at most (less if it was written shorter).</summary>
        public const float MomentDays = 0.5f;

        /// <summary>And at most this many points, however big it was written.</summary>
        public const float MomentMost = 3f;

        /// <summary>The moment's mood: as felt, but never more than <see cref="MomentMost"/> either way.</summary>
        public static float MomentMood(float felt)
        {
            if (float.IsNaN(felt) || felt == 0f) return 0f;
            float a = Math.Min(Math.Abs(felt), MomentMost);
            return felt < 0f ? -a : a;
        }

        /// <summary>How long the moment lasts, in days: the written time, at most <see cref="MomentDays"/> (half a day for a lasting one).</summary>
        public static float MomentLength(float writtenDays) => writtenDays > 0f && writtenDays < MomentDays ? writtenDays : MomentDays;

        /// <summary>
        /// What the letdown or joy would have weighed beyond its moment, in mood-days (its size times its days, less the
        /// moment's): the part that goes to the heart. Never below nothing.
        /// </summary>
        public static float Moved(float felt, float writtenDays)
        {
            if (float.IsNaN(felt) || float.IsNaN(writtenDays) || writtenDays <= 0f) return 0f;
            float whole = Math.Abs(felt) * writtenDays;
            float moment = Math.Abs(MomentMood(felt)) * MomentLength(writtenDays);
            return Math.Max(0f, whole - moment);
        }

        /// <summary>Strain for each mood-day a letdown moves to the heart.</summary>
        public const float StrainPerMoodDay = 0.8f;

        /// <summary>Relief for each mood-day a joy moves to the heart.</summary>
        public const float ReliefPerMoodDay = 0.6f;

        /// <summary>Strain from a letdown that came as a moment (before the heart's caps and their nature).</summary>
        public static float MomentStrain(float felt, float writtenDays) => Moved(felt, writtenDays) * StrainPerMoodDay;

        /// <summary>Relief from a joy that came as a moment (before the heart's daily cap for joys).</summary>
        public static float MomentRelief(float felt, float writtenDays) => Moved(felt, writtenDays) * ReliefPerMoodDay;

        /// <summary>
        /// A lasting one (the same meal again, a varied table) while it holds, per hour: its size as mood-days an hour, at
        /// the same rate as a moment's, for strain (a letdown) or relief (a joy).
        /// </summary>
        public static float LastingPerHour(float felt, bool joy)
        {
            if (float.IsNaN(felt)) return 0f;
            return Math.Abs(felt) / 24f * (joy ? ReliefPerMoodDay : StrainPerMoodDay);
        }

        /// <summary>A lasting one comes back as a moment this often while it holds (and whenever it gets worse).</summary>
        public const float AgainDays = 2f;

        /// <summary>
        /// Whether a lasting one is felt as a moment now: it has just begun (no stage before, -1), got worse (a higher
        /// stage), or held for <see cref="AgainDays"/> since its last moment.
        /// </summary>
        public static bool MomentDue(int stage, int lastStage, float daysSinceMoment) =>
            lastStage < 0 || stage > lastStage || float.IsNaN(daysSinceMoment) || daysSinceMoment >= AgainDays;

        // ---------------- Every small mood, anyone's ----------------

        /// <summary>A mood counts as small up to this many points (a group of them, as the game adds it up).</summary>
        public const float SmallMost = 6f;

        /// <summary>A memory counts as small if it lasts this many days or less; a lasting mood (a room, a need) counts while it holds.</summary>
        public const float SmallDays = 3f;

        /// <summary>
        /// Whether a mood is a small one the heart counts toward the pile: at most <see cref="SmallMost"/> either way,
        /// and a memory of <see cref="SmallDays"/> or less (<paramref name="durationDays"/> below zero: a lasting mood).
        /// The heavy kinds (grief, fear, pain, bad nights, hunger) are left to their own sources before this is asked.
        /// </summary>
        public static bool IsSmall(float offset, float durationDays)
        {
            if (float.IsNaN(offset) || offset == 0f || Math.Abs(offset) > SmallMost) return false;
            return durationDays < 0f || durationDays <= SmallDays;
        }

        /// <summary>Small bad moods up to this many points in all are just life; past it, they wear on the heart.</summary>
        public const float PileFrom = 6f;

        /// <summary>Small good moods past this many points in all ease the heart.</summary>
        public const float JoyPileFrom = 10f;

        /// <summary>Strain an hour for each point of small bad moods past <see cref="PileFrom"/>.</summary>
        public const float PileStrainPerPoint = 0.03f;

        /// <summary>Relief an hour for each point of small good moods past <see cref="JoyPileFrom"/>.</summary>
        public const float JoyReliefPerPoint = 0.02f;

        /// <summary>Strain an hour from the small bad moods on their mind (the sum, as positive points).</summary>
        public static float PileStrainPerHour(float smallBad)
        {
            if (float.IsNaN(smallBad) || smallBad <= PileFrom) return 0f;
            return (smallBad - PileFrom) * PileStrainPerPoint;
        }

        /// <summary>Relief an hour from the small good moods on their mind.</summary>
        public static float JoyReliefPerHour(float smallGood)
        {
            if (float.IsNaN(smallGood) || smallGood <= JoyPileFrom) return 0f;
            return (smallGood - JoyPileFrom) * JoyReliefPerPoint;
        }

        /// <summary>The most small joys may ease in a day, all together (the heart's cap for them).</summary>
        public const float JoyReliefCap = 6f;

        /// <summary>Relief from small joys within the day's cap: what's left of it, never more.</summary>
        public static float WithinJoyCap(float amount, float todaySoFar)
        {
            if (float.IsNaN(amount) || amount <= 0f) return 0f;
            float room = Math.Max(0f, JoyReliefCap - Math.Max(0f, todaySoFar));
            return Math.Min(amount, room);
        }
    }
}
