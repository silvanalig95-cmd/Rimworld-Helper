using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Wishes, "Promises kept": the granted lasting wishes being watched (Wishes/Kept.cs), one row each: who,
    /// what, and where it stands: kept, missing (with the grace time left) or taken back (with the time left to make it
    /// right). Taken back and missing come first.
    /// </summary>
    public static class KeptPanel
    {
        private const float RowH = 28f;

        /// <summary>Draws the section at <paramref name="y"/>; returns the height used (0 when there's nothing to show).</summary>
        public static float Draw(CouncilGameComponent comp, float y, float width)
        {
            var watches = comp?.kept?.watches;
            if (!KeptTracker.On || watches == null || watches.Count == 0) return 0f;
            float start = y;
            y += 16f;
            y += UiKit.SectionTitle(0f, y, width, "CC_Kept_Header".Translate(), Icons.Promise);
            y += UiKit.Paragraph(0f, y, width, "CC_Kept_Sub".Translate(), UiKit.Faint) + 6f;
            int now = Find.TickManager.TicksGame;
            var rows = UiCache.Get(comp, 73, () => watches.Where(w => w?.pawn != null)
                .OrderBy(w => w.phase == KeptPhase.TakenBack ? 0 : w.phase == KeptPhase.Missing ? 1 : 2)
                .ThenBy(w => w.watchUntil).ToList(), 0.5f);
            for (int i = 0; i < rows.Count; i++)
            {
                Row(rows[i], y, width, i, now);
                y += RowH;
            }
            return y - start;
        }

        private static void Row(GrantWatch w, float y, float width, int i, int now)
        {
            var row = new Rect(0f, y, width, RowH - 2f);
            UiKit.Zebra(row, i);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

            string state;
            Color color;
            Texture2D icon;
            switch (w.phase)
            {
                case KeptPhase.Missing:
                    state = "CC_Kept_State_Missing".Translate(KeptTracker.Span(Mathf.Max(0, w.graceUntil - now))).Resolve();
                    color = UiKit.Warn;
                    icon = Icons.Warning;
                    break;
                case KeptPhase.TakenBack:
                    state = "CC_Kept_State_TakenBack".Translate().Resolve();
                    color = UiKit.Bad;
                    icon = Icons.Warning;
                    break;
                default:
                    state = "CC_Kept_State_Kept".Translate().Resolve();
                    color = UiKit.Good;
                    icon = Icons.Promise;
                    break;
            }
            float pillW = Mathf.Min(UiKit.PillWidth(state, icon), Mathf.Max(80f, width * 0.35f));
            float x = 4f;
            x += UiKit.PawnChip(x, y + 1f, w.pawn, 24f, true, Mathf.Min(170f, width * 0.3f)) + 10f;
            UiKit.LabelFit(new Rect(x, y + 1f, Mathf.Max(20f, width - x - pillW - 14f), 24f), w.label.CapitalizeFirst(), Color.white);
            UiKit.Pill(width - pillW - 6f, y + 4f, state, color, icon, null, pillW);
            var watch = w;
            TooltipHandler.TipRegion(row, () => Tip(watch), w.id ^ 0x4B3E7);
        }

        /// <summary>Built only when the tooltip shows.</summary>
        private static string Tip(GrantWatch w)
        {
            int now = Find.TickManager.TicksGame;
            string ago = KeptTracker.Span(Mathf.Max(1, now - w.grantedTick));
            switch (w.phase)
            {
                case KeptPhase.Missing:
                    return KeptTracker.LostText(w) + "\n\n" + "CC_Kept_Tip_Missing".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, w.graceUntil - now)).Named("LEFT")).Resolve();
                case KeptPhase.TakenBack:
                    int amends = w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay) - now;
                    return "CC_Kept_Tip_TakenBack".Translate(w.pawn.Named("PAWN"), KeptTracker.Span(Mathf.Max(1, amends)).Named("LEFT")).Resolve();
                default:
                    return "CC_Kept_Tip_Kept".Translate(w.pawn.Named("PAWN"), ago.Named("AGO"), KeptTracker.Span(Mathf.Max(1, w.watchUntil - now)).Named("LEFT")).Resolve();
            }
        }
    }
}
