using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>
/// Step 2 of the weapons (docs/design/weapons.md): the game's reading of a weapon (Weapons/WeaponReadMath.cs, the same file
/// the review runs on), a handful of the base game's weapons built as the game builds them from their live defs rating
/// exactly as docs/weapons/weapons.json has them; the weapon in hand (quality, unique and persona traits, biocoding); the
/// rating for a colonist and why it differs; Simple Sidearms' pair, Dual Wield's off hand, RunAndGun on the move; the
/// card's chart and colours; and the seams (the settings, the keys, the hooks, the card's frame, the Almanac page).
/// </summary>
public static class WeaponLiveTests
{
    // ---------------------------------------------------------------- the base game's defs, as the game reads them

    static readonly Dictionary<string, WeaponDamageFacts> Damages = new()
    {
        ["Bullet"] = Dmg("Bullet", "bullet", "Sharp", "DamageWorker_AddInjury"),
        ["Bomb"] = Dmg("Bomb", "bomb", "Sharp", "DamageWorker_AddInjury", 50f, 0.1f, 0.5f),
        ["Blunt"] = Dmg("Blunt", "blunt", "Blunt", "DamageWorker_Blunt"),
        ["Poke"] = Dmg("Poke", "blunt", "Blunt", "DamageWorker_Stab"),
        ["Stab"] = Dmg("Stab", "stab", "Sharp", "DamageWorker_Stab"),
        ["Cut"] = Dmg("Cut", "cut", "Sharp", "DamageWorker_Cut"),
        ["EMP"] = new WeaponDamageFacts { Name = "EMP", Label = "EMP", ArmourCategory = "", Worker = "", DefaultDamage = 50f, DefaultArmourPen = -1f, DefaultStoppingPower = 0f, HarmsHealth = false },
        ["Flame"] = Dmg("Flame", "flame", "Heat", "DamageWorker_Flame"),
    };

    static WeaponDamageFacts Dmg(string name, string label, string cat, string worker, float dd = -1f, float dap = -1f, float dsp = 0f) =>
        new() { Name = name, Label = label, ArmourCategory = cat, Worker = worker, DefaultDamage = dd, DefaultArmourPen = dap, DefaultStoppingPower = dsp, HarmsHealth = true };

    static WeaponDamageFacts DamageOf(string n) => n != null && Damages.TryGetValue(n, out var d) ? d : null;

    static List<WeaponPartFacts> Comps(params string[] classes) => classes.Select(c => new WeaponPartFacts { Class = c }).ToList();

    static readonly string[] GunComps = { "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompProperties_Art", "CompQuality", "CompProperties_Biocodable" };

    static WeaponToolFacts Tool(string label, float power, float cd, params (string cap, string dmg)[] caps) => new()
    {
        Label = label, Power = power, Cooldown = cd,
        Capacities = caps.Select(c => new WeaponCapacityFacts { Capacity = c.cap, DamageDef = c.dmg, VerbClass = "Verb_MeleeAttackDamage" }).ToList()
    };

    static List<WeaponToolFacts> GunTools() => new() { Tool("stock", 9, 2, ("Blunt", "Blunt")), Tool("barrel", 9, 2, ("Blunt", "Blunt"), ("Poke", "Poke")) };

    /// <summary>A gun as WeaponLive fills it at normal quality: its accuracy and cooldown stat values, the multipliers at one.</summary>
    static WeaponDefFacts Gun(string def, string label, float mass, float t, float s, float m, float l, float cd, WeaponVerbFacts verb, string[] tags,
        string research, string tech, string[] comps = null)
    {
        var f = new WeaponDefFacts
        {
            DefName = def, Label = label, Mod = "ludeon.rimworld", ModName = "Core", Tech = tech, Mass = mass, DamageOf = DamageOf,
            Verbs = { verb }, Tools = verb.VerbClass == "Verb_Shoot" ? GunTools() : new List<WeaponToolFacts>(), WeaponTags = tags.ToList(),
            Research = research == null ? new List<string>() : new List<string> { research }, ResearchTech = research == null ? "" : tech,
            Comps = Comps(comps ?? GunComps), CompClasses = (comps ?? GunComps).ToList()
        };
        f.Stats["AccuracyTouch"] = t;
        f.Stats["AccuracyShort"] = s;
        f.Stats["AccuracyMedium"] = m;
        f.Stats["AccuracyLong"] = l;
        f.Stats["RangedWeapon_Cooldown"] = cd;
        foreach (var k in new[] { "RangedWeapon_WarmupMultiplier", "RangedWeapon_RangeMultiplier", "RangedWeapon_DamageMultiplier", "RangedWeapon_ArmorPenetrationMultiplier" })
            f.Stats[k] = 1f;
        return f;
    }

