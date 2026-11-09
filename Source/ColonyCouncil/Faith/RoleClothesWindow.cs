using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// "Choose other clothes for the role" (the Mood tab's button under a "wants the role's clothes" mood; Faith/RoleClothes.cs):
    /// the clothes the role asks for now, each with its icon, how the colony can come by it and whether it has one, and the
    /// others the game's own pool allows for that role and ideoligion (Precept_Role.AllPossibleRequirements, the pool its
    /// editor offers), in reach first by the rule of Faith/RoleClothesMath.cs, those that can't be worn with the rest greyed
    /// with the game's reason. The player picks which to replace and what instead, then confirms; it says it edits the
    /// ideoligion's role as the game's own editor would, and does it that way (RoleClothes.Replace). Only for the colony's own
    /// ideoligions. In Rimfolk's frame (key "RoleClothes").
    /// </summary>
    public class Dialog_RoleClothes : FramedWindow
    {
        private sealed class Row
        {
            public PreceptApparelRequirement req;
            public ThingDef icon;
            public string label, plain, how, has, tip, why;
            public RoleClothesMath.Reach reach;
            public float left;
            public bool allowed = true, met;
        }

        private readonly Precept_Role role;
        private readonly Pawn pawn;
        private readonly List<Row> now = new List<Row>(), instead = new List<Row>();
        private List<int> order = new List<int>();
        private int replace = -1, picked = -1;
        private Vector2 scroll;
        private string title, sub, note, nowHead, insteadHead, outHead, confirmLabel, confirmTip, whyNot, memeWhy, noneLine, outTag, keptTag, replaceTip, asksNothing;

        private const float RowH = 50f;

        protected override string FrameTitle => title ?? "";

        protected override Texture2D FrameIcon => Icons.CatIdeology;

        /// <param name="pawn">The colonist whose mood it came from: their own name for the role, and the clothes they lack first.</param>
        public Dialog_RoleClothes(Precept_Role role, Pawn pawn = null) : base("RoleClothes")
        {
            this.role = role;
            this.pawn = pawn;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            Build();
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(640f, UI.screenWidth - 40f), Mathf.Min(700f, UI.screenHeight - 40f));

        private bool Valid => role?.ideo != null && RoleClothes.ColonyIdeo(role.ideo) && role.ideo.PreceptsListForReading.Contains(role);

        // ---------------- The rows, worked out once (and again when what to replace changes) ----------------

        private void Build()
        {
            now.Clear();
            instead.Clear();
            if (!Valid) return;
            string roleName = pawn != null ? role.LabelForPawn(pawn) : role.LabelCap;
            var holders = role.ChosenPawns().Where(p => p != null).Select(p => p.LabelShort).ToList();
            title = "CC_RoleClothes_Title".Translate(roleName.Named("ROLE")).Resolve();
            sub = (holders.Count > 0
                ? "CC_RoleClothes_Sub".Translate(role.ideo.name.Named("IDEO"), roleName.Named("ROLE"), holders.ToCommaList(true).Named("WHO"))
                : "CC_RoleClothes_SubNobody".Translate(role.ideo.name.Named("IDEO"), roleName.Named("ROLE"))).Resolve();
            note = "CC_RoleClothes_EditNote".Translate().Resolve();
            nowHead = "CC_RoleClothes_Now".Translate().Resolve();
            insteadHead = "CC_RoleClothes_Instead".Translate().Resolve();
            outHead = "CC_RoleClothes_OutHead".Translate().Resolve();
            noneLine = "CC_RoleClothes_NoneOffered".Translate().Resolve();
            outTag = "CC_RoleClothes_OutOfReach".Translate().Resolve();
            keptTag = "CC_RoleClothes_Kept".Translate().Resolve();
            replaceTip = "CC_RoleClothes_ReplaceTip".Translate().Resolve();
            asksNothing = "CC_RoleClothes_AsksNothing".Translate().Resolve();
            var meme = role.ideo.memes?.FirstOrDefault(m => m != null && m.preventApparelRequirements);
            memeWhy = meme != null ? "CannotNotAddRoleApparelDueToMeme".Translate(meme.LabelCap.Named("MEME")).Resolve() : null;

            var ctx = new RoleClothes.Context(role.ideo, role.def);
            var unmet = pawn != null ? RoleClothes.Unmet(pawn, role) : new List<PreceptApparelRequirement>();
            if (role.apparelRequirements != null)
                foreach (var r in role.apparelRequirements)
                {
                    var row = RowOf(ctx, r);
                    row.met = pawn != null && !unmet.Contains(r);
                    now.Add(row);
                }
            List<PreceptApparelRequirement> pool;
            try
            {
                pool = Precept_Role.AllPossibleRequirements(role.ideo, role.def, true);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("role clothes: the game's pool", e);
                pool = new List<PreceptApparelRequirement>();
            }
            foreach (var r in pool)
            {
                if (r?.requirement == null || now.Any(n => RoleClothes.SameAs(n.req, r)) || instead.Any(n => RoleClothes.SameAs(n.req, r))) continue;
                if (RoleClothes.Pieces(r.requirement).Count == 0) continue;
                instead.Add(RowOf(ctx, r));
            }

            // What to replace first: what is out of reach, else what they lack, else the first.
            replace = now.FindIndex(n => !RoleClothesMath.Reachable(n.reach));
            if (replace < 0) replace = now.FindIndex(n => pawn != null && !n.met);
            if (replace < 0 && now.Count > 0) replace = 0;
            Recheck();
        }

        private static Row RowOf(RoleClothes.Context ctx, PreceptApparelRequirement r)
        {
            var best = ctx.Best(r);
            var pieces = RoleClothes.Pieces(r.requirement);
            var row = new Row
            {
                req = r,
                icon = best.def ?? pieces.FirstOrDefault(),
                label = RoleClothes.LabelOf(r),
                plain = RoleClothes.LabelOf(r, false),
                reach = best.reach,
                how = best.item != null ? ctx.HowToGet(best.item, best.reach) : "CC_RoleClothes_How_Unmade".Translate().Resolve(),
                has = best.item != null && best.item.has > 0
                    ? "CC_RoleClothes_HasCount".Translate(best.item.has.Named("COUNT")).Resolve()
                    : "CC_RoleClothes_HasNone".Translate().Resolve()
            };
            if (best.item != null)
            {
                int w = RoleClothesMath.BestWay(best.item, ctx.ages);
                row.left = w < 0 ? 0f : RoleClothesMath.Left(best.item.ways[w]);
            }
            var tip = new List<string>();
            if (row.icon != null) tip.Add(row.icon.LabelCap.Resolve() + (row.icon.description.NullOrEmpty() ? "" : "\n" + row.icon.description));
            if (pieces.Count > 1) tip.Add("CC_RoleClothes_AnyOf".Translate(pieces.Select(p => p.label).ToCommaList(false).Named("WHAT")).Resolve());
            tip.Add(row.how.CapitalizeFirst());
            if (best.item != null && (best.reach == RoleClothesMath.Reach.OutOfReach || best.reach == RoleClothesMath.Reach.CalledFor))
            {
                string whyOut = ctx.WhyOut(best.item);
                if (!whyOut.NullOrEmpty()) tip.Add(whyOut);
                if (best.reach == RoleClothesMath.Reach.CalledFor) tip.Add("CC_RoleClothes_KeptTip".Translate().Resolve());
            }
            row.tip = string.Join("\n\n", tip);
            return row;
        }

        /// <summary>Which of the others can take the place of the one to replace (the game's own checks), their order, and the confirm button's words.</summary>
        private void Recheck()
        {
            var others = new List<PreceptApparelRequirement>();
            if (role.apparelRequirements != null)
                for (int i = 0; i < role.apparelRequirements.Count; i++)
                    if (i != replace && role.apparelRequirements[i] != null)
                        others.Add(role.apparelRequirements[i]);
            foreach (var row in instead)
            {
                row.allowed = true;
                row.why = null;
                try
                {
                    if (!row.req.CanAddRequirement(role, others, out string reason))
                    {
                        row.allowed = false;
                        row.why = reason.NullOrEmpty() ? "CC_RoleClothes_CantAdd".Translate().Resolve() : reason.CapitalizeFirst();
                    }
                    else if (row.req.RequirementOverlapsOther(others, out string clash))
                    {
                        row.allowed = false;
                        row.why = clash.NullOrEmpty() ? "CC_RoleClothes_CantAdd".Translate().Resolve() : clash.CapitalizeFirst();
                    }
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("role clothes: can it be added", e);
                    row.allowed = false;
                    row.why = "CC_RoleClothes_CantAdd".Translate().Resolve();
                }
            }
            order = RoleClothesMath.Order(instead.Select(r => new RoleClothesMath.Choice(r.reach, r.left, r.label, r.allowed)).ToList());
            if (picked >= 0 && (picked >= instead.Count || !instead[picked].allowed)) picked = -1;
            Words();
        }

        private void Words()
        {
            bool ready = replace >= 0 && replace < now.Count && picked >= 0 && picked < instead.Count;
            confirmLabel = ready
                ? "CC_RoleClothes_Confirm".Translate(instead[picked].plain.Named("NEW"), now[replace].plain.Named("OLD")).Resolve()
                : "CC_RoleClothes_ConfirmEmpty".Translate().Resolve();
            confirmTip = "CC_RoleClothes_ConfirmTip".Translate().Resolve();
            whyNot = memeWhy ?? (replace < 0 ? "CC_RoleClothes_NothingToReplace".Translate().Resolve() : !ready ? "CC_RoleClothes_PickFirst".Translate().Resolve() : null);
        }

        // ---------------- Drawing ----------------

        protected override void DoFramedContents(Rect inRect)
        {
            if (!Valid)
            {
                Close();
                return;
            }
            Text.Font = GameFont.Small;
            float y = inRect.y;
            y += UiKit.DialogHeader(new Rect(inRect.x, y, inRect.width - (TitleInBar ? 0f : 24f), UiKit.HeaderHeight), TitleInBar, Icons.CatIdeology, title, sub);
            y += UiKit.Paragraph(inRect.x, y, inRect.width, note, UiKit.Faint, GameFont.Tiny) + 8f;

            // What the role asks for now: pick which to replace.
            y += UiKit.SectionTitle(inRect.x, y, inRect.width, nowHead, now.Count, Icons.CatIdeology);
            if (now.Count == 0)
            {
                UiKit.EmptyLine(new Rect(inRect.x, y, inRect.width, UiKit.RowDense + 4f), asksNothing);
                y += UiKit.RowDense + 8f;
            }
            for (int i = 0; i < now.Count; i++)
            {
                var r = new Rect(inRect.x, y, inRect.width, RowH);
                if (DrawRow(r, now[i], i, true, i == replace) && replace != i)
                {
                    replace = i;
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Recheck();
                }
                y += RowH + 2f;
            }
            y += 8f;

            // What it could ask for instead.
            y += UiKit.SectionTitle(inRect.x, y, inRect.width, insteadHead, instead.Count);
            var outer = new Rect(inRect.x, y, inRect.width, Mathf.Max(RowH, inRect.yMax - y - 46f));
            if (instead.Count == 0) UiKit.EmptyLine(new Rect(outer.x, outer.y, outer.width, UiKit.RowDense + 4f), noneLine);
            else
            {
                int firstOut = order.FindIndex(i => !RoleClothesMath.Reachable(instead[i].reach));
                float headH = firstOut >= 0 ? 26f : 0f;
                var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(order.Count * (RowH + 2f) + headH, outer.height - 1f));
                Widgets.BeginScrollView(outer, ref scroll, view);
                float ry = 0f;
                for (int k = 0; k < order.Count; k++)
                {
                    if (k == firstOut)
                    {
                        UiKit.LabelFit(new Rect(2f, ry + 4f, view.width - 4f, 20f), outHead, UiKit.Faint, GameFont.Tiny);
                        ry += headH;
                    }
                    int i = order[k];
                    var r = new Rect(0f, ry, view.width, RowH);
                    if (ry + RowH >= scroll.y && ry <= scroll.y + outer.height && DrawRow(r, instead[i], k, false, i == picked) && instead[i].allowed && picked != i)
                    {
                        picked = i;
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        Words();
                    }
                    ry += RowH + 2f;
                }
                Widgets.EndScrollView();
            }

            float by = inRect.yMax - 38f;
            if (UiKit.Button(new Rect(inRect.x, by, 140f, 34f), "CancelButton".Translate())) Close();
            float cw = Mathf.Min(inRect.width - 150f, Mathf.Max(220f, UiKit.ActButtonWidth(confirmLabel) + 20f));
            if (UiKit.ActButton(new Rect(inRect.xMax - cw, by, cw, 34f), confirmLabel, whyNot, confirmTip)) Confirm();
        }

        /// <summary>One piece: its icon, name, whether the colony has one, how to come by it, and why it can't be picked. True when clicked.</summary>
        private bool DrawRow(Rect r, Row row, int index, bool current, bool selected)
        {
            UiKit.Zebra(r, index);
            if (selected) Widgets.DrawHighlightSelected(r);
            else if (current ? now.Count > 1 : row.allowed) UiKit.RowHover(r);
            float x = r.x + 4f;
            if (current && now.Count > 1)
            {
                Widgets.RadioButton(x, r.y + (r.height - 24f) / 2f, selected);
                x += 30f;
            }
            if (row.icon != null) Widgets.ThingIcon(new Rect(x, r.y + 7f, 36f, 36f), row.icon);
            x += 44f;
            bool dim = !current && !row.allowed;
            bool outOfReach = !RoleClothesMath.Reachable(row.reach);
            // The first line: the name, and on the right whether the colony has one (and if it is out of reach, or kept).
            string right = row.has + (outOfReach ? "  ·  " + outTag : row.reach == RoleClothesMath.Reach.CalledFor ? "  ·  " + keptTag : "");
            Text.Font = GameFont.Tiny;
            float rw = Mathf.Min(r.width * 0.45f, Text.CalcSize(right).x + 6f);
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(x, r.y + 3f, r.xMax - x - rw - 8f, 24f), row.label, dim ? UiKit.Faint : current && !row.met && pawn != null ? UiKit.Soon : Color.white);
            UiKit.LabelFit(new Rect(r.xMax - rw - 4f, r.y + 3f, rw, 24f), right, outOfReach ? UiKit.Sour : row.reach == RoleClothesMath.Reach.Has ? UiKit.Warm : UiKit.Faint, GameFont.Tiny,
                TextAnchor.MiddleRight);
            // The second line: how to come by it, or why it can't take the place of the one replaced.
            string second = dim && !row.why.NullOrEmpty() ? row.why : row.how.CapitalizeFirst();
            UiKit.LabelFit(new Rect(x, r.y + 25f, r.xMax - x - 8f, 22f), second, dim ? UiKit.Soon : outOfReach ? UiKit.Sour : UiKit.Faint, GameFont.Tiny);
            string tip = row.tip;
            if (dim && !row.why.NullOrEmpty()) tip = row.why + "\n\n" + tip;
            else if (current && now.Count > 1) tip = replaceTip + "\n\n" + tip;
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            return Widgets.ButtonInvisible(r, false);
        }

        private void Confirm()
        {
            if (replace < 0 || replace >= now.Count || picked < 0 || picked >= instead.Count || memeWhy != null) return;
            string was = now[replace].plain, becomes = instead[picked].plain;
            if (!RoleClothes.Replace(role, replace, instead[picked].req)) return;
            string roleName = pawn != null ? role.LabelForPawn(pawn) : role.LabelCap;
            Messages.Message("CC_RoleClothes_Done".Translate(roleName.Named("ROLE"), role.ideo.name.Named("IDEO"), becomes.Named("NEW"), was.Named("OLD")).Resolve(),
                MessageTypeDefOf.TaskCompletion, false);
            SoundDefOf.Quest_Accepted.PlayOneShotOnCamera();
            Close();
        }
    }
}
