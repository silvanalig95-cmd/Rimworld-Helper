using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a small letdown or joy was, for its words and for the heart's page.</summary>
    public enum SmallKind
    {
        None,
        /// <summary>An everyday pressure (the same meal, the same work, someone away, a crowded room, half-built work).</summary>
        Pressure,
        /// <summary>A setback (an idea turned down, a vote lost, work wrecked, a sick friend...).</summary>
        Setback,
        /// <summary>A quiet trouble (a worry, a hard day to remember).</summary>
        Trouble,
        /// <summary>A nuisance (rot in the stores, soaked through, junk in the room).</summary>
        Nuisance,
        /// <summary>A friction between two people (an argument about the chores, a joke at their expense).</summary>
        Friction,
        /// <summary>A small good thing (a laugh with a friend, a varied table, a kindness).</summary>
        Joy
    }

    /// <summary>
    /// One small letdown or joy as it is felt in the moment: a short, small mood, in one row of the Mood tab with the others
    /// of its side ("small letdowns", "small joys"), each saying in the row's tooltip what it was. The memory that brought it
    /// stays on as it was for what else it does (an opinion of someone, what other systems look for), without a mood.
    /// </summary>
    public class Thought_SmallMoment : Thought_Memory, ICouncilMood, IExplainedThought
    {
        /// <summary>The memory or lasting mood it was (its feelings, its hint, its kind lately for growing ordinary).</summary>
        public ThoughtDef what;
        public SmallKind kind;
        /// <summary>Its own label and cause, in words as they were when it happened.</summary>
        public string label, why;
        /// <summary>The moment's mood (SmallMomentMath.MomentMood).</summary>
        public float size;
        /// <summary>Who it was about, if anyone.</summary>
        public Pawn about;
        /// <summary>How many of the same may be on their mind at once (the memory's own stack limit), as it was given.</summary>
        [Unsaved] public int sameLimit = 1;

        public float OwnOffset() => size;

        public bool Scaled
        {
            set { }
        }

        public override float MoodOffset()
        {
            if (pawn == null || ThoughtUtility.ThoughtNullified(pawn, def)) return 0f;
            return size;
        }

        public override bool VisibleInNeedsTab => base.VisibleInNeedsTab && size != 0f;

        public override string Description => SmallMoments.Describe(pawn, def) ?? base.Description;

        public string Why => SmallMoments.WhatTheyWere(pawn, def);

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            showBubble = false;
            return SmallMoments.Merge(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref what, "what");
            Scribe_Values.Look(ref kind, "kind", SmallKind.None);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref size, "size");
            Scribe_References.Look(ref about, "about", saveDestroyedThings: true);
        }
    }

    /// <summary>
    /// Small letdowns and small joys (phase 3 of the review, the player's decision of 6 November 2026: "the letdowns also
    /// make sense as mood impact, no? But I like strain too though!", and the good side the same way). Rimfolk's five
    /// kinds of small letdown (pressures, setbacks, quiet troubles, nuisances, frictions) and its small good moments (a
    /// moment's kindness, a laugh, a varied table: Rimfolk's good memories of a few points and a day or two that no council
    /// answer gave) are each felt as a short, small mood at the moment they happen (SmallMomentMath: at most 3 points,
    /// half a day), all of a side in one row of the Mood tab. What each would have weighed beyond its moment goes to the
    /// heart: strain for a letdown (StrainSource.Letdowns), relief for a joy (StrainRelief.Joys), within the heart's caps,
    /// so a run of them wears or eases the heart while a single one passes. A lasting one (the same meal again, a wish
    /// still waiting, a varied table) gives no mood of its own any more: a moment when it begins, gets worse or holds
    /// another day, and strain or relief each hour while it holds.
    /// <para>
    /// Every small mood on a colonist's mind counts toward the heart by the same rule, whoever made it: the game's and
    /// other mods' too, by size and length (SmallMomentMath.IsSmall), when they pile up past a few points. Their moods
    /// stay exactly as the game and those mods made them; only Rimfolk's own became smaller, and only Rimfolk's share a
    /// row. The heavy kinds that have strain sources of their own (losses, bad nights, hunger, pain, the dark, the
    /// council's hard choices, gatherings and talks, the feelings that wear) are left to those, so nothing counts twice.
    /// </para>
    /// <para>
    /// Off (the heart off, the deeper game hooks off or "Advisor only"), every letdown and joy is a mood of its own as before.
    /// Memories given before this keep their moods and run out as they were.
    /// </para>
    /// </summary>
    public static class SmallMoments
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static ThoughtDef letdownsDef, joysDef;
        private static bool looked;

        public static ThoughtDef LetdownsDef
        {
            get
            {
                LookUp();
                return letdownsDef;
            }
        }

        public static ThoughtDef JoysDef
        {
            get
            {
                LookUp();
                return joysDef;
            }
        }

        private static void LookUp()
        {
            if (looked) return;
            looked = true;
            letdownsDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_SmallLetdowns");
            joysDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_SmallJoys");
        }

        public static bool On => RimfolkPatches.On && S != null && S.MoodEffects && Heart.On && LetdownsDef != null && JoysDef != null;

        /// <summary>Who feels them this way: the colony's free people with an inner life (as for pressures and setbacks).</summary>
        public static bool Eligible(Pawn p) => Pressures.Eligible(p);

        public static void ResetCaches()
        {
            looked = false;
            kinds.Clear();
            heavy.Clear();
        }

        // ---------------- What counts ----------------

        private static readonly Dictionary<ThoughtDef, SmallKind> kinds = new Dictionary<ThoughtDef, SmallKind>();

        /// <summary>Which kind of small letdown this kind of thought is (None if it isn't one).</summary>
        public static SmallKind LetdownKind(ThoughtDef def)
        {
            if (def == null) return SmallKind.None;
            if (kinds.TryGetValue(def, out var k)) return k;
            k = SmallKind.None;
            if (GoodLife.IsRimfolk(def) && def != LetdownsDef && def != JoysDef)
            {
                var cls = def.ThoughtClass;
                string name = def.defName;
                if (typeof(Thought_Pressure).IsAssignableFrom(cls)) k = SmallKind.Pressure;
                else if (typeof(Thought_Trouble).IsAssignableFrom(cls)) k = SmallKind.Trouble;
                else if (name.StartsWith("Council_Friction_", StringComparison.Ordinal)) k = SmallKind.Friction;
                else if (name.StartsWith("Council_Nuisance_", StringComparison.Ordinal) || name.StartsWith("Rimfolk_Nuisance_", StringComparison.Ordinal)) k = SmallKind.Nuisance;
                else if (Setbacks.Ext(def) is SetbackExtension ext) k = ext.group == SetbackGroup.Friction ? SmallKind.Friction : SmallKind.Setback;
                else if (typeof(Thought_Setback).IsAssignableFrom(cls)) k = SmallKind.Setback;
            }
            kinds[def] = k;
            return k;
        }

        /// <summary>A good memory as written is small: a few points at most, two days at most.</summary>
        public const float JoyWrittenMost = 5f, JoyWrittenDays = 2f;

        /// <summary>
        /// A small good moment of Rimfolk's: one of its memories that no council answer gave (a moment, a talk, a kindness,
        /// scaled as it's felt), small as written, and not a life goal reached or something that gives no mood.
        /// </summary>
        public static bool JoySource(Thought_Memory m)
        {
            var def = m?.def;
            if (def == null || m is Thought_SmallMoment || m is Thought_Dream || !GoodLife.IsRimfolk(def) || LetdownKind(def) != SmallKind.None) return false;
            if (Milestone.Is(def) || RewardInstead.On(def) || def.durationDays <= 0f || def.durationDays > JoyWrittenDays || def.stages.NullOrEmpty()) return false;
            bool unscaled = m is Thought_CouncilMemory cm ? !cm.scaled : m is Thought_CouncilSocial cs && !cs.scaled;
            if (!unscaled) return false;
            float most = 0f;
            foreach (var st in def.stages)
                if (st != null)
                    most = Mathf.Max(most, st.baseMoodEffect);
            return most > 0f && most <= JoyWrittenMost;
        }

        /// <summary>A memory whose mood went to a moment (it stays on without one): the heart leaves it to the moment.</summary>
        public static bool Converted(Thought_Memory m) =>
            m != null && m.moodPowerFactor == 0f && !(m is Thought_SmallMoment) && (LetdownKind(m.def) != SmallKind.None || JoySource(m));

        // ---------------- The moment ----------------

        /// <summary>
        /// A memory is about to be gained (Patch_GoodTimesFade): a small letdown or joy of Rimfolk's becomes a moment, and
        /// the memory itself goes on without a mood.
        /// </summary>
        public static void Take(MemoryThoughtHandler handler, Thought_Memory t, Pawn other)
        {
            var p = handler?.pawn;
            if (p == null || t?.def == null || t is Thought_SmallMoment || t.permanent || !On || !Eligible(p)) return;
            var kind = LetdownKind(t.def);
            bool joy = kind == SmallKind.None && JoySource(t);
            if (kind == SmallKind.None && !joy) return;
            if (!ThoughtUtility.CanGetThought(p, t.def)) return;
            // Whose it is and whom it's about, as the game sets them next, so its size and words read right.
            var about = t is Thought_MemorySocial ? other ?? t.otherPawn : other;
            // As the game would refuse it: a memory about someone needs them, of an age it is for.
            if (t is Thought_MemorySocial && (about == null || !t.def.socialTargetDevelopmentalStageFilter.Has(about.DevelopmentalStage))) return;
            t.pawn = p;
            t.otherPawn = about;
            float felt;
            string label, why;
            try
            {
                felt = t.MoodOffset();
                label = t.LabelCap;
                why = (t as ICausedThought)?.Cause ?? (t as IExplainedThought)?.Why;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small moment " + t.def.defName, e);
                return;
            }
            if (float.IsNaN(felt) || (joy ? felt <= 0f : felt >= 0f)) return;
            Give(p, joy ? SmallKind.Joy : kind, t.def, felt, t.DurationTicks / (float)GenDate.TicksPerDay, label, why, about == p ? null : about, t.def.stackLimit, true);
            t.moodPowerFactor = 0f;
        }

        /// <summary>
        /// Gives the moment, and (<paramref name="heart"/>) what it weighs beyond it to the heart: strain for a letdown,
        /// relief for a joy. A lasting one's heart part comes by the hour instead.
        /// </summary>
        public static void Give(Pawn p, SmallKind kind, ThoughtDef what, float felt, float writtenDays, string label, string why, Pawn about, int sameLimit, bool heart)
        {
            var def = kind == SmallKind.Joy ? JoysDef : LetdownsDef;
            var memories = p?.needs?.mood?.thoughts?.memories;
            if (def == null || memories == null) return;
            try
            {
                if (!(ThoughtMaker.MakeThought(def) is Thought_SmallMoment m)) return;
                m.what = what;
                m.kind = kind;
                m.size = SmallMomentMath.MomentMood(felt);
                m.label = label.NullOrEmpty() ? what?.LabelCap.ToString() : label;
                m.why = why;
                m.about = about;
                m.sameLimit = Math.Max(1, sameLimit);
                int ticks = Mathf.Max(1, Mathf.RoundToInt(SmallMomentMath.MomentLength(writtenDays) * GenDate.TicksPerDay));
                if (ticks != def.DurationTicks) m.durationTicksOverride = ticks;
                memories.TryGainMemory(m);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small moment " + what?.defName, e);
                return;
            }
            if (!heart) return;
            if (kind == SmallKind.Joy) HeartLogic.EaseJoys(p, SmallMomentMath.MomentRelief(felt, writtenDays), label);
            else HeartLogic.Build(p, StrainSource.Letdowns, SmallMomentMath.MomentStrain(felt, writtenDays), label);
        }

        /// <summary>
        /// The same small thing again: while as many of it as its memory allows are on their mind already, the oldest starts
        /// over instead of another joining them (as the game renews a memory), with the newer words and the bigger size.
        /// </summary>
        public static bool Merge(Thought_SmallMoment fresh)
        {
            var list = fresh?.pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (list == null) return false;
            int same = 0;
            Thought_SmallMoment oldest = null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Thought_SmallMoment m && m != fresh && m.def == fresh.def && m.what == fresh.what && m.about == fresh.about)
                {
                    same++;
                    if (oldest == null || m.age > oldest.age) oldest = m;
                }
            if (oldest == null || same < fresh.sameLimit) return false;
            oldest.age = 0;
            oldest.size = Mathf.Abs(fresh.size) >= Mathf.Abs(oldest.size) ? fresh.size : oldest.size;
            oldest.label = fresh.label;
            oldest.why = fresh.why ?? oldest.why;
            oldest.durationTicksOverride = fresh.durationTicksOverride;
            return true;
        }

        // ---------------- Lasting ones and everyone's small moods, by the hour ----------------

        /// <summary>A lasting letdown or joy as the hour finds it.</summary>
        public struct Held
        {
            public Thought_Situational thought;
            public float felt;
            public SmallKind kind;
        }

        private static readonly List<Thought_Situational> heldTmp = new List<Thought_Situational>();

        /// <summary>The lasting letdowns and joys holding for them now (a pressure, a lasting setback), with their mood as felt.</summary>
        public static void Lasting(Pawn p, List<Held> into)
        {
            into.Clear();
            var list = GoodLife.SituationalOf(p?.needs?.mood?.thoughts?.situational);
            if (list == null) return;
            heldTmp.Clear();
            heldTmp.AddRange(list);
            foreach (var t in heldTmp)
            {
                if (t == null || !t.Active) continue;
                float felt;
                SmallKind kind;
                try
                {
                    if (t is Thought_Pressure tp)
                    {
                        felt = tp.Felt();
                        kind = felt < 0f ? SmallKind.Pressure : SmallKind.Joy;
                    }
                    else if (t is Thought_Setback ts)
                    {
                        felt = ts.Felt();
                        kind = SmallKind.Setback;
                        if (felt >= 0f) continue;
                    }
                    else continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("small moment lasting " + t.def?.defName, e);
                    continue;
                }
                if (felt == 0f || float.IsNaN(felt)) continue;
                into.Add(new Held { thought = t, felt = felt, kind = kind });
            }
            heldTmp.Clear();
        }

        private static readonly List<Held> held = new List<Held>();
        private static readonly List<ThoughtDef> endedTmp = new List<ThoughtDef>();

        /// <summary>
        /// The hour's part (HeartLogic.Strain): each lasting letdown or joy wears or eases the heart while it holds and comes
        /// back as a moment when it begins, gets worse or holds another day; then every small mood on their mind, anyone's,
        /// once they pile up. Strain goes through <paramref name="build"/>; the lasting joys' relief is returned, and the
        /// pile's (every small good mood, the game's comforts among them) in <paramref name="comforts"/>: the hour eases
        /// strain with both and fills the heart's good side with what is beyond, the pile's only a little.
        /// </summary>
        public static float Hour(Pawn p, PawnHeart h, List<Thought> moodGroups, float hours, int now, Func<StrainSource, float, string, float> build, out float comforts)
        {
            comforts = 0f;
            if (!On || !Eligible(p)) return 0f;
            float relief = 0f;
            try
            {
                Lasting(p, held);
                h.lasting ??= new Dictionary<ThoughtDef, PawnHeart.LastingSeen>();
                foreach (var x in held)
                {
                    var t = x.thought;
                    bool joy = x.kind == SmallKind.Joy;
                    string label = SafeLabel(t);
                    float per = SmallMomentMath.LastingPerHour(x.felt, joy) * hours;
                    if (joy) relief += HeartLogic.JoysWithinCap(h, per, label);
                    else build(StrainSource.Letdowns, per, label);
                    int stage = t.CurStageIndex;
                    bool had = h.lasting.TryGetValue(t.def, out var was);
                    // Just after a load, what holds already is taken as seen: no moment for it then.
                    if (!h.lastingLooked)
                    {
                        h.lasting[t.def] = new PawnHeart.LastingSeen { stage = stage, tick = now };
                        continue;
                    }
                    float days = had ? (now - was.tick) / (float)GenDate.TicksPerDay : float.NaN;
                    if (SmallMomentMath.MomentDue(stage, had ? was.stage : -1, days))
                    {
                        Give(p, x.kind, t.def, x.felt, -1f, label, SafeWhy(t), null, 1, false);
                        h.lasting[t.def] = new PawnHeart.LastingSeen { stage = stage, tick = now };
                    }
                    else if (stage < was.stage) h.lasting[t.def] = new PawnHeart.LastingSeen { stage = stage, tick = was.tick };
                }
                endedTmp.Clear();
                foreach (var k in h.lasting.Keys)
                    if (!held.Any(x => x.thought.def == k))
                        endedTmp.Add(k);
                foreach (var k in endedTmp) h.lasting.Remove(k);
                endedTmp.Clear();
                h.lastingLooked = true;
                held.Clear();

                // Every small mood on their mind, anyone's, by the same rule.
                var thoughts = p.needs.mood.thoughts;
                float bad = 0f, good = 0f, worst = 0f, best = 0f;
                string worstLabel = null, bestLabel = null;
                for (int i = 0; i < moodGroups.Count; i++)
                {
                    var g = moodGroups[i];
                    if (g?.def == null) continue;
                    bool moment = g is Thought_SmallMoment;
                    if (!moment && Heavy(g.def)) continue;
                    float off;
                    try
                    {
                        off = thoughts.MoodOffsetOfGroup(g);
                    }
                    catch
                    {
                        continue;
                    }
                    float lasts = g is Thought_Memory m ? m.permanent ? -1f : m.DurationTicks / (float)GenDate.TicksPerDay : -1f;
                    if (!moment && !SmallMomentMath.IsSmall(off, lasts)) continue;
                    if (off < 0f)
                    {
                        bad -= off;
                        if (-off > worst)
                        {
                            worst = -off;
                            worstLabel = SafeLabel(g);
                        }
                    }
                    else if (off > 0f)
                    {
                        good += off;
                        if (off > best)
                        {
                            best = off;
                            bestLabel = SafeLabel(g);
                        }
                    }
                }
                float strain = SmallMomentMath.PileStrainPerHour(bad) * hours;
                if (strain > 0f) build(StrainSource.Letdowns, strain, worstLabel);
                float eased = SmallMomentMath.JoyReliefPerHour(good) * hours;
                if (eased > 0f) comforts += HeartLogic.JoysWithinCap(h, eased, bestLabel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small moments, the hour", e);
                held.Clear();
            }
            return relief;
        }

        private static readonly Dictionary<ThoughtDef, bool> heavy = new Dictionary<ThoughtDef, bool>();

        /// <summary>
        /// A mood the heart already counts by a source of its own (HeartLogic's memories: losses, bad nights, the
        /// council's hard choices, gatherings and talks; hunger, rest and recreation, pain, the dark; the heart's own
        /// strain; the feelings that wear), so the pile of small moods leaves it out.
        /// </summary>
        public static bool Heavy(ThoughtDef def)
        {
            if (def == null) return true;
            if (heavy.TryGetValue(def, out bool h)) return h;
            string n = def.defName;
            h = HeartLogic.HasRule(def) || def == HeartLogic.DarkThought || n == "NeedFood" || n == "NeedRest" || n == "NeedJoy" || n == "Pain"
                || n.StartsWith("Council_Heart_", StringComparison.Ordinal) || Feelings.FeedsWearingFeeling(def);
            heavy[def] = h;
            return h;
        }

        private static string SafeLabel(Thought t)
        {
            try
            {
                return t.LabelCap;
            }
            catch
            {
                return t.def?.label;
            }
        }

        private static string SafeWhy(Thought t)
        {
            try
            {
                return (t as IExplainedThought)?.Why;
            }
            catch
            {
                return null;
            }
        }

        // ---------------- Words ----------------

        private static readonly List<Thought_SmallMoment> rowTmp = new List<Thought_SmallMoment>();

        /// <summary>The moments of a side on their mind, biggest first.</summary>
        public static List<Thought_SmallMoment> Of(Pawn p, ThoughtDef def)
        {
            var result = new List<Thought_SmallMoment>();
            var list = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (list == null || def == null) return result;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Thought_SmallMoment m && m.def == def && !m.ShouldDiscard && m.MoodOffset() != 0f)
                    result.Add(m);
            result.Sort((a, b) => Mathf.Abs(b.size).CompareTo(Mathf.Abs(a.size)));
            return result;
        }

        /// <summary>
        /// The row's tooltip: what each one was, its mood and its cause, and in a line what happens beyond the moment.
        /// </summary>
        public static string Describe(Pawn p, ThoughtDef def)
        {
            if (p == null || def == null) return null;
            var all = Of(p, def);
            if (all.Count == 0) return null;
            bool joys = def == JoysDef;
            var lines = new List<string> { (joys ? "CC_Small_Joys_Intro" : "CC_Small_Letdowns_Intro").Translate(p.Named("PAWN")).Resolve() };
            foreach (var m in all)
            {
                string item = "CC_Small_Item".Translate((m.label ?? m.what?.label ?? "?").CapitalizeFirst().Named("LABEL"), MoodView.Number(m.size).Named("SIZE")).Resolve();
                if (!m.why.NullOrEmpty()) item += " " + m.why.Colorize(ColoredText.SubtleGrayColor);
                lines.Add(item);
            }
            lines.Add("");
            lines.Add((joys ? "CC_Small_Joys_Heart" : "CC_Small_Letdowns_Heart").Translate(p.Named("PAWN")).Resolve());
            return string.Join("\n", lines);
        }

        /// <summary>What they were, in a few words ("my idea turned down and rot in the stores"), for the row's cause.</summary>
        public static string WhatTheyWere(Pawn p, ThoughtDef def)
        {
            var all = Of(p, def);
            if (all.Count == 0) return null;
            var labels = all.Select(m => (m.label ?? m.what?.label ?? "?").ToLowerInvariant()).Distinct().Take(3).ToList();
            return TextUtil.JoinAnd(labels);
        }

        /// <summary>
        /// What helps, for the row (MoodInsight.Advice): the fixes of the letdowns behind it that can be fixed, else that
        /// they pass by themselves.
        /// </summary>
        public static string Advice(Pawn p, ThoughtDef def, bool bad)
        {
            var fixes = new List<string>();
            foreach (var m in Of(p, def))
            {
                var hint = m.what == null ? null : MoodInsight.HintFor(m.what);
                string text = hint == null ? null : bad ? hint.fix : hint.keep;
                if (text.NullOrEmpty() || fixes.Contains(text)) continue;
                if (bad && !hint.Actionable) continue;
                fixes.Add(text);
                if (fixes.Count >= 2) break;
            }
            if (fixes.Count > 0) return string.Join(" ", fixes);
            return null;
        }
    }
}
