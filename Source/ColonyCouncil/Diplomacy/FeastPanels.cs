using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where feasts with guests show (docs/design/interface.md, "Where stage 2 and stage 3 go"): an Event card for each
    /// feast (its history entry: where it stands, the guests and their answers, the day, the contests and how each went,
    /// calling it off before the guests come), the colony's calendar (the feast day, or planning one), the people card's
    /// and a notable's card's acts ("Hold a feast for them", "Invite to a feast"), their rows on the Peoples view, the
    /// guests in Here and away, and the one inbox: a feast day in Coming, the invitations and the guests' stay in Under way,
    /// in steps (never a time left).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FeastPanels
    {
        static FeastPanels()
        {
            CardSections.RegisterLines(CardKind.Event, CardSlot.Now, 5f, EventNow, null, "feast-now");
            CardSections.RegisterLines(CardKind.Event, CardSlot.Story, 20f, EventResults, "CC_Feast_Card_Results", "feast-results");
            CardActs.Register(CardKind.Event, r => FeastWords.P("CC_Feast_Act_CallOff"), r => Feasts.CallOffWhyNot(PlanOf(r)), r => Feasts.CallOffByColony(PlanOf(r)),
                r => PlanOf(r) is GuestFeast p && p.Live && p.purpose == FeastPurpose.Feast, 30f, FeastWords.P("CC_Feast_Act_CallOff_Tip"), "feast-calloff");
            CardSections.RegisterLines(CardKind.Person, CardSlot.Now, 32f, PersonLines, null, "feast-guest");
            CardActs.Register(CardKind.Person, r => FeastWords.P("CC_Feast_Act_Invite"), r => InviteWhyNot(ThingRefs.Notable(r)), r => Open(null, ThingRefs.Notable(r)),
                r => ThingRefs.Notable(r) is Notable n && n.faction != null && !Diplomacy.Hostile(n.faction) && Feasts.On, 46f, FeastWords.P("CC_Feast_Act_Invite_Tip"), "feast-invite");
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 44f, r => Rows(ThingRefs.Faction(r), null).Select(x => x.text).ToList(), null, "feast-people");
            CardActs.Register(CardKind.People, r => FeastWords.P("CC_Feast_Act_Hold"), r => HoldWhyNot(ThingRefs.Faction(r)), r => Open(ThingRefs.Faction(r), null),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Feasts.On, 40f, FeastWords.P("CC_Feast_Act_Hold_Tip"), "feast-hold");
            Inbox.Register("feast-coming", InboxSection.Coming, 12f, ComingRows);
            Inbox.Register("feast-under-way", InboxSection.UnderWay, 55f, UnderWayRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static string LinkPeople(Faction f) => ThingRefs.Link(FeastWords.People(f), ThingRefs.Of(f));

        private static string LinkGuest(FeastGuest g) => g.pawn != null && !g.pawn.Discarded ? ThingRefs.Link(g.pawn.LabelShort, ThingRefs.Of(g.pawn)) : g.name ?? "?";

        private static GuestFeast PlanOf(ThingRef r)
        {
            var e = ThingRefs.Entry(r);
            if (e == null || e.kind != DiplomacyMath.Feast) return null;
            return Feasts.OfEntry(e.id, out _);
        }

        private static void Open(Faction f, Notable n)
        {
            var home = Home;
            if (home != null) Find.WindowStack.Add(new Dialog_Feast(home, f, n));
        }

        private static string HoldWhyNot(Faction f)
        {
            var home = Home;
            string why = Feasts.PlanWhyNot(home);
            if (why != null) return why;
            var first = Feasts.Candidates(home, f).FirstOrDefault();
            return first == null ? FeastWords.P("CC_Feast_Why_NoneOfTheirs", FeastWords.People(f).Named("PEOPLE")) : Feasts.InviteWhyNot(first, home);
        }

        private static string InviteWhyNot(Notable n) => Feasts.PlanWhyNot(Home) ?? Feasts.InviteWhyNot(n, Home);

        // ================= In words =================

        /// <summary>A guest's answer in a word: awaited, coming, not coming, here, gone home.</summary>
        public static string GuestWord(GuestFeast p, FeastGuest g)
        {
            // An answer on its way back isn't known yet.
            if (g.state != GuestState.Invited && p.stage == FeastStage.Inviting && LetterOpen(g)) return FeastWords.P("CC_Feast_Guest_Invited");
            return FeastWords.P("CC_Feast_Guest_" + g.state);
        }

        private static bool LetterOpen(FeastGuest g) => g.letter >= 0 && (NotableBook.Instance?.letters.Any(l => l != null && l.id == g.letter && l.Open) ?? false);

        /// <summary>Where the feast stands, in a sentence (never a time left).</summary>
        public static string StageLine(GuestFeast p, Map map)
        {
            string feast = FeastWords.Name(map);
            switch (p.stage)
            {
                case FeastStage.Inviting:
                    return FeastWords.P("CC_Feast_Stage_Inviting", feast.Named("FEAST"));
                case FeastStage.Set:
                    return FeastWords.P("CC_Feast_Stage_Set", feast.Named("FEAST"), Feasts.DateText(map, p.day).Named("DATE"));
                case FeastStage.Here:
                    return FeastWords.P("CC_Feast_Stage_Here", feast.Named("FEAST"));
                case FeastStage.Contests:
                    return FeastWords.P("CC_Feast_Stage_Contests", feast.Named("FEAST"),
                        (p.contest < p.contests.Count ? FeastWords.Contest(p.contests[p.contest]) : FeastWords.P("CC_Feast_NoContests")).Named("CONTEST"));
                case FeastStage.Feasting:
                    return FeastWords.P("CC_Feast_Stage_Feasting", feast.Named("FEAST"));
                case FeastStage.Night:
                    return FeastWords.P("CC_Feast_Stage_Night", feast.Named("FEAST"));
                default:
                    return p.calledOff ? FeastWords.P("CC_Feast_Stage_Off", feast.Named("FEAST"), (p.offWhy ?? "").Named("WHY"))
                        : FeastWords.P("CC_Feast_Stage_Done", feast.Named("FEAST"));
            }
        }

        /// <summary>The guests with their answers: "Ula of the Ashfall Clans, coming".</summary>
        private static IEnumerable<string> GuestLines(GuestFeast p)
        {
            foreach (var g in p.guests)
                yield return FeastWords.P("CC_Feast_Card_Guest", LinkGuest(g).Named("NAME"), LinkPeople(g.people).Named("PEOPLE"), GuestWord(p, g).Named("ANSWER"),
                    FeastWords.Hope(g.hope).Named("HOPE"));
        }

        // ================= The cards =================

        private static List<string> EventNow(ThingRef r)
        {
            var p = PlanOf(r);
            if (p == null) return null;
            Feasts.Get(p.id, out var h);
            var map = h?.Map;
            var list = new List<string>();
            try
            {
                list.Add(StageLine(p, map));
                list.AddRange(GuestLines(p));
                if (p.contests.Count > 0) list.Add(FeastWords.P("CC_Feast_Card_Contests", FeastWords.Contests(p.contests).Named("CONTESTS")));
                if (!p.prizeName.NullOrEmpty())
                    list.Add(FeastWords.P(p.prizeGiven ? "CC_Feast_Card_PrizeGiven" : "CC_Feast_Card_Prize", p.prizeName.Named("PRIZE"), (p.championName ?? "?").Named("CHAMPION")));
                if (!p.writerName.NullOrEmpty() && p.purpose == FeastPurpose.Feast) list.Add(FeastWords.P("CC_Feast_Card_Writer", p.writerName.Named("PAWN")));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the event card", e);
            }
            return list;
        }

        private static List<string> EventResults(ThingRef r)
        {
            var p = PlanOf(r);
            if (p == null || p.results.Count == 0) return null;
            var list = p.results.Select(FeastWords.Result).ToList();
            if (!p.championName.NullOrEmpty()) list.Add(FeastWords.P("CC_Feast_Card_Champion", p.championName.Named("CHAMPION")));
            foreach (var g in p.guests.Where(g => g.state == GuestState.Gone && p.ended >= 0))
                list.Add(FeastWords.P(g.welcome >= FeastMath.RememberedAt ? "CC_Feast_Card_Welcome_Glad" : g.welcome >= 0 ? "CC_Feast_Card_Welcome_Fair" : "CC_Feast_Card_Welcome_Poor",
                    LinkGuest(g).Named("NAME")));
            return list;
        }

        private static List<string> PersonLines(ThingRef r)
        {
            var n = ThingRefs.Notable(r);
            var x = ThingRefs.Pawn(r) ?? n?.pawn;
            if (x == null) return null;
            foreach (var (h, p) in Feasts.All())
            {
                var g = p.GuestOf(x) ?? (n != null ? p.guests.FirstOrDefault(o => o.notable == n.id) : null);
                if (g == null) continue;
                if (g.pawn != x) return new List<string> { FeastWords.P("CC_Feast_Card_Retinue", LinkGuest(g).Named("NAME"), FeastWords.Name(h.Map).Named("FEAST")) };
                return new List<string> { FeastWords.P("CC_Feast_Card_GuestOf", FeastWords.Name(h.Map).Named("FEAST"), GuestWord(p, g).Named("ANSWER")) };
            }
            return null;
        }

        // ================= Rows for the Peoples view =================

        /// <summary>A feast with this people's notables invited or here, at a home (or any), and its act.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            try
            {
                foreach (var (h, p) in Feasts.All())
                {
                    if (home != null && h.Map != home) continue;
                    var theirs = p.guests.Where(g => g.people == f).ToList();
                    if (theirs.Count == 0) continue;
                    string guests = TextUtil.JoinAnd(theirs.Select(g => LinkGuest(g) + " (" + GuestWord(p, g) + ")").ToList());
                    var entry = p.entry > 0 ? History.Get(p.entry) : null;
                    list.Add(new DipRow
                    {
                        text = FeastWords.P("CC_Feast_Row", FeastWords.Name(h.Map).Named("FEAST"), guests.Named("GUESTS")), note = StageLine(p, h.Map), color = UiKit.Gold,
                        link = entry != null ? (object)ThingRefs.Of(entry) : f
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the people's rows", e);
            }
            return list;
        }

        /// <summary>The people card's act: hold a feast with their notables as guests.</summary>
        public static DipAct HoldAct(Faction f)
        {
            var a = new DipAct { label = FeastWords.P("CC_Feast_Act_Hold"), tip = FeastWords.P("CC_Feast_Act_Hold_Tip"), why = HoldWhyNot(f) };
            if (a.why == null)
            {
                var people = f;
                a.run = () => Open(people, null);
            }
            return a;
        }

        // ================= The calendar =================

        /// <summary>
        /// The calendar page's row for a feast with guests (above the colony's days, beside election day): the feast under way
        /// with its day, else "A feast with guests" with the act to plan one, greyed with why not. Returns the height used.
        /// </summary>
        public static float CalendarRow(float x, float y, float w, ref int index, Map map)
        {
            if (!Feasts.On || map == null || !Homes.IsHome(map)) return 0f;
            var p = Feasts.At(map);
            const float dateW = 130f, h = 48f;
            var r = new Rect(x, y, w, h);
            UiKit.Zebra(r, index++);
            UiKit.RowHover(r);
            UiKit.Icon(new Rect(x + 6f, y + 6f, 20f, 20f), Icons.Visitor, UiKit.Gold);
            float nx = x + 32f + dateW + 8f;
            if (p != null)
            {
                string date = p.day >= 0 ? Feasts.DateText(map, p.day) : FeastWords.P("CC_Feast_Cal_NoDate");
                UiKit.LabelFit(new Rect(x + 32f, y + 3f, dateW, 22f), date, UiKit.Gold);
                UiKit.LabelFit(new Rect(nx, y + 3f, w - nx - 8f, 22f), FeastWords.P("CC_Feast_Cal_Name", FeastWords.Name(map).Named("FEAST"),
                    FeastWords.Peoples(p, p.stage != FeastStage.Inviting).Named("PEOPLES")).CapitalizeFirst(), UiKit.Ink);
                UiKit.LabelFit(new Rect(nx, y + 25f, w - nx - 8f, 18f), StageLine(p, map), UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(r, string.Join("\n", GuestLines(p).Select(LinkTextMath.Plain)));
                if (Widgets.ButtonInvisible(r))
                {
                    var entry = p.entry > 0 ? History.Get(p.entry) : null;
                    if (entry != null) Cards.Open(ThingRefs.Of(entry));
                }
                return h;
            }
            UiKit.LabelFit(new Rect(x + 32f, y + 3f, dateW, 22f), FeastWords.P("CC_Feast_Cal_Any"), UiKit.Faint);
            UiKit.LabelFit(new Rect(nx, y + 3f, w - nx - 160f, 22f), FeastWords.P("CC_Feast_Cal_Plan", FeastWords.Name(map).Named("FEAST")).CapitalizeFirst(), UiKit.Ink);
            UiKit.LabelFit(new Rect(nx, y + 25f, w - nx - 160f, 18f), FeastWords.P("CC_Feast_Cal_PlanNote"), UiKit.Faint, GameFont.Tiny);
            string why = Feasts.PlanWhyNot(map);
            if (UiKit.ActButton(new Rect(x + w - 150f, y + 10f, 140f, 28f), FeastWords.P("CC_Feast_Act_Plan"), why, FeastWords.P("CC_Feast_Act_Hold_Tip")))
                Find.WindowStack.Add(new Dialog_Feast(map));
            return h;
        }

        // ================= The inbox =================

        private static string[] steps;

        /// <summary>Coming: a feast day set, with its date (never a time left).</summary>
        private static void ComingRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, p) in Feasts.All())
                {
                    if (p.stage != FeastStage.Set) continue;
                    var map = h.Map;
                    var entry = p.entry > 0 ? History.Get(p.entry) : null;
                    int at = Feasts.DayStart(map, p.day) + 12 * GenDate.TicksPerHour;
                    var people = p.guests.FirstOrDefault(g => g.state == GuestState.Coming)?.people;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Gold,
                        faction = people,
                        text = WeekModel.AtHome(map, FeastWords.P(p.purpose == FeastPurpose.Wedding ? "CC_Feast_Week_Wedding" : "CC_Feast_Week_Day", FeastWords.Name(map).Named("FEAST"),
                            FeastWords.Guests(p, true).Named("GUESTS"))),
                        note = InboxWords.When(at),
                        click = () =>
                        {
                            if (entry != null) Cards.Open(ThingRefs.Of(entry));
                        },
                        follow = entry != null ? ThingRefs.Of(entry) : ThingRef.None,
                        order = at - Find.TickManager.TicksGame
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: this week's feast day", e);
            }
        }

        /// <summary>Under way: the invitations out, the answers in, the guests here, the feast, home again; in steps.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, p) in Feasts.All())
                {
                    if (p.stage == FeastStage.Set) continue;
                    var map = h.Map;
                    var entry = p.entry > 0 ? History.Get(p.entry) : null;
                    steps ??= new[] { FeastWords.P("CC_Feast_Step_Invited"), FeastWords.P("CC_Feast_Step_Here"), FeastWords.P("CC_Feast_Step_Feast"), FeastWords.P("CC_Feast_Step_Night") };
                    int step = p.stage == FeastStage.Inviting ? 0 : p.stage == FeastStage.Here || p.stage == FeastStage.Contests ? 1 : p.stage == FeastStage.Feasting ? 2 : 3;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Gold,
                        faction = p.guests.FirstOrDefault()?.people,
                        text = WeekModel.AtHome(map, StageLine(p, map)),
                        steps = steps,
                        step = step,
                        click = () =>
                        {
                            if (entry != null) Cards.Open(ThingRefs.Of(entry));
                        },
                        follow = entry != null ? ThingRefs.Of(entry) : ThingRef.None,
                        order = p.created
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: this week's under way", e);
            }
        }
    }
}
