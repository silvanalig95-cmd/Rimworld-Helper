using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pop-up's click (the player, 10 November 2026: "i should be able to read the letter directly by clicking the
    /// pop-up"): a message about one of the colony's letters written out in full opens it to read (Letters/LetterClick.cs).
    /// The rules, pure.
    /// </summary>
    public static class LetterClickMath
    {
        /// <summary>Messages kept with their letter at once (the game shows twelve at most); the oldest let go past it.</summary>
        public const int MaxKept = 24;

        /// <summary>
        /// The message's last words: "Click to read it." when a click opens it; where it can be read when the click only jumps
        /// the camera (the patch not applied, or the deeper hooks off); nothing for a letter not written out in full.
        /// </summary>
        public static string HintKey(bool kept, bool clickable, bool inBag) =>
            !kept ? null : clickable ? "CC_LetterRead_MsgClick" : inBag ? "CC_LetterRead_Msg" : "CC_LetterRead_MsgPeople";

        /// <summary>
        /// A click opens the letter: the left button going down on the message, a letter kept with it, the game playing. A right
        /// click still dismisses the message, and anything else is the game's.
        /// </summary>
        public static bool Opens(bool mouseDown, int button, bool inside, bool hasLetter, bool playing) => mouseDown && button == 0 && inside && hasLetter && playing;
    }

    /// <summary>
    /// A short-lived map from a message on screen to its letter, keyed by the message itself (by reference, never by its
    /// words): the newest <see cref="LetterClickMath.MaxKept"/>, each let go once its message is off the screen.
    /// </summary>
    public class ClickMap<T> where T : class
    {
        private readonly List<KeyValuePair<T, int>> kept = new List<KeyValuePair<T, int>>();

        public int Count => kept.Count;

        /// <summary>Kept with its letter (a message noted again takes the newer letter).</summary>
        public void Note(T key, int letterId)
        {
            if (key == null || letterId < 0) return;
            Forget(key);
            kept.Add(new KeyValuePair<T, int>(key, letterId));
            while (kept.Count > LetterClickMath.MaxKept) kept.RemoveAt(0);
        }

        /// <summary>The letter kept with this message, or -1.</summary>
        public int Of(T key)
        {
            if (key == null) return -1;
            for (int i = kept.Count - 1; i >= 0; i--)
                if (ReferenceEquals(kept[i].Key, key))
                    return kept[i].Value;
            return -1;
        }

        public void Forget(T key) => kept.RemoveAll(k => ReferenceEquals(k.Key, key));

        /// <summary>Lets go of every message no longer on screen.</summary>
        public void Prune(Func<T, bool> live) => kept.RemoveAll(k => k.Key == null || !live(k.Key));

        public void Clear() => kept.Clear();
    }
}
