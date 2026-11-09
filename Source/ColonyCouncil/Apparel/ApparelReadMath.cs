using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    // ---------------------------------------------------------------- what a piece's defs say, as plain data

    /// <summary>
    /// A comp or a mod extension of a piece: its class as written (or its comp class when it has none), its simple fields as
    /// text, its lists of names, its maps of stat to number, and every key it writes whatever its kind.
    /// </summary>
    public sealed class ApparelPartFacts
    {
        public string Class = "";
        public string CompClass = "";
        public Dictionary<string, string> Fields = new Dictionary<string, string>();
        public Dictionary<string, List<string>> Lists = new Dictionary<string, List<string>>();
        public Dictionary<string, Dictionary<string, float>> Maps = new Dictionary<string, Dictionary<string, float>>();
        public HashSet<string> Present = new HashSet<string>();

        public string S(string key) => Fields.TryGetValue(key, out var v) ? v ?? "" : "";

        public float F(string key, float def = 0f) =>
            Fields.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;

        public bool B(string key) => S(key).Trim().ToLowerInvariant() == "true";

        public bool Has(string key) => Present.Contains(key) || Fields.ContainsKey(key) || Lists.ContainsKey(key) || Maps.ContainsKey(key);

        /// <summary>A list of names, or null when it isn't a list.</summary>
        public List<string> L(string key) => Lists.TryGetValue(key, out var l) ? l : null;

        public Dictionary<string, float> M(string key) => Maps.TryGetValue(key, out var m) && m != null ? m : new Dictionary<string, float>();
    }

    /// <summary>A stuff: its label, its stuff powers (StuffPower_Armor_Sharp, StuffPower_Insulation_Cold...) and its flammability factor.</summary>
    public sealed class ApparelStuffFacts
    {
        public string Def = "", Label = "";
        public Dictionary<string, float> Powers = new Dictionary<string, float>();
        public float Flammability = 1f;
    }

    /// <summary>One stage of a hediff a piece gives while worn.</summary>
    public sealed class ApparelHediffStage
    {
        public float MinSeverity;
        public Dictionary<string, float> Offsets = new Dictionary<string, float>();
        public Dictionary<string, float> Factors = new Dictionary<string, float>();
        /// <summary>Capacities it changes (capacity, offset, post factor, held at most: -1 when not held).</summary>
        public List<(string capacity, float offset, float factor, float max)> Capacities = new List<(string, float, float, float)>();
        public float PainOffset;
        public float BleedFactor = 1f;
    }

    /// <summary>A hediff a piece gives while worn: its stages and its comps' classes as written.</summary>
    public sealed class ApparelHediffFacts
    {
        public string Name = "";
        public bool Found = true;
        public string Label = "";
        public List<ApparelHediffStage> Stages = new List<ApparelHediffStage>();
        public List<string> CompClasses = new List<string>();
    }

    /// <summary>A verb's projectile: its numbers and its damage's armour category (empty when unknown) and whether it harms health.</summary>
    public sealed class ApparelProjectileFacts
    {
        public string DefName = "";
        public bool Found = true, NoProjectile;
        public string DamageDef = "";
        public float Damage, ArmourPen, StoppingPower, ExplosionRadius, ExplosionDelayTicks, ChanceToStartFire;
        public string PostGas = "";
        public string ArmourCategory = "";
        public bool HarmsHealth = true;
    }

    /// <summary>A verb of the piece (a jump, a smokepop, a grenade belt's throw): its class, label, range and timing, and what it fires.</summary>
    public sealed class ApparelVerbFacts
    {
        public string VerbClass = "", Label = "";
        public float Range, ForcedMiss, Warmup, Cooldown;
        public int Burst = 1, TicksBetween = 15;
        public bool CanGoWild = true;
        /// <summary>Null when the verb names no projectile.</summary>
        public ApparelProjectileFacts Projectile;
    }

    /// <summary>An ability it grants: its name, label, its verb's range (null: none) and its cooldown.</summary>
    public sealed class ApparelAbilityFacts
    {
        public string DefName = "", Label = "";
        public float? VerbRange;
        public float CooldownTicks;
    }

    /// <summary>A royal title's or a faith's apparel rule the piece meets: where it comes from, its label and seniority, and whether it asks for it.</summary>
    public sealed class ApparelRuleFacts
    {
        public string Source = "", Label = "";
        public float Seniority;
        public bool Required;
    }

    /// <summary>
    /// What a piece's defs say, as plain numbers and names: filled by Tools/ApparelReview from the player's collection
    /// (Tools/apparel_extract.py) and by the game from the live def and the piece in hand (Apparel/ApparelLive.cs), so the
    /// review and the game read a piece by one set of rules (<see cref="ApparelRead"/>).
    /// </summary>
    public sealed class ApparelDefFacts
    {
        public string DefName = "", Label = "", Mod = "", ModName = "", TechLevel = "", ThingClass = "";
        public Dictionary<string, float> StatBases = new Dictionary<string, float>();
        public Dictionary<string, float> EquippedOffsets = new Dictionary<string, float>();
        public List<string> StuffCategories = new List<string>(), Layers = new List<string>(), Groups = new List<string>();
        public List<string> Tags = new List<string>(), ThingCategories = new List<string>(), Research = new List<string>();
        public string ResearchTech = "";
        /// <summary>A colonist's own value of a stat (a human's, else the stat's default), for turning a factor into an offset.</summary>
        public Dictionary<string, float> StatDefaults = new Dictionary<string, float>();

        // ---- Its apparel properties ----
        public bool CountsAsClothes = true, CareIfDamaged = true, CareIfWornByCorpse = true;
        public bool SlaveApparel, MechanitorApparel, ImmuneToToxGas, BlocksVision, IgnoredByNonViolent;
        public float WearPerDay = 0.4f;
        /// <summary>Male, Female or empty for anyone.</summary>
        public string Gender = "";
        /// <summary>Its developmental stages as the game writes them ("Adult", "Child, Adult").</summary>
        public string Ages = "Adult";

        // ---- What it does ----
        public List<ApparelPartFacts> Comps = new List<ApparelPartFacts>();
        public List<ApparelPartFacts> Extensions = new List<ApparelPartFacts>();
        public List<ApparelVerbFacts> Verbs = new List<ApparelVerbFacts>();
        public List<ApparelAbilityFacts> Abilities = new List<ApparelAbilityFacts>();
        public List<ApparelRuleFacts> Rules = new List<ApparelRuleFacts>();
        /// <summary>A recipe makes it; a faction's people wear it (with neither, it is hard to come by).</summary>
        public bool Made = true, WornByKinds = true;
        /// <summary>Patches the review doesn't apply (what they touch, the mod's name); the game's live def has them already.</summary>
        public List<(string what, string modName)> Patches = new List<(string, string)>();
        /// <summary>The hediffs by name (null for one the source doesn't hold).</summary>
        public Func<string, ApparelHediffFacts> HediffOf = n => null;

        // ---- Its stuff ----

        /// <summary>The stuff it is made of (its usual one for a kind), or empty when it takes none.</summary>
        public string Stuff = "";
        /// <summary>The stuffs it is also rated in (the common ones of its kinds), in order.</summary>
        public List<string> OtherStuffs = new List<string>();
        /// <summary>The stuffs by name (null for one the source doesn't hold).</summary>
        public Func<string, ApparelStuffFacts> StuffOf = n => null;

        // ---- The piece in hand (left as they are for a kind) ----

        /// <summary>Its quality's factor on armour and on insulation, as the game's own stats give them (normal 1).</summary>
        public float ArmourQuality = 1f, InsulationQuality = 1f;
        /// <summary>Its hit points now and at most (-1: a kind, not known).</summary>
        public float HitPoints = -1f, MaxHitPoints = -1f;
        public bool UsesHitPoints = true;
        /// <summary>Taken from the dead.</summary>
        public bool WornByCorpse;
        /// <summary>The game's thoughts it can give its wearer: below half its hit points, below a fifth, taken from the dead (label, mood; 0 when unknown).</summary>
        public (string label, float mood) Frayed, Tattered, DeadMans;
        /// <summary>The one it is biocoded to, by name, or empty.</summary>
        public string BiocodedTo = "";
    }

    /// <summary>One piece read and rated: its facts, the rating, its tags, its line, its rating in other stuffs and its table.</summary>
    public sealed class ApparelReport
    {
        public ApparelDefFacts Def;
        public ApparelFacts Facts;
        public ApparelRating Rating;
        public List<ThingTag> Tags = new List<ThingTag>();
        public string Plain = "";
        /// <summary>Its rating in each of its common stuffs, by the stuff's label.</summary>
        public Dictionary<string, float> ByStuff = new Dictionary<string, float>();
        /// <summary>Which table of the review it goes in: its role, or children.</summary>
        public string Table = "";

        /// <summary>Marked rough: part of what it does is a mod's own code, or a patch the review doesn't apply.</summary>
        public bool Rough => Facts != null && Facts.Unsure.Count > 0;

        public string StuffLabel => Facts?.StuffLabel ?? "";
    }

    /// <summary>
    /// Reads a piece's facts into the apparel rating's own facts, rates it, and gives its tags, its line and its rating in
    /// other stuffs: the review's rules (Tools/ApparelReview) and the game's (Apparel/ApparelLive.cs) are this one file, so a
    /// piece at normal quality in its usual stuff rates the same in the game as in docs/apparel/apparel.json.
    /// </summary>
    public static class ApparelRead
    {
        /// <summary>The stuff a piece is rated in, by its stuff categories in this order (devilstrand where it takes fabric).</summary>
        public static readonly (string category, string stuff)[] DefaultStuffs =
        {
            ("Fabric", "DevilstrandCloth"), ("Metallic", "Steel"), ("Leathery", "Leather_Plain"), ("Woody", "WoodLog"), ("Stony", "BlocksGranite"),
        };

        /// <summary>The common stuffs of each category it is also rated in (Tools/apparel_extract.py's own list).</summary>
        public static readonly Dictionary<string, string[]> CommonStuffs = new Dictionary<string, string[]>
        {
            ["Fabric"] = new[] { "Cloth", "Synthread", "DevilstrandCloth", "Hyperweave" },
            ["Leathery"] = new[] { "Leather_Plain", "Leather_Bear", "Leather_Thrumbo" },
            ["Metallic"] = new[] { "Steel", "Plasteel", "Uranium" },
            ["Woody"] = new[] { "WoodLog" },
            ["Stony"] = new[] { "BlocksGranite" },
        };

        /// <summary>The plain stuff a piece's role is judged in, by its categories (cloth, steel, plain leather, wood, granite).</summary>
        static readonly (string category, string stuff)[] PlainByCategory =
        {
            ("Fabric", "Cloth"), ("Metallic", "Steel"), ("Leathery", "Leather_Plain"), ("Woody", "WoodLog"), ("Stony", "BlocksGranite"),
        };

        /// <summary>The usual stuff of a piece that takes these categories (the first of <see cref="DefaultStuffs"/> that exists), or null.</summary>
        public static string UsualStuff(IList<string> categories, Func<string, bool> exists)
        {
            if (categories == null) return null;
            foreach (var (cat, s) in DefaultStuffs)
                if (categories.Contains(cat) && exists(s)) return s;
            return null;
        }

        /// <summary>The stuffs it is also rated in: each category's common stuffs that exist, the usual one first when it isn't among them.</summary>
        public static List<string> OtherStuffsFor(IList<string> categories, string usual, Func<string, bool> exists)
        {
            var list = new List<string>();
            foreach (var cat in categories ?? new List<string>())
                if (CommonStuffs.TryGetValue(cat, out var common))
                    foreach (var s in common)
                        if (exists(s) && !list.Contains(s)) list.Add(s);
            if (!string.IsNullOrEmpty(usual) && !list.Contains(usual)) list.Insert(0, usual);
            return list;
        }

        /// <summary>Comps whose work is a mod's own code: named and marked rough.</summary>
        static readonly string[] CodeOnly =
        {
            "VFED.CompInvisibilityEngulfer", "VFED.CompIntelExtractor", "VFED.CompProperties_Deployable", "VFEEmpire.CompResearchRefresh",
            "RimsenalSecurity.CompDefensePack", "FairycoreApparel2.QualityScaledBonusExtension", "AbilityUser.CompProperties_AbilityItem",
            "MVCF.Comps.CompProperties_VerbGiver", "VanillaGravshipExpanded2.CompProperties_ApparelVerbOwnerOxygen",
        };

        static string Tail(string cls) => cls.Contains('.') ? cls.Substring(cls.LastIndexOf('.') + 1) : cls;

        /// <summary>A def's name in words: VGE_OxygenCanister is "oxygen canister".</summary>
        public static string Words(string def)
        {
            if (string.IsNullOrEmpty(def)) return "";
            string d = def.Contains('_') ? def.Substring(def.IndexOf('_') + 1) : def;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < d.Length; i++)
            {
                if (i > 0 && char.IsUpper(d[i]) && !char.IsUpper(d[i - 1])) sb.Append(' ');
                sb.Append(char.ToLowerInvariant(d[i]));
            }
            return sb.ToString();
        }

        static string ModOf(string cls)
        {
            switch (cls.Split('.')[0])
            {
                case "VFED": return "VFE Deserters";
                case "VFEEmpire": return "VFE Empire";
                case "RimsenalSecurity": return "Rimsenal Security";
                case "AbilityUser": return "A RimWorld of Magic";
                case "VanillaGravshipExpanded2": return "Vanilla Gravship Expanded";
                default: return cls.Split('.')[0];
            }
        }

        // ---------------------------------------------------------------- reading

        /// <summary>A copy of the facts made of another stuff (its label, powers and flammability; none of them when the stuff is unknown).</summary>
        public static ApparelFacts WithStuff(ApparelFacts a, string def, ApparelStuffFacts s)
        {
            var b = a.Clone();
            b.StuffDef = def;
            b.StuffLabel = s?.Label ?? "";
            b.StuffPowers = s?.Powers ?? new Dictionary<string, float>();
            b.StuffFlammability = s?.Flammability ?? 1f;
            return b;
        }

        /// <summary>A hediff's stage at a severity: the last whose minimum it reaches (the first when none does).</summary>
        static ApparelHediffStage Stage(ApparelHediffFacts h, float severity)
        {
            ApparelHediffStage pick = null;
            foreach (var st in h.Stages)
                if (pick == null || st.MinSeverity <= severity) pick = st;
            return pick;
        }

        static void AddHediff(ApparelFacts a, ApparelDefFacts d, string name, float severity = 1f)
        {
            var h = string.IsNullOrEmpty(name) ? null : d.HediffOf(name);
            if (h == null || !h.Found)
            {
                a.Unsure.Add("its hediff " + name + " isn't in the player's defs");
                return;
            }
            var st = Stage(h, severity);
            if (st == null)
            {
                a.Unsure.Add("its hediff " + h.Label + " works by its mod's own code");
                return;
            }
            if (st.Offsets.Count == 0 && st.Factors.Count == 0 && st.Capacities.Count == 0 && st.PainOffset == 0f && st.BleedFactor == 1f)
                a.Unsure.Add("its hediff " + h.Label + " works by its mod's own code");
            foreach (var kv in st.Offsets) a.Offsets[kv.Key] = (a.Offsets.TryGetValue(kv.Key, out var o) ? o : 0f) + kv.Value;
            foreach (var kv in st.Factors) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * kv.Value;
            foreach (var c in st.Capacities) a.Capacities.Add(c);
            a.PainOffset += st.PainOffset;
            a.BleedFactor *= st.BleedFactor;
            foreach (var cc in h.CompClasses)
            {
                string cls = Tail(cc ?? "");
                if (cls != "" && cls != "HediffCompProperties_Disappears" && cls != "HediffCompProperties_RemoveIfApparelDropped" && !cls.Contains("SeverityPerDay"))
                    a.Notes.Add("its hediff " + h.Label + " has " + cls.Replace("HediffCompProperties_", "").Replace("HediffComp", "") + " (not counted)");
            }
        }

        /// <summary>A verb's projectile as the weapon rating reads a shot; null for none.</summary>
        static WeaponRangedInput Shot(ApparelVerbFacts verb)
        {
            var p = verb?.Projectile;
            if (p == null || !p.Found || p.NoProjectile) return null;
            string cat = p.ArmourCategory ?? "";
            var w = new WeaponRangedInput
            {
                Damage = Math.Max(p.Damage, 0f), ArmourPen = Math.Max(p.ArmourPen, 0f),
                Armour = cat == "Sharp" ? WeaponArmour.Sharp : cat == "Blunt" ? WeaponArmour.Blunt : cat == "Heat" ? WeaponArmour.Heat : WeaponArmour.None,
                HarmsHealth = p.HarmsHealth, StoppingPower = p.StoppingPower,
                ExplosionRadius = p.ExplosionRadius, ExplosionFalloff = true, ExplosionDelaySeconds = p.ExplosionDelayTicks / 60f,
                ForcedMissRadius = verb.ForcedMiss, CanGoWild = verb.CanGoWild, Burst = Math.Max(verb.Burst, 1),
                TicksBetweenBurst = verb.TicksBetween, Warmup = verb.Warmup, Cooldown = Math.Max(verb.Cooldown, 1f),
                Range = verb.Range, AccTouch = 1f, AccShort = 1f, AccMedium = 1f, AccLong = 1f
            };
            string dn = p.DamageDef ?? "";
            if (dn == "Flame" || p.ChanceToStartFire > 0f) w.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, Math.Max(p.ChanceToStartFire, dn == "Flame" ? 0.5f : 0f)));
            if (dn == "EMP")
            {
                w.HarmsHealth = false;
                w.Payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, w.Damage));
            }
            if (dn == "Smoke" || p.PostGas == "BlindSmoke") w.HarmsHealth = false;
            return w;
        }

        /// <summary>
        /// The rating's facts of a piece made of a stuff (none: as made): its stats, its stuff, its coverage, its comps,
        /// extensions, verbs, abilities and hediffs read by the rating's own <see cref="ApparelRateMath.ReadComp"/> and
        /// <see cref="ApparelRateMath.PowerOfVerb"/>; what can't be read named and marked rough; the piece in hand's quality,
        /// moods and biocoding.
        /// </summary>
        public static ApparelFacts Facts(ApparelDefFacts d, ApparelStuffFacts stuff)
        {
            var a = new ApparelFacts
            {
                DefName = d.DefName, Label = d.Label, Mod = d.Mod, ModName = (d.ModName ?? "").Replace(" — ", " - "), TechLevel = d.TechLevel,
                StatBases = new Dictionary<string, float>(d.StatBases), Offsets = new Dictionary<string, float>(d.EquippedOffsets),
                StuffCategories = d.StuffCategories.ToList(), Layers = d.Layers.ToList(), Groups = d.Groups.Distinct().ToList(),
                Tags = d.Tags.ToList(), ThingCategories = d.ThingCategories.ToList(),
                Research = string.Join(", ", d.Research), ResearchTech = d.ResearchTech,
            };
            a.MarketValue = a.StatBases.TryGetValue("MarketValue", out var mv) ? mv : 0f;
            foreach (var kv in d.StatDefaults) a.StatDefaults[kv.Key] = kv.Value;
            a.CountsAsClothes = d.CountsAsClothes;
            a.CareIfDamaged = d.CareIfDamaged;
            a.WearPerDay = d.WearPerDay;
            a.Gender = d.Gender == "None" ? "" : d.Gender ?? "";
            a.Ages = string.IsNullOrEmpty(d.Ages) ? "Adult" : d.Ages.Replace(" ", "");
            a.SlaveApparel = d.SlaveApparel;
            a.MechanitorApparel = d.MechanitorApparel;
            a.ImmuneToToxGas = d.ImmuneToToxGas;
            a.BlocksVision = d.BlocksVision;
            a.IgnoredByNonViolent = d.IgnoredByNonViolent;
            if (stuff != null && !string.IsNullOrEmpty(stuff.Def))
            {
                a.StuffDef = stuff.Def;
                a.StuffLabel = stuff.Label ?? "";
                a.StuffPowers = stuff.Powers ?? new Dictionary<string, float>();
                a.StuffFlammability = stuff.Flammability;
            }

            // Comps and extensions, read by the rating's own rules.
            int charges = 0;
            bool runsOut = false, berserk = false;
            string ammo = "";
            foreach (var c in d.Comps)
            {
                if (c.Has("maxCharges")) charges = (int)c.F("maxCharges");
                if (c.S("ammoDef") != "") ammo = Words(c.S("ammoDef"));
                if (c.S("fuelDef") != "") ammo = Words(c.S("fuelDef"));
                if (c.B("destroyOnEmpty")) runsOut = true;
                if (Tail(c.Class) == "CompTargetEffect_Berserk" || (c.CompClass ?? "").EndsWith("CompTargetEffect_Berserk")) berserk = true;
            }
            foreach (var c in d.Comps.Concat(d.Extensions))
            {
                string cls = c.Class ?? "";
                if (cls == "" && !string.IsNullOrEmpty(c.CompClass)) cls = c.CompClass;
                var part = c;
                bool known = ApparelRateMath.ReadComp(a, cls, k => part.S(k), k => part.L(k));
                switch (cls)
                {
                    case "CompProperties_CauseHediff_Apparel":
                        AddHediff(a, d, c.S("hediff"));
                        break;
                    case "VEF.Apparels.CompProperties_ApparelHediffs":
                        foreach (var h in c.L("hediffDefnames") ?? new List<string>()) AddHediff(a, d, h);
                        break;
                    case "TorannMagic.Enchantment.CompProperties_EnchantedItem":
                        if (c.S("hediff") != "") AddHediff(a, d, c.S("hediff"), c.F("hediffSeverity", 1f));
                        break;
                    case "VEF.Apparels.ApparelExtension":
                        foreach (var kv in c.M("equippedStatFactors")) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * kv.Value;
                        {
                            var tags = c.L("workDisables") is List<string> wl ? wl.ToList() : new List<string> { c.S("workDisables") };
                            tags.RemoveAll(x => x == "");
                            if (tags.Count > 0)
                                a.WorkDisables = string.Join(" or ", tags.Select(x => x == "ManualDumb" ? "dumb manual" : x == "ManualSkilled" ? "skilled manual" : Words(x)));
                        }
                        if (c.Has("skillGainModifier") && c.F("skillGainModifier", 1f) == 0f) a.SkillGainZero = true;
                        if (c.B("preventBleeding")) a.PreventsBleeding = true;
                        if (c.Has("pawnCapacityMinLevels")) a.Notes.Add("holds some capacities at a minimum (VEF's own code)");
                        break;
                    case "FairycoreApparel2.FemaleOnlyExtension":
                        a.Gender = "Female";
                        break;
                    case "FairycoreApparel2.QualityScaledBonusExtension":
                        foreach (var kv in c.M("wornStatFactors")) a.Factors[kv.Key] = (a.Factors.TryGetValue(kv.Key, out var o) ? o : 1f) * (1f + kv.Value);
                        break;
                }
                if (Array.IndexOf(CodeOnly, cls) >= 0)
                    a.Unsure.Add(cls.StartsWith("FairycoreApparel2") ? "its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality"
                        : cls.StartsWith("MVCF") ? "its gun fires by Vanilla Expanded's own turret code"
                        : "its " + Tail(cls).Replace("CompProperties_", "").Replace("Comp", "") + " is " + ModOf(cls) + "'s own code, named, not counted");
                else if (!known && !cls.StartsWith("VEF.Abilities"))
                    a.Unsure.Add("its " + Tail(cls) + " is a mod's own code, not read");
            }

            foreach (var p in a.Powers)
                if (p.Kind == "oxygen" && p.Ammo == "" && p.Charges > 0) p.Ammo = ammo;

            // Verbs: the powers it gives (jump, smoke, low-shield, firefoam, grenades, lances, targeters, summons).
            foreach (var v in d.Verbs)
            {
                string vc = v.VerbClass ?? "";
                string projName = v.Projectile?.DefName ?? "";
                float range = v.Range;
                if (vc.EndsWith("Verb_Jump") || vc.EndsWith("Verb_OxygenJump"))
                {
                    float jr = a.StatBases.TryGetValue("JumpRange", out var j) ? j : a.StatBases.TryGetValue("VGE_OxygenJumpRange", out var oj) ? oj : range;
                    range = jr;
                }
                float radius = a.StatBases.TryGetValue("PackRadius", out var pr) ? pr : 0f;
                var p = ApparelRateMath.PowerOfVerb(vc, v.Label, charges, runsOut, range, radius, Shot(v), berserk, projName);
                if (vc.StartsWith("PokeWorld"))
                {
                    p.Kind = "other";
                    p.Counted = false;
                    p.Note = "throws a ball to catch a creature (PokeWorld's own code, not in the upload)";
                }
                if (vc.EndsWith("Verb_OxygenJump"))
                {
                    p.Charges = 10;
                    p.Note = "a jump on its oxygen, every 50 seconds";
                }
                if (!p.Counted && p.Note == "") p.Note = "its " + Tail(vc) + " is its own code";
                if (charges > 0) p.Ammo = ammo;
                if (p.Kind == "other" && !p.Counted) a.Unsure.Add("its " + (p.Label != "" ? p.Label : Tail(vc)) + " (" + Tail(vc) + ") isn't valued");
                a.Powers.Add(p);
            }
            // A turret on the shoulder (VAE Accessories' mini-turret pack): its own gun, firing with its wearer.
            foreach (var c in d.Comps)
                if (c.Class == "MVCF.Comps.CompProperties_VerbGiver")
                {
                    var shot = Shot(d.Verbs.FirstOrDefault());
                    if (shot != null)
                    {
                        shot.AccTouch = ApparelRateMath.Stat(d.StatBases, "AccuracyTouch", 1f);
                        shot.AccShort = ApparelRateMath.Stat(d.StatBases, "AccuracyShort", 1f);
                        shot.AccMedium = ApparelRateMath.Stat(d.StatBases, "AccuracyMedium", 1f);
                        shot.AccLong = ApparelRateMath.Stat(d.StatBases, "AccuracyLong", 1f);
                        shot.Cooldown = ApparelRateMath.Stat(d.StatBases, "RangedWeapon_Cooldown", 1f);
                        a.Powers.RemoveAll(p => p.Kind == "grenade" && p.Shot != null);
                        a.Powers.Add(new ApparelPower { Kind = "gun", Label = "mini-turret gun", Shot = shot, Note = "fires on its own beside its wearer" });
                    }
                }
            // Abilities it grants (apparel abilities, VEF's ability apparel, A RimWorld of Magic's orbs): named; a grappling hook is a jump.
            foreach (var ab in d.Abilities)
            {
                string label = !string.IsNullOrEmpty(ab.Label) ? ab.Label : ab.DefName;
                if ((ab.DefName ?? "").Contains("Grappling"))
                    a.Powers.Add(new ApparelPower { Kind = "jump", Label = label, Range = ab.VerbRange ?? 15f, CooldownSeconds = ab.CooldownTicks / 60f, Charges = 2, Note = "pulls its wearer across" });
                else
                    a.Powers.Add(new ApparelPower { Kind = "ability", Label = label, Counted = false, Note = "its own effect" });
            }
            // Classes a mod's code gives meaning to.
            string thing = d.ThingClass ?? "";
            if (thing.EndsWith("ResurrectorBelt")) a.Powers.Add(new ApparelPower { Kind = "resurrect", Label = "resurrection", Charges = 1, RunsOut = true, Note = "brings its wearer back from death once (VAE Accessories)" });
            if (thing.EndsWith("Apparel_flyfire2002_MedicBag")) a.Powers.Add(new ApparelPower { Kind = "stabilize", Label = "stabilize", Note = "its wearer can stop someone's bleeding in the field (Field Medic)" });
            if (thing == "Rimatomics.Apparel_RadSuit") a.Notes.Add("Rimatomics' radiation gear: it protects by its toxic resistance (its class adds nothing)");
            if (a.DefName == "VPE_Psyring") a.Unsure.Add("a psyring carries a psycast chosen when it is made (Vanilla Psycasts Expanded)");
            if (a.DefName == "MigCorp_SkipBeacon" || a.DefName == "VME_TackleBox") a.Unsure.Add("what it does is its mod's own code (not in the upload)");
            if (a.Mod == "themoonitself.pokeworld") a.Unsure.Add("PokeWorld's own code isn't in the upload");
            if (d.Comps.Any(c => (c.CompClass ?? "").Contains("BombardmentTargeter")))
                a.Unsure.Add("it aims the colony's gravship guns (Vanilla Gravship Expanded's own code): counted as an orbital strike");
            if (a.Mod == "co.uk.epicguru.meleeanimation")
                a.Powers.Add(new ApparelPower { Kind = "other", Label = "lasso", Counted = false, Note = "pulls an enemy to its wearer (Melee Animation's own code)" });

            // Royal titles and faiths whose apparel rules it meets; how the colony comes by it.
            foreach (var m in d.Rules)
            {
                if (m.Source == "RoyalTitleDef")
                {
                    if (!a.Titles.Any(t => t.label == m.Label)) a.Titles.Add((m.Label, m.Seniority));
                }
                else if (m.Required && !a.Faiths.Contains(m.Label)) a.Faiths.Add(m.Label);
            }
            a.HardToGet = !d.Made && !d.WornByKinds;
            foreach (var (what, modName) in d.Patches)
            {
                string part = (what ?? "").Split('/')[0];
                if (part == "statBases" || part == "equippedStatOffsets" || part == "comps")
                    a.Unsure.Add(modName + " patches its " + what + " (rated from before the patch)");
            }

            // The piece in hand: its quality, the moods it gives as it is, whom it is coded to.
            a.ArmourQuality = d.ArmourQuality;
            a.InsulationQuality = d.InsulationQuality;
            int stage = DamageStage(d);
            if (stage == 0 && d.Frayed.mood != 0f) a.Moods.Add(d.Frayed);
            if (stage == 1 && d.Tattered.mood != 0f) a.Moods.Add(d.Tattered);
            if (d.WornByCorpse && d.CareIfWornByCorpse && d.DeadMans.mood != 0f) a.Moods.Add(d.DeadMans);
            a.BiocodedTo = d.BiocodedTo ?? "";
            return a;
        }

        // ---------------------------------------------------------------- wear

        /// <summary>The game's thresholds for the apparel-damaged mood (ThoughtWorker_ApparelDamaged): below half its hit points, and below a fifth.</summary>
        public const float FrayedBelow = 0.5f, TatteredBelow = 0.2f;

        /// <summary>The share of its hit points left, or -1 for a kind.</summary>
        public static float HitPointShare(ApparelDefFacts d) =>
            d == null || d.MaxHitPoints <= 0f || d.HitPoints < 0f ? -1f : Math.Max(0f, Math.Min(1f, d.HitPoints / d.MaxHitPoints));

        /// <summary>
        /// The stage of the apparel-damaged mood the piece gives its wearer as it is: -1 none, 0 below half its hit points,
        /// 1 below a fifth; only for a piece that wears and whose wearer cares (the game's careIfDamaged).
        /// </summary>
        public static int DamageStage(ApparelDefFacts d)
        {
            if (d == null || !d.UsesHitPoints || !d.CareIfDamaged) return -1;
            float s = HitPointShare(d);
            if (s < 0f) return -1;
            return s < TatteredBelow ? 1 : s < FrayedBelow ? 0 : -1;
        }

        /// <summary>The days of wear it takes to bring the piece from its hit points now to a share of its most (null: it doesn't wear, or is already below).</summary>
        public static float? DaysOfWearTo(ApparelDefFacts d, float share)
        {
            if (d == null || !d.UsesHitPoints) return null;
            float max = d.MaxHitPoints > 0f ? d.MaxHitPoints : ApparelRateMath.Stat(d.StatBases, "MaxHitPoints", 100f);
            return DaysOfWear(d.HitPoints >= 0f ? d.HitPoints : max, max, d.WearPerDay, share);
        }

        /// <summary>The days of wear from hit points now to a share of the most (null: it doesn't wear, or is already below).</summary>
        public static float? DaysOfWear(float hitPoints, float maxHitPoints, float wearPerDay, float share)
        {
            if (wearPerDay <= 0f || maxHitPoints <= 0f) return null;
            float left = hitPoints - maxHitPoints * share;
            return left <= 0f ? (float?)null : left / wearPerDay;
        }

        // ---------------------------------------------------------------- the whole of it

        /// <summary>The plain stuff its role is judged in (cloth, steel, plain leather, wood, granite by its categories), or null.</summary>
        public static string PlainStuff(ApparelDefFacts d)
        {
            foreach (var (cat, s) in PlainByCategory)
                if (d.StuffCategories.Contains(cat) && d.StuffOf(s) != null) return s;
            return null;
        }

        /// <summary>
        /// The whole of it: read in its stuff, rated with its role judged in plain stuff, its rating in its other stuffs, its
        /// tags, its line and its table.
        /// </summary>
        public static ApparelReport Full(ApparelDefFacts d)
        {
            var rep = new ApparelReport { Def = d };
            var stuff = string.IsNullOrEmpty(d.Stuff) ? null : d.StuffOf(d.Stuff) ?? new ApparelStuffFacts { Def = d.Stuff };
            var a = Facts(d, stuff);
            rep.Facts = a;
            string plainDef = a.StuffDef != "" ? PlainStuff(d) ?? a.StuffDef : null;
            var r = ApparelRateMath.Rate(a, plainDef == null ? null : WithStuff(a, plainDef, d.StuffOf(plainDef)));
            rep.Rating = r;
            foreach (var s in d.OtherStuffs)
            {
                var b = WithStuff(a, s, d.StuffOf(s));
                rep.ByStuff[b.StuffLabel] = ApparelRateMath.Rate(b, r.Role).Overall;
            }
            rep.Tags = ApparelTagMath.For(r, a, byStuff: rep.ByStuff);
            rep.Plain = ApparelTagMath.Line(r, rep.Tags);
            rep.Table = a.Ages == "Child" ? "children" : r.Role;
            return rep;
        }
    }
}
