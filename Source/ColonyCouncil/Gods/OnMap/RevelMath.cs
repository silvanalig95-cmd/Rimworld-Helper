using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Revel, the Open Cup (the player, 15 and 16 November 2026): his own happenings' rules, beside the others' in the same
    /// pure class (OwnHappeningsMath: the happenings, their pace, PaceOf), with no game types so Tools/Tests reads the same
    /// numbers the game uses. What calls each and how it plays out. The harm in them is only the game's own: the drugs and
    /// drink the colony's people take themselves, with the game's own highs, hangovers, addictions and overdoses.
    /// </summary>
    public static partial class OwnHappeningsMath
    {
        // ================= The long night =================

        /// <summary>A long night begins at this hour (or the next two) and breaks up at this hour of the small hours.</summary>
        public const int LongNightFrom = 21, LongNightTo = 23, LongNightEnds = 2;

        /// <summary>At least this many of the colony gathered for it: at a party or a feast, or round a late fire.</summary>
        public const int NightGathered = 3, NightGatheredParty = 2;

        /// <summary>
        /// A night runs long at Revel's own feast or a party under way (the game's own, or a ritual), strongly; ordinarily
        /// round a late fire with enough of the colony at their leisure; never with fewer.
        /// </summary>
        public static GodPaceMath.Trigger NightTrigger(bool feast, bool party, int gathered)
        {
            if (feast && gathered >= NightGatheredParty) return GodPaceMath.Trigger.Strong;
            if (party && gathered >= NightGatheredParty) return GodPaceMath.Trigger.Strong;
            return gathered >= NightGathered ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        /// <summary>The recreation each of them takes from every short step of the long night, and the rest it costs them by its end.</summary>
        public const float NightJoyStep = 0.012f, NightRestCost = 0.2f;

        /// <summary>Someone stays up only while rested and fed enough, and leaves the moment either runs low.</summary>
        public const float NightRestFloor = 0.3f, NightFoodFloor = 0.25f;

        public static bool StaysUp(float rest, float food) => rest >= NightRestFloor && food >= NightFoodFloor;

        // ================= A vision =================

        /// <summary>Someone high has a vision only with no fight on: strongly at night or under his presence, else ordinarily.</summary>
        public static GodPaceMath.Trigger VisionTrigger(bool high, bool night, bool presence)
        {
            if (!high) return GodPaceMath.Trigger.None;
            return night || presence ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>A vision shows something true this often (the rest is false, and believed all the same), more the nearer he is.</summary>
        public const float VisionTrueBase = 0.45f;

        public static bool VisionTrue(float roll, float near) => roll < Math.Min(0.75f, VisionTrueBase + 0.2f * Math.Max(0f, Math.Min(1f, near)));

        /// <summary>The place a vision sends them to: this far from where they stand at least, and this far at most.</summary>
        public const float VisionFrom = 8f, VisionTo = 45f;

        /// <summary>The false sights a vision may show (the words' keys' endings), and the true ones by what is really there.</summary>
        public static readonly string[] FalseSights = { "Door", "Watcher", "Light", "Buried" };

        // ================= A craving in the night =================

        /// <summary>The craving gets someone up between these hours.</summary>
        public const int CravingFrom = 0, CravingTo = 3;

        /// <summary>An addict's craving gets them up: strongly in withdrawal, ordinarily with the craving on them, never while satisfied.</summary>
        public static GodPaceMath.Trigger CravingTrigger(bool addicted, bool withdrawal, bool craving, bool drugHere)
        {
            if (!addicted || !drugHere) return GodPaceMath.Trigger.None;
            if (withdrawal) return GodPaceMath.Trigger.Strong;
            return craving ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        // ================= A stash found =================

        /// <summary>Someone this far out from the colony's middle, working outdoors by day, may come on a stash.</summary>
        public const float StashFar = 25f;

        public static GodPaceMath.Trigger StashTrigger(bool someoneFarOut, bool presence) =>
            !someoneFarOut ? GodPaceMath.Trigger.None : presence ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>How many a stash holds, by how dear one of them is: a few of the plain, fewer of the dear, never more than about a hundred silver's worth.</summary>
        public static int StashCount(float unitValue)
        {
            if (unitValue <= 0f || float.IsNaN(unitValue)) return 0;
            int byWorth = (int)Math.Floor(110f / unitValue);
            return Math.Max(1, Math.Min(8, byWorth));
        }

        // ================= A binge egged on =================

        /// <summary>A binge of the game's own is seen this often as if someone were egging it on (once a binge, a witness needed).</summary>
        public const float BingeSeenChance = 0.5f;

        public static GodPaceMath.Trigger BingeTrigger(bool binging, bool witness) => binging && witness ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.None;

        // ================= The craving eased =================

        /// <summary>Someone sits up with an addict in withdrawal from this hour, and at this hour of the morning it has eased.</summary>
        public const int LetGoFrom = 21, LetGoTo = 23, LetGoDawn = 5;

        /// <summary>A friend this close sits up with them (or the priest, or one devoted to Revel).</summary>
        public const int LetGoOpinion = 20;

        /// <summary>The addiction's own severity lifted by this much at dawn (the game's own hediff: it wears off that much sooner).</summary>
        public const float LetGoEase = 0.35f;

        public static GodPaceMath.Trigger LetGoTrigger(bool withdrawal, bool sitter, bool devotedSitter) =>
            !withdrawal || !sitter ? GodPaceMath.Trigger.None : devotedSitter ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        public static float Eased(float severity) => Math.Max(0.01f, severity - LetGoEase);

        // ================= The horned reveller =================

        /// <summary>He comes between these hours, to a party or a feast, or a late fire with this many round it.</summary>
        public const int SatyrFrom = 20, SatyrTo = 23, SatyrGathered = 2;

        /// <summary>He is gone by this hour of the morning.</summary>
        public const int SatyrGone = 4;

        public static GodPaceMath.Trigger SatyrTrigger(bool feast, bool party, int gathered, bool longNight)
        {
            if (gathered < SatyrGathered) return GodPaceMath.Trigger.None;
            return feast || party || longNight ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>Hours he stays at most, from his coming (gone before dawn whatever the hour).</summary>
        public static float SatyrHours(int hourCome)
        {
            int h = ((hourCome % 24) + 24) % 24;
            int until = h >= 12 ? 24 - h + SatyrGone : SatyrGone - h;
            return Math.Max(1f, Math.Min(8f, until));
        }

        // ================= The god in a cup =================

        /// <summary>
        /// He takes one drink or dose in the stores with drink or drugs there to take: strongly at his feast or under his presence,
        /// ordinarily otherwise, never with nothing in the stores or one of his already waiting.
        /// </summary>
        public static GodPaceMath.Trigger CupTrigger(bool stores, bool waiting, bool feastOrPresence)
        {
            if (!stores || waiting) return GodPaceMath.Trigger.None;
            return feastOrPresence ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }
    }

    /// <summary>What the god's cup does to whoever takes it, good or bad and never told in advance.</summary>
    public enum CupOutcome
    {
        None,
        /// <summary>A strong, clean high, and no hangover after.</summary>
        CleanHigh,
        /// <summary>A burst of skill: an inspiration of the game's own, else a sharp mind for half a day.</summary>
        Sharp,
        /// <summary>A vision, true or false, that they believe (OwnRevel's).</summary>
        Vision,
        /// <summary>No fear in a fight for a day.</summary>
        Fearless,
        /// <summary>His deceit: a wonderful high, then a hard crash.</summary>
        Crash,
        /// <summary>His deceit: a sudden craving for more of it.</summary>
        Craving,
        /// <summary>His deceit: the drug's own addiction taking hold at once (the game's own, under the shared harm budget).</summary>
        Hooked
    }

    /// <summary>What the one who took it comes to believe it was (the colony's book keeps it, perhaps wrongly).</summary>
    public enum CupReading
    {
        None,
        /// <summary>A gift of the god's.</summary>
        Blessed,
        /// <summary>Only a strong batch, no god in it.</summary>
        Strong,
        /// <summary>A true sight, given them.</summary>
        Shown,
        /// <summary>A trick played on them.</summary>
        Tricked,
        /// <summary>Something cursed.</summary>
        Cursed
    }

    /// <summary>
    /// The god in a cup (Gods/OnMap/RevelCup.cs; the player, 15 November 2026: "what would be interesting is that the god could
    /// take shape inside an actual drug, and once a pawn consumes it, they will get a godly effect from it"). Now and then he
    /// is in one drink or one dose of a stack in the stores: the k-th one of it, counted from the front, followed through the
    /// game's own splitting and merging of stacks. Whoever takes that one gets one of his effects, good or bad, and the
    /// colony's book keeps what they believe it was. Pure, so Tools/Tests checks the counting, the odds and the readings.
    /// </summary>
    public static class RevelCupMath
    {
        // ================= Following the one =================

        /// <summary>
        /// A stack split: <paramref name="count"/> taken off the front of <paramref name="stackBefore"/>. Whether the god's one
        /// went with the part taken off, and where it now is in its stack. A split of the whole stack moves nothing.
        /// </summary>
        public static (bool moves, int k) Split(int k, int count, int stackBefore)
        {
            if (count <= 0 || count >= stackBefore) return (false, k);
            return k <= count ? (true, k) : (false, k - count);
        }

        /// <summary>
        /// A stack of <paramref name="thisBefore"/> took in <paramref name="n"/> from the front of another holding the god's
        /// one at <paramref name="kOther"/>: whether it came across, and where it now is (after the stack's own).
        /// </summary>
        public static (bool moves, int k) Absorb(int kOther, int n, int thisBefore)
        {
            if (n <= 0) return (false, kOther);
            return kOther <= n ? (true, thisBefore + kOther) : (false, kOther - n);
        }

        /// <summary>Someone took <paramref name="taken"/> from the front of the stack: whether the god's one was among them, and where it is if not.</summary>
        public static (bool taken, int k) Eat(int k, int taken)
        {
            if (taken <= 0) return (false, k);
            return k <= taken ? (true, 0) : (false, k - taken);
        }

        /// <summary>Which one of a stack he is in, from a roll in [0, 1): any of them alike.</summary>
        public static int Which(int stack, float roll) => stack <= 1 ? 1 : 1 + Math.Min(stack - 1, (int)(Math.Max(0f, roll) * stack));

        // ================= What it does =================

        /// <summary>
        /// How likely each is, out of 100: his generosity two times in three (a clean high, a burst of skill, a vision, no fear
        /// in a fight), his deceit one time in three (a high and a crash, a craving, an addiction at once). The addiction only
        /// for a drug that has one (the rest of its share goes to the crash), and a little likelier for a hard drug.
        /// </summary>
        public static (CupOutcome o, float w)[] Weights(bool addictive, bool hard)
        {
            float hooked = !addictive ? 0f : hard ? 7f : 5f;
            return new[]
            {
                (CupOutcome.CleanHigh, 22f), (CupOutcome.Sharp, 18f), (CupOutcome.Vision, 15f), (CupOutcome.Fearless, 12f),
                (CupOutcome.Crash, 18f + (addictive ? 0f : 5f) - (hard && addictive ? 2f : 0f)), (CupOutcome.Craving, 10f), (CupOutcome.Hooked, hooked)
            };
        }

        /// <summary>The outcome for a roll in [0, 1).</summary>
        public static CupOutcome Pick(float roll, bool addictive, bool hard)
        {
            var w = Weights(addictive, hard);
            float total = 0f;
            foreach (var x in w) total += x.w;
            float at = Math.Max(0f, Math.Min(0.99999f, roll)) * total;
            foreach (var x in w)
            {
                if (at < x.w) return x.o;
                at -= x.w;
            }
            return CupOutcome.CleanHigh;
        }

        /// <summary>His deceit, not his generosity.</summary>
        public static bool Deceit(CupOutcome o) => o == CupOutcome.Crash || o == CupOutcome.Craving || o == CupOutcome.Hooked;

        /// <summary>An addiction that could not take (the shared budget said no, or the drug has none) is a high and a crash instead.</summary>
        public static CupOutcome HookedOr(bool mayHook) => mayHook ? CupOutcome.Hooked : CupOutcome.Crash;

        /// <summary>A craving sends them for a second one only of a drug that isn't hard, and only where their drug policy allows it for fun.</summary>
        public static bool SecondDose(bool hard, bool allowed) => allowed && !hard;

        // ================= What they come to believe =================

        /// <summary>
        /// What the one who took it believes it was, as the book keeps it: by what happened and how they stand toward the god
        /// they take it for. A scoffer believes it was only strong stuff, whatever it was; one in dread takes even a kindness
        /// for a trick; the rest read a kindness as a gift (or, one time in four, only a strong batch), a vision as a true
        /// sight (true or not), and the deceit as a trick or, one time in three, a curse. Never the truth marked.
        /// </summary>
        public static CupReading Read(CupOutcome o, bool scoffer, bool dreads, float roll)
        {
            if (o == CupOutcome.None) return CupReading.None;
            if (scoffer) return CupReading.Strong;
            if (o == CupOutcome.Vision) return CupReading.Shown;
            if (dreads) return Deceit(o) && roll < 0.33f ? CupReading.Cursed : CupReading.Tricked;
            if (Deceit(o)) return roll < 0.33f ? CupReading.Cursed : CupReading.Tricked;
            return roll < 0.25f ? CupReading.Strong : CupReading.Blessed;
        }

        /// <summary>A scoffer's reading goes into the book as a doubt, not a belief.</summary>
        public static bool Doubts(CupReading r) => r == CupReading.Strong;

        // ================= Its hours =================

        /// <summary>How long his one waits in the stores before he leaves it (days): nobody took it.</summary>
        public const float CupWaitDays = 30f;

        /// <summary>Someone passing near it may notice something small about it, once: this likely in an hour they are near.</summary>
        public const float NoticeChance = 0.12f;

        /// <summary>How near they pass, in cells.</summary>
        public const float NoticeRange = 4f;

        /// <summary>The crash comes this many hours after the high (Rimfolk_RevelCrash: a quarter of its severity at one and a half a day), and the book is written after it.</summary>
        public const float CrashHours = 4f;

        /// <summary>A clean high keeps the hangover off for this many hours.</summary>
        public const float CleanHours = 30f;

        /// <summary>The book is written this many hours after the cup was taken (the effect has shown by then), the crash after it comes.</summary>
        public const float BookHours = 4f;
    }
}
