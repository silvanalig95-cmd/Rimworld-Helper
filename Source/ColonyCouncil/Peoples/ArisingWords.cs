using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of a people that arose (Languages/English/Keyed/Rimfolk_Peoples_Arising.xml): its letter, which opens
    /// with the colony's means of hearing it (Ages.Form, the founding's word: a traveler, a rider, the radio, a comms
    /// relay), says what they came from, how they live (their calling, which is what their town and their people show
    /// on the map), where their town stands with the founding's rite by their age, and how they feel toward the colony
    /// (their goodwill); the history's line (world:arose); and the line on their card and their towns' cards.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ArisingWords
    {
        /// <summary>The history's kind for a people that arose (the world family).</summary>
        public const string Kind = "world:arose";

        static ArisingWords()
        {
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 12f, r => Lines(ThingRefs.Faction(r)), null, "arisen-people");
            CardSections.TownLines(CardSlot.Now, 12f, s => Lines(s?.Faction), null, "arisen-town");
        }

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        private static string PeopleName(Faction f, string kept = null) => f?.Name ?? kept ?? "CC_Arise_Someone".Translate().Resolve();

        /// <summary>The league's towns as the words name them: "Ashford, Brightwater and Corran".</summary>
        public static string Towns(List<Settlement> towns) => TextUtil.JoinAnd(towns.Where(t => t != null).Select(t => t.HasName ? t.Name : t.Label).ToList());

        private static string Ago(int tick) => tick >= 0 ? FactionMemory.AgoText(tick) : "CC_Memory_Ago_Lately".Translate().Resolve();

        // ---------------- The letter ----------------

        /// <summary>
        /// The letter of a people that arose, to the home nearest it, and the history's line. Their town is on the map
        /// (the letter's look-target), with the fallen town's ruin, the league's towns, or the faith's town beside it.
        /// </summary>
        public static void Tell(ArisingCause c, Faction f, Settlement town, Arising.Detail detail)
        {
            // apart: news of people out in the world, heard from afar by the colony's means; nobody of the colony does anything with them here.
            var home = c.home ?? RegionTowns.NearestHome(town.Tile, out _);
            float tiles = home != null && home.Tile.Valid && home.Tile.Layer == town.Tile.Layer ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile) : -1f;
            var form = Ages.Form(AgeFeature.Founding, home ?? Homes.Seat, f);
            string calling = Peoples.CallingOf(f) ?? "";
            int feeling = ArisingMath.Feeling(f.HasGoodwill ? f.PlayerGoodwill : 0);
            string faith = c.kind == ArisingMath.Splinter ? c.name : null;
            string newFaith = ModsConfig.IdeologyActive ? f.ideos?.PrimaryIdeo?.name ?? detail?.faith : detail?.faith;
            var args = new List<NamedArgument>
            {
                form.word.CapitalizeFirst().Named("MEANS"), f.Name.Named("NAME"), (c.name ?? "").Named("TOWN"), PeopleName(c.parent).Named("PEOPLE"),
                Ago(c.since).Named("AGO"), Towns(c.towns).Named("TOWNS"), town.Name.Named("TOWNNEW"),
                (Peoples.TownWord(f) ?? "CC_Arise_TownWord".Translate().Resolve()).Named("TOWNWORD"), RegionWords.Where(home, town.Tile, tiles).Named("WHERE"),
                form.rite.Named("RITE"), (faith ?? "").Named("FAITH"), (newFaith ?? "").Named("NEWFAITH"), (c.friend?.LabelShort ?? "").Named("FRIEND"),
                (c.record?.word ?? BanishTracker.AgeWord()).Named("WORD")
            };
            if (c.exile != null) args.Add(c.exile.Named("PAWN"));
            var a = args.ToArray();
            var parts = new List<string>();
            string open = c.kind == ArisingMath.League ? LeagueOpening(f) : c.kind == ArisingMath.Splinter && c.war == null ? "CC_Arise_splinter_StandIn" : "CC_Arise_" + c.kind;
            parts.Add(T(open, a));
            if (c.kind == ArisingMath.Refugees && c.parent != null && !Find.WorldObjects.Settlements.Any(s => s != null && !s.Destroyed && s.Faction == c.parent))
                parts.Add(T("CC_Arise_refugees_Last", a));
            if (c.kind == ArisingMath.Splinter && detail != null && (detail.dropped != null || detail.added != null))
            {
                var meme = detail.dropped ?? detail.added;
                parts.Add(T(detail.dropped != null ? "CC_Arise_splinter_dropped" : "CC_Arise_splinter_added", a.Concat(new[] { meme.label.Named("MEME") }).ToArray()));
            }
            string how = "CC_Arise_" + c.kind + "_" + calling;
            if (c.kind != ArisingMath.League && how.CanTranslate()) parts.Add(T(how, a));
            string text = string.Join(" ", parts);
            var after = new List<string> { T("CC_Arise_Town", a) };
            if (c.kind == ArisingMath.Splinter && c.parent != null) after.Add(T("CC_Arise_splinter_Parent", a));
            after.Add(T(FeelKey(c, feeling), a));
            text += "\n\n" + string.Join(" ", after);
            string title = T("CC_Arise_" + c.kind + "_Title", a);
            var look = new List<GlobalTargetInfo> { new GlobalTargetInfo(town) };
            if (c.ruin != null && !c.ruin.Destroyed) look.Add(new GlobalTargetInfo(c.ruin));
            foreach (var t in c.towns)
                if (t != null && !t.Destroyed)
                    look.Add(new GlobalTargetInfo(t));
            global::ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Important, home, title, text, feeling < 0 ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent,
                new LookTargets(look), f, "arise|" + f.loadID);
            History(c, f, town);
        }

        /// <summary>The league's opening is by its calling (merchants or herders); the feeling's line by its goodwill and what happened.</summary>
        private static string FeelKey(ArisingCause c, int feeling)
        {
            string k = "CC_Arise_" + c.kind;
            switch (c.kind)
            {
                case ArisingMath.Refugees:
                    return feeling < 0 ? c.byColony ? k + "_CoolOurs" : k + "_Cool" : feeling > 0 ? k + "_Warm" : k + "_Plain";
                case ArisingMath.Exiles:
                    if (feeling < 0) return c.record != null && c.record.grudge ? k + "_Cool" : k + "_CoolGlad";
                    return feeling > 0 ? c.friend != null ? k + "_WarmFriend" : k + "_Warm" : k + "_Plain";
                case ArisingMath.Splinter:
                    return feeling > 0 && c.debate ? k + "_Warm" : feeling < 0 ? k + "_Cool" : k + "_Plain";
                default:
                    return feeling < 0 ? k + "_Cool" : feeling > 0 ? k + "_Warm" : k + "_Plain";
            }
        }

        /// <summary>The league's letter opens by its calling: the merchants' road league, the herders' folk.</summary>
        public static string LeagueOpening(Faction f) => "CC_Arise_league_" + (Peoples.CallingOf(f) == PeoplesMath.Herders ? PeoplesMath.Herders : PeoplesMath.Traders);

        private static void History(ArisingCause c, Faction f, Settlement town)
        {
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Refs.Of(town.Tile), HistoryRole.About) };
            if (c.parent != null) actors.Add(new HistoryActor(Refs.Of(c.parent), HistoryRole.About));
            foreach (var t in c.towns)
                if (t?.Faction != null && t.Faction != c.parent)
                    actors.Add(new HistoryActor(Refs.Of(t.Faction), HistoryRole.With));
            if (c.exile != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(c.exile), HistoryRole.Did));
            var rec = ArisingBook.Instance?.Of(f);
            string line = T("CC_Arise_H_" + c.kind, (rec?.from ?? c.name ?? "").Named("FROM"), f.Name.Named("NAME"), PeopleName(c.parent).Named("PEOPLE"));
            global::ColonyCouncil.History.Add(Kind, actors, Refs.Of(town), HistoryMath.Milestone, line, values: new List<string> { f.Name, rec?.from ?? "" });
        }

        // ---------------- Their cards ----------------

        /// <summary>A people that arose, on its card and its towns' cards: "Arose 40 days ago: the survivors of Ashford, a town of the Lenlia that fell."</summary>
        public static List<string> Lines(Faction f)
        {
            if (f == null) return null;
            try
            {
                var r = ArisingBook.Instance?.Of(f);
                if (r == null || !ArisingMath.IsKind(r.kind)) return null;
                return new List<string>
                {
                    T("CC_Arise_Card_" + r.kind, Ago(r.tick).Named("AGO"), (r.from ?? "?").Named("FROM"), PeopleName(r.parent, r.parentName).Named("PEOPLE"))
                };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("new peoples: their card", e);
                return null;
            }
        }
    }
}
