using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic of a good life grown ordinary (Moods/GoodLife.cs) and of the quiet troubles (Setbacks/Troubles.cs),
    /// free of game types so Tools/Tests and Tools/Balance use it as it is.
    /// </summary>
    public static class GoodLifeMath
    {
        // ---------------- Used to the good life ----------------

        /// <summary>The soft line for a modest colonist: good moods up to this many points count in full.</summary>
        public const float LineModest = 45f;

        /// <summary>How much lower the line is at each step of standards: 40 comfortable, 35 particular, 30 spoiled.</summary>
        public const float LineStep = 5f;

        /// <summary>The soft line for a spoiled colonist.</summary>
        public const float LineSpoiled = LineModest - 3f * LineStep;

        /// <summary>The first stretch above the line, of which <see cref="FirstShare"/> is taken back.</summary>
        public const float FirstStretch = 20f;

        public const float FirstShare = 0.5f;

        /// <summary>The share taken back of everything beyond the first stretch.</summary>
        public const float BeyondShare = 0.75f;

        /// <summary>Where good moods start to count for less, by the colonist's standards level (0 modest to 3 spoiled).</summary>
        public static float LineFor(int level) => LineModest - LineStep * Math.Max(0, Math.Min(3, level));

        /// <summary>The standards level of a standards value (0 to 1), as Standards.LevelFor has it: a step every quarter.</summary>
        public static int Level(float standards) =>
            float.IsNaN(standards) || standards < 0.25f ? 0 : standards < 0.5f ? 1 : standards < 0.75f ? 2 : 3;

        /// <summary>Where good moods start to count for less, by the colonist's standards (0 modest to 1 spoiled).</summary>
        public static float Line(float standards) => LineFor(Level(standards));

        /// <summary>
        /// How much of a colonist's good moods (the sum of every positive mood, as felt) a good life takes back: nothing up
        /// to the line, half of the first <see cref="FirstStretch"/> points over it, three quarters of the rest. Every
        /// point more still adds at least a quarter of a point, so more good things always mean a better mood.
        /// </summary>
        public static float TakeBack(float positives, float line)
        {
            if (float.IsNaN(positives) || float.IsNaN(line) || positives <= line) return 0f;
            float over = positives - line;
            float first = Math.Min(over, FirstStretch);
            return first * FirstShare + (over - first) * BeyondShare;
        }

        /// <summary>The good moods as they still count after the take-back: never less than the line (or all of them, below it).</summary>
        public static float Kept(float positives, float line) => positives - TakeBack(positives, line);

        /// <summary>
        /// The take-back as it's felt in hard times (PressureMath.HardTimes, 0 to 1): in full while life is good, fading to
        /// nothing at the minor-break line, so a good life grown ordinary never pushes anyone toward a break.
        /// </summary>
        public static float InHardTimes(float takeBack, float hard)
        {
            if (takeBack <= 0f || float.IsNaN(takeBack)) return 0f;
            float h = float.IsNaN(hard) ? 0f : Math.Max(0f, Math.Min(1f, hard));
            return takeBack * (1f - h);
        }

        /// <summary>The least share of a good mood anyone feels, however much they have.</summary>
        public const float MinShare = 0.05f;

        /// <summary>
        /// The share of each good mood a colonist still feels when <paramref name="takenBack"/> of their good moods'
        /// sum <paramref name="positives"/> no longer lifts them: every good mood shrinks by the same share, so the sum
        /// comes out as before and no mood of its own is needed. 1 when nothing is taken back.
        /// </summary>
        public static float Share(float positives, float takenBack)
        {
            if (float.IsNaN(positives) || float.IsNaN(takenBack) || positives <= 0f || takenBack <= 0f) return 1f;
            float s = (positives - takenBack) / positives;
            return s < MinShare ? MinShare : s > 1f ? 1f : s;
        }

        // ---------------- Everyday comforts and fresh joys ----------------

        /// <summary>How ordinary something brand new is: it loses the least.</summary>
        public const float FreshWeight = 0.15f;

        /// <summary>How ordinary a small good memory is (a day and a half or less) whose recent count is unknown (given before loading).</summary>
        public const float UnknownWeight = 0.65f;

        /// <summary>A good memory lasting this many days or less is a small everyday pleasure: a meal, a chat, a smoke.</summary>
        public const float SmallDays = 1.5f;

        /// <summary>A good memory turns ordinary over this share of its time, between a day and six days.</summary>
        public const float SettleShare = 0.6f, MinSettleDays = 1f, MaxSettleDays = 6f;

        /// <summary>How long the count of a kind of good memory takes to halve: two days.</summary>
        public const int RecentHalfLifeTicks = 120000;

        /// <summary>The count of a kind of memory had lately, <paramref name="ticks"/> after it stood at <paramref name="count"/>.</summary>
        public static float Decayed(float count, int ticks)
        {
            if (float.IsNaN(count) || count <= 0f) return 0f;
            if (ticks <= 0) return count;
            return count * (float)Math.Pow(0.5, ticks / (double)RecentHalfLifeTicks);
        }

        /// <summary>
        /// How ordinary a good memory has become with its age: brand new it is <see cref="FreshWeight"/>, everyday once
        /// <see cref="SettleShare"/> of its time has passed (at least a day, at most six; six for one without an end).
        /// </summary>
        public static float AgeWeight(float ageDays, float durationDays)
        {
            float settle = durationDays > 0f ? Math.Max(MinSettleDays, Math.Min(MaxSettleDays, durationDays * SettleShare)) : MaxSettleDays;
            float t = float.IsNaN(ageDays) || ageDays <= 0f ? 0f : Math.Min(1f, ageDays / settle);
            return FreshWeight + (1f - FreshWeight) * t;
        }

        /// <summary>
        /// How ordinary a kind of good memory is from how often it has come lately (<paramref name="recent"/>, halving every
        /// two days): once is new, three times or more is everyday, as a meal a day settles at about three and a half.
        /// Unknown (below zero): a small everyday pleasure counts as in between, anything bigger leaves it to its age (0).
        /// </summary>
        public static float OftenWeight(float recent, float durationDays)
        {
            if (float.IsNaN(recent) || recent < 0f) return durationDays > 0f && durationDays <= SmallDays ? UnknownWeight : 0f;
            float w = FreshWeight + (1f - FreshWeight) * (recent - 1f) / 2f;
            return w < FreshWeight ? FreshWeight : w > 1f ? 1f : w;
        }

        /// <summary>
        /// How ordinary a good mood is to them, from <see cref="FreshWeight"/> (new) to 1 (everyday): a lasting mood (a room,
        /// their beliefs, the council) and several of a kind at once are everyday; a memory is as ordinary as the more of
        /// its age (a wedding starts new and settles over days) and how often its kind has come lately.
        /// </summary>
        public static float Weight(bool lasting, float recent, int held, float ageDays, float durationDays)
        {
            if (lasting || held >= 2) return 1f;
            return Math.Max(AgeWeight(ageDays, durationDays), OftenWeight(recent, durationDays));
        }

        /// <summary>
        /// Standards' cut on a comfort (GoodTimesMath.ComfortFactor), as felt for something this ordinary: in full on an
        /// everyday comfort, hardly at all on something new.
        /// </summary>
        public static float ComfortFor(float factor, float weight)
        {
            if (float.IsNaN(factor) || factor >= 1f) return 1f;
            float w = float.IsNaN(weight) ? 1f : Math.Max(0f, Math.Min(1f, weight));
            return 1f - (1f - factor) * w;
        }

        /// <summary>
        /// Spreads what a good life takes back over their good moods by how ordinary each is: each one's share is
        /// 1 - k * weight, with k set so that what comes off all of them adds up to <paramref name="takeBack"/>, and no
        /// share under <see cref="MinShare"/> (what one can't give, the others give). An everyday comfort loses the most,
        /// something new the least; with equal weights every share is the same as <see cref="Share"/>. Writes each
        /// mood's share into <paramref name="shares"/> (1 for a zero or bad one).
        /// </summary>
        public static void Spread(IList<float> values, IList<float> weights, float takeBack, IList<float> shares)
        {
            int n = values.Count;
            float total = 0f, weighted = 0f, least = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                shares[i] = 1f;
                float v = values[i], w = weights[i];
                if (!(v > 0f) || !(w > 0f)) continue;
                total += v;
                weighted += v * w;
                if (w < least) least = w;
            }
            if (!(takeBack > 0f) || total <= 0f || weighted <= 0f) return;
            const float cap = 1f - MinShare;
            // What comes off them all with a given k: rises with k, until every share sits at the least.
            float Taken(float k)
            {
                float t = 0f;
                for (int i = 0; i < n; i++)
                    if (values[i] > 0f && weights[i] > 0f)
                        t += values[i] * Math.Min(cap, k * weights[i]);
                return t;
            }
            float lo = 0f, hi = cap / least;
            if (Taken(hi) <= takeBack) lo = hi;
            else
                for (int it = 0; it < 40; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Taken(mid) < takeBack) lo = mid;
                    else hi = mid;
                }
            float kk = lo == hi ? lo : (lo + hi) * 0.5f;
            for (int i = 0; i < n; i++)
                if (values[i] > 0f && weights[i] > 0f)
                    shares[i] = 1f - Math.Min(cap, kk * weights[i]);
        }

        /// <summary>The mood (0 to 1) from good and bad moods with the take-back: 50% plus the sum, clamped as the game clamps it.</summary>
        public static float Mood(float positives, float negatives, float line)
        {
            float m = 0.5f + (Kept(positives, line) + Math.Min(0f, negatives)) / 100f;
            return m < 0f ? 0f : m > 1f ? 1f : m;
        }

        // ---------------- Quiet troubles ----------------

        /// <summary>A day counts as content from this mood up (the mood at which standards start to rise).</summary>
        public const float ContentMood = 0.7f;

        /// <summary>Content days before a quiet trouble can come at all.</summary>
        public const int TroubleFromDays = 3;

        /// <summary>The chance on the first day it can come, what each further content day adds, and the most it gets to.</summary>
        public const float TroubleStart = 0.02f, TroubleRise = 0.01f, TroubleCap = 0.12f;

        /// <summary>Days after one quiet trouble before the next can come to the same colonist.</summary>
        public const float TroubleGapDays = 10f;

        /// <summary>Content days in a row after one more day at this mood: one more, or none after a day below the line.</summary>
        public static int NextContentDays(int days, float mood) => float.IsNaN(mood) || mood < ContentMood ? 0 : Math.Max(0, days) + 1;

        /// <summary>
        /// The chance of a quiet trouble today, by how many days in a row they've been content: none for the first
        /// <see cref="TroubleFromDays"/>, then 2%, one point more each day, and never more than 12%.
        /// </summary>
        public static float TroubleChance(int contentDays)
        {
            if (contentDays < TroubleFromDays) return 0f;
            return Math.Min(TroubleCap, TroubleStart + TroubleRise * (contentDays - TroubleFromDays));
        }

        /// <summary>A trouble eased by company: the age it jumps to, so that half of what was left of it is gone.</summary>
        public static int EasedAge(int age, int duration)
        {
            if (duration <= 0) return age;
            int a = Math.Max(0, age);
            return a >= duration ? a : a + (duration - a) / 2;
        }

        // ---------------- What the troubles read ----------------

        /// <summary>
        /// Worry about family out in the world, by how near they are on the family tree (FamilyMath.TreeRank: 0 spouse,
        /// 1 fiancé, 2 lover, 3 parent, 4 sibling, 5 half-sibling, 6 child, 7 grandparent, 8 grandchild): 1 for a partner,
        /// a parent or a child, 0 for the rest of the near family, -1 for anyone further out.
        /// </summary>
        public static int WorryStage(int treeRank) => treeRank < 0 || treeRank > 8 ? -1 : treeRank <= 3 || treeRank == 6 ? 1 : 0;

        /// <summary>A body that aches with the years: 50 in human years.</summary>
        public const float AchingAge = 50f;

        /// <summary>Aches come twice as readily on a cold or wet day.</summary>
        public static float AchesWeight(bool coldOrWet) => coldOrWet ? 2f : 1f;

        /// <summary>Missing their old life comes most in the first season here, less after a year, little after three.</summary>
        public static float FormerLifeWeight(float daysHere) => daysHere < 15f ? 1.5f : daysHere < 60f ? 1f : daysHere < 180f ? 0.6f : 0.3f;

        /// <summary>Days of the same work before it can make someone restless (with the same recreation too), and on its own.</summary>
        public const int RestlessWorkDays = 5, RestlessWorkAlone = 8;

        /// <summary>Recreation counted as done lately (the game's tolerance for that kind of recreation).</summary>
        public const float JoyLately = 0.02f;

        /// <summary>The one kind of recreation they've had has to be this well worn to count as "the same recreation".</summary>
        public const float JoyWorn = 0.1f;

        /// <summary>
        /// Only one kind of recreation lately (one tolerance above <see cref="JoyLately"/>), and that one well worn. No
        /// recreation at all isn't the same recreation; the game has its own mood for that.
        /// </summary>
        public static bool SameRecreation(IList<float> tolerances)
        {
            if (tolerances == null) return false;
            int lately = 0;
            float top = 0f;
            for (int i = 0; i < tolerances.Count; i++)
            {
                if (tolerances[i] > JoyLately) lately++;
                if (tolerances[i] > top) top = tolerances[i];
            }
            return lately == 1 && top >= JoyWorn;
        }

        /// <summary>Restless: the same work for <see cref="RestlessWorkDays"/> days with the same recreation, or for <see cref="RestlessWorkAlone"/> whatever they did for fun.</summary>
        public static bool Restless(int sameWorkDays, bool sameRecreation) =>
            sameWorkDays >= RestlessWorkAlone || (sameWorkDays >= RestlessWorkDays && sameRecreation);
    }
}
