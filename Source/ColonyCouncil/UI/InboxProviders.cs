using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Words This week shares: a day as a date, as "today" or "tomorrow", and its acts' labels, each translated once.</summary>
    internal static class InboxWords
    {
        private static float Longitude()
        {
            try
            {
                var map = HappeningTracker.HomeMap() ?? Find.CurrentMap;
                return map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>The day a tick of the game falls on, as the calendar says it ("9 Jugust").</summary>
        public static string Date(int tick) => GenDate.DateShortStringAt(GenDate.TickGameToAbs(tick), new Vector2(Longitude(), 0f));

        private static int Day(int tick, float lng) => (int)((GenDate.TickGameToAbs(tick) + GenDate.LocalTicksOffsetFromLongitude(lng)) / GenDate.TicksPerDay);

        /// <summary>When something comes, by the day it falls on: today, tomorrow, or its date. Never how long until.</summary>
        public static string When(int tick)
        {
            float lng = Longitude();
            int now = Find.TickManager.TicksGame;
            switch (InboxMath.When(Day(now, lng), Day(tick, lng)))
            {
                case DueWord.Today: return Today;
                case DueWord.Tomorrow: return Tomorrow;
                default: return Date(tick);
            }
        }

        private static string today, tomorrow, answer, decide, look, pay, plan, putAway, send;
        public static string Today => today ??= "CC_Inbox_Today".Translate();
        public static string Tomorrow => tomorrow ??= "CC_Inbox_Tomorrow".Translate();
        public static string Answer => answer ??= "CC_Inbox_Act_Answer".Translate();
        public static string Decide => decide ??= "CC_Inbox_Act_Decide".Translate();
        public static string Look => look ??= "CC_Inbox_Act_Look".Translate();
        public static string Pay => pay ??= "CC_Inbox_Act_Pay".Translate();
        public static string Plan => plan ??= "CC_Inbox_Act_Plan".Translate();
        public static string PutAway => putAway ??= "CC_Inbox_Act_PutAway".Translate();
        public static string Send => send ??= "CC_Inbox_Act_Send".Translate();
    }

    /// <summary>
    /// This week's own providers (UI/Inbox.cs), what were its twelve cards, each in its section now: Following (what the
    /// player follows, and the wishes pinned), Needs you (wishes and petitions, letters waiting for an answer, the
    /// region's and diplomacy's asks, settlers at the gate, promises at risk; what they count is the one count), Coming
    /// (the calendar's days, the council's meetings, fights promised, stories, missions offered, a post's warning, a
    /// raiders' camp, each by date), How people are (strain, illness, trust, the mood balance at its foot) and the plan
    /// (the advisor's steps). What is under way is UI/InboxUnderWay.cs.
    /// </summary>
    public static class InboxProviders
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;
        private static CouncilGameComponent Comp => CouncilGameComponent.Instance;

        internal static void AddOwn()
        {
            // Following: what the player follows, each with its newest change (UI/FollowWords.cs), then the wishes pinned.
            Inbox.Register("following", InboxSection.Following, 10f, FollowWords.Rows);
            Inbox.Register("pinned-wishes", InboxSection.Following, 20f, PinnedWishes);
            // Needs you: the one count is what these count. The on-screen council panel and the colonist bar read the same
            // rows (Inbox.Waiting): wishes and petitions, letters waiting, promises at risk, the other asks.
            Inbox.Register("wishes", InboxSection.Needs, 10f, Wishes, WishesCount, part: InboxMath.PartWishes);
            Inbox.Register("answers", InboxSection.Needs, 20f, Answers, AnswersCount, part: InboxMath.PartQuestions);
            Inbox.Register("asks", InboxSection.Needs, 25f, Asks, AsksCount);
            Inbox.Register("favours", InboxSection.Needs, 30f, DiplomacyPanels.WeekRows, DiplomacyPanels.WeekCount);
            Inbox.Register("settlers", InboxSection.Needs, 35f, HamletPage.WeekRows, HamletPage.WeekCount);
            Inbox.Register("promises", InboxSection.Needs, 40f, Promises, PromisesCount, part: InboxMath.PartPromises);
            // Coming, by date.
            Inbox.Register("calendar", InboxSection.Coming, 10f, Calendar);
            Inbox.Register("meetings", InboxSection.Coming, 15f, Meetings);
            Inbox.Register("fights", InboxSection.Coming, 20f, Fights);
            Inbox.Register("stories", InboxSection.Coming, 30f, Stories);
            Inbox.Register("offers", InboxSection.Coming, 40f, Offers);
            Inbox.Register("posts", InboxSection.Coming, 50f, TradingPostPage.WeekRows);
            Inbox.Register("raiders", InboxSection.Coming, 60f, RaidersCampPage.WeekRows);
            // A founding or a hand-over under way, where it stands, never with a timer (Colonies/ColonyCards.cs; stage 3, release 4).
            Inbox.Register("colonies", InboxSection.Coming, 70f, ColonyCards.WeekRows);
            // How people are, as before; the mood balance at its foot.
            Inbox.Register("strain", InboxSection.People, 10f, Strain);
            Inbox.Register("health", InboxSection.People, 20f, Health);
            Inbox.Register("trust", InboxSection.People, 30f, Trust);
            Inbox.Register("balance", InboxSection.People, 90f, Balance, null, true);
            // The plan: the advisor's most pressing steps, and the proposals waiting under them.
            Inbox.Register("plan", InboxSection.Plan, 10f, Plan);
            Inbox.Register("proposed", InboxSection.Plan, 90f, Proposed, null, true);
        }

        private static string ClickPage(string page) => WeekModel.ClickPage(page);

        private static string Tip(string body, string click) => WeekModel.Tip(body, click);

        // ---------------- Following: wishes pinned ----------------

        private static void PinnedWishes(List<WeekRow> rows)
        {
            var memory = WeekMemory.Instance;
            var comp = Comp;
            if (memory == null || comp == null) return;
            foreach (int id in memory.pinnedWishes)
            {
                var w = comp.wishes.FirstOrDefault(x => x.id == id && x.state == WishState.Open);
                if (w == null || w.pawn == null) continue;
                var row = WishRow(w);
                row.pinned = true;
                row.order = rows.Count;
                rows.Add(row);
            }
        }

        // ---------------- Needs you: wishes and petitions, oldest first ----------------

        private static int WishesCount()
        {
            var comp = Comp;
            if (comp == null) return 0;
            // What the rows list, counted the same way (the panel and the badges read the rows, the button this count).
            int petitions = 0, wishes = 0;
            foreach (var council in ColonyState.Ticking())
                foreach (var r in council.decrees.requests)
                    if (r?.def != null && r.pawn != null)
                        petitions++;
            // A shared want's petition of several counts once, as its row (its first want).
            if (S.enableWishes)
                foreach (var w in comp.wishes)
                    if (w.state == WishState.Open && w.pawn != null && comp.LeadsPetition(w))
                        wishes++;
            return petitions + wishes;
        }

        private static void Wishes(List<WeekRow> rows)
        {
            var comp = Comp;
            if (comp == null) return;
            var memory = WeekMemory.Instance;
            if (S.enableWishes)
                foreach (var w in comp.OpenWishes)
                {
                    // A petition of several is one row, its first want (Wishes/Petitions.cs).
                    if (w.pawn == null || !comp.LeadsPetition(w)) continue;
                    var row = WishRow(w);
                    row.pinned = memory != null && memory.IsPinned(w);
                    rows.Add(row);
                }
            // Every colony's petitions, each named with more than one colony.
            foreach (var council in ColonyState.Ticking())
            foreach (var r in council.decrees.requests)
            {
                if (r?.def == null || r.pawn == null) continue;
                var open = WeekModel.CouncilPage(council, "Decrees");
                rows.Add(new WeekRow
                {
                    pawn = r.pawn,
                    text = WeekModel.AtColony(council, (r.repeal ? "CC_Week_PetitionLift" : "CC_Week_Petition").Translate(WeekModel.Name(r.pawn), r.def.label).Resolve()),
                    sub = "CC_Inbox_Sub_Petition".Translate(InboxWords.Date(r.tick).Named("DATE")).Resolve(),
                    tip = Tip(r.line.NullOrEmpty() ? null : "\"" + r.line + "\"", ClickPage("Decrees")),
                    click = open,
                    act = InboxWords.Decide,
                    actRun = open,
                    follow = ThingRefs.Of(r.pawn),
                    order = r.tick,
                    about = r,
                    part = InboxMath.PartPetitions
                });
            }
        }

        private static WeekRow WishRow(Wish w)
        {
            int left = w.TicksLeft;
            int total = Mathf.Max(1, w.expiresTick - w.createdTick);
            var comp = Comp;
            string text = comp != null && comp.IsPetition(w)
                ? "CC_Petition_Week".Translate(comp.PetitionNames(w).Named("NAMES"), w.LabelInline.Named("WISH")).Resolve()
                : w.ForSomeoneElse && w.Beneficiary != null
                ? "CC_Week_WishFor".Translate(WeekModel.Name(w.pawn), w.Label, WeekModel.Name(w.Beneficiary)).Resolve()
                : "CC_Week_Wish".Translate(WeekModel.Name(w.pawn), w.Label).Resolve();
            if (w.coSigners.Count > 0) text += " " + "CC_Week_WishBacked".Translate(w.coSigners.Count).Resolve();
            var lines = new List<string> { "\"" + w.quote + "\"" };
            if (!w.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + w.reason);
            if (w.promised) lines.Add("CC_Promise_Tip".Translate());
            // When it lapses, as a date: never a countdown on the line.
            if (w.expiresTick > 0) lines.Add("CC_Inbox_Lapses".Translate(InboxWords.Date(w.expiresTick).Named("DATE")).Resolve());
            var open = WeekModel.Page("Wishes");
            float share = left / (float)total;
            // A petition's other names: the row is theirs too (the bar's badge on each of them reads it).
            List<Pawn> others = null;
            if (comp != null && comp.IsPetition(w))
                foreach (var o in comp.PetitionOf(w))
                    if (o != w && o.pawn != null && o.pawn != w.pawn)
                        (others ??= new List<Pawn>()).Add(o.pawn);
            return new WeekRow
            {
                pawn = w.pawn,
                others = others,
                text = text,
                sub = (w.promised ? "CC_Inbox_Sub_WishPromised" : "CC_Inbox_Sub_Wish").Translate(InboxWords.Date(w.createdTick).Named("DATE")).Resolve(),
                subColor = share < 0.25f ? UiKit.Bad : share < 0.5f ? UiKit.Warn : UiKit.Faint,
                color = w.stage >= 2 ? UiKit.Bad : w.promised ? UiKit.Gold : Color.white,
                tip = Tip(string.Join("\n", lines), ClickPage("Wishes")),
                click = open,
                act = InboxWords.Look,
                actRun = open,
                pinWish = w,
                order = w.createdTick,
                about = w,
                // On the panels: worth knowing, soon as its patience runs out, as the council panel always ranked it.
                rank = HudRank.Wish(Mathf.Clamp01(share), w.stage),
                part = InboxMath.PartWishes
            };
        }

        // ---------------- Needs you: letters waiting for an answer ----------------

        private static int AnswersCount()
        {
            int n = 0;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (WeekModel.WaitingKind(l) != null)
                    n++;
            return n;
        }

        private static void Answers(List<WeekRow> rows)
        {
            int now = Now;
            int news = 0;
            Letter newest = null;
            foreach (var l in Find.LetterStack.LettersListForReading)
            {
                if (l is ChoiceLetter_News)
                {
                    news++;
                    if (newest == null || l.arrivalTick > newest.arrivalTick) newest = l;
                    continue;
                }
                string kind = WeekModel.WaitingKind(l);
                if (kind == null) continue;
                var letter = l;
                int gone = l is LetterWithTimeout lt && lt.TimeoutActive ? lt.disappearAtTick : -1;
                int left = gone > 0 ? gone - now : -1;
                Action read = () => letter.OpenLetter();
                rows.Add(new WeekRow
                {
                    icon = l is ChoiceLetter_Dilemma ? Icons.Decree : l is ChoiceLetter_CalendarDay ? CalendarPanel.Icon : Icons.Warning,
                    color = UiKit.Gold,
                    text = kind.Translate(l.Label.ToString()).Resolve(),
                    sub = (l is ChoiceLetter_CouncilMeeting ? "CC_Inbox_Sub_Meeting" : "CC_Inbox_Sub_Letter").Translate(),
                    subColor = left > 0 && left < GenDate.TicksPerDay ? UiKit.Bad : UiKit.Faint,
                    tip = gone > 0 ? Tip("CC_Inbox_LetterGoes".Translate(InboxWords.Date(gone).Named("DATE")).Resolve(), "CC_Week_ClickLetter".Translate())
                        : "CC_Week_ClickLetter".Translate().ToString(),
                    click = read,
                    act = InboxWords.Answer,
                    actRun = read,
                    order = left > 0 ? left : int.MaxValue / 2 + l.arrivalTick,
                    about = l,
                    // Soon on the panels when it would go unanswered within a day.
                    rank = HudRank.Question(left > 0 ? left / (float)GenDate.TicksPerHour : -1f)
                });
            }
            if (news > 0)
            {
                var open = newest;
                rows.Add(new WeekRow
                {
                    icon = Icons.Chronicle,
                    color = UiKit.Faint,
                    text = "CC_Week_News".Translate(news).Resolve(),
                    tip = "CC_Week_NewsTip".Translate(),
                    click = () => open?.OpenLetter(),
                    act = InboxWords.PutAway,
                    actTip = "CC_Week_MarkRead_Tip".Translate(),
                    actRun = () =>
                    {
                        int n = News.PutAllAway();
                        if (n > 0) Messages.Message("CC_Week_MarkedRead".Translate(n).Resolve(), MessageTypeDefOf.SilentInput, false);
                        WeekPanel.Dirty();
                    },
                    order = int.MaxValue,
                    // The evenings' news asks nothing: not counted, not on the council panel.
                    waits = false
                });
            }
        }

        // ---------------- Needs you: the region's asks, and a journey's start waiting ----------------

        /// <summary>A letter of the region's or the council's that still waits for the player's answer (not counted with Rimfolk's other letters), and what it is.</summary>
        private static string AskKind(Letter l)
        {
            switch (l)
            {
                case ChoiceLetter_RaidersCamp c: return c.Waiting ? "CC_Inbox_Sub_CampTerms" : null;
                case ChoiceLetter_Hamlet h: return h.Waiting ? "CC_Inbox_Sub_Hamlet" : null;
                case ChoiceLetter_Election e: return e.Waiting ? "CC_Inbox_Sub_Election" : null;
                case ChoiceLetter_Journey j: return j.Waiting ? "CC_Inbox_Sub_Journey" : null;
                default: return null;
            }
        }

        private static int AsksCount()
        {
            int n = 0;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (AskKind(l) != null)
                    n++;
            return n;
        }

        private static void Asks(List<WeekRow> rows)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
            {
                string kind = AskKind(l);
                if (kind == null) continue;
                var letter = l;
                Action read = () => letter.OpenLetter();
                // The place it is about, as a link: the camp's site, the hamlet's town or place.
                var place = ThingRef.None;
                try
                {
                    var look = l.lookTargets?.PrimaryTarget ?? GlobalTargetInfo.Invalid;
                    if (look.HasWorldObject) place = ThingRefs.Of(look.WorldObject);
                }
                catch (Exception)
                {
                    place = ThingRef.None;
                }
                string label = l.Label.ToString();
                rows.Add(new WeekRow
                {
                    icon = l is ChoiceLetter_RaidersCamp ? Icons.Raid : l is ChoiceLetter_Hamlet ? Icons.Colony : l is ChoiceLetter_Election ? Icons.Office : Icons.World,
                    color = l is ChoiceLetter_RaidersCamp ? UiKit.Bad : UiKit.Gold,
                    text = label.CapitalizeFirst(),
                    sub = place.IsNone ? kind.Translate().Resolve() : "CC_Inbox_Sub_At".Translate(kind.Translate().Named("WHAT"), ThingRefs.Link(place).Named("PLACE")).Resolve(),
                    tip = "CC_Week_ClickLetter".Translate(),
                    click = read,
                    act = InboxWords.Answer,
                    actRun = read,
                    follow = place,
                    order = l.arrivalTick,
                    about = l
                });
            }
        }

        // ---------------- Needs you: promises at risk ----------------

        /// <summary>
        /// A promise at risk: what was given is gone (the grace running), or was taken back and can still be made right. One
        /// taken back past the days to make amends is lost, not waiting (its watch closes at the next look).
        /// </summary>
        private static bool AtRisk(GrantWatch w, int now) =>
            w?.pawn != null && w.phase != KeptPhase.Kept
            && !(w.phase == KeptPhase.TakenBack && now >= w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay));

        private static int PromisesCount()
        {
            var watches = Comp?.kept?.watches;
            if (!KeptTracker.On || watches == null) return 0;
            int n = 0, now = Now;
            foreach (var w in watches)
                if (AtRisk(w, now))
                    n++;
            return n;
        }

        private static void Promises(List<WeekRow> rows)
        {
            var watches = Comp?.kept?.watches;
            if (!KeptTracker.On || watches == null) return;
            int now = Now;
            foreach (var w in watches)
            {
                if (!AtRisk(w, now)) continue;
                bool missing = w.phase == KeptPhase.Missing;
                int until = missing ? w.graceUntil : w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay);
                int left = until - now;
                var target = Where(w);
                string body = missing ? KeptTracker.LostText(w) : "CC_Kept_Tip_TakenBack".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, left)).Named("LEFT")).Resolve();
                var go = target.IsValid ? WeekModel.Go(target) : WeekModel.Page("Wishes");
                rows.Add(new WeekRow
                {
                    pawn = w.pawn,
                    text = "CC_Week_Wish".Translate(WeekModel.Name(w.pawn), (w.label ?? "").CapitalizeFirst()).Resolve(),
                    sub = (missing ? "CC_Inbox_Sub_Missing" : "CC_Inbox_Sub_TakenBack").Translate(InboxWords.Date(until).Named("DATE")).Resolve(),
                    subColor = missing ? UiKit.Warn : UiKit.Bad,
                    color = missing ? Color.white : UiKit.Bad,
                    tip = Tip(body, target.IsValid ? "CC_Week_ClickPlace".Translate().ToString() : ClickPage("Wishes")),
                    click = go,
                    act = InboxWords.Look,
                    actRun = go,
                    follow = ThingRefs.Of(w.pawn),
                    order = missing ? left : -1000000 + left,
                    about = w,
                    // Soon on the panels, sooner in its last day.
                    rank = left < GenDate.TicksPerDay ? 270f : 230f
                });
            }
        }

        /// <summary>Where a watched wish's thing or place is, if it can still be gone to.</summary>
        private static GlobalTargetInfo Where(GrantWatch w)
        {
            try
            {
                foreach (var t in w.things)
                    if (t != null && t.Spawned)
                        return new GlobalTargetInfo(t);
                var map = Find.Maps.FirstOrDefault(m => m.uniqueID == w.mapId);
                if (map != null && w.cells.Count > 0 && w.cells[0].InBounds(map)) return new GlobalTargetInfo(w.cells[0], map);
            }
            catch
            {
            }
            return GlobalTargetInfo.Invalid;
        }

        // ---------------- Coming: the calendar, meetings, fights, stories, offers ----------------

        private static void Calendar(List<WeekRow> rows)
        {
            int now = Now;
            foreach (var n in ColonyCalendar.Coming(now)) rows.Add(Note(n, now, "Calendar"));
        }

        private static void Stories(List<WeekRow> rows)
        {
            int now = Now;
            foreach (var n in StoryChains.Coming(now)) rows.Add(Note(n, now, "HereAndAway"));
        }

        private static WeekRow Note(WorldNote n, int now, string page)
        {
            string text = n.title.NullOrEmpty() ? n.text : n.title;
            string body = n.title.NullOrEmpty() ? n.tip : n.text + (n.tip.NullOrEmpty() ? "" : "\n\n" + n.tip);
            return new WeekRow
            {
                icon = n.icon ?? Icons.WorldEvent,
                color = n.color,
                text = text,
                note = n.due > now ? InboxWords.When(n.due) : null,
                tip = Tip(body, ClickPage(page)),
                click = n.click ?? WeekModel.Page(page),
                order = n.due > 0 ? n.due - now : int.MaxValue / 2
            };
        }

        /// <summary>Each colony's council meeting while it is called or within the week (one in session is under way).</summary>
        private static void Meetings(List<WeekRow> rows)
        {
            if (!S.enableAmbitions) return;
            int now = Now;
            foreach (var council in ColonyState.Ticking())
            {
                var amb = council.ambitions;
                if (amb == null) continue;
                var meeting = amb.meeting;
                bool called = meeting?.map != null && (meeting.Started || meeting.scheduledTick >= 0);
                if (called && meeting.Started) continue;
                int until = called ? meeting.scheduledTick - now : amb.TicksUntilMeeting;
                if (!called && (until < 0 || until > GenDate.TicksPerDay * 7)) continue;
                rows.Add(new WeekRow
                {
                    icon = Icons.Council,
                    color = UiKit.Info,
                    text = WeekModel.AtColony(council, "CC_Week_Meeting".Translate()),
                    note = InboxWords.When(now + Mathf.Max(0, until)),
                    tip = Tip(called ? AmbitionTracker.AnnouncementText(meeting, now) : null, ClickPage("Ambition")),
                    click = WeekModel.CouncilPage(council, "Ambition"),
                    order = Mathf.Max(0, until)
                });
            }
        }

        /// <summary>Fights promised: a people coming to fight, on the day they come.</summary>
        private static void Fights(List<WeekRow> rows)
        {
            var comp = Comp;
            if (comp == null) return;
            int now = Now;
            foreach (var r in comp.happenings.raids)
            {
                if (r?.faction == null) continue;
                var open = WeekModel.Page("Map/Threats/List");
                rows.Add(new WeekRow
                {
                    icon = Icons.Raid,
                    color = UiKit.Bad,
                    faction = r.faction,
                    text = r.label.NullOrEmpty() ? "CC_Week_Fight".Translate(ThingRefs.Link(r.faction.Name, ThingRefs.Of(r.faction))).Resolve() : r.label,
                    note = InboxWords.When(Mathf.Max(now, r.tick)),
                    noteColor = UiKit.Bad,
                    tip = Tip(r.text, ClickPage("Map/Threats/List")),
                    click = open,
                    follow = ThingRefs.Of(r.faction),
                    order = r.tick - now
                });
            }
        }

        /// <summary>Missions offered, until the day the offer stands; one taken on is under way (UI/InboxUnderWay.cs).</summary>
        private static void Offers(List<WeekRow> rows)
        {
            int now = Now;
            foreach (var m in Missions.All)
            {
                if (m == null || m.state != MissionState.Offered) continue;
                var q = Missions.QuestOf(m);
                int until = q != null && q.acceptanceExpireTick > 0 ? q.acceptanceExpireTick : -1;
                var mission = m;
                string goods = null;
                try
                {
                    goods = Missions.GoodsText(m);
                }
                catch
                {
                }
                Action look = q != null ? () => Missions.ShowQuest(mission) : WeekModel.Page("HereAndAway");
                rows.Add(new WeekRow
                {
                    icon = Icons.Promise,
                    color = UiKit.Gold,
                    faction = m.faction,
                    text = m.title ?? m.def?.LabelCap.ToString() ?? "?",
                    sub = m.faction != null
                        ? "CC_Inbox_Sub_OfferFrom".Translate(ThingRefs.Link(m.faction.Name, ThingRefs.Of(m.faction)).Named("PEOPLE")).Resolve()
                        : "CC_Week_Offered".Translate().Resolve(),
                    note = until > now ? "CC_Inbox_Until".Translate(InboxWords.Date(until).Named("DATE")).Resolve() : null,
                    noteColor = until > now && until - now < GenDate.TicksPerDay * 2 ? UiKit.Warn : UiKit.Faint,
                    tip = Tip(goods.NullOrEmpty() ? null : "CC_Missions_TipGoods".Translate(goods).Resolve(), ClickPage("HereAndAway")),
                    click = look,
                    act = InboxWords.Look,
                    actRun = look,
                    order = until > 0 ? until - now : int.MaxValue / 2
                });
            }
        }

        // ---------------- How people are: under strain or near a break ----------------

        private static void Strain(List<WeekRow> rows)
        {
            var memory = WeekMemory.Instance;
            foreach (var p in WeekModel.Colonists())
            {
                var need = p.needs?.mood;
                if (need == null) continue;
                float mood = need.CurLevelPercentage;
                float minor = 0.35f, major = 0.2f;
                try
                {
                    var br = p.mindState?.mentalBreaker;
                    if (br != null)
                    {
                        minor = br.BreakThresholdMinor;
                        major = br.BreakThresholdMajor;
                    }
                }
                catch
                {
                }
                var h = Heart.ForView(p);
                var band = h?.Band ?? StrainBand.Steady;
                bool inBreak = p.InMentalState;
                bool nearBreak = mood < minor + 0.05f;
                if (!inBreak && !nearBreak && band < StrainBand.Worn) continue;
                var parts = new List<string>();
                if (inBreak) parts.Add("CC_Week_InBreak".Translate(p.MentalStateDef?.label ?? "").Resolve());
                else if (mood < major) parts.Add("CC_Week_BelowMajor".Translate());
                else if (mood < minor) parts.Add("CC_Week_BelowMinor".Translate());
                else if (nearBreak) parts.Add("CC_Week_NearMinor".Translate());
                if (band >= StrainBand.Strained) parts.Add(HeartView.BandLabel(band));
                string feel = h != null ? HeartView.DominantText(h) : null;
                if (!feel.NullOrEmpty()) parts.Add(feel);
                // Worst first: a break, below the major line, then strain, then near the minor line.
                float order = inBreak ? -3f : mood < major ? -2f + mood : band == StrainBand.Breaking ? -1f : mood - minor;
                string body = PeopleTable.MoodLines(p);
                if (h != null && band >= StrainBand.Strained) body = (body.NullOrEmpty() ? "" : body + "\n\n") + HeartView.StrainTip(p, h);
                // The heart's needs too: what is missing, what follows from it and what would help.
                string needs = h != null ? HeartView.NeedsTip(p, h) : null;
                if (!needs.NullOrEmpty()) body = (body.NullOrEmpty() ? "" : body + "\n\n") + needs;
                var who = ThingRefs.Of(p);
                rows.Add(new WeekRow
                {
                    pawn = p,
                    text = WeekModel.Name(p) + ": " + string.Join(", ", parts),
                    color = inBreak || mood < major || band == StrainBand.Breaking ? UiKit.Bad : Color.white,
                    note = "CC_Week_MoodNote".Translate(mood.ToStringPercent()).Resolve(),
                    noteColor = WeekModel.MoodColor(p, mood),
                    tip = Tip(body, "CC_Week_ClickMood".Translate(p.LabelShort).Resolve()),
                    click = WeekModel.Inspect(p, typeof(ITab_Pawn_Mood)),
                    follow = who,
                    pinned = memory != null && memory.IsFollowed(who),
                    order = order
                });
            }
        }

        // ---------------- How people are: serious illness and recovery ----------------

        private static void Health(List<WeekRow> rows)
        {
            var memory = WeekMemory.Instance;
            foreach (var p in WeekModel.Colonists())
            {
                var hediffs = p.health?.hediffSet?.hediffs;
                if (hediffs == null) continue;
                foreach (var h in hediffs)
                {
                    if (h?.def == null || !h.Visible) continue;
                    bool serious = AilmentTracker.InfoOf(h.def)?.serious ?? false;
                    bool lethal = !serious && h.def.lethalSeverity > 0f && h.def.makesSickThought && h.def.isBad;
                    bool recovering = !serious && !lethal && h.def == HealthDefOf.Council_Ail_Convalescent;
                    if (!serious && !lethal && !recovering) continue;
                    string tip;
                    try
                    {
                        tip = h.GetTooltip(p, false);
                    }
                    catch
                    {
                        tip = h.LabelCap;
                    }
                    bool tendable = false;
                    try
                    {
                        tendable = h.TendableNow();
                    }
                    catch
                    {
                    }
                    var who = ThingRefs.Of(p);
                    rows.Add(new WeekRow
                    {
                        pawn = p,
                        text = WeekModel.Name(p) + ": " + h.LabelCap,
                        color = recovering ? Color.white : UiKit.Bad,
                        note = tendable ? "CC_Week_NeedsTending".Translate().ToString() : recovering ? "CC_Week_Recovering".Translate().ToString() : null,
                        noteColor = tendable ? UiKit.Bad : UiKit.Faint,
                        tip = Tip(tip, "CC_Week_ClickHealth".Translate(p.LabelShort).Resolve()),
                        click = WeekModel.Inspect(p, typeof(ITab_Pawn_Health)),
                        follow = who,
                        pinned = memory != null && memory.IsFollowed(who),
                        order = recovering ? 1f : tendable ? -1f : 0f
                    });
                }
            }
        }

        // ---------------- How people are: trust this week ----------------

        private static void Trust(List<WeekRow> rows)
        {
            var comp = Comp;
            if (comp == null) return;
            var memory = WeekMemory.Instance;
            var colonists = WeekModel.Colonists();
            int now = Now;
            foreach (var p in colonists)
            {
                var st = comp.standings.For(p, false);
                if (st == null) continue;
                float trust = st.trust;
                float? trend = memory?.TrustTrend(p, trust);
                bool leaving = st.leavingTick > 0;
                bool moved = trend.HasValue && Mathf.Abs(trend.Value) >= 5f;
                if (!leaving && !moved && st.Level != TrustLevel.Bitter) continue;
                string level = ("CC_Trust_" + st.Level).Translate();
                string note = leaving ? "CC_Trust_Leaving".Translate(TextUtil.DaysString(Mathf.Max(0, st.leavingTick - now))).Resolve()
                    : moved ? "CC_Week_TrustMoved".Translate(trend.Value.ToString("+0;-0")).Resolve() : null;
                var pawn = p;
                var who = ThingRefs.Of(p);
                rows.Add(new WeekRow
                {
                    pawn = p,
                    text = WeekModel.Name(p) + ": " + level,
                    color = UiKit.TrustColor(trust),
                    note = note,
                    noteColor = leaving || (trend ?? 0f) < 0f ? UiKit.Bad : UiKit.Good,
                    tip = Tip(st.log.Count > 0 ? string.Join("\n", st.log.AsEnumerable().Reverse()) : null, "CC_Week_ClickPerson".Translate(p.LabelShort).Resolve()),
                    click = () => Cards.Open(pawn),
                    follow = who,
                    pinned = memory != null && memory.IsFollowed(who),
                    order = leaving ? -1000f : -Mathf.Abs(trend ?? 0f)
                });
            }
            // The colony's own trend, from the daily history, after those who moved most.
            var week = comp.history.LastOrDefault(h => h.day <= GenDate.DaysPassed - 7);
            if (week == null || colonists.Count == 0) return;
            float avg = 0f;
            foreach (var p in colonists) avg += comp.standings.TrustOf(p);
            avg /= colonists.Count;
            float d = avg - week.trust;
            if (Mathf.Abs(d) < 1f) return;
            rows.Add(new WeekRow
            {
                icon = Icons.Trust,
                color = Color.white,
                text = "CC_Inbox_ColonyTrust".Translate(("CC_Trust_" + PawnStanding.LevelFor(avg)).Translate().Named("LEVEL")).Resolve(),
                note = "CC_Week_ColonyTrend".Translate(d.ToString("+0;-0")).Resolve(),
                noteColor = d < 0f ? UiKit.Bad : UiKit.Good,
                tip = ClickPage("Table"),
                click = WeekModel.Page("Table"),
                order = 1000f
            });
        }

        // ---------------- How people are: the colony's mood balance ----------------

        private static void Balance(List<WeekRow> rows)
        {
            var colonists = WeekModel.Colonists();
            if (!S.moodInsight || colonists.Count == 0) return;
            float good = 0f, bad = 0f, allGood = 0f, allBad = 0f;
            int people = 0;
            var byKind = new Dictionary<ThoughtDef, (string label, float total)>();
            foreach (var p in colonists)
            {
                if (p.needs?.mood?.thoughts == null || p.Suspended) continue;
                people++;
                foreach (var l in MoodInsight.LinesFor(p))
                {
                    if (l.mood > 0f) allGood += l.mood;
                    else allBad += l.mood;
                    if (MoodInsight.SourceOf(l.def).kind != MoodSource.Rimfolk) continue;
                    if (l.mood > 0f) good += l.mood;
                    else bad += l.mood;
                    byKind.TryGetValue(l.def, out var had);
                    byKind[l.def] = (had.label ?? l.label, had.total + l.mood);
                }
            }
            if (people == 0) return;
            good /= people;
            bad /= people;
            rows.Add(new WeekRow
            {
                balance = CouncilLayoutMath.GoodShare(good, bad),
                balanceGood = "CC_Week_BalanceGood".Translate(MoodView.Number(good)).Resolve(),
                balanceBad = "CC_Week_BalanceBad".Translate(MoodView.Number(bad)).Resolve(),
                tip = Tip("CC_Week_BalanceTip".Translate(MoodView.Number(good), MoodView.Number(bad), MoodView.Number(allGood / people), MoodView.Number(allBad / people)).Resolve()
                          + "\n\n" + "CC_Week_BalanceNet".Translate(MoodView.Number(good + bad)).Resolve().CapitalizeFirst(), ClickPage("Moods")),
                click = WeekModel.Page("Moods"),
                order = -1f
            });
            // The two that weigh most and the two that lift most, across the colony.
            var kinds = byKind.Select(kv => (kv.Key, kv.Value.label, kv.Value.total)).ToList();
            foreach (var k in kinds.Where(k => k.total < -0.05f).OrderBy(k => k.total).Take(2))
                rows.Add(BalanceLine(k.label, k.total, MoodView.BadColor, 0f));
            foreach (var k in kinds.Where(k => k.total > 0.05f).OrderByDescending(k => k.total).Take(2))
                rows.Add(BalanceLine(k.label, k.total, MoodView.GoodColor, 1f));
        }

        private static WeekRow BalanceLine(string label, float total, Color color, float order) => new WeekRow
        {
            icon = total < 0f ? Icons.TrendDown : Icons.TrendUp,
            color = Color.white,
            text = label,
            note = MoodView.Number(total),
            noteColor = color,
            tip = "CC_Week_BalanceLineTip".Translate().Resolve() + "\n\n" + ClickPage("Moods").Colorize(ColoredText.SubtleGrayColor),
            click = WeekModel.Page("Moods"),
            order = order
        };

        // ---------------- The plan ----------------

        /// <summary>The colony's next steps, the advisor's most pressing first, each leading to Planning's Plan (Building/NextSteps.cs).</summary>
        private static void Plan(List<WeekRow> rows)
        {
            if (!NextSteps.On) return;
            var map = Find.CurrentMap;
            int n = 0;
            var open = WeekModel.Page("Plan");
            foreach (var s in NextSteps.PlanFor(map).Where(s => s.Planned))
            {
                n++;
                var level = NextSteps.LevelOf(s);
                rows.Add(new WeekRow
                {
                    icon = Icons.Plan ?? Icons.Week,
                    text = n + ". " + s.title,
                    note = s.HasSource ? ("CC_Level_" + level).Translate().ToString() : "CC_Plan_OwnNote".Translate().ToString(),
                    noteColor = level >= AdviceLevel.High ? UiKit.Warn : UiKit.Faint,
                    color = level >= AdviceLevel.Critical ? UiKit.Bad : Color.white,
                    tip = Tip(s.detail, ClickPage("Plan")),
                    click = open,
                    act = InboxWords.Plan,
                    actRun = open,
                    order = n
                });
            }
        }

        /// <summary>The advisor's proposals waiting for the player's yes, under the plan's steps.</summary>
        private static void Proposed(List<WeekRow> rows)
        {
            if (!NextSteps.On) return;
            int proposed = NextSteps.ProposedFor(Find.CurrentMap).Count;
            if (proposed <= 0) return;
            var open = WeekModel.Page("Plan");
            rows.Add(new WeekRow
            {
                icon = Icons.Priorities ?? Icons.Plan,
                color = UiKit.Faint,
                text = "CC_Week_PlanProposed".Translate(proposed).Resolve(),
                tip = "CC_Week_PlanProposedTip".Translate(),
                click = open,
                order = 0f
            });
        }
    }
}
