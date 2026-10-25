using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The "defense" button in the bottom bar, next to the council's: the Defenses page (UI/DefensePanel.cs) in a window
    /// of its own, the size of the council window. The same page stays on the council window's Colony section; a click
    /// on a raid's warning and the Defenses page key open this one while its button is shown.
    /// </summary>
    public class MainTabWindow_Defenses : MainTabWindow
    {
        protected override float Margin => 12f;

        public override Vector2 RequestedTabSize =>
            new Vector2(Mathf.Min(1010f, UI.screenWidth - 20f), Mathf.Min(720f, UI.screenHeight - 35f - 20f));

        /// <summary>Opens on the side of the screen its button is on.</summary>
        public override MainTabWindowAnchor Anchor
        {
            get
            {
                var visible = DefDatabase<MainButtonDef>.AllDefsListForReading.Where(b => b.buttonVisible).OrderBy(b => b.order).ToList();
                int i = def != null ? visible.IndexOf(def) : -1;
                return i >= 0 && i < visible.Count / 2 ? MainTabWindowAnchor.Left : MainTabWindowAnchor.Right;
            }
        }

        /// <summary>The button is in the bar, so the Defenses page opens here.</summary>
        public static bool Shown => CouncilDefOf.Council_DefenseButton?.buttonVisible ?? false;

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            if (CouncilGameComponent.Instance == null)
            {
                UiKit.EmptyState(inRect, Icons.Defenses ?? Icons.CatDefense, "CC_NoGame".Translate());
                return;
            }
            DefensePanel.Draw(inRect, Find.CurrentMap);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
