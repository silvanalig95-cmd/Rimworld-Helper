using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Who's out there, by what they are to the colony (Region → Peoples → Faces draws each group as a section).</summary>
    public enum OutGroup
    {
        /// <summary>Nemeses, grudges, rivals, a great beast.</summary>
        Foes,
        /// <summary>Those who quit or were sent away, a deserter.</summary>
        Left,
        /// <summary>The rest: a child raised elsewhere, pilgrims, hunts, stories under way.</summary>
        Others
    }

    /// <summary>
    /// What the region window's Neighbours page held before the region had four views, as sections the views draw: what's
    /// coming (questions waiting for an answer, debts, orders and visits due, stories with a stage on its way) on Here and
    /// away, as a timeline; who's out there with the colony on their mind (old foes, those who left, the rest) and the faces
    /// the colony keeps up with on Peoples → Faces. The fights on their way are the Threats lens's (UI/Region/LensModel.cs),
    /// the calendar's days and the council's meetings This week's and the Calendar's, the towns nearby the Stance lens's.
    /// Built at most once a second while a view shows them.
    /// </summary>
    public static class WorldPanel
    {
        private static readonly Color Threat = UiKit.Sour;
        private static readonly Color Calm = UiKit.World;

        /// <summary>Countdown bars run over this many days: a fight five days off shows half a bar.</summary>
        private const int Horizon = GenDate.TicksPerDay * 10;

        private class Entry
        {
            public string title, text, tip, meterLabel, date;
            public Texture2D icon;
            public Color color = Color.white;
            public Faction faction;
            public Thing thing;
            /// <summary>Whose card a click opens (a nemesis, someone who left, a people), or none.</summary>
            public ThingRef card;
            public OutGroup group = OutGroup.Others;
            public int dueTick = -1;
            public System.Action click;
            // Layout, worked out again when the width changes.
            public float width = -1f, textH, height;
        }

        private class Model
        {
            public List<Entry> coming, outThere;
        }

        private static Model model;
        private static float modelAt = -1f;

        private static Model Current(CouncilGameComponent comp)
        {
            if (model == null || Time.realtimeSinceStartup - modelAt > 1f)
            {
                model = Build(comp);
                modelAt = Time.realtimeSinceStartup;
            }
            return model;
        }

        /// <summary>How many things are on their way (Here and away's count).</summary>
        public static int ComingCount(CouncilGameComponent comp) => comp == null ? 0 : Current(comp).coming.Count;

        /// <summary>
        /// "What's coming" at <paramref name="y"/> under its heading (Region → Here and away): the timeline of what is on
        /// its way to the colony, or a sentence of what would show. Returns the y below it.
        /// </summary>
        public static float ComingSection(float y, float w, CouncilGameComponent comp, Folds folds)
        {
            if (comp == null) return y;
            var m = Current(comp);
            y += UiKit.SectionHeading(0f, y, w, "CC_World_Coming".Translate(), m.coming.Count, folds, "coming", out bool open);
            if (!open) return y + 4f;
            if (m.coming.Count == 0) return y + UiKit.Paragraph(4f, y, w - 4f, "CC_World_NothingComing".Translate(), UiKit.Faint) + 10f;
            return Timeline(y + 4f, w, m.coming) + 4f;
        }

        /// <summary>
        /// One group of who's out there under its heading (Region → Peoples → Faces), a click on one opening their card.
        /// Returns the y below it; a group with nobody in it says so in a sentence.
        /// </summary>
        public static float OutThereSection(float y, float w, CouncilGameComponent comp, OutGroup group, Folds folds)
        {
            if (comp == null) return y;
            var m = Current(comp);
            var list = m.outThere.Where(e => e.group == group).ToList();
            y += UiKit.SectionHeading(0f, y, w, ("CC_Faces_" + group).Translate(), list.Count, folds, "out:" + group, out bool open);
            if (!open) return y + 4f;
            if (list.Count == 0) return y + UiKit.Paragraph(4f, y, w - 4f, ("CC_Faces_" + group + "_None").Translate(), UiKit.Faint) + 10f;
            return Grid(y + 2f, w, list) + 6f;
        }

        /// <summary>
        /// The regulars the colony keeps up with (Happenings/Regulars.cs) and what visitors have come to notice about the
        /// place (Immersion/VisitorNotices.cs), as one faint line each.
        /// </summary>
        public static float Known(float y, float w, CouncilGameComponent comp)
        {
            if (comp == null) return y;
            string regulars = UiCache.Get(comp, 75, () => Regulars.Line());
            string known = UiCache.Get(comp, 76, () => VisitorNotices.KnownLine());
            foreach (var text in new[] { regulars, known })
            {
                if (text.NullOrEmpty()) continue;
                y += 4f;
                UiKit.Icon(new Rect(2f, y + 2f, 16f, 16f), Icons.Visitor, UiKit.Dim);
                y += UiKit.Paragraph(24f, y, w - 24f, text, UiKit.Faint, GameFont.Tiny);
            }
            return y;
        }

        private static Model Build(CouncilGameComponent comp)
        {
            var t = comp.happenings;
            // Events come unannounced: no countdown to the next one (with the storyteller in charge of them, Rimfolk's
            // own timer doesn't run at all, so a countdown would only ever show a time long past).
            return new Model { coming = Coming(t), outThere = OutThere(comp) };
        }

        // ---------------- What's coming ----------------

        /// <summary>
        /// Questions waiting for an answer first, then what is due in the order it comes: debts, orders, commissions,
        /// visits and markets announced, stories with a stage on its way. The fights on their way are the Threats lens's;
        /// the calendar's days and the council's meetings are This week's.
        /// </summary>
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
                else if (mm.kind == HappeningWorker_Commission.Kind)
                {
                    line = HappeningWorker_Commission.WorldLine(mm, now);
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
            // Story chains with a stage on its way (Workers_Chains2.cs).
            foreach (var n in StoryChains.Coming(now)) timed.Add(FromNote(n, "CC_UI_TimeLeft".Translate()));
            // The rest in the order they'll happen; those without a date last.
            list.AddRange(timed.OrderBy(e => e.dueTick < 0 ? int.MaxValue : e.dueTick));
            return list;
        }

        /// <summary>A story's or a mission's line as a card.</summary>
        private static Entry FromNote(WorldNote n, string meterLabel = null) => new Entry
        {
            title = n.title, text = n.text, tip = n.tip, icon = n.icon ?? Icons.WorldEvent, color = n.color, faction = n.faction,
            dueTick = n.due, meterLabel = meterLabel, click = n.click
        };

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
                    group = OutGroup.Foes,
                    click = () =>
                    {
                        Pages.LeaveForMap();
                        CameraJumper.TryJumpAndSelect(beast);
                    }
                });
            }
            if (ColonyCouncilMod.Settings.nemeses)
                foreach (var n in comp.nemeses.Open.Where(n => !n.captured && n.pawn != null && !n.pawn.Dead && !n.pawn.Spawned).Take(6))
                    list.Add(new Entry { text = "CC_World_Nemesis".Translate(n.Name, n.DeedText).Resolve(), icon = Icons.Nemesis, color = UiKit.Bad, group = OutGroup.Foes, card = ThingRefs.Of(n.pawn) });
            foreach (var d in t.departed.Where(d => d.pawn != null && !d.pawn.Dead).OrderByDescending(d => d.tick))
                list.Add(new Entry
                {
                    // One the council sent away is remembered so, with the colony's word for it (Social/Banishment.cs).
                    text = !d.quit && BanishTracker.Banished(d.pawn) is BanishRecord br
                        ? "CC_World_Departed_Council".Translate(d.name, TextUtil.DaysString(now - d.tick), br.word ?? BanishTracker.AgeWord()).Resolve()
                        : (d.quit ? "CC_World_Departed_Quit" : "CC_World_Departed_Banished").Translate(d.name, TextUtil.DaysString(now - d.tick)).Resolve(),
                    icon = Icons.Visitor,
                    color = UiKit.Faint,
                    group = OutGroup.Left,
                    card = ThingRefs.Of(d.pawn)
                });
            foreach (var g in Grudges.All().Where(g => !g.Settled && g.faction != null && !g.faction.defeated))
                list.Add(new Entry { text = "CC_World_Grudge".Translate(g.faction.Name, g.Name).Resolve(), icon = Icons.Vendetta, color = Threat, group = OutGroup.Foes, card = ThingRefs.Of(g.faction) });
            foreach (var mm in t.memos.Where(x => !x.used))
            {
                string line = mm.kind == "hunt" ? ScenarioStakes.WorldLine(mm, now) : Memo(mm);
                if (line == null) continue;
                var group = mm.kind == "challenger" ? OutGroup.Foes : mm.kind == "deserter" ? OutGroup.Left : OutGroup.Others;
                var card = mm.pawn != null && !mm.pawn.Dead ? ThingRefs.Of(mm.pawn) : ThingRefs.Of(mm.faction);
                list.Add(new Entry { text = line, icon = MemoIcon(mm.kind), color = Calm, group = group, card = card });
            }
            foreach (var n in StoryChains.OutThere(now)) list.Add(FromNote(n));
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
                if (i < entries.Count - 1) Widgets.DrawBoxSolid(new Rect(RailX - 1f, y + 26f, 2f, h + 8f - 22f), UiKit.CardBorder);
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
                    UiKit.FactionChip(r.xMax - 8f - chipW, cy, e.faction, 22f, maxChip, true);
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
                    bool opens = !e.card.IsNone;
                    UiKit.Card(r, null, false, e.click != null || opens);
                    var iconRect = new Rect(r.x + 8f, r.y + 7f, 22f, 22f);
                    if (e.thing != null) Widgets.ThingIcon(iconRect, e.thing);
                    else UiKit.Icon(iconRect, e.icon, e.color);
                    Widgets.Label(new Rect(r.x + 38f, r.y + 6f, cw - 46f, e.textH), e.text);
                    // Their card in the drawer (a nemesis, someone who left, a people), else what the line leads to.
                    if (opens && Mouse.IsOver(r)) Previews.Hover(e.card, r);
                    if ((opens || e.click != null) && Widgets.ButtonInvisible(r))
                    {
                        if (opens)
                        {
                            Cards.Click();
                            Cards.Open(e.card);
                        }
                        else e.click();
                    }
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

    }
}
