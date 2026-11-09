using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Step 2 of armour and apparel (docs/design/apparel.md, point 5): the game's reading of a piece (Apparel/ApparelReadMath.cs,
/// the same file the review runs on), a dozen of the base game's pieces built as the live reader builds them from their
/// defs rating exactly as docs/apparel/apparel.json has them; the piece in hand (quality, wear and the moods it gives, a
/// dead wearer, biocoding); the rating for a colonist and why it differs (how they fight, their body, their weather, the
/// moods it adds); protection by region; and the seams (the settings, the keys, the hooks, the card's frame, the Almanac).
/// </summary>
public static class ApparelLiveTests
{
    // ---------------------------------------------------------------- the base game's defs, as the game reads them

    static readonly Dictionary<string, ApparelStuffFacts> Stuffs = new()
    {
        ["DevilstrandCloth"] = Stuff("DevilstrandCloth", "devilstrand", 0.36f, 3.0f, 1.4f, 20f, 24f, 0.4f),
        ["Cloth"] = Stuff("Cloth", "cloth", 0f, 0.18f, 0.36f, 18f, 18f, 1.2f),
        ["Synthread"] = Stuff("Synthread", "synthread", 0.26f, 0.9f, 0.94f, 22f, 22f, 0.7f),
        ["Hyperweave"] = Stuff("Hyperweave", "hyperweave", 0.54f, 2.88f, 2.0f, 26f, 26f, 0.4f),
        ["Leather_Plain"] = Stuff("Leather_Plain", "plainleather", 0.24f, 1.5f, 0.81f, 16f, 16f, 1f),
        ["Leather_Bear"] = Stuff("Leather_Bear", "bearskin", 0.24f, 1.5f, 1.12f, 20f, 20f, 1f),
        ["Leather_Thrumbo"] = Stuff("Leather_Thrumbo", "thrumbofur", 0.36f, 1.5f, 2.08f, 34f, 22f, 1f),
    };

    static ApparelStuffFacts Stuff(string def, string label, float blunt, float heat, float sharp, float cold, float hot, float flam) => new()
    {
        Def = def, Label = label, Flammability = flam,
        Powers = new Dictionary<string, float>
        {
            ["StuffPower_Armor_Blunt"] = blunt, ["StuffPower_Armor_Heat"] = heat, ["StuffPower_Armor_Sharp"] = sharp,
            ["StuffPower_Insulation_Cold"] = cold, ["StuffPower_Insulation_Heat"] = hot,
        }
    };

    static ApparelStuffFacts StuffOf(string n) => n != null && Stuffs.TryGetValue(n, out var s) ? s : null;

    /// <summary>A comp as the live reader reads it: its class as defs write it, and its public fields.</summary>
    static ApparelPartFacts Comp(string cls, string compClass = "", params (string k, string v)[] fields)
    {
        var p = new ApparelPartFacts { Class = cls, CompClass = compClass };
        foreach (var (k, v) in fields)
        {
            p.Fields[k] = v;
            p.Present.Add(k);
        }
        return p;
    }

    /// <summary>The comps every base game piece carries, as the live reader reads them (a plain CompProperties named by its comp class).</summary>
    static List<ApparelPartFacts> Common() => new()
    {
        Comp("CompProperties_Forbiddable", "CompForbiddable"), Comp("CompColorable", "CompColorable"), Comp("CompQuality", "CompQuality"),
        Comp("CompProperties_Styleable", "CompStyleable"),
    };

    static ApparelDefFacts Piece(string def, string label, string mod, string tech, string[] layers, string[] groups, string[] tags, string tcat, string research,
        string researchTech, params (string stat, float v)[] statBases)
    {
        var d = new ApparelDefFacts
        {
            DefName = def, Label = label, Mod = mod, ModName = mod == "ludeon.rimworld" ? "Core" : "Royalty", TechLevel = tech, ThingClass = "Apparel",
            Layers = layers.ToList(), Groups = groups.ToList(), Tags = tags.ToList(), ThingCategories = new List<string> { tcat },
            Research = research == null ? new List<string>() : new List<string> { research }, ResearchTech = research == null ? "" : researchTech,
            Comps = Common(), StuffOf = StuffOf, HediffOf = n => null,
        };
        foreach (var (s, v) in statBases) d.StatBases[s] = v;
        return d;
    }

    /// <summary>A piece made of stuff, in its usual stuff (devilstrand for fabric), rated in its kinds' common stuffs as the live reader lists them.</summary>
    static ApparelDefFacts Stuffed(ApparelDefFacts d, params string[] cats)
    {
        d.StuffCategories = cats.ToList();
        d.Stuff = ApparelRead.UsualStuff(d.StuffCategories, s => Stuffs.ContainsKey(s));
        d.OtherStuffs = ApparelRead.OtherStuffsFor(d.StuffCategories, d.Stuff, s => Stuffs.ContainsKey(s));
        return d;
    }

    static ApparelDefFacts Utility(ApparelDefFacts d)
    {
        d.CareIfDamaged = false;
        d.CareIfWornByCorpse = false;
        d.CountsAsClothes = false;
        d.WearPerDay = 0f;
        return d;
    }

