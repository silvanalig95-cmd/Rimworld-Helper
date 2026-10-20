using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk's column in the game's own Social tab (UI/SocialColumn.cs). The game's tab is 540 wide and draws its card
    // with SocialCardUtility.DrawSocialCard from FillTab. Three small patches, each applied on its own:
    //   - FillTab: a prefix makes the tab wider when the column shows (and gives it back its own width when it doesn't),
    //     a postfix draws the column in the room to the right, and a finalizer forgets the colonist even if the game's
    //     card throws;
    //   - DrawSocialCard: while the Social tab is drawing for that colonist, the card keeps its own width, however
    //     FillTab sizes it;
    //   - GetPawnRowTooltip: a row of the game's list gets a Rimfolk line about the pair at the end of its own tooltip.
    // The column only draws when both of the first two are in place, the window really is wide enough (another mod may
    // keep the tab at a width of its own) and the game's card really was kept to its width this frame. Otherwise the
    // game's tab is left as it is.

    /// <summary>What the Social tab is drawing this frame, shared by the patches below.</summary>
    public static class SocialTabState
    {
        /// <summary>The colonist the Social tab is drawing for, while its FillTab runs; null otherwise.</summary>
        public static Pawn drawingFor;
        /// <summary>The width to keep the game's card to this frame, or below 0 to leave it.</summary>
        public static float cardWidth = -1f;
        /// <summary>The game's card was drawn and kept to its width this frame.</summary>
        public static bool cardKept;
        /// <summary>The column should show this frame (wanted, and the window is wide enough).</summary>
        public static bool columnShown;
        /// <summary>The tab window's size this frame.</summary>
        public static float windowWidth, windowHeight;
        /// <summary>The tab's own width (540 in the game), and the width Rimfolk last gave it.</summary>
        public static float baseWidth = -1f, lastSet = -1f;

        public static void Clear()
        {
            drawingFor = null;
            cardWidth = -1f;
            cardKept = false;
            columnShown = false;
        }
    }

    /// <summary>The Social tab: wider with the column, the column drawn beside the game's card.</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Social), "FillTab")]
    public static class Patch_SocialTabColumn
    {
        private static AccessTools.FieldRef<InspectTabBase, Vector2> sizeRef;
        private static Func<ITab_Pawn_Social, Pawn> pawnOf;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                sizeRef = AccessTools.FieldRefAccess<InspectTabBase, Vector2>("size");
            }
            catch (Exception e)
            {
                sizeRef = null;
                CouncilLog.ErrorOnce("social tab: the tab's size", e);
            }
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "SelPawnForSocialInfo");
                if (getter != null && !getter.IsStatic && getter.ReturnType == typeof(Pawn))
                    pawnOf = (Func<ITab_Pawn_Social, Pawn>)Delegate.CreateDelegate(typeof(Func<ITab_Pawn_Social, Pawn>), getter);
            }
            catch (Exception)
            {
                pawnOf = null;
            }
        }

        /// <summary>Whom the tab is about: the game's own answer, or the selected colonist (or the one in a selected corpse).</summary>
        private static Pawn PawnOf(ITab_Pawn_Social tab)
        {
            if (pawnOf != null)
            {
                try
                {
                    return pawnOf(tab);
                }
                catch (Exception)
                {
                    pawnOf = null;
                }
            }
            var thing = Find.Selector?.SingleSelectedThing;
            return thing as Pawn ?? (thing as Corpse)?.InnerPawn;
        }

        public static void Prefix(ITab_Pawn_Social __instance)
        {
            SocialTabState.Clear();
            try
            {
                Look();
                if (sizeRef == null || __instance == null) return;
                ref Vector2 size = ref sizeRef(__instance);
                float now = size.x;
                SocialTabState.baseWidth = TabFitMath.BaseWidth(now, SocialTabState.lastSet, SocialTabState.baseWidth);
                float baseWidth = SocialTabState.baseWidth;
                // The window this is drawn in: the tab's own (as wide as the tab was when it was placed), or whatever
                // window another mod draws the tab in.
                var window = Find.WindowStack?.currentlyDrawnWindow;
                SocialTabState.windowWidth = window != null ? Mathf.Min(window.windowRect.width, now) : now;
                SocialTabState.windowHeight = window != null ? window.windowRect.height : size.y;
                var pawn = PawnOf(__instance);
                float column = SocialColumn.Wants(pawn) ? TabFitMath.ColumnWidth(UI.screenWidth, baseWidth) : 0f;
                float want = TabFitMath.WithColumn(baseWidth, column);
                if (Mathf.Abs(size.x - want) > 0.5f) size.x = want;
                SocialTabState.lastSet = want;
                SocialTabState.drawingFor = pawn;
                // Keep the game's card to its own width whenever the tab is (or is about to be) wider than that.
                SocialTabState.cardWidth = want > baseWidth + 0.5f || now > baseWidth + 0.5f ? baseWidth : -1f;
                SocialTabState.columnShown = column > 0f && TabFitMath.ColumnFits(SocialTabState.windowWidth, baseWidth);
                SocialColumn.Noted(column > 0f, SocialTabState.columnShown);
            }
            catch (Exception e)
            {
                SocialTabState.Clear();
                CouncilLog.ErrorOnce("social tab: making room", e);
            }
        }

        public static void Postfix()
        {
            var p = SocialTabState.drawingFor;
            if (p == null || !SocialTabState.columnShown || !SocialTabState.cardKept) return;
            try
            {
                float x = SocialTabState.baseWidth + TabFitMath.ColumnGap;
                float w = SocialTabState.windowWidth - x;
                if (w < TabFitMath.ColumnMin - 1f) return;
                SocialColumn.Draw(new Rect(x, 0f, w, SocialTabState.windowHeight), p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("social tab: the column", e);
            }
        }

        /// <summary>Always, even when the game's card threw: nothing is left behind for the next card drawn elsewhere.</summary>
        public static void Finalizer() => SocialTabState.Clear();
    }

    /// <summary>While the Social tab draws for a colonist with Rimfolk's column, the game's card keeps its own width.</summary>
    [HarmonyPatch(typeof(SocialCardUtility), nameof(SocialCardUtility.DrawSocialCard))]
    public static class Patch_SocialCardWidth
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref Rect rect, Pawn pawn)
        {
            var p = SocialTabState.drawingFor;
            if (p == null || pawn != p) return;
            float w = SocialTabState.cardWidth;
            if (w > 0f && rect.width > w + 0.5f) rect.width = w;
            SocialTabState.cardKept = rect.width <= SocialTabState.baseWidth + 0.5f;
        }
    }

    /// <summary>A row of the game's Social tab: a Rimfolk line about the pair at the end of the game's own tooltip.</summary>
    [HarmonyPatch(typeof(SocialCardUtility), "GetPawnRowTooltip")]
    public static class Patch_SocialRowTip
    {
        private static FieldInfo otherPawn;
        private static bool looked;

        private static Pawn OtherOf(object entry)
        {
            if (entry == null) return null;
            if (!looked)
            {
                looked = true;
                otherPawn = AccessTools.Field(entry.GetType(), "otherPawn");
                if (otherPawn != null && otherPawn.FieldType != typeof(Pawn)) otherPawn = null;
            }
            return otherPawn?.GetValue(entry) as Pawn;
        }

        public static void Postfix(object entry, Pawn selPawnForSocialInfo, ref string __result)
        {
            if (!SocialColumn.TipsWanted || selPawnForSocialInfo == null) return;
            try
            {
                string extra = SocialColumn.PairNote(selPawnForSocialInfo, OtherOf(entry));
                if (extra.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? extra : __result.TrimEnd() + "\n\n" + extra;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("social tab: a row's tooltip", e);
            }
        }
    }
}
