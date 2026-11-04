using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The sections of This week, the one inbox (docs/design/interface.md, "This week: one inbox"), in the order they are drawn.</summary>
    public enum InboxSection : byte
    {
        /// <summary>What the player follows, each with its newest change.</summary>
        Following,
        /// <summary>What waits on the player: answers, asks, offers, calls, promises at risk. What its providers count is the one count.</summary>
        Needs,
        /// <summary>What is on its way: letters, caravans, missions taken on, settlers. In steps, never a countdown.</summary>
        UnderWay,
        /// <summary>What comes: the calendar's days, fights promised, missions offered, by date.</summary>
        Coming,
        /// <summary>How people are: strain, health, trust, the mood balance.</summary>
        People,
        /// <summary>The advisor's most pressing steps, leading to Planning's Plan.</summary>
        Plan
    }

    /// <summary>A day something comes, in words: today, tomorrow, or its date.</summary>
    public enum DueWord : byte
    {
        Today,
        Tomorrow,
        Date
    }

    /// <summary>
    /// The inbox's arithmetic, free of game types so Tools/Tests can check it (InboxTests): the sections' order and how
    /// many rows each shows before "and N more", the one count of what waits on the player (the sum of what the Needs
    /// providers count, which with no new asks is 0.83's NeedsCount: petitions, open wishes, letters waiting for an
    /// answer, promises at risk), the order of a section's rows (provider by provider, each provider's rows as This week
    /// always sorted them), the steps of what is under way (never a time left), and a day coming as a word or a date.
    /// </summary>
    public static class InboxMath
    {
        /// <summary>Every section, in the order This week draws them.</summary>
        public static readonly InboxSection[] Sections =
        {
            InboxSection.Following, InboxSection.Needs, InboxSection.UnderWay, InboxSection.Coming, InboxSection.People, InboxSection.Plan
        };

        /// <summary>The most rows a section shows before "and N more" (which opens the rest in place).</summary>
        public const int MaxRows = 6;

        /// <summary>The plan shows the advisor's two most pressing steps; the rest are a click away.</summary>
        public const int PlanRows = 2;

        /// <summary>How many rows a section shows folded.</summary>
        public static int Cap(InboxSection s) => s == InboxSection.Plan ? PlanRows : MaxRows;

        /// <summary>How many of a section's rows show: all when opened, else up to its cap (a single row over the cap shows itself rather than "and 1 more").</summary>
        public static int Shown(InboxSection s, int rows, bool opened)
        {
            if (rows <= 0) return 0;
            if (opened) return rows;
            int cap = Math.Max(1, Cap(s));
            return rows <= cap + 1 ? rows : cap;
        }

        // ---------------- The count ----------------

        /// <summary>The one count: what every provider of Needs you counts, together (a provider's count below 0 counts as none).</summary>
        public static int Count(IEnumerable<int> needs)
        {
            int n = 0;
            if (needs == null) return 0;
            foreach (int x in needs)
                if (x > 0)
                    n += x;
            return n;
        }

        /// <summary>
        /// What waited on the player before the inbox (0.83's WeekModel.NeedsCount): every colony's petitions, the open
        /// wishes, Rimfolk's letters waiting for an answer, the promises at risk.
        /// </summary>
        public static int Before(int petitions, int wishes, int letters, int promises) => Count(new[] { petitions, wishes, letters, promises });

        // ---------------- The order of rows ----------------

        /// <summary>
        /// The order of a section's rows: provider by provider in the providers' order, and within each provider by the
        /// rows' own order, as each of This week's cards always sorted them; equals keep the order they were found in.
        /// Returns the indexes in drawing order.
        /// </summary>
        public static int[] Order(IList<float> providerOrder, IList<float> rowOrder)
        {
            int n = Math.Min(providerOrder?.Count ?? 0, rowOrder?.Count ?? 0);
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            // A stable sort: List.Sort isn't, so the index breaks ties.
            Array.Sort(idx, (a, b) =>
            {
                int c = providerOrder[a].CompareTo(providerOrder[b]);
                if (c != 0) return c;
                c = rowOrder[a].CompareTo(rowOrder[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            return idx;
        }

        // ---------------- Steps: what is under way, never a time left ----------------

        /// <summary>A caravan of the colony's: set out, on the road, there, on the way home (the steps' words are keyed).</summary>
        public const int CaravanSteps = 4;

        /// <summary>Where a caravan is among its steps: moving home is the last, resting at a place not home is there, else on the road.</summary>
        public static int CaravanStep(bool moving, bool headingHome, bool atPlace)
        {
            if (moving) return headingHome ? 3 : 1;
            return atPlace ? 2 : 1;
        }

        /// <summary>A mission taken on: taken on, under way (goods gathered, or the work going on), done.</summary>
        public const int MissionSteps = 3;

        /// <summary>Where a mission is: at work it is taken on; its goal met and the neighbours coming, under way.</summary>
        public static int MissionStep(int stage) => stage <= 0 ? 0 : 1;

        /// <summary>Settlers for a hamlet: at the gate, on the road, settled.</summary>
        public const int SettlerSteps = 3;

        /// <summary>Where settlers are, or -1 when they aren't under way (not yet taken in, or settled).</summary>
        public static int SettlerStep(HamletStage stage)
        {
            switch (stage)
            {
                case HamletStage.Waiting: return 0;
                case HamletStage.Escorted:
                case HamletStage.Alone: return 1;
                default: return -1;
            }
        }

        /// <summary>A journey planned ahead: planned, getting ready, at the gate, set out (JourneyStage as a number: planned, preparing, final, asking).</summary>
        public const int JourneySteps = 4;

        public static int JourneyStep(int stage) => stage <= 0 ? 0 : stage == 1 ? 1 : 2;

        private static readonly string[] TimeWords =
        {
            "day", "days", "hour", "hours", "week", "weeks", "season", "seasons", "year", "years", "left", "soon", "until", "tomorrow", "today", "minutes"
        };

        /// <summary>A step's words say how long is left (a number, or a word of time): never allowed, the player's rule.</summary>
        public static bool ShowsTimeLeft(string words)
        {
            if (string.IsNullOrEmpty(words)) return false;
            foreach (char c in words)
                if (c >= '0' && c <= '9')
                    return true;
            foreach (var w in words.ToLowerInvariant().Split(new[] { ' ', ',', '.', ';', ':', '(', ')' }, StringSplitOptions.RemoveEmptyEntries))
                if (Array.IndexOf(TimeWords, w) >= 0)
                    return true;
            // A placeholder ("{0}") would put a number in.
            return words.IndexOf('{') >= 0;
        }

        // ---------------- Coming: by date ----------------

        /// <summary>When something comes, by the calendar days of the place (days since the start, as the game counts them): today, tomorrow, or its date.</summary>
        public static DueWord When(int dayNow, int dayDue) => dayDue <= dayNow ? DueWord.Today : dayDue == dayNow + 1 ? DueWord.Tomorrow : DueWord.Date;
    }

    /// <summary>
    /// The inbox's providers (UI/Inbox.cs), kept in the order This week draws them: by section, then by the order given,
    /// then in the order they were added; one added again under its id replaces the first, keeping its place among
    /// equals. Pure, so Tools/Tests can check the order; the game's side holds what each provider gives.
    /// </summary>
    public sealed class InboxRegistry<T>
    {
        public sealed class Entry
        {
            public InboxSection section;
            public float order;
            public string id;
            public int seq;
            public T value;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<InboxSection, List<Entry>> sorted = new Dictionary<InboxSection, List<Entry>>();
        private int next;

        public int Count => entries.Count;

        public IReadOnlyList<Entry> All => entries;

        public Entry Add(InboxSection section, float order, string id, T value)
        {
            var e = new Entry { section = section, order = order, id = id, value = value, seq = next++ };
            if (!string.IsNullOrEmpty(id))
            {
                int i = entries.FindIndex(x => x.id == id);
                if (i >= 0)
                {
                    e.seq = entries[i].seq;
                    entries[i] = e;
                    sorted.Clear();
                    return e;
                }
            }
            entries.Add(e);
            sorted.Clear();
            return e;
        }

        /// <summary>One section's providers in drawing order (made once, kept until one is added).</summary>
        public List<Entry> In(InboxSection section)
        {
            if (sorted.TryGetValue(section, out var list)) return list;
            list = new List<Entry>();
            foreach (var e in entries)
                if (e.section == section)
                    list.Add(e);
            list.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.seq.CompareTo(b.seq));
            sorted[section] = list;
            return list;
        }
    }
}
