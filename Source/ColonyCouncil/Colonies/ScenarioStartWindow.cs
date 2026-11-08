using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A scenario's start for the new colony (release 3, Colonies/ScenarioStarts.cs), from the founding window: the loaded
    /// scenarios on the left (the game's, its DLCs', Rimfolk's and other mods', this game's own, the player's own and the
    /// workshop's the game holds), and for the one picked, its summary and what comes for the new colony alone, what
    /// isn't carried because it changes the whole game, and what never comes. A whole new scenario on its own page is what
    /// a new game is for, and the window says so.
    /// </summary>
    public class Dialog_ScenarioStart : FramedWindow
    {
        private readonly Action<string> chosen;
        private readonly bool oldLeftBehind, partyGoes;
        private readonly List<ScenarioStarts.Entry> list;
        private string selected;
        private ScenarioStart read;
        private Vector2 scrollLeft, scrollRight;
        private float leftH = 400f, rightH = 400f;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(960f, UI.screenWidth - 40f), Mathf.Min(720f, UI.screenHeight - 40f));

        private static string title;

        protected override string FrameTitle => title ??= "CC_ScenStart_ListTitle".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.Colony;

        /// <summary>In Rimfolk's frame (key "ScenarioStart"), its title in the title bar; wider, the list takes more of it.</summary>
        public Dialog_ScenarioStart(string current, bool oldLeftBehind, bool partyGoes, Action<string> chosen) : base("ScenarioStart")
        {
            this.chosen = chosen;
            this.oldLeftBehind = oldLeftBehind;
            this.partyGoes = partyGoes;
            list = ScenarioStarts.Loaded();
            selected = list.Any(e => e.key == current) ? current : list.Count > 0 ? list[0].key : null;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        private ScenarioStart Selected
        {
            get
            {
                var e = list.FirstOrDefault(x => x.key == selected);
                if (e.scenario == null) return null;
                if (read?.scenario != e.scenario) read = ScenarioStarts.Read(e.scenario, e.key);
                return read;
            }
        }

        protected override void DoFramedContents(Rect inRect)
        {
            try
            {
                float top = TitleAndLine(inRect, FrameTitle, "CC_ScenStart_ListLine".Translate().Resolve(), GameFont.Tiny);
                const float bottomH = 44f;
                float leftW = Mathf.Clamp(inRect.width * 0.32f, 220f, inRect.width >= FramedWindowsMath.JourneyWideFrom ? 420f : 320f);
                var left = new Rect(inRect.x, top, leftW, inRect.height - (top - inRect.y) - bottomH);
                var right = new Rect(left.xMax + 14f, top, inRect.width - leftW - 14f, left.height);
                DrawList(left);
                DrawScenario(right);
                DrawBottom(new Rect(inRect.x, inRect.yMax - bottomH + 8f, inRect.width, bottomH - 8f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawList(Rect rect)
        {
            UiKit.Section(rect);
            var outer = rect.ContractedBy(4f);
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(leftH, outer.height));
            Widgets.BeginScrollView(outer, ref scrollLeft, view);
            float y = 0f;
            int group = -1;
            foreach (var e in list)
            {
                if (e.from != group)
                {
                    group = e.from;
                    if (y > 0f) y += 6f;
                    UiKit.LabelFit(new Rect(4f, y, view.width - 8f, 22f), ("CC_ScenStart_From_" + group).Translate().Resolve(), UiKit.Gold, GameFont.Tiny);
                    y += 22f;
                }
                var row = new Rect(0f, y, view.width, 26f);
                if (UiKit.OwnLook) UiKit.ListRow(row, e.key == selected);
                else
                {
                    if (e.key == selected) Widgets.DrawHighlightSelected(row);
                    Widgets.DrawHighlightIfMouseover(row);
                }
                UiKit.LabelFit(new Rect(8f, y, view.width - 12f, 26f), e.scenario.name ?? "?", Color.white);
                if (Widgets.ButtonInvisible(row))
                {
                    selected = e.key;
                    scrollRight = Vector2.zero;
                }
                y += 26f;
            }
            if (list.Count == 0) UiKit.LabelFit(new Rect(4f, y, view.width - 8f, 22f), "CC_ScenStart_NoneLoaded".Translate().Resolve(), UiKit.Faint, GameFont.Tiny);
            leftH = y + 10f;
            Widgets.EndScrollView();
        }

        private void DrawScenario(Rect rect)
        {
            var st = Selected;
            if (st == null) return;
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rightH, rect.height));
            Widgets.BeginScrollView(rect, ref scrollRight, view);
            float y = 0f, w = view.width;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, w, 32f), st.Name);
            Text.Font = GameFont.Small;
            y += 34f;
            if (!st.scenario.summary.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, st.scenario.summary, UiKit.Faint, GameFont.Small) + 10f;
            var (carried, not, never) = ScenarioStarts.Lines(st, oldLeftBehind, partyGoes);
            float Section(string head, Texture2D icon, List<string> lines, Color color)
            {
                if (lines.Count == 0) return 0f;
                float h = UiKit.SectionTitle(0f, y, w, head, icon) + 2f;
                foreach (var l in lines) h += UiKit.Paragraph(8f, y + h, w - 12f, "- " + l, color, GameFont.Small) + 2f;
                return h + 8f;
            }
            y += Section("CC_ScenStart_Carried".Translate().Resolve(), Icons.Colony, carried, Color.white);
            y += Section("CC_ScenStart_NotCarried".Translate().Resolve(), Icons.World, not, UiKit.Faint);
            y += Section("CC_ScenStart_Never".Translate().Resolve(), Icons.Warning, never, UiKit.Warn);
            y += UiKit.Paragraph(0f, y, w, "CC_ScenStart_NewGame".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            rightH = y + 10f;
            Widgets.EndScrollView();
        }

        private void DrawBottom(Rect rect)
        {
            var confirm = new Rect(rect.xMax - 220f, rect.y, 220f, rect.height);
            bool can = Selected != null;
            if (UiKit.Button(confirm, "CC_ScenStart_Choose".Translate(), ButtonKind.Primary, can))
            {
                chosen?.Invoke(selected);
                Close();
                return;
            }
            if (UiKit.Button(new Rect(confirm.x - 170f, rect.y, 160f, rect.height), "CloseButton".Translate())) Close();
        }
    }
}
