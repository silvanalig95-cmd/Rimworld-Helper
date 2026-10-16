using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What set off a mood, shown under the thought's description in its tooltip: "Why: Ann's wish came true: a room
    /// of her own." Code that gives thoughts wraps itself in <c>using var _ = Causes.Push(...)</c>; every thought
    /// given meanwhile records it.
    /// </summary>
    public static class Causes
    {
        private static readonly List<string> stack = new List<string>();

        public static string Current
        {
            get
            {
                for (int i = stack.Count - 1; i >= 0; i--)
                    if (!stack[i].NullOrEmpty()) return stack[i];
                return null;
            }
        }

        public static Scope Push(string cause)
        {
            stack.Add(cause);
            return new Scope(stack.Count);
        }

        public readonly struct Scope : IDisposable
        {
            private readonly int depth;

            public Scope(int depth)
            {
                this.depth = depth;
            }

            public void Dispose()
            {
                while (stack.Count >= depth && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }
        }

        public static string Append(string description, string cause)
        {
            if (cause.NullOrEmpty()) return description;
            string line = "CC_Cause_Line".Translate(cause).Resolve().Colorize(ColoredText.SubtleGrayColor);
            return description.NullOrEmpty() ? line : description + "\n\n" + line;
        }

        /// <summary>A memory just given replaces an older one of the same kind: the newer cause is the one that counts.</summary>
        public static void CarryOver(Thought_Memory fresh, string cause)
        {
            if (cause.NullOrEmpty() || fresh?.def == null || fresh.def.stackLimit != 1) return;
            var memories = fresh.pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return;
            foreach (var m in memories)
                if (m != fresh && m is ICausedThought c && m.GroupsWith(fresh)) c.Cause = cause;
        }
    }

    public interface ICausedThought
    {
        string Cause { get; set; }
    }

    /// <summary>A lasting mood that can say why it's there right now (which decree, which reasons).</summary>
    public interface IExplainedThought
    {
        string Why { get; }
    }

    /// <summary>The mod's memory thoughts: an ordinary memory that remembers why.</summary>
    public class Thought_CouncilMemory : Thought_Memory, ICausedThought, ICouncilMood
    {
        public string cause;

        /// <summary>The council's scale was applied when it was given (else it's scaled as it's felt, good or bad).</summary>
        public bool scaled;

        public string Cause
        {
            get => cause;
            set => cause = value;
        }

        public bool Scaled
        {
            set => scaled = value;
        }

        public float OwnOffset()
        {
            float own = base.MoodOffset();
            if (scaled) return own;
            // Not given through the council (a moment, a friction): scaled as it's felt instead. Rewards follow the reward
            // strength and standards; letdowns sting more for the comfortable (contrast, off on gentle life pressures).
            return own > 0f ? own * SmallJoys.LiveScale(pawn) : own < 0f ? own * Pressures.Contrast(pawn) : own;
        }

        public override float MoodOffset()
        {
            float own = OwnOffset();
            return own > 0f ? own * SmallJoys.Factor(this) : own;
        }

        public override string Description => GoodTimes.FadeNote(this, SmallJoys.Note(this, Causes.Append(base.Description, cause)));

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            Causes.CarryOver(this, cause);
            RewardInstead.Gained(this);
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cause, "cause");
            // Memories from before this was saved were all given through the council.
            Scribe_Values.Look(ref scaled, "scaled", true);
        }
    }

    /// <summary>The mod's social memories (about someone): same, with the opinion effect.</summary>
    public class Thought_CouncilSocial : Thought_MemorySocial, ICausedThought, ICouncilMood
    {
        public string cause;

        /// <summary>The council's scale was applied when it was given (else it's scaled as it's felt, good or bad).</summary>
        public bool scaled;

        /// <summary>
        /// Getting used to good times (GoodTimes): the age at which this memory's good mood ends, when it came while life
        /// was already good; -1 for the memory's full time. The opinion of the other person always lasts the full time.
        /// </summary>
        public int moodTicks = -1;

        /// <summary>Its good mood has run its (shorter) course; the opinion stays.</summary>
        public bool MoodOver => moodTicks >= 0 && age >= moodTicks;

        public string Cause
        {
            get => cause;
            set => cause = value;
        }

        public bool Scaled
        {
            set => scaled = value;
        }

        public float OwnOffset()
        {
            if (MoodOver) return 0f;
            float own = base.MoodOffset();
            if (scaled) return own;
            // Not given through the council (a moment, a friction): scaled as it's felt instead. Rewards follow the reward
            // strength and standards; letdowns sting more for the comfortable (contrast, off on gentle life pressures).
            return own > 0f ? own * SmallJoys.LiveScale(pawn) : own < 0f ? own * Pressures.Contrast(pawn) : own;
        }

        public override float MoodOffset()
        {
            float own = OwnOffset();
            return own > 0f ? own * SmallJoys.Factor(this) : own;
        }

        public override string Description => GoodTimes.FadeNote(this, SmallJoys.Note(this, Causes.Append(base.Description, cause)));

        public override bool VisibleInNeedsTab => base.VisibleInNeedsTab && !MoodOver;

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            Causes.CarryOver(this, cause);
            RewardInstead.Gained(this);
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cause, "cause");
            // Memories from before this was saved were all given through the council.
            Scribe_Values.Look(ref scaled, "scaled", true);
            Scribe_Values.Look(ref moodTicks, "moodTicks", -1);
        }
    }
}
