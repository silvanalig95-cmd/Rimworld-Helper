using System.Globalization;
using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>
/// One piece of the extracted collection (Tools/apparel_extract.py) as the plain facts the mod's own reading takes
/// (Source/ColonyCouncil/Apparel/ApparelReadMath.cs, linked): its stats, stuff, coverage, comps, verbs, abilities, hediffs
/// and the apparel rules it meets. Everything after that (the rating's facts, the rating, its tags and line, the rating by
/// stuff) is the same code the game runs on a live def, so the review and the game agree.
/// </summary>
public static class Inputs
{
    public static JsonObject Data;

    public static float F(JsonNode n, string key, float def = 0f)
    {
        var v = n?[key];
        if (v == null) return def;
        try { return v.GetValue<float>(); }
        catch
        {
            try { return (float)v.GetValue<double>(); }
            catch { return float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def; }
        }
    }

    public static string S(JsonNode n, string key) => n?[key]?.ToString() ?? "";

    public static bool B(JsonNode n, string key, bool def = false)
    {
        var s = n?[key]?.ToString();
        return string.IsNullOrEmpty(s) ? def : s.Trim().ToLowerInvariant() == "true";
    }

    public static IEnumerable<JsonNode> Arr(JsonNode n, string key) => (n?[key] as JsonArray)?.Where(x => x != null) ?? Enumerable.Empty<JsonNode>();

    public static List<string> Strs(JsonNode n, string key) => Arr(n, key).Select(x => x.ToString()).ToList();

