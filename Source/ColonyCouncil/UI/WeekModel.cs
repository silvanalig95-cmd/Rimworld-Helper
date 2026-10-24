using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One line on This week: what it is, a note on the right (time left, a number), where a click goes.</summary>
    public sealed class WeekRow
    {
        public string text, note, tip;
        public Texture2D icon;
        public Color color = Color.white, noteColor = UiKit.Faint;
        /// <summary>A portrait in front of the line instead of the icon.</summary>
        public Pawn pawn;
        public Action click;
        /// <summary>What may be pinned from this line (a colonist or a wish); the pin shows on hover, or always when pinned.</summary>
        public Pawn pinPawn;
        public Wish pinWish;
        public bool pinned;
        /// <summary>The mood balance bar: the good share of Rimfolk's moods (0..1), or -1 for an ordinary line.</summary>
        public float balance = -1f;
        public string balanceGood, balanceBad;
        /// <summary>Sorting within the card (smaller first); not shown.</summary>
        public float order;
        /// <summary>The note's width, measured once when first drawn.</summary>
        public float noteWidth = -1f;
    }

    /// <summary>One card on This week: a heading that opens its page, its lines (the first few), and an optional action.</summary>
    public sealed class WeekCard
    {
        public string key, title, empty, page, pageName, right, more;
        public Texture2D icon;
        public Color color = UiKit.HeaderColor;
        public readonly List<WeekRow> rows = new List<WeekRow>();
        public string actionLabel, actionTip;
        public Action action;
        // Placed by the panel when the width changes.
        public int column;
        public float y, height;
        public int shown;
    }

    /// <summary>
    /// What This week shows, worked out at most once a second (or right after the player changes something, such as a
    /// pin): every system is asked once here, so drawing the page asks nothing of the game. Each part is built in its
    /// own try/catch, so one system's trouble leaves the rest of the page standing.
    /// </summary>
    public sealed class WeekModel
    {
        public string title, sub, stat, headerTip;
        public Color statColor = UiKit.Gold;
        public readonly List<WeekCard> cards = new List<WeekCard>();
        public int needs;

        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;

        public static WeekModel Build(CouncilGameComponent comp, Map map)
        {
            var m = new WeekModel
            {
                title = "CC_Week_Header".Translate(),
                sub = "CC_UI_Sub_Week".Translate(),
                headerTip = "CC_Week_HeaderTip".Translate()
            };
            var colonists = Colonists();
            var memory = WeekMemory.Instance;
            m.Add(comp, "pinned", () => Pinned(comp, colonists, memory), true);
            m.Add(comp, "wishes", () => Wishes(comp, memory));
            m.Add(comp, "answers", Answers);
            m.Add(comp, "promises", () => Promises(comp));
            m.Add(comp, "strain", () => Strain(colonists, memory));
            m.Add(comp, "planned", () => Planned(comp));
            m.Add(comp, "coming", () => Coming(comp));
            m.Add(comp, "health", () => Health(colonists, memory));
            m.Add(comp, "missions", Missions_);
            m.Add(comp, "trust", () => Trust(comp, colonists, memory));
            m.Add(comp, "balance", () => Balance(colonists));
            // The same count as the page's tab (the news letters aren't counted: they ask nothing).
            m.needs = NeedsCount(comp);
            m.stat = m.needs > 0 ? "CC_Week_StatNeeds".Translate(m.needs).Resolve() : "CC_Week_StatCalm".Translate().Resolve();
            m.statColor = m.needs > 0 ? UiKit.Gold : UiKit.Good;
            return m;
        }

        private void Add(CouncilGameComponent comp, string key, Func<WeekCard> make, bool onlyWithRows = false)
        {
            WeekCard c;
            try
            {
                c = make();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("this week: " + key, e);
                return;
            }
            if (c == null || (onlyWithRows && c.rows.Count == 0)) return;
            c.key = key;
            if (!c.page.NullOrEmpty()) c.pageName = MainTabWindow_Council.TabName(c.page);
            // A stable sort, so lines with the same order keep the order they were found in.
            var sorted = c.rows.OrderBy(r => r.order).ToList();
            c.rows.Clear();
            c.rows.AddRange(sorted);
            int hidden = c.rows.Count - CouncilLayoutMath.ShownRows(c.rows.Count);
            if (hidden > 0) c.more = "CC_Week_More".Translate(hidden).Resolve();
            cards.Add(c);
        }

        private static WeekCard Card(string titleKey, string emptyKey, Texture2D icon, Color color, string page) => new WeekCard
        {
            title = titleKey.Translate(),
            empty = emptyKey.Translate(),
            icon = icon,
            color = color,
            page = page
        };

        /// <summary>Everyone the page is about: the colony's free colonists with an inner life, on a map or away.</summary>
        public static List<Pawn> Colonists()
        {
            var list = new List<Pawn>();
            // A copy: the game reuses the list it hands out.
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep.ToList())
                if (p != null && !p.IsQuestLodger() && p.RaceProps.Humanlike && People.HasInnerLife(p) && !list.Contains(p))
                    list.Add(p);
            return list;
        }

        // ---------------- Clicks ----------------

        internal static Action Page(string page) => () => MainTabWindow_Council.OpenTab(page);

        /// <summary>Close the window and go to a thing or a place on the map.</summary>
        internal static Action Go(GlobalTargetInfo target) => () =>
        {
            if (!target.IsValid) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJumpAndSelect(target);
        };

        /// <summary>Select a colonist and open one of their inspect tabs (the Mood tab, the Health tab).</summary>
        internal static Action Inspect(Pawn p, Type tab) => () =>
        {
            if (p == null) return;
            try
            {
                if (!p.Spawned && p.CarriedBy == null)
                {
                    MainTabWindow_Council.OpenPerson(p);
                    return;
                }
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
                if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(tab);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("this week: inspect", e);
            }
        };

        private static string ClickPage(string page) => "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName(page)).Resolve();

        private static string Tip(string body, string click) => body.NullOrEmpty() ? click : body + "\n\n" + click.Colorize(ColoredText.SubtleGrayColor);

        // ---------------- Pinned ----------------

        private static WeekCard Pinned(CouncilGameComponent comp, List<Pawn> colonists, WeekMemory memory)
        {
            var c = Card("CC_Week_Pinned", "CC_Week_Empty_Pinned", Icons.Pin ?? Icons.Promise, UiKit.Gold, "Everyone");
            if (memory == null) return c;
            foreach (var id in memory.pinnedPawns)
            {
                var p = colonists.FirstOrDefault(x => x.ThingID == id);
                if (p == null) continue;
                var row = PersonRow(comp, p);
                row.pinPawn = p;
                row.pinned = true;
                row.order = c.rows.Count;
                c.rows.Add(row);
            }
            foreach (int id in memory.pinnedWishes)
            {
                var w = comp.wishes.FirstOrDefault(x => x.id == id && x.state == WishState.Open);
                if (w == null) continue;
                var row = WishRow(comp, w);
                row.pinned = true;
                row.order = 1000 + c.rows.Count;
                c.rows.Add(row);
            }
            return c;
        }

        /// <summary>A pinned colonist: mood, strain, their open wish, what's ailing them.</summary>
        private static WeekRow PersonRow(CouncilGameComponent comp, Pawn p)
        {
            var parts = new List<string>();
            var h = Heart.ForView(p);
            if (h != null && h.Band != StrainBand.Steady) parts.Add(HeartView.BandLabel(h.Band));
            string feel = h != null ? HeartView.DominantText(h) : null;
            if (!feel.NullOrEmpty()) parts.Add(feel);
            var wish = comp.OpenWishes.FirstOrDefault(w => w.pawn == p);
            if (wish != null) parts.Add("CC_Week_PinnedWish".Translate(wish.Label).Resolve());
            float mood = p.needs?.mood?.CurLevelPercentage ?? -1f;
            var row = new WeekRow
            {
                pawn = p,
                text = p.LabelShortCap + (parts.Count > 0 ? ": " + string.Join(", ", parts) : ""),
                note = mood >= 0f ? "CC_Week_MoodNote".Translate(mood.ToStringPercent()).Resolve() : null,
                noteColor = mood >= 0f ? MoodColor(p, mood) : UiKit.Faint,
                click = Inspect(p, typeof(ITab_Pawn_Mood))
            };
            // The heart's need they lack most, if any: what follows from it and what would help.
            string summary = PeopleTable.Summary(p), brief = h != null ? HeartView.NeedsBrief(p, h) : null;
            if (!brief.NullOrEmpty()) summary = (summary.NullOrEmpty() ? "" : summary + "\n\n") + brief;
            row.tip = Tip(summary, "CC_Week_ClickMood".Translate(p.LabelShort).Resolve());
            return row;
        }

        internal static Color MoodColor(Pawn p, float mood)
        {
            if (p.InMentalState) return UiKit.Bad;
            var br = p.mindState?.mentalBreaker;
            float minor = 0.35f, major = 0.2f;
            try
            {
                if (br != null)
                {
                    minor = br.BreakThresholdMinor;
                    major = br.BreakThresholdMajor;
                }
            }
            catch
            {
            }
            return mood < major ? UiKit.Bad : mood < minor ? UiKit.Warn : mood < minor + 0.1f ? UiKit.Gold : UiKit.Good;
        }

        // ---------------- Wishes and petitions, oldest first ----------------

        private static WeekCard Wishes(CouncilGameComponent comp, WeekMemory memory)
        {
            var c = Card("CC_Week_Wishes", "CC_Week_Empty_Wishes", Icons.Wishes, UiKit.Gold, "Wishes");
            if (S.enableWishes)
                foreach (var w in comp.OpenWishes)
                {
                    if (w.pawn == null) continue;
                    var row = WishRow(comp, w);
                    row.pinned = memory != null && memory.IsPinned(w);
                    c.rows.Add(row);
                }
            foreach (var r in comp.decrees.requests)
            {
                if (r?.def == null || r.pawn == null) continue;
                var req = r;
                c.rows.Add(new WeekRow
                {
                    pawn = r.pawn,
                    text = (r.repeal ? "CC_Week_PetitionLift" : "CC_Week_Petition").Translate(r.pawn.LabelShort, r.def.label).Resolve(),
                    note = MainTabWindow_Council.AgoString(r.tick),
                    tip = Tip(r.line.NullOrEmpty() ? null : "\"" + r.line + "\"", ClickPage("Decrees")),
                    click = Page("Decrees"),
                    order = r.tick
                });
            }
            return c;
        }

        private static WeekRow WishRow(CouncilGameComponent comp, Wish w)
        {
            int left = w.TicksLeft;
            int total = Mathf.Max(1, w.expiresTick - w.createdTick);
            string text = w.ForSomeoneElse && w.Beneficiary != null
                ? "CC_Week_WishFor".Translate(w.pawn.LabelShort, w.Label, w.Beneficiary.LabelShort).Resolve()
                : "CC_Week_Wish".Translate(w.pawn.LabelShort, w.Label).Resolve();
            if (w.coSigners.Count > 0) text += " " + "CC_Week_WishBacked".Translate(w.coSigners.Count).Resolve();
            var lines = new List<string> { "\"" + w.quote + "\"" };
            if (!w.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + w.reason);
            if (w.promised) lines.Add("CC_Promise_Tip".Translate());
            return new WeekRow
            {
                pawn = w.pawn,
                text = text,
                note = "CC_Week_Left".Translate(UiKit.Duration(left)).Resolve(),
                noteColor = UiKit.Urgency(left / (float)total),
                color = w.stage >= 2 ? UiKit.Bad : w.promised ? UiKit.Gold : Color.white,
                tip = Tip(string.Join("\n", lines), ClickPage("Wishes")),
                click = Page("Wishes"),
                pinWish = w,
                order = w.createdTick
            };
        }

        // ---------------- Letters waiting for an answer ----------------

        private static WeekCard Answers()
        {
            var c = Card("CC_Week_Answers", "CC_Week_Empty_Answers", Icons.Decree, UiKit.Gold, "Dilemmas");
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
                string kind = WaitingKind(l);
                if (kind == null) continue;
                var letter = l;
                int left = l is LetterWithTimeout lt && lt.TimeoutActive ? lt.disappearAtTick - now : -1;
                c.rows.Add(new WeekRow
                {
                    icon = l is ChoiceLetter_Dilemma ? Icons.Decree : l is ChoiceLetter_CalendarDay ? CalendarPanel.Icon : Icons.Warning,
                    color = UiKit.Gold,
                    text = kind.Translate(l.Label.ToString()).Resolve(),
                    note = left > 0 ? "CC_Week_Left".Translate(UiKit.Duration(left)).Resolve() : null,
                    noteColor = left > 0 && left < GenDate.TicksPerDay ? UiKit.Bad : UiKit.Faint,
                    tip = "CC_Week_ClickLetter".Translate(),
                    click = () => letter.OpenLetter(),
                    order = left > 0 ? left : int.MaxValue / 2 + l.arrivalTick
                });
            }
            if (news > 0)
            {
                var open = newest;
                c.rows.Add(new WeekRow
                {
                    icon = Icons.Chronicle,
                    color = UiKit.Faint,
                    text = "CC_Week_News".Translate(news).Resolve(),
                    tip = "CC_Week_NewsTip".Translate(),
                    click = () => open?.OpenLetter(),
                    order = int.MaxValue
                });
                c.actionLabel = "CC_Week_MarkRead".Translate();
                c.actionTip = "CC_Week_MarkRead_Tip".Translate();
                c.action = () =>
                {
                    int n = News.PutAllAway();
                    if (n > 0) Messages.Message("CC_Week_MarkedRead".Translate(n).Resolve(), MessageTypeDefOf.SilentInput, false);
                    WeekPanel.Dirty();
                };
            }
            return c;
        }

        /// <summary>The line's key for a Rimfolk letter still waiting for the player's answer, or null.</summary>
        internal static string WaitingKind(Letter l)
        {
            switch (l)
            {
                case ChoiceLetter_Dilemma d: return d.resolved ? null : "CC_Week_LetterDilemma";
                case ChoiceLetter_Happening h: return h.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_CalendarDay cd: return cd.resolved ? null : "CC_Week_LetterDay";
                case ChoiceLetter_Camp camp: return camp.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_Situation s: return s.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_CouncilMeeting _: return "CC_Week_LetterMeeting";
                default: return null;
            }
        }

        // ---------------- Promises at risk ----------------

        private static WeekCard Promises(CouncilGameComponent comp)
        {
            var c = Card("CC_Week_Promises", "CC_Week_Empty_Promises", Icons.Promise, UiKit.Warn, "Wishes");
            var watches = comp.kept?.watches;
            if (!KeptTracker.On || watches == null) return c;
            int now = Now;
            foreach (var w in watches)
            {
                if (w?.pawn == null || w.phase == KeptPhase.Kept) continue;
                bool missing = w.phase == KeptPhase.Missing;
                int left = missing ? w.graceUntil - now : w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay) - now;
                var target = Where(w);
                string body = missing ? KeptTracker.LostText(w) : "CC_Kept_Tip_TakenBack".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, left)).Named("LEFT")).Resolve();
                c.rows.Add(new WeekRow
                {
                    pawn = w.pawn,
                    text = "CC_Week_Wish".Translate(w.pawn.LabelShort, (w.label ?? "").CapitalizeFirst()).Resolve(),
                    note = (missing ? "CC_Week_MissingLeft" : "CC_Week_TakenBackLeft").Translate(KeptTracker.Span(Mathf.Max(1, left))).Resolve(),
                    noteColor = missing ? UiKit.Warn : UiKit.Bad,
                    color = missing ? Color.white : UiKit.Bad,
                    tip = Tip(body, target.IsValid ? "CC_Week_ClickPlace".Translate().ToString() : ClickPage("Wishes")),
                    click = target.IsValid ? Go(target) : Page("Wishes"),
                    order = missing ? left : -1000000 + left
                });
            }
            return c;
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

        // ---------------- Gatherings and plans ----------------

        private static WeekCard Planned(CouncilGameComponent comp)
        {
            var c = Card("CC_Week_Planned", "CC_Week_Empty_Planned", Icons.Wedding, UiKit.Info, "Calendar");
            int now = Now;
            var plays = PlayOutTracker.Instance?.plays;
            if (plays != null)
                foreach (var p in plays)
                {
                    if (p == null || p.state == PlayState.Done) continue;
                    // Who it's about, what it is and how far it has got (UI/PlayOutText.cs: gatherings, visits, work parties, walks, vigils, searches, errands, hunts).
                    var who = PlayOutText.Who(p);
                    string label = p.label.NullOrEmpty() ? ("CC_Week_Play_" + p.kind).Translate().ToString() : p.label.CapitalizeFirst();
                    bool running = p.state == PlayState.Running;
                    int left = p.deadline - now;
                    var target = who != null && who.Spawned ? new GlobalTargetInfo(who) : GlobalTargetInfo.Invalid;
                    string body = PlayOutText.Body(p);
                    if (!running && !p.waitReason.NullOrEmpty()) body = (body == null ? "" : body + "\n") + "CC_Week_PlayWaitingWhy".Translate(p.waitReason).Resolve();
                    c.rows.Add(new WeekRow
                    {
                        pawn = who,
                        icon = PlayOutText.Icon(p),
                        color = running ? UiKit.Good : Color.white,
                        text = label,
                        note = running ? "CC_Week_UnderWay".Translate().ToString() : left > 0 ? "CC_Week_Within".Translate(UiKit.Duration(left)).Resolve() : "CC_Week_Waiting".Translate().ToString(),
                        noteColor = running ? UiKit.Good : UiKit.Faint,
                        tip = Tip(body, target.IsValid ? "CC_Week_ClickMap".Translate(who.LabelShort).Resolve() : null),
                        click = target.IsValid ? Go(target) : null,
                        order = running ? -1 : Mathf.Max(0, left)
                    });
                }
            // What an answer keeps on the map for a while (PlayOut/OnMapTimed.cs): lanterns, a fire, the pyre, a bolted door, an animal at heel.
            var kept = OnMapTimed.Instance?.kept;
            if (kept != null)
                foreach (var k in kept)
                {
                    var row = OnMapTimed.WeekRowFor(k);
                    if (row != null) c.rows.Add(row);
                }
            // The council meeting: called, or simply next on the calendar.
            var amb = comp.ambitions;
            var meeting = amb?.meeting;
            if (S.enableAmbitions && amb != null)
            {
                bool called = meeting?.map != null && (meeting.Started || meeting.scheduledTick >= 0);
                int until = called ? (meeting.Started ? 0 : meeting.scheduledTick - now) : amb.TicksUntilMeeting;
                if (called || (until >= 0 && until <= GenDate.TicksPerDay * 7))
                    c.rows.Add(new WeekRow
                    {
                        icon = Icons.Council,
                        color = UiKit.Info,
                        text = "CC_Week_Meeting".Translate(),
                        note = called && meeting.Started ? "CC_Week_UnderWay".Translate().ToString() : "CC_Week_In".Translate(UiKit.Duration(Mathf.Max(0, until))).Resolve(),
                        tip = Tip(called ? (meeting.Started ? AmbitionPanel.InSessionText(meeting) : AmbitionTracker.AnnouncementText(meeting, now)) : null, ClickPage("Ambition")),
                        click = Page("Ambition"),
                        order = Mathf.Max(0, until)
                    });
            }
            return c;
        }

        // ---------------- Coming days: the calendar, fights, stories ----------------

        private static WeekCard Coming(CouncilGameComponent comp)
        {
            var c = Card("CC_Week_Coming", "CC_Week_Empty_Coming", CalendarPanel.Icon ?? Icons.Chronicle, UiKit.HeaderColor, "Calendar");
            int now = Now;
            foreach (var n in CalendarTracker.Coming(now)) c.rows.Add(Note(n, now, "Calendar"));
            foreach (var r in comp.happenings.raids)
            {
                if (r?.faction == null) continue;
                c.rows.Add(new WeekRow
                {
                    icon = Icons.Raid,
                    color = UiKit.Bad,
                    text = r.label.NullOrEmpty() ? "CC_Week_Fight".Translate(r.faction.Name).Resolve() : r.label,
                    note = "CC_Week_In".Translate(UiKit.Duration(Mathf.Max(0, r.tick - now))).Resolve(),
                    noteColor = UiKit.Bad,
                    tip = Tip(r.text, ClickPage("World")),
                    click = Page("World"),
                    order = r.tick - now
                });
            }
            foreach (var n in StoryChains.Coming(now)) c.rows.Add(Note(n, now, "World"));
            return c;
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
                note = n.due > now ? "CC_Week_In".Translate(UiKit.Duration(n.due - now)).Resolve() : null,
                tip = Tip(body, ClickPage(page)),
                click = n.click ?? Page(page),
                order = n.due > 0 ? n.due - now : int.MaxValue / 2
            };
        }

        // ---------------- Missions and camps ----------------

        private static WeekCard Missions_()
        {
            var c = Card("CC_Week_Missions", "CC_Week_Empty_Missions", Icons.Trader, UiKit.Gold, "World");
            int now = Now;
            foreach (var m in Missions.All)
            {
                if (m == null || !m.Open) continue;
                string note = null;
                int left = -1;
                if (m.state == MissionState.Offered)
                {
                    var q = Missions.QuestOf(m);
                    left = q != null && q.acceptanceExpireTick > 0 ? q.acceptanceExpireTick - now : -1;
                    note = left > 0 ? "CC_Week_ToAccept".Translate(UiKit.Duration(left)).Resolve() : "CC_Week_Offered".Translate().ToString();
                }
                else if (m.deadline > 0)
                {
                    left = m.deadline - now;
                    note = "CC_Week_Left".Translate(UiKit.Duration(Mathf.Max(0, left))).Resolve();
                }
                else note = "CC_Week_UnderWay".Translate();
                var mission = m;
                string goods = null;
                try
                {
                    goods = Missions.GoodsText(m);
                }
                catch
                {
                }
                c.rows.Add(new WeekRow
                {
                    icon = Icons.Promise,
                    color = UiKit.Gold,
                    text = m.title ?? m.def?.LabelCap.ToString() ?? "?",
                    note = note,
                    noteColor = left > 0 && left < GenDate.TicksPerDay * 2 ? UiKit.Bad : UiKit.Faint,
                    tip = Tip(goods.NullOrEmpty() ? null : "CC_Missions_TipGoods".Translate(goods).Resolve(), ClickPage("World")),
                    click = Missions.QuestOf(m) != null ? () => Missions.ShowQuest(mission) : Page("World"),
                    order = left > 0 ? left : int.MaxValue / 2
                });
            }
            var camp = CampTracker.Instance?.camp;
            if (camp != null && camp.Active && camp.map != null && Find.Maps.Contains(camp.map))
            {
                var target = camp.center.IsValid ? new GlobalTargetInfo(camp.center, camp.map) : GlobalTargetInfo.Invalid;
                c.rows.Add(new WeekRow
                {
                    icon = Icons.Visitor,
                    color = UiKit.StrangerColor,
                    text = "CC_Week_Camp".Translate(camp.kind?.LabelCap.ToString() ?? "?").Resolve(),
                    note = "CC_Week_Tension".Translate(camp.TensionWord).Resolve(),
                    noteColor = CampPanel.TensionColor(camp.tension),
                    tip = Tip(camp.StatusLine() + "\n" + camp.TermsLine(), target.IsValid ? "CC_Week_ClickPlace".Translate().ToString() : ClickPage("World")),
                    click = target.IsValid ? Go(target) : Page("World"),
                    order = -1
                });
            }
            // Hospitality's guests staying now (Integrations/GuestsWeek.cs).
            GuestsWeek.AddRows(c);
            return c;
        }

        // ---------------- Under strain or near a break ----------------

        private static WeekCard Strain(List<Pawn> colonists, WeekMemory memory)
        {
            var c = Card("CC_Week_Strain", "CC_Week_Empty_Strain", Icons.Warning, UiKit.Warn, "Moods");
            foreach (var p in colonists)
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
                c.rows.Add(new WeekRow
                {
                    pawn = p,
                    text = p.LabelShortCap + ": " + string.Join(", ", parts),
                    color = inBreak || mood < major || band == StrainBand.Breaking ? UiKit.Bad : Color.white,
                    note = "CC_Week_MoodNote".Translate(mood.ToStringPercent()).Resolve(),
                    noteColor = MoodColor(p, mood),
                    tip = Tip(body, "CC_Week_ClickMood".Translate(p.LabelShort).Resolve()),
                    click = Inspect(p, typeof(ITab_Pawn_Mood)),
                    pinPawn = p,
                    pinned = memory != null && memory.IsPinned(p),
                    order = order
                });
            }
            return c;
        }

        // ---------------- Serious illness and recovery ----------------

        private static WeekCard Health(List<Pawn> colonists, WeekMemory memory)
        {
            var c = Card("CC_Week_Health", "CC_Week_Empty_Health", Icons.CatHealth, UiKit.Bad, null);
            foreach (var p in colonists)
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
                    c.rows.Add(new WeekRow
                    {
                        pawn = p,
                        text = p.LabelShortCap + ": " + h.LabelCap,
                        color = recovering ? Color.white : UiKit.Bad,
                        note = tendable ? "CC_Week_NeedsTending".Translate().ToString() : recovering ? "CC_Week_Recovering".Translate().ToString() : null,
                        noteColor = tendable ? UiKit.Bad : UiKit.Faint,
                        tip = Tip(tip, "CC_Week_ClickHealth".Translate(p.LabelShort).Resolve()),
                        click = Inspect(p, typeof(ITab_Pawn_Health)),
                        pinPawn = p,
                        pinned = memory != null && memory.IsPinned(p),
                        order = recovering ? 1f : tendable ? -1f : 0f
                    });
                }
            }
            return c;
        }

        // ---------------- Trust this week ----------------

        private static WeekCard Trust(CouncilGameComponent comp, List<Pawn> colonists, WeekMemory memory)
        {
            var c = Card("CC_Week_Trust", "CC_Week_Empty_Trust", Icons.Trust, UiKit.Info, "Wishes");
            // The colony's own trend, from the daily history.
            var week = comp.history.LastOrDefault(h => h.day <= GenDate.DaysPassed - 7);
            if (week != null && colonists.Count > 0)
            {
                float avg = 0f;
                foreach (var p in colonists) avg += comp.standings.TrustOf(p);
                avg /= colonists.Count;
                float d = avg - week.trust;
                if (Mathf.Abs(d) >= 1f) c.right = "CC_Week_ColonyTrend".Translate(d.ToString("+0;-0")).Resolve();
            }
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
                c.rows.Add(new WeekRow
                {
                    pawn = p,
                    text = p.LabelShortCap + ": " + level,
                    color = UiKit.TrustColor(trust),
                    note = note,
                    noteColor = leaving || (trend ?? 0f) < 0f ? UiKit.Bad : UiKit.Good,
                    tip = Tip(st.log.Count > 0 ? string.Join("\n", st.log.AsEnumerable().Reverse()) : null, "CC_Week_ClickPerson".Translate(p.LabelShort).Resolve()),
                    click = () => MainTabWindow_Council.OpenPerson(pawn),
                    pinPawn = p,
                    pinned = memory != null && memory.IsPinned(p),
                    order = leaving ? -1000f : -Mathf.Abs(trend ?? 0f)
                });
            }
            return c;
        }

        // ---------------- The colony's mood balance ----------------

        private static WeekCard Balance(List<Pawn> colonists)
        {
            var c = Card("CC_Week_Balance", "CC_Week_Empty_Balance", Icons.Moods, UiKit.Good, "Moods");
            if (!S.moodInsight || colonists.Count == 0) return c;
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
            if (people == 0) return c;
            good /= people;
            bad /= people;
            float net = good + bad;
            c.right = "CC_Week_BalanceNet".Translate(MoodView.Number(net)).Resolve();
            c.rows.Add(new WeekRow
            {
                balance = CouncilLayoutMath.GoodShare(good, bad),
                balanceGood = "CC_Week_BalanceGood".Translate(MoodView.Number(good)).Resolve(),
                balanceBad = "CC_Week_BalanceBad".Translate(MoodView.Number(bad)).Resolve(),
                tip = Tip("CC_Week_BalanceTip".Translate(MoodView.Number(good), MoodView.Number(bad), MoodView.Number(allGood / people), MoodView.Number(allBad / people)).Resolve(),
                    ClickPage("Moods")),
                click = Page("Moods"),
                order = -1f
            });
            // The two that weigh most and the two that lift most, across the colony.
            var kinds = byKind.Select(kv => (kv.Key, kv.Value.label, kv.Value.total)).ToList();
            foreach (var k in kinds.Where(k => k.total < -0.05f).OrderBy(k => k.total).Take(2))
                c.rows.Add(BalanceLine(k.label, k.total, MoodView.BadColor, 0f));
            foreach (var k in kinds.Where(k => k.total > 0.05f).OrderByDescending(k => k.total).Take(2))
                c.rows.Add(BalanceLine(k.label, k.total, MoodView.GoodColor, 1f));
            return c;
        }

        private static WeekRow BalanceLine(string label, float total, Color color, float order) => new WeekRow
        {
            icon = total < 0f ? Icons.TrendDown : Icons.TrendUp,
            color = Color.white,
            text = label,
            note = MoodView.Number(total),
            noteColor = color,
            tip = "CC_Week_BalanceLineTip".Translate().Resolve() + "\n\n" + ClickPage("Moods").Colorize(ColoredText.SubtleGrayColor),
            click = Page("Moods"),
            order = order
        };

        // ---------------- The count on the tab ----------------

        /// <summary>How many things wait on the player (wishes, petitions, letters, promises at risk), for the tab's label.</summary>
        public static int NeedsCount(CouncilGameComponent comp)
        {
            if (comp == null) return 0;
            int n = comp.decrees.requests.Count;
            if (S.enableWishes)
                foreach (var w in comp.wishes)
                    if (w.state == WishState.Open)
                        n++;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (WaitingKind(l) != null)
                    n++;
            var watches = comp.kept?.watches;
            if (KeptTracker.On && watches != null)
                foreach (var w in watches)
                    if (w != null && w.phase != KeptPhase.Kept)
                        n++;
            return n;
        }
    }
}
