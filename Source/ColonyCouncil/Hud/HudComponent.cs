using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The on-screen panels' part of a game: opens them when a game starts or loads (and the game's interface going
    /// back to the main menu takes them with it), keeps them in step with the settings once a frame, hears their keys,
    /// draws the hover card, and keeps the mood trend: once an in-game hour, the colony's average mood and its lowest
    /// colonist's, for the last week (saved, so the graph survives a reload). While the inventory panel is on, it also
    /// keeps a few snapshots of each home map's stock (every six in-game hours, the last day and a quarter, one packed
    /// line each), so the panel can say what changed since about the same hour yesterday. Nothing here runs per tick.
    /// </summary>
    public class HudComponent : GameComponent
    {
        /// <summary>A week of hourly samples (HudMath.Pack: the hour, the average and the lowest mood).</summary>
        public const int MaxSamples = 168;

        private List<int> samples = new List<int>();
        private int lastHour = -1;

        /// <summary>Snapshots of a home map's stock kept for each map (HudInventoryMath: every six hours, so a day and a quarter).</summary>
        public const int MaxStockPerMap = 5;

        /// <summary>Snapshots kept in all (a few home maps at most).</summary>
        public const int MaxStock = 15;

        /// <summary>The stock snapshots, packed (HudInventoryMath.Pack: map, hour, and what there was of each item).</summary>
        private List<string> stock = new List<string>();
        private int lastStockHour = -1;
        private int stockVersion;
        private readonly Dictionary<int, (int version, int hour, Dictionary<string, int> counts)> yesterday =
            new Dictionary<int, (int, int, Dictionary<string, int>)>();

        private static HudComponent cached;
        private static Game cachedGame;
        private static KeyBindingDef panelsKey, cardKey;
        private static bool keysLooked;

        public HudComponent(Game game)
        {
        }

        public static HudComponent Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<HudComponent>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public IReadOnlyList<int> Samples => samples;

        /// <summary>Bumped with each new sample, so the graph knows when to read them again.</summary>
        public int Version { get; private set; }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            try
            {
                HudManager.NewGame();
                HudHoverCard.Reset();
                HudMood.Reset();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels: start", e);
            }
        }

        public override void GameComponentUpdate()
        {
            try
            {
                HudManager.Update();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels", e);
            }
            try
            {
                int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
                if (hour != lastHour)
                {
                    lastHour = hour;
                    Sample(hour);
                    if (HudInventoryMath.SnapshotDue(lastStockHour, hour) && HudInventory.Keeping) TakeStock(hour);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood trend sample", e);
            }
        }

        public override void GameComponentOnGUI()
        {
            try
            {
                Keys();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels: keys", e);
            }
            HudHoverCard.OnGUI();
        }

        // ---------------- The mood trend ----------------

        private void Sample(int hour)
        {
            float sum = 0f, low = 1f;
            int n = 0;
            foreach (var p in WeekModel.Colonists())
            {
                var need = p.needs?.mood;
                if (need == null || p.Suspended) continue;
                float m = need.CurLevelPercentage;
                sum += m;
                if (m < low) low = m;
                n++;
            }
            if (n == 0) return;
            // One sample an hour: a second one for the same hour (a reload) takes its place.
            if (samples.Count > 0)
            {
                HudMath.Unpack(samples[samples.Count - 1], out int lastSampled, out _, out _);
                if (lastSampled == hour) samples.RemoveAt(samples.Count - 1);
                else if (lastSampled > hour) samples.Clear();
            }
            samples.Add(HudMath.Pack(hour, sum / n, low));
            while (samples.Count > MaxSamples) samples.RemoveAt(0);
            Version++;
        }

        // ---------------- The stock, for the inventory's change since yesterday ----------------

        /// <summary>A snapshot of every home map's counted stock (the game's resource counter), the oldest dropped past the caps.</summary>
        private void TakeStock(int hour)
        {
            try
            {
                lastStockHour = hour;
                var keys = new List<KeyValuePair<string, int>>();
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome || map.resourceCounter == null) continue;
                    keys.Clear();
                    foreach (var kv in map.resourceCounter.AllCountedAmounts)
                        if (kv.Key != null && kv.Value > 0)
                            keys.Add(new KeyValuePair<string, int>(kv.Key.defName, kv.Value));
                    stock.Add(HudInventoryMath.Pack(map.uniqueID, hour, keys));
                }
                Prune();
                stockVersion++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("inventory: the stock snapshot", e);
            }
        }

        /// <summary>At most <see cref="MaxStockPerMap"/> a map, none for maps gone, <see cref="MaxStock"/> in all.</summary>
        private void Prune()
        {
            var perMap = new Dictionary<int, int>();
            var keep = new List<string>();
            var live = new HashSet<int>();
            foreach (var m in Find.Maps)
                if (m != null)
                    live.Add(m.uniqueID);
            // Newest first, so the caps drop the oldest.
            for (int i = stock.Count - 1; i >= 0; i--)
            {
                if (!HudInventoryMath.Unpack(stock[i], out int map, out _, null) || !live.Contains(map)) continue;
                perMap.TryGetValue(map, out int n);
                if (n >= MaxStockPerMap || keep.Count >= MaxStock) continue;
                perMap[map] = n + 1;
                keep.Add(stock[i]);
            }
            keep.Reverse();
            stock = keep;
        }

        /// <summary>
        /// What there was of each item on <paramref name="map"/> at about the same hour yesterday (the newest snapshot a day
        /// to a day and a quarter old), or null when there's none. Unpacked once per snapshot.
        /// </summary>
        public Dictionary<string, int> Yesterday(Map map, int nowHour)
        {
            if (map == null) return null;
            if (yesterday.TryGetValue(map.uniqueID, out var c) && c.version == stockVersion && c.hour == nowHour) return c.counts;
            Dictionary<string, int> found = null;
            var hours = new List<int>();
            var lines = new List<string>();
            foreach (var line in stock)
                if (HudInventoryMath.Unpack(line, out int m, out int h, null) && m == map.uniqueID)
                {
                    hours.Add(h);
                    lines.Add(line);
                }
            int at = HudInventoryMath.Yesterday(hours, nowHour);
            if (at >= 0)
            {
                found = new Dictionary<string, int>();
                HudInventoryMath.Unpack(lines[at], out _, out _, found);
            }
            yesterday[map.uniqueID] = (stockVersion, nowHour, found);
            return found;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref stock, "stockSnapshots", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stock ??= new List<string>();
                stock.RemoveAll(l => !HudInventoryMath.Unpack(l, out _, out _, null));
                while (stock.Count > MaxStock) stock.RemoveAt(0);
                foreach (var l in stock)
                    if (HudInventoryMath.Unpack(l, out _, out int h, null))
                        lastStockHour = Math.Max(lastStockHour, h);
                stockVersion++;
            }
            Scribe_Collections.Look(ref samples, "moodSamples", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                samples ??= new List<int>();
                samples.RemoveAll(s => s < 0);
                while (samples.Count > MaxSamples) samples.RemoveAt(0);
                if (samples.Count > 0)
                {
                    HudMath.Unpack(samples[samples.Count - 1], out lastHour, out _, out _);
                }
                Version++;
            }
        }

        // ---------------- Keys ----------------

        private static void LookKeys()
        {
            if (keysLooked) return;
            keysLooked = true;
            panelsKey = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_Panels");
            cardKey = DefDatabase<KeyBindingDef>.GetNamedSilentFail("Council_Key_HoverCard");
        }

        private static void Keys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None || GUIUtility.keyboardControl != 0) return;
            LookKeys();
            if (panelsKey != null && panelsKey.KeyDownEvent)
            {
                HudManager.Toggle();
                e.Use();
            }
            else if (cardKey != null && cardKey.KeyDownEvent)
            {
                var s = ColonyCouncilMod.Settings;
                s.hoverCard = !s.hoverCard;
                HudLayout.Save();
                e.Use();
            }
        }

        /// <summary>The key that switches the panels ("F7"), or null when none is set.</summary>
        public static string KeyLabel()
        {
            LookKeys();
            try
            {
                return panelsKey == null || panelsKey.MainKey == KeyCode.None ? null : panelsKey.MainKeyLabel;
            }
            catch
            {
                return null;
            }
        }
    }
}
