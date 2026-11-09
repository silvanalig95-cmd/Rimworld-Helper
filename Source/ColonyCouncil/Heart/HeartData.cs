using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A feeling as it is now: how strong, since when, and what fed it (worked out hourly, not saved).</summary>
    public class FeelingNow
    {
        public FeelingDef def;
        public float intensity;
        public int since;
        /// <summary>The strongest causes, biggest first ("Ana died", "hostiles on the map").</summary>
        public List<string> causes = new List<string>(3);
        public List<float> causeAmounts = new List<float>(3);
        /// <summary>The person it's most about, if any: who they're angry at, grateful to, in love with.</summary>
        public Pawn target;
        public float targetAmount;
        /// <summary>Some of it comes from the council's own doings (a wish granted, a promise kept).</summary>
        public bool fromCouncil;

        public float Share => HeartMath.Share(intensity);

        public void AddCause(string cause, float amount)
        {
            if (cause.NullOrEmpty() || amount <= 0f) return;
            int existing = causes.IndexOf(cause);
            if (existing >= 0)
            {
                causeAmounts[existing] += amount;
                Sort();
                return;
            }
            if (causes.Count < 3)
            {
                causes.Add(cause);
                causeAmounts.Add(amount);
                Sort();
                return;
            }
            if (amount <= causeAmounts[2]) return;
            causes[2] = cause;
            causeAmounts[2] = amount;
            Sort();
        }

        private void Sort()
        {
            for (int i = 1; i < causes.Count; i++)
                for (int j = i; j > 0 && causeAmounts[j] > causeAmounts[j - 1]; j--)
                {
                    (causes[j], causes[j - 1]) = (causes[j - 1], causes[j]);
                    (causeAmounts[j], causeAmounts[j - 1]) = (causeAmounts[j - 1], causeAmounts[j]);
                }
        }
    }

    /// <summary>
    /// One thing that raises or holds down a need of the heart this hour: how much (0 for something missing that only
    /// explains a low value, such as "no friends here"), what it is, and what would help when it holds the need down.
    /// </summary>
    public struct NeedPart
    {
        public float amount;
        public string text;
        /// <summary>Whether <see cref="amount"/> really moved the value (false: a note on what's missing).</summary>
        public bool counted;
        /// <summary>A Keyed string saying what would help, for what holds it down; else null.</summary>
        public string help;
        /// <summary>An argument for <see cref="help"/> (a decree's name), or null.</summary>
        public string helpArg;
    }

    /// <summary>One need of the heart as it is now, with the main reason it's high or low and everything that moved it.</summary>
    public struct NeedNow
    {
        public float value;
        public string why;
        /// <summary>What raised it and what held it down this hour, biggest first (worked out hourly, not saved).</summary>
        public NeedPart[] parts;
    }

    /// <summary>
    /// One colonist's heart: their strain, what built it and drained it lately, and since when each feeling has been
    /// there. Feelings and needs themselves are worked out each hour and not saved; only what can't be worked out again
    /// is (strain, recent totals, the day's caps, episode times).
    /// </summary>
    public class PawnHeart : IExposable
    {
        public float strain;
        /// <summary>Strain built and drained in the last few days, by source (halving in about two days).</summary>
        public List<float> recentBuild = new List<float>();
        public List<float> recentRelief = new List<float>();
        /// <summary>Strain already built today, by source, and in all (for the daily caps).</summary>
        public List<float> today = new List<float>();
        public float todayTotal;
        /// <summary>Relief from gatherings and from good talks already had today (each capped per day).</summary>
        public float todayGathering;
        public float todayFriends;
        public int day = -1;
        public int lastEvalTick = -1;
        public int lastEpisodeTick = -999999;
        /// <summary>Hours of very low mood lately (fades), for despair.</summary>
        public float lowMoodHours;
        /// <summary>Strain a day ago, for hope (strain falling).</summary>
        public float strainDayAgo;
        public int strainDayAgoTick = -1;
        /// <summary>The last mental break they had (not an episode), for shame after the embarrassing ones.</summary>
        public string lastBreak;
        public int lastBreakTick = -999999;
        public int lastInspirationTick = -999999;
        public int lastTrustTick = -999999;
        /// <summary>Since when each feeling has been felt (defName and tick, in two lists).</summary>
        public List<string> feelingSinceKeys = new List<string>();
        public List<int> feelingSinceTicks = new List<int>();
        /// <summary>
        /// For each need of the heart, the hours it has been badly lacking lately (HeartNeedMath.LackHours): after
        /// about three days, it adds a little strain.
        /// </summary>
        public List<float> needLack = new List<float>();
        /// <summary>
        /// The last act against (or with) their nature they felt (Diplomacy/Nature.cs): when (-1: none; one a day), which
        /// (NatureAct), whether it eased them, and its words for the heart's page ("swore the colony's oath to the Lenlia,
        /// against her pride"). Values, not a list: the heart's record gains four fields.
        /// </summary>
        public int natureTick = -1;
        public int natureAct = -1;
        public bool natureEased;
        public string natureWhy;
        /// <summary>Relief from small joys had today (Moods/SmallMoments.cs: their daily cap, SmallMomentMath.JoyReliefCap).</summary>
        public float todayJoys;
        /// <summary>
        /// What the small letdowns and joys were lately, in a few words for the heart's page ("the same meal, my idea turned
        /// down"): the biggest of them, kept as words like <see cref="natureWhy"/>.
        /// </summary>
        public string letdownsWhy, joysWhy;

        // ---------------- The good side (Heart/GoodHeart.cs) ----------------

        /// <summary>The heart's good side (0 to 100): in good heart from 45, in high spirits from 65. It grows beside strain, slower the more there is.</summary>
        public float good;
        /// <summary>What filled it already today, by source (GoodSource) and in all, for its daily caps.</summary>
        public List<float> goodToday = new List<float>();
        public float goodTodayTotal;
        /// <summary>What filled it lately, by source, and what spent it, by strain's sources (halving in about two days).</summary>
        public List<float> goodRecent = new List<float>();
        public List<float> goodSpent = new List<float>();
        /// <summary>How high it has stood lately (halving in about three days): someone in good heart lately recovers sooner.</summary>
        public float goodLately;
        /// <summary>The last look at their deeds (-1: none yet, the first look only takes note), and at their life goal.</summary>
        public int deedTick = -1, goalTick = -1;
        public float goalProgress;
        /// <summary>The good side's last release (GoodRelease), and what it was in words ("inspired", "a kind word for Ana").</summary>
        public int lastReleaseTick = -999999;
        public string releaseWhy;

        // ---------------- The heart, felt (Heart/HeartFelt.cs) ----------------

        /// <summary>
        /// The lasting weight of heavy things (a loss, being downed): rest can't take strain below it (with <see cref="held"/>);
        /// it lifts slowly until <see cref="weightUntil"/>, then faster. Its words ("losing Ana"), the kind of the heaviest
        /// (WeightKind) and whom it is about (a name).
        /// </summary>
        public float weight;
        public int weightUntil = -1;
        public string weightWhy, weightAbout;
        public int weightKind;
        /// <summary>
        /// The weight of what lasts now (a sickness in the house, a fight going badly): lifted to it each hour while it lasts,
        /// and lifting after, slowly until <see cref="heldUntil"/>. Its words, kind and whom or what it is about.
        /// </summary>
        public float held;
        public int heldUntil = -1;
        public string heldWhy, heldAbout;
        public int heldKind;
        /// <summary>The bands last told the player in a message (HeartFeltMath.Tell): -1, not looked at yet.</summary>
        public int toldStrain = -1, toldGood = -1;

        // ---------------- The heart, made to count (Heart/HeartTimeOff.cs) ----------------

        /// <summary>
        /// Time off work under way (TimeOffKind: a day off the player gave, a bad afternoon): when it started and when it ends
        /// (ticks; -1 none), and their old timetable for the hours it changed ("9=Work", "7=Sleep>Sleep" for a lie-in), put
        /// back when it ends. <see cref="offWhy"/> is what wore them down to it, in a few words, for the page and the message.
        /// </summary>
        public int offKind, offStart = -1, offUntil = -1;
        public List<string> offHours = new List<string>();
        public string offWhy;
        /// <summary>The last day off they were given and the last bad afternoon they had (for their cooldowns).</summary>
        public int lastDayOffTick = -999999, lastBadDayTick = -999999;

        // ---------------- Worked out each hour, not saved ----------------

        [Unsaved] public List<FeelingNow> feelings = new List<FeelingNow>();
        [Unsaved] public NeedNow[] needs;
        [Unsaved] public bool evaluated;
        /// <summary>The UI already tried to work this heart out on the spot (Heart.ForView), so it doesn't retry every frame.</summary>
        [Unsaved] public bool viewTried;
        /// <summary>The inspector line, built when the heart changes (never per frame).</summary>
        [Unsaved] public string inspectLine;
        /// <summary>RimHUD's values, built at the same time.</summary>
        [Unsaved] public string hudStrain, hudFeelings, hudNeeds;
        /// <summary>The work type of each of the last hours they were working (0 = not working), for variety.</summary>
        [Unsaved] public int[] workSamples = new int[12];
        [Unsaved] public int workSampleIndex;
        /// <summary>The mental state they were in last hour, to notice when a break ends.</summary>
        [Unsaved] public string inBreak;
        /// <summary>Strain this hour, by source, for the tooltip ("+2 fighting this hour").</summary>
        [Unsaved] public float lastHourBuild, lastHourRelief;
        /// <summary>When each kind of bad night was last counted, so a night renewed by waking again counts once.</summary>
        [Unsaved] public Dictionary<ThoughtDef, int> badNights;

        /// <summary>A lasting letdown or joy as last seen: its stage, and when it last came as a moment.</summary>
        public struct LastingSeen
        {
            public int stage, tick;
        }

        /// <summary>The lasting letdowns and joys holding for them, as last seen (Moods/SmallMoments.cs); worked out again after a load.</summary>
        [Unsaved] public Dictionary<ThoughtDef, LastingSeen> lasting;
        /// <summary>The hour has looked at the lasting ones since the game loaded (what held already then brings no moment).</summary>
        [Unsaved] public bool lastingLooked;
        /// <summary>What small letdowns and joys built and eased lately, by their words (halving in about two days).</summary>
        [Unsaved] public Dictionary<string, float> wore, eased;
        /// <summary>
        /// The heart on the map (Heart/HeartFelt.cs, HeartActs.cs): whether they were downed at the last look (a downing
        /// weighs once), the bands and the strong feeling at the last look (for a line when the band turns and a sign when a
        /// feeling grows strong), a line waiting to be said, and when the last line and sign came.
        /// </summary>
        [Unsaved] public bool wasDowned;
        [Unsaved] public int lastBand = -1, lastGood = -1;
        [Unsaved] public FeelingDef lastStrong;
        [Unsaved] public string turnLine;
        [Unsaved] public int turnLineUntil = -1, lastLineTick = -999999, lastSignTick = -999999;
        /// <summary>Whom a loss already weighed for lately (by id, and when), so the memories of one death weigh once.</summary>
        [Unsaved] public Dictionary<int, (int tick, float weight)> weighedFor;
        /// <summary>
        /// The heart, made to count: what would help them now (Heart/HeartAdvice.cs), built at most once a game hour for the
        /// page, the card and the bar; when it was built; the last act of their time off; who close wore on or lifted them
        /// this hour, by name, for the page.
        /// </summary>
        [Unsaved] public List<HeartAdvice.Item> advice;
        [Unsaved] public int adviceTick = -999999, offActTick = -999999;
        [Unsaved] public string spreadFrom, liftedBy;

        /// <summary>Time off work of this kind is under way.</summary>
        public bool Off(TimeOffKind kind) => offKind == (int)kind && offUntil >= 0;

        /// <summary>Any time off work under way.</summary>
        public bool AnyOff => offKind != 0 && offUntil >= 0;

        /// <summary>A small letdown wore on them, or a joy eased them, by this much: the heart's page names the biggest few.</summary>
        public void Note(bool joy, string why, float amount)
        {
            if (why.NullOrEmpty() || amount <= 0f) return;
            var d = joy ? eased ??= new Dictionary<string, float>() : wore ??= new Dictionary<string, float>();
            string key = why.ToLowerInvariant();
            d[key] = (d.TryGetValue(key, out float had) ? had : 0f) + amount;
            Words(joy);
        }

        /// <summary>The hour's fading of what wore and eased (as the recent totals fade).</summary>
        public void FadeNotes(float decay)
        {
            Fade(wore, decay);
            Fade(eased, decay);
        }

        private static readonly List<string> keysTmp = new List<string>();

        private static void Fade(Dictionary<string, float> d, float decay)
        {
            if (d == null || d.Count == 0) return;
            keysTmp.Clear();
            keysTmp.AddRange(d.Keys);
            foreach (var k in keysTmp)
            {
                float v = d[k] * decay;
                if (v < 0.2f) d.Remove(k);
                else d[k] = v;
            }
            keysTmp.Clear();
        }

        private void Words(bool joy)
        {
            var d = joy ? eased : wore;
            if (d == null) return;
            var top = new List<KeyValuePair<string, float>>(d);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var words = new List<string>();
            for (int i = 0; i < top.Count && words.Count < 3; i++) words.Add(top[i].Key);
            string text = words.Count == 0 ? null : TextUtil.JoinAnd(words);
            if (joy) joysWhy = text;
            else letdownsWhy = text;
        }

        public StrainBand Band => HeartMath.BandFor(strain);

        public void EnsureLists()
        {
            Pad(recentBuild, HeartMath.SourceCount);
            Pad(recentRelief, HeartMath.ReliefCount);
            Pad(today, HeartMath.SourceCount);
            goodToday ??= new List<float>();
            goodRecent ??= new List<float>();
            goodSpent ??= new List<float>();
            Pad(goodToday, HeartMath.GoodSourceCount);
            Pad(goodRecent, HeartMath.GoodSourceCount);
            Pad(goodSpent, HeartMath.SourceCount);
            if (needLack == null) needLack = new List<float>();
            Pad(needLack, NeedCount);
            if (feelingSinceKeys == null || feelingSinceTicks == null || feelingSinceKeys.Count != feelingSinceTicks.Count)
            {
                feelingSinceKeys = new List<string>();
                feelingSinceTicks = new List<int>();
            }
            if (feelings == null) feelings = new List<FeelingNow>();
            if (workSamples == null) workSamples = new int[12];
        }

        private static void Pad(List<float> list, int n)
        {
            while (list.Count < n) list.Add(0f);
            while (list.Count > n) list.RemoveAt(list.Count - 1);
        }

        /// <summary>The four needs of the heart (HeartNeed).</summary>
        public const int NeedCount = 4;

        /// <summary>Hours this need has been badly lacking lately (see <see cref="needLack"/>).</summary>
        public float LackHours(HeartNeed n) => needLack != null && (int)n < needLack.Count ? needLack[(int)n] : 0f;

        public float Recent(StrainSource s) => (int)s < recentBuild.Count ? recentBuild[(int)s] : 0f;

        public float RecentRelief(StrainRelief r) => (int)r < recentRelief.Count ? recentRelief[(int)r] : 0f;

        public int SinceOf(FeelingDef def, int fallback)
        {
            int i = feelingSinceKeys.IndexOf(def.defName);
            return i >= 0 ? feelingSinceTicks[i] : fallback;
        }

        public void SetSince(FeelingDef def, int tick)
        {
            int i = feelingSinceKeys.IndexOf(def.defName);
            if (i >= 0) feelingSinceTicks[i] = tick;
            else
            {
                feelingSinceKeys.Add(def.defName);
                feelingSinceTicks.Add(tick);
            }
        }

        public void ClearSince(FeelingDef def)
        {
            int i = feelingSinceKeys.IndexOf(def.defName);
            if (i < 0) return;
            feelingSinceKeys.RemoveAt(i);
            feelingSinceTicks.RemoveAt(i);
        }

        public FeelingNow Feeling(FeelingDef def)
        {
            for (int i = 0; i < feelings.Count; i++)
                if (feelings[i].def == def) return feelings[i];
            return null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref strain, "strain");
            Scribe_Collections.Look(ref recentBuild, "recentBuild", LookMode.Value);
            Scribe_Collections.Look(ref recentRelief, "recentRelief", LookMode.Value);
            Scribe_Collections.Look(ref today, "today", LookMode.Value);
            Scribe_Values.Look(ref todayTotal, "todayTotal");
            Scribe_Values.Look(ref todayGathering, "todayGathering");
            Scribe_Values.Look(ref todayFriends, "todayFriends");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref lastEvalTick, "lastEvalTick", -1);
            Scribe_Values.Look(ref lastEpisodeTick, "lastEpisodeTick", -999999);
            Scribe_Values.Look(ref lowMoodHours, "lowMoodHours");
            Scribe_Values.Look(ref strainDayAgo, "strainDayAgo");
            Scribe_Values.Look(ref strainDayAgoTick, "strainDayAgoTick", -1);
            Scribe_Values.Look(ref lastBreak, "lastBreak");
            Scribe_Values.Look(ref lastBreakTick, "lastBreakTick", -999999);
            Scribe_Values.Look(ref lastInspirationTick, "lastInspirationTick", -999999);
            Scribe_Values.Look(ref lastTrustTick, "lastTrustTick", -999999);
            Scribe_Collections.Look(ref feelingSinceKeys, "feelingSinceKeys", LookMode.Value);
            Scribe_Collections.Look(ref feelingSinceTicks, "feelingSinceTicks", LookMode.Value);
            Scribe_Collections.Look(ref needLack, "needLack", LookMode.Value);
            Scribe_Values.Look(ref natureTick, "natureTick", -1);
            Scribe_Values.Look(ref natureAct, "natureAct", -1);
            Scribe_Values.Look(ref natureEased, "natureEased");
            Scribe_Values.Look(ref natureWhy, "natureWhy");
            Scribe_Values.Look(ref todayJoys, "todayJoys");
            Scribe_Values.Look(ref letdownsWhy, "letdownsWhy");
            Scribe_Values.Look(ref joysWhy, "joysWhy");
            // The good side: an older save has none of it, and starts at nothing.
            Scribe_Values.Look(ref good, "good");
            Scribe_Collections.Look(ref goodToday, "goodToday", LookMode.Value);
            Scribe_Values.Look(ref goodTodayTotal, "goodTodayTotal");
            Scribe_Collections.Look(ref goodRecent, "goodRecent", LookMode.Value);
            Scribe_Collections.Look(ref goodSpent, "goodSpent", LookMode.Value);
            Scribe_Values.Look(ref goodLately, "goodLately");
            Scribe_Values.Look(ref deedTick, "deedTick", -1);
            Scribe_Values.Look(ref goalTick, "goalTick", -1);
            Scribe_Values.Look(ref goalProgress, "goalProgress");
            Scribe_Values.Look(ref lastReleaseTick, "lastReleaseTick", -999999);
            Scribe_Values.Look(ref releaseWhy, "releaseWhy");
            // The heart, felt: an older save has no weight, and its bands are taken note of at the first look.
            Scribe_Values.Look(ref weight, "weight");
            Scribe_Values.Look(ref weightUntil, "weightUntil", -1);
            Scribe_Values.Look(ref weightWhy, "weightWhy");
            Scribe_Values.Look(ref weightAbout, "weightAbout");
            Scribe_Values.Look(ref weightKind, "weightKind");
            Scribe_Values.Look(ref held, "held");
            Scribe_Values.Look(ref heldUntil, "heldUntil", -1);
            Scribe_Values.Look(ref heldWhy, "heldWhy");
            Scribe_Values.Look(ref heldAbout, "heldAbout");
            Scribe_Values.Look(ref heldKind, "heldKind");
            Scribe_Values.Look(ref toldStrain, "toldStrain", -1);
            Scribe_Values.Look(ref toldGood, "toldGood", -1);
            // The heart, made to count: an older save has no time off under way, and every cooldown is past.
            Scribe_Values.Look(ref offKind, "offKind");
            Scribe_Values.Look(ref offStart, "offStart", -1);
            Scribe_Values.Look(ref offUntil, "offUntil", -1);
            Scribe_Collections.Look(ref offHours, "offHours", LookMode.Value);
            Scribe_Values.Look(ref offWhy, "offWhy");
            Scribe_Values.Look(ref lastDayOffTick, "lastDayOffTick", -999999);
            Scribe_Values.Look(ref lastBadDayTick, "lastBadDayTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                good = HeartMath.Clamp(good);
                weight = HeartMath.Clamp(weight);
                held = HeartMath.Clamp(held);
                // Saves from before the needs could wear on anyone have none: nobody has been lacking anything yet.
                if (needLack == null) needLack = new List<float>();
                if (recentBuild == null) recentBuild = new List<float>();
                if (recentRelief == null) recentRelief = new List<float>();
                if (today == null) today = new List<float>();
                offHours ??= new List<string>();
                strain = HeartMath.Clamp(strain);
                EnsureLists();
            }
        }
    }
}
