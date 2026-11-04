using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where pacts and truces show (docs/design/interface.md, "Where stage 2 and stage 3 go"): each as a row on its people's
    /// card in Region → Peoples and on its town's card (DiplomacyModel), the tie's own card with its terms in words and its
    /// acts, the people card's acts (offer a truce, propose a pact, call them), and the one inbox: a call to arms and their
    /// offers waiting in Needs you, a truce to be sworn, a band or the colony's fighters on the way in Under way (in steps,
    /// never a time left), a truce broken in Coming. The Stance lens draws their lines, and the Threats lens dims a truce
    /// partner (UI/Region/LensModel.cs). The pieces are DiplomacyModel's (DipRow, DipAct), drawn by the pages' own code.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PactsPanels
    {
        static PactsPanels()
        {
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 5f, TieLines, null, "pact-terms");
            CardActs.Register(CardKind.Tie, r => PactWords.P("CC_Pact_Act_Call", PactWords.People(Pacts.PeopleOf(ThingRefs.Tie(r))).Named("PEOPLE")),
                r => Pacts.CallWhyNot(ThingRefs.Tie(r), Pacts.HomeOf(ThingRefs.Tie(r))), r => Pacts.Call(ThingRefs.Tie(r), Pacts.HomeOf(ThingRefs.Tie(r))),
                r => IsPact(ThingRefs.Tie(r), PactKind.Defence), 20f, PactWords.P("CC_Pact_Act_Call_Tip"), "pact-call");
            CardActs.Register(CardKind.Tie, r => PactWords.P("CC_Pact_Act_End"), r => EndWhyNot(ThingRefs.Tie(r)), r => EndByLetter(ThingRefs.Tie(r)),
                r => IsPact(ThingRefs.Tie(r), null), 60f, PactWords.P("CC_Pact_Act_End_Tip"), "pact-end");
            CardActs.Register(CardKind.People, r => PactWords.P("CC_Truce_Act_Offer"), r => OfferAct(ThingRefs.Faction(r)).why, r => OfferAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => Diplomacy.Hostile(ThingRefs.Faction(r)) && Truces.On, 30f, PactWords.P("CC_Truce_Act_Offer_Tip"), "truce-offer");
            CardActs.Register(CardKind.People, r => PactWords.P("CC_Pact_Act_Propose"), r => ProposeAct(ThingRefs.Faction(r)).why, r => ProposeAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && !Ages.IsEmpire(f) && Pacts.On, 32f, PactWords.P("CC_Pact_Act_Propose_Tip"), "pact-propose");
            CardActs.Register(CardKind.People, r => PactWords.P("CC_Pact_Act_Call", PactWords.People(ThingRefs.Faction(r)).Named("PEOPLE")),
                r => Pacts.CallWhyNot(ThingRefs.Faction(r), Home), r => Pacts.Call(Pacts.Of(ThingRefs.Faction(r), Home, PactKind.Defence), Home),
                r => Pacts.Of(ThingRefs.Faction(r), Home, PactKind.Defence) != null, 20f, PactWords.P("CC_Pact_Act_Call_Tip"), "pact-call-people");
            Inbox.Register("pacts", InboxSection.Needs, 32f, NeedRows, NeedCount);
            Inbox.Register("pacts-under-way", InboxSection.UnderWay, 52f, UnderWayRows);
            Inbox.Register("truces-broken", InboxSection.Coming, 25f, BrokenRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static bool IsPact(Tie t, PactKind? kind) => t != null && t.kind == Tie.Kind.Pact && t.origin == Pacts.Origin && t.Live && (kind == null || Pacts.KindOf(t) == kind);

        // ================= Rows for the people card and the town card =================

        /// <summary>The pacts and truces between this people and a home, as rows: in their age's words, since when, where they stand, and an act beside.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            foreach (var t in Truces.Live())
                if (Truces.PeopleOf(t) == f && (home == null || Truces.HomeOf(t) == home))
                    list.Add(TruceRow(t));
            foreach (var t in Pacts.Live())
                if (Pacts.PeopleOf(t) == f && (home == null || Pacts.HomeOf(t) == home))
                    list.Add(PactRow(t));
            return list;
        }

        public static DipRow TruceRow(Tie t)
        {
            var f = Truces.PeopleOf(t);
            var home = Truces.HomeOf(t);
            var form = PactWords.TruceForm(home, f);
            string text = PactWords.P(Truces.Pending(t) ? "CC_Truce_RowPending" : "CC_Truce_Row", form.word.Named("WORD"), PactWords.People(f).Named("PEOPLE"),
                PactWords.Term(Truces.TermOf(t)).Named("TERM"));
            string note = Truces.Pending(t)
                ? PactWords.P("CC_Truce_RowPendingNote", form.rite.Named("RITE"))
                : PactWords.Since(t.since) + "; " + Truces.FromPeaceLine(f);
            return new DipRow { text = text, note = note, color = UiKit.Cool, link = t, tip = PactWords.Price(Truces.PriceOf(t), t.terms.count).CapitalizeFirst() };
        }

        public static DipRow PactRow(Tie t)
        {
            var f = Pacts.PeopleOf(t);
            var home = Pacts.HomeOf(t);
            var form = PactWords.PactForm(home, f);
            var k = Pacts.KindOf(t);
            string text = PactWords.P("CC_Pact_Row", PactWords.Kind(k).Named("KIND"), PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"));
            var row = new DipRow { text = text, note = PactState(t), color = UiKit.Warm, link = t };
            if (k == PactKind.Defence)
            {
                var a = new DipAct { label = PactWords.P("CC_Pact_Act_CallShort"), tip = PactWords.P("CC_Pact_Act_Call_Tip"), why = Pacts.CallWhyNot(t, home) };
                if (a.why == null) a.run = () => Pacts.Call(t, home);
                row.act = a;
            }
            return row;
        }

        /// <summary>Where a pact stands in words: since when, and a call under way either way, or the cart of a trade pact.</summary>
        public static string PactState(Tie t)
        {
            var f = Pacts.PeopleOf(t);
            string since = PactWords.Since(t.since);
            if (t.terms.goods == Pacts.Calling) return since + "; " + PactWords.P("CC_Pact_State_Calling");
            if (Pacts.BandComing(t, out _)) return since + "; " + PactWords.P("CC_Pact_State_Band", PactWords.People(f).Named("PEOPLE"));
            if (PactMath.ParseArms(t.terms.goods, out int town, out _))
                return since + "; " + PactWords.P("CC_Pact_State_Arms", (NotableBook.TownOf(town)?.Name ?? "?").Named("TOWN"));
            if (Pacts.KindOf(t) == PactKind.Trade)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(t.terms.goods ?? "");
                if (def != null) return since + "; " + PactWords.P("CC_Pact_State_Cart", Couriers.Amount(t.terms.count, null, def).Named("GOODS"));
            }
            return since;
        }

        // ================= The people card's acts =================

        /// <summary>Offering a truce: a letter or a call to their head, its term and price chosen there.</summary>
        public static DipAct OfferAct(Faction f)
        {
            var home = Home;
            var a = new DipAct { label = PactWords.P("CC_Truce_Act_Offer"), tip = PactWords.P("CC_Truce_Act_Offer_Tip"), why = Truces.WhyNot(f, home) };
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", PactWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Truce);
            if (a.why == null)
            {
                var n = head;
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Truce, null));
            }
            return a;
        }

        /// <summary>Proposing a pact: a letter or a call to their head, the kind (and a trade pact's cart) chosen there.</summary>
        public static DipAct ProposeAct(Faction f)
        {
            var home = Home;
            var a = new DipAct { label = PactWords.P("CC_Pact_Act_Propose"), tip = PactWords.P("CC_Pact_Act_Propose_Tip"), why = Pacts.WhyNotAny(f, home) };
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", PactWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Pact);
            if (a.why == null)
            {
                var n = head;
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Pact, null));
            }
            return a;
        }

        /// <summary>The acts that fit a people on the Peoples view, each greyed with why not.</summary>
        public static IEnumerable<DipAct> Acts(Faction f, Map home)
        {
            if (f == null || !Truces.On) yield break;
            if (Diplomacy.Hostile(f))
            {
                yield return OfferAct(f);
                yield break;
            }
            var t = Pacts.Of(f, home, PactKind.Defence);
            if (t != null)
            {
                var call = new DipAct { label = PactWords.P("CC_Pact_Act_Call", PactWords.People(f).Named("PEOPLE")), tip = PactWords.P("CC_Pact_Act_Call_Tip"), why = Pacts.CallWhyNot(t, home) };
                if (call.why == null) call.run = () => Pacts.Call(t, home);
                yield return call;
            }
            if (!Ages.IsEmpire(f)) yield return ProposeAct(f);
        }

        private static string EndWhyNot(Tie t)
        {
            if (!IsPact(t, null)) return PactWords.P("CC_Truce_Why_Gone");
            if (Pacts.CallUnderWay(t)) return PactWords.P("CC_Pact_Why_EndCall");
            var f = Pacts.PeopleOf(t);
            var head = FavourCalls.HeadOf(f, Pacts.HomeOf(t));
            if (head == null) return DipWords.P("CC_Dip_Why_NoNotable", PactWords.People(f).Named("PEOPLE"));
            return NeighbourLetters.WhyNot(head, LetterPurpose.Pact);
        }

        private static void EndByLetter(Tie t)
        {
            var head = FavourCalls.HeadOf(Pacts.PeopleOf(t), Pacts.HomeOf(t));
            if (head != null) Find.WindowStack.Add(new Dialog_NeighbourLetter(head, LetterPurpose.Pact, DiplomacyLetters.PactEndPrefix + t.id));
        }

        // ================= The tie's card =================

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.IsView || (t.kind != Tie.Kind.Pact && t.kind != Tie.Kind.Truce)) return null;
            var list = new List<string>();
            try
            {
                if (t.kind == Tie.Kind.Truce && t.origin == Truces.Origin)
                {
                    var f = Truces.PeopleOf(t);
                    var row = TruceRow(t);
                    list.Add(row.text);
                    list.Add(PactWords.P("CC_Truce_Card_Price", PactWords.Price(Truces.PriceOf(t), t.terms.count).Named("PRICE")));
                    list.Add(PactWords.P(Truces.OursOffered(t) ? "CC_Card_OfferedByUs" : "CC_Card_OfferedByThem", PactWords.People(f).Named("PEOPLE")));
                    if (t.Live) list.Add(row.note);
                }
                else if (t.kind == Tie.Kind.Pact && t.origin == Pacts.Origin)
                {
                    var f = Pacts.PeopleOf(t);
                    list.Add(PactRow(t).text);
                    list.Add(PactWords.P("CC_Pact_What_" + Pacts.KindOf(t), PactWords.People(f).Named("PEOPLE")));
                    list.Add(PactWords.P(Pacts.OursOffered(t) ? "CC_Card_OfferedByUs" : "CC_Card_OfferedByThem", PactWords.People(f).Named("PEOPLE")));
                    if (t.Live)
                    {
                        list.Add(PactState(t));
                        if (Pacts.KindOf(t) == PactKind.Defence && t.terms.count > 0) list.Add(PactWords.P("CC_Pact_Card_Calls", t.terms.count.Named("COUNT")));
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: the tie's card", e);
            }
            return list;
        }

        // ================= The inbox =================

        /// <summary>Their letters waiting for the colony's answer: a truce or a pact offered, a call to arms on a pact (a debt's is the favours' row).</summary>
        private static IEnumerable<Letter> Waiting()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_TruceOffer || l is ChoiceLetter_PactOffer || l is ChoiceLetter_CallToArms c && Pacts.Get(c.tie) != null)
                    yield return l;
        }

        private static int NeedCount()
        {
            if (WorldLedger.Instance == null) return 0;
            int n = 0;
            foreach (var _ in Waiting()) n++;
            return n;
        }

        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var l in Waiting())
                {
                    var letter = l;
                    Action read = () => letter.OpenLetter();
                    var f = l.relatedFaction;
                    string kind = l is ChoiceLetter_TruceOffer ? "CC_Pact_Week_TruceOffer" : l is ChoiceLetter_PactOffer ? "CC_Pact_Week_PactOffer" : "CC_Pact_Week_Arms";
                    rows.Add(new WeekRow
                    {
                        icon = l is ChoiceLetter_CallToArms ? Icons.Raid : Icons.Promise ?? Icons.Trust,
                        color = l is ChoiceLetter_CallToArms ? UiKit.Soon : UiKit.Gold,
                        faction = f,
                        text = PactWords.P(kind, ThingRefs.Link(PactWords.People(f), ThingRefs.Of(f)).Named("PEOPLE")),
                        sub = PactWords.P("CC_Pact_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = InboxWords.Answer,
                        actRun = read,
                        follow = ThingRefs.Of(f),
                        order = l.arrivalTick
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: this week's asks", e);
            }
        }

        private static string[] truceSteps, bandSteps, armsSteps;

        private static string[] Steps(params string[] keys) => keys.Select(k => PactWords.P(k)).ToArray();

        /// <summary>A truce to be sworn, a band on its way, the colony's fighters on the road to a partner's town: in steps.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var t in Truces.Live())
                {
                    if (!Truces.Pending(t)) continue;
                    var f = Truces.PeopleOf(t);
                    var home = Truces.HomeOf(t);
                    var lord = TruceParley.LordOf(t, home);
                    int step = lord == null ? 0 : (lord.LordJob as LordJob_Parley)?.arrived >= 0 ? 2 : 1;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = UiKit.Cool,
                        faction = f,
                        text = WeekModel.AtHome(home, PactWords.P("CC_Pact_Week_Truce", ThingRefs.Link(PactWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"))),
                        steps = truceSteps ??= Steps("CC_Pact_Step_Agreed", "CC_Pact_Step_EnvoyRoad", "CC_Pact_Step_AtGate", "CC_Pact_Step_Sworn"),
                        step = step,
                        click = () => DiplomacyLinks.Open(t),
                        follow = ThingRefs.Of(t),
                        order = t.since
                    });
                }
                foreach (var t in Pacts.Live())
                {
                    var f = Pacts.PeopleOf(t);
                    var home = Pacts.HomeOf(t);
                    int step;
                    string key;
                    string[] steps;
                    if (t.terms.goods == Pacts.Calling || Pacts.BandComing(t, out _))
                    {
                        key = "CC_Pact_Week_Band";
                        steps = bandSteps ??= Steps("CC_Pact_Step_Calling", "CC_Pact_Step_BandRoad", "CC_Pact_Step_Here");
                        step = t.terms.goods == Pacts.Calling ? 0 : 1;
                    }
                    else if (PactMath.ParseArms(t.terms.goods, out int townId, out _))
                    {
                        var town = NotableBook.TownOf(townId);
                        if (town == null || CallsToArms.Answering(town) == null) continue;
                        key = "CC_Pact_Week_Fighters";
                        steps = armsSteps ??= Steps("CC_Pact_Step_Asked", "CC_Pact_Step_SetOut", "CC_Pact_Step_There");
                        step = 1;
                    }
                    else continue;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Raid,
                        color = UiKit.Warm,
                        faction = f,
                        text = WeekModel.AtHome(home, PactWords.P(key, ThingRefs.Link(PactWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"))),
                        steps = steps,
                        step = step,
                        click = () => DiplomacyLinks.Open(t),
                        follow = ThingRefs.Of(t),
                        order = t.since
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: this week's under way", e);
            }
        }

        /// <summary>A truce broken lately: their raids may come again (by them), or the colony's word is broken (by it).</summary>
        private static void BrokenRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            int now = Find.TickManager.TicksGame;
            try
            {
                foreach (var t in WorldLedger.OwnTies(Tie.Kind.Truce))
                {
                    if (t == null || t.origin != Truces.Origin || t.state != Tie.State.Broken || t.closed < 0 || now - t.closed > PactMath.SeasonDays * PactMath.DayTicks) continue;
                    var f = Truces.PeopleOf(t);
                    if (f == null || !Diplomacy.Hostile(f)) continue;
                    bool theirs = t.terms.goods == Truces.BrokenByThemKey;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Raid,
                        color = UiKit.Sour,
                        faction = f,
                        text = WeekModel.AtHome(Truces.HomeOf(t), PactWords.P(theirs ? "CC_Pact_Week_BrokenByThem" : "CC_Pact_Week_BrokenByUs",
                            ThingRefs.Link(PactWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"))),
                        note = InboxWords.When(t.closed),
                        click = () => DiplomacyLinks.Open(t),
                        follow = ThingRefs.Of(f),
                        order = -t.closed
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: this week's broken truces", e);
            }
        }
    }

    /// <summary>
    /// "Call the Lenlia": a threat is on a home that holds a defence pact the colony could call in now. A click makes the
    /// call (the caller to the console, or the fire laid by the gate); the explanation says how.
    /// </summary>
    public class Alert_PactCall : Alert
    {
        private Tie pact;
        private Map home;
        private int lookedAt = -1;

        public Alert_PactCall()
        {
            defaultPriority = AlertPriority.High;
        }

        public override string GetLabel() => pact != null ? PactWords.P("CC_Pact_Alert", PactWords.People(Pacts.PeopleOf(pact)).Named("PEOPLE")) : "";

        public override TaggedString GetExplanation()
        {
            if (pact == null) return "";
            var f = Pacts.PeopleOf(pact);
            var way = PactCalls.Means(home, f, out _);
            return PactWords.P("CC_Pact_AlertWhat", PactWords.People(f).Named("PEOPLE"),
                (way == PactCalls.Way.Comms ? PactWords.CallByComms(home) : PactWords.CallByFire(home)).Named("MEANS"));
        }

        public override AlertReport GetReport()
        {
            if (!Pacts.On || Current.Game == null) return false;
            int now = Find.TickManager.TicksGame;
            if (now / Sched.RareTicks != lookedAt)
            {
                lookedAt = now / Sched.RareTicks;
                pact = null;
                home = null;
                try
                {
                    foreach (var t in Pacts.Live())
                    {
                        if (Pacts.KindOf(t) != PactKind.Defence) continue;
                        var h = Pacts.HomeOf(t);
                        if (h == null || !MapFacts.ThreatOn(h) || Pacts.CallWhyNot(t, h) != null) continue;
                        pact = t;
                        home = h;
                        break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pacts: the call alert", e);
                }
            }
            return pact != null;
        }

        protected override void OnClick()
        {
            if (pact != null && home != null && Pacts.CallWhyNot(pact, home) == null) Pacts.Call(pact, home);
            lookedAt = -1;
        }
    }
}
