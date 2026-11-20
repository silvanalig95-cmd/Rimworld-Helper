using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods' integrations, part 1 (docs/content-update-proposals.md, C1 to C4, C16, C17; the player's go of 20 November
/// 2026): each god's festival kept as its words say, sacred kinds for every god from the player's animals, a god's beast
/// at home, the beast that stays taken for a god's, legendary Pokémon taken for a god's, and the gods, the beasts and the
/// land in the colony's art. The pure rules (OwnFeastsMath, GodBeastsMath, ArtTalesMath), the defs (the gods' sacred
/// kinds, the jobs, moods, lines and tales, the council's question), the words (every key the code builds, the unseen
/// rule) and the seams.
/// </summary>
public static class GodsIntegrationTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods' integrations: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string feasts = Read("Gods/OnMap/OwnFeasts.cs");
        string beasts = Read("Gods/OnMap/GodBeasts.cs");
        string tales = Read("Things/ArtTales.cs");
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        IEnumerable<XElement> All(string tag) => defs.SelectMany(r => r.Elements(tag));
        XElement Def(string tag, string name) => All(tag).FirstOrDefault(e => e.Element("defName")?.Value == name);

        // ================= C1: each god's festival kept as its words say =================

        var eight = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Pip", "Hush", "Undertow", "Selene" };
        C(OwnFeastsMath.Gods.Length == 8 && eight.All(OwnFeastsMath.Keeps) && !OwnFeastsMath.Keeps("Ennu") && !OwnFeastsMath.Keeps("Helios") && !OwnFeastsMath.Keeps(null),
            "the eight festivals that kept only the shared feast play out here (the other six in their own files)");
        C(eight.All(g => OwnFeastsMath.StartHour(g) >= 0 && OwnFeastsMath.StartHour(g) <= OwnFeastsMath.LatestHour(g) && OwnFeastsMath.LatestHour(g) <= 23),
            "each begins at its own hour and no later than its latest");
        C(OwnFeastsMath.StartHour("Hush") >= 17 && OwnFeastsMath.StartHour("Undertow") >= 17 && OwnFeastsMath.StartHour("Selene") >= 19 && OwnFeastsMath.StartHour("Hollin") <= 10,
            "the supper and the nights begin in the evening, the planting in the morning");
        C(OwnFeastsMath.BeginNow("Hollin", 8) && OwnFeastsMath.BeginNow("Hollin", 15) && !OwnFeastsMath.BeginNow("Hollin", 16) && !OwnFeastsMath.BeginNow("Selene", 19),
            "kept late in the day, it begins as soon as it may until its latest hour");
        C(OwnFeastsMath.StillWaiting(10, 10) && OwnFeastsMath.StillWaiting(10, 11) && !OwnFeastsMath.StillWaiting(10, 12), "kept too late, it is held the next day, then let be");
        C(OwnFeastsMath.Saplings(0) == 0 && OwnFeastsMath.Saplings(7) == 7 && OwnFeastsMath.Saplings(300) == OwnFeastsMath.SaplingsMost, "a sapling for each tree felled, a cap on one Greening");
        C(OwnFeastsMath.Share(10, 4).Sum() == 10 && OwnFeastsMath.Share(10, 4).Max() - OwnFeastsMath.Share(10, 4).Min() <= 1 && OwnFeastsMath.Share(2, 4).Count == 2
          && OwnFeastsMath.Share(0, 3).Count == 0, "the saplings shared fairly among the planters");
        C(OwnFeastsMath.Silver(4) == 0 && OwnFeastsMath.Silver(50) == 5 && OwnFeastsMath.Silver(400) == 20 && OwnFeastsMath.Silver(100000) == 40, "the Deep Silver: a twentieth, 5 to 40, none from nearly empty stores");
        C(OwnFeastsMath.StewMeals(9) == 0 && OwnFeastsMath.StewMeals(25) == 2 && OwnFeastsMath.StewMeals(1000) == OwnFeastsMath.StewMost, "the stew from the stores' mushrooms, a pot at most");
        var seats = OwnFeastsMath.SeatOrder(new List<(bool, int, int)> { (false, 30, 1), (false, 70, 2), (true, 20, 3), (false, 64, 4), (true, 50, 5) });
        C(seats.SequenceEqual(new[] { 5, 3, 2, 4, 1 }), "the sick first, then the old, the oldest of each nearest the fire");
        var behind = OwnFeastsMath.Behind(6);
        C(behind.Count == 6 && behind.Distinct().Count() == 6 && behind.All(x => x.back >= 2), "the believers stand in rows behind the one leading the rite, each a place of their own");
        C(OwnFeastsMath.PutBack(true, true) && !OwnFeastsMath.PutBack(true, false) && !OwnFeastsMath.PutBack(false, true), "a lamp or door put back at dawn only if nobody changed it since");
        C(feasts.Contains("Rimfolk_Thought_Feast_") && eight.All(g => Def("ThoughtDef", "Rimfolk_Thought_Feast_" + g) != null), "a small mood for each festival's part");
        foreach (var j in new[] { "Rimfolk_Feast_Plant", "Rimfolk_Feast_Dig", "Rimfolk_Feast_Cook", "Rimfolk_Feast_Machine", "Rimfolk_Feast_Lamp" })
            C(Def("JobDef", j)?.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_FeastWork" && feasts.Contains("\"" + j + "\""), j + " is the feast's own job");
        // Every key the code builds (K("God", "Part")) is in the words.
        var built = Regex.Matches(feasts, @"K\(""([A-Za-z]+)"", ""([A-Za-z]+)""\)").Cast<Match>().Select(m => "CC_Feast_" + m.Groups[1].Value + "_" + m.Groups[2].Value).ToList();
        built.AddRange(Regex.Matches(feasts, @"K\(""([A-Za-z]+)"", [^)]*\? ""([A-Za-z]+)"" : ""([A-Za-z]+)""\)").Cast<Match>()
            .SelectMany(m => new[] { "CC_Feast_" + m.Groups[1].Value + "_" + m.Groups[2].Value, "CC_Feast_" + m.Groups[1].Value + "_" + m.Groups[3].Value }));
        built.AddRange(Regex.Matches(feasts, @"""(CC_Feast_[A-Za-z_]+)""").Cast<Match>().Select(m => m.Groups[1].Value));
        C(built.Count >= 40, "the festivals' words are built from keys (" + built.Distinct().Count() + ")");
        foreach (var k in built.Distinct()) C(keyed.ContainsKey(k), k + " is in the words");
        foreach (var g in eight)
            C(keyed.Keys.Any(k => k.StartsWith("CC_Feast_" + g + "_Book")), g + "'s festival goes in the colony's book");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Feast_") && !kv.Key.Contains("_Book")).All(kv => !kv.Value.Contains("{GOD}")),
            "the festivals' messages never name a god (the calendar's name for the day does)");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Feast_") && kv.Key.Contains("_Book")).Count(kv => Regex.IsMatch(kv.Value, @"\b(asketh|hath|unto|thereby|upon)\b")) >= 6,
            "the book's lines are in the old voice of the gods' texts");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Feast_") || kv.Key.StartsWith("CC_GodBeast_")).All(kv => !kv.Value.Contains("—") && !kv.Value.Contains("!")), "no em dashes or exclamation marks");
        var pool = All("ColonyCouncil.FlavorPoolDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Pool_GodFeast_Say");
        var lines = pool?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var (tag, moment) in new[] { ("hollin", "plant"), ("hollin", "meal"), ("smoulder", "down"), ("smoulder", "empty"), ("bellwether", "herd"), ("bellwether", "alone"),
                     ("bellwether", "story"), ("morel", "dig"), ("morel", "stew"), ("pip", "mended"), ("pip", "machine"), ("pip", "bench"), ("pip", "lamp"), ("pip", "alllit"),
                     ("undertow", "shut"), ("undertow", "lamp"), ("undertow", "alllit"), ("selene", "watch"), ("selene", "dream") })
            C(lines.Any(l => l.StartsWith("[" + tag + "," + moment + "]") || l.StartsWith("[" + tag + "," + moment + ",")), "something is said at " + tag + "'s " + moment);
        C(lines.Where(l => l.Contains("{GOD}")).All(l => l.Contains("named")), "a god named in a festival's line only where the colony is sure (tagged named)");
        C(feasts.Contains("PatrolWatch") || feasts.Contains("patrol = true, untilDawn = true"), "Selene's watch is the night watch's own play-out (its cold the shared budget's)");
        C(Read("Gods/OnMap/OwnHappenings.cs").Contains("OwnFeasts.Begin(Book, god, map)") && Read("Gods/OnMap/OwnHappenings.cs").Contains("OwnFeasts.Hourly(b, now)")
          && Read("Gods/OnMap/OwnHappenings.cs").Contains("OwnFeasts.Step(b, now)") && Read("Gods/OnMap/OwnHappenings.cs").Contains("OwnFeasts.Felled(b)"),
            "the seams: the festival kept, the gods' hour and steps, a tree felled");
        C(Read("Gods/Worship/Festivals.cs").Contains("OwnFeasts.Gather(p, shrine, g,"), "the believers stand behind the priest at the festival's rite");
        C(Read("Gods/OnMap/OwnSeven.cs").Contains("Scribe_Deep.Look(ref feast, \"feast\")"), "the festival under way is saved with the gods' own book");

        // ================= C2: sacred kinds for every god =================

        var gods = All("ColonyCouncil.GodDef").ToDictionary(e => e.Element("key")?.Value ?? "", e => e);
        List<string> Kinds(string key) => gods.TryGetValue(key, out var e) ? e.Element("sacredAnimals")?.Elements("li").Select(l => l.Value.Trim()).ToList() ?? new List<string>() : new List<string>();
        var withKinds = GodLoreMath.Keys.Where(k => Kinds(k).Count > 0).ToList();
        C(withKinds.Count == 14 && Kinds("Pip").Count == 0, "every god but Pip, the god of machines, has sacred kinds of its own");
        C(Kinds("Smoulder").Contains("DD_Drake") && Kinds("Cirrus").Contains("AA_Thunderox") && Kinds("Undertow").Contains("AA_DuskProwler") && Kinds("Hush").Contains("AA_FrostLynx")
          && Kinds("Hollin").Contains("VDE_AwakenedDryad_Woodmaker") && Kinds("Hollin").Contains("Dryad_Basic") && Kinds("Murmur").Contains("ColossusToad") && Kinds("Murmur").Contains("AEXP_Beaver")
          && Kinds("Ducat").Contains("AEXP_Pangolin") && Kinds("Selene").Contains("RG_Owlbeast") && Kinds("Helios").Contains("Peacock") && Kinds("Ennu").Contains("Crow")
          && Kinds("Morel").Contains("AA_FungalHusk") && Kinds("Bellwether").Contains("Thrumbo"),
            "the kinds the proposal names, from the player's animals");
        var everyKind = GodLoreMath.Keys.SelectMany(k => Kinds(k).Select(x => (k, x))).ToList();
        C(everyKind.GroupBy(x => x.x).All(g => g.Count() == 1), "no kind is sacred to two gods");
        var common = new[] { "Deer", "Elk", "Hare", "Squirrel", "Rat", "Boar", "Muffalo", "Cow", "Chicken", "Pig", "Alpaca", "Dromedary", "Horse", "Turkey", "Duck" };
        C(everyKind.All(x => !common.Contains(x.x)), "never the land's common game or the colony's usual livestock");
        foreach (var e in gods.Values)
            foreach (var li in e.Element("sacredAnimals")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string name = li.Value.Trim();
                bool core = name == "Donkey" || name == "Thrumbo" || name == "AlphaThrumbo" || name == "Cat" || name == "Goat" || name.StartsWith("Dryad_") || name == "ColossusToad"
                            || name.StartsWith("AEXP_Cat") || name == "AEXP_Lion" || name == "AA_SandLion";
                if (!core) C(li.Attribute("MayRequire") != null, name + " is soft (MayRequire)");
            }
        string kindsCode = Read("Gods/SacredKinds.cs");
        C(kindsCode.Contains("own.Count > 0 ? own : g.DomainDef?.Animals"), "a god's own kinds first, else its domain's");
        C(Read("Gods/GodDeeds.cs").Contains("SacredKinds.Killed(victim, map, slaughtered, wild, who)") && Read("Gods/GodDeeds.cs").Contains("SacredKinds.Own(g)"),
            "a sacred beast killed offends the god whose own kind it is, and a domain's only gods with none of their own");
        C(Read("Gods/World/GodHealth.cs").Contains("SacredKinds.GodsOf(victim.def)"), "the god whose own kind it is holds it against the killer");
        C(Read("Gods/Worship/Vows.cs").Contains("SacredKinds.Of(g)") && Read("Gods/Worship/GodDreams.cs").Contains("SacredKinds.Of(g)"), "the vow to spare a beast and the dream's warning by the god's own kinds");
        C(Read("Gods/Presence/GodMarkGoods.cs").Contains("!SacredKinds.AnySacred(k.race)"), "the god's mark is never one of its sacred beasts");

        // ================= C3, C4, C16: the gods' beasts =================

        C(GodBeastsMath.Legends.Count == 24 && GodBeastsMath.Legends.Values.Distinct().Count() == 10 && GodBeastsMath.Legends.Values.All(GodLoreMath.Keys.Contains),
            "the legendaries of PokéWorld each taken for one of ten gods (the proposal's table)");
        C(GodBeastsMath.LegendGod("PW_HoOh") == "Helios" && GodBeastsMath.LegendGod("PW_Spiritomb") == "Mog" && GodBeastsMath.LegendGod("PW_Meowth") == "Ducat" && GodBeastsMath.LegendGod("PW_Pikachu") == null,
            "who is taken for whose");
        C(GodBeastsMath.LegendDue(-1, 100) && !GodBeastsMath.LegendDue(0, 10 * 60000) && GodBeastsMath.LegendDue(0, GodBeastsMath.LegendGapDays * 60000), "the same kind taken for a god's once in a while");
        C(GodBeastsMath.BiteDue(-999999, 100, 0f) && !GodBeastsMath.BiteDue(0, 60000, 0f) && !GodBeastsMath.BiteDue(-999999, 100, 0.99f), "a bite by hand now and then, days apart");
        C(GodBeastsMath.FestivalOmen(2, 3) && !GodBeastsMath.FestivalOmen(3, 3), "a god's beast born on its festival is an omen while there is room among the living");
        var rolls = Enumerable.Range(1, 400).Select(i => GodBeastsMath.Roll(1000 + i, 7 * i)).ToList();
        C(GodBeastsMath.Roll(5, 9) == GodBeastsMath.Roll(5, 9) && rolls.All(r => r >= 0f && r < 1f) && rolls.Count(r => r < GodLoreMath.MistakeChance) > 40,
            "each believer takes a beast at home the same way every time, and some of them wrongly");
        C(beasts.Contains("GodLoreMath.Attribute(truth.Key, KnownOf(") && beasts.Contains("PawnGods.NameOf("), "a god in their words is their own name for the one they take it for, perhaps wrongly");
        C(beasts.Contains("OwnFeasts.Holds(p, g)") && beasts.Contains("\"greet\"") && beasts.Contains("\"bite\"") && beasts.Contains("\"festivalbite\"") && beasts.Contains("\"shrine\""),
            "those who hold the god they take it for greet it, bring it a bite, and on the festival feed it first and walk it to the god's place");
        C(Read("Animals/OmenAnimals.cs").Contains("GodBeasts.FestivalBirth(book, a, map, now)") && beasts.Contains("NamesPool"), "one born on its god's festival day an omen named in the god's own words");
        C(beasts.Contains("GoneThought") && beasts.Contains("GodLore.Believes(writer, written"), "its death grieved a little by those who hold the god, a believer's verse in the book");
        C(Read("Happenings/Happenings.cs").Contains("GodBeasts.Died(victim, dinfo, note.map)"), "the death hook");
        string rb = Read("Animals/ResidentBeast.cs");
        C(rb.Contains("GodBeasts.Resident(this, a, map)") && rb.Contains("SacredDilemma") && rb.Contains("GodBeasts.ResidentStayed(this, a, HomeMap)")
          && rb.Contains("Scribe_Values.Look(ref takenGod, \"takenGod\", -1)"),
            "the beast that stays: taken for a god's when named, the believer's question, a season kept in the book, saved");
        var q = Def("ColonyCouncil.DilemmaDef", "Council_DlmA_ResidentBeastSacred");
        var opts = q?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        C(q != null && q.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_ResidentBeastSacred" && q.Element("defaultOption")?.Value == "0" && opts.Count == 6
          && opts[0].Descendants("li").Any(e => e.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SparedForGod")
          && opts[0].Descendants("li").Any(e => e.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Vigil"),
            "the council's question has the believer's answer first, played out on the map, and the default");
        C(q?.Element("texts")?.Elements("li").All(t => t.Value.Contains("{GOD}") || t.Value.StartsWith("[asked")) == true && q.Element("texts").Elements("li").Count() >= 4,
            "its words give the believer's own name for the god");
        C(Read("Gods/GodHands.cs").Contains("public static bool ShowWith(") && beasts.Contains("GodHands.ShowWith(map, like, HandKind.Sign"), "a legendary on the land is kept as a god's hand");
        C(beasts.Contains("Slain(victim, map, Gods.ById(legend.god)") && beasts.Contains("SlainThought"), "hunting one is the harm of a sacred beast for those who believe it");
        var bpool = All("ColonyCouncil.FlavorPoolDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Pool_GodBeast_Say")?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var moment in new[] { "greet", "bite", "festivalbite", "shrine", "gone", "resident", "legend", "legendunnamed", "legendscoff" })
            C(bpool.Count(l => l.StartsWith("[" + moment + "]") || l.StartsWith("[" + moment + ",")) >= 2, "something is said at a god's beast's " + moment);
        C(bpool.Where(l => l.StartsWith("[legendunnamed]") || l.StartsWith("[legendscoff]")).All(l => !l.Contains("{GOD}")), "a believer who can name no god, and a scoffer, name none");
        var names = All("ColonyCouncil.FlavorPoolDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Pool_GodBeast_Names")?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        C(GodLoreMath.Keys.All(k => names.Count(l => l.StartsWith("[god:" + k.ToLowerInvariant() + "]")) >= 2), "names in each god's own words");
        foreach (var k in Regex.Matches(beasts + Read("Animals/ResidentBeast.cs"), @"""(CC_GodBeast_[A-Za-z_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(keyed.ContainsKey(k) || k.EndsWith("_"), k + " is in the words");
        C(keyed.ContainsKey("CC_GodBeast_Book_Slain_legend") && keyed.ContainsKey("CC_GodBeast_Book_Slain_resident"), "both deaths have their verse");

        // ================= C17: the colony's art =================

        C(ArtTalesMath.Kinds.Length == 8 && ArtTalesMath.Kinds.Distinct().Count() == 8, "eight of Rimfolk's moments are tales of the game's");
        C(ArtTalesMath.Due("Rimfolk_Tale_GodSeen", -1, 0) && !ArtTalesMath.Due("Rimfolk_Tale_GodSeen", 0, 60000) && ArtTalesMath.Due("Rimfolk_Tale_FestivalKept", 0, 1),
            "a kind comes at most once in a few days, a festival every time");
        C(ArtTalesMath.Held(new[] { "Held" }) && !ArtTalesMath.Held(new[] { "Held", "Leak" }) && !ArtTalesMath.Held(new[] { "Leak" }) && !ArtTalesMath.Held(null), "a raid held at a line: through the gate, nobody fell");
        var withOther = new[] { "Rimfolk_Tale_FestivalBeast", "Rimfolk_Tale_BeastNamed", "Rimfolk_Tale_HerdLeaderDied" };
        foreach (var kind in ArtTalesMath.Kinds)
        {
            var t = Def("TaleDef", kind);
            var rules = t?.Element("rulePack")?.Element("rulesStrings")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
            C(t != null && t.Element("taleClass")?.Value == "ColonyCouncil.Tale_Rimfolk" && t.Element("usableForArt")?.Value == "true", kind + " is a tale the art can show");
            C(new[] { "tale_noun->", "image->", "circumstance_phrase->", "desc_sentence->" }.All(p => rules.Any(r => r.StartsWith(p))), kind + " has the art's four parts");
            C(rules.Where(r => r.StartsWith("image->")).All(r => r.Contains("[circumstance_group]")), kind + "'s images take the game's circumstances");
            var symbols = rules.SelectMany(r => Regex.Matches(r, @"\[([A-Za-z_]+)\]").Cast<Match>().Select(m => m.Groups[1].Value)).Distinct().ToList();
            C(symbols.All(s => s == "circumstance_group" || s == "GOD" || s == "WHAT" || s.StartsWith("PAWN_") || s.StartsWith("OTHER_") && withOther.Contains(kind)),
                kind + " uses only what its tale gives (" + string.Join(", ", symbols) + ")");
            C(tales.Contains("\"" + kind + "\""), kind + " is recorded by the code");
        }
        C(tales.Contains("Gods.CardName(g, map) ?? g.def?.unheard"), "a god in the art by the colony's name where it knew it was that god, else as what was seen");
        C(Read("Gods/Gods.cs").Contains("ArtTales.Seen(spec.god, spec.map, witnesses, spec.kind)") && Read("Gods/OnMap/SacredAnimal.cs").Contains("ArtTales.SacredBeast")
          && Read("Gods/Worship/Offerings.cs").Contains("ArtTales.OfferingTaken") && Read("Gods/Worship/Festivals.cs").Contains("ArtTales.Festival")
          && rb.Contains("ArtTales.BeastNamed") && Read("Animals/HerdStory.cs").Contains("ArtTales.HerdLeader") && Read("Defense/DefenseLinesWatch.cs").Contains("ArtTales.LineHeld"),
            "the seams: a god seen, a sacred beast's visit, an offering taken, a festival kept, the beast named, the old leader's death, a line held");

        // ================= The balance model, the dev actions =================

        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        foreach (var t in eight.Select(g => "Rimfolk_Thought_Feast_" + g).Concat(new[] { "Rimfolk_Thought_GodBeastGone", "Rimfolk_Thought_SacredBeastSlain", "Rimfolk_Thought_BeastSpared" }))
            C(Def("ThoughtDef", t) != null && rates.Contains("(\"" + t + "\""), t + " exists and is in the balance model");
        string dev = Read("Gods/GodsIntegrationDebug.cs");
        C(new[] { "a festival kept now", "next part", "the believers behind", "a god's beast at home", "the beast that stays", "a legendary on the land", "each of Rimfolk's tales" }.All(dev.Contains),
            "a dev action for each");
        return n;
    }
}
