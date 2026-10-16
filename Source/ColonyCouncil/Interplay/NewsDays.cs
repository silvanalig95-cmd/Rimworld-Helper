using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The evening's colony news, read as people's days: when three or more pieces of the day's news are about the same
    /// colonist (a moment, a story moving on, a quirk, a visit at their sickbed...), they come together under their name
    /// ("Hector's day: ..."), in the place of the first of them, so a busy day reads as one life rather than scattered
    /// lines. Only with the evening digest and the "systems talk to each other" setting; nothing is saved.
    /// </summary>
    public static class NewsDays
    {
        /// <summary>Pieces of news about one colonist that make a day of their own.</summary>
        public const int MinLines = 3;

        /// <summary>The day's news with each busy colonist's lines brought together (NewsComponent.SendNow).</summary>
        public static List<NewsItem> Group(List<NewsItem> items)
        {
            if (!Interplay.On || items == null || items.Count <= MinLines) return items;
            try
            {
                var byPawn = new Dictionary<Pawn, List<NewsItem>>();
                foreach (var i in items)
                {
                    var p = Subject(i);
                    if (p == null) continue;
                    if (!byPawn.TryGetValue(p, out var list)) byPawn[p] = list = new List<NewsItem>();
                    list.Add(i);
                }
                if (!byPawn.Values.Any(l => l.Count >= MinLines)) return items;
                var result = new List<NewsItem>(items.Count);
                var done = new HashSet<NewsItem>();
                foreach (var i in items)
                {
                    if (done.Contains(i)) continue;
                    var p = Subject(i);
                    if (p != null && byPawn.TryGetValue(p, out var list) && list.Count >= MinLines)
                    {
                        result.Add(Merge(p, list));
                        foreach (var x in list) done.Add(x);
                        continue;
                    }
                    result.Add(i);
                }
                // A day that was all one colonist's stays a letter of lines, not one long message.
                return result.Count < 2 ? items : result;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("colony news: a colonist's day", e);
                return items;
            }
        }

        /// <summary>The colonist a piece of news is about: its first place, when that is one of the colony's people.</summary>
        private static Pawn Subject(NewsItem i)
        {
            if (i?.look == null || !i.look.IsValid) return null;
            var t = i.look.TryGetPrimaryTarget();
            return t.Thing is Pawn p && p.RaceProps != null && p.RaceProps.Humanlike && !p.Dead && p.IsColonist ? p : null;
        }

        private static NewsItem Merge(Pawn p, List<NewsItem> list)
        {
            var sb = new StringBuilder("CC_Interplay_NewsDay".Translate(p.Named("PAWN")).Resolve());
            foreach (var x in list) sb.Append("\n      · ").Append(x.text);
            // Under the heading most of it belongs to.
            var kind = list.GroupBy(x => x.kind).OrderByDescending(g => g.Count()).First().Key;
            return new NewsItem
            {
                text = sb.ToString(),
                kind = kind,
                look = p.Spawned ? new LookTargets(p) : list[0].look,
                type = list.FirstOrDefault(x => x.type != null && x.type != MessageTypeDefOf.SilentInput)?.type ?? list[0].type
            };
        }
    }
}
