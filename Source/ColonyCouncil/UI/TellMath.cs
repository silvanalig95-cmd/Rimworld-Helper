using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What <see cref="TellBook"/> decides for one message or letter.</summary>
    public enum TellVerdict
    {
        /// <summary>Shown now.</summary>
        Show,
        /// <summary>The same thing was told within <see cref="TellMath.MergeHours"/>: not again.</summary>
        Merged,
        /// <summary>Over the day's cap for its kind and colony: kept for the evening's colony news.</summary>
        Held
    }

    /// <summary>
    /// The arithmetic of Rimfolk's one voice to the player (UI/Tell.cs), with no game types so Tools/Tests can check it:
    /// the default caps, the hours within which the same thing is told once, and which kind of news a source folder's
    /// messages are counted under.
    /// </summary>
    public static class TellMath
    {
        /// <summary>The most messages a day, of one kind of news and one colony, before the rest wait for the evening.</summary>
        public const int DefaultMessagesADay = 20;

        /// <summary>The most letters that only tell, a day, of one kind of news and one colony, before the rest become lines in the evening's news.</summary>
        public const int DefaultLettersADay = 6;

        /// <summary>The same text (or the same key) within this many in-game hours is told once.</summary>
        public const int MergeHours = 4;

        public const int MergeTicks = MergeHours * Sched.HourTicks;

        /// <summary>The colony of news that belongs to none (the world beyond the homes).</summary>
        public const int NoColony = -1;

        /// <summary>
        /// The kind of news (a <c>NewsKind</c> name) a message is counted under, from the folder of the code that sent it,
        /// for the callers that don't say (Notify.Message).
        /// </summary>
        public static string KindOfFolder(string folder)
        {
            switch (folder ?? "")
            {
                case "Social":
                case "Secrets":
                case "Persona":
                case "Quirks":
                case "Lives":
                case "Dreams":
                case "Arcs":
                case "Aspirations":
                case "Heart":
                case "Faith":
                case "Epithets":
                case "Immersion":
                case "Moods":
                case "Pressures":
                    return "People";
                case "Health":
                case "Ailments":
                    return "Health";
                case "World":
                case "Journeys":
                case "Outsiders":
                case "Couriers":
                case "Strangers":
                case "Visitors":
                case "Missions":
                case "Camps":
                case "Scenarios":
                case "Happenings":
                    return "World";
                case "Animals":
                case "Weather":
                case "Fields":
                    return "Land";
                default:
                    return "Colony";
            }
        }

        /// <summary>The folder a source file sits in (its last directory), from a path with either kind of slash.</summary>
        public static string FolderOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string p = path.Replace('\\', '/');
            int end = p.LastIndexOf('/');
            if (end <= 0) return "";
            int start = p.LastIndexOf('/', end - 1);
            return p.Substring(start + 1, end - start - 1);
        }

        /// <summary>A letter as one line of the evening's news: its title, then its text with the paragraphs run together.</summary>
        public static string LetterLine(string label, string text)
        {
            string body = string.Join(" ", (text ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            body = body.Replace("  ", " ");
            if (string.IsNullOrEmpty(label)) return body;
            if (body.Length == 0) return label;
            return label + ": " + body;
        }
    }

    /// <summary>
    /// What Rimfolk has told the player lately: each message's and letter's text or key (to tell the same thing once
    /// within <see cref="TellMath.MergeHours"/>), and how many of each kind each colony has had today (for the caps).
    /// Not saved: after a load the day starts counting again, which only lets a little more through.
    /// </summary>
    public sealed class TellBook
    {
        /// <summary>When each thing was last told (its merge key), pruned of what's older than the merge window.</summary>
        private readonly Dictionary<string, int> told = new Dictionary<string, int>();

        /// <summary>Today's count per colony, kind and channel (messages or letters).</summary>
        private readonly Dictionary<(int colony, int kind, bool letter), int> today = new Dictionary<(int, int, bool), int>();

        private int day = int.MinValue;
        private int prunedAt = int.MinValue;

        public int Remembered => told.Count;

        public void Clear()
        {
            told.Clear();
            today.Clear();
            day = int.MinValue;
            prunedAt = int.MinValue;
        }

        /// <summary>How many of this kind the colony has been shown today, as messages or as letters.</summary>
        public int Count(int colony, int kind, bool letter, int now)
        {
            Roll(now);
            return today.TryGetValue((colony, kind, letter), out int n) ? n : 0;
        }

        /// <summary>
        /// Whether to show a message or letter now. <paramref name="key"/> is what makes two the same (null: never the
        /// same as another); <paramref name="cap"/> is the day's most of its kind for its colony (0 or less: no cap).
        /// A thing shown or held is remembered, so the same again within the window is merged either way.
        /// </summary>
        public TellVerdict Judge(string key, int colony, int kind, bool letter, int cap, int now)
        {
            Roll(now);
            Prune(now);
            if (key != null && told.TryGetValue(key, out int at) && at <= now && now - at < TellMath.MergeTicks) return TellVerdict.Merged;
            if (key != null) told[key] = now;
            var slot = (colony, kind, letter);
            today.TryGetValue(slot, out int n);
            if (cap > 0 && n >= cap) return TellVerdict.Held;
            today[slot] = n + 1;
            return TellVerdict.Show;
        }

        /// <summary>A new day starts the counts again; a clock gone back (another game loaded) too.</summary>
        private void Roll(int now)
        {
            int d = Day(now);
            if (d == day) return;
            day = d;
            today.Clear();
        }

        /// <summary>What's older than the merge window is forgotten, an hour at a time.</summary>
        private void Prune(int now)
        {
            if (prunedAt != int.MinValue && now >= prunedAt && now - prunedAt < Sched.HourTicks) return;
            prunedAt = now;
            if (told.Count == 0) return;
            var old = new List<string>();
            foreach (var kv in told)
                if (kv.Value > now || now - kv.Value >= TellMath.MergeTicks) old.Add(kv.Key);
            foreach (var k in old) told.Remove(k);
        }

        private static int Day(int ticks) => ticks >= 0 ? ticks / Sched.DayTicks : (ticks - Sched.DayTicks + 1) / Sched.DayTicks;
    }
}
