using ColonyCouncil;

/// <summary>
/// The base game's pieces as the apparel rating reads them, built from their defs' numbers (Core, Royalty; the stuffs'
/// powers from Items_Resource_Stuff): the anchors the scale is set on. Tools/ApparelReview links this file and checks
/// that rating the extracted defs gives the same numbers.
/// </summary>
public static class ApparelAnchors
{
    public static readonly Dictionary<string, (string label, Dictionary<string, float> powers)> Stuffs = new()
    {
        ["DevilstrandCloth"] = ("devilstrand", Powers(1.40f, 0.36f, 3.00f, 20f, 24f)),
        ["Steel"] = ("steel", Powers(0.90f, 0.45f, 0.60f, 3f, 0f)),
        ["Cloth"] = ("cloth", Powers(0.36f, 0f, 0.18f, 18f, 18f)),
        ["Leather_Plain"] = ("leather", Powers(0.81f, 0.24f, 1.5f, 16f, 16f)),
        ["Synthread"] = ("synthread", Powers(0.94f, 0.26f, 0.90f, 22f, 22f)),
        ["Hyperweave"] = ("hyperweave", Powers(2.00f, 0.54f, 2.88f, 26f, 26f)),
    };

    static Dictionary<string, float> Powers(float sharp, float blunt, float heat, float cold, float hot) => new()
    {
        ["StuffPower_Armor_Sharp"] = sharp, ["StuffPower_Armor_Blunt"] = blunt, ["StuffPower_Armor_Heat"] = heat,
        ["StuffPower_Insulation_Cold"] = cold, ["StuffPower_Insulation_Heat"] = hot,
    };

    static readonly string[] Body5 = { "Torso", "Neck", "Shoulders", "Arms", "Legs" };

    /// <summary>A piece made as made (no stuff): its armour and insulation, its offsets, groups and layers.</summary>
    static ApparelFacts Made(string label, float sharp, float blunt, float heat, float cold, float hot, string[] groups, string[] layers,
        float hp, float mass, params (string stat, float v)[] offsets)
    {
        var a = new ApparelFacts { Label = label, Groups = groups.ToList(), Layers = layers.ToList() };
        a.StatBases["ArmorRating_Sharp"] = sharp;
        a.StatBases["ArmorRating_Blunt"] = blunt;
        a.StatBases["ArmorRating_Heat"] = heat;
        a.StatBases["Insulation_Cold"] = cold;
        a.StatBases["Insulation_Heat"] = hot;
        a.StatBases["MaxHitPoints"] = hp;
        a.StatBases["Mass"] = mass;
        foreach (var (s, v) in offsets) a.Offsets[s] = v;
        return a;
    }

    /// <summary>A piece made of stuff: its multipliers for armour and insulation.</summary>
    static ApparelFacts Stuffed(string label, string stuff, float armour, float cold, float hot, string[] groups, string[] layers, float hp, float mass,
        params (string stat, float v)[] offsets)
    {
        var a = new ApparelFacts { Label = label, Groups = groups.ToList(), Layers = layers.ToList(), StuffDef = stuff };
        a.StatBases["StuffEffectMultiplierArmor"] = armour;
        a.StatBases["StuffEffectMultiplierInsulation_Cold"] = cold;
        a.StatBases["StuffEffectMultiplierInsulation_Heat"] = hot;
        a.StatBases["MaxHitPoints"] = hp;
        a.StatBases["Mass"] = mass;
        a.StuffLabel = Stuffs[stuff].label;
        a.StuffPowers = Stuffs[stuff].powers;
        foreach (var (s, v) in offsets) a.Offsets[s] = v;
        return a;
    }

    public static ApparelFacts WithStuff(ApparelFacts a, string stuff)
    {
        var b = Stuffed(a.Label, stuff, ApparelRateMath.Stat(a.StatBases, "StuffEffectMultiplierArmor"), ApparelRateMath.Stat(a.StatBases, "StuffEffectMultiplierInsulation_Cold"),
            ApparelRateMath.Stat(a.StatBases, "StuffEffectMultiplierInsulation_Heat"), a.Groups.ToArray(), a.Layers.ToArray(), ApparelRateMath.Stat(a.StatBases, "MaxHitPoints"),
            ApparelRateMath.Stat(a.StatBases, "Mass"));
        foreach (var kv in a.Offsets) b.Offsets[kv.Key] = kv.Value;
        return b;
    }

