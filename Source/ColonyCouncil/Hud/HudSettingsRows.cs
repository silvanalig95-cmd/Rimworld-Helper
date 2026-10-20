using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The rows of buttons under "On-screen panels" in Rimfolk's settings (UI/SettingsWindow.cs draws the rest of the
    /// page): a button per panel that opens its options, the same as its gear on the map (four to a row, so two rows),
    /// and one that puts every panel and the hover card back as Rimfolk ships them.
    /// </summary>
    public static class HudSettingsRows
    {
        private const int PerRow = 4;

        public static void Draw(string rowId, string label, float x, float y, float width, float height, bool on)
        {
            if (rowId == "hudReset")
            {
                var one = new Rect(x, y, Mathf.Min(width, 360f), height);
                if (Widgets.ButtonText(one, UiKit.Fit(label, one.width - 12f)))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Pnl_ResetConfirm".Translate(), HudLayout.ResetAll, true));
                TooltipHandler.TipRegion(one, "CC_Settings_HudReset_Tip".Translate());
                return;
            }
            // Four panels on the first row, the rest on the second ("hudPanelOptions2"), each button the same width.
            var ids = HudLayout.Ids;
            int from = rowId == "hudPanelOptions2" ? PerRow : 0;
            int to = Mathf.Min(ids.Length, from + PerRow);
            float gap = 6f;
            float bw = (width - gap * (PerRow - 1)) / PerRow;
            for (int i = from; i < to; i++)
            {
                var b = new Rect(x + (i - from) * (bw + gap), y, bw, height);
                string name = HudLayout.Label(ids[i]);
                bool shown = HudLayout.State(ids[i]).on;
                if (!shown) GUI.color = UiKit.Dim;
                bool click = Widgets.ButtonText(b, UiKit.Fit(name, bw - 12f), true, true, on);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(b, "CC_Pnl_SettingsButtonTip".Translate(name, (shown ? "CC_SetUI_On" : "CC_SetUI_Off").Translate()).Resolve());
                if (click && on) Dialog_HudPanel.Open(ids[i]);
            }
        }
    }
}
