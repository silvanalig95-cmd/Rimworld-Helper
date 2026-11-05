using System;

namespace ColonyCouncil
{
    /// <summary>How a match came about: from courting (the two are lovers), the council seeking one, or their offer.</summary>
    public enum MatchWay
    {
        /// <summary>A colonist and one of theirs who courted their way to lovers: "Speak for a match" turns it into an alliance.</summary>
        Courted,
        /// <summary>The council sought it: a letter to their notable, or face to face with one of theirs.</summary>
        Sought,
        /// <summary>They offered it, by their letter.</summary>
        Offered
    }

    /// <summary>Where the married couple lives, chosen in each match (decision 8): either side may move.</summary>
    public enum MatchHome
    {
        /// <summary>Theirs comes to live at the colony and joins it on arriving; the wedding is at the colony, their kin as guests.</summary>
        Colony,
        /// <summary>Ours goes to live in their town and leaves the colony for good; their kin come for them, and the wedding is at their town.</summary>
        Theirs
    }

    /// <summary>Where a match stands, step by step (never a time left).</summary>
    public enum MatchStage
    {
        /// <summary>They said yes, and the colonist is against it: their objection waits for the council's word.</summary>
        Asking,
        /// <summary>Agreed: the two are engaged; their kin are on the road to the colony, or coming for the colony's own.</summary>
        Agreed,
        /// <summary>Their kin are at the colony's gate: the wedding there, or the colony's own walking out to them.</summary>
        Here,
        /// <summary>The colony's own is on the road to their town with their kin, the wedding there to come.</summary>
        Away,
        /// <summary>Married: the alliance is a tie of the ledger, and the plan is let go.</summary>
        Wed,
        /// <summary>Called off (by either side, a death, a war, the council).</summary>
        Off
    }

    /// <summary>How a colonist feels about a match: glad of it, willing, or against it (the council's question).</summary>
    public enum Consent
    {
        Glad,
        Willing,
        Unwilling
    }

    /// <summary>Why a colonist is against a match, in a word's key (the council's question names it).</summary>
    public enum ConsentWhy
    {
        None,
        /// <summary>They love someone else, here.</summary>
        Another,
        /// <summary>They would leave their children behind.</summary>
        Children,
        /// <summary>They can't stand the one chosen.</summary>
        Dislike
    }

    /// <summary>How the colony wronged a spouse from among them (design: killed by a colonist, executed, imprisoned, sold, banished).</summary>
    public enum MatchWrong
    {
        None,
        Killed,
        Executed,
        Imprisoned,
        Enslaved,
        Sold,
        Banished
    }

    /// <summary>
    /// The arithmetic of marriage alliances (docs/design/stage2-diplomacy.md, round b, item 6; release 2b.2; the player's
    /// decisions 8 and 9), with no game types so Tools/Tests can check it. A match between one of the colony's and one of
    /// theirs (a notable, or a member of a notable's house made for it): who may marry by the game's own answers (of age,
    /// drawn to each other's sex both ways, not kin, the faith's rule on spouses, not engaged elsewhere, and the Empire's
    /// titled nobles only to the titled); the colonist's consent by their own feelings (glad, willing, or against it, and
    /// why); their answer to the colony's proposal by their head's regard; their own offer once a year at most at a regard of
    /// 40; where the couple lives, chosen in each match; the wedding day by the road; gifts by their ways both ways at the
    /// gate. While it holds their notables think 15 better of the colony, their kin send a small gift once a season, they
    /// write when a raiders' camp is set against the colony's home, and the alliance stands as a defence pact (one, not two,
    /// and never with the Empire). It ends at a spouse's death with no child of the two living; it is broken when the
    /// colony wrongs the spouse, with a grudge their house's next head takes up; it ends when the couple part.
    /// </summary>
    public static class MatchMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        /// <summary>The origin of the ledger's ties made here (the Marriage tie, the grudge of a spouse wronged).</summary>
        public const string Origin = "match";

