using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Who a ward or hostage record is about, and which way they went.</summary>
    public enum WardKind
    {
        /// <summary>Their youth, living at the colony for a year to learn a skill from a guardian (a lodger).</summary>
        Theirs,
        /// <summary>A youth of the colony's, living with their people for a year (lent; with the Empire, a page in a titled household).</summary>
        Ours,
        /// <summary>One of theirs given for a truce's or a pact's terms, living at the colony for the term (a lodger).</summary>
        TheirHostage,
        /// <summary>One of the colony's given for the terms, living at their town for the term (lent).</summary>
        OurHostage
    }

    /// <summary>Where a ward or hostage stands, step by step (never a time left).</summary>
    public enum WardStage
    {
        /// <summary>Agreed: theirs on the road to the colony, ours getting ready to set out.</summary>
        Coming,
        /// <summary>Theirs living at the colony.</summary>
        Here,
        /// <summary>Ours on the road to their town, or living there.</summary>
        Away,
        /// <summary>The term over: theirs walking out to their kin, ours on the road home.</summary>
        Going,
        /// <summary>Their hostage, their side having broken the terms: the colony's to decide about.</summary>
        Forfeit,
        /// <summary>Ours held by their people, the colony having broken the terms: the game's own captivity.</summary>
        Held
    }

    /// <summary>How their ward is, in a word: homesick, settling in, happy.</summary>
    public enum WardMood
    {
        Homesick,
        Settling,
        Happy
    }

    /// <summary>How the year went, as their people see it when the ward comes home.</summary>
    public enum WardResult
    {
        Poor,
        Fair,
        Good,
        /// <summary>Taught, fed, unhurt and content: a favour owed.</summary>
        Fine
    }

    /// <summary>
    /// What their hostage is among their people (the player's decision, 6 November 2026: anyone of standing among them,
    /// never by kinship; until then one of their notable's kin): made with their own kinds as their people have them.
    /// </summary>
    public enum HostageStanding
    {
        /// <summary>Given before the rule (an older save's), or the colony's own.</summary>
        Unknown,
        /// <summary>One of their traders: the kind their caravans bring.</summary>
        Trader,
        /// <summary>One of their seasoned fighters: the stronger half of their war bands' kinds.</summary>
        Fighter,
        /// <summary>A people with neither: one of their people.</summary>
        OneOfThem
    }

    /// <summary>
    /// The arithmetic of wards and hostages (docs/design/stage2-diplomacy.md, round b, item 7; release 2b.3; the player's
    /// decisions 9 and 10), with no game types so Tools/Tests can check it.
    /// <list type="bullet">
    /// <item>Their ward: a youth of their town (a child with Biotech, else 14 to 19; never of a notable's house since 5
    /// November 2026) comes for a year to learn a skill
    /// the colony is known for, from a guardian with 10 or more in it; offered by a people at peace once a year at most
    /// (their head's regard 30 or more), a season apart at a home, or asked for by letter. The guardian teaches them an hour
    /// a day when both are free and together (a child with Biotech learns at the game's own lessons). How they are is read
    /// from their mood and the days they have been here; at the year's end how they were kept (taught, fed, unhurt,
    /// content) moves their people, a year kept well is a favour owed, and the grown ward remembers it.</item>
    /// <item>Our ward: a youth of the colony's goes to their town for a year, writes home every ten days and comes back with
    /// what their people are good at, by their ways, and a friend among their notables. With the Empire, a page in a titled
    /// household: regard only, no skill.</item>
    /// <item>Hostages, adults only (children never): given for a truce's or a defence pact's terms, either way. Theirs, one of
    /// standing among their people (one of their traders or seasoned fighters, never by kinship since 6 November 2026), lives
    /// at the colony as a lodger for the term, ours at their town. At the end both go home; their side breaking the terms
    /// makes theirs the colony's to decide about, the colony breaking them leaves ours in the game's own captivity.</item>
    /// </list>
    /// Per home one ward each way and one hostage each way per tie, four records in all.
    /// </summary>
    public static class WardMath
    {
        public const int DayTicks = 60000;

        /// <summary>A ward stays a year.</summary>
        public const float YearDays = 60f;

        public const string WardOrigin = "ward", HostageOrigin = "hostage";

        /// <summary>The key of a fostered ward's Regard tie toward the colony: they remember the year while they live.</summary>
        public const string FosteredKey = "fostered";

        /// <summary>At most this many records at a home: a ward each way and a hostage each way.</summary>
        public const int MaxPerHome = 4;

        // ================= Their ward =================

        /// <summary>Their head thinks this well of the colony or better before they offer a ward, or agree to send one.</summary>
        public const int MinRegard = 30;

        /// <summary>Once a year per people; a season apart at a home.</summary>
        public const float PeopleGapDays = 60f, HomeGapDays = 15f;

        /// <summary>Each day a people that may offer does so one time in this many.</summary>
        public const float DailyOdds = 40f;

        /// <summary>The guardian has this much of the skill or more.</summary>
        public const int GuardianSkill = 10;

        /// <summary>
        /// A people may offer a ward to this home now: at peace, their head's regard 30 or more, a guardian at the home for
        /// some skill, no ward of theirs or ours under way there, and the gaps kept (-1: never offered).
        /// </summary>
        public static bool MayOffer(bool hostile, int regard, bool guardianThere, bool wardThere, int lastPeopleOffer, int lastHomeOffer, int now) =>
            !hostile && regard >= MinRegard && guardianThere && !wardThere
            && (lastPeopleOffer < 0 || now - lastPeopleOffer >= PeopleGapDays * DayTicks)
            && (lastHomeOffer < 0 || now - lastHomeOffer >= HomeGapDays * DayTicks);

        public static bool OffersToday(float roll) => roll < 1f / DailyOdds;

        /// <summary>
        /// The colony may ask them for a ward only while their head thinks 30 or more of the colony (<see cref="MinRegard"/>, as
        /// for their own offer; the player, 10 November 2026: "gameplay realism"). Until then any regard from 0 could ask.
        /// </summary>
        public static bool MayAsk(int headRegard) => headRegard >= MinRegard;

        /// <summary>The colony asks by letter: they send one by their head's regard (none below 30, never certain): 0.6 at 30, 0.95 from 65.</summary>
        public static float SendChance(int regard) => regard < MinRegard ? 0f : Clamp(0.3f + regard / 100f, 0.1f, 0.95f);

        public static bool SendsWhenAsked(bool hostile, int regard, float roll) => !hostile && roll < SendChance(regard);

        /// <summary>The ward's age: a child of 9 to 12 where the game has children (Biotech), else a youth of 14 to 19.</summary>
        public static (float min, float max) WardAge(bool children) => children ? (9f, 12f) : (14f, 19f);

        /// <summary>
        /// The skill the colony is known for, to teach (its index in <paramref name="best"/>, the best level of each skill
        /// among its people, guardians-to-be; -1 when no one has 10 in anything): the highest level, and where two tie the
        /// first.
        /// </summary>
        public static int KnownFor(IList<int> best, IList<bool> barred = null)
        {
            int pick = -1, top = GuardianSkill - 1;
            if (best == null) return -1;
            for (int i = 0; i < best.Count; i++)
            {
                if (barred != null && i < barred.Count && barred[i]) continue;
                if (best[i] > top)
                {
                    top = best[i];
                    pick = i;
                }
            }
            return pick;
        }

        // ---------------- Lessons ----------------

        /// <summary>A lesson is an hour.</summary>
        public const int LessonTicks = 2500;

        /// <summary>Lessons are given by day, between these hours.</summary>
        public const int LessonFrom = 8, LessonTo = 20;

        /// <summary>A lesson hasn't been given today yet (by the local day's number), and it is the time of day for one.</summary>
        public static bool LessonDue(int lastDay, int today, int hour) => lastDay < today && hour >= LessonFrom && hour < LessonTo;

        /// <summary>
        /// Practice a tick of the lesson gives the ward, by how far the guardian leads them: a little at least, more the
        /// further ahead the guardian is (the game's own learning factors and passions apply on top).
        /// </summary>
        public static float LessonXpPerTick(int guardian, int ward) => Math.Max(0.06f, Math.Min(0.2f, 0.06f + 0.01f * (guardian - ward)));

        /// <summary>A child at the game's own lessons (Biotech) is counted taught for the day by this much practice in the skill.</summary>
        public const float ChildDayXp = 150f;

        // ---------------- How they are ----------------

        /// <summary>The first days they are settling in; after these, how they are is their mood alone.</summary>
        public const float SettleDays = 10f;

        /// <summary>How their ward is: their mood (0 to 1) and how long they have been here.</summary>
        public static WardMood Mood(float mood, float days)
        {
            if (mood < 0.3f) return WardMood.Homesick;
            if (days < SettleDays) return mood < 0.5f ? WardMood.Homesick : WardMood.Settling;
            if (mood >= 0.6f) return WardMood.Happy;
            return mood < 0.4f ? WardMood.Homesick : WardMood.Settling;
        }

        /// <summary>Hungry this many days in ten or fewer counts as fed.</summary>
        public static bool Fed(int hungryDays, int days) => days <= 0 || hungryDays <= Math.Max(1, days / 10);

        // ---------------- The year's end ----------------

        /// <summary>
        /// How the year went: taught (lessons on half the days or more, or a quarter for some credit), fed (hungry on a
        /// tenth of the days at most; hungry on more than a third counts against), unhurt (no new injuries; more than two
        /// counts against), content (their mood over the year).
        /// </summary>
        public static WardResult Year(int lessons, int days, int hungryDays, int hurt, float moodAverage)
        {
            float score = 0f;
            float taught = days <= 0 ? 0f : lessons / (float)days;
            if (taught >= 0.5f) score += 1f;
            else if (taught >= 0.25f) score += 0.5f;
            float hungry = days <= 0 ? 0f : hungryDays / (float)days;
            if (Fed(hungryDays, days)) score += 1f;
            else if (hungry > 0.35f) score -= 1f;
            else score += 0.25f;
            if (hurt <= 0) score += 1f;
            else if (hurt > 2) score -= 1f;
            if (moodAverage >= 0.55f) score += 1f;
            else if (moodAverage >= 0.4f) score += 0.5f;
            else if (moodAverage < 0.3f) score -= 1f;
            if (score >= 3.5f) return WardResult.Fine;
            if (score >= 2.5f) return WardResult.Good;
            return score >= 1f ? WardResult.Fair : WardResult.Poor;
        }

        /// <summary>Their head's regard (and the ward's kin's) when the ward comes home.</summary>
        public static int HomeRegard(WardResult r)
        {
            switch (r)
            {
                case WardResult.Fine: return 20;
                case WardResult.Good: return 12;
                case WardResult.Fair: return 4;
                default: return -10;
            }
        }

        /// <summary>Their people's goodwill when the ward comes home.</summary>
        public static int HomeGoodwill(WardResult r)
        {
            switch (r)
            {
                case WardResult.Fine: return 8;
                case WardResult.Good: return 5;
                case WardResult.Fair: return 2;
                default: return -5;
            }
        }

        /// <summary>The deed "fostered" by how the year went (12 or more is a favour owed: FavourMath.Earns); a poor year is "slighted".</summary>
        public static int FosteredWeight(WardResult r)
        {
            switch (r)
            {
                case WardResult.Fine: return 12;
                case WardResult.Good: return 8;
                case WardResult.Fair: return 4;
                default: return 0;
            }
        }

        /// <summary>The grown ward, a notable of theirs later on, still thinks this much better (or worse) of the colony.</summary>
        public static int GrownRegard(WardResult r)
        {
            switch (r)
            {
                case WardResult.Fine: return 25;
                case WardResult.Good: return 15;
                case WardResult.Fair: return 5;
                default: return -10;
            }
        }

        /// <summary>The ward is old enough to stand as their town's heir: 18.</summary>
        public const float HeirAge = 18f;

        // ---------------- Hurt or killed in the colony's care ----------------

        /// <summary>
        /// Their ward died in the colony's care: their head's regard falls a little where the colony sheltered them, much
        /// where it left them in the open, and most by a colonist's hand (a feud: a grudge of their people's).
        /// </summary>
        public static int DiedRegard(bool sheltered, bool byColony) => byColony ? -50 : sheltered ? -15 : -35;

        public static int DiedGoodwill(bool sheltered, bool byColony) => byColony ? -30 : sheltered ? -8 : -20;

        /// <summary>A ward's death at a colonist's hand is a grudge their people hold.</summary>
        public static bool Feud(bool byColony) => byColony;

        public const string FeudKey = "ward";

        // ================= Our ward =================

        /// <summary>A youth of the colony's may go: 10 to 19 where the game has children (Biotech), 14 to 19 otherwise.</summary>
        public static bool YouthMayGo(float age, bool children, bool baby) => !baby && age < 20f && age >= (children ? 10f : 14f);

        /// <summary>
        /// What their people are good at, by their ways, as skill def names: a tribe's beasts and plants, the middle ages'
        /// crafts and the sword, the machine age's building and medicine, the spacers' learning and medicine.
        /// </summary>
        public static string[] TheirSkills(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return new[] { "Animals", "Plants" };
                case WorldAge.Medieval: return new[] { "Crafting", "Melee" };
                case WorldAge.Industrial: return new[] { "Construction", "Medicine" };
                default: return new[] { "Intellectual", "Medicine" };
            }
        }

        /// <summary>One of their skills, by a roll.</summary>
        public static string TheirSkill(WorldAge age, float roll)
        {
            var all = TheirSkills(age);
            return all[Math.Min(all.Length - 1, Math.Max(0, (int)(roll * all.Length)))];
        }

        /// <summary>The practice our ward brings home: about a lesson's worth a day of the year spent there (none for a page).</summary>
        public static float AbroadXp(float days, bool page) => page ? 0f : Math.Max(0f, Math.Min(YearDays, days)) * 300f;

        /// <summary>Their letters come every ten days of the year.</summary>
        public const float LetterDays = 10f;

        public static bool LetterDue(float daysThere, int lettersSent) => daysThere >= (lettersSent + 1) * LetterDays;

        /// <summary>Each letter raises the regard of their town's notables: a little; a page in a titled household, more (regard only).</summary>
        public static int AbroadRegard(bool page) => page ? 6 : 3;

        /// <summary>The friend our ward makes among their notables thinks this much better of them.</summary>
        public const int FriendRegard = 25;

        /// <summary>The deed "taught" their people did the colony (theirs; 10 or more would be a debt the colony owes them).</summary>
        public const int TaughtWeight = 8;

        // ================= Hostages =================

        /// <summary>Adults only: children are never hostages.</summary>
        public const float HostageAge = 18f;

        public static bool MayBeHostage(float age, bool juvenile) => !juvenile && age >= HostageAge;

        /// <summary>
        /// Their hostage's age as they are made: grown (never under <see cref="HostageAge"/>), and old enough to have some
        /// standing among their people.
        /// </summary>
        public static (float min, float max) StandingAge => (20f, 50f);

        /// <summary>
        /// Which of what their people have their hostage is (the player's decision, 6 November 2026: anyone of standing
        /// among them, never by kinship): one of their traders or of their seasoned fighters, by a roll where they have
        /// both; one of their people where they have neither.
        /// </summary>
        public static HostageStanding Standing(bool traders, bool fighters, float roll) =>
            traders && fighters ? (roll < 0.5f ? HostageStanding.Trader : HostageStanding.Fighter)
            : traders ? HostageStanding.Trader : fighters ? HostageStanding.Fighter : HostageStanding.OneOfThem;

        /// <summary>
        /// A seasoned fighter of their war bands: a kind of the stronger half of those their war bands bring, by the game's
        /// combat power (at or above the middle one; the only one where there is one).
        /// </summary>
        public static bool Seasoned(float power, IList<float> powers)
        {
            if (powers == null || powers.Count == 0) return false;
            var sorted = new List<float>(powers);
            sorted.Sort();
            return power >= sorted[sorted.Count / 2];
        }

        /// <summary>The talk's tag for their hostage spoken to, by their standing (them:hostage:trader, :fighter), or null.</summary>
        public static string StandingTag(HostageStanding s) =>
            s == HostageStanding.Trader ? ThemHostageTag + ":trader" : s == HostageStanding.Fighter ? ThemHostageTag + ":fighter" : null;

        /// <summary>The standing's tags, for the talk's conditions (beside <see cref="Tags"/>).</summary>
        public static readonly string[] StandingTags = { ThemHostageTag + ":trader", ThemHostageTag + ":fighter" };

        /// <summary>
        /// A pact's roll with a hostage among its terms: their chance moves by the score shift over the pact's own curve (a
        /// score point is a sixtieth of the chance: PactMath.PactChance), so the roll moves the other way.
        /// </summary>
        public static float PactRoll(float roll, int scoreShift) => roll - scoreShift / 60f;

        /// <summary>A hostage of theirs makes a truce or pact harder for them to agree to; one of the colony's, easier.</summary>
        public const int TheirHostageScore = -10, OurHostageScore = 10;

        /// <summary>How a hostage's term ended, from the tie it guarantees.</summary>
        public enum HostageEnd
        {
            /// <summary>Still in force: the hostage stays.</summary>
            Holds,
            /// <summary>Kept or ended with nobody at fault: both go home.</summary>
            Home,
            /// <summary>Their side broke it: their hostage is the colony's to decide about, the colony's comes home.</summary>
            TheyBroke,
            /// <summary>The colony broke it: the colony's hostage is held by their people, theirs goes home.</summary>
            WeBroke
        }

        /// <summary>
        /// The end of a hostage's term from its tie: live, closed by its term, or broken (by which side). A defence pact's
        /// hostage is given for the pact's year: once it is out (<paramref name="yearOver"/>) they go home with the term
        /// kept, even when the pact runs on renewed (by their letter, or by a marriage alliance); a renewal may ask for a new
        /// hostage as its own term.
        /// </summary>
        public static HostageEnd EndOf(bool live, bool broken, bool brokenByThem, bool yearOver = false) =>
            live ? yearOver ? HostageEnd.Home : HostageEnd.Holds : !broken ? HostageEnd.Home : brokenByThem ? HostageEnd.TheyBroke : HostageEnd.WeBroke;

        /// <summary>A defence pact's hostage: the pact year they were given for (its last tick, WardStay.until) is out.</summary>
        public static bool YearOver(int termEnd, int now) => termEnd >= 0 && now >= termEnd;

        /// <summary>The tick a pact's year ends, from its terms' day (PactMath: the number of a pact is the day its year is out).</summary>
        public static int YearEndTick(float pactDay) => pactDay <= 0f ? -1 : (int)Math.Round(pactDay * DayTicks);

        /// <summary>A forfeit hostage of theirs would stay with the colony: content (mood 0.5 or more) after ten days here.</summary>
        public static bool WouldJoin(float mood, float days) => mood >= 0.5f && days >= SettleDays;

        /// <summary>Left unanswered this long, a forfeit hostage slips away home on their own.</summary>
        public const float ForfeitDays = 5f;

        /// <summary>The colony returns their hostage at the end of the term: the deed "hostageReturned", by how long they were kept.</summary>
        public static int ReturnedWeight(float days) => days >= 30f ? 8 : 6;

        // ================= Their kin at the gate =================

        /// <summary>Their kin wait at the gate this long for the ward to come out to them, then go home (the ward walks out alone).</summary>
        public const float KinWaitHours = 30f;

        /// <summary>The ward or hostage is due one to two days after it is agreed.</summary>
        public static float ComeDays(float roll) => 1f + Clamp(roll, 0f, 1f);

        // ================= The letters' topics =================

        /// <summary>Asking for their ward ("ward:ask"), offering a youth of the colony's ("ward:send").</summary>
        public const string AskTopic = "ward:ask", SendTopic = "ward:send";

        /// <summary>What is asked: the skill the colony teaches ("skill:Crafting"), the youth the colony sends ("youth:123").</summary>
        public const string SkillPrefix = "skill:", YouthPrefix = "youth:";

        public static string SkillCode(string defName) => SkillPrefix + defName;

        public static string YouthCode(int id) => YouthPrefix + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool ParseSkill(string code, out string defName)
        {
            defName = null;
            if (code == null || !code.StartsWith(SkillPrefix, StringComparison.Ordinal) || code.Length <= SkillPrefix.Length) return false;
            defName = code.Substring(SkillPrefix.Length);
            return defName.IndexOf(':') < 0;
        }

        public static bool ParseYouth(string code, out int id)
        {
            id = -1;
            return code != null && code.StartsWith(YouthPrefix, StringComparison.Ordinal)
                   && int.TryParse(code.Substring(YouthPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id);
        }

        /// <summary>A hostage asked as a defence pact's term: "hostage:Theirs" or "hostage:Ours:123" (the colonist).</summary>
        public const string HostagePrefix = "hostage:";

        public static string HostageCode(bool ours, int id = -1) =>
            HostagePrefix + (ours ? "Ours:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Theirs");

        public static bool ParseHostage(string code, out bool ours, out int id)
        {
            ours = false;
            id = -1;
            if (code == null || !code.StartsWith(HostagePrefix, StringComparison.Ordinal)) return false;
            var rest = code.Substring(HostagePrefix.Length);
            if (rest == "Theirs") return true;
            if (!rest.StartsWith("Ours:", StringComparison.Ordinal)) return false;
            ours = true;
            return int.TryParse(rest.Substring(5), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id);
        }

        // ================= Facts =================

        /// <summary>
        /// A ward or hostage between the colony and the scene's people: tie:ward (:theirs their youth at the colony, :ours the
        /// colony's with them), tie:hostage (:theirs, :ours, :due a hostage forfeit or held), them:ward and them:hostage (the
        /// one spoken to is that ward or hostage: a lodger at the colony is one of the outsiders the colonists talk with),
        /// them:fostered (the one spoken to was the colony's ward once); {WARD}, {HOSTAGE}, {GUARDIAN} (their ward's teacher).
        /// </summary>
        public const string WardTag = "tie:ward", HostageTag = "tie:hostage", ThemWardTag = "them:ward", ThemHostageTag = "them:hostage", FosteredTag = "them:fostered";

        public const string WardKey = "WARD", HostageKey = "HOSTAGE", GuardianKey = "GUARDIAN";

        public static readonly string[] Tags =
        {
            WardTag, WardTag + ":theirs", WardTag + ":ours", HostageTag, HostageTag + ":theirs", HostageTag + ":ours", HostageTag + ":due", ThemWardTag, ThemHostageTag,
            FosteredTag, MoodTag(WardMood.Homesick), MoodTag(WardMood.Settling), MoodTag(WardMood.Happy)
        };

        /// <summary>How their ward spoken to is: them:ward:homesick, them:ward:settling, them:ward:happy.</summary>
        public static string MoodTag(WardMood m) => ThemWardTag + ":" + m.ToString().ToLowerInvariant();

        /// <summary>The facts' tags of a ward or hostage: its kind's tag, which side it is of, and (a hostage) whether it is forfeit or held.</summary>
        public static IEnumerable<string> TagsOf(WardKind kind, WardStage stage)
        {
            bool hostage = kind == WardKind.TheirHostage || kind == WardKind.OurHostage;
            bool theirs = kind == WardKind.Theirs || kind == WardKind.TheirHostage;
            string tag = hostage ? HostageTag : WardTag;
            yield return tag;
            yield return tag + (theirs ? ":theirs" : ":ours");
            if (hostage && (stage == WardStage.Forfeit || stage == WardStage.Held)) yield return tag + ":due";
        }

        public static bool IsHostage(WardKind k) => k == WardKind.TheirHostage || k == WardKind.OurHostage;

        public static bool IsTheirs(WardKind k) => k == WardKind.Theirs || k == WardKind.TheirHostage;

        /// <summary>A record of this kind may be added to a home: one ward each way; one hostage each way per tie; four in all.</summary>
        public static bool Fits(IList<(WardKind kind, int guarantees)> there, WardKind kind, int guarantees)
        {
            if (there == null) return true;
            if (there.Count >= MaxPerHome) return false;
            foreach (var (k, g) in there)
            {
                if (k != kind) continue;
                if (!IsHostage(kind) || g == guarantees) return false;
            }
            return true;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
