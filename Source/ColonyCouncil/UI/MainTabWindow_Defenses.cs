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
    /// not its button shows on the bar.
    /// </summary>
    public class MainTabWindow_Defenses : RimfolkTabWindow
    {
        public override RimfolkWindow Which => RimfolkWindow.Defense;

        private readonly List<WindowTab> tabs = new List<WindowTab>();
        private string label;

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            label ??= "CC_Tab_Defenses".Translate();
            tabs.Clear();
            tabs.Add(new WindowTab(label, Icons.Defenses ?? Icons.CatDefense, true, null));
            var body = WindowTabs.Draw(inRect, tabs, RimfolkWindow.Defense);
            var content = body.ContractedBy(12f);
            if (CouncilGameComponent.Instance == null)
            {
                UiKit.EmptyState(content, Icons.Defenses ?? Icons.CatDefense, "CC_NoGame".Translate());
                return;
            }
            DefensePanel.Draw(content, Find.CurrentMap);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
