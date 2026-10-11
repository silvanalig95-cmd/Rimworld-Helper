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

    /// <summary>The mod's memory thoughts: an ordinary memory that remembers why.</summary>
    public class Thought_CouncilMemory : Thought_Memory, ICausedThought
    {
        public string cause;

        public string Cause
        {
            get => cause;
            set => cause = value;
        }

        public override string Description => Causes.Append(base.Description, cause);

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            Causes.CarryOver(this, cause);
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cause, "cause");
        }
    }

    /// <summary>The mod's social memories (about someone): same, with the opinion effect.</summary>
    public class Thought_CouncilSocial : Thought_MemorySocial, ICausedThought
    {
        public string cause;

        public string Cause
        {
            get => cause;
            set => cause = value;
        }

        public override string Description => Causes.Append(base.Description, cause);

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            Causes.CarryOver(this, cause);
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cause, "cause");
        }
    }
}
