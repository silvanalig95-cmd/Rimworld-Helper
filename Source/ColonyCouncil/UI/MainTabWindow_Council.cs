using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council button's window: the colony's own life in four sections, each with its pages along the top. Council:
    /// decrees with the offices and elections, the ambition and its meetings, the colony's ways, dilemmas, wishes and
    /// petitions. People: the roster, everyone, bonds, faiths, moods, dreams and journals. Colony: this week, the
    /// overview, priorities, the calendar and the chronicle. And the Almanac. Which page is in which section is
    /// UI/PagesMath.cs; the region, defense and planning windows have the rest, one click away in the top right corner.
    /// </summary>
    [StaticConstructorOnStartup]
    public class MainTabWindow_Council : RimfolkTabWindow
    {
        /// <summary>The sections, in PagesMath.CouncilSections' order.</summary>
        private enum Section
        {
            Council,
            People,
            Colony,
            Almanac
        }

        /// <summary>The council window's pages, by their names in PagesMath.</summary>
        private enum Tab
        {
            Decrees,
            Ambition,
            Ways,
            Dilemmas,
            Wishes,
            Roster,
            Everyone,
            Bonds,
            Faiths,
            Moods,
            Dreams,
            Journals,
            Week,
            Overview,
            Priorities,
            Calendar,
            Chronicle,
            Almanac
        }

        public override RimfolkWindow Which => RimfolkWindow.Council;

        private static Tab tab = Tab.Week;
        /// <summary>A button asked for a page (OpenTab, OpenPerson...): the window opens there, not on This week.</summary>
        private static bool asked;
        /// <summary>The sub-tab last open in each section, so switching sections comes back to where you were.</summary>
        private static readonly Dictionary<Section, Tab> lastInSection = new Dictionary<Section, Tab>();
        /// <summary>
        /// The page from the settings has been put back (once a session), or a button already asked for another page.
        /// The window opens where the player left it: within a session from the static tab, across sessions from the
        /// settings, which are saved when the window closes on a different page than before.
        /// </summary>
        private static bool restored;

        private static Section SectionOf(Tab t) => System.Enum.TryParse(PagesMath.SectionOf(t.ToString()), out Section s) ? s : Section.Almanac;

        private static Tab[] TabsOf(Section s) => Faiths.On ? WithFaiths[(int)s] : WithoutFaiths[(int)s];

        // Each section's pages from PagesMath, made once (asked every frame); the faiths page only while "Faith in the colony" is on.
        private static readonly Tab[][] WithFaiths = MakeTabs(true);
        private static readonly Tab[][] WithoutFaiths = MakeTabs(false);

        private static Tab[][] MakeTabs(bool faiths)
        {
            var sections = (Section[])System.Enum.GetValues(typeof(Section));
            var result = new Tab[sections.Length][];
            foreach (var s in sections)
            {
                var list = new List<Tab>();
                foreach (var name in PagesMath.PagesIn(RimfolkWindow.Council, s.ToString()))
                    if (System.Enum.TryParse(name, out Tab t) && (faiths || t != Tab.Faiths))
                        list.Add(t);
                result[(int)s] = list.ToArray();
            }
            return result;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RestorePage();
            // "Council window opens on: This week", unless a button asked for another page.
            if (!asked && Settings?.councilOpensOn == CouncilOpensOn.ThisWeek)
            {
                tab = Tab.Week;
                lastInSection[Section.Colony] = Tab.Week;
            }
            asked = false;
            WeekPanel.Dirty();
        }

        public override void PostClose()
        {
            base.PostClose();
            asked = false;
            RememberPage();
        }

        /// <summary>Opens on the page last looked at in an earlier session, if nothing asked for another one.</summary>
        private static void RestorePage()
        {
            if (restored) return;
            restored = true;
            try
            {
                var s = Settings;
                // A page that went to another window (World, Defenses, the planning pages) isn't one here: the window opens as it would.
                if (s == null || s.lastCouncilPage.NullOrEmpty() || !System.Enum.TryParse(s.lastCouncilPage, out Tab t)) return;
                if (!asked)
                {
                    tab = t;
                    lastInSection[SectionOf(t)] = t;
                }
                var page = s.lastAlmanacPage.NullOrEmpty() ? null : DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(s.lastAlmanacPage);
                if (page != null) AlmanacPanel.Show(page);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("council window page", e);
            }
        }

        /// <summary>Keeps the page in the settings for next time; the file is written only when the page changed.</summary>
        private static void RememberPage()
        {
            try
            {
                var s = Settings;
                if (s == null) return;
                string page = tab.ToString();
                string almanac = AlmanacPanel.Selected?.defName ?? "";
                // The pages' filters and sort orders (UI/PageMemory.cs) go into the same write.
                bool pages = PageMemory.Flush();
                if (s.lastCouncilPage == page && s.lastAlmanacPage == almanac && !pages) return;
                s.lastCouncilPage = page;
                s.lastAlmanacPage = almanac;
                s.Write();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("saving the council window page", e);
            }
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static readonly Section[] Sections = (Section[])System.Enum.GetValues(typeof(Section));
        /// <summary>One width per page of the widest section.</summary>
        private static readonly float[] subTabWidths = new float[System.Enum.GetValues(typeof(Tab)).Length];
        // Tab labels carry counts that take a little work to find; they're refreshed twice a second.
        private static readonly Dictionary<int, string> labels = new Dictionary<int, string>();
        private static float labelsAt = -1f;
        private readonly List<WindowTab> sectionTabs = new List<WindowTab>();

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            var comp = CouncilGameComponent.Instance;
            var current = SectionOf(tab);
            if (Time.realtimeSinceStartup - labelsAt > 0.5f)
            {
                labels.Clear();
                labelsAt = Time.realtimeSinceStartup;
            }

            // The sections, as the game's tabs with their icons, and the other windows in the corner.
            sectionTabs.Clear();
            foreach (var section in Sections)
            {
                var s = section;
                sectionTabs.Add(new WindowTab(SectionText(s, comp, map), SectionIcon(s), current == s, () => OpenSection(s)));
            }
            var body = WindowTabs.Draw(inRect, sectionTabs, RimfolkWindow.Council);
            var content = body.ContractedBy(12f);

            // The optional next and previous page keys (Options, Keyboard, Rimfolk).
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0) Step(step);

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.Council, "CC_NoGame".Translate());
                return;
            }

            // The section's sub-tabs, as a row of flat tabs along the top; squeezed evenly when the window is narrow.
            var subTabs = TabsOf(current);
            if (subTabs.Length > 1)
            {
                float total = 0f;
                for (int i = 0; i < subTabs.Length; i++)
                {
                    var t = subTabs[i];
                    subTabWidths[i] = Mathf.Max(110f, UiKit.TabButtonWidth(SubTabText(t, comp), TabIcon(t)));
                    total += subTabWidths[i] + (i > 0 ? 4f : 0f);
                }
                float even = (content.width - 4f * (subTabs.Length - 1)) / subTabs.Length;
                float x = content.x;
                for (int i = 0; i < subTabs.Length; i++)
                {
                    var t = subTabs[i];
                    float w = total > content.width ? even : subTabWidths[i];
                    if (UiKit.TabButton(new Rect(x, content.y, w, 28f), SubTabText(t, comp), TabIcon(t), t == tab, TabTipText(t)))
                        SetTab(t);
                    x += w + 4f;
                }
                UiKit.Divider(content.x, content.y + 30f, content.width);
                content = new Rect(content.x, content.y + 38f, content.width, content.height - 38f);
            }

            // The council's own pages and the calendar, with more than one colony: whose they are, and the way to another's.
            if (tab == Tab.Decrees || tab == Tab.Ambition || tab == Tab.Dilemmas || tab == Tab.Calendar)
            {
                float named = ColonyView.Header(new Rect(content.x, content.y, content.width, 26f));
                if (named > 0f) content = new Rect(content.x, content.y + named + 6f, content.width, content.height - named - 6f);
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            switch (tab)
            {
                case Tab.Week: WeekPanel.Draw(content, comp, map); break;
                case Tab.Overview: OverviewPanel.Draw(content, comp, map); break;
                case Tab.Everyone: PeopleTable.Draw(content, comp); break;
                case Tab.Dilemmas: DilemmasPanel.Draw(content, comp); break;
                case Tab.Priorities: PrioritiesPanel.Draw(content, map); break;
                case Tab.Roster: PeoplePanel.Draw(content, comp); break;
                case Tab.Bonds: SocialPanel.Draw(content, comp); break;
                case Tab.Faiths: FaithsPanel.Draw(content, comp); break;
                case Tab.Wishes: WishesPanel.Draw(content, comp); break;
                case Tab.Decrees: DecreePanel.Draw(content, comp); break;
                case Tab.Ambition: AmbitionPanel.Draw(content, comp, map); break;
                case Tab.Ways: WaysPanel.Draw(content, comp); break;
                case Tab.Moods: MoodsPanel.Draw(content); break;
                case Tab.Chronicle: ChroniclePanel.Draw(content, comp); break;
                case Tab.Dreams: HistoryPanels.DrawDreams(content, comp); break;
                case Tab.Journals: HistoryPanels.DrawJournals(content, comp); break;
                case Tab.Calendar: CalendarPanel.Draw(content); break;
                case Tab.Almanac: AlmanacPanel.Draw(content); break;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void SetTab(Tab t)
        {
            restored = true;
            tab = t;
            lastInSection[SectionOf(t)] = t;
        }

        private void OpenSection(Section s) => SetTab(lastInSection.TryGetValue(s, out var t) ? t : TabsOf(s)[0]);

        /// <summary>The next or previous page, through every section in order (the page keys).</summary>
        private void Step(int dir)
        {
            var all = new List<Tab>();
            foreach (var s in Sections) all.AddRange(TabsOf(s));
            int i = all.IndexOf(tab);
            SetTab(all[(i + dir + all.Count) % all.Count]);
        }

        /// <summary>A page's tooltip: its shortcut, when the player gave it one; else none (the label says it all).</summary>
        private static string TabTip(Tab t)
        {
            string key = CouncilKeys.KeyFor(t.ToString());
            return key == null ? "" : "CC_Keys_TabTip".Translate(TabName(t.ToString()), key).Resolve();
        }

        // The labels, from the twice-a-second cache; looked up without making a closure every frame.
        private static string SectionText(Section section, CouncilGameComponent comp, Map map)
        {
            int key = (int)section;
            if (!labels.TryGetValue(key, out var s)) labels[key] = s = SectionLabel(section, comp, map);
            return s;
        }

        private static string SubTabText(Tab t, CouncilGameComponent comp)
        {
            int key = 100 + (int)t;
            if (!labels.TryGetValue(key, out var s)) labels[key] = s = SubTabLabel(t, comp);
            return s;
        }

        private static string TabTipText(Tab t)
        {
            int key = 200 + (int)t;
            if (!labels.TryGetValue(key, out var s)) labels[key] = s = TabTip(t);
            return s;
        }

        private static Texture2D SectionIcon(Section s)
        {
            switch (s)
            {
                case Section.Colony: return Icons.Colony;
                case Section.People: return Icons.People;
                case Section.Council: return Icons.Council;
                default: return Icons.Almanac;
            }
        }

        private static Texture2D TabIcon(Tab t)
        {
            switch (t)
            {
                case Tab.Week: return Icons.Week ?? Icons.Overview;
                case Tab.Everyone: return Icons.Table ?? Icons.Roster;
                case Tab.Dilemmas: return Icons.Dilemmas ?? Icons.Decree;
                case Tab.Overview: return Icons.Overview;
                case Tab.Priorities: return Icons.Priorities;
                case Tab.Moods: return Icons.Moods;
                case Tab.Roster: return Icons.Roster;
                case Tab.Bonds: return Icons.Bonds;
                case Tab.Faiths: return Icons.CatIdeology ?? Icons.People;
                case Tab.Wishes: return Icons.Wishes;
                case Tab.Decrees: return Icons.Decrees;
                case Tab.Ambition: return Icons.Ambition;
                case Tab.Ways: return Icons.Ways;
                case Tab.Chronicle: return Icons.Chronicle;
                case Tab.Dreams: return Icons.Dreams;
                case Tab.Journals: return Icons.Journals;
                case Tab.Calendar: return CalendarPanel.Icon ?? Icons.Chronicle;
                default: return Icons.Almanac;
            }
        }

        /// <summary>Section names, with a count when something there wants attention.</summary>
        private static string SectionLabel(Section s, CouncilGameComponent comp, Map map)
        {
            string label = ("CC_Section_" + s).Translate();
            if (comp == null) return label;
            int n = 0;
            switch (s)
            {
                case Section.Colony:
                    n = map != null && Settings.enableAdvisor ? CouncilMapComponent.For(map)?.Signals?.Count(x => x.Level >= AdviceLevel.High) ?? 0 : 0;
                    // Fights the colony knows are coming.
                    n += comp.happenings.raids.Count;
                    break;
                case Section.Council:
                    // Petitions to the council of the colony in view, and the colonists' wishes waiting for an answer.
                    n = (ColonyView.Current?.decrees ?? comp.decrees).requests.Count + OpenWishCount();
                    break;
            }
            return n > 0 ? label + " (" + n + ")" : label;
        }

        private static string SubTabLabel(Tab t, CouncilGameComponent comp)
        {
            switch (t)
            {
                case Tab.Week:
                    int needs = WeekModel.NeedsCount(comp);
                    return needs > 0 ? "CC_Tab_WeekCount".Translate(needs).ToString() : "CC_Tab_Week".Translate().ToString();
                case Tab.Wishes: return "CC_Tab_Wishes".Translate(OpenWishCount());
                case Tab.Decrees:
                    // The colony in view's petitions.
                    int asked = (ColonyView.Current?.decrees ?? comp.decrees).requests.Count;
                    return asked > 0 ? "CC_Tab_DecreesAsked".Translate(asked).ToString() : "CC_Tab_Decrees".Translate().ToString();
                default: return ("CC_Tab_" + t).Translate();
            }
        }

        // ---------------- Where the window opens ----------------

        /// <summary>The page the window shows (or will, opened), by name.</summary>
        internal static string Page => tab.ToString();

        /// <summary>The last page looked at in one of the window's sections (its first page before any).</summary>
        internal static string LastIn(string section)
        {
            if (!System.Enum.TryParse(section, out Section s)) return null;
            return (lastInSection.TryGetValue(s, out var t) ? t : TabsOf(s)[0]).ToString();
        }

        /// <summary>The window is to open on this page (Pages.Open opens it).</summary>
        internal static void Select(string page)
        {
            if (!System.Enum.TryParse(page, out Tab t)) return;
            restored = true;
            // Asked while closed: it opens there rather than on This week. Asked while open: it just turns the page.
            asked = !Pages.Showing(RimfolkWindow.Council);
            tab = t;
            lastInSection[SectionOf(t)] = t;
        }

        /// <summary>Opens the council window on a colonist's page in the People roster.</summary>
        public static void OpenPerson(Pawn p)
        {
            PeoplePanel.Select(p);
            Pages.Open(nameof(Tab.Roster));
        }

        /// <summary>
        /// Opens a page by name ("Decrees", "Wishes", "Ways", "Defenses", "World"...) in whichever of Rimfolk's windows has
        /// it now (UI/Pages.cs): the defense window has the Defenses page, the region window the World page's neighbours.
        /// </summary>
        public static void OpenTab(string name) => Pages.Open(name);

        /// <summary>A page's shortcut key: opens its window on it, or closes the window when that page is already showing.</summary>
        public static void OpenOrClose(string name) => Pages.OpenOrClose(name);

        /// <summary>A section's bottom bar button: opens the window on the section's last page, or closes it when that section is showing.</summary>
        public static void OpenOrCloseSection(string name) => Pages.OpenOrCloseSection(name);

        /// <summary>A page of one of Rimfolk's windows has this name (for the mood hints' "Go to" buttons).</summary>
        public static bool IsPage(string name) => Pages.IsPage(name);

        /// <summary>A page's plain name, for buttons that lead there.</summary>
        public static string TabName(string name) => Pages.Name(name);

        /// <summary>Opens the council window on an almanac page.</summary>
        public static void OpenAlmanac(AlmanacArticleDef article)
        {
            AlmanacPanel.Show(article);
            Pages.Open(nameof(Tab.Almanac));
        }

        private static int OpenWishCount() => CouncilGameComponent.Instance?.OpenWishes.Count() ?? 0;

        // ---------------- Shared helpers ----------------

        internal static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            Text.Font = font;
            float h = Text.CalcHeight(text, width);
            GUI.color = color ?? Color.white;
            Widgets.Label(new Rect(x, y, width, h), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return h;
        }

        internal static void StartPlanning(RoomTemplateDef template, bool findSpot) => Designator_PlanRoom.Begin(template, findSpot);

        internal static void Jump(LookTargets look)
        {
            if (look == null || !look.IsValid) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJumpAndSelect(look.PrimaryTarget);
        }

        internal static string DayString(int tick) => "CC_DayN".Translate(tick / GenDate.TicksPerDay + 1);

        internal static string AgoString(int tick)
        {
            int days = (Find.TickManager.TicksGame - tick) / GenDate.TicksPerDay;
            return days <= 0 ? "CC_Today".Translate().Resolve() : "CC_DaysAgo".Translate(days).Resolve();
        }
    }
}
