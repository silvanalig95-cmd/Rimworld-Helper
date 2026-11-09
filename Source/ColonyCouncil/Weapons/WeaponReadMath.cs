using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    // ---------------------------------------------------------------- what a weapon's defs say, as plain data

    /// <summary>A damage def's numbers (armour category, worker, defaults, extra hediffs). A field the def doesn't hold is null.</summary>
    public sealed class WeaponDamageFacts
    {
        public string Name = "";
        /// <summary>False for a damage the collection names but doesn't hold.</summary>
        public bool Found = true;
        public string Label = "";
        public string ArmourCategory = "";
        /// <summary>The worker's class as written ("DamageWorker_Cut", a mod's "Namespace.DamageWorker_X"; "" for the plain one).</summary>
        public string Worker = "";
        public float? DefaultDamage, DefaultArmourPen, DefaultStoppingPower;
        public bool? HarmsHealth;
        public List<WeaponHediffFacts> Hediffs = new List<WeaponHediffFacts>();
    }

    public sealed class WeaponHediffFacts
    {
        public string Hediff = "";
        public float PerDamage, Severity;
    }

    /// <summary>An extra damage on a projectile or a melee tool (or a unique trait's).</summary>
    public sealed class WeaponExtraFacts
    {
        public string Def = "";
        public float Amount;
        public float ArmourPen = -1f;
        public float Chance = 1f;
        /// <summary>A melee tool's surprise attack only.</summary>
        public bool Surprise;
    }

    /// <summary>A projectile's numbers, by the game's own rules for its damage, armour piercing and stopping power.</summary>
    public sealed class WeaponProjectileFacts
    {
        public string DefName = "";
        public bool Found = true;
        /// <summary>A def with no projectile part.</summary>
        public bool NoProjectile;
        /// <summary>Its class as written ("Bullet", "Projectile_Explosive", a mod's "Namespace.Class").</summary>
        public string ThingClass = "";
        public string DamageDef = "";
        public float Damage, ArmourPen, StoppingPower = 0.5f, ExplosionRadius, ExplosionDelayTicks, ChanceToStartFire, ShotCount = 1f;
        public bool Falloff, DealsDamageOnce;
        public string PreSpawn = "", PostSpawn = "", PostGas = "";
        public float? LifeTime, TickDamageRate;
        /// <summary>Vanilla Expanded Framework's stream or slug (an ExpandableProjectileDef).</summary>
        public bool Stream;
        public List<WeaponExtraFacts> Extras = new List<WeaponExtraFacts>();
        /// <summary>Its mod extensions' class names (the last part).</summary>
        public List<string> ExtensionClasses = new List<string>();
    }

    /// <summary>A verb: its class, timing, range, scatter and what it fires (a projectile or a beam).</summary>
    public sealed class WeaponVerbFacts
    {
        public string VerbClass = "";
        public float Range = 1.42f, MinRange, Warmup, ForcedMiss, BeamTotalDamage, BeamFireChance, BeamWidth = 1f, FuelPerShot;
        public int Burst = 1, TicksBetween = 15;
        public bool CanGoWild = true, BeamNeighbours;
        public string BeamDamageDef = "";
        /// <summary>Null when the verb names no projectile.</summary>
        public WeaponProjectileFacts Projectile;

        /// <summary>It fires something: a projectile or a beam.</summary>
        public bool Fires => Projectile != null || !string.IsNullOrEmpty(BeamDamageDef);
    }

    /// <summary>One capacity of a melee tool, with the maneuver's verb and damage.</summary>
    public sealed class WeaponCapacityFacts
    {
        public string Capacity = "";
        public string DamageDef = "";
        public string VerbClass = "";
        /// <summary>False when no maneuver has this capacity.</summary>
        public bool Known = true;
    }

    public sealed class WeaponToolFacts
    {
        public string Label = "";
        public float Power, Cooldown = 2f, ArmourPen = -1f, ChanceFactor = 1f;
        public List<WeaponCapacityFacts> Capacities = new List<WeaponCapacityFacts>();
        public List<WeaponExtraFacts> Extras = new List<WeaponExtraFacts>();
    }

    public sealed class WeaponAbilityCompFacts
    {
        public string Class = "";
        public float? DamAmount, LineWidthEnd;
        public WeaponProjectileFacts Projectile;
    }

    /// <summary>An ability the weapon grants: its verb (if any), comps, cooldown and charges.</summary>
    public sealed class WeaponAbilityFacts
    {
        public string DefName = "", Label = "";
        public bool Found = true;
        public int Charges;
        public float CooldownTicks;
        public WeaponVerbFacts Verb;
        public List<WeaponAbilityCompFacts> Comps = new List<WeaponAbilityCompFacts>();
    }

    /// <summary>A comp or a mod extension: its class (the last part of its name), its simple fields as text, its lists of names.</summary>
    public sealed class WeaponPartFacts
    {
        public string Class = "";
        public Dictionary<string, string> Fields = new Dictionary<string, string>();
        public Dictionary<string, List<string>> Lists = new Dictionary<string, List<string>>();

        public string S(string key) => Fields.TryGetValue(key, out var v) ? v ?? "" : "";

        public float F(string key, float def = 0f) =>
            Fields.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;

        public bool B(string key) => S(key).ToLowerInvariant() == "true";

        public List<string> L(string key) => Lists.TryGetValue(key, out var l) && l != null ? l : new List<string>();
    }

    /// <summary>
    /// A trait of the weapon in hand: an Odyssey unique weapon's (WeaponTraitDef on CompUniqueWeapon) or a persona weapon's
    /// (CompBladelinkWeapon). Its changes to the weapon's own stats are already in the weapon's stat values; these are the
    /// rest: the burst, the stopping power, the damage it turns the shot into, extra damage, what it does to the wielder.
    /// </summary>
    public sealed class WeaponTraitFacts
    {
        public string Label = "";
        public float BurstCountFactor = 1f, BurstSpeedFactor = 1f, StoppingPower;
        public string DamageOverride = "";
        public List<WeaponExtraFacts> Extras = new List<WeaponExtraFacts>();
        /// <summary>What it changes on whoever holds it (equippedStatOffsets).</summary>
        public List<WeaponPassive> Equipped = new List<WeaponPassive>();
        /// <summary>It works on moods, needs or the body (a thought, a hediff, a kill thirst), named and not counted.</summary>
        public bool Moods;
        /// <summary>Words for the weapon stats it changes (accuracy, cooldown), already counted in the stat values.</summary>
        public List<string> StatWords = new List<string>();
    }

    /// <summary>
    /// What a weapon's defs say, as plain numbers and names: filled by Tools/WeaponReview from the player's collection
    /// (Tools/weapon_extract.py) and by the game from the live def and the weapon in hand (Weapons/WeaponLive.cs), so the
    /// review and the game read a weapon by one set of rules (<see cref="WeaponRead"/>).
    /// </summary>
    public sealed class WeaponDefFacts
    {
        public string DefName = "", Label = "", Mod = "", ModName = "", Tech = "";
        public float Mass = 1f, Market;
        /// <summary>Why a colonist can't wield it (a turret's gun, a mechanoid's), or empty.</summary>
        public string LeftOut = "";
        /// <summary>
        /// The weapon's own stat values its numbers are read with: AccuracyTouch to AccuracyLong, RangedWeapon_Cooldown and the
        /// RangedWeapon_ multipliers (warm-up, range, damage, armour piercing). The review: its stat bases times its
        /// stuff's factors; the game: the weapon's own values (quality, stuff, unique traits and patches in them).
        /// </summary>
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        /// <summary>Melee: made of stuff (or with a cooldown factor of its own), the stuff's sharp and blunt factors, the cooldown and damage factors.</summary>
        public bool Stuffed;
        public string StuffLabel;
        public float SharpFactor = 1f, BluntFactor = 1f, MeleeCooldownFactor = 1f, MeleeDamageFactor = 1f;
        public List<KeyValuePair<string, float>> EquippedOffsets = new List<KeyValuePair<string, float>>();
        public List<WeaponVerbFacts> Verbs = new List<WeaponVerbFacts>();
        public List<WeaponToolFacts> Tools = new List<WeaponToolFacts>();
        public List<WeaponAbilityFacts> Abilities = new List<WeaponAbilityFacts>();
        public List<WeaponPartFacts> Comps = new List<WeaponPartFacts>();
        /// <summary>Every comp's class name, as the comps list writes them.</summary>
        public List<string> CompClasses = new List<string>();
        public List<WeaponPartFacts> Extensions = new List<WeaponPartFacts>();
        public List<string> WeaponTags = new List<string>();
        public List<string> Research = new List<string>();
        public string ResearchTech = "";
        /// <summary>A recipe makes it; a faction's people carry it (with neither, it is hard to find).</summary>
        public bool Made = true, CarriedByKinds = true;
        /// <summary>Lines about patches the review doesn't apply (the game's live def has them already).</summary>
        public List<string> PatchNotes = new List<string>();
        /// <summary>The damage defs by name (null for one the source doesn't hold).</summary>
        public Func<string, WeaponDamageFacts> DamageOf = n => null;

        // ---- The weapon in hand (left empty for a def) ----

        /// <summary>A unique weapon's traits, when the weapon in hand is known (null: rated as the plain weapon).</summary>
        public List<WeaponTraitFacts> UniqueTraits;
        /// <summary>A persona weapon's traits, when known.</summary>
        public List<WeaponTraitFacts> PersonaTraits;
        /// <summary>The one it is biocoded or bonded to, by name, or empty.</summary>
        public string BiocodedTo = "";
    }

    /// <summary>One weapon read and rated: the rating's inputs, what can only be named, the rating, its tags and its line.</summary>
    public sealed class WeaponReport
    {
        public WeaponDefFacts Def;
        /// <summary>Where it was read from (the review keeps its JSON node here).</summary>
        public object Source;
        public string DefName, Label, Mod, ModName, Tech;
        public float Mass, Market;
        /// <summary>ranged, melee, left out, unrated.</summary>
        public string Kind;
        /// <summary>Which table of the review it goes in.</summary>
        public string Table;
        public WeaponRangedInput Ranged;
        /// <summary>Its other firing modes (a gauss rifle's high-power shot, a multi-verb weapon's second verb): rated too, the best kept.</summary>
        public List<WeaponRangedInput> Alternates = new List<WeaponRangedInput>();
        public WeaponMeleeInput Melee;
        public List<WeaponPassive> Passives = new List<WeaponPassive>();
        public List<WeaponAbilityInput> Abilities = new List<WeaponAbilityInput>();
        public List<WeaponSpecial> Named = new List<WeaponSpecial>();
        public List<string> Unsure = new List<string>();
        public WeaponRating Rating;
        public WeaponRating MeleeRating;
        public string Plain;
        public string StuffLabel;
        /// <summary>Why it isn't rated though a colonist can wield it (its attack is wholly a mod's own code).</summary>
        public string Unrated;
        public Dictionary<string, float> BySkill = new Dictionary<string, float>();
        public Dictionary<string, float> ByTrait = new Dictionary<string, float>();
        public List<ThingTag> Tags = new List<ThingTag>();
        public WeaponTagFacts TagFacts = new WeaponTagFacts();

        public bool Rated => Rating != null && (Kind == "ranged" || Kind == "melee");

        /// <summary>Marked rough: part of what it does is a mod's own code, a rolled trait or a patch.</summary>
        public bool Rough => Unsure.Count > 0;
    }

    /// <summary>
    /// Reads a weapon's facts into the weapon rating's inputs, rates it and gives its tags and its line: the review's rules
    /// (Tools/WeaponReview) and the game's (Weapons/WeaponLive.cs) are this one file, so a weapon at normal quality in steel
    /// rates the same in the game as in docs/weapons/weapons.json. What a mod's own code does is valued where its code was
    /// read (docs/weapons/README.md, "What the mods' own code does"); the rest is named and marks the rating rough.
    /// </summary>
    public static class WeaponRead
    {
        /// <summary>
        /// Verbs that shoot as the game's own do, the game's and the ones the mods' code was read for (Vanilla Expanded
        /// Framework's shot and makeshift shot, VFE Medieval's matchlock shot, A RimWorld of Magic's mage-only shot, the
        /// Fairycore wands' channelled shot, VWE Bioferrite's heavy incinerator and ability shots).
        /// </summary>
        static readonly HashSet<string> KnownVerbs = new HashSet<string>
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
        static readonly HashSet<string> KnownProjectiles = new HashSet<string>
        {
            "Bullet", "Projectile_Explosive", "Beam", "Projectile_Liquid", "Projectile_DoomsdayRocket", "Projectile_SpawnsThing",
            "VEF.Weapons.LaserBeam", "FireArrow", "VFETribals.ThrowspikesThrown", "VFEMedieval.WarbowArrow", "VFEMedieval.MatchlockProjectile",
            "VFE_Settlers.Comps.DynamiteComp", "TorannMagic.Weapon.Projectile_BlazingPower", "TorannMagic.Weapon.Projectile_FireWand",
            "TorannMagic.Weapon.Projectile_IceWand", "TorannMagic.Weapon.Projectile_LightningWand", "FairycorePixieWands.Projectile_WandOrb",
            "VanillaQuestsExpandedCryptoforge.Bullet_Cryptobolter", "VEF.Weapons.Projectile_SmokeGrenade", "VEF.Weapons.Projectile_TopAttackMissile"
        };

        /// <summary>Verbs whose attack is wholly their own code (what they launch and how often): not rated.</summary>
        static readonly HashSet<string> OwnAttackVerbs = new HashSet<string> { "TorannMagic.Weapon.Verb_FreezingWinds" };

        static readonly HashSet<string> VanillaWorkers = new HashSet<string>
        {
            "", "DamageWorker_AddInjury", "DamageWorker_Cut", "DamageWorker_Stab", "DamageWorker_Blunt", "DamageWorker_Bite",
            "DamageWorker_Scratch", "DamageWorker_Flame", "DamageWorker_Stun", "DamageWorker_Extinguish", "DamageWorker_Vaporize",
            "DamageWorker_Nerve"
        };

        /// <summary>Comps that only change looks, names, quality or handling: nothing to say about a fight.</summary>
        static readonly HashSet<string> QuietComps = new HashSet<string>
        {
            "CompProperties_Forbiddable", "CompEquippable", "CompProperties_Styleable", "CompProperties_Art", "CompQuality",
            "CompProperties_GeneratedName", "CompOversizedWeapon", "CompProperties_GraphicCustomization",
            "CompGraphicCustomization_PsychicWeapon", "CompProperties_EditHeraldic", "CompProperties_Biocodable",
            "CompProperties_BladelinkWeapon", "CompProperties_UniqueWeapon", "CompProperties_EquippableAbilityReloadable",
            "CompProperties_EquippableAbility", "CompProperties_Explosive", "CompProperties_WeaponDeteriorable",
            "CompProperties_LaserCapacitor", "CompProperties_MultiVerbWeapon", "CompProperties_ChangeableProjectile",
            "CompProperties_EnchantedItem", "CompProperties_Deflector", "CompBPReflector", "CompEquippable_Wand"
        };

        /// <summary>The last part of a dotted name.</summary>
        public static string Last(string s) => string.IsNullOrEmpty(s) ? "" : s.Split('.').Last();

        static WeaponSpecial Special(string key, string label, bool counted, int sign = 1) =>
            new WeaponSpecial { Key = key, Label = label, Counted = counted, Sign = sign };

        static float Stat(WeaponDefFacts w, string stat, float def = 1f) => w.Stats.TryGetValue(stat, out var v) ? v : def;

        static WeaponArmour ArmourOf(WeaponDefFacts w, string damageDef)
        {
            switch (w.DamageOf(damageDef)?.ArmourCategory ?? "")
            {
                case "Sharp": return WeaponArmour.Sharp;
                case "Blunt": return WeaponArmour.Blunt;
                case "Heat": return WeaponArmour.Heat;
                default: return WeaponArmour.None;
            }
        }

        static string DamageWords(WeaponDefFacts w, string name)
        {
            string l = w.DamageOf(name)?.Label ?? "";
            return string.IsNullOrEmpty(l) ? name : l;
        }

        static WeaponDamageFacts Dmg(WeaponDefFacts w, string name) => string.IsNullOrEmpty(name) ? null : w.DamageOf(name);

        // ---------------------------------------------------------------- reading

        /// <summary>A weapon's facts into the rating's inputs, its named specials and what is less sure.</summary>
        public static WeaponReport Read(WeaponDefFacts w)
        {
            var r = new WeaponReport
            {
                Def = w, DefName = w.DefName, Label = w.Label, Mod = w.Mod, ModName = w.ModName, Tech = w.Tech, Mass = w.Mass, Market = w.Market
            };
            if (!string.IsNullOrEmpty(w.LeftOut))
            {
                r.Kind = "left out";
                return r;
            }
            foreach (var kv in w.EquippedOffsets)
                r.Passives.Add(new WeaponPassive(kv.Key, kv.Value));
            var verb = w.Verbs.FirstOrDefault(v => v.Fires);
            bool nonLethal = w.Mod == "vanillaexpanded.vwenl";
            if (verb != null)
            {
                r.Kind = "ranged";
                r.Ranged = RangedFrom(w, verb, r, nonLethal);
                foreach (var other in w.Verbs.Where(v => v != verb && v.Fires))
                    r.Alternates.Add(RangedFrom(w, other, new WeaponReport { Mod = r.Mod }, nonLethal));
                if (r.Alternates.Count > 0)
                    r.Named.Add(Special("modes", "more than one way to fire: rated in its best", true, 0));
            }
            else if (w.Tools.Any())
            {
                r.Kind = "melee";
            }
            else
            {
                r.Kind = "left out";
                return r;
            }
            if (w.Tools.Any())
                r.Melee = MeleeFrom(w, r, nonLethal);
            foreach (var a in w.Abilities)
                r.Abilities.Add(AbilityFrom(w, a, r));
            NameTheRest(w, r);
            return r;
        }

        /// <summary>The main verb and its projectile, with the weapon's own stat values (normal quality changes nothing).</summary>
        static WeaponRangedInput RangedFrom(WeaponDefFacts w, WeaponVerbFacts v, WeaponReport r, bool nonLethal)
        {
            string vc = v.VerbClass ?? "";
            if (OwnAttackVerbs.Contains(vc))
                r.Unrated = "its attack is its own code (" + Last(vc) + ": what it launches and how often), so it isn't rated";
            if (!KnownVerbs.Contains(vc))
                r.Unsure.Add("fires by its own rule (" + Last(vc) + "), read as an ordinary shot");
            else if (vc.Contains("Verb_BlazingPower"))
                r.Named.Add(Special("mage-only", "only a mage of A RimWorld of Magic can fire it (anyone else's shot fails); rated for a mage", false, -1));
            else if (vc.Contains("Verb_ShootWand"))
                r.Named.Add(Special("channel", "only someone who can channel the wand can fire it", false, -1));
            else if (vc.Contains("Verb_ShootWithSmoke"))
                r.Named.Add(Special("smoke", "each shot leaves a cloud of smoke around the shooter, which blinds shots through it", false, 0));
            var p = v.Projectile;
            var input = new WeaponRangedInput
            {
                Warmup = v.Warmup * Stat(w, "RangedWeapon_WarmupMultiplier"),
                Cooldown = Stat(w, "RangedWeapon_Cooldown"),
                Range = v.Range * Stat(w, "RangedWeapon_RangeMultiplier"),
                MinRange = v.MinRange,
                Burst = Math.Max(1, v.Burst),
                TicksBetweenBurst = v.TicksBetween,
                ForcedMissRadius = v.ForcedMiss,
                CanGoWild = v.CanGoWild,
                AccTouch = Math.Min(1f, Stat(w, "AccuracyTouch")),
                AccShort = Math.Min(1f, Stat(w, "AccuracyShort")),
                AccMedium = Math.Min(1f, Stat(w, "AccuracyMedium")),
                AccLong = Math.Min(1f, Stat(w, "AccuracyLong")),
                OneUse = vc.Contains("OneUse"),
                NonLethal = nonLethal
            };
            // A unique weapon in hand: its traits' burst, stopping power, damage and extra damage (Verb.BurstShotCount,
            // TicksBetweenBurstShots, Projectile.Launch, Verb_LaunchProjectile).
            var traits = w.UniqueTraits;
            if (traits != null)
            {
                float count = input.Burst, ticks = input.TicksBetweenBurst;
                foreach (var t in traits)
                {
                    count *= t.BurstCountFactor;
                    if (t.BurstSpeedFactor > 0f) ticks /= t.BurstSpeedFactor;
                }
                input.Burst = Math.Max(1, (int)Math.Ceiling(count - 0.0001f));
                input.TicksBetweenBurst = (int)Math.Round(ticks, MidpointRounding.AwayFromZero);
            }
            string dmgName;
            if (p != null && !p.NoProjectile && p.Found)
            {
                dmgName = p.DamageDef;
                if (traits != null)
                    foreach (var t in traits)
                        if (!string.IsNullOrEmpty(t.DamageOverride)) dmgName = t.DamageOverride;
                float dmgMult = Stat(w, "RangedWeapon_DamageMultiplier");
                input.Damage = (float)Math.Round(p.Damage * dmgMult, MidpointRounding.AwayFromZero);
                input.ArmourPen = p.ArmourPen * Stat(w, "RangedWeapon_ArmorPenetrationMultiplier");
                input.StoppingPower = p.StoppingPower;
                if (traits != null) foreach (var t in traits) input.StoppingPower += t.StoppingPower;
                input.ExplosionRadius = p.ExplosionRadius;
                input.ExplosionFalloff = p.Falloff;
                input.ExplosionDelaySeconds = p.ExplosionDelayTicks / 60f;
                ProjectileRules(w, p, p.ThingClass ?? "", input, r);
                if (p.ChanceToStartFire > 0f)
                    input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, p.ChanceToStartFire));
                if ((p.PreSpawn ?? "").Contains("Fuel") || (p.PostSpawn ?? "").Contains("Fuel"))
                    input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f));
                if ((p.PostGas ?? "").Contains("Tox"))
                    input.Payloads.Add(WeaponPayload.Of(WeaponEffect.ToxGas, 1f));
                string spawn = p.PostSpawn ?? "";
                if (!string.IsNullOrEmpty(spawn) && !spawn.Contains("Fuel") && !spawn.StartsWith("Filth"))
                    r.Named.Add(Special("leaves:" + spawn, "leaves " + spawn + " behind (its own effect, not counted)", false, 0));
                foreach (var e in p.Extras) AddExtra(w, input.Payloads, e, r, e.Chance);
                if (traits != null)
                    foreach (var t in traits)
                        foreach (var e in t.Extras) AddExtra(w, input.Payloads, e, r, e.Chance);
            }
            else
            {
                dmgName = v.BeamDamageDef ?? "";
                var d = Dmg(w, dmgName);
                input.Damage = v.BeamTotalDamage > 0f ? v.BeamTotalDamage / Math.Max(1f, input.Burst) : (d == null ? 10f : d.DefaultDamage ?? 10f);
                input.ArmourPen = Math.Max(0f, d == null ? 0f : d.DefaultArmourPen ?? 0f);
                if (input.ArmourPen <= 0f && (d?.ArmourCategory ?? "") != "") input.ArmourPen = input.Damage * 0.015f;
                input.StoppingPower = d == null ? 0f : d.DefaultStoppingPower ?? 0f;
                if (v.BeamFireChance > 0f) input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, v.BeamFireChance));
            }
            if (vc.Contains("ShootBeam") || vc.Contains("ArcSprayIncinerator"))
            {
                input.Beam = true;
                input.BeamWidth = v.BeamWidth;
                input.BeamHitsNeighbours = v.BeamNeighbours;
            }
            Damage(w, input, dmgName, r);
            if (v.FuelPerShot > 0f)
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
        static void ProjectileRules(WeaponDefFacts w, WeaponProjectileFacts p, string pc, WeaponRangedInput input, WeaponReport r)
        {
            if (p.Stream)
            {
                input.Line = true;
                float life = p.LifeTime ?? 100f, tick = Math.Max(1f, p.TickDamageRate ?? 60f);
                input.HitsPerShot = p.DealsDamageOnce ? 1f : (float)Math.Ceiling(life / tick);
                return;
            }
            if (!KnownProjectiles.Contains(pc))
            {
                r.Unsure.Add("its projectile has its own behaviour (" + Last(pc) + "), read as an ordinary one");
                return;
            }
            switch (Last(pc))
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
                    input.ProjectilesPerShot = Math.Max(1, (int)p.ShotCount);
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
                    foreach (var x in p.ExtensionClasses)
                        if (x == "OrbExtension")
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
        static void Damage(WeaponDefFacts w, WeaponRangedInput input, string dmgName, WeaponReport r)
        {
            var d = Dmg(w, dmgName);
            if (d == null || !d.Found)
            {
                r.Unsure.Add("its damage (" + dmgName + ") isn't in the collection");
                return;
            }
            string cat = d.ArmourCategory ?? "";
            input.Armour = ArmourOf(w, dmgName);
            if (cat == "Light" || cat == "Dark")
                r.Named.Add(Special("alignment", "meets only A RimWorld of Magic's alignment armour, which ordinary armour doesn't give", true));
            else if (cat != "" && cat != "Sharp" && cat != "Blunt" && cat != "Heat")
                r.Unsure.Add("its damage meets an armour of its own (" + cat + "), read as ignoring armour");
            input.HarmsHealth = d.HarmsHealth ?? true;
            string worker = d.Worker ?? "";
            if (dmgName == "EMP")
            {
                input.Payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, input.Damage > 0 ? input.Damage : 50f));
                input.HarmsHealth = false;
            }
            else if (dmgName == "Stun" || worker.EndsWith("Stun") || worker.Contains("ConditionalStun"))
            {
                float amount = input.Damage > 1f ? input.Damage : d.DefaultDamage ?? 20f;
                if (worker.Contains("ConditionalStun"))
                {
                    amount = (d.DefaultDamage ?? 20f) * 0.5f;
                    r.Unsure.Add("its stun follows its own rule, counted as a plain stun of " + (amount * 0.5f).ToString("0", CultureInfo.InvariantCulture) + " seconds");
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
                r.Named.Add(Special("worker:" + worker, DamageWords(w, dmgName) + ": its own damage effect, not counted", false, 0));
                r.Unsure.Add("its damage has its own effect (" + Last(worker) + ")");
                if (worker.Contains("TearGas") || worker.Contains("NonLethal")) input.NonLethal = true;
            }
            foreach (var h in d.Hediffs)
            {
                string hed = h.Hediff ?? "";
                if (hed == "ToxicBuildup")
                    input.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = h.PerDamage * Math.Max(input.Damage, 1f) + h.Severity });
                else
                {
                    r.Named.Add(Special("hediff:" + hed, "inflicts " + Words(hed) + " (its own effect, not counted)", false));
                    if (hed.Contains("Anesthetic")) input.NonLethal = true;
                }
            }
        }

        /// <summary>An extra damage (a projectile's or a melee tool's), by its def.</summary>
        static void AddExtra(WeaponDefFacts w, List<WeaponPayload> payloads, WeaponExtraFacts e, WeaponReport r, float chance)
        {
            string name = e.Def ?? "";
            var d = Dmg(w, name);
            float amount = e.Amount;
            if (name == "EMP")
            {
                payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, amount, chance));
                return;
            }
            if (name == "Stun" || (d?.Worker ?? "").EndsWith("Stun"))
            {
                payloads.Add(WeaponPayload.Of(WeaponEffect.Stun, amount, chance));
                return;
            }
            if (d == null || !d.Found)
            {
                r.Named.Add(Special("extra:" + name, "extra " + name + " (" + amount.ToString("0.#", CultureInfo.InvariantCulture) + ", not in the collection)", false));
                return;
            }
            if (d.HarmsHealth.HasValue && !d.HarmsHealth.Value)
            {
                r.Named.Add(Special("extra:" + name, "extra " + DamageWords(w, name) + " (its own effect, not counted)", false));
                return;
            }
            float ap = e.ArmourPen;
            if (ap < 0f) ap = (d.ArmourCategory ?? "") == "" ? 0f : amount * 0.015f;
            payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = amount, ArmourPen = ap, Armour = ArmourOf(w, name), Chance = chance, Name = DamageWords(w, name) });
            if (d.Worker == "DamageWorker_Flame")
                payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, chance));
            foreach (var h in d.Hediffs)
            {
                if (h.Hediff == "ToxicBuildup")
                    payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = h.PerDamage * amount, Chance = chance });
                else
                    r.Named.Add(Special("hediff:" + h.Hediff, "inflicts " + Words(h.Hediff) + " (its own effect, not counted)", false));
            }
        }

        /// <summary>The melee tools, each capacity a verb with its damage, and the stuff the weapon is rated in.</summary>
        static WeaponMeleeInput MeleeFrom(WeaponDefFacts w, WeaponReport r, bool nonLethal)
        {
            var m = new WeaponMeleeInput { NonLethal = nonLethal, QualityFactor = w.MeleeDamageFactor };
            if (w.Stuffed)
            {
                m.Stuffed = true;
                m.SharpFactor = w.SharpFactor;
                m.BluntFactor = w.BluntFactor;
                m.CooldownFactor = w.MeleeCooldownFactor;
                r.StuffLabel = w.StuffLabel;
            }
            foreach (var t in w.Tools)
            {
                foreach (var c in t.Capacities)
                {
                    string dmg = c.DamageDef ?? "";
                    if (!c.Known)
                    {
                        r.Unsure.Add("a melee move with no maneuver in the collection (" + c.Capacity + ")");
                        continue;
                    }
                    string verbClass = c.VerbClass ?? "";
                    bool cleave = verbClass.EndsWith("Verb_MeleeCleave");
                    if (cleave)
                        r.Named.Add(Special("cleave", t.Label + ": cleaves, 60% more to the target and 60% to every enemy beside it (counted on the target)", true));
                    else if (verbClass.EndsWith("Verb_MeleeAttackDamageCryptoaxe"))
                    {
                        r.Named.Add(Special("slow", "slows whoever it strikes (cryptoslowdown, weaker on heat armour), not counted", false));
                        r.Unsure.Add("its slowing effect isn't valued");
                    }
                    else if (verbClass != "Verb_MeleeAttackDamage" && !string.IsNullOrEmpty(verbClass))
                    {
                        r.Unsure.Add("a melee move with its own rule (" + Last(verbClass) + "), read as an ordinary blow");
                        r.Named.Add(Special("move:" + c.Capacity, t.Label + ": its own move (" + Last(verbClass) + "), not counted beyond its damage", false));
                    }
                    var d = Dmg(w, dmg);
                    string worker = d?.Worker ?? "";
                    var a = new WeaponMeleeAttack
                    {
                        Label = t.Label, Power = t.Power, Cooldown = t.Cooldown, ArmourPen = t.ArmourPen,
                        Armour = ArmourOf(w, dmg), ChanceFactor = t.ChanceFactor, AdditionalHediffs = d?.Hediffs.Count ?? 0,
                        Cuts = (d?.ArmourCategory ?? "") != "Blunt" &&
                               (worker == "DamageWorker_Cut" || worker == "DamageWorker_Stab" || worker == "DamageWorker_Scratch" || worker == "DamageWorker_Bite")
                    };
                    if (!VanillaWorkers.Contains(worker))
                    {
                        r.Named.Add(Special("worker:" + worker, DamageWords(w, dmg) + ": its own damage effect, not counted", false, 0));
                        r.Unsure.Add("a melee damage with its own effect (" + Last(worker) + ")");
                    }
                    if (d != null)
                        foreach (var h in d.Hediffs)
                        {
                            if (h.Hediff == "ToxicBuildup")
                                a.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.ToxicBuildup, Amount = h.PerDamage * a.Power });
                            else
                                r.Named.Add(Special("hediff:" + h.Hediff, "inflicts " + Words(h.Hediff) + " (its own effect, not counted)", false));
                        }
                    foreach (var e in t.Extras)
                    {
                        if (e.Surprise) continue;
                        AddExtra(w, a.Payloads, e, r, e.Chance);
                    }
                    if (cleave)
                        a.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = 0.6f * a.Power, ArmourPen = a.ArmourPen >= 0f ? a.ArmourPen : 0.6f * a.Power * 0.015f, Armour = a.Armour, Name = "cleave" });
                    m.Attacks.Add(a);
                }
            }
            if (m.Attacks.Exists(a => a.Payloads.Exists(p => p.Effect == WeaponEffect.Stun)) && r.Mod == "vanillaexpanded.vwenl") m.NonLethal = true;
            return m;
        }

        /// <summary>An ability the weapon grants, counted when its defs say what it does.</summary>
        static WeaponAbilityInput AbilityFrom(WeaponDefFacts w, WeaponAbilityFacts a, WeaponReport r)
        {
            var ab = new WeaponAbilityInput { Label = a.Label, Charges = a.Charges, CooldownSeconds = a.CooldownTicks / 60f };
            if (string.IsNullOrEmpty(ab.Label)) ab.Label = a.DefName;
            if (!a.Found)
            {
                ab.Note = "not in the collection";
                r.Unsure.Add("grants an ability the collection doesn't hold (" + a.DefName + ")");
                return ab;
            }
            var v = a.Verb;
            WeaponProjectileFacts proj = null;
            foreach (var c in a.Comps)
            {
                if (c.Class == "CompProperties_AbilityLaunchProjectile" && c.Projectile != null) proj = c.Projectile;
                if (c.Class == "CompProperties_AbilityFireSpew")
                {
                    ab.AreaDamage = c.DamAmount ?? 10f;
                    ab.AreaArmour = WeaponArmour.Heat;
                    ab.AreaTargets = 1.5f + (c.LineWidthEnd ?? 2f) * 0.25f;
                    ab.AreaIgnite = 1f;
                    ab.Counted = true;
                }
            }
            if (proj == null && v?.Projectile != null) proj = v.Projectile;
            if (proj == null && !string.IsNullOrEmpty(v?.BeamDamageDef))
            {
                var d = Dmg(w, v.BeamDamageDef);
                var beam = new WeaponRangedInput
                {
                    Warmup = v.Warmup, Cooldown = 0f, Range = v.Range, Burst = Math.Max(1, v.Burst),
                    TicksBetweenBurst = v.TicksBetween, Beam = true, BeamWidth = v.BeamWidth,
                    BeamHitsNeighbours = v.BeamNeighbours, Damage = d == null ? 10f : d.DefaultDamage ?? 10f,
                    ArmourPen = Math.Max(0f, d == null ? 0f : d.DefaultArmourPen ?? 0f), Armour = ArmourOf(w, v.BeamDamageDef)
                };
                Damage(w, beam, v.BeamDamageDef, new WeaponReport());
                ab.Shot = beam;
                ab.Counted = true;
                return ab;
            }
            if (proj != null && proj.Found && !proj.NoProjectile)
            {
                var shot = new WeaponRangedInput
                {
                    Warmup = v?.Warmup ?? 0f, Cooldown = 0f, Range = v?.Range ?? 12f, Burst = Math.Max(1, v?.Burst ?? 1),
                    TicksBetweenBurst = v?.TicksBetween ?? 15, ForcedMissRadius = v?.ForcedMiss ?? 0f,
                    CanGoWild = v?.CanGoWild ?? true,
                    Damage = proj.Damage, ArmourPen = proj.ArmourPen, StoppingPower = proj.StoppingPower,
                    ExplosionRadius = proj.ExplosionRadius, ExplosionFalloff = proj.Falloff,
                    ExplosionDelaySeconds = proj.ExplosionDelayTicks / 60f
                };
                var dummy = new WeaponReport();
                Damage(w, shot, proj.DamageDef, dummy);
                if (proj.ChanceToStartFire > 0f) shot.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, proj.ChanceToStartFire));
                if ((proj.PreSpawn ?? "").Contains("Fuel")) shot.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f));
                foreach (var e in proj.Extras) AddExtra(w, shot.Payloads, e, dummy, e.Chance);
                ab.Shot = shot;
                ab.Counted = true;
                if (!KnownVerbs.Contains(v?.VerbClass ?? ""))
                    r.Unsure.Add("its ability fires by its own rule (" + Last(v?.VerbClass ?? "") + "), read as an ordinary shot");
            }
            if (!ab.Counted)
            {
                ab.Note = "its own effect";
                r.Unsure.Add("its ability (" + ab.Label + ") works by its own code, not counted");
            }
            return ab;
        }

        /// <summary>What can only be named: biocoding, personas, unique traits, heavy-weapon rules, mods' own comps, patches.</summary>
        static void NameTheRest(WeaponDefFacts w, WeaponReport r)
        {
            foreach (var c in w.Comps)
            {
                string cls = c.Class ?? "";
                switch (cls)
                {
                    case "CompProperties_Biocodable":
                        r.Named.Add(Special("biocodable", !string.IsNullOrEmpty(w.BiocodedTo)
                            ? "biocoded to " + w.BiocodedTo + ": only they can use it"
                            : "can be biocoded to one person", false, 0));
                        break;
                    case "CompProperties_BladelinkWeapon":
                        if (w.PersonaTraits != null)
                        {
                            foreach (var t in w.PersonaTraits) r.Passives.AddRange(t.Equipped);
                            r.Named.Add(Special("persona", "persona weapon" + (!string.IsNullOrEmpty(w.BiocodedTo) ? ", bonded to " + w.BiocodedTo : ", not bonded yet") +
                                (w.PersonaTraits.Count > 0 ? "; its traits: " + TraitsLine(w.PersonaTraits) : ""), w.PersonaTraits.Any(t => t.Equipped.Count > 0), 0));
                        }
                        else
                            r.Named.Add(Special("persona", "persona weapon: bonds to whoever wields it first, with persona traits rolled when made (moods and needs, plus or minus), not counted", false, 0));
                        break;
                    case "CompProperties_UniqueWeapon":
                        if (w.UniqueTraits != null)
                            r.Named.Add(Special("unique", "unique weapon" + (w.UniqueTraits.Count > 0 ? ", its traits counted: " + TraitsLine(w.UniqueTraits) : ", no traits"), true, 0));
                        else
                        {
                            r.Named.Add(Special("unique", "unique weapon: one to three traits rolled when made; rated as the plain weapon", false, 0));
                            r.Unsure.Add("a unique weapon: its rolled traits can move it either way");
                        }
                        break;
                    case "CompProperties_Explosive":
                        r.Named.Add(Special("volatile", "explodes if it burns", false, -1));
                        break;
                    case "CompProperties_LaserCapacitor":
                        if (r.Ranged != null && c.F("WarmUpReductionPerShot") > 0f)
                        {
                            // Vanilla Expanded Framework's capacitor, in its quick-fire mode: each shot from the same spot takes
                            // WarmUpReductionPerShot off the aim, down to none; counted over a stand of ten shots.
                            float w0 = r.Ranged.Warmup, red = c.F("WarmUpReductionPerShot"), sum = 0f;
                            for (int k = 0; k < 10; k++) sum += Math.Max(0f, w0 - red * k);
                            r.Ranged.Warmup = sum / 10f;
                            r.Named.Add(Special("laser", "laser capacitor: in its quick-fire mode, aims faster with each shot from the same spot (counted over ten shots)", true));
                            if (c.B("Overheats"))
                                r.Named.Add(Special("overheat", "at full heat each shot risks an overheat blast on the shooter (" + Math.Round(c.F("OverheatChance") * 100f).ToString("0", CultureInfo.InvariantCulture) + "%)", false, -1));
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
                        float chance = Math.Min(1f, c.F("baseDeflectChance") + 8f * c.F("deflectRatePerSkillPoint"));
                        r.Passives.Add(new WeaponPassive("DeflectChance", chance));
                        r.Named.Add(Special("deflector", "deflects incoming shots (" + Math.Round(chance * 100f).ToString("0", CultureInfo.InvariantCulture) + "% at skill 8)" +
                            (c.B("canReflect") ? ", sometimes back at the shooter" : "") + " (counted)", true));
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
            foreach (var x in w.Extensions)
            {
                string cls = x.Class ?? "";
                if (cls == "HeavyWeapon" && x.B("isHeavy"))
                {
                    var who = x.L("supportedArmors").Select(Words).Concat(x.L("supportedTraits").Select(a => "the " + Words(a) + " trait"))
                        .Concat(x.L("supportedGenes").Select(Words)).ToList();
                    r.Named.Add(Special("heavy-weapon", "a heavy weapon: only " + (who.Count > 0 ? "someone with " + string.Join(" or ", who) : "some") + " can carry it" +
                        (x.F("hpPerShot") > 0f ? "; wears down by " + x.F("hpPerShot").ToString("0", CultureInfo.InvariantCulture) + " a shot" : ""), false, -1));
                }
                if (cls == "MakeshiftProperties")
                    r.Named.Add(Special("makeshift", "makeshift: may fall apart after " + x.S("shots").Replace("~", " to ") + " shots (its own rule)", false, -1));
                if (cls == "ThingDefExtension" && x.B("usableWithShields"))
                    r.Named.Add(Special("shields", "can be used behind a shield belt", false));
            }
            r.Unsure.AddRange(w.PatchNotes);
            if (w.CompClasses.Contains("CompProperties_EquippableAbilityReloadable") && !w.Abilities.Any() && !w.CompClasses.Contains("CompProperties_UniqueWeapon"))
                r.Unsure.Add("can hold charges for an ability it doesn't name");
        }

        /// <summary>Traits by name, each with what it changes in a few words.</summary>
        static string TraitsLine(List<WeaponTraitFacts> traits)
        {
            var parts = new List<string>();
            foreach (var t in traits)
            {
                var what = new List<string>(t.StatWords);
                if (t.BurstCountFactor != 1f) what.Add("burst " + Signed(t.BurstCountFactor - 1f));
                if (t.BurstSpeedFactor != 1f) what.Add("burst speed " + Signed(t.BurstSpeedFactor - 1f));
                if (t.StoppingPower != 0f) what.Add("stopping power " + (t.StoppingPower > 0f ? "+" : "") + t.StoppingPower.ToString("0.#", CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(t.DamageOverride)) what.Add("its shots turn " + Words(t.DamageOverride));
                foreach (var e in t.Extras) what.Add("extra " + Words(e.Def));
                foreach (var p in t.Equipped) what.Add(WeaponRateMath.StatWords(p.Stat) + " " + (p.Offset >= 0f ? "+" : "") + p.Offset.ToString("0.##", CultureInfo.InvariantCulture));
                if (t.Moods) what.Add("moods and needs");
                parts.Add(t.Label + (what.Count > 0 ? " (" + string.Join(", ", what) + ")" : ""));
            }
            return string.Join(", ", parts);
        }

        static string Signed(float share) => (share >= 0f ? "+" : "") + Math.Round(share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        /// <summary>A def name in plain words: the name split at capitals, a mod's short prefix dropped.</summary>
        public static string Words(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "";
            string s = System.Text.RegularExpressions.Regex.Replace(defName, "^[A-Z]{2,5}_", "");
            s = s.Replace("_", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
            return s.ToLowerInvariant();
        }

        // ---------------------------------------------------------------- rating

        /// <summary>
        /// Rates a read weapon for the reference colonist: a ranged weapon in its best firing mode (its melee rating as the
        /// "in melee" situation), a melee weapon by its role; its rating at Shooting or Melee 4, 8, 12 and 16 and with the
        /// traits that change aim or a blow (trigger-happy, careful shooter, brawler).
        /// </summary>
        public static void Rate(WeaponReport r)
        {
            if (r.Kind == "ranged")
            {
                r.Rating = WeaponRateMath.RateRanged(r.Ranged, null, r.Passives, r.Abilities, null, r.Mass);
                foreach (var alt in r.Alternates)
                {
                    var other = WeaponRateMath.RateRanged(alt, null, r.Passives, r.Abilities, null, r.Mass);
                    if (other.FightValue > r.Rating.FightValue)
                    {
                        r.Rating = other;
                        r.Ranged = alt;
                    }
                }
                if (r.Melee != null && r.Melee.Attacks.Count > 0)
                {
                    r.MeleeRating = WeaponRateMath.RateMelee(r.Melee, r.Mass, null, r.Passives);
                    r.Rating.Situations["in melee"] = r.MeleeRating.Overall;
                }
                foreach (int s in new[] { 4, 8, 12, 16 })
                {
                    var f = WeaponRateMath.Reference;
                    f.ShootingSkill = s;
                    r.BySkill[s.ToString(CultureInfo.InvariantCulture)] = WeaponRateMath.RateRanged(r.Ranged, f, r.Passives, r.Abilities, r.Rating.Role, r.Mass).Overall;
                }
                var th = WeaponRateMath.Reference;
                th.ShootingSkill = 3f;
                th.AimFactor = 0.5f;
                r.ByTrait["trigger-happy"] = WeaponRateMath.RateRanged(r.Ranged, th, r.Passives, r.Abilities, r.Rating.Role, r.Mass).Overall;
                var cs = WeaponRateMath.Reference;
                cs.ShootingSkill = 13f;
                cs.AimFactor = 1.25f;
                r.ByTrait["careful shooter"] = WeaponRateMath.RateRanged(r.Ranged, cs, r.Passives, r.Abilities, r.Rating.Role, r.Mass).Overall;
            }
            else
            {
                if (r.Melee == null || r.Melee.Attacks.Count == 0)
                {
                    r.Kind = "left out";
                    return;
                }
                r.Rating = WeaponRateMath.RateMelee(r.Melee, r.Mass, null, r.Passives, r.Abilities);
                foreach (int s in new[] { 4, 8, 12, 16 })
                {
                    var f = WeaponRateMath.Reference;
                    f.MeleeSkill = s;
                    r.BySkill[s.ToString(CultureInfo.InvariantCulture)] = WeaponRateMath.RateMelee(r.Melee, r.Mass, f, r.Passives, r.Abilities, r.Rating.Role).Overall;
                }
                var br = WeaponRateMath.Reference;
                br.MeleeSkill = 12f;
                r.ByTrait["brawler"] = WeaponRateMath.RateMelee(r.Melee, r.Mass, br, r.Passives, r.Abilities, r.Rating.Role).Overall;
            }
            foreach (var n in r.Named)
                if (!r.Rating.Specials.Any(s => s.Key == n.Key)) r.Rating.Specials.Add(n);
            r.Unsure = r.Unsure.Distinct().ToList();
        }

        /// <summary>Which table of the review a rated weapon goes in: its role, grenades and launchers apart.</summary>
        public static string TableOf(WeaponReport r)
        {
            var g = r.Rating;
            if (g.Kind == "melee") return g.Role;
            string label = (r.Label ?? "").ToLowerInvariant();
            var w = r.Def;
            bool thrown = (w.Verbs.Count > 0 && (w.Verbs[0].VerbClass ?? "").Contains("LaunchProjectile")) ||
                          w.WeaponTags.Any(t => t.Contains("Grenade")) || label.Contains("grenade") || label.Contains("throwing") ||
                          label.Contains("molotov") || label.Contains("javelin") || label.Contains("dynamite");
            if (g.Role == "one-use") return "grenades";
            if (g.Role == "area" || g.Role == "anti-mechanoid" || g.Role == "utility") return thrown && r.Mass <= 2f ? "grenades" : "launchers";
            if (thrown && r.Mass <= 1.5f) return "grenades";
            return g.Role;
        }

        /// <summary>What the tags read beyond the rating, from the facts.</summary>
        public static WeaponTagFacts TagFacts(WeaponReport r)
        {
            var w = r.Def;
            var f = new WeaponTagFacts { DefName = r.DefName ?? "", Mass = r.Mass };
            foreach (var p in r.Passives)
            {
                if (p.Stat == "MoveSpeed") f.MoveSpeedOffset += p.Offset;
                if (p.Stat == "MeleeDodgeChance") f.DodgeOffset += p.Offset;
                if (p.Stat == "DeflectChance") f.DeflectChance = p.Offset;
                if (p.Stat == "PsychicSensitivityOffset") f.PsychicSensitivityOffset += p.Offset;
            }
            var keys = r.Rating.Specials.Select(s => s.Key).ToList();
            f.Persona = keys.Contains("persona");
            f.Unique = keys.Contains("unique");
            f.Biocodable = keys.Contains("biocodable");
            f.Wears = keys.Contains("wears");
            f.Thrown = r.Table == "grenades";
            if (w.UniqueTraits != null)
                f.UniqueWhy = w.UniqueTraits.Count > 0 ? "its traits: " + string.Join(", ", w.UniqueTraits.Select(t => t.Label)) : "no traits rolled";
            if (w.PersonaTraits != null)
                f.PersonaWhy = (!string.IsNullOrEmpty(w.BiocodedTo) ? "bonded to " + w.BiocodedTo : "not bonded yet") +
                               (w.PersonaTraits.Count > 0 ? "; its traits: " + string.Join(", ", w.PersonaTraits.Select(t => t.Label)) : "");
            if (!string.IsNullOrEmpty(w.BiocodedTo) && f.Biocodable) f.BiocodedWhy = "biocoded to " + w.BiocodedTo;
            var who = new List<string>();
            foreach (var x in w.Extensions)
            {
                if (x.Class == "HeavyWeapon" && x.B("isHeavy"))
                {
                    who.AddRange(x.L("supportedArmors").Select(a => "someone in " + Words(a)));
                    who.AddRange(x.L("supportedTraits").Select(a => "someone with the " + Words(a) + " trait"));
                    who.AddRange(x.L("supportedGenes").Select(a => "someone with the " + Words(a) + " gene"));
                    f.HitPointsPerShot = x.F("hpPerShot");
                }
                if (x.Class == "MakeshiftProperties") f.BreaksAfter = x.S("shots").Replace("~", " to ");
            }
            if (keys.Contains("mage-only")) who.Add("a mage of A RimWorld of Magic");
            if (keys.Contains("channel")) who.Add("someone who can channel it");
            f.RestrictedTo = string.Join(" or ", who.Distinct());
            foreach (var c in w.Comps)
                if (c.Class == "CompProperties_LaserCapacitor" && c.B("Overheats")) f.OverheatChance = c.F("OverheatChance");
            f.BypassesShields = r.Ranged != null && w.Verbs.Any(v => ((v.Projectile?.DamageDef ?? "") + (v.BeamDamageDef ?? "")).Contains("BypassShields"));
            f.Demolishes = w.Tools.Any(t => t.Capacities.Any(c => c.Capacity == "Demolish"));
            f.Destructive = w.WeaponTags.Any(t => t.Contains("Destructive") && r.Rating.Kind == "melee");
            f.Research = string.Join(" and ", w.Research.Select(Words));
            f.ResearchTech = w.ResearchTech ?? "";
            f.HardToGet = !w.Made && !w.CarriedByKinds;
            f.RatingAtSkill4 = r.BySkill.TryGetValue("4", out var a4) ? a4 : r.Rating.Overall;
            f.RatingAtSkill16 = r.BySkill.TryGetValue("16", out var a16) ? a16 : r.Rating.Overall;
            foreach (var a in r.Abilities) f.Abilities.Add((a.Label, a.Charges, a.Counted));
            return f;
        }

        /// <summary>
        /// The whole of it: read, rated, its table, tags and line. Unrated (its attack its own code) or left out (no attack a
        /// colonist can use) when so; the report says which.
        /// </summary>
        public static WeaponReport Full(WeaponDefFacts w)
        {
            var r = Read(w);
            if (r.Kind == "left out") return r;
            if (r.Unrated != null)
            {
                r.Kind = "unrated";
                return r;
            }
            Rate(r);
            if (r.Kind == "left out") return r;
            Finish(r);
            return r;
        }

        /// <summary>A rated weapon's table, tag facts, tags and line.</summary>
        public static void Finish(WeaponReport r)
        {
            r.Table = TableOf(r);
            r.TagFacts = TagFacts(r);
            r.Tags = WeaponTagMath.For(r.Rating, r.TagFacts, r.Rating.Kind == "ranged" ? r.Ranged : null, r.Rating.Kind == "melee" ? r.Melee : null);
            r.Plain = WeaponWords.Plain(r);
        }
    }
}
