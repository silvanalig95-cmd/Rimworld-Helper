using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Something the player can do from a row: its words, why not when it can't be now (greyed), and what it does.</summary>
    public sealed class DipAct
    {
        public string label;
        public string why;
        public string tip;
        public Action run;
    }

    /// <summary>One line of a page or panel: its words, a quieter note, what a click on it opens (DiplomacyLinks), and an act beside it.</summary>
    public sealed class DipRow
    {
        public string text;
        public string note;
        public Color color = Color.white;
        public object link;
        public DipAct act;
        public string tip;
    }

    /// <summary>A people's card: its stance in words, its head, its ties with the colony in their age's words, what is under way, the acts that fit.</summary>
    public sealed class DipPeople
    {
        public Faction people;
        public string name;
        public string stance;
        public Color stanceColor;
        public DipRow head;
        public readonly List<DipRow> ties = new List<DipRow>();
        public readonly List<DipRow> underway = new List<DipRow>();
        public readonly List<DipAct> acts = new List<DipAct>();
        /// <summary>For the order: peoples with ties first.</summary>
        public int weight;
    }

    /// <summary>
    /// What the Diplomacy and Household pages and the town panel's section show, gathered in one place and built at most once
    /// every two seconds while something reads it (DiplomacyMath.Stale), sooner when the ledger, the memory, the notables or
    /// the history move: the strip of what is owed each way; a card for each people within the region map's reach or with a
    /// tie to the colony; who is here from outside and who of the colony's is away; the colony's houses (its families known
    /// by name). The pieces (DipRow, DipAct, DipPeople) are drawn by DiplomacyPanels and can be drawn anywhere else.
    /// </summary>
    public sealed class DiplomacyModel
    {
        public readonly List<DipRow> owedUs = new List<DipRow>();
        public readonly List<DipRow> owedByUs = new List<DipRow>();
        public readonly List<DipPeople> peoples = new List<DipPeople>();
        public readonly List<DipRow> here = new List<DipRow>();
        public readonly List<DipRow> away = new List<DipRow>();
        public readonly List<DipRow> houses = new List<DipRow>();

        private static DiplomacyModel current;
        private static float builtAt = -1f;
        private static int builtVersion;
        private static Game builtGame;

        private static int VersionNow => DiplomacyMath.Version(WorldLedger.Version, FactionMemory.Version, NotableBook.Version, History.Version);

        /// <summary>The model, built again when stale.</summary>
        public static DiplomacyModel Current
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                int v = VersionNow;
                if (current == null || builtGame != Current_Game || DiplomacyMath.Stale(builtAt, now, builtVersion, v))
                {
                    try
                    {
                        current = Build();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("diplomacy: the pages' model", e);
                        current = new DiplomacyModel();
                    }
                    builtAt = now;
                    builtVersion = v;
                    builtGame = Current_Game;
                }
                return current;
            }
        }

        private static Game Current_Game => Verse.Current.Game;

        /// <summary>Built again on the next read (after an act the player took).</summary>
        public static void Dirty() => builtAt = -1f;

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        // ================= Building =================

        public static DiplomacyModel Build()
        {
            var m = new DiplomacyModel();
            if (Verse.Current.Game == null) return m;
            var home = Home;
            foreach (var t in FavoursOwed.Live())
            {
                var row = DebtRow(t, home);
                if (FavoursOwed.ColonyOwes(t)) m.owedByUs.Add(row);
                else m.owedUs.Add(row);
            }
            // The peoples within reach of any home, and any with a tie to the colony.
            var peoples = new HashSet<Faction>();
            foreach (var r in RegionTowns.Reach)
                foreach (var s in r.towns)
                    if (s?.Faction != null && Diplomacy.Deals(s.Faction))
                        peoples.Add(s.Faction);
            foreach (var t in FavoursOwed.Live())
            {
                var f = FavoursOwed.PeopleOf(t);
                if (f != null && Diplomacy.Deals(f)) peoples.Add(f);
            }
            foreach (var f in peoples)
                m.peoples.Add(Card(f, home));
            m.peoples.Sort((a, b) =>
            {
                int c = b.weight.CompareTo(a.weight);
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            BuildHousehold(m);
            return m;
        }

        /// <summary>A debt of honour as a row: who owes whom and for what, by both ages; where it stands; calling it in or answering it.</summary>
        public static DipRow DebtRow(Tie t, Map home)
        {
            var f = FavoursOwed.PeopleOf(t);
            var row = new DipRow
            {
                text = DipWords.DebtLine(t), note = DipWords.StateLine(t), link = FavoursOwed.IsPersonal(t) ? (object)FavoursOwed.NotableOf(t) ?? f : f,
                color = FavoursOwed.ColonyOwes(t) ? UiKit.Warn : UiKit.Good
            };
            row.act = FavoursOwed.ColonyOwes(t) ? AnswerAct(t) : CallAct(t, home);
            return row;
        }

        /// <summary>Calling in a debt of theirs: a letter or a call to their head (or the notable who owes), its asks chosen there.</summary>
        public static DipAct CallAct(Tie t, Map home)
        {
            var a = new DipAct { label = DipWords.P("CC_Dip_Act_Call") };
            a.why = FavourCalls.WhyNot(t);
            var to = FavoursOwed.NotableOf(t) ?? FavourCalls.HeadOf(FavoursOwed.PeopleOf(t), home);
            if (a.why == null && to == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", DipWords.People(FavoursOwed.PeopleOf(t)).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(to, LetterPurpose.Favour);
            a.tip = DipWords.P("CC_Dip_Act_Call_Tip");
            if (a.why == null)
            {
                var n = to;
                string key = DiplomacyLetters.TieKey(t);
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Favour, key));
            }
            return a;
        }

        /// <summary>Their call on a debt of the colony's: open their letter to answer it (a mission's own letter answers it there).</summary>
        public static DipAct AnswerAct(Tie t)
        {
            if (t.state != Tie.State.CalledIn) return null;
            var letter = FavourAsks.LetterFor(t);
            var a = new DipAct { label = DipWords.P("CC_Dip_Act_Answer"), tip = DipWords.P("CC_Dip_Act_Answer_Tip") };
            if (letter == null) a.why = FavourMath.ParseNeed(t.terms.goods, out var n) && FavourMath.ByMission(n) ? DipWords.P("CC_Dip_Why_ByMission") : DipWords.P("CC_Dip_Why_LetterGone");
            else a.run = () => DiplomacyLinks.Open(letter);
            return a;
        }

        private static DipPeople Card(Faction f, Map home)
        {
            var c = new DipPeople { people = f, name = DipWords.People(f), stance = DipWords.Stance(f), stanceColor = DipWords.StanceColor(f) };
            var head = FavourCalls.HeadOf(f, home);
            if (head != null)
            {
                int op = NotableBook.Opinion(head);
                c.head = new DipRow
                {
                    text = DipWords.P("CC_Dip_Head", NotableBook.Called(head).Named("NAME"), NotableBook.RegardWords(op).Named("REGARD")), link = head,
                    color = NotableBook.RegardColor(op)
                };
            }
            // Debts of honour, either way, and the notables' personal ones.
            foreach (var t in FavoursOwed.OwedBy(f).Concat(FavoursOwed.OwedTo(f)))
            {
                c.ties.Add(DebtRow(t, home));
                c.weight += 10;
            }
            // The other ties the ledger keeps with them (a favour to bring, a good word, a claim, a debt, fealty, a grudge, a promise of peace).
            var r = Refs.Of(f);
            foreach (var t in WorldLedger.TiesOf(r))
            {
                if (t == null || !t.Live || t.kind == Tie.Kind.Regard || t.kind == Tie.Kind.Favour && t.origin == FavourMath.Origin) continue;
                string line = TieLine(t);
                if (line.NullOrEmpty()) continue;
                c.ties.Add(new DipRow { text = line, note = t.since >= 0 ? DipWords.Since(t.since) : null, color = UiKit.Info, link = f });
                c.weight += 3;
            }
            // Under way: letters and calls to their notables, their calls waiting on the colony.
            var book = NotableBook.Instance;
            if (book != null)
                foreach (var l in book.letters)
                {
                    if (l == null || !l.Open) continue;
                    var n = book.Get(l.notable);
                    if (n?.faction != f) continue;
                    string status = NeighboursPanel.StatusWords(l, n);
                    if (status.NullOrEmpty()) continue;
                    c.underway.Add(new DipRow { text = status, link = n, color = UiKit.Faint });
                    c.weight += 2;
                }
            // The acts that fit: write to their head (any purpose), each with why not.
            var write = new DipAct { label = DipWords.P("CC_Dip_Act_Write"), tip = DipWords.P("CC_Dip_Act_Write_Tip") };
            if (head == null) write.why = DipWords.P("CC_Dip_Why_NoNotable", c.name.Named("PEOPLE"));
            else write.why = NeighboursPanel.WhyNoLetter(head);
            if (write.why == null)
            {
                var n = head;
                write.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n));
            }
            c.acts.Add(write);
            if (!Diplomacy.Hostile(f)) c.weight += 1;
            return c;
        }

        /// <summary>One of the ledger's other ties, in words (null for one with nothing to say here).</summary>
        private static string TieLine(Tie t)
        {
            string from = DipWords.NameOf(t.from), to = DipWords.NameOf(t.to), about = t.terms?.about.IsNone ?? true ? "" : DipWords.NameOf(t.terms.about);
            switch (t.kind)
            {
                case Tie.Kind.Promise:
                    if (t.origin == FavourMath.Origin && t.terms.key == FavourMath.PeaceKey) return DipWords.P("CC_Dip_Tie_Peace", from.Named("FROM"), about.Named("ABOUT"));
                    return DipWords.P("CC_Dip_Tie_Promise", from.Named("FROM"), to.Named("TO"));
                case Tie.Kind.Favour: return DipWords.P("CC_Dip_Tie_Favour", from.Named("FROM"), to.Named("TO"));
                case Tie.Kind.Claim: return DipWords.P("CC_Dip_Tie_Claim", from.Named("FROM"), about.Named("ABOUT"));
                case Tie.Kind.Debt: return DipWords.P("CC_Dip_Tie_Debt", from.Named("FROM"), to.Named("TO"));
                case Tie.Kind.Fealty: return DipWords.P("CC_Dip_Tie_Fealty", from.Named("FROM"), to.Named("TO"));
                case Tie.Kind.Grudge: return DipWords.P("CC_Dip_Tie_Grudge", from.Named("FROM"), to.Named("TO"));
                default: return null;
            }
        }

        // ================= The household =================

        private const int MaxRows = 24;

        private static void BuildHousehold(DiplomacyModel m)
        {
            // Here from outside: their people's guests, traders and visitors, lodgers, at each home.
            foreach (var map in Homes.All)
            {
                foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (m.here.Count >= MaxRows) break;
                    if (!Outsiders.IsOutsider(p)) continue;
                    var f = Outsiders.PeopleOf(p);
                    string why = WhyHere(p);
                    string text = f != null ? DipWords.P("CC_Dip_Here", p.Named("PAWN"), DipWords.People(f).Named("PEOPLE"), why.Named("WHY"))
                        : DipWords.P("CC_Dip_HereAlone", p.Named("PAWN"), why.Named("WHY"));
                    if (Homes.All.Count > 1) text = Homes.Name(map) + ": " + text;
                    m.here.Add(new DipRow { text = text, link = p, color = UiKit.StrangerColor });
                }
            }
            // Ours away: with a caravan, at another map, held by another people.
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled) continue;
                foreach (var p in c.PawnsListForReading)
                    if (p != null && p.IsColonist && p.RaceProps.Humanlike && m.away.Count < MaxRows)
                        m.away.Add(new DipRow { text = DipWords.P("CC_Dip_Away_Caravan", p.Named("PAWN"), c.Name.Named("CARAVAN")), link = c, color = UiKit.Info });
            }
            foreach (var map in Find.Maps)
            {
                if (map == null || map.IsPlayerHome) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (m.away.Count < MaxRows)
                        m.away.Add(new DipRow { text = DipWords.P("CC_Dip_Away_Map", p.Named("PAWN"), (map.Parent?.LabelCap ?? "?").Named("PLACE")), link = p, color = UiKit.Info });
            }
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
                foreach (var p in FavourCalls.Held(f))
                    if (m.away.Count < MaxRows)
                        m.away.Add(new DipRow { text = DipWords.P("CC_Dip_Away_Held", p.Named("PAWN"), DipWords.People(f).Named("PEOPLE")), link = f, color = UiKit.Warn });
            // The colony's houses: its families known by name.
            var seen = new HashSet<string>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
            {
                string plural = FamilyNames.Plural(p);
                if (plural.NullOrEmpty() || !seen.Add(plural)) continue;
                var members = FamilyNames.Members(p);
                m.houses.Add(new DipRow
                {
                    text = DipWords.P("CC_Dip_House", plural.Named("HOUSE"), string.Join(", ", members.Select(x => x.LabelShort)).Named("MEMBERS")),
                    note = FamilyNames.ReputeLine(p), link = p, color = UiKit.Gold
                });
            }
        }

        /// <summary>Why someone of theirs is here: a lodger, trading, visiting, staying at the camp, or just here.</summary>
        private static string WhyHere(Pawn p)
        {
            if (p.IsQuestLodger()) return DipWords.P("CC_Dip_HereAs_Lodger");
            var job = p.GetLord()?.LordJob;
            if (job is LordJob_TradeWithColony) return DipWords.P("CC_Dip_HereAs_Trading");
            if (job is LordJob_VisitColony) return DipWords.P("CC_Dip_HereAs_Visiting");
            return DipWords.P("CC_Dip_HereAs_Here");
        }

        // ================= A town's panel =================

        private static int townFor = -1;
        private static float townAt = -1f;
        private static int townVersion;
        private static List<DipRow> townRows = new List<DipRow>();

        /// <summary>A town's part: its people's debts with the colony, the personal ones of its notables, and calling one in. Built at most twice a second.</summary>
        public static List<DipRow> TownRows(Settlement s)
        {
            if (s == null) return new List<DipRow>();
            float now = Time.realtimeSinceStartup;
            int v = VersionNow;
            if (townFor == s.ID && townVersion == v && now - townAt < 0.5f) return townRows;
            townRows = BuildTownRows(s);
            townFor = s.ID;
            townVersion = v;
            townAt = now;
            return townRows;
        }

        private static List<DipRow> BuildTownRows(Settlement s)
        {
            var list = new List<DipRow>();
            var f = s?.Faction;
            if (f == null) return list;
            var home = Home;
            foreach (var t in FavoursOwed.OwedBy(f).Concat(FavoursOwed.OwedTo(f)))
            {
                if (FavoursOwed.IsPersonal(t) && FavoursOwed.NotableOf(t)?.town != s.ID) continue;
                list.Add(DebtRow(t, home));
            }
            return list;
        }
    }
}
