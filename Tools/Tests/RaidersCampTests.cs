using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Raiders' camps (World/Region/RaidersCampMath.cs; docs/design/stage1-region.md, item 4; decision 7): when one is set
/// up (the colony's wealth, the rest after the last, who qualifies by their towns, territory and grudges, the rate),
/// where it stands, how soon it is found, the raid schedule from the game's raid-source curve, the give-up rule, what is
/// left after an attack, their price and their word, the ally's burning, and the endings' history kinds and memories.
/// Also the mod's side: the passes call it, the words exist for every age and ending, spoken lines are one sentence,
/// the deeds are known, the page is registered and the setting is on at 100%.
/// </summary>
public static class RaidersCampTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "raiders' camps: " + what);
        }

        bool Near(float a, float b, float eps = 1e-3f) => Math.Abs(a - b) < eps;
        const int D = RaidersCampMath.DayTicks;

        // ---------------- When one is set up ----------------

        C(RaidersCampMath.HomeFits(1000f, 60 * D, false, -1), "a home of 1,000 threat points on day 60 with no camp: may get one");
        C(!RaidersCampMath.HomeFits(999f, 60 * D, false, -1), "a poorer colony: none (about a mid-game colony first)");
        C(!RaidersCampMath.HomeFits(5000f, 59 * D, false, -1), "never before day 60");
        C(!RaidersCampMath.HomeFits(5000f, 100 * D, true, -1), "one camp per home");
        C(!RaidersCampMath.HomeFits(5000f, 100 * D, false, 41 * D) && RaidersCampMath.HomeFits(5000f, 100 * D, false, 40 * D),
            "none within 60 days after the last ended (the cooldown)");
        C(RaidersCampMath.LookDue(-1, 0) && !RaidersCampMath.LookDue(5 * D, 14 * D) && RaidersCampMath.LookDue(5 * D, 15 * D), "each home looked at every 10 days");
        C(Near(RaidersCampMath.Chance(1f), 0.1f) && Near(RaidersCampMath.Chance(2f), 0.2f) && RaidersCampMath.Chance(0f) == 0f,
            "10% a look at 100%, 20% at 200%, none at 0%");
        C(Near(RaidersCampMath.Chance(float.NaN), 0.1f) && RaidersCampMath.Chance(-1f) == 0f && Near(RaidersCampMath.Chance(9f), 0.2f),
            "a broken setting reads as the default, a negative one as off, at most 200%");
        // About 100 days to a camp once a people qualifies: 10 looks of 10 days at 10%.
        float none = 1f;
        for (int i = 0; i < 10; i++) none *= 1f - RaidersCampMath.Chance(1f);
        C(none > 0.3f && none < 0.4f, $"a camp in about 100 days of qualifying (still none after 100 days: {none:P0})");

        C(RaidersCampMath.PeopleFits(true, 40f, -1f, 0f, false, false), "a hostile people with a town within 60 tiles");
        C(!RaidersCampMath.PeopleFits(true, 61f, -1f, 0f, false, false), "their nearest town 61 tiles off and no grudge: not near enough");
        C(RaidersCampMath.PeopleFits(true, -1f, 30f, 0f, false, false) && !RaidersCampMath.PeopleFits(true, -1f, 31f, 0f, false, false),
            "or their territory within 30 tiles (Faction Territories)");
        C(RaidersCampMath.PeopleFits(true, 200f, -1f, 20f, false, false), "or a grudge, wherever their towns are");
        C(!RaidersCampMath.PeopleFits(false, 10f, 5f, 99f, false, false), "never a people that can't raid the colony");
        C(!RaidersCampMath.PeopleFits(true, 10f, -1f, 99f, true, false), "never while a world event about them runs");
        C(!RaidersCampMath.PeopleFits(true, 10f, -1f, 99f, false, true), "never while their word to keep away holds");
        C(Near(RaidersCampMath.Grudge(0f, 1, false), 15f) && Near(RaidersCampMath.Grudge(5f, 0, true), 25f) && Near(RaidersCampMath.Grudge(-3f, -1, false), 0f),
            "a grudge: their memory's wrongs, 15 for each of their dead held against the colony, 20 for a vendetta");
        C(RaidersCampMath.Pick(new List<float> { 10f, 30f, 30f }, new List<float> { 5f, 40f, 20f }) == 2, "the biggest grudge first, then the nearest");
        C(RaidersCampMath.Pick(new List<float> { 0f, 0f }, new List<float> { 50f, -1f }) == 0, "with no grudge, the nearest (none near counts last)");
        C(RaidersCampMath.Pick(new List<float>(), new List<float>()) == -1 && RaidersCampMath.Pick(null, null) == -1, "nobody qualifies: none");

        // ---------------- Where ----------------

        C(!RaidersCampMath.PlaceFits(5f, 5f, -1f, true), "never within 5 tiles of the home");
        C(RaidersCampMath.PlaceFits(6f, 6f, -1f, true) && RaidersCampMath.PlaceFits(10f, 10f, 20f, true), "6 to 10 tiles off");
        C(!RaidersCampMath.PlaceFits(10.5f, 10.5f, -1f, true), "no farther than 10 tiles (a few days' walk)");
        C(!RaidersCampMath.PlaceFits(8f, 4f, -1f, true), "never within 5 tiles of any other home of the colony either");
        C(!RaidersCampMath.PlaceFits(8f, 8f, 2.5f, true) && RaidersCampMath.PlaceFits(8f, 8f, 3f, true), "3 tiles or more from a friendly town");
        C(!RaidersCampMath.PlaceFits(8f, 8f, -1f, false), "only on land that can hold it (a base can stand there, nothing else on it)");
        C(RaidersCampMath.PlaceWeight(8f, true, false) > RaidersCampMath.PlaceWeight(8f, false, true)
          && RaidersCampMath.PlaceWeight(8f, false, true) > RaidersCampMath.PlaceWeight(8f, false, false), "hills first, then forest");
        C(RaidersCampMath.PlaceWeight(6f, false, false) > RaidersCampMath.PlaceWeight(10f, false, false), "a nearer tile a little more");

        // ---------------- Found ----------------

        float hour = RaidersCampMath.FindChance(0.5f);
        float day = 1f - (float)Math.Pow(1f - hour, RaidersCampMath.LightTo - RaidersCampMath.LightFrom);
        C(Near(day, 0.3f), $"30% a day that someone out beyond the walls sees it ({day:P1})");
        C(RaidersCampMath.FindChance(3f) == 1f, "from the third day anyone out of doors sees its smoke");
        C(RaidersCampMath.Daylight(6) && RaidersCampMath.Daylight(17) && !RaidersCampMath.Daylight(18) && !RaidersCampMath.Daylight(3), "seen by day only");

        // ---------------- The raid schedule, from the curve ----------------

        C(Near(RaidersCampMath.MtbDays(400f), 25f) && Near(RaidersCampMath.MtbDays(1500f), 10f) && Near(RaidersCampMath.MtbDays(5000f), 5f),
            "the game's raid-source curve: 25 days at 400 points, 10 at 1,500, 5 at 5,000");
        C(Near(RaidersCampMath.MtbDays(950f), 17.5f) && Near(RaidersCampMath.MtbDays(3250f), 7.5f), "straight between its points");
        C(Near(RaidersCampMath.MtbDays(100f), 25f) && Near(RaidersCampMath.MtbDays(9000f), 5f) && Near(RaidersCampMath.MtbDays(float.NaN), 25f), "flat beyond its ends");
        // The curve's points are the game's own (QuestTuning.cs in the 1.6 code).
        C(RaidersCampMath.CurvePoints.SequenceEqual(new[] { 400f, 1500f, 5000f }) && RaidersCampMath.CurveDays.SequenceEqual(new[] { 25f, 10f, 5f }), "the curve as the game has it");
        C(Near(RaidersCampMath.RaidMtb(10f, 1f), 10f) && Near(RaidersCampMath.RaidMtb(10f, 2f), 5f) && float.IsPositiveInfinity(RaidersCampMath.RaidMtb(10f, 0f)),
            "at 100% the game's rate, twice as often at 200%, never at 0%");
        C(Near(RaidersCampMath.HourChance(10f), 1f / 240f, 1e-6f) && RaidersCampMath.HourChance(float.PositiveInfinity) == 0f, "an hour's roll: an hour over the mean");
        // Two months standing at 1,500 points, many times over: about five raids (a mean of 10 days, a day and a half apart at least).
        var rnd = new Random(7);
        int raids = 0, last = -1, tooSoon = 0;
        const int runs = 400;
        for (int run = 0; run < runs; run++)
        {
            last = -1;
            for (int t = 2 * D; t < 62 * D; t += RaidersCampMath.HourTicks)
            {
                if (!RaidersCampMath.MayRaid(CampStage.Found, 0, 1 * D, last, t)) continue;
                if (rnd.NextDouble() >= RaidersCampMath.HourChance(RaidersCampMath.RaidMtb(RaidersCampMath.MtbDays(1500f), 1f))) continue;
                if (last >= 0 && t - last < 1.5f * D) tooSoon++;
                raids++;
                last = t;
            }
        }
        float mean = raids / (float)runs;
        C(mean > 4.5f && mean < 6f && tooSoon == 0, $"two months standing at 1,500 points: about five raids on average ({mean:0.00}), none within a day and a half of the last");
        C(!RaidersCampMath.MayRaid(CampStage.Hidden, 0, -1, -1, 30 * D), "no raid while it is hidden: the letter that names the danger comes first");
        C(!RaidersCampMath.MayRaid(CampStage.Found, 0, 1 * D, -1, 1 * D + D / 2) && RaidersCampMath.MayRaid(CampStage.Found, 0, 1 * D, -1, 2 * D),
            "the first raid two days after it is set up and a day after it was found");
        C(!RaidersCampMath.MayRaid(CampStage.Paying, 0, 1 * D, -1, 20 * D), "none while their price is on its way to them");
        C(RaidersCampMath.Beaten(8, 4, false) && !RaidersCampMath.Beaten(8, 3, false) && RaidersCampMath.Beaten(8, 0, true) && !RaidersCampMath.Beaten(0, 0, false),
            "a raid beaten off: half its fighters down or taken, or they fled");

        // ---------------- The side a raid comes in from ----------------

        // The game's own Rot4 from a heading (PlanetLayer.GetRotFromTo): north from 315 to 45, east to 135, south to 225.
        int GameRot(float h) => h >= 315f || h < 45f ? 0 : h < 135f ? 1 : h < 225f ? 2 : 3;
        bool sidesOk = true, ownFits = true;
        for (float h = 0f; h < 360f; h += 2.5f)
        {
            var s = RaidersCampMath.Sides(h);
            if (s.Length != 4 || s.Distinct().Count() != 4 || s[0] != GameRot(h) || s[3] != (s[0] + 2) % 4) sidesOk = false;
            if (!RaidersCampMath.SideFits(h, s[0])) ownFits = false;
        }
        C(sidesOk, "a raid tries the camp's own side first (the game's rule), the far side last, each side once");
        C(ownFits, "the camp's own side always fits the direction the words give it");
        C(RaidersCampMath.Sides(10f).SequenceEqual(new[] { 0, 1, 3, 2 }) && RaidersCampMath.Sides(350f).SequenceEqual(new[] { 0, 3, 1, 2 })
          && RaidersCampMath.Sides(100f).SequenceEqual(new[] { 1, 2, 0, 3 }), "after its own side, the side next to it nearer the camp's heading");
        C(RaidersCampMath.SideFits(30f, 0) && RaidersCampMath.SideFits(30f, 1) && !RaidersCampMath.SideFits(10f, 1) && !RaidersCampMath.SideFits(10f, 3)
          && !RaidersCampMath.SideFits(10f, 2), "a camp to the northeast fits the north and east sides; one due north, the north side only");
        C(RaidersCampMath.SideFits(340f, 0) && RaidersCampMath.SideFits(320f, 3) && RaidersCampMath.SideFits(-20f, 0) && RaidersCampMath.SideFits(float.NaN, 0),
            "headings round the circle, and a broken one, read safely");

        // ---------------- Its leader ----------------

        C(!RaidersCampMath.LeaderLost(false, true, true, false), "a camp with no leader loses none");
        C(!RaidersCampMath.LeaderLost(true, false, false, true), "a leader alive, free and of its people leads it");
        C(RaidersCampMath.LeaderLost(true, true, false, true) && RaidersCampMath.LeaderLost(true, false, true, true) && RaidersCampMath.LeaderLost(true, false, false, false),
            "dead, taken, or one of the colony now: its leader is lost");
        C(RaidersCampMath.BreaksUp(true, CampStage.Found, false) && RaidersCampMath.BreaksUp(true, CampStage.Hidden, false), "without its leader the band breaks up");
        C(!RaidersCampMath.BreaksUp(true, CampStage.Found, true) && !RaidersCampMath.BreaksUp(true, CampStage.Paying, false) && !RaidersCampMath.BreaksUp(false, CampStage.Found, false),
            "not while a raid of theirs is on the map, nor while their price is on its way, nor with its leader there");
        C(RaidersCampMath.GaveUpWhy(true, 2) == "_Leader" && RaidersCampMath.GaveUpWhy(false, 1) == "_Time" && RaidersCampMath.GaveUpWhy(false, 2) == "",
            "a camp given up says why: its leader lost, its raids beaten off, or the months gone");

        // ---------------- The give-up rule ----------------

        C(RaidersCampMath.GivesUp(2, 0, 10 * D) && !RaidersCampMath.GivesUp(1, 0, 59 * D), "two raids beaten off and they move on");
        C(RaidersCampMath.GivesUp(0, 5 * D, 65 * D) && !RaidersCampMath.GivesUp(0, 5 * D, 64 * D), "or two months after it was set up");

        // ---------------- After an attack ----------------

        C(RaidersCampMath.Broken(false, 10, 6), "no threat left on its map: broken");
        C(RaidersCampMath.Broken(true, 12, 2) && !RaidersCampMath.Broken(true, 12, 3), "fewer than a quarter of its fighters standing: broken all the same");
        C(RaidersCampMath.Broken(true, 10, 0), "nobody standing: broken");
        C(Near(RaidersCampMath.PointsAfter(1000f, 10, 6), 600f) && Near(RaidersCampMath.PointsAfter(1000f, 0, -1), 1000f), "put back with its points less those who fell");

        // ---------------- Their price and their word ----------------

        C(RaidersCampMath.Price(840f) == 420 && RaidersCampMath.Price(845f) == 420 && RaidersCampMath.Price(850f) == 430, "about half a raid's points in silver, in tens");
        C(RaidersCampMath.Price(50f) == 100 && RaidersCampMath.Price(float.NaN) == 100, "never under 100");
        C(!RaidersCampMath.TermsDue(CampStage.Found, 5 * D, false, 5 * D + D / 2) && RaidersCampMath.TermsDue(CampStage.Found, 5 * D, false, 6 * D),
            "their terms a day after it is found");
        C(!RaidersCampMath.TermsDue(CampStage.Found, 5 * D, true, 9 * D) && !RaidersCampMath.TermsDue(CampStage.Hidden, -1, false, 9 * D), "once, and only for a camp the colony knows of");
        C(RaidersCampMath.WordHolds(10 * D, false, 69 * D) && !RaidersCampMath.WordHolds(10 * D, false, 70 * D), "their word holds a year");
        C(!RaidersCampMath.WordHolds(10 * D, true, 11 * D) && !RaidersCampMath.WordHolds(-1, false, 0), "one who breaks it is held back only by the rest after a camp");
        C(RaidersCampMath.WordKept(0, 60 * D) && !RaidersCampMath.WordKept(0, 59 * D) && !RaidersCampMath.WordKept(-1, 99 * D), "a year on, their word was kept");
        C(Near(RaidersCampMath.BreakWord, 0.1f), "one in ten break it");

        // ---------------- An ally ----------------

        C(RaidersCampMath.AllyDue(-1, 0) && !RaidersCampMath.AllyDue(0, 4 * D) && RaidersCampMath.AllyDue(0, 5 * D), "an ally looks at it every 5 days");
        C(Near(RaidersCampMath.AllyChance, 0.1f) && Near(RaidersCampMath.AllyTiles, 30f), "10% a look, with a town of theirs within 30 tiles");

        // ---------------- Endings: the history and the memories ----------------

        var ends = Enum.GetValues(typeof(CampEnd)).Cast<CampEnd>().ToList();
        foreach (var e in ends.Where(e => e != CampEnd.Gone))
        {
            string kind = RaidersCampMath.KindOf(e);
            C(kind != null && kind.StartsWith("world:camp:"), $"{e} goes into the history as {kind}");
            C(RaidersCampMath.DeedOf(e, out bool ally) != null && ally == (e == CampEnd.Burned), $"{e} goes into what {(e == CampEnd.Burned ? "the ally" : "their people")} remember");
        }
        C(RaidersCampMath.KindOf(CampEnd.Gone) == null && RaidersCampMath.DeedOf(CampEnd.Gone, out _) == null, "a camp gone without a story writes nothing");
        C(ends.Select(RaidersCampMath.KindOf).Where(k => k != null).Distinct().Count() == 5, "five ways to end, each its own kind");
        foreach (var k in new[] { RaidersCampMath.KindFound, RaidersCampMath.KindRaid, RaidersCampMath.KindBack })
            C(HistoryKinds.Matches(k, "world:camp") && HistoryKinds.Matches(k, "world"), $"{k} is of the families world:camp and world");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string keyed = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        string mine = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_RaidersCamps.xml"));
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
        {
            C(keyed.Contains("<" + RaidersCampMath.KindKey(age) + ">"), $"the camp's kind in the {age} age");
            C(keyed.Contains("<" + RaidersCampMath.AllyKey(age) + ">"), $"an ally's riders in the {age} age");
        }
        var kinds = Enum.GetValues(typeof(WorldAge)).Cast<WorldAge>().Select(a => Regex.Match(mine, "<" + RaidersCampMath.KindKey(a) + ">([^<]*)<").Groups[1].Value).ToList();
        C(kinds.SequenceEqual(new[] { "war camp", "brigands' hold", "bandit camp", "raider base" }), "a war camp, a brigands' hold, a bandit camp, a raider base");
        foreach (var e in ends.Where(e => e != CampEnd.Gone))
            C(keyed.Contains("<CC_RaidCamp_End_" + e + ">") && keyed.Contains("<CC_RaidCamp_End_" + e + "_Label>"), $"the words of an ending: {e}");
        C(keyed.Contains("<CC_RaidCamp_End_GaveUp_Time>"), "giving up after two months has words of its own");
        C(keyed.Contains("<CC_RaidCamp_End_GaveUp_Leader>") && Regex.Match(mine, "<CC_RaidCamp_End_GaveUp_Leader>([^<]*)<").Groups[1].Value.Contains("{LEADER}"),
            "breaking up without its leader has words of its own, naming him");
        C(Regex.Match(mine, "<CC_RaidCamp_RaidRound>([^<]*)<").Groups[1].Value.Contains("{SIDE}"), "a raid that came round says from which side");
        string memory = File.ReadAllText(Path.Combine(src, "World", "FactionMemory.cs"));
        var deeds = ends.Select(e => RaidersCampMath.DeedOf(e, out _)).Where(d => d != null).Append(RaidersCampMath.DeedCamped).ToList();
        foreach (var d in deeds)
            C(memory.Contains("[\"" + d + "\"]") && keyed.Contains("<CC_Memory_Deed_" + d + ">"), $"the peoples' memory knows the deed {d}, with its words");
        // Said over a head: one sentence of 90 characters at most.
        string say = Regex.Match(mine, "<CC_RaidCamp_Say>([^<]*)<").Groups[1].Value.Replace("{DIR}", "northeast");
        C(say.Length > 0 && say.Length <= 90 && say.TrimEnd('.').IndexOfAny(new[] { '.', '!', '?' }) < 0, $"the line said on seeing it is one short sentence ('{say}')");
        C(!mine.Contains("—") && !Regex.IsMatch(mine, ">[^<]*![^<]*<"), "no em dashes and no exclamation marks in its words");
        // Every key the code names exists.
        string code = string.Concat(Directory.GetFiles(Path.Combine(src, "World", "Region"), "RaidersCamp*.cs").Select(File.ReadAllText));
        var named = Regex.Matches(code, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(named.Count >= 40, $"the keys the code names ({named.Count})");
        foreach (var k in named) C(keyed.Contains("<" + k + ">"), $"{k} exists");

        string book = File.ReadAllText(Path.Combine(src, "World", "Region", "RegionBook.cs"));
        C(book.Contains("RaidersCamps.Hourly(this, now)") && book.Contains("RaidersCamps.Daily(this, now)"), "the region's two passes run the camps (no pass of their own)");
        C(book.Contains("Scribe_Deep.Look(ref raiders, \"raiders\")"), "each home's camp is saved with its part of the region");
        C(book.Contains("ColonyBook.Lost(homes, lost, RaidersCamps.HomeLost)"), "a home lost takes its camp with it");
        C(!code.Contains("GameComponentTick") && !code.Contains("override void Tick"), "nothing per tick");
        string lens = File.ReadAllText(Path.Combine(src, "UI", "Region", "LensModel.cs"));
        C(!code.Contains("MainTabWindow_World.Register(") && lens.Contains("RaidersCamps.Known()") && lens.Contains("RaidersCampPage.AskToPay(h, camp)"),
            "the camps are the region map's Threats lens, with their list and Pay (their old page's name forwards there)");
        C(code.Contains("SiteMaker.MakeSite(SitePartDefOf.BanditCamp"), "the camp is the game's own bandit camp site");
        C(code.Contains("QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve"), "its raids read the game's raid-source curve");
        C(code.Contains("LetterDefOf.ThreatSmall") && code.Contains("\"CC_RaidCamp_Found_Label\""), "it is found with a threat letter");
        C(code.Contains("if (On) TryFind(home, rh, c, now);") && code.Contains("else if (On) TryWarn(home, c, now);"),
            "switched off, a hidden camp is never found (its letter would promise raids that never come)");
        C(code.Contains("RaidersCampMath.Sides(heading)") && code.Contains("!RaidersCampMath.SideFits(heading, side)") && code.Contains("\"CC_RaidCamp_RaidRound\""),
            "a raid walks in from the nearest side that lets it in, and its letter names the side when it isn't the camp's");
        C(code.Contains("RaidersCampMath.BreaksUp(leaderLost, c.stage, c.raidOpen)") && code.Contains("else if (leaderLost) End(home, rh, c, CampEnd.GaveUp, null);"),
            "a camp whose leader is lost breaks up (and isn't put back after an attack)");
        C(code.Contains("c.leader != null && !LeaderLost(c) && !c.leader.Spawned") && code.Contains("!RaidersCamps.LeaderLost(c)"),
            "a lost leader leads no raid and isn't named on the page");
        string helpers = File.ReadAllText(Path.Combine(src, "Happenings", "HappeningHelpers.cs"));
        C(helpers.Contains("if (CanLead(leader, f) && leader.kindDef != null) points -= leader.kindDef.combatPower;") && helpers.Contains("if (!CanLead(leader, f)) return;"),
            "a raid's leader counts against its strength only when he can walk in with it");
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        C(settings.Contains("public float raidersCamps = 1f;") && settings.Contains("Scribe_Values.Look(ref raidersCamps, \"raidersCamps\", 1f);"), "the setting is on by default, at 100%");
        C(File.ReadAllText(Path.Combine(src, "UI", "SettingsCatalog.cs")).Contains("Slider(\"raidersCamps\""), "the setting is in the settings window");
        C(keyed.Contains("<CC_Settings_RaidersCamps_Tip>"), "the setting says what it does");
        return n;
    }
}