    public static List<ApparelDefFacts> Vanilla()
    {
        const string core = "ludeon.rimworld";
        var flak = Piece("Apparel_FlakVest", "flak vest", core, "Industrial", new[] { "Middle" }, new[] { "Torso", "Neck" }, new[] { "IndustrialMilitaryBasic" }, "ApparelArmor",
            "FlakArmor", "Industrial", ("ArmorRating_Blunt", 0.36f), ("ArmorRating_Heat", 0.27f), ("ArmorRating_Sharp", 1.0f), ("Beauty", -3f), ("DeteriorationRate", 2f),
            ("EquipDelay", 5f), ("Flammability", 0.6f), ("Insulation_Cold", 1f), ("Mass", 4f), ("MaxHitPoints", 200f), ("WorkToMake", 9000f));
        flak.EquippedOffsets["MoveSpeed"] = -0.12f;

        var belt = Utility(Piece("Apparel_ShieldBelt", "shield belt", core, "Spacer", new[] { "Belt" }, new[] { "Waist" }, new[] { "BeltDefense" }, "ApparelUtility", "ShieldBelt", "Spacer",
            ("Beauty", -3f), ("DeteriorationRate", 2f), ("EnergyShieldEnergyMax", 1.1f), ("EnergyShieldRechargeRate", 0.13f), ("EquipDelay", 2f), ("Flammability", 0.4f),
            ("Mass", 3f), ("MaxHitPoints", 100f), ("WorkToMake", 14000f)));
        belt.Ages = "Child, Adult";
        belt.Comps.Add(Comp("CompProperties_Shield", "CompShield", ("startingTicksToReset", "3200"), ("minDrawSize", "1.2"), ("maxDrawSize", "1.55"),
            ("energyLossPerDamage", "0.033"), ("energyOnReset", "0.2"), ("blocksRangedWeapons", "true")));

        var jump = Utility(Piece("Apparel_PackJump", "jump pack", "ludeon.rimworld.royalty", "Industrial", new[] { "Belt" }, new[] { "Waist" }, new[] { "PackJump" }, "ApparelUtility",
            "JumpPack", "Industrial", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 2f), ("Flammability", 0.4f), ("JumpRange", 23.9f), ("Mass", 3f),
            ("MaxHitPoints", 100f), ("WorkToMake", 14000f)));
        jump.WornByKinds = false;
        jump.Comps.Add(Comp("CompProperties_ApparelReloadable", "CompApparelReloadable", ("ammoDef", "Chemfuel"), ("ammoCountToRefill", "0"), ("ammoCountPerCharge", "20"),
            ("baseReloadTicks", "60"), ("replenishAfterCooldown", "false"), ("soundReload", "Standard_Reload"), ("cooldownGerund", "on cooldown"), ("maxCharges", "5"),
            ("destroyOnEmpty", "false"), ("chargeNoun", "jump"), ("hotKey", "Misc4"), ("displayGizmoWhileUndrafted", "false"), ("displayGizmoWhileDrafted", "true")));
        jump.Verbs.Add(new ApparelVerbFacts { VerbClass = "Verb_Jump", Label = "jump", Range = 1.42f, Warmup = 0.5f });

