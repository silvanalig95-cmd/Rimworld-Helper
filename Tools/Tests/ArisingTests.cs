using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Peoples that arise during play, release B (Peoples/ArisingMath.cs; docs/design/peoples.md, "Release B"): the four
/// causes and when each is due, the pace and its setting, the league's towns, the template each cause takes in every
/// age, the goodwill a new people starts with (never an enemy or an ally), its emblem colour apart from the world's, its
/// name; and the mod's side: the words for every kind and calling, the setting and its presets, the region's day, the
/// one book saved, the leader made after the faith, the splinter only with Ideology, the town clear of the game's
/// nearness rule, the dev actions, the Almanac's section.
/// </summary>
public static class ArisingTests
{
    private const int Day = ArisingMath.DayTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "new peoples: " + what);
        }

        // ================= The causes =================

        C(ArisingMath.Kinds.SequenceEqual(new[] { "refugees", "league", "exiles", "splinter" }) && ArisingMath.IsKind("exiles") && !ArisingMath.IsKind("pirates") && !ArisingMath.IsKind(null),
            "four causes: survivors of a fallen town, a league of towns, a band of the banished, faithful who broke away");
        C(ArisingMath.Key("refugees", 4211) == "refugees:4211", "a cause is kept by its kind and id");
        string lk = ArisingMath.LeagueKey(new[] { 77, 12, 40 });
        C(lk == "league:12,40,77" && ArisingMath.LeagueTowns(lk).SequenceEqual(new[] { 12, 40, 77 }), "a league's key holds its towns, in order");
        C(ArisingMath.LeagueTowns("refugees:12").Count == 0 && ArisingMath.LeagueTowns(null).Count == 0, "no other key holds a league's towns");

        // ================= The pace =================

        C(Near(ArisingMath.Gap(1f, 40f), 40f) && Near(ArisingMath.Gap(2f, 40f), 20f) && Near(ArisingMath.Gap(0.5f, 40f), 80f) && float.IsPositiveInfinity(ArisingMath.Gap(0f, 40f))
          && Near(ArisingMath.Gap(5f, 40f), 20f), "the setting shortens or lengthens the waits; 0 never, past 200% no shorter");
        int now = 200 * Day;
        C(ArisingMath.PaceAllows(1f, now, -1, -1, 0), "a world with none yet may see one");
        C(!ArisingMath.PaceAllows(0f, now, -1, -1, 0) && !ArisingMath.PaceAllows(float.NaN, now, -1, -1, 0), "the setting at 0: none");
        C(!ArisingMath.PaceAllows(1f, 14 * Day, -1, -1, 0) && ArisingMath.PaceAllows(1f, 15 * Day, -1, -1, 0), "none before the world's 15th day");
        C(!ArisingMath.PaceAllows(1f, now, now - 39 * Day, -1, 0) && ArisingMath.PaceAllows(1f, now, now - 40 * Day, -1, 0), "40 days between two new peoples at 100%");
        C(!ArisingMath.PaceAllows(1f, now, now - 50 * Day, now - 89 * Day, 0) && ArisingMath.PaceAllows(1f, now, now - 50 * Day, now - 90 * Day, 0), "90 between two of the same cause");
        C(ArisingMath.PaceAllows(2f, now, now - 20 * Day, now - 45 * Day, 0) && !ArisingMath.PaceAllows(0.5f, now, now - 79 * Day, -1, 0), "200% halves the waits, 50% doubles them");
        C(!ArisingMath.PaceAllows(1f, now, -1, -1, ArisingMath.MaxStanding) && ArisingMath.PaceAllows(1f, now, -1, -1, ArisingMath.MaxStanding - 1), "six standing at most");
        // A year is 60 days: at 100% one or two a year at most, the same cause at most once in a year and a half.
        C(60f / ArisingMath.GapDays <= 1.5f && ArisingMath.KindGapDays >= 60f && ArisingMath.MaxRecords >= ArisingMath.MaxStanding, "one or two a year at most in a busy region");
        var looked = Enumerable.Range(0, 20).Count(d => ArisingMath.LookDay(d, 7));
        C(looked == 4 && Enumerable.Range(0, 5).Count(id => ArisingMath.LookDay(3, id)) == 1, "each cause looked at every five days, spread by its id");
        C(Enumerable.Range(0, 50).All(id => ArisingMath.Fated(id, 1, 0.4f) == ArisingMath.Fated(id, 1, 0.4f)) && !ArisingMath.Fated(5, 1, 0f) && ArisingMath.Fated(5, 1, 1f),
            "a cause's fate is the same each time it is asked");
        float share = Enumerable.Range(1000, 10000).Count(id => ArisingMath.Fated(id, ArisingMath.SaltRefugees, ArisingMath.RefugeesChance)) / 10000f;
        float shareTicks = Enumerable.Range(0, 4000).Count(i => ArisingMath.Fated(1500000L + i * 7919L, ArisingMath.SaltSplinter, 0.5f)) / 4000f;
        C(Math.Abs(share - 0.4f) < 0.03f && Math.Abs(shareTicks - 0.5f) < 0.04f, $"two fallen towns in five, half the wars of words ({share:0.000}, {shareTicks:0.000})");
        C(Enumerable.Range(0, 2000).Count(id => ArisingMath.Fated(id, ArisingMath.SaltRefugees, 0.5f) != ArisingMath.Fated(id, ArisingMath.SaltExiles, 0.5f)) > 600,
            "the causes' fates are not the same draw");

        C(!ArisingMath.RefugeesDue(10 * Day, 12 * Day) && ArisingMath.RefugeesDue(10 * Day, 13 * Day) && ArisingMath.RefugeesDue(10 * Day, 70 * Day)
          && !ArisingMath.RefugeesDue(10 * Day, 71 * Day) && !ArisingMath.RefugeesDue(-1, 13 * Day), "survivors settle from three days after the fall to sixty");
        C(!ArisingMath.ExilesDue(10 * Day, false, false, 24 * Day) && ArisingMath.ExilesDue(10 * Day, false, false, 25 * Day) && !ArisingMath.ExilesDue(10 * Day, false, false, 311 * Day),
            "a band gathers from 15 days after the banishment to 300");
        C(!ArisingMath.ExilesDue(10 * Day, true, false, 40 * Day) && !ArisingMath.ExilesDue(10 * Day, false, true, 40 * Day) && !ArisingMath.ExilesDue(-1, false, false, 40 * Day),
            "never one who came home, or rode with raiders");
        C(ArisingMath.SplinterDue(10 * Day, 10 * Day) && ArisingMath.SplinterDue(10 * Day, 40 * Day) && !ArisingMath.SplinterDue(10 * Day, 41 * Day) && !ArisingMath.SplinterDue(-1, 40 * Day),
            "faithful break away within 30 days of the war of words' end");
        C(ArisingMath.RefugeesChance > 0f && ArisingMath.RefugeesChance < 0.5f && ArisingMath.ExilesChance <= 0.5f && ArisingMath.SplinterChance <= 0.5f && ArisingMath.LeagueChance <= 0.25f,
            "rare: most causes give no people");

        // ================= The league's towns =================

        float[,] at = { { 0, 5, 9, 30, 12 }, { 5, 0, 6, 30, 12 }, { 9, 6, 0, 30, 12 }, { 30, 30, 30, 0, 30 }, { 12, 12, 12, 30, 0 } };
        float Tiles(int a, int b) => at[a, b];
        var lg = ArisingMath.LeagueOf(new[] { 80f, 70f, 65f, 90f, 40f }, new[] { 0, 1, 0, 2, 1 }, Tiles, (a, b) => false);
        C(lg.Count == 3 && lg.Contains(0) && lg.Contains(1) && lg.Contains(2), "three prospering towns of two peoples near each other band together; a far one and a poor one don't");
        C(ArisingMath.LeagueOf(new[] { 80f, 70f, 65f }, new[] { 0, 0, 0 }, Tiles, (a, b) => false).Count == 0, "towns of one people are no league");
        C(ArisingMath.LeagueOf(new[] { 80f, 70f, 65f }, new[] { 0, 1, 2 }, Tiles, (a, b) => a + b == 1).Count == 0, "peoples at war don't band together");
        C(ArisingMath.LeagueOf(new[] { 80f, 70f, 61f }, new[] { 0, 1, 2 }, Tiles, (a, b) => false).Count == 0, "each town at 62 or more");
        var big = ArisingMath.LeagueOf(new[] { 90f, 85f, 80f, 75f, 70f }, new[] { 0, 1, 2, 3, 4 }, (a, b) => 3f, (a, b) => false);
        C(big.Count == ArisingMath.LeagueMost && big[0] == 0, "four towns at most, the most prosperous first");
        C(ArisingMath.MeanAge(new[] { 1, 1, 2 }) == 1 && ArisingMath.MeanAge(new[] { 1, 2 }) == 2 && ArisingMath.MeanAge(new int[0]) == 2, "a league's age: most of its towns'");

        // ================= The template, in every age =================

        var callings = new[] { "traders", "healers", "herders" };
        var ages = new[] { 2, 1, 0 };
        string Of(string kind, int age)
        {
            int i = ArisingMath.Template(kind, age, callings, ages);
            return i < 0 ? null : callings[i];
        }
        foreach (var kind in ArisingMath.Kinds)
            for (int age = 0; age <= 3; age++)
                C(Of(kind, age) != null && ArisingMath.Callings(kind).Contains(Of(kind, age)), $"{kind} in the age {age}: one of release A's templates of a fitting calling");
        C(Of("refugees", 0) == "herders" && Of("refugees", 1) == "healers" && Of("refugees", 2) == "traders" && Of("refugees", 3) == "traders",
            "survivors take to the herds, a sickhouse or the road, by their age");
        C(Of("league", 0) == "herders" && Of("league", 1) == "traders" && Of("league", 2) == "traders", "a league of tribes is a herding folk, of towns a road league");
        C(Of("exiles", 0) == "herders" && Of("exiles", 1) == "herders" && Of("exiles", 2) == "traders" && Of("exiles", 3) == "traders", "a band of the banished herds or hires out on the roads");
        C(Of("splinter", 0) == "herders" && Of("splinter", 1) == "healers" && Of("splinter", 3) == "healers", "faithful who broke away keep a sickhouse, or follow their herds");
        C(ArisingMath.Template("league", 2, new[] { "healers" }, new[] { 1 }) == -1 && ArisingMath.Template("league", 2, null, null) == -1, "no template of its calling: none arises");
        C(ArisingMath.Callings("pirates").Length == 0 && ArisingMath.Kinds.All(k => ArisingMath.Callings(k).All(PeoplesMath.IsCalling)), "only callings Rimfolk's words know");

        // ================= How they feel =================

        C(ArisingMath.RefugeesGoodwill(true, 80, false) == -40 && ArisingMath.Feeling(-40) == -1, "survivors of a town the colony took are cool");
        C(ArisingMath.RefugeesGoodwill(false, 60, false) == 30 && ArisingMath.RefugeesGoodwill(false, 100, false) == 40 && ArisingMath.RefugeesGoodwill(false, -100, false) == -20,
            "else half the colony's goodwill with their old people, between -20 and 40");
        C(ArisingMath.RefugeesGoodwill(false, 60, true) <= 0, "never warm toward a colony their old people were at war with");
        C(ArisingMath.LeagueGoodwill(new[] { 40, 60 }) == 35 && ArisingMath.LeagueGoodwill(new int[0]) == 10 && ArisingMath.LeagueGoodwill(new[] { -100, -100 }) == -10,
            "a league: by its towns' peoples' goodwill");
        C(ArisingMath.ExilesGoodwill(true, 5, 0) == -30 && ArisingMath.ExilesGoodwill(false, 2, 0) == 26 && ArisingMath.ExilesGoodwill(false, 0, 4) == -20,
            "a band of the banished: cool with a grudge, warmer for those who stood by them, cooler for those glad to see them go");
        C(ArisingMath.SplinterGoodwill(true) == 25 && ArisingMath.SplinterGoodwill(false) == 10 && ArisingMath.WithParent < 0 && ArisingMath.WithParent > ArisingMath.Hostile,
            "faithful who broke away: warm after a fair debate; cool toward those they left, never at war");
        bool peace = true;
        for (int g = -100; g <= 100; g += 5)
            peace &= ArisingMath.AtPeace(ArisingMath.RefugeesGoodwill(g % 2 == 0, g, g < 0)) && ArisingMath.AtPeace(ArisingMath.LeagueGoodwill(new[] { g, -g }))
                     && ArisingMath.AtPeace(ArisingMath.ExilesGoodwill(g < 0, Math.Max(0, g / 10), Math.Max(0, -g / 10)));
        C(peace && ArisingMath.AtPeace(ArisingMath.WithKin) && ArisingMath.AtPeace(ArisingMath.WithMembers) && ArisingMath.AtPeace(ArisingMath.WithParent),
            "no new people is an enemy or an ally on its first day");
        C(ArisingMath.Feeling(-15) == -1 && ArisingMath.Feeling(-14) == 0 && ArisingMath.Feeling(14) == 0 && ArisingMath.Feeling(15) == 1, "cool, plain, warm");

        // ================= The splinter's creed, their name and colour =================

        C(ArisingMath.CreedChange(1, 0) == "dropped" && ArisingMath.CreedChange(0, 1) == "added" && ArisingMath.CreedChange(0, 0) == null, "their creed: a teaching dropped or one added");
        C(ArisingMath.NameFits("Ashford Folk", new[] { "Lenlia" }) && !ArisingMath.NameFits("Ashford Folk", new[] { "Ashford Folk" }) && !ArisingMath.NameFits(new string('a', 25), null)
          && !ArisingMath.NameFits("", null), "a name of their own: short, and no other people's");
        C(ArisingMath.Palette.Length == 12 && ArisingMath.Palette.All(p => p.Length == 3 && p.All(v => v >= 0f && v <= 1f)), "twelve emblem colours");
        float D(float[] a, float[] b) => (a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]);
        bool apart = true;
        for (int i = 0; i < 12; i++)
            for (int j = i + 1; j < 12; j++)
                apart &= D(ArisingMath.Palette[i], ArisingMath.Palette[j]) > 0.04f;
        // Release A's own colours (Defs/PeopleDefs/: teal, ivory, russet): each spectrum's two ends and its middle.
        var theirs = new[]
        {
            new[] { 0.16f, 0.50f, 0.54f }, new[] { 0.36f, 0.74f, 0.76f }, new[] { 0.26f, 0.62f, 0.65f }, new[] { 0.84f, 0.82f, 0.74f }, new[] { 0.97f, 0.95f, 0.88f },
            new[] { 0.90f, 0.88f, 0.81f }, new[] { 0.66f, 0.36f, 0.20f }, new[] { 0.88f, 0.58f, 0.34f }, new[] { 0.77f, 0.47f, 0.27f }
        };
        C(apart && ArisingMath.Palette.All(p => theirs.All(t => D(p, t) > 0.02f)), "they stand apart from each other and from the three peoples' own");
        C(ArisingMath.Colour(new List<float[]>()) == 0 && ArisingMath.Colour(new List<float[]> { ArisingMath.Palette[0] }) != 0, "a colour nobody has");
        var taken = ArisingMath.Palette.Take(11).ToList();
        C(ArisingMath.Colour(taken) == 11, "the one left when the rest are taken");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        bool Has(string k) => keyed.ContainsKey(k);
        foreach (var kind in ArisingMath.Kinds)
        {
            C(Has("CC_Arise_" + kind + "_Title") && Has("CC_Arise_H_" + kind) && Has("CC_Arise_Card_" + kind), $"{kind}: its title, its history's line, its card's line");
            foreach (var calling in ArisingMath.Callings(kind))
            {
                if (kind == "league") C(Has("CC_Arise_league_" + calling), $"a league of {calling}: its letter opens by its calling");
                else C(Has("CC_Arise_" + kind) && Has("CC_Arise_" + kind + "_" + calling), $"{kind} as {calling}: what they came from and how they live");
                if (kind != "splinter") C(Has("CC_Arise_Name_" + kind + "_" + calling) && Has("CC_Arise_Name_" + kind + "_" + calling + "_2"), $"{kind} as {calling}: two names");
            }
            foreach (var feel in new[] { "_Cool", "_Warm", "_Plain" }) C(Has("CC_Arise_" + kind + feel), $"{kind}: how they feel ({feel})");
        }
        C(Has("CC_Arise_refugees_CoolOurs") && Has("CC_Arise_refugees_Last") && Has("CC_Arise_exiles_CoolGlad") && Has("CC_Arise_exiles_WarmFriend") && Has("CC_Arise_splinter_Parent")
          && Has("CC_Arise_splinter_dropped") && Has("CC_Arise_splinter_added") && Has("CC_Arise_splinter_StandIn") && Has("CC_Arise_Town") && Has("CC_Arise_TownName_Refugees"),
            "the town they took, the last of a people, those glad to see them go, the friend asked after, the creed's change, their town");
        C(keyed["CC_Arise_refugees_CoolOurs"].Contains("burned") && keyed["CC_Arise_exiles_Cool"].Contains("forgiven") && keyed["CC_Arise_splinter_Parent"].Contains("no love lost"),
            "a grievance is said, so a cool welcome is never unexplained");
        C(keyed["CC_Arise_refugees_healers"].Contains("sickhouse") && keyed["CC_Arise_refugees_traders"].Contains("waystation") && keyed["CC_Arise_refugees_herders"].Contains("grass")
          && keyed["CC_Arise_exiles_traders"].Contains("waystation") && keyed["CC_Arise_splinter_healers"].Contains("sickhouse"), "how they live is what their town is on the map");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Arise_") && !kv.Key.StartsWith("CC_Arise_Name_") && kv.Key != "CC_Arise_TownWord" && kv.Key != "CC_Arise_Someone")
                .All(kv => kv.Value.TrimEnd().EndsWith(".") || kv.Key.EndsWith("_Title") || kv.Key == "CC_Arise_TownName_Refugees"), "every line a sentence");
        C(keyed.Values.All(v => !v.Contains('—') && !v.Contains('!')), "no em dashes and no exclamation marks");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\bin \d+ (days?|hours?)\b")), "no countdowns");
        C(new[] { "CC_Arise_refugees", "CC_Arise_refugees_2", "CC_Arise_league_traders", "CC_Arise_league_herders", "CC_Arise_exiles", "CC_Arise_splinter", "CC_Arise_splinter_StandIn" }
            .All(k => keyed[k].StartsWith("{MEANS}") && keyed[k].Contains("{NAME}")), "every opening comes by the colony's means and names the new people");
        C(keyed["CC_Arise_Town"].Contains("{WHERE}") && keyed["CC_Arise_Town"].Contains("{RITE}") && keyed["CC_Arise_Town"].Contains("{TOWNNEW}"),
            "their town: where it stands, with the founding's rite by their age");
        C(keyed.ContainsKey("CC_Settings_NewPeoples") && keyed.ContainsKey("CC_SetNote_NewPeoples") && keyed["CC_Settings_NewPeoples_Tip"].Contains("40 days")
          && keyed["CC_Settings_NewPeoples_Tip"].Contains("0% switches it off"), "the setting says what it does");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string arising = Read("Peoples", "Arising.cs"), book = Read("Peoples", "ArisingBook.cs"), words = Read("Peoples", "ArisingWords.cs"), debug = Read("Peoples", "ArisingDebug.cs");
        string all = arising + book + words + debug;
        C(!Regex.IsMatch(all, @"Sched\.|GameComponentTick|override void Tick"), "no clock of its own: the region's day");
        C(Read("World", "Region", "RegionBook.cs").Contains("if (Arising.On) Safe(\"new peoples\", () => Arising.Daily(now));"), "run by the region's day");
        C(arising.Contains("if (made != null) return;"), "one a day at most");
        C(!Regex.IsMatch(arising + words + debug, "Scribe_|ExposeData") && book.Contains("Scribe_Collections.Look(ref arisen, \"arisen\", LookMode.Deep)"), "one book saved, its records deep");
        C(book.Contains("ArisingMath.MaxRecords"), "the records capped");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Peoples/ArisingBook.cs:arisen"), "the save's model has the records");
        C(arising.Contains("if (!ModsConfig.IdeologyActive) return null;") && arising.Contains("Council_WS_HolyWar"), "without Ideology no faithful break away; with it, after a war of words");
        C(arising.Contains("IdeoUtility.RandomizeNormalMemesForReforming"), "their creed reformed the game's own way");
        C(Read("Colonies", "HandOverPatches.cs").Contains("if (Arising.Making && Peoples.IsOurs(__instance))") && arising.Contains("f.TryGenerateNewLeader()"),
            "the game makes no leader while the people is made: one comes once its faith is set");
        C(arising.Contains("c.exile.SetFaction(f);") && arising.Contains("f.leader = c.exile;"), "the one sent away leads the band they gathered");
        C(Read("Social", "Banishment.cs").Contains("x.Faction.leader == x"), "and rides with no one else's raid");
        C(arising.Contains("RiseFallMath.ClearOfHomes(nearest, goodwill)") && arising.Contains("TileFinder.IsValidTileForNewSettlement(t)") && arising.Contains("WorldMechanics.TerritoryOwner(t)"),
            "their town clear of the game's nearness rule round every home, on no other people's land");
        C(Read("Peoples", "Peoples.cs").Contains("Arising.TryTownTile(near.Tile, 3, 10, null, null, out var tile)"), "the dev tools' people made near a home keep clear of the nearness rule too");
        C(arising.Contains("new FactionGeneratorParms(def, parms, true)") && arising.Contains("f.hidden = false;") && arising.Contains("Find.FactionManager.Add(f);"),
            "made the game's way, hidden while made so the game places no town of its own");
        C(arising.Contains("RegionFaces.Founded(town, f.leader") && arising.Contains("RiseFall.NoteFounded(town)"), "the region gives their town its faces and counts it on its pace");
        C(arising.Contains("ArisingWords.Tell(c, f, town, detail)") && words.Contains("Importance.Important") && words.Contains("new GlobalTargetInfo(town)"),
            "a letter, never held back, that looks at their town on the map");
        C(words.Contains("Ages.Form(AgeFeature.Founding") && words.Contains("form.rite.Named(\"RITE\")") && words.Contains("form.word.CapitalizeFirst().Named(\"MEANS\")"),
            "the news by the colony's means, their town's rite by their age");
        C(words.Contains("public const string Kind = \"world:arose\"") && words.Contains("HistoryMath.Milestone"), "the history keeps it as a milestone of the world");
        C(words.Contains("CardKind.People") && words.Contains("CardSections.TownLines"), "their card and their towns' cards say where they came from");
        C(debug.Contains("\"Peoples: a new people arises now (testing)...\"") && debug.Contains("\"Peoples: what could arise now\""), "dev actions: each cause now, and what could arise and why not");

        // The setting: a rate on How much happens, at 100% by default, set by the presets.
        string settings = Read("CouncilSettings.cs"), catalog = Read("UI", "SettingsCatalog.cs");
        C(settings.Contains("public float newPeoples = 1f;") && settings.Contains("Scribe_Values.Look(ref newPeoples, \"newPeoples\", 1f);"), "the setting, 100% by default, saved");
        C(catalog.Contains("Slider(\"newPeoples\", \"CC_Settings_NewPeoples\", \"CC_SetNote_NewPeoples\", 0f, 2f, 0.05f, \"%\");")
          && catalog.Contains("V(\"newPeoples\", \"0.5\")") && catalog.Contains("V(\"newPeoples\", \"1.25\")"), "a slider from 0 to 200%; light touch halves it, story-rich a quarter more");
        C(arising.Contains("ColonyCouncilMod.Settings?.newPeoples ?? 1f"), "read where it runs");

        // The Almanac: a section on the peoples' page, its keywords as they were.
        var page = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Peoples.xml")).Root!.Elements().Single();
        C(page.Element("sections")!.Elements("li").Any(li => (li.Element("heading")?.Value ?? "").Contains("arise")), "the Almanac's peoples page has a section on peoples that arise");
        C(page.Element("keywords")!.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "road league", "hospitaller order", "herding folk", "waystation", "sickhouse", "winter grazing" }),
            "its keywords unchanged");
        return n;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
