using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>
/// One weapon of the extracted collection (Tools/weapon_extract.py) turned into the weapon rating's inputs, with what the
/// review can only name (a mod's own verb, projectile, damage or comp) and the reasons a number is less sure.
/// </summary>
public sealed class Reviewed
{
    public JsonNode W;
    public string DefName, Label, Mod, ModName, Tech;
    public float Mass, Market;
    public string Kind;      // ranged, melee, left out
    public string Table;     // which table of the README
    public WeaponRangedInput Ranged;
    /// <summary>Its other firing modes (a gauss rifle's high-power shot, a multi-verb weapon's second verb): rated too, the best kept.</summary>
    public List<WeaponRangedInput> Alternates = new();
    public WeaponMeleeInput Melee;
    public List<WeaponPassive> Passives = new();
    public List<WeaponAbilityInput> Abilities = new();
    public List<WeaponSpecial> Named = new();
    public List<string> Unsure = new();
    public WeaponRating Rating;
    public WeaponRating MeleeRating;
    public string Plain;
    public string StuffLabel;
    /// <summary>Why it isn't rated though a colonist can wield it (its attack is wholly a mod's own code).</summary>
    public string Unrated;
    public Dictionary<string, float> BySkill = new();
    public Dictionary<string, float> ByTrait = new();
    public List<ThingTag> Tags = new();
    public WeaponTagFacts Facts = new();
}

public static class Inputs
{
    public static JsonObject DamageDefs;

    /// <summary>
    /// Verbs that shoot as the game's own do, the game's and the ones the mods' code was read for (Vanilla Expanded
    /// Framework's shot and makeshift shot, VFE Medieval's matchlock shot, A RimWorld of Magic's mage-only shot, the
    /// Fairycore wands' channelled shot, VWE Bioferrite's heavy incinerator and ability shots).
    /// </summary>
    static readonly HashSet<string> KnownVerbs = new()
    {
        "Verb_Shoot", "Verb_ShootOneUse", "Verb_LaunchProjectile", "Verb_ShootBeam", "Verb_ArcSprayProjectile",
        "Verb_ArcSprayIncinerator", "Verb_SpewFire", "Verb_shoot", "Verb_LaunchProjectileStatic", "Verb_CastAbility",
        "VEF.Weapons.Verb_Shoot", "VEF.Weapons.Verb_MakeshiftShoot", "VFEMedieval.Verb_ShootWithSmoke",
        "TorannMagic.Weapon.Verb_BlazingPower", "FairycorePixieWands.Verb_ShootWand",
        "VanillaWeaponsExpanded_Bioferrite.Verb_ArcSprayIncineratorHeavy", "VanillaWeaponsExpanded_Bioferrite.Verb_CastAbility_Shoot",
        "VanillaWeaponsExpanded_Bioferrite.Verb_CastAbility_ShootStaticPsychic",
        "VanillaWeaponsExpanded_Bioferrite.Verb_CastAbility_ArcSprayIncinerator",
        "VanillaWeaponsExpanded_Bioferrite.Verb_CastAbility_ArcSprayIncineratorHeavy"
    };

    /// <summary>Projectiles that fly and strike as a bullet or a shell does (their own code only draws them differently, or is read below).</summary>
    static readonly HashSet<string> KnownProjectiles = new()
    {
        "Bullet", "Projectile_Explosive", "Beam", "Projectile_Liquid", "Projectile_DoomsdayRocket", "Projectile_SpawnsThing",
        "VEF.Weapons.LaserBeam", "FireArrow", "VFETribals.ThrowspikesThrown", "VFEMedieval.WarbowArrow", "VFEMedieval.MatchlockProjectile",
        "VFE_Settlers.Comps.DynamiteComp", "TorannMagic.Weapon.Projectile_BlazingPower", "TorannMagic.Weapon.Projectile_FireWand",
        "TorannMagic.Weapon.Projectile_IceWand", "TorannMagic.Weapon.Projectile_LightningWand", "FairycorePixieWands.Projectile_WandOrb",
        "VanillaQuestsExpandedCryptoforge.Bullet_Cryptobolter", "VEF.Weapons.Projectile_SmokeGrenade", "VEF.Weapons.Projectile_TopAttackMissile"
    };

    /// <summary>Verbs whose attack is wholly their own code (what they launch and how often): not rated, named in the appendix.</summary>
    static readonly HashSet<string> OwnAttackVerbs = new() { "TorannMagic.Weapon.Verb_FreezingWinds" };

    static readonly HashSet<string> VanillaWorkers = new()
    {
        "", "DamageWorker_AddInjury", "DamageWorker_Cut", "DamageWorker_Stab", "DamageWorker_Blunt", "DamageWorker_Bite",
        "DamageWorker_Scratch", "DamageWorker_Flame", "DamageWorker_Stun", "DamageWorker_Extinguish", "DamageWorker_Vaporize",
        "DamageWorker_Nerve"
    };

