using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A place dedicated to a god (Worship/Shrines.cs): the building, its god, who dedicated it and since when; pending until someone has said the words there.</summary>
    public class ShrineRecord : IExposable
    {
        public Thing thing;
        public int god = -1;
        public Pawn by;
        public string byName;
        public int since = -1;
        /// <summary>Asked for and waiting for someone free to walk over and dedicate it, since <see cref="asked"/>.</summary>
        public bool pending;
        public int asked;
        /// <summary>The god it was dedicated to before (a pending re-dedication keeps the old one until the words are said), or -1.</summary>
        public int before = -1;

        public bool Live => thing != null && !thing.Destroyed && thing.Spawned;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref byName, "byName");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref pending, "pending");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref before, "before", -1);
        }
    }

    /// <summary>An offering carried to a shrine (Worship/Offerings.cs), until it is taken, eaten, carried off, spoiled or returned.</summary>
    public class OfferingRecord : IExposable
    {
        public Thing thing;
        public Thing shrine;
        public int god = -1;
        public int home = HomesMath.None;
        public Pawn carrier;
        /// <summary>What it was, in words ("a fine meal", "a thrumbo fur"), for when it is gone.</summary>
        public string label;
        public OfferingKind kind;
        /// <summary>The favour it moves when taken, worked out as it was chosen.</summary>
        public float favour;
        /// <summary>Picked and on its way (false), or set down at the shrine since <see cref="placed"/>.</summary>
        public bool set;
        public int chosen, placed = -1;
        public OfferingFate fate = OfferingFate.Waiting;
        /// <summary>It answers the god's demand in a dream.</summary>
        public bool demand;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_References.Look(ref shrine, "shrine");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref kind, "kind", OfferingKind.Other);
            Scribe_Values.Look(ref favour, "favour");
            Scribe_Values.Look(ref set, "set");
            Scribe_Values.Look(ref chosen, "chosen");
            Scribe_Values.Look(ref placed, "placed", -1);
            Scribe_Values.Look(ref fate, "fate", OfferingFate.Waiting);
            Scribe_Values.Look(ref demand, "demand");
        }
    }

    /// <summary>A prayer said in a hard moment (Worship/Prayers.cs), until it is answered, comes good on its own, or goes unanswered.</summary>
    public class PrayerRecord : IExposable
    {
        public Pawn pawn;
        public int god = -1;
        public PrayerKind kind;
        /// <summary>Who it was for: the sick, the dead; null for a raid or a drought.</summary>
        public Pawn about;
        public string aboutName;
        public int home = HomesMath.None;
        public int tick;
        /// <summary>Said yet (false while it waits for them to be free), at a shrine, answered, and settled.</summary>
        public bool said, atShrine, answered, settled;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref kind, "kind", PrayerKind.Sickness);
            Scribe_References.Look(ref about, "about");
            Scribe_Values.Look(ref aboutName, "aboutName");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref said, "said");
            Scribe_Values.Look(ref atShrine, "atShrine");
            Scribe_Values.Look(ref answered, "answered");
            Scribe_Values.Look(ref settled, "settled");
        }
    }

    /// <summary>What a colonist's vow is (Worship/Vows.cs), beside the core's GodVow (found by pawn, god and since).</summary>
    public class VowRecord : IExposable
    {
        public Pawn pawn;
        public int god = -1;
        public int since;
        public VowKind kind;
        /// <summary>The sick one a vow of no meat is for.</summary>
        public Pawn about;
        public string aboutName;
        /// <summary>The beast kind a vow spares.</summary>
        public ThingDef beast;
        public int until = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref kind, "kind", VowKind.NoMeat);
            Scribe_References.Look(ref about, "about");
            Scribe_Values.Look(ref aboutName, "aboutName");
            Scribe_Defs.Look(ref beast, "beast");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>A god's demand in a dream (Worship/GodDreams.cs): an offering of a kind at its shrine within a few days.</summary>
    public class DemandRecord : IExposable
    {
        public int god = -1;
        public Thing shrine;
        public OfferingKind kind;
        public Pawn dreamer;
        public int home = HomesMath.None;
        public int until;
        public bool met, done;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref shrine, "shrine");
            Scribe_Values.Look(ref kind, "kind", OfferingKind.Other);
            Scribe_References.Look(ref dreamer, "dreamer");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref met, "met");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>Something of a person's that the gods' worship remembers: a crisis of faith (tick), a turning to another god, a pilgrimage made.</summary>
    public class FaithMark : IExposable
    {
        public Pawn pawn;
        /// <summary>"crisis", "turned" (to <see cref="god"/>), "pilgrimage" (to <see cref="god"/>).</summary>
        public string what;
        public int god = -1;
        public int tick;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>Something kept for a home for a while: the god honoured this year, the rain kept away, the Hunger's due, a god's last answer.</summary>
    public class HomeMark : IExposable
    {
        public int home = HomesMath.None;
        /// <summary>
        /// "honoured" (the year in <see cref="value"/>), "dry" (until <see cref="until"/>), "due" (the Hunger's, from <see cref="until"/>),
        /// "answer" (the tick of the god's last in <see cref="value"/>), "rain" (an answered prayer's rain, at <see cref="value"/>).
        /// </summary>
        public string what;
        public int god = -1;
        public int value, until;

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref until, "until");
        }
    }

    /// <summary>
    /// Worship of the gods, saved with the game (docs/design/gods.md, stage 2 "Worship"; the rules in WorshipMath): the
    /// places dedicated to a god, the offerings at them, the prayers said in hard moments, the vows' kinds, the gods' demands
    /// in dreams, the faith's marks on a person (a crisis, a turning, a pilgrimage), and a home's marks (the god honoured this
    /// year, the rain kept away, the Hunger's due, each god's last answer). Kept in the region's hour and day beside the
    /// pantheon (World/Region/RegionBook.cs); everything else goes through the core's <see cref="Gods"/> and its two seams.
    /// </summary>
    public class WorshipBook : GameComponent
    {
        public List<ShrineRecord> shrines = new List<ShrineRecord>();
        public List<OfferingRecord> offerings = new List<OfferingRecord>();
        public List<PrayerRecord> prayers = new List<PrayerRecord>();
        public List<VowRecord> vows = new List<VowRecord>();
        public List<DemandRecord> demands = new List<DemandRecord>();
        public List<FaithMark> marks = new List<FaithMark>();
        public List<HomeMark> homeMarks = new List<HomeMark>();

        /// <summary>At most this many of each kept: dedicated places, offerings (open and lately done), prayers, vows, demands, marks.</summary>
        public const int MaxShrines = 60, MaxOfferings = 40, MaxPrayers = 80, MaxVows = 40, MaxDemands = 12, MaxMarks = 120, MaxHomeMarks = 80;

        private static WorshipBook cached;
        private static Game cachedGame;

        [Unsaved] private Dictionary<int, ShrineRecord> byThing;

        public WorshipBook(Game game)
        {
        }

        public static WorshipBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WorshipBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byThing = null;
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Shrines =================

        public ShrineRecord ShrineOf(Thing t)
        {
            if (t == null) return null;
            if (byThing == null)
            {
                byThing = new Dictionary<int, ShrineRecord>();
                foreach (var s in shrines)
                    if (s?.thing != null)
                        byThing[s.thing.thingIDNumber] = s;
            }
            return byThing.TryGetValue(t.thingIDNumber, out var r) ? r : null;
        }

        public void Changed() => byThing = null;

        // ================= Home marks =================

        public HomeMark Mark(int home, string what, int god = -1) =>
            homeMarks.FirstOrDefault(m => m != null && m.home == home && m.what == what && (god < 0 || m.god == god));

        public HomeMark SetMark(int home, string what, int god, int value, int until)
        {
            var m = Mark(home, what, what == "answer" ? god : -1);
            if (m == null) homeMarks.Add(m = new HomeMark { home = home, what = what });
            m.god = god;
            m.value = value;
            m.until = until;
            while (homeMarks.Count > MaxHomeMarks) homeMarks.RemoveAt(0);
            return m;
        }

        // ================= Faith marks =================

        public void AddMark(Pawn p, string what, int god)
        {
            if (p == null) return;
            marks.Add(new FaithMark { pawn = p, what = what, god = god, tick = Now });
            while (marks.Count > MaxMarks) marks.RemoveAt(0);
        }

        /// <summary>The god someone turned to after a crisis of faith (the newest), or -1.</summary>
        public int TurnedTo(Pawn p)
        {
            if (p == null) return -1;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                if (m != null && m.pawn == p && m.what == "turned") return m.god;
            }
            return -1;
        }

        public int Count(Pawn p, string what, int sinceTick, int god = -1) =>
            marks.Count(m => m != null && m.pawn == p && m.what == what && m.tick >= sinceTick && (god < 0 || m.god == god));

        // ================= Pruning =================

        /// <summary>The region's day: gone things and people let go, old settled records trimmed.</summary>
        public void Prune(int now)
        {
            shrines.RemoveAll(s => s == null || s.thing == null || s.thing.Destroyed || s.god < 0 || Gods.ById(s.god) == null);
            while (shrines.Count > MaxShrines) shrines.RemoveAt(0);
            offerings.RemoveAll(o => o == null || o.fate != OfferingFate.Waiting && now - Math.Max(o.placed, o.chosen) > GenDate.TicksPerDay * 10);
            while (offerings.Count > MaxOfferings) offerings.RemoveAt(0);
            prayers.RemoveAll(p => p == null || p.pawn == null || p.pawn.Discarded || p.settled && now - p.tick > GenDate.TicksPerDay * 20);
            while (prayers.Count > MaxPrayers) prayers.RemoveAt(0);
            vows.RemoveAll(v => v == null || v.pawn == null || v.pawn.Discarded || v.done && now - v.since > GenDate.TicksPerYear);
            while (vows.Count > MaxVows && vows.Any(v => v.done)) vows.Remove(vows.First(v => v.done));
            demands.RemoveAll(d => d == null || d.done && now - d.until > GenDate.TicksPerDay * 10);
            while (demands.Count > MaxDemands) demands.RemoveAt(0);
            marks.RemoveAll(m => m == null || m.pawn == null || m.pawn.Discarded || m.what == "crisis" && now - m.tick > GenDate.TicksPerDay * WorshipMath.CrisisWindowDays);
            homeMarks.RemoveAll(m => m == null || m.what == "dry" && m.until < now || m.what == "due" && m.until < now - GenDate.TicksPerDay * 3
                                    || m.what == "answer" && now - m.value > GenDate.TicksPerDay * 20
                                    || m.what == "rain" && now - m.value > GenDate.TicksPerDay * 2 || m.what == "honoured" && now - m.until > GenDate.TicksPerYear * 2);
            byThing = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref shrines, "shrines", LookMode.Deep);
            Scribe_Collections.Look(ref offerings, "offerings", LookMode.Deep);
            Scribe_Collections.Look(ref prayers, "prayers", LookMode.Deep);
            Scribe_Collections.Look(ref vows, "vows", LookMode.Deep);
            Scribe_Collections.Look(ref demands, "demands", LookMode.Deep);
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            Scribe_Collections.Look(ref homeMarks, "homeMarks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                shrines ??= new List<ShrineRecord>();
                shrines.RemoveAll(s => s?.thing == null);
                offerings ??= new List<OfferingRecord>();
                offerings.RemoveAll(o => o == null);
                prayers ??= new List<PrayerRecord>();
                prayers.RemoveAll(p => p?.pawn == null);
                vows ??= new List<VowRecord>();
                vows.RemoveAll(v => v?.pawn == null);
                demands ??= new List<DemandRecord>();
                demands.RemoveAll(d => d == null);
                marks ??= new List<FaithMark>();
                marks.RemoveAll(m => m?.pawn == null);
                homeMarks ??= new List<HomeMark>();
                homeMarks.RemoveAll(m => m == null);
                byThing = null;
            }
        }
    }
}
