using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rimfolk's item card, step 3 (the player, 9 November 2026: "also like item card for everything"; docs/design/rimfolk-look.md,
/// stage 3, point 5): every item the first two steps left. The rules (Items/ItemCardUseMath.cs) on the base game's own, by
/// hand from their defs and what the card reads of them: the kinds and their order; the sums (the game's experience curve and
/// a neurotrainer's levels, a book's experience an hour, hunger by metabolism, a surgery's chance, techprints left, who gains
/// most); the tags and the line for a novel, a textbook, a schematic, a tome, a bionic arm, a natural kidney, a peg leg, a
/// techprint, a shooting neurotrainer, a psytrainer, a genepack and a xenogerm, a psychic shock lance's effects, a psychic
/// animal pulser, a healer mech serum, a high-explosive mortar shell, a minified armchair and bed, an unfinished thing and a
/// plain item, on their own and at home; and the seams (the sorting, every key, the model built rarely, the card drawn from
/// its model alone, the setting naming every kind).
/// </summary>
public static class ItemCardStepThreeTests
{
    static UseCandidate W(string name, bool can, float gain, string why) => new UseCandidate { Name = name, Can = can, Gain = gain, Why = why };

    static ItemFacts F(string label, ItemKind kind, UseFacts u, float value = 100f, float mass = 0.5f, bool home = false) => new ItemFacts
    {
        Label = label, Kind = kind, InHand = true, Value = value, Mass = mass, RotDays = -1f, Use = u, HasColony = home
    };

    // ---------------- The base game's own, by hand ----------------

    public static ItemFacts Novel(bool home)
    {
        var u = new UseFacts { BookKind = "novel", Quality = "excellent", JoyFactor = 1.4f, ReadHours = 2f };
        if (home) u.Who = ItemCardMath.Ranked(new[] { W("Bo", true, 0.8f, "recreation at 20%"), W("Cy", true, 0.3f, "recreation at 70%") });
        return F("novel", ItemKind.Book, u, 160f, 0.5f, home);
    }

    public static ItemFacts Textbook(bool home)
    {
        var u = new UseFacts { BookKind = "textbook", Quality = "good", Skills = { "shooting", "melee" }, SkillXpHour = 300f, MaxLevel = 10, ReadHours = 2f };
        if (home)
            u.Who = ItemCardMath.Ranked(new[]
            {
                W("Cy", false, 0f, "Shooting 12: past what it teaches (level 10), Melee 11: past what it teaches (level 10)"),
                W("Bo", true, ItemCardMath.BookGain(300f, 1.5f, 6, 10, false) + ItemCardMath.BookGain(300f, 0.35f, 3, 10, false), "Shooting 6 (burning): about 450 an hour now, Melee 3: about 105 an hour now"),
                W("Di", false, 0f, "can't learn shooting, can't learn melee")
            });
        return F("textbook", ItemKind.Book, u, 160f, 0.5f, home);
    }

    public static ItemFacts Schematic(bool done, bool home)
    {
        var u = new UseFacts { BookKind = "schematic", Quality = "normal", ResearchHour = 120f, ReadHours = 2f };
        if (done) u.ProjectsDone = 1;
        else u.Projects.Add("gun turrets");
        if (home) u.Who = ItemCardMath.Ranked(new[] { W("Bo", true, 1.2f, "reads at 120%"), W("Cy", true, 1f, "reads at 100%") });
        return F("schematic", ItemKind.Book, u, 150f, 0.5f, home);
    }

    public static ItemFacts Tome() => F("tome", ItemKind.Book, new UseFacts { BookKind = "tome", Quality = "normal", Projects = { "basic" }, ResearchHour = 2f, BreakHour = 0.02f }, 250f);

    public static ItemFacts BionicArm(bool home)
    {
        var u = new UseFacts { Part = "shoulder", Efficiency = 1.25f, BetterThanNatural = true, Installable = true, DoctorLevel = 5, Adds = { "Manipulation +10%" } };
        if (home)
        {
            u.Missing = 1;
            u.MissingWho = "Bo is missing the part it replaces";
            u.BestDoctor = "Cy";
            u.BestChance = 0.82f;
            u.FaithFor = "faiths at home glad of it: Way of the Wire (Body modification: approved)";
            u.Who = ItemCardMath.Ranked(new[] { W("Bo", true, 1.25f, "missing their left arm"), W("Cy", false, 0f, "left shoulder at 100%: no better with it") });
        }
        return F("bionic arm", ItemKind.BodyPart, u, 1200f, 5f, home);
    }

    public static ItemFacts Kidney(bool home)
    {
        var u = new UseFacts { Part = "kidney", Natural = true, Efficiency = 1f, Installable = true, DoctorLevel = 5 };
        if (home) u.Who = ItemCardMath.Ranked(new[] { W("Bo", false, 0f, "left kidney at 100%: no better with it"), W("Cy", false, 0f, "right kidney at 100%: no better with it") });
        return F("kidney", ItemKind.BodyPart, u, 250f, 0.5f, home);
    }

    public static ItemFacts PegLeg(bool home)
    {
        var u = new UseFacts { Part = "leg", Efficiency = 0.6f, Installable = true };
        if (home)
        {
            u.FaithAgainst = "faiths at home that frown on it: Old Ways (Body modification: abhorrent)";
            u.Who = ItemCardMath.Ranked(new[] { W("Bo", false, 0f, "left leg at 100%: no better with it") });
        }
        return F("peg leg", ItemKind.BodyPart, u, 25f, 3f, home);
    }

