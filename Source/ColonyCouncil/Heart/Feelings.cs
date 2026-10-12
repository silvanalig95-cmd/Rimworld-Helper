using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Works out a colonist's feelings once an hour: each FeelingDef adds up the mood thoughts it lists (as big as the
    /// game counts them), what its worker sees, a little strain for fear and grief, then traits. The ones above their
    /// threshold are felt; the strongest one or two stand out. Feelings never add mood of their own.
    /// </summary>
    public static class Feelings
    {
        private static List<FeelingDef> defs;
        private static int[] orderOf;
        /// <summary>Per thought: which feelings it feeds and how (exact names, patterns and tags; generic rules aside).</summary>
        private static readonly Dictionary<ThoughtDef, List<(int feeling, FeelingFeed feed, float weight)>> index = new Dictionary<ThoughtDef, List<(int, FeelingFeed, float)>>();
        private static readonly List<(int feeling, FeelingFeed feed)> generic = new List<(int, FeelingFeed)>();

        public static List<FeelingDef> All
        {
            get
            {
                if (defs == null) Build();
                return defs;
            }
        }

        public static void Reset()
        {
            defs = null;
            index.Clear();
            generic.Clear();
        }

        private static void Build()
        {
            defs = new List<FeelingDef>(DefDatabase<FeelingDef>.AllDefsListForReading);
            defs.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.defName, b.defName));
            index.Clear();
            generic.Clear();
            for (int i = 0; i < defs.Count; i++)
                foreach (var f in defs[i].AllFeeds)
                    if (f.Generic) generic.Add((i, f));
        }

        private static List<(int feeling, FeelingFeed feed, float weight)> FeedsOf(ThoughtDef def)
        {
            if (index.TryGetValue(def, out var list)) return list;
            list = null;
            string name = def.defName;
            for (int i = 0; i < defs.Count; i++)
                foreach (var f in defs[i].AllFeeds)
                    if (!f.Generic && f.NameMatches(name)) (list ??= new List<(int, FeelingFeed, float)>()).Add((i, f, 1f));
            // Tags on the thought itself (any mod can put them there).
            var tags = def.GetModExtension<HeartTags>();
            if (tags?.feelings != null)
                foreach (var t in tags.feelings)
                {
                    int i = defs.FindIndex(d => d.key == t || d.defName == t);
                    if (i < 0) continue;
                    var feed = new FeelingFeed { thought = name, sign = 0, weight = tags.weight };
                    (list ??= new List<(int, FeelingFeed, float)>()).Add((i, feed, 1f));
                }
            index[def] = list;
            return list;
        }

        // ---------------- The hour's work ----------------

        private class Eval : IFeelingSink
        {
            public FeelingNow now;

            public void Add(float amount, string cause, Pawn about = null)
            {
                if (amount <= 0f) return;
                now.intensity += amount;
                now.AddCause(cause, amount);
                if (about != null && amount > now.targetAmount)
                {
                    now.target = about;
                    now.targetAmount = amount;
                }
            }
        }

        private static readonly Eval sink = new Eval();
        private static FeelingNow[] work = new FeelingNow[0];
        private static float[] best = new float[0];

        public static void Compute(Pawn p, PawnHeart h, HeartMapContext m, List<Thought> moodGroups, int now, StrainTraits traits)
        {
            var all = All;
            int n = all.Count;
            if (work.Length != n)
            {
                work = new FeelingNow[n];
                best = new float[n];
            }
            for (int i = 0; i < n; i++) work[i] = new FeelingNow { def = all[i] };

            var thoughts = p.needs.mood.thoughts;
            for (int g = 0; g < moodGroups.Count; g++)
            {
                var group = moodGroups[g];
                if (group?.def == null) continue;
                float off;
                try
                {
                    off = thoughts.MoodOffsetOfGroup(group);
                }
                catch
                {
                    continue;
                }
                if (Mathf.Abs(off) < 0.01f) continue;
                var feeds = FeedsOf(group.def);
                if (feeds == null && generic.Count == 0) continue;
                Pawn other = OtherOf(group, p);
                for (int i = 0; i < n; i++) best[i] = 0f;
                if (feeds != null)
                    foreach (var (fi, feed, _) in feeds)
                        if (Holds(feed, off, other, p)) best[fi] = Mathf.Max(best[fi], feed.weight);
                foreach (var (fi, feed) in generic)
                    if (Holds(feed, off, other, p)) best[fi] = Mathf.Max(best[fi], feed.weight);
                string label = null;
                bool council = CouncilDidIt(group.def);
                for (int i = 0; i < n; i++)
                {
                    if (best[i] <= 0f) continue;
                    float amount = Mathf.Abs(off) * best[i];
                    var f = work[i];
                    f.intensity += amount;
                    label ??= LabelOf(group);
                    f.AddCause(label, amount);
                    if (other != null && amount > f.targetAmount)
                    {
                        f.target = other;
                        f.targetAmount = amount;
                    }
                    if (council) f.fromCouncil = true;
                }
            }

            // What thoughts can't say.
            for (int i = 0; i < n; i++)
            {
                var worker = all[i].Worker;
                if (worker == null) continue;
                sink.now = work[i];
                try
                {
                    worker.Feed(p, h, m, sink);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feeling " + all[i].defName, e);
                }
            }
            sink.now = null;

            // Who they are, and how worn down.
            for (int i = 0; i < n; i++)
            {
                var d = all[i];
                var f = work[i];
                if (f.intensity <= 0f) continue;
                if (d.immuneTraits != null)
                    foreach (var t in d.immuneTraits)
                        if (Heart.HasTrait(p, t))
                        {
                            f.intensity = 0f;
                            break;
                        }
                if (f.intensity <= 0f) continue;
                if (d.traitFactors != null)
                    foreach (var tf in d.traitFactors)
                        if (tf != null && Heart.HasTrait(p, tf.trait, tf.degree)) f.intensity *= tf.factor;
                if (d.fromStrain > 0f && h.strain >= 30f)
                {
                    float add = h.strain * d.fromStrain;
                    f.intensity += add;
                    f.AddCause("CC_Heart_Cause_Strain".Translate(), add);
                }
            }
            // One feeling easing another (hope eases despair, love eases loneliness).
            for (int i = 0; i < n; i++)
            {
                var c = all[i].counters;
                if (c == null || work[i].intensity <= 0f) continue;
                int j = all.IndexOf(c);
                if (j >= 0) work[j].intensity = Mathf.Max(0f, work[j].intensity - work[i].intensity * all[i].counterAmount);
            }

            // What's felt, and since when.
            h.feelings.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = all[i];
                var f = work[i];
                int was = h.SinceOf(d, -1);
                float threshold = was >= 0 ? d.threshold * 0.75f : d.threshold;
                if (f.intensity < threshold || f.intensity <= 0f)
                {
                    if (was >= 0) h.ClearSince(d);
                    continue;
                }
                if (was < 0)
                {
                    was = now;
                    h.SetSince(d, now);
                }
                f.since = was;
                if (f.target != null && (f.target.Dead || f.target == p || !f.target.RaceProps.Humanlike)) f.target = null;
                h.feelings.Add(f);
            }
            h.feelings.Sort((a, b) => b.intensity.CompareTo(a.intensity));
            // Forget the "since" of feelings that no longer exist.
            for (int k = h.feelingSinceKeys.Count - 1; k >= 0; k--)
                if (DefDatabase<FeelingDef>.GetNamedSilentFail(h.feelingSinceKeys[k]) == null)
                {
                    h.feelingSinceKeys.RemoveAt(k);
                    h.feelingSinceTicks.RemoveAt(k);
                }
        }

        /// <summary>The council's own doing: a wish granted, a promise kept, a petition heeded, someone brought home.</summary>
        private static readonly string[] CouncilDeeds =
        {
            "Council_Granted_", "Council_PromiseKept", "Council_SpokeUpForMe", "Council_DecreeHeeded", "Council_ObjectionHeard",
            "Council_Dlm_Understood", "Council_Secret2_Protected", "Council_Secret2_HeardOut", "Council_Secret2_DebtPaid", "Council_BroughtHome"
        };

        private static readonly Dictionary<ThoughtDef, bool> councilDeed = new Dictionary<ThoughtDef, bool>();

        /// <summary>Whether the thought is one of the council's deeds, worked out once per thought.</summary>
        private static bool CouncilDidIt(ThoughtDef def)
        {
            if (councilDeed.TryGetValue(def, out bool did)) return did;
            string name = def.defName ?? "";
            for (int i = 0; i < CouncilDeeds.Length && !did; i++)
                if (name.StartsWith(CouncilDeeds[i], StringComparison.Ordinal)) did = true;
            councilDeed[def] = did;
            return did;
        }

        private static bool Holds(FeelingFeed feed, float off, Pawn other, Pawn p)
        {
            if (feed.sign < 0 && off >= 0f) return false;
            if (feed.sign > 0 && off <= 0f) return false;
            if (feed.aboutSomeone && (other == null || other.Dead)) return false;
            if (feed.opinionBelow != int.MaxValue || feed.opinionAbove != int.MinValue)
            {
                if (other == null || p.relations == null) return false;
                int op = p.relations.OpinionOf(other);
                if (op >= feed.opinionBelow || op <= feed.opinionAbove) return false;
            }
            return true;
        }

        /// <summary>The person a thought is about, if it's about another living person.</summary>
        private static Pawn OtherOf(Thought t, Pawn p)
        {
            Pawn other = (t as Thought_Memory)?.otherPawn;
            if (other == null && t is ISocialThought social)
            {
                try
                {
                    other = social.OtherPawn();
                }
                catch
                {
                    other = null;
                }
            }
            if (other == null || other == p || other.RaceProps == null || !other.RaceProps.Humanlike) return null;
            return other;
        }

        private static string LabelOf(Thought t)
        {
            try
            {
                string label = t.LabelCap;
                return label.NullOrEmpty() ? t.def.defName : label;
            }
            catch
            {
                return t.def.defName;
            }
        }
    }
}
