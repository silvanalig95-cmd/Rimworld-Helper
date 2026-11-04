using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The region (World/Region/RegionMath.cs; docs/design/stage1-region.md): the towns kept in detail (the nearest 40 within
/// the reach, humanlike peoples only, one set for every home), remoteness (its edges at 12 and 40 tiles, the lone-town
/// step), prosperity (bands, the seed by the land, an event's move spread over its first days, full at its town and half
/// at the others, drift, a war near, the cause that fits), the new towns (their first year, the founder's letter, why
/// they came), the bounds of what is kept, and the two passes' slots. Also the mod's side: the passes timed, the def
/// field and its values, the ledger's new records saved and modelled, Faction Territories' near messages given back
/// when the region doesn't take them, and each home's part kept by the colonies' book.
/// </summary>
public static class RegionTests
{
    private const int Day = RegionMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "region: " + what);
        }

        // ---------------- The reach in detail ----------------

        var d = new List<float> { 30f, 5f, 12f, 35f, 35.5f, 20f, 12f };
        var fit = new List<bool> { true, true, true, true, true, false, true };
        var r = RegionMath.InReach(d, fit, 35f);
        C(r.SequenceEqual(new[] { 1, 2, 6, 0, 3 }), "within the reach, nearest first, equals in the order they came: " + string.Join(",", r));
        C(!r.Contains(4), "a town just past the reach is not kept");
        C(!r.Contains(5), "a town of a people that isn't kept (an insect hive, the player's own) is left out");
        C(RegionMath.InReach(d, null, 35f).Count == 6, "with no filter every town within reach counts");
        var many = Enumerable.Range(0, 120).Select(i => i * 0.25f).ToList();
        var nearest = RegionMath.InReach(many, null, 35f);
        C(nearest.Count == RegionMath.MaxTownsPerHome && nearest.SequenceEqual(Enumerable.Range(0, RegionMath.MaxTownsPerHome)),
            $"a crowded region keeps the nearest {RegionMath.MaxTownsPerHome}");
        C(RegionMath.MaxTownsPerHome == 40 && RegionMath.DefaultReach == 35f, "40 towns a home, the region map's reach (35 by default)");
        C(RegionMath.InReach(new List<float>(), null, 35f).Count == 0 && RegionMath.InReach(null, null, 35f).Count == 0, "no towns, nothing kept");
        C(RegionMath.InReach(d, fit, 35f, 0).Count == 0, "a cap of nothing keeps nothing");
        var union = RegionMath.Union(new List<IList<int>> { new List<int> { 4, 7, 9 }, new List<int> { 9, 2, 4 }, null });
        C(union.SequenceEqual(new[] { 4, 7, 9, 2 }), "a town in reach of two homes is kept once (one fortune)");

        // ---------------- Remoteness ----------------

        C(RegionMath.Remoteness(5f, 6) == 0f && RegionMath.Remoteness(12f, 3) == 0f, "a town at peace within 12 tiles: not remote at all");
        C(Math.Abs(RegionMath.Remoteness(26f, 3) - 0.5f) < 1e-4, "halfway between 12 and 40: half remote");
        C(RegionMath.Remoteness(40f, 2) == 1f && RegionMath.Remoteness(90f, 5) == 1f, "40 tiles and beyond: fully remote");
        C(Math.Abs(RegionMath.Remoteness(26f, 1) - 0.7f) < 1e-4, "fewer than two towns at peace within 40 tiles adds 0.2");
        C(RegionMath.Remoteness(5f, 1) == 0.2f, "even a near lone town leaves a little of it");
        C(RegionMath.Remoteness(38f, 0) == 1f, "never more than 1");
        C(RegionMath.Remoteness(-1f, 0) == 1f && RegionMath.Remoteness(float.PositiveInfinity, 0) == 1f, "no town at peace anywhere: as remote as it gets");
        C(RegionMath.RemoteLevel(0f) == 0 && RegionMath.RemoteLevel(0.2f) == 1 && RegionMath.RemoteLevel(0.5f) == 2 && RegionMath.RemoteLevel(0.8f) == 3
          && RegionMath.RemoteLevel(1f) == 4, "remoteness in words");
        float lastR = -1f;
        bool rises = true;
        for (float t = 0f; t <= 50f; t += 0.5f)
        {
            float v = RegionMath.Remoteness(t, 3);
            if (v < lastR) rises = false;
            lastR = v;
        }
        C(rises, "it never falls as the nearest town gets farther");

        // ---------------- Prosperity: bands, seed, clamp ----------------

        C(RegionMath.Band(0f) == Fortune.Struggling && RegionMath.Band(19.9f) == Fortune.Struggling && RegionMath.Band(20f) == Fortune.Poor
          && RegionMath.Band(39.9f) == Fortune.Poor && RegionMath.Band(40f) == Fortune.Steady && RegionMath.Band(59.9f) == Fortune.Steady
          && RegionMath.Band(60f) == Fortune.Thriving && RegionMath.Band(79.9f) == Fortune.Thriving && RegionMath.Band(80f) == Fortune.Flourishing
          && RegionMath.Band(100f) == Fortune.Flourishing, "bands: struggling under 20, poor to 40, steady to 60, thriving to 80, flourishing above");
        C(RegionMath.Tag(Fortune.Struggling) == "town:struggling" && RegionMath.Tag(Fortune.Flourishing) == "town:flourishing", "each band's tag");
        C(Enum.GetValues(typeof(Fortune)).Cast<Fortune>().All(f => FactsMath.RegionTags.Contains(RegionMath.Tag(f))), "every band's tag is one the facts give");
        C(RegionMath.Clamp(-5f) == 0f && RegionMath.Clamp(130f) == 100f && RegionMath.Clamp(55f) == 55f && RegionMath.Clamp(90f, RegionMath.PostCap) == 70f,
            "within 0 and 100 (a trading post 70)");
        C(RegionMath.Seed(1f, false) == 50f && RegionMath.Seed(2f, false) == 50f, "a town first seen on good land starts at 50");
        C(RegionMath.Seed(0f, false) == 35f && Math.Abs(RegionMath.Seed(0.5f, false) - 42.5f) < 1e-4, "less on poor land, by its biome's settlement weight");
        C(RegionMath.Seed(1f, true) == 40f && RegionMath.Seed(0f, true) == 25f, "less again while its people are in hard times");
        C(RegionMath.NewTownStart == 30f && RegionMath.TakenStart == 25f, "a new town starts at 30, a town taken at 25");
        C(RegionMath.DriftTarget(1f) == 50f && RegionMath.DriftTarget(0.2f) == 45f && RegionMath.IsPoorLand(0.49f) && !RegionMath.IsPoorLand(0.5f),
            "it drifts back toward 50, 45 on poor land");
        C(Math.Abs(RegionMath.Drift(20f, 50f, 1f) - 20.3f) < 1e-4 && Math.Abs(RegionMath.Drift(80f, 50f, 2f) - 79.4f) < 1e-4, "0.3 a day toward it");
        C(RegionMath.Drift(49.9f, 50f, 1f) == 50f && RegionMath.Drift(50.1f, 50f, 3f) == 50f && RegionMath.Drift(50f, 50f, 5f) == 50f, "never past it");
        C(RegionMath.Drift(30f, 50f, 0f) == 30f && RegionMath.Drift(30f, 50f, -2f) == 30f, "no time, no drift");

        // ---------------- An event's move ----------------

        C(RegionMath.Share(-20f, true) == -20f && RegionMath.Share(-20f, false) == -10f, "in full at the event's town, half at its people's other towns");
        C(RegionMath.SpreadDays == 5 && RegionMath.DayShare(-20f) == -4f, "spread over its first five days");
        C(RegionMath.DaysDue(0f, 0) == 1 && RegionMath.DaysDue(0.9f, 1) == 0 && RegionMath.DaysDue(1f, 1) == 1 && RegionMath.DaysDue(3.5f, 1) == 3,
            "a share for each day begun, less those given");
        C(RegionMath.DaysDue(12f, 0) == 5 && RegionMath.DaysDue(12f, 5) == 0 && RegionMath.DaysDue(-1f, 0) == 0 && RegionMath.DaysDue(2f, 9) == 0,
            "never more than five, never again, never before it starts");
        foreach (var start in new[] { 0f, 0.3f, 0.75f })
        {
            // The region's day comes once a day; the event starts at some hour of it.
            float town = 0f, other = 0f;
            int applied = 0;
            for (int day = 0; day < 20; day++)
            {
                float daysIn = day + 1f - start;
                int due = RegionMath.DaysDue(daysIn, applied);
                applied += due;
                town += RegionMath.Share(RegionMath.DayShare(-25f) * due, true);
                other += RegionMath.Share(RegionMath.DayShare(-25f) * due, false);
            }
            C(Math.Abs(town + 25f) < 1e-3 && Math.Abs(other + 12.5f) < 1e-3 && applied == 5, $"a famine starting {start:0.##} into a day moves its town 25 and the others 12.5, in all");
        }
        // A reload or a missed day: what is due is caught up, never twice.
        int a2 = 0;
        int total = RegionMath.DaysDue(0.2f, a2);
        a2 += total;
        total += RegionMath.DaysDue(3.4f, a2);
        a2 = total;
        total += RegionMath.DaysDue(3.4f, a2);
        C(total == 4, $"a day missed is caught up the next time, and asking twice gives nothing more ({total})");

        // ---------------- War, low, causes ----------------

        C(RegionMath.WarMove(20f) == -0.3f && RegionMath.WarMove(5f) == -0.3f && RegionMath.WarMove(20.5f) == 0f && RegionMath.WarMove(-1f) == 0f,
            "within 20 tiles of an enemy's town: 0.3 a day");
        C(RegionMath.LowSince(14f, -1, 500) == 500 && RegionMath.LowSince(14f, 200, 500) == 200 && RegionMath.LowSince(15f, 200, 500) == -1,
            "low since it first went under 15, until it is not");
        C(RegionMath.CauseFits(Fortune.Struggling, -1) && RegionMath.CauseFits(Fortune.Steady, -1) && !RegionMath.CauseFits(Fortune.Thriving, -1),
            "a blow is named while the town is steady or worse");
        C(RegionMath.CauseFits(Fortune.Flourishing, 1) && RegionMath.CauseFits(Fortune.Steady, 1) && !RegionMath.CauseFits(Fortune.Poor, 1),
            "a boon while it is steady or better");
        C(RegionMath.CauseFits(Fortune.Poor, 0) && RegionMath.CauseFits(Fortune.Flourishing, 0), "its founding always");
        C(RegionMath.CauseDays == 60, "named for 60 days");

        // Five years of a town: plagues, fairs and drift never take it out of 0 to 100, and it comes back toward 50.
        var rng = new Random(7);
        float p = 50f;
        bool inside = true;
        for (int day = 0; day < 300; day++)
        {
            p = RegionMath.Drift(p, 50f, 1f);
            if (rng.Next(20) == 0) p += RegionMath.DayShare(rng.Next(2) == 0 ? -30f : 12f) * 5;
            p = RegionMath.Clamp(p);
            inside &= p >= 0f && p <= 100f;
        }
        for (int day = 0; day < 200; day++) p = RegionMath.Drift(p, 50f, 1f);
        C(inside && p == 50f, "five years of events stay within 0 to 100, and quiet years bring it back to 50");

        // ---------------- New towns ----------------

        C(RegionMath.IsNew(0, 0) && RegionMath.IsNew(0, 60 * Day - 1) && !RegionMath.IsNew(0, 60 * Day) && !RegionMath.IsNew(-1, 5) && !RegionMath.IsNew(100, 50),
            "new for its first year (60 days); not when its founding isn't known");
        C(RegionMath.LetterDue(15f, -1, 0) && !RegionMath.LetterDue(15.5f, -1, 0) && !RegionMath.LetterDue(-1f, -1, 0), "a founder writes to a home within 15 tiles");
        C(!RegionMath.LetterDue(5f, 10 * Day, 24 * Day) && RegionMath.LetterDue(5f, 10 * Day, 25 * Day), "at most one founder's letter a season per home");
        C(RegionMath.FoundersADay == 2 && RegionMath.FounderMinAge == 30f && RegionMath.FounderMaxAge == 55f, "founders two a day at most, aged 30 to 55");
        C(RegionMath.WhyFounded(new HashSet<string>(), 0.1f) == RegionMath.AnyLand && RegionMath.WhyFounded(null, 0.9f) == RegionMath.AnyLand, "with nothing in the world, the land itself");
        var holds = new HashSet<string> { "hills", "road", "refugees" };
        C(RegionMath.WhyFounded(holds, 0.2f) == "refugees", "the strongest reason most of the time: a town of theirs given up lately");
        C(RegionMath.WhyFounded(holds, 0.7f) == "road" && RegionMath.WhyFounded(holds, 0.99f) == "hills", "now and then one of the others");
        C(RegionMath.WhyFounded(new HashSet<string> { "coast" }, 0.95f) == "coast", "a lone reason always");
        C(RegionMath.Reasons.SequenceEqual(new[] { "refugees", "ruin", "mother", "road", "river", "coast", "hills" }), "the reasons, strongest first");
        var whys = new HashSet<string>();
        for (int i = 0; i < 100; i++) whys.Add(RegionMath.WhyFounded(new HashSet<string> { "ruin", "river", "coast" }, i / 100f));
        C(whys.SetEquals(new[] { "ruin", "river", "coast" }), "every reason that holds can come up");

        // ---------------- The full welcome (decision 1) ----------------

        C(RegionMath.WelcomeDue(15f, -1, 0) && !RegionMath.WelcomeDue(16f, -1, 0) && !RegionMath.WelcomeDue(-1f, -1, 0), "the full welcome for a new town within 15 tiles");
        C(!RegionMath.WelcomeDue(5f, 0, 59 * Day) && RegionMath.WelcomeDue(5f, 0, 60 * Day), "at most once a year per home");

        // ---------------- What a town sells (decision 3) ----------------

        C(RegionMath.StockFactor(Fortune.Struggling, RegionMath.StockKind.Food) == 0.75f && RegionMath.StockFactor(Fortune.Struggling, RegionMath.StockKind.Medicine) == 0.75f
          && RegionMath.StockFactor(Fortune.Struggling, RegionMath.StockKind.Silver) == 0.75f, "a struggling town: a quarter less food, medicine and silver");
        C(RegionMath.StockFactor(Fortune.Thriving, RegionMath.StockKind.Silver) == 1.1f && RegionMath.StockFactor(Fortune.Flourishing, RegionMath.StockKind.Silver) == 1.2f,
            "a thriving town a little more silver, a flourishing one more again");
        C(RegionMath.StockFactor(Fortune.Flourishing, RegionMath.StockKind.Food) == 1f && RegionMath.StockFactor(Fortune.Poor, RegionMath.StockKind.Silver) == 1f
          && RegionMath.StockFactor(Fortune.Steady, RegionMath.StockKind.Medicine) == 1f, "everything else as the game made it");
        C(Enum.GetValues(typeof(Fortune)).Cast<Fortune>().All(f => RegionMath.StockFactor(f, RegionMath.StockKind.Other) == 1f), "goods that aren't food, medicine or silver are never touched");
        C(RegionMath.StockTarget(400, 0.75f) == 300 && RegionMath.StockTarget(500, 1.1f) == 550 && RegionMath.StockTarget(3, 0.75f) == 2, "a share of each good's stacks together");
        C(RegionMath.StockTarget(1, 0.75f) == 1 && RegionMath.StockTarget(0, 0.75f) == 0 && RegionMath.StockTarget(-4, 1.2f) == 0 && RegionMath.StockTarget(7, 1f) == 7,
            "a lone item stays, nothing stays nothing");

        // ---------------- Bounds ----------------

        var list = Enumerable.Range(0, 9).ToList();
        C(RegionMath.TrimOldest(list, RegionMath.MaxFoundings) == 3 && list.SequenceEqual(new[] { 3, 4, 5, 6, 7, 8 }), "6 foundings at most, the oldest going first");
        C(RegionMath.TrimOldest(list, 6) == 0 && RegionMath.TrimOldest<int>(null, 6) == 0, "nothing to trim under the cap");
        C(RegionMath.HousesPerTown == 2 && RegionMath.MembersPerHouse == 6, "two houses per town, six members each");
        // A founding at the day's look.
        C(RegionMath.FoundingDay(true, true, false, false, false) == FoundingFate.Keep, "a town built, its faces coming in the hour: kept");
        C(RegionMath.FoundingDay(true, false, false, true, true) == FoundingFate.LetGo,
            "a town built with the new towns' faces off: let go with its founder (no faces will come, and nothing is kept for good)");
        C(RegionMath.FoundingDay(false, false, true, true, false) == FoundingFate.Keep && RegionMath.FoundingDay(false, true, true, true, false) == FoundingFate.Keep,
            "a site still being built within reach: kept");
        C(RegionMath.FoundingDay(false, true, false, true, true) == FoundingFate.Built, "finished without the hook seeing it, faces on: its town stands");
        C(RegionMath.FoundingDay(false, false, false, true, true) == FoundingFate.LetGo && RegionMath.FoundingDay(false, true, true, false, false) == FoundingFate.LetGo
          && RegionMath.FoundingDay(false, true, false, true, false) == FoundingFate.LetGo, "finished with the faces off, out of reach, or razed: let go");
        // With the faces off, no founding stays for good (none is ever made then, and those there go within a day).
        var sitesLeft = new List<bool> { true, false, false, true, false, true };
        int day1 = sitesLeft.Count(b => RegionMath.FoundingDay(b, false, false, true, true) == FoundingFate.Keep);
        C(day1 == 0, "with the faces off, the foundings whose towns stand are gone the next day");
        C(RegionMath.KeepTown(false, true, true, -1, 0), "a standing town in detail is kept");
        C(!RegionMath.KeepTown(false, true, false, -1, 0), "beyond every home's reach, nothing is kept");
        C(RegionMath.KeepTown(true, true, false, -1, 0), "a town a notable lives in is kept wherever it is");
        C(RegionMath.KeepTown(false, false, false, 10 * Day, 100 * Day) && !RegionMath.KeepTown(false, false, false, 10 * Day, 131 * Day),
            "a gone town is remembered 120 days (two years) as before");
        // Over five years, a crowded region's ledger stays inside its bounds: 40 towns a home, two houses each, six foundings.
        var towns = new HashSet<int>();
        var houses = new Dictionary<int, int>();
        var foundings = new List<int>();
        int nextId = 1;
        var rng2 = new Random(3);
        int most = 0, mostHouses = 0;
        for (int day = 0; day < 300; day++)
        {
            // About a town a day across the world, a few in reach of the colony.
            if (rng2.Next(4) == 0) foundings.Add(nextId++);
            RegionMath.TrimOldest(foundings, RegionMath.MaxFoundings);
            if (foundings.Count > 0 && rng2.Next(3) == 0)
            {
                int t = foundings[0];
                foundings.RemoveAt(0);
                towns.Add(t);
                houses[t] = Math.Min(RegionMath.HousesPerTown, (houses.TryGetValue(t, out int h) ? h : 0) + 2);
            }
            var distances = towns.Select(t => (float)(t % 50)).ToList();
            var keep = RegionMath.InReach(distances, null, 35f).Select(i => towns.ElementAt(i)).ToHashSet();
            towns.RemoveWhere(t => !keep.Contains(t));
            foreach (var k in houses.Keys.ToList())
                if (!towns.Contains(k))
                    houses.Remove(k);
            most = Math.Max(most, towns.Count);
            mostHouses = Math.Max(mostHouses, houses.Values.Sum());
        }
        C(most <= RegionMath.MaxTownsPerHome && mostHouses <= RegionMath.MaxTownsPerHome * RegionMath.HousesPerTown && foundings.Count <= RegionMath.MaxFoundings,
            $"five years of foundings stay within the bounds ({most} towns, {mostHouses} houses at most)");

        // ---------------- Words ----------------

        C(Math.Abs(RegionMath.WalkDays(6f) - 1.98f) < 1e-4 && RegionMath.WalkDays(-3f) == 0f, "a third of a day's walk a tile");
        C(RegionMath.DaysLevel(0.5f) == 0 && RegionMath.DaysLevel(1f) == 1 && RegionMath.DaysLevel(2f) == 2 && RegionMath.DaysLevel(4f) == 3, "less than a day, a day, two days, more");

        // ---------------- The passes' slots ----------------

        C(Sched.HourlyOnCycle.Contains(Sched.Region) && !Sched.HourlyOffsets.Contains(Sched.Region), "the region's hour has a tick of the hour's first stretch of its own");
        C(Sched.Region >= 0 && Sched.Region < Sched.RareTicks, "inside the hour's first 250 ticks");
        var rareAll = Sched.RareOffsets.Concat(new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers, Sched.RareVisitors, Sched.RareTimed,
            Sched.RareRearrange, Sched.RareNeighbours, Sched.RareOutsiders, Sched.RareJourneys, Sched.RareOutsiderTalk }).ToList();
        C(!rareAll.Contains(Sched.Region), "on no 250-tick pass's tick");
        C(Sched.HourlyOffsets.All(o => o % Sched.RareTicks != Sched.Region), "on no hourly pass's tick of the cycle");
        int dr = Sched.DailyRegion;
        C(dr > 0 && dr < Sched.HourTicks && !Sched.HourlyOffsets.Contains(dr) && !rareAll.Contains(dr % Sched.RareTicks) && dr % Sched.PeopleLogPeriod != Sched.PeopleLog
          && Sched.HourlyOffsets.All(o => o % Sched.RareTicks != dr % Sched.RareTicks),
            "the region's day: in the day's first hour, on no hourly or 250-tick pass's tick, nor the people log's");
        C(new[] { Sched.DailyWealth, Sched.DailyMagicNeeds, Sched.DailyBestFriends, Sched.DailyHistory }.All(o => o != dr && o % Sched.RareTicks != dr % Sched.RareTicks),
            "nor another daily pass's");
        C(PassTable.All.Any(x => x.pass == Pass.Region && x.period == Sched.HourTicks && x.offset == Sched.Region)
          && PassTable.All.Any(x => x.pass == Pass.DailyRegion && x.period == Sched.DayTicks && x.offset == dr), "the clock has both passes");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string book = Code("World/Region/RegionBook.cs");
        C(book.Contains("Sched.Hourly(now, Sched.Region)") && book.Contains("PassClock.Time(Pass.Region)"), "the region's hour runs on its slot, timed");
        C(book.Contains("Sched.Every(now, Sched.DayTicks, Sched.DailyRegion)") && book.Contains("PassClock.Time(Pass.DailyRegion)"), "the region's day runs on its slot, timed");
        var tick = Regex.Match(book, @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(tick.Length > 0 && !tick.Contains("foreach") && !tick.Contains("Find.WorldObjects"), "nothing per tick at world level: the tick only asks the clock");
        C(book.Contains("class RegionHome : IExposable, IColonyEntry") && book.Contains("ColonyBook.Entry(homes, map, () => new RegionHome())")
          && Code("ColonyState.cs").Contains("RegionBook.Instance?.HomesLost(lost)"), "each home's part is kept by the colonies' book (one home plays as before)");
        string towns2 = Code("World/Region/RegionTowns.cs");
        C(!Regex.IsMatch(towns2, @"TraversalDistance|WorldPath|CaravanArrivalTimeEstimator"), "the reach is worked out with straight-line distances only");
        C(towns2.Contains("def.humanlikeFaction") && towns2.Contains("RegionMath.InReach(distances, fit, reachTiles)"), "humanlike peoples only, by the rule");
        string ledger = Code("World/Ledger/WorldLedger.cs") + Code("World/Ledger/LedgerRegion.cs") + Code("World/Ledger/LedgerPersons.cs");
        C(ledger.Contains("ExposeRegion();") && ledger.Contains("Scribe_Collections.Look(ref houses, \"houses\"") && ledger.Contains("Scribe_Collections.Look(ref foundings, \"foundings\""),
            "the ledger saves its houses and foundings");
        C(ledger.Contains("RegionMath.KeepTown(") && ledger.Contains("RegionTowns.InDetail()"), "town records are kept by the reach");
        C(Code("World/Ledger/LedgerKeep.cs").Contains("case KeepFor.Founding: return FounderNamed(Refs.Of(p));"), "a founder stays kept while a founding or a town names them");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var k in new[] { "World/Ledger/LedgerRegion.cs:houses", "World/Ledger/LedgerRegion.cs:foundings", "World/Ledger/LedgerRegion.cs:members", "World/Region/RegionBook.cs:homes" })
            C(est.Contains("\"" + k + "\""), "the save model has " + k);
        string news = Code("Integrations/TerritoryNews.cs");
        C(news.Contains("heldClaims = RegionFaces.Claims") && news.Contains("bool taken = RegionFaces.Claims != heldClaims") && news.Contains("Messages.Message(text, look"),
            "Faction Territories' near message is held, dropped when the region took the town, else given back: one message, never none");
        string faces = Code("World/Region/RegionFaces.cs") + Code("World/Region/RegionWords.cs") + Code("World/Region/RegionFortunes.cs");
        C(!Regex.IsMatch(faces, @"Messages\.Message|LetterStack|News\.Post|Notify\."), "the region tells the player through Tell only");
        C(faces.Contains("RegionMath.FoundingDay(built, RegionBook.FacesOn, stands, inReach,")
          && Code("World/Region/RegionBook.cs").Contains("if (FacesOn) RegionFaces.Hourly(this, now);") && Code("World/Region/RegionBook.cs").Contains("RegionFaces.Daily(now)"),
            "the day looks over every founding by the rule, the faces' hour only while they are on");
        C(faces.Contains("Tell.Minor(NewsKind.World, Importance.Notable, home, text") && faces.Contains("Tell.Letter(NewsKind.World, Importance.Notable, home, label"),
            "a line when begun and when built, and the founder's letter");
        C(faces.Contains("Ages.Form(AgeFeature.Founding, home, people)") && faces.Contains("Ages.Form(AgeFeature.Founding, home, town.Faction)"), "the news by the colony's means and their rite (Ages)");

        // The full welcome: the new-town event's beats on the town that stands, never by itself, once a year, when a slot is free.
        string welcome = Code("World/Region/RegionWelcome.cs");
        C(welcome.Contains("class SituationWorker_NewTownMet : SituationWorker_NewTown") && welcome.Contains("public override bool CanStart(Map map) => pending != null && base.CanStart(map);"),
            "the welcome runs only when the region starts it with a town");
        C(welcome.Contains("!ws.SlotFree(home)") && Code("World/WorldSituations.cs").Contains("active.Count(s => !s.ended) < MaxActive && !MapFacts.ThreatOn(map) && !Waiting && Tell.CanAsk"),
            "only when a world event's slot is free, as the world's own rhythm would");
        string facesCode = Code("World/Region/RegionFaces.cs");
        C(facesCode.Contains("RegionMath.WelcomeDue(tiles, mine.welcomeAt, now)") && facesCode.Contains("!FullWelcome(book, town, founder, home, tiles)"),
            "once a year per home, and its letter is the town's news (no second line)");
        var met = Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WorldSituationDef"))
            .FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_WS_NewTownMet");
        C(met != null && met.Element("workerClass")?.Value == "ColonyCouncil.SituationWorker_NewTownMet", "the welcome's def");
        var beats = met?.Element("beats")?.Elements("li").Select(b => b.Element("key")?.Value).ToList() ?? new List<string>();
        C(beats.SequenceEqual(new[] { "welcome", "traders", "visitors" }), "the new-town event's beats: the runner and the gift, its traders, its visitors");
        string metText = met?.ToString() ?? "";
        C(metText.Contains("VisitEffect_AtGate") && metText.Contains("<what>runner</what>") && metText.Contains("<kind>gift</kind>"), "a runner at the gate, and a gift hoped for and remembered");

        // A town's fortune in its stock: a postfix that runs last, guarded by the settings.
        string stock = Code("World/Region/RegionStock.cs");
        C(stock.Contains("[HarmonyPatch(typeof(Settlement_TraderTracker), \"RegenerateStock\")]") && stock.Contains("[HarmonyPriority(Priority.Last)]")
          && stock.Contains("public static void Postfix(") && !stock.Contains("Prefix("), "the stock patch is a postfix that runs after other mods' and never skips the game's restock");
        C(stock.Contains("RimfolkPatches.On && RegionBook.FortunesOn && (ColonyCouncilMod.Settings?.regionStock ?? true)") && stock.Contains("CouncilLog.ErrorOnce"),
            "off with the deeper hooks, the fortunes or its own setting off, and never throws into the game");
        C(stock.Contains("RegionTowns.IsInDetail(town.ID)"), "only towns kept in detail");

        // ---------------- World events move fortunes ----------------

        string def = Code("World/WorldSituationDef.cs");
        C(def.Contains("public float prosperity;") && def.Contains("public float prosperityOther;"), "world events have a prosperity field, and one for the other people");
        var moves = new Dictionary<string, (float p, float o)>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements("ColonyCouncil.WorldSituationDef"))
            {
                float pv = float.Parse(e.Element("prosperity")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float ov = float.Parse(e.Element("prosperityOther")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                if (pv != 0f || ov != 0f) moves[e.Element("defName")!.Value] = (pv, ov);
                C(Math.Abs(pv) <= 50f && Math.Abs(ov) <= 50f, $"{e.Element("defName")!.Value} moves a town by 50 at most");
                if (pv != 0f || ov != 0f) C(e.Element("faction") != null, $"{e.Element("defName")!.Value} has a people to move");
            }
        C(moves.Count >= 30, $"about 30 world events move fortunes ({moves.Count})");
        var design = new Dictionary<string, float>
        {
            ["Council_WS_Plague"] = -20, ["Council_WS_Famine"] = -25, ["Council_WS_HardWinter"] = -10, ["Council_WS_Locusts"] = -15, ["Council_WS_Flood"] = -15,
            ["Council_WS_GreatFire"] = -30, ["Council_WS_HolyWar"] = -15, ["Council_WS_Bumper"] = 10, ["Council_WS_TradeBoom"] = 10, ["Council_WS_NewRoad"] = 10,
            ["Council_WS_Fair"] = 8, ["Council_WS_SilverRush"] = 12
        };
        foreach (var kv in design)
            C(moves.TryGetValue(kv.Key, out var m) && m.p == kv.Value, $"{kv.Key} moves its town {kv.Value}, as the design has it");
        C(moves.TryGetValue("Council_WS_HolyWar", out var hw) && hw.o < 0f, "a war of creeds hurts the other people's towns too");
        C(Code("World/Region/RegionFortunes.cs").Contains("s.SetNum(AppliedKey, applied + due)"), "each event keeps how many days' shares it gave");

        return n;
    }
}
