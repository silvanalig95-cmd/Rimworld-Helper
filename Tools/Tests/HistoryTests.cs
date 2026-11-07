using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The world's one history (World/Ledger/HistoryMath.cs): kinds and their families, actors as saved, the chronicle's
/// lines taken in as they are, the indexes and queries, merging a run within a day, folding old small events into their
/// season's summaries with the milestones kept, the bounds over five busy years, and the chronicle itself left exactly as
/// it was. Also the places world situations now carry, and the history's daily slot in Rimfolk's clock.
/// </summary>
public static class HistoryTests
{
    private const int Day = HistoryMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "history: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");

        // ---------------- Kinds ----------------

        // The chronicle's kinds, in ChronicleKind's order (ChronicleMath.cs, shared with the tests), and its milestones, read from Chronicle.cs.
        var kinds = Enum.GetNames(typeof(ChronicleKind)).Select(HistoryMath.LowerFirst).ToList();
        C(kinds.Count == 20 && kinds.SequenceEqual(HistoryKinds.Chronicle), "the chronicle's kinds in ChronicleKind's order: " + string.Join(",", kinds));
        string chron = File.ReadAllText(Path.Combine(src, "Chronicle.cs"));
        var mileExpr = Regex.Match(chron, @"public bool Milestone\s*=>(.*?);", RegexOptions.Singleline).Groups[1].Value;
        var miles = Regex.Matches(mileExpr, @"ChronicleKind\.(\w+)").Select(m => HistoryMath.LowerFirst(m.Groups[1].Value)).OrderBy(s => s).ToList();
        C(miles.Count == 6 && miles.SequenceEqual(HistoryKinds.Milestones.OrderBy(s => s)), "the milestones are the chronicle's own: " + string.Join(",", miles));
        foreach (var k in HistoryKinds.Chronicle)
            C(HistoryMath.IsWeighty(HistoryMath.OfChronicleKind(k)) == HistoryKinds.Milestones.Contains(k), $"{k} weighs as the chronicle's milestone rule says");
        C(HistoryKinds.OfChronicle(16) == "loss" && HistoryKinds.OfChronicle(19) == "world" && HistoryKinds.OfChronicle(-1) == "note" && HistoryKinds.OfChronicle(20) == "note",
            "a chronicle kind by its number, an unknown one a note");

        C(HistoryKinds.Matches("deed:traded", "deed") && HistoryKinds.Matches("deed:traded", "deed:traded") && !HistoryKinds.Matches("deed", "deed:traded"),
            "a family finds its kinds, a kind not its family");
        C(HistoryKinds.Matches("tie:pact:made", "tie:pact") && HistoryKinds.Matches("tie:pact:made", "tie") && !HistoryKinds.Matches("tie:pact:made", "tie:pa")
          && !HistoryKinds.Matches("tie:pactx", "tie:pact") && !HistoryKinds.Matches("deeds", "deed"), "families end at a colon");
        C(HistoryKinds.Levels("tie:pact:made").SequenceEqual(new[] { "tie:pact:made", "tie:pact", "tie" }) && HistoryKinds.Levels("loss").SequenceEqual(new[] { "loss" }),
            "a kind's levels, longest first");
        C(HistoryKinds.Family("world:began") == "world" && HistoryKinds.Family("loss") == "loss", "the family of a kind");
        C(HistoryKinds.OfDeed("traded") == "deed:traded" && HistoryKinds.OfTie(Tie.Kind.Pact, "made") == "tie:pact:made"
          && HistoryKinds.OfTie(Tie.Kind.Debt, "kept") == "tie:debt:kept", "deed and tie kinds");
        C(HistoryKinds.MatchesAny("loss", null) && HistoryKinds.MatchesAny("loss", new string[0]) && HistoryKinds.MatchesAny("deed:gift", new[] { "loss", "deed" })
          && !HistoryKinds.MatchesAny("bond", new[] { "loss", "deed" }), "any of several kinds, or every kind when none is asked");

        // ---------------- Weights ----------------

        C(HistoryMath.OfDeed(14) == HistoryMath.Everyday && HistoryMath.OfDeed(15) == HistoryMath.Milestone && HistoryMath.OfDeed(45) == HistoryMath.Milestone,
            "a big deed is a milestone");
        C(HistoryMath.IsWeighty(HistoryMath.OfTie(Tie.Kind.Pact)) && HistoryMath.IsWeighty(HistoryMath.OfTie(Tie.Kind.Marriage)) && !HistoryMath.IsWeighty(HistoryMath.OfTie(Tie.Kind.Regard))
          && !HistoryMath.IsWeighty(HistoryMath.OfTie(Tie.Kind.Favour)), "pacts and marriages are milestones, regard and favours everyday");

        // ---------------- Places and actors ----------------

        var tile = Ref.Tile(5);
        var orbitTile = Ref.Tile(5, 1);
        C(tile.kind == RefKind.Tile && tile.ToString() == "w5" && tile.TileId == 5 && tile.TileLayer == 0, "a surface tile is w and its id");
        C(orbitTile.ToString() == "w" + (Ref.TileLayerSpan + 5) && orbitTile.TileId == 5 && orbitTile.TileLayer == 1 && orbitTile != tile, "a tile on another layer keeps its layer");
        C(Ref.Parse(tile.ToString()) == tile && Ref.Parse(orbitTile.ToString()) == orbitTile && Ref.Parse("w0") == Ref.Tile(0), "tiles read back");
        C(Ref.Tile(-1).IsNone && Ref.Tile(Ref.TileLayerSpan).IsNone && Ref.Tile(0, -1).IsNone && Ref.Tile(0, 214).IsNone && !Ref.Tile(9999999, 213).IsNone,
            "tiles out of range name nothing");
        C(Ref.Parse("w-1").IsNone && Ref.Parse("w").IsNone && Ref.Tile(3) != Ref.Town(3) && Ref.Town(5).TileId == -1, "a tile is not a town");

        var a = Ref.Person(1);
        var b = Ref.Person(2);
        var col = Ref.Colony(100);
        var fac = Ref.People(7);
        var town = Ref.Town(55);
        var acts = new List<HistoryActor>
        {
            new HistoryActor(a, HistoryRole.About), new HistoryActor(fac, HistoryRole.Against), new HistoryActor(orbitTile, HistoryRole.For),
            new HistoryActor(Ref.Colony(-7), HistoryRole.With), new HistoryActor(Ref.House(2), HistoryRole.Did), new HistoryActor(Ref.LedgerPerson(4), HistoryRole.About)
        };
        string packed = HistoryMath.PackActors(acts);
        C(packed == "about:p1 against:f7 for:w" + (Ref.TileLayerSpan + 5) + " with:c-7 did:h2 about:p-4", "actors as saved: " + packed);
        C(HistoryMath.UnpackActors(packed).SequenceEqual(acts), "actors read back");
        C(HistoryMath.UnpackActors("xx:p1 about:zz about :p1 did:t3  for:p-0").SequenceEqual(new[] { new HistoryActor(Ref.Town(3), HistoryRole.Did) }),
            "what can't be read is left out");
        C(HistoryMath.UnpackActors(null).Count == 0 && HistoryMath.UnpackActors("").Count == 0 && HistoryMath.PackActors(null) == "", "no actors");
        C(HistoryMath.IdNumberOf("Human123") == 123 && HistoryMath.IdNumberOf("123") == 123 && HistoryMath.IdNumberOf("Human") == -1 && HistoryMath.IdNumberOf(null) == -1
          && HistoryMath.IdNumberOf("Hu12man") == -1 && HistoryMath.IdNumberOf("Human99999999999") == -1, "the number in a ThingID");

        // A chronicle line as an event: the same tick, words and kind, the colonist, the colony.
        var line = HistoryMath.FromChronicle(1234, 16, "Ana died.", "Human77", col);
        C(line.tick == 1234 && line.first == 1234 && line.kind == "loss" && line.line == "Ana died." && line.place == col && line.Weighty
          && line.actors.SequenceEqual(new[] { new HistoryActor(Ref.Person(77), HistoryRole.About) }), "a chronicle line comes in as it is");
        var none = HistoryMath.FromChronicle(5, 0, "A wish.", null, Ref.None);
        C(none.kind == "wishMade" && !none.Weighty && none.actors.Count == 0 && none.place.IsNone, "a line about nobody, with no colony");

        // ---------------- Indexes and queries ----------------

        var book = new HistoryBook();
        HistoryEntry E(int tick, string kind, Ref who, Ref place, int weight = HistoryMath.Everyday, string text = null, HistoryRole role = HistoryRole.About)
        {
            var e = new HistoryEntry { tick = tick, kind = kind, place = place, weight = weight, line = text };
            if (!who.IsNone) e.actors.Add(new HistoryActor(who, role));
            return e;
        }
        var e1 = book.Add(E(10 * Day, "wishGranted", a, col, text: "one"));
        var e2 = book.Add(E(11 * Day, "loss", a, col, HistoryMath.Milestone, "two"));
        var e3 = book.Add(E(12 * Day, "wishGranted", b, col, text: "three"));
        var e4 = book.Add(E(13 * Day, "world:began", fac, town, HistoryMath.Milestone, "four"));
        var e5 = book.Add(E(9 * Day, "bond", a, col, text: "five, told late"));
        C(book.Count == 5 && e1.id == 1 && e5.id == 5 && book.nextId == 6, "ids in order of coming");
        C(book.events.Select(e => e.id).SequenceEqual(new[] { 5, 1, 2, 3, 4 }), "kept oldest first, an older one in its place");
        C(book.Of(a).Select(e => e.id).SequenceEqual(new[] { 2, 1, 5 }), "of someone, newest first");
        C(book.Of(a, new[] { "loss" }).Select(e => e.id).SequenceEqual(new[] { 2 }) && book.Of(a, since: 10 * Day).Count == 2 && book.Of(a, max: 1).Single() == e2,
            "of someone, by kind, since, at most");
        C(book.At(col).Count == 4 && book.At(town).Single() == e4 && book.At(Ref.Town(56)).Count == 0 && book.At(Ref.None).Count == 0, "at a place");
        C(book.OfKind("world").Single() == e4 && book.OfKind("world:began").Single() == e4 && book.OfKind("world:ended").Count == 0, "by kind and family");
        C(book.Latest("wishGranted") == e3 && book.Latest("wishGranted", a) == e1 && book.Latest("wishGranted", place: town) == null && book.Latest("nothing") == null,
            "the latest of a kind, of someone, at a place");
        C(book.CountOf() == 5 && book.CountOf("wishGranted") == 2 && book.CountOf(who: a) == 3 && book.CountOf("wishGranted", a, col) == 1 && book.CountOf(since: 11 * Day) == 3,
            "how many times");
        C(book.Get(3) == e3 && book.Get(99) == null, "by id");
        C(book.Of(fac).Single() == e4 && e4.RoleOf(fac) == HistoryRole.About && e4.RoleOf(a) == null && e4.Involves(fac) && !e4.Involves(Ref.None), "who took part, and how");

        // An actor named twice is indexed once.
        var twice = new HistoryEntry { tick = 14 * Day, kind = "bond", place = col };
        twice.actors.Add(new HistoryActor(a, HistoryRole.Did));
        twice.actors.Add(new HistoryActor(a, HistoryRole.With));
        book.Add(twice);
        C(book.Of(a).Count(e => e == twice) == 1, "an actor named twice counts once");

        // Loading: nulls and unreadable ones go, ids made unique, the next id past them, in order, indexes built.
        var saved = new List<HistoryEntry>
        {
            E(30, "bond", b, col), null, E(10, "loss", a, col, HistoryMath.Milestone), new HistoryEntry { tick = 5, kind = null }, E(20, "bond", a, col), E(25, "bond", a, Ref.None)
        };
        saved[0].id = 4;
        saved[2].id = 4;
        saved[4].id = 0;
        saved[5].id = 9;
        var loaded = new HistoryBook();
        loaded.Load(saved, 3);
        C(loaded.Count == 4 && loaded.events.Select(e => e.tick).SequenceEqual(new[] { 10, 20, 25, 30 }), "a save loads in order, without what can't be read");
        C(loaded.events.Select(e => e.id).Distinct().Count() == 4 && loaded.nextId > loaded.events.Max(e => e.id), "ids unique and the next past them");
        C(loaded.Of(a).Count == 3 && loaded.At(col).Count == 3 && loaded.Get(9)?.tick == 25, "indexes built on load");
        var empty = new HistoryBook();
        empty.Load(null, 0);
        C(empty.Count == 0 && empty.nextId == 1 && empty.Of(a).Count == 0, "an older save with no history");

        // ---------------- A run within a day: one event with a count ----------------

        var runs = new HistoryBook();
        HistoryEntry Deed(int tick, string kind, string name, Ref people, Ref place = default, int weight = HistoryMath.Everyday)
        {
            var e = new HistoryEntry { tick = tick, kind = kind, key = "CC_Memory_Deed_" + kind.Substring(5), weight = weight, place = place };
            e.actors.Add(new HistoryActor(people, HistoryRole.Against));
            e.actors.Add(new HistoryActor(col, HistoryRole.Did));
            if (name != null) e.values = new List<string> { name };
            return e;
        }
        var fell = runs.Add(Deed(100 * Day, "deed:fell", "Ana", fac), HistoryMath.MergeTicks);
        var fell2 = runs.Add(Deed(100 * Day + 300, "deed:fell", "Bo", fac), HistoryMath.MergeTicks);
        var fell3 = runs.Add(Deed(100 * Day + 900, "deed:fell", "Cy", fac), HistoryMath.MergeTicks);
        C(fell == fell2 && fell2 == fell3 && runs.Count == 1 && fell.count == 3 && fell.values == null && fell.tick == 100 * Day, "a raid's fallen are one event of three, with no one name");
        var same = runs.Add(Deed(100 * Day + 1000, "deed:fell", null, fac), HistoryMath.MergeTicks);
        C(same == fell && fell.count == 4, "and another");
        C(runs.Add(Deed(102 * Day, "deed:fell", null, fac), HistoryMath.MergeTicks) != fell && runs.Count == 2, "a day later is a new one");
        C(runs.Add(Deed(102 * Day + 10, "deed:fell", null, Ref.People(8)), HistoryMath.MergeTicks).count == 1 && runs.Count == 3, "another people's is their own");
        C(runs.Add(Deed(102 * Day + 20, "deed:fell", null, fac, town), HistoryMath.MergeTicks).count == 1 && runs.Count == 4, "somewhere else is its own");
        C(runs.Add(Deed(102 * Day + 30, "deed:killed", null, fac, default, HistoryMath.Milestone), HistoryMath.MergeTicks) != null
          && runs.Add(Deed(102 * Day + 40, "deed:killed", null, fac, default, HistoryMath.Milestone), HistoryMath.MergeTicks).count == 1 && runs.Count == 6, "milestones stand alone");
        var said = runs.Add(E(102 * Day + 50, "bond", a, col, text: "one line"), HistoryMath.MergeTicks);
        C(runs.Add(E(102 * Day + 60, "bond", a, col, text: "one line"), HistoryMath.MergeTicks) != said && runs.Count == 8, "a line of its own is never merged");
        C(runs.Add(Deed(102 * Day + 70, "deed:fell", null, fac)).count == 1 && runs.Count == 9, "nothing merges unless asked");
        C(runs.CountOf("deed:fell") == 8 && runs.CountOf("deed") == 10, "counts count every one");

        // ---------------- Folding ----------------

        var fold = new HistoryBook();
        // Ana's granted wishes in the first season (days 1 to 5), Bo's on days 2, 3 and 20 (two seasons), Ana's losses,
        // a bond on its own, a day's trading and two more days of it, and lately what happens now.
        for (int d = 1; d <= 5; d++) fold.Add(E(d * Day, "wishGranted", a, col, text: "Ana's wish " + d));
        fold.Add(E(2 * Day + 5, "wishGranted", b, col, text: "Bo's wish 2"));
        fold.Add(E(3 * Day + 5, "wishGranted", b, col, text: "Bo's wish 3"));
        var boLater = fold.Add(E(20 * Day, "wishGranted", b, col, text: "Bo's wish 20"));
        var loss4 = fold.Add(E(4 * Day + 7, "loss", a, col, HistoryMath.Milestone, "Ana lost a friend."));
        var loss6 = fold.Add(E(6 * Day + 7, "loss", a, col, HistoryMath.Milestone, "Ana lost her dog."));
        var bond7 = fold.Add(E(7 * Day, "bond", a, col, text: "Ana and Bo are friends."));
        HistoryEntry Trade(int tick, string name)
        {
            var e = new HistoryEntry { tick = tick, kind = "deed:traded", key = "CC_Memory_Deed_traded", values = new List<string> { name } };
            e.actors.Add(new HistoryActor(fac, HistoryRole.For));
            e.actors.Add(new HistoryActor(col, HistoryRole.Did));
            return e;
        }
        var firstTrade = fold.Add(Trade(10 * Day, "salt"), HistoryMath.MergeTicks);
        fold.Add(Trade(10 * Day + 2000, "wool"), HistoryMath.MergeTicks);
        fold.Add(Trade(11 * Day + 100, "salt"), HistoryMath.MergeTicks);
        fold.Add(Trade(12 * Day + 500, "beer"), HistoryMath.MergeTicks);
        var recent = new List<HistoryEntry>();
        for (int d = 100; d < 130; d += 3) recent.Add(fold.Add(E(d * Day, "wishGranted", a, col, text: "Ana's wish " + d)));
        int before = fold.Count;
        var countsBefore = new[] { fold.CountOf("wishGranted"), fold.CountOf(who: a), fold.CountOf(who: b), fold.CountOf("deed"), fold.CountOf("loss"), fold.CountOf() };
        int firstAnaId = fold.Of(a, new[] { "wishGranted" }).Where(e => e.tick < 10 * Day).Min(e => e.id);
        int gone = fold.Fold(130 * Day, 0);
        var countsAfter = new[] { fold.CountOf("wishGranted"), fold.CountOf(who: a), fold.CountOf(who: b), fold.CountOf("deed"), fold.CountOf("loss"), fold.CountOf() };
        C(countsBefore.SequenceEqual(countsAfter), $"folding keeps every count ({string.Join(",", countsBefore)} / {string.Join(",", countsAfter)})");
        // Ana's five became one; Bo's two in the first season one, his third (another season) stays; the trading one.
        C(gone == 4 + 1 + 2 && fold.Count == before - gone, $"seven fewer ({gone})");
        var anaSum = fold.Of(a, new[] { "wishGranted" }).Single(e => e.tick < 10 * Day);
        C(anaSum.folded && anaSum.count == 5 && anaSum.first == 1 * Day && anaSum.tick == 5 * Day && anaSum.line == null && anaSum.place == col
          && anaSum.actors.SequenceEqual(new[] { new HistoryActor(a, HistoryRole.About) }) && anaSum.id == firstAnaId, "Ana's wishes that season: one summary of five, by her oldest's id");
        var boSum = fold.Of(b, new[] { "wishGranted" }).Where(e => e.tick < 10 * Day).Single();
        C(boSum.folded && boSum.count == 2 && boSum.first == 2 * Day + 5, "Bo's first season: one of two");
        C(!boLater.folded && boLater.line == "Bo's wish 20" && fold.Get(boLater.id) == boLater, "Bo's one wish of the next season stays as it was");
        C(fold.Get(loss4.id) == loss4 && loss4.line == "Ana lost a friend." && !loss4.folded && fold.Get(loss6.id) == loss6, "milestones stay as they are");
        C(fold.Get(bond7.id) == bond7 && !bond7.folded && bond7.line != null, "a small one alone in its season stays as it is");
        var trades = fold.OfKind("deed:traded").Single();
        C(trades == firstTrade && trades.folded && trades.count == 4 && trades.key == "CC_Memory_Deed_traded" && trades.values == null
          && trades.actors.SequenceEqual(new[] { new HistoryActor(fac, HistoryRole.For), new HistoryActor(col, HistoryRole.Did) }),
            "traded with them four times: one summary, its words' key kept, the people and the colony");
        C(recent.All(e => fold.Get(e.id) == e && !e.folded && e.line != null), "what is younger than sixty days stays");
        C(fold.Latest("loss", a) == loss6 && fold.Of(a).First() == recent.Last(), "queries after a fold");
        C(fold.Add(Trade(140 * Day, "salt"), HistoryMath.MergeTicks).count == 1 && fold.Add(Trade(141 * Day, "salt"), HistoryMath.MergeTicks).count == 2,
            "a day to the tick after is still the same run");
        int v = fold.Version;
        C(fold.Fold(130 * Day, 0) == 0 && fold.Version == v, "folding again the same day changes nothing");
        // A wish of that first season told late (an older tick) folds into the summary already there.
        var late = fold.Add(E(14 * Day, "wishGranted", a, col, text: "told late"));
        C(fold.Fold(131 * Day, 0) == 1 && anaSum.count == 6 && anaSum.tick == 14 * Day && fold.Get(late.id) == null && fold.Get(anaSum.id) == anaSum,
            "a late one of a folded season joins its summary");
        // Seasons are the planet's: a game that began mid-season splits its first fifteen days.
        C(HistoryMath.SeasonOf(0, 0) == 0 && HistoryMath.SeasonOf(15 * Day - 1, 0) == 0 && HistoryMath.SeasonOf(15 * Day, 0) == 1
          && HistoryMath.SeasonOf(0, 10 * Day) == 0 && HistoryMath.SeasonOf(5 * Day, 10 * Day) == 1 && HistoryMath.SeasonOf(-5, 0) == 0, "seasons by the planet's calendar");
        var split = new HistoryBook();
        split.Add(E(1 * Day, "bond", a, col));
        split.Add(E(6 * Day, "bond", a, col));
        split.Fold(100 * Day, 10 * Day);
        C(split.Count == 2 && split.events.All(e => !e.folded), "two days of different seasons stay apart");

        // Crowded: more than the bound within sixty days folds sooner, from thirty days and then fifteen.
        var crowd = new HistoryBook();
        var rnd = new Random(7);
        for (int i = 0; i <= HistoryMath.MaxEvents + HistoryMath.MaxEvents / 4; i++)
            crowd.Add(E(10 * Day + (int)((long)i * 50 * Day / 1001), "petition", Ref.Person(rnd.Next(10)), col, text: "p" + i));
        C(crowd.Count == HistoryMath.MaxEvents, $"far over the bound between folds, the oldest small ones go at once ({crowd.Count})");
        crowd = new HistoryBook();
        for (int i = 0; i < 950; i++)
            crowd.Add(E(10 * Day + (int)((long)i * 50 * Day / 950), "petition", Ref.Person(rnd.Next(10)), col, text: "p" + i));
        int total = crowd.CountOf();
        crowd.Fold(60 * Day, 0);
        C(crowd.Count <= HistoryMath.MaxEvents && crowd.CountOf() == total, $"crowded, they fold sooner and nothing is lost ({crowd.Count}, {crowd.CountOf()} of {total})");
        C(crowd.events.Where(e => e.tick >= 45 * Day).All(e => !e.folded), "the last fifteen days stay as they are");

        // Over the bound with older seasons' summaries: they make way, and the last sixty days stay whole.
        var seasons = new HistoryBook();
        for (int d = 0; d < 200; d++)
        for (int p = 0; p < 6; p++)
            seasons.Add(E(d * Day + p, "petition", Ref.Person(p * 1000 + d), col, text: "old " + d));
        var lately = new List<HistoryEntry>();
        for (int i = 0; i < 700; i++) lately.Add(seasons.Add(E(200 * Day + (int)((long)i * 59 * Day / 700), "bond", Ref.Person(i % 40), col, text: "lately " + i)));
        seasons.Fold(260 * Day, 0);
        C(seasons.Count <= HistoryMath.MaxEvents && lately.All(e => seasons.Get(e.id) == e && !e.folded), $"older seasons make way first; the last sixty days stay whole ({seasons.Count})");

        // Too many milestones even after folding: the oldest small ones go first, then the oldest milestones, the newest always kept.
        var full = new HistoryBook();
        for (int i = 0; i < 900; i++) full.Add(E(i * 10, i % 3 == 0 ? "loss" : "bond", Ref.Person(i), col, i % 3 == 0 ? HistoryMath.Milestone : HistoryMath.Everyday, "x" + i));
        var newest = full.events.Skip(full.Count - HistoryMath.KeepNewest).ToList();
        full.Fold(10000, 0);
        C(full.Count == HistoryMath.MaxEvents && newest.All(e => full.Get(e.id) == e), "over the bound: the newest stay");
        C(full.events.Count(e => e.Weighty) == 300, "and every milestone, while small ones are left to go");

        // ---------------- Five years: the bound holds, and the chronicle is in it as written ----------------

        // A busy colony (about eight chronicle lines a day, save_estimate.py's generous rate, and four deeds) and an
        // ordinary one (about four lines and two deeds).
        foreach (var (name, linesMin, linesMax, deeds, seed) in new[] { ("busy", 6, 11, 4, 11), ("ordinary", 3, 6, 2, 12) })
        {
            bool busy = name == "busy";
            var years = new HistoryBook();
            var r5 = new Random(seed);
            int maxSeen = 0;
            // What the old chronicle list (300 lines, the newest 200 always, milestones longest) would hold, to compare.
            var chronicle = new List<(int tick, string kind, string text)>();
            var everMile = new List<(int tick, string kind, string text)>();
            var lineAt = new Dictionary<(int, string), string>();
            bool Mile((int tick, string kind, string text) x) => HistoryKinds.Milestones.Contains(x.kind);
            bool within = true;
            for (int d = 0; d < 300 && within; d++)
            {
                int lines = r5.Next(linesMin, linesMax);
                for (int i = 0; i < lines; i++)
                {
                    int tick = d * Day + r5.Next(Day / 2);
                    int kind = r5.Next(5) == 0 ? new[] { 14, 16, 19, 15, 7 }[r5.Next(5)] : new[] { 0, 1, 2, 3, 4, 10, 11, 12, 13, 17, 18 }[r5.Next(11)];
                    string text = new string('x', 60 + r5.Next(120)) + d + "." + i;
                    var entry = (tick, HistoryKinds.OfChronicle(kind), text);
                    chronicle.Add(entry);
                    if (chronicle.Count > 300) Caps.TrimKeeping(chronicle, 300, Mile, 200);
                    if (Mile(entry)) everMile.Add(entry);
                    string who = "Human" + r5.Next(20);
                    lineAt[(tick, text)] = who;
                    years.Add(HistoryMath.FromChronicle(tick, kind, text, who, col));
                }
                for (int i = 0; i < deeds; i++)
                {
                    var e = new HistoryEntry { tick = d * Day + r5.Next(Day / 2), kind = HistoryKinds.OfDeed(new[] { "traded", "fell", "gift", "raided", "tended" }[r5.Next(5)]) };
                    e.actors.Add(new HistoryActor(Ref.People(r5.Next(8)), HistoryRole.For));
                    e.actors.Add(new HistoryActor(col, HistoryRole.Did));
                    e.weight = r5.Next(20) == 0 ? HistoryMath.Milestone : HistoryMath.Everyday;
                    years.Add(e, HistoryMath.MergeTicks);
                }
                maxSeen = Math.Max(maxSeen, years.Count);
                years.Fold(d * Day + Day / 2, 0);
                within = years.Count <= HistoryMath.MaxEvents;
            }
            C(within, $"{name}: within the bound after every day's fold ({years.Count})");
            C(maxSeen <= HistoryMath.MaxEvents + HistoryMath.MaxEvents / 4, $"{name}: never far over it between folds ({maxSeen})");
            // Rough size in the save: each event's XML, as HistoryEntry.ExposeData writes it.
            long bytes = years.events.Sum(e => (long)Size(e));
            C(bytes < 300 * 1024, $"{name}: about {bytes / 1024} KB in the save after five years");
            if (Environment.GetEnvironmentVariable("RIMFOLK_HISTORY_PRINT") == "1")
                Console.WriteLine($"history, {name} colony after five years: {years.Count} events ({years.events.Count(e => e.folded)} summaries, "
                                  + $"{years.events.Count(e => e.Weighty)} milestones), {bytes / 1024} KB, {bytes / Math.Max(1, years.Count)} bytes each, "
                                  + $"the most between folds {maxSeen}, reaching back to day {years.events.Min(e => e.tick) / Day}");
            C(years.events.Count(e => e.folded) > 0 && years.events.Where(e => e.folded).All(e => e.line == null && e.count > 1), $"{name}: old seasons are summaries without lines");

            // The chronicle is the history's view (ChronicleMath.Shows): what it shows lately is there line for line, the
            // last sixty days in an ordinary colony, the last fifteen in a busy one (crowded, it folds sooner); and every
            // milestone the history still reaches.
            var view = years.events.Where(e => ChronicleMath.Shows(e, null)).ToList();
            int now5 = 299 * Day + Day / 2;
            int window = busy ? HistoryMath.CrowdedFoldDays.Min() : HistoryMath.FoldDays;
            var hist = new HashSet<(int, string, string)>(view.Where(e => e.line != null).Select(e => (e.tick, e.kind, e.line)));
            var young = chronicle.Where(x => x.tick >= now5 - window * Day).ToList();
            C(young.Count > 0 && young.All(x => hist.Contains((x.tick, x.kind, x.text))), $"{name}: the chronicle's last {window} days are in its view as written ({young.Count})");
            int oldestKept = years.events.Min(e => e.tick);
            var miles2 = everMile.Where(x => x.tick >= oldestKept).ToList();
            C(miles2.Count > 0 && miles2.All(x => hist.Contains((x.tick, x.kind, x.text))), $"{name}: every milestone the history reaches back to is in the view ({miles2.Count} of {everMile.Count})");
            // The old list dropped its older everyday lines at 300; the view keeps every milestone ever written in an ordinary colony.
            if (!busy) C(miles2.Count == everMile.Count, $"{name}: every milestone of five years is kept ({miles2.Count} of {everMile.Count})");
            C(view.Where(e => !e.folded).All(e => e.line != null), $"{name}: a chronicle line that isn't folded keeps its words");
            C(view.Count == years.events.Count(e => HistoryKinds.Chronicle.Contains(HistoryKinds.Family(e.kind))) && view.All(e => !HistoryKinds.Matches(e.kind, HistoryKinds.Deed)),
                $"{name}: the view is the chronicle's lines and their summaries, never a people's deed ({view.Count})");
            // Nothing the old list would still show is lost: each of its lines is in the view as written, or counted in its
            // season's summary (folded into it), unless the bound let it go.
            int lost = 0;
            foreach (var x in chronicle)
            {
                if (hist.Contains((x.tick, x.kind, x.text))) continue;
                var told = HistoryMath.FromChronicle(x.tick, HistoryKinds.Chronicle.ToList().IndexOf(x.kind), x.text, lineAt[(x.tick, x.text)], col);
                if (!view.Any(s => HistoryBook.Holds(s, told))) lost++;
            }
            C(busy || lost == 0, $"{name}: every line the old chronicle list would show is in the view, or counted in its season's summary ({lost} not)");
            // It reaches back further than the old list did.
            C(view.Min(e => e.first) <= chronicle.Min(x => x.tick), $"{name}: the view reaches back to day {view.Min(e => e.first) / Day}, the old list to day {chronicle.Min(x => x.tick) / Day}");
        }

        // An older save's list taken in where the history already folded its lines: counted once, not twice.
        var folded = new HistoryBook();
        var anaLines = new List<HistoryEntry>();
        for (int i = 0; i < 4; i++)
        {
            var l = HistoryMath.FromChronicle(i * Day + 5, 0, "Ana wished " + i + ".", "Human1", col);
            anaLines.Add(HistoryMath.FromChronicle(l.tick, 0, l.line, "Human1", col));
            folded.Add(l);
        }
        folded.Add(HistoryMath.FromChronicle(5, 16, "Bo died.", "Human2", col));
        folded.Fold(80 * Day, 0);
        int wishes = folded.CountOf("wishMade");
        C(folded.events.Count(e => e.folded) == 1 && wishes == 4, "four small lines of a season fold into one summary of four");
        anaLines.Add(HistoryMath.FromChronicle(5, 16, "Bo died.", "Human2", col));
        anaLines.Add(HistoryMath.FromChronicle(70 * Day, 0, "Cy wished.", "Human3", col));
        C(folded.TakeChronicle(anaLines) == 1 && folded.CountOf("wishMade") == wishes + 1 && folded.CountOf("loss") == 1,
            "an older save's lines already folded or kept stay out; only the one the history lacks comes in");
        C(!HistoryBook.Holds(folded.events.First(e => e.folded), HistoryMath.FromChronicle(5, 0, "Ana wished 0.", "Human9", col))
          && !HistoryBook.Holds(folded.events.First(e => e.folded), HistoryMath.FromChronicle(5, 1, "x", "Human1", col)),
            "a summary holds only lines of its kind and of the one it is of");

        // An older save's chronicle comes in once, as it is, and again nothing (the same tick and words, as often as there).
        var older = new HistoryBook();
        var oldLines = new List<HistoryEntry>
        {
            HistoryMath.FromChronicle(10, 16, "Ana died.", "Human1", col), HistoryMath.FromChronicle(20, 0, "Bo wished.", "Human2", col),
            HistoryMath.FromChronicle(20, 0, "Bo wished.", "Human2", col)
        };
        C(older.TakeChronicle(oldLines) == 3 && older.Count == 3, "an older save's chronicle comes in whole, the same line twice twice");
        var again = new List<HistoryEntry> { HistoryMath.FromChronicle(10, 16, "Ana died.", "Human1", col), HistoryMath.FromChronicle(20, 0, "Bo wished.", "Human2", col),
            HistoryMath.FromChronicle(20, 0, "Bo wished.", "Human2", col), HistoryMath.FromChronicle(30, 0, "New.", "Human2", col) };
        C(older.TakeChronicle(again) == 1 && older.Count == 4, "and only what isn't there yet the next time");

        // ---------------- The mod's side: one record, the chronicle its view ----------------

        string comp = File.ReadAllText(Path.Combine(src, "CouncilGameComponent.cs"));
        string tellCode = File.ReadAllText(Path.Combine(src, "UI", "Tell.cs"));
        C(comp.Contains("public void AddChronicle(ChronicleEntry entry) => Tell.Chronicle(entry);") && tellCode.Contains("History.FromChronicle(entry);"),
            "a chronicle line is written into the history by the teller, and kept nowhere else");
        string hist2 = File.ReadAllText(Path.Combine(src, "World", "Ledger", "History.cs"));
        var from = Regex.Match(hist2, @"public static void FromChronicle\(ChronicleEntry entry\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(from.Contains("try") && from.Contains("catch") && !from.Contains(".chronicle"), "the history's side can't throw into it");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(src.Length + 1).Replace('\\', '/');
            if (rel == "CouncilGameComponent.cs" || rel == "World/Ledger/History.cs") continue;
            C(!Regex.IsMatch(File.ReadAllText(file), @"\bcomp\??\.chronicle\b|\bInstance\??\.chronicle\b"), $"{rel} reads the chronicle as the history's view, not the old list");
        }
        C(Regex.Matches(comp, @"if \(Scribe\.mode != LoadSaveMode\.Saving\) Scribe_Collections\.Look\(ref chronicle, ""chronicle"", LookMode\.Deep\)").Count == 1,
            "an older save's chronicle list is read, never written again");
        var ready = Regex.Match(hist2, @"private void HistoryReady\(\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(ready.Contains("history.TakeChronicle(lines);") && ready.Contains("chronicle.Clear();") && ready.Contains("catch"),
            "the ledger takes an older save's list into the history at every load, each line once, then lets it go");
        string led = File.ReadAllText(Path.Combine(src, "World", "Ledger", "WorldLedger.cs"));
        C(led.Contains("ExposeHistory();") && led.Contains("HistoryReady();"), "the ledger saves the history and takes an older save's chronicle in");
        C(hist2.Contains("Sched.Every(tm.TicksGame, Sched.DayTicks, Sched.DailyHistory)") && hist2.Contains("PassClock.Time(Pass.DailyHistory)"), "the history folds in its own daily pass, timed");

        // ---------------- The daily slot ----------------

        int dh = Sched.DailyHistory;
        C(dh > 0 && dh < Sched.HourTicks, "the history's day runs in the first hour of the day");
        C(!Sched.HourlyOffsets.Contains(dh), "on no hourly pass's tick");
        var rare = Sched.RareOffsets.Concat(new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers, Sched.RareVisitors, Sched.RareTimed,
            Sched.RareRearrange, Sched.RareNeighbours, Sched.RareOutsiders, Sched.RareJourneys, Sched.RareOutsiderTalk, Sched.RareHauling }).ToList();
        C(!rare.Contains(dh % Sched.RareTicks), "on no 250-tick pass's tick");
        C(dh % Sched.PeopleLogPeriod != Sched.PeopleLog, "nor the people log's");
        C(new[] { Sched.DailyWealth, Sched.DailyMagicNeeds, Sched.DailyBestFriends }.All(o => o != dh), "nor another daily pass's");
        C(PassTable.All.Any(p => p.pass == Pass.DailyHistory && p.period == Sched.DayTicks && p.offset == dh), "the clock has its pass");

        // ---------------- World situations name their place ----------------

        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').EndsWith("World/WorldSituation.cs")) continue;
            var text = File.ReadAllText(file);
            C(!Regex.IsMatch(text, @"\bs\.ctx\.placeName\s*=[^=]"), $"{Path.GetFileName(file)}: a situation's place is set with its town (WorldSituation.At)");
        }
        string ws = File.ReadAllText(Path.Combine(src, "World", "WorldSituation.cs"));
        C(Regex.Matches(ws, @"Refs\.Look\(ref place, ""place""\)").Count == 2, "situations and their records save their place");
        string wss = File.ReadAllText(Path.Combine(src, "World", "WorldSituations.cs"));
        C(wss.Contains("place = s.place") && Regex.Matches(wss, @"History\.Situation\(s, ""began""\)").Count == 1 && Regex.Matches(wss, @"using \(History\.Situation\(s, ""ended""\)\)").Count == 2,
            "a record keeps the place; the chronicle lines carry it into the history");

        // ---------------- The save model ----------------

        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/Ledger/History.cs:events\"") && est.Contains("\"World/Ledger/History.cs:values\"") && est.Contains("\"MaxEvents\""), "the save model has the history");

        return n;
    }

    /// <summary>About how many bytes an event takes in the save (tabs, tags and values, as ExposeData writes them).</summary>
    private static int Size(HistoryEntry e)
    {
        int Tag(string name, string value) => value == null ? 0 : 6 + 2 * name.Length + 5 + value.Length;
        int s = 4 + 9 + 2; // <li>, </li>, line ends
        s += Tag("id", e.id.ToString()) + Tag("tick", e.tick.ToString()) + Tag("kind", e.kind);
        if (e.first != e.tick) s += Tag("first", e.first.ToString());
        if (e.count != 1) s += Tag("count", e.count.ToString());
        var packed = HistoryMath.PackActors(e.actors);
        if (packed.Length > 0) s += Tag("actors", packed);
        if (!e.place.IsNone) s += Tag("place", e.place.ToString());
        if (e.weight != HistoryMath.Everyday) s += Tag("weight", e.weight.ToString());
        s += Tag("key", e.key) + Tag("line", e.line);
        if (e.values != null && e.values.Count > 0) s += 20 + e.values.Sum(x => Tag("li", x));
        if (e.folded) s += Tag("folded", "True");
        return s;
    }
}
