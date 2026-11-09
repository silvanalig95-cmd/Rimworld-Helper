using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rimfolk's item card, step 2 (the player, 9 November 2026: "also like item card for everything"; docs/design/rimfolk-look.md,
/// stage 3, point 5): materials and art. The rules (Items/ItemCardMaterialMath.cs) on the base game's own, by hand from their
/// defs: the kinds; the stuff comparison and its sums (what it is compared with, the table, a unit's weight, the worth of
/// what is made of it, repairs, fuel, short and spare); the tags and the line for steel, wood, plasteel, uranium, gold,
/// silver, jade, cloth, devilstrand, hyperweave, plainleather, granite blocks, a granite chunk, components, advanced
/// components and chemfuel; the game's room sums and a grand sculpture at several qualities in a room with it and without
/// it, placed, outdoors and in storage; and the seams (the hook names the new kinds, every key, the model built rarely).
/// </summary>
public static class ItemCardStepTwoTests
{
    // ---------------- The base game's stuffs (their defs' numbers: stuff powers, stat factors, damage, worth and weight) ----------------

    public static StuffNumbers Steel() => new StuffNumbers
    {
        Def = "Steel", Label = "steel", Categories = { "Metallic" }, ArmorSharp = 0.9f, ArmorBlunt = 0.45f, ArmorHeat = 0.6f, InsulationCold = 3f, Apparel = true, Flammability = 0.4f,
        Weapons = true, Value = 1.9f, Mass = 0.5f
    };

    public static StuffNumbers Wood() => new StuffNumbers
    {
        Def = "WoodLog", Label = "wood", Categories = { "Woody" }, ArmorSharp = 0.54f, ArmorBlunt = 0.54f, ArmorHeat = 0.4f, InsulationCold = 8f, InsulationHeat = 4f, Apparel = true,
        HitPoints = 0.65f, Flammability = 1f, WorkToMake = 0.7f, WorkToBuild = 0.7f, Weapons = true, SharpDamage = 0.4f, BluntDamage = 0.9f, Value = 1.2f, Mass = 0.4f
    };

    public static StuffNumbers Plasteel() => new StuffNumbers
    {
        Def = "Plasteel", Label = "plasteel", Categories = { "Metallic" }, ArmorSharp = 1.14f, ArmorBlunt = 0.55f, ArmorHeat = 0.65f, InsulationCold = 3f, Apparel = true, HitPoints = 2.8f,
        Flammability = 0f, WorkToMake = 2.2f, WorkToBuild = 2.2f, Weapons = true, SharpDamage = 1.1f, BluntDamage = 0.9f, Value = 9f, Mass = 0.25f
    };

    public static StuffNumbers Uranium() => new StuffNumbers
    {
        Def = "Uranium", Label = "uranium", Categories = { "Metallic" }, ArmorSharp = 1.08f, ArmorBlunt = 0.54f, ArmorHeat = 0.65f, InsulationCold = 3f, Apparel = true, HitPoints = 2.5f,
        Beauty = 0.5f, Flammability = 0f, WorkToMake = 1.9f, WorkToBuild = 1.9f, Weapons = true, SharpDamage = 1.1f, BluntDamage = 1.5f, Value = 6f, Mass = 1f
    };

    public static StuffNumbers Gold() => new StuffNumbers
    {
        Def = "Gold", Label = "gold", Categories = { "Metallic" }, ArmorSharp = 0.36f, ArmorBlunt = 0.18f, ArmorHeat = 0.3f, InsulationCold = 3f, Apparel = true, HitPoints = 0.6f, Beauty = 4f,
        Flammability = 0.4f, Weapons = true, SharpDamage = 0.6f, BluntDamage = 1f, Value = 10f, Mass = 0.008f
    };

    public static StuffNumbers Silver() => new StuffNumbers
    {
        Def = "Silver", Label = "silver", Categories = { "Metallic" }, ArmorSharp = 0.45f, ArmorBlunt = 0.2f, ArmorHeat = 0.3f, InsulationCold = 3f, Apparel = true, HitPoints = 0.7f, Beauty = 2f,
        Flammability = 0.4f, Weapons = true, SharpDamage = 0.5f, BluntDamage = 0.8f, Value = 1f, Mass = 0.008f
    };

    public static StuffNumbers Jade() => new StuffNumbers
    {
        Def = "Jade", Label = "jade", Categories = { "Stony" }, HitPoints = 0.6f, Beauty = 2.5f, Flammability = 0f, WorkToMake = 1.3f, WorkToBuild = 1.5f, Value = 5f, Mass = 0.5f
    };

    public static StuffNumbers Cloth() => new StuffNumbers
    {
        Def = "Cloth", Label = "cloth", Categories = { "Fabric" }, Hard = false, ArmorSharp = 0.36f, ArmorHeat = 0.18f, InsulationCold = 18f, InsulationHeat = 18f, Apparel = true,
        Flammability = 1.2f, Value = 1.5f, Mass = 0.026f
    };

    public static StuffNumbers Devilstrand() => new StuffNumbers
    {
        Def = "DevilstrandCloth", Label = "devilstrand", Categories = { "Fabric" }, Hard = false, ArmorSharp = 1.4f, ArmorBlunt = 0.36f, ArmorHeat = 3f, InsulationCold = 20f, InsulationHeat = 24f,
        Apparel = true, HitPoints = 1.3f, Beauty = 3.2f, Flammability = 0.4f, Value = 5.5f, Mass = 0.032f
    };

    public static StuffNumbers Hyperweave() => new StuffNumbers
    {
        Def = "Hyperweave", Label = "hyperweave", Categories = { "Fabric" }, Hard = false, ArmorSharp = 2f, ArmorBlunt = 0.54f, ArmorHeat = 2.88f, InsulationCold = 26f, InsulationHeat = 26f,
        Apparel = true, HitPoints = 2.4f, Beauty = 5.5f, Flammability = 0.4f, Value = 9f, Mass = 0.038f
    };

    public static StuffNumbers Plainleather() => new StuffNumbers
    {
        Def = "Leather_Plain", Label = "plainleather", Categories = { "Leathery" }, Hard = false, ArmorSharp = 0.81f, ArmorBlunt = 0.24f, ArmorHeat = 1.5f, InsulationCold = 16f,
        InsulationHeat = 16f, Apparel = true, HitPoints = 1.3f, Value = 2.1f, Mass = 0.03f
    };

    public static StuffNumbers Granite() => new StuffNumbers
    {
        Def = "BlocksGranite", Label = "granite blocks", Categories = { "Stony" }, HitPoints = 1.7f, Flammability = 0f, WorkToMake = 1.3f, WorkToBuild = 6f, Weapons = true, SharpDamage = 0.65f,
        Value = 0.9f, Mass = 1.25f
    };

