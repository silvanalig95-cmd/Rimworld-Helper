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

    /// <summary>One need of the heart as it is now, with the main reason it's high or low.</summary>
    public struct NeedNow
    {
        public float value;
        public string why;
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

        public StrainBand Band => HeartMath.BandFor(strain);

        public void EnsureLists()
        {
            Pad(recentBuild, HeartMath.SourceCount);
            Pad(recentRelief, HeartMath.ReliefCount);
            Pad(today, HeartMath.SourceCount);
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (recentBuild == null) recentBuild = new List<float>();
                if (recentRelief == null) recentRelief = new List<float>();
                if (today == null) today = new List<float>();
                strain = HeartMath.Clamp(strain);
                EnsureLists();
            }
        }
    }
}
