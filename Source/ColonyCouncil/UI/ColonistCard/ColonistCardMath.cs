using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The four parts of a colonist's card at a glance, in the order they are read.</summary>
    public enum CardPart
    {
        /// <summary>Their mood with its break lines and where it heads, the heart's two meters, what weighs and lifts most, the bands' effects, time off.</summary>
        Now,
        /// <summary>Every skill as a bar with its level and passion, what they are best at, the skills they can't do marked.</summary>
        Skills,
        /// <summary>The weapons in hand and carried, the apparel by slot, each with its rating; the kit's gaps.</summary>
        Kit,
        /// <summary>Their life goal, wish, quirks, standing, closest friends and rivals (the Story tab's glance).</summary>
        Life
    }

    /// <summary>Where a worn piece is listed on the card: by what it covers, the belt's layer first.</summary>
    public enum WornSlot
    {
        Head,
        Body,
        Legs,
        Hands,
        Feet,
        Belt,
        Other
    }

    /// <summary>
    /// The rules of a colonist's card (UI/ColonistCard/, docs/design/rimfolk-look.md, stage 3, point 4), free of game types so
    /// Tools/Tests try them: its sizes, how many columns its four parts take at a width and which part goes in which, how
    /// often its model is built again, the step to the previous or next colonist past those whose card is already open, the
    /// place a second card opens at beside the first, the skills' bars and what a colonist is best at, the heart's line under
    /// the portrait, the slot a worn piece is listed in, and how many rows the foot's buttons take.
    /// </summary>
    public static class ColonistCardMath
    {
        // ---------------- Sizes ----------------

        /// <summary>The least the card is sized to: the header, a column of the parts (they scroll) and the foot's buttons in two rows.</summary>
        public const float LeastW = 460f, LeastH = 440f;

        /// <summary>The size it first asks for: the four parts side by side on a 1920 by 1080 screen.</summary>
        public const float FirstW = 1100f, FirstH = 740f;

        /// <summary>A column's least width (the heart's meters, a friend's chip, a skill's bar keep their room) and the gap between columns.</summary>
        public const float ColumnLeast = 240f, ColumnGap = 16f;

        /// <summary>The most columns: the four parts side by side.</summary>
        public const int MostColumns = 4;

        /// <summary>The portrait in the header: the game's whole figure, at the size the game's own character card draws it.</summary>
        public const float PortraitW = 100f, PortraitH = 140f;

        /// <summary>The header's height: the portrait, the heart's line under it and a little room.</summary>
        public const float HeaderH = PortraitH + 14f;

        /// <summary>A skill's row, a weapon's row and a worn piece's row.</summary>
        public const float SkillRowH = 20f, WeaponRowH = 38f, WornRowH = 30f;

        /// <summary>The foot's buttons: their height and the gap between them.</summary>
        public const float FootButtonH = 28f, FootGap = 6f;

        /// <summary>The skill level the bar is full at (the game's highest).</summary>
        public const int TopLevel = 20;

        /// <summary>How many skills "best at" names, and the least level it names one from.</summary>
        public const int BestShown = 3, BestFrom = 6;

        // ---------------- How often it is built ----------------

        /// <summary>The live parts (the mood, the heart, time off) are built again twice a second; the heavy parts (skills, kit, life) every three.</summary>
        public const float LiveEvery = 0.5f, HeavyEvery = 3f;

        /// <summary>Whether a part built at <paramref name="at"/> (below 0: never) is due again at <paramref name="now"/>; a clock gone back counts as due.</summary>
        public static bool Due(float now, float at, float every) => at < 0f || now < at || now - at >= every;

        // ---------------- Columns ----------------

        /// <summary>How many columns the parts take in a body this wide: as many as keep their least, one to four.</summary>
        public static int Columns(float width)
        {
            if (float.IsNaN(width) || width <= 0f) return 1;
            int n = (int)Math.Floor((width + ColumnGap) / (ColumnLeast + ColumnGap));
            return n < 1 ? 1 : n > MostColumns ? MostColumns : n;
        }

        /// <summary>A column's width in a body this wide, in <paramref name="n"/> columns.</summary>
        public static float ColumnWidth(float width, int n) => n <= 1 ? Math.Max(0f, width) : Math.Max(0f, (width - ColumnGap * (n - 1)) / n);

        /// <summary>A column's left edge, from the body's.</summary>
        public static float ColumnX(float width, int n, int column) => column * (ColumnWidth(width, n) + ColumnGap);

        private static readonly CardPart[][] One = { new[] { CardPart.Now, CardPart.Skills, CardPart.Kit, CardPart.Life } };
        private static readonly CardPart[][] Two = { new[] { CardPart.Now, CardPart.Skills }, new[] { CardPart.Kit, CardPart.Life } };
        private static readonly CardPart[][] Three = { new[] { CardPart.Now, CardPart.Skills }, new[] { CardPart.Kit }, new[] { CardPart.Life } };
        private static readonly CardPart[][] Four = { new[] { CardPart.Now }, new[] { CardPart.Skills }, new[] { CardPart.Kit }, new[] { CardPart.Life } };

        /// <summary>
        /// Which parts go in which column: one column stacks them in their order; two put how they are and what they can do
        /// on the left, their kit and their life on the right; three keep the skills under how they are; four side by side.
        /// Fixed for a number of columns, so nothing jumps from one column to another as a part grows.
        /// </summary>
        public static CardPart[][] Parts(int columns)
        {
            switch (columns <= 1 ? 1 : columns >= MostColumns ? MostColumns : columns)
            {
                case 1: return One;
                case 2: return Two;
                case 3: return Three;
                default: return Four;
            }
        }

        // ---------------- The previous and the next colonist ----------------

        /// <summary>
        /// The colonist the arrows step to from <paramref name="at"/> among <paramref name="count"/> (the colonist bar's order),
        /// <paramref name="dir"/> 1 for the next and -1 for the previous, round the ends, past those whose card is open in
        /// another window (<paramref name="taken"/>); -1 when there is none. A colonist not in the list (<paramref name="at"/>
        /// below 0) steps to the first or the last.
        /// </summary>
        public static int Step(int count, int at, int dir, Func<int, bool> taken)
        {
            if (count <= 0 || dir == 0) return -1;
            dir = dir > 0 ? 1 : -1;
            int start = at < 0 || at >= count ? (dir > 0 ? count - 1 : 0) : at;
            for (int k = 1; k <= count; k++)
            {
                int i = ((start + dir * k) % count + count) % count;
                if (i == at) return -1;
                if (taken == null || !taken(i)) return i;
            }
            return -1;
        }

        // ---------------- Several cards ----------------

        /// <summary>How far a card opens from one already open at its place, down and to the right: the title bar's height.</summary>
        public const float Cascade = 28f;

        /// <summary>
        /// Where a card opens beside the cards already open: at <paramref name="start"/> (where the last one was left), or a
        /// title bar's height down and right of it for each open card already there, so each title bar shows; back to the
        /// screen's top left corner when that runs off the screen, kept on the screen and no smaller than the least.
        /// </summary>
        public static FrameRect Cascaded(FrameRect start, IList<FrameRect> open, float screenW, float screenH, float minW, float minH, float bottom)
        {
            var r = WindowFrameMath.Clamp(start, screenW, screenH, minW, minH, bottom);
            if (open == null || open.Count == 0) return r;
            for (int k = 0; k <= open.Count; k++)
            {
                bool clash = false;
                foreach (var o in open)
                    if (Math.Abs(o.x - r.x) < Cascade / 2f && Math.Abs(o.y - r.y) < Cascade / 2f)
                    {
                        clash = true;
                        break;
                    }
                if (!clash) return r;
                var next = new FrameRect(r.x + Cascade, r.y + Cascade, r.w, r.h);
                if (next.XMax > screenW || next.YMax > screenH - bottom) next = new FrameRect(Cascade * (k + 1) % Math.Max(Cascade, screenW / 3f), 0f, r.w, r.h);
                r = WindowFrameMath.Clamp(next, screenW, screenH, minW, minH, bottom);
            }
            return r;
        }

        // ---------------- The header ----------------

        /// <summary>
        /// The heart's line under the portrait, as the colonist bar draws it under theirs: strain and the good side each as long
        /// as it is full (out of 100) across the portrait's width, shown from the bar's own threshold; 0 for a side that doesn't show.
        /// </summary>
        public static float HeartLine(float value, float width) =>
            float.IsNaN(value) || value < BarBadgeMath.HeartLineFrom || width <= 0f ? 0f : width * Clamp01(value / 100f);

        // ---------------- Skills ----------------

        /// <summary>A skill's bar: its level out of the game's highest.</summary>
        public static float SkillFill(int level) => Clamp01(level / (float)TopLevel);

        /// <summary>
        /// What they are best at: the skills they can do at <see cref="BestFrom"/> or more, highest first, a burning passion
        /// before an interest at the same level, then the game's order; at most <see cref="BestShown"/>. Indices into the lists.
        /// </summary>
        public static List<int> Best(IList<int> levels, IList<int> passions, IList<bool> disabled)
        {
            var list = new List<int>();
            if (levels == null) return list;
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] >= BestFrom && (disabled == null || i >= disabled.Count || !disabled[i]))
                    list.Add(i);
            int P(int i) => passions == null || i >= passions.Count ? 0 : passions[i];
            list.Sort((a, b) => levels[a] != levels[b] ? levels[b].CompareTo(levels[a]) : P(a) != P(b) ? P(b).CompareTo(P(a)) : a.CompareTo(b));
            if (list.Count > BestShown) list.RemoveRange(BestShown, list.Count - BestShown);
            return list;
        }

        // ---------------- Kit ----------------

        /// <summary>
        /// The slot a worn piece is listed in: a belt's layer first (a shield belt, a pack), then what it covers, the body
        /// before the legs before the head (a duster or a suit is clothing, not a hat), the hands, the feet, and the rest.
        /// </summary>
        public static WornSlot SlotOf(bool beltLayer, bool torso, bool legs, bool head, bool hands, bool feet)
        {
            if (beltLayer) return WornSlot.Belt;
            if (torso) return WornSlot.Body;
            if (legs) return WornSlot.Legs;
            if (head) return WornSlot.Head;
            if (hands) return WornSlot.Hands;
            if (feet) return WornSlot.Feet;
            return WornSlot.Other;
        }

        /// <summary>The order the slots are listed in: head to feet, then the belt and the rest.</summary>
        public static readonly WornSlot[] SlotOrder = { WornSlot.Head, WornSlot.Body, WornSlot.Legs, WornSlot.Hands, WornSlot.Feet, WornSlot.Belt, WornSlot.Other };

        /// <summary>
        /// Whether what they wear keeps them comfortable through the season where they are: too cold when the season's
        /// coldest goes below their least comfortable temperature, too hot when its hottest goes above their most (0: fine,
        /// -1: too cold, 1: too hot, 2: both), by more than a degree.
        /// </summary>
        public static int Weather(float comfyMin, float comfyMax, float coldest, float hottest)
        {
            bool cold = coldest < comfyMin - 1f, hot = hottest > comfyMax + 1f;
            return cold && hot ? 2 : cold ? -1 : hot ? 1 : 0;
        }

        // ---------------- The foot ----------------

        /// <summary>How many rows the foot's buttons take in a width, each row as many as fit in order.</summary>
        public static int FootRows(IList<float> widths, float width, float gap)
        {
            if (widths == null || widths.Count == 0) return 0;
            int rows = 1;
            float x = 0f;
            foreach (float w in widths)
            {
                if (x > 0f && x + w > width + 0.01f)
                {
                    rows++;
                    x = 0f;
                }
                x += w + gap;
            }
            return rows;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