    /// <summary>A material's facts: its stuff and the two it is compared with, by RefsFor's own order.</summary>
    public static ItemFacts Material(string label, StuffNumbers stuff, string noun, params StuffNumbers[] refs) => new ItemFacts
    {
        Label = label, Kind = ItemKind.Material, Value = stuff?.Value ?? 0f, Mass = stuff?.Mass ?? 0f, RotDays = -1f,
        Material = new MaterialFacts { Stuff = stuff, Noun = noun, Refs = refs.ToList() }
    };

    /// <summary>The colony's side: people, what it holds, what it can make now, the trade advice's reserve for a material of so many people.</summary>
    public static ItemFacts Home(ItemFacts f, int held, TradeGood good, int people = 6, int buildings = 0, int apparel = 0, int weapons = 0, int others = 0)
    {
        f.HasColony = true;
        var m = f.Material;
        m.Held = held;
        m.Buildings = buildings;
        m.Apparel = apparel;
        m.Weapons = weapons;
        m.Others = others;
        m.Counted = TradeAdviceMath.Counted(good, 0f);
        m.Need = TradeAdviceMath.Need(good, people, 0f);
        m.Keep = TradeAdviceMath.Keep(good, people, 0f, 0);
        return f;
    }

    public static ItemFacts SteelF() => Material("steel", Steel(), "metal", Wood(), Granite());
    public static ItemFacts WoodF() => Material("wood", Wood(), "wood", Steel(), Granite());

    public static ItemFacts PlasteelF()
    {
        var f = Material("plasteel", Plasteel(), "metal", Steel(), Wood());
        f.Material.SturdiestOfKind = true;
        return f;
    }

    public static ItemFacts UraniumF() => Material("uranium", Uranium(), "metal", Steel(), Wood());

    public static ItemFacts GoldF()
    {
        var f = Material("gold", Gold(), "metal", Steel(), Wood());
        f.Material.Precious = f.Material.Gift = true;
        return f;
    }

    public static ItemFacts SilverF()
    {
        var f = Material("silver", Silver(), "metal", Steel(), Wood());
        f.Material.Precious = f.Material.Gift = f.Material.Money = true;
        return f;
    }

    public static ItemFacts JadeF()
    {
        var f = Material("jade", Jade(), "stone", Granite(), Steel());
        f.Material.Precious = f.Material.Gift = true;
        return f;
    }
    public static ItemFacts ClothF() => Material("cloth", Cloth(), "fabric", Plainleather(), Devilstrand());
    public static ItemFacts DevilstrandF() => Material("devilstrand", Devilstrand(), "fabric", Cloth(), Plainleather());
    public static ItemFacts HyperweaveF() => Material("hyperweave", Hyperweave(), "fabric", Cloth(), Plainleather());
    public static ItemFacts PlainleatherF() => Material("plainleather", Plainleather(), "leather", Cloth(), Devilstrand());
    public static ItemFacts GraniteF() => Material("granite blocks", Granite(), "stone", Steel(), Wood());

    public static ItemFacts ChunkF() => new ItemFacts
    {
        Label = "granite chunk", Kind = ItemKind.Material, Value = 0f, Mass = 20f, Tradeable = false, RotDays = -1f,
        Material = new MaterialFacts { Noun = "chunk", Chunk = true, RawInto = "granite blocks", RawBench = "stonecutter's table", RawBenchBuilt = true }
    };

    public static ItemFacts ComponentF() => new ItemFacts
    {
        Label = "component", Kind = ItemKind.Material, Value = 32f, Mass = 0.6f, RotDays = -1f,
        Material = new MaterialFacts { Noun = "material", Makeable = true, Bench = "fabrication bench", BenchBuilt = true, BreakdownDays = 228f }
    };

    public static ItemFacts AdvancedComponentF() => new ItemFacts
    {
        Label = "advanced component", Kind = ItemKind.Material, Value = 200f, Mass = 0.6f, RotDays = -1f,
        Material = new MaterialFacts { Noun = "material", Makeable = true, Bench = "fabrication bench", BenchBuilt = false, BreakdownDays = 228f }
    };

    public static ItemFacts ChemfuelF() => new ItemFacts
    {
        Label = "chemfuel", Kind = ItemKind.Material, Value = 2.3f, Mass = 1f, RotDays = -1f,
        Material = new MaterialFacts { Noun = "material", Explosive = true, FuelAnywhere = true, Makeable = true, Bench = "refinery", BenchBuilt = true }
    };

    // ---------------- Art ----------------

    /// <summary>The base game's impressiveness stage of a score (RoomStatDef Impressiveness), for the facts made here.</summary>
    public static (int index, string word) Stage(float score)
    {
        int i = ItemCardMath.StageIndex(score, ItemCardMath.ImpressStages);
        return (i, ItemCardMath.ImpressWords[i]);
    }

    /// <summary>
    /// A grand sculpture of a quality, placed in a room (given as it is without the piece) or not placed (the room it would
    /// go to, as it is now), its room's numbers with it and without it by the game's sums.
    /// </summary>
    public static ItemFacts Sculpture(string quality, int qualityIndex, float beauty, float worth, RoomNumbers? room, bool placed = true, string roomWord = "dining room")
    {
        var a = new ArtFacts { Quality = quality, QualityIndex = qualityIndex, Beauty = beauty, Worth = worth, Flammability = 0f, Placed = placed };
        var f = new ItemFacts { Label = "grand sculpture", Kind = ItemKind.Art, InHand = true, Value = worth, Mass = 50f, RotDays = -1f, ArtPiece = a, HasColony = true };
        if (room.HasValue && placed)
        {
            // The game keeps the room with the piece in it; Rimfolk works back to it without.
            var with = ItemCardMath.With(room.Value, beauty, worth, ItemCardMath.BeautyDivisor(room.Value.Cells));
            var without = ItemCardMath.Without(with, beauty, worth, ItemCardMath.BeautyDivisor(with.Cells));
            a.Indoors = true;
            a.Room = roomWord;
            a.Role = "DiningRoom";
            a.RoomMatters = true;
            a.BeautyWith = with.Beauty;
            a.BeautyWithout = without.Beauty;
            a.ImpressWith = with.Impressiveness;
            a.ImpressWithout = without.Impressiveness;
            (a.StageIndexWith, a.StageWith) = Stage(with.Impressiveness);
            (a.StageIndexWithout, a.StageWithout) = Stage(without.Impressiveness);
        }
        else if (room.HasValue)
        {
            var now = room.Value;
            var with = ItemCardMath.With(now, beauty, worth, ItemCardMath.BeautyDivisor(now.Cells));
            a.BestRoom = roomWord;
            a.BestNow = now.Impressiveness;
            a.BestWith = with.Impressiveness;
            (a.BestIndexNow, a.BestStageNow) = Stage(now.Impressiveness);
            (a.BestIndexWith, a.BestStageWith) = Stage(with.Impressiveness);
        }
        return f;
    }

