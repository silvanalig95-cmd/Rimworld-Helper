using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Trading Expanded (vanillaexpanded.vanillatradingexpanded), read softly for the trade screen
    /// (Trade/TradeAdvice.cs, Trade/TradeMarket.cs): its world market's price of each good against the good's usual worth,
    /// the price history it keeps of each good (one price a day), what the colony traded of each good since it last
    /// reckoned the colony's trades into the prices, its news that moved prices, its setting for how much the colony's
    /// trades move them, the contracts its trading terminal shows (what other people ask the colony for, and what the
    /// colony asked for itself), and the silver the colony keeps in its banks. All found by name in its TradingManager;
    /// nothing of it is changed, and without it (or with a version that differs) every answer is "nothing known". VTE
    /// draws nothing on the game's trade screen itself: it moves a good's market value (so every trade price already
    /// carries its market), and its own windows are on the trading terminal.
    /// </summary>
    public static class VanillaTrading
    {
        public const string PackageId = "vanillaexpanded.vanillatradingexpanded";

        /// <summary>A contract on VTE's terminal: so many of a thing (of a stuff, or any) for a reward in silver.</summary>
        public struct Contract
        {
            public ThingDef item, stuff;
            public int amount, reward;
            /// <summary>Another people asks it of the colony (else the colony asked for it).</summary>
            public bool theirs;
        }

        private static bool looked, ready, historyReady;
        private static FieldInfo instance, priceModifiers, npcContracts, playerContracts, banks, vanillaOnly;
        private static FieldInfo cItem, cStuff, cAmount, cReward;
        private static PropertyInfo bankDeposit, bankName;
        // The price history, the colony's trades not yet reckoned, the news, and VTE's own settings.
        private static FieldInfo recorders, records, pending, allNews, nText, nImpact, nStart, modSettings, impactSetting;
        private static MethodInfo nAffected;

        /// <summary>VTE is loaded and the trade screen's part for it is on.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.tradeVte ?? true) && Ready;

        public static bool Ready
        {
            get
            {
                Look();
                return ready;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                if (!People.ModActive(PackageId)) return;
                var manager = AccessTools.TypeByName("VanillaTradingExpanded.TradingManager");
                var contract = AccessTools.TypeByName("VanillaTradingExpanded.Contract");
                var bank = AccessTools.TypeByName("VanillaTradingExpanded.Bank");
                var patch = AccessTools.TypeByName("VanillaTradingExpanded.StatWorker_GetBaseValueFor_Patch");
                if (manager == null) return;
                instance = AccessTools.Field(manager, "Instance");
                priceModifiers = AccessTools.Field(manager, "priceModifiers");
                npcContracts = AccessTools.Field(manager, "npcSubmittedContracts");
                playerContracts = AccessTools.Field(manager, "playerSubmittedContracts");
                banks = AccessTools.Field(manager, "banksByFaction");
                vanillaOnly = patch != null ? AccessTools.Field(patch, "outputOnlyVanilla") : null;
                if (vanillaOnly != null && (!vanillaOnly.IsStatic || vanillaOnly.FieldType != typeof(bool))) vanillaOnly = null;
                if (contract != null)
                {
                    cItem = AccessTools.Field(contract, "item");
                    cStuff = AccessTools.Field(contract, "stuff");
                    cAmount = AccessTools.Field(contract, "amount");
                    cReward = AccessTools.Field(contract, "reward");
                }
                if (bank != null)
                {
                    bankDeposit = AccessTools.Property(bank, "DepositAmount");
                    bankName = AccessTools.Property(bank, "Name");
                }
                ready = instance != null && instance.IsStatic;
                if (!ready) Log.Warning("[Rimfolk] Vanilla Trading Expanded isn't the version Rimfolk knows, so the trade screen leaves its market out.");
                LookHistory(manager);
            }
            catch (Exception e)
            {
                ready = false;
                CouncilLog.ErrorOnce("Vanilla Trading Expanded", e);
            }
        }

        /// <summary>Its price history, the colony's trades it hasn't reckoned yet, its news and its setting for the colony's trades: each by name, each may be missing.</summary>
        private static void LookHistory(Type manager)
        {
            var recorder = AccessTools.TypeByName("VanillaTradingExpanded.PriceHistoryAutoRecorder");
            var news = AccessTools.TypeByName("VanillaTradingExpanded.News");
            var mod = AccessTools.TypeByName("VanillaTradingExpanded.VanillaTradingExpandedMod");
            var settings = AccessTools.TypeByName("VanillaTradingExpanded.Settings");
            recorders = AccessTools.Field(manager, "priceHistoryRecorders");
            records = recorder != null ? AccessTools.Field(recorder, "records") : null;
            if (records != null && records.FieldType != typeof(List<float>)) records = null;
            pending = AccessTools.Field(manager, "thingsAffectedBySoldPurchasedMarketValue");
            allNews = AccessTools.Field(manager, "allNews");
            if (news != null)
            {
                nText = AccessTools.Field(news, "text");
                nImpact = AccessTools.Field(news, "priceImpact");
                nStart = AccessTools.Field(news, "priceImpactStartTick");
                nAffected = AccessTools.Method(news, "AffectedThingDefs");
            }
            modSettings = mod != null ? AccessTools.Field(mod, "settings") : null;
            impactSetting = settings != null ? AccessTools.Field(settings, "playerTransactionImpactMultiplier") : null;
            historyReady = ready && recorders != null && records != null;
            if (ready && !historyReady)
                Log.Warning("[Rimfolk] Vanilla Trading Expanded keeps its price history where Rimfolk doesn't know it, so the trade screen leaves its history out.");
        }

        private static object Manager => ready ? instance.GetValue(null) : null;

        /// <summary>VTE's price history can be read, and the trade screen's part for it is on.</summary>
        public static bool HistoryOn => On && (ColonyCouncilMod.Settings?.tradeVteHistory ?? true) && HistoryReady;

        public static bool HistoryReady
        {
            get
            {
                Look();
                return historyReady;
            }
        }

        /// <summary>
        /// A good's worth as the game itself has it, read as VTE itself reads it: its own switch for the game's value is
        /// set for the read and set back at once.
        /// </summary>
        public static float Usual(ThingDef def)
        {
            if (def == null) return 0f;
            if (vanillaOnly == null) return def.BaseMarketValue;
            bool was = (bool)vanillaOnly.GetValue(null);
            vanillaOnly.SetValue(null, true);
            try
            {
                return def.GetStatValueAbstract(StatDefOf.MarketValue);
            }
            finally
            {
                vanillaOnly.SetValue(null, was);
            }
        }

        /// <summary>
        /// A good's market as VTE keeps it: its history (one price a day, oldest first; VTE's own list, read only), its
        /// market price now (what VTE records and counts the colony's trades at) and its usual worth. False when VTE keeps
        /// no history of it.
        /// </summary>
        public static bool Market(ThingDef def, out List<float> history, out float now, out float usual)
        {
            history = null;
            now = usual = 0f;
            if (def == null || !HistoryOn) return false;
            try
            {
                if (!(recorders.GetValue(Manager) is IDictionary all) || !all.Contains(def) || all[def] == null) return false;
                history = records.GetValue(all[def]) as List<float>;
                if (history == null) return false;
                now = def.GetStatValueAbstract(StatDefOf.MarketValue);
                usual = Usual(def);
                return now > 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded price history", e);
                history = null;
                return false;
            }
        }

        /// <summary>The net market worth of a good the colony traded since VTE last reckoned its trades (sold positive, bought negative); 0 when none or not known.</summary>
        public static float Pending(ThingDef def)
        {
            if (def == null || !HistoryOn || pending == null) return 0f;
            try
            {
                return pending.GetValue(Manager) is Dictionary<ThingDef, float> d && d.TryGetValue(def, out float v) ? v : 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded trades not yet reckoned", e);
                return 0f;
            }
        }

        /// <summary>VTE's setting for how much the colony's trades move prices (1 as it comes); 1 when it can't be read.</summary>
        public static float ImpactMultiplier
        {
            get
            {
                if (!HistoryOn || modSettings == null || impactSetting == null) return 1f;
                try
                {
                    var s = modSettings.GetValue(null);
                    return s != null && impactSetting.GetValue(s) is float f ? f : 1f;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("Vanilla Trading Expanded settings", e);
                    return 1f;
                }
            }
        }

        /// <summary>A piece of VTE's news that moved a good's price: its words, when it moved them (game ticks), and which way (+1 up).</summary>
        public struct NewsItem
        {
            public string text;
            public int tick, way;
        }

        /// <summary>
        /// The latest of VTE's news that moved each good's price since <paramref name="since"/> (game ticks): only the news
        /// already in the prices, and only news about goods (its news of companies names none).
        /// </summary>
        public static Dictionary<ThingDef, NewsItem> NewsByGood(int since)
        {
            var map = new Dictionary<ThingDef, NewsItem>();
            if (!HistoryOn || allNews == null || nText == null || nStart == null || nAffected == null) return map;
            try
            {
                if (!(allNews.GetValue(Manager) is IList list)) return map;
                foreach (var news in list)
                {
                    if (news == null || !(nStart.GetValue(news) is int tick) || tick < since) continue;
                    string text = nText.GetValue(news) as string;
                    if (text.NullOrEmpty()) continue;
                    float impact = nImpact?.GetValue(news) is float f ? f : 0f;
                    if (!(nAffected.Invoke(news, null) is IEnumerable defs)) continue;
                    foreach (var o in defs)
                        if (o is ThingDef d && (!map.TryGetValue(d, out var had) || had.tick < tick))
                            map[d] = new NewsItem { text = text, tick = tick, way = impact > 0f ? 1 : impact < 0f ? -1 : 0 };
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded news", e);
            }
            return map;
        }

        /// <summary>
        /// The world market's price of a good against its usual worth: 1.3 when VTE's market pays a third more just now,
        /// 1 when it isn't known. The usual worth is read as VTE itself reads it (its own switch for the game's value,
        /// set back at once).
        /// </summary>
        public static float WorldFactor(ThingDef def)
        {
            if (def == null || !On || priceModifiers == null) return 1f;
            try
            {
                if (!(priceModifiers.GetValue(Manager) is Dictionary<ThingDef, float> prices) || !prices.TryGetValue(def, out float now) || now <= 0f) return 1f;
                float usual = Usual(def);
                return usual > 0.0001f ? now / usual : 1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded prices", e);
                return 1f;
            }
        }

        /// <summary>The open contracts on VTE's terminal: other peoples' asks of the colony, and the colony's own.</summary>
        public static List<Contract> Contracts()
        {
            var list = new List<Contract>();
            if (!On || cItem == null || cAmount == null || cReward == null) return list;
            try
            {
                var m = Manager;
                void Read(FieldInfo f, bool theirs)
                {
                    if (f == null || !(f.GetValue(m) is IList items)) return;
                    foreach (var c in items)
                    {
                        if (c == null || !(cItem.GetValue(c) is ThingDef item)) continue;
                        list.Add(new Contract
                        {
                            item = item, stuff = cStuff?.GetValue(c) as ThingDef, amount = cAmount.GetValue(c) is int a ? a : 0, reward = cReward.GetValue(c) is int r ? r : 0,
                            theirs = theirs
                        });
                    }
                }
                Read(npcContracts, true);
                Read(playerContracts, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded contracts", e);
            }
            return list;
        }

        /// <summary>The silver the colony keeps in VTE's banks, each bank by its name; empty when none is known.</summary>
        public static List<(string bank, Faction faction, int silver)> Deposits()
        {
            var list = new List<(string, Faction, int)>();
            if (!On || banks == null || bankDeposit == null) return list;
            try
            {
                if (!(banks.GetValue(Manager) is IDictionary all)) return list;
                foreach (DictionaryEntry e in all)
                {
                    if (e.Value == null || !(bankDeposit.GetValue(e.Value) is int silver) || silver <= 0) continue;
                    var f = e.Key as Faction;
                    string name = bankName?.GetValue(e.Value) as string;
                    list.Add((name.NullOrEmpty() ? f?.Name ?? "?" : name, f, silver));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Vanilla Trading Expanded banks", e);
            }
            return list;
        }
    }
}
