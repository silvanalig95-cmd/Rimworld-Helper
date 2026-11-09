using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The weapon rating in the game (docs/design/weapons.md, step 2): a weapon's facts read from its live def (patches in
    /// it) and the weapon in hand (its quality and stuff by the game's own stats, an Odyssey unique weapon's traits, a
    /// persona weapon's traits and bond, biocoding), rated by the same reading the review runs (Weapons/WeaponReadMath.cs),
    /// so a weapon at normal quality in steel rates as docs/weapons/weapons.json has it. Kept by def, stuff and quality, and
    /// by the weapon itself only where unique or persona traits apply; a colonist's rating is kept an hour of the game's
    /// time. Nothing is worked out per frame once a weapon has been seen.
    /// </summary>
    public static class WeaponLive
    {
        /// <summary>"Weapon ratings": the rating anywhere (the info card, Rimfolk's card, the gear tab, the button on the map).</summary>
        public static bool On => ColonyCouncilMod.Settings?.weaponRatings ?? true;

        /// <summary>How long a colonist's own rating is kept: an hour of the game's time.</summary>
        public const int KeepTicks = 2500;

        private static readonly Dictionary<(ThingDef, ThingDef, QualityCategory), WeaponReport> byDef = new Dictionary<(ThingDef, ThingDef, QualityCategory), WeaponReport>();
        private static readonly Dictionary<int, (int tick, WeaponReport report)> byThing = new Dictionary<int, (int, WeaponReport)>();
        private static readonly Dictionary<int, (int tick, FighterFacts facts)> fighters = new Dictionary<int, (int, FighterFacts)>();
        private static readonly Dictionary<(int, WeaponReport), (int tick, WeaponForColonist rating)> forColonist = new Dictionary<(int, WeaponReport), (int, WeaponForColonist)>();
        private static readonly Dictionary<string, WeaponDamageFacts> damages = new Dictionary<string, WeaponDamageFacts>();

        /// <summary>Forgets every rating kept (a new game, the settings changed).</summary>
        public static void Reset()
        {
            byDef.Clear();
            byThing.Clear();
            fighters.Clear();
            forColonist.Clear();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------------------------------------------------------- which weapons

        /// <summary>
        /// A weapon a colonist can wield and the rating reads, as the review chooses them: a weapon by its tags or its
        /// category (not a bottle with a blow), a primary with an attack, not a turret's or a body's.
        /// </summary>
        public static bool Rateable(ThingDef def) =>
            def != null && def.IsWeapon && def.equipmentType == EquipmentType.Primary && !def.destroyOnDrop &&
            (!def.Verbs.NullOrEmpty() || !def.tools.NullOrEmpty()) &&
            (!def.weaponTags.NullOrEmpty() || (def.thingCategories?.Any(c => c?.defName != null && c.defName.Contains("Weapon")) ?? false)) &&
            (def.weaponTags == null || !def.weaponTags.Any(t => t != null && t.Contains("Turret")));

        /// <summary>The stuff a def is rated in when none is given: steel where it takes metal, else wood, else granite blocks, else the game's own default.</summary>
        public static ThingDef DefaultStuff(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            var cats = def.stuffCategories;
            foreach (var (stuff, cat) in new[] { ("Steel", "Metallic"), ("WoodLog", "Woody"), ("BlocksGranite", "Stony") })
            {
                if (cats == null || !cats.Any(c => c?.defName == cat)) continue;
                var s = DefDatabase<ThingDef>.GetNamedSilentFail(stuff);
                if (s != null) return s;
            }
            return GenStuff.DefaultStuffFor(def);
        }

        /// <summary>A weapon in hand has traits of its own (an Odyssey unique weapon's, a persona weapon's): it is read by itself.</summary>
        public static bool HasOwnTraits(Thing t) => t is ThingWithComps tc && (tc.GetComp<CompUniqueWeapon>() != null || tc.GetComp<CompBladelinkWeapon>() != null);

        // ---------------------------------------------------------------- the reports

        /// <summary>The weapon in hand, read and rated; null for one the rating doesn't read (or with the setting off).</summary>
        public static WeaponReport Of(Thing t)
        {
            if (t == null || !On || !Rateable(t.def)) return null;
            if (HasOwnTraits(t))
            {
                int now = Now;
                if (byThing.TryGetValue(t.thingIDNumber, out var kept) && now - kept.tick < KeepTicks && now >= kept.tick) return kept.report;
                if (byThing.Count > 400) byThing.Clear();
                var r = Build(t.def, t.Stuff, Quality(t), t);
                byThing[t.thingIDNumber] = (now, r);
                return r;
            }
            return Of(t.def, t.Stuff, Quality(t));
        }

        /// <summary>A weapon def in a stuff (its default when none) at a quality, read and rated.</summary>
        public static WeaponReport Of(ThingDef def, ThingDef stuff, QualityCategory q = QualityCategory.Normal)
        {
            if (!On || !Rateable(def)) return null;
            if (stuff == null && def.MadeFromStuff) stuff = DefaultStuff(def);
            var key = (def, stuff, q);
            if (byDef.TryGetValue(key, out var r)) return r;
            r = Build(def, stuff, q, null);
            byDef[key] = r;
            return r;
        }

        private static QualityCategory Quality(Thing t) => t.TryGetQuality(out var q) ? q : QualityCategory.Normal;

        private static WeaponReport Build(ThingDef def, ThingDef stuff, QualityCategory q, Thing thing)
        {
            try
            {
                var r = WeaponRead.Full(FactsFor(def, stuff, q, thing));
                return r.Rated ? r : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the weapon rating of " + def?.defName, e);
                return null;
            }
        }

        // ---------------------------------------------------------------- the live def as facts

        /// <summary>A type's name as defs write it: the game's own without its namespace, a mod's in full ("VEF.Weapons.Verb_Shoot").</summary>
        public static string NameOf(Type t)
        {
            if (t == null) return "";
            string ns = t.Namespace ?? "";
            return ns == "Verse" || ns == "RimWorld" || ns.StartsWith("Verse.") || ns.StartsWith("RimWorld.") ? t.Name : t.FullName ?? t.Name;
        }

        /// <summary>A damage def's facts, once each.</summary>
        public static WeaponDamageFacts DamageOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (damages.TryGetValue(name, out var known)) return known;
            var d = DefDatabase<DamageDef>.GetNamedSilentFail(name);
            WeaponDamageFacts f = null;
            if (d != null)
            {
                f = new WeaponDamageFacts
                {
                    Name = name, Label = d.label ?? "", ArmourCategory = d.armorCategory?.defName ?? "",
                    Worker = d.workerClass == null || d.workerClass == typeof(DamageWorker) ? "" : NameOf(d.workerClass),
                    DefaultDamage = d.defaultDamage, DefaultArmourPen = d.defaultArmorPenetration, DefaultStoppingPower = d.defaultStoppingPower, HarmsHealth = d.harmsHealth
                };
                if (d.additionalHediffs != null)
                    foreach (var h in d.additionalHediffs)
                        if (h?.hediff != null)
                            f.Hediffs.Add(new WeaponHediffFacts { Hediff = h.hediff.defName, PerDamage = h.severityPerDamageDealt, Severity = h.severityFixed });
            }
            damages[name] = f;
            return f;
        }

        /// <summary>The gun's own stats the facts take as they are (read when asked, so after the defs have loaded).</summary>
        private static StatDef[] RangedStats => new[]
        {
            StatDefOf.AccuracyTouch, StatDefOf.AccuracyShort, StatDefOf.AccuracyMedium, StatDefOf.AccuracyLong, StatDefOf.RangedWeapon_Cooldown,
            StatDefOf.RangedWeapon_WarmupMultiplier, StatDefOf.RangedWeapon_RangeMultiplier
        };

        /// <summary>
        /// The facts of a weapon: its def's verbs, projectiles, tools, abilities, comps and extensions as the game has loaded
        /// them (other mods' patches in them), and its numbers by the game's own stats for this weapon (the thing in hand) or
        /// this def, stuff and quality. A projectile's damage and piercing are the game's own for the weapon, so the facts'
        /// damage and piercing multipliers stay at one.
        /// </summary>
        public static WeaponDefFacts FactsFor(ThingDef def, ThingDef stuff, QualityCategory q, Thing thing)
        {
            var req = thing != null ? StatRequest.For(thing) : StatRequest.For(def, stuff, q);
            float S(StatDef s, float fallback = 1f)
            {
                if (s == null) return fallback;
                try
                {
                    return s.Worker.GetValue(req);
                }
                catch
                {
                    return fallback;
                }
            }
            var mod = def.modContentPack;
            var f = new WeaponDefFacts
            {
                DefName = def.defName, Label = def.label ?? def.defName, Mod = (mod?.PackageId ?? "").Replace("_steam", ""), ModName = mod?.Name ?? "",
                Tech = def.techLevel.ToString(), Mass = S(StatDefOf.Mass, 1f), Market = S(StatDefOf.MarketValue, 0f), DamageOf = DamageOf
            };
            foreach (var s in RangedStats)
                if (s != null) f.Stats[s.defName] = S(s);
            f.Stats["RangedWeapon_DamageMultiplier"] = 1f;
            f.Stats["RangedWeapon_ArmorPenetrationMultiplier"] = 1f;
            float damageMult = S(StatDefOf.RangedWeapon_DamageMultiplier), penMult = S(StatDefOf.RangedWeapon_ArmorPenetrationMultiplier);
            f.MeleeCooldownFactor = S(StatDefOf.MeleeWeapon_CooldownMultiplier);
            f.MeleeDamageFactor = S(StatDefOf.MeleeWeapon_DamageMultiplier);
            if (stuff != null)
            {
                f.StuffLabel = stuff.label;
                f.SharpFactor = stuff.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier);
                f.BluntFactor = stuff.GetStatValueAbstract(StatDefOf.BluntDamageMultiplier);
            }
            f.Stuffed = stuff != null || Math.Abs(f.MeleeCooldownFactor - 1f) > 0.0001f;
            if (def.equippedStatOffsets != null)
                foreach (var m in def.equippedStatOffsets)
                    if (m?.stat != null) f.EquippedOffsets.Add(new KeyValuePair<string, float>(m.stat.defName, m.value));
            if (def.Verbs != null)
                foreach (var v in def.Verbs)
                    if (v != null) f.Verbs.Add(VerbFacts(v, damageMult, penMult));
            if (def.tools != null)
                foreach (var t in def.tools)
                    if (t != null) f.Tools.Add(ToolFacts(t));
            // The weapon in hand's own comps: a unique weapon's trait sets the props of its ability comp.
            var comps = thing is ThingWithComps twc && twc.AllComps != null
                ? twc.AllComps.Select(c => c?.props).Where(p => p != null).ToList()
                : (def.comps ?? new List<CompProperties>()).Where(p => p != null).ToList();
            foreach (var p in comps)
            {
                var part = PartFacts(p, CompClassName(p));
                f.Comps.Add(part);
                f.CompClasses.Add(part.Class);
                AbilitiesOf(p, f);
            }
            if (def.modExtensions != null)
                foreach (var x in def.modExtensions)
                    if (x != null) f.Extensions.Add(ExtensionFacts(x));
            if (def.weaponTags != null) f.WeaponTags.AddRange(def.weaponTags.Where(t => t != null));
            var rm = def.recipeMaker;
            if (rm != null)
            {
                var research = new List<ResearchProjectDef>();
                if (rm.researchPrerequisite != null) research.Add(rm.researchPrerequisite);
                if (rm.researchPrerequisites != null) research.AddRange(rm.researchPrerequisites.Where(x => x != null));
                f.Research.AddRange(research.Select(x => x.defName));
                if (research.Count > 0) f.ResearchTech = research.Max(x => x.techLevel).ToString();
            }
            f.Made = Made(def);
            f.CarriedByKinds = Carried(def);
            if (thing is ThingWithComps tw)
            {
                var unique = tw.GetComp<CompUniqueWeapon>();
                if (unique != null) f.UniqueTraits = (unique.TraitsListForReading ?? new List<WeaponTraitDef>()).Where(t => t != null).Select(TraitFacts).ToList();
                var persona = tw.GetComp<CompBladelinkWeapon>();
                if (persona != null) f.PersonaTraits = (persona.TraitsListForReading ?? new List<WeaponTraitDef>()).Where(t => t != null).Select(TraitFacts).ToList();
                var code = tw.GetComp<CompBiocodable>();
                if (code != null && code.Biocoded) f.BiocodedTo = code.CodedPawn?.LabelShort ?? code.CodedPawnLabel ?? "";
            }
            return f;
        }

        private static string CompClassName(CompProperties p) =>
            p.GetType() == typeof(CompProperties) ? p.compClass?.Name ?? "CompProperties" : p.GetType().Name;

        private static WeaponVerbFacts VerbFacts(VerbProperties v, float damageMult, float penMult)
        {
            var f = new WeaponVerbFacts
            {
                VerbClass = v.verbClass == null || v.verbClass == typeof(Verb) ? "" : NameOf(v.verbClass), Range = v.range, MinRange = v.minRange,
                Warmup = v.warmupTime, Burst = v.burstShotCount, TicksBetween = v.ticksBetweenBurstShots, ForcedMiss = v.ForcedMissRadius,
                CanGoWild = v.canGoWild, BeamDamageDef = v.beamDamageDef?.defName ?? "", BeamTotalDamage = v.beamTotalDamage,
                BeamFireChance = v.beamChanceToAttachFire, BeamWidth = v.beamWidth, BeamNeighbours = v.beamHitsNeighborCells, FuelPerShot = v.consumeFuelPerShot
            };
            if (v.defaultProjectile != null) f.Projectile = ProjectileFacts(v.defaultProjectile, damageMult, penMult);
            return f;
        }

        private static WeaponProjectileFacts ProjectileFacts(ThingDef p, float damageMult, float penMult)
        {
            var f = new WeaponProjectileFacts { DefName = p.defName, ThingClass = NameOf(p.thingClass) };
            var pr = p.projectile;
            if (pr == null)
            {
                f.NoProjectile = true;
                return f;
            }
            f.DamageDef = pr.damageDef?.defName ?? "";
            if (pr.damageDef != null)
            {
                f.Damage = pr.GetDamageAmount(damageMult, null);
                f.ArmourPen = pr.GetArmorPenetration(null) * penMult;
            }
            f.StoppingPower = pr.stoppingPower == 0f && pr.damageDef != null ? pr.damageDef.defaultStoppingPower : pr.stoppingPower;
            f.ExplosionRadius = pr.explosionRadius;
            f.Falloff = pr.explosionDamageFalloff;
            f.ExplosionDelayTicks = pr.explosionDelay;
            f.ChanceToStartFire = pr.explosionChanceToStartFire;
            f.PreSpawn = pr.preExplosionSpawnThingDef?.defName ?? "";
            f.PostSpawn = pr.postExplosionSpawnThingDef?.defName ?? "";
            f.PostGas = pr.postExplosionGasType?.ToString() ?? "";
            if (pr.extraDamages != null)
                foreach (var e in pr.extraDamages)
                    if (e?.def != null) f.Extras.Add(new WeaponExtraFacts { Def = e.def.defName, Amount = e.amount, ArmourPen = e.armorPenetration, Chance = e.chance });
            // Mods' own projectile numbers: VFE Medieval's matchlock shot count, Vanilla Expanded Framework's streams.
            f.ShotCount = Num(pr, "shotCount") ?? 1f;
            f.Stream = p.GetType().Name == "ExpandableProjectileDef";
            if (f.Stream)
            {
                f.LifeTime = Num(p, "lifeTimeDuration");
                f.TickDamageRate = Num(p, "tickDamageRate");
                f.DealsDamageOnce = Flag(p, "dealsDamageOnce");
            }
            if (p.modExtensions != null) f.ExtensionClasses.AddRange(p.modExtensions.Where(x => x != null).Select(x => x.GetType().Name));
            return f;
        }

        private static WeaponToolFacts ToolFacts(Tool t)
        {
            var f = new WeaponToolFacts { Label = t.label ?? "", Power = t.power, Cooldown = t.cooldownTime, ArmourPen = t.armorPenetration, ChanceFactor = t.chanceFactor };
            if (t.capacities != null)
                foreach (var c in t.capacities)
                {
                    if (c == null) continue;
                    var m = Maneuver(c);
                    f.Capacities.Add(new WeaponCapacityFacts
                    {
                        Capacity = c.defName, Known = m != null, DamageDef = m?.verb?.meleeDamageDef?.defName ?? "",
                        VerbClass = m?.verb?.verbClass == null ? "" : NameOf(m.verb.verbClass)
                    });
                }
            if (t.extraMeleeDamages != null)
                foreach (var e in t.extraMeleeDamages)
                    if (e?.def != null) f.Extras.Add(new WeaponExtraFacts { Def = e.def.defName, Amount = e.amount, ArmourPen = e.armorPenetration, Chance = e.chance });
            if (t.surpriseAttack?.extraMeleeDamages != null)
                foreach (var e in t.surpriseAttack.extraMeleeDamages)
                    if (e?.def != null) f.Extras.Add(new WeaponExtraFacts { Def = e.def.defName, Amount = e.amount, ArmourPen = e.armorPenetration, Chance = e.chance, Surprise = true });
            return f;
        }

        private static Dictionary<ToolCapacityDef, ManeuverDef> maneuvers;

        /// <summary>The maneuver a capacity is struck with (the first that needs it, as the defs list them).</summary>
        private static ManeuverDef Maneuver(ToolCapacityDef c)
        {
            if (maneuvers == null)
            {
                maneuvers = new Dictionary<ToolCapacityDef, ManeuverDef>();
                foreach (var m in DefDatabase<ManeuverDef>.AllDefsListForReading)
                    if (m?.requiredCapacity != null && !maneuvers.ContainsKey(m.requiredCapacity))
                        maneuvers[m.requiredCapacity] = m;
            }
            return maneuvers.TryGetValue(c, out var found) ? found : null;
        }

        /// <summary>A comp's abilities (the game's equippable ability and charges, a mod's list of abilities), as facts.</summary>
        private static void AbilitiesOf(CompProperties p, WeaponDefFacts f)
        {
            int charges = (int)(Num(p, "maxCharges") ?? 0f);
            var defs = new List<Def>();
            if (Field(p, "abilityDef") is Def one) defs.Add(one);
            foreach (var name in new[] { "abilities", "abilityDefs" })
                if (Field(p, name) is IEnumerable list && !(list is string))
                    foreach (var x in list)
                        if (x is Def d) defs.Add(d);
            foreach (var d in defs) f.Abilities.Add(AbilityFacts(d, charges));
        }

        private static WeaponAbilityFacts AbilityFacts(Def a, int charges)
        {
            var f = new WeaponAbilityFacts { DefName = a.defName, Label = a.label ?? "", Charges = charges };
            if (Field(a, "verbProperties") is VerbProperties v) f.Verb = VerbFacts(v, 1f, 1f);
            if (Field(a, "cooldownTicksRange") is IntRange cd) f.CooldownTicks = (cd.min + cd.max) / 2f;
            if (Field(a, "comps") is IEnumerable comps)
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var cf = new WeaponAbilityCompFacts { Class = c.GetType().Name };
                    if (Field(c, "projectileDef") is ThingDef pd) cf.Projectile = ProjectileFacts(pd, 1f, 1f);
                    float? dam = Num(c, "damAmount");
                    if (dam.HasValue && dam.Value >= 0f) cf.DamAmount = dam;
                    // Unset in its def (0), as the review reads a field the def doesn't write.
                    float? width = Num(c, "lineWidthEnd");
                    if (width.HasValue && width.Value > 0f) cf.LineWidthEnd = width;
                    f.Comps.Add(cf);
                }
            return f;
        }

        /// <summary>A comp's or extension's simple fields as text and its lists of defs by name, as the review reads them from XML.</summary>
        private static WeaponPartFacts PartFacts(object o, string cls)
        {
            var f = new WeaponPartFacts { Class = cls };
            foreach (var fi in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v;
                try
                {
                    v = fi.GetValue(o);
                }
                catch
                {
                    continue;
                }
                if (v == null) continue;
                switch (v)
                {
                    case float x: f.Fields[fi.Name] = x.ToString(CultureInfo.InvariantCulture); break;
                    case int x: f.Fields[fi.Name] = x.ToString(CultureInfo.InvariantCulture); break;
                    case bool x: f.Fields[fi.Name] = x ? "true" : "false"; break;
                    case string x: f.Fields[fi.Name] = x; break;
                    case IntRange x: f.Fields[fi.Name] = x.min + "~" + x.max; break;
                    case Def x: f.Fields[fi.Name] = x.defName; break;
                    case IEnumerable list:
                        var names = new List<string>();
                        foreach (var e in list)
                            if (e is Def d) names.Add(d.defName);
                        if (names.Count > 0) f.Lists[fi.Name] = names;
                        break;
                }
            }
            return f;
        }

        /// <summary>A mod extension's facts; Vanilla Expanded Framework's heavy weapon under the names the review reads.</summary>
        private static WeaponPartFacts ExtensionFacts(DefModExtension x)
        {
            var f = PartFacts(x, x.GetType().Name);
            if (f.Class == "HeavyWeapon" && f.Fields.TryGetValue("weaponHitPointsDeductionOnShot", out var hp)) f.Fields["hpPerShot"] = hp;
            return f;
        }

        private static WeaponTraitFacts TraitFacts(WeaponTraitDef t)
        {
            var f = new WeaponTraitFacts
            {
                Label = t.label ?? t.defName, BurstCountFactor = t.burstShotCountMultiplier, BurstSpeedFactor = t.burstShotSpeedMultiplier,
                StoppingPower = t.additionalStoppingPower, DamageOverride = t.damageDefOverride?.defName ?? "",
                Moods = t.bondedThought != null || t.killThought != null || !t.equippedHediffs.NullOrEmpty() || !t.bondedHediffs.NullOrEmpty()
            };
            if (t.extraDamages != null)
                foreach (var e in t.extraDamages)
                    if (e?.def != null) f.Extras.Add(new WeaponExtraFacts { Def = e.def.defName, Amount = e.amount, ArmourPen = e.armorPenetration, Chance = e.chance });
            if (t.equippedStatOffsets != null)
                foreach (var m in t.equippedStatOffsets)
                    if (m?.stat != null) f.Equipped.Add(new WeaponPassive(m.stat.defName, m.value));
            if (t.statOffsets != null)
                foreach (var m in t.statOffsets)
                    if (m?.stat != null) f.StatWords.Add((m.stat.label ?? m.stat.defName) + " " + m.ValueToStringAsOffset);
            if (t.statFactors != null)
                foreach (var m in t.statFactors)
                    if (m?.stat != null) f.StatWords.Add((m.stat.label ?? m.stat.defName) + " " + m.ToStringAsFactor);
            return f;
        }

        private static HashSet<ThingDef> made;
        private static HashSet<string> kindTags;

        /// <summary>A recipe makes it (a bench's recipe, or its own recipe maker).</summary>
        private static bool Made(ThingDef def)
        {
            if (made == null)
            {
                made = new HashSet<ThingDef>();
                foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                    if (r?.products != null)
                        foreach (var p in r.products)
                            if (p?.thingDef != null) made.Add(p.thingDef);
            }
            return def.recipeMaker != null || made.Contains(def);
        }

        /// <summary>A faction's people carry it: a pawn kind's weapon tags take one of its own.</summary>
        private static bool Carried(ThingDef def)
        {
            if (kindTags == null)
            {
                kindTags = new HashSet<string>();
                foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
                    if (k?.weaponTags != null)
                        foreach (var t in k.weaponTags)
                            if (t != null) kindTags.Add(t);
            }
            return def.weaponTags != null && def.weaponTags.Any(kindTags.Contains);
        }

        // ---------------------------------------------------------------- reflection, softly

        private static object Field(object o, string name)
        {
            if (o == null) return null;
            try
            {
                return o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o);
            }
            catch
            {
                return null;
            }
        }

        private static float? Num(object o, string name)
        {
            switch (Field(o, name))
            {
                case float f: return f;
                case int i: return i;
                case double d: return (float)d;
                default: return null;
            }
        }

        private static bool Flag(object o, string name) => Field(o, name) is bool b && b;

        // ---------------------------------------------------------------- a colonist

        /// <summary>A colonist as a fighter: their skills, their real stats, the weapon in their hands, their traits and body. Kept an hour.</summary>
        public static FighterFacts Fighter(Pawn p)
        {
            if (p == null) return null;
            int now = Now;
            if (fighters.TryGetValue(p.thingIDNumber, out var kept) && now - kept.tick < KeepTicks && now >= kept.tick) return kept.facts;
            if (fighters.Count > 200) fighters.Clear();
            FighterFacts f;
            try
            {
                f = ReadFighter(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist as a fighter, for the weapon rating", e);
                f = new FighterFacts { Name = p.LabelShort };
            }
            fighters[p.thingIDNumber] = (now, f);
            return f;
        }

        private static FighterFacts ReadFighter(Pawn p)
        {
            var f = new FighterFacts
            {
                Name = p.LabelShort, CanFight = !p.WorkTagIsDisabled(WorkTags.Violent),
                ShootingLevel = p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 8, MeleeLevel = p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 8,
                Brawler = p.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false
            };
            if (p.skills != null)
            {
                f.ShootingAccuracy = p.GetStatValue(StatDefOf.ShootingAccuracyPawn);
                f.MeleeHitChance = p.GetStatValue(StatDefOf.MeleeHitChance);
            }
            f.AimFactor = p.GetStatValue(StatDefOf.AimingDelayFactor);
            f.RangedCooldownFactor = p.GetStatValue(StatDefOf.RangedCooldownFactor);
            f.MeleeCooldownFactor = p.GetStatValue(StatDefOf.MeleeCooldownFactor);
            f.MeleeDamageFactor = p.GetStatValue(StatDefOf.MeleeDamageFactor);
            if (p.equipment != null)
                foreach (var eq in p.equipment.AllEquipmentListForReading)
                {
                    if (eq == null) continue;
                    f.HeldShooting += StatWorker.StatOffsetFromGear(eq, StatDefOf.ShootingAccuracyPawn);
                    f.HeldMeleeHit += StatWorker.StatOffsetFromGear(eq, StatDefOf.MeleeHitChance);
                    f.HeldAim += StatWorker.StatOffsetFromGear(eq, StatDefOf.AimingDelayFactor);
                    f.HeldRangedCooldown += StatWorker.StatOffsetFromGear(eq, StatDefOf.RangedCooldownFactor);
                    f.HeldMeleeCooldown += StatWorker.StatOffsetFromGear(eq, StatDefOf.MeleeCooldownFactor);
                }
            if (p.story?.traits?.allTraits != null)
                foreach (var t in p.story.traits.allTraits)
                {
                    var data = t?.CurrentData;
                    if (data == null || t.Suppressed) continue;
                    var ft = new FighterTrait
                    {
                        Label = t.Label ?? "",
                        Shooting = data.statOffsets.GetStatOffsetFromList(StatDefOf.ShootingAccuracyPawn),
                        MeleeHit = data.statOffsets.GetStatOffsetFromList(StatDefOf.MeleeHitChance),
                        AimFactor = data.statFactors.GetStatFactorFromList(StatDefOf.AimingDelayFactor) + data.statOffsets.GetStatOffsetFromList(StatDefOf.AimingDelayFactor),
                        RangedCooldownFactor = data.statFactors.GetStatFactorFromList(StatDefOf.RangedCooldownFactor) + data.statOffsets.GetStatOffsetFromList(StatDefOf.RangedCooldownFactor),
                        MeleeCooldownFactor = data.statFactors.GetStatFactorFromList(StatDefOf.MeleeCooldownFactor) + data.statOffsets.GetStatOffsetFromList(StatDefOf.MeleeCooldownFactor),
                        MeleeDamageFactor = data.statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor) + data.statOffsets.GetStatOffsetFromList(StatDefOf.MeleeDamageFactor)
                    };
                    if (ft.Ranged || ft.Melee) f.Traits.Add(ft);
                }
            if (p.def?.tools != null && p.def.tools.Count > 0)
            {
                var natural = new List<WeaponMeleeAttack>();
                foreach (var t in p.def.tools)
                {
                    if (t?.capacities == null) continue;
                    foreach (var c in t.capacities)
                    {
                        var m = c == null ? null : Maneuver(c);
                        var dmg = m?.verb?.meleeDamageDef;
                        string cat = dmg?.armorCategory?.defName ?? "";
                        natural.Add(new WeaponMeleeAttack
                        {
                            Label = t.label, Power = t.power, Cooldown = t.cooldownTime, ArmourPen = t.armorPenetration, ChanceFactor = t.chanceFactor, Natural = true,
                            Armour = cat == "Sharp" ? WeaponArmour.Sharp : cat == "Heat" ? WeaponArmour.Heat : cat == "Blunt" ? WeaponArmour.Blunt : WeaponArmour.None
                        });
                        break;
                    }
                }
                if (natural.Count > 0) f.Natural = natural;
            }
            return f;
        }

        /// <summary>A weapon's rating for a colonist and why it differs. Kept an hour; null if they can't fight.</summary>
        public static WeaponForColonist ForColonist(Pawn p, WeaponReport r)
        {
            if (p == null || r == null) return null;
            var facts = Fighter(p);
            if (facts == null || !facts.CanFight) return null;
            int now = Now;
            var key = (p.thingIDNumber, r);
            if (forColonist.TryGetValue(key, out var kept) && now - kept.tick < KeepTicks && now >= kept.tick) return kept.rating;
            if (forColonist.Count > 600) forColonist.Clear();
            WeaponForColonist c = null;
            try
            {
                c = WeaponForMath.For(r, facts);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a weapon's rating for a colonist", e);
            }
            forColonist[key] = (now, c);
            return c;
        }

        /// <summary>Why a colonist can't use a weapon at all (incapable of violence, biocoded to someone else, a heavy weapon they can't carry), or null.</summary>
        public static string CantUse(Pawn p, Thing weapon, WeaponReport r)
        {
            if (p == null || r == null) return null;
            if (p.WorkTagIsDisabled(WorkTags.Violent)) return "CC_Weapon_CantFight".Translate(p.Named("PAWN")).Resolve();
            var code = (weapon as ThingWithComps)?.GetComp<CompBiocodable>();
            if (code != null && code.Biocoded && code.CodedPawn != p)
                return "CC_Weapon_CodedToOther".Translate(p.Named("PAWN"), (code.CodedPawn?.LabelShort ?? code.CodedPawnLabel ?? "?").Named("OTHER")).Resolve();
            if (Heavy(r) && HeavyCarriedBy(p, r) == null) return "CC_Weapon_TooHeavyFor".Translate(p.Named("PAWN")).Resolve();
            return null;
        }

        /// <summary>A heavy weapon of Vanilla Expanded Framework's: only someone with its trait (Tough), armour (an exoframe) or gene may carry it.</summary>
        public static bool Heavy(WeaponReport r) => r?.Def != null && r.Def.Extensions.Any(x => x.Class == "HeavyWeapon" && x.B("isHeavy"));

        /// <summary>What lets a colonist carry a heavy weapon: their trait (Tough), their armour or their gene, by name; null if nothing does.</summary>
        public static string HeavyCarriedBy(Pawn p, WeaponReport r)
        {
            if (p == null || r?.Def == null) return null;
            foreach (var x in r.Def.Extensions)
            {
                if (x.Class != "HeavyWeapon" || !x.B("isHeavy")) continue;
                foreach (var t in x.L("supportedTraits"))
                {
                    var trait = p.story?.traits?.allTraits?.FirstOrDefault(tr => tr?.def?.defName == t);
                    if (trait != null) return trait.Label;
                }
                foreach (var a in x.L("supportedArmors"))
                {
                    var worn = p.apparel?.WornApparel?.FirstOrDefault(w => w?.def?.defName == a);
                    if (worn != null) return worn.LabelNoCount;
                }
                foreach (var g in x.L("supportedGenes"))
                {
                    var gene = p.genes?.GenesListForReading?.FirstOrDefault(ge => ge?.def?.defName == g && ge.Active);
                    if (gene != null) return gene.Label;
                }
            }
            return null;
        }
    }
}
