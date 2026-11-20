using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The comms console by radio and the foreign peoples' ages (the player's go, 20 November 2026: "only the ones with radio can
/// answer, and also that foreign peoples change [age] at their pace"; Peoples/ConsoleRadio.cs, ConsoleRadioMath.cs,
/// Peoples/Foreign/ForeignAgesMath.cs, ForeignPeoples.cs; DESIGN.md "The comms console by radio, and the foreign peoples'
/// ages"): the radio rule, one for the console and every call of Rimfolk's; the foreign peoples' records and their seasons
/// by Rimfolk's one rule, played out for the dark elves (the Stormriders) and for the player's own save; the arming cap, and
/// every armed role of the dark elves armed for the industrial age from the player's active mods (docs/player-mods); the
/// setting, the words and the seams.
/// </summary>
public static class ForeignAgesTests
{
    /// <summary>The Stormriders' numbers, for the run's line (and DESIGN.md, the handoff).</summary>
    public static string Summary = "";

    /// <summary>The Stormriders in the player's save (League_of_Toba_5, DESIGN.md "The Stormriders"): Faction_25, an ally.</summary>
    public const int StormridersId = 25;

    private const string Elves = "devdesigner.elves.continued";

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the console and the foreign ages: " + what);
        }

        // ================= The radio rule =================

        C(ConsoleRadioMath.Reaches(4, null) && ConsoleRadioMath.Reaches(5, new float[0]) && ConsoleRadioMath.Reaches(6, null) && ConsoleRadioMath.Reaches(7, null),
            "a people of the industrial age or later has a radio (the Empire's ultra tech level too)");
        C(!ConsoleRadioMath.Reaches(3, null) && !ConsoleRadioMath.Reaches(2, new[] { -1f }) && !ConsoleRadioMath.Reaches(1, null) && !ConsoleRadioMath.Reaches(0, null),
            "tribes, medieval folk and a people of no age the game gives have none");
        C(ConsoleRadioMath.Reaches(3, new[] { -1f, 40f, 12f }) && !ConsoleRadioMath.Reaches(3, new[] { -1f, PostMath.MastReach + 1f }) && ConsoleRadioMath.Reaches(2, new[] { PostMath.MastReach }),
            "a town of theirs within a Signal Line mast's reach carries the call, as the letters' calls have it");
        C(Enumerable.Range(0, 8).All(t => PostMath.RadioReaches(t, -1f) == NotableMath.Callable(t)) && Enumerable.Range(0, 8).All(t => ConsoleRadioMath.Reaches(t, null) == NotableMath.Callable(t)),
            "one rule: the console, the letters' radio and every call of Rimfolk's agree on every age");
        C(Enumerable.Range(0, 4).All(a => ConsoleRadioMath.Reaches(PeopleAgesMath.TechOfAge(a), null) == a >= PeopleAgesMath.Industrial),
            "by the age a people lives in: the industrial and the spacer age have the radio");
        C(ConsoleRadioMath.Greyed(true, true, false, false) && !ConsoleRadioMath.Greyed(true, true, false, true) && !ConsoleRadioMath.Greyed(false, true, false, false)
          && !ConsoleRadioMath.Greyed(true, false, false, false) && !ConsoleRadioMath.Greyed(true, true, true, false),
            "greyed only with the setting and the deeper hooks on, never the colony, never a people with a radio");
        C(ConsoleRadioMath.Key(true) == "CC_ConsoleRadio_Letter" && ConsoleRadioMath.Key(false) == "CC_ConsoleRadio_None", "the reason names the letters while they are on");

        // ================= The foreign peoples' ways =================

        C(ForeignAgesMath.Pace(-1f) == ForeignAgesMath.DefaultPace && ForeignAgesMath.Pace(0f) == ForeignAgesMath.DefaultPace && ForeignAgesMath.Pace(float.NaN) == ForeignAgesMath.DefaultPace
          && ForeignAgesMath.Pace(11f) == 11f, "a pace of its own, else the default");
        C(ForeignAgesMath.RoleArmed(false, false, false) && ForeignAgesMath.RoleArmed(true, true, true) && !ForeignAgesMath.RoleArmed(true, false, false) && !ForeignAgesMath.RoleArmed(true, true, false),
            "a role that carries arms needs its set for the age, and the set has to find its arms");
        var all = new[] { true, true, true, true };
        C(ForeignAgesMath.ArmedLimit(1, 2, all) == 2 && ForeignAgesMath.ArmedLimit(1, 3, all) == 3 && ForeignAgesMath.ArmedLimit(1, -1, all) == 2,
            "armed for every age: its ways' limit (unset, one above its own)");
        C(ForeignAgesMath.ArmedLimit(1, 3, new[] { true, true, false, true }) == 1 && ForeignAgesMath.ArmedLimit(1, 3, new[] { true, true, true, false }) == 2
          && ForeignAgesMath.ArmedLimit(1, 3, new[] { true, true, true }) == 2 && ForeignAgesMath.ArmedLimit(1, 3, null) == 1,
            "it rises no further than an unbroken run of ages its roles are armed for");
        C(ForeignAgesMath.ArmedLimit(3, 3, new[] { false, false, false, false }) == 3 && ForeignAgesMath.ArmedLimit(2, 0, all) == 2,
            "never below the age its mod made it for");
        C(float.IsPositiveInfinity(ForeignAgesMath.SeasonsToRise(12f, 3, 1f, 0f, 1f)) && ForeignAgesMath.SeasonsToRise(10f, 1, 1f, 0f, 1f) == 40f
          && ForeignAgesMath.SeasonsToRise(10f, 1, 1f, 0.5f, 2f) == 10f, "the seasons to rise an age, by its pace, its head start and the colony's help");

        // ================= The dark elves' profile =================

        string file = Path.Combine(root, "Defs", "PeopleDefs", "Rimfolk_ForeignPeoples.xml");
        var d = XDocument.Load(file).Root!.Elements("ColonyCouncil.ForeignPeopleDef").First();
        bool hasWays = d.Element("agePace") != null && d.Element("ageLimit") != null;
        C(hasWays, "the dark elves have a pace and a limit of their own");
        if (!hasWays) return n;
        float pace = F(d.Element("agePace")!.Value);
        int limit = int.Parse(d.Element("ageLimit")!.Value);
        int floor = d.Element("ageFloor") != null ? int.Parse(d.Element("ageFloor")!.Value) : -1;
        const int own = PeopleAgesMath.Medieval;
        var paceComment = d.Element("agePace")!.NodesBeforeSelf().OfType<XComment>().LastOrDefault();
        C(paceComment != null && paceComment.Value.Contains("Their age over the years") && paceComment.Value.Trim().Length > 60, "a line of why beside their pace");
        C(PeopleAgesMath.Sound(pace, limit, own, false, 0f) && pace >= 8f && pace <= 17f, $"a medieval people's pace ({pace} years an age), within the medieval peoples' 8 to 17");
        C(limit == PeopleAgesMath.Industrial && PeopleAgesMath.Floor(floor, own) == PeopleAgesMath.Tribal, "no further than the industrial age, no lower than the tribal");

        // ================= The player's catalogue =================

        string cat = Path.Combine(root, "docs", "player-mods");
        List<Dictionary<string, string>> Table(string f)
        {
            var lines = File.ReadAllLines(Path.Combine(cat, f));
            var head = lines[0].Split('\t');
            return lines.Skip(1).Where(l => l.Length > 0).Select(l =>
            {
                var c = l.Split('\t');
                var row = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
                return row;
            }).Where(r => r["active"] == "yes").ToList();
        }
        var apparel = Table("apparel.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var weapons = Table("weapons.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var kinds = Table("pawnkinds.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var gearWorth = File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")).Where(l => !l.StartsWith("#") && l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[1] == "weapon" && c[5] != "-").ToDictionary(c => c[2], c => F(c[5]));
        int Tech(Dictionary<string, string> r) => r["techLevel"] switch
        {
            "Animal" => 1, "Neolithic" => 2, "Medieval" => 3, "Industrial" => 4, "Spacer" => 5, "Ultra" => 6, "Archotech" => 7, _ => 0
        };
        string[] Tags(Dictionary<string, string> r, string col) => (r[col] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        float Worth(Dictionary<string, string> r) =>
            float.TryParse(r["marketValue"], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0f ? v : gearWorth.TryGetValue(r["defName"], out var g) ? g : float.NaN;
        string Req(XElement e) => (e.Attribute("MayRequire")?.Value ?? "").ToLowerInvariant();
        List<string> Li(XElement? parent) => parent?.Elements("li").Select(e => e.Value.Trim()).ToList() ?? new List<string>();

        // Every armed role (a kind of it given weapons by the Elves mod) armed for every age above its own up to its limit.
        var roles = d.Element("roles")!.Elements("li").ToList();
        var armedAt = new bool[4];
        armedAt[own] = true;
        int checkedSets = 0;
        for (int a = 0; a < 4; a++)
        {
            if (a == own) continue;
            bool every = true;
            foreach (var r in roles)
            {
                string name = r.Element("name")?.Value ?? "?";
                var mine = Li(r.Element("kinds")).Where(kinds.ContainsKey).ToList();
                bool carries = mine.Any(k => Tags(kinds[k], "weaponTags").Length > 0);
                var set = r.Element("ageArms")?.Elements("li").FirstOrDefault(s =>
                {
                    var span = s.Element("ages")!.Value.Split('~').Select(int.Parse).ToArray();
                    return span[0] <= a && a <= span[1];
                });
                bool finds = false;
                if (set != null)
                {
                    var tagEls = set.Element("weaponTags")?.Elements("li").ToList() ?? new List<XElement>();
                    var kit = set.Element("apparel")?.Elements("li").ToList() ?? new List<XElement>();
                    float money = set.Element("money") != null ? F(set.Element("money")!.Value) : 187.5f;
                    var found = weapons.Values.Where(w => Tags(w, "weaponTags").Intersect(tagEls.Select(t => t.Value.Trim())).Any() && !PeopleAgesMath.Later(Tech(w), a)
                                                          && (float.IsNaN(Worth(w)) ? w["mod"] != "ludeon.rimworld" : Worth(w) <= money)).ToList();
                    finds = tagEls.Count > 0 ? found.Count > 0 : kit.Count > 0;
                    if (a > own && a <= limit)
                    {
                        checkedSets++;
                        foreach (var t in tagEls)
                            C(weapons.Values.Any(w => Tags(w, "weaponTags").Contains(t.Value.Trim()) && (Req(t) == "" ? w["mod"] == "ludeon.rimworld" : w["mod"] == Req(t))),
                                $"{name}, the age {a}: the weapon tag {t.Value} is the game's or the mod's it names, in the player's active mods");
                        C(tagEls.Count == 0 || found.Count > 0, $"{name}, the age {a}: its set finds a weapon of the age within its means ({found.Count})");
                        foreach (var k in kit)
                        {
                            bool known = apparel.TryGetValue(k.Value.Trim(), out var piece);
                            C(known && (Req(k) == "" ? piece!["mod"] == "ludeon.rimworld" : piece!["mod"] == Req(k)) && !PeopleAgesMath.Later(Tech(piece), a),
                                $"{name}, the age {a}: its kit's {k.Value} is in the player's active mods, soft on its mod, of no later age");
                            // The kit sits beside the Elves mod's own: it takes the place of none of the kind's pieces.
                            if (known)
                                foreach (string kind in mine)
                                    C(!ForeignKit(kind).Where(apparel.ContainsKey).SelectMany(p => Slots(apparel[p])).Intersect(Slots(piece!)).Any(),
                                        $"{name}, the age {a}: {k.Value} sits beside {kind}'s own kit");
                        }
                        // A fighter at range (every weapon of its kinds ranged) carries a gun of the age.
                        bool ranged = mine.Count > 0 && mine.All(k => Tags(kinds[k], "weaponTags").All(t => weapons.Values.Where(w => Tags(w, "weaponTags").Contains(t)).All(w => w["kind"] == "ranged")));
                        if (carries && ranged && a >= PeopleAgesMath.Industrial)
                            C(found.Count > 0 && found.All(w => w["kind"] == "ranged" && Tech(w) >= 4), $"{name}, the age {a}: a fighter at range carries a gun of the age");
                    }
                }
                bool armed = ForeignAgesMath.RoleArmed(carries, set != null, finds);
                if (a > own && a <= limit) C(armed, $"{name}: armed for the age {a}");
                every &= armed;
            }
            armedAt[a] = every;
        }
        C(checkedSets >= 8, $"every role that carries arms has its set for the industrial age ({checkedSets})");
        C(!roles.Where(r => r.Element("name")?.Value == "trader" || r.Element("name")?.Value == "folk").Any(r => r.Element("ageArms") != null),
            "their traders and ordinary folk carry no arms of their own and need no set");
        C(ForeignAgesMath.ArmedLimit(own, limit, armedAt) == PeopleAgesMath.Industrial, "by the player's mods the dark elves can rise to the industrial age");
        C(!armedAt[PeopleAgesMath.Spacer], "and no set reaches the spacer age, where they can't go");

        // ================= Their seasons, played out =================

        var ways = new AgeWays { pace = ForeignAgesMath.Pace(pace), limit = ForeignAgesMath.ArmedLimit(own, limit, armedAt), floor = PeopleAgesMath.Floor(floor, own) };
        float head = PeopleAgesMath.HeadStart(StormridersId), fate = PeopleAgesMath.Fate(StormridersId);
        int Rise(AgeDeeds deeds, int id, float headStart, out AgeStep last)
        {
            var st = new AgeStep { age = own, progress = headStart };
            int seasons = 0;
            do
            {
                st = PeopleAgesMath.Season(st.age, st.progress, ways, PeopleAgesMath.Fate(id), deeds);
                seasons++;
            } while (st.change == 0 && seasons < 400);
            last = st;
            return seasons;
        }
        var alone = new AgeDeeds { towns = 3 };
        var allied = new AgeDeeds { towns = 3, bond = 1f };
        var trading = new AgeDeeds { towns = 3, bond = 1f, trade = 2500f };
        int sAlone = Rise(alone, StormridersId, head, out var stAlone);
        int sAllied = Rise(allied, StormridersId, head, out var stAllied);
        int sTrading = Rise(trading, StormridersId, head, out var stTrading);
        C(stAlone.change == 1 && stAlone.age == PeopleAgesMath.Industrial && stAllied.age == PeopleAgesMath.Industrial && stTrading.age == PeopleAgesMath.Industrial,
            "the Stormriders rise into the industrial age, by themselves or with the colony's help");
        C(sAlone == (int)ForeignAgesMath.SeasonsToRise(pace, own, fate, head, 1f) && sAllied == (int)ForeignAgesMath.SeasonsToRise(pace, own, fate, head, 1.5f)
          && sTrading == (int)ForeignAgesMath.SeasonsToRise(pace, own, fate, head, 2f), "the seasons to rise worked out agree with the seasons played out");
        C(sAllied < sAlone && sTrading < sAllied, "an alliance quickens them, trade more");
        var settled = PeopleAgesMath.Season(PeopleAgesMath.Industrial, 0f, ways, fate, trading);
        C(settled.change == 0 && settled.age == PeopleAgesMath.Industrial && settled.why == "settled", "at the industrial age they are settled in their ways");
        var cut = new AgeDeeds { yearsAlone = 6f, war = 1f, burned = 1, towns = 1 };
        var fell = new AgeStep { age = own, progress = 0f };
        int sFall = 0;
        do
        {
            fell = PeopleAgesMath.Season(fell.age, fell.progress, ways, fate, cut);
            sFall++;
        } while (fell.change == 0 && sFall < 400);
        C(fell.change == -1 && fell.age == PeopleAgesMath.Tribal && PeopleAgesMath.Season(PeopleAgesMath.Tribal, -0.98f, ways, fate, cut).age == PeopleAgesMath.Tribal,
            $"cut off from the world they fall back to the tribes in a few years ({sFall} seasons), and no lower");
        // Over many worlds (each its own fate and head start), by themselves and as the colony's ally.
        const int Worlds = 400;
        double Share(AgeDeeds deeds, float years)
        {
            int rose = 0;
            for (int w = 0; w < Worlds; w++)
            {
                int id = 1000 + w * 7;
                var st = new AgeStep { age = own, progress = PeopleAgesMath.HeadStart(id) };
                for (int q = 0; q < years * PeopleAgesMath.SeasonsAYear; q++) st = PeopleAgesMath.Season(st.age, st.progress, ways, PeopleAgesMath.Fate(id), deeds);
                if (st.age > own) rose++;
            }
            return rose / (double)Worlds;
        }
        double alone5 = Share(alone, 5f), alone10 = Share(alone, 10f), allied5 = Share(allied, 5f), allied10 = Share(allied, 10f), trading5 = Share(trading, 5f);
        string Pct(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        string Y(int seasons) => (seasons / (float)PeopleAgesMath.SeasonsAYear).ToString("0.#", CultureInfo.InvariantCulture);
        Summary = $"the Stormriders (Faction_{StormridersId}) begin medieval with a head start of {head:0.00} and a fate of x{fate:0.00}, and reach the industrial age in {Y(sAlone)} years by "
                  + $"themselves, {Y(sAllied)} as the colony's ally and {Y(sTrading)} with 2500 silver of trade a year besides; over many worlds the dark elves rise in 5 years in "
                  + $"{Pct(alone5)} by themselves, {Pct(allied5)} allied and {Pct(trading5)} trading, in 10 years in {Pct(alone10)} and {Pct(allied10)}";
        C(alone5 == 0 && alone10 > 0.3 && allied10 > 0.95 && trading5 > 0.5, $"slow by themselves, sooner with the colony's help ({Pct(alone5)}, {Pct(alone10)}, {Pct(allied10)}, {Pct(trading5)})");

        // ================= The setting and the words =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleAges.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (string k in new[] { "CC_Settings_RadioConsole", "CC_Settings_RadioConsole_Tip", "CC_SetNote_RadioConsole", "CC_ConsoleRadio_ShortLetter", "CC_ConsoleRadio_Short", ConsoleRadioMath.Key(true), ConsoleRadioMath.Key(false) })
            C(keyed.ContainsKey(k), $"{k} has its words");
        C(keyed["CC_ConsoleRadio_ShortLetter"] == "no radio: they can only be reached by a letter" && keyed[ConsoleRadioMath.Key(true)].Contains("{PEOPLE}") && keyed[ConsoleRadioMath.Key(true)].Contains("{AGE}")
          && keyed[ConsoleRadioMath.Key(true)].Contains("courier") && keyed[ConsoleRadioMath.Key(true)].Contains("their card"), "the reason says how a letter reaches them: their card, a courier or the post");
        string dn = d.Element("defName")!.Value;
        foreach (string way in new[] { "_Rise", "_Fall" })
        {
            C(keyed.TryGetValue("CC_PeopleAge_Voice_" + dn + way, out var line) && line.StartsWith("Their {LEADER} says") && Regex.Matches(line, @"[.?]").Count == 1,
                $"the dark elves' head speaks of a {way.Trim('_').ToLowerInvariant()} in their own voice, one sentence like the other peoples' heads");
        }
        C(keyed["CC_Settings_PeopleAges_Tip"].Contains("dark elves") && keyed["CC_Settings_PeopleAges_Tip"].Contains("comms console"), "the ages' setting names the other mods' peoples and the console");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes, no exclamation marks");
        C(Read("CouncilSettings.cs").Contains("public bool radioConsole = true;") && Read("CouncilSettings.cs").Contains("Scribe_Values.Look(ref radioConsole, \"radioConsole\", true);"),
            "the setting, on by default");
        var row = SettingsCatalog.Find("radioConsole");
        var ages = SettingsCatalog.Find("peopleAges");
        C(row != null && ages != null && row.page == SettingsPage.WhatsOn && row.section == ages.section && row.needs == "deepHooks"
          && SettingsCatalog.On(SettingsPage.WhatsOn).Select(e => e.id).ToList().IndexOf("radioConsole") == SettingsCatalog.On(SettingsPage.WhatsOn).Select(e => e.id).ToList().IndexOf("peopleAges") + 1,
            "its row right under the peoples' ages, among the colony's world, with the deeper hooks");

        // ================= The seams =================

        string console = Read("Peoples/ConsoleRadio.cs"), reach = Read("Post/PostReach.cs"), peoples = Read("Peoples/Peoples.cs"), agesCs = Read("Peoples/PeopleAges.cs");
        string foreign = Read("Peoples/Foreign/ForeignPeoples.cs"), words = Read("Peoples/PeopleAgeWords.cs"), patches = Read("Peoples/PeopleAgePatches.cs");
        C(console.Contains("[HarmonyPatch(typeof(Faction), nameof(Faction.CommFloatMenuOption))]") && console.Contains("public static void Postfix(") && !console.Contains("Prefix")
          && console.Contains("!RimfolkPatches.On || !ConsoleRadio.On") && console.Contains("PostReach.Callable(f)"),
            "only the console's own call to a people, a postfix under the deeper hooks and the setting, by the one rule");
        C(!console.Contains("TryOpenComms") && !console.Contains("GetCommTargets") && !console.Contains("PassingShip") && console.Contains("option.action = null") && console.Contains("option.tooltip"),
            "ships, quests, permits and dialogs opened any other way are left alone; the option is greyed with its reason, never hidden");
        C(reach.Contains("public static bool Callable(Faction f)") && reach.Contains("ConsoleRadioMath.Reaches(tech, tiles)") && reach.Contains("Peoples.TechOf(f, TechLevel.Industrial)"),
            "the people's reach by the age it lives in now, or a town near the masts");
        foreach (string rel in new[] { "Diplomacy/PactCalls.cs", "Diplomacy/Ransoms.cs", "Diplomacy/FavourAsks.cs", "Diplomacy/Truces.cs" })
            C(Read(rel).Contains("PostReach.Callable(f)") && !Read(rel).Contains("NotableMath.Callable("), $"{rel}: a call by the same rule as the console's");
        C(Read("World/NeighbourLetters.cs").Contains("PostReach.Radio(n.faction") && Read("Camps/CampRaid.cs").Contains("Ages.Of(c.faction) >= WorldAge.Industrial"),
            "the letters' radio and a camp's word over the air agree by the age");
        C(peoples.Contains("!PeopleAges.Kept(f) ? began : PeopleAges.AgeOf(f, began)") && peoples.Contains("if (!PeopleAges.Kept(f)) return f.def.techLevel;"),
            "a foreign people's age and tech level are its record's");
        C(agesCs.Contains("public static bool Kept(Faction f) => f != null && !f.IsPlayer && (Peoples.IsOurs(f) || ForeignPeoples.Has(f));")
          && Regex.Matches(agesCs, @"!Kept\(f\)").Count >= 2 && agesCs.Contains("return ForeignPeoples.Ways(f, start);"),
            "its record made and looked at on the region's day as Rimfolk's own peoples', by its own ways");
        C(patches.Contains("!PeopleAges.Kept(f)) return;") && words.Contains("!PeopleAges.Kept(f)) return null;") && words.Contains("PeopleAges.VoiceKey(f, rise)"),
            "their traders' goods, their card's heading and the letter in their voice follow");
        C(foreign.Contains("if (age != Peoples.BeganIn(p.Faction)) ToAge(p, role, age);") && foreign.Contains("AgeArms.Arm(p, role.ageArms, age)")
          && foreign.Contains("AgeArms.ToAge(p, age, AgeArms.LastArmed, AgeArms.LastKit)") && foreign.Contains("AgeArms.Weapons(set.weaponTags, means)")
          && foreign.Contains("ForeignAgesMath.ArmedLimit(own, d.ageLimit, armed)"), "a pawn of theirs made in another age: its role's arms, the every-age pass; the limit by the arms");
        C(Read("Peoples/PeopleGifts.cs").Contains("var choices = Weapons(set.weaponTags, means);"), "the set's weapons found the same way when arming and when capping");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("another mod's people"), "the save estimate counts their records");
        var foreignDefs = XDocument.Load(file).Root!.Elements("ColonyCouncil.ForeignPeopleDef").Sum(x => x.Element("factions")?.Elements("li").Count() ?? 0);
        C(PeopleAgesMath.MaxRecords >= 23 + 6 + foreignDefs, $"the records kept hold Rimfolk's peoples, those that arise and the foreign ones ({PeopleAgesMath.MaxRecords})");
        return n;
    }

    /// <summary>What each dark elf kind of Elves (Continued) requires (ForeignPeoplesTests' list, the mod's own 1.6 kinds).</summary>
    private static IEnumerable<string> ForeignKit(string kind) => ForeignPeoplesTests.KitOf(kind);

    /// <summary>A piece's (layer, body part group) pairs, as the game tells pieces that can't be worn together.</summary>
    private static IEnumerable<string> Slots(Dictionary<string, string> row)
    {
        var layers = row["layers"].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var groups = row["bodyPartGroups"].Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string l in layers)
            foreach (string g in groups)
                yield return l + "/" + g;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
