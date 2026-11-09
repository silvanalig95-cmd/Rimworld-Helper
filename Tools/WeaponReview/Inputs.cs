using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>
/// One weapon of the extracted collection (Tools/weapon_extract.py) as the facts the mod's own reading takes
/// (Source/ColonyCouncil/Weapons/WeaponReadMath.cs, linked): the review and the game read a weapon by the same rules, the
/// review from the player's def files, the game from the live def and the weapon in hand (Weapons/WeaponLive.cs).
/// </summary>
public static class Inputs
{
    public static JsonObject DamageDefs;

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

    static float? Maybe(JsonNode n, string key) => n?[key] == null ? null : F(n, key);

    static readonly Dictionary<string, WeaponDamageFacts> damages = new();

    /// <summary>A damage def's facts by name: null for one the collection doesn't list, not found for one it names but doesn't hold.</summary>
    public static WeaponDamageFacts DamageOf(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (damages.TryGetValue(name, out var known)) return known;
        var d = DamageDefs?[name];
        WeaponDamageFacts f = null;
        if (d != null)
        {
            f = new WeaponDamageFacts { Name = name, Found = B(d, "found") };
            if (f.Found)
            {
                f.Label = S(d, "label");
                f.ArmourCategory = S(d, "armorCategory");
                f.Worker = S(d, "workerClass");
                f.DefaultDamage = Maybe(d, "defaultDamage");
                f.DefaultArmourPen = Maybe(d, "defaultArmorPenetration");
                f.DefaultStoppingPower = Maybe(d, "defaultStoppingPower");
                f.HarmsHealth = d["harmsHealth"] == null ? null : B(d, "harmsHealth");
                foreach (var h in Arr(d, "additionalHediffs"))
                    f.Hediffs.Add(new WeaponHediffFacts { Hediff = S(h, "hediff"), PerDamage = F(h, "severityPerDamage"), Severity = F(h, "severity") });
            }
        }
        damages[name] = f;
        return f;
    }

    /// <summary>The weapon's facts: its numbers with its stuff's factors (normal quality), its verbs, tools, abilities, comps and patches.</summary>
    public static WeaponDefFacts Facts(JsonNode w)
    {
        var sb = w["statBases"];
        var stuff = w["stuff"];
        float Factor(string stat)
        {
            float f = F(sb, stat, 1f);
            if (stuff != null) f *= F(stuff["statFactors"], stat, 1f);
            return f;
        }
        var f = new WeaponDefFacts
        {
            DefName = S(w, "defName"), Label = S(w, "label"), Mod = S(w, "mod"), ModName = S(w, "modName"), Tech = S(w, "techLevel"),
            Mass = F(sb, "Mass", 1f), Market = F(sb, "MarketValue", 0f), LeftOut = S(w, "leftOut"),
            ResearchTech = S(w, "researchTech"), Made = B(w, "made"), CarriedByKinds = B(w, "carriedByKinds"), DamageOf = DamageOf
        };
        foreach (var stat in new[] { "RangedWeapon_WarmupMultiplier", "RangedWeapon_RangeMultiplier", "RangedWeapon_DamageMultiplier",
                     "RangedWeapon_ArmorPenetrationMultiplier", "RangedWeapon_Cooldown" })
            f.Stats[stat] = Factor(stat);
        foreach (var stat in new[] { "AccuracyTouch", "AccuracyShort", "AccuracyMedium", "AccuracyLong" })
            f.Stats[stat] = F(sb, stat, 1f);
        if (stuff != null)
        {
            f.Stuffed = true;
            f.StuffLabel = S(stuff, "label");
            f.SharpFactor = F(stuff["statBases"], "SharpDamageMultiplier", 1f);
            f.BluntFactor = F(stuff["statBases"], "BluntDamageMultiplier", 1f);
            f.MeleeCooldownFactor = F(stuff["statFactors"], "MeleeWeapon_CooldownMultiplier", 1f);
        }
        foreach (var kv in (w["equippedStatOffsets"] as JsonObject) ?? new JsonObject())
            f.EquippedOffsets.Add(new KeyValuePair<string, float>(kv.Key, F(w["equippedStatOffsets"], kv.Key)));
        f.Verbs.AddRange(Arr(w, "verbs").Select(Verb));
        f.Tools.AddRange(Arr(w, "tools").Select(Tool));
        f.Abilities.AddRange(Arr(w, "abilities").Select(Ability));
        f.Comps.AddRange(Arr(w, "comps").Select(Part));
        f.CompClasses.AddRange(Arr(w, "compClasses").Select(x => x.ToString()));
        f.Extensions.AddRange(Arr(w, "modExtensions").Select(Part));
        f.WeaponTags.AddRange(Arr(w, "weaponTags").Select(x => x.ToString()));
        f.Research.AddRange(Arr(w, "research").Select(x => x.ToString()));
        foreach (var p in Arr(w, "patches"))
        {
            string what = S(p, "what");
            if ((what.StartsWith("tools") || what.StartsWith("verbs") || what.StartsWith("statBases") || what.StartsWith("projectile") || what.StartsWith("equippedStatOffsets"))
                && string.IsNullOrEmpty(S(p, "when")))
                f.PatchNotes.Add("a patch from " + S(p, "modName") + " changes its " + what.Split('/')[0] + " (not applied here)");
        }
        return f;
    }

