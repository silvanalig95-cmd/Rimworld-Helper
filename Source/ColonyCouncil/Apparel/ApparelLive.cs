using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The apparel rating in the game (docs/design/apparel.md, step 2): a piece's facts read from its live def (patches in
    /// it) and the piece in hand (its quality and stuff by the game's own stats, its hit points and the moods its wear and a
    /// dead wearer give, A RimWorld of Magic's enchantment as rolled on it, whom it is biocoded to), rated by the same
    /// reading the review runs (Apparel/ApparelReadMath.cs), so a piece at normal quality in its usual stuff rates as
    /// docs/apparel/apparel.json has it. Kept by def, stuff and quality, and by the piece itself only where it differs from
    /// its kind (worn below half its hit points, taken from the dead, enchanted, biocoded); a colonist's rating is kept an
    /// hour of the game's time. Nothing is worked out per frame once a piece has been seen.
    /// </summary>
    public static class ApparelLive
    {
        /// <summary>"Apparel ratings": the rating anywhere (the info card, Rimfolk's card, the gear tab).</summary>
        public static bool On => ColonyCouncilMod.Settings?.apparelRatings ?? true;

        /// <summary>How long a piece in hand's own reading and a colonist's rating are kept: an hour of the game's time.</summary>
        public const int KeepTicks = 2500;

        private static readonly Dictionary<(ThingDef, ThingDef, QualityCategory), ApparelReport> byDef = new Dictionary<(ThingDef, ThingDef, QualityCategory), ApparelReport>();
        private static readonly Dictionary<int, (int tick, int stage, bool corpse, ApparelReport report)> byThing = new Dictionary<int, (int, int, bool, ApparelReport)>();
        private static readonly Dictionary<(int, ApparelReport, int), (int tick, ApparelWearer wearer, ApparelForColonist rating)> forColonist =
            new Dictionary<(int, ApparelReport, int), (int, ApparelWearer, ApparelForColonist)>();
        private static readonly Dictionary<string, ApparelStuffFacts> stuffs = new Dictionary<string, ApparelStuffFacts>();
        private static readonly Dictionary<string, ApparelHediffFacts> hediffs = new Dictionary<string, ApparelHediffFacts>();

        /// <summary>Forgets every rating kept (a new game, the settings changed).</summary>
        public static void Reset()
        {
            byDef.Clear();
            byThing.Clear();
            forColonist.Clear();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------------------------------------------------------- which pieces

        /// <summary>A piece a colonist can wear and the rating reads: apparel with layers that isn't gone once taken off (a mechanoid's, a power's).</summary>
        public static bool Rateable(ThingDef def) => def != null && def.IsApparel && def.apparel != null && !def.destroyOnDrop && !def.apparel.layers.NullOrEmpty();

        private static ThingDef Named(string defName) => string.IsNullOrEmpty(defName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        private static List<string> Categories(ThingDef def) => def.stuffCategories?.Where(c => c != null).Select(c => c.defName).ToList() ?? new List<string>();

        /// <summary>The stuff a kind is rated in when none is given: devilstrand where it takes fabric, else steel, plain leather, wood, granite (the review's), else the game's default.</summary>
        public static ThingDef DefaultStuff(ThingDef def)
        {
            if (def == null || !def.MadeFromStuff) return null;
            string usual = ApparelRead.UsualStuff(Categories(def), s => Named(s) != null);
            return Named(usual) ?? GenStuff.DefaultStuffFor(def);
        }

        /// <summary>The colonist who wears or carries a piece, or null.</summary>
        public static Pawn Holder(Thing t)
        {
            switch (t?.ParentHolder)
            {
                case Pawn_ApparelTracker worn: return worn.pawn;
                case Pawn_InventoryTracker inv: return inv.pawn;
                case Pawn_EquipmentTracker eq: return eq.pawn;
                default: return null;
            }
        }

        // ---------------------------------------------------------------- the reports

        private static QualityCategory Quality(Thing t) => t.TryGetQuality(out var q) ? q : QualityCategory.Normal;

        /// <summary>The stage of the apparel-damaged mood a piece gives as it is (-1 none, 0 below half its hit points, 1 below a fifth), by the game's rule.</summary>
        public static int DamageStage(Thing t)
        {
            if (t?.def?.apparel == null || !t.def.useHitPoints || !t.def.apparel.careIfDamaged || t.MaxHitPoints <= 0) return -1;
            float s = (float)t.HitPoints / t.MaxHitPoints;
            return s < ApparelRead.TatteredBelow ? 1 : s < ApparelRead.FrayedBelow ? 0 : -1;
        }

        private static bool FromTheDead(Thing t) => t is Apparel ap && ap.WornByCorpse && ap.def.apparel.careIfWornByCorpse;

        private static readonly Dictionary<ThingDef, bool> enchantable = new Dictionary<ThingDef, bool>();

        /// <summary>A piece whose comps hold values of its own (A RimWorld of Magic's enchantment, rolled or worked on it).</summary>
        private static bool OwnComps(ThingDef def)
        {
            if (!enchantable.TryGetValue(def, out var own))
            {
                own = def.comps != null && def.comps.Any(c => c != null && c.GetType().FullName == "TorannMagic.Enchantment.CompProperties_EnchantedItem");
                enchantable[def] = own;
            }
            return own;
        }

        private static bool Biocoded(Thing t) => (t as ThingWithComps)?.GetComp<CompBiocodable>()?.Biocoded ?? false;

        /// <summary>The piece in hand, read and rated; null for one the rating doesn't read (or with the setting off).</summary>
        public static ApparelReport Of(Thing t)
        {
            if (t == null || !On || !Rateable(t.def)) return null;
            int stage = DamageStage(t);
            bool corpse = FromTheDead(t);
            if (stage < 0 && !corpse && !OwnComps(t.def) && !Biocoded(t)) return Of(t.def, t.Stuff, Quality(t));
            int now = Now;
            if (byThing.TryGetValue(t.thingIDNumber, out var kept) && kept.stage == stage && kept.corpse == corpse && now - kept.tick < KeepTicks && now >= kept.tick)
                return kept.report;
            if (byThing.Count > 400) byThing.Clear();
            var r = Build(t.def, t.Stuff, Quality(t), t);
            byThing[t.thingIDNumber] = (now, stage, corpse, r);
            return r;
        }

        /// <summary>A kind in a stuff (its usual one when none) at a quality, read and rated.</summary>
        public static ApparelReport Of(ThingDef def, ThingDef stuff, QualityCategory q = QualityCategory.Normal)
        {
            if (!On || !Rateable(def)) return null;
            if (stuff == null && def.MadeFromStuff) stuff = DefaultStuff(def);
            var key = (def, stuff, q);
            if (byDef.TryGetValue(key, out var r)) return r;
            r = Build(def, stuff, q, null);
            byDef[key] = r;
            return r;
        }

        private static ApparelReport Build(ThingDef def, ThingDef stuff, QualityCategory q, Thing thing)
        {
            try
            {
                return ApparelRead.Full(FactsFor(def, stuff, q, thing));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the apparel rating of " + def?.defName, e);
                return null;
            }
        }

        // ---------------------------------------------------------------- the live def as facts

        private static float StatOf(StatDef s, StatRequest req, float fallback)
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

        private static Dictionary<string, float> Mods(List<StatModifier> list)
        {
            var d = new Dictionary<string, float>();
            if (list != null)
                foreach (var m in list)
                    if (m?.stat != null) d[m.stat.defName] = m.value;
            return d;
        }

        /// <summary>
        /// A quality's factor as the game's own stats give it: the stat's value for this piece over its value from its base
        /// and stuff alone (the rating's own sum), from the first of the stats that isn't held at its cap; 1 where the game
        /// changes nothing.
        /// </summary>
        private static float QualityFactor(ThingDef def, ApparelStuffFacts stuff, StatRequest req, params (StatDef stat, string baseKey, string powerKey, string multKey)[] stats)
        {
            var bases = Mods(def.statBases);
            float best = -1f;
            foreach (var (stat, baseKey, powerKey, multKey) in stats)
            {
                float raw = ApparelRateMath.Stat(bases, baseKey) + ApparelRateMath.Stat(stuff?.Powers, powerKey) * ApparelRateMath.Stat(bases, multKey);
                if (raw <= 0.001f) continue;
                float game = StatOf(stat, req, raw);
                float f = game / raw;
                if (game < 1.999f || stat.maxValue > 100f)
                {
                    best = f;
                    break;
                }
                best = Math.Max(best, f);
            }
            return best <= 0f || Math.Abs(best - 1f) < 0.0001f ? 1f : best;
        }

        /// <summary>A stuff's label, powers and flammability, as the review reads them from its def (once each).</summary>
        public static ApparelStuffFacts StuffFacts(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (stuffs.TryGetValue(defName, out var known)) return known;
            var s = Named(defName);
            ApparelStuffFacts f = null;
            if (s != null && s.IsStuff)
            {
                f = new ApparelStuffFacts { Def = defName, Label = s.label ?? defName };
                if (s.statBases != null)
                    foreach (var m in s.statBases)
                        if (m?.stat != null && m.stat.defName.StartsWith("StuffPower_")) f.Powers[m.stat.defName] = m.value;
                var factors = Mods(s.stuffProps?.statFactors);
                f.Flammability = factors.TryGetValue("Flammability", out var fl) ? fl : 1f;
            }
            stuffs[defName] = f;
            return f;
        }

        /// <summary>A hediff's stages and comps, as the review reads them (once each).</summary>
        public static ApparelHediffFacts HediffFacts(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (hediffs.TryGetValue(name, out var known)) return known;
            var h = DefDatabase<HediffDef>.GetNamedSilentFail(name);
            ApparelHediffFacts f = null;
            if (h != null)
            {
                f = new ApparelHediffFacts { Name = name, Label = h.label ?? name };
                if (h.stages != null)
                    foreach (var st in h.stages)
                    {
                        if (st == null) continue;
                        var stage = new ApparelHediffStage
                        {
                            MinSeverity = st.minSeverity, Offsets = Mods(st.statOffsets), Factors = Mods(st.statFactors), PainOffset = st.painOffset,
                            BleedFactor = st.totalBleedFactor
                        };
                        if (st.capMods != null)
                            foreach (var c in st.capMods)
                                if (c?.capacity != null) stage.Capacities.Add((c.capacity.defName, c.offset, c.postFactor, c.setMax >= 998f ? -1f : c.setMax));
                        f.Stages.Add(stage);
                    }
                if (h.comps != null)
                    foreach (var c in h.comps)
                        if (c != null) f.CompClasses.Add(c.GetType() == typeof(HediffCompProperties) ? "" : WeaponLive.NameOf(c.GetType()));
            }
            hediffs[name] = f;
            return f;
        }

        /// <summary>The stats the review turns factors into offsets with: a human's own value, else the stat's default.</summary>
        private static readonly string[] DefaultStats =
        {
            "MoveSpeed", "PsychicEntropyMax", "PsychicEntropyRecoveryRate", "MeditationFocusGain", "CarryingCapacity", "PsychicSensitivity",
            "PainShockThreshold", "MentalBreakThreshold", "MechBandwidth", "WorkSpeedGlobal", "ToxicResistance", "ToxicEnvironmentResistance",
            "VacuumResistance", "HuntingStealth", "SocialImpact", "NegotiationAbility", "TradePriceImprovement", "GlobalLearningFactor",
            "RestRateMultiplier", "ImmunityGainSpeed", "IncomingDamageFactor", "MeleeDodgeChance", "ShootingAccuracyPawn", "MeleeHitChance",
            "AimingDelayFactor", "RangedCooldownFactor", "MeleeDamageFactor", "SuppressionPower", "ResearchSpeed", "MedicalTendQuality",
            "MedicalTendSpeed", "MedicalSurgerySuccessChance", "ConstructionSpeed"
        };

        private static Dictionary<string, float> statDefaults;

        private static Dictionary<string, float> StatDefaults()
        {
            if (statDefaults != null) return statDefaults;
            var d = new Dictionary<string, float>();
            var human = Mods(ThingDefOf.Human?.statBases);
            foreach (var name in DefaultStats)
            {
                var s = DefDatabase<StatDef>.GetNamedSilentFail(name);
                if (s == null) continue;
                d[name] = human.TryGetValue(name, out var v) ? v : s.defaultBaseValue;
            }
            statDefaults = d;
            return d;
        }

        /// <summary>
        /// The facts of a piece: its def's stats, comps, extensions, verbs and abilities as the game has loaded them (other
        /// mods' patches in them), its quality's factors on armour and insulation by the game's own stats, its equipped stat
        /// offsets as the game gives them for this piece, and for the piece in hand its hit points, a dead wearer, the moods
        /// those give, its comps' own values and whom it is biocoded to.
        /// </summary>
        public static ApparelDefFacts FactsFor(ThingDef def, ThingDef stuff, QualityCategory q, Thing thing)
        {
            var req = thing != null ? StatRequest.For(thing) : StatRequest.For(def, stuff, q);
            var mod = def.modContentPack;
            var ap = def.apparel;
            var cats = Categories(def);
            string usual = stuff?.defName ?? "";
            var f = new ApparelDefFacts
            {
                DefName = def.defName, Label = def.label ?? def.defName, Mod = (mod?.PackageId ?? "").Replace("_steam", ""), ModName = mod?.Name ?? "",
                TechLevel = def.techLevel.ToString(), ThingClass = WeaponLive.NameOf(def.thingClass), StatBases = Mods(def.statBases),
                StuffCategories = cats, Layers = ap.layers.Where(l => l != null).Select(l => l.defName).ToList(),
                Groups = ap.bodyPartGroups?.Where(g => g != null).Select(g => g.defName).ToList() ?? new List<string>(),
                Tags = ap.tags?.Where(t => t != null).ToList() ?? new List<string>(),
                ThingCategories = def.thingCategories?.Where(c => c != null).Select(c => c.defName).ToList() ?? new List<string>(),
                StatDefaults = StatDefaults(), StuffOf = StuffFacts, HediffOf = HediffFacts,
                CountsAsClothes = ap.countsAsClothingForNudity, CareIfDamaged = ap.careIfDamaged, CareIfWornByCorpse = ap.careIfWornByCorpse,
                SlaveApparel = ap.slaveApparel, MechanitorApparel = ap.mechanitorApparel, ImmuneToToxGas = ap.immuneToToxGasExposure,
                BlocksVision = ap.blocksVision, IgnoredByNonViolent = ap.ignoredByNonViolent, WearPerDay = ap.wearPerDay,
                Gender = ap.gender == Gender.None ? "" : ap.gender.ToString(), Ages = ap.developmentalStageFilter.ToString(),
                Stuff = usual, OtherStuffs = def.MadeFromStuff ? ApparelRead.OtherStuffsFor(cats, ApparelRead.UsualStuff(cats, s => Named(s) != null), s => Named(s) != null) : new List<string>(),
                UsesHitPoints = def.useHitPoints,
            };
            if (usual != "" && !f.OtherStuffs.Contains(usual)) f.OtherStuffs.Add(usual);

            // Its equipped stat offsets as the game gives them for this piece (StatWorker.StatOffsetFromGear: the stat's parts on them).
            if (def.equippedStatOffsets != null)
                foreach (var m in def.equippedStatOffsets)
                {
                    if (m?.stat == null) continue;
                    float v = m.value;
                    try
                    {
                        if (thing != null) v = StatWorker.StatOffsetFromGear(thing, m.stat);
                        else if (Math.Abs(v) > float.Epsilon && m.stat.parts != null)
                            foreach (var part in m.stat.parts) part.TransformValue(req, ref v);
                    }
                    catch
                    {
                        v = m.value;
                    }
                    f.EquippedOffsets[m.stat.defName] = v;
                }

            // Comps: the piece in hand's own (an enchantment rolled on it), else its def's.
            var own = thing is ThingWithComps twc && twc.AllComps != null ? twc.AllComps.Where(c => c?.props != null).ToList() : null;
            if (def.comps != null)
                foreach (var p in def.comps)
                {
                    if (p == null) continue;
                    var part = PartFacts(p, p.GetType() == typeof(CompProperties) ? WeaponLive.NameOf(p.compClass) : WeaponLive.NameOf(p.GetType()));
                    part.CompClass = WeaponLive.NameOf(p.compClass);
                    var comp = own?.FirstOrDefault(c => c.props == p);
                    if (comp != null) Overlay(part, comp);
                    f.Comps.Add(part);
                }
            if (def.modExtensions != null)
                foreach (var x in def.modExtensions)
                    if (x != null) f.Extensions.Add(PartFacts(x, WeaponLive.NameOf(x.GetType())));
            if (def.Verbs != null)
                foreach (var v in def.Verbs)
                    if (v != null) f.Verbs.Add(VerbFacts(v));
            Abilities(def, f);
            f.Rules = RulesFor(def);
            f.Made = Made(def);
            f.WornByKinds = Worn(def);
            var rm = def.recipeMaker;
            if (rm != null)
            {
                var research = new List<ResearchProjectDef>();
                if (rm.researchPrerequisite != null) research.Add(rm.researchPrerequisite);
                if (rm.researchPrerequisites != null) research.AddRange(rm.researchPrerequisites.Where(x => x != null));
                f.Research.AddRange(research.Select(x => x.defName));
                if (research.Count > 0) f.ResearchTech = research.Max(x => x.techLevel).ToString();
            }

            // Its quality, by the game's own armour and insulation for this piece.
            var stuffFacts = StuffFacts(usual);
            f.ArmourQuality = QualityFactor(def, stuffFacts, req,
                (StatDefOf.ArmorRating_Sharp, "ArmorRating_Sharp", "StuffPower_Armor_Sharp", "StuffEffectMultiplierArmor"),
                (StatDefOf.ArmorRating_Blunt, "ArmorRating_Blunt", "StuffPower_Armor_Blunt", "StuffEffectMultiplierArmor"),
                (StatDefOf.ArmorRating_Heat, "ArmorRating_Heat", "StuffPower_Armor_Heat", "StuffEffectMultiplierArmor"));
            f.InsulationQuality = QualityFactor(def, stuffFacts, req,
                (StatDefOf.Insulation_Cold, "Insulation_Cold", "StuffPower_Insulation_Cold", "StuffEffectMultiplierInsulation_Cold"),
                (StatDefOf.Insulation_Heat, "Insulation_Heat", "StuffPower_Insulation_Heat", "StuffEffectMultiplierInsulation_Heat"));

            // The moods the game's own thoughts give for a piece as it is: their numbers, and the apparel-damaged stages' own
            // labels (the dead wearer's carries the piece's name in the game's, so it is said in Rimfolk's words).
            f.Frayed = (StageLabel(ThoughtDefOf.ApparelDamaged, 0, "CC_Apparel_Mood_Frayed"), StageMood(ThoughtDefOf.ApparelDamaged, 0));
            f.Tattered = (StageLabel(ThoughtDefOf.ApparelDamaged, 1, "CC_Apparel_Mood_Tattered"), StageMood(ThoughtDefOf.ApparelDamaged, 1));
            f.DeadMans = ("CC_Apparel_Mood_DeadMans".Translate().Resolve(), StageMood(ThoughtDefOf.DeadMansApparel, 0));
            if (thing != null)
            {
                f.HitPoints = thing.HitPoints;
                f.MaxHitPoints = thing.MaxHitPoints;
                f.WornByCorpse = thing is Apparel worn && worn.WornByCorpse;
                var code = (thing as ThingWithComps)?.GetComp<CompBiocodable>();
                if (code != null && code.Biocoded) f.BiocodedTo = code.CodedPawn?.LabelShort ?? code.CodedPawnLabel ?? "";
            }
            return f;
        }

        /// <summary>A thought's stage's own label, or Rimfolk's words for it.</summary>
        public static string StageLabel(ThoughtDef t, int stage, string fallbackKey)
        {
            string l = t?.stages != null && stage >= 0 && stage < t.stages.Count ? t.stages[stage]?.label : null;
            return l.NullOrEmpty() || l.Contains("{") ? fallbackKey.Translate().Resolve() : l;
        }

        /// <summary>A thought's stage's mood, or 0.</summary>
        public static float StageMood(ThoughtDef t, int stage) => t?.stages != null && stage >= 0 && stage < t.stages.Count && t.stages[stage] != null ? t.stages[stage].baseMoodEffect : 0f;

        private static ApparelVerbFacts VerbFacts(VerbProperties v)
        {
            var f = new ApparelVerbFacts
            {
                VerbClass = v.verbClass == null || v.verbClass == typeof(Verb) ? "" : WeaponLive.NameOf(v.verbClass), Label = v.label ?? "", Range = v.range,
                ForcedMiss = v.ForcedMissRadius, CanGoWild = v.canGoWild, Burst = v.burstShotCount, TicksBetween = v.ticksBetweenBurstShots,
                Warmup = v.warmupTime, Cooldown = v.defaultCooldownTime
            };
            var p = v.defaultProjectile;
            if (p != null)
            {
                var pr = p.projectile;
                var proj = new ApparelProjectileFacts { DefName = p.defName, NoProjectile = pr == null };
                if (pr != null)
                {
                    proj.DamageDef = pr.damageDef?.defName ?? "";
                    if (pr.damageDef != null)
                    {
                        proj.Damage = pr.GetDamageAmount(1f, null);
                        proj.ArmourPen = pr.GetArmorPenetration(null);
                        proj.ArmourCategory = pr.damageDef.armorCategory?.defName ?? "";
                        proj.HarmsHealth = pr.damageDef.harmsHealth;
                    }
                    proj.StoppingPower = pr.stoppingPower == 0f && pr.damageDef != null ? pr.damageDef.defaultStoppingPower : pr.stoppingPower;
                    proj.ExplosionRadius = pr.explosionRadius;
                    proj.ExplosionDelayTicks = pr.explosionDelay;
                    proj.ChanceToStartFire = pr.explosionChanceToStartFire;
                    proj.PostGas = pr.postExplosionGasType?.ToString() ?? "";
                }
                f.Projectile = proj;
            }
            return f;
        }

        /// <summary>The abilities it grants: the game's apparel abilities, and a comp's list or single ability (VEF's, A RimWorld of Magic's).</summary>
        private static void Abilities(ThingDef def, ApparelDefFacts f)
        {
            var defs = new List<Def>();
            if (def.apparel.abilities != null) defs.AddRange(def.apparel.abilities.Where(a => a != null));
            if (def.comps != null)
                foreach (var c in def.comps)
                {
                    if (c == null) continue;
                    foreach (var name in new[] { "abilities", "abilityDefs", "Abilities", "MagicAbilities" })
                        if (Field(c, name) is IEnumerable list && !(list is string))
                            foreach (var x in list)
                                if (x is Def d) defs.Add(d);
                    if (Field(c, "abilityDef") is Def one) defs.Add(one);
                }
            foreach (var a in defs)
            {
                var ab = new ApparelAbilityFacts { DefName = a.defName, Label = a.label ?? "" };
                if (Field(a, "verbProperties") is VerbProperties vp) ab.VerbRange = vp.range;
                if (Field(a, "cooldownTicksRange") is IntRange cd) ab.CooldownTicks = (cd.min + cd.max) / 2f;
                f.Abilities.Add(ab);
            }
        }

        /// <summary>A comp's or extension's public fields: simple ones as text, lists of names, stat lists as maps; every key it writes.</summary>
        private static ApparelPartFacts PartFacts(object o, string cls)
        {
            var f = new ApparelPartFacts { Class = cls };
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
                string n = fi.Name;
                switch (v)
                {
                    case float x: f.Fields[n] = x.ToString(CultureInfo.InvariantCulture); f.Present.Add(n); break;
                    case int x: f.Fields[n] = x.ToString(CultureInfo.InvariantCulture); f.Present.Add(n); break;
                    case bool x: f.Fields[n] = x ? "true" : "false"; f.Present.Add(n); break;
                    case string x: f.Fields[n] = x; f.Present.Add(n); break;
                    case Enum x: f.Fields[n] = x.ToString(); f.Present.Add(n); break;
                    case Def x: f.Fields[n] = x.defName; f.Present.Add(n); break;
                    case Type x: f.Fields[n] = WeaponLive.NameOf(x); f.Present.Add(n); break;
                    case IntRange x: f.Fields[n] = x.min + "~" + x.max; f.Present.Add(n); break;
                    case List<StatModifier> mods:
                        if (mods.Count > 0)
                        {
                            f.Maps[n] = Mods(mods);
                            f.Present.Add(n);
                        }
                        break;
                    case IEnumerable list:
                        var names = new List<string>();
                        int count = 0;
                        foreach (var e in list)
                        {
                            count++;
                            if (e is Def d) names.Add(d.defName);
                            else if (e is string s) names.Add(s);
                            else if (e is Enum en) names.Add(en.ToString());
                        }
                        if (names.Count > 0) f.Lists[n] = names;
                        if (count > 0) f.Present.Add(n);
                        break;
                    default:
                        f.Present.Add(n);
                        break;
                }
            }
            return f;
        }

        /// <summary>The piece in hand's own comp: its numbers and flags of the same names as its props' (an enchantment rolled or worked on it) take their place.</summary>
        private static void Overlay(ApparelPartFacts part, ThingComp comp)
        {
            foreach (var fi in comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!part.Fields.ContainsKey(fi.Name)) continue;
                object v;
                try
                {
                    v = fi.GetValue(comp);
                }
                catch
                {
                    continue;
                }
                switch (v)
                {
                    case float x: part.Fields[fi.Name] = x.ToString(CultureInfo.InvariantCulture); break;
                    case int x: part.Fields[fi.Name] = x.ToString(CultureInfo.InvariantCulture); break;
                    case bool x: part.Fields[fi.Name] = x ? "true" : "false"; break;
                    case Def x: part.Fields[fi.Name] = x.defName; break;
                }
            }
        }

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

        // ---------------------------------------------------------------- titles and faiths, how it comes about

        private sealed class Rule
        {
            public string Source, Label;
            public float Seniority;
            public List<string> Groups = new List<string>(), RequiredTags = new List<string>(), AllowedTags = new List<string>(), RequiredDefs = new List<string>();
        }

        private static List<Rule> rules;

        private static List<string> Names(object list)
        {
            var l = new List<string>();
            if (list is IEnumerable e)
                foreach (var x in e)
                    if (x is Def d) l.Add(d.defName);
                    else if (x is string s) l.Add(s);
            return l;
        }

        /// <summary>Every apparel rule of the loaded defs, as the review reads them: royal titles' required apparel, memes' and roles' (once).</summary>
        private static List<Rule> Rules()
        {
            if (rules != null) return rules;
            var list = new List<Rule>();
            void Read(Def d, string source, float seniority)
            {
                foreach (var path in new[] { "requiredApparel", "apparelRequirements", "roleApparelRequirements" })
                    if (Field(d, path) is IEnumerable items)
                        foreach (var li in items)
                        {
                            if (li == null) continue;
                            var rq = Field(li, "requirement") ?? li;
                            list.Add(new Rule
                            {
                                Source = source, Label = d.label ?? d.defName, Seniority = seniority, Groups = Names(Field(rq, "bodyPartGroupsMatchAny")),
                                RequiredTags = Names(Field(rq, "requiredTags")), AllowedTags = Names(Field(rq, "allowedTags")), RequiredDefs = Names(Field(rq, "requiredDefs"))
                            });
                        }
            }
            try
            {
                foreach (var t in DefDatabase<RoyalTitleDef>.AllDefs) Read(t, "RoyalTitleDef", t.seniority);
                foreach (var m in DefDatabase<MemeDef>.AllDefs) Read(m, "MemeDef", 0f);
                foreach (var p in DefDatabase<PreceptDef>.AllDefs) Read(p, "PreceptDef", 0f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the apparel rules of titles and faiths", e);
            }
            rules = list;
            return list;
        }

        /// <summary>The titles' and faiths' apparel rules a piece meets (ApparelRequirement on a def: named, or a required or allowed tag, and a group it matches).</summary>
        private static List<ApparelRuleFacts> RulesFor(ThingDef def)
        {
            var groups = def.apparel.bodyPartGroups?.Where(g => g != null).Select(g => g.defName).ToList() ?? new List<string>();
            var tags = def.apparel.tags ?? new List<string>();
            var met = new List<ApparelRuleFacts>();
            foreach (var r in Rules())
            {
                bool named = r.RequiredDefs.Contains(def.defName) || tags.Any(t => r.RequiredTags.Contains(t) || r.AllowedTags.Contains(t));
                bool required = r.RequiredDefs.Contains(def.defName) || tags.Any(t => r.RequiredTags.Contains(t));
                bool group = groups.Any(g => r.Groups.Contains(g));
                if (named && group) met.Add(new ApparelRuleFacts { Source = r.Source, Label = r.Label, Seniority = r.Seniority, Required = required && group });
            }
            return met;
        }

        private static HashSet<ThingDef> made;
        private static HashSet<ThingDef> wornDefs;
        private static HashSet<string> wornTags;

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

        /// <summary>A faction's people wear it: a pawn kind requires it or its apparel tags take one of its own.</summary>
        private static bool Worn(ThingDef def)
        {
            if (wornDefs == null)
            {
                wornDefs = new HashSet<ThingDef>();
                wornTags = new HashSet<string>();
                foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
                {
                    if (k?.apparelRequired != null) wornDefs.UnionWith(k.apparelRequired.Where(x => x != null));
                    if (k?.apparelTags != null) wornTags.UnionWith(k.apparelTags.Where(x => x != null));
                }
            }
            return wornDefs.Contains(def) || (def.apparel.tags?.Any(wornTags.Contains) ?? false);
        }

        // ---------------------------------------------------------------- a colonist

        /// <summary>What a colonist wears now that this piece would take the place of (the same layer over the same parts), without the piece itself.</summary>
        public static List<Apparel> Replaces(Pawn p, ThingDef def, Thing thing)
        {
            var list = new List<Apparel>();
            if (p?.apparel == null || def?.apparel == null) return list;
            var body = p.RaceProps.body;
            foreach (var w in p.apparel.WornApparel)
                if (w != null && w != thing && !ApparelUtility.CanWearTogether(def, w.def, body)) list.Add(w);
            return list;
        }

        /// <summary>Why a colonist can't wear a piece at all (coded to someone else, their faith's role, made for another age, nothing to wear it on), or null.</summary>
        public static string CantWear(Pawn p, Thing thing, ThingDef def)
        {
            if (p == null || def?.apparel == null) return null;
            if (p.apparel == null) return "CC_Apparel_CantWearAtAll".Translate(p.Named("PAWN")).Resolve();
            if (thing != null && !EquipmentUtility.CanEquip(thing, p, out string why)) return why;
            if (!def.apparel.developmentalStageFilter.Has(p.DevelopmentalStage)) return "CC_Apparel_WrongAge".Translate(p.Named("PAWN")).Resolve();
            if (!ApparelUtility.HasPartsToWear(p, def)) return "CC_Apparel_NoParts".Translate(p.Named("PAWN")).Resolve();
            return null;
        }

        /// <summary>The temperatures where they are: outside now, and the season's coldest night and hottest day (its three twelfths, the sun's daily swing of 7 °C).</summary>
        public static (bool has, float now, float low, float high) Climate(Pawn p)
        {
            try
            {
                PlanetTile tile = p.Tile;
                if (!tile.Valid || Find.WorldGrid == null) return (false, 21f, 21f, 21f);
                float now = p.MapHeld?.mapTemperature != null ? p.MapHeld.mapTemperature.OutdoorTemp : GenTemperature.GetTemperatureAtTile(tile);
                float lat = Find.WorldGrid.LongLatOf(tile).y;
                var mid = GenLocalDate.Season(tile).GetMiddleTwelfth(lat);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var tw in new[] { mid.PreviousTwelfth(), mid, mid.NextTwelfth() })
                {
                    float avg = GenTemperature.AverageTemperatureAtTileForTwelfth(tile, tw);
                    lo = Math.Min(lo, avg);
                    hi = Math.Max(hi, avg);
                }
                return (true, now, lo - SunSwing, hi + SunSwing);
            }
            catch
            {
                return (false, 21f, 21f, 21f);
            }
        }

        /// <summary>How far a day's warmest and coldest hours stray from its average (GenTemperature.OffsetFromSunCycle's 7 °C).</summary>
        public const float SunSwing = 7f;

        private static float Gear(Thing t, StatDef s)
        {
            try
            {
                return StatWorker.StatOffsetFromGear(t, s);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>How much a worn piece lowers its wearer's least comfortable temperature and raises their most (its insulation, by the game's StatPart_GearStatOffset).</summary>
        public static (float warmth, float cooling) Comfort(Thing t)
        {
            if (t == null) return (0f, 0f);
            float warmth = t.GetStatValue(StatDefOf.Insulation_Cold) + Gear(t, StatDefOf.Insulation_Cold) - Gear(t, StatDefOf.ComfyTemperatureMin);
            float cooling = t.GetStatValue(StatDefOf.Insulation_Heat) + Gear(t, StatDefOf.Insulation_Heat) + Gear(t, StatDefOf.ComfyTemperatureMax);
            return (warmth, cooling);
        }

        private static readonly string[] ZoneGroups = ApparelRateMath.Human.SelectMany(z => z.Groups).Distinct().ToArray();

        /// <summary>A colonist as a wearer of a piece: how they fight, the body they have, where they are, how warm they are kept without it and what it replaces, the moods they have already.</summary>
        public static ApparelWearer Wearer(Pawn p, ThingDef def, Thing thing)
        {
            var w = new ApparelWearer { Name = p.LabelShort };
            bool canFight = !p.WorkTagIsDisabled(WorkTags.Violent);
            var shooting = p.skills?.GetSkill(SkillDefOf.Shooting);
            var melee = p.skills?.GetSkill(SkillDefOf.Melee);
            bool canShoot = shooting == null || !shooting.TotallyDisabled;
            var held = p.equipment?.Primary;
            string heldKind = held == null ? "" : held.def.IsRangedWeapon ? "ranged" : held.def.IsMeleeWeapon ? "melee" : "";
            bool brawler = p.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false;
            int sl = shooting?.Level ?? 8, ml = melee?.Level ?? 8;
            w.Kind = ApparelForMath.KindOf(canFight, canShoot, heldKind, brawler, sl, ml);
            w.KindWhy = !canFight ? "CC_Apparel_KindWhy_NoViolence".Translate(p.Named("PAWN")).Resolve()
                : !canShoot ? "CC_Apparel_KindWhy_NoShooting".Translate(p.Named("PAWN")).Resolve()
                : heldKind != "" ? "CC_Apparel_KindWhy_Holds".Translate(p.Named("PAWN"), held.LabelNoCount.Named("WEAPON")).Resolve()
                : brawler ? "CC_Apparel_KindWhy_Brawler".Translate(p.Named("PAWN")).Resolve()
                : "CC_Apparel_KindWhy_Skills".Translate(sl.Named("SHOOTING"), ml.Named("MELEE")).Resolve();

            // The body they have: a group none of their parts carries any more (that their body has at all).
            try
            {
                var have = new HashSet<string>();
                foreach (var part in p.health.hediffSet.GetNotMissingParts())
                    if (part?.groups != null)
                        foreach (var g in part.groups)
                            if (g != null) have.Add(g.defName);
                var exist = new HashSet<string>();
                foreach (var part in p.RaceProps.body.AllParts)
                    if (part?.groups != null)
                        foreach (var g in part.groups)
                            if (g != null) exist.Add(g.defName);
                foreach (var g in ZoneGroups)
                    if (exist.Contains(g) && !have.Contains(g)) w.MissingGroups.Add(g);
                if (w.MissingGroups.Count > 0)
                    w.MissingWhy = string.Join(", ", p.health.hediffSet.GetMissingPartsCommonAncestors().Where(h => h?.Part != null).Select(h => h.Part.Label));
            }
            catch
            {
                w.MissingGroups.Clear();
            }

            // Where they are.
            var (has, now, low, high) = Climate(p);
            w.HasClimate = has;
            w.OutdoorNow = now;
            w.SeasonLow = low;
            w.SeasonHigh = high;

            // How warm and cool they are kept without this piece and what it would replace.
            float min = p.GetStatValue(StatDefOf.ComfyTemperatureMin), max = p.GetStatValue(StatDefOf.ComfyTemperatureMax);
            var replaced = Replaces(p, def, thing);
            var takenOff = new List<Thing>(replaced);
            if (thing != null && p.apparel != null && p.apparel.WornApparel.Contains(thing as Apparel)) takenOff.Add(thing);
            foreach (var t in takenOff)
            {
                var (warmth, cooling) = Comfort(t);
                min += warmth;
                max -= cooling;
            }
            w.ComfyMinWithout = min;
            w.ComfyMaxWithout = max;

            // The moods they have already from what they keep wearing.
            w.Frayed = ("", StageMood(ThoughtDefOf.ApparelDamaged, 0));
            w.Tattered = ("", StageMood(ThoughtDefOf.ApparelDamaged, 1));
            var gender = DefDatabase<ThoughtDef>.GetNamedSilentFail("WrongApparelGender");
            w.WrongGenderMood = ("", StageMood(gender, 0));
            w.WrongGender = !def.apparel.CorrectGenderForWearing(p.gender);
            if (p.apparel != null)
                foreach (var kept in p.apparel.WornApparel)
                {
                    if (kept == null || kept == thing || takenOff.Contains(kept)) continue;
                    if (!kept.def.apparel.CorrectGenderForWearing(p.gender)) w.WrongGenderAlready = true;
                    if (p.apparel.IsLocked(kept)) continue;
                    w.DamageStageKept = Math.Max(w.DamageStageKept, DamageStage(kept));
                }
            return w;
        }

        /// <summary>A piece's rating for a colonist and the wearer it was worked out for. Kept an hour; null if they can't wear clothes at all.</summary>
        public static (ApparelWearer wearer, ApparelForColonist rating) ForColonist(Pawn p, ApparelReport r, Thing thing)
        {
            if (p == null || r?.Def == null || p.apparel == null || !p.RaceProps.Humanlike) return (null, null);
            var def = thing?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(r.Def.DefName);
            if (def?.apparel == null) return (null, null);
            int now = Now;
            var key = (p.thingIDNumber, r, thing?.thingIDNumber ?? -1);
            if (forColonist.TryGetValue(key, out var kept) && now - kept.tick < KeepTicks && now >= kept.tick) return (kept.wearer, kept.rating);
            if (forColonist.Count > 600) forColonist.Clear();
            ApparelWearer w = null;
            ApparelForColonist c = null;
            try
            {
                w = Wearer(p, def, thing);
                c = ApparelForMath.For(r, w);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a piece of apparel's rating for a colonist", e);
            }
            forColonist[key] = (now, w, c);
            return (w, c);
        }
    }
}
