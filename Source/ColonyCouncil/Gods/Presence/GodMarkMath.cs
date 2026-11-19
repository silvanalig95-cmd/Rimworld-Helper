using System;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's mark worth having (DESIGN.md, "A god's mark worth having"; the player, 19 November 2026: "many of them just
    /// generate a single stone [chunk]?? it's a very anticlimatic item and doesn't thematically fit for most of them"): which
    /// marks leave goods, what they are worth by the colony's age (0 tribal to 3 spacer), how many of each thing that takes,
    /// the player's mods' things tried first with the game's own last, the wait between two marks of goods, and the words
    /// that say what was left. Free of game types so Tools/Tests checks them.
    /// </summary>
    public static class GodMarkMath
    {
        public const int Tribal = 0, Medieval = 1, Industrial = 2, Spacer = 3;

        /// <summary>An age from 0 tribal to 3 spacer.</summary>
        public static int Age(int age) => age < Tribal ? Tribal : age > Spacer ? Spacer : age;

        // ================= What it is worth =================

        /// <summary>The worth a mark of goods comes to, in silver, about: no less and no more.</summary>
        public const float WorthMin = 100f, WorthMax = 300f;

        private static readonly float[] Targets = { 120f, 160f, 210f, 260f };

        /// <summary>The worth a mark of goods aims at by the colony's age: 120 tribal, 160 medieval, 210 industrial, 260 spacer.</summary>
        public static float Target(int age) => Targets[Age(age)];

        /// <summary>The marks that leave goods (the rest only change the ground).</summary>
        public static bool HasWorth(GodMark m) =>
            m == GodMark.Kill || m == GodMark.Weapon || m == GodMark.StandingStone || m == GodMark.Hearth || m == GodMark.Metal || m == GodMark.Components;

        // ================= How often =================

        /// <summary>
        /// A mark of goods at most once in this many days, whichever god and home: six in a year of sixty days at the most. A
        /// moment within the wait leaves the mark's plain form (<see cref="Lesser"/>).
        /// </summary>
        public const float WorthGapDays = 10f;

        /// <summary>The most marks of goods in a year, and their most worth together.</summary>
        public static int MostAYear => (int)(60f / WorthGapDays);

        public static float MostWorthAYear => MostAYear * WorthMax;

        public static bool WorthDue(float daysSinceLast) => !float.IsNaN(daysSinceLast) && daysSinceLast >= WorthGapDays;

        /// <summary>
        /// A mark of goods within the wait: its plain form without them. The fire, the forge, the machine and war scorch the
        /// ground in a ring; the hunt and the way leave nothing.
        /// </summary>
        public static GodMark Lesser(GodMark m)
        {
            switch (m)
            {
                case GodMark.Weapon:
                case GodMark.Hearth:
                case GodMark.Metal:
                case GodMark.Components:
                    return GodMark.Scorch;
                case GodMark.Kill:
                case GodMark.StandingStone:
                    return GodMark.None;
                default:
                    return m;
            }
        }

        /// <summary>The mark a moment leaves now: a mark of goods within the wait gives way to its plain form.</summary>
        public static GodMark Allowed(GodMark m, float daysSinceLast) => HasWorth(m) && !WorthDue(daysSinceLast) ? Lesser(m) : m;

        // ================= How many =================

        /// <summary>
        /// How many of a thing worth <paramref name="unit"/> each come to <paramref name="target"/>: rounded (to fives from twenty
        /// up), within <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        public static int Count(float target, float unit, int min, int max)
        {
            if (unit <= 0f || float.IsNaN(unit) || float.IsNaN(target)) return min;
            float n = target / unit;
            int c = n >= 20f ? 5 * (int)Math.Round(n / 5f, MidpointRounding.AwayFromZero) : (int)Math.Round(n, MidpointRounding.AwayFromZero);
            return c < min ? min : c > max ? max : c;
        }

        // ---------------- The Craft: the colony's working metal ----------------

        /// <summary>The working metal by age, the first that is there taken: steel, plasteel in a spacer colony.</summary>
        public static string[] MetalFor(int age) => Age(age) >= Spacer ? new[] { "Plasteel", "Steel" } : new[] { "Steel" };

        public static int MetalCount(int age, float unit) => Count(Target(age), unit, 10, 150);

        // ---------------- The Machine: components ----------------

        public const string Component = "ComponentIndustrial", AdvancedComponent = "ComponentSpacer";

        /// <summary>
        /// The components by age: plain ones to the age's worth before the spacer age (four tribal, five medieval, seven
        /// industrial); in the spacer age one advanced component and plain ones to make up the rest.
        /// </summary>
        public static (int advanced, int plain) Components(int age, float plainWorth, float advancedWorth)
        {
            float target = Target(age);
            if (Age(age) >= Spacer && advancedWorth > 0f && advancedWorth <= target) return (1, Count(target - advancedWorth, plainWorth, 0, 4));
            return (0, Count(target, plainWorth, 2, 9));
        }

        // ---------------- The Hearth: firewood and warm meals ----------------

        public const string Firewood = "WoodLog";

        /// <summary>The bundle of firewood: this much wood.</summary>
        public const int FirewoodCount = 25;

        /// <summary>
        /// The warm meals of an age, any of these that the game has (Vanilla Cooking Expanded's stews, soups, grills and bakes
        /// first, the game's own meal last): simple ones tribal, fine ones medieval and industrial, lavish ones spacer.
        /// </summary>
        public static string[] MealsFor(int age)
        {
            switch (Age(age))
            {
                case Tribal: return new[] { "VCE_CookedStewSimple", "VCE_CookedSoupSimple", "MealSimple" };
                case Medieval: return new[] { "VCE_CookedStewFine", "VCE_CookedSoupFine", "VCE_FineBake", "MealFine" };
                case Industrial: return new[] { "VCE_FineGrill", "VCE_FineBake", "VCE_CookedSoupFine", "MealFine" };
                default: return new[] { "VCE_LavishGrill", "VCE_CookedStewLavish", "VCE_LavishBake", "MealLavish" };
            }
        }

        /// <summary>How many warm meals beside the firewood (worth <paramref name="woodWorth"/> each) make the age's worth: three to eight.</summary>
        public static int Meals(int age, float mealWorth, float woodWorth) => Count(Target(age) - FirewoodCount * Math.Max(0f, woodWorth), mealWorth, 3, 8);

        // ---------------- The Way: a standing stone ----------------

        /// <summary>
        /// The standing stone, the first of these the game has that stands on one cell and can be made of the land's stone
        /// blocks within the worth: Vanilla Furniture Expanded - Art's obelisk, else the game's small sculpture.
        /// </summary>
        public static readonly string[] Stones = { "VFE_Obelisk", "SculptureSmall" };

        public const string FallbackBlocks = "BlocksGranite";

        // ---------------- War: a weapon of the colony's age ----------------

        /// <summary>
        /// How near a weapon's age is to the colony's (both 0 tribal to 3 spacer): 0 its own age, 1 an earlier one (tried
        /// only when nothing of its own age is within the worth), -1 a later one (never).
        /// </summary>
        public static int WeaponTier(int weaponAge, int colonyAge)
        {
            if (weaponAge < 0) return -1;
            int a = Age(colonyAge);
            return weaponAge == a ? 0 : weaponAge < a ? 1 : -1;
        }

        /// <summary>The most a weapon left by a war god is worth in an age: the age's worth and a half again, never above <see cref="WorthMax"/>.</summary>
        public static float WeaponMost(int age) => Math.Min(WorthMax, Target(age) * 1.5f);

        /// <summary>A weapon of this worth (its material and quality counted) may be left in this age.</summary>
        public static bool WeaponFits(float worth, int age) => !float.IsNaN(worth) && worth >= WorthMin && worth <= WeaponMost(age);

        /// <summary>The chance it is of poor make rather than normal (never poor where that takes it under <see cref="WorthMin"/>).</summary>
        public const float WeaponPoorChance = 0.5f;

        // ---------------- The Hunt: a fresh kill ----------------

        /// <summary>The kill's body size: from a fox's to a deer's, never a large animal; wider only when the land has none such.</summary>
        public const float KillBodyMin = 0.5f, KillBodyMax = 1.5f, KillBodyMinWide = 0.3f, KillBodyMaxWide = 2f;

        public static bool KillFits(float bodySize, bool wide = false) =>
            !float.IsNaN(bodySize) && bodySize >= (wide ? KillBodyMinWide : KillBodyMin) && bodySize <= (wide ? KillBodyMaxWide : KillBodyMax);

        /// <summary>The share of a kill's meat and leather a butcher of middling skill gets (the game's butchery efficiency, about).</summary>
        public const float ButcherShare = 0.75f;

        /// <summary>What a kill is worth to the colony: its meat and leather at their prices, as much as a butcher gets of them.</summary>
        public static float KillWorth(float meatAndLeather) => float.IsNaN(meatAndLeather) || meatAndLeather <= 0f ? 0f : meatAndLeather * ButcherShare;

        /// <summary>A kill worth no more than a fifth over <see cref="WorthMax"/> (a deer is about that); the least of those tried where none is.</summary>
        public static bool KillWorthFits(float worth) => !float.IsNaN(worth) && worth > 0f && worth <= WorthMax * 1.2f;

        // ================= The words =================

        /// <summary>The words for what a mark left ("CC_GodP_Left_" and the mark; a weapon with no ring round it has its own).</summary>
        public static string WordsKey(GodMark m, bool alone = false) => m == GodMark.None ? null : "CC_GodP_Left_" + m + (m == GodMark.Weapon && alone ? "Alone" : "");

        /// <summary>
        /// The words of a moment with what it left after them (<paramref name="left"/>, a sentence of its own): either alone
        /// when the other is empty, else the two with a full stop between.
        /// </summary>
        public static string WithLeft(string words, string left)
        {
            string w = (words ?? "").Trim(), l = (left ?? "").Trim();
            if (l.Length == 0) return words;
            if (w.Length == 0) return l;
            if (!w.EndsWith(".") && !w.EndsWith("?") && !w.EndsWith("!") && !w.EndsWith("\"")) w += ".";
            return w + " " + l;
        }
    }
}
