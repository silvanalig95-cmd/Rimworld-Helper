using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Wishes: what the colonists ask the council for, as cards (who asks, what they want, in their words,
    /// with tags and a meter for the time left), what came of earlier wishes, and how much each one trusts the council.
    /// </summary>
    public static class WishesPanel
    {
        // The interface's tokens (UI/UiKit.cs): spoiled and leaving sour, particular soon, a wish's progress under way.
        private static readonly Color HighColor = UiKit.Sour;
        private static readonly Color MediumColor = UiKit.Soon;
        private static readonly Color ProgressColor = UiKit.Cool;
        private static int lastListenTick = -999999;
        private static Vector2 scroll;
        private static float height = 1000f;

        private const float PortraitSize = 56f;
        private const float TextX = 82f;
        private const float ButtonsW = 150f;
        private const float ThingW = 44f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        /// <summary>A wish card's text and measurements, worked out about once a second (and again when the width changes).</summary>
        private class Layout
        {
            public float width = -1f, at;
            public string header, quote, details, progressText;
            /// <summary>The header as drawn: who asks (and for whom) as links to their cards.</summary>
            public string headerShown;
            public float headerH, quoteH, pillsH, detailsH, height, progress = -1f;
            public List<UiKit.PillSpec> pills = new List<UiKit.PillSpec>();
            public RoomTemplateDef template;
            public bool canBuild;
            public int buttons;
        }

        private static readonly Dictionary<Wish, Layout> layouts = new Dictionary<Wish, Layout>();

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!Settings.enableWishes)
            {
                UiKit.EmptyState(rect, Icons.Wishes, "CC_WishesDisabled".Translate());
                return;
            }
            // Not cached: a wish answered with a click must leave the list at once.
            var open = comp.OpenWishes.OrderBy(w => w.TicksLeft).ToList();
            string stat = "CC_UI_Stat_Open".Translate(open.Count).Resolve();
            float top = UiKit.PageHeader(rect, Icons.Wishes, "CC_Wishes_Header".Translate(), ("CC_Stakes_Short_" + Settings.stakes).Translate(), stat, null, 220f);

            int now = Find.TickManager.TicksGame;
            bool canListen = now - lastListenTick > GenDate.TicksPerDay;
            var listenRect = new Rect(rect.xMax - 210f, rect.y + 6f, 210f, UiKit.RowStandard);
            // Greyed with why not (once a day) rather than refused after the click.
            if (UiKit.ActButton(listenRect, "CC_Listen".Translate(), canListen ? null : "CC_Listen_Cooldown".Translate().ToString(), "CC_Listen_Tip".Translate()))
            {
                lastListenTick = now;
                // In the colony in view.
                var w = comp.TryGenerateWish(true, null, ColonyState.InView);
                // direct: the answer to the player's click on "Listen".
                if (w == null) Messages.Message("CC_Listen_Nothing".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float width = view.width;
            float y = 0f;
            if (open.Count == 0) y += UiKit.EmptyState(0f, y, width, Icons.Wishes, "CC_Wishes_None".Translate(), 120f) + 6f;
            if (layouts.Count > 100) layouts.Clear();
            foreach (var w in open)
            {
                var l = LayoutOf(comp, w, width);
                // Only cards on screen are drawn.
                if (y + l.height >= scroll.y && y <= scroll.y + outRect.height) DrawWish(comp, w, l, 0f, y, width);
                y += l.height + UiKit.CardGap;
            }

            y += UiKit.SectionGap - UiKit.CardGap;
            y += UiKit.SectionTitle(0f, y, width, "CC_Wishes_Recent".Translate(), Icons.Chronicle);
            int shown = 0;
            for (int i = comp.chronicle.Count - 1; i >= 0 && shown < 12; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind != ChronicleKind.WishGranted && e.kind != ChronicleKind.WishExpired && e.kind != ChronicleKind.WishDismissed && e.kind != ChronicleKind.Petition) continue;
                y += HistoryRow(e, y, width, shown);
                shown++;
            }
            if (shown == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, width, UiKit.RowDense + 4f), "CC_Overview_Nothing".Translate());
                y += UiKit.RowDense + 8f;
            }

            // Promises kept: what was granted and could still be taken away (UI/KeptPanel.cs).
            y += KeptPanel.Draw(comp, y, width);

            y += UiKit.SectionGap;
            y += UiKit.SectionTitle(0f, y, width, "CC_Trust_Header".Translate(), Icons.Trust);
            y += UiKit.Paragraph(0f, y, width, "CC_Trust_Sub".Translate(), UiKit.Faint) + 6f;
            var pawns = UiCache.Get(comp, 61, () => PawnsFinder.AllMaps_FreeColonists.Where(x => !x.IsQuestLodger()).OrderBy(x => comp.standings.TrustOf(x)).ToList(), 0.5f);
            for (int i = 0; i < pawns.Count; i++)
            {
                TrustRow(comp, pawns[i], y, width, i);
                y += 30f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        // ---------------- Wish cards ----------------

        private static float TextWidth(Wish w, float width) => Mathf.Max(80f, width - TextX - ButtonsW - 8f - (w.thingDef != null ? ThingW : 0f));

        private static Layout LayoutOf(CouncilGameComponent comp, Wish w, float width)
        {
            float now = Time.realtimeSinceStartup;
            if (layouts.TryGetValue(w, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 1f) return l;
            l = new Layout { width = width, at = now };
            l.header = w.ForSomeoneElse
                ? "CC_WishHeaderForOther".Translate(w.pawn.LabelShort, w.Beneficiary.LabelShort, w.Label).Resolve()
                : "CC_WishHeader".Translate(w.pawn.LabelShort, w.Label).Resolve();
            l.quote = (w.scene.NullOrEmpty() ? "" : w.scene + "\n") + "\"" + w.quote + "\"";

            // Tags: how they feel about the council, and what's special about this wish.
            var standing = comp.standings.For(w.pawn, false);
            var level = standing?.Level ?? TrustLevel.Neutral;
            l.pills.Add(new UiKit.PillSpec(("CC_Trust_" + level).Translate().CapitalizeFirst(), UiKit.TrustColor(standing?.trust ?? 0f), Icons.Trust,
                "CC_TrustLabel".Translate(("CC_Trust_" + level).Translate())));
            if (w.promised) l.pills.Add(new UiKit.PillSpec("CC_PromisedTag".Translate().CapitalizeFirst(), UiKit.Gold, Icons.Promise, "CC_Promise_Tip".Translate()));
            if (w.stage >= 2) l.pills.Add(new UiKit.PillSpec("CC_LastCallTag".Translate().CapitalizeFirst(), UiKit.Bad, Icons.Warning));
            // Who envies it, who objects: the tag leads to their card.
            if (w.envied != null) l.pills.Add(new UiKit.PillSpec("CC_EnvyTag".Translate(w.envied.LabelShort).CapitalizeFirst(), UiKit.Soon, null, null, ThingRefs.Of(w.envied)));
            if (w.objector != null) l.pills.Add(new UiKit.PillSpec("CC_ObjectorTag".Translate(w.objector.LabelShort).CapitalizeFirst(), UiKit.Sour, null, null, ThingRefs.Of(w.objector)));
            if (w.coSigners.Count > 0)
                l.pills.Add(new UiKit.PillSpec("CC_UI_Pill_Petition".Translate(w.coSigners.Count), UiKit.Info, Icons.People,
                    "CC_Petition".Translate(TextUtil.JoinAnd(w.coSigners.Select(p => p.LabelShort).ToList()))));

            l.progress = SafeProgress(w);
            l.progressText = SafeProgressText(w);
            var lines = new List<string>();
            if (!w.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + w.reason);
            string when = Safe(() => w.Worker.FulfilledWhen(w));
            if (!when.NullOrEmpty()) lines.Add("CC_FulfilledWhen".Translate() + ": " + when);
            // Progress with a bar goes into the meter; without one it stays a line.
            if (l.progress < 0f && !l.progressText.NullOrEmpty()) lines.Add("CC_Progress".Translate() + ": " + l.progressText);
            lines.Add("CC_Reward".Translate() + ": " + comp.RewardText(w));
            l.details = string.Join("\n", lines);

            l.template = SuggestedTemplate(w);
            l.canBuild = w.thingDef != null && w.thingDef.BuildableByPlayer && Avail.CanBuild(w.thingDef, Find.CurrentMap);
            l.buttons = 2 + (l.template != null ? 1 : 0) + (l.canBuild ? 1 : 0) + (w.promised ? 0 : 1);
            l.headerShown = PaneLinks.Mark(l.header, w.ForSomeoneElse ? new[] { w.pawn, w.Beneficiary } : new[] { w.pawn });

            float textW = TextWidth(w, width);
            Text.Font = GameFont.Small;
            l.headerH = LinkText.Height(l.headerShown, textW);
            l.quoteH = Text.CalcHeight(l.quote, textW);
            l.pillsH = UiKit.Pills(0f, 0f, textW, l.pills, false);
            Text.Font = GameFont.Tiny;
            l.detailsH = Text.CalcHeight(l.details, textW);
            Text.Font = GameFont.Small;
            float textH = 10f + l.headerH + l.quoteH + 4f + l.pillsH + 6f + l.detailsH + 6f + UiKit.MeterHeight + 8f;
            l.height = Mathf.Max(textH, PortraitSize + 24f, l.buttons * ButtonStep + 12f);
            layouts[w] = l;
            return l;
        }

        private static void DrawWish(CouncilGameComponent comp, Wish w, Layout l, float x, float y, float width)
        {
            var accent = w.stage >= 2 ? UiKit.Sour : w.promised ? UiKit.Gold : UiKit.WithAlpha(UiKit.Faint, 0.5f);
            var card = new Rect(x, y, width, l.height);
            UiKit.Card(card, accent, false, false);
            // The pin to This week, in the corner: always when pinned, else when the card is under the mouse.
            var memory = WeekMemory.Instance;
            if (memory != null && WeekPanel.PinButton(new Rect(x + 4f, y + 3f, 16f, 16f), memory.IsPinned(w), Mouse.IsOver(card))) memory.TogglePin(w);

            // Who asks (and for whom).
            var portrait = new Rect(x + 14f, y + 10f, PortraitSize, PortraitSize);
            UiKit.Portrait(portrait, w.pawn, UiKit.TrustColor(comp.standings.TrustOf(w.pawn)));
            var asker = ThingRefs.Of(w.pawn);
            if (Mouse.IsOver(portrait)) Previews.Hover(asker, portrait);
            if (Previews.Shown != asker) TooltipHandler.TipRegion(portrait, () => w.pawn.LabelShortCap + "\n\n" + "CC_UI_OpenPage".Translate(), w.id ^ 0x2A11);
            if (Widgets.ButtonInvisible(portrait))
            {
                Cards.Click();
                Cards.Open(asker);
            }
            if (w.ForSomeoneElse && w.Beneficiary != null)
            {
                var small = new Rect(portrait.xMax - 20f, portrait.yMax - 20f, 28f, 28f);
                UiKit.Portrait(small, w.Beneficiary, UiKit.TrustColor(comp.standings.TrustOf(w.Beneficiary)));
                TooltipHandler.TipRegion(small, w.Beneficiary.LabelShortCap);
            }

            float textX = x + TextX;
            float textW = TextWidth(w, width);
            float cy = y + 10f;
            LinkText.Draw(new Rect(textX, cy, textW, l.headerH), l.headerShown ?? l.header, UiKit.Ink);
            cy += l.headerH;
            GUI.color = UiKit.Quote;
            Widgets.Label(new Rect(textX, cy, textW, l.quoteH), l.quote);
            GUI.color = UiKit.Ink;
            cy += l.quoteH + 4f;
            cy += UiKit.Pills(textX, cy, textW, l.pills) + 6f;
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(textX, cy, textW, l.detailsH), l.details);
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
            cy += l.detailsH + 6f;

            // Meters: progress (when the wish can measure it) and the time left.
            int total = w.expiresTick - w.createdTick;
            if (l.progress >= 0f && textW >= 300f)
            {
                float half = (textW - 12f) / 2f;
                UiKit.Meter(new Rect(textX, cy, half, UiKit.MeterHeight), l.progress, ProgressColor, "CC_Progress".Translate(), l.progressText ?? l.progress.ToStringPercent());
                UiKit.Countdown(new Rect(textX + half + 12f, cy, half, UiKit.MeterHeight), w.TicksLeft, total, "CC_Expires".Translate());
            }
            else if (l.progress >= 0f)
            {
                // Narrow: the time left as the meter, the progress in its tooltip.
                UiKit.Countdown(new Rect(textX, cy, textW, UiKit.MeterHeight), w.TicksLeft, total, "CC_Expires".Translate(),
                    "CC_Progress".Translate() + ": " + (l.progressText ?? l.progress.ToStringPercent()));
            }
            else UiKit.Countdown(new Rect(textX, cy, textW, UiKit.MeterHeight), w.TicksLeft, total, "CC_Expires".Translate());

            // What they want, as a thing.
            if (w.thingDef != null)
            {
                var tr = new Rect(textX + textW + 8f, y + 10f, ThingW - 8f, ThingW - 8f);
                Widgets.DrawBoxSolid(tr, UiKit.WithAlpha(UiKit.Shade, 0.22f));
                try
                {
                    if (Widgets.CanDrawIconFor(w.thingDef)) Widgets.DefIcon(tr.ContractedBy(3f), w.thingDef);
                }
                catch (System.Exception)
                {
                    // A modded thing without a usable graphic: the box stays empty.
                }
                var def = w.thingDef;
                TooltipHandler.TipRegion(tr, () => def.LabelCap + (def.description.NullOrEmpty() ? "" : "\n\n" + def.description), def.shortHash ^ 0x5157);
            }

            DrawButtons(comp, w, l, x + width - ButtonsW, y + 8f);
        }

        /// <summary>A card's acts, one under another: the design's button height and the gap between.</summary>
        private const float ButtonH = UiKit.RowStandard, ButtonStep = UiKit.RowStandard + 4f;

        private static void DrawButtons(CouncilGameComponent comp, Wish w, Layout l, float bx, float by)
        {
            const float bw = ButtonsW - 10f;
            if (UiKit.ActButton(new Rect(bx, by, bw, ButtonH), "CC_Show".Translate(), null)) MainTabWindow_Council.Jump(w.Worker.LookAt(w));
            by += ButtonStep;
            if (l.template != null)
            {
                if (UiKit.ActButton(new Rect(bx, by, bw, ButtonH), "CC_PlanIt".Translate(), null)) MainTabWindow_Council.StartPlanning(l.template, true);
                by += ButtonStep;
            }
            if (l.canBuild)
            {
                if (UiKit.ActButton(new Rect(bx, by, bw, ButtonH), "CC_BuildThing".Translate(w.thingDef.label), null))
                {
                    var des = BuildCopyCommandUtility.FindAllowedDesignator(w.thingDef);
                    if (des != null)
                    {
                        Find.MainTabsRoot.EscapeCurrentTab(false);
                        Find.DesignatorManager.Select(des);
                    }
                }
                by += ButtonStep;
            }
            if (!w.promised)
            {
                if (UiKit.ActButton(new Rect(bx, by, bw, ButtonH), "CC_Promise".Translate(), null, "CC_Promise_Tip".Translate())) comp.Promise(w);
                by += ButtonStep;
            }
            float half = (bw - 2f) / 2f;
            if (UiKit.ActButton(new Rect(bx, by, half, ButtonH), "CC_Refuse".Translate(), null, "CC_Refuse_Tip".Translate()))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Refuse_Confirm".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve(), () => comp.Refuse(w), true));
            if (UiKit.ActButton(new Rect(bx + half + 2f, by, half, ButtonH), "CC_NotNow".Translate(), null, "CC_NotNow_Tip".Translate())) comp.Dismiss(w);
        }

        private static RoomTemplateDef SuggestedTemplate(Wish w)
        {
            try
            {
                var t = w.Worker.SuggestedTemplate(w);
                return t != null && Find.CurrentMap != null && t.UnavailableReason(Find.CurrentMap) == null ? t : null;
            }
            catch
            {
                return null;
            }
        }

        private static string Safe(System.Func<string> f)
        {
            try
            {
                return f();
            }
            catch
            {
                return null;
            }
        }

        private static string SafeProgressText(Wish w) => Safe(() => w.Worker.ProgressText(w));

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

        // ---------------- What came of earlier wishes ----------------

        private static float HistoryRow(ChronicleEntry e, float y, float width, int i)
        {
            const float dateW = 64f;
            Text.Font = GameFont.Small;
            float h = Mathf.Max(24f, LinkText.Height(ChroniclePanel.TextOf(e, CouncilGameComponent.Instance), width - dateW - 28f) + 4f);
            var r = new Rect(0f, y, width, h);
            UiKit.Zebra(r, i);
            var color = UiKit.ChronicleColor(e.kind);
            UiKit.LabelFit(new Rect(4f, y + 2f, dateW - 6f, 20f), MainTabWindow_Council.AgoString(e.tick), UiKit.Faint, GameFont.Tiny);
            UiKit.Icon(new Rect(dateW, y + 4f, UiKit.IconDense, UiKit.IconDense), e.kind == ChronicleKind.WishGranted ? Icons.Promise : Icons.Wishes, color);
            // The kind's colour on its icon; the words in ink with the colonists they name as links.
            LinkText.Draw(new Rect(dateW + 22f, y + 2f, width - dateW - 28f, h - 4f), ChroniclePanel.TextOf(e, CouncilGameComponent.Instance), color == UiKit.Faint ? UiKit.Faint : UiKit.Ink);
            return h;
        }

        // ---------------- Trust ----------------

        private static void TrustRow(CouncilGameComponent comp, Pawn p, float y, float width, int i)
        {
            var st = comp.standings.For(p, false);
            float trust = st?.trust ?? 0f;
            var row = new Rect(0f, y, width, 28f);
            UiKit.Zebra(row, i);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            // The portrait and name open their page in the People roster (story, goal, bonds, journal).
            UiKit.PawnChip(4f, y + 2f, p, 24f, true, 170f, null, null, link: true);
            var color = UiKit.TrustColor(trust);
            UiKit.Bar(new Rect(184f, y + 10f, 150f, 8f), (trust + 100f) / 200f, color);
            Widgets.DrawBoxSolid(new Rect(184f + 75f, y + 8f, 1f, 12f), UiKit.WithAlpha(UiKit.Ink, 0.3f));
            UiKit.LabelFit(new Rect(342f, y + 2f, 100f, 24f), ("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate().CapitalizeFirst(), color);
            float x = 448f;
            if (Settings.risingStandards && width - x > 60f)
            {
                var level = Standards.LevelOf(p);
                var sRect = new Rect(x, y + 2f, Mathf.Min(170f, width - x), 24f);
                UiKit.LabelFit(sRect, "CC_Standards_Label".Translate(("CC_Standards_" + level).Translate()),
                    level == StandardsLevel.Spoiled ? HighColor : level == StandardsLevel.Particular ? MediumColor : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(sRect, "CC_Standards_Tip".Translate(("CC_Standards_" + level).Translate(), p.LabelShort));
                x += 176f;
            }
            if (st != null && st.leavingTick > 0 && width - x > 40f)
                UiKit.Pill(x, y + 5f, "CC_Trust_Leaving".Translate(TextUtil.DaysString(st.leavingTick - Find.TickManager.TicksGame)), HighColor, Icons.Warning, null, width - x);
            if (st != null)
            {
                var tipPawn = p;
                TooltipHandler.TipRegion(row, () => TrustTip(comp, tipPawn), p.thingIDNumber ^ 0x5A17);
            }
        }

        /// <summary>Built only when the tooltip shows, not every frame.</summary>
        private static string TrustTip(CouncilGameComponent comp, Pawn p)
        {
            var st = comp.standings.For(p, false);
            if (st == null) return "";
            float trust = st.trust;
            string tip = "CC_Trust_Tip".Translate(p.LabelShort, trust.ToString("0"), st.granted, st.ignored, st.refused, st.promisesKept, st.promisesBroken).Resolve();
            var office = OfficeTracker.HeldBy(p);
            if (office != null) tip = "CC_Office_Holds".Translate(p.LabelShort, office.label) + "\n" + tip;
            var goal = comp.aspirations.Active(p);
            if (goal != null && Settings.aspirations) tip = "CC_Asp_Tooltip".Translate(goal.Goal, goal.Progress.ToStringPercent()) + "\n" + tip;
            string quirkLine = comp.quirks.Describe(p);
            if (!quirkLine.NullOrEmpty()) tip = quirkLine + "\n" + tip;
            string bonds = comp.social.Describe(p);
            if (!bonds.NullOrEmpty()) tip = bonds + "\n\n" + tip;
            string titles = comp.epithets.TitlesOf(p);
            if (!titles.NullOrEmpty()) tip = "CC_Epithet_Known".Translate(p.LabelShort, titles) + "\n\n" + tip;
            if (st.log.Count > 0) tip += "\n\n" + string.Join("\n", st.log.AsEnumerable().Reverse());
            return tip;
        }
    }
}