        /// <summary>The key of a Marriage tie: "spouse:" and the ref of their spouse (as Diplomatic Marriage+'s view writes it).</summary>
        public const string SpousePrefix = "spouse:";

        /// <summary>The key of a grudge for a spouse wronged.</summary>
        public const string GrudgeKey = "spouse";

        /// <summary>Where they live, kept in the tie's terms (goods): at the colony, or at their town.</summary>
        public const string AtColony = "colony", AtTheirs = "theirs";

        public static string SpouseKey(string spouseRef) => SpousePrefix + (spouseRef ?? "");

        public static bool ParseSpouse(string key, out string spouseRef)
        {
            spouseRef = null;
            if (key == null || !key.StartsWith(SpousePrefix, StringComparison.Ordinal) || key.Length == SpousePrefix.Length) return false;
            spouseRef = key.Substring(SpousePrefix.Length);
            return true;
        }

        public static string LivesKey(MatchHome h) => h == MatchHome.Colony ? AtColony : AtTheirs;

        public static MatchHome LivesOf(string key) => key == AtTheirs ? MatchHome.Theirs : MatchHome.Colony;

        // ---------------- Who may marry ----------------

        /// <summary>Grown: the game's own adulthood, and 18 or more.</summary>
        public const float MinAge = 18f;

        public static bool OfAge(float years, bool adult) => adult && years >= MinAge;

        /// <summary>
        /// The pair may be matched, by the game's answers: both of age, each drawn to the other's sex (the game's
        /// orientation), not kin (the game's incest rule), each free to take one more spouse (unmarried, or their faith
        /// allows more: the game's spouse-count rule), neither engaged already, and the Empire's titled nobles only to a
        /// titled colonist (decision 9). Returns the reason's key (CC_Match_Why_ and it), or null.
        /// </summary>
        public static string WhyNot(bool oursOfAge, bool theirsOfAge, bool oursDrawn, bool theirsDrawn, bool kin, bool oursFree, bool theirsFree, bool engaged,
            bool empire, bool theirsTitled, bool oursTitled)
        {
            if (!oursOfAge || !theirsOfAge) return "Young";
            if (!oursDrawn || !theirsDrawn) return "NotDrawn";
            if (kin) return "Kin";
            if (!oursFree || !theirsFree) return "Married";
            if (engaged) return "Engaged";
            if (!EmpireFits(empire, theirsTitled, oursTitled)) return "Titled";
            return null;
        }

        /// <summary>The Empire's titled nobles marry only titled colonists (decision 9); anyone else, anyone.</summary>
        public static bool EmpireFits(bool empire, bool theirsTitled, bool oursTitled) => !empire || !theirsTitled || oursTitled;

        // ---------------- The colonist asked first ----------------

        /// <summary>Glad of it from this regard of the one chosen; against it below the other.</summary>
        public const int GladOpinion = 30, AgainstOpinion = -10;

        /// <summary>
        /// How a colonist feels about a match, by their own feelings: against it while they love someone else here, or would
        /// leave their children behind to go to them, or can't stand the one chosen; glad of it with a lover, or one they
        /// think well of; else willing.
        /// </summary>
        public static Consent ConsentOf(bool lovers, int opinion, bool another, bool leavesChildren, out ConsentWhy why)
        {
            why = ConsentWhy.None;
            if (another)
            {
                why = ConsentWhy.Another;
                return Consent.Unwilling;
            }
            if (leavesChildren)
            {
                why = ConsentWhy.Children;
                return Consent.Unwilling;
            }
            if (lovers || opinion >= GladOpinion) return Consent.Glad;
            if (opinion <= AgainstOpinion)
            {
                why = ConsentWhy.Dislike;
                return Consent.Unwilling;
            }
            return Consent.Willing;
        }

        // ---------------- Their answer ----------------

        /// <summary>Below this regard of the colony they won't hear of a match.</summary>
        public const int MinRegard = 0;

