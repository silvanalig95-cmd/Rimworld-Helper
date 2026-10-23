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
        /// A line as it shows over a head or quoted in a message: whole when it fits in <paramref name="maxLen"/>;
        /// otherwise as many whole sentences as fit, at least the first (a first sentence too long on its own is cut
        /// with "…"). A sentence ends at '.', '!' or '?' from the 13th character on, before a space or the end (with
        /// any closing quote). Quotes round the whole line go; a quote cut open is closed.
        /// </summary>
        public static string Shorten(string line, int maxLen = 90)
        {
            if (string.IsNullOrEmpty(line)) return line;
            string s = line.Trim();
            // Quotes round the whole line go; a quote that opens a line ("{WISH}" is still waiting.) stays.
            if (s.Length >= 2 && s[0] == '"' && s.IndexOf('"', 1) == s.Length - 1) s = s.Substring(1, s.Length - 2).Trim();
            if (s.Length > maxLen)
            {
                int keep = -1;
                for (int i = 12; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c != '.' && c != '!' && c != '?') continue;
                    int end = i + 1;
                    while (end < s.Length && (s[end] == '"' || s[end] == '\'' || s[end] == ')')) end++;
                    if (end < s.Length && !char.IsWhiteSpace(s[end])) continue;
                    if (keep >= 0 && end > maxLen) break;
                    keep = end;
                    i = end - 1;
                }
                if (keep > 0 && keep < s.Length) s = s.Substring(0, keep).TrimEnd();
                if (s.Length > maxLen) s = s.Substring(0, maxLen - 1).TrimEnd() + "…";
            }
            // A cut inside a quotation closes it.
            int quotes = 0;
            foreach (char c in s)
                if (c == '"') quotes++;
            if (quotes % 2 == 1) s += "\"";
            return s;
        }

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
