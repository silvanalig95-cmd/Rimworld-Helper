using System;

namespace ColonyCouncil
{
    /// <summary>How someone stands toward the god a happening is of, for who says what and how it ends between two people.</summary>
    public enum BetweenStance
    {
        /// <summary>They hold to this god (lean toward it at least, not in dread of it).</summary>
        Holds,
        /// <summary>They hold to another god, one of this god's rivals by the stories.</summary>
        Rival,
        /// <summary>They hold to another god, no rival of this one.</summary>
        Other,
        /// <summary>No strong belief either way.</summary>
        Neither,
        /// <summary>They believe none of it.</summary>
        Doubts
    }

    /// <summary>How a happening between two of the colony's people ends (Gods/OnMap/Between/Between.cs, Outcome).</summary>
    public enum BetweenOutcome
    {
        None,
        /// <summary>The two come away on better terms.</summary>
        Closer,
        /// <summary>The two argue over it and part on worse terms.</summary>
        Quarrel,
        /// <summary>Two who were at odds make it up.</summary>
        MadeUp,
        /// <summary>One is ashamed before the other.</summary>
        Ashamed,
        /// <summary>One resents the other for it.</summary>
        Jealous,
        /// <summary>One keeps away from the other after what they saw.</summary>
        Wary,
        /// <summary>One laughed at the other, and it stings.</summary>
        Laughed,
        /// <summary>One won't forget the other's kindness.</summary>
        Kind
    }

    /// <summary>
    /// The gods between the colony's people (round 6 of each god's own happenings; the player, 20 November 2026: "additional
    /// God events, more immersion ... how they're integrated, the effects they have, the relations between pawns, the kind
    /// of events that generated from it"): each god's happenings that set its people toward or against each other, two or
    /// more a god, each on the map (Gods/OnMap/Between/). Their pace is the gods' own (PaceOf), and what calls each and how
    /// it ends between the two are pure here, so Tools/Tests reads the same rules.
    /// </summary>
    public static partial class OwnHappeningsMath
    {
        /// <summary>The pace of the happenings between the colony's people (PaceOf's own for them; 0 mean: it follows what calls it, at its own chance).</summary>
        public static (float gap, float mean, float ramp) BetweenPaceOf(OwnHappening h)
        {
            switch (h)
            {
                case OwnHappening.HollinBound: return (20f, 0f, 0f);
                case OwnHappening.HollinStumps: return (30f, 12f, 20f);
                case OwnHappening.SmoulderPraise: return (15f, 0f, 0f);
                case OwnHappening.SmoulderNearMiss: return (30f, 20f, 30f);
                case OwnHappening.BellwetherTold: return (20f, 10f, 20f);
                case OwnHappening.BellwetherSpared: return (25f, 0f, 0f);
                case OwnHappening.MorelWasted: return (20f, 8f, 15f);
                case OwnHappening.MorelSealed: return (10f, 0f, 0f);
                case OwnHappening.EnnuWelcomed: return (3f, 0f, 0f);
                case OwnHappening.EnnuWaitedUp: return (15f, 8f, 15f);
                case OwnHappening.PipTwoHands: return (20f, 6f, 15f);
                case OwnHappening.PipTalked: return (20f, 12f, 20f);
                case OwnHappening.CirrusTwoWatchers: return (15f, 6f, 15f);
                case OwnHappening.CirrusCloudShape: return (20f, 10f, 20f);
                case OwnHappening.HushCoat: return (15f, 0f, 0f);
                case OwnHappening.HushNoise: return (20f, 0f, 0f);
                case OwnHappening.UndertowWhisper: return (25f, 8f, 20f);
                case OwnHappening.UndertowLampOut: return (25f, 10f, 20f);
                case OwnHappening.HeliosSun: return (6f, 3f, 8f);
                case OwnHappening.HeliosAlike: return (20f, 10f, 20f);
                case OwnHappening.HeliosShade: return (15f, 8f, 15f);
                case OwnHappening.SeleneSorry: return (8f, 0f, 0f);
                case OwnHappening.SeleneSameDream: return (12f, 8f, 15f);
                case OwnHappening.MogGraveCat: return (15f, 8f, 15f);
                case OwnHappening.MogRiddle: return (15f, 8f, 15f);
                case OwnHappening.MurmurSplash: return (15f, 8f, 15f);
                case OwnHappening.MurmurFrogIn: return (10f, 0f, 0f);
                case OwnHappening.DucatPassed: return (20f, 10f, 20f);
                case OwnHappening.DucatMiser: return (30f, 12f, 20f);
                case OwnHappening.RevelRivalsCup: return (15f, 8f, 15f);
                case OwnHappening.RevelWager: return (15f, 0f, 0f);
                // Round 7 (OwnBetweenFaithMath.cs).
                case OwnHappening.FaithDare: return (20f, 10f, 20f);
                case OwnHappening.FaithCameThrough: return (15f, 0f, 0f);
                case OwnHappening.FaithSides: return (10f, 0f, 0f);
                case OwnHappening.EnnuBurying: return (60f, 0f, 0f);
                case OwnHappening.FaithLittleShrine: return (30f, 12f, 20f);
                case OwnHappening.FaithTurned: return (8f, 0f, 0f);
                case OwnHappening.PipWake: return (30f, 0f, 0f);
                case OwnHappening.FaithTakenOffering: return (25f, 12f, 20f);
                case OwnHappening.FaithBookRead: return (12f, 4f, 10f);
                case OwnHappening.FaithWeddingSign: return (10f, 0f, 0f);
                case OwnHappening.FaithRaiderWord: return (15f, 0f, 0f);
                case OwnHappening.FaithGift: return (12f, 0f, 0f);
                case OwnHappening.FaithBargain: return (25f, 12f, 20f);
                case OwnHappening.FaithLightning: return (60f, 0f, 0f);
                default: return (30f, 0f, 0f);
            }
        }

