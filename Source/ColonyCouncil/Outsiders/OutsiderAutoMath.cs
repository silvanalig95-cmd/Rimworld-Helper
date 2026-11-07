using System;

namespace ColonyCouncil
{
    /// <summary>
    /// What colonists do with people from outside the colony by themselves, without the right-click (the setting): nothing,
    /// only the things that spend nothing of the colony's and risk nothing beyond a talk going cool, or those and the ones
    /// that spend a little of the stores or take a small risk, each only when it is clearly worth it and within the day's
    /// budget.
    /// </summary>
    public enum OutsiderAutoMode
    {
        Off,
        FreeOnly,
        WithinLimits
    }

    /// <summary>How a thing done with an outsider may happen on its own.</summary>
    public enum AutoTier
    {
        /// <summary>Spends nothing of the colony's: done by themselves whenever the setting is on.</summary>
        Free,
        /// <summary>Spends a little of the stores or takes a small risk: done by themselves only within limits, when clearly worth it.</summary>
        Limited,
        /// <summary>The player's own decision: never done by themselves.</summary>
        Manual
    }

    /// <summary>What one thing with one outsider would bring now, for ranking it (no game types).</summary>
    public struct AutoOption
    {
        public OutsiderAct act;
        /// <summary>How the outsider regards the colony and the colonist, -100 to 100.</summary>
        public int ofColony, ofColonist;
        /// <summary>The pair's record: the bond (-100 to 100), how well they know each other (0 to 100), visits, friends, sweethearts.</summary>
        public float warmth, familiarity;
        public int visits;
        public bool friends, sweethearts;
        /// <summary>Their people's goodwill, -100 to 100.</summary>
        public int goodwill;
        /// <summary>News of their town heard in the last two days (from anyone of theirs).</summary>
        public bool heardLately;
        /// <summary>The same faith (a talk of faith with another is the player's).</summary>
        public bool sameFaith;
        /// <summary>A courting talk's chance (the game's romance chance, by how well they know each other).</summary>
        public float courtChance;
        /// <summary>A lesson: the colonist's passion for the skill (0 none, 1, 2) and how far the teacher leads.</summary>
        public int passion, lead;
        /// <summary>A bout: the chance they'd take it up, and the colonist keen on it (a passion for melee or a fighting temper).</summary>
        public float sparChance;
        public bool keen;
        /// <summary>A meal: the colonist's food (0..1); a drink: their recreation (0..1).</summary>
        public float food, joy;
        /// <summary>Their recreation time by the schedule.</summary>
        public bool joyTime;
        /// <summary>A gift: how it suits them (OutsiderMath.GiftFit), whether it's the first of their visit, someone the colony knows by name.</summary>
        public float giftFit;
        public bool firstGift, notable;
    }

    /// <summary>Who might do it: what makes one colonist the right one for it (no game types).</summary>
    public struct AutoWho
    {
        public OutsiderAct act;
        public int social;
        /// <summary>How the outsider regards the colonist, and how well the two know each other.</summary>
        public int ofColonist;
        public float familiarity;
        /// <summary>The outsider is someone the colony knows by name (a notable).</summary>
        public bool notable;
        /// <summary>The colonist's seat: the speaker or the steward, the warden of guests, the chaplain.</summary>
        public bool speakerOrSteward, warden, chaplain;
        /// <summary>A lesson's passion and lead, a bout's melee level and keenness, a meal's food, a drink's recreation.</summary>
        public int passion, lead, melee;
        public bool keen;
        public float food, joy;
        public bool shy;
        /// <summary>Cells to walk to them.</summary>
        public float distance;
        /// <summary>They did the last one the colony did by themselves; how many they did today.</summary>
        public bool wasLast;
        public int today;
    }

    /// <summary>What the pace looks at before anything is done by themselves.</summary>
    public struct AutoPace
    {
        public int now;
        /// <summary>Done by themselves with anyone today, and when the last one was (-1 never).</summary>
        public int today, last;
        /// <summary>Done with this outsider today, and when the last one with them was (-1 never).</summary>
        public int withThem, lastWithThem;
        /// <summary>Their patience left (OutsiderTalk.Left).</summary>
        public int patienceLeft;
        /// <summary>When they last talked on their own with anyone of the colony (-1 never).</summary>
        public int chatterLast;
    }

