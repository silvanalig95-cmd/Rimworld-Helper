using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Defense → Defenses, "Defense lines": one card per line the player is building up. Its name, where it lies and
    /// what it's meant to be; the ladder of steps as a row of discs (done, the one to do now, later, set aside); the
    /// step to do now with why now, how it works and the common mistakes, and buttons to plan it at the line, show it,
    /// skip it or read more; the helper's notes on the line with their own buttons; its rough strength against
    /// today's raids; and what the last raid taught. A card folds to one row, remembered per line.
    /// </summary>
    public static class DefenseLinesPanel
    {
        private const float Pad = 10f, ButtonH = 24f, Disc = 18f;
        private static readonly Dictionary<(string, int, GameFont), float> heights = new Dictionary<(string, int, GameFont), float>();
        /// <summary>The step a card shows when the player clicked another disc than the current one, and whose "how it works" is open.</summary>
        private static readonly Dictionary<int, string> focus = new Dictionary<int, string>();
        private static readonly HashSet<int> howOpen = new HashSet<int>(), allNotes = new HashSet<int>();

        // A line's steps in the design's colours for steps (UI/UiKit.cs's tokens): done in done's colour, the one it is at
        // gold, what waits on something cool, the rest quiet.
        private static Color DoneColor => UiKit.Done;
        private static Color NowColor => UiKit.Gold;
        private static Color LaterColor => UiKit.Dim;
        private static Color WaitColor => UiKit.Cool;

        private static float Measure(string text, float w, GameFont font)
        {
            if (text.NullOrEmpty()) return 0f;
            var key = (text, (int)w, font);
            if (heights.TryGetValue(key, out float h)) return h;
            var old = Text.Font;
            Text.Font = font;
            h = Text.CalcHeight(text, w);
            Text.Font = old;
            if (heights.Count > 500) heights.Clear();
            heights[key] = h;
            return h;
        }

        /// <summary>Opens a line's card (unfolded) and draws it on the map.</summary>
        public static void Open(Map map, DefenseLine line)
        {
            if (line == null) return;
            line.folded = false;
            focus.Remove(line.id);
            DefenseLines.Select(map, line);
            DefensePanel.ScrollToTop();
        }

        // ---------------- The section ----------------

        /// <summary>
        /// The defense lines' section on the Defenses page: its heading (folding with <paramref name="folds"/>, with the
        /// count of lines), the buttons for a new line and how lines work, then a card for each line. Returns the y under it.
        /// </summary>
        public static float Draw(DefenseSurvey s, float x, float y, float w, Folds folds = null)
        {
            var map = s.map;
            var comp = DefenseLines.Of(map);
            if (comp == null) return y;
            var lines = comp.lines;
            y += UiKit.SectionHeading(x, y, w, "CC_DefLine_Section".Translate(), lines.Count, folds, "lines", out bool open,
                lines.Count == 0 ? "CC_DefLine_SectionNone".Translate().Resolve() : "CC_DefLine_SectionCount".Translate(lines.Count).Resolve());
            if (!open) return y + 4f;
            var newRect = new Rect(x + w - 200f, y, 200f, ButtonH);
            if (UiKit.ActButton(newRect, "CC_DefLine_New".Translate(), null)) NewMenu(s, comp);
            TooltipHandler.TipRegion(newRect, "CC_DefLine_New_Tip".Translate());
            var almanac = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail("Rimfolk_AlmDef_Lines");
            if (almanac != null)
            {
                var readRect = new Rect(x + w - 200f - 6f - 150f, y, 150f, ButtonH);
                if (UiKit.ActButton(readRect, "CC_DefLine_HowLines".Translate(), null)) MainTabWindow_Council.OpenAlmanac(almanac);
            }
            y += ButtonH + 6f;
            if (lines.Count == 0)
            {
                string intro = "CC_DefLine_Intro".Translate();
                float h = Measure(intro, w - 2 * Pad, GameFont.Tiny) + 2 * Pad;
                UiKit.Card(new Rect(x, y, w, h), UiKit.WithAlpha(NowColor, 0.4f), false, false);
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(x + Pad, y + Pad, w - 2 * Pad, h - 2 * Pad), intro);
                GUI.color = UiKit.Ink;
                Text.Font = GameFont.Small;
                return y + h + 12f;
            }
            foreach (var line in lines.ToList())
            {
                DefenseLineReview r;
                try
                {
                    r = DefenseLineReview.For(map, line);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("defense line card", e);
                    r = null;
                }
                if (r == null) continue;
                y = Card(r, comp, x, y, w) + 8f;
            }
            return y + 6f;
        }

        private static void NewMenu(DefenseSurvey s, DefenseLines comp)
        {
            var map = s.map;
            var opts = new List<FloatMenuOption>();
            // Gaps a line holds already (its own cells, the same pass a few cells off, the same ways in) aren't offered again.
            foreach (var c in s.chokes.OrderByDescending(c => c.c.share))
            {
                if (comp.ForChoke(s, c) != null) continue;
                var local = c;
                string label = "CC_DefLine_New_Gap".Translate(DefenseAdvice.Where(s, c.cell), s.GapWords(c), DefenseSurvey.Cells(c.c.width), Mathf.Max(1, Mathf.RoundToInt(c.c.share * 10f))).Resolve();
                opts.Add(new FloatMenuOption(label, () => Made(map, comp.FromChoke(s, local))));
            }
            if (s.chokes.Count == 0 && s.approach.IsValid && comp.Near(s.approach, 8f) == null)
                opts.Add(new FloatMenuOption("CC_DefLine_New_Open".Translate(DefenseAdvice.Where(s, s.approach)).Resolve(), () => Made(map, comp.AtCell(s.approach))));
            opts.Add(new FloatMenuOption("CC_DefLine_New_Pick".Translate(), Designator_PickLineSpot.Begin));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        /// <summary>Starts a line on a gap from its piece of advice ("Make this a defense line").</summary>
        public static void MakeFromAdvice(DefenseSurvey s, int choke)
        {
            var comp = DefenseLines.Of(s?.map);
            if (comp == null || choke < 0 || choke >= s.chokes.Count) return;
            // A gap a line holds already opens that line.
            var there = comp.ForChoke(s, s.chokes[choke]);
            if (there != null)
            {
                Open(s.map, there);
                return;
            }
            Made(s.map, comp.FromChoke(s, s.chokes[choke]));
        }

        private static void Made(Map map, DefenseLine line)
        {
            if (line == null) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            Messages.Message("CC_DefLine_Made".Translate(DefenseLineReview.NameOf(line, map)).Resolve(), new LookTargets(line.gate, map), MessageTypeDefOf.TaskCompletion, false);
            Open(map, line);
        }

        // ---------------- A line's card ----------------

        private static float Card(DefenseLineReview r, DefenseLines comp, float x, float y, float w)
        {
            float h = Body(r, comp, x, y, w, false);
            var rect = new Rect(x, y, w, h);
            var accent = r.verdict >= 1 ? DoneColor : r.verdict == 0 ? NowColor : UiKit.Bad;
            UiKit.Card(rect, UiKit.WithAlpha(accent, r.line.folded ? 0.35f : 0.6f), DefenseLines.Selected(comp.map) == r.line, false);
            Body(r, comp, x, y, w, true);
            return y + h;
        }

        private static StepResult Shown(DefenseLineReview r)
        {
            if (focus.TryGetValue(r.line.id, out var name))
            {
                var picked = r.steps.FirstOrDefault(s => s.def.defName == name);
                if (picked != null) return picked;
            }
            return r.CurrentStep;
        }

        private static string StateWord(StepState s)
        {
            switch (s)
            {
                case StepState.Done: return "CC_DefLine_State_Done".Translate();
                case StepState.Partly: return "CC_DefLine_State_Partly".Translate();
                case StepState.Later: return "CC_DefLine_State_Later".Translate();
                case StepState.NotYet: return "CC_DefLine_State_NotYet".Translate();
                case StepState.NotApplicable: return "CC_DefLine_State_NotApplicable".Translate();
                case StepState.Skipped: return "CC_DefLine_State_Skipped".Translate();
                default: return "CC_DefLine_State_Open".Translate();
            }
        }

        private static Color StateColor(StepState s, bool current)
        {
            if (current) return NowColor;
            switch (s)
            {
                case StepState.Done: return DoneColor;
                case StepState.NotYet: return WaitColor;
                case StepState.Partly: return UiKit.Soon;
                default: return LaterColor;
            }
        }

        private static string AimLabel(string aim)
        {
            switch (aim)
            {
                case "killbox": return "CC_DefLine_Aim_killbox".Translate();
                case "turrets": return "CC_DefLine_Aim_turrets".Translate();
                case "archers": return "CC_DefLine_Aim_archers".Translate();
                default: return "CC_DefLine_Aim_gate".Translate();
            }
        }

        private static string AimTip(string aim)
        {
            switch (aim)
            {
                case "killbox": return "CC_DefLine_AimTip_killbox".Translate();
                case "turrets": return "CC_DefLine_AimTip_turrets".Translate();
                case "archers": return "CC_DefLine_AimTip_archers".Translate();
                default: return "CC_DefLine_AimTip_gate".Translate();
            }
        }

        /// <summary>The card's contents; measured with draw off, drawn with it on. Returns the height.</summary>
        private static float Body(DefenseLineReview r, DefenseLines comp, float x0, float y0, float w0, bool draw)
        {
            var line = r.line;
            var map = comp.map;
            float x = x0 + Pad, w = w0 - 2 * Pad, y = y0 + 8f;
            string name = DefenseLineReview.NameOf(line, map);
            var current = r.CurrentStep;
            DefenseLineMath.Tally(r.steps.Select(s => s.state).ToList(), out int done, out int counted);

            // Header: fold, name, aim.
            const float aimW = 170f;
            if (draw)
            {
                var foldRect = new Rect(x - 2f, y + 2f, 20f, 20f);
                if (Widgets.ButtonImage(foldRect, line.folded ? TexButton.Reveal : TexButton.Collapse))
                {
                    line.folded = !line.folded;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                TooltipHandler.TipRegion(foldRect, (line.folded ? "CC_DefLine_Unfold" : "CC_DefLine_Fold").Translate());
                var nameRect = new Rect(x + 22f, y, w - 22f - aimW - 8f, 24f);
                UiKit.LabelFit(nameRect, name.CapitalizeFirst(), UiKit.Gold);
                if (Widgets.ButtonInvisible(nameRect))
                {
                    DefenseLines.Select(map, line);
                    DefenseOverlay.Show = true;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                TooltipHandler.TipRegion(nameRect, "CC_DefLine_Name_Tip".Translate());
                var aimRect = new Rect(x + w - aimW, y, aimW, ButtonH);
                if (UiKit.ActButton(aimRect, AimLabel(line.aim), null)) AimMenu(line);
                TooltipHandler.TipRegion(aimRect, "CC_DefLine_Aim_Tip".Translate() + "\n\n" + AimTip(line.aim));
            }
            y += 26f;

            if (line.folded)
            {
                // One row: the next step and the strength.
                string row = current != null
                    ? "CC_DefLine_FoldedRow".Translate(done, counted, current.Label, r.points.ToString("N0")).Resolve()
                    : "CC_DefLine_FoldedDone".Translate(done, counted, r.points.ToString("N0")).Resolve();
                if (draw)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(x + 22f, y, w - 22f, 18f), row.Truncate(w - 26f));
                    GUI.color = UiKit.Ink;
                    Text.Font = GameFont.Small;
                }
                return y + 20f - y0 + 4f;
            }

            // Where it lies.
            float wh = Measure(r.where, w, GameFont.Tiny);
            if (draw) Tiny(new Rect(x, y, w, wh), r.where, UiKit.Faint);
            y += wh + 4f;

            // The ladder.
            y = Ladder(r, x, y, w, draw, done, counted);

            // The step shown (the current one, or the one picked).
            var shown = Shown(r);
            if (shown != null) y = Step(r, shown, shown == current, x, y, w, draw);
            else
            {
                string all = "CC_DefLine_AllDone".Translate();
                float ah = Measure(all, w, GameFont.Tiny);
                if (draw) Tiny(new Rect(x, y, w, ah), all, DoneColor);
                y += ah + 6f;
            }

            // The helper's notes.
            y = Notes(r, x, y, w, draw);

            // Strength.
            y = Strength(r, x, y, w, draw);

            // The last raid.
            y = LastRaid(r, x, y, w, draw);

            // Remove.
            if (draw)
            {
                var removeRect = new Rect(x + w - 120f, y, 120f, 20f);
                Text.Font = GameFont.Tiny;
                if (UiKit.ActButton(removeRect, "CC_DefLine_Remove".Translate(), null))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_DefLine_Remove_Confirm".Translate(name).Resolve(), () => comp.Remove(line), true));
                var renameRect = new Rect(x + w - 120f - 6f - 120f, y, 120f, 20f);
                if (UiKit.ActButton(renameRect, "CC_DefLine_Rename".Translate(), null)) Rename(line, map);
                Text.Font = GameFont.Small;
            }
            y += 24f;
            return y - y0 + 4f;
        }

        /// <summary>A line of small text with a dot in front (a plus for something done well); returns its height.</summary>
        private static float Bullet(float x, float y, float w, string text, Color color, bool draw, bool good = false)
        {
            float h = Measure(text, w - 10f, GameFont.Tiny);
            if (!draw) return h;
            if (good)
            {
                Widgets.DrawBoxSolid(new Rect(x + 1f, y + 6f, 6f, 2f), color);
                Widgets.DrawBoxSolid(new Rect(x + 3f, y + 4f, 2f, 6f), color);
            }
            else Widgets.DrawBoxSolid(new Rect(x + 2f, y + 6f, 3f, 3f), UiKit.WithAlpha(color, 0.8f));
            Tiny(new Rect(x + 10f, y, w - 10f, h), text, color);
            return h;
        }

        private static void Tiny(Rect r, string text, Color color)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = color;
            Widgets.Label(r, text);
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
        }

        private static float Ladder(DefenseLineReview r, float x, float y, float w, bool draw, int done, int counted)
        {
            float cx = x;
            var shown = Shown(r);
            for (int i = 0; i < r.steps.Count; i++)
            {
                var s = r.steps[i];
                bool now = i == r.current;
                var rect = new Rect(cx, y + 1f, Disc, Disc);
                if (draw)
                {
                    var color = StateColor(s.state, now);
                    if (s.state == StepState.Skipped || s.state == StepState.NotApplicable)
                    {
                        UiKit.DiscFill(rect, UiKit.WithAlpha(LaterColor, 0.25f));
                        Widgets.DrawBoxSolid(new Rect(rect.x + 4f, rect.center.y - 1f, rect.width - 8f, 2f), LaterColor);
                    }
                    else if (s.state == StepState.Done)
                    {
                        UiKit.DiscFill(rect, UiKit.WithAlpha(color, 0.85f));
                        GUI.DrawTexture(rect.ContractedBy(3f), Widgets.CheckboxOnTex);
                    }
                    else if (now)
                    {
                        UiKit.DiscFill(rect.ExpandedBy(1f), color);
                        UiKit.DiscFill(rect.ContractedBy(5f), UiKit.PortraitBack);
                    }
                    else
                    {
                        UiKit.DiscFill(rect, UiKit.WithAlpha(color, 0.75f));
                        UiKit.DiscFill(rect.ContractedBy(3f), UiKit.PortraitBack);
                        if (s.state == StepState.Partly) UiKit.DiscFill(rect.ContractedBy(6f), UiKit.WithAlpha(color, 0.9f));
                    }
                    if (s == shown && !now) Widgets.DrawBox(rect.ExpandedBy(2f));
                    s.tip ??= "CC_DefLine_DiscTip".Translate(i + 1, s.Label, StateWord(s.state)).Resolve() + (s.left.NullOrEmpty() ? "" : "\n\n" + s.left);
                    TooltipHandler.TipRegion(rect, s.tip);
                    if (Widgets.ButtonInvisible(rect))
                    {
                        if (now) focus.Remove(r.line.id);
                        else focus[r.line.id] = s.def.defName;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                cx += Disc + 6f;
            }
            if (draw)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(cx + 4f, y + 2f, Mathf.Max(0f, x + w - cx - 4f), 18f), "CC_DefLine_StepsDone".Translate(done, counted).Resolve());
                GUI.color = UiKit.Ink;
                Text.Font = GameFont.Small;
            }
            return y + Disc + 8f;
        }

        private static float Step(DefenseLineReview r, StepResult s, bool now, float x, float y, float w, bool draw)
        {
            var map = r.f.map;
            var line = r.line;
            int index = r.steps.IndexOf(s) + 1;
            string title = (now ? "CC_DefLine_StepNow" : "CC_DefLine_StepOther").Translate(index, r.steps.Count, s.Label).Resolve();
            var color = StateColor(s.state, now);
            float th = Measure(title, w - 120f, GameFont.Small);
            if (draw)
            {
                // The state's colour on its tag beside the words; the step's title in ink.
                GUI.color = UiKit.Ink;
                Widgets.Label(new Rect(x, y, w - 120f, th), title);
                UiKit.Pill(x + w - 110f, y + 2f, StateWord(s.state), color, null, null, 110f);
            }
            y += th + 2f;
            if (s.state != StepState.Done && !s.def.whyNow.NullOrEmpty())
            {
                string why = "CC_DefLine_WhyNow".Translate(s.def.whyNow).Resolve();
                float h = Measure(why, w, GameFont.Tiny);
                if (draw) Tiny(new Rect(x, y, w, h), why, UiKit.Ink);
                y += h + 2f;
            }
            if (!s.left.NullOrEmpty())
            {
                string left = (s.state == StepState.NotYet ? "CC_DefLine_NotYet" : s.state == StepState.Later ? "CC_DefLine_LaterLine" : s.state == StepState.NotApplicable ? "CC_DefLine_NotApplicableLine" : "CC_DefLine_LeftLine").Translate(s.left).Resolve();
                float h = Measure(left, w, GameFont.Tiny);
                if (draw) Tiny(new Rect(x, y, w, h), left, s.state == StepState.NotYet ? WaitColor : UiKit.Soon);
                y += h + 2f;
            }
            foreach (var note in s.notes) y += Bullet(x + 4f, y, w - 4f, note, UiKit.Faint, draw) + 1f;
            // How it works, and the mistakes, folded away.
            bool open = howOpen.Contains(line.id);
            var howRect = new Rect(x, y, w, 20f);
            if (draw)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(howRect, (open ? "CC_DefLine_HowOpen" : "CC_DefLine_HowShut").Translate());
                GUI.color = UiKit.Ink;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(howRect))
                {
                    if (!howOpen.Remove(line.id)) howOpen.Add(line.id);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
            y += 18f;
            if (open)
            {
                float h = Measure(s.def.howItWorks, w - 8f, GameFont.Tiny);
                if (draw) Tiny(new Rect(x + 8f, y, w - 8f, h), s.def.howItWorks, UiKit.Ink);
                y += h + 3f;
                if (!s.def.mistakes.NullOrEmpty())
                {
                    string head = "CC_DefLine_Mistakes".Translate();
                    if (draw) Tiny(new Rect(x + 8f, y, w - 8f, 16f), head, UiKit.Faint);
                    y += 16f;
                    foreach (var m in s.def.mistakes) y += Bullet(x + 12f, y, w - 12f, m, UiKit.Faint, draw) + 1f;
                }
                y += 2f;
            }
            // Buttons.
            var buttons = new List<(string label, string tip, System.Action act, bool enabled)>();
            if (s.template != null && s.state != StepState.Done && s.state != StepState.Skipped)
            {
                string reason = s.template.UnavailableReason(map);
                var step = s;
                s.choices ??= s.def.Worker.Choices(r.f, s);
                if (s.choices.Count > 1)
                {
                    // The step's own pick first, then the step's other plans; open while any of them can be built.
                    bool any = s.choices.Any(t => t.UnavailableReason(map) == null);
                    buttons.Add(("CC_DefPlan_StepMenu".Translate(), any ? "CC_DefPlan_StepMenu_Tip".Translate(s.template.LabelNow).Resolve() : reason,
                        () => PlanMenu(r, step), any));
                }
                else
                    buttons.Add(("CC_DefLine_PlanStep".Translate(), reason ?? "CC_DefLine_PlanStep_Tip".Translate(s.template.LabelNow).Resolve(),
                        () => DefenseLineReview.PlanStep(map, line, step), reason == null));
            }
            if (s.state == StepState.Done)
            {
                s.choices ??= s.def.Worker.Choices(r.f, s);
                if (s.choices.Count > 0)
                    buttons.Add(((s.choices.Count > 1 ? "CC_DefLine_AgainMenu" : "CC_DefLine_Again").Translate(), "CC_DefLine_Again_Tip".Translate(), () => PlanMenu(r, s), true));
            }
            if (s.fix != null && s.state != StepState.Skipped) buttons.Add((s.fixLabel, s.fixTip, s.fix, true));
            var look = s.look.IsValid ? s.look : line.gate;
            buttons.Add(("CC_Show".Translate(), "CC_DefLine_Show_Tip".Translate(), () => DefenseLineReview.Show(map, line, look), true));
            if (s.state == StepState.Skipped)
                buttons.Add(("CC_DefLine_Unskip".Translate(), "CC_DefLine_Unskip_Tip".Translate(), () =>
                {
                    line.skipped.Remove(s.def.defName);
                    line.Changed();
                }, true));
            else if (s.state != StepState.Done)
                buttons.Add(("CC_DefLine_Skip".Translate(), "CC_DefLine_Skip_Tip".Translate(), () =>
                {
                    if (!line.skipped.Contains(s.def.defName)) line.skipped.Add(s.def.defName);
                    focus.Remove(line.id);
                    line.Changed();
                }, true));
            var page = s.def.Article;
            if (page != null) buttons.Add(("CC_ReadMore".Translate(), page.label.CapitalizeFirst(), () => MainTabWindow_Council.OpenAlmanac(page), true));
            y = ButtonRow(buttons, x, y, w, draw);
            return y + 4f;
        }

        /// <summary>
        /// A step's plans to place: the one it picked for the line first (for a finished step, the one it has, drawn anew),
        /// then the step's others. One that stands is marked built here, one begun says how far, and one the colony can't
        /// build yet says why.
        /// </summary>
        private static void PlanMenu(DefenseLineReview r, StepResult s)
        {
            if (s.choices.Count == 1)
            {
                Plan(r, s, s.choices[0]);
                return;
            }
            bool done = s.state == StepState.Done;
            var opts = new List<FloatMenuOption>();
            foreach (var t in s.choices)
            {
                string label = t.LabelCapNow;
                var p = r.f.BestPlan(t);
                if (p != null && p.Progress >= 0.85f) label = "CC_DefLine_Again_Built".Translate(label).Resolve();
                else if (!done && p != null && p.built > 0) label = "CC_DefPlan_Begun".Translate(label, p.built, p.total).Resolve();
                else if (!done && t == s.template) label = "CC_DefPlan_Picked".Translate(label).Resolve();
                string reason = t.UnavailableReason(r.f.map);
                var pick = t;
                opts.Add(reason != null
                    ? new FloatMenuOption(label + " (" + reason + ")", null)
                    : new FloatMenuOption(label, () => Plan(r, s, pick)));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        /// <summary>A step that isn't done places its own pick as "Plan this step" does; any other plan, or a finished step's, goes where its worker puts that plan.</summary>
        private static void Plan(DefenseLineReview r, StepResult s, DefenseTemplateDef t)
        {
            if (s.state != StepState.Done && t == s.template) DefenseLineReview.PlanStep(r.f.map, r.line, s);
            else DefenseLineReview.PlanAgain(r.f, s, t);
        }

        private static float ButtonRow(List<(string label, string tip, System.Action act, bool enabled)> buttons, float x, float y, float w, bool draw, float height = ButtonH, GameFont font = GameFont.Small)
        {
            float bx = x;
            var old = Text.Font;
            Text.Font = font;
            string firstWhy = null, firstWhyAct = null;
            foreach (var (label, tip, act, enabled) in buttons)
            {
                float bw = Mathf.Max(70f, Text.CalcSize(label).x + 20f);
                if (bx > x && bx + bw > x + w)
                {
                    bx = x;
                    y += height + 4f;
                }
                if (!enabled && firstWhy == null && !tip.NullOrEmpty())
                {
                    firstWhy = tip;
                    firstWhyAct = label;
                }
                if (draw)
                {
                    // An act that can't be done now is greyed with why not as its tooltip (and in a line under the row),
                    // rather than refused after the click.
                    var rect = new Rect(bx, y, bw, height);
                    string why = enabled ? null : tip ?? "";
                    bool used = font == GameFont.Tiny ? UiKit.RowAct(rect, label, why, tip) : UiKit.ActButton(rect, label, why, tip);
                    Text.Font = font;
                    if (used) act?.Invoke();
                }
                bx += bw + 4f;
            }
            Text.Font = old;
            if (buttons.Count == 0) return y;
            y += height + 4f;
            if (firstWhy != null)
            {
                if (draw) UiKit.WhyNotLine(x, y, w, firstWhyAct, firstWhy);
                y += 18f;
            }
            return y;
        }

        private const int NotesShown = 4;

        /// <summary>A note's key in the shared suggestion core: "line:" the line's id and the note's key, set aside with "Not this".</summary>
        public static string NoteKey(DefenseLineReview r, LineNote n) => "line:" + r.line.id + ":" + n.key;

        private static float Notes(DefenseLineReview r, float x, float y, float w, bool draw)
        {
            var tracker = SuggestTracker.Instance;
            // What the player set aside with "Not this" stays out until brought back (things done well are never set aside).
            var notes = r.notes.Where(n => n.good || tracker == null || !tracker.Declined(NoteKey(r, n))).ToList();
            int setAside = r.notes.Count - notes.Count;
            if (notes.Count == 0 && setAside == 0) return y;
            if (draw)
            {
                UiKit.Divider(x, y + 2f, w);
                Tiny(new Rect(x, y + 4f, w, 16f), "CC_DefLine_Notes".Translate(), UiKit.Heading);
            }
            y += 22f;
            bool all = allNotes.Contains(r.line.id);
            var list = all ? notes : notes.Take(NotesShown).ToList();
            foreach (var n in list)
            {
                float h = Bullet(x + 4f, y, w - 4f, n.text, n.good ? DoneColor : UiKit.Ink, draw, n.good);
                if (draw)
                {
                    var rect = new Rect(x + 4f, y, w - 4f, h);
                    string tip = n.how ?? "";
                    if (n.article != null) tip += (tip.Length > 0 ? "\n\n" : "") + "CC_DefLine_NoteRead".Translate(n.article.label).Resolve();
                    if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(rect, tip);
                }
                y += h + 2f;
                var buttons = n.actions.Select(a => (a.label, a.tip, a.act, true)).ToList();
                if (n.article != null && !n.good)
                {
                    var page = n.article;
                    buttons.Add(("CC_ReadMore".Translate().ToString(), "CC_Same_ReadMoreTip".Translate(page.label.CapitalizeFirst()).Resolve(), (System.Action)(() => MainTabWindow_Council.OpenAlmanac(page)), true));
                }
                if (!n.good)
                {
                    string key = NoteKey(r, n), text = n.text;
                    buttons.Add(("CC_Ahead_NotThis".Translate().ToString(), "CC_Same_Line_NotThisTip".Translate().Resolve(), (System.Action)(() =>
                    {
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                        tracker?.Decline(key, text);
                    }), true));
                }
                if (buttons.Count > 0 && !n.good) y = ButtonRow(buttons, x + 12f, y, w - 12f, draw, 20f, GameFont.Tiny);
                y += 2f;
            }
            if (notes.Count > NotesShown)
            {
                var more = new Rect(x + 4f, y, w - 8f, 18f);
                if (draw)
                {
                    Tiny(more, (all ? "CC_DefLine_NotesFewer" : "CC_DefLine_NotesMore").Translate(notes.Count - NotesShown).Resolve(), UiKit.Info);
                    if (Widgets.ButtonInvisible(more))
                    {
                        if (!allNotes.Remove(r.line.id)) allNotes.Add(r.line.id);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                y += 20f;
            }
            // The notes set aside, brought back at a click.
            if (setAside > 0)
            {
                var back = new Rect(x + 4f, y, w - 8f, 18f);
                if (draw)
                {
                    Tiny(back, "CC_Same_Line_Aside".Translate(setAside).Resolve(), UiKit.Info);
                    TooltipHandler.TipRegion(back, "CC_Same_Line_AsideTip".Translate());
                    if (Widgets.ButtonInvisible(back) && tracker != null)
                    {
                        foreach (var (key, _, _) in tracker.DeclinedFor("line:" + r.line.id)) tracker.BringBack(key);
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                }
                y += 20f;
            }
            return y + 2f;
        }

        private static float Strength(DefenseLineReview r, float x, float y, float w, bool draw)
        {
            if (r.strength == null) return y;
            if (draw) UiKit.Divider(x, y + 2f, w);
            y += 6f;
            string text = r.StrengthText();
            float h = Measure(text, w, GameFont.Tiny);
            var color = r.verdict >= 1 ? DoneColor : r.verdict == 0 ? NowColor : UiKit.Bad;
            if (draw)
            {
                var bar = new Rect(x, y, w, 6f);
                UiKit.Bar(bar, r.f.raidPoints > 0f ? Mathf.Clamp01(r.strength.total / (r.f.raidPoints * 2f)) : 1f, color);
                // The raids now, as a tick at the middle of the bar (the bar runs to twice their size).
                Widgets.DrawBoxSolid(new Rect(bar.center.x - 1f, bar.y - 2f, 2f, bar.height + 4f), UiKit.Ink);
                var tr = new Rect(x, y + 8f, w, h);
                Tiny(tr, text, color);
                TooltipHandler.TipRegion(new Rect(x, y, w, h + 8f), r.StrengthTip());
            }
            return y + 8f + h + 4f;
        }

        private static float LastRaid(DefenseLineReview r, float x, float y, float w, bool draw)
        {
            var raid = r.line.LastRaid;
            if (raid == null) return y;
            var map = r.f.map;
            if (draw) UiKit.Divider(x, y + 2f, w);
            y += 6f;
            string when = GenDate.ToStringTicksToPeriod(Mathf.Max(0, Find.TickManager.TicksGame - raid.tick), true, true, true);
            string head = "CC_DefLine_LastRaid".Translate(when, DefenseLines.Summary(r.line, raid, map)).Resolve();
            float h = Measure(head, w, GameFont.Tiny);
            if (draw) Tiny(new Rect(x, y, w, h), head, UiKit.Quote);
            y += h + 3f;
            foreach (var lesson in raid.lessons)
            {
                string text = DefenseLines.LessonText(r.line, raid, lesson);
                if (text.NullOrEmpty()) continue;
                y += Bullet(x + 4f, y, w - 4f, text, UiKit.Ink, draw) + 2f;
                var act = LessonAction(r, lesson);
                if (act != null) y = ButtonRow(new List<(string, string, System.Action, bool)> { (act.label, act.tip, act.act, true) }, x + 12f, y, w - 12f, draw, 20f, GameFont.Tiny);
            }
            return y + 4f;
        }

        /// <summary>What to do about a lesson: the review's check it points at, or the step.</summary>
        private static LineAction LessonAction(DefenseLineReview r, string lesson)
        {
            var map = r.f.map;
            StepResult Step<T>() where T : DefenseStepWorker => r.steps.FirstOrDefault(s => s.def.Worker is T);
            LineAction Go(StepResult s) => s == null ? null : new LineAction
            {
                label = "CC_DefLine_Lesson_GoStep".Translate(s.Label).Resolve(),
                tip = "CC_DefLine_Lesson_GoStep_Tip".Translate(),
                act = () =>
                {
                    focus[r.line.id] = s.def.defName;
                    if (s.state == StepState.Skipped)
                    {
                        r.line.skipped.Remove(s.def.defName);
                        r.line.Changed();
                    }
                }
            };
            switch (lesson)
            {
                case nameof(DefenseLineMath.Lesson.Leak):
                {
                    var note = r.notes.FirstOrDefault(n => n.key == "leak" && !n.good);
                    return note?.actions.FirstOrDefault() ?? Go(Step<StepWorker_CloseGap>());
                }
                case nameof(DefenseLineMath.Lesson.Harden): return Go(Step<StepWorker_Harden>());
                case nameof(DefenseLineMath.Lesson.CoverLeft):
                case nameof(DefenseLineMath.Lesson.CoverRight): return Go(Step<StepWorker_Cover>());
                case nameof(DefenseLineMath.Lesson.Fallback): return Go(Step<StepWorker_Fallback>());
                default: return null;
            }
        }

        // ---------------- Menus and names ----------------

        private static void AimMenu(DefenseLine line)
        {
            var opts = new List<FloatMenuOption>();
            foreach (var aim in DefenseLine.Aims)
            {
                var local = aim;
                bool turretsLow = aim == "turrets" && Avail.IsPreIndustrial;
                var opt = new FloatMenuOption(AimLabel(aim) + (turretsLow ? " (" + "CC_DefLine_Aim_NeedsTurrets".Translate() + ")" : ""), turretsLow ? (System.Action)null : () =>
                {
                    line.aim = local;
                    focus.Remove(line.id);
                    line.Changed();
                });
                opt.tooltip = AimTip(aim);
                opts.Add(opt);
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void Rename(DefenseLine line, Map map)
        {
            Find.WindowStack.Add(new Dialog_NameLine(DefenseLineReview.NameOf(line, map), n =>
            {
                line.name = n;
                line.Changed();
            }));
        }
    }

    /// <summary>Asks for a defense line's name; an empty name gives it back the helper's.</summary>
    public class Dialog_NameLine : Window
    {
        private string name;
        private readonly System.Action<string> done;
        private bool focused, finished;
        public const int MaxLength = 40;

        public Dialog_NameLine(string suggested, System.Action<string> done)
        {
            name = suggested ?? "";
            this.done = done;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(380f, 170f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 28f), "CC_DefLine_NamePrompt".Translate());
            GUI.SetNextControlName("RimfolkLineName");
            name = Widgets.TextField(new Rect(0f, 32f, inRect.width, 32f), name, MaxLength);
            if (!focused)
            {
                UI.FocusControl("RimfolkLineName", this);
                focused = true;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(0f, 68f, inRect.width, 20f), "CC_DefLine_NameEmpty".Translate());
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
            if (UiKit.ActButton(new Rect(inRect.width / 2f - 70f, inRect.height - 38f, 140f, 34f), "OK".Translate(), null) || KeyBindingDefOf.Accept.KeyDownEvent)
                Accept();
        }

        public override void OnAcceptKeyPressed() => Accept();

        private void Accept()
        {
            if (finished) return;
            finished = true;
            string n = (name ?? "").Trim();
            done?.Invoke(n.NullOrEmpty() ? null : n);
            Close();
        }
    }
}