        /// <summary>The first of the happenings between the colony's people in the shared list (added last, so a save's list keeps its order).</summary>
        public const OwnHappening FirstBetween = OwnHappening.HollinBound;

        /// <summary>A happening is one of those between the colony's people (the content round 3's on the land come after them).</summary>
        public static bool IsBetween(OwnHappening h) => h >= FirstBetween && !IsLand3(h);

        /// <summary>
        /// The chance one that follows something (a fight, a fine piece, a body sealed, a newcomer, a hunt, an insult) comes
        /// with it: none within its gap, its own chance otherwise, a little more the nearer its god is.
        /// </summary>
        public static float FollowChance(OwnHappening h, float daysSince, float chance, float near) =>
            !Free(h, daysSince) ? 0f : Clamp01Between(chance * GodNearMath.WorldFactor(near));

        // ================= Who stands where, and how it ends =================

        /// <summary>How someone stands: holding it (strong enough), holding a rival, holding another, of no strong belief, or doubting it all.</summary>
        public static BetweenStance Stance(bool holds, bool holdsRival, bool holdsOther, bool doubts) =>
            holds ? BetweenStance.Holds : doubts ? BetweenStance.Doubts : holdsRival ? BetweenStance.Rival : holdsOther ? BetweenStance.Other : BetweenStance.Neither;

        /// <summary>Two see a sign of the god together: both who hold it, or one who holds it and one who has no quarrel with it, come away closer; a rival's or a doubter's word against it is a quarrel.</summary>
        public static BetweenOutcome Shared(BetweenStance a, BetweenStance b)
        {
            if (Against(a) || Against(b)) return a == BetweenStance.Holds || b == BetweenStance.Holds ? BetweenOutcome.Quarrel : BetweenOutcome.None;
            return a == BetweenStance.Holds || b == BetweenStance.Holds ? BetweenOutcome.Closer : BetweenOutcome.None;
        }

