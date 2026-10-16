using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How an answered dilemma went, for the "How it went" column of Council → Dilemmas.</summary>
    public enum DilemmaWent
    {
        /// <summary>It happened as the answer said.</summary>
        AsAnswered,
        /// <summary>The answer's gamble went wrong (its risk came true).</summary>
        WentWrong,
        /// <summary>What the answer set going on the map (a feast, a visit) is still waiting for its moment.</summary>
        Waiting,
        /// <summary>It is happening now.</summary>
        UnderWay,
        /// <summary>What it set going took place.</summary>
        TookPlace,
        /// <summary>What it set going never came about (its moods were given anyway).</summary>
        FellThrough
    }

    /// <summary>One answered dilemma: the question, the answer, what came of it, and what it set going on the map.</summary>
    public class DilemmaRecord : IExposable
    {
        public string defName;
        public string question;
        public string answer;
        /// <summary>The follow-up text the player was told.</summary>
        public string outcome;
        /// <summary>Who it was about (by name: they may be long gone).</summary>
        public string who;
        public int tick;
        public bool risk;
        public bool byDefault;
        /// <summary>Play-outs (Answers play out) it set going that are still being watched, by id.</summary>
        public List<int> plays = new List<int>();
        /// <summary>Of those, the ones seen under way, so their end can be told from one that never came about.</summary>
        public List<int> started = new List<int>();
        public int tookPlace;
        public int fellThrough;
        /// <summary>The first thing it set going, for the column ("the victory feast").</summary>
        public string playLabel;

        public DilemmaWent Went
        {
            get
            {
                if (risk) return DilemmaWent.WentWrong;
                if (plays.Count > 0) return started.Count > 0 ? DilemmaWent.UnderWay : DilemmaWent.Waiting;
                if (tookPlace > 0) return DilemmaWent.TookPlace;
                if (fellThrough > 0) return DilemmaWent.FellThrough;
                return DilemmaWent.AsAnswered;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref question, "question");
            Scribe_Values.Look(ref answer, "answer");
            Scribe_Values.Look(ref outcome, "outcome");
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref risk, "risk");
            Scribe_Values.Look(ref byDefault, "byDefault");
            Scribe_Collections.Look(ref plays, "plays", LookMode.Value);
            Scribe_Collections.Look(ref started, "started", LookMode.Value);
            Scribe_Values.Look(ref tookPlace, "tookPlace");
            Scribe_Values.Look(ref fellThrough, "fellThrough");
            Scribe_Values.Look(ref playLabel, "playLabel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plays ??= new List<int>();
                started ??= new List<int>();
            }
        }
    }

    /// <summary>
    /// The council's answered dilemmas, newest last, kept with the game (in <see cref="WeekMemory"/>) for Council →
    /// Dilemmas. Each answer is written down as it's given (Dilemmas.cs calls <see cref="Record"/>), and what it set going
    /// on the map is watched until it has taken place or fallen through: a light look every 250 ticks, and only while
    /// something is being watched.
    /// </summary>
    public static class DilemmaLog
    {
        public const int MaxRecords = 60;

        /// <summary>The tick of the 250-tick cycle the watching runs on (clear of Sched's passes).</summary>
        public const int WatchTick = Sched.RareWeekMemory;

        public static List<DilemmaRecord> Records => WeekMemory.Instance?.dilemmas;

        /// <summary>An answer was given (or the time ran out). Never throws: the dilemma itself matters more than its record.</summary>
        public static void Record(DilemmaContext ctx, DilemmaOption option, string told, bool risk, bool byDefault)
        {
            try
            {
                var list = Records;
                if (list == null || ctx?.def == null || option == null) return;
                int now = Find.TickManager.TicksGame;
                var r = new DilemmaRecord
                {
                    defName = ctx.def.defName,
                    question = ctx.def.LabelCap,
                    answer = ctx.Format(option.label),
                    outcome = told,
                    who = ctx.a?.LabelShort,
                    tick = now,
                    risk = risk,
                    byDefault = byDefault
                };
                // What the answer set going on the map this very tick.
                var plays = PlayOutTracker.Instance?.plays;
                if (plays != null)
                    foreach (var p in plays)
                    {
                        if (p == null || p.created != now || p.state == PlayState.Done || Claimed(list, p.id)) continue;
                        r.plays.Add(p.id);
                        if (r.playLabel == null && !p.label.NullOrEmpty()) r.playLabel = p.label;
                    }
                list.Add(r);
                if (list.Count > MaxRecords) list.RemoveRange(0, list.Count - MaxRecords);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dilemma record", e);
            }
        }

        private static bool Claimed(List<DilemmaRecord> list, int id)
        {
            for (int i = list.Count - 1; i >= 0 && i >= list.Count - 5; i--)
                if (list[i].plays.Contains(id))
                    return true;
            return false;
        }

        /// <summary>Follows what the answers set going: under way, taken place or fallen through.</summary>
        public static void Watch(List<DilemmaRecord> list)
        {
            if (list == null) return;
            var tracker = PlayOutTracker.Instance;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.plays.Count == 0) continue;
                for (int j = r.plays.Count - 1; j >= 0; j--)
                {
                    int id = r.plays[j];
                    var p = tracker?.Get(id);
                    bool running = p != null && (p.state == PlayState.Running || p.started >= 0);
                    if (running && !r.started.Contains(id)) r.started.Add(id);
                    if (p != null && p.state != PlayState.Done) continue;
                    // Gone or done: it took place if it was ever seen under way.
                    if (r.started.Remove(id) || running) r.tookPlace++;
                    else r.fellThrough++;
                    r.plays.RemoveAt(j);
                }
            }
        }

        public static bool AnyWatched(List<DilemmaRecord> list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].plays.Count > 0)
                    return true;
            return false;
        }

        public static string WentLabel(DilemmaRecord r) => ("CC_Dil_Went_" + r.Went).Translate();

        public static UnityEngine.Color WentColor(DilemmaWent w)
        {
            switch (w)
            {
                case DilemmaWent.WentWrong: return UiKit.Bad;
                case DilemmaWent.FellThrough: return UiKit.Warn;
                case DilemmaWent.Waiting:
                case DilemmaWent.UnderWay: return UiKit.Info;
                default: return UiKit.Good;
            }
        }
    }
}
