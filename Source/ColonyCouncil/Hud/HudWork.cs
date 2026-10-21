using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Work and idle: a row for each colonist with what they're doing now (the game's job report, cut short), a mark when
    /// they're idle (the game's own idle check, the one its "idle colonists" alert uses) and for how long, three small
    /// bars for food, rest and recreation (red when low), and a mark when drafted, down or in a break. Above the rows,
    /// only when there's something to say: how many are idle, who needs food or sleep, and work waiting that nobody has
    /// switched on (a fire, someone to tend, marked plants or rock), looked for once every five seconds with a handful of
    /// cheap checks. Starving or exhausted first, then idle (longest first), then busy. A click goes to them.
    /// </summary>
    public class HudWork : HudPanel
    {
        public const string PartDoing = "doing", PartIdle = "idle", PartFood = "needfood", PartRest = "needrest", PartJoy = "needjoy", PartStatus = "status",
            PartDrafted = "drafted", PartColony = "colony", PartUnmanned = "unmanned";
        public static readonly string[] Parts = { PartDoing, PartIdle, PartFood, PartRest, PartJoy, PartStatus, PartDrafted, PartColony, PartUnmanned };

        private sealed class Row
        {
            public Pawn pawn;
            public float score;
            public string name, doing, mark, tip;
            public Color markColor;
            public bool idle;
            public float food = -1f, rest = -1f, joy = -1f;
            public bool foodLow, restLow, joyLow;
            public int barOrder;
        }

        private sealed class Line
        {
            public string text, tip;
            public float score;
            public Texture2D icon;
            public Action click;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly List<Line> colony = new List<Line>();
        private readonly List<Pawn> idlePeople = new List<Pawn>(), needyPeople = new List<Pawn>();
        private int hidden, cycle;
        private string moreText;
        private string[] cutAt;

        /// <summary>When each colonist was first seen idle (ticks), by ID; gone when they work again.</summary>
        private static readonly Dictionary<int, int> idleSince = new Dictionary<int, int>();

        // Work waiting that nobody does: looked for at most every five seconds.
        private float unmannedAt = -100f;
        private readonly List<(string label, string why)> unmanned = new List<(string, string)>();

        public HudWork() : base(HudLayout.Work)
        {
        }

        // ---------------- Gathering ----------------

        protected override void Refresh()
        {
            rows.Clear();
            colony.Clear();
            idlePeople.Clear();
            needyPeople.Clear();
            var st = St;
            cutAt ??= HudKit.T("CC_Pnl_W_CutAt").Split('|').Select(w => w.Trim()).Where(w => w.Length > 0).Select(w => " " + w + " ").ToArray();
            int now = Find.TickManager?.TicksGame ?? 0;
            var order = st.sort == HudSort.Bar ? HudMood.BarOrder() : null;
            var all = new List<Row>();
            var people = HudPeople.Who(st.who == HudWho.WithPrisoners ? HudWho.Everywhere : st.who);
            var present = new HashSet<int>();
            foreach (var p in people)
            {
                Row row;
                try
                {
                    row = RowFor(p, st, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("work panel: " + p.LabelShort, e);
                    continue;
                }
                present.Add(p.thingIDNumber);
                if (row.idle) idlePeople.Add(p);
                if (row.foodLow || row.restLow) needyPeople.Add(p);
                if (p.Drafted && !st.Shows(PartDrafted)) continue;
                row.barOrder = order != null && order.TryGetValue(p, out int i) ? i : int.MaxValue;
                all.Add(row);
            }
            // Forget the idle clocks of those no longer listed.
            foreach (var id in idleSince.Keys.Where(k => !present.Contains(k)).ToList()) idleSince.Remove(id);
            if (st.sort == HudSort.Bar) all = all.OrderBy(r => r.barOrder).ThenBy(r => r.pawn.thingIDNumber).ToList();
            else all = all.OrderByDescending(r => r.score).ThenBy(r => r.pawn.thingIDNumber).ToList();
            var keep = HudMath.Keep(all.Select(r => r.score).ToList(), st.maxRows, HudRank.LifeFloor, out hidden);
            foreach (int i in keep) rows.Add(all[i]);
            moreText = hidden > 0 ? "CC_Pnl_AndMore".Translate(hidden).Resolve() : null;
            if (st.Shows(PartColony)) Colony(now);
            if (st.Shows(PartUnmanned)) Unmanned();
        }

        private Row RowFor(Pawn p, HudPanelState st, int now)
        {
            var row = new Row { pawn = p, name = p.LabelShortCap };
            var food = p.needs?.food;
            var rest = p.needs?.rest;
            var joy = p.needs?.joy;
            if (food != null)
            {
                row.food = food.CurLevelPercentage;
                row.foodLow = food.CurCategory >= HungerCategory.Hungry;
            }
            if (rest != null)
            {
                row.rest = rest.CurLevelPercentage;
                row.restLow = rest.CurCategory >= RestCategory.Tired;
            }
            if (joy != null)
            {
                row.joy = joy.CurLevelPercentage;
                row.joyLow = joy.CurCategory <= JoyCategory.Low;
            }
            bool starving = food != null && food.Starving;
            bool urgent = food != null && food.CurCategory >= HungerCategory.UrgentlyHungry;
            bool exhausted = rest != null && rest.CurCategory == RestCategory.Exhausted;
            bool inBreak = p.InMentalState;
            // The game's own idle check (Pawn_MindState.IsIdle: on the map, not down, the last job an idle one).
            row.idle = p.mindState != null && p.mindState.IsIdle && !p.Drafted;
            float idleHours = 0f;
            if (row.idle)
            {
                if (!idleSince.TryGetValue(p.thingIDNumber, out int since))
                {
                    since = now;
                    idleSince[p.thingIDNumber] = since;
                }
                idleHours = HudWorkMath.IdleHours(since, now);
            }
            else idleSince.Remove(p.thingIDNumber);
            float mal = starving ? p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f : 0f;
            row.score = HudRank.Work(starving, mal, urgent, exhausted, inBreak, row.idle, idleHours, row.foodLow || row.restLow, p.Drafted);
            // What they're doing, in the game's words, cut short.
            string report = null;
            string away = HudPeople.Away(p);
            if (away != null) report = away;
            else if (inBreak) report = p.MentalStateDef?.LabelCap;
            else
                try
                {
                    report = p.jobs?.curDriver?.GetReport();
                }
                catch (Exception)
                {
                    report = null;
                }
            row.doing = HudWorkMath.ShortReport(report, cutAt).CapitalizeFirst();
            // The mark: a break, down, drafted, or idle and for how long.
            if (inBreak)
            {
                row.mark = HudKit.T("CC_Pnl_W_Break");
                row.markColor = UiKit.Bad;
            }
            else if (p.Downed)
            {
                row.mark = HudKit.T("CC_Pnl_W_Down");
                row.markColor = UiKit.Warn;
            }
            else if (p.Drafted)
            {
                row.mark = HudKit.T("CC_Pnl_W_Drafted");
                row.markColor = UiKit.Gold;
            }
            else if (row.idle && st.Shows(PartIdle))
            {
                row.mark = idleHours >= 1f ? "CC_Pnl_W_IdleFor".Translate(Mathf.FloorToInt(idleHours)).Resolve() : HudKit.T("CC_Pnl_W_Idle");
                row.markColor = BarLow;
            }
            if (!st.Shows(PartStatus) && !row.idle) row.mark = null;
            row.tip = Tip(p, report, row, idleHours);
            return row;
        }

        private static string Tip(Pawn p, string report, Row row, float idleHours)
        {
            var sb = new StringBuilder();
            sb.Append(p.LabelShortCap.Colorize(UiKit.HeaderColor));
            if (!report.NullOrEmpty()) sb.Append("\n").Append(report.CapitalizeFirst());
            if (row.idle) sb.Append("\n").Append("CC_Pnl_W_IdleTip".Translate(idleHours.ToString("0.#")).Resolve().Colorize(UiKit.Warn));
            var needs = new List<string>();
            if (row.food >= 0f) needs.Add(Need("CC_Pnl_W_Food", row.food, row.foodLow));
            if (row.rest >= 0f) needs.Add(Need("CC_Pnl_W_Rest", row.rest, row.restLow));
            if (row.joy >= 0f) needs.Add(Need("CC_Pnl_W_Joy", row.joy, row.joyLow));
            if (needs.Count > 0) sb.Append("\n\n").Append(string.Join("\n", needs));
            sb.Append("\n\n").Append(HudKit.T("CC_Pnl_W_Click").Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        private static string Need(string key, float level, bool low)
        {
            string s = HudKit.T(key) + ": " + level.ToStringPercent();
            return low ? s.Colorize(UiKit.Bad) : s;
        }

        /// <summary>The colony part: who is idle, who needs food or sleep. Nothing at all when everyone is busy and fed.</summary>
        private void Colony(int now)
        {
            if (idlePeople.Count > 0)
            {
                float hours = 0f;
                foreach (var p in idlePeople)
                    if (idleSince.TryGetValue(p.thingIDNumber, out int since))
                        hours = Mathf.Max(hours, HudWorkMath.IdleHours(since, now));
                var list = idlePeople.ToList();
                colony.Add(new Line
                {
                    text = "CC_Pnl_W_IdleCount".Translate(list.Count).Resolve(), score = HudRank.Idle(hours), icon = Icons.Warning,
                    tip = Names(list) + "\n\n" + HudKit.T("CC_Pnl_W_CycleClick").Colorize(ColoredText.SubtleGrayColor), click = () => Cycle(list)
                });
            }
            if (needyPeople.Count > 0)
            {
                var list = needyPeople.ToList();
                bool urgent = list.Any(p => p.needs?.food?.CurCategory >= HungerCategory.UrgentlyHungry || p.needs?.rest?.CurCategory == RestCategory.Exhausted);
                colony.Add(new Line
                {
                    text = "CC_Pnl_W_NeedyCount".Translate(list.Count).Resolve(), score = urgent ? HudRank.Hungry : HudRank.LowNeeds, icon = Icons.CatFood,
                    tip = Needy(list) + "\n\n" + HudKit.T("CC_Pnl_W_CycleClick").Colorize(ColoredText.SubtleGrayColor), click = () => Cycle(list)
                });
            }
        }

        private static string Names(List<Pawn> people) => string.Join(", ", people.Select(p => p.LabelShort));

        private static string Needy(List<Pawn> people)
        {
            var lines = new List<string>();
            foreach (var p in people)
            {
                var what = new List<string>();
                var food = p.needs?.food;
                var rest = p.needs?.rest;
                if (food != null && food.CurCategory >= HungerCategory.Hungry) what.Add(HungerLevelUtility.GetLabel(food.CurCategory).ToLower());
                if (rest != null && rest.CurCategory >= RestCategory.Tired) what.Add(RestCategoryUtility.GetLabel(rest.CurCategory).ToLower());
                lines.Add(p.LabelShort + ": " + string.Join(", ", what));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Each click goes to the next of them.</summary>
        private void Cycle(List<Pawn> people)
        {
            if (people.Count == 0) return;
            cycle = (cycle + 1) % people.Count;
            HudPeople.Go(people[cycle]);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        // ---------------- Work nobody does ----------------

        private struct Waiting
        {
            public string workType;
            public string why;
            public Func<Map, bool> waiting;
        }

        /// <summary>Work types and a cheap look at whether work for them is waiting on the map (lists and designations the game keeps anyway).</summary>
        private static readonly Waiting[] Checks =
        {
            new Waiting { workType = "Firefighter", why = "CC_Pnl_W_WhyFire", waiting = m => m.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Any(f => m.areaManager.Home[f.Position]) },
            new Waiting { workType = "Doctor", why = "CC_Pnl_W_WhyTend", waiting = m => m.mapPawns.FreeColonistsAndPrisonersSpawned.Any(p => p.health != null && p.health.HasHediffsNeedingTend()) },
            new Waiting { workType = "Construction", why = "CC_Pnl_W_WhyBuild",
                waiting = m => m.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Any(t => t.Faction == Faction.OfPlayer)
                               || m.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Any(t => t.Faction == Faction.OfPlayer) },
            new Waiting { workType = "PlantCutting", why = "CC_Pnl_W_WhyCut",
                waiting = m => m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.CutPlant) || m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.HarvestPlant) },
            new Waiting { workType = "Mining", why = "CC_Pnl_W_WhyMine", waiting = m => m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Mine) },
            new Waiting { workType = "Hunting", why = "CC_Pnl_W_WhyHunt", waiting = m => m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Hunt) },
            new Waiting { workType = "Handling", why = "CC_Pnl_W_WhyHandle",
                waiting = m => m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Tame) || m.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Slaughter) },
            new Waiting { workType = "Warden", why = "CC_Pnl_W_WhyWarden", waiting = m => m.mapPawns.PrisonersOfColonySpawnedCount > 0 },
            new Waiting { workType = "Hauling", why = "CC_Pnl_W_WhyHaul", waiting = m => m.listerHaulables.ThingsPotentiallyNeedingHauling().Count > 0 },
            new Waiting { workType = "Research", why = "CC_Pnl_W_WhyResearch", waiting = m => Find.ResearchManager?.GetProject() != null },
        };

        private static readonly Dictionary<string, WorkTypeDef> workTypes = new Dictionary<string, WorkTypeDef>();

        private static WorkTypeDef WorkType(string name)
        {
            if (!workTypes.TryGetValue(name, out var def))
            {
                def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                workTypes[name] = def;
            }
            return def;
        }

        private void Unmanned()
        {
            float t = Time.realtimeSinceStartup;
            if (t - unmannedAt >= 5f || t < unmannedAt)
            {
                unmannedAt = t;
                unmanned.Clear();
                var map = HudVitals.MapFor();
                if (map != null)
                {
                    var workers = map.mapPawns.FreeColonistsSpawned.Where(p => p.workSettings != null && p.workSettings.EverWork).ToList();
                    workers.AddRange(map.mapPawns.SlavesOfColonySpawned.Where(p => p.workSettings != null && p.workSettings.EverWork));
                    if (workers.Count > 0)
                        foreach (var check in Checks)
                        {
                            var wt = WorkType(check.workType);
                            if (wt == null) continue;
                            try
                            {
                                int on = 0;
                                foreach (var p in workers)
                                    if (!p.WorkTypeIsDisabled(wt) && p.workSettings.WorkIsActive(wt))
                                        on++;
                                // Only when nobody has it on is the waiting work looked for at all.
                                if (on > 0) continue;
                                if (HudWorkMath.Unmanned(check.waiting(map), on)) unmanned.Add((wt.labelShort.NullOrEmpty() ? wt.LabelCap.ToString() : wt.labelShort.CapitalizeFirst(), check.why));
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("work panel: work waiting (" + check.workType + ")", e);
                            }
                        }
                }
            }
            if (unmanned.Count == 0) return;
            string tip = string.Join("\n", unmanned.Select(u => u.label.Colorize(UiKit.Warn) + ": " + HudKit.T(u.why))) + "\n\n" +
                         HudKit.T("CC_Pnl_W_UnmannedClick").Colorize(ColoredText.SubtleGrayColor);
            colony.Add(new Line
            {
                text = "CC_Pnl_W_Unmanned".Translate(string.Join(", ", unmanned.Select(u => u.label))).Resolve(), score = 220f, icon = Icons.Priorities ?? Icons.Warning,
                tip = tip, click = OpenWorkTab
            });
        }

        // ---------------- Sizes ----------------

        private static readonly Color BarOk = new Color(0.62f, 0.72f, 0.62f, 0.9f);
        private static readonly Color BarLow = new Color(0.95f, 0.72f, 0.3f);

        private float RowH(float s) => Mathf.Round(20f * s);
        private float LineH(float s) => Mathf.Round(18f * s);
        private float Width(float s) => Mathf.Round((St.compact || !St.Shows(PartDoing) ? 176f : 256f) * s);

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            float top = colony.Count > 0 ? colony.Count * LineH(s) + Mathf.Round(4f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round(170f * s), pad * 2f + top + Mathf.Round(20f * s));
            return new Vector2(Width(s), pad * 2f + top + rows.Count * RowH(s) + more);
        }

        // ---------------- Drawing ----------------

        protected override void DrawBody(Rect r, float s)
        {
            var st = St;
            float pad = Mathf.Round(4f * s);
            float y = r.y + pad;
            var main = HudKit.Main(s);
            var minor = HudKit.Minor(s);
            // The colony part.
            if (colony.Count > 0)
            {
                float lh = LineH(s), icon = Mathf.Round(13f * s);
                foreach (var l in colony)
                {
                    var lr = new Rect(r.x + 2f, y, r.width - 4f, lh);
                    var c = HudKit.ColorOf(l.score, BarLow);
                    if (Over(lr)) Widgets.DrawBoxSolid(lr, UiKit.CardHover);
                    if (l.icon != null) UiKit.Icon(new Rect(lr.x + 2f, lr.y + (lh - icon) / 2f, icon, icon), l.icon, c);
                    HudKit.Label(new Rect(lr.x + icon + 6f, lr.y, lr.width - icon - 8f, lh), l.text, c, minor);
                    Tip(lr, l.tip);
                    if (Clicked(lr))
                    {
                        if (Event.current.button == 1) OpenMenu();
                        else
                            try
                            {
                                l.click?.Invoke();
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("work panel: click", e);
                            }
                    }
                    y += lh;
                }
                UiKit.Divider(r.x + pad, y + Mathf.Round(1f * s), r.width - pad * 2f);
                y += Mathf.Round(4f * s);
            }
            if (rows.Count == 0)
            {
                HudKit.Label(new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(20f * s)), HudKit.T("CC_Pnl_NoOne"), UiKit.Faint, minor);
                return;
            }
            float rh = RowH(s);
            bool doing = !st.compact && st.Shows(PartDoing);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var level = HudRank.LevelOf(row.score);
                if (level == HudLevel.Life) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Bad, 0.12f + 0.1f * HudKit.Pulse));
                else if (level == HudLevel.Soon) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Warn, 0.1f));
                else if (row.idle) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(BarLow, 0.08f));
                else UiKit.Zebra(rr, i);
                if (Over(rr)) Widgets.DrawBoxSolid(rr, UiKit.CardHover);
                float x = rr.x + Mathf.Round(4f * s);
                float right = rr.xMax - Mathf.Round(3f * s);
                // The three bars at the right end.
                right = Bars(rr, right, row, s, st);
                // The mark: idle and how long, drafted, down, a break.
                if (!row.mark.NullOrEmpty())
                {
                    float mw = Mathf.Min(HudKit.Width(row.mark, GameFont.Tiny) + 4f, rr.width * 0.3f);
                    HudKit.Label(new Rect(right - mw, rr.y, mw, rh), row.mark, row.markColor, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= mw + 3f;
                }
                // The name, then what they're doing.
                float nameW = doing ? Mathf.Min(Mathf.Round(66f * s), HudKit.Width(row.name, main) + 4f) : right - x;
                HudKit.Label(new Rect(x, rr.y, nameW, rh), row.name, level >= HudLevel.Life ? UiKit.Bad : Color.white, main);
                if (doing && !row.doing.NullOrEmpty())
                {
                    float dx = x + nameW + Mathf.Round(4f * s);
                    HudKit.Label(new Rect(dx, rr.y, Mathf.Max(0f, right - dx - 2f), rh), row.doing, row.idle ? BarLow : UiKit.Faint, minor);
                }
                var data = row;
                Tip(rr, () => data.tip, row.pawn.thingIDNumber ^ 0x48E2);
                if (Clicked(rr))
                {
                    if (Event.current.button == 1) OpenMenu();
                    else
                    {
                        HudPeople.Go(row.pawn);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y + rows.Count * rh, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                Tip(mr, HudKit.T("CC_Pnl_MoreLinesTip"));
            }
        }

        /// <summary>Food, rest and recreation as three thin upright bars, red when low; returns where they begin.</summary>
        private static float Bars(Rect rr, float right, Row row, float s, HudPanelState st)
        {
            float bw = Mathf.Max(3f, Mathf.Round(4f * s)), gap = Mathf.Max(1f, Mathf.Round(2f * s));
            float bh = rr.height - Mathf.Round(6f * s);
            void Bar(float level, bool low)
            {
                if (level < 0f) return;
                var b = new Rect(right - bw, rr.y + (rr.height - bh) / 2f, bw, bh);
                if (Event.current.type == EventType.Repaint)
                {
                    Widgets.DrawBoxSolid(b, UiKit.MeterBack);
                    float fh = Mathf.Max(1f, b.height * Mathf.Clamp01(level));
                    Widgets.DrawBoxSolid(new Rect(b.x, b.yMax - fh, b.width, fh), low ? UiKit.Bad : BarOk);
                }
                right -= bw + gap;
            }
            // Right to left: recreation, rest, food, so they read food, rest, recreation.
            if (st.Shows(PartJoy)) Bar(row.joy, row.joyLow);
            if (st.Shows(PartRest)) Bar(row.rest, row.restLow);
            if (st.Shows(PartFood)) Bar(row.food, row.foodLow);
            return right - Mathf.Round(2f * s);
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var st = St;
            bool drafted = st.Shows(PartDrafted);
            options.Add(new FloatMenuOption(HudKit.T(drafted ? "CC_Pnl_W_HideDrafted" : "CC_Pnl_W_ShowDrafted"), () =>
            {
                st.Toggle(PartDrafted);
                MarkDirty();
                HudLayout.Save();
            }));
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_W_OpenWork"), OpenWorkTab));
        }

        /// <summary>The game's Work tab (by name: it has no DefOf).</summary>
        private static void OpenWorkTab()
        {
            var tab = DefDatabase<MainButtonDef>.GetNamedSilentFail("Work");
            if (tab != null) Find.MainTabsRoot.SetCurrentTab(tab);
        }
    }
}
