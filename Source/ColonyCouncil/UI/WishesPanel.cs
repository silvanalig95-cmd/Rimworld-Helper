using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>People → Wishes: what the colonists ask the council for, what came of earlier wishes, and how much each one trusts the council.</summary>
    [StaticConstructorOnStartup]
    public static class WishesPanel
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color QuoteColor = new Color(1f, 0.93f, 0.75f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color MediumColor = new Color(0.9f, 0.85f, 0.35f);
        private static int lastListenTick = -999999;
        private static readonly Texture2D TrustGood = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.65f, 0.4f));
        private static readonly Texture2D TrustBad = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.35f, 0.3f));
        private static Vector2 scroll;
        private static float lastHeight = 1000f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static float BeginScroll(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 18f, lastHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            return view.width;
        }

        private static void EndScroll(float height)
        {
            lastHeight = Mathf.Max(height + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small) =>
            MainTabWindow_Council.WrappedLabel(x, y, width, text, color, font);

        public static void Draw(Rect rect, CouncilGameComponent comp)
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
                y += WrappedLabel(0f, y, width, MainTabWindow_Council.AgoString(e.tick) + "  " + e.text, col) + 2f;
                shown++;
            }

            y += 14f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "CC_Trust_Header".Translate());
            Text.Font = GameFont.Small;
            y += 30f;
            y += WrappedLabel(0f, y, width, "CC_Trust_Sub".Translate(), Faint) + 4f;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(x => !x.IsQuestLodger()).OrderBy(x => comp.standings.TrustOf(x)))
            {
                var st = comp.standings.For(p, false);
                float trust = st?.trust ?? 0f;
                var row = new Rect(0f, y, width, 26f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Widgets.ThingIcon(new Rect(0f, y + 1f, 24f, 24f), p);
                var nameRect = new Rect(30f, y + 2f, 160f, 24f);
                Widgets.Label(nameRect, p.LabelShort);
                // The name opens their page in the People roster (story, goal, bonds, journal).
                Widgets.DrawHighlightIfMouseover(nameRect);
                if (Widgets.ButtonInvisible(nameRect)) MainTabWindow_Council.OpenPerson(p);
                var bar = new Rect(195f, y + 7f, 160f, 12f);
                Widgets.FillableBar(bar, (trust + 100f) / 200f, trust >= 0f ? TrustGood : TrustBad, BaseContent.BlackTex, false);
                Widgets.Label(new Rect(365f, y + 2f, 100f, 24f), ("CC_Trust_" + PawnStanding.LevelFor(trust)).Translate());
                if (Settings.risingStandards)
                {
                    var level = Standards.LevelOf(p);
                    var sRect = new Rect(470f, y + 2f, 160f, 24f);
                    GUI.color = level == StandardsLevel.Spoiled ? HighColor : level == StandardsLevel.Particular ? MediumColor : Faint;
                    Widgets.Label(sRect, "CC_Standards_Label".Translate(("CC_Standards_" + level).Translate()));
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(sRect, "CC_Standards_Tip".Translate(("CC_Standards_" + level).Translate(), p.LabelShort));
                }
                if (st != null && st.leavingTick > 0)
                {
                    GUI.color = HighColor;
                    Widgets.Label(new Rect(635f, y + 2f, Mathf.Max(0f, width - 635f), 24f), "CC_Trust_Leaving".Translate(TextUtil.DaysString(st.leavingTick - Find.TickManager.TicksGame)));
                    GUI.color = Color.white;
                }
                if (st != null)
                {
                    var tipPawn = p;
                    TooltipHandler.TipRegion(row, () => TrustTip(comp, tipPawn), p.thingIDNumber ^ 0x5A17);
                }
                y += 27f;
            }
            EndScroll(y);
        }

        /// <summary>Built only when the tooltip shows, not every frame.</summary>
        private static string TrustTip(CouncilGameComponent comp, Pawn p)
        {
            var st = comp.standings.For(p, false);
            if (st == null) return "";
            float trust = st.trust;
            string tip = "CC_Trust_Tip".Translate(p.LabelShort, trust.ToString("0"), st.granted, st.ignored, st.refused, st.promisesKept, st.promisesBroken).Resolve();
            var office = comp.offices.OfficeOf(p);
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

        private static float DrawWish(CouncilGameComponent comp, Wish w, float x, float y, float width)
        {
            float textX = x + 84f;
            float textW = width - 84f - 150f;
            string header = w.ForSomeoneElse
                ? "CC_WishHeaderForOther".Translate(w.pawn.LabelShort, w.Beneficiary.LabelShort, w.Label).Resolve()
                : "CC_WishHeader".Translate(w.pawn.LabelShort, w.Label).Resolve();
            string quote = (w.scene.NullOrEmpty() ? "" : w.scene + "\n") + "\"" + w.quote + "\"";
            var lines = new List<string>();
            var standing = comp.standings.For(w.pawn, false);
            string status = "CC_TrustLabel".Translate(("CC_Trust_" + (standing?.Level ?? TrustLevel.Neutral)).Translate());
            if (w.promised) status += "   ·   " + "CC_PromisedTag".Translate();
            if (w.stage >= 2) status += "   ·   " + "CC_LastCallTag".Translate();
            if (w.envied != null) status += "   ·   " + "CC_EnvyTag".Translate(w.envied.LabelShort);
            if (w.objector != null) status += "   ·   " + "CC_ObjectorTag".Translate(w.objector.LabelShort);
            lines.Add(status);
            if (!w.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + w.reason);
            string when = UiCache.Get(w, 1, () => w.Worker.FulfilledWhen(w));
            if (!when.NullOrEmpty()) lines.Add("CC_FulfilledWhen".Translate() + ": " + when);
            string prog = UiCache.Get(w, 2, () => SafeProgressText(w));
            if (!prog.NullOrEmpty()) lines.Add("CC_Progress".Translate() + ": " + prog);
            if (w.coSigners.Count > 0) lines.Add("CC_Petition".Translate(TextUtil.JoinAnd(w.coSigners.Select(p => p.LabelShort).ToList())));
            lines.Add("CC_Reward".Translate() + ": " + comp.RewardText(w) + "   ·   " + "CC_Expires".Translate() + ": " + TextUtil.DaysString(w.TicksLeft));
            string body = string.Join("\n", lines);

            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(header, textW) + Text.CalcHeight(quote, textW) + Text.CalcHeight(body, textW) + 18f;
            float progress = UiCache.Get(w, 3, () => SafeProgress(w));
            if (progress >= 0f) h += 14f;
            h = Mathf.Max(h, 96f + 30f * 3f);
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
            if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_Show".Translate())) MainTabWindow_Council.Jump(w.Worker.LookAt(w));
            by += 30f;
            var template = UiCache.Get(w, 4, () =>
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
            });
            if (template != null)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_PlanIt".Translate())) MainTabWindow_Council.StartPlanning(template, true);
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
            if (!w.promised)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 138f, 26f), "CC_Promise".Translate())) comp.Promise(w);
                TooltipHandler.TipRegion(new Rect(bx, by, 138f, 26f), "CC_Promise_Tip".Translate());
                by += 30f;
            }
            if (Widgets.ButtonText(new Rect(bx, by, 68f, 26f), "CC_Refuse".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Refuse_Confirm".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve(), () => comp.Refuse(w), true));
            }
            TooltipHandler.TipRegion(new Rect(bx, by, 68f, 26f), "CC_Refuse_Tip".Translate());
            if (Widgets.ButtonText(new Rect(bx + 70f, by, 68f, 26f), "CC_NotNow".Translate())) comp.Dismiss(w);
            TooltipHandler.TipRegion(new Rect(bx + 70f, by, 68f, 26f), "CC_NotNow_Tip".Translate());
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
    }
}