    /// <summary>
    /// The pure rules of colonists doing the right-click things with outsiders by themselves (Outsiders/OutsiderAuto.cs):
    /// which things may happen on their own on which setting, the moment, the pace (a few a day, two with one outsider,
    /// never their last patience), the budget of the stores, what each thing would bring now, and who of the colony is the
    /// one to do it. Everything else (the waits, patience, asking twice, who is willing) is the right-click's own, checked
    /// the same way for both. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class OutsiderAutoMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        // ---------------- Which things ----------------

        /// <summary>
        /// How each thing may happen on its own. Free: news of home, getting to know them, a good word, a talk of the same
        /// faith. Within limits: a meal, a drink, a gift, a lesson, a practice bout, courting a sweetheart. The player's
        /// alone: a chat (the talks of their own already are one), a letter, something brought, an invitation to stay, hard
        /// words, a duel, and the diplomacy of favours, pacts, matches and exchanges.
        /// </summary>
        public static AutoTier Tier(OutsiderAct a)
        {
            switch (a)
            {
                case OutsiderAct.AskHome:
                case OutsiderAct.Befriend:
                case OutsiderAct.GoodWord:
                case OutsiderAct.Faith:
                    return AutoTier.Free;
                case OutsiderAct.Meal:
                case OutsiderAct.Drink:
                case OutsiderAct.Gift:
                case OutsiderAct.Teach:
                case OutsiderAct.Spar:
                case OutsiderAct.Court:
                    return AutoTier.Limited;
                default:
                    return AutoTier.Manual;
            }
        }

        public static bool Allowed(OutsiderAutoMode mode, OutsiderAct a)
        {
            var t = Tier(a);
            if (t == AutoTier.Manual) return false;
            return mode == OutsiderAutoMode.WithinLimits || mode == OutsiderAutoMode.FreeOnly && t == AutoTier.Free;
        }

        /// <summary>The things looked at, in the order they are weighed (a tie goes to the first).</summary>
        public static readonly OutsiderAct[] Order =
        {
            OutsiderAct.GoodWord, OutsiderAct.Befriend, OutsiderAct.Court, OutsiderAct.AskHome, OutsiderAct.Faith, OutsiderAct.Teach, OutsiderAct.Meal,
            OutsiderAct.Drink, OutsiderAct.Gift, OutsiderAct.Spar
        };

        // ---------------- The moment ----------------

        /// <summary>The look runs every this many of the outsiders' 250-tick looks (every 1,000 ticks).</summary>
        public const int LookEvery = 4;

        /// <summary>A good moment is taken at this chance a look, so nothing starts the moment they arrive.</summary>
        public const float TakeChance = 0.35f;

        /// <summary>The colonist walks this far at most to them (cells).</summary>
        public const float Range = 24f;

        /// <summary>The colonists looked at for one outsider, the nearest first.</summary>
        public const int NearestColonists = 6;

        /// <summary>The best few pairings checked in full (the right-click's own reasons and the stores) for one outsider.</summary>
        public const int FullChecks = 6;

        /// <summary>Outsiders weighed in one look at most (in a shuffled order), so a big caravan costs no more than a small one.</summary>
        public const int OutsidersALook = 3;

        /// <summary>
        /// A colonist free for it: awake, up, not drafted, not in a mental state, not at something the player ordered or
        /// that matters (only idle, wandering or at leisure), not hungry or tired enough to need seeing to.
        /// </summary>
        public static bool ColonistFree(bool awake, bool downed, bool drafted, bool mental, bool playerForced, bool atLeisure, bool needsPress) =>
            awake && !downed && !drafted && !mental && !playerForced && atLeisure && !needsPress;

        /// <summary>An outsider free for it: not trading, not leaving or passing through, not busy with their own, in no danger.</summary>
        public static bool OutsiderFree(bool busy, bool leaving, bool passing, bool trading, bool inScene, bool sought) =>
            !busy && !leaving && !passing && !trading && !inScene && !sought;

        // ---------------- The pace ----------------

        /// <summary>At most this many a day across the colony, two with one outsider, two hours between any two on a home.</summary>
        public const int PerDay = 5;
        public const int PerOutsiderDay = 2;
        public const float GapHours = 2f;
        /// <summary>The same outsider again only after this long.</summary>
        public const float OutsiderGapHours = 4f;
        /// <summary>The same colonist starts one at most once in this long.</summary>
        public const float ColonistGapHours = 6f;
        /// <summary>Not within this long of a talk of their own with them.</summary>
        public const float AfterChatterHours = 1f;
        /// <summary>Their last patience is never taken: that one is left for the player's own right-click.</summary>
        public const int PatienceKept = 1;
        /// <summary>One practice bout a day at most by themselves.</summary>
        public const int BoutsADay = 1;

