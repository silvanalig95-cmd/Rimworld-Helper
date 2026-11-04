using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>What a click on a follow pin did.</summary>
    public enum FollowChange : byte
    {
        /// <summary>Nothing: no thing, or nothing to undo.</summary>
        None,
        Followed,
        Unfollowed,
        /// <summary>Already twelve followed: one has to go first.</summary>
        Full
    }

    /// <summary>
    /// Following (docs/design/interface.md, "Back, forward, search, following"): any thing a card shows can be followed
    /// with the pin on its card's head, at most twelve, kept with the game as a list of the things' text forms ("p123",
    /// "t55", "f3", "y7"; ThingRef). Pure, so Tools/Tests checks the bound, the toggle, the cleaning of a saved list and
    /// the colonists pinned before (kept by their ThingID, "Human123", read as the person "p123").
    /// </summary>
    public static class FollowMath
    {
        /// <summary>At most this many things followed.</summary>
        public const int Max = 12;

        /// <summary>The thing is followed.</summary>
        public static bool Has(List<string> list, ThingRef r) => list != null && !r.IsNone && list.Contains(r.ToString());

        /// <summary>Follows the thing, or stops following it; a thirteenth is refused (<see cref="FollowChange.Full"/>).</summary>
        public static FollowChange Toggle(List<string> list, ThingRef r)
        {
            if (list == null || r.IsNone) return FollowChange.None;
            string key = r.ToString();
            if (list.Remove(key)) return FollowChange.Unfollowed;
            if (list.Count >= Max) return FollowChange.Full;
            list.Add(key);
            return FollowChange.Followed;
        }

        /// <summary>
        /// A saved list made sound: what doesn't read as a thing, what is twice, and what <paramref name="keep"/> says is
        /// gone are dropped, and anything past the twelfth (an older save, a list edited by hand). Returns how many went.
        /// </summary>
        public static int Clean(List<string> list, Func<ThingRef, bool> keep = null)
        {
            if (list == null) return 0;
            int before = list.Count;
            var seen = new HashSet<string>();
            var kept = new List<string>(list.Count);
            foreach (var s in list)
            {
                if (!ThingRef.TryParse(s, out var r)) continue;
                string key = r.ToString();
                if (!seen.Add(key)) continue;
                if (keep != null && !keep(r)) continue;
                if (kept.Count >= Max) break;
                kept.Add(key);
            }
            list.Clear();
            list.AddRange(kept);
            return before - list.Count;
        }

        /// <summary>A colonist pinned before following (by their ThingID, "Human123"): the person it names ("p123"), or none.</summary>
        public static ThingRef FromThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId)) return ThingRef.None;
            int i = thingId.Length;
            while (i > 0 && thingId[i - 1] >= '0' && thingId[i - 1] <= '9') i--;
            if (i == thingId.Length || i == 0) return ThingRef.None;
            return int.TryParse(thingId.Substring(i), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                ? ThingRef.From(Ref.Person(n))
                : ThingRef.None;
        }

        /// <summary>The colonists pinned before, added to what is followed (first come first, up to twelve); returns how many came over.</summary>
        public static int TakeOldPins(List<string> following, List<string> oldPins)
        {
            if (following == null || oldPins == null) return 0;
            int n = 0;
            foreach (var id in oldPins)
            {
                var r = FromThingId(id);
                if (r.IsNone || Has(following, r) || following.Count >= Max) continue;
                following.Add(r.ToString());
                n++;
            }
            return n;
        }
    }
}
