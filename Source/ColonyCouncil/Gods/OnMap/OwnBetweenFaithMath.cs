using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a scoffer dares a believer's god to do by morning (OwnHappening.FaithDare).</summary>
    public enum GodDare
    {
        /// <summary>Rain by morning, in a dry spell.</summary>
        Rain,
        /// <summary>The sick one up and about by morning.</summary>
        Sick,
        /// <summary>The light at the god's place still burning at dawn, with nobody seeing to it.</summary>
        Light
    }

    /// <summary>Who comes to the children's little place (OwnHappening.FaithLittleShrine).</summary>
    public enum ShrineVisitor
    {
        None,
        /// <summary>A grown-up who holds the god helps them set a flat stone on top.</summary>
        Helper,
        /// <summary>The keeper of the colony's book tells them the god's name as the book has it.</summary>
        Keeper,
        /// <summary>A scoffer kicks it over.</summary>
        Scoffer
    }

    /// <summary>What a machine was that the colony holds a wake for (OwnHappening.PipWake).</summary>
    public enum WakeOf
    {
        /// <summary>A mechanitor's mech of a year or more.</summary>
        Mech,
        /// <summary>The colony's oldest turret.</summary>
        Turret,
        /// <summary>The colony's oldest generator.</summary>
        Generator,
        /// <summary>The colony's oldest workbench or loom (the craft's, for a colony of the early ages).</summary>
        Bench,
        /// <summary>The colony's oldest vehicle (Vanilla Vehicles Expanded's, by name, softly).</summary>
        Vehicle
    }

    /// <summary>What the guests at a wedding take for a sign (OwnHappening.FaithWeddingSign).</summary>
    public enum WeddingSign
    {
        /// <summary>Rain begins on the couple.</summary>
        Rain,
        /// <summary>A light by the couple goes out.</summary>
        LightOut,
        /// <summary>The god's own small sign where they stand.</summary>
        GodSign
    }

    /// <summary>What run of luck others noticed on someone (OwnHappening.FaithBargain).</summary>
    public enum LuckOf
    {
        None,
        /// <summary>They made the Hunger's bargain (GodVoid), and it paid them.</summary>
        Bargained,
        /// <summary>The beasts keep away from them (after a bargain: UndertowShun).</summary>
        Shunned,
        /// <summary>The great cat's chosen, with a lucky season while she stays (SacredAnimal.Lucky): no bargain at all.</summary>
        Lucky
    }

    /// <summary>
    /// Round 7 of the gods between the colony's people (docs/content-update-proposals-2.md, section 1, D1 to D15; the player's
    /// go, 27 November 2026: "just more content to the already existing mechanics"): fourteen more happenings in the frame of
    /// 0.281.0 (Gods/OnMap/Between/), most of them of whichever god their people hold. What calls each and how it ends between
    /// the two are pure here, so Tools/Tests reads the same rules.
    /// </summary>
    public static partial class OwnHappeningsMath
    {
        /// <summary>A happening between the colony's people of whichever god its people hold (named Faith...), not of one god by name.</summary>
        public static bool OfAnyGod(OwnHappening h) => IsBetween(h) && GodOf(h) == null;

        // ================= D1: the dare =================

        /// <summary>The evening's hours the dare is made in, and the morning's the two walk out to look in.</summary>
        public const int DareFrom = 17, DareTo = 22, DareMorningFrom = 6, DareMorningTo = 11;

        /// <summary>A dry spell for a dare of rain: this many dry days before today.</summary>
        public const int DareDryDays = 3;

        /// <summary>What can be dared now, each only where it is settled by the morning: rain in a dry spell, the sick in bed, a light at the god's place.</summary>
        public static List<GodDare> Dares(int dryDays, bool raining, bool sickInBed, bool lightAtPlace)
        {
            var list = new List<GodDare>();
            if (dryDays >= DareDryDays && !raining) list.Add(GodDare.Rain);
            if (sickInBed) list.Add(GodDare.Sick);
            if (lightAtPlace) list.Add(GodDare.Light);
            return list;
        }

        /// <summary>A believer and a scoffer together with something to dare; likelier when the believer was just seen at their small rite.</summary>
        public static GodPaceMath.Trigger DareTrigger(bool pair, int dares, bool atRite) =>
            !pair || dares <= 0 ? GodPaceMath.Trigger.None : atRite ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>Whatever came of it, the two may argue what it means instead, at this chance.</summary>
        public const float DareArgueChance = 0.3f;

        /// <summary>
        /// The morning's look: it came (the believer proud, the scoffer shaken a little: None here, the code gives each their
        /// own), it didn't (the scoffer pleased: a good sport takes it in good part, else it stings), or they argue over it.
        /// </summary>
        public static BetweenOutcome DareEnd(bool came, float believerOpinionOfScoffer, float roll)
        {
            if (!float.IsNaN(roll) && roll < DareArgueChance) return BetweenOutcome.Quarrel;
            return came ? BetweenOutcome.None : Laugh(believerOpinionOfScoffer);
        }

        // ================= D2: came through =================

        /// <summary>Blood lost (severity) from which one bled near to death, and an illness's share of its lethal severity past which it nearly took them.</summary>
        public const float BledFrom = 0.3f, IllShareFrom = 0.3f, OverItFrom = 0.85f;

        /// <summary>They came through a near death: bled near to it, or a deadly illness past its worst (immune enough now).</summary>
        public static bool NearDeath(float bloodLoss, float illnessShare, float immunity) =>
            bloodLoss >= BledFrom || illnessShare > IllShareFrom && immunity >= OverItFrom;

        /// <summary>Once on their feet, they walk to their god's place at this chance (the gap past).</summary>
        public const float CameChance = 0.5f;

        /// <summary>
        /// The doctor who tended them hears of it: one who holds the same god walks over and stands with them (closer); one who
        /// doubts or holds another says it was the stitches (a good sport laughs along, a sore one minds it); one of no strong
        /// belief says nothing.
        /// </summary>
        public static BetweenOutcome Doctor(BetweenStance doctor, float patientOpinionOfDoctor) =>
            doctor == BetweenStance.Holds ? BetweenOutcome.Closer : doctor == BetweenStance.Neither ? BetweenOutcome.None : Laugh(patientOpinionOfDoctor);

        // ================= D3: the believers take sides =================

        /// <summary>The supper's hours while two gods' quarrel goes on, and an hour's chance of the argument.</summary>
        public const int SupperFrom = 18, SupperTo = 21;
        public const float SidesChance = 0.25f;

        /// <summary>Sat at one table by the peacemaker: they make it up, unless either thinks too little of the other now (a lasting coolness).</summary>
        public const float CoolBelow = -25f;

        public static BetweenOutcome Sat(float opinionAB, float opinionBA) =>
            opinionAB >= CoolBelow && opinionBA >= CoolBelow ? BetweenOutcome.MadeUp : BetweenOutcome.None;

        // ================= D4: the guest at the burying =================

        /// <summary>A grave of one of the colony dead this lately (days), the afternoon's hours the stranger comes in, at this chance, and how long it stays.</summary>
        public const float BuryingDays = 2f, BuryingChance = 0.35f, BuryingHours = 4f;
        public const int BuryingFrom = 14, BuryingTo = 18;

        /// <summary>The stranger comes only with no other of the gods' strangers at the home, and a people at peace to come from.</summary>
        public static bool BuryingMay(bool freshGrave, bool strangerHere, bool peopleAtPeace) => freshGrave && !strangerHere && peopleAtPeace;

        // ================= D5: the children's little place =================

        /// <summary>Two children at least, one of them leaning toward a god; the day's hours they build in.</summary>
        public const int ShrineChildrenFrom = 2, ShrineFrom = 9, ShrineTo = 17;

        public static GodPaceMath.Trigger LittleShrineTrigger(int children, bool oneLeans) =>
            children >= ShrineChildrenFrom && oneLeans ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>How far out from the colony's middle the corner is (cells).</summary>
        public const float ShrineEdgeMin = 12f, ShrineEdgeMax = 26f;

        /// <summary>Who comes to the little place, of those who can: one who holds the god half the time, the keeper, else a scoffer.</summary>
        public static ShrineVisitor Visitor(bool helper, bool keeper, bool scoffer, float roll)
        {
            if (float.IsNaN(roll)) roll = 0f;
            float h = helper ? 0.5f : 0f, k = keeper ? 0.3f : 0f, s = scoffer ? 0.2f : 0f;
            float all = h + k + s;
            if (all <= 0f) return ShrineVisitor.None;
            float r = roll * all;
            return r < h ? ShrineVisitor.Helper : r < h + k ? ShrineVisitor.Keeper : ShrineVisitor.Scoffer;
        }

        /// <summary>Those fond of a child (opinion) think worse of the scoffer who kicked their place over.</summary>
        public const float FondFrom = 30f;

        // ================= D7: the one who turned =================

        /// <summary>A turning this lately (days) is walked out the next day, at this chance, in the day's hours.</summary>
        public const float TurnedDays = 2f, TurnedChance = 0.8f;
        public const int TurnedFrom = 8, TurnedTo = 18;

        /// <summary>One who holds the old god meets them: a friend (opinion) understands, others call it a betrayal.</summary>
        public const float TurnedFriendFrom = 20f;

        public static BetweenOutcome OldFaithful(float opinionOfTurner) => opinionOfTurner >= TurnedFriendFrom ? BetweenOutcome.Closer : BetweenOutcome.Quarrel;

        // ================= D8: a machine's wake =================

        /// <summary>A loss's chance of a wake, the dusk's hours, and how old a mech or the colony must be (years).</summary>
        public const float WakeChance = 0.6f, MechYearsFrom = 1f, ColonyYearsFrom = 1f;
        public const int WakeFrom = 17, WakeTo = 21;

        /// <summary>
        /// The machine served the colony long: a mech a year old or more; a turret, a generator or a bench the oldest of its
        /// kind at the home, the colony a year old. A bench only for a colony of the early ages (Pip's craft); none where the
        /// age has neither.
        /// </summary>
        public static bool Served(WakeOf what, float mechYears, bool oldestOfKind, float colonyYears, int age)
        {
            switch (what)
            {
                case WakeOf.Mech: return mechYears >= MechYearsFrom;
                case WakeOf.Bench: return age <= 1 && oldestOfKind && colonyYears >= ColonyYearsFrom;
                default: return oldestOfKind && colonyYears >= ColonyYearsFrom;
            }
        }

        // ================= D9: the offering nobody's god took =================

        /// <summary>The late hours before the gods' own small hours (WorshipMath.GodTakesNow: one to four), when the taker goes.</summary>
        public const int TakenFrom = 22, TakenTo = 24;

        /// <summary>Hungry (their food) below this.</summary>
        public const float HungryBelow = 0.3f;

        public static GodPaceMath.Trigger TakenTrigger(bool waiting, bool taker, bool witness) =>
            waiting && taker && witness ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The one who saw it tells, unless they like the taker this well and don't hold the god (then mostly they keep it).</summary>
        public const float KeepsFrom = 20f, TellsAnyway = 0.3f;

        public static bool Tells(float witnessOpinionOfTaker, bool witnessHoldsGod, float roll) =>
            witnessHoldsGod || witnessOpinionOfTaker < KeepsFrom || !float.IsNaN(roll) && roll < TellsAnyway;

        /// <summary>The morning's hours the one who saw it tells, after the believers have found the place empty.</summary>
        public const int TellFrom = 9, TellTo = 18;

        // ================= D10: the book read at the fire =================

        /// <summary>The evening's hours, the new entries since the last reading it takes, who sits to listen at most, and the verses read.</summary>
        public const int ReadFrom = 18, ReadTo = 22, ReadNewFrom = 2, ReadListeners = 3, ReadVerses = 3;

        public static GodPaceMath.Trigger ReadTrigger(int fresh, int listeners) =>
            fresh < ReadNewFrom || listeners < 1 ? GodPaceMath.Trigger.None : fresh >= ReadNewFrom * 3 ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        // ================= D11: a sign at the wedding =================

        /// <summary>A wedding's chance of a sign the guests take for one.</summary>
        public const float WeddingChance = 0.7f;

        /// <summary>What can come at the wedding now: rain on the couple out of doors (where it isn't raining and the land has rain), a light near them going out, and always the god's own small sign.</summary>
        public static List<WeddingSign> WeddingSigns(bool outdoors, bool raining, bool rainAllowed, bool light)
        {
            var list = new List<WeddingSign>();
            if (outdoors && !raining && rainAllowed) list.Add(WeddingSign.Rain);
            if (light) list.Add(WeddingSign.LightOut);
            list.Add(WeddingSign.GodSign);
            return list;
        }

        /// <summary>The first guest takes it for a blessing (the second for a warning), or the other way round: rain and the god's sign mostly a blessing, a light gone out mostly not.</summary>
        public static bool FirstBlesses(WeddingSign s, float roll) => !float.IsNaN(roll) && roll < (s == WeddingSign.LightOut ? 0.3f : 0.7f);

        // ================= D12: a word over the raider =================

        /// <summary>A raider dead this lately (days) is spoken over, at this chance, by one who holds a god their people hold.</summary>
        public const float RaiderDays = 1f, RaiderChance = 0.5f;

        /// <summary>The marshal, or one who lost a friend in the fight, answers: a nod from one who thinks well enough of the speaker (opinion), never from one who lost a friend.</summary>
        public const float RaiderNodFrom = 10f;

        public static BetweenOutcome OverRaider(float opinionOfSpeaker, bool lostFriend) =>
            lostFriend || opinionOfSpeaker < RaiderNodFrom ? BetweenOutcome.Quarrel : BetweenOutcome.Closer;

        /// <summary>A friend (opinion) of one of the colony who fell in the fight.</summary>
        public const float LostFriendFrom = 30f;

        // ================= D13: a gift at the other's place =================

        /// <summary>A quarrel over their gods between this many and this many days ago, and a look's chance of the gift.</summary>
        public const float GiftAfterDays = 2f, GiftWithinDays = 8f, GiftChance = 0.3f;

        /// <summary>The one who gives: a kind heart, or the one who thinks better of the other.</summary>
        public static bool Gives(bool kind, float opinionOfOther, float othersOpinion) => kind || opinionOfOther > othersOpinion;

        /// <summary>A zealous believer of the giver's own god (their hold) calls it disloyal.</summary>
        public const float ZealousFrom = 55f;

        // ================= D14: suspected of a bargain =================

        /// <summary>A run of luck someone could notice: a bargain made (strong), the beasts keeping away (strong), the great cat's chosen (ordinary).</summary>
        public static GodPaceMath.Trigger BargainTrigger(LuckOf luck) =>
            luck == LuckOf.Bargained || luck == LuckOf.Shunned ? GodPaceMath.Trigger.Strong : luck == LuckOf.Lucky ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The accuser thinks this little of them at most (opinion); a friend stands up for them from this.</summary>
        public const float AccuserBelow = 30f, DefenderFrom = 30f;

        /// <summary>Only one who did make a bargain may own it, at this chance; the rest deny it, true or not.</summary>
        public const float OwnsChance = 0.35f;

        public static bool Owns(LuckOf luck, float roll) => (luck == LuckOf.Bargained || luck == LuckOf.Shunned) && !float.IsNaN(roll) && roll < OwnsChance;

        /// <summary>Believers of the gods that turn from such bargains within this many cells keep away from them a while.</summary>
        public const float ShunWithin = 18f;

        // ================= D15: lightning on the shrine =================

        /// <summary>A place kept busy: something left or prayed there this lately (days).</summary>
        public const float BusyDays = 4f;

        /// <summary>A thunderstorm, a busy place, and someone at home who holds its god's rival.</summary>
        public static GodPaceMath.Trigger LightningTrigger(bool storm, bool busy, bool rivalHeld) =>
            storm && busy && rivalHeld ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>An hour's chance of the strike in such a storm, before the harm setting and the shared budget (HarmKinds.GodLightning).</summary>
        public const float LightningChance = 0.15f;

        /// <summary>The morning after: two of the place's own who agree rebuild it together (closer); one set against its god argues.</summary>
        public static BetweenOutcome Rebuild(BetweenStance a, BetweenStance b) => Against(a) || Against(b) ? BetweenOutcome.Quarrel : BetweenOutcome.Closer;
    }
}
