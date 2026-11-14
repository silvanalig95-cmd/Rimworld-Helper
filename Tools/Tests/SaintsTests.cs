using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The saints (the player, 14 November 2026: "perhaps instead of these being the gods that are currently registered to an
/// ideology, this could, for example, be some sort of saints or important characters ... kind of saints or holy men or unholy
/// men or whatever", then "let's go with saints then"; docs/gods-next-proposals.md, section F, option b, round 5): a faith's
/// deities read as its saints or its damned, each tied to a god by its own words; a fallen colonist the council may name a
/// saint, never their kin; the saints' places, their day, their relic; the words by age and culture; the ideoligion never
/// changed; the unseen gods; saved and small.
/// </summary>
public static class SaintsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the saints: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keys = new HashSet<string>(Regex.Matches(keyedAll, @"<([A-Za-z0-9_]+)>").Cast<Match>().Select(m => m.Groups[1].Value));
        string Key(string k) => Regex.Match(keyedAll, "<" + k + @">([^<]*)<").Groups[1].Value;

        // ================= A death that merits a saint =================

        var rescuer = new SaintDeath { ours = true, days = 5, byEnemy = true, rescuing = true };
        C(SaintsMath.Merit(rescuer, out float s1) == SaintReason.Rescuer && s1 >= SaintsMath.MeritFrom, "died carrying the wounded out under attack: a saint's death on its own, even new to the colony");
        var defender = new SaintDeath { ours = true, days = 70, byEnemy = true, drafted = true };
        C(SaintsMath.Merit(defender, out _) == SaintReason.Defender, "held the line, a year with the colony");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 30, byEnemy = true, drafted = true }, out _) == SaintReason.None, "a fighter who fell new to the colony: remembered, not a saint");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 30, byEnemy = true, drafted = true, vow = true }, out _) == SaintReason.Defender, "a vow kept to the end counts beside a deed");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 61, medicine = SaintsMath.HealerSkill, tended = SaintsMath.HealerTended }, out _) == SaintReason.Healer,
            "the healer's years");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 61, medicine = SaintsMath.HealerSkill - 1, tended = 500 }, out _) == SaintReason.None
          && SaintsMath.Merit(new SaintDeath { ours = true, days = 61, medicine = SaintsMath.HealerPassionSkill, medicinePassion = true, tended = SaintsMath.HealerPassionTended }, out _) == SaintReason.Healer,
            "a healer by skill and many tended, or by a passion and many more");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = SaintsMath.KeeperDays, devoted = true }, out _) == SaintReason.Keeper
          && SaintsMath.Merit(new SaintDeath { ours = true, days = SaintsMath.KeeperDays - 1, devoted = true }, out _) == SaintReason.None,
            "a keeper of a god's ways for two years and more");
        var priestly = SaintsMath.Merit(new SaintDeath { ours = true, days = 200, devoted = true, priest = true }, out float sp);
        SaintsMath.Merit(new SaintDeath { ours = true, days = 200, devoted = true }, out float sd);
        C(priestly == SaintReason.Keeper && sp > sd, "the devoted priest counts more than the devoted alone");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 500, vow = true, godNear = true }, out _) == SaintReason.None, "a vow alone is not enough");
        C(SaintsMath.Merit(new SaintDeath { ours = true, days = 500 }, out _) == SaintReason.None, "a long life alone is not a saint's");
        C(SaintsMath.Merit(new SaintDeath { ours = false, days = 500, byEnemy = true, rescuing = true }, out _) == SaintReason.None, "never a guest, a prisoner, a slave or a child");
        C(SaintsMath.Merit(new SaintDeath { ours = true, scoffer = true, days = 500, byEnemy = true, rescuing = true }, out _) == SaintReason.None, "never one who doubted them all");
        C(SaintsMath.Merit(new SaintDeath { ours = true, hunger = true, days = 500, byEnemy = true, rescuing = true }, out _) == SaintReason.None, "never one who held the Hunger first");
        C(SaintsMath.ReasonDomain(SaintReason.Healer) == GodDomain.Healing && SaintsMath.ReasonDomain(SaintReason.Defender) == GodDomain.War
          && SaintsMath.ReasonDomain(SaintReason.Rescuer) == GodDomain.Hearth && SaintsMath.ReasonDomain(SaintReason.Keeper) == GodDomain.None, "the god of one who held none: the one their deed speaks of");
        C(SaintsMath.CandidateDays >= 7 && SaintsMath.CandidateDays <= 15 && SaintsMath.MaxWaiting <= 6, "the council is asked soon after, or not at all");

        // ================= A faith's deity read as a saint =================

        C(SaintsMath.Damned("Raider", "raider", "Vorgath", "god of the hunt", GodDomain.Hunt), "a deity of a dark meme is one of the damned");
        C(SaintsMath.Damned(null, null, "Mog-Rath", "the devourer of stars", GodDomain.Night) && SaintsMath.Damned(null, null, "Ys", "", GodDomain.Void),
            "dark words or the void make one of the damned");
        C(!SaintsMath.Damned("NaturePrimacy", "nature primacy", "Aranea", "goddess of the hunt", GodDomain.Hunt), "a deity of the hunt is a saint");
        C(SaintsMath.Damned(null, null, "Ys", "the bloodied one", GodDomain.War) && !SaintsMath.Damned(null, null, "Ys", "the unraided one", GodDomain.War),
            "a dark word counts at a word's start (bloodied), not inside one");
        C(!SaintsMath.Damned(null, null, "Aranea", "goddess of the harvest", GodDomain.Harvest), "a goddess of the harvest is a saint");
        foreach (var m in SaintsMath.DarkMemes)
        {
            var row = PoolRow(root, m);
            C(row != null, $"the dark meme {m} is in the player's mod pool, active");
        }

        var godDefs = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Gods.xml")).Root!.Elements("ColonyCouncil.GodDef").ToList();
        var gods = godDefs.Select(g => (key: ((string?)g.Element("defName") ?? "").Replace("Rimfolk_God_", ""),
                d: Enum.TryParse<GodDomain>((string?)g.Element("domain"), out var d) ? d : GodDomain.None,
                s: Enum.TryParse<GodDomain>((string?)g.Element("second"), out var s) ? s : GodDomain.None,
                hunger: (string?)g.Element("kind") == "Hunger")).ToList();
        C(gods.Count == 14 && gods.Count(g => g.hunger) == 1, "the fourteen gods read from their defs, one of them the Hunger");
        string For(GodDomain a, GodDomain b, bool damned, string name = "Aranea") => SaintsMath.GodFor(a, b, damned, gods, SaintsMath.Seed(name));
        C(For(GodDomain.Hunt, GodDomain.None, false) == "Bellwether", "a saint of the hunt is the first thrumbo's");
        C(For(GodDomain.Wild, GodDomain.None, false) == "Hollin" && For(GodDomain.Machine, GodDomain.None, false) == "Pip" && For(GodDomain.Storm, GodDomain.None, false) == "Cirrus"
          && For(GodDomain.Waters, GodDomain.None, false) == "Murmur", "the wild's, the machine's, the storm's and the waters' saints to their own gods");
        C(For(GodDomain.War, GodDomain.None, false) == "Smoulder", "war, which no god holds first, to the nearest (the coil under the mountain)");
        C(For(GodDomain.Healing, GodDomain.None, false) == "Selene", "healing to the one who holds it second");
        var harvest = Enumerable.Range(0, 40).Select(i => For(GodDomain.Harvest, GodDomain.None, false, "Saint" + i)).Distinct().ToList();
        C(harvest.Count >= 2 && harvest.All(k => gods.Any(g => g.key == k && g.s == GodDomain.Harvest)), "the harvest's saints spread among the gods who hold it, by name");
        C(For(GodDomain.Harvest, GodDomain.None, false, "Aranea") == For(GodDomain.Harvest, GodDomain.None, false, "Aranea"), "the same deity keeps its god");
        C(Enumerable.Range(0, 40).All(i => For((GodDomain)(1 + i % 14), GodDomain.Void, false, "x" + i) != "Undertow"), "a saint is never tied to the Hunger");
        C(For(GodDomain.Void, GodDomain.None, true) == "Undertow" && For(GodDomain.Night, GodDomain.None, true) == "Hush" && For(GodDomain.Craft, GodDomain.None, true) == "Smoulder",
            "the damned to the Hunger or the darker gods (the long white, the coil)");
        C(For(GodDomain.Harvest, GodDomain.None, true) == "Undertow" && For(GodDomain.None, GodDomain.None, true) == "Undertow", "the damned nothing else holds: the Hunger");
        C(Enumerable.Range(0, 40).All(i => SaintsMath.DarkGods.Contains(For((GodDomain)(1 + i % 14), GodDomain.None, true, "d" + i) ?? "")), "the damned only ever to the dark gods");
        C(SaintsMath.GodFor(GodDomain.Hunt, GodDomain.None, false, new List<(string, GodDomain, GodDomain, bool)>(), 1) == null, "no gods, no tie");
        C(For(GodDomain.None, GodDomain.None, false) == null, "a saint with nothing to read: no god named");

        // ================= Words by age and culture =================

        C(SaintsMath.WordSet(0, null) == "Ancestors" && SaintsMath.WordSet(1, null) == "Saints" && SaintsMath.WordSet(2, null) == "Saints" && SaintsMath.WordSet(3, null) == "Exemplars",
            "every age its word: the tribes' ancestors, saints, the spacers' exemplars");
        C(SaintsMath.WordSet(1, "Corunan") == "Venerated" && SaintsMath.WordSet(3, "Kriminul") == "Martyrs" && SaintsMath.WordSet(1, "Rustican") == "Saints", "a culture's own word where it has one");
        C(SaintsMath.DamnedSet(0) == "Forsaken" && SaintsMath.DamnedSet(1) == "Damned" && SaintsMath.DamnedSet(3) == "Fallen", "the damned's word by age");
        C(SaintsMath.HeaderSet(1, null, 0, 2) == "Damned" && SaintsMath.HeaderSet(1, null, 1, 2) == "Saints", "the screen says damned only where every one is");
        C(SaintsMath.AgeOfTech(2) == 0 && SaintsMath.AgeOfTech(3) == 1 && SaintsMath.AgeOfTech(4) == 2 && SaintsMath.AgeOfTech(5) == 3, "an age from a tech level");
        foreach (var set in SaintsMath.Sets)
        {
            C(keys.Contains("CC_Saint_Title_" + set) && Key("CC_Saint_Title_" + set).Contains("{NAME}"), $"a title in the {set} word");
            C(keys.Contains("CC_Saint_Word_" + set), $"a header word for {set}");
        }
        foreach (var set in new[] { "Ancestors", "Saints", "Exemplars", "Venerated", "Martyrs" })
            C(keys.Contains("CC_Saint_A_" + set) && Key("CC_Saint_AOf_" + set).Contains("{GOD}"), $"one of the {set}, and one of them of a god");
        C(Key("CC_Saint_Title_Saints") == "Saint {NAME}" && Key("CC_Saint_Word_Saints") == "Saints", "the player's word: saints");
        foreach (SaintReason r in Enum.GetValues(typeof(SaintReason)))
            C(keys.Contains("CC_Saint_Deed_" + r) && Key("CC_Saint_Deed_" + r).StartsWith("who "), $"the deed of a {r}, as a phrase");
        for (int a = 0; a < 4; a++) C(keys.Contains("CC_Saint_RelicForm_" + a), $"the relic's form in age {a}");

        // ================= What a saint gives their god =================

        var lean = SaintsMath.WithSaints(new Dictionary<string, float> { ["Hollin"] = 0.8f, ["Morel"] = 0.4f }, new[] { "Hollin", "Bellwether", "Bellwether" });
        C(Math.Abs(lean["Hollin"] - 1f) < 0.001f && Math.Abs(lean["Bellwether"] - SaintsMath.SaintLean) < 0.001f && Math.Abs(lean["Morel"] - 0.4f) < 0.001f,
            "a faith's saint leans its believers toward the saint's god a little more, never past the strongest");
        C(SaintsMath.WithSaints(null, null).Count == 0, "no faith, no lean");
        C(SaintsMath.SaintLean > 0f && SaintsMath.SaintLean < 0.5f, "a little more, not much");
        C(SaintsMath.DamnedDread >= 20f && SaintsMath.DamnedDread < PawnGodsMath.DreadFrom, "the damned leave their believers uneasy, not in dread");

        // ================= The day, prayers, statues =================

        C(SaintsMath.FeastDue(12, 18, 5502, 12, 5501, 5501) && !SaintsMath.FeastDue(12, 18, 5501, 12, 5501, -1) && !SaintsMath.FeastDue(12, 18, 5502, 12, 5501, 5502)
          && !SaintsMath.FeastDue(13, 18, 5502, 12, 5501, -1) && !SaintsMath.FeastDue(12, 10, 5502, 12, 5501, -1),
            "a saint's day: the day they died, from the year after, once a year, in the evening");
        C(SaintsMath.PrayChance(0) == 0f && SaintsMath.PrayChance(1) > 0f && SaintsMath.PrayChance(30) <= 0.2f && SaintsMath.PrayChance(3) > SaintsMath.PrayChance(1),
            "prayers at the saints' places: more with more places, never much");
        foreach (var st in new[] { "SculptureSmall", "SculptureLarge", "SculptureGrand", "Statue", "VFE_StatueLarge", "AM_Sphynx", "AM_GrayStatueDecorative", "VME_Statue" })
            C(SaintsMath.IsStatue(st, st.StartsWith("Sculpture")), $"{st} can be raised to a saint");
        foreach (var st in new[] { "BiosculpterPod", "TableSculpting", "VoidSculpture", "GrayStatueTeleporter", "GrayStatueDeadlifeDust", "SculptureTerror", "StoneCubeSculpture", "UnfinishedSculpture", "Bed" })
            C(!SaintsMath.IsStatue(st, true), $"{st} is not a saint's statue");
        foreach (var st in SaintsMath.Statues)
            C(PoolRow(root, st, "ThingDef") != null, $"the statue {st} is in the player's mod pool, active");

        // ================= The defs =================

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement? Def(string type, string name) => defs.SelectMany(d => d.Root!.Elements(type)).FirstOrDefault(e => (string?)e.Element("defName") == name);
        var dlm = Def("ColonyCouncil.DilemmaDef", "Rimfolk_Dlm_Saint");
        C(dlm != null && (string?)dlm.Element("workerClass") == "ColonyCouncil.DilemmaWorker_Saint" && (string?)dlm.Element("timely") == "true", "the council's card, asked in its moment");
        var options = dlm?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        C(options.Count == 3 && options[0].Descendants("li").Any(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_NameSaint")
          && options[0].Descendants("li").Any(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_SaintWords")
          && options[1].Descendants("li").Any(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_SaintWords"),
            "named a saint or honoured: words said at the grave, on the map");
        C((string?)dlm?.Element("defaultOption") == "2" && !options[2].Descendants("li").Any(e => ((string?)e.Attribute("Class") ?? "").Contains("Saint")), "unanswered, the dead rest as they were");
        string dlmText = dlm?.ToString() ?? "";
        C(!Regex.IsMatch(dlmText, @"\b(family|kin|son|daughter|heir|bloodline|dynasty|children)\b", RegexOptions.IgnoreCase), "the card never speaks of kin or a line of descent");
        foreach (var t in new[] { "Rimfolk_Saint_Named", "Rimfolk_Saint_Prayed", "Rimfolk_Saint_PrayedRelic", "Rimfolk_Saint_FeastKept" })
        {
            var th = Def("ThoughtDef", t);
            float mood = float.Parse((string?)th?.Descendants("baseMoodEffect").FirstOrDefault() ?? "99", System.Globalization.CultureInfo.InvariantCulture);
            C(th != null && mood > 0f && mood <= 3f, $"{t}: a small good mood");
            C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs")).Contains("(\"" + t + "\""), $"{t} in the balance model");
            C(Read("Gods/Saints/Saints.cs").Contains("public static ThoughtDef " + t + ";"), $"{t} reached from the code");
        }
        foreach (var j in new[] { "Rimfolk_Saint_Pray", "Rimfolk_Saint_Raise" })
            C((string?)Def("JobDef", j)?.Element("driverClass") == "ColonyCouncil.JobDriver_SaintPlace", $"{j} walks there and does it on the map");
        var relic = Def("ThingDef", "Rimfolk_SaintRelic");
        C(relic != null && relic.ToString().Contains("ColonyCouncil.CompSaintRelic") && (string?)relic.Element("tradeability") == "None"
          && File.Exists(Path.Combine(root, "Textures", (string?)relic.Element("graphicData")?.Element("texPath") + ".png")), "the relic: its comp, its picture, never sold");
        foreach (var pool in new[] { "Rimfolk_Pool_Saint_Prayer", "Rimfolk_Pool_Saint_Feast", "Rimfolk_Pool_Saint_Raise", "Rimfolk_Pool_Saint_Words", "Rimfolk_Pool_Saint_Remembered" })
        {
            var el = Def("ColonyCouncil.FlavorPoolDef", pool);
            var lines = el?.Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList() ?? new List<string>();
            C(lines.Count >= 4, $"{pool} has lines");
            foreach (var l in lines)
            {
                C(l.Length <= 90 && Regex.Matches(l.TrimEnd('.', '?'), @"[.?!]\s").Count == 0, $"{pool}: '{l}' is one short sentence, said over a head");
                C(!l.Contains("{GOD}") || el!.Descendants("li").Any(x => x.Value.StartsWith("[known]") && x.Value.EndsWith(l)), $"{pool}: '{l}' names the god only where the colony knows it");
            }
        }

        // ================= The seams, and the rules =================

        string saints = Read("Gods/Saints/Saints.cs"), places = Read("Gods/Saints/SaintsPlaces.cs"), patches = Read("Gods/Saints/SaintsPatches.cs"),
            council = Read("Gods/Saints/SaintsCouncil.cs"), book = Read("Gods/Saints/SaintsBook.cs");
        string all = saints + places + patches + council + book + Read("Gods/Saints/SaintsMath.cs");
        C(!Regex.IsMatch(all, @"SetDeities|GenerateDeities|DeitiesListForReading\.(Add|Remove|Clear)|\.deities\b|ideo\.name\s*=|deity\.(name|type)\s*="), "the ideoligion is only ever read, never changed");
        C(!Regex.IsMatch(all, @"FamilyByBlood|Family\.Of|Family\.Relation|GetDirectRelation|DirectRelationExists|relations\.|DilemmaTarget\.(FamilyOfA|CloseToA)"),
            "nothing reads kin: a person honoured, never their family");
        C(saints.Contains("public static string OfGod(God g) => g != null && Gods.Known(g)"), "a saint's god is named only where the colony knows it");
        C(Read("Gods/Worship/Worship.cs").Contains("Saints.Hourly(map, now)") && Read("Gods/Worship/Worship.cs").Contains("Saints.Daily(now)"), "the saints keep worship's hour and day (no hour of their own)");
        C(Read("Gods/Belief/PawnGods.cs").Contains("Saints.Lean(p, s);"), "a faith's saints lean its believers (round 3's draw)");
        C(Read("Letters/LetterFormShapes.cs").Contains("Saints.SermonName(ideo) ?? ideo.KeyDeityName"), "a sermon names the faith's saint, else the game's own name");
        C(Read("Gods/GodLorePage.cs").Contains("y = SaintsPart(x, y, w, p);") && Read("Gods/GodLorePage.cs").Contains("Saints.BookLines(g)"), "the book's chapter keeps its saints");
        C(patches.Contains("[HarmonyPatch(typeof(IdeoFoundation_Deity), nameof(IdeoFoundation_Deity.DoInfo))]") && patches.Contains("public static bool Prepare()")
          && patches.Contains("public static void Finalizer(") && patches.Contains("r.value = was;"), "the ideoligion screen's word swapped while it draws, put back after, standing down safely");
        C(patches.Contains("Current.ProgramState != ProgramState.Playing"), "the line of gods under the deities only in play");
        C(places.Contains("(\"RimWorld.CompStatue\", \"name\")"), "Odyssey's statue of a person known as the saint's likeness, a seam for Pawns on Display");
        C(places.Contains("!v.damned") && places.Contains("Saints.Of(ideo).Where(x => !x.damned)"), "nobody prays to the damned, and no statue is raised to one");
        C(saints.Contains("x.kind != GodKind.Dead") || saints.Contains("g.kind != GodKind.Dead"), "never a dead god");
        C(saints.Contains("g.kind == GodKind.Hunger") && saints.Contains("Worship.Scoffer(x)"), "never the Hunger's saint; scoffers don't share the mood");
        C(Read("Gods/Saints/SaintsCouncil.cs").Contains("Scribe_Deep.Look(ref dead, \"dead\")"), "the fallen kept with the card while it waits");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var f in new[] { "saints", "waiting", "places", "seen", "feared" })
            C(est.Contains("\"Gods/Saints/SaintsBook.cs:" + f + "\""), $"the book's {f} modelled in the save estimate");
        C(Regex.IsMatch(book, @"while \(saints\.Count > SaintsMath\.MaxSaints\)") && Regex.IsMatch(book, @"while \(feared\.Count > SaintsMath\.MaxFeared\)"), "each kept within its bound");
        C(File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py")).Contains("\"DilemmaEffect_SaintWords\""), "the words at the grave counted as played out");
        string validator = File.ReadAllText(Path.Combine(root, "Tools", "DefValidator", "Program.cs"));
        C(validator.Contains("\"SAINT\"") && validator.Contains("\"ASAINT\""), "the def validator knows {SAINT} and {ASAINT}");
        return n;
    }

    /// <summary>The player's mod pool's row for a def, active, or null (docs/player-mods/defs.tsv.gz).</summary>
    private static string? PoolRow(string root, string defName, string type = "MemeDef")
    {
        pool ??= LoadPool(root);
        return pool.TryGetValue(type + "|" + defName, out var row) ? row : null;
    }

    private static Dictionary<string, string>? pool;

    private static Dictionary<string, string> LoadPool(string root)
    {
        var map = new Dictionary<string, string>();
        string path = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        if (!File.Exists(path)) return map;
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string? line;
        while ((line = r.ReadLine()) != null)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 4 && parts[3] == "yes") map[parts[0] + "|" + parts[1]] = line;
        }
        return map;
    }
}