    public static ItemFacts Techprint(int applied, int needed, bool done, bool home) => F("charged shot techprint", ItemKind.Techprint, new UseFacts
    {
        Project = "Charged shot", Applied = applied, Needed = needed, ProjectDone = done, Unlocks = 3, UnlockWords = "charge rifle, charge lance and 1 more"
    }, 600f, 0.03f, home);

    public static ItemFacts Neurotrainer(bool home)
    {
        var u = new UseFacts { Skill = "shooting", Xp = 50000f };
        if (home)
            u.Who = ItemCardMath.Ranked(new[]
            {
                W("Bo", true, ItemCardMath.LevelAfter(6, 0f, 50000f * 1.5f) - 6f, "level 6 (burning): to about 12.9"),
                W("Cy", true, ItemCardMath.LevelAfter(6, 0f, 50000f * 0.35f) - 6f, "level 6: to about 8.3"),
                W("Di", false, 0f, "can't learn shooting")
            });
        return F("shooting neurotrainer", ItemKind.Trainer, u, 1000f, 0.3f, home);
    }

    public static ItemFacts Psytrainer(bool anyone, bool home)
    {
        var u = new UseFacts { Psycast = "stun", PsycastLevel = 1 };
        if (home)
            u.Who = ItemCardMath.Ranked(anyone
                ? new[] { W("Bo", false, 0f, "no psylink"), W("Cy", true, 2f, "psylink 2: can learn it") }
                : new[] { W("Bo", false, 0f, "no psylink"), W("Cy", false, 0f, "no psylink") });
        return F("psytrainer (stun)", ItemKind.Trainer, u, 500f, 0.3f, home);
    }

    public static ItemFacts Genepack(bool assembler, int max, bool home) => F("genepack (robust, fast runner)", ItemKind.Genes, new UseFacts
    {
        GeneKind = "genepack", GenesKnown = true, GeneCount = 2, GeneNames = "robust, fast runner", Complexity = 4, Metabolism = -2, Hunger = ItemCardMath.HungerFor(-2),
        Assembler = assembler, AssemblerMax = max
    }, 100f, 0.2f, home);

    public static ItemFacts Xenogerm(bool home)
    {
        var u = new UseFacts
        {
            GeneKind = "xenogerm", GenesKnown = true, Xenotype = "Ironhide", GeneCount = 5, GeneNames = "robust, tough skin, deathless, 2 more", Complexity = 11, Metabolism = 1, Archites = 1,
            Hunger = ItemCardMath.HungerFor(1)
        };
        if (home)
        {
            u.FaithAgainst = "Di: their faith forbids implanting it";
            u.Who = ItemCardMath.Ranked(new[]
            {
                W("Bo", true, 6f + 1f, "now baseliner; metabolism +1 with it"), W("Cy", true, 6f - 2f, "now hussar, whose xenogenes it would replace; metabolism -2 with it"),
                W("Di", false, 0f, "their faith forbids implanting it")
            });
        }
        return F("xenogerm (Ironhide)", ItemKind.Genes, u, 20f, 0.2f, home);
    }

    public static ItemFacts ShockLance() => F("psychic shock lance", ItemKind.Usable, new UseFacts
    {
        Effects = { "CompTargetEffect_PsychicShock", "CompTargetEffect_BrainDamageChance", "CompTargetEffect_GoodwillImpact" },
        Does = "puts the target in psychic shock", Targets = "one person or animal the user chooses", UsedUp = false,
        Risks = { "30% chance of brain damage to the target", "goodwill -200 with the target's people" }
    }, 550f, 0.5f);

    public static ItemFacts AnimalPulser() => F("psychic animal pulser", ItemKind.Usable, new UseFacts
    {
        Effects = { "CompTargetEffect_Manhunter" }, Does = "turns the animals manhunter", Targets = "every animal on the map (those psychically sensitive)", UsedUp = true,
        Risks = { "manhunter animals attack anyone, the colony too" }
    }, 700f, 0.5f);

    public static ItemFacts HealerSerum() => F("healer mech serum", ItemKind.Usable, new UseFacts
    {
        Effects = { "CompUseEffect_FixWorstHealthCondition" }, Does = "cures the user's worst health condition", Targets = "the user", UsedUp = true
    }, 1300f, 0.2f);

    public static ItemFacts Shell(int launchers, bool any, bool home) => F("high-explosive shell", ItemKind.Shell, new UseFacts
    {
        Damage = "bomb", DamageAmount = 50, Radius = 2.9f, Penetration = 0.75f, ExplodesItself = true, Launchers = launchers, LauncherWords = launchers > 0 ? launchers + " mortars" : "",
        AnyLauncher = any, LauncherKind = "mortar"
    }, 55f, 1.25f, home);

    public static ItemFacts Armchair(bool home) => F("armchair", ItemKind.Furniture, new UseFacts
    {
        Seat = true, Comfort = 0.8f, Beauty = 3f, BestRoom = home ? "dining room" : "", BestStageNow = "dull", BestStageWith = "mediocre", BestIndexNow = 1, BestIndexWith = home ? 2 : 0
    }, 150f, 15f, home);

    public static ItemFacts Bed(int bedless, bool home) => F("bed", ItemKind.Furniture, new UseFacts
    {
        Bed = true, Rest = 1f, Comfort = 0.75f, Beauty = 1f, Bedless = bedless, BedlessWho = bedless > 0 ? "Bo and Cy" : ""
    }, 100f, 30f, home);

    public static ItemFacts Generator() => F("chemfuel powered generator", ItemKind.Furniture, new UseFacts { Power = -1000f, Beauty = -10f }, 400f, 40f);

