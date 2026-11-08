using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What the colony and the world did with a people over the past year, as Rimfolk already records it (the game side,
    /// Peoples/PeopleAges.cs, reads it from the trade record, the peoples' memory, the history, the pacts, the world events
    /// and the ledger's ruins). Every field is plain numbers so the rule can be played out over years in Tools/Tests.
    /// </summary>
    public struct AgeDeeds
    {
        /// <summary>Silver the colony traded with them over the past year (both ways).</summary>
        public float trade;
        /// <summary>Silver of books, techprints, components and tools the colony gave them, as they remember it.</summary>
        public float gifts;
        /// <summary>Theirs who learned at the colony over the past year: apprentices hosted, wards fostered.</summary>
        public int learners;
        /// <summary>Their masters who taught the colony's people over the past year (an exchange of learning: they learn by it too).</summary>
        public int lessons;
        /// <summary>1 allied with the colony (the game's alliance or a pact of defence), 0.5 a trade pact, 0 none.</summary>
        public float bond;
        /// <summary>Years since anyone last traded with them: the colony, or a trade or road world event of theirs.</summary>
        public float yearsAlone;
        /// <summary>Their towns that fell in a fight in the past two years, and their towns standing now.</summary>
        public int burned, towns;
        /// <summary>The share of the world's peoples (the colony with them) at war with them, 0 to 1.</summary>
        public float war;
        /// <summary>Silver of components and parts the colony sold them over the past year (a people that declines unless supplied).</summary>
        public float supply;
    }

    /// <summary>A people's ways in the matter of its age (PeopleExtension.agePace, ageLimit, ageFloor, ageDeclines, ageDeclineYears).</summary>
    public struct AgeWays
    {
        /// <summary>Years an age takes at its own pace, with nothing from the colony.</summary>
        public float pace;
        /// <summary>The highest age it would accept by its ways (0 tribal to 3 spacer); and the lowest it falls back to.</summary>
        public int limit, floor;
        /// <summary>It declines unless supplied (the Sunken Vault's way), an age every so many years with nothing.</summary>
        public bool declines;
        public float declineYears;
    }

    /// <summary>What a season did to a people's age.</summary>
    public struct AgeStep
    {
        public int age;
        public float progress;
        /// <summary>+1 it rose an age this season, -1 it fell back one, 0 neither.</summary>
        public int change;
        /// <summary>Which way it is heading: +1 rising, -1 falling back, 0 settled or holding.</summary>
        public int heading;
        /// <summary>It is heading somewhere it can't go (its floor, its limit): it holds where it is.</summary>
        public bool blocked;
        /// <summary>Why, the strongest first (<see cref="PeopleAgesMath.Reasons"/>), joined by commas.</summary>
        public string why;
    }

    /// <summary>
    /// Peoples whose age changes (docs/design/peoples-third.md, "Ages that change over time"; DESIGN.md, "Peoples whose age
    /// changes"): the one rule, pure (no game types) so Tools/Tests (PeopleAgesTests) checks it and plays it out over years.
    /// Each season every one of Rimfolk's peoples moves toward its next age at its own pace (its ways: years an age, and the
    /// highest age it would take), faster for what the colony does with it over the year (trade, gifts of learning,
    /// teaching, an alliance), and back toward the age before when it is cut off from the world (no trade with anyone for
    /// years, its towns burned, at war with most of the world). A people of the Sunken Vault's way declines unless supplied.
    /// Progress runs from -1 (falls back an age) to +1 (rises one); a change starts it again from nothing.
    /// </summary>
    public static class PeopleAgesMath
    {
        public const int DayTicks = 60000;
        public const int SeasonDays = 15, YearDays = 60, SeasonsAYear = 4;
        public const int SeasonTicks = SeasonDays * DayTicks, YearTicks = YearDays * DayTicks;
        public const int Tribal = 0, Medieval = 1, Industrial = 2, Spacer = 3;

        /// <summary>Records kept: one a people standing (fifteen and the six that may arise), the gone ones let go first.</summary>
        public const int MaxRecords = 40;

        // ================= Ages and tech levels =================

        /// <summary>The game's tech level of an age (as numbers: 2 neolithic, 3 medieval, 4 industrial, 5 spacer).</summary>
        public static int TechOfAge(int age) => Clamp(age, 0, 3) + 2;

        /// <summary>The age of a tech level, as LedgerMath.AgeOf reads it.</summary>
        public static int AgeOfTech(int tech) => tech <= 2 ? 0 : tech == 3 ? 1 : tech == 4 ? 2 : 3;

        // ================= A people's ways =================

        /// <summary>The highest age it would accept: its ways' limit, but never below the age it began in (unset: one above it).</summary>
        public static int Limit(int limit, int start) => limit < 0 ? Clamp(start + 1, 0, 3) : Clamp(Math.Max(limit, start), 0, 3);

        /// <summary>The lowest age it falls back to: its ways' floor, else one below the age it began in.</summary>
        public static int Floor(int floor, int start) => floor >= 0 ? Clamp(Math.Min(floor, start), 0, 3) : Math.Max(0, start - 1);

        /// <summary>Its ways are sound: a pace, a limit within the ages, and room to rise at least one age unless it is spacer already.</summary>
        public static bool Sound(float pace, int limit, int own, bool declines, float declineYears) =>
            !float.IsNaN(pace) && pace > 0f && limit >= 0 && limit <= 3 && (limit > own || own >= Spacer) && (!declines || declineYears > 0f);

        /// <summary>
        /// Every age a people of this template can reach: from each age it can begin in (its own, or any of the span it
        /// arises in), down to its floor and up to its limit.
        /// </summary>
        public static List<int> Reachable(int own, int ariseLo, int ariseHi, int limit, int floor)
        {
            var starts = new List<int> { own };
            if (ariseLo >= 0 && ariseHi >= ariseLo)
                for (int a = ariseLo; a <= ariseHi; a++)
                    starts.Add(a);
            var ages = new SortedSet<int>();
            foreach (int s in starts)
                for (int a = Floor(floor, s); a <= Limit(limit, s); a++)
                    ages.Add(a);
            return ages.ToList();
        }

        // ================= Its fate =================

        /// <summary>How much faster or slower than its ways' pace this people goes: 0.8 to 1.25, the same each time (by its id).</summary>
        public static float Fate(int id) => 0.8f + 0.45f * Unit(id, 0x9E3779B9u);

        /// <summary>The way it had already come toward its next age when the colony first heard of it: 0 to 0.2.</summary>
        public static float HeadStart(int id) => 0.2f * Unit(id, 0x85EBCA6Bu);

        private static float Unit(int id, uint salt)
        {
            uint h = unchecked((uint)id * 2654435761u ^ salt);
            h ^= h >> 15;
            h = unchecked(h * 0x2C1B3C6Du);
            h ^= h >> 12;
            h = unchecked(h * 0x297A2D39u);
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>A people's look comes once a season, each on a day of its own (by its id).</summary>
        public static bool LookDay(int day, int id) => day >= 0 && (day + (id & 0x7fffffff) % SeasonDays) % SeasonDays == 0;

        // ================= Rising =================

        /// <summary>How steep the next step is: the first comes easiest, the climb to the spacer age is the longest.</summary>
        public static float StepFactor(int age) => age <= Tribal ? 1.2f : age == Medieval ? 1f : age == Industrial ? 0.7f : 0f;

        /// <summary>Trade with the colony: up to twice the pace at this much silver a year.</summary>
        public const float TradeFull = 5000f, TradeMax = 1f;
        /// <summary>Gifts of books, techprints, components and tools: up to this much more at this much silver remembered.</summary>
        public const float GiftFull = 1500f, GiftMax = 0.6f;
        /// <summary>Teaching: so much more for each of theirs who learned at the colony this year, half for each lesson they gave, at most this.</summary>
        public const float LearnerWorth = 0.4f, LessonWorth = 0.2f, TeachMax = 0.8f;
        /// <summary>An alliance with the colony (a trade pact half of it).</summary>
        public const float BondWorth = 0.5f;
        /// <summary>The most the colony can quicken a people: three times its own pace.</summary>
        public const float MaxBoost = 3f;
        /// <summary>Under this a reason is too small to name.</summary>
        private const float Named = 0.04f;

        /// <summary>
        /// How much the colony quickens a people's rise, 1 (its own pace) to <see cref="MaxBoost"/>, and the reasons it
        /// names, the largest first; with nothing from the colony the reason is the years ("years").
        /// </summary>
        public static float Boost(AgeDeeds d, List<(string key, float size)> why)
        {
            float trade = Math.Min(TradeMax, Pos(d.trade) / TradeFull);
            float gifts = Math.Min(GiftMax, Pos(d.gifts) / GiftFull * GiftMax);
            float teach = Math.Min(TeachMax, LearnerWorth * Math.Max(0, d.learners) + LessonWorth * Math.Max(0, d.lessons));
            float bond = BondWorth * Clamp01(d.bond);
            if (why != null)
            {
                var mine = new List<(string, float)>();
                if (trade >= Named) mine.Add(("trade", trade));
                if (gifts >= Named) mine.Add(("gifts", gifts));
                if (teach >= Named) mine.Add(("teaching", teach));
                if (bond >= Named) mine.Add((d.bond >= 1f ? "ally" : "pact", bond));
                if (mine.Count == 0) mine.Add(("years", 1f));
                why.AddRange(mine.OrderByDescending(x => x.Item2));
            }
            return Math.Min(MaxBoost, 1f + trade + gifts + teach + bond);
        }

        /// <summary>Its own progress in a season toward the next age, before what the colony adds.</summary>
        public static float OwnPace(float pace, int age, float fate) =>
            pace <= 0f || float.IsNaN(pace) ? 0f : Pos(fate) * StepFactor(age) / (pace * SeasonsAYear);

        // ================= Falling back =================

        /// <summary>No trade with anyone for this many years begins to cut a people off; this many cuts it off wholly.</summary>
        public const float AloneFrom = 2f, AloneFull = 4f;
        /// <summary>Towns that fell in a fight count this many years.</summary>
        public const int BurnedYears = 2;
        /// <summary>At war with more than this share of the world begins to cut it off; this share wholly.</summary>
        public const float WarFrom = 0.5f, WarFull = 0.85f;
        /// <summary>The three signs together reach this and the people is cut off (more than one of them, or one whole and another begun).</summary>
        public const float CutOffAt = 1.2f;
        /// <summary>Cut off wholly, a people falls back an age in this many years; just cut off, in twice that.</summary>
        public const float FallYears = 5f;

        /// <summary>
        /// How cut off from the world a people is, 0 to 3, the three signs added (each 0 to 1): no trade with anyone for
        /// years, its towns burned (one of two, or its last one, is all of it), at war with most of the world; and the
        /// signs it names, the largest first.
        /// </summary>
        public static float CutOff(AgeDeeds d, List<(string key, float size)> why)
        {
            // A people the colony traded with this year is not alone, whatever else.
            float alone = d.trade > 0f ? 0f : Clamp01((d.yearsAlone - AloneFrom) / (AloneFull - AloneFrom));
            float burned = d.burned <= 0 ? 0f : d.towns <= 1 ? 1f : Math.Min(1f, 0.5f * d.burned);
            float war = Clamp01((d.war - WarFrom) / (WarFull - WarFrom));
            if (why != null)
            {
                var mine = new List<(string, float)>();
                if (alone > 0f) mine.Add(("alone", alone));
                if (burned > 0f) mine.Add(("burned", burned));
                if (war > 0f) mine.Add(("war", war));
                why.AddRange(mine.OrderByDescending(x => x.Item2));
            }
            return alone + burned + war;
        }

        /// <summary>How fast a people that is cut off falls back: none under <see cref="CutOffAt"/>, a half at it, all at the three signs whole.</summary>
        public static float Severity(float cutOff) => float.IsNaN(cutOff) || cutOff < CutOffAt ? 0f : Math.Min(1f, cutOff / 3f + 0.1f);

        /// <summary>A people that declines unless supplied holds its age at this much silver of parts a year.</summary>
        public const float SupplyFull = 1500f;

        /// <summary>How well supplied a declining people is: 0 to 1, by the parts traded to it and the gifts it remembers.</summary>
        public static float Supplied(AgeDeeds d) => Clamp01((Pos(d.supply) + Pos(d.gifts)) / SupplyFull);

        // ================= The season =================

        /// <summary>The reasons the rule names, as the texts know them (CC_PeopleAge_Why_KEY).</summary>
        public static readonly string[] Reasons = { "trade", "gifts", "teaching", "ally", "pact", "years", "alone", "burned", "war", "unsupplied", "supplied", "settled", "height" };

        /// <summary>
        /// A season of a people's life: its age, its progress (-1 to 1) and its ways, how fast its fate makes it, and what
        /// the colony and the world did with it over the past year. A people that declines (the Sunken Vault's way) loses
        /// ground unless supplied, and never rises on its own. One that is cut off loses ground and doesn't rise. Any other
        /// rises at its pace, quickened by the colony; at its limit it is settled in its ways (at the spacer age, at the
        /// height of what anyone knows), and comes back up to nothing if it had lost ground. Past +1 it rises an age, past
        /// -1 it falls back one (never past its limit or its floor), and its progress starts again from nothing.
        /// </summary>
        public static AgeStep Season(int age, float progress, AgeWays w, float fate, AgeDeeds d)
        {
            age = Clamp(age, 0, 3);
            if (float.IsNaN(progress)) progress = 0f;
            int limit = Clamp(Math.Max(w.limit, 0), 0, 3), floor = Clamp(Math.Max(w.floor, 0), 0, 3);
            var why = new List<(string key, float size)>();
            float delta;
            bool settled = false;
            if (w.declines)
            {
                float s = Supplied(d);
                delta = w.declineYears > 0f ? -(1f - s) / (w.declineYears * SeasonsAYear) : 0f;
                why.Add(s < 1f ? ("unsupplied", 1f - s) : ("supplied", s));
            }
            else
            {
                var cut = new List<(string key, float size)>();
                float severity = Severity(CutOff(d, cut));
                if (severity > 0f)
                {
                    delta = -severity / (FallYears * SeasonsAYear);
                    why.AddRange(cut);
                }
                else
                {
                    var up = new List<(string key, float size)>();
                    delta = OwnPace(w.pace, age, fate) * Boost(d, up);
                    if (age >= limit)
                    {
                        // Settled in its ways: it comes back up to nothing if it had lost ground, and goes no further.
                        delta = progress < 0f ? Math.Min(delta, -progress) : 0f;
                        settled = true;
                        why.Add((age >= Spacer ? "height" : "settled", 1f));
                    }
                    else why.AddRange(up);
                }
            }
            var step = new AgeStep { age = age, progress = progress + delta, heading = settled ? 0 : delta > 1e-6f ? 1 : delta < -1e-6f ? -1 : 0 };
            if (step.progress >= 1f && age < limit)
            {
                step.age = age + 1;
                step.progress = 0f;
                step.change = 1;
            }
            else if (step.progress <= -1f && age > floor)
            {
                step.age = age - 1;
                step.progress = 0f;
                step.change = -1;
            }
            else
            {
                if (step.progress > 0.99f) step.progress = age < limit ? 0.99f : 0f;
                if (step.progress < -0.99f) step.progress = -0.99f;
                if (age >= limit && step.progress > 0f) step.progress = 0f;
                step.blocked = step.heading > 0 && age >= limit || step.heading < 0 && age <= floor;
            }
            step.why = string.Join(",", why.Select(x => x.key));
            return step;
        }

        // ================= The traders' goods =================

        /// <summary>
        /// A stock generator's tech cap for a people whose age moved from its def's (tech levels as numbers): a cap set for
        /// its own age moves with it, as far as the age moved; an uncapped one (archotech) stays for a people that rose and
        /// becomes the age's own for one that fell back, which can't make what it lost. The game's undefined stays.
        /// </summary>
        public static int ShiftedCap(int cap, int defTech, int ageTech)
        {
            if (cap <= 0 || ageTech == defTech || ageTech <= 0 || defTech <= 0) return cap;
            int delta = ageTech - defTech;
            if (cap >= 7) return delta < 0 ? Math.Max(2, ageTech) : cap;
            return Clamp(cap + delta, 2, 7);
        }

        // ================= Every age whole =================

        /// <summary>
        /// A thing of a later age than a people lives in (its tech level as a number; undefined is of every age): for a
        /// tribal or medieval people, anything industrial or later (the tribes trade for cloth and iron, never for guns);
        /// for an industrial one, anything of the spacer age; nothing for a spacer one.
        /// </summary>
        public static bool Later(int tech, int age)
        {
            if (tech <= 0) return false;
            int a = AgeOfTech(tech);
            return age >= Industrial ? a > age : a >= Industrial;
        }

        /// <summary>
        /// The game's own weapon tags a people of an age carries when what its kind was given is of a later age (a
        /// medieval scholar's revolver) or a fighter's bow in an age of guns: the base game's arms of that age, by what the
        /// pawn does (a fighter at range, a fighter close in, anyone else).
        /// </summary>
        public static string[] FallbackArms(int age, bool fighter, bool ranged)
        {
            switch (Clamp(age, 0, 3))
            {
                case Tribal: return fighter ? ranged ? new[] { "NeolithicRangedDecent", "NeolithicRangedBasic" } : new[] { "NeolithicMeleeAdvanced", "NeolithicMeleeDecent" } : new[] { "NeolithicMeleeBasic" };
                case Medieval: return fighter ? ranged ? new[] { "NeolithicRangedDecent", "NeolithicRangedHeavy" } : new[] { "MedievalMeleeDecent", "MedievalMeleeAdvanced" } : new[] { "MedievalMeleeBasic", "NeolithicMeleeBasic" };
                case Industrial: return fighter ? ranged ? new[] { "Gun" } : new[] { "MedievalMeleeAdvanced", "Gun" } : new[] { "SimpleGun" };
                default: return fighter ? ranged ? new[] { "SpacerGun", "Gun" } : new[] { "Gun" } : new[] { "SimpleGun" };
            }
        }

        /// <summary>A fighter's bow (a ranged weapon of the tribal or medieval age) in the industrial or spacer age: it takes a gun of the age.</summary>
        public static bool BowInGunAge(int weaponTech, bool ranged, bool fighter, int age) => fighter && ranged && age >= Industrial && weaponTech > 0 && AgeOfTech(weaponTech) <= Medieval;

        /// <summary>The game's plain clothes of an age that cover a body left bare when its later pieces come off: tribal wear, or a shirt and trousers.</summary>
        public static string[] FallbackDress(int age) => age <= Tribal ? new[] { "Apparel_TribalA" } : new[] { "Apparel_BasicShirt", "Apparel_Pants" };

        // ================= What counts =================

        /// <summary>A gift of learning: what kind of thing, by the thing's own nature (a book, a techprint) or its name and tags; null for none.</summary>
        public static string LearnedKind(string defName, bool book, bool techprint, IEnumerable<string> categories, IEnumerable<string> weaponTags)
        {
            if (techprint) return "techprints";
            if (book) return "books";
            if (!string.IsNullOrEmpty(defName) && defName.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0) return "components";
            if (categories != null && categories.Any(c => c != null && c.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0)) return "components";
            if (weaponTags != null && weaponTags.Any(t => t != null && t.StartsWith("Tool", StringComparison.Ordinal))) return "tools";
            if (categories != null && categories.Any(c => c != null && c.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0)) return "tools";
            return null;
        }

        /// <summary>A part a people that declines unless supplied needs: components (a mod's too) and the tools it keeps them with.</summary>
        public static bool Part(string kind) => kind == "components" || kind == "tools";

        /// <summary>The deed of a gift of learning weighs a point for every so much silver of it (2 to 30), and is read back the same way.</summary>
        public const float GiftSilverPerWeight = 60f;

        public static int GiftWeight(float value) => float.IsNaN(value) || value <= 0f ? 0 : Clamp((int)Math.Round(value / GiftSilverPerWeight), 2, 30);

        /// <summary>A trade the peoples' memory counts (200 silver at least, Patch_FactionMemory) stands for this much when the trade record has less.</summary>
        public const float TradeDeedSilver = 300f;

        /// <summary>The kinds of deed that are theirs learning at the colony, and their masters teaching the colony's people.</summary>
        public static readonly string[] LearnerDeeds = { "learnedHere", "fostered" }, LessonDeeds = { "taught" };

        /// <summary>The missions in which one of theirs stays at the colony to learn (the Conclave's apprentice of the sky, the guild's wander-year).</summary>
        public static readonly string[] LearnerMissions = { "glassApprentice", "wanderYear" };

        // ================= Small helpers =================

        private static float Pos(float v) => float.IsNaN(v) || v < 0f ? 0f : v;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