        public static int Day(int tick) => tick < 0 ? -1 : tick / TicksPerDay;

        public static bool Waited(int last, int now, float hours) => last < 0 || now < last || now - last >= hours * TicksPerHour;

        /// <summary>The pace allows one more with this outsider now.</summary>
        public static bool PaceAllows(AutoPace p) =>
            p.today < PerDay && p.withThem < PerOutsiderDay && p.patienceLeft > PatienceKept && Waited(p.last, p.now, GapHours)
            && Waited(p.lastWithThem, p.now, OutsiderGapHours) && Waited(p.chatterLast, p.now, AfterChatterHours);

        /// <summary>The colonist may start another by themselves (a while since their last).</summary>
        public static bool ColonistRested(int lastByThem, int now) => Waited(lastByThem, now, ColonistGapHours);

        // ---------------- The budget of the stores ----------------

        /// <summary>
        /// What the colony spends of its stores by itself in a day, in silver: a four-hundredth of its wealth, 30 to 250
        /// (a colony of 12,000 spends 30, one of 40,000 100, one of 100,000 or more 250).
        /// </summary>
        public static float Budget(float wealth) => Math.Max(30f, Math.Min(250f, wealth / 400f));

        public static float BudgetLeft(float wealth, float spentToday) => Math.Max(0f, Budget(wealth) - Math.Max(0f, spentToday));

        public static bool Affords(float value, float wealth, float spentToday) => value >= 0f && value <= BudgetLeft(wealth, spentToday) + 0.01f;

        /// <summary>A shared meal only with this many meals to spare in the stores beyond one for each colonist.</summary>
        public const int MealsSpare = 2;

        public static bool MealsToSpare(int meals, int colonists) => meals >= Math.Max(0, colonists) + MealsSpare;

        /// <summary>A drink only with at least this many in the stores.</summary>
        public const int DrinksKept = 4;

        /// <summary>A gift only of something the colony keeps at least this many times as much of.</summary>
        public const int GiftKeptTimes = 3;

        public static bool GiftFromPlenty(int count, int inStores) => count > 0 && inStores >= count * GiftKeptTimes;

        /// <summary>A gift by themselves is worth at least this much, in silver (less isn't worth the walk).</summary>
        public const float GiftLeast = 20f;

        /// <summary>
        /// How many to give by themselves: the right-click's share of the stack (about 120 silver), no more than the day's
        /// budget left buys, and none when that comes to less than <see cref="GiftLeast"/>.
        /// </summary>
        public static int GiftCount(float unitValue, int stack, float budgetLeft)
        {
            if (unitValue <= 0f) return 0;
            int n = OutsiderMath.GiftCount(unitValue, stack);
            n = Math.Min(n, (int)Math.Floor((Math.Max(0f, budgetLeft) + 0.01f) / unitValue));
            return n <= 0 || n * unitValue < GiftLeast ? 0 : n;
        }

        /// <summary>A meal when it's due soon anyway: the colonist's food at or below this.</summary>
        public const float MealHunger = 0.45f;

        /// <summary>A drink when the colonist wants recreation: at or below this, or in their recreation time.</summary>
        public const float DrinkJoy = 0.5f;

        /// <summary>Courting a sweetheart by themselves only when it's likelier to go well than not.</summary>
        public const float CourtAtLeast = 0.5f;

        /// <summary>A bout by themselves only with someone who'd likely take it up.</summary>
        public const float SparAtLeast = 0.5f;

        // ---------------- What it would bring ----------------

