using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Good times turn ordinary. Without taking anything away, two rules keep a happy colony from sitting at the top of
    /// the mood bar on a pile of good moods (the third, Rimfolk's good moods adding up slowly, is in SmallJoys.cs):
    /// <list type="bullet">
    /// <item>Getting used to good times: a colonist whose mood is already high keeps Rimfolk's new good memories for
    /// less time, in full up to 75% mood and down to half from 90%. In hard times good things last their full length.
    /// A memory about someone keeps its opinion for the full time; only its good mood ends sooner.</item>
    /// <item>Standards cover the game's comforts: comfortable, particular and spoiled colonists feel the good moods of the
    /// base game and other mods at 90%, 80% and 70%, on the first 10 points of each. Rimfolk's own already follow
    /// standards as they're given (CouncilGameComponent.GiveThought, SmallJoys.LiveScale).</item>
    /// </list>
    /// Both work through deeper game hooks (Patches/Patch_GoodTimes.cs), so a failed hook only switches its rule off.
    /// The arithmetic is in GoodTimesMath.
    /// </summary>
    public static class GoodTimes
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>
        /// While set, moods read as they would without these rules and without good moods adding up slowly: the Mood
        /// tab's readout and the tooltips use it to say how much they take off.
        /// </summary>
        public static bool Raw;

        public static bool FadeOn => RimfolkPatches.On && S != null && S.goodTimesFade;

        public static bool ComfortsOn => RimfolkPatches.On && S != null && S.standardsCoverComforts && S.risingStandards;

        /// <summary>A def from Rimfolk itself, not from the game or another mod.</summary>
        public static bool IsRimfolk(Def def) => def?.modContentPack != null && def.modContentPack == ColonyCouncilMod.Pack;

        // ---------------- Getting used to good times ----------------

        /// <summary>Returned when a memory being given isn't one of Rimfolk's good memories (nothing to do afterwards).</summary>
        public const int Untouched = int.MinValue;

        /// <summary>One of Rimfolk's good memories with a time of its own: what getting used to good times shortens (never a milestone).</summary>
        public static bool IsGoodMemory(Thought_Memory m)
        {
            if (m?.def == null || m.permanent || m.moodPowerFactor <= 0f || !IsRimfolk(m.def) || m.def.DurationTicks <= 0 || Milestone.Is(m.def)) return false;
            ThoughtStage stage;
            try
            {
                stage = m.CurStage;
            }
            catch
            {
                return false;
            }
            return stage != null && stage.baseMoodEffect > 0f;
        }

        /// <summary>How long a good memory given to this colonist now lasts, as a share of its full time.</summary>
        public static float DurationFactor(Pawn p)
        {
            var need = p?.needs?.mood;
            return need == null ? 1f : GoodTimesMath.DurationFactor(need.CurLevelPercentage);
        }

        /// <summary>The age at which a memory's mood ends: its time, or sooner for a memory about someone that came in good times.</summary>
        public static int MoodTicks(Thought_Memory m)
        {
            int full = m.DurationTicks;
            return m is Thought_CouncilSocial s && s.moodTicks >= 0 && s.moodTicks < full ? s.moodTicks : full;
        }

        /// <summary>
        /// A good memory given while life was already good, which fades sooner for it. Rimfolk never sets a memory's
        /// time itself, so a shorter one on its good memory is this rule's.
        /// </summary>
        public static bool FadesSooner(Thought_Memory m)
        {
            if (m?.def == null) return false;
            bool shorter = m is Thought_CouncilSocial s
                ? s.moodTicks >= 0 && s.moodTicks < m.DurationTicks
                : m.durationTicksOverride >= 0 && m.durationTicksOverride < m.def.DurationTicks;
            return shorter && IsGoodMemory(m);
        }

        /// <summary>Fades sooner, and its good mood is still there.</summary>
        public static bool FadingSooner(Thought_Memory m) => FadesSooner(m) && m.age < MoodTicks(m);

        /// <summary>The tooltip line for a good memory that fades sooner (null if it doesn't).</summary>
        public static string FadeLine(Thought_Memory m)
        {
            if (!FadesSooner(m)) return null;
            float share = MoodTicks(m) / (float)m.def.DurationTicks;
            return "CC_GoodTimes_FadeNote".Translate(share.ToStringPercent()).Resolve();
        }

        /// <summary>A memory's description with the line about fading sooner, if it does.</summary>
        public static string FadeNote(Thought_Memory m, string description)
        {
            string line = FadeLine(m);
            if (line == null) return description;
            return description.NullOrEmpty() ? line : description + "\n\n" + line;
        }

        /// <summary>
        /// As a memory is given (Patch_GoodTimesFade): one of Rimfolk's good memories gets a shorter time when the
        /// colonist's mood is high. Returns the time its giver set (-1: the def's own), or <see cref="Untouched"/>.
        /// </summary>
        public static int BeforeGiven(MemoryThoughtHandler handler, Thought_Memory m)
        {
            if (!FadeOn || handler == null || !IsGoodMemory(m)) return Untouched;
            int own = m.durationTicksOverride;
            float f = DurationFactor(handler.pawn);
            if (m is Thought_CouncilSocial social)
            {
                // A memory about someone: the opinion lasts its full time, only the good mood ends sooner.
                social.moodTicks = f < 1f ? GoodTimesMath.ShortenedTicks(m.DurationTicks, f) : -1;
                return own;
            }
            // Another mod's kind of memory about someone can't end its mood alone: its opinion matters more.
            if (m is Thought_MemorySocial) return Untouched;
            if (f < 1f) m.durationTicksOverride = GoodTimesMath.ShortenedTicks(own >= 0 ? own : m.def.DurationTicks, f);
            return own;
        }

        /// <summary>
        /// After it was given: if it renewed a memory already there (the same good thing again) instead of joining the
        /// list, that one starts over with the time the new one would have had, shorter in good times and its full
        /// length again in hard times.
        /// </summary>
        public static void AfterGiven(MemoryThoughtHandler handler, Thought_Memory m, int own)
        {
            if (own == Untouched || handler == null || m == null || m.pawn == null || m.pawn != handler.pawn) return;
            var list = handler.Memories;
            if (list == null || list.Contains(m)) return;
            for (int i = 0; i < list.Count; i++)
            {
                var old = list[i];
                if (old == null || old.def != m.def || old.age != 0 || old.permanent || !old.GroupsWith(m)) continue;
                old.durationTicksOverride = m.durationTicksOverride;
                if (old is Thought_CouncilSocial os && m is Thought_CouncilSocial ms) os.moodTicks = ms.moodTicks;
            }
        }

        // ---------------- Standards and the game's comforts ----------------

        private static int cachedPawnId = -1, cachedTick = -1;
        private static float cachedFactor = 1f;

        /// <summary>
        /// How much of a good mood from the game or another mod this colonist feels: 90% comfortable, 80% particular,
        /// 70% spoiled. Rimfolk's own moods follow standards as they're given, so they aren't scaled twice.
        /// </summary>
        public static float ComfortFactor(Pawn p, ThoughtDef def)
        {
            if (Raw || p == null || def == null || IsRimfolk(def) || !ComfortsOn) return 1f;
            // The game adds up one colonist's moods in a row, so each colonist's factor is kept for the tick.
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (p.thingIDNumber != cachedPawnId || tick != cachedTick)
            {
                cachedPawnId = p.thingIDNumber;
                cachedTick = tick;
                cachedFactor = GoodTimesMath.ComfortFactor((int)Standards.LevelOf(p));
            }
            return cachedFactor;
        }

        /// <summary>A mood group as the colonist feels it (Patch_ComfortStandards): only good moods from outside Rimfolk change.</summary>
        public static float Felt(ThoughtHandler handler, Thought group, float offset)
        {
            if (offset <= 0f || handler == null) return offset;
            float f = ComfortFactor(handler.pawn, group?.def);
            return f < 1f ? GoodTimesMath.Comfort(offset, f) : offset;
        }

        /// <summary>A mood group as it would be without these rules and without good moods adding up slowly.</summary>
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

        private static string LevelLabel(Pawn p) => ("CC_Standards_" + Standards.LevelOf(p)).Translate().ToString();

        /// <summary>The tooltip line for a comfort the colonist feels less for their standards (null if they don't).</summary>
        public static string ComfortNote(Pawn p, Thought group, float felt)
        {
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null || felt <= 0f || group?.def == null || ComfortFactor(p, group.def) >= 1f) return null;
            float full = RawOffsetOfGroup(handler, group);
            if (full - felt < 0.05f) return null;
            return "CC_GoodTimes_ComfortNote".Translate(p.Named("PAWN"), LevelLabel(p).Named("LEVEL"), MoodView.Number(felt).Named("FELT"),
                MoodView.Number(full).Named("FULL")).Resolve();
        }

        // ---------------- The Mood tab's readout ----------------

        private static readonly List<Thought> tmpGroups = new List<Thought>();

        private static string Whole(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);

        /// <summary>
        /// One line for the Mood tab: what these rules and good moods adding up slowly take off the colonist's good
        /// moods right now.
        /// Null when they take nothing off.
        /// </summary>
        public static string Readout(Pawn p)
        {
            var handler = p?.needs?.mood?.thoughts;
            if (handler == null) return null;
            float rimFelt = 0f, rimFull = 0f, gameFelt = 0f, gameFull = 0f;
            int sooner = 0;
            try
            {
                tmpGroups.Clear();
                handler.GetDistinctMoodThoughtGroups(tmpGroups);
                for (int i = 0; i < tmpGroups.Count; i++)
                {
                    var g = tmpGroups[i];
                    if (g?.def == null) continue;
                    float full = RawOffsetOfGroup(handler, g);
                    if (full <= 0f) continue;
                    float felt = Mathf.Max(0f, handler.MoodOffsetOfGroup(g));
                    if (IsRimfolk(g.def))
                    {
                        rimFelt += felt;
                        rimFull += full;
                    }
                    else
                    {
                        gameFelt += felt;
                        gameFull += full;
                    }
                }
                var memories = handler.memories?.Memories;
                if (memories != null)
                    for (int i = 0; i < memories.Count; i++)
                        if (FadingSooner(memories[i]))
                            sooner++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("good times readout", e);
                return null;
            }
            finally
            {
                // Don't keep thoughts (and through them, the whole game) alive between calls.
                tmpGroups.Clear();
            }
            var parts = new List<string>(2);
            bool rimLess = rimFull - rimFelt >= 0.5f;
            if (rimLess || sooner > 0)
            {
                string part = rimLess
                    ? "CC_GoodTimes_Rimfolk".Translate(Whole(rimFelt), Whole(rimFull)).Resolve()
                    : "CC_GoodTimes_RimfolkNow".Translate(Whole(rimFelt)).Resolve();
                if (sooner > 0) part += ", " + "CC_GoodTimes_Sooner".Translate(sooner).Resolve();
                parts.Add(part);
            }
            if (gameFull - gameFelt >= 0.5f)
                parts.Add("CC_GoodTimes_Comforts".Translate(Whole(gameFelt), Whole(gameFull), LevelLabel(p)).Resolve());
            return parts.Count == 0 ? null : "CC_GoodTimes_Line".Translate(string.Join("  ·  ", parts)).Resolve();
        }
    }
}