        /// <summary>
        /// The chance they agree to the colony's proposal: by their head's regard of the colony and their people's goodwill,
        /// much more for two who are lovers already, a little less when one of theirs would leave them. None below regard 0;
        /// a tenth at worst above it, never certain.
        /// </summary>
        public static float AcceptChance(int regard, int goodwill, bool lovers, bool theirsMoves)
        {
            if (regard < MinRegard) return 0f;
            float c = 0.3f + Clamp(regard, 0, 100) * 0.006f + (lovers ? 0.35f : 0f) + (goodwill >= 75 ? 0.1f : 0f) - (theirsMoves ? 0.05f : 0f);
            return Clamp(c, 0.1f, 0.95f);
        }

        public static bool Accepts(int regard, int goodwill, bool lovers, bool theirsMoves, float roll) =>
            Clamp01(roll) < AcceptChance(regard, goodwill, lovers, theirsMoves);

        // ---------------- Their offer ----------------

        /// <summary>They offer one when their head thinks this well of the colony or better.</summary>
        public const int OfferRegard = 40;

        /// <summary>Once a year at most per people.</summary>
        public const float OfferGapDays = 60f;

        /// <summary>The day's chance that a people who may offer does (a mean of a season once they may).</summary>
        public const float OfferDaily = 1f / 15f;

        /// <summary>Three marriages with a people at most (the ledger's bound of a kind between two).</summary>
        public const int MaxPerPeople = 3;

        /// <summary>Matches under way at a home at once.</summary>
        public const int MaxPlans = 3;

        public static bool MayOffer(int regard, bool hostile, int lastOffer, int now, int live) =>
            !hostile && regard >= OfferRegard && live < MaxPerPeople && (lastOffer < 0 || now - lastOffer >= (long)(OfferGapDays * DayTicks));

        /// <summary>Where they would have the couple live: one of theirs who is to lead them stays home; else either, by a roll.</summary>
        public static MatchHome TheyPropose(bool theirsHeir, float roll) => theirsHeir || Clamp01(roll) < 0.5f ? MatchHome.Theirs : MatchHome.Colony;

        // ---------------- The wedding day ----------------

        /// <summary>
        /// Days from the agreement to the day their kin can be at the colony (or come for the colony's own): their road and
        /// a day to make ready, a day and a half at least.
        /// </summary>
        public static float LeadDays(float travelDays) => Math.Max(1.5f, Math.Max(0f, travelDays) + 1f);

        /// <summary>Their kin's walk-in hours on the day (by daylight).</summary>
        public const int ArriveFrom = 8, ArriveTo = 15;

        /// <summary>The game holds a wedding ceremony up to this hour.</summary>
        public const int CeremonyFrom = 5, CeremonyTo = 16;

        /// <summary>
        /// The game's ceremony may be begun this hour: in the game's hours, and with the wedding's guests at the colony only
        /// before the evening's feast is due (FeastMath.FeastFrom). The ceremony (its party before the vows and after them)
        /// and the feast each let no other gathering begin while they last, so begun in the same hour one waits on the other
        /// until it falls through.
        /// </summary>
        public static bool CeremonyHour(int hour, bool guestsHere) => hour >= CeremonyFrom && hour <= CeremonyTo && !(guestsHere && hour >= FeastMath.FeastFrom);

        /// <summary>Their kin coming for the colony's own walk in now: the day has come, in their hours, and it is safe.</summary>
        public static bool ArriveNow(int now, int due, int hour, bool danger) => now >= due && hour >= ArriveFrom && hour < ArriveTo && !danger;

        /// <summary>Their kin wait at the gate this long for the colony's own to walk out to them.</summary>
        public const float EscortWaitHours = 10f;

        /// <summary>Days past the day before an agreed match with nobody coming (or nobody sent) is let go.</summary>
        public const float GiveUpDays = 8f;

        /// <summary>The road to their town for the colony's own and their kin (by their means), a day at least: the wedding there after.</summary>
        public static float RoadDays(float travelDays) => Math.Max(1f, travelDays);

        // ---------------- Gifts by their ways ----------------

