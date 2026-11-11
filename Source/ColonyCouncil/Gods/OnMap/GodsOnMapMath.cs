using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What comes to the map when a god does (docs/design/gods.md, "Stage 2, on the map").</summary>
    public enum ComingKind
    {
        None,
        /// <summary>A lone traveller asks for shelter: a god in disguise (brainstorm 7, theoxenia).</summary>
        Stranger,
        /// <summary>A god walks as a beast: a white stag, a great wolf, a raven that follows one colonist (brainstorm 8).</summary>
        Animal
    }

    /// <summary>The signs in the days before a god comes (brainstorm 23).</summary>
    public enum OmenKind
    {
        /// <summary>The land's birds leave it, all at once.</summary>
        Birds,
        /// <summary>A strange calm: the wind dies for a few hours.</summary>
        Calm,
        /// <summary>Two colonists wake from the same dream.</summary>
        Dream,
        /// <summary>A lamp or a torch in the colony won't stay lit.</summary>
        Candle,
        /// <summary>The animals are restless: they start up and run a few steps, then stand looking one way.</summary>
        Restless
    }

    /// <summary>How the colony answered the stranger at the gate.</summary>
    public enum StrangerWelcome
    {
        /// <summary>Sent on their way.</summary>
        TurnedAway,
        /// <summary>Let to sleep outside by the gate, and no more.</summary>
        Gate,
        /// <summary>Given a bed for the night and a place at the table.</summary>
        Bed
    }

    /// <summary>How the stranger was treated, all told, worst first.</summary>
    public enum Treatment
    {
        /// <summary>Struck or robbed by the colony's own.</summary>
        Harmed,
        TurnedAway,
        Poor,
        Fair,
        Kind,
        Generous
    }

    /// <summary>How a god shows itself at the end of a coming, by how real the gods are.</summary>
    public enum GodShow
    {
        /// <summary>Not at all: nothing supernatural happens (Believed).</summary>
        None,
        /// <summary>Never so plainly that nobody could doubt it: gone before anyone can be sure, "some say" (Felt).</summary>
        Veiled,
        /// <summary>Openly, named for what it is (Walking).</summary>
        Plain
    }

    /// <summary>The lasting gift the kindest colonist gets from a god pleased with them.</summary>
    public enum GodGift
    {
        None,
        /// <summary>One of A RimWorld of Magic's spells of the god's domain, for one who already casts.</summary>
        Spell,
        /// <summary>One of A RimWorld of Magic's classes of the god's domain.</summary>
        Class,
        /// <summary>A burning passion for the domain's work.</summary>
        Passion,
        /// <summary>A trait of the domain.</summary>
        Trait,
        /// <summary>The god's favour: the colony stands better with it for their sake, and a spark of inspiration.</summary>
        Favour
    }

    /// <summary>A god answering in the weather (brainstorm 9).</summary>
    public enum WeatherVoice
    {
        None,
        /// <summary>Rain after the harvest's god is pleased.</summary>
        Rain,
        /// <summary>Thunder on cue for a god of storms.</summary>
        Thunder,
        /// <summary>An eclipse for a dark god's anger.</summary>
        Eclipse,
        /// <summary>An aurora on a god's night.</summary>
        Aurora
    }

    /// <summary>What a sacred animal leads someone to.</summary>
    public enum AnimalBoon
    {
        /// <summary>Something left there: a find of the colony's age.</summary>
        Find,
        /// <summary>The one who followed it is eased of a hurt or an illness.</summary>
        Cure,
        /// <summary>A vein of ore in the rock that nobody had seen.</summary>
        Vein
    }

    /// <summary>
    /// What the colony did for the stranger over their stay, as the rules see it (Gods/OnMap/GodStranger.cs reads it off the
    /// map): how they were let in, where they slept, what they ate, who talked with them and did them a kindness, how the
    /// animals were kept, and whether anyone struck or robbed them.
    /// </summary>
    public struct StayTally
    {
        public StrangerWelcome welcome;
        public bool harmed, robbed;
        /// <summary>How the visit went for them, 0 to 1: their mood over the stay, or Hospitality's own score for its guest.</summary>
        public float content;
        /// <summary>The best they ate: 0 nothing, 1 raw, 2 a simple meal, 3 a fine one, 4 a lavish one.</summary>
        public int ate;
        /// <summary>Where they slept: -1 in the open, 0 a corner indoors, 1 a bed, 2 a bed in a good room.</summary>
        public int slept;
        /// <summary>How many of the colony talked with them.</summary>
        public int talkers;
        /// <summary>Kindnesses done them: a meal or a drink shared, a gift, a wound tended.</summary>
        public int kindnesses;
        /// <summary>The colony's animals went hungry, untended or cold while they stayed.</summary>
        public bool animalsNeglected;
        /// <summary>Animals the colony slaughtered while they stayed.</summary>
        public int slaughtered;
    }

    /// <summary>
    /// The pure rules of the gods on the map (Gods/OnMap/; docs/design/gods.md, "On the map"; brainstorm 7, 8, 9, 23 and 26):
    /// how often a god comes and in which shape, the omens in the days before, how the stranger's stay is judged and what
    /// it brings, the kindest colonist and their gift, the sacred animal's stay and what it leads to, the weather a god
    /// answers in, and the marks remembered and visited. Free of game types so Tools/Tests try them.
    /// </summary>
    public static class GodsOnMapMath
    {
        // ================= How often a god comes =================

        /// <summary>Not before the colony's this many days on its land.</summary>
        public const int EarliestDay = 20;

        /// <summary>Days between any two comings of the gods on the colony's land (a stranger or a sacred animal).</summary>
        public const int AnyGapDays = 40;

        /// <summary>Days between two strangers at the gate, and two sacred animals.</summary>
        public const int StrangerGapDays = 120, AnimalGapDays = 90;

        /// <summary>Once the gaps are past, the chance a day that a god sets out (about one in thirty days).</summary>
        public const float DailyChance = 0.033f;

        /// <summary>Days of omens before a coming, at least and at most.</summary>
        public const int OmenDaysMin = 2, OmenDaysMax = 4;

        /// <summary>
        /// Whether a god sets out for the colony today, and as what: only where avatars come at all (Felt and Walking; never
        /// in Believed), after the colony's first days, the gaps past, and the day's roll (<paramref name="roll"/> in [0, 1))
        /// under <see cref="DailyChance"/>. A stranger needs a people at peace to come from (<paramref name="strangerCan"/>),
        /// an animal a god whose look walks as one that can live here (<paramref name="animalCan"/>); when both may come, the
        /// one longest gone (the stranger on a tie), decided by <paramref name="pick"/> in [0, 1) when they are as long gone.
        /// </summary>
        public static ComingKind Due(GodRealness r, int day, int daysSinceAny, int daysSinceStranger, int daysSinceAnimal, bool strangerCan, bool animalCan, float roll,
            float pick)
        {
            if (!GodsMath.AvatarsCome(r) || day < EarliestDay || daysSinceAny < AnyGapDays || float.IsNaN(roll) || roll >= DailyChance) return ComingKind.None;
            bool s = strangerCan && daysSinceStranger >= StrangerGapDays;
            bool a = animalCan && daysSinceAnimal >= AnimalGapDays;
            if (s && a)
            {
                float sw = Math.Min(daysSinceStranger, 1000) / (float)StrangerGapDays, aw = Math.Min(daysSinceAnimal, 1000) / (float)AnimalGapDays;
                return pick * (sw + aw) < sw ? ComingKind.Stranger : ComingKind.Animal;
            }
            return s ? ComingKind.Stranger : a ? ComingKind.Animal : ComingKind.None;
        }

        /// <summary>How a god shows itself at a coming's end, by how real the gods are.</summary>
        public static GodShow Show(GodRealness r) => r == GodRealness.Walking ? GodShow.Plain : r == GodRealness.Felt ? GodShow.Veiled : GodShow.None;

        /// <summary>
        /// How likely a god of a domain is the stranger at the gate: the road's gods and the hearth's most (the oldest
        /// story of the stranger is a god of guests), the colony's own gods more than others, its patron more again; a god
        /// that can't walk (dead, imprisoned) never.
        /// </summary>
        public static float StrangerWeight(GodDomain d, GodDomain second, GodKind kind, bool colonyGod, bool patron, bool known)
        {
            if (kind == GodKind.Dead || kind == GodKind.Imprisoned) return 0f;
            float w = 1f;
            if (d == GodDomain.Way || second == GodDomain.Way) w *= 3f;
            else if (d == GodDomain.Hearth || second == GodDomain.Hearth) w *= 2f;
            if (patron) w *= 2.5f;
            else if (colonyGod) w *= 1.8f;
            else if (!known) w *= 0.4f;
            if (kind == GodKind.Archotech) w *= 0.5f;
            if (kind == GodKind.Fading) w *= 0.7f;
            return w;
        }

        /// <summary>
        /// How likely a god walks as a beast: those of the wild and the hunt most, then the rest whose look has beasts
        /// that can live here (<paramref name="beasts"/>); the colony's own and its patron more.
        /// </summary>
        public static float AnimalWeight(GodDomain d, GodKind kind, int beasts, bool colonyGod, bool patron, bool known)
        {
            if (beasts <= 0 || kind == GodKind.Dead || kind == GodKind.Imprisoned) return 0f;
            float w = d == GodDomain.Wild || d == GodDomain.Hunt ? 3f : d == GodDomain.Harvest || d == GodDomain.Night || d == GodDomain.Storm ? 1.5f : 1f;
            if (patron) w *= 2.5f;
            else if (colonyGod) w *= 1.8f;
            else if (!known) w *= 0.4f;
            return w;
        }

        // ================= The omens =================

        /// <summary>At most this many omens before one coming.</summary>
        public const int MaxOmens = 4;

        /// <summary>
        /// The omens over <paramref name="days"/> days before a coming: one a day (two on the last day when there are more
        /// days' worth of omens than days), at most <see cref="MaxOmens"/>, each of a kind that can show on this map
        /// (<paramref name="can"/>, by OmenKind), no kind twice while another is left; picked in turn by
        /// <paramref name="seed"/>. Empty when none can show.
        /// </summary>
        public static List<OmenKind> Omens(int days, Func<OmenKind, bool> can, int seed)
        {
            var list = new List<OmenKind>();
            if (days <= 0 || can == null) return list;
            var open = new List<OmenKind>();
            foreach (OmenKind k in Enum.GetValues(typeof(OmenKind)))
                if (can(k))
                    open.Add(k);
            if (open.Count == 0) return list;
            int n = Math.Min(MaxOmens, Math.Max(1, days));
            var left = new List<OmenKind>(open);
            for (int i = 0; i < n; i++)
            {
                if (left.Count == 0) left.AddRange(open);
                int at = Mod(Seed(seed, i), left.Count);
                list.Add(left[at]);
                left.RemoveAt(at);
            }
            return list;
        }

        /// <summary>
        /// The hour of the day each omen of <paramref name="count"/> over <paramref name="days"/> days comes at, as ticks
        /// after the first: one a day, in the day's waking hours (a dream at dawn, when people wake), spread so the last
        /// is on the coming's eve.
        /// </summary>
        public static int OmenAt(int index, int count, int days, OmenKind kind, int seed, int ticksPerDay = 60000)
        {
            if (count <= 0 || days <= 0) return 0;
            int day = count >= days ? Math.Min(days - 1, index) : (int)Math.Round(index * (days - 1) / (double)Math.Max(1, count - 1));
            int hour = kind == OmenKind.Dream ? 6 + Mod(Seed(seed, index, 7), 2) : kind == OmenKind.Candle ? 19 + Mod(Seed(seed, index, 3), 3) : 9 + Mod(Seed(seed, index, 11), 8);
            return day * ticksPerDay + hour * (ticksPerDay / 24);
        }

        /// <summary>The dream omen needs two colonists who slept the night; the candle one a light in the colony; the birds one birds on the land.</summary>
        public static bool DreamCan(int sleepers) => sleepers >= 2;

        // ================= The stranger at the gate =================

        /// <summary>How long the stranger stays when let in or let sleep by the gate, in hours (a night and the day after, about).</summary>
        public const float StayHoursMin = 22f, StayHoursMax = 32f;

        /// <summary>How long the colony has to answer, in days.</summary>
        public const float AnswerDays = 0.4f;

        /// <summary>How long a stranger's farewell waits for them to stop and turn, in ticks (about half a minute at normal speed).</summary>
        public const int FarewellTicks = 1500;

        /// <summary>
        /// How the stranger was treated, all told. Struck or robbed is the worst whatever else; turned away next. The rest by a
        /// score: the bed given (+2), where they slept (-1 in the open, +1 a bed, +2 a good room), the best they ate (-2 nothing,
        /// -1 raw, 0 simple, +1 fine, +2 lavish), how many talked with them (0.7 each, three at most) and the kindnesses done
        /// them (one each, three at most), how the visit went for them (-2 to +2), less for animals neglected (1.5) and
        /// slaughtered under their eyes (1).
        /// </summary>
        public static Treatment Judge(StayTally t) => Judge(t, out _);

        public static Treatment Judge(StayTally t, out float score)
        {
            score = 0f;
            if (t.harmed || t.robbed) return Treatment.Harmed;
            if (t.welcome == StrangerWelcome.TurnedAway) return Treatment.TurnedAway;
            score += t.welcome == StrangerWelcome.Bed ? 2f : 0f;
            score += t.slept < 0 ? -1f : t.slept >= 2 ? 2f : t.slept;
            score += t.ate <= 0 ? -2f : t.ate - 2f;
            score += Math.Min(3, Math.Max(0, t.talkers)) * 0.7f;
            score += Math.Min(3, Math.Max(0, t.kindnesses));
            float c = float.IsNaN(t.content) ? 0.5f : Math.Max(0f, Math.Min(1f, t.content));
            score += (c - 0.5f) * 4f;
            if (t.animalsNeglected) score -= 1.5f;
            if (t.slaughtered > 0) score -= 1f;
            if (score < -1f) return Treatment.Poor;
            if (score < 2.5f) return Treatment.Fair;
            if (score < 5.5f) return Treatment.Kind;
            return Treatment.Generous;
        }

        /// <summary>The favour the stranger's god takes from how it was treated (before its nature): the worst a sacrilege.</summary>
        public static float FavourFor(Treatment t) => t switch
        {
            Treatment.Harmed => -30f,
            Treatment.TurnedAway => -10f,
            Treatment.Poor => -4f,
            Treatment.Fair => 5f,
            Treatment.Kind => 12f,
            _ => 20f
        };

        /// <summary>Where a treatment's lost favour comes from, for what offends the god's nature: struck is sacrilege, the rest neglect of a guest.</summary>
        public static FavourSource SourceOf(Treatment t) => t == Treatment.Harmed ? FavourSource.Sacrilege : FavourSource.Deed;

        /// <summary>The offence a treatment is (turned away and poorly kept: the stranger turned away).</summary>
        public static GodOffence OffenceOf(Treatment t) => t == Treatment.TurnedAway || t == Treatment.Poor ? GodOffence.Stinginess : GodOffence.None;

        /// <summary>The colony is blessed: a find or the domain's gift (fair and better).</summary>
        public static bool Blesses(Treatment t) => t >= Treatment.Fair;

        /// <summary>The kindest colonist gets a lasting gift (kind and better).</summary>
        public static bool Gives(Treatment t) => t >= Treatment.Kind;

        /// <summary>The colony is cursed in the domain (struck, or turned away).</summary>
        public static bool Curses(Treatment t) => t <= Treatment.TurnedAway;

        /// <summary>The moment at the end: a wrath for the struck and the turned away, a blessing for the kind, a departure between.</summary>
        public static PresenceKind EndKind(Treatment t) => t <= Treatment.TurnedAway ? PresenceKind.Wrath : t >= Treatment.Kind ? PresenceKind.Blessing : PresenceKind.Depart;

        /// <summary>The words of the end, by the key's ending (CC_GodM_Stranger_End_*).</summary>
        public static string EndWord(Treatment t) => t.ToString();

        // ---- Who was kindest ----

        /// <summary>What each kindness is worth toward being the kindest: a word (1), a long talk (2), a meal or drink shared (3), a gift given (3), a wound tended (4), the one who let them in (2); a harsh word -3, a blow -10.</summary>
        public const int Word = 1, Talk = 2, Shared = 3, Given = 3, Tended = 4, LetIn = 2, Harsh = -3, Blow = -10;

        /// <summary>The kindest is the one with the most, from this many up (a word or two is not yet kindness).</summary>
        public const int KindestFrom = 3;

        /// <summary>The kindest of the colony: the most points from <see cref="KindestFrom"/> up; -1 for nobody.</summary>
        public static int Kindest(IList<int> points)
        {
            int best = -1, most = KindestFrom - 1;
            if (points == null) return -1;
            for (int i = 0; i < points.Count; i++)
                if (points[i] > most)
                {
                    most = points[i];
                    best = i;
                }
            return best;
        }

        /// <summary>
        /// The kindest colonist's gift: a spell of the domain for one who already casts (A RimWorld of Magic), sometimes a
        /// class of the domain for one with none, a passion for the domain's work they lack, a trait of the domain they lack,
        /// else the god's favour; <paramref name="roll"/> in [0, 1) chooses among those open.
        /// </summary>
        public static GodGift Gift(bool spellOpen, bool classOpen, bool passionOpen, bool traitOpen, float roll)
        {
            float s = spellOpen ? 0.5f : 0f, c = classOpen ? 0.2f : 0f, p = passionOpen ? 0.5f : 0f, t = traitOpen ? 0.25f : 0f;
            float all = s + c + p + t;
            if (all <= 0f || float.IsNaN(roll)) return GodGift.Favour;
            float x = Math.Max(0f, Math.Min(0.9999f, roll)) * all;
            if (x < s) return GodGift.Spell;
            if (x < s + c) return GodGift.Class;
            if (x < s + c + p) return GodGift.Passion;
            return GodGift.Trait;
        }

        /// <summary>The favour a god's gift of its favour adds, and the share of the god's favour the kindest's gift adds on top.</summary>
        public const float FavourGift = 6f;

        // ---- What a god of each domain gives ----

        /// <summary>The skill whose passion a god of a domain gives.</summary>
        public static string Skill(GodDomain d) => d switch
        {
            GodDomain.Harvest => "Plants",
            GodDomain.Storm => "Shooting",
            GodDomain.Hunt => "Shooting",
            GodDomain.Healing => "Medicine",
            GodDomain.War => "Melee",
            GodDomain.Craft => "Crafting",
            GodDomain.Way => "Social",
            GodDomain.Dead => "Medicine",
            GodDomain.Night => "Artistic",
            GodDomain.Hearth => "Cooking",
            GodDomain.Wild => "Animals",
            GodDomain.Waters => "Plants",
            GodDomain.Machine => "Intellectual",
            GodDomain.Void => "Intellectual",
            _ => "Social"
        };

        /// <summary>The trait (the game's own, by name, and its degree) a god of a domain gives.</summary>
        public static (string trait, int degree) Trait(GodDomain d) => d switch
        {
            GodDomain.Harvest => ("Industriousness", 1),
            GodDomain.Storm => ("Nerves", 2),
            GodDomain.Hunt => ("Nimble", 0),
            GodDomain.Healing => ("Immunity", 1),
            GodDomain.War => ("Tough", 0),
            GodDomain.Craft => ("Industriousness", 2),
            GodDomain.Way => ("SpeedOffset", 2),
            GodDomain.Dead => ("Nerves", 1),
            GodDomain.Night => ("NightOwl", 0),
            GodDomain.Hearth => ("Kind", 0),
            GodDomain.Wild => ("QuickSleeper", 0),
            GodDomain.Waters => ("NaturalMood", 1),
            GodDomain.Machine => ("FastLearner", 0),
            GodDomain.Void => ("PsychicSensitivity", 2),
            _ => ("Kind", 0)
        };

        /// <summary>A RimWorld of Magic's classes a god of a domain grants (by its trait's name), as the design names them.</summary>
        public static string[] Classes(GodDomain d) => d switch
        {
            GodDomain.Healing => new[] { "Priest", "Paladin" },
            GodDomain.Wild => new[] { "Druid" },
            GodDomain.Harvest => new[] { "Druid" },
            GodDomain.Hunt => new[] { "Ranger" },
            GodDomain.Dead => new[] { "Necromancer", "DeathKnight" },
            GodDomain.Void => new[] { "Necromancer", "DeathKnight" },
            GodDomain.Storm => new[] { "StormBorn" },
            GodDomain.Craft => new[] { "Enchanter", "Geomancer" },
            GodDomain.Machine => new[] { "Technomancer" },
            GodDomain.Night => new[] { "TM_Bard" },
            GodDomain.War => new[] { "Gladiator", "TM_Monk" },
            GodDomain.Way => new[] { "TM_Wayfarer" },
            GodDomain.Hearth => new[] { "InnerFire" },
            GodDomain.Waters => new[] { "HeartOfFrost" },
            _ => new string[0]
        };

        /// <summary>A RimWorld of Magic's spell scrolls of a domain (its SpellOf_ things), for one who already casts.</summary>
        public static string[] Spells(GodDomain d) => d switch
        {
            GodDomain.Harvest => new[] { "SpellOf_Sunlight", "SpellOf_FertileLands", "SpellOf_Rain" },
            GodDomain.Storm => new[] { "SpellOf_EyeOfTheStorm", "SpellOf_AlterStorm", "SpellOf_Rain" },
            GodDomain.Hunt => new[] { "SpellOf_GuardianSpirit", "SpellOf_Blur" },
            GodDomain.Healing => new[] { "SpellOf_Heal", "SpellOf_CauterizeWound", "SpellOf_RegrowLimb" },
            GodDomain.War => new[] { "SpellOf_BattleHymn", "SpellOf_HolyWrath", "SpellOf_ShieldOther" },
            GodDomain.Craft => new[] { "SpellOf_SpellMending", "SpellOf_MagicWardrobe" },
            GodDomain.Way => new[] { "SpellOf_Recall", "SpellOf_Teleport", "SpellOf_Blink" },
            GodDomain.Dead => new[] { "SpellOf_GuardianSpirit", "SpellOf_FadeEmotions" },
            GodDomain.Night => new[] { "SpellOf_MageLight", "SpellOf_SpiritOfLight", "SpellOf_BloodMoon" },
            GodDomain.Hearth => new[] { "SpellOf_Heater", "SpellOf_Cooler" },
            GodDomain.Wild => new[] { "SpellOf_FertileLands", "SpellOf_BriarPatch" },
            GodDomain.Waters => new[] { "SpellOf_WetGround", "SpellOf_Rain" },
            GodDomain.Machine => new[] { "SpellOf_ChargeBattery", "SpellOf_PowerNode", "SpellOf_TechnoShield" },
            GodDomain.Void => new[] { "SpellOf_Scorn", "SpellOf_SiphonMana", "SpellOf_BlankMind" },
            _ => new string[0]
        };

        /// <summary>
        /// A god's find where it blessed the colony, by its domain and the colony's age (0 tribal to 3 spacer): the
        /// game's own things by name, the first that exists, with how much of it. Harvest and hearth food, healing medicine,
        /// craft and machine the forge's or the workshop's best, the road silver, the rest gold or silver.
        /// </summary>
        public static (string[] things, int count) Find(GodDomain d, int age) => d switch
        {
            GodDomain.Harvest => (age >= 2 ? new[] { "MealFine", "Pemmican" } : new[] { "Pemmican", "MealFine" }, 18),
            GodDomain.Hearth => (new[] { "MealFine", "Pemmican" }, 12),
            GodDomain.Healing => (age >= 2 ? new[] { "MedicineIndustrial", "MedicineHerbal" } : new[] { "MedicineHerbal" }, age >= 2 ? 6 : 10),
            GodDomain.Craft => (age >= 2 ? new[] { "ComponentIndustrial", "Steel" } : new[] { "Steel", "WoodLog" }, age >= 2 ? 6 : 60),
            GodDomain.Machine => (age >= 3 ? new[] { "ComponentSpacer", "ComponentIndustrial" } : new[] { "ComponentIndustrial", "Steel" }, age >= 3 ? 2 : 6),
            GodDomain.Way => (new[] { "Silver" }, 120 + 60 * Math.Max(0, Math.Min(3, age))),
            GodDomain.Wild => (new[] { "MedicineHerbal", "Pemmican" }, 10),
            GodDomain.Waters => (new[] { "Pemmican", "MealFine" }, 14),
            _ => (new[] { "Gold", "Silver" }, 12)
        };

        // ================= The sacred animal =================

        /// <summary>How long a sacred animal stays on the land, in days.</summary>
        public const float AnimalStayMin = 2f, AnimalStayMax = 4f;

        /// <summary>A small beast (a raven, a cat, an otter) follows one colonist about; a large one walks the land and leads.</summary>
        public static bool Follows(float bodySize) => bodySize <= 0.6f;

        /// <summary>How often (in days) a free colonist outdoors near it takes to following a large one of their own, at most once a stay.</summary>
        public const float BeckonChanceADay = 0.5f;

        /// <summary>The favour a sacred animal's god takes, before its nature: left be, fed, followed, hurt, killed.</summary>
        public const float LeftBe = 5f, Fed = 4f, Followed = 4f, Hurt = -20f, Slain = -40f;

        /// <summary>The favour the stay brings in all, by what was done (hurt and slain overrule the rest).</summary>
        public static float AnimalFavour(bool hurt, bool slain, bool fed, bool followed)
        {
            if (slain) return Slain;
            if (hurt) return Hurt;
            return LeftBe + (fed ? Fed : 0f) + (followed ? Followed : 0f);
        }

        /// <summary>
        /// What a sacred animal leads its follower to: a cure when they are hurt or ill and its god heals or grows things, a
        /// vein when there is ore to find and its god is of the forge, the wild or the dead (the deep), else a find; a
        /// <paramref name="roll"/> in [0, 1) lets a vein or a cure go to a find now and then.
        /// </summary>
        public static AnimalBoon Boon(GodDomain d, bool followerAiling, bool oreNear, float roll)
        {
            bool cures = d == GodDomain.Healing || d == GodDomain.Wild || d == GodDomain.Harvest || d == GodDomain.Waters || d == GodDomain.Night;
            bool digs = d == GodDomain.Craft || d == GodDomain.Machine || d == GodDomain.Dead || d == GodDomain.Wild || d == GodDomain.Hunt || d == GodDomain.War;
            if (followerAiling && cures && roll < 0.85f) return AnimalBoon.Cure;
            if (oreNear && digs && roll < 0.7f) return AnimalBoon.Vein;
            if (followerAiling && roll < 0.4f) return AnimalBoon.Cure;
            return AnimalBoon.Find;
        }

        /// <summary>How far a sacred animal leads someone, in cells: far enough to be a walk, never off into the map's edge.</summary>
        public const int LeadMin = 18, LeadMax = 45;

        // ================= Weather as a voice =================

        /// <summary>Days between two answers in the weather at one home.</summary>
        public const int WeatherGapDays = 12;

        /// <summary>A change of favour at least this big is something a god may answer in the weather.</summary>
        public const float WeatherFrom = 3f;

        /// <summary>
        /// Which weather a god answers in, for a change of favour of <paramref name="amount"/> (pleased or angered). By its
        /// domain: a god of storms with thunder either way; a god of the harvest or the waters, pleased, with rain; a dark god
        /// (the dead, the night, the void) angered, with an eclipse by day; a god pleased at night, of the night, the road or
        /// the dead, with an aurora. A god whose shape names its own voices (<paramref name="only"/>, GodShapeDef.voices)
        /// answers with the first of them that fits the moment: rain and an aurora only when pleased (an aurora only at
        /// night), an eclipse only when angered and by day, thunder either way. None for a small change.
        /// </summary>
        public static WeatherVoice Voice(GodDomain d, GodDomain second, float amount, bool night, IList<WeatherVoice> only = null)
        {
            if (float.IsNaN(amount) || Math.Abs(amount) < WeatherFrom) return WeatherVoice.None;
            bool pleased = amount > 0f;
            if (only != null && only.Count > 0)
            {
                foreach (var v in only)
                    if (Fits(v, pleased, night))
                        return v;
                return WeatherVoice.None;
            }
            bool Has(GodDomain x) => d == x || second == x;
            if (Has(GodDomain.Storm)) return WeatherVoice.Thunder;
            if (pleased && (Has(GodDomain.Harvest) || Has(GodDomain.Waters))) return WeatherVoice.Rain;
            if (!pleased && !night && (Has(GodDomain.Void) || Has(GodDomain.Dead) || Has(GodDomain.Night))) return WeatherVoice.Eclipse;
            if (pleased && night && (Has(GodDomain.Night) || Has(GodDomain.Way) || Has(GodDomain.Dead))) return WeatherVoice.Aurora;
            return WeatherVoice.None;
        }

        /// <summary>A voice fits the moment: rain pleased, an aurora pleased at night, an eclipse angered by day, thunder either way.</summary>
        public static bool Fits(WeatherVoice v, bool pleased, bool night) => v switch
        {
            WeatherVoice.Rain => pleased,
            WeatherVoice.Aurora => pleased && night,
            WeatherVoice.Eclipse => !pleased && !night,
            WeatherVoice.Thunder => true,
            _ => false
        };

        /// <summary>A god with no body acts through the sky: it answers this much more readily, and its answers come this much closer together.</summary>
        public const float BodilessChance = 1.6f, BodilessGap = 0.5f;

        /// <summary>The days between two answers in the weather at one home, for a god with a body or without.</summary>
        public static float WeatherGap(bool bodiless) => bodiless ? WeatherGapDays * BodilessGap : WeatherGapDays;

        /// <summary>
        /// The chance a god answers at all: by how loudly it answers (its nature and kind) and how big the change was, at
        /// most two in three (a god with no body more readily, at most six in seven); the weather comes only as the land
        /// allows it (the caller asks the game).
        /// </summary>
        public static float VoiceChance(float loudness, float amount, bool bodiless = false)
        {
            if (float.IsNaN(loudness) || float.IsNaN(amount)) return 0f;
            float a = Math.Min(1f, Math.Abs(amount) / 15f);
            float c = 0.15f + 0.35f * Math.Max(0f, Math.Min(1f, loudness)) + 0.25f * a;
            if (bodiless) c *= BodilessChance;
            return Math.Max(0f, Math.Min(bodiless ? 0.85f : 0.66f, c));
        }

        /// <summary>
        /// In Believed nothing is sent: the weather the game brings of its own within this many hours of a change of favour is
        /// what the colony reads as the god's answer.
        /// </summary>
        public const int BelievedWithinHours = 24;

        // ================= The marks =================

        /// <summary>The marks remembered at most, the oldest let go first.</summary>
        public const int MaxMarks = 24;

        /// <summary>A mark is spoken of for this many days (about five years).</summary>
        public const int MarkYears = 5;

        /// <summary>Days between two visits to a mark, and the chance a quiet day brings one.</summary>
        public const int VisitGapDays = 8;
        public const float VisitChance = 0.2f;

        /// <summary>A visitor near a mark remarks on it at most once a day a home.</summary>
        public const int VisitorRemarkGapTicks = 60000;

        /// <summary>A mark is still spoken of: within <see cref="MarkYears"/> years.</summary>
        public static bool Remembered(int days) => days >= 0 && days <= MarkYears * 60;

        /// <summary>
        /// Which mark someone visits on a quiet day, of those still remembered: the newest and least visited first,
        /// <paramref name="days"/> each mark's age, <paramref name="visits"/> how often it has been visited; -1 for none.
        /// </summary>
        public static int VisitPick(IList<int> days, IList<int> visits, IList<int> daysSinceVisit)
        {
            if (days == null || visits == null || daysSinceVisit == null) return -1;
            int best = -1;
            float most = 0f;
            for (int i = 0; i < days.Count && i < visits.Count && i < daysSinceVisit.Count; i++)
            {
                if (!Remembered(days[i]) || daysSinceVisit[i] < VisitGapDays) continue;
                float w = 1f / (1f + visits[i]) * (1f + 60f / (30f + days[i]));
                if (w > most)
                {
                    most = w;
                    best = i;
                }
            }
            return best;
        }

        // ================= Small helpers =================

        private static int Mod(int a, int n) => n <= 0 ? 0 : ((a % n) + n) % n;

        private static int Seed(params int[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (int p in parts) h = h * 31 + p;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
