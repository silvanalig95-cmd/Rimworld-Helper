using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The two rows of buttons under "On-screen panels" in Rimfolk's settings (UI/SettingsWindow.cs draws the rest of
    /// the page): a button per panel that opens its options, the same as its gear on the map, and one that puts every
    /// panel and the hover card back as Rimfolk ships them.
    /// </summary>
    public static class HudSettingsRows
    {
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
            var ids = HudLayout.Ids;
            float gap = 6f;
            float bw = (width - gap * (ids.Length - 1)) / ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                var b = new Rect(x + i * (bw + gap), y, bw, height);
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
