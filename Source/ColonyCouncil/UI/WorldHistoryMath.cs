using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the region window's History page (UI/WorldPages.cs), free of game types so Tools/Tests can check
    /// them: which kinds of event its filter offers, how a kind reads when there is no text for it, and which events a
    /// people and a town together leave in the list.
    /// </summary>
    public static class WorldHistoryMath
    {
        /// <summary>The most events the page lists at once (the history keeps at most HistoryMath.MaxEvents).</summary>
        public const int MaxShown = 300;

        /// <summary>The families of these kinds ("deed:traded" is of "deed"), the most common first, then by name; each once.</summary>
        public static List<string> Families(IEnumerable<string> kinds)
        {
            var count = new Dictionary<string, int>();
            if (kinds != null)
                foreach (var k in kinds)
                {
                    string f = HistoryKinds.Family(k);
                    if (string.IsNullOrEmpty(f)) continue;
                    count[f] = count.TryGetValue(f, out int n) ? n + 1 : 1;
                }
            var list = new List<string>(count.Keys);
            list.Sort((a, b) =>
            {
                int c = count[b].CompareTo(count[a]);
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
            return list;
        }

        /// <summary>
        /// A kind in plain words, for an event with no text of its own and for a family with no name in the keys:
        /// "tie:pact:made" is "pact made", "ambitionAchieved" is "ambition achieved", "world:began" is "world began".
        /// </summary>
        public static string Words(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "";
            var parts = kind.Split(':');
            // A tie's family word ("tie") says nothing a reader needs: its kind and what happened do.
            int from = parts.Length > 1 && parts[0] == HistoryKinds.Tie ? 1 : 0;
            var sb = new StringBuilder();
            for (int i = from; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                for (int j = 0; j < p.Length; j++)
                {
                    char c = p[j];
                    if (char.IsUpper(c) && j > 0) sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        /// <summary>Which of two filters asks first: a people (the history of someone), else a town (what happened at it or to it), else none.</summary>
        public static Ref Lead(Ref people, Ref town) => !people.IsNone ? people : town;

        /// <summary>
        /// An event kept by every filter that is set: of the people when one is set, at or about the town when one is
        /// set, of the kind's family when one is set.
        /// </summary>
        public static bool Keeps(HistoryEntry e, Ref people, Ref town, string family)
        {
            if (e == null) return false;
            if (!people.IsNone && !e.Involves(people)) return false;
            if (!town.IsNone && e.place != town && !e.Involves(town)) return false;
            return family == null || HistoryKinds.Matches(e.kind, family);
        }

        /// <summary>The days an event spans, for its line: one day, or a summary's or a run's first and last.</summary>
        public static (int from, int to) Days(HistoryEntry e)
        {
            if (e == null) return (0, 0);
            int to = e.tick / HistoryMath.DayTicks + 1;
            int from = Math.Min(e.first, e.tick) / HistoryMath.DayTicks + 1;
            return (from, to);
        }
    }
}