        /// <summary>Set against the god: a doubter, or one who holds its rival.</summary>
        public static bool Against(BetweenStance s) => s == BetweenStance.Doubts || s == BetweenStance.Rival;

        /// <summary>Two at odds brought to one place by a sign: they make it up unless one of them scoffs at it, which only sours it.</summary>
        public static BetweenOutcome Mend(BetweenStance a, BetweenStance b) =>
            a == BetweenStance.Doubts || b == BetweenStance.Doubts ? BetweenOutcome.Quarrel : BetweenOutcome.MadeUp;

        /// <summary>A kindness done in the god's way: the one helped won't forget it, whatever either of them believes.</summary>
        public static BetweenOutcome Helped(bool came) => came ? BetweenOutcome.Kind : BetweenOutcome.None;

        /// <summary>Someone's own deed shown to them by a believer: ashamed if they hold the god or have no strong belief; a doubter or a rival's man argues instead.</summary>
        public static BetweenOutcome Shown(BetweenStance shown) => Against(shown) ? BetweenOutcome.Quarrel : BetweenOutcome.Ashamed;

        /// <summary>One laughs at another: a good sport laughs along (closer), else it stings.</summary>
        public static BetweenOutcome Laugh(float opinionOfLaugher) => opinionOfLaugher >= GoodSportFrom ? BetweenOutcome.Closer : BetweenOutcome.Laughed;

        /// <summary>From this opinion of the one who laughed, it is taken in good part.</summary>
        public const float GoodSportFrom = 20f;

        // ================= Hollin =================

        /// <summary>Two who fought (the game's angering fight) this lately (days) may find the saplings, at this chance.</summary>
        public const float BoundFightDays = 1.5f, BoundChance = 0.35f;

        /// <summary>A woodcutter who felled this many trees (strongly from the second) within this many days, while the colony planted few, finds the brambles.</summary>
        public const int StumpsFrom = 12, StumpsStrong = 25;
        public const float StumpsDays = 15f;

