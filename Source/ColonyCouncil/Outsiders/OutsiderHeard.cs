using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's "Heard lately": the news its people told the colony's people in their talks (OutsiderTalk.Heard), newest
    /// first, each with who told whom and when. Drawn under the town's people on the World page's Neighbours section and,
    /// registered here, in the region map's panel for the town (<see cref="NeighbourPanel.Sections"/>).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class OutsiderHeard
    {
        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);
        private const float HeadH = 22f, LineH = 20f;

        static OutsiderHeard()
        {
            NeighbourPanel.Sections.Add(DrawTown);
        }

        /// <summary>The height the town's news takes (0 with none).</summary>
        public static float Height(int town)
        {
            if (!Outsiders.On) return 0f;
            int n = OutsiderTalk.HeardAt(town, OutsiderTalkMath.HeardShown).Count;
            return n == 0 ? 0f : HeadH + n * LineH + 4f;
        }

        /// <summary>The town's news in <paramref name="r"/>, from its top; returns the height used.</summary>
        public static float Draw(Rect r, int town)
        {
            if (!Outsiders.On) return 0f;
            var list = OutsiderTalk.HeardAt(town, OutsiderTalkMath.HeardShown);
            if (list.Count == 0) return 0f;
            float y = r.y;
            UiKit.LabelFit(new Rect(r.x, y, r.width, HeadH), "CC_Out_Heard_Header".Translate().Resolve(), Accent);
            y += HeadH;
            foreach (var h in list)
            {
                string line = "CC_Out_Heard_Line".Translate(h.text.Named("TEXT"), (h.teller ?? "?").Named("TELLER"), (h.told ?? "?").Named("PAWN"), Ago(h.tick).Named("AGO")).Resolve();
                var row = new Rect(r.x + 6f, y, r.width - 6f, LineH);
                UiKit.LabelFit(row, line, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, line);
                y += LineH;
            }
            return y + 4f - r.y;
        }

        /// <summary>"today", "yesterday", "5 days ago" (never a countdown).</summary>
        private static string Ago(int tick)
        {
            int days = Mathf.Max(0, (Find.TickManager.TicksGame - tick) / GenDate.TicksPerDay);
            if (days == 0) return "CC_Out_Heard_Today".Translate().Resolve();
            if (days == 1) return "CC_Out_Heard_Yesterday".Translate().Resolve();
            return "CC_Out_Heard_DaysAgo".Translate(days).Resolve();
        }

        private static float DrawTown(Rect rect, Settlement s)
        {
            if (s == null) return 0f;
            try
            {
                return Draw(rect, s.ID);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard lately section", e);
                return 0f;
            }
        }
    }
}
