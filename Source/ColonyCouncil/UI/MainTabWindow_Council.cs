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

        public override Vector2 RequestedTabSize =>
            new Vector2(Mathf.Min(1010f, UI.screenWidth - 20f), Mathf.Min(720f, UI.screenHeight - 35f - 20f));

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public override void DoWindowContents(Rect inRect)
        {
            var map = Find.CurrentMap;
            var comp = CouncilGameComponent.Instance;
            var current = SectionOf(tab);
            var tabs = new List<TabRecord>();
            foreach (Section s in System.Enum.GetValues(typeof(Section)))
            {
                var section = s;
                tabs.Add(new TabRecord(SectionLabel(section, comp, map), () => OpenSection(section), current == section));
            }
            var body = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs, 200f);
            var content = body.ContractedBy(12f);

            if (comp == null)
            {
                Widgets.Label(content, "CC_NoGame".Translate());
                return;
            }

            // The section's sub-tabs, as a row of buttons along the top.
            var subTabs = TabsOf(current);
            if (subTabs.Length > 1)
            {
                float x = content.x;
                foreach (var t in subTabs)
                {
                    string label = SubTabLabel(t, comp);
                    float w = Mathf.Max(110f, Text.CalcSize(label).x + 24f);
                    var r = new Rect(x, content.y, w, 28f);
                    if (t == tab) Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.12f));
                    Widgets.DrawHighlightIfMouseover(r);
                    GUI.color = t == tab ? Color.white : Faint;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(r, label);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    if (t == tab) Widgets.DrawLineHorizontal(r.x, r.yMax - 1f, r.width);
                    if (Widgets.ButtonInvisible(r)) SetTab(t);
                    x += w + 4f;
                }
                Widgets.DrawLineHorizontal(content.x, content.y + 30f, content.width);
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
            tab = t;
            lastInSection[SectionOf(t)] = t;
        }

        private void OpenSection(Section s) => SetTab(lastInSection.TryGetValue(s, out var t) ? t : TabsOf(s)[0]);

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
            tab = Tab.Almanac;
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
