using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a stores' count finds shortest: nothing, the food, the medicine, or the cooked meals.</summary>
    public enum StoresShort
    {
        None,
        Food,
        Medicine,
        Meals
    }

    /// <summary>
    /// The council's integrations, part 1 (the content update's A1, A2, A5, A6 and A21, 20 November 2026): the four seats
    /// seen at their work, the seat whose work a question is about, a promise looked at halfway through, petitions read
    /// out at the meeting, and the hard call of "the weight of office". The rules, free of game types
    /// (Tools/Tests/SeatWorkTests.cs); the game's side is Interplay/Interplay_Seats.cs, Offices/SeatCalls.cs,
    /// Offices/SeatHalfway.cs and Ambitions/MeetingBusiness.cs.
    /// </summary>
    public static class SeatMath
    {
        // ---------------- The speaker hears someone out (A1) ----------------

        /// <summary>Trust in the council under which someone is worth an hour of the speaker's time (neutral trust runs -15 to 25).</summary>
        public const float HearBelow = 10f;

        /// <summary>Days between the speaker's sittings, and before the same person is heard out again.</summary>
        public const float SpeakerEvery = 3f, HearAgain = 8f;

        /// <summary>How long they sit, in hours.</summary>
        public const float HearHours = 1f;

        /// <summary>The trust an hour with the speaker gives back: one point, two from a speaker of Social 20.</summary>
        public static float HeardTrust(int social) => 1f + Math.Max(0f, Math.Min(1f, social / 20f));

        /// <summary>
        /// Whom the speaker sits with: the one whose trust is lowest, when it is under <see cref="HearBelow"/> and they
        /// may be heard again (<paramref name="mayHear"/>); -1 for nobody.
        /// </summary>
        public static int Lowest(IList<float> trust, IList<bool> mayHear)
        {
            int best = -1;
            if (trust == null) return best;
            for (int i = 0; i < trust.Count; i++)
            {
                if (trust[i] >= HearBelow || (mayHear != null && i < mayHear.Count && !mayHear[i])) continue;
                if (best < 0 || trust[i] < trust[best]) best = i;
            }
            return best;
        }

        // ---------------- The quartermaster's count (A1) ----------------

        /// <summary>Once a week, as the seat's own promise says ("a count of the stores every week").</summary>
        public const float CountEvery = 7f;

        /// <summary>The count's hours: at dawn.</summary>
        public const int DawnFrom = 5, DawnTo = 8;

        /// <summary>The marks a store is measured against: days of food, medicine and cooked meals for each of the colony.</summary>
        public const float FoodMark = 15f, MedicineMark = 2f, MealsMark = 2f;

        /// <summary>
        /// What is shortest: the store furthest under its mark (days of food against <see cref="FoodMark"/>, medicine and
        /// cooked meals for each person against theirs); nothing when every store is at its mark or above.
        /// </summary>
        public static StoresShort Shortest(float foodDays, float medicineEach, float mealsEach)
        {
            float food = foodDays / FoodMark, medicine = medicineEach / MedicineMark, meals = mealsEach / MealsMark;
            float least = Math.Min(food, Math.Min(medicine, meals));
            if (least >= 1f) return StoresShort.None;
            if (least == medicine) return StoresShort.Medicine;
            if (least == food) return StoresShort.Food;
            return StoresShort.Meals;
        }

        // ---------------- The reeve's walk (A1) ----------------

        /// <summary>Days between the reeve's walks in the growing season; with blight in the fields not yet marked, the next morning.</summary>
        public const float ReeveEvery = 2f, ReeveBlight = 0.5f;

        /// <summary>The reeve walks the fields this morning: in the growing season, every other day, or at once for blight.</summary>
        public static bool ReeveDue(bool growing, bool blight, float daysSince) =>
            (growing || blight) && daysSince >= (blight ? ReeveBlight : ReeveEvery);

        // ---------------- The marshal at dusk (A1) ----------------

        /// <summary>The marshal walks the walls at dusk for this many days after an attack.</summary>
        public const float MarshalDays = 2f;

        /// <summary>The dusk hours, and how long the walk is (hours).</summary>
        public const int DuskFrom = 17, DuskTo = 20;

        public const float MarshalHours = 1f;

        /// <summary>A dusk walk is due: an attack in the last two days, and no walk in the last most of a day.</summary>
        public static bool MarshalDue(float daysSinceThreat, float daysSinceWalk) =>
            daysSinceThreat >= 0f && daysSinceThreat <= MarshalDays && daysSinceWalk >= 0.75f;

        // ---------------- A promise halfway (A5) ----------------

        /// <summary>A promise is looked at once its holder has been this long in the seat.</summary>
        public const float HalfwayDays = 25f;

        /// <summary>How the hands go to a seat whose promise is going badly: the seat's own work.</summary>
        public enum Hands
        {
            None,
            /// <summary>A night's watch on the walls (the colony has lost someone).</summary>
            Watch,
            /// <summary>Food brought in: the harvest, the fish, the hauling (the meals are running out).</summary>
            Food,
            /// <summary>The sickroom cleaned and the medicine carried in (the medicine or the sickbeds).</summary>
            Sick,
            /// <summary>The herd brought home and seen to.</summary>
            Herd,
            /// <summary>An evening together, the holder's own (the colony's spirits).</summary>
            Evening
        }

        /// <summary>The hands for a promise judged by this test; None for a promise hands can't help (trust, quarrels, days, faith, the seat's own work).</summary>
        public static Hands HandsFor(PromiseTest test)
        {
            switch (test)
            {
                case PromiseTest.Lost: return Hands.Watch;
                case PromiseTest.Food: return Hands.Food;
                case PromiseTest.Medicine:
                case PromiseTest.Sickbeds: return Hands.Sick;
                case PromiseTest.Herd: return Hands.Herd;
                case PromiseTest.Mood: return Hands.Evening;
                default: return Hands.None;
            }
        }

        /// <summary>
        /// The map clearly shows the promise going badly halfway through: the holder 25 days or more in the seat, a promise
        /// hands can help, and the very test the vote will use (<see cref="RecordMath.Judge"/>) already reading broken.
        /// </summary>
        public static bool GoingBadly(PromiseTest test, float before, float now, float days) =>
            days >= HalfwayDays && HandsFor(test) != Hands.None && RecordMath.Judge(test, before, now, days) == PromiseVerdict.Broken;

        // ---------------- Any other business (A6) ----------------

        /// <summary>
        /// Aye to a decree petition, by how someone feels about the decree (-2 hates it ... 2 loves it): for it, they like
        /// it; to lift it, they dislike it.
        /// </summary>
        public static bool AyeToDecree(int feeling, bool repeal) => repeal ? feeling < 0 : feeling > 0;

        /// <summary>Aye to a petition of wishes: they are in it, want the same, or think well of whoever asked (opinion 20 or more).</summary>
        public static bool AyeToWish(bool inIt, bool wantsSame, int opinionOfAsker) => inIt || wantsSame || opinionOfAsker >= 20;

        /// <summary>The meeting backed it: more ayes than not among those there.</summary>
        public static bool Backed(int ayes, int present) => present > 0 && ayes * 2 > present;

        /// <summary>At most this many petitions read out at one meeting, the oldest first.</summary>
        public const int MaxRead = 4;

        // ---------------- The hard call (A21) ----------------

        /// <summary>A real call for the weight of office: a question of the seat's work, a decree they backed that someone hates, the supper of A5.</summary>
        public const string CallQuestion = "call:question", CallDecree = "call:decree", CallSupper = "call:supper";

        /// <summary>The story's tag when the seat was lost, and when no call came in time.</summary>
        public const string OfficeGone = "officegone", NoCall = "nocall";

        /// <summary>The stage that waits for the call, and how long it waits before the story ends quietly.</summary>
        public const int CallStage = 1;

        public const float NoCallDays = 20f;

        public static bool IsCall(string tag) => tag == CallQuestion || tag == CallDecree || tag == CallSupper;

        /// <summary>The story waits on for a call: in the stage that waits for it, with none yet.</summary>
        public static bool WaitsForCall(int stage, string tag) => stage == CallStage && !IsCall(tag);

        /// <summary>It ends quietly: in that stage, no call in twenty days.</summary>
        public static bool EndsQuietly(int stage, string tag, float daysInStage) => WaitsForCall(stage, tag) && daysInStage >= NoCallDays;

        // ---------------- Calls in the record (A2) ----------------

        /// <summary>Questions of the seat's work spoken in since the promise: the record's count now, less what it was then.</summary>
        public static int CallsSince(float now, float then) => Math.Max(0, (int)Math.Round(now - then));
    }
}
