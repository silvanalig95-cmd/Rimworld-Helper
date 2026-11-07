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
    /// (Trade/TradeAdvice.cs): its world market's price of each good against the good's usual worth, the contracts its
    /// trading terminal shows (what other people ask the colony for, and what the colony asked for itself), and the
    /// silver the colony keeps in its banks. All found by name in its TradingManager; nothing of it is changed, and
    /// without it (or with a version that differs) every answer is "nothing known". VTE draws nothing on the game's
    /// trade screen itself: it moves a good's market value (so every trade price already carries its market), and its
    /// own windows are on the trading terminal.
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

        private static bool looked, ready;
        private static FieldInfo instance, priceModifiers, npcContracts, playerContracts, banks, vanillaOnly;
        private static FieldInfo cItem, cStuff, cAmount, cReward;
        private static PropertyInfo bankDeposit, bankName;

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
            }
            catch (Exception e)
            {
                ready = false;
                CouncilLog.ErrorOnce("Vanilla Trading Expanded", e);
            }
        }

        private static object Manager => ready ? instance.GetValue(null) : null;

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
                float usual;
                if (vanillaOnly != null)
                {
                    bool was = (bool)vanillaOnly.GetValue(null);
                    vanillaOnly.SetValue(null, true);
                    try
                    {
                        usual = def.GetStatValueAbstract(StatDefOf.MarketValue);
                    }
                    finally
                    {
                        vanillaOnly.SetValue(null, was);
                    }
                }
                else usual = def.BaseMarketValue;
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
