using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Saving an event (its fields and rules are in HistoryMath.cs). Refs are saved in their short text form, the actors as one line.</summary>
    public sealed partial class HistoryEntry : IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref tick, "tick");
            int from = first == tick ? -1 : first;
            Scribe_Values.Look(ref from, "first", -1);
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref kind, "kind");
            string who = Scribe.mode == LoadSaveMode.Saving ? HistoryMath.PackActors(actors) : null;
            if (who == "") who = null;
            Scribe_Values.Look(ref who, "actors");
            Refs.Look(ref place, "place");
            Scribe_Values.Look(ref weight, "weight", HistoryMath.Everyday);
            Scribe_Values.Look(ref key, "key");
            // Most events have no values: nothing written for them.
            if (Scribe.mode != LoadSaveMode.Saving || values != null && values.Count > 0) Scribe_Collections.Look(ref values, "values", LookMode.Value);
            Scribe_Values.Look(ref line, "line");
            Scribe_Values.Look(ref folded, "folded");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                first = from < 0 ? tick : from;
                actors = HistoryMath.UnpackActors(who);
            }
        }
    }

    /// <summary>
    /// The world's one history, kept by the world ledger (the book is pure: HistoryMath.cs). What writes to it: every line
    /// of the colony's chronicle (<see cref="CouncilGameComponent.AddChronicle"/>, which keeps its own list and caps as
    /// before), world situations through their chronicle lines (with their place and peoples), the deeds the peoples
    /// remember (<see cref="FactionMemory.Note"/>), and the ledger's ties as they are made and move. Once a day small
    /// events past sixty days fold into one summary per actor, kind and season; milestones stay. Saved with the ledger.
    /// </summary>
    public partial class WorldLedger
    {
        private HistoryBook history = new HistoryBook();
        /// <summary>The chronicle's entries were taken into the history (an older save's, on its first load with it).</summary>
        private bool chronicleRead;

        /// <summary>The history, for the queries (<see cref="ColonyCouncil.History"/>) and the dev window.</summary>
        public HistoryBook Book => history;

        private void ExposeHistory()
        {
            Scribe_Collections.Look(ref history.events, "events", LookMode.Deep);
            Scribe_Values.Look(ref history.nextId, "nextEvent", 1);
            Scribe_Values.Look(ref chronicleRead, "chronicleRead");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) history.Load(history.events, history.nextId);
        }

        /// <summary>After loading or starting a game: an older save's chronicle is taken in, once.</summary>
        private void HistoryReady()
        {
            if (chronicleRead) return;
            chronicleRead = true;
            try
            {
                var chronicle = Current.Game?.GetComponent<CouncilGameComponent>()?.chronicle;
                if (chronicle == null || chronicle.Count == 0) return;
                // Where its lines happened isn't known any more: the colony the council sits at.
                var seat = Refs.Seat;
                var lines = new List<HistoryEntry>();
                foreach (var c in chronicle)
                    if (c != null)
                        lines.Add(HistoryMath.FromChronicle(c.tick, (int)c.kind, c.text, c.pawnId, seat));
                history.TakeChronicle(lines);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("history: the chronicle taken in", e);
            }
        }

        /// <summary>The history's day: old small events fold into their season's summaries, and it is kept within its bound.</summary>
        public override void GameComponentTick()
        {
            var tm = Find.TickManager;
            if (tm == null || !Sched.Every(tm.TicksGame, Sched.DayTicks, Sched.DailyHistory)) return;
            using var clock = PassClock.Time(Pass.DailyHistory);
            try
            {
                history.Fold(tm.TicksGame, tm.gameStartAbsTick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("history: the day's folding", e);
            }
        }
    }

    /// <summary>
    /// One history of the world: what happened, to whom, where, and how much it mattered. Later features (the region's
    /// towns, the diplomacy, letters and talk that remember) ask it rather than each keeping a list of their own:
    /// <see cref="Of"/> someone, <see cref="At"/> a place, the <see cref="Latest"/> of a kind, how many times
    /// (<see cref="Count"/>). Kinds are small keys with families ("deed" finds every deed). Queries give the newest first
    /// and are index lookups. Everything is safe to call without a game (empty answers).
    /// </summary>
    public static class History
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static HistoryBook Book => WorldLedger.Instance?.Book;

        /// <summary>Moves on with every change to the history (an event, a fold, a load).</summary>
        public static int Version => Book?.Version ?? 0;

        public static int Size => Book?.Count ?? 0;

        // ---------------- Asking ----------------

        /// <summary>What someone (a person, a people, a town, a colony) took part in, newest first: of these kinds (or families), since then, at most so many.</summary>
        public static List<HistoryEntry> Of(Ref who, string[] kinds = null, int since = int.MinValue, int max = int.MaxValue) =>
            Book?.Of(who, kinds, since, max) ?? new List<HistoryEntry>();

        /// <summary>What happened at a place (a colony, a town, a tile), newest first, filtered the same way.</summary>
        public static List<HistoryEntry> At(Ref place, string[] kinds = null, int since = int.MinValue, int max = int.MaxValue) =>
            Book?.At(place, kinds, since, max) ?? new List<HistoryEntry>();

        /// <summary>The newest event of a kind (or family), of someone and at a place when given; null for none.</summary>
        public static HistoryEntry Latest(string kind, Ref who = default, Ref place = default) => Book?.Latest(kind, who, place);

        /// <summary>How many times (a summary counts all it stands for): of a kind or family (null: any), of someone, at a place, since then.</summary>
        public static int Count(string kind = null, Ref who = default, Ref place = default, int since = int.MinValue) =>
            Book?.CountOf(kind, who, place, since) ?? 0;

        public static HistoryEntry Get(int id) => Book?.Get(id);

        /// <summary>
        /// Its words: the finished line it was written with (the chronicle's); for a deed, the peoples' memory's words for
        /// it ("traded fairly with them, 11 times"); otherwise null (a summary of chronicle lines has no words of its own).
        /// </summary>
        public static string Text(HistoryEntry e)
        {
            if (e == null) return null;
            if (e.line != null) return e.line;
            if (HistoryKinds.Matches(e.kind, HistoryKinds.Deed) && e.kind.Length > HistoryKinds.Deed.Length + 1)
            {
                Faction f = null;
                foreach (var a in e.actors)
                    if (a.who.kind == RefKind.People)
                        f = Refs.Faction(a.who);
                string deed = e.kind.Substring(HistoryKinds.Deed.Length + 1);
                return FactionMemory.Text(new FactionDeed { kind = deed, count = e.count, faction = f, name = e.values != null && e.values.Count > 0 ? e.values[0] : null });
            }
            return null;
        }

        // ---------------- Writing ----------------

        /// <summary>
        /// A new event, now (or at <paramref name="tick"/>, for one noticed late): its kind, who took part, where, how much
        /// it matters, and its words (a finished line, or a text key with values). <paramref name="mergeTicks"/>: a small
        /// one like the last within that long adds to its count. Returns it (or the one it added to); null without a game.
        /// </summary>
        public static HistoryEntry Add(string kind, List<HistoryActor> actors, Ref place, int weight = HistoryMath.Everyday, string line = null, string key = null,
            List<string> values = null, int count = 1, int mergeTicks = 0, int tick = -1)
        {
            var book = Book;
            if (book == null || kind == null || Scribe.mode != LoadSaveMode.Inactive) return null;
            try
            {
                int now = tick >= 0 ? Math.Min(tick, Now) : Now;
                return book.Add(new HistoryEntry
                {
                    tick = now, first = now, kind = kind, actors = actors ?? new List<HistoryActor>(), place = place, weight = weight, line = line, key = key, values = values,
                    count = count
                }, mergeTicks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("history: an event " + kind, e);
                return null;
            }
        }

        /// <summary>
        /// A line of the chronicle, the same in the history: its tick, words and kind, the colonist it is about, and the
        /// colony as its place (theirs, or the seat). A world situation's line also has the situation's place and peoples
        /// (<see cref="Situation"/>). The chronicle itself is not touched.
        /// </summary>
        public static void FromChronicle(ChronicleEntry entry)
        {
            if (entry == null) return;
            var pawn = entry.about;
            entry.about = null;
            var book = Book;
            if (book == null || Scribe.mode != LoadSaveMode.Inactive) return;
            try
            {
                var e = HistoryMath.FromChronicle(entry.tick, (int)entry.kind, entry.text, entry.pawnId, Refs.Colony(pawn != null ? Homes.Of(pawn) : Homes.Seat));
                var note = notes.Count > 0 ? notes[notes.Count - 1] : null;
                if (note != null)
                {
                    if (note.kind != null && HistoryKinds.Matches(note.kind, e.kind)) e.kind = note.kind;
                    if (!note.place.IsNone) e.place = note.place;
                    var actors = new List<HistoryActor>(note.actors);
                    foreach (var a in e.actors)
                        if (!actors.Exists(x => x.who == a.who))
                            actors.Add(a);
                    e.actors = actors;
                }
                book.Add(e);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("history: a chronicle line", ex);
            }
        }

        /// <summary>
        /// A deed a people will remember (as FactionMemory notes it): the people and the colony with their parts by its
        /// side, its words by the memory's key, a milestone when it is big. A run of the same within a day is one event
        /// with a count (a raid's fallen).
        /// </summary>
        public static void Deed(Faction f, string deedKind, DeedSide side, int weight, int count, string name)
        {
            if (f == null || deedKind == null || Book == null) return;
            HistoryRole theirs, ours;
            switch (side)
            {
                case DeedSide.Favor:
                    theirs = HistoryRole.For;
                    ours = HistoryRole.Did;
                    break;
                case DeedSide.Wrong:
                    theirs = HistoryRole.Against;
                    ours = HistoryRole.Did;
                    break;
                case DeedSide.TheirFavor:
                    theirs = HistoryRole.Did;
                    ours = HistoryRole.For;
                    break;
                default:
                    theirs = HistoryRole.Did;
                    ours = HistoryRole.Against;
                    break;
            }
            try
            {
                var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(f), theirs), new HistoryActor(Refs.Seat, ours) };
                Add(HistoryKinds.OfDeed(deedKind), actors, Ref.None, HistoryMath.OfDeed(weight), null, "CC_Memory_Deed_" + deedKind,
                    name.NullOrEmpty() ? null : new List<string> { name }, Math.Max(1, count), HistoryMath.MergeTicks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("history: a deed " + deedKind, e);
            }
        }

        /// <summary>One of the ledger's ties made or moved (made, kept, calledIn, broken, ended, putOff): who is bound, toward whom, and whom it concerns.</summary>
        public static void Tie(Tie t, string what, int tick = -1)
        {
            if (t == null || t.IsView || Book == null) return;
            var actors = new List<HistoryActor>
            {
                new HistoryActor(t.from, HistoryRole.Did),
                new HistoryActor(t.to, t.kind == ColonyCouncil.Tie.Kind.Grudge ? HistoryRole.Against : HistoryRole.For)
            };
            if (t.terms != null && !t.terms.about.IsNone) actors.Add(new HistoryActor(t.terms.about, HistoryRole.About));
            Add(HistoryKinds.OfTie(t.kind, what), actors, Ref.None, HistoryMath.OfTie(t.kind), tick: tick);
        }

        /// <summary>The history's word for a tie moving to a state ("putOff": called in, then given more time).</summary>
        public static string TieWord(Tie.State to, Tie.State from)
        {
            switch (to)
            {
                case ColonyCouncil.Tie.State.Kept: return "kept";
                case ColonyCouncil.Tie.State.CalledIn: return "calledIn";
                case ColonyCouncil.Tie.State.Broken: return "broken";
                case ColonyCouncil.Tie.State.Ended: return "ended";
                default: return from == ColonyCouncil.Tie.State.CalledIn ? "putOff" : "made";
            }
        }

        // ---------------- What a chronicle line is part of ----------------

        private sealed class Note
        {
            public string kind;
            public Ref place;
            public List<HistoryActor> actors;
        }

        private static readonly List<Note> notes = new List<Note>();

        /// <summary>While it lasts, chronicle lines written are part of this: a kind within theirs ("world:began" for a world line), a place, the actors first.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly int depth;

            internal Scope(int depth)
            {
                this.depth = depth;
            }

            public void Dispose()
            {
                if (notes.Count >= depth && depth > 0) notes.RemoveRange(depth - 1, notes.Count - depth + 1);
            }
        }

        public static Scope About(string kind, Ref place, List<HistoryActor> actors)
        {
            notes.Add(new Note { kind = kind, place = place, actors = actors ?? new List<HistoryActor>() });
            return new Scope(notes.Count);
        }

        /// <summary>
        /// A world situation's chronicle line ("began", "ended"): at the situation's place, with its people, its enemy and
        /// its own person (a warlord) before the colonist the line names.
        /// </summary>
        public static Scope Situation(WorldSituation s, string what)
        {
            var actors = new List<HistoryActor>();
            var place = Ref.None;
            try
            {
                var ctx = s?.ctx;
                if (ctx != null)
                {
                    if (ctx.faction != null && !ctx.faction.IsPlayer) actors.Add(new HistoryActor(Refs.Of(ctx.faction), HistoryRole.About));
                    if (ctx.enemy != null && !ctx.enemy.IsPlayer) actors.Add(new HistoryActor(Refs.Of(ctx.enemy), HistoryRole.Against));
                    if (ctx.stranger != null) actors.Add(new HistoryActor(Refs.Of(ctx.stranger), HistoryRole.About));
                }
                if (s != null) place = s.place;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("history: a world situation's part", e);
            }
            return About("world:" + what, place, actors);
        }
    }
}
