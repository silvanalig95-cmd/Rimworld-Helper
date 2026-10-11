using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "What can I do about it?" for a kind of mood, written in XML (Defs/MoodHintDefs). A hint matches moods by name,
    /// by the game class behind them (so modded moods built on a vanilla idea are covered too), by part of the name,
    /// or by the mod that adds them. The best match wins.
    /// </summary>
    public class MoodHintDef : Def
    {
        /// <summary>Exact ThoughtDef defNames.</summary>
        public List<string> thoughts;
        /// <summary>ThoughtWorker types (by name; subclasses count), e.g. ThoughtWorker_Dark.</summary>
        public List<string> workers;
        /// <summary>Thought types (by name; subclasses count), e.g. Thought_Decree.</summary>
        public List<string> thoughtClasses;
        /// <summary>Lowercase parts of the defName, e.g. "hygiene".</summary>
        public List<string> nameContains;
        /// <summary>Package ids: any mood from these mods.</summary>
        public List<string> mods;
        /// <summary>When it hurts: what to do about it.</summary>
        [MustTranslate] public string fix;
        /// <summary>When it helps: how to keep it, or get more of it.</summary>
        [MustTranslate] public string keep;
        public AlmanacArticleDef article;
        public AdvisorSignalDef signal;
        /// <summary>A page of the council window that deals with it: Decrees, Wishes, Ways, Bonds, Roster...</summary>
        public string councilTab;
        public int priority;

        public int Score(ThoughtDef t)
        {
            if (t == null) return 0;
            int best = 0;
            if (thoughts != null && thoughts.Contains(t.defName)) best = 1000;
            if (best < 600 && thoughtClasses != null && Is(t.ThoughtClass, thoughtClasses)) best = 600;
            if (best < 500 && workers != null && Is(t.workerClass, workers)) best = 500;
            if (best < 300 && nameContains != null)
            {
                string n = t.defName.ToLowerInvariant();
                if (nameContains.Any(c => n.Contains(c))) best = 300;
            }
            if (best < 100 && mods != null && t.modContentPack != null)
            {
                string id = t.modContentPack.PackageId.ToLowerInvariant();
                if (mods.Any(m => id == m || id == m + "_steam" || id == m + "_copy")) best = 100;
            }
            return best == 0 ? 0 : best + priority;
        }

        private static bool Is(Type type, List<string> names)
        {
            for (var x = type; x != null && x != typeof(object); x = x.BaseType)
                if (names.Contains(x.Name) || names.Contains(x.FullName)) return true;
            return false;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (fix.NullOrEmpty() && keep.NullOrEmpty()) yield return "mood hint has neither fix nor keep";
            if (thoughts.NullOrEmpty() && workers.NullOrEmpty() && thoughtClasses.NullOrEmpty() && nameContains.NullOrEmpty() && mods.NullOrEmpty())
                yield return "mood hint matches nothing";
            if (nameContains != null && nameContains.Any(c => c != c.ToLowerInvariant())) yield return "nameContains must be lowercase";
        }
    }

    public enum MoodSource
    {
        Game,
        Rimfolk,
        Mod
    }

    /// <summary>One mood a colonist has (several of the same kind count as one line).</summary>
    public class MoodLine
    {
        public Pawn pawn;
        public Thought thought;
        public ThoughtDef def;
        public string label;
        public float mood;
        public int count = 1;
        public string why;
        /// <summary>Ticks until the last of them fades; -1 while its cause lasts; -2 never.</summary>
        public int ticksLeft = -1;
        public string description;
    }

    /// <summary>One kind of mood across the colony.</summary>
    public class ColonyMoodLine
    {
        public ThoughtDef def;
        public string label;
        public float total;
        public List<MoodLine> lines = new List<MoodLine>();
        /// <summary>The strongest single case, whose label names the line ("ravenously hungry" over "hungry").</summary>
        public float strongest;
    }

    /// <summary>
    /// Reads what's behind every colonist's mood: each mood with its size, why it's there, when it fades, which mod
    /// it comes from, and what can be done about it.
    /// </summary>
    public static class MoodInsight
    {
        private static Dictionary<ThoughtDef, MoodHintDef> hints;
        private static readonly Dictionary<ThoughtDef, (MoodSource, string)> sources = new Dictionary<ThoughtDef, (MoodSource, string)>();
        private static readonly List<Thought> tmpGroups = new List<Thought>();
        private static readonly List<Thought> tmpThoughts = new List<Thought>();

        public static MoodHintDef HintFor(ThoughtDef def)
        {
            if (def == null) return null;
            if (hints == null) hints = new Dictionary<ThoughtDef, MoodHintDef>();
            if (!hints.TryGetValue(def, out var hint))
            {
                int best = 0;
                foreach (var h in DefDatabase<MoodHintDef>.AllDefsListForReading)
                {
                    int s = h.Score(def);
                    if (s > best)
                    {
                        best = s;
                        hint = h;
                    }
                }
                hints[def] = hint;
            }
            return hint;
        }

        /// <summary>Where a mood comes from: the base game (with its DLCs), Rimfolk, or another mod (by name).</summary>
        public static (MoodSource kind, string name) SourceOf(ThoughtDef def)
        {
            if (def == null) return (MoodSource.Game, null);
            if (!sources.TryGetValue(def, out var src))
            {
                var pack = def.modContentPack;
                if (pack == null || pack.IsOfficialMod) src = (MoodSource.Game, "CC_Mood_Source_Game".Translate().ToString());
                else if (pack.PackageId.ToLowerInvariant().StartsWith("silvanalig.colonycouncil")) src = (MoodSource.Rimfolk, "Rimfolk");
                else src = (MoodSource.Mod, pack.Name);
                sources[def] = src;
            }
            return src;
        }

        /// <summary>Everyone whose mood the colony looks after.</summary>
        public static List<Pawn> Colonists() =>
            PawnsFinder.AllMaps_FreeColonists.Where(p => p.needs?.mood?.thoughts != null && !p.IsQuestLodger() && !p.Dead).Distinct().ToList();

        /// <summary>A colonist's moods, worst first.</summary>
        public static List<MoodLine> LinesFor(Pawn p)
        {
            var result = new List<MoodLine>();
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null) return result;
            tmpGroups.Clear();
            try
            {
                handler.GetDistinctMoodThoughtGroups(tmpGroups);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood groups", e);
                return result;
            }
            foreach (var group in tmpGroups)
            {
                // One misbehaving modded mood shouldn't hide the rest.
                try
                {
                    if (group?.def == null) continue;
                    float mood = handler.MoodOffsetOfGroup(group);
                    if (Mathf.Abs(mood) < 0.05f) continue;
                    tmpThoughts.Clear();
                    handler.GetMoodThoughts(group, tmpThoughts);
                    result.Add(new MoodLine
                    {
                        pawn = p,
                        thought = group,
                        def = group.def,
                        label = group.LabelCap.NullOrEmpty() ? group.def.defName : group.LabelCap,
                        mood = mood,
                        count = Mathf.Max(1, tmpThoughts.Count),
                        why = Why(group),
                        ticksLeft = TicksLeft(tmpThoughts)
                    });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("mood line " + group?.def?.defName, e);
                }
            }
            return result.OrderBy(l => l.mood).ToList();
        }

        /// <summary>The thought's own description, worked out only when asked (it can be slow for some mods).</summary>
        public static string Description(MoodLine l)
        {
            if (l.description == null)
            {
                try
                {
                    l.description = l.thought.Description ?? "";
                }
                catch
                {
                    l.description = "";
                }
            }
            return l.description;
        }

        private static int TicksLeft(List<Thought> thoughts)
        {
            int left = -1;
            foreach (var t in thoughts)
            {
                if (!(t is Thought_Memory m)) return -1;
                if (m.permanent) return -2;
                left = Mathf.Max(left, m.DurationTicks - m.age);
            }
            return left;
        }

        /// <summary>Why the mood is there, if it can be said in a line.</summary>
        public static string Why(Thought t)
        {
            try
            {
                if (t is ICausedThought c && !c.Cause.NullOrEmpty()) return c.Cause;
                if (t is IExplainedThought x) return x.Why;
                if (t.sourcePrecept != null)
                    return "CC_Mood_Belief".Translate(t.sourcePrecept.LabelCap, t.sourcePrecept.ideo?.name ?? "?").Resolve();
                if (t is ISocialThought s && s.OtherPawn() != null) return "CC_Mood_About".Translate(s.OtherPawn().LabelShort).Resolve();
            }
            catch
            {
            }
            return null;
        }

        /// <summary>What to do about it (or how to keep it), and when it ends.</summary>
        public static string Advice(MoodLine l)
        {
            var hint = HintFor(l.def);
            bool bad = l.mood < 0f;
            string text = hint == null ? null : bad ? hint.fix : hint.keep;
            if (!text.NullOrEmpty()) return text;
            if (!bad) return null;
            if (l.thought?.sourcePrecept != null) return "CC_Mood_Generic_Precept".Translate();
            if (l.ticksLeft >= 0) return "CC_Mood_Generic_Memory".Translate();
            if (l.ticksLeft == -1) return "CC_Mood_Generic_Situation".Translate();
            return null;
        }

        public static string WhenItEnds(MoodLine l)
        {
            if (l.ticksLeft == -2) return "CC_Mood_Permanent".Translate();
            if (l.ticksLeft >= 0) return "CC_Mood_FadesIn".Translate(TextUtil.DaysString(l.ticksLeft));
            return "CC_Mood_WhileCause".Translate();
        }

        /// <summary>Every mood in the colony, by kind, with who has it.</summary>
        public static List<ColonyMoodLine> Colony(List<Pawn> pawns, Dictionary<Pawn, List<MoodLine>> perPawn)
        {
            var byDef = new Dictionary<ThoughtDef, ColonyMoodLine>();
            foreach (var p in pawns)
            {
                if (!perPawn.TryGetValue(p, out var lines)) perPawn[p] = lines = LinesFor(p);
                foreach (var l in lines)
                {
                    if (!byDef.TryGetValue(l.def, out var c)) byDef[l.def] = c = new ColonyMoodLine { def = l.def, label = l.label };
                    c.total += l.mood;
                    c.lines.Add(l);
                    if (Mathf.Abs(l.mood) > c.strongest)
                    {
                        c.strongest = Mathf.Abs(l.mood);
                        c.label = l.label;
                    }
                }
            }
            return byDef.Values.ToList();
        }

        /// <summary>A colonist's mood now, where it's heading, and the line where they might break.</summary>
        public static (float now, float heading, float minor, float major, float extreme) Levels(Pawn p)
        {
            var need = p?.needs?.mood;
            if (need == null) return (0f, 0f, 0f, 0f, 0f);
            float now = need.CurLevelPercentage, heading = now;
            try
            {
                heading = Mathf.Clamp01(need.CurInstantLevel);
            }
            catch
            {
            }
            var breaker = p.mindState?.mentalBreaker;
            return (now, heading, breaker?.BreakThresholdMinor ?? 0.35f, breaker?.BreakThresholdMajor ?? 0.2f, breaker?.BreakThresholdExtreme ?? 0.05f);
        }

        public static void ResetCache()
        {
            hints = null;
        }
    }
}
