using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of A RimWorld of Magic among Rimfolk's peoples (Peoples/Magic/PeoplesMagic.cs; DESIGN.md "Magic among the
    /// peoples, seen"), with no game types so Tools/Tests can check them: which peoples are built on magic and how big a
    /// share of a kind may have a class; what a caster wears or carries of their class by the age their people lives in;
    /// the helpful spells a visitor may cast in peace, on whom and when; who may cast them; and the mod's spells Rimfolk
    /// keeps out of its peoples' hands, with why. Linked into the tests (Tools/Tests/PeoplesMagicTests.cs).
    /// </summary>
    public static class PeoplesMagicMath
    {
        // ================= The peoples built on magic, and the shares =================

        /// <summary>
        /// The callings of the peoples whose lore revolves around what A RimWorld of Magic does (the player: "use them
        /// sparingly overall, unless of course the faction revolves around magic"): the Stormglass Conclave, who feel the
        /// weather in their minds; the Mossborn, whose forest remembers; the Choir of the Spire, who touch other minds; the
        /// Quiet Bell, who listen to the ground for what should not stir. Every other people has its casters sparingly.
        /// </summary>
        public static readonly IReadOnlyCollection<string> MagicCallings = new HashSet<string> { "seers", "grovekeepers", "listeners", "sextons" };

        /// <summary>A people of this calling is built on magic.</summary>
        public static bool BuiltOnMagic(string calling) => calling != null && MagicCallings.Contains(calling);

        /// <summary>The most of an ordinary kind that may have a class: sparing, and more among the peoples built on magic.</summary>
        public const float SpareShare = 0.06f, MagicShare = 0.15f;

        /// <summary>The same for a people's leader (one pawn, seldom on the map).</summary>
        public const float SpareLeaderShare = 0.15f, MagicLeaderShare = 0.40f;

        /// <summary>The most of a kind that may have a class.</summary>
        public static float ShareCap(bool magic, bool leader) => leader ? magic ? MagicLeaderShare : SpareLeaderShare : magic ? MagicShare : SpareShare;

        /// <summary>A kind's class shares together fit its cap (a hundredth of slack for sums written in the defs).</summary>
        public static bool ShareFits(float sum, bool magic, bool leader) => !float.IsNaN(sum) && sum >= 0f && sum <= ShareCap(magic, leader) + 0.0001f;

        // ================= What a caster wears or carries =================

        /// <summary>
        /// Each class's own piece from the mod, by the age the people lives in: in the tribal and medieval ages (0 and 1)
        /// the first, in the industrial and spacer ages (2 and 3) the second; null where the mod has none for that class
        /// (its fighters, the technomancer). A hood or hat sits over any age's clothes; the elemental mages' wands take the
        /// place of a bow or a sword in the old ages, and give way to the age's guns later (their hood then); a paladin wears
        /// the Helm of the Valiant over plate, and over a later age's armour the Mantle of Protection.
        /// </summary>
        private static readonly Dictionary<string, (string old, string later)> garb = new Dictionary<string, (string, string)>
        {
            ["Priest"] = ("TM_DiscipleHood", "TM_DiscipleHood"),
            ["Paladin"] = ("RoyalHelm", "TM_MantleOfProtection"),
            ["Druid"] = ("TM_HoodOfProtection", "TM_HoodOfProtection"),
            ["Arcanist"] = ("ArcaneHood", "ArcaneHood"),
            ["Chronomancer"] = ("TM_WizardHat", "TM_WizardHat"),
            ["Enchanter"] = ("TM_WizardHat", "TM_WizardHat"),
            ["Geomancer"] = ("TM_HoodOfProtection", "TM_HoodOfProtection"),
            ["Summoner"] = ("TM_HoodOfProtection", "TM_HoodOfProtection"),
            ["InnerFire"] = ("TM_FireWand", "ArcaneHood"),
            ["HeartOfFrost"] = ("TM_IceWand", "ArcaneHood"),
            ["StormBorn"] = ("TM_LightningWand", "ArcaneHood"),
            ["TM_Bard"] = ("TM_WizardHat", "TM_WizardHat"),
            ["TM_Wanderer"] = ("TM_MaestersCloak", "TM_MaestersCloak"),
            ["TM_Monk"] = ("TM_DiscipleHood", "TM_DiscipleHood"),
        };

        /// <summary>Every piece <see cref="Garb"/> may give (for the tests' check against the player's mods).</summary>
        public static IEnumerable<string> AllGarb => garb.Values.SelectMany(g => new[] { g.old, g.later }).Distinct();

        /// <summary>The classes that have a piece of their own.</summary>
        public static IEnumerable<string> Garbed => garb.Keys;

        /// <summary>The piece a caster of this class wears or carries in this age (0 tribal to 3 spacer), or null.</summary>
        public static string Garb(string trait, int age)
        {
            if (trait == null || !garb.TryGetValue(trait, out var g)) return null;
            return age <= 1 ? g.old : g.later;
        }

        /// <summary>The piece is a wand (a weapon, held in place of one), not something worn.</summary>
        public static bool Wand(string def) => def != null && def.StartsWith("TM_", StringComparison.Ordinal) && def.EndsWith("Wand", StringComparison.Ordinal);

        // ================= A spell in peace =================

        /// <summary>What a helpful spell in peace is for.</summary>
        public enum Need
        {
            None,
            /// <summary>A colonist hurt: wounds that can still heal.</summary>
            Heal,
            /// <summary>A colonist with an infection or the flu (what the druid's cure takes at its first level).</summary>
            Cure,
            /// <summary>A colonist with an old scar.</summary>
            Scar,
            /// <summary>The land dry: a heat wave, a fire, or nothing fallen for a week, and not freezing.</summary>
            Rain
        }

        /// <summary>
        /// The helpful spells each class may cast for the colony in peace, in the order they are looked at (the most
        /// pressing first), by the mod's own ability names: the priest's Advanced Heal and Purify, the paladin's Heal, the
        /// druid's Cure Disease and Regenerate, the ice mage's Rainmaker. Every other class has none that is done at once
        /// and leaves nothing to keep up (an enchanter's, a geomancer's and a technomancer's workings last only while their
        /// maker keeps them, and a chronomancer's Accelerate Time ages anyone it touches).
        /// </summary>
        private static readonly Dictionary<string, (Need need, string spell)[]> peace = new Dictionary<string, (Need, string)[]>
        {
            ["Priest"] = new[] { (Need.Heal, "TM_AdvancedHeal"), (Need.Scar, "TM_Purify") },
            ["Paladin"] = new[] { (Need.Heal, "TM_Heal") },
            ["Druid"] = new[] { (Need.Cure, "TM_CureDisease"), (Need.Heal, "TM_Regenerate") },
            ["HeartOfFrost"] = new[] { (Need.Rain, "TM_Rainmaker") },
        };

        /// <summary>The helpful spells of a class, most pressing first (none for most).</summary>
        public static IReadOnlyList<(Need need, string spell)> PeaceSpells(string trait) =>
            trait != null && peace.TryGetValue(trait, out var s) ? s : Array.Empty<(Need, string)>();

        /// <summary>Every spell a visitor may cast in peace.</summary>
        public static IEnumerable<string> AllPeaceSpells => peace.Values.SelectMany(s => s.Select(x => x.spell)).Distinct();

        /// <summary>A class that may cast in peace at all.</summary>
        public static bool Helps(string trait) => trait != null && peace.ContainsKey(trait);

        /// <summary>The ability's own name without the mod's level mark ("TM_Heal_II" is TM_Heal).</summary>
        public static string BaseSpell(string def)
        {
            if (def == null) return null;
            foreach (var mark in new[] { "_III", "_II", "_I" })
                if (def.EndsWith(mark, StringComparison.Ordinal))
                    return def.Substring(0, def.Length - mark.Length);
            return def;
        }

        /// <summary>A visitor in good spirits: the game's mood at this or above.</summary>
        public const float ContentMood = 0.5f;

        /// <summary>
        /// A visitor may cast a helpful spell for the colony now: not hostile, up and about (not downed, in a mental state
        /// or leaving), no danger on the map, not one who has already cast this visit, and content on the map (their mood
        /// at <see cref="ContentMood"/> or more) or of a people allied with the colony (the player: "visitors should be
        /// able to cast benefitial spells if they are happy or allied with me").
        /// </summary>
        public static bool MayCastInPeace(bool hostile, bool allied, float mood, bool castThisVisit, bool danger, bool unable, bool leaving) =>
            !hostile && !castThisVisit && !danger && !unable && !leaving && (allied || !float.IsNaN(mood) && mood >= ContentMood);

        /// <summary>
        /// The chance an hour that a caster who may, and has someone or something to cast for, does: rare, a little less
        /// rare among the peoples built on magic. Most visits have no caster at all (a few in a hundred of most kinds).
        /// </summary>
        public static float PeaceChance(bool magicPeople) => magicPeople ? 0.06f : 0.03f;

        /// <summary>Hurt enough to be worth a healing spell: bleeding, or wounds that add up to this much.</summary>
        public const float HurtSeverity = 4f;

        public static bool HurtEnough(float woundSeverity, bool bleeding) => bleeding || !float.IsNaN(woundSeverity) && woundSeverity >= HurtSeverity;

        /// <summary>What the druid's Cure Disease takes at the mod's first level (Verb_CureDisease): wound infections, the flus and infections.</summary>
        public static bool CuredAtFirst(string hediff) =>
            hediff != null && (hediff == "WoundInfection" || hediff.Contains("Flu") || hediff.Contains("Infection"));

        /// <summary>The ice mage's rain is wanted: not raining or snowing now, not freezing (it would snow), and the land dry (a heat wave, a fire, or a week with nothing fallen).</summary>
        public static bool RainWanted(bool fallingNow, float outdoor, bool heatWave, bool fire, bool drySpell) =>
            !fallingNow && !float.IsNaN(outdoor) && outdoor > RainAbove && (heatWave || fire || drySpell);

        /// <summary>The Rainmaker brings snow below freezing; the colony only sees rain above this.</summary>
        public const float RainAbove = 3f;

        /// <summary>The weather record's requirement for a dry spell: nothing fallen these last days (WeatherMath.Fits, "dry:M").</summary>
        public const string DrySpell = "dry:7";

        /// <summary>How long Rimfolk waits for a spell it asked for to be cast; past this it lets the try go, saying nothing.</summary>
        public const int CastWaitTicks = 2500;

        /// <summary>Visitors who cast this visit, kept with the game (each one at most once a visit): at most this many.</summary>
        public const int MaxCast = 40;

        /// <summary>The target is near enough to walk to for a spell: within this many cells.</summary>
        public const float TargetRange = 60f;

        // ================= Said aloud =================

        /// <summary>A visitor names their craft in their first hours on the map (six), when someone of the colony is with them.</summary>
        public const int NameWindowTicks = 2500 * 6;

        /// <summary>The colony names a raider's craft when a colonist sees them this close, once a people a map every six hours.</summary>
        public const float FoeSightRange = 28f;

        public const int FoeGapTicks = 2500 * 6;

        /// <summary>The colony's card and letters name at most this many of a people's crafts, the commonest first.</summary>
        public const int AmongMax = 3;

        /// <summary>
        /// Which crafts a people is known for, from its kinds' class shares (trait and share, a kind's each): each class
        /// summed over the kinds, the commonest first, at most <paramref name="max"/>; ties by name, so the card reads the
        /// same each time. Nothing below a share of none.
        /// </summary>
        public static List<string> Among(IEnumerable<(string trait, float share)> gifts, int max = AmongMax)
        {
            if (gifts == null) return new List<string>();
            var sums = new Dictionary<string, float>();
            foreach (var (trait, share) in gifts)
            {
                if (trait == null || float.IsNaN(share) || share <= 0f) continue;
                sums.TryGetValue(trait, out float s);
                sums[trait] = s + share;
            }
            return sums.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(Math.Max(0, max)).Select(kv => kv.Key).ToList();
        }

        // ================= Kept out =================

        /// <summary>
        /// The mod's spells and techniques Rimfolk's peoples never use, as visitors, allies or raiders (the player: "some
        /// spells are quite broken, but let's give it a try"), with why: the masters' spells that wreck a base or a fight
        /// at a stroke, the ones that turn the colony's people into something else or bring the dead back, and the few
        /// fighters' techniques that kill outright or bring a wall down. Everything else the mod's own AI decides, by its
        /// own settings. A list to revisit after a play test.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> KeptOut = new Dictionary<string, string>
        {
            ["TM_Firestorm"] = "the fire mage's master spell: a storm of meteors that sets a whole base alight",
            ["TM_Blizzard"] = "the ice mage's master spell: a deadly blizzard over a wide area",
            ["TM_EyeOfTheStorm"] = "the lightning mage's master spell: a ball of lightning that annihilates everything in its path",
            ["TM_LightningStorm"] = "an uncontrolled lightning storm over an area: fires and deaths across a base",
            ["TM_Meteor"] = "the geomancer's master spell: a meteorite that buries a place",
            ["TM_HolyWrath"] = "the paladin's master spell: wrath on every enemy on the map at once",
            ["TM_OrbitalStrike"] = "the technomancer's master spell: an orbital bombardment",
            ["TM_FoldReality"] = "the arcanist's master spell: moves everything in a wide circle to another place",
            ["TM_ChronostaticField"] = "takes everyone in an area out of time, a fight decided at a stroke",
            ["TM_Resurrection"] = "the priest's master spell: a fallen raider brought back",
            ["TM_Polymorph"] = "turns the colony's people into beasts",
            ["TM_SummonPoppi"] = "the summoner's master spell: a pack of rabid poppi",
            ["TM_SummonElemental"] = "an elemental as strong as a small raid of its own",
            ["TM_Sabotage"] = "makes the colony's turrets, mechs and guns turn on themselves",
            ["TM_TechnoTurret"] = "a turret with rockets and mortars set down in the fight",
            ["TM_RaiseUndead"] = "the colony's dead raised against it",
            ["TM_LichForm"] = "an undying lich",
            ["TM_BloodMoon"] = "the blood mage's master spell",
            ["TM_Scorn"] = "the demonkin's master spell",
            ["TM_PsychicShock"] = "a psychic shock that drops everyone near",
            ["TM_Headshot"] = "the sniper's shot that kills outright",
            ["TM_60mmMortar"] = "a mortar barrage before the fight is even met",
            ["TM_PsionicStorm"] = "the psionic's storm over everyone near",
            ["TM_BreachingCharge"] = "brings a wall down at a throw",
            ["TM_GraveBlade"] = "the death knight's master technique: spirit blades over an area"
        };

        /// <summary>The spell or technique (by the mod's name, any level) is kept out of the peoples' hands.</summary>
        public static bool IsKeptOut(string def)
        {
            string b = BaseSpell(def);
            return b != null && KeptOut.ContainsKey(b);
        }
    }
}
