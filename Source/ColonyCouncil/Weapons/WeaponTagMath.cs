using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>What a tag says about a thing: what it is good at, bad at, what can go wrong, what it does, what it is for, what it asks.</summary>
    public enum TagCategory
    {
        Strength,
        Weakness,
        Risk,
        Special,
        Role,
        Needs
    }

    /// <summary>A category's look: its colour (from Rimfolk's own palette, UI/SkinMath) and a symbol, so colour is never the only cue.</summary>
    public sealed class TagCategoryStyle
    {
        public TagCategory Category;
        public string Label;
        public uint Colour;
        public string Symbol;
        /// <summary>A word for the icon step 2 draws in the game (its own texture), where the symbol's glyph may be missing.</summary>
        public string IconKey;

        public string Hex => "#" + Colour.ToString("x6", CultureInfo.InvariantCulture);
    }

    /// <summary>One tag of the vocabulary: its id, its short label, its category and the rule that gives it (for the legend).</summary>
    public sealed class TagDef
    {
        public string Id;
        public string Label;
        public TagCategory Category;
        public string Rule;
        /// <summary>How much it matters, for the order tags are shown in (higher first).</summary>
        public int Importance;
    }

    /// <summary>A tag a thing has: its id, its label (an ability's tag names the ability), its category and why, in its own numbers.</summary>
    public sealed class ThingTag
    {
        public string Id;
        public string Label;
        public TagCategory Category;
        public string Why;
        public int Importance;
    }

    /// <summary>
    /// What the tags read about a weapon besides its rating: its numbers (a gun's verb and shot, a melee weapon's quickest
    /// and best attack), what it carries, its mass and what it does to its wielder, and what only its comps and mods say
    /// (a persona, unique traits, biocoding, who may carry it, an overheat, wear). The review fills it from the defs, the
    /// game side will from the weapon in hand.
    /// </summary>
    public sealed class WeaponTagFacts
    {
        public string DefName = "";
        public float Mass;
        public float MoveSpeedOffset;
        public float DodgeOffset;
        public float DeflectChance;
        public float PsychicSensitivityOffset;
        public bool Persona, Unique, Biocodable, Thrown, BypassesShields, Demolishes, Destructive;
        /// <summary>No recipe in the player's game makes it and no faction's people carry it: only a trader, a quest or an event brings it.</summary>
        public bool HardToGet;
        /// <summary>Who alone may carry it (an exoframe or the Tough trait, a mage, someone who can channel): empty for anyone.</summary>
        public string RestrictedTo = "";
        public float OverheatChance;
        public float HitPointsPerShot;
        public string BreaksAfter = "";
        public bool Wears;
        /// <summary>The research its recipe needs and that research's age (Spacer, Ultra, Archotech count as advanced).</summary>
        public string Research = "";
        public string ResearchTech = "";
        /// <summary>Its rating at skill 4 and at skill 16 (shooting or melee), the reference's own traits otherwise.</summary>
        public float RatingAtSkill4, RatingAtSkill16;
        public List<(string label, int charges, bool counted)> Abilities = new List<(string, int, bool)>();
        /// <summary>The weapon in hand: its unique traits, its persona and bond, whom it is biocoded to (empty for a def: the tags' usual reasons).</summary>
        public string UniqueWhy = "", PersonaWhy = "", BiocodedWhy = "";
    }

    /// <summary>
    /// Tags for weapons, the way many games label gear: a fixed vocabulary in six colour-coded categories, each tag given by
    /// a rule on the weapon's own numbers (never by hand) with a line saying why. A weapon gets its most important few
    /// (at most seven, at least its first strength and its first weakness when it has them), in the order of importance.
    /// The categories and the TagDef/ThingTag shapes are not weapon-only: armour and apparel can take their own vocabulary
    /// in the same six categories later.
    ///
    /// The thresholds are the base game's own lines where it has them: a raider's assault rifle reaches 30.9 cells;
    /// stopping power 1.5 staggers a man (body size 1) with room to spare; Simple Sidearms' default preset (Basic) carries
    /// melee sidearms up to 1.9 kg and ranged ones up to 2.7 kg; Dual Wield's defaults take any weapon in the off hand
    /// but a bow, a blowgun, one over 3 kg or a melee weapon over 1.5 kg, and count bows, shotguns and anything over 3 kg
    /// two-handed.
    /// </summary>
    public static class WeaponTagMath
    {
        /// <summary>Simple Sidearms' default preset (Basic): the heaviest melee and ranged sidearm for an ordinary colonist.</summary>
        public const float SidearmMeleeMass = 1.9f, SidearmRangedMass = 2.7f;
        /// <summary>Dual Wield's defaults: an off-hand weapon is at most 3 kg (1.5 kg for melee); over 3 kg is two-handed.</summary>
        public const float OffhandMass = 3f, OffhandMeleeMass = 1.5f, TwoHandMass = 3f;

        public static readonly TagCategoryStyle[] Categories =
        {
            new TagCategoryStyle { Category = TagCategory.Strength, Label = "Strength", Colour = SkinMath.Done, Symbol = "▲", IconKey = "strength" },
            new TagCategoryStyle { Category = TagCategory.Weakness, Label = "Weakness", Colour = SkinMath.Danger, Symbol = "▼", IconKey = "weakness" },
            new TagCategoryStyle { Category = TagCategory.Risk, Label = "Risk", Colour = SkinMath.Foe, Symbol = "⚠", IconKey = "risk" },
            new TagCategoryStyle { Category = TagCategory.Special, Label = "Special", Colour = SkinMath.World, Symbol = "✦", IconKey = "special" },
            new TagCategoryStyle { Category = TagCategory.Role, Label = "Role", Colour = SkinMath.Ally, Symbol = "◆", IconKey = "role" },
            new TagCategoryStyle { Category = TagCategory.Needs, Label = "Needs", Colour = SkinMath.Cool, Symbol = "●", IconKey = "needs" },
        };

        public static TagCategoryStyle Style(TagCategory c)
        {
            foreach (var s in Categories) if (s.Category == c) return s;
            return Categories[0];
        }

        static TagDef D(string id, string label, TagCategory c, int importance, string rule) =>
            new TagDef { Id = id, Label = label, Category = c, Importance = importance, Rule = rule };

        const TagCategory S = TagCategory.Strength, W = TagCategory.Weakness, R = TagCategory.Risk, X = TagCategory.Special,
            O = TagCategory.Role, N = TagCategory.Needs;

        /// <summary>The weapon vocabulary: every tag, its category, its importance and its rule in words (the legend).</summary>
        public static readonly TagDef[] WeaponTags =
        {
            D("role-sniper", "Sniper", O, 95, "a long gun: reaches 38 cells or more and is at least as accurate far as near"),
            D("role-assault", "Assault", O, 95, "an all-round gun or bow"),
            D("role-close", "Close quarters", O, 95, "a close weapon: under 20 cells, or under 26 and losing a quarter of its accuracy by 25 cells"),
            D("role-grenadier", "Grenadier", O, 95, "a thrown explosive or a one-use launcher"),
            D("role-launcher", "Launcher", O, 95, "fires explosive or scattering shells"),
            D("role-antimech", "Anti-mech", O, 95, "an EMP weapon that harms no flesh"),
            D("role-melee", "Melee", O, 95, "a melee weapon (light under 1.5 kg, heavy from 1.5 kg)"),
            D("role-support", "Suppression", O, 70, "bursts of 5 shots or more that strike half again as many in a bunched group"),
            D("role-brawler", "Brawler's pick", O, 66, "a melee weapon rated 6.5 or more"),
            D("role-hunting", "Hunting", O, 58, "a gun or bow reaching 25 cells, rated 5 or more at 12 and 25 cells, with no blast or fire to spoil the kill"),
            D("role-capture", "Capture alive", O, 60, "non-lethal, or a melee weapon whose blows are mostly blunt"),
            D("role-breacher", "Breacher", O, 56, "a demolishing blow, a destructive weapon, or an explosive shot of 30 damage or more"),
            D("role-sidearm", "Sidearm", O, 54, "within Simple Sidearms' default limits: melee up to 1.9 kg, ranged up to 2.7 kg, not one-use"),
            D("role-offhand", "Offhand", O, 50, "Dual Wield's default: not a bow or a blowgun, at most 3 kg (melee 1.5 kg), not one-use"),
            D("long-reach", "Long reach", S, 84, "reaches 34.9 cells or more, past a raider's 30.9-cell rifle"),
            D("dead-accurate", "Dead accurate", S, 76, "85% accurate or better at 25 cells"),
            D("shreds-armour", "Shreds armour", S, 86, "keeps 55% or more of its damage against marine armour, and rates 5 or more against it"),
            D("mech-killer", "Mech killer", S, 85, "rates 7 or more against mechanoids and 1.5 above its rating against flak, or stuns them with EMP"),
            D("crowd-breaker", "Crowd breaker", S, 82, "strikes 60% more in a bunched group (a blast, a beam, a sprayed burst)"),
            D("stopping-power", "Stopping power", S, 72, "stopping power 1.5 or more: staggers a charging man"),
            D("rapid-fire", "Rapid fire", S, 74, "1.5 shots a second or more over its whole cycle"),
            D("hard-hitter", "Hard hitter", S, 78, "25 damage or more a hit (a gun's shot, a melee weapon's best blow)"),
            D("quick-aim", "Quick to aim", S, 68, "aims in 0.6 seconds or less"),
            D("quick-strikes", "Quick strikes", S, 68, "a melee blow every 1.6 seconds or less"),
            D("good-defence", "Good defence", S, 70, "deflects shots or adds to its wielder's dodge"),
            D("poor-close", "Poor up close", W, 74, "can't fire within 3 cells, or 55% accurate or worse there while 75% at 25 cells"),
            D("short-range", "Short range", W, 80, "reaches under 20 cells"),
            D("slow-aim", "Slow to aim", W, 76, "2.5 seconds or more to aim"),
            D("long-reload", "Long reload", W, 72, "3 seconds or more of cooldown after each burst"),
            D("wild-spray", "Wild spray", W, 78, "40% accurate or worse at 12 cells (not an explosive)"),
            D("bounces-off", "Bounces off armour", W, 83, "keeps 40% or less of its damage against marine armour, or rates 1.8 below its own overall against it"),
            D("weak-damage", "Weak damage", W, 79, "8 damage or less a shot, or a melee weapon's best blow 10 or less"),
            D("heavy-carry", "Heavy to carry", W, 77, "slows its carrier, or weighs 8 kg or more"),
            D("mechs-shrug", "Mechs shrug off", W, 75, "rates 2.5 or less against mechanoids though it harms flesh"),
            D("fire-risk", "Fire risk", R, 81, "sets fires where it lands: a fire blast, fuel, a burning hit"),
            D("hits-friends", "Hits friends", R, 82, "a blast of 1.5 cells or more, or a scatter of 1.5 cells or more"),
            D("ruins-hunt", "Ruins the hunt", R, 62, "explodes or burns: spoils meat and pelts"),
            D("one-use", "One use", R, 90, "spent after one volley"),
            D("self-harm", "Can hurt you", R, 80, "a mod's own code can blast its shooter (an overheating laser)"),
            D("wears-out", "Wears out", R, 64, "loses hit points with every shot, deteriorates, or may fall apart"),
            D("emp", "EMP", X, 88, "carries EMP: stuns mechanoids, breaks shield belts"),
            D("sets-fire", "Sets on fire", X, 79, "sets people alight"),
            D("stuns", "Stuns", X, 80, "stuns what it hits"),
            D("toxic", "Toxic", X, 77, "toxic buildup or a tox gas cloud"),
            D("bleeds", "Bleeds", X, 52, "a melee weapon whose blows are mostly cuts and stabs: wounds bleed"),
            D("breaks-bones", "Breaks bones", X, 52, "a melee weapon whose blows are mostly blunt: cracked bones, a head blow can stun"),
            D("beam", "Beam", X, 70, "a beam, spray or line that strikes everyone along it"),
            D("grants", "Grants: (ability)", X, 84, "grants an ability, named"),
            D("persona", "Persona bond", X, 73, "a persona weapon: bonds to its first wielder, with persona traits"),
            D("unique", "Unique traits", X, 71, "a unique weapon: traits rolled when made"),
            D("non-lethal", "Non-lethal", X, 86, "made to bring people down alive"),
            D("psychic", "Psychic", X, 66, "raises its wielder's psychic sensitivity"),
            D("shield-breaker", "Shield-breaker", X, 72, "breaks shield belts (EMP) or passes through them"),
            D("good-shot", "Needs a marksman", N, 74, "rates 3 points or more higher at Shooting 16 than at Shooting 4"),
            D("strong-arm", "Needs a fighter", N, 74, "rates 3 points or more higher at Melee 16 than at Melee 4"),
            D("two-handed", "Two-handed", N, 48, "Dual Wield's default: a bow, a shotgun, or over 3 kg"),
            D("biocoded", "Biocodable", N, 40, "can be biocoded to one colonist"),
            D("restricted", "Restricted", N, 87, "only some can wield it (an exoframe or the Tough trait, a mage, a channeller)"),
            D("research", "Advanced research", N, 45, "made only after spacer or ultra research"),
            D("hard-to-get", "Hard to find", N, 89, "no recipe in the player's game makes it and no faction's people carry it"),
        };

        public static TagDef Def(string id)
        {
            foreach (var d in WeaponTags) if (d.Id == id) return d;
            return null;
        }

        /// <summary>
        /// A weapon's tags from its rating and facts: every rule that holds, ordered by importance, at most <paramref name="max"/>
        /// of them, keeping its role, its first strength and its first weakness.
        /// </summary>
        public static List<ThingTag> For(WeaponRating r, WeaponTagFacts f, WeaponRangedInput w = null, WeaponMeleeInput m = null, int max = 7)
        {
            var all = new List<ThingTag>();
            void Add(string id, string why, string label = null, int bump = 0)
            {
                var d = Def(id);
                if (d == null) return;
                foreach (var t in all) if (t.Id == id && label == null) return;
                all.Add(new ThingTag { Id = id, Label = label ?? d.Label, Category = d.Category, Why = why, Importance = d.Importance + bump });
            }
            string F0(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            string P(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
            float o = r.Overall;
            bool ranged = w != null;
            var ps = new List<WeaponPayload>();
            if (w != null) ps.AddRange(w.Payloads);
            if (m != null) foreach (var a in m.Attacks) ps.AddRange(a.Payloads);
            bool Has(WeaponEffect e) { foreach (var p in ps) if (p.Effect == e && p.Chance > 0f) return true; return false; }
            float keep = KeepOnHeavy(r);

            // ---- Role: what it is ----
            switch (r.Role)
            {
                case "long": Add("role-sniper", "a long gun: judged mostly at 25 and 40 cells"); break;
                case "close": Add("role-close", "judged mostly within a dozen cells"); break;
                case "area":
                case "one-use":
                    if (f.Thrown || r.Role == "one-use") Add("role-grenadier", r.Role == "one-use" ? "one big volley" : "thrown into a group");
                    else Add("role-launcher", "lobs shells into a group");
                    break;
                case "anti-mechanoid": Add("role-antimech", "judged three fifths on mechanoids"); break;
                case "utility": break;
                case "light melee":
                case "heavy melee": Add("role-melee", r.Role == "light melee" ? "a light melee weapon" : "a heavy melee weapon"); break;
                default: Add("role-assault", "judged at every range up to 25 cells"); break;
            }

            if (ranged)
            {
                float shotsPerSecond = Math.Max(w.Burst, 1) * Math.Max(w.ProjectilesPerShot, 1) / Math.Max(WeaponRateMath.CycleSeconds(w, WeaponRateMath.Reference), 0.05f);
                bool blast = w.ExplosionRadius >= 1f;
                bool fire = Has(WeaponEffect.Ignite) || (w.Armour == WeaponArmour.Heat && w.HarmsHealth && w.Damage > 0f);
                bool harms = w.HarmsHealth && w.Damage > 0f;
                // Strengths
                if (w.Range >= WeaponRateMath.EnemyReach + 4f && !w.OneUse) Add("long-reach", "reaches " + F0(w.Range) + " cells, past a raider's rifle at 31");
                if (w.Range >= 25f && w.AccMedium >= 0.85f && w.ForcedMissRadius <= 0.5f && !w.Beam) Add("dead-accurate", P(w.AccMedium) + " accurate at 25 cells");
                if (harms && keep >= 0.55f && r.Sub["heavy"] >= 5f) Add("shreds-armour", "keeps " + P(keep) + " of its damage against marine armour");
                if (Has(WeaponEffect.Emp)) Add("mech-killer", "its EMP stuns a mechanoid for " + F0(EmpSeconds(ps)) + " seconds", null, 2);
                else if (MechKiller(r)) Add("mech-killer", "rates " + F0(r.Sub["mechanoids"]) + " against mechanoids, " + F0(r.Sub["armoured"]) + " against flak");
                if (harms && r.CrowdFactor >= 1.6f) Add("crowd-breaker", "strikes about " + F0(r.CrowdFactor - 1f) + " more for each one it aims at, in a bunched group");
                if (w.StoppingPower >= 1.5f && harms) Add("stopping-power", "stopping power " + F0(w.StoppingPower) + ": staggers anyone up to body size " + F0(w.StoppingPower));
                if (shotsPerSecond >= 1.5f && !w.OneUse) Add("rapid-fire", F0(shotsPerSecond) + " shots a second over its cycle");
                if (harms && w.Damage >= 25f) Add("hard-hitter", F0(w.Damage) + " damage a hit");
                if (w.Warmup <= 0.6f && !w.OneUse) Add("quick-aim", "aims in " + F0(w.Warmup) + " seconds");
                // Weaknesses
                if (w.MinRange > 3f) Add("poor-close", "can't fire within " + F0(w.MinRange) + " cells");
                else if (w.AccTouch <= 0.55f && w.AccMedium >= 0.75f && !w.Beam) Add("poor-close", "only " + P(w.AccTouch) + " accurate within 3 cells");
                if (w.Range < 20f) Add("short-range", "reaches only " + F0(w.Range) + " cells");
                if (w.Warmup >= 2.5f) Add("slow-aim", F0(w.Warmup) + " seconds to aim");
                if (w.Cooldown >= 3f && !w.OneUse) Add("long-reload", F0(w.Cooldown) + " seconds of cooldown after each burst");
                if (!blast && w.ForcedMissRadius <= 0.5f && !w.Beam && WeaponRateMath.WeaponAccuracy(w, 12f) <= 0.4f)
                    Add("wild-spray", P(WeaponRateMath.WeaponAccuracy(w, 12f)) + " accurate at 12 cells");
                if (harms && (keep <= 0.4f || r.Sub["heavy"] <= o - 1.8f)) Add("bounces-off", "keeps only " + P(keep) + " of its damage against marine armour");
                if (harms && w.Damage <= 8f) Add("weak-damage", F0(w.Damage) + " damage a shot");
                if (harms && r.Sub["mechanoids"] <= 2.5f) Add("mechs-shrug", "rates " + F0(r.Sub["mechanoids"]) + " against mechanoids" + (w.Armour == WeaponArmour.Heat ? ": their armour stops heat" : ""));
                // Risks
                if (fire) Add("fire-risk", blast ? "its blast starts fires" : "starts fires where it strikes");
                if (harms && (w.ExplosionRadius >= 1.5f || w.ForcedMissRadius >= 1.5f))
                    Add("hits-friends", w.ExplosionRadius >= 1.5f ? "a blast of " + F0(w.ExplosionRadius) + " cells" : "lands anywhere within " + F0(w.ForcedMissRadius) + " cells");
                if (harms && (blast || fire)) Add("ruins-hunt", blast ? "a blast spoils meat and pelts" : "fire spoils meat and pelts");
                if (w.OneUse) Add("one-use", "spent after one volley");
                // Specials
                if (w.Beam || w.Line) Add("beam", w.Beam ? "sweeps a beam across its target" : "strikes everyone along its line");
                if (w.NonLethal) Add("non-lethal", "made to bring them down alive");
                // Roles
                if (r.CrowdFactor >= 1.5f && w.Burst >= 5 && harms && !blast) Add("role-support", "bursts of " + w.Burst + " that keep a group's heads down");
                if (!w.OneUse && f.Mass > 0f && f.Mass <= SidearmRangedMass) Add("role-sidearm", F0(f.Mass) + " kg: within Simple Sidearms' default " + F0(SidearmRangedMass) + " kg");
                if (harms && !blast && !fire && w.Range >= 25f && r.Sub["medium"] >= 5f && r.Sub["long"] >= 5f) Add("role-hunting", "reaches " + F0(w.Range) + " cells and leaves meat and pelt whole");
                if (w.NonLethal) Add("role-capture", "brings them down alive");
                if (blast && w.Damage >= 30f && harms) Add("role-breacher", F0(w.Damage) + "-damage blasts break walls and doors");
                bool bow = f.DefName.Contains("Bow_") || f.DefName.Contains("Blowgun");
                if (!w.OneUse && !bow && f.Mass > 0f && f.Mass <= OffhandMass) Add("role-offhand", "Dual Wield's default takes it in the off hand");
                if (f.DefName.Contains("Bow") || f.DefName.Contains("Shotgun") || f.Mass > TwoHandMass) Add("two-handed", "Dual Wield's default counts it two-handed");
                if (f.RatingAtSkill16 - f.RatingAtSkill4 >= 3f) Add("good-shot", "rates " + F0(f.RatingAtSkill4) + " at Shooting 4, " + F0(f.RatingAtSkill16) + " at 16");
            }
            if (m != null && !ranged)
            {
                float best = 0f, minCd = float.MaxValue, sharp = 0f, blunt = 0f;
                foreach (var a in m.Attacks)
                {
                    var n = WeaponRateMath.MeleeNumbers(a, m, WeaponRateMath.Reference);
                    best = Math.Max(best, n.damage);
                    minCd = Math.Min(minCd, n.cooldown);
                    if (a.IsCut) sharp += a.Power; else if (a.IsBlunt) blunt += a.Power;
                }
                if (keep >= 0.55f && r.Sub["heavy"] >= 5f) Add("shreds-armour", "keeps " + P(keep) + " of its damage against marine armour");
                if (Has(WeaponEffect.Emp)) Add("mech-killer", "its EMP stuns a mechanoid for " + F0(EmpSeconds(ps)) + " seconds", null, 2);
                else if (MechKiller(r)) Add("mech-killer", "rates " + F0(r.Sub["mechanoids"]) + " against mechanoids, " + F0(r.Sub["armoured"]) + " against flak");
                if (best >= 25f) Add("hard-hitter", F0(best) + " damage with its best blow");
                if (minCd <= 1.6f) Add("quick-strikes", "a blow every " + F0(minCd) + " seconds");
                if (keep <= 0.4f || r.Sub["heavy"] <= o - 1.8f) Add("bounces-off", "keeps only " + P(keep) + " of its damage against marine armour");
                if (best <= 10f) Add("weak-damage", F0(best) + " damage with its best blow");
                if (r.Sub["mechanoids"] <= 2.5f) Add("mechs-shrug", "rates " + F0(r.Sub["mechanoids"]) + " against mechanoids");
                if (sharp >= 1.5f * blunt && sharp > 0f) Add("bleeds", "cuts and stabs: wounds bleed");
                else if (blunt >= 1.5f * sharp && blunt > 0f) Add("breaks-bones", "blunt blows: cracked bones, a head blow can stun");
                if (m.NonLethal || (blunt >= 1.5f * sharp && blunt > 0f)) Add("role-capture", m.NonLethal ? "made to bring them down alive" : "blunt blows down more than they kill");
                if (m.NonLethal) Add("non-lethal", "made to bring them down alive");
                if (o >= 6.5f) Add("role-brawler", "rates " + F0(o) + ": a melee fighter's main weapon");
                if (f.Demolishes || f.Destructive) Add("role-breacher", f.Demolishes ? "a demolishing blow breaks walls and doors" : "made for breaking down walls");
                if (f.Mass > 0f && f.Mass <= SidearmMeleeMass) Add("role-sidearm", F0(f.Mass) + " kg: within Simple Sidearms' default " + F0(SidearmMeleeMass) + " kg");
                if (f.Mass > 0f && f.Mass <= OffhandMeleeMass) Add("role-offhand", "Dual Wield's default takes it in the off hand");
                if (f.Mass > TwoHandMass) Add("two-handed", "Dual Wield's default counts it two-handed");
                if (f.RatingAtSkill16 - f.RatingAtSkill4 >= 3f) Add("strong-arm", "rates " + F0(f.RatingAtSkill4) + " at Melee 4, " + F0(f.RatingAtSkill16) + " at 16");
            }
            // ---- What both kinds carry ----
            if (Has(WeaponEffect.Emp)) Add("emp", "stuns mechanoids for " + F0(EmpSeconds(ps)) + " seconds, breaks shield belts");
            if (Has(WeaponEffect.Emp) || f.BypassesShields) Add("shield-breaker", Has(WeaponEffect.Emp) ? "its EMP breaks shield belts" : "passes through shield belts");
            if (Has(WeaponEffect.Ignite)) Add("sets-fire", "sets people alight");
            if (Has(WeaponEffect.Stun)) Add("stuns", "stuns what it hits");
            if (Has(WeaponEffect.ToxicBuildup) || Has(WeaponEffect.ToxGas)) Add("toxic", "poisons the living; mechanoids are immune");
            if (f.MoveSpeedOffset < 0f) Add("heavy-carry", "slows its carrier by " + F0(-f.MoveSpeedOffset) + " cells a second");
            else if (f.Mass >= 8f) Add("heavy-carry", F0(f.Mass) + " kg to carry");
            if (f.DeflectChance > 0f) Add("good-defence", "deflects " + P(f.DeflectChance) + " of the shots at its wielder");
            else if (f.DodgeOffset > 0f) Add("good-defence", "+" + F0(f.DodgeOffset) + " melee dodge");
            if (f.OverheatChance > 0f) Add("self-harm", P(f.OverheatChance) + " chance of an overheat blast on the shooter at full heat");
            if (f.HitPointsPerShot > 0f) Add("wears-out", "loses " + F0(f.HitPointsPerShot) + " hit points a shot");
            else if (f.BreaksAfter != "") Add("wears-out", "may fall apart after " + f.BreaksAfter + " shots");
            else if (f.Wears) Add("wears-out", "deteriorates as it is used");
            if (f.PsychicSensitivityOffset > 0f) Add("psychic", "+" + P(f.PsychicSensitivityOffset) + " psychic sensitivity");
            if (f.Persona) Add("persona", f.PersonaWhy != "" ? f.PersonaWhy : "bonds to its first wielder; persona traits rolled when made");
            if (f.Unique) Add("unique", f.UniqueWhy != "" ? f.UniqueWhy : "one to three traits rolled when made");
            foreach (var (label, charges, counted) in f.Abilities)
                Add("grants", label + (charges > 0 ? ", " + charges + (charges == 1 ? " charge" : " charges") : "") + (counted ? "" : " (its own effect)"), "Grants: " + Short(label));
            if (f.RestrictedTo != "") Add("restricted", "only " + f.RestrictedTo);
            if (f.Biocodable) Add("biocoded", f.BiocodedWhy != "" ? f.BiocodedWhy : "can be biocoded to one colonist");
            if (f.HardToGet) Add("hard-to-get", "no recipe makes it and no faction carries it: only a trader, a quest or an event brings it");
            if (f.Research != "" && (f.ResearchTech == "Spacer" || f.ResearchTech == "Ultra" || f.ResearchTech == "Archotech"))
                Add("research", "needs " + f.Research + " to make");
            return Pick(all, max);
        }

        /// <summary>Better against mechanoids than against flak by a clear margin (an EMP weapon counts on its own).</summary>
        public static bool MechKiller(WeaponRating r) => r.Sub["mechanoids"] >= 7f && r.Sub["mechanoids"] >= r.Sub["armoured"] + 1.5f;

        /// <summary>The share of its damage a weapon keeps against marine armour, at the distances its role weighs.</summary>
        public static float KeepOnHeavy(WeaponRating r)
        {
            float bare = 0f, heavy = 0f;
            foreach (var kv in r.Dps)
            {
                if (kv.Key.EndsWith("/unarmoured")) bare += kv.Value;
                else if (kv.Key.EndsWith("/heavy")) heavy += kv.Value;
            }
            return bare > 0f ? heavy / bare : 0f;
        }

        static float EmpSeconds(List<WeaponPayload> ps)
        {
            float s = 0f;
            foreach (var p in ps) if (p.Effect == WeaponEffect.Emp) s = Math.Max(s, p.Amount * 0.5f);
            return s;
        }

        /// <summary>An ability's name in at most two words, for its tag.</summary>
        public static string Short(string label)
        {
            var words = (label ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 2) return label ?? "";
            return words[words.Length - 2] + " " + words[words.Length - 1];
        }

        /// <summary>
        /// The tags shown: by importance, at most <paramref name="max"/>, keeping the role (the first tag), the first
        /// strength and the first weakness even when less important tags crowd them out; at most three roles (its own and two more).
        /// </summary>
        public static List<ThingTag> Pick(List<ThingTag> all, int max)
        {
            all.Sort((a, b) => b.Importance.CompareTo(a.Importance));
            var keep = new List<ThingTag>();
            ThingTag firstStrength = all.Find(t => t.Category == TagCategory.Strength);
            ThingTag firstWeakness = all.Find(t => t.Category == TagCategory.Weakness);
            int roles = 0;
            foreach (var t in all)
            {
                if (t.Category == TagCategory.Role && roles >= 3) continue;
                int reserved = (firstStrength != null && !keep.Contains(firstStrength) && t != firstStrength ? 1 : 0) +
                               (firstWeakness != null && !keep.Contains(firstWeakness) && t != firstWeakness ? 1 : 0);
                if (keep.Count + reserved >= max && t != firstStrength && t != firstWeakness) continue;
                if (keep.Count >= max) break;
                keep.Add(t);
                if (t.Category == TagCategory.Role) roles++;
            }
            keep.Sort((a, b) => b.Importance.CompareTo(a.Importance));
            return keep;
        }
    }
}
