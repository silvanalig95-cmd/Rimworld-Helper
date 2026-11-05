using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Pacts with a people at peace with the colony (docs/design/stage2-diplomacy.md, round a, item 2; decision 2; the rules
    /// in PactMath.cs): a defence pact, each coming when the other is attacked, and a trade pact, their trader caravan each
    /// season and a cart of the colony's surplus to their porters. Proposed by letter or call (DiplomacyLetters) or face to
    /// face with one of theirs (DiplomacyActs), or offered by them (once a year at most, when their head thinks 50 or more
    /// of the colony and both have an enemy in common). A year, renewable at its end by their letter. The Empire makes no
    /// pacts (decision 9).
    /// <para>Calling a defence pact in while a threat is on a home: the caller at the comms console, or a signal fire laid
    /// and lit by the gate that their watchers see (PactCalls.cs). Their band walks in some hours later (Camps/WarCamp.cs:
    /// about half the threat), at most once a season per pact; nine times in ten they come, once in ten their riders are
    /// elsewhere (put off), and if the attacker is their friend they decline and the pact ends with nobody at fault. Their
    /// own calls on the colony's fighters are CallsToArms.cs. A debt of honour called in for fighters brings their band the
    /// same way. Nothing is saved but the ties.</para>
    /// </summary>
    public static class Pacts
    {
        public const string Origin = PactMath.PactOrigin;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Day => Now / (float)PactMath.DayTicks;

        public static bool On => Truces.On;

        /// <summary>A call of the colony's being made (the caller at the console, the fire being laid).</summary>
        public const string Calling = "call";

        // ================= Reading =================

        public static List<Tie> Live()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Pact))
                if (t != null && t.Live && t.origin == Origin)
                    list.Add(t);
            return list;
        }

        public static Tie Get(int id)
        {
            var t = WorldLedger.Get(id);
            return t != null && t.kind == Tie.Kind.Pact && t.origin == Origin ? t : null;
        }

        public static PactKind KindOf(Tie t)
        {
            PactMath.ParseKind(t?.terms?.key, out var k);
            return k;
        }

        public static Faction PeopleOf(Tie t) => Truces.PeopleOf(t);

        public static Map HomeOf(Tie t) => Truces.HomeOf(t);

        public static bool OursOffered(Tie t) => Truces.OursOffered(t);

        /// <summary>The live pact of a kind between this home and that people, or null.</summary>
        public static Tie Of(Faction f, Map home, PactKind k)
        {
            if (f == null) return null;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            string key = PactMath.KindKey(k);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Pact))
                if (t != null && t.Live && t.origin == Origin && t.terms?.key == key && t.Between(people, colony))
                    return t;
            return null;
        }

        /// <summary>The live pacts with that people, at any home.</summary>
        public static List<Tie> With(Faction f) => f == null ? new List<Tie>() : Live().Where(t => PeopleOf(t) == f).ToList();

        /// <summary>A call under way on this pact: the colony's (being made, or their band on its way) or theirs (fighters asked of the colony).</summary>
        public static bool CallUnderWay(Tie t) => t != null && !t.terms.goods.NullOrEmpty() && KindOf(t) == PactKind.Defence;

        public static bool BandComing(Tie t, out float day)
        {
            day = 0f;
            return t != null && KindOf(t) == PactKind.Defence && PactMath.ParseBand(t.terms.goods, out day);
        }

        // ================= Proposing one =================

        public static int HeadRegard(Faction f, Map home)
        {
            var head = FavourCalls.HeadOf(f, home);
            return head != null ? NotableBook.Opinion(head) : f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;
        }

        private static int Goodwill(Faction f) => f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;

        /// <summary>A people at war with both the colony and them.</summary>
        public static Faction CommonEnemy(Faction f)
        {
            if (f == null) return null;
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
                if (o != null && o != f && !o.IsPlayer && Diplomacy.Deals(o) && Diplomacy.Hostile(o) && o.HostileTo(f))
                    return o;
            return null;
        }

        /// <summary>Why a pact of this kind can't be proposed to them now, or null.</summary>
        public static string WhyNot(Faction f, Map home, PactKind k)
        {
            if (!On) return PactWords.P("CC_Truce_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Ages.IsEmpire(f)) return PactWords.P("CC_Pact_Why_Empire", PactWords.People(f).Named("PEOPLE"));
            if (Diplomacy.Hostile(f)) return DipWords.P("CC_Dip_Why_War", PactWords.People(f).Named("PEOPLE"));
            if (Goodwill(f) < PactMath.PactGoodwill) return PactWords.P("CC_Pact_Why_Goodwill", PactWords.People(f).Named("PEOPLE"), PactMath.PactGoodwill.Named("NEED"));
            if (Of(f, home, k) != null) return PactWords.P("CC_Pact_Why_Already", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(k).Named("KIND"));
            if (k == PactKind.Trade && CartGoods(home).Count == 0) return PactWords.P("CC_Pact_Why_NoSurplus");
            return null;
        }

        /// <summary>Why no pact at all can be proposed to them (both kinds can't), or null.</summary>
        public static string WhyNotAny(Faction f, Map home)
        {
            string a = WhyNot(f, home, PactKind.Defence);
            if (a == null) return null;
            return WhyNot(f, home, PactKind.Trade) == null ? null : a;
        }

        public static int Score(Faction f, Map home, PactKind k) => PactMath.PactScore(Goodwill(f), HeadRegard(f, home), CommonEnemy(f) != null, k);

        /// <summary>
        /// They hear the colony's proposal (its letter read, the call heard, or face to face): sworn or not, by their goodwill
        /// and their head's regard, an enemy in common and the kind. Returns their answer in words.
        /// </summary>
        public static string Hear(Faction f, Map home, PactKind k, ThingDef cart, int cartCount, float roll, out bool accepted)
        {
            accepted = false;
            var form = PactWords.PactForm(home, f);
            if (WhyNot(f, home, k) != null) return PactWords.T("CC_Pact_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
            if (!PactMath.PactAccepted(Score(f, home, k), roll))
                return PactWords.T("CC_Pact_Ans_No", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), PactWords.Kind(k).Named("KIND"));
            var t = Make(f, home, k, true, cart, cartCount);
            if (t == null) return PactWords.T("CC_Pact_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
            accepted = true;
            return PactWords.T("CC_Pact_Ans_Yes", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), PactWords.Kind(k).Named("KIND"))
                   + (k == PactKind.Trade && cart != null ? " " + PactWords.P("CC_Pact_CartTerms", Couriers.Amount(cartCount, null, cart).Named("GOODS")) : "");
        }

        /// <summary>A pact made: from whoever offered it, a year from now, the cart in a trade pact's terms; their people remember the oath.</summary>
        public static Tie Make(Faction f, Map home, PactKind k, bool oursOffered, ThingDef cart, int cartCount)
        {
            if (f == null || Of(f, home, k) != null) return null;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            var terms = new TieTerms { key = PactMath.KindKey(k), number = Day + PactMath.PactDays };
            if (k == PactKind.Trade && cart != null)
            {
                terms.goods = cart.defName;
                terms.count = Mathf.Max(1, cartCount);
            }
            var t = WorldLedger.AddTie(Tie.Kind.Pact, oursOffered ? colony : people, oursOffered ? people : colony, terms, Tie.Never, Origin);
            if (t == null) return null;
            FactionMemory.Note(f, "swore", PactWords.Kind(k));
            Diplomacy.Dirty();
            return t;
        }

        // ================= The trade pact's cart =================

        /// <summary>What a cart could carry: the colony's most plentiful goods in its stores (bulk goods, not silver), with the count for a cart.</summary>
        public static List<(ThingDef def, int count)> CartGoods(Map home)
        {
            var list = new List<(ThingDef def, int count)>();
            if (home?.resourceCounter == null) return list;
            try
            {
                foreach (var kv in home.resourceCounter.AllCountedAmounts.OrderByDescending(kv => kv.Value * (kv.Key?.BaseMarketValue ?? 0f)))
                {
                    var d = kv.Key;
                    if (d == null || kv.Value <= 0 || d == ThingDefOf.Silver || d.category != ThingCategory.Item || d.stackLimit <= 1 || d.IsCorpse) continue;
                    if (d.tradeability != Tradeability.All && d.tradeability != Tradeability.Sellable) continue;
                    if (d.BaseMarketValue < 0.3f || d.BaseMarketValue > 60f) continue;
                    int n = PactMath.CartCount(d.BaseMarketValue, kv.Value);
                    if (n <= 0 || kv.Value < 3) continue;
                    list.Add((d, n));
                    if (list.Count >= 5) break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: the cart's goods", e);
            }
            return list;
        }

        // ================= Their offer =================

        public static int LastOffer(Faction f)
        {
            var r = Refs.Of(f);
            foreach (var e in History.Of(r, new[] { DiplomacyMath.PactOffer }, Now - (int)(PactMath.TheirOfferGapDays * PactMath.DayTicks)))
                return e.tick;
            return -1;
        }

        /// <summary>The day's look: a friend with an enemy in common, whose head thinks 50 or more of the colony, may offer a pact (once a year at most).</summary>
        public static void Daily(int now)
        {
            if (!On || !Tell.CanAsk) return;
            var home = Homes.Seat;
            if (home == null) return;
            foreach (var r in RegionTowns.Reach)
                foreach (var s in r.towns)
                {
                    var f = s?.Faction;
                    if (f == null || !Diplomacy.Deals(f) || Of(f, home, PactKind.Defence) != null) continue;
                    var enemy = CommonEnemy(f);
                    if (!PactMath.MayOfferPact(HeadRegard(f, home), Goodwill(f), enemy != null, Diplomacy.Hostile(f), Ages.IsEmpire(f), LastOffer(f), now)) continue;
                    if (!Rand.Chance(PactMath.TheirOfferDaily)) continue;
                    Offer(f, home, PactKind.Defence, enemy, null);
                    return;
                }
        }

        /// <summary>Their offer (or their offer to renew one at its end), as a letter: accept, or let it be.</summary>
        public static void Offer(Faction f, Map home, PactKind k, Faction enemy, Tie renew)
        {
            var def = PactsDefOf.Rimfolk_PactOffer;
            if (def == null || f == null || home == null) return;
            var head = FavourCalls.HeadOf(f, home);
            var form = PactWords.PactForm(home, f);
            string who = head != null ? NotableBook.Called(head) : PactWords.People(f);
            string text = renew != null
                ? PactWords.T("CC_Pact_Renew", who.Named("WHO"), PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), PactWords.Kind(k).Named("KIND"))
                : PactWords.T("CC_Pact_Offer", who.Named("WHO"), PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), PactWords.Kind(k).Named("KIND"),
                    PactWords.People(enemy).Named("ENEMY"));
            text += "\n\n" + PactWords.P("CC_Pact_What_" + k, PactWords.People(f).Named("PEOPLE"));
            // A hostage given for the year goes home with it (Hostages.EndOf), renewed or not; the renewal may ask for a new one.
            if (renew != null && k == PactKind.Defence && Hostages.For(renew).Count > 0) text += "\n\n" + PactWords.P("CC_Pact_Renew_Hostage", PactWords.People(f).Named("PEOPLE"));
            var town = head != null ? NotableBook.TownOf(head.town) : WorldSituationWorker.NearestTown(f, home);
            var letter = LetterMaker.MakeLetter(PactWords.P(renew != null ? "CC_Pact_Renew_Label" : "CC_Pact_Offer_Label", PactWords.People(f).Named("PEOPLE")), text, def,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_PactOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.kind = (int)k;
                l.renew = renew?.id ?? -1;
            }
            if (renew == null)
                Diplomacy.Write(DiplomacyMath.PactOffer, new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For) },
                    Diplomacy.Colony(home), PactWords.P("CC_Pact_Hist_Offer", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(k).Named("KIND")));
            Tell.Ask(letter, home);
        }

        /// <summary>Their offer taken: a new pact, or the old one another year.</summary>
        public static void Accept(Faction f, Map home, PactKind k, int renew)
        {
            var old = Get(renew);
            var form = PactWords.PactForm(home, f);
            if (old != null && old.Live)
            {
                old.terms.number = Day + PactMath.PactDays;
                renewAsked.Remove(old.id);
                WorldLedger.Touch(old);
                FactionMemory.Note(f, "keptWord", PactWords.Kind(k));
                // apart: the colony's answer to their letter, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, PactWords.P("CC_Pact_Renewed", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(k).Named("KIND")),
                    LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                return;
            }
            ThingDef cart = null;
            int count = 0;
            if (k == PactKind.Trade)
            {
                var goods = CartGoods(home);
                if (goods.Count > 0) (cart, count) = goods[0];
            }
            if (Make(f, home, k, false, cart, count) == null) return;
            // apart: the colony's answer to their letter, told in a message.
            Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P("CC_Pact_Made", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(k).Named("KIND"),
                form.word.Named("WORD")), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): each pact's end, calls being made and bands on their way, a trade pact's season; favours' bands.</summary>
        public static void Hourly(int now)
        {
            float day = now / (float)PactMath.DayTicks;
            foreach (var t in Live())
            {
                try
                {
                    var f = PeopleOf(t);
                    var home = HomeOf(t);
                    if (f == null || f.defeated || home == null)
                    {
                        End(t, Tie.State.Ended, null);
                        continue;
                    }
                    // At war with the colony now (their goodwill fell away): the pact ends with them.
                    if (Diplomacy.Hostile(f))
                    {
                        End(t, Tie.State.Ended, PactWords.P("CC_Pact_Msg_War", PactWords.People(f).Named("PEOPLE")));
                        continue;
                    }
                    if (KindOf(t) == PactKind.Defence) DefenceHour(t, f, home, day);
                    else TradeHour(t, f, home, day);
                    if (t.Live && day >= t.terms.number && !CallUnderWay(t)) YearOver(t, f, home);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pacts: the hour", e);
                }
            }
            FavourBands(day);
        }

        /// <summary>Pacts whose renewal was offered (this session): left unanswered a season, they end.</summary>
        private static readonly HashSet<int> renewAsked = new HashSet<int>();

        /// <summary>Its year is over: they offer to renew it while they think well enough of the colony (once), else it ends.</summary>
        private static void YearOver(Tie t, Faction f, Map home)
        {
            var k = KindOf(t);
            bool asked = Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_PactOffer o && o.renew == t.id);
            if (asked) return;
            if (On && !renewAsked.Contains(t.id) && PactMath.Renews(Goodwill(f), HeadRegard(f, home), Diplomacy.Hostile(f)))
            {
                // Their letter comes when one of Rimfolk's questions may: the pact holds a little longer meanwhile.
                if (!Tell.CanAsk) return;
                renewAsked.Add(t.id);
                Offer(f, home, k, null, t);
                // Their letter waits the season for its answer; unanswered by then, it ends.
                t.terms.number = Day + PactMath.SeasonDays;
                WorldLedger.Touch(t);
                return;
            }
            End(t, Tie.State.Ended, PactWords.P("CC_Pact_Msg_Over", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(k).Named("KIND")));
        }

        /// <summary>Closed (kept: run its course; broken; ended: with nobody at fault), and a message when one is given.</summary>
        public static void End(Tie t, Tie.State state, string message)
        {
            if (t == null || !t.Live) return;
            var home = HomeOf(t);
            WorldLedger.Set(t, state);
            Diplomacy.Dirty();
            if (!message.NullOrEmpty())
                // apart: what became of the pact, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, message, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        // ================= A defence pact: the colony's call =================

        /// <summary>Who is making a call (the caller at the console, the one laying the fire), by pact: this session's only.</summary>
        private static readonly Dictionary<int, (Pawn by, int at)> callers = new Dictionary<int, (Pawn, int)>();

        /// <summary>Why the colony can't call this people's defence pact in now, or null.</summary>
        public static string CallWhyNot(Faction f, Map home)
        {
            if (!On) return PactWords.P("CC_Truce_Why_Off");
            var t = Of(f, home, PactKind.Defence);
            if (t == null) return PactWords.P("CC_Pact_Why_NoDefence", PactWords.People(f).Named("PEOPLE"));
            return CallWhyNot(t, home);
        }

        public static string CallWhyNot(Tie t, Map home)
        {
            var f = PeopleOf(t);
            if (home == null || !MapFacts.ThreatOn(home)) return PactWords.P("CC_Pact_Why_NoThreat");
            if (t.terms.goods == Calling) return PactWords.P("CC_Pact_Why_Calling");
            if (BandComing(t, out _)) return PactWords.P("CC_Pact_Why_BandComing", PactWords.People(f).Named("PEOPLE"));
            if (PactMath.ParseArms(t.terms.goods, out _, out _)) return PactWords.P("CC_Pact_Why_TheirCall", PactWords.People(f).Named("PEOPLE"));
            if (!PactMath.MayCall(t.terms.days, Day)) return PactWords.P("CC_Pact_Why_Season", PactWords.People(f).Named("PEOPLE"));
            if (CampTracker.WarBandAt(home) != null) return PactWords.P("CC_Pact_Why_BandHere");
            if (PactCalls.Means(home, f, out string why) == PactCalls.Way.None) return why;
            return null;
        }

        /// <summary>The call begins: the caller goes to the console, or someone lays a signal fire by the gate.</summary>
        public static bool Call(Tie t, Map home)
        {
            if (t == null || CallWhyNot(t, home) != null) return false;
            var f = PeopleOf(t);
            var by = PactCalls.Begin(t, home, f);
            if (by == null) return false;
            t.terms.goods = Calling;
            WorldLedger.Touch(t);
            callers[t.id] = (by, Now);
            Diplomacy.Dirty();
            return true;
        }

        /// <summary>
        /// The call is made (the caller has spoken at the console, or the fire burns by the gate): their answer, by whether
        /// the attacker is their friend and the roll: their band sets out, their riders are elsewhere, or they decline and
        /// the pact ends with nobody at fault.
        /// </summary>
        public static void CallMade(Tie t, Map home, Pawn by, bool fire)
        {
            var f = PeopleOf(t);
            if (t == null || f == null || !t.Live || t.terms.goods != Calling) return;
            callers.Remove(t.id);
            var enemy = WarCamps.Attacker(home);
            float day = Day;
            t.terms.goods = null;
            t.terms.days = day;
            t.terms.count++;
            var form = PactWords.PactForm(home, f);
            string means = fire ? PactWords.CallByFire(home) : PactWords.CallByComms(home);
            var answer = PactMath.Answer(enemy != null && enemy != f && !enemy.HostileTo(f) && enemy.def.humanlikeFaction, Rand.Value);
            switch (answer)
            {
                case PactAnswer.Decline:
                    WorldLedger.Touch(t);
                    End(t, Tie.State.Ended, null);
                    Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P("CC_Pact_Decline_Label", PactWords.People(f).Named("PEOPLE")),
                        PactWords.T("CC_Pact_Decline", PactWords.People(f).Named("PEOPLE"), PactWords.People(enemy).Named("ENEMY"), form.word.Named("WORD")), LetterDefOf.NeutralEvent,
                        LookTargets.Invalid, f);
                    return;
                case PactAnswer.PutOff:
                    WorldLedger.Touch(t);
                    // apart: their answer to the call, told in a message.
                    Tell.Message(NewsKind.World, Importance.Important, home, PactWords.T("CC_Pact_PutOff", PactWords.People(f).Named("PEOPLE"), means.Named("MEANS")),
                        LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
                    break;
                default:
                    float hours = PactMath.BandHours(TilesTo(f, home));
                    t.terms.goods = PactMath.BandCode(day + hours / 24f);
                    t.terms.about = Refs.Of(enemy);
                    WorldLedger.Touch(t);
                    // apart: their answer to the call; their band walks in later, on the map.
                    Tell.Message(NewsKind.World, Importance.Important, home, PactWords.T("CC_Pact_Coming", PactWords.People(f).Named("PEOPLE"), means.Named("MEANS")),
                        by != null && by.Spawned ? new LookTargets(by) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    break;
            }
            Diplomacy.Dirty();
        }

        /// <summary>A call that was being made but nobody finished (the caller called away, the fire never lit): let go after some hours.</summary>
        private static void StaleCall(Tie t)
        {
            if (callers.TryGetValue(t.id, out var c) && Now - c.at < 6 * GenDate.TicksPerHour && c.by != null && !c.by.Dead && PactCalls.AtIt(c.by)) return;
            callers.Remove(t.id);
            t.terms.goods = null;
            WorldLedger.Touch(t);
            Diplomacy.Dirty();
        }

        private static float TilesTo(Faction f, Map home)
        {
            var town = WorldSituationWorker.NearestTown(f, home);
            return town != null ? WorldSituationWorker.Distance(home, town) : 6f;
        }

        private static void DefenceHour(Tie t, Faction f, Map home, float day)
        {
            if (t.terms.goods == Calling)
            {
                StaleCall(t);
                return;
            }
            if (BandComing(t, out float due))
            {
                if (day < due) return;
                if (Arrive(f, home, Refs.Faction(t.terms.about), "pact", out bool wait) || !wait)
                {
                    t.terms.goods = null;
                    WorldLedger.Touch(t);
                    Diplomacy.Dirty();
                }
                return;
            }
            CallsToArms.PactHour(t, f, home, day);
        }

        // ================= Their band walks in =================

        /// <summary>
        /// Their band is due at a home: they walk in from an edge and hunt the enemy (Camps/WarCamp.cs), or, with the fight
        /// already over, they turn back with a word. False with <paramref name="wait"/> while another band of fighters is on the
        /// map (they wait some hours for it). <paramref name="words"/>: "fealty" for a liege's band or "vassal" for a vassal's
        /// levies (their own letters and history lines, Fealty.cs and Vassals.cs); <paramref name="strength"/>: how strong
        /// the band comes against the usual half of the threat (a vassal's levies, a third of it).
        /// </summary>
        public static bool Arrive(Faction f, Map home, Faction enemy, string called, out bool wait, string words = null, float strength = 1f)
        {
            wait = false;
            if (f == null || home == null) return false;
            var form = words == "fealty" ? FealtyWords.Form(home, f) : words == "vassal" ? FealtyWords.LevyForm(home, f) : PactWords.PactForm(home, f);
            string keys = words == "fealty" ? "CC_Fealty_Band" : words == "vassal" ? "CC_Vassal_Band" : "CC_Pact_Band";
            string hist = words == "fealty" ? "CC_Fealty_Hist_Band" : words == "vassal" ? "CC_Vassal_Hist_Band" : "CC_Pact_Hist_Band";
            if (!MapFacts.ThreatOn(home))
            {
                // apart: their band reached the colony's land after the fight; told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, PactWords.T(keys + "_Late", PactWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
                Write(f, home, enemy, hist + "Late");
                return true;
            }
            if (CampTracker.WarBandAt(home) != null)
            {
                wait = true;
                return false;
            }
            enemy ??= WarCamps.Attacker(home);
            var pawns = WarCamps.MakeFighters(f, home, TaskResults.LevyStrength(f, home) * strength);
            var band = pawns.Count > 0 ? WarCamps.Start(home, f, enemy, pawns) : null;
            if (band == null)
            {
                foreach (var p in pawns) Arrivals.Discard(p);
                wait = true;
                return false;
            }
            band.called = called;
            Write(f, home, enemy, hist);
            Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P(keys + "_Label", PactWords.People(f).Named("PEOPLE")),
                PactWords.T(keys, PactWords.People(f).Named("PEOPLE"), PactWords.People(enemy).Named("ENEMY"), pawns.Count.Named("COUNT"), form.word.Named("WORD")),
                LetterDefOf.PositiveEvent, new LookTargets(pawns.Cast<Thing>()), f);
            return true;
        }

        private static void Write(Faction f, Map home, Faction enemy, string key)
        {
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For) };
            if (enemy != null) actors.Add(new HistoryActor(Refs.Of(enemy), HistoryRole.Against));
            Diplomacy.Write(DiplomacyMath.Band, actors, Diplomacy.Colony(home), PactWords.P(key, PactWords.People(f).Named("PEOPLE"), PactWords.People(enemy).Named("ENEMY")), true);
        }

        /// <summary>A debt of honour called in for fighters: their band walks in when it is due, and the debt is kept.</summary>
        private static void FavourBands(float day)
        {
            foreach (var t in FavoursOwed.Live())
            {
                if (t.state != Tie.State.CalledIn || FavoursOwed.ColonyOwes(t) || !FavourMath.ParseAsk(t.terms.goods, out var ask, out var arg) || ask != FavourAsk.Fighters) continue;
                if (t.terms.days <= 0f || day < t.terms.days) continue;
                var f = FavoursOwed.PeopleOf(t);
                var home = int.TryParse(arg, out int key) ? Homes.MapOf(key) : FavoursOwed.HomeOf(t);
                try
                {
                    if (f == null || home == null || Diplomacy.Hostile(f))
                    {
                        FavoursOwed.Close(t, Tie.State.Ended);
                        continue;
                    }
                    if (!Arrive(f, home, null, "favour", out bool wait) && wait)
                    {
                        t.terms.days = day + 0.1f;
                        continue;
                    }
                    FavoursOwed.Close(t, Tie.State.Kept);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("favours: their band", e);
                }
            }
        }

        // ================= A trade pact's season =================

        private static void TradeHour(Tie t, Faction f, Map home, float day)
        {
            if (!PactMath.SeasonDue(t.terms.days, day) || MapFacts.ThreatOn(home)) return;
            int hour = GenLocalDate.HourOfDay(home);
            if (hour < 8 || hour > 15) return;
            t.terms.days = day;
            WorldLedger.Touch(t);
            var form = PactWords.PactForm(home, f);
            // Their caravan comes to trade, as the pact promised.
            bool came = false;
            try
            {
                var inc = IncidentDefOf.TraderCaravanArrival;
                var parms = StorytellerUtility.DefaultParmsNow(inc.category, home);
                parms.faction = f;
                parms.forced = true;
                came = inc.Worker.CanFireNow(parms) && inc.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: their caravan", e);
            }
            // The colony's cart: carried out to the gate, where their porters take it and pay for it.
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(t.terms.goods ?? "");
            int count = t.terms.count;
            string cart;
            if (def != null && count > 0 && PlayCarry.Have(home, def.defName, def) >= count)
            {
                var ctx = new DilemmaContext { map = home, faction = f };
                // apart: one of the colony carries the cart's load out to the gate, where their porters wait for it.
                new DilemmaEffect_CarryOut { what = def.defName, count = count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = PactWords.P("CC_Pact_CartLabel", PactWords.People(f).Named("PEOPLE")) }
                    .Apply(ctx);
                int pay = PactMath.CartPay(def.BaseMarketValue, count);
                Couriers.PaySilver(home, DropCellFinder.TradeDropSpot(home), pay, f);
                cart = PactWords.P("CC_Pact_Cart", Couriers.Amount(count, null, def).Named("GOODS"), pay.Named("SILVER"));
            }
            else cart = def != null ? PactWords.P("CC_Pact_CartShort", Couriers.Amount(count, null, def).Named("GOODS")) : null;
            Tell.Letter(NewsKind.World, Importance.Notable, home, PactWords.P("CC_Pact_Season_Label", PactWords.People(f).Named("PEOPLE")),
                PactWords.T(came ? "CC_Pact_Season" : "CC_Pact_Season_NoCaravan", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD")) + (cart.NullOrEmpty() ? "" : "\n\n" + cart),
                LetterDefOf.PositiveEvent, LookTargets.Invalid, f);
        }

        // ================= Broken =================

        /// <summary>The colony broke it (a deed of its against them): every pact with them broken, "brokePact", and its word broken in every ear in reach. True when one was.</summary>
        public static bool BrokenByUs(Faction f, string how)
        {
            var pacts = With(f);
            if (pacts.Count == 0) return false;
            foreach (var t in pacts)
            {
                WorldLedger.Set(t, Tie.State.Broken);
                var home = HomeOf(t);
                Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P("CC_Pact_BrokenByUs_Label", PactWords.People(f).Named("PEOPLE")),
                    PactWords.T("CC_Pact_BrokenByUs", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(KindOf(t)).Named("KIND"), how.Named("HOW")), LetterDefOf.NegativeEvent,
                    LookTargets.Invalid, f);
            }
            Diplomacy.Dirty();
            FactionMemory.Note(f, "brokePact", how);
            Truces.WordBroken(f);
            return true;
        }

        /// <summary>The colony ends a pact by its letter, without blame: ended, and their people remember it was done openly.</summary>
        public static void EndByLetter(Tie t)
        {
            var f = PeopleOf(t);
            if (t == null || f == null) return;
            End(t, Tie.State.Ended, PactWords.P("CC_Pact_Msg_EndedByUs", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(KindOf(t)).Named("KIND")));
        }
    }

    /// <summary>Their offer of a pact (or to renew one at its end), as a letter: accept, or let it be.</summary>
    public class ChoiceLetter_PactOffer : ChoiceLetter
    {
        public Faction people;
        public int home = HomesMath.None;
        public int kind;
        public int renew = -1;

        private bool Valid(out Map map)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            if (people == null || people.defeated || Diplomacy.Hostile(people) || map == null) return false;
            if (renew >= 0) return Pacts.Get(renew)?.Live ?? false;
            return Pacts.Of(people, map, (PactKind)kind) == null;
        }

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Valid(out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                Map map = null;
                if (ArchivedOnly || !Valid(out map))
                {
                    yield return Option_Close;
                    yield break;
                }
                var m = map;
                yield return new DiaOption(PactWords.P(renew >= 0 ? "CC_Pact_Opt_Renew" : "CC_Pact_Opt_Accept"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (Valid(out var mm)) Pacts.Accept(people, mm, (PactKind)kind, renew);
                    },
                    resolveTree = true
                };
                // A defence pact renewed may take a new hostage as a term of its own (the last one goes home with the year: Hostages.EndOf).
                if (renew >= 0 && (PactKind)kind == PactKind.Defence && Hostages.On)
                    foreach (var o in Hostages.PactOptions(people, m))
                    {
                        if (!WardMath.ParseHostage(o.code, out _, out _)) continue;
                        string code = o.code;
                        var opt = new DiaOption(PactWords.P("CC_Pact_Opt_RenewHostage", o.label.Named("TERM")))
                        {
                            action = () =>
                            {
                                Find.LetterStack.RemoveLetter(this);
                                if (!Valid(out var mm)) return;
                                Pacts.Accept(people, mm, (PactKind)kind, renew);
                                string given = Hostages.ForPact(people, mm, (PactKind)kind, code);
                                if (!given.NullOrEmpty())
                                    // apart: the colony's answer to their letter, told in a message; the hostage comes or goes on the map.
                                    Tell.Message(NewsKind.World, Importance.Notable, mm, given, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                            },
                            resolveTree = true
                        };
                        if (o.why != null) opt.Disable(o.why);
                        yield return opt;
                    }
                yield return new DiaOption(PactWords.P(renew >= 0 ? "CC_Pact_Opt_LetEnd" : "CC_Pact_Opt_Refuse"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        var old = Pacts.Get(renew);
                        if (old != null && old.Live) Pacts.End(old, Tie.State.Ended, PactWords.P("CC_Pact_Msg_Over", PactWords.People(people).Named("PEOPLE"),
                            PactWords.Kind((PactKind)kind).Named("KIND")));
                    },
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref renew, "renew", -1);
        }
    }

    /// <summary>Their offer of a truce after a raid of theirs was beaten, as a letter: accept (their envoy comes to the gate), or refuse.</summary>
    public class ChoiceLetter_TruceOffer : ChoiceLetter
    {
        public Faction people;
        public int home = HomesMath.None;
        public int price;
        public int silver;

        private bool Valid(out Map map)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            return people != null && !people.defeated && Diplomacy.Hostile(people) && map != null && Truces.Of(people, map) == null && Truces.On;
        }

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Valid(out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid(out _))
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption(PactWords.P("CC_Truce_Opt_Accept"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (!Valid(out var map)) return;
                        Truces.MakePending(people, map, TruceTerm.Season, (TrucePrice)price, silver, false, null,
                            Rand.Range(PactMath.EnvoyHoursMin, PactMath.EnvoyHoursMax) / 24f);
                        // apart: the colony's answer to their letter; their envoy comes to the gate later.
                        Tell.Message(NewsKind.World, Importance.Notable, map, PactWords.P(Truces.ByWord(people) ? "CC_Truce_AcceptedByWord" : "CC_Truce_Accepted", PactWords.People(people).Named("PEOPLE")), LookTargets.Invalid,
                            MessageTypeDefOf.PositiveEvent);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(PactWords.P("CC_Truce_Opt_Refuse"))
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref price, "price");
            Scribe_Values.Look(ref silver, "silver");
        }
    }

    [DefOf]
    public static class PactsDefOf
    {
        public static LetterDef Rimfolk_TruceOffer;
        public static LetterDef Rimfolk_PactOffer;
        public static LetterDef Rimfolk_CallToArms;
        public static JobDef Rimfolk_TruceParley;
        public static JobDef Rimfolk_SignalFire;
        public static JobDef Rimfolk_PactCall;

        static PactsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PactsDefOf));
        }
    }
}
