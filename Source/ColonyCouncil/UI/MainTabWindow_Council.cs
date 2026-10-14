using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The "Council" tab: priorities, wishes, ambition, build plans and the chronicle.</summary>
    [StaticConstructorOnStartup]
    public class MainTabWindow_Council : MainTabWindow
    {
        /// <summary>Five sections, each with its own sub-tabs.</summary>
        private enum Section
        {
            Colony,
            People,
            Council,
            History,
            Almanac
        }

        private enum Tab
        {
            Overview,
            Priorities,
            Moods,
            World,
            Build,
            Roster,
            Bonds,
            Wishes,
            Decrees,
            Ambition,
            Ways,
            Chronicle,
            Dreams,
            Journals,
            Almanac
        }

        private static Tab tab = Tab.Overview;
        /// <summary>The sub-tab last open in each section, so switching sections comes back to where you were.</summary>
        private static readonly Dictionary<Section, Tab> lastInSection = new Dictionary<Section, Tab>();
        /// <summary>
        /// The page from the settings has been put back (once a session), or a button already asked for another page.
        /// The window opens where the player left it: within a session from the static tab, across sessions from the
        /// settings, which are saved when the window closes on a different page than before.
        /// </summary>
        private static bool restored;

        private static Section SectionOf(Tab t)
        {
            switch (t)
            {
                case Tab.Overview:
                case Tab.Priorities:
                case Tab.Moods:
                case Tab.World:
                case Tab.Build: return Section.Colony;
                case Tab.Roster:
                case Tab.Bonds:
                case Tab.Wishes: return Section.People;
                case Tab.Decrees:
                case Tab.Ambition:
                case Tab.Ways: return Section.Council;
                case Tab.Chronicle:
                case Tab.Dreams:
                case Tab.Journals: return Section.History;
                default: return Section.Almanac;
            }
        }

        private static Tab[] TabsOf(Section s)
        {
            switch (s)
            {
                case Section.Colony: return new[] { Tab.Overview, Tab.Priorities, Tab.Moods, Tab.World, Tab.Build };
                case Section.People: return new[] { Tab.Roster, Tab.Bonds, Tab.Wishes };
                case Section.Council: return new[] { Tab.Decrees, Tab.Ambition, Tab.Ways };
                case Section.History: return new[] { Tab.Chronicle, Tab.Dreams, Tab.Journals };
                default: return new[] { Tab.Almanac };
            }
        }
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);

        public override void PreOpen()
        {
            base.PreOpen();
            RestorePage();
        }

        public override void PostClose()
        {
            base.PostClose();
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
                if (s == null || s.lastCouncilPage.NullOrEmpty() || !System.Enum.TryParse(s.lastCouncilPage, out Tab t)) return;
                tab = t;
                lastInSection[SectionOf(t)] = t;
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
                if (s.lastCouncilPage == page && s.lastAlmanacPage == almanac) return;
                s.lastCouncilPage = page;
                s.lastAlmanacPage = almanac;
                s.Write();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("saving the council window page", e);
            }
        }

        public override Vector2 RequestedTabSize =>
            new Vector2(Mathf.Min(1010f, UI.screenWidth - 20f), Mathf.Min(720f, UI.screenHeight - 35f - 20f));

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static readonly Section[] Sections = (Section[])System.Enum.GetValues(typeof(Section));
        private const float TabIconSize = 18f;
        private static readonly bool[] sectionHasIcon = new bool[Sections.Length];
        private static readonly float[] subTabWidths = new float[8];
        // Tab labels carry counts that take a little work to find; they're refreshed twice a second.
        private static readonly Dictionary<int, string> labels = new Dictionary<int, string>();
        private static float labelsAt = -1f;
        private static string iconPad;

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

            // The sections, as vanilla tabs. Where a tab has room, its label starts with blank space and the
            // section's icon is drawn into it.
            Text.Font = GameFont.Small;
            var body = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
            float overlap = TabDrawer.TabHoriztonalOverlap;
            float tabW = Mathf.Min(200f, (body.width + (Sections.Length - 1) * overlap) / Sections.Length);
            var tabs = new List<TabRecord>(Sections.Length);
            for (int i = 0; i < Sections.Length; i++)
            {
                var section = Sections[i];
                string label = Label((int)section, () => SectionLabel(section, comp, map));
                sectionHasIcon[i] = SectionIcon(section) != null && Text.CalcSize(IconPad + label).x <= tabW - 24f;
                tabs.Add(new TabRecord(sectionHasIcon[i] ? IconPad + label : label, () => OpenSection(section), current == section));
            }
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs, 200f);
            for (int i = 0; i < Sections.Length; i++)
            {
                if (!sectionHasIcon[i]) continue;
                // Where TabDrawer puts each tab, and where it centers the label in it.
                var tr = new Rect(body.x + i * (tabW - overlap), body.y - TabDrawer.TabHeight + 1f, tabW, TabDrawer.TabHeight);
                float left = tr.center.x - Text.CalcSize(tabs[i].label).x / 2f;
                var color = Sections[i] == current ? UiKit.Gold : Mouse.IsOver(tr) ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                UiKit.Icon(new Rect(left + 1f, tr.center.y - TabIconSize / 2f, TabIconSize, TabIconSize), SectionIcon(Sections[i]), color);
            }
            var content = body.ContractedBy(12f);

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
                    subTabWidths[i] = Mathf.Max(110f, UiKit.TabButtonWidth(Label(100 + (int)t, () => SubTabLabel(t, comp)), TabIcon(t)));
                    total += subTabWidths[i] + (i > 0 ? 4f : 0f);
                }
                float even = (content.width - 4f * (subTabs.Length - 1)) / subTabs.Length;
                float x = content.x;
                for (int i = 0; i < subTabs.Length; i++)
                {
                    var t = subTabs[i];
                    float w = total > content.width ? even : subTabWidths[i];
                    if (UiKit.TabButton(new Rect(x, content.y, w, 28f), Label(100 + (int)t, () => SubTabLabel(t, comp)), TabIcon(t), t == tab)) SetTab(t);
                    x += w + 4f;
                }
                UiKit.Divider(content.x, content.y + 30f, content.width);
                content = new Rect(content.x, content.y + 38f, content.width, content.height - 38f);
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            switch (tab)
            {
                case Tab.Overview: OverviewPanel.Draw(content, comp, map); break;
                case Tab.Priorities: PrioritiesPanel.Draw(content, map); break;
                case Tab.Build: BuildPanel.Draw(content, map); break;
                case Tab.Roster: PeoplePanel.Draw(content, comp); break;
                case Tab.Bonds: SocialPanel.Draw(content, comp); break;
                case Tab.Wishes: WishesPanel.Draw(content, comp); break;
                case Tab.Decrees: DecreePanel.Draw(content, comp); break;
                case Tab.Ambition: AmbitionPanel.Draw(content, comp, map); break;
                case Tab.Ways: WaysPanel.Draw(content, comp); break;
                case Tab.Moods: MoodsPanel.Draw(content); break;
                case Tab.World: WorldPanel.Draw(content, comp); break;
                case Tab.Chronicle: ChroniclePanel.Draw(content, comp); break;
                case Tab.Dreams: HistoryPanels.DrawDreams(content, comp); break;
                case Tab.Journals: HistoryPanels.DrawJournals(content, comp); break;
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

        private static string Label(int key, System.Func<string> make)
        {
            if (!labels.TryGetValue(key, out var s)) labels[key] = s = make();
            return s;
        }

        /// <summary>Blank space as wide as a tab icon, measured between two letters (a string of spaces alone may measure as nothing).</summary>
        private static string IconPad
        {
            get
            {
                if (iconPad != null) return iconPad;
                var old = Text.Font;
                Text.Font = GameFont.Small;
                float bare = Text.CalcSize("xx").x;
                string pad = " ";
                while (pad.Length < 12 && Text.CalcSize("x" + pad + "x").x - bare < TabIconSize + 5f) pad += " ";
                Text.Font = old;
                return iconPad = pad;
            }
        }

        private static Texture2D SectionIcon(Section s)
        {
            switch (s)
            {
                case Section.Colony: return Icons.Colony;
                case Section.People: return Icons.People;
                case Section.Council: return Icons.Council;
                case Section.History: return Icons.History;
                default: return Icons.Almanac;
            }
        }

        private static Texture2D TabIcon(Tab t)
        {
            switch (t)
            {
                case Tab.Overview: return Icons.Overview;
                case Tab.Priorities: return Icons.Priorities;
                case Tab.Moods: return Icons.Moods;
                case Tab.World: return Icons.World;
                case Tab.Build: return Icons.Rooms;
                case Tab.Roster: return Icons.Roster;
                case Tab.Bonds: return Icons.Bonds;
                case Tab.Wishes: return Icons.Wishes;
                case Tab.Decrees: return Icons.Decrees;
                case Tab.Ambition: return Icons.Ambition;
                case Tab.Ways: return Icons.Ways;
                case Tab.Chronicle: return Icons.Chronicle;
                case Tab.Dreams: return Icons.Dreams;
                case Tab.Journals: return Icons.Journals;
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
                case Section.People:
                    n = OpenWishCount();
                    break;
                case Section.Council:
                    n = comp.decrees.requests.Count;
                    break;
            }
            return n > 0 ? label + " (" + n + ")" : label;
        }

        private static string SubTabLabel(Tab t, CouncilGameComponent comp)
        {
            switch (t)
            {
                case Tab.Wishes: return "CC_Tab_Wishes".Translate(OpenWishCount());
                case Tab.Decrees:
                    return comp.decrees.requests.Count > 0 ? "CC_Tab_DecreesAsked".Translate(comp.decrees.requests.Count).ToString() : "CC_Tab_Decrees".Translate().ToString();
                case Tab.World:
                    return comp.happenings.raids.Count > 0 ? "CC_Tab_WorldComing".Translate(comp.happenings.raids.Count).ToString() : "CC_Tab_World".Translate().ToString();
                default: return ("CC_Tab_" + t).Translate();
            }
        }

        /// <summary>Opens the council window on a colonist's page in the People roster.</summary>
        public static void OpenPerson(Pawn p)
        {
            restored = true;
            tab = Tab.Roster;
            lastInSection[Section.People] = Tab.Roster;
            PeoplePanel.Select(p);
            var button = CouncilDefOf.Council_MainButton;
            if (button != null && Find.MainTabsRoot.OpenTab != button) Find.MainTabsRoot.SetCurrentTab(button, false);
        }

        /// <summary>Opens the council window on a page by name ("Decrees", "Wishes", "Ways"...).</summary>
        public static void OpenTab(string name)
        {
            if (!System.Enum.TryParse(name, true, out Tab t)) return;
            restored = true;
            tab = t;
            lastInSection[SectionOf(t)] = t;
            var button = CouncilDefOf.Council_MainButton;
            if (button != null && Find.MainTabsRoot.OpenTab != button) Find.MainTabsRoot.SetCurrentTab(button, false);
        }

        /// <summary>A page's plain name, for buttons that lead there.</summary>
        public static string TabName(string name)
        {
            if (!System.Enum.TryParse(name, true, out Tab t)) return name;
            switch (t)
            {
                case Tab.Wishes: return "CC_Mood_WishesTab".Translate();
                case Tab.Decrees: return "CC_Tab_Decrees".Translate();
                default: return ("CC_Tab_" + t).Translate();
            }
        }

        /// <summary>Opens the council window on an almanac page.</summary>
        public static void OpenAlmanac(AlmanacArticleDef article)
        {
            restored = true;
            tab = Tab.Almanac;
            lastInSection[Section.Almanac] = Tab.Almanac;
            AlmanacPanel.Show(article);
            var button = CouncilDefOf.Council_MainButton;
            if (Find.MainTabsRoot.OpenTab != button) Find.MainTabsRoot.SetCurrentTab(button, false);
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
