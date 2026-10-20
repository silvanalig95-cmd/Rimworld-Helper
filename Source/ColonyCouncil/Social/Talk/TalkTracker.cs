using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Something that happened that people will talk about for a few days: a raid, a death, a wedding, a decree.</summary>
    public class TalkNote : IExposable
    {
        /// <summary>The TopicDef's defName.</summary>
        public string topic;
        public int tick;
        /// <summary>Who or what it was about, as the lines name it (a people, the dead, the newlyweds).</summary>
        public string subject;
        public int subjectId = -1;
        /// <summary>A tag its lines can be written for (won, lost, fallen, mechs, wedding, binge...).</summary>
        public string tag;

        public void ExposeData()
        {
            Scribe_Values.Look(ref topic, "topic");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref subject, "subject");
            Scribe_Values.Look(ref subjectId, "subjectId", -1);
            Scribe_Values.Look(ref tag, "tag");
        }
    }

    /// <summary>
    /// Conversations' own clock and memory (Social/Talk/). It keeps the news people talk about (<see cref="TalkNote"/>,
    /// at most <see cref="MaxNotes"/>, a month at most), plays out the lines of conversations under way (every 15 ticks,
    /// only while one is: <see cref="ExchangeQueue{T}"/>, not saved), and once an hour looks for what people go through
    /// together (<see cref="TalkHooks.Hourly"/>), and once a day lets views drift with what's happening. Who talked to
    /// whom lately isn't saved either: after a load, anyone may talk again.
    /// </summary>
    public class TalkTracker : GameComponent
    {
        public List<TalkNote> notes = new List<TalkNote>();

        public const int MaxNotes = 40;
        public const int NoteDays = 30;
        /// <summary>The lines of a talk under way are looked at every this many ticks, at this tick of the cycle.</summary>
        public const int LinePeriod = 15, LineOffset = 7;

        [Unsaved] public readonly ExchangeQueue<Pawn> exchanges = new ExchangeQueue<Pawn>(12);
        [Unsaved] private readonly Dictionary<int, int> lastStarted = new Dictionary<int, int>();
        [Unsaved] private readonly Dictionary<long, int> lastPair = new Dictionary<long, int>();
        [Unsaved] private readonly Dictionary<(long, ushort), int> lastTopic = new Dictionary<(long, ushort), int>();
        /// <summary>The last thing each pair argued about (a topic's "about"), for the memory of making up.</summary>
        [Unsaved] public readonly Dictionary<long, string> lastArgued = new Dictionary<long, string>();
        /// <summary>The last talk that moved each person's view of a topic (key: id|topic), for the character page.</summary>
        [Unsaved] public readonly Dictionary<string, (string with, int tick)> movedBy = new Dictionary<string, (string, int)>();

        private static TalkTracker cached;
        private static Game cachedGame;

        public static TalkTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<TalkTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public TalkTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Reset();
        }

        private void Reset()
        {
            exchanges.Clear();
            lastStarted.Clear();
            lastPair.Clear();
            lastTopic.Clear();
            lastArgued.Clear();
            movedBy.Clear();
            hotTick = -1;
            TalkScene.Reset();
            TalkHooks.Reset();
            Talk.Reset();
            SocialPace.Reset();
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (exchanges.Count > 0 && Sched.Every(t, LinePeriod, LineOffset))
            {
                // Social life turned off mid-talk: the rest of it isn't said.
                if (!Persona.On) exchanges.Clear();
                else Talk.Step(this, t);
            }
            if (!Sched.Hourly(t, Sched.Talk)) return;
            if (!Persona.On) return;
            try
            {
                TalkHooks.Hourly(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk: the hour", e);
            }
            if (!Sched.FirstHourOfDay(t)) return;
            try
            {
                Daily();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk: the day", e);
            }
        }

        private void Daily()
        {
            int now = Find.TickManager.TicksGame;
            notes.RemoveAll(n => n == null || now - n.tick > NoteDays * GenDate.TicksPerDay || n.tick > now);
            Talk.DailyDrift();
            TalkHooks.Daily(this);
            if (lastTopic.Count > 2000) lastTopic.Clear();
            if (movedBy.Count > 2000) movedBy.Clear();
        }

        // ---------------- News ----------------

        /// <summary>Something people will talk about (a raid, a death, a wedding): kept a month at most, the oldest go first.</summary>
        public TalkNote Note(string topic, string subject = null, int subjectId = -1, string tag = null)
        {
            if (topic.NullOrEmpty()) return null;
            var n = new TalkNote { topic = topic, tick = Find.TickManager.TicksGame, subject = subject, subjectId = subjectId, tag = tag };
            notes.Add(n);
            while (notes.Count > MaxNotes) notes.RemoveAt(0);
            hotTick = -1;
            return n;
        }

        /// <summary>The newest note on a topic within <paramref name="days"/> days, or null.</summary>
        public TalkNote Latest(string topic, float days)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                if (n.topic == topic && now - n.tick <= days * GenDate.TicksPerDay && n.tick <= now) return n;
            }
            return null;
        }

        /// <summary>The notes on a topic within <paramref name="days"/> days, newest first.</summary>
        public IEnumerable<TalkNote> Recent(string topic, float days)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var n = notes[i];
                if (n.topic == topic && now - n.tick <= days * GenDate.TicksPerDay && n.tick <= now) yield return n;
            }
        }

        private int hotTick = -1;
        private readonly Dictionary<string, float> hot = new Dictionary<string, float>();

        /// <summary>How hot a topic's news is (0..1): fresh news counts fully, fading over the topic's hot days. Worked out at most every 250 ticks.</summary>
        public float HotFor(TopicDef def)
        {
            if (def == null) return 0f;
            int now = Find.TickManager.TicksGame;
            if (hotTick < 0 || now - hotTick >= 250 || now < hotTick)
            {
                hotTick = now;
                hot.Clear();
                foreach (var n in notes)
                {
                    var t = DefRefs.Named<TopicDef>(n.topic);
                    if (t == null || n.tick > now) continue;
                    float days = (now - n.tick) / (float)GenDate.TicksPerDay;
                    float h = 1f - days / Math.Max(0.5f, t.hotDays);
                    if (h <= 0f) continue;
                    if (!hot.TryGetValue(n.topic, out float was) || h > was) hot[n.topic] = h;
                }
            }
            return hot.TryGetValue(def.defName, out float v) ? v : 0f;
        }

        // ---------------- Who talked lately ----------------

        /// <summary>Starting a talk costs four hours before the same person starts another, and the same two talk at most every half day.</summary>
        public const int StarterGapTicks = GenDate.TicksPerHour * 4, PairGapTicks = GenDate.TicksPerHour * 10;

        public bool MayStart(Pawn a, Pawn b, int now)
        {
            if (exchanges.Busy(a) || exchanges.Busy(b)) return false;
            if (lastStarted.TryGetValue(a.thingIDNumber, out int t) && t <= now && now - t < StarterGapTicks) return false;
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
            if (lastPair.TryGetValue(key, out int p) && p <= now && now - p < PairGapTicks) return false;
            return true;
        }

        /// <summary>How fresh a topic is between the two: 1 if they haven't had it in two days, less if they just did.</summary>
        public float Freshness(Pawn a, Pawn b, ushort topic, int now)
        {
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
            if (!lastTopic.TryGetValue((key, topic), out int t) || t > now) return 1f;
            float days = (now - t) / (float)GenDate.TicksPerDay;
            return days >= 2f ? 1f : 0.2f + 0.4f * days;
        }

        public void Started(Pawn a, Pawn b, ushort topic, int now)
        {
            lastStarted[a.thingIDNumber] = now;
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
            lastPair[key] = now;
            if (topic != 0) lastTopic[(key, topic)] = now;
            if (lastStarted.Count > 500) lastStarted.Clear();
            if (lastPair.Count > 2000) lastPair.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int now = Find.TickManager.TicksGame;
                notes.RemoveAll(n => n == null || now - n.tick > NoteDays * GenDate.TicksPerDay);
            }
            Scribe_Collections.Look(ref notes, "notes", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                notes ??= new List<TalkNote>();
                notes.RemoveAll(n => n == null || n.topic.NullOrEmpty());
            }
        }
    }
}
