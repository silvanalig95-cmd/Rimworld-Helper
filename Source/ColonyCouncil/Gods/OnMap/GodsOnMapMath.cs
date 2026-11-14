using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What comes to the map when a god does (docs/design/gods.md, "Stage 2, on the map").</summary>
    public enum ComingKind
    {
        None,
        /// <summary>A god with no body rises from the land for an hour, very rarely (Hollin: roots drawn up into a shape taller than any tree).</summary>
        Rising,
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
        Restless,

        // ---- Each god's own (GodShapeDef.omens; the brainstorm of the nine, "How it is seen") ----

        /// <summary>Hollin: leaves and seeds drift across the land toward one place, on no wind.</summary>
        Drift,
        /// <summary>Hollin: an anima tree hums loud enough to hear indoors.</summary>
        AnimaHum,
        /// <summary>Hollin: her dryads stop what they are doing and stand still.</summary>
        DryadsStill,
        /// <summary>Smoulder: a vast winged shadow crosses the land at noon, and a wind flattens the grass.</summary>
        Shadow,
        /// <summary>Smoulder: steam rises from the ground and the geysers, his breath.</summary>
        Steam,
        /// <summary>Smoulder: the rock rumbles where the colony mines deep, and loose stone falls.</summary>
        Rumble,
        /// <summary>Morel: a ring of mushrooms overnight around something she wants noticed.</summary>
        FairyRing,
        /// <summary>Morel: a smell of rich earth after rain where no rain fell.</summary>
        Earth,
        /// <summary>Morel: white threads across the floor of a room shut too long.</summary>
        Threads,
        /// <summary>Pip: the lights flicker in a pattern, like someone tapping.</summary>
        Flicker,
        /// <summary>Pip: a comms console whispers static that sounds like breathing, then like a laugh.</summary>
        Static,
        /// <summary>Pip: a light with no power glows all the same, to show it can.</summary>
        Unpowered,
        /// <summary>Cirrus: at noon, a shadow too large to have a source.</summary>
        VastShadow,
        /// <summary>Cirrus: at night, a single star that moves the wrong way.</summary>
        Star,
        /// <summary>Hush: frost by a wall in lines that look like writing.</summary>
        FrostWriting,
        /// <summary>Hush: every sound stops for a breath.</summary>
        Silence,
        /// <summary>Hush: at the edge of the land at night, a tall white figure, standing still.</summary>
        WhiteFigure,
        /// <summary>Undertow: a patch of dark that a light doesn't reach.</summary>
        Dark,
        /// <summary>Undertow: the most desperate of the colony hears a sweet, reasonable voice.</summary>
        Whisper,

        // ---- The four the player added (brainstorm 10 to 13) ----

        /// <summary>Murmur: a deep croaking from the wells and the marshes before rain, felt in the chest.</summary>
        Croak,
        /// <summary>Helios: the sun stands still a moment too long, and the shadows don't move.</summary>
        StillSun,
        /// <summary>Helios: a heat haze over the open ground, the air shimmering.</summary>
        HeatHaze,
        /// <summary>Selene: the moon comes up huge and close and stays over the colony all night.</summary>
        BigMoon,
        /// <summary>Ducat: a glint of gold in the corner of someone's eye.</summary>
        Glint,
        /// <summary>Ducat: a golden beetle walking across a table.</summary>
        Beetle,
        /// <summary>Ducat: a coin found where none was dropped.</summary>
        Coin,

        // ---- Tamiu's, the cat who keeps the dead (defName Mog; docs/gods-next-proposals.md, C 12) ----

        /// <summary>Tamiu: every cat on the land stops and sits facing the graves (or the colony's door, with no graves).</summary>
        CatsFacing,
        /// <summary>Tamiu: two gold eyes low in the dark by the graves, and gone.</summary>
        GoldEyes,
        /// <summary>Tamiu: fine pale sand across a grave or a doorstep, where no wind blows sand.</summary>
        Sand
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
        Aurora,
        /// <summary>A cold snap for a god of winter's displeasure (the game's own).</summary>
        Cold,
        /// <summary>Snow for a god of winter pleased, where the land has snow now.</summary>
        Snow,
        /// <summary>A heat wave: the sun staring too hard (the game's own).</summary>
        Heat,
        /// <summary>Clear skies for a long while: the sun pleased.</summary>
        Clear,
        /// <summary>A night of bad sleep and nightmares: the moon displeased.</summary>
        Sleepless
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

        /// <summary>
        /// While Mog stays, the chance a day that Ennu comes to the gate in its disguise, once the stranger's own gap has
        /// passed: about one of her seasons in three (docs/gods-pantheon-brainstorm.md, Mog: "she sees Ennu for what it is").
        /// </summary>
        public const float GuestInHerSeasonADay = 0.03f;

        public static bool GuestInHerSeason(int daysSinceStranger, bool strangerCan, float roll) =>
            strangerCan && daysSinceStranger >= StrangerGapDays && roll < GuestInHerSeasonADay;

        /// <summary>Once the gaps are past, the chance a day that a god sets out (about one in thirty days).</summary>
        public const float DailyChance = 0.033f;

        /// <summary>Days of omens before a coming, at least and at most.</summary>
        public const int OmenDaysMin = 2, OmenDaysMax = 4;

        /// <summary>
        /// Whether a god sets out for the colony today, and as what: after the colony's first days, the gaps past, and the
        /// day's roll (<paramref name="roll"/> in [0, 1)) under <see cref="DailyChance"/>: never sure on any day. A stranger needs a people at peace to come from (<paramref name="strangerCan"/>),
        /// an animal a god whose look walks as one that can live here (<paramref name="animalCan"/>); when both may come, the
        /// one longest gone (the stranger on a tie), decided by <paramref name="pick"/> in [0, 1) when they are as long gone.
        /// </summary>
        public static ComingKind Due(int day, int daysSinceAny, int daysSinceStranger, int daysSinceAnimal, bool strangerCan, bool animalCan, float roll, float pick)
        {
            if (day < EarliestDay || daysSinceAny < AnyGapDays || float.IsNaN(roll) || roll >= DailyChance) return ComingKind.None;
            bool s = strangerCan && daysSinceStranger >= StrangerGapDays;
            bool a = animalCan && daysSinceAnimal >= AnimalGapDays;
            if (s && a)
            {
                float sw = Math.Min(daysSinceStranger, 1000) / (float)StrangerGapDays, aw = Math.Min(daysSinceAnimal, 1000) / (float)AnimalGapDays;
                return pick * (sw + aw) < sw ? ComingKind.Stranger : ComingKind.Animal;
            }
            return s ? ComingKind.Stranger : a ? ComingKind.Animal : ComingKind.None;
        }

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

        /// <summary>The signs any god may send when its shape names none of its own.</summary>
        public static readonly OmenKind[] Common = { OmenKind.Birds, OmenKind.Calm, OmenKind.Dream, OmenKind.Candle, OmenKind.Restless };

        /// <summary>An omen shown on the sky by day (noon), at night, or at a light (the evening), else in the waking hours.</summary>
        public static int OmenHour(OmenKind k, int seed) => k switch
        {
            OmenKind.Dream => 6 + Mod(seed, 2),
            OmenKind.Shadow or OmenKind.VastShadow => 11 + Mod(seed, 3),
            OmenKind.StillSun => 15 + Mod(seed, 2),
            OmenKind.HeatHaze => 12 + Mod(seed, 3),
            OmenKind.Star or OmenKind.WhiteFigure or OmenKind.Dark or OmenKind.BigMoon => 21 + Mod(seed, 3),
            OmenKind.Croak => 17 + Mod(seed, 4),
            OmenKind.Candle or OmenKind.Flicker or OmenKind.Unpowered or OmenKind.Whisper => 19 + Mod(seed, 3),
            OmenKind.FrostWriting or OmenKind.Earth => 7 + Mod(seed, 2),
            _ => 9 + Mod(seed, 8)
        };

        /// <summary>
        /// The omens over <paramref name="days"/> days before a coming: one a day (two on the last day when there are more
        /// days' worth of omens than days), at most <see cref="MaxOmens"/>, each of a kind that can show on this map
        /// (<paramref name="can"/>, by OmenKind), no kind twice while another is left; picked in turn by
        /// <paramref name="seed"/>. Empty when none can show.
        /// </summary>
        public static List<OmenKind> Omens(int days, Func<OmenKind, bool> can, int seed, IList<OmenKind> own = null)
        {
            var list = new List<OmenKind>();
            if (days <= 0 || can == null) return list;
            var open = new List<OmenKind>();
            // A god's own signs (its shape's), else the five every god may send.
            var kinds = own != null && own.Count > 0 ? (IEnumerable<OmenKind>)own : Common;
            foreach (var k in kinds)
                if (can(k) && !open.Contains(k))
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
            int hour = OmenHour(kind, Seed(seed, index, 7));
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

        /// <summary>Days between two answers in the weather at one home, and the days after it over which its chance grows back (GodPaceMath.Ramp).</summary>
        public const int WeatherGapDays = 15, WeatherRampDays = 15;

        /// <summary>A change of favour at least this big is something a god may answer in the weather.</summary>
        public const float WeatherFrom = 3f;

        /// <summary>
        /// Which weather a god answers in, for a change of favour of <paramref name="amount"/> (pleased or angered). By its
        /// domain: a god of storms with thunder either way; a god of the harvest or the waters, pleased, with rain; a dark god
        /// (the dead, the night, the void) angered, with an eclipse by day; a god pleased at night, of the night, the road or
        /// the dead, with an aurora. A god whose shape names its own voices (<paramref name="only"/>, GodShapeDef.voices)
        /// answers with the first of them that fits the moment: rain and an aurora only when pleased (an aurora only at
        /// night), an eclipse only when angered and by day, thunder either way; of those that fit, one it didn't answer in
        /// last time at this home (<paramref name="lately"/>, newest first) where there is one. None for a small change.
        /// </summary>
        public static WeatherVoice Voice(GodDomain d, GodDomain second, float amount, bool night, IList<WeatherVoice> only = null, IList<string> lately = null)
        {
            if (float.IsNaN(amount) || Math.Abs(amount) < WeatherFrom) return WeatherVoice.None;
            bool pleased = amount > 0f;
            if (only != null && only.Count > 0)
            {
                var first = WeatherVoice.None;
                foreach (var v in only)
                {
                    if (!Fits(v, pleased, night)) continue;
                    if (first == WeatherVoice.None) first = v;
                    if (GodPaceMath.Lean(v.ToString(), lately) > GodPaceMath.LastLean) return v;
                }
                return first;
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
            WeatherVoice.Cold => !pleased,
            WeatherVoice.Snow => pleased,
            WeatherVoice.Heat => !pleased && !night,
            WeatherVoice.Clear => pleased && !night,
            WeatherVoice.Sleepless => !pleased && night,
            _ => false
        };

        /// <summary>A god with no body acts through the sky: it answers this much more readily, and its answers come this much closer together.</summary>
        public const float BodilessChance = 1.6f, BodilessGap = 0.7f;

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

        // ================= The marks =================

        /// <summary>The marks remembered at most, the oldest let go first.</summary>
        public const int MaxMarks = 24;

        /// <summary>A mark is spoken of for this many days (about five years).</summary>
        public const int MarkYears = 5;

        /// <summary>Days between two visits to a mark, the chance a quiet day brings one once the gap is past, and the days that chance grows over (GodPaceMath.Ramp).</summary>
        public const int VisitGapDays = 12, VisitRampDays = 12;
        public const float VisitChance = 0.12f;

        /// <summary>A mark brought up at supper: never within this many days of the last time, then at this chance an evening hour, growing over the days after.</summary>
        public const int SupperMarkGapDays = 6, SupperMarkRampDays = 10;
        public const float SupperMarkChance = 0.08f;

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

        // ================= A god rising =================

        /// <summary>Days between two risings, and the chance a day once past (very rarely: once in a long game or two).</summary>
        public const int RisingGapDays = 300;
        public const float RisingChance = 0.004f;

        /// <summary>
        /// A god that can rise (its shape's rises) rises today: after its long gap, on the day's roll, a little likelier the
        /// nearer it is drawn (<paramref name="nearness"/>: Murmur in a long drought), never sure.
        /// </summary>
        public static bool RisingDue(int day, int daysSinceAny, int daysSinceRising, bool can, float roll, float nearness = 0f) =>
            can && day >= EarliestDay * 3 && daysSinceAny >= AnyGapDays && daysSinceRising >= RisingGapDays && !float.IsNaN(roll)
            && roll < RisingChance * (1f + RisingNearFactor * GodNearMath.Strength(nearness));

        /// <summary>A god drawn as near as it comes rises this many times more readily (on top of once).</summary>
        public const float RisingNearFactor = 1.5f;

        // ================= The Guest's memory =================

        /// <summary>
        /// The tag the Guest's lines carry for what it remembers of this colony's last welcome: none the first time, kept for
        /// a good welcome, cold for a poor one or a door shut, harmed for a blow.
        /// </summary>
        public static string Remembers(int visits, Treatment last) =>
            visits <= 0 ? "first" : last >= Treatment.Kind ? "rememberskind" : last == Treatment.Harmed ? "remembersharm" : last <= Treatment.Poor ? "rememberscold" : "remembers";

        /// <summary>A good welcome is blessed in ways that arrive years later: the days until the Guest's late gift.</summary>
        public const int LateGiftMinDays = 60, LateGiftMaxDays = 180;

        // ================= Sitting near the Old Horn =================

        /// <summary>How someone comes away changed after sitting near the god's beast while it grazes.</summary>
        public enum SitChange
        {
            Nothing,
            /// <summary>A grief eased: the heaviest memory of a death lifted.</summary>
            Grief,
            /// <summary>A fear gone: a nervous or a cowardly nature lifted.</summary>
            Fear,
            /// <summary>A passion found.</summary>
            Passion,
            /// <summary>A child let close to touch its horn (and the sky noticed).</summary>
            Horn
        }

        /// <summary>A child touches the horn; a grieving one is eased first, then a fearful one, else a passion found.</summary>
        public static SitChange Sat(bool child, bool grieving, bool fearful, bool passionOpen)
        {
            if (child) return SitChange.Horn;
            if (grieving) return SitChange.Grief;
            if (fearful) return SitChange.Fear;
            return passionOpen ? SitChange.Passion : SitChange.Nothing;
        }

        /// <summary>At most this many come away changed in one visit, and each only once.</summary>
        public const int SittersAVisit = 3;

        /// <summary>The favour every god loses when the colony harms the beast whose harm is the worst thing (Bellwether, or his kin).</summary>
        public const float EveryGodColder = -3f;

        /// <summary>The goodwill every people with someone on the map loses when it hears of it.</summary>
        public const int PeoplesHear = -10;

        // ================= The sleeper under the mountain =================

        /// <summary>
        /// The rock rumbles over the colony's deep mining (under thick rock): never within <see cref="DeepGapDays"/> of the
        /// last, then at a small chance in each hour someone mines there, growing over <see cref="DeepRampDays"/> days
        /// (GodPaceMath.Ramp). Miners who keep digging past the warnings hear it again, each time the next step toward its
        /// waking, but never on a day anyone could name.
        /// </summary>
        public const int DeepGapDays = 5, DeepRampDays = 10;
        public const float DeepChanceAnHour = 0.03f;

        /// <summary>The chance the rock rumbles in an hour someone of the colony mines deep, <paramref name="daysSince"/> days after the last.</summary>
        public static float DeepNow(int daysSince) => DeepChanceAnHour * GodPaceMath.Ramp(daysSince, DeepGapDays, DeepRampDays);

        /// <summary>The rumbles are forgotten when the colony leaves the deep alone this long.</summary>
        public const int DeepForgottenDays = 30;

        /// <summary>From this many rumbles loose stone may fall on the miner; at this many it wakes.</summary>
        public const int StoneFallsFrom = 3, WakesAt = 6;

        /// <summary>Once woken it sleeps again for years; its true form stays this many hours on the land.</summary>
        public const int WakeGapDays = 240, WokenHours = 3;

        /// <summary>The rumbles counted with this one: one more, or the first again when the last was long ago.</summary>
        public static int Warned(int warnings, int daysSinceLast) => (daysSinceLast >= DeepForgottenDays ? 0 : Math.Max(0, warnings)) + 1;

        /// <summary>It wakes: past every warning, and not within years of its last waking.</summary>
        public static bool Wakes(int warnings, int daysSinceWake) => warnings >= WakesAt && daysSinceWake >= WakeGapDays;

        /// <summary>
        /// Its true form is seen only when the god is drawn near enough to show itself plainly (GodNearMath.Plain on its
        /// nearness, <paramref name="roll"/> in [0, 1)), and only when the game has one for it; else the mountain quakes and its
        /// shadow passes, or the land stirs and nothing more is seen.
        /// </summary>
        public static bool TrueForm(float nearness, bool hasForm, float roll) => hasForm && GodNearMath.Plain(nearness, roll);

        /// <summary>Loose stone falls on the miner with this rumble: from the third, at a chance by the harm setting (the shared cap is the caller's).</summary>
        public static bool StoneFalls(int warnings, float harmScale, float roll) =>
            warnings >= StoneFallsFrom && !float.IsNaN(roll) && !float.IsNaN(harmScale) && roll < Math.Min(0.6f, 0.3f * Math.Max(0f, harmScale));

        // ================= The moon's return =================

        /// <summary>What comes back in the night for a deed of the day: a kindness as something found, a good dream or a wound healed; a cruelty as a nightmare, a frost or an animal that returns.</summary>
        public enum NightReturn
        {
            None,
            Found,
            GoodDream,
            Healed,
            Nightmare,
            Frost,
            AnimalBack
        }

        /// <summary>A deed counts as a kindness or a cruelty from this much of its name's kindness (times its size), and as a strong one from this much.</summary>
        public const float ReturnDeedFrom = 3f, ReturnStrongFrom = 7f;

        /// <summary>
        /// The moon returns by the moon itself: at most once in a cycle of its face (fifteen days), never within this many days
        /// of the last, and its chance grows over the next two cycles after (GodPaceMath.Ramp).
        /// </summary>
        public const int ReturnGapDays = 12, ReturnRampDays = 30;

        /// <summary>A deed of a kind the colony did this often lately (in <see cref="RoutineDays"/>), or one that has run on for days, is routine: no reason for the moon.</summary>
        public const int RoutineCount = 3, RoutineDays = 15, RoutineAgeDays = 3;

        /// <summary>
        /// How strongly the day's deed calls the moon: none for a small one or one the colony does all the time (tending the
        /// sick every day is no reason); ordinary from <see cref="ReturnDeedFrom"/>, strong from <see cref="ReturnStrongFrom"/>.
        /// A cruelty done again by someone the moon already came back to for one is a story the world gives a reason for:
        /// strong, even when it has become their habit.
        /// </summary>
        public static GodPaceMath.Trigger ReturnTrigger(float kindness, bool doneToday, int sameKindLately, float ageDays, bool crueltyAgain)
        {
            if (float.IsNaN(kindness) || !doneToday || Math.Abs(kindness) < ReturnDeedFrom) return GodPaceMath.Trigger.None;
            if (kindness < 0f && crueltyAgain) return GodPaceMath.Trigger.Strong;
            if (sameKindLately >= RoutineCount || ageDays >= RoutineAgeDays) return GodPaceMath.Trigger.None;
            return Math.Abs(kindness) >= ReturnStrongFrom ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>How strongly the colony's dealings with the peoples by day call the moon: the day's good turns less its wrongs.</summary>
        public static GodPaceMath.Trigger WorldTrigger(int kindness, int cruelty)
        {
            int net = Math.Abs(kindness - cruelty);
            return net >= 3 ? GodPaceMath.Trigger.Strong : net >= 1 ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The chance on one night that the moon returns, for an ordinary reason, by her face: around the full moon only.</summary>
        public static float ReturnNight(MoonFace face) => face switch
        {
            MoonFace.Full => 0.2f,
            MoonFace.Half => 0.05f,
            _ => 0f
        };

        /// <summary>
        /// The moon returns tonight: only with a reason (<paramref name="t"/>), only around the full moon (her face), a cycle
        /// apart at least, its chance small at first and growing over the cycles after (GodPaceMath.Ramp); never sure.
        /// </summary>
        public static float ReturnTonight(MoonFace face, int daysSince, GodPaceMath.Trigger t) =>
            ReturnNight(face) * GodPaceMath.Weight(t) * GodPaceMath.Ramp(daysSince, ReturnGapDays, ReturnRampDays);

        public static bool Returns(MoonFace face, int daysSince, GodPaceMath.Trigger t, float roll) => !float.IsNaN(roll) && roll < ReturnTonight(face, daysSince, t);

        /// <summary>The order a kindness and a cruelty come back in when the same one comes back to the same person again: each time the next.</summary>
        public static readonly NightReturn[] KindSteps = { NightReturn.Found, NightReturn.GoodDream, NightReturn.Healed };

        public static readonly NightReturn[] CruelSteps = { NightReturn.Nightmare, NightReturn.Frost, NightReturn.AnimalBack };

        /// <summary>A form can come back now: a wound healed only for one who is hurt, a frost only on a cold night.</summary>
        public static bool ReturnCan(NightReturn k, bool hurt, bool cold) => k switch
        {
            NightReturn.Healed => hurt,
            NightReturn.Frost => cold,
            NightReturn.None => false,
            _ => true
        };

        /// <summary>
        /// What comes back for a deed (its kindness weight: positive kind, negative cruel): a kindness as a wound healed when the
        /// one who did it is hurt, something found or a good dream; a cruelty as an animal back (most when the cruelty was to an
        /// animal), a frost when the night is cold enough, or a nightmare. It leans away from the forms the moon took lately
        /// (<paramref name="lately"/>, newest first: GodPaceMath.Lean). When it comes back to the same person for the same kind
        /// of deed (<paramref name="theirLast"/>, the form it took for them last time), it is the next step of that story
        /// (<see cref="KindSteps"/>, <see cref="CruelSteps"/>), not the same card.
        /// </summary>
        public static NightReturn ReturnFor(float kindness, bool hurt, bool toAnimal, bool cold, float roll, IList<string> lately = null, NightReturn theirLast = NightReturn.None)
        {
            if (float.IsNaN(kindness) || Math.Abs(kindness) < ReturnDeedFrom || float.IsNaN(roll)) return NightReturn.None;
            bool kind = kindness > 0f;
            var steps = kind ? KindSteps : CruelSteps;
            if (theirLast != NightReturn.None && Array.IndexOf(steps, theirLast) >= 0)
            {
                var order = new List<string>();
                foreach (var k in steps) order.Add(k.ToString());
                string next = GodPaceMath.NextStep(order, theirLast.ToString(), f => ReturnCan((NightReturn)Enum.Parse(typeof(NightReturn), f), hurt, cold));
                if (next != null && next != theirLast.ToString()) return (NightReturn)Enum.Parse(typeof(NightReturn), next);
                // No step further can come tonight: a different form all the same, never theirs of last time.
                var mind = new List<string> { theirLast.ToString() };
                if (lately != null) mind.AddRange(lately);
                lately = mind;
            }
            var forms = new List<string>();
            var weights = new List<float>();
            void Add(NightReturn k, float w)
            {
                forms.Add(k.ToString());
                weights.Add(ReturnCan(k, hurt, cold) ? w : 0f);
            }
            if (kind)
            {
                Add(NightReturn.Healed, 2f);
                Add(NightReturn.Found, 1.5f);
                Add(NightReturn.GoodDream, 1f);
            }
            else
            {
                Add(NightReturn.AnimalBack, toAnimal ? 2f : 0.4f);
                Add(NightReturn.Frost, 1f);
                Add(NightReturn.Nightmare, 1.5f);
            }
            int i = GodPaceMath.Pick(forms, weights, lately, roll);
            return i < 0 ? NightReturn.None : (NightReturn)Enum.Parse(typeof(NightReturn), forms[i]);
        }

        // ================= The moon's face =================

        /// <summary>The moon's face through a season of fifteen days: gone, thin, half, full, half, thin, gone.</summary>
        public enum MoonFace
        {
            Gone,
            Thin,
            Half,
            Full
        }

        /// <summary>The moon's face on day <paramref name="day"/> of the world (Rimfolk's own calendar: one round a season of fifteen days).</summary>
        public static MoonFace Face(int day)
        {
            int d = Mod(day, 15);
            return d <= 1 || d == 14 ? MoonFace.Gone : d <= 4 || d >= 12 ? MoonFace.Thin : d <= 6 || d >= 10 ? MoonFace.Half : MoonFace.Full;
        }

        /// <summary>The moon's light over a night, by its face (the huge moon is drawn this much of its fullest).</summary>
        public static float MoonLight(MoonFace f) => f switch
        {
            MoonFace.Full => 1f,
            MoonFace.Half => 0.7f,
            MoonFace.Thin => 0.4f,
            _ => 0f
        };

        /// <summary>On a night with no moon, the void's signs come a little more easily: this many hours early.</summary>
        public const int DarkNightEarlyHours = 12;

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
