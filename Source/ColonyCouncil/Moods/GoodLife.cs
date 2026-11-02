using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Used to the good life: a colonist with a pile of good moods stops feeling all of it. The sum of every good mood on
    /// their mind (the game's, other mods' and Rimfolk's, as each is felt) counts in full up to a soft line set by their
    /// standards (45 for the modest, down to 30 for the spoiled); of what is above it, half of the first 20 points and
    /// three quarters of the rest are taken back, as one bad mood with its own line on the Mood tab. Bad moods are
    /// never touched, and every good thing still helps (each point more adds at least a quarter), so progress adds up,
    /// but a richly comfortable colonist settles well under the top of the bar, where a bad day still shows. Like
    /// Rimfolk's pressures it eases in hard times, to nothing at the minor-break line, so it never pushes anyone into a
    /// break. The arithmetic is in <see cref="GoodLifeMath"/>.
    /// <para>
    /// The sum is worked out from the thoughts themselves, grouped and stacked as the game stacks them, never through
    /// the game's own totals (those share lists with the mood being added up), and kept per colonist for a few seconds
    /// of game time; the thought's own worker refreshes it, and the thought only reads it.
    /// </para>
    /// </summary>
    public static class GoodLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.usedToGoodLife && S.MoodEffects;

        /// <summary>How long a colonist's sum is kept before the worker works it out again.</summary>
        public const int RecomputeTicks = 250;

        public class Reading
        {
            public int tick = int.MinValue;
            public float positives, line, takeBack;
            /// <summary>How hard times are (PressureMath.HardTimes), and the take-back as it's felt with that.</summary>
            public float hard, felt;
        }

        private static readonly Dictionary<int, Reading> readings = new Dictionary<int, Reading>();
        private static readonly List<Thought> tmpThoughts = new List<Thought>();
        private static readonly List<float> tmpOffsets = new List<float>();
        private static bool working;

        private static AccessTools.FieldRef<SituationalThoughtHandler, List<Thought_Situational>> situationalRef;
        private static bool situationalLooked;

        public static void Reset() => readings.Clear();

        /// <summary>What a good life takes back from this colonist right now, as last worked out and as felt (0 if nothing).</summary>
        public static float Current(Pawn p) => p != null && readings.TryGetValue(p.thingIDNumber, out var r) ? r.felt : 0f;

        public static Reading ReadingOf(Pawn p) => p != null && readings.TryGetValue(p.thingIDNumber, out var r) ? r : null;

        /// <summary>
        /// The take-back, worked out again when the last reading is older than <see cref="RecomputeTicks"/> (from the
        /// thought's worker). While it is being worked out the thought itself counts as whatever it was before.
        /// </summary>
        public static float Refresh(Pawn p, ThoughtDef own)
        {
            if (p == null) return 0f;
            int now = Find.TickManager?.TicksGame ?? 0;
            readings.TryGetValue(p.thingIDNumber, out var r);
            if (r != null && r.tick <= now && now - r.tick < RecomputeTicks) return r.takeBack;
            if (working) return r?.takeBack ?? 0f;
            working = true;
            try
            {
                if (r == null)
                {
                    if (readings.Count > 300) Prune(now);
                    readings[p.thingIDNumber] = r = new Reading();
                }
                r.tick = now;
                r.positives = Positives(p, own);
                r.line = GoodLifeMath.LineFor((int)Standards.LevelOf(p));
                r.takeBack = GoodLifeMath.TakeBack(r.positives, r.line);
                // People go easier on themselves when they're struggling: it fades out near the minor-break line.
                r.hard = Pressures.Hard(p);
                r.felt = GoodLifeMath.InHardTimes(r.takeBack, r.hard);
                return r.takeBack;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("used to the good life", e);
                return 0f;
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
        /// Every good mood on their mind, as felt: the memories and the lasting moods that are on, grouped and stacked as
        /// the game adds them up, with standards' share of the game's comforts taken off (GoodTimes.Felt, as the game's
        /// total has it). This thought is left out, and so is every bad mood.
        /// </summary>
        private static float Positives(Pawn p, ThoughtDef own)
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
                    if (m?.def == null || m.def == own) continue;
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
                    if (s?.def == null || s.def == own || !s.Active) continue;
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

        /// <summary>The "why" line: their good moods, the line, and what is taken back.</summary>
        public static string Why(Pawn p)
        {
            var r = ReadingOf(p);
            if (r == null || r.takeBack <= 0f) return null;
            string level = ("CC_Standards_" + Standards.LevelOf(p)).Translate().ToString();
            string why = "CC_GoodLife_Why".Translate(Whole(r.positives).Named("GOOD"), Whole(r.line).Named("LINE"), level.Named("LEVEL"),
                Mathf.RoundToInt(GoodLifeMath.FirstStretch).Named("FIRST"), Mathf.RoundToInt(r.takeBack).Named("TAKEN"), p.Named("PAWN")).Resolve();
            if (r.hard > 0f) why += " " + "CC_GoodLife_Hard".Translate(Mathf.RoundToInt(r.felt).Named("FELT"), p.Named("PAWN")).Resolve();
            return why;
        }

        private static string Whole(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);
    }

    /// <summary>
    /// Used to the good life's mood: as big as the share of their good moods a good life takes back right now (worked
    /// out by the worker, see <see cref="GoodLife"/>). It reads as nothing while the Mood tab shows moods without
    /// Rimfolk's balance rules.
    /// </summary>
    public class Thought_GoodLife : Thought_Situational, IExplainedThought
    {
        public override float MoodOffset() => GoodTimes.Raw ? 0f : -GoodLife.Current(pawn);

        /// <summary>Under half a point it still counts, but isn't listed as a "0".</summary>
        public override bool VisibleInNeedsTab => base.VisibleInNeedsTab && GoodLife.Current(pawn) >= 0.5f;

        public string Why
        {
            get
            {
                try
                {
                    return GoodLife.Why(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("used to the good life: why", e);
                    return null;
                }
            }
        }

        public override string Description => Causes.Append(base.Description, Why);
    }

    /// <summary>On while a good life takes something back from them (see <see cref="GoodLife"/>).</summary>
    public class ThoughtWorker_GoodLife : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            try
            {
                if (!GoodLife.On || !Pressures.Eligible(p)) return ThoughtState.Inactive;
                return GoodLife.Refresh(p, def) > 0f ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("used to the good life", e);
                return ThoughtState.Inactive;
            }
        }
    }
}
