using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The "Council" tab: priorities, wishes, ambition, build plans and the chronicle.</summary>
    public class MainTabWindow_Council : MainTabWindow
    {
        private enum Tab
        {
            Priorities,
            Wishes,
            Ambition,
            Build,
            Chronicle
        }

        private static Tab tab = Tab.Priorities;
        private Vector2 scroll;
        private float lastHeight = 1000f;
        private static int lastListenTick = -999999;

        private static readonly Color CriticalColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color MediumColor = new Color(0.9f, 0.85f, 0.35f);
        private static readonly Color LowColor = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color IdeaColor = new Color(0.45f, 0.7f, 0.95f);
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color QuoteColor = new Color(1f, 0.93f, 0.75f);
        private static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.55f);

        public override Vector2 RequestedTabSize =>
            new Vector2(Mathf.Min(1010f, UI.screenWidth - 20f), Mathf.Min(720f, UI.screenHeight - 35f - 20f));

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public override void DoWindowContents(Rect inRect)
        {
            var tabs = new List<TabRecord>
            {
                new TabRecord("CC_Tab_Priorities".Translate(), () => SetTab(Tab.Priorities), tab == Tab.Priorities),
                new TabRecord("CC_Tab_Wishes".Translate(OpenWishCount()), () => SetTab(Tab.Wishes), tab == Tab.Wishes),
                new TabRecord("CC_Tab_Ambition".Translate(), () => SetTab(Tab.Ambition), tab == Tab.Ambition),
                new TabRecord("CC_Tab_Build".Translate(), () => SetTab(Tab.Build), tab == Tab.Build),
                new TabRecord("CC_Tab_Chronicle".Translate(), () => SetTab(Tab.Chronicle), tab == Tab.Chronicle)
            };
            var body = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs, 190f);
            var content = body.ContractedBy(12f);

            var map = Find.CurrentMap;
            var comp = CouncilGameComponent.Instance;
            if (comp == null)
            {
                Widgets.Label(content, "CC_NoGame".Translate());
                return;
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            switch (tab)
            {
                case Tab.Priorities: DrawPriorities(content, map); break;
                case Tab.Wishes: DrawWishes(content, comp); break;
                case Tab.Ambition: DrawAmbition(content, comp, map); break;
                case Tab.Build: DrawBuild(content, map); break;
                case Tab.Chronicle: DrawChronicle(content, comp); break;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void SetTab(Tab t)
        {
            tab = t;
            scroll = Vector2.zero;
        }

        private static int OpenWishCount() => CouncilGameComponent.Instance?.OpenWishes.Count() ?? 0;

        // ---------------- Shared helpers ----------------

        private float BeginScroll(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 18f, lastHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            return view.width;
        }

        private void EndScroll(float height)
        {
            lastHeight = Mathf.Max(height + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small)
        {
            if (text.NullOrEmpty()) return 0f;
            Text.Font = font;
            float h = Text.CalcHeight(text, width);
            GUI.color = color ?? Color.white;
            Widgets.Label(new Rect(x, y, width, h), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return h;
        }

        private static Color LevelColor(AdviceLevel level)
        {
            switch (level)
            {
                case AdviceLevel.Critical: return CriticalColor;
                case AdviceLevel.High: return HighColor;
                case AdviceLevel.Medium: return MediumColor;
                case AdviceLevel.Low: return LowColor;
                default: return IdeaColor;
            }
        }

        private void StartPlanning(RoomTemplateDef template, bool findSpot)
        {
            if (template == null || Find.CurrentMap == null) return;
            string reason = template.UnavailableReason(Find.CurrentMap);
            if (reason != null)
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var des = new Designator_PlanRoom(template);
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            if (findSpot) des.SuggestSpot();
        }

        private static void Jump(LookTargets look)
        {
            if (look == null || !look.IsValid) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            CameraJumper.TryJumpAndSelect(look.PrimaryTarget);
        }

        // ---------------- Priorities ----------------

        private void DrawPriorities(Rect rect, Map map)
        {
            if (!Settings.enableAdvisor)
            {
                Widgets.Label(rect, "CC_AdvisorDisabled".Translate());
                return;
            }
            if (map == null || !map.IsPlayerHome)
            {
                Widgets.Label(rect, "CC_NoHomeMap".Translate());
                return;
            }
            var mc = CouncilMapComponent.For(map);
            var top = new Rect(rect.x, rect.y, rect.width, 48f);
            string era = "CC_Era".Translate(Avail.EffectiveTech.ToStringHuman().CapitalizeFirst(),
                (Avail.HasElectricity ? "CC_Yes" : "CC_No").Translate());
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(top.x, top.y, top.width - 160f, 32f), "CC_Priorities_Header".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Faint;
            Widgets.Label(new Rect(top.x, top.y + 28f, top.width - 160f, 22f), era);
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(top.xMax - 150f, top.y + 4f, 150f, 30f), "CC_Refresh".Translate())) mc?.Refresh(true);

            var signals = mc?.Signals ?? new List<AdvisorSignal>();
            var outRect = new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 56f);
            float width = BeginScroll(outRect);
            float y = 0f;
            if (signals.Count == 0)
            {
                y += WrappedLabel(0f, y, width, "CC_Priorities_AllGood".Translate(), GoodColor);
            }
            foreach (var s in signals)
            {
                y += DrawSignal(s, 0f, y, width, map) + 8f;
            }
            EndScroll(y);
        }

        private float DrawSignal(AdvisorSignal s, float x, float y, float width, Map map)
        {
            float textX = x + 120f;
            float textW = width - 120f - 140f;
            float h = 0f;
            Text.Font = GameFont.Small;
            float titleH = Text.CalcHeight(s.title, textW);
            float detailH = Text.CalcHeight(s.detail ?? "", textW);
            string tip = s.Tip;
            Text.Font = GameFont.Tiny;
            float tipH = tip.NullOrEmpty() ? 0f : Text.CalcHeight("CC_WhyItMatters".Translate() + ": " + tip, textW);
            Text.Font = GameFont.Small;
            h = Mathf.Max(64f, titleH + detailH + tipH + 14f);
            var row = new Rect(x, y, width, h);
            Widgets.DrawLightHighlight(row);

            var badge = new Rect(x + 6f, y + 8f, 104f, 24f);
            Widgets.DrawBoxSolid(badge, LevelColor(s.Level) * new Color(1f, 1f, 1f, 0.35f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(badge, ("CC_Level_" + s.Level).Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x + 6f, y + 36f, 104f, 20f), ("CC_Cat_" + s.def.category).Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            float cy = y + 6f;
            GUI.color = LevelColor(s.Level);
            Widgets.Label(new Rect(textX, cy, textW, titleH), s.title);
            GUI.color = Color.white;
            cy += titleH;
            Widgets.Label(new Rect(textX, cy, textW, detailH), s.detail ?? "");
            cy += detailH + 2f;
            if (!tip.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Faint;
                Widgets.Label(new Rect(textX, cy, textW, tipH), "CC_WhyItMatters".Translate() + ": " + tip);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            float bx = x + width - 132f;
            float by = y + 6f;
            if (s.look != null && s.look.IsValid)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 128f, 26f), "CC_Show".Translate())) Jump(s.look);
                by += 30f;
            }
            if (s.template != null && map != null)
            {
                string reason = s.template.UnavailableReason(map);
                if (reason == null)
                {
                    if (Widgets.ButtonText(new Rect(bx, by, 128f, 26f), "CC_PlanIt".Translate())) StartPlanning(s.template, true);
                }
                by += 30f;
            }
            if (s.suggestThing != null)
            {
                var iconRect = new Rect(bx + 100f, y + h - 30f, 26f, 26f);
                Widgets.ThingIcon(iconRect, s.suggestThing);
                TooltipHandler.TipRegion(iconRect, s.suggestThing.LabelCap);
            }
            return h;
        }

        // ---------------- Wishes ----------------

        private void DrawWishes(Rect rect, CouncilGameComponent comp)
        {
            if (!Settings.enableWishes)
            {
                Widgets.Label(rect, "CC_WishesDisabled".Translate());
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 220f, 32f), "CC_Wishes_Header".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Faint;
            Widgets.Label(new Rect(rect.x, rect.y + 28f, rect.width - 220f, 22f), ("CC_Stakes_Short_" + Settings.stakes).Translate());
            GUI.color = Color.white;

            int now = Find.TickManager.TicksGame;
            bool canListen = now - lastListenTick > GenDate.TicksPerDay;
            var listenRect = new Rect(rect.xMax - 210f, rect.y + 4f, 210f, 30f);
            if (Widgets.ButtonText(listenRect, "CC_Listen".Translate(), true, true, canListen))
            {
                lastListenTick = now;
                var w = comp.TryGenerateWish(true);
                if (w == null) Messages.Message("CC_Listen_Nothing".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            TooltipHandler.TipRegion(listenRect, canListen ? "CC_Listen_Tip".Translate() : "CC_Listen_Cooldown".Translate());

            var outRect = new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 56f);
            float width = BeginScroll(outRect);
            float y = 0f;
            var open = comp.OpenWishes.OrderBy(w => w.TicksLeft).ToList();
            if (open.Count == 0)
                y += WrappedLabel(0f, y, width, "CC_Wishes_None".Translate(), Faint) + 8f;
            foreach (var w in open)
                y += DrawWish(comp, w, 0f, y, width) + 8f;

            y += 10f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "CC_Wishes_Recent".Translate());
            Text.Font = GameFont.Small;
            y += 32f;
            int shown = 0;
            for (int i = comp.chronicle.Count - 1; i >= 0 && shown < 12; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind != ChronicleKind.WishGranted && e.kind != ChronicleKind.WishExpired && e.kind != ChronicleKind.WishDismissed && e.kind != ChronicleKind.Petition) continue;
                var col = e.kind == ChronicleKind.WishGranted ? GoodColor : Faint;
                y += WrappedLabel(0f, y, width, AgoString(e.tick) + "  " + e.text, col) + 2f;
                shown++;
            }
            EndScroll(y);
        }

        private float DrawWish(CouncilGameComponent comp, Wish w, float x, float y, float width)
        {
            float textX = x + 84f;
            float textW = width - 84f - 150f;
            string header = w.ForSomeoneElse
                ? "CC_WishHeaderForOther".Translate(w.pawn.LabelShort, w.Beneficiary.LabelShort, w.Label).Resolve()
                : "CC_WishHeader".Translate(w.pawn.LabelShort, w.Label).Resolve();
            string quote = "\"" + w.quote + "\"";
            var lines = new List<string>();
            if (!w.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + w.reason);
            string when = w.Worker.FulfilledWhen(w);
            if (!when.NullOrEmpty()) lines.Add("CC_FulfilledWhen".Translate() + ": " + when);
            string prog = SafeProgressText(w);
            if (!prog.NullOrEmpty()) lines.Add("CC_Progress".Translate() + ": " + prog);
            if (w.coSigners.Count > 0) lines.Add("CC_Petition".Translate(TextUtil.JoinAnd(w.coSigners.Select(p => p.LabelShort).ToList())));
            lines.Add("CC_Reward".Translate() + ": " + comp.RewardText(w) + "   ·   " + "CC_Expires".Translate() + ": " + TextUtil.DaysString(w.TicksLeft));
            string body = string.Join("\n", lines);

            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(header, textW) + Text.CalcHeight(quote, textW) + Text.CalcHeight(body, textW) + 18f;
            float progress = SafeProgress(w);
            if (progress >= 0f) h += 14f;
            h = Mathf.Max(h, 96f);
            var row = new Rect(x, y, width, h);
            Widgets.DrawLightHighlight(row);

            var portrait = new Rect(x + 6f, y + 6f, 70f, 70f);
            Widgets.ThingIcon(portrait, w.pawn);
            if (w.ForSomeoneElse) Widgets.ThingIcon(new Rect(x + 46f, y + 52f, 36f, 36f), w.Beneficiary);

            float cy = y + 6f;
            float hh = Text.CalcHeight(header, textW);
            Widgets.Label(new Rect(textX, cy, textW, hh), header);
            cy += hh;
            float qh = Text.CalcHeight(quote, textW);
            GUI.color = QuoteColor;
            Widgets.Label(new Rect(textX, cy, textW, qh), quote);
            GUI.color = Color.white;
            cy += qh + 2f;
            float bh = Text.CalcHeight(body, textW);
            Widgets.Label(new Rect(textX, cy, textW, bh), body);
            cy += bh + 2f;
            if (progress >= 0f) Widgets.FillableBar(new Rect(textX, cy, Mathf.Min(300f, textW), 10f), progress);

            float bx = x + width - 142f;
            float by = y + 6f;
            if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_Show".Translate())) Jump(w.Worker.LookAt(w));
            by += 30f;
            RoomTemplateDef template = null;
            try
            {
                template = w.Worker.SuggestedTemplate(w);
            }
            catch
            {
            }
            if (template != null && Find.CurrentMap != null && template.UnavailableReason(Find.CurrentMap) == null)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_PlanIt".Translate())) StartPlanning(template, true);
                by += 30f;
            }
            if (w.thingDef != null && w.thingDef.BuildableByPlayer && Avail.CanBuild(w.thingDef, Find.CurrentMap))
            {
                if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_BuildThing".Translate(w.thingDef.label)))
                {
                    var des = BuildCopyCommandUtility.FindAllowedDesignator(w.thingDef);
                    if (des != null)
                    {
                        Find.MainTabsRoot.EscapeCurrentTab(false);
                        Find.DesignatorManager.Select(des);
                    }
                }
                by += 30f;
            }
            if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_NotNow".Translate())) comp.Dismiss(w);
            TooltipHandler.TipRegion(new Rect(bx, by, 138f, 26f), "CC_NotNow_Tip".Translate());
            return h;
        }

        private static string SafeProgressText(Wish w)
        {
            try
            {
                return w.Worker.ProgressText(w);
            }
            catch
            {
                return null;
            }
        }

        private static float SafeProgress(Wish w)
        {
            try
            {
                return w.Worker.Progress(w);
            }
            catch
            {
                return -1f;
            }
        }

        // ---------------- Ambition ----------------

        private void DrawAmbition(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (!Settings.enableAmbitions)
            {
                Widgets.Label(rect, "CC_AmbitionsDisabled".Translate());
                return;
            }
            var tracker = comp.ambitions;
            float width = BeginScroll(rect);
            float y = 0f;
            var a = tracker.active;
            Text.Font = GameFont.Medium;
            if (a != null)
            {
                Widgets.Label(new Rect(0f, y, width, 32f), "CC_Ambition_Current".Translate(a.def.LabelCap.ToString()));
                Text.Font = GameFont.Small;
                y += 34f;
                y += WrappedLabel(0f, y, width, a.def.description, Faint) + 6f;
                string meta = "CC_Ambition_Meta".Translate(a.proposer?.LabelShort ?? "?", TextUtil.DaysString(Mathf.Max(0, a.deadlineTick - Find.TickManager.TicksGame)));
                y += WrappedLabel(0f, y, width, meta) + 6f;
                Widgets.FillableBar(new Rect(0f, y, Mathf.Min(400f, width), 18f), a.Progress);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0f, y, Mathf.Min(400f, width), 18f), a.Progress.ToStringPercent());
                Text.Anchor = TextAnchor.UpperLeft;
                y += 26f;
                for (int i = 0; i < a.def.goals.Count; i++)
                {
                    var g = a.def.goals[i];
                    float p = g.Progress(a.map, a.Baseline(i));
                    bool done = p >= 0.999f;
                    Widgets.CheckboxDraw(6f, y, done, false, 22f);
                    y += Mathf.Max(24f, WrappedLabel(34f, y + 1f, width - 34f, g.Describe(a.map, a.Baseline(i)), done ? GoodColor : Color.white)) + 4f;
                }
                y += 10f;
                if (a.def.template != null && map != null && a.def.template.UnavailableReason(map) == null)
                {
                    if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f), "CC_PlanIt".Translate())) StartPlanning(a.def.template, true);
                }
                if (Widgets.ButtonText(new Rect(210f, y, 200f, 30f), "CC_Ambition_Abandon".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Ambition_AbandonConfirm".Translate(a.def.label), () => tracker.Abandon(), true));
                }
                y += 40f;
            }
            else
            {
                Widgets.Label(new Rect(0f, y, width, 32f), "CC_Ambition_None".Translate());
                Text.Font = GameFont.Small;
                y += 34f;
                int ticks = tracker.TicksUntilMeeting;
                y += WrappedLabel(0f, y, width, ticks >= 0 ? "CC_Ambition_NextMeeting".Translate(TextUtil.DaysString(ticks)).Resolve() : "CC_Ambition_SoonMeeting".Translate().Resolve(), Faint) + 8f;
                bool can = tracker.CanCallMeetingNow(out string reason);
                var btn = new Rect(0f, y, 240f, 32f);
                if (Widgets.ButtonText(btn, "CC_Ambition_CallMeeting".Translate(), true, true, can) && can) tracker.HoldMeeting(comp, true);
                if (!can && reason != null) TooltipHandler.TipRegion(btn, reason);
                y += 42f;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "CC_Ambition_History".Translate());
            Text.Font = GameFont.Small;
            y += 32f;
            int shown = 0;
            for (int i = comp.chronicle.Count - 1; i >= 0 && shown < 15; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind < ChronicleKind.Meeting || e.kind > ChronicleKind.AmbitionAbandoned) continue;
                var col = e.kind == ChronicleKind.AmbitionAchieved ? GoodColor : Faint;
                y += WrappedLabel(0f, y, width, AgoString(e.tick) + "  " + e.text, col) + 2f;
                shown++;
            }
            EndScroll(y);
        }

        // ---------------- Build ----------------

        private void DrawBuild(Rect rect, Map map)
        {
            if (map == null)
            {
                Widgets.Label(rect, "CC_NoHomeMap".Translate());
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Build_Header".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Faint;
            Widgets.Label(new Rect(rect.x, rect.y + 28f, rect.width, 22f), "CC_Build_Sub".Translate());
            GUI.color = Color.white;
            var outRect = new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 56f);
            float width = BeginScroll(outRect);
            float y = 0f;
            foreach (var t in DefDatabase<RoomTemplateDef>.AllDefsListForReading.OrderBy(d => d.order))
            {
                var furniture = t.ResolveCached(map, out string reason);
                string furnitureText = furniture.Count == 0 ? "" : "CC_Build_Contains".Translate(string.Join(", ", furniture.GroupBy(f => f.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label)));
                float textW = width - 330f;
                string title = t.LabelCap + "  (" + t.size.x + "×" + t.size.z + ")";
                string desc = t.purpose ?? t.description;
                float h = Mathf.Max(60f, Text.CalcHeight(title, textW) + Text.CalcHeight(desc ?? "", textW) + Text.CalcHeight(furnitureText, textW) + 14f);
                var row = new Rect(0f, y, width, h);
                Widgets.DrawLightHighlight(row);
                float cy = y + 6f;
                cy += WrappedLabel(8f, cy, textW, title);
                cy += WrappedLabel(8f, cy, textW, desc, Faint);
                WrappedLabel(8f, cy, textW, reason ?? furnitureText, reason != null ? HighColor : Color.white);

                float bx = width - 310f;
                if (reason == null)
                {
                    if (Widgets.ButtonText(new Rect(bx, y + 8f, 150f, 30f), "CC_Build_Plan".Translate())) StartPlanning(t, false);
                    if (Widgets.ButtonText(new Rect(bx + 156f, y + 8f, 150f, 30f), "CC_Plan_FindSpot".Translate())) StartPlanning(t, true);
                }
                y += h + 6f;
            }
            EndScroll(y);
        }

        // ---------------- Chronicle ----------------

        private void DrawChronicle(Rect rect, CouncilGameComponent comp)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Chronicle_Header".Translate());
            Text.Font = GameFont.Small;
            var outRect = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);
            float width = BeginScroll(outRect);
            float y = 0f;
            if (comp.chronicle.Count == 0) y += WrappedLabel(0f, y, width, "CC_Chronicle_Empty".Translate(), Faint);
            for (int i = comp.chronicle.Count - 1; i >= 0; i--)
            {
                var e = comp.chronicle[i];
                Color col;
                switch (e.kind)
                {
                    case ChronicleKind.WishGranted:
                    case ChronicleKind.AmbitionAchieved:
                    case ChronicleKind.PersonalTouch:
                        col = GoodColor;
                        break;
                    case ChronicleKind.WishExpired:
                    case ChronicleKind.AmbitionFailed:
                    case ChronicleKind.AmbitionAbandoned:
                    case ChronicleKind.WishDismissed:
                        col = Faint;
                        break;
                    case ChronicleKind.Meeting:
                    case ChronicleKind.AmbitionChosen:
                        col = IdeaColor;
                        break;
                    default:
                        col = Color.white;
                        break;
                }
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, 110f, 24f), DayString(e.tick));
                GUI.color = Color.white;
                y += WrappedLabel(115f, y, width - 115f, e.text, col) + 4f;
            }
            EndScroll(y);
        }

        private static string DayString(int tick) => "CC_DayN".Translate(tick / GenDate.TicksPerDay + 1);

        private static string AgoString(int tick)
        {
            int days = (Find.TickManager.TicksGame - tick) / GenDate.TicksPerDay;
            return days <= 0 ? "CC_Today".Translate().Resolve() : "CC_DaysAgo".Translate(days).Resolve();
        }
    }
}
