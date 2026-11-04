using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's "Heard lately": the news its people told the colony's people in their talks (OutsiderTalk.Heard), newest
    /// first, each with who told whom and when. Drawn under the town's people on the region window's Neighbours page and,
    /// registered here, in the town's card (its Words slot, with the colonist told as a link).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class OutsiderHeard
    {
        private static readonly Color Accent = new Color(0.78f, 0.86f, 0.7f);
        private const float HeadH = 22f, LineH = 20f;

        static OutsiderHeard()
        {
            CardSections.TownLines(CardSlot.Words, 10f, CardLines, "CC_Out_Heard_Header", "heard");
        }

        /// <summary>The town's news on its card (Words), each line with the colonist who was told as a link to their card.</summary>
        private static List<string> CardLines(Settlement s)
        {
            if (s == null || !Outsiders.On) return null;
            try
            {
                var list = OutsiderTalk.HeardAt(s.ID, OutsiderTalkMath.HeardShown);
                if (list.Count == 0) return null;
                var names = new List<KeyValuePair<string, string>>();
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                    if (p != null && !p.LabelShort.NullOrEmpty())
                        names.Add(new KeyValuePair<string, string>(p.LabelShort, ThingRefs.Of(p).ToString()));
                var lines = new List<string>();
                foreach (var h in list)
                {
                    string line = "CC_Out_Heard_Line".Translate(h.text.Named("TEXT"), (h.teller ?? "?").Named("TELLER"), (h.told ?? "?").Named("PAWN"), Ago(h.tick).Named("AGO")).Resolve();
                    lines.Add(LinkTextMath.MarkNames(line, names));
                }
                return lines;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heard lately on a town's card", e);
                return null;
            }
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

    }
}
