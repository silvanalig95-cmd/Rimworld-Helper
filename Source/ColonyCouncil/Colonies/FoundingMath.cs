using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a grown colonist would rather do when a new colony is founded from their home (the player's decision 2A).</summary>
    public enum FoundingWish
    {
        Torn,
        Go,
        Stay
    }

    /// <summary>How the founding party reaches the new place: on foot by the journey planner, by the colony's own transport pods or shuttle, or the gravship; or nobody goes from the colony (newcomers only).</summary>
    public enum FoundingMeans
    {
        Caravan,
        Pods,
        Gravship,
        Newcomers
    }

    /// <summary>Where a founding stands: the party on its way (or the newcomers still to be chosen), or the colony standing.</summary>
    public enum FoundingStage
    {
        Going,
        Founded
    }

    /// <summary>How a colony on the ledger ended (LedgerColony). Release 2 adds the hand-over; the others come with the game's own ends.</summary>
    public enum ColonyEnd
    {
        None,
        Abandoned,
        HandedOver,
        Lost,
        TakenBack
    }

    /// <summary>
    /// What one grown colonist's ties say about a new colony founded from their home: who of their own goes and who
    /// stays, how they get on with the rest, what holds them here. Counts, not people, so Tools/Tests can check the rule.
    /// </summary>
    public struct FoundingTies
    {
        /// <summary>Their spouse, lover or betrothed, their children and their parents: going with the party, or staying.</summary>
        public int kinGoing, kinStaying;
        /// <summary>Close friends (a good opinion both ways): going, or staying.</summary>
        public int friendsGoing, friendsStaying;
        /// <summary>Rivals (a bad opinion): going, or staying.</summary>
        public int rivalsGoing, rivalsStaying;
        /// <summary>At odds with most here: the fights the council counts against them (BanishMath's measure).</summary>
        public bool atOdds;
        /// <summary>A room of their own at this home.</summary>
        public bool ownRoom;
        /// <summary>A seat on this colony's council (an officer who goes steps down there).</summary>
        public bool office;
        /// <summary>Old (the game's elder years) or young (a grown colonist not yet 25).</summary>
        public bool elder, young;
    }

    /// <summary>
    /// The rules of a fresh start in the same world (docs/design/stage3-many-colonies.md, release 1), with no game types
    /// so Tools/Tests can check them: each grown colonist's wish and what goes against it; who may be sent and the checks
    /// on the party; the newcomers' price and how often they may come; the seat, once a season; the game's colony limit,
    /// raised by one and set back; the founding kit by the colony's age; whose bond is split; and the colonies' record.
    /// </summary>
    public static class FoundingMath
    {
        public const int DayTicks = 60000;
        public const int SeasonTicks = 15 * DayTicks;
        public const int YearTicks = 60 * DayTicks;

        // ---------------- Each one's wish (decision 2A) ----------------

        /// <summary>One of their own (spouse, lover, child, parent) pulls this hard toward where they go.</summary>
        public const float KinPull = 3f;
        /// <summary>A close friend pulls this hard; a rival pushes this hard the other way.</summary>
        public const float FriendPull = 1f, RivalPush = 1f;
        /// <summary>At odds with most here: a reason to go.</summary>
        public const float AtOddsPush = 2f;
        /// <summary>Their own room, a seat on the council, old age: each a reason to stay; youth a reason to go.</summary>
        public const float RoomHold = 1f, OfficeHold = 1f, ElderHold = 1f, YoungPull = 1f;
        /// <summary>Past this either way they want to go or to stay; between, torn.</summary>
        public const float WishLine = 1.5f;

        /// <summary>Their pull toward going (above zero) or staying (below), from their ties.</summary>
        public static float Pull(FoundingTies t)
        {
            float s = 0f;
            s += KinPull * (t.kinGoing - t.kinStaying);
            s += FriendPull * (t.friendsGoing - t.friendsStaying);
            // A rival going is a reason to stay; one staying, a reason to go.
            s += RivalPush * (t.rivalsStaying - t.rivalsGoing);
            if (t.atOdds) s += AtOddsPush;
            if (t.ownRoom) s -= RoomHold;
            if (t.office) s -= OfficeHold;
            if (t.elder) s -= ElderHold;
            if (t.young) s += YoungPull;
            return s;
        }

        /// <summary>Their wish from their ties: go past the line, stay below it, torn between.</summary>
        public static FoundingWish WishOf(FoundingTies t)
        {
            float s = Pull(t);
            return s >= WishLine ? FoundingWish.Go : s <= -WishLine ? FoundingWish.Stay : FoundingWish.Torn;
        }

        /// <summary>The reasons behind a wish, as the keys of their words (CC_Found_Why_*), the strongest first.</summary>
        public static List<string> Reasons(FoundingTies t)
        {
            var list = new List<(string key, float weight)>();
            if (t.kinGoing > 0) list.Add(("KinGoing", KinPull * t.kinGoing));
            if (t.kinStaying > 0) list.Add(("KinStaying", KinPull * t.kinStaying));
            if (t.atOdds) list.Add(("AtOdds", AtOddsPush));
            if (t.friendsGoing > 0) list.Add(("FriendsGoing", FriendPull * t.friendsGoing));
            if (t.friendsStaying > 0) list.Add(("FriendsStaying", FriendPull * t.friendsStaying));
            if (t.rivalsGoing > 0) list.Add(("RivalsGoing", RivalPush * t.rivalsGoing));
            if (t.rivalsStaying > 0) list.Add(("RivalsStaying", RivalPush * t.rivalsStaying));
            if (t.office) list.Add(("Office", OfficeHold));
            if (t.ownRoom) list.Add(("OwnRoom", RoomHold));
            if (t.elder) list.Add(("Elder", ElderHold));
            if (t.young) list.Add(("Young", YoungPull));
            list.Sort((a, b) => b.weight.CompareTo(a.weight));
            var keys = new List<string>();
            foreach (var r in list) keys.Add(r.key);
            return keys;
        }

        /// <summary>
        /// Sent or kept against their wish: one who wanted to stay and was sent, or wanted to go and was kept. They feel
        /// uprooted for a season; the torn go either way.
        /// </summary>
        public static bool AgainstWish(FoundingWish wish, bool going) => wish == FoundingWish.Stay && going || wish == FoundingWish.Go && !going;

        /// <summary>A season of feeling uprooted, and of being parted from someone close (the moods' duration).</summary>
        public const int UprootedDays = 15, PartedDays = 15;

        /// <summary>
        /// Uprooted, with the old colony kept, may ask once to move to the other colony: no sooner than this many days after
        /// the founding, and not once the season is over.
        /// </summary>
        public const int AskMoveFromDays = 3, AskMoveToDays = 15;

        /// <summary>Whether someone's question about moving is due now (once, between the days above).</summary>
        public static bool AskMoveDue(int now, int founded, bool asked) =>
            !asked && founded >= 0 && now - founded >= AskMoveFromDays * DayTicks && now - founded < AskMoveToDays * DayTicks;

        /// <summary>The question was never asked within its season: let go.</summary>
        public static bool AskMoveLapsed(int now, int founded) => founded < 0 || now - founded >= AskMoveToDays * DayTicks;

        /// <summary>
        /// A close bond split between the two colonies, felt as "parted" for a season: a spouse, lover or betrothed, a
        /// parent or child, a sibling, or a close friend (a good opinion of <see cref="CloseOpinion"/> or more).
        /// </summary>
        public static bool Close(bool partner, bool parentOrChild, bool sibling, int opinion) =>
            partner || parentOrChild || sibling || opinion >= CloseOpinion;

        public const int CloseOpinion = 60;

        /// <summary>The most "parted" moods one colonist gets at a founding (the closest first).</summary>
        public const int MaxParted = 3;

        // ---------------- Who may be sent, and the party ----------------

        /// <summary>
        /// Why someone can't be in the founding party, as the key of its words (CC_Found_Cant_*), or null when they can:
        /// not one of this home's free people, a lodger of another people, leaving (a banishment under way), already on
        /// another journey's plan, or away from the home now (on a caravan, in pods).
        /// </summary>
        public static string CantSend(bool ours, bool lodger, bool leaving, bool otherJourney, bool atHome)
        {
            if (!ours) return "NotOurs";
            if (lodger) return "Lodger";
            if (leaving) return "Leaving";
            if (otherJourney) return "OtherJourney";
            if (!atHome) return "Away";
            return null;
        }

        /// <summary>
        /// What keeps the founding from going ahead, as the key of its words (CC_Found_No_*), or null when it can: nobody
        /// goes and no newcomers; a party of children only; the old colony kept with no grown colonist left in it; a
        /// party with no means but newcomers; the newcomers can't come (not this year with the old colony kept).
        /// </summary>
        public static string PartyCheck(int grownGoing, int childrenGoing, int grownStaying, bool keepOld, bool newcomers, bool newcomersMayCome, FoundingMeans means)
        {
            int going = grownGoing + childrenGoing;
            if (going == 0 && !newcomers) return "Nobody";
            if (newcomers && !newcomersMayCome) return "NewcomersNotYet";
            if (going > 0 && grownGoing == 0) return "ChildrenOnly";
            if (going > 0 && means == FoundingMeans.Newcomers) return "NoMeans";
            if (keepOld && grownStaying == 0) return "NobodyStays";
            return null;
        }

        /// <summary>The means a founding uses: newcomers only when nobody goes from the colony, else the one chosen.</summary>
        public static FoundingMeans MeansOf(int going, FoundingMeans chosen) => going == 0 ? FoundingMeans.Newcomers : chosen == FoundingMeans.Newcomers ? FoundingMeans.Caravan : chosen;

        /// <summary>A home founded this near the chosen place (in tiles) is the colony the founding made: the party may have stopped a tile short.</summary>
        public const float NearTiles = 2.5f;

        public static bool AtPlace(float tiles) => tiles >= 0f && tiles <= NearTiles;

        // ---------------- The newcomers' price (decision 4A) ----------------

        /// <summary>A newcomer's passage is worth at least this much, whatever little they bring.</summary>
        public const int MinPassage = 150;

        /// <summary>At most half the passage is paid in food (by its market value), the rest in silver.</summary>
        public const float FoodShare = 0.5f;

        public const int MinNewcomers = 1, MaxNewcomers = 6;

        /// <summary>Free when the old colony is abandoned in the same act (a true fresh start, as the game's new wanderers are free).</summary>
        public static bool Free(bool abandoning) => abandoning;

        /// <summary>With the old colony kept, newcomers come at most once a year (<paramref name="lastPaid"/>: when they last came so, or -1).</summary>
        public static bool NewcomersMayCome(bool abandoning, int now, int lastPaid) => abandoning || lastPaid < 0 || now - lastPaid >= YearTicks;

        /// <summary>When they may next come with the old colony kept (-1: now).</summary>
        public static int NewcomersNext(int now, int lastPaid) => lastPaid < 0 || now - lastPaid >= YearTicks ? -1 : lastPaid + YearTicks;

        /// <summary>One newcomer's passage: about the worth of their kit, at least <see cref="MinPassage"/>.</summary>
        public static int Passage(float kitValue) => Math.Max(MinPassage, (int)Math.Round(kitValue / 10f) * 10);

        /// <summary>The passage of all of them, from each one's kit's worth; nothing when free.</summary>
        public static int Passage(IEnumerable<float> kitValues, bool free)
        {
            if (free || kitValues == null) return 0;
            int sum = 0;
            foreach (var v in kitValues) sum += Passage(v);
            return sum;
        }

        /// <summary>The passage before the newcomers are chosen: the game's default of three, at the least passage each.</summary>
        public static int PassageNamed(int count, bool free) => free ? 0 : Math.Max(MinNewcomers, count) * MinPassage;

        /// <summary>
        /// How a passage is paid from the stores: up to <see cref="FoodShare"/> of it in food (by market value, as much as
        /// there is), the rest in silver. False when the stores can't cover it (then nothing is paid).
        /// </summary>
        public static bool Pay(int price, int silverHeld, float foodValueHeld, out int silver, out float food)
        {
            silver = 0;
            food = 0f;
            if (price <= 0) return true;
            food = Math.Min(Math.Max(0f, foodValueHeld), price * FoodShare);
            silver = (int)Math.Ceiling(price - food);
            if (silver > silverHeld)
            {
                // More food in place of the silver that isn't there, if there is food enough.
                float more = Math.Min(Math.Max(0f, foodValueHeld) - food, silver - silverHeld);
                if (more > 0f)
                {
                    food += more;
                    silver = (int)Math.Ceiling(price - food);
                }
            }
            if (silver <= silverHeld) return true;
            silver = 0;
            food = 0f;
            return false;
        }

        // ---------------- The seat (decision 1A) ----------------

        /// <summary>
        /// Why a home can't be made the seat now, as the key of its words (CC_Seat_No_*), or null when it can: one colony
        /// only, already the seat, a home in orbit (nobody walks in), or the seat moved less than a season ago.
        /// </summary>
        public static string SeatWhyNot(int homes, bool isSeat, bool ground, int now, int movedAt)
        {
            if (homes < 2) return "OneColony";
            if (isSeat) return "Already";
            if (!ground) return "Orbit";
            if (movedAt >= 0 && now - movedAt < SeasonTicks) return "ThisSeason";
            return null;
        }

        /// <summary>When the seat may move again (-1: now).</summary>
        public static int SeatNext(int now, int movedAt) => movedAt < 0 || now - movedAt >= SeasonTicks ? -1 : movedAt + SeasonTicks;

        /// <summary>The old seat's people trust the council this much less (trust, not a mood).</summary>
        public const float SeatTrust = -3f;

        // ---------------- The game's colony limit (decision 5B) ----------------

        /// <summary>The game's own most: its Options let the player choose 1 to 5.</summary>
        public const int GameMax = 5;

        /// <summary>
        /// The game's colony limit for a founding, from the player's setting (<paramref name="pref"/>) and the homes the
        /// game counts now (<paramref name="counted"/>): as it is when there's room; one more when the new colony would
        /// pass it, never past the game's five; -1 when one more isn't enough (the setting is lower than the homes there
        /// are) or the game's five are reached.
        /// </summary>
        public static int LimitFor(int pref, int counted)
        {
            if (counted < pref) return pref;
            if (counted == pref && pref < GameMax) return pref + 1;
            return -1;
        }

        /// <summary>
        /// The setting once a founding that raised it is over with the old colony abandoned: back down by one, while it is
        /// still what Rimfolk set and the homes left fit under it; else as it is (the player changed it, or it's needed).
        /// </summary>
        public static int LimitBack(int pref, int raisedTo, int counted) => raisedTo > 0 && pref == raisedTo && counted <= pref - 1 ? pref - 1 : pref;

        // ---------------- What they bring (the founding kit) ----------------

        /// <summary>Food for fifteen days besides the road (the planner's own packing covers the road and its margin).</summary>
        public const int KitFoodDays = 15;

        /// <summary>
        /// What raises the first shelter by the colony's means: wood for a tribe or a medieval colony; steel and components
        /// for industry and beyond. Each kind's count for a party of <paramref name="people"/>, by the age (0 tribal, 1
        /// medieval, 2 industrial, 3 spacer): wood, steel, components.
        /// </summary>
        public static (int wood, int steel, int components) Shelter(int age, int people)
        {
            int n = Math.Max(1, people);
            if (age <= 1) return (150 + 25 * n, 0, 0);
            return (60, 100 + 25 * n, 4 + n);
        }

        /// <summary>Medicine for the first season: one more for every two of the party (beside the planner's for the road).</summary>
        public static int KitMedicine(int people) => (Math.Max(1, people) + 1) / 2;

        // ---------------- The colonies' record ----------------

        /// <summary>The most colonies the ledger keeps a record of; past it the oldest ended ones fold into the history.</summary>
        public const int MaxRecords = 24;

        /// <summary>
        /// Which record goes when there are too many: the oldest ended one (by when it ended), or -1 when there's room or
        /// every one still stands. <paramref name="ended"/>: when each ended, or -1 while it stands.
        /// </summary>
        public static int RecordToFold(IReadOnlyList<int> ended)
        {
            if (ended == null || ended.Count <= MaxRecords) return -1;
            int best = -1;
            for (int i = 0; i < ended.Count; i++)
                if (ended[i] >= 0 && (best < 0 || ended[i] < ended[best]))
                    best = i;
            return best;
        }

        /// <summary>
        /// The old colony abandoned in the same act as a fresh start: its council goes with its people to the new home
        /// (7a's rule, as a gravship landing carries it), in place of the new home's own, made since the founding. True
        /// when it was carried.
        /// </summary>
        public static bool CarryCouncil<T>(List<T> book, int from, int to) where T : class, IColonyEntry
        {
            if (book == null || from == to || from == HomesMath.None || to == HomesMath.None) return false;
            T old = null;
            for (int i = 0; i < book.Count; i++)
                if (book[i] != null && book[i].Home == from)
                    old = book[i];
            if (old == null) return false;
            book.RemoveAll(e => e != null && e != old && e.Home == to);
            old.Home = to;
            old.LostAt = ColonyStateMath.NotLost;
            return true;
        }

        // ---------------- History kinds ----------------

        /// <summary>The history's family for the colonies (World/Ledger/HistoryMath.cs): every kind below is a milestone.</summary>
        public const string Family = "colony";

        public const string KindFounded = "colony:founded";
        public const string KindSeat = "colony:seat";
        public const string KindAbandoned = "colony:abandoned";
        /// <summary>Release 2 and 4: a colony handed over, merged, taken back, and a former colonist home again.</summary>
        public const string KindHandedOver = "colony:handedOver", KindMerged = "colony:merged", KindTakenBack = "colony:takenBack", KindHomecoming = "colony:homecoming";
    }
}
