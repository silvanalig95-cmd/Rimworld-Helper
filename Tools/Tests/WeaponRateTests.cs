using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The base game's weapons as their defs write them (Data/Core and Royalty, normal quality, steel where made of stuff),
/// the anchors the weapon rating is calibrated on (Weapons/WeaponRateMath).
/// </summary>
public static class WeaponAnchors
{
    public static WeaponRangedInput Gun(float dmg, float warm, float cd, float range, float t, float s, float m, float l,
        int burst = 1, int ticks = 15, float pen = -1f, float stop = 0.5f)
    {
        return new WeaponRangedInput
        {
            Damage = dmg, ArmourPen = pen >= 0f ? pen : dmg * 0.015f, StoppingPower = stop, Warmup = warm, Cooldown = cd,
            Range = range, AccTouch = t, AccShort = s, AccMedium = m, AccLong = l, Burst = burst, TicksBetweenBurst = ticks
        };
    }

    public static WeaponMeleeAttack Hit(string label, WeaponArmour kind, float power, float cd, float pen = -1f) =>
        new WeaponMeleeAttack { Label = label, Armour = kind, Power = power, Cooldown = cd, ArmourPen = pen };

    public static WeaponMeleeInput Blade(params WeaponMeleeAttack[] attacks) => Stuffed(1f, 1f, 1f, attacks);

    public static WeaponMeleeInput Stuffed(float sharp, float blunt, float cd, params WeaponMeleeAttack[] attacks) =>
        new WeaponMeleeInput { Attacks = attacks.ToList(), Stuffed = true, SharpFactor = sharp, BluntFactor = blunt, CooldownFactor = cd };

    public static WeaponMeleeInput Ultra(params WeaponMeleeAttack[] attacks) => new WeaponMeleeInput { Attacks = attacks.ToList() };

    const WeaponArmour S = WeaponArmour.Sharp, B = WeaponArmour.Blunt;

    public static readonly Dictionary<string, WeaponRangedInput> Ranged = new Dictionary<string, WeaponRangedInput>
    {
        ["short bow"] = Gun(11, 1.35f, 1.65f, 22.9f, .75f, .65f, .45f, .25f),
        ["recurve bow"] = Gun(14, 1.45f, 1.65f, 25.9f, .70f, .78f, .65f, .35f, stop: 1f),
        ["greatbow"] = Gun(17, 2.0f, 1.5f, 29.9f, .65f, .85f, .75f, .50f, pen: .15f, stop: 1.5f),
        ["pila"] = Gun(25, 4f, 2.5f, 18.9f, .80f, .71f, .50f, .32f, pen: .10f, stop: 2.5f),
        ["revolver"] = Gun(12, 0.3f, 1.6f, 25.9f, .80f, .75f, .55f, .40f, stop: 1f),
        ["autopistol"] = Gun(10, 0.3f, 1.0f, 25.9f, .80f, .70f, .40f, .30f),
        ["machine pistol"] = Gun(6, 0.5f, 0.9f, 19.9f, .90f, .65f, .35f, .15f, burst: 3, ticks: 7),
        ["pump shotgun"] = Gun(18, 0.9f, 1.25f, 15.9f, .80f, .87f, .77f, .64f, pen: .14f, stop: 3f),
        ["chain shotgun"] = Gun(18, 1.2f, 1.35f, 12.9f, .57f, .64f, .55f, .45f, burst: 3, ticks: 10, pen: .14f, stop: 3f),
        ["heavy SMG"] = Gun(12, 0.9f, 1.65f, 22.9f, .85f, .65f, .35f, .20f, burst: 3, ticks: 11),
        ["bolt-action rifle"] = Gun(18, 1.7f, 1.5f, 36.9f, .65f, .80f, .90f, .80f, stop: 1.5f),
        ["assault rifle"] = Gun(11, 1.0f, 1.7f, 30.9f, .60f, .70f, .65f, .55f, burst: 3, ticks: 10),
        ["LMG"] = Gun(12, 1.8f, 1.6f, 25.9f, .40f, .48f, .35f, .26f, burst: 6, ticks: 7, stop: 1f),
        ["sniper rifle"] = Gun(25, 3.5f, 1.5f, 44.9f, .50f, .70f, .88f, .90f, stop: 1.5f),
        ["minigun"] = Gun(10, 2.5f, 1.5f, 30.9f, .20f, .25f, .25f, .18f, burst: 25, ticks: 5),
        ["charge rifle"] = Gun(16, 1.0f, 2.0f, 27.9f, .55f, .64f, .55f, .45f, burst: 3, ticks: 12, pen: .35f),
        ["charge lance"] = Gun(30, 1.7f, 2.7f, 32.9f, .65f, .85f, .85f, .75f, stop: 1.5f),
    };

    /// <summary>The minigun and the beam repeater slow whoever carries them by 0.25 cells a second.</summary>
    public static readonly WeaponPassive[] Slow = { new WeaponPassive("MoveSpeed", -0.25f) };