        public static GodPaceMath.Trigger StumpsTrigger(int felledByOne, int plantedLately)
        {
            if (felledByOne < StumpsFrom || plantedLately * 3 >= felledByOne) return GodPaceMath.Trigger.None;
            return felledByOne >= StumpsStrong ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>The woodcutters counted at most, and the brambles round a stump.</summary>
        public const int FellersKept = 8, StumpBrambles = 5;

        // ================= Smoulder =================

        /// <summary>The forge flares for a fine piece: often for a masterwork, nearly always for a legend (QualityCategory as an int: 5 masterwork, 6 legendary).</summary>
        public static float PraiseChance(int quality) => quality >= 6 ? 0.9f : quality == 5 ? 0.5f : 0f;

        /// <summary>A crafter whose skill is this close to the maker's is the one who resents it.</summary>
        public const int RivalCrafterWithin = 4;

        /// <summary>Two working under the mountain this close (cells): a few stones come down where one of them stood, and the other pulls them back.</summary>
        public const float NearMissWithin = 8f;

        public static GodPaceMath.Trigger NearMissTrigger(bool twoUnderRock, float deepStir) =>
            !twoUnderRock ? GodPaceMath.Trigger.None : deepStir >= 0.3f ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        // ================= Bellwether =================

        /// <summary>The old one who tells it (this age at least, else the eldest grown of the colony) and the hours of dusk.</summary>
        public const float ElderFrom = 50f;
        public const int ToldFrom = 17, ToldTo = 20;

        /// <summary>An old one and a child, and wild grazers on the land to come near: told; more readily after the white beast passed in peace.</summary>
        public static GodPaceMath.Trigger ToldTrigger(bool elderAndChild, int grazers, bool peaceLately) =>
            !elderAndChild || grazers <= 0 ? GodPaceMath.Trigger.None : peaceLately ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>A hunter lets the beast go only while the colony has this many days of food; a look's chance each hour of the hunt.</summary>
        public const float SparedFoodDays = 10f, SparedChance = 0.15f;

        public static bool MaySpare(float foodDays, bool hunterScoffs) => !hunterScoffs && foodDays >= SparedFoodDays;

        // ================= Morel =================

        /// <summary>One of the colony dead this lately (days) and sealed in a sarcophagus brings the threads on the stone, at this chance.</summary>
        public const float SealedDays = 10f, SealedChance = 0.6f;

        /// <summary>The threads lie round the stone this many hours.</summary>
        public const int SealedHours = 30;

        // ================= Ennu =================

        /// <summary>One who joined the colony within this many days is a newcomer to welcome, at this chance.</summary>
        public const float NewcomerDays = 1f, WelcomeChance = 0.6f;

        /// <summary>Someone of the colony out this far from home (cells) late at night is waited up for, between these hours.</summary>
        public const float FarCells = 50f;
        public const int WaitFrom = 21, WaitTo = 2;

        public static bool WaitHour(int hour) => hour >= WaitFrom || hour < WaitTo;

        /// <summary>The one who waits likes them at least this well.</summary>
        public const float WaiterOpinion = 15f;

        // ================= Pip =================

        /// <summary>A machine broken down this many hours with nobody at it calls two to it.</summary>
        public const float TwoHandsHours = 4f;

        public static GodPaceMath.Trigger TwoHandsTrigger(float brokenHours, bool someoneRepairing) =>
            someoneRepairing || brokenHours < TwoHandsHours ? GodPaceMath.Trigger.None : brokenHours >= TwoHandsHours * 3 ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>A doubter this near (cells) to one who talks to the machine.</summary>
        public const float TalkedWithin = 15f;

        // ================= Cirrus =================

        /// <summary>Two out under the sky this close (cells) at night watch a star cross together.</summary>
        public const float WatchersWithin = 12f;

        public static bool NightHour(int hour) => hour >= 21 || hour < 4;

        /// <summary>Two out in the open this close in the day stop to point at the clouds.</summary>
        public const float CloudWithin = 14f;

        // ================= Hush =================

        /// <summary>Someone this cold (hypothermia, any) with a warmer coat in the stores: a coat is carried to them, at this chance a look.</summary>
        public const float CoatChance = 0.25f;

        /// <summary>A coat warmer than what they wear by this much (cold insulation, degrees).</summary>
        public const float CoatWarmerBy = 10f;

        /// <summary>The hours of a winter night when the noise is heard, how near the Hush believer must be (cells), and a look's chance.</summary>
        public const float NoiseWithin = 25f, NoiseChance = 0.3f;

        public static bool NoiseHour(int hour) => hour >= 22 || hour < 4;

        // ================= Undertow =================

        /// <summary>A grudge this deep (opinion), or a mood this low, sends someone to whisper at the edge at night.</summary>
        public const float GrudgeBelow = -50f, DespairBelow = 0.25f;

        public static GodPaceMath.Trigger WhisperTrigger(float worstOpinion, float mood, bool night) =>
            !night ? GodPaceMath.Trigger.None : worstOpinion <= GrudgeBelow && mood <= DespairBelow ? GodPaceMath.Trigger.Strong
            : worstOpinion <= GrudgeBelow || mood <= DespairBelow ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        public static bool DeepNight(int hour) => hour >= 0 && hour < 4;

        // ================= Helios =================

        /// <summary>A clear, warm noon (degrees, hours) for a walk into the sun: never in a heat wave.</summary>
        public const float SunFrom = 15f, SunTo = 34f;
        public const int NoonFrom = 11, NoonTo = 14;

        public static bool SunNoon(int hour, float outdoor, bool clear, bool heatWave) =>
            hour >= NoonFrom && hour < NoonTo && clear && !heatWave && outdoor >= SunFrom && outdoor <= SunTo;

        /// <summary>The ailment moves on this much faster for the day after an hour in the sun (its severity eased once, as a cup eases a cold).</summary>
        public const float SunEase = 0.06f;

        /// <summary>Two at odds (opinion below this, either way) outdoors this close at a clear noon.</summary>
        public const float AlikeBelow = -10f, AlikeWithin = 20f;

        /// <summary>A hot noon (degrees), a friend (opinion) and a shaded cell within reach (cells) for the shade.</summary>
        public const float ShadeFrom = 33f, ShadeFriend = 20f, ShadeWithin = 25f;

        // ================= Selene =================

        /// <summary>An insult this lately (days) keeps the one who said it awake under a moon at least half full (GodsOnMapMath.MoonFace), at this chance.</summary>
        public const float SorryDays = 1f, SorryChance = 0.5f;

        /// <summary>The moon at least half full (its face as GodsOnMapMath.MoonFace counts it: gone 0, thin 1, half 2, full 3).</summary>
        public static bool MoonEnough(int face) => face >= 2;

        /// <summary>The insulted one accepts the apology from this opinion of the other up.</summary>
        public const float SorryAcceptFrom = -40f;

        public static BetweenOutcome Sorry(float opinionOfInsulter) => opinionOfInsulter >= SorryAcceptFrom ? BetweenOutcome.MadeUp : BetweenOutcome.Quarrel;

        /// <summary>The morning's hours of breakfast, when dreams are told.</summary>
        public const int BreakfastFrom = 6, BreakfastTo = 9;

        /// <summary>The same dream told: two who like each other (this opinion) come away closer; two who don't are uneasy.</summary>
        public const float DreamFriends = 0f;

        public static BetweenOutcome SameDream(float opinionAB, float opinionBA) =>
            opinionAB >= DreamFriends && opinionBA >= DreamFriends ? BetweenOutcome.Closer : BetweenOutcome.Wary;

        // ================= Tamiu =================

        /// <summary>A grave of one of the colony dead this long at most (days), and those who loved them (opinion) to sit by it.</summary>
        public const float GraveDays = 120f, MournerFrom = 20f;

        public const int GraveDuskFrom = 17, GraveDuskTo = 20;

        /// <summary>Two clever ones (Intellectual at least this) trade riddles at supper.</summary>
        public const int RiddlerFrom = 6;

        /// <summary>The winner of the riddles: the better wit (skill), the luck of the night breaking a tie.</summary>
        public static bool FirstWins(int skillA, int skillB, float roll) => skillA != skillB ? (skillA > skillB ? roll < 0.7f : roll < 0.3f) : roll < 0.5f;

        /// <summary>The cat walks at the winner's heel this many hours.</summary>
        public const int HeelHours = 8;

        // ================= Murmur =================

        /// <summary>A hot afternoon (degrees) at water this near the colony's middle (cells), for a splash, and the frogs that come up croaking.</summary>
        public const float SplashFrom = 26f, SplashWater = 40f;
        public const int SplashFrogs = 2;

        /// <summary>A look's chance, each hour of rain, of a frog in a bedroom.</summary>
        public const float FrogInChance = 0.05f;

        // ================= Ducat =================

        /// <summary>The coin passes this many hands at most before it goes to the stores, a day apart.</summary>
        public const int CoinHands = 3;

        /// <summary>Silver in the stores from which a miser counts it at night, and the heaps it is found in.</summary>
        public const int MiserSilverFrom = 300, MiserHeaps = 6;

        // ================= Revel =================

        /// <summary>Two at odds (opinion below this, either way) at the late fire, between these hours.</summary>
        public const float CupBelow = -5f;
        public const int LateFrom = 20, LateTo = 24;

        /// <summary>Two both drunk late at night make a wager, at this chance a look; the loser cleans the winner's room the next morning.</summary>
        public const float WagerChance = 0.3f;

        public static bool WagerHour(int hour) => hour >= 21 || hour < 2;

        /// <summary>The loser takes it in good part from this opinion of the winner.</summary>
        public const float WagerGoodSport = 10f;

        private static float Clamp01Between(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
