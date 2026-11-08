using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own trade window in place of the game's (DESIGN.md "The trade screen"): when the game opens its trade
    /// screen (a trader or visitor on the map, Hospitality's guests, a ship called on the comms console, a caravan at a
    /// town, a caravan met on the road, transport pods landed at a town), <see cref="Window_RimfolkTrade"/> opens
    /// instead, on the same trade. The game's own screen has already set the trade up when it is made (its constructor
    /// calls TradeSession.SetupWith), so it is kept as it is and opened by the switch in Rimfolk's window, the counts
    /// set staying where they are (they live in the game's TradeDeal); a button on the game's screen comes back. Gift
    /// mode, a trade in favor (the Empire's tribute collector) and a mod's own kind of trade screen stay the game's.
    /// The setting "Rimfolk's own trade window" (What's on, Trade screen) and the deeper game hooks turn it off.
    /// </summary>
    public static class TradeWindow
    {
        /// <summary>Rimfolk's window is drawing a row: the game's row icons it calls draw no marks of the old screen.</summary>
        public static bool Drawing;

        /// <summary>The game's screen Rimfolk opens itself (the switch): let through once, not replaced.</summary>
        private static Dialog_Trade letThrough;

        /// <summary>Rimfolk's window open on a trade, or null.</summary>
        public static Window_RimfolkTrade Open => Find.WindowStack?.WindowOfType<Window_RimfolkTrade>();

        /// <summary>The setting and the deeper game hooks are on.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tradeWindow ?? true);

        /// <summary>The open trade is one Rimfolk's window takes: silver, not gifts, with a negotiator and the silver row the game always adds.</summary>
        public static bool Takes =>
            On && TradeSession.Active && TradeSession.deal != null && !TradeSession.giftMode && TradeSession.playerNegotiator != null
            && TradeSession.TradeCurrency == TradeCurrency.Silver && TradeSession.deal.CurrencyTradeable != null;

        /// <summary>From the window stack's Add: the game's own trade screen, opened by the game, becomes Rimfolk's window.</summary>
        public static Window Replace(Window window)
        {
            if (!(window is Dialog_Trade game) || window.GetType() != typeof(Dialog_Trade)) return window;
            if (letThrough != null && ReferenceEquals(letThrough, game))
            {
                letThrough = null;
                return window;
            }
            if (!Takes) return window;
            try
            {
                return new Window_RimfolkTrade(game, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade window", e);
                return window;
            }
        }

        /// <summary>
        /// The game's own screen for this trade, in place of Rimfolk's window: the counts set stay (they are the game's
        /// deal's). With <paramref name="gifts"/>, in the game's gift mode (its own switch empties the deal, as here).
        /// </summary>
        public static void ToGame(Window_RimfolkTrade from, Dialog_Trade game, bool gifts = false)
        {
            if (game == null) return;
            if (gifts)
            {
                TradeSession.giftMode = true;
                TradeSession.deal?.Reset();
            }
            from?.CloseForSwitch();
            // Its opening sound was heard when the trade opened.
            game.soundAppear = null;
            letThrough = game;
            Find.WindowStack.Add(game);
            if (ReferenceEquals(letThrough, game)) letThrough = null;
        }

        /// <summary>From the game's screen back to Rimfolk's window, on the same trade and counts. The game's screen is taken off without its own close (that hands a trader's quest over, which is for when the trade ends).</summary>
        public static void ToRimfolk(Dialog_Trade game)
        {
            if (game == null || !Takes) return;
            Window_RimfolkTrade window;
            try
            {
                window = new Window_RimfolkTrade(game, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade window", e);
                return;
            }
            Find.WindowStack.TryRemove(game, false);
            Find.WindowStack.Add(window);
        }

        private const float BackSize = 27f, BackX = 360f;

        /// <summary>
        /// On the game's own screen, right of its two sort menus (where its search box would be, which it draws at the
        /// foot instead): a button back to Rimfolk's window for this trade.
        /// </summary>
        public static void DrawBackButton(Dialog_Trade game)
        {
            var r = new Rect(BackX, 0f, BackSize, BackSize);
            if (Widgets.ButtonImage(r, Icons.Trader ?? BaseContent.BadTex, Color.white, GenUI.MouseoverColor))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                ToRimfolk(game);
            }
            TooltipHandler.TipRegion(r, "CC_TradeWin_BackTip".Translate().Resolve());
        }
    }

    /// <summary>The game's trade screen opens as Rimfolk's own trade window (Trade/TradeWindow.cs); a gift, a trade in favor or another mod's own kind of screen stays the game's.</summary>
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    public static class Patch_TradeWindowOpen
    {
        public static void Prefix(ref Window window)
        {
            if (!(window is Dialog_Trade)) return;
            window = TradeWindow.Replace(window);
        }
    }
}