        /// <summary>
        /// What one thing would bring now, before who does it; less than zero when it isn't worth doing by themselves.
        /// A good word with their town's head lasts longest (6, and more the better they regard the colony); getting to
        /// know them 4, 7 when the two are close to becoming friends, 2 once they are; courting a sweetheart 5 when it's
        /// likely to go well; news 4, 1 with their town's news heard lately; the same faith 3; a lesson 3 and the
        /// colonist's passion for it; a meal 4 when the colonist would eat soon anyway; a drink 3 at leisure; a gift 2 that
        /// suits them, 4 when their people are cool toward the colony, the first of the visit only; a bout 3 between two
        /// keen on it. Dislike of the colonist takes a little off everything.
        /// </summary>
        public static float Worth(AutoOption o)
        {
            float w;
            switch (o.act)
            {
                case OutsiderAct.GoodWord:
                    w = 6f + Clamp(o.ofColony, 0, 100) * 0.02f;
                    break;
                case OutsiderAct.Befriend:
                    if (o.friends) w = 2f;
                    else w = NearFriends(o.warmth, o.familiarity, o.visits) ? 7f : 4f;
                    break;
                case OutsiderAct.Court:
                    if (!o.sweethearts || o.courtChance < CourtAtLeast) return -1f;
                    w = 5f;
                    break;
                case OutsiderAct.AskHome:
                    w = o.heardLately ? 1f : 4f;
                    break;
                case OutsiderAct.Faith:
                    if (!o.sameFaith) return -1f;
                    w = 3f;
                    break;
                case OutsiderAct.Teach:
                    if (o.passion <= 0 || o.lead < OutsiderMath.TeachLead) return -1f;
                    w = 3f + Math.Min(2, o.passion) + Math.Min(3, o.lead - OutsiderMath.TeachLead) * 0.3f;
                    break;
                case OutsiderAct.Meal:
                    if (o.food > MealHunger) return -1f;
                    w = 4f;
                    break;
                case OutsiderAct.Drink:
                    if (o.joy > DrinkJoy && !o.joyTime) return -1f;
                    w = 3f;
                    break;
                case OutsiderAct.Gift:
                    if (!o.firstGift || o.giftFit < 1.5f) return -1f;
                    w = (o.goodwill < 25 ? 4f : 2f) + (o.notable ? 1f : 0f);
                    break;
                case OutsiderAct.Spar:
                    if (!o.keen || o.sparChance < SparAtLeast) return -1f;
                    w = 3f;
                    break;
                default:
                    return -1f;
            }
            if (o.ofColonist < 0) w -= Math.Min(3f, -o.ofColonist * 0.05f);
            w += Math.Max(0f, Math.Min(100f, o.familiarity)) * 0.01f;
            return w;
        }

        /// <summary>Close to becoming friends: within reach of the bond and the knowing it takes, a visit or more behind them.</summary>
        public static bool NearFriends(float warmth, float familiarity, int visits) =>
            warmth >= OutsiderMath.FriendWarmth - 12f && familiarity >= OutsiderMath.FriendKnowing - 12f && visits >= OutsiderMath.FriendVisits - 1;

        // ---------------- Who does it ----------------

        /// <summary>
        /// How right one colonist is for it: their way with words (0.3 a level), how the outsider regards them (0.05 a
        /// point) and how well the two know each other (0.03 a point); the speaker or the steward with someone the colony
        /// knows by name, and for a good word (3), the warden of guests with any outsider (1.5), the chaplain for faith (3);
        /// for a lesson the learner's passion (2 a step) and how far they'd learn, for a bout melee and keenness, for a
        /// meal hunger, for a drink the want of recreation; less for the shy (2), for a long walk (0.05 a cell), for
        /// whoever did the last one (4) and for each they did today (2), so it is never the same colonist over and over.
        /// </summary>
        public static float WhoScore(AutoWho w)
        {
            float s = Clamp(w.social, 0, 20) * 0.3f + Clamp(w.ofColonist, -100, 100) * 0.05f + Math.Max(0f, Math.Min(100f, w.familiarity)) * 0.03f;
            if (w.speakerOrSteward && (w.notable || w.act == OutsiderAct.GoodWord)) s += 3f;
            if (w.warden) s += 1.5f;
            switch (w.act)
            {
                case OutsiderAct.Faith:
                    if (w.chaplain) s += 3f;
                    break;
                case OutsiderAct.Teach:
                    s += Math.Min(2, Math.Max(0, w.passion)) * 2f + Math.Min(8, Math.Max(0, w.lead)) * 0.2f;
                    break;
                case OutsiderAct.Spar:
                    s += Clamp(w.melee, 0, 20) * 0.15f + (w.keen ? 2f : 0f);
                    break;
                case OutsiderAct.Meal:
                    s += (1f - Clamp01(w.food)) * 3f;
                    break;
                case OutsiderAct.Drink:
                    s += (1f - Clamp01(w.joy)) * 2f;
                    break;
            }
            if (w.shy) s -= 2f;
            s -= Math.Max(0f, w.distance) * 0.05f;
            if (w.wasLast) s -= 4f;
            s -= Math.Max(0, w.today) * 2f;
            return s;
        }

        /// <summary>The thing and the colonist together: what it would bring and how right they are for it.</summary>
        public static float Score(float worth, float who) => worth < 0f ? float.MinValue : worth * 2f + who;

        // ---------------- Arithmetic ----------------

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
