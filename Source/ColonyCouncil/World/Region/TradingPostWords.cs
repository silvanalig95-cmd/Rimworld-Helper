using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of the trading posts (Languages/English/Keyed/Rimfolk_TradingPosts.xml): a post's name, its kind and its
    /// keeper's title by its people's age (Ages.Form, the feature Post: a trading camp, a market, a depot, a ground
    /// station; a trader, a factor, a manager, a quartermaster), what is left when it closes, the colony's means of
    /// reaching it by the colony's own age; the letters of its opening, its keeper's warning, its closing and its sack,
    /// the news of things mended and of a post razed; the history's lines; the region map's panel for a post. Every line
    /// goes to the player through Tell, and says only what the map shows.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TradingPostWords
    {
        private static readonly Color Accent = new Color(0.86f, 0.78f, 0.52f);

        static TradingPostWords()
        {
            NeighbourPanel.Sections.Add(DrawPost);
        }

        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        private static string PeopleName(Faction f) => f?.Name ?? "CC_RiseFall_Someone".Translate().Resolve();

        /// <summary>The post's kind in a sentence, by its people's age: "trading camp", "market", "depot", "ground station".</summary>
        public static string KindWord(Faction people) => AgesMath.RiteKey(AgeFeature.Post, Ages.Of(people)).Translate().Resolve();

        /// <summary>Its keeper's title by its people's age: "trader", "factor", "manager", "quartermaster".</summary>
        public static string KeeperWord(Faction people) => TradingPostMath.KeeperKey(Ages.Of(people)).Translate().Resolve();

        /// <summary>The colony's own way to it, by the colony's age: "a trading party on foot", "a pack train", "a caravan", "a supply run".</summary>
        private static string Means(Map home, Faction people) => Ages.Form(AgeFeature.Post, home, people).word;

        /// <summary>What is left where a post stood, by its people's age.</summary>
        public static string LeftWord(Faction people) => ("CC_Post_Left_" + Ages.Of(people)).Translate().Resolve();

        /// <summary>The post's name: their naming rule's name with its kind by their age ("Ashford Market").</summary>
        public static string PostName(string root, Faction people) =>
            root.NullOrEmpty() ? root : TradingPostMath.NameKey(Ages.Of(people)).Translate(root.Named("TOWN")).Resolve();

        /// <summary>
        /// The keeper by name, with the title the game shows for one of the Empire's ("Knight Sora Venn"): the Empire keeps
        /// its own titles.
        /// </summary>
        public static string KeeperName(Notable n)
        {
            if (n == null) return null;
            var p = n.pawn;
            if (p != null && Ages.IsEmpire(n.faction))
            {
                var t = WorldMechanics.Title(p);
                if (t != null && !t.label.NullOrEmpty()) return t.label.CapitalizeFirst() + " " + n.Name;
            }
            return n.Name;
        }

        private static string Where(Map home, TradingPost p)
        {
            var t = RiseFall.TileOf(p.tile);
            if (home == null || !t.Valid || t.Layer != home.Tile.Layer) return "CC_Region_Where_Near".Translate().Resolve();
            return RegionWords.Where(home, t, Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t));
        }

        private static string Colony(Map home) => Homes.Name(home);

        /// <summary>The colony has transport pods: they can trade at a town too, so the opening letter says so.</summary>
        private static bool HasPods
        {
            get
            {
                var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("TransportPod");
                return r != null && r.IsFinished;
            }
        }

        // ---------------- The letters and the news ----------------

        /// <summary>
        /// The opening letter, looking at the post: who opened it, what and where it is, its keeper's greeting, how the
        /// colony gets there by its own means, and (with Systems talk to each other on) what it means for their caravans
        /// and the road.
        /// </summary>
        public static void TellOpened(Map home, TradingPost p, Settlement town, Pawn keeper, Settlement from)
        {
            if (home == null || p == null || town == null) return;
            try
            {
                var people = p.people;
                var args = new List<NamedArgument>
                {
                    PeopleName(people).Named("PEOPLE"), town.Name.Named("POST"), KindWord(people).Named("KIND"), Where(home, p).Named("WHERE"),
                    Colony(home).Named("COLONY"), (from?.Name ?? "").Named("FROM"), KeeperWord(people).Named("TITLE"), Means(home, people).CapitalizeFirst().Named("MEANS")
                };
                var n = KeeperOf(keeper);
                args.Add((KeeperName(n) ?? "").Named("KEEPER"));
                var parts = new List<string>
                {
                    T(from != null ? "CC_Post_Opened_Text" : "CC_Post_Opened_Text_NoFrom", args.ToArray()),
                    T(n != null ? "CC_Post_Opened_Keeper" : "CC_Post_Opened_Keeper_NoName", args.ToArray()),
                    T(HasPods ? "CC_Post_Opened_Means_Pods" : "CC_Post_Opened_Means", args.ToArray())
                };
                if (TradingPosts.LinksOn)
                {
                    string road = T("CC_Post_Opened_Road", args.ToArray());
                    parts.Add(TradingPosts.CaravansOn ? T("CC_Post_Opened_Caravans", args.ToArray()) + " " + road : road);
                }
                Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Post_Opened_Label", args.ToArray()), string.Join("\n\n", parts), LetterDefOf.PositiveEvent,
                    new LookTargets(town), people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: the opening letter", e);
            }
        }

        private static Notable KeeperOf(Pawn keeper)
        {
            var book = NotableBook.Instance;
            if (keeper == null || book == null) return null;
            foreach (var n in book.notables)
                if (n != null && n.pawn == keeper)
                    return n;
            return null;
        }

        private static NamedArgument[] Args(Map home, TradingPost p, Notable keeper, Faction enemy = null) => new[]
        {
            PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"), KindWord(p.people).Named("KIND"), Where(home, p).Named("WHERE"), Colony(home).Named("COLONY"),
            KeeperWord(p.people).Named("TITLE"), (KeeperName(keeper) ?? "").Named("KEEPER"), LeftWord(p.people).Named("LEFT"), PeopleName(enemy).Named("ENEMY")
        };

        /// <summary>The keeper's warning: the post will close unless things mend between their people and the colony. No day is named.</summary>
        public static void TellWarned(Map home, TradingPost p, Settlement town, Notable keeper)
        {
            if (home == null || p == null) return;
            try
            {
                var args = Args(home, p, keeper);
                string text = T(keeper != null ? "CC_Post_Warned_Text" : "CC_Post_Warned_Text_NoName", args) + "\n\n" + T("CC_Post_Warned_How", args);
                Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Post_Warned_Label", args), text, LetterDefOf.NegativeEvent,
                    town != null ? new LookTargets(town) : LookTargets.Invalid, p.people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: the keeper's warning", e);
            }
        }

        /// <summary>Things mended after the warning: the keeper writes that the post stays open.</summary>
        public static void TellMended(Map home, TradingPost p, Settlement town, Notable keeper)
        {
            if (home == null || p == null) return;
            var args = Args(home, p, keeper);
            Tell.Minor(NewsKind.World, Importance.Notable, home, T(keeper != null ? "CC_Post_Mended" : "CC_Post_Mended_NoName", args),
                town != null ? new LookTargets(town) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>A sack it survived: raiders of a people at war with theirs struck it; it stands, poorer.</summary>
        public static void TellSacked(Map home, TradingPost p, Settlement town, Faction enemy)
        {
            if (home == null || p == null) return;
            var args = Args(home, p, null, enemy);
            Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Post_Sacked_Label", args), T("CC_Post_Sacked_Text", args), LetterDefOf.NegativeEvent,
                town != null ? new LookTargets(town) : LookTargets.Invalid, p.people);
        }

        /// <summary>
        /// How it ended: a closing's letter looking at what is left (soured, at war, sacked and given up), a line in the
        /// news for a post the colony razed (the game's own letter tells the fight); nothing for a post simply gone.
        /// </summary>
        public static void TellEnded(Map home, TradingPost p, PostEnd why, WorldObject ruin, Faction enemy)
        {
            if (home == null || p == null) return;
            try
            {
                var args = Args(home, p, null, enemy);
                var look = ruin != null && !ruin.Destroyed ? new LookTargets(ruin) : LookTargets.Invalid;
                switch (why)
                {
                    case PostEnd.Soured:
                    case PostEnd.War:
                    case PostEnd.Sacked:
                        Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Post_Closed_Label", args), T("CC_Post_Closed_" + why, args), LetterDefOf.NegativeEvent,
                            look, p.people);
                        return;
                    case PostEnd.Razed:
                        Tell.Minor(NewsKind.World, Importance.Notable, home, T("CC_Post_Razed", args), look, MessageTypeDefOf.NegativeEvent);
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: its end told", e);
            }
        }

        /// <summary>What its keeper remembers of the colony from the start (a note of the notables'): it counts as a town the colony dealt with.</summary>
        public static string NoteOpened(Settlement town) => T("CC_Post_Note_Opened", (town?.Name ?? "").Named("POST"));

        // ---------------- The history's lines ----------------

        public static string HistoryOpened(TradingPost p, Map home) =>
            T("CC_Post_H_Opened", PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"), KindWord(p.people).Named("KIND"), Colony(home).Named("COLONY"));

        public static string HistoryWarned(TradingPost p) => T("CC_Post_H_Warned", PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"));

        public static string HistoryMended(TradingPost p) => T("CC_Post_H_Mended", PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"));

        public static string HistorySacked(TradingPost p, Faction enemy) =>
            T("CC_Post_H_Struck", PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"), PeopleName(enemy).Named("ENEMY"));

        public static string HistoryEnded(TradingPost p, PostEnd why, Faction enemy) =>
            T("CC_Post_H_" + why, PeopleName(p.people).Named("PEOPLE"), (p.name ?? "").Named("POST"), PeopleName(enemy).Named("ENEMY"));

        // ---------------- The region map's panel and the page ----------------

        /// <summary>A post's lines: what and whose it is and since when, its keeper, what it does for their caravans, a warning given or a closing waiting.</summary>
        public static List<string> Lines(Map home, TradingPost p, bool withHome)
        {
            var lines = new List<string>();
            if (p == null) return lines;
            var keeper = TradingPosts.KeeperOf(p);
            var args = new List<NamedArgument>(Args(home, p, keeper))
            {
                FactionMemory.AgoText(p.opened).Named("AGO"), (home != null ? Colony(home) : "").Named("HOME"),
                (p.sackedAt >= 0 ? FactionMemory.AgoText(p.sackedAt) : "").Named("SACKED")
            };
            var a = args.ToArray();
            lines.Add(T(withHome && Homes.All.Count > 1 ? "CC_Post_Panel_WhatHome" : "CC_Post_Panel_What", a));
            if (keeper != null) lines.Add(T("CC_Post_Panel_Keeper", a));
            if (p.closing >= 0) lines.Add(T("CC_Post_Panel_Closing", a));
            else if (p.warnedAt >= 0) lines.Add(T("CC_Post_Panel_Warned", a));
            if (TradingPosts.CaravansOn && p.closing < 0) lines.Add(T("CC_Post_Panel_Caravans", a));
            if (p.sackedAt >= 0) lines.Add(T("CC_Post_Panel_Sacked", a));
            return lines;
        }

        /// <summary>The region map's panel for a town that is a post: its own section under the town's.</summary>
        private static float DrawPost(Rect rect, Settlement s)
        {
            if (s == null) return 0f;
            try
            {
                var p = TradingPosts.PostAt(s.ID, out var home);
                if (p == null) return 0f;
                var lines = Lines(home, p, true);
                if (lines.Count == 0) return 0f;
                float y = rect.y;
                UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), "CC_Post_Panel_Header".Translate(), Accent);
                y += 24f;
                foreach (var l in lines) y += UiKit.Paragraph(rect.x, y, rect.width, l, Color.white, GameFont.Small) + 2f;
                return y + 2f - rect.y;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a post's panel", e);
                return 0f;
            }
        }
    }
}
