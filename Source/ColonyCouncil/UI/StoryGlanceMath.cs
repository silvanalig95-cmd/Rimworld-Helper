using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The Story tab at a glance (docs/design/rimfolk-look.md, stage 3, point 2; the player, 9 November 2026: "story tab
    /// sounds great"), its rules free of game types so the tests can check them: who counts as a close friend or a rival
    /// and in what order, how many small portraits fit in a row, where trust sits on its meter and where its bands begin,
    /// and how the glance's rows share a width (UI/StoryGlance.cs draws them).
    /// </summary>
    public static class StoryGlanceMath
    {
        /// <summary>The game's own lines for a friend and a rival in its Social tab (Pawn_RelationsTracker: 20 and -20).</summary>
        public const float FriendAt = 20f, RivalAt = -20f;

        /// <summary>How many friends and how many rivals the glance names, at most; the section below has them all.</summary>
        public const int Shown = 3;

        /// <summary>Trust in the council runs from -100 to 100.</summary>
        public const float TrustMin = -100f, TrustMax = 100f;

        /// <summary>
        /// Where trust's words change (PawnStanding.LevelFor): bitter up to -45, doubtful up to -15, neutral under 25,
        /// trusting under 60, devoted from 60.
        /// </summary>
        public static readonly float[] TrustTicks = { -45f, -15f, 25f, 60f };

        /// <summary>How far along the trust meter a value sits, 0 at -100 and 1 at 100.</summary>
        public static float TrustFill(float trust) => Clamp01((trust - TrustMin) / (TrustMax - TrustMin));

        /// <summary>Someone the colonist knows: their opinion of them, and any bond Rimfolk keeps between the two.</summary>
        public struct Other
        {
            /// <summary>Their thing id, for an order that holds between frames.</summary>
            public int id;
            /// <summary>The colonist's opinion of them (the game's, -100 to 100).</summary>
            public float opinion;
            /// <summary>A bond of friendship or of fighting side by side (Rimfolk's), and one of rivalry.</summary>
            public bool friendTie, rivalTie;
            /// <summary>The strongest such bond's strength (0 to 100).</summary>
            public float tie;

            public Other(int id, float opinion, bool friendTie = false, bool rivalTie = false, float tie = 0f)
            {
                this.id = id;
                this.opinion = opinion;
                this.friendTie = friendTie;
                this.rivalTie = rivalTie;
                this.tie = tie;
            }
        }

        /// <summary>
        /// A friend: at the game's friend line or above, or bound as friends (or comrades) and not against them. A rival: at
        /// the rival line or below, or bound as rivals and not for them. Never both.
        /// </summary>
        public static bool IsFriend(Other o) => o.opinion >= FriendAt || o.friendTie && o.opinion >= 0f && !(o.rivalTie && o.opinion <= 0f);

        public static bool IsRival(Other o) => !IsFriend(o) && (o.opinion <= RivalAt || o.rivalTie && o.opinion <= 0f);

        /// <summary>The friends among them, closest first (opinion, then the bond, then who has been in the game longest): their places in the list.</summary>
        public static List<int> Friends(IList<Other> others) => Pick(others, true);

        /// <summary>The rivals among them, the worst first: their places in the list.</summary>
        public static List<int> Rivals(IList<Other> others) => Pick(others, false);

        private static List<int> Pick(IList<Other> others, bool friends)
        {
            var list = new List<int>();
            if (others == null) return list;
            for (int i = 0; i < others.Count; i++)
                if (friends ? IsFriend(others[i]) : IsRival(others[i]))
                    list.Add(i);
            list.Sort((a, b) =>
            {
                var x = others[a];
                var y = others[b];
                int c = friends ? y.opinion.CompareTo(x.opinion) : x.opinion.CompareTo(y.opinion);
                if (c != 0) return c;
                c = y.tie.CompareTo(x.tie);
                return c != 0 ? c : x.id.CompareTo(y.id);
            });
            return list;
        }

        /// <summary>
        /// How many of the chips fit in <paramref name="room"/>, in order, with <paramref name="gap"/> between them; when not
        /// all fit, room is kept for the count of the rest (<paramref name="moreWidth"/>). At most <paramref name="max"/>.
        /// </summary>
        public static int Fits(float room, IList<float> widths, float gap, float moreWidth, int max = Shown)
        {
            if (widths == null || widths.Count == 0 || room <= 0f) return 0;
            int n = Math.Min(max, widths.Count);
            // All of them, without the count.
            float all = 0f;
            for (int i = 0; i < n; i++) all += widths[i] + (i > 0 ? gap : 0f);
            if (n == widths.Count && all <= room) return n;
            // Otherwise as many as leave room for "+N".
            float used = 0f;
            int fit = 0;
            for (int i = 0; i < n; i++)
            {
                float next = used + (i > 0 ? gap : 0f) + widths[i];
                if (next + gap + moreWidth > room) break;
                used = next;
                fit++;
            }
            return fit;
        }

        // ---------------- The rows ----------------

        /// <summary>A row's icon and the space after it.</summary>
        public const float IconW = 22f;

        /// <summary>The words before a row of portraits ("Friends", "Rivals").</summary>
        public const float LabelW = 56f;

        /// <summary>The least a bar is drawn at (the life goal's, trust's).</summary>
        public const float MinBar = 60f;

        /// <summary>
        /// The life goal's second line: the bar from after the icon, its progress in words at its right, which takes at most
        /// <paramref name="share"/> of the width. Returns the bar's width and the words' width.
        /// </summary>
        public static (float bar, float words) GoalLine(float width, float wordsWidth, float share = 0.45f)
        {
            float room = Math.Max(0f, width - IconW);
            float words = Math.Min(wordsWidth, room * share);
            float bar = room - words - (words > 0f ? 6f : 0f);
            if (bar < MinBar)
            {
                bar = Math.Min(room, MinBar);
                words = Math.Max(0f, room - bar - 6f);
            }
            return (bar, words);
        }

        /// <summary>
        /// The wish's acts beside its words or under them: beside while the words keep at least <paramref name="minText"/>,
        /// under them where the width is short. Returns whether they go under, and the words' width.
        /// </summary>
        public static (bool under, float text) WishLine(float width, float actsWidth, float minText = 180f)
        {
            float text = width - IconW - actsWidth - 6f;
            if (actsWidth <= 0f) return (false, Math.Max(0f, width - IconW));
            return text >= minText ? (false, text) : (true, Math.Max(0f, width - IconW));
        }

        /// <summary>Trust's row: its words, then the bar to the end (never under <see cref="MinBar"/>, the words cut first).</summary>
        public static (float words, float bar) TrustLine(float width, float wordsWidth)
        {
            float room = Math.Max(0f, width - IconW);
            float words = Math.Min(wordsWidth, Math.Max(0f, room - MinBar - 8f));
            float bar = Math.Max(0f, room - words - 8f);
            return (words, bar);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
