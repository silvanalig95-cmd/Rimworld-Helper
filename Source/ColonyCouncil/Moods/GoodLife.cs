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
    /// of its own: it comes off the good moods themselves, most off the everyday ones (lasting moods such as rooms,
    /// beliefs and the council, and memories of a kind they've had often lately) and least off something new, so a
    /// wedding still lands while the fifth fine meal this week barely registers. Each good mood's line on the Mood tab
    /// shows what it counts for now, and its tooltip says why. Life goals reached count in full and are left out. Bad
    /// moods are never touched, and every good thing still helps (each point more adds at least a quarter),
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
            /// <summary>The share of their good moods they still feel, all together (1: all of it).</summary>
            public float share = 1f;
            /// <summary>Each kind of good mood's own share, and how ordinary it is to them (GoodLifeMath.Weight).</summary>
            public readonly Dictionary<ThoughtDef, float> shares = new Dictionary<ThoughtDef, float>();
            public readonly Dictionary<ThoughtDef, float> weights = new Dictionary<ThoughtDef, float>();
        }

        /// <summary>A kind of memory had lately: how many times, halving every two days, as of a tick.</summary>
        private struct Recent
        {
            public float count;
            public int tick;
        }

        /// <summary>
        /// How often each kind of memory has come to each colonist lately (not saved: after loading, what was already
        /// on their mind counts as neither new nor everyday until it comes again).
        /// </summary>
        private static readonly Dictionary<int, Dictionary<ThoughtDef, Recent>> recent = new Dictionary<int, Dictionary<ThoughtDef, Recent>>();

        private static readonly Dictionary<int, Reading> readings = new Dictionary<int, Reading>();
        private static readonly List<Thought> tmpThoughts = new List<Thought>();
        private static readonly List<float> tmpOffsets = new List<float>();
        private static readonly List<ThoughtDef> tmpDefs = new List<ThoughtDef>();
        private static readonly List<float> tmpValues = new List<float>(), tmpWeights = new List<float>(), tmpShares = new List<float>();
        private static readonly List<int> tmpHeld = new List<int>();
        private static readonly List<bool> tmpLasting = new List<bool>();
        private static bool working;

        private static AccessTools.FieldRef<SituationalThoughtHandler, List<Thought_Situational>> situationalRef;
        private static bool situationalLooked;

        public static void Reset()
        {
            readings.Clear();
            recent.Clear();
        }

        /// <summary>
        /// The share of this kind of good mood the colonist feels right now (1 when the rule is off or takes nothing, and
        /// for a life goal reached), worked out again when the last reading is older than <see cref="RecomputeTicks"/>.
        /// A kind that came since then counts in full until the next reading.
        /// </summary>
        public static float ShareOf(Pawn p, ThoughtDef def)
        {
            if (p == null || def == null || GoodTimes.Raw || !On) return 1f;
            var r = Refresh(p);
            return r != null && r.shares.TryGetValue(def, out float s) ? s : 1f;
        }

        /// <summary>The share of all their good moods together the colonist feels right now (1 when nothing is taken).</summary>
        public static float Share(Pawn p)
        {
            if (p == null || GoodTimes.Raw || !On) return 1f;
            return Refresh(p)?.share ?? 1f;
        }

        /// <summary>How ordinary this kind of good mood is to them (GoodLifeMath.Weight), as last worked out; -1 if unknown.</summary>
        public static float WeightOf(Pawn p, ThoughtDef def)
        {
            var r = ReadingOf(p);
            return r != null && def != null && r.weights.TryGetValue(def, out float w) ? w : -1f;
        }

        /// <summary>A memory came to them (Patch_GoodTimesFade): one more of its kind lately.</summary>
        public static void Notify_Memory(Pawn p, Thought_Memory m)
        {
            if (p == null || m?.def == null || !On || !p.IsColonist) return;
            try
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                if (!recent.TryGetValue(p.thingIDNumber, out var kinds))
                {
                    if (recent.Count > 300) recent.Clear();
                    recent[p.thingIDNumber] = kinds = new Dictionary<ThoughtDef, Recent>();
                }
                kinds.TryGetValue(m.def, out var had);
                float count = had.tick > now ? 0f : GoodLifeMath.Decayed(had.count, now - had.tick);
                kinds[m.def] = new Recent { count = count + 1f, tick = now };
                if (kinds.Count > 120) Forget(kinds, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("used to the good life: memories lately", e);
            }
        }

        /// <summary>Drops the kinds that haven't come for a long while.</summary>
        private static void Forget(Dictionary<ThoughtDef, Recent> kinds, int now)
        {
            var old = new List<ThoughtDef>();
            foreach (var kv in kinds)
                if (kv.Value.tick > now || GoodLifeMath.Decayed(kv.Value.count, now - kv.Value.tick) < 0.1f)
                    old.Add(kv.Key);
            foreach (var d in old) kinds.Remove(d);
        }

        /// <summary>How many of this kind of memory came to them lately, halving every two days; -1 if not known.</summary>
        private static float RecentCount(Pawn p, ThoughtDef def, int now)
        {
            if (!recent.TryGetValue(p.thingIDNumber, out var kinds) || !kinds.TryGetValue(def, out var had)) return -1f;
            return had.tick > now ? -1f : GoodLifeMath.Decayed(had.count, now - had.tick);
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
                r.shares.Clear();
                r.weights.Clear();
                if (!Pressures.Eligible(p))
                {
                    r.positives = r.line = r.takeBack = r.hard = r.felt = 0f;
                    r.share = 1f;
                    return r;
                }
                r.positives = Positives(p, now);
                r.line = GoodLifeMath.LineFor((int)Standards.LevelOf(p));
                r.takeBack = GoodLifeMath.TakeBack(r.positives, r.line);
                // People go easier on themselves when they're struggling: it fades out near the minor-break line.
                r.hard = Pressures.Hard(p);
                r.felt = GoodLifeMath.InHardTimes(r.takeBack, r.hard);
                r.share = GoodLifeMath.Share(r.positives, r.felt);
                // The same amount, most of it off the everyday good moods and least off the new ones.
                tmpShares.Clear();
                for (int i = 0; i < tmpValues.Count; i++) tmpShares.Add(1f);
                GoodLifeMath.Spread(tmpValues, tmpWeights, r.felt, tmpShares);
                for (int i = 0; i < tmpDefs.Count; i++)
                {
                    r.weights[tmpDefs[i]] = tmpWeights[i];
                    if (tmpShares[i] < 1f) r.shares[tmpDefs[i]] = tmpShares[i];
                }
                return r;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("used to the good life", e);
                if (r != null)
                {
                    r.share = 1f;
                    r.shares.Clear();
                }
                return r;
            }
            finally
            {
                working = false;
                // Don't keep thoughts (and through them, the whole game) alive between calls.
                tmpThoughts.Clear();
                tmpOffsets.Clear();
                tmpDefs.Clear();
                tmpValues.Clear();
                tmpWeights.Clear();
                tmpShares.Clear();
                tmpHeld.Clear();
                tmpLasting.Clear();
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
        /// as the game's total has it). Bad moods and life goals reached are left out. Each kind's total and how ordinary
        /// it is are left in tmpDefs, tmpValues and tmpWeights for the spread.
        /// </summary>
        private static float Positives(Pawn p, int now)
        {
            tmpDefs.Clear();
            tmpValues.Clear();
            tmpWeights.Clear();
            tmpHeld.Clear();
            tmpLasting.Clear();
            var handler = p.needs?.mood?.thoughts;
            if (handler == null) return 0f;
            tmpThoughts.Clear();
            tmpOffsets.Clear();
            var memories = handler.memories?.Memories;
            if (memories != null)
                for (int i = 0; i < memories.Count; i++)
                {
                    var m = memories[i];
                    if (m?.def == null || Milestone.Is(m.def)) continue;
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
                    if (s?.def == null || !s.Active || Milestone.Is(s.def)) continue;
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
                float felt = GoodTimes.Felt(handler, first, group);
                sum += felt;
                // By kind: social memories about different people are one kind, and so are several of the same.
                int k = tmpDefs.IndexOf(first.def);
                if (k < 0)
                {
                    tmpDefs.Add(first.def);
                    tmpValues.Add(0f);
                    tmpHeld.Add(0);
                    tmpLasting.Add(first is Thought_Situational);
                    k = tmpDefs.Count - 1;
                }
                tmpValues[k] += felt;
                tmpHeld[k] += n;
            }
            for (int i = 0; i < tmpDefs.Count; i++)
                tmpWeights.Add(GoodLifeMath.Weight(tmpLasting[i], tmpLasting[i] ? 0f : RecentCount(p, tmpDefs[i], now), tmpHeld[i]));
            return sum;
        }

        /// <summary>
        /// How ordinary a kind of good mood is to them, for its tooltip: part of everyday life (a lasting mood), had
        /// often lately, still new, or in between.
        /// </summary>
        public enum Kind { Lasting, Often, New, Between }

        public static Kind KindOf(Pawn p, Thought group)
        {
            if (group is Thought_Situational) return Kind.Lasting;
            float w = WeightOf(p, group?.def);
            return w >= 0.8f ? Kind.Often : w >= 0f && w <= 0.45f ? Kind.New : Kind.Between;
        }

        // ---------------- Text ----------------

        private static string Whole(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);

        /// <summary>
        /// The Mood tab readout's part: the share of their good moods they feel in all, from how far their good moods go
        /// over their standards' line (null when it takes nothing).
        /// </summary>
        public static string ReadoutPart(Pawn p)
        {
            var r = ReadingOf(p);
            if (!On || r == null || r.share >= 0.995f) return null;
            return "CC_GoodLife_Readout".Translate(r.share.ToStringPercent().Named("SHARE")).Resolve();
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
