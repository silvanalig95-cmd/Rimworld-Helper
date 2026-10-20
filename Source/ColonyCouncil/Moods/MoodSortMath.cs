using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The Mood tab's groups, in the order they are shown.</summary>
    public enum MoodGroup
    {
        /// <summary>Bad moods the player can do something about now.</summary>
        Fix = 0,
        /// <summary>Bad memories fading on their own.</summary>
        Pass = 1,
        /// <summary>Bad moods from their faith, their traits or genes, and permanent ones.</summary>
        Faith = 2,
        /// <summary>The good moods.</summary>
        Lifting = 3
    }

    /// <summary>What a mood's hint says about acting on it.</summary>
    public enum HintAction
    {
        /// <summary>No hint, or one without a fix.</summary>
        None,
        /// <summary>Doing something now ends it, or keeps it from coming straight back.</summary>
        Actionable,
        /// <summary>It has advice, but the mood itself only fades.</summary>
        Fades
    }

    /// <summary>The short line a mood without a short action of its own gets, by kind.</summary>
    public enum MoodFallback
    {
        /// <summary>"While it lasts": a mood that stays while its cause does.</summary>
        WhileItLasts,
        /// <summary>"Fades in 2 days": a memory.</summary>
        Fades,
        /// <summary>"Their faith": a precept's mood.</summary>
        Faith,
        /// <summary>"Their nature": a trait's or gene's mood.</summary>
        Nature,
        /// <summary>"Lasting": a mood that never fades.</summary>
        Lasting
    }

    /// <summary>What the third group holds, for its name: faith, who they are, or both.</summary>
    public enum ThirdKind
    {
        Faith,
        Nature,
        Both
    }

    /// <summary>
    /// How the compact Mood tab sorts a colonist's moods by what the player can do about them, free of game types so the
    /// tests can check it (Tools/Tests). A bad mood goes to "You can fix" when its hint has a fix that acting on now
    /// helps (or, without a hint, while its cause lasts), to "Will pass" when it only fades, and to "Faith and who they
    /// are" when it comes from a belief, a trait or a gene, or never fades. Good moods are "Lifting". Each group is
    /// ordered by size, biggest first.
    /// </summary>
    public static class MoodSortMath
    {
        public const int GroupCount = 4;

        /// <summary>MoodLine.ticksLeft for a mood that lasts while its cause does.</summary>
        public const int WhileCause = -1;

        /// <summary>MoodLine.ticksLeft for a mood that never fades.</summary>
        public const int Permanent = -2;

        /// <summary>A hint's short action (MoodHintDef.act): at most this many characters...</summary>
        public const int MaxActLength = 40;

        /// <summary>...and from two to this many words.</summary>
        public const int MaxActWords = 6;

        /// <summary>A compact row: its height, the column for the number, the gaps, and the least room the label keeps.</summary>
        public const float RowHeight = 24f, NumberWidth = 40f, Gap = 8f, MinLabel = 110f;

        /// <summary>The short action never takes more than this share of the row after the number.</summary>
        public const float MaxActShare = 0.5f;

        /// <summary>The good moods named in the folded "Lifting" line before "and N more".</summary>
        public const int NamedLifting = 3;

        /// <summary>
        /// The group for one mood. <paramref name="ticksLeft"/>: ticks until a memory fades, <see cref="WhileCause"/> or
        /// <see cref="Permanent"/>. A hint that says acting helps wins over faith and nature (a faith's missing altar can
        /// be built), but not over a mood that never fades.
        /// </summary>
        public static MoodGroup GroupOf(float mood, int ticksLeft, bool faith, bool nature, HintAction hint)
        {
            if (mood > 0f) return MoodGroup.Lifting;
            if (ticksLeft == Permanent) return MoodGroup.Faith;
            if (hint == HintAction.Actionable) return MoodGroup.Fix;
            if (faith || nature) return MoodGroup.Faith;
            if (ticksLeft >= 0) return MoodGroup.Pass;
            // While its cause lasts: something to change, unless its hint says it only passes (a world event, a quirk).
            return hint == HintAction.Fades ? MoodGroup.Pass : MoodGroup.Fix;
        }

        /// <summary>The short line for a mood without a short action of its own.</summary>
        public static MoodFallback FallbackOf(int ticksLeft, bool faith, bool nature)
        {
            if (faith) return MoodFallback.Faith;
            if (ticksLeft == Permanent) return MoodFallback.Lasting;
            if (nature) return MoodFallback.Nature;
            return ticksLeft >= 0 ? MoodFallback.Fades : MoodFallback.WhileItLasts;
        }

        /// <summary>
        /// Whether a row shows its hint's short action (true) or when it ends (false). Fading moods and good ones say
        /// when they end; the rest say what to do, or what they are.
        /// </summary>
        public static bool ShowsAct(MoodGroup g, bool hasAct, int ticksLeft)
        {
            if (!hasAct) return false;
            if (g == MoodGroup.Lifting) return false;
            if (g == MoodGroup.Pass) return ticksLeft < 0;
            return true;
        }

        /// <summary>A short action is written in the accent colour only where it is something to do.</summary>
        public static bool Gold(MoodGroup g, bool showsAct) => showsAct && g == MoodGroup.Fix;

        /// <summary>Order within a group: biggest first (most negative for the bad, most positive for the good), then by name.</summary>
        public static int Compare(MoodGroup g, float a, string labelA, float b, string labelB)
        {
            int c = g == MoodGroup.Lifting ? b.CompareTo(a) : a.CompareTo(b);
            return c != 0 ? c : string.CompareOrdinal(labelA ?? "", labelB ?? "");
        }

        /// <summary>Sorts one group's rows in place.</summary>
        public static void Sort<T>(List<T> rows, MoodGroup g, Func<T, float> mood, Func<T, string> label)
        {
            rows.Sort((x, y) => Compare(g, mood(x), label(x), mood(y), label(y)));
        }

        /// <summary>
        /// When the last of the fading moods is gone, in ticks: the latest of them, or -1 when one of them has no end in
        /// sight (a world event) or there are none.
        /// </summary>
        public static int LastGone(IEnumerable<int> ticksLeft)
        {
            int last = -1;
            bool any = false;
            foreach (int t in ticksLeft)
            {
                if (t < 0) return -1;
                any = true;
                if (t > last) last = t;
            }
            return any ? last : -1;
        }

        /// <summary>The largest mood shown, either way, which the faint bars behind the rows are scaled to.</summary>
        public static float Largest(IEnumerable<float> moods)
        {
            float best = 0f;
            foreach (float m in moods) best = Math.Max(best, Math.Abs(m));
            return best;
        }

        /// <summary>How much of the row the faint bar behind a mood fills: its share of the largest, never more than all of it.</summary>
        public static float BarFraction(float mood, float largest) => largest <= 0f ? 0f : Math.Min(1f, Math.Abs(mood) / largest);

        /// <summary>What the third group holds, from how many of its moods come from faith and how many don't.</summary>
        public static ThirdKind ThirdOf(int faith, int other) => faith > 0 && other > 0 ? ThirdKind.Both : faith > 0 ? ThirdKind.Faith : ThirdKind.Nature;

        /// <summary>How many good moods the folded "Lifting" line names: all of them when one more would be "and 1 more".</summary>
        public static int Named(int count) => count <= NamedLifting + 1 ? Math.Max(0, count) : NamedLifting;

        /// <summary>
        /// Where a compact row's parts go at this width, with the short action wanting <paramref name="actWanted"/>: the
        /// number on the left, the label after it, the short action right-aligned at the end. The action gets at most
        /// half the room and never squeezes the label below <see cref="MinLabel"/>; whatever doesn't fit is cut with "...".
        /// </summary>
        public static void RowLayout(float width, float actWanted, out float labelX, out float labelW, out float actX, out float actW)
        {
            labelX = NumberWidth + Gap;
            float room = Math.Max(0f, width - labelX - 4f);
            float most = Math.Max(0f, Math.Min(room * MaxActShare, room - Gap - MinLabel));
            actW = actWanted <= 0f ? 0f : Math.Min(actWanted, most);
            actX = width - 4f - actW;
            labelW = actW > 0f ? Math.Max(0f, actX - Gap - labelX) : room;
        }

        // ---------------- The compact tab's layout (ITab_Pawn_Mood) ----------------

        /// <summary>The tab's padding, and its title row and mood bar, which always stay at the top.</summary>
        public const float TabPadding = 10f, TitleAndBar = 56f;

        /// <summary>With at least this much room under the bar, the state sentence stays at the top with the chips; with less, it scrolls.</summary>
        public const float StateFixedRoom = 300f;

        public static bool StateFixed(float roomUnderBar) => roomUnderBar >= StateFixedRoom;

        /// <summary>How tall the compact tab's scrolling part is, in a tab this tall with a state sentence and chips this tall.</summary>
        public static float ScrollHeight(float tabHeight, float stateH, float chipsH)
        {
            float room = tabHeight - 2f * TabPadding - TitleAndBar;
            if (StateFixed(room)) room -= stateH + 6f;
            return Math.Max(0f, room - chipsH - 8f);
        }

        /// <summary>How many compact rows a scrolling part this tall shows at once.</summary>
        public static int RowsInView(float scrollHeight) => (int)(Math.Max(0f, scrollHeight) / (RowHeight + 1f));

        /// <summary>Whether a row from <paramref name="y"/>, <paramref name="h"/> tall, can be seen in a scroll view showing <paramref name="top"/> to top + view.</summary>
        public static bool InView(float y, float h, float top, float view) => y + h >= top && y <= top + view;

        /// <summary>What's wrong with a hint's short action, or null when it's fine (the tests check every hint).</summary>
        public static string ActProblem(string act)
        {
            if (act == null || act.Trim().Length == 0) return "empty";
            if (act.IndexOf('\n') >= 0 || act.IndexOf('\r') >= 0) return "has a line break";
            if (act != act.Trim()) return "has spaces at an end";
            if (act.Length > MaxActLength) return "longer than " + MaxActLength + " characters (" + act.Length + ")";
            if (act.EndsWith(".")) return "ends with a full stop";
            if (act.EndsWith("!")) return "ends with an exclamation mark";
            int words = act.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words < 2 || words > MaxActWords) return words + " words (2 to " + MaxActWords + ")";
            if (!char.IsUpper(act[0])) return "doesn't start with a capital";
            return null;
        }
    }
}
