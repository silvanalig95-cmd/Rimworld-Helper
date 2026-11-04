using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of towns that rise and fall (Languages/English/Keyed/Rimfolk_RiseFall.xml): the colony news of a town
    /// given up, a ruin left by a fight, a ruin gone, a ruin settled again and a town founded by settlers from a
    /// flourishing one, each opening with the colony's means of hearing it (Ages.Form, the founding's word: a traveler, a
    /// rider, the radio, a comms relay) and naming a ruin by its people's age (cold hearths, an empty town, a ghost town,
    /// a derelict colony); the history's lines; the region map's panel for a ruin and for a town being given up; and what
    /// the game's own inspect pane says of a ruin Rimfolk keeps. Every line goes to the player through Tell.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RiseFallWords
    {
        /// <summary>The history's kinds (the world family): a town given up, a ruin left by a fight, a ruin gone to grass, a ruin settled again.</summary>
        public const string GivenUpKind = "world:abandoned";
        public const string FellKind = "world:ruined";
        public const string FadedKind = "world:ruingone";
        public const string ResettledKind = "world:resettled";

        public static readonly string[] Kinds = { GivenUpKind, FellKind, FadedKind, ResettledKind };

        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);

        static RiseFallWords()
        {
            NeighbourPanel.PlaceSections.Add(DrawRuin);
            NeighbourPanel.Sections.Add(DrawClosing);
        }

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        /// <summary>The colony's means of hearing news of a town, capitalized ("Word from a traveler").</summary>
        private static string Means(Map home, Faction people) => Ages.Form(AgeFeature.Founding, home, people).word.CapitalizeFirst();

        /// <summary>What is left of a town, by its people's age: "cold hearths and empty longhouses", "an empty town", "a ghost town", "a derelict colony".</summary>
        public static string RuinWord(Faction people) => ("CC_RiseFall_Ruin_" + Ages.Of(people)).Translate().Resolve();

        private static string Where(Map home, PlanetTile tile, float tiles) => RegionWords.Where(home, tile, tiles);

        private static string PeopleName(Faction f) => f?.Name ?? "CC_RiseFall_Someone".Translate().Resolve();

        // ---------------- The colony news ----------------

        /// <summary>"Word from a traveler: the Lenlia are giving up Ashford, two days' walk north. Its families are on the road to Brightwater."</summary>
        public static void TellLeaving(Map home, Settlement town, float tiles, Settlement next)
        {
            string key = next != null ? "CC_RiseFall_Leaving" : "CC_RiseFall_Leaving_NoNext";
            string text = T(key, Means(home, town.Faction).Named("MEANS"), PeopleName(town.Faction).Named("PEOPLE"), town.Name.Named("TOWN"),
                Where(home, town.Tile, tiles).Named("WHERE"), (next?.Name ?? "").Named("NEXT"));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(town), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>"Word from a traveler: a few families would not leave Ashford ... The town hangs on, smaller than it was."</summary>
        public static void TellHeldOn(Map home, Settlement town, float tiles)
        {
            string text = T("CC_RiseFall_HeldOn", Means(home, town.Faction).Named("MEANS"), PeopleName(town.Faction).Named("PEOPLE"), town.Name.Named("TOWN"),
                Where(home, town.Tile, tiles).Named("WHERE"));
            Tell.Minor(NewsKind.World, Importance.Flavor, home, text, new LookTargets(town), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>"Word from a traveler: the last families have left Ashford, two days' walk north. Nothing is left there but an empty town."</summary>
        public static void TellEmpty(Map home, WorldObject ruin, Faction people, string name, float tiles)
        {
            string text = T("CC_RiseFall_Empty", Means(home, people).Named("MEANS"), PeopleName(people).Named("PEOPLE"), (name ?? "").Named("TOWN"),
                Where(home, ruin.Tile, tiles).Named("WHERE"), RuinWord(people).Named("RUIN"));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(ruin), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A fight's ruin stands: "Where Ashford stood, two days' walk north, there is only a ghost town now." (by the colony's own people, without the means).</summary>
        public static void TellFell(Map home, WorldObject ruin, LedgerRuin r, float tiles)
        {
            string key = r.byColony ? "CC_RiseFall_Fell_Ours" : "CC_RiseFall_Fell";
            string text = T(key, Means(home, r.people).Named("MEANS"), PeopleName(r.people).Named("PEOPLE"), (r.name ?? "").Named("TOWN"),
                Where(home, ruin.Tile, tiles).Named("WHERE"), RuinWord(r.people).Named("RUIN"));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(ruin), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A ruin's 300 days are over: "Word from a traveler: hardly a wall of Ashford is left standing; the grass has the rest."</summary>
        public static void TellFaded(Map home, PlanetTile tile, LedgerRuin r, float tiles)
        {
            string text = T("CC_RiseFall_Faded", Means(home, r.people).Named("MEANS"), (r.name ?? "").Named("TOWN"), Where(home, tile, tiles).Named("WHERE"));
            Tell.Minor(NewsKind.World, Importance.Flavor, home, text, new LookTargets(tile), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>"Word from a traveler: settlers from Ashford have founded Brightwater of the Lenlia, two days' walk north."</summary>
        public static void TellDaughter(Map home, Settlement town, Settlement mother, float tiles)
        {
            string text = T("CC_RiseFall_Daughter", Means(home, town.Faction).Named("MEANS"), PeopleName(town.Faction).Named("PEOPLE"), town.Name.Named("TOWN"),
                (mother?.Name ?? "").Named("MOTHER"), Where(home, town.Tile, tiles).Named("WHERE"), Ages.Form(AgeFeature.Founding, home, town.Faction).rite.Named("RITE"));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(town), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// "Word from a traveler: families of the Lenlia have come back to the old walls of Ashford, two days' walk north."
        /// Newcomers (another people) raise a town there, under the old name or their own: a clan, families, a company or
        /// a charter by their age.
        /// </summary>
        public static void TellResettled(Map home, Settlement town, LedgerRuin r, bool own, float tiles)
        {
            var age = Ages.Of(town.Faction);
            // Under a new name (newcomers' own, or the old one taken by another town meanwhile), the line says so.
            string key = town.Name != r.name ? "CC_RiseFall_Renamed_" + age : own ? "CC_RiseFall_Back_" + age : "CC_RiseFall_Newcomers_" + age;
            string text = T(key, Means(home, town.Faction).Named("MEANS"), PeopleName(town.Faction).Named("PEOPLE"), (r.name ?? town.Name).Named("TOWN"),
                town.Name.Named("NEWNAME"), Where(home, town.Tile, tiles).Named("WHERE"));
            Tell.Minor(NewsKind.World, Importance.Notable, home, text, new LookTargets(town), MessageTypeDefOf.PositiveEvent);
        }

        // ---------------- The history's lines ----------------

        public static string HistoryGivenUp(Faction people, string town) => T("CC_RiseFall_H_GivenUp", PeopleName(people).Named("PEOPLE"), (town ?? "").Named("TOWN"));

        public static string HistoryFell(Faction people, string town) => T("CC_RiseFall_H_Fell", PeopleName(people).Named("PEOPLE"), (town ?? "").Named("TOWN"));

        public static string HistoryFaded(string town) => T("CC_RiseFall_H_Faded", (town ?? "").Named("TOWN"));

        public static string HistoryResettled(Faction people, string old, string town) =>
            T(old.NullOrEmpty() || old == town ? "CC_RiseFall_H_Back" : "CC_RiseFall_H_Renamed", PeopleName(people).Named("PEOPLE"), (old ?? town).Named("TOWN"), town.Named("NEWNAME"));

        // ---------------- The region map's panel and the inspect pane ----------------

        /// <summary>A ruin Rimfolk keeps: what town it was, whose, what is left of it, and how and when it came to this.</summary>
        public static List<string> RuinLines(LedgerRuin r)
        {
            var lines = new List<string>();
            if (r == null) return lines;
            lines.Add(T("CC_RiseFall_Panel_Was", (r.name ?? "?").Named("TOWN"), PeopleName(r.people).Named("PEOPLE"), RuinWord(r.people).Named("RUIN")));
            string ago = FactionMemory.AgoText(r.since);
            string how = r.why == LedgerRuin.Fight ? r.byColony ? "CC_RiseFall_Panel_Ours" : "CC_RiseFall_Panel_Fight" : "CC_RiseFall_Panel_GivenUp";
            lines.Add(T(how, ago.Named("AGO")));
            if (RegionBook.FortunesOn) lines.Add(T("CC_RiseFall_Panel_Again"));
            return lines;
        }

        private static float DrawRuin(Rect rect, PlanetTile tile, WorldObject wo)
        {
            if (!(wo is AbandonedSettlement)) return 0f;
            try
            {
                var r = RiseFall.RuinOf(wo);
                return r == null ? 0f : Draw(rect, RuinLines(r), "CC_RiseFall_Panel_Header");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a ruin's panel", e);
                return 0f;
            }
        }

        /// <summary>A town whose people are giving it up: one line under its own.</summary>
        private static float DrawClosing(Rect rect, Settlement s)
        {
            if (s == null || !RiseFall.On || !RiseFall.IsClosing(s.ID)) return 0f;
            try
            {
                var next = RiseFall.NextTown(s);
                string line = next != null ? T("CC_RiseFall_Panel_Leaving", next.Name.Named("NEXT")) : T("CC_RiseFall_Panel_Leaving_NoNext");
                return Draw(rect, new List<string> { line }, "CC_RiseFall_Panel_LeavingHeader");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a town being given up, its panel", e);
                return 0f;
            }
        }

        private static float Draw(Rect rect, List<string> lines, string header)
        {
            if (lines == null || lines.Count == 0) return 0f;
            float y = rect.y;
            UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), header.Translate(), Accent);
            y += 24f;
            foreach (var l in lines) y += UiKit.Paragraph(rect.x, y, rect.width, l, Color.white, GameFont.Small) + 2f;
            return y + 2f - rect.y;
        }

        /// <summary>The inspect pane of a ruin Rimfolk keeps: "Once Ashford of the Lenlia, given up 40 days ago."</summary>
        public static string Inspect(WorldObject o)
        {
            var r = RiseFall.RuinOf(o);
            if (r == null || r.name.NullOrEmpty()) return null;
            string how = r.why == LedgerRuin.Fight ? "CC_RiseFall_Inspect_Fight" : "CC_RiseFall_Inspect_GivenUp";
            return T(how, r.name.Named("TOWN"), PeopleName(r.people).Named("PEOPLE"), FactionMemory.AgoText(r.since).Named("AGO"));
        }
    }
}
