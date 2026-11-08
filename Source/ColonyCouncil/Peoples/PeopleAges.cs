using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of Rimfolk's peoples' age as it changes over the years (Peoples/PeopleAges.cs): the age it lives in now, the
    /// age it began in, its way toward the next age (+1) or back to the one before (-1), which way it was heading at its
    /// last look and why, and when it last changed.
    /// </summary>
    public class PeopleAgeRecord : IExposable
    {
        public Faction people;
        public int age = -1, start = -1;
        public float progress;
        /// <summary>When the colony first heard of it (its record began), its last look, and its last change of age (-1: none).</summary>
        public int made = -1, looked = -1, changed = -1;
        /// <summary>The age before its last change.</summary>
        public int was = -1;
        /// <summary>Which way it was heading at its last look (+1 rising, -1 falling back, 0 settled), and why (PeopleAgesMath.Reasons, the strongest first).</summary>
        public int heading;
        public bool blocked;
        public string why;

        public bool Standing => people != null && !people.defeated;

        public List<string> Why => why.NullOrEmpty() ? new List<string>() : why.Split(',').Where(w => w.Length > 0).ToList();

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref age, "age", -1);
            Scribe_Values.Look(ref start, "start", -1);
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref made, "made", -1);
            Scribe_Values.Look(ref looked, "looked", -1);
            Scribe_Values.Look(ref changed, "changed", -1);
            Scribe_Values.Look(ref was, "was", -1);
            Scribe_Values.Look(ref heading, "heading");
            Scribe_Values.Look(ref blocked, "blocked");
            Scribe_Values.Look(ref why, "why");
        }
    }

    /// <summary>
    /// The peoples' ages as they change, kept with the game: one record a people of Rimfolk's, made the first time the
    /// region's day looks at it, at most <see cref="PeopleAgesMath.MaxRecords"/> (a people gone from the world goes first).
    /// A save from before has none: each people begins in the age it lives in.
    /// </summary>
    public class PeopleAgeBook : GameComponent
    {
        public List<PeopleAgeRecord> ages = new List<PeopleAgeRecord>();

        private static PeopleAgeBook cached;
        private static Game cachedGame;
        private Dictionary<int, PeopleAgeRecord> byId;

        public PeopleAgeBook(Game game)
        {
        }

        public static PeopleAgeBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleAgeBook>();
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
            byId = null;
        }

        /// <summary>The record of a people, or null.</summary>
        public PeopleAgeRecord Of(Faction f)
        {
            if (f == null) return null;
            if (byId == null)
            {
                byId = new Dictionary<int, PeopleAgeRecord>();
                foreach (var r in ages)
                    if (r?.people != null)
                        byId[r.people.loadID] = r;
            }
            return byId.TryGetValue(f.loadID, out var rec) && rec.people == f ? rec : null;
        }

        public void Add(PeopleAgeRecord r)
        {
            if (r?.people == null) return;
            ages.Add(r);
            while (ages.Count > PeopleAgesMath.MaxRecords)
            {
                var old = ages.Where(x => x == null || !x.Standing).OrderBy(x => x?.made ?? -1).FirstOrDefault() ?? ages.OrderBy(x => x.made).First();
                ages.Remove(old);
            }
            byId = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // A people the game let go leaves nothing behind that can't be found on load.
            if (Scribe.mode == LoadSaveMode.Saving && Find.FactionManager != null)
                ages.RemoveAll(r => r?.people == null || !Find.FactionManager.AllFactionsListForReading.Contains(r.people));
            Scribe_Collections.Look(ref ages, "ages", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ages ??= new List<PeopleAgeRecord>();
                ages.RemoveAll(r => r?.people == null || r.age < 0);
                byId = null;
            }
        }
    }

    /// <summary>
    /// Peoples whose age changes (docs/design/peoples-third.md, "Ages that change over time"; DESIGN.md, "Peoples whose
    /// age changes"). Once a season, each of Rimfolk's peoples (and each that arose in their ways) is looked at on a day of
    /// its own, on the region's day (RegionBook.Daily; no Sched slot), and moves toward its next age or back by the one
    /// rule (<see cref="PeopleAgesMath.Season"/>), from what Rimfolk already records over the past year: the colony's
    /// trade with it (the trade record, and the peoples' memory of fair trades), its gifts of books, techprints,
    /// components and tools (the memory's "gaveLearning"), theirs who learned at the colony (apprentices hosted, wards
    /// fostered) and their masters who taught the colony's people (the history's deeds), an alliance or a pact; and for
    /// falling back, the last trade anyone had with it (the colony, or a trade or road world event of theirs), its towns
    /// that fell in a fight (the ledger's ruins) and its wars with the world's peoples. A change of age is told in a
    /// letter in its own voice (PeopleAgeWords) and written into the history; <see cref="Peoples.AgeOf"/> reads the age,
    /// so their gear, arms, gifts, camps, words, messengers and traders' goods follow.
    /// </summary>
    public static class PeopleAges
    {
        /// <summary>The setting "Peoples' ages change": off, every people lives in the age it began in, and nothing moves.</summary>
        public static bool On => ColonyCouncilMod.Settings?.peopleAges ?? true;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The age a people lives in now, given the age it began in: its record's, with the setting on.</summary>
        public static int AgeOf(Faction f, int began)
        {
            if (!On) return began;
            var r = PeopleAgeBook.Instance?.Of(f);
            return r != null && r.age >= 0 ? r.age : began;
        }

        /// <summary>A people of Rimfolk's in the world, its record made the first time it is asked for.</summary>
        public static PeopleAgeRecord Ensure(Faction f)
        {
            var book = PeopleAgeBook.Instance;
            if (book == null || f == null || f.defeated || !Peoples.IsOurs(f)) return null;
            var r = book.Of(f);
            if (r != null) return r;
            int began = Peoples.BeganIn(f);
            r = new PeopleAgeRecord { people = f, age = began, start = began, progress = PeopleAgesMath.HeadStart(f.loadID), made = Now, heading = 1, why = "years" };
            book.Add(r);
            return r;
        }

        /// <summary>A people's ways for its record: its template's, from the age it began in.</summary>
        public static AgeWays Ways(Faction f, PeopleAgeRecord r)
        {
            var ext = Peoples.Of(f);
            return ext?.Ways(r?.start ?? Peoples.BeganIn(f)) ?? new AgeWays { pace = 12f, limit = 2, floor = 0 };
        }

        // ================= The region's day =================

        /// <summary>Each people on its own day of the season: its record made if it has none, and its look.</summary>
        public static void Daily(int now)
        {
            var book = PeopleAgeBook.Instance;
            if (book == null || Find.FactionManager == null) return;
            int day = now / PeopleAgesMath.DayTicks;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.ToList())
            {
                if (f == null || f.defeated || f.IsPlayer || !Peoples.IsOurs(f)) continue;
                var r = Ensure(f);
                if (r == null || !PeopleAgesMath.LookDay(day, f.loadID)) continue;
                if (r.looked >= 0 && now - r.looked < (PeopleAgesMath.SeasonDays - 1) * PeopleAgesMath.DayTicks) continue;
                try
                {
                    Look(f, r, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' ages: a people's look", e);
                }
            }
        }

        /// <summary>A season of a people's life: the rule, and a letter when its age changes.</summary>
        public static AgeStep Look(Faction f, PeopleAgeRecord r, int now)
        {
            var step = PeopleAgesMath.Season(r.age, r.progress, Ways(f, r), PeopleAgesMath.Fate(f.loadID), Deeds(f, r, now));
            r.looked = now;
            r.progress = step.progress;
            r.heading = step.heading;
            r.blocked = step.blocked;
            r.why = step.why;
            if (step.change != 0) Change(f, r, step.age, step.Why());
            return step;
        }

        /// <summary>The age changes (by the years, or by the dev tools): the record, the letter in their voice, the history.</summary>
        public static void Change(Faction f, PeopleAgeRecord r, int age, List<string> why)
        {
            if (r == null || age == r.age || age < 0 || age > 3) return;
            r.was = r.age;
            r.age = age;
            r.progress = 0f;
            r.changed = Now;
            try
            {
                PeopleAgeWords.Tell(f, r, why);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: the letter", e);
            }
        }

        // ================= What Rimfolk records =================

        /// <summary>The names the trade record knows a people by: its own, and each of its towns'.</summary>
        private static HashSet<string> TradeNames(Faction f)
        {
            var names = new HashSet<string> { TradeMarketMath.Who(f.Name) };
            foreach (var s in Find.WorldObjects.Settlements)
                if (s != null && s.Faction == f)
                {
                    names.Add(TradeMarketMath.Who(s.Label));
                    if (s.HasName) names.Add(TradeMarketMath.Who(s.Name));
                }
            return names;
        }

        /// <summary>The colony's trade with a people since then, in silver: the trade record's lines with them, or at least what the peoples' memory counts.</summary>
        public static float Trade(Faction f, int since, out int last, out float parts)
        {
            last = -1;
            parts = 0f;
            float silver = 0f;
            var book = TradeBook.Instance;
            if (book != null)
            {
                var names = TradeNames(f);
                foreach (var g in book.goods)
                {
                    if (g?.lines == null) continue;
                    bool part = g.def != null && PeopleAgesMath.Part(PeopleAgeGifts.KindOf(g.def));
                    foreach (var l in g.lines)
                    {
                        if (l == null || !names.Contains(l.who ?? "")) continue;
                        if (l.tick > last) last = l.tick;
                        if (l.tick < since) continue;
                        float v = Math.Max(0, l.count) * Math.Max(0f, l.price);
                        silver += v;
                        if (part && l.sold) parts += v;
                    }
                }
            }
            int deeds = 0;
            foreach (var e in History.Of(Refs.Of(f), new[] { HistoryKinds.OfDeed("traded") }))
            {
                if (e == null) continue;
                if (e.tick > last) last = e.tick;
                if (e.tick >= since) deeds += Math.Max(1, e.count);
            }
            return Math.Max(silver, deeds * PeopleAgesMath.TradeDeedSilver);
        }

        /// <summary>The colony's gifts of learning as the people remembers them, in silver.</summary>
        public static float Gifts(Faction f, int now)
        {
            var mem = FactionMemory.Instance;
            if (mem == null) return 0f;
            float s = 0f;
            foreach (var d in mem.deeds)
                if (d != null && d.faction == f && d.kind == PeopleAgeGifts.DeedKind)
                    s += d.StrengthAt(now);
            return s * PeopleAgesMath.GiftSilverPerWeight;
        }

        /// <summary>How many of these deeds the history holds with this people since then (a summary counts all it stands for).</summary>
        private static int Count(Faction f, string[] deeds, int since)
        {
            int n = 0;
            foreach (var d in deeds) n += History.Count(HistoryKinds.OfDeed(d), Refs.Of(f), default, since);
            return n;
        }

        /// <summary>The colony's bond with them: allied (the game's alliance, or a pact of defence) 1, a trade pact 0.5, else 0.</summary>
        public static float Bond(Faction f)
        {
            if (f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally) return 1f;
            if (!Pacts.On) return 0f;
            float b = 0f;
            foreach (var t in Pacts.With(f))
                b = Math.Max(b, Pacts.KindOf(t) == PactKind.Defence ? 1f : 0.5f);
            return b;
        }

        /// <summary>The last time a trade or road world event of theirs ran (now, while one runs), or -1.</summary>
        private static int TradeEvent(Faction f, int now)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return -1;
            foreach (var s in ws.Running)
                if (s?.ctx?.faction == f && TradeKind(s.def?.kind))
                    return now;
            int last = -1;
            foreach (var r in ws.history)
                if (r?.faction == f && r.endTick > last && TradeKind(DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "")?.kind))
                    last = r.endTick;
            return last;
        }

        private static bool TradeKind(string kind) => kind == "trade" || kind == "road";

        /// <summary>Everything the rule reads, over the past year (towns burned over two).</summary>
        public static AgeDeeds Deeds(Faction f, PeopleAgeRecord r, int now)
        {
            int year = now - PeopleAgesMath.YearTicks;
            var d = new AgeDeeds { trade = Trade(f, year, out int lastTrade, out float parts), supply = parts };
            d.gifts = Gifts(f, now);
            d.learners = Count(f, PeopleAgesMath.LearnerDeeds, year);
            d.lessons = Count(f, PeopleAgesMath.LessonDeeds, year);
            d.bond = Bond(f);
            int last = Math.Max(Math.Max(lastTrade, TradeEvent(f, now)), r?.made ?? -1);
            d.yearsAlone = last < 0 ? 0f : Math.Max(0, now - last) / (float)PeopleAgesMath.YearTicks;
            int burnedSince = now - PeopleAgesMath.BurnedYears * PeopleAgesMath.YearTicks;
            var ledger = WorldLedger.Instance;
            if (ledger != null)
                foreach (var ruin in ledger.Ruins)
                    if (ruin != null && ruin.people == f && ruin.why == LedgerRuin.Fight && ruin.since >= burnedSince)
                        d.burned++;
            d.towns = Find.WorldObjects.Settlements.Count(s => s != null && s.Faction == f);
            int peoples = 1, wars = f.HostileTo(Faction.OfPlayer) ? 1 : 0;
            foreach (var g in Find.FactionManager.AllFactionsListForReading)
            {
                if (g == f || !Facts.Counts(g)) continue;
                peoples++;
                if (f.HostileTo(g)) wars++;
            }
            d.war = wars / (float)peoples;
            return d;
        }

        // ================= For the dev tools =================

        /// <summary>A people up or down an age now, whatever its pace (dev mode): the letter as the years would bring it.</summary>
        public static bool Push(Faction f, int dir)
        {
            var r = Ensure(f);
            if (r == null) return false;
            int age = r.age + Math.Sign(dir);
            if (age < 0 || age > 3) return false;
            Change(f, r, age, new List<string> { dir > 0 ? "years" : "alone" });
            return true;
        }
    }

    public static class AgeStepWhy
    {
        /// <summary>The reasons of a step, the strongest first.</summary>
        public static List<string> Why(this AgeStep s) => s.why.NullOrEmpty() ? new List<string>() : s.why.Split(',').Where(w => w.Length > 0).ToList();
    }
}
