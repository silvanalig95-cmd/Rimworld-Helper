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
        private static readonly Color OkColor = new Color(0.35f, 0.6f, 0.4f);
        private static readonly Color MinorColor = new Color(0.95f, 0.85f, 0.3f);
        private static readonly Color MajorColor = new Color(0.95f, 0.55f, 0.2f);
        private static readonly Color ExtremeColor = new Color(0.9f, 0.2f, 0.2f);
        private const float NumW = 52f;

        /// <summary>A line's text and layout, worked out once per refresh rather than every frame.</summary>
        public class Texts
        {
            public string title, meta, why, advice;
            public bool bad;
            public MoodHintDef hint;
            public AdvisorSignal signal;
            public bool buttons;
            /// <summary>The buttons' labels, worked out with the texts (not every frame); null where there is no such button.</summary>
            public string advisorButton, goToButton, readMoreButton;
            public int tipId;
            public float width = -1f, th, mh, wh, ah, height;
        }

        public static Color ColorFor(float mood) => mood >= 0f ? GoodColor : BadColor;

        public static string Number(float mood) => mood >= 0f ? "+" + mood.ToString("0.#") : mood.ToString("0.#");

        /// <summary>The mood bar: where they are, where they're heading, and the lines where breaks begin.</summary>
        public static void DrawBar(Rect r, PawnMood m)
        {
            Widgets.DrawBoxSolid(r, new Color(0.12f, 0.12f, 0.12f, 0.9f));
            var color = m.now < m.extreme ? ExtremeColor : m.now < m.major ? MajorColor : m.now < m.minor ? MinorColor : OkColor;
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width * m.now, r.height), color);
            void Mark(float at, Color c, string key)
            {
                var line = new Rect(r.x + r.width * at - 1f, r.y - 2f, 2f, r.height + 4f);
                Widgets.DrawBoxSolid(line, c);
                TooltipHandler.TipRegion(new Rect(line.x - 3f, line.y, 8f, line.height), () => key.Translate(at.ToStringPercent()), Gen.HashCombineInt(key.GetHashCode(), m.pawn?.thingIDNumber ?? 0));
            }
            Mark(m.extreme, ExtremeColor, "CC_Mood_LineExtreme");
            Mark(m.major, MajorColor, "CC_Mood_LineMajor");
            Mark(m.minor, MinorColor, "CC_Mood_LineMinor");
            // Where the mood is heading: a thin marker in the bar.
            if (Mathf.Abs(m.heading - m.now) > 0.01f)
                Widgets.DrawBoxSolid(new Rect(r.x + r.width * m.heading - 1f, r.y, 2f, r.height), m.heading > m.now ? GoodColor : BadColor);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(r, m.now.ToStringPercent());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>"Mood 42%, sinking towards 35%. 7% above the line where minor breaks become possible (35%)."</summary>
        public static string State(PawnMood m)
        {
            string dir = Mathf.Abs(m.heading - m.now) < 0.01f ? "CC_Mood_Steady".Translate().ToString()
                : (m.heading > m.now ? "CC_Mood_HeadingUp" : "CC_Mood_HeadingDown").Translate(m.heading.ToStringPercent()).ToString();
            string state = "CC_Mood_State".Translate(m.now.ToStringPercent(), dir).Resolve();
            string risk;
            if (m.pawn.InMentalState) risk = "CC_Mood_InBreak".Translate(m.pawn.MentalStateDef?.label ?? "?").Resolve();
            else if (m.now < m.extreme) risk = "CC_Mood_BelowExtreme".Translate(m.extreme.ToStringPercent()).Resolve();
            else if (m.now < m.major) risk = "CC_Mood_BelowMajor".Translate(m.major.ToStringPercent()).Resolve();
            else if (m.now < m.minor) risk = "CC_Mood_BelowMinor".Translate(m.minor.ToStringPercent()).Resolve();
            else risk = "CC_Mood_Above".Translate((m.now - m.minor).ToStringPercent(), m.minor.ToStringPercent()).Resolve();
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

        // ---------------- Text, once per refresh ----------------

        public static Texts TextsFor(MoodLine l)
        {
            var (kind, source) = MoodInsight.SourceOf(l.def);
            var t = new Texts
            {
                bad = l.mood < 0f,
                title = l.label + (l.count > 1 ? " ×" + l.count : ""),
                meta = MoodInsight.WhenItEnds(l) + (kind != MoodSource.Game ? "  ·  " + "CC_Mood_From".Translate(source).Resolve() : ""),
                why = l.why.NullOrEmpty() ? null : "CC_Cause_Line".Translate(l.why).Resolve(),
                hint = MoodInsight.HintFor(l.def),
                tipId = Gen.HashCombineInt(l.pawn?.thingIDNumber ?? 0, l.def.shortHash ^ (l.label?.GetHashCode() ?? 0))
            };
            Finish(t, MoodInsight.Advice(l), l.pawn?.MapHeld);
            return t;
        }

        public static Texts TextsFor(ColonyMoodLine c)
        {
            var (kind, source) = MoodInsight.SourceOf(c.def);
            var who = c.byPawn.OrderBy(kv => c.good ? -kv.Value : kv.Value).Take(10).Select(kv => kv.Key.LabelShort + " " + Number(kv.Value)).ToList();
            var t = new Texts
            {
                bad = !c.good,
                title = c.label + "  ·  " + "CC_Moods_People".Translate(c.byPawn.Count).Resolve(),
                meta = (kind != MoodSource.Game ? "CC_Mood_From".Translate(source).Resolve() + "  ·  " : "") + string.Join(", ", who) + (c.byPawn.Count > 10 ? ", …" : ""),
                why = c.lines.Count == 1 && !c.sample.why.NullOrEmpty() ? "CC_Cause_Line".Translate(c.sample.why).Resolve() : null,
                hint = MoodInsight.HintFor(c.def),
                tipId = Gen.HashCombineInt(c.def.shortHash, c.good ? 0x51A7 : 0x0BAD)
            };
            Finish(t, MoodInsight.Advice(c.sample), c.sample.pawn?.MapHeld);
            return t;
        }

        private static void Finish(Texts t, string advice, Map map)
        {
            if (!advice.NullOrEmpty()) t.advice = (t.bad ? "CC_Mood_Fix" : "CC_Mood_Keep").Translate(advice).Resolve();
            t.signal = t.bad ? ActiveSignal(t.hint, map) : null;
            t.buttons = t.bad && t.hint != null && (t.hint.article != null || !t.hint.councilTab.NullOrEmpty() || t.signal != null);
            if (!t.buttons) return;
            if (t.signal != null) t.advisorButton = "CC_Mood_Advisor".Translate(TextUtil.ShortSpeech(t.signal.title, 40)).Resolve();
            if (!t.hint.councilTab.NullOrEmpty()) t.goToButton = "CC_Mood_GoTo".Translate(MainTabWindow_Council.TabName(t.hint.councilTab)).Resolve();
            if (t.hint.article != null) t.readMoreButton = "CC_Mood_ReadMore".Translate(t.hint.article.label).Resolve();
        }

        private static AdvisorSignal ActiveSignal(MoodHintDef hint, Map map)
        {
            if (hint?.signal == null || map == null || !ColonyCouncilMod.Settings.enableAdvisor) return null;
            try
            {
                return CouncilMapComponent.For(map)?.Signals?.FirstOrDefault(s => s.def == hint.signal);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>How tall a line is at this width (measured once per width).</summary>
        public static float Measure(Texts t, float w)
        {
            if (Mathf.Abs(t.width - w) < 0.5f) return t.height;
            float textW = w - NumW - 6f;
            Text.Font = GameFont.Small;
            t.th = Text.CalcHeight(t.title, textW);
            Text.Font = GameFont.Tiny;
            t.mh = Text.CalcHeight(t.meta, textW);
            t.wh = t.why == null ? 0f : Text.CalcHeight(t.why, textW);
            t.ah = t.advice == null ? 0f : Text.CalcHeight(t.advice, textW);
            Text.Font = GameFont.Small;
            t.height = t.th + t.mh + t.wh + t.ah + (t.buttons ? 24f : 0f) + 8f;
            t.width = w;
            return t.height;
        }

        /// <summary>One line: size, name, when it ends, where it's from, why, and what to do.</summary>
        public static void Draw(float x, float y, float w, Texts t, float mood, System.Func<string> description)
        {
            float h = Measure(t, w);
            var row = new Rect(x, y, w, h);
            UiKit.Card(row, UiKit.WithAlpha(ColorFor(mood), 0.8f));
            TooltipHandler.TipRegion(row, description, t.tipId);

            Text.Font = GameFont.Medium;
            GUI.color = ColorFor(mood);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(x, y + 2f, NumW, Mathf.Min(h - 4f, 34f)), Number(mood));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float textW = w - NumW - 6f;
            float tx = x + NumW + 4f;
            float cy = y + 4f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(tx, cy, textW, t.th), t.title);
            cy += t.th;
            Text.Font = GameFont.Tiny;
            GUI.color = Faint;
            Widgets.Label(new Rect(tx, cy, textW, t.mh), t.meta);
            cy += t.mh;
            if (t.why != null)
            {
                Widgets.Label(new Rect(tx, cy, textW, t.wh), t.why);
                cy += t.wh;
            }
            GUI.color = t.bad ? AdviceColor : Faint;
            if (t.advice != null)
            {
                Widgets.Label(new Rect(tx, cy, textW, t.ah), t.advice);
                cy += t.ah;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (t.buttons) DrawButtons(t, tx, cy + 1f, textW);
        }

        private static void DrawButtons(Texts t, float x, float y, float w)
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
            if (t.signal != null && Button(t.advisorButton ?? "CC_Mood_Advisor".Translate(TextUtil.ShortSpeech(t.signal.title, 40)).Resolve()))
                MainTabWindow_Council.OpenTab("Priorities");
            if (!t.hint.councilTab.NullOrEmpty() && Button(t.goToButton ?? "CC_Mood_GoTo".Translate(MainTabWindow_Council.TabName(t.hint.councilTab)).Resolve()))
                MainTabWindow_Council.OpenTab(t.hint.councilTab);
            if (t.hint.article != null && Button(t.readMoreButton ?? "CC_Mood_ReadMore".Translate(t.hint.article.label).Resolve()))
                MainTabWindow_Council.OpenAlmanac(t.hint.article);
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Draws lines inside a scroll view, only those in sight. Returns the new y. viewTop/viewHeight: the visible
        /// part of the scroll view.
        /// </summary>
        public static float DrawList<T>(IEnumerable<T> items, float y, float w, float viewTop, float viewHeight, System.Func<T, Texts> texts,
            System.Func<T, float> mood, System.Func<T, System.Func<string>> description)
        {
            foreach (var item in items)
            {
                var t = texts(item);
                float h = Measure(t, w);
                if (y + h >= viewTop && y <= viewTop + viewHeight) Draw(0f, y, w, t, mood(item), description(item));
                y += h + 3f;
            }
            return y;
        }

        /// <summary>
        /// Select a colonist and open their Mood tab; if they can't be selected on the map, open their council page. With
        /// <paramref name="jump"/> off (from their own Needs tab), a colonist already selected is left where the camera is.
        /// </summary>
        public static void ShowMoodTab(Pawn p, bool jump = true)
        {
            if (p == null) return;
            try
            {
                if (!jump && Find.Selector.SingleSelectedThing == p)
                {
                    InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Mood));
                    return;
                }
                if (Find.MainTabsRoot.OpenTab != null && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect) Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
                // A carried baby or someone in a shuttle selects whoever holds them: don't show the wrong person.
                if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Mood));
                else MainTabWindow_Council.OpenPerson(p);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("open mood tab", e);
            }
        }
    }
}
