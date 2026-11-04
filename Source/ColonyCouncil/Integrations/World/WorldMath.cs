using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules shared by the adapters for other mods' world mechanics (WorldMechanics.cs): the history's kinds for
    /// what those mods do to the world, which of it the history keeps and how much it weighs, which home a holding
    /// belongs to, how far along a settlement being built is, and a battle decided after it was written. Pure (no game
    /// types): Tools/Tests checks them.
    /// </summary>
    public static class WorldMath
    {
        /// <summary>A people began building a settlement (place: the tile).</summary>
        public const string Founded = "world:founded";
        /// <summary>A settlement being built was finished: a new town (place: the town; about: the tile it was built on).</summary>
        public const string Built = "world:built";
        /// <summary>A settlement being built was destroyed by those who attacked it (place: the tile).</summary>
        public const string Razed = "world:razed";
        /// <summary>A town taken by another people, ours among them when a vassal of ours is taken (place: the town now; about: the town it was).</summary>
        public const string Taken = "world:taken";
        /// <summary>A town made a colony's vassal (place: the vassal; about: the town it was, when known).</summary>
        public const string Vassal = "world:vassal";
        /// <summary>Two peoples fought on one of the colony's homes (place: the colony). Both are "with" until one is known to have won: then the winner "did" and the loser was "against".</summary>
        public const string Battle = "world:battle";

        /// <summary>Every kind the adapters write, all in the family "world" (the chronicle's kind for world events).</summary>
        public static readonly string[] Kinds = { Founded, Built, Razed, Taken, Vassal, Battle };

        /// <summary>A battle is matched to its outcome within this long of its start.</summary>
        public const int BattleDays = 3;

        /// <summary>
        /// The history keeps what touches the colony (its homes, its vassals, a site it attacked) wherever it is, and the
        /// rest only within the region map's reach of a home; beyond it the mods keep their own world.
        /// </summary>
        public static bool Records(bool inReach, bool ours) => ours || inReach;

        /// <summary>What touches the colony is a milestone (kept, never folded); the region's other comings and goings are everyday.</summary>
        public static int Weight(bool ours) => ours ? HistoryMath.Milestone : HistoryMath.Everyday;

        /// <summary>A place lies within reach of a home: no farther than <paramref name="reach"/> tiles from one of them.</summary>
        public static bool InReach(IReadOnlyList<float> distances, float reach)
        {
            if (distances == null) return false;
            for (int i = 0; i < distances.Count; i++)
                if (distances[i] <= reach)
                    return true;
            return false;
        }

        /// <summary>
        /// Which home a holding (a vassal, an outpost) belongs to, by index into <paramref name="homes"/>: the home it names
        /// itself (an outpost's delivery colony) while that is still a home, else the nearest one (the first of equals),
        /// else -1 (no home).
        /// </summary>
        public static int HomeFor(int named, IReadOnlyList<int> homes, IReadOnlyList<float> distances)
        {
            if (homes == null || homes.Count == 0) return -1;
            if (named != HomesMath.None)
                for (int i = 0; i < homes.Count; i++)
                    if (homes[i] == named)
                        return i;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < homes.Count; i++)
            {
                float d = distances != null && i < distances.Count ? distances[i] : float.MaxValue;
                if (best >= 0 && d >= bestDistance) continue;
                best = i;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>How far along a settlement being built is: 0 at its start, 1 when it is due, in between by the clock.</summary>
        public static float Progress(int started, int completes, int now)
        {
            if (completes <= started) return now >= completes ? 1f : 0f;
            if (now <= started) return 0f;
            if (now >= completes) return 1f;
            return (now - started) / (float)(completes - started);
        }

        /// <summary>
        /// The undecided battle between these two peoples at this place, begun no more than <see cref="BattleDays"/> before
        /// <paramref name="now"/>: the newest of <paramref name="newestFirst"/> (a place's events, newest first) with both
        /// still "with". Null for none.
        /// </summary>
        public static HistoryEntry OpenBattle(IReadOnlyList<HistoryEntry> newestFirst, Ref a, Ref b, int now)
        {
            if (newestFirst == null || a.IsNone || b.IsNone) return null;
            int since = now - BattleDays * HistoryMath.DayTicks;
            for (int i = 0; i < newestFirst.Count; i++)
            {
                var e = newestFirst[i];
                if (e == null) continue;
                if (e.tick < since) break;
                if (e.kind != Battle) continue;
                if (e.RoleOf(a) == HistoryRole.With && e.RoleOf(b) == HistoryRole.With) return e;
            }
            return null;
        }

        /// <summary>
        /// A battle already decided between these two at this place, in the same span: the newest with one of them as the
        /// one who "did". Null for none.
        /// </summary>
        public static HistoryEntry DecidedBattle(IReadOnlyList<HistoryEntry> newestFirst, Ref a, Ref b, int now)
        {
            if (newestFirst == null || a.IsNone || b.IsNone) return null;
            int since = now - BattleDays * HistoryMath.DayTicks;
            for (int i = 0; i < newestFirst.Count; i++)
            {
                var e = newestFirst[i];
                if (e == null) continue;
                if (e.tick < since) break;
                if (e.kind != Battle || !e.Involves(a) || !e.Involves(b)) continue;
                if (e.RoleOf(a) == HistoryRole.Did || e.RoleOf(b) == HistoryRole.Did) return e;
            }
            return null;
        }

        /// <summary>A battle's outcome: the winner did, the loser was against. False when either isn't in it.</summary>
        public static bool Decide(HistoryBook book, HistoryEntry battle, Ref winner, Ref loser)
        {
            if (book == null || battle == null || !battle.Involves(winner) || !battle.Involves(loser) || winner == loser) return false;
            book.SetRole(battle, winner, HistoryRole.Did);
            book.SetRole(battle, loser, HistoryRole.Against);
            return true;
        }
    }
}
