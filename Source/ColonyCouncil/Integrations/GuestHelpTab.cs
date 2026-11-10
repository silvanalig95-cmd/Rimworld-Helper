using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Helping out" on Hospitality's guest tab, under its own lines and above Rimfolk's recruiting checklist
    /// (GuestCheck.cs draws both from the same patch, and the tab grows to fit): the switch "Let them help" (on by
    /// default), the trouble they're pitching in to, a day's work in thanks, today's everyday hours against the cap, what
    /// they'd help with and why ("Cooking: burning passion"), and what they won't ("No hauling: titled").
    /// </summary>
    public static class GuestHelpTab
    {
        public static bool On => GuestHelp.Any;

        internal static void Draw(Listing_Standard listing, Pawn g)
        {
            if (listing == null || g == null || !On || !Strangers.IsHospitalityGuest(g)) return;
            var tab = GuestHelp.TabOf(g);
            var font = Text.Font;
            bool oneColumn = listing.maxOneColumn;
            listing.maxOneColumn = true;
            listing.GapLine(8f);
            Text.Font = GameFont.Small;
            var head = listing.GetRect(Text.LineHeight);
            float w = head.width;
            float boxW = Mathf.Min(170f, w * 0.45f);
            UiKit.LabelFit(new Rect(head.x, head.y, w - boxW - 6f, head.height), "CC_GuestHelp_Title".Translate(g.Named("PAWN")), UiKit.HeaderColor);
            bool let = GuestHelp.LetHelp(g);
            bool was = let;
            var box = new Rect(head.xMax - boxW, head.y, boxW, head.height);
            Widgets.CheckboxLabeled(box, "CC_GuestHelp_Let".Translate(), ref let);
            TooltipHandler.TipRegion(box, "CC_GuestHelp_Let_Tip".Translate(g.Named("PAWN")));
            if (let != was) GuestHelp.SetLetHelp(g, let);
            Text.Font = GameFont.Tiny;
            foreach (var (text, color) in tab.lines)
            {
                float h = Mathf.Max(16f, Text.CalcHeight(text, w));
                var r = listing.GetRect(h);
                GUI.color = color;
                Widgets.Label(r, text);
                GUI.color = Color.white;
            }
            Text.Font = font;
            listing.maxOneColumn = oneColumn;
        }
    }
}
