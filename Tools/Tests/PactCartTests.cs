using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The pact's cart and the schemes' thefts (the player, 17 November 2026: "it just shouldn't auto take valuable materials";
/// "they shouldn't steal components again and other valuable things"; Things/GoodsMath.cs, Diplomacy/PactMath.cs,
/// SchemeMath.cs): valuable goods and plain goods on clear cases and on the player's own catalogue (docs/player-mods, the
/// active rows), the cart's order by the partner's age, the colony's reserve and the count, the player's own colony (their
/// save of 17 November 2026: what replaces the 8 components), the thief's goods, "8 components", and the seams.
/// </summary>
public static class PactCartTests
{
    private static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    private static string[] Split(string s) => (s ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the pact's cart: " + what);
        }

        // ================= Valuable and plain, on clear cases =================

        GoodsFacts G(float value, Action<GoodsFacts> set = null)
        {
            var f = new GoodsFacts { value = value, tech = 3 };
            set?.Invoke(f);
            return f;
        }
        var wood = G(1.2f, f => { f.woody = true; f.raw = true; });
        var steel = G(1.9f, f => { f.metallic = true; f.raw = true; });
        var plasteel = G(9f, f => { f.metallic = true; f.raw = true; });
        var gold = G(10f, f => { f.metallic = true; f.raw = true; });
        var component = G(32f);
        var chemfuel = G(2.3f);
        var cloth = G(1.5f, f => { f.fabric = true; f.textile = true; });
        var devilstrand = G(5.5f, f => { f.fabric = true; f.textile = true; });
        var leather = G(2.1f, f => { f.leathery = true; f.textile = true; });
        var thrumboFur = G(14f, f => { f.leathery = true; f.textile = true; });
        var blocks = G(0.9f, f => { f.stony = true; f.blocks = true; });
        var jade = G(5f, f => { f.stony = true; f.raw = true; });
        var rice = G(1.1f, f => { f.nutrition = 0.05f; f.preferability = ThingRolesMath.RawBad; });
        var meal = G(15f, f => { f.nutrition = 0.9f; f.preferability = ThingRolesMath.MealSimple; });
        var jelly = G(8f, f => { f.nutrition = 0.05f; f.preferability = ThingRolesMath.MealFine; });
        var butter = G(10f, f => { f.nutrition = 0.01f; f.preferability = GoodsMath.NeverForNutrition; });
        var hops = G(1.3f, f => { f.nutrition = 0.05f; f.preferability = 2; f.plantMatter = true; });
        var medicine = G(10f, f => f.medicine = true);
        var beer = G(12f, f => { f.drug = true; f.nutrition = 0.08f; });
        var silver = G(1f, f => { f.money = true; f.metallic = true; f.raw = true; });
        var hyperweave = G(3.9f, f => { f.fabric = true; f.textile = true; f.tech = 5; });
        var egg = G(2f, f => { f.nutrition = 0.25f; f.preferability = ThingRolesMath.RawTasty; f.special = true; });
        var humanLeather = G(4.2f, f => { f.leathery = true; f.humanlike = true; });
        C(GoodsMath.Kind(wood) == GoodsKind.Wood && GoodsMath.Kind(steel) == GoodsKind.Metal && GoodsMath.Kind(cloth) == GoodsKind.Cloth
          && GoodsMath.Kind(leather) == GoodsKind.Leather && GoodsMath.Kind(blocks) == GoodsKind.Blocks && GoodsMath.Kind(rice) == GoodsKind.Food
          && GoodsMath.Kind(meal) == GoodsKind.Food, "wood, steel, cloth, leather, stone blocks, rice and a meal are plain goods");
        C(GoodsMath.Kind(jelly) == GoodsKind.Food, "food is food whatever it is worth (insect jelly)");
        foreach (var (what, f) in new[] { ("plasteel", plasteel), ("gold", gold), ("a component", component), ("chemfuel", chemfuel), ("devilstrand", devilstrand),
                     ("a thrumbo's fur", thrumboFur), ("jade", jade), ("medicine", medicine), ("beer", beer), ("silver", silver), ("hyperweave", hyperweave),
                     ("butter, an ingredient", butter), ("hops, grown for brewing", hops), ("a fertilized egg", egg) })
            C(GoodsMath.Kind(f) == GoodsKind.Valuable, $"{what} is valuable");
        C(GoodsMath.Kind(humanLeather) == GoodsKind.Unfit && GoodsMath.Kind(null) == GoodsKind.Unfit, "a person's leather never goes, and nothing unknown");
        C(GoodsMath.Plain(GoodsKind.Food) && !GoodsMath.Plain(GoodsKind.Valuable) && !GoodsMath.Plain(GoodsKind.Unfit), "plain: every kind but valuable and unfit");
        C(Math.Abs(GoodsMath.PreciousValue - 4f) < 0.001f && GoodsMath.Kind(G(3.9f, f => { f.metallic = true; f.raw = true; })) == GoodsKind.Metal
          && GoodsMath.Kind(G(4f, f => { f.metallic = true; f.raw = true; })) == GoodsKind.Valuable, "a material worth 4 a unit or more is precious");

        // ================= The player's own catalogue =================

        string cat = Path.Combine(root, "docs", "player-mods");
        var lines = File.ReadAllLines(Path.Combine(cat, "items.tsv"));
        var head = lines[0].Split('\t');
        var all = lines.Skip(1).Where(l => l.Length > 0).Select(l =>
        {
            var c = l.Split('\t');
            var row = new Dictionary<string, string>();
            for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
            return row;
        }).ToList();
        var rows = all.Where(r => r["active"] == "yes" && r["category"] == "Item").ToList();
        var races = File.ReadAllLines(Path.Combine(cat, "races.tsv"));
        var rhead = races[0].Split('\t');
        int kindAt = Array.IndexOf(rhead, "kind");
        var humanRaces = races.Skip(1).Select(l => l.Split('\t')).Where(c => kindAt >= 0 && c.Length > kindAt && c[kindAt] == "humanlike").Select(c => c[0]).ToHashSet();
        // The trade tags the game itself uses (ThingRoles: a tag only a mod uses marks its good as special).
        var weaponLines = File.ReadAllLines(Path.Combine(cat, "weapons.tsv"));
        int wTags = Array.IndexOf(weaponLines[0].Split('\t'), "tradeTags"), wMod = Array.IndexOf(weaponLines[0].Split('\t'), "mod");
        var gameTags = all.Where(r => r["mod"].StartsWith("ludeon.")).SelectMany(r => Split(r["tradeTags"]))
            .Concat(weaponLines.Skip(1).Select(l => l.Split('\t')).Where(c => wMod >= 0 && c.Length > Math.Max(wTags, wMod) && c[wMod].StartsWith("ludeon.")).SelectMany(c => Split(c[wTags])))
            .ToHashSet();
        GoodsFacts Facts(Dictionary<string, string> r)
        {
            var cats = Split(r["thingCategories"]);
            var stuff = Split(r["stuffProps"]);
            var food = Split(r["foodType"]);
            string drug = r["drugCategory"];
            bool special = Split(r["comps"]).Any(ThingRolesMath.SpecialComp) || Split(r["tradeTags"]).Any(t => ThingRolesMath.SpecialTag(t, gameTags));
            return new GoodsFacts
            {
                defName = r["defName"],
                value = F(r["marketValue"]),
                tech = ThingRolesMath.IndexOf(ThingRolesMath.Techs, r["techLevel"]),
                money = r["defName"] == "Silver",
                medicine = F(r["potency"]) > 0f || cats.Contains("Medicine"),
                drug = drug != "" && drug != "None",
                special = special,
                // A race's leather is linked by the race in the game (race.leatherDef, Things/ThingRoles.cs), which the table doesn't carry.
                humanlike = humanRaces.Contains(r["sourceDef"]) || ThingRolesMath.PersonName(r["defName"], food.Length > 0 || drug != "") || r["defName"] == "Leather_Human",
                nutrition = F(r["nutrition"]),
                preferability = ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, r["preferability"]),
                plantMatter = cats.Contains("PlantMatter"),
                blocks = cats.Contains("StoneBlocks"),
                raw = cats.Contains("ResourcesRaw"),
                // The table names a def's own categories: leathers and wools are the game's textiles too.
                textile = cats.Contains("Textiles") || cats.Contains("Leathers") || cats.Contains("Wools"),
                woody = stuff.Contains("Woody"),
                stony = stuff.Contains("Stony"),
                metallic = stuff.Contains("Metallic"),
                fabric = stuff.Contains("Fabric"),
                leathery = stuff.Contains("Leathery")
            };
        }
        var kinds = rows.GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => GoodsMath.Kind(Facts(g.Last())));
        GoodsKind K(string def) => kinds.TryGetValue(def, out var k) ? k : GoodsKind.Unfit;
        C(kinds.Count > 1500, $"the active items are read ({kinds.Count})");

        // The game's own.
        foreach (var d in new[] { "ComponentIndustrial", "ComponentSpacer", "Plasteel", "Gold", "Jade", "Uranium", "Silver", "Chemfuel", "Neutroamine", "MedicineHerbal",
                     "MedicineIndustrial", "MedicineUltratech", "Beer", "Penoxycyline", "Yayo", "DevilstrandCloth", "Hyperweave", "Synthread", "Leather_Thrumbo", "Shell_HighExplosive",
                     "Wort", "PsychoidLeaves", "Dye" })
            C(K(d) == GoodsKind.Valuable, $"{d} is valuable");
        foreach (var (d, k) in new[] { ("WoodLog", GoodsKind.Wood), ("Steel", GoodsKind.Metal), ("Cloth", GoodsKind.Cloth), ("WoolSheep", GoodsKind.Cloth),
                     ("Leather_Plain", GoodsKind.Leather), ("Leather_Bluefur", GoodsKind.Leather), ("BlocksGranite", GoodsKind.Blocks), ("BlocksSlate", GoodsKind.Blocks),
                     ("RawRice", GoodsKind.Food), ("MealSimple", GoodsKind.Food), ("MealFine", GoodsKind.Food), ("Pemmican", GoodsKind.Food), ("Kibble", GoodsKind.Food),
                     ("Hay", GoodsKind.Food), ("EggChickenUnfertilized", GoodsKind.Food), ("Milk", GoodsKind.Food) })
            C(K(d) == k, $"{d} is {k}");
        // (Meat is made by the game from each race at load, so it is not in the table: a person's meat is unfit by its source in the game.)
        C(K("Leather_Human") == GoodsKind.Unfit, "a person's leather never goes");

        // The player's mods: their valuables caught by what they are, never by name.
        foreach (var d in new[] { "VGE_Astrofuel", "AB_Propane", "VCHE_Deepchem", "CrucibleCore", "RB_ComponentBioInterface", "GR_BoosterImmuno", "PW_Potion", "RawMagicyte",
                     "TM_Arcalleum", "Foerum", "AA_SkySteel", "AB_CordaxResin", "Obsidian", "VFEC_Tyrian", "AA_MothSilk", "Synthamide", "VFEP_Rum", "VFEM2_Mead", "VFEM2_Wine",
                     "FiberComposite", "AA_CactipineQuill", "TargetingChip", "RB_Beeswax", "AC_Butter", "VCE_Flour", "VFEM2_Must" })
            C(!kinds.ContainsKey(d) || K(d) == GoodsKind.Valuable, $"{d} (a mod's) is valuable");
        foreach (var (d, k) in new[] { ("VFEM2_HardLeather", GoodsKind.Leather), ("VFEC_Bronze", GoodsKind.Metal), ("VFEM2_Hardweave", GoodsKind.Cloth),
                     ("VFEM2_Hayweave", GoodsKind.Cloth), ("VDE_Gaubric", GoodsKind.Cloth), ("AB_CrystalWood", GoodsKind.Wood), ("GU_RedWood", GoodsKind.Wood),
                     ("GR_DarkYoungWoodLog", GoodsKind.Wood), ("AB_BlocksCragstone", GoodsKind.Blocks), ("RG_DinosaursHideMedium", GoodsKind.Leather),
                     ("DevDesigner_AncientDeerWool", GoodsKind.Cloth), ("AA_ChameleonYakWoolWinter", GoodsKind.Cloth), ("WoolCloth", GoodsKind.Cloth) })
            C(!kinds.ContainsKey(d) || K(d) == k, $"{d} (a mod's) is {k}");
        // Every active def in a category that is all valuable is valuable; every stone block, plain.
        var allDear = new[] { "Medicine", "Drugs", "MortarShells", "Manufactured", "RimatomicsProduct", "GR_Boosters", "PW_HealingItem", "SabotRounds", "TM_Magicyte" };
        var wrongDear = rows.Where(r => Split(r["thingCategories"]).Any(allDear.Contains) && !Split(r["thingCategories"]).Contains("Textiles") && K(r["defName"]) != GoodsKind.Valuable
                                        && K(r["defName"]) != GoodsKind.Unfit).Select(r => r["defName"]).Distinct().ToList();
        C(wrongDear.Count == 0, $"medicine, drugs, shells, crafting parts and a mod's parts and potions are all valuable ({string.Join(", ", wrongDear.Take(8))})");
        var wrongBlocks = rows.Where(r => Split(r["thingCategories"]).Contains("StoneBlocks") && K(r["defName"]) != GoodsKind.Blocks).Select(r => r["defName"]).ToList();
        C(wrongBlocks.Count == 0, $"every stone block of the player's mods is plain ({string.Join(", ", wrongBlocks.Take(8))})");
        var cheapHides = rows.Where(r => Split(r["thingCategories"]).Contains("Leathers") && F(r["marketValue"]) < GoodsMath.PreciousValue && Split(r["tradeTags"]).Length == 0
                                         && K(r["defName"]) != GoodsKind.Unfit).ToList();
        C(cheapHides.Count >= 40 && cheapHides.All(r => K(r["defName"]) == GoodsKind.Leather), $"every ordinary leather of the player's mods is plain ({cheapHides.Count})");
        var counts = kinds.Values.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        C(counts.GetValueOrDefault(GoodsKind.Food) > 100 && counts.GetValueOrDefault(GoodsKind.Leather) > 40 && counts.GetValueOrDefault(GoodsKind.Cloth) > 15
          && counts.GetValueOrDefault(GoodsKind.Valuable) > 300,
            "plenty of each: " + string.Join(", ", counts.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value)));

        // ================= The cart: the order by age, the reserve, the count =================

        var plainKinds = new[] { GoodsKind.Wood, GoodsKind.Cloth, GoodsKind.Leather, GoodsKind.Food, GoodsKind.Blocks, GoodsKind.Metal };
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
            C(GoodsMath.CartOrder(age).OrderBy(k => k).SequenceEqual(plainKinds.OrderBy(k => k)), $"{age}: every plain kind once, nothing valuable");
        C(GoodsMath.CartOrder(WorldAge.Medieval).Take(3).SequenceEqual(new[] { GoodsKind.Wood, GoodsKind.Cloth, GoodsKind.Leather })
          && GoodsMath.CartOrder(WorldAge.Tribal)[0] == GoodsKind.Wood && GoodsMath.CartOrder(WorldAge.Industrial)[0] == GoodsKind.Metal
          && GoodsMath.CartOrder(WorldAge.Spacer)[0] == GoodsKind.Metal, "what the partner's age uses first: wood and cloth for the medieval and tribal, steel later");
        C(GoodsMath.Rank(GoodsKind.Valuable, WorldAge.Medieval) > GoodsMath.Rank(GoodsKind.Metal, WorldAge.Medieval), "a valuable good after every plain one");
        C(GoodsMath.Reserve(GoodsKind.Food, 10, 0.05f) == 1600 && GoodsMath.Reserve(GoodsKind.Food, 10, 0.9f) == 89 && GoodsMath.Reserve(GoodsKind.Wood, 10) == 350
          && GoodsMath.Reserve(GoodsKind.Leather, 10) == 200 && GoodsMath.Reserve(GoodsKind.Blocks, 10) == 400 && GoodsMath.Reserve(GoodsKind.Metal, 10) == 500
          && GoodsMath.Reserve(GoodsKind.Valuable, 10) == 0, "the colony keeps five days of each food, and wood, cloth, leather, blocks and metal by its size");
        C(PactMath.CartCount(2.1f, 404, 200) == 102 && PactMath.CartCount(2.1f, 190, 200) == 0 && PactMath.CartCount(1f, 3000, 350) == 250
          && PactMath.CartCount(32f, 68) == 8, "half of what is above the reserve at most; a valuable good counted as before (8 of 68 components)");
        C(PactMath.CartWorth(2.1f, 102) == 215 && PactMath.CartPay(2.1f, 102) == 171 && PactMath.CartWorth(1.2f, 12) == 14, "its worth in words to the nearest 5, its pay exact");
        C(!PactMath.CartWorthGoing(1.9f, 30) && PactMath.CartWorthGoing(1.9f, 32) && !PactMath.CartWorthGoing(5f, 0), "a cart worth less than 60 silver isn't offered");

        // The player's colony (their save of 17 November 2026, day 124: ten colonists, the stores as they stood) with the
        // Stormriders, a medieval people: the 8 components a season give way to plainleather.
        PactMath.CartStock S(string d, GoodsKind k, float v, int have, float nut = 0f) => new PactMath.CartStock { def = d, kind = k, value = v, have = have, nutrition = nut };
        var stores = new[]
        {
            S("ComponentIndustrial", GoodsKind.Valuable, 32f, 68), S("Steel", GoodsKind.Metal, 1.9f, 781), S("Leather_Plain", GoodsKind.Leather, 2.1f, 404),
            S("Leather_Bluefur", GoodsKind.Leather, 2.3f, 334), S("Leather_Light", GoodsKind.Leather, 1.9f, 231), S("Meat_Yak", GoodsKind.Food, 2f, 242, 0.05f),
            S("BlocksSlate", GoodsKind.Blocks, 0.9f, 240), S("Pemmican", GoodsKind.Food, 1.4f, 138, 0.05f), S("RawMagicyte", GoodsKind.Valuable, 5f, 178),
            S("Jade", GoodsKind.Valuable, 5f, 51), S("WoodLog", GoodsKind.Wood, 1.2f, 17), S("Cloth", GoodsKind.Cloth, 1.5f, 17), S("Uranium", GoodsKind.Valuable, 6f, 38),
            S("Leather_Human", GoodsKind.Unfit, 4.2f, 40)
        };
        var picks = PactMath.CartPicks(stores, WorldAge.Medieval, 10);
        C(picks.Count > 0 && picks[0].def == "Leather_Plain" && picks[0].count == 102 && !picks[0].valuable,
            $"the player's pact gets 102 plainleather a season in place of the 8 components ({string.Join(", ", picks.Select(p => p.count + " " + p.def + (p.valuable ? " (valuable)" : "")))})");
        int firstDear = picks.FindIndex(p => p.valuable);
        C(firstDear > 0 && picks.Skip(firstDear).All(p => p.valuable) && picks.Any(p => p.def == "ComponentIndustrial" && p.count == 8 && p.valuable),
            "the valuable carts after every plain one, marked, the components among them to pick on purpose");
        C(picks.Any(p => p.def == "Steel") && picks.FindIndex(p => p.def == "Steel") > picks.FindIndex(p => p.def == "Leather_Bluefur"),
            "steel offered after the leathers a medieval people uses");
        C(!picks.Any(p => p.def == "Leather_Human" || p.def == "WoodLog" || p.def == "Cloth" || p.def == "Meat_Yak" || p.def == "BlocksSlate"),
            "nothing of a person, and nothing the colony can't spare above its reserve (17 wood, 17 cloth, its meat and blocks)");
        var industrial = PactMath.CartPicks(stores, WorldAge.Industrial, 10);
        C(industrial.Count > 0 && industrial[0].def == "Steel", "an industrial partner gets the steel first");
        C(PactMath.CartPicks(new[] { S("ComponentIndustrial", GoodsKind.Valuable, 32f, 68) }, WorldAge.Medieval, 10).All(p => p.valuable),
            "with nothing plain to spare, only valuable carts, none of them taken without a choice");
        C(PactMath.CartPicks(null, WorldAge.Medieval, 10).Count == 0 && PactMath.CartPicks(stores, WorldAge.Medieval, 10, 2, 1).Count == 3, "nothing in stores, nothing; the lists kept short");

        // ================= The thief =================

        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
        {
            var order = SchemeMath.TheftOrder(age);
            C(order.Length > 0 && order.All(GoodsMath.Plain) && !order.Contains(GoodsKind.Wood) && !order.Contains(GoodsKind.Blocks),
                $"{age}: a thief takes plain goods only, never a valuable good, nor logs or blocks");
        }
        C(SchemeMath.ActOf(SchemeMath.Stores) == SchemeAct.Theft && SchemeMath.ActOf(SchemeMath.Tool) == SchemeAct.Theft && SchemeMath.ActOf(SchemeMath.Purse) == SchemeAct.Theft,
            "a theft of goods, and the old forms in a save, are thefts");
        C(SchemeMath.LootCount(1.9f, 0.5f, 500, 1f) == 70 && SchemeMath.LootCount(1.5f, 0.026f, 500, 0f) == 40 && SchemeMath.LootCount(2f, 0f, 10, 1f) == 10,
            "about 60 to 180 silver's worth, what one pack holds at most");

        // ================= "8 components" =================

        C(CourierMath.Counted(8, "component", false) == "components" && CourierMath.Counted(102, "plainleather", true) == "plainleather"
          && CourierMath.Counted(8, "component", false, (s, k) => s) == "component", "8 components by Rimfolk's own rule; a stuff keeps its form");
        string couriers = Code("Couriers/Couriers.cs");
        C(couriers.Contains("English ? null : (Func<string, int, string>)Find.ActiveLanguageWorker.Pluralize") && couriers.Contains("LanguageDatabase.defaultLanguage"),
            "in English the game's language worker is not asked (it gave \"component\" back in the player's game)");

        // ================= The seams =================

        string pacts = Code("Diplomacy/Pacts.cs");
        C(!Regex.IsMatch(Code("Things/GoodsMath.cs"), @"using (Verse|RimWorld|UnityEngine)") && !Regex.IsMatch(Code("Diplomacy/PactMath.cs"), @"using (Verse|RimWorld|UnityEngine)"),
            "GoodsMath.cs and PactMath.cs are pure");
        C(Code("Things/Goods.cs").Contains("GoodsMath.Kind(Facts(d))") && !Regex.IsMatch(Code("Things/Goods.cs"), "\"(VFE|AA_|RB_|TM_|AB_|GR_)"), "the live game's goods read by what they are, no mod named");
        C(pacts.Contains("PactMath.CartPicks(stores, age, colonists)") && pacts.Contains("Goods.KindOf(d)") && !pacts.Contains("OrderByDescending(kv => kv.Value * (kv.Key?.BaseMarketValue"),
            "the cart's goods by the rule, not the stores' dearest first");
        C(Regex.IsMatch(pacts, @"CartGoods\(home, f\)\s*;\s*\n\s*if \(goods\.Count > 0\) \(cart, count\) = goods\[0\]") && Code("Colonies/HandOvers.cs").Contains("Pacts.CartGoods(bound, people)"),
            "a pact nobody chooses a cart for (accepted from them, a hand-over) takes the first plain one");
        C(Code("Diplomacy/DiplomacyLetters.cs").Contains("asks.FirstOrDefault(o => o.why == null && !o.onPurpose)") && Code("Diplomacy/DiplomacyLetters.cs").Contains("onPurpose = valuable"),
            "the letter's list never starts on a valuable good");
        C(Code("Dealings/DealingLetters.cs").Contains("Pacts.CartChoices(home, f)") && Code("Dealings/DealingLetters.cs").Contains("onPurpose = valuable"),
            "a supply contract's load the same way");
        C(Code("Diplomacy/DiplomacyActs.cs").Contains("Pacts.CartChoices(home, f)") && Code("Diplomacy/DiplomacyActs.cs").Contains("PactWords.CartLine(def, count)"),
            "face to face, each cart with its worth and their pay");
        C(Regex.IsMatch(pacts, @"private static void TradeHour[\s\S]*?AskCart\(t, f, home, def, count, season\)") && !Regex.IsMatch(pacts, @"private static void TradeHour[\s\S]*?DilemmaEffect_CarryOut[\s\S]*?private static bool AskCart"),
            "the season's cart waits for the colony's answer; nothing is carried out before it");
        C(pacts.Contains("class ChoiceLetter_PactCart : RimfolkLetter") && pacts.Contains("\"CC_Pact_Opt_SendCart\"") && pacts.Contains("\"CC_Pact_Opt_RefuseCart\"")
          && pacts.Contains("Pacts.SendCart(tt, mm, Def, count)") && pacts.Contains("Pacts.RefuseCart(tt)") && pacts.Contains("l.StartTimeout("), "send it, or refuse and end the pact; unanswered, it goes");
        C(Regex.IsMatch(pacts, @"public static void RefuseCart[\s\S]*?End\(t, Tie\.State\.Ended, null\)[\s\S]*?CC_Pact_Ans_Ended[\s\S]*?TheirWords\(head, DiplomacyLetters\.PactEndTag"),
            "refused, the pact ends without blame and their head's word comes back as for an end by letter");
        C(Regex.IsMatch(pacts, @"public static void Hourly[\s\S]*?if \(cartsLooked != Current\.Game\) PlainCarts\(\);") && Regex.IsMatch(pacts, @"public static void Daily[\s\S]*?PlainCarts\(\);")
          && Regex.IsMatch(pacts, @"public static void PlainCarts[\s\S]*?ChosenOnPurpose\(t\)[\s\S]*?CC_Pact_Msg_CartPlain"),
            "a valuable cart nobody chose is made plain once loaded, with a message; one picked on purpose stays");
        C(Code("Diplomacy/DiplomacyBook.cs").Contains("Scribe_Collections.Look(ref chosenCarts, \"chosenCarts\", LookMode.Value)") && Code("Diplomacy/DiplomacyBook.cs").Contains("chosenCarts ??= new List<int>()"),
            "the carts picked on purpose are saved, and an old save loads with none");
        string panels = Code("Diplomacy/PactsPanels.cs");
        C(panels.Contains("\"pact-cart\"") && panels.Contains("DiplomacyLetters.PactCartPrefix + t.id") && panels.Contains("ChoiceLetter_PactCart k && k.Waiting"),
            "\"Change the cart\" on the pact's card, a letter to their head; the season's question in the inbox");
        string letters = Code("Diplomacy/DiplomacyLetters.cs");
        C(Regex.IsMatch(letters, @"PactCartPrefix, StringComparison\.Ordinal\)\)\s*\{[\s\S]*?Pacts\.SetCart\(t, def, c\)") && letters.Contains("tags.Add(IsMoot(l) ? PactCartMootTag : PactCartTag)"),
            "their answer changes the cart, in their own words for it");
        string schemes = Code("Diplomacy/Schemes.cs");
        C(!schemes.Contains("ThingDefOf.ComponentIndustrial") && !schemes.Contains("ThingDefOf.Gold") && !schemes.Contains("ThingDefOf.Jade") && schemes.Contains("Goods.Plain(t.def)"),
            "a thief's goods by the same rule: no components, gold, jade or silver");
        C(Code("Dilemmas/AnswerMath.cs").Contains("(\"pactCart\", ChronicleKind.World, \"ChoiceLetter_PactCart\")") && Code("UI/EventCards/EventCardMath.cs").Contains("[\"Rimfolk_PactCart\"] = \"Peoples\""),
            "the season's question in the answers' log and on its card");
        var letterDefs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Pacts.xml")).Root!.Elements("LetterDef").ToList();
        C(letterDefs.Any(d => d.Element("defName")?.Value == "Rimfolk_PactCart" && d.Element("letterClass")?.Value == "ColonyCouncil.ChoiceLetter_PactCart"), "its letter def");

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var mine = new[] { "CC_Pact_CartLine", "CC_Pact_CartLineOnce", "CC_Pact_CartValuable", "CC_Pact_CartValuable_What", "CC_Pact_CartAsk", "CC_Pact_Opt_SendCart", "CC_Pact_Opt_RefuseCart",
            "CC_Pact_CartRefused_Label", "CC_Pact_CartRefused", "CC_Pact_CartRefused_Words", "CC_Pact_Msg_CartPlain", "CC_Pact_Ans_Cart", "CC_Pact_TopicCart", "CC_Pact_Act_Cart",
            "CC_Pact_Act_Cart_Tip", "CC_Pact_Why_NoOtherCart", "CC_Pact_Week_PactCart", "CC_Dip_Dlg_PickOne", "CC_Scheme_What_Stores", "CC_Scheme_Short_Stores" };
        foreach (var k in mine)
        {
            C(keyed.ContainsKey(k), $"{k} has its words");
            if (!keyed.TryGetValue(k, out var v)) continue;
            C(!v.Contains("—") && !v.Contains("!") && !Regex.IsMatch(v, @"\b(needeth|meaneth|doth|hath)\b"), $"{k}: plain, no em dash or exclamation mark");
        }
        C(keyed.TryGetValue("CC_Pact_CartLine", out var line) && line.Contains("{GOODS}") && line.Contains("{WORTH}") && line.Contains("{SILVER}") && line.Contains("a season"),
            "each cart says what it is, what it is worth and what they pay");
        C(keyed.TryGetValue("CC_Pact_CartAsk", out var ask) && !Regex.IsMatch(ask, @"\b(days?|hours?)\b"), "the season's question has no countdown");
        var ends = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PactEnds.xml")).Descendants("li").Select(x => x.Value).ToList();
        C(ends.Any(l => l.StartsWith("[pactcart,cordial")) && ends.Any(l => l.StartsWith("[pactcart,cool")) && !ends.Any(l => l.StartsWith("[pactcart,refusal"))
          && ends.Any(l => l.StartsWith("[pactcartmoot")), "a changed cart's own words, cordial or cool, and when overtaken");
        C(ends.Count(l => Regex.IsMatch(l, @"^\[pactend,(cool|curt)(,(theirLowtech|theirHightech))?\]")) >= 4, "an end's words fit for a word sent back with the porters (no letter or call in them)");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