    public static Dictionary<string, float> Dict(JsonNode n, string key)
    {
        var d = new Dictionary<string, float>();
        if (n?[key] is JsonObject o)
            foreach (var kv in o)
                if (kv.Value != null && float.TryParse(kv.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) d[kv.Key] = f;
        return d;
    }

    static readonly Dictionary<string, ApparelStuffFacts> stuffs = new();
    static readonly Dictionary<string, ApparelHediffFacts> hediffs = new();

    /// <summary>A stuff's label, powers and flammability, from the extracted stuffs; null for one they don't hold.</summary>
    public static ApparelStuffFacts StuffFacts(string def)
    {
        if (string.IsNullOrEmpty(def)) return null;
        if (stuffs.TryGetValue(def, out var known)) return known;
        var s = Data["stuffs"]?[def];
        ApparelStuffFacts f = null;
        if (s != null)
        {
            var factors = Dict(s, "statFactors");
            f = new ApparelStuffFacts
            {
                Def = def, Label = S(s, "label"),
                Powers = Dict(s, "statBases").Where(kv => kv.Key.StartsWith("StuffPower_")).ToDictionary(kv => kv.Key, kv => kv.Value),
                Flammability = factors.TryGetValue("Flammability", out var fl) ? fl : 1f
            };
        }
        stuffs[def] = f;
        return f;
    }

    /// <summary>A hediff's stages and comps, from the extracted hediffs; null for one they don't name.</summary>
    public static ApparelHediffFacts Hediff(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (hediffs.TryGetValue(name, out var known)) return known;
        var h = Data["hediffs"]?[name];
        ApparelHediffFacts f = null;
        if (h != null)
        {
            f = new ApparelHediffFacts { Name = name, Found = B(h, "found"), Label = S(h, "label") };
            foreach (var st in Arr(h, "stages"))
            {
                var stage = new ApparelHediffStage
                {
                    MinSeverity = F(st, "minSeverity"), Offsets = Dict(st, "statOffsets"), Factors = Dict(st, "statFactors"),
                    PainOffset = F(st, "painOffset"), BleedFactor = F(st, "totalBleedFactor", 1f)
                };
                foreach (var c in Arr(st, "capMods"))
                    stage.Capacities.Add((S(c, "capacity"), F(c, "offset"), F(c, "postFactor", 1f), c["setMax"] == null ? -1f : F(c, "setMax")));
                f.Stages.Add(stage);
            }
            foreach (var c in Arr(h, "comps")) f.CompClasses.Add(S(c, "class"));
        }
        hediffs[name] = f;
        return f;
    }

    /// <summary>A comp or an extension: its class, its scalar fields as text, its lists, its maps of numbers, every key it writes.</summary>
    static ApparelPartFacts Part(JsonNode c)
    {
        var p = new ApparelPartFacts { Class = S(c, "class"), CompClass = S(c, "compClass") };
        if (c is JsonObject o)
            foreach (var kv in o)
            {
                if (kv.Value == null) continue;
                p.Present.Add(kv.Key);
                switch (kv.Value)
                {
                    case JsonValue v:
                        p.Fields[kv.Key] = v.ToString();
                        break;
                    case JsonArray:
                        p.Lists[kv.Key] = Strs(c, kv.Key);
                        break;
                    case JsonObject:
                        p.Maps[kv.Key] = Dict(c, kv.Key);
                        break;
                }
            }
        return p;
    }

    /// <summary>A verb, its projectile and its damage's armour category and harm.</summary>
    static ApparelVerbFacts Verb(JsonNode v)
    {
        var f = new ApparelVerbFacts
        {
            VerbClass = S(v, "verbClass"), Label = S(v, "label"), Range = F(v, "range"), ForcedMiss = F(v, "forcedMissRadius"),
            CanGoWild = B(v, "canGoWild", true), Burst = (int)F(v, "burstShotCount", 1), TicksBetween = (int)F(v, "ticksBetweenBurstShots", 15),
            Warmup = F(v, "warmupTime"), Cooldown = F(v, "defaultCooldownTime")
        };
        var p = v?["projectile"];
        if (p != null)
        {
            var dd = Data["damageDefs"]?[S(p, "damageDef")];
            f.Projectile = new ApparelProjectileFacts
            {
                DefName = S(p, "defName"), Found = B(p, "found"), NoProjectile = B(p, "noProjectile"), DamageDef = S(p, "damageDef"),
                Damage = F(p, "damage"), ArmourPen = F(p, "armorPenetration"), StoppingPower = F(p, "stoppingPower"),
                ExplosionRadius = F(p, "explosionRadius"), ExplosionDelayTicks = F(p, "explosionDelay"), ChanceToStartFire = F(p, "explosionChanceToStartFire"),
                PostGas = S(p, "postExplosionGasType"), ArmourCategory = S(dd, "armorCategory"), HarmsHealth = dd == null || B(dd, "harmsHealth", true)
            };
        }
        return f;
    }

    /// <summary>The plain facts of one extracted piece, in its usual stuff.</summary>
    public static ApparelDefFacts DefFacts(JsonNode n)
    {
        var d = new ApparelDefFacts
        {
            DefName = S(n, "defName"), Label = S(n, "label"), Mod = S(n, "mod"), ModName = S(n, "modName"), TechLevel = S(n, "techLevel"),
            ThingClass = S(n, "thingClass"), StatBases = Dict(n, "statBases"), EquippedOffsets = Dict(n, "equippedStatOffsets"),
            StuffCategories = Strs(n, "stuffCategories"), Layers = Strs(n, "layers"), Groups = Strs(n, "bodyPartGroups"), Tags = Strs(n, "tags"),
            ThingCategories = Strs(n, "thingCategories"), Research = Strs(n, "research"), ResearchTech = S(n, "researchTech"),
            StatDefaults = Dict(Data, "statDefaults"), Made = B(n, "made"), WornByKinds = B(n, "wornByKinds"),
            Stuff = S(n, "stuff"), OtherStuffs = Strs(n, "stuffs"), StuffOf = StuffFacts, HediffOf = Hediff,
        };
        var ap = n["apparel"];
        d.CountsAsClothes = B(ap, "countsAsClothingForNudity", true);
        d.CareIfDamaged = B(ap, "careIfDamaged", true);
        d.CareIfWornByCorpse = B(ap, "careIfWornByCorpse", true);
        d.WearPerDay = ap?["wearPerDay"] == null ? 0.4f : F(ap, "wearPerDay", 0.4f);
        d.Gender = S(ap, "gender");
        d.Ages = S(ap, "developmentalStageFilter");
        d.SlaveApparel = B(ap, "slaveApparel");
        d.MechanitorApparel = B(ap, "mechanitorApparel");
        d.ImmuneToToxGas = B(ap, "immuneToToxGasExposure");
        d.BlocksVision = B(ap, "blocksVision");
        d.IgnoredByNonViolent = B(ap, "ignoredByNonViolent");
        d.Comps = Arr(n, "comps").Select(Part).ToList();
        d.Extensions = Arr(n, "extensions").Select(Part).ToList();
        d.Verbs = Arr(n, "verbs").Select(Verb).ToList();
        foreach (var ab in Arr(n, "abilities"))
            d.Abilities.Add(new ApparelAbilityFacts
            {
                DefName = S(ab, "defName"), Label = S(ab, "label"), CooldownTicks = F(ab, "cooldownTicks"),
                VerbRange = ab["verb"] == null ? null : F(ab["verb"], "range", 15f)
            });
        foreach (var m in Arr(n, "requirementsMet"))
            d.Rules.Add(new ApparelRuleFacts { Source = S(m, "source"), Label = S(m, "label"), Seniority = F(m, "seniority"), Required = B(m, "required") });
        foreach (var p in Arr(n, "patches")) d.Patches.Add((S(p, "what"), S(p, "modName")));
        return d;
    }

    /// <summary>The rating's facts of one extracted piece, made of the stuff given (its usual one when none).</summary>
    public static ApparelFacts Facts(JsonNode n, string stuff = null)
    {
        var d = DefFacts(n);
        string sd = stuff ?? d.Stuff;
        return ApparelRead.Facts(d, sd == "" ? null : StuffFacts(sd) ?? new ApparelStuffFacts { Def = sd });
    }
}
