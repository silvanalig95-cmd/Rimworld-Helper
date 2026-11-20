using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony life's integrations, part 2 (the content update, the player's go of 20 November 2026): what is kept between
    /// passes for them. A wished-for thing still to be tried out (A12, <see cref="TryOuts"/>), lines said in turn at a
    /// gathering under way (<see cref="Readings"/>: the aims said at the year's first supper, A27, and the year read aloud,
    /// A29), the aims said aloud this year for the year letter to read back (A27), the ambitions reached this year for the
    /// reading (A14, A29), the colony's record keeper and the year last read aloud (A29). Saved with the council
    /// (CouncilGameComponent.life); each list is kept short (DESIGN.md, "Save size").
    /// </summary>
    public class LifeBook : IExposable
    {
        public List<TryOut> tryOuts = new List<TryOut>();
        public List<GatheringReading> readings = new List<GatheringReading>();
        public List<SaidAim> aims = new List<SaidAim>();
        /// <summary>The ambitions reached since the year was last read aloud, by their label ("a great hall").</summary>
        public List<string> built = new List<string>();
        /// <summary>The colony's record keeper, by the council's answer ("Make A keeper of the colony's record"): writes the year from then on.</summary>
        public Pawn recordKeeper;
        /// <summary>The colony's year (the landing's count) last read aloud, so it is read once.</summary>
        public int yearRead = -1;

        /// <summary>At most this many of each list: a handful at once is all a colony ever has.</summary>
        public const int MaxTryOuts = 12, MaxReadings = 6, MaxAims = 30, MaxBuilt = 12;

        public static LifeBook Instance => CouncilGameComponent.Instance?.life;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                tryOuts.RemoveAll(t => t == null || t.pawn == null || t.pawn.Destroyed);
                if (recordKeeper != null && (recordKeeper.Dead || recordKeeper.Discarded)) recordKeeper = null;
            }
            Scribe_Collections.Look(ref tryOuts, "tryOuts", LookMode.Deep);
            Scribe_Collections.Look(ref readings, "readings", LookMode.Deep);
            Scribe_Collections.Look(ref aims, "aims", LookMode.Deep);
            Scribe_Collections.Look(ref built, "built", LookMode.Value);
            Scribe_References.Look(ref recordKeeper, "recordKeeper");
            Scribe_Values.Look(ref yearRead, "yearRead", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tryOuts ??= new List<TryOut>();
                tryOuts.RemoveAll(t => t?.pawn == null);
                readings ??= new List<GatheringReading>();
                readings.RemoveAll(r => r == null);
                aims ??= new List<SaidAim>();
                aims.RemoveAll(a => a == null || a.pawnId.NullOrEmpty());
                built ??= new List<string>();
            }
        }

        // ---------------- The pass ----------------

        /// <summary>
        /// Every 250 ticks with the answers playing out (PlayOutTracker's pass): try-outs that may start or have run out
        /// of time, and the lines still to be said at a gathering under way. Nothing when nothing waits.
        /// </summary>
        public static void Tick(int now)
        {
            var book = Instance;
            if (book == null || (book.tryOuts.Count == 0 && book.readings.Count == 0)) return;
            if (book.tryOuts.Count > 0) Safe("wished-for things tried out", () => TryOuts.Step(book, now));
            if (book.readings.Count > 0) Safe("lines said at a gathering", () => Readings.Step(book, now));
        }

        /// <summary>A plan is over and happened (PlayOutTracker.Done): its readings close with those who were there.</summary>
        public static void PlayDone(PlayOut p, List<Pawn> took)
        {
            var book = Instance;
            if (book == null || p == null || book.readings.Count == 0) return;
            Safe("a reading closed", () => Readings.Closed(book, p, took));
        }

        internal static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(what, e);
            }
        }

        // ---------------- The year's aims and works ----------------

        /// <summary>An ambition reached (A14): kept for the year's reading aloud, the newest few.</summary>
        public void NoteBuilt(string label)
        {
            if (label.NullOrEmpty()) return;
            built.Add(label);
            while (built.Count > MaxBuilt) built.RemoveAt(0);
        }

        /// <summary>An aim said aloud at the year's first supper (A27), for the year letter to read back.</summary>
        public void NoteAim(SaidAim a)
        {
            if (a == null || a.pawnId.NullOrEmpty()) return;
            aims.RemoveAll(x => x.pawnId == a.pawnId && x.year == a.year);
            aims.Add(a);
            while (aims.Count > MaxAims) aims.RemoveAt(0);
        }

        /// <summary>The record keeper, while they are alive, ours and grown; else null.</summary>
        public Pawn Keeper(IList<Pawn> colonists)
        {
            var k = recordKeeper;
            if (k == null) return null;
            if (k.Dead || k.Discarded)
            {
                recordKeeper = null;
                return null;
            }
            return colonists != null && colonists.Contains(k) && !k.DevelopmentalStage.Juvenile() ? k : null;
        }
    }

    /// <summary>One aim said aloud at the year's first supper (A27): whose, which life goal, in which of the colony's years.</summary>
    public class SaidAim : IExposable
    {
        public string pawnId;
        public string name;
        /// <summary>The life goal (an AspirationDef's name) and its words then ("train the dogs").</summary>
        public string def;
        public string goal;
        /// <summary>The colony's year since the landing (YearLetterMath.YearOf) it was said in, and when.</summary>
        public int year;
        public int tick;
        /// <summary>Chosen there and then, at the supper.</summary>
        public bool fresh;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref goal, "goal");
            Scribe_Values.Look(ref year, "year");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref fresh, "fresh");
        }
    }

    public partial class CouncilGameComponent
    {
        /// <summary>Colony life's integrations, part 2: try-outs, readings, the year's aims and the record keeper (ColonyLife/).</summary>
        public LifeBook life = new LifeBook();

        private void ExposeLife()
        {
            Scribe_Deep.Look(ref life, "life");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) life ??= new LifeBook();
        }
    }
}
