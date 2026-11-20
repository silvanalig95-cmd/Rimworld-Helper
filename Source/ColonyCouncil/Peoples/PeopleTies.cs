using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One tie between the colony and a people: what it is, who of the colony carries it (for their gifts), and whom it names.</summary>
    public struct PeopleBond
    {
        public TieWay way;
        /// <summary>The colonist it rests on: the ward's guardian, the one married in, the colonist a debt is about, the speaker; or null.</summary>
        public Pawn carrier;
        /// <summary>The name the letter gives it: the ward, the spouse, the envoy; or null.</summary>
        public string who;
    }

    /// <summary>
    /// Ties make a people's own events and camps come (the content update's B14, 20 November 2026): a people bound to the
    /// colony (a defence or trade pact, a marriage between the two, a ward or hostage of theirs here or of the colony's
    /// there, an envoy either way, fealty either way, a debt of honour either way) sends its own events (the peoples'
    /// hundred, by the callings on their defs) and its camps about half again as often, and a people the colony broke its
    /// word with (a pact or truce broken, its word broken, fealty renounced, a contract broken, while they still feel it)
    /// about a third less often (PeopleTiesMath). Their events' letters name the tie in a closing line, and while a people's
    /// own event is being cast its gift goes first to the colonist who carries the tie (<see cref="Giving"/>, read by
    /// PeopleEvents.Takers). What is bound is read from the diplomacy books each hour at most; nothing is saved.
    /// </summary>
    public static class PeopleTies
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool On => FactionMemory.On;

        // ================= What binds them =================

        /// <summary>Every tie the colony has with this people now, each once.</summary>
        public static List<PeopleBond> Of(Faction f)
        {
            var list = new List<PeopleBond>();
            if (f == null || f.IsPlayer) return list;
            try
            {
                foreach (var t in Matches.With(f))
                {
                    var ours = Matches.OursOf(t);
                    var theirs = Matches.TheirsOf(t);
                    var carrier = KinMemory.Ours(theirs) ? theirs : KinMemory.Ours(ours) ? ours : null;
                    list.Add(new PeopleBond { way = TieWay.Marriage, carrier = carrier, who = (theirs ?? ours)?.LabelShort });
                }
                foreach (var w in Wards.With(f))
                {
                    if (w == null) continue;
                    var carrier = w.Theirs && w.stage == WardStage.Here && KinMemory.Ours(w.guardian) ? w.guardian : null;
                    list.Add(new PeopleBond { way = w.Hostage ? TieWay.Hostage : TieWay.Ward, carrier = w.Hostage ? null : carrier, who = w.pawn?.LabelShort ?? w.name });
                }
                var speaker = Speaker(null);
                var envoy = Envoys.With(f, out var envoyHome);
                if (envoy?.envoy != null) list.Add(new PeopleBond { way = TieWay.Envoy, carrier = Speaker(envoyHome?.Map) ?? speaker, who = envoy.envoy.LabelShort });
                var abroad = EnvoysAbroad.With(f);
                if (abroad?.colonist != null) list.Add(new PeopleBond { way = TieWay.Envoy, carrier = speaker, who = abroad.colonist.LabelShort });
                foreach (var t in Pacts.With(f))
                    list.Add(new PeopleBond { way = TieWay.Pact, carrier = Speaker(Pacts.HomeOf(t)) ?? speaker });
                var liege = Fealty.With(f);
                if (liege != null) list.Add(new PeopleBond { way = TieWay.Fealty, carrier = Speaker(Fealty.HomeOf(liege)) ?? speaker });
                foreach (var (vHome, v) in Vassals.All())
                    if (v != null && Vassals.TownOf(v)?.Faction == f)
                        list.Add(new PeopleBond { way = TieWay.Fealty, carrier = Speaker(vHome) ?? speaker, who = Vassals.NameOf(v) });
                if (FavoursOwed.On)
                    foreach (var t in FavoursOwed.OwedBy(f).Concat(FavoursOwed.OwedTo(f)))
                    {
                        var c = FavoursOwed.Colonist(t);
                        list.Add(new PeopleBond { way = TieWay.Debt, carrier = KinMemory.Ours(c) ? c : null, who = c?.LabelShort });
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: what binds them", e);
            }
            return list;
        }

        /// <summary>The colony's speaker at this home (else the seat's), the one who speaks for its word to others.</summary>
        private static Pawn Speaker(Map home)
        {
            var p = OfficeTracker.For(home ?? Homes.Seat)?.HolderOf("Council_Office_Speaker");
            return KinMemory.Ours(p) ? p : null;
        }

        /// <summary>The colony broke its word with them, and they still feel it (PeopleTiesMath.BrokenDeeds).</summary>
        public static bool Broke(Faction f)
        {
            var mem = FactionMemory.Instance;
            if (f == null || mem == null) return false;
            int now = Now;
            foreach (var d in mem.deeds)
                if (d != null && d.faction == f && !d.settled && PeopleTiesMath.IsBrokenWord(d.kind) && MemoryMath.Strength(d.weight, now - d.tick) >= PeopleTiesMath.BrokenFelt)
                    return true;
            return false;
        }

        // ================= How often (read once an hour) =================

        private static readonly Dictionary<int, (bool bound, bool broke)> cache = new Dictionary<int, (bool, bool)>();
        private static int cacheHour = -1;
        private static Game cacheGame;

        private static (bool bound, bool broke) Read(Faction f)
        {
            if (f == null) return (false, false);
            int hour = Now / GenDate.TicksPerHour;
            if (hour != cacheHour || cacheGame != Current.Game)
            {
                cache.Clear();
                cacheHour = hour;
                cacheGame = Current.Game;
            }
            if (cache.TryGetValue(f.loadID, out var r)) return r;
            r = (Of(f).Count > 0, Broke(f));
            cache[f.loadID] = r;
            return r;
        }

        /// <summary>Bound to the colony by any tie now.</summary>
        public static bool Bound(Faction f) => Read(f).bound;

        /// <summary>How much likelier this people's own things come: bound, broken, both or neither.</summary>
        public static float Factor(Faction f)
        {
            if (!On || f == null) return 1f;
            var r = Read(f);
            return PeopleTiesMath.Factor(r.bound, r.broke);
        }

        /// <summary>The peoples of a calling at peace with the colony (usually one).</summary>
        private static IEnumerable<Faction> OfCalling(string calling) =>
            Find.FactionManager?.AllFactionsListForReading.Where(f => f != null && !f.defeated && PeoplesD.Theirs(f, calling) && !f.HostileTo(Faction.OfPlayer)) ?? Enumerable.Empty<Faction>();

        /// <summary>
        /// One of the peoples' own events (its def names their callings): as likely as its people's ties make it, the
        /// likeliest of them for an event two peoples share; 1 for any other event.
        /// </summary>
        public static float EventFactor(IncidentDef d)
        {
            var callings = d?.GetModExtension<HappeningExtension>()?.callings;
            if (!On || callings.NullOrEmpty()) return 1f;
            try
            {
                float best = -1f;
                foreach (var c in callings)
                    foreach (var f in OfCalling(c))
                        best = Math.Max(best, Factor(f));
                return best < 0f ? 1f : best;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: an event's weight", e);
                return 1f;
            }
        }

        /// <summary>A kind of camp only one people sends (its calling, or a people that favours it): as likely as their ties make it.</summary>
        public static float CampKindFactor(CampKindDef k)
        {
            if (!On || k == null) return 1f;
            try
            {
                if (!k.calling.NullOrEmpty())
                {
                    float best = -1f;
                    foreach (var f in OfCalling(k.calling)) best = Math.Max(best, Factor(f));
                    return best < 0f ? 1f : best;
                }
                return 1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: a camp's weight", e);
                return 1f;
            }
        }

        /// <summary>A camp's people: a bound people camps here more often, one the colony broke its word with less.</summary>
        public static float CampPeopleFactor(Faction f) => Factor(f);

        // ================= Their letters =================

        /// <summary>
        /// The line a people's own event's letter closes with, naming the tie (the strongest), when they are bound; null
        /// otherwise or for any other event. In the letters' own style.
        /// </summary>
        public static string LetterLine(HappeningContext ctx)
        {
            if (!On || ctx?.Ext?.callings.NullOrEmpty() != false) return null;
            var f = ctx.People;
            if (f == null || f.IsPlayer) return null;
            try
            {
                var bonds = Of(f);
                var way = PeopleTiesMath.Strongest(bonds.Select(b => b.way));
                if (way == TieWay.None) return null;
                var bond = bonds.FirstOrDefault(b => b.way == way);
                string key = "CC_Ties_Letter_" + way;
                // The fealty's way round: the colony sworn to them, or they to the colony.
                if (way == TieWay.Fealty && Fealty.With(f) == null) key += "Ours";
                // The envoy's: theirs here, or the colony's there.
                if (way == TieWay.Envoy && Envoys.With(f, out _) == null) key += "Ours";
                if (!key.CanTranslate()) return null;
                return key.Translate((f.Name ?? "").Named("PEOPLE"), (bond.who ?? "").Named("WHO")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: a letter's line", e);
                return null;
            }
        }

        // ================= Their gifts =================

        /// <summary>The colonist who carries the tie with this people (the first by PeopleTiesMath.CarrierRank) and is at this home, or null.</summary>
        public static Pawn Carrier(Faction f, Map map)
        {
            if (!On || f == null || map == null) return null;
            PeopleBond? best = null;
            foreach (var b in Of(f))
            {
                int rank = PeopleTiesMath.CarrierRank(b.way);
                if (rank < 0 || b.carrier == null || !b.carrier.Spawned || b.carrier.Map != map || b.carrier.Dead || b.carrier.Downed) continue;
                if (best == null || rank < PeopleTiesMath.CarrierRank(best.Value.way)) best = b;
            }
            return best?.carrier;
        }

        /// <summary>While a people's own event is cast: the colonist its gift goes to first (PeopleEvents.Takers yields only them, when they may have it).</summary>
        [ThreadStatic] private static Pawn giftTo;

        public static Pawn GiftTo => giftTo;

        /// <summary>The tie's carrier for the event's people (its callings') at this home, or null: the event is cast for them first.</summary>
        public static Pawn CarrierFor(IncidentDef d, Map map)
        {
            var callings = d?.GetModExtension<HappeningExtension>()?.callings;
            if (!On || callings.NullOrEmpty() || map == null) return null;
            try
            {
                foreach (var c in callings)
                    foreach (var f in OfCalling(c))
                    {
                        var p = Carrier(f, map);
                        if (p != null) return p;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: a gift's carrier", e);
            }
            return null;
        }

        /// <summary>Their gift goes first to <paramref name="carrier"/> until disposed.</summary>
        public static IDisposable Giving(Pawn carrier) => new GiftScope(carrier);

        private sealed class GiftScope : IDisposable
        {
            private readonly Pawn before;

            public GiftScope(Pawn p)
            {
                before = giftTo;
                giftTo = p;
            }

            public void Dispose() => giftTo = before;
        }

        // ================= For the dev tools and the cards =================

        /// <summary>The ties in a line ("a marriage (Ula), a pact"), for the dev tools.</summary>
        public static string Describe(Faction f)
        {
            var bonds = Of(f);
            if (bonds.Count == 0) return "none";
            return string.Join(", ", bonds.Select(b => b.way + (b.who.NullOrEmpty() ? "" : " (" + b.who + ")") + (b.carrier != null ? " carried by " + b.carrier.LabelShort : "")));
        }
    }
}
