using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// A quiet day's evening news (UI/NewsQuiet.cs) as plain rules: a day is quiet when the news waiting for the evening
    /// is only a few pieces, none of them bad news, and short enough to read as one message; its lines are then joined
    /// into one short digest. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class NewsQuietMath
    {
        /// <summary>A quiet day has this many pieces of news at most.</summary>
        public const int MaxItems = 4;
        /// <summary>Fewer than this is no letter anyway: a single piece of news already comes as a message.</summary>
        public const int MinItems = 2;
        /// <summary>The day's lines together are this long at most, so the digest stays one short message.</summary>
        public const int MaxChars = 320;

        /// <summary>Whether the day was quiet: two to four pieces of news, none bad, short enough together.</summary>
        public static bool Quiet(int items, int bad, int chars) => items >= MinItems && items <= MaxItems && bad <= 0 && chars <= MaxChars;

        /// <summary>The day's lines as one run of sentences: each ends with a stop, empty ones left out.</summary>
        public static string Join(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            if (lines == null) return "";
            foreach (var raw in lines)
            {
                string line = raw?.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(line);
                char last = line[line.Length - 1];
                if (last != '.' && last != '?' && last != '!' && last != ')' && last != '"') sb.Append('.');
            }
            return sb.ToString();
        }
    }
}
