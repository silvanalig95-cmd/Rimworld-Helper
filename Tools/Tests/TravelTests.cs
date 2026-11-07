using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Travelers who seek out the remote and the invitations' titles and rites (World/Region/TravelMath.cs,
/// Missions/InviteMath.cs; docs/design/stage1-region.md, items 5 and 7): the rates by remoteness (the camps' daily chance
/// and their rest, the seekers' weight, Rimfolk's visitors with the wait between events cut so the rest keep their rate),
/// the reasons the land gives (which a kind picks, the land's thresholds), the far towns and the bounds of their news, a
/// quarrel's rate, and which ritual fits an occasion. Also the mod's side: the clocks read the rates per home, the camp
/// kinds' reasons are ones the code knows, the scholars' camp in every age, and the setting on by default.
/// </summary>
public static class TravelTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "travelers: " + what);
        }

        bool Near(float a, float b, float eps = 1e-4f) => Math.Abs(a - b) < eps;

        // ---------------- The rates, by remoteness ----------------

        C(Near(TravelMath.CampChance(0.05f, 0f, 1f), 0.05f), "a home close to its neighbors: the camps' chance as it was (0.05)");
        C(Near(TravelMath.CampChance(0.05f, 1f, 1f), 0.08f), "the most remote home: 60% more (0.08, a camp in about 12 days)");
        C(Near(TravelMath.CampChance(0.05f, 0.5f, 1f), 0.065f), "half remote: 30% more");
        C(Near(TravelMath.CampChance(0.05f, 1f, 0f), 0.05f), "the setting at 0: as before");
        C(Near(TravelMath.CampChance(0.05f, 1f, 2f), 0.11f) && Near(TravelMath.CampChance(0.05f, 1f, 9f), 0.11f), "the setting at 200% (and no further): 120% more");
        C(Near(TravelMath.CampChance(0.05f, 3f, 1f), 0.08f) && Near(TravelMath.CampChance(0.05f, -1f, 1f), 0.05f), "remoteness is read within 0 and 1");
        C(Near(TravelMath.GapFactor(0f, 1f), 1f) && Near(TravelMath.GapFactor(1f, 1f), 0.7f), "the rest after a camp: 15 days near, 30% less (10.5) at the most remote");
        C(Near(TravelMath.GapFactor(1f, 2f), 0.4f) && TravelMath.GapFactor(1f, 2f) >= TravelMath.MinGap, "never under 40% of the rest");
        C(Near(TravelMath.SeekWeight(1f, 1f, true), 3f) && Near(TravelMath.SeekWeight(0f, 1f, true), 1f), "a seeker with its reason on the land: 1 + 2 x remoteness");
        C(Near(TravelMath.SeekWeight(1f, 1f, false), 1f), "a kind with no reason on this map keeps its base weight");
        C(Near(TravelMath.VisitorFactor(1f, 1f), 1.6f) && Near(TravelMath.VisitorFactor(0f, 1f), 1f), "Rimfolk's visitors weigh up to 60% more");
        C(Near(TravelMath.Rate(float.NaN), 1f) && TravelMath.Rate(-1f) == 0f, "a broken setting reads as the default, a negative one as off");
        // The visitors 60% more often, the other events at the rate they had: the visitors' weight times 1.6 and the wait
        // cut by 1 + 0.6 x their share.
        foreach (float share in new[] { 0.2f, 0.45f, 0.8f })
        {
            float factor = TravelMath.VisitorFactor(1f, 1f);
            float wait = TravelMath.IntervalFactor(1f, 1f, share);
            float boosted = TravelMath.BoostedShare(share, factor);
            float visitors = boosted / wait / share, others = (1f - boosted) / wait / (1f - share);
            C(Near(visitors, 1.6f, 1e-3f), $"with {share:P0} of events visitors, they come 60% more often ({visitors:0.000})");
            C(Near(others, 1f, 1e-3f), $"with {share:P0} of events visitors, the rest come as often as before ({others:0.000})");
        }
        C(Near(TravelMath.IntervalFactor(0f, 1f, 0.5f), 1f) && Near(TravelMath.IntervalFactor(1f, 1f, 0f), 1f), "a home near its neighbors, or no visitors that could come: the wait as it was");
        C(Near(TravelMath.VisitorShare(45f, 100f), 0.45f) && TravelMath.VisitorShare(0f, 0f) == 0f && TravelMath.VisitorShare(5f, 2f) == 1f, "the visitors' share by weight");

        // ---------------- Reasons the land gives ----------------

        var holds = new HashSet<string> { "ruins", "library" };
        var scholars = new List<string> { "ruins", "ruinRegion", "library", "history", "wonder" };
        C(TravelMath.Pick(scholars, holds, 0.1f) == "ruins", "the strongest reason the land has comes first (most of the time)");
        C(TravelMath.Pick(scholars, holds, 0.95f) == "library", "now and then another the land has");
        C(TravelMath.Pick(scholars, new HashSet<string> { "ore" }, 0.1f) == null, "nothing they seek on the land: no reason, none told");
        C(TravelMath.Pick(scholars, new HashSet<string> { "history" }, 0.99f) == "history", "one reason: always that one");
        C(TravelMath.Pick(null, holds, 0.1f) == null && TravelMath.Pick(scholars, null, 0.1f) == null, "a kind that seeks nothing, or no land read: none");
        C(TravelMath.AnyHolds(scholars, holds) && !TravelMath.AnyHolds(scholars, new HashSet<string> { "grazing" }), "whether the land has anything a kind seeks");
        C(TravelMath.Reasons.Length == 12 && TravelMath.Reasons.Distinct().Count() == 12 && TravelMath.Reasons.All(TravelMath.Known), "twelve reasons, each once");
        C(TravelMath.OnTheMap.All(TravelMath.Known), "the reasons on the map are reasons");
        C(TravelMath.LibraryHolds(6) && !TravelMath.LibraryHolds(5), "a library from six books");
        C(TravelMath.HistoryHolds(60, 5) && !TravelMath.HistoryHolds(59, 9) && !TravelMath.HistoryHolds(200, 4), "a history: a year old, five milestones");
        C(TravelMath.OreHolds(6, 0) && TravelMath.OreHolds(0, 40) && !TravelMath.OreHolds(5, 39), "ore: six cells of a rich ore in sight, or forty of steel");
        C(TravelMath.GrazingHolds(0.3f, true) && !TravelMath.GrazingHolds(0.9f, false) && !TravelMath.GrazingHolds(0.1f, true), "grazing: grass enough, in its season");
        C(TravelMath.FarHolds(0.5f) && !TravelMath.FarHolds(0.3f) && TravelMath.FarHolds(1f), "far from every market: rather remote or more");

        // ---------------- Far towns, and the bounds of their news ----------------

        var d = new List<float> { 10f, 36f, 50f, 106f, 40f, 104f, 200f, 70f };
        var fit = new List<bool> { true, true, true, true, false, true, true, true };
        var far = TravelMath.FarTownsOf(d, fit, 35f, 105f);
        C(far.SequenceEqual(new[] { 1, 2, 7, 5 }), "far towns: beyond the reach and within three times it, nearest first: " + string.Join(",", far));
        C(!far.Contains(0) && !far.Contains(3) && !far.Contains(6), "a town in the reach, or past three times it, is not a far town");
        C(!far.Contains(4), "a town of a people not kept is left out");
        C(TravelMath.FarTownsOf(Enumerable.Range(0, 100).Select(i => 36f + i * 0.5f).ToList(), null, 35f, 105f).Count == TravelMath.FarTowns, $"the nearest {TravelMath.FarTowns} far towns at most");
        C(TravelMath.FarTownsOf(d, fit, 35f, 30f).Count == 0 && TravelMath.FarTownsOf(null, null, 35f, 105f).Count == 0, "no towns, or no room beyond the reach: none");
        C(TravelMath.MayTell(0) && TravelMath.MayTell(1) && !TravelMath.MayTell(2), "two pieces of far news a stay at most");
        C(TravelMath.ToldLately(0, 9 * RegionMath.DayTicks) && !TravelMath.ToldLately(0, 10 * RegionMath.DayTicks) && !TravelMath.ToldLately(-1, 5), "the same news of a town again only after ten days");
        C(TravelMath.NewsRank("situation") < TravelMath.NewsRank("war") && TravelMath.NewsRank("war") < TravelMath.NewsRank("quest")
          && TravelMath.NewsRank("nonsense") == TravelMath.NewsKinds.Length, "a world event first, a quest last, anything else after");

        // ---------------- A quarrel over a claim ----------------

        float hour = TravelMath.QuarrelHourChance(7f);
        float stay = 1f - (float)Math.Pow(1f - hour, 7 * 8);
        C(Near(stay, TravelMath.QuarrelChance, 1e-3f), $"a week's stay comes to {TravelMath.QuarrelChance:P0} that prospectors come to blows ({stay:P1})");
        C(TravelMath.QuarrelChance <= 0.15f, "about the game's own odds of a social fight in a week among a few people, not more");

        // ---------------- Titles and rites in invitations ----------------

        C(InviteMath.RiteKind("Funeral Funeral") == "funeral" && InviteMath.RiteKind("Celebration_Feast DateRitualFeast") == "feast"
          && InviteMath.RiteKind("DanceParty") == "feast" && InviteMath.RiteKind("MarriageCeremony") == "wedding" && InviteMath.RiteKind("RoleChange") == null,
            "what a ritual is for, from its names");
        C(InviteMath.RiteKind("") == null && InviteMath.RiteKind(null) == null, "no names, no rite");
        var kinds = new List<string> { null, "funeral", "feast", "wedding" };
        C(InviteMath.Best("feast", kinds) == 2 && InviteMath.Best("midwinter", kinds) == 2, "a feast or midwinter: their feast");
        C(InviteMath.Best("wedding", kinds) == 3 && InviteMath.Best("wedding", new List<string> { "feast" }) == 0, "a wedding: its own rite, else their feast");
        C(InviteMath.Best("funeral", kinds) == 1 && InviteMath.Best("funeral", new List<string> { "feast" }) == -1, "a funeral: only a funeral rite");
        C(InviteMath.Best("apprentice", kinds) == -1 && InviteMath.Best("feast", null) == -1, "no occasion, no rite");
        C(InviteMath.Feature("feast") == AgeFeature.Feast && InviteMath.Feature("wedding") == AgeFeature.Marriage && InviteMath.Feature("funeral") == null,
            "without a rite of their faith's, the age's form of a feast or a marriage");
        C(InviteMath.Titled("baroness", "Ana Bell") == "Baroness Ana Bell" && InviteMath.Titled(null, "Ana Bell") == "Ana Bell" && InviteMath.Titled("knight", "") == "knight",
            "a royal title before the name");

        // ---------------- The mod's side ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string timer = File.ReadAllText(Path.Combine(src, "Camps", "CampTimer.cs"));
        string tracker = File.ReadAllText(Path.Combine(src, "Camps", "CampTracker.cs"));
        string happenings = File.ReadAllText(Path.Combine(src, "Happenings", "Happenings.cs"));
        C(timer.Contains("Rand.Chance(TravelLand.CampChance(map))"), "the camps' daily try reads its home's chance (per home, on the camps' own clock)");
        C(tracker.Contains("gap *= TravelLand.GapFactor(map)"), "the rest after a camp reads its home's remoteness");
        C(tracker.Contains("k.WeightNow(map) * k.HourFit(hourNow) * TravelLand.SeekWeight(k, map, land)"), "the kind is picked by the hour and by what the land has");
        C(happenings.Contains("TravelLand.VisitorFactor(map)") && happenings.Contains("TravelLand.IntervalFactor(map, lastVisitorShare)"),
            "Rimfolk's timer weighs a remote home's visitors and cuts its wait by their share");
        foreach (var f in new[] { "TravelLand.cs", "FarNews.cs" })
            C(!File.ReadAllText(Path.Combine(src, "World", "Region", f)).Contains("GameComponentTick"), $"{f} has no tick of its own (the existing clocks call it)");
        string settings = File.ReadAllText(Path.Combine(src, "CouncilSettings.cs"));
        C(settings.Contains("public float remoteTravellers = 1f;") && settings.Contains("Scribe_Values.Look(ref remoteTravellers, \"remoteTravellers\", 1f);"), "the setting is on by default, at 100%");
        // Every camp kind's reasons are ones the land can give, and every reason a kind asks about has the general line.
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Travellers.xml"));
        foreach (var r in TravelMath.Reasons)
        {
            C(keyed.Contains("<CC_Travel_What_" + r + ">"), $"the words for what draws them: {r}");
            C(keyed.Contains("<CC_Travel_Came_" + r + ">"), $"the general line for why they came: {r}");
        }
        var defs = Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").Select(XDocument.Load).ToList();
        var seekers = defs.SelectMany(x => x.Root!.Elements("ColonyCouncil.CampKindDef")).Where(k => k.Element("seeks") != null).ToList();
        C(seekers.Count >= 10, $"the kinds that seek something of the land ({seekers.Count})");
        foreach (var k in seekers)
        {
            string name = k.Element("defName")?.Value ?? "?";
            var seeks = k.Element("seeks")!.Elements("li").Select(e => e.Value).ToList();
            C(seeks.Count > 0 && seeks.All(TravelMath.Known), $"{name}: seeks only reasons the land can give");
            foreach (var t in k.Element("seekTexts")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                var m = Regex.Match(t.Value, @"^\[([^\]]*)\]");
                var tags = m.Success ? m.Groups[1].Value.Split(',') : new string[0];
                var seekTag = tags.FirstOrDefault(x => x.StartsWith("seek:"));
                C(seekTag != null && seeks.Contains(seekTag.Substring(5)), $"{name}: each of its own lines says which reason it is for ({t.Value.Substring(0, Math.Min(40, t.Value.Length))})");
            }
            foreach (var a in k.Element("asks")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string need = a.Element("needs")?.Value;
                if (need == null || !need.StartsWith("seek:")) continue;
                C(seeks.Contains(need.Substring(5)), $"{name}: its ask for {need} is for a reason it seeks");
                C(a.Element("theyAsk")?.Value == "true" && a.Element("askTexts")?.Elements("li").Any() == true, $"{name}: the ask for {need} is theirs, by letter");
            }
        }
        var nomads = new[] { "Council_Camp_Drovers", "Council_Camp_SaltCaravan", "Council_Camp_FarMerchants" };
        foreach (var nomad in nomads)
            C(seekers.Any(k => k.Element("defName")?.Value == nomad), $"the nomads: {nomad} is likelier for the remote");
        // The scholars: a kind of their own, in every age, only when the land has what they study.
        var sch = defs.SelectMany(x => x.Root!.Elements("ColonyCouncil.CampKindDef")).FirstOrDefault(k => k.Element("defName")?.Value == "Rimfolk_Camp_Scholars");
        C(sch != null, "the scholars' camp exists");
        if (sch != null)
        {
            C(sch.Element("seekOnly")?.Value == "true", "the scholars come only for something of the land's");
            C(sch.Element("eras") == null && sch.Element("minTech")?.Value == "Neolithic", "the scholars fit every age");
            var texts = sch.Element("texts")!.Elements("li").Select(e => e.Value).ToList();
            foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
                C(texts.Any(t => t.StartsWith("[their:" + age + "]")), $"the scholars as the {age} age has them");
            C(texts.Any(t => !t.StartsWith("[")), "and a line for any age");
            var keys = sch.Element("asks")!.Elements("li").Select(a => a.Element("key")?.Value).ToList();
            C(keys.Contains("library") && keys.Contains("history"), "they ask to read in the library and to hear the colony's story");
            C(sch.Element("help")!.Elements("li").Any(e => e.Value == "book"), "they may leave a book");
        }
        string camp = File.ReadAllText(Path.Combine(src, "Camps", "CampSeekers.cs"));
        C(camp.Contains("Together.Can(c, x)") && camp.Contains("TravelMath.MayTell(farNewsTold)"), "far news is told to a colonist sitting with them, two pieces at most");
        C(camp.Contains("StartSocialFight") && camp.Contains("seeking != TravelMath.Ore"), "the quarrel only among prospectors who came for the ore, between two of their own");
        string farSrc = File.ReadAllText(Path.Combine(src, "World", "Region", "FarNews.cs"));
        C(farSrc.Contains("OutsiderTalkMath.HeardMax") && farSrc.Contains("History.Add(Kind"), "far news goes into Heard lately (within its bound) and the history");
        C(farSrc.Contains("RegionTowns.Fits(s) && s.HasName"), "far news only of towns with a name of their own (it is told by name)");
        return n;
    }
}
