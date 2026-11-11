using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The rider's supplies for the road (Post/RiderPacks.cs): what is packed by kind (food, medicine, drugs, the beast's
    /// feed, anything else), each line's number to change, take off or put back; "Add from the stores" (the journey
    /// planner's own picker) and "Pack the recommended"; below, the food against the road there and back, the beast's feed,
    /// the medicine and the load, and when it falls short, "Let them go short, knowing it". From the mailbag's "Send with our
    /// rider" it sends the letter (greyed with why); from the letter's window it is done and the window sends. In Rimfolk's
    /// frame (key "RiderPack").
    /// </summary>
    public class Dialog_RiderPack : FramedWindow
    {
        private readonly RiderPackModel model;

        /// <summary>The mailbag's send, with the supplies; null from the letter's window (Done).</summary>
        private readonly Action<RiderPack> send;

        private Vector2 scroll;
        private float listHeight;

        private const float RowH = 44f, RemovedH = 30f;

        protected override string FrameTitle => "CC_RiderPack_Title".Translate().Resolve();

        protected override Texture2D FrameIcon => RoadFoodIcon.Icon;

        public Dialog_RiderPack(RiderPackModel model, Action<RiderPack> send) : base("RiderPack")
        {
            this.model = model;
            this.send = send;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            forcePause = true;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(580f, UI.screenWidth - 20f), Mathf.Min(700f, UI.screenHeight - 20f));

        /// <summary>The supplies window for a model (one at a time).</summary>
        public static void Open(RiderPackModel model, Action<RiderPack> send)
        {
            if (model == null) return;
            Find.WindowStack.WindowOfType<Dialog_RiderPack>()?.Close(false);
            model.Refresh(true);
            Find.WindowStack.Add(new Dialog_RiderPack(model, send));
        }

        /// <summary>The mailbag's "Send with our rider", the rider chosen: the supplies, then the letter goes with them.</summary>
        public static void ForLetter(Map map, NeighbourLetter l, Pawn rider)
        {
            var town = Outbox.TownOf(l, out _);
            if (map == null || l == null || rider == null || town == null) return;
            Open(new RiderPackModel(map, rider, town), pack =>
            {
                // Still in the bag and the rider still free to go: else said, and nothing sent.
                if (!l.Open || l.stage != LetterStage.Outbox || l.ownRider || !OwnRiders.Candidates(map).Contains(rider))
                {
                    // apart: the answer to the player's own click.
                    Tell.Message(NewsKind.World, Importance.Important, map, PostWords.P("CC_RiderPack_Gone"), LookTargets.Invalid, MessageTypeDefOf.RejectInput);
                    return;
                }
                OwnRiders.Send(l, rider, pack);
            });
        }

        protected override void DoFramedContents(Rect inRect)
        {
            try
            {
                Contents(inRect);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's supplies window", e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void Contents(Rect inRect)
        {
            // Gone with what it belongs to: the map, the rider, the town, or the letter's window it was opened from.
            if (model?.map == null || !Find.Maps.Contains(model.map) || model.rider == null || model.rider.Dead || model.town == null
                || send == null && Find.WindowStack.WindowOfType<Dialog_NeighbourLetter>() == null)
            {
                Close();
                return;
            }
            model.Refresh();
            float y = inRect.y;
            y += UiKit.DialogHeader(new Rect(inRect.x, y, inRect.width - (TitleInBar ? 0f : 24f), UiKit.HeaderHeight), TitleInBar, RoadFoodIcon.Icon,
                "CC_RiderPack_Title".Translate(), model.Subtitle());

            // The foot: the lines on the food, the feed, the medicine and the load; going short knowingly; the buttons.
            var lines = model.Lines();
            string why = model.Why;
            float lineW = inRect.width;
            Text.Font = GameFont.Small;
            float footH = 34f + 8f + 30f + lines.Sum(x => Text.CalcHeight(x.text, lineW) + 2f);
            bool shortNow = model.verdict.Short && !model.verdict.over;
            if (shortNow) footH += 28f;
            if (why != null) footH += Text.CalcHeight(why, lineW) + 4f;

            var list = new Rect(inRect.x, y, inRect.width, Mathf.Max(60f, inRect.yMax - y - footH));
            DrawList(list);
            y = list.yMax + 6f;

            // Add from the stores (the journey planner's picker), and the suggestions again.
            var add = new Rect(inRect.x, y, 200f, 26f);
            if (UiKit.Button(add, "CC_Journey_AddFromStores".Translate(), ButtonKind.Normal, true, "CC_Journey_AddFromStores_Tip".Translate().Resolve())) OpenPicker();
            var again = new Rect(add.xMax + 8f, y, 200f, 26f);
            if (UiKit.Button(again, "CC_RiderPack_Recommended".Translate(), ButtonKind.Normal, true, "CC_RiderPack_Recommended_Tip".Translate().Resolve()))
            {
                model.Recommended();
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            y += 30f;

            foreach (var (text, color) in lines)
            {
                float h = Text.CalcHeight(text, lineW);
                GUI.color = color;
                Widgets.Label(new Rect(inRect.x, y, lineW, h), text);
                GUI.color = Color.white;
                y += h + 2f;
            }
            if (shortNow)
            {
                bool k = model.pack.knowingly;
                var box = new Rect(inRect.x, y + 2f, Mathf.Min(inRect.width, 360f), 24f);
                UiKit.Toggle(box, "CC_RiderPack_Knowingly".Translate(), ref k, "CC_RiderPack_Knowingly_Tip".Translate().Resolve());
                model.pack.knowingly = k;
                y += 28f;
            }
            if (why != null)
            {
                float h = Text.CalcHeight(why, lineW);
                GUI.color = UiKit.Bad;
                Widgets.Label(new Rect(inRect.x, y, lineW, h), why);
                GUI.color = Color.white;
            }

            // Send (the mailbag's) or Done (the letter's window sends).
            var main = new Rect(inRect.x, inRect.yMax - 34f, 200f, 32f);
            if (send != null)
            {
                bool ok = model.MaySend;
                if (UiKit.Button(main, "CC_LetterDlg_Send".Translate(), ButtonKind.Primary, ok, ok ? null : why))
                {
                    Close();
                    send(model.pack);
                }
                if (UiKit.Button(new Rect(inRect.xMax - 160f, inRect.yMax - 34f, 160f, 32f), "CC_LetterDlg_Cancel".Translate())) Close();
            }
            else if (UiKit.Button(main, "CC_RiderPack_Done".Translate(), ButtonKind.Primary)) Close();
        }

        // ---------------- The list ----------------

        private void DrawList(Rect outer)
        {
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(listHeight, outer.height - 1f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            int index = 0;
            var shown = model.pack.lines.Where(l => l?.def != null).ToList();
            if (shown.Count == 0)
            {
                UiKit.LabelFit(new Rect(0f, y, view.width, 28f), "CC_RiderPack_Nothing".Translate(), UiKit.Faint);
                y += 30f;
            }
            foreach (string group in JourneyMath.Groups)
            {
                var rows = shown.Where(l => JourneyMath.GroupOf(l.kind) == group).ToList();
                if (rows.Count == 0) continue;
                string title = group == "feed" && model.mount != null
                    ? "CC_RiderPack_Group_Feed".Translate(model.mount.LabelShort.Named("MOUNT")).Resolve()
                    : ("CC_Journey_Group_" + group).Translate().Resolve();
                y += UiKit.SectionTitle(0f, y, view.width, title, rows.Count(l => l.Taken));
                foreach (var line in rows.ToList())
                {
                    y = line.removed ? RemovedRow(line, y, view.width, index) : Row(line, y, view.width, index);
                    index++;
                }
                y += 4f;
            }
            listHeight = y;
            Widgets.EndScrollView();
        }

        private int ToHand(ThingDef d) => JourneyPack.InStores(model.map, d) + model.Carried(d);

        private float Row(JourneyLine line, float y, float w, int index)
        {
            var row = new Rect(0f, y, w, RowH);
            UiKit.Zebra(row, index);
            int toHand = ToHand(line.def);
            bool mine = line.edited || line.added;
            float cy = y + 10f;
            float right = w - 2f;
            var remove = new Rect(right - 20f, cy + 2f, 20f, 20f);
            right -= 26f;
            var all = new Rect(right - 38f, cy, 38f, 24f);
            right -= 44f;
            var plus = new Rect(right - 24f, cy, 24f, 24f);
            right -= 26f;
            var count = new Rect(right - 46f, cy, 46f, 24f);
            right -= 48f;
            var minus = new Rect(right - 24f, cy, 24f, 24f);
            right -= 30f;

            Widgets.ThingIcon(new Rect(4f, y + 8f, 28f, 28f), line.def);
            UiKit.LabelFit(new Rect(38f, y + 2f, Mathf.Max(0f, right - 38f), 22f), line.def.LabelCap, mine ? UiKit.Gold : Color.white);
            UiKit.LabelFit(new Rect(38f, y + 23f, Mathf.Max(0f, right - 38f), 17f), Details(line, toHand, out bool warn), warn ? UiKit.Warn : UiKit.Dim, GameFont.Tiny);
            var about = new Rect(0f, y, right, RowH);
            if (Mouse.IsOver(about)) TooltipHandler.TipRegion(about, Tip(line, toHand));

            int step = GenUI.CurrentAdjustmentMultiplier();
            string stepTip = "CC_Journey_StepTip".Translate();
            if (UiKit.Button(minus, "-")) Set(line, RiderPackMath.Step(line.count, -step, toHand));
            TooltipHandler.TipRegion(minus, stepTip);
            UiKit.LabelFit(count, line.count.ToStringCached(), line.count > toHand ? UiKit.Warn : Color.white, GameFont.Small, TextAnchor.MiddleCenter);
            if (UiKit.Button(plus, "+")) Set(line, RiderPackMath.Step(line.count, step, toHand));
            TooltipHandler.TipRegion(plus, stepTip);
            Text.Font = GameFont.Tiny;
            if (UiKit.Button(all, "CC_Journey_SetAll".Translate())) Set(line, toHand);
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(all, "CC_Journey_SetAllTip".Translate(toHand));
            if (Widgets.ButtonImage(remove, TexButton.CloseXSmall))
            {
                if (line.added) model.pack.lines.Remove(line);
                else
                {
                    line.removed = true;
                    line.edited = false;
                    line.count = 0;
                }
                model.Changed();
            }
            TooltipHandler.TipRegion(remove, (line.added ? "CC_Journey_RemoveAddedTip" : "CC_RiderPack_RemoveTip").Translate());
            return y + RowH + 2f;
        }

        /// <summary>A suggestion taken off: faint, with the way to put it back.</summary>
        private float RemovedRow(JourneyLine line, float y, float w, int index)
        {
            UiKit.Zebra(new Rect(0f, y, w, RemovedH), index);
            GUI.color = UiKit.WithAlpha(UiKit.Ink, 0.45f);
            Widgets.ThingIcon(new Rect(8f, y + 5f, 20f, 20f), line.def);
            GUI.color = Color.white;
            var back = new Rect(w - 112f, y + 3f, 110f, 24f);
            UiKit.LabelFit(new Rect(38f, y, Mathf.Max(0f, back.x - 44f), RemovedH), "CC_Journey_TakenOff".Translate(line.def.LabelCap).Resolve(), UiKit.Dim);
            Text.Font = GameFont.Tiny;
            if (UiKit.Button(back, "CC_Journey_PutBack".Translate()))
            {
                line.removed = false;
                model.Refresh(true);
            }
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(back, "CC_RiderPack_PutBackTip".Translate());
            return y + RemovedH + 2f;
        }

        private void Set(JourneyLine line, int count)
        {
            if (count == line.count) return;
            line.count = count;
            if (!line.added) line.edited = true;
            model.Changed();
        }

        /// <summary>Whose number it is, the days it feeds the rider, how many are to hand, and a word when the rider won't eat it on the road.</summary>
        private string Details(JourneyLine line, int toHand, out bool warn)
        {
            warn = false;
            var parts = new List<string> { (line.added ? "CC_Journey_Added" : line.edited ? "CC_Journey_YourNumber" : "CC_RiderPack_Suggested").Translate().Resolve() };
            float perDay = model.facts.perDay;
            if (line.kind == "food")
            {
                if (!RiderPacks.Eats(model.rider, line.def))
                {
                    warn = true;
                    parts.Add("CC_RiderPack_WontEat".Translate(model.rider.LabelShort.Named("PAWN")).Resolve());
                }
                else if (perDay > 0f) parts.Add("CC_Journey_LineDays".Translate(RiderPackMath.Days(line.count * JourneyPack.Nutrition(line.def) / perDay)).Resolve());
            }
            parts.Add("CC_RiderPack_ToHand".Translate(toHand.Named("COUNT")).Resolve());
            if (line.count > toHand)
            {
                warn = true;
                parts.Add("CC_RiderPack_MoreThanToHand".Translate().Resolve());
            }
            float kg = line.count * JourneyPack.UnitMass(line.def);
            parts.Add("CC_Journey_Kg".Translate(kg.ToString(kg < 10f ? "0.#" : "0")).Resolve());
            return string.Join(" · ", parts);
        }

        private static string Tip(JourneyLine line, int toHand)
        {
            string desc = line.def.description ?? "";
            if (desc.Length > 300) desc = desc.Substring(0, 300).TrimEnd() + "...";
            string group = ("CC_Journey_Group_" + JourneyMath.GroupOf(line.kind) + "_Tip").Translate().Resolve();
            return line.def.LabelCap + (desc.Length > 0 ? "\n\n" + desc : "") + "\n\n" + "CC_RiderPack_ToHand".Translate(toHand.Named("COUNT")).Resolve().CapitalizeFirst()
                   + "\n" + "CC_RiderPack_LineTip".Translate().Resolve() + (group.NullOrEmpty() ? "" : "\n\n" + group);
        }

        // ---------------- The picker ----------------

        /// <summary>The journey planner's "Add from the stores" on the rider's own list, beside this window; it closes with it.</summary>
        private void OpenPicker()
        {
            var plan = RiderPacks.Shadow(model.map, model.rider, model.mount, model.town, model.pack.lines);
            float w = Mathf.Min(580f, UI.screenWidth - 20f);
            float h = Mathf.Clamp(windowRect.height, 320f, Mathf.Min(720f, UI.screenHeight - 20f));
            float x = windowRect.x - w - 6f;
            if (x < 0f) x = Mathf.Min(windowRect.xMax + 6f, UI.screenWidth - w);
            float y = Mathf.Clamp(windowRect.y, 0f, UI.screenHeight - h);
            Find.WindowStack.Add(new Dialog_JourneyPick(plan, model.Changed, new Rect(x, y, w, h), () => Find.WindowStack.IsOpen(this), false));
        }
    }
}
