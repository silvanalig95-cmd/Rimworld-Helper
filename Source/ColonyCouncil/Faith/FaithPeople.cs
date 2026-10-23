using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Something that moved a colonist's faith (a talk that swayed them, a crisis), for their page and the faiths page.</summary>
    public class FaithNote : IExposable
    {
        public int tick;
        public string text;
        /// <summary>How far their certainty moved (-0.12: twelve points less sure); 0 for a new faith taken.</summary>
        public float delta;
        /// <summary>A crisis of faith, not a talk.</summary>
        public bool crisis;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref delta, "delta");
            Scribe_Values.Look(ref crisis, "crisis");
        }
    }

    /// <summary>What faith in the colony keeps of one colonist: when talk last moved them, their last crisis, the demand of their faith weighing on them, and what moved them lately.</summary>
    public class FaithPerson : IExposable
    {
        public int id;
        /// <summary>When a talk last swayed them, last steadied them, and their last crisis of faith (-1: never).</summary>
        public int lastSwayed = -1, lastSteadied = -1, lastCrisis = -1;
        /// <summary>The demand of their faith weighing on them at the daily look (a ThoughtDef's name), and for how many days running.</summary>
        public string burden;
        public int burdenDays;
        public List<FaithNote> notes = new List<FaithNote>();
        /// <summary>How strongly they're drawn to other ideoligions (Faith/FaithLeanings.cs).</summary>
        public List<FaithLeaning> leanings = new List<FaithLeaning>();
        /// <summary>When a leaning last took them over (-1: never, or settled since): they settle into it a while.</summary>
        public int lastSwitch = -1;

        public bool Empty => notes.Count == 0 && leanings.Count == 0 && burden == null && lastSwayed < 0 && lastSteadied < 0 && lastCrisis < 0 && lastSwitch < 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref lastSwayed, "lastSwayed", -1);
            Scribe_Values.Look(ref lastSteadied, "lastSteadied", -1);
            Scribe_Values.Look(ref lastCrisis, "lastCrisis", -1);
            Scribe_Values.Look(ref lastSwitch, "lastSwitch", -1);
            Scribe_Values.Look(ref burden, "burden");
            Scribe_Values.Look(ref burdenDays, "burdenDays");
            Scribe_Collections.Look(ref notes, "notes", LookMode.Deep);
            Scribe_Collections.Look(ref leanings, "leanings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                notes ??= new List<FaithNote>();
                notes.RemoveAll(n => n == null);
                leanings ??= new List<FaithLeaning>();
                leanings.RemoveAll(l => l?.ideo == null);
            }
        }
    }

    /// <summary>A crisis of faith that comes some time after what caused it (the days after a funeral, back on their feet after a near-death).</summary>
    public class FaithWaiting : IExposable
    {
        public int pawn;
        /// <summary>The family of marks it gives (FaithLoss, FaithRescued, FaithNearDeath).</summary>
        public string family;
        /// <summary>The other person in it (thingIDNumber), or -1.</summary>
        public int other = -1;
        public string label;
        /// <summary>When it happened, and when it may be felt.</summary>
        public int tick, due;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref family, "family");
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref due, "due");
        }
    }

    /// <summary>
    /// What faith in the colony remembers about its people (Faith/): per colonist a small record (<see cref="FaithPerson"/>),
    /// and the crises waiting to be felt. Pruned of people the game no longer knows when it saves and once a day; a
    /// colonist's faith moments are kept <see cref="FaithPeopleMath.NoteDays"/> days, <see cref="FaithPeopleMath.MaxNotes"/>
    /// at most. Which pairs talked about faith lately isn't saved: after a load, any two may move each other again.
    /// </summary>
    public class FaithPeopleTracker : GameComponent
    {
        public List<FaithPerson> people = new List<FaithPerson>();
        public List<FaithWaiting> waiting = new List<FaithWaiting>();

        [Unsaved] private Dictionary<int, FaithPerson> byId;
        /// <summary>When a talk last moved the faith of either of two people (by pair).</summary>
        [Unsaved] private readonly Dictionary<long, int> pairs = new Dictionary<long, int>();

        private static FaithPeopleTracker cached;
        private static Game cachedGame;

        public static FaithPeopleTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<FaithPeopleTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public FaithPeopleTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byId = null;
            pairs.Clear();
        }

        public FaithPerson Of(Pawn p, bool create)
        {
            if (p == null) return null;
            if (byId == null)
            {
                byId = new Dictionary<int, FaithPerson>();
                foreach (var r in people) byId[r.id] = r;
            }
            if (byId.TryGetValue(p.thingIDNumber, out var found) || !create) return found;
            found = new FaithPerson { id = p.thingIDNumber };
            people.Add(found);
            byId[found.id] = found;
            return found;
        }

        private static long Pair(Pawn a, Pawn b) => PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);

        /// <summary>Whether a talk may move these two's faith now (one move a pair in <see cref="FaithPeopleMath.PairGapTicks"/>).</summary>
        public bool PairReady(Pawn a, Pawn b, int now) =>
            !pairs.TryGetValue(Pair(a, b), out int last) || FaithPeopleMath.Ready(last, now, FaithPeopleMath.PairGapTicks);

        public void PairMoved(Pawn a, Pawn b, int now) => pairs[Pair(a, b)] = now;

        public void Wait(Pawn p, string family, Pawn other, string label, int due)
        {
            if (p == null || family == null) return;
            int id = p.thingIDNumber;
            if (waiting.Any(w => w.pawn == id && w.family == family)) return;
            waiting.Add(new FaithWaiting { pawn = id, family = family, other = other?.thingIDNumber ?? -1, label = label, tick = Find.TickManager.TicksGame, due = due });
        }

        /// <summary>Lets go of people the game no longer knows, old faith moments, crises long past and pairs that may talk again.</summary>
        public void Prune()
        {
            var known = CouncilGameComponent.KnownPawnNumbers();
            int now = Find.TickManager.TicksGame;
            foreach (var r in people)
            {
                r.notes.RemoveAll(n => n == null || now - n.tick > FaithPeopleMath.NoteDays * FaithPeopleMath.DayTicks);
                while (r.notes.Count > FaithPeopleMath.MaxNotes) r.notes.RemoveAt(0);
                if (r.lastSwayed >= 0 && now - r.lastSwayed > FaithPeopleMath.DayTicks * 3) r.lastSwayed = -1;
                if (r.lastSteadied >= 0 && now - r.lastSteadied > FaithPeopleMath.DayTicks * 3) r.lastSteadied = -1;
                // The last crisis only matters while the gap after it lasts.
                if (r.lastCrisis >= 0 && FaithPeopleMath.CrisisReady(r.lastCrisis, now)) r.lastCrisis = -1;
                if (r.lastSwitch >= 0 && LeaningMath.Settled(r.lastSwitch, now, FaithPeopleMath.DayTicks)) r.lastSwitch = -1;
                // An ideoligion the game has let go of draws nobody.
                r.leanings.RemoveAll(l => !FaithLeanings.Alive(l?.ideo));
            }
            people.RemoveAll(r => r == null || !known.Contains(r.id) || r.Empty);
            waiting.RemoveAll(w => w == null || !known.Contains(w.pawn) || now - w.tick > FaithPeopleMath.WaitingDays * FaithPeopleMath.DayTicks);
            foreach (var k in pairs.Where(kv => now - kv.Value > FaithPeopleMath.PairGapTicks || kv.Value > now).Select(kv => kv.Key).ToList()) pairs.Remove(k);
            byId = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("faith clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref people, "people", LookMode.Deep);
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<FaithPerson>();
                waiting ??= new List<FaithWaiting>();
                people.RemoveAll(r => r == null);
                waiting.RemoveAll(w => w == null);
                byId = null;
            }
        }
    }

    /// <summary>
    /// Moving a colonist's faith the way faith in the colony does it (Faith/FaithTalk.cs, FaithCrisis.cs), on top of the
    /// core (<see cref="Faiths"/>): each move noted for their page, and their view about faith kept in step with how
    /// firmly they hold theirs, so the character page's view and certainty read sensibly together.
    /// </summary>
    public static class FaithPeople
    {
        public static FaithPeopleTracker Tracker => FaithPeopleTracker.Instance;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>One of the colony's own free people with a faith, whose faith Rimfolk speaks about.</summary>
        public static bool Ours(Pawn p) => Faiths.Holds(p) && p.IsFreeColonist && !p.Dead && !p.IsQuestLodger();

        /// <summary>A faith moment for their page: what it was and how far it moved them.</summary>
        public static void Note(Pawn p, string text, float delta, bool crisis = false)
        {
            if (p == null || text.NullOrEmpty()) return;
            var r = Tracker?.Of(p, true);
            if (r == null) return;
            r.notes.Add(new FaithNote { tick = Now, text = text, delta = delta, crisis = crisis });
            while (r.notes.Count > FaithPeopleMath.MaxNotes) r.notes.RemoveAt(0);
        }

        /// <summary>Their faith moments, newest first.</summary>
        public static List<FaithNote> Notes(Pawn p)
        {
            var r = Tracker?.Of(p, false);
            return r == null ? new List<FaithNote>() : r.notes.AsEnumerable().Reverse().ToList();
        }

        /// <summary>
        /// A word from <paramref name="speaker"/> sways <paramref name="listener"/> (the core's <see cref="Faiths.Sway"/>),
        /// noted for their page with how far it moved them, their view about faith following.
        /// </summary>
        public static Faiths.SwayResult Sway(Pawn listener, Pawn speaker, float baseAmount, string cause, string note)
        {
            float before = Faiths.Certainty(listener);
            var r = Faiths.Sway(listener, speaker, baseAmount, cause);
            After(listener, r, before, note, false);
            return r;
        }

        /// <summary>The same toward a faith by an amount already worked out (a rescuer's).</summary>
        public static Faiths.SwayResult SwayToward(Pawn listener, Ideo toward, float amount, string cause, Pawn by, string note, bool crisis)
        {
            float before = Faiths.Certainty(listener);
            var r = Faiths.SwayToward(listener, toward, amount, cause, by);
            After(listener, r, before, note, crisis);
            return r;
        }

        private static void After(Pawn p, Faiths.SwayResult r, float before, string note, bool crisis)
        {
            if (r == Faiths.SwayResult.None) return;
            if (r == Faiths.SwayResult.Converted)
            {
                Note(p, "CC_FaithP_NoteConverted".Translate((Faiths.Of(p)?.name ?? "").Named("FAITH")).Resolve(), 0f, crisis);
                return;
            }
            float d = Faiths.Certainty(p) - before;
            Note(p, note, d, crisis);
            KeepView(p, d);
        }

        /// <summary>A crisis takes certainty without pointing anywhere (the core's <see cref="Faiths.Shake"/>), noted.</summary>
        public static float Shake(Pawn p, float amount, string note)
        {
            float before = Faiths.Certainty(p);
            Faiths.Shake(p, amount);
            float d = Faiths.Certainty(p) - before;
            if (d < 0f)
            {
                Note(p, note, d, true);
                KeepView(p, d);
            }
            return d;
        }

        /// <summary>A little steadier (a talk with one of their own, an argument that made them dig in); not noted, too small.</summary>
        public static void Steady(Pawn p, float amount)
        {
            float before = Faiths.Certainty(p);
            Faiths.Steady(p, amount);
            KeepView(p, Faiths.Certainty(p) - before);
        }

        /// <summary>
        /// Their view about faith (the talk's faith topic) moved with their certainty, then kept where it can't read
        /// against it (<see cref="FaithPeopleMath.ViewWithCertainty"/>). Only a view they already hold.
        /// </summary>
        public static void KeepView(Pawn p, float certaintyMoved)
        {
            try
            {
                var topic = TalkDefs.Topic("Faith");
                if (topic == null || p == null || !Faiths.Holds(p) || !Persona.HasStance(p, topic.defName)) return;
                float s = Persona.Stance(p, topic.defName);
                float now = FaithPeopleMath.ViewWithCertainty(TalkMath.Shift(s, certaintyMoved * FaithPeopleMath.ViewPerCertainty), Faiths.Certainty(p));
                if (Math.Abs(now - s) > 0.0001f) Persona.SetStance(p, topic.defName, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: the view about faith", e);
            }
        }

        // ---------------- Who could sway whom ----------------

        /// <summary>
        /// How much one good talk (a friendly debate the speaker came out of well) would take off the listener's certainty:
        /// the core's sway (the speaker's conversion power, the listener's openness, pride, opinion, a faith that frowns
        /// on preaching) and the game's own certainty-loss factor.
        /// </summary>
        public static float Expected(Pawn listener, Pawn speaker, float baseAmount, float? speakerPower = null, float? listenerLoss = null)
        {
            if (!Faiths.Holds(listener) || !Faiths.Holds(speaker) || Faiths.Same(listener, speaker)) return 0f;
            try
            {
                float power = speakerPower ?? speaker.GetStatValue(StatDefOf.ConversionPower);
                float opinion = listener.relations?.OpinionOf(speaker) ?? 0f;
                float v = FaithMath.Sway(baseAmount, power, Persona.Get(listener, PersonaAxis.Openness), Persona.Get(listener, PersonaAxis.Pride), opinion,
                    Faiths.DislikesPreaching(listener));
                return v * Math.Max(0f, listenerLoss ?? listener.GetStatValue(StatDefOf.CertaintyLossFactor));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: who could sway whom", e);
                return 0f;
            }
        }

        /// <summary>A believer's faith in a word: "devout", "wavering" or "steady" (translated).</summary>
        public static string StateWord(float certainty) => ("CC_FaithP_State_" + FaithPeopleMath.State(certainty)).Translate();
    }

    /// <summary>
    /// What a faith asks that weighs on someone right now: their negative mood thoughts that come from their own faith's
    /// precepts (the thought's own precept, or a thought one of the faith's precepts gives). Read by the faiths page, the
    /// character page and the daily look for a faith's demands held too long.
    /// </summary>
    public static class FaithAsks
    {
        public class Ask
        {
            public ThoughtDef def;
            public string label;
            public float mood;
        }

        private static readonly List<Thought> groups = new List<Thought>();
        private static readonly Dictionary<Ideo, (int tick, HashSet<ThoughtDef> defs)> linked = new Dictionary<Ideo, (int, HashSet<ThoughtDef>)>();

        /// <summary>The thoughts a faith's precepts give (their comps), worked out at most once an hour per faith (a reform can change them).</summary>
        public static HashSet<ThoughtDef> Linked(Ideo ideo)
        {
            if (ideo == null) return new HashSet<ThoughtDef>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (linked.TryGetValue(ideo, out var c) && now >= c.tick && now - c.tick < GenDate.TicksPerHour) return c.defs;
            var set = new HashSet<ThoughtDef>();
            foreach (var pr in ideo.PreceptsListForReading)
            {
                var comps = pr?.def?.comps;
                if (comps == null) continue;
                foreach (var comp in comps)
                    if (comp is PreceptComp_Thought t && t.thought != null) set.Add(t.thought);
            }
            if (linked.Count > 40) linked.Clear();
            linked[ideo] = (now, set);
            return set;
        }

        /// <summary>What their own faith asks that weighs on them now, worst first (each at least half a point off their mood).</summary>
        public static List<Ask> Of(Pawn p)
        {
            var list = new List<Ask>();
            var ideo = Faiths.Of(p);
            var th = p?.needs?.mood?.thoughts;
            if (ideo == null || th == null) return list;
            try
            {
                groups.Clear();
                th.GetDistinctMoodThoughtGroups(groups);
                HashSet<ThoughtDef> own = null;
                foreach (var t in groups)
                {
                    if (t?.def == null) continue;
                    bool fromFaith = t.sourcePrecept != null ? t.sourcePrecept.ideo == ideo : (own ??= Linked(ideo)).Contains(t.def);
                    if (!fromFaith) continue;
                    float mood = th.MoodOffsetOfGroup(t);
                    if (mood > -0.5f) continue;
                    list.Add(new Ask { def = t.def, label = t.LabelCap, mood = mood });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith: what a faith asks", e);
            }
            finally
            {
                groups.Clear();
            }
            list.Sort((a, b) => a.mood.CompareTo(b.mood));
            return list;
        }

        /// <summary>"Ate without a table (-3)": an ask as the pages say it.</summary>
        public static string Text(Ask a) => "CC_FaithP_Ask".Translate(a.label.Named("WHAT"), a.mood.ToString("0").Named("MOOD")).Resolve();
    }
}