    /// <summary>A room as the game keeps its numbers, its impressiveness by the game's own sum.</summary>
    public static RoomNumbers Room(float wealth, float beauty, float space, int cells, float cleanliness = 0f) => new RoomNumbers
    {
        Wealth = wealth, Beauty = beauty, Space = space, Cells = cells, Cleanliness = cleanliness, Impressiveness = ItemCardMath.Impressiveness(wealth, beauty, space, cleanliness)
    };

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
            check(ok, "item card, materials and art: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        bool Has(ItemFacts f, string id) => ItemCardMath.Tags(f).Any(t => t.Id == id);
        string Why(ItemFacts f, string id) => ItemCardMath.AllTags(f).FirstOrDefault(t => t.Id == id)?.Why ?? "";
        string P(ItemFacts f) => ItemCardMath.Plain(f);
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The kinds ----------
        C(ItemCardMath.KindOf(new ItemSort { Stuff = true }) == ItemKind.Material, "stuff is a material (steel, wood, cloth, a mod's beeswax)");
        C(ItemCardMath.KindOf(new ItemSort { Resource = true }) == ItemKind.Material, "a raw or manufactured resource or a chunk is a material (components, chemfuel, neutroamine, granite chunks)");
        C(ItemCardMath.KindOf(new ItemSort { Resource = true, ToUse = true }) == ItemKind.None, "a resource to use, read or install (a book, a techprint, a body part, a shell) stays the game's for now");
        C(ItemCardMath.KindOf(new ItemSort { Art = true }) == ItemKind.Art && ItemCardMath.KindOf(new ItemSort { Art = true, Stuff = true }) == ItemKind.Art, "anything with the art comp is art");
        C(ItemCardMath.KindOf(new ItemSort { Medicine = true, Resource = true }) == ItemKind.Medicine && ItemCardMath.KindOf(new ItemSort { Drug = true, Resource = true }) == ItemKind.Drug,
            "step 1's kinds come first: medicine and drugs filed as manufactured keep their cards");
        C(ItemCardMath.KindOf(new ItemSort { Feeds = true, Stuff = true }) == ItemKind.Food, "food that is also stuff (a mod's gingerbread) is food");
        C(ItemCardMath.KindOf(new ItemSort()) == ItemKind.None && ItemCardMath.KindOf((ItemSort)null) == ItemKind.None, "anything else opens the game's card");
        C(ItemCardMath.KindOf(true, false, false, false) == ItemKind.Medicine && ItemCardMath.KindOf(false, false, false, true) == ItemKind.Food, "step 1's own rule is unchanged");

        // ---------- The stuff comparison ----------
        C(ItemCardMath.RefsFor("Plasteel", true, "Steel").SequenceEqual(new[] { "Steel", "WoodLog" }), "plasteel against the colony's steel and wood");
        C(ItemCardMath.RefsFor("Steel", true, null).SequenceEqual(new[] { "WoodLog", "BlocksGranite" }), "steel against wood and granite blocks");
        C(ItemCardMath.RefsFor("WoodLog", true, null).SequenceEqual(new[] { "Steel", "BlocksGranite" }), "wood against steel and granite blocks");
        C(ItemCardMath.RefsFor("BlocksMarble", true, "BlocksGranite").SequenceEqual(new[] { "BlocksGranite", "Steel" }), "marble against the colony's most held stone, then steel");
        C(ItemCardMath.RefsFor("Cloth", false, null).SequenceEqual(new[] { "Leather_Plain", "DevilstrandCloth" }), "cloth against plainleather and devilstrand");
        C(ItemCardMath.RefsFor("DevilstrandCloth", false, "Cloth").SequenceEqual(new[] { "Cloth", "Leather_Plain" }), "devilstrand against the colony's cloth and plainleather");
        C(ItemCardMath.RefsFor("Leather_Bear", false, "Leather_Plain").SequenceEqual(new[] { "Leather_Plain", "Cloth" }), "a mod's or the game's other leather against the colony's plainleather and cloth");
        C(ItemCardMath.IsHard(new[] { "Metallic" }) && ItemCardMath.IsHard(new[] { "RB_Waxy", "Metallic" }) && !ItemCardMath.IsHard(new[] { "Fabric" }) && !ItemCardMath.IsHard(new[] { "Fabric", "Leathery" }),
            "hard is metal, wood, stone and a mod's like them; fabric and leather are soft");
        C(ItemCardMath.NounFor(new[] { "Metallic" }, null, false) == "metal" && ItemCardMath.NounFor(new[] { "Woody" }, null, false) == "wood" && ItemCardMath.NounFor(new[] { "Stony" }, null, false) == "stone"
          && ItemCardMath.NounFor(new[] { "Fabric" }, new[] { "Textiles" }, false) == "fabric" && ItemCardMath.NounFor(new[] { "Leathery" }, null, false) == "leather"
          && ItemCardMath.NounFor(new[] { "Fabric", "Leathery" }, null, false) == "fabric" && ItemCardMath.NounFor(new[] { "Fabric" }, new[] { "Wools" }, false) == "wool"
          && ItemCardMath.NounFor(null, new[] { "StoneChunks" }, true) == "chunk" && ItemCardMath.NounFor(new[] { "RB_Waxy" }, null, false) == "material", "a material's noun by its stuff categories and the game's filing");
        C(ItemCardMath.NounPhrase("wood") == "timber" && ItemCardMath.NounPhrase("wool") == "wool" && ItemCardMath.NounPhrase("metal") == "a metal", "timber and wool without an article");
        var table = ItemCardMath.StuffTable(Plasteel(), new[] { Steel(), Wood() });
        StuffRow Row(List<StuffRow> rows, string id) => rows.FirstOrDefault(r => r.Id == id);
        C(table.Select(r => r.Id).SequenceEqual(new[] { "ArmorSharp", "ArmorBlunt", "ArmorHeat", "InsulationCold", "InsulationHeat", "HitPoints", "Beauty", "Flammability", "MeleeSharp", "MeleeBlunt", "WorkToBuild", "WorkToMake", "Worth", "Weight" }),
            "plasteel's table: armour and insulation (apparel stuff), hit points, beauty, flammability, melee (weapon stuff), work, worth and weight");
        C(Row(table, "HitPoints").Values.SequenceEqual(new[] { "x2.8", "x1", "x0.65" }) && Row(table, "HitPoints").Tone == 1, "hit points: plasteel's x2.8 beside steel's x1 and wood's x0.65, the best of them");
        C(Row(table, "Flammability").Values.SequenceEqual(new[] { "0%", "40%", "100%" }) && Row(table, "Flammability").Tone == 1, "flammability: less is better");
        C(Row(table, "WorkToBuild").Values.SequenceEqual(new[] { "x2.2", "x1", "x0.7" }) && Row(table, "WorkToBuild").Tone == -1, "work to build: more is worse");
        C(Row(table, "ArmorSharp").Values.SequenceEqual(new[] { "114%", "90%", "54%" }) && Row(table, "InsulationCold").Values[0] == "3 °C", "armour as shares, insulation in degrees");
        C(Row(table, "Worth").Values.SequenceEqual(new[] { "$9", "$1.9", "$1.2" }) && Row(table, "Worth").Tone == 0, "worth a unit, neither better nor worse");
        C(Row(table, "Weight").Values.SequenceEqual(new[] { "0.25 kg", "0.5 kg", "0.4 kg" }) && Row(table, "Weight").Tone == 1, "weight a unit: lighter is better");
        C(Row(table, "MeleeBlunt").Tone == 0 && Row(table, "MeleeSharp").Tone == 1, "between the others it is neither");
        var granite = ItemCardMath.StuffTable(Granite(), new[] { Steel(), Wood() });
        C(Row(granite, "ArmorSharp") == null && Row(granite, "MeleeSharp") != null && Row(granite, "WorkToBuild").Values[0] == "x6", "granite blocks: no armour rows (no apparel of it), melee rows, six times the work to build");
        var cloth = ItemCardMath.StuffTable(Cloth(), new[] { Plainleather(), Devilstrand() });
        C(Row(cloth, "MeleeSharp") == null && Row(cloth, "InsulationCold").Values.SequenceEqual(new[] { "18 °C", "16 °C", "20 °C" }), "cloth: no melee rows, insulation against leather and devilstrand");
        var gold = Gold();
        gold.BeautyOffset = 6f;
        C(Row(ItemCardMath.StuffTable(gold, new[] { Steel(), Wood() }), "BeautyAdded")?.Values[0] == "+6" && Row(table, "BeautyAdded") == null, "a beauty offset gets its row only where one of them has one");
        C(ItemCardMath.StuffRowIds.Distinct().Count() == ItemCardMath.StuffRowIds.Length && table.All(r => ItemCardMath.StuffRowIds.Contains(r.Id)), "every row is one of the known ids");

        // ---------- The sums ----------
        C(Near(ItemCardMath.MadeWorth(0f, 50, 9f, 0f, 1000f, 0.0036f), 453.6f) && Near(ItemCardMath.MadeWorth(300f, 50, 9f, 10f, 1000f, 0.0036f), 300f),
            "what a thing made of it is worth: its own price, else the stuff, the other ingredients and the work (the game's sum)");
        C(Near(ItemCardMath.RepairsASeason(14, 228f), 0.92f) && ItemCardMath.RepairsASeason(0, 228f) == 0f, "components for repairs: one a breakdown, 14 machines about 0.9 a season");
        C(Near(ItemCardMath.DaysOf(300, 9f), 33.33f) && ItemCardMath.DaysOf(300, 0f) < 0f, "the fuel's days at so much a day");
        C(Near(ItemCardMath.UseADay(new MaterialFacts { UseADay = 12f, BurnADay = 9f }), 12f) && Near(ItemCardMath.UseADay(new MaterialFacts { BurnADay = 9f }), 9f),
            "the colony's record of use first, else what burns it all the time");
        var plans = new MaterialFacts { Held = 340, BuildWants = 400, Counted = true, Need = 30, Keep = 60 };
        C(ItemCardMath.ShortBy(plans) == 60 && ItemCardMath.NeededNow(plans), "the blueprints need 400, the colony holds 340: short by 60, needed now");
        var floor = new MaterialFacts { Held = 90, Counted = true, Need = 150, Keep = 300 };
        C(ItemCardMath.ShortBy(floor) == 60 && !ItemCardMath.NeededNow(floor), "under the trade advice's floor alone: short on the card, not needed now");
        floor.UseADay = 2f;
        C(ItemCardMath.NeededNow(floor), "under the need of a stock the colony uses: needed now");
        C(ItemCardMath.ShortBy(new MaterialFacts { Held = 0, Counted = true, Need = 30 }) == 0, "a stock the colony never kept isn't short by the floor (the advice never starts one)");
        C(ItemCardMath.NeededNow(new MaterialFacts { Held = 12, Counted = true, Need = 18, Machines = 14 }), "components the machines need: needed now");
        C(ItemCardMath.NeededNow(new MaterialFacts { Held = 40, BillsStarved = 1 }), "a bill that can't start for want of it: needed now");
        C(ItemCardMath.Spare(new MaterialFacts { Held = 1250, Counted = true, Need = 300, Keep = 600, BuildWants = 300 }) && !ItemCardMath.Spare(new MaterialFacts { Held = 800, Counted = true, Need = 300, Keep = 600, BuildWants = 300 }),
            "spare beyond the advice's keep and the plans' share");
        C(ItemCardMath.Money(1400f) == "$1,400" && ItemCardMath.Money(9f) == "$9" && ItemCardMath.Money(1.9f) == "$1.9" && ItemCardMath.Times(2.8f) == "x2.8" && ItemCardMath.Kg(1.25f) == "1.25 kg"
          && ItemCardMath.Signed(6f) == "+6" && ItemCardMath.Degrees(18f) == "18 °C", "worth, factors, weights and degrees in words");
        C(ItemCardMath.AndList(new[] { "a" }) == "a" && ItemCardMath.AndList(new[] { "a", "b" }) == "a and b" && ItemCardMath.AndList(new[] { "a", "b", "c" }) == "a, b and c", "words joined");

        // ---------- The materials: tags and line, on their own ----------
        var steelF = SteelF();
        C(!Has(steelF, "sturdy") && !Has(steelF, "soft") && !Has(steelF, "burns") && !Has(steelF, "fireproof") && P(steelF) == "Steel: a metal.", "steel is the yard: none of the strengths or weaknesses measured against it");
        var woodF = WoodF();
        C(Has(woodF, "soft") && Has(woodF, "burns") && Why(woodF, "soft") == "things made of it have x0.65 hit points (steel x1): they break sooner", "wood: soft and flammable, against steel");
        var plasteelF = PlasteelF();
        C(Has(plasteelF, "sturdy") && Has(plasteelF, "fireproof") && Has(plasteelF, "light") && Has(plasteelF, "strong-armour") && Has(plasteelF, "slow-work") && Has(plasteelF, "sharp-blades"),
            "plasteel: sturdy, fireproof, light, strong armour, sharp blades, slow to work");
        C(P(plasteelF) == "Plasteel: the sturdiest metal there is, light and fireproof, but slow to work.", "plasteel in plain words: " + P(plasteelF));
        C(Why(plasteelF, "strong-armour") == "armour made of it has a sharp armour power of 114% (steel 90%), blunt 55%", "strong armour from its own stuff power against steel's");
        var uraniumF = UraniumF();
        C(Has(uraniumF, "heavy") && Has(uraniumF, "hard-blows") && Has(uraniumF, "sturdy") && P(uraniumF) == "Uranium: a metal, fireproof and sturdy, but heavy and slow to work.", "uranium: sturdy and heavy, hard blows: " + P(uraniumF));
        var goldF = GoldF();
        C(Has(goldF, "precious") && Has(goldF, "gift") && Has(goldF, "beautiful") && Has(goldF, "soft") && !Has(goldF, "light") && P(goldF) == "Gold: a precious metal, but soft.",
            "gold: precious, a gift between peoples, beautiful and soft, never called light: " + P(goldF));
        var silverF = SilverF();
        C(Has(silverF, "money") && !Has(silverF, "trade-good") && P(silverF) == "Silver: the money traders pay and ask in.", "silver is the money, never a trade good of its own");
        var jadeF = JadeF();
        C(Has(jadeF, "precious") && Has(jadeF, "fireproof") && Why(jadeF, "soft").Contains("(granite blocks x1.7)") && P(jadeF) == "Jade: a precious stone, fireproof, but soft and slow to work.",
            "jade against the colony's stone: " + P(jadeF));
        var clothF = ClothF();
        C(Has(clothF, "burns") && !Has(clothF, "warm") && Has(clothF, "trade-good") && P(clothF) == "Cloth: a fabric, but flammable.", "cloth: flammable, not warm enough to say (18 °C)");
        var devF = DevilstrandF();
        C(Has(devF, "strong-armour") && Has(devF, "warm") && Has(devF, "keeps-cool") && Has(devF, "beautiful") && !Has(devF, "burns") && !Has(devF, "light") && P(devF) == "Devilstrand: a fabric, beautiful and warm.",
            "devilstrand: strong armour, warm, cool, beautiful; weight never said for cloth");
        var hyperF = HyperweaveF();
        C(Has(hyperF, "strong-armour") && Has(hyperF, "sturdy") && Why(hyperF, "warm") == "clothes of it insulate 26 °C against the cold (cloth 18 °C)", "hyperweave: warm against cloth's 18 °C");
        var leatherF = PlainleatherF();
        C(Has(leatherF, "burns") && !Has(leatherF, "strong-armour") && P(leatherF) == "Plainleather: a leather, but flammable.", "plainleather: flammable, its armour under steel's");
        var graniteF = GraniteF();
        C(Has(graniteF, "sturdy") && Has(graniteF, "fireproof") && Has(graniteF, "heavy") && Has(graniteF, "slow-work") && Why(graniteF, "slow-work") == "building with it takes x6 the work (steel x1)",
            "granite blocks: sturdy and fireproof, heavy, six times the work to build");
        var chunkF = ChunkF();
        C(Has(chunkF, "heavy") && !Has(chunkF, "trade-good") && P(chunkF) == "Granite chunk: made into granite blocks at a stonecutter's table, but heavy.", "a chunk: heavy, made into blocks: " + P(chunkF));
        var compF = ComponentF();
        C(Has(compF, "trade-good") && !Has(compF, "repairs") && P(compF) == "Component: a material.", "a component without a colony: no repairs to speak of");
        var chemF = ChemfuelF();
        C(Has(chemF, "explosive") && Has(chemF, "fuel") && !Has(chemF, "heavy") && P(chemF) == "Chemfuel: a fuel, but explosive in a fire.", "chemfuel: explodes in a fire, a fuel");
        C(ItemCardMath.Tags(chemF)[0].Id == "explosive", "a risk comes first");
        var human = PlainleatherF();
        human.Label = "human leather";
        human.Material.OfPerson = true;
        C(ItemCardMath.Tags(human)[0].Id == "of-person" && P(human).Contains("but of a person"), "human leather says so first");

        // ---------- The materials at home ----------
        var steelH = Home(SteelF(), 1250, TradeGood.Material, 6, 120, 8, 20, 30);
        steelH.Material.BuildWants = 300;
        C(Has(steelH, "building-material") && Has(steelH, "spare") && P(steelH) == "Steel: a metal for building, armour and weapons; 1,250 in store, more than the colony keeps.", "steel at home: " + P(steelH));
        var woodH = Home(WoodF(), 800, TradeGood.Material, 6, 90, 0, 6, 12);
        woodH.Material.Burners = 5;
        woodH.Material.BurnersWhileUsed = 1;
        woodH.Material.BurnADay = 14f;
        woodH.Material.BurnerWords = "3 campfires and 2 torch lamps";
        C(Has(woodH, "fuel") && Why(woodH, "fuel") == "the colony's 3 campfires and 2 torch lamps burn it"
          && P(woodH) == "Wood: timber for building and weapons, burned as fuel, but flammable and soft; 800 in store, lasts about 57 days.", "wood at home, burned as fuel: " + P(woodH));
        var plasteelH = Home(PlasteelF(), 340, TradeGood.Precious, 6, 60, 4, 10, 8);
        plasteelH.Material.BuildWants = 400;
        C(Has(plasteelH, "needed") && ItemCardMath.Tags(plasteelH)[0].Id == "needed"
          && P(plasteelH) == "Plasteel: the sturdiest metal there is, light and fireproof, but slow to work; 340 in store, the blueprints need 60 more.", "plasteel the blueprints wait on: " + P(plasteelH));
        var goldH = Home(GoldF(), 120, TradeGood.Precious, 6, 30, 0, 0, 5);
        C(Has(goldH, "spare") && P(goldH) == "Gold: a precious metal, but soft; 120 in store, more than the colony keeps.", "gold beyond what the colony keeps: " + P(goldH));
        var silverH = Home(SilverF(), 2300, TradeGood.Other, 6, 30);
        C(P(silverH) == "Silver: the money traders pay and ask in; 2,300 in store." && !Has(silverH, "spare"), "silver at home: no reserve to be beyond");
        var jadeH = Home(JadeF(), 40, TradeGood.Precious, 6, 20, 0, 0, 4);
        C(P(jadeH) == "Jade: a precious stone, fireproof, but soft and slow to work; 40 in store, enough.", "jade at home: " + P(jadeH));
        var clothH = Home(ClothF(), 400, TradeGood.Textile, 6, 3, 25, 0, 2);
        C(Has(clothH, "for-clothes") && !Has(clothH, "building-material") && P(clothH) == "Cloth: a fabric for clothes, but flammable; 400 in store, more than the colony keeps.", "cloth at home: " + P(clothH));
        var devH = Home(DevilstrandF(), 90, TradeGood.Textile, 6, 3, 25, 0, 2);
        C(!Has(devH, "needed") && P(devH) == "Devilstrand: a fabric for clothes, beautiful and warm; 90 in store.", "devilstrand under the textile floor, but never kept: no alarm");
        var hyperH = Home(HyperweaveF(), 30, TradeGood.Textile, 6, 3, 25, 0, 2);
        C(P(hyperH) == "Hyperweave: a fabric for clothes, beautiful and warm; 30 in store.", "hyperweave at home: " + P(hyperH));
        var leatherH = Home(PlainleatherF(), 200, TradeGood.Textile, 6, 3, 25, 0, 2);
        C(P(leatherH) == "Plainleather: a leather for clothes, but flammable; 200 in store, enough.", "plainleather at home: " + P(leatherH));
        var graniteH = Home(GraniteF(), 600, TradeGood.Material, 6, 80, 0, 0, 10);
        C(P(graniteH) == "Granite blocks: a stone for building, fireproof and sturdy, but heavy and slow to work; 600 in store, enough.", "granite blocks at home: " + P(graniteH));
        var chunkH = Home(ChunkF(), 40, TradeGood.Other, 6);
        C(Has(chunkH, "raw-material") && !Has(chunkH, "bench") && P(chunkH) == "Granite chunk: made into granite blocks at a stonecutter's table, but heavy; 40 in store.", "chunks at home: " + P(chunkH));
        chunkH.Material.RawBenchBuilt = false;
        C(Has(chunkH, "bench") && Why(chunkH, "bench") == "made into granite blocks at a stonecutter's table, and none is built", "no stonecutter's table: a bench is needed");
        var compH = Home(ComponentF(), 12, TradeGood.Component, 6, 30, 2, 6, 40);
        compH.Material.Machines = 14;
        C(Has(compH, "repairs") && Has(compH, "needed") && Why(compH, "repairs") == "the colony's 14 machines that break down take one each to mend: about 0.9 a season"
          && P(compH) == "Component: mends the colony's 14 machines when they break down; 12 in store, under the 18 the colony keeps.", "components for repairs: " + P(compH));
        var advH = Home(AdvancedComponentF(), 3, TradeGood.Component, 6, 5, 2, 3, 9);
        C(Has(advH, "bench") && !Has(advH, "needed") && P(advH) == "Advanced component: an ingredient of 19 things the colony can make; 3 in store.", "advanced components: an ingredient, the fabrication bench unbuilt: " + P(advH));
        var chemH = Home(ChemfuelF(), 300, TradeGood.Other, 6);
        chemH.Material.Burners = 2;
        chemH.Material.BurnADay = 9f;
        chemH.Material.BurnerWords = "2 chemfuel generators";
        C(P(chemH) == "Chemfuel: fuel for the colony's 2 chemfuel generators, but explosive in a fire; 300 in store, lasts about 33 days.", "chemfuel at home: " + P(chemH));
        var research = Home(Material("thrumbofur", Plainleather(), "leather", Cloth(), Devilstrand()), 0, TradeGood.Textile, 6);
        research.Material.Later = 4;
        research.Material.LaterResearch = "Complex clothing";
        C(Has(research, "research") && Why(research, "research").StartsWith("nothing made of it is open yet; Complex clothing opens some"), "nothing open yet: the research that opens most");
        var starved = Home(SteelF(), 5, TradeGood.Material, 6, 120);
        starved.Material.BillsStarved = 2;
        C(Has(starved, "needed") && P(starved).EndsWith("5 in store, 2 bills wait on it."), "bills that wait on it: " + P(starved));

        // ---------- Rooms: the game's sums ----------
        C(Near(ItemCardMath.Impressiveness(1500f, 3f, 125f, 0f), 100f), "a room whose every part is at the game's yard is 100 impressive");
        C(Near(ItemCardMath.ImpressFactor(0.5f), 0.5f) && Near(ItemCardMath.ImpressFactor(2f), 1f + (float)Math.Log(2)) && Near(ItemCardMath.ImpressFactor(-2f), -1f - (float)Math.Log(2)),
            "the game's factor: itself under one, one and its logarithm beyond");
        C(ItemCardMath.Impressiveness(1500f, 6f, 125f, 0f) > 100f && ItemCardMath.Impressiveness(3000f, 3f, 125f, 0f) > 100f && ItemCardMath.Impressiveness(1500f, 3f, 125f, -2f) < 100f,
            "beauty and wealth lift a room, dirt lowers it");
        C(ItemCardMath.Impressiveness(1e6f, 50f, 12.5f, 0f) < ItemCardMath.Impressiveness(1e6f, 50f, 125f, 0f) / 2f, "a cramped room is held far down however rich and beautiful (the space's cap)");
        C(ItemCardMath.BeautyDivisor(3) == 20f && ItemCardMath.BeautyDivisor(40) == 40f && ItemCardMath.BeautyDivisor(100) == 100f && ItemCardMath.BeautyDivisor(22) > 20f && ItemCardMath.BeautyDivisor(22) < 40f,
            "the game's room size divisor for beauty");
        var dining = Room(2500f, 0.5f, 80f, 60);
        var withPiece = ItemCardMath.With(dining, 300f, 1400f, ItemCardMath.BeautyDivisor(60));
        var back = ItemCardMath.Without(withPiece, 300f, 1400f, ItemCardMath.BeautyDivisor(60));
        C(Near(withPiece.Beauty, 5.5f) && Near(withPiece.Wealth, 3900f) && withPiece.Space == 80f && withPiece.Impressiveness > dining.Impressiveness, "a piece of beauty 300 adds 5 to a 60-cell room's beauty and its worth to its wealth");
        C(Near(back.Beauty, dining.Beauty) && Near(back.Wealth, dining.Wealth) && Near(back.Impressiveness, dining.Impressiveness, 0.05f), "with it and without it are the same sums both ways");
        var gameSays = withPiece;
        gameSays.Impressiveness += 4f;
        C(Near(ItemCardMath.Without(gameSays, 300f, 1400f, 60f).Impressiveness, dining.Impressiveness + 4f, 0.05f), "the room's own number is the game's; only the difference is Rimfolk's sum");
        C(ItemCardMath.StageIndex(45f, ItemCardMath.ImpressStages) == 3 && ItemCardMath.StageIndex(-50f, ItemCardMath.ImpressStages) == 0 && ItemCardMath.StageIndex(240f, ItemCardMath.ImpressStages) == 9
          && ItemCardMath.ImpressWords[3] == "decent", "the game's impressiveness stages");
        C(ItemCardMath.RoleMatters("DiningRoom") && ItemCardMath.RoleMatters("Bedroom") && ItemCardMath.RoleMatters("ThroneRoom") && !ItemCardMath.RoleMatters("Workshop") && !ItemCardMath.RoleMatters(null),
            "the rooms whose impressiveness gives a mood");
        C(ItemCardMath.ArtRelated("AM_Art") && ItemCardMath.ArtRelated("AM_ArtQuality") && ItemCardMath.ArtRelated("AM_Artist") && ItemCardMath.ArtRelated("AM_ArtProductionSpeed")
          && ItemCardMath.ArtRelated("Artistic") && ItemCardMath.ArtRelated("BeautyLover") && !ItemCardMath.ArtRelated("Heartbreak") && !ItemCardMath.ArtRelated("Party")
          && !ItemCardMath.ArtRelated("Biosculpting") && !ItemCardMath.ArtRelated("ArtilleryStrike") && !ItemCardMath.ArtRelated(null), "precepts and memes about art by their names' words (Alpha Memes' artist), never a heart or a party");
        C(ItemCardMath.Words("AM_ArtQuality").SequenceEqual(new[] { "AM", "Art", "Quality" }), "a def name's words");

        // ---------- A grand sculpture at a few qualities, placed in the dining room ----------
        var room = Room(2500f, 0.5f, 80f, 60);
        var excellent = Sculpture("excellent", 4, 300f, 1400f, room);
        var a = excellent.ArtPiece;
        C(a.StageWith == "very impressive" && a.StageWithout == "slightly impressive" && Near(a.ImpressWithout, room.Impressiveness, 0.05f), "excellent: the dining room very impressive with it, slightly impressive without");
        C(Has(excellent, "lifts-room") && Why(excellent, "lifts-room") == "the dining room is very impressive with it (107), slightly impressive without (60)", "it lifts the room: " + Why(excellent, "lifts-room"));
        C(P(excellent) == "Grand sculpture, excellent: beauty 300 in the dining room, worth $1,400; selling it would make the room merely slightly impressive.", "an excellent grand sculpture in plain words: " + P(excellent));
        var normal = Sculpture("normal", 2, 100f, 900f, room);
        C(Has(normal, "lifts-room") && !Has(normal, "masterwork") && P(normal).StartsWith("Grand sculpture, normal: beauty 100 in the dining room, worth $900; selling it"), "a normal one lifts it a stage too");
        var legendary = Sculpture("legendary", 6, 800f, 3500f, room);
        C(Has(legendary, "masterwork") && Why(legendary, "masterwork") == "Legendary work: the best an artist makes" && legendary.ArtPiece.StageWith == "extremely impressive", "a legendary one: a masterwork, the room extremely impressive");
        var awful = Sculpture("awful", 0, -40f, 400f, room);
        C(Has(awful, "ugly") && !Has(awful, "lifts-room") && !Has(awful, "beautiful") && ItemCardMath.BetterWithout(awful.ArtPiece), "an awful one: ugly, its room better without it");
        C(P(awful) == "Grand sculpture, awful: beauty -40 in the dining room, worth $400; the room would be better without it.", "an awful grand sculpture in plain words: " + P(awful));
        var small = Sculpture("good", 3, 30f, 300f, Room(40000f, 6f, 300f, 200));
        C(!Has(small, "lifts-room") && P(small).EndsWith("; the room stays " + small.ArtPiece.StageWith + " without it."), "a small piece in a grand room: the room stays as it is: " + P(small));

        // Held, outdoors, nowhere to go.
        var held = Sculpture("excellent", 4, 300f, 1400f, room, false);
        C(Has(held, "lifts-room") && Has(held, "a-room") && !Has(held, "spare") && P(held) == "Grand sculpture, excellent: beauty 300, worth $1,400; placed in the dining room it would make it very impressive.",
            "in storage: where it would do most: " + P(held));
        var outdoors = Sculpture("excellent", 4, 300f, 1400f, null, true);
        C(P(outdoors) == "Grand sculpture, excellent: beauty 300 outdoors, worth $1,400; it lifts no room where it stands." && Has(outdoors, "beautiful"), "outdoors it lifts no room");
        var nowhere = Sculpture("excellent", 4, 300f, 1400f, null, false);
        C(Has(nowhere, "spare") && Why(nowhere, "spare").EndsWith("worth $1,400 to a trader") && P(nowhere).EndsWith("no room of the colony's would rise a stage with it."), "no room it would lift: spare to sell");
        var away = Sculpture("excellent", 4, 300f, 1400f, null, false);
        away.HasColony = false;
        C(P(away) == "Grand sculpture, excellent: beauty 300, worth $1,400." && !Has(away, "spare") && !Has(away, "a-room"), "away from home: its beauty and worth alone");

        // The rules that care, a story, a gift.
        var noble = Sculpture("excellent", 4, 300f, 1400f, room);
        noble.ArtPiece.Noble = "Bo";
        noble.ArtPiece.NobleRoom = "throne room";
        noble.ArtPiece.NobleNeeds = 100;
        C(Has(noble, "noble-room") && Why(noble, "noble-room") == "Bo's throne room needs impressiveness 100: 107 with it, 60 without", "a noble's throne room with it and without it");
        var told = Sculpture("masterwork", 5, 500f, 2400f, room);
        told.ArtPiece.Story = true;
        told.ArtPiece.Title = "The Long Winter";
        told.ArtPiece.Artist = "Cy";
        told.ArtPiece.Tale = "a raid beaten off";
        told.ArtPiece.SmallArt = true;
        told.ArtPiece.Faiths = "faiths at home with precepts on art: Way of the Hearth";
        told.ArtPiece.MeditationFocus = true;
        told.ArtPiece.Meditators = 2;
        C(Has(told, "masterwork") && Why(told, "tells-story") == "\"The Long Winter\" shows a raid beaten off, by Cy" && Has(told, "gift") && Has(told, "faith-art") && Has(told, "meditation"),
            "a masterwork that tells a story, small art for a gift, a faith that cares, a meditation focus");
        C(ItemCardMath.Tags(told).Count <= ItemCardMath.MaxTags && ItemCardMath.Tags(told).Any(t => t.Category == TagCategory.Strength), "eight at most, the first strength kept");
        var wooden = Sculpture("good", 3, 60f, 300f, room);
        wooden.ArtPiece.Flammability = 1f;
        C(Has(wooden, "burns"), "a wooden piece burns");

        // ---------- The vocabulary ----------
        var ids = ItemCardMath.Vocabulary.Select(t => t.Id).ToList();
        C(ids.Distinct().Count() == ids.Count, "the one vocabulary: step 1's and step 2's tags each their own");
        foreach (var id in new[] { "strong-armour", "sturdy", "fireproof", "beautiful", "warm", "keeps-cool", "light", "sharp-blades", "hard-blows", "lifts-room" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Strength, $"{id} is a strength");
        foreach (var id in new[] { "soft", "burns", "heavy", "slow-work", "ugly" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Weakness, $"{id} is a weakness");
        foreach (var id in new[] { "explosive", "of-person" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Risk, $"{id} is a risk");
        foreach (var id in new[] { "precious", "masterwork", "tells-story", "gift", "faith-art", "meditation" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Special, $"{id} is special");
        foreach (var id in new[] { "building-material", "for-clothes", "fuel", "repairs", "money", "raw-material", "noble-room", "trade-good", "needed", "spare" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Role, $"{id} is a role");
        foreach (var id in new[] { "research", "bench", "a-room" }) C(ItemCardMath.Def(id)?.Category == TagCategory.Needs, $"{id} is a need");
        var all = new List<ItemFacts>
        {
            SteelF(), WoodF(), PlasteelF(), UraniumF(), GoldF(), SilverF(), JadeF(), ClothF(), DevilstrandF(), HyperweaveF(), PlainleatherF(), GraniteF(), ChunkF(), ComponentF(), AdvancedComponentF(), ChemfuelF(),
            steelH, woodH, plasteelH, goldH, silverH, jadeH, clothH, devH, hyperH, leatherH, graniteH, chunkH, compH, advH, chemH, research, starved, human,
            excellent, normal, legendary, awful, small, held, outdoors, nowhere, away, noble, told, wooden
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
        string math = Read("Source/ColonyCouncil/Items/ItemCardMaterialMath.cs");
        string live = Read("Source/ColonyCouncil/Items/ItemCardLive.cs");
        string mats = Read("Source/ColonyCouncil/Items/ItemCardMaterials.cs");
        string art = Read("Source/ColonyCouncil/Items/ItemCardArt.cs");
        string window = Read("Source/ColonyCouncil/Items/ThingCardWindow.cs");
        string first = Read("Source/ColonyCouncil/Items/ItemCardFirst.cs");
        string hook = Read("Source/ColonyCouncil/Weapons/WeaponCardFirst.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_ItemCard.xml");
        C(!math.Contains("using Verse") && !math.Contains("using RimWorld") && !math.Contains("using UnityEngine") && Read("Tools/Tests/Tests.csproj").Contains("Items/ItemCardMaterialMath.cs"),
            "the step 2 rules are pure and tested here");
        C(live.Contains("return ItemCardMath.KindOf(SortOf(d, t));") && live.Contains("d.IsWeapon || d.IsApparel") && live.Contains("private static bool ToUse(ThingDef d)"),
            "the kind from the def: never a weapon or apparel; things to use left to step 3");
        C(live.Contains("IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)") && live.Contains("IsWithinCategory(ThingCategoryDefOf.Manufactured)") && live.Contains("IsWithinCategory(ThingCategoryDefOf.Chunks)")
          && live.Contains("GetCompProperties<CompProperties_Art>()"), "materials by the game's filing and stuff, art by the game's art comp: the player's mods come in on their own");
        C(live.Contains("thing?.GetInnerIfMinified()") && first.Contains("!ItemCardLive.Covers(thing)") && window.Contains("!ItemCardLive.Covers(t)"), "a minified piece is read as the piece inside, wherever its card opens");
        C(hook.Contains("if (ItemCardFirst.Swapped(card)) return false;") && hook.Contains("return !WeaponCardFirst.Swapped(card) && !ApparelCardFirst.Swapped(card);"), "the one hook on the window stack, unchanged");
        C(live.Contains("if (p.kind == ItemKind.Material) ReadMaterial(p);") && Regex.Matches(live, @"ReadMaterial\(p\);").Count == 1 && Regex.Matches(mats, @"MadeOf\(d, mp\);").Count == 1
          && Regex.Matches(mats, @"Yields\(d, mp\);").Count == 1, "every building and recipe is looked through once a kind, when its numbers are first read (PartOf keeps them)");
        C(!Regex.IsMatch(mats + art + live, @"\.AllRecipeUsers\b"), "recipe users come from the remembered list, never the game's walk over every def");
        C(live.Contains("if (m.kind == ItemKind.Material) ColonyMaterial(m, p);") && live.Contains("if (colony)"), "the colony's part (stock, demand, deposits, rooms) only on the ten seconds' refresh");
        int bodyAt = window.IndexOf("protected override float Body", StringComparison.Ordinal);
        string body = bodyAt >= 0 ? window.Substring(bodyAt) : "";
        C(bodyAt > 0 && body.Contains("MaterialParts(w, y, m)") && body.Contains("ArtParts(w, y, m)") && !body.Contains("ItemCardLive.") && !body.Contains("GetRoom") && !body.Contains("listerBuildings"),
            "the card draws materials and art from the model alone");
        C(art.Contains("AccessTools.Field(typeof(RoomStatWorker_Beauty), \"CellCountCurve\")") && art.Contains("ItemCardMath.Without(with, a.Beauty, a.Worth, BeautyDivisor(with.Cells))")
          && art.Contains("stat.GetScoreStageIndex(with.Impressiveness)"), "a placed piece's room: the game's own numbers and stages, the game's divisor, Rimfolk's sums only for without");
        foreach (var id in ItemCardMath.StuffRowIds) C(keys.Contains("<CC_ItemCard_Stuff_" + id + ">"), $"the comparison row {id} has its words");
        foreach (var role in ItemCardMath.RolesThatMatter.Concat(new[] { "Other" })) C(keys.Contains("<CC_ItemCard_RoomDoes_" + role + ">"), $"what a {role} does has its words");
        C(keys.Contains("<CC_SetNote_ItemCardFirst>Food, drink, drugs, medicine, materials and art open as Rimfolk's item card") && keys.Contains("a material (metal, wood, stone blocks"),
            "the setting names the new kinds");
        C(!keys.Contains("—") && !Regex.IsMatch(keys, @"<CC_[A-Za-z_]+>[^<]*!"), "no em dashes or exclamation marks in the keys");
        return n;
    }
}
