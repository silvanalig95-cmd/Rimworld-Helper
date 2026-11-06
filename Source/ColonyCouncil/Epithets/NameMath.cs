using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// How one of the colony's names holds to the one who has it (Epithets.cs): since when they have it, who is clearly
    /// ahead of them now and since when, with both counts as that began, and who had it before. Kept per name and colony,
    /// packed into a line for the save (<see cref="NameMath.Pack"/>). Pure, so Tools/Tests can check it.
    /// </summary>
    public sealed class NameHold
    {
        /// <summary>When the holder was given it; -1 until the first look (an older save's name, or one given before this rule).</summary>
        public int since = -1;
        /// <summary>Who is clearly ahead of the holder now (a ThingID), or null.</summary>
        public string challenger;
        /// <summary>Since when they have been ahead.</summary>
        public int challengeFrom;
        /// <summary>The challenger's and the holder's counts when the challenge began.</summary>
        public float challengerAt, holderAt;
        /// <summary>Who had it before the holder (a ThingID), when it was taken from them, or null.</summary>
        public string lastHolder;
        /// <summary>When the last holder lost it.</summary>
        public int lostAt;

        public void Clear()
        {
            challenger = null;
            challengeFrom = 0;
            challengerAt = holderAt = 0f;
        }
    }

    /// <summary>What a day's look at a name decides: it stays with its holder, or passes to the one who outdid them.</summary>
    public enum NameMove
    {
        Keep,
        Pass
    }

    /// <summary>
    /// The rules of one slow name (phase 4 of the review, the player's decision 6 of 6 November 2026: one "known as" per
    /// colonist, the colony's name for them its short form). A name, once earned, holds: nobody can take it for
    /// <see cref="HoldDays"/>. After that it passes only to someone who clearly outdoes the holder over time: ahead on the
    /// count by a quarter at least (<see cref="Lead"/>) every day of a whole season (<see cref="ChallengeDays"/>), and over
    /// that season doing half again as much of the work as the holder (<see cref="Outdid"/>), or twice the holder's count
    /// and more of the work. A holder still at the work keeps it, and the season starts again. The one who lost a name
    /// can't take it back for a year (<see cref="ReturnDays"/>), so it never goes back and forth. And a colonist is known by
    /// one name (<see cref="Chief"/>): of the names they hold, a proud one before a dubious one, then the one held longest.
    /// </summary>
    public static class NameMath
    {
        public const int DayTicks = 60000;

        /// <summary>A name holds this many days (two seasons) before anyone can take it.</summary>
        public const int HoldDays = 30;

        /// <summary>A challenger must stay clearly ahead this many days (a season), every day of it.</summary>
        public const int ChallengeDays = 15;

        /// <summary>Clearly ahead: a quarter more on the count at least (a name's own margin when it asks for more).</summary>
        public const float ClearLead = 1.25f;

        /// <summary>Clearly outdone over the season: half again as much of the work as the holder did.</summary>
        public const float OutdoShare = 1.5f;

        /// <summary>Far ahead: twice the holder's count; then doing more of the work over the season is enough.</summary>
        public const float FarAhead = 2f;

        /// <summary>The one who lost a name can't take it back for a year.</summary>
        public const int ReturnDays = 60;

        /// <summary>The lead a challenger needs on a name with this margin.</summary>
        public static float Lead(float margin) => Math.Max(margin, ClearLead);

        /// <summary>The challenger's count is clearly ahead of the holder's.</summary>
        public static bool Ahead(float challenger, float holder, float margin) => challenger > holder && challenger >= holder * Lead(margin);

        /// <summary>
        /// Over the season the challenger did half again as much of the work as the holder, or, twice ahead on the count,
        /// more of it. Nothing done is never outdoing.
        /// </summary>
        public static bool Outdid(float challengerGain, float holderGain, bool farAhead)
        {
            if (challengerGain <= 0f) return false;
            float held = Math.Max(0f, holderGain);
            return challengerGain >= held * OutdoShare || farAhead && challengerGain > held;
        }

        /// <summary>
        /// A day's look at a name with a holder: whether it passes to <paramref name="bestId"/> (the one with the highest count
        /// who may hold it; the holder themselves when nobody is ahead). Updates the challenge in <paramref name="h"/>; an
        /// older save's name is held from this first look.
        /// </summary>
        public static NameMove Look(NameHold h, int now, string holderId, float holderCount, string bestId, float bestCount, float margin)
        {
            if (h == null) return NameMove.Keep;
            if (h.since < 0) h.since = now;
            bool ahead = bestId != null && bestId != holderId && Ahead(bestCount, holderCount, margin)
                         && !(bestId == h.lastHolder && now - h.lostAt < ReturnDays * DayTicks);
            if (!ahead || now - h.since < HoldDays * DayTicks)
            {
                h.Clear();
                return NameMove.Keep;
            }
            if (h.challenger != bestId)
            {
                Begin(h, bestId, now, bestCount, holderCount);
                return NameMove.Keep;
            }
            if (now - h.challengeFrom < ChallengeDays * DayTicks) return NameMove.Keep;
            if (Outdid(bestCount - h.challengerAt, holderCount - h.holderAt, bestCount >= holderCount * FarAhead)) return NameMove.Pass;
            // The holder kept at the work: another season to show it.
            Begin(h, bestId, now, bestCount, holderCount);
            return NameMove.Keep;
        }

        private static void Begin(NameHold h, string who, int now, float theirs, float holders)
        {
            h.challenger = who;
            h.challengeFrom = now;
            h.challengerAt = theirs;
            h.holderAt = holders;
        }

        /// <summary>A name given now: held from now, with who had it before (null for none, or one who is gone).</summary>
        public static NameHold Given(int now, string previous) => new NameHold { since = now, lastHolder = previous, lostAt = previous != null ? now : 0 };

        // ================= A colonist's one name =================

        /// <summary>One name a colonist holds: its def, since when (-1 unknown), whether it is dubious (the Kindling).</summary>
        public readonly struct Held
        {
            public readonly string defName;
            public readonly int since;
            public readonly bool dubious;

            public Held(string defName, int since, bool dubious)
            {
                this.defName = defName;
                this.since = since;
                this.dubious = dubious;
            }
        }

        /// <summary>
        /// The name a colonist is known by, of those they hold (its index, -1 for none): a proud name before a dubious one,
        /// then the one held longest (an unknown since counts as the newest), then by name, so it is the same each time and a
        /// name earned later never pushes an older one aside.
        /// </summary>
        public static int Chief(IList<Held> held)
        {
            if (held == null || held.Count == 0) return -1;
            int best = 0;
            for (int i = 1; i < held.Count; i++)
                if (Before(held[i], held[best]))
                    best = i;
            return best;
        }

        private static bool Before(Held a, Held b)
        {
            if (a.dubious != b.dubious) return !a.dubious;
            long sa = a.since < 0 ? long.MaxValue : a.since, sb = b.since < 0 ? long.MaxValue : b.since;
            if (sa != sb) return sa < sb;
            return string.CompareOrdinal(a.defName, b.defName) < 0;
        }

        // ================= Saving =================

        private const char Sep = '|';

        /// <summary>A hold as one line for the save: since|challenger|from|theirs|holder's|last|lost.</summary>
        public static string Pack(NameHold h)
        {
            if (h == null) return null;
            var sb = new StringBuilder();
            sb.Append(h.since.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(h.challenger ?? "").Append(Sep);
            sb.Append(h.challengeFrom.ToString(CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(h.challengerAt.ToString("R", CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(h.holderAt.ToString("R", CultureInfo.InvariantCulture)).Append(Sep);
            sb.Append(h.lastHolder ?? "").Append(Sep);
            sb.Append(h.lostAt.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>A hold from its line; null for one that can't be read (then the name is held from the next look).</summary>
        public static NameHold Unpack(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var p = line.Split(Sep);
            if (p.Length != 7) return null;
            var ci = CultureInfo.InvariantCulture;
            if (!int.TryParse(p[0], NumberStyles.Integer, ci, out int since) || !int.TryParse(p[2], NumberStyles.Integer, ci, out int from)
                || !float.TryParse(p[3], NumberStyles.Float, ci, out float theirs) || !float.TryParse(p[4], NumberStyles.Float, ci, out float holders)
                || !int.TryParse(p[6], NumberStyles.Integer, ci, out int lost))
                return null;
            return new NameHold
            {
                since = since,
                challenger = p[1].Length == 0 ? null : p[1],
                challengeFrom = from,
                challengerAt = theirs,
                holderAt = holders,
                lastHolder = p[5].Length == 0 ? null : p[5],
                lostAt = lost
            };
        }

        // ================= An older save's names =================

        /// <summary>A line of the history without its colour tags, for comparing words.</summary>
        public static string Plain(string line)
        {
            if (string.IsNullOrEmpty(line)) return line ?? "";
            var sb = new StringBuilder(line.Length);
            bool tag = false;
            foreach (char c in line)
            {
                if (c == '<') tag = true;
                else if (c == '>' && tag) tag = false;
                else if (!tag) sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>The history's line is the one written when a name was given (the same words, colour and case aside).</summary>
        public static bool SameLine(string historyLine, string given) =>
            !string.IsNullOrEmpty(given) && string.Equals(Plain(historyLine), Plain(given), StringComparison.OrdinalIgnoreCase);
    }
}