        var smoke = Utility(Piece("Apparel_SmokepopBelt", "smokepop pack", core, "Industrial", new[] { "Belt" }, new[] { "Waist" }, new[] { "BeltDefensePop" }, "ApparelUtility",
            "SmokepopBelt", "Industrial", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 2f), ("Flammability", 0.4f), ("Mass", 3f), ("MaxHitPoints", 100f),
            ("PackRadius", 4.9f), ("WorkToMake", 3600f)));
        smoke.ThingClass = "SmokepopBelt";
        smoke.Comps.Add(Comp("CompProperties_ApparelReloadable", "CompApparelReloadable", ("ammoDef", "Chemfuel"), ("ammoCountToRefill", "10"), ("maxCharges", "3"),
            ("destroyOnEmpty", "false"), ("chargeNoun", "smokepop")));
        smoke.Verbs.Add(new ApparelVerbFacts { VerbClass = "Verb_Smokepop", Label = "pop smoke", Range = 1.42f });

        var parka = Stuffed(Piece("Apparel_Parka", "parka", core, "Neolithic", new[] { "Shell" }, new[] { "Torso", "Neck", "Shoulders", "Arms" }, new[] { "IndustrialBasic", "Neolithic" },
            "ApparelMisc", null, "", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 3f), ("Flammability", 1f), ("Mass", 2f), ("MaxHitPoints", 180f),
            ("StuffEffectMultiplierArmor", 0.2f), ("StuffEffectMultiplierInsulation_Cold", 2f), ("StuffEffectMultiplierInsulation_Heat", 0f), ("WorkToMake", 8000f)), "Fabric", "Leathery");

        var duster = Stuffed(Piece("Apparel_Duster", "duster", core, "Medieval", new[] { "Shell" }, new[] { "Torso", "Neck", "Shoulders", "Arms", "Legs" }, new[] { "IndustrialAdvanced" },
            "ApparelMisc", "ComplexClothing", "Medieval", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 3f), ("Flammability", 1f), ("Mass", 2f), ("MaxHitPoints", 200f),
            ("StuffEffectMultiplierArmor", 0.3f), ("StuffEffectMultiplierInsulation_Cold", 0.6f), ("StuffEffectMultiplierInsulation_Heat", 0.85f), ("WorkToMake", 10000f)), "Fabric", "Leathery");
        duster.EquippedOffsets["SlaveSuppressionOffset"] = -0.05f;

        var tuque = Stuffed(Piece("Apparel_Tuque", "tuque", core, "Medieval", new[] { "Overhead" }, new[] { "UpperHead" }, new[] { "IndustrialBasic" }, "Headgear", "ComplexClothing",
            "Medieval", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 0.8f), ("Flammability", 1f), ("Mass", 0.07f), ("MaxHitPoints", 80f),
            ("StuffEffectMultiplierArmor", 0.2f), ("StuffEffectMultiplierInsulation_Cold", 0.5f), ("StuffEffectMultiplierInsulation_Heat", 0f), ("WorkToMake", 1200f)), "Fabric");
        tuque.CountsAsClothes = false;
        tuque.Ages = "Child, Adult";

        var cowboy = Stuffed(Piece("Apparel_CowboyHat", "cowboy hat", core, "Medieval", new[] { "Overhead" }, new[] { "UpperHead" }, new[] { "IndustrialBasic", "Western" }, "Headgear",
            "ComplexClothing", "Medieval", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 0.8f), ("Flammability", 1f), ("Mass", 0.14f), ("MaxHitPoints", 80f),
            ("StuffEffectMultiplierArmor", 0.2f), ("StuffEffectMultiplierInsulation_Cold", 0.1f), ("StuffEffectMultiplierInsulation_Heat", 0.5f), ("WorkToMake", 1800f)), "Fabric", "Leathery");
        cowboy.CountsAsClothes = false;
        cowboy.Ages = "Child, Adult";
        cowboy.EquippedOffsets["SlaveSuppressionOffset"] = -0.05f;
        cowboy.EquippedOffsets["SocialImpact"] = 0.1f;
        foreach (var role in new[] { "leader", "second triumvir", "third triumvir" }) cowboy.Rules.Add(new ApparelRuleFacts { Source = "PreceptDef", Label = role, Required = true });

        var foil = Piece("Apparel_PsychicFoilHelmet", "psychic foil helmet", core, "Medieval", new[] { "Overhead" }, new[] { "UpperHead" },
            new[] { "IndustrialMilitaryBasic", "IndustrialMilitaryAdvanced", "SpacerMilitary" }, "Headgear", null, "", ("ArmorRating_Blunt", 0.09f), ("ArmorRating_Heat", 0.27f),
            ("ArmorRating_Sharp", 0.09f), ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 1.5f), ("Flammability", 0.4f), ("Insulation_Cold", 2f), ("Insulation_Heat", 1f),
            ("MarketValue", 250f), ("Mass", 1f), ("MaxHitPoints", 80f));
        foil.EquippedOffsets["PsychicSensitivity"] = -0.9f;
        foil.Made = false;

        var marine = Piece("Apparel_PowerArmor", "marine armor", core, "Spacer", new[] { "Middle", "Shell" }, new[] { "Torso", "Neck", "Shoulders", "Arms", "Legs" },
            new[] { "SpacerMilitary" }, "ApparelArmor", "PoweredArmor", "Spacer", ("ArmorRating_Blunt", 0.45f), ("ArmorRating_Heat", 0.54f), ("ArmorRating_Sharp", 1.06f), ("Beauty", -3f),
            ("DeteriorationRate", 2f), ("EquipDelay", 14f), ("Flammability", 0.4f), ("Insulation_Cold", 68f), ("Insulation_Heat", 10f), ("Mass", 12f), ("MaxHitPoints", 340f),
            ("WorkToMake", 60000f));
        marine.EquippedOffsets["MoveSpeed"] = -0.25f;
        marine.EquippedOffsets["SlaveSuppressionOffset"] = -0.3f;
        marine.EquippedOffsets["VacuumResistance"] = 0.3f;
        marine.Comps.Add(Comp("CompProperties_Biocodable", "CompBiocodable", ("biocodeOnEquip", "false")));

        var recon = Piece("Apparel_ArmorHelmetRecon", "recon helmet", core, "Spacer", new[] { "Overhead" }, new[] { "FullHead" }, new[] { "SpacerMilitary" }, "ArmorHeadgear",
            "ReconArmor", "Spacer", ("ArmorRating_Blunt", 0.4f), ("ArmorRating_Heat", 0.46f), ("ArmorRating_Sharp", 0.92f), ("Beauty", -3f), ("DeteriorationRate", 2f),
            ("EquipDelay", 2f), ("Flammability", 0.4f), ("Insulation_Cold", 4f), ("Insulation_Heat", 2f), ("Mass", 1f), ("MaxHitPoints", 120f), ("WorkToMake", 15750f));
        recon.EquippedOffsets["SlaveSuppressionOffset"] = -0.1f;
        recon.EquippedOffsets["VacuumResistance"] = 0.65f;
        recon.Comps.Add(Comp("CompProperties_Biocodable", "CompBiocodable"));

        var tribal = Stuffed(Piece("Apparel_TribalA", "tribalwear", core, "Neolithic", new[] { "OnSkin" }, new[] { "Torso", "Legs" }, new[] { "Neolithic" }, "ApparelMisc", null, "",
            ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 1.5f), ("Flammability", 1f), ("Mass", 0.5f), ("MaxHitPoints", 100f), ("StuffEffectMultiplierArmor", 0.2f),
            ("StuffEffectMultiplierInsulation_Cold", 0.55f), ("StuffEffectMultiplierInsulation_Heat", 0.55f), ("WorkToMake", 1800f)), "Fabric", "Leathery");

        var shirt = Stuffed(Piece("Apparel_BasicShirt", "T-shirt", core, "Medieval", new[] { "OnSkin" }, new[] { "Torso", "Shoulders" }, new[] { "IndustrialBasic" }, "ApparelMisc",
            "ComplexClothing", "Medieval", ("Beauty", -3f), ("DeteriorationRate", 2f), ("EquipDelay", 1.5f), ("Flammability", 1f), ("Mass", 0.25f), ("MaxHitPoints", 100f),
            ("StuffEffectMultiplierArmor", 0.2f), ("StuffEffectMultiplierInsulation_Cold", 0.22f), ("StuffEffectMultiplierInsulation_Heat", 0.1f), ("WorkToMake", 1600f)), "Fabric", "Leathery");

        return new List<ApparelDefFacts> { flak, belt, jump, smoke, parka, duster, tuque, cowboy, foil, marine, recon, tribal, shirt };
    }

    static ApparelDefFacts Named(string defName) => Vanilla().First(d => d.DefName == defName);

    /// <summary>The moods of the game's apparel thoughts, as the live reader passes them (their numbers from ThoughtDefOf).</summary>
    static void Moods(ApparelDefFacts d)
    {
        d.Frayed = ("tattered apparel", -3f);
        d.Tattered = ("shredded apparel", -5f);
        d.DeadMans = ("taken from the dead", -5f);
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "apparel card: " + what);
        }
        bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";
        float Num(JsonNode j) => (float)j;

        // ---------- The game's reading gives the review's ratings ----------
        var json = JsonNode.Parse(Read("docs/apparel/apparel.json"));
        var review = ((JsonArray)json["apparel"]).ToDictionary(a => a["defName"].ToString(), a => a);
        var reports = new Dictionary<string, ApparelReport>();
        foreach (var d in Vanilla())
        {
            var r = ApparelRead.Full(d);
            reports[d.DefName] = r;
            C(review.TryGetValue(d.DefName, out var j), $"{d.Label} is in apparel.json");
            if (j == null) continue;
            var g = r.Rating;
            C(Near(g.Overall, Num(j["rating"])), $"{d.Label}: the game's reading rates it {g.Overall}, as the review ({j["rating"]})");
            C(Near(g.OverallShooter, Num(j["ratingShooter"])) && Near(g.OverallMelee, Num(j["ratingMelee"])), $"{d.Label}: a shooter's {g.OverallShooter} and a melee fighter's {g.OverallMelee}, as the review");
            C(g.Role == j["role"].ToString() && g.For == j["ratedFor"].ToString() && g.Slot == j["slot"].ToString(), $"{d.Label}: {g.Role}, {g.For}, as the review");
            C(r.Table == j["table"].ToString(), $"{d.Label}: the review's table");
            foreach (var kv in (JsonObject)j["sub"])
                C(g.Sub.TryGetValue(kv.Key, out var v) && Near(v, Num(kv.Value), 0.01f), $"{d.Label}: sub-rating {kv.Key} as the review ({kv.Value})");
            C(g.Sub.Count == ((JsonObject)j["sub"]).Count, $"{d.Label}: the review's sub-ratings, no more");
            foreach (var kv in (JsonObject)j["situations"])
                C(g.Situations.TryGetValue(kv.Key, out var v) && Near(v, Num(kv.Value), 0.01f), $"{d.Label}: situation {kv.Key} as the review");
            var tags = ((JsonArray)j["tags"]).Select(t => t["id"].ToString()).ToList();
            C(r.Tags.Select(t => t.Id).SequenceEqual(tags), $"{d.Label}: tags {string.Join(",", r.Tags.Select(t => t.Id))} as the review ({string.Join(",", tags)})");
            C(r.Tags.Select(t => t.Why).SequenceEqual(((JsonArray)j["tags"]).Select(t => t["why"].ToString())), $"{d.Label}: each tag's why as the review");
            C(r.Plain == j["plain"].ToString(), $"{d.Label}: the plain line as the review ({r.Plain})");
            var specials = ((JsonArray)j["specials"]).Select(s => s["key"] + "|" + s["label"]).ToList();
            C(g.Specials.Select(s => s.Key + "|" + s.Label).SequenceEqual(specials), $"{d.Label}: specials as the review");
            var byStuff = (JsonObject)j["ratingByStuff"];
            C(r.ByStuff.Count == byStuff.Count && byStuff.All(kv => r.ByStuff.TryGetValue(kv.Key, out var v) && Near(v, Num(kv.Value), 0.05f)),
                $"{d.Label}: its rating in other stuffs as the review ({string.Join(", ", r.ByStuff.Select(kv => kv.Key + " " + kv.Value))})");
            C(Near(g.Sharp, Num(j["armour"]["sharp"]), 0.001f) && Near(g.Blunt, Num(j["armour"]["blunt"]), 0.001f) && Near(g.Heat, Num(j["armour"]["heat"]), 0.001f),
                $"{d.Label}: its armour as the review");
            C(Near(g.Cold, Num(j["insulation"]["cold"]), 0.01f) && Near(g.HeatInsulation, Num(j["insulation"]["heat"]), 0.01f), $"{d.Label}: its insulation as the review");
            C(Near(g.Coverage, Num(j["coverage"]), 0.0001f), $"{d.Label}: its coverage as the review");
            C(r.Facts.Unsure.Count == ((JsonArray)j["unsure"]).Count, $"{d.Label}: rough or not as the review");
            C((r.StuffLabel == "" ? null : r.StuffLabel) == j["stuffLabel"]?.ToString(), $"{d.Label}: its usual stuff as the review");
        }
        C(reports.Count == 13, "thirteen base game pieces read: armour, helmets, belts, packs, weather gear, hats, clothes");

        // ---------- The extractor's own lists, ported ----------
        C(ApparelRead.UsualStuff(new[] { "Fabric", "Leathery" }, s => true) == "DevilstrandCloth" && ApparelRead.UsualStuff(new[] { "Metallic" }, s => true) == "Steel"
          && ApparelRead.UsualStuff(new[] { "Leathery" }, s => true) == "Leather_Plain" && ApparelRead.UsualStuff(new[] { "Fabric" }, s => s != "DevilstrandCloth") == null,
            "a kind's usual stuff: devilstrand for fabric, steel, plain leather (Tools/apparel_extract.py's)");
        C(ApparelRead.OtherStuffsFor(new[] { "Fabric", "Leathery" }, "DevilstrandCloth", s => true)
              .SequenceEqual(new[] { "Cloth", "Synthread", "DevilstrandCloth", "Hyperweave", "Leather_Plain", "Leather_Bear", "Leather_Thrumbo" }),
            "its other stuffs: each kind's common ones, as the extractor lists them");
        C(ApparelRead.Words("VGE_OxygenCanister") == "oxygen canister" && ApparelRead.Words("Chemfuel") == "chemfuel", "a def's name in words");

        // ---------- The piece in hand ----------
        var plainParka = reports["Apparel_Parka"];
        var legendary = Named("Apparel_Parka");
        legendary.ArmourQuality = 1.8f;
        legendary.InsulationQuality = 1.2f;
        var legR = ApparelRead.Full(legendary);
        C(legR.Rating.Overall > plainParka.Rating.Overall && Near(legR.Rating.Cold, plainParka.Rating.Cold * 1.2f, 0.01f), $"a legendary parka keeps warmer and rates higher ({legR.Rating.Overall})");
        var awful = Named("Apparel_Parka");
        awful.ArmourQuality = 0.6f;
        awful.InsulationQuality = 0.8f;
        C(ApparelRead.Full(awful).Rating.Overall < plainParka.Rating.Overall, "an awful parka (the player's \"Bluefur parka (awful)\") rates lower");
        var capped = Named("Apparel_PowerArmor");
        capped.ArmourQuality = 2.5f;
        C(Near(ApparelRateMath.ArmourRating(ApparelRead.Full(capped).Facts, "Sharp"), 2f), "armour held at the game's 200% whatever its quality");

        var worn = Named("Apparel_FlakVest");
        Moods(worn);
        worn.MaxHitPoints = 200f;
        worn.HitPoints = 180f;
        var whole = ApparelRead.Full(worn);
        C(ApparelRead.DamageStage(worn) == -1 && whole.Facts.Moods.Count == 0 && Near(whole.Rating.Overall, reports["Apparel_FlakVest"].Rating.Overall),
            "a piece at 90% wears no mood and rates as new (wear doesn't weaken armour)");
        worn.HitPoints = 80f;
        var frayed = ApparelRead.Full(worn);
        C(ApparelRead.DamageStage(worn) == 0 && frayed.Facts.Moods.Count == 1 && frayed.Facts.Moods[0].mood == -3f, "below half its hit points: the game's first mood");
        C(frayed.Rating.Areas["mind"] < whole.Rating.Areas["mind"] - 0.04f && frayed.Rating.Overall <= whole.Rating.Overall, $"the mood counts in the mind ({frayed.Rating.Overall})");
        C(frayed.Rating.Specials.Any(s => s.Key == "mood" && s.Label.StartsWith("tattered apparel: mood -3") && s.Sign < 0), "and is named among its effects");
        worn.HitPoints = 30f;
        C(ApparelRead.DamageStage(worn) == 1 && ApparelRead.Full(worn).Facts.Moods[0].mood == -5f, "below a fifth: the second");
        worn.HitPoints = 30f;
        worn.CareIfDamaged = false;
        C(ApparelRead.DamageStage(worn) == -1, "a piece its wearer doesn't mind the wear of (a belt) gives no mood");
        C(Near(ApparelRead.DaysOfWear(180f, 200f, 0.4f, 0.5f) ?? 0f, 200f) && ApparelRead.DaysOfWear(90f, 200f, 0.4f, 0.5f) == null && ApparelRead.DaysOfWear(180f, 200f, 0f, 0.5f) == null,
            "days of wear to half: 80 hit points at 0.4 a day is 200; none below it or for what doesn't wear");
        var dead = Named("Apparel_BasicShirt");
        Moods(dead);
        dead.WornByCorpse = true;
        var deadR = ApparelRead.Full(dead);
        C(deadR.Facts.Moods.Any(m => m.label == "taken from the dead") && deadR.Rating.Overall < reports["Apparel_BasicShirt"].Rating.Overall, "taken from the dead: its mood counted");
        var coded = Named("Apparel_PowerArmor");
        coded.BiocodedTo = "Ana";
        var codedR = ApparelRead.Full(coded);
        C(codedR.Rating.Specials.Any(s => s.Key == "biocodable" && s.Label == "biocoded to Ana") && Near(codedR.Rating.Overall, reports["Apparel_PowerArmor"].Rating.Overall),
            "biocoding names whom it is coded to and doesn't change the rating");

        // ---------- For a colonist ----------
        var beltR = reports["Apparel_ShieldBelt"];
        var w = new ApparelWearer { Kind = "melee" };
        var melee = ApparelForMath.For(beltR, w);
        C(Near(melee.Rating, beltR.Rating.OverallMelee) && ApparelForMath.Telling(melee).Count == 0, $"a melee fighter rates a shield belt as the review's melee fighter ({melee.Rating})");
        var shooter = ApparelForMath.For(beltR, new ApparelWearer { Kind = "shooter" });
        C(Near(shooter.Rating, beltR.Rating.OverallShooter) && shooter.Why[0].why == "kind:shooter" && shooter.Why[0].delta < -5f,
            $"a shooter rates it as the review's shooter: it stops their own shots ({shooter.Rating})");
        var flakR = reports["Apparel_FlakVest"];
        var anyone = ApparelForMath.For(flakR, new ApparelWearer { Kind = "both" });
        C(Near(anyone.Rating, flakR.Rating.Overall) && ApparelForMath.Telling(anyone).Count == 0, "someone who fights both ways rates a piece for anyone as the review");
        var none = ApparelForMath.For(flakR, new ApparelWearer { Kind = "none" });
        C(none.Why[0].why == "kind:none", "someone who doesn't fight: their own step");
        C(Near(ApparelForMath.For(beltR, new ApparelWearer { Kind = "shooter" }).Why.Sum(s => s.delta), shooter.Rating - shooter.Reference, 0.01f), "the steps add up to the difference");

        // Their body: flak pants for someone with no legs left.
        var noLegs = new ApparelWearer { Kind = "both", MissingGroups = { "Legs" } };
        var flakVest = ApparelForMath.For(flakR, noLegs);
        C(flakVest.Why.First(s => s.why == "body").delta >= -0.05f, "a vest's protection holds on a body without legs (it covers the torso)");
        C(ApparelForMath.BodyWithout(new[] { "Legs" }).Length == ApparelRateMath.Human.Length - 1 && ApparelForMath.BodyWithout(new List<string>()) == null
          && ApparelForMath.BodyWithout(new[] { "LeftHand" }).Length == ApparelRateMath.Human.Length - 2, "the zones a body keeps: no legs, no left hand (its fingers too)");
        var torsoless = ApparelForMath.For(flakR, new ApparelWearer { Kind = "both", MissingGroups = { "Neck" } });
        C(torsoless.Why.First(s => s.why == "body").delta > -1f, "a missing part shifts the rating only by its share of the hits");

        // Their weather: a parka in a mild place, in a cold one.
        var parkaR = reports["Apparel_Parka"];
        var mild = ApparelForMath.For(parkaR, new ApparelWearer { HasClimate = true, OutdoorNow = 20f, SeasonLow = 12f, SeasonHigh = 28f, ComfyMinWithout = 12f, ComfyMaxWithout = 28f });
        var cold = ApparelForMath.For(parkaR, new ApparelWearer { HasClimate = true, OutdoorNow = -20f, SeasonLow = -32f, SeasonHigh = -8f, ComfyMinWithout = 12f, ComfyMaxWithout = 28f });
        C(mild.Rating < parkaR.Rating.Overall && cold.Rating > parkaR.Rating.Overall, $"a parka rates lower where it is mild ({mild.Rating}) and higher in the cold ({cold.Rating}) than usually ({parkaR.Rating.Overall})");
        C(mild.Why.Any(s => s.why == "climate" && s.delta < 0f) && cold.Why.Any(s => s.why == "climate" && s.delta > 0f), "the weather is its own step");
        C(cold.NeedWarmth == 44f && Near(cold.Warmth, parkaR.Rating.Cold) && Near(cold.ComfyMinWith, 12f - parkaR.Rating.Cold), "how much colder it gets than they can bear without it, and the comfort with it");
        C(!cold.WarmEnough && mild.WarmEnough && mild.CoolEnough, "warm enough where it is mild; not in a -32 °C night with only a parka over 12 °C");
        var dressed = ApparelForMath.For(parkaR, new ApparelWearer { HasClimate = true, OutdoorNow = -20f, SeasonLow = -32f, SeasonHigh = -8f, ComfyMinWithout = -40f, ComfyMaxWithout = 20f });
        C(dressed.Rating < cold.Rating, "a second warm coat is worth less to someone already dressed for the cold");
        C(Near(ApparelForMath.ClimateWorth(30f, 0f, 1f), 30f * 0.2f * 0.01f) && Near(ApparelForMath.ClimateWorth(30f, 40f, 1f), 30f * 2f * 0.01f)
          && Near(ApparelForMath.ClimateWorth(30f, 10f, 1f), (10f * 2f + 20f * 0.2f) * 0.01f), "the degrees the season asks count with its weight and one more, the spare ones at a fifth");
        C(Near(ApparelForMath.For(parkaR, new ApparelWearer()).Rating, parkaR.Rating.Overall), "without a climate, the usual rating");

        // Moods: their other clothes already worn out; the other gender's.
        var shredded = Named("Apparel_FlakVest");
        Moods(shredded);
        shredded.MaxHitPoints = 200f;
        shredded.HitPoints = 80f;
        var shreddedR = ApparelRead.Full(shredded);
        var already = ApparelForMath.For(shreddedR, new ApparelWearer { Kind = "both", DamageStageKept = 1, Frayed = ("", -3f), Tattered = ("", -5f) });
        C(already.Why.Any(s => s.why == "worn-already" && s.delta >= 0f) && already.Rating >= shreddedR.Rating.Overall,
            "someone whose other clothes are worse worn already: this one's wear adds no mood");
        var gender = ApparelForMath.For(reports["Apparel_BasicShirt"], new ApparelWearer { WrongGender = true, WrongGenderMood = ("", -5f) });
        C(gender.Why.Any(s => s.why == "gender") && gender.Rating < reports["Apparel_BasicShirt"].Rating.Overall, "a piece of the other gender's: its mood counted for them");
        C(!ApparelForMath.For(reports["Apparel_BasicShirt"], new ApparelWearer { WrongGender = true, WrongGenderAlready = true, WrongGenderMood = ("", -5f) }).Why.Any(s => s.why == "gender"),
            "unless they already wear one");

        // How they fight.
        C(ApparelForMath.KindOf(false, true, "ranged", false, 10, 2) == "none" && ApparelForMath.KindOf(true, false, "", false, 10, 2) == "melee"
          && ApparelForMath.KindOf(true, true, "ranged", true, 2, 10) == "shooter" && ApparelForMath.KindOf(true, true, "melee", false, 12, 2) == "melee"
          && ApparelForMath.KindOf(true, true, "", true, 8, 8) == "melee" && ApparelForMath.KindOf(true, true, "", false, 12, 6) == "shooter"
          && ApparelForMath.KindOf(true, true, "", false, 8, 9) == "both", "how they fight: what they hold first, then a brawler, then their skills four apart");

        // ---------- Protection by region, words and order ----------
        var regions = ApparelForMath.Coverage(new[] { "Torso", "Neck" });
        C(regions.Count == 8 && Near(regions.Sum(r => r.Shots), 1f, 0.001f), "eight regions, their shares of the shots adding up");
        C(regions.First(r => r.Key == "torso").Covered == 1f && regions.First(r => r.Key == "legs").Covered == 0f && regions.First(r => r.Key == "neck").Covered == 1f, "a vest covers the torso and neck");
        var hat = ApparelForMath.Coverage(new[] { "UpperHead" }).First(r => r.Key == "head");
        C(hat.Covered > 0.3f && hat.Covered < 0.9f, $"a hat covers part of the head ({hat.Covered:0.00}), not the jaw or the eyes");
        C(Near(ApparelForMath.Coverage(new[] { "FullHead" }).First(r => r.Key == "head").Covered, 1f), "a helmet over the whole head covers all of it");
        C(ApparelForMath.Coverage(new[] { "Waist" }).All(r => r.Covered == 0f), "a belt covers no region the shots land on");
        C(ApparelForMath.Regions.SelectMany(r => r.zones).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, ApparelRateMath.Human.Length)), "every zone of the body in one region");
        C(ApparelForMath.BothDiffer(beltR.Rating) && !ApparelForMath.BothDiffer(flakR.Rating), "a shooter's and a melee fighter's shown where they differ by half a point");
        C(ApparelForMath.SubKeys(flakR.Rating).First() == "guns" && ApparelForMath.SubKeys(flakR.Rating).All(k => flakR.Rating.Sub.ContainsKey(k)), "the sub-ratings in their order, guns first");
        C(reports.Values.All(r => r.Rating.Sub.Keys.All(k => ApparelForMath.SubOrder.Contains(k))), "every sub-rating has its place in the order");
        C(ApparelForMath.Best(flakR.Rating, "guns") == flakR.Rating.BestFor.Contains("gunfights"), "best for, as the rating marks it");
        C(ApparelForMath.Weak(reports["Apparel_Parka"].Rating, "in the heat") == reports["Apparel_Parka"].Rating.WeakWhen.Contains("in the heat"), "weak when, as the rating marks it");
        C(ApparelForMath.KeyWord("psychic shelter") == "Psychicshelter" && ApparelForMath.KeyWord("off-hand shield") == "Offhandshield", "keys for words");

        // ---------- The seams ----------
        string live = Read("Source/ColonyCouncil/Apparel/ApparelLive.cs");
        string card = Read("Source/ColonyCouncil/Apparel/ApparelCardWindow.cs");
        string info = Read("Source/ColonyCouncil/Apparel/ApparelInfoCard.cs");
        string gear = Read("Source/ColonyCouncil/Apparel/ApparelGearTab.cs");
        string first = Read("Source/ColonyCouncil/Apparel/ApparelCardFirst.cs");
        string weaponFirst = Read("Source/ColonyCouncil/Weapons/WeaponCardFirst.cs");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_Apparel.xml");
        C(live.Contains("ApparelRead.Full(") && live.Contains("StatRequest.For(") && live.Contains("StatWorker.StatOffsetFromGear") && live.Contains("ThoughtDefOf.ApparelDamaged")
          && live.Contains("WornByCorpse") && live.Contains("CompBiocodable"), "the game reads the piece in hand through the shared reading: its stats, offsets, wear, a dead wearer, biocoding");
        C(Read("Tools/ApparelReview/Program.cs").Contains("ApparelRead.Full(Inputs.DefFacts(n))") && Read("Tools/ApparelReview/ApparelReview.csproj").Contains("ApparelReadMath.cs"),
            "the review rates every piece through the same reading");
        foreach (var (field, label, needs) in new[] { ("apparelRatings", "ApparelRatings", "deepHooks"), ("apparelGearTab", "ApparelGearTab", "apparelRatings,deepHooks"),
                     ("apparelCardFirst", "ApparelCardFirst", "apparelRatings,deepHooks") })
        {
            C(settings.Contains("public bool " + field + " = true;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", true);"), $"{field}: a setting, on at first, saved");
            C(catalog.Contains("Toggle(\"" + field + "\", \"CC_Settings_" + label + "\", \"CC_SetNote_" + label + "\", \"" + needs + "\");"), $"{field}: on the settings page, under Apparel ({needs})");
            C(keys.Contains("<CC_Settings_" + label + ">") && keys.Contains("<CC_Settings_" + label + "_Tip>") && keys.Contains("<CC_SetNote_" + label + ">"), $"{field}: its label, tip and note");
        }
        C(catalog.Contains("Section(\"CC_SetSec_Apparel\");") && catalog.IndexOf("CC_SetSec_Apparel", StringComparison.Ordinal) > catalog.IndexOf("CC_SetSec_Weapons", StringComparison.Ordinal)
          && keys.Contains("<CC_SetSec_Apparel>Apparel</CC_SetSec_Apparel>"), "the Apparel group under How you're told, after Weapons");
        C(info.Contains("[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]") && info.Contains("[HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]"),
            "the info card's entries and its button are hooks of their own");
        C(Read("Defs/Misc/Rimfolk_ApparelStats.xml").Contains("<defName>Rimfolk_ApparelRating</defName>") && Read("Defs/Misc/Rimfolk_ApparelStats.xml").Contains("<displayOrder>0</displayOrder>"),
            "the rating's category is sorted first in the info card");
        C(weaponFirst.Contains("return !WeaponCardFirst.Swapped(card) && !ApparelCardFirst.Swapped(card);") && weaponFirst.Contains("if (broken || !(window is Dialog_InfoCard card)) return true;"),
            "the one hook on the window stack opens the apparel card first too, every other window untouched");
        C(first.Contains("WeaponCardFirst.insideGameCard") && first.Contains("letThrough") && card.Contains("ApparelCardFirst.OpenGameCard(t, d, s)"),
            "links inside the game's card stay in it; \"All the game's stats\" is let through");
        C(first.Contains("ApparelCardWindow.On && (ColonyCouncilMod.Settings?.apparelCardFirst ?? true)") && info.Contains("if (ApparelCardFirst.On) card.Close(false);"),
            "only while the card is on; going back from the game's card closes it");
        C(gear.Contains("[HarmonyPatch(typeof(ITab_Pawn_Gear), \"DrawThingRow\")]") && gear.Contains("NiceInventoryTab.ApparelGridSlot") && gear.Contains("WeaponGearTab.NiceRow")
          && gear.Contains("WeaponGearTab.NiceCell"), "the badge on the game's gear tab and Nice Inventory Tab's rows, cells and apparel slots");
        foreach (var soft in new[] { "Patch_ApparelNiceInventoryRow", "Patch_ApparelNiceInventoryCell", "Patch_ApparelNiceInventorySlot" })
        {
            int at = gear.IndexOf("class " + soft, StringComparison.Ordinal);
            string body = at >= 0 ? gear.Substring(at, Math.Min(1600, gear.Length - at)) : "";
            C(body.Contains("Prepare()") && body.Contains("TargetMethod()") && body.Contains("StandsDownWhy()"), $"{soft}: skipped cleanly without Nice Inventory Tab");
        }
        C(card.Contains(": ItemCardWindow") && card.Contains("base(\"ApparelCard\")") && Read("Source/ColonyCouncil/UI/ItemCardWindow.cs").Contains("public abstract class ItemCardWindow : FramedWindow"),
            "Rimfolk's apparel card is a framed window on the item card's base");
        C(FramedWindowsMath.TryKey("ApparelCard", out var ak) && ak.leastW >= 420f && ak.leastH >= 420f, "the card's frame key and its least size");
        C(card.Contains("UiKit.ItemPortrait(") || Read("Source/ColonyCouncil/UI/ItemCardWindow.cs").Contains("UiKit.ItemPortrait(pic, thing, def, stuff, true)"), "the piece's own picture in the card's head");

        // Every key the code names exists, and every key made from a word.
        var codeKeys = new HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Apparel"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"(CC_Apparel_[A-Za-z0-9_]+)\""))
                codeKeys.Add(m.Groups[1].Value);
        foreach (var k in codeKeys.Where(k => !k.EndsWith("_")))
            C(keys.Contains("<" + k + ">"), $"the key {k} is in Rimfolk_Apparel.xml");
        foreach (var role in ApparelRateMath.Roles) C(keys.Contains("<CC_Apparel_Role_" + ApparelForMath.KeyWord(role) + ">"), $"the role {role} in words");
        foreach (var sub in ApparelForMath.SubOrder)
            C(keys.Contains("<CC_Apparel_Sub_" + ApparelForMath.KeyWord(sub) + ">") && keys.Contains("<CC_Apparel_SubExpl_" + ApparelForMath.KeyWord(sub) + ">"), $"the sub-rating {sub} in words");
        foreach (var sit in reports.Values.SelectMany(r => r.Rating.Situations.Keys).Distinct())
            C(keys.Contains("<CC_Apparel_Sit_" + ApparelForMath.KeyWord(sit) + ">") && keys.Contains("<CC_Apparel_SitExpl_" + ApparelForMath.KeyWord(sit) + ">"), $"the situation {sit} in words");
        foreach (var (key, _) in ApparelForMath.Regions) C(keys.Contains("<CC_Apparel_Region_" + ApparelForMath.KeyWord(key) + ">"), $"the region {key} in words");
        foreach (var kind in new[] { "shooter", "melee", "both", "none" }) C(keys.Contains("<CC_Apparel_Kind_" + ApparelForMath.KeyWord(kind) + ">"), $"how {kind} fights in words");
        C(!keys.Contains("—") && !Regex.IsMatch(keys, @"<CC_[A-Za-z_]+>[^<]*!"), "no em dashes or exclamation marks in the apparel keys");

        var almanac = Read("Defs/AlmanacArticleDefs/Almanac_Apparel.xml");
        C(almanac.Contains("<label>judging armour and clothes</label>") && !almanac.Contains("—"), "the Almanac's page, judging armour and clothes");
        return n;
    }
}
