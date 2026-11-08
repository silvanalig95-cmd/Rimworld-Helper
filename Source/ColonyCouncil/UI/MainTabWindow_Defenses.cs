using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The "defense" button in the bottom bar: the Defenses page (UI/DefensePanel.cs) in its own window, the size and
    /// place of Rimfolk's other windows, with their icons in its top right corner. The page is only here now: a click
    /// on a raid's warning, a next step about the defenses and the Defenses page key all open this window, whether or
    /// not its button shows on the bar. Since UI 5 (docs/design/interface.md, "one look") it reads as the other windows
    /// do: its one view drawn with the view header (title, one line, the stat and the "?" to its Almanac page) and the
    /// drawer, so a colonist or a people named on the page opens its card beside it.
    /// </summary>
    public class MainTabWindow_Defenses : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.Defense;

        private const string Page = "Defenses";

        private static readonly List<WindowTab> tabs = new List<WindowTab>();
        private static string label;
        /// <summary>The view's header extras: no modes (one way of looking), and the "?" to the defense planner's Almanac page.</summary>
        private static readonly ViewHeadModel head = new ViewHeadModel { article = PagesMath.ArticleOf(Page) };
        private static readonly Action<Rect> drawView = DrawView;
        private static readonly Action<Rect> drawPage = r => DefensePanel.Draw(r, Find.CurrentMap);

        private static Texture2D Icon => Icons.Defenses ?? Icons.CatDefense;

        protected override void DrawContents(Rect inRect, bool cornerInTitle) => DrawDefenses(inRect, cornerInTitle);

        /// <summary>
        /// The defense window's contents, the tab's and the floating window's alike: its one view as a tab (the corner beside
        /// it, or in the title bar with <paramref name="cornerInTitle"/>), the Defenses page with its drawer under it.
        /// </summary>
        internal static void DrawDefenses(Rect inRect, bool cornerInTitle)
        {
            Text.Font = GameFont.Small;
            GUI.color = UiKit.Ink;
            label ??= "CC_Tab_Defenses".Translate();
            tabs.Clear();
            tabs.Add(new WindowTab(label, Icon, true, null));
            var body = WindowTabs.DrawFor(inRect, tabs, RimfolkWindow.Defense, cornerInTitle);
            var content = body.ContractedBy(12f);
            if (CouncilGameComponent.Instance == null)
            {
                UiKit.EmptyState(content, Icon, "CC_NoGame".Translate());
                return;
            }
            try
            {
                Drawer.Around(RimfolkWindow.Defense, content, drawView);
                Nav.Observe(RimfolkWindow.Defense, "Defenses", Drawer.Current(RimfolkWindow.Defense));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UiKit.Ink;
            }
        }

        /// <summary>The page as a view: its own header takes the "?", or the window gives it one when it draws none (no home map).</summary>
        private static void DrawView(Rect rect) => ViewHead.Draw(rect, Page, label, Icon, head, drawPage);
    }
}
