using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Used to the good life: a colonist with a pile of good moods stops feeling all of it, so each good mood counts for
    /// a little less. The sum of every good mood on their mind (the game's, other mods' and Rimfolk's, as each is felt)
    /// counts in full up to a soft line set by their standards (45 for the modest, down to 30 for the spoiled); of what is
    /// above it, half of the first 20 points and three quarters of the rest no longer lift them. That part is not a mood
    /// of its own: every good mood is felt at the same share, which its own line on the Mood tab shows, and its tooltip
    /// says why. Bad moods are never touched, and every good thing still helps (each point more adds at least a quarter),
    /// so progress adds up, but a richly comfortable colonist settles well under the top of the bar, where a bad day still
    /// shows. Like Rimfolk's pressures it eases in hard times, to nothing at the minor-break line, so it never pushes
    /// anyone into a break. The arithmetic is in <see cref="GoodLifeMath"/>; the share is applied where the game adds up
    /// each mood group (Patches/Patch_GoodTimes.cs, Patch_ComfortStandards).
    /// <para>
    /// The sum is worked out from the thoughts themselves, grouped and stacked as the game stacks them, never through
    /// the game's own totals (those are what the share changes), and kept per colonist for a few seconds of game time.
    /// </para>
    /// </summary>
    public static class GoodLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => RimfolkPatches.On && S != null && S.usedToGoodLife && S.MoodEffects;

        /// <summary>How long a colonist's sum is kept before it is worked out again.</summary>
        public const int RecomputeTicks = 250;

        public class Reading
        {
            public int tick = int.MinValue;
            public float positives, line, takeBack;
            /// <summary>How hard times are (PressureMath.HardTimes), and the part taken back as it's felt with that.</summary>
            public float hard, felt;
            /// <summary>The share of each good mood they still feel (1: all of it).</summary>
            public float share = 1f;
        }

        private static readonly Dictionary<int, Reading> readings = new Dictionary<int, Reading>();
        private static readonly List<Thought> tmpThoughts = new List<Thought>();
        private static readonly List<float> tmpOffsets = new List<float>();
        private static bool working;

        private static AccessTools.FieldRef<SituationalThoughtHandler, List<Thought_Situational>> situationalRef;
        private static bool situationalLooked;

        public static void Reset() => readings.Clear();

        /// <summary>
        /// The share of each good mood this colonist feels right now (1 when the rule is off or takes nothing), worked out
        /// again when the last reading is older than <see cref="RecomputeTicks"/>.
        /// </summary>
        public static float Share(Pawn p)
        {
            if (p == null || GoodTimes.Raw || !On) return 1f;
            return Refresh(p)?.share ?? 1f;
        }

        /// <summary>The last reading for this colonist, as the tooltips and the Mood tab's readout explain it (null if none).</summary>
        public static Reading ReadingOf(Pawn p) => p != null && readings.TryGetValue(p.thingIDNumber, out var r) ? r : null;

        private static Reading Refresh(Pawn p)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            readings.TryGetValue(p.thingIDNumber, out var r);
            if (r != null && r.tick <= now && now - r.tick < RecomputeTicks) return r;
            // Adding up the good moods asks some of them for their size, which could come back here: the last reading stands.
            if (working) return r;
            working = true;
            try
            {
                if (r == null)
                {
                    if (readings.Count > 300) Prune(now);
                    readings[p.thingIDNumber] = r = new Reading();
                }
                r.tick = now;
                if (!Pressures.Eligible(p))
                {
                    r.positives = r.line = r.takeBack = r.hard = r.felt = 0f;
                    r.share = 1f;
                    return r;
                }
                r.positives = Positives(p);
                r.line = GoodLifeMath.LineFor((int)Standards.LevelOf(p));
                r.takeBack = GoodLifeMath.TakeBack(r.positives, r.line);
                // People go easier on themselves when they're struggling: it fades out near the minor-break line.
                r.hard = Pressures.Hard(p);
                r.felt = GoodLifeMath.InHardTimes(r.takeBack, r.hard);
                r.share = GoodLifeMath.Share(r.positives, r.felt);
                return r;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("used to the good life", e);
                if (r != null) r.share = 1f;
                return r;
            }
            finally
            {
                working = false;
                // Don't keep thoughts (and through them, the whole game) alive between calls.
                tmpThoughts.Clear();
                tmpOffsets.Clear();
            }
        }

        private static void Prune(int now)
        {
            var old = new List<int>();
            foreach (var kv in readings)
                if (kv.Value.tick > now || now - kv.Value.tick > GenDate.TicksPerDay)
                    old.Add(kv.Key);
            foreach (int k in old) readings.Remove(k);
        }

        private static List<Thought_Situational> SituationalOf(SituationalThoughtHandler handler)
        {
            if (handler == null) return null;
            if (!situationalLooked)
            {
                situationalLooked = true;
                try
                {
                    situationalRef = AccessTools.FieldRefAccess<SituationalThoughtHandler, List<Thought_Situational>>("cachedThoughts");
                }
                catch (Exception e)
                {
                    situationalRef = null;
                    CouncilLog.ErrorOnce("used to the good life: the lasting moods", e);
                }
            }
            return situationalRef == null ? null : situationalRef(handler);
        }

        /// <summary>
        /// Every good mood on their mind, as felt before this rule: the memories and the lasting moods that are on, grouped
        /// and stacked as the game adds them up, with standards' share of the game's comforts taken off (GoodTimes.Felt,
        /// as the game's total has it). Bad moods are left out.
        /// </summary>
        private static float Positives(Pawn p)
        {
            var handler = p.needs?.mood?.thoughts;
            if (handler == null) return 0f;
            tmpThoughts.Clear();
            tmpOffsets.Clear();
            var memories = handler.memories?.Memories;
            if (memories != null)
                for (int i = 0; i < memories.Count; i++)
                {
                    var m = memories[i];
                    if (m?.def == null) continue;
                    float o = m.MoodOffset();
                    if (o == 0f || float.IsNaN(o)) continue;
                    tmpThoughts.Add(m);
                    tmpOffsets.Add(o);
                }
            var lasting = SituationalOf(handler.situational);
            if (lasting != null)
                for (int i = 0; i < lasting.Count; i++)
                {
                    var s = lasting[i];
                    if (s?.def == null || !s.Active) continue;
                    float o = s.MoodOffset();
                    if (o == 0f || float.IsNaN(o)) continue;
                    tmpThoughts.Add(s);
                    tmpOffsets.Add(o);
                }
            float sum = 0f;
            for (int i = 0; i < tmpThoughts.Count; i++)
            {
                var first = tmpThoughts[i];
                if (first == null) continue;
                // The game adds a group up as the average of its thoughts times 1 + m + m^2..., m being the def's stacking.
                float total = 0f, stacked = 0f, mult = 1f;
                int n = 0;
                for (int j = i; j < tmpThoughts.Count; j++)
                {
                    var t = tmpThoughts[j];
                    if (t == null || (j != i && !t.GroupsWith(first))) continue;
                    total += tmpOffsets[j];
                    stacked += mult;
                    mult *= t.def.stackedEffectMultiplier;
                    n++;
                    if (j != i) tmpThoughts[j] = null;
                }
                float group = total / n * stacked;
                if (group <= 0f) continue;
                sum += GoodTimes.Felt(handler, first, group);
            }
            return sum;
        }

        // ---------------- Text ----------------

        private static string Whole(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);

        /// <summary>
        /// The Mood tab readout's part: the share of each good mood they feel, from how far their good moods go over their
        /// standards' line (null when it takes nothing).
        /// </summary>
        public static string ReadoutPart(Pawn p)
        {
            var r = ReadingOf(p);
            if (!On || r == null || r.share >= 0.995f) return null;
            return "CC_GoodLife_Readout".Translate(r.share.ToStringPercent().Named("SHARE"), Whole(r.positives).Named("GOOD"),
                Whole(r.line).Named("LINE")).Resolve();
        }

        /// <summary>The paragraph for the readout's tooltip: the numbers behind the share, and hard times easing it.</summary>
        public static string Why(Pawn p)
        {
            var r = ReadingOf(p);
            if (!On || r == null || r.takeBack <= 0f) return null;
            string level = ("CC_Standards_" + Standards.LevelOf(p)).Translate().ToString();
            string why = "CC_GoodLife_Why".Translate(Whole(r.positives).Named("GOOD"), Whole(r.line).Named("LINE"), level.Named("LEVEL"),
                Mathf.RoundToInt(GoodLifeMath.FirstStretch).Named("FIRST"), r.share.ToStringPercent().Named("SHARE"), p.Named("PAWN")).Resolve();
            if (r.hard > 0f) why += " " + "CC_GoodLife_Hard".Translate(p.Named("PAWN")).Resolve();
            return why;
        }
    }
}
