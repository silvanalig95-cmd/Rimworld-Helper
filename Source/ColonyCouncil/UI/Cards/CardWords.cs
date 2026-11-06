using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words the cards and the linked rows share: a tie's and a letter's name, a letter's steps, how far a place is,
    /// what a town is, and the history's lines with the names they mention as links. Linked text is for Rimfolk's own
    /// windows only: whatever goes to the game is written without links, as it always was.
    /// </summary>
    public static class CardWords
    {
        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>"a favour owed", "a pact", "a grudge": a tie by its kind.</summary>
        public static string TieName(Tie t)
        {
            if (t == null) return "";
            string key = "CC_Card_TieKind_" + t.kind;
            return key.CanTranslate() ? T(key) : WorldHistoryMath.Words(t.kind.ToString()).CapitalizeFirst();
        }

        /// <summary>Where a tie stands in words: open, called in, kept, broken, ended, and since when.</summary>
        public static string TieState(Tie t)
        {
            if (t == null) return "";
            return T("CC_Card_TieState_" + t.state, FactionMemory.AgoText(t.state == Tie.State.Open || t.state == Tie.State.CalledIn || t.closed < 0 ? t.since : t.closed).Named("AGO"));
        }

        /// <summary>"A letter of greetings to Edda Marsh" (a call when it went by comms).</summary>
        public static string LetterName(NeighbourLetter l)
        {
            if (l == null) return "";
            string purpose = ("CC_Letter_Purpose_" + l.purpose).Translate();
            var n = NotableBook.Instance?.Get(l.notable);
            string to = l.toName ?? n?.Name ?? "?";
            return T(l.channel == LetterChannel.Console ? "CC_Card_CallTo" : "CC_Card_LetterTo", purpose.Named("PURPOSE"), to.Named("NAME"));
        }

        /// <summary>The steps a letter goes through, in words: written, on the road, arrived, answered (a call: called, through, answered).</summary>
        public static string[] LetterSteps(NeighbourLetter l)
        {
            bool call = l != null && l.channel == LetterChannel.Console;
            return call
                ? new[] { T("CC_Card_Step_Calling"), T("CC_Card_Step_Through"), T("CC_Card_Step_Answered") }
                : new[] { T("CC_Card_Step_Written"), T("CC_Card_Step_Road"), T("CC_Card_Step_Arrived"), T("CC_Card_Step_Answered") };
        }

        /// <summary>How far along a letter is among its steps; -1 for one called off.</summary>
        public static int LetterStep(NeighbourLetter l)
        {
            if (l == null) return -1;
            bool call = l.channel == LetterChannel.Console;
            switch (l.stage)
            {
                case LetterStage.ToWrite:
                case LetterStage.Writing:
                case LetterStage.ToCall:
                case LetterStage.Calling:
                    return 0;
                case LetterStage.AwaitMessenger:
                case LetterStage.Travelling:
                    return 1;
                case LetterStage.CallbackWait:
                case LetterStage.Ringing:
                    return call ? 1 : 2;
                case LetterStage.Answering:
                case LetterStage.Visiting:
                case LetterStage.Guesting:
                    return call ? 2 : 2;
                case LetterStage.Done:
                    return call ? 2 : 3;
                default:
                    return -1;
            }
        }

        // ---------------- Places ----------------

        /// <summary>What a town is, with its people as a link: "a town of the Ashfall Clans", "a trading post of ...".</summary>
        public static string TownKind(Settlement s, bool links = true)
        {
            if (s == null) return "";
            var f = s.Faction;
            string people = f == null ? T("CC_Card_NoPeople") : links ? ThingRefs.Link(f.Name, ThingRefs.Of(f)) : f.Name;
            var rec = WorldLedger.TownRecord(s.ID);
            string kind = rec != null ? rec.kind.ToString() : "Town";
            string key = "CC_Card_Kind_" + kind;
            if (!key.CanTranslate()) key = "CC_Card_Kind_Town";
            return T(key, people.Named("PEOPLE"));
        }

        private static readonly Dictionary<long, int> ticksTo = new Dictionary<long, int>();

        /// <summary>
        /// How far a place is from the home the region map is drawn around, by the game's own caravan estimate (worked out
        /// once per place and home): "3.2 days by caravan (14 tiles)". Null when there is no home.
        /// </summary>
        public static string Distance(PlanetTile tile) => Distance(tile, false);

        /// <summary>
        /// How far a place is, short, for a row of a list (the region map's lenses): "3.2 days by caravan", or its tiles
        /// when there is no way by caravan. The same estimate as <see cref="Distance(PlanetTile)"/>, kept with it.
        /// </summary>
        public static string DistanceShort(PlanetTile tile) => Distance(tile, true);

        private static string Distance(PlanetTile tile, bool brief)
        {
            if (!tile.Valid || !RegionMap.HomeTile(out _, out var home)) return null;
            try
            {
                if (tile == home) return T("CC_Region_HomeTile");
                int tiles = Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(home, tile));
                long key = (long)home.tileId << 32 | (uint)tile.tileId;
                if (!ticksTo.TryGetValue(key, out int ticks))
                {
                    try
                    {
                        ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(home, tile, null);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("cards: the caravan estimate", e);
                        ticks = -1;
                    }
                    if (ticksTo.Count > 400) ticksTo.Clear();
                    ticksTo[key] = ticks;
                }
                if (brief) return ticks > 0 ? "CC_Lens_ByCaravan".Translate(TextUtil.DaysString(ticks).Named("DAYS")).Resolve() : "CC_Lens_Tiles".Translate(tiles.Named("TILES")).Resolve();
                if (ticks > 0) return "CC_Region_Days".Translate(TextUtil.DaysString(ticks), tiles).Resolve();
                if (ticks == 0) return "CC_Region_NoWay".Translate(tiles).Resolve();
                return "CC_Region_TilesAway".Translate(tiles).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cards: how far a place is", e);
                return null;
            }
        }

        // ---------------- The history ----------------

        /// <summary>
        /// An entry's words for Rimfolk's windows: the history's own (<see cref="History.Text"/>), else its kind in words and
        /// who took part, with a count for a summary; and with <paramref name="links"/>, each one it names (who took part,
        /// where) a link to them. The plain words are the same as the world history page wrote before links.
        /// </summary>
        public static string HistoryLine(HistoryEntry e, bool links)
        {
            if (e == null) return "";
            string text = History.Text(e);
            var names = new List<KeyValuePair<string, string>>();
            foreach (var a in e.actors)
            {
                var r = ThingRefs.Of(a.who);
                if (r.IsNone || !ThingRefs.Exists(r)) continue;
                string n = ThingRefs.Name(r);
                if (!n.NullOrEmpty() && n != "?") names.Add(new KeyValuePair<string, string>(n, r.ToString()));
            }
            if (!e.place.IsNone)
            {
                var p = ThingRefs.Of(e.place);
                if (p.kind != ThingKind.Tile && ThingRefs.Exists(p))
                {
                    string n = ThingRefs.Name(p);
                    if (!n.NullOrEmpty() && n != "?") names.Add(new KeyValuePair<string, string>(n, p.ToString()));
                }
            }
            if (text.NullOrEmpty())
            {
                var who = new List<string>();
                foreach (var n in names)
                    if (!who.Contains(n.Key))
                        who.Add(n.Key);
                text = WorldHistoryMath.Words(e.kind).CapitalizeFirst() + (who.Count > 0 ? ": " + TextUtil.JoinAnd(who) : "");
            }
            // An answer says what came of it, once that's known (the answers' log).
            else if (!e.folded && HistoryKinds.Matches(e.kind, AnswerMath.Family))
            {
                string came = Chronicle.CameOf(e.values);
                if (!came.NullOrEmpty()) text += " " + came;
            }
            if (e.count > 1)
            {
                var (from, to) = WorldHistoryMath.Days(e);
                text += " " + (from < to
                    ? "CC_WHist_TimesFrom".Translate(e.count, "CC_DayN".Translate(from).ToString(), "CC_DayN".Translate(to).ToString())
                    : "CC_WHist_Times".Translate(e.count)).Resolve();
            }
            return links ? LinkTextMath.MarkNames(text, names) : text;
        }

        /// <summary>The day of an entry ("Day 34"), as a link to the entry's own card.</summary>
        public static string HistoryDate(HistoryEntry e, bool links)
        {
            if (e == null) return "";
            string day = "CC_DayN".Translate(WorldHistoryMath.Days(e).to).Resolve();
            return links ? ThingRefs.Link(day, ThingRefs.Of(e)) : day;
        }

        /// <summary>
        /// A card's story: its newest three lines of the history (each with its day, a link to the entry), and a link to
        /// its whole history on the History page. <paramref name="who"/>: what someone took part in (a person, a people, a
        /// house); <paramref name="town"/>: what happened at a town and what the town itself did.
        /// </summary>
        public static void Story(CardModel m, Ref who, Ref town, float order = 50f)
        {
            var found = new List<HistoryEntry>();
            int total = 0;
            void Take(List<HistoryEntry> list)
            {
                foreach (var e in list)
                    if (e != null && !found.Exists(x => x.id == e.id))
                        found.Add(e);
            }
            if (!who.IsNone)
            {
                Take(History.Of(who, max: 3));
                total += History.Count(who: who);
            }
            if (!town.IsNone)
            {
                Take(History.At(town, max: 3));
                Take(History.Of(town, max: 3));
                total += History.Count(place: town) + History.Count(who: town);
            }
            found.Sort((a, b) => b.tick != a.tick ? b.tick.CompareTo(a.tick) : b.id.CompareTo(a.id));
            if (found.Count > 3) found.RemoveRange(3, found.Count - 3);
            var b0 = m.Block(CardSlot.Story, order, "story", T("CC_Card_Head_Story"));
            if (found.Count == 0)
            {
                b0.Line(T("CC_Card_NoHistory"), UiKit.Dim);
                return;
            }
            foreach (var e in found)
                b0.lines.Add(new CardLine(HistoryDate(e, true) + "  " + HistoryLine(e, true), UiKit.Faint, GameFont.Small));
            b0.more = T("CC_Card_WholeHistory", Math.Max(total, found.Count).Named("COUNT"));
            b0.moreRun = () => WorldHistoryPage.ShowFor(who, town);
        }
    }
}
