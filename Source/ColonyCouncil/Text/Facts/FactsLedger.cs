using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The providers of the world ledger and the history: the ages of the two sides, the ties between them, and what the
    // history holds of their town and of the one spoken to lately. They read without upkeep: the ledger's ties as they
    // stand (a tie whose end has come counts as it would be), nothing tidied, kept or let go.
    public static partial class Facts
    {
        // ---------------- The ages ----------------

        /// <summary>
        /// The colony's age as lines are tagged everywhere (tribal, medieval, industrial, spacer, and lowtech before
        /// electricity: Flavor.AddEraTags), and their people's (their:medieval, their:lowtech, their:empire) with how it
        /// stands to the colony's (their:sameage, their:behind, their:ahead).
        /// </summary>
        private static void AgeTags(FactScene s, FactSet f)
        {
            var era = new HashSet<string>();
            Flavor.AddEraTags(era);
            foreach (var t in era) f.Tag(t);
            if (s.people != null) FactsMath.AddAges(f, ColonyCouncil.Ages.Of(s.colony), ColonyCouncil.Ages.Of(s.people), ColonyCouncil.Ages.IsEmpire(s.people));
        }

        // ---------------- Ties ----------------

        /// <summary>
        /// The live ties between the scene's two sides (our side: a colony, the colony's people, one of the colony; theirs:
        /// the one spoken to, their people, their town, one of their people), a favour owed, a promise, a claim, a debt,
        /// fealty or a grudge (FactsMath.TieKinds), the ledger's own and those kept in other stores: of each kind the one
        /// the speaker is party to, else the newest (FactsMath.AddTie).
        /// </summary>
        private static void Ties(FactScene s, FactSet f)
        {
            if (WorldLedger.Instance == null) return;
            int now = Now;
            var speaker = s.speaker != null ? Refs.Of(s.speaker) : Ref.None;
            var best = new Dictionary<Tie.Kind, (Tie tie, TieFact fact)>();
            // A debt of honour (Diplomacy/FavoursOwed.cs) speaks apart from a favour to bring: the one the speaker is party to, else the newest.
            (Tie tie, TieFact fact) honour = (null, default);
            void Look(Tie t)
            {
                if (t == null || !FactsMath.Speaks(t.kind)) return;
                bool debt = t.kind == Tie.Kind.Favour && t.origin == FavourMath.Origin;
                var state = TieRules.StateAt(t, now);
                if (!TieRules.Live(state)) return;
                int from = Side(s, t.from);
                if (from == 0) return;
                int to = Side(s, t.to);
                if (to == 0 || from == to) return;
                var fact = new TieFact
                {
                    kind = t.kind, oursBound = from > 0, you = !speaker.IsNone && (t.from == speaker || t.to == speaker), calledIn = state == Tie.State.CalledIn,
                    days = t.since >= 0 && t.since <= now ? (now - t.since) / (float)GenDate.TicksPerDay : float.MaxValue
                };
                if (debt)
                {
                    if (honour.tie == null || FactsMath.Before(fact, honour.fact)) honour = (t, fact);
                    return;
                }
                if (!best.TryGetValue(t.kind, out var was) || FactsMath.Before(fact, was.fact)) best[t.kind] = (t, fact);
            }
            foreach (var k in FactsMath.TieKinds)
                foreach (var t in WorldLedger.OwnTies(k))
                    Look(t);
            foreach (var t in WorldLedger.Today()) Look(t);
            foreach (var k in FactsMath.TieKinds)
            {
                if (!best.TryGetValue(k, out var b)) continue;
                var fact = b.fact;
                fact.toName = NameOf(s, b.tie.to);
                FactsMath.AddTie(f, fact);
            }
            if (honour.tie != null)
            {
                var fact = honour.fact;
                fact.toName = NameOf(s, honour.tie.to);
                FactsMath.AddHonour(f, fact, DipWords.Why(honour.tie));
            }
            // A pact or a truce with their people (Diplomacy/FactsPacts.cs): tie:pact, tie:truce, {PACTWITH}, {TRUCEWITH}.
            PactTies(s, f, now);
            // Captives and envoys between the two (Diplomacy/FactsRansom.cs): ransom:theirs, ransom:held, them:wascaptive, envoy:here, them:envoy, envoy:ours.
            RansomEnvoyFacts(s, f);
            // Feasts with guests (Diplomacy/FactsFeast.cs): feast:invited, feast:soon, feast:here, them:feastguest, feast:theywon, feast:wewon.
            FeastFacts(s, f);
            // Marriage alliances (Diplomacy/FactsMatch.cs): tie:marriage, :ours, :theirs, :you, match:agreed, match:child, match:wronged.
            MatchFacts(s, f);
            // Wards and hostages either way (Diplomacy/FactsWards.cs): tie:ward, tie:hostage, them:ward, them:hostage, them:fostered.
            WardFacts(s, f);
            // Fealty and vassals (Diplomacy/FactsFealty.cs): fealty:levies, fealty:protected, fealty:short, vassal:heavy, vassal:levy.
            FealtyFacts(s, f);
            // House renown (Diplomacy/FactsRenown.cs): house:ours, house:renowned, house:knownfor:KIND, them:house, them:house:feud, {HOUSE}, {KNOWNFOR}, {RENOWN}.
            RenownFacts(s, f);
        }

        /// <summary>Which side of the scene a party is on: 1 ours, -1 theirs, 0 neither.</summary>
        private static int Side(FactScene s, Ref r)
        {
            switch (r.kind)
            {
                case RefKind.Colony:
                    return 1;
                case RefKind.People:
                    if (Faction.OfPlayerSilentFail != null && r.id == Faction.OfPlayerSilentFail.loadID) return 1;
                    return s.people != null && r.id == s.people.loadID ? -1 : 0;
                case RefKind.Town:
                {
                    if (s.place != null && r.id == s.place.ID) return -1;
                    var town = Refs.Town(r);
                    return town != null && s.people != null && town.Faction == s.people ? -1 : 0;
                }
                case RefKind.Person:
                {
                    if (r.HasPawn && s.speaker != null && r.id == s.speaker.thingIDNumber) return 1;
                    if (r.HasPawn && s.listener != null && r.id == s.listener.thingIDNumber) return -1;
                    // Someone else: the ledger's record of them, else (a party to a tie kept in another store, a vendetta's
                    // avenger) one of the colonists, else their pawn wherever the game keeps it.
                    var rec = WorldLedger.PersonRecord(r);
                    var pawn = rec?.pawn ?? (rec == null ? Colonist(r) ?? Refs.Pawn(r) : null);
                    if (pawn != null && KinMemory.Ours(pawn)) return 1;
                    // Their people as the outsiders read it (a guest's is their home's), else as the ledger last knew it.
                    var people = pawn != null && !pawn.Discarded ? Outsiders.PeopleOf(pawn) : rec?.people;
                    if (pawn == null && people != null && people.IsPlayer) return 1;
                    return people != null && s.people != null && people == s.people ? -1 : 0;
                }
                default:
                    return 0;
            }
        }

        /// <summary>One of the colony's free colonists by a person's ref, or null.</summary>
        private static Pawn Colonist(Ref r)
        {
            if (!r.HasPawn) return null;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                if (p != null && p.thingIDNumber == r.id)
                    return p;
            return null;
        }

        /// <summary>A party's name as the texts use it: a person's, the colony's (as {COLONY}), a people's, a town's.</summary>
        private static string NameOf(FactScene s, Ref r)
        {
            switch (r.kind)
            {
                case RefKind.Person:
                    if (r.HasPawn && s.speaker != null && r.id == s.speaker.thingIDNumber) return s.speaker.LabelShort;
                    if (r.HasPawn && s.listener != null && r.id == s.listener.thingIDNumber) return s.listener.LabelShort;
                    return WorldLedger.NameOf(r) ?? Refs.Pawn(r)?.LabelShort;
                case RefKind.Colony:
                {
                    var home = Refs.Home(r)?.Parent as Settlement;
                    return home != null && home.HasName ? home.Name : Faction.OfPlayerSilentFail?.Name;
                }
                case RefKind.People:
                    return Refs.Faction(r)?.Name;
                case RefKind.Town:
                    return Refs.Town(r)?.Name ?? WorldLedger.TownRecord(r.id)?.name;
                default:
                    return null;
            }
        }

        // ---------------- The history ----------------

        /// <summary>
        /// What the world's history holds lately (FactsMath.LatelyDays) of their town (town:lately, town:lately:KIND: a
        /// world event there, the town taken or made a vassal) and of the one spoken to (them:lately, them:lately:KIND: a
        /// tie of theirs made or kept, a world event they were part of).
        /// </summary>
        private static void Lately(FactScene s, FactSet f)
        {
            int now = Now;
            int since = now - (int)(FactsMath.LatelyDays * GenDate.TicksPerDay);
            if (s.place != null) FactsMath.AddLately(f, "town", Ago(ColonyCouncil.History.At(Refs.Place(s.place), null, since, FactsMath.LatelyMax), now));
            if (s.listener != null) FactsMath.AddLately(f, "them", Ago(ColonyCouncil.History.Of(Refs.Of(s.listener), null, since, FactsMath.LatelyMax), now));
        }

        private static IEnumerable<(string kind, float days)> Ago(List<HistoryEntry> events, int now)
        {
            foreach (var e in events)
                if (e != null)
                    yield return (e.kind, (now - e.tick) / (float)GenDate.TicksPerDay);
        }
    }
}