    /// <summary>Comps that only change looks, names, quality or handling: nothing to say about a fight.</summary>
    static readonly HashSet<string> QuietComps = new()
    {
        "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompProperties_Art", "CompQuality",
        "CompProperties_GeneratedName", "CompOversizedWeapon", "CompProperties_GraphicCustomization",
        "CompGraphicCustomization_PsychicWeapon", "CompProperties_EditHeraldic", "CompProperties_Biocodable",
        "CompProperties_BladelinkWeapon", "CompProperties_UniqueWeapon", "CompProperties_EquippableAbilityReloadable",
        "CompProperties_EquippableAbility", "CompProperties_Explosive", "CompProperties_WeaponDeteriorable",
        "CompProperties_LaserCapacitor", "CompProperties_MultiVerbWeapon", "CompProperties_ChangeableProjectile",
        "CompProperties_EnchantedItem", "CompProperties_Deflector", "CompBPReflector", "CompEquippable_Wand"
    };

    public static float F(JsonNode n, string key, float def = 0f)
    {
        var v = n?[key];
        if (v == null) return def;
        try { return v.GetValue<float>(); }
        catch
        {
            try { return (float)v.GetValue<double>(); }
            catch { return float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def; }
        }
    }

    public static string S(JsonNode n, string key) => n?[key]?.ToString() ?? "";

    public static bool B(JsonNode n, string key) => n?[key] != null && n[key].ToString().ToLowerInvariant() == "true";

    public static IEnumerable<JsonNode> Arr(JsonNode n, string key) => (n?[key] as JsonArray)?.Where(x => x != null) ?? Enumerable.Empty<JsonNode>();

    public static JsonNode Dmg(string name) => string.IsNullOrEmpty(name) ? null : DamageDefs?[name];

    public static WeaponArmour ArmourOf(string damageDef)
    {
        var d = Dmg(damageDef);
        switch (S(d, "armorCategory"))
        {
            case "Sharp": return WeaponArmour.Sharp;
            case "Blunt": return WeaponArmour.Blunt;
            case "Heat": return WeaponArmour.Heat;
            default: return WeaponArmour.None;
        }
    }

    static string DamageWords(string name)
    {
        var d = Dmg(name);
        string l = S(d, "label");
        return string.IsNullOrEmpty(l) ? name : l;
    }

    public static Reviewed Read(JsonNode w)
    {
        var r = new Reviewed
        {
            W = w, DefName = S(w, "defName"), Label = S(w, "label"), Mod = S(w, "mod"), ModName = S(w, "modName"), Tech = S(w, "techLevel"),
            Mass = F(w["statBases"], "Mass", 1f), Market = F(w["statBases"], "MarketValue", 0f)
        };
        if (!string.IsNullOrEmpty(S(w, "leftOut")))
        {
            r.Kind = "left out";
            return r;
        }
        foreach (var kv in (w["equippedStatOffsets"] as JsonObject) ?? new JsonObject())
            r.Passives.Add(new WeaponPassive(kv.Key, F(w["equippedStatOffsets"], kv.Key)));
        var verb = Arr(w, "verbs").FirstOrDefault(v => v["projectile"] != null || !string.IsNullOrEmpty(S(v, "beamDamageDef")));
        bool nonLethal = r.Mod == "vanillaexpanded.vwenl";
        if (verb != null)
        {
            r.Kind = "ranged";
            r.Ranged = RangedFrom(w, verb, r, nonLethal);
            foreach (var other in Arr(w, "verbs").Where(v => v != verb && (v["projectile"] != null || !string.IsNullOrEmpty(S(v, "beamDamageDef")))))
                r.Alternates.Add(RangedFrom(w, other, new Reviewed { Mod = r.Mod }, nonLethal));
            if (r.Alternates.Count > 0)
                r.Named.Add(Special("modes", "more than one way to fire: rated in its best", true, 0));
        }
        else if (Arr(w, "tools").Any())
        {
            r.Kind = "melee";
        }
        else
        {
            r.Kind = "left out";
            return r;
        }
        if (Arr(w, "tools").Any())
            r.Melee = MeleeFrom(w, r, nonLethal);
        foreach (var a in Arr(w, "abilities"))
            r.Abilities.Add(AbilityFrom(a, r));
        NameTheRest(w, r);
        return r;
    }

    static WeaponSpecial Special(string key, string label, bool counted, int sign = 1) =>
        new WeaponSpecial { Key = key, Label = label, Counted = counted, Sign = sign };

