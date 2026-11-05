using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where marriage alliances show (docs/design/interface.md, "Where stage 2 and stage 3 go": Tie, House and Person cards;
    /// the council question and "Talk with..."; This week: the match proposed): the marriage's Tie card (who married whom,
    /// where they live, held through a child, what the alliance gives) and a grudge for a spouse wronged; a notable's Person
    /// card (a match under way, who of the colony's would fit, "Propose a match"); the People card (marriages and matches with them, "Seek a match"); a colonist's card (promised or married
    /// to one of theirs); the Peoples view's rows and act, Here and away (the colony's own married into their town, the peoples it
    /// is bound to by marriage), and the one inbox: their offer of a match in Needs you, the matches under way in steps, the day
    /// their kin come for the colony's own in Coming (never a time left).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MatchPanels
    {
        static MatchPanels()
        {
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 7f, TieLines, null, "match-tie");
            CardActs.Register(CardKind.Tie, r => MatchWords.P("CC_Match_Act_Write"), r => WriteWhyNot(Matches.PeopleOf(ThingRefs.Tie(r))), r => Write(Matches.PeopleOf(ThingRefs.Tie(r))),
                r => ThingRefs.Tie(r) is Tie t && t.kind == Tie.Kind.Marriage && t.origin == MatchMath.Origin && t.Live, 40f, MatchWords.P("CC_Match_Act_Write_Tip"), "match-write");
            CardSections.RegisterLines(CardKind.Person, CardSlot.Now, 34f, PersonLines, null, "match-person");
            CardActs.Register(CardKind.Person, r => MatchWords.P("CC_Match_Act_Propose"), r => ProposeWhyNot(ThingRefs.Notable(r)), r => Propose(ThingRefs.Notable(r)),
                r => ThingRefs.Notable(r) is Notable n && n.faction != null && !Diplomacy.Hostile(n.faction) && Matches.On, 48f, MatchWords.P("CC_Match_Act_Propose_Tip"), "match-propose");
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 46f, r => Rows(ThingRefs.Faction(r), null).Select(x => x.text).ToList(), null, "match-people");
            CardActs.Register(CardKind.People, r => MatchWords.P("CC_Match_Act_Seek"), r => SeekAct(ThingRefs.Faction(r)).why, r => SeekAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Matches.On, 42f, MatchWords.P("CC_Match_Act_Seek_Tip"), "match-seek");
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 36f, ColonistLines, null, "match-colonist");
            Inbox.Register("match", InboxSection.Needs, 35f, NeedRows, NeedCount);
            Inbox.Register("match-under-way", InboxSection.UnderWay, 56f, UnderWayRows);
            Inbox.Register("match-coming", InboxSection.Coming, 13f, ComingRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static string Link(Pawn p, string kept) => p != null && !p.Discarded ? ThingRefs.Link(p.LabelShort, ThingRefs.Of(p)) : kept ?? "?";

        private static string LinkPeople(Faction f) => ThingRefs.Link(MatchWords.People(f), ThingRefs.Of(f));

        private static string LinkRef(Ref r) => ThingRefs.Link(DipWords.NameOf(r), ThingRef.From(r));

        // ================= In words =================

        /// <summary>Where a match stands, in a sentence (never a time left).</summary>
        public static string StageLine(MatchPlan p, bool links = true)
        {
            string ours = links ? Link(p.ours, p.oursName) : MatchWords.Name(p.ours, p.oursName), theirs = links ? Link(p.theirs, p.theirsName) : MatchWords.Name(p.theirs, p.theirsName);
            string people = links ? LinkPeople(p.people) : MatchWords.People(p.people);
            switch (p.stage)
            {
                case MatchStage.Asking:
                    return MatchWords.P("CC_Match_Stage_Asking", ours.Named("OURS"), theirs.Named("THEIRS"), people.Named("PEOPLE"));
                case MatchStage.Agreed:
                    return MatchWords.P(p.lives == MatchHome.Colony ? "CC_Match_Stage_Agreed_Colony" : "CC_Match_Stage_Agreed_Theirs", ours.Named("OURS"), theirs.Named("THEIRS"),
                        people.Named("PEOPLE"));
                case MatchStage.Here:
                    return MatchWords.P(p.lives == MatchHome.Colony ? "CC_Match_Stage_Here_Colony" : "CC_Match_Stage_Here_Theirs", ours.Named("OURS"), theirs.Named("THEIRS"),
                        people.Named("PEOPLE"));
                default:
                    return MatchWords.P("CC_Match_Stage_Away", ours.Named("OURS"), theirs.Named("THEIRS"), (p.townName ?? "?").Named("TOWN"));
            }
        }

        /// <summary>A marriage in a sentence: "Ada and Bram of the Lenlia, married, living at the colony".</summary>
        public static string MarriageLine(Tie t, bool links = true)
        {
            var f = Matches.PeopleOf(t);
            string ours = links ? LinkRef(t.terms.about) : DipWords.NameOf(t.terms.about);
            string theirs = MatchMath.ParseSpouse(t.terms.key, out string r) ? links ? LinkRef(Ref.Parse(r)) : DipWords.NameOf(Ref.Parse(r)) : "?";
            return MatchWords.P(Matches.LivesOf(t) == MatchHome.Colony ? "CC_Match_Line_Colony" : "CC_Match_Line_Theirs", ours.Named("OURS"), theirs.Named("THEIRS"),
                (links ? LinkPeople(f) : MatchWords.People(f)).Named("PEOPLE"));
        }

        // ================= The cards =================

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.origin != MatchMath.Origin) return null;
            var list = new List<string>();
            try
            {
                var f = Matches.PeopleOf(t);
                if (t.kind == Tie.Kind.Grudge)
                {
                    var people = Refs.Faction(Ref.People(t.terms.count));
                    list.Add(MatchWords.P("CC_Match_Card_Grudge", LinkRef(t.from).Named("HOLDER"), LinkRef(t.terms.about).Named("NAME"), LinkPeople(people).Named("PEOPLE")));
                    return list;
                }
                if (t.kind != Tie.Kind.Marriage) return null;
                list.Add(MarriageLine(t));
                if (t.since >= 0) list.Add(MatchWords.P("CC_Match_Card_Since", FactionMemory.AgoText(t.since).Named("AGO")));
                if (t.terms.count > 0) list.Add(MatchWords.P("CC_Match_Card_Child"));
                if (t.Live)
                {
                    bool empire = Ages.IsEmpire(f);
                    list.Add(MatchWords.P(empire ? "CC_Match_Card_What_Empire" : "CC_Match_Card_What", LinkPeople(f).Named("PEOPLE"), MatchMath.RegardBonus.Named("REGARD")));
                    var pact = Pacts.Of(f, Matches.HomeOf(t), PactKind.Defence);
                    if (!empire) list.Add(MatchWords.P(pact != null ? "CC_Match_Card_Pact" : "CC_Match_Card_NoPact", LinkPeople(f).Named("PEOPLE")));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the tie's card", e);
            }
            return list;
        }

        private static List<string> PersonLines(ThingRef r)
        {
            var n = ThingRefs.Notable(r);
            var x = ThingRefs.Pawn(r) ?? n?.pawn;
            if (x == null) return null;
            var list = new List<string>();
            try
            {
                var p = Matches.PlanOf(x);
                if (p != null) list.Add(StageLine(p));
                var t = Matches.TieOf(x);
                if (t != null) list.Add(MarriageLine(t));
                if (p == null && t == null && n != null && Matches.On && !Diplomacy.Hostile(n.faction))
                {
                    var fits = Fits(n, Home);
                    if (fits.Count > 0)
                        list.Add(MatchWords.P("CC_Match_Card_Fits", TextUtil.JoinAnd(fits.Take(3).Select(c => Link(c, null)).ToList()).Named("NAMES")));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: a person's card", e);
            }
            return list;
        }

        /// <summary>The colony's own at the home who would fit this notable, or one of their house.</summary>
        private static List<Pawn> Fits(Notable n, Map home)
        {
            var list = new List<Pawn>();
            if (n?.pawn == null || home == null) return list;
            foreach (var c in Matches.Colonists(home))
                if (n.role == NotableRole.Head ? Matches.WhyNotOfTown(c, n) == null : Matches.WhyNotPair(c, n.pawn, n.faction) == null)
                    list.Add(c);
            return list;
        }

        private static List<string> ColonistLines(ThingRef r)
        {
            var x = ThingRefs.Pawn(r);
            if (x == null) return null;
            var list = new List<string>();
            var p = Matches.PlanOf(x);
            if (p != null) list.Add(StageLine(p));
            var t = Matches.TieOf(x);
            if (t != null) list.Add(MarriageLine(t));
            return list;
        }

        /// <summary>A house of a town: its members married into the colony, or promised to one of the colony's.</summary>
        private static List<string> HouseLines(ThingRef r)
        {
            var h = ThingRefs.House(r);
            if (h == null || h.name.NullOrEmpty()) return null;
            var list = new List<string>();
            try
            {
                bool Of(Pawn x) => x?.Name is NameTriple tri && tri.Last == h.name && (x.Faction == h.people || KinMemory.Ours(x));
                foreach (var t in Matches.Live())
                {
                    if (Matches.PeopleOf(t) != h.people) continue;
                    var theirs = Matches.TheirsOf(t);
                    if (Of(theirs)) list.Add(MarriageLine(t));
                }
                foreach (var (_, p) in Matches.All())
                    if (p.people == h.people && Of(p.theirs))
                        list.Add(StageLine(p));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: a house's card", e);
            }
            return list;
        }

        // ================= The acts =================

        private static string WriteWhyNot(Faction f)
        {
            var head = FavourCalls.HeadOf(f, Home);
            return head == null ? DipWords.P("CC_Dip_Why_NoNotable", MatchWords.People(f).Named("PEOPLE")) : NeighboursPanel.WhyNoLetter(head);
        }

        private static void Write(Faction f)
        {
            var head = FavourCalls.HeadOf(f, Home);
            if (head != null && NeighboursPanel.WhyNoLetter(head) == null) Find.WindowStack.Add(new Dialog_NeighbourLetter(head));
        }

        private static string ProposeWhyNot(Notable n)
        {
            if (n == null) return MatchWords.P("CC_Match_Why_Gone");
            if (!NeighbourLetters.On) return "CC_Letter_Why_Off".Translate().Resolve();
            return NeighbourLetters.WhyNot(n, LetterPurpose.Match);
        }

        private static void Propose(Notable n)
        {
            if (ProposeWhyNot(n) == null) Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Match, null));
        }

        /// <summary>The people card's act: a letter to their head proposing a match (who would fit is chosen there).</summary>
        public static DipAct SeekAct(Faction f)
        {
            var a = new DipAct { label = MatchWords.P("CC_Match_Act_Seek"), tip = MatchWords.P("CC_Match_Act_Seek_Tip") };
            var head = FavourCalls.HeadOf(f, Home);
            a.why = Matches.PeopleWhyNot(f, Home) ?? (head == null ? DipWords.P("CC_Dip_Why_NoNotable", MatchWords.People(f).Named("PEOPLE")) : ProposeWhyNot(head));
            if (a.why == null)
            {
                var n = head;
                a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Match, null));
            }
            return a;
        }

        // ================= Rows for the Peoples view and Here and away =================

        /// <summary>The marriages and matches with this people (at a home, or any), Diplomatic Marriage+'s alliance as the mod's own.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            try
            {
                foreach (var t in Matches.With(f))
                    if (home == null || Matches.HomeOf(t) == home)
                        list.Add(new DipRow
                        {
                            text = MarriageLine(t), note = t.since >= 0 ? DipWords.Since(t.since) : null, color = UiKit.Gold, link = t,
                            tip = MatchWords.P(Ages.IsEmpire(f) ? "CC_Match_Card_What_Empire" : "CC_Match_Card_What", MatchWords.People(f).Named("PEOPLE"),
                                MatchMath.RegardBonus.Named("REGARD"))
                        });
                foreach (var (h, p) in Matches.All())
                    if (p.people == f && (home == null || h.Map == home))
                        list.Add(new DipRow { text = StageLine(p), color = UiKit.Soon, link = (object)p.ours ?? f });
                foreach (var t in Matches.Views())
                    if (Matches.PeopleOf(t) == f)
                        list.Add(new DipRow { text = MatchWords.P("CC_Match_Line_Mod", LinkRef(t.terms.about).Named("OURS"), LinkPeople(f).Named("PEOPLE")), color = UiKit.Info, link = f });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the people's rows", e);
            }
            return list;
        }

        /// <summary>Here and away: the colony's own married into their town (and on the road there).</summary>
        public static IEnumerable<DipRow> AwayRows()
        {
            foreach (var t in Matches.Live())
                if (Matches.LivesOf(t) == MatchHome.Theirs)
                    yield return new DipRow { text = MarriageLine(t), note = t.since >= 0 ? DipWords.Since(t.since) : null, color = UiKit.Info, link = t };
            foreach (var (_, p) in Matches.All())
                if (p.stage == MatchStage.Away)
                    yield return new DipRow { text = StageLine(p), color = UiKit.Info, link = (object)p.ours ?? p.people };
        }

        /// <summary>Here and away's houses: the colony's in-laws, one row a marriage.</summary>
        public static IEnumerable<DipRow> InlawRows()
        {
            foreach (var t in Matches.Live())
            {
                var f = Matches.PeopleOf(t);
                var theirs = Matches.TheirsOf(t);
                string house = theirs?.Name is NameTriple tri ? tri.Last : null;
                yield return new DipRow
                {
                    text = MatchWords.P(house.NullOrEmpty() ? "CC_Match_Inlaws_People" : "CC_Match_Inlaws", (house ?? "").Named("HOUSE"), LinkPeople(f).Named("PEOPLE"),
                        LinkRef(t.terms.about).Named("OURS"), Link(theirs, null).Named("THEIRS")),
                    color = UiKit.Gold, link = t
                };
            }
        }

        // ================= The inbox =================

        private static IEnumerable<ChoiceLetter_MatchOffer> Waiting()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_MatchOffer m && m.Waiting)
                    yield return m;
        }

        private static int NeedCount() => Current.Game == null ? 0 : Waiting().Count();

        /// <summary>Needs you: their offer of a match, waiting for the colony's answer.</summary>
        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var l in Waiting())
                {
                    var letter = l;
                    Action read = () => letter.OpenLetter();
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Wedding ?? Icons.Visitor,
                        color = UiKit.Gold,
                        faction = l.people,
                        text = MatchWords.P("CC_Match_Week_Offer", LinkPeople(l.people).Named("PEOPLE"), Link(l.ours, null).Named("OURS")),
                        sub = MatchWords.P("CC_Match_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = InboxWords.Answer,
                        actRun = read,
                        follow = ThingRefs.Of(l.people),
                        order = l.arrivalTick
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: this week's offers", e);
            }
        }

        private static string[] stepsColony, stepsTheirs;

        /// <summary>Under way: each match in steps (the council's word, agreed, their kin here, the wedding; or on the road to their town).</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                stepsColony ??= new[] { MatchWords.P("CC_Match_Step_Agreed"), MatchWords.P("CC_Match_Step_Coming"), MatchWords.P("CC_Match_Step_Wedding") };
                stepsTheirs ??= new[] { MatchWords.P("CC_Match_Step_Agreed"), MatchWords.P("CC_Match_Step_Fetched"), MatchWords.P("CC_Match_Step_Road"), MatchWords.P("CC_Match_Step_Wedding") };
                foreach (var (h, p) in Matches.All())
                {
                    var map = h.Map;
                    bool colony = p.lives == MatchHome.Colony;
                    int step = p.stage == MatchStage.Asking ? -1 : p.stage == MatchStage.Agreed ? 0 : p.stage == MatchStage.Here ? 1 : 2;
                    var who = p.ours;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Wedding ?? Icons.Visitor,
                        color = UiKit.Gold,
                        faction = p.people,
                        text = WeekModel.AtHome(map, StageLine(p)),
                        steps = colony ? stepsColony : stepsTheirs,
                        step = step,
                        click = () =>
                        {
                            if (who != null) Cards.Open(ThingRefs.Of(who));
                        },
                        follow = who != null ? ThingRefs.Of(who) : ThingRef.None,
                        order = p.agreed >= 0 ? p.agreed : p.id
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: this week's under way", e);
            }
        }

        /// <summary>Coming: the day their kin come for the colony's own (the wedding's guests have the feast's own row), with its date.</summary>
        private static void ComingRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, p) in Matches.All())
                {
                    if (p.stage != MatchStage.Agreed || p.lives != MatchHome.Theirs || p.due < 0) continue;
                    var who = p.ours;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Wedding ?? Icons.Visitor,
                        color = UiKit.Gold,
                        faction = p.people,
                        text = WeekModel.AtHome(h.Map, MatchWords.P("CC_Match_Week_Kin", Link(p.ours, p.oursName).Named("OURS"), LinkPeople(p.people).Named("PEOPLE"))),
                        note = InboxWords.When(p.due),
                        click = () =>
                        {
                            if (who != null) Cards.Open(ThingRefs.Of(who));
                        },
                        follow = who != null ? ThingRefs.Of(who) : ThingRef.None,
                        order = p.due - Find.TickManager.TicksGame
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: this week's coming", e);
            }
        }
    }
}