        /// <summary>
        /// The gifts carried both ways at the gate when a match is agreed, each worth about this much silver: 100 at a poor
        /// town, up to 250 at a flourishing one (fortune 0 to 100).
        /// </summary>
        public static int GiftValue(float fortune) => (int)Math.Round(100f + Clamp(fortune, 0f, 100f) * 1.5f);

        /// <summary>How many of a thing make up a gift of this worth (at least one).</summary>
        public static int GiftCount(float value, float unitValue) => unitValue <= 0f ? 0 : Math.Max(1, (int)Math.Round(value / unitValue));

        // ---------------- While it holds ----------------

        /// <summary>Their notables think this much better of the colony while a marriage of Rimfolk's with them holds (never Diplomatic Marriage+'s).</summary>
        public const int RegardBonus = 15;

        /// <summary>Their kin send a small gift once a season.</summary>
        public const float KinGiftDays = 15f;

        public static bool KinGiftDue(float lastDay, float sinceDay, float day) => day - (lastDay >= 0f ? lastDay : sinceDay) >= KinGiftDays;

        /// <summary>The kin's small gift: about a third of a match's.</summary>
        public static int KinGiftValue(float fortune) => GiftValue(fortune) / 3;

        /// <summary>A raiders' camp set against the colony's home, hidden this many days or more: their letter warns of it, this chance a day.</summary>
        public const float WarnAfterDays = 2f, WarnDaily = 0.25f;

        public static bool Warns(float campDays, float roll) => campDays >= WarnAfterDays && Clamp01(roll) < WarnDaily;

        /// <summary>The alliance makes its defence pact again a year after the last pact with them closed.</summary>
        public const float PactAgainDays = 60f;

        public static bool PactAgain(bool empire, bool anyLive, float daysSinceClosed) => !empire && !anyLive && daysSinceClosed >= PactAgainDays;

        /// <summary>The alliance's own pact runs on by itself while the marriage holds: its year moved on this close to its end.</summary>
        public const float PactRenewWithinDays = 5f;

        // ---------------- Its ends, and the bad sides ----------------

        /// <summary>A spouse's death ends it, unless a child of the two lives: then it holds through the child.</summary>
        public static bool EndsAtDeath(bool childLiving) => !childLiving;

        /// <summary>
        /// What their notables think of a spouse's death at the colony: little if the colony sheltered and tended them, much if
        /// they died in the open or untended; nothing for a death away from the colony.
        /// </summary>
        public static int DiedRegard(bool atColony, bool inTheOpen, bool untended) => !atColony ? 0 : inTheOpen || untended ? -15 : -3;

        /// <summary>The couple parted (a divorce, a breakup): their regard falls this much.</summary>
        public const int PartedRegard = -10;

        /// <summary>The match called off before the wedding by the colony (or its own not sent): their regard falls, and the colony's word is broken.</summary>
        public const int CalledOffRegard = -12;

        /// <summary>A match they turned down, or the colony turned down theirs: a little.</summary>
        public const int DeclinedRegard = -3;

        /// <summary>A spouse wronged: their notables' regard, on top of the memory's deed.</summary>
        public const int WrongedRegard = -30;

        /// <summary>The colony wronged a spouse from among them (killed by its hand, executed, imprisoned, enslaved, sold, banished).</summary>
        public static bool Wronged(MatchWrong w) => w != MatchWrong.None;

        /// <summary>
        /// The worst of what became of a spouse at the colony's hands: killed (by an execution, else by a colonist's blow),
        /// held as a prisoner or a slave, sold (taken away by another people), or sent away (no people's, or not the colony's).
        /// </summary>
        public static MatchWrong WrongOf(bool killedByOurs, bool executed, bool prisonerOfColony, bool slaveOfColony, bool sold, bool banished)
        {
            if (executed) return MatchWrong.Executed;
            if (killedByOurs) return MatchWrong.Killed;
            if (slaveOfColony) return MatchWrong.Enslaved;
            if (prisonerOfColony) return MatchWrong.Imprisoned;
            if (sold) return MatchWrong.Sold;
            if (banished) return MatchWrong.Banished;
            return MatchWrong.None;
        }

