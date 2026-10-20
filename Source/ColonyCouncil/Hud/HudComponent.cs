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
    /// colonist's, for the last week (saved, so the graph survives a reload). Nothing here runs per tick.
    /// </summary>
    public class HudComponent : GameComponent
    {
        /// <summary>A week of hourly samples (HudMath.Pack: the hour, the average and the lowest mood).</summary>
        public const int MaxSamples = 168;

        private List<int> samples = new List<int>();
        private int lastHour = -1;

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

        public override void ExposeData()
        {
            base.ExposeData();
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
