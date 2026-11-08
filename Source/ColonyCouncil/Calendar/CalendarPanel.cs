using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → Calendar: the colony's festive year in one place, its days and the council's customs it keeps, soonest
    /// first, how each is kept, when it next comes and how close it is to a tradition; and a button to name a day.
    /// Self-contained: the council window only calls <see cref="Draw"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CalendarPanel
    {
        public static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/Calendar", false);

        private static Vector2 scroll;
        private static float height = 600f;

        /// <summary>
        /// How a day is kept, by the interface's tokens (UI/UiKit.cs): the colony's own ways of keeping a day gold, a day of
        /// remembrance quiet. The way's word on its tag says which it is; colour carries meaning only.
        /// </summary>
        public static Color WayColor(DayWay way)
        {
            switch (way)
            {
                case DayWay.Remembrance: return UiKit.Faint;
                case DayWay.Feast:
                case DayWay.Rest:
                case DayWay.Games:
                case DayWay.Bonfire:
                case DayWay.Gifts:
                case DayWay.Hunt:
                case DayWay.Planting:
                case DayWay.Faith:
                case DayWay.Walk:
                case DayWay.Custom: return UiKit.Gold;
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
                case CalendarKind.CampGift: return Icons.Visitor;
                case CalendarKind.Situation: return Icons.WorldEvent;
                case CalendarKind.FaithDay: return Icons.CatIdeology;
                case CalendarKind.Rescue: return Icons.Heart;
                case CalendarKind.Custom: return Icons.Ways;
                case CalendarKind.Parting: return Icons.Colony;
                default: return Icon ?? Icons.Chronicle;
            }
        }

        public static void Draw(Rect rect)
        {
            // The calendar of the colony in view (its own days, kept at its own home).
            var cal = ColonyCalendar.InView;
            var map = cal?.HomeMap;
            if (cal == null)
            {
                UiKit.EmptyState(rect, Icon, "CC_NoGame".Translate());
                return;
            }
            int today = map != null ? GenLocalDate.DayOfYear(map) : 0;
            string stat = map != null ? "CC_Cal_Today".Translate(ColonyCalendar.DateText(today, map)).Resolve() : null;
            const float buttonW = 150f;
            float top = UiKit.PageHeader(rect, Icon, "CC_Cal_Title".Translate(), "CC_UI_Sub_Calendar".Translate(), stat, null, buttonW + 10f);
            var button = new Rect(rect.xMax - buttonW, rect.y + 6f, buttonW, UiKit.RowStandard);
            bool full = cal.days.Count(d => d.byPlayer) >= CalendarMath.MaxNamedDays;
            // Greyed with why not (the calendar is full) rather than refused.
            string whyNot = full ? "CC_Cal_NameDay_Full".Translate(CalendarMath.MaxNamedDays).ToString() : map == null ? "CC_NoHomeMap".Translate().ToString() : null;
            if (UiKit.ActButton(button, "CC_Cal_NameDay".Translate(), whyNot, "CC_Cal_NameDay_Tip".Translate()))
                Find.WindowStack.Add(new Dialog_CalendarDay(null, cal));

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;
            if (!ColonyCalendar.On) y += UiKit.Paragraph(0f, y, w, "CC_Cal_Off".Translate(), UiKit.Soon) + 8f;
            y += UiKit.Paragraph(0f, y, w, "CC_Fest_Intro".Translate(CalendarMath.TraditionAfter.Named("YEARS")), UiKit.Faint) + 10f;

            // The days, soonest first; the first snow, which has no date, last.
            var rows = UiCache.Get(cal, 4101, () => Rows(cal, map, today));
            y += UiKit.SectionTitle(0f, y, w, "CC_Cal_Days".Translate(), rows.Count, Icon);
            int index = 0;
            // Election day, while an election is under way: once, not every year (Offices/OfficesElection.cs).
            y += DrawElection(0f, y, w, index);
            if (ColonyView.Current?.offices?.election != null) index++;
            // A feast with guests, set or to plan (Diplomacy/FeastPanels.cs).
            y += FeastPanels.CalendarRow(0f, y, w, ref index, map);
            if (rows.Count == 0) y += UiKit.EmptyState(0f, y, w, Icon, "CC_Cal_NoDays".Translate(), 80f);
            // The council's customs are days among the rest (Calendar/CalendarCustoms.cs); an older save's not yet carried over
            // are on it within the hour.
            foreach (var r in rows) y += DrawDay(r, 0f, y, w, index++);
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        /// <summary>A day as the page shows it, its words worked out once a second rather than every frame.</summary>
        private class Row
        {
            public CalendarDay day;
            public string date, when, name, origin, way, wayTip, status, statusTip;
            /// <summary>The name and where it came from as drawn: the colony's people they name as links.</summary>
            public string nameShown, originShown;
            public Color statusColor;
            public bool soon;
        }

        private static List<Row> Rows(ColonyCalendar cal, Map map, int today)
        {
            var list = new List<Row>();
            var people = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            // The days with a date soonest first; the first snow, and customs that come with their occasion, last.
            foreach (var d in cal.days.OrderBy(d => d.Dated ? CalendarMath.DaysUntil(today, d.day) : d.kind == CalendarKind.FirstSnow ? 998 : 999).ThenByDescending(d => d.Standing))
            {
                int until = CalendarMath.DaysUntil(today, d.day);
                bool snow = !d.Dated;
                var r = new Row
                {
                    day = d,
                    soon = !snow && until <= 5,
                    date = d.kind == CalendarKind.FirstSnow ? "CC_Cal_WithSnow".Translate().ToString() : !d.Dated ? "CC_Fest_WithOccasion".Translate().ToString() : ColonyCalendar.DateText(d.day, map),
                    when = snow ? null
                        : until == 0 ? "CC_Today".Translate().ToString().CapitalizeFirst()
                        : until == 1 ? "CC_Cal_Tomorrow".Translate().ToString()
                        : "CC_Cal_InDays".Translate(until).Resolve(),
                    name = d.label.CapitalizeFirst(),
                    origin = Origin(d),
                    way = ColonyCalendar.WayLabel(d.way) + (d.way == DayWay.Faith && d.kind == CalendarKind.Holiday && !d.subject.NullOrEmpty() ? ": " + d.subject : ""),
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
                    r.statusColor = UiKit.Done;
                }
                r.nameShown = PaneLinks.Mark(r.name, people);
                r.originShown = PaneLinks.Mark(r.origin, people);
                list.Add(r);
            }
            return list;
        }

        /// <summary>Election day as a row of its own at the top of the days: its date, what it is, and a click to the Decrees page.</summary>
        private static float DrawElection(float x, float y, float w, int index)
        {
            // The election of the colony in view.
            var e = ColonyView.Current?.offices?.election;
            if (e == null || !ColonyCouncilMod.Settings.offices) return 0f;
            const float dateW = 130f;
            var r = new Rect(x, y, w, 48f);
            UiKit.Zebra(r, index);
            Widgets.DrawHighlightIfMouseover(r);
            UiKit.Icon(new Rect(x + 6f, y + 6f, 20f, 20f), Icons.Office, UiKit.Gold);
            UiKit.LabelFit(new Rect(x + 32f, y + 3f, dateW, 22f), OfficeTracker.ElectionDay(e), UiKit.Gold);
            float nx = x + 32f + dateW + 8f;
            string name = (e.ceremony ? "CC_Elect_Cal_Ceremony" : "CC_Elect_Cal_Day").Translate();
            UiKit.LabelFit(new Rect(nx, y + 3f, w - nx - 8f, 22f), name, UiKit.Ink);
            UiKit.LabelFit(new Rect(nx, y + 25f, w - nx - 8f, 18f), (e.ceremony ? "CC_Elect_Cal_CeremonyNote" : "CC_Elect_Cal_Note").Translate(), UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, "CC_Elect_Cal_Tip".Translate());
            if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenTab("Decrees");
            return 48f;
        }

        private static float DrawDay(Row row, float x, float y, float w, int index)
        {
            var d = row.day;
            const float dateW = 130f;
            const float menuW = 30f;
            float nx = x + 32f + dateW + 8f;
            float nw = w - (nx - x) - menuW - 8f;
            float oh = row.origin.NullOrEmpty() ? 0f : LinkText.Height(row.originShown ?? row.origin, nw, GameFont.Tiny);
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
            LinkText.Line(new Rect(nx, y + 3f, nameW, 22f), row.nameShown ?? row.name, UiKit.Ink);
            float px = nx + nameW + 8f;
            px += UiKit.Pill(px, y + 5f, row.way, WayColor(d.way), null, row.wayTip) + 4f;
            if (row.status != null) px += UiKit.Pill(px, y + 5f, row.status, row.statusColor, d.tradition ? Icons.Ways : null, row.statusTip) + 4f;
            if (d.byPlayer) UiKit.Pill(px, y + 5f, "CC_Cal_Pill_Named".Translate(), UiKit.Gold);
            if (oh > 0f) LinkText.Draw(new Rect(nx, y + 27f, nw, oh + 2f), row.originShown ?? row.origin, UiKit.Faint, GameFont.Tiny);

            // What can be done with it: the More icon's menu.
            var menu = new Rect(x + w - menuW - 4f, y + 6f, 24f, 24f);
            if (UiKit.IconButton(menu, Icons.More, "CC_Cal_Menu_Tip".Translate())) Find.WindowStack.Add(new FloatMenu(Options(d)));
            return h;
        }

        private static List<FloatMenuOption> Options(CalendarDay d)
        {
            var cal = ColonyCalendar.InView;
            var list = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_Cal_Menu_Change".Translate(), () => Find.WindowStack.Add(new Dialog_CalendarDay(d, cal)))
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
                case CalendarKind.FirstSnow: return "CC_Cal_Origin_FirstSnow".Translate(ColonyCalendar.DateText(d.day), d.sinceYear).Resolve();
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
                case CalendarKind.CampGift: return "CC_Cal_Origin_CampGift".Translate(d.enemy ?? "?", d.sinceYear, d.subject ?? "?", d.kills).Resolve();
                case CalendarKind.Situation: return "CC_Cal_Origin_Situation".Translate(d.subject ?? "?", d.sinceYear).Resolve().CapitalizeFirst();
                case CalendarKind.FaithDay: return "CC_Cal_Origin_FaithDay".Translate(d.subject ?? "?", d.otherName ?? "?").Resolve();
                case CalendarKind.Rescue: return "CC_Cal_Origin_Rescue".Translate(d.pawnName ?? "?", d.sinceYear, d.kills, d.enemy ?? "?").Resolve();
                case CalendarKind.Custom:
                {
                    string origin = (d.moment ? "CC_Fest_Origin_CustomMoment" : "CC_Fest_Origin_Custom").Translate(d.sinceYear.Named("YEAR")).Resolve();
                    string answer = ColonyCalendar.KeepingAnswer(d.CustomDef);
                    return answer == null ? origin : origin + " " + "CC_Fest_Origin_Answer".Translate(answer.Named("ANSWER")).Resolve();
                }
                case CalendarKind.Parting: return ColonyCalendar.PartingOrigin(d);
                default: return "CC_Cal_Origin_Holiday".Translate(d.sinceYear).Resolve();
            }
        }
    }

    /// <summary>
    /// Naming a day, or changing one: its name, how the colony keeps it, and (for the player's own days) its date. A
    /// birthday or an arrival keeps its own way; any other day can be kept as a feast, a remembrance, a rest day, games, a
    /// bonfire, a day of gifts or a hunt.
    /// </summary>
    public class Dialog_CalendarDay : FramedWindow
    {
        public const int MaxNameLength = 40;

        private readonly CalendarDay editing;
        /// <summary>The calendar the day is on (the colony in view when the dialog opened).</summary>
        private readonly ColonyCalendar cal;
        private string name;
        private DayWay way;
        private int quadrum;
        private int dayOfQuadrum;
        private bool focused;
        private bool finished;

        private static readonly DayWay[] AllWays = { DayWay.Feast, DayWay.Remembrance, DayWay.Rest, DayWay.Games, DayWay.Bonfire, DayWay.Gifts, DayWay.Hunt, DayWay.Planting, DayWay.Walk, DayWay.Faith };

        /// <summary>
        /// The ways a day can be kept here: an evening of faith only with faith in the colony (Ideology), and only for a
        /// day the player names (the day keeps its faith in place of what a colony's own day is about).
        /// </summary>
        private IEnumerable<DayWay> Ways => AllWays.Where(w => w != DayWay.Faith || (Faiths.On && (editing == null || editing.kind == CalendarKind.Holiday || editing.kind == CalendarKind.FaithDay)));

        /// <summary>The faith a day kept as an evening of faith speaks for (by name, as the day keeps it).</summary>
        private string faithName;

        protected override string FrameTitle => editing == null ? "CC_Cal_Dialog_New".Translate().Resolve() : "CC_Cal_Dialog_Change".Translate(editing.label.CapitalizeFirst()).Resolve();

        protected override Texture2D FrameIcon => CalendarPanel.Icon ?? Icons.Chronicle;

        /// <summary>In Rimfolk's frame (key "CalendarDay"), its title in the title bar.</summary>
        public Dialog_CalendarDay(CalendarDay editing, ColonyCalendar cal) : base("CalendarDay")
        {
            this.editing = editing;
            this.cal = cal;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnAccept = false;
            var map = cal?.HomeMap;
            int day = editing?.day ?? (map != null ? GenLocalDate.DayOfYear(map) : 0);
            CalendarMath.Split(day, out quadrum, out dayOfQuadrum);
            name = editing?.label ?? "";
            way = editing?.way ?? DayWay.Feast;
            faithName = editing?.way == DayWay.Faith ? editing.subject : null;
            if (faithName.NullOrEmpty() && Faiths.On) faithName = Faiths.Census().FirstOrDefault()?.ideo?.name;
        }

        private bool DateEditable => editing == null || editing.byPlayer;

        /// <summary>Not a birthday's or an arrival's, nor a custom's: its question's answers keep it.</summary>
        private bool WayEditable => editing == null || !editing.Personal && !editing.Custom;

        public override Vector2 InitialSize => new Vector2(560f, 630f);

        protected override void DoFramedContents(Rect inRect)
        {
            float y = TitleTop(inRect, FrameTitle, 30f, 40f);
            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "CC_Cal_Dialog_Name".Translate());
            y += 24f;
            name = UiKit.TextField(new Rect(0f, y, inRect.width, 30f), name, "RimfolkCalendarName", null, MaxNameLength);
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
                    if (Widgets.RadioButtonLabeled(r, ColonyCalendar.WayLabel(wy).CapitalizeFirst() + ": " + ("CC_Cal_Way_" + wy + "_Desc").Translate(), way == wy))
                        way = wy;
                    y += 28f;
                }
                // An evening of faith: which faith it's for (the colony's faiths, most believers first).
                if (way == DayWay.Faith && Faiths.On)
                {
                    var faiths = Faiths.Census().Where(f => f?.ideo != null).ToList();
                    float fx = 24f;
                    foreach (var f in faiths)
                    {
                        string label = f.ideo.name;
                        float bw = Mathf.Min(Text.CalcSize(label).x + 24f, inRect.width - 24f);
                        if (fx > 24f && fx + bw > inRect.width)
                        {
                            fx = 24f;
                            y += 30f;
                        }
                        if (UiKit.TabButton(new Rect(fx, y, bw, 26f), label, null, faithName == label)) faithName = label;
                        fx += bw + 4f;
                    }
                    if (faiths.Count > 0) y += 30f;
                }
            }
            else
            {
                UiKit.Paragraph(0f, y, inRect.width, (editing.Custom ? "CC_Fest_Dialog_Custom" : "CC_Cal_Dialog_Personal").Translate(), UiKit.Faint);
                y += 40f;
            }
            y += 6f;

            var map = cal?.HomeMap;
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
                UiKit.LabelFit(new Rect(inRect.width - 160f, y, 160f, 30f), ColonyCalendar.DateText(CalendarMath.Join(quadrum, dayOfQuadrum), map), UiKit.Gold);
                y += 34f;
            }
            else
            {
                string date = editing?.kind == CalendarKind.FirstSnow ? "CC_Cal_WithSnow".Translate().ToString()
                    : editing != null && !editing.Dated ? "CC_Fest_WithOccasion".Translate().ToString() : ColonyCalendar.DateText(editing?.day ?? 0, map);
                UiKit.Paragraph(0f, y, inRect.width, "CC_Cal_Dialog_DateFixed".Translate(date).Resolve(), UiKit.Faint);
                y += 30f;
            }

            if (UiKit.ActButton(new Rect(inRect.width / 2f - 80f, inRect.height - 38f, 160f, 34f), (editing == null ? "CC_Cal_Dialog_Add" : "CC_Cal_Dialog_Save").Translate(), null)
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
            if (cal == null) return;
            if (editing == null)
            {
                var d = cal.AddNamed(n, way, CalendarMath.Join(quadrum, dayOfQuadrum));
                if (d == null)
                {
                    Messages.Message("CC_Cal_NameDay_Full".Translate(CalendarMath.MaxNamedDays), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (way == DayWay.Faith) d.subject = faithName;
                // direct: the dialog's own confirmation of the day the player just added.
                Messages.Message("CC_Cal_Dialog_Added".Translate(d.label, ColonyCalendar.DateText(d.day, cal.HomeMap)).Resolve(), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                editing.label = n;
                if (WayEditable) editing.way = way;
                if (WayEditable && way == DayWay.Faith && editing.kind == CalendarKind.Holiday) editing.subject = faithName;
                if (DateEditable && editing.kind != CalendarKind.FirstSnow)
                {
                    int day = CalendarMath.Join(quadrum, dayOfQuadrum);
                    if (day != editing.day)
                    {
                        // Moved: it comes on its new date, once this year at most.
                        var map = cal?.HomeMap;
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