    public static WeaponRangedInput FragGrenade() => new WeaponRangedInput
    {
        Damage = 50, ArmourPen = 0.10f, ExplosionRadius = 1.9f, ForcedMissRadius = 1.9f, Warmup = 1.5f, Cooldown = 2.66f, Range = 12.9f,
        ExplosionDelaySeconds = 100f / 60f
    };

    public static WeaponRangedInput EmpGrenade() => new WeaponRangedInput
    {
        Damage = 50, HarmsHealth = false, Armour = WeaponArmour.None, ExplosionRadius = 3.5f, ForcedMissRadius = 1.9f, Warmup = 1.5f,
        Cooldown = 2.66f, Range = 12.9f, ExplosionDelaySeconds = 100f / 60f, Payloads = { WeaponPayload.Of(WeaponEffect.Emp, 50) }
    };

    public static WeaponRangedInput TripleRocket() => new WeaponRangedInput
    {
        Damage = 50, ArmourPen = 0.10f, ExplosionRadius = 3.9f, ForcedMissRadius = 2.9f, Burst = 3, TicksBetweenBurst = 20, Warmup = 4.5f,
        Cooldown = 4.5f, Range = 35.9f, OneUse = true
    };

    public static readonly Dictionary<string, (WeaponMeleeInput w, float mass)> Melee = new Dictionary<string, (WeaponMeleeInput, float)>
    {
        ["knife"] = (Blade(Hit("handle", B, 9, 2), Hit("blade", S, 12, 1.5f), Hit("point", S, 13, 2)), 0.5f),
        ["gladius"] = (Blade(Hit("handle", B, 9, 2), Hit("point", S, 16, 2), Hit("edge", S, 16, 2)), 0.85f),
        ["ikwa"] = (Blade(Hit("handle", B, 9, 2), Hit("handle", B, 9, 2), Hit("head", S, 15, 2), Hit("edge", S, 15, 2)), 1.1f),
        ["club"] = (Blade(Hit("handle", B, 9, 2), Hit("head", B, 14, 2)), 2f),
        ["mace"] = (Blade(Hit("handle", B, 9, 2), Hit("head", B, 15.7f, 2)), 1.25f),
        ["axe"] = (Blade(Hit("handle", B, 9, 2), Hit("edge", S, 15, 2)), 1.5f),
        ["spear"] = (Blade(Hit("shaft", B, 13, 2.6f), Hit("shaft", B, 13, 2.6f), Hit("point", S, 23, 2.6f, 0.5f)), 2f),
        ["longsword"] = (Blade(Hit("handle", B, 9, 2), Hit("point", S, 23, 2.6f), Hit("edge", S, 23, 2.6f)), 2f),
        ["warhammer"] = (Blade(Hit("handle", B, 11, 2.6f), Hit("head", B, 20, 2.6f)), 5f),
        ["plasteel longsword"] = (Stuffed(1.1f, 0.9f, 0.8f, Hit("handle", B, 9, 2), Hit("point", S, 23, 2.6f), Hit("edge", S, 23, 2.6f)), 2f),
        ["uranium longsword"] = (Stuffed(1.1f, 1.5f, 1.1f, Hit("handle", B, 9, 2), Hit("point", S, 23, 2.6f), Hit("edge", S, 23, 2.6f)), 2f),
        ["uranium mace"] = (Stuffed(1.1f, 1.5f, 1.1f, Hit("handle", B, 9, 2), Hit("head", B, 15.7f, 2)), 1.25f),
        ["monosword"] = (Ultra(Hit("handle", B, 12, 1.6f), Hit("point", S, 25, 2, 0.9f), Hit("edge", S, 25, 2, 0.9f)), 2f),
        ["plasma sword"] = (Ultra(Hit("handle", B, 12, 2), Plasma(Hit("point", S, 21, 2.6f)), Plasma(Hit("edge", S, 21, 2.6f))), 2f),
        ["zeushammer"] = (Ultra(Hit("handle", B, 15, 2), Emp(Hit("head", B, 31, 3))), 2f),
    };

    static WeaponMeleeAttack Plasma(WeaponMeleeAttack a)
    {
        a.Payloads.Add(new WeaponPayload { Effect = WeaponEffect.Damage, Amount = 10, Armour = WeaponArmour.Heat, ArmourPen = 0.15f, Chance = 0.5f, Name = "flame" });
        a.Payloads.Add(WeaponPayload.Of(WeaponEffect.Ignite, 1f, 0.5f * 0.7f));
        return a;
    }

    static WeaponMeleeAttack Emp(WeaponMeleeAttack a)
    {
        a.Payloads.Add(WeaponPayload.Of(WeaponEffect.Emp, 9));
        return a;
    }

    public static WeaponRating RateRanged(string name) =>
        WeaponRateMath.RateRanged(Ranged[name], passives: name == "minigun" ? Slow : null, mass: name == "minigun" ? 10f : 3f);

    public static WeaponRating RateMelee(string name) => WeaponRateMath.RateMelee(Melee[name].w, Melee[name].mass);

