using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules of the titles and rites in invitations (docs/design/stage1-region.md, item 7): which of a people's
    /// rituals fits an occasion (a feast, a wedding, a funeral), read from the ritual's names as the game gives them, and
    /// how a host or a guest is called by a title. Pure (no game types): Tools/Tests (TravelTests) checks them.
    /// </summary>
    public static class InviteMath
    {
        /// <summary>A ritual that is a feast, a party, a dance or a festival (the calendar's own list, CalendarKeeping.FeastWords).</summary>
        public static readonly string[] FeastWords =
        {
            "feast", "party", "celebrat", "festival", "dance", "drum", "skylantern", "banquet", "bonfire", "concert"
        };

        public static readonly string[] WeddingWords = { "marriage", "wedding" };
        public static readonly string[] FuneralWords = { "funeral", "memorial" };

        /// <summary>What a ritual is for, from its names (its def, its pattern, its group, lower case): "funeral", "wedding", "feast", or null.</summary>
        public static string RiteKind(string names)
        {
            if (string.IsNullOrEmpty(names)) return null;
            string n = names.ToLowerInvariant();
            foreach (var w in FuneralWords)
                if (n.Contains(w))
                    return "funeral";
            foreach (var w in WeddingWords)
                if (n.Contains(w))
                    return "wedding";
            foreach (var w in FeastWords)
                if (n.Contains(w))
                    return "feast";
            return null;
        }

        /// <summary>
        /// The kinds of ritual that fit an occasion, best first: a feast or midwinter wants a feast; a wedding its own rite,
        /// else a feast; a funeral its own rite only. None for anything else.
        /// </summary>
        public static string[] Fits(string occasion)
        {
            switch (occasion)
            {
                case "feast":
                case "midwinter": return new[] { "feast" };
                case "wedding": return new[] { "wedding", "feast" };
                case "funeral": return new[] { "funeral" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// Of the rituals given by their kinds (in the order the faith lists them), the index of the best for the occasion,
        /// or -1 when none fits.
        /// </summary>
        public static int Best(string occasion, IList<string> kinds)
        {
            if (kinds == null) return -1;
            foreach (var want in Fits(occasion))
                for (int i = 0; i < kinds.Count; i++)
                    if (kinds[i] == want)
                        return i;
            return -1;
        }

        /// <summary>The age-form feature an occasion speaks of when their faith gives no rite of its own (Ages.Form): a wedding's marriage, a feast's feast; none for a funeral.</summary>
        public static AgeFeature? Feature(string occasion)
        {
            switch (occasion)
            {
                case "feast":
                case "midwinter": return AgeFeature.Feast;
                case "wedding": return AgeFeature.Marriage;
                default: return null;
            }
        }

        /// <summary>A royal title before a name ("Baroness Ana Bell"); the name alone without one.</summary>
        public static string Titled(string title, string name) =>
            string.IsNullOrEmpty(title) ? name ?? "" : string.IsNullOrEmpty(name) ? title : char.ToUpperInvariant(title[0]) + title.Substring(1) + " " + name;
    }
}
