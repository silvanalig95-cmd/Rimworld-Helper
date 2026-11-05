using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// House renown (docs/design/stage2-diplomacy.md, item 12; release 2d.1; the player's decision 17: earned and shown in
    /// words, with a "known for", nothing spent). The colony's houses are its families known by name (two or more related
    /// colonists of one last name, Social/FamilyNames.cs), kept on the world ledger as <see cref="LedgerHouse"/> records of
    /// their own (twelve at most, World/Ledger/LedgerRenown.cs) with the people who were ever of them; the region's houses
    /// are the towns' founding families the ledger already keeps. Renown is never stored: it is read when asked
    /// (<see cref="Read"/>, kept for the hour) from what the world keeps of a house's people, the rules in RenownMath:
    /// <list type="bullet">
    /// <item>the history's deeds that name them (a champion at a feast, a marriage into their houses, a ward fostered well,
    /// a season abroad as youth, envoy or hostage, a truce sworn as speaker, a task done, a secret told);</item>
    /// <item>what the peoples remember of them by name (World/ColonistMemory.cs: a camp broken, fighting beside theirs,
    /// trading at their post, a prisoner freed, one of theirs tended; the wrongs);</item>
    /// <item>what they hold now: the colony's names for them (Epithets/), years in an office, terms as speaker, the
    /// Empire's titles (read, never given), notables who think well of them;</item>
    /// <item>the colony's own deeds, shared by each of its houses (an oath or a truce sworn, its word kept, levies sent, a
    /// pact kept; a truce or a pact broken, an oath renounced, a secret told, a call refused);</item>
    /// <item>for theirs: a founding family, a head's or a people's leader's house, their ties with the colony, the town's
    /// fortune, what their head is best at.</item>
    /// </list>
    /// What it does: a band change of one of the colony's houses is told and written (dip:renown, a milestone), with a word
    /// from one of its people on the map; the notables think up to 15 better of a renowned house's people (Notables.OpinionOf),
    /// good words for them count up to half again, matches with them are offered and accepted up to twice as readily, and
    /// what a house is known for tips the world's events (the champion's challenge, the sick and the hurt, guests, the far
    /// market, work remembered, pilgrims) and a feast's guests' answer. Its words: Diplomacy/RenownWords.cs; its cards and
    /// rows: RenownPanels.cs; its facts: FactsRenown.cs.
    /// </summary>
    public static class Renown
    {
        /// <summary>The "House renown" setting (Diplomacy), with the peoples' memory it reads.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.diplomacyRenown ?? true) && FactionMemory.On && WorldLedger.Instance != null;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Days(int tick) => Math.Max(0f, (Now - tick) / (float)GenDate.TicksPerDay);

        // ================= The colony's houses =================

        private static readonly Dictionary<int, List<Pawn>> living = new Dictionary<int, List<Pawn>>();
        private static readonly Dictionary<int, LedgerHouse> byPawn = new Dictionary<int, LedgerHouse>();
        private static int indexAt = int.MinValue;
        private static Game indexGame;

        private static readonly List<Pawn> None = new List<Pawn>();

        /// <summary>Who of the colony's free people belongs to which of its houses, found again each hour (or when the houses change).</summary>
        private static void Index()
        {
            int now = Now;
            if (indexGame == Current.Game && indexAt != int.MinValue && now >= indexAt && now - indexAt < GenDate.TicksPerHour) return;
            indexGame = Current.Game;
            indexAt = now;
            living.Clear();
            byPawn.Clear();
            var ours = WorldLedger.OurHouses();
            if (ours.Count == 0) return;
            // Each person with every house that remembers them: one who took another's name on marrying is in both.
            var of = new Dictionary<int, List<LedgerHouse>>();
            foreach (var h in ours)
                foreach (var r in h.folk)
                    if (r.kind == RefKind.Person)
                    {
                        if (!of.TryGetValue(r.id, out var hs)) of[r.id] = hs = new List<LedgerHouse>();
                        if (!hs.Contains(h)) hs.Add(h);
                    }
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                if (p != null && of.TryGetValue(p.thingIDNumber, out var hs))
                    Put(p, hs);
            foreach (var list in living.Values)
                list.Sort((a, b) => (b.ageTracker?.AgeBiologicalTicks ?? 0L).CompareTo(a.ageTracker?.AgeBiologicalTicks ?? 0L));
        }

        /// <summary>One of the colony's people into the house they are of now: the one of the name they carry, else the newest that remembers them.</summary>
        private static void Put(Pawn p, List<LedgerHouse> houses)
        {
            int i = RenownMath.HouseFor(houses.Select(x => x.name).ToList(), FamilyNames.Last(p));
            if (i < 0) return;
            var h = houses[i];
            if (!living.TryGetValue(h.house.id, out var list)) living[h.house.id] = list = new List<Pawn>();
            list.Add(p);
            byPawn[p.thingIDNumber] = h;
        }

        /// <summary>Someone a house remembers who is still one of the colony's, of this house or another (not among its gone).</summary>
        public static bool StillOurs(Ref r)
        {
            if (r.kind != RefKind.Person || WorldLedger.Instance == null) return false;
            Index();
            return byPawn.ContainsKey(r.id);
        }

        /// <summary>The colony's house a colonist is of, or null.</summary>
        public static LedgerHouse HouseOf(Pawn p)
        {
            if (p == null || WorldLedger.Instance == null) return null;
            Index();
            return byPawn.TryGetValue(p.thingIDNumber, out var h) ? h : null;
        }

        /// <summary>The colony's people of one of its houses who are still with it, the eldest first.</summary>
        public static List<Pawn> Living(LedgerHouse h)
        {
            if (h == null || !h.ours) return None;
            Index();
            return living.TryGetValue(h.house.id, out var l) ? l : None;
        }

        /// <summary>The colony's houses and the region's were changed or are to be read again (a dev action, a band told).</summary>
        public static void Dirty()
        {
            indexAt = int.MinValue;
            reads.Clear();
        }

        /// <summary>
        /// Once a day: the colony's families known by name become its houses (twelve at most) and each takes in its people
        /// (the newest twelve kept, the gone among them first to go), its eldest as head, its seat where most of it lives; a
        /// house with nobody left is kept while a tie names one of its people or for two years. Then each house's band, read
        /// afresh: a change of one of the colony's is told and written.
        /// </summary>
        public static void Daily(int now)
        {
            if (!On) return;
            Sync(now);
            Dirty();
            foreach (var h in WorldLedger.Instance.Houses.ToList())
            {
                try
                {
                    Settle(h, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("renown: a house's day", e);
                }
            }
        }

        private static void Sync(int now)
        {
            var seen = new HashSet<LedgerHouse>();
            var made = new List<LedgerHouse>();
            foreach (var group in FamilyNames.Families().GroupBy(f => RenownMath.Key(f.last)))
            {
                if (group.Key == null) continue;
                var members = group.SelectMany(g => g.members).Where(p => p != null && !p.Dead).Distinct().ToList();
                if (members.Count < RenownMath.AtLeast) continue;
                string name = group.First().last;
                var h = WorldLedger.OurHouse(group.Key);
                if (h == null)
                {
                    if (WorldLedger.OurHouses().Count >= RenownMath.MaxOurs) MakeRoom(now);
                    h = WorldLedger.AddOurHouse(name, Homes.KeyOf(SeatOf(members)));
                    if (h == null) continue;
                    made.Add(h);
                }
                seen.Add(h);
                Take(h, members, now);
            }
            // A house made today starts at the band it already has, untold (its name was known before the colony kept
            // count), read once its people are found.
            if (made.Count > 0)
            {
                Dirty();
                foreach (var h in made) h.band = (int)Compute(h, RenownBand.Unknown).band;
            }
            foreach (var h in WorldLedger.OurHouses())
            {
                if (seen.Contains(h)) continue;
                if (!RenownMath.Kept(false, TieNames(h), h.lastSeen, now)) WorldLedger.DropOurHouse(h);
            }
        }

        /// <summary>Room for a new house among twelve: the one with nobody left at the colony, gone longest, makes way.</summary>
        private static void MakeRoom(int now)
        {
            var give = WorldLedger.OurHouses().Where(h => Living(h).Count == 0 && !TieNames(h)).OrderBy(h => h.lastSeen).FirstOrDefault();
            if (give != null) WorldLedger.DropOurHouse(give);
        }

        /// <summary>The home most of them live at, else the seat.</summary>
        private static Map SeatOf(List<Pawn> members) =>
            members.Select(Homes.Of).Where(m => m != null).GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? Homes.Seat;

        private static void Take(LedgerHouse h, List<Pawn> members, int now)
        {
            members.Sort((a, b) => (b.ageTracker?.AgeBiologicalTicks ?? 0L).CompareTo(a.ageTracker?.AgeBiologicalTicks ?? 0L));
            int folkWas = h.folk.Count, seatWas = h.seat;
            var headWas = h.head;
            string membersWas = string.Join("|", h.members);
            bool added = false;
            foreach (var p in members)
            {
                var r = Refs.Of(p);
                if (h.folk.Contains(r)) continue;
                h.folk.Add(r);
                added = true;
            }
            // The newest twelve: the gone go first, then the earliest taken in.
            var here = new HashSet<Ref>(members.Select(Refs.Of));
            while (h.folk.Count > RenownMath.MembersKept)
            {
                int i = h.folk.FindIndex(r => !here.Contains(r));
                h.folk.RemoveAt(i >= 0 ? i : 0);
            }
            h.head = Refs.Of(members[0]);
            h.members.Clear();
            foreach (var p in members.Take(RenownMath.MembersKept)) h.members.Add(p.LabelShort);
            var seat = SeatOf(members);
            if (seat != null) h.seat = Homes.KeyOf(seat);
            h.lastSeen = now;
            // The views are built again only when what they show changed (its people, head or seat), not every day.
            if (added || h.folk.Count != folkWas || h.seat != seatWas || h.head != headWas || string.Join("|", h.members) != membersWas) WorldLedger.TownChanged();
        }

        /// <summary>A live tie of the ledger names one of the house's people (a marriage, a ward, a grudge about them).</summary>
        private static bool TieNames(LedgerHouse h)
        {
            if (h?.folk == null || h.folk.Count == 0) return false;
            var folk = new HashSet<Ref>(h.folk);
            foreach (Tie.Kind k in Enum.GetValues(typeof(Tie.Kind)))
            {
                if (k == Tie.Kind.Regard) continue;
                foreach (var t in WorldLedger.OwnTies(k))
                    if (t != null && t.Live && (folk.Contains(t.from) || folk.Contains(t.to) || t.terms != null && folk.Contains(t.terms.about)))
                        return true;
            }
            return false;
        }

        /// <summary>A house's band read afresh against the one last told: one of the colony's that moved is told and written; a town's is kept quietly.</summary>
        private static void Settle(LedgerHouse h, int now)
        {
            if (h == null || h.house.IsNone) return;
            var told = (RenownBand)h.band;
            var r = Compute(h, told);
            if (!RenownMath.Moved(told, r.band)) return;
            h.band = (int)r.band;
            WorldLedger.TownChanged();
            if (h.ours) Told(h, told, r);
        }

        /// <summary>Dev: one of the colony's houses told a band up or down from where it stands now (the same words, history and bubble as the day's look).</summary>
        public static void TellStep(LedgerHouse h, bool up)
        {
            if (h == null || !h.ours) return;
            Dirty();
            var r = Compute(h, (RenownBand)h.band);
            var was = r.band;
            int to = (int)was + (up ? 1 : -1);
            if (to > (int)RenownBand.Peoples || to < (int)RenownBand.Infamous) return;
            r.band = (RenownBand)to;
            h.band = to;
            WorldLedger.TownChanged();
            Told(h, was, r);
        }

        /// <summary>One of the colony's houses moved a band: into the history (dip:renown, a milestone), a word from one of its people on the map, and the news.</summary>
        private static void Told(LedgerHouse h, RenownBand was, RenownMath.Reading r)
        {
            var home = Homes.MapOf(h.seat) ?? Homes.Seat;
            bool up = r.band > was;
            string line = RenownWords.Moved(h, r, up, home);
            var actors = new List<HistoryActor> { new HistoryActor(h.house, HistoryRole.Did) };
            foreach (var p in Living(h)) actors.Add(new HistoryActor(Refs.Of(p), HistoryRole.With));
            actors.Add(new HistoryActor(Diplomacy.Colony(home), HistoryRole.For));
            Diplomacy.Write(DiplomacyMath.Renown, actors, Diplomacy.Colony(home), line, true);
            // One of them hears of it on the map and says so, to nobody (an officer home from a visit does the same).
            var speaker = Living(h).FirstOrDefault(p => p.Spawned && !p.Downed && !p.InMentalState && p.Map == home);
            // apart: the news of a name told in the region, said by one of the house where they stand.
            if (speaker != null) Outsiders.Say(speaker, up ? "renownup" : "renowndown", null);
            Tell.Minor(NewsKind.Colony, Importance.Notable, home, line, speaker != null ? new LookTargets(speaker) : LookTargets.Invalid,
                up ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }

        // ================= Reading it =================

        private static readonly Dictionary<int, RenownMath.Reading> reads = new Dictionary<int, RenownMath.Reading>();
        private static int readsHour = -1;
        private static Game readsGame;

        /// <summary>A house's renown as it stands, read at most once an in-game hour (null for none).</summary>
        public static RenownMath.Reading Read(LedgerHouse h)
        {
            if (h == null || h.house.IsNone || WorldLedger.Instance == null) return null;
            int hour = Now / GenDate.TicksPerHour;
            if (hour != readsHour || readsGame != Current.Game)
            {
                reads.Clear();
                readsHour = hour;
                readsGame = Current.Game;
            }
            if (reads.TryGetValue(h.house.id, out var r)) return r;
            try
            {
                r = Compute(h, (RenownBand)h.band);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: reading a house", e);
                r = RenownMath.Read(null);
            }
            reads[h.house.id] = r;
            return r;
        }

        /// <summary>The renown of a colonist's house, or null when they are of none (or it is off).</summary>
        public static RenownMath.Reading OfPawn(Pawn p) => On ? Read(HouseOf(p)) : null;

        private static RenownMath.Reading Compute(LedgerHouse h, RenownBand previous)
        {
            var hits = new List<RenownMath.Hit>();
            if (h.ours) Ours(h, hits);
            else Theirs(h, hits);
            return RenownMath.Read(hits, previous);
        }

        private static void Ours(LedgerHouse h, List<RenownMath.Hit> hits)
        {
            int now = Now;
            var counted = new HashSet<int>();
            var alive = Living(h).ToDictionary(p => p.thingIDNumber);
            foreach (var r in h.folk)
            {
                // Their deeds the history keeps, each event once for the house.
                foreach (var e in History.Of(r, RenownMath.MemberKinds))
                {
                    if (e == null || !e.actors.Exists(a => a.who == r) || !counted.Add(e.id)) continue;
                    var role = e.actors.First(a => a.who == r).role;
                    bool against = e.actors.Exists(a => a.role == HistoryRole.Against && a.who.kind == RefKind.Person);
                    bool about = e.actors.Exists(a => a.role == HistoryRole.About && a.who != r);
                    string key = RenownMath.SourceOf(e.kind, role, against, about, e.Weighty);
                    if (key != null) hits.Add(new RenownMath.Hit(key, Days(e.tick), Math.Max(1, e.count)));
                }
                // What the peoples remember of them by name.
                foreach (var m in ColonistMemory.MarksOf(r))
                {
                    string key = RenownMath.SourceOfMark(m.kind);
                    if (key != null) hits.Add(new RenownMath.Hit(key, Days(m.tick), Math.Max(1, m.count)));
                }
                if (r.kind == RefKind.Person && alive.TryGetValue(r.id, out var p)) Held(p, hits);
            }
            // The colony's own deeds, shared by each of its houses.
            var home = Homes.MapOf(h.seat) ?? Homes.Seat;
            var colony = Diplomacy.Colony(home);
            var deeds = RenownMath.ColonyDeeds.Select(HistoryKinds.OfDeed).ToArray();
            foreach (var who in new[] { colony, Refs.Seat }.Distinct())
                foreach (var e in History.Of(who, deeds))
                {
                    if (e == null || !counted.Add(e.id) || !e.actors.Exists(a => a.who == who && a.role == HistoryRole.Did)) continue;
                    hits.Add(new RenownMath.Hit(e.kind.Substring(HistoryKinds.Deed.Length + 1), Days(e.tick), Math.Max(1, e.count)));
                }
            foreach (var e in History.Of(colony, new[] { HistoryKinds.OfTie(Tie.Kind.Pact, "kept") }))
                if (e != null && counted.Add(e.id))
                    hits.Add(new RenownMath.Hit("pactKept", Days(e.tick)));
        }

        /// <summary>What one of the house's living people holds now: the colony's names for them, years in office, terms as speaker, a title of the Empire, notables who think well of them.</summary>
        private static void Held(Pawn p, List<RenownMath.Hit> hits)
        {
            var ep = CouncilGameComponent.Instance?.epithets;
            if (ep != null)
                foreach (var d in ep.Of(p))
                    if (d != null && !d.dubious)
                        hits.Add(new RenownMath.Hit("epithet", 0f, 1f, RenownMath.EpithetFor(d.defName)));
            if (p.records != null)
            {
                foreach (var kv in RenownMath.Offices)
                {
                    var def = DefDatabase<RecordDef>.GetNamedSilentFail(kv.Key);
                    int years = def != null ? RenownMath.YearsOf(p.records.GetValue(def)) : 0;
                    if (years > 0) hits.Add(new RenownMath.Hit("office", 0f, years, kv.Value));
                }
                var terms = DefDatabase<RecordDef>.GetNamedSilentFail("Council_SpeakerTerms");
                if (terms != null && p.records.GetValue(terms) >= 1f) hits.Add(new RenownMath.Hit("speaker", 0f, (float)Math.Floor(p.records.GetValue(terms))));
            }
            int seniority = Seniority(p);
            if (seniority > 0) hits.Add(new RenownMath.Hit("title", 0f, RenownMath.TitlePoints(seniority)));
            int known = KnownTo(p).Count;
            if (known > 0) hits.Add(new RenownMath.Hit("regard", 0f, known));
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

        private static void Theirs(LedgerHouse h, List<RenownMath.Hit> hits)
        {
            if (h.founding) hits.Add(new RenownMath.Hit("founding"));
            var n = h.head.IsNone ? null : NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.person == h.head && x.Alive);
            if (n != null)
            {
                if (n.role == NotableRole.Head) hits.Add(new RenownMath.Hit("head"));
                if (n.pawn != null && n.faction != null && n.faction.leader == n.pawn) hits.Add(new RenownMath.Hit("leader"));
            }
            var pawn = n?.pawn ?? WorldLedger.PawnOf(h.head);
            var best = pawn?.skills?.skills?.Where(s => s != null && !s.TotallyDisabled).OrderByDescending(s => s.Level).FirstOrDefault();
            if (best != null && best.Level >= 8)
            {
                var k = RenownMath.SkillFor(best.def.defName);
                if (k != KnownFor.None) hits.Add(new RenownMath.Hit("skill", 0f, 1f, k));
            }
            var town = WorldLedger.TownRecord(h.town);
            string fortune = town != null ? RenownMath.FortuneSource((int)town.prosperity) : null;
            if (fortune != null) hits.Add(new RenownMath.Hit(fortune));
            // Their ties with the colony: anything between their head and the colony the history keeps.
            if (!h.head.IsNone)
                foreach (var e in History.Of(h.head, new[] { DiplomacyMath.Family, HistoryKinds.Tie }))
                    if (e != null && e.actors.Exists(a => a.who.kind == RefKind.Colony || a.who == Refs.Of(Faction.OfPlayerSilentFail)))
                        hits.Add(new RenownMath.Hit("colonyTie", Days(e.tick)));
        }

        // ================= What it does =================

        /// <summary>How much better (or worse) a notable thinks of one of the colony for their house's name (Notables.OpinionOf).</summary>
        public static float RegardBonus(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 0f : RenownMath.RegardBonus(r.score);
        }

        /// <summary>How much more a good word for one of the colony counts for their house's name (OutsiderOutcomes.GoodWord).</summary>
        public static float GoodWordFactor(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 1f : RenownMath.GoodWordFactor(r.score);
        }

        /// <summary>How much more readily a match with one of the colony is taken up for their house's name (Matches).</summary>
        public static float MatchFactor(Pawn p)
        {
            var r = OfPawn(p);
            return r == null ? 1f : RenownMath.MatchFactor(r.score);
        }

        /// <summary>The colony's houses at a home by what each is known for and how far (for the world's events).</summary>
        public static List<KeyValuePair<KnownFor, RenownBand>> KnownAt(Map home)
        {
            var list = new List<KeyValuePair<KnownFor, RenownBand>>();
            if (!On) return list;
            int key = home != null && Homes.IsHome(home) ? Homes.KeyOf(home) : Homes.KeyOf(Homes.Seat);
            bool several = Homes.All.Count > 1;
            foreach (var h in WorldLedger.OurHouses())
            {
                // With more than one home, a house draws to its own seat.
                if (several && h.seat != key) continue;
                var r = Read(h);
                if (r != null && r.known != KnownFor.None && r.band >= RenownBand.Town) list.Add(new KeyValuePair<KnownFor, RenownBand>(r.known, r.band));
            }
            return list;
        }

        /// <summary>An event's factor at a home for what its houses are known for (Happenings, IncidentWorker_Happening); 1 when none draws it.</summary>
        public static float EventFactor(IncidentDef def, Map home)
        {
            if (def == null || !On) return 1f;
            try
            {
                return RenownMath.EventFactor(def.defName, KnownAt(home));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: an event's factor", e);
                return 1f;
            }
        }

        /// <summary>A feast's guests accept a little more readily at a home with a house known for hosting (Feasts).</summary>
        public static float GuestFactor(Map home)
        {
            float f = 1f;
            foreach (var kv in KnownAt(home))
                if (kv.Key == KnownFor.Hosts)
                    f = Math.Max(f, RenownMath.GuestFactor(kv.Value));
            return f;
        }

        /// <summary>The travelling champion's choice among the colony's best: one of a house known for fighters, when one of them is among them (HappeningWorker_Champion).</summary>
        public static bool FightersHouse(Pawn p)
        {
            var h = HouseOf(p);
            var r = On ? Read(h) : null;
            return r != null && RenownMath.ChallengesFighter(r.known, r.band);
        }

        /// <summary>The tags of a colonist's house for a bubble (Outsiders.Line): house:ours, house:renowned, house:knownfor:KIND; nothing for none.</summary>
        public static void AddTags(HashSet<string> tags, Pawn colonist)
        {
            if (tags == null || colonist == null || !On) return;
            try
            {
                var h = HouseOf(colonist);
                var r = Read(h);
                if (h != null && r != null) tags.UnionWith(RenownMath.OurTags(r.band, r.known));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a bubble's tags", e);
            }
        }

        /// <summary>The live grudges a house of theirs holds against the colony (held by its head), or against one of a house of the colony (about one of its people).</summary>
        public static List<Tie> Feuds(LedgerHouse h)
        {
            var list = new List<Tie>();
            if (h == null) return list;
            var folk = h.ours ? new HashSet<Ref>(h.folk) : null;
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Grudge))
            {
                if (t == null || !t.Live) continue;
                if (h.ours ? t.terms != null && folk.Contains(t.terms.about) : !h.head.IsNone && t.from == h.head) list.Add(t);
            }
            return list;
        }
    }
}
