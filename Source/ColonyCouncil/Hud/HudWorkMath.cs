using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The work panel's small rules, free of game types so Tools/Tests can check them: the game's job report cut to its
    /// first words ("Hauling steel to stockpile zone 1." to "Hauling steel"), how long someone has been idle, and when
    /// a work type nobody does is worth a line.
    /// </summary>
    public static class HudWorkMath
    {
        /// <summary>
        /// The job report shortened: no closing full stop, and cut before the first of <paramref name="cutAt"/> (" to ",
        /// " at "...) when two words or more come before it. Never longer than <paramref name="maxChars"/> (an ellipsis then).
        /// </summary>
        public static string ShortReport(string report, IList<string> cutAt, int maxChars = 40)
        {
            if (string.IsNullOrEmpty(report)) return "";
            string s = report.Trim();
            int nl = s.IndexOf('\n');
            if (nl > 0) s = s.Substring(0, nl).Trim();
            while (s.EndsWith(".") || s.EndsWith("…")) s = s.Substring(0, s.Length - 1).TrimEnd();
            if (cutAt != null)
            {
                int best = -1;
                foreach (var w in cutAt)
                {
                    if (string.IsNullOrEmpty(w)) continue;
                    int i = s.IndexOf(w, StringComparison.OrdinalIgnoreCase);
                    // Two words must come before it: "Hauling steel to..." is cut, "Tending to Bob" stays whole.
                    if (i > 0 && Words(s.Substring(0, i)) >= 2 && (best < 0 || i < best)) best = i;
                }
                if (best > 0) s = s.Substring(0, best).TrimEnd(' ', ',', ';', ':');
            }
            if (maxChars > 3 && s.Length > maxChars) s = s.Substring(0, maxChars - 1).TrimEnd() + "…";
            return s;
        }

        private static int Words(string s) => s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

        /// <summary>Idle time in hours from the ticks it began and now (an hour is 2500 ticks); 0 when unknown.</summary>
        public static float IdleHours(int sinceTick, int nowTick) => sinceTick < 0 || nowTick < sinceTick ? 0f : (nowTick - sinceTick) / 2500f;

        /// <summary>
        /// Whether a work type with nobody switched on for it is worth a line: only when there's work for it waiting
        /// and no one able to do it has it on.
        /// </summary>
        public static bool Unmanned(bool workWaiting, int peopleWithItOn) => workWaiting && peopleWithItOn == 0;
    }
}