    public static ItemFacts Unfinished() => F("unfinished charge rifle", ItemKind.Other, new UseFacts { Unfinished = true, Product = "charge rifle", WorkLeft = 12000f, Bench = "fabrication bench" }, 300f, 1f);

    public static ItemFacts Wastepack(bool home) => F("toxic wastepack", ItemKind.Other, new UseFacts { Held = home ? 12 : -1 }, 0f, 2f, home);

    public static void Dump(ItemFacts f)
    {
        Console.WriteLine(ItemCardMath.Plain(f));
        foreach (var t in ItemCardMath.Tags(f)) Console.WriteLine("   [" + t.Category + "] " + t.Id + " / " + t.Label + ": " + t.Why);
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "item card, the rest: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        bool Has(ItemFacts f, string id) => ItemCardMath.Tags(f).Any(t => t.Id == id);
        string Why(ItemFacts f, string id) => ItemCardMath.AllTags(f).FirstOrDefault(t => t.Id == id)?.Why ?? "";
        string P(ItemFacts f) => ItemCardMath.Plain(f);
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The kinds ----------
        ItemKind K(ItemSort s) => ItemCardMath.KindOf(s);
        C(K(new ItemSort { Item = true, Book = true, ToUse = true }) == ItemKind.Book, "a book (a novel, a textbook, a schematic, a tome, a mod's) is a book");
        C(K(new ItemSort { Item = true, Techprint = true, ToUse = true, Resource = true }) == ItemKind.Techprint, "a techprint filed among resources is a techprint, never a material");
        C(K(new ItemSort { Item = true, Trainer = true, Usable = true, ToUse = true }) == ItemKind.Trainer, "a neurotrainer or psytrainer (used from the hand) is a trainer before a usable item");
        C(K(new ItemSort { Item = true, Genes = true, ToUse = true }) == ItemKind.Genes, "a genepack, xenogerm or embryo is genes");
        C(K(new ItemSort { Item = true, Usable = true, BodyPart = true, ToUse = true }) == ItemKind.Usable, "a psylink neuroformer (a body part used from the hand) is a usable item");
        C(K(new ItemSort { Item = true, BodyPart = true, ToUse = true }) == ItemKind.BodyPart, "a bionic arm, a kidney, a peg leg is a body part");
        C(K(new ItemSort { Item = true, Shell = true, ToUse = true, Resource = true }) == ItemKind.Shell, "a mortar shell is a shell");
        C(K(new ItemSort { Minified = true }) == ItemKind.Furniture && K(new ItemSort { Minified = true, Art = true }) == ItemKind.Art, "a building in a crate is furniture, a sculpture in a crate still art");
        C(K(new ItemSort { Item = true }) == ItemKind.Other && K(new ItemSort { Item = true, ToUse = true }) == ItemKind.Other, "any other item (a wastepack, an unfinished thing) opens the generic card");
        C(K(new ItemSort { Item = true, Stuff = true }) == ItemKind.Material && K(new ItemSort { Item = true, Resource = true }) == ItemKind.Material, "materials keep their card");
        C(K(new ItemSort { Item = true, Medicine = true, Usable = true }) == ItemKind.Medicine && K(new ItemSort { Item = true, Drug = true, Book = true }) == ItemKind.Drug
          && K(new ItemSort { Item = true, Feeds = true }) == ItemKind.Food, "step 1's kinds come first");
        C(K(new ItemSort()) == ItemKind.None && K(new ItemSort { ToUse = true }) == ItemKind.None, "a building standing on the map (no flag of an item) opens the game's card");
        C(Enum.GetValues(typeof(ItemKind)).Cast<ItemKind>().Count(ItemCardMath.StepThree) == 9 && !ItemCardMath.StepThree(ItemKind.Art) && ItemCardMath.StepThree(ItemKind.Book)
          && ItemCardMath.StepThree(ItemKind.Other), "step 3's nine kinds come after art");

        // ---------- The sums ----------
        C(ItemCardMath.XpToLevelUp(0) == 1000f && ItemCardMath.XpToLevelUp(9) == 10000f && ItemCardMath.XpToLevelUp(19) == 30000f && ItemCardMath.XpToLevelUp(14) == 20000f,
            "the game's experience a level asks: 1,000 at 0, 10,000 at 9, 30,000 at 19");
        C(Near(ItemCardMath.LevelAfter(6, 0f, 50000f), 11f + 4000f / 14000f), "50,000 experience from level 6 with an interest: level 11 and a bit");
        C(Near(ItemCardMath.LevelAfter(6, 0f, 50000f * 0.35f), 8f + 2500f / 9000f), "with no passion only 0.35 of it: level 8 and a bit");
        C(ItemCardMath.LevelAfter(19, 0f, 1e6f) == 20f && ItemCardMath.LevelAfter(20, 0f, 1000f) == 20f, "held at 20");
        C(Near(ItemCardMath.LevelAfter(3, 500f, 3500f, l => 4000f), 4f), "the game's own curve when passed in, and the experience already earned");
        C(ItemCardMath.PassionFactor(0) == 0.35f && ItemCardMath.PassionFactor(1) == 1f && ItemCardMath.PassionFactor(2) == 1.5f, "the game's learning by passion");
        C(Near(ItemCardMath.BookGain(300f, 1.5f, 6, 10, false), 450f) && ItemCardMath.BookGain(300f, 1.5f, 10, 10, false) == 0f && ItemCardMath.BookGain(300f, 1f, 2, 10, true) == 0f
          && Near(ItemCardMath.BookGain(300f, 1f, 15, 0, false), 300f), "a book's experience an hour: none past its level or for a skill they can't learn");
        C(Near(ItemCardMath.HungerFor(0), 1f) && Near(ItemCardMath.HungerFor(-5), 2.25f) && Near(ItemCardMath.HungerFor(5), 0.5f) && Near(ItemCardMath.HungerFor(-9), 2.25f),
            "hunger by metabolism, the game's curve's ends");
        C(Near(ItemCardMath.SurgeryChance(0.9f, 1.2f), 1f) && Near(ItemCardMath.SurgeryChance(0.8f, 0.5f), 0.4f) && Near(ItemCardMath.SurgeryChance(0.8f, 0f), 0.8f), "a surgery's chance, held at certain");
        C(ItemCardMath.PrintsLeft(1, 2) == 1 && ItemCardMath.PrintsLeft(3, 2) == 0, "techprints still needed");
        var ranked = ItemCardMath.Ranked(new[] { W("Cy", false, 5f, ""), W("Bo", true, 1f, ""), W("Al", true, 3f, ""), W("Di", true, 3f, "") });
        C(ranked.Select(c => c.Name).SequenceEqual(new[] { "Al", "Di", "Bo", "Cy" }), "who at home: those who can first, the most gain first, then by name");
        C(ItemCardMath.Best(new UseFacts { Who = ranked })?.Name == "Al" && ItemCardMath.Best(new UseFacts { Who = { W("Bo", true, 0f, "") } }) == null && ItemCardMath.Able(new UseFacts { Who = ranked }) == 3,
            "the best gains something");
        C(ItemCardMath.EffectWords("CompUseEffect_YouthSerum") == "youth serum" && ItemCardMath.EffectWords("CompTargetEffect_FreezeSolid") == "freeze solid", "a mod's effect in words from its class name");
        C(ItemCardMath.Whole(50000f) == "50,000" && ItemCardMath.Count(1, "gene", "genes") == "1 gene" && ItemCardMath.Count(3, "gene", "genes") == "3 genes", "numbers in words");

        // ---------- Books ----------
        var novel = Novel(false);
        C(Has(novel, "recreation") && Why(novel, "recreation") == "reading it is recreation, x1.4 a plain read's" && P(novel) == "Novel, excellent: recreation to read (x1.4).", "a novel: " + P(novel));
        var novelH = Novel(true);
        C(Has(novelH, "useful-here") && P(novelH) == "Novel, excellent: recreation to read (x1.4); Bo would enjoy it most.", "a novel at home, for the one who most wants recreation: " + P(novelH));
        var text = Textbook(false);
        C(Has(text, "teaches") && Why(text, "teaches") == "about 300 experience an hour in shooting and melee, up to level 10"
          && P(text) == "Textbook, good: teaches shooting and melee up to level 10, about 300 experience an hour.", "a textbook: " + P(text));
        var textH = Textbook(true);
        C(Why(textH, "useful-here").StartsWith("Bo: Shooting 6 (burning)") && P(textH).EndsWith("; Bo would gain most."), "a textbook at home: the burning passion gains most: " + P(textH));
        var textNone = Textbook(true);
        textNone.Use.Who = textNone.Use.Who.Where(c => !c.Can).ToList();
        C(Has(textNone, "useless-here") && !Has(textNone, "useful-here") && P(textNone).EndsWith("; nobody at home would gain from it."), "everyone past it or unable: no use here");
        var schem = Schematic(false, false);
        C(Has(schem, "gives-research") && !Has(schem, "research-done") && P(schem) == "Schematic, normal: research toward gun turrets.", "a schematic: " + P(schem));
        var schemDone = Schematic(true, true);
        C(Has(schemDone, "research-done") && !Has(schemDone, "gives-research") && P(schemDone).StartsWith("Schematic, normal: its research is known already"), "a schematic whose research is done: " + P(schemDone));
        var tome = Tome();
        C(Has(tome, "mental-break") && ItemCardMath.Tags(tome)[0].Id == "mental-break" && P(tome) == "Tome, normal: research toward basic, but a mental break risk.", "a tome: a break risk first: " + P(tome));
        var map = F("map", ItemKind.Book, new UseFacts { BookKind = "map", Quest = true });
        C(Has(map, "reveals-place") && P(map) == "Map: reading it reveals a place on the world.", "a map: " + P(map));
        var textKind = F("textbook", ItemKind.Book, new UseFacts { BookKind = "textbook" });
        textKind.InHand = false;
        C(P(textKind) == "Textbook: teaches the skills it was written on." && !Has(textKind, "teaches"), "a textbook as a kind: its skills are its own when written");

        // ---------- Body parts ----------
        var arm = BionicArm(false);
        C(Has(arm, "better-than-natural") && Why(arm, "better-than-natural") == "125% of a natural one" && Has(arm, "surgery") && Why(arm, "surgery") == "a doctor installs it, Medicine 5 or more",
            "a bionic arm: better than natural, surgery");
        C(P(arm) == "Bionic arm: better than a natural one (125%).", "a bionic arm: " + P(arm));
        var armH = BionicArm(true);
        C(Has(armH, "needed") && Why(armH, "needed") == "Bo is missing the part it replaces" && Has(armH, "faith-for") && Why(armH, "surgery").EndsWith("; Cy succeeds about 82% of the time")
          && P(armH) == "Bionic arm: better than a natural one (125%); someone at home is missing one.", "a bionic arm at home: " + P(armH));
        var kidney = Kidney(true);
        C(Has(kidney, "natural") && Why(kidney, "natural") == "a natural kidney: it works as one's own" && !Has(kidney, "better-than-natural") && !Has(kidney, "needed")
          && P(kidney) == "Kidney: a natural kidney, as good as one's own; nobody at home needs one.", "a natural kidney: " + P(kidney));
        C(Has(kidney, "useless-here"), "a kidney nobody needs: no use here");
        var peg = PegLeg(true);
        C(Has(peg, "worse-than-natural") && Why(peg, "worse-than-natural") == "60% of a natural one: better than none, worse than a natural one" && Has(peg, "faith-against")
          && ItemCardMath.Tags(peg)[0].Id == "faith-against" && P(peg) == "Peg leg: works at 60% of a natural one; nobody at home needs one.", "a peg leg: " + P(peg));
        var implant = F("joywire", ItemKind.BodyPart, new UseFacts { Part = "brain", Implant = true, Adds = { "Consciousness -20%", "Mood +30" }, Installable = true });
        C(Has(implant, "adds-ability") && P(implant) == "Joywire: an implant in the brain: Consciousness -20% and Mood +30.", "an implant: " + P(implant));
        var withAbility = F("arm blade", ItemKind.BodyPart, new UseFacts { Part = "shoulder", Efficiency = 1f, Abilities = { "a blade strike" }, Installable = true });
        C(Has(withAbility, "adds-ability") && Why(withAbility, "adds-ability") == "it gives a blade strike", "a part that gives an ability");

        // ---------- Techprints ----------
        var tp = Techprint(1, 2, false, true);
        C(Has(tp, "needed") && Why(tp, "needed") == "1 of 2 applied: Charged shot needs 1 more before it can be studied" && Has(tp, "unlocks") && !Has(tp, "trade-good")
          && P(tp) == "Charged shot techprint: 1 of 2 applied, 1 more needed, it opens 3 things.", "a techprint the project still needs: " + P(tp));
        var tpOther = Techprint(1, 2, false, false);
        tpOther.Label = "techprint";
        C(P(tpOther) == "Techprint: for Charged shot, 1 of 2 applied, 1 more needed, it opens 3 things.", "a techprint whose label doesn't name its project: " + P(tpOther));
        var tpAll = Techprint(2, 2, false, true);
        C(Has(tpAll, "research-done") && Has(tpAll, "spare") && !Has(tpAll, "needed") && P(tpAll).Contains("all applied, this one spare"), "every techprint applied: spare");
        var tpDone = Techprint(2, 2, true, true);
        C(Has(tpDone, "research-done") && Why(tpDone, "research-done") == "Charged shot is researched already" && !Has(tpDone, "unlocks") && P(tpDone).Contains("the research is done"), "the research done");
        var tpWaits = Techprint(0, 2, false, false);
        tpWaits.Use.Waits = "microelectronics";
        C(Has(tpWaits, "prerequisites") && Why(tpWaits, "prerequisites") == "Charged shot needs microelectronics first" && !Has(tpWaits, "needed"), "earlier research first; away from home nothing is needed");

        // ---------- Trainers ----------
        var neuro = Neurotrainer(false);
        C(Has(neuro, "teaches") && Has(neuro, "one-use") && Why(neuro, "teaches") == "50,000 experience in shooting at once, by the user's passion"
          && P(neuro) == "Shooting neurotrainer: 50,000 shooting experience at once, one use.", "a shooting neurotrainer: " + P(neuro));
        var neuroH = Neurotrainer(true);
        C(ItemCardMath.Best(neuroH.Use)?.Name == "Bo" && P(neuroH).EndsWith("; Bo gains most.") && Why(neuroH, "useful-here") == "Bo: level 6 (burning): to about 12.9", "at home the burning passion gains most: " + P(neuroH));
        C(Near(ItemCardMath.LevelAfter(6, 0f, 75000f), 12.9f, 0.05f), "a burning passion from level 6: about 12.9");
        var psy = Psytrainer(true, true);
        C(Has(psy, "psycast") && Why(psy, "psycast") == "stun, a level 1 psycast" && !Has(psy, "needs-psylink") && P(psy) == "Psytrainer (stun): teaches stun, a level 1 psycast, one use; Cy can learn it.",
            "a psytrainer: " + P(psy));
        var psyNone = Psytrainer(false, true);
        C(Has(psyNone, "needs-psylink") && Has(psyNone, "useless-here") && P(psyNone).EndsWith("; nobody at home has the psylink for it."), "nobody with the psylink: " + P(psyNone));
        C(Has(Psytrainer(false, false), "needs-psylink"), "away from home: a psylink is needed all the same");

        // ---------- Genes ----------
        var pack = Genepack(false, 0, true);
        C(Has(pack, "hungry") && Why(pack, "hungry") == "metabolism -2: hunger x1.5" && Has(pack, "gene-assembler") && Why(pack, "gene-assembler") == "the colony has no gene assembler to make a xenogerm of it"
          && P(pack) == "Genepack (robust, fast runner): 2 genes (robust, fast runner), complexity 4, metabolism -2; the colony has no gene assembler.", "a genepack: " + P(pack));
        var packFits = Genepack(true, 6, true);
        C(!Has(packFits, "gene-assembler") && P(packFits).EndsWith("metabolism -2."), "a genepack the assembler takes");
        var packBig = Genepack(true, 3, true);
        C(Why(packBig, "gene-assembler") == "complexity 4, more than the colony's assembler takes (3)", "a genepack too complex for the assembler");
        var xeno = Xenogerm(true);
        C(Has(xeno, "archite") && Has(xeno, "eats-less") && Has(xeno, "faith-against") && Has(xeno, "useful-here") && Why(xeno, "useful-here") == "Bo: now baseliner; metabolism +1 with it",
            "a xenogerm: archite, eats less, a faith against it, who it suits");
        C(P(xeno) == "Xenogerm (Ironhide): Ironhide, 5 genes (robust, tough skin, deathless, 2 more), complexity 11, metabolism +1, 1 archite capsule; suits Bo best.", "a xenogerm: " + P(xeno));
        var embryoKind = F("embryo", ItemKind.Genes, new UseFacts { GeneKind = "embryo" });
        C(P(embryoKind) == "Embryo: an embryo, its genes its parents'." && ItemCardMath.Tags(embryoKind).All(t => t.Id == "trade-good"), "an embryo as a kind: no mechanics, flavour only");

        // ---------- Usable items ----------
        var lance = ShockLance();
        C(Has(lance, "risky-use") && Why(lance, "risky-use") == "30% chance of brain damage to the target and goodwill -200 with the target's people" && !Has(lance, "one-use")
          && P(lance) == "Psychic shock lance: puts the target in psychic shock (one person or animal the user chooses), but 30% chance of brain damage to the target.", "a psychic shock lance's effects: " + P(lance));
        var pulser = AnimalPulser();
        C(Has(pulser, "risky-use") && Has(pulser, "one-use") && P(pulser) == "Psychic animal pulser: turns the animals manhunter (every animal on the map (those psychically sensitive)), one use, but manhunter animals attack anyone, the colony too.",
            "a psychic animal pulser: " + P(pulser));
        var serum = HealerSerum();
        C(Has(serum, "heals") && Has(serum, "one-use") && !Has(serum, "risky-use") && P(serum) == "Healer mech serum: cures the user's worst health condition (the user), one use.", "a healer mech serum: " + P(serum));
        C(ItemCardMath.Heals(serum.Use) && !ItemCardMath.Resurrects(serum.Use) && ItemCardMath.Resurrects(new UseFacts { Effects = { "CompTargetEffect_Resurrect" } }), "heals and brings back by their effects");
        var lanceHome = ShockLance();
        lanceHome.HasColony = true;
        lanceHome.Use.Who = ItemCardMath.Ranked(new[] { W("Bo", true, 1f, "can use it"), W("Cy", true, 1f, "can use it") });
        C(!Has(lanceHome, "useful-here") && !Has(lanceHome, "useless-here"), "a usable item names no one as gaining most");
        C(ItemCardMath.RiskyEffects.All(e => ItemCardMath.KnownEffects.Contains(e)) && ItemCardMath.KnownEffects.Distinct().Count() == ItemCardMath.KnownEffects.Length, "every risky effect is a known one");

        // ---------- Shells ----------
        var shell = Shell(2, true, true);
        C(Has(shell, "explosive") && Has(shell, "blast") && Why(shell, "blast") == "bomb, 50 in a radius of 2.9" && Has(shell, "for-launcher") && Why(shell, "for-launcher") == "the colony's 2 mortars fire it"
          && P(shell) == "High-explosive shell: bomb, 50 in a radius of 2.9; the colony's 2 mortars fire it.", "a high-explosive shell at home: " + P(shell));
        C(ItemCardMath.Tags(shell)[0].Id == "explosive", "its own explosion comes first");
        var shellNo = Shell(0, true, true);
        C(Has(shellNo, "no-launcher") && Why(shellNo, "no-launcher") == "nothing at home fires it; a mortar would" && P(shellNo).EndsWith("; nothing at home fires it."), "no mortar at home: " + P(shellNo));
        var shellAway = Shell(0, true, false);
        C(!Has(shellAway, "no-launcher") && P(shellAway) == "High-explosive shell: bomb, 50 in a radius of 2.9.", "away from home: its burst alone");

        // ---------- Furniture in a crate ----------
        var chair = Armchair(true);
        C(Has(chair, "comfortable") && Why(chair, "comfortable") == "comfort 80%" && Has(chair, "lifts-room") && Has(chair, "a-room")
          && P(chair) == "Armchair: comfort 80%, beauty 3; placed in the dining room it would make it mediocre.", "a minified armchair: " + P(chair));
        var chairAway = Armchair(false);
        C(!Has(chairAway, "a-room") && !Has(chairAway, "lifts-room") && P(chairAway) == "Armchair: comfort 80%, beauty 3.", "an armchair away from home");
        var bed = Bed(2, true);
        C(Has(bed, "needed") && Why(bed, "needed") == "2 at home sleep without a bed of their own: Bo and Cy" && Has(bed, "comfortable")
          && P(bed) == "Bed: a bed, rest 100%, comfort 75%, beauty 1; 2 at home sleep without a bed.", "a minified bed: " + P(bed));
        C(!Has(Bed(0, true), "needed") && P(Bed(0, true)) == "Bed: a bed, rest 100%, comfort 75%, beauty 1.", "everyone has a bed: nothing needed");
        var gen = Generator();
        C(Has(gen, "power-make") && Why(gen, "power-make") == "makes 1,000 W" && P(gen) == "Chemfuel powered generator: makes 1,000 W, beauty -10.", "a generator: " + P(gen));
        var lamp = F("standing lamp", ItemKind.Furniture, new UseFacts { Power = 75f });
        C(Has(lamp, "power-use") && Why(lamp, "power-use") == "uses 75 W", "a lamp needs power");
        var joy = F("chess table", ItemKind.Furniture, new UseFacts { JoyKind = "cerebral", JoyNew = true, Beauty = 2f }, 100f, 20f, true);
        C(Has(joy, "recreation") && Why(joy, "recreation") == "cerebral recreation, a kind the colony has none of yet" && P(joy).EndsWith("; a recreation the colony has none of."), "a joy building of a new kind: " + P(joy));
        var bench = F("electric tailor bench", ItemKind.Furniture, new UseFacts { WorkTable = true, WorkSpeed = 1f, Power = 120f, Facility = "" });
        C(!Has(bench, "work-speed") && Has(bench, "power-use"), "a bench at its plain speed isn't faster work");
        var shelf = F("tool cabinet", ItemKind.Furniture, new UseFacts { Facility = "Work table efficiency +6% to the tailor bench" });
        C(Has(shelf, "work-speed") && Why(shelf, "work-speed") == "Work table efficiency +6% to the tailor bench", "a facility's offsets");

        // ---------- Anything else ----------
        var uft = Unfinished();
        C(Has(uft, "unfinished") && Why(uft, "unfinished") == "charge rifle: 12,000 work left at a fabrication bench" && P(uft) == "Unfinished charge rifle: an unfinished charge rifle, 12,000 work left.", "an unfinished thing: " + P(uft));
        var waste = Wastepack(true);
        C(P(waste) == "Toxic wastepack: worth $0, 2 kg; 12 in store." && ItemCardMath.Tags(waste).Count == 0, "a plain item at home: " + P(waste));
        C(P(Wastepack(false)) == "Toxic wastepack: worth $0, 2 kg.", "a plain item away from home");
        var gem = F("jewel", ItemKind.Other, new UseFacts(), 500f, 0.5f);
        C(Has(gem, "trade-good"), "a precious trinket is a trade good");

        // ---------- The vocabulary ----------
        var ids = ItemCardMath.Vocabulary.Select(t => t.Id).ToList();
        C(ids.Distinct().Count() == ids.Count, "the one vocabulary: step 1's, step 2's and step 3's tags each their own");
        foreach (var id in new[] { "better-than-natural", "heals", "resurrects", "teaches", "gives-research", "psycast", "unlocks", "adds-ability", "blast", "power-make", "work-speed", "comfortable", "eats-less" })
            C(ItemCardMath.Def(id)?.Category == TagCategory.Strength, $"{id} is a strength");
        foreach (var id in new[] { "worse-than-natural", "research-done", "useless-here", "hungry", "one-use", "power-use" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Weakness, $"{id} is a weakness");
        foreach (var id in new[] { "mental-break", "risky-use", "faith-against" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Risk, $"{id} is a risk");
        foreach (var id in new[] { "natural", "archite", "reveals-place", "faith-for" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Special, $"{id} is special");
        foreach (var id in new[] { "useful-here", "for-launcher" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Role, $"{id} is a role");
        foreach (var id in new[] { "surgery", "prerequisites", "needs-psylink", "gene-assembler", "no-launcher", "unfinished" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Needs, $"{id} is a need");
        var all = new List<ItemFacts>
        {
            Novel(false), novelH, text, textH, textNone, schem, schemDone, tome, map, textKind, arm, armH, kidney, peg, implant, withAbility, tp, tpAll, tpDone, tpWaits, neuro, neuroH, psy, psyNone,
            pack, packFits, packBig, xeno, embryoKind, lance, pulser, serum, lanceHome, shell, shellNo, shellAway, chair, chairAway, bed, gen, lamp, joy, bench, shelf, uft, waste, gem
        };
        foreach (var f in all)
        {
            var tags = ItemCardMath.Tags(f);
            C(tags.Count <= ItemCardMath.MaxTags, $"{f.Label}: at most {ItemCardMath.MaxTags} tags");
            C(tags.All(t => ItemCardMath.Def(t.Id) != null && !string.IsNullOrEmpty(t.Why)), $"{f.Label}: every tag is of the vocabulary and says why");
            C(tags.Select(t => t.Importance).SequenceEqual(tags.Select(t => t.Importance).OrderByDescending(i => i)), $"{f.Label}: the tags in order of importance");
            C(tags.Select(t => t.Id).Distinct().Count() == tags.Count, $"{f.Label}: no tag twice");
            string plain = P(f);
            C(plain.EndsWith(".") && !plain.Contains(". ") && ItemCardMath.CleanWords(plain), $"{f.Label}: the line is one sentence with no em dash or exclamation mark");
            C(tags.All(t => ItemCardMath.CleanWords(t.Why) && ItemCardMath.CleanWords(t.Label)), $"{f.Label}: the tags' words are clean");
        }
        C(ItemCardMath.Vocabulary.All(t => ItemCardMath.CleanWords(t.Rule) && ItemCardMath.CleanWords(t.Label)), "the vocabulary's rules are clean words");

        // ---------- The seams ----------
        string math = Read("Source/ColonyCouncil/Items/ItemCardUseMath.cs");
        string live = Read("Source/ColonyCouncil/Items/ItemCardLive.cs");
        string uses = Read("Source/ColonyCouncil/Items/ItemCardUses.cs");
        string bodies = Read("Source/ColonyCouncil/Items/ItemCardBodies.cs");
        string things = Read("Source/ColonyCouncil/Items/ItemCardThings.cs");
        string window = Read("Source/ColonyCouncil/Items/ThingCardWindow.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_ItemCard.xml");
        C(!math.Contains("using Verse") && !math.Contains("using RimWorld") && !math.Contains("using UnityEngine") && Read("Tools/Tests/Tests.csproj").Contains("Items/ItemCardUseMath.cs"),
            "the step 3 rules are pure and tested here");
        C(live.Contains("StepThreeSort(d, s);") && live.Contains("t.ParentHolder is MinifiedThing") && live.Contains("d.IsWeapon || d.IsApparel") && live.Contains("d.IsCorpse"),
            "the sorting: items by their class and comps, a crate's piece as furniture, never a weapon, apparel or a corpse");
        C(uses.Contains("typeof(Book).IsAssignableFrom(cls)") && uses.Contains("GetCompProperties<CompProperties_Readable>()") && uses.Contains("GetCompProperties<CompProperties_Techprint>()")
          && uses.Contains("CompProperties_UseEffect_LearnSkill") && uses.Contains("CompProperties_UseEffect_GainAbility") && uses.Contains("typeof(GeneSetHolderBase).IsAssignableFrom(cls)")
          && uses.Contains("GetCompProperties<CompProperties_Usable>()") && uses.Contains("d.isTechHediff") && uses.Contains("d.IsShell"),
            "every kind read from the defs and comps, so the player's mods' books, trainers, parts and items come in on their own");
        C(Regex.Matches(uses + bodies + things, @"DefDatabase<RecipeDef>\.AllDefsListForReading").Count == 1 && bodies.Contains("private static void ReadBodyPart(ThingDef d, UsePart up)")
          && Regex.Matches(uses, @"ReadBodyPart\(p\.def, up\);").Count == 1 && Regex.Matches(things, @"Avail\.AllBuildings").Count == 1 && things.Contains("private static void ReadShell(ThingDef d, UsePart up)"),
            "every recipe and building is looked through once a kind (a part's install recipes, a shell's guns), when its numbers are first read");
        C(live.Contains("else if (ItemCardMath.StepThree(m.kind)) ColonyUse(m, p);") && live.Contains("if (colony)") && uses.Contains("if (!m.useRead)"),
            "the colony's part only on the ten seconds' refresh; the thing's own once a card");
        int bodyAt = window.IndexOf("protected override float Body", StringComparison.Ordinal);
        string body = bodyAt >= 0 ? window.Substring(bodyAt) : "";
        C(bodyAt > 0 && body.Contains("UseParts(w, y, m)") && !body.Contains("ItemCardLive.") && !body.Contains("DefDatabase") && !body.Contains("listerBuildings") && !body.Contains("mapPawns"),
            "the card draws step 3's kinds from the model alone");
        C(uses.Contains("BookUtility.CanReadEver(x)") && uses.Contains("SkillRecord.XpRequiredToLevelUpFrom") && uses.Contains("rec.LearnRateFactor(true)") && uses.Contains("CanBeUsedBy(x, false, true)"),
            "who at home by the game's own rules: who reads, the experience curve, learning by passion, who may use it");
        C(bodies.Contains("recipe.Worker.GetPartsToApplyOn(x, recipe)") && bodies.Contains("PawnCapacityUtility.CalculatePartEfficiency(hs, part)") && bodies.Contains("HistoryEventDefOf.InstalledProsthetic")
          && bodies.Contains("xg.PawnIdeoDisallowsImplanting(x)") && bodies.Contains("GeneUtility.MetabolismAfterImplanting(x, xg.GeneSet)"),
            "parts and genes by the game's own rules: the recipe's parts, the part's efficiency, a faith's thoughts on a prosthetic, a faith's word on a xenogerm, metabolism after it");
        foreach (var k in Enum.GetValues(typeof(ItemKind)).Cast<ItemKind>().Where(ItemCardMath.StepThree))
            C(keys.Contains("<CC_ItemCard_Sec_Use_" + k + ">") && keys.Contains("<CC_ItemCard_Sec_Who_" + k + ">"), $"the {k} card's sections have their words");
        foreach (var b in new[] { "textbook", "schematic", "tome", "map", "novel", "book" }) C(keys.Contains("<CC_ItemCard_Kind_Book_" + b + ">"), $"a {b}'s kind has its words");
        foreach (var e in ItemCardMath.KnownEffects) C(keys.Contains("<CC_ItemCard_Does_" + e + ">"), $"what {e} does has its words");
        foreach (var t in ItemCardMath.KnownTargets) C(keys.Contains("<CC_ItemCard_Targets_" + t + ">"), $"{t}'s targets have their words");
        foreach (var r in ItemCardMath.RiskyEffects) C(keys.Contains("<CC_ItemCard_Risk_" + r + ">"), $"{r}'s risk has its words");
        foreach (var room in new[] { "Bed", "Table", "Joy", "Bench", "Seat" }) C(keys.Contains("<CC_ItemCard_RoomNeeds_" + room + ">"), $"what a {room}'s room needs has its words");
        C(keys.Contains("<CC_SetNote_ItemCardFirst>Every item opens as Rimfolk's item card; weapons and apparel keep theirs")
          && keys.Contains("a book, a body part, a techprint, a neurotrainer or psytrainer, a genepack, xenogerm or embryo, something to use")
          && keys.Contains("a shell, furniture in a crate, and anything else") && keys.Contains("Weapons and apparel keep their own cards; corpses keep the game's."),
            "the setting's note says every item, its tip names every kind and what stays the game's");
        C(!Regex.IsMatch(keys, @"<CC_Settings_ItemCardFirst_Tip>[^<]*comes later"), "the setting's tip no longer holds the rest back");
        C(!keys.Contains("—") && !Regex.IsMatch(keys, @"<CC_[A-Za-z_]+>[^<]*!"), "no em dashes or exclamation marks in the keys");
        return n;
    }
}
