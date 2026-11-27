using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The part of a camper's day an hour belongs to.</summary>
    public enum CampHour
    {
        /// <summary>In their own bed.</summary>
        Night,
        /// <summary>Up, a bite at the table or the fire before work.</summary>
        Morning,
        /// <summary>The day's work, by the person and the kind's calling.</summary>
        Work,
        /// <summary>A meal at midday.</summary>
        Noon,
        /// <summary>Supper cooked at the fire and eaten at the table.</summary>
        Supper,
        /// <summary>By the fire: talk among themselves and with colonists who come by.</summary>
        Evening
    }

    /// <summary>One kind of day work a camper can do (CampKindDef.works, CampWork.cs).</summary>
    public enum CampLabour
    {
        Chop,
        Hunt,
        Fish,
        Mine,
        Gather,
        Craft,
        Haul,
        Mend
    }

    /// <summary>
    /// A camper's day (Camps/CampWork.cs): the hours of sleep, meals, work and the evening by the fire, which work a camper
    /// takes up by what they're good at and what their kind comes to do, and how much of each the camp does in a day, so
    /// the work looks busy and the land isn't stripped in a stay. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampDayMath
    {
        /// <summary>The part of the day an hour is: night from ten to six, a morning bite, work from seven, noon, work again, supper from five, the evening from seven.</summary>
        public static CampHour HourOf(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (hour >= 22 || hour < 6) return CampHour.Night;
            if (hour < 7) return CampHour.Morning;
            if (hour < 12) return CampHour.Work;
            if (hour < 13) return CampHour.Noon;
            if (hour < 17) return CampHour.Work;
            if (hour < 19) return CampHour.Supper;
            return CampHour.Evening;
        }

        /// <summary>
        /// Hungry enough to sit down to a meal now: below this at a mealtime (a meal of theirs fills about half the need, so
        /// none is wasted), below <see cref="Starving"/> at any hour.
        /// </summary>
        public static float MealBelow(CampHour h) => h == CampHour.Morning || h == CampHour.Noon || h == CampHour.Supper ? 0.55f : Starving;

        public const float Starving = 0.3f;

        /// <summary>
        /// How many of a work the whole camp does in a day: the kind's own rate where it has one (a fraction is a chance,
        /// rolled in <see cref="Roll"/>), else what its calling brings, by how many grown hands it has; nothing it doesn't do.
        /// </summary>
        public static float PerDay(CampLabour w, bool calling, float kindRate, int hands)
        {
            if (hands <= 0) return 0f;
            if (kindRate > 0f) return kindRate * (calling ? 1.5f : 1f);
            switch (w)
            {
                // Wood for the fire and the building, a tree or two a day; a woodcutters' camp its own rate.
                case CampLabour.Chop: return calling ? 1f + hands * 0.4f : 0.6f + hands * 0.1f;
                case CampLabour.Hunt: return calling ? 0.8f : 0f;
                // A catch or two where there's water with fish in it.
                case CampLabour.Fish: return calling ? hands * 2f : 0.5f + hands * 0.15f;
                // Plain rock: a stonecutters' or prospectors' camp works a face, anyone else digs a little for a hearth stone.
                case CampLabour.Mine: return calling ? 2f + hands : 0f;
                // Berries, herbs, wild fruit: a few plants a day.
                case CampLabour.Gather: return calling ? 2f + hands : 0.5f + hands * 0.25f;
                // Something simple at their spot: a piece a day, more for a crafting kind.
                case CampLabour.Craft: return calling ? Math.Min(3f, 0.5f + hands * 0.5f) : 0.5f;
                default: return 99f;
            }
        }

        /// <summary>A day's count from a rate: the whole part, and the fraction as a chance (<paramref name="roll"/> 0 to 1).</summary>
        public static int Roll(float perDay, double roll)
        {
            if (perDay <= 0f) return 0;
            int n = (int)Math.Floor(perDay);
            return n + (roll < perDay - n ? 1 : 0);
        }

        /// <summary>
        /// How much of the land a stay may take at most, whatever the rates: a tree for every two cells of its reach's
        /// grown trees in ten, plain rock to a dozen cells a day, game to a third of the wild beasts that are fair game.
        /// </summary>
        public static int StayCap(CampLabour w, int available, float days)
        {
            if (available <= 0) return 0;
            switch (w)
            {
                case CampLabour.Chop: return Math.Max(1, available / 5);
                case CampLabour.Hunt: return Math.Max(1, available / 3);
                case CampLabour.Mine: return Math.Max(1, (int)(12 * Math.Max(1f, days)));
                case CampLabour.Gather: return Math.Max(1, available / 2);
                default: return available;
            }
        }

        /// <summary>
        /// The order a grown camper tries the day's work in: their kind's callings first (in the order the kind gives them),
        /// then what they're best at, then the camp's common work (wood, gathering, a catch), then hauling and mending.
        /// <paramref name="skills"/>: their skill for each work (0 to 20, -1 when they can't do it at all).
        /// </summary>
        public static List<CampLabour> Order(IList<CampLabour> callings, Func<CampLabour, int> skills)
        {
            var list = new List<CampLabour>();
            void Add(CampLabour w)
            {
                if (!list.Contains(w) && skills(w) >= 0) list.Add(w);
            }
            if (callings != null)
                foreach (var w in callings) Add(w);
            var rest = new List<CampLabour> { CampLabour.Chop, CampLabour.Gather, CampLabour.Fish, CampLabour.Craft };
            rest.Sort((a, b) => skills(b).CompareTo(skills(a)));
            foreach (var w in rest) Add(w);
            Add(CampLabour.Haul);
            Add(CampLabour.Mend);
            return list;
        }

        /// <summary>A building of theirs is mended when it is below this share of its hit points.</summary>
        public const float MendBelow = 0.8f;

        /// <summary>
        /// The evening's talk: each hour by the fire, the chance one of them says something to another sitting there (one
        /// line, through the speech system), more with more of them sitting together.
        /// </summary>
        public static float TalkChance(int sitting) => sitting < 2 ? 0f : Math.Min(0.6f, 0.2f + 0.1f * sitting);

        // ---------------- Their goods into their own storage ----------------

        /// <summary>
        /// One of their goods is carried into their own storage (their shelf, a basket, a firewood rack: Camps/CampStore.cs):
        /// it is theirs, lying loose at their camp, not in one of their stores already, not a body (bodies go to the
        /// butcher's spot), not something they put out on purpose (bales by the beasts, a woodpile by the door, a stall's
        /// wares), not in the colony's storage or home area, nobody else has it in hand, and one of their stores takes it
        /// and has room. Until 15 November 2026 anything within three and a half cells of the stash's corner counted as put
        /// away, and goods were set down only on bare ground beside the shelf, so nothing ever went onto it.
        /// </summary>
        public static bool Stows(StowFacts f) => f.theirs && f.atCamp && !f.corpse && !f.putOut && !f.inStore && !f.inColony && !f.busy && f.storeTakes;

        /// <summary>
        /// A stack they brought to build with goes back in a pack (Camps/CampStore.cs): once their camp stands and nothing of
        /// theirs is under way, and no piece of theirs still needs it; never what they put out on purpose, what someone has in
        /// hand, or what the colony took. Until 21 November 2026 it lay where it was set down for the rest of the stay (the
        /// grove-keepers' leather in the player's save of that day, half gone in the weather). Since 27 November 2026 only
        /// where no store of theirs takes it (a camp with no shelf or store tent, or one full): what the group shares goes in
        /// their store, a pack is for a person's own things (the player: "they should keep to their personal inventory ...
        /// what will be only relevant for this pawn individually, and things that could be shared or used or gathered from
        /// different visitors should be put into the general storage"); it is shared out among the packs when they go
        /// (<see cref="TakeFirst"/>).
        /// </summary>
        public static bool Repacks(RepackFacts f) => f.brought && f.theirs && f.atCamp && f.settled && !f.underWay && !f.needed && !f.putOut && !f.busy && !f.inColony
                                                    && !f.inStore && !f.storeTakes;

        /// <summary>
        /// Leaving in their own time, what of the camp's goods they take first, as far as they can carry (the player: "they
        /// should take out and with them when they leave whatever they still need ... or maybe leave back what they don't
        /// need"): food and medicine first, then what is worth the most for its weight (silver, fine things), the bulky and
        /// cheap last (logs, stone), and what doesn't fit is left at their camp for anyone. Higher goes first.
        /// </summary>
        public static float TakeFirst(bool food, bool medicine, float valuePerKilo) => (food || medicine ? 1000f : 0f) + Math.Max(0f, Math.Min(valuePerKilo, 999f));

        /// <summary>How much of a stack one trip carries into the store: what the cell has room for and the carrier can lift, at least one.</summary>
        public static int StowCount(int stack, int room, int carry) => Math.Max(1, Math.Min(stack, Math.Min(Math.Max(1, room), Math.Max(1, carry))));

        // ---------------- Clean ground round their camp ----------------

        /// <summary>
        /// Filth they sweep or wash away (Camps/CampClean.cs): the mess people and their work make (blood, vomit, ash, trash,
        /// dirt tracked about, rubble, animal filth, bile, spilled fuel, slime), never what the land itself lays down (leaves,
        /// petals, sand, snow, water, mud, moss). By the def's name, so the mods' own blood and trash are covered.
        /// </summary>
        public static bool Mess(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            string n = defName.ToLowerInvariant();
            foreach (var land in LandFilth)
                if (n.Contains(land)) return false;
            foreach (var mess in MessFilth)
                if (n.Contains(mess)) return true;
            return false;
        }

        private static readonly string[] MessFilth = { "blood", "vomit", "ash", "trash", "dirt", "rubble", "animalfilth", "bile", "fuel", "slime", "gore", "offal", "hair", "puddle" };
        private static readonly string[] LandFilth = { "leaves", "leaf", "petal", "sand", "snow", "water", "mud", "moss", "pollen", "seed" };

        /// <summary>
        /// They clean this filth: it is a mess (<see cref="Mess"/>), on their own ground (within their camp's reach), and
        /// never on the colony's (its home area, a zone, a plan, near its land) nor in a room under a roof that isn't theirs.
        /// </summary>
        public static bool Cleans(CleanFacts f) => f.mess && f.onTheirGround && !f.colonyGround && !f.colonyRoom && !f.reserved;

        /// <summary>The chance, at a free hour, that one of them looks round for filth to clean: the orderly often, the careless seldom.</summary>
        public static float CleanChance(CampTidy t) => t == CampTidy.Orderly ? 0.8f : t == CampTidy.Careless ? 0.15f : 0.45f;

        /// <summary>How many bits of filth lie on their ground before anyone bothers: one for the orderly, three usually, eight for the careless.</summary>
        public static int CleanFrom(CampTidy t) => t == CampTidy.Orderly ? 1 : t == CampTidy.Careless ? 8 : 3;

        /// <summary>At most this many at their cleaning at once (one, two for a big camp of the orderly).</summary>
        public static int Cleaners(CampTidy t, int people) => t == CampTidy.Orderly && people >= 6 ? 2 : 1;

        /// <summary>Going in their own time, the orderly tidy their site before they set off (their packing hours wait for it).</summary>
        public static bool TidiesBeforeLeaving(CampTidy t, bool orderlyGoing) => orderlyGoing && t == CampTidy.Orderly;
    }

    /// <summary>How tidy a kind of camp keeps its ground (CampKindDef.tidy): orderly, as most, or careless.</summary>
    public enum CampTidy
    {
        Usual,
        Orderly,
        Careless
    }

    /// <summary>What there is to know about one of a camp's goods, for whether it is carried into their own storage (CampDayMath.Stows).</summary>
    public struct StowFacts
    {
        /// <summary>One of their goods (the camp owns it).</summary>
        public bool theirs;
        /// <summary>Lying within their camp's reach.</summary>
        public bool atCamp;
        /// <summary>A body: dressed at the butcher's spot, never shelved.</summary>
        public bool corpse;
        /// <summary>Put out on purpose: bales by the beasts, a woodpile by the door, a stall's wares (porters move those).</summary>
        public bool putOut;
        /// <summary>In one of their own stores already.</summary>
        public bool inStore;
        /// <summary>In the colony's storage or its home area: taken, not theirs to fetch.</summary>
        public bool inColony;
        /// <summary>Someone has it in hand or reserved.</summary>
        public bool busy;
        /// <summary>One of their stores takes it and has room for it.</summary>
        public bool storeTakes;
    }

    /// <summary>What there is to know about a stack they brought to build with, for whether it goes back in a pack (CampDayMath.Repacks).</summary>
    public struct RepackFacts
    {
        /// <summary>They carried it in and set it down at their stash to build with.</summary>
        public bool brought;
        /// <summary>One of their goods (the camp owns it).</summary>
        public bool theirs;
        /// <summary>Lying within their camp's reach.</summary>
        public bool atCamp;
        /// <summary>Their camp stands and they live in it (not packing up to go).</summary>
        public bool settled;
        /// <summary>Something of theirs is still being built, or something added to the camp.</summary>
        public bool underWay;
        /// <summary>A piece of theirs still needs some of it.</summary>
        public bool needed;
        /// <summary>Put out on purpose: bales by the beasts, a woodpile by the door, a stall's wares.</summary>
        public bool putOut;
        /// <summary>Someone has it in hand or reserved.</summary>
        public bool busy;
        /// <summary>In one of their own stores already, or a store of theirs takes it and has room: it stays with the group's goods.</summary>
        public bool inStore, storeTakes;
        /// <summary>In the colony's storage or its home area: taken, not theirs to fetch.</summary>
        public bool inColony;
    }

    /// <summary>What there is to know about a bit of filth near a camp, for whether they clean it (CampDayMath.Cleans).</summary>
    public struct CleanFacts
    {
        /// <summary>Mess that people make (CampDayMath.Mess), not what the land lays down.</summary>
        public bool mess;
        /// <summary>Within their camp's reach of their fire.</summary>
        public bool onTheirGround;
        /// <summary>The colony's: its home area or near it, a zone, a plan or a mark.</summary>
        public bool colonyGround;
        /// <summary>Indoors under a roof that isn't one of theirs.</summary>
        public bool colonyRoom;
        /// <summary>Someone else is at it already.</summary>
        public bool reserved;
    }
}
