using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public partial class Camp
    {
        /// <summary>Who's there and how long they've been and will be (for the World page).</summary>
        public string StatusLine()
        {
            var here = Here;
            string who = TextUtil.JoinAnd(here.Take(4).Select(p => p.LabelShort).ToList());
            if (here.Count > 4) who += " " + "CC_Camp_AndMore".Translate(here.Count - 4).Resolve();
            switch (phase)
            {
                case CampPhase.Coming: return "CC_Camp_Status_Coming".Translate(who, place ?? "").Resolve();
                case CampPhase.Building: return "CC_Camp_Status_Building".Translate(who, place ?? "").Resolve();
                case CampPhase.Living:
                    int left = untilTick - Find.TickManager.TicksGame;
                    return (left > 0 ? "CC_Camp_Status_Living" : "CC_Camp_Status_Overstaying").Translate(who, place ?? "", TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, Find.TickManager.TicksGame - arrivedTick)), TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, left))).Resolve();
                case CampPhase.Fighting: return "CC_Camp_Status_Fighting".Translate(who).Resolve();
                default: return "CC_Camp_Status_Leaving".Translate(who).Resolve();
            }
        }

        public List<string> TermNames()
        {
            var list = new List<string>();
            if (termFee) list.Add("CC_Camp_Term_FeeAmount".Translate(fee).Resolve());
            if (termShare) list.Add("CC_Camp_Term_Share".Translate());
            if (termBoundary) list.Add("CC_Camp_Term_Boundary".Translate());
            if (termLimit) list.Add("CC_Camp_Term_Limit".Translate());
            return list;
        }

        public string TermsLine() => AnyTerms ? "CC_Camp_TermsNow".Translate(TextUtil.JoinAnd(TermNames())).Resolve() : "CC_Camp_NoTerms".Translate().Resolve();

        public string TakenLine() => "CC_Camp_TakenSoFar".Translate(treesFelled, animalsHunted).Resolve();
    }

    /// <summary>The camp on the World page: who, how long, terms, tension, and a way to talk to them.</summary>
    public static class CampPanel
    {
        public static float Section(float y, float w)
        {
            var c = CampTracker.Instance?.camp;
            if (c == null || !c.Active || c.map == null) return y;
            y += UiKit.SectionTitle(0f, y, w, "CC_Camp_Section".Translate(), Icons.Visitor, UiKit.StrangerColor);
            var lines = new List<string> { c.StatusLine(), c.TermsLine() };
            if (c.Settled && c.treesFelled + c.animalsHunted > 0) lines.Add(c.TakenLine());
            if (c.trader != null) lines.Add("CC_Camp_TraderLine".Translate(c.trader.Named("X")).Resolve());
            string text = string.Join("\n", lines);
            Text.Font = GameFont.Small;
            float textW = w - 20f;
            float textH = Text.CalcHeight(text, textW);
            float h = 8f + 26f + textH + 6f + UiKit.MeterHeight + 8f + 30f + 8f;
            var r = new Rect(0f, y, w, h);
            UiKit.Card(r, UiKit.WithAlpha(TensionColor(c.tension), 0.7f), false, false);
            float cy = r.y + 8f;
            float chipW = 0f;
            if (c.faction != null)
            {
                float maxChip = Mathf.Min(220f, w * 0.45f);
                chipW = UiKit.FactionChipWidth(c.faction, 22f, maxChip);
                UiKit.FactionChip(r.xMax - 10f - chipW, cy, c.faction, 22f, maxChip);
            }
            UiKit.LabelFit(new Rect(r.x + 10f, cy, w - chipW - 30f, 24f), c.kind.LabelCap, UiKit.StrangerColor);
            cy += 26f;
            Widgets.Label(new Rect(r.x + 10f, cy, textW, textH), text);
            cy += textH + 6f;
            UiKit.Meter(new Rect(r.x + 10f, cy, Mathf.Min(textW, 360f), UiKit.MeterHeight), c.tension / 100f, TensionColor(c.tension),
                "CC_Camp_TensionLabel".Translate(), c.TensionWord, null, "CC_Camp_TensionTip".Translate());
            cy += UiKit.MeterHeight + 8f;
            float bx = r.x + 10f;
            if (c.Settled || c.phase == CampPhase.Coming)
            {
                if (Widgets.ButtonText(new Rect(bx, cy, 180f, 28f), "CC_Camp_Talk".Translate())) CampAnswers.Talk(c, false);
                bx += 188f;
            }
            if (Widgets.ButtonText(new Rect(bx, cy, 160f, 28f), "CC_Camp_Show".Translate())) CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(c.center, c.map));
            return y + h + 14f;
        }

        public static Color TensionColor(float t) => t >= 70f ? UiKit.Bad : t >= 40f ? UiKit.Warn : t >= 15f ? UiKit.Gold : UiKit.Good;
    }

    /// <summary>
    /// Talking to a camp: their trader, the terms of their stay, gifts, hiring some of them, and asking them to go (or
    /// making them).
    /// </summary>
    public class Dialog_Camp : Window
    {
        private readonly Camp camp;
        private bool fee, share, boundary, limit;
        private string reply;
        private Vector2 scroll;
        private float height = 600f;

        public Dialog_Camp(Camp camp, bool terms)
        {
            this.camp = camp;
            fee = camp.termFee;
            share = camp.termShare;
            boundary = camp.termBoundary;
            limit = camp.termLimit;
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(640f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            if (camp == null || !camp.Active)
            {
                Close();
                return;
            }
            float top = UiKit.PageHeader(inRect, Icons.Visitor, camp.kind.LabelCap, camp.faction?.Name ?? "", null, null, 0f, null);
            var outRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top - 50f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float y = 0f;
                y += UiKit.Paragraph(0f, y, w, camp.StatusLine() + "\n" + camp.TermsLine(), Color.white) + 6f;
                UiKit.Meter(new Rect(0f, y, Mathf.Min(w, 360f), UiKit.MeterHeight), camp.tension / 100f, CampPanel.TensionColor(camp.tension),
                    "CC_Camp_TensionLabel".Translate(), camp.TensionWord, null, "CC_Camp_TensionTip".Translate());
                y += UiKit.MeterHeight + 8f;
                if (!reply.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, reply, UiKit.Quote) + 8f;
                bool here = camp.Settled || camp.phase == CampPhase.Coming;

                // Trade
                if (camp.trader != null)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_Camp_Dlg_Trade".Translate(), Icons.Trader, UiKit.Gold);
                    y += UiKit.Paragraph(0f, y, w, "CC_Camp_Dlg_TradeHow".Translate(camp.trader.Named("X")).Resolve(), UiKit.Faint) + 4f;
                    if (Widgets.ButtonText(new Rect(0f, y, 200f, 28f), "CC_Camp_Dlg_ShowTrader".Translate(camp.trader.Named("X")).Resolve()))
                    {
                        CameraJumper.TryJumpAndSelect(camp.trader);
                        Close();
                    }
                    y += 36f;
                }

                // Terms
                y += UiKit.SectionTitle(0f, y, w, "CC_Camp_Dlg_Terms".Translate(), Icons.Decree, UiKit.Info);
                y += UiKit.Paragraph(0f, y, w, "CC_Camp_Dlg_TermsHow".Translate(), UiKit.Faint) + 4f;
                Check(ref y, w, "CC_Camp_Term_Fee".Translate(), ref fee, "CC_Camp_Term_Fee_Tip".Translate(), camp.kind.canPay);
                Check(ref y, w, "CC_Camp_Term_Share".Translate(), ref share, "CC_Camp_Term_Share_Tip".Translate(), camp.kind.huntsPerDay > 0f);
                Check(ref y, w, "CC_Camp_Term_Boundary".Translate(), ref boundary, "CC_Camp_Term_Boundary_Tip".Translate(), true);
                Check(ref y, w, "CC_Camp_Term_Limit".Translate(), ref limit, "CC_Camp_Term_Limit_Tip".Translate(), true);
                bool changed = fee != camp.termFee || share != camp.termShare || boundary != camp.termBoundary || limit != camp.termLimit;
                if (Button(ref y, "CC_Camp_Dlg_PutTerms".Translate(), changed && here, here ? null : "CC_Camp_Dlg_NotYet".Translate().ToString()))
                {
                    reply = CampAnswers.SetTerms(camp, fee, share, boundary, limit);
                    fee = camp.termFee;
                    share = camp.termShare;
                    boundary = camp.termBoundary;
                    limit = camp.termLimit;
                }
                y += 6f;

                // Gifts and hiring
                y += UiKit.SectionTitle(0f, y, w, "CC_Camp_Dlg_Goodwill".Translate(), Icons.Heart, UiKit.Good);
                int gift = CampAnswers.GiftSize(camp);
                int silver = CampAnswers.Silver(camp.map);
                if (Button(ref y, "CC_Camp_Opt_Gift".Translate(gift.Named("COUNT")).Resolve(), silver >= gift, silver >= gift ? null : NotEnough(gift)))
                    reply = CampAnswers.Gift(camp, gift);
                var willing = camp.willing.Where(p => p != null && camp.Here.Contains(p)).ToList();
                if (willing.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Camp_Dlg_NoHire".Translate(), UiKit.Faint) + 4f;
                foreach (var p in willing)
                {
                    int price = CampAnswers.HirePrice(p);
                    if (Button(ref y, "CC_Camp_Dlg_Hire".Translate(p.Named("X"), price.Named("COUNT")).Resolve(), here && silver >= price, silver >= price ? null : NotEnough(price), p))
                    {
                        CampAnswers.Hire(camp, p, price);
                        reply = null;
                    }
                }
                y += 6f;

                // Ending it
                y += UiKit.SectionTitle(0f, y, w, "CC_Camp_Dlg_Leave".Translate(), Icons.Warning, UiKit.Warn);
                bool canAsk = camp.phase != CampPhase.Leaving && camp.phase != CampPhase.Fighting;
                if (Button(ref y, "CC_Camp_Opt_AskGift".Translate(gift.Named("COUNT")).Resolve(), canAsk && silver >= gift, silver >= gift ? null : NotEnough(gift)))
                {
                    if (CampAnswers.Pay(camp.map, gift)) CampAnswers.AskToLeave(camp, gift);
                    Close();
                }
                if (Button(ref y, "CC_Camp_Opt_AskLeave".Translate(), canAsk, null, null, "CC_Camp_Opt_AskLeave_Tip".Translate()))
                {
                    CampAnswers.AskToLeave(camp, 0);
                    Close();
                }
                if (Button(ref y, "CC_Camp_Opt_Force".Translate(), canAsk, null, null, "CC_Camp_Opt_Force_Tip".Translate()))
                {
                    var c = camp;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Camp_Opt_Force_Confirm".Translate(c.faction?.Name ?? "?"), () =>
                    {
                        string told = CampAnswers.DriveOff(c);
                        if (!told.NullOrEmpty()) Messages.Message(told, c.Look, MessageTypeDefOf.ThreatSmall, false);
                    }, true));
                    Close();
                }
                y += UiKit.Paragraph(0f, y, w, "CC_Camp_Dlg_Arrest".Translate(), UiKit.Faint) + 6f;
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private string NotEnough(int amount) => "CC_Dilemma_NotEnough".Translate(amount, ThingDefOf.Silver.label).ToString();

        private static void Check(ref float y, float w, string label, ref bool value, string tip, bool possible)
        {
            var r = new Rect(0f, y, Mathf.Min(w, 460f), 26f);
            if (possible) Widgets.CheckboxLabeled(r, label, ref value);
            else
            {
                bool off = false;
                Widgets.CheckboxLabeled(r, label, ref off, true);
                tip += "\n\n" + "CC_Camp_Term_NotForThem".Translate();
            }
            TooltipHandler.TipRegion(r, tip);
            y += 28f;
        }

        private static bool Button(ref float y, string label, bool active, string why, Pawn portrait = null, string tip = null)
        {
            float x = 0f;
            if (portrait != null)
            {
                UiKit.Portrait(new Rect(0f, y, 28f, 28f), portrait, UiKit.StrangerColor);
                x = 34f;
            }
            var r = new Rect(x, y, 360f, 28f);
            bool clicked = Widgets.ButtonText(r, label, true, true, active);
            string t = why ?? tip;
            if (!t.NullOrEmpty()) TooltipHandler.TipRegion(r, t);
            y += 34f;
            return clicked && active;
        }
    }
}
