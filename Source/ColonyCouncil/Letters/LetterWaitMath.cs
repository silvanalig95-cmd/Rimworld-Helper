using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What the colony still waits on for one of its letters: what a greyed letter (and "Write back") says it waits for.</summary>
    public enum LetterWait
    {
        /// <summary>Nothing: its answer is in (an invitation taken up may still have its guest to come), or it is over.</summary>
        None,
        /// <summary>Not written yet, or the call not made yet.</summary>
        Writing,
        /// <summary>Written, and waiting in the mailbag or at the gate for someone going their way, or for their messenger.</summary>
        Gate,
        /// <summary>Taken by someone who hasn't left the map yet: not on the road.</summary>
        NotLeft,
        /// <summary>On the road to them, or with them to read (a call's message with an aide).</summary>
        Road,
        /// <summary>Their answer is on its way back.</summary>
        Back,
        /// <summary>Read at their end, and no answer has come: none may (a people at war keeping silent).</summary>
        NoWord
    }

    /// <summary>
    /// When the colony's letters are answered, with no game types so Tools/Tests can check it (World/NeighbourLetters.cs
    /// reads it; the player, 11 November 2026: "i'm a bit confused about when i can reply to a message and when not", and
    /// "It should always take the necessary time for stuff to actually get delivered, depending on the distance"). A letter
    /// counts as one under way, and holds one letter at a time to its reader, only while the colony waits for its answer:
    /// once the answer is in, the colony may write again, whatever follows (a guest still to come). A letter is on the road
    /// only once whoever carries it has left the map, and an answer, a refusal or silence is known only once the road back
    /// allows it.
    /// </summary>
    public static class LetterWaitMath
    {
        public const int DayTicks = 60000;

        /// <summary>The colony waits on it: it holds one letter at a time to its reader and counts toward the letters under way.</summary>
        public static bool Waits(LetterWait w) => w != LetterWait.None;

        /// <summary>
        /// A letter read at their end: what the colony waits for now. <paramref name="silenceKnownLater"/>: nothing came at
        /// once and nothing was sent, so the colony is still waiting for word (an answer the colony has not had);
        /// <paramref name="carried"/>: the answer goes by someone (the post, a messenger, the colony's own rider, or held at
        /// their end for the road back); <paramref name="stillOut"/>: it is still on its way. Carried and in, or told at once
        /// (a call): nothing.
        /// </summary>
        public static LetterWait AfterReading(bool silenceKnownLater, bool carried, bool stillOut) =>
            stillOut ? LetterWait.Back : carried ? LetterWait.None : silenceKnownLater ? LetterWait.NoWord : LetterWait.None;

        /// <summary>
        /// While whoever carries it is still on the colony's map, the day it reaches them moves on with the clock: its days on
        /// the road are kept, counted from when they leave. Returns the new day it is read.
        /// </summary>
        public static int Shifted(int due, int since, int now) => due + Math.Max(0, now - since);

        /// <summary>A messenger sent at last with mail that waited in the post: no sooner than it came into the region.</summary>
        public static int MessengerArrives(int now, int arrive) => Math.Max(now, arrive);

        /// <summary>Days after it is read before the colony takes silence as the answer: the road back twice over and two days more.</summary>
        public static float NoWordDays(float backDays) => Math.Max(0f, backDays) * 2f + 2f;

        /// <summary>
        /// The soonest a letter can go again after the waits for the same thing (each purpose's last letter, -1 never, and its
        /// days to wait): the earliest tick still to come, or -1 when nothing is waiting.
        /// </summary>
        public static int SoonestOpen(IList<int> lasts, IList<float> waitDays, int now)
        {
            int best = -1;
            if (lasts == null || waitDays == null) return best;
            for (int i = 0; i < lasts.Count && i < waitDays.Count; i++)
            {
                if (lasts[i] < 0) continue;
                int open = lasts[i] + (int)(waitDays[i] * DayTicks);
                if (open <= now) continue;
                if (best < 0 || open < best) best = open;
            }
            return best;
        }

        /// <summary>The words of one letter at a time to each: "once their answer reaches the colony", or, with no word yet, "or once it is plain none will".</summary>
        public static string OneAtATimeKey(LetterWait w) => w == LetterWait.NoWord ? "CC_Letter_Why_OneAtATime_NoWord" : "CC_Letter_Why_OneAtATime";
    }
}
