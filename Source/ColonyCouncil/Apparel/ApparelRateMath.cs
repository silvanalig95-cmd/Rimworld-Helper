using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// A zone of the body a hit can land on: the body part groups its parts belong to (apparel covering any of them covers
    /// the zone, as Apparel.CoversBodyPart reads it) and the share of hits that land there: a shot (any part, by its own
    /// coverage), a melee blow or a blast (an outside part), and a blunt blow (an outside part, never an eye).
    /// </summary>
    public sealed class ApparelZone
    {
        public string[] Groups;
        public float Ranged, Melee, MeleeBlunt;

        public ApparelZone(float ranged, float melee, float blunt, params string[] groups)
        {
            Ranged = ranged;
            Melee = melee;
            MeleeBlunt = blunt;
            Groups = groups;
        }

        /// <summary>A zone of the head (everything above the neck): a hit there counts double (<see cref="ApparelRateMath.HeadHarm"/>).</summary>
        public bool Head => Array.IndexOf(Groups, "FullHead") >= 0;

        public bool CoveredBy(IList<string> groups)
        {
            if (groups == null) return true;
            foreach (var g in groups) if (Array.IndexOf(Groups, g) >= 0) return true;
            return false;
        }
    }

    /// <summary>One attack of the reference enemies: who makes it, its damage, armour piercing and kind, how it lands, and its share of its family.</summary>
    public sealed class ApparelThreat
    {
        /// <summary>guns (raiders' guns and bows), melee (raiders' blades and fists), animals, mechanoids, fire.</summary>
        public string Family;
        public string Label;
        public float Damage, Pen, Weight;
        public WeaponArmour Armour = WeaponArmour.Sharp;
        /// <summary>A shot: it can land on any part. A blow or a blast lands on an outside part.</summary>
        public bool Ranged;
        /// <summary>A shot or a blast: a shield belt stops it (CompShield takes ranged and explosive damage).</summary>
        public bool Projectile;
    }

    /// <summary>One layer of armour: the groups it covers (null: the whole body, the wearer's own armour) and its ratings after stuff and quality.</summary>
    public sealed class ApparelLayer
    {
        public string Label = "";
        public string[] Groups;
        public float Sharp, Blunt, Heat;
    }

    /// <summary>
    /// An energy shield the piece carries: how much damage it absorbs from full, how fast it recharges, how long it is down
    /// once broken, and whether it stops its wearer shooting (the game's shield belt) or also covers those beside them (a field).
    /// </summary>
    public sealed class ApparelShield
    {
        public string Label = "shield";
        public float Capacity;
        public float Recharge;
        public float ResetSeconds = 3200f / 60f;
        public bool BlocksShooting;
        public bool Field;
    }

    /// <summary>
    /// Something the piece lets its wearer do (a verb, an ability, a comp): what kind, how many uses, and the numbers its
    /// kind is valued by. Kinds: jump, smoke, lowshield, firefoam, grenade (a shot), lance, targeter, summon, gas, oxygen,
    /// resurrect, stabilize, other. A kind the rating can't value is named and not counted.
    /// </summary>
    public sealed class ApparelPower
    {
        public string Kind = "other";
        public string Label = "";
        /// <summary>Uses before it must be reloaded (0: none, a cooldown only).</summary>
        public int Charges;
        /// <summary>Used up for good when empty (a psychic lance, an orbital targeter).</summary>
        public bool RunsOut;
        public float CooldownSeconds;
        public float Range, Radius, Seconds;
        /// <summary>For a grenade or a launched shot: the shot as the weapon rating reads it.</summary>
        public WeaponRangedInput Shot;
        /// <summary>For a psychic lance: the target is put into a berserk rage (else shocked).</summary>
        public bool Berserk;
        public bool Counted = true;
        /// <summary>What its charges are refilled with (chemfuel, an oxygen canister), empty when nothing.</summary>
        public string Ammo = "";
        public string Note = "";
    }

    /// <summary>
    /// Everything the rating reads about one piece, in the game's own terms: its stat bases and the stuff it is made of (or
    /// none), what it changes on its wearer, its coverage and layers, and what its comps, verbs and hediffs do, read into
    /// shields, powers, extra armour and capacities (<see cref="ApparelRateMath.ReadComp"/>). Tools/ApparelReview fills it
    /// from the extracted defs; the game side will fill it from the live def and the piece in hand.
    /// </summary>
    public sealed class ApparelFacts
    {
        public string DefName = "", Label = "", Mod = "", ModName = "", TechLevel = "";
        public Dictionary<string, float> StatBases = new Dictionary<string, float>();
        /// <summary>What it changes on its wearer: equippedStatOffsets, and the stat offsets of the hediffs it gives while worn.</summary>
        public Dictionary<string, float> Offsets = new Dictionary<string, float>();
        /// <summary>Factors on its wearer's stats (VEF's ApparelExtension, the hediffs' statFactors).</summary>
        public Dictionary<string, float> Factors = new Dictionary<string, float>();
        /// <summary>The stuff it is made of: its label and stuff powers (StuffPower_Armor_Sharp, StuffPower_Insulation_Cold...); empty when not stuffed.</summary>
        public string StuffDef = "", StuffLabel = "";
        public Dictionary<string, float> StuffPowers = new Dictionary<string, float>();
        public float StuffFlammability = 1f;
        /// <summary>The armour and insulation factor of its quality (normal 1).</summary>
        public float ArmourQuality = 1f, InsulationQuality = 1f;
        public List<string> StuffCategories = new List<string>();
        public List<string> Layers = new List<string>();
        public List<string> Groups = new List<string>();
        public List<string> Tags = new List<string>();
        public List<string> ThingCategories = new List<string>();
        public bool CountsAsClothes = true, CareIfDamaged = true, SlaveApparel, MechanitorApparel, ImmuneToToxGas, BlocksVision, IgnoredByNonViolent;
        public float WearPerDay = 0.4f;
        /// <summary>Male, Female or empty for anyone.</summary>
        public string Gender = "";
        /// <summary>Adult, Child or both, as the game's developmentalStageFilter writes it.</summary>
        public string Ages = "Adult";
        public bool Biocodable;
        public List<ApparelShield> Shields = new List<ApparelShield>();
        /// <summary>An off-hand shield (VEF's): its armour stops hits on the groups it covers, before anything else, while its wearer holds a light one-handed weapon.</summary>
        public string[] OffHandCovers;
        public List<ApparelPower> Powers = new List<ApparelPower>();
        /// <summary>Capacities its hediffs change (capacity, offset, factor, max).</summary>
        public List<(string capacity, float offset, float factor, float max)> Capacities = new List<(string, float, float, float)>();
        public float PainOffset;
        public float BleedFactor = 1f;
        /// <summary>A RimWorld of Magic enchantment: maxMP, mpRegenRate, coolDown, mpCost, xpGain, arcaneRes, arcaneDmg, healthRegenRate.</summary>
        public Dictionary<string, float> Enchantment = new Dictionary<string, float>();
        public bool Camouflage;
        public bool PreventsBleeding, SkillGainZero;
        public string WorkDisables = "";
        /// <summary>The royal titles whose apparel rule it meets (label, seniority) and the faith roles and memes that ask for it.</summary>
        public List<(string label, float seniority)> Titles = new List<(string, float)>();
        public List<string> Faiths = new List<string>();
        public bool HardToGet;
        public string Research = "", ResearchTech = "";
        public float MarketValue;
        /// <summary>The base value of a stat on a colonist (the StatDef's default or a human's own), for turning an offset into a share.</summary>
        public Dictionary<string, float> StatDefaults = new Dictionary<string, float>();
        /// <summary>What the rating can name but not read (a mod's own code, a rolled trait).</summary>
        public List<string> Unsure = new List<string>();
        /// <summary>Named effects with no number to count (a heraldic tabard, a rum suit's rum).</summary>
        public List<string> Notes = new List<string>();
    }

    /// <summary>A named effect: whether the rating counts it, a short note, and its sign for the wearer.</summary>
    public sealed class ApparelSpecial
    {
        public string Key;
        public string Label;
        public bool Counted;
        public int Sign = 1;
    }

    /// <summary>What the rating says about one piece. All ratings are 1.0 to 10.0 with one decimal.</summary>
    public sealed class ApparelRating
    {
        public string Role;
        public string Slot;
        /// <summary>
        /// Who it is rated for: "a shooter" or "a melee fighter" when it clearly suits one kind (<see cref="ApparelRateMath.SuitsGap"/>:
        /// it blocks shooting, is an off-hand shield, or does a tenth more for one than for the other, the walk aside), else "anyone".
        /// </summary>
        public string For = "anyone";
        public float Overall;
        /// <summary>The overall for each wearer: a shooter (Shooting 8, an assault rifle) and a melee fighter (Melee 8, a steel longsword), on the same role and scale.</summary>
        public float OverallShooter, OverallMelee;
        /// <summary>How much more the piece does for a melee fighter than for a shooter, the walk aside: ln(melee / shooter), toughness times output.</summary>
        public float Lean;
        /// <summary>The worth the overall is made from: what the piece adds to its wearer, as a share of a colonist, by its role's weights.</summary>
        public float Worth;
        public Dictionary<string, float> Sub = new Dictionary<string, float>();
        public Dictionary<string, float> Situations = new Dictionary<string, float>();
        /// <summary>Each area's worth before the role's weights (fight, cold, heat, work, social, mind, psychic, mech, magic, toxic, vacuum, carry, hunting).</summary>
        public Dictionary<string, float> Areas = new Dictionary<string, float>();
        /// <summary>How much longer the wearer lasts against each enemy family (toughness, 1 = no protection).</summary>
        public Dictionary<string, float> Toughness = new Dictionary<string, float>();
        public float FightShooter = 1f, FightMelee = 1f, Fight = 1f;
        /// <summary>The protection alone: how much longer a shooter wearing it lasts, over the whole mix (no aim, feet or powers).</summary>
        public float Protection = 1f;
        public float Sharp, Blunt, Heat, Cold, HeatInsulation;
        /// <summary>The share of hits (shots, by the body's coverage, a head hit counting double) that land on what it covers.</summary>
        public float Coverage;
        public List<ApparelSpecial> Specials = new List<ApparelSpecial>();
        public List<string> BestFor = new List<string>();
        public List<string> WeakWhen = new List<string>();
        public string Skill = "";
    }

    /// <summary>
    /// The apparel rating: what a piece of armour or clothing does for whoever wears it, by the game's own rules, as one
    /// number from 1.0 to 10.0, sub-ratings and situations, without game types (Tools/ApparelReview rates every piece of the
    /// player's mods with it, Tools/Tests checks it against the base game's pieces).
    ///
    /// Protection is the game's armour roll (WeaponRateMath.ArmourFactor; 1.6 decompiled, ArmorUtility) on every zone of the
    /// human body the piece covers, against the reference enemies' attacks: shots land anywhere by the body's coverage,
    /// blows and blasts on the outside, a sharp hit halved by armour goes on as blunt (the next layer meets it as blunt).
    /// What it does in a fight is the wearer's toughness (how many more hits they take before going down) times their
    /// output (aim, melee hit, the weapon's cycle, what its powers add) times their feet (a slower walk), for two wearers:
    /// a shooter and a melee fighter. A piece that clearly suits one of them is rated for that one; else the two are mixed,
    /// three shooters to one melee fighter. Everything else
    /// (warmth, work, talks, the mind, psycasts, mechanitors, magic, toxins, vacuum, carrying, hunting) is a share of what a
    /// colonist gets done. The role weighs the areas, and the scale is the weapon rating's own Laplace curve in the log of
    /// that worth, with a middle and width per role.
    /// </summary>
    public static class ApparelRateMath
    {
        // ---------------------------------------------------------------- the body

        /// <summary>
        /// The human body's hit zones (Bodies_Humanlike), each with the share of shots, melee blows and blunt blows that land
        /// there. The game picks the part by its coverageAbs (its share of its parent less its children's), weighted by the
        /// part's hit chance for the damage: any part for a bullet or an arrow (no depth set), an outside part for a melee
        /// blow (Verb_MeleeAttackDamage) or a blast (DamageDef.explosionAffectOutsidePartsOnly), never an eye for a blunt blow
        /// (the eye's hitChanceFactors). Tools/apparel_extract.py works the same out from the def; the review checks they agree.
        /// </summary>
        public static readonly ApparelZone[] Human =
        {
            new ApparelZone(0.4266f, 0.24208f, 0.24541f, "Torso"),
            new ApparelZone(0.252f, 0.31632f, 0.32066f, "Legs"),
            new ApparelZone(0.15893f, 0.19684f, 0.19954f, "Arms"),
            new ApparelZone(0.0336f, 0.05423f, 0.05497f, "Shoulders"),
            new ApparelZone(0.028f, 0.04519f, 0.04581f, "Feet"),
            new ApparelZone(0.0174f, 0.02808f, 0.02847f, "FullHead", "HeadAttackTool", "UpperHead"),
            new ApparelZone(0.01656f, 0.02672f, 0.02709f, "Hands"),
            new ApparelZone(0.015f, 0.02421f, 0.02454f, "Neck"),
            new ApparelZone(0.0108f, 0f, 0f, "Eyes", "FullHead", "UpperHead"),
            new ApparelZone(0.00899f, 0.01451f, 0.01471f, "FullHead", "Mouth", "Teeth"),
            new ApparelZone(0.0084f, 0.01356f, 0f, "Eyes", "FullHead"),
            new ApparelZone(0.0084f, 0.01356f, 0.01374f, "FullHead", "UpperHead"),
            new ApparelZone(0.00601f, 0.00968f, 0.00982f, "FullHead"),
            new ApparelZone(0.00362f, 0.00585f, 0.00593f, "Hands", "LeftHand"),
            new ApparelZone(0.00362f, 0.00585f, 0.00593f, "Hands", "RightHand"),
            new ApparelZone(0.00103f, 0.00167f, 0.00169f, "Hands", "LeftHand", "MiddleFingers"),
            new ApparelZone(0.00103f, 0.00167f, 0.00169f, "Hands", "MiddleFingers", "RightHand"),
        };

        /// <summary>A hit on the head counts double: the brain (10 hit points) and the eyes are there, and one hit can kill or blind.</summary>
        public const float HeadHarm = 2f;

        /// <summary>The body part groups of the head: a piece covering only these is headgear.</summary>
        public static readonly string[] HeadGroups = { "FullHead", "UpperHead", "Eyes", "Teeth", "Mouth", "HeadAttackTool" };

        // ---------------------------------------------------------------- the reference enemies

        static ApparelThreat T(string family, string label, float dmg, float pen, WeaponArmour kind, bool ranged, float weight, bool projectile = false) =>
            new ApparelThreat { Family = family, Label = label, Damage = dmg, Pen = pen, Armour = kind, Ranged = ranged, Weight = weight, Projectile = projectile || ranged };

        const WeaponArmour S = WeaponArmour.Sharp, B = WeaponArmour.Blunt, H = WeaponArmour.Heat;

        /// <summary>A frag grenade's blast as it reaches someone inside it: its 50 damage and 0.1 piercing by the game's falloff over its 1.9 cells.</summary>
        public static readonly float FragShare = WeaponRateMath.FalloffShare(1.9f);

        /// <summary>The animals the reference mix holds: their weight among animals and their tools (power, cooldown, chance, kind).</summary>
        public static readonly (string label, float weight, (string tool, float power, float cd, float chance, WeaponArmour kind)[] tools)[] Animals =
        {
            ("timber wolf", 0.25f, new[] { ("claw", 10.9f, 2f, 0.5f, S), ("claw", 10.9f, 2f, 0.5f, S), ("bite", 12f, 2f, 0.9f, S), ("head", 6f, 2f, 0.2f, B) }),
            ("warg", 0.15f, new[] { ("claw", 13f, 1.5f, 1f, S), ("claw", 13f, 1.5f, 1f, S), ("razorfangs", 15f, 2f, 0.9f, S), ("head", 5f, 2f, 0.2f, B) }),
            ("grizzly bear", 0.15f, new[] { ("claw", 17f, 2f, 1f, S), ("claw", 17f, 2f, 1f, S), ("bite", 23.6f, 2.6f, 0.5f, S), ("head", 11f, 2f, 0.2f, B) }),
            ("cougar", 0.15f, new[] { ("claw", 11f, 1.5f, 1f, S), ("claw", 11f, 1.5f, 1f, S), ("bite", 16f, 2f, 0.7f, S), ("head", 9f, 2f, 0.2f, B) }),
            ("megaspider", 0.15f, new[] { ("head claw", 12f, 2.6f, 1f, S), ("head", 7f, 2f, 0.2f, B) }),
            ("thrumbo", 0.05f, new[] { ("horn", 23f, 2f, 1f, S), ("bite", 28f, 2.6f, 0.7f, S), ("foot", 19f, 2f, 1f, B), ("foot", 19f, 2f, 1f, B), ("head", 17f, 2f, 0.2f, B) }),
            ("muffalo", 0.10f, new[] { ("head", 13f, 2.6f, 1f, B), ("hoof", 10f, 2f, 1f, B), ("hoof", 10f, 2f, 1f, B), ("bite", 10f, 2f, 0.5f, S) }),
        };

        /// <summary>
        /// The reference enemies' attacks, by family, each with its share of that family's hits (numbers from the defs: the
        /// guns' projectiles, the melee weapons in steel as docs/weapons/weapons.json has them, the animals' and mechanoids'
        /// tools by the game's own choice of attack, WeaponRateMath.MeleeShares; Tools/ApparelReview checks them against the
        /// player's defs). Raiders' guns and bows: pistols, SMGs, a shotgun, rifles, an LMG, a sniper rifle, a charge rifle, a
        /// recurve bow and a frag grenade. Raiders' melee: fists, a knife, an ikwa, a spear, a longsword, a club and a mace.
        /// Animals: a timber wolf, a warg, a grizzly bear, a cougar, a megaspider, a thrumbo, a muffalo. Mechanoids: a lancer's
        /// charge lance, a scyther's blades, a centipede's heavy charge blaster and inferno cannon, a pikeman's needle gun, a
        /// militor's mini-shotgun, a termite's thump cannon. Fire: a molotov or incendiary burst, and burning.
        /// </summary>
        public static readonly List<ApparelThreat> Threats = BuildThreats();

        /// <summary>How the families count in the overall fight: as a colony meets them after its first year.</summary>
        public static readonly Dictionary<string, float> FamilyWeights = new Dictionary<string, float>
        {
            ["guns"] = 0.45f, ["melee"] = 0.15f, ["animals"] = 0.15f, ["mechanoids"] = 0.20f, ["fire"] = 0.05f,
        };

        public static readonly string[] Families = { "guns", "melee", "animals", "mechanoids", "fire" };

        static List<ApparelThreat> BuildThreats()
        {
            var l = new List<ApparelThreat>
            {
                T("guns", "autopistol", 10f, 0.15f, S, true, 0.10f),
                T("guns", "revolver", 12f, 0.18f, S, true, 0.05f),
                T("guns", "machine pistol", 6f, 0.09f, S, true, 0.07f),
                T("guns", "heavy SMG", 12f, 0.18f, S, true, 0.08f),
                T("guns", "pump shotgun", 18f, 0.14f, S, true, 0.08f),
                T("guns", "bolt-action rifle", 18f, 0.27f, S, true, 0.10f),
                T("guns", "assault rifle", 11f, 0.165f, S, true, 0.20f),
                T("guns", "LMG", 12f, 0.18f, S, true, 0.10f),
                T("guns", "sniper rifle", 25f, 0.375f, S, true, 0.04f),
                T("guns", "charge rifle", 16f, 0.35f, S, true, 0.06f),
                T("guns", "recurve bow", 14f, 0.21f, S, true, 0.08f),
                T("guns", "frag grenade blast", 50f * FragShare, 0.1f * FragShare, S, false, 0.04f, projectile: true),
                T("melee", "fists", 8.2f, 0.123f, B, false, 0.15f),
                T("melee", "knife", 12f, 0.18f, S, false, 0.15f),
                T("melee", "ikwa", 15f, 0.225f, S, false, 0.10f),
                T("melee", "spear", 23f, 0.5f, S, false, 0.15f),
                T("melee", "longsword", 23f, 0.345f, S, false, 0.20f),
                T("melee", "club", 14f, 0.21f, B, false, 0.10f),
                T("melee", "mace", 15.7f, 0.235f, B, false, 0.15f),
                T("mechanoids", "lancer's charge lance", 30f, 0.45f, S, true, 0.25f),
                T("mechanoids", "scyther's blades", 20f, 0.30f, S, false, 0.20f),
                T("mechanoids", "centipede's heavy charge blaster", 15f, 0.225f, S, true, 0.15f),
                T("mechanoids", "centipede's inferno cannon", 10f, 0f, H, false, 0.05f, projectile: true),
                T("mechanoids", "pikeman's needle gun", 15f, 0.35f, S, true, 0.15f),
                T("mechanoids", "militor's mini-shotgun", 10f, 0.12f, S, true, 0.15f),
                T("mechanoids", "termite's thump cannon", 9f, 0.135f, S, false, 0.05f, projectile: true),
                T("fire", "molotov or incendiary burst", 10f, 0f, H, false, 0.5f, projectile: true),
                T("fire", "burning", 5f, 0f, H, false, 0.5f),
            };
            foreach (var (label, weight, tools) in Animals)
            {
                var f = new WeaponFighter { Natural = new List<WeaponMeleeAttack>() };
                foreach (var (tool, power, cd, chance, kind) in tools)
                    f.Natural.Add(new WeaponMeleeAttack { Label = tool, Power = power, Cooldown = cd, ChanceFactor = chance, Armour = kind, Natural = true });
                foreach (var (a, share) in WeaponRateMath.MeleeShares(new WeaponMeleeInput(), f))
                {
                    if (share <= 0f) continue;
                    l.Add(T("animals", label + "'s " + a.Label, a.Power, a.Power * 0.015f, a.Armour, false, weight * share));
                }
            }
            return l;
        }

        // ---------------------------------------------------------------- the game's armour roll, layer by layer

        /// <summary>
        /// The damage left after a stack of armour layers, outside in, as a share of the hit (ArmorUtility.GetPostArmorDamage):
        /// each layer rolls its armour less the attack's piercing; deflected stops the hit, halved goes on at half, and a
        /// sharp hit halved goes on as blunt and meets the next layers' blunt armour.
        /// </summary>
        public static float AfterLayers(IList<ApparelLayer> layers, WeaponArmour kind, float pen)
        {
            if (kind == WeaponArmour.None || layers.Count == 0) return 1f;
            // (share of the hit left, its kind, the chance of this branch)
            var branches = new List<(float left, WeaponArmour kind, float p)> { (1f, kind, 1f) };
            foreach (var layer in layers)
            {
                var next = new List<(float, WeaponArmour, float)>();
                foreach (var (left, k, p) in branches)
                {
                    float armour = k == WeaponArmour.Sharp ? layer.Sharp : k == WeaponArmour.Blunt ? layer.Blunt : layer.Heat;
                    float e = Math.Max(Math.Min(armour, 2f) - pen, 0f);
                    float deflect = Math.Min(e * 0.5f, 1f);
                    float halve = Math.Max(Math.Min(e, 1f) - deflect, 0f);
                    float pass = Math.Max(1f - deflect - halve, 0f);
                    if (pass > 0f) next.Add((left, k, p * pass));
                    if (halve > 0f) next.Add((left * 0.5f, k == WeaponArmour.Sharp ? WeaponArmour.Blunt : k, p * halve));
                }
                branches = next;
            }
            float sum = 0f;
            foreach (var (left, _, p) in branches) sum += left * p;
            return sum;
        }

        /// <summary>The layers that cover a zone, outside in: an off-hand shield, then the piece, then the wearer's own armour.</summary>
        static List<ApparelLayer> LayersOn(ApparelZone z, IList<ApparelLayer> layers)
        {
            var on = new List<ApparelLayer>();
            foreach (var l in layers) if (l.Groups == null ? false : z.CoveredBy(l.Groups)) on.Add(l);
            foreach (var l in layers) if (l.Groups == null) on.Add(l);
            return on;
        }

        static float ZoneShare(ApparelZone z, ApparelThreat t) => t.Ranged ? z.Ranged : t.Armour == WeaponArmour.Blunt ? z.MeleeBlunt : z.Melee;

        /// <summary>A hit's damage as it lands on a bare body, over where hits land, a head hit counting double.</summary>
        public static float BareDamage(ApparelThreat t, IList<ApparelZone> body)
        {
            float s = 0f;
            foreach (var z in body) s += ZoneShare(z, t) * (z.Head ? HeadHarm : 1f);
            return s * t.Damage;
        }

        /// <summary>A hit's damage after the layers, over where hits land, a head hit counting double.</summary>
        public static float ArmouredDamage(ApparelThreat t, IList<ApparelZone> body, IList<ApparelLayer> layers)
        {
            float s = 0f;
            foreach (var z in body)
            {
                float share = ZoneShare(z, t);
                if (share <= 0f) continue;
                s += share * (z.Head ? HeadHarm : 1f) * AfterLayers(LayersOn(z, layers), t.Armour, t.Pen);
            }
            return s * t.Damage;
        }

        /// <summary>The share of shots that land on the groups given, a head hit counting double.</summary>
        public static float CoverageOf(IList<string> groups, IList<ApparelZone> body = null)
        {
            body = body ?? Human;
            float all = 0f, covered = 0f;
            foreach (var z in body)
            {
                float w = z.Ranged * (z.Head ? HeadHarm : 1f);
                all += w;
                if (groups != null && z.CoveredBy(groups)) covered += w;
            }
            return all > 0f ? covered / all : 0f;
        }

        // ---------------------------------------------------------------- stats, stuff and quality

        public static float Stat(Dictionary<string, float> d, string key, float def = 0f) => d != null && d.TryGetValue(key, out var v) ? v : def;

        /// <summary>
        /// An armour rating as the game works it out (StatPart_Stuff, StatPart_Quality, ArmorRatingBase): the piece's own rating
        /// plus its stuff's power times its multiplier, times its quality's factor, at most 2.
        /// </summary>
        public static float ArmourRating(ApparelFacts a, string kind)
        {
            float v = Stat(a.StatBases, "ArmorRating_" + kind) + Stat(a.StuffPowers, "StuffPower_Armor_" + kind) * Stat(a.StatBases, "StuffEffectMultiplierArmor");
            return WeaponRateMath.Clamp(v * a.ArmourQuality, 0f, 2f);
        }

        /// <summary>Insulation as the game works it out: the piece's own plus its stuff's power times its multiplier, times its quality's factor.</summary>
        public static float Insulation(ApparelFacts a, string kind)
        {
            float v = Stat(a.StatBases, "Insulation_" + kind) + Stat(a.StuffPowers, "StuffPower_Insulation_" + kind) * Stat(a.StatBases, "StuffEffectMultiplierInsulation_" + kind);
            return v * a.InsulationQuality;
        }

        /// <summary>What the piece changes on its wearer: its offset, and its factor's change on the stat's base value.</summary>
        public static float Offset(ApparelFacts a, string stat)
        {
            float o = Stat(a.Offsets, stat);
            if (a.Factors.TryGetValue(stat, out var f)) o += (f - 1f) * Stat(a.StatDefaults, stat, DefaultOf(stat));
            return o;
        }

        static float DefaultOf(string stat)
        {
            switch (stat)
            {
                case "MoveSpeed": return BaseMoveSpeed;
                case "PsychicEntropyMax": return 30f;
                case "PsychicEntropyRecoveryRate": return 0.54f;
                case "MeditationFocusGain": return 0.5f;
                case "CarryingCapacity": return 75f;
                case "PainShockThreshold": return 0.8f;
                case "MentalBreakThreshold": return 0.35f;
                default: return 1f;
            }
        }

        public const float BaseMoveSpeed = 4.6f;

        // ---------------------------------------------------------------- reading comps

        /// <summary>
        /// What a comp, a mod extension or a verb of the piece does, by its class and its fields as the def writes them: a
        /// shield (the game's CompShield, VEF's shield bubble and shield field), an off-hand shield (VEF's), a RimWorld of
        /// Magic enchantment, VEF's apparel extension (stat factors, work it disables, no learning, no bleeding),
        /// camouflage, oxygen, and the powers of its verbs (jump, smoke, low-shield, firefoam, grenades, lances, targeters,
        /// summons). Returns false for a class it doesn't know (named and marked rough by the caller) and true otherwise,
        /// including the ones that change nothing worth rating (colour, quality, art, biocoding, faction colours, contraband).
        /// </summary>
        public static bool ReadComp(ApparelFacts a, string cls, Func<string, string> field, Func<string, List<string>> list)
        {
            string c = cls ?? "";
            string tail = c.Contains(".") ? c.Substring(c.LastIndexOf('.') + 1) : c;
            float N(string k, float d = 0f) => float.TryParse(field(k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
            bool Bo(string k, bool d = false) { var s = field(k); return string.IsNullOrEmpty(s) ? d : s.Trim().ToLowerInvariant() == "true"; }
            switch (c)
            {
                case "CompProperties_Shield":
                {
                    float loss = N("energyLossPerDamage", 0.033f);
                    float max = Stat(a.StatBases, "EnergyShieldEnergyMax");
                    float rate = Stat(a.StatBases, "EnergyShieldRechargeRate", 0.45f);
                    a.Shields.Add(new ApparelShield
                    {
                        Label = "shield", Capacity = max / Math.Max(loss, 0.001f), Recharge = rate / Math.Max(loss, 0.001f),
                        ResetSeconds = N("startingTicksToReset", 3200f) / 60f, BlocksShooting = Bo("blocksRangedWeapons", true)
                    });
                    return true;
                }
                case "VEF.Apparels.CompProperties_ShieldBubble":
                {
                    float max = N("EnergyShieldEnergyMax");
                    if (max <= 0f) max = Stat(a.StatBases, "EnergyShieldEnergyMax") * 100f;
                    float rate = N("EnergyShieldRechargeRate");
                    if (rate <= 0f) rate = Stat(a.StatBases, "EnergyShieldRechargeRate");
                    float loss = N("EnergyLossPerDamage", 1f);
                    if (Bo("blockRangedAttack", true))
                        a.Shields.Add(new ApparelShield
                        {
                            Label = "shield bubble", Capacity = max / Math.Max(loss, 0.001f), Recharge = rate * max / Math.Max(loss, 0.001f),
                            BlocksShooting = Bo("dontAllowRangedAttack")
                        });
                    return true;
                }
                case "VEF.Apparels.CompProperties_ShieldField":
                {
                    float max = Stat(a.StatBases, "EnergyShieldEnergyMax");
                    float rate = Stat(a.StatBases, "EnergyShieldRechargeRate");
                    a.Shields.Add(new ApparelShield
                    {
                        Label = "shield field", Capacity = max / 0.033f, Recharge = rate / 0.033f, ResetSeconds = N("rechargeTicksWhenDepleted", 3200f) / 60f,
                        Field = true
                    });
                    return true;
                }
                case "VEF.Apparels.CompProperties_Shield":
                {
                    var g = list("coveredBodyPartGroups");
                    a.OffHandCovers = g != null && g.Count > 0 ? g.ToArray() : a.Groups.ToArray();
                    return true;
                }
                case "TorannMagic.Enchantment.CompProperties_EnchantedItem":
                {
                    foreach (var k in new[] { "maxMP", "mpRegenRate", "coolDown", "mpCost", "xpGain", "arcaneRes", "arcaneDmg", "healthRegenRate" })
                    {
                        float v = N(k);
                        if (v != 0f) a.Enchantment[k] = v;
                    }
                    if (Bo("arcaneSpectre")) a.Notes.Add("arcane spectre: its magic shield (A RimWorld of Magic's own code)");
                    if (Bo("phantomShift")) a.Notes.Add("phantom shift: blinks its wearer out of harm (A RimWorld of Magic's own code)");
                    if (a.Enchantment.Count == 0 && string.IsNullOrEmpty(field("hediff")) && !Bo("arcaneSpectre") && !Bo("phantomShift"))
                        a.Unsure.Add("its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted");
                    return true;
                }
                case "VEF.Apparels.ApparelExtension":
                    return true; // its fields are read by the review into Factors, WorkDisables, SkillGainZero, PreventsBleeding
                case "VEF.Apparels.CompProperties_Camouflage":
                    a.Camouflage = true;
                    return true;
                case "VanillaGravshipExpanded.CompProperties_ApparelOxygenProvider":
                    a.Powers.Add(new ApparelPower { Kind = "oxygen", Label = "oxygen supply", Charges = (int)N("maxCharges"), RunsOut = Bo("destroyOnEmpty") });
                    return true;
                case "CompProperties_Biocodable":
                    a.Biocodable = true;
                    return true;
                case "VAE_Accessories.CompProperties_ExplodeOnDeath":
                    a.Notes.Add("blows up when its wearer dies (" + field("explosionRadius") + " cells)");
                    return true;
                case "VFEC.Comps.CompProperties_ToggleHediff":
                    a.Notes.Add("a buff for allies nearby, switched on and off (its own hediff)");
                    return true;
                case "VFEPirates.CompProperties_RumSuit":
                    a.Notes.Add("makes rum as it is worn");
                    return true;
            }
            switch (tail)
            {
                case "CompProperties_Forbiddable":
                case "CompColorable":
                case "CompQuality":
                case "CompProperties_Styleable":
                case "CompProperties_Art":
                case "CompEquippable":
                case "ThingDefExtension":
                case "EquipmentOffsetConditions":
                case "ContrabandExtension":
                case "ApparelExtension":
                case "CompProperties_EditHeraldic":
                case "FemaleOnlyExtension":
                case "CompProperties_TargetEffect_FleckConnecting":
                case "CompProperties_TargetEffect_FleckOnTarget":
                case "CompProperties_TargetEffect_GoodwillImpact":
                case "CompProperties_TargetEffect_BrainDamageChance":
                case "CompTargetEffect_Berserk":
                case "CompTargetEffect_PsychicShock":
                case "CompTargetEffect_MutateIntoFleshbeast":
                case "CompProperties_ApparelReloadable":
                case "CompProperties_ApparelVerbOwnerCharged":
                case "CompProperties_ApparelVerbOwner":
                case "CompProperties_AIUSablePack":
                case "CompProperties_Explosive":
                case "CompFirefoamPack":
                case "CompToxPack":
                case "CompProperties_ReleaseGas":
                case "CompProperties_DisruptorFlarePack":
                case "CompProperties_CauseHediff_Apparel":
                case "CompProperties_ApparelHediffs":
                case "CompProperties_Pokeball":
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A verb of the piece as a power, by its class: a jump (Verb_Jump, an oxygen jump), smoke (Verb_SmokePop), a
        /// low-shield (Verb_DeployBroadshield), firefoam (Verb_FirefoamPop), a launched shot (a grenade, an incendiary, a
        /// frag: Verb_LaunchProjectileStatic and its kin, valued by the weapon rating), a psychic lance (Verb_CastTargetEffect
        /// with the shock or berserk effect), a targeter (Verb_Bombardment, Verb_PowerBeam, Verb_Spawn, Verb_MechCluster),
        /// a summon (a turret or a drone deployed, deadlife dust), a tox pack. Anything else is named, not counted.
        /// </summary>
        public static ApparelPower PowerOfVerb(string verbClass, string label, int charges, bool runsOut, float range, float radius, WeaponRangedInput shot,
            bool berserk, string projectileName)
        {
            string v = verbClass ?? "";
            string tail = v.Contains(".") ? v.Substring(v.LastIndexOf('.') + 1) : v;
            var p = new ApparelPower { Label = label ?? "", Charges = charges, RunsOut = runsOut, Range = range, Radius = radius };
            switch (tail)
            {
                case "Verb_Jump":
                case "Verb_OxygenJump":
                    p.Kind = "jump";
                    break;
                case "Verb_SmokePop":
                case "Verb_Smokepop":
                    p.Kind = "smoke";
                    p.Seconds = SmokeSeconds;
                    break;
                case "Verb_DeployBroadshield":
                    p.Kind = "lowshield";
                    p.Seconds = LowShieldSeconds;
                    break;
                case "Verb_FirefoamPop":
                    p.Kind = "firefoam";
                    break;
                case "Verb_CastTargetEffectLances":
                    p.Kind = "lance";
                    p.Berserk = berserk;
                    break;
                case "Verb_Bombardment":
                case "Verb_PowerBeam":
                case "Verb_BombardmentTargeter":
                    p.Kind = "targeter";
                    break;
                case "Verb_Spawn":
                    p.Kind = "targeter";
                    p.Note = "a tornado where it is aimed";
                    break;
                case "Verb_MechCluster":
                    p.Kind = "other";
                    p.Counted = false;
                    p.Note = "calls a hostile mechanoid cluster down where it is aimed";
                    break;
                case "Verb_DeployToxPack":
                    p.Kind = "gas";
                    break;
                case "Verb_LaunchProjectileStatic":
                case "Verb_LaunchProjectileStaticOneUse":
                case "Verb_LaunchProjectile":
                case "Verb_Shoot":
                case "Verb_LaunchProjectileStaticPsychic":
                    if (shot != null && (shot.HarmsHealth && shot.Damage > 0f || shot.Payloads.Count > 0))
                    {
                        p.Kind = "grenade";
                        p.Shot = shot;
                    }
                    else if ((projectileName ?? "").Contains("Turret") || (projectileName ?? "").Contains("Hunter"))
                    {
                        p.Kind = "summon";
                        p.Note = "deploys a fighter of its own";
                    }
                    else if ((projectileName ?? "").Contains("Deadlife"))
                    {
                        p.Kind = "summon";
                        p.Note = "raises the dead nearby to fight for its wearer";
                    }
                    else
                    {
                        p.Kind = "other";
                        p.Counted = false;
                    }
                    break;
                default:
                    p.Kind = "other";
                    p.Counted = false;
                    break;
            }
            return p;
        }

        /// <summary>How long a smokepop's cloud keeps shots off (seconds); smoke cuts a shot's chance by 30% (ShotReport's BlindSmoke).</summary>
        public const float SmokeSeconds = 20f, SmokeHitFactor = 0.7f;
        /// <summary>A low-shield stops every shot from outside for its 1800 ticks (CompDestroyAfterDelay), 4.9 cells around it.</summary>
        public const float LowShieldSeconds = 30f;

        // ---------------------------------------------------------------- the reference fight

        /// <summary>A fight the wearer is in: 30 seconds under the fire of one to two raiders (6 damage a second reaching them).</summary>
        public const float FightSeconds = 30f, IncomingRangedDps = 6f;
        /// <summary>How the overall weighs a shooter and a melee fighter when a piece suits both: three colonists in four fight with guns.</summary>
        public const float ShooterShare = 0.75f;
        /// <summary>
        /// A piece clearly suits one kind of fighter when what it does for them (how much longer they last times how much more
        /// they deal) is a tenth more than for the other: ln 1.1. The walk is left aside, since a slower walk costs every wearer
        /// (a melee fighter more, which is the weapon rating's own rule, not what the piece is made for).
        /// </summary>
        public static readonly float SuitsGap = (float)Math.Log(1.1);
        /// <summary>An enemy taken out of the fight (shocked, sent berserk, caught by a strike) counts as the damage it takes to down one: about 60.</summary>
        public const float EnemyDownValue = 60f;
        /// <summary>A melee fighter crosses about 20 cells under fire to reach the enemy, twice a fight.</summary>
        public const float ChargeCells = 20f;

        /// <summary>The reference shooter's weapon: an assault rifle (WeaponRateMath's own numbers, weapons.json).</summary>
        public static WeaponRangedInput ReferenceRifle() => new WeaponRangedInput
        {
            Damage = 11f, ArmourPen = 0.165f, Warmup = 1f, Cooldown = 1.7f, Burst = 3, TicksBetweenBurst = 10, Range = 30.9f,
            AccTouch = 0.6f, AccShort = 0.7f, AccMedium = 0.65f, AccLong = 0.55f
        };

        /// <summary>The reference melee fighter's weapon: a steel longsword (its handle, point and edge).</summary>
        public static WeaponMeleeInput ReferenceSword() => new WeaponMeleeInput
        {
            Attacks = new List<WeaponMeleeAttack>
            {
                new WeaponMeleeAttack { Label = "handle", Power = 9f, Cooldown = 2f, Armour = WeaponArmour.Blunt },
                new WeaponMeleeAttack { Label = "point", Power = 23f, Cooldown = 2.6f, Armour = WeaponArmour.Sharp },
                new WeaponMeleeAttack { Label = "edge", Power = 23f, Cooldown = 2.6f, Armour = WeaponArmour.Sharp },
            }
        };

        /// <summary>
        /// The share of a fight's ranged damage a shield absorbs: from full it takes its capacity, then its recharge each
        /// second, until the incoming fire (6 a second for 30 seconds) breaks it; once broken it is down for its reset; at
        /// most <see cref="ShieldCap"/>.
        /// </summary>
        public static float ShieldShare(ApparelShield s)
        {
            if (s == null || s.Capacity <= 0f) return 0f;
            float total = IncomingRangedDps * FightSeconds;
            float net = IncomingRangedDps - s.Recharge;
            if (net <= 0f) return ShieldCap;
            float breakAt = s.Capacity / net;
            if (breakAt >= FightSeconds) return ShieldCap;
            float absorbed = IncomingRangedDps * breakAt;
            float back = breakAt + s.ResetSeconds;
            if (back < FightSeconds) absorbed += Math.Min(IncomingRangedDps * (FightSeconds - back), s.Capacity * 0.2f + s.Recharge * (FightSeconds - back));
            return WeaponRateMath.Clamp(absorbed / total, 0f, ShieldCap);
        }

        /// <summary>
        /// Even the best shield lets some through: an EMP shot drops it at once, a heavy volley overwhelms it, a blast lands
        /// before it is back. It takes at most 85% of a fight's shots and blasts.
        /// </summary>
        public const float ShieldCap = 0.85f;

        // ---------------------------------------------------------------- the fight

        /// <summary>The armour layers the piece puts on its wearer: an off-hand shield's (when held), its own, and its armour offsets on the whole body.</summary>
        public static List<ApparelLayer> LayersOf(ApparelFacts a, bool withOffHand)
        {
            var l = new List<ApparelLayer>();
            float sh = ArmourRating(a, "Sharp"), bl = ArmourRating(a, "Blunt"), he = ArmourRating(a, "Heat");
            if (withOffHand && a.OffHandCovers != null)
                l.Add(new ApparelLayer { Label = "off-hand shield", Groups = a.OffHandCovers, Sharp = sh, Blunt = bl, Heat = he });
            else if (a.OffHandCovers == null && (sh > 0f || bl > 0f || he > 0f))
                l.Add(new ApparelLayer { Label = a.Label, Groups = a.Groups.ToArray(), Sharp = sh, Blunt = bl, Heat = he });
            float os = Stat(a.Offsets, "ArmorRating_Sharp"), ob = Stat(a.Offsets, "ArmorRating_Blunt"), oh = Stat(a.Offsets, "ArmorRating_Heat");
            if (os > 0f || ob > 0f || oh > 0f)
                l.Add(new ApparelLayer { Label = "on the wearer", Groups = null, Sharp = os, Blunt = ob, Heat = oh });
            return l;
        }

        /// <summary>
        /// How much longer the wearer lasts against each family (bare damage over damage taken), in a context: as a shooter
        /// or a melee fighter. Armour by the game's roll; shields take shots and blasts first; smoke and a low-shield keep shots
        /// off for their time; a jump shortens a melee fighter's charge under fire; dodge turns melee blows; damage taken and
        /// pain change what the wearer can take.
        /// </summary>
        public static Dictionary<string, float> Toughness(ApparelFacts a, bool melee, IList<ApparelZone> body = null)
        {
            body = body ?? Human;
            var layers = LayersOf(a, melee);
            float dmgFactor = Math.Max(0.1f, 1f + Offset(a, "IncomingDamageFactor"));
            float dodgeBase = WeaponRateMath.MeleeDodgeChance(8f);
            float dodge = WeaponRateMath.MeleeDodgeChance(8f + Offset(a, "MeleeDodgeChance"));
            float meleeTaken = (1f - dodge) / (1f - dodgeBase);
            float shieldTaken = 1f;
            foreach (var s in a.Shields) shieldTaken *= 1f - ShieldShare(s);
            float coverTaken = 1f;
            float jumpTaken = 1f;
            float fireTaken = 1f;
            foreach (var p in a.Powers)
            {
                if (!p.Counted) continue;
                float use = UseShare(p);
                switch (p.Kind)
                {
                    case "smoke":
                        coverTaken *= 1f - use * (1f - SmokeHitFactor) * Math.Min(p.Seconds / FightSeconds, 1f);
                        break;
                    case "lowshield":
                        coverTaken *= 1f - use * 0.67f * Math.Min(p.Seconds / FightSeconds, 1f);
                        break;
                    case "jump":
                        jumpTaken *= 1f - use * (melee ? 1f : 0.5f) * JumpSaved(p) / FightSeconds;
                        break;
                    case "firefoam":
                        fireTaken *= 1f - use * 0.5f;
                        break;
                }
            }
            var t = new Dictionary<string, float>();
            foreach (var fam in Families)
            {
                float bare = 0f, taken = 0f;
                foreach (var th in Threats)
                {
                    if (th.Family != fam) continue;
                    float b = th.Weight * BareDamage(th, body);
                    float w = th.Weight * ArmouredDamage(th, body, layers) * dmgFactor;
                    if (th.Projectile) w *= shieldTaken;
                    if (th.Ranged) w *= coverTaken * jumpTaken;
                    if (!th.Ranged && !th.Projectile) w *= meleeTaken;
                    if (fam == "fire") w *= fireTaken;
                    bare += b;
                    taken += w;
                }
                t[fam] = taken > 0f ? bare / taken : 100f;
            }
            return t;
        }

        /// <summary>
        /// How much of a fight a power is there for: a power with charges is used once a fight (twice for a jump, one per
        /// charge), and one that is gone for good when its charges are spent (a low-shield, a psychic lance, an orbital
        /// targeter) counts at half: it serves a fight or two, then the colony must find another.
        /// </summary>
        public static float UseShare(ApparelPower p) => p.RunsOut ? 0.5f : 1f;

        /// <summary>The seconds a jump saves a melee fighter's charge: 20 cells walked under fire become a one-second jump, twice a fight.</summary>
        public static float JumpSaved(ApparelPower p) =>
            Math.Min(Math.Max(Math.Min(p.Range, ChargeCells) / BaseMoveSpeed - 1f, 0f) * Math.Min(Math.Max(p.Charges, 2), 2), FightSeconds * 0.5f);

        /// <summary>The pooled toughness over the families by their weights (damage taken over the whole mix), and pain's part.</summary>
        public static float PooledToughness(Dictionary<string, float> t, ApparelFacts a)
        {
            float bare = 0f, taken = 0f;
            foreach (var fam in Families)
            {
                float b = FamilyWeights[fam] * FamilyBare(fam);
                bare += b;
                taken += b / Math.Max(t[fam], 0.01f);
            }
            float pooled = taken > 0f ? bare / taken : 1f;
            return pooled * PainFactor(a);
        }

        static readonly Dictionary<string, float> familyBare = new Dictionary<string, float>();

        /// <summary>A family's bare damage per hit, as it lands on a bare human (for pooling the families by the damage they deal).</summary>
        public static float FamilyBare(string fam)
        {
            lock (familyBare)
            {
                if (familyBare.TryGetValue(fam, out var v)) return v;
                float s = 0f;
                foreach (var th in Threats) if (th.Family == fam) s += th.Weight * BareDamage(th, Human);
                familyBare[fam] = s;
                return s;
            }
        }

        /// <summary>
        /// Pain: a higher pain shock threshold keeps the wearer up longer. Pain is about half of how a colonist falls in a
        /// fight (bleeding and lost parts the other half), so the threshold's rise counts at half (0.8 is a colonist's own).
        /// </summary>
        public static float PainFactor(ApparelFacts a)
        {
            float x = Offset(a, "PainShockThreshold");
            float pain = a.PainOffset;
            return Math.Max(0.5f, 1f + 0.5f * (x / 0.8f) - 0.5f * pain);
        }

        /// <summary>The weapon rating's reference fighter with the piece's stat offsets on them (shooting and melee skill, aim, cooldowns, melee damage).</summary>
        public static WeaponFighter FighterWith(ApparelFacts a)
        {
            var f = WeaponRateMath.Reference;
            f.ShootingSkill += Offset(a, "ShootingAccuracyPawn");
            f.MeleeSkill += Offset(a, "MeleeHitChance");
            f.AimFactor = Math.Max(0.1f, 1f + Offset(a, "AimingDelayFactor"));
            f.RangedCooldownFactor = Math.Max(0.1f, 1f + Offset(a, "RangedCooldownFactor")) * RangedCooldownFactorOf(a);
            f.MeleeCooldownFactor = Math.Max(0.1f, 1f + Offset(a, "MeleeCooldownFactor"));
            f.MeleeDamageFactor = Math.Max(0.1f, 1f + Offset(a, "MeleeDamageFactor"));
            return f;
        }

        /// <summary>
        /// VEF's own ranged cooldown factor on a quiver or an ammo pack (VEF_RangedCooldownFactor, its stat base): it shortens
        /// the wearer's cooldown between shots by that share (a quiver's for bows and other neolithic and medieval weapons only).
        /// </summary>
        public static float RangedCooldownFactorOf(ApparelFacts a)
        {
            float v = Stat(a.StatBases, "VEF_RangedCooldownFactor");
            return v > 0f ? Math.Max(0.1f, 1f - v * 0.5f) : 1f;
        }

        /// <summary>What a power adds to its wearer's output in a minute's fight, as damage a second on the weapon rating's own footing.</summary>
        public static float PowerOutput(ApparelPower p)
        {
            if (!p.Counted) return 0f;
            float perFight = (p.Charges > 0 ? Math.Min(p.Charges, 2) : p.CooldownSeconds > 0f ? Math.Max(1f, 60f / p.CooldownSeconds) : 1f) * UseShare(p);
            switch (p.Kind)
            {
                case "grenade":
                {
                    if (p.Shot == null) return 0f;
                    var ab = new WeaponAbilityInput { Label = p.Label, Counted = true, Shot = p.Shot, Charges = (int)Math.Ceiling(perFight) };
                    return WeaponRateMath.AbilityValue(ab, WeaponRateMath.Reference, WeaponRateMath.RangedMix);
                }
                case "gun":
                    return p.Shot == null ? 0f : WeaponRateMath.RangedFightValue(p.Shot, WeaponRateMath.Reference, WeaponRateMath.RangedRole(p.Shot));
                case "lance":
                    return perFight * EnemyDownValue * (p.Berserk ? 1.5f : 1f) / 60f;
                case "targeter":
                    return perFight * EnemyDownValue * 3f / 60f;
                case "summon":
                    return perFight * EnemyDownValue / 60f;
                case "gas":
                    return perFight * EnemyDownValue * 0.5f / 60f;
                default:
                    return 0f;
            }
        }

        /// <summary>The reference shooter's and melee fighter's own fight value (the weapon rating's), for setting an output against.</summary>
        public static readonly float ShooterValue = WeaponRateMath.RangedFightValue(ReferenceRifle(), WeaponRateMath.Reference, "all-round");
        public static readonly float MeleeValue = WeaponRateMath.MeleeFightValue(ReferenceSword(), WeaponRateMath.Reference, "heavy melee");

        /// <summary>A shooter's and a melee fighter's output with the piece on: their weapon with its stat offsets, and its powers added.</summary>
        public static (float shooter, float melee) Output(ApparelFacts a)
        {
            var f = FighterWith(a);
            float shoot = WeaponRateMath.RangedFightValue(ReferenceRifle(), f, "all-round") / ShooterValue;
            float melee = WeaponRateMath.MeleeFightValue(ReferenceSword(), f, "heavy melee") / MeleeValue;
            float extra = 0f;
            foreach (var p in a.Powers) extra += PowerOutput(p);
            shoot += Math.Min(extra / ShooterValue, WeaponRateMath.AbilityShareCap);
            melee += Math.Min(extra / MeleeValue, WeaponRateMath.AbilityShareCap);
            foreach (var p in a.Powers)
            {
                if (!p.Counted) continue;
                // A shooter in the cloud fires through its edge now and then: a quarter of the smoke's own cut.
                if (p.Kind == "smoke") shoot *= 1f - UseShare(p) * 0.25f * (1f - SmokeHitFactor) * Math.Min(p.Seconds / FightSeconds, 1f);
                // A jump puts a melee fighter to work sooner, and lets a shooter reach cover or a flank.
                if (p.Kind == "jump")
                {
                    float saved = UseShare(p) * JumpSaved(p) / FightSeconds;
                    melee *= 1f + saved;
                    shoot *= 1f + 0.25f * saved;
                }
            }
            if (BlocksShooting(a)) shoot = 0f;
            float sight = CapacityFactor(a, "Sight");
            float manip = CapacityFactor(a, "Manipulation");
            shoot *= sight * manip;
            melee *= (0.5f + 0.5f * sight) * manip;
            return (shoot, melee);
        }

        /// <summary>What a capacity is left at with the piece's hediffs (a blindfold's sight, knuckle dusters' none).</summary>
        public static float CapacityFactor(ApparelFacts a, string capacity)
        {
            float v = 1f;
            foreach (var (cap, off, fac, max) in a.Capacities)
            {
                if (cap != capacity) continue;
                v = (v + off) * fac;
                if (max >= 0f) v = Math.Min(v, max);
            }
            return WeaponRateMath.Clamp(v, 0f, 2f);
        }

        public static bool BlocksShooting(ApparelFacts a)
        {
            foreach (var s in a.Shields) if (s.BlocksShooting) return true;
            return false;
        }

        /// <summary>A slower or quicker walk: half its share for a shooter, all of it for a melee fighter (the weapon rating's own rule).</summary>
        public static (float shooter, float melee) Feet(ApparelFacts a)
        {
            float d = Offset(a, "MoveSpeed");
            float moving = CapacityFactor(a, "Moving");
            float rel = (BaseMoveSpeed * moving + d) / BaseMoveSpeed - 1f;
            return (Math.Max(0.3f, 1f + 0.5f * rel), Math.Max(0.2f, 1f + rel));
        }

        // ---------------------------------------------------------------- the other areas

        /// <summary>
        /// The worth of one stat change as a share of what a colonist gets done: the area it belongs to and its share per
        /// unit. A whole-work stat counts in full; one kind of work at a third (whoever wears it is the one doing it, about a
        /// third of their day); medical at a third; social impact at 0.3, negotiation at 0.4, trade at 0.6; a lower mental
        /// break threshold at 1.5 (a tenth lower is far fewer breaks); learning at 0.3; psychic sensitivity in full for a
        /// psycaster, psychic heat and focus as a share of their base at half and a third; a mechanitor's bandwidth as a share
        /// of their own 6, at half; toxins at 0.15 a full point, vacuum at 0.3 (surviving it is the point); carrying at 0.2 of the base 75 kg; hunting stealth
        /// at 0.2. Stats a fight counts (aim, melee, move speed, pain, dodge, damage taken, armour) are in the fight instead.
        /// </summary>
        public static readonly Dictionary<string, (string area, float perUnit)> StatWorth = new Dictionary<string, (string, float)>
        {
            ["WorkSpeedGlobal"] = ("work", 1f),
            ["GeneralLaborSpeed"] = ("work", 0.33f),
            ["ConstructionSpeed"] = ("work", 0.33f),
            ["ConstructSuccessChance"] = ("work", 0.33f),
            ["FixBrokenDownBuildingSuccessChance"] = ("work", 0.1f),
            ["SmoothingSpeed"] = ("work", 0.1f),
            ["MiningSpeed"] = ("work", 0.33f),
            ["PlantWorkSpeed"] = ("work", 0.33f),
            ["PlantHarvestYield"] = ("work", 0.33f),
            ["AnimalGatherSpeed"] = ("work", 0.1f),
            ["AnimalGatherYield"] = ("work", 0.1f),
            ["CookSpeed"] = ("work", 0.33f),
            ["FoodPoisonChance"] = ("work", -0.5f),
            ["ButcheryFleshSpeed"] = ("work", 0.1f),
            ["ButcheryFleshEfficiency"] = ("work", 0.2f),
            ["DrugCookingSpeed"] = ("work", 0.1f),
            ["DrugSynthesisSpeed"] = ("work", 0.1f),
            ["CleaningSpeed"] = ("work", 0.1f),
            ["ResearchSpeed"] = ("work", 0.33f),
            ["EntityStudyRate"] = ("work", 0.1f),
            ["ForagedNutritionPerDay"] = ("work", 0.1f),
            ["MedicalTendQuality"] = ("work", 0.33f),
            ["MedicalTendSpeed"] = ("work", 0.2f),
            ["MedicalSurgerySuccessChance"] = ("work", 0.33f),
            ["MedicalOperationSpeed"] = ("work", 0.1f),
            ["MedicalPotency"] = ("work", 0.2f),
            ["SocialImpact"] = ("social", 0.3f),
            ["NegotiationAbility"] = ("social", 0.4f),
            ["TradePriceImprovement"] = ("social", 0.6f),
            ["SuppressionPower"] = ("social", 0.2f),
            ["MentalBreakThreshold"] = ("mind", -1.5f),
            ["GlobalLearningFactor"] = ("mind", 0.3f),
            ["RestRateMultiplier"] = ("mind", 0.2f),
            ["RestFallRateFactor"] = ("mind", -0.2f),
            ["ImmunityGainSpeed"] = ("mind", 0.2f),
            ["InjuryHealingFactor"] = ("mind", 0.2f),
            ["EatingSpeed"] = ("mind", 0.05f),
            ["PsychicEntropyRecoveryRate"] = ("psychic", 0.5f / 0.54f),
            ["PsychicEntropyMax"] = ("psychic", 0.5f / 30f),
            ["MeditationFocusGain"] = ("psychic", 0.33f / 0.5f),
            ["MechBandwidth"] = ("mech", 0.5f / 6f),
            ["MechControlGroups"] = ("mech", 0.1f),
            ["WorkSpeedGlobalOffsetMech"] = ("mech", 0.3f),
            ["ToxicEnvironmentResistance"] = ("toxic", 0.15f),
            ["ToxicResistance"] = ("toxic", 0.15f),
            ["VacuumResistance"] = ("vacuum", 0.3f),
            ["CarryingCapacity"] = ("carry", 0.2f / 75f),
            ["VEF_MassCarryCapacity"] = ("carry", 0.1f / 35f),
            ["HuntingStealth"] = ("hunting", 0.2f),
        };

        /// <summary>The stats the fight counts, so they aren't counted twice.</summary>
        public static readonly HashSet<string> FightStats = new HashSet<string>
        {
            "MoveSpeed", "ShootingAccuracyPawn", "AimingDelayFactor", "RangedCooldownFactor", "MeleeHitChance", "MeleeDodgeChance",
            "MeleeDamageFactor", "MeleeCooldownFactor", "PainShockThreshold", "IncomingDamageFactor", "ArmorRating_Sharp",
            "ArmorRating_Blunt", "ArmorRating_Heat",
        };

        public static readonly string[] Areas = { "fight", "cold", "heat", "work", "social", "mind", "psychic", "mech", "magic", "toxic", "vacuum", "carry", "hunting" };

        /// <summary>A degree of insulation counts as a hundredth of a colonist: ten degrees, a tenth (in the climate it is worn for).</summary>
        public const float WorthPerDegree = 0.01f;

        /// <summary>Walking is about half a colonist's day: a slower or quicker walk counts at half outside fights, in work.</summary>
        public const float WalkShareOfWork = 0.5f;

        /// <summary>
        /// A RimWorld of Magic enchantment's worth for a mage, at half (a mage's own gains, as a psycaster's): more mana,
        /// faster regeneration, shorter cooldowns and cheaper spells in full, more experience and arcane damage at half,
        /// arcane resistance at a quarter.
        /// </summary>
        public static float MagicWorth(ApparelFacts a)
        {
            float E(string k) => Stat(a.Enchantment, k);
            return 0.5f * (E("maxMP") + E("mpRegenRate") - E("coolDown") - E("mpCost") + 0.5f * E("xpGain") + 0.5f * E("arcaneDmg") + 0.25f * E("arcaneRes") + 0.5f * E("healthRegenRate"));
        }

        /// <summary>Lower psychic sensitivity shelters the mind from psychic drones and lances, which come now and then: 0.15 a full point.</summary>
        public const float ShelterWorth = 0.15f;

        /// <summary>Psychic sensitivity the piece adds (the game's PsychicSensitivity, Royalty's eltex PsychicSensitivityOffset).</summary>
        public static float Sensitivity(ApparelFacts a) => Offset(a, "PsychicSensitivity") + Stat(a.Offsets, "PsychicSensitivityOffset");

        /// <summary>Each area's worth, before the role's weights.</summary>
        public static Dictionary<string, float> AreaWorth(ApparelFacts a, float fight)
        {
            var w = new Dictionary<string, float>();
            foreach (var k in Areas) w[k] = 0f;
            w["fight"] = fight - 1f;
            w["cold"] = Insulation(a, "Cold") * WorthPerDegree - Offset(a, "ComfyTemperatureMin") * WorthPerDegree;
            w["heat"] = Insulation(a, "Heat") * WorthPerDegree + Offset(a, "ComfyTemperatureMax") * WorthPerDegree;
            var keys = new HashSet<string>(a.Offsets.Keys);
            foreach (var k in a.Factors.Keys) keys.Add(k);
            foreach (var k in keys)
            {
                if (FightStats.Contains(k) || !StatWorth.TryGetValue(k, out var rule)) continue;
                w[rule.area] += Offset(a, k) * rule.perUnit;
            }
            float sens = Sensitivity(a);
            if (sens > 0f) w["psychic"] += sens;
            w["mind"] += Math.Max(0f, -sens) * ShelterWorth;
            w["work"] += (Feet(a).melee - 1f) * WalkShareOfWork;
            if (!string.IsNullOrEmpty(a.WorkDisables)) w["work"] -= 0.15f;
            if (a.SkillGainZero) w["mind"] -= 0.3f;
            w["magic"] += MagicWorth(a);
            if (a.ImmuneToToxGas) w["toxic"] += 0.1f;
            if (a.Camouflage) w["hunting"] += 0.1f;
            foreach (var p in a.Powers)
            {
                if (!p.Counted) continue;
                if (p.Kind == "oxygen") w["vacuum"] += 0.1f;
                if (p.Kind == "resurrect") w["fight"] += 0.3f;
                if (p.Kind == "stabilize") w["work"] += 0.05f;
            }
            if (a.PreventsBleeding) w["fight"] += 0.1f;
            float man = CapacityFactor(a, "Manipulation"), sight = CapacityFactor(a, "Sight");
            w["work"] -= (1f - man * sight) * 0.8f;
            return w;
        }

        // ---------------------------------------------------------------- roles

        public static readonly string[] Roles = { "body armour", "helmet", "off-hand shield", "utility", "everyday clothing", "headgear", "cold-weather gear", "hot-weather gear", "hazard gear", "ceremonial or psychic" };

        /// <summary>
        /// How much each area counts by role: a piece is rated for what it is for. Armour and helmets on the fight, with a
        /// little for the rest; utility on whatever it does; everyday clothing and hats on warmth, work, talks and the mind
        /// with some for the fight; weather gear on its cold or heat; ceremonial and psychic pieces on psycasts, talks and the
        /// mind; hazard gear on keeping out toxins and vacuum.
        /// </summary>
        public static Dictionary<string, float> RoleWeights(string role)
        {
            var w = new Dictionary<string, float>();
            void Set(float fight, float cold, float heat, float work, float social, float mind, float psychic, float mech, float magic, float toxic, float vacuum, float carry, float hunting)
            {
                w["fight"] = fight; w["cold"] = cold; w["heat"] = heat; w["work"] = work; w["social"] = social; w["mind"] = mind;
                w["psychic"] = psychic; w["mech"] = mech; w["magic"] = magic; w["toxic"] = toxic; w["vacuum"] = vacuum; w["carry"] = carry; w["hunting"] = hunting;
            }
            switch (role)
            {
                case "body armour":
                case "helmet":
                case "off-hand shield":
                    Set(1f, 0.15f, 0.15f, 0.5f, 0.3f, 0.5f, 0.3f, 0.5f, 0.3f, 0.2f, 0.15f, 0.3f, 0.3f); break;
                case "hazard gear":
                    Set(0.4f, 0.3f, 0.3f, 0.5f, 0.5f, 0.5f, 0.3f, 0.3f, 0.3f, 1f, 1f, 0.5f, 0.5f); break;
                case "utility":
                    Set(1f, 0.5f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f); break;
                case "cold-weather gear":
                    Set(0.4f, 1f, 0.2f, 0.5f, 0.5f, 0.5f, 0.3f, 0.3f, 0.3f, 0.5f, 0.5f, 0.5f, 0.5f); break;
                case "hot-weather gear":
                    Set(0.4f, 0.2f, 1f, 0.5f, 0.5f, 0.5f, 0.3f, 0.3f, 0.3f, 0.5f, 0.5f, 0.5f, 0.5f); break;
                case "ceremonial or psychic":
                    Set(0.4f, 0.4f, 0.4f, 0.6f, 1f, 1f, 1f, 1f, 1f, 0.5f, 0.5f, 0.5f, 0.5f); break;
                default:
                    Set(0.4f, 0.6f, 0.6f, 1f, 1f, 1f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f); break;
            }
            return w;
        }

        /// <summary>The scale's middle (the worth that rates 5.5) and width, per role, set so the base game's pieces spread over the scale (Tools/Tests holds the anchors).</summary>
        public static (float mid, float width) RoleScale(string role)
        {
            switch (role)
            {
                case "body armour": return (0.42f, 0.85f);
                case "helmet": return (0.04f, 1.10f);
                case "off-hand shield": return (0.42f, 0.85f);
                case "utility": return (0.20f, 0.75f);
                case "hazard gear": return (0.30f, 0.60f);
                case "cold-weather gear": return (0.28f, 0.45f);
                case "hot-weather gear": return (0.22f, 0.45f);
                case "headgear": return (0.06f, 0.80f);
                case "ceremonial or psychic": return (0.30f, 0.60f);
                default: return (0.08f, 0.60f);
            }
        }

        /// <summary>The scale: the weapon rating's own Laplace curve in the log of the worth over the role's middle, 1.0 to 10.0 with one decimal; no worth at all is 1.0.</summary>
        public static float Scale(float worth, float mid, float width)
        {
            if (worth <= 0f) return 1f;
            double x = Math.Log(worth / mid) / width;
            double r = 1.0 + 9.0 * WeaponRateMath.Spread(x);
            return (float)Math.Round(Math.Min(Math.Max(r, 1.0), 10.0), 1);
        }

        /// <summary>
        /// The sub-ratings' and situations' scales: protection (how much longer the wearer lasts) and insulation by slot, a
        /// head piece against head pieces; the other areas on one scale for every piece.
        /// </summary>
        public static (float mid, float width) SubScale(string key, string slot)
        {
            bool head = slot == "head";
            switch (key)
            {
                case "guns":
                case "melee":
                case "mechanoids":
                    return head ? (0.07f, 0.55f) : (0.70f, 0.55f);
                case "fire":
                    return head ? (0.07f, 0.55f) : (0.70f, 0.55f);
                case "shooter":
                case "melee fighter":
                    return head ? (0.07f, 0.55f) : (0.70f, 0.55f);
                case "cold": return head ? (6f, 0.6f) : (18f, 0.7f);
                case "heat": return head ? (6f, 0.6f) : (14f, 0.7f);
                case "work": return (0.06f, 0.6f);
                case "social": return (0.05f, 0.6f);
                case "mind": return (0.08f, 0.6f);
                case "psychic": return (0.2f, 0.6f);
                case "psychic shelter": return (0.3f, 0.6f);
                case "mechanitor": return (0.5f, 0.6f);
                case "magic": return (0.3f, 0.6f);
                case "toxins": return (0.5f, 0.6f);
                case "vacuum": return (0.4f, 0.6f);
                case "hunting": return (0.3f, 0.6f);
                case "carrying": return (0.06f, 0.6f);
                default: return (0.2f, 0.6f);
            }
        }

        /// <summary>A piece's slot: the head when it covers only head groups (and perhaps the neck below it), else the body.</summary>
        public static string SlotOf(ApparelFacts a)
        {
            if (a.Groups.Count == 0) return "body";
            bool head = false;
            foreach (var g in a.Groups)
            {
                if (Array.IndexOf(HeadGroups, g) >= 0) head = true;
                else if (g != "Neck") return "body";
            }
            return head ? "head" : "body";
        }

        /// <summary>Layers that are utility slots: a belt or pack, Melee Animation's hip, A RimWorld of Magic's artifacts.</summary>
        public static readonly string[] UtilityLayers = { "Belt", "AM_Hip", "TM_Artifact" };

        /// <summary>
        /// What a piece is for, from its own numbers, judged as made in plain stuff (cloth, plain leather, steel, wood,
        /// granite: <see cref="PlainStuffs"/>, so a duster is hot-weather gear whatever it is made of): an off-hand shield
        /// (VEF's); a belt, pack or accessory; a royal or psychic piece whose psychic, magic or royal worth outweighs its
        /// fight; on the head, combat headgear (a helmet) when its sharp armour is 0.4 or more, it is made as armour (its
        /// stuff's armour counts at 45% or more) or it makes its wearer a tenth better in a fight (a gunlink), else headgear;
        /// on the body, armour when its sharp armour is 0.4 or more, it is made as armour, or its protection alone lasts its
        /// wearer a fifth longer, else hazard gear when keeping out toxins and vacuum is worth more
        /// than its fight (a vacsuit, a hazmat suit), else weather gear when its insulation is 14 degrees or more, a third
        /// above the other kind and worth at least what it does for work, talks, the mind, carrying and hunting, else everyday
        /// clothing.
        /// </summary>
        public static string RoleOf(ApparelFacts a, ApparelRating r)
        {
            if (a.OffHandCovers != null) return "off-hand shield";
            foreach (var l in a.Layers) if (Array.IndexOf(UtilityLayers, l) >= 0) return "utility";
            string slot = SlotOf(a);
            var worth = r.Areas;
            float fight = worth["fight"];
            float spirit = worth["psychic"] + worth["magic"] + (a.Titles.Count > 0 && r.Sharp < 0.4f ? 0.1f : 0f);
            bool madeAsArmour = Stat(a.StatBases, "StuffEffectMultiplierArmor") >= 0.45f;
            bool armoured = slot == "head" ? r.Sharp >= 0.4f || madeAsArmour || fight >= 0.1f : r.Sharp >= 0.4f || madeAsArmour || r.Protection - 1f >= 0.2f;
            if (spirit > 0.15f && spirit > fight) return "ceremonial or psychic";
            float hazard = worth["toxic"] + worth["vacuum"];
            if (hazard >= 0.05f && hazard > fight && slot == "body") return "hazard gear";
            if (slot == "head") return armoured ? "helmet" : "headgear";
            if (armoured) return "body armour";
            float rest = 0f;
            foreach (var k in new[] { "work", "social", "mind", "psychic", "mech", "magic", "carry", "hunting" }) rest += Math.Max(worth[k], 0f);
            if (r.Cold >= 14f && r.Cold >= 1.3f * r.HeatInsulation && worth["cold"] >= rest) return "cold-weather gear";
            if (r.HeatInsulation >= 14f && r.HeatInsulation >= 1.3f * r.Cold && worth["heat"] >= rest) return "hot-weather gear";
            return "everyday clothing";
        }

        // ---------------------------------------------------------------- the rating

        /// <summary>
        /// The rating of a piece: its fight (toughness, output and feet, as a shooter and as a melee fighter), every area's
        /// worth, its role (given, or from its numbers), the overall on the role's scale, the sub-ratings and situations,
        /// specials, best for and weak when.
        /// </summary>
        public static ApparelRating Rate(ApparelFacts a, string role = null)
        {
            var r = new ApparelRating();
            r.Slot = SlotOf(a);
            r.Sharp = ArmourRating(a, "Sharp");
            r.Blunt = ArmourRating(a, "Blunt");
            r.Heat = ArmourRating(a, "Heat");
            r.Cold = Insulation(a, "Cold");
            r.HeatInsulation = Insulation(a, "Heat");
            r.Coverage = CoverageOf(a.OffHandCovers ?? a.Groups.ToArray());
            var tShoot = Toughness(a, false);
            var tMelee = Toughness(a, true);
            var (oS, oM) = Output(a);
            var (mS, mM) = Feet(a);
            float pS = PooledToughness(tShoot, a), pM = PooledToughness(tMelee, a);
            r.Protection = pS;
            r.FightShooter = pS * oS * mS;
            r.FightMelee = pM * oM * mM;
            r.Lean = (float)(Math.Log(Math.Max(pM * oM, 0.01f)) - Math.Log(Math.Max(pS * oS, 0.01f)));
            bool meleeOnly = BlocksShooting(a) || a.OffHandCovers != null || r.Lean >= SuitsGap;
            bool shooterOnly = !meleeOnly && r.Lean <= -SuitsGap;
            r.For = meleeOnly ? "a melee fighter" : shooterOnly ? "a shooter" : "anyone";
            r.Fight = meleeOnly ? r.FightMelee : shooterOnly ? r.FightShooter
                : (float)Math.Exp(ShooterShare * Math.Log(Math.Max(r.FightShooter, 0.01f)) + (1f - ShooterShare) * Math.Log(Math.Max(r.FightMelee, 0.01f)));
            var t = meleeOnly ? tMelee : tShoot;
            foreach (var fam in Families) r.Toughness[fam] = t[fam];
            r.Areas = AreaWorth(a, r.Fight);
            r.Role = role ?? RoleOf(a, r);
            r.Worth = WorthFor(r.Areas, r.Role);
            var (mid, width) = RoleScale(r.Role);
            r.Overall = Scale(r.Worth, mid, width);
            r.OverallShooter = Scale(WorthFor(AreaWorth(a, r.FightShooter), r.Role), mid, width);
            r.OverallMelee = Scale(WorthFor(AreaWorth(a, r.FightMelee), r.Role), mid, width);

            // Sub-ratings: protection and insulation by slot, the rest on one scale.
            string slot = r.Slot;
            float Sub(string key, float v) { var (m, w) = SubScale(key, slot); return Scale(v, m, w); }
            float meleeT = Pool(t, "melee", "animals");
            r.Sub["guns"] = Sub("guns", t["guns"] - 1f);
            r.Sub["melee"] = Sub("melee", meleeT - 1f);
            r.Sub["fire"] = Sub("fire", t["fire"] - 1f);
            r.Sub["mechanoids"] = Sub("mechanoids", t["mechanoids"] - 1f);
            r.Sub["cold"] = Sub("cold", r.Cold - Offset(a, "ComfyTemperatureMin"));
            r.Sub["heat"] = Sub("heat", r.HeatInsulation + Offset(a, "ComfyTemperatureMax"));
            void Opt(string key, float v) { if (v > 0.0001f) r.Sub[key] = Sub(key, v); }
            float tox = Offset(a, "ToxicEnvironmentResistance") + Offset(a, "ToxicResistance") + (a.ImmuneToToxGas ? 0.5f : 0f);
            Opt("toxins", tox);
            float vac = Offset(a, "VacuumResistance");
            foreach (var p in a.Powers) if (p.Kind == "oxygen") vac += 0.5f;
            Opt("vacuum", vac);
            Opt("psychic", r.Areas["psychic"]);
            Opt("psychic shelter", Math.Max(0f, -Sensitivity(a)));
            Opt("work", r.Areas["work"]);
            Opt("social", r.Areas["social"]);
            Opt("mechanitor", r.Areas["mech"]);
            Opt("magic", r.Areas["magic"]);
            Opt("hunting", r.Areas["hunting"]);
            Opt("carrying", r.Areas["carry"]);

            // Situations: the sub-ratings, the fight as a shooter and as a melee fighter, and the overall in the cold and the heat.
            foreach (var kv in r.Sub) r.Situations[kv.Key] = kv.Value;
            r.Situations["for a shooter"] = BlocksShooting(a) ? 1f : Sub("shooter", r.FightShooter - 1f);
            r.Situations["for a melee fighter"] = Sub("melee fighter", r.FightMelee - 1f);
            r.Situations["in the cold"] = Scale(WorthIn(r.Areas, r.Role, "cold"), mid, width);
            r.Situations["in the heat"] = Scale(WorthIn(r.Areas, r.Role, "heat"), mid, width);
            r.Situations["while working"] = Scale(r.Areas["work"], SubScale("work", slot).mid, SubScale("work", slot).width);
            r.Situations["for social or trade"] = Scale(r.Areas["social"], SubScale("social", slot).mid, SubScale("social", slot).width);

            Specials(a, r);
            BestAndWeak(a, r);
            r.Skill = r.Areas["mech"] > 0.05f ? "mechanitor" : r.Areas["psychic"] > 0.05f ? "psycaster" : r.Areas["magic"] > 0.05f ? "mage"
                : meleeOnly ? "Melee" : shooterOnly || Offset(a, "ShootingAccuracyPawn") > 0f || Offset(a, "AimingDelayFactor") < 0f ? "Shooting"
                : r.Areas["social"] > 0.03f ? "Social" : r.Areas["work"] > 0.03f ? "work" : "";
            return r;
        }

        /// <summary>A piece rated in its stuff, its role judged in plain stuff (the same piece made of cloth, steel...: <see cref="RoleOf"/>).</summary>
        public static ApparelRating Rate(ApparelFacts a, ApparelFacts plain) => Rate(a, plain == null ? null : Rate(plain).Role);

        /// <summary>The pooled toughness of some families (melee people and animals for the melee sub-rating).</summary>
        static float Pool(Dictionary<string, float> t, params string[] fams)
        {
            float bare = 0f, taken = 0f;
            foreach (var f in fams)
            {
                float b = FamilyWeights[f] * FamilyBare(f);
                bare += b;
                taken += b / Math.Max(t[f], 0.01f);
            }
            return taken > 0f ? bare / taken : 1f;
        }

        /// <summary>The worth by a role's weights.</summary>
        public static float WorthFor(Dictionary<string, float> areas, string role)
        {
            var w = RoleWeights(role);
            float s = 0f;
            foreach (var kv in areas) s += (w.TryGetValue(kv.Key, out var x) ? x : 0f) * kv.Value;
            return s;
        }

        /// <summary>The worth in a cold or hot climate: that kind of insulation counts in full on top of the role's weight, the other not at all.</summary>
        public static float WorthIn(Dictionary<string, float> areas, string role, string climate)
        {
            var w = RoleWeights(role);
            float s = 0f;
            foreach (var kv in areas)
            {
                float x = w.TryGetValue(kv.Key, out var v) ? v : 0f;
                if (kv.Key == climate) x += 1f;
                else if (kv.Key == "cold" || kv.Key == "heat") x = 0f;
                s += x * kv.Value;
            }
            return s;
        }

        static void Specials(ApparelFacts a, ApparelRating r)
        {
            void Add(string key, string label, bool counted, int sign = 1) => r.Specials.Add(new ApparelSpecial { Key = key, Label = label, Counted = counted, Sign = sign });
            string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var s in a.Shields)
                Add("shield", s.Label + ": absorbs " + F(s.Capacity) + " damage of shots and blasts, recharging " + F(s.Recharge) + " a second" +
                    (s.BlocksShooting ? "; its wearer can't shoot out" : "") + (s.Field ? "; covers those beside its wearer too" : ""), true, s.BlocksShooting ? 0 : 1);
            if (a.OffHandCovers != null)
                Add("offhand", "an off-hand shield: its armour stops hits on the " + string.Join(", ", a.OffHandCovers).ToLowerInvariant() +
                    " first, while its wearer holds a weapon under 1.65 kg that Dual Wield allows in the off hand", true);
            foreach (var p in a.Powers)
            {
                string uses = p.Charges > 0 ? ", " + p.Charges + (p.Charges == 1 ? " use" : " uses") + (p.RunsOut ? ", then gone" : "") : "";
                Add("power:" + p.Kind, (p.Label != "" ? p.Label : p.Kind) + uses + (p.Note != "" ? ": " + p.Note : ""), p.Counted);
            }
            foreach (var kv in a.Offsets)
            {
                if (kv.Value == 0f || kv.Key == "SlaveSuppressionOffset") continue;
                bool fight = FightStats.Contains(kv.Key);
                bool known = fight || StatWorth.ContainsKey(kv.Key) || kv.Key.StartsWith("Psychic") || kv.Key == "ComfyTemperatureMin" || kv.Key == "ComfyTemperatureMax";
                int sign = kv.Value > 0f ? 1 : -1;
                if (kv.Key == "MentalBreakThreshold" || kv.Key == "AimingDelayFactor" || kv.Key == "IncomingDamageFactor" || kv.Key == "FoodPoisonChance"
                    || kv.Key == "RestFallRateFactor" || kv.Key == "ComfyTemperatureMin") sign = -sign;
                Add("stat:" + kv.Key, kv.Key + " " + (kv.Value > 0 ? "+" : "") + F(kv.Value) + " while worn", known, sign);
            }
            foreach (var kv in a.Factors)
                if (kv.Value != 1f) Add("factor:" + kv.Key, kv.Key + " x" + F(kv.Value) + " while worn", true, kv.Value > 1f ? 1 : -1);
            if (a.Offsets.TryGetValue("SlaveSuppressionOffset", out var ss) && ss != 0f)
                Add("stat:SlaveSuppressionOffset", "slave suppression " + (ss > 0 ? "+" : "") + F(ss) + " on a slave who wears it", false, ss > 0 ? 1 : -1);
            foreach (var (cap, off, fac, max) in a.Capacities)
                Add("capacity:" + cap, cap.ToLowerInvariant() + (max >= 0f ? " held at " + F(max) : off != 0f ? " " + (off > 0 ? "+" : "") + F(off) : " x" + F(fac)), true,
                    off > 0f || fac > 1f ? 1 : -1);
            if (a.PainOffset != 0f) Add("pain", "pain " + (a.PainOffset > 0 ? "+" : "") + F(a.PainOffset), true, a.PainOffset > 0 ? -1 : 1);
            if (a.BleedFactor != 1f) Add("bleed", "bleeding x" + F(a.BleedFactor), false, a.BleedFactor < 1f ? 1 : -1);
            if (a.Enchantment.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in a.Enchantment) parts.Add(kv.Key + " " + (kv.Value > 0 ? "+" : "") + F(kv.Value));
                Add("magic", "enchanted: " + string.Join(", ", parts), true, MagicWorth(a) >= 0f ? 1 : -1);
            }
            foreach (var n in a.Notes) Add("note", n, false, 0);
            if (a.Camouflage) Add("camouflage", "camouflage: its wearer is hard to see while still", true);
            if (!string.IsNullOrEmpty(a.WorkDisables)) Add("workdisables", "its wearer can't do " + a.WorkDisables.ToLowerInvariant() + " work", true, -1);
            if (a.SkillGainZero) Add("noskill", "its wearer learns nothing while wearing it", true, -1);
            if (a.PreventsBleeding) Add("nobleed", "its wearer doesn't bleed", true);
            if (a.Biocodable) Add("biocodable", "can be biocoded to one person", false, 0);
        }

        static void BestAndWeak(ApparelFacts a, ApparelRating r)
        {
            var s = r.Situations;
            void B(string k, string text, float at = 7f) { if (s.TryGetValue(k, out var v) && v >= at) r.BestFor.Add(text); }
            void W(string k, string text, float at = 2.5f) { if (s.TryGetValue(k, out var v) && v <= at) r.WeakWhen.Add(text); }
            B("guns", "gunfights");
            B("melee", "melee and animals");
            B("mechanoids", "mechanoid raids");
            B("fire", "fire and incendiaries");
            B("cold", "the cold");
            B("heat", "the heat");
            B("toxins", "toxic fallout and gas", 6f);
            B("vacuum", "vacuum and space", 6f);
            B("psychic", "psycasters", 6f);
            B("psychic shelter", "psychic drones and lances", 6f);
            B("work", "working", 6f);
            B("social", "talks and trade", 6f);
            B("mechanitor", "mechanitors", 6f);
            B("magic", "mages", 6f);
            B("hunting", "hunting and stealth", 6f);
            B("for a melee fighter", "melee fighters", 7.5f);
            B("for a shooter", "shooters", 7.5f);
            if (r.Role == "body armour" || r.Role == "helmet" || r.Role == "off-hand shield")
            {
                W("guns", "under heavy fire", 3f);
                W("mechanoids", "against mechanoids", 3f);
                W("melee", "in melee", 3f);
                if (r.Cold < 10f && r.Slot == "body") r.WeakWhen.Add("in the cold");
            }
            if (r.Role == "cold-weather gear" && r.HeatInsulation < 2f) r.WeakWhen.Add("in the heat");
            if (r.Role == "hot-weather gear" && r.Cold < 8f) r.WeakWhen.Add("in the cold");
            if (r.For == "a melee fighter" && r.OverallShooter <= 2.5f && r.OverallMelee - r.OverallShooter >= 1f) r.WeakWhen.Add("for a shooter");
            if (r.For == "a shooter" && r.OverallMelee <= 2.5f && r.OverallShooter - r.OverallMelee >= 1f) r.WeakWhen.Add("for a melee fighter");
            if (Feet(a).melee < 0.92f) r.WeakWhen.Add("chasing or fleeing");
            if (Sensitivity(a) < -0.2f) r.WeakWhen.Add("for a psycaster");
        }

        // ---------------------------------------------------------------- reading the plain stuff, for the role

        /// <summary>The plain stuff a piece's role is judged in: cloth, plain leather, steel, wood, granite (so its role doesn't hang on a costly stuff).</summary>
        public static readonly string[] PlainStuffs = { "Cloth", "Leather_Plain", "Steel", "WoodLog", "BlocksGranite" };
    }
}
