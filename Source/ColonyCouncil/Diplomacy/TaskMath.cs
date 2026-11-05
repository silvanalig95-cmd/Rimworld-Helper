using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What an officer's task is (docs/design/stage2-diplomacy.md, item 8; decision 14). Saved by name.</summary>
    public enum TaskKind
    {
        None,
        /// <summary>The speaker wins over a town's head: letters, a call, an invitation, a visit; at a regard of 60, their offer.</summary>
        WinOver,
        /// <summary>The keeper of secrets learns a notable's secret: talks with their people here, a paid informer, a visit.</summary>
        Secrets,
        /// <summary>The marshal walks the edges of the colony's land at dawn for a season: a hidden raiders' camp found sooner.</summary>
        Scout,
        /// <summary>The marshal visits a pact partner and drills their fighters: their band a fifth stronger for a season.</summary>
        Drill,
        /// <summary>The quartermaster opens a trade with a town's trader: letters, samples sent, a visit; their offer at the end.</summary>
        Trade,
        /// <summary>The quartermaster collects the dues of the colony's own vassals (release 2c.2; reserved, not offered yet).</summary>
        Dues,
        /// <summary>The physician visits a town with a sickness running, with medicine from the stores.</summary>
        Tend,
        /// <summary>The warden of guests writes to the people holding one of the colony's: their price for them, lowered.</summary>
        BringHome,
        /// <summary>The warden writes to a people whose kin are in the colony's cells: their offers sooner and higher.</summary>
        TheirKin,
        /// <summary>The chaplain keeps the faith with a notable of the colony's own faith: their regard, and the believers at home steadied.</summary>
        KeepFaith,
        /// <summary>The chaplain speaks for the faith to a notable of another: their certainty shaken a little, never a conversion.</summary>
        SpeakFaith,
        /// <summary>The steward prepares the next feast with a people's notables as guests: their welcome counts half again.</summary>
        Host
    }

    /// <summary>Where a task stands.</summary>
    public enum TaskStage
    {
        Running,
        /// <summary>It came to what it was for (won over, a secret learned, the feast held, a season's patrols walked).</summary>
        Done,
        /// <summary>It went wrong (the keeper found out).</summary>
        Failed,
        /// <summary>Called off by the council, or its officer, target or road gone.</summary>
        Off
    }

    /// <summary>A step an officer takes on a task (the last one is shown, never a time left).</summary>
    public enum TaskStep
    {
        None,
        /// <summary>A letter written at a table, carried by their messenger.</summary>
        Letter,
        /// <summary>A call on the comms console (a powered console, and a people industrial or later).</summary>
        Call,
        /// <summary>A talk face to face with one of theirs at the colony.</summary>
        Talk,
        /// <summary>A paid informer met at the gate.</summary>
        Informer,
        /// <summary>A walk along the edges of the colony's land at dawn.</summary>
        Patrol,
        /// <summary>A visit to their town by caravan (the officer and a companion, through the journey planner).</summary>
        Visit,
        /// <summary>Their notable received as the colony's guest (the invitation's visit).</summary>
        Guest,
        /// <summary>A cart of goods sent to them (samples for their trader; a vassal's dues from release 2c.2).</summary>
        Cart,
        /// <summary>A feast with their notables as guests.</summary>
        Feast
    }

    /// <summary>An officer's visit by caravan: none, planned (the journey planner holds it), on the road, at the town, on the way home.</summary>
    public enum VisitStage
    {
        None,
        Planned,
        Road,
        There,
        Homeward
    }

    /// <summary>What a task is aimed at.</summary>
    public enum TaskTarget
    {
        /// <summary>A notable of a town (a head, a trader).</summary>
        Notable,
        /// <summary>A people (through their head).</summary>
        People,
        /// <summary>A town with something running there (a sickness).</summary>
        Town,
        /// <summary>One of the colony's own, held by another people.</summary>
        Colonist,
        /// <summary>The colony's own land.</summary>
        Home
    }

    /// <summary>The result of a keeper's step: a secret learned, nothing, or found out.</summary>
    public enum SecretRoll
    {
        Nothing,
        Learned,
        FoundOut
    }

    /// <summary>
    /// Council tasks (docs/design/stage2-diplomacy.md, item 8; release 2c.1), with no game types so Tools/Tests can check
    /// them: which office has which task and what it is aimed at, the steps each takes (letters and calls, talks, an
    /// informer, patrols, a visit by caravan), their timing (never shown as a time left), what a step does by the
    /// officer's skill, how a task ends, one officer away at a home and one visit a season each, the bad sides at the
    /// design's rates, the words' keys and the facts' tags. The keeper of secrets' own rules are KeeperMath.cs.
    /// </summary>
    public static class TaskMath
    {
        public const int DayTicks = 60000;
        public const int HourTicks = 2500;
        public const int SeasonDays = 15;
        public const int YearDays = 60;

        // ---------------- Offices and their tasks ----------------

        public const string Speaker = "Council_Office_Speaker", KeeperOffice = "Council_Office_SecretKeeper", Marshal = "Council_Office_Marshal",
            Quartermaster = "Council_Office_Quartermaster", Physician = "Council_Office_Physician", Warden = "Council_Office_Warden",
            Chaplain = "Council_Office_Chaplain", Steward = "Council_Office_Steward";

        /// <summary>The eight offices with tasks (decision 14); the reeve, the herdmaster and the keeper of days have none.</summary>
        public static readonly string[] Offices = { Speaker, KeeperOffice, Marshal, Quartermaster, Physician, Warden, Chaplain, Steward };

        /// <summary>Every kind but None, in the order offered.</summary>
        public static readonly TaskKind[] Kinds =
        {
            TaskKind.WinOver, TaskKind.Secrets, TaskKind.Scout, TaskKind.Drill, TaskKind.Trade, TaskKind.Dues, TaskKind.Tend, TaskKind.BringHome, TaskKind.TheirKin,
            TaskKind.KeepFaith, TaskKind.SpeakFaith, TaskKind.Host
        };

        /// <summary>One task per office per home, so a home keeps thirteen records at most (one a seat kind, the design's bound).</summary>
        public const int MaxPerHome = 13;

        public static string OfficeOf(TaskKind k)
        {
            switch (k)
            {
                case TaskKind.WinOver: return Speaker;
                case TaskKind.Secrets: return KeeperOffice;
                case TaskKind.Scout:
                case TaskKind.Drill: return Marshal;
                case TaskKind.Trade:
                case TaskKind.Dues: return Quartermaster;
                case TaskKind.Tend: return Physician;
                case TaskKind.BringHome:
                case TaskKind.TheirKin: return Warden;
                case TaskKind.KeepFaith:
                case TaskKind.SpeakFaith: return Chaplain;
                case TaskKind.Host: return Steward;
                default: return null;
            }
        }

        public static List<TaskKind> KindsOf(string office)
        {
            var list = new List<TaskKind>();
            foreach (var k in Kinds)
                if (OfficeOf(k) == office)
                    list.Add(k);
            return list;
        }

        public static bool HasTasks(string office) => Array.IndexOf(Offices, office) >= 0;

        /// <summary>Built and offered now: every kind but the vassals' dues, which come with release 2c.2.</summary>
        public static bool Built(TaskKind k) => k != TaskKind.None && k != TaskKind.Dues;

        public static TaskTarget TargetOf(TaskKind k)
        {
            switch (k)
            {
                case TaskKind.WinOver:
                case TaskKind.Secrets:
                case TaskKind.Trade:
                case TaskKind.KeepFaith:
                case TaskKind.SpeakFaith: return TaskTarget.Notable;
                case TaskKind.Tend: return TaskTarget.Town;
                case TaskKind.BringHome: return TaskTarget.Colonist;
                case TaskKind.Scout: return TaskTarget.Home;
                default: return TaskTarget.People;
            }
        }

        /// <summary>The officer writes (or calls) on this task.</summary>
        public static bool Writes(TaskKind k) =>
            k == TaskKind.WinOver || k == TaskKind.Trade || k == TaskKind.BringHome || k == TaskKind.TheirKin || k == TaskKind.KeepFaith || k == TaskKind.SpeakFaith
            || k == TaskKind.Host;

        /// <summary>The officer may visit their town on this task (once a season).</summary>
        public static bool MayVisit(TaskKind k) =>
            k == TaskKind.WinOver || k == TaskKind.Secrets || k == TaskKind.Drill || k == TaskKind.Trade || k == TaskKind.Tend || k == TaskKind.KeepFaith
            || k == TaskKind.SpeakFaith;

        /// <summary>The task is the visit: it is planned when the task is set, and ends when they are home.</summary>
        public static bool VisitOnly(TaskKind k) => k == TaskKind.Drill || k == TaskKind.Tend;

        /// <summary>Aimed at a people the colony is at peace with (the warden's bring-home is aimed at whoever holds one of the colony's).</summary>
        public static bool AtPeaceOnly(TaskKind k) => k != TaskKind.BringHome && k != TaskKind.TheirKin && k != TaskKind.Scout;

        // ---------------- One away, a visit a season ----------------

        /// <summary>A new visit may be planned: nobody of this home away on the council's business, and this office's last visit a season ago.</summary>
        public static bool MayVisitNow(bool anyAway, int lastVisit, int now) => !anyAway && (lastVisit < 0 || now - lastVisit >= SeasonDays * DayTicks);

        /// <summary>A day at their town before the way home.</summary>
        public const float StayDays = 1f;

        /// <summary>A visit's colonists: the officer and a companion (a home needs a third to stay).</summary>
        public const int VisitParty = 2, HomeKeeps = 1;

        public static bool CanSpare(int freeAtHome) => freeAtHome >= VisitParty + HomeKeeps;

        /// <summary>The trip's length in days for the planner: the road there and back and the day at their town.</summary>
        public static float TripDays(float roadDaysEachWay) => Math.Max(0.5f, roadDaysEachWay) * 2f + StayDays;

        // ---------------- Timing (never shown as a time left) ----------------

        /// <summary>The first letter goes a few hours after the task is set.</summary>
        public static float FirstLetterHours(float roll) => 2f + 10f * Clamp01(roll);

        /// <summary>A letter every four to six days.</summary>
        public static float NextLetterDays(float roll) => 4f + 2f * Clamp01(roll);

        /// <summary>A letter that can't go now (the writer busy, one at a time to them, the wait): tried again some hours on.</summary>
        public const float RetryHours = 6f;

        /// <summary>A task with letters gives up after a year without coming to anything.</summary>
        public static bool GivesUp(TaskKind k, int started, int now)
        {
            if (started < 0) return false;
            float days = (now - started) / (float)DayTicks;
            switch (k)
            {
                case TaskKind.Scout:
                case TaskKind.KeepFaith:
                case TaskKind.SpeakFaith:
                case TaskKind.Host:
                case TaskKind.TheirKin:
                    return days >= SeasonDays;
                default:
                    return days >= YearDays;
            }
        }

        /// <summary>A season's task ends well when something was done in it: a letter answered, a patrol walked.</summary>
        public static bool SeasonDone(TaskKind k, int steps) => (k == TaskKind.Scout || k == TaskKind.KeepFaith || k == TaskKind.SpeakFaith || k == TaskKind.TheirKin) && steps > 0;

        // ---------------- The speaker: win them over ----------------

        /// <summary>At this regard the head offers a pact or owes the colony a favour.</summary>
        public const int WinGoal = 60;

        /// <summary>A letter's regard by the officer's Social: 1 for a clumsy hand, 6 for a master.</summary>
        public static float RegardUp(int social) => 1f + Clamp(social, 0, 20) * 0.25f;

        /// <summary>A visit counts three letters.</summary>
        public const float VisitTimes = 3f;

        /// <summary>A titled head receives a titled officer more warmly (the Empire, decision 9: regard half again).</summary>
        public static float Titled(bool titledHead, bool titledOfficer) => titledHead && titledOfficer ? 1.5f : 1f;

        /// <summary>Their answer's tone (0 warm, 1 cordial, 2 cool, 3 curt, 4 refusal, 5 silence) scales a letter's step: a refusal does nothing, silence nothing.</summary>
        public static float ToneShare(int tone)
        {
            switch (tone)
            {
                case 0: return 1.25f;
                case 1: return 1f;
                case 2: return 0.6f;
                case 3: return 0.3f;
                default: return 0f;
            }
        }

        public static bool Won(int opinion) => opinion >= WinGoal;

        /// <summary>What winning them over brings: a pact offered when one can be (a defence pact before a trade pact), else a favour owed.</summary>
        public static string WinResult(bool defenceOpen, bool tradeOpen) => defenceOpen ? "defence" : tradeOpen ? "trade" : "favour";

        // ---------------- The quartermaster: open a trade ----------------

        /// <summary>The trade is open at this much.</summary>
        public const float TradeGoal = 100f;

        /// <summary>A letter's way toward it, by Social and Intellectual (the counting and the talking): 5 to 20.</summary>
        public static float TradeGain(int social, int intellectual) => 5f + (Clamp(social, 0, 20) + Clamp(intellectual, 0, 20)) * 0.375f;

        /// <summary>A cart of samples counts this much.</summary>
        public const float CartGain = 30f;

        /// <summary>What opening the trade brings: their trade pact offered when one can be, else their trader's caravan sent soon.</summary>
        public static string TradeResult(bool tradePactOpen) => tradePactOpen ? "pact" : "caravan";

        /// <summary>Their caravan comes in one to three days.</summary>
        public static float CaravanDays(float roll) => 1f + 2f * Clamp01(roll);

        /// <summary>The samples sent: a quarter of what a trade pact's cart would carry, at least ten.</summary>
        public static int Samples(int cartCount) => Math.Max(10, cartCount / 4);

        // ---------------- The marshal ----------------

        /// <summary>A patrol every two days at dawn (from 5 to 8 o'clock), for a season.</summary>
        public const int PatrolEveryDays = 2, DawnFrom = 5, DawnTo = 9;

        public static bool Dawn(int hour) => hour >= DawnFrom && hour < DawnTo;

        public static bool PatrolDue(int lastPatrol, int now) => lastPatrol < 0 || now - lastPatrol >= PatrolEveryDays * DayTicks - 6 * HourTicks;

        /// <summary>A hidden raiders' camp is seen from the edges on a patrol at this chance (the game's own finding goes on beside it).</summary>
        public const float SpotChance = 0.6f;

        public static bool Spotted(float roll) => Clamp01(roll) < SpotChance;

        /// <summary>The cells a patrol walks to: this many along the edges, this far in.</summary>
        public const int PatrolPoints = 4, EdgeIn = 8;

        /// <summary>Drilled by the marshal: their band a fifth stronger for a season after the visit.</summary>
        public const float LevyStrength = 1.2f;

        public static float LevyFactor(int drilledAt, int now) => drilledAt >= 0 && now - drilledAt < SeasonDays * DayTicks ? LevyStrength : 1f;

        // ---------------- The physician ----------------

        /// <summary>Medicine taken from the stores to them: this many (fewer when the stores hold fewer, but some).</summary>
        public const int Medicine = 6, MinMedicine = 2;

        public static int MedicineFor(int inStores) => Math.Min(Medicine, Math.Max(0, inStores - 2));

        /// <summary>One visit in five brings the sickness home: the game's own disease on one of the caravan (named before the choice).</summary>
        public const float SickBackChance = 0.2f;

        public static bool SickBack(float roll) => Clamp01(roll) < SickBackChance;

        /// <summary>Their head's regard for the medicine brought.</summary>
        public const float TendRegard = 12f;

        // ---------------- The warden of guests ----------------

        /// <summary>Their price for one of the colony's, lowered by each letter by the warden's Social, to half at most.</summary>
        public static float PriceFactor(int social, int letters) => Math.Max(0.5f, 1f - Math.Max(0, letters) * (0.05f + Clamp(social, 0, 20) * 0.01f));

        /// <summary>After this many letters answered, their word on a price for the colony's own comes to the gate.</summary>
        public const int DemandAfter = 2;

        /// <summary>While the warden writes to them, their offers for their own in the colony's cells come in half the time and a quarter higher.</summary>
        public const float KinSooner = 0.5f, KinHigher = 1.25f;

        // ---------------- The chaplain ----------------

        /// <summary>The believers at home each a little surer for a warm or cordial answer from a notable of the same faith.</summary>
        public const float CertaintyUp = 0.02f;

        /// <summary>A letter to a notable of another faith shakes their certainty a little by the chaplain's Social, never below a fifth.</summary>
        public static float CertaintyDown(int social) => 0.02f + Clamp(social, 0, 20) * 0.002f;

        public const float MinCertainty = 0.2f;

        public static float Shaken(float certainty, int social) => Math.Max(Math.Min(certainty, MinCertainty), certainty - CertaintyDown(social));

        // ---------------- The steward ----------------

        /// <summary>The steward's guests' welcome counts half again.</summary>
        public const float WelcomeFactor = 1.5f;

        public static float Welcome(float welcome, bool stewards) => stewards && welcome > 0f ? welcome * WelcomeFactor : welcome;

        // ---------------- The steps in This week ----------------

        /// <summary>Where the task stands for This week's steps: set, at work, away, done (never a time left).</summary>
        public static int StepIndex(TaskStage stage, VisitStage visit, int steps)
        {
            if (stage != TaskStage.Running) return 3;
            if (visit != VisitStage.None) return 2;
            return steps > 0 ? 1 : 0;
        }

        /// <summary>A visit still to be planned is a step the player takes (This week's Needs you).</summary>
        public static bool NeedsPlayer(TaskKind k, TaskStage stage, VisitStage visit, bool feastPlanned, int letters) =>
            stage == TaskStage.Running && (VisitOnly(k) && visit == VisitStage.None || k == TaskKind.Host && !feastPlanned && letters > 0);

        // ---------------- History, facts, words ----------------

        public const string HistoryKind = "dip:task";

        /// <summary>The facts' tags: a task of the colony's with their people (us:task, us:task:KIND), the officer who writes often, who visited this season.</summary>
        public const string TaskTag = "us:task", WritesTag = "task:writes", VisitedTag = "task:visited";

        public static string KindTag(TaskKind k) => TaskTag + ":" + Code(k);

        /// <summary>Two letters or more is "an officer who writes often".</summary>
        public const int WritesOften = 2;

        /// <summary>A task they know of: everything but the keeper's (a secret sought isn't talked of).</summary>
        public static bool Known(TaskKind k) => k != TaskKind.Secrets && k != TaskKind.Scout && k != TaskKind.None;

        /// <summary>Every tag the facts can set, for the talk pieces' checks.</summary>
        public static IEnumerable<string> Tags()
        {
            yield return TaskTag;
            yield return WritesTag;
            yield return VisitedTag;
            foreach (var k in Kinds)
                if (Known(k))
                    yield return KindTag(k);
        }

        /// <summary>{OFFICER} the officer's name, {OFFICE} the office in a word.</summary>
        public const string OfficerKey = "OFFICER", OfficeKey = "OFFICE";

        public static string Code(TaskKind k) => k.ToString().ToLowerInvariant();

        public static bool Parse(string s, out TaskKind k)
        {
            k = TaskKind.None;
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var x in Kinds)
                if (string.Equals(Code(x), s, StringComparison.OrdinalIgnoreCase))
                {
                    k = x;
                    return true;
                }
            return false;
        }

        // ---------------- Helpers ----------------

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
