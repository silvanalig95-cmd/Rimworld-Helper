using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where ransom and the envoy show (docs/design/interface.md, "Where stage 2 and stage 3 go"): the captive's card (an
    /// offer for them, offering them for ransom, an exchange, the letter to answer), the card of one of the colony's they
    /// hold, their envoy's Person card (how the stay goes, the bed set aside, seeing them off), the people card and a town's
    /// card (what is under way with them, asking for their envoy, sending the colony's own, an exchange), the tie's card
    /// for a grudge of refusals; rows on the Peoples view and a town's ties (DiplomacyModel); Here and away (their envoy
    /// here, the colony's away); and the one inbox: an offer for a captive and the envoy's asks in Needs you, an offer on
    /// its way, an exchange set, the envoy's stay and the colony's envoy away in Under way, in steps (never a time left).
    /// Needs you counts through Inbox.Count (the providers' counts).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RansomPanels
    {
        static RansomPanels()
        {
            // The colony's own card: a captive of theirs in its cells, one of its own they hold, its envoy away.
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 30f, ColonistLines, null, "ransom-colonist");
            CardActs.Register(CardKind.Colonist, r => RansomWords.P("CC_Ransom_Act_Answer"), r => null, r => AnswerLetter(Pawn(r))?.OpenLetter(),
                r => AnswerLetter(Pawn(r)) != null, 10f, RansomWords.P("CC_Ransom_Act_Answer_Tip"), "ransom-answer");
            CardActs.Register(CardKind.Colonist, r => RansomWords.P("CC_Ransom_Act_Offer"), r => OfferAct(Pawn(r)).why, r => OfferAct(Pawn(r)).run?.Invoke(),
                r => Pawn(r) is Pawn p && p.IsPrisonerOfColony && Ransoms.Deals(p.Faction) && Ransoms.On, 40f, RansomWords.P("CC_Ransom_Act_Offer_Tip"), "ransom-offer");
            CardActs.Register(CardKind.Colonist, r => RansomWords.P("CC_Ransom_Act_Exchange"), r => ExchangeAct(Pawn(r), null).why, r => ExchangeAct(Pawn(r), null).run?.Invoke(),
                r => Ransoms.On && ExchangeShows(Pawn(r)), 42f, RansomWords.P("CC_Ransom_Act_Exchange_Tip"), "ransom-exchange");
            CardActs.Register(CardKind.Colonist, r => RansomWords.P("CC_Envoy_Act_Recall"), r => RecallWhyNot(Pawn(r)), r => EnvoysAbroad.Recall(EnvoysAbroad.Of(Pawn(r), out _)),
                r => EnvoysAbroad.Of(Pawn(r), out _) != null, 44f, RansomWords.P("CC_Envoy_Act_Recall_Tip"), "envoy-recall");
            // Their envoy's own card.
            CardSections.RegisterLines(CardKind.Person, CardSlot.Now, 30f, PersonLines, null, "envoy-stay");
            CardActs.Register(CardKind.Person, r => RansomWords.P("CC_Envoy_Act_Bed"), r => BedWhyNot(Pawn(r)), r => ChooseBed(Pawn(r)), r => Staying(Pawn(r)) != null, 40f,
                RansomWords.P("CC_Envoy_Act_Bed_Tip"), "envoy-bed");
            CardActs.Register(CardKind.Person, r => RansomWords.P("CC_Envoy_Act_SeeOff"), r => null, r => Envoys.SeeOff(Staying(Pawn(r))), r => Staying(Pawn(r)) != null, 60f,
                RansomWords.P("CC_Envoy_Act_SeeOff_Tip"), "envoy-seeoff");
            // The people card.
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 40f, r => PeopleLines(ThingRefs.Faction(r)), null, "ransom-envoy-people");
            CardActs.Register(CardKind.People, r => RansomWords.P("CC_Ransom_Act_Exchange"), r => ExchangeAct(null, ThingRefs.Faction(r)).why,
                r => ExchangeAct(null, ThingRefs.Faction(r)).run?.Invoke(), r => Ransoms.On && Ransoms.Pairs(ThingRefs.Faction(r), Home).Count > 0, 34f,
                RansomWords.P("CC_Ransom_Act_Exchange_Tip"), "ransom-exchange-people");
            CardActs.Register(CardKind.People, r => RansomWords.P("CC_Envoy_Act_Ask"), r => AskAct(ThingRefs.Faction(r)).why, r => AskAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Envoys.On, 36f, RansomWords.P("CC_Envoy_Act_Ask_Tip"), "envoy-ask");
            CardActs.Register(CardKind.People, r => RansomWords.P("CC_Envoy_Act_Send"), r => SendAct(ThingRefs.Faction(r), null).why, r => SendAct(ThingRefs.Faction(r), null).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Envoys.On, 38f, RansomWords.P("CC_Envoy_Act_Send_Tip"), "envoy-send");
            // A town of theirs: the colony's envoy there, and sending one.
            CardSections.TownLines(CardSlot.Now, 40f, TownLines, null, "envoy-town");
            CardActs.Register(CardKind.Place, r => RansomWords.P("CC_Envoy_Act_Send"), r => SendAct(CardSections.TownOf(r)?.Faction, CardSections.TownOf(r)).why,
                r => SendAct(CardSections.TownOf(r)?.Faction, CardSections.TownOf(r)).run?.Invoke(),
                r => CardSections.TownOf(r) is Settlement s && s.Faction != null && !Diplomacy.Hostile(s.Faction) && Envoys.On, 38f, RansomWords.P("CC_Envoy_Act_Send_Tip"), "envoy-send-town");
            // A grudge of refused offers on its tie's card.
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 6f, TieLines, null, "ransom-grudge");
            // The one inbox.
            Inbox.Register("ransom", InboxSection.Needs, 33f, NeedRows, NeedCount);
            Inbox.Register("envoy", InboxSection.Needs, 34f, EnvoyNeedRows, EnvoyNeedCount);
            Inbox.Register("ransom-under-way", InboxSection.UnderWay, 53f, UnderWayRows);
            Inbox.Register("envoy-under-way", InboxSection.UnderWay, 54f, EnvoyUnderWayRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static Pawn Pawn(ThingRef r) => ThingRefs.Pawn(r);

        private static string Name(Pawn p, string kept) => RansomWords.Name(p, kept);

        private static string Link(Pawn p, string kept) => p != null ? ThingRefs.Link(p.LabelShort, ThingRefs.Of(p)) : kept ?? "?";

        private static string LinkPeople(Faction f) => ThingRefs.Link(RansomWords.People(f), ThingRefs.Of(f));

        // ================= Where an offer stands, in words =================

        /// <summary>
        /// The colony knows of it: their word has come (or is on the road), the colony asked, or they asked before. An offer
        /// of theirs still to come unasked isn't shown: the colony learns of it when their messenger appears.
        /// </summary>
        public static bool Known(RansomOffer o) => o != null && (o.kind != RansomKind.Theirs || o.stage != RansomStage.Waiting || o.asked || o.refusals > 0);

        /// <summary>An offer in a sentence: who, for what, and where it stands (never a time left).</summary>
        public static string OfferLine(RansomOffer o)
        {
            string people = LinkPeople(o.people);
            switch (o.kind)
            {
                case RansomKind.Demand:
                    return RansomWords.P("CC_Ransom_Row_Demand_" + o.stage, Link(o.ours, o.oursName).Named("OURS"), people.Named("PEOPLE"), o.price.Named("SILVER"));
                case RansomKind.Exchange:
                    return RansomWords.P("CC_Ransom_Row_Exchange_" + o.stage, Link(o.ours, o.oursName).Named("OURS"), Link(o.captive, o.captiveName).Named("THEIRS"), people.Named("PEOPLE"));
                default:
                    return RansomWords.P("CC_Ransom_Row_Theirs_" + o.stage, Link(o.captive, o.captiveName).Named("CAPTIVE"), people.Named("PEOPLE"),
                        RansomWords.Price(o.price, o.goods).Named("PRICE"));
            }
        }

        /// <summary>An envoy's stay in a sentence: coming, staying (how it goes), leaving.</summary>
        public static string StayLine(EnvoyStay s)
        {
            string envoy = Link(s.envoy, s.envoyName);
            if (s.stage == EnvoyStage.Coming) return RansomWords.P("CC_Envoy_Row_Coming", envoy.Named("ENVOY"), LinkPeople(s.people).Named("PEOPLE"));
            return RansomWords.P("CC_Envoy_Row_Staying", envoy.Named("ENVOY"), LinkPeople(s.people).Named("PEOPLE"), RansomWords.Mood(Envoys.Mood(s)).Named("MOOD"));
        }

        /// <summary>The colony's envoy away in a sentence.</summary>
        public static string AbroadLine(EnvoyAbroad a)
        {
            var town = EnvoysAbroad.TownOf(a);
            string townName = town != null ? ThingRefs.Link(town.Name, ThingRefs.Of(town)) : a.townName ?? "?";
            return RansomWords.P("CC_Envoy_Row_Abroad_" + a.stage, Link(a.colonist, a.name).Named("PAWN"), townName.Named("TOWN"));
        }

        // ================= Rows for the Peoples view and a town's ties =================

        /// <summary>What is under way with this people at a home (or any): offers for captives, their envoy, the colony's envoy.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            try
            {
                foreach (var (h, o) in Ransoms.All())
                    if (o.people == f && Known(o) && (home == null || h.Map == home))
                        list.Add(new DipRow { text = OfferLine(o), color = UiKit.Gold, link = (object)o.captive ?? o.ours, act = OfferRowAct(o) });
                foreach (var (h, s) in Envoys.All())
                    if (s.people == f && (home == null || h.Map == home))
                        list.Add(new DipRow { text = StayLine(s), note = s.since >= 0 ? RansomWords.P("CC_Envoy_SinceHere", FactionMemory.AgoText(s.since).Named("AGO")) : null, color = UiKit.Cool, link = s.envoy });
                foreach (var (h, a) in EnvoysAbroad.All())
                    if (a.people == f && (home == null || h.Map == home))
                        list.Add(new DipRow { text = AbroadLine(a), note = a.since >= 0 ? RansomWords.P("CC_Envoy_SinceAway", FactionMemory.AgoText(a.since).Named("AGO")) : null, color = UiKit.Warm, link = (object)EnvoysAbroad.TownOf(a) ?? a.colonist });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the people's rows", e);
            }
            return list;
        }

        private static DipAct OfferRowAct(RansomOffer o)
        {
            var letter = Waiting().FirstOrDefault(l => LetterOffer(l) == o.id);
            if (letter == null) return null;
            return new DipAct { label = InboxWords.Answer, tip = RansomWords.P("CC_Ransom_Act_Answer_Tip"), run = () => letter.OpenLetter() };
        }

        /// <summary>Here and away: who of the colony's is away as its envoy.</summary>
        public static IEnumerable<DipRow> AwayRows()
        {
            foreach (var (_, a) in EnvoysAbroad.All())
                if (a.stage != EnvoyStage.Coming)
                    yield return new DipRow { text = AbroadLine(a), note = a.since >= 0 ? RansomWords.P("CC_Envoy_SinceAway", FactionMemory.AgoText(a.since).Named("AGO")) : null, color = UiKit.Info, link = (object)EnvoysAbroad.TownOf(a) ?? a.colonist };
        }

        // ================= The cards =================

        private static List<string> ColonistLines(ThingRef r)
        {
            var p = Pawn(r);
            if (p == null) return null;
            var list = new List<string>();
            try
            {
                var o = Ransoms.For(p);
                if (o != null && Known(o)) list.Add(OfferLine(o));
                if (p.IsPrisonerOfColony && o == null && Ransoms.Grudge(p.Faction) != null)
                    list.Add(RansomWords.P("CC_Ransom_Card_Grudge", LinkPeople(p.Faction).Named("PEOPLE")));
                if (o == null && p.IsPrisonerOfColony && Diplomacy.Hostile(p.Faction) && Ransoms.Deals(p.Faction))
                    list.Add(RansomWords.P("CC_Ransom_Card_Trap", LinkPeople(p.Faction).Named("PEOPLE"), RansomWords.Odds(Ransoms.TrapChance(p.Faction, p.MapHeld ?? Home)).Named("ODDS")));
                var a = EnvoysAbroad.Of(p, out _);
                if (a != null) list.Add(AbroadLine(a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the colonist card", e);
            }
            return list;
        }

        private static EnvoyStay Staying(Pawn p)
        {
            var s = Envoys.Of(p);
            return s != null && s.envoy == p && s.stage == EnvoyStage.Staying ? s : null;
        }

        private static List<string> PersonLines(ThingRef r)
        {
            var p = Pawn(r);
            var s = Envoys.Of(p, out var h);
            if (s == null) return null;
            var list = new List<string>();
            try
            {
                if (p == s.attendant)
                {
                    list.Add(RansomWords.P("CC_Envoy_Card_Attendant", Link(s.envoy, s.envoyName).Named("ENVOY")));
                    return list;
                }
                list.Add(StayLine(s));
                if (s.stage == EnvoyStage.Staying)
                {
                    list.Add(RansomWords.P("CC_Envoy_SinceHere", FactionMemory.AgoText(s.since).Named("AGO")));
                    list.Add(s.bed != null && !s.bed.Destroyed ? RansomWords.P("CC_Envoy_Card_Bed", s.bed.LabelCap.Named("BED")) : RansomWords.P("CC_Envoy_Card_NoBed"));
                    if (s.nightDay >= 0) list.Add(RansomWords.P("CC_Envoy_Card_Night_" + s.night));
                    if (s.letters > 0) list.Add(RansomWords.P("CC_Envoy_Card_Letters", s.letters.Named("COUNT")));
                    if (Envoys.Empire(s)) list.Add(RansomWords.P("CC_Envoy_Card_Empire"));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: the person card", e);
            }
            return list;
        }

        private static List<string> PeopleLines(Faction f)
        {
            if (f == null) return null;
            var list = Rows(f, null).Select(x => x.text).ToList();
            var g = Ransoms.Grudge(f);
            if (g != null) list.Add(RansomWords.P("CC_Ransom_Card_Grudge", LinkPeople(f).Named("PEOPLE")));
            return list;
        }

        private static List<string> TownLines(Settlement s)
        {
            var list = new List<string>();
            foreach (var (_, a) in EnvoysAbroad.All())
                if (a.town == s.ID && a.stage == EnvoyStage.There)
                    list.Add(AbroadLine(a));
            return list;
        }

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.IsView || t.kind != Tie.Kind.Grudge || t.origin != RansomMath.Origin) return null;
            var f = Refs.Faction(t.from);
            return new List<string>
            {
                RansomWords.P("CC_Ransom_Card_GrudgeTie", LinkPeople(f).Named("PEOPLE"), t.terms.count.Named("COUNT"),
                    (t.terms.about.IsNone ? "?" : DipWords.NameOf(t.terms.about)).Named("CAPTIVE")),
                DipWords.Since(t.since)
            };
        }

        // ================= The acts =================

        /// <summary>The letter waiting about this captive (either way), or null.</summary>
        private static Letter AnswerLetter(Pawn p)
        {
            var o = Ransoms.For(p);
            return o == null ? null : Waiting().FirstOrDefault(l => LetterOffer(l) == o.id);
        }

        /// <summary>Offering one of theirs for ransom: a letter or a call to their head (their offer comes sooner).</summary>
        public static DipAct OfferAct(Pawn captive)
        {
            var a = new DipAct { label = RansomWords.P("CC_Ransom_Act_Offer"), tip = RansomWords.P("CC_Ransom_Act_Offer_Tip") };
            var f = captive?.Faction;
            var home = captive?.MapHeld ?? Home;
            var o = Ransoms.For(captive);
            if (!Ransoms.On) a.why = RansomWords.P("CC_Ransom_Why_Off");
            else if (captive == null || !captive.IsPrisonerOfColony) a.why = RansomWords.P("CC_Ransom_Why_Gone");
            else if (!Ransoms.Deals(f)) a.why = RansomWords.P("CC_Ransom_Why_Never", RansomWords.People(f).Named("PEOPLE"));
            else if (o != null && o.stage != RansomStage.Waiting) a.why = RansomWords.P("CC_Ransom_Why_UnderWay");
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", RansomWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Ransom);
            if (a.why == null)
            {
                var n = head;
                string key = RansomMath.RansomTopic(captive.thingIDNumber);
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Ransom, key));
            }
            return a;
        }

        private static bool ExchangeShows(Pawn p)
        {
            if (p == null) return false;
            var home = Home;
            if (p.IsPrisonerOfColony) return Ransoms.Pairs(p.Faction, p.MapHeld ?? home).Any(x => x.theirs == p);
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
                if (f?.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p))
                    return Ransoms.Pairs(f, home).Any(x => x.ours == p);
            return false;
        }

        /// <summary>
        /// Offering an exchange: the pairs that could be made (with this captive or this colonist, or any with that people),
        /// a pick opening the letter to their head with it chosen.
        /// </summary>
        public static DipAct ExchangeAct(Pawn p, Faction people)
        {
            var a = new DipAct { label = RansomWords.P("CC_Ransom_Act_Exchange"), tip = RansomWords.P("CC_Ransom_Act_Exchange_Tip") };
            var home = Home;
            var f = people ?? (p != null && p.IsPrisonerOfColony ? p.Faction : Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x?.kidnapped != null && p != null
                && x.kidnapped.KidnappedPawnsListForReading.Contains(p)));
            if (p != null && p.IsPrisonerOfColony) home = p.MapHeld ?? home;
            var pairs = Ransoms.Pairs(f, home).Where(x => p == null || x.ours == p || x.theirs == p).ToList();
            var open = pairs.Where(x => Ransoms.ExchangeWhyNot(x.ours, x.theirs, home) == null).ToList();
            if (pairs.Count == 0) a.why = RansomWords.P("CC_Ransom_Why_Nothing", RansomWords.People(f).Named("PEOPLE"));
            else if (open.Count == 0) a.why = Ransoms.ExchangeWhyNot(pairs[0].ours, pairs[0].theirs, home);
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", RansomWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Ransom);
            if (a.why != null) return a;
            var n = head;
            a.run = () =>
            {
                if (open.Count == 1)
                {
                    Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Ransom, RansomMath.ExchangeTopic(open[0].ours.thingIDNumber, open[0].theirs.thingIDNumber)));
                    return;
                }
                Find.WindowStack.Add(new FloatMenu(open.Select(x => new FloatMenuOption(RansomWords.P("CC_Ransom_TopicExchange", x.ours.Named("OURS"), x.theirs.Named("THEIRS")),
                    () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Ransom, RansomMath.ExchangeTopic(x.ours.thingIDNumber, x.theirs.thingIDNumber))))).ToList()));
            };
            return a;
        }

        /// <summary>Asking them for their envoy: a letter or a call to their head.</summary>
        public static DipAct AskAct(Faction f)
        {
            var home = Home;
            var a = new DipAct { label = RansomWords.P("CC_Envoy_Act_Ask"), tip = RansomWords.P("CC_Envoy_Act_Ask_Tip"), why = Envoys.AskWhyNot(f, home) };
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", RansomWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Envoy);
            if (a.why == null)
            {
                var n = head;
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Envoy, RansomLetters.EnvoyAsk));
            }
            return a;
        }

        /// <summary>Sending the colony's own envoy: who goes, then the council's question.</summary>
        public static DipAct SendAct(Faction f, Settlement town)
        {
            var home = Home;
            var a = new DipAct { label = RansomWords.P("CC_Envoy_Act_Send"), tip = RansomWords.P("CC_Envoy_Act_Send_Tip"), why = EnvoysAbroad.WhyNot(f, home, town) };
            if (a.why == null) a.run = () => EnvoysAbroad.Choose(f, home, town);
            return a;
        }

        private static string RecallWhyNot(Pawn p)
        {
            var a = EnvoysAbroad.Of(p, out _);
            return a != null && a.stage == EnvoyStage.There ? null : RansomWords.P("CC_Envoy_Why_NotThere");
        }

        private static string BedWhyNot(Pawn p)
        {
            var s = Staying(p);
            Envoys.Of(p, out var h);
            return Envoys.BedWhyNot(s, h?.Map);
        }

        private static void ChooseBed(Pawn p)
        {
            var s = Staying(p);
            Envoys.Of(p, out var h);
            if (s != null && h?.Map != null) Envoys.ChooseBed(s, h.Map);
        }

        // ================= The inbox =================

        private static int LetterOffer(Letter l) => l is ChoiceLetter_RansomOffer r ? r.offer : l is ChoiceLetter_RansomByHand b ? b.offer : -1;

        /// <summary>Their letters about captives waiting for the colony's answer.</summary>
        private static IEnumerable<Letter> Waiting()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_RansomOffer r && r.Waiting || l is ChoiceLetter_RansomByHand b && b.Waiting)
                    yield return l;
        }

        private static int NeedCount()
        {
            if (Current.Game == null) return 0;
            int n = 0;
            foreach (var _ in Waiting()) n++;
            return n;
        }

        /// <summary>Needs you: an offer for a captive (theirs for one in the colony's cells, or their demand for one of its own).</summary>
        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var l in Waiting())
                {
                    var letter = l;
                    var o = Ransoms.Get(LetterOffer(l), out var h);
                    if (o == null) continue;
                    Action read = () => letter.OpenLetter();
                    var who = o.kind == RansomKind.Demand ? o.ours : o.captive;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = o.kind == RansomKind.Demand ? UiKit.Soon : UiKit.Gold,
                        faction = o.people,
                        text = WeekModel.AtHome(h.Map, RansomWords.P(o.kind == RansomKind.Demand ? "CC_Ransom_Week_Demand" : "CC_Ransom_Week_Offer",
                            Link(who, o.kind == RansomKind.Demand ? o.oursName : o.captiveName).Named("NAME"), LinkPeople(o.people).Named("PEOPLE"))),
                        sub = RansomWords.P("CC_Ransom_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = InboxWords.Answer,
                        actRun = read,
                        follow = who != null ? ThingRefs.Of(who) : ThingRefs.Of(o.people),
                        order = l.arrivalTick
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: this week's offers", e);
            }
        }

        private static IEnumerable<Letter> EnvoyLetters()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_EnvoyOffer e && e.Waiting)
                    yield return l;
        }

        /// <summary>A stay whose envoy has no bed set aside (the envoy's ask).</summary>
        private static IEnumerable<(DiplomacyHome h, EnvoyStay s)> BedAsks() =>
            Envoys.All().Where(x => x.stay.stage == EnvoyStage.Staying && (x.stay.bed == null || x.stay.bed.Destroyed) && x.stay.nightDay >= 0 && x.stay.night != EnvoyNight.Own);

        private static int EnvoyNeedCount()
        {
            if (Current.Game == null) return 0;
            return EnvoyLetters().Count() + BedAsks().Count();
        }

        /// <summary>Needs you: their offer of an envoy, and their envoy's ask for a bed of their own.</summary>
        private static void EnvoyNeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var l in EnvoyLetters())
                {
                    var letter = l;
                    Action read = () => letter.OpenLetter();
                    var f = l.relatedFaction;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Gold,
                        faction = f,
                        text = RansomWords.P("CC_Envoy_Week_Offer", LinkPeople(f).Named("PEOPLE")),
                        sub = RansomWords.P("CC_Ransom_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = InboxWords.Answer,
                        actRun = read,
                        follow = ThingRefs.Of(f),
                        order = l.arrivalTick
                    });
                }
                foreach (var (h, s) in BedAsks())
                {
                    var stay = s;
                    var map = h.Map;
                    string why = Envoys.BedWhyNot(stay, map);
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Warm,
                        faction = s.people,
                        text = WeekModel.AtHome(map, RansomWords.P("CC_Envoy_Week_Bed", Link(s.envoy, s.envoyName).Named("ENVOY"))),
                        sub = RansomWords.P("CC_Envoy_Card_Night_" + s.night),
                        click = () => DiplomacyLinks.Open(stay.envoy),
                        act = RansomWords.P("CC_Envoy_Act_BedShort"),
                        actWhy = why,
                        actTip = RansomWords.P("CC_Envoy_Act_Bed_Tip"),
                        actRun = () => Envoys.ChooseBed(stay, map),
                        follow = ThingRefs.Of(s.envoy),
                        order = s.since
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: this week's asks", e);
            }
        }

        private static string[] offerSteps, exchangeSteps, demandSteps, staySteps, abroadSteps;

        private static string[] Steps(params string[] keys) => keys.Select(k => RansomWords.P(k)).ToArray();

        /// <summary>Under way: an offer coming to the gate, an exchange set, the colony's own coming home; in steps.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, o) in Ransoms.All())
                {
                    if (o.stage == RansomStage.AtGate) continue;
                    if (!Known(o)) continue;
                    string[] steps;
                    int step;
                    switch (o.kind)
                    {
                        case RansomKind.Exchange:
                            steps = exchangeSteps ??= Steps("CC_Ransom_Step_Agreed", "CC_Ransom_Step_Road", "CC_Ransom_Step_Exchanged");
                            step = o.stage == RansomStage.Waiting ? 0 : o.stage == RansomStage.Coming ? 1 : 2;
                            break;
                        case RansomKind.Demand:
                            steps = demandSteps ??= Steps("CC_Ransom_Step_Messenger", "CC_Ransom_Step_Paid", "CC_Ransom_Step_Home");
                            step = o.stage == RansomStage.Coming ? 0 : o.stage == RansomStage.Paying ? 1 : 2;
                            break;
                        default:
                            steps = offerSteps ??= Steps("CC_Ransom_Step_Asked", "CC_Ransom_Step_Road", "CC_Ransom_Step_Paid");
                            step = o.stage == RansomStage.Waiting ? 0 : o.stage == RansomStage.Coming ? 1 : 2;
                            break;
                    }
                    var who = (object)o.captive ?? o.ours;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = UiKit.Gold,
                        faction = o.people,
                        text = WeekModel.AtHome(h.Map, OfferLine(o)),
                        steps = steps,
                        step = step,
                        click = () => DiplomacyLinks.Open(who),
                        follow = o.captive != null ? ThingRefs.Of(o.captive) : ThingRefs.Of(o.ours),
                        order = o.created
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: this week's under way", e);
            }
        }

        /// <summary>Under way: their envoy's stay, the colony's envoy away; in steps.</summary>
        private static void EnvoyUnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, s) in Envoys.All())
                {
                    var stay = s;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Cool,
                        faction = s.people,
                        text = WeekModel.AtHome(h.Map, StayLine(s)),
                        steps = staySteps ??= Steps("CC_Envoy_Step_Agreed", "CC_Envoy_Step_Staying", "CC_Envoy_Step_Home"),
                        step = s.stage == EnvoyStage.Coming ? 0 : s.stage == EnvoyStage.Staying ? 1 : 2,
                        click = () => DiplomacyLinks.Open((object)stay.envoy ?? stay.people),
                        follow = s.envoy != null ? ThingRefs.Of(s.envoy) : ThingRefs.Of(s.people),
                        order = s.agreed
                    });
                }
                foreach (var (h, a) in EnvoysAbroad.All())
                {
                    var away = a;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Away ?? Icons.Visitor,
                        color = UiKit.Warm,
                        faction = a.people,
                        text = WeekModel.AtHome(h.Map, AbroadLine(a)),
                        steps = abroadSteps ??= Steps("CC_Envoy_Step_SettingOut", "CC_Envoy_Step_Road", "CC_Envoy_Step_There", "CC_Envoy_Step_HomeRoad"),
                        step = (int)a.stage,
                        click = () => DiplomacyLinks.Open((object)EnvoysAbroad.TownOf(away) ?? away.colonist),
                        follow = a.colonist != null ? ThingRefs.Of(a.colonist) : ThingRefs.Of(a.people),
                        order = a.agreed
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: this week's under way", e);
            }
        }
    }
}
