using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → World: what's coming (promised fights, questions waiting for an answer) as a timeline with countdowns,
    /// who's out there with the colony on their mind (nemeses, people who left, grudges, rivals, a great beast), and
    /// what the world brought lately.
    /// </summary>
    public static class WorldPanel
    {
        private static readonly Color Threat = new Color(0.9f, 0.55f, 0.45f);
        private static readonly Color Calm = new Color(0.72f, 0.8f, 0.95f);
        private static Vector2 scroll;
        private static float height = 600f;

        /// <summary>Countdown bars run over this many days: a fight five days off shows half a bar.</summary>
        private const int Horizon = GenDate.TicksPerDay * 10;

        private class Entry
        {
            public string title, text, tip, meterLabel, date;
            public Texture2D icon;
            public Color color = Color.white;
            public Faction faction;
            public Thing thing;
            public int dueTick = -1;
            public System.Action click;
            // Layout, worked out again when the width changes.
            public float width = -1f, textH, height;
        }

        private class Model
        {
            public List<Entry> coming, outThere, lately;
            public string next;
            public int raids;
        }

        private static Model model;
        private static float modelAt = -1f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (model == null || Time.realtimeSinceStartup - modelAt > 1f)
            {
                model = Build(comp);
                modelAt = Time.realtimeSinceStartup;
            }
            var m = model;
            string stat = m.coming.Count > 0 ? "CC_UI_Stat_Coming".Translate(m.coming.Count).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.World, "CC_World_Header".Translate(), "CC_UI_Sub_World".Translate(), stat, Threat, 0f,
                "CC_World_Intro".Translate());
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                float w = view.width;
                y += UiKit.SectionTitle(0f, y, w, "CC_World_Coming".Translate(), Icons.Raid, Threat);
                if (m.coming.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.World, "CC_World_NothingComing".Translate(), 70f);
                else y = Timeline(y + 4f, w, m.coming);
                if (m.next != null)
                {
                    UiKit.Icon(new Rect(4f, y + 3f, 16f, 16f), Icons.WorldEvent, UiKit.Faint);
                    y += UiKit.Paragraph(26f, y, w - 26f, m.next, UiKit.Faint) + 4f;
                }
                y += 14f;
                y += UiKit.SectionTitle(0f, y, w, "CC_World_OutThere".Translate(), Icons.Visitor, Calm);
                if (m.outThere.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.Visitor, "CC_World_NobodyOut".Translate(), 70f);
                else y = Grid(y + 2f, w, m.outThere);
                y += 14f;
                y += UiKit.SectionTitle(0f, y, w, "CC_World_Lately".Translate(), Icons.WorldEvent, Color.white);
                if (m.lately.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.WorldEvent, "CC_World_NothingLately".Translate(), 60f);
                else y = Rows(y, w, m.lately);
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

        private static Model Build(CouncilGameComponent comp)
        {
            var t = comp.happenings;
            var m = new Model { coming = Coming(t), outThere = OutThere(comp), lately = Lately(comp) };
            if (ColonyCouncilMod.Settings.worldEvents && t.nextTick > 0)
                m.next = "CC_World_NextEvent".Translate(TextUtil.DaysString(Mathf.Max(0, t.nextTick - Find.TickManager.TicksGame))).Resolve();
            return m;
        }

        // ---------------- What's coming ----------------

        private static List<Entry> Coming(HappeningTracker t)
        {
            int now = Find.TickManager.TicksGame;
            var list = new List<Entry>();
            // Questions waiting for an answer come first: they need the player now.
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Happening>().Where(l => !l.resolved).ToList())
            {
                var letter = l;
                int left = l.disappearAtTick > 0 ? l.disappearAtTick - now : 0;
                list.Add(new Entry
                {
                    text = "CC_World_Waiting".Translate(l.Label.ToString(), TextUtil.DaysString(Mathf.Max(0, left))).Resolve(),
                    icon = Icons.Warning,
                    color = UiKit.Gold,
                    click = () => letter.OpenLetter()
                });
            }
            var timed = new List<Entry>();
            foreach (var r in t.raids)
            {
                if (r.faction == null) continue;
                timed.Add(new Entry
                {
                    title = r.label.NullOrEmpty() ? "CC_World_AFight".Translate().ToString() : r.label,
                    tip = r.text,
                    icon = Icons.Raid,
                    color = Threat,
                    faction = r.faction,
                    dueTick = r.tick,
                    meterLabel = "CC_UI_Arrives".Translate()
                });
            }
            foreach (var mm in t.memos)
            {
                if (mm.used) continue;
                string line = null;
                var icon = Icons.WorldEvent;
                var color = Calm;
                if (mm.kind == "debt")
                {
                    line = ScenarioStakes.WorldLine(mm, now);
                    icon = Icons.Trader;
                    color = UiKit.Gold;
                }
                else if (mm.kind == "order")
                {
                    line = HappeningWorker_Order.WorldLine(mm, now);
                    icon = Icons.Trader;
                    color = UiKit.Gold;
                }
                else if (LivingEvents.IsPending(mm))
                {
                    line = LivingEvents.WorldLine(mm, now);
                    if (mm.kind == LivingEvents.KinComing || mm.kind == LivingEvents.Guest) icon = Icons.Visitor;
                    else if (mm.kind == LivingEvents.Market || mm.kind == LivingEvents.Fair) icon = Icons.Trader;
                }
                else if (StorytellerEvents.IsPending(mm))
                {
                    line = StorytellerEvents.WorldLine(mm, now);
                    if (mm.kind == StorytellerEvents.Venture) icon = Icons.Trader;
                }
                if (line == null) continue;
                timed.Add(new Entry
                {
                    text = line,
                    icon = icon,
                    color = color,
                    dueTick = mm.due != int.MaxValue && mm.due > 0 ? mm.due : -1,
                    meterLabel = "CC_UI_TimeLeft".Translate()
                });
            }
            // The rest in the order they'll happen; those without a date last.
            list.AddRange(timed.OrderBy(e => e.dueTick < 0 ? int.MaxValue : e.dueTick));
            return list;
        }

        // ---------------- Who's out there ----------------

        private static List<Entry> OutThere(CouncilGameComponent comp)
        {
            var t = comp.happenings;
            int now = Find.TickManager.TicksGame;
            var list = new List<Entry>();
            if (t.beast != null && !t.beast.Dead && t.beast.Spawned)
            {
                var beast = t.beast;
                int left = beast.mindState?.exitMapAfterTick > now ? beast.mindState.exitMapAfterTick - now : 0;
                list.Add(new Entry
                {
                    text = "CC_World_Beast".Translate(t.beastName ?? beast.LabelShort, TextUtil.DaysString(left)).Resolve(),
                    thing = beast,
                    color = Threat,
                    click = () => CameraJumper.TryJumpAndSelect(beast)
                });
            }
            if (ColonyCouncilMod.Settings.nemeses)
                foreach (var n in comp.nemeses.Open.Where(n => !n.captured && n.pawn != null && !n.pawn.Dead && !n.pawn.Spawned).Take(6))
                    list.Add(new Entry { text = "CC_World_Nemesis".Translate(n.Name, n.DeedText).Resolve(), icon = Icons.Nemesis, color = UiKit.Bad });
            foreach (var d in t.departed.Where(d => d.pawn != null && !d.pawn.Dead).OrderByDescending(d => d.tick))
                list.Add(new Entry
                {
                    text = (d.quit ? "CC_World_Departed_Quit" : "CC_World_Departed_Banished").Translate(d.name, TextUtil.DaysString(now - d.tick)).Resolve(),
                    icon = Icons.Visitor,
                    color = UiKit.Faint
                });
            foreach (var g in t.grudges.Where(g => !g.settled && g.faction != null && !g.faction.defeated))
                list.Add(new Entry { text = "CC_World_Grudge".Translate(g.faction.Name, g.name).Resolve(), icon = Icons.Vendetta, color = Threat });
            foreach (var mm in t.memos.Where(x => !x.used))
            {
                string line = mm.kind == "hunt" ? ScenarioStakes.WorldLine(mm, now) : Memo(mm);
                if (line != null) list.Add(new Entry { text = line, icon = MemoIcon(mm.kind), color = Calm });
            }
            return list;
        }

        private static Texture2D MemoIcon(string kind)
        {
            switch (kind)
            {
                case "hunt":
                case "challenger": return Icons.Raid;
                case "child": return Icons.Birth;
                case "deserter": return Icons.Visitor;
                case "beast": return Icons.Nemesis;
                case "pilgrims": return Icons.Heart;
                default: return Icons.WorldEvent;
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
                    return LivingEvents.MemoLine(m) ?? StorytellerEvents.MemoLine(m);
            }
        }

        // ---------------- Lately ----------------

        private static List<Entry> Lately(CouncilGameComponent comp)
        {
            var list = new List<Entry>();
            for (int i = comp.chronicle.Count - 1; i >= 0 && list.Count < 12; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind != ChronicleKind.World) continue;
                list.Add(new Entry { text = e.text, date = MainTabWindow_Council.DayString(e.tick), icon = Icons.WorldEvent, color = UiKit.WorldColor });
            }
            return list;
        }

        // ---------------- Drawing ----------------

        private const float RailX = 14f;
        private const float CardX = 36f;

        /// <summary>Cards down a line, each with its icon on the line: the order things will happen in.</summary>
        private static float Timeline(float y, float w, List<Entry> entries)
        {
            float cardW = w - CardX;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float h = Measure(e, cardW);
                if (i < entries.Count - 1) Widgets.DrawBoxSolid(new Rect(RailX - 1f, y + 26f, 2f, h + 8f - 22f), new Color(1f, 1f, 1f, 0.12f));
                UiKit.Badge(new Rect(RailX - 12f, y + 4f, 24f, 24f), e.icon, e.color);
                DrawCard(new Rect(CardX, y, cardW, h), e);
                y += h + 8f;
            }
            return y;
        }

        private static float Measure(Entry e, float w)
        {
            if (Mathf.Abs(e.width - w) < 0.5f) return e.height;
            e.width = w;
            Text.Font = GameFont.Small;
            e.textH = e.text.NullOrEmpty() ? 0f : Text.CalcHeight(e.text, w - 16f);
            e.height = 8f + (e.title != null ? 24f : 0f) + (e.textH > 0f ? e.textH + 2f : 0f) + (e.dueTick >= 0 ? UiKit.MeterHeight + 2f : 0f) + 4f;
            e.height = Mathf.Max(e.height, 34f);
            return e.height;
        }

        private static void DrawCard(Rect r, Entry e)
        {
            UiKit.Card(r, UiKit.WithAlpha(e.color, 0.7f), false, e.click != null || !e.tip.NullOrEmpty());
            float x = r.x + 10f, w = r.width - 16f;
            float cy = r.y + 6f;
            if (e.title != null)
            {
                float chipW = 0f;
                if (e.faction != null)
                {
                    float maxChip = Mathf.Min(220f, w * 0.5f);
                    chipW = UiKit.FactionChipWidth(e.faction, 22f, maxChip);
                    UiKit.FactionChip(r.xMax - 8f - chipW, cy, e.faction, 22f, maxChip);
                    chipW += 8f;
                }
                UiKit.LabelFit(new Rect(x, cy, w - chipW, 22f), e.title, e.color);
                cy += 24f;
            }
            if (e.textH > 0f)
            {
                Widgets.Label(new Rect(x, cy, w, e.textH), e.text);
                cy += e.textH + 2f;
            }
            if (e.dueTick >= 0) UiKit.Countdown(new Rect(x, cy, w, UiKit.MeterHeight), e.dueTick - Find.TickManager.TicksGame, Horizon, e.meterLabel);
            if (!e.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, e.tip);
            if (e.click != null && Widgets.ButtonInvisible(r)) e.click();
        }

        /// <summary>Small cards in two columns (one when the page is narrow).</summary>
        private static float Grid(float y, float w, List<Entry> entries)
        {
            int cols = w >= 600f ? 2 : 1;
            float cw = (w - (cols - 1) * 8f) / cols;
            for (int i = 0; i < entries.Count; i += cols)
            {
                float rowH = 0f;
                for (int c = 0; c < cols && i + c < entries.Count; c++) rowH = Mathf.Max(rowH, MeasureSmall(entries[i + c], cw));
                for (int c = 0; c < cols && i + c < entries.Count; c++)
                {
                    var e = entries[i + c];
                    var r = new Rect(c * (cw + 8f), y, cw, rowH);
                    UiKit.Card(r, null, false, e.click != null);
                    var iconRect = new Rect(r.x + 8f, r.y + 7f, 22f, 22f);
                    if (e.thing != null) Widgets.ThingIcon(iconRect, e.thing);
                    else UiKit.Icon(iconRect, e.icon, e.color);
                    Widgets.Label(new Rect(r.x + 38f, r.y + 6f, cw - 46f, e.textH), e.text);
                    if (e.click != null && Widgets.ButtonInvisible(r)) e.click();
                }
                y += rowH + 6f;
            }
            return y;
        }

        private static float MeasureSmall(Entry e, float w)
        {
            if (Mathf.Abs(e.width - w) < 0.5f) return e.height;
            e.width = w;
            Text.Font = GameFont.Small;
            e.textH = Text.CalcHeight(e.text ?? "", w - 46f);
            e.height = Mathf.Max(36f, e.textH + 12f);
            return e.height;
        }

        /// <summary>The date on the left, the line beside it, every other row shaded.</summary>
        private static float Rows(float y, float w, List<Entry> entries)
        {
            const float dateW = 64f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (Mathf.Abs(e.width - w) >= 0.5f)
                {
                    e.width = w;
                    Text.Font = GameFont.Small;
                    e.textH = Text.CalcHeight(e.text ?? "", w - dateW - 28f);
                    e.height = Mathf.Max(24f, e.textH + 4f);
                }
                var r = new Rect(0f, y, w, e.height);
                UiKit.Zebra(r, i);
                UiKit.LabelFit(new Rect(4f, y + 2f, dateW - 6f, 20f), e.date, UiKit.Faint, GameFont.Tiny);
                UiKit.Icon(new Rect(dateW, y + 4f, 14f, 14f), e.icon, e.color);
                GUI.color = e.color;
                Widgets.Label(new Rect(dateW + 22f, y + 2f, w - dateW - 28f, e.textH), e.text);
                GUI.color = Color.white;
                y += e.height;
            }
            return y;
        }
    }
}
