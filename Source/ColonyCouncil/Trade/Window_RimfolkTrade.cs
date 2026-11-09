using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own trade window (DESIGN.md "The trade screen"; opened in place of the game's by Trade/TradeWindow.cs).
    /// Two lists, the colony's goods to sell and the trader's to buy (the player, 9 November 2026: "could buy and sell be
    /// split into different tabs or some other setup? where I have separate lists"): side by side when the window is wide
    /// enough, else as two tabs, Sell and Buy, the player's tab and choice remembered. Each list is grouped under its
    /// kinds and has few columns: the good's picture and its whole name, how many there are, the price with a mark
    /// against the usual, the deal's grade, the need, and a count control that goes one way (sold in the sell list,
    /// bought in the buy list), set through the game's own limits and AdjustTo. Every price is the game's own
    /// (Tradeable.GetPriceFor); a row's tooltip has the rest: its market value and mass, the price's share of the value
    /// and the game's reckoning, why the deal is called what it is, the market and its trend, and the advice; the market
    /// card beside it draws the trend. Accept is the game's own path (TryExecute, its confirmations, the negotiator, the
    /// caravan and the pods). A search, kind chips and "only in the deal" above; headers that sort each list, and a menu
    /// for the sorts the columns leave out; on the right the colony's silver, the best deals both ways (a click finds the
    /// row) and the deal's summary; at the foot Fill suggested, Clear suggested, Reset, Accept, Cancel and each side's
    /// silver after the trade. The rules are <see cref="TradeWindowMath"/>'s; the advice, the market and the record are
    /// Trade/'s own, read here and not worked out again. Rows are read once a trade and drawn from what was read; a
    /// name's wrapping is measured once for a width. If anything fails while it draws, the game's own screen takes the
    /// trade over. It is in Rimfolk's frame (UI/WindowFrame.cs) and its own look while that is on.
    /// </summary>
    public class Window_RimfolkTrade : FramedWindow
    {
        /// <summary>One good of the trade, read once a trade (and again after Reset): what doesn't change while the window is open.</summary>
        private sealed class Row
        {
            public Tradeable t;
            public string label;
            public TradeCategory cat;
            public Pawn pawn;
            public bool person, willTrade, canSell, canBuy, noSlaves;
            public int ours, theirs;
            public float value, mass, pay, ask, usualSell, usualBuy, bestSold, bestBought;
            public DealRating sell, buy;
            public TradeNote note;
            public MarketView market;
            public float? trend;
            public TradeRowKeys sellKeys, buyKeys;
            // What the row draws, made when it is read.
            public string payText, askText, payMark, askMark, need, needTip;
            public int payPct, askPct, payBetter, askBetter;
            public Color needColor;
            public Func<string> nameTip;
            public object sellCard, buyCard;
            public int tipId;
            public readonly RowSide ofSell = new RowSide(), ofBuy = new RowSide();

            public RowSide Of(bool sale) => sale ? ofSell : ofBuy;
        }

        /// <summary>A row in one list: its name's height at the width it was measured for, its number box, its note when the good goes the other way.</summary>
        private sealed class RowSide
        {
            public float nameH, nameFor = -1f;
            public string field, typed, other;
            public int otherFor = -1;
        }

        /// <summary>A heading or a row of a list, where it stands and how tall.</summary>
        private struct Line
        {
            public Row row;
            public string head, headCount;
            public int nth;
            public float top, height;
        }

        /// <summary>One of the two lists: the colony's goods to sell, or the trader's to buy.</summary>
        private sealed class ListView
        {
            public readonly bool sale;
            /// <summary>Every row of this side: what the colony has (to sell) or the trader has (to buy).</summary>
            public readonly List<Row> rows = new List<Row>();
            /// <summary>What is shown: the kinds' headings and their rows, in order.</summary>
            public readonly List<Line> lines = new List<Line>();
            public int shown, inDeal;
            public Vector2 scroll;
            public float viewH = 400f, contentH, placesW = -1f, measuredFor = -1f;
            public bool dirty = true;
            public List<ColumnPlace> places;
            /// <summary>Each column's heading, the line under it, and its tooltip as sorted and not, made with the columns.</summary>
            public string[] heads, legends, tipsSorted, tipsUnsorted;
            public Row jumpTo;
            public string title, titleTip, sub;

            public ListView(bool sale)
            {
                this.sale = sale;
            }
        }

        private const float TopH = 52f, FootH = 44f, HeadH = 36f, ChipH = 24f, CaravanH = 40f, SideGap = 14f, BarH = 30f;
        private const float PictureSize = 32f, ArrowW = 22f, PawnIcons = 52f, NumberPad = 8f, ListsButtonW = 160f;

        /// <summary>The least the window is sized to: the foot's buttons and the silver, a list with all its columns, the side.</summary>
        public const float LeastW = WindowFrameMath.TradeLeastW, LeastH = WindowFrameMath.TradeLeastH;

        /// <summary>The top's height: the title, the negotiator and the buttons; in the frame, the title is in the title bar.</summary>
        private static float Top => WindowFrame.Shown ? 40f : TopH;
        private const int BestMax = 5;
        private const float FlashSeconds = 1.6f;

        private static readonly Color Great = new Color(0.32f, 0.95f, 0.5f), BarOff = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Regex Digits = new Regex("^[0-9]*$");

        /// <summary>Each list's sort (the sell list's first), kept for the session's next trade: the name from A until a header is clicked.</summary>
        private static readonly TradeColumn[] sortCols = { TradeColumn.Name, TradeColumn.Name };
        private static readonly bool[] sortDescs = { false, false };

        private const string ListsKey = "trade.lists", TabKey = "trade.tab";

        private static Texture2D sellableIcon, sortUp, sortDown;

        private readonly Dialog_Trade game;
        private readonly bool first;
        private readonly TradeDeal deal;
        private readonly bool playerIsCaravan, market;
        private readonly List<Thing> caravanThings;
        private readonly List<Row> rows = new List<Row>();
        private readonly ListView sellList = new ListView(true), buyList = new ListView(false);
        private readonly List<Row> bestSells = new List<Row>(), bestBuys = new List<Row>();
        private readonly List<Tradeable> goods = new List<Tradeable>();
        /// <summary>The chips: everything, then each kind this trade has, with how many goods of it.</summary>
        private readonly List<(TradeCategory? cat, int n)> chips = new List<(TradeCategory?, int)>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Tradeable currency;
        private TradeCategory? category;
        private bool inDealOnly, failed, switching, caravanDirty = true;
        private int stamp = int.MinValue;
        private Vector2 sideScroll;
        private float sideHeight = 600f;
        private Row flashRow;
        private bool flashSale;
        private float flashAt = -99f;
        /// <summary>The player's choice of side by side or tabs, and the tab shown (remembered with the settings); side by side as drawn last.</summary>
        private TradeListsChoice listsChoice;
        private bool tabSell, sideNow;
        /// <summary>The chips' words, widths and tooltips, made once a trade.</summary>
        private readonly List<string> chipLabels = new List<string>(), chipTips = new List<string>();
        private readonly List<float> chipWidths = new List<float>();

        // The deal as it stands, worked out when a count changes.
        private DealSums sums = new DealSums();
        private int colonyNow, colonyAfter, traderNow, traderAfter;
        private readonly List<string> impacts = new List<string>();
        private List<(string text, string tip)> readouts = new List<(string, string)>();
        private CaravanUIUtility.CaravanInfo caravanInfo;

        // Words said in every row, made once.
        private static string wontTrade, usualWord, otherBuyingTip, otherSellingTip;
        private static readonly string[] tagWords = new string[5], kindWords = new string[Enum.GetValues(typeof(TradeCategory)).Length];
        private static string offWord;

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        private static readonly Dictionary<string, string> said = new Dictionary<string, string>();

        /// <summary>A word without arguments, translated once (the words drawn every frame).</summary>
        private static string K(string key)
        {
            if (!said.TryGetValue(key, out var s)) said[key] = s = W(key);
            return s;
        }

        private static string Money(float silver) => silver.ToStringMoney();

        public override Vector2 InitialSize => new Vector2(Mathf.Min(UI.screenWidth - 24f, 1800f), UI.screenHeight);

        public Window_RimfolkTrade(Dialog_Trade game, bool first) : base("Trade", LeastW, LeastH, 0f)
        {
            this.game = game;
            this.first = first;
            deal = TradeSession.deal;
            frameTitle = W("CC_TradeWin_Title", TradeSession.trader.TraderName.Named("TRADER"));
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            soundAppear = first ? SoundDefOf.CommsWindow_Open : null;
            soundClose = SoundDefOf.CommsWindow_Close;
            if (TradeSession.trader is PassingShip) soundAmbient = SoundDefOf.RadioComms_Ambience;
            market = TradeMarket.History;
            listsChoice = PageMemory.GetEnum(ListsKey, TradeListsChoice.Auto);
            tabSell = PageMemory.Get(TabKey) != "buy";
            sellList.title = W("CC_TradeWin_ListSell");
            sellList.titleTip = W("CC_TradeWin_ListSellTip");
            buyList.title = W("CC_TradeWin_ListBuy");
            buyList.titleTip = W("CC_TradeWin_ListBuyTip");
            // A caravan's pawns and goods, for its mass, speed and food after the trade, as the game's own screen reads them.
            var caravan = TradeSession.playerNegotiator.GetCaravan();
            if (caravan != null)
            {
                playerIsCaravan = true;
                caravanThings = new List<Thing>();
                caravanThings.AddRange(caravan.PawnsListForReading);
                caravanThings.AddRange(CaravanInventoryUtility.AllInventoryItems(caravan));
            }
            Build();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            search.Reset();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            // The game's own word when the negotiator can't talk or hear well, once, as its own screen gives it.
            if (!first || playerIsCaravan) return;
            var negotiator = TradeSession.playerNegotiator;
            float talking = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Talking), hearing = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
            if (talking >= 0.95f && hearing >= 0.95f) return;
            TaggedString text = talking < 0.95f ? "NegotiatorTalkingImpaired".Translate(negotiator.LabelShort, negotiator) : "NegotiatorHearingImpaired".Translate(negotiator.LabelShort, negotiator);
            text += "\n\n" + "NegotiatorCapacityImpaired".Translate();
            Find.WindowStack.Add(new Dialog_MessageBox(text));
        }

        public override bool CausesMessageBackground() => true;

        protected override string FrameTitle => frameTitle;

        /// <summary>The title bar's words, made when the window is (the trade may end before the window closes).</summary>
        private readonly string frameTitle;

        protected override Texture2D FrameIcon => Icons.Trader;

        /// <summary>Closed for good (Cancel, Accept, Escape): as the game's own screen closes, a trader with a quest for the colony hands it over.</summary>
        public override void Close(bool doCloseSound = true)
        {
            if (switching)
            {
                base.Close(false);
                return;
            }
            DragSliderManager.ForceStop();
            base.Close(doCloseSound);
            if (TradeSession.trader is Pawn pawn && pawn.mindState != null && pawn.mindState.hasQuest) TradeUtility.ReceiveQuestFromTrader(pawn, TradeSession.playerNegotiator);
        }

        /// <summary>Taken off for the game's own screen on the same trade: no closing sound, no quest handed over (that is when the trade ends).</summary>
        public void CloseForSwitch()
        {
            switching = true;
            Close(false);
        }

        // ---------------- Reading the trade ----------------

        /// <summary>Every good of the trade a row, as the game's own screen lists them (what the trader won't deal in left out where its kind hides it); the colony's in the sell list, the trader's in the buy list.</summary>
        private void Build()
        {
            rows.Clear();
            goods.Clear();
            sellList.rows.Clear();
            buyList.rows.Clear();
            currency = deal.CurrencyTradeable;
            var trader = TradeSession.trader;
            bool hide = trader.TraderKind?.hideThingsNotWillingToTrade ?? false;
            var notes = TradeAdvice.All;
            var book = TradeBook.On ? TradeBook.Instance : null;
            int now = Find.TickManager.TicksGame;
            foreach (var t in deal.AllTradeables)
            {
                if (t == null || t.IsCurrency || t.IsFavor || !t.HasAnyThing) continue;
                goods.Add(t);
                if (!t.TraderWillTrade && hide) continue;
                var r = Read(t, notes, book, now, rows.Count);
                rows.Add(r);
                if (r.ours > 0) sellList.rows.Add(r);
                if (r.theirs > 0) buyList.rows.Add(r);
            }
            chips.Clear();
            chipLabels.Clear();
            chipWidths.Clear();
            chipTips.Clear();
            chips.Add((null, rows.Count));
            chips.AddRange(rows.GroupBy(x => x.cat).OrderBy(g => (int)g.Key).Select(g => ((TradeCategory?)g.Key, g.Count())));
            if (category != null && !chips.Any(c => c.cat == category)) category = null;
            bestSells.Clear();
            bestBuys.Clear();
            bestSells.AddRange(TradeWindowMath.Best(rows.Select((r, i) => (i, r.sell)).ToList(), BestMax).Select(i => rows[i]));
            bestBuys.AddRange(TradeWindowMath.Best(rows.Select((r, i) => (i, r.buy)).ToList(), BestMax).Select(i => rows[i]));
            stamp = int.MinValue;
            Refilter();
        }

        private Row Read(Tradeable t, Dictionary<Tradeable, TradeNote> notes, TradeBook book, int now, int index)
        {
            var thing = t.AnyThing;
            var r = new Row { t = t, label = t.LabelCap, pawn = thing as Pawn };
            r.person = r.pawn != null && r.pawn.RaceProps.Humanlike;
            r.cat = CategoryOf(thing);
            r.willTrade = t.TraderWillTrade;
            r.ours = t.CountHeldBy(Transactor.Colony);
            r.theirs = t.CountHeldBy(Transactor.Trader);
            r.value = t.BaseMarketValue;
            r.mass = thing.GetStatValue(StatDefOf.Mass);
            r.canSell = r.ours > 0 && r.willTrade;
            r.canBuy = r.theirs > 0 && r.willTrade;
            if (r.canSell) r.pay = t.GetPriceFor(TradeAction.PlayerSells);
            if (r.canBuy) r.ask = t.GetPriceFor(TradeAction.PlayerBuys);
            notes.TryGetValue(t, out r.note);
            if (r.note != null)
            {
                r.usualSell = r.note.usualSell;
                r.usualBuy = r.note.usualBuy;
            }
            else TradeAdvice.UsualFor(t, out r.usualSell, out r.usualBuy);
            if (!r.person && r.pawn == null) r.market = TradeMarket.Known(t.ThingDef);
            // The colony's best prices of the last 15 days, of the same quality where it has one.
            var log = book?.For(t.ThingDef, t.StuffDef);
            if (log != null && log.lines.Count > 0)
            {
                var entries = log.Entries;
                int quality = thing.TryGetQuality(out var qc) ? (int)qc : -1;
                int i = TradeMarketMath.Best(entries, true, now, quality);
                if (i >= 0) r.bestSold = entries[i].price;
                i = TradeMarketMath.Best(entries, false, now, quality);
                if (i >= 0) r.bestBought = entries[i].price;
            }
            var stats = r.market?.stats;
            var n = r.note;
            if (r.canSell)
                r.sell = TradeWindowMath.Rate(new DealInput
                {
                    sale = true, price = r.pay, usual = r.usualSell, market = TradeWindowMath.MarketStep(TradeMarketMath.Hint(stats, true)), best = r.bestSold,
                    kept = n != null && (n.mark == TradeMark.Buy || n.setAside > 0 && n.setAside >= n.have), graded = !r.person
                });
            if (r.canBuy)
                r.buy = TradeWindowMath.Rate(new DealInput
                {
                    sale = false, price = r.ask, usual = r.usualBuy, market = TradeWindowMath.MarketStep(TradeMarketMath.Hint(stats, false)), best = r.bestBought,
                    use = n != null && n.mark == TradeMark.Buy, graded = !r.person
                });
            if (stats != null && stats.known && (stats.monthDays > 0 || stats.weekDays > 0)) r.trend = stats.monthDays > 0 ? stats.month : stats.week;
            // Each list's sort keys: its own count, price and deal.
            float? marketKey = stats != null && stats.known ? stats.vsUsual : (float?)null;
            float? needKey = n != null ? TradeAdviceMath.SortScore(n.mark, n.priority) : (float?)null;
            r.sellKeys = new TradeRowKeys
            {
                name = r.label, category = (int)r.cat, ours = r.ours, theirs = r.theirs, value = r.value, mass = r.mass, pay = r.canSell ? r.pay : (float?)null,
                deal = TradeWindowMath.SideKey(r.sell), market = marketKey, trend = r.trend, need = needKey, off = !r.willTrade
            };
            r.buyKeys = new TradeRowKeys
            {
                name = r.label, category = (int)r.cat, ours = r.ours, theirs = r.theirs, value = r.value, mass = r.mass, ask = r.canBuy ? r.ask : (float?)null,
                deal = TradeWindowMath.SideKey(r.buy), market = marketKey, trend = r.trend, need = needKey, off = !r.willTrade
            };
            // What the row draws: the prices and their marks against the usual, the need's word.
            if (r.canSell) Mark(r.pay, r.usualSell, true, out r.payText, out r.payPct, out r.payBetter, out r.payMark);
            if (r.canBuy) Mark(r.ask, r.usualBuy, false, out r.askText, out r.askPct, out r.askBetter, out r.askMark);
            NeedWord(r);
            // Ideology: a negotiator who won't sell into slavery, as the game's own row asks.
            r.noSlaves = ModsConfig.IdeologyActive && r.canSell && TransferableUIUtility.TradeIsPlayerSellingToSlavery(t, TradeSession.trader.Faction)
                         && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
            r.nameTip = () => NameTip(t);
            r.sellCard = (t, 0);
            r.buyCard = (t, 1);
            r.tipId = t.GetHashCode();
            r.ofSell.field = "RimfolkTradeSell" + index;
            r.ofBuy.field = "RimfolkTradeBuy" + index;
            return r;
        }

        /// <summary>A price's words: the silver, and its mark against the usual (how far above or under it, and whether that is better or worse for the colony).</summary>
        private static void Mark(float price, float usual, bool sale, out string text, out int pct, out int better, out string mark)
        {
            text = Money(price);
            pct = usual > 0f ? TradeWindowMath.VsUsual(price, usual) : 0;
            better = TradeWindowMath.Better(sale, price, usual);
            usualWord ??= W("CC_TradeWin_Usual");
            mark = usual <= 0f ? null : Math.Abs(pct) < 2 ? usualWord : Math.Abs(pct).ToStringCached() + "%";
        }

        /// <summary>What the colony needs of it, from the advice: short (worth buying), spare (worth selling) or kept; for the trader's people, how they would join.</summary>
        private static void NeedWord(Row r)
        {
            var n = r.note;
            if (r.person && r.pawn.guest != null && !TransferableUIUtility.TransferableIsCaptive(r.t))
            {
                bool colonist = r.pawn.guest.joinStatus == JoinStatus.JoinAsColonist;
                r.need = (colonist ? "JoinsAsColonist" : "JoinsAsSlave").Translate();
                r.needTip = (colonist ? "JoinsAsColonistDesc" : "JoinsAsSlaveDesc").Translate();
                r.needColor = UiKit.Faint;
                return;
            }
            if (n == null || n.mark == TradeMark.None) return;
            switch (n.mark)
            {
                case TradeMark.Buy:
                    r.need = W("CC_TradeWin_Need_Buy", n.count.Named("COUNT"));
                    r.needColor = TradeScreen.BuyColor;
                    break;
                case TradeMark.Sell:
                    r.need = W("CC_TradeWin_Need_Sell", n.count.Named("COUNT"));
                    r.needColor = TradeScreen.SellColor;
                    break;
                default:
                    r.need = n.setAside >= n.have ? W("CC_TradeWin_Need_KeepAll") : W("CC_TradeWin_Need_Keep", n.setAside.Named("COUNT"));
                    r.needColor = TradeScreen.KeepColor;
                    break;
            }
        }

        /// <summary>A thing's kind for the chips and the lists' headings (TradeWindowMath.CategoryOf): read from the game's own def and categories, so every mod's goods find theirs.</summary>
        private static TradeCategory CategoryOf(Thing thing)
        {
            var f = new TradeWindowMath.Facts();
            if (thing is Pawn p)
            {
                f.person = p.RaceProps.Humanlike;
                f.animal = !f.person;
                return TradeWindowMath.CategoryOf(f);
            }
            var d = thing.def;
            f.medicine = d.IsMedicine;
            f.drug = d.IsNonMedicalDrug;
            f.food = d.IsNutritionGivingIngestible && !d.IsCorpse;
            // A weapon is what can be held as one: the game counts anything with an attack as a weapon (a mod gives wood logs
            // one), which put wood under Weapons.
            f.weapon = d.IsWeapon && d.equipmentType == EquipmentType.Primary;
            f.apparel = d.IsApparel;
            f.art = thing.TryGetComp<CompArt>() != null || d.IsWithinCategory(ThingCategoryDefOf.BuildingsArt);
            f.book = thing is Book;
            f.raw = d.IsStuff || d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw) || d.IsWithinCategory(ThingCategoryDefOf.Chunks);
            f.made = d.IsWithinCategory(ThingCategoryDefOf.Manufactured) || d.category == ThingCategory.Building || d.IsWithinCategory(ThingCategoryDefOf.Buildings)
                     || d.IsWithinCategory(ThingCategoryDefOf.BodyParts) && !d.IsNaturalOrgan;
            return TradeWindowMath.CategoryOf(f);
        }

        /// <summary>Both lists again: the kind chosen, the search, only what is in the deal its way if asked; under the kinds, in the list's order, what the trader won't deal in last.</summary>
        private void Refilter()
        {
            Fill(sellList);
            Fill(buyList);
            search.noResultsMatched = sellList.shown + buyList.shown == 0 && search.filter.Active;
        }

        private void Fill(ListView l)
        {
            var kept = new List<Row>();
            var keys = new List<TradeRowKeys>();
            foreach (var r in l.rows)
            {
                if (category != null && r.cat != category.Value) continue;
                if (inDealOnly && TradeWindowMath.ThisWay(r.t.CountToTransfer, l.sale) == 0) continue;
                if (search.filter.Active && !search.filter.Matches(r.label)) continue;
                kept.Add(r);
                keys.Add(l.sale ? r.sellKeys : r.buyKeys);
            }
            int side = l.sale ? 0 : 1;
            l.lines.Clear();
            int nth = 0;
            foreach (var g in TradeWindowMath.Grouped(keys, sortCols[side], sortDescs[side]))
            {
                if (g.row < 0)
                {
                    l.lines.Add(new Line { head = GroupWord(g.group), headCount = g.count.ToStringCached() });
                    nth = 0;
                }
                else l.lines.Add(new Line { row = kept[g.row], nth = nth++ });
            }
            l.shown = kept.Count;
            l.dirty = true;
            Sub(l);
        }

        private static string GroupWord(int group)
        {
            if (group == TradeWindowMath.OffGroup) return offWord ??= W("CC_TradeWin_Group_Off");
            return kindWords[group] ??= W("CC_TradeWin_Cat_" + (TradeCategory)group);
        }

        /// <summary>The line beside a list's title: how many goods it shows, and how many of them are in the deal its way.</summary>
        private static void Sub(ListView l)
        {
            l.sub = l.inDeal > 0 ? W("CC_TradeWin_ListSubDeal", l.shown.Named("COUNT"), l.inDeal.Named("DEAL")) : W("CC_TradeWin_ListSub", l.shown.Named("COUNT"));
        }

        // ---------------- The deal as it stands ----------------

        /// <summary>The counts as they stand, folded into one number: a change anywhere (a row, Fill suggested, the game's own screen) changes it.</summary>
        private int Stamp()
        {
            unchecked
            {
                var all = deal.AllTradeables;
                int h = 17 + all.Count;
                for (int i = 0; i < all.Count; i++) h = h * 31 + all[i].CountToTransfer;
                return h;
            }
        }

        private void CountsChanged()
        {
            caravanDirty = true;
            var lines = new List<DealLine>();
            impacts.Clear();
            var moved = new HashSet<ThingDef>();
            int sold = 0, bought = 0;
            foreach (var r in rows)
            {
                var t = r.t;
                int c = t.CountToTransfer;
                if (c == 0) continue;
                var action = t.ActionToDo;
                bool sale = action == TradeAction.PlayerSells;
                if (sale) sold++;
                else bought++;
                lines.Add(new DealLine(sale, Math.Abs(c), t.GetPriceFor(action), sale ? r.usualSell : r.usualBuy, r.value));
                if (market && r.pawn == null) moved.Add(t.ThingDef);
            }
            sums = TradeWindowMath.Sum(lines);
            colonyNow = currency.CountHeldBy(Transactor.Colony);
            traderNow = currency.CountHeldBy(Transactor.Trader);
            colonyAfter = currency.CountPostDealFor(Transactor.Colony);
            traderAfter = currency.CountPostDealFor(Transactor.Trader);
            // Vanilla Trading Expanded's own rule for the colony's trades, in the market card's words.
            foreach (var def in moved)
            {
                string line = TradeMarket.ImpactLine(def);
                if (!line.NullOrEmpty()) impacts.Add(W("CC_TradeWin_Impact", def.LabelCap.ToString().Named("GOOD"), line.Named("LINE")));
            }
            readouts = TradeScreen.ReadoutsOn ? TradeScreen.ReadoutLines() : new List<(string, string)>();
            sellList.inDeal = sold;
            buyList.inDeal = bought;
            if (inDealOnly) Refilter();
            else
            {
                Sub(sellList);
                Sub(buyList);
            }
        }

        /// <summary>A caravan's mass, speed, food days, foraging and visibility after the trade: the game's own calculators, as its own screen reads them (here over every good, not only the ones its search shows).</summary>
        private void ReadCaravan()
        {
            if (!caravanDirty || !playerIsCaravan) return;
            caravanDirty = false;
            var negotiator = TradeSession.playerNegotiator;
            var caravan = negotiator.GetCaravan();
            if (caravan == null) return;
            var tile = negotiator.Tile;
            var withSilver = new List<Tradeable>(goods);
            if (currency != null) withSilver.Add(currency);
            var shuttle = caravan.Shuttle;
            float usage, capacity, tiles;
            string capacityWhy = null, tilesWhy;
            if (shuttle != null)
            {
                usage = CollectionsMassCalculator.MassUsageLeftAfterTradeableTransfer(caravanThings, withSilver, IgnorePawnsInventoryMode.Ignore, true) - shuttle.GetStatValue(StatDefOf.Mass);
                capacity = shuttle.TransporterComp.MassCapacity;
                tiles = 0f;
                tilesWhy = "CaravanMovementSpeedShuttle".Translate();
            }
            else
            {
                usage = CollectionsMassCalculator.MassUsageLeftAfterTradeableTransfer(caravanThings, withSilver, IgnorePawnsInventoryMode.Ignore);
                var sb = new StringBuilder();
                capacity = CollectionsMassCalculator.CapacityLeftAfterTradeableTransfer(caravanThings, withSilver, sb);
                capacityWhy = sb.ToString();
                sb = new StringBuilder();
                tiles = TilesPerDayCalculator.ApproxTilesPerDayLeftAfterTradeableTransfer(caravanThings, goods, usage, capacity, tile,
                    caravan.pather.Moving ? caravan.pather.nextTile : PlanetTile.Invalid, false, sb);
                tilesWhy = sb.ToString();
            }
            var food = (DaysWorthOfFoodCalculator.ApproxDaysWorthOfFoodLeftAfterTradeableTransfer(caravanThings, goods, tile, IgnorePawnsInventoryMode.Ignore, Faction.OfPlayer),
                DaysUntilRotCalculator.ApproxDaysUntilRotLeftAfterTradeableTransfer(caravanThings, goods, tile, IgnorePawnsInventoryMode.Ignore));
            var forageWhy = new StringBuilder();
            var forage = ForagedFoodPerDayCalculator.ForagedFoodPerDayLeftAfterTradeableTransfer(caravanThings, goods, Find.WorldGrid[tile].PrimaryBiome, Faction.OfPlayer, forageWhy);
            var seenWhy = new StringBuilder();
            float seen = CaravanVisibilityCalculator.VisibilityLeftAfterTradeableTransfer(caravanThings, goods, seenWhy);
            caravanInfo = new CaravanUIUtility.CaravanInfo(usage, capacity, capacityWhy, tiles, tilesWhy, food, forage, forageWhy.ToString(), seen, seenWhy.ToString());
        }

        // ---------------- Drawing ----------------

        protected override void DoFramedContents(Rect inRect)
        {
            if (failed) return;
            // The trade ended elsewhere: nothing to show.
            if (!TradeSession.Active || TradeSession.deal != deal)
            {
                Close();
                return;
            }
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            try
            {
                deal.UpdateCurrencyCount();
                int now = Stamp();
                if (now != stamp)
                {
                    stamp = now;
                    CountsChanged();
                }
                Draw(inRect);
            }
            catch (Exception e)
            {
                // Never leave the player without a trade screen: the game's own takes over, on the same counts.
                failed = true;
                CouncilLog.ErrorOnce("trade window", e);
                TradeWindow.ToGame(this, game);
            }
            finally
            {
                TradeWindow.Drawing = false;
                GUI.color = Color.white;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = true;
            }
        }

        private void Draw(Rect inRect)
        {
            float w = inRect.width, h = inRect.height;
            float side = w >= 1500f ? 300f : 240f;
            float tableW = w - side - SideGap;
            // A switch to the game's own screen ends this window's frame.
            float top = Top;
            if (!TopBar(new Rect(0f, 0f, w, top))) return;
            float y = top + 4f;
            if (playerIsCaravan)
            {
                ReadCaravan();
                CaravanUIUtility.DrawCaravanInfo(caravanInfo, null, TradeSession.playerNegotiator.Tile, null, -9999f, new Rect(12f, y, tableW - 24f, CaravanH));
                y += CaravanH + 8f;
            }
            float footY = h - FootH;
            // The colony's silver, before and after, and whether it can pay: at the top of the side, in large (the player:
            // "it must be easier to see how much silver i actually have and whether i can afford it, at first glance").
            float walletH = currency != null ? WalletH : 0f;
            if (walletH > 0f) Wallet(new Rect(tableW + SideGap, top + 4f, side, walletH));
            Side(new Rect(tableW + SideGap, top + 4f + walletH + (walletH > 0f ? 8f : 0f), side, footY - top - 10f - walletH - (walletH > 0f ? 8f : 0f)));
            float filterH = Filters(new Rect(0f, y, tableW, 0f), false);
            Filters(new Rect(0f, y, tableW, filterH), true);
            y += filterH + 4f;
            // The two lists: side by side when there's room (or the player chose it), else the tab chosen.
            sideNow = TradeWindowMath.SideBySide(tableW, listsChoice);
            float each = TradeWindowMath.ListWidth(tableW, sideNow);
            ListsBar(new Rect(0f, y, tableW, BarH), each);
            y += BarH + 2f;
            var area = new Rect(0f, y, tableW, footY - y - 6f);
            TradeWindow.Drawing = true;
            try
            {
                if (sideNow)
                {
                    DrawList(sellList, new Rect(area.x, area.y, each, area.height));
                    float bx = area.x + each + TradeWindowMath.ListGap;
                    Widgets.DrawBoxSolid(new Rect(bx - TradeWindowMath.ListGap / 2f, area.y, 1f, area.height), UiKit.DividerColor);
                    DrawList(buyList, new Rect(bx, area.y, each, area.height));
                }
                else DrawList(tabSell ? sellList : buyList, area);
            }
            finally
            {
                TradeWindow.Drawing = false;
            }
            Foot(new Rect(0f, footY, w, FootH));
        }

        // ---------------- The top ----------------

        /// <summary>The title, the negotiator and the buttons; false once a button has handed the trade to the game's own screen.</summary>
        private bool TopBar(Rect r)
        {
            var trader = TradeSession.trader;
            var negotiator = TradeSession.playerNegotiator;
            // The buttons, from the right: the game's own screen, gifts, what they buy, the market.
            float x = r.xMax;
            Text.Font = GameFont.Small;
            float by = WindowFrame.Shown ? r.y + (r.height - 30f) / 2f : r.y + 6f, bh = WindowFrame.Shown ? 30f : 32f;
            var toGame = new Rect(x - 150f, by, 150f, bh);
            x = toGame.x - 8f;
            if (UiKit.Button(toGame, K("CC_TradeWin_ToGame"), ButtonKind.Quiet))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                TradeWindow.ToGame(this, game);
                return false;
            }
            TooltipHandler.TipRegion(toGame, K("CC_TradeWin_ToGameTip"));
            var faction = trader.Faction;
            if (faction != null && !faction.def.permanentEnemy)
            {
                var gifts = new Rect(x - 120f, by, 120f, bh);
                x = gifts.x - 8f;
                if (UiKit.Button(gifts, K("CC_TradeWin_Gifts")))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    TradeWindow.ToGame(this, game, true);
                    return false;
                }
                TooltipHandler.TipRegion(gifts, W("CC_TradeWin_GiftsTip", faction.Name.Named("FACTION")));
            }
            sellableIcon ??= ContentFinder<Texture2D>.Get("UI/Commands/SellableItems", false);
            var sellable = new Rect(x - bh, by, bh, bh);
            x = sellable.x - 8f;
            bool sellableClicked = UiKit.OwnLook ? sellableIcon != null ? UiKit.IconPlate(sellable, sellableIcon) : UiKit.Button(sellable, "?")
                : sellableIcon != null ? Widgets.ButtonImageWithBG(sellable, sellableIcon, new Vector2(26f, 26f)) : Widgets.ButtonText(sellable, "?");
            if (sellableClicked)
                Find.WindowStack.Add(new Dialog_SellableItems(trader));
            TooltipHandler.TipRegionByKey(sellable, "CommandShowSellableItemsDesc");
            if (TradeMarket.On)
            {
                var mk = new Rect(x - 100f, by, 100f, bh);
                x = mk.x - 8f;
                if (UiKit.Button(mk, K("CC_Trade_MarketButton")) && Find.WindowStack.WindowOfType<Window_TradeMarket>() == null)
                    Find.WindowStack.Add(new Window_TradeMarket());
                TooltipHandler.TipRegion(mk, K(TradeMarket.History ? "CC_Trade_MarketButtonTip" : "CC_Trade_MarketButtonTipNoVte"));
            }
            // The title: who, what kind of trader, and the negotiator (in the frame, the title is in its title bar above).
            float tw = Mathf.Max(60f, x - r.x - 8f);
            string kind = trader.TraderKind?.LabelCap.ToString() ?? "";
            string line = W("CC_TradeWin_Negotiator", kind.Named("KIND"), negotiator.LabelShort.Named("NAME"),
                negotiator.GetStatValue(StatDefOf.TradePriceImprovement).ToStringPercent().Named("PCT"));
            if (WindowFrame.Shown)
            {
                UiKit.LabelFit(new Rect(r.x + 2f, r.y, tw, r.height), line, UiKit.Faint);
                Text.Font = GameFont.Small;
                return true;
            }
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(r.x, r.y, tw, 32f), W("CC_TradeWin_Title", trader.TraderName.Named("TRADER")).Truncate(tw));
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(r.x, r.y + 32f, tw, 20f), line.Truncate(tw));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return true;
        }

        /// <summary>The search, the kind chips (the kinds this trade has, each with its count) and "only in the deal"; returns the height they take.</summary>
        private float Filters(Rect r, bool draw)
        {
            const float searchW = 180f, inDealW = 150f;
            var cats = chips;
            Text.Font = GameFont.Tiny;
            if (chipLabels.Count != cats.Count)
            {
                // Made once a trade (the chips change only with Build).
                chipLabels.Clear();
                chipWidths.Clear();
                chipTips.Clear();
                foreach (var c in cats)
                {
                    string l = (c.cat == null ? W("CC_TradeWin_Cat_All") : W("CC_TradeWin_Cat_" + c.cat.Value)) + " " + c.n.ToStringCached();
                    chipLabels.Add(l);
                    chipWidths.Add(Text.CalcSize(l).x + 16f);
                    chipTips.Add(c.cat == null ? null : W("CC_TradeWin_ChipTip", W("CC_TradeWin_Cat_" + c.cat.Value).Named("LABEL")));
                }
            }
            var labels = chipLabels;
            var widths = chipWidths;
            float chipsX = searchW + 10f, chipsW = Mathf.Max(80f, r.width - chipsX - inDealW - 10f);
            var at = TradeWindowMath.Flow(widths, chipsW, 4f, ChipH, out float chipsH);
            float height = Mathf.Max(ChipH, chipsH) + 4f;
            if (!draw) return height;
            Text.Font = GameFont.Small;
            UiKit.SearchField(new Rect(r.x, r.y + 2f, searchW, 24f), search, Refilter);
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < cats.Count; i++)
            {
                var c = cats[i];
                var cr = new Rect(r.x + chipsX + at[i].x, r.y + 2f + at[i].y, widths[i], ChipH);
                bool on = category == c.cat;
                if (UiKit.OwnLook) UiKit.ChipBack(cr, on, Mouse.IsOver(cr));
                else
                {
                    Widgets.DrawBoxSolid(cr, on ? UiKit.CardSelected : UiKit.CardFill);
                    if (on) Widgets.DrawBox(cr);
                    if (Mouse.IsOver(cr)) Widgets.DrawHighlight(cr);
                }
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = on ? UiKit.Gold : UiKit.Ink;
                Widgets.Label(cr, labels[i]);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (chipTips[i] != null) TooltipHandler.TipRegion(cr, chipTips[i]);
                if (Widgets.ButtonInvisible(cr))
                {
                    category = on || c.cat == null ? null : c.cat;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    Refilter();
                }
            }
            Text.Font = GameFont.Small;
            var inDeal = new Rect(r.xMax - inDealW, r.y + 2f, inDealW, 24f);
            if (UiKit.Toggle(inDeal, K("CC_TradeWin_InDeal"), ref inDealOnly, K("CC_TradeWin_InDealTip"))) Refilter();
            return height;
        }

        // ---------------- The two lists ----------------

        /// <summary>
        /// Above the lists: side by side, each list's title over it; as tabs, Sell and Buy and the shown list's line; at the
        /// right, the player's choice of side by side or tabs (a menu; its tooltip says when the window is too narrow for it).
        /// </summary>
        private void ListsBar(Rect r, float each)
        {
            var choice = new Rect(r.xMax - ListsButtonW, r.y + 3f, ListsButtonW, 24f);
            bool narrow = listsChoice == TradeListsChoice.SideBySide && !sideNow;
            if (UiKit.Button(choice, ListsWord(listsChoice), ButtonKind.Quiet, true, K(narrow ? "CC_TradeWin_ListsNarrow" : "CC_TradeWin_ListsTip"), GameFont.Tiny))
                Find.WindowStack.Add(new FloatMenu(ListsOptions()));
            if (sideNow)
            {
                Title(new Rect(r.x, r.y, each, r.height), sellList);
                float bx = r.x + each + TradeWindowMath.ListGap;
                Title(new Rect(bx, r.y, Mathf.Max(0f, choice.x - 8f - bx), r.height), buyList);
                return;
            }
            Text.Font = GameFont.Small;
            string sellWord = K("CC_TradeWin_TabSell"), buyWord = K("CC_TradeWin_TabBuy");
            float tw = Mathf.Max(90f, Mathf.Max(UiKit.TabButtonWidth(sellWord, null), UiKit.TabButtonWidth(buyWord, null)));
            var sellTab = new Rect(r.x, r.y + 1f, tw, r.height - 2f);
            var buyTab = new Rect(sellTab.xMax + 4f, sellTab.y, tw, sellTab.height);
            if (UiKit.TabButton(sellTab, sellWord, null, tabSell, sellList.titleTip) && !tabSell) SetTab(true);
            if (UiKit.TabButton(buyTab, buyWord, null, !tabSell, buyList.titleTip) && tabSell) SetTab(false);
            var l = tabSell ? sellList : buyList;
            Title(new Rect(buyTab.xMax + 14f, r.y, Mathf.Max(0f, choice.x - 8f - buyTab.xMax - 14f), r.height), l);
        }

        /// <summary>A list's title and the line beside it (how many goods it shows, and how many are in the deal its way).</summary>
        private static void Title(Rect r, ListView l)
        {
            Text.Font = GameFont.Small;
            float tw = Mathf.Min(UiKit.KeptWidth(l.title, GameFont.Small) + 4f, r.width);
            UiKit.LabelFit(new Rect(r.x + 2f, r.y, tw, r.height), l.title, UiKit.Heading);
            if (!l.sub.NullOrEmpty() && r.width - tw - 12f > 30f) UiKit.LabelFit(new Rect(r.x + tw + 10f, r.y + 1f, r.width - tw - 12f, r.height), l.sub, UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(new Rect(r.x, r.y, tw + 4f, r.height), l.titleTip);
        }

        private static string ListsWord(TradeListsChoice c) =>
            K(c == TradeListsChoice.SideBySide ? "CC_TradeWin_Lists_SideBySide" : c == TradeListsChoice.Tabs ? "CC_TradeWin_Lists_Tabs" : "CC_TradeWin_Lists_Auto");

        private List<FloatMenuOption> ListsOptions()
        {
            var list = new List<FloatMenuOption>();
            foreach (var c in new[] { TradeListsChoice.Auto, TradeListsChoice.SideBySide, TradeListsChoice.Tabs })
            {
                string label = W(c == TradeListsChoice.SideBySide ? "CC_TradeWin_ListsOpt_SideBySide" : c == TradeListsChoice.Tabs ? "CC_TradeWin_ListsOpt_Tabs" : "CC_TradeWin_ListsOpt_Auto");
                if (c == listsChoice) label = W("CC_TradeWin_MenuNow", label.Named("LABEL"));
                var chosen = c;
                list.Add(new FloatMenuOption(label, () =>
                {
                    listsChoice = chosen;
                    PageMemory.SetEnum(ListsKey, chosen);
                    if (PageMemory.Flush()) CouncilSettings.SaveNow();
                }));
            }
            return list;
        }

        /// <summary>The tab shown, remembered for the next trade.</summary>
        private void SetTab(bool sell)
        {
            tabSell = sell;
            PageMemory.Set(TabKey, sell ? "sell" : "buy");
            if (PageMemory.Flush()) CouncilSettings.SaveNow();
        }

        /// <summary>A list: its columns' headings, then its kinds' headings and rows in a scroll view, only those in sight drawn.</summary>
        private void DrawList(ListView l, Rect r)
        {
            float rowsW = TradeWindowMath.RowsWidth(r.width);
            if (l.places == null || Mathf.Abs(l.placesW - rowsW) > 0.5f)
            {
                l.placesW = rowsW;
                l.places = TradeWindowMath.Fit(TradeWindowMath.ListColumns(l.sale), rowsW, TradeWindowMath.ColGap);
                Heads(l);
            }
            float nameW = NameWidth(l);
            Measure(l, nameW);
            Header(l, new Rect(r.x, r.y, rowsW, HeadH));
            var area = new Rect(r.x, r.y + HeadH + 2f, r.width, r.height - HeadH - 2f);
            l.viewH = area.height;
            if (l.jumpTo != null)
            {
                foreach (var line in l.lines)
                    if (line.row == l.jumpTo)
                    {
                        l.scroll.y = TradeWindowMath.ScrollToSpan(line.top, line.height, l.viewH, l.contentH);
                        break;
                    }
                l.jumpTo = null;
            }
            if (l.lines.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UiKit.Faint;
                Text.Font = GameFont.Small;
                Widgets.Label(area, K(l.rows.Count > 0 ? "CC_TradeWin_NoneShown" : l.sale ? "CC_TradeWin_NoneToSell" : "CC_TradeWin_NoneToBuy"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var view = new Rect(0f, 0f, rowsW, l.contentH);
            string focused = GUI.GetNameOfFocusedControl();
            Widgets.BeginScrollView(area, ref l.scroll, view);
            try
            {
                for (int i = 0; i < l.lines.Count; i++)
                {
                    var line = l.lines[i];
                    if (line.top + line.height < l.scroll.y || line.top > l.scroll.y + area.height) continue;
                    var rect = new Rect(0f, line.top, view.width, line.height);
                    if (line.row == null) Group(rect, line);
                    else DrawRow(rect, line.row, l, line.nth, nameW, focused);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>The name's own width in a list's name column: less the picture and the info card's button.</summary>
        private static float NameWidth(ListView l) => Mathf.Max(40f, l.places[0].width - TradeWindowMath.PictureW - TradeWindowMath.InfoW);

        /// <summary>Each line's height and where it stands: a row as tall as its name wrapped in its column needs (each measured once for a width).</summary>
        private static void Measure(ListView l, float nameW)
        {
            if (!l.dirty && Mathf.Abs(l.measuredFor - nameW) < 0.5f) return;
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            Text.WordWrap = true;
            float y = 0f;
            for (int i = 0; i < l.lines.Count; i++)
            {
                var line = l.lines[i];
                if (line.row == null) line.height = TradeWindowMath.GroupH;
                else
                {
                    var s = line.row.Of(l.sale);
                    float w = LabelWidth(line.row, nameW);
                    if (Mathf.Abs(s.nameFor - w) > 0.5f)
                    {
                        s.nameH = Text.CalcHeight(line.row.label, w);
                        s.nameFor = w;
                    }
                    line.height = Mathf.Round(TradeWindowMath.RowHeight(s.nameH));
                }
                line.top = y;
                y += line.height;
                l.lines[i] = line;
            }
            Text.Font = oldFont;
            l.contentH = y;
            l.measuredFor = nameW;
            l.dirty = false;
        }

        /// <summary>The name's width in its row: an animal's or a person's keeps room for the game's own small icons (a bond, a pregnancy, a sale into slavery).</summary>
        private static float LabelWidth(Row r, float nameW) => r.pawn != null ? Mathf.Max(40f, nameW - PawnIcons) : nameW;

        private static string HeadKey(TradeColumn c, bool sale)
        {
            switch (c)
            {
                case TradeColumn.Need:
                    return sale ? "CC_TradeWin_Col_Keep" : "CC_TradeWin_Col_Need";
                case TradeColumn.Count:
                    return sale ? "CC_TradeWin_Col_ToSell" : "CC_TradeWin_Col_ToBuy";
                default:
                    return "CC_TradeWin_Col_" + c;
            }
        }

        /// <summary>A column's second line under its name, saying what the cell's small print is; null for none.</summary>
        private static string Legend(TradeColumn c)
        {
            switch (c)
            {
                case TradeColumn.Pay:
                case TradeColumn.Ask:
                    return W("CC_TradeWin_Leg_Price");
                case TradeColumn.Deal:
                    return W("CC_TradeWin_Leg_Deal");
                default:
                    return null;
            }
        }

        /// <summary>A list's headings, legends and tooltips, made with its columns (not each frame).</summary>
        private static void Heads(ListView l)
        {
            int n = l.places.Count;
            l.heads = new string[n];
            l.legends = new string[n];
            l.tipsSorted = new string[n];
            l.tipsUnsorted = new string[n];
            for (int i = 0; i < n; i++)
            {
                var c = l.places[i].col;
                string key = HeadKey(c, l.sale);
                l.heads[i] = W(key);
                l.legends[i] = Legend(c);
                string tip = W(key + "Tip");
                bool sortable = TradeWindowMath.Sortable(c);
                l.tipsSorted[i] = sortable ? tip + "\n\n" + W("CC_TradeWin_Sorted") : tip;
                l.tipsUnsorted[i] = sortable ? tip + "\n\n" + W("CC_TradeWin_SortBy") : tip;
            }
        }

        /// <summary>The columns' headings: a click sorts the list by one (again for the other way); at the count's, a menu of every sort, those the columns leave out too.</summary>
        private void Header(ListView l, Rect r)
        {
            sortUp ??= ContentFinder<Texture2D>.Get("UI/Icons/Sorting", false);
            sortDown ??= ContentFinder<Texture2D>.Get("UI/Icons/SortingDescending", false);
            int side = l.sale ? 0 : 1;
            for (int i = 0; i < l.places.Count; i++)
            {
                var p = l.places[i];
                var cell = new Rect(r.x + p.x, r.y, p.width, r.height);
                bool sortable = TradeWindowMath.Sortable(p.col), active = sortable && sortCols[side] == p.col;
                float menuW = 0f;
                if (p.col == TradeColumn.Count)
                {
                    // Every sort this list has, the columns' and those they leave out (market value, the market, its trend, mass).
                    menuW = 24f;
                    var menu = new Rect(cell.xMax - menuW, cell.y + (cell.height - 22f) / 2f, 22f, 22f);
                    if (UiKit.IconButton(menu, Icons.More, K("CC_TradeWin_SortMenuTip"))) Find.WindowStack.Add(new FloatMenu(SortOptions(l)));
                }
                var hit = new Rect(cell.x, cell.y, cell.width - menuW, cell.height);
                if (sortable && Mouse.IsOver(hit)) Widgets.DrawHighlight(hit);
                string legend = l.legends[i];
                Text.Font = GameFont.Tiny;
                bool number = UiKit.OwnLook && Number(p.col);
                float left = p.col == TradeColumn.Name ? TradeWindowMath.PictureW : 0f;
                Text.Anchor = p.col == TradeColumn.Name ? TextAnchor.MiddleLeft : number ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                var top = new Rect(cell.x + left, cell.y + 2f, cell.width - left - menuW - (number ? NumberPad + (active ? 10f : 0f) : 0f), legend != null ? 18f : cell.height - 4f);
                GUI.color = active ? UiKit.Gold : UiKit.Heading;
                Widgets.Label(top, UiKit.Fit(l.heads[i], top.width));
                if (legend != null)
                {
                    GUI.color = UiKit.Dim;
                    Widgets.Label(new Rect(top.x, top.yMax - 2f, top.width, 16f), UiKit.Fit(legend, top.width));
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (active)
                {
                    var icon = sortDescs[side] ? sortDown : sortUp;
                    if (icon != null) GUI.DrawTexture(new Rect(hit.xMax - 12f, cell.y + 4f, 10f, 10f), icon);
                    if (UiKit.OwnLook) UiKit.Round(new Rect(cell.x + 4f, cell.yMax - 3f, hit.width - 8f, 3f), UiKit.Gold, SkinMath.RadiusSmall);
                    else Widgets.DrawBoxSolid(new Rect(cell.x + 2f, cell.yMax - 2f, hit.width - 4f, 2f), UiKit.Gold);
                }
                TooltipHandler.TipRegion(hit, active ? l.tipsSorted[i] : l.tipsUnsorted[i]);
                if (sortable && Widgets.ButtonInvisible(hit))
                {
                    SortBy(l, p.col);
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                }
            }
            Text.Font = GameFont.Small;
            UiKit.Divider(r.x, r.yMax, r.width);
        }

        /// <summary>A list sorted by a column: again the other way when it already is, else in the column's first direction.</summary>
        private void SortBy(ListView l, TradeColumn c)
        {
            int side = l.sale ? 0 : 1;
            if (sortCols[side] == c) sortDescs[side] = !sortDescs[side];
            else
            {
                sortCols[side] = c;
                sortDescs[side] = TradeWindowMath.DefaultDescending(c);
            }
            Fill(l);
        }

        /// <summary>The sort menu: the list's columns, then what they leave out (market value, with Vanilla Trading Expanded's history its market and trend, mass).</summary>
        private List<FloatMenuOption> SortOptions(ListView l)
        {
            var cols = new List<TradeColumn>
            {
                TradeColumn.Name, l.sale ? TradeColumn.Ours : TradeColumn.Theirs, l.sale ? TradeColumn.Pay : TradeColumn.Ask, TradeColumn.Deal, TradeColumn.Need, TradeColumn.Value
            };
            if (market)
            {
                cols.Add(TradeColumn.Market);
                cols.Add(TradeColumn.Trend);
            }
            cols.Add(TradeColumn.Mass);
            var list = new List<FloatMenuOption>();
            int side = l.sale ? 0 : 1;
            foreach (var c in cols)
            {
                string label = W(HeadKey(c, l.sale));
                if (sortCols[side] == c) label = W("CC_TradeWin_MenuNow", label.Named("LABEL"));
                var col = c;
                list.Add(new FloatMenuOption(label, () => SortBy(l, col)));
            }
            return list;
        }

        /// <summary>A kind's heading in a list: its name and how many of its goods are shown.</summary>
        private static void Group(Rect r, Line line)
        {
            Text.Font = GameFont.Tiny;
            float hw = UiKit.KeptWidth(line.head, GameFont.Tiny) + 2f;
            var head = new Rect(r.x + 4f, r.y + 6f, Mathf.Min(hw, r.width - 8f), r.height - 8f);
            UiKit.LabelFit(head, line.head, UiKit.Heading, GameFont.Tiny);
            UiKit.LabelFit(new Rect(head.xMax + 6f, head.y, 60f, head.height), line.headCount, UiKit.Dim, GameFont.Tiny);
            UiKit.Divider(r.x + 2f, r.yMax - 2f, r.width - 4f);
            Text.Font = GameFont.Small;
        }

        private void DrawRow(Rect rect, Row r, ListView l, int nth, float nameW, string focused)
        {
            bool sale = l.sale;
            int here = TradeWindowMath.ThisWay(r.t.CountToTransfer, sale);
            if (UiKit.OwnLook) UiKit.Zebra(rect, nth);
            else if (nth % 2 == 1) Widgets.DrawLightHighlight(rect);
            if (r == flashRow && sale == flashSale && Time.realtimeSinceStartup - flashAt < FlashSeconds)
                Widgets.DrawBoxSolid(rect, UiKit.WithAlpha(UiKit.Gold, 0.22f * (1f - (Time.realtimeSinceStartup - flashAt) / FlashSeconds)));
            if (UiKit.OwnLook) UiKit.RowHover(rect);
            else if (Mouse.IsOver(rect)) Widgets.DrawBoxSolid(rect, UiKit.CardHover);
            // In the deal this list's way: a thin edge in the way's colour down its left.
            if (here > 0) Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), sale ? TradeScreen.SellColor : TradeScreen.BuyColor);
            Widgets.BeginGroup(rect);
            try
            {
                Rect pic = default, info = default, control = default;
                foreach (var p in l.places)
                {
                    var cell = new Rect(p.x, 0f, p.width, rect.height);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.white;
                    switch (p.col)
                    {
                        case TradeColumn.Name:
                            NameCell(cell, r, nameW, out pic, out info);
                            break;
                        case TradeColumn.Ours:
                        case TradeColumn.Theirs:
                            CountCell(cell, sale ? r.ours : r.theirs);
                            break;
                        case TradeColumn.Pay:
                        case TradeColumn.Ask:
                            PriceCell(cell, r, sale);
                            break;
                        case TradeColumn.Deal:
                            DealTag(cell, sale ? r.sell : r.buy);
                            break;
                        case TradeColumn.Need:
                            NeedCell(cell, r, sale);
                            break;
                        case TradeColumn.Count:
                            control = cell;
                            OneWay(cell, r, sale, focused);
                            break;
                    }
                }
                // The row's whole picture on its tooltip (the picture, the card's button and the count keep their own), and the market card beside it.
                var whole = new Rect(0f, 0f, rect.width, rect.height);
                if (Mouse.IsOver(whole) && !Mouse.IsOver(pic) && !Mouse.IsOver(info) && !Mouse.IsOver(control))
                {
                    TooltipHandler.TipRegion(whole, new TipSignal(() => RowTip(r, sale), r.tipId ^ (sale ? 0x5e11 : 0xb0b7)));
                    if (TradeMarket.On) TradeMarket.Hover(r.t, sale ? r.sellCard : r.buyCard, whole);
                }
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.EndGroup();
            }
        }

        private static void Label(Rect cell, string text, Color color, GameFont font = GameFont.Small)
        {
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(cell, UiKit.Fit(text, cell.width - 4f));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>The columns of numbers: right-aligned in Rimfolk's own look, centred in the game's as before.</summary>
        private static bool Number(TradeColumn c) => c == TradeColumn.Ours || c == TradeColumn.Pay || c == TradeColumn.Ask || c == TradeColumn.Theirs;

        /// <summary>A number in its cell: right-aligned in Rimfolk's own look, centred in the game's.</summary>
        private static void Num(Rect cell, string text, Color color, GameFont font = GameFont.Small)
        {
            if (!UiKit.OwnLook)
            {
                Label(cell, text, color, font);
                return;
            }
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = color;
            var r = new Rect(cell.x, cell.y, cell.width - NumberPad, cell.height);
            Widgets.Label(r, UiKit.Fit(text, r.width - 2f));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void Dash(Rect cell)
        {
            if (UiKit.OwnLook) Num(cell, "-", UiKit.Dim);
            else Label(cell, "-", UiKit.Dim);
        }

        /// <summary>
        /// The good's picture (its tooltip the game's own words on it), its whole name wrapped in its column, never cut, the
        /// game's own small icons for an animal or a captive, and the info card's button.
        /// </summary>
        private static void NameCell(Rect cell, Row r, float nameW, out Rect pic, out Rect info)
        {
            var t = r.t;
            pic = new Rect(cell.x + 4f, cell.y + (cell.height - PictureSize) / 2f, PictureSize, PictureSize);
            UiKit.ItemPortrait(pic, t.AnyThing, tip: r.nameTip);
            info = new Rect(cell.xMax - 24f, cell.y + (cell.height - 24f) / 2f, 24f, 24f);
            Widgets.InfoCardButton(info.x, info.y, t.AnyThing);
            if (r.pawn != null)
            {
                // The game's own small icons: a bond, a pregnancy, a ride, a sickness; a ransom or a sale into slavery.
                float curX = info.x - 2f;
                TransferableUIUtility.DoExtraIcons(t, new Rect(0f, cell.y, cell.width, cell.height), ref curX);
                if (ModsConfig.IdeologyActive && TransferableUIUtility.TransferableIsCaptive(t) && (r.pawn.IsSlaveOfColony || r.pawn.IsPrisonerOfColony))
                    TransferableUIUtility.DrawCaptiveTradeInfo(t, TradeSession.trader, new Rect(0f, cell.y, cell.width, cell.height), ref curX);
            }
            var label = new Rect(cell.x + TradeWindowMath.PictureW, cell.y, LabelWidth(r, nameW), cell.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = true;
            GUI.color = r.willTrade ? UiKit.Ink : TradeUI.NoTradeColor;
            Widgets.Label(label, r.label);
            GUI.color = Color.white;
        }

        /// <summary>The game's own tooltip on a good's name.</summary>
        private static string NameTip(Tradeable t)
        {
            if (!t.HasAnyThing) return "";
            string text = t.LabelCap;
            string desc = t.TipDescription;
            if (t.AnyThing is Book) text = desc;
            else if (!desc.NullOrEmpty()) text = text + ": " + desc + TransferableUIUtility.ContentSourceDescription(t.AnyThing);
            var ingredients = t.AnyThing.TryGetComp<CompIngredients>();
            if (ingredients != null) text = text + "\n\n" + ingredients.CompInspectStringExtra();
            return text;
        }

        private static void CountCell(Rect cell, int n)
        {
            if (n == 0) Dash(cell);
            else Num(cell, n.ToStringCached(), UiKit.Ink);
        }

        /// <summary>
        /// What they pay or ask a piece, and under it its mark against the usual price for this negotiator: an arrow up when
        /// above it, down when under, and how far, green when that is better for the colony and orange when worse.
        /// </summary>
        private static void PriceCell(Rect cell, Row r, bool sale)
        {
            if (!(sale ? r.canSell : r.canBuy))
            {
                Dash(cell);
                return;
            }
            int better = sale ? r.payBetter : r.askBetter, pct = sale ? r.payPct : r.askPct;
            string mark = sale ? r.payMark : r.askMark;
            var color = better > 0 ? UiKit.Warm : better < 0 ? UiKit.Soon : UiKit.Ink;
            bool right = UiKit.OwnLook;
            float top = cell.y + (cell.height - 36f) / 2f;
            Text.Font = GameFont.Small;
            Text.Anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(new Rect(cell.x, top, cell.width - (right ? NumberPad : 0f), 20f), sale ? r.payText : r.askText);
            GUI.color = Color.white;
            if (mark == null) return;
            bool arrow = Math.Abs(pct) >= 2;
            float tw = UiKit.KeptWidth(mark, GameFont.Tiny) + 2f, iw = arrow ? 12f : 0f;
            float x0 = right ? cell.xMax - NumberPad - tw - iw : cell.x + (cell.width - tw - iw) / 2f;
            var ink = better != 0 ? color : UiKit.Faint;
            if (arrow) UiKit.Icon(new Rect(x0, top + 23f, 10f, 10f), pct > 0 ? Icons.TrendUp : Icons.TrendDown, ink);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = ink;
            Widgets.Label(new Rect(x0 + iw, top + 19f, tw + 2f, 16f), mark);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static string How(int pct)
        {
            if (Math.Abs(pct) < 2) return W("CC_TradeWin_How_Same");
            return W(pct > 0 ? "CC_TradeWin_How_Above" : "CC_TradeWin_How_Below", Math.Abs(pct).Named("PCT"));
        }

        /// <summary>The game's own breakdown of the price, then Rimfolk's two comparisons in words.</summary>
        private static string PriceTip(Row r, bool sale)
        {
            string game = r.t.GetPriceTooltip(sale ? TradeAction.PlayerSells : TradeAction.PlayerBuys);
            float price = sale ? r.pay : r.ask, usual = sale ? r.usualSell : r.usualBuy;
            if (usual <= 0f) return game;
            string ours = W(sale ? "CC_TradeWin_PriceTipSell" : "CC_TradeWin_PriceTipBuy", TradeWindowMath.ShareOf(price, r.value).Named("SHARE"), Money(r.value).Named("VALUE"),
                Money(usual).Named("USUAL"), How(TradeWindowMath.VsUsual(price, usual)).Named("HOW"));
            return game + "\n\n" + ours;
        }

        /// <summary>
        /// A row's whole picture, on its tooltip: how many there are, its market value and mass, the price's share of the
        /// value with the game's own reckoning, why the deal is called what it is, the market and its trend, and what the
        /// colony uses it for (the advice).
        /// </summary>
        private string RowTip(Row r, bool sale)
        {
            var parts = new List<string>
            {
                W(sale ? "CC_TradeWin_TipSell" : "CC_TradeWin_TipBuy", r.label.Named("GOOD"), (sale ? r.ours : r.theirs).Named("COUNT")),
                W("CC_TradeWin_ValueTip", Money(r.value).Named("VALUE")) + " " + W("CC_TradeWin_TipMass", r.mass.ToStringMass().Named("MASS"))
            };
            if (sale ? r.canSell : r.canBuy) parts.Add(PriceTip(r, sale));
            else if (!r.willTrade) parts.Add("TraderWillNotTrade".Translate());
            if (r.person) parts.Add(W("CC_TradeWin_NoGradePeople"));
            string why = DealTip(r, sale);
            if (!why.NullOrEmpty()) parts.Add(why);
            // The market in words, where the market card (its drawn trend, beside the tooltip) is off.
            if (r.market != null && !TradeMarket.On)
            {
                var lines = TradeMarket.MarketLines(r.market);
                string news = TradeMarket.NewsLine(r.market);
                if (!news.NullOrEmpty()) lines.Add(news);
                if (lines.Count > 0) parts.Add(string.Join("\n", lines));
            }
            if (r.note != null)
            {
                string advice = TradeAdvice.Tip(r.note, r.t);
                if (!advice.NullOrEmpty()) parts.Add(advice);
            }
            else if (!r.needTip.NullOrEmpty()) parts.Add(r.needTip);
            return string.Join("\n\n", parts);
        }

        /// <summary>Why a side's grade: the price against the usual, the market's reason, the colony's record, a cap; then what the grades mean.</summary>
        private static string DealTip(Row r, bool sale)
        {
            var g = sale ? r.sell : r.buy;
            if (!g.Graded) return null;
            float price = sale ? r.pay : r.ask, usual = sale ? r.usualSell : r.usualBuy, best = sale ? r.bestSold : r.bestBought;
            var lines = new List<string>
            {
                W(sale ? "CC_TradeWin_DealSell" : "CC_TradeWin_DealBuy", GradeWord(g.grade, sale).Named("GRADE"), Money(price).Named("PRICE"), Money(usual).Named("USUAL"),
                    How(TradeWindowMath.VsUsual(price, usual)).Named("HOW"))
            };
            if (g.marketStep != 0)
            {
                string hint = TradeMarket.HintLine(r.t.ThingDef, sale);
                if (!hint.NullOrEmpty()) lines.Add(hint);
            }
            if (g.recordStep != 0 && best > 0f)
                lines.Add(W(g.recordStep > 0 ? sale ? "CC_TradeWin_RecBetterSold" : "CC_TradeWin_RecBetterBought" : sale ? "CC_TradeWin_RecWorseSold" : "CC_TradeWin_RecWorseBought",
                    Money(best).Named("PRICE"), TradeMarketMath.BestDays.Named("DAYS")));
            if (g.capped) lines.Add(W(sale ? "CC_TradeWin_CappedSell" : "CC_TradeWin_CappedBuy"));
            lines.Add(W("CC_TradeWin_DealHow"));
            return string.Join("\n", lines);
        }

        private static Color GradeColor(DealGrade g) => g == DealGrade.Great ? Great : g == DealGrade.Good ? UiKit.Warm : g == DealGrade.Fair ? UiKit.Faint : UiKit.Soon;

        private static string GradeWord(DealGrade g, bool sale) => g == DealGrade.None ? "-" : W("CC_TradeWin_Grade_" + g + (sale ? "Sell" : "Buy"));

        /// <summary>A grade's one word on its tag ("good"), made once.</summary>
        private static string TagWord(DealGrade g) => tagWords[(int)g] ??= W("CC_TradeWin_Tag_" + g);

        /// <summary>Three bars for a grade, as many filled as it has (great three, good two, fair one, poor none).</summary>
        private static void Bars(Rect r, DealGrade g)
        {
            int n = TradeWindowMath.Bars(g);
            var c = GradeColor(g);
            for (int i = 0; i < 3; i++)
            {
                float bh = 6f + i * 4f;
                var bar = new Rect(r.x + i * 6f, r.yMax - bh, 4f, bh);
                Widgets.DrawBoxSolid(bar, i < n ? c : BarOff);
            }
        }

        /// <summary>The side's grade as a tag: its bars and its word in its colour (so the colour is never the only sign).</summary>
        private static void DealTag(Rect cell, DealRating g)
        {
            if (!g.Graded)
            {
                Dash(cell);
                return;
            }
            Bars(new Rect(cell.x + 4f, cell.center.y - 7f, 18f, 14f), g.grade);
            UiKit.LabelFit(new Rect(cell.x + 26f, cell.y, cell.width - 28f, cell.height), TagWord(g.grade), GradeColor(g.grade));
        }

        /// <summary>The need's word: short, spare or kept (red when this trade sells into what is set aside); for the trader's people, how they would join.</summary>
        private static void NeedCell(Rect cell, Row r, bool sale)
        {
            if (r.need == null)
            {
                Dash(cell);
                return;
            }
            var n = r.note;
            var color = r.needColor;
            int c = r.t.CountToTransfer;
            // Red when this trade sells into what is set aside, as the old screen's pin.
            if (sale && n != null && n.mark == TradeMark.Keep && c < 0 && n.have + c < n.setAside) color = TradeScreen.WarnColor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = true;
            GUI.color = color;
            Widgets.Label(new Rect(cell.x + 2f, cell.y, cell.width - 4f, cell.height), r.need);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// How many go this list's way, in the game's own counts and limits: the arrows point where the goods go (right to
        /// the trader, left to the colony), so in the sell list ">" sells one more and "<" takes one back, in the buy list
        /// "<" buys one more and ">" puts one back; ten with Shift, a hundred with Ctrl; a double arrow all or none; a number
        /// typed. Never over to the other way: a good in the deal the other way says so, and a click takes it out.
        /// Where the trader won't deal in it, or the negotiator won't sell into slavery, the game's own words.
        /// </summary>
        private void OneWay(Rect cell, Row r, bool sale, string focused)
        {
            var t = r.t;
            var rect = new Rect(cell.x, cell.y + (cell.height - 26f) / 2f, cell.width, 26f);
            if (!r.willTrade)
            {
                WillNot(rect, wontTrade ??= "TraderWillNotTrade".Translate());
                return;
            }
            if (sale && r.noSlaves)
            {
                WillNot(rect, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    TooltipHandler.TipRegion(rect, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
                }
                return;
            }
            int c = t.CountToTransfer;
            var s = r.Of(sale);
            if (TradeWindowMath.OtherWay(c, sale))
            {
                OtherWay(rect, r, sale, c, s);
                return;
            }
            int least = t.GetMinimumToTransfer(), most = t.GetMaximumToTransfer();
            int n = TradeWindowMath.ThisWay(c, sale), top = TradeWindowMath.MostThisWay(sale, least, most);
            int step = GenUI.CurrentAdjustmentMultiplier();
            // One piece (a person, an animal): single arrows only, as the game's own control has.
            bool single = top <= 1;
            bool leftMore = !sale;
            float gap = 2f, field = single ? rect.width - 2f * (ArrowW * 2f + gap) : rect.width - 4f * ArrowW - 4f * gap;
            float x = rect.x;
            Text.Font = GameFont.Small;
            if (!single)
            {
                Arrow(new Rect(x, rect.y, ArrowW, rect.height), "<<", t, leftMore ? TradeWindowMath.CountFor(sale, top, least, most) : 0, leftMore ? n < top : n > 0);
                x += ArrowW + gap;
            }
            float aw = single ? ArrowW * 2f : ArrowW;
            Arrow(new Rect(x, rect.y, aw, rect.height), "<", t, TradeWindowMath.Stepped(sale, c, leftMore, step, least, most), leftMore ? n < top : n > 0);
            x += aw + gap;
            Field(new Rect(x, rect.y, field, rect.height), t, sale, s, n, least, most, focused);
            x += field + gap;
            Arrow(new Rect(x, rect.y, aw, rect.height), ">", t, TradeWindowMath.Stepped(sale, c, !leftMore, step, least, most), leftMore ? n > 0 : n < top);
            x += aw + gap;
            if (!single) Arrow(new Rect(x, rect.y, ArrowW, rect.height), ">>", t, leftMore ? 0 : TradeWindowMath.CountFor(sale, top, least, most), leftMore ? n > 0 : n < top);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>An arrow: greyed where it can't move the count; a click sets the count it gives, by the game's own AdjustTo within its limits.</summary>
        private static void Arrow(Rect r, string label, Tradeable t, int target, bool can)
        {
            bool active = can && target != t.CountToTransfer;
            // Rimfolk's button keeps a margin its narrow arrows don't have room for: the arrow is drawn over it, whole.
            bool clicked = UiKit.Button(r, UiKit.OwnLook ? "" : label, ButtonKind.Normal, active, null, GameFont.Small);
            if (UiKit.OwnLook)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = active ? UiKit.Ink : UiKit.Dim;
                Widgets.Label(r, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            if (!clicked) return;
            if (!t.CanAdjustTo(target).Accepted) return;
            bool more = Math.Abs(target) > Math.Abs(t.CountToTransfer);
            t.AdjustTo(target);
            (more ? SoundDefOf.Tick_High : SoundDefOf.Tick_Low).PlayOneShotOnCamera();
        }

        /// <summary>The number going this way, typed: set as it is typed, kept between none and all there is; an empty box is none.</summary>
        private static void Field(Rect r, Tradeable t, bool sale, RowSide s, int n, int least, int most, string focused)
        {
            bool typing = focused == s.field;
            if (!typing || s.typed == null) s.typed = n.ToStringCached();
            string got = UiKit.TextField(r, s.typed, s.field, null, 7, Digits);
            if (!typing || got == s.typed) return;
            s.typed = got;
            int want = got.Length == 0 ? 0 : int.TryParse(got, out int v) ? v : n;
            int target = TradeWindowMath.CountFor(sale, want, least, most);
            if (got.Length > 0 && Math.Abs(target) != want) s.typed = Math.Abs(target).ToStringCached();
            if (target != t.CountToTransfer && t.CanAdjustTo(target).Accepted) t.AdjustTo(target);
        }

        /// <summary>A good in the deal the other way (one trade either buys or sells it): says so, and a click takes it out of the deal so it can go this way.</summary>
        private static void OtherWay(Rect rect, Row r, bool sale, int c, RowSide s)
        {
            int n = Math.Abs(c);
            if (s.other == null || s.otherFor != n)
            {
                s.other = W(sale ? "CC_TradeWin_OtherBuying" : "CC_TradeWin_OtherSelling", n.Named("COUNT"));
                s.otherFor = n;
            }
            string tip = sale ? otherBuyingTip ??= W("CC_TradeWin_OtherBuyingTip") : otherSellingTip ??= W("CC_TradeWin_OtherSellingTip");
            if (!UiKit.Button(rect, s.other, ButtonKind.Quiet, true, tip, GameFont.Tiny)) return;
            if (!r.t.CanAdjustTo(0).Accepted) return;
            r.t.AdjustTo(0);
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        private static void WillNot(Rect rect, string text)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = TradeUI.NoTradeColor;
            Widgets.Label(rect, text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ---------------- The side: the best deals and the deal ----------------

        private void Side(Rect r)
        {
            if (UiKit.OwnLook) UiKit.OwnCard(r);
            else Widgets.DrawBoxSolid(r, UiKit.CardFill);
            var view = new Rect(0f, 0f, r.width - 16f, Mathf.Max(sideHeight, r.height - 4f));
            Widgets.BeginScrollView(r.ContractedBy(2f), ref sideScroll, view);
            try
            {
                float x = 6f, w = view.width - 12f, y = 6f;
                y = Heading(x, y, w, K("CC_TradeWin_Best"));
                y = BestList(x, y, w, K("CC_TradeWin_BestSell"), bestSells, true, K("CC_TradeWin_BestNoneSell"));
                y = BestList(x, y + 4f, w, K("CC_TradeWin_BestBuy"), bestBuys, false, K("CC_TradeWin_BestNoneBuy"));
                y = Heading(x, y + 10f, w, K("CC_TradeWin_Deal"));
                y = Summary(x, y, w);
                sideHeight = y + 8f;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float Heading(float x, float y, float w, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UiKit.Heading;
            Widgets.Label(new Rect(x, y, w, 24f), text);
            GUI.color = Color.white;
            UiKit.Divider(x, y + 23f, w);
            return y + 28f;
        }

        private float BestList(float x, float y, float w, string title, List<Row> list, bool sale, string none)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x, y, w, 18f), title);
            GUI.color = Color.white;
            y += 18f;
            if (list.Count == 0)
            {
                y = Para(x, y, w, none, UiKit.Dim, GameFont.Tiny);
                return y;
            }
            foreach (var row in list)
            {
                var g = sale ? row.sell : row.buy;
                var line = new Rect(x, y, w, 26f);
                if (UiKit.OwnLook) UiKit.RowHover(line);
                else if (Mouse.IsOver(line)) Widgets.DrawHighlight(line);
                try
                {
                    Widgets.ThingIcon(new Rect(x + 2f, y + 3f, 20f, 20f), row.t.AnyThing);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trade window icon", e);
                }
                float price = sale ? row.pay : row.ask, usual = sale ? row.usualSell : row.usualBuy;
                string pct = TradeWindowMath.Signed(TradeWindowMath.VsUsual(price, usual));
                Text.Font = GameFont.Tiny;
                float pw = Text.CalcSize(pct).x + 4f;
                Bars(new Rect(x + w - pw - 22f, y + 6f, 18f, 14f), g.grade);
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = GradeColor(g.grade);
                Widgets.Label(new Rect(x + w - pw, y, pw, 26f), pct);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.white;
                float lw = w - pw - 24f - 28f;
                Widgets.Label(new Rect(x + 26f, y, lw, 26f), row.label.Truncate(lw));
                Text.Anchor = TextAnchor.UpperLeft;
                var rr = row;
                TooltipHandler.TipRegion(line, new TipSignal(() => W("CC_TradeWin_BestTip", GradeWord(g.grade, sale).CapitalizeFirst().Named("GRADE"), Money(price).Named("PRICE"),
                    How(TradeWindowMath.VsUsual(price, usual)).Named("HOW")), rr.t.GetHashCode() ^ (sale ? 0x15e1 : 0x2b0f)));
                if (Widgets.ButtonInvisible(line))
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    Jump(row, sale);
                }
                y += 26f;
            }
            return y;
        }

        /// <summary>The row in its list (the sell list for a sale, the buy list for a purchase), shown in the middle and lit for a moment: the filters cleared first if they hide it, its tab shown.</summary>
        private void Jump(Row r, bool sale)
        {
            var l = sale ? sellList : buyList;
            if (!Shows(l, r))
            {
                search.Reset();
                category = null;
                inDealOnly = false;
                Refilter();
            }
            if (!Shows(l, r)) return;
            if (!sideNow && tabSell != sale) SetTab(sale);
            l.jumpTo = r;
            flashRow = r;
            flashSale = sale;
            flashAt = Time.realtimeSinceStartup;
        }

        private static bool Shows(ListView l, Row r)
        {
            foreach (var line in l.lines)
                if (line.row == r)
                    return true;
            return false;
        }

        private static float Para(float x, float y, float w, string text, Color color, GameFont font = GameFont.Small, string tip = null)
        {
            Text.Font = font;
            Text.Anchor = TextAnchor.UpperLeft;
            float h = Text.CalcHeight(text, w);
            var r = new Rect(x, y, w, h);
            GUI.color = color;
            Widgets.Label(r, text);
            GUI.color = Color.white;
            if (tip != null) TooltipHandler.TipRegion(r, tip);
            Text.Font = GameFont.Small;
            return y + h + 3f;
        }

        /// <summary>What the deal comes to: what is given and got against market value, against the usual prices, both by market value, the readouts, and what it may do to Vanilla Trading Expanded's prices.</summary>
        private float Summary(float x, float y, float w)
        {
            if (!sums.Any) return Para(x, y, w, K("CC_TradeWin_DealEmpty"), UiKit.Faint, GameFont.Tiny);
            if (sums.soldPieces > 0)
                y = Para(x, y, w, W("CC_TradeWin_Give", sums.soldPieces.Named("COUNT"), Money(sums.soldValue).Named("VALUE"), Money(sums.soldSilver).Named("SILVER"),
                    sums.SoldShare.ToStringPercent().Named("SHARE")), UiKit.Ink);
            if (sums.boughtPieces > 0)
                y = Para(x, y, w, W("CC_TradeWin_Get", sums.boughtPieces.Named("COUNT"), Money(sums.boughtValue).Named("VALUE"), Money(sums.boughtSilver).Named("SILVER"),
                    sums.BoughtShare.ToStringPercent().Named("SHARE")), UiKit.Ink);
            // The net silver as the game counts it for the silver row (its own rounding of the total).
            int net = colonyAfter - colonyNow;
            y = Para(x, y, w, net == 0 ? W("CC_TradeWin_NetEven") : W(net > 0 ? "CC_TradeWin_NetIn" : "CC_TradeWin_NetOut", Mathf.Abs(net).ToStringCached().Named("COUNT")),
                net > 0 ? UiKit.Warm : net < 0 ? UiKit.Soon : UiKit.Faint);
            int lean = TradeWindowMath.Leaning(sums);
            int pct = Mathf.RoundToInt(Mathf.Abs(sums.VsUsual) * 100f);
            y = Para(x, y + 2f, w, lean == 0 ? W("CC_TradeWin_VsUsualSame") : W(lean > 0 ? "CC_TradeWin_VsUsualBetter" : "CC_TradeWin_VsUsualWorse", pct.Named("PCT")),
                lean > 0 ? UiKit.Warm : lean < 0 ? UiKit.Soon : UiKit.Faint, GameFont.Small, K("CC_TradeWin_VsUsualTip"));
            y = Para(x, y, w, W("CC_TradeWin_ByValue", Money(sums.GiveWorth).Named("GIVE"), Money(sums.GetWorth).Named("GET")), UiKit.Faint, GameFont.Tiny, K("CC_TradeWin_ByValueTip"));
            if (traderAfter < 0) y = Para(x, y + 2f, w, W("CC_TradeWin_TraderShort", (-traderAfter).Named("COUNT")), UiKit.Soon, GameFont.Tiny);
            if (colonyAfter < 0) y = Para(x, y + 2f, w, W("CC_TradeWin_ColonyShort", (-colonyAfter).Named("COUNT")), Short, GameFont.Tiny);
            foreach (var line in readouts) y = Para(x, y + 2f, w, line.text, UiKit.Faint, GameFont.Tiny, line.tip);
            foreach (var line in impacts) y = Para(x, y + 2f, w, line, UiKit.Soon, GameFont.Tiny);
            return y;
        }

        // ---------------- The foot ----------------

        private void Foot(Rect r)
        {
            Text.Font = GameFont.Small;
            float mid = r.x + r.width / 2f;
            var accept = new Rect(mid - 80f, r.y + 2f, 160f, 40f);
            var reset = new Rect(accept.x - 170f, accept.y, 160f, 40f);
            var cancel = new Rect(accept.xMax + 10f, accept.y, 160f, 40f);
            if (TradeScreen.BasketOn)
            {
                float bw = Mathf.Min(140f, (reset.x - r.x - 20f) / 2f);
                if (bw >= 80f)
                {
                    var fill = new Rect(r.x, accept.y, bw, 40f);
                    var clear = new Rect(fill.xMax + 8f, accept.y, bw, 40f);
                    bool any = TradeScreen.AnySuggested, some = TradeScreen.HasBasket;
                    if (UiKit.Button(fill, K("CC_Trade_Fill"), ButtonKind.Normal, any))
                    {
                        if (any)
                        {
                            TradeScreen.Fill(null);
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        }
                        else SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                    TooltipHandler.TipRegion(fill, K(any ? "CC_Trade_FillTip" : "CC_Trade_FillNone"));
                    if (UiKit.Button(clear, K("CC_Trade_Clear"), ButtonKind.Normal, some))
                    {
                        if (some)
                        {
                            TradeScreen.Clear(null);
                            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                        }
                        else SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                    TooltipHandler.TipRegion(clear, K("CC_Trade_ClearTip"));
                }
            }
            if (UiKit.Button(reset, "ResetButton".Translate(), ButtonKind.Quiet))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                deal.Reset();
                Build();
            }
            if (UiKit.Button(accept, "AcceptButton".Translate(), ButtonKind.Primary))
            {
                Accept();
                Event.current.Use();
            }
            if (UiKit.Button(cancel, "CancelButton".Translate()))
            {
                Close();
                Event.current.Use();
            }
            Silver(new Rect(cancel.xMax + 16f, r.y, r.xMax - cancel.xMax - 16f, r.height));
        }

        private const float WalletH = 112f;

        /// <summary>The colony short of silver: the game's red, or the own look's danger (with its words, never alone).</summary>
        private static Color Short => UiKit.OwnLook ? UiKit.Danger : TradeScreen.WarnColor;

        /// <summary>
        /// The colony's purse at a glance: its silver now, an arrow, its silver after this trade, both large; under them in
        /// words and colour whether it can pay (green, or red with how much is missing), and the trader's purse
        /// small below, orange when they can't pay all of what they owe. Lit when the game says the silver won't do.
        /// </summary>
        private void Wallet(Rect r)
        {
            bool shortOurs = colonyAfter < 0, shortTheirs = traderAfter < 0;
            if (UiKit.OwnLook)
            {
                // Rimfolk's own look: a raised card with its soft shadow, washed and edged in the warning's colour when short.
                UiKit.SoftShadow(r, 0.7f);
                UiKit.RoundBox(r, UiKit.Raised, shortOurs ? Short : UiKit.Edge, SkinMath.RadiusCard);
                if (shortOurs) UiKit.Round(r.ContractedBy(1f), UiKit.WithAlpha(Short, 0.12f), SkinMath.RadiusCard);
            }
            else
                Widgets.DrawBoxSolidWithOutline(r, shortOurs ? new Color(0.45f, 0.12f, 0.1f, 0.35f) : UiKit.CardFill, shortOurs ? TradeScreen.WarnColor : UiKit.CardBorder, 1);
            if (Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f) GUI.DrawTexture(r, TransferableUIUtility.FlashTex);
            var inner = r.ContractedBy(10f);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 18f), K("CC_TradeWin_WalletTitle"));
            Text.Font = GameFont.Medium;
            float numY = inner.y + 18f;
            string now = colonyNow.ToStringCached(), after = colonyAfter.ToStringCached();
            GUI.color = UiKit.Ink;
            float nowW = Text.CalcSize(now).x;
            Widgets.Label(new Rect(inner.x, numY, nowW + 4f, 34f), now);
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(inner.x + nowW + 8f, numY, 30f, 34f), "→");
            GUI.color = shortOurs ? Short : colonyAfter < colonyNow ? UiKit.Gold : UiKit.Good;
            Widgets.Label(new Rect(inner.x + nowW + 38f, numY, inner.width - nowW - 38f, 34f), after);
            Text.Font = GameFont.Small;
            float lineY = numY + 34f;
            int diff = colonyAfter - colonyNow;
            string verdict = shortOurs ? W("CC_TradeWin_WalletShort", (-colonyAfter).Named("COUNT"))
                : diff < 0 ? W("CC_TradeWin_WalletPay", (-diff).Named("COUNT"))
                : diff > 0 ? W("CC_TradeWin_WalletGet", diff.Named("COUNT"))
                : W("CC_TradeWin_WalletEven");
            GUI.color = shortOurs ? Short : UiKit.Good;
            Widgets.Label(new Rect(inner.x, lineY, inner.width, 22f), verdict);
            Text.Font = GameFont.Tiny;
            GUI.color = shortTheirs ? UiKit.Soon : UiKit.Faint;
            Widgets.Label(new Rect(inner.x, lineY + 22f, inner.width, 18f),
                shortTheirs ? W("CC_TradeWin_WalletTheirsShort", traderNow.ToStringCached().Named("NOW"), (-traderAfter).Named("COUNT"))
                : W("CC_TradeWin_WalletTheirs", traderNow.ToStringCached().Named("NOW"), traderAfter.ToStringCached().Named("AFTER")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, K("CC_TradeWin_SilverTip"));
        }

        /// <summary>Each side's silver now and after the trade, the colony's above, red where a side runs short; lit when the game says the silver won't do.</summary>
        private void Silver(Rect r)
        {
            if (r.width < 120f || currency == null) return;
            if (Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f) GUI.DrawTexture(r, TransferableUIUtility.FlashTex);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            var top = new Rect(r.x, r.y + 2f, r.width - 4f, r.height / 2f - 2f);
            var bottom = new Rect(r.x, r.y + r.height / 2f, r.width - 4f, r.height / 2f - 2f);
            GUI.color = colonyAfter < 0 ? Short : UiKit.Ink;
            Widgets.Label(top, W("CC_TradeWin_SilverOurs", colonyNow.ToStringCached().Named("NOW"), colonyAfter.ToStringCached().Named("AFTER")).Truncate(top.width));
            GUI.color = traderAfter < 0 ? UiKit.Soon : UiKit.Faint;
            Widgets.Label(bottom, W("CC_TradeWin_SilverTheirs", traderNow.ToStringCached().Named("NOW"), traderAfter.ToStringCached().Named("AFTER")).Truncate(bottom.width));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, K("CC_TradeWin_SilverTip"));
        }

        /// <summary>
        /// The game's own Accept: with the trader short of silver, its own question first; then its TryExecute, which
        /// asks the negotiator's ideoligion about organs, pays, hands the goods over (by pod or into the caravan) and
        /// tells the faction. The game's own check that the colony can pay comes first here, since the game flashes the
        /// silver of its own screen when it can't, and this window isn't that screen.
        /// </summary>
        private void Accept()
        {
            void Go()
            {
                deal.UpdateCurrencyCount();
                var silver = deal.CurrencyTradeable;
                if (silver == null || silver.CountPostDealFor(Transactor.Colony) < 0)
                {
                    Dialog_Trade.lastCurrencyFlashTime = Time.time;
                    Messages.Message("MessageColonyCannotAfford".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (!deal.TryExecute(out bool actuallyTraded)) return;
                if (actuallyTraded)
                {
                    SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
                    TradeSession.playerNegotiator.GetCaravan()?.RecacheInventory();
                    Close(false);
                }
                else Close();
            }

            if (deal.DoesTraderHaveEnoughSilver()) Go();
            else
            {
                Dialog_Trade.lastCurrencyFlashTime = Time.time;
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmTraderShortFunds".Translate(), Go));
            }
        }
    }
}
