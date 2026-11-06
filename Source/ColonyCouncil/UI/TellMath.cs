using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What <see cref="TellBook"/> decides for one message or letter: the channel it goes by.</summary>
    public enum TellVerdict
    {
        /// <summary>Shown now (a message on screen, or the letter).</summary>
        Show,
        /// <summary>The same thing was told within <see cref="TellMath.MergeHours"/>: not again.</summary>
        Merged,
        /// <summary>The day is full, or busy for what it is: kept for the evening's colony news.</summary>
        Held
    }

    /// <summary>
    /// How much a happening matters, as the teller weighs it (Notify.cs's <c>Importance</c> as a number, so the rules here
    /// need no game types): colour, worth knowing, hard to miss.
    /// </summary>
    public static class TellWeight
    {
        public const int Flavor = 0, Notable = 1, Important = 2;
    }

    /// <summary>
    /// The arithmetic of Rimfolk's teller (UI/Tell.cs), with no game types so Tools/Tests can check it. The teller picks
    /// the channel by how much a happening matters (its importance and its kind) and by how busy the colony's day already
    /// is: something hard to miss is always shown at once; something worth knowing is shown until the colony's count for
    /// the day is reached, and on a busy day (half the day's messages shown, or something hard to miss told in the last few
    /// hours) one person's news, health and the land's wait for the evening while the colony's and the world's still come;
    /// colour is told only when the "How many messages" setting asks for everything. The evening's colony news carries the
    /// lines that matter most (<see cref="EveningLines"/>) and puts the rest in the History tab. One count a day per colony for messages and
    /// one for letters, whatever the kind (the old caps of each kind folded in); the same thing within a few hours is told
    /// once, by whichever channel came first.
    /// </summary>
    public static class TellMath
    {
        /// <summary>The most messages a day for one colony, all kinds together, before the rest wait for the evening.</summary>
        public const int DefaultMessagesADay = 12;

        /// <summary>The most letters that only tell, a day for one colony, before the rest become lines in the evening's news.</summary>
        public const int DefaultLettersADay = 4;

        /// <summary>The settings' ranges.</summary>
        public const int MinMessagesADay = 4, MaxMessagesADay = 40, MinLettersADay = 1, MaxLettersADay = 12;

        /// <summary>Something hard to miss told within this many hours makes the colony's day busy.</summary>
        public const int BusyHours = 6;

        public const int BusyTicks = BusyHours * Sched.HourTicks;

        /// <summary>The most lines the evening's colony news carries; past it, the lightest go to the History tab.</summary>
        public const int EveningLines = 12;

        /// <summary>The same text (or the same key) within this many in-game hours is told once.</summary>
        public const int MergeHours = 4;

        public const int MergeTicks = MergeHours * Sched.HourTicks;

        /// <summary>The colony of news that belongs to none (the world beyond the homes).</summary>
        public const int NoColony = -1;

        /// <summary>The kinds of news as numbers (UI/News.cs's <c>NewsKind</c>, in its order).</summary>
        public const int KindPeople = 0, KindHealth = 1, KindColony = 2, KindWorld = 3, KindLand = 4;

        /// <summary>The colony's and the world's news weigh more than one person's day, their health or the land's.</summary>
        public static bool Weighty(int kind) => kind == KindColony || kind == KindWorld;

        /// <summary>The day is busy for a message once half the day's are shown (never below two).</summary>
        public static int BusyAt(int messagesADay) => Math.Max(2, messagesADay / 2);

        /// <summary>A line's place in the evening's letter: hard to miss first, then what the day held back, then the colony's and the world's.</summary>
        public static int EveningScore(int weight, bool held, int kind) => weight * 4 + (held ? 2 : 0) + (Weighty(kind) ? 1 : 0);

        /// <summary>
        /// Which of the evening's lines the letter carries: all of them up to <paramref name="max"/>, else the
        /// <paramref name="max"/> that score highest (the earlier first among equals), left in their own order. The rest
        /// go to the History tab.
        /// </summary>
        public static bool[] EveningKeep(IReadOnlyList<int> scores, int max)
        {
            int n = scores?.Count ?? 0;
            var keep = new bool[n];
            if (n == 0) return keep;
            if (max <= 0 || n <= max)
            {
                for (int i = 0; i < n; i++) keep[i] = true;
                return keep;
            }
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => scores[b] != scores[a] ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
            for (int i = 0; i < max; i++) keep[order[i]] = true;
            return keep;
        }

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
    /// within <see cref="TellMath.MergeHours"/>, by whichever channel), how many messages and letters each colony has been
    /// shown today, when it was last told something hard to miss, and the day's tally of each channel (the dev log's
    /// measure of a day). Not saved: after a load the day starts counting again, which only lets a little more through.
    /// </summary>
    public sealed class TellBook
    {
        /// <summary>When each thing was last told (its merge key), pruned of what's older than the merge window.</summary>
        private readonly Dictionary<string, int> told = new Dictionary<string, int>();

        /// <summary>Today's count per colony and channel (messages or letters), all kinds together.</summary>
        private readonly Dictionary<(int colony, bool letter), int> today = new Dictionary<(int, bool), int>();

        /// <summary>When each colony was last told something hard to miss.</summary>
        private readonly Dictionary<int, int> lastImportant = new Dictionary<int, int>();

        /// <summary>Today's tally per colony of what went by each channel.</summary>
        private readonly Dictionary<(int colony, string channel), int> tally = new Dictionary<(int, string), int>();

        private int day = int.MinValue;
        private int prunedAt = int.MinValue;

        public int Remembered => told.Count;

        public void Clear()
        {
            told.Clear();
            today.Clear();
            lastImportant.Clear();
            tally.Clear();
            day = int.MinValue;
            prunedAt = int.MinValue;
        }

        /// <summary>How many messages or letters the colony has been shown today, all kinds together.</summary>
        public int Count(int colony, bool letter, int now)
        {
            Roll(now);
            return today.TryGetValue((colony, letter), out int n) ? n : 0;
        }

        /// <summary>
        /// The colony's day is busy: something hard to miss told within <see cref="TellMath.BusyHours"/>, or
        /// <paramref name="busyAt"/> messages shown already (0: that part doesn't count).
        /// </summary>
        public bool Busy(int colony, int busyAt, int now)
        {
            Roll(now);
            if (lastImportant.TryGetValue(colony, out int at) && at <= now && now - at < TellMath.BusyTicks) return true;
            return busyAt > 0 && Count(colony, false, now) >= busyAt;
        }

        /// <summary>
        /// The channel for a message or letter now. <paramref name="key"/> is what makes two the same (null: never the same
        /// as another); <paramref name="weight"/> how much it matters (<see cref="TellWeight"/>); <paramref name="kind"/> its
        /// kind of news (<see cref="TellMath.KindPeople"/> and so on); <paramref name="cap"/> the day's most for the colony
        /// (0 or less: no cap). Hard to miss: shown, always, and the day is busy for a while. Worth knowing: shown until the
        /// cap, then held for the evening; on a busy day one person's news, health and the land's are held while the
        /// colony's and the world's are still shown. A thing shown or held is remembered, so the same again within the window
        /// is merged either way.
        /// </summary>
        public TellVerdict Judge(string key, int colony, int kind, bool letter, int weight, int cap, int now)
        {
            Roll(now);
            Prune(now);
            if (key != null && told.TryGetValue(key, out int at) && at <= now && now - at < TellMath.MergeTicks) return Tallied(colony, "merged", TellVerdict.Merged);
            if (key != null) told[key] = now;
            var slot = (colony, letter);
            today.TryGetValue(slot, out int n);
            if (weight >= TellWeight.Important)
            {
                lastImportant[colony] = now;
                today[slot] = n + 1;
                return Tallied(colony, letter ? "letter" : "message", TellVerdict.Show);
            }
            if (cap > 0 && n >= cap) return Tallied(colony, "evening", TellVerdict.Held);
            if (!TellMath.Weighty(kind) && Busy(colony, letter || cap <= 0 ? 0 : TellMath.BusyAt(cap), now)) return Tallied(colony, "evening", TellVerdict.Held);
            today[slot] = n + 1;
            return Tallied(colony, letter ? "letter" : "message", TellVerdict.Show);
        }

        /// <summary>Something hard to miss the teller didn't tell itself (the storyteller's raid) happened at <paramref name="at"/>: the day is busy from then.</summary>
        public void Important(int colony, int at, int now)
        {
            Roll(now);
            if (at > now || now - at >= TellMath.BusyTicks) return;
            if (!lastImportant.TryGetValue(colony, out int was) || was < at || was > now) lastImportant[colony] = at;
        }

        /// <summary>The same thing was told (by any channel) within the merge window: not again. Otherwise it's noted as told now.</summary>
        public bool Told(string key, int now)
        {
            Roll(now);
            Prune(now);
            if (key == null) return false;
            if (told.TryGetValue(key, out int at) && at <= now && now - at < TellMath.MergeTicks) return true;
            told[key] = now;
            return false;
        }

        /// <summary>One more by a channel today (the evening's news, the History tab, the chronicle), for the day's tally.</summary>
        public void Note(int colony, string channel, int now)
        {
            Roll(now);
            tally.TryGetValue((colony, channel), out int n);
            tally[(colony, channel)] = n + 1;
        }

        /// <summary>Today's tally of a channel for a colony: "message", "letter", "evening", "history", "merged" or "chronicle".</summary>
        public int Tally(int colony, string channel, int now)
        {
            Roll(now);
            return tally.TryGetValue((colony, channel), out int n) ? n : 0;
        }

        /// <summary>The colonies told anything today.</summary>
        public List<int> ColoniesToday(int now)
        {
            Roll(now);
            var list = new List<int>();
            foreach (var k in tally.Keys)
                if (!list.Contains(k.colony)) list.Add(k.colony);
            return list;
        }

        private TellVerdict Tallied(int colony, string channel, TellVerdict v)
        {
            tally.TryGetValue((colony, channel), out int n);
            tally[(colony, channel)] = n + 1;
            return v;
        }

        /// <summary>A new day starts the counts again; a clock gone back (another game loaded) forgets the busy hours too.</summary>
        private void Roll(int now)
        {
            int d = Day(now);
            if (d == day) return;
            if (d < day) lastImportant.Clear();
            day = d;
            today.Clear();
            tally.Clear();
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
