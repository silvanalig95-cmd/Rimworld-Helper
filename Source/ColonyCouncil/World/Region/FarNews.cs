using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// News of far towns, brought by travellers (docs/design/stage1-region.md, item 5, "News"): the towns beyond the
    /// region kept in detail (beyond the reach of every home, within three times it, the nearest twelve) and what is true
    /// of them now: a world event about their people, a war of theirs begun or ended lately, the town new, a site near it,
    /// a quest of theirs. A camp of travellers tells two such pieces at most, each to a colonist sitting with one of them:
    /// into the far town's "Heard lately" (OutsiderBook.heard, forty at most, sixty days), the history (at the colony, the
    /// town and its people named) and the colony news. Read when told; nothing of its own is kept.
    /// </summary>
    public static class FarNews
    {
        /// <summary>The history's kind for news of a far town heard at a colony (its family "world").</summary>
        public const string Kind = "world:heard";

        public sealed class Piece
        {
            public Settlement town;
            public string kind;
            /// <summary>The news as it goes into the town's "Heard lately" ("The Lenlia are at war with the Kotar.").</summary>
            public string text;
            /// <summary>How far, in the colony's words ("twelve days' walk to the west").</summary>
            public string where;
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The far towns of a home, nearest first: beyond the reach of every home, within TravelMath.FarOuter times it.</summary>
        public static List<(Settlement town, float tiles)> Towns(Map home)
        {
            var list = new List<(Settlement, float)>();
            if (home == null || !home.Tile.Valid || Find.WorldObjects == null || Find.WorldGrid == null) return list;
            var all = Find.WorldObjects.Settlements;
            var distances = new List<float>(all.Count);
            var fit = new List<bool>(all.Count);
            var detail = RegionTowns.InDetail();
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                bool ok = RegionTowns.Fits(s) && s.Tile.Valid && s.Tile.Layer == home.Tile.Layer && !detail.Contains(s.ID);
                distances.Add(ok ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile) : float.MaxValue);
                fit.Add(ok);
            }
            float reach = RegionTowns.ReachTiles;
            foreach (int i in TravelMath.FarTownsOf(distances, fit, reach, reach * TravelMath.FarOuter))
                list.Add((all[i], distances[i]));
            return list;
        }

        /// <summary>
        /// The weightiest news of the far towns not told lately (by TravelMath.NewsRank, the nearer town first among
        /// equals); null when no far town has any.
        /// </summary>
        public static Piece Pick(Map home)
        {
            Piece best = null;
            int bestRank = int.MaxValue;
            foreach (var (town, tiles) in Towns(home))
                foreach (var p in NewsOf(town, home, tiles))
                {
                    if (p?.text == null || ToldLately(town, p.text)) continue;
                    int rank = TravelMath.NewsRank(p.kind);
                    if (rank >= bestRank) continue;
                    best = p;
                    bestRank = rank;
                }
            return best;
        }

        private static bool ToldLately(Settlement town, string text)
        {
            var book = OutsiderBook.Instance;
            if (book?.heard == null) return false;
            int now = Now;
            return book.heard.Any(h => h != null && h.town == town.ID && h.text == text && TravelMath.ToldLately(h.tick, now));
        }

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        /// <summary>What is true of a far town now, as its news.</summary>
        private static IEnumerable<Piece> NewsOf(Settlement town, Map home, float tiles)
        {
            var f = town?.Faction;
            if (f == null) yield break;
            string where = RegionWords.Where(home, town.Tile, tiles);
            Piece P(string kind, string text) => new Piece { town = town, kind = kind, text = text, where = where };
            var people = f.Name.Named("PEOPLE");
            var townName = town.Name.Named("TOWN");
            // A world event about their people, or at the town.
            var place = Refs.Of(town);
            foreach (var s in WorldSituations.Instance?.Running ?? Enumerable.Empty<WorldSituation>())
            {
                if (s?.def == null || s.ctx == null) continue;
                if (s.ctx.faction != f && s.ctx.enemy != f && s.place != place) continue;
                yield return P("situation", T("CC_FarNews_situation", people, townName, CampWords.SituationName(s).Named("WHAT")));
                break;
            }
            // A war of theirs begun lately, or one ended (as the colony saw them: OutsiderBook.wars).
            int now = Now;
            foreach (var w in OutsiderBook.Instance?.wars ?? new List<WarSeen>())
            {
                if (w == null || !w.Of(f)) continue;
                var other = w.Other(f);
                if (other == null) continue;
                if (w.end < 0 && w.start >= 0 && now - w.start < TravelMath.NewTownDays * GenDate.TicksPerDay)
                    yield return P("war", T("CC_FarNews_war", people, townName, other.Name.Named("ENEMY")));
                else if (w.end >= 0 && now - w.end < TravelMath.NewTownDays * GenDate.TicksPerDay)
                    yield return P("war", T("CC_FarNews_peace", people, townName, other.Name.Named("ENEMY")));
            }
            // The town itself new.
            if (town.creationGameTicks > 0 && now - town.creationGameTicks < TravelMath.NewTownDays * GenDate.TicksPerDay)
                yield return P("new", T("CC_FarNews_new", people, townName));
            // A site near it (a camp, a cache, ruins someone found).
            var site = Find.WorldObjects.Sites.Where(x => x != null && x.Tile.Valid && x.Tile.Layer == town.Tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(x.Tile, town.Tile) <= 10f)
                .OrderBy(x => Find.WorldGrid.ApproxDistanceInTiles(x.Tile, town.Tile)).FirstOrDefault();
            if (site != null)
                yield return P("site", T("CC_FarNews_site", people, townName, site.LabelCap.ToString().Named("SITE"), NeighbourLetters.Direction(town.Tile, site.Tile).Named("DIR")));
            // A quest of theirs nobody has taken up.
            var quest = Find.QuestManager?.QuestsListForReading.FirstOrDefault(q => q != null && !q.hidden && q.State == QuestState.NotYetAccepted && !q.name.NullOrEmpty() && q.InvolvedFactions.Contains(f));
            if (quest != null) yield return P("quest", T("CC_FarNews_quest", people, townName, quest.name.Named("QUEST")));
        }

        /// <summary>
        /// One of the camp tells a colonist sitting with them news of a far town: said aloud, into the town's "Heard
        /// lately", the history and the colony news. False when there is none to tell.
        /// </summary>
        public static bool Tell(Map home, Pawn teller, Pawn told, string camp)
        {
            if (home == null || teller == null || told == null) return false;
            // together: the camp's hour found the colonist at their fire, within talking range of the teller (Together.Can).
            var p = Pick(home);
            if (p == null) return false;
            var book = OutsiderBook.Instance;
            if (book?.heard == null) return false;
            int now = Now;
            book.heard.Add(new HeardNews
            {
                town = p.town.ID, townName = p.town.Name, faction = p.town.Faction, text = p.text, teller = teller.LabelShort, told = told.LabelShort, tick = now
            });
            while (book.heard.Count > OutsiderTalkMath.HeardMax) book.heard.RemoveAt(0);
            NotableBook.Changed();
            Speech.Say(teller, T("CC_FarNews_Say", p.town.Name.Named("TOWN")), told);
            string line = T("CC_FarNews_Told", teller.Named("X"), told.Named("A"), (camp ?? "").Named("CAMP"), p.town.Name.Named("TOWN"), p.where.Named("WHERE"), p.text.Named("NEWS"));
            var actors = new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p.town.Faction), HistoryRole.About), new HistoryActor(Refs.Of(p.town), HistoryRole.About), new HistoryActor(Refs.Of(told), HistoryRole.With)
            };
            History.Add(Kind, actors, Refs.Colony(home), HistoryMath.Everyday, line);
            ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Notable, home, line, new LookTargets(told));
            return true;
        }

        /// <summary>The far news heard lately, newest first: the entries of "Heard lately" whose town lies beyond the region kept in detail.</summary>
        public static List<HeardNews> Heard(int max)
        {
            var book = OutsiderBook.Instance;
            if (book?.heard == null || max <= 0) return new List<HeardNews>();
            var detail = RegionTowns.InDetail();
            var ids = new HashSet<int>();
            foreach (var s in Find.WorldObjects?.Settlements ?? new List<Settlement>())
                if (s != null) ids.Add(s.ID);
            return book.heard.Where(h => h != null && h.town >= 0 && !detail.Contains(h.town) && ids.Contains(h.town) && NotNear(h.town))
                .OrderByDescending(h => h.tick).Take(max).ToList();
        }

        /// <summary>A town's people the colony meets at its own reach: not one of the far towns' (a town a notable lives in, kept beyond the reach, is near enough).</summary>
        private static bool NotNear(int town) => WorldLedger.TownRecord(town) == null;
    }
}