    static WeaponVerbFacts Verb(JsonNode v) => new()
    {
        VerbClass = S(v, "verbClass"), Range = F(v, "range", 1.42f), MinRange = F(v, "minRange"), Warmup = F(v, "warmupTime"),
        Burst = (int)F(v, "burstShotCount", 1), TicksBetween = (int)F(v, "ticksBetweenBurstShots", 15), ForcedMiss = F(v, "forcedMissRadius"),
        CanGoWild = v["canGoWild"] == null || B(v, "canGoWild"), BeamDamageDef = S(v, "beamDamageDef"), BeamTotalDamage = F(v, "beamTotalDamage"),
        BeamFireChance = F(v, "beamChanceToAttachFire"), BeamWidth = F(v, "beamWidth", 1f), BeamNeighbours = B(v, "beamHitsNeighborCells"),
        FuelPerShot = F(v, "consumeFuelPerShot"), Projectile = v["projectile"] == null ? null : Projectile(v["projectile"])
    };

    static WeaponProjectileFacts Projectile(JsonNode p)
    {
        var f = new WeaponProjectileFacts { DefName = S(p, "defName"), Found = B(p, "found"), NoProjectile = B(p, "noProjectile"), ThingClass = S(p, "thingClass") };
        if (!f.Found || f.NoProjectile) return f;
        f.DamageDef = S(p, "damageDef");
        f.Damage = F(p, "damage");
        f.ArmourPen = F(p, "armorPenetration");
        f.StoppingPower = F(p, "stoppingPower", 0.5f);
        f.ExplosionRadius = F(p, "explosionRadius");
        f.Falloff = B(p, "explosionDamageFalloff");
        f.ExplosionDelayTicks = F(p, "explosionDelay");
        f.ChanceToStartFire = F(p, "explosionChanceToStartFire");
        f.PreSpawn = S(p, "preExplosionSpawnThingDef");
        f.PostSpawn = S(p, "postExplosionSpawnThingDef");
        f.PostGas = S(p, "postExplosionGasType");
        f.Stream = S(p, "defTag") == "ExpandableProjectileDef";
        f.LifeTime = Maybe(p, "lifeTimeDuration");
        f.TickDamageRate = Maybe(p, "tickDamageRate");
        f.DealsDamageOnce = B(p, "dealsDamageOnce");
        f.ShotCount = F(p, "shotCount", 1f);
        f.Extras.AddRange(Arr(p, "extraDamages").Select(Extra));
        f.ExtensionClasses.AddRange(Arr(p, "extensions").Select(x => S(x, "class")));
        return f;
    }

    static WeaponExtraFacts Extra(JsonNode e) => new()
    {
        Def = S(e, "def"), Amount = F(e, "amount"), ArmourPen = F(e, "armorPenetration", -1f), Chance = F(e, "chance", 1f), Surprise = B(e, "surprise")
    };

    static WeaponToolFacts Tool(JsonNode t)
    {
        var f = new WeaponToolFacts
        {
            Label = S(t, "label"), Power = F(t, "power"), Cooldown = F(t, "cooldownTime", 2f), ArmourPen = F(t, "armorPenetration", -1f), ChanceFactor = F(t, "chanceFactor", 1f)
        };
        foreach (var c in Arr(t, "capacities"))
            f.Capacities.Add(new WeaponCapacityFacts { Capacity = S(c, "capacity"), DamageDef = S(c, "damageDef"), VerbClass = S(c, "verbClass"), Known = B(c, "known") });
        f.Extras.AddRange(Arr(t, "extraDamages").Select(Extra));
        return f;
    }

    static WeaponAbilityFacts Ability(JsonNode a)
    {
        var f = new WeaponAbilityFacts
        {
            DefName = S(a, "defName"), Label = S(a, "label"), Found = B(a, "found"), Charges = (int)F(a, "charges"), CooldownTicks = F(a, "cooldownTicks"),
            Verb = a["verb"] == null ? null : Verb(a["verb"])
        };
        foreach (var c in Arr(a, "comps"))
            f.Comps.Add(new WeaponAbilityCompFacts
            {
                Class = S(c, "class"), DamAmount = Maybe(c, "damAmount"), LineWidthEnd = Maybe(c, "lineWidthEnd"),
                Projectile = c["projectile"] == null ? null : Projectile(c["projectile"])
            });
        return f;
    }

    /// <summary>A comp or an extension: its class, its simple fields as written, its lists.</summary>
    static WeaponPartFacts Part(JsonNode c)
    {
        var f = new WeaponPartFacts { Class = S(c, "class") };
        foreach (var kv in (c as JsonObject) ?? new JsonObject())
        {
            if (kv.Value is JsonArray arr) f.Lists[kv.Key] = arr.Where(x => x != null).Select(x => x.ToString()).ToList();
            else if (kv.Value is JsonValue) f.Fields[kv.Key] = kv.Value.ToString();
        }
        return f;
    }
}
