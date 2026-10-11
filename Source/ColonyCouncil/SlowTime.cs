using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Two speeds below normal (half and quarter) for unhurried planning. The game runs at its normal speed setting
    /// while the tick clock is held back, so everything else (pausing, letters, forced slowdowns) behaves as usual.
    /// Chosen from small on-screen buttons (drag them by the grip) or by the game's own "slower"/"faster" keys,
    /// which now step below normal speed.
    /// </summary>
    public class SlowTimeComponent : GameComponent
    {
        public static readonly float[] Factors = { 1f, 0.5f, 0.25f };

        /// <summary>0 = normal, 1 = half, 2 = quarter.</summary>
        public static int level;

        private static FieldInfo realTimeField;
        private static bool fieldLooked;
        private static float allowance;
        private static bool holdNormal;
        private static bool dragging;
        private static Vector2 dragOffset;

        public SlowTimeComponent(Game game)
        {
            level = 0;
            allowance = 0f;
        }

        private static FieldInfo RealTime
        {
            get
            {
                if (!fieldLooked)
                {
                    fieldLooked = true;
                    realTimeField = typeof(TickManager).GetField("realTimeToTickThrough", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (realTimeField == null || realTimeField.FieldType != typeof(float))
                    {
                        realTimeField = null;
                        CouncilLog.WarnOnce("slow speeds are unavailable: this version of the game keeps time differently.");
                    }
                }
                return realTimeField;
            }
        }

        public static bool Available => RealTime != null;

        public static void Set(int newLevel)
        {
            var tm = Find.TickManager;
            if (tm == null || !Available) return;
            level = Mathf.Clamp(newLevel, 0, Factors.Length - 1);
            allowance = 0f;
            if (level > 0 && tm.CurTimeSpeed != TimeSpeed.Normal)
            {
                tm.CurTimeSpeed = TimeSpeed.Normal;
                SoundDefOf.Clock_Normal.PlayOneShotOnCamera();
            }
        }

        public override void GameComponentUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("slow speeds", e);
                level = 0;
            }
        }

        private static void Tick()
        {
            var tm = Find.TickManager;
            if (tm == null || !Available) return;
            var speed = tm.CurTimeSpeed;

            // The game's own speed keys: "slower" at normal speed steps down to half, then quarter; "faster" and
            // "normal" step back up. (The game handles the same key press later this frame, so a "faster" that
            // would jump to fast is undone next frame.)
            if (holdNormal)
            {
                // Whatever the game made of the same key press (fast, or a pause), we meant normal-but-slower.
                holdNormal = false;
                if (speed != TimeSpeed.Normal)
                {
                    tm.CurTimeSpeed = TimeSpeed.Normal;
                    speed = TimeSpeed.Normal;
                }
            }
            // Not while typing (a "-" in a name field isn't a speed change).
            bool keys = GUIUtility.keyboardControl == 0;
            if (keys && KeyBindingDefOf.TimeSpeed_Normal.JustPressed) level = 0;
            else if (keys && KeyBindingDefOf.TimeSpeed_Slower.JustPressed && speed == TimeSpeed.Normal && level < Factors.Length - 1)
            {
                Set(level + 1);
                holdNormal = true;
                return;
            }
            else if (keys && KeyBindingDefOf.TimeSpeed_Faster.JustPressed && level > 0 && speed == TimeSpeed.Normal)
            {
                Set(level - 1);
                holdNormal = true;
                return;
            }

            // Picked a faster speed: slow time ends.
            if (speed == TimeSpeed.Fast || speed == TimeSpeed.Superfast || speed == TimeSpeed.Ultrafast) level = 0;
            if (level == 0 || tm.Paused || speed != TimeSpeed.Normal)
            {
                allowance = 0f;
                return;
            }

            // Let ticks through at the chosen fraction: credit what's allowed, debit what happened, and when ahead,
            // hold the clock back by pushing its time budget below zero.
            float perTick = 1f / (60f * Mathf.Max(0.01f, tm.TickRateMultiplier));
            allowance += Factors[level] * Mathf.Min(Time.deltaTime, 0.25f) / perTick;
            allowance -= tm.TicksThisFrame;
            allowance = Mathf.Clamp(allowance, -2f, 2f);
            if (allowance < 0f)
            {
                float budget = (float)RealTime.GetValue(tm);
                float wanted = allowance * perTick;
                if (budget > wanted) RealTime.SetValue(tm, wanted);
            }
        }

        // ---------------- The buttons ----------------

        private const float ButW = 30f;
        private const float ButH = 24f;
        private const float Grip = 10f;

        private static Rect PanelRect()
        {
            var s = ColonyCouncilMod.Settings;
            float w = Grip + ButW * 2f + 4f;
            float x = s.slowButtonsX >= 0f ? s.slowButtonsX * UI.screenWidth : UI.screenWidth - 200f - w - 8f;
            float y = s.slowButtonsY >= 0f ? s.slowButtonsY * UI.screenHeight : UI.screenHeight - 35f - 70f;
            x = Mathf.Clamp(x, 0f, UI.screenWidth - w);
            y = Mathf.Clamp(y, 0f, UI.screenHeight - ButH);
            return new Rect(x, y, w, ButH);
        }

        public override void GameComponentOnGUI()
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.slowButtons || !Available || Find.TickManager == null || Find.UIRoot?.screenshotMode?.FiltersCurrentEvent == true) return;
            var rect = PanelRect();
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.55f));

            // Grip: drag to move, remembered between games.
            var grip = new Rect(rect.x, rect.y, Grip, rect.height);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            for (int i = 0; i < 3; i++) Widgets.DrawLineHorizontal(grip.x + 2f, grip.y + 7f + i * 5f, Grip - 4f);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(grip, "CC_Slow_Grip".Translate());
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && Mouse.IsOver(grip))
            {
                dragging = true;
                dragOffset = e.mousePosition - rect.position;
                e.Use();
            }
            else if (dragging && e.type == EventType.MouseDrag)
            {
                var p = e.mousePosition - dragOffset;
                s.slowButtonsX = Mathf.Clamp01(p.x / UI.screenWidth);
                s.slowButtonsY = Mathf.Clamp01(p.y / UI.screenHeight);
                e.Use();
            }
            else if (dragging && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                dragging = false;
                s.Write();
            }

            bool active = !Find.TickManager.Paused && Find.TickManager.CurTimeSpeed == TimeSpeed.Normal;
            for (int i = 1; i < Factors.Length; i++)
            {
                var b = new Rect(rect.x + Grip + (i - 1) * (ButW + 4f), rect.y, ButW, ButH);
                bool on = level == i;
                if (on) Widgets.DrawBoxSolid(b, active ? new Color(0.35f, 0.55f, 0.35f, 0.8f) : new Color(0.35f, 0.4f, 0.35f, 0.6f));
                Widgets.DrawHighlightIfMouseover(b);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = on ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(b, ("CC_Slow_Level_" + i).Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(b, ("CC_Slow_Level_" + i + "_Tip").Translate(KeyBindingDefOf.TimeSpeed_Slower.MainKeyLabel, KeyBindingDefOf.TimeSpeed_Faster.MainKeyLabel));
                if (Widgets.ButtonInvisible(b))
                {
                    if (on) Set(0);
                    else
                    {
                        Set(i);
                        if (Find.TickManager.Paused) Find.TickManager.TogglePaused();
                    }
                }
            }
        }
    }
}