    static WeaponVerbFacts Shoot(float range, float warmup, int burst, int ticks, string proj, float dmg, float ap, float stop, string cls = "Verb_Shoot",
        float forcedMiss = 0f, string damageDef = "Bullet", string thingClass = "Bullet", float radius = 0f, float delay = 0f) => new()
    {
        VerbClass = cls, Range = range, Warmup = warmup, Burst = burst, TicksBetween = ticks, ForcedMiss = forcedMiss,
        Projectile = new WeaponProjectileFacts
        {
            DefName = proj, ThingClass = thingClass, DamageDef = damageDef, Damage = dmg, ArmourPen = ap, StoppingPower = stop,
            ExplosionRadius = radius, ExplosionDelayTicks = delay
        }
    };

    /// <summary>A melee weapon as WeaponLive fills it at normal quality: steel's factors where made of stuff.</summary>
    static WeaponDefFacts Melee(string def, string label, float mass, bool steel, string tech, string[] tags, bool made, params WeaponToolFacts[] tools)
    {
        var comps = steel
            ? new[] { "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompQuality", "CompProperties_Art" }
            : new[] { "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompQuality", "CompProperties_Art", "CompProperties_Biocodable" };
        return new WeaponDefFacts
        {
            DefName = def, Label = label, Mod = "ludeon.rimworld", ModName = "Core", Tech = tech, Mass = mass, DamageOf = DamageOf,
            Stuffed = steel, StuffLabel = steel ? "steel" : null, Tools = tools.ToList(), WeaponTags = tags.ToList(), Made = made,
            Comps = Comps(comps), CompClasses = comps.ToList()
        };
    }

