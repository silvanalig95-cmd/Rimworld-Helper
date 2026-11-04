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
    /// This week's Under way (docs/design/interface.md, "This week: one inbox"): letters on the road, the journey
    /// planned, the colony's caravans out, missions taken on, settlers on the road to their hamlet, a raiders' camp's
    /// price going out, what an answer set going on the map, the council meeting in session, the camp on the colony's
    /// land and Hospitality's guests. Each shows where it is in steps (written, on the road, arrived, answered), never a
    /// time left: the player's rule. The steps' words are keyed (CC_Inbox_Step_*, and the letters' CC_Card_Step_*), and
    /// InboxTests checks none of them says how long is left.
    /// </summary>
    public static class InboxUnderWay
    {
        private static int Now => Find.TickManager.TicksGame;

        internal static void AddOwn()
        {
            Inbox.Register("letters", InboxSection.UnderWay, 10f, Letters);
            Inbox.Register("journey", InboxSection.UnderWay, 20f, Journey);
            Inbox.Register("caravans", InboxSection.UnderWay, 30f, Caravans);
            Inbox.Register("missions", InboxSection.UnderWay, 40f, Missions_);
            Inbox.Register("hamlet-road", InboxSection.UnderWay, 50f, Settlers);
            Inbox.Register("camp-price", InboxSection.UnderWay, 55f, Price);
            Inbox.Register("plays", InboxSection.UnderWay, 60f, Plays);
            Inbox.Register("meeting", InboxSection.UnderWay, 65f, Meeting);
            Inbox.Register("camp", InboxSection.UnderWay, 70f, Camps);
            Inbox.Register("guests", InboxSection.UnderWay, 80f, GuestsWeek.AddRows);
        }

        private static string[] Words(params string[] keys)
        {
            var w = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) w[i] = keys[i].Translate();
            return w;
        }

        private static string[] caravan, mission, settlers, journey, price, play;

        private static string[] CaravanSteps => caravan ??= Words("CC_Inbox_Step_SetOut", "CC_Inbox_Step_Road", "CC_Inbox_Step_There", "CC_Inbox_Step_HomeWay");
        private static string[] MissionSteps => mission ??= Words("CC_Inbox_Step_TakenOn", "CC_Inbox_Step_UnderWay", "CC_Inbox_Step_Done");
        private static string[] SettlerSteps => settlers ??= Words("CC_Inbox_Step_AtGate", "CC_Inbox_Step_Road", "CC_Inbox_Step_Settled");
        private static string[] JourneySteps => journey ??= Words("CC_Inbox_Step_Planned", "CC_Inbox_Step_Ready", "CC_Inbox_Step_AtGate", "CC_Inbox_Step_SetOut");
        private static string[] PriceSteps => price ??= Words("CC_Inbox_Step_Agreed", "CC_Inbox_Step_GoingOut", "CC_Inbox_Step_HandedOver");
        private static string[] PlaySteps => play ??= Words("CC_Inbox_Step_SetGoing", "CC_Inbox_Step_Happening", "CC_Inbox_Step_Done");

        // ---------------- Letters on the road ----------------

        private static void Letters(List<WeekRow> rows)
        {
            var book = NotableBook.Instance;
            if (!NotableBook.On || book == null) return;
            foreach (var l in book.letters)
            {
                if (l == null || !l.Open) continue;
                var n = book.Get(l.notable);
                string purpose = ("CC_Letter_Purpose_" + l.purpose).Translate();
                string name = l.toName ?? n?.Name ?? "?";
                var to = n != null ? ThingRefs.Of(n) : ThingRef.None;
                if (!to.IsNone) name = ThingRefs.Link(name, to);
                var letter = ThingRefs.Of(l);
                rows.Add(new WeekRow
                {
                    icon = l.channel == LetterChannel.Console ? Icons.Visitor : Icons.Journal,
                    pawn = n?.pawn,
                    color = UiKit.Info,
                    text = (l.channel == LetterChannel.Console ? "CC_Card_CallTo" : "CC_Card_LetterTo").Translate(purpose.Named("PURPOSE"), name.Named("NAME")).Resolve(),
                    steps = CardWords.LetterSteps(l),
                    step = CardWords.LetterStep(l),
                    tip = n != null ? NeighboursPanel.StatusWords(l, n) : null,
                    click = () => Cards.Open(letter),
                    follow = to,
                    order = l.created
                });
            }
        }

        // ---------------- The journey planned, and the caravans out ----------------

        private static void Journey(List<WeekRow> rows)
        {
            if (!JourneyBook.On) return;
            foreach (var plan in JourneyBook.Plans)
            {
                if (plan?.map == null) continue;
                string text = "CC_Inbox_Journey".Translate(JourneyRun.Who(plan).Named("WHO"), plan.Dest.Named("PLACE")).Resolve();
                // The travellers and the place they go to, as links (made once a second with the page).
                text = PaneLinks.Mark(text, plan.travellers);
                if (plan.HasDestination) text = PaneLinks.Mark(text, plan.Dest, ThingRefs.OfTile(plan.DestTile));
                rows.Add(new WeekRow
                {
                    icon = Icons.World,
                    color = UiKit.Gold,
                    text = WeekModel.AtHome(plan.map, text),
                    steps = JourneySteps,
                    step = InboxMath.JourneyStep((int)plan.stage),
                    tip = WeekModel.Tip(JourneyPanel.Status(plan), WeekModel.ClickPage("HereAndAway")),
                    click = WeekModel.Page("HereAndAway"),
                    order = plan.departAbs
                });
            }
        }

        private static void Caravans(List<WeekRow> rows)
        {
            var all = Find.WorldObjects?.Caravans;
            if (all == null) return;
            foreach (var c in all)
            {
                if (c == null || c.Destroyed || !c.IsPlayerControlled) continue;
                bool moving = c.pather != null && c.pather.Moving;
                Settlement to = moving ? Find.WorldObjects.SettlementAt(c.pather.Destination) : null;
                bool home = to != null && to.Faction == Faction.OfPlayer;
                bool atPlace = !moving && Find.WorldObjects.SettlementAt(c.Tile) != null;
                var self = ThingRefs.Of(c);
                string link = ThingRefs.Link(c.LabelCap, self);
                string text = to != null && !home
                    ? "CC_Inbox_CaravanTo".Translate(link.Named("CARAVAN"), ThingRefs.Link(to.LabelCap, ThingRefs.Of(to)).Named("PLACE")).Resolve()
                    : link;
                var caravanObj = c;
                int people = c.PawnsListForReading.Count(p => p.RaceProps.Humanlike);
                rows.Add(new WeekRow
                {
                    icon = Icons.Trader,
                    color = UiKit.Gold,
                    text = text,
                    steps = CaravanSteps,
                    step = InboxMath.CaravanStep(moving, home, atPlace),
                    tip = "CC_JourneysPg_People".Translate(people).Resolve(),
                    click = () =>
                    {
                        if (caravanObj != null && !caravanObj.Destroyed) Cards.ShowOnMap(caravanObj);
                    },
                    order = c.ID
                });
            }
        }

        // ---------------- Missions taken on ----------------

        private static void Missions_(List<WeekRow> rows)
        {
            foreach (var m in Missions.All)
            {
                if (m == null || m.state != MissionState.Active) continue;
                var mission = m;
                string goods = null;
                try
                {
                    goods = Missions.GoodsText(m);
                }
                catch
                {
                }
                var lines = new List<string>();
                if (!goods.NullOrEmpty()) lines.Add("CC_Missions_TipGoods".Translate(goods).Resolve());
                // The day it is due, as a date in the tooltip: never a countdown on the line.
                if (m.deadline > 0) lines.Add("CC_Inbox_DueBy".Translate(InboxWords.Date(m.deadline).Named("DATE")).Resolve());
                rows.Add(new WeekRow
                {
                    icon = Icons.Promise,
                    color = UiKit.Gold,
                    faction = m.faction,
                    text = m.title ?? m.def?.LabelCap.ToString() ?? "?",
                    steps = MissionSteps,
                    step = InboxMath.MissionStep(m.stage),
                    tip = WeekModel.Tip(string.Join("\n", lines), WeekModel.ClickPage("HereAndAway")),
                    click = Missions.QuestOf(m) != null ? () => Missions.ShowQuest(mission) : WeekModel.Page("HereAndAway"),
                    order = m.started
                });
            }
        }

        // ---------------- Settlers on the road to their hamlet ----------------

        private static void Settlers(List<WeekRow> rows)
        {
            if (RegionBook.Instance == null || !Hamlets.On) return;
            foreach (var (home, h) in Hamlets.All())
            {
                int at = InboxMath.SettlerStep(h.stage);
                // At the gate they wait on the colony: Needs you has them (World/Region/HamletPage.cs).
                if (at < 1) continue;
                string people = h.people != null ? ThingRefs.Link(h.people.Name, ThingRefs.Of(h.people)) : "?";
                var target = HamletPage.Target(h);
                rows.Add(new WeekRow
                {
                    icon = Icons.Colony,
                    color = UiKit.Good,
                    faction = h.people,
                    text = WeekModel.AtHome(home, "CC_Inbox_Settlers".Translate(people.Named("PEOPLE")).Resolve()),
                    steps = SettlerSteps,
                    step = at,
                    tip = string.Join("\n", HamletWords.Lines(home, h, false)),
                    click = target.IsValid ? WeekModel.Go(target) : WeekModel.Page("Map/Ours/List"),
                    order = h.askedAt
                });
            }
        }

        // ---------------- A raiders' camp's price going out ----------------

        private static void Price(List<WeekRow> rows)
        {
            if (RegionBook.Instance == null) return;
            foreach (var (home, c) in RaidersCamps.Known())
            {
                if (c.stage != CampStage.Paying) continue;
                var site = RaidersCamps.SiteOf(c);
                string camp = site != null ? ThingRefs.Link(c.label, ThingRefs.Of(site)) : c.label;
                rows.Add(new WeekRow
                {
                    icon = Icons.Raid,
                    color = UiKit.Warn,
                    text = WeekModel.AtHome(home, "CC_Inbox_CampPrice".Translate(camp.Named("NAME"), c.price.Named("SILVER")).Resolve()),
                    steps = PriceSteps,
                    step = 1,
                    tip = string.Join("\n", RaidersCampPage.Lines(home, c, false)),
                    click = site != null ? WeekModel.Go(new GlobalTargetInfo(site)) : WeekModel.Page("Map/Threats/List"),
                    order = c.setAt
                });
            }
        }

        // ---------------- What an answer set going on the map ----------------

        private static void Plays(List<WeekRow> rows)
        {
            var plays = PlayOutTracker.Instance?.plays;
            if (plays != null)
                foreach (var p in plays)
                {
                    if (p == null || p.state == PlayState.Done) continue;
                    // Who it's about, what it is and how far it has got (UI/PlayOutText.cs: gatherings, visits, work parties, walks, vigils, searches, errands, hunts).
                    var who = PlayOutText.Who(p);
                    string label = p.label.NullOrEmpty() ? ("CC_Week_Play_" + p.kind).Translate().ToString() : p.label.CapitalizeFirst();
                    bool running = p.state == PlayState.Running;
                    var target = who != null && who.Spawned ? new GlobalTargetInfo(who) : GlobalTargetInfo.Invalid;
                    string body = PlayOutText.Body(p);
                    if (!running && !p.waitReason.NullOrEmpty()) body = (body == null ? "" : body + "\n") + "CC_Week_PlayWaitingWhy".Translate(p.waitReason).Resolve();
                    rows.Add(new WeekRow
                    {
                        pawn = who,
                        icon = PlayOutText.Icon(p),
                        color = running ? UiKit.Good : Color.white,
                        text = PaneLinks.Mark(label, who),
                        steps = PlaySteps,
                        step = running ? 1 : 0,
                        tip = WeekModel.Tip(body, target.IsValid ? "CC_Week_ClickMap".Translate(who.LabelShort).Resolve() : null),
                        click = target.IsValid ? WeekModel.Go(target) : null,
                        order = running ? -1 : p.deadline
                    });
                }
            // What an answer keeps on the map for a while (PlayOut/OnMapTimed.cs): lanterns, a fire, the pyre, a bolted door, an animal at heel.
            var kept = OnMapTimed.Instance?.kept;
            if (kept != null)
                foreach (var k in kept)
                {
                    var row = OnMapTimed.WeekRowFor(k);
                    if (row != null) rows.Add(row);
                }
        }

        /// <summary>Each colony's council meeting while it sits.</summary>
        private static void Meeting(List<WeekRow> rows)
        {
            if (!ColonyCouncilMod.Settings.enableAmbitions) return;
            foreach (var council in ColonyState.Ticking())
            {
                var meeting = council.ambitions?.meeting;
                if (meeting?.map == null || !meeting.Started) continue;
                rows.Add(new WeekRow
                {
                    icon = Icons.Council,
                    color = UiKit.Info,
                    text = WeekModel.AtColony(council, "CC_Week_Meeting".Translate()),
                    note = "CC_Week_UnderWay".Translate(),
                    noteColor = UiKit.Good,
                    tip = WeekModel.Tip(AmbitionPanel.InSessionText(meeting), WeekModel.ClickPage("Ambition")),
                    click = WeekModel.CouncilPage(council, "Ambition"),
                    order = -1
                });
            }
        }

        // ---------------- The camp on the colony's land ----------------

        private static void Camps(List<WeekRow> rows)
        {
            foreach (var camp in CampTracker.Instance?.Visiting.ToList() ?? new List<Camp>())
            {
                if (!camp.Active || camp.map == null || !Find.Maps.Contains(camp.map)) continue;
                var target = camp.center.IsValid ? new GlobalTargetInfo(camp.center, camp.map) : GlobalTargetInfo.Invalid;
                rows.Add(new WeekRow
                {
                    icon = Icons.Visitor,
                    color = UiKit.StrangerColor,
                    text = WeekModel.AtHome(camp.map, "CC_Week_Camp".Translate(camp.kind?.LabelCap.ToString() ?? "?").Resolve()),
                    note = "CC_Week_Tension".Translate(camp.TensionWord).Resolve(),
                    noteColor = CampPanel.TensionColor(camp.tension),
                    tip = WeekModel.Tip(camp.StatusLine() + "\n" + camp.TermsLine(), target.IsValid ? "CC_Week_ClickPlace".Translate().ToString() : WeekModel.ClickPage("HereAndAway")),
                    click = target.IsValid ? WeekModel.Go(target) : WeekModel.Page("HereAndAway"),
                    order = -1
                });
                // What their people are coming into the colony for, or are there doing now (Camps/CampRequests.cs): the day they come, never how long until.
                string errand = camp.ErrandText();
                if (errand == null) continue;
                bool coming = camp.errandFrom > Now;
                var at = camp.errandCell.IsValid ? new GlobalTargetInfo(camp.errandCell, camp.map) : target;
                rows.Add(new WeekRow
                {
                    icon = Icons.Visitor,
                    color = UiKit.StrangerColor,
                    text = errand,
                    note = coming ? InboxWords.When(camp.errandFrom) : "CC_Week_UnderWay".Translate().ToString(),
                    tip = at.IsValid ? "CC_Week_ClickPlace".Translate().ToString() : WeekModel.ClickPage("HereAndAway"),
                    click = at.IsValid ? WeekModel.Go(at) : WeekModel.Page("HereAndAway"),
                    order = 0
                });
            }
        }
    }
}
