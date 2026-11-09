using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The ways into a colonist's card from the game's own interface (UI/ColonistCard/ColonistCardWindow.cs), each a deeper
    /// game hook with its own setting and each leaving the game's own clicks as they are:
    /// <list type="bullet">
    /// <item>the colonist bar: Ctrl and a click on a portrait (a plain click still selects, a double-click still jumps, a
    /// right-drag still reorders, Shift and a click still adds to the selection);</item>
    /// <item>the inspect pane: a small card button by a colonist's name, left of the game's own buttons there.</item>
    /// </list>
    /// Rimfolk's own windows open it wherever they opened a colonist's card (Cards.Open), and an event card has a button on
    /// each colonist it names.
    /// </summary>
    public static class ColonistCardEntry
    {
        /// <summary>"Ctrl-click on the colonist bar opens the card" (on), with the card on and the deeper hooks.</summary>
        public static bool BarOn => RimfolkPatches.On && ColonistCardWindow.On && (ColonyCouncilMod.Settings?.colonistCardBar ?? true);

        /// <summary>"A card button on the inspect pane" (on), with the card on and the deeper hooks.</summary>
        public static bool PaneOn => RimfolkPatches.On && ColonistCardWindow.On && (ColonyCouncilMod.Settings?.colonistCardPane ?? true);

        /// <summary>
        /// A mouse-down with Ctrl (Command on a Mac) on a colonist's portrait in the bar: their card opens, and the press is
        /// taken so the bar neither selects nor starts a drag from it. Every other event passes untouched.
        /// </summary>
        public static void BarClick(Rect rect, Pawn colonist)
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.MouseDown || ev.button != 0 || !(ev.control || ev.command) || !BarOn) return;
            if (!ColonistCardWindow.Fits(colonist) || !Mouse.IsOver(rect)) return;
            ev.Use();
            SoundDefOf.Click.PlayOneShotOnCamera();
            ColonistCardWindow.Open(colonist);
        }

        private static string paneTip;

        /// <summary>
        /// The card button on the inspect pane, by the name of a colonist selected alone: drawn left of the game's own buttons
        /// there (it counts itself in <paramref name="lineEndWidth"/>, so the name gives it room as it does theirs).
        /// </summary>
        public static void PaneButton(Rect rect, ref float lineEndWidth)
        {
            if (!PaneOn || Find.Selector == null || Find.Selector.NumSelected != 1) return;
            if (!(Find.Selector.SingleSelectedThing is Pawn p) || !ColonistCardWindow.Fits(p)) return;
            var r = new Rect(rect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            if (r.x < 60f || Icons.ColonistCard == null) return;
            lineEndWidth += 24f;
            paneTip ??= "CC_CCard_PaneTip".Translate().Resolve();
            TooltipHandler.TipRegion(r, paneTip);
            if (!Widgets.ButtonImage(r.ContractedBy(2f), Icons.ColonistCard, Color.white, GenUI.MouseoverColor)) return;
            ColonistCardWindow.Open(p);
        }

        private static string rowTip;

        /// <summary>
        /// A small card button at the right of a row that names a colonist (an event card's "who is involved"): their card on
        /// a click, the row's own click left to the rest of it. Returns the room it took (0 when it doesn't show).
        /// </summary>
        public static float RowButton(Rect row, Pawn p)
        {
            if (!ColonistCardWindow.On || !ColonistCardWindow.Fits(p)) return 0f;
            const float s = 22f;
            var r = new Rect(row.xMax - s - 2f, row.y + (row.height - s) / 2f, s, s);
            rowTip ??= "CC_CCard_RowTip".Translate().Resolve();
            if (UiKit.IconButton(r, Icons.ColonistCard ?? Icons.People, rowTip)) ColonistCardWindow.Open(p);
            return s + 4f;
        }
    }

    /// <summary>
    /// Ctrl and a click on a portrait in the colonist bar opens that colonist's card (UI/ColonistCard/). The game handles the
    /// bar's clicks here before it paints, every event: the prefix takes only a mouse-down with Ctrl on a portrait and leaves
    /// every other event to the game (its selection, double-click, reordering, the badges' own clicks). Applied on its own.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.HandleClicks))]
    public static class Patch_ColonistCardBarClick
    {
        public static void Prefix(Rect rect, Pawn colonist)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                ColonistCardEntry.BarClick(rect, colonist);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: the colonist bar's click", e);
            }
        }
    }

    /// <summary>
    /// The card button by a colonist's name on the inspect pane (UI/ColonistCard/), after the game's own buttons there (the
    /// info card, the response to danger), counted in the pane's line end so the name makes room for it. Applied on its own.
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoInspectPaneButtons))]
    public static class Patch_ColonistCardPaneButton
    {
        public static void Postfix(Rect rect, ref float lineEndWidth)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                ColonistCardEntry.PaneButton(rect, ref lineEndWidth);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: the inspect pane's button", e);
            }
        }
    }
}