        /// <summary>
        /// Sold away, as the daily look reads it: no longer the colony's but another people's (not their own, not held as a
        /// prisoner), and not a people at war with the colony. One carried off by raiders goes over to their captors in time
        /// (the game's own captivity): no deed of the colony's.
        /// </summary>
        public static bool SoldAway(bool anotherPeople, bool prisoner, bool atWarWithColony) => anotherPeople && !prisoner && !atWarWithColony;

        /// <summary>The grudge for a spouse wronged passes to their house's next head when the one who holds it is gone.</summary>
        public static bool PassesDown(bool holderGone, bool nextKnown) => holderGone && nextKnown;

        // ---------------- Codes ----------------

        /// <summary>
        /// A letter's topic: "match:OURS:THEIRS" (a colonist and one of theirs, by pawn id), or "match:OURS:h:NOTABLE:G" (one of
        /// a notable's house, made when they agree: G 1 male, 2 female).
        /// </summary>
        public const string TopicPrefix = "match:";

        public static string Topic(int ours, int theirs) => TopicPrefix + ours + ":" + theirs;

        public static string HouseTopic(int ours, int notable, int gender) => TopicPrefix + ours + ":h:" + notable + ":" + gender;

        public static bool ParseTopic(string key, out int ours, out int theirs, out int notable, out int gender)
        {
            ours = theirs = notable = gender = -1;
            if (key == null || !key.StartsWith(TopicPrefix, StringComparison.Ordinal)) return false;
            var parts = key.Substring(TopicPrefix.Length).Split(':');
            if (parts.Length == 2) return int.TryParse(parts[0], out ours) && int.TryParse(parts[1], out theirs);
            if (parts.Length == 4 && parts[1] == "h") return int.TryParse(parts[0], out ours) && int.TryParse(parts[2], out notable) && int.TryParse(parts[3], out gender);
            return false;
        }

        /// <summary>What is asked in the letter: where they live ("home:Colony", "home:Theirs").</summary>
        public const string AskPrefix = "home:";

        public static string AskCode(MatchHome h) => AskPrefix + h;

        public static bool ParseAsk(string code, out MatchHome h)
        {
            h = MatchHome.Colony;
            return code != null && code.StartsWith(AskPrefix, StringComparison.Ordinal) && Enum.TryParse(code.Substring(AskPrefix.Length), out h);
        }

        /// <summary>The plan's tag on the feast that hosts the wedding's guests (Feasts.Host): "match:ID".</summary>
        public static string FeastTag(int plan) => TopicPrefix + plan;

        public static bool ParseFeastTag(string tag, out int plan)
        {
            plan = -1;
            return tag != null && tag.StartsWith(TopicPrefix, StringComparison.Ordinal) && tag.IndexOf(':', TopicPrefix.Length) < 0 && int.TryParse(tag.Substring(TopicPrefix.Length), out plan);
        }

        // ---------------- The facts ----------------

        /// <summary>
        /// The facts: a marriage holds between the colony and their people (tie:marriage), one of the colony's living among
        /// them (tie:marriage:ours) or one of theirs at the colony (tie:marriage:theirs), the one spoken to is the spouse or
        /// their kin (tie:marriage:you); a match agreed and the wedding to come (match:agreed); held through a child after a
        /// death (match:child); a spouse wronged by the colony (match:wronged); {INLAW} their spouse, {SPOUSEOF} the colony's.
        /// </summary>
        public const string Tag = "tie:marriage", OursTag = "tie:marriage:ours", TheirsTag = "tie:marriage:theirs", YouTag = "tie:marriage:you", AgreedTag = "match:agreed",
            ChildTag = "match:child", WrongedTag = "match:wronged", InlawKey = "INLAW", SpouseOfKey = "SPOUSEOF";

        public static readonly string[] Tags = { Tag, OursTag, TheirsTag, YouTag, AgreedTag, ChildTag, WrongedTag };

        /// <summary>Talked of this long after a wedding or a wrong (days).</summary>
        public const float TalkDays = 30f;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
