using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's trade screen (Dialog_Trade: traders on the map, a caravan at a town, a ship by the comms console),
    /// made quicker to read: Rimfolk's price sorts in the game's own sort menus (Trade/TradeSorts.cs), a mark beside each
    /// good worth selling, worth buying or kept (Trade/TradeAdvice.cs says why), a button that fills the suggested basket
    /// within the silver both sides have and one that takes it back out, the silver each side has after the trade under
    /// its silver, and the days of food after it at the foot of a trade at home (a caravan's own food days are the
    /// game's, at the top). A good's market and the colony's own last prices are in a card on its prices and in the
    /// Market window (Trade/TradeMarket.cs). Nothing is traded but by the game's Accept. Each part has its setting; the
    /// marks, the buttons, the readouts and the market card are deeper game hooks.
    /// <para>
    /// Other mods: Vanilla Trading Expanded draws nothing on this screen (its windows are on its trading terminal), so
    /// its market and contracts are read into the advice and its banks shown at the foot. Trade Helper widens this screen
    /// for a side panel of its own (a wish list, and lists of what is bought and sold, drawn after the game's own); Rimfolk
    /// draws nothing there, and with Trade Helper loaded leaves out its silver-after readout, since Trade Helper's lists
    /// already say what the trade comes to.
    /// </para>
    /// </summary>
    public static class TradeScreen
    {
        public const string TradeHelperId = "timmyliang.tradehelper";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static bool? tradeHelper;

        /// <summary>Trade Helper is loaded.</summary>
        public static bool TradeHelper => tradeHelper ??= People.ModActive(TradeHelperId);

        /// <summary>The game's trade screen is open on a trade (not giving gifts).</summary>
        public static bool Trading => TradeSession.Active && TradeSession.deal != null && !TradeSession.giftMode && Find.WindowStack?.WindowOfType<Dialog_Trade>() != null;

        public static bool Silver => TradeSession.Active && TradeSession.TradeCurrency == TradeCurrency.Silver;

        public static bool SortsOn => S?.tradeSorts ?? true;

        public static bool MarksOn => RimfolkPatches.On && (S?.tradeMarks ?? true) && Trading;

        public static bool BasketOn => RimfolkPatches.On && (S?.tradeBasket ?? true) && Trading && Silver;

        public static bool ReadoutsOn => RimfolkPatches.On && (S?.tradeReadouts ?? true) && Trading;

        /// <summary>The silver after the trade: not while Trade Helper's lists say what the trade comes to.</summary>
        public static bool SilverAfterOn => ReadoutsOn && Silver && !TradeHelper;

        // ---------------- The marks ----------------

        private static readonly Color BuyColor = new Color(0.45f, 0.85f, 0.45f), SellColor = new Color(0.95f, 0.78f, 0.35f), KeepColor = new Color(0.6f, 0.78f, 1f),
            WarnColor = new Color(1f, 0.45f, 0.4f);

        public const float MarkSize = 24f;

        /// <summary>A row's mark at the right end of its name (where the game puts its own small icons), with why as its tooltip.</summary>
        public static void DrawMark(Tradeable t, Rect row, ref float curX)
        {
            var n = TradeAdvice.For(t);
            if (n == null || !n.Marked) return;
            var r = new Rect(curX - MarkSize, (row.height - MarkSize) / 2f, MarkSize, MarkSize);
            curX -= MarkSize;
            Texture2D icon;
            Color color;
            switch (n.mark)
            {
                case TradeMark.Buy:
                    icon = Icons.TrendUp;
                    color = BuyColor;
                    break;
                case TradeMark.Sell:
                    icon = Icons.TrendDown;
                    color = SellColor;
                    break;
                default:
                    icon = Icons.Pin;
                    color = KeepColor;
                    break;
            }
            // Selling into what is set aside turns the mark red.
            if (n.setAside > 0 && t.CountToTransfer < 0 && n.have + t.CountToTransfer < n.setAside) color = WarnColor;
            if (icon != null)
            {
                GUI.color = color;
                GUI.DrawTexture(r.ContractedBy(3f), icon);
                GUI.color = Color.white;
            }
            if (Mouse.IsOver(r))
            {
                Widgets.DrawHighlight(r);
                TooltipHandler.TipRegion(r, new TipSignal(() => TradeAdvice.Tip(n, t), t.GetHashCode() ^ 0x7a4de));
            }
        }

        // ---------------- The silver after ----------------

        /// <summary>Under each side's silver, in the price columns the silver row leaves empty: what each side has after the trade.</summary>
        public static void DrawSilverAfter(Rect rect, Tradeable currency)
        {
            if (currency.CountToTransfer == 0) return;
            int colony = currency.CountPostDealFor(Transactor.Colony), trader = currency.CountPostDealFor(Transactor.Trader);
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            var mine = new Rect(rect.x + rect.width - TradeUI.TotalNumbersColumnsWidths + TradeUI.CountColumnWidth + 5f, rect.y, TradeUI.PriceColumnWidth - 10f, rect.height);
            var theirs = new Rect(rect.x + rect.width - TradeUI.CountColumnWidth - TradeUI.PriceColumnWidth + 5f, rect.y, TradeUI.PriceColumnWidth - 10f, rect.height);
            GUI.color = colony < 0 ? WarnColor : new Color(0.75f, 0.75f, 0.75f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(mine, "CC_Trade_SilverAfter".Translate(colony.ToStringCached()));
            GUI.color = trader < 0 ? WarnColor : new Color(0.75f, 0.75f, 0.75f);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(theirs, "CC_Trade_SilverAfter".Translate(trader.ToStringCached()));
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
            string tip = "CC_Trade_SilverAfterTip".Translate(colony.ToStringCached().Named("COLONY"), trader.ToStringCached().Named("TRADER")).Resolve();
            TooltipHandler.TipRegion(mine, tip);
            TooltipHandler.TipRegion(theirs, tip);
        }

        // ---------------- The foot: the basket and the readouts ----------------

        /// <summary>
        /// The foot of the screen, beside the game's Reset, Accept and Cancel: on the left the basket's two buttons, on the
        /// right the days of food after the trade (a trade at home) and the silver in Vanilla Trading Expanded's banks.
        /// Laid out from the list's own rect, so it follows the screen however wide it is drawn.
        /// </summary>
        public static void DrawFoot(Dialog_Trade dialog, Rect list)
        {
            float w = list.width, y = list.yMax + 3f, h = 40f;
            // The game's buttons: Reset, Accept and Cancel round the middle (160, 160, 160 wide, 10 apart), its two icons at the right.
            float leftEnd = w / 2f - 80f - 10f - 160f - 10f, rightStart = w / 2f + 80f + 10f + 160f + 10f, rightEnd = w - 2f * 40f - 4f - 8f;
            if (BasketOn) Basket(dialog, new Rect(0f, y, leftEnd, h));
            if (ReadoutsOn) Readouts(new Rect(rightStart, y, rightEnd - rightStart, h));
        }

        private static void Basket(Dialog_Trade dialog, Rect area)
        {
            float bw = Mathf.Min(120f, (area.width - 8f) / 2f);
            if (bw < 60f) return;
            var fill = new Rect(area.x, area.y, bw, area.height);
            var clear = new Rect(fill.xMax + 8f, area.y, bw, area.height);
            var notes = TradeAdvice.All;
            bool any = notes.Values.Any(n => n.mark == TradeMark.Buy || n.mark == TradeMark.Sell);
            Prune();
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(fill, "CC_Trade_Fill".Translate(), active: any))
            {
                if (any)
                {
                    Fill(dialog);
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                else SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(fill, (any ? "CC_Trade_FillTip" : "CC_Trade_FillNone").Translate().Resolve());
            bool some = basket.Count > 0;
            if (Widgets.ButtonText(clear, "CC_Trade_Clear".Translate(), active: some))
            {
                if (some)
                {
                    Clear(dialog);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
                else SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(clear, "CC_Trade_ClearTip".Translate().Resolve());
        }

        private static void Readouts(Rect area)
        {
            if (area.width < 80f) return;
            var lines = new List<(string text, string tip)>();
            // A caravan's days of food are the game's own, at the top of the screen; at home they are Rimfolk's here.
            var notes = TradeAdvice.All;
            if (TradeSession.playerNegotiator?.GetCaravan() == null && TradeSession.playerNegotiator?.MapHeld?.IsPlayerHome == true && notes != null && TradeAdvice.foodKnown)
            {
                float now = TradeAdviceMath.FoodDays(TradeAdvice.mealNutrition, TradeAdvice.rawNutrition, TradeAdvice.eatADay, TradeAdvice.cooks);
                float meal = TradeAdvice.mealNutrition, raw = TradeAdvice.rawNutrition;
                foreach (var t in TradeSession.deal.AllTradeables)
                {
                    if (t == null || t.IsCurrency || t.CountToTransfer == 0 || !t.HasAnyThing || TradeAdvice.KindOf(t.ThingDef) != TradeGood.Food) continue;
                    float nutrition = t.ThingDef.GetStatValueAbstract(StatDefOf.Nutrition) * t.CountToTransferToSource;
                    if (ColonyNeeds.KindOf(t.ThingDef) == AheadMath.Raw) raw += nutrition;
                    else meal += nutrition;
                }
                float after = TradeAdviceMath.FoodDays(meal, raw, TradeAdvice.eatADay, TradeAdvice.cooks);
                lines.Add(("CC_Trade_FoodAfter".Translate(Days(now).Named("NOW"), Days(after).Named("AFTER")).Resolve(),
                    "CC_Trade_FoodAfterTip".Translate(TradeAdvice.eatADay.ToString("0.#").Named("NEED")).Resolve()));
            }
            if (VanillaTrading.On)
            {
                // Read once a trade: the banks don't move while the screen is open.
                if (banksDeal != TradeSession.deal)
                {
                    banksDeal = TradeSession.deal;
                    var banks = VanillaTrading.Deposits();
                    int total = banks.Sum(b => b.silver);
                    banksLine = total > 0
                        ? ("CC_Trade_Banks".Translate(total.ToStringCached()).Resolve(),
                            "CC_Trade_BanksTip".Translate(string.Join("\n", banks.Select(b => b.bank + ": " + b.silver.ToStringCached())).Named("LIST")).Resolve())
                        : (null, null);
                }
                if (banksLine.text != null) lines.Add(banksLine);
            }
            if (lines.Count == 0) return;
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            float lh = Mathf.Min(20f, area.height / lines.Count);
            float top = area.y + (area.height - lh * lines.Count) / 2f;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            for (int i = 0; i < lines.Count; i++)
            {
                var r = new Rect(area.x, top + i * lh, area.width, lh);
                Widgets.Label(r, lines[i].text.Truncate(r.width));
                TooltipHandler.TipRegion(r, lines[i].tip);
            }
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private static TradeDeal banksDeal;
        private static (string text, string tip) banksLine;

        private static string Days(float d) => d >= AheadMath.Far ? "-" : d.ToString(d < 10f ? "0.#" : "0");

        // ---------------- The suggested basket ----------------

        /// <summary>The rows the last fill set: what each held before and what it was set to, for this trade's goods only.</summary>
        private static readonly Dictionary<Tradeable, (int before, int set)> basket = new Dictionary<Tradeable, (int, int)>();

        private static TradeDeal basketDeal;
        private static MethodInfo countChanged;

        /// <summary>Forgets a fill from another trade, or rows the game's Reset replaced.</summary>
        private static void Prune()
        {
            var deal = TradeSession.deal;
            if (deal != basketDeal)
            {
                basket.Clear();
                basketDeal = deal;
            }
            if (basket.Count == 0) return;
            var all = deal?.AllTradeables;
            foreach (var t in basket.Keys.ToList())
                if (all == null || !all.Contains(t) || t.CountToTransfer != basket[t].set)
                    basket.Remove(t);
        }

        /// <summary>The caravan's mass, speed and food days are worked out again (the dialog's own note that counts changed).</summary>
        private static void Changed(Dialog_Trade dialog)
        {
            try
            {
                countChanged ??= AccessTools.Method(typeof(Dialog_Trade), "CountToTransferChanged");
                countChanged?.Invoke(dialog, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade basket", e);
            }
        }

        /// <summary>
        /// Fills the suggested basket: the buys and sales marked, within the silver both sides have once what the player
        /// set by hand is paid for (TradeAdviceMath.Fill). Rows the player set by hand are left as they are.
        /// </summary>
        public static void Fill(Dialog_Trade dialog)
        {
            Prune();
            UndoBasket();
            var deal = TradeSession.deal;
            var currency = deal?.CurrencyTradeable;
            if (currency == null) return;
            var notes = TradeAdvice.All;
            var needs = new Dictionary<string, float>(TradeAdvice.GroupNeeds);
            // What the player set by hand: its silver comes first, and what it buys toward a need counts.
            float handNet = 0f;
            foreach (var t in deal.AllTradeables)
            {
                if (t == null || t.IsCurrency || t.CountToTransfer == 0) continue;
                handNet += t.CurTotalCurrencyCostForSource;
                if (t.CountToTransferToSource > 0 && notes.TryGetValue(t, out var hn) && hn.group != null && needs.ContainsKey(hn.group))
                    needs[hn.group] -= t.CountToTransferToSource * hn.perUnit;
            }
            var rows = new List<Tradeable>();
            var buys = new List<BasketLine>();
            var sales = new List<BasketLine>();
            foreach (var kv in notes)
            {
                var t = kv.Key;
                var n = kv.Value;
                if (t.CountToTransfer != 0 || n.count <= 0) continue;
                if (n.mark == TradeMark.Buy && n.canBuy)
                {
                    rows.Add(t);
                    buys.Add(new BasketLine
                    {
                        id = rows.Count - 1, max = Mathf.Min(n.count, t.CountHeldBy(Transactor.Trader)), price = t.GetPriceFor(TradeAction.PlayerBuys), priority = n.priority,
                        group = n.group, perUnit = n.perUnit
                    });
                }
                else if (n.mark == TradeMark.Sell && n.canSell)
                {
                    rows.Add(t);
                    sales.Add(new BasketLine
                    {
                        id = rows.Count - 1, max = Mathf.Min(n.count, t.CountHeldBy(Transactor.Colony)), price = t.GetPriceFor(TradeAction.PlayerSells), priority = n.priority
                    });
                }
            }
            int colonySilver = currency.CountHeldBy(Transactor.Colony) - Mathf.CeilToInt(handNet);
            int traderSilver = currency.CountHeldBy(Transactor.Trader) + Mathf.FloorToInt(handNet);
            var take = TradeAdviceMath.Fill(buys, needs, sales, colonySilver, traderSilver);
            var buying = new HashSet<int>(buys.Select(b => b.id));
            foreach (var kv in take)
            {
                var t = rows[kv.Key];
                int target = t.ClampAmount(buying.Contains(kv.Key) ? kv.Value : -kv.Value);
                if (target == 0 || !t.CanAdjustTo(target).Accepted) continue;
                basket[t] = (t.CountToTransfer, target);
                t.AdjustTo(target);
            }
            basketDeal = deal;
            Changed(dialog);
        }

        /// <summary>Takes the suggested basket back out: each row it set, still as it set it, goes back to what it held.</summary>
        public static void Clear(Dialog_Trade dialog)
        {
            Prune();
            UndoBasket();
            Changed(dialog);
        }

        private static void UndoBasket()
        {
            foreach (var kv in basket)
                if (kv.Key.CountToTransfer == kv.Value.set && kv.Key.CanAdjustTo(kv.Value.before).Accepted)
                    kv.Key.AdjustTo(kv.Value.before);
            basket.Clear();
        }
    }

    // ---------------- The game's hooks ----------------

    /// <summary>
    /// Rimfolk's sorts only where they mean something: the game's sort menu is shared by the trade screen, the caravan
    /// and transport pod windows and a caravan's items tab, and only the trade screen has prices. The menu's list of
    /// sorts is read through <see cref="Menu"/>, which leaves Rimfolk's out anywhere else (and on the trade screen too
    /// while their setting is off).
    /// </summary>
    [HarmonyPatch(typeof(TransferableUIUtility), "OpenSorterChangeFloatMenu")]
    public static class Patch_TradeSortMenu
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var game = AccessTools.PropertyGetter(typeof(DefDatabase<TransferableSorterDef>), nameof(DefDatabase<TransferableSorterDef>.AllDefsListForReading));
            var ours = AccessTools.Method(typeof(Patch_TradeSortMenu), nameof(Menu));
            foreach (var ci in instructions)
            {
                if (game != null && ours != null && ci.Calls(game))
                {
                    var call = new CodeInstruction(System.Reflection.Emit.OpCodes.Call, ours);
                    ci.MoveLabelsTo(call);
                    ci.MoveBlocksTo(call);
                    yield return call;
                }
                else yield return ci;
            }
        }

        public static List<TransferableSorterDef> Menu()
        {
            var all = DefDatabase<TransferableSorterDef>.AllDefsListForReading;
            if (!RimfolkPatches.On) return all;
            // On the trade screen: all of them, the price change only with Vanilla Trading Expanded's history to sort by.
            if (TradeScreen.SortsOn && TradeScreen.Trading)
                return TradeMarket.History ? all : all.Where(d => d?.comparerClass != typeof(TransferableComparer_PriceChange)).ToList();
            return all.Where(d => d?.comparerClass == null || !typeof(TransferableComparer_Rimfolk).IsAssignableFrom(d.comparerClass)).ToList();
        }
    }

    /// <summary>
    /// A row's mark and the trend line of a good whose market value moved (Trade/TradeMarket.cs), drawn where the game
    /// draws its own small icons for a row (a bond, a pregnancy); on the silver row, the Market button.
    /// </summary>
    [HarmonyPatch(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DoExtraIcons))]
    public static class Patch_TradeRowMark
    {
        public static void Postfix(Transferable trad, Rect rect, ref float curX)
        {
            if (!(trad is Tradeable t)) return;
            bool marks = TradeScreen.MarksOn, market = TradeMarket.On;
            if (!marks && !market) return;
            try
            {
                if (t.IsCurrency)
                {
                    if (market) TradeMarket.DrawMarketButton(rect, ref curX);
                    return;
                }
                if (marks) TradeScreen.DrawMark(t, rect, ref curX);
                if (market) TradeMarket.DrawTrendMark(t, rect, ref curX);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade mark", e);
            }
        }
    }

    /// <summary>The silver each side has after the trade, under its silver.</summary>
    [HarmonyPatch(typeof(TradeUI), nameof(TradeUI.DrawTradeableRow))]
    public static class Patch_TradeSilverAfter
    {
        public static void Postfix(Rect rect, Tradeable trad)
        {
            if (trad == null || !trad.IsCurrency || !TradeScreen.SilverAfterOn) return;
            try
            {
                TradeScreen.DrawSilverAfter(rect, trad);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade silver after", e);
            }
        }
    }

    /// <summary>The basket's buttons and the readouts, at the foot of the screen beside the game's own buttons.</summary>
    [HarmonyPatch(typeof(Dialog_Trade), "FillMainRect")]
    public static class Patch_TradeFoot
    {
        public static void Postfix(Dialog_Trade __instance, Rect mainRect)
        {
            if (!TradeScreen.BasketOn && !TradeScreen.ReadoutsOn) return;
            try
            {
                TradeScreen.DrawFoot(__instance, mainRect);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade screen foot", e);
            }
        }
    }
}
