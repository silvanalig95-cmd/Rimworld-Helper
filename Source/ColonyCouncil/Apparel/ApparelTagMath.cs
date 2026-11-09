using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>
    /// Tags for armour and apparel, in the weapons' six categories, colours and symbols (WeaponTagMath.Categories: one
    /// table for both): a fixed vocabulary, each tag given by a rule on the piece's own numbers with a line saying why, the
    /// most important few shown (at most seven, keeping its role, its first strength and its first weakness:
    /// WeaponTagMath.Pick). And the piece in plain words, from its rating and tags.
    /// </summary>
    public static class ApparelTagMath
    {
        static TagDef D(string id, string label, TagCategory c, int importance, string rule) =>
            new TagDef { Id = id, Label = label, Category = c, Importance = importance, Rule = rule };

        const TagCategory S = TagCategory.Strength, W = TagCategory.Weakness, R = TagCategory.Risk, X = TagCategory.Special,
            O = TagCategory.Role, N = TagCategory.Needs;

        /// <summary>The apparel vocabulary: every tag, its category, its importance and its rule in words (the legend).</summary>
        public static readonly TagDef[] ApparelTags =
        {
            D("role-armour", "Combat armour", O, 95, "body armour: sharp armour of 40% or more as made, or a fifth longer in a fight"),
            D("role-helmet", "Helmet", O, 95, "on the head, with armour that matters (sharp 40% or more, or a tenth longer in a fight)"),
            D("role-headgear", "Headgear", O, 95, "on the head, for warmth, shade, looks or work"),
            D("role-shield", "Off-hand shield", O, 95, "carried in the off hand (Vanilla Expanded's shields)"),
            D("role-utility", "Utility", O, 95, "a belt, pack or accessory"),
            D("role-everyday", "Everyday wear", O, 95, "clothing for every day: no real armour, no strong insulation one way"),
            D("role-cold", "Cold-weather", O, 95, "its cold insulation in plain stuff is 14 °C or more and a third above its heat insulation"),
            D("role-hot", "Hot-weather", O, 95, "its heat insulation in plain stuff is 14 °C or more and a third above its cold insulation"),
            D("role-hazard", "Hazard gear", O, 95, "a body piece whose toxic and vacuum worth is 5% or more and above its fight worth"),
            D("role-ceremonial", "Ceremonial", O, 95, "worn for a title, a faith or its looks more than for a fight"),
            D("role-psychic", "Psycaster's", O, 95, "its psychic or magic worth outweighs its fight"),
            D("role-royal", "Royal", O, 70, "meets a royal title's apparel rule"),
            D("role-faith", "Faith's apparel", O, 60, "a faith's role or meme asks for it"),
            D("role-worker", "Work clothes", O, 66, "makes its wearer's work 5% more or better"),
            D("role-trader", "Trader's", O, 62, "makes its wearer's talks and trade 5% better or more"),
            D("role-medic", "Medic's", O, 60, "better tending or surgery"),
            D("role-hunter", "Hunter's", O, 60, "hunting stealth 20% or more, or camouflage"),
            D("role-melee", "For melee fighters", O, 94, "rated for a melee fighter: it blocks shooting, is an off-hand shield, or does a tenth more for a melee fighter than for a shooter (how much longer they last times how much more they deal, the walk aside)"),
            D("role-spacer", "Spacefarer's", O, 62, "vacuum resistance of 30% or more, or an oxygen supply"),
            D("role-slave", "For slaves", O, 58, "slave apparel, or raises a slave's suppression"),
            D("role-shooter", "For shooters", O, 94, "rated for a shooter: it does a tenth more for a shooter than for a melee fighter (how much longer they last times how much more they deal, the walk aside)"),
            D("role-armour-piece", "Made as armour", O, 80, "not rated as body armour or a helmet, but made as armour (its stuff's armour counts at 40% or more): named for what it covers, armour for the hands, the feet or the head, an armoured cloak"),
            D("role-builder", "Builder's", O, 61, "better construction, smoothing or repairs"),
            D("role-researcher", "Researcher's", O, 61, "faster research or entity study"),
            D("role-cook", "Cook's", O, 61, "faster cooking or butchering, less food poisoning"),
            D("role-grower", "Grower's", O, 61, "faster plant work or a bigger harvest"),
            D("role-handler", "Handler's", O, 61, "faster or bigger animal gathering"),
            D("role-miner", "Miner's", O, 61, "faster mining"),
            D("role-top", "Top", O, 50, "covers the torso, not the legs"),
            D("role-legs", "Legwear", O, 50, "covers the legs, not the torso"),
            D("role-outfit", "Full outfit", O, 50, "covers the torso and the legs"),
            D("role-coat", "Coat", O, 52, "an outer layer over the torso"),
            D("role-feet", "Footwear", O, 50, "covers the feet"),
            D("role-hands", "Handwear", O, 50, "covers the hands"),
            D("role-cloak", "Cloak", O, 50, "an outer layer over the neck and shoulders only"),
            D("role-hat", "Hat", O, 50, "worn on the upper head"),
            D("role-mask", "Mask or hood", O, 50, "covers the face, the mouth or the whole head"),
            D("role-eyewear", "Eyewear", O, 50, "worn over the eyes"),
            D("role-looks", "Worn for looks", O, 48, "it does little else: worth under 2% of a colonist in every area, and not made as armour"),
            D("stops-bullets", "Stops bullets", S, 88, "rates 7 or more against guns"),
            D("turns-blades", "Turns blades", S, 86, "rates 7 or more against blades, fists and animals"),
            D("holds-mechs", "Holds off mechs", S, 84, "rates 7 or more against mechanoids"),
            D("fireproof", "Fireproof", S, 80, "rates 7 or more against fire"),
            D("warm", "Warm", S, 82, "cold insulation of 20 °C or more on a coat or armour, 10 °C next to the skin, 6 °C on the head"),
            D("cool", "Cool", S, 80, "heat insulation of 15 °C or more on a coat or armour, 10 °C next to the skin, 6 °C on the head"),
            D("light", "Light", S, 70, "armour of 5 kg or less that doesn't slow its wearer"),
            D("full-cover", "Full cover", S, 74, "armour over 80% or more of where shots land"),
            D("quick", "Quick feet", S, 72, "speeds its wearer up"),
            D("aim", "Steadier aim", S, 72, "better shooting accuracy or quicker aim (with both a gain and a cost, by what a shooter deals in the end)"),
            D("melee-edge", "Melee edge", S, 70, "better melee hit, damage or dodge"),
            D("painproof", "Shrugs off pain", S, 70, "pain shock threshold up by 10% or more"),
            D("steady", "Steady nerves", S, 68, "a lower mental break threshold"),
            D("hard-wearing", "Hard-wearing", S, 56, "armour with 300 hit points or more"),
            D("carries-more", "Carries more", S, 64, "raises what its wearer can carry"),
            D("heals", "Heals faster", S, 62, "faster immunity or wound healing"),
            D("impression", "Good impression", S, 58, "raises social impact or negotiation"),
            D("slows", "Slows you", W, 84, "slows its wearer"),
            D("blocks-shooting", "Blocks shooting", W, 90, "its shield stops its wearer's own shots"),
            D("blinds", "Blinds", W, 92, "takes its wearer's sight"),
            D("thin-cover", "Thin cover", W, 76, "armour over less than half of where shots land"),
            D("weak-blunt", "Weak to blunt", W, 72, "armour whose blunt rating is under a third of its sharp: fists, clubs and paws get through"),
            D("mechs-pierce", "Mechs pierce it", W, 70, "armour that rates 2 or more lower against mechanoids than against guns"),
            D("no-warmth", "No warmth", W, 64, "body armour with under 10 °C of cold insulation: useless in a blizzard"),
            D("worse-aim", "Spoils your aim", W, 80, "lower shooting accuracy or slower aim (first among the weaknesses on a piece for melee fighters)"),
            D("clumsy", "Clumsy in melee", W, 74, "lower melee hit or dodge (first among the weaknesses on a piece for shooters)"),
            D("slows-work", "Slows work", W, 76, "lower work speed"),
            D("disables-work", "Bars work", W, 82, "its wearer can't do some work, or learns nothing"),
            D("off-putting", "Off-putting", W, 64, "lowers social impact or negotiation"),
            D("blocks-psycasts", "Blocks psycasts", W, 70, "lowers psychic sensitivity: bad for a psycaster"),
            D("flammable", "Flammable", W, 60, "burns readily: flammability of 100% or more and heat armour under 20%"),
            D("little-warmth", "Little warmth", W, 54, "clothing, a helmet or other headgear with under 4 °C of insulation either way (5 °C on the head)"),
            D("no-armour", "Won't stop a bullet", W, 62, "clothing over a fifth or more of where shots land that rates 2.5 or less against guns"),
            D("wears-fast", "Wears fast", R, 66, "under 60 days of wear to half its hit points, when the tattered mood starts"),
            D("tatters", "Tattered mood", R, 70, "body armour over 30% or more of where shots land, under 200 hit points: a quarter of each hit's damage wears it, so a few fights tatter it"),
            D("runs-out", "Runs out", R, 74, "gone when its charges are used"),
            D("angers", "Angers factions", R, 72, "using it on someone costs goodwill with their faction"),
            D("hurts", "Hurts its wearer", R, 84, "pain while worn"),
            D("gendered", "Wrong-gender mood", R, 58, "made for one gender: the other is unhappy wearing it"),
            D("naked", "Counts as naked", R, 62, "covers the torso or legs but doesn't count as clothing"),
            D("explodes", "Explodes on death", R, 68, "blows up when its wearer dies"),
            D("calls-enemies", "Calls enemies", R, 80, "brings hostile mechanoids down where it is aimed"),
            D("shield", "Shield", X, 90, "an energy shield that takes shots and blasts"),
            D("shields-allies", "Shields allies", X, 80, "a shield field or low-shield that covers those beside its wearer"),
            D("jump", "Jump", X, 88, "jumps over walls and enemies"),
            D("smoke", "Smoke", X, 84, "pops a smoke cloud that spoils shots"),
            D("lowshield", "Low shield", X, 84, "deploys a low shield that stops shots from outside for 30 seconds"),
            D("firefoam", "Firefoam", X, 78, "puts out fires around its wearer"),
            D("grenades", "Grants: (ability)", X, 84, "throws or launches something, named"),
            D("lance", "Psychic lance", X, 86, "shocks or maddens one enemy from afar"),
            D("strike", "Orbital strike", X, 86, "calls a strike or a tornado from orbit"),
            D("summon", "Summons help", X, 80, "deploys a turret, a drone or the risen dead"),
            D("psychic", "Psychic", X, 78, "raises psychic sensitivity, psychic heat or focus"),
            D("shelter", "Psychic shield", X, 80, "lowers psychic sensitivity: shelters from drones and lances"),
            D("vacuum-proof", "Vacuum-proof", X, 82, "vacuum resistance of 60% or more, or an oxygen supply"),
            D("toxin-proof", "Toxin-proof", X, 78, "toxic resistance of 50% or more, or immune to tox gas"),
            D("bandwidth", "Mech bandwidth", X, 82, "adds a mechanitor's bandwidth or control groups"),
            D("enchanted", "Enchanted", X, 80, "an enchantment of A RimWorld of Magic"),
            D("camouflage", "Camouflage", X, 76, "hides its wearer while still"),
            D("second-life", "Second life", X, 90, "brings its wearer back from death once"),
            D("no-bleed", "Stops bleeding", X, 74, "its wearer bleeds less or not at all"),
            D("wards", "Wards the body", X, 76, "adds armour to its wearer's whole body"),
            D("extra-gun", "Extra gun", X, 82, "a gun of its own that fires beside its wearer"),
            D("own-effect", "Its own effect", X, 58, "part of what it does is its mod's own code: named, not counted"),
            D("needs-title", "Needs a title", N, 72, "royal apparel: worth most to someone whose title asks for it"),
            D("needs-psylink", "Needs a psylink", N, 80, "its psychic gains need a psycaster"),
            D("mechanitor-only", "Mechanitor only", N, 86, "its bandwidth needs a mechanitor"),
            D("needs-mage", "Needs a mage", N, 84, "its enchantment needs a mage or a might user"),
            D("one-gender", "Women only", N, 76, "only one gender can wear it (or is meant to)"),
            D("children-only", "Children only", N, 88, "made for children"),
            D("one-handed", "Light weapon only", N, 82, "works only with a weapon under 1.65 kg that Dual Wield allows in the off hand"),
            D("research", "Advanced research", N, 45, "made only after spacer or ultra research"),
            D("hard-to-get", "Hard to find", N, 89, "no recipe in the player's game makes it and no faction's people wear it"),
            D("biocoded", "Biocodable", N, 40, "can be biocoded to one colonist"),
            D("good-stuff", "Stuff matters", N, 50, "its rating moves by 1.5 or more between the common stuffs it can be made of"),
            D("needs-ammo", "Needs (fuel)", N, 52, "its charges are refilled with something the colony must make or buy, named"),
            D("slot", "Belt slot", N, 30, "takes a utility slot (a belt, an artifact, a hip): one piece in it at a time"),
        };

        public static TagDef Def(string id)
        {
            foreach (var d in ApparelTags) if (d.Id == id) return d;
            return null;
        }

        static readonly Dictionary<string, string> RoleTag = new Dictionary<string, string>
        {
            ["body armour"] = "role-armour", ["helmet"] = "role-helmet", ["headgear"] = "role-headgear", ["off-hand shield"] = "role-shield",
            ["utility"] = "role-utility", ["everyday clothing"] = "role-everyday", ["cold-weather gear"] = "role-cold",
            ["hot-weather gear"] = "role-hot", ["hazard gear"] = "role-hazard", ["ceremonial or psychic"] = "role-ceremonial",
        };

        /// <summary>
        /// A piece's tags from its rating and facts: every rule that holds, ordered by importance, at most <paramref name="max"/>
        /// of them, keeping its role, its first strength and its first weakness (the weapons' own Pick).
        /// </summary>
        public static List<ThingTag> For(ApparelRating r, ApparelFacts a, int max = 7, Dictionary<string, float> byStuff = null)
        {
            var all = new List<ThingTag>();
            void Add(string id, string why, string label = null, int bump = 0)
            {
                var d = Def(id);
                if (d == null) return;
                foreach (var t in all) if (t.Id == id) return;
                all.Add(new ThingTag { Id = id, Label = label ?? d.Label, Category = d.Category, Why = why, Importance = d.Importance + bump });
            }
            string F0(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            string P(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
            float Off(string s) => ApparelRateMath.Offset(a, s);
            bool combat = r.Role == "body armour" || r.Role == "helmet" || r.Role == "off-hand shield";
            bool head = r.Slot == "head";

            // ---- Role ----
            string roleId = RoleTag.TryGetValue(r.Role, out var rid) ? rid : "role-everyday";
            if (r.Role == "ceremonial or psychic" && r.Areas["psychic"] + r.Areas["magic"] > r.Areas["social"]) roleId = "role-psychic";
            string piece = ArmourPiece(r, a);
            if (piece != null)
            {
                float mult = ApparelRateMath.Stat(a.StatBases, "StuffEffectMultiplierArmor");
                bool clothes = r.Role == "everyday clothing" || r.Role == "headgear";
                string why = (mult > 0f ? "its stuff's armour counts at " + P(mult) + (clothes ? " (" + (r.Slot == "head" ? "a helmet's" : "body armour's") + " line is 45%)" : "") + ": " : "") +
                    P(r.Sharp) + " sharp" + (a.StuffLabel != "" ? " in " + a.StuffLabel : "") +
                    (r.Slot == "body" && r.Coverage < 0.05f ? ", over only " + P(r.Coverage) + " of where shots land" : "") +
                    (clothes ? "" : "; rated with " + RoleNoun(r.Role) + " for what it does most");
                // Everyday clothing that is made as armour is named for what it is, in the role tag's place.
                Add("role-armour-piece", why, piece, r.Role == "everyday clothing" ? 15 : 0);
            }
            if (piece == null || r.Role != "everyday clothing") Add(roleId, roleId == "role-psychic" ? "its psychic or magic worth outweighs its fight" : RoleWhy(r));
            string both = "rates " + R1(r.OverallMelee) + " for a melee fighter, " + R1(r.OverallShooter) + " for a shooter";
            if (r.For == "a melee fighter")
                Add("role-melee", (ApparelRateMath.BlocksShooting(a) ? "its shield blocks shooting" : a.OffHandCovers != null ? "an off-hand shield, for a sword or a pistol"
                    : Off("ShootingAccuracyPawn") < 0f ? "shooting accuracy " + F0(Off("ShootingAccuracyPawn")) + " levels"
                    : Off("AimingDelayFactor") > 0f ? "aims " + P(Off("AimingDelayFactor")) + " slower"
                    : Off("MeleeHitChance") > 0f ? "melee hit +" + F0(Off("MeleeHitChance")) + " levels"
                    : Off("MeleeDodgeChance") > 0f ? "melee dodge +" + F0(Off("MeleeDodgeChance")) + " levels"
                    : Off("MeleeDamageFactor") > 0f ? "melee damage +" + P(Off("MeleeDamageFactor"))
                    : a.Powers.Exists(p => p.Kind == "jump" && p.Counted) ? "a jump closes the distance"
                    : P((float)Math.Exp(r.Lean) - 1f) + " more for a melee fighter") + ": " + both);
            if (r.For == "a shooter")
                Add("role-shooter", (Off("ShootingAccuracyPawn") > 0f ? "shooting accuracy +" + F0(Off("ShootingAccuracyPawn")) + " levels"
                    : Off("AimingDelayFactor") < 0f ? "aims " + P(-Off("AimingDelayFactor")) + " faster"
                    : ApparelRateMath.RangedCooldownFactorOf(a) < 1f || Off("RangedCooldownFactor") < 0f ? "a shorter cooldown between shots"
                    : Off("MeleeHitChance") < 0f ? "melee hit " + F0(Off("MeleeHitChance")) + " levels"
                    : Off("MeleeDodgeChance") < 0f ? "melee dodge " + F0(Off("MeleeDodgeChance")) + " levels"
                    : P((float)Math.Exp(-r.Lean) - 1f) + " more for a shooter") + ": " + both);
            if (a.Titles.Count > 0)
            {
                var lowest = a.Titles[0];
                foreach (var t in a.Titles) if (t.seniority < lowest.seniority) lowest = t;
                string article = lowest.label.Length > 0 && "aeiouAEIOU".IndexOf(lowest.label[0]) >= 0 ? "an " : "a ";
                Add("role-royal", "meets the apparel rule of " + article + lowest.label + (a.Titles.Count > 1 ? " and higher titles" : ""));
            }
            if (a.Faiths.Count > 0) Add("role-faith", "asked for by " + a.Faiths[0] + (a.Faiths.Count > 1 ? " and " + (a.Faiths.Count - 1) + " more" : ""));
            if (r.Areas["work"] >= 0.05f) Add("role-worker", "work about " + P(r.Areas["work"]) + " better");
            if (r.Areas["social"] >= 0.05f) Add("role-trader", "talks and trade about " + P(r.Areas["social"]) + " better");
            if (Off("MedicalTendQuality") > 0f || Off("MedicalSurgerySuccessChance") > 0f) Add("role-medic", "tending +" + P(Off("MedicalTendQuality")) + ", surgery +" + P(Off("MedicalSurgerySuccessChance")));
            if (Off("HuntingStealth") >= 0.2f || a.Camouflage) Add("role-hunter", a.Camouflage ? "camouflage" : "hunting stealth +" + P(Off("HuntingStealth")));
            float vac = Off("VacuumResistance");
            bool oxygen = a.Powers.Exists(p => p.Kind == "oxygen");
            if (vac >= 0.3f || oxygen) Add("role-spacer", oxygen ? "carries its own oxygen" : "vacuum resistance " + P(vac));
            if (a.SlaveApparel || Off("SlaveSuppressionOffset") > 0f) Add("role-slave", a.SlaveApparel ? "slave apparel" : "slave suppression +" + P(Off("SlaveSuppressionOffset")));
            foreach (var (id, stats) in WorkKinds)
            {
                string best = null;
                foreach (var s in stats) if (s.StartsWith("-") ? Off(s.Substring(1)) < 0f : Off(s) > 0f) { best = s.TrimStart('-'); break; }
                if (best != null) Add(id, best + " " + (Off(best) > 0 ? "+" : "") + P(Off(best)));
            }
            if (r.Slot == "body" && !combat && r.Role != "utility")
            {
                bool torso = a.Groups.Contains("Torso"), legs = a.Groups.Contains("Legs");
                bool shell = a.Layers.Contains("Shell") || a.Layers.Contains("VFEC_OuterShell") || a.Layers.Contains("TM_Cloak");
                if (shell && torso) Add("role-coat", "worn over the rest, on the " + string.Join(", ", a.Layers).ToLowerInvariant() + " layer");
                else if (shell && !torso && !legs) Add("role-cloak", "over the " + string.Join(", ", a.Groups).ToLowerInvariant());
                if (torso && legs) Add("role-outfit", "covers the torso and the legs");
                else if (torso) Add("role-top", "covers the torso");
                else if (legs) Add("role-legs", "covers the legs");
                else if (a.Groups.Contains("Feet")) Add("role-feet", "covers the feet");
                else if (a.Groups.Contains("Hands") || a.Groups.Contains("LeftHand") || a.Groups.Contains("RightHand")) Add("role-hands", "covers the hands");
            }
            if (r.Slot == "head" && !combat)
            {
                if (a.Groups.Contains("FullHead") || a.Groups.Contains("Mouth") || a.Groups.Contains("Teeth")) Add("role-mask", "covers " + string.Join(", ", a.Groups).ToLowerInvariant());
                else if (a.Groups.Contains("UpperHead")) Add("role-hat", "on the upper head");
                else if (a.Groups.Contains("Eyes")) Add("role-eyewear", "over the eyes");
            }

            // ---- Strengths ----
            float sGuns = r.Sub["guns"], sMelee = r.Sub["melee"], sMech = r.Sub["mechanoids"], sFire = r.Sub["fire"];
            if (sGuns >= 7f) Add("stops-bullets", "rates " + F0(sGuns) + " against guns: lasts " + F0(r.Toughness["guns"]) + " times as long");
            if (sMelee >= 7f) Add("turns-blades", "rates " + F0(sMelee) + " against blades, fists and animals");
            if (sMech >= 7f) Add("holds-mechs", "rates " + F0(sMech) + " against mechanoids");
            if (sFire >= 7f) Add("fireproof", "rates " + F0(sFire) + " against fire: heat armour " + P(r.Heat));
            bool skin = a.Layers.Count > 0 && a.Layers.TrueForAll(l => l == "OnSkin");
            float warmAt = head ? 6f : skin ? 10f : 20f, coolAt = head ? 6f : skin ? 10f : 15f;
            if (r.Cold - Off("ComfyTemperatureMin") >= warmAt) Add("warm", "keeps out " + F0(r.Cold - Off("ComfyTemperatureMin")) + " °C of cold");
            if (r.HeatInsulation + Off("ComfyTemperatureMax") >= coolAt) Add("cool", "keeps out " + F0(r.HeatInsulation + Off("ComfyTemperatureMax")) + " °C of heat");
            float mass = ApparelRateMath.Stat(a.StatBases, "Mass");
            float move = Off("MoveSpeed");
            if (combat && mass > 0f && mass <= 5f && move >= 0f && r.Sharp >= 0.4f) Add("light", F0(mass) + " kg, no slower walk");
            if (combat && !head && r.Coverage >= 0.8f) Add("full-cover", "covers " + P(r.Coverage) + " of where shots land");
            if (move > 0f) Add("quick", "+" + F0(move) + " cells a second");
            // Aim up and aim down on one piece (a ghillie hood: a little more accurate, slower to aim) is one tag, by what a shooter deals.
            bool aimUp = Off("ShootingAccuracyPawn") > 0f || Off("AimingDelayFactor") < 0f || ApparelRateMath.RangedCooldownFactorOf(a) < 1f;
            bool aimDown = Off("ShootingAccuracyPawn") < 0f || Off("AimingDelayFactor") > 0f;
            string aimBoth = "";
            if (aimUp && aimDown)
            {
                float shot = ApparelRateMath.Output(a).shooter;
                aimBoth = "shooting accuracy " + (Off("ShootingAccuracyPawn") > 0f ? "+" : "") + F0(Off("ShootingAccuracyPawn")) + " levels, aims " +
                    P(Math.Abs(Off("AimingDelayFactor"))) + (Off("AimingDelayFactor") > 0f ? " slower" : " faster") + ": a shooter deals x" + shot.ToString("0.00", CultureInfo.InvariantCulture);
                if (shot >= 1f) aimDown = false; else aimUp = false;
            }
            if (aimUp)
                Add("aim", aimBoth != "" ? aimBoth : Off("ShootingAccuracyPawn") > 0f ? "shooting accuracy +" + F0(Off("ShootingAccuracyPawn")) + " levels" :
                    Off("AimingDelayFactor") < 0f ? "aims " + P(-Off("AimingDelayFactor")) + " faster" : "shorter cooldown between shots");
            if (Off("MeleeHitChance") > 0f || Off("MeleeDamageFactor") > 0f || Off("MeleeDodgeChance") > 0f)
                Add("melee-edge", Off("MeleeDodgeChance") > 0f ? "melee dodge +" + F0(Off("MeleeDodgeChance")) + " levels" :
                    Off("MeleeHitChance") > 0f ? "melee hit +" + F0(Off("MeleeHitChance")) + " levels" : "melee damage +" + P(Off("MeleeDamageFactor")));
            if (Off("PainShockThreshold") >= 0.1f) Add("painproof", "pain shock threshold +" + P(Off("PainShockThreshold")));
            if (Off("MentalBreakThreshold") < 0f) Add("steady", "mental break threshold " + P(Off("MentalBreakThreshold")));
            float hp = ApparelRateMath.Stat(a.StatBases, "MaxHitPoints", 100f);
            if (combat && hp >= 300f) Add("hard-wearing", F0(hp) + " hit points");
            if (Off("SocialImpact") > 0f || Off("NegotiationAbility") > 0f)
                Add("impression", Off("SocialImpact") > 0f ? "social impact +" + P(Off("SocialImpact")) : "negotiation +" + P(Off("NegotiationAbility")));
            if (r.Areas["carry"] > 0.005f) Add("carries-more", Off("CarryingCapacity") > 0f ? "+" + F0(Off("CarryingCapacity")) + " kg carried" : "+" + F0(Off("VEF_MassCarryCapacity")) + " kg in a caravan");
            if (Off("ImmunityGainSpeed") > 0f || Off("InjuryHealingFactor") > 0f)
                Add("heals", Off("ImmunityGainSpeed") > 0f ? "immunity +" + P(Off("ImmunityGainSpeed")) : "healing +" + P(Off("InjuryHealingFactor")));

            // ---- Weaknesses ----
            if (move < 0f) Add("slows", "slows its wearer by " + F0(-move) + " cells a second (" + P(-move / ApparelRateMath.BaseMoveSpeed) + ")");
            if (ApparelRateMath.BlocksShooting(a)) Add("blocks-shooting", "its wearer can't shoot while it is on: " + R1(r.OverallShooter) + " for a shooter");
            if (ApparelRateMath.CapacityFactor(a, "Sight") < 0.5f) Add("blinds", "sight held at " + P(ApparelRateMath.CapacityFactor(a, "Sight")));
            if (combat && !head && r.Coverage < 0.5f && a.OffHandCovers == null) Add("thin-cover", "covers only " + P(r.Coverage) + " of where shots land");
            if (combat && r.Sharp >= 0.4f && r.Blunt < r.Sharp / 3f) Add("weak-blunt", "blunt armour " + P(r.Blunt) + " against sharp " + P(r.Sharp));
            if (combat && sGuns - sMech >= 2f) Add("mechs-pierce", "rates " + F0(sMech) + " against mechanoids, " + F0(sGuns) + " against guns");
            if (r.Role == "body armour" && r.Cold < 10f) Add("no-warmth", "only " + F0(r.Cold) + " °C of cold insulation");
            if (aimDown)
                Add("worse-aim", (aimBoth != "" ? aimBoth : Off("ShootingAccuracyPawn") < 0f ? "shooting accuracy " + F0(Off("ShootingAccuracyPawn")) + " levels" : "aims " + P(Off("AimingDelayFactor")) + " slower") +
                    (r.For == "a melee fighter" ? ": " + R1(r.OverallShooter) + " for a shooter" : ""), null, r.For == "a melee fighter" ? 13 : 0);
            if (Off("MeleeHitChance") < 0f || Off("MeleeDodgeChance") < 0f)
                Add("clumsy", (Off("MeleeDodgeChance") < 0f ? "melee dodge " + F0(Off("MeleeDodgeChance")) + " levels" : "melee hit " + F0(Off("MeleeHitChance")) + " levels") +
                    (r.For == "a shooter" ? ": " + R1(r.OverallMelee) + " for a melee fighter" : ""), null, r.For == "a shooter" ? 19 : 0);
            if (Off("WorkSpeedGlobal") < 0f) Add("slows-work", "work speed " + P(Off("WorkSpeedGlobal")));
            if (!string.IsNullOrEmpty(a.WorkDisables) || a.SkillGainZero) Add("disables-work", a.SkillGainZero ? "its wearer learns nothing" : "no " + a.WorkDisables.ToLowerInvariant() + " work");
            if (Off("SocialImpact") < 0f || Off("NegotiationAbility") < 0f) Add("off-putting", "social impact " + P(Off("SocialImpact")));
            float sens = ApparelRateMath.Sensitivity(a);
            if (sens < 0f) Add("blocks-psycasts", "psychic sensitivity " + P(sens));
            float warmthAt = head ? 5f : 4f;
            if ((!combat || r.Role == "helmet") && r.Role != "utility" && r.Role != "hazard gear" && r.Cold < warmthAt && r.HeatInsulation < warmthAt)
                Add("little-warmth", "keeps out " + F0(r.Cold) + " °C of cold and " + F0(r.HeatInsulation) + " °C of heat");
            if (r.Slot == "body" && !combat && r.Role != "utility" && r.Coverage >= 0.2f && sGuns <= 2.5f)
                Add("no-armour", "rates " + F0(sGuns) + " against guns");
            float flam = ApparelRateMath.Stat(a.StatBases, "Flammability", 1f) * a.StuffFlammability;
            if (flam >= 1f && r.Heat < 0.2f && (r.Role == "body armour" || r.Role.EndsWith("gear") || r.Role == "everyday clothing")) Add("flammable", "flammability " + P(flam) + ", heat armour " + P(r.Heat));

            // ---- Risks ----
            float wear = a.WearPerDay;
            if (a.CareIfDamaged && wear > 0f && hp * 0.5f / wear < 60f) Add("wears-fast", "half its " + F0(hp) + " hit points gone in " + F0(hp * 0.5f / wear) + " days of wear");
            if (combat && !head && r.Coverage >= 0.3f && a.CareIfDamaged && hp < 200f && r.Sharp >= 0.3f) Add("tatters", hp + " hit points: a quarter of each hit's damage wears it");
            bool runsOut = false, angers = false, calls = false;
            foreach (var p in a.Powers) { if (p.RunsOut) runsOut = true; if (p.Kind == "lance") angers = true; if (p.Note.Contains("hostile")) calls = true; }
            if (runsOut) Add("runs-out", "gone when its charges are used");
            if (angers) Add("angers", "its target's faction loses 200 goodwill");
            if (a.PainOffset > 0f) Add("hurts", "pain +" + P(a.PainOffset));
            if (a.Gender != "") Add("gendered", "made for " + a.Gender.ToLowerInvariant() + " wearers");
            bool torsoOrLegs = a.Groups.Contains("Torso") || a.Groups.Contains("Legs");
            if (!a.CountsAsClothes && torsoOrLegs && r.Role != "utility") Add("naked", "doesn't count as clothing");
            foreach (var n in a.Notes) if (n.StartsWith("blows up")) Add("explodes", n);
            if (calls) Add("calls-enemies", "calls hostile mechanoids");

            // ---- Specials ----
            foreach (var s in a.Shields)
            {
                Add("shield", s.Label + ": " + F0(s.Capacity) + " damage, " + F0(s.Recharge) + " a second back");
                if (s.Field) Add("shields-allies", "its field covers those beside its wearer");
            }
            foreach (var p in a.Powers)
            {
                if (!p.Counted && p.Kind == "other") continue;
                string uses = p.Charges > 0 ? p.Charges + (p.Charges == 1 ? " use" : " uses") : "no charges";
                switch (p.Kind)
                {
                    case "jump": Add("jump", "jumps up to " + F0(p.Range) + " cells, " + uses); break;
                    case "smoke": Add("smoke", "a smoke cloud of " + F0(p.Radius) + " cells, " + uses); break;
                    case "lowshield": Add("lowshield", "a low shield for " + F0(p.Seconds) + " seconds, " + uses); Add("shields-allies", "the low shield covers everyone inside it"); break;
                    case "firefoam": Add("firefoam", "foam over " + F0(p.Radius) + " cells, " + uses); break;
                    case "grenade": Add("grenades", p.Label + ", " + uses, "Grants: " + WeaponTagMath.Short(p.Label)); break;
                    case "lance": Add("lance", (p.Berserk ? "sends one enemy berserk" : "psychic shock on one enemy") + ", " + uses); break;
                    case "targeter": Add("strike", p.Label + ", " + uses); break;
                    case "summon": Add("summon", p.Note + ", " + uses); break;
                    case "gas": Add("grenades", p.Label + ", " + uses, "Grants: " + WeaponTagMath.Short(p.Label)); break;
                    case "resurrect": Add("second-life", "brings its wearer back once, then it is gone"); break;
                }
            }
            if (sens > 0f || r.Areas["psychic"] > 0.02f) Add("psychic", sens > 0f ? "psychic sensitivity +" + P(sens) : "psychic heat or focus");
            if (sens < 0f) Add("shelter", "psychic sensitivity " + P(sens));
            if (vac >= 0.6f || oxygen) Add("vacuum-proof", oxygen ? "an oxygen supply" : "vacuum resistance " + P(vac));
            float tox = Off("ToxicEnvironmentResistance") + Off("ToxicResistance");
            if (tox >= 0.5f || a.ImmuneToToxGas) Add("toxin-proof", a.ImmuneToToxGas ? "immune to tox gas" : "toxic resistance " + P(tox));
            if (Off("MechBandwidth") > 0f || Off("MechControlGroups") > 0f)
                Add("bandwidth", Off("MechBandwidth") > 0f ? "+" + F0(Off("MechBandwidth")) + " bandwidth" : "+" + F0(Off("MechControlGroups")) + " control group");
            if (a.Enchantment.Count > 0) Add("enchanted", "magic worth " + P(ApparelRateMath.MagicWorth(a)));
            if (a.Camouflage) Add("camouflage", "hides its wearer while still");
            if (a.PreventsBleeding || a.BleedFactor < 1f) Add("no-bleed", a.PreventsBleeding ? "its wearer doesn't bleed" : "bleeding x" + F0(a.BleedFactor));
            float ward = Off("ArmorRating_Sharp");
            if (ward > 0f) Add("wards", "+" + P(ward) + " sharp armour over the whole body");
            foreach (var p in a.Powers) if (p.Kind == "gun") { Add("extra-gun", p.Label + ": " + p.Note); break; }
            if (a.Unsure.Count > 0) Add("own-effect", a.Unsure[0]);
            else foreach (var p in a.Powers) if (!p.Counted && p.Note != "") { Add("own-effect", (p.Label != "" ? p.Label + ": " : "") + p.Note); break; }

            // ---- Needs ----
            if (a.Titles.Count > 0 && r.Role == "ceremonial or psychic") Add("needs-title", "worn for a title's apparel rule");
            if (r.Areas["psychic"] > 0.02f) Add("needs-psylink", "its psychic gains need a psycaster");
            if (r.Areas["mech"] > 0.01f || a.MechanitorApparel) Add("mechanitor-only", "bandwidth is a mechanitor's");
            if (a.Enchantment.Count > 0 && ApparelRateMath.MagicWorth(a) > 0f) Add("needs-mage", "an enchantment for a mage or a might user");
            if (a.Gender != "") Add("one-gender", a.Gender + " only", a.Gender == "Male" ? "Men only" : "Women only");
            if (a.Ages == "Child") Add("children-only", "made for children");
            if (a.OffHandCovers != null) Add("one-handed", "only with a weapon under 1.65 kg that Dual Wield takes in the off hand");
            if (a.Research != "" && (a.ResearchTech == "Spacer" || a.ResearchTech == "Ultra" || a.ResearchTech == "Archotech")) Add("research", "needs " + a.Research + " to make");
            if (a.HardToGet) Add("hard-to-get", "no recipe makes it and no faction wears it: only a trader, a quest or an event brings it");
            if (a.Biocodable) Add("biocoded", "can be biocoded to one colonist");
            if (byStuff != null && byStuff.Count > 1)
            {
                string lo = null, hi = null;
                foreach (var kv in byStuff)
                {
                    if (lo == null || kv.Value < byStuff[lo]) lo = kv.Key;
                    if (hi == null || kv.Value > byStuff[hi]) hi = kv.Key;
                }
                if (byStuff[hi] - byStuff[lo] >= 1.5f) Add("good-stuff", "in " + hi + " " + F0(byStuff[hi]) + ", in " + lo + " " + F0(byStuff[lo]));
            }
            foreach (var p in a.Powers)
                if (p.Ammo != "") { Add("needs-ammo", "its charges take " + p.Ammo.ToLowerInvariant() + " to refill", "Needs " + p.Ammo.ToLowerInvariant()); break; }
            foreach (var l in a.Layers)
                if (Array.IndexOf(ApparelRateMath.UtilityLayers, l) >= 0)
                {
                    string slot = l == "Belt" ? "Belt slot" : l == "TM_Artifact" ? "Artifact slot" : "Hip slot";
                    Add("slot", "one piece on the " + slot.ToLowerInvariant() + " over the " + string.Join(", ", a.Groups).ToLowerInvariant() + " at a time", slot);
                    break;
                }
            float any = 0f;
            foreach (var kv in r.Areas) any = Math.Max(any, Math.Abs(kv.Value));
            if (any < 0.02f && a.Powers.Count == 0 && a.Shields.Count == 0 && piece == null) Add("role-looks", "no area is worth 2% of a colonist");
            return WeaponTagMath.Pick(all, max);
        }

        /// <summary>The kinds of work a piece can suit, each by its stats in order (a minus: lower is better).</summary>
        static readonly (string id, string[] stats)[] WorkKinds =
        {
            ("role-builder", new[] { "ConstructionSpeed", "ConstructSuccessChance", "SmoothingSpeed", "FixBrokenDownBuildingSuccessChance" }),
            ("role-researcher", new[] { "ResearchSpeed", "EntityStudyRate" }),
            ("role-cook", new[] { "CookSpeed", "-FoodPoisonChance", "ButcheryFleshSpeed", "ButcheryFleshEfficiency" }),
            ("role-grower", new[] { "PlantWorkSpeed", "PlantHarvestYield" }),
            ("role-handler", new[] { "AnimalGatherSpeed", "AnimalGatherYield" }),
            ("role-miner", new[] { "MiningSpeed" }),
        };

        static string RoleWhy(ApparelRating r)
        {
            switch (r.Role)
            {
                case "body armour": return "body armour: armour " + Pct(r.Sharp) + " sharp, " + Pct(r.Blunt) + " blunt";
                case "helmet": return "a helmet: armour " + Pct(r.Sharp) + " sharp";
                case "headgear": return "worn on the head";
                case "off-hand shield": return "carried in the off hand";
                case "utility": return "a belt, pack or accessory";
                case "cold-weather gear": return "made for the cold";
                case "hot-weather gear": return "made for the heat";
                case "hazard gear": return "made to keep out toxins or vacuum";
                case "ceremonial or psychic": return "worn for a title, a faith or the mind";
                default: return "clothing for every day";
            }
        }

        static string Pct(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        static string R1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        static string RoleNoun(string role) => role == "everyday clothing" ? "everyday clothing" : role.EndsWith("gear") || role == "headgear" ? role
            : role == "ceremonial or psychic" ? "ceremonial and psychic pieces" : role;

        /// <summary>
        /// A piece that isn't rated as body armour, a helmet or a shield but is made as armour (its stuff's armour counts at
        /// 40% or more, or, made of no stuff, its own sharp armour is 40% or more): named for what it covers. Null otherwise.
        /// </summary>
        public static string ArmourPiece(ApparelRating r, ApparelFacts a)
        {
            if (r.Role == "body armour" || r.Role == "helmet" || r.Role == "off-hand shield" || r.Role == "utility") return null;
            float mult = ApparelRateMath.Stat(a.StatBases, "StuffEffectMultiplierArmor");
            bool made = a.StuffDef != "" ? mult >= 0.4f : r.Sharp >= 0.4f;
            if (!made) return null;
            if (r.Slot == "head") return "Armour for the head";
            bool hands = true, feet = true;
            foreach (var g in a.Groups)
            {
                if (g != "Hands" && g != "LeftHand" && g != "RightHand") hands = false;
                if (g != "Feet") feet = false;
            }
            if (a.Groups.Count > 0 && hands) return "Armour for the hands";
            if (a.Groups.Count > 0 && feet) return "Armour for the feet";
            bool shell = a.Layers.Contains("Shell") || a.Layers.Contains("TM_Cloak");
            if (shell && !a.Groups.Contains("Torso") && !a.Groups.Contains("Legs")) return "Armoured cloak";
            return "Made as armour";
        }

        // ---------------------------------------------------------------- the piece in plain words

        static readonly Dictionary<string, string> Noun = new Dictionary<string, string>
        {
            ["body armour"] = "body armour", ["helmet"] = "helmet", ["headgear"] = "piece of headgear", ["off-hand shield"] = "shield",
            ["utility"] = "utility piece", ["everyday clothing"] = "everyday garment", ["cold-weather gear"] = "piece of cold-weather gear",
            ["hot-weather gear"] = "piece of hot-weather gear", ["hazard gear"] = "hazard suit", ["ceremonial or psychic"] = "ceremonial piece",
        };

        static readonly Dictionary<string, string> Phrase = new Dictionary<string, string>
        {
            ["stops-bullets"] = "stops most bullets", ["turns-blades"] = "turns blades and claws", ["holds-mechs"] = "holds up against mechanoids",
            ["fireproof"] = "shrugs off fire", ["warm"] = "warm in a blizzard", ["cool"] = "keeps its wearer cool", ["light"] = "for little weight",
            ["full-cover"] = "covers nearly everything", ["quick"] = "quickens the step", ["aim"] = "steadies the aim", ["melee-edge"] = "sharpens a fighter",
            ["painproof"] = "keeps its wearer standing through pain", ["steady"] = "steadies the nerves",
            ["slows"] = "slows its wearer", ["blocks-shooting"] = "its wearer can't shoot out", ["blinds"] = "its wearer can't see",
            ["thin-cover"] = "covers little", ["weak-blunt"] = "blunt blows get through", ["mechs-pierce"] = "mechanoid weapons punch through",
            ["no-warmth"] = "useless in a blizzard", ["worse-aim"] = "spoils the aim", ["clumsy"] = "clumsy in melee", ["slows-work"] = "slows work",
            ["disables-work"] = "bars some work", ["off-putting"] = "puts people off", ["blocks-psycasts"] = "dulls psycasts",
            ["flammable"] = "catches fire easily", ["wears-fast"] = "wears out fast", ["tatters"] = "tatters after a few fights",
            ["runs-out"] = "gone once its charges are spent", ["hurts"] = "hurts to wear", ["shield"] = "takes shots and blasts on its shield",
            ["jump"] = "jumps over walls and enemies", ["smoke"] = "smoke on demand", ["lowshield"] = "a low shield for a stand",
            ["firefoam"] = "puts fires out", ["lance"] = "takes one enemy out from afar", ["strike"] = "calls a strike from orbit",
            ["summon"] = "brings help of its own", ["psychic"] = "lifts a psycaster", ["shelter"] = "shields the mind from psychic attacks",
            ["vacuum-proof"] = "keeps its wearer alive in vacuum", ["toxin-proof"] = "keeps toxins out", ["bandwidth"] = "more mechs for a mechanitor",
            ["enchanted"] = "enchanted for a mage", ["camouflage"] = "hides its wearer", ["second-life"] = "brings its wearer back from death once",
            ["no-bleed"] = "keeps its wearer from bleeding out", ["role-worker"] = "speeds up work", ["role-trader"] = "helps in talks and trade",
            ["role-medic"] = "for the colony's doctor", ["role-hunter"] = "for a hunter", ["role-royal"] = "fit for a title",
            ["role-researcher"] = "for a researcher", ["role-builder"] = "for a builder", ["role-cook"] = "for a cook",
            ["role-grower"] = "for a grower", ["role-handler"] = "for an animal handler", ["role-miner"] = "for a miner",
            ["carries-more"] = "carries more", ["heals"] = "heals its wearer faster", ["impression"] = "makes a good impression",
            ["wards"] = "wards the whole body", ["extra-gun"] = "a gun of its own beside its wearer", ["little-warmth"] = "little warmth",
            ["no-armour"] = "won't stop a bullet", ["calls-enemies"] = "calls enemies down", ["explodes"] = "blows up when its wearer dies",
            ["angers"] = "angers its target's faction", ["role-faith"] = "a faith asks for it",
        };

        /// <summary>
        /// The piece in a line, as a seasoned player would say it: how good it is for what it is for, then what stands out
        /// most (its first strengths and specials, then its first weaknesses and risks), three at most.
        /// </summary>
        public static string Line(ApparelRating r, List<ThingTag> tags)
        {
            float o = r.Overall;
            string q = o >= 8.5f ? "A superb" : o >= 7.5f ? "An excellent" : o >= 6.5f ? "A strong" : o >= 5.5f ? "A solid" : o >= 4.5f ? "A fair"
                : o >= 3.5f ? "A modest" : o >= 2.5f ? "A weak" : "A poor";
            string noun = Noun.TryGetValue(r.Role, out var n) ? n : "piece";
            if (r.Role == "helmet" && r.Sharp < 0.2f) noun = "piece of combat headgear";
            // Everyday clothing is named for what it is: armour for the hands, an armoured cloak, a cloak.
            if (r.Role == "everyday clothing")
                foreach (var t in tags)
                {
                    if (t.Id == "role-armour-piece" && t.Label != "Made as armour") { noun = t.Label == "Armoured cloak" ? "armoured cloak" : "piece of " + t.Label.ToLowerInvariant(); break; }
                    if (t.Id == "role-cloak") noun = "cloak";
                }
            var parts = new List<string>();
            var good = new List<string>();
            var bad = new List<string>();
            foreach (var t in tags)
            {
                string ph;
                if (t.Id == "grenades") ph = "its " + t.Label.Replace("Grants: ", "") + " at hand";
                else if (!Phrase.TryGetValue(t.Id, out ph)) continue;
                if (t.Category == TagCategory.Weakness || t.Category == TagCategory.Risk) bad.Add(ph);
                else good.Add(ph);
            }
            int gi = 0, bi = 0;
            while (parts.Count < 3 && (gi < good.Count || bi < bad.Count))
            {
                if (gi < good.Count && (parts.Count < 2 || bi >= bad.Count)) parts.Add(good[gi++]);
                else if (bi < bad.Count) parts.Add(bad[bi++]);
            }
            string head = q + " " + noun;
            if (r.For == "a melee fighter") head += " for a melee fighter";
            else if (r.For == "a shooter") head += " for a shooter";
            return parts.Count == 0 ? head + "." : head + "; " + string.Join("; ", parts) + ".";
        }
    }
}
