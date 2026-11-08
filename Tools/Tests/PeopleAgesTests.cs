using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Peoples whose age changes (Peoples/PeopleAgesMath.cs, PeopleAges.cs, PeopleAgeWords.cs, PeopleAgePatches.cs;
/// docs/design/peoples-third.md, "Ages that change over time"; DESIGN.md, "Peoples whose age changes"): the one rule (its
/// pace, limits and floor, the colony's quickening, being cut off, the Sunken Vault's way, the traders' caps, what counts
/// as a gift of learning, every age whole), each people's pace and limit in its def, the rule played out over years for
/// the fifteen (the numbers in <see cref="Summary"/>, DESIGN.md has them), every age a people can reach dressed and armed
/// from the player's own mods, and the seams.
/// </summary>
public static class PeopleAgesTests
{
    /// <summary>The simulation's numbers, for the run's line (and DESIGN.md).</summary>
    public static string Summary = "";

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples' ages: " + what);
        }

        // ================= Ages and tech levels =================

        C(PeopleAgesMath.TechOfAge(0) == 2 && PeopleAgesMath.TechOfAge(1) == 3 && PeopleAgesMath.TechOfAge(2) == 4 && PeopleAgesMath.TechOfAge(3) == 5 && PeopleAgesMath.TechOfAge(9) == 5,
            "an age's tech level, as the game numbers them");
        C(Enumerable.Range(0, 4).All(a => PeopleAgesMath.AgeOfTech(PeopleAgesMath.TechOfAge(a)) == a) && PeopleAgesMath.AgeOfTech(7) == 3 && PeopleAgesMath.AgeOfTech(1) == 0
          && Enumerable.Range(0, 8).All(t => PeopleAgesMath.AgeOfTech(t) == (int)LedgerMath.AgeOf(t)), "and back, as the ledger reads a tech level");

        // ================= A people's ways =================

        C(PeopleAgesMath.Limit(-1, 1) == 2 && PeopleAgesMath.Limit(2, 1) == 2 && PeopleAgesMath.Limit(2, 3) == 3 && PeopleAgesMath.Limit(-1, 3) == 3 && PeopleAgesMath.Limit(9, 0) == 3,
            "the highest age a people takes: its ways' limit, never below the age it began in");
        C(PeopleAgesMath.Floor(-1, 0) == 0 && PeopleAgesMath.Floor(-1, 2) == 1 && PeopleAgesMath.Floor(0, 2) == 0 && PeopleAgesMath.Floor(3, 1) == 1,
            "the lowest it falls back to: one below the age it began in, or its ways' floor");
        C(PeopleAgesMath.Sound(10f, 2, 1, false, 0f) && !PeopleAgesMath.Sound(10f, 1, 1, false, 0f) && PeopleAgesMath.Sound(8f, 3, 3, false, 0f)
          && !PeopleAgesMath.Sound(0f, 2, 1, false, 0f) && !PeopleAgesMath.Sound(float.NaN, 2, 1, false, 0f) && !PeopleAgesMath.Sound(10f, 2, 1, true, 0f),
            "sound ways: a pace, room to rise an age unless at the spacer age already, and a decline's years");
        C(PeopleAgesMath.Reachable(1, -1, -1, 2, -1).SequenceEqual(new[] { 0, 1, 2 }) && PeopleAgesMath.Reachable(1, 1, 3, 2, -1).SequenceEqual(new[] { 0, 1, 2, 3 })
          && PeopleAgesMath.Reachable(0, -1, -1, 1, -1).SequenceEqual(new[] { 0, 1 }) && PeopleAgesMath.Reachable(3, -1, -1, 3, -1).SequenceEqual(new[] { 2, 3 }),
            "every age a people of a template can reach, from every age it can begin in");

        // ================= Its fate =================

        var fates = Enumerable.Range(1, 500).Select(PeopleAgesMath.Fate).ToList();
        var heads = Enumerable.Range(1, 500).Select(PeopleAgesMath.HeadStart).ToList();
        C(fates.All(x => x >= 0.8f && x <= 1.25f) && fates.Max() - fates.Min() > 0.35f && Math.Abs(fates.Average() - 1.025f) < 0.04f, "each people's own pace: 0.8 to 1.25 of its ways', spread evenly");
        C(heads.All(x => x >= 0f && x <= 0.2f) && heads.Max() > 0.15f && heads.Min() < 0.05f && PeopleAgesMath.Fate(42) == PeopleAgesMath.Fate(42),
            "a head start of up to a fifth of an age, the same each time");
        C(Enumerable.Range(0, 40).All(id => Enumerable.Range(100, 15).Count(day => PeopleAgesMath.LookDay(day, id)) == 1) && !PeopleAgesMath.LookDay(-1, 3),
            "each people looked at once a season, on a day of its own");

        // ================= Rising =================

        C(PeopleAgesMath.StepFactor(0) == 1.2f && PeopleAgesMath.StepFactor(1) == 1f && PeopleAgesMath.StepFactor(2) == 0.7f && PeopleAgesMath.StepFactor(3) == 0f,
            "the first step comes easiest, the climb to the spacer age is the longest");
        var why = new List<(string key, float size)>();
        C(PeopleAgesMath.Boost(new AgeDeeds(), why) == 1f && why.Count == 1 && why[0].key == "years", "with nothing from the colony: its own pace, as the years pass");
        why.Clear();
        C(Math.Abs(PeopleAgesMath.Boost(new AgeDeeds { trade = 2500f }, why) - 1.5f) < 1e-4f && why[0].key == "trade"
          && Math.Abs(PeopleAgesMath.Boost(new AgeDeeds { trade = 50000f }, null) - 2f) < 1e-4f, "trade with the colony: up to twice the pace at 5000 silver a year");
        why.Clear();
        float allBoost = PeopleAgesMath.Boost(new AgeDeeds { trade = 9000f, gifts = 9000f, learners = 5, lessons = 5, bond = 1f }, why);
        C(allBoost == PeopleAgesMath.MaxBoost && why.Select(w => w.key).SequenceEqual(new[] { "trade", "teaching", "gifts", "ally" }), "everything at once: three times the pace at most, the largest reason first");
        why.Clear();
        PeopleAgesMath.Boost(new AgeDeeds { learners = 1, lessons = 1, bond = 0.5f, gifts = 300f }, why);
        C(why.Select(w => w.key).SequenceEqual(new[] { "teaching", "pact", "gifts" }) && Math.Abs(why[0].size - 0.6f) < 1e-4f,
            "an apprentice and a lesson, a trade pact and a small gift of books");
        C(Math.Abs(PeopleAgesMath.OwnPace(10f, 1, 1f) - 1f / 40f) < 1e-6f && PeopleAgesMath.OwnPace(0f, 1, 1f) == 0f && PeopleAgesMath.OwnPace(10f, 3, 1f) == 0f,
            "its own pace: an age in so many years");

        // ================= Falling back =================

        why.Clear();
        C(Math.Abs(PeopleAgesMath.CutOff(new AgeDeeds { yearsAlone = 3f, towns = 3 }, why) - 0.5f) < 1e-4f && why[0].key == "alone", "three years with nobody's trade: half a sign");
        C(PeopleAgesMath.CutOff(new AgeDeeds { burned = 1, towns = 1 }, null) == 1f && PeopleAgesMath.CutOff(new AgeDeeds { burned = 1, towns = 4 }, null) == 0.5f
          && PeopleAgesMath.CutOff(new AgeDeeds { burned = 3, towns = 4 }, null) == 1f, "towns burned: half a sign each, all of it for the last town");
        C(PeopleAgesMath.CutOff(new AgeDeeds { war = 0.5f, towns = 2 }, null) == 0f && Math.Abs(PeopleAgesMath.CutOff(new AgeDeeds { war = 0.85f, towns = 2 }, null) - 1f) < 1e-4f,
            "at war with half the world is no sign; with most of it, a whole one");
        C(PeopleAgesMath.Severity(1f) == 0f && Math.Abs(PeopleAgesMath.Severity(1.2f) - 0.5f) < 1e-4f && PeopleAgesMath.Severity(3f) == 1f && PeopleAgesMath.Severity(float.NaN) == 0f,
            "one sign alone cuts nobody off; more than one does, slowly");

        // ================= The season =================

        var ways = new AgeWays { pace = 10f, limit = 2, floor = 0 };
        int seasons = 0;
        var s = new AgeStep { age = 1, progress = 0f };
        do
        {
            s = PeopleAgesMath.Season(s.age, s.progress, ways, 1f, new AgeDeeds { towns = 2 });
            seasons++;
        } while (s.change == 0 && seasons < 200);
        C(s.change == 1 && s.age == 2 && s.progress == 0f && seasons >= 39 && seasons <= 41 && s.why == "years", $"a medieval people of ten years an age rises in ten years by itself ({seasons} seasons)");
        var top = PeopleAgesMath.Season(2, 0.5f, ways, 1f, new AgeDeeds { trade = 9000f, towns = 2 });
        C(top.change == 0 && top.age == 2 && top.progress == 0f && top.heading == 0 && top.why == "settled", "at its limit it is settled in its ways, whatever the colony does");
        var back = PeopleAgesMath.Season(2, -0.5f, ways, 1f, new AgeDeeds { towns = 2 });
        C(back.progress > -0.5f && back.progress <= 0f && back.heading == 0, "at its limit it comes back up to nothing if it had lost ground");
        var height = PeopleAgesMath.Season(3, 0f, new AgeWays { pace = 8f, limit = 3, floor = 2 }, 1f, new AgeDeeds { towns = 2 });
        C(height.why == "height" && height.change == 0, "at the spacer age: the height of what anyone knows");
        var helped = PeopleAgesMath.Season(1, 0f, ways, 1f, new AgeDeeds { trade = 5000f, towns = 2 });
        C(Math.Abs(helped.progress - 2f / 40f) < 1e-5f && helped.heading == 1 && helped.why == "trade", "trade at 5000 silver a year doubles the season's way");
        var cut = new AgeDeeds { yearsAlone = 5f, war = 0.9f, towns = 2 };
        var falling = PeopleAgesMath.Season(1, 0.5f, ways, 1f, cut);
        C(falling.heading == -1 && falling.progress < 0.5f && falling.why == "alone,war", "cut off: it loses ground instead, and says why");
        var cutTrade = cut;
        cutTrade.trade = 9000f;
        C(PeopleAgesMath.Season(1, 0.5f, ways, 1f, cutTrade).heading == 1, "the colony's trade is trade: a people the colony trades with is not alone");
        seasons = 0;
        s = new AgeStep { age = 1, progress = 0f };
        var cutAll = new AgeDeeds { yearsAlone = 6f, war = 1f, burned = 1, towns = 1 };
        do
        {
            s = PeopleAgesMath.Season(s.age, s.progress, ways, 1f, cutAll);
            seasons++;
        } while (s.change == 0 && seasons < 200);
        C(s.change == -1 && s.age == 0 && seasons >= 19 && seasons <= 21, $"cut off wholly, a medieval people falls back to the tribes in five years ({seasons} seasons)");
        var floor = PeopleAgesMath.Season(0, -0.98f, ways, 1f, cutAll);
        C(floor.change == 0 && floor.age == 0 && floor.progress == -0.99f && floor.blocked && floor.heading == -1, "at its floor it holds on");

        // ================= The Sunken Vault's way =================

        var vault = new AgeWays { pace = 8f, limit = 3, floor = 1, declines = true, declineYears = 4f };
        seasons = 0;
        s = new AgeStep { age = 3, progress = 0f };
        do
        {
            s = PeopleAgesMath.Season(s.age, s.progress, vault, 1f, new AgeDeeds { towns = 2, trade = 9000f, learners = 3, bond = 1f });
            seasons++;
        } while (s.change == 0 && seasons < 200);
        C(s.change == -1 && s.age == 2 && seasons == 16 && s.why == "unsupplied", $"a people that declines unless supplied loses an age every so many years, whatever else the colony does ({seasons} seasons)");
        var held = PeopleAgesMath.Season(3, -0.4f, vault, 1f, new AgeDeeds { supply = 1000f, gifts = 600f, towns = 2 });
        C(held.progress == -0.4f && held.heading == 0 && held.why == "supplied", "supplied with components and parts, it holds its age");
        var half = PeopleAgesMath.Season(3, 0f, vault, 1f, new AgeDeeds { supply = 750f, towns = 2 });
        C(Math.Abs(half.progress + 0.5f / 16f) < 1e-5f, "half supplied, it declines at half the pace");
        C(PeopleAgesMath.Supplied(new AgeDeeds { supply = 3000f }) == 1f && PeopleAgesMath.Supplied(new AgeDeeds { supply = -5f }) == 0f, "supply between nothing and enough");
        var vaultFloor = PeopleAgesMath.Season(1, -0.99f, vault, 1f, new AgeDeeds { towns = 2 });
        C(vaultFloor.age == 1 && vaultFloor.blocked, "and never below its floor");

        // ================= The traders' goods =================

        C(PeopleAgesMath.ShiftedCap(3, 3, 4) == 4 && PeopleAgesMath.ShiftedCap(3, 3, 5) == 5 && PeopleAgesMath.ShiftedCap(7, 3, 4) == 7 && PeopleAgesMath.ShiftedCap(7, 4, 3) == 3
          && PeopleAgesMath.ShiftedCap(4, 4, 3) == 3 && PeopleAgesMath.ShiftedCap(2, 3, 2) == 2 && PeopleAgesMath.ShiftedCap(0, 3, 4) == 0 && PeopleAgesMath.ShiftedCap(3, 3, 3) == 3,
            "a trader's cap set for the people's own age moves with its age; an open one closes for a people fallen back");

        // ================= What counts =================

        C(PeopleAgesMath.LearnedKind("TextBook", true, false, null, null) == "books" && PeopleAgesMath.LearnedKind("Techprint_Foo", false, true, null, null) == "techprints"
          && PeopleAgesMath.LearnedKind("ComponentIndustrial", false, false, new[] { "Manufactured" }, null) == "components"
          && PeopleAgesMath.LearnedKind("Gizmo", false, false, new[] { "Components" }, null) == "components"
          && PeopleAgesMath.LearnedKind("Hammer", false, false, null, new[] { "ToolDecent" }) == "tools"
          && PeopleAgesMath.LearnedKind("Steel", false, false, new[] { "ResourcesRaw" }, null) == null && PeopleAgesMath.LearnedKind(null, false, false, null, null) == null,
            "a gift of learning: books, techprints, components and tools, nothing else");
        C(PeopleAgesMath.Part("components") && PeopleAgesMath.Part("tools") && !PeopleAgesMath.Part("books"), "components and tools are the parts a declining people needs");
        C(PeopleAgesMath.GiftWeight(0f) == 0 && PeopleAgesMath.GiftWeight(30f) == 2 && PeopleAgesMath.GiftWeight(600f) == 10 && PeopleAgesMath.GiftWeight(99999f) == 30,
            "the deed of a gift weighs its worth");

        // ================= Every age whole: the rules =================

        C(!PeopleAgesMath.Later(3, 0) && PeopleAgesMath.Later(4, 1) && PeopleAgesMath.Later(4, 0) && !PeopleAgesMath.Later(4, 2) && PeopleAgesMath.Later(5, 2) && !PeopleAgesMath.Later(6, 3)
          && !PeopleAgesMath.Later(0, 0), "of a later age: guns and flak among the tribes and the medieval, spacer gear in the industrial age, nothing at the spacer age");
        C(PeopleAgesMath.BowInGunAge(2, true, true, 2) && PeopleAgesMath.BowInGunAge(3, true, true, 3) && !PeopleAgesMath.BowInGunAge(3, true, false, 2)
          && !PeopleAgesMath.BowInGunAge(3, false, true, 2) && !PeopleAgesMath.BowInGunAge(4, true, true, 2) && !PeopleAgesMath.BowInGunAge(2, true, true, 1),
            "a fighter's bow in an age of guns");
        C(PeopleAgesMath.FallbackDress(0).SequenceEqual(new[] { "Apparel_TribalA" }) && PeopleAgesMath.FallbackDress(2).SequenceEqual(new[] { "Apparel_BasicShirt", "Apparel_Pants" }),
            "the age's plain clothes");

        // ================= The player's catalogue =================

        string catDir = Path.Combine(root, "docs", "player-mods");
        List<Dictionary<string, string>> Table(string file)
        {
            var lines = File.ReadAllLines(Path.Combine(catDir, file));
            var head = lines[0].Split('\t');
            return lines.Skip(1).Where(l => l.Length > 0).Select(l =>
            {
                var c = l.Split('\t');
                var row = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
                return row;
            }).ToList();
        }
        var mods = new Dictionary<string, bool>();
        foreach (var r in Table("mods.tsv")) mods[r["packageId"].ToLowerInvariant()] = r["active"] == "yes";
        bool ModOn(string id) => id.StartsWith("ludeon.rimworld") || mods.TryGetValue(id, out bool on) && on;
        var apparel = Table("apparel.tsv").Where(r => r["active"] == "yes").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var weapons = Table("weapons.tsv").Where(r => r["active"] == "yes").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var gearWorth = File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")).Where(l => !l.StartsWith("#") && l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[1] == "weapon" && c[5] != "-").ToDictionary(c => c[2], c => F(c[5]));
        int Tech(Dictionary<string, string> r) => r["techLevel"] switch
        {
            "Animal" => 1, "Neolithic" => 2, "Medieval" => 3, "Industrial" => 4, "Spacer" => 5, "Ultra" => 6, "Archotech" => 7, _ => 0
        };
        string[] Tags(Dictionary<string, string> r, string col) => (r[col] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        float Worth(Dictionary<string, string> r) =>
            float.TryParse(r["marketValue"], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0f ? v : gearWorth.TryGetValue(r["defName"], out var g) ? g : float.NaN;
        bool Core(Dictionary<string, string> r) => r["mod"] == "ludeon.rimworld";
        C(apparel.Count > 400 && weapons.Count > 500, "the player's catalogue is read");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var docs = Directory.GetFiles(peopleDir, "Rimfolk_People_*.xml").OrderBy(f => f).Select(f => (file: f, text: File.ReadAllText(f), doc: XDocument.Load(f))).ToList();
        var all = docs.SelectMany(d => d.doc.Root!.Elements()).ToList();
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToList();
        var kindEls = all.Where(e => e.Name.LocalName == "PawnKindDef").ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        int OwnAge(XElement f) => PeopleAgesMath.AgeOfTech(f.Element("techLevel")!.Value switch { "Neolithic" => 2, "Medieval" => 3, "Industrial" => 4, _ => 5 });
        C(factions.Count == 15, "fifteen peoples");
        var paces = new Dictionary<string, float>();
        var limits = new Dictionary<string, int>();
        foreach (var f in factions)
        {
            string fd = Dn(f);
            var ext = Ext(f, "PeopleExtension")!;
            bool has = ext.Element("agePace") != null && ext.Element("ageLimit") != null;
            C(has, $"{fd}: its pace and limit in its def");
            if (!has) continue;
            float pace = F(ext.Element("agePace")!.Value);
            int limit = int.Parse(ext.Element("ageLimit")!.Value);
            paces[fd] = pace;
            limits[fd] = limit;
            int own = OwnAge(f);
            C(PeopleAgesMath.Sound(pace, limit, own, false, 0f) && pace >= 5f && pace <= 30f, $"{fd}: sound ways ({pace} years an age, up to the age {limit} from {own})");
            C(limit > own || own == 3, $"{fd}: it can rise at least one age (or is at the spacer age already)");
            var comment = ext.Element("agePace")!.NodesBeforeSelf().OfType<XComment>().LastOrDefault();
            C(comment != null && comment.Value.Contains("Their age over the years") && comment.Value.Trim().Length > 60, $"{fd}: a line of why beside its pace");
        }
        var byPace = paces.OrderBy(p => p.Value).Select(p => p.Key).ToList();
        C(byPace.Take(3).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_League", "Rimfolk_Scholars", "Rimfolk_Stormglass" }), "the society, the Conclave and the road league rise soonest");
        C(byPace.Skip(13).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_Herders", "Rimfolk_Mossborn" }) && limits["Rimfolk_Mossborn"] == 1 && limits["Rimfolk_Herders"] == 2,
            "the Mossborn and the herders rise latest, and not past the medieval and the industrial age");
        C(limits["Rimfolk_Order"] == 2 && limits["Rimfolk_Enclave"] == 3, "the hospitallers' faith keeps them from the spacer age; the enclave is at the height already");
        C(!docs.Any(d => d.text.Contains("<ageDeclines>true</ageDeclines>")), "no people of the fifteen declines unless supplied (that is the Sunken Vault's, still to come)");

        // ================= The years, played out =================

        // The fifteen in a world, from their defs: each its own fate and head start (by a made-up id), looked at every season.
        var peoples = factions.Where(f => paces.ContainsKey(Dn(f))).Select(f => (name: Dn(f), own: OwnAge(f), pace: paces[Dn(f)], limit: limits[Dn(f)])).ToList();
        // A typical game: the colony trades with the road league (and holds a trade pact with it), the guild (with a
        // journeyman's wander-year and a small gift of tools), the hospitallers (allied), the river folk and the society
        // (a gift of books, one of its masters teaching a colonist); everyone else goes its own way.
        var typical = new Dictionary<string, AgeDeeds>
        {
            ["Rimfolk_League"] = new AgeDeeds { trade = 3500f, bond = 0.5f, towns = 3 },
            ["Rimfolk_KilnGuild"] = new AgeDeeds { trade = 1500f, gifts = 300f, learners = 1, towns = 3 },
            ["Rimfolk_Order"] = new AgeDeeds { trade = 1200f, bond = 1f, towns = 3 },
            ["Rimfolk_RiverFolk"] = new AgeDeeds { trade = 800f, towns = 3 },
            ["Rimfolk_Scholars"] = new AgeDeeds { trade = 800f, gifts = 300f, lessons = 1, towns = 3 }
        };
        // The colony that helps one learned people all it can (the guild): trade, gifts, two journeymen a year, an alliance.
        var devoted = new Dictionary<string, AgeDeeds> { ["Rimfolk_KilnGuild"] = new AgeDeeds { trade = 5000f, gifts = 1500f, learners = 2, bond = 1f, towns = 3 } };
        const int Worlds = 400;
        (double rose, double twice, Dictionary<string, double> share) Play(Dictionary<string, AgeDeeds> help, float years)
        {
            double rose = 0, twice = 0;
            var share = peoples.ToDictionary(p => p.name, p => 0.0);
            for (int w = 0; w < Worlds; w++)
                for (int i = 0; i < peoples.Count; i++)
                {
                    var p = peoples[i];
                    int id = w * 100 + i;
                    var ways2 = new AgeWays { pace = p.pace, limit = PeopleAgesMath.Limit(p.limit, p.own), floor = PeopleAgesMath.Floor(-1, p.own) };
                    var deeds = help.TryGetValue(p.name, out var h) ? h : new AgeDeeds { towns = 3 };
                    var st = new AgeStep { age = p.own, progress = PeopleAgesMath.HeadStart(id) };
                    for (int q = 0; q < years * PeopleAgesMath.SeasonsAYear; q++) st = PeopleAgesMath.Season(st.age, st.progress, ways2, PeopleAgesMath.Fate(id), deeds);
                    if (st.age > p.own) rose++;
                    if (st.age > p.own + 1) twice++;
                    if (st.age > p.own) share[p.name]++;
                }
            return (rose / Worlds, twice / Worlds, share.ToDictionary(kv => kv.Key, kv => kv.Value / Worlds));
        }
        var none5 = Play(new Dictionary<string, AgeDeeds>(), 5f);
        var none10 = Play(new Dictionary<string, AgeDeeds>(), 10f);
        var t5 = Play(typical, 5f);
        var t75 = Play(typical, 7.5f);
        var t10 = Play(typical, 10f);
        var d10 = Play(devoted, 10f);
        var d7 = Play(devoted, 7f);
        string Pct(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        Summary = $"by themselves {none5.rose:0.0} of 15 rise an age in 5 years and {none10.rose:0.0} in 10; in a typical game {t5.rose:0.0} in 5 years, {t75.rose:0.0} in 7.5 "
                  + $"and {t10.rose:0.0} in 10 (two ages: {t10.twice:0.00}); the guild with all the colony's help goes two ages in 10 years in {Pct(d10.twice)} "
                  + $"of worlds ({Pct(d7.twice)} in 7); the herders rise in 10 years in {Pct(t10.share["Rimfolk_Herders"])}, the Mossborn in {Pct(t10.share["Rimfolk_Mossborn"])}";
        C(none5.rose <= 2 && none10.rose >= 2 && none10.rose <= 6.5, $"by themselves, a few rise over the years ({none5.rose:0.0} in 5, {none10.rose:0.0} in 10)");
        C(t5.rose >= 1 && t5.rose <= 3.5 && t75.rose >= 2.5 && t75.rose <= 5.5 && t10.rose >= 4 && t10.rose <= 8,
            $"in a typical game of 5 to 10 years a few peoples reach the next age ({t5.rose:0.0}, {t75.rose:0.0}, {t10.rose:0.0})");
        C(t10.twice < 1 && d10.twice >= 0.5 && d7.twice > 0.1, $"a learned one may go two ages with the colony's help ({Pct(d10.twice)} of worlds in 10 years), and hardly anyone otherwise");
        C(t10.share["Rimfolk_Mossborn"] < 0.05 && t10.share["Rimfolk_Herders"] < 0.15 && t5.share["Rimfolk_Herders"] == 0 && t5.share["Rimfolk_Mossborn"] == 0,
            "the Mossborn and the herders rise late, if at all");
        C(t10.share["Rimfolk_League"] > 0.9 && t10.share["Rimfolk_Scholars"] > 0.9 && t10.share["Rimfolk_Enclave"] == 0, "the league and the society the colony trades with reach the spacer age within ten years");
        C(t75.share["Rimfolk_Order"] > none10.share["Rimfolk_Order"] * 0.9 && t5.share["Rimfolk_Order"] > none5.share["Rimfolk_Order"], "an alliance and trade quicken the hospitallers");

        // ================= Every age whole =================

        var kits = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKits.xml")).Root!.Elements().Select(e => e.Element("age")?.Value).Where(a => a != null).ToHashSet();
        C(new[] { "tribal", "medieval", "industrial", "spacer" }.All(kits.Contains), "a camp kit for every age, so each people builds its camps its age's way");
        foreach (string name in new[] { "Apparel_TribalA", "Apparel_BasicShirt", "Apparel_Pants" })
            C(apparel.TryGetValue(name, out var r) && Core(r) && Tech(r) <= 3, $"the age's plain clothes {name} are the base game's, of no later age than medieval");
        bool Covers(IEnumerable<string> names, string part) => names.Any(nm => apparel.TryGetValue(nm, out var r) && Tags(r, "bodyPartGroups").Contains(part));
        C(Covers(PeopleAgesMath.FallbackDress(0), "Torso") && Covers(PeopleAgesMath.FallbackDress(0), "Legs") && Covers(PeopleAgesMath.FallbackDress(1), "Torso")
          && Covers(PeopleAgesMath.FallbackDress(1), "Legs"), "they cover the body and the legs");
        string Req(XElement e) => (e.Attribute("MayRequire")?.Value ?? "").ToLowerInvariant();
        bool Avail(XElement e) => Req(e).Split(',', StringSplitOptions.RemoveEmptyEntries).All(id => ModOn(id.Trim()));
        List<XElement> Merged(XElement k, string list)
        {
            var parent = kindEls.FirstOrDefault(p => p.Attribute("Name")?.Value == k.Attribute("ParentName")?.Value);
            var own = k.Element(list);
            var mine = own?.Elements("li").Where(Avail).ToList() ?? new List<XElement>();
            if (own?.Attribute("Inherit")?.Value == "False" || parent?.Element(list) == null) return mine;
            return parent.Element(list)!.Elements("li").Where(Avail).Concat(mine).ToList();
        }
        IEnumerable<Dictionary<string, string>> Behind(IEnumerable<string> tags, float means) =>
            weapons.Values.Where(w => Tags(w, "weaponTags").Intersect(tags).Any() && (float.IsNaN(Worth(w)) ? Core(w) == false : Worth(w) <= means));
        int checkedKits = 0, sets = 0;
        foreach (var f in factions)
        {
            string fd = Dn(f);
            var ext = Ext(f, "PeopleExtension")!;
            int own = OwnAge(f);
            var span = ext.Element("ariseAges")?.Value?.Split('~').Select(int.Parse).ToArray() ?? new[] { -1, -1 };
            var ages = PeopleAgesMath.Reachable(own, span[0], span[1], limits.TryGetValue(fd, out int lim) ? lim : -1, -1);
            var mine = kindEls.Where(k => k.Attribute("Abstract") == null && kindEls.Any(p => p.Attribute("Name")?.Value == k.Attribute("ParentName")?.Value
                                                                                             && p.Element("defaultFactionDef")?.Value == fd)).ToList();
            foreach (var k in mine)
            {
                string kn = Dn(k);
                bool fighter = (k.Element("requiredWorkTags")?.Value ?? "").Contains("Violent");
                float budget = F((k.Element("weaponMoney")?.Value ?? "0~150").Split('~').Last());
                float means = Math.Max(budget, 150f) * 1.25f;
                var setEls = Ext(k, "PeopleKindExtension")?.Element("ageArms")?.Elements("li").ToList() ?? new List<XElement>();
                foreach (var set in setEls)
                {
                    sets++;
                    var a2 = set.Element("ages")!.Value.Split('~').Select(int.Parse).ToArray();
                    var tags = set.Element("weaponTags")?.Elements("li").ToList() ?? new List<XElement>();
                    var pieces = set.Element("apparel")?.Elements("li").ToList() ?? new List<XElement>();
                    C(a2[0] >= 0 && a2[1] <= 3 && a2[0] <= a2[1] && (tags.Count > 0 || pieces.Count > 0), $"{kn}: a set for the ages {a2[0]} to {a2[1]}, of arms or a kit");
                    foreach (var li in tags)
                        C(!Avail(li) || weapons.Values.Any(w => Tags(w, "weaponTags").Contains(li.Value) && (Req(li) == "" ? Core(w) : w["mod"] == Req(li))),
                            $"{kn}: the weapon tag {li.Value} is the game's or the mod's it names, in the player's active mods");
                    foreach (var li in pieces)
                        C(apparel.TryGetValue(li.Value, out var r) && (Core(r) ? Req(li) == "" : Req(li) == r["mod"]) && !PeopleAgesMath.Later(Tech(r), a2[0]),
                            $"{kn}: its kit's {li.Value} is in the player's active mods, soft on its mod, of no later age than {a2[0]}");
                }
                foreach (int a in ages.Where(a => a != own))
                {
                    checkedKits++;
                    var set = setEls.FirstOrDefault(x =>
                    {
                        var a2 = x.Element("ages")!.Value.Split('~').Select(int.Parse).ToArray();
                        return a2[0] <= a && a <= a2[1] && ((x.Element("weaponTags")?.Elements("li").Any(Avail) ?? false) || (x.Element("apparel")?.Elements("li").Any(Avail) ?? false));
                    });
                    var setTags = set?.Element("weaponTags")?.Elements("li").Where(Avail).Select(li => li.Value).ToList() ?? new List<string>();
                    float setMeans = set?.Element("money") != null ? F(set.Element("money")!.Value) : means;
                    if (setTags.Count > 0 && fighter)
                    {
                        var fit = Behind(setTags, setMeans).Where(w => !PeopleAgesMath.Later(Tech(w), a)).ToList();
                        C(fit.Count > 0, $"{fd}, the age {a}: {kn} is armed by its set with a weapon of the age within its means");
                        if (a >= 2 && Behind(Merged(k, "weaponTags").Select(li => li.Value), means).All(w => w["kind"] == "ranged"))
                            C(fit.Any(w => w["kind"] == "ranged" && Tech(w) >= 4), $"{fd}, the age {a}: {kn}, a fighter at range, carries a gun of the age");
                        continue;
                    }
                    // Its own weapons, and what takes the place of any of a later age (or a fighter's bow in an age of guns).
                    var ownWeps = Behind(Merged(k, "weaponTags").Select(li => li.Value), means).ToList();
                    if (ownWeps.Count == 0) continue;
                    foreach (bool ranged in new[] { true, false })
                    {
                        bool replaced = ownWeps.Any(w => (w["kind"] == "ranged") == ranged
                                                         && (PeopleAgesMath.Later(Tech(w), a) || PeopleAgesMath.BowInGunAge(Tech(w), ranged, fighter, a)));
                        if (!replaced) continue;
                        var fb = Behind(PeopleAgesMath.FallbackArms(a, fighter, ranged), means).Where(w => Core(w) && !PeopleAgesMath.Later(Tech(w), a)).ToList();
                        C(fb.Count > 0 && (!fighter || !ranged || a < 2 || fb.Any(w => w["kind"] == "ranged" && Tech(w) >= 4)),
                            $"{fd}, the age {a}: {kn}'s {(ranged ? "ranged" : "close")} weapons of another age give way to the base game's of the age within its means");
                    }
                }
            }
        }
        C(checkedKits > 120 && sets >= 30, $"every kind of the fifteen in every age it can reach ({checkedKits} kinds in an age, {sets} sets)");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string peoplesCs = Read("Peoples/Peoples.cs"), agesCs = Read("Peoples/PeopleAges.cs"), words = Read("Peoples/PeopleAgeWords.cs"), patches = Read("Peoples/PeopleAgePatches.cs");
        string gifts = Read("Peoples/PeopleGifts.cs");
        C(peoplesCs.Contains("PeopleAges.AgeOf(f, began)") && peoplesCs.Contains("public static TechLevel TechOf(Faction f)"), "Peoples.AgeOf reads the people's age now; TechOf gives Rimfolk's reads its tech level");
        C(Read("Lean/Ages.cs").Contains("Peoples.TechOf(people, TechLevel.Industrial)") && Read("Camps/CampKit.cs").Contains("Peoples.TechOf(f, TechLevel.Undefined)")
          && Read("Couriers/CourierWords.cs").Contains("Peoples.TechOf(f") && Read("Peoples/PeopleTowns.cs").Contains("techLevel = Peoples.TechOf(f)"),
            "words, camps, messengers and town stores by the age a people lives in");
        C(Read("World/Region/RegionBook.cs").Contains("if (PeopleAges.On) Safe(\"the peoples' ages\", () => PeopleAges.Daily(now));") && agesCs.Contains("PeopleAgesMath.LookDay(day, f.loadID)"),
            "once a season each, on the region's day: no clock of its own");
        C(agesCs.Contains("Scribe_Collections.Look(ref ages, \"ages\", LookMode.Deep)") && File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Peoples/PeopleAges.cs:ages\""),
            "the records saved, and modelled in the save estimate");
        C(agesCs.Contains("TradeBook.Instance") && agesCs.Contains("HistoryKinds.OfDeed(\"traded\")") && agesCs.Contains("PeopleAgeGifts.DeedKind") && agesCs.Contains("PeopleAgesMath.LearnerDeeds")
          && agesCs.Contains("Pacts.With(f)") && agesCs.Contains("FactionRelationKind.Ally") && agesCs.Contains("LedgerRuin.Fight") && agesCs.Contains("ws.history"),
            "the rule reads what Rimfolk records: the trade record, the memory, the history, the pacts, the ruins, the world events");
        C(words.Contains("global::ColonyCouncil.Tell.Letter(") && words.Contains("Ages.Form(AgeFeature.Founding") && words.Contains("CardSections.RegisterLines(CardKind.People")
          && Read("UI/Region/PeoplesModel.cs").Contains("PeopleAgeWords.Heading(f)"), "the letter through Tell, by the colony's means; their card and the region's page");
        C(patches.Contains("[HarmonyPatch(typeof(ThingSetMaker_TraderStock), \"Generate\"") && patches.Contains("[HarmonyPatch(typeof(FactionGiftUtility), \"GiveGiftInternal\")]")
          && Regex.Matches(patches, "!RimfolkPatches.On").Count >= 4 && patches.Contains("Finalizer"), "the traders' caps and the gifts by the game's own steps, under the deeper hooks, put back always");
        C(gifts.Contains("AgeArms.ToAge(p, age, AgeArms.LastArmed, AgeArms.LastKit)") && gifts.Contains("PeoplesGearMath.GenesAge(age)") && gifts.Contains("PeopleAgesMath.Later((int)d.techLevel, age)"),
            "every age whole as a pawn is made: nothing of a later age, genes only with the means, a set's arms of the age");
        C(Read("World/Patch_FactionMemory.cs").Contains("PeopleAgesMath.LearnerMissions") && Read("World/FactionMemory.cs").Contains("[\"gaveLearning\"]")
          && Read("World/FactionMemory.cs").Contains("[\"learnedHere\"]"), "the peoples remember the colony's gifts of learning and theirs who learned there");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleAges.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (string r in PeopleAgesMath.Reasons) C(keyed.ContainsKey("CC_PeopleAge_Why_" + r), $"the reason {r} has its words");
        foreach (string r in new[] { "trade", "gifts", "teaching", "ally", "pact", "alone", "burned", "war", "unsupplied" }) C(keyed.ContainsKey("CC_PeopleAge_Letter_" + r), $"the letter says {r}");
        foreach (string c in PeoplesMath.Callings)
            C(keyed.ContainsKey("CC_PeopleAge_Voice_" + c + "_Rise") && keyed.ContainsKey("CC_PeopleAge_Voice_" + c + "_Fall"), $"the {c} speak of a rise and a fall in their own voice");
        C(Enumerable.Range(1, 3).All(a => keyed.ContainsKey("CC_PeopleAge_Rise_" + a)) && Enumerable.Range(0, 3).All(a => keyed.ContainsKey("CC_PeopleAge_Fall_" + a))
          && keyed.ContainsKey("CC_Memory_Deed_gaveLearning") && keyed.ContainsKey("CC_Memory_Deed_learnedHere_Name") && keyed.ContainsKey("CC_Settings_PeopleAges_Tip"),
            "what they learned and lost by the age, the deeds' words, the setting's");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes, no exclamation marks");
        C(Read("CouncilSettings.cs").Contains("Scribe_Values.Look(ref peopleAges, \"peopleAges\", true)") && Read("UI/SettingsCatalog.cs").Contains("Toggle(\"peopleAges\""),
            "the setting, on by default");
        string debug = Read("Peoples/PeopleAgesDebug.cs");
        C(debug.Contains("every people's age and progress (log)") && debug.Contains("a people up an age now (testing)") && debug.Contains("a people down an age now (testing)"),
            "dev actions to see every people's age and to push one up or down");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
