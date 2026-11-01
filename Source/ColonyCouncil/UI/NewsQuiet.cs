using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The evening news of a quiet day: when the day's news is only a few pieces, none of them bad news (the rules in
    /// <see cref="NewsQuietMath"/>), it comes as one short message, "A quiet day in the colony", kept in the History tab,
    /// instead of a letter on the stack. A busy day comes as before, and so does a day whose lines come together as one
    /// colonist's day (Interplay/NewsDays.cs). The "Quiet days in one short message" setting turns it off; nothing is
    /// saved.
    /// </summary>
    public static class NewsQuiet
    {
        public static bool On => ColonyCouncilMod.Settings?.quietNewsDays ?? true;

        /// <summary>A piece of news the game would show in red: a bad turn, a health setback, a threat.</summary>
        public static bool Bad(NewsItem i) =>
            i?.type != null && (i.type == MessageTypeDefOf.NegativeEvent || i.type == MessageTypeDefOf.NegativeHealthEvent
                                || i.type == MessageTypeDefOf.ThreatSmall || i.type == MessageTypeDefOf.ThreatBig);

        /// <summary>The day's news folded into one short piece when the day was quiet; null when it wasn't (or the setting is off).</summary>
        public static NewsItem Fold(List<NewsItem> items)
        {
            if (!On || items == null) return null;
            int chars = items.Sum(i => i?.text?.Length ?? 0);
            if (!NewsQuietMath.Quiet(items.Count, items.Count(Bad), chars)) return null;
            string text = "CC_News_QuietDay".Translate(NewsQuietMath.Join(items.Select(i => i.text))).Resolve();
            var kind = items.GroupBy(i => i.kind).OrderByDescending(g => g.Count()).ThenBy(g => (int)g.Key).First().Key;
            var look = items.FirstOrDefault(i => i.look != null && i.look.IsValid)?.look;
            var type = items.Any(i => i.type == MessageTypeDefOf.PositiveEvent) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent;
            return new NewsItem { text = text, kind = kind, look = look, type = type };
        }
    }
}
