using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Good things grow ordinary: the one rule for how much a good mood counts (phase 3 of the review, 6 November 2026;
    /// it took the place of four: standards shrinking the council's rewards and the game's comforts, good memories
    /// fading sooner in good times, good moods adding up slowly, and used to the good life). The sum of every good mood
    /// on a colonist's mind (the game's, other mods' and Rimfolk's, as each is felt) counts in full up to a soft line
    /// their standards set: a colony used to the good life asks more, so the line is lower the more spoiled they are. Of
    /// what is above it, half of the first 20 points and three quarters of the rest no longer lift them. That part is
    /// not a mood of its own: it comes off the good moods themselves by how ordinary each is (GoodLifeMath.Weight), so a
    /// fresh joy (a wedding just attended) counts nearly in full and drifts down as the days go by, and the everyday
    /// comforts (a fine room, their beliefs, trust in the council, a kind of memory had often lately) shrink most. Each
    /// good mood's line on the Mood tab shows what it counts for now, and one line in its tooltip says why. Life goals
    /// reached count in full and are left out. Bad moods are never touched, every good thing still helps (each point
    /// more adds at least a quarter), and it eases in hard times, to nothing at the minor-break line, so it never pushes
    /// anyone into a break. The arithmetic is in <see cref="GoodLifeMath"/>; the share is applied where the game adds up
    /// each mood group (Patches/Patch_GoodTimes.cs, Patch_ComfortStandards). One setting, "Good things grow ordinary".
    /// <para>
    /// The sum is worked out from the thoughts themselves, grouped and stacked as the game stacks them, never through
    /// the game's own totals (those are what the share changes), and kept per colonist for a few seconds of game time.
    /// </para>
    /// </summary>
    public static class GoodLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => RimfolkPatches.On && S != null && S.goodGrowsOrdinary && S.MoodEffects;

        /// <summary>
        /// While set, moods read as they would without this rule: the Mood tab's readout and the tooltips use it to say how
        /// much it takes off.
        /// </summary>
        public static bool Raw;

        /// <summary>A def from Rimfolk itself, not from the game or another mod.</summary>
        public static bool IsRimfolk(Def def) => def?.modContentPack != null && def.modContentPack == ColonyCouncilMod.Pack;

        /// <summary>
        /// The reward strength for a good mood Rimfolk gives that didn't go through the council (a moment, a lasting
        /// reward): the setting alone. How much it counts beside their other good moods is this rule's.
        /// </summary>
        public static float RewardScale(Pawn p) => S?.rewardFactor ?? 1f;

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
            public readonly Dictionary<ThoughtDef, Kind> kinds = new Dictionary<ThoughtDef, Kind>();
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
        private static readonly List<float> tmpAge = new List<float>(), tmpLasts = new List<float>(), tmpGroupValues = new List<float>();
        private static readonly List<Kind> tmpKinds = new List<Kind>();
        private static readonly List<Thought> tmpGroups = new List<Thought>();
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
            if (p == null || def == null || Raw || !On) return 1f;
            var r = Refresh(p);
            return r != null && r.shares.TryGetValue(def, out float s) ? s : 1f;
        }

        /// <summary>The share of all their good moods together the colonist feels right now (1 when nothing is taken).</summary>
        public static float Share(Pawn p)
        {
            if (p == null || Raw || !On) return 1f;
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
                r.kinds.Clear();
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
                    r.kinds[tmpDefs[i]] = tmpKinds[i];
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
                tmpAge.Clear();
                tmpLasts.Clear();
                tmpKinds.Clear();
                tmpGroups.Clear();
                tmpGroupValues.Clear();
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

        /// <summary>The lasting moods the game has worked out for them (its cache, read without changing it).</summary>
        public static List<Thought_Situational> SituationalOf(SituationalThoughtHandler handler)
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
        /// and stacked as the game adds them up. Bad moods and life goals reached are left out. Each kind's total and how ordinary
        /// it is are left in tmpDefs, tmpValues and tmpWeights for the spread.
        /// </summary>
        private static float Positives(Pawn p, int now)
        {
            tmpDefs.Clear();
            tmpValues.Clear();
            tmpWeights.Clear();
            tmpHeld.Clear();
            tmpLasting.Clear();
            tmpAge.Clear();
            tmpLasts.Clear();
            tmpKinds.Clear();
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
            // First the groups as the game adds them up, and by kind how ordinary each is (social memories about
            // different people are one kind, and so are several of the same).
            tmpGroups.Clear();
            tmpGroupValues.Clear();
            for (int i = 0; i < tmpThoughts.Count; i++)
            {
                var first = tmpThoughts[i];
                if (first == null) continue;
                // The game adds a group up as the average of its thoughts times 1 + m + m^2..., m being the def's stacking.
                float total = 0f, stacked = 0f, mult = 1f, youngest = float.MaxValue, lasts = 0f;
                int n = 0;
                for (int j = i; j < tmpThoughts.Count; j++)
                {
                    var t = tmpThoughts[j];
                    if (t == null || (j != i && !t.GroupsWith(first))) continue;
                    total += tmpOffsets[j];
                    stacked += mult;
                    mult *= t.def.stackedEffectMultiplier;
                    n++;
                    if (t is Thought_Memory tm && tm.age / (float)GenDate.TicksPerDay < youngest)
                    {
                        youngest = tm.age / (float)GenDate.TicksPerDay;
                        lasts = tm.permanent ? 0f : tm.DurationTicks / (float)GenDate.TicksPerDay;
                    }
                    if (j != i) tmpThoughts[j] = null;
                }
                float group = total / n * stacked;
                if (group <= 0f) continue;
                tmpGroups.Add(first);
                tmpGroupValues.Add(group);
                int k = tmpDefs.IndexOf(first.def);
                if (k < 0)
                {
                    tmpDefs.Add(first.def);
                    tmpValues.Add(0f);
                    tmpHeld.Add(0);
                    tmpLasting.Add(first is Thought_Situational);
                    tmpAge.Add(float.MaxValue);
                    tmpLasts.Add(0f);
                    k = tmpDefs.Count - 1;
                }
                tmpHeld[k] += n;
                if (youngest < tmpAge[k])
                {
                    tmpAge[k] = youngest;
                    tmpLasts[k] = lasts;
                }
            }
            for (int i = 0; i < tmpDefs.Count; i++)
            {
                bool lastingMood = tmpLasting[i];
                float age = tmpAge[i] == float.MaxValue ? 0f : tmpAge[i];
                float often = lastingMood ? 0f : GoodLifeMath.OftenWeight(RecentCount(p, tmpDefs[i], now), tmpLasts[i]);
                float aged = GoodLifeMath.AgeWeight(age, tmpLasts[i]);
                float w = GoodLifeMath.Weight(lastingMood, lastingMood ? 0f : RecentCount(p, tmpDefs[i], now), tmpHeld[i], age, tmpLasts[i]);
                tmpWeights.Add(w);
                Kind kind;
                if (lastingMood) kind = Kind.Lasting;
                else if (tmpHeld[i] >= 2) kind = Kind.Often;
                else if (w <= 0.45f) kind = Kind.New;
                else if (w >= 0.8f) kind = often >= aged ? Kind.Often : Kind.Settled;
                else kind = Kind.Between;
                tmpKinds.Add(kind);
            }
            // Then each group as felt.
            float sum = 0f;
            for (int g = 0; g < tmpGroups.Count; g++)
            {
                int k = tmpDefs.IndexOf(tmpGroups[g].def);
                float felt = tmpGroupValues[g];
                sum += felt;
                tmpValues[k] += felt;
            }
            tmpGroups.Clear();
            return sum;
        }

        /// <summary>
        /// How ordinary a kind of good mood is to them, for its tooltip: part of everyday life (a lasting mood), had
        /// often lately, still new, settled with time (the moment has passed), or in between.
        /// </summary>
        public enum Kind { Lasting, Often, New, Settled, Between }

        public static Kind KindOf(Pawn p, Thought group)
        {
            if (group is Thought_Situational) return Kind.Lasting;
            var r = ReadingOf(p);
            return r != null && group?.def != null && r.kinds.TryGetValue(group.def, out var k) ? k : Kind.Between;
        }

        // ---------------- Text ----------------

        private static string Whole(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);

        private static string LevelLabel(Pawn p) => ("CC_Standards_" + Standards.LevelOf(p)).Translate().ToString();

        private static readonly List<Thought> tmpReadout = new List<Thought>();

        /// <summary>A mood group as it would be without this rule.</summary>
        public static float RawOffsetOfGroup(ThoughtHandler handler, Thought group)
        {
            bool was = Raw;
            Raw = true;
            try
            {
                return handler.MoodOffsetOfGroup(group);
            }
            finally
            {
                Raw = was;
            }
        }

        /// <summary>
        /// The one line for a good mood that counts for less (in its tooltip on the Mood tab and the game's Needs tab):
        /// what it counts as now, of what, and why in a few words. Null when it counts in full.
        /// </summary>
        public static string Note(Pawn p, Thought group, float felt)
        {
            if (p == null || group?.def == null || felt <= 0f || !On) return null;
            float share = ShareOf(p, group.def);
            if (share >= 0.995f) return null;
            float full = felt / share;
            if (full - felt < 0.05f) return null;
            string how;
            switch (KindOf(p, group))
            {
                case Kind.New: how = "CC_Ordinary_How_New"; break;
                case Kind.Settled: how = "CC_Ordinary_How_Settled"; break;
                case Kind.Lasting: how = "CC_Ordinary_How_Lasting"; break;
                case Kind.Often: how = "CC_Ordinary_How_Often"; break;
                default: how = "CC_Ordinary_How_Between"; break;
            }
            return "CC_Ordinary_Note".Translate(MoodView.Number(felt).Named("FELT"), MoodView.Number(full).Named("FULL"),
                how.Translate(p.Named("PAWN")).Resolve().Named("HOW"), p.Named("PAWN")).Resolve();
        }

        /// <summary>
        /// One line for the Mood tab: what good things grown ordinary take off the colonist's good moods right now, as
        /// felt against in full. Null when it takes nothing off.
        /// </summary>
        public static string Readout(Pawn p)
        {
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null || !On) return null;
            float felt = 0f, full = 0f;
            try
            {
                tmpReadout.Clear();
                handler.GetDistinctMoodThoughtGroups(tmpReadout);
                for (int i = 0; i < tmpReadout.Count; i++)
                {
                    var g = tmpReadout[i];
                    if (g?.def == null) continue;
                    float raw = RawOffsetOfGroup(handler, g);
                    if (raw <= 0f) continue;
                    full += raw;
                    felt += Mathf.Max(0f, handler.MoodOffsetOfGroup(g));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("good things grown ordinary: readout", e);
                return null;
            }
            finally
            {
                // Don't keep thoughts (and through them, the whole game) alive between calls.
                tmpReadout.Clear();
            }
            if (full - felt < 0.5f) return null;
            return "CC_GoodTimes_Line".Translate("CC_Ordinary_Readout".Translate(Whole(felt).Named("FELT"), Whole(full).Named("FULL")).Resolve()).Resolve();
        }

        /// <summary>The Mood tab readout's tooltip: how the rule works, and the numbers behind it for them.</summary>
        public static string Tip(Pawn p)
        {
            string tip = "CC_GoodTimes_Tip".Translate().Resolve();
            string why = Why(p);
            return why.NullOrEmpty() ? tip : tip + "\n\n" + why;
        }

        /// <summary>The paragraph for the readout's tooltip: the numbers behind the share, and hard times easing it.</summary>
        public static string Why(Pawn p)
        {
            var r = ReadingOf(p);
            if (!On || r == null || r.takeBack <= 0f) return null;
            string why = "CC_GoodLife_Why".Translate(Whole(r.positives).Named("GOOD"), Whole(r.line).Named("LINE"), LevelLabel(p).Named("LEVEL"),
                Mathf.RoundToInt(GoodLifeMath.FirstStretch).Named("FIRST"), r.share.ToStringPercent().Named("SHARE"), p.Named("PAWN")).Resolve();
            if (r.hard > 0f) why += " " + "CC_GoodLife_Hard".Translate(p.Named("PAWN")).Resolve();
            return why;
        }
    }
}
