using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// An animal's temperament: every named or bonded animal of the colony has one (Animals/AnimalTempers.cs), shown on
    /// its card and in small moments on the map. The order is the saved number: add new ones at the end.
    /// </summary>
    public enum Temper
    {
        Brave,
        Timid,
        Greedy,
        Gentle,
        Proud,
        Mischievous,
        Loyal,
        Lazy,
        Curious
    }

    /// <summary>
    /// Whether animals speak (Animals/AnimalVoices.cs), a fantasy part: Auto speaks only while a fantasy source is there
    /// (PokéWorld, A RimWorld of Magic, or someone in the colony with a psylink); On always; Off never.
    /// </summary>
    public enum AnimalSpeech
    {
        Auto,
        On,
        Off
    }

    /// <summary>What is known of an animal's kind for its temperament and its voice, read from the game.</summary>
    public struct NatureFacts
    {
        /// <summary>Its family (<see cref="AnimalNatureMath.Family"/>): canine, feline, hoofed, grazer, ursine, thrumbo, bird, bug, reptile, small, beast.</summary>
        public string family;
        public bool predator;
        public bool pokemon;
        public float bodySize;
        /// <summary>0 nothing can be taught, 1 the middle, 2 the most.</summary>
        public int trainability;
        /// <summary>A Pokémon's types by name (Fire, Psychic...); empty otherwise.</summary>
        public IList<string> types;
    }

    /// <summary>
    /// The rules behind the animals' natures, with no game types so the tests can run them: which temperament a kind
    /// leans to and the fixed pick for one animal, how often each temperament's moment comes, when two animals become
    /// friends or rivals and when that fades, how long a grieving animal keeps going to a grave, how long a sold or
    /// released animal remembers its handler, who may speak, and the compass word for where a danger is.
    /// </summary>
    public static class AnimalNatureMath
    {
        public static readonly Temper[] All = (Temper[])Enum.GetValues(typeof(Temper));

        /// <summary>The temperament's word in tags and keys: brave, timid, greedy...</summary>
        public static string Key(Temper t) => t.ToString().ToLowerInvariant();

        /// <summary>A saved number back to a temperament; anything out of range is none.</summary>
        public static bool TryOf(int n, out Temper t)
        {
            t = Temper.Brave;
            if (n < 0 || n >= All.Length) return false;
            t = (Temper)n;
            return true;
        }

        // ---------------- A kind's family, from its words ----------------

        private static readonly Dictionary<string, string> familyWords = new Dictionary<string, string>
        {
            ["dog"] = "canine", ["dogs"] = "canine", ["husky"] = "canine", ["labrador"] = "canine", ["retriever"] = "canine", ["wolf"] = "canine", ["warg"] = "canine",
            ["fox"] = "canine", ["coyote"] = "canine", ["jackal"] = "canine", ["hound"] = "canine", ["terrier"] = "canine", ["yorkshire"] = "canine",
            ["dingo"] = "canine", ["hyena"] = "canine", ["canine"] = "canine", ["manwolf"] = "canine",
            ["cat"] = "feline", ["cats"] = "feline", ["lynx"] = "feline", ["cougar"] = "feline", ["panther"] = "feline", ["lion"] = "feline", ["tiger"] = "feline",
            ["leopard"] = "feline", ["jaguar"] = "feline", ["puma"] = "feline", ["cheetah"] = "feline", ["serval"] = "feline", ["ocelot"] = "feline",
            ["feline"] = "feline", ["smilodon"] = "feline",
            ["horse"] = "hoofed", ["pony"] = "hoofed", ["donkey"] = "hoofed", ["zebra"] = "hoofed", ["mule"] = "hoofed", ["camel"] = "hoofed",
            ["dromedary"] = "hoofed", ["alpaca"] = "hoofed", ["llama"] = "hoofed", ["equine"] = "hoofed", ["demigryph"] = "hoofed",
            ["muffalo"] = "grazer", ["cow"] = "grazer", ["cattle"] = "grazer", ["bison"] = "grazer", ["yak"] = "grazer", ["buffalo"] = "grazer",
            ["ox"] = "grazer", ["sheep"] = "grazer", ["goat"] = "grazer", ["ibex"] = "grazer", ["elk"] = "grazer", ["deer"] = "grazer",
            ["caribou"] = "grazer", ["gazelle"] = "grazer", ["boar"] = "grazer", ["pig"] = "grazer", ["elephant"] = "grazer", ["mammoth"] = "grazer",
            ["dromedaries"] = "grazer", ["manffalo"] = "grazer",
            ["bear"] = "ursine", ["ursine"] = "ursine", ["manbear"] = "ursine",
            ["thrumbo"] = "thrumbo",
            ["lizard"] = "reptile", ["iguana"] = "reptile", ["tortoise"] = "reptile", ["turtle"] = "reptile", ["snake"] = "reptile", ["cobra"] = "reptile",
            ["crocodile"] = "reptile", ["alligator"] = "reptile", ["monitor"] = "reptile", ["gecko"] = "reptile", ["komodo"] = "reptile",
            ["rat"] = "small", ["squirrel"] = "small", ["rabbit"] = "small", ["hare"] = "small", ["capybara"] = "small", ["chinchilla"] = "small",
            ["hamster"] = "small", ["mouse"] = "small", ["ferret"] = "small", ["raccoon"] = "small", ["hedgehog"] = "small", ["marmot"] = "small",
            ["guinea"] = "small", ["fennec"] = "small", ["boomrat"] = "small"
        };

        /// <summary>The words of a name: split on anything not a letter and before a capital ("YorkshireTerrier", "snow_hare").</summary>
        public static List<string> Words(string s)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(s)) return words;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool letter = char.IsLetter(c);
                bool split = !letter || (char.IsUpper(c) && i > 0 && char.IsLower(s[i - 1]));
                if (split && cur.Length > 0)
                {
                    words.Add(cur.ToString().ToLowerInvariant());
                    cur.Clear();
                }
                if (letter) cur.Append(c);
            }
            if (cur.Length > 0) words.Add(cur.ToString().ToLowerInvariant());
            return words;
        }

        /// <summary>
        /// Its family, from its label's words first and then its defName's: canine, feline, hoofed, grazer, ursine,
        /// thrumbo, reptile, small; a bird by its body and an insect (bug) whatever its words; else a beast. A Pokémon
        /// keeps the family of its words (a Growlithe is a beast unless its words say otherwise).
        /// </summary>
        public static string Family(string defName, string label, bool bird, bool insect)
        {
            if (insect) return "bug";
            foreach (var source in new[] { label, defName })
                foreach (var w in Words(source))
                    if (familyWords.TryGetValue(w, out var f))
                        return f;
            return bird ? "bird" : "beast";
        }

        // ---------------- Which temperament a kind leans to ----------------

        private static void Mul(float[] w, Temper t, float f) => w[(int)t] *= f;

        /// <summary>
        /// How likely each temperament is for an animal of this kind (one weight each, in <see cref="All"/>'s order): all
        /// start even, and a kind's family, its size, whether it hunts, how much it can be taught and a Pokémon's types
        /// lean them. A dog leans loyal, a cat proud and mischievous, a muffalo gentle and lazy, a thrumbo gentle and proud.
        /// </summary>
        public static float[] Weights(NatureFacts f)
        {
            var w = new float[All.Length];
            for (int i = 0; i < w.Length; i++) w[i] = 1f;
            switch (f.family)
            {
                case "canine":
                    Mul(w, Temper.Loyal, 3f); Mul(w, Temper.Brave, 1.5f); Mul(w, Temper.Curious, 1.2f); Mul(w, Temper.Proud, 0.6f);
                    break;
                case "feline":
                    Mul(w, Temper.Proud, 2.5f); Mul(w, Temper.Mischievous, 1.8f); Mul(w, Temper.Lazy, 1.6f); Mul(w, Temper.Curious, 1.3f); Mul(w, Temper.Loyal, 0.5f);
                    Mul(w, Temper.Gentle, 0.8f);
                    break;
                case "hoofed":
                    Mul(w, Temper.Gentle, 1.5f); Mul(w, Temper.Timid, 1.5f); Mul(w, Temper.Loyal, 1.3f); Mul(w, Temper.Proud, 1.3f); Mul(w, Temper.Greedy, 1.1f);
                    break;
                case "grazer":
                    Mul(w, Temper.Gentle, 1.8f); Mul(w, Temper.Lazy, 1.6f); Mul(w, Temper.Greedy, 1.5f); Mul(w, Temper.Timid, 1.3f); Mul(w, Temper.Brave, 0.6f);
                    Mul(w, Temper.Mischievous, 0.8f);
                    break;
                case "ursine":
                    Mul(w, Temper.Greedy, 2f); Mul(w, Temper.Lazy, 1.8f); Mul(w, Temper.Brave, 1.4f); Mul(w, Temper.Timid, 0.5f);
                    break;
                case "thrumbo":
                    Mul(w, Temper.Gentle, 2.5f); Mul(w, Temper.Proud, 2f); Mul(w, Temper.Lazy, 1.2f); Mul(w, Temper.Timid, 0.3f); Mul(w, Temper.Mischievous, 0.4f);
                    Mul(w, Temper.Greedy, 0.6f);
                    break;
                case "bird":
                    Mul(w, Temper.Curious, 1.8f); Mul(w, Temper.Timid, 1.6f); Mul(w, Temper.Mischievous, 1.4f); Mul(w, Temper.Greedy, 1.2f); Mul(w, Temper.Lazy, 0.6f);
                    break;
                case "bug":
                    Mul(w, Temper.Greedy, 1.5f); Mul(w, Temper.Brave, 1.4f); Mul(w, Temper.Curious, 1.2f); Mul(w, Temper.Gentle, 0.5f);
                    break;
                case "reptile":
                    Mul(w, Temper.Lazy, 2f); Mul(w, Temper.Proud, 1.4f); Mul(w, Temper.Greedy, 1.3f); Mul(w, Temper.Curious, 0.7f);
                    break;
                case "small":
                    Mul(w, Temper.Timid, 2f); Mul(w, Temper.Curious, 1.6f); Mul(w, Temper.Mischievous, 1.5f); Mul(w, Temper.Greedy, 1.3f); Mul(w, Temper.Brave, 0.5f);
                    Mul(w, Temper.Proud, 0.6f);
                    break;
            }
            if (f.predator)
            {
                Mul(w, Temper.Brave, 1.6f); Mul(w, Temper.Proud, 1.4f); Mul(w, Temper.Timid, 0.5f);
            }
            if (f.bodySize >= 2.5f)
            {
                Mul(w, Temper.Brave, 1.2f); Mul(w, Temper.Gentle, 1.3f); Mul(w, Temper.Timid, 0.6f); Mul(w, Temper.Mischievous, 0.6f);
            }
            else if (f.bodySize > 0f && f.bodySize < 0.4f)
            {
                Mul(w, Temper.Timid, 1.3f); Mul(w, Temper.Brave, 0.7f);
            }
            if (f.trainability >= 2)
            {
                Mul(w, Temper.Loyal, 1.4f); Mul(w, Temper.Curious, 1.2f);
            }
            else if (f.trainability <= 0) Mul(w, Temper.Loyal, 0.6f);
            if (f.types != null)
                foreach (var type in f.types)
                    TypeLean(w, type);
            return w;
        }

        /// <summary>A Pokémon's type leans its temperament: fire brave and proud, ghost mischievous, psychic proud and curious...</summary>
        private static void TypeLean(float[] w, string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "normal": Mul(w, Temper.Loyal, 1.6f); Mul(w, Temper.Greedy, 1.3f); Mul(w, Temper.Lazy, 1.2f); break;
                case "fire": Mul(w, Temper.Brave, 1.6f); Mul(w, Temper.Proud, 1.5f); break;
                case "water": Mul(w, Temper.Gentle, 1.4f); Mul(w, Temper.Lazy, 1.3f); break;
                case "grass": Mul(w, Temper.Gentle, 1.5f); Mul(w, Temper.Lazy, 1.3f); break;
                case "electric": Mul(w, Temper.Mischievous, 1.5f); Mul(w, Temper.Curious, 1.5f); break;
                case "ice": Mul(w, Temper.Proud, 1.4f); Mul(w, Temper.Timid, 1.2f); break;
                case "fighting": Mul(w, Temper.Brave, 1.8f); Mul(w, Temper.Proud, 1.4f); break;
                case "poison": Mul(w, Temper.Mischievous, 1.5f); Mul(w, Temper.Greedy, 1.2f); break;
                case "ground": Mul(w, Temper.Brave, 1.3f); Mul(w, Temper.Lazy, 1.3f); break;
                case "flying": Mul(w, Temper.Curious, 1.4f); Mul(w, Temper.Proud, 1.3f); break;
                case "psychic": Mul(w, Temper.Proud, 1.6f); Mul(w, Temper.Curious, 1.5f); break;
                case "bug": Mul(w, Temper.Timid, 1.3f); Mul(w, Temper.Curious, 1.3f); break;
                case "rock": Mul(w, Temper.Brave, 1.3f); Mul(w, Temper.Lazy, 1.4f); break;
                case "ghost": Mul(w, Temper.Mischievous, 1.8f); Mul(w, Temper.Timid, 1.3f); break;
                case "dragon": Mul(w, Temper.Proud, 1.8f); Mul(w, Temper.Brave, 1.4f); break;
                case "dark": Mul(w, Temper.Mischievous, 1.6f); Mul(w, Temper.Proud, 1.3f); Mul(w, Temper.Greedy, 1.3f); break;
                case "steel": Mul(w, Temper.Brave, 1.4f); Mul(w, Temper.Loyal, 1.3f); break;
                case "fairy": Mul(w, Temper.Gentle, 1.6f); Mul(w, Temper.Mischievous, 1.4f); break;
            }
        }

        /// <summary>A number spread evenly from a seed (the animal's id): the same seed always gives the same number.</summary>
        public static uint Mix(int seed)
        {
            unchecked
            {
                uint x = (uint)seed * 0x9E3779B1u + 0x7F4A7C15u;
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>The temperament an animal is given, by its seed and its kind's weights: fixed for the seed, likelier where the weight is.</summary>
        public static Temper Pick(int seed, float[] weights)
        {
            if (weights == null || weights.Length != All.Length) return Temper.Curious;
            double sum = 0;
            foreach (var x in weights) sum += Math.Max(0f, x);
            if (sum <= 0) return Temper.Curious;
            double r = Mix(seed) / (double)uint.MaxValue * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                double x = Math.Max(0f, weights[i]);
                if (r < x) return (Temper)i;
                r -= x;
            }
            return (Temper)(weights.Length - 1);
        }

        // ---------------- How often a temperament shows ----------------

        /// <summary>
        /// Days between an animal's own moments of its temperament at the setting's 100%: the greedy one at the stores, the
        /// mischievous one in the crops or on a shelf every five days or so; the gentle, proud, lazy and curious every
        /// three; the brave and the timid every two, when there is something to be brave or timid about; the loyal one
        /// goes to wait at the door about twice a day while its person is away.
        /// </summary>
        public static float MomentDays(Temper t)
        {
            switch (t)
            {
                case Temper.Greedy:
                case Temper.Mischievous: return 5f;
                case Temper.Brave:
                case Temper.Timid: return 2f;
                case Temper.Loyal: return 0.5f;
                default: return 3f;
            }
        }

        /// <summary>The chance in an hour of an animal's moment, for the days between them and the setting (0 to 2): 0 at 0.</summary>
        public static float HourChance(float days, float rate)
        {
            if (rate <= 0f || days <= 0f) return 0f;
            return Math.Min(1f, rate / (days * 24f));
        }

        /// <summary>At most one temperament moment an hour on a map, so they never crowd one another.</summary>
        public const int MomentsAnHour = 1;

        /// <summary>The chance, in a fight, that a brave one stands by its person or a timid one bolts at the shooting: once a fight each.</summary>
        public static float FightChance(float rate) => rate <= 0f ? 0f : Math.Min(1f, 0.6f * rate);

        /// <summary>Hours before the same animal shows its temperament in a fight again (a fight is over by then).</summary>
        public const float FightGapHours = 8f;

        /// <summary>Smaller than this, a brave one doesn't stand between its person and an attacker (a brave hen puffs up and stays put).</summary>
        public const float StandMinSize = 0.4f;

        /// <summary>How near the shooting must be for a timid one to bolt, and its person to an attacker for a brave one to stand.</summary>
        public const float FightRange = 20f;

        /// <summary>
        /// How long a lingering moment lasts, in ticks: a wait at the door, a nap, a vigil at a grave, showing off, and a
        /// stand (an hour at most; in a fight it ends once no danger is left near).
        /// </summary>
        public const int WaitTicks = 1500, NapTicks = 1800, VigilTicks = 2000, ShowTicks = 900, StandTicks = 2500;

        // ---------------- Friendships and rivalries ----------------

        /// <summary>Nights asleep side by side before two animals are friends.</summary>
        public const int FriendNights = 3;

        /// <summary>Two not seen asleep together for this long are forgotten as a pair (before they were friends).</summary>
        public const float PairForgetDays = 10f;

        /// <summary>Friends or rivals not seen near each other for this long drift apart.</summary>
        public const float TieFadeDays = 40f;

        /// <summary>Friends each animal keeps at most, and rivals.</summary>
        public const int MaxFriends = 2, MaxRivals = 1;

        /// <summary>A night counts once: only a later day than the last one counted.</summary>
        public static bool NightCounts(int lastDay, int today) => today > lastDay;

        /// <summary>Asleep this close (in cells) is asleep side by side.</summary>
        public const float SideBySide = 1.9f;

        /// <summary>Awake this close (in cells) is close enough to quarrel, by day.</summary>
        public const float AtOdds = 6f;

        /// <summary>
        /// The chance, on a day two of the colony's companions of different kinds are about each other, that it turns to
        /// a feud: very small (one day in 250), ten times that for a cat and a dog (a month or so of days about each
        /// other), more for two that hunt and for the proud, the greedy and the mischievous, less for the gentle; never
        /// between friends or two of the same kind.
        /// </summary>
        public static float RivalChance(bool catAndDog, bool bothHunt, Temper a, Temper b, bool friends, bool sameKind)
        {
            if (friends || sameKind) return 0f;
            float c = 0.004f;
            if (catAndDog) c *= 10f;
            if (bothHunt) c *= 2f;
            foreach (var t in new[] { a, b })
                if (t == Temper.Proud || t == Temper.Greedy || t == Temper.Mischievous) c *= 1.8f;
            if (a == Temper.Gentle || b == Temper.Gentle) c *= 0.4f;
            return Math.Min(0.5f, c);
        }

        /// <summary>Days between two friends' moments by day (a nuzzle, falling in together), and between two rivals' (a hiss and a chase).</summary>
        public const float FriendDays = 1.5f, RivalDays = 2f;

        /// <summary>The chance in an hour of a pair's moment: looked at in the day's waking hours only, so a little under the days say.</summary>
        public static float TieHourChance(bool rivals) => HourChance(rivals ? RivalDays : FriendDays, 1f);

        /// <summary>A friend gone: the other misses it this long (shown on its card, and it goes to the grave if there is one).</summary>
        public const float MissDays = 30f;

        // ---------------- Grief ----------------

        /// <summary>For this long after its person died a bonded animal goes to the grave every few days; then now and then.</summary>
        public const float GriefDays = 60f, LongGriefDays = 120f;

        /// <summary>
        /// The chance in a waking hour (ten of them a day) that a grieving animal goes to the grave: about every two and a
        /// half days at first, every eight after <see cref="GriefDays"/>, none after <see cref="LongGriefDays"/>.
        /// </summary>
        public static float GraveHourChance(float daysSince)
        {
            if (daysSince < 0f || daysSince > LongGriefDays) return 0f;
            return daysSince <= GriefDays ? 1f / (2.5f * 10f) : 1f / (8f * 10f);
        }

        /// <summary>Still grieving (its card says so): within <see cref="LongGriefDays"/>.</summary>
        public static bool Grieving(float daysSince) => daysSince >= 0f && daysSince <= LongGriefDays;

        // ---------------- An old animal meets its handler ----------------

        /// <summary>Gone (sold, given, released) at least this long before it knows its handler again: not the trader's own caravan walking out.</summary>
        public const float MeetAfterDays = 2f;

        /// <summary>It remembers its handler this long after it left (two years of sixty days).</summary>
        public const float RemembersDays = 120f;

        /// <summary>Between two meetings of the same animal and handler.</summary>
        public const float MeetGapDays = 10f;

        /// <summary>It knows them again: long enough gone to be a meeting, not so long it has forgotten, and not just after the last one.</summary>
        public static bool Recognises(float daysGone, float daysSinceLastMeeting) =>
            daysGone >= MeetAfterDays && daysGone <= RemembersDays && (daysSinceLastMeeting < 0f || daysSinceLastMeeting >= MeetGapDays);

        /// <summary>How near (in cells) it must see them to come over.</summary>
        public const float MeetRange = 30f;

        // ---------------- Voices ----------------

        /// <summary>Animals speak: always On, never Off, and on Auto only while a fantasy source is there.</summary>
        public static bool Speaks(AnimalSpeech setting, bool fantasy) => setting == AnimalSpeech.On || (setting == AnimalSpeech.Auto && fantasy);

        /// <summary>A psycaster this strong (psylink level) hears their bonded animal.</summary>
        public const int StrongPsylink = 3;

        public static bool HearsByPsylink(int level) => level >= StrongPsylink;

        /// <summary>A speaking animal says something to its person about once a day at most, when the two are together and in view.</summary>
        public const float SayHourChance = 0.2f, SayGapDays = 1f;

        /// <summary>
        /// How far it senses raiders and predators, how long before it warns of the same ones again, and how long between
        /// two of its warnings of predators (raids aside).
        /// </summary>
        public const float WarnRange = 45f, WarnGapDays = 1f, PredatorWarnGapDays = 0.5f;

        /// <summary>
        /// A predator worth a warning: an angry beast, one on the hunt, or one at least four fifths its own size (a fox
        /// doesn't frighten a horse).
        /// </summary>
        public static bool Dangerous(bool angry, bool hunting, float itsSize, float mySize) => angry || hunting || itsSize >= 0.8f * mySize;

        /// <summary>The share of the council's questions about its person, or its person's animal, that a speaking animal speaks up on.</summary>
        public const float DilemmaShare = 0.5f;

        /// <summary>
        /// Its voice: a Pokémon's own, else by family (a dog's, a cat's, a horse's, the herd's, a bear's, a thrumbo's, a
        /// bird's, a bug's, a reptile's, a small creature's), else a beast's. Lines are tagged voice:dog and so on.
        /// </summary>
        public static string Voice(NatureFacts f)
        {
            if (f.pokemon) return "pokemon";
            switch (f.family)
            {
                case "canine": return "dog";
                case "feline": return "cat";
                case "hoofed": return "horse";
                case "grazer": return "herd";
                case "ursine": return "bear";
                case "thrumbo": return "thrumbo";
                case "bird": return "bird";
                case "bug": return "bug";
                case "reptile": return "reptile";
                case "small": return "small";
                default: return "beast";
            }
        }

        /// <summary>
        /// Where something is from where one stands, as a word of the compass (north is up the map): north, northeast,
        /// east... Within a cell, "here".
        /// </summary>
        public static string Compass(int dx, int dz)
        {
            if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1) return "here";
            double angle = Math.Atan2(dz, dx) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            string[] names = { "east", "northeast", "north", "northwest", "west", "southwest", "south", "southeast" };
            int i = (int)Math.Round(angle / 45.0) % 8;
            return names[i];
        }

        // ---------------- What is kept ----------------

        /// <summary>Temperaments kept (one an animal), friendships and rivalries (with the pairs still being watched), and animals gone with their handler.</summary>
        public const int MaxMarks = 300, MaxTies = 80, MaxGone = 30;

        /// <summary>An animal not seen for this long is let go from the temperaments kept (one away in a ball, a caravan or a cryptosleep pod comes back to the same one, by its id).</summary>
        public const float MarkForgetDays = 120f;

        /// <summary>How many hours of a day are its waking hours for the grief's chance (6 to 18 by the clock, ten of them looked at).</summary>
        public const int WakingHours = 10;
    }
}
