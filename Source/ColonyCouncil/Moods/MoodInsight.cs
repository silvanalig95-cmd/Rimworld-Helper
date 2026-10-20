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
        /// <summary>
        /// The fix in a few words, for the Mood tab's compact rows ("Build a dining table", "Passes with time"): an
        /// imperative of 2 to 6 words, at most <see cref="MoodSortMath.MaxActLength"/> characters, no full stop.
        /// </summary>
        [MustTranslate] public string act;
        /// <summary>
        /// Whether something done now ends this mood or keeps it from coming straight back (a table, a light, a decree
        /// repealed), so it counts under "You can fix". False for moods that only fade (grief, a council answer, a story
        /// gone badly); those count under "Will pass". Only matters with a <see cref="fix"/>.
        /// </summary>
        public bool actionable = true;
        public AlmanacArticleDef article;
        public AdvisorSignalDef signal;
        /// <summary>A page of the council window that deals with it: Decrees, Wishes, Ways, Bonds, Roster...</summary>
        public string councilTab;
        public int priority;

        /// <summary>Whether acting now helps with this mood (see <see cref="actionable"/>): a fix that says so.</summary>
        public bool Actionable => actionable && !fix.NullOrEmpty();

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
            // The compact row has one line for it (the tests hold the English text to its length and words; a translation may run longer).
            if (act != null && (act.Trim().Length == 0 || act.IndexOf('\n') >= 0)) yield return "act must be one line of text";
            if (thoughts.NullOrEmpty() && workers.NullOrEmpty() && thoughtClasses.NullOrEmpty() && nameContains.NullOrEmpty() && mods.NullOrEmpty())
                yield return "mood hint matches nothing";
            if (nameContains != null && nameContains.Any(c => c != c.ToLowerInvariant())) yield return "nameContains must be lowercase";
            // The "Go to ..." button under the hint must lead somewhere.
            if (!councilTab.NullOrEmpty() && !MainTabWindow_Council.IsPage(councilTab)) yield return "councilTab " + councilTab + " is not a page of the council window";
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
        /// <summary>One of them was given while life was already good, and fades sooner for it (GoodTimes).</summary>
        public bool sooner;
        public string description;
        /// <summary>Its text, worked out once (see MoodView).</summary>
        public MoodView.Texts texts;
    }

    /// <summary>One kind of mood across the colony, the good and the bad kept apart.</summary>
    public class ColonyMoodLine
    {
        public ThoughtDef def;
        public bool good;
        public string label;
        public float total;
        public List<MoodLine> lines = new List<MoodLine>();
        /// <summary>Each colonist once, with everything this kind of mood does to them.</summary>
        public Dictionary<Pawn, float> byPawn = new Dictionary<Pawn, float>();
        public MoodLine sample;
        public MoodView.Texts texts;
    }

    /// <summary>A colonist's mood at a glance, worked out once per refresh.</summary>
    public class PawnMood
    {
        public Pawn pawn;
        public float now, heading, minor, major, extreme;
        public string state;
        public string worst;
        public string sources;
        public List<MoodLine> lines;
    }

    /// <summary>
    /// Reads what's behind every colonist's mood: each mood with its size, why it's there, when it fades, which mod
    /// it comes from, and what can be done about it.
    /// </summary>
    public static class MoodInsight
    {
        private static Dictionary<ThoughtDef, MoodHintDef> hints;
        private static readonly Dictionary<ThoughtDef, (MoodSource, string)> sources = new Dictionary<ThoughtDef, (MoodSource, string)>();
        private static readonly Dictionary<Type, bool> lastsWhileCause = new Dictionary<Type, bool>();
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

        /// <summary>Everyone whose mood the colony looks after (not those asleep in caskets or pods).</summary>
        public static List<Pawn> Colonists() =>
            PawnsFinder.AllMaps_FreeColonists.Where(p => p.needs?.mood?.thoughts != null && !p.IsQuestLodger() && !p.Dead && !p.Suspended).Distinct().ToList();

        /// <summary>A colonist's moods, worst first.</summary>
        public static List<MoodLine> LinesFor(Pawn p)
        {
            var result = new List<MoodLine>();
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null) return result;
            try
            {
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
                        // Moods the game itself keeps out of the Needs tab stay out of ours too.
                        if (group?.def == null || !group.VisibleInNeedsTab || group.CurStage == null) continue;
                        float mood = handler.MoodOffsetOfGroup(group);
                        if (Mathf.Abs(mood) < 0.05f) continue;
                        tmpThoughts.Clear();
                        handler.GetMoodThoughts(group, tmpThoughts);
                        string label = group.CurStage.label.NullOrEmpty() ? group.def.label : group.LabelCap;
                        result.Add(new MoodLine
                        {
                            pawn = p,
                            thought = group,
                            def = group.def,
                            label = label.NullOrEmpty() ? group.def.defName : label.CapitalizeFirst(),
                            mood = mood,
                            count = Mathf.Max(1, tmpThoughts.Count),
                            why = Why(group),
                            ticksLeft = TicksLeft(tmpThoughts),
                            sooner = AnySooner(tmpThoughts)
                        });
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("mood line " + group?.def?.defName, e);
                    }
                }
            }
            finally
            {
                // Don't keep thoughts (and through them, the whole game) alive between calls.
                tmpGroups.Clear();
                tmpThoughts.Clear();
            }
            return result.OrderBy(l => l.mood).ToList();
        }

        /// <summary>A colonist's mood at a glance: levels, a sentence, the worst moods and where they come from.</summary>
        public static PawnMood MoodOf(Pawn p, List<MoodLine> lines = null)
        {
            var m = new PawnMood { pawn = p, lines = lines ?? LinesFor(p) };
            var need = p?.needs?.mood;
            if (need == null) return m;
            m.now = need.CurLevelPercentage;
            m.heading = m.now;
            try
            {
                m.heading = Mathf.Clamp01(need.CurInstantLevel);
            }
            catch
            {
            }
            var breaker = p.mindState?.mentalBreaker;
            m.minor = 0.35f;
            m.major = 0.2f;
            m.extreme = 0.05f;
            try
            {
                if (breaker != null)
                {
                    m.minor = breaker.BreakThresholdMinor;
                    m.major = breaker.BreakThresholdMajor;
                    m.extreme = breaker.BreakThresholdExtreme;
                }
            }
            catch
            {
            }
            m.state = MoodView.State(m);
            var worst = m.lines.Where(l => l.mood < 0f).Take(3).Select(l => l.label + " " + MoodView.Number(l.mood)).ToList();
            m.worst = worst.Count > 0 ? string.Join(",  ", worst) : "CC_Mood_Nothing".Translate().ToString();
            m.sources = MoodView.Sources(m.lines, 1);
            return m;
        }

        /// <summary>The thought's own description, worked out only when asked (it can be slow for some mods).</summary>
        public static string Description(MoodLine l)
        {
            if (l.description == null)
            {
                try
                {
                    l.description = l.thought.Description ?? "";
                    // A comfort from the game or another mod that the colonist's standards make count for less.
                    string comfort = GoodTimes.ComfortNote(l.pawn, l.thought, l.mood);
                    if (!comfort.NullOrEmpty()) l.description = l.description.NullOrEmpty() ? comfort : l.description.TrimEnd() + "\n\n" + comfort;
                }
                catch
                {
                    l.description = l.description ?? "";
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
                // Some memories end by their own rules (renewed while a weapon is held, a harmonizer nearby...):
                // they last while their cause does, whatever their clock says.
                if (LastsWhileCause(m.GetType())) return -1;
                left = Mathf.Max(left, GoodTimes.MoodTicks(m) - m.age);
            }
            return left;
        }

        private static bool AnySooner(List<Thought> thoughts)
        {
            foreach (var t in thoughts)
                if (t is Thought_Memory m && GoodTimes.FadingSooner(m))
                    return true;
            return false;
        }

        private static bool LastsWhileCause(Type type)
        {
            if (!lastsWhileCause.TryGetValue(type, out bool own))
            {
                var decl = type.GetProperty("ShouldDiscard")?.GetGetMethod()?.DeclaringType;
                own = decl != null && decl != typeof(Thought_Memory) && decl != typeof(Thought_MemorySocial) && decl.Assembly != typeof(MoodInsight).Assembly;
                lastsWhileCause[type] = own;
            }
            return own;
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

        /// <summary>What to do about it (or how to keep it).</summary>
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
            if (l.ticksLeft >= 0) return (l.sooner ? "CC_Mood_FadesInSooner" : "CC_Mood_FadesIn").Translate(TextUtil.DaysString(l.ticksLeft));
            return "CC_Mood_WhileCause".Translate();
        }

        /// <summary>
        /// Every mood in the colony by kind, the good and the bad apart (a fine bedroom for one and an awful one for
        /// another are two lines), each colonist counted once.
        /// </summary>
        public static List<ColonyMoodLine> Colony(IEnumerable<PawnMood> pawns)
        {
            var byKey = new Dictionary<(ThoughtDef, bool), ColonyMoodLine>();
            foreach (var pm in pawns)
            {
                foreach (var l in pm.lines)
                {
                    var key = (l.def, l.mood > 0f);
                    if (!byKey.TryGetValue(key, out var c)) byKey[key] = c = new ColonyMoodLine { def = l.def, good = l.mood > 0f };
                    c.total += l.mood;
                    c.lines.Add(l);
                    c.byPawn[l.pawn] = (c.byPawn.TryGetValue(l.pawn, out float had) ? had : 0f) + l.mood;
                    if (c.sample == null || Mathf.Abs(l.mood) > Mathf.Abs(c.sample.mood)) c.sample = l;
                }
            }
            foreach (var c in byKey.Values)
            {
                // One label for everyone, unless the moods are about different people ("my friend Bob died").
                bool sameLabel = c.lines.All(l => l.label == c.lines[0].label);
                c.label = sameLabel || c.def.label.NullOrEmpty() ? c.sample.label : c.def.LabelCap.ToString();
            }
            return byKey.Values.ToList();
        }

        /// <summary>One mood group as a line, for a single tooltip (the Needs tab).</summary>
        public static MoodLine LineFor(Pawn p, Thought group)
        {
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null || group?.def == null) return null;
            var line = new MoodLine { pawn = p, thought = group, def = group.def, label = group.LabelCap, why = Why(group) };
            try
            {
                line.mood = handler.MoodOffsetOfGroup(group);
                tmpThoughts.Clear();
                handler.GetMoodThoughts(group, tmpThoughts);
                line.count = Mathf.Max(1, tmpThoughts.Count);
                line.ticksLeft = TicksLeft(tmpThoughts);
                line.sooner = AnySooner(tmpThoughts);
            }
            finally
            {
                tmpThoughts.Clear();
            }
            return line;
        }

        /// <summary>
        /// Rimfolk's lines for a mood in the game's own Needs tab tooltip, added under the game's text: which mod it's
        /// from, why (if the description doesn't already say), and what to do. The game's text already says when a
        /// memory expires, so that isn't repeated; a mood that lasts while its cause does says so.
        /// </summary>
        public static string HintLines(Pawn p, Thought group)
        {
            try
            {
                var l = LineFor(p, group);
                if (l == null) return "";
                var parts = new System.Collections.Generic.List<string>();
                // The game writes "Expires in ..." for memories with a duration; anything else gets ours.
                bool gameSaysWhen = group is Thought_Memory && group.def.DurationTicks > 5;
                string meta = gameSaysWhen ? null : WhenItEnds(l);
                var (kind, source) = SourceOf(l.def);
                if (kind != MoodSource.Game)
                {
                    string from = "CC_Mood_From".Translate(source).Resolve();
                    meta = meta == null ? from.CapitalizeFirst() : meta + "  ·  " + from;
                }
                if (!meta.NullOrEmpty()) parts.Add(meta.Colorize(ColoredText.SubtleGrayColor));
                // Rimfolk's own moods already say why in their description.
                if (!l.why.NullOrEmpty() && !(group is ICausedThought) && !(group is IExplainedThought)) parts.Add("CC_Cause_Line".Translate(l.why).Resolve());
                // Getting used to good times and standards: Rimfolk's own moods say so in their description already.
                if (!(group is ICouncilMood) && group is Thought_Memory memory)
                {
                    string fade = GoodTimes.FadeLine(memory);
                    if (fade != null) parts.Add(fade);
                }
                string comfort = GoodTimes.ComfortNote(p, group, l.mood);
                if (comfort != null) parts.Add(comfort);
                string advice = Advice(l);
                if (!advice.NullOrEmpty()) parts.Add((l.mood < 0f ? "CC_Mood_Fix" : "CC_Mood_Keep").Translate(advice).Resolve());
                return string.Join("\n\n", parts);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood hint tooltip", e);
                return "";
            }
        }

        public static void ResetCache()
        {
            hints = null;
        }
    }
}
