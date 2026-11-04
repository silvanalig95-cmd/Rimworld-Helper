using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// A people's memory of single colonists (docs/design/stage1-region.md, item 7; decision 13), the rules without the
    /// game: what each colonist is remembered for (a few marks per people and colonist: what, how big, when, how often),
    /// how strongly it is remembered now (each mark fades as the peoples' deeds do, MemoryMath), the bounds, and what it
    /// does: a name for talk and letters ({FRIEND}, {FOE}), a notable's regard of that colonist, and a trader who won't deal
    /// with someone their people hold a death against. Pure: Tools/Tests (FollowUpTests) checks it.
    /// </summary>
    public static class ColonistMemoryMath
    {
        public const int DayTicks = 60000;

        /// <summary>Marks kept for one colonist by one people: past that, the faintest goes.</summary>
        public const int MaxMarks = 4;
        /// <summary>Colonists one people remembers by name: past that, the one remembered least goes.</summary>
        public const int MaxPerPeople = 6;
        /// <summary>Colonists remembered by name over every people.</summary>
        public const int MaxAll = 48;

        /// <summary>A standing this strong (either way) names the colonist: their people's {FRIEND} or {FOE}.</summary>
        public const float Named = 4f;
        /// <summary>A standing this low and their traders won't deal with that colonist.</summary>
        public const float RefuseAt = -12f;
        /// <summary>What a notable makes of a colonist from what their people remember: 1.5 a point, 40 at most either way.</summary>
        public const float RegardFactor = 1.5f, RegardCap = 40f;
        /// <summary>A raiders' camp broken: the peoples at peace with the colony with a town this near it remember who broke it, the nearest so many.</summary>
        public const float TroubledTiles = 20f;
        public const int MaxTroubled = 4;
        /// <summary>Colonists named for a camp broken (those who fought on its map), and peoples whose fighters stood beside a colonist in one fight.</summary>
        public const int MaxFought = 12, MaxBeside = 3;
        /// <summary>Their fighters within this many cells of a colonist's kill fought beside them.</summary>
        public const float BesideCells = 30f;

        /// <summary>One thing a colonist is remembered for.</summary>
        public sealed class Mark
        {
            public string kind;
            /// <summary>1: a good turn, -1: a wrong.</summary>
            public int sign;
            public int weight;
            public int tick;
            public int count = 1;
        }

        /// <summary>A kind of mark of this memory's own: which way, how big, how close together two count as one, and the most a run adds up to.</summary>
        public struct Kind
        {
            public int sign;
            public int weight;
            public float mergeDays;
            public int max;
        }

        private static Kind K(int sign, int weight, float mergeDays = 0f, int max = MemoryMath.MaxWeight) =>
            new Kind { sign = sign, weight = weight, mergeDays = mergeDays, max = max };

        /// <summary>
        /// The marks this memory makes itself. Deeds a people already remembers by a colonist's name (a prisoner set free,
        /// a wounded guest tended, hard words, a brawl: World/KinMemory.cs) come in with their own kind and weight.
        /// </summary>
        public static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            // Good turns.
            ["campBroken"] = K(1, 12, 0f, 24),
            ["foughtBeside"] = K(1, 6, 2f, 18),
            ["tradedPost"] = K(1, 2, 15f, 10),
            ["walkedSettlers"] = K(1, 14, 0f, 28),
            // Wrongs: one of theirs killed in a fight (in their raid on the colony, elsewhere, or at peace), a prisoner executed or cut open.
            ["killedRaid"] = K(-1, 2, 2f, 10),
            ["killedField"] = K(-1, 6, 2f, 24),
            ["killedPeace"] = K(-1, 15, 2f, 30),
            ["executed"] = K(-1, 25, 0f, 40),
            ["harvested"] = K(-1, 20, 0f, 40)
        };

        /// <summary>The kind of killing a death was: in their raid on the colony, out on other ground in a fight, or at peace.</summary>
        public static string KillKind(bool hostile, bool raidOnUs) => !hostile ? "killedPeace" : raidOnUs ? "killedRaid" : "killedField";

        /// <summary>The words of a mark's kind ("killed", "executed", "campbroken"), as a tag (lower case).</summary>
        public static string Tag(string kind)
        {
            switch (kind)
            {
                case "killedRaid":
                case "killedField":
                case "killedPeace": return "killed";
                case "released": return "freed";
                case "slighted": return "insulted";
                default: return (kind ?? "").ToLowerInvariant();
            }
        }

        // ---------------- One colonist's marks ----------------

        public static float Strength(Mark m, int now) => m == null ? 0f : MemoryMath.Strength(m.weight, now - m.tick);

        /// <summary>How the people stand toward the colonist now: the good marks less the wrongs, each faded.</summary>
        public static float Standing(List<Mark> marks, int now)
        {
            float s = 0f;
            if (marks == null) return s;
            foreach (var m in marks)
                if (m != null)
                    s += Math.Sign(m.sign) * Strength(m, now);
            return s;
        }

        /// <summary>The good marks alone (with <paramref name="sign"/> 1) or the wrongs alone (-1), as a size.</summary>
        public static float Sum(List<Mark> marks, int now, int sign)
        {
            float s = 0f;
            if (marks == null) return s;
            foreach (var m in marks)
                if (m != null && Math.Sign(m.sign) == sign)
                    s += Strength(m, now);
            return s;
        }

        /// <summary>The strongest mark on one side, or null.</summary>
        public static Mark Best(List<Mark> marks, int now, int sign)
        {
            Mark best = null;
            float bs = 0f;
            if (marks == null) return null;
            foreach (var m in marks)
            {
                if (m == null || Math.Sign(m.sign) != sign) continue;
                float s = Strength(m, now);
                if (best == null || s > bs)
                {
                    best = m;
                    bs = s;
                }
            }
            return best;
        }

        /// <summary>Everything faded below what is still remembered (MemoryMath.Forgotten).</summary>
        public static bool Faded(List<Mark> marks, int now)
        {
            if (marks == null) return true;
            foreach (var m in marks)
                if (m != null && Strength(m, now) >= MemoryMath.Forgotten)
                    return false;
            return true;
        }

        /// <summary>
        /// A new mark: the same kind on the same side within <paramref name="mergeDays"/> adds to the last (MemoryMath.Merged,
        /// at most <paramref name="max"/>), else it stands alone, and past <see cref="MaxMarks"/> the faintest goes.
        /// Returns the mark it went into.
        /// </summary>
        public static Mark Add(List<Mark> marks, string kind, int sign, int weight, int now, float mergeDays = 0f, int max = MemoryMath.MaxWeight)
        {
            if (marks == null || string.IsNullOrEmpty(kind) || sign == 0 || weight <= 0) return null;
            sign = Math.Sign(sign);
            int cap = Math.Max(1, Math.Min(max, MemoryMath.MaxWeight));
            if (mergeDays > 0f)
                foreach (var m in marks)
                    if (m != null && m.kind == kind && Math.Sign(m.sign) == sign && now - m.tick <= mergeDays * DayTicks)
                    {
                        m.weight = MemoryMath.Merged(m.weight, now - m.tick, weight, cap);
                        m.count++;
                        m.tick = now;
                        return m;
                    }
            var mark = new Mark { kind = kind, sign = sign, weight = Math.Max(1, Math.Min(cap, weight)), tick = now };
            marks.Add(mark);
            while (marks.Count > MaxMarks)
            {
                int weakest = -1;
                float ws = float.MaxValue;
                for (int i = 0; i < marks.Count; i++)
                {
                    float s = marks[i] == null ? -1f : Strength(marks[i], now);
                    if (marks[i] != mark && s < ws)
                    {
                        weakest = i;
                        ws = s;
                    }
                }
                if (weakest < 0) break;
                marks.RemoveAt(weakest);
            }
            return mark;
        }

        /// <summary>A mark of this memory's own kind (<see cref="Kinds"/>), at its weight times <paramref name="factor"/>; null for a kind it doesn't know.</summary>
        public static Mark AddKind(List<Mark> marks, string kind, int now, float factor = 1f)
        {
            if (kind == null || !Kinds.TryGetValue(kind, out var k)) return null;
            return Add(marks, kind, k.sign, Math.Max(1, (int)Math.Round(k.weight * factor)), now, k.mergeDays, k.max);
        }

        // ---------------- The marks as text (the ledger's tie keeps them in its key) ----------------

        public static string Pack(List<Mark> marks)
        {
            if (marks == null || marks.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (var m in marks)
            {
                if (m == null || string.IsNullOrEmpty(m.kind) || m.kind.IndexOf(':') >= 0 || m.kind.IndexOf(';') >= 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(m.kind).Append(':').Append(m.sign.ToString(CultureInfo.InvariantCulture)).Append(':').Append(m.weight.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(m.tick.ToString(CultureInfo.InvariantCulture)).Append(':').Append(m.count.ToString(CultureInfo.InvariantCulture));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        public static List<Mark> Unpack(string text)
        {
            var list = new List<Mark>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var part in text.Split(';'))
            {
                var f = part.Split(':');
                if (f.Length < 5 || string.IsNullOrEmpty(f[0])) continue;
                if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sign) || sign == 0) continue;
                if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) || weight <= 0) continue;
                if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tick)) continue;
                if (!int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count < 1) count = 1;
                list.Add(new Mark { kind = f[0], sign = Math.Sign(sign), weight = weight, tick = tick, count = count });
            }
            return list;
        }

        // ---------------- What it does ----------------

        /// <summary>Named as their people's friend (a standing of <see cref="Named"/> or more) or foe (as low or lower).</summary>
        public static bool Friend(float standing) => standing >= Named;

        public static bool Foe(float standing) => standing <= -Named;

        /// <summary>Their traders won't deal with this colonist.</summary>
        public static bool Refuses(float standing) => standing <= RefuseAt;

        /// <summary>What one of their notables makes of the colonist from it: <see cref="RegardFactor"/> a point, <see cref="RegardCap"/> at most either way.</summary>
        public static float Regard(float standing) => Math.Max(-RegardCap, Math.Min(RegardCap, standing * RegardFactor));

        /// <summary>
        /// Which of a people's (or everyone's) remembered colonists goes when there are too many: the one remembered least
        /// strongly either way. -1 for none.
        /// </summary>
        public static int Weakest(IList<float> standings)
        {
            int best = -1;
            for (int i = 0; i < standings.Count; i++)
                if (best < 0 || Math.Abs(standings[i]) < Math.Abs(standings[best]))
                    best = i;
            return best;
        }
    }
}
