namespace ColonyCouncil
{
    /// <summary>How colonists' speech shows (the setting "Colonists' speech shows as").</summary>
    public enum SpeechMode
    {
        /// <summary>As speech bubbles when Interaction Bubbles is loaded, else as floating text (the default).</summary>
        Bubbles,
        /// <summary>As floating text over the speaker, always.</summary>
        Floating,
        /// <summary>Not at all (the social log still keeps the moments between two people).</summary>
        Off
    }

    /// <summary>
    /// The choices behind <see cref="Speech"/>: how a line shows, and how a moment between two people goes into the
    /// social log. No game types, so Tools/Tests can check them.
    /// </summary>
    public static class SpeechMath
    {
        /// <summary>
        /// How speech shows now: bubbles only when the setting asks for them and Interaction Bubbles is there to draw
        /// them; floating text otherwise; nothing when switched off.
        /// </summary>
        public static SpeechMode Shows(SpeechMode setting, bool bubblesLoaded)
        {
            if (setting == SpeechMode.Off) return SpeechMode.Off;
            return setting == SpeechMode.Bubbles && bubblesLoaded ? SpeechMode.Bubbles : SpeechMode.Floating;
        }

        /// <summary>
        /// A moment between two people goes into the social log through the game's own door (where Interaction Bubbles
        /// listens) when it should show as a bubble, or when nothing would draw it anyway (no bubbles: other mods that read
        /// the log still hear of it). Otherwise it is written in quietly, so it is kept without a second bubble or one the
        /// player switched off.
        /// </summary>
        public static bool ThroughLog(SpeechMode shows, bool bubblesLoaded, bool show) =>
            !bubblesLoaded || (show && shows == SpeechMode.Bubbles);

        /// <summary>A moment between two people also floats over the first of them: shown, and speech shows as floating text.</summary>
        public static bool Floats(SpeechMode shows, bool show) => show && shows == SpeechMode.Floating;
    }
}
