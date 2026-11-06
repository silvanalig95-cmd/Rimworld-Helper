using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council button's window: the colony's own life in four views, one row of tabs (docs/design/interface.md,
    /// decision 5). This week: what waits on the player. People: the colonists as a table, their moods, bonds,
    /// ideoligions and houses, each a mode of the one view; a colonist's card opens in the drawer beside the list, with
    /// their dreams and journal in it. Council: offices and decrees, the ambition and its meetings, the colony's ways,
    /// wishes and petitions, dilemmas. Colony: the overview, the calendar and the chronicle (with dreams and journals as
    /// its filters). Every view's header carries its modes and a "?" to its Almanac article, which opens in the Almanac's
    /// own window. Which page is in which view is UI/PagesMath.cs; a card opened from any name here opens in this
    /// window's drawer (UI/Cards/Drawer.cs); the region, defense and planning windows are one click away in the corner.
    /// </summary>
    [StaticConstructorOnStartup]
    public class MainTabWindow_Council : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.Council;

        private static readonly List<string> Views = PagesMath.ViewsOf(RimfolkWindow.Council);
        private static readonly List<string> Order = PagesMath.PagesIn(RimfolkWindow.Council);

        private static string page = "Week";
        /// <summary>A button asked for a page (OpenTab, OpenPerson...): the window opens there, not on This week.</summary>
        private static bool asked;
        /// <summary>The mode last open in each view, so going back to a view comes back to where you were.</summary>
        private static readonly Dictionary<string, string> lastInView = new Dictionary<string, string>();
        /// <summary>
        /// The page from the settings has been put back (once a session), or a button already asked for another page.
        /// The window opens where the player left it: within a session from the static page, across sessions from the
        /// settings, which are saved when the window closes on a different page than before.
        /// </summary>
        private static bool restored;

        /// <summary>A page shows now: the ideoligions' only while "Faith in the colony" is on.</summary>
        private static bool Shown(string p) => p != "Faiths" || Faiths.On;

        private static string ViewOf(string p) => PagesMath.SectionOf(p) ?? "Week";

        // Each view's modes as they show (made once per setting of the faiths' switch: asked every frame).
        private static readonly Dictionary<string, List<string>> withFaiths = MakeModes(true), withoutFaiths = MakeModes(false);

        private static Dictionary<string, List<string>> MakeModes(bool faiths)
        {
            var d = new Dictionary<string, List<string>>();
            foreach (var v in Views) d[v] = PagesMath.ModesOf(RimfolkWindow.Council, v).Where(p => faiths || p != "Faiths").ToList();
            return d;
        }

        private static List<string> ModesOf(string view) => (Faiths.On ? withFaiths : withoutFaiths)[view];

        public override void PreOpen()
        {
            base.PreOpen();
            RestorePage();
            // "Council window opens on: This week", unless a button asked for another page.
            if (!asked && Settings?.councilOpensOn == CouncilOpensOn.ThisWeek) SetPage("Week");
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
                // An old page name reads as where it went ("Roster" is People's table); one that went to another window
                // (World, Defenses, Priorities, the Almanac) isn't one here: the window opens as it would.
                string p = s == null || s.lastCouncilPage.NullOrEmpty() ? null : PagesMath.Resolve(s.lastCouncilPage);
                if (p == null || !Order.Contains(p) || asked) return;
                SetPage(p);
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
                // The pages' filters and sort orders (UI/PageMemory.cs) go into the same write.
                bool pages = PageMemory.Flush();
                if (s.lastCouncilPage == page && !pages) return;
                s.lastCouncilPage = page;
                s.Write();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("saving the council window page", e);
            }
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        // Tab and mode labels carry counts that take a little work to find; they're refreshed twice a second.
        private static readonly Dictionary<string, string> viewLabels = new Dictionary<string, string>(), viewTips = new Dictionary<string, string>(),
            modeLabels = new Dictionary<string, string>(), modeTips = new Dictionary<string, string>();
        private static readonly Dictionary<string, float> headsAt = new Dictionary<string, float>();
        private static float labelsAt = -1f;
        private readonly List<WindowTab> viewTabs = new List<WindowTab>();
        private static readonly Dictionary<string, ViewHeadModel> heads = new Dictionary<string, ViewHeadModel>();
        private static readonly System.Action<Rect> drawView = DrawView;
        private static readonly System.Action<Rect> drawPage = DrawPage;
        private static CouncilGameComponent comp;
        private static Map map;

        public override void DoWindowContents(Rect inRect)
        {
            map = Find.CurrentMap;
            comp = CouncilGameComponent.Instance;
            if (Time.realtimeSinceStartup - labelsAt > 0.5f)
            {
                viewLabels.Clear();
                viewTips.Clear();
                modeLabels.Clear();
                modeTips.Clear();
                labelsAt = Time.realtimeSinceStartup;
            }
            if (!Shown(page)) SetPage(ModesOf(ViewOf(page))[0]);
            string current = ViewOf(page);

            // The views, as the game's tabs with their icons, and the other windows in the corner.
            viewTabs.Clear();
            foreach (var view in Views)
            {
                var v = view;
                viewTabs.Add(new WindowTab(Get(viewLabels, v, ViewLabel), ViewIcon(v), current == v, () => OpenView(v), Get(viewTips, v, ViewTip)));
            }
            var body = WindowTabs.Draw(inRect, viewTabs, RimfolkWindow.Council);
            var content = body.ContractedBy(12f);

            // The optional next and previous page keys (Options, Keyboard, Rimfolk).
            int step = comp != null ? CouncilKeys.PageStep() : 0;
            if (step != 0) Step(step);

            if (comp == null)
            {
                UiKit.EmptyState(content, Icons.Council, "CC_NoGame".Translate());
                return;
            }
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            // The view, with the drawer beside or over it where a card is open.
            Drawer.Around(RimfolkWindow.Council, content, drawView);
            Nav.Observe(RimfolkWindow.Council, page, Drawer.Current(RimfolkWindow.Council));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// <summary>The view in its rect: whose colony it is (with more than one), then the page with its header's modes and "?".</summary>
        private static void DrawView(Rect rect)
        {
            // The council's own pages and the calendar, with more than one colony: whose they are, and the way to another's.
            if (page == "Decrees" || page == "Ambition" || page == "Dilemmas" || page == "Calendar")
            {
                float named = ColonyView.Header(new Rect(rect.x, rect.y, rect.width, 26f));
                if (named > 0f) rect = new Rect(rect.x, rect.y + named + 6f, rect.width, rect.height - named - 6f);
            }
            ViewHead.Draw(rect, page, Get(names, page, ModeName), TabIcon(page), Head(ViewOf(page)), drawPage);
        }

        private static void DrawPage(Rect content)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            switch (page)
            {
                case "Week": WeekPanel.Draw(content, comp, map); break;
                case "Table": PeopleTable.Draw(content, comp); break;
                case "Moods": MoodsPanel.Draw(content); break;
                case "Bonds": SocialPanel.Draw(content, comp); break;
                case "Faiths": FaithsPanel.Draw(content, comp); break;
                case "Renown": RenownPage.Draw(content, comp); break;
                case "Decrees": DecreePanel.Draw(content, comp); break;
                case "Ambition": AmbitionPanel.Draw(content, comp, map); break;
                case "Ways": WaysPanel.Draw(content, comp); break;
                case "Wishes": WishesPanel.Draw(content, comp); break;
                case "Dilemmas": DilemmasPanel.Draw(content, comp); break;
                case "Overview": OverviewPanel.Draw(content, comp, map); break;
                case "Calendar": CalendarPanel.Draw(content); break;
                case "Chronicle": ChroniclePanel.Draw(content, comp); break;
            }
        }

        /// <summary>A view's header extras: its modes as they show, the one lit, and the "?" to the page's article.</summary>
        private static ViewHeadModel Head(string view)
        {
            if (!heads.TryGetValue(view, out var h))
            {
                var v = view;
                heads[view] = h = new ViewHeadModel { pick = i => PickMode(v, i) };
            }
            var modes = ModesOf(view);
            // The labels again only when the twice-a-second labels were (their counts), or the modes changed.
            if (h.labels.Count != modes.Count || !headsAt.TryGetValue(view, out float at) || at != labelsAt)
            {
                headsAt[view] = labelsAt;
                h.labels.Clear();
                h.tips.Clear();
                foreach (var m in modes)
                {
                    h.labels.Add(Get(modeLabels, m, ModeLabel));
                    h.tips.Add(Get(modeTips, m, TabTip));
                }
            }
            h.lit = modes.IndexOf(page);
            h.article = PagesMath.ArticleOf(page);
            return h;
        }

        private static void PickMode(string view, int i)
        {
            var modes = ModesOf(view);
            if (i >= 0 && i < modes.Count) SetPage(modes[i]);
        }

        private static void SetPage(string p)
        {
            restored = true;
            page = p;
            lastInView[ViewOf(p)] = p;
        }

        private static void OpenView(string view) => SetPage(LastIn(view));

        /// <summary>The next or previous page, through every view's modes in order (the page keys).</summary>
        private static void Step(int dir)
        {
            var all = Order.Where(Shown).ToList();
            int i = all.IndexOf(page);
            SetPage(all[(i + dir + all.Count) % all.Count]);
        }

        /// <summary>A page's tooltip: its shortcut, when the player gave it one; else none (the label says it all).</summary>
        private static string TabTipOf(string p)
        {
            string key = CouncilKeys.KeyFor(p);
            return key == null ? "" : "CC_Keys_TabTip".Translate(TabName(p), key).Resolve();
        }

        // The labels, from the twice-a-second caches (the methods are made into delegates once).
        private static readonly System.Func<string, string> ViewLabel = ViewLabelOf, ViewTip = ViewTipOf, ModeLabel = ModeLabelOf, TabTip = TabTipOf, ModeName = Pages.ModeName;
        /// <summary>Each page's plain name (the header a page with none of its own gets), made once.</summary>
        private static readonly Dictionary<string, string> names = new Dictionary<string, string>();

        private static string Get(Dictionary<string, string> cache, string key, System.Func<string, string> make)
        {
            if (!cache.TryGetValue(key, out var s)) cache[key] = s = make(key);
            return s;
        }

        private static string ViewTipOf(string view)
        {
            string key = "CC_View_Tip_" + view;
            string tip = key.CanTranslate() ? key.Translate().Resolve() : null;
            // The keys bound to its pages.
            foreach (var p in PagesMath.ModesOf(RimfolkWindow.Council, view))
            {
                string bound = CouncilKeys.KeyFor(p);
                if (bound != null) tip = (tip == null ? "" : tip + "\n\n") + "CC_Keys_TabTip".Translate(TabName(p), bound).Resolve();
            }
            return tip;
        }

        private static Texture2D ViewIcon(string view)
        {
            switch (view)
            {
                case "Week": return Icons.Week ?? Icons.Overview;
                case "People": return Icons.People;
                case "Council": return Icons.Council;
                default: return Icons.Colony;
            }
        }

        private static Texture2D TabIcon(string p)
        {
            switch (p)
            {
                case "Week": return Icons.Week ?? Icons.Overview;
                case "Table": return Icons.Table ?? Icons.Roster;
                case "Dilemmas": return Icons.Dilemmas ?? Icons.Decree;
                case "Overview": return Icons.Overview;
                case "Moods": return Icons.Moods;
                case "Bonds": return Icons.Bonds;
                case "Faiths": return Icons.CatIdeology ?? Icons.People;
                case "Renown": return Icons.People;
                case "Wishes": return Icons.Wishes;
                case "Decrees": return Icons.Decrees;
                case "Ambition": return Icons.Ambition;
                case "Ways": return Icons.Ways;
                case "Chronicle": return Icons.Chronicle;
                case "Calendar": return CalendarPanel.Icon ?? Icons.Chronicle;
                default: return Icons.Council;
            }
        }

        /// <summary>A view's name on its tab, with a count when something there waits on the player.</summary>
        private static string ViewLabelOf(string view)
        {
            string label = Pages.ViewName(RimfolkWindow.Council, view);
            if (comp == null) return label;
            switch (view)
            {
                case "Week":
                    int needs = WeekModel.NeedsCount(comp);
                    return needs > 0 ? "CC_Tab_WeekCount".Translate(needs).ToString() : label;
                case "Council":
                    // Petitions to the council of the colony in view, and the colonists' wishes waiting for an answer.
                    int n = (ColonyView.Current?.decrees ?? comp.decrees).requests.Count + OpenWishCount();
                    return n > 0 ? label + " (" + n + ")" : label;
                default: return label;
            }
        }

        /// <summary>A mode's label on its button, with a count where its page waits on the player.</summary>
        private static string ModeLabelOf(string p)
        {
            string label = Pages.ModeName(p);
            if (comp == null) return label;
            int n = 0;
            switch (p)
            {
                case "Wishes": n = OpenWishCount(); break;
                case "Decrees": n = (ColonyView.Current?.decrees ?? comp.decrees).requests.Count; break;
            }
            return n > 0 ? label + " (" + n + ")" : label;
        }

        // ---------------- Where the window opens ----------------

        /// <summary>The page the window shows (or will, opened), by name.</summary>
        internal static string Page => page;

        /// <summary>The mode last looked at in one of the window's views (its first before any).</summary>
        internal static string LastIn(string view)
        {
            if (view == null || !Views.Contains(view)) return null;
            return lastInView.TryGetValue(view, out var p) && Shown(p) ? p : ModesOf(view)[0];
        }

        /// <summary>The window is to open on this page (Pages.Open opens it).</summary>
        internal static void Select(string p)
        {
            if (!Order.Contains(p)) return;
            // Asked while closed: it opens there rather than on This week. Asked while open: it just turns the page.
            asked = !Pages.Showing(RimfolkWindow.Council);
            SetPage(p);
        }

        /// <summary>Opens the council window on People's table with a colonist's card in its drawer.</summary>
        public static void OpenPerson(Pawn p)
        {
            if (p == null) return;
            Drawer.Open(RimfolkWindow.Council, ThingRefs.Of(p));
            Pages.Open("Table");
        }

        /// <summary>
        /// Opens a page by name ("Decrees", "Wishes", "Ways", "Defenses", "World"...) in whichever of Rimfolk's windows has
        /// it now (UI/Pages.cs): the defense window has the Defenses page, the region window the World page's neighbours.
        /// </summary>
        public static void OpenTab(string name) => Pages.Open(name);

        /// <summary>A page's shortcut key: opens its window on it, or closes the window when that page is already showing.</summary>
        public static void OpenOrClose(string name) => Pages.OpenOrClose(name);

        /// <summary>A section's bottom bar button: opens the window on the view's last mode, or closes it when that view is showing.</summary>
        public static void OpenOrCloseSection(string name) => Pages.OpenOrCloseSection(name);

        /// <summary>A page of one of Rimfolk's windows has this name (for the mood hints' "Go to" buttons).</summary>
        public static bool IsPage(string name) => Pages.IsPage(name);

        /// <summary>A page's plain name, for buttons that lead there.</summary>
        public static string TabName(string name) => Pages.Name(name);

        /// <summary>Opens the Almanac's own window on a page (the old way to the Almanac, from every caller).</summary>
        public static void OpenAlmanac(AlmanacArticleDef article) => Window_Almanac.Open(article);

        private static int OpenWishCount() => CouncilGameComponent.Instance is CouncilGameComponent c ? c.OpenWishes.Count(c.LeadsPetition) : 0;

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
