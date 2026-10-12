using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → World: what's coming (promised fights, questions waiting for an answer), who's out there with the colony
    /// on their mind (nemeses, people who left, grudges, rivals, a great beast), and what the world brought lately.
    /// </summary>
    public static class WorldPanel
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Threat = new Color(0.9f, 0.55f, 0.45f);
        private static readonly Color Calm = new Color(0.72f, 0.8f, 0.95f);
        private static Vector2 scroll;
        private static float height = 600f;

        private struct Row
        {
            public string text;
            public string tip;
            public System.Action click;
        }

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            var t = comp.happenings;
            var view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, w, 32f), "CC_World_Header".Translate());
                Text.Font = GameFont.Small;
                y += 34f;
                y += Label(y, w, "CC_World_Intro".Translate(), Faint) + 10f;

                y = Section(y, w, "CC_World_Coming".Translate(), Threat, Coming(t), "CC_World_NothingComing".Translate());
                if (ColonyCouncilMod.Settings.worldEvents && t.nextTick > 0)
                {
                    int left = Mathf.Max(0, t.nextTick - Find.TickManager.TicksGame);
                    y += Label(y, w, "CC_World_NextEvent".Translate(TextUtil.DaysString(left)).Resolve(), Faint) + 4f;
                }
                y += 10f;
                y = Section(y, w, "CC_World_OutThere".Translate(), Calm, OutThere(comp), "CC_World_NobodyOut".Translate());
                y += 10f;
                y = Section(y, w, "CC_World_Lately".Translate(), Color.white, Lately(comp), "CC_World_NothingLately".Translate());
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        // ---------------- What's coming ----------------

        private static IEnumerable<Row> Coming(HappeningTracker t)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var r in t.raids.OrderBy(r => r.tick))
            {
                if (r.faction == null) continue;
                int left = r.tick - now;
                string label = r.label.NullOrEmpty() ? "CC_World_AFight".Translate().ToString() : r.label;
                yield return new Row
                {
                    text = left > GenDate.TicksPerHour
                        ? "CC_World_Raid".Translate(label, r.faction.Name, TextUtil.DaysString(left)).Resolve()
                        : "CC_World_RaidSoon".Translate(label, r.faction.Name).Resolve(),
                    tip = r.text
                };
            }
            foreach (var m in t.memos.Where(m => !m.used && m.kind == "debt").OrderBy(m => m.due))
            {
                string line = ScenarioStakes.WorldLine(m, now);
                if (line != null) yield return new Row { text = line };
            }
            foreach (var m in t.memos.Where(m => !m.used && m.kind == "order").OrderBy(m => m.due))
            {
                string line = HappeningWorker_Order.WorldLine(m, now);
                if (line != null) yield return new Row { text = line };
            }
            foreach (var m in t.memos.Where(LivingEvents.IsPending).OrderBy(m => m.due))
            {
                // Formatting a line is costly; the page redraws every frame.
                var memo = m;
                string line = UiCache.Get(memo, 7, () => LivingEvents.WorldLine(memo, now));
                if (line != null) yield return new Row { text = line };
            }
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Happening>().Where(l => !l.resolved).ToList())
            {
                var letter = l;
                int left = l.disappearAtTick > 0 ? l.disappearAtTick - now : 0;
                yield return new Row
                {
                    text = "CC_World_Waiting".Translate(l.Label.ToString(), TextUtil.DaysString(Mathf.Max(0, left))).Resolve(),
                    click = () => letter.OpenLetter()
                };
            }
        }

        // ---------------- Who's out there ----------------

        private static IEnumerable<Row> OutThere(CouncilGameComponent comp)
        {
            var t = comp.happenings;
            int now = Find.TickManager.TicksGame;
            if (t.beast != null && !t.beast.Dead && t.beast.Spawned)
            {
                var beast = t.beast;
                int left = beast.mindState?.exitMapAfterTick > now ? beast.mindState.exitMapAfterTick - now : 0;
                yield return new Row
                {
                    text = "CC_World_Beast".Translate(t.beastName ?? beast.LabelShort, TextUtil.DaysString(left)).Resolve(),
                    click = () => CameraJumper.TryJumpAndSelect(beast)
                };
            }
            if (ColonyCouncilMod.Settings.nemeses)
                foreach (var n in comp.nemeses.Open.Where(n => !n.captured && n.pawn != null && !n.pawn.Dead && !n.pawn.Spawned).Take(6))
                    yield return new Row { text = "CC_World_Nemesis".Translate(n.Name, n.DeedText).Resolve() };
            foreach (var d in t.departed.Where(d => d.pawn != null && !d.pawn.Dead).OrderByDescending(d => d.tick))
                yield return new Row { text = (d.quit ? "CC_World_Departed_Quit" : "CC_World_Departed_Banished").Translate(d.name, TextUtil.DaysString(now - d.tick)).Resolve() };
            foreach (var g in t.grudges.Where(g => !g.settled && g.faction != null && !g.faction.defeated))
                yield return new Row { text = "CC_World_Grudge".Translate(g.faction.Name, g.name).Resolve() };
            foreach (var m in t.memos.Where(m => !m.used))
            {
                string line = m.kind == "hunt" ? ScenarioStakes.WorldLine(m, now) : Memo(m);
                if (line != null) yield return new Row { text = line };
            }
        }

        private static string Memo(Memo m)
        {
            string x = m.pawn != null && !m.pawn.Dead ? m.pawn.LabelShort : null;
            string ours = m.other != null ? m.other.LabelShort : "CC_World_OurBest".Translate().ToString();
            switch (m.kind)
            {
                case "challenger":
                    if (x == null) return null;
                    var sk = DefDatabase<SkillDef>.GetNamedSilentFail(m.def ?? "");
                    string what = HappeningText.SkillNoun(sk, true);
                    return (m.won ? "CC_World_Rival_Won" : "CC_World_Rival_Lost").Translate(x, ours, what).Resolve();
                case "child":
                    return x == null || m.faction == null ? null : "CC_World_Child".Translate(x, m.faction.Name).Resolve();
                case "deserter":
                    return x == null || m.faction == null || !m.pawn.IsColonist ? null : "CC_World_Deserter".Translate(x, m.faction.Name).Resolve();
                case "beast":
                    return m.text.NullOrEmpty() ? null : "CC_World_BeastLine".Translate(m.text).Resolve();
                case "pilgrims":
                    return m.faction == null ? null : "CC_World_Pilgrims".Translate(m.faction.Name).Resolve();
                default:
                    return UiCache.Get(m, 8, () => LivingEvents.MemoLine(m));
            }
        }

        // ---------------- Lately ----------------

        private static IEnumerable<Row> Lately(CouncilGameComponent comp) =>
            comp.chronicle.Where(e => e.kind == ChronicleKind.World).Reverse().Take(12)
                .Select(e => new Row { text = "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1) + ": " + e.text });

        // ---------------- Drawing ----------------

        private static float Section(float y, float w, string header, Color color, IEnumerable<Row> rows, string empty)
        {
            Text.Font = GameFont.Medium;
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, w, 30f), header);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 32f;
            bool any = false;
            foreach (var row in rows)
            {
                any = true;
                float h = Text.CalcHeight(row.text, w - 8f);
                var r = new Rect(0f, y, w, h + 4f);
                if (row.click != null || !row.tip.NullOrEmpty()) Widgets.DrawHighlightIfMouseover(r);
                Widgets.Label(new Rect(8f, y + 2f, w - 8f, h), row.text);
                if (!row.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, row.tip);
                if (row.click != null && Widgets.ButtonInvisible(r)) row.click();
                y += h + 6f;
            }
            if (!any) y += Label(y, w, empty, Faint) + 4f;
            return y;
        }

        private static float Label(float y, float w, string text, Color color)
        {
            if (text.NullOrEmpty()) return 0f;
            float h = Text.CalcHeight(text, w);
            GUI.color = color;
            Widgets.Label(new Rect(0f, y, w, h), text);
            GUI.color = Color.white;
            return h;
        }
    }
}
