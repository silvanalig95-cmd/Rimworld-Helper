using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own trade window (DESIGN.md "The trade screen"; opened in place of the game's by Trade/TradeWindow.cs).
    /// One table with the colony's side left of the count and the trader's right of it, as the game's arrows go: each
    /// good's name and kind, its market value a piece, how many the colony has and what the trader pays, the game's
    /// own count control, what the trader asks and how many it has, the deal's grade, Vanilla Trading Expanded's market
    /// and trend, the mass and what the colony needs of it. Every price is the game's own (Tradeable.GetPriceFor), shown
    /// with its share of the market value and against the usual price for this negotiator; every count is set through
    /// the game's own control and the game's deal, and Accept is the game's own path (TryExecute, its confirmations,
    /// the negotiator, the caravan and the pods). A search, kind chips and "only in the deal" above; column headers that
    /// sort; on the right the best deals both ways (a click finds the row) and the deal's summary; at the foot Fill
    /// suggested, Clear suggested, Reset, Accept, Cancel and each side's silver after the trade. The rules are
    /// <see cref="TradeWindowMath"/>'s; the advice, the market and the record are Trade/'s own, read here and not
    /// worked out again. If anything fails while it draws, the game's own screen takes the trade over. It is in Rimfolk's
    /// frame (UI/WindowFrame.cs): moved by its title bar, sized from its edges, large and back, where it was left, and in
    /// Rimfolk's own look while that is on (docs/design/rimfolk-look.md, stage 1); with both settings off, as it was.
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
            public bool person, willTrade, canSell, canBuy;
            public int ours, theirs;
            public float value, mass, pay, ask, usualSell, usualBuy, bestSold, bestBought;
            public DealRating sell, buy;
            public TradeNote note;
            public MarketView market;
            public float? trend;
            public TradeRowKeys keys;
        }

        private const float TopH = 52f, FootH = 44f, RowH = 36f, HeadH = 36f, ChipH = 24f, Gap = 4f, CaravanH = 40f, SideGap = 14f;

        /// <summary>The least the window is sized to: the foot's buttons and the silver, the table's name and count, the side.</summary>
        public const float LeastW = WindowFrameMath.TradeLeastW, LeastH = WindowFrameMath.TradeLeastH;

        /// <summary>The top's height: the title, the negotiator and the buttons; in the frame, the title is in the title bar.</summary>
        private static float Top => WindowFrame.Shown ? 40f : TopH;
        private const int BestMax = 5;
        private const float FlashSeconds = 1.6f;

        private static readonly Color Great = new Color(0.32f, 0.95f, 0.5f), BarOff = new Color(1f, 1f, 1f, 0.14f);

        /// <summary>The column clicked last, kept for the session's next trade.</summary>
        private static TradeColumn sortCol = TradeColumn.Category;
        private static bool sortDesc;

        private static Texture2D sellableIcon, sortUp, sortDown;

        private readonly Dialog_Trade game;
        private readonly bool first;
        private readonly TradeDeal deal;
        private readonly bool playerIsCaravan, market;
        private readonly List<Thing> caravanThings;
        private readonly List<Row> rows = new List<Row>();
        private List<Row> shown = new List<Row>();
        private readonly List<Row> bestSells = new List<Row>(), bestBuys = new List<Row>();
        private readonly List<Tradeable> goods = new List<Tradeable>();
        /// <summary>The chips: everything, then each kind this trade has, with how many goods of it.</summary>
        private readonly List<(TradeCategory? cat, int n)> chips = new List<(TradeCategory?, int)>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Tradeable currency;
        private TradeCategory? category;
        private bool inDealOnly, failed, switching, caravanDirty = true;
        private int stamp = int.MinValue;
        private Vector2 listScroll, sideScroll;
        private float listViewH = 400f, sideHeight = 600f;
        private Row flashRow;
        private float flashAt = -99f;
        private List<ColumnPlace> places;
        private float placesWidth = -1f;
        /// <summary>The chips' words and widths, made once a trade.</summary>
        private readonly List<string> chipLabels = new List<string>();
        private readonly List<float> chipWidths = new List<float>();

        // The deal as it stands, worked out when a count changes.
        private DealSums sums = new DealSums();
        private int colonyNow, colonyAfter, traderNow, traderAfter;
        private readonly List<string> impacts = new List<string>();
        private List<(string text, string tip)> readouts = new List<(string, string)>();
        private CaravanUIUtility.CaravanInfo caravanInfo;

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

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

        /// <summary>Every good of the trade a row, as the game's own screen lists them (what the trader won't deal in left out where its kind hides it).</summary>
        private void Build()
        {
            rows.Clear();
            goods.Clear();
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
                rows.Add(Read(t, notes, book, now));
            }
            chips.Clear();
            chipLabels.Clear();
            chipWidths.Clear();
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

        private Row Read(Tradeable t, Dictionary<Tradeable, TradeNote> notes, TradeBook book, int now)
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
            r.keys = new TradeRowKeys
            {
                name = r.label, category = (int)r.cat, ours = r.ours, theirs = r.theirs, value = r.value, mass = r.mass,
                pay = r.canSell ? r.pay : (float?)null, ask = r.canBuy ? r.ask : (float?)null, deal = TradeWindowMath.DealKey(r.sell, r.buy),
                market = stats != null && stats.known ? stats.vsUsual : (float?)null, trend = r.trend,
                need = n != null ? TradeAdviceMath.SortScore(n.mark, n.priority) : (float?)null
            };
            return r;
        }

        /// <summary>A thing's kind for the chips (TradeWindowMath.CategoryOf): read from the game's own def and categories, so every mod's goods find theirs.</summary>
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

        /// <summary>The rows shown: the kind chosen, the search, only what is in the deal if asked; in the column's order, what the trader won't deal in last.</summary>
        private void Refilter()
        {
            var list = rows.Where(r => (category == null || r.cat == category.Value) && (!inDealOnly || r.t.CountToTransfer != 0)
                                       && (!search.filter.Active || search.filter.Matches(r.label))).ToList();
            var order = TradeWindowMath.Order(list.Select(r => r.keys).ToList(), sortCol, sortDesc);
            var sorted = order.Select(i => list[i]).ToList();
            shown = sorted.Where(r => r.willTrade).Concat(sorted.Where(r => !r.willTrade)).ToList();
            search.noResultsMatched = shown.Count == 0 && search.filter.Active;
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
            foreach (var r in rows)
            {
                var t = r.t;
                int c = t.CountToTransfer;
                if (c == 0) continue;
                var action = t.ActionToDo;
                bool sold = action == TradeAction.PlayerSells;
                lines.Add(new DealLine(sold, Math.Abs(c), t.GetPriceFor(action), sold ? r.usualSell : r.usualBuy, r.value));
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
            if (inDealOnly) Refilter();
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
            if (places == null || Mathf.Abs(placesWidth - (tableW - 16f)) > 0.5f)
            {
                placesWidth = tableW - 16f;
                places = TradeWindowMath.Fit(Specs(), placesWidth, Gap);
            }
            Header(new Rect(0f, y, placesWidth, HeadH));
            y += HeadH + 2f;
            List(new Rect(0f, y, tableW, footY - y - 6f));
            Foot(new Rect(0f, footY, w, FootH));
        }

        /// <summary>The columns and how soon each is left out of a narrow window (the name, the count and both sides' numbers stay).</summary>
        private List<ColumnSpec> Specs()
        {
            var l = new List<ColumnSpec>
            {
                new ColumnSpec(TradeColumn.Name, 140f, 0, 330f),
                new ColumnSpec(TradeColumn.Category, 82f, 11),
                new ColumnSpec(TradeColumn.Value, 76f, 7),
                new ColumnSpec(TradeColumn.Ours, 54f, 1),
                new ColumnSpec(TradeColumn.Pay, 92f, 2),
                new ColumnSpec(TradeColumn.Count, 212f, 0),
                new ColumnSpec(TradeColumn.Ask, 92f, 2),
                new ColumnSpec(TradeColumn.Theirs, 54f, 1),
                new ColumnSpec(TradeColumn.Deal, 104f, 3)
            };
            if (market)
            {
                l.Add(new ColumnSpec(TradeColumn.Market, 84f, 9));
                l.Add(new ColumnSpec(TradeColumn.Trend, 76f, 10));
            }
            l.Add(new ColumnSpec(TradeColumn.Mass, 64f, 12));
            l.Add(new ColumnSpec(TradeColumn.Need, 92f, 8));
            return l;
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
            if (UiKit.Button(toGame, W("CC_TradeWin_ToGame"), ButtonKind.Quiet))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                TradeWindow.ToGame(this, game);
                return false;
            }
            TooltipHandler.TipRegion(toGame, W("CC_TradeWin_ToGameTip"));
            var faction = trader.Faction;
            if (faction != null && !faction.def.permanentEnemy)
            {
                var gifts = new Rect(x - 120f, by, 120f, bh);
                x = gifts.x - 8f;
                if (UiKit.Button(gifts, W("CC_TradeWin_Gifts")))
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
                if (UiKit.Button(mk, W("CC_Trade_MarketButton")) && Find.WindowStack.WindowOfType<Window_TradeMarket>() == null)
                    Find.WindowStack.Add(new Window_TradeMarket());
                TooltipHandler.TipRegion(mk, W(TradeMarket.History ? "CC_Trade_MarketButtonTip" : "CC_Trade_MarketButtonTipNoVte"));
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
                foreach (var c in cats)
                {
                    string l = (c.cat == null ? W("CC_TradeWin_Cat_All") : W("CC_TradeWin_Cat_" + c.cat.Value)) + " " + c.n.ToStringCached();
                    chipLabels.Add(l);
                    chipWidths.Add(Text.CalcSize(l).x + 16f);
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
                if (c.cat != null) TooltipHandler.TipRegion(cr, W("CC_TradeWin_ChipTip", W("CC_TradeWin_Cat_" + c.cat.Value).Named("LABEL")));
                if (Widgets.ButtonInvisible(cr))
                {
                    category = on || c.cat == null ? null : c.cat;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    Refilter();
                }
            }
            Text.Font = GameFont.Small;
            var inDeal = new Rect(r.xMax - inDealW, r.y + 2f, inDealW, 24f);
            if (UiKit.Toggle(inDeal, W("CC_TradeWin_InDeal"), ref inDealOnly, W("CC_TradeWin_InDealTip"))) Refilter();
            return height;
        }

        // ---------------- The table ----------------

        private static string HeadKey(TradeColumn c) => "CC_TradeWin_Col_" + c;

        /// <summary>A column's second line under its name, saying what the cell's small numbers are; null for none.</summary>
        private static string Legend(TradeColumn c)
        {
            switch (c)
            {
                case TradeColumn.Value:
                case TradeColumn.Mass:
                    return W("CC_TradeWin_Leg_Piece");
                case TradeColumn.Pay:
                case TradeColumn.Ask:
                    return W("CC_TradeWin_Leg_Price");
                case TradeColumn.Deal:
                    return W("CC_TradeWin_Leg_Deal");
                case TradeColumn.Market:
                    return W("CC_TradeWin_Leg_Market");
                case TradeColumn.Trend:
                    return W("CC_TradeWin_Leg_Trend");
                default:
                    return null;
            }
        }

        private void Header(Rect r)
        {
            sortUp ??= ContentFinder<Texture2D>.Get("UI/Icons/Sorting", false);
            sortDown ??= ContentFinder<Texture2D>.Get("UI/Icons/SortingDescending", false);
            foreach (var p in places)
            {
                var cell = new Rect(r.x + p.x, r.y, p.width, r.height);
                bool sortable = TradeWindowMath.Sortable(p.col), active = sortable && sortCol == p.col;
                if (sortable && Mouse.IsOver(cell)) Widgets.DrawHighlight(cell);
                string label = p.col == TradeColumn.Count ? "PositiveBuysNegativeSells".Translate().Resolve() : W(HeadKey(p.col));
                string legend = Legend(p.col);
                Text.Font = GameFont.Tiny;
                bool number = UiKit.OwnLook && Number(p.col);
                Text.Anchor = p.col == TradeColumn.Name ? TextAnchor.MiddleLeft : number ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                var top = new Rect(cell.x + (p.col == TradeColumn.Name ? 32f : 0f), cell.y + 2f, cell.width - (p.col == TradeColumn.Name ? 32f : 0f) - (number ? NumberPad + (active ? 10f : 0f) : 0f),
                    legend != null ? 18f : cell.height - 4f);
                GUI.color = active ? UiKit.Gold : UiKit.Heading;
                Widgets.Label(top, label.Truncate(top.width));
                if (legend != null)
                {
                    GUI.color = UiKit.Dim;
                    Widgets.Label(new Rect(top.x, top.yMax - 2f, top.width, 16f), legend.Truncate(top.width));
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (active)
                {
                    var icon = sortDesc ? sortDown : sortUp;
                    if (icon != null) GUI.DrawTexture(new Rect(cell.xMax - 12f, cell.y + 4f, 10f, 10f), icon);
                    if (UiKit.OwnLook) UiKit.Round(new Rect(cell.x + 4f, cell.yMax - 3f, cell.width - 8f, 3f), UiKit.Gold, SkinMath.RadiusSmall);
                    else Widgets.DrawBoxSolid(new Rect(cell.x + 2f, cell.yMax - 2f, cell.width - 4f, 2f), UiKit.Gold);
                }
                string tip = p.col == TradeColumn.Count ? W("CC_TradeWin_Col_CountTip") : W(HeadKey(p.col) + "Tip");
                if (sortable) tip += "\n\n" + W(active ? "CC_TradeWin_Sorted" : "CC_TradeWin_SortBy");
                TooltipHandler.TipRegion(cell, tip);
                if (sortable && Widgets.ButtonInvisible(cell))
                {
                    if (sortCol == p.col) sortDesc = !sortDesc;
                    else
                    {
                        sortCol = p.col;
                        sortDesc = TradeWindowMath.DefaultDescending(p.col);
                    }
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    Refilter();
                }
            }
            Text.Font = GameFont.Small;
            UiKit.Divider(r.x, r.yMax, r.width);
        }

        private void List(Rect area)
        {
            listViewH = area.height;
            if (shown.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UiKit.Faint;
                Widgets.Label(area, W(rows.Count == 0 ? "CC_TradeWin_NoGoods" : "CC_TradeWin_NoneShown"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var view = new Rect(0f, 0f, area.width - 16f, shown.Count * RowH);
            Widgets.BeginScrollView(area, ref listScroll, view);
            TradeWindow.Drawing = true;
            try
            {
                for (int i = 0; i < shown.Count; i++)
                {
                    float top = i * RowH;
                    if (top + RowH < listScroll.y || top > listScroll.y + area.height) continue;
                    DrawRow(new Rect(0f, top, view.width, RowH), shown[i], i);
                }
            }
            finally
            {
                TradeWindow.Drawing = false;
                Widgets.EndScrollView();
            }
        }

        private void DrawRow(Rect rect, Row r, int index)
        {
            if (UiKit.OwnLook) UiKit.Zebra(rect, index);
            else if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
            if (r == flashRow && Time.realtimeSinceStartup - flashAt < FlashSeconds)
                Widgets.DrawBoxSolid(rect, UiKit.WithAlpha(UiKit.Gold, 0.22f * (1f - (Time.realtimeSinceStartup - flashAt) / FlashSeconds)));
            if (UiKit.OwnLook) UiKit.RowHover(rect);
            else if (Mouse.IsOver(rect)) Widgets.DrawBoxSolid(rect, UiKit.CardHover);
            Widgets.BeginGroup(rect);
            try
            {
                foreach (var p in places)
                {
                    var cell = new Rect(p.x, 0f, p.width, rect.height);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.white;
                    switch (p.col)
                    {
                        case TradeColumn.Name:
                            NameCell(cell, r);
                            break;
                        case TradeColumn.Category:
                            Tiny(cell, W("CC_TradeWin_Cat_" + r.cat), UiKit.Faint);
                            break;
                        case TradeColumn.Value:
                            Num(cell, Money(r.value), UiKit.Ink);
                            TooltipHandler.TipRegion(cell, new TipSignal(() => ValueTip(r), r.t.GetHashCode() ^ 0x51a7e));
                            break;
                        case TradeColumn.Ours:
                            CountCell(cell, r.ours, "ColonyCount");
                            break;
                        case TradeColumn.Pay:
                            PriceCell(cell, r, true);
                            break;
                        case TradeColumn.Count:
                            CountControl(cell, r, index);
                            break;
                        case TradeColumn.Ask:
                            PriceCell(cell, r, false);
                            break;
                        case TradeColumn.Theirs:
                            CountCell(cell, r.theirs, "TraderCount");
                            break;
                        case TradeColumn.Deal:
                            DealCell(cell, r);
                            break;
                        case TradeColumn.Market:
                            MarketCell(cell, r);
                            break;
                        case TradeColumn.Trend:
                            TrendCell(cell, r);
                            break;
                        case TradeColumn.Mass:
                            Num(cell, r.mass.ToString("0.##") + " " + "kg".Translate(), UiKit.Faint, GameFont.Tiny);
                            break;
                        case TradeColumn.Need:
                            NeedCell(cell, r);
                            break;
                    }
                }
            }
            finally
            {
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                Widgets.EndGroup();
            }
        }

        private static void Label(Rect cell, string text, Color color, GameFont font = GameFont.Small)
        {
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(cell, text.Truncate(cell.width - 4f));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void Tiny(Rect cell, string text, Color color) => Label(cell, text, color, GameFont.Tiny);

        /// <summary>Room kept at a number's right in its column (Rimfolk's own look right-aligns them).</summary>
        private const float NumberPad = 8f;

        /// <summary>The columns of numbers: right-aligned in Rimfolk's own look, centred in the game's as before.</summary>
        private static bool Number(TradeColumn c) =>
            c == TradeColumn.Value || c == TradeColumn.Ours || c == TradeColumn.Pay || c == TradeColumn.Ask || c == TradeColumn.Theirs || c == TradeColumn.Mass;

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
            Widgets.Label(r, text.Truncate(r.width - 2f));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static void Dash(Rect cell)
        {
            if (UiKit.OwnLook) Num(cell, "-", UiKit.Dim);
            else Label(cell, "-", UiKit.Dim);
        }

        /// <summary>The icon, the name, the game's own icons for an animal or a captive, and the info card: the game's own tooltip on the name.</summary>
        private static void NameCell(Rect cell, Row r)
        {
            var t = r.t;
            var icon = new Rect(cell.x + 2f, cell.y + (cell.height - 27f) / 2f, 27f, 27f);
            try
            {
                Widgets.ThingIcon(icon, t.AnyThing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade window icon", e);
            }
            float curX = cell.xMax - 26f;
            Widgets.InfoCardButton(curX, cell.y + (cell.height - 24f) / 2f, t.AnyThing);
            curX -= 2f;
            // The game's own small icons: a bond, a pregnancy, a ride, a sickness; a ransom or a sale into slavery.
            TransferableUIUtility.DoExtraIcons(t, new Rect(0f, cell.y, cell.width, cell.height), ref curX);
            if (ModsConfig.IdeologyActive && TransferableUIUtility.TransferableIsCaptive(t) && r.pawn != null && (r.pawn.IsSlaveOfColony || r.pawn.IsPrisonerOfColony))
                TransferableUIUtility.DrawCaptiveTradeInfo(t, TradeSession.trader, new Rect(0f, cell.y, cell.width, cell.height), ref curX);
            var label = new Rect(cell.x + 32f, cell.y, Mathf.Max(20f, curX - cell.x - 34f), cell.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            GUI.color = r.willTrade ? UiKit.Ink : TradeUI.NoTradeColor;
            Widgets.Label(label, r.label.Truncate(label.width));
            GUI.color = Color.white;
            Text.WordWrap = true;
            if (Mouse.IsOver(label))
            {
                Widgets.DrawHighlight(label);
                TooltipHandler.TipRegion(label, new TipSignal(() => NameTip(t), t.GetHashCode()));
            }
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

        private static void CountCell(Rect cell, int n, string tipKey)
        {
            if (n == 0)
            {
                Dash(cell);
                return;
            }
            Num(cell, n.ToStringCached(), UiKit.Ink);
            if (Mouse.IsOver(cell)) Widgets.DrawHighlight(cell);
            TooltipHandler.TipRegionByKey(cell, tipKey);
        }

        /// <summary>What they pay or ask a piece, and under it that price's share of the market value and its side of the usual price, coloured by whether it is better or worse for the colony.</summary>
        private void PriceCell(Rect cell, Row r, bool sale)
        {
            bool has = sale ? r.canSell : r.canBuy;
            if (!has)
            {
                Dash(cell);
                return;
            }
            float price = sale ? r.pay : r.ask, usual = sale ? r.usualSell : r.usualBuy;
            int better = TradeWindowMath.Better(sale, price, usual);
            var color = better > 0 ? UiKit.Warm : better < 0 ? UiKit.Soon : UiKit.Ink;
            bool right = UiKit.OwnLook;
            Text.Font = GameFont.Small;
            Text.Anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(new Rect(cell.x, cell.y + 1f, cell.width - (right ? NumberPad : 0f), 20f), Money(price));
            int share = TradeWindowMath.ShareOf(price, r.value);
            string a = share >= 0 ? share + "%" : "-", b = usual > 0f ? TradeWindowMath.Signed(TradeWindowMath.VsUsual(price, usual)) : "-";
            Text.Font = GameFont.Tiny;
            float wa = Text.CalcSize(a + " · ").x, wb = Text.CalcSize(b).x;
            float x0 = right ? cell.xMax - NumberPad - wa - wb : cell.x + (cell.width - wa - wb) / 2f;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x0, cell.y + 19f, wa + 2f, 16f), a + " · ");
            GUI.color = better != 0 ? color : UiKit.Faint;
            Widgets.Label(new Rect(x0 + wa, cell.y + 19f, wb + 2f, 16f), b);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                var t = r.t;
                TooltipHandler.TipRegion(cell, new TipSignal(() => PriceTip(r, sale), t.GetHashCode() * 297 + (sale ? 1 : 2)));
            }
            // The market card beside the mouse, as on the game's own screen, with its own setting.
            if (TradeMarket.On) TradeMarket.Hover(r.t, (r.t, sale ? 0 : 1), cell);
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

        private string ValueTip(Row r)
        {
            string tip = W("CC_TradeWin_ValueTip", Money(r.value).Named("VALUE"));
            if (r.market != null)
            {
                var lines = TradeMarket.MarketLines(r.market);
                if (lines.Count > 0) tip += "\n\n" + string.Join("\n", lines);
            }
            return tip;
        }

        /// <summary>The game's own count control, or the game's own words where it won't trade (the trader doesn't deal in it, or the negotiator won't sell into slavery).</summary>
        private void CountControl(Rect cell, Row r, int index)
        {
            var t = r.t;
            var rect = new Rect(cell.x, cell.y + (cell.height - 30f) / 2f, cell.width, 30f);
            if (!r.willTrade)
            {
                WillNot(rect, "TraderWillNotTrade".Translate());
                return;
            }
            if (ModsConfig.IdeologyActive && TransferableUIUtility.TradeIsPlayerSellingToSlavery(t, TradeSession.trader.Faction)
                                          && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                WillNot(rect, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    TooltipHandler.TipRegion(rect, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
                }
                return;
            }
            Text.Font = GameFont.Small;
            TransferableUIUtility.DoCountAdjustInterface(rect, t, index, t.GetMinimumToTransfer(), t.GetMaximumToTransfer());
            Text.Anchor = TextAnchor.UpperLeft;
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

        private static Color GradeColor(DealGrade g) => g == DealGrade.Great ? Great : g == DealGrade.Good ? UiKit.Warm : g == DealGrade.Fair ? UiKit.Faint : UiKit.Soon;

        private static string GradeWord(DealGrade g, bool sale) => g == DealGrade.None ? "-" : W("CC_TradeWin_Grade_" + g + (sale ? "Sell" : "Buy"));

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

        /// <summary>The row's better side's grade, in bars and words, and under it the other side's.</summary>
        private void DealCell(Rect cell, Row r)
        {
            if (!r.sell.Graded && !r.buy.Graded)
            {
                Dash(cell);
                if (r.person) TooltipHandler.TipRegion(cell, W("CC_TradeWin_NoGradePeople"));
                return;
            }
            // The side being traded leads once a count is set; the better one until then.
            int c = r.t.CountToTransfer;
            bool sale = c < 0 && r.sell.Graded || c == 0 && TradeWindowMath.SaleIsBetter(r.sell, r.buy) || c > 0 && !r.buy.Graded;
            var main = sale ? r.sell : r.buy;
            var other = sale ? r.buy : r.sell;
            Bars(new Rect(cell.x + 6f, cell.y + 5f, 18f, 14f), main.grade);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = GradeColor(main.grade);
            Widgets.Label(new Rect(cell.x + 28f, cell.y, cell.width - 30f, 22f), GradeWord(main.grade, sale).Truncate(cell.width - 30f));
            if (other.Graded)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Dim;
                Widgets.Label(new Rect(cell.x + 28f, cell.y + 18f, cell.width - 30f, 16f), GradeWord(other.grade, !sale).Truncate(cell.width - 30f));
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, new TipSignal(() => DealTip(r), r.t.GetHashCode() ^ 0x3d1a7));
            }
        }

        /// <summary>Why each side's grade: the price against the usual, the market's reason, the colony's record, a cap; then the advice's own reasons.</summary>
        private static string DealTip(Row r)
        {
            var parts = new List<string>();
            foreach (bool sale in new[] { true, false })
            {
                var g = sale ? r.sell : r.buy;
                if (!g.Graded) continue;
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
                parts.Add(string.Join("\n", lines));
            }
            parts.Add(W("CC_TradeWin_DealHow"));
            if (r.note != null)
            {
                string advice = TradeAdvice.Tip(r.note, r.t);
                if (!advice.NullOrEmpty()) parts.Add(advice);
            }
            return string.Join("\n\n", parts);
        }

        /// <summary>Vanilla Trading Expanded's market value now against the good's usual worth, and against its average of the last 30 days.</summary>
        private static void MarketCell(Rect cell, Row r)
        {
            var s = r.market?.stats;
            if (s == null || !s.known)
            {
                Dash(cell);
                return;
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(new Rect(cell.x, cell.y + 2f, cell.width, 16f), W("CC_TradeWin_Mkt_Usual", TradeWindowMath.Signed(TradeMarketMath.Percent(s.vsUsual)).Named("PCT")));
            GUI.color = UiKit.Faint;
            if (s.days >= 2)
                Widgets.Label(new Rect(cell.x, cell.y + 18f, cell.width, 16f), W("CC_TradeWin_Mkt_Avg", TradeWindowMath.Signed(TradeMarketMath.Percent(s.vsAverage)).Named("PCT")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, new TipSignal(() => string.Join("\n", TradeMarket.MarketLines(r.market)), r.t.GetHashCode() ^ 0x7e11d));
            }
        }

        /// <summary>A line of the last 30 days on Vanilla Trading Expanded's market, its colour the way it went, and its change over them.</summary>
        private static void TrendCell(Rect cell, Row r)
        {
            var v = r.market;
            if (v?.points == null || v.points.Count < 2 || !r.trend.HasValue)
            {
                Dash(cell);
                return;
            }
            int n = Math.Min(v.points.Count, TradeMarketMath.MonthDays + 1);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = v.points.Count - n; i < v.points.Count; i++)
            {
                min = Mathf.Min(min, v.points[i]);
                max = Mathf.Max(max, v.points[i]);
            }
            if (max - min < 0.0001f) max = min + 1f;
            var color = UiKit.DaysColor(v.stats.heading);
            UiKit.DaysLine(new Rect(cell.x + 4f, cell.y + 6f, cell.width - 8f, 14f), v.points, n, min, max, color);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = v.stats.heading != 0 ? color : UiKit.Faint;
            Widgets.Label(new Rect(cell.x, cell.y + 19f, cell.width, 16f), TradeWindowMath.Signed(TradeMarketMath.Percent(r.trend.Value)));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, new TipSignal(() => TrendTip(r), r.t.GetHashCode() ^ 0x2b7e9));
            }
        }

        private static string TrendTip(Row r)
        {
            var lines = TradeMarket.MarketLines(r.market);
            string news = TradeMarket.NewsLine(r.market);
            if (!news.NullOrEmpty()) lines.Add(news);
            return string.Join("\n", lines);
        }

        /// <summary>What the colony needs of it, from the advice: short (worth buying), spare (worth selling) or kept; for people, how they would join.</summary>
        private static void NeedCell(Rect cell, Row r)
        {
            var n = r.note;
            if (r.person && r.pawn.guest != null && !TransferableUIUtility.TransferableIsCaptive(r.t))
            {
                bool colonist = r.pawn.guest.joinStatus == JoinStatus.JoinAsColonist;
                Tiny(cell, (colonist ? "JoinsAsColonist" : "JoinsAsSlave").Translate(), UiKit.Faint);
                TooltipHandler.TipRegion(cell, (colonist ? "JoinsAsColonistDesc" : "JoinsAsSlaveDesc").Translate());
                return;
            }
            if (n == null || n.mark == TradeMark.None)
            {
                Dash(cell);
                return;
            }
            string word;
            Color color;
            switch (n.mark)
            {
                case TradeMark.Buy:
                    word = W("CC_TradeWin_Need_Buy", n.count.Named("COUNT"));
                    color = TradeScreen.BuyColor;
                    break;
                case TradeMark.Sell:
                    word = W("CC_TradeWin_Need_Sell", n.count.Named("COUNT"));
                    color = TradeScreen.SellColor;
                    break;
                default:
                    word = n.setAside >= n.have ? W("CC_TradeWin_Need_KeepAll") : W("CC_TradeWin_Need_Keep", n.setAside.Named("COUNT"));
                    // Red when this trade sells into what is set aside, as the old screen's pin.
                    color = r.t.CountToTransfer < 0 && n.have + r.t.CountToTransfer < n.setAside ? TradeScreen.WarnColor : TradeScreen.KeepColor;
                    break;
            }
            Tiny(cell, word, color);
            if (Mouse.IsOver(cell))
            {
                Widgets.DrawHighlight(cell);
                TooltipHandler.TipRegion(cell, new TipSignal(() => TradeAdvice.Tip(n, r.t), r.t.GetHashCode() ^ 0x7a4de));
            }
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
                y = Heading(x, y, w, W("CC_TradeWin_Best"));
                y = BestList(x, y, w, W("CC_TradeWin_BestSell"), bestSells, true, W("CC_TradeWin_BestNoneSell"));
                y = BestList(x, y + 4f, w, W("CC_TradeWin_BestBuy"), bestBuys, false, W("CC_TradeWin_BestNoneBuy"));
                y = Heading(x, y + 10f, w, W("CC_TradeWin_Deal"));
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
                    Jump(row);
                }
                y += 26f;
            }
            return y;
        }

        /// <summary>The row in the list, shown in the middle and lit for a moment; the filters cleared first if they hide it.</summary>
        private void Jump(Row r)
        {
            int i = shown.IndexOf(r);
            if (i < 0)
            {
                search.Reset();
                category = null;
                inDealOnly = false;
                Refilter();
                i = shown.IndexOf(r);
            }
            if (i < 0) return;
            listScroll.y = TradeWindowMath.ScrollTo(i, RowH, listViewH, shown.Count * RowH);
            flashRow = r;
            flashAt = Time.realtimeSinceStartup;
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
            if (!sums.Any) return Para(x, y, w, W("CC_TradeWin_DealEmpty"), UiKit.Faint, GameFont.Tiny);
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
                lean > 0 ? UiKit.Warm : lean < 0 ? UiKit.Soon : UiKit.Faint, GameFont.Small, W("CC_TradeWin_VsUsualTip"));
            y = Para(x, y, w, W("CC_TradeWin_ByValue", Money(sums.GiveWorth).Named("GIVE"), Money(sums.GetWorth).Named("GET")), UiKit.Faint, GameFont.Tiny, W("CC_TradeWin_ByValueTip"));
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
                    if (UiKit.Button(fill, W("CC_Trade_Fill"), ButtonKind.Normal, any))
                    {
                        if (any)
                        {
                            TradeScreen.Fill(null);
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        }
                        else SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                    TooltipHandler.TipRegion(fill, W(any ? "CC_Trade_FillTip" : "CC_Trade_FillNone"));
                    if (UiKit.Button(clear, W("CC_Trade_Clear"), ButtonKind.Normal, some))
                    {
                        if (some)
                        {
                            TradeScreen.Clear(null);
                            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                        }
                        else SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                    TooltipHandler.TipRegion(clear, W("CC_Trade_ClearTip"));
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
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 18f), W("CC_TradeWin_WalletTitle"));
            Text.Font = GameFont.Medium;
            float numY = inner.y + 18f;
            string now = colonyNow.ToStringCached(), after = colonyAfter.ToStringCached();
            GUI.color = UiKit.Ink;
            float nowW = Text.CalcSize(now).x;
            Widgets.Label(new Rect(inner.x, numY, nowW + 4f, 34f), now);
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(inner.x + nowW + 8f, numY, 30f, 34f), "\u2192");
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
            TooltipHandler.TipRegion(r, W("CC_TradeWin_SilverTip"));
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
            TooltipHandler.TipRegion(r, W("CC_TradeWin_SilverTip"));
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