    /// <summary>The main verb and its projectile, with the weapon's own stat factors (normal quality changes nothing).</summary>
    static WeaponRangedInput RangedFrom(JsonNode w, JsonNode v, Reviewed r, bool nonLethal)
    {
        var sb = w["statBases"];
        var stuff = w["stuff"];
        float Factor(string stat)
        {
            float f = F(sb, stat, 1f);
            if (stuff != null) f *= F(stuff["statFactors"], stat, 1f);
            return f;
        }
        string vc = S(v, "verbClass");
        if (OwnAttackVerbs.Contains(vc))
            r.Unrated = "its attack is its own code (" + vc.Split('.').Last() + ": what it launches and how often), so it isn't rated";
        if (!KnownVerbs.Contains(vc))
            r.Unsure.Add("fires by its own rule (" + vc.Split('.').Last() + "), read as an ordinary shot");
        else if (vc.Contains("Verb_BlazingPower"))
            r.Named.Add(Special("mage-only", "only a mage of A RimWorld of Magic can fire it (anyone else's shot fails); rated for a mage", false, -1));
        else if (vc.Contains("Verb_ShootWand"))
            r.Named.Add(Special("channel", "only someone who can channel the wand can fire it", false, -1));
        else if (vc.Contains("Verb_ShootWithSmoke"))
            r.Named.Add(Special("smoke", "each shot leaves a cloud of smoke around the shooter, which blinds shots through it", false, 0));
        var p = v["projectile"];
        var input = new WeaponRangedInput
        {
            Warmup = F(v, "warmupTime") * Factor("RangedWeapon_WarmupMultiplier"),
            Cooldown = F(sb, "RangedWeapon_Cooldown", 1f),
            Range = F(v, "range", 1.42f) * Factor("RangedWeapon_RangeMultiplier"),
            MinRange = F(v, "minRange"),
            Burst = Math.Max(1, (int)F(v, "burstShotCount", 1)),
            TicksBetweenBurst = (int)F(v, "ticksBetweenBurstShots", 15),
            ForcedMissRadius = F(v, "forcedMissRadius"),
            CanGoWild = v["canGoWild"] == null || B(v, "canGoWild"),
            AccTouch = Math.Min(1f, F(sb, "AccuracyTouch", 1f)),
            AccShort = Math.Min(1f, F(sb, "AccuracyShort", 1f)),
            AccMedium = Math.Min(1f, F(sb, "AccuracyMedium", 1f)),
            AccLong = Math.Min(1f, F(sb, "AccuracyLong", 1f)),
            OneUse = vc.Contains("OneUse"),
            NonLethal = nonLethal
        };
        if (stuff != null && F(stuff["statFactors"], "RangedWeapon_Cooldown", 1f) != 1f)
            input.Cooldown *= F(stuff["statFactors"], "RangedWeapon_Cooldown", 1f);
        string dmgName;
        if (p != null && !B(p, "noProjectile") && B(p, "found"))
        {
            dmgName = S(p, "damageDef");
            float dmgMult = Factor("RangedWeapon_DamageMultiplier");
            input.Damage = (float)Math.Round(F(p, "damage") * dmgMult, MidpointRounding.AwayFromZero);
            input.ArmourPen = F(p, "armorPenetration") * Factor("RangedWeapon_ArmorPenetrationMultiplier");
            input.StoppingPower = F(p, "stoppingPower", 0.5f);
            input.ExplosionRadius = F(p, "explosionRadius");
            input.ExplosionFalloff = B(p, "explosionDamageFalloff");
            input.ExplosionDelaySeconds = F(p, "explosionDelay") / 60f;
            string pc = S(p, "thingClass");
            ProjectileRules(p, pc, input, r);
            if (F(p, "explosionChanceToStartFire") > 0f)
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, F(p, "explosionChanceToStartFire")));
            if (S(p, "preExplosionSpawnThingDef").Contains("Fuel") || S(p, "postExplosionSpawnThingDef").Contains("Fuel"))
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f));
            if (S(p, "postExplosionGasType").Contains("Tox"))
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.ToxGas, 1f));
            string spawn = S(p, "postExplosionSpawnThingDef");
            if (!string.IsNullOrEmpty(spawn) && !spawn.Contains("Fuel") && !spawn.StartsWith("Filth"))
                r.Named.Add(Special("leaves:" + spawn, "leaves " + spawn + " behind (its own effect, not counted)", false, 0));
            foreach (var e in Arr(p, "extraDamages")) AddExtra(input.Payloads, e, r, F(e, "chance", 1f));
        }
        else
        {
            dmgName = S(v, "beamDamageDef");
            var d = Dmg(dmgName);
            input.Damage = F(v, "beamTotalDamage") > 0f ? F(v, "beamTotalDamage") / Math.Max(1f, input.Burst) : F(d, "defaultDamage", 10f);
            input.ArmourPen = Math.Max(0f, F(d, "defaultArmorPenetration", 0f));
            if (input.ArmourPen <= 0f && S(d, "armorCategory") != "") input.ArmourPen = input.Damage * 0.015f;
            input.StoppingPower = F(d, "defaultStoppingPower");
            if (F(v, "beamChanceToAttachFire") > 0f) input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, F(v, "beamChanceToAttachFire")));
        }
        if (vc.Contains("ShootBeam") || vc.Contains("ArcSprayIncinerator"))
        {
            input.Beam = true;
            input.BeamWidth = F(v, "beamWidth", 1f);
            input.BeamHitsNeighbours = B(v, "beamHitsNeighborCells");
        }
        Damage(input, dmgName, r);
        if (F(v, "consumeFuelPerShot") > 0f)
            r.Named.Add(Special("fuel", "burns fuel with every shot", false, -1));
        return input;
    }

    /// <summary>
    /// What a projectile's own code does, read from the game's and the mods' code: the doomsday rocket's three fire blasts;
    /// Vanilla Expanded Framework's streams and gauss slugs (a line that strikes everyone on it, once or every so many
    /// ticks); VFE Medieval's matchlock shots (several balls, twice as hard point blank and nothing at range); the
    /// dynamite's long, random fuse; A RimWorld of Magic's staff and wand bolts (their blasts pierce armour as their code
    /// sets it, the lightning's sparks stun); the others fly and strike as a bullet does.
    /// </summary>
    static void ProjectileRules(JsonNode p, string pc, WeaponRangedInput input, Reviewed r)
    {
        bool stream = S(p, "defTag") == "ExpandableProjectileDef";
        if (stream)
        {
            input.Line = true;
            float life = F(p, "lifeTimeDuration", 100f), tick = Math.Max(1f, F(p, "tickDamageRate", 60f));
            input.HitsPerShot = B(p, "dealsDamageOnce") ? 1f : (float)Math.Ceiling(life / tick);
            return;
        }
        if (!KnownProjectiles.Contains(pc))
        {
            r.Unsure.Add("its projectile has its own behaviour (" + pc.Split('.').Last() + "), read as an ordinary one");
            return;
        }
        switch (pc.Split('.').Last())
        {
            case "Projectile_DoomsdayRocket":
                // Three Flame blasts of radius 3.9 and the rocket's damage, at random cells within 5 of the impact: a target
                // near it is in at least one of them about four times in five.
                input.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = input.Damage * 0.78f, ArmourPen = input.ArmourPen, Armour = WeaponArmour.Heat, Name = "fire blasts" });
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, 0.78f));
                r.Named.Add(Special("doomsday", "three fire blasts after the main one (counted)", true));
                break;
            case "MatchlockProjectile":
                input.DamageFallsWithDistance = true;
                input.ProjectilesPerShot = Math.Max(1, (int)F(p, "shotCount", 1f));
                break;
            case "DynamiteComp":
                // A fuse of 20 to 666 ticks; a quarter of the sticks go off as they land.
                input.ExplosionDelaySeconds = 0.75f * (20f + 323f) / 60f;
                r.Named.Add(Special("fuse", "a long, random fuse: a stick can go off at once or seconds later", true, -1));
                break;
            case "Projectile_BlazingPower":
                input.Armour = WeaponArmour.None;
                break;
            case "Projectile_FireWand":
                input.ExplosionRadius = Math.Max(input.ExplosionRadius, 1f);
                input.Armour = WeaponArmour.None;
                input.ExplosionFalloff = true;
                break;
            case "Projectile_IceWand":
                input.ArmourPen = Math.Max(input.ArmourPen, 1f);
                break;
            case "Projectile_LightningWand":
                input.ArmourPen = 0f;
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Stun, (2f + input.Damage / 2f) / 2f, 0.3f));
                break;
            case "FireArrow":
                // The game's fire arrow spreads fuel and starts a fire where it lands: whoever stands there catches.
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, 0.7f));
                break;
            case "WarbowArrow":
                r.Named.Add(Special("arc", "its arrows arc over cover and walls", true));
                break;
            case "Projectile_WandOrb":
                foreach (var x in Arr(p, "extensions"))
                    if (S(x, "class") == "OrbExtension")
                    {
                        r.Named.Add(Special("orb", "its orb carries an extra effect of its own (not counted)", false));
                        r.Unsure.Add("its orb's extra effect isn't valued");
                    }
                break;
            case "Bullet_Cryptobolter":
                r.Named.Add(Special("slow", "slows the target (cryptoslowdown, weaker on heat armour), not counted", false));
                r.Unsure.Add("its slowing effect isn't valued");
                break;
            case "Projectile_TopAttackMissile":
                r.Named.Add(Special("top-attack", "flies over cover and strikes from above", true));
                break;
        }
    }

    /// <summary>What the projectile's damage def adds: its armour category, harm, stun, EMP, fire, toxins and other hediffs.</summary>
    static void Damage(WeaponRangedInput input, string dmgName, Reviewed r)
    {
        var d = Dmg(dmgName);
        if (d == null || !B(d, "found"))
        {
            r.Unsure.Add("its damage (" + dmgName + ") isn't in the collection");
            return;
        }
        string cat = S(d, "armorCategory");
        input.Armour = ArmourOf(dmgName);
        if (cat == "Light" || cat == "Dark")
            r.Named.Add(Special("alignment", "meets only A RimWorld of Magic's alignment armour, which ordinary armour doesn't give", true));
        else if (cat != "" && cat != "Sharp" && cat != "Blunt" && cat != "Heat")
            r.Unsure.Add("its damage meets an armour of its own (" + cat + "), read as ignoring armour");
        input.HarmsHealth = d["harmsHealth"] == null || B(d, "harmsHealth");
        string worker = S(d, "workerClass");
        if (dmgName == "EMP")
        {
            input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, input.Damage > 0 ? input.Damage : 50f));
            input.HarmsHealth = false;
        }
        else if (dmgName == "Stun" || worker.EndsWith("Stun") || worker.Contains("ConditionalStun"))
        {
            float amount = input.Damage > 1f ? input.Damage : F(d, "defaultDamage", 20f);
            if (worker.Contains("ConditionalStun"))
            {
                amount = F(d, "defaultDamage", 20f) * 0.5f;
                r.Unsure.Add("its stun follows its own rule, counted as a plain stun of " + (amount * 0.5f).ToString("0") + " seconds");
            }
            input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Stun, amount));
            input.HarmsHealth = false;
            input.NonLethal = true;
        }
        if (worker == "DamageWorker_Flame")
            input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f));
        if (dmgName == "ToxGas")
            input.Payloads.Add(WeaponPayload.Of(WeaponEffect.ToxGas, 1f));
        if (!VanillaWorkers.Contains(worker) && !worker.EndsWith("Stun"))
        {
            r.Named.Add(Special("worker:" + worker, DamageWords(dmgName) + ": its own damage effect, not counted", false, 0));
            r.Unsure.Add("its damage has its own effect (" + worker.Split('.').Last() + ")");
            if (worker.Contains("TearGas") || worker.Contains("NonLethal")) input.NonLethal = true;
        }
        foreach (var h in Arr(d, "additionalHediffs"))
        {
            string hed = S(h, "hediff");
            if (hed == "ToxicBuildup")
                input.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = F(h, "severityPerDamage") * Math.Max(input.Damage, 1f) + F(h, "severity") });
            else
            {
                r.Named.Add(Special("hediff:" + hed, "inflicts " + Words(hed) + " (its own effect, not counted)", false));
                if (hed.Contains("Anesthetic")) input.NonLethal = true;
            }
        }
    }

    /// <summary>An extra damage (a projectile's or a melee tool's), by its def.</summary>
    static void AddExtra(List<WeaponPayload> payloads, JsonNode e, Reviewed r, float chance)
    {
        string name = S(e, "def");
        var d = Dmg(name);
        float amount = F(e, "amount");
        if (name == "EMP") { payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, amount, chance)); return; }
        if (name == "Stun" || S(d, "workerClass").EndsWith("Stun")) { payloads.Add(WeaponPayload.Of(WeaponEffect.Stun, amount, chance)); return; }
        if (d == null || !B(d, "found"))
        {
            r.Named.Add(Special("extra:" + name, "extra " + name + " (" + amount.ToString("0.#") + ", not in the collection)", false));
            return;
        }
        if (d["harmsHealth"] != null && !B(d, "harmsHealth"))
        {
            r.Named.Add(Special("extra:" + name, "extra " + DamageWords(name) + " (its own effect, not counted)", false));
            return;
        }
        float ap = F(e, "armorPenetration", -1f);
        if (ap < 0f) ap = S(d, "armorCategory") == "" ? 0f : amount * 0.015f;
        payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = amount, ArmourPen = ap, Armour = ArmourOf(name), Chance = chance, Name = DamageWords(name) });
        if (S(d, "workerClass") == "DamageWorker_Flame")
            payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, chance));
        foreach (var h in Arr(d, "additionalHediffs"))
        {
            if (S(h, "hediff") == "ToxicBuildup")
                payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = F(h, "severityPerDamage") * amount, Chance = chance });
            else
                r.Named.Add(Special("hediff:" + S(h, "hediff"), "inflicts " + Words(S(h, "hediff")) + " (its own effect, not counted)", false));
        }
    }

    /// <summary>The melee tools, each capacity a verb with its damage, and the stuff the weapon is rated in.</summary>
    static WeaponMeleeInput MeleeFrom(JsonNode w, Reviewed r, bool nonLethal)
    {
        var m = new WeaponMeleeInput { NonLethal = nonLethal };
        var stuff = w["stuff"];
        if (stuff != null)
        {
            m.Stuffed = true;
            m.SharpFactor = F(stuff["statBases"], "SharpDamageMultiplier", 1f);
            m.BluntFactor = F(stuff["statBases"], "BluntDamageMultiplier", 1f);
            m.CooldownFactor = F(stuff["statFactors"], "MeleeWeapon_CooldownMultiplier", 1f);
            r.StuffLabel = S(stuff, "label");
        }
        foreach (var t in Arr(w, "tools"))
        {
            foreach (var c in Arr(t, "capacities"))
            {
                string dmg = S(c, "damageDef");
                if (!B(c, "known"))
                {
                    r.Unsure.Add("a melee move with no maneuver in the collection (" + S(c, "capacity") + ")");
                    continue;
                }
                string verbClass = S(c, "verbClass");
                bool cleave = verbClass.EndsWith("Verb_MeleeCleave");
                if (cleave)
                    r.Named.Add(Special("cleave", S(t, "label") + ": cleaves, 60% more to the target and 60% to every enemy beside it (counted on the target)", true));
                else if (verbClass.EndsWith("Verb_MeleeAttackDamageCryptoaxe"))
                {
                    r.Named.Add(Special("slow", "slows whoever it strikes (cryptoslowdown, weaker on heat armour), not counted", false));
                    r.Unsure.Add("its slowing effect isn't valued");
                }
                else if (verbClass != "Verb_MeleeAttackDamage" && !string.IsNullOrEmpty(verbClass))
                {
                    r.Unsure.Add("a melee move with its own rule (" + verbClass.Split('.').Last() + "), read as an ordinary blow");
                    r.Named.Add(Special("move:" + S(c, "capacity"), S(t, "label") + ": its own move (" + verbClass.Split('.').Last() + "), not counted beyond its damage", false));
                }
                var d = Dmg(dmg);
                var a = new WeaponMeleeAttack
                {
                    Label = S(t, "label"), Power = F(t, "power"), Cooldown = F(t, "cooldownTime", 2f), ArmourPen = F(t, "armorPenetration", -1f),
                    Armour = ArmourOf(dmg), ChanceFactor = F(t, "chanceFactor", 1f), AdditionalHediffs = Arr(d, "additionalHediffs").Count(),
                    Cuts = S(d, "armorCategory") != "Blunt" && S(d, "workerClass") is "DamageWorker_Cut" or "DamageWorker_Stab" or "DamageWorker_Scratch" or "DamageWorker_Bite"
                };
                if (!VanillaWorkers.Contains(S(d, "workerClass")))
                {
                    r.Named.Add(Special("worker:" + S(d, "workerClass"), DamageWords(dmg) + ": its own damage effect, not counted", false, 0));
                    r.Unsure.Add("a melee damage with its own effect (" + S(d, "workerClass").Split('.').Last() + ")");
                }
                foreach (var h in Arr(d, "additionalHediffs"))
                {
                    if (S(h, "hediff") == "ToxicBuildup")
                        a.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = F(h, "severityPerDamage") * a.Power });
                    else
                        r.Named.Add(Special("hediff:" + S(h, "hediff"), "inflicts " + Words(S(h, "hediff")) + " (its own effect, not counted)", false));
                }
                foreach (var e in Arr(t, "extraDamages"))
                {
                    if (B(e, "surprise")) continue;
                    AddExtra(a.Payloads, e, r, F(e, "chance", 1f));
                }
                if (cleave)
                    a.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = 0.6f * a.Power, ArmourPen = a.ArmourPen >= 0f ? a.ArmourPen : 0.6f * a.Power * 0.015f, Armour = a.Armour, Name = "cleave" });
                m.Attacks.Add(a);
            }
        }
        if (m.Attacks.Exists(a => a.Payloads.Exists(p => p.Effect == WeaponEffect.Stun)) && r.Mod == "vanillaexpanded.vwenl") m.NonLethal = true;
        return m;
    }

    /// <summary>An ability the weapon grants, counted when the review can read what it does from its defs.</summary>
    static WeaponAbilityInput AbilityFrom(JsonNode a, Reviewed r)
    {
        var ab = new WeaponAbilityInput { Label = S(a, "label"), Charges = (int)F(a, "charges"), CooldownSeconds = F(a, "cooldownTicks") / 60f };
        if (string.IsNullOrEmpty(ab.Label)) ab.Label = S(a, "defName");
        if (!B(a, "found"))
        {
            ab.Note = "not in the collection";
            r.Unsure.Add("grants an ability the collection doesn't hold (" + S(a, "defName") + ")");
            return ab;
        }
        var v = a["verb"];
        JsonNode proj = null;
        foreach (var c in Arr(a, "comps"))
        {
            string cls = S(c, "class");
            if (cls == "CompProperties_AbilityLaunchProjectile" && c["projectile"] != null) proj = c["projectile"];
            if (cls == "CompProperties_AbilityFireSpew")
            {
                ab.AreaDamage = F(c, "damAmount", 10f);
                ab.AreaArmour = WeaponArmour.Heat;
                ab.AreaTargets = 1.5f + F(c, "lineWidthEnd", 2f) * 0.25f;
                ab.AreaIgnite = 1f;
                ab.Counted = true;
            }
        }
        if (proj == null && v?["projectile"] != null) proj = v["projectile"];
        if (proj == null && !string.IsNullOrEmpty(S(v, "beamDamageDef")))
        {
            var d = Dmg(S(v, "beamDamageDef"));
            var beam = new WeaponRangedInput
            {
                Warmup = F(v, "warmupTime"), Cooldown = 0f, Range = F(v, "range", 12f), Burst = Math.Max(1, (int)F(v, "burstShotCount", 1)),
                TicksBetweenBurst = (int)F(v, "ticksBetweenBurstShots", 15), Beam = true, BeamWidth = F(v, "beamWidth", 1f),
                BeamHitsNeighbours = B(v, "beamHitsNeighborCells"), Damage = F(d, "defaultDamage", 10f),
                ArmourPen = Math.Max(0f, F(d, "defaultArmorPenetration", 0f)), Armour = ArmourOf(S(v, "beamDamageDef"))
            };
            var dummy = new Reviewed();
            Damage(beam, S(v, "beamDamageDef"), dummy);
            ab.Shot = beam;
            ab.Counted = true;
            return ab;
        }
        if (proj != null && B(proj, "found") && !B(proj, "noProjectile"))
        {
            var shot = new WeaponRangedInput
            {
                Warmup = F(v, "warmupTime"), Cooldown = 0f, Range = F(v, "range", 12f), Burst = Math.Max(1, (int)F(v, "burstShotCount", 1)),
                TicksBetweenBurst = (int)F(v, "ticksBetweenBurstShots", 15), ForcedMissRadius = F(v, "forcedMissRadius"),
                CanGoWild = v?["canGoWild"] == null || B(v, "canGoWild"),
                Damage = F(proj, "damage"), ArmourPen = F(proj, "armorPenetration"), StoppingPower = F(proj, "stoppingPower", 0.5f),
                ExplosionRadius = F(proj, "explosionRadius"), ExplosionFalloff = B(proj, "explosionDamageFalloff"),
                ExplosionDelaySeconds = F(proj, "explosionDelay") / 60f
            };
            var dummy = new Reviewed();
            Damage(shot, S(proj, "damageDef"), dummy);
            if (F(proj, "explosionChanceToStartFire") > 0f) shot.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, F(proj, "explosionChanceToStartFire")));
            if (S(proj, "preExplosionSpawnThingDef").Contains("Fuel")) shot.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f));
            foreach (var e in Arr(proj, "extraDamages")) AddExtra(shot.Payloads, e, dummy, F(e, "chance", 1f));
            ab.Shot = shot;
            ab.Counted = true;
            if (!KnownVerbs.Contains(S(v, "verbClass")))
                r.Unsure.Add("its ability fires by its own rule (" + S(v, "verbClass").Split('.').Last() + "), read as an ordinary shot");
        }
        if (!ab.Counted)
        {
            ab.Note = "its own effect";
            r.Unsure.Add("its ability (" + ab.Label + ") works by its own code, not counted");
        }
        return ab;
    }

    /// <summary>What the review can only name: biocoding, personas, unique traits, heavy-weapon rules, mods' own comps, patches.</summary>
    static void NameTheRest(JsonNode w, Reviewed r)
    {
        var classes = Arr(w, "compClasses").Select(x => x.ToString()).ToList();
        foreach (var c in Arr(w, "comps"))
        {
            string cls = S(c, "class");
            switch (cls)
            {
                case "CompProperties_Biocodable":
                    r.Named.Add(Special("biocodable", "can be biocoded to one person", false, 0));
                    break;
                case "CompProperties_BladelinkWeapon":
                    r.Named.Add(Special("persona", "persona weapon: bonds to whoever wields it first, with persona traits rolled when made (moods and needs, plus or minus), not counted", false, 0));
                    break;
                case "CompProperties_UniqueWeapon":
                    r.Named.Add(Special("unique", "unique weapon: one to three traits rolled when made; rated as the plain weapon", false, 0));
                    r.Unsure.Add("a unique weapon: its rolled traits can move it either way");
                    break;
                case "CompProperties_Explosive":
                    r.Named.Add(Special("volatile", "explodes if it burns", false, -1));
                    break;
                case "CompProperties_LaserCapacitor":
                    if (r.Ranged != null && F(c, "WarmUpReductionPerShot") > 0f)
                    {
                        // Vanilla Expanded Framework's capacitor, in its quick-fire mode: each shot from the same spot takes
                        // WarmUpReductionPerShot off the aim, down to none; counted over a stand of ten shots.
                        float w0 = r.Ranged.Warmup, red = F(c, "WarmUpReductionPerShot"), sum = 0f;
                        for (int k = 0; k < 10; k++) sum += Math.Max(0f, w0 - red * k);
                        r.Ranged.Warmup = sum / 10f;
                        r.Named.Add(Special("laser", "laser capacitor: in its quick-fire mode, aims faster with each shot from the same spot (counted over ten shots)", true));
                        if (B(c, "Overheats"))
                            r.Named.Add(Special("overheat", "at full heat each shot risks an overheat blast on the shooter (" + Math.Round(F(c, "OverheatChance") * 100f).ToString("0") + "%)", false, -1));
                    }
                    break;
                case "CompProperties_MultiVerbWeapon":
                case "CompProperties_VerbProps":
                    r.Named.Add(Special("modes", "switches between firing modes; rated in its best", true, 0));
                    break;
                case "CompProperties_ChangeableProjectile":
                    r.Named.Add(Special("shells", "fires the shells it is loaded with", false, 0));
                    r.Unsure.Add("fires whatever it is loaded with");
                    break;
                case "CompProperties_RallyAbility":
                    r.Named.Add(Special("rally", "rallies its bearer when enemies are near (a fighting boost of its own hediff), not counted in damage", false));
                    break;
                case "CompProperties_EnchantedItem":
                    r.Named.Add(Special("enchanted", "magic enchantments (A RimWorld of Magic), not counted", false, 0));
                    break;
                case "CompProperties_Deflector":
                case "CompBPReflector":
                    // A RimWorld of Magic's deflector: its base chance plus a share per level of the skill it names.
                    float chance = Math.Min(1f, F(c, "baseDeflectChance") + 8f * F(c, "deflectRatePerSkillPoint"));
                    r.Passives.Add(new WeaponPassive("DeflectChance", chance));
                    r.Named.Add(Special("deflector", "deflects incoming shots (" + Math.Round(chance * 100f).ToString("0") + "% at skill 8)" + (B(c, "canReflect") ? ", sometimes back at the shooter" : "") + " (counted)", true));
                    break;
                case "CompProperties_WeaponDeteriorable":
                    r.Named.Add(Special("wears", "wears out as it is used", false, -1));
                    break;
                case "CompEquippable_Wand":
                    r.Named.Add(Special("wand", "a wand: works by its own rules (not counted)", false, 0));
                    break;
                default:
                    if (!QuietComps.Contains(cls) && !string.IsNullOrEmpty(cls) && !cls.StartsWith("CompProperties_Equippable"))
                    {
                        r.Named.Add(Special("comp:" + cls, "its own part (" + cls.Replace("CompProperties_", "") + "), not counted", false, 0));
                        r.Unsure.Add("has a part of its own (" + cls.Replace("CompProperties_", "") + ")");
                    }
                    break;
            }
        }
        foreach (var x in Arr(w, "modExtensions"))
        {
            string cls = S(x, "class");
            if (cls == "HeavyWeapon" && B(x, "isHeavy"))
            {
                var who = Arr(x, "supportedArmors").Select(a => Words(a.ToString())).Concat(Arr(x, "supportedTraits").Select(a => "the " + Words(a.ToString()) + " trait"))
                    .Concat(Arr(x, "supportedGenes").Select(a => Words(a.ToString()))).ToList();
                r.Named.Add(Special("heavy-weapon", "a heavy weapon: only " + (who.Count > 0 ? "someone with " + string.Join(" or ", who) : "some") + " can carry it" +
                    (F(x, "hpPerShot") > 0f ? "; wears down by " + F(x, "hpPerShot").ToString("0") + " a shot" : ""), false, -1));
            }
            if (cls == "MakeshiftProperties")
                r.Named.Add(Special("makeshift", "makeshift: may fall apart after " + S(x, "shots").Replace("~", " to ") + " shots (its own rule)", false, -1));
            if (cls == "ThingDefExtension" && B(x, "usableWithShields"))
                r.Named.Add(Special("shields", "can be used behind a shield belt", false));
        }
        foreach (var p in Arr(w, "patches"))
        {
            string what = S(p, "what");
            string mod = S(p, "modName");
            if ((what.StartsWith("tools") || what.StartsWith("verbs") || what.StartsWith("statBases") || what.StartsWith("projectile") || what.StartsWith("equippedStatOffsets"))
                && string.IsNullOrEmpty(S(p, "when")))
                r.Unsure.Add("a patch from " + mod + " changes its " + what.Split('/')[0] + " (not applied here)");
        }
        if (classes.Contains("CompProperties_EquippableAbilityReloadable") && !Arr(w, "abilities").Any() && !classes.Contains("CompProperties_UniqueWeapon"))
            r.Unsure.Add("can hold charges for an ability it doesn't name");
    }

    /// <summary>A def name in plain words: its own label when the collection holds it, else the name split at capitals.</summary>
    public static string Words(string defName)
    {
        if (string.IsNullOrEmpty(defName)) return "";
        string s = System.Text.RegularExpressions.Regex.Replace(defName, "^[A-Z]{2,5}_", "");
        s = s.Replace("_", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
        return s.ToLowerInvariant();
    }
}
