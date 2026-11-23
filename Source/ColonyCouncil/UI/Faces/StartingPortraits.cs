using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The painted portrait on the starting pawns' page (the player, 23 November 2026: "when you start a new colony now in
    /// game, you select your pawns at first ... Could we include the image of the pawn already there? That would be lovely.
    /// It would provide another level of immersion right from the beginning", and "please make it so that images are shown
    /// on the new game start screen"):
    /// <list type="bullet">
    /// <item>The pawn shown on the page (the game's <c>StartingPawnUtility.DrawPortraitArea</c> tells which) gets the large
    /// portrait as the game would show it, with their backdrop where one is found, in a panel beside the page (in the
    /// free space to its right, else to its left; none where the screen has no room for it). It covers nothing of the
    /// page.</item>
    /// <item>A pick there is a trial (<see cref="PortraitLibrary"/>'s DecideTrial): held while that pawn is on the page,
    /// never kept or reserved, never a face another pawn on the page holds; let go when the pawn is randomized (a new pawn)
    /// or removed. A right click: "Choose another picture", the next that may stand for them, held as chosen by hand.</item>
    /// <item>When the game starts with them (<see cref="PortraitKeeper"/>.StartedNewGame), each colonist keeps the picture
    /// the page showed, as if picked (or chosen by hand) in the game; a loaded game lets every trial go.</item>
    /// </list>
    /// With painted portraits off (the setting), nothing shows.
    /// </summary>
    public static class StartingPortraits
    {
        private sealed class TrialPick
        {
            public string picture, character;
            public bool byHand, pinned;
            public float distance;
        }

        private static readonly Dictionary<int, TrialPick> picks = new Dictionary<int, TrialPick>();

        /// <summary>The pawn shown on the page (its index in the game's starting pawns) and the frame it was drawn in.</summary>
        private static int shownIndex = -1, shownFrame = -100;

        /// <summary>The panel as drawn last (for the right click, which the page would take otherwise).</summary>
        private static Rect panel;
        private static int panelFrame = -100;
        private static bool failed;

        public const float MostWide = 320f, LeastWide = 150f, Gap = 12f, BandHigh = 28f;

        // ---------------- The trial picks ----------------

        /// <summary>The picture held for this person on the page, if any.</summary>
        public static bool Trial(int id, out string picture)
        {
            picture = null;
            if (!picks.TryGetValue(id, out var t)) return false;
            picture = t.picture;
            return true;
        }

        /// <summary>A picture held for this person on the page; one chosen by hand is not replaced by a pick of the usual rules.</summary>
        public static void Note(int id, string picture, string character, bool byHand, float distance, bool pinned)
        {
            if (picks.TryGetValue(id, out var had) && had.byHand && !byHand && had.picture == picture) return;
            picks[id] = new TrialPick { picture = picture, character = character, byHand = byHand, distance = distance, pinned = pinned };
        }

        public static void Forget(int id) => picks.Remove(id);

        /// <summary>Whether another person on the page holds this face.</summary>
        public static bool TakenByOther(string character, int id)
        {
            if (string.IsNullOrEmpty(character)) return false;
            var onPage = OnPage();
            foreach (var kv in picks)
                if (kv.Key != id && kv.Value.character == character && (onPage == null || onPage.Contains(kv.Key)))
                    return true;
            return false;
        }

        /// <summary>The ids of the pawns on the starting pawns' page now, or null where there is none.</summary>
        private static HashSet<int> OnPage()
        {
            var pawns = Current.Game?.InitData?.startingAndOptionalPawns;
            if (pawns == null) return null;
            var ids = new HashSet<int>();
            foreach (var p in pawns)
                if (p != null)
                    ids.Add(p.thingIDNumber);
            return ids;
        }

        /// <summary>The trial picks of pawns no longer on the page (randomized, removed) let go.</summary>
        private static void Prune()
        {
            var onPage = OnPage();
            if (onPage == null || picks.Count == 0) return;
            foreach (int id in picks.Keys.Where(id => !onPage.Contains(id)).ToList()) picks.Remove(id);
        }

        /// <summary>A new game started: each colonist keeps the picture the page showed them; every other trial let go.</summary>
        public static void KeepAll()
        {
            if (picks.Count == 0) return;
            var held = new Dictionary<int, TrialPick>(picks);
            picks.Clear();
            try
            {
                var lib = PortraitLibrary.Source;
                if (lib == null) return;
                int kept = 0;
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                {
                    if (p == null || !held.TryGetValue(p.thingIDNumber, out var t)) continue;
                    lib.KeepFromStart(p, t.picture, t.byHand, t.distance, t.pinned);
                    kept++;
                }
                if (kept > 0) Log.Message("[Rimfolk] Portraits: " + kept + " colonists keep the pictures the starting pawns' page showed them.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: keeping the starting pawns' pictures", e);
            }
        }

        /// <summary>A game loaded: the trials of a page left unstarted let go.</summary>
        public static void Clear() => picks.Clear();

        // ---------------- The panel ----------------

        /// <summary>From the game's drawing of the page's portrait area: which pawn the page shows.</summary>
        public static void Seen(int pawnIndex)
        {
            shownIndex = pawnIndex;
            shownFrame = Time.frameCount;
        }

        private static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.portraitLibrary ?? true) && !failed;

        /// <summary>The pawn the page shows now (drawn this frame or the last), or null.</summary>
        private static Pawn Shown(out Page_ConfigureStartingPawns page)
        {
            page = null;
            if (Current.ProgramState != ProgramState.Entry || Time.frameCount - shownFrame > 2) return null;
            var pawns = Current.Game?.InitData?.startingAndOptionalPawns;
            if (pawns == null || shownIndex < 0 || shownIndex >= pawns.Count) return null;
            page = Find.WindowStack?.WindowOfType<Page_ConfigureStartingPawns>();
            return page == null ? null : pawns[shownIndex];
        }

        /// <summary>Before the windows: a right click on the panel opens its menu (the page takes every click around it).</summary>
        public static void BeforeWindows()
        {
            if (Current.ProgramState != ProgramState.Entry || !On) return;
            var e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 1 || Time.frameCount - panelFrame > 2 || !Mouse.IsOver(panel)) return;
            try
            {
                var p = Shown(out _);
                if (p == null) return;
                Menu(p);
                e.Use();
            }
            catch (Exception ex)
            {
                failed = true;
                CouncilLog.ErrorOnce("portraits: the starting pawns' picture menu", ex);
            }
        }

        /// <summary>After the windows: the panel beside the page, where the screen has room for it.</summary>
        public static void AfterWindows()
        {
            if (Current.ProgramState != ProgramState.Entry || !On) return;
            try
            {
                var p = Shown(out var page);
                if (p == null) return;
                Prune();
                var pr = page.windowRect;
                float right = UI.screenWidth - pr.xMax, left = pr.x;
                float w = Mathf.Min(MostWide, Mathf.Max(right, left) - 2f * Gap);
                if (w < LeastWide) return;
                float inner = w - 2f * FaceMath.Border;
                float h = inner * FaceEngine.StageTall(p) + 2f * FaceMath.Border;
                h = Mathf.Min(h, pr.height - 60f);
                var r = new Rect(right >= left ? pr.xMax + Gap : pr.x - Gap - w, pr.y + 40f, w, h);
                Draw(r, p);
                panel = r;
                panelFrame = Time.frameCount;
            }
            catch (Exception ex)
            {
                failed = true;
                CouncilLog.ErrorOnce("portraits: the starting pawns' picture", ex);
            }
        }

        private static void Draw(Rect r, Pawn p)
        {
            if (Event.current.type == EventType.Repaint)
            {
                if (UiKit.OwnLook) UiKit.RoundBox(r, UiKit.PortraitBack, UiKit.Edge, SkinMath.RadiusCard);
                else Widgets.DrawBoxSolidWithOutline(r, UiKit.PortraitBack, UiKit.CardBorder, 1);
                var inner = r.ContractedBy(FaceMath.Border);
                if (!FaceEngine.DrawStaged(inner, p, inner.size, true) && !FaceEngine.DrawGameStaged(inner, p, inner.size, true))
                    FaceEngine.Draw(inner, p, FaceFrame.Bust, inner.size, 1f, true);
                var band = new Rect(inner.x, inner.yMax - BandHigh, inner.width, BandHigh);
                Widgets.DrawBoxSolid(band, UiKit.WithAlpha(UiKit.WindowGround, 0.78f));
                UiKit.LabelFit(new Rect(band.x + 4f, band.y, band.width - 8f, band.height), p.LabelShortCap, UiKit.Ink, GameFont.Small, TextAnchor.MiddleCenter);
            }
            TooltipHandler.TipRegion(r, "CC_StartingPortrait_Tip".Translate(p.Named("PAWN")).Resolve());
        }

        private static void Menu(Pawn p)
        {
            var options = new List<FloatMenuOption>();
            var lib = PortraitLibrary.Source;
            string label = "CC_Portrait_ChooseAnother".Translate().Resolve();
            if (lib == null || lib.Count == 0) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_ChooseEmpty".Translate().Resolve() + ")", null));
            else if (!lib.CanChoose(p)) options.Add(new FloatMenuOption(label + " (" + "CC_Portrait_ChooseNoneShort".Translate().Resolve() + ")", null));
            else
                options.Add(new FloatMenuOption(label, () =>
                {
                    string said = lib.ChooseAnother(p);
                    Messages.Message(said, MessageTypeDefOf.SilentInput, false);
                }));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    /// <summary>Which pawn the starting pawns' page shows (its portrait area drawn for that index).</summary>
    [HarmonyPatch(typeof(StartingPawnUtility), "DrawPortraitArea")]
    public static class Patch_StartingPortraitArea
    {
        public static void Postfix(object[] __args)
        {
            if (!RimfolkPatches.On || __args == null || __args.Length < 2 || !(__args[1] is int index)) return;
            StartingPortraits.Seen(index);
        }
    }

    /// <summary>The starting pawns' picture beside the page: its right click before the windows, the panel after them.</summary>
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    public static class Patch_StartingPortraitPanel
    {
        public static void Prefix() => StartingPortraits.BeforeWindows();

        public static void Postfix() => StartingPortraits.AfterWindows();
    }
}