    static ApparelFacts Belt(string label, Action<ApparelFacts> fill)
    {
        var a = new ApparelFacts { Label = label, Groups = new List<string> { "Waist" }, Layers = new List<string> { "Belt" }, CountsAsClothes = false, CareIfDamaged = false, WearPerDay = 0f };
        a.StatBases["MaxHitPoints"] = 100f;
        a.StatBases["Mass"] = 3f;
        fill(a);
        return a;
    }

    public static readonly string[] Torso = { "Torso", "Neck" }, Jacket = { "Torso", "Neck", "Shoulders", "Arms" };

    /// <summary>The game's packs refill with chemfuel (CompProperties_ApparelReloadable's ammoDef).</summary>
    static ApparelPower Fuelled(ApparelPower p)
    {
        p.Ammo = "chemfuel";
        return p;
    }

    /// <summary>A piece's rating in each of the common stuffs of its kind, for the "Stuff matters" tag.</summary>
    public static Dictionary<string, float> ByStuff(string name)
    {
        var a = Pieces[name]();
        var d = new Dictionary<string, float>();
        if (a.StuffDef == "") return d;
        string role = Rate(name).Role;
        foreach (var s in a.StuffDef == "Steel" ? new[] { "Steel" } : new[] { "Cloth", "Synthread", "DevilstrandCloth", "Hyperweave" })
            d[Stuffs[s].label] = ApparelRateMath.Rate(WithStuff(a, s), role).Overall;
        return d;
    }

