using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A colonist's own name for their deeds: their renown read alone, the part they are known by, and with whom (the people a deed was done with or for) and what (a contest, a title).</summary>
    public sealed class OwnName
    {
        public RenownMath.Reading reading;
        /// <summary>The part they are known by (RenownMath.KnownAs), or null while nobody outside speaks of them.</summary>
        public string part;
        /// <summary>The people their best-known deed was done with or for, when it names one.</summary>
        public Ref people;
        /// <summary>What it was: a contest's kind, the colony's name for them (an epithet's defName), an office's record, a title's label, a notable's name.</summary>
        public string detail;
    }

    /// <summary>
    /// The colony's renown (docs/design/stage2-diplomacy.md, item 12; built in release 2d.1 as house renown and reshaped by the
    /// player's decision of 5 November 2026: the colony is the family, never a dynasty; decision 17: earned and shown in
    /// words, with a "known for", nothing spent). The colony's name among the peoples: how far it carries and what it is
    /// known for, built from everything its people do. Renown is never stored: it is read when asked (<see cref="Read"/>, kept
    /// for the in-game hour) from what the world keeps, the rules in RenownMath:
    /// <list type="bullet">
    /// <item>the history's deeds of the colony's people, the living and the gone (a champion at a feast, a marriage with
    /// another people, a ward fostered well, a season abroad as youth, envoy or hostage, a truce sworn as speaker);</item>
    /// <item>what the peoples remember of them by name (World/ColonistMemory.cs: a camp broken, fighting beside theirs,
    /// trading at their post, a prisoner freed, one of theirs tended; the wrongs);</item>
    /// <item>what the living hold now: the colony's names for them (Epithets/), years in an office, terms as speaker, the
    /// Empire's titles (read, never given), notables who think well of them;</item>
    /// <item>the colony's own deeds (an oath or a truce sworn, its word kept, levies sent, a pact kept; a truce or a pact
    /// broken, an oath renounced, the colony's scheme, a call refused).</item>
    /// </list>
    /// Each colonist's own name is read the same way from their own deeds alone (<see cref="Own"/>) and said by the part they
    /// are best known for, on their card and in talk with a visitor whose people know it. What it does: a band change of the
    /// colony's is told and written (dip:renown, a milestone), with a word from one of its people on the map; the notables
    /// think up to 15 better of its people (Notables.OpinionOf), good words for them count up to half again, matches with
    /// them are offered and accepted up to twice as readily, and what it is known for tips the world's events (the champion's
    /// challenge, the sick and the hurt, guests, the far market, work remembered, pilgrims) and a feast's guests' answer; a
    /// colonist known by name as a fighter draws the champion's challenge, and one known for something draws its events a
    /// little. Its words: Diplomacy/RenownWords.cs; its page, card lines and rows: RenownPanels.cs; its facts: FactsRenown.cs.
    /// The houses of release 2d.1 are gone: an older save's (LedgerHouse records marked ours) are dropped as it loads.
    /// </summary>
    public static class Renown
    {
        /// <summary>The "Colony renown" setting (Diplomacy), with the peoples' memory it reads.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.diplomacyRenown ?? true) && FactionMemory.On && WorldLedger.Instance != null;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Days(int tick) => Math.Max(0f, (Now - tick) / (float)GenDate.TicksPerDay);

        // ================= The colony's people =================

        private static readonly List<Pawn> people = new List<Pawn>();
        private static readonly HashSet<int> peopleIds = new HashSet<int>();
        private static int peopleAt = int.MinValue;
        private static Game peopleGame;

        /// <summary>
        /// The colony's people whose renown counts now: its free colonists (not a ward or a hostage of theirs living here as
        /// a lodger: they are their people's), and those away at another people's town on its errand (its envoy, youth or
        /// hostage, kept in the world as lent). Found again each hour.
        /// </summary>
        public static List<Pawn> People()
        {
            int now = Now;
            if (peopleGame == Current.Game && peopleAt != int.MinValue && now >= peopleAt && now - peopleAt < GenDate.TicksPerHour) return people;
            peopleGame = Current.Game;
            peopleAt = now;
            people.Clear();
            peopleIds.Clear();
            try
            {
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                    if (p != null && !p.IsQuestLodger() && peopleIds.Add(p.thingIDNumber))
                        people.Add(p);
                foreach (var p in Find.WorldPawns?.AllPawnsAlive.ToList() ?? new List<Pawn>())
                    if (p != null && !peopleIds.Contains(p.thingIDNumber) && KinMemory.Ours(p) && WorldLedger.KeptFor(p, KeepFor.Lent) && peopleIds.Add(p.thingIDNumber))
                        people.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: the colony's people", e);
            }
            return people;
        }

        /// <summary>One of the colony's people whose renown counts (a free colonist, or one away on its errand).</summary>
        public static bool Ours(Pawn p)
        {
            if (p == null) return false;
            People();
            return peopleIds.Contains(p.thingIDNumber);
        }

        /// <summary>The colony's renown and its people's names are to be read again (a dev action, a band told).</summary>
        public static void Dirty()
        {
            peopleAt = int.MinValue;
            colony = null;
            own.Clear();
        }

        // ================= The day =================

        /// <summary>
        /// Once a day (the diplomacy's day): the colony's band read afresh against the one last told; a change is told and
        /// written. The first look (a new game, an older save with houses) sets it without a word.
        /// </summary>
        public static void Daily(int now)
        {
            if (!On) return;
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            Dirty();
            try
            {
                var r = Compute(RenownMath.Told(book.renownBand));
                if (book.renownBand == RenownMath.NotRead)
                {
                    book.renownBand = (int)r.band;
                    return;
                }
                if (!RenownMath.MovedFrom(book.renownBand, r.band)) return;
                var was = RenownMath.Told(book.renownBand);
                book.renownBand = (int)r.band;
                Told(was, r);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: the colony's day", e);
            }
        }

        /// <summary>Dev: the colony's name told a band up or down from where it stands now (the same words, history and bubble as the day's look).</summary>
        public static void TellStep(bool up)
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            Dirty();
            var r = Compute(RenownMath.Told(book.renownBand));
            var was = r.band;
            int to = (int)was + (up ? 1 : -1);
            if (to > (int)RenownBand.Peoples || to < (int)RenownBand.Infamous) return;
            r.band = (RenownBand)to;
            book.renownBand = to;
            Told(was, r);
        }

        /// <summary>The colony's name moved a band: into the history (dip:renown, a milestone), a word from one of its people on the map, and the news.</summary>
        private static void Told(RenownBand was, RenownMath.Reading r)
        {
            var home = Homes.Seat;
            bool up = r.band > was;
            string line = RenownWords.Moved(r, up);
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did) };
            Diplomacy.Write(DiplomacyMath.Renown, actors, Diplomacy.Colony(home), line, true);
            // One of the colony hears of it on the map and says so, to nobody: awake and free, and only where the bubble's
            // words are true (RenownMath.Bubble).
            string moment = RenownMath.Bubble(was, r.band);
            var speaker = moment == null || home == null ? null
                : home.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake() && !p.DevelopmentalStage.Baby() && Ours(p)).RandomElementWithFallback();
            // apart: the news of the colony's name told in the region, said by one of its people where they stand; a name
            // fallen from the region's band or wider may be said to have been sung (RenownMath.BubbleTag).
            if (speaker != null) Outsiders.Say(speaker, moment, null, null, RenownMath.BubbleTag(was, r.band));
            Tell.Minor(NewsKind.Colony, Importance.Notable, home, line, speaker != null ? new LookTargets(speaker) : LookTargets.Invalid,
                up ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
            // This line is the colony's one name moving (Character/ColonyName.cs), with what it is known for: not told again.
            ColonyName.Settle();
        }

        // ================= Reading it =================

        private static RenownMath.Reading colony;
        private static readonly Dictionary<int, OwnName> own = new Dictionary<int, OwnName>();
        private static int readsHour = -1;
        private static Game readsGame;

        private static void Fresh()
        {
            int hour = Now / GenDate.TicksPerHour;
            if (hour == readsHour && readsGame == Current.Game) return;
            readsHour = hour;
            readsGame = Current.Game;
            colony = null;
            own.Clear();
        }

        /// <summary>The colony's renown as it stands, read at most once an in-game hour (null without a game or with it off).</summary>
        public static RenownMath.Reading Read()
        {
            if (!On) return null;
            Fresh();
            if (colony != null) return colony;
            try
            {
                colony = Compute(RenownMath.Told(DiplomacyBook.Instance?.renownBand ?? RenownMath.NotRead));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: reading the colony's", e);
                colony = RenownMath.Read(null);
            }
            return colony;
        }

        /// <summary>The colony's renown for one of its people (its name is theirs), or null for anyone else or with it off.</summary>
        public static RenownMath.Reading OfPawn(Pawn p) => Ours(p) ? Read() : null;

        private static bool IsColony(Ref r) => r.kind == RefKind.Colony || r.kind == RefKind.People && r == Refs.Of(Faction.OfPlayerSilentFail);

        private static RenownMath.Reading Compute(RenownBand previous)
        {
            var hits = new List<RenownMath.Hit>();
            var book = WorldLedger.Instance?.Book;
            var counted = new HashSet<int>();
            if (book != null)
            {
                // Its people's deeds the history keeps, the living and the gone: each event the colony is a party to, once.
                foreach (string kind in RenownMath.MemberKinds)
                    foreach (var e in book.OfKind(kind))
                    {
                        if (e == null || !counted.Add(e.id)) continue;
                        string key = OurPart(e, Ref.None);
                        if (key != null) hits.Add(new RenownMath.Hit(key, Days(e.tick), Math.Max(1, e.count)));
                    }
            }
            // What the peoples remember of them by name.
            foreach (var m in ColonistMemory.AllMarks())
            {
                string key = RenownMath.SourceOfMark(m.kind);
                if (key != null) hits.Add(new RenownMath.Hit(key, Days(m.tick), Math.Max(1, m.count)));
            }
            // What the living hold now.
            foreach (var p in People()) Held(p, hits, null);
            // The colony's own deeds, at every home and as a whole.
            var deeds = RenownMath.ColonyDeeds.Select(HistoryKinds.OfDeed).ToArray();
            var parties = Homes.All.Select(Diplomacy.Colony).Concat(new[] { Refs.Seat, Refs.Us, Refs.Of(Faction.OfPlayerSilentFail) }).Where(r => !r.IsNone).Distinct().ToList();
            foreach (var who in parties)
                foreach (var e in History.Of(who, deeds))
                {
                    if (e == null || !counted.Add(e.id) || !e.actors.Exists(a => a.who == who && a.role == HistoryRole.Did)) continue;
                    hits.Add(new RenownMath.Hit(e.kind.Substring(HistoryKinds.Deed.Length + 1), Days(e.tick), Math.Max(1, e.count)));
                }
            foreach (var who in parties)
                foreach (var e in History.Of(who, new[] { HistoryKinds.OfTie(Tie.Kind.Pact, "kept") }))
                    if (e != null && counted.Add(e.id))
                        hits.Add(new RenownMath.Hit("pactKept", Days(e.tick)));
            return RenownMath.Read(hits, previous);
        }

        /// <summary>
        /// The part an event of the history gives the colony (or, given <paramref name="person"/>, that one of its people): the
        /// colony is a party to it, the one named is the colony's own in it (a contest's champion only when the colony won),
        /// and their role in it is a deed (RenownMath.SourceOf). Null for none.
        /// </summary>
        private static string OurPart(HistoryEntry e, Ref person)
        {
            var party = e.actors.FirstOrDefault(a => IsColony(a.who));
            if (party.who.IsNone || !RenownMath.OursIn(e.kind, party.role)) return null;
            bool against = e.actors.Exists(a => a.role == HistoryRole.Against && a.who.kind == RefKind.Person);
            bool forColony = e.actors.Exists(a => a.role == HistoryRole.For && IsColony(a.who));
            foreach (var a in e.actors)
            {
                if (a.who.kind != RefKind.Person || !person.IsNone && a.who != person) continue;
                bool about = e.actors.Exists(x => x.role == HistoryRole.About && x.who != a.who);
                string key = RenownMath.SourceOf(e.kind, a.role, against, about, e.Weighty, forColony);
                if (key != null) return key;
            }
            return null;
        }

        /// <summary>What one of the colony's living people holds now: the colony's names for them, years in office, terms as speaker, a title of the Empire, notables who think well of them (with what each was, for their own name).</summary>
        private static void Held(Pawn p, List<RenownMath.Hit> hits, Dictionary<string, string> details)
        {
            var ep = CouncilGameComponent.Instance?.epithets;
            if (ep != null)
            {
                foreach (var d in ep.Of(p))
                    if (d != null && !d.dubious)
                        hits.Add(new RenownMath.Hit("epithet", 0f, 1f, RenownMath.EpithetFor(d.defName)));
                // Said abroad by the one name they are known by at home (KnownAs's short form; a proud one comes first).
                var chief = details != null ? ep.Chief(p) : null;
                if (chief != null && !chief.dubious) details["epithet"] = chief.label;
            }
            if (p.records != null)
            {
                foreach (var kv in RenownMath.Offices)
                {
                    var def = DefDatabase<RecordDef>.GetNamedSilentFail(kv.Key);
                    int years = def != null ? RenownMath.YearsOf(p.records.GetValue(def)) : 0;
                    if (years <= 0) continue;
                    hits.Add(new RenownMath.Hit("office", 0f, years, kv.Value));
                    if (details != null && !details.ContainsKey("office")) details["office"] = kv.Key;
                }
                var terms = DefDatabase<RecordDef>.GetNamedSilentFail("Council_SpeakerTerms");
                if (terms != null && p.records.GetValue(terms) >= 1f) hits.Add(new RenownMath.Hit("speaker", 0f, (float)Math.Floor(p.records.GetValue(terms))));
            }
            int seniority = Seniority(p);
            if (seniority > 0)
            {
                hits.Add(new RenownMath.Hit("title", 0f, RenownMath.TitlePoints(seniority)));
                if (details != null) details["title"] = TitleOf(p);
            }
            var known = KnownTo(p);
            if (known.Count > 0)
            {
                hits.Add(new RenownMath.Hit("regard", 0f, known.Count));
                if (details != null) details["regard"] = NotableBook.Called(known[0]);
            }
        }

        /// <summary>The Empire's title they hold, by its seniority (0 for none, or without Royalty).</summary>
        public static int Seniority(Pawn p)
        {
            try
            {
                var titles = p?.royalty?.AllTitlesForReading;
                if (titles == null || titles.Count == 0) return 0;
                return titles.Max(t => t?.def?.seniority ?? 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a title", e);
                return 0;
            }
        }

        /// <summary>Their most senior title of the Empire in its own words ("baron"), or null.</summary>
        private static string TitleOf(Pawn p)
        {
            try
            {
                var t = p?.royalty?.AllTitlesForReading?.Where(x => x?.def != null).OrderByDescending(x => x.def.seniority).FirstOrDefault();
                return t?.def.GetLabelFor(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a title's name", e);
                return null;
            }
        }

        /// <summary>The notables who think well of one of the colony (their own regard of them, RenownMath.KnownToRegard or more).</summary>
        public static List<Notable> KnownTo(Pawn p)
        {
            var list = new List<Notable>();
            var book = NotableBook.Instance;
            if (book == null || p == null) return list;
            foreach (var n in book.notables)
                if (n != null && n.Alive && (n.RegardOf(p, false)?.personal ?? 0f) >= RenownMath.KnownToRegard)
                    list.Add(n);
            return list;
        }

        // ================= Each colonist's own name =================

        /// <summary>A colonist's own name, read from their own deeds alone at most once an in-game hour (null for anyone not of the colony, or with it off).</summary>
        public static OwnName Own(Pawn p)
        {
            if (!On || !Ours(p)) return null;
            Fresh();
            if (own.TryGetValue(p.thingIDNumber, out var o)) return o;
            try
            {
                o = ComputeOwn(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a colonist's own name", e);
                o = new OwnName { reading = RenownMath.Read(null) };
            }
            own[p.thingIDNumber] = o;
            return o;
        }

        private static OwnName ComputeOwn(Pawn p)
        {
            var hits = new List<RenownMath.Hit>();
            var r = Refs.Of(p);
            // For each part, the newest deed's people and what it was (a contest's kind), for the words of their name.
            var with = new Dictionary<string, (Ref people, string detail, int tick)>();
            void Note(string key, Ref f, string detail, int tick)
            {
                if (!with.TryGetValue(key, out var was) || tick > was.tick) with[key] = (f, detail, tick);
            }
            foreach (var e in History.Of(r, RenownMath.MemberKinds))
            {
                if (e == null) continue;
                string key = OurPart(e, r);
                if (key == null) continue;
                hits.Add(new RenownMath.Hit(key, Days(e.tick), Math.Max(1, e.count)));
                var f = e.actors.FirstOrDefault(a => a.who.kind == RefKind.People && !IsColony(a.who)).who;
                Note(key, f, e.key, e.tick);
            }
            // What the peoples remember of them by name: the people who keep the most of it.
            var kept = new Dictionary<string, Dictionary<Ref, float>>();
            foreach (var (f, m) in ColonistMemory.MarksOf(r))
            {
                string key = RenownMath.SourceOfMark(m.kind);
                if (key == null) continue;
                hits.Add(new RenownMath.Hit(key, Days(m.tick), Math.Max(1, m.count)));
                if (!kept.TryGetValue(key, out var by)) kept[key] = by = new Dictionary<Ref, float>();
                by[f] = (by.TryGetValue(f, out var v) ? v : 0f) + Math.Max(1, m.count);
            }
            foreach (var kv in kept)
                if (!with.ContainsKey(kv.Key))
                    with[kv.Key] = (kv.Value.OrderByDescending(x => x.Value).First().Key, null, 0);
            var details = new Dictionary<string, string>();
            if (!p.Dead) Held(p, hits, details);
            var reading = RenownMath.Read(hits);
            var o = new OwnName { reading = reading, part = RenownMath.KnownAs(reading) };
            if (o.part != null)
            {
                if (with.TryGetValue(o.part, out var w))
                {
                    o.people = w.people;
                    o.detail = w.detail;
                }
                if (details.TryGetValue(o.part, out var d)) o.detail = d;
            }
            return o;
        }

        /// <summary>Whether the people of the one spoken to know a colonist's own name (RenownMath.TheyKnow).</summary>
        public static bool KnownAmong(OwnName o, Faction f)
        {
            if (o?.part == null || f == null) return false;
            bool theirs = !o.people.IsNone && o.people == Refs.Of(f);
            return RenownMath.TheyKnow(o.reading.band, theirs, RenownMath.NamesPeople(o.part) && !o.people.IsNone);
        }

        // ================= What it does =================

        /// <summary>How much better (or worse) a notable thinks of one of the colony for its name (Notables.OpinionOf).</summary>
        public static float RegardBonus(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 0f : RenownMath.RegardBonus(r.score);
        }

        /// <summary>How much more a good word for one of the colony counts for its name (OutsiderOutcomes.GoodWord).</summary>
        public static float GoodWordFactor(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 1f : RenownMath.GoodWordFactor(r.score);
        }

        /// <summary>How much more readily a match with one of the colony is taken up for its name (Matches).</summary>
        public static float MatchFactor(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 1f : RenownMath.MatchFactor(r.score);
        }

        /// <summary>
        /// An event's factor at a home for what the colony is known for (Happenings, IncidentWorker_Happening), or for what one
        /// of its people there is known for by name (a quarter more at most); the best of them, 1 when none draws it.
        /// </summary>
        public static float EventFactor(IncidentDef def, Map home)
        {
            if (def == null || !On) return 1f;
            try
            {
                var r = Read();
                float f = 1f;
                if (r != null && r.known != KnownFor.None && r.band >= RenownBand.Town)
                    f = RenownMath.EventFactor(def.defName, new[] { new KeyValuePair<KnownFor, RenownBand>(r.known, r.band) });
                var map = home != null && Homes.IsHome(home) ? home : Homes.Seat;
                if (map != null)
                {
                    var named = new List<KnownFor>();
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    {
                        var o = Own(p);
                        if (o?.part != null && o.reading.band >= RenownBand.Town && o.reading.known != KnownFor.None) named.Add(o.reading.known);
                    }
                    f = Math.Max(f, RenownMath.OwnFactor(def.defName, named));
                }
                return f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: an event's factor", e);
                return 1f;
            }
        }

        /// <summary>A feast's guests accept a little more readily at a colony known for hosting (Feasts).</summary>
        public static float GuestFactor(Map home)
        {
            var r = Read();
            return r != null && r.known == KnownFor.Hosts ? RenownMath.GuestFactor(r.band) : 1f;
        }

        /// <summary>The travelling champion's choice among the colony's best: one known by name as a fighter, when one of them is among them (HappeningWorker_Champion).</summary>
        public static bool KnownFighter(Pawn p)
        {
            var o = Own(p);
            return o?.part != null && RenownMath.ChallengesFighter(o.reading.known, o.reading.band);
        }

        /// <summary>The colony's tags for a bubble (Outsiders.Line): colony:known, colony:renowned, colony:knownfor:KIND; nothing while it is not yet known.</summary>
        public static void AddTags(HashSet<string> tags, Pawn colonist)
        {
            if (tags == null || colonist == null || !On || !Ours(colonist)) return;
            try
            {
                var r = Read();
                if (r != null) tags.UnionWith(RenownMath.ColonyTags(r.band, r.known));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a bubble's tags", e);
            }
        }
    }
}
