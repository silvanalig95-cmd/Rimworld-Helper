using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Drawing for the Mood tab and the council's Moods page: a mood bar with the break lines, and mood lines with their why and what to do.</summary>
    public static class MoodView
    {
        public static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color GoodColor = new Color(0.45f, 0.85f, 0.45f);
        public static readonly Color BadColor = new Color(0.95f, 0.45f, 0.35f);
        public static readonly Color AdviceColor = new Color(1f, 0.88f, 0.6f);
        private static readonly Color MinorColor = new Color(0.95f, 0.85f, 0.3f);
        private static readonly Color MajorColor = new Color(0.95f, 0.55f, 0.2f);
        private static readonly Color ExtremeColor = new Color(0.9f, 0.2f, 0.2f);

        public static Color ColorFor(float mood) => mood >= 0f ? GoodColor : BadColor;

        public static string Number(float mood) => mood >= 0f ? "+" + mood.ToString("0.#") : mood.ToString("0.#");

        /// <summary>The mood bar: where they are, where they're heading, and the lines where breaks begin.</summary>
        public static void DrawBar(Rect r, Pawn p)
        {
            var (now, heading, minor, major, extreme) = MoodInsight.Levels(p);
            Widgets.DrawBoxSolid(r, new Color(0.12f, 0.12f, 0.12f, 0.9f));
            var fill = new Rect(r.x, r.y, r.width * now, r.height);
            Widgets.DrawBoxSolid(fill, now < minor ? (now < major ? ExtremeColor : MajorColor) : new Color(0.35f, 0.6f, 0.4f));
            void Mark(float at, Color c, string tip)
            {
                var line = new Rect(r.x + r.width * at - 1f, r.y - 2f, 2f, r.height + 4f);
                Widgets.DrawBoxSolid(line, c);
                TooltipHandler.TipRegion(new Rect(line.x - 3f, line.y, 8f, line.height), tip);
            }
            Mark(extreme, ExtremeColor, "CC_Mood_LineExtreme".Translate(extreme.ToStringPercent()));
            Mark(major, MajorColor, "CC_Mood_LineMajor".Translate(major.ToStringPercent()));
            Mark(minor, MinorColor, "CC_Mood_LineMinor".Translate(minor.ToStringPercent()));
            // Where the mood is heading: a small triangle above the bar.
            if (Mathf.Abs(heading - now) > 0.01f)
            {
                float hx = r.x + r.width * heading;
                GUI.color = heading > now ? GoodColor : BadColor;
                Widgets.DrawBoxSolid(new Rect(hx - 1f, r.y, 2f, r.height), GUI.color);
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(r, now.ToStringPercent());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>"Mood 42%, heading down to 35%. Just above the line where minor breaks begin (35%)."</summary>
        public static string State(Pawn p)
        {
            var (now, heading, minor, major, extreme) = MoodInsight.Levels(p);
            string dir = Mathf.Abs(heading - now) < 0.01f ? "CC_Mood_Steady".Translate().ToString()
                : (heading > now ? "CC_Mood_HeadingUp" : "CC_Mood_HeadingDown").Translate(heading.ToStringPercent()).ToString();
            string state = "CC_Mood_State".Translate(now.ToStringPercent(), dir).Resolve();
            string risk;
            if (p.InMentalState) risk = "CC_Mood_InBreak".Translate(p.MentalStateDef?.label ?? "?").Resolve();
            else if (now < extreme) risk = "CC_Mood_BelowExtreme".Translate(extreme.ToStringPercent()).Resolve();
            else if (now < major) risk = "CC_Mood_BelowMajor".Translate(major.ToStringPercent()).Resolve();
            else if (now < minor) risk = "CC_Mood_BelowMinor".Translate(minor.ToStringPercent()).Resolve();
            else risk = "CC_Mood_Above".Translate((now - minor).ToStringPercent(), minor.ToStringPercent()).Resolve();
            return state + " " + risk;
        }

        /// <summary>How much of the mood comes from the base game, Rimfolk and each other mod.</summary>
        public static string Sources(IEnumerable<MoodLine> lines, int people)
        {
            var parts = lines.GroupBy(l => MoodInsight.SourceOf(l.def).name)
                .Select(g => (name: g.Key, sum: g.Sum(l => l.mood) / Mathf.Max(1, people)))
                .Where(x => Mathf.Abs(x.sum) >= 0.5f)
                .OrderBy(x => x.sum)
                .Select(x => x.name + " " + Number(x.sum))
                .ToList();
            return parts.Count == 0 ? null : "CC_Mood_Sources".Translate(string.Join(",  ", parts)).Resolve();
        }

        /// <summary>One mood line: size, name, why, when it ends, and what to do. Returns its height.</summary>
        public static float DrawLine(float x, float y, float w, MoodLine l, string who = null)
        {
            float start = y;
            var (kind, source) = MoodInsight.SourceOf(l.def);
            string title = l.label + (l.count > 1 ? " ×" + l.count : "") + (who != null ? "  ·  " + who : "");
            string meta = MoodInsight.WhenItEnds(l) + (kind != MoodSource.Game ? "  ·  " + "CC_Mood_From".Translate(source).Resolve() : "");
            string why = l.why.NullOrEmpty() ? null : "CC_Cause_Line".Translate(l.why).Resolve();
            string advice = MoodInsight.Advice(l);
            if (!advice.NullOrEmpty()) advice = (l.mood < 0f ? "CC_Mood_Fix" : "CC_Mood_Keep").Translate(advice).Resolve();
            const float numW = 44f;
            float textW = w - numW - 6f;

            // Measure, then draw.
            Text.Font = GameFont.Small;
            float th = Text.CalcHeight(title, textW);
            Text.Font = GameFont.Tiny;
            float mh = Text.CalcHeight(meta, textW);
            float wh = why == null ? 0f : Text.CalcHeight(why, textW);
            float ah = advice == null ? 0f : Text.CalcHeight(advice, textW);
            var hint = MoodInsight.HintFor(l.def);
            bool buttons = hint != null && l.mood < 0f && (hint.article != null || !hint.councilTab.NullOrEmpty() || ActiveSignal(hint) != null);
            float h = th + mh + wh + ah + (buttons ? 24f : 0f) + 8f;
            var row = new Rect(x, y, w, h);
            Widgets.DrawLightHighlight(row);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            TooltipHandler.TipRegion(row, () => MoodInsight.Description(l), l.GetHashCode());

            Text.Font = GameFont.Medium;
            GUI.color = ColorFor(l.mood);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x, y + 2f, numW, Mathf.Min(h - 4f, 34f)), Number(l.mood));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float tx = x + numW + 4f;
            y += 4f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(tx, y, textW, th), title);
            y += th;
            Text.Font = GameFont.Tiny;
            GUI.color = Faint;
            Widgets.Label(new Rect(tx, y, textW, mh), meta);
            y += mh;
            if (why != null)
            {
                Widgets.Label(new Rect(tx, y, textW, wh), why);
                y += wh;
            }
            GUI.color = l.mood < 0f ? AdviceColor : Faint;
            if (advice != null)
            {
                Widgets.Label(new Rect(tx, y, textW, ah), advice);
                y += ah;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (buttons) DrawButtons(hint, tx, y + 1f, textW);
            return h;
        }

        /// <summary>A colony-wide line: one kind of mood, everyone who has it, and what to do.</summary>
        public static float DrawColonyLine(float x, float y, float w, ColonyMoodLine c)
        {
            var sample = c.total < 0f ? c.lines.OrderBy(l => l.mood).First() : c.lines.OrderByDescending(l => l.mood).First();
            var (kind, source) = MoodInsight.SourceOf(c.def);
            string title = c.label + "  ·  " + "CC_Moods_People".Translate(c.lines.Count).Resolve();
            string who = string.Join(", ", c.lines.OrderBy(l => c.total < 0f ? l.mood : -l.mood).Take(10).Select(l => l.pawn.LabelShort + " " + Number(l.mood)))
                         + (c.lines.Count > 10 ? ", …" : "");
            string meta = (kind != MoodSource.Game ? "CC_Mood_From".Translate(source).Resolve() + "  ·  " : "") + who;
            string why = sample.why.NullOrEmpty() || c.lines.Count > 1 ? null : "CC_Cause_Line".Translate(sample.why).Resolve();
            string advice = MoodInsight.Advice(sample);
            if (!advice.NullOrEmpty()) advice = (c.total < 0f ? "CC_Mood_Fix" : "CC_Mood_Keep").Translate(advice).Resolve();
            const float numW = 56f;
            float textW = w - numW - 6f;
            Text.Font = GameFont.Small;
            float th = Text.CalcHeight(title, textW);
            Text.Font = GameFont.Tiny;
            float mh = Text.CalcHeight(meta, textW);
            float wh = why == null ? 0f : Text.CalcHeight(why, textW);
            float ah = advice == null ? 0f : Text.CalcHeight(advice, textW);
            var hint = MoodInsight.HintFor(c.def);
            bool buttons = hint != null && c.total < 0f && (hint.article != null || !hint.councilTab.NullOrEmpty() || ActiveSignal(hint) != null);
            float h = th + mh + wh + ah + (buttons ? 24f : 0f) + 8f;
            var row = new Rect(x, y, w, h);
            Widgets.DrawLightHighlight(row);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            TooltipHandler.TipRegion(row, () => MoodInsight.Description(sample), c.GetHashCode());

            Text.Font = GameFont.Medium;
            GUI.color = ColorFor(c.total);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x, y + 2f, numW, Mathf.Min(h - 4f, 34f)), Number(c.total));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            float tx = x + numW + 4f;
            float cy = y + 4f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(tx, cy, textW, th), title);
            cy += th;
            Text.Font = GameFont.Tiny;
            GUI.color = Faint;
            Widgets.Label(new Rect(tx, cy, textW, mh), meta);
            cy += mh;
            if (why != null)
            {
                Widgets.Label(new Rect(tx, cy, textW, wh), why);
                cy += wh;
            }
            GUI.color = c.total < 0f ? AdviceColor : Faint;
            if (advice != null)
            {
                Widgets.Label(new Rect(tx, cy, textW, ah), advice);
                cy += ah;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (buttons) DrawButtons(hint, tx, cy + 1f, textW);
            return h;
        }

        private static AdvisorSignal ActiveSignal(MoodHintDef hint)
        {
            if (hint?.signal == null || Find.CurrentMap == null) return null;
            return CouncilMapComponent.For(Find.CurrentMap)?.Signals?.FirstOrDefault(s => s.def == hint.signal);
        }

        private static void DrawButtons(MoodHintDef hint, float x, float y, float w)
        {
            Text.Font = GameFont.Tiny;
            float bx = x;
            bool Button(string label)
            {
                float bw = Text.CalcSize(label).x + 16f;
                if (bx + bw > x + w) return false;
                bool clicked = Widgets.ButtonText(new Rect(bx, y, bw, 20f), label);
                bx += bw + 6f;
                return clicked;
            }
            var signal = ActiveSignal(hint);
            if (signal != null && Button("CC_Mood_Advisor".Translate(TextUtil.ShortSpeech(signal.title, 40)).Resolve()))
                MainTabWindow_Council.OpenTab("Priorities");
            if (!hint.councilTab.NullOrEmpty() && Button("CC_Mood_GoTo".Translate(MainTabWindow_Council.TabName(hint.councilTab)).Resolve()))
                MainTabWindow_Council.OpenTab(hint.councilTab);
            if (hint.article != null && Button("CC_Mood_ReadMore".Translate(hint.article.label).Resolve()))
                MainTabWindow_Council.OpenAlmanac(hint.article);
            Text.Font = GameFont.Small;
        }

        /// <summary>Select a colonist and open their Mood tab.</summary>
        public static void ShowMoodTab(Pawn p)
        {
            if (p == null) return;
            try
            {
                if (Find.MainTabsRoot.OpenTab != null && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect) Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
                InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Mood));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("open mood tab", e);
            }
        }
    }
}
