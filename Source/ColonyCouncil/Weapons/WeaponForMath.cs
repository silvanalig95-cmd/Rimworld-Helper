using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>A trait's part in a fight: what it adds to the skill before the game's curve, and the factors it sets.</summary>
    public sealed class FighterTrait
    {
        public string Label = "";
        /// <summary>Added to Shooting and to Melee before the game's curves (careful shooter +5 shooting, brawler +4 melee hit).</summary>
        public float Shooting, MeleeHit;
        public float AimFactor = 1f, RangedCooldownFactor = 1f, MeleeCooldownFactor = 1f, MeleeDamageFactor = 1f;

        public bool Ranged => Shooting != 0f || AimFactor != 1f || RangedCooldownFactor != 1f;
        public bool Melee => MeleeHit != 0f || MeleeCooldownFactor != 1f || MeleeDamageFactor != 1f;
    }

    /// <summary>
    /// A colonist as the weapon rating sees them: their skill levels, their real stats as the game has them (accuracy, melee
    /// hit, aim, cooldowns, damage: their traits, body and gear in them), what the weapon they hold now adds to those (taken
    /// back out, so another weapon is judged on its own), their traits that work on a fight, and their own natural attacks.
    /// </summary>
    public sealed class FighterFacts
    {
        public string Name = "";
        public bool CanFight = true;
        public int ShootingLevel = 8, MeleeLevel = 8;
        /// <summary>ShootingAccuracyPawn and MeleeHitChance as the game has them now (below zero: not known, the skill alone).</summary>
        public float ShootingAccuracy = -1f, MeleeHitChance = -1f;
        public float AimFactor = 1f, RangedCooldownFactor = 1f, MeleeCooldownFactor = 1f, MeleeDamageFactor = 1f;
        /// <summary>What the weapon in their hands adds to each (its equippedStatOffsets), taken out when judging another.</summary>
        public float HeldShooting, HeldMeleeHit, HeldAim, HeldRangedCooldown, HeldMeleeCooldown;
        public List<FighterTrait> Traits = new List<FighterTrait>();
        /// <summary>Their body's own melee attacks; null for a human's fists.</summary>
        public List<WeaponMeleeAttack> Natural;
        public bool Brawler;
    }

    /// <summary>A weapon rated for one colonist: their rating, the reference's, and why it differs, step by step.</summary>
    public sealed class WeaponForColonist
    {
        public float Rating, Reference;
        public WeaponRating Full;
        /// <summary>Each step from the reference colonist to this one: what changed and how much it moved the rating.</summary>
        public List<(string why, float delta)> Why = new List<(string, float)>();
    }

    /// <summary>A weapon a colonist carries, rated for them, for the pairs (Simple Sidearms' sidearms, Dual Wield's off hand).</summary>
    public sealed class CarriedWeapon
    {
        public string Key = "", Label = "";
        public bool Ranged, Primary, OneUse;
        public float Mass, Rating;
    }

    /// <summary>
    /// The weapon rating for a colonist and the pairs they carry, without game types (Tools/Tests try it): the colonist's
    /// rating from their real stats with the steps that explain it (their skill, each trait, their body and gear); the
    /// sensible pair a colonist carries under Simple Sidearms' default limits; a weapon in the off hand under Dual Wield's
    /// default penalties and the pair together; RunAndGun's shooting on the move; the damage by distance the card draws;
    /// and the colour a rating is shown in.
    /// </summary>
    public static class WeaponForMath
    {
        // ---------------------------------------------------------------- a colonist

        /// <summary>The colonist as a fighter: their real stats as skill levels and factors, the weapon now in hand taken out.</summary>
        public static WeaponFighter Fighter(FighterFacts c)
        {
            var f = WeaponRateMath.Reference;
            f.ShootingSkill = c.ShootingAccuracy >= 0f ? WeaponRateMath.ShootingLevelFor(c.ShootingAccuracy) - c.HeldShooting : SkillWithTraits(c, true);
            f.MeleeSkill = c.MeleeHitChance >= 0f ? WeaponRateMath.MeleeLevelFor(c.MeleeHitChance) - c.HeldMeleeHit : SkillWithTraits(c, false);
            f.AimFactor = Math.Max(0.1f, c.AimFactor - c.HeldAim);
            f.RangedCooldownFactor = Math.Max(0.1f, c.RangedCooldownFactor - c.HeldRangedCooldown);
            f.MeleeCooldownFactor = Math.Max(0.1f, c.MeleeCooldownFactor - c.HeldMeleeCooldown);
            f.MeleeDamageFactor = Math.Max(0.1f, c.MeleeDamageFactor);
            if (c.Natural != null && c.Natural.Count > 0) f.Natural = new List<WeaponMeleeAttack>(c.Natural);
            return f;
        }

        static float SkillWithTraits(FighterFacts c, bool shooting)
        {
            float s = shooting ? c.ShootingLevel : c.MeleeLevel;
            foreach (var t in c.Traits) s += shooting ? t.Shooting : t.MeleeHit;
            return s;
        }

        /// <summary>A read weapon rated for a fighter, in its own role (the reference's), with its passives and abilities.</summary>
        public static WeaponRating RateFor(WeaponReport r, WeaponFighter f, WeaponRangedInput ranged = null)
        {
            if (r.Rating.Kind == "ranged")
                return WeaponRateMath.RateRanged(ranged ?? r.Ranged, f, r.Passives, r.Abilities, r.Rating.Role, r.Mass);
            return WeaponRateMath.RateMelee(r.Melee, r.Mass, f, r.Passives, r.Abilities, r.Rating.Role);
        }

        /// <summary>
        /// A weapon's rating for a colonist, and why it differs from the reference colonist's (Shooting or Melee 8, no
        /// traits): first their skill, then each trait that works on this kind of fight, then the rest of what the game has
        /// for them (sight and hands, bionics, other gear). The steps add up to the difference.
        /// </summary>
        public static WeaponForColonist For(WeaponReport r, FighterFacts c)
        {
            var o = new WeaponForColonist { Reference = r.Rating.Overall };
            bool ranged = r.Rating.Kind == "ranged";
            float prev = r.Rating.Overall;
            var f = WeaponRateMath.Reference;
            if (ranged) f.ShootingSkill = c.ShootingLevel;
            else f.MeleeSkill = c.MeleeLevel;
            float now = RateFor(r, f).Overall;
            o.Why.Add(((ranged ? "Shooting " : "Melee ") + (ranged ? c.ShootingLevel : c.MeleeLevel).ToString(CultureInfo.InvariantCulture), now - prev));
            prev = now;
            foreach (var t in c.Traits)
            {
                if (ranged ? !t.Ranged : !t.Melee) continue;
                f.ShootingSkill += t.Shooting;
                f.MeleeSkill += t.MeleeHit;
                f.AimFactor *= t.AimFactor;
                f.RangedCooldownFactor *= t.RangedCooldownFactor;
                f.MeleeCooldownFactor *= t.MeleeCooldownFactor;
                f.MeleeDamageFactor *= t.MeleeDamageFactor;
                now = RateFor(r, f).Overall;
                o.Why.Add((t.Label, now - prev));
                prev = now;
            }
            o.Full = RateFor(r, Fighter(c));
            o.Rating = o.Full.Overall;
            o.Why.Add(("body and gear", o.Rating - prev));
            return o;
        }

        /// <summary>A step's change as the card shows it: "+0.4", "-1.2", "0".</summary>
        public static string Delta(float d)
        {
            float v = (float)Math.Round(d, 1);
            if (Math.Abs(v) < 0.05f) return "0";
            return (v > 0f ? "+" : "-") + Math.Abs(v).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>The steps worth naming: those that moved the rating by a tenth or more.</summary>
        public static List<(string why, float delta)> Telling(WeaponForColonist c) => c.Why.Where(s => Math.Abs(s.delta) >= 0.05f).ToList();

        // ---------------------------------------------------------------- Simple Sidearms

        /// <summary>
        /// The pair a colonist carries, as Simple Sidearms' default preset allows it: the best gun or bow and the best melee
        /// weapon among what they hold and carry, a sidearm only within its weight (melee 1.9 kg, ranged 2.7 kg; the weapon in
        /// hand has no limit), never a one-use launcher as the gun. Either can be missing (then fists, or no gun).
        /// </summary>
        public static (CarriedWeapon ranged, CarriedWeapon melee) BestPair(IEnumerable<CarriedWeapon> carried)
        {
            CarriedWeapon bestR = null, bestM = null;
            foreach (var w in carried ?? Enumerable.Empty<CarriedWeapon>())
            {
                if (!Fits(w)) continue;
                if (w.Ranged)
                {
                    if (w.OneUse) continue;
                    if (bestR == null || w.Rating > bestR.Rating || (w.Rating == bestR.Rating && w.Primary)) bestR = w;
                }
                else if (bestM == null || w.Rating > bestM.Rating || (w.Rating == bestM.Rating && w.Primary)) bestM = w;
            }
            return (bestR, bestM);
        }

        /// <summary>Within Simple Sidearms' default weight for a sidearm of its kind (the weapon in hand always fits).</summary>
        public static bool Fits(CarriedWeapon w) =>
            w.Primary || w.Mass <= (w.Ranged ? WeaponTagMath.SidearmRangedMass : WeaponTagMath.SidearmMeleeMass) + 0.001f;

        // ---------------------------------------------------------------- Dual Wield

        /// <summary>Dual Wield's penalties (its defaults, read from its settings where it is loaded): percent, static and per skill level below 20.</summary>
        public sealed class DualWieldRules
        {
            public float CooldownOffHand = 20f, CooldownMainHand = 10f, AccuracyOffHand = 10f, AccuracyMainHand = 10f, CooldownPerLevel = 5f, AccuracyPerLevel = 0.5f;
        }

        public static readonly DualWieldRules DualWieldDefaults = new DualWieldRules();

        /// <summary>Dual Wield's cooldown factor on a hand's weapon while both hands hold one (VerbProperties.AdjustedCooldown).</summary>
        public static float DualCooldownFactor(DualWieldRules d, bool offHand, int skill) =>
            1f + (offHand ? d.CooldownOffHand : d.CooldownMainHand) / 100f + d.CooldownPerLevel / 100f * (20 - Math.Max(0, Math.Min(20, skill)));

        /// <summary>Dual Wield's accuracy factor on a hand's gun while both hands hold one (VerbProperties.AdjustedAccuracy).</summary>
        public static float DualAccuracyFactor(DualWieldRules d, bool offHand, int skill) =>
            Math.Max(0.01f, 1f - (offHand ? d.AccuracyOffHand : d.AccuracyMainHand) / 100f - d.AccuracyPerLevel / 100f * (20 - Math.Max(0, Math.Min(20, skill))));

        /// <summary>A gun's numbers with a factor on its accuracy at every distance.</summary>
        public static WeaponRangedInput WithAccuracy(WeaponRangedInput w, float factor)
        {
            var c = w.Clone();
            c.AccTouch = Math.Min(1f, w.AccTouch * factor);
            c.AccShort = Math.Min(1f, w.AccShort * factor);
            c.AccMedium = Math.Min(1f, w.AccMedium * factor);
            c.AccLong = Math.Min(1f, w.AccLong * factor);
            return c;
        }

        /// <summary>
        /// A weapon rated in one hand of a pair, for a fighter, with Dual Wield's penalties: a longer cooldown (guns and blows)
        /// and, for a gun, a lower accuracy, both worse the lower the colonist's skill.
        /// </summary>
        public static WeaponRating RateInHand(WeaponReport r, WeaponFighter f, DualWieldRules d, bool offHand, int skill)
        {
            var g = f.Copy();
            float cd = DualCooldownFactor(d, offHand, skill);
            if (r.Rating.Kind == "ranged")
            {
                g.RangedCooldownFactor *= cd;
                return RateFor(r, g, WithAccuracy(r.Ranged, DualAccuracyFactor(d, offHand, skill)));
            }
            g.MeleeCooldownFactor *= cd;
            return RateFor(r, g);
        }

        /// <summary>
        /// Both hands together, when both are guns or both melee weapons (Dual Wield fires and swings them at once): what
        /// each gets done added, on the rating's scale. Null when one is a gun and the other a blade: each fights its own fight.
        /// </summary>
        public static float? PairTogether(WeaponRating main, WeaponRating off)
        {
            if (main == null || off == null || main.Kind != off.Kind) return null;
            return WeaponRateMath.Scale(main.FightValue + off.FightValue, main.Kind == "melee");
        }

        // ---------------------------------------------------------------- RunAndGun

        /// <summary>RunAndGun's accuracy while moving: the weapon's accuracy times (100 - its penalty) percent.</summary>
        public static float MovingAccuracyFactor(int penaltyPercent) => Math.Max(0.01f, (100 - Math.Max(0, Math.Min(100, penaltyPercent))) / 100f);

        // ---------------------------------------------------------------- the card's chart

        /// <summary>The distances the card's chart is drawn at, up to the weapon's range.</summary>
        public static List<float> ChartDistances(WeaponRangedInput w)
        {
            var list = new List<float>();
            float max = Math.Min(Math.Max(w.Range, 3f), 50f);
            float[] steps = { 2f, 3f, 5f, 8f, 12f, 16f, 20f, 25f, 30f, 35f, 40f, 45f, 50f };
            foreach (var d in steps)
                if (d <= max + 0.01f && d >= Math.Max(w.MinRange, 1f)) list.Add(d);
            if (list.Count == 0 || list[list.Count - 1] < max - 0.5f) list.Add((float)Math.Floor(max));
            return list;
        }

        /// <summary>The chart's enemies: no armour, flak, marine armour, mechanoids (the light and the heavy one, averaged).</summary>
        public static readonly string[] ChartEnemies = { "unarmoured", "armoured", "heavy", "mechanoids" };

        /// <summary>Damage per second by distance against each of the chart's enemies, for a fighter.</summary>
        public static float[][] RangedChart(WeaponRangedInput w, WeaponFighter f, List<float> distances)
        {
            var t = WeaponRateMath.Targets;
            var rows = new float[4][];
            for (int k = 0; k < 4; k++) rows[k] = new float[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                float d = distances[i];
                rows[0][i] = WeaponRateMath.RangedDps(w, f, t[0], d);
                rows[1][i] = WeaponRateMath.RangedDps(w, f, t[1], d);
                rows[2][i] = WeaponRateMath.RangedDps(w, f, t[2], d);
                rows[3][i] = 0.5f * (WeaponRateMath.RangedDps(w, f, t[3], d) + WeaponRateMath.RangedDps(w, f, t[4], d));
            }
            return rows;
        }

        /// <summary>A melee weapon's damage per second against each of the chart's enemies, for a fighter.</summary>
        public static float[] MeleeChart(WeaponMeleeInput w, WeaponFighter f)
        {
            var t = WeaponRateMath.Targets;
            return new[]
            {
                WeaponRateMath.MeleeDps(w, f, t[0]), WeaponRateMath.MeleeDps(w, f, t[1]), WeaponRateMath.MeleeDps(w, f, t[2]),
                0.5f * (WeaponRateMath.MeleeDps(w, f, t[3]) + WeaponRateMath.MeleeDps(w, f, t[4]))
            };
        }

        /// <summary>A round top for a chart's scale: 1, 2 or 5 times a power of ten, at or above the highest value.</summary>
        public static float NiceTop(float max)
        {
            if (max <= 0f) return 1f;
            double p = Math.Pow(10, Math.Floor(Math.Log10(max)));
            foreach (var m in new[] { 1.0, 2.0, 2.5, 5.0, 10.0 })
                if (m * p >= max - 1e-6) return (float)(m * p);
            return (float)(10 * p);
        }

        // ---------------------------------------------------------------- how a rating is shown

        /// <summary>
        /// The colour a rating is shown in, by its band (the words of <see cref="WeaponWords.Quality"/>), from Rimfolk's own
        /// palette: poor red, weak orange, fair yellow, solid green, strong blue, excellent and superb violet.
        /// </summary>
        public static uint RatingColour(float o) =>
            o >= 7.5f ? SkinMath.World : o >= 6.5f ? SkinMath.Ally : o >= 5.5f ? SkinMath.Done : o >= 4.5f ? SkinMath.Soon : o >= 3.5f ? SkinMath.Foe : SkinMath.Danger;

        /// <summary>A rating as the badge shows it: one decimal.</summary>
        public static string RatingText(float o) => o.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>A tag in a line of text: its symbol, label and why ("▲ Long reach: reaches 44.9 cells ...").</summary>
        public static string TagLine(ThingTag t) => WeaponTagMath.Style(t.Category).Symbol + " " + t.Label + ": " + t.Why;

        /// <summary>The sub-ratings worth a row, in order: distances (guns), enemies, a crowd (guns).</summary>
        public static IEnumerable<string> SubKeys(WeaponRating r)
        {
            if (r.Kind == "ranged")
                foreach (var k in WeaponRateMath.DistanceKeys)
                    yield return k;
            foreach (var k in ChartEnemies) yield return k;
            if (r.Kind == "ranged") yield return "crowd";
        }
    }
}