    public static List<WeaponDefFacts> Vanilla()
    {
        var zeus = Tool("head", 31, 3, ("Blunt", "Blunt"));
        zeus.Extras.Add(new WeaponExtraFacts { Def = "EMP", Amount = 9f });
        var frag = Gun("Weapon_GrenadeFrag", "frag grenades", 1f, 1f, 1f, 1f, 1f, 2.66f,
            Shoot(12.9f, 1.5f, 1, 15, "Proj_GrenadeFrag", 50f, 0.1f, 0.5f, "Verb_LaunchProjectile", 1.9f, "Bomb", "Projectile_Explosive", 1.9f, 100f),
            new[] { "GrenadeDestructive", "EmpireGrenadeDestructive" }, "Machining", "Industrial",
            new[] { "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompProperties_Explosive" });
        return new List<WeaponDefFacts>
        {
            Gun("Gun_AssaultRifle", "assault rifle", 3.5f, .6f, .7f, .65f, .55f, 1.7f, Shoot(30.9f, 1f, 3, 10, "Bullet_AssaultRifle", 11f, 0.165f, 0.5f),
                new[] { "IndustrialGunAdvanced", "AssaultRifle" }, "PrecisionRifling", "Industrial"),
            Gun("Gun_SniperRifle", "sniper rifle", 4f, .5f, .7f, .88f, .9f, 1.5f, Shoot(44.9f, 3.5f, 1, 15, "Bullet_SniperRifle", 25f, 0.375f, 1.5f),
                new[] { "SniperRifle" }, "PrecisionRifling", "Industrial"),
            Gun("Gun_ChainShotgun", "chain shotgun", 4.5f, .57f, .64f, .55f, .45f, 1.35f, Shoot(12.9f, 1.2f, 3, 10, "Bullet_Shotgun", 18f, 0.14f, 3f),
                new[] { "Gun", "IndustrialGunAdvanced", "ShortShots" }, "GasOperation", "Industrial"),
            Gun("Gun_ChargeRifle", "charge rifle", 4.6f, .55f, .64f, .55f, .45f, 2f, Shoot(27.9f, 1f, 3, 12, "Bullet_ChargeRifle", 16f, 0.35f, 0.5f),
                new[] { "Gun", "SpacerGun" }, "ChargedShot", "Spacer"),
            frag,
            Melee("MeleeWeapon_Knife", "knife", 0.5f, true, "Medieval", new[] { "MedievalMeleeBasic", "NeolithicMeleeBasic" }, true,
                Tool("handle", 9, 2, ("Blunt", "Blunt")), Tool("blade", 12, 1.5f, ("Cut", "Cut")), Tool("point", 13, 2, ("Stab", "Stab"))),
            Melee("MeleeWeapon_LongSword", "longsword", 2f, true, "Medieval", new[] { "MedievalMeleeAdvanced", "LongSword" }, true,
                Tool("handle", 9, 2, ("Blunt", "Blunt")), Tool("point", 23, 2.6f, ("Stab", "Stab")), Tool("edge", 23, 2.6f, ("Cut", "Cut"))),
            Melee("MeleeWeapon_Zeushammer", "zeushammer", 2f, false, "Ultra", new[] { "UltratechMelee" }, false,
                Tool("handle", 15, 2, ("Poke", "Poke")), zeus),
        };
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "weapon card: " + what);
        }
        bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The game's reading gives the review's ratings ----------
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "weapons", "weapons.json")));
        var review = ((JsonArray)json["weapons"]).ToDictionary(w => w["defName"].ToString(), w => w);
        var reports = new Dictionary<string, WeaponReport>();
        foreach (var f in Vanilla())
        {
            var r = WeaponRead.Full(f);
            reports[f.DefName] = r;
            C(review.TryGetValue(f.DefName, out var j), $"{f.Label} is in weapons.json");
            if (j == null) continue;
            C(r.Rated, $"{f.Label} is rated");
            if (!r.Rated) continue;
            C(Near(r.Rating.Overall, (float)j["rating"]), $"{f.Label}: the game's reading rates it {r.Rating.Overall}, as the review ({j["rating"]})");
            C(r.Rating.Role == j["role"].ToString(), $"{f.Label}: role {r.Rating.Role}, as the review");
            foreach (var kv in (JsonObject)j["sub"])
                C(r.Rating.Sub.TryGetValue(kv.Key, out var v) && Near(v, (float)kv.Value), $"{f.Label}: sub-rating {kv.Key} as the review ({kv.Value})");
            foreach (var kv in (JsonObject)j["situations"])
                C(r.Rating.Situations.TryGetValue(kv.Key, out var v) && Near(v, (float)kv.Value, 0.01f), $"{f.Label}: situation {kv.Key} as the review");
            var tags = ((JsonArray)j["tags"]).Select(t => t["id"].ToString()).ToList();
            C(r.Tags.Select(t => t.Id).SequenceEqual(tags), $"{f.Label}: tags {string.Join(",", r.Tags.Select(t => t.Id))} as the review ({string.Join(",", tags)})");
            var whys = ((JsonArray)j["tags"]).Select(t => t["why"].ToString()).ToList();
            C(r.Tags.Select(t => t.Why).SequenceEqual(whys), $"{f.Label}: each tag's why as the review");
            C(r.Plain == j["plain"].ToString(), $"{f.Label}: the plain line as the review ({r.Plain})");
            C(r.Table == j["table"].ToString(), $"{f.Label}: the review's table");
            C(r.Unsure.Count == ((JsonArray)j["unsure"]).Count, $"{f.Label}: rough or not as the review");
            var specials = ((JsonArray)j["specials"]).Select(s => s["key"].ToString()).ToList();
            C(r.Rating.Specials.Select(s => s.Key).SequenceEqual(specials), $"{f.Label}: specials as the review");
            foreach (var kv in (JsonObject)j["ratingBySkill"])
                C(r.BySkill.TryGetValue(kv.Key, out var v) && Near(v, (float)kv.Value), $"{f.Label}: at skill {kv.Key} as the review");
        }

        // ---------- The weapon in hand ----------
        var sword = Vanilla().First(f => f.DefName == "MeleeWeapon_LongSword");
        sword.MeleeDamageFactor = 1.45f; // legendary
        var legendary = WeaponRead.Full(sword);
        C(legendary.Rating.Overall > reports["MeleeWeapon_LongSword"].Rating.Overall + 0.5f, $"a legendary longsword rates well above a normal one ({legendary.Rating.Overall})");
        var plasteel = Vanilla().First(f => f.DefName == "MeleeWeapon_LongSword");
        plasteel.SharpFactor = 1.1f;
        plasteel.BluntFactor = 0.9f;
        plasteel.MeleeCooldownFactor = 0.8f;
        plasteel.StuffLabel = "plasteel";
        C(Near(WeaponRead.Full(plasteel).Rating.Overall, WeaponAnchors.RateMelee("plasteel longsword").Overall), "a plasteel longsword read from its stuff rates as the anchor");
        var awfulRifle = Vanilla().First(f => f.DefName == "Gun_AssaultRifle");
        foreach (var k in new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" }) awfulRifle.Stats[k] *= 0.8f;
        awfulRifle.Stats["RangedWeapon_DamageMultiplier"] = 0.9f;
        C(WeaponRead.Full(awfulRifle).Rating.Overall < reports["Gun_AssaultRifle"].Rating.Overall - 0.3f, "an awful assault rifle (its quality's accuracy and damage) rates lower");

        var unique = Vanilla().First(f => f.DefName == "Gun_ChargeRifle");
        unique.Comps.Add(new WeaponPartFacts { Class = "CompProperties_UniqueWeapon" });
        unique.CompClasses.Add("CompProperties_UniqueWeapon");
        var plainUnique = WeaponRead.Full(unique);
        C(plainUnique.Rough && plainUnique.Unsure.Any(u => u.StartsWith("a unique weapon")), "a unique weapon not in hand is rated as the plain weapon, marked rough");
        unique.UniqueTraits = new List<WeaponTraitFacts>
        {
            new() { Label = "rapid", BurstCountFactor = 1.5f, BurstSpeedFactor = 1.25f },
            new() { Label = "hard-hitting", StoppingPower = 1f, StatWords = { "damage +25%" } }
        };
        unique.Stats["RangedWeapon_DamageMultiplier"] = 1.25f;
        var inHand = WeaponRead.Full(unique);
        C(inHand.Ranged.Burst == 5 && inHand.Ranged.TicksBetweenBurst == 10, $"its traits' burst: 3 shots x1.5 = 5, 12 ticks / 1.25 = 10 ({inHand.Ranged.Burst}, {inHand.Ranged.TicksBetweenBurst})");
        C(Near(inHand.Ranged.StoppingPower, 1.5f) && Near(inHand.Ranged.Damage, 20f), "its traits' stopping power and damage (16 x1.25 = 20)");
        C(inHand.Rating.Overall > plainUnique.Rating.Overall, $"its traits lift it ({plainUnique.Rating.Overall} to {inHand.Rating.Overall})");
        C(!inHand.Rough && inHand.Rating.Specials.Any(s => s.Key == "unique" && s.Counted && s.Label.Contains("rapid")), "in hand its traits are named and counted, not rough");
        C(inHand.Tags.Any(t => t.Id == "unique" && t.Why.Contains("rapid")) || !inHand.Tags.Any(t => t.Id == "unique"), "the unique tag names its traits");
        C(!inHand.Plain.Contains("rolled traits"), "the line doesn't say its traits can lift or sink it once they are known");
        var empTrait = Vanilla().First(f => f.DefName == "Gun_AssaultRifle");
        empTrait.Comps.Add(new WeaponPartFacts { Class = "CompProperties_UniqueWeapon" });
        empTrait.UniqueTraits = new List<WeaponTraitFacts> { new() { Label = "EMP rounds", DamageOverride = "EMP" } };
        var emp = WeaponRead.Full(empTrait);
        C(emp.Ranged.Payloads.Any(p => p.Effect == WeaponEffect.Emp) && !emp.Ranged.HarmsHealth, "a trait that turns its shots to EMP makes it an EMP gun");

        var persona = Vanilla().First(f => f.DefName == "MeleeWeapon_Zeushammer");
        persona.Comps.Add(new WeaponPartFacts { Class = "CompProperties_BladelinkWeapon" });
        var unbonded = WeaponRead.Full(persona);
        persona.PersonaTraits = new List<WeaponTraitFacts>
        {
            new() { Label = "fast", Equipped = { new WeaponPassive("MoveSpeed", 0.5f) } },
            new() { Label = "kill thirst", Moods = true }
        };
        persona.BiocodedTo = "Ana";
        var bonded = WeaponRead.Full(persona);
        C(bonded.Passives.Any(p => p.Stat == "MoveSpeed") && bonded.Rating.Overall > unbonded.Rating.Overall, "a persona trait that quickens its wielder counts");
        C(bonded.Rating.Specials.Any(s => s.Key == "persona" && s.Label.Contains("bonded to Ana") && s.Label.Contains("kill thirst")), "the persona special names the bond and the traits");
        var coded = Vanilla().First(f => f.DefName == "Gun_SniperRifle");
        coded.BiocodedTo = "Ben";
        var codedR = WeaponRead.Full(coded);
        C(codedR.Rating.Specials.Any(s => s.Key == "biocodable" && s.Label.Contains("Ben")) && Near(codedR.Rating.Overall, reports["Gun_SniperRifle"].Rating.Overall),
            "biocoding names whom it is coded to and doesn't change the rating");

        // ---------- The game's curves backwards ----------
        foreach (float s in new[] { 0f, 4f, 8f, 13f, 20f })
        {
            C(Near(WeaponRateMath.ShootingLevelFor(WeaponRateMath.ShootingAccuracy(s)), s, 0.01f), $"shooting accuracy at {s} read back as {s}");
            C(Near(WeaponRateMath.MeleeLevelFor(WeaponRateMath.MeleeHitChance(s)), s, 0.01f), $"melee hit at {s} read back as {s}");
        }
        C(Near(WeaponRateMath.ShootingLevelFor(0.5f), -20f) && Near(WeaponRateMath.ShootingLevelFor(1f), 60f), "past the curve's ends, its ends");

        // ---------- For a colonist ----------
        var rifle = reports["Gun_AssaultRifle"];
        var plainColonist = new FighterFacts { ShootingLevel = 8, MeleeLevel = 8 };
        var same = WeaponForMath.For(rifle, plainColonist);
        C(Near(same.Rating, rifle.Rating.Overall), $"a colonist of skill 8 with no traits rates it as the reference ({same.Rating})");
        C(WeaponForMath.Telling(same).Count == 0, "and nothing to explain");
        var careful = new FighterFacts
        {
            ShootingLevel = 8, Traits = { new FighterTrait { Label = "careful shooter", Shooting = 5f, AimFactor = 1.25f } },
            ShootingAccuracy = WeaponRateMath.ShootingAccuracy(13f), AimFactor = 1.25f
        };
        var cf = WeaponForMath.For(rifle, careful);
        C(Near(cf.Rating, rifle.ByTrait["careful shooter"]), $"a careful shooter rates it as the review's careful shooter ({cf.Rating}, {rifle.ByTrait["careful shooter"]})");
        var cfSteps = WeaponForMath.Telling(cf);
        C(cfSteps.Any(s => s.why == "careful shooter"), "the careful shooter's step is named");
        C(Near(cf.Why.Sum(s => s.delta), cf.Rating - cf.Reference, 0.01f), "the steps add up to the difference");
        var sniper = reports["Gun_SniperRifle"];
        var happy = new FighterFacts
        {
            ShootingLevel = 12, Traits = { new FighterTrait { Label = "trigger-happy", Shooting = -5f, AimFactor = 0.5f } },
            ShootingAccuracy = WeaponRateMath.ShootingAccuracy(7f), AimFactor = 0.5f
        };
        var hf = WeaponForMath.For(sniper, happy);
        C(hf.Why[0].delta > 0f && hf.Why.First(s => s.why == "trigger-happy").delta != 0f, "skill 12 lifts the sniper, trigger-happy moves it");
        C(Near(hf.Why.Sum(s => s.delta), hf.Rating - hf.Reference, 0.01f), "a trigger-happy colonist's steps add up too");
        var held = new FighterFacts { ShootingLevel = 8, ShootingAccuracy = WeaponRateMath.ShootingAccuracy(11f), HeldShooting = 3f };
        C(Near(WeaponForMath.For(rifle, held).Rating, rifle.Rating.Overall), "what the weapon in hand adds to accuracy is taken out when judging another");
        var blind = new FighterFacts { ShootingLevel = 8, ShootingAccuracy = WeaponRateMath.ShootingAccuracy(2f) };
        var bf = WeaponForMath.For(rifle, blind);
        C(bf.Rating < rifle.Rating.Overall && WeaponForMath.Telling(bf).Any(s => s.why == "body and gear" && s.delta < 0f), "poor sight shows as body and gear");
        var knife = reports["MeleeWeapon_Knife"];
        var brawler = new FighterFacts { MeleeLevel = 8, Traits = { new FighterTrait { Label = "brawler", MeleeHit = 4f } }, MeleeHitChance = WeaponRateMath.MeleeHitChance(12f), Brawler = true };
        var brf = WeaponForMath.For(knife, brawler);
        C(Near(brf.Rating, knife.ByTrait["brawler"]) && WeaponForMath.Telling(brf).Any(s => s.why == "brawler" && s.delta > 0f), $"a brawler rates a knife as the review's brawler ({brf.Rating})");
        C(WeaponForMath.Delta(0.4f) == "+0.4" && WeaponForMath.Delta(-1.25f) == "-1.2" || WeaponForMath.Delta(-1.25f) == "-1.3", "a step's change in words");
        C(WeaponForMath.Delta(0.01f) == "0", "a step of nothing says 0");

        // ---------- Simple Sidearms ----------
        var carried = new List<CarriedWeapon>
        {
            new() { Key = "rifle", Ranged = true, Primary = true, Mass = 3.5f, Rating = 5.8f },
            new() { Key = "pistol", Ranged = true, Mass = 1.2f, Rating = 4.7f },
            new() { Key = "lmg", Ranged = true, Mass = 9f, Rating = 6.5f },
            new() { Key = "knife", Mass = 0.5f, Rating = 4.0f },
            new() { Key = "longsword", Mass = 2f, Rating = 5.2f },
            new() { Key = "rockets", Ranged = true, Mass = 1f, Rating = 7.0f, OneUse = true },
        };
        var pair = WeaponForMath.BestPair(carried);
        C(pair.ranged?.Key == "rifle", "the pair's gun: the rifle in hand, not a sidearm over Simple Sidearms' 2.7 kg nor a one-use launcher");
        C(pair.melee?.Key == "knife", "the pair's blade: the knife, the longsword being over its 1.9 kg");
        C(!WeaponForMath.Fits(carried[2]) && WeaponForMath.Fits(carried[0]) && WeaponForMath.Fits(carried[1]), "Simple Sidearms' default weights: what fits");
        var none = WeaponForMath.BestPair(new List<CarriedWeapon> { carried[0] });
        C(none.melee == null && none.ranged != null, "no blade carried: no melee weapon in the pair");

        // ---------- Dual Wield ----------
        var dw = WeaponForMath.DualWieldDefaults;
        C(Near(WeaponForMath.DualCooldownFactor(dw, true, 8), 1.8f) && Near(WeaponForMath.DualCooldownFactor(dw, false, 8), 1.7f),
            "Dual Wield's cooldowns at skill 8: the off hand 80% longer, the main hand 70%");
        C(Near(WeaponForMath.DualAccuracyFactor(dw, true, 8), 0.84f) && Near(WeaponForMath.DualAccuracyFactor(dw, true, 20), 0.9f),
            "Dual Wield's accuracy: 16% worse at skill 8, 10% at 20");
        var main = WeaponForMath.RateInHand(rifle, WeaponRateMath.Reference, dw, false, 8);
        var off = WeaponForMath.RateInHand(reports["Gun_ChargeRifle"], WeaponRateMath.Reference, dw, true, 8);
        C(main.Overall < rifle.Rating.Overall && off.Overall < reports["Gun_ChargeRifle"].Rating.Overall, "each hand rates lower with Dual Wield's penalties");
        var together = WeaponForMath.PairTogether(main, off);
        C(together.HasValue && together.Value > main.Overall && together.Value > off.Overall, $"two guns together get more done than either ({together})");
        C(WeaponForMath.PairTogether(main, WeaponForMath.RateInHand(knife, WeaponRateMath.Reference, dw, true, 8)) == null, "a gun and a blade aren't added: each fights its own fight");
        var knifeOff = WeaponForMath.RateInHand(knife, WeaponRateMath.Reference, dw, true, 8);
        C(knifeOff.Overall < knife.Rating.Overall, "a knife in the off hand swings slower");

        // ---------- RunAndGun ----------
        C(Near(WeaponForMath.MovingAccuracyFactor(10), 0.9f) && Near(WeaponForMath.MovingAccuracyFactor(150), 0.01f), "RunAndGun's 10% accuracy penalty on the move");
        var moving = WeaponForMath.RateFor(rifle, WeaponRateMath.Reference, WeaponForMath.WithAccuracy(rifle.Ranged, 0.9f));
        C(moving.Overall < rifle.Rating.Overall, "shooting on the move rates lower");

        // ---------- The card's chart and colours ----------
        var ds = WeaponForMath.ChartDistances(rifle.Ranged);
        C(ds.First() >= 1f && ds.Last() <= 31f && ds.Last() >= 30f && ds.SequenceEqual(ds.OrderBy(x => x)), $"the chart runs up to the rifle's reach ({string.Join(",", ds)})");
        var chart = WeaponForMath.RangedChart(rifle.Ranged, WeaponRateMath.Reference, ds);
        C(chart.Length == 4 && chart.All(row => row.Length == ds.Count), "four lines: no armour, flak, marine, mechanoids");
        C(Enumerable.Range(0, ds.Count).All(i => chart[0][i] >= chart[2][i]), "marine armour never takes more than no armour");
        var mchart = WeaponForMath.MeleeChart(knife.Melee, WeaponRateMath.Reference);
        C(mchart.Length == 4 && mchart[0] > mchart[2], "a knife's bars: less against marine armour");
        C(WeaponForMath.NiceTop(7.3f) == 10f && WeaponForMath.NiceTop(1.6f) == 2f && WeaponForMath.NiceTop(22f) == 25f, "the chart's scale tops are round");
        C(WeaponForMath.RatingColour(2f) == SkinMath.Danger && WeaponForMath.RatingColour(5.8f) == SkinMath.Done && WeaponForMath.RatingColour(8f) == SkinMath.World,
            "ratings are coloured by their band");
        C(WeaponForMath.RatingText(5.75f) == "5.8" || WeaponForMath.RatingText(5.75f) == "5.7", "a rating shows one decimal");
        C(WeaponForMath.SubKeys(rifle.Rating).Count() == 9 && WeaponForMath.SubKeys(knife.Rating).Count() == 4, "a gun's nine sub-ratings, a blade's four");
        C(WeaponForMath.TagLine(rifle.Tags[0]).StartsWith(WeaponTagMath.Style(rifle.Tags[0].Category).Symbol + " "), "a tag's line starts with its symbol");

        // ---------- The seams ----------
        string live = Read("Source/ColonyCouncil/Weapons/WeaponLive.cs");
        string card = Read("Source/ColonyCouncil/Weapons/WeaponCardWindow.cs");
        string info = Read("Source/ColonyCouncil/Weapons/WeaponInfoCard.cs");
        string gear = Read("Source/ColonyCouncil/Weapons/WeaponGearTab.cs");
        string mods = Read("Source/ColonyCouncil/Weapons/WeaponMods.cs");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_Weapons.xml");
        C(live.Contains("WeaponRead.Full(") && live.Contains("CompUniqueWeapon") && live.Contains("CompBladelinkWeapon") && live.Contains("CompBiocodable"),
            "the game reads the weapon in hand through the shared reading: unique and persona traits, biocoding");
        C(live.Contains("StatRequest.For(") && live.Contains("RangedWeapon_DamageMultiplier"), "a def's rating takes its stuff and quality from the game's own stats");
        foreach (var (field, label) in new[] { ("weaponRatings", "WeaponRatings"), ("weaponGearTab", "WeaponGearTab"), ("weaponCard", "WeaponCard") })
        {
            C(settings.Contains("public bool " + field + " = true;") && settings.Contains("Scribe_Values.Look(ref " + field + ", \"" + field + "\", true);"),
                $"{field}: a setting, on at first, saved");
            C(catalog.Contains("Toggle(\"" + field + "\", \"CC_Settings_" + label + "\""), $"{field}: on the settings page");
            C(keys.Contains("<CC_Settings_" + label + ">") && keys.Contains("<CC_Settings_" + label + "_Tip>") && keys.Contains("<CC_SetNote_" + label + ">"),
                $"{field}: its label, tip and note");
        }
        C(info.Contains("[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]") && info.Contains("[HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]"),
            "the info card's entries and its button are hooks of their own");
        C(Read("Defs/Misc/Rimfolk_WeaponStats.xml").Contains("<displayOrder>0</displayOrder>"), "the rating's category is sorted first in the info card");
        C(gear.Contains("[HarmonyPatch(typeof(ITab_Pawn_Gear), \"DrawThingRow\")]") && gear.Contains("NiceInventoryTab.EquippedItem") && gear.Contains("NiceInventoryTab.InventoryItem"),
            "the badge on the game's gear tab and on Nice Inventory Tab's rows");
        foreach (var soft in new[] { "Patch_WeaponNiceInventoryRow", "Patch_WeaponNiceInventoryCell" })
        {
            int at = gear.IndexOf("class " + soft, StringComparison.Ordinal);
            string body = at >= 0 ? gear.Substring(at, Math.Min(1600, gear.Length - at)) : "";
            C(body.Contains("Prepare()") && body.Contains("TargetMethod()") && body.Contains("StandsDownWhy()"), $"{soft}: skipped cleanly without Nice Inventory Tab");
        }
        C(mods.Contains("SimpleSidearms.rimworld.CompSidearmMemory") && mods.Contains("DualWield.Ext_Pawn_EquipmentTracker") && mods.Contains("RunAndGun.RunAndGun")
          && mods.Contains("AM.AnimDef"), "the weapon mods are read by name: Simple Sidearms, Dual Wield, RunAndGun, Melee Animation");
        C(!mods.Contains("using SimpleSidearms") && !mods.Contains("using DualWield") && !mods.Contains("using RunAndGun"), "no hard reference to the weapon mods");
        C(card.Contains(": FramedWindow") && card.Contains("base(\"WeaponCard\")"), "Rimfolk's weapon card is a framed window");
        C(FramedWindowsMath.TryKey("WeaponCard", out var wk) && wk.leastW >= 420f && wk.leastH >= 420f, "the card's frame key and its least size");
        C(Read("Source/ColonyCouncil/Weapons/WeaponGizmo.cs").Contains("nameof(ThingWithComps.GetGizmos)"), "a button on a weapon on the map");
        var almanac = Read("Defs/AlmanacArticleDefs/Almanac_Weapons.xml");
        C(almanac.Contains("<label>judging a weapon</label>") && !almanac.Contains("—"), "the Almanac's page, judging a weapon");
        C(!keys.Contains("—") && !keys.Contains("!</"), "the keys: no em dashes, no exclamation marks");
        return n;
    }
}
