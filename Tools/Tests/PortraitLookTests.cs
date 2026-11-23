using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Portraits, phase 5: looks, special looks, work, and what the library lacks kept in the save (the player, 22 November
/// 2026: "if like a trait appears where they could be visually displayed ... co-join twins ... That should then, of course,
/// also be visible. Or if there are certain traits like this ... that the game then rather shows no image than ... a really
/// not fitting one", "the profession can and should also be reflected in how they look like", "it should always be also
/// based on an actual pawn's characteristic, and only then also this image is loaded", "Maybe the mod can also then log
/// ... in the save"); and the backdrop behind the game's own drawing ("That's the least it can do"). A pawn's looks from
/// traits with and without degrees, genes, hediffs and the body; the matcher's gates and costs; special looks both ways;
/// work from backstories and skills; the missing list's merging, columns and cap; what the large portrait draws; the
/// starter list's names against the player's mods (docs/player-mods); the seams.
/// </summary>
public static class PortraitLookTests
{
    private static PortraitColour Hex(string h) => PortraitLibraryMath.TryColour(h, out var c) ? c : throw new Exception("bad colour " + h);

    private static PortraitLook Person(string[]? looks = null, string role = "", PortraitSex sex = PortraitSex.Male, PortraitAge age = PortraitAge.Adult)
    {
        var l = new PortraitLook { sex = sex, age = age, skin = Hex("#c9a27e"), hairColour = Hex("#2a2a2a"), xenotype = "baseliner", race = "human", baseliner = true, role = role };
        foreach (var w in looks ?? Array.Empty<string>())
        {
            var r = PortraitLookMath.Book.Find(w);
            l.AddLook(r?.word ?? w, r?.special ?? false);
        }
        return l;
    }

    private static PortraitRecord Picture(string name, string[]? looks = null, string? role = null, string? pawn = null, PortraitSex sex = PortraitSex.Male, PortraitAge age = PortraitAge.Adult,
        List<string>? problems = null)
    {
        var f = new Dictionary<string, string?>
        {
            ["texPath"] = "x/" + name, ["sex"] = sex == PortraitSex.Female ? "female" : "male", ["age"] = PortraitLibraryMath.Word(age), ["skinColor"] = "#c9a27e",
            ["hairColor"] = "#2a2a2a", ["role"] = role, ["pawn"] = pawn
        };
        return PortraitLibraryMath.Record(name, k => f.TryGetValue(k, out var v) ? v : null, k => k == "looks" ? looks : null, problems ?? new List<string>());
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits, phase 5: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- A look's def read into a rule ----------------
        C(PortraitLookMath.TraitLine("DrugDesire:2", out var tn, out var td) && tn == "DrugDesire" && td == 2, "a trait line with a degree");
        C(PortraitLookMath.TraitLine("Beauty:-1", out tn, out td) && tn == "Beauty" && td == -1 && PortraitLookMath.TraitLine(" Pyromaniac ", out tn, out td) && tn == "Pyromaniac" && td == null,
            "a negative degree; a trait with no degree means any");
        C(!PortraitLookMath.TraitLine("Beauty:x", out _, out _) && !PortraitLookMath.TraitLine(":2", out _, out _) && !PortraitLookMath.TraitLine("", out _, out _), "a line that is no trait");
        var probs = new List<string>();
        PortraitLookMath.Rule("", false, null, null, null, null, probs);
        C(probs.Count == 2, $"no word and nothing that gives it: two problems ({string.Join("; ", probs)})");
        probs.Clear();
        PortraitLookMath.Rule("fire mage", false, new[] { "InnerFire", "Beauty:x" }, null, null, null, probs);
        C(probs.Count == 2 && probs.Any(p => p.Contains("word")) && probs.Any(p => p.Contains("Beauty:x")), "a word with a space, a trait line with no number: told");

        var rules = new[]
        {
            PortraitLookMath.Rule("drinker", false, new[] { "DrugDesire:1", "DrugDesire:2" }, null, new[] { "AlcoholAddiction" }, null, null),
            PortraitLookMath.Rule("pyromaniac", false, new[] { "Pyromaniac" }, null, null, null, null),
            PortraitLookMath.Rule("beautiful", false, new[] { "Beauty:2" }, null, null, null, null),
            PortraitLookMath.Rule("ugly", false, new[] { "Beauty:-1" }, null, null, null, null),
            PortraitLookMath.Rule("fireMage", false, new[] { "InnerFire" }, null, null, null, null),
            PortraitLookMath.Rule("batWings", true, null, new[] { "AG_BatWings" }, null, null, null),
            PortraitLookMath.Rule("conjoinedTwins", true, new[] { "MultiMind_ConjoinedTwins" }, null, null, new[] { "MultiMind_ConjoinedHumanBody" }, null),
            PortraitLookMath.Rule("drinker", false, new[] { "Wimp" }, null, null, null, null)
        };
        var bookProbs = new List<string>();
        var book = new LookBook(rules, bookProbs);
        C(book.Count == 7 && bookProbs.Count == 1 && book.AnySpecial && book.Find("DRINKER")?.word == "drinker" && book.Find("nothing") == null,
            "the book: a word given twice told and the first kept; words found whatever their case");

        // ---------------- A pawn's looks ----------------
        List<string> Of(string[]? traits = null, string[]? genes = null, string[]? hediffs = null, string body = "Human", bool special = false)
        {
            var ts = (traits ?? Array.Empty<string>()).Select(t => t.Split(':')).Select(a => new KeyValuePair<string, int>(a[0], a.Length > 1 ? int.Parse(a[1]) : 0));
            return book.Of(ts, genes, hediffs, body, special).Select(f => f.word + (f.special ? "*" : "") + "<" + f.from).OrderBy(x => x).ToList();
        }
        C(Of(new[] { "Pyromaniac:0" }).SequenceEqual(new[] { "pyromaniac<trait" }), "a trait with no degree in the line: the look");
        C(Of(new[] { "DrugDesire:2" }).SequenceEqual(new[] { "drinker<trait" }) && Of(new[] { "DrugDesire:1" }).SequenceEqual(new[] { "drinker<trait" })
          && Of(new[] { "DrugDesire:-1" }).Count == 0, "a trait line with a degree: only those degrees (a teetotaler is no drinker)");
        C(Of(new[] { "Beauty:2" }).SequenceEqual(new[] { "beautiful<trait" }) && Of(new[] { "Beauty:-1" }).SequenceEqual(new[] { "ugly<trait" }) && Of(new[] { "Beauty:1" }).Count == 0,
            "beauty by its degree");
        C(Of(hediffs: new[] { "AlcoholAddiction", "Flu" }).SequenceEqual(new[] { "drinker<hediff" }), "a hediff gives a look");
        C(Of(genes: new[] { "AG_BatWings", "Hair_Grey" }).SequenceEqual(new[] { "batWings*<gene" }), "a gene gives a special look");
        C(Of(body: "MultiMind_ConjoinedHumanBody").SequenceEqual(new[] { "conjoinedTwins*<body" }) && Of(new[] { "MultiMind_ConjoinedTwins:0" }, body: "MultiMind_ConjoinedHumanBody").Count == 1,
            "the race's body gives a look, once however many lines give it");
        C(Of(new[] { "DrugDesire:2", "Pyromaniac:0" }, new[] { "AG_BatWings" }, new[] { "AlcoholAddiction" }).Count == 3
          && Of(new[] { "DrugDesire:2", "Pyromaniac:0" }, new[] { "AG_BatWings" }, special: true).SequenceEqual(new[] { "batWings*<gene" }),
            "several at once, each once; only the special ones where asked");
        C(Of(new[] { "Unloaded_Trait:1" }, new[] { "NoSuchGene" }, new[] { "NoSuchHediff" }, "NoSuchBody").Count == 0 && new LookBook(null).Of(null, null, null, null).Count == 0,
            "names nobody has never match; an empty book gives nothing");

        PortraitLookMath.Use(book);
        try
        {
            // ---------------- Records with looks and work ----------------
            var rp = new List<string>();
            var mage = Picture("mage", new[] { "FIREMAGE" }, problems: rp);
            C(rp.Count == 0 && mage.looks.Contains("fireMage") && mage.specials.Count == 0, "a record's look by its word, the case aside");
            rp.Clear();
            var bad = Picture("bad", new[] { "fireMage", "wizardHat" }, "wizard", problems: rp);
            C(rp.Count == 2 && rp.Any(p => p.Contains("wizardHat")) && rp.Any(p => p.Contains("role")) && bad.looks.Count == 1 && bad.role == "",
                $"an unknown look or work is a record problem, the rest kept ({string.Join("; ", rp)})");
            var bats = Picture("bats", new[] { "batWings" });
            C(bats.specials.Contains("batWings") && bats.looks.Contains("batWings"), "a special look's picture knows it is special");
            C(PortraitLookMath.RoleWord("Soldier") == "soldier" && PortraitLookMath.RoleWord("laborer") == "labourer" && PortraitLookMath.RoleWord("wizard") == null
              && PortraitLookMath.Roles.Length == 18, "the eighteen works, as records write them");
            var people = new List<FolderPortrait>();
            var folderProbs = new List<string>();
            PortraitFolderMath.Parse("<Defs><ColonyCouncil.PortraitDef><defName>P1</defName><texPath>a/b</texPath><sex>male</sex><age>adult</age><skinColor>#c9a27e</skinColor>"
                                     + "<hairColor>#111111</hairColor><looks><li>drinker</li><li>batWings</li></looks><role>soldier</role></ColonyCouncil.PortraitDef></Defs>", people, null, folderProbs);
            C(people.Count == 1 && folderProbs.Count == 0 && people[0].record.looks.SetEquals(new[] { "drinker", "batWings" }) && people[0].record.specials.Contains("batWings")
              && people[0].record.role == "soldier", "the library folder's records.xml: looks and role read as the defs' are");

            // ---------------- The matcher: looks ----------------
            var plain = Picture("plain");
            var plain2 = Picture("plain2");
            var drinkerPic = Picture("drinkerPic", new[] { "drinker" });
            var lib = new List<PortraitRecord> { plain, mage, drinkerPic };
            float[] D(PortraitLook l) => PortraitLibraryMath.Distances(l, lib);
            var nobody = Person();
            var fireMage = Person(new[] { "fireMage" });
            var drinker = Person(new[] { "drinker" });
            C(float.IsInfinity(D(nobody)[1]) && float.IsInfinity(D(nobody)[2]) && !float.IsInfinity(D(nobody)[0]),
                "a picture showing a look never stands for someone without it (no fire mage's flame for one who can't do magic); the plain one does");
            C(!float.IsInfinity(D(fireMage)[0]) && float.IsInfinity(D(fireMage)[2]) && D(fireMage)[1] < D(fireMage)[0],
                "a fire mage takes the fire mage's picture before a plain one, never a drinker's");
            var onlyPlain = new List<PortraitRecord> { plain, drinkerPic };
            var pick = PortraitLibraryMath.Pick(fireMage, onlyPlain, 7, _ => 0);
            C(pick.Any && onlyPlain[pick.index] == plain, "with no fire mage's picture, a plain one stands in for a fire mage");
            C(Math.Abs(PortraitLibraryMath.LookCost(drinker, plain) - PortraitLibraryMath.LookMissingCost) < 1e-4f && PortraitLibraryMath.LookCost(drinker, drinkerPic) == 0f
              && PortraitLibraryMath.LookMissingCost < PortraitLibraryMath.FeatureCost, "a look of theirs a picture doesn't show costs 0.75, less than a feature; one it shows nothing");
            var both = Person(new[] { "drinker", "fireMage" });
            C(PortraitLibraryMath.LooksAgree(drinkerPic, both) && PortraitLibraryMath.LooksAgree(mage, both) && !PortraitLibraryMath.LooksAgree(Picture("db", new[] { "drinker", "beautiful" }), both),
                "a picture may show some of someone's looks, never one they haven't got");

            // ---------------- The matcher: work ----------------
            var soldierPic = Picture("soldierPic", role: "soldier");
            var work = new List<PortraitRecord> { plain, soldierPic };
            var cook = Person(role: "cook");
            var soldier = Person(role: "soldier");
            var child = Person(age: PortraitAge.Child);
            C(float.IsInfinity(PortraitLibraryMath.Distances(cook, work)[1]) && !float.IsInfinity(PortraitLibraryMath.Distances(cook, work)[0]),
                "a soldier's picture never stands for a cook; a picture with no work does");
            var sd = PortraitLibraryMath.Distances(soldier, work);
            C(sd[1] < sd[0] && Math.Abs(sd[0] - sd[1] - PortraitLibraryMath.RoleMissingCost) < 1e-4f, "a soldier prefers a soldier's picture; a plain one costs a little");
            C(PortraitLibraryMath.LookCost(child, plain) == 0f && !PortraitLibraryMath.LooksAgree(soldierPic, Person()), "no work: nothing to cost; a picture with a work never stands for someone with none");

            // ---------------- Special looks, both ways ----------------
            var twins = Person(new[] { "conjoinedTwins" });
            var twinPic = Picture("twinPic", new[] { "conjoinedTwins" });
            var special = new List<PortraitRecord> { plain, plain2, twinPic };
            var td2 = PortraitLibraryMath.Distances(twins, special);
            C(float.IsInfinity(td2[0]) && float.IsInfinity(td2[1]) && !float.IsInfinity(td2[2]), "a conjoined twin gets only a picture showing them");
            C(float.IsInfinity(PortraitLibraryMath.Distances(nobody, special)[2]), "a picture of conjoined twins never stands for anyone else");
            C(!PortraitLibraryMath.Pick(twins, new List<PortraitRecord> { plain, plain2 }, 3, _ => 0).Any, "with no picture of them, none at all: the game's own drawing");
            var batTwins = Person(new[] { "conjoinedTwins", "batWings" });
            C(float.IsInfinity(PortraitLibraryMath.Distances(batTwins, special)[2]) && !PortraitLibraryMath.SpecialsAgree(twinPic, batTwins), "two special looks: only a picture showing both");
            var pinPlain = Picture("pinPlain", pawn: "Avila");
            var pinTwins = Picture("pinTwins", new[] { "conjoinedTwins" }, pawn: "Avila");
            var pins = new List<PortraitRecord> { pinPlain, pinTwins };
            C(PortraitLibraryMath.PinFor(twins, pins, _ => true).index == 1 && PortraitLibraryMath.PinFor(nobody, pins, _ => true).index == 0
              && PortraitLibraryMath.PinFor(twins, new List<PortraitRecord> { pinPlain }, _ => true).index < 0,
                "a pin too: a special look's only where it shows it, and the plain pin never for them");
            var pinDrinker = Picture("pinDrinker", new[] { "drinker" }, "soldier", "Avila");
            C(PortraitLibraryMath.PinFor(nobody, new List<PortraitRecord> { pinDrinker }, _ => true).index == 0,
                "a pin is made from its colonist: its ordinary looks and work are not held against them");
            C(PortraitLibraryMath.LacksSpecial(plain, twins.specials) && !PortraitLibraryMath.LacksSpecial(twinPic, nobody.specials) && !PortraitLibraryMath.LacksSpecial(twinPic, twins.specials),
                "a kept picture lacks a special look gained since; one lost keeps it");
            bool Again(bool lacks, bool byHand = false) => PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitStage.Grown, "p", true, 0, byHand, 1, 1, "w", "w", 0, false, lacks);
            C(Again(true) && Again(true, true) && !Again(false), "picked once and kept, but a special look gained since its picture gives way (even one chosen by hand)");

            // ---------------- What the library lacks: looks ----------------
            var lackLib = new List<PortraitRecord> { plain, drinkerPic, Picture("fem", new[] { "fireMage" }, sex: PortraitSex.Female), pinTwins };
            var lacking = PortraitLibraryMath.LooksLacking(Person(new[] { "drinker", "fireMage", "conjoinedTwins" }), lackLib);
            C(lacking.SequenceEqual(new[] { "conjoinedTwins", "fireMage" }), $"looks no picture of their kind, sex and age shows (another's pin aside): {string.Join(", ", lacking)}");
            C(PortraitLibraryMath.LooksLacking(Person(new[] { "conjoinedTwins" }), lackLib, r => r.pawn == "Avila").Count == 0, "their own pin counts for them");
            C(Person(new[] { "drinker" }, "cook").Words.EndsWith("_drinker_cook") && Person().Words != Person(new[] { "drinker" }).Words, "their looks and work are part of their words, kept with a pick");
        }
        finally
        {
            PortraitLookMath.Use(null);
        }

        // ---------------- Work from backstories and skills ----------------
        string? RoleOf(string title, string defName, string cats = "") =>
            PortraitLookMath.RoleFromWords(PortraitLibraryMath.Tokens(title), PortraitLibraryMath.Tokens(defName), PortraitLibraryMath.Tokens(cats));
        C(RoleOf("Infantry medic", "InfantryMedic6") == "doctor" && RoleOf("", "InfantryMedic6") == "doctor" && RoleOf("", "MilitaryChaplain10") == "priest"
          && RoleOf("", "ArmyCook0") == "cook" && RoleOf("", "VFEM_Blacksmith37") == "smith" && RoleOf("", "VBE_StewCooker") == "cook",
            "the head of the phrase names the work: an infantry medic a doctor, a military chaplain a priest, an army cook a cook");
        C(RoleOf("", "AmateurBotanist79") == "farmer" && RoleOf("", "VFEM_BaronOfTheWoodlands91") == "leader" && RoleOf("", "VFEM_KeeperOfTheHerds93") == "herder"
          && RoleOf("", "Archer25") == "hunter" && RoleOf("", "VBE_BlacksmithApprentice") == "smith" && RoleOf("", "BrokenSoldier29") == "soldier",
            "the game's, Vanilla Backstories Expanded's and Medieval 2's backstories by their names");
        C(RoleOf("Glitterworld surgeon", "Something12") == "doctor" && RoleOf("Wanderer", "Scientist3") == "scout" && RoleOf("", "AbandonedChild23") == null
          && RoleOf("", "Nobody", "ImperialFighter") == "soldier", "the title before the name, the categories last; a backstory naming no work none");
        PortraitLookMath.SkillSeen S(string s, int lv, int pa, bool off = false) => new PortraitLookMath.SkillSeen(s, lv, pa, off);
        C(PortraitLookMath.RoleFromSkills(new[] { S("Intellectual", 12, 1), S("Shooting", 14, 0), S("Cooking", 9, 2) }, false) == "scholar",
            "their best skill with a passion (a better one without passion passed over)");
        C(PortraitLookMath.RoleFromSkills(new[] { S("Shooting", 8, 1) }, false) == "soldier" && PortraitLookMath.RoleFromSkills(new[] { S("Shooting", 8, 1) }, true) == "hunter",
            "shooting a soldier's, among people of the bow a hunter's");
        C(PortraitLookMath.RoleFromSkills(new[] { S("Melee", 10, 1), S("Plants", 10, 2) }, false) == "farmer" && PortraitLookMath.RoleFromSkills(new[] { S("Melee", 10, 2, true) }, false) == null
          && PortraitLookMath.RoleFromSkills(new[] { S("Fishing", 15, 2) }, false) == null && PortraitLookMath.RoleFromSkills(null!, false) == null,
            "equal levels: the burning passion; a disabled skill, a modded one or none: no work");

        // ---------------- Who holds a face: the player's own, notables, strangers ----------------
        {
            var own = PortraitRank.Own;
            var notable = PortraitRank.Notable;
            var stranger = PortraitRank.Stranger;
            var owners = PortraitLibraryMath.Owners(new[]
            {
                (1, "a", 100, notable), (2, "a", 500, own), (3, "b", 100, notable), (4, "b", 50, notable), (5, "c", 10, stranger), (6, "d", 900, own),
                (7, "e", 5, notable)
            }, new[] { new KeyValuePair<string, int>("e", 8) });
            C(owners["a"] == 2, "one of the player's own outranks a notable who had the face first (recruiting someone who had it)");
            C(owners["b"] == 4, "of two notables, the one who had it first");
            C(!owners.ContainsKey("c"), "a stranger's face is nobody's own");
            C(owners["d"] == 6 && owners["e"] == 8, "a colonist's face theirs; a face pinned to a colonist theirs above everyone");
            C(PortraitLibraryMath.Owners(new[] { (3, "x", 9), (4, "x", 2) }, null)["x"] == 4, "the older form: all the player's own, the first to have it");

            // May a stranger get a face another stranger keeps?
            C(!PortraitLibraryMath.MayShare(PortraitShareMode.Never, false, false) && !PortraitLibraryMath.MayShare(PortraitShareMode.Never, false, true),
                "\"Never\": a face another stranger keeps never goes to a second");
            C(PortraitLibraryMath.MayShare(PortraitShareMode.NotTogether, false, false) && !PortraitLibraryMath.MayShare(PortraitShareMode.NotTogether, false, true),
                "\"When not seen together\": only while the one keeping it is on no map");
            C(PortraitLibraryMath.MayShare(PortraitShareMode.Always, false, true), "\"Always\": shared freely, for a small library");
            C(PortraitLibraryMath.MayShare(PortraitShareMode.Never, true, true), "a special look's picture (conjoined twins, wings) may always be shared among strangers");

            // Two strangers with one face on the maps: the later one gives way.
            var older = new[] { (10, 100, true) };
            C(PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 11, 200, true, older), "two strangers with one face on the maps: the one who got it later gives way");
            C(!PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 11, 200, false, older) && !PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 11, 200, true, new[] { (10, 100, false) }),
                "not while either of them is off the maps (never seen together)");
            C(!PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 9, 50, true, older), "the one who had it first keeps it");
            C(PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 12, 100, true, older) && !PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, false, 9, 100, true, older),
                "got at the same moment: the lower id keeps it");
            C(PortraitLibraryMath.GivesWay(PortraitShareMode.Never, false, 11, 200, false, new[] { (10, 100, false) }), "\"Never\": the later one gives way wherever they are");
            C(!PortraitLibraryMath.GivesWay(PortraitShareMode.Always, false, 11, 200, true, older) && !PortraitLibraryMath.GivesWay(PortraitShareMode.NotTogether, true, 11, 200, true, older),
                "\"Always\", or a special look's picture: nobody gives way");

            // The pick: a free face first, wherever there is one; else a shareable one; else none.
            PortraitRecord Face(string name, string hair) => new PortraitRecord
            {
                name = name, sex = PortraitSex.Male, age = PortraitAge.Adult, skin = Hex("#c9a27e"), skinSet = true, hairColour = Hex(hair), hairColourSet = true
            };
            var lib = new List<PortraitRecord> { Face("near", "#2a2a2a"), Face("far", "#e8cc90") };
            var man = Person();
            var usedNear = new Dictionary<string, int> { ["near"] = 1 };
            int Used(string c) => usedNear.TryGetValue(c, out int u) ? u : 0;
            var free = PortraitLibraryMath.Pick(man, lib, 3, Used, null, (_, _) => true);
            C(free.Any && free.character == "far" && PortraitLibraryMath.Distance(man, lib[1]) > PortraitLibraryMath.Distance(man, lib[0]),
                "a face nobody keeps before a nearer one someone keeps (\"it should not be that the same image comes over and over again\")");
            var allUsed = new Dictionary<string, int> { ["near"] = 1, ["far"] = 2 };
            var none = PortraitLibraryMath.Pick(man, lib, 3, c => allUsed[c], null, (_, _) => false);
            C(!none.Any && none.crowded, "every fitting face someone else's and none to share: no picture (the game's drawing), told as no free picture");
            var shared = PortraitLibraryMath.Pick(man, lib, 3, c => allUsed[c], null, (c, _) => c == "far");
            C(shared.Any && shared.character == "far", "only a face that may be shared is shared");
            C(PortraitLibraryMath.Pick(man, lib, 3, c => allUsed[c]).character == "near", "with no rule given, the fewest kept: the nearest");
            C(!PortraitLibraryMath.Pick(man, lib, 3, _ => 0, c => true).Any && PortraitLibraryMath.Pick(man, lib, 3, _ => 0, c => true).crowded && !PortraitLibraryMath.Pick(man, new List<PortraitRecord>(), 3, _ => 0).crowded,
                "faces another's own are never given (no free picture); an empty library is no crowding");
        }

        // ---------------- What the library lacks, kept in the save ----------------
        {
            PortraitNeed Need(string what, string kind, int count, int day, string look = "", string date = "2026-11-22") =>
                new PortraitNeed { what = what, kind = kind, look = look, sex = "male", age = "adult", why = "w", count = count, day = day, lastSeen = date };
            var old = new List<PortraitNeed> { Need("person", "Human/Baseliner", 2, 10), Need("look", "Human/Baseliner", 1, 11, "pyromaniac") };
            var fresh = new List<PortraitNeed> { Need("look", "Human/Baseliner", 3, 14, "pyromaniac"), Need("look", "Human/Baseliner", 1, 14, "drinker"), Need("room", "Bedroom", 1, 9) };
            var merged = PortraitFolderMath.Merge(old, fresh);
            var pyro = merged.First(x => x.look == "pyromaniac");
            C(merged.Count == 4 && pyro.count == 4 && pyro.day == 14 && merged.Sum(x => x.count) == 8, "merged: one line per need, the counts added, the newest day; nothing lost");
            C(merged[0].what == "person" && merged[1].what == "look" && merged[2].what == "look" && merged[3].what == "room" && merged[1].look == "pyromaniac",
                "people first, then looks, rooms and the land; the commonest first");
            C(Need("look", "K", 1, 1, "drinker").Key != Need("look", "K", 1, 1, "pyromaniac").Key && new PortraitNeed { what = "person", role = "cook" }.Key != new PortraitNeed { what = "person", role = "smith" }.Key,
                "a look or a work makes its own line");
            var saved = merged.Select(x => PortraitFolderMath.Line(x, true)).ToList();
            var back = PortraitFolderMath.ReadLines(PortraitFolderMath.SaveColumnsLine, saved);
            C(back.Count == 4 && back.Select(x => x.Key).SequenceEqual(merged.Select(x => x.Key)) && back.Sum(x => x.day) == merged.Sum(x => x.day) && back.Sum(x => x.count) == 8,
                "the save's lines read back the same, the game's day with them");
            var older = PortraitFolderMath.ReadLines("what,kind,mod,sex,age,skin,hair,era,why,used instead,how many,last seen,day", new[] { "person,Human/Baseliner,ludeon.rimworld,male,adult,fair,black,medieval,none,x,3,2026-11-20,4" });
            C(older.Count == 1 && older[0].count == 3 && older[0].day == 4 && older[0].look == "" && older[0].mod == "ludeon.rimworld",
                "lines saved with other columns read back by their names (a save from before the look and role columns)");
            var oldFile = PortraitFolderMath.Read("# x\nwhat,kind,mod,sex,age,skin,hair,era,why,used instead,how many,last seen\nroom,Barracks,,,,,,medieval,no backdrop,,2,2026-11-21\n");
            C(oldFile.Count == 1 && oldFile[0].kind == "Barracks" && oldFile[0].count == 2 && oldFile[0].day == -1, "a missing.csv written before the new columns reads as it was");
            var many = Enumerable.Range(0, 260).Select(i => Need("look", "K" + i, 1, i)).ToList();
            var kept = PortraitFolderMath.Keep(many, PortraitNeedsKeptMost);
            C(kept.Count == PortraitNeedsKeptMost && kept.Min(x => x.day) == 260 - PortraitNeedsKeptMost && kept.All(x => x.day >= 60),
                $"at most {PortraitNeedsKeptMost} lines, the least recently seen let go");
            C(PortraitFolderMath.Keep(merged, PortraitNeedsKeptMost).Count == 4, "under the cap nothing is let go");
            string log = PortraitFolderMath.Csv(merged, true);
            C(log.Contains("what,kind,look,role,mod,sex,age,skin,hair,era,why,used instead,how many,last seen,day") && log.Contains("look,Human/Baseliner,pyromaniac,"),
                "for the log: the save's columns, the game's day last");
        }

        // ---------------- What the large portrait draws ----------------
        C(PortraitComposeMath.WhatToDraw(true, true, PortraitBackdropMode.AsPainted, false) == PortraitStageDraw.PaintedOnBackdrop
          && PortraitComposeMath.WhatToDraw(true, true, PortraitBackdropMode.Softened, true) == PortraitStageDraw.PaintedOnBackdrop,
            "a painted picture in front of the backdrop of where they are, whatever the new setting");
        C(PortraitComposeMath.WhatToDraw(false, true, PortraitBackdropMode.AsPainted, true) == PortraitStageDraw.GameOnBackdrop
          && PortraitComposeMath.WhatToDraw(false, true, PortraitBackdropMode.LightlySoftened, true) == PortraitStageDraw.GameOnBackdrop,
            "the game's own drawing in front of the backdrop too (no picture of their kind, a special look, a library of backdrops only)");
        C(PortraitComposeMath.WhatToDraw(false, false, PortraitBackdropMode.AsPainted, true) == PortraitStageDraw.GameOnGround
          && PortraitComposeMath.WhatToDraw(false, true, PortraitBackdropMode.Plain, true) == PortraitStageDraw.GameOnGround
          && PortraitComposeMath.WhatToDraw(false, true, PortraitBackdropMode.AsPainted, false) == PortraitStageDraw.GameOnGround,
            "the game's drawing on the plain panel with no backdrop, with \"Plain\", or with \"Backdrop behind the game's own drawing too\" off");
        C(PortraitComposeMath.WhatToDraw(true, false, PortraitBackdropMode.AsPainted, true) == PortraitStageDraw.PaintedOnGround
          && PortraitComposeMath.WhatToDraw(true, true, PortraitBackdropMode.Plain, true) == PortraitStageDraw.PaintedOnGround, "a painted picture on its ground with no backdrop or \"Plain\"");
        PortraitComposeMath.GameLine(PortraitOutlineMode.Off, out float sh0, out float ir0, out float ink0);
        PortraitComposeMath.GameLine(PortraitOutlineMode.Subtle, out float sh1, out float ir1, out float ink1);
        PortraitComposeMath.GameLine(PortraitOutlineMode.Stronger, out float sh2, out float ir2, out float ink2);
        C(sh0 > 0f && ink0 == 0f && ir0 == 0f && sh1 > sh0 && ink1 > 0f && sh2 > sh1 && ink2 > ink1 && ir2 > ir1,
            "behind the game's drawing always a soft shadow; the line only with \"Line round the person\" on, firmer with Stronger");

        // ---------------- The starter list against the player's mods ----------------
        var pool = new Dictionary<string, List<(string type, string mod, bool active)>>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var sr = new StreamReader(gz))
        {
            sr.ReadLine();
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length < 4) continue;
                if (!pool.TryGetValue(c[1], out var l)) pool[c[1]] = l = new List<(string, string, bool)>();
                l.Add((c[0], c[2], c[3] == "yes"));
            }
        }
        bool Active(string name, params string[] types) => pool.TryGetValue(name, out var l) && l.Any(x => x.active && types.Contains(x.type));
        // Every file of looks (the starter list, or the catalogue's from Tools/PortraitArt/looks_to_defs.py): the mods test
        // leaves this folder to these checks.
        var defs = Directory.GetFiles(Path.Combine(root, "Defs", "PortraitLookDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.PortraitLookDef")).ToList();
        C(defs.Count > 0, $"the looks' defs read ({defs.Count})");
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var starter = new List<LookRule>();
        foreach (var d in defs)
        {
            string name = d.Element("defName")?.Value ?? "?";
            List<string> L(string f) => d.Element(f)?.Elements("li").Select(x => x.Value.Trim()).ToList() ?? new List<string>();
            var own = new List<string>();
            var rule = PortraitLookMath.Rule(d.Element("word")?.Value, d.Element("special")?.Value == "true", L("traits"), L("genes"), L("hediffs"), L("bodies"), own);
            starter.Add(rule);
            C(own.Count == 0 && words.Add(rule.word), $"{name}: read without a problem, its word its own ({string.Join("; ", own)})");
            C(!string.IsNullOrWhiteSpace(d.Element("prompt")?.Value) && !string.IsNullOrWhiteSpace(d.Element("label")?.Value), $"{name}: a label and words for the image prompts");
            var missing = rule.traits.Where(t => !Active(t.Key, "TraitDef")).Select(t => "trait " + t.Key)
                .Concat(rule.genes.Where(g => !Active(g, "GeneDef", "GenelineGeneDef")).Select(g => "gene " + g))
                .Concat(rule.hediffs.Where(h => !Active(h, "HediffDef")).Select(h => "hediff " + h))
                .Concat(rule.bodies.Where(b => !Active(b, "BodyDef")).Select(b => "body " + b)).ToList();
            C(missing.Count == 0, $"{name}: every name is a def of the game or a mod the player has active ({string.Join(", ", missing)})");
            foreach (var f in d.Elements().Select(x => x.Name.LocalName).Where(x => x != "defName" && x != "label"))
                C(Regex.IsMatch(Read("UI/Faces/PortraitLookDef.cs"), @"public (string|bool|List<string>) " + f + @"\b"), $"{name}: '{f}' is a field of the def");
        }
        // By what gives each look, not its word: the catalogue's file (Tools/PortraitArt/looks_to_defs.py) may name them its own way.
        var sb = new LookBook(starter);
        bool Special(Func<LookRule, bool> by) => sb.rules.Any(r => r.special && by(r));
        bool Ordinary(Func<LookRule, bool> by) => sb.rules.Any(r => !r.special && by(r));
        bool Trait(LookRule r, string name, int? degree) => r.traits.Any(t => t.Key == name && t.Value == degree);
        C(Special(r => r.bodies.Contains("MultiMind_ConjoinedHumanBody")) && Special(r => r.genes.Contains("AG_BatWings")) && Special(r => r.genes.Contains("VRE_InsectWings")),
            "special: Conjoined Twins' twins by their body, bat wings, insect wings");
        C(Ordinary(r => Trait(r, "Pyromaniac", null)) && Ordinary(r => Trait(r, "Psychopath", null)) && new[] { 2, 1, -1, -2 }.All(dg => Ordinary(r => Trait(r, "Beauty", dg)))
          && Ordinary(r => r.hediffs.Contains("AlcoholAddiction")) && Ordinary(r => r.hediffs.Contains("PsychiteAddiction")) && Ordinary(r => Trait(r, "InnerFire", null)),
            "ordinary: a pyromaniac, a psychopath, beauty's four degrees, a drinker, a drug addict, a fire mage");
        var wingGenes = pool.Where(kv => kv.Value.Any(x => x.active && (x.type == "GeneDef" || x.type == "GenelineGeneDef")) && Regex.IsMatch(kv.Key, "Wings?(_|$)|Glidewings")
                                         && !kv.Key.Contains("NoWings") && !kv.Key.Contains("EarWings")).Select(kv => kv.Key).ToList();
        var unlooked = wingGenes.Where(g => !sb.rules.Any(r => r.special && r.genes.Contains(g))).ToList();
        C(wingGenes.Count > 20 && unlooked.Count == 0, $"every active wing gene of the player's mods gives a special look ({wingGenes.Count}; without: {string.Join(", ", unlooked)})");
        C(!sb.rules.Any(r => r.hediffs.Contains("AlcoholAddiction") && r.hediffs.Contains("PsychiteAddiction")), "a drinker's look and a drug addict's are two looks");

        // ---------------- The seams ----------------
        string lib2 = Read("UI/Faces/PortraitLibrary.cs"), needs = Read("UI/Faces/PortraitNeeds.cs"), reader = Read("UI/Faces/PortraitReader.cs"), lookDef = Read("UI/Faces/PortraitLookDef.cs");
        string stage = Read("UI/Faces/FaceEngineStage.cs"), folder = Read("UI/Faces/PortraitFolder.cs"), defSrc = Read("UI/Faces/PortraitDef.cs");
        C(lib2.Contains("!PortraitLibraryMath.LacksSpecial(records[keptAt], PortraitLooks.SpecialsOf(p))") && lib2.Contains("PortraitLibraryMath.PickAgain(") && lib2.Contains(", lacks);"),
            "a kept picture checked every few seconds against the special looks they have now; one lacking gives way");
        C(lib2.Contains("NoteOnce(p, null, keptAt, k.distance, k.pinned || k.byHand)") && lib2.Contains("PortraitLibraryMath.LooksLacking(look, records, r => Theirs(r, p))")
          && lib2.Contains("PortraitNeeds.NoteLook(") && lib2.Contains("noted.Clear();"), "each person looked at once for the missing list each time the library is read, kept pictures too");
        C(reader.Contains("PortraitLooks.Fill(p, l);") && reader.Contains("l.role = RoleOf(p, age);") && reader.Contains("p.story?.Adulthood") && reader.Contains("p.skills?.skills")
          && reader.Contains("age < PortraitAge.Teen"), "a pawn's looks and work read with the rest of their looks; a child has no work");
        C(lookDef.Contains("public class PortraitLookDef : Def") && lookDef.Contains("t.Suppressed") && lookDef.Contains("g.Active") && lookDef.Contains("p.RaceProps?.body?.defName")
          && lookDef.Contains("if (Prefs.DevMode) NoteUnloaded(defs);"), "the looks' def; a suppressed trait and an inactive gene give none; names of mods not loaded a note in dev mode");
        C(folder.Contains("PortraitLooks.Ensure();") && defSrc.Contains("PortraitLooks.Ensure();") && defSrc.Contains("name == \"looks\" ? looks") && defSrc.Contains("case \"role\": return role;"),
            "the looks known before any record is read; the def's looks and role read");
        C(needs.Contains("public class PortraitNeedsKept : GameComponent") && needs.Contains("Scribe_Collections.Look(ref lines, \"lines\", LookMode.Value);")
          && needs.Contains("public const int MostLines = 200;") && needs.Contains("PortraitFolderMath.Merge(PortraitFolderMath.ReadLines(columns, lines), needs)")
          && needs.Contains("PortraitNeedsKept.Instance?.Add(n.Copy());"), "kept in the save: one line per need, at most 200, merged on load with nothing lost");
        C(needs.Contains("[DebugAction(\"Rimfolk\", \"Portraits: what the library lacks (log)\"") && needs.Contains("public static bool Writes => PortraitFolder.Folder.Length > 0;")
          && needs.Contains("if (!Writes || writing || pending.Count == 0) return;"), "the dev action writes the save's list to the log; missing.csv only with a folder, the save always");
        C(needs.Contains("PortraitNeedsKept.MostLines") && Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")), "PortraitNeeds.cs:lines"),
            "the saved list has a model in the save estimate");
        C(stage.Contains("public static bool DrawGameStaged(") && stage.Contains("PortraitComposeMath.WhatToDraw(false, look.backdrop != null, mode, true)")
          && stage.Contains("if (PortraitSources.ForStage(p) != null) return false;") && stage.Contains("DrawShadow(new Rect(r.x + sx, r.y, r.width, r.height), cam, shadow);"),
            "the game's drawing on a backdrop only where the library has no picture for them, with its shadow");
        string keeperSrc = Read("UI/Faces/PortraitKeeper.cs");
        C(lib2.Contains("|| p.IsPrisonerOfColony") && lib2.Contains("p.Faction.leader == p || PortraitReader.Titled(p)") && lib2.Contains("notableIds.Add(n.pawn.thingIDNumber)")
          && lib2.Contains("PortraitLibraryMath.Owners(keeper?.Holders, pinned)"),
            "the player's own (colonists, slaves, prisoners) and notables (leaders, titled nobles, the colony's named contacts) hold their faces");
        C(lib2.Contains("keeper.SetNotable(k, rank == PortraitRank.Notable)") && lib2.Contains("SweepNotables(keeper);") && keeperSrc.Contains("Scribe_Values.Look(ref notable, \"notable\");")
          && keeperSrc.Contains("!k.reserved && !k.notable && now - k.seen"), "a notable's hold kept in the save, set at once, let go when they die or no longer matter");
        C(lib2.Contains("!GivesWay(k, id, records[keptAt], rank, keeper)") && lib2.Contains("(c, r) => MayShare(c, r, id, rank, keeper)") && lib2.Contains("map.mapPawns.AllPawnsSpawned")
          && lib2.Contains("CC_PortraitNeed_NoFree") && lib2.Contains("retryAt[id] = now + RetryEvery;"),
            "strangers: a free face first, sharing as the setting says, the later of two on the maps giving way, no free picture told and looked at again");
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public PortraitShareMode portraitShare = PortraitShareMode.NotTogether;")
          && settings.Contains("Scribe_Values.Look(ref portraitShare, \"portraitShare\", PortraitShareMode.NotTogether);") && SettingsCatalog.Find("portraitShare")?.section == "CC_SetSec_Portraits",
            "the setting \"Strangers may share a picture\", when not seen together by default, under Portraits");
        C(settings.Contains("public bool portraitBackdropGame = true;") && settings.Contains("Scribe_Values.Look(ref portraitBackdropGame, \"portraitBackdropGame\", true);")
          && SettingsCatalog.Find("portraitBackdropGame")?.section == "CC_SetSec_Portraits", "the setting \"Backdrop behind the game's own drawing too\", on, under Portraits");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        foreach (var k in new[] { "CC_Settings_PortraitBackdropGame", "CC_Settings_PortraitBackdropGame_Tip", "CC_SetNote_PortraitBackdropGame", "CC_PortraitNeed_NoLook", "CC_PortraitNeed_NoSpecialLook",
                     "CC_PortraitNeed_NoFree", "CC_SetLabel_PortraitShare", "CC_SetTip_PortraitShare", "CC_PortraitShare_Never", "CC_PortraitShare_NotTogether", "CC_PortraitShare_Always",
                     "CC_SetNote_PortraitShare_Never", "CC_SetNote_PortraitShare_NotTogether", "CC_SetNote_PortraitShare_Always" })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");
        return n;
    }

    /// <summary>The save's cap, read from the source (the game's class isn't linked here).</summary>
    private static int PortraitNeedsKeptMost => 200;
}
