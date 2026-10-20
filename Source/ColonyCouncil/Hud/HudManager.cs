using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Opens and closes the on-screen panels (once a frame, from <see cref="HudComponent"/>'s update, never per tick):
    /// each panel shows while the panels are switched on, it is on, it hasn't failed this session, and nothing hides
    /// them (the world map, a big window such as the Work tab). Also the switch itself (the button by the game's
    /// play settings, the key, the settings), the button's menu, and the layout mode.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HudManager
    {
        private static readonly Dictionary<string, HudPanel> windows = new Dictionary<string, HudPanel>();
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static HudEditBar editBar;
        private static Game game;
        private static readonly List<HudBox> tmpBoxes = new List<HudBox>();

        /// <summary>Arranging the panels: every bar shows, the lock is off, a small bar of its own says how.</summary>
        public static bool editing;

        public static readonly Texture2D EditLineTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.83f, 0.47f, 0.55f));

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool Failed(string id) => failed.Contains(id);

        /// <summary>A panel threw: one line in the log, and it stays hidden for the rest of the session.</summary>
        public static void Fail(string id, Exception e)
        {
            if (!failed.Add(id)) return;
            CouncilLog.ErrorOnce("on-screen panel " + id + " (hidden for this session)", e);
        }

        /// <summary>A panel is being moved.</summary>
        public static bool AnyDragging
        {
            get
            {
                foreach (var w in windows.Values)
                    if (w.Dragging && w.IsOpen)
                        return true;
                return false;
            }
        }

        /// <summary>The other open panels' places, for snapping.</summary>
        public static List<HudBox> OtherBoxes(HudPanel except)
        {
            tmpBoxes.Clear();
            foreach (var w in windows.Values)
                if (w != except && w.IsOpen)
                    tmpBoxes.Add(new HudBox(w.Body.x, w.Body.y, w.Body.width, w.Body.height));
            return tmpBoxes;
        }

        public static HudPanel Window(string id) => windows.TryGetValue(id, out var w) ? w : null;

        private static HudPanel Make(string id)
        {
            switch (id)
            {
                case HudLayout.Moods: return new HudMoodBoard();
                case HudLayout.Trend: return new HudMoodTrend();
                case HudLayout.Vitals: return new HudVitals();
                case HudLayout.Council: return new HudCouncil();
                default: return null;
            }
        }

        /// <summary>A new game or a load: the last game's windows went with its interface.</summary>
        public static void NewGame()
        {
            windows.Clear();
            editBar = null;
            editing = false;
            game = Current.Game;
        }

        // ---------------- Once a frame ----------------

        /// <summary>Whether something hides every panel right now (the world map, a big window).</summary>
        public static bool Hidden()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.WindowStack == null) return true;
            if (S.hudHideOnWorld && WorldRendererUtility.WorldSelected) return true;
            if (S.hudHideWithTabs && BigTabOpen()) return true;
            return false;
        }

        /// <summary>A main tab that covers much of the screen (Work, Research, the council's window), not the small ones (Architect, the inspect pane).</summary>
        private static bool BigTabOpen()
        {
            var open = Find.MainTabsRoot?.OpenTab;
            if (open == null || open == MainButtonDefOf.Inspect) return false;
            var w = open.TabWindow;
            if (w == null) return false;
            var r = w.windowRect;
            return r.width * r.height >= UI.screenWidth * UI.screenHeight * 0.15f;
        }

        public static void Update()
        {
            if (Current.Game != game) NewGame();
            bool hideAll = !S.hudShown || Hidden();
            foreach (var id in HudLayout.Ids)
            {
                bool want = !hideAll && !failed.Contains(id) && HudLayout.State(id).on;
                windows.TryGetValue(id, out var w);
                if (want)
                {
                    if (w == null)
                    {
                        w = Make(id);
                        if (w == null) continue;
                        windows[id] = w;
                    }
                    if (!w.IsOpen) Find.WindowStack.Add(w);
                }
                else if (w != null && w.IsOpen) w.Close(false);
            }
            bool bar = editing && !Hidden();
            if (bar && (editBar == null || !editBar.IsOpen))
            {
                editBar = editBar ?? new HudEditBar();
                Find.WindowStack.Add(editBar);
            }
            else if (!bar && editBar != null && editBar.IsOpen) editBar.Close(false);
        }

        // ---------------- The switch ----------------

        public static void Toggle()
        {
            S.hudShown = !S.hudShown;
            if (!S.hudShown) editing = false;
            HudLayout.Save();
            (S.hudShown ? SoundDefOf.Tick_High : SoundDefOf.Tick_Low).PlayOneShotOnCamera();
        }

        public static void SetPanel(string id, bool on)
        {
            HudLayout.State(id).on = on;
            if (on) S.hudShown = true;
            HudLayout.Save();
        }

        /// <summary>The menu of the button by the play settings (right-click): each panel, the hover card, the layout.</summary>
        public static List<FloatMenuOption> Menu()
        {
            var options = new List<FloatMenuOption>();
            foreach (var id in HudLayout.Ids)
            {
                string pid = id;
                bool on = HudLayout.State(id).on;
                options.Add(new FloatMenuOption(HudLayout.Label(id), () => SetPanel(pid, !on), on ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, Color.white));
            }
            bool card = S.hoverCard;
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_Name_hover"), () =>
            {
                S.hoverCard = !card;
                HudLayout.Save();
            }, card ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, Color.white));
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_EditLayout"), () =>
            {
                S.hudShown = true;
                editing = true;
            }));
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_ResetPositions"), HudLayout.ResetPositions));
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_AllOptions"), OpenSettings));
            return options;
        }

        /// <summary>Rimfolk's settings, where "On-screen panels" has every option.</summary>
        public static void OpenSettings()
        {
            try
            {
                var mod = LoadedModManager.GetMod<ColonyCouncilMod>();
                if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("opening the settings", e);
            }
        }

        /// <summary>The button's tooltip: what it does, the key, the right-click.</summary>
        public static string ButtonTip()
        {
            string key = HudComponent.KeyLabel();
            string tip = HudKit.T("CC_Pnl_ButtonTip");
            if (!key.NullOrEmpty()) tip += "\n" + "CC_Pnl_ButtonKey".Translate(key).Resolve();
            return tip;
        }
    }

    /// <summary>The small bar while the panels are being arranged: each panel on or off, their places put back, done.</summary>
    public class HudEditBar : Window
    {
        public HudEditBar()
        {
            layer = WindowLayer.GameUI;
            doCloseX = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            focusWhenOpened = false;
            draggable = true;
            drawShadow = false;
            soundAppear = null;
            soundClose = null;
            drawInScreenshotMode = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(560f, UI.screenWidth - 20f), 88f);

        protected override float Margin => 8f;

        protected override void SetInitialSizeAndPosition()
        {
            var size = InitialSize;
            windowRect = new Rect((UI.screenWidth - size.x) / 2f, Mathf.Round(UI.screenHeight * 0.16f), size.x, size.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                HudKit.Label(new Rect(inRect.x, inRect.y, inRect.width, 22f), HudKit.T("CC_Pnl_EditHint"), UiKit.HeaderColor, GameFont.Small);
                float x = inRect.x, y = inRect.y + 26f;
                foreach (var id in HudLayout.Ids)
                {
                    var st = HudLayout.State(id);
                    string label = HudLayout.Label(id);
                    Text.Font = GameFont.Small;
                    float w = Text.CalcSize(label).x + 32f;
                    bool on = st.on;
                    Widgets.CheckboxLabeled(new Rect(x, y, w, 24f), label, ref on);
                    if (on != st.on) HudManager.SetPanel(id, on);
                    x += w + 12f;
                }
                float by = inRect.yMax - 26f;
                if (Widgets.ButtonText(new Rect(inRect.x, by, 170f, 26f), HudKit.T("CC_Pnl_ResetPositions"))) HudLayout.ResetPositions();
                if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, by, 120f, 26f), HudKit.T("CC_Pnl_Done")))
                {
                    HudManager.editing = false;
                    HudLayout.Save();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels: layout bar", e);
                HudManager.editing = false;
            }
            finally
            {
                Text.Font = GameFont.Small;
            }
        }
    }
}
