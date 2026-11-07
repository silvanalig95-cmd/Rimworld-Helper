using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>An animal's temperament, kept by its id once it was given one, and when it was last seen among the colony's.</summary>
    public class AnimalMark : IExposable
    {
        public int id;
        /// <summary>The temperament's number (<see cref="Temper"/>).</summary>
        public int temper;
        public int seen;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref temper, "temper");
            Scribe_Values.Look(ref seen, "seen");
        }
    }

    /// <summary>
    /// Two of the colony's animals: a pair seen asleep side by side and not yet friends, friends, or rivals. Kept by
    /// their ids (the game lets a dead animal's body go), with their names for the card when one of them is gone.
    /// </summary>
    public class AnimalTie : IExposable
    {
        public const int Pair = 0, Friends = 1, Rivals = 2;

        public int a, b;
        public string nameA, nameB;
        public int kind = Pair;
        /// <summary>Nights asleep side by side (a pair and friends), and the day the last one was counted.</summary>
        public int nights, lastNight = -1;
        /// <summary>When they became friends or rivals; when they were last seen near each other.</summary>
        public int since = -1, lastSeen = -1;
        /// <summary>One of them died: which, and when; the other misses it for a while.</summary>
        public int lostId, lostTick = -1;

        public bool Has(int id) => a == id || b == id;

        public int Other(int id) => a == id ? b : a;

        public string NameOf(int id) => a == id ? nameA : nameB;

        public bool Lost => lostTick >= 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref a, "a");
            Scribe_Values.Look(ref b, "b");
            Scribe_Values.Look(ref nameA, "nameA");
            Scribe_Values.Look(ref nameB, "nameB");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref nights, "nights");
            Scribe_Values.Look(ref lastNight, "lastNight", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref lastSeen, "lastSeen", -1);
            Scribe_Values.Look(ref lostId, "lostId");
            Scribe_Values.Look(ref lostTick, "lostTick", -1);
        }
    }

    /// <summary>One of the colony's named or bonded animals that left it alive (sold, given away, released, gone wild), with the one who handled it.</summary>
    public class AnimalGone : IExposable
    {
        public int id;
        public string name, kind;
        public Pawn handler;
        public int tick = -1;
        /// <summary>When it last came over to its handler again, and how often it has.</summary>
        public int metTick = -1, meetings;
        /// <summary>Released (it went wild), else sold or given to a people.</summary>
        public bool wild;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref handler, "handler");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref metTick, "metTick", -1);
            Scribe_Values.Look(ref meetings, "meetings");
            Scribe_Values.Look(ref wild, "wild");
        }
    }

    /// <summary>
    /// The colony's animals' natures, kept with the game (Animals/AnimalTempers.cs, AnimalBonds.cs): each named or bonded
    /// animal's temperament once given (<see cref="AnimalNatureMath.MaxMarks"/>, let go when unseen for
    /// <see cref="AnimalNatureMath.MarkForgetDays"/>), the pairs, friendships and rivalries between them
    /// (<see cref="AnimalNatureMath.MaxTies"/>, pairs first to go), and the animals that left alive with the one who
    /// handled them (<see cref="AnimalNatureMath.MaxGone"/>, oldest first, gone after they would have forgotten). Animals
    /// are kept by id, never by reference, so an animal the game has let go leaves nothing behind. A save from before has
    /// an empty one.
    /// </summary>
    public class AnimalBook : GameComponent
    {
        public List<AnimalMark> marks = new List<AnimalMark>();
        public List<AnimalTie> ties = new List<AnimalTie>();
        public List<AnimalGone> gone = new List<AnimalGone>();

        private static AnimalBook cached;
        private static Game cachedGame;

        public AnimalBook(Game game)
        {
        }

        public static AnimalBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<AnimalBook>();
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
            byId.Clear();
            foreach (var m in marks) if (m != null) byId[m.id] = m;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref gone, "gone", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                marks ??= new List<AnimalMark>();
                ties ??= new List<AnimalTie>();
                gone ??= new List<AnimalGone>();
                marks.RemoveAll(m => m == null);
                ties.RemoveAll(t => t == null || t.a == t.b);
                gone.RemoveAll(g => g == null);
                byId.Clear();
                foreach (var m in marks) byId[m.id] = m;
            }
        }

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- Temperaments ----------------

        [Unsaved] private readonly Dictionary<int, AnimalMark> byId = new Dictionary<int, AnimalMark>();

        /// <summary>The temperament kept for this animal, if it has been given one.</summary>
        public bool TryTemper(int id, out Temper t)
        {
            t = Temper.Curious;
            return byId.TryGetValue(id, out var m) && AnimalNatureMath.TryOf(m.temper, out t);
        }

        /// <summary>Keeps this animal's temperament (a new one, a dev action's, or one carried over).</summary>
        public void SetTemper(int id, Temper t)
        {
            if (!byId.TryGetValue(id, out var m))
            {
                m = new AnimalMark { id = id };
                marks.Add(m);
                byId[id] = m;
            }
            m.temper = (int)t;
            m.seen = Now;
            if (marks.Count > AnimalNatureMath.MaxMarks) TrimMarks();
        }

        /// <summary>A Pokémon evolved into a new one (PokéWorld makes a new animal): its temperament, friends and rivals go with it.</summary>
        public void Carry(Pawn before, Pawn after)
        {
            if (before == null || after == null || before == after) return;
            int from = before.thingIDNumber, to = after.thingIDNumber;
            if (TryTemper(from, out var t)) SetTemper(to, t);
            foreach (var tie in ties)
            {
                if (tie.a == from)
                {
                    tie.a = to;
                    tie.nameA = after.LabelShort;
                }
                else if (tie.b == from)
                {
                    tie.b = to;
                    tie.nameB = after.LabelShort;
                }
                if (tie.lostId == from) tie.lostId = to;
                // The lower id first, as every pair is kept.
                if (tie.a > tie.b)
                {
                    (tie.a, tie.b) = (tie.b, tie.a);
                    (tie.nameA, tie.nameB) = (tie.nameB, tie.nameA);
                }
            }
            ties.RemoveAll(x => x.a == x.b);
            foreach (var g in gone)
                if (g.id == from) g.id = to;
        }

        private void TrimMarks()
        {
            // The longest unseen go first.
            marks.Sort((x, y) => x.seen.CompareTo(y.seen));
            while (marks.Count > AnimalNatureMath.MaxMarks)
            {
                byId.Remove(marks[0].id);
                marks.RemoveAt(0);
            }
        }

        // ---------------- Ties ----------------

        public AnimalTie TieOf(int x, int y)
        {
            if (x == y) return null;
            int lo = Math.Min(x, y), hi = Math.Max(x, y);
            for (int i = 0; i < ties.Count; i++)
                if (ties[i].a == lo && ties[i].b == hi) return ties[i];
            return null;
        }

        /// <summary>The pair, made if it isn't kept yet (the lower id first).</summary>
        public AnimalTie TieFor(Pawn x, Pawn y)
        {
            var t = TieOf(x.thingIDNumber, y.thingIDNumber);
            if (t != null) return t;
            bool xFirst = x.thingIDNumber < y.thingIDNumber;
            t = new AnimalTie
            {
                a = xFirst ? x.thingIDNumber : y.thingIDNumber, b = xFirst ? y.thingIDNumber : x.thingIDNumber,
                nameA = (xFirst ? x : y).LabelShort, nameB = (xFirst ? y : x).LabelShort, lastSeen = Now
            };
            ties.Add(t);
            if (ties.Count > AnimalNatureMath.MaxTies) TrimTies();
            return t;
        }

        public IEnumerable<AnimalTie> TiesOf(int id, int kind) => ties.Where(t => t.kind == kind && t.Has(id));

        public int Count(int id, int kind) => ties.Count(t => t.kind == kind && !t.Lost && t.Has(id));

        private void TrimTies()
        {
            // Pairs not yet friends first, the longest unseen of them; then the oldest friendships and rivalries already lost.
            while (ties.Count > AnimalNatureMath.MaxTies)
            {
                var drop = ties.Where(t => t.kind == AnimalTie.Pair).OrderBy(t => t.lastSeen).FirstOrDefault()
                           ?? ties.Where(t => t.Lost).OrderBy(t => t.lostTick).FirstOrDefault()
                           ?? ties.OrderBy(t => t.lastSeen).First();
                ties.Remove(drop);
            }
        }

        // ---------------- Gone ----------------

        /// <summary>One of the colony's named or bonded animals left it alive: kept with its handler, for the day they meet again.</summary>
        public void Left(Pawn animal, Pawn handler, bool wild)
        {
            if (animal == null || handler == null) return;
            gone.RemoveAll(g => g.id == animal.thingIDNumber);
            gone.Add(new AnimalGone
            {
                id = animal.thingIDNumber, name = animal.LabelShort, kind = AnimalFacts.KindLabel(animal), handler = handler, tick = Now, wild = wild
            });
            while (gone.Count > AnimalNatureMath.MaxGone) gone.RemoveAt(0);
        }

        // ---------------- The day ----------------

        /// <summary>
        /// Once a day: the colony's animals seen are noted (on every map and with the caravans), temperaments unseen long
        /// let go; a tie one of whose animals died is marked lost when they were friends (the other misses it) and let go
        /// otherwise, one whose animal left the colony is let go, and the rest drift apart only by the days since the two
        /// were last seen together (so a Pokémon resting in its ball, or one away with a caravan, keeps its friends); an
        /// animal gone too long, or whose handler died, is forgotten.
        /// </summary>
        public void Daily()
        {
            int now = Now;
            var alive = new Dictionary<int, Pawn>();
            foreach (var map in Find.Maps)
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                    alive[a.thingIDNumber] = a;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c.IsPlayerControlled)
                    foreach (var p in c.PawnsListForReading)
                        if (p.RaceProps.Animal && !p.Dead) alive[p.thingIDNumber] = p;
            foreach (var m in marks)
                if (alive.ContainsKey(m.id)) m.seen = now;
            int before = marks.Count;
            marks.RemoveAll(m => !alive.ContainsKey(m.id) && now - m.seen > AnimalNatureMath.MarkForgetDays * GenDate.TicksPerDay);
            if (marks.Count != before)
            {
                byId.Clear();
                foreach (var m in marks) byId[m.id] = m;
            }

            var dead = DeadAnimalIds();
            var left = new HashSet<int>(gone.Select(g => g.id));
            foreach (var t in ties.ToList())
            {
                if (t.Lost)
                {
                    // Missed for a while; let go sooner if the one left behind has died or left too.
                    int survivor = t.Other(t.lostId);
                    if (now - t.lostTick > AnimalNatureMath.MissDays * GenDate.TicksPerDay || dead.Contains(survivor) || left.Contains(survivor)) ties.Remove(t);
                    continue;
                }
                bool deadA = dead.Contains(t.a), deadB = dead.Contains(t.b);
                if (deadA || deadB)
                {
                    // A friend that died is missed by the other, still living; anything else ends there.
                    if (t.kind == AnimalTie.Friends && deadA != deadB)
                    {
                        t.lostId = deadA ? t.a : t.b;
                        t.lostTick = now;
                    }
                    else ties.Remove(t);
                    continue;
                }
                if (left.Contains(t.a) || left.Contains(t.b))
                {
                    ties.Remove(t);
                    continue;
                }
                float apart = t.lastSeen < 0 ? 0f : (now - t.lastSeen) / (float)GenDate.TicksPerDay;
                if (apart > (t.kind == AnimalTie.Pair ? AnimalNatureMath.PairForgetDays : AnimalNatureMath.TieFadeDays)) ties.Remove(t);
            }

            gone.RemoveAll(g => g.handler == null || g.handler.Dead || now - g.tick > AnimalNatureMath.RemembersDays * GenDate.TicksPerDay);
        }

        /// <summary>
        /// The ids of the colony's animals that died lately, from the pets' watch (<see cref="AnimalBonds.Notify_Died"/>)
        /// and from the bodies still about: the corpses on the maps and in their graves.
        /// </summary>
        private static HashSet<int> DeadAnimalIds()
        {
            var ids = new HashSet<int>(AnimalBonds.RecentDead);
            foreach (var map in Find.Maps)
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    if (t is Corpse c && c.InnerPawn?.RaceProps?.Animal == true) ids.Add(c.InnerPawn.thingIDNumber);
            return ids;
        }
    }
}
