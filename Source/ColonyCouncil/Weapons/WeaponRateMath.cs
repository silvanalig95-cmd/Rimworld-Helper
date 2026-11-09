using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How a damage packet meets armour: by the game's three armour ratings, or not at all.</summary>
    public enum WeaponArmour
    {
        Sharp,
        Blunt,
        Heat,
        /// <summary>No armour category (stun, EMP, smoke, a psychic blow): armour doesn't touch it.</summary>
        None
    }

    /// <summary>What one landed hit of a weapon carries besides its main damage.</summary>
    public enum WeaponEffect
    {
        /// <summary>More damage of its own kind (an extra damage, a frost or a toxic cut), with its own armour roll.</summary>
        Damage,
        /// <summary>The game's Stun damage: half a second out of the fight per point (StunHandler), flesh or metal.</summary>
        Stun,
        /// <summary>EMP: half a second of stun per point on mechanoids, who adapt for 2,200 ticks after; shield belts broken.</summary>
        Emp,
        /// <summary>Sets the target alight with this chance (a flame hit, an incendiary round); mechanoids don't burn.</summary>
        Ignite,
        /// <summary>Toxic buildup, by severity per hit (BulletToxic's additional hediff); mechanoids are immune.</summary>
        ToxicBuildup,
        /// <summary>A lingering gas cloud (tox gas): sickens the unprotected in it; mechanoids are immune.</summary>
        ToxGas
    }

    /// <summary>One thing a landed hit carries: an effect, its amount, its armour piercing and armour kind, and its chance.</summary>
    public sealed class WeaponPayload
    {
        public WeaponEffect Effect;
        public float Amount;
        public float ArmourPen;
        public WeaponArmour Armour = WeaponArmour.None;
        public float Chance = 1f;
        /// <summary>The damage's name, for the specials (frost, toxic, burn).</summary>
        public string Name;

        public static WeaponPayload Of(WeaponEffect effect, float amount, float chance = 1f) =>
            new WeaponPayload { Effect = effect, Amount = amount, Chance = chance };
    }

    /// <summary>
    /// A fighter on the colony's side: shooting and melee skill (the game's skill levels, before its curves) and what their
    /// body and traits change. The reference is a colonist of skill 8 in both with no traits (<see cref="WeaponRateMath.Reference"/>);
    /// trait offsets are added to the skill as the game adds them, before its curve (careful shooter +5 and aiming 1.25,
    /// trigger-happy -5 and aiming 0.5, brawler +4 melee hit, nimble +15 dodge).
    /// </summary>
    public sealed class WeaponFighter
    {
        public float ShootingSkill = 8f;
        public float MeleeSkill = 8f;
        /// <summary>The pawn's AimingDelayFactor (careful shooter 1.25, trigger-happy 0.5).</summary>
        public float AimFactor = 1f;
        public float RangedCooldownFactor = 1f;
        public float MeleeCooldownFactor = 1f;
        public float MeleeDamageFactor = 1f;
        /// <summary>The fighter's own melee attacks, which the game weighs against the weapon's (a human's two fists, head and teeth).</summary>
        public List<WeaponMeleeAttack> Natural = WeaponRateMath.HumanNaturalAttacks();

        public WeaponFighter Copy()
        {
            var f = (WeaponFighter)MemberwiseClone();
            f.Natural = new List<WeaponMeleeAttack>(Natural);
            return f;
        }
    }

    /// <summary>
    /// A reference enemy. Its armour is a few zones, each with the share of hits that land there and the armour of what
    /// covers it (the game rolls each hit against the armour on the part it strikes); size makes a big target easier to
    /// hit (the game's 0.5 to 2 factor), dodge is its melee dodge chance.
    /// </summary>
    public sealed class WeaponTarget
    {
        public string Key;
        public string Label;
        public (float share, float sharp, float blunt, float heat)[] Zones;
        public float Size = 1f;
        public float Dodge;
        public bool Mechanoid;
    }

    /// <summary>
    /// A ranged weapon's numbers as the game keeps them: its main verb (warm-up, burst, cooldown, range, accuracy at 3, 12,
    /// 25 and 40 cells, forced miss radius) and the projectile's damage, armour piercing, stopping power and explosion.
    /// Quality and the weapon's own stat factors are applied by the caller (normal quality changes nothing).
    /// </summary>
    public sealed class WeaponRangedInput
    {
        public float Damage;
        public float ArmourPen;
        public WeaponArmour Armour = WeaponArmour.Sharp;
        /// <summary>False for a projectile whose main damage doesn't harm (EMP, smoke, tox gas): its payloads are what it does.</summary>
        public bool HarmsHealth = true;
        /// <summary>A weapon made to bring people down alive (a non-lethal round, a stun weapon).</summary>
        public bool NonLethal;
        public float StoppingPower;
        public float ExplosionRadius;
        public bool ExplosionFalloff;
        /// <summary>A fuse before the blast (a thrown grenade's 100 ticks): a target on the move can walk out of it.</summary>
        public float ExplosionDelaySeconds;
        public float ForcedMissRadius;
        public bool CanGoWild = true;
        public int Burst = 1;
        public int TicksBetweenBurst = 15;
        public float Warmup;
        public float Cooldown = 1f;
        public float Range;
        public float MinRange;
        public float AccTouch = 1f, AccShort = 1f, AccMedium = 1f, AccLong = 1f;
        public bool OneUse;
        /// <summary>
        /// A beam that sweeps across the target (Verb_ShootBeam, the incinerator's arc spray): each burst tick strikes the
        /// cell it has reached, whoever stands there, with no roll to hit; the sweep is BeamWidth + 1 cells wide.
        /// </summary>
        public bool Beam;
        public float BeamWidth = 1f;
        /// <summary>The beam also strikes the cells beside the one it reaches (half damage off its path).</summary>
        public bool BeamHitsNeighbours;
        /// <summary>Projectiles fired at once per shot, each aimed on its own (a matchlock's shot, VFE Medieval's shotCount).</summary>
        public int ProjectilesPerShot = 1;
        /// <summary>A shot that loses its force with distance: twice its damage point blank, none at its range (VFE Medieval's matchlocks).</summary>
        public bool DamageFallsWithDistance;
        /// <summary>A shot that is a line from the gun (a gauss slug that pierces, a flame stream): it strikes whoever stands on it.</summary>
        public bool Line;
        /// <summary>How many times a lingering stream strikes the same target (1 for a shot or a stream that strikes once).</summary>
        public float HitsPerShot = 1f;
        public List<WeaponPayload> Payloads = new List<WeaponPayload>();

        public WeaponRangedInput Clone()
        {
            var c = (WeaponRangedInput)MemberwiseClone();
            c.Payloads = new List<WeaponPayload>(Payloads);
            return c;
        }
    }

    /// <summary>
    /// One melee attack: a tool with one of its capacities (a sword's edge cuts, its point stabs), as the game makes one
    /// verb of each. Power and armour piercing are the tool's own; the stuff and quality factors come from the weapon.
    /// </summary>
    public sealed class WeaponMeleeAttack
    {
        public string Label;
        public float Power;
        public float Cooldown;
        /// <summary>The tool's armour piercing; below zero means the game's default, 1.5% of the damage.</summary>
        public float ArmourPen = -1f;
        public WeaponArmour Armour = WeaponArmour.Blunt;
        /// <summary>The blow cuts or stabs (wounds bleed) whatever armour it meets: a sharp blow, or a cut that ignores armour.</summary>
        public bool Cuts;
        public bool IsCut => Cuts || Armour == WeaponArmour.Sharp;
        public bool IsBlunt => !IsCut && Armour == WeaponArmour.Blunt;
        public float ChanceFactor = 1f;
        /// <summary>The damage's additional hediffs (each adds 0.1 to how readily the game picks it).</summary>
        public int AdditionalHediffs;
        /// <summary>The fighter's own body part (fists, head), not the weapon: no stuff or quality.</summary>
        public bool Natural;
        public List<WeaponPayload> Payloads = new List<WeaponPayload>();
    }

    /// <summary>A melee weapon: its attacks, and the stuff and quality factors on them.</summary>
    public sealed class WeaponMeleeInput
    {
        public List<WeaponMeleeAttack> Attacks = new List<WeaponMeleeAttack>();
        public float SharpFactor = 1f;
        public float BluntFactor = 1f;
        public float CooldownFactor = 1f;
        /// <summary>MeleeWeapon_DamageMultiplier by quality: 1 at normal (0.8 awful to 1.65 legendary).</summary>
        public float QualityFactor = 1f;
        /// <summary>Whether the stuff factors apply at all (a weapon made of stuff).</summary>
        public bool Stuffed;
        public bool NonLethal;
    }

    /// <summary>
    /// An ability the weapon grants (CompEquippableAbility): what one use does and how often it can be used. Counted
    /// only when its effect is one the review can read from its defs (a projectile, a flame spew); a mod's own code is
    /// named and left out.
    /// </summary>
    public sealed class WeaponAbilityInput
    {
        public string Label;
        public bool Counted;
        public WeaponRangedInput Shot;
        /// <summary>For a cone of flame or the like: damage to each enemy in it, and how many it catches.</summary>
        public float AreaDamage;
        public WeaponArmour AreaArmour = WeaponArmour.Heat;
        public float AreaTargets;
        public float AreaIgnite;
        public float CooldownSeconds;
        /// <summary>Uses before it must be reloaded (0: no charges, only the cooldown).</summary>
        public int Charges;
        public string Note;
    }

    /// <summary>A stat the weapon changes on whoever holds it (equippedStatOffsets), plus or minus.</summary>
    public struct WeaponPassive
    {
        public string Stat;
        public float Offset;

        public WeaponPassive(string stat, float offset)
        {
            Stat = stat;
            Offset = offset;
        }
    }

    /// <summary>A named special effect: whether the rating counts it, and a short note.</summary>
    public sealed class WeaponSpecial
    {
        public string Key;
        public string Label;
        public bool Counted;
        /// <summary>Plus or minus for whoever holds it (1, -1, or 0 for neither).</summary>
        public int Sign = 1;
    }

    /// <summary>What the rating says about one weapon. All ratings are 1.0 to 10.0 with one decimal.</summary>
    public sealed class WeaponRating
    {
        public string Kind;
        public string Role;
        public float Overall;
        /// <summary>The damage per second (and its equivalents) the overall is made from, after the role's weights.</summary>
        public float FightValue;
        public Dictionary<string, float> Sub = new Dictionary<string, float>();
        public Dictionary<string, float> Situations = new Dictionary<string, float>();
        /// <summary>Expected damage per second at each reference distance (ranged) against each reference target.</summary>
        public Dictionary<string, float> Dps = new Dictionary<string, float>();
        public List<WeaponSpecial> Specials = new List<WeaponSpecial>();
        public List<string> Tags = new List<string>();
        public List<string> BestFor = new List<string>();
        public List<string> WeakWhen = new List<string>();
        public string Skill;
        public float CrowdFactor = 1f;
        public float PassiveFactor = 1f;
        /// <summary>What a short reach costs a ranged weapon (<see cref="WeaponRateMath.ReachFactor"/>).</summary>
        public float ReachFactor = 1f;
        public float AbilityValue;
        /// <summary>The fight value at skill 16 over that at skill 8: how much a better fighter gets out of it.</summary>
        public float SkillGain = 1f;
    }

    /// <summary>
    /// The weapon rating: what a weapon really does in a fight by the game's own rules, as one number from 1.0 to 10.0
    /// and a few sub-ratings, without game types (Tools/WeaponReview rates every weapon of the player's mods with it,
    /// Tools/Tests checks it against the base game's weapons; the game side will feed it live values).
    ///
    /// The rules are the game's (1.6, decompiled): a shot's chance to hit is the shooter's accuracy to the power of the
    /// distance in cells times the weapon's accuracy at that distance (ShotReport, VerbProperties.GetHitChanceFactor) times
    /// the target's size; a hit meets the armour on the part it strikes (ArmorUtility: armour minus piercing, half that
    /// chance to be deflected, the same again to be halved); a weapon fires a burst after its warm-up and rests its
    /// cooldown; explosives land within their forced miss radius or near a miss and hurt whoever is inside the blast;
    /// melee weapons strike with the attack the game picks (Best and Mid verbs by damage per second, 3 in 4 and 1 in 4,
    /// the fighter's fists among them), hit by the fighter's melee hit chance and the target's dodge.
    ///
    /// The reference fighter is a colonist of skill 8 (shooting accuracy 96% a cell, melee hit 74%) with a human's fists.
    /// The reference enemies are the game's own: a raider in plain clothes; a raider in a flak vest, flak pants and a
    /// steel simple helmet; a raider in marine armour and helmet; and mechanoids, half a lancer or scyther and half a
    /// centipede. The share of hits on each armour piece is from the human body's part coverage.
    /// </summary>
    public static class WeaponRateMath
    {
        // ---------------------------------------------------------------- the game's own curves

        /// <summary>The reference colonist: skill 8 in shooting and melee, no traits, a human's natural attacks.</summary>
        public static WeaponFighter Reference => new WeaponFighter();

        static readonly float[] ShootX = { -20, -10, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 26, 30, 40, 60 };
        static readonly float[] ShootY = { 0.70f, 0.80f, 0.83f, 0.85f, 0.87f, 0.89f, 0.93f, 0.94f, 0.95f, 0.96f, 0.97f, 0.975f, 0.98f, 0.98333f, 0.98666f, 0.99f, 0.9925f, 0.995f, 0.9965f, 0.998f, 0.999f };
        static readonly float[] MeleeX = { -20, -10, 0, 10, 20, 40, 60 };
        static readonly float[] MeleeY = { 0.05f, 0.10f, 0.50f, 0.80f, 0.90f, 0.96f, 0.98f };
        static readonly float[] DodgeX = { 5, 20, 60 };
        static readonly float[] DodgeY = { 0f, 0.30f, 0.50f };

        /// <summary>ShootingAccuracyPawn: the chance not to miss per cell, from the skill level plus trait offsets.</summary>
        public static float ShootingAccuracy(float skill) => Curve(ShootX, ShootY, skill);

        /// <summary>MeleeHitChance from the melee skill level plus trait offsets.</summary>
        public static float MeleeHitChance(float skill) => Curve(MeleeX, MeleeY, skill);

        /// <summary>MeleeDodgeChance from the melee skill level plus offsets (nimble +15).</summary>
        public static float MeleeDodgeChance(float skill) => Curve(DodgeX, DodgeY, skill);

        /// <summary>A human's natural melee attacks (Races_Humanlike: two fists, teeth, head).</summary>
        public static List<WeaponMeleeAttack> HumanNaturalAttacks() => new List<WeaponMeleeAttack>
        {
            new WeaponMeleeAttack { Label = "left fist", Power = 8.2f, Cooldown = 2f, Armour = WeaponArmour.Blunt, Natural = true },
            new WeaponMeleeAttack { Label = "right fist", Power = 8.2f, Cooldown = 2f, Armour = WeaponArmour.Blunt, Natural = true },
            new WeaponMeleeAttack { Label = "teeth", Power = 8.2f, Cooldown = 2f, Armour = WeaponArmour.Sharp, ChanceFactor = 0.07f, Natural = true },
            new WeaponMeleeAttack { Label = "head", Power = 5f, Cooldown = 2f, Armour = WeaponArmour.Blunt, ChanceFactor = 0.2f, Natural = true },
        };

        /// <summary>ShotReport.HitFactorFromShooter for a pawn with no accuracy factors: accuracy to the power of the distance.</summary>
        public static float ShooterFactor(float accuracyPerCell, float distance) =>
            Math.Max((float)Math.Pow(accuracyPerCell, distance), 0.0201f);

        /// <summary>VerbProperties.GetHitChanceFactor: the weapon's accuracy, lerped between 3, 12, 25 and 40 cells.</summary>
        public static float WeaponAccuracy(WeaponRangedInput w, float d)
        {
            float a;
            if (d <= 3f) a = w.AccTouch;
            else if (d <= 12f) a = Lerp(w.AccTouch, w.AccShort, (d - 3f) / 9f);
            else if (d <= 25f) a = Lerp(w.AccShort, w.AccMedium, (d - 12f) / 13f);
            else if (d <= 40f) a = Lerp(w.AccMedium, w.AccLong, (d - 25f) / 15f);
            else a = w.AccLong;
            return Clamp(a, 0.01f, 1f);
        }

        /// <summary>The chance a shot is aimed on the target (AimOnTargetChance, open ground, clear weather, no cover).</summary>
        public static float AimOn(float shooter, float weapon, float size) =>
            Math.Max(shooter * weapon, 0.0201f) * Clamp(size, 0.5f, 2f);

        /// <summary>
        /// The share of a hit's damage left after one armour roll (ArmorUtility.ApplyArmor): with armour minus piercing
        /// at e, the hit is deflected with chance e/2 and halved with chance e/2 (below e = 1); from 1 to 2 it is halved
        /// whenever not deflected; from 2 it never gets through.
        /// </summary>
        public static float ArmourFactor(float armour, float pen)
        {
            float e = Math.Max(armour - pen, 0f);
            if (e >= 2f) return 0f;
            if (e >= 1f) return (1f - e / 2f) * 0.5f;
            return 1f - 0.75f * e;
        }

        /// <summary>The share of damage that gets through a target's armour, over where hits land.</summary>
        public static float AfterArmour(WeaponTarget t, WeaponArmour kind, float pen)
        {
            if (kind == WeaponArmour.None) return 1f;
            float sum = 0f;
            foreach (var z in t.Zones)
            {
                float a = kind == WeaponArmour.Sharp ? z.sharp : kind == WeaponArmour.Blunt ? z.blunt : z.heat;
                sum += z.share * ArmourFactor(a, pen);
            }
            return sum;
        }

        /// <summary>ShootTuning.MissDistanceFromAimOnChanceCurves at its top: how far a wild miss can land, by the aim chance.</summary>
        public static float MaxWildMiss(float aimOn)
        {
            float[] x = { 0.02f, 0.04f, 0.07f, 0.11f, 0.22f, 1f };
            float[] y = { 10f, 8f, 6f, 4f, 2f, 1f };
            return Curve(x, y, aimOn);
        }

        /// <summary>The cells within a radius (GenRadial.NumCellsInRadius).</summary>
        public static int CellsInRadius(float r)
        {
            int n = 0;
            int k = (int)Math.Ceiling(r);
            for (int x = -k; x <= k; x++)
                for (int z = -k; z <= k; z++)
                    if (x * x + z * z <= r * r + 0.0001f) n++;
            return n;
        }

        /// <summary>VerbUtility.CalculateAdjustedForcedMiss: none within 3 cells, half within 5, four fifths within 7.</summary>
        public static float AdjustedForcedMiss(float radius, float d)
        {
            float sq = d * d;
            if (sq < 9f) return 0f;
            if (sq < 25f) return radius * 0.5f;
            if (sq < 49f) return radius * 0.8f;
            return radius;
        }

        // ---------------------------------------------------------------- the reference enemies

        /// <summary>
        /// Shares of hits by armour piece on a human, from the body's part coverage (Bodies_Humanlike): torso and neck
        /// (a flak vest) 44.2%, legs (flak pants) 25.2%, upper head (a simple helmet) 3.7%, the rest 26.9%; marine armour
        /// and helmet together 94.6%.
        /// </summary>
        public const float VestShare = 0.442f, PantsShare = 0.252f, HelmetShare = 0.037f, MarineShare = 0.946f;

        public static readonly WeaponTarget Unarmoured = new WeaponTarget
        {
            Key = "unarmoured", Label = "a raider in plain clothes",
            Zones = new[] { (1f, 0f, 0f, 0f) }, Dodge = MeleeDodgeChance(8f)
        };

        /// <summary>A flak vest (sharp 100%, blunt 36%, heat 27%), flak pants (55/8/10) and a steel simple helmet (45/22.5/0).</summary>
        public static readonly WeaponTarget Flak = new WeaponTarget
        {
            Key = "armoured", Label = "a raider in flak",
            Zones = new[]
            {
                (VestShare, 1.00f, 0.36f, 0.27f),
                (PantsShare, 0.55f, 0.08f, 0.10f),
                (HelmetShare, 0.45f, 0.225f, 0.0f),
                (1f - VestShare - PantsShare - HelmetShare, 0f, 0f, 0f)
            },
            Dodge = MeleeDodgeChance(8f)
        };

        /// <summary>Marine armour and helmet (sharp 106%, blunt 45%, heat 54%), the hands and feet bare.</summary>
        public static readonly WeaponTarget Heavy = new WeaponTarget
        {
            Key = "heavy", Label = "a raider in marine armour",
            Zones = new[] { (MarineShare, 1.06f, 0.45f, 0.54f), (1f - MarineShare, 0f, 0f, 0f) },
            Dodge = MeleeDodgeChance(8f)
        };

        /// <summary>A lancer or scyther (sharp 40%, blunt 20%, heat 200%, size 1).</summary>
        public static readonly WeaponTarget MechLight = new WeaponTarget
        {
            Key = "mech_light", Label = "a lancer or scyther",
            Zones = new[] { (1f, 0.40f, 0.20f, 2.00f) }, Size = 1f, Dodge = 0f, Mechanoid = true
        };

        /// <summary>A centipede (sharp 72%, blunt 22%, heat 200%, body size 3, so twice as easy to hit).</summary>
        public static readonly WeaponTarget MechHeavy = new WeaponTarget
        {
            Key = "mech_heavy", Label = "a centipede",
            Zones = new[] { (1f, 0.72f, 0.22f, 2.00f) }, Size = 3f, Dodge = 0f, Mechanoid = true
        };

        public static readonly WeaponTarget[] Targets = { Unarmoured, Flak, Heavy, MechLight, MechHeavy };

        /// <summary>
        /// The mix of enemies the overall weighs: a quarter everyday raiders, two fifths armoured ones, a fifth heavily
        /// armoured ones and the rest mechanoids (split between the light and the heavy one), as a colony meets them past
        /// its first year. Light melee leans a little more on the everyday fights it is carried for.
        /// </summary>
        public static readonly float[] RangedMix = { 0.25f, 0.40f, 0.20f, 0.075f, 0.075f };
        public static readonly float[] LightMeleeMix = { 0.40f, 0.35f, 0.15f, 0.05f, 0.05f };
        public static readonly float[] HeavyMeleeMix = { 0.25f, 0.40f, 0.20f, 0.075f, 0.075f };

        // ---------------------------------------------------------------- the fight's own values

        /// <summary>Reference distances for ranged weapons: point blank, short, medium and long (cells).</summary>
        public static readonly float[] Distances = { 5f, 12f, 25f, 40f };
        public static readonly string[] DistanceKeys = { "close", "medium", "long", "far" };

        /// <summary>The usual raider's rifle reaches 30.9 cells: damage dealt from beyond it can't be answered, and counts double.</summary>
        public const float EnemyReach = 30.9f;
        public const float BeyondReachFactor = 2f;

        /// <summary>
        /// A second of an enemy out of the fight (stunned, EMPed) counts as this much damage: about what the reference
        /// rifleman deals in that time against a raider; twice that for a mechanoid, which hits harder and takes longer
        /// to put down.
        /// </summary>
        public const float StunSecondValue = 4f;
        public const float MechStunSecondValue = 8f;
        /// <summary>A mechanoid adapts to EMP for 2,200 ticks after a stun (DamageDef EMP.stunAdaptationTicks).</summary>
        public const float EmpAdaptSeconds = 2200f / 60f;
        /// <summary>A target set alight burns and runs: counted as this much damage (heat, armour-checked).</summary>
        public const float BurnValue = 10f;
        /// <summary>Toxic buildup kills at severity 1: a severity point counts as this much damage.</summary>
        public const float ToxicSeverityValue = 40f;
        /// <summary>A tox gas cloud from one hit: counted as this much damage on unprotected flesh (it lingers and spreads).</summary>
        public const float ToxGasValue = 8f;

        /// <summary>A raid group bunched around a target: about one more enemy in every eight cells, at most four more caught.</summary>
        public const float CrowdDensity = 0.12f;
        public const float MaxCrowdExtra = 4f;
        /// <summary>A missed bullet that goes wild among a crowd strikes someone else this often (about one time in seven).</summary>
        public const float StrayHitChance = 0.15f;

        /// <summary>
        /// Heavier hits count for more: from 10 damage a hit upward, up to a third more at 30, for what one blow can take
        /// out (a brain has 10 points of health, a heart 15, a neck 25, a leg 30).
        /// </summary>
        public static float PunchFactor(float damagePerHit) => 1f + 0.35f * Clamp((damagePerHit - 10f) / 20f, 0f, 1f);

        /// <summary>A blast near the colony's own line can catch its own: a few hundredths off for each cell of radius.</summary>
        public static float FriendlyFireFactor(float radius) => radius >= 1f ? 1f - Clamp(0.04f * radius, 0f, 0.3f) : 1f;

        /// <summary>
        /// A gun that can't reach as far as the usual raider's rifle has to close in under fire: a quarter off at 12 cells,
        /// nothing off from 30.9 (in a straight line between).
        /// </summary>
        public static float ReachFactor(float range) => Lerp(0.75f, 1f, (range - 12f) / (EnemyReach - 12f));

        /// <summary>A fuse lets a target on the move walk out of the blast: a fifth of the throws lost for each second of fuse, at most half.</summary>
        public static float FuseFactor(float seconds) => 1f - Clamp(seconds * 0.2f, 0f, 0.5f);

        /// <summary>A one-use weapon's single volley is spread over a fight of this many seconds.</summary>
        public const float FightSeconds = 30f;
        /// <summary>An ability's uses are counted over a fight of this many seconds (its charges, or as often as its cooldown allows).</summary>
        public const float AbilityFightSeconds = 60f;
        /// <summary>
        /// What its abilities add is at most half the weapon's own fight value: a few charges or a long cooldown help a
        /// fight, they don't carry it.
        /// </summary>
        public const float AbilityShareCap = 0.5f;

        /// <summary>A colonist's base move speed (cells a second), for what slowing the wielder costs.</summary>
        public const float BaseMoveSpeed = 4.6f;

        // ---------------------------------------------------------------- roles

        public static readonly string[] RangedRoles = { "close", "all-round", "long", "area", "one-use", "anti-mechanoid", "utility" };

        /// <summary>
        /// The enemy mix a ranged weapon is judged on: an EMP weapon that harms no flesh is made for mechanoids, and is
        /// judged three fifths on them; everything else on the usual mix.
        /// </summary>
        public static float[] MixFor(string role) =>
            role == "anti-mechanoid" ? new[] { 0.10f, 0.15f, 0.15f, 0.30f, 0.30f } : RangedMix;

        /// <summary>The distances a weapon is judged at by its role; an anti-mechanoid weapon by how it is thrown or fired.</summary>
        public static float[] DistanceWeightsFor(WeaponRangedInput w, string role)
        {
            if (role == "anti-mechanoid" || role == "utility")
                return DistanceWeights(w.ExplosionRadius >= 1f || w.ForcedMissRadius > 0.5f ? "area" : w.Range < 20f ? "close" : "all-round");
            return DistanceWeights(role);
        }
        public static readonly string[] MeleeRoles = { "light melee", "heavy melee" };

        /// <summary>How much each reference distance (5, 12, 25, 40 cells) counts in the overall, by what the weapon is for.</summary>
        public static float[] DistanceWeights(string role)
        {
            switch (role)
            {
                case "close": return new[] { 0.55f, 0.40f, 0.05f, 0f };
                case "long": return new[] { 0.05f, 0.20f, 0.40f, 0.35f };
                case "area": return new[] { 0.20f, 0.60f, 0.20f, 0f };
                case "one-use": return new[] { 0.10f, 0.40f, 0.40f, 0.10f };
                default: return new[] { 0.20f, 0.40f, 0.35f, 0.05f };
            }
        }

        /// <summary>How much what a weapon adds against a bunched crowd counts in the overall, by role.</summary>
        public static float CrowdWeight(string role)
        {
            switch (role)
            {
                case "anti-mechanoid": return 0.35f;
                case "area": return 0.35f;
                case "one-use": return 0.35f;
                case "long": return 0.1f;
                default: return 0.2f;
            }
        }

        /// <summary>
        /// What a ranged weapon is for, from its own numbers: one that harms no flesh is an anti-mechanoid weapon when it
        /// carries EMP, else a utility (smoke) not rated for damage; one used up in a volley is one-use; one whose shot is an
        /// explosion or that throws within its forced miss radius is an area weapon; under 20 cells, or under 26 when it is
        /// most accurate close and loses a quarter of its accuracy by 25 cells (pistols, SMGs, short bows), it is a close
        /// weapon; from 38 cells, when it is at least as accurate far as near, a long one; the rest all-round.
        /// </summary>
        public static string RangedRole(WeaponRangedInput w)
        {
            bool harms = w.HarmsHealth && w.Damage > 0f || w.Payloads.Exists(p => p.Effect != WeaponEffect.Emp && p.Amount > 0f);
            if (!harms && w.Payloads.Exists(p => p.Effect == WeaponEffect.Emp)) return "anti-mechanoid";
            if (!harms) return "utility";
            if (w.OneUse) return "one-use";
            if (w.ExplosionRadius >= 1f || w.ForcedMissRadius > 0.5f) return "area";
            if (w.Range < 20f) return "close";
            if (w.Range < 26f && w.AccTouch >= w.AccShort && w.AccMedium < 0.75f * w.AccShort) return "close";
            if (w.Range >= 38f && w.AccMedium + w.AccLong >= w.AccTouch + w.AccShort) return "long";
            return "all-round";
        }

        /// <summary>Light melee is a weapon under 1.5 kg (carried beside a gun, quick in a scrap); heavier is heavy melee.</summary>
        public static string MeleeRole(float mass) => mass < 1.5f ? "light melee" : "heavy melee";

        // ---------------------------------------------------------------- the rating scale

        /// <summary>
        /// The scale from a fight value (damage per second and its equivalents) to a rating: in the logarithm of the value,
        /// so each doubling adds about the same near the middle (5.5), and flattening toward both ends the way a Laplace
        /// curve does (it halves its distance to 1 or to 10 every 2 widths), so a mod's super-weapon three or ten times the
        /// best of the base game lands at 9.something rather than all of them sitting at 10. The middle and the width are
        /// set per kind so the base game's weapons spread over the scale (Tools/Tests holds the anchors).
        /// </summary>
        public const float RangedMid = 5.0f, RangedWidth = 0.65f;
        public const float MeleeMid = 5.6f, MeleeWidth = 0.42f;

        public static float Scale(float value, bool melee)
        {
            if (value <= 0f) return 1f;
            float mid = melee ? MeleeMid : RangedMid;
            float width = melee ? MeleeWidth : RangedWidth;
            double x = Math.Log(value / mid) / width;
            double r = 1.0 + 9.0 * Spread(x);
            return (float)Math.Round(Math.Min(Math.Max(r, 1.0), 10.0), 1);
        }

        /// <summary>The fight value a rating stands for (the scale backwards), for reading the anchors.</summary>
        public static float ValueFor(float rating, bool melee)
        {
            float mid = melee ? MeleeMid : RangedMid;
            float width = melee ? MeleeWidth : RangedWidth;
            double p = (rating - 1.0) / 9.0;
            p = Math.Min(Math.Max(p, 1e-6), 1 - 1e-6);
            double x = p < 0.5 ? 2.0 * Math.Log(2.0 * p) : -2.0 * Math.Log(2.0 * (1.0 - p));
            return (float)(mid * Math.Exp(width * x));
        }

        /// <summary>The Laplace curve the scale follows: 0 to 1, a half at 0, as steep there as a logistic, with long tails.</summary>
        public static double Spread(double x) => x >= 0 ? 1.0 - 0.5 * Math.Exp(-x / 2.0) : 0.5 * Math.Exp(x / 2.0);

        // ---------------------------------------------------------------- ranged

        /// <summary>The time from one burst to the next: warm-up, the burst's own ticks and the cooldown.</summary>
        public static float CycleSeconds(WeaponRangedInput w, WeaponFighter f) =>
            w.Warmup * f.AimFactor + w.Cooldown * f.RangedCooldownFactor + (Math.Max(w.Burst, 1) - 1) * w.TicksBetweenBurst / 60f;

        /// <summary>
        /// The chance one shot's blast catches the target: a shot within its forced miss radius lands on a random cell of
        /// that circle (the target's own cell then flies as an aimed shot); otherwise it is aimed, and a wild miss lands
        /// one to a few cells away (by the aim chance), still in the blast when the blast is wide enough.
        /// </summary>
        public static float BlastChance(WeaponRangedInput w, float aimOn, float aimStandard, float d)
        {
            float r = w.ExplosionRadius;
            float aimed = Clamp(aimOn, 0f, 1f);
            if (w.CanGoWild)
            {
                float max = MaxWildMiss(aimStandard);
                float wildIn = max <= 1f ? (r >= 1f ? 1f : 0f) : Clamp((r - 1f) / (max - 1f), 0f, 1f);
                aimed = aimed + (1f - aimed) * wildIn;
            }
            else aimed = 1f;
            float m = w.ForcedMissRadius > 0.5f ? AdjustedForcedMiss(w.ForcedMissRadius, d) : 0f;
            if (m <= 0.5f) return aimed;
            int all = CellsInRadius(m);
            int inside = CellsInRadius(Math.Min(r, m));
            return (inside - 1f) / all + aimed / all;
        }

        /// <summary>The average share of an explosion's damage a target in it takes, with the game's falloff (full at the centre, a fifth at the edge).</summary>
        public static float FalloffShare(float radius)
        {
            if (radius <= 0f) return 1f;
            int k = (int)Math.Ceiling(radius);
            float sum = 0f;
            int n = 0;
            for (int x = -k; x <= k; x++)
                for (int z = -k; z <= k; z++)
                {
                    float dist = (float)Math.Sqrt(x * x + z * z);
                    if (dist > radius + 0.0001f) continue;
                    sum += Lerp(1f, 0.2f, dist / radius);
                    n++;
                }
            return n == 0 ? 1f : sum / n;
        }

        /// <summary>The damage-like worth of one landed hit's payloads (more damage, fire, toxins) against a target.</summary>
        public static float PayloadValue(List<WeaponPayload> payloads, WeaponTarget t)
        {
            float v = 0f;
            if (payloads == null) return 0f;
            foreach (var p in payloads)
            {
                float chance = Clamp(p.Chance, 0f, 1f);
                switch (p.Effect)
                {
                    case WeaponEffect.Damage:
                        v += chance * p.Amount * AfterArmour(t, p.Armour, p.ArmourPen);
                        break;
                    case WeaponEffect.Ignite:
                        if (!t.Mechanoid) v += chance * BurnValue * AfterArmour(t, WeaponArmour.Heat, 0f);
                        break;
                    case WeaponEffect.ToxicBuildup:
                        if (!t.Mechanoid) v += chance * p.Amount * ToxicSeverityValue;
                        break;
                    case WeaponEffect.ToxGas:
                        if (!t.Mechanoid) v += chance * ToxGasValue;
                        break;
                }
            }
            return v;
        }

        /// <summary>
        /// The worth a second of a stream of hits that stun: a stun lasts half a second a point and doesn't stack (a new
        /// one only tops it up), so the time stunned can't pass the time fought; an EMP stuns a mechanoid once each time
        /// it has adapted (stun plus 2,200 ticks) and does nothing to flesh but break its shield.
        /// </summary>
        public static float StunValuePerSecond(List<WeaponPayload> payloads, WeaponTarget t, float hitsPerSecond)
        {
            float v = 0f;
            if (payloads == null) return 0f;
            float worth = t.Mechanoid ? MechStunSecondValue : StunSecondValue;
            foreach (var p in payloads)
            {
                float rate = hitsPerSecond * Clamp(p.Chance, 0f, 1f);
                float s = p.Amount * 0.5f;
                if (p.Effect == WeaponEffect.Stun)
                    v += Math.Min(rate * s, 1f) * worth;
                else if (p.Effect == WeaponEffect.Emp && t.Mechanoid)
                    v += Math.Min(Math.Min(rate, 1f / Math.Max(s, EmpAdaptSeconds)) * s, 1f) * worth;
            }
            return v;
        }

        /// <summary>
        /// Expected damage per second (and its equivalents) of a ranged weapon at a distance against a target: hit or blast
        /// chance times what a hit does after armour, over the cycle. Zero where the weapon can't fire (past its range,
        /// inside its minimum range).
        /// </summary>
        public static float RangedDps(WeaponRangedInput w, WeaponFighter f, WeaponTarget t, float d)
        {
            if (d > w.Range + 0.01f || d < w.MinRange) return 0f;
            float acc = ShootingAccuracy(f.ShootingSkill);
            float shooter = w.CanGoWild ? ShooterFactor(acc, d) : 1f;
            float weapon = WeaponAccuracy(w, d);
            float aimStandard = Math.Max(shooter * weapon, 0.0201f);
            float aimOn = AimOn(shooter, weapon, t.Size);
            float hit;
            float share = 1f;
            if (w.Beam)
                hit = BeamShare(w);
            else if (w.Line && w.ExplosionRadius <= 0.5f)
                hit = Clamp(aimOn, 0f, 1f) + (1f - Clamp(aimOn, 0f, 1f)) * 0.3f;
            else if (w.ExplosionRadius > 0.5f)
            {
                hit = BlastChance(w, aimOn, aimStandard, d);
                if (w.ExplosionFalloff) share = FalloffShare(w.ExplosionRadius);
            }
            else if (w.ForcedMissRadius > 0.5f && AdjustedForcedMiss(w.ForcedMissRadius, d) > 0.5f)
                hit = Clamp(aimOn, 0f, 1f) / CellsInRadius(AdjustedForcedMiss(w.ForcedMissRadius, d));
            else hit = Clamp(aimOn, 0f, 1f);
            float perHit = 0f;
            float dmg = w.Damage;
            if (w.DamageFallsWithDistance && w.Range > 0f) dmg *= Clamp(1f - d / w.Range, 0f, 1f) * 2f;
            if (w.HarmsHealth && dmg > 0f)
                perHit += dmg * share * AfterArmour(t, w.Armour, w.ArmourPen * share) * PunchFactor(dmg * share);
            perHit += PayloadValue(w.Payloads, t);
            float cycle = CycleSeconds(w, f);
            if (cycle <= 0f) return 0f;
            float seconds = w.OneUse ? FightSeconds : cycle;
            if (w.ExplosionRadius > 0.5f) hit *= FuseFactor(w.ExplosionDelaySeconds);
            float hitsPerSecond = hit * Math.Max(w.Burst, 1) * Math.Max(w.ProjectilesPerShot, 1) * Math.Max(w.HitsPerShot, 1f) / seconds;
            return hitsPerSecond * perHit + StunValuePerSecond(w.Payloads, t, hitsPerSecond);
        }

        /// <summary>
        /// The share of a sweeping beam's burst ticks that strike one target: one cell of the sweep's width plus one, and
        /// half again from the ticks next to it when the beam strikes the cells beside its path.
        /// </summary>
        public static float BeamShare(WeaponRangedInput w) => Clamp((w.BeamHitsNeighbours ? 2f : 1f) / (Math.Max(w.BeamWidth, 0f) + 1f), 0f, 1f);

        /// <summary>
        /// How many enemies a hit harms against a bunched group, as a factor on its damage: an explosion catches those in
        /// its blast (one more in every eight cells, at most four more), a beam the one behind, and a missed bullet that
        /// goes wild strikes someone else one time in five.
        /// </summary>
        public static float CrowdFactor(WeaponRangedInput w, WeaponFighter f, float d)
        {
            if (d > w.Range + 0.01f || d < w.MinRange) return 1f;
            if (w.ExplosionRadius > 0.5f)
                return 1f + Math.Min(MaxCrowdExtra, CrowdDensity * (CellsInRadius(w.ExplosionRadius) - 1));
            if (w.Beam)
                return 1f + Math.Min(MaxCrowdExtra, CrowdDensity * (Math.Max(w.BeamWidth, 0f) + 1f) * (w.BeamHitsNeighbours ? 3f : 1f) + 0.5f);
            if (w.Line)
                return 1.5f;
            float acc = ShootingAccuracy(f.ShootingSkill);
            float hit = Clamp(AimOn(w.CanGoWild ? ShooterFactor(acc, d) : 1f, WeaponAccuracy(w, d), 1f), 0.02f, 1f);
            return (hit + StrayHitChance * (1f - hit)) / hit;
        }

        /// <summary>
        /// Each distance judged against what a good shot can do there: the damage divided by the reference shooter's own
        /// falloff (96% a cell), so a sniper's 40 cells and a shotgun's 5 are rated on one footing (a thrown or lobbed
        /// weapon that scatters within its own radius doesn't suffer that falloff, and isn't lifted by it); damage from
        /// beyond the usual raider's reach counts double.
        /// </summary>
        public static float Normalised(float dps, float d, WeaponRangedInput w)
        {
            bool scatters = w.ForcedMissRadius > 0.5f || !w.CanGoWild || w.Beam;
            float v = scatters ? dps : dps / ShooterFactor(ShootingAccuracy(8f), d);
            if (d > EnemyReach && w.CanGoWild) v *= BeyondReachFactor;
            return v;
        }

        /// <summary>
        /// The role's fight value of a ranged weapon for a fighter: the normalised damage at each distance against the
        /// enemy mix, weighed by the role's distances, with what it adds against a crowd by the role's crowd weight.
        /// </summary>
        public static float RangedFightValue(WeaponRangedInput w, WeaponFighter f, string role)
        {
            float[] dw = DistanceWeightsFor(w, role);
            float cw = CrowdWeight(role);
            var mix = MixFor(role);
            float fv = 0f;
            for (int i = 0; i < Distances.Length; i++)
            {
                if (dw[i] <= 0f) continue;
                float d = Distances[i];
                float m = 0f;
                for (int k = 0; k < Targets.Length; k++) m += mix[k] * Normalised(RangedDps(w, f, Targets[k], d), d, w);
                fv += dw[i] * m * (1f + cw * (CrowdFactor(w, f, d) - 1f));
            }
            return fv;
        }

        // ---------------------------------------------------------------- melee

        /// <summary>One melee attack with the weapon's stuff and quality: its damage, armour piercing and cooldown.</summary>
        public static (float damage, float pen, float cooldown) MeleeNumbers(WeaponMeleeAttack a, WeaponMeleeInput w, WeaponFighter f)
        {
            float dmg = a.Power;
            float cd = a.Cooldown;
            if (!a.Natural && w != null)
            {
                dmg *= w.QualityFactor;
                if (w.Stuffed)
                {
                    if (a.Armour == WeaponArmour.Sharp) dmg *= w.SharpFactor;
                    else if (a.Armour == WeaponArmour.Blunt) dmg *= w.BluntFactor;
                    cd *= w.CooldownFactor;
                }
            }
            dmg *= f.MeleeDamageFactor;
            cd *= f.MeleeCooldownFactor;
            float pen = a.ArmourPen >= 0f ? a.ArmourPen * (a.Natural || w == null ? 1f : w.QualityFactor) : dmg * 0.015f;
            return (dmg, pen, Math.Max(cd, 0.05f));
        }

        /// <summary>
        /// How often the game picks each melee attack (VerbUtility): each verb's damage times (1 + piercing) over its
        /// cooldown, times its chance factor; those within 95% of the best share three quarters, those above a quarter of
        /// the best share the last quarter, the rest are never used. The fighter's own fists are in the running.
        /// </summary>
        public static List<(WeaponMeleeAttack attack, float share)> MeleeShares(WeaponMeleeInput w, WeaponFighter f)
        {
            var all = new List<WeaponMeleeAttack>(w.Attacks);
            all.AddRange(f.Natural);
            var weights = new List<float>();
            float best = 0f;
            foreach (var a in all)
            {
                var n = MeleeNumbers(a, w, f);
                float wt = n.damage * (1f + n.pen) / n.cooldown * (a.ChanceFactor + 0.1f * a.AdditionalHediffs);
                weights.Add(wt);
                if (wt > best) best = wt;
            }
            int bestCount = 0, midCount = 0;
            foreach (var wt in weights)
            {
                if (wt >= best * 0.95f) bestCount++;
                else if (wt >= best * 0.25f) midCount++;
            }
            float bestTotal = midCount > 0 ? 0.75f : 1f;
            var shares = new List<(WeaponMeleeAttack, float)>();
            for (int i = 0; i < all.Count; i++)
            {
                float wt = weights[i];
                float s = best <= 0f ? 0f : wt >= best * 0.95f ? bestTotal / bestCount : wt >= best * 0.25f ? 0.25f / midCount : 0f;
                shares.Add((all[i], s));
            }
            return shares;
        }

        /// <summary>
        /// Expected melee damage per second against a target: each attack's share of the swings, its chance to land (the
        /// fighter's hit chance, then the target's dodge), its damage after armour and what it carries, over the average
        /// cooldown of the swings.
        /// </summary>
        public static float MeleeDps(WeaponMeleeInput w, WeaponFighter f, WeaponTarget t)
        {
            float hit = Clamp(MeleeHitChance(f.MeleeSkill), 0f, 1f) * (1f - t.Dodge);
            float dmgSum = 0f, cdSum = 0f;
            var stunners = new List<(WeaponMeleeAttack a, float share)>();
            foreach (var (a, s) in MeleeShares(w, f))
            {
                if (s <= 0f) continue;
                var n = MeleeNumbers(a, w, f);
                dmgSum += s * hit * (n.damage * AfterArmour(t, a.Armour, n.pen) * PunchFactor(n.damage) + PayloadValue(a.Payloads, t));
                cdSum += s * n.cooldown;
                if (a.Payloads.Count > 0) stunners.Add((a, s));
            }
            if (cdSum <= 0f) return 0f;
            float dps = dmgSum / cdSum;
            foreach (var (a, s) in stunners)
                dps += StunValuePerSecond(a.Payloads, t, s * hit / cdSum);
            return dps;
        }

        /// <summary>The role's fight value of a melee weapon: its damage per second against the role's mix of enemies.</summary>
        public static float MeleeFightValue(WeaponMeleeInput w, WeaponFighter f, string role)
        {
            var mix = role == "light melee" ? LightMeleeMix : HeavyMeleeMix;
            float fv = 0f;
            for (int k = 0; k < Targets.Length; k++) fv += mix[k] * MeleeDps(w, f, Targets[k]);
            return fv;
        }

        // ---------------------------------------------------------------- passives and abilities

        /// <summary>
        /// What the stats a weapon changes on its wielder do to its worth: slower feet cost a ranged fighter some and a
        /// melee fighter more; aim, accuracy and melee hit are put into the fight itself (see <see cref="Apply"/>); dodge,
        /// toughness and pain count a little. Stats with no part in a fight (work, psychic sensitivity, comfort) are
        /// named and not counted.
        /// </summary>
        public static float PassiveFactor(IEnumerable<WeaponPassive> passives, bool melee)
        {
            float f = 1f;
            if (passives == null) return f;
            foreach (var p in passives)
            {
                switch (p.Stat)
                {
                    case "MoveSpeed": f *= 1f + (melee ? 1f : 0.5f) * p.Offset / BaseMoveSpeed; break;
                    case "MeleeDodgeChance": if (melee) f *= 1f + Clamp(p.Offset / 100f, -0.5f, 0.5f); break;
                    case "IncomingDamageFactor": f *= 1f - 0.5f * p.Offset; break;
                    case "PainShockThreshold": f *= 1f + 0.3f * p.Offset; break;
                    case "DeflectChance": f *= 1f + 0.5f * Clamp(p.Offset, 0f, 1f); break;
                }
            }
            return Clamp(f, 0.5f, 1.5f);
        }

        /// <summary>Whether the rating counts a passive stat.</summary>
        public static bool PassiveCounted(string stat, bool melee) =>
            stat == "MoveSpeed" || stat == "IncomingDamageFactor" || stat == "PainShockThreshold" || stat == "DeflectChance" ||
            (!melee && (stat == "ShootingAccuracyPawn" || stat == "AimingDelayFactor" || stat == "RangedCooldownFactor")) ||
            (melee && (stat == "MeleeHitChance" || stat == "MeleeCooldownFactor" || stat == "MeleeDodgeChance"));

        /// <summary>The fighter with the weapon's own stat offsets on them (the game adds accuracy and melee hit to the skill, before its curve).</summary>
        public static WeaponFighter Apply(WeaponFighter f, IEnumerable<WeaponPassive> passives)
        {
            var g = f.Copy();
            if (passives == null) return g;
            foreach (var p in passives)
            {
                switch (p.Stat)
                {
                    case "ShootingAccuracyPawn": g.ShootingSkill += p.Offset; break;
                    case "MeleeHitChance": g.MeleeSkill += p.Offset; break;
                    case "AimingDelayFactor": g.AimFactor = Math.Max(0.1f, g.AimFactor + p.Offset); break;
                    case "RangedCooldownFactor": g.RangedCooldownFactor = Math.Max(0.1f, g.RangedCooldownFactor + p.Offset); break;
                    case "MeleeCooldownFactor": g.MeleeCooldownFactor = Math.Max(0.1f, g.MeleeCooldownFactor + p.Offset); break;
                }
            }
            return g;
        }

        /// <summary>
        /// An ability's worth as damage per second: what its uses deal in a minute's fight (its charges, or as often as its
        /// cooldown allows, whichever is fewer), against the enemy mix at up to 12 cells, a crowd counted as for the weapon.
        /// </summary>
        public static float AbilityValue(WeaponAbilityInput a, WeaponFighter f, float[] mix)
        {
            if (a == null || !a.Counted) return 0f;
            float uses = a.CooldownSeconds > 0f ? Math.Max(1f, AbilityFightSeconds / a.CooldownSeconds) : 1f;
            if (a.Charges > 0) uses = Math.Min(a.CooldownSeconds > 0f ? uses : a.Charges, a.Charges);
            float perUse = 0f;
            if (a.Shot != null)
            {
                var s = a.Shot.Clone();
                s.OneUse = true;
                float d = Math.Min(12f, Math.Max(s.Range, 1f));
                float one = 0f;
                for (int i = 0; i < Targets.Length; i++) one += mix[i] * RangedDps(s, f, Targets[i], d) * FightSeconds;
                perUse = Normalised(one, d, s) * CrowdFactor(s, f, d);
            }
            if (a.AreaDamage > 0f)
            {
                float one = 0f;
                for (int i = 0; i < Targets.Length; i++)
                {
                    var t = Targets[i];
                    float v = a.AreaDamage * AfterArmour(t, a.AreaArmour, 0f);
                    if (!t.Mechanoid) v += a.AreaIgnite * BurnValue * AfterArmour(t, WeaponArmour.Heat, 0f);
                    one += mix[i] * v;
                }
                perUse += one * Math.Max(1f, a.AreaTargets);
            }
            return perUse * uses / AbilityFightSeconds;
        }

        // ---------------------------------------------------------------- the rating

        /// <summary>
        /// Rates a ranged weapon. The sub-ratings are each distance against the enemy mix, and each enemy at the role's
        /// distances; the overall is the role's fight value (<see cref="RangedFightValue"/>), times what its passive stats
        /// do and a little for stopping a charge, plus its abilities.
        /// </summary>
        public static WeaponRating RateRanged(WeaponRangedInput w, WeaponFighter fighter = null, IEnumerable<WeaponPassive> passives = null,
            IEnumerable<WeaponAbilityInput> abilities = null, string role = null, float mass = 0f)
        {
            var baseFighter = fighter ?? Reference;
            var f = Apply(baseFighter, passives);
            var r = new WeaponRating { Kind = "ranged", Role = role ?? RangedRole(w), Skill = "Shooting" };
            float[] dw = DistanceWeightsFor(w, r.Role);
            float cw = CrowdWeight(r.Role);
            var mix = MixFor(r.Role);
            var perTarget = new float[Targets.Length];
            float crowdNum = 0f, crowdDen = 0f;
            for (int i = 0; i < Distances.Length; i++)
            {
                float d = Distances[i];
                float m = 0f;
                float cf = CrowdFactor(w, f, d);
                float roleCrowd = 1f + cw * (cf - 1f);
                for (int k = 0; k < Targets.Length; k++)
                {
                    float dps = RangedDps(w, f, Targets[k], d);
                    r.Dps[DistanceKeys[i] + "/" + Targets[k].Key] = Round2(dps);
                    float n = Normalised(dps, d, w);
                    m += mix[k] * n;
                    perTarget[k] += dw[i] * n * roleCrowd;
                }
                r.Sub[DistanceKeys[i]] = Scale(m * roleCrowd, false);
                crowdNum += dw[i] * m * cf;
                crowdDen += dw[i] * m;
            }
            r.CrowdFactor = crowdDen > 0f ? crowdNum / crowdDen : 1f;
            r.Sub["unarmoured"] = Scale(perTarget[0], false);
            r.Sub["armoured"] = Scale(perTarget[1], false);
            r.Sub["heavy"] = Scale(perTarget[2], false);
            r.Sub["mechanoids"] = Scale(0.5f * (perTarget[3] + perTarget[4]), false);
            r.Sub["crowd"] = Scale(crowdNum, false);
            float fv = RangedFightValue(w, f, r.Role);
            r.PassiveFactor = PassiveFactor(passives, false);
            fv *= r.PassiveFactor;
            if (!w.OneUse) fv *= StopFactor(w.StoppingPower) * ReachFactor(w.Range);
            fv *= FriendlyFireFactor(w.ExplosionRadius);
            if (abilities != null)
                foreach (var a in abilities) r.AbilityValue += AbilityValue(a, f, mix);
            r.AbilityValue = Math.Min(r.AbilityValue, AbilityShareCap * Math.Max(fv, 1f));
            fv += r.AbilityValue;
            if (r.Role == "utility" && r.AbilityValue <= 0f) fv = 0f;
            r.FightValue = fv;
            r.Overall = Scale(fv, false);
            float plain = RangedFightValue(w, f, r.Role);
            r.ReachFactor = w.OneUse ? 1f : ReachFactor(w.Range);
            var good = Apply(baseFighter, passives);
            good.ShootingSkill += 8f;
            r.SkillGain = plain > 0f ? RangedFightValue(w, good, r.Role) / plain : 1f;
            Describe(r, w, null, passives, abilities, mass);
            return r;
        }

        /// <summary>Stopping power that staggers a man (1 or more) is worth a little against a charge, a little more when it staggers bigger things (2.5).</summary>
        public static float StopFactor(float stoppingPower) => stoppingPower >= 2.5f ? 1.06f : stoppingPower >= 1f ? 1.03f : 1f;

        /// <summary>
        /// Rates a melee weapon: its damage per second against each reference enemy, weighed by the role's mix, times what
        /// its passive stats do, plus its abilities.
        /// </summary>
        public static WeaponRating RateMelee(WeaponMeleeInput w, float mass, WeaponFighter fighter = null, IEnumerable<WeaponPassive> passives = null,
            IEnumerable<WeaponAbilityInput> abilities = null, string role = null)
        {
            var f = Apply(fighter ?? Reference, passives);
            var r = new WeaponRating { Kind = "melee", Role = role ?? MeleeRole(mass), Skill = "Melee" };
            var mix = r.Role == "light melee" ? LightMeleeMix : HeavyMeleeMix;
            var per = new float[Targets.Length];
            for (int k = 0; k < Targets.Length; k++)
            {
                per[k] = MeleeDps(w, f, Targets[k]);
                r.Dps["melee/" + Targets[k].Key] = Round2(per[k]);
            }
            r.Sub["unarmoured"] = Scale(per[0], true);
            r.Sub["armoured"] = Scale(per[1], true);
            r.Sub["heavy"] = Scale(per[2], true);
            r.Sub["mechanoids"] = Scale(0.5f * (per[3] + per[4]), true);
            float fv = MeleeFightValue(w, f, r.Role);
            r.PassiveFactor = PassiveFactor(passives, true);
            fv *= r.PassiveFactor;
            if (abilities != null)
                foreach (var a in abilities) r.AbilityValue += AbilityValue(a, f, mix);
            r.AbilityValue = Math.Min(r.AbilityValue, AbilityShareCap * Math.Max(fv, 1f));
            fv += r.AbilityValue;
            r.FightValue = fv;
            r.Overall = Scale(fv, true);
            Describe(r, null, w, passives, abilities, mass);
            return r;
        }

        // ---------------------------------------------------------------- specials, tags and situations

        /// <summary>
        /// The specials the numbers show, each named and counted: explosions and their size, fire, EMP, stun, toxins,
        /// extra damage, stopping power, one use, a beam, armour piercing, cuts that bleed against blunt blows, non-lethal
        /// rounds, and every stat it changes on its wielder. The caller adds what it can only name (a mod's own verb or
        /// comp, biocoding, a persona, unique traits).
        /// </summary>
        public static List<WeaponSpecial> Specials(WeaponRangedInput w, WeaponMeleeInput m, IEnumerable<WeaponPassive> passives,
            IEnumerable<WeaponAbilityInput> abilities)
        {
            var list = new List<WeaponSpecial>();
            void Add(string key, string label, bool counted = true, int sign = 1)
            {
                foreach (var s in list) if (s.Key == key) return;
                list.Add(new WeaponSpecial { Key = key, Label = label, Counted = counted, Sign = sign });
            }
            var payloads = new List<WeaponPayload>();
            if (w != null)
            {
                payloads.AddRange(w.Payloads);
                if (w.ExplosionRadius >= 1f)
                    Add("explosive", "explodes (radius " + w.ExplosionRadius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "), friendly fire if near its own side");
                if (w.ExplosionRadius > 0.5f && w.ExplosionRadius < 1f) Add("explosive", "small blast on impact");
                if (w.OneUse) Add("one-use", "one use only: spent after its volley");
                if (w.StoppingPower >= 2.5f) Add("stopping", "great stopping power (" + w.StoppingPower.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "): staggers even big beasts");
                else if (w.StoppingPower >= 1f) Add("stopping", "stopping power (" + w.StoppingPower.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "): staggers a man");
                if (w.Beam) Add("beam", "a sweeping beam or spray: strikes everything along its sweep");
                if (w.Line) Add("line", "a line: strikes whoever stands on it, not only the target");
                if (w.ProjectilesPerShot > 1) Add("shot", w.ProjectilesPerShot + " projectiles a shot");
                if (w.DamageFallsWithDistance) Add("falloff", "hits twice as hard point blank, fading to nothing at its range");
                if (w.HarmsHealth && w.Armour == WeaponArmour.None && w.Damage > 0f) Add("armour-ignoring", "ignores armour");
                else if (w.HarmsHealth && w.ArmourPen >= 0.5f) Add("piercing", "pierces armour (" + Pct(w.ArmourPen) + ")");
                if (w.NonLethal) Add("non-lethal", "non-lethal: brings them down alive");
                if (w.HarmsHealth && w.Armour == WeaponArmour.Heat && w.Damage > 0f) Add("fire", "burns (heat damage; mechanoids shrug it off)");
                if (w.ForcedMissRadius > 0.5f && w.ExplosionRadius < 1f) Add("scatter", "scatters: lands anywhere within " + w.ForcedMissRadius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " cells");
                if (w.MinRange > 1.5f) Add("min-range", "can't fire within " + w.MinRange.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " cells", true, -1);
            }
            if (m != null)
            {
                float sharp = 0f, blunt = 0f, best = 0f;
                foreach (var a in m.Attacks)
                {
                    payloads.AddRange(a.Payloads);
                    if (a.IsCut) sharp += a.Power; else if (a.IsBlunt) blunt += a.Power;
                    float pen = a.ArmourPen >= 0f ? a.ArmourPen : a.Power * 0.015f;
                    if (pen > best) best = pen;
                }
                if (sharp > blunt) Add("bleeding", "cuts and stabs: wounds bleed, more killing than downing");
                else if (blunt > 0f) Add("blunt", "blunt blows: bruises and broken bones, fewer deaths, a blow to the head can stun");
                if (best >= 0.5f) Add("piercing", "pierces armour (" + Pct(best) + ")");
                if (m.NonLethal) Add("non-lethal", "non-lethal: brings them down alive");
            }
            foreach (var p in payloads)
            {
                switch (p.Effect)
                {
                    case WeaponEffect.Damage:
                        Add("extra:" + (p.Name ?? ""), "extra " + (p.Name ?? "damage") + " (" + p.Amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                            (p.Chance < 1f ? ", " + Pct(p.Chance) + " of hits" : "") + ")");
                        break;
                    case WeaponEffect.Stun: Add("stun", "stuns (" + (p.Amount * 0.5f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s a hit" + (p.Chance < 1f ? ", " + Pct(p.Chance) + " of hits" : "") + ")"); break;
                    case WeaponEffect.Emp: Add("emp", "EMP: stuns mechanoids (" + (p.Amount * 0.5f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s), breaks shield belts"); break;
                    case WeaponEffect.Ignite: Add("ignite", "sets people alight" + (p.Chance < 1f ? " (" + Pct(p.Chance) + ")" : "")); break;
                    case WeaponEffect.ToxicBuildup: Add("toxic", "toxic buildup on the living"); break;
                    case WeaponEffect.ToxGas: Add("toxgas", "toxic gas cloud (the unprotected sicken; mechanoids immune)"); break;
                }
            }
            bool melee = m != null;
            if (passives != null)
                foreach (var p in passives)
                {
                    bool counted = PassiveCounted(p.Stat, melee);
                    int sign = PassiveSign(p.Stat, p.Offset);
                    Add("passive:" + p.Stat, StatWords(p.Stat) + " " + (p.Offset >= 0f ? "+" : "") + StatNumber(p.Stat, p.Offset) + " while held", counted, sign);
                }
            if (abilities != null)
                foreach (var a in abilities)
                    Add("ability:" + a.Label, "grants " + a.Label + (a.Charges > 0 ? " (" + a.Charges + " charges)" : "") +
                        (a.Counted ? "" : ": its own effect, not counted"), a.Counted);
            return list;
        }

        /// <summary>Whether a stat change helps (1) or hinders (-1) the one holding the weapon.</summary>
        public static int PassiveSign(string stat, float offset)
        {
            bool lowerIsBetter = stat == "AimingDelayFactor" || stat == "IncomingDamageFactor" || stat == "RangedCooldownFactor" ||
                                 stat == "MeleeCooldownFactor" || stat == "PawnTrapSpringChance" || stat == "ComfyTemperatureMin";
            if (offset == 0f) return 0;
            return (offset > 0f) != lowerIsBetter ? 1 : -1;
        }

        static string StatNumber(string stat, float v)
        {
            switch (stat)
            {
                case "MoveSpeed": return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " c/s";
                case "ShootingAccuracyPawn":
                case "MeleeHitChance":
                case "MeleeDodgeChance": return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                case "ComfyTemperatureMin":
                case "ComfyTemperatureMax": return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " C";
                default: return Math.Abs(v) < 5f ? Pct(v) : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>A stat's name in plain words.</summary>
        public static string StatWords(string stat)
        {
            switch (stat)
            {
                case "MoveSpeed": return "move speed";
                case "ShootingAccuracyPawn": return "shooting accuracy";
                case "AimingDelayFactor": return "aiming time";
                case "MeleeHitChance": return "melee hit chance";
                case "MeleeDodgeChance": return "melee dodge chance";
                case "IncomingDamageFactor": return "damage taken";
                case "PainShockThreshold": return "pain threshold";
                case "PsychicSensitivityOffset": return "psychic sensitivity";
                case "PsychicEntropyRecoveryRate": return "neural heat recovery";
                case "WorkSpeedGlobal": return "work speed";
                case "PlantWorkSpeed": return "plant work speed";
                case "HuntingStealth": return "hunting stealth";
                case "ConstructionSpeed": return "construction speed";
                case "MiningSpeed": return "mining speed";
                case "ComfyTemperatureMin": return "lowest comfortable temperature";
                case "ComfyTemperatureMax": return "highest comfortable temperature";
                case "RangedCooldownFactor": return "ranged cooldown";
                case "MeleeCooldownFactor": return "melee cooldown";
                case "DeflectChance": return "chance to deflect shots";
                default:
                    var sb = new System.Text.StringBuilder();
                    foreach (char c in stat)
                    {
                        if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                        sb.Append(char.ToLowerInvariant(c));
                    }
                    return sb.ToString();
            }
        }

        /// <summary>
        /// Tags for recommendations and filters, from the numbers: heavy, sidearm-friendly, offhand-capable (likely; Dual
        /// Wield's own settings decide), slow to fire, quick, bursts, needs a good shot, armour-piercing, explosive, one-use,
        /// stuns, EMP, incendiary, non-lethal.
        /// </summary>
        static void Describe(WeaponRating r, WeaponRangedInput w, WeaponMeleeInput m, IEnumerable<WeaponPassive> passives,
            IEnumerable<WeaponAbilityInput> abilities, float mass)
        {
            float o = r.Overall;
            r.Specials = Specials(w, m, passives, abilities);
            bool slows = false;
            if (passives != null) foreach (var p in passives) if (p.Stat == "MoveSpeed" && p.Offset < 0f) slows = true;
            r.Situations["mechanoids"] = r.Sub["mechanoids"];
            r.Situations["heavily armoured"] = r.Sub["heavy"];
            bool has(WeaponEffect e) => (w != null && HasEffect(w.Payloads, e)) || (m != null && m.Attacks.Exists(a => HasEffect(a.Payloads, e)));
            if (w != null)
            {
                bool blast = w.ExplosionRadius >= 1f;
                bool fire = has(WeaponEffect.Ignite) || (w.Armour == WeaponArmour.Heat && w.HarmsHealth && w.Damage > 0f);
                r.Situations["crowds"] = r.Sub["crowd"];
                r.Situations["hunting"] = blast || fire || !w.HarmsHealth || w.OneUse ? 1f : Mean(r.Sub["medium"], r.Sub["long"]);
                r.Situations["indoors"] = blast || fire ? Math.Max(1f, o - 3f) : Math.Max(r.Sub["close"], 1f);
                r.Situations["holding a choke"] = Clamp(Mean(r.Sub["close"], r.Sub["medium"]) + (w.StoppingPower >= 1f ? 0.5f : 0f) +
                                                        (r.CrowdFactor > 1.3f ? 0.5f : 0f), 1f, 10f);
                r.Situations["kiting"] = w.Range >= EnemyReach && !slows ? Math.Max(r.Sub["long"], r.Sub["far"]) : Math.Max(1f, r.Sub["long"] - 1.5f);
                r.Situations["siege or long duel"] = w.Range > EnemyReach ? Math.Max(r.Sub["far"], r.Sub["long"]) : Math.Max(1f, r.Sub["long"] - 1.5f);
                r.Situations["capture alive"] = w.NonLethal ? Math.Max(o, 6f) : Math.Max(1f, o - 3.5f);
                r.Situations["sidearm"] = mass > 0f && mass <= 2f && !w.OneUse ? o : Math.Max(1f, o - 3f);
                if (blast) r.Tags.Add("explosive");
                if (fire) r.Tags.Add("incendiary");
                if (w.OneUse) r.Tags.Add("one-use");
                if (w.Burst >= 3) r.Tags.Add("bursts");
                if (w.Warmup >= 2f || CycleSeconds(w, Reference) >= 4.5f) r.Tags.Add("slow to fire");
                if (r.SkillGain >= 1.6f) r.Tags.Add("needs a good shot");
                if (w.HarmsHealth && w.ArmourPen >= 0.35f) r.Tags.Add("armour-piercing");
                if (mass > 0f && mass <= 2f && !w.OneUse) r.Tags.Add("sidearm-friendly");
                if (mass > 0f && mass <= 1.6f && w.Range < 30f && !blast) r.Tags.Add("offhand-capable (likely)");
                if (w.Range > EnemyReach) r.Tags.Add("outranges rifles");
            }
            else
            {
                float minCd = float.MaxValue, sharp = 0f, blunt = 0f;
                foreach (var a in m.Attacks)
                {
                    minCd = Math.Min(minCd, a.Cooldown * (m.Stuffed ? m.CooldownFactor : 1f));
                    if (a.IsCut) sharp += a.Power; else if (a.IsBlunt) blunt += a.Power;
                }
                r.Situations["duel"] = Mean(r.Sub["armoured"], r.Sub["heavy"]);
                r.Situations["crowds"] = Math.Max(1f, o - 1.5f);
                r.Situations["hunting"] = Math.Max(1f, o - 3f);
                r.Situations["indoors"] = o;
                r.Situations["holding a choke"] = o;
                r.Situations["capture alive"] = m.NonLethal ? Math.Max(o, 6f) : blunt >= sharp ? Math.Max(1f, o - 0.5f) : Math.Max(1f, o - 2.5f);
                r.Situations["sidearm"] = mass > 0f && mass < 1.5f ? o : Math.Max(1f, o - 2f);
                if (minCd <= 1.6f) r.Tags.Add("quick");
                if (mass > 0f && mass < 1.5f) r.Tags.Add("sidearm-friendly");
                if (mass > 0f && mass < 1.5f) r.Tags.Add("offhand-capable (likely)");
                if (mass >= 2.5f) r.Tags.Add("two-handed (likely)");
                if (r.Sub["heavy"] >= o) r.Tags.Add("armour-piercing");
                r.Tags.Add(blunt >= sharp ? "blunt" : "sharp");
            }
            if (slows || mass >= 8f) r.Tags.Add("heavy");
            if (has(WeaponEffect.Emp)) r.Tags.Add("EMP");
            if (has(WeaponEffect.Stun)) r.Tags.Add("stuns");
            if (has(WeaponEffect.ToxGas) || has(WeaponEffect.ToxicBuildup)) r.Tags.Add("toxic");
            if ((w != null && w.NonLethal) || (m != null && m.NonLethal)) r.Tags.Add("non-lethal");
            // What it's best for and weak when: situations well above or below its own overall.
            foreach (var kv in r.Situations)
            {
                if (kv.Value >= Math.Max(o + 1f, 6f)) r.BestFor.Add(kv.Key);
                else if (kv.Value <= Math.Min(o - 1.5f, 4f)) r.WeakWhen.Add(kv.Key);
            }
        }

        static bool HasEffect(List<WeaponPayload> ps, WeaponEffect e)
        {
            if (ps == null) return false;
            foreach (var p in ps) if (p.Effect == e && p.Chance > 0f) return true;
            return false;
        }

        // ---------------------------------------------------------------- small helpers

        public static float Curve(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
                if (x <= xs[i]) return Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
            return ys[ys.Length - 1];
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        static float Mean(float a, float b) => (float)Math.Round((a + b) / 2f, 1);

        static float Round2(float v) => (float)Math.Round(v, 2);

        static string Pct(float v) => Math.Round(v * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
