using System.Globalization;
using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>
/// One piece of the extracted collection (Tools/apparel_extract.py) as the facts the mod's own rating takes
/// (Source/ColonyCouncil/Apparel/ApparelRateMath.cs, linked): its stats, its stuff, its coverage, and its comps, verbs and
/// hediffs read by the rating's own ReadComp and PowerOfVerb, so the game can read a live def into the same facts.
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

    static string Tail(string cls) => cls.Contains('.') ? cls[(cls.LastIndexOf('.') + 1)..] : cls;

    /// <summary>A stuff's powers and label, from the extracted stuffs.</summary>
    public static (string label, Dictionary<string, float> powers, float flammability) Stuff(string def)
    {
        var s = Data["stuffs"]?[def];
        if (s == null) return ("", new Dictionary<string, float>(), 1f);
        var powers = Dict(s, "statBases").Where(kv => kv.Key.StartsWith("StuffPower_")).ToDictionary(kv => kv.Key, kv => kv.Value);
        var factors = Dict(s, "statFactors");
        return (S(s, "label"), powers, factors.TryGetValue("Flammability", out var fl) ? fl : 1f);
    }

    /// <summary>A copy of the facts made of another stuff.</summary>
    public static ApparelFacts WithStuff(ApparelFacts a, string def)
    {
        var (label, powers, flam) = Stuff(def);
        var b = (ApparelFacts)typeof(ApparelFacts).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(a, null)!;
        b.StuffDef = def;
        b.StuffLabel = label;
        b.StuffPowers = powers;
        b.StuffFlammability = flam;
        return b;
    }

    /// <summary>A hediff's stage at a severity: the last whose minimum it reaches.</summary>
    static JsonNode Stage(JsonNode h, float severity)
    {
        JsonNode pick = null;
        foreach (var st in Arr(h, "stages"))
            if (pick == null || F(st, "minSeverity") <= severity) pick = st;
        return pick;
    }

    static void AddHediff(ApparelFacts a, string name, float severity = 1f)
    {
        var h = Data["hediffs"]?[name];
        if (h == null || !B(h, "found")) { a.Unsure.Add("its hediff " + name + " isn't in the player's defs"); return; }
        var st = Stage(h, severity);
        if (st == null) { a.Unsure.Add("its hediff " + S(h, "label") + " works by its mod's own code"); return; }
        if (Dict(st, "statOffsets").Count == 0 && Dict(st, "statFactors").Count == 0 && !Arr(st, "capMods").Any() && F(st, "painOffset") == 0f && F(st, "totalBleedFactor", 1f) == 1f)
            a.Unsure.Add("its hediff " + S(h, "label") + " works by its mod's own code");
        foreach (var kv in Dict(st, "statOffsets")) a.Offsets[kv.Key] = (a.Offsets.TryGetValue(kv.Key, out var o) ? o : 0f) + kv.Value;
        foreach (var kv in Dict(st, "statFactors")) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * kv.Value;
        foreach (var c in Arr(st, "capMods"))
            a.Capacities.Add((S(c, "capacity"), F(c, "offset"), F(c, "postFactor", 1f), c["setMax"] == null ? -1f : F(c, "setMax")));
        a.PainOffset += F(st, "painOffset");
        a.BleedFactor *= F(st, "totalBleedFactor", 1f);
        foreach (var c in Arr(h, "comps"))
        {
            string cls = Tail(S(c, "class"));
            if (cls != "" && cls != "HediffCompProperties_Disappears" && cls != "HediffCompProperties_RemoveIfApparelDropped" && !cls.Contains("SeverityPerDay"))
                a.Notes.Add("its hediff " + S(h, "label") + " has " + cls.Replace("HediffCompProperties_", "").Replace("HediffComp", "") + " (not counted)");
        }
    }

    /// <summary>A projectile as the weapon rating reads a shot.</summary>
    static WeaponRangedInput Shot(JsonNode verb)
    {
        var p = verb?["projectile"];
        if (p == null || !B(p, "found") || B(p, "noProjectile")) return null;
        var dd = Data["damageDefs"]?[S(p, "damageDef")];
        string cat = S(dd, "armorCategory");
        var w = new WeaponRangedInput
        {
            Damage = Math.Max(F(p, "damage"), 0f), ArmourPen = Math.Max(F(p, "armorPenetration"), 0f),
            Armour = cat == "Sharp" ? WeaponArmour.Sharp : cat == "Blunt" ? WeaponArmour.Blunt : cat == "Heat" ? WeaponArmour.Heat : WeaponArmour.None,
            HarmsHealth = dd == null || B(dd, "harmsHealth", true), StoppingPower = F(p, "stoppingPower"),
            ExplosionRadius = F(p, "explosionRadius"), ExplosionFalloff = true, ExplosionDelaySeconds = F(p, "explosionDelay") / 60f,
            ForcedMissRadius = F(verb, "forcedMissRadius"), CanGoWild = B(verb, "canGoWild", true), Burst = Math.Max((int)F(verb, "burstShotCount", 1), 1),
            TicksBetweenBurst = (int)F(verb, "ticksBetweenBurstShots", 15), Warmup = F(verb, "warmupTime"), Cooldown = Math.Max(F(verb, "defaultCooldownTime"), 1f),
            Range = F(verb, "range"), AccTouch = 1f, AccShort = 1f, AccMedium = 1f, AccLong = 1f
        };
        string dn = S(p, "damageDef");
        if (dn == "Flame" || F(p, "explosionChanceToStartFire") > 0f) w.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, Math.Max(F(p, "explosionChanceToStartFire"), dn == "Flame" ? 0.5f : 0f)));
        if (dn == "EMP") { w.HarmsHealth = false; w.Payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, w.Damage)); }
        if (dn == "Smoke" || S(p, "postExplosionGasType") == "BlindSmoke") w.HarmsHealth = false;
        return w;
    }

    static readonly string[] CodeOnly =
    {
        "VFED.CompInvisibilityEngulfer", "VFED.CompIntelExtractor", "VFED.CompProperties_Deployable", "VFEEmpire.CompResearchRefresh",
        "RimsenalSecurity.CompDefensePack", "FairycoreApparel2.QualityScaledBonusExtension", "AbilityUser.CompProperties_AbilityItem",
        "MVCF.Comps.CompProperties_VerbGiver", "VanillaGravshipExpanded2.CompProperties_ApparelVerbOwnerOxygen",
    };

    /// <summary>The facts of one extracted piece, made of the stuff given (its default when none).</summary>
    public static ApparelFacts Facts(JsonNode n, string stuff = null)
    {
        var a = new ApparelFacts
        {
            DefName = S(n, "defName"), Label = S(n, "label"), Mod = S(n, "mod"), ModName = S(n, "modName").Replace(" — ", " - "), TechLevel = S(n, "techLevel"),
            StatBases = Dict(n, "statBases"), Offsets = Dict(n, "equippedStatOffsets"),
            StuffCategories = Strs(n, "stuffCategories"), Layers = Strs(n, "layers"), Groups = Strs(n, "bodyPartGroups").Distinct().ToList(),
            Tags = Strs(n, "tags"), ThingCategories = Strs(n, "thingCategories"),
            Research = string.Join(", ", Strs(n, "research")), ResearchTech = S(n, "researchTech"),
        };
        a.MarketValue = a.StatBases.TryGetValue("MarketValue", out var mv) ? mv : 0f;
        foreach (var kv in Dict(Data, "statDefaults")) a.StatDefaults[kv.Key] = kv.Value;
        var ap = n["apparel"];
        a.CountsAsClothes = B(ap, "countsAsClothingForNudity", true);
        a.CareIfDamaged = B(ap, "careIfDamaged", true);
        a.WearPerDay = ap?["wearPerDay"] == null ? 0.4f : F(ap, "wearPerDay", 0.4f);
        a.Gender = S(ap, "gender") == "None" ? "" : S(ap, "gender");
        a.Ages = S(ap, "developmentalStageFilter") == "" ? "Adult" : S(ap, "developmentalStageFilter").Replace(" ", "");
        a.SlaveApparel = B(ap, "slaveApparel");
        a.MechanitorApparel = B(ap, "mechanitorApparel");
        a.ImmuneToToxGas = B(ap, "immuneToToxGasExposure");
        a.BlocksVision = B(ap, "blocksVision");
        a.IgnoredByNonViolent = B(ap, "ignoredByNonViolent");
        string sd = stuff ?? S(n, "stuff");
        if (sd != "")
        {
            var (label, powers, flam) = Stuff(sd);
            a.StuffDef = sd;
            a.StuffLabel = label;
            a.StuffPowers = powers;
            a.StuffFlammability = flam;
        }

        // Comps and extensions, read by the rating's own rules.
        int charges = 0;
        bool runsOut = false, berserk = false;
        string ammo = "";
        foreach (var c in Arr(n, "comps"))
        {
            string cls = S(c, "class");
            if (c["maxCharges"] != null) charges = (int)F(c, "maxCharges");
            if (S(c, "ammoDef") != "") ammo = Words(S(c, "ammoDef"));
            if (S(c, "fuelDef") != "") ammo = Words(S(c, "fuelDef"));
            if (B(c, "destroyOnEmpty")) runsOut = true;
            if (Tail(cls) == "CompTargetEffect_Berserk" || S(c, "compClass").EndsWith("CompTargetEffect_Berserk")) berserk = true;
        }
        foreach (var c in Arr(n, "comps").Concat(Arr(n, "extensions")))
        {
            string cls = S(c, "class");
            if (cls == "" && S(c, "compClass") != "") cls = S(c, "compClass");
            bool known = ApparelRateMath.ReadComp(a, cls, k => c[k] is JsonValue ? S(c, k) : "", k => c[k] is JsonArray ? Strs(c, k) : null);
            switch (cls)
            {
                case "CompProperties_CauseHediff_Apparel":
                    AddHediff(a, S(c, "hediff"));
                    break;
                case "VEF.Apparels.CompProperties_ApparelHediffs":
                    foreach (var h in Strs(c, "hediffDefnames")) AddHediff(a, h);
                    break;
                case "TorannMagic.Enchantment.CompProperties_EnchantedItem":
                    if (S(c, "hediff") != "") AddHediff(a, S(c, "hediff"), F(c, "hediffSeverity", 1f));
                    break;
                case "VEF.Apparels.ApparelExtension":
                    foreach (var kv in Dict(c, "equippedStatFactors")) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * kv.Value;
                    {
                        var tags = c["workDisables"] is JsonArray ? Strs(c, "workDisables") : new List<string> { S(c, "workDisables") };
                        tags.RemoveAll(x => x == "");
                        if (tags.Count > 0) a.WorkDisables = string.Join(" or ", tags.Select(x => x == "ManualDumb" ? "dumb manual" : x == "ManualSkilled" ? "skilled manual" : Words(x)));
                    }
                    if (c["skillGainModifier"] != null && F(c, "skillGainModifier", 1f) == 0f) a.SkillGainZero = true;
                    if (B(c, "preventBleeding")) a.PreventsBleeding = true;
                    if (c["pawnCapacityMinLevels"] != null) a.Notes.Add("holds some capacities at a minimum (VEF's own code)");
                    break;
                case "FairycoreApparel2.FemaleOnlyExtension":
                    a.Gender = "Female";
                    break;
                case "FairycoreApparel2.QualityScaledBonusExtension":
                    foreach (var kv in Dict(c, "wornStatFactors")) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * (1f + kv.Value);
                    break;
                case "VEF.Abilities.CompProperties_AbilitiesApparel":
                    break;
            }
            if (Array.IndexOf(CodeOnly, cls) >= 0)
                a.Unsure.Add(cls.StartsWith("FairycoreApparel2") ? "its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality"
                    : cls.StartsWith("MVCF") ? "its gun fires by Vanilla Expanded's own turret code"
                    : "its " + Tail(cls).Replace("CompProperties_", "").Replace("Comp", "") + " is " + ModOf(cls) + "'s own code, named, not counted");
            else if (!known && !cls.StartsWith("VEF.Abilities"))
                a.Unsure.Add("its " + Tail(cls) + " is a mod's own code, not read");
        }

        foreach (var p in a.Powers) if (p.Kind == "oxygen" && p.Ammo == "" && p.Charges > 0) p.Ammo = ammo;

        // Verbs: the powers it gives (jump, smoke, low-shield, firefoam, grenades, lances, targeters, summons).
        foreach (var v in Arr(n, "verbs"))
        {
            string vc = S(v, "verbClass");
            string projName = S(v["projectile"], "defName");
            float range = F(v, "range");
            if (vc.EndsWith("Verb_Jump") || vc.EndsWith("Verb_OxygenJump"))
            {
                float jr = a.StatBases.TryGetValue("JumpRange", out var j) ? j : a.StatBases.TryGetValue("VGE_OxygenJumpRange", out var oj) ? oj : range;
                range = jr;
            }
            float radius = a.StatBases.TryGetValue("PackRadius", out var pr) ? pr : 0f;
            var p = ApparelRateMath.PowerOfVerb(vc, S(v, "label"), charges, runsOut, range, radius, Shot(v), berserk, projName);
            if (vc.StartsWith("PokeWorld")) { p.Kind = "other"; p.Counted = false; p.Note = "throws a ball to catch a creature (PokeWorld's own code, not in the upload)"; }
            if (vc.EndsWith("Verb_OxygenJump")) { p.Charges = 10; p.Note = "a jump on its oxygen, every 50 seconds"; }
            if (!p.Counted && p.Note == "") p.Note = "its " + Tail(vc) + " is its own code";
            if (charges > 0) p.Ammo = ammo;
            if (p.Kind == "other" && !p.Counted) a.Unsure.Add("its " + (p.Label != "" ? p.Label : Tail(vc)) + " (" + Tail(vc) + ") isn't valued");
            a.Powers.Add(p);
        }
        // A turret on the shoulder (VAE Accessories' mini-turret pack): its own gun, firing with its wearer.
        foreach (var c in Arr(n, "comps"))
            if (S(c, "class") == "MVCF.Comps.CompProperties_VerbGiver")
            {
                var v = Arr(n, "verbs").FirstOrDefault();
                var shot = Shot(v);
                if (shot != null)
                {
                    shot.AccTouch = F(n["statBases"], "AccuracyTouch", 1f); shot.AccShort = F(n["statBases"], "AccuracyShort", 1f);
                    shot.AccMedium = F(n["statBases"], "AccuracyMedium", 1f); shot.AccLong = F(n["statBases"], "AccuracyLong", 1f);
                    shot.Cooldown = F(n["statBases"], "RangedWeapon_Cooldown", 1f);
                    a.Powers.RemoveAll(p => p.Kind == "grenade" && p.Shot != null);
                    a.Powers.Add(new ApparelPower { Kind = "gun", Label = "mini-turret gun", Shot = shot, Note = "fires on its own beside its wearer" });
                }
            }
        // Abilities it grants (apparel abilities, VEF's ability apparel, A RimWorld of Magic's orbs): named; a grappling hook is a jump.
        foreach (var ab in Arr(n, "abilities"))
        {
            string label = S(ab, "label") != "" ? S(ab, "label") : S(ab, "defName");
            if (S(ab, "defName").Contains("Grappling"))
                a.Powers.Add(new ApparelPower { Kind = "jump", Label = label, Range = F(ab["verb"], "range", 15f), CooldownSeconds = F(ab, "cooldownTicks") / 60f, Charges = 2, Note = "pulls its wearer across" });
            else
                a.Powers.Add(new ApparelPower { Kind = "ability", Label = label, Counted = false, Note = "its own effect" });
        }
        // Classes a mod's code gives meaning to.
        string thing = S(n, "thingClass");
        if (thing.EndsWith("ResurrectorBelt")) a.Powers.Add(new ApparelPower { Kind = "resurrect", Label = "resurrection", Charges = 1, RunsOut = true, Note = "brings its wearer back from death once (VAE Accessories)" });
        if (thing.EndsWith("Apparel_flyfire2002_MedicBag")) a.Powers.Add(new ApparelPower { Kind = "stabilize", Label = "stabilize", Note = "its wearer can stop someone's bleeding in the field (Field Medic)" });
        if (thing == "Rimatomics.Apparel_RadSuit") a.Notes.Add("Rimatomics' radiation gear: it protects by its toxic resistance (its class adds nothing)");
        if (a.DefName == "VPE_Psyring") a.Unsure.Add("a psyring carries a psycast chosen when it is made (Vanilla Psycasts Expanded)");
        if (a.DefName == "MigCorp_SkipBeacon" || a.DefName == "VME_TackleBox") a.Unsure.Add("what it does is its mod's own code (not in the upload)");
        if (a.Mod == "themoonitself.pokeworld") a.Unsure.Add("PokeWorld's own code isn't in the upload");
        if (Arr(n, "comps").Any(c => S(c, "compClass").Contains("BombardmentTargeter")))
            a.Unsure.Add("it aims the colony's gravship guns (Vanilla Gravship Expanded's own code): counted as an orbital strike");
        if (a.Mod == "co.uk.epicguru.meleeanimation")
            a.Powers.Add(new ApparelPower { Kind = "other", Label = "lasso", Counted = false, Note = "pulls an enemy to its wearer (Melee Animation's own code)" });

        // Royal titles and faiths whose apparel rules it meets; how the player's game comes by it.
        foreach (var m in Arr(n, "requirementsMet"))
        {
            if (S(m, "source") == "RoyalTitleDef") { if (!a.Titles.Any(t => t.label == S(m, "label"))) a.Titles.Add((S(m, "label"), F(m, "seniority"))); }
            else if (B(m, "required") && !a.Faiths.Contains(S(m, "label"))) a.Faiths.Add(S(m, "label"));
        }
        a.HardToGet = !B(n, "made") && !B(n, "wornByKinds");
        foreach (var p in Arr(n, "patches"))
        {
            string what = S(p, "what").Split('/')[0];
            if (what is "statBases" or "equippedStatOffsets" or "comps")
                a.Unsure.Add(S(p, "modName") + " patches its " + S(p, "what") + " (rated from before the patch)");
        }
        return a;
    }

    /// <summary>A def's name in words: VGE_OxygenCanister is "oxygen canister".</summary>
    public static string Words(string def)
    {
        string d = def.Contains('_') ? def[(def.IndexOf('_') + 1)..] : def;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < d.Length; i++)
        {
            if (i > 0 && char.IsUpper(d[i]) && !char.IsUpper(d[i - 1])) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(d[i]));
        }
        return sb.ToString();
    }

    static string ModOf(string cls) => cls.Split('.')[0] switch
    {
        "VFED" => "VFE Deserters", "VFEEmpire" => "VFE Empire", "RimsenalSecurity" => "Rimsenal Security", "AbilityUser" => "A RimWorld of Magic",
        "VanillaGravshipExpanded2" => "Vanilla Gravship Expanded", var x => x
    };
}