    /// <summary>Every anchor's rating, for the README and for reading the calibration.</summary>
    public static IEnumerable<(string name, WeaponRating r)> All()
    {
        foreach (var k in Ranged.Keys) yield return (k, RateRanged(k));
        yield return ("frag grenades", WeaponRateMath.RateRanged(FragGrenade(), mass: 1f));
        yield return ("EMP grenades", WeaponRateMath.RateRanged(EmpGrenade(), mass: 1f));
        yield return ("triple rocket launcher", WeaponRateMath.RateRanged(TripleRocket(), mass: 7f));
        foreach (var k in Melee.Keys) yield return (k, RateMelee(k));
    }
}

/// <summary>
/// The weapon rating (Weapons/WeaponRateMath): the game's own rules (accuracy, armour, the melee choice), the reference
/// fighter and enemies, the roles, and the base game's weapons in the order experienced players know them.
/// </summary>
public static class WeaponRateTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "weapon rating: " + what);
        }
        bool Near(float a, float b, float eps = 0.005f) => Math.Abs(a - b) <= eps;

        // ---------- The game's own rules ----------
        C(Near(WeaponRateMath.ShootingAccuracy(8f), 0.96f), "skill 8 shoots 96% a cell (Stats_Pawns_Combat)");
        C(Near(WeaponRateMath.ShootingAccuracy(3f), 0.935f) && Near(WeaponRateMath.ShootingAccuracy(13f), 0.9775f),
            "trigger-happy (-5) and careful shooter (+5) move the skill before the curve");
        C(Near(WeaponRateMath.MeleeHitChance(8f), 0.74f), "skill 8 hits 74% in melee");
        C(Near(WeaponRateMath.MeleeDodgeChance(8f), 0.06f) && WeaponRateMath.MeleeDodgeChance(0f) == 0f, "skill 8 dodges 6%, a mechanoid none");
        C(Near(WeaponRateMath.ShooterFactor(0.96f, 12f), (float)Math.Pow(0.96, 12)), "the shooter's factor is accuracy to the power of the distance");
        C(Near(WeaponRateMath.ShooterFactor(0.5f, 40f), 0.0201f), "never below the game's floor of 2.01%");
        var ar = WeaponAnchors.Ranged["assault rifle"];
        C(Near(WeaponRateMath.WeaponAccuracy(ar, 3f), 0.60f) && Near(WeaponRateMath.WeaponAccuracy(ar, 12f), 0.70f) &&
          Near(WeaponRateMath.WeaponAccuracy(ar, 18.5f), 0.675f) && Near(WeaponRateMath.WeaponAccuracy(ar, 50f), 0.55f),
            "the weapon's accuracy lerps between 3, 12, 25 and 40 cells");
        C(Near(WeaponRateMath.ArmourFactor(0f, 0f), 1f) && Near(WeaponRateMath.ArmourFactor(0.5f, 0f), 0.625f) &&
          Near(WeaponRateMath.ArmourFactor(1f, 0f), 0.25f) && Near(WeaponRateMath.ArmourFactor(1.5f, 0f), 0.125f) &&
          WeaponRateMath.ArmourFactor(2f, 0f) == 0f, "the armour roll: deflected e/2, halved e/2, nothing through from 2");
        C(Near(WeaponRateMath.ArmourFactor(1f, 0.4f), WeaponRateMath.ArmourFactor(0.6f, 0f)), "piercing comes off the armour");
        C(WeaponRateMath.CellsInRadius(1.9f) == 9 && WeaponRateMath.CellsInRadius(2.9f) == 25 && WeaponRateMath.CellsInRadius(0.5f) == 1,
            "a radius of 1.9 is the 3 by 3 square, 2.9 is 25 cells");
        C(WeaponRateMath.AdjustedForcedMiss(1.9f, 2.5f) == 0f && Near(WeaponRateMath.AdjustedForcedMiss(1.9f, 4f), 0.95f) &&
          Near(WeaponRateMath.AdjustedForcedMiss(1.9f, 6f), 1.52f) && WeaponRateMath.AdjustedForcedMiss(1.9f, 12f) == 1.9f,
            "a throw scatters less close in, as the game's forced miss does");
        C(Near(WeaponRateMath.MaxWildMiss(0.02f), 10f) && Near(WeaponRateMath.MaxWildMiss(1f), 1f) && Near(WeaponRateMath.MaxWildMiss(0.22f), 2f),
            "a wild miss lands farther the worse the aim");
        float sum = 0f;
        foreach (var z in WeaponRateMath.Flak.Zones) sum += z.share;
        C(Near(sum, 1f), "the flak raider's zones cover the whole body");

        // ---------- Hit chance and damage ----------
        float dps12 = WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f);
        float expect = (float)Math.Pow(0.96, 12) * 0.70f * 11f * 3f / (1.0f + 1.7f + 20f / 60f) * WeaponRateMath.PunchFactor(11f);
        C(Near(dps12, expect, 0.01f), $"an assault rifle at 12 cells: hit chance times damage over the cycle ({dps12:0.00} vs {expect:0.00})");
        C(WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 40f) == 0f, "nothing past its range");
        C(WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Flak, 12f) < dps12 &&
          WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Heavy, 12f) < WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Flak, 12f),
            "armour takes its share, heavy armour more");
        var lance = WeaponAnchors.Ranged["charge lance"];
        float lanceKeep = WeaponRateMath.RangedDps(lance, WeaponRateMath.Reference, WeaponRateMath.Heavy, 12f) / WeaponRateMath.RangedDps(lance, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f);
        float arKeep = WeaponRateMath.RangedDps(ar, WeaponRateMath.Reference, WeaponRateMath.Heavy, 12f) / dps12;
        C(lanceKeep > arKeep + 0.1f, $"the charge lance's piercing keeps more against marine armour ({lanceKeep:0.00} vs {arKeep:0.00})");
        var best = WeaponAnchors.Ranged["revolver"];
        var good = WeaponRateMath.Reference;
        good.ShootingSkill = 16f;
        C(WeaponRateMath.RangedDps(best, good, WeaponRateMath.Unarmoured, 20f) > WeaponRateMath.RangedDps(best, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 20f),
            "a better shot hits more");
        var frag = WeaponAnchors.FragGrenade();
        float blast = WeaponRateMath.BlastChance(frag, 0.5f, 0.5f, 12f);
        C(blast > 0.9f, $"a frag grenade's blast covers its whole scatter: the target is caught ({blast:0.00})");
        float sniperCrowd = WeaponRateMath.CrowdFactor(WeaponAnchors.Ranged["sniper rifle"], WeaponRateMath.Reference, 25f);
        C(WeaponRateMath.CrowdFactor(frag, WeaponRateMath.Reference, 12f) > 1.5f &&
          WeaponRateMath.CrowdFactor(WeaponAnchors.Ranged["minigun"], WeaponRateMath.Reference, 25f) > sniperCrowd + 0.3f,
            "a crowd: a blast catches several, a sprayed burst strays into others, a sniper's careful shot little");
        var emp = WeaponAnchors.EmpGrenade();
        C(WeaponRateMath.RangedDps(emp, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f) == 0f &&
          WeaponRateMath.RangedDps(emp, WeaponRateMath.Reference, WeaponRateMath.MechLight, 12f) > 2f,
            "EMP does nothing to flesh and stuns mechanoids");
        float empCap = WeaponRateMath.StunValuePerSecond(emp.Payloads, WeaponRateMath.MechLight, 100f);
        C(Near(empCap, 25f / WeaponRateMath.EmpAdaptSeconds * WeaponRateMath.MechStunSecondValue, 0.01f),
            "a mechanoid adapts: EMP stuns it once per stun and adaptation, however often it lands");
        var stun = new List<WeaponPayload> { WeaponPayload.Of(WeaponEffect.Stun, 20) };
        C(Near(WeaponRateMath.StunValuePerSecond(stun, WeaponRateMath.Unarmoured, 10f), WeaponRateMath.StunSecondValue),
            "stuns don't stack: never more than the time fought");

        // ---------- Melee: the game's choice of attack ----------
        var sword = WeaponAnchors.Melee["longsword"].w;
        var shares = WeaponRateMath.MeleeShares(sword, WeaponRateMath.Reference);
        float Share(string label) => shares.Where(x => x.attack.Label == label).Sum(x => x.share);
        C(Near(Share("point"), 0.375f) && Near(Share("edge"), 0.375f), "a longsword's point and edge are its best: three quarters between them");
        C(Near(Share("handle") + Share("left fist") + Share("right fist"), 0.25f), "the handle and the fists share the last quarter");
        C(Share("teeth") == 0f && Share("head") == 0f, "teeth and head are never picked beside a sword");
        var mono = WeaponAnchors.Melee["monosword"].w;
        var monoShares = WeaponRateMath.MeleeShares(mono, WeaponRateMath.Reference);
        C(monoShares.Where(x => x.attack.Natural).Sum(x => x.share) == 0f, "a monosword is so much better than fists they drop out");
        var plasteel = WeaponAnchors.Melee["plasteel longsword"].w;
        var nums = WeaponRateMath.MeleeNumbers(plasteel.Attacks[1], plasteel, WeaponRateMath.Reference);
        C(Near(nums.damage, 25.3f, 0.01f) && Near(nums.cooldown, 2.08f, 0.01f) && Near(nums.pen, 25.3f * 0.015f, 0.001f),
            "plasteel: sharp 110%, cooldown 80%, piercing 1.5% of the damage");
        var monoNums = WeaponRateMath.MeleeNumbers(mono.Attacks[1], mono, WeaponRateMath.Reference);
        C(Near(monoNums.pen, 0.9f), "a set piercing stays as written");
        float swordFlak = WeaponRateMath.MeleeDps(sword, WeaponRateMath.Reference, WeaponRateMath.Flak);
        float swordBare = WeaponRateMath.MeleeDps(sword, WeaponRateMath.Reference, WeaponRateMath.Unarmoured);
        C(swordFlak < swordBare, "armour takes its share in melee too");
        var zeus = WeaponAnchors.Melee["zeushammer"].w;
        C(WeaponRateMath.MeleeDps(zeus, WeaponRateMath.Reference, WeaponRateMath.MechLight) >
          WeaponRateMath.MeleeDps(WeaponAnchors.Melee["warhammer"].w, WeaponRateMath.Reference, WeaponRateMath.MechLight) * 1.5f,
            "the zeushammer's EMP makes it the hammer for mechanoids");

        // ---------- Roles ----------
        C(WeaponRateMath.RangedRole(WeaponAnchors.Ranged["sniper rifle"]) == "long", "the sniper rifle is a long gun");
        C(WeaponRateMath.RangedRole(WeaponAnchors.Ranged["pump shotgun"]) == "close" && WeaponRateMath.RangedRole(WeaponAnchors.Ranged["machine pistol"]) == "close",
            "shotguns and the machine pistol are close weapons");
        C(WeaponRateMath.RangedRole(ar) == "all-round" && WeaponRateMath.RangedRole(WeaponAnchors.Ranged["bolt-action rifle"]) == "all-round",
            "rifles are all-round");
        C(WeaponRateMath.RangedRole(frag) == "area" && WeaponRateMath.RangedRole(WeaponAnchors.TripleRocket()) == "one-use",
            "grenades are area weapons, the rocket launcher one-use");
        C(WeaponRateMath.MeleeRole(0.5f) == "light melee" && WeaponRateMath.MeleeRole(2f) == "heavy melee", "a knife is light, a longsword heavy");
        var sniper = WeaponAnchors.RateRanged("sniper rifle");
        C(sniper.Sub["close"] < sniper.Overall, "the sniper is poor at five cells, but its overall doesn't pay for it");

        // ---------- The scale ----------
        C(WeaponRateMath.Scale(WeaponRateMath.RangedMid, false) == 5.5f && WeaponRateMath.Scale(WeaponRateMath.MeleeMid, true) == 5.5f,
            "the middle of each scale is 5.5");
        C(WeaponRateMath.Scale(0f, false) == 1f && WeaponRateMath.Scale(1e6f, false) == 10f, "1 at nothing, 10 only for the absurd");
        C(WeaponRateMath.Scale(WeaponRateMath.RangedMid * 4f, false) < 9.7f, "soft at the top: four times the middle still short of 10");
        C(Math.Abs(WeaponRateMath.Scale(WeaponRateMath.ValueFor(7.3f, true), true) - 7.3f) < 0.051f, "the scale reads backwards");

        // ---------- The base game's weapons in the order players know ----------
        var r = WeaponAnchors.All().ToDictionary(x => x.name, x => x.r);
        float R(string k) => r[k].Overall;
        void Band(string k, float lo, float hi) => C(R(k) >= lo && R(k) <= hi, $"{k} rates {lo:0.0} to {hi:0.0} ({R(k):0.0})");
        // Ranged: light guns and bows low, rifles in the middle, the charge weapons high.
        Band("short bow", 1.5f, 3.5f);
        Band("revolver", 2.0f, 4.8f);
        Band("autopistol", 2.0f, 4.8f);
        Band("recurve bow", 2.5f, 4.5f);
        Band("bolt-action rifle", 4.3f, 6.3f);
        Band("assault rifle", 4.8f, 6.5f);
        Band("LMG", 4.3f, 6.5f);
        Band("sniper rifle", 4.3f, 6.8f);
        Band("minigun", 4.5f, 7.5f);
        Band("charge rifle", 6.2f, 8.2f);
        Band("charge lance", 6.0f, 8.2f);
        C(R("short bow") < R("recurve bow") && R("recurve bow") < R("greatbow"), "short bow, recurve bow, greatbow in order");
        C(R("revolver") < R("assault rifle") && R("autopistol") < R("assault rifle"), "pistols below the assault rifle");
        C(R("bolt-action rifle") <= R("assault rifle") + 0.3f && R("bolt-action rifle") >= R("revolver"), "the bolt-action rifle with the rifles");
        C(R("assault rifle") < R("charge rifle") && R("assault rifle") < R("charge lance"), "the charge weapons above the assault rifle");
        C(R("heavy SMG") > R("machine pistol"), "the heavy SMG above the machine pistol");
        C(R("pump shotgun") > R("revolver") && R("chain shotgun") > R("pump shotgun"), "shotguns above the revolver, the chain shotgun above the pump");
        C(R("charge rifle") >= R("LMG") && R("charge rifle") >= R("sniper rifle"), "the charge rifle above the LMG and the sniper");
        // Melee: the knife low, the longsword in the middle, plasteel and uranium higher, the ultratech near the top.
        Band("knife", 1.5f, 4.0f);
        Band("club", 2.5f, 5.0f);
        Band("gladius", 3.0f, 5.5f);
        Band("longsword", 4.5f, 6.5f);
        Band("plasteel longsword", 5.8f, 7.8f);
        Band("uranium mace", 4.5f, 7.5f);
        Band("monosword", 7.5f, 9.6f);
        Band("zeushammer", 7.0f, 9.6f);
        Band("plasma sword", 5.0f, 8.0f);
        C(R("knife") < R("gladius") && R("gladius") < R("longsword"), "knife, gladius, longsword in order");
        C(R("longsword") < R("plasteel longsword") && R("longsword") < R("uranium longsword"), "plasteel and uranium above steel");
        C(R("mace") < R("uranium mace"), "uranium makes a better mace (blunt 150%)");
        C(R("plasteel longsword") < R("monosword") && R("longsword") < R("zeushammer"), "the ultratech blades above anything forged");
        C(r["spear"].Sub["heavy"] > r["longsword"].Sub["heavy"], "the spear's piercing tells against marine armour");
        C(r["monosword"].Sub["heavy"] > r["plasteel longsword"].Sub["heavy"] + 1f, "the monosword cuts through marine armour");
        C(r["zeushammer"].Sub["mechanoids"] > r["warhammer"].Sub["mechanoids"] + 1f, "the zeushammer is the answer to mechanoids among the hammers");

        // ---------- Specials, tags and situations ----------
        var mini = r["minigun"];
        C(mini.Tags.Contains("heavy") && mini.Specials.Any(s => s.Key == "passive:MoveSpeed" && s.Sign < 0 && s.Counted),
            "the minigun is heavy and slows its wielder, counted");
        C(mini.Sub["crowd"] > mini.Sub["medium"], "the minigun is better against a crowd");
        var fr = r["frag grenades"];
        C(fr.Tags.Contains("explosive") && fr.Situations["hunting"] == 1f && fr.Situations["indoors"] < fr.Overall,
            "grenades: explosive, ruin a hunt, risky indoors");
        var em = r["EMP grenades"];
        C(em.Tags.Contains("EMP") && em.Sub["mechanoids"] > em.Sub["unarmoured"] + 3f && em.BestFor.Contains("mechanoids"),
            "EMP grenades are for mechanoids");
        C(WeaponRateMath.RangedRole(WeaponAnchors.EmpGrenade()) == "anti-mechanoid" && em.Overall > 4f, "an EMP weapon is judged on the mechanoids it is for");
        var smoke = new WeaponRangedInput { HarmsHealth = false, Armour = WeaponArmour.None, ExplosionRadius = 2.4f, ForcedMissRadius = 1.9f, Warmup = 3.5f, Cooldown = 4.5f, Range = 23.9f };
        C(WeaponRateMath.RangedRole(smoke) == "utility" && WeaponRateMath.RateRanged(smoke).Overall == 1f, "a smoke launcher is a tool, not rated for damage");
        C(r["sniper rifle"].Tags.Contains("needs a good shot") && r["sniper rifle"].Tags.Contains("outranges rifles"), "a sniper rewards a good shot and outranges");
        C(r["knife"].Tags.Contains("sidearm-friendly") && r["knife"].Tags.Contains("quick") && !r["warhammer"].Tags.Contains("sidearm-friendly"),
            "a knife is a quick sidearm, a warhammer isn't");
        C(r["triple rocket launcher"].Tags.Contains("one-use") && r["triple rocket launcher"].Specials.Any(s => s.Key == "one-use"), "one use only, named");
        C(r["charge lance"].Specials.Any(s => s.Key == "stopping"), "the charge lance's stopping power is named");
        C(r["longsword"].Specials.Any(s => s.Key == "bleeding") && r["mace"].Specials.Any(s => s.Key == "blunt"), "cuts bleed, blunt blows bruise");
        C(r["mace"].Situations["capture alive"] > r["longsword"].Situations["capture alive"], "a mace takes them alive more often than a sword");
        var psy = WeaponRateMath.Specials(null, sword, new[] { new WeaponPassive("PsychicSensitivityOffset", 0.5f) }, null);
        C(psy.Any(s => s.Key == "passive:PsychicSensitivityOffset" && !s.Counted && s.Sign > 0), "psychic sensitivity is named, not counted");
        var ability = new WeaponAbilityInput { Label = "launch frag grenade", Counted = true, Shot = WeaponAnchors.FragGrenade(), Charges = 2 };
        ability.Shot.Range = 20f;
        var withAbility = WeaponRateMath.RateRanged(ar, abilities: new[] { ability }, mass: 3.5f);
        C(withAbility.AbilityValue > 0.3f && withAbility.Overall > R("assault rifle"), $"an ability counts in the rating ({withAbility.AbilityValue:0.00})");
        var unread = new WeaponAbilityInput { Label = "a mod's own power", Counted = false };
        var withUnread = WeaponRateMath.RateRanged(ar, abilities: new[] { unread }, mass: 3.5f);
        C(withUnread.Overall == R("assault rifle") && withUnread.Specials.Any(s => s.Key.StartsWith("ability:") && !s.Counted),
            "an ability the review can't read is named, not counted");
        var careful = WeaponRateMath.Reference;
        careful.ShootingSkill = 13f;
        careful.AimFactor = 1.25f;
        C(WeaponRateMath.RateRanged(WeaponAnchors.Ranged["sniper rifle"], careful).Overall > sniper.Overall, "a careful shooter gets more from a sniper rifle");

        // ---------- The scale's soft top ----------
        float monoValue = r["monosword"].FightValue;
        C(WeaponRateMath.Scale(monoValue * 3f, true) < 9.6f && WeaponRateMath.Scale(monoValue * 3f, true) > r["monosword"].Overall + 1f,
            "a super-weapon three times the monosword rates high but short of 10");
        C(WeaponRateMath.Scale(monoValue * 10f, true) < WeaponRateMath.Scale(monoValue * 30f, true) || WeaponRateMath.Scale(monoValue * 10f, true) >= 9.8f,
            "ten and thirty times still tell apart, or both sit just under 10");
        C(Math.Abs(WeaponRateMath.Spread(0.0) - 0.5) < 1e-9 && WeaponRateMath.Spread(-50) > 0 && WeaponRateMath.Spread(50) < 1, "the curve is a half at the middle, never 0 or 1");

        // ---------- The mods' own rules, read from their code ----------
        var beam = WeaponAnchors.Gun(10, 1f, 1.7f, 24.9f, 1, 1, 1, 1, burst: 5, ticks: 22);
        beam.Beam = true;
        beam.BeamWidth = 1f;
        C(Near(WeaponRateMath.BeamShare(beam), 0.5f), "a beam two cells wide strikes the target on half its ticks");
        beam.BeamHitsNeighbours = true;
        C(Near(WeaponRateMath.BeamShare(beam), 1f), "and on all of them when it also strikes the cells beside its path");
        var matchlock = WeaponAnchors.Gun(20, 2f, 3f, 20f, .8f, .7f, .5f, .3f);
        matchlock.DamageFallsWithDistance = true;
        matchlock.ProjectilesPerShot = 3;
        float near = WeaponRateMath.RangedDps(matchlock, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 5f);
        float far12 = WeaponRateMath.RangedDps(matchlock, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f);
        C(near > far12 * 1.5f, "a matchlock hits far harder point blank (VFE Medieval: twice at the muzzle, nothing at range)");
        var single = WeaponAnchors.Gun(20, 2f, 3f, 20f, .8f, .7f, .5f, .3f);
        single.DamageFallsWithDistance = true;
        C(Near(near, 3f * WeaponRateMath.RangedDps(single, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 5f), 0.01f), "three balls a shot, each aimed");
        var line = WeaponAnchors.Gun(20, 1f, 2f, 30f, .5f, .5f, .5f, .5f);
        float aimed = WeaponRateMath.RangedDps(line, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f);
        line.Line = true;
        C(WeaponRateMath.RangedDps(line, WeaponRateMath.Reference, WeaponRateMath.Unarmoured, 12f) > aimed &&
          WeaponRateMath.CrowdFactor(line, WeaponRateMath.Reference, 12f) == 1.5f, "a stream or a gauss slug strikes the near-missed and the one behind");
        C(WeaponRateMath.PassiveFactor(new[] { new WeaponPassive("DeflectChance", 0.4f) }, true) > 1.15f &&
          WeaponRateMath.Specials(null, sword, new[] { new WeaponPassive("DeflectChance", 0.4f) }, null).Any(x => x.Key == "passive:DeflectChance" && x.Counted),
            "a deflector (A RimWorld of Magic) counts as defence");

        // ---------- Tags ----------
        var ids = new HashSet<string>();
        bool unique = true;
        foreach (var d in WeaponTagMath.WeaponTags) unique &= ids.Add(d.Id);
        C(unique && WeaponTagMath.WeaponTags.Length >= 40, $"the vocabulary has {WeaponTagMath.WeaponTags.Length} tags, each id once");
        C(WeaponTagMath.WeaponTags.All(d => !string.IsNullOrWhiteSpace(d.Rule) && (d.Id == "grants" || d.Label.Split(' ').Length <= 3)),
            "every tag has a rule, and a label of one to three words");
        C(Enum.GetValues(typeof(TagCategory)).Cast<TagCategory>().All(c => WeaponTagMath.Categories.Count(s => s.Category == c) == 1 &&
                                                                            WeaponTagMath.WeaponTags.Any(d => d.Category == c)),
            "six categories, each with one style and its own tags");
        C(WeaponTagMath.Categories.Select(s => s.Symbol).Distinct().Count() == 6 && WeaponTagMath.Categories.Select(s => s.Colour).Distinct().Count() == 6,
            "each category its own colour and symbol, so colour is never the only cue");
        foreach (var st in WeaponTagMath.Categories)
            foreach (var (gname, ground) in SkinMath.Grounds)
                C(SkinMath.Contrast(st.Colour, ground) >= SkinMath.TextContrast, $"{st.Label} ({st.Hex}) reads at 4.5:1 on {gname}");
        C(WeaponTagMath.Style(TagCategory.Strength).Colour == SkinMath.Done && WeaponTagMath.Style(TagCategory.Weakness).Colour == SkinMath.Danger &&
          WeaponTagMath.Style(TagCategory.Risk).Colour == SkinMath.Foe && WeaponTagMath.Style(TagCategory.Role).Colour == SkinMath.Ally,
            "green, red, orange and blue are Rimfolk's own tokens");
        List<ThingTag> Tags(string name, float mass, string defName = "")
        {
            var rr = r[name];
            var facts = new WeaponTagFacts { DefName = defName, Mass = mass, RatingAtSkill4 = rr.Overall, RatingAtSkill16 = rr.Overall };
            if (name == "minigun") facts.MoveSpeedOffset = -0.25f;
            if (WeaponAnchors.Ranged.TryGetValue(name, out var g)) return WeaponTagMath.For(rr, facts, g);
            if (name == "frag grenades") return WeaponTagMath.For(rr, new WeaponTagFacts { Mass = 1f, Thrown = true }, WeaponAnchors.FragGrenade());
            if (name == "EMP grenades") return WeaponTagMath.For(rr, new WeaponTagFacts { Mass = 1f, Thrown = true }, WeaponAnchors.EmpGrenade());
            return WeaponTagMath.For(rr, facts, null, WeaponAnchors.Melee[name].w);
        }
        bool HasTag(List<ThingTag> ts, string id) => ts.Any(t => t.Id == id);
        var sn = Tags("sniper rifle", 4f, "Gun_SniperRifle");
        C(HasTag(sn, "role-sniper") && HasTag(sn, "long-reach") && HasTag(sn, "slow-aim") && sn[0].Id == "role-sniper",
            "a sniper rifle: Sniper first, Long reach, Slow to aim");
        var mg = Tags("minigun", 10f, "Gun_Minigun");
        C(HasTag(mg, "wild-spray") && HasTag(mg, "heavy-carry") && HasTag(mg, "rapid-fire"), "a minigun: Rapid fire, Wild spray, Heavy to carry");
        var fg = Tags("frag grenades", 1f);
        C(HasTag(fg, "hits-friends") && HasTag(fg, "role-grenadier") && HasTag(fg, "short-range"), "frag grenades: Grenadier, Hits friends, Short range");
        var eg = Tags("EMP grenades", 1f);
        C(HasTag(eg, "emp") && HasTag(eg, "mech-killer") && HasTag(eg, "shield-breaker"), "EMP grenades: EMP, Mech killer, Shield-breaker");
        var kn = Tags("knife", 0.5f, "MeleeWeapon_Knife");
        C(HasTag(kn, "role-sidearm") && HasTag(kn, "role-offhand") && HasTag(kn, "bleeds") && !HasTag(kn, "two-handed"),
            "a knife: Sidearm and Offhand by Simple Sidearms' and Dual Wield's defaults, Bleeds");
        var wh = Tags("warhammer", 5f, "MeleeWeapon_Warhammer");
        C(HasTag(wh, "two-handed") && HasTag(wh, "breaks-bones") && !HasTag(wh, "role-sidearm"), "a warhammer: two-handed by Dual Wield's default, Breaks bones, no sidearm");
        var bow = Tags("short bow", 1f, "Bow_Short");
        C(!HasTag(bow, "role-offhand") && HasTag(bow, "two-handed"), "a bow is never an off-hand weapon (Dual Wield's default)");
        foreach (var name in new[] { "sniper rifle", "minigun", "assault rifle", "charge lance", "knife", "monosword" })
        {
            var ts = Tags(name, name == "knife" ? 0.5f : name == "minigun" ? 10f : 3f);
            C(ts.Count <= 7 && ts.Count >= 2, $"{name}: {ts.Count} tags, a few and no more than seven");
            C(ts.Zip(ts.Skip(1), (a, b) => a.Importance >= b.Importance).All(x => x), $"{name}: tags in order of importance");
            C(ts.All(t => !string.IsNullOrWhiteSpace(t.Why)), $"{name}: every tag says why in its own numbers");
        }
        var many = new List<ThingTag>();
        for (int i = 0; i < 12; i++) many.Add(new ThingTag { Id = "x" + i, Category = TagCategory.Special, Importance = 90 - i });
        many.Add(new ThingTag { Id = "s", Category = TagCategory.Strength, Importance = 10 });
        many.Add(new ThingTag { Id = "w", Category = TagCategory.Weakness, Importance = 5 });
        var picked = WeaponTagMath.Pick(many, 7);
        C(picked.Count == 7 && picked.Any(t => t.Id == "s") && picked.Any(t => t.Id == "w"), "a crowded list still keeps its first strength and weakness");
        return n;
    }

    /// <summary>The anchors as a table (Tools/WeaponReview writes them into the README).</summary>
    public static string Table()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (name, r) in WeaponAnchors.All())
            sb.AppendLine($"{name}\t{r.Kind}\t{r.Role}\t{r.Overall:0.0}\t{r.FightValue:0.00}\t" + string.Join(" ", r.Sub.Select(kv => kv.Key + "=" + kv.Value.ToString("0.0"))));
        return sb.ToString();
    }
}