    /// <summary>The anchors, by name: the base game's armour, helmets, utility, weather gear, clothes and psychic pieces.</summary>
    public static readonly Dictionary<string, Func<ApparelFacts>> Pieces = new()
    {
        ["flak vest"] = () => Made("flak vest", 1.00f, 0.36f, 0.27f, 1f, 0f, Torso, new[] { "Middle" }, 200, 4, ("MoveSpeed", -0.12f)),
        ["flak pants"] = () => Made("flak pants", 0.55f, 0.08f, 0.10f, 3.5f, 1f, new[] { "Legs" }, new[] { "OnSkin", "Middle" }, 200, 4, ("MoveSpeed", -0.12f)),
        ["flak jacket"] = () => Made("flak jacket", 0.55f, 0.08f, 0.10f, 14.4f, 3f, Jacket, new[] { "Shell" }, 200, 7, ("MoveSpeed", -0.12f)),
        ["plate armor"] = () => Stuffed("plate armor", "Steel", 0.9f, 1f, 0f, Body5, new[] { "Middle", "Shell" }, 290, 15, ("MoveSpeed", -0.8f)),
        ["recon armor"] = () => Made("recon armor", 0.92f, 0.40f, 0.46f, 64f, 9f, Body5, new[] { "Middle", "Shell" }, 280, 9, ("VacuumResistance", 0.3f)),
        ["marine armor"] = () => Made("marine armor", 1.06f, 0.45f, 0.54f, 68f, 10f, Body5, new[] { "Middle", "Shell" }, 340, 12, ("MoveSpeed", -0.25f), ("VacuumResistance", 0.3f)),
        ["cataphract armor"] = () => Made("cataphract armor", 1.20f, 0.50f, 0.60f, 70f, 12f, Body5, new[] { "Middle", "Shell" }, 400, 15, ("MoveSpeed", -0.5f), ("VacuumResistance", 0.3f)),
        ["simple helmet"] = () => Stuffed("simple helmet", "Steel", 0.5f, 0.15f, 0f, new[] { "UpperHead" }, new[] { "Overhead" }, 100, 2),
        ["flak helmet"] = () => Stuffed("flak helmet", "Steel", 0.7f, 0.15f, 0f, new[] { "UpperHead" }, new[] { "Overhead" }, 120, 1.2f),
        ["recon helmet"] = () => Made("recon helmet", 0.92f, 0.40f, 0.46f, 4f, 2f, new[] { "FullHead" }, new[] { "Overhead" }, 120, 1, ("VacuumResistance", 0.65f)),
        ["marine helmet"] = () => Made("marine helmet", 1.06f, 0.45f, 0.54f, 4f, 2f, new[] { "FullHead" }, new[] { "Overhead" }, 150, 1.5f, ("VacuumResistance", 0.67f)),
        ["cataphract helmet"] = () => Made("cataphract helmet", 1.20f, 0.50f, 0.60f, 4f, 2f, new[] { "FullHead" }, new[] { "Overhead" }, 180, 2, ("VacuumResistance", 0.68f)),
        ["shield belt"] = () => Belt("shield belt", a =>
        {
            a.StatBases["EnergyShieldEnergyMax"] = 1.1f;
            a.StatBases["EnergyShieldRechargeRate"] = 0.13f;
            ApparelRateMath.ReadComp(a, "CompProperties_Shield", _ => "", _ => null);
        }),
        ["jump pack"] = () => Belt("jump pack", a => a.Powers.Add(Fuelled(ApparelRateMath.PowerOfVerb("Verb_Jump", "jump", 5, false, 23.9f, 0f, null, false, "")))),
        ["smokepop pack"] = () => Belt("smokepop pack", a => a.Powers.Add(Fuelled(ApparelRateMath.PowerOfVerb("Verb_Smokepop", "pop smoke", 3, false, 1.42f, 4.9f, null, false, "")))),
        ["firefoam pop pack"] = () => Belt("firefoam pop pack", a => a.Powers.Add(Fuelled(ApparelRateMath.PowerOfVerb("Verb_FirefoamPop", "pop firefoam", 1, false, 1.42f, 4.9f, null, false, "")))),
        ["low-shield pack"] = () => Belt("low-shield pack", a => a.Powers.Add(ApparelRateMath.PowerOfVerb("Verb_DeployBroadshield", "deploy low-shield", 1, true, 1.42f, 0f, null, false, ""))),
        ["parka"] = () => Stuffed("parka", "DevilstrandCloth", 0.2f, 2.0f, 0f, Jacket, new[] { "Shell" }, 180, 2),
        ["tribalwear"] = () => Stuffed("tribalwear", "DevilstrandCloth", 0.2f, 0.55f, 0.55f, new[] { "Torso", "Legs" }, new[] { "OnSkin" }, 100, 0.5f),
        ["duster"] = () => Stuffed("duster", "DevilstrandCloth", 0.3f, 0.6f, 0.85f, Body5, new[] { "Shell" }, 200, 2),
        ["jacket"] = () => Stuffed("jacket", "DevilstrandCloth", 0.3f, 0.8f, 0.3f, Jacket, new[] { "Shell" }, 160, 1.7f),
        ["T-shirt"] = () => Stuffed("T-shirt", "DevilstrandCloth", 0.2f, 0.22f, 0.1f, new[] { "Torso", "Shoulders" }, new[] { "OnSkin" }, 100, 0.25f),
        ["button-down shirt"] = () => Stuffed("button-down shirt", "DevilstrandCloth", 0.2f, 0.26f, 0.1f, Jacket, new[] { "OnSkin" }, 100, 0.3f),
        ["pants"] = () => Stuffed("pants", "DevilstrandCloth", 0.2f, 0.2f, 0.08f, new[] { "Legs" }, new[] { "OnSkin" }, 100, 0.5f),
        ["tuque"] = () => Stuffed("tuque", "DevilstrandCloth", 0.2f, 0.5f, 0f, new[] { "UpperHead" }, new[] { "Overhead" }, 80, 0.07f),
        ["cowboy hat"] = () => Stuffed("cowboy hat", "DevilstrandCloth", 0.2f, 0.1f, 0.5f, new[] { "UpperHead" }, new[] { "Overhead" }, 80, 0.14f, ("SocialImpact", 0.1f)),
        ["psychic foil helmet"] = () => Made("psychic foil helmet", 0.09f, 0.09f, 0.27f, 2f, 1f, new[] { "UpperHead" }, new[] { "Overhead" }, 80, 1, ("PsychicSensitivity", -0.9f)),
        ["eltex skullcap"] = () => Made("eltex skullcap", 0f, 0f, 0f, 0f, 0f, new[] { "UpperHead" }, new[] { "Overhead" }, 80, 0.8f, ("PsychicSensitivityOffset", 0.4f), ("PsychicEntropyRecoveryRate", 0.091f)),
        // For the two wearers: Royalty's gunlink (a shooter's), VFE Medieval 2's heavy plate helmet (a melee fighter's),
        // and Vanilla Armour Expanded's plate gloves (made as armour, rated with clothing).
        ["gunlink"] = () => Made("gunlink", 0f, 0f, 0f, 0f, 0f, new[] { "UpperHead" }, new[] { "Overhead" }, 120, 1f, ("ShootingAccuracyPawn", 3f)),
        ["heavy plate helmet"] = () => Stuffed("heavy plate helmet", "Steel", 0.55f, 0.15f, 0f, new[] { "FullHead" }, new[] { "Overhead" }, 150, 3.5f,
            ("MeleeHitChance", -2f), ("ShootingAccuracyPawn", -8f)),
        ["plate gloves"] = () => Stuffed("plate gloves", "Steel", 0.4f, 0.15f, 0f, new[] { "Hands" }, new[] { "Middle" }, 100, 2.2f, ("WorkSpeedGlobal", -0.05f)),
    };

