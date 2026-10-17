using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// History → Calendar: the days that matter to this colony, soonest first, how each is kept and how close it is to
    /// a tradition; the seasonal customs from the council's dilemmas; and a button to name a day. Self-contained: the
    /// council window only calls <see cref="Draw"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CalendarPanel
    {
        public static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/Calendar", false);

        private static Vector2 scroll;
        private static float height = 600f;

        private static readonly Color FeastColor = new Color(1f, 0.83f, 0.47f);
        private static readonly Color RestColor = new Color(0.56f, 0.73f, 0.96f);
        private static readonly Color GamesColor = new Color(0.52f, 0.85f, 0.5f);
        private static readonly Color BonfireColor = new Color(1f, 0.6f, 0.35f);
        private static readonly Color GiftsColor = new Color(0.9f, 0.62f, 0.85f);
        private static readonly Color HuntColor = new Color(0.72f, 0.66f, 0.45f);
        private static readonly Color PlantingColor = new Color(0.62f, 0.8f, 0.42f);

        public static Color WayColor(DayWay way)
        {
            switch (way)
            {
                case DayWay.Feast: return FeastColor;
                case DayWay.Remembrance: return UiKit.LossColor;
                case DayWay.Rest: return RestColor;
                case DayWay.Games: return GamesColor;
                case DayWay.Bonfire: return BonfireColor;
                case DayWay.Gifts: return GiftsColor;
                case DayWay.Hunt: return HuntColor;
                case DayWay.Planting: return PlantingColor;
                default: return UiKit.Faint;
            }
        }

        public static Texture2D KindIcon(CalendarDay d)
        {
            switch (d.kind)
            {
                case CalendarKind.Founding: return Icons.Colony;
                case CalendarKind.Harvest: return Icons.CatFood;
                case CalendarKind.FirstSnow: return Icons.Dreams;
                case CalendarKind.Battle: return d.id != null && (d.id.StartsWith("nemesis|") || d.id.StartsWith("return|")) ? Icons.Nemesis : Icons.Raid;
                case CalendarKind.Death: return Icons.Death;
                case CalendarKind.Wedding: return Icons.Wedding;
                case CalendarKind.Arrival: return Icons.Visitor;
                case CalendarKind.Birthday: return Icons.Birth;
                case CalendarKind.FirstChild: return Icons.Birth;
                case CalendarKind.Walls: return Icons.CatDefense;
                case CalendarKind.Homecoming: return Icons.WorldEvent;
                case CalendarKind.NewCrop: return Icons.CatFood;
                case CalendarKind.FirstTrade: return Icons.Trader;
                case CalendarKind.GreatHunt: return Icons.CatLowTech;
                case CalendarKind.Foal: return Icons.Birth;
                case CalendarKind.Companion: return Icons.Death;
                default: return Icon ?? Icons.Chronicle;
            }
        }

        public static void Draw(Rect rect)
        {
            var cal = CalendarTracker.Instance;
            var map = HappeningTracker.HomeMap();
            if (cal == null)
            {
                UiKit.EmptyState(rect, Icon, "CC_NoGame".Translate());
                return;
            }
            int today = map != null ? GenLocalDate.DayOfYear(map) : 0;
            string stat = map != null ? "CC_Cal_Today".Translate(CalendarTracker.DateText(today, map)).Resolve() : null;
            const float buttonW = 150f;
            float top = UiKit.PageHeader(rect, Icon, "CC_Cal_Title".Translate(), "CC_UI_Sub_Calendar".Translate(), stat, null, buttonW + 10f);
            var button = new Rect(rect.xMax - buttonW, rect.y + 6f, buttonW, 30f);
            bool full = cal.days.Count(d => d.byPlayer) >= CalendarMath.MaxNamedDays;
            if (Widgets.ButtonText(button, "CC_Cal_NameDay".Translate(), active: !full && map != null))
                Find.WindowStack.Add(new Dialog_CalendarDay(null));
            TooltipHandler.TipRegion(button, full ? "CC_Cal_NameDay_Full".Translate(CalendarMath.MaxNamedDays) : "CC_Cal_NameDay_Tip".Translate());

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;
            if (!CalendarTracker.On) y += UiKit.Paragraph(0f, y, w, "CC_Cal_Off".Translate(), UiKit.Warn) + 8f;
            y += UiKit.Paragraph(0f, y, w, "CC_Cal_Intro".Translate(CalendarMath.TraditionAfter), UiKit.Faint) + 10f;

            // The days, soonest first; the first snow, which has no date, last.
            var rows = UiCache.Get(cal, 4101, () => Rows(cal, map, today));
            y += UiKit.SectionTitle(0f, y, w, "CC_Cal_Days".Translate(), Icon, null, "CC_Cal_DaysCount".Translate(rows.Count).Resolve());
            if (rows.Count == 0) y += UiKit.EmptyState(0f, y, w, Icon, "CC_Cal_NoDays".Translate(), 80f);
            int index = 0;
            foreach (var r in rows) y += DrawDay(r, 0f, y, w, index++);
            y += 14f;

            // The council's seasonal customs (traditions grown from the dilemmas).
            var customs = UiCache.Get(cal, 4102, Customs);
            y += UiKit.SectionTitle(0f, y, w, "CC_Cal_Customs".Translate(), Icons.Ways);
            if (customs.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, "CC_Cal_NoCustoms".Translate(), UiKit.Faint) + 6f;
            foreach (var (label, tip) in customs)
            {
                var r = new Rect(0f, y, w, 26f);
                UiKit.Zebra(r, index++);
                UiKit.Icon(new Rect(4f, y + 4f, 16f, 16f), Icons.Ways, UiKit.Gold);
                UiKit.LabelFit(new Rect(26f, y, w - 30f, 26f), label, Color.white);
                TooltipHandler.TipRegion(r, tip);
                y += 26f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        /// <summary>A day as the page shows it, its words worked out once a second rather than every frame.</summary>
        private class Row
        {
            public CalendarDay day;
            public string date, when, name, origin, way, wayTip, status, statusTip;
            public Color statusColor;
            public bool soon;
        }

        private static List<Row> Rows(CalendarTracker cal, Map map, int today)
        {
            var list = new List<Row>();
            foreach (var d in cal.days.OrderBy(d => d.kind == CalendarKind.FirstSnow ? 999 : CalendarMath.DaysUntil(today, d.day)).ThenByDescending(d => d.Standing))
            {
                int until = CalendarMath.DaysUntil(today, d.day);
                bool snow = d.kind == CalendarKind.FirstSnow;
                var r = new Row
                {
                    day = d,
                    soon = !snow && until <= 5,
                    date = snow ? "CC_Cal_WithSnow".Translate().ToString() : CalendarTracker.DateText(d.day, map),
                    when = snow ? null
                        : until == 0 ? "CC_Today".Translate().ToString().CapitalizeFirst()
                        : until == 1 ? "CC_Cal_Tomorrow".Translate().ToString()
                        : "CC_Cal_InDays".Translate(until).Resolve(),
                    name = d.label.CapitalizeFirst(),
                    origin = Origin(d),
                    way = CalendarTracker.WayLabel(d.way),
                    wayTip = ("CC_Cal_Way_" + d.way + "_Desc").Translate()
                };
                if (d.tradition)
                {
                    r.status = "CC_Cal_Pill_Tradition".Translate();
                    r.statusTip = "CC_Cal_Status_Tradition".Translate(d.timesKept).Resolve();
                    r.statusColor = UiKit.Gold;
                }
                else if (!d.Personal && d.keptYears > 0)
                {
                    r.status = "CC_Cal_Pill_Kept".Translate(d.keptYears).Resolve();
                    r.statusTip = "CC_Cal_Pill_Kept_Tip".Translate(CalendarMath.YearsToTradition(d.keptYears, false)).Resolve();
                    r.statusColor = UiKit.Good;
                }
                list.Add(r);
            }
            return list;
        }

        private static List<(string label, string tip)> Customs()
        {
            var list = new List<(string, string)>();
            var traditions = CouncilGameComponent.Instance?.dilemmas?.Traditions;
            if (traditions == null) return list;
            foreach (var c in traditions)
            {
                if (c.traditionLabel.NullOrEmpty()) continue;
                string when = c.seasons.NullOrEmpty() ? "" : " (" + TextUtil.JoinAnd(c.seasons.Select(s => s.Label()).Distinct().ToList()) + ")";
                list.Add((c.traditionLabel.CapitalizeFirst() + when, "CC_Cal_Custom_Tip".Translate(c.label).Resolve()));
            }
            return list;
        }

        private static float DrawDay(Row row, float x, float y, float w, int index)
        {
            var d = row.day;
            const float dateW = 130f;
            const float menuW = 30f;
            float nx = x + 32f + dateW + 8f;
            float nw = w - (nx - x) - menuW - 8f;
            Text.Font = GameFont.Tiny;
            float oh = row.origin.NullOrEmpty() ? 0f : Text.CalcHeight(row.origin, nw);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(48f, 28f + oh + 6f);
            var r = new Rect(x, y, w, h);
            UiKit.Zebra(r, index);
            Widgets.DrawHighlightIfMouseover(r);
            UiKit.Icon(new Rect(x + 6f, y + 6f, 20f, 20f), KindIcon(d), row.soon ? UiKit.Gold : UiKit.Faint);

            // The date column.
            UiKit.LabelFit(new Rect(x + 32f, y + 3f, dateW, 22f), row.date, row.soon ? UiKit.Gold : UiKit.Faint);
            if (row.when != null) UiKit.LabelFit(new Rect(x + 32f, y + 23f, dateW, 18f), row.when, UiKit.Dim, GameFont.Tiny);

            // The name, how it's kept, and how close it is to a tradition.
            Text.Font = GameFont.Small;
            float nameW = Mathf.Min(Text.CalcSize(row.name).x + 4f, nw * 0.55f);
            UiKit.LabelFit(new Rect(nx, y + 3f, nameW, 22f), row.name, Color.white);
            float px = nx + nameW + 8f;
            px += UiKit.Pill(px, y + 5f, row.way, WayColor(d.way), null, row.wayTip) + 4f;
            if (row.status != null) px += UiKit.Pill(px, y + 5f, row.status, row.statusColor, d.tradition ? Icons.Ways : null, row.statusTip) + 4f;
            if (d.byPlayer) UiKit.Pill(px, y + 5f, "CC_Cal_Pill_Named".Translate(), UiKit.Info);
            if (oh > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(nx, y + 27f, nw, oh + 2f), row.origin);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // What can be done with it.
            var menu = new Rect(x + w - menuW - 4f, y + 6f, menuW, 24f);
            if (Widgets.ButtonText(menu, "...")) Find.WindowStack.Add(new FloatMenu(Options(d)));
            TooltipHandler.TipRegion(menu, "CC_Cal_Menu_Tip".Translate());
            return h;
        }

        private static List<FloatMenuOption> Options(CalendarDay d)
        {
            var cal = CalendarTracker.Instance;
            var list = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_Cal_Menu_Change".Translate(), () => Find.WindowStack.Add(new Dialog_CalendarDay(d)))
            };
            if (d.kind != CalendarKind.Founding)
            {
                string label = (d.byPlayer ? "CC_Cal_Menu_Remove" : "CC_Cal_Menu_Forget").Translate();
                list.Add(new FloatMenuOption(label, () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Cal_Menu_Confirm".Translate(d.label), () =>
                    {
                        cal?.Remove(d);
                        UiCache.Clear();
                    }, true));
                }));
            }
            return list;
        }

        /// <summary>Where a day comes from, in a line under its name.</summary>
        public static string Origin(CalendarDay d)
        {
            switch (d.kind)
            {
                case CalendarKind.Founding: return "CC_Cal_Origin_Founding".Translate(d.sinceYear).Resolve();
                case CalendarKind.Harvest: return "CC_Cal_Origin_Harvest".Translate(d.sinceYear).Resolve();
                case CalendarKind.FirstSnow: return "CC_Cal_Origin_FirstSnow".Translate(CalendarTracker.DateText(d.day), d.sinceYear).Resolve();
                case CalendarKind.Battle:
                    if (d.id != null && d.id.StartsWith("nemesis|")) return "CC_Cal_Origin_Nemesis".Translate(d.enemy ?? "?", d.sinceYear).Resolve();
                    if (d.id != null && d.id.StartsWith("return|")) return "CC_Cal_Origin_NemesisReturn".Translate(d.enemy ?? "?", d.sinceYear).Resolve();
                    return d.fallen.Count > 0
                        ? "CC_Cal_Origin_BattleCostly".Translate(d.enemy ?? "?", d.sinceYear, d.kills, TextUtil.JoinAnd(d.fallen)).Resolve()
                        : "CC_Cal_Origin_Battle".Translate(d.enemy ?? "?", d.sinceYear, d.kills).Resolve();
                case CalendarKind.Death: return "CC_Cal_Origin_Death".Translate(d.pawnName ?? "?", d.sinceYear).Resolve();
                case CalendarKind.Wedding: return "CC_Cal_Origin_Wedding".Translate(d.pawnName ?? "?", d.otherName ?? "?", d.sinceYear).Resolve();
                case CalendarKind.Arrival: return "CC_Cal_Origin_Arrival".Translate(d.pawnName ?? "?", d.sinceYear).Resolve();
                case CalendarKind.Birthday: return "CC_Cal_Origin_Birthday".Translate(d.pawnName ?? "?").Resolve();
                case CalendarKind.FirstChild: return "CC_Cal_Origin_FirstChild".Translate(d.pawnName ?? "?", d.sinceYear).Resolve();
                case CalendarKind.Walls: return "CC_Cal_Origin_Walls".Translate(d.sinceYear).Resolve();
                case CalendarKind.Homecoming: return "CC_Cal_Origin_Homecoming".Translate(d.subject ?? d.pawnName ?? "?", d.sinceYear, d.kills).Resolve();
                case CalendarKind.NewCrop: return "CC_Cal_Origin_NewCrop".Translate(d.subject ?? "?", d.sinceYear).Resolve();
                case CalendarKind.FirstTrade: return "CC_Cal_Origin_FirstTrade".Translate(d.sinceYear).Resolve();
                case CalendarKind.GreatHunt: return "CC_Cal_Origin_GreatHunt".Translate(d.sinceYear, d.kills, d.subject ?? "?").Resolve();
                case CalendarKind.Foal: return "CC_Cal_Origin_Foal".Translate(d.subject ?? "?", d.sinceYear, d.pawnName ?? "?").Resolve();
                case CalendarKind.Companion: return "CC_Cal_Origin_Companion".Translate(d.pawnName ?? "?", d.sinceYear, d.otherName ?? "?", d.subject ?? "?").Resolve();
                default: return "CC_Cal_Origin_Holiday".Translate(d.sinceYear).Resolve();
            }
        }
    }

    /// <summary>
    /// Naming a day, or changing one: its name, how the colony keeps it, and (for the player's own days) its date. A
    /// birthday or an arrival keeps its own way; any other day can be kept as a feast, a remembrance, a rest day, games, a
    /// bonfire, a day of gifts or a hunt.
    /// </summary>
    public class Dialog_CalendarDay : Window
    {
        public const int MaxNameLength = 40;

        private readonly CalendarDay editing;
        private string name;
        private DayWay way;
        private int quadrum;
        private int dayOfQuadrum;
        private bool focused;
        private bool finished;

        private static readonly DayWay[] Ways = { DayWay.Feast, DayWay.Remembrance, DayWay.Rest, DayWay.Games, DayWay.Bonfire, DayWay.Gifts, DayWay.Hunt, DayWay.Planting };

        public Dialog_CalendarDay(CalendarDay editing)
        {
            this.editing = editing;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnAccept = false;
            var map = HappeningTracker.HomeMap();
            int day = editing?.day ?? (map != null ? GenLocalDate.DayOfYear(map) : 0);
            CalendarMath.Split(day, out quadrum, out dayOfQuadrum);
            name = editing?.label ?? "";
            way = editing?.way ?? DayWay.Feast;
        }

        private bool DateEditable => editing == null || editing.byPlayer;

        private bool WayEditable => editing == null || !editing.Personal;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, 34f), editing == null ? "CC_Cal_Dialog_New".Translate() : "CC_Cal_Dialog_Change".Translate(editing.label.CapitalizeFirst()));
            Text.Font = GameFont.Small;
            float y = 40f;
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "CC_Cal_Dialog_Name".Translate());
            y += 24f;
            GUI.SetNextControlName("RimfolkCalendarName");
            name = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), name, MaxNameLength);
            if (!focused)
            {
                UI.FocusControl("RimfolkCalendarName", this);
                focused = true;
            }
            y += 38f;

            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "CC_Cal_Dialog_Way".Translate());
            y += 26f;
            if (WayEditable)
            {
                foreach (var wy in Ways)
                {
                    var r = new Rect(0f, y, inRect.width, 26f);
                    if (Widgets.RadioButtonLabeled(r, CalendarTracker.WayLabel(wy).CapitalizeFirst() + ": " + ("CC_Cal_Way_" + wy + "_Desc").Translate(), way == wy))
                        way = wy;
                    y += 28f;
                }
            }
            else
            {
                UiKit.Paragraph(0f, y, inRect.width, "CC_Cal_Dialog_Personal".Translate(), UiKit.Faint);
                y += 40f;
            }
            y += 6f;

            var map = HappeningTracker.HomeMap();
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "CC_Cal_Dialog_Date".Translate());
            y += 26f;
            if (DateEditable && editing?.kind != CalendarKind.FirstSnow)
            {
                float bw = (inRect.width - 12f) / 4f;
                for (int q = 0; q < 4; q++)
                {
                    var r = new Rect(q * (bw + 4f), y, bw, 28f);
                    string label = ((Quadrum)q).Label();
                    if (map != null) label += " (" + ((Quadrum)q).GetSeason(Find.WorldGrid.LongLatOf(map.Tile).y).Label() + ")";
                    if (UiKit.TabButton(r, label, null, q == quadrum)) quadrum = q;
                }
                y += 34f;
                float v = Widgets.HorizontalSlider(new Rect(0f, y + 6f, inRect.width - 170f, 20f), dayOfQuadrum, 1f, CalendarMath.DaysPerQuadrum, true, null, null, null, 1f);
                dayOfQuadrum = Mathf.Clamp(Mathf.RoundToInt(v), 1, CalendarMath.DaysPerQuadrum);
                UiKit.LabelFit(new Rect(inRect.width - 160f, y, 160f, 30f), CalendarTracker.DateText(CalendarMath.Join(quadrum, dayOfQuadrum), map), UiKit.Gold);
                y += 34f;
            }
            else
            {
                string date = editing?.kind == CalendarKind.FirstSnow ? "CC_Cal_WithSnow".Translate().ToString() : CalendarTracker.DateText(editing?.day ?? 0, map);
                UiKit.Paragraph(0f, y, inRect.width, "CC_Cal_Dialog_DateFixed".Translate(date).Resolve(), UiKit.Faint);
                y += 30f;
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 80f, inRect.height - 38f, 160f, 34f), (editing == null ? "CC_Cal_Dialog_Add" : "CC_Cal_Dialog_Save").Translate())
                || KeyBindingDefOf.Accept.KeyDownEvent)
                Accept();
        }

        public override void OnAcceptKeyPressed() => Accept();

        private void Accept()
        {
            if (finished) return;
            string n = (name ?? "").Trim();
            if (n.NullOrEmpty())
            {
                Messages.Message("CC_Cal_Dialog_NeedName".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var cal = CalendarTracker.Instance;
            if (cal == null) return;
            if (editing == null)
            {
                var d = cal.AddNamed(n, way, CalendarMath.Join(quadrum, dayOfQuadrum));
                if (d == null)
                {
                    Messages.Message("CC_Cal_NameDay_Full".Translate(CalendarMath.MaxNamedDays), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                Messages.Message("CC_Cal_Dialog_Added".Translate(d.label, CalendarTracker.DateText(d.day)).Resolve(), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                editing.label = n;
                if (WayEditable) editing.way = way;
                if (DateEditable && editing.kind != CalendarKind.FirstSnow)
                {
                    int day = CalendarMath.Join(quadrum, dayOfQuadrum);
                    if (day != editing.day)
                    {
                        // Moved: it comes on its new date, once this year at most.
                        var map = HappeningTracker.HomeMap();
                        if (map != null && editing.lastYear < GenLocalDate.Year(map))
                            editing.lastYear = CalendarMath.InitialLastYear(GenLocalDate.DayOfYear(map), GenLocalDate.Year(map), day, GenLocalDate.HourOfDay(map) <= 18);
                        editing.day = day;
                    }
                }
            }
            finished = true;
            UiCache.Clear();
            Close();
        }
    }
}