    /// <summary>A piece's role is judged in plain stuff (cloth for fabric, steel for metal), as the review judges it.</summary>
    public static ApparelRating Rate(string name)
    {
        var a = Pieces[name]();
        return ApparelRateMath.Rate(a, a.StuffDef == "" ? null : WithStuff(a, a.StuffDef == "Steel" ? "Steel" : "Cloth"));
    }

    public static IEnumerable<(string name, ApparelRating r)> All()
    {
        foreach (var k in Pieces.Keys) yield return (k, Rate(k));
    }
}

/// <summary>
/// The apparel rating (Apparel/ApparelRateMath) and its tags (ApparelTagMath): the game's armour roll layer by layer, the
/// human body's zones, the reference enemies, shields, powers, the roles and their scales, the base game's pieces in the
/// order experienced players know them, and the tags' vocabulary, colours and rules.
/// </summary>
public static class ApparelRateTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "apparel rating: " + what);
        }
        bool Near(float a, float b, float eps = 0.005f) => Math.Abs(a - b) <= eps;
        var R = new Dictionary<string, ApparelRating>();
        foreach (var (name, r) in ApparelAnchors.All()) R[name] = r;
        float O(string k) => R[k].Overall;

        // ---------- The body and the game's rules ----------
        float ranged = 0f, melee = 0f, blunt = 0f;
        foreach (var z in ApparelRateMath.Human) { ranged += z.Ranged; melee += z.Melee; blunt += z.MeleeBlunt; }
        C(Near(ranged, 1f, 0.002f) && Near(melee, 1f, 0.002f) && Near(blunt, 1f, 0.002f), "the human body's zones take every shot, blow and blunt blow");
        C(Near(ApparelRateMath.CoverageOf(new[] { "Torso", "Neck" }), 0.4416f / 1.06f, 0.003f), "a flak vest covers the torso and neck: 44% of shots, the head counting double");
        C(ApparelRateMath.Human.Any(z => z.Groups.Contains("Eyes") && z.MeleeBlunt == 0f && z.Melee > 0f), "a blunt blow never lands on an eye");
        C(ApparelRateMath.Human.Any(z => z.Groups.Contains("Eyes") && z.Groups.Contains("UpperHead") && z.Melee == 0f && z.Ranged > 0f),
            "the brain is inside the skull: a shot can reach it, a blow lands outside");
        var one = new List<ApparelLayer> { new ApparelLayer { Groups = new[] { "Torso" }, Sharp = 1f, Blunt = 0.3f, Heat = 0.5f } };
        C(Near(ApparelRateMath.AfterLayers(one, WeaponArmour.Sharp, 0.2f), WeaponRateMath.ArmourFactor(1f, 0.2f)), "one layer: the weapon rating's own armour roll");
        var two = new List<ApparelLayer>
        {
            new ApparelLayer { Groups = new[] { "Torso" }, Sharp = 1f, Blunt = 0.2f },
            new ApparelLayer { Groups = new[] { "Torso" }, Sharp = 1f, Blunt = 0.6f },
        };
        float stack = ApparelRateMath.AfterLayers(two, WeaponArmour.Sharp, 0f);
        float expect = 0f * 0.5f + 0.5f * 0.5f * (1f - 0.75f * 0.6f);
        C(Near(stack, expect, 0.001f), $"two layers: a sharp hit halved by the shell meets the vest as blunt ({stack:0.000} vs {expect:0.000})");
        C(ApparelRateMath.AfterLayers(new List<ApparelLayer> { new ApparelLayer { Groups = new[] { "Torso" }, Sharp = 2.5f } }, WeaponArmour.Sharp, 0f) == 0f,
            "armour is held at 2: nothing gets through it, as the game caps it");
        var heat = new List<ApparelLayer> { new ApparelLayer { Groups = new[] { "Torso" }, Sharp = 0f, Heat = 1f } };
        C(Near(ApparelRateMath.AfterLayers(heat, WeaponArmour.Heat, 0f), 0.25f) && ApparelRateMath.AfterLayers(heat, WeaponArmour.Sharp, 0f) == 1f,
            "heat armour meets fire and nothing else");
        var plain = new ApparelFacts();
        var t = ApparelRateMath.Toughness(plain, false);
        C(ApparelRateMath.Families.All(f => Near(t[f], 1f)), "with nothing on, every enemy deals its full damage");
        C(Near(ApparelRateMath.FamilyWeights.Values.Sum(), 1f), "the families' weights add up to one");
        foreach (var fam in ApparelRateMath.Families)
            C(Near(ApparelRateMath.Threats.Where(x => x.Family == fam).Sum(x => x.Weight), 1f, 0.01f), "the " + fam + "' attacks add up to the whole family");
        C(ApparelRateMath.Threats.Any(x => x.Family == "animals" && x.Label.StartsWith("grizzly bear's claw")) &&
          !ApparelRateMath.Threats.Any(x => x.Family == "animals" && x.Label.StartsWith("grizzly bear's head")),
            "animals attack by the game's own choice: a bear's claws, never its head butt");

        // ---------- Shields and powers ----------
        var sb = ApparelAnchors.Pieces["shield belt"]();
        C(sb.Shields.Count == 1 && Near(sb.Shields[0].Capacity, 1.1f / 0.033f, 0.1f) && Near(sb.Shields[0].Recharge, 0.13f / 0.033f, 0.05f) && sb.Shields[0].BlocksShooting,
            "the shield belt takes 33 damage, recharges 3.9 a second, and blocks its wearer's shots (CompShield)");
        float share = ApparelRateMath.ShieldShare(sb.Shields[0]);
        C(share > 0.4f && share < 0.7f, $"against 6 damage a second for 30 seconds, it takes about half ({share:0.00})");
        C(ApparelRateMath.ShieldShare(new ApparelShield { Capacity = 10f, Recharge = 7f }) == ApparelRateMath.ShieldCap,
            "a shield that recharges faster than the fire never breaks, and still lets some through (an EMP shot, a heavy volley)");
        C(R["shield belt"].Role == "utility" && R["shield belt"].For == "a melee fighter" && R["shield belt"].Situations["for a shooter"] == 1f,
            "the shield belt is utility for a melee fighter, and nothing for a shooter");
        C(R["shield belt"].OverallShooter == 1f && R["shield belt"].OverallMelee >= 7f && R["shield belt"].Overall == R["shield belt"].OverallMelee,
            $"the shield belt rates {R["shield belt"].OverallMelee} for a melee fighter, 1.0 for a shooter, and is rated for the melee fighter");
        C(R["shield belt"].Toughness["guns"] > 1.6f && Near(R["shield belt"].Toughness["melee"], 1f), "it stops shots, not blows");
        C(R["jump pack"].FightMelee > R["jump pack"].FightShooter, "a jump helps a melee fighter's charge more than a shooter");
        C(R["smokepop pack"].Toughness["guns"] > 1.1f && Near(R["smokepop pack"].Toughness["melee"], 1f), "smoke spoils shots, not blows");
        C(R["firefoam pop pack"].Toughness["fire"] > 1.3f, "firefoam puts fires out around its wearer");

        // ---------- The anchors: protection in the order players know ----------
        C(R["flak vest"].Role == "body armour" && R["marine armor"].Role == "body armour" && R["plate armor"].Role == "body armour", "armour is body armour");
        C(R["simple helmet"].Role == "helmet" && R["cataphract helmet"].Role == "helmet" && R["tuque"].Role == "headgear" && R["cowboy hat"].Role == "headgear",
            "helmets are helmets, hats are headgear");
        C(R["parka"].Role == "cold-weather gear" && R["duster"].Role == "hot-weather gear" && R["tribalwear"].Role == "everyday clothing",
            "the parka is cold-weather gear, the duster hot-weather gear, tribalwear everyday clothing");
        C(R["psychic foil helmet"].Slot == "head" && R["eltex skullcap"].Role == "ceremonial or psychic", "the eltex skullcap is a psycaster's piece");
        C(O("flak jacket") < O("plate armor") && O("flak vest") < O("plate armor") && O("plate armor") < O("recon armor") &&
          O("recon armor") < O("marine armor") && O("marine armor") < O("cataphract armor"),
            $"body armour rises: flak vest {O("flak vest")}, flak jacket {O("flak jacket")}, plate {O("plate armor")}, recon {O("recon armor")}, marine {O("marine armor")}, cataphract {O("cataphract armor")}");
        C(O("simple helmet") < O("flak helmet") && O("flak helmet") < O("recon helmet") && O("recon helmet") < O("marine helmet") && O("marine helmet") < O("cataphract helmet"),
            $"helmets rise: simple {O("simple helmet")}, flak {O("flak helmet")}, recon {O("recon helmet")}, marine {O("marine helmet")}, cataphract {O("cataphract helmet")}");
        C(O("cataphract armor") >= 7.5f && O("cataphract armor") < 9.5f && O("flak vest") >= 3.5f && O("flak vest") <= 5.5f,
            "the scale has room at both ends: a flak vest in the middle, cataphract armour high but not at the top");
        C(R["parka"].Sub["guns"] < 4f && R["tribalwear"].Sub["guns"] < 3.5f && R["parka"].Sub["cold"] >= 7f && R["tribalwear"].Sub["cold"] >= 4f,
            $"tribalwear and the parka: low as armour (guns {R["tribalwear"].Sub["guns"]}, {R["parka"].Sub["guns"]}), strong for cold ({R["tribalwear"].Sub["cold"]}, {R["parka"].Sub["cold"]})");
        C(R["parka"].Sub["cold"] > R["jacket"].Sub["cold"] && R["jacket"].Sub["cold"] > R["T-shirt"].Sub["cold"], "the parka warmer than the jacket, the jacket than a T-shirt");
        C(R["marine armor"].Sub["guns"] > R["flak vest"].Sub["guns"] + 2f, "marine armour stops far more bullets than a flak vest");
        C(R["marine armor"].Sub["cold"] >= 7f, "marine armour is warm in a blizzard (68 °C)");
        C(R["flak vest"].Sub["cold"] < 3f && R["flak vest"].WeakWhen.Contains("in the cold"), "a flak vest is useless in a blizzard");
        C(R["plate armor"].FightMelee < R["plate armor"].FightShooter * 1.1f, "plate armour's slow walk costs a melee fighter more");
        C(O("shield belt") > O("jump pack") && O("jump pack") > O("firefoam pop pack") && O("shield belt") >= 6.5f,
            $"utility: the shield belt {O("shield belt")} above the jump pack {O("jump pack")}, the smokepop {O("smokepop pack")}, firefoam {O("firefoam pop pack")}");
        C(R["psychic foil helmet"].Sub.ContainsKey("psychic shelter") && R["psychic foil helmet"].Sub["psychic shelter"] >= 7f, "the foil helmet shelters the mind");
        C(R["eltex skullcap"].Sub["psychic"] >= 6f, "the eltex skullcap lifts a psycaster");
        C(R["cataphract armor"].Situations["in the cold"] > R["cataphract armor"].Situations["in the heat"], "cataphract armour is better in the cold than in the heat");
        C(R["duster"].Situations["in the heat"] > R["duster"].Situations["in the cold"] && R["parka"].Situations["in the cold"] > R["parka"].Situations["in the heat"],
            "the duster for the heat, the parka for the cold");
        var hyper = ApparelRateMath.Rate(ApparelAnchors.WithStuff(ApparelAnchors.Pieces["duster"](), "Hyperweave"), "hot-weather gear");
        var cloth = ApparelRateMath.Rate(ApparelAnchors.WithStuff(ApparelAnchors.Pieces["duster"](), "Cloth"), "hot-weather gear");
        C(hyper.Overall > R["duster"].Overall && R["duster"].Overall > cloth.Overall && hyper.Sub["guns"] > cloth.Sub["guns"] + 1f,
            $"stuff matters: a duster in hyperweave {hyper.Overall}, devilstrand {R["duster"].Overall}, cloth {cloth.Overall}");
        foreach (var (name, r) in R)
            C(r.Overall >= 1f && r.Overall <= 10f && Math.Abs(r.Overall * 10f - Math.Round(r.Overall * 10f)) < 0.001f, name + " rates 1.0 to 10.0 with one decimal");

        // ---------- Two wearers: a shooter and a melee fighter ----------
        foreach (var (name, r) in R)
        {
            bool clear = r.For != "anyone";
            float chosen = r.For == "a shooter" ? r.OverallShooter : r.For == "a melee fighter" ? r.OverallMelee : r.Overall;
            C(chosen == r.Overall && (!clear || r.Overall >= Math.Min(r.OverallShooter, r.OverallMelee)),
                $"{name}: rated for {r.For} ({r.Overall}; shooter {r.OverallShooter}, melee {r.OverallMelee})");
            C(clear || r.Overall >= Math.Min(r.OverallShooter, r.OverallMelee) - 0.05f && r.Overall <= Math.Max(r.OverallShooter, r.OverallMelee) + 0.05f,
                name + ": a piece for anyone rates between its shooter's and its melee fighter's ratings");
        }
        foreach (var name in new[] { "flak vest", "flak jacket", "marine armor", "cataphract armor", "recon helmet", "marine helmet", "parka" })
            C(R[name].For == "anyone" && Math.Abs(R[name].OverallShooter - R[name].OverallMelee) <= 0.3f,
                $"plain armour and clothes suit both: {name} {R[name].OverallShooter} for a shooter, {R[name].OverallMelee} for a melee fighter");
        C(R["plate armor"].For == "anyone" && R["plate armor"].OverallShooter > R["plate armor"].OverallMelee,
            "plate armour's slow walk costs a melee fighter more, but the walk alone doesn't make it a shooter's");
        C(R["gunlink"].For == "a shooter" && R["gunlink"].Overall == R["gunlink"].OverallShooter && R["gunlink"].OverallShooter >= R["gunlink"].OverallMelee + 3f,
            $"the gunlink is a shooter's: {R["gunlink"].OverallShooter} for a shooter, {R["gunlink"].OverallMelee} for a melee fighter");
        C(R["jump pack"].For == "a melee fighter" && R["jump pack"].OverallMelee > R["jump pack"].OverallShooter,
            $"the jump pack is a melee fighter's: {R["jump pack"].OverallMelee} against {R["jump pack"].OverallShooter}");
        var hp = R["heavy plate helmet"];
        C(hp.For == "a melee fighter" && hp.Role == "helmet" && hp.FightMelee > 2f * hp.FightShooter && hp.OverallMelee >= hp.OverallShooter,
            $"VFE Medieval 2's heavy plate helmet is a melee fighter's (fight x{hp.FightMelee:0.00} against a shooter's x{hp.FightShooter:0.00}: 8 levels off shooting)");
        C(hp.FightMelee < 1f && hp.OverallMelee <= 2f,
            $"and by the game's numbers it still costs a melee fighter more than it gives: 2 levels off melee hit (74% to 68%) against a head's armour (x{hp.FightMelee:0.00}, {hp.OverallMelee})");
        var gap = new ApparelFacts { Label = "small aim", Groups = new List<string> { "UpperHead" }, Layers = new List<string> { "Overhead" } };
        gap.StatBases["MaxHitPoints"] = 80f;
        gap.Offsets["ShootingAccuracyPawn"] = 0.5f;
        C(ApparelRateMath.Rate(gap).For == "anyone", "a small aim bonus (under a tenth more for a shooter) suits both");
        gap.Offsets["ShootingAccuracyPawn"] = 3f;
        C(ApparelRateMath.Rate(gap).For == "a shooter", "a gunlink's aim (+3 levels) is a tenth more for a shooter: rated for one");

        // ---------- The scale ----------
        C(ApparelRateMath.Scale(0f, 0.5f, 0.5f) == 1f && ApparelRateMath.Scale(0.5f, 0.5f, 0.5f) == 5.5f, "no worth is 1.0, the role's middle is 5.5");
        C(ApparelRateMath.Scale(50f, 0.5f, 0.5f) < 10f || ApparelRateMath.Scale(50f, 0.5f, 0.5f) == 10f, "a soft top");
        C(ApparelRateMath.Scale(5f, 0.5f, 0.5f) > ApparelRateMath.Scale(2f, 0.5f, 0.5f) && ApparelRateMath.Scale(5f, 0.5f, 0.5f) - ApparelRateMath.Scale(2f, 0.5f, 0.5f) < 1.5f,
            "the scale flattens toward the top: a mod's super-armour lands at 9-something");

        // ---------- Tags ----------
        var styles = WeaponTagMath.Categories;
        C(styles.Length == 6, "the same six tag categories as the weapons, one table");
        C(ApparelTagMath.ApparelTags.All(d => styles.Any(s => s.Category == d.Category)), "every apparel tag is in one of the six categories");
        C(ApparelTagMath.ApparelTags.Select(d => d.Id).Distinct().Count() == ApparelTagMath.ApparelTags.Length, "every tag id once");
        C(ApparelTagMath.ApparelTags.All(d => !d.Rule.Contains("—") && !d.Label.Contains("!") && !d.Rule.Contains("!")), "no em dashes or exclamation marks in the legend");
        foreach (var name in new[] { "flak vest", "marine armor", "parka", "shield belt", "tuque", "cataphract helmet", "jump pack", "duster", "eltex skullcap" })
        {
            var a = ApparelAnchors.Pieces[name]();
            var r = R[name];
            var tags = ApparelTagMath.For(r, a, byStuff: ApparelAnchors.ByStuff(name));
            C(tags.Count >= 3 && tags.Count <= 7, $"{name}: {tags.Count} tags ({string.Join(", ", tags.Select(x => x.Label))})");
            C(tags[0].Category == TagCategory.Role || tags.Any(x => x.Category == TagCategory.Role), name + ": its role is among its tags");
            C(tags.All(x => x.Why != ""), name + ": every tag says why");
            string line = ApparelTagMath.Line(r, tags);
            C(line.EndsWith(".") && !line.Contains("—") && !line.Contains("!"), name + ": a plain line, " + line);
        }
        var vestTags = ApparelTagMath.For(R["flak vest"], ApparelAnchors.Pieces["flak vest"]()).Select(x => x.Id).ToList();
        C(vestTags.Contains("slows") && vestTags.Contains("thin-cover"), "a flak vest slows you and covers little");
        var beltTags = ApparelTagMath.For(R["shield belt"], ApparelAnchors.Pieces["shield belt"]()).Select(x => x.Id).ToList();
        C(beltTags.Contains("shield") && beltTags.Contains("blocks-shooting") && beltTags.Contains("role-melee"), "the shield belt: Shield, Blocks shooting, For melee fighters");
        var helmTags = ApparelTagMath.For(hp, ApparelAnchors.Pieces["heavy plate helmet"]());
        C(helmTags.Any(x => x.Id == "role-melee") && helmTags.First(x => x.Category == TagCategory.Weakness).Id == "worse-aim",
            "the heavy plate helmet: For melee fighters, and Spoils your aim first among its weaknesses: " + string.Join(", ", helmTags.Select(x => x.Label)));
        var gunTags = ApparelTagMath.For(R["gunlink"], ApparelAnchors.Pieces["gunlink"]()).Select(x => x.Id).ToList();
        C(gunTags.Contains("role-shooter") && gunTags.Contains("aim") && !gunTags.Contains("role-melee"), "the gunlink: For shooters, Steadier aim");
        C(ApparelTagMath.Line(R["gunlink"], ApparelTagMath.For(R["gunlink"], ApparelAnchors.Pieces["gunlink"]())).Contains("for a shooter"), "the gunlink's line says it is a shooter's");
        var gloves = R["plate gloves"];
        var gloveTags = ApparelTagMath.For(gloves, ApparelAnchors.Pieces["plate gloves"]());
        string gloveLine = ApparelTagMath.Line(gloves, gloveTags);
        C(gloves.Role == "everyday clothing" && gloveTags[0].Id == "role-armour-piece" && gloveTags[0].Label == "Armour for the hands" &&
          !gloveTags.Any(x => x.Id == "role-everyday") && gloveLine.Contains("armour for the hands"),
            "plate gloves, rated with clothing, say what they are: " + string.Join(", ", gloveTags.Select(x => x.Label)) + "; " + gloveLine);
        var vestPiece = ApparelTagMath.For(R["flak vest"], ApparelAnchors.Pieces["flak vest"]()).Select(x => x.Id).ToList();
        C(!vestPiece.Contains("role-armour-piece") && !vestPiece.Contains("role-melee") && !vestPiece.Contains("role-shooter"), "a flak vest is body armour for anyone, no more tags");
        var parkaTags = ApparelTagMath.For(R["parka"], ApparelAnchors.Pieces["parka"]()).Select(x => x.Id).ToList();
        C(parkaTags.Contains("warm") && parkaTags.Contains("role-cold"), "the parka: Cold-weather, Warm");
        var jumpTags = ApparelTagMath.For(R["jump pack"], ApparelAnchors.Pieces["jump pack"]()).Select(x => x.Id).ToList();
        C(jumpTags.Contains("jump"), "the jump pack: Jump");
        var foil = ApparelTagMath.For(R["psychic foil helmet"], ApparelAnchors.Pieces["psychic foil helmet"]()).Select(x => x.Id).ToList();
        C(foil.Contains("shelter") && foil.Contains("blocks-psycasts"), "the foil helmet: Psychic shield, Blocks psycasts");
        var marine = ApparelTagMath.For(R["marine armor"], ApparelAnchors.Pieces["marine armor"]()).Select(x => x.Id).ToList();
        C(marine.Contains("stops-bullets") && marine.Contains("warm") && marine.Contains("slows"), "marine armour: Stops bullets, Warm, Slows you");
        return n;
    }
}
