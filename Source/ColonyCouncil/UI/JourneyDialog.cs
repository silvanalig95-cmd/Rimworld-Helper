using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning a journey (Journeys/): who goes (colonists, and the animals to bring), where (a town or a place picked on
    /// the region map, or nowhere in particular for a chosen length), when (an hour on a day, the next dawn by default),
    /// how long on the road (the game's caravan estimate there, or there and back, or a chosen length), the weather on
    /// the way, the packing list to see and change, what each traveller changes into, and how each one's last night is
    /// shifted. The packing list is in groups (food, medicine, bedding, feed, other) with a subtotal each; every line can
    /// be typed into, stepped (Shift 10, Ctrl 100), set to all or none, put back to the planner's number or taken off,
    /// and anything in the stores can be added (UI/JourneyPick.cs). What the food and feed last against the road and the
    /// load against the room stay in sight at the foot of the window. The working copy is kept for the session while a
    /// place is picked on the region map.
    /// </summary>
    public class Dialog_JourneyPlan : Window
    {
        /// <summary>The planner's working copy, kept while the planner is closed to pick a place on the region map.</summary>
        public static JourneyPlan Draft;

        private static bool draftEditing;

        private readonly JourneyPlan plan;
        private readonly bool editing;
        private JourneyRoute route = new JourneyRoute();
        private Dictionary<Pawn, List<Apparel>> clothes = new Dictionary<Pawn, List<Apparel>>();
        private readonly Dictionary<Pawn, JourneyMath.NightPlan> nights = new Dictionary<Pawn, JourneyMath.NightPlan>();
        private readonly Dictionary<Pawn, int> nightHourNow = new Dictionary<Pawn, int>();
        private bool dirty = true;
        /// <summary>Closed to pick a place on the region map: the working copy waits for the planner to come back.</summary>
        private bool keepDraft;
        private float computedAt = -10f;
        private Vector2 leftScroll, rightScroll;
        private float leftH = 400f, rightH = 700f, leftW = 320f, totalsHeight;

        /// <summary>The travellers' nourishment a day, and each animal's with the days it needs feed for (worked out with the list).</summary>
        private float eatPerDay;
        private List<(float perDay, float feedDays)> feedNeeds = new List<(float, float)>();
        /// <summary>What the stores hold and the travellers carry, by thing: counted with the list and every two seconds.</summary>
        private Dictionary<ThingDef, (int stores, int carried)> held = new Dictionary<ThingDef, (int, int)>();
        private float heldAt = -10f;
        /// <summary>The food on the list against the policy each traveller eats by on the road (Journeys/RoadFood.cs), counted with the stores.</summary>
        private List<(string text, int level)> roadNotes = new List<(string, int)>();
        /// <summary>A line's count as it is being typed: set when the box is left or Enter pressed.</summary>
        private readonly Dictionary<string, string> countText = new Dictionary<string, string>();
        private string countEditing;

        private static readonly Color Accent = new Color(0.8f, 0.86f, 0.7f);
        private const int HourTicks = GenDate.TicksPerHour;
        private const string CountControl = "CC_JourneyCount_";
        private static readonly Regex Digits = new Regex("^[0-9]*$");

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1000f, UI.screenWidth - 40f), Mathf.Min(760f, UI.screenHeight - 40f));

        private Dialog_JourneyPlan(JourneyPlan plan, bool editing)
        {
            this.plan = plan;
            this.editing = editing;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            // Enter sets a typed count rather than closing the planner.
            closeOnAccept = false;
        }

        // ---------------- Opening ----------------

        private static Map HomeMap()
        {
            var cur = Find.CurrentMap;
            return cur != null && cur.IsPlayerHome ? cur : HappeningTracker.HomeMap();
        }

        private static JourneyPlan Fresh(Map map)
        {
            int now = Find.TickManager.TicksAbs;
            int hourNow = JourneyRun.HourOfDay(now, map);
            return new JourneyPlan
            {
                map = map, departAbs = JourneyRun.HourStart(now) + JourneyMath.DefaultHoursAhead(hourNow) * HourTicks, days = 2f, daysChosen = true, andBack = true
            };
        }

        private static JourneyPlan Clone(JourneyPlan p) =>
            new JourneyPlan
            {
                id = p.id, map = p.map, travellers = p.travellers.ToList(), animals = p.animals.ToList(), destTile = p.destTile, destObject = p.destObject,
                destName = p.destName, startTile = p.startTile, departAbs = p.departAbs, andBack = p.andBack, days = p.days, daysChosen = p.daysChosen, low = p.low,
                high = p.high, packing = p.packing.Select(l => l.Copy()).ToList()
            };

        /// <summary>The planner: the journey planned (to change it), the working copy, or a new plan.</summary>
        public static void Open(bool editPlanned = false)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var live = JourneyBook.Plan;
            if (editPlanned && live != null)
            {
                Draft = Clone(live);
                draftEditing = true;
            }
            else if (Draft == null || Draft.map == null || !Find.Maps.Contains(Draft.map))
            {
                var map = HomeMap();
                if (map == null) return;
                Draft = Fresh(map);
                draftEditing = false;
            }
            Show();
        }

        /// <summary>From the region map: this place as the destination of the plan being made (or of a new one).</summary>
        public static void OpenTo(PlanetTile tile, WorldObject wo)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            var live = JourneyBook.Plan;
            if (Draft == null || Draft.map == null || !Find.Maps.Contains(Draft.map))
            {
                if (live != null && live.destTile == tile.tileId)
                {
                    Open(true);
                    return;
                }
                var map = HomeMap();
                if (map == null) return;
                Draft = Fresh(map);
                draftEditing = false;
            }
            SetDest(Draft, tile, wo);
            Show();
        }

        private static void Show()
        {
            var open = Find.WindowStack.WindowOfType<Dialog_JourneyPlan>();
            if (open != null)
            {
                if (open.plan == Draft)
                {
                    open.dirty = true;
                    Find.WindowStack.Notify_ManuallySetFocus(open);
                    return;
                }
                open.Close(false);
            }
            Find.WindowStack.Add(new Dialog_JourneyPlan(Draft, draftEditing));
        }

        private static void SetDest(JourneyPlan p, PlanetTile tile, WorldObject wo)
        {
            if (!tile.Valid)
            {
                p.destTile = -1;
                p.destObject = -1;
                p.destName = null;
                p.daysChosen = true;
                return;
            }
            p.destTile = tile.tileId;
            p.destObject = wo?.ID ?? -1;
            p.startTile = -1;
            if (wo is Settlement s && !s.Name.NullOrEmpty()) p.destName = s.Name;
            else if (wo != null) p.destName = wo.LabelCap;
            else
            {
                int tiles = p.map != null ? Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(p.map.Tile, tile)) : 0;
                p.destName = "CC_Journey_Place".Translate(tile.Tile?.PrimaryBiome?.label ?? "?", tiles).Resolve();
            }
            p.daysChosen = false;
        }

        public override void PreClose()
        {
            base.PreClose();
            CommitCount();
        }

        public override void OnAcceptKeyPressed()
        {
            if (countEditing == null) return;
            CommitCount();
            UI.UnfocusCurrentControl();
        }

        public override void PostClose()
        {
            base.PostClose();
            // Closed for good: the next planner starts afresh (or from the plan made).
            if (!keepDraft && Draft == plan) Draft = null;
        }

        // ---------------- Working it out ----------------

        private void Compute()
        {
            dirty = false;
            computedAt = Time.realtimeSinceStartup;
            var map = plan.map;
            if (map == null) return;
            try
            {
                plan.travellers.RemoveAll(p => p == null || p.Destroyed);
                plan.animals.RemoveAll(a => a == null || a.Destroyed);
                var pawns = plan.travellers.Concat(plan.animals).ToList();
                // Twice: the load the packing list makes slows the caravan, and a longer road wants more packed.
                for (int pass = 0; pass < 2; pass++)
                {
                    float mass = JourneyPack.Mass(plan.travellers, plan.packing);
                    route = JourneyPack.Route(map, plan.DestTile, pawns, mass, plan.departAbs, plan.andBack, plan.daysChosen ? plan.days : -1f);
                    if (!plan.daysChosen) plan.days = route.oneWayTicks > 0 ? JourneyMath.TripDays(route.oneWayTicks, plan.andBack) : Mathf.Max(plan.days, 1f);
                    plan.low = route.low;
                    plan.high = route.high;
                    plan.packing = JourneyPack.Packing(map, plan.travellers, plan.animals, plan.days, route, plan.packing);
                }
                plan.Changed();
                eatPerDay = JourneyPack.PeoplePerDay(plan.travellers);
                feedNeeds = JourneyPack.FeedNeeds(plan.animals, plan.days, route);
                Count();
                clothes = JourneyPack.Clothes(plan);
                nights.Clear();
                nightHourNow.Clear();
                if (plan.departAbs - Find.TickManager.TicksAbs <= JourneyMath.PrepareHours * HourTicks)
                    foreach (var p in plan.travellers.Where(p => p.timetable != null))
                    {
                        nights[p] = JourneyRun.Night(p, plan.departAbs, map, out int hourNow);
                        nightHourNow[p] = hourNow;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey planner", e);
            }
        }

        // ---------------- Drawing ----------------

        public override void DoWindowContents(Rect inRect)
        {
            if (plan.map == null || !Find.Maps.Contains(plan.map))
            {
                Close();
                return;
            }
            if (countEditing != null && GUI.GetNameOfFocusedControl() != CountControl + countEditing) CommitCount();
            if (dirty && Time.realtimeSinceStartup - computedAt > 0.3f) Compute();
            else if (Time.realtimeSinceStartup - heldAt > 2f) Count();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 34f), (editing ? "CC_Journey_TitleEdit" : "CC_Journey_Title").Translate());
            Text.Font = GameFont.Small;
            float top = inRect.y + 40f;
            const float bottomH = 44f;
            leftW = Mathf.Clamp(inRect.width * 0.32f, 180f, 320f);
            try
            {
                // What the food lasts and the load, always in sight above the buttons; the rest scrolls.
                var totals = Totals();
                float totalsH = totalsHeight = TotalsHeight(totals, inRect.width);
                var left = new Rect(inRect.x, top, leftW, inRect.height - (top - inRect.y) - bottomH - totalsH);
                var right = new Rect(left.xMax + 14f, top, inRect.width - leftW - 14f, left.height);
                DrawWho(left);
                DrawDetails(right);
                DrawTotals(new Rect(inRect.x, left.yMax, inRect.width, totalsH), totals);
                DrawBottom(new Rect(inRect.x, inRect.yMax - bottomH + 8f, inRect.width, bottomH - 8f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey planner drawing", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawWho(Rect rect)
        {
            var map = plan.map;
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).OrderBy(p => p.LabelShort).ToList();
            var beasts = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.RaceProps.Animal).OrderByDescending(a => a.RaceProps.packAnimal).ThenBy(a => a.LabelShort).ToList();
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(leftH, rect.height));
            Widgets.BeginScrollView(rect, ref leftScroll, view);
            float y = 0f, w = view.width;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_Who".Translate(), Icons.People, Accent, plan.travellers.Count.ToString()) + 2f;
            foreach (var p in people) y += PawnRow(p, y, w, plan.travellers, false);
            y += 10f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_Beasts".Translate(), null, Accent, plan.animals.Count.ToString()) + 2f;
            if (beasts.Count == 0) y += UiKit.Paragraph(4f, y, w - 4f, "CC_Journey_NoBeasts".Translate(), UiKit.Faint, GameFont.Tiny) + 4f;
            foreach (var a in beasts) y += PawnRow(a, y, w, plan.animals, true);
            leftH = y + 10f;
            Widgets.EndScrollView();
        }

        private float PawnRow(Pawn p, float y, float w, List<Pawn> list, bool animal)
        {
            const float h = 30f;
            var row = new Rect(0f, y, w, h);
            bool on = list.Contains(p);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            bool now = on;
            Widgets.CheckboxDraw(row.x + 2f, row.y + 3f, on, false);
            UiKit.Portrait(new Rect(row.x + 30f, row.y + 2f, 26f, 26f), p, on ? UiKit.Gold : UiKit.Dim);
            string why = JourneyRun.NotReady(p, plan.map, animal);
            string note = why != null ? (why + "_Short").Translate().ToString() : animal && p.RaceProps.packAnimal ? "CC_Journey_PackAnimal".Translate().ToString() : null;
            UiKit.LabelFit(new Rect(row.x + 62f, row.y, w - 62f, note != null ? 18f : h), p.LabelShortCap, on ? Color.white : UiKit.Faint);
            if (note != null) UiKit.LabelFit(new Rect(row.x + 62f, row.y + 15f, w - 62f, 15f), note, why != null ? UiKit.Warn : UiKit.Dim, GameFont.Tiny);
            if (Widgets.ButtonInvisible(row)) now = !on;
            if (now != on)
            {
                if (now) list.Add(p);
                else list.Remove(p);
                dirty = true;
            }
            return h + 2f;
        }

        private void DrawDetails(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rightH, rect.height));
            Widgets.BeginScrollView(rect, ref rightScroll, view);
            float y = 0f, w = view.width;
            y = Where(y, w);
            y = When(y, w);
            y = Road(y, w);
            y = Packing(y, w);
            y = Clothes(y, w);
            y = LastDay(y, w);
            rightH = y + 10f;
            Widgets.EndScrollView();
        }

        private static bool SmallButton(float x, float y, float w, string label, string tip = null, bool active = true)
        {
            var r = new Rect(x, y, w, 26f);
            if (tip != null) TooltipHandler.TipRegion(r, tip);
            return Widgets.ButtonText(r, label, true, true, active) && active;
        }

        private float Where(float y, float w)
        {
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_Where".Translate(), Icons.World, Accent) + 2f;
            string text;
            if (!plan.HasDestination) text = "CC_Journey_NoDestText".Translate();
            else if (route.noWay) text = "CC_Journey_NoWay".Translate(plan.Dest).Resolve();
            else if (route.oneWayTicks > 0) text = "CC_Journey_DestDays".Translate(plan.Dest, TextUtil.DaysString(route.oneWayTicks)).Resolve();
            else text = plan.Dest;
            y += UiKit.Paragraph(4f, y, w - 8f, text, plan.HasDestination && route.noWay ? UiKit.Warn : Color.white) + 4f;
            float bx = 4f;
            if (SmallButton(bx, y, 220f, "CC_Journey_PickOnMap".Translate(), "CC_Journey_PickOnMap_Tip".Translate()))
            {
                Draft = plan;
                draftEditing = editing;
                keepDraft = true;
                Close(false);
                Window_RegionMap.Open();
                Messages.Message("CC_Journey_PickHint".Translate(), MessageTypeDefOf.SilentInput, false);
            }
            bx += 228f;
            if (plan.HasDestination && SmallButton(bx, y, 160f, "CC_Journey_NoDest".Translate()))
            {
                SetDest(plan, PlanetTile.Invalid, null);
                dirty = true;
            }
            return y + 34f;
        }

        private void Shift(int ticks)
        {
            int now = Find.TickManager.TicksAbs;
            int min = JourneyRun.HourStart(now) + JourneyMath.MinLeadHours * HourTicks;
            int max = JourneyRun.HourStart(now) + JourneyMath.MaxDaysAhead * GenDate.TicksPerDay;
            plan.departAbs = Mathf.Clamp(plan.departAbs + ticks, min, max);
            dirty = true;
        }

        private float When(float y, float w)
        {
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_WhenTitle".Translate(), null, Accent) + 2f;
            // A departure that has slipped into the past (the planner left open) moves to the soonest allowed.
            if (plan.departAbs < JourneyRun.HourStart(Find.TickManager.TicksAbs) + JourneyMath.MinLeadHours * HourTicks) Shift(0);
            UiKit.LabelFit(new Rect(4f, y, w - 8f, 26f), "CC_Journey_SetsOff".Translate(JourneyRun.When(plan)).Resolve(), Color.white);
            y += 28f;
            float bx = 4f;
            if (SmallButton(bx, y, 70f, "CC_Journey_DayLess".Translate())) Shift(-GenDate.TicksPerDay);
            bx += 74f;
            if (SmallButton(bx, y, 60f, "CC_Journey_HourLess".Translate())) Shift(-HourTicks);
            bx += 64f;
            if (SmallButton(bx, y, 60f, "CC_Journey_HourMore".Translate())) Shift(HourTicks);
            bx += 64f;
            if (SmallButton(bx, y, 70f, "CC_Journey_DayMore".Translate())) Shift(GenDate.TicksPerDay);
            bx += 78f;
            if (SmallButton(bx, y, 130f, "CC_Journey_NextDawn".Translate()))
            {
                int now = Find.TickManager.TicksAbs;
                plan.departAbs = JourneyRun.HourStart(now) + JourneyMath.DefaultHoursAhead(JourneyRun.HourOfDay(now, plan.map)) * HourTicks;
                dirty = true;
            }
            y += 30f;
            int hours = (plan.departAbs - JourneyRun.HourStart(Find.TickManager.TicksAbs)) / HourTicks;
            if (hours < JourneyRun.FreeHours)
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_Journey_Soon".Translate(JourneyRun.FreeHours).Resolve(), UiKit.Warn, GameFont.Tiny) + 2f;
            return y + 4f;
        }

        private static string Temp(float t) => float.IsNaN(t) ? "?" : t.ToStringTemperature("F0");

        private float Road(float y, float w)
        {
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_RoadTitle".Translate(), null, Accent) + 2f;
            if (plan.HasDestination && route.oneWayTicks > 0)
            {
                bool back = plan.andBack;
                Widgets.CheckboxLabeled(new Rect(4f, y, 260f, 24f), "CC_Journey_AndBack".Translate(), ref back);
                if (back != plan.andBack)
                {
                    plan.andBack = back;
                    dirty = true;
                }
                y += 28f;
            }
            string length = "CC_Journey_Length".Translate(plan.days.ToString("0.#")).Resolve();
            if (plan.HasDestination && !plan.daysChosen && route.oneWayTicks > 0) length += " " + "CC_Journey_ByEstimate".Translate().Resolve();
            UiKit.LabelFit(new Rect(4f, y, w - 250f, 26f), length, Color.white);
            float bx = w - 240f;
            if (SmallButton(bx, y, 40f, "-"))
            {
                plan.days = Mathf.Max(0.5f, plan.days - 0.5f);
                plan.daysChosen = true;
                dirty = true;
            }
            if (SmallButton(bx + 44f, y, 40f, "+"))
            {
                plan.days = Mathf.Min(60f, plan.days + 0.5f);
                plan.daysChosen = true;
                dirty = true;
            }
            if (plan.HasDestination && plan.daysChosen && route.oneWayTicks > 0 && SmallButton(bx + 88f, y, 148f, "CC_Journey_UseEstimate".Translate()))
            {
                plan.daysChosen = false;
                dirty = true;
            }
            y += 30f;
            if (!float.IsNaN(route.low))
            {
                string weather = "CC_Journey_Weather".Translate(Temp(route.low), Temp(route.high)).Resolve();
                if (plan.HasDestination && !float.IsNaN(route.destLow)) weather += " " + "CC_Journey_WeatherThere".Translate(plan.Dest, Temp(route.destLow), Temp(route.destHigh)).Resolve();
                if (plan.animals.Count > 0 && route.noGrazing > 0.05f) weather += " " + "CC_Journey_NoGrazing".Translate(route.noGrazing.ToStringPercent()).Resolve();
                y += UiKit.Paragraph(4f, y, w - 8f, weather, UiKit.Faint) + 4f;
            }
            return y + 4f;
        }

        // ---------------- What they take ----------------

        /// <summary>What the stores hold and the travellers carry, counted again.</summary>
        private void Count()
        {
            heldAt = Time.realtimeSinceStartup;
            held = JourneyPack.Holdings(plan.map, plan.travellers);
            try
            {
                roadNotes = RoadFood.PlanNotes(plan.travellers, plan.packing.Where(l => l.Taken && l.kind == "food").Select(l => l.def).ToList());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the food policy on the road, in the planner", e);
                roadNotes = new List<(string, int)>();
            }
        }

        private (int stores, int carried) Held(ThingDef def) => def != null && held.TryGetValue(def, out var h) ? h : default;

        private int ToHand(ThingDef def)
        {
            var h = Held(def);
            return h.stores + h.carried;
        }

        private static string Key(JourneyLine l) => l.kind + "/" + l.def.defName;

        private static string D(float days) => days.ToString(days < 10f ? "0.#" : "0");

        private static string Kg(float kg) => "CC_Journey_Kg".Translate(kg.ToString(kg < 10f ? "0.#" : "0")).Resolve();

        /// <summary>Green with the margin, orange for the road only, red when short.</summary>
        private static Color CoverColor(JourneyMath.Cover c) => c == JourneyMath.Cover.Margin ? UiKit.Good : c == JourneyMath.Cover.Road ? UiKit.Warn : UiKit.Bad;

        private JourneyMath.PackRow RowOf(JourneyLine l) => new JourneyMath.PackRow
        {
            kind = l.kind, count = l.Taken ? l.count : 0, carried = Held(l.def).carried, nutrition = JourneyPack.Nutrition(l.def), mass = JourneyPack.UnitMass(l.def),
            sleeps = JourneyPack.Sleeps(l.def)
        };

        /// <summary>What the animals that need feed eat a day between them.</summary>
        private float FeedEat => feedNeeds.Where(a => a.feedDays > 0f).Sum(a => a.perDay);

        private int People => plan.travellers.Count(p => p != null && !p.Dead);

        private float Packing(float y, float w)
        {
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_PackTitle".Translate(), null, Accent) + 2f;
            // Anything from the stores; the planner's own list again.
            float bx = 4f;
            if (SmallButton(bx, y, 200f, "CC_Journey_AddFromStores".Translate(), "CC_Journey_AddFromStores_Tip".Translate())) OpenPicker();
            bx += 208f;
            if (plan.packing.Any(l => (l.edited || l.removed) && !l.added) && SmallButton(bx, y, 220f, "CC_Journey_PackReset".Translate(), "CC_Journey_PackReset_Tip".Translate()))
            {
                CommitCount();
                foreach (var l in plan.packing.Where(l => !l.added))
                {
                    l.edited = false;
                    l.removed = false;
                }
                dirty = true;
            }
            y += 32f;
            // The food policy they eat by on the road: one of the colony's, or their own.
            if (SmallButton(4f, y, Mathf.Min(w - 8f, 420f), "CC_RoadFood_Row".Translate(RoadFood.Label).Resolve(), "CC_RoadFood_Row_Tip".Translate().Resolve()))
            {
                RoadFood.Menu();
                heldAt = -10f;
            }
            y += 32f;
            if (!plan.packing.Any(l => !l.removed))
                y += UiKit.Paragraph(4f, y, w - 8f, plan.travellers.Count == 0 ? "CC_Journey_PackNobody".Translate() : "CC_Journey_PackNothing".Translate(), UiKit.Faint) + 4f;
            int i = 0;
            foreach (string group in JourneyMath.Groups)
            {
                var lines = plan.packing.Where(l => l.def != null && JourneyMath.GroupOf(l.kind) == group).ToList();
                if (lines.Count == 0) continue;
                y = GroupHeader(group, y, w, lines);
                foreach (var line in lines.Where(l => !l.removed)) y = LineRow(line, y, w, i++);
                foreach (var line in lines.Where(l => l.removed)) y = RemovedRow(line, y, w, i++);
                y += 6f;
            }
            plan.packing.RemoveAll(l => l.def == null || (l.count <= 0 && !l.Player));
            // Weapons as they carry them.
            var people = plan.travellers.Where(p => p != null).ToList();
            if (people.Count > 0)
            {
                var arms = people.Select(p => p.equipment?.Primary != null ? "CC_Journey_Armed".Translate(p.LabelShort, p.equipment.Primary.LabelNoCount).Resolve()
                    : "CC_Journey_Unarmed".Translate(p.LabelShort).Resolve()).ToList();
                y += UiKit.Paragraph(4f, y + 2f, w - 8f, "CC_Journey_Weapons".Translate(TextUtil.JoinAnd(arms)).Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            }
            return y + 4f;
        }

        /// <summary>A group's name and its subtotal: days for food and feed, a person's share of medicine, sleeping places for bedding, and the weight.</summary>
        private float GroupHeader(string group, float y, float w, List<JourneyLine> lines)
        {
            var sum = JourneyMath.Sum(lines.Select(RowOf), group);
            string kg = Kg(sum.mass);
            int people = People;
            string sub;
            var color = UiKit.Faint;
            switch (group)
            {
                case "food":
                    if (eatPerDay > 0f)
                    {
                        float lasts = JourneyMath.Lasts(sum.nutrition, eatPerDay);
                        sub = "CC_Journey_SubDays".Translate(D(lasts), kg).Resolve();
                        color = CoverColor(JourneyMath.Covers(lasts, plan.days));
                    }
                    else sub = "CC_Journey_SubNutrition".Translate(sum.nutrition.ToString("0.#"), kg).Resolve();
                    break;
                case "medicine":
                    sub = people > 0 ? "CC_Journey_SubMedicine".Translate(JourneyMath.PerPerson(sum.count, people).ToString("0.#"), kg).Resolve() : kg;
                    break;
                case "bedding":
                    sub = people > 0 ? "CC_Journey_SubBedding".Translate(sum.sleeps, people, kg).Resolve() : kg;
                    break;
                case "feed":
                    if (FeedEat > 0f)
                    {
                        var (lasts, need) = JourneyMath.FeedCover(feedNeeds, sum.nutrition);
                        sub = "CC_Journey_SubDays".Translate(D(lasts), kg).Resolve();
                        color = CoverColor(JourneyMath.Covers(lasts, need));
                    }
                    else sub = kg;
                    break;
                default:
                    sub = kg;
                    break;
            }
            var r = new Rect(0f, y, w, 24f);
            Text.Font = GameFont.Small;
            string name = ("CC_Journey_Group_" + group).Translate();
            float nameW = Mathf.Min(w * 0.5f, Text.CalcSize(name).x + 12f);
            UiKit.LabelFit(new Rect(4f, y, nameW, 24f), name, UiKit.HeaderColor);
            UiKit.LabelFit(new Rect(nameW + 4f, y, w - nameW - 8f, 24f), sub, color, GameFont.Tiny, TextAnchor.MiddleRight);
            UiKit.Divider(4f, y + 23f, w - 8f);
            TooltipHandler.TipRegion(r, ("CC_Journey_Group_" + group + "_Tip").Translate());
            return y + 26f;
        }

        /// <summary>
        /// One line: its icon, name and what it comes to (whose number, the days it feeds them, its weight, how many are to
        /// hand), and how many: -, the number to type, +, all, none, back to the planner's number, take it off.
        /// </summary>
        private float LineRow(JourneyLine line, float y, float w, int index)
        {
            bool narrow = w < 540f;
            float h = narrow ? 66f : 40f;
            var row = new Rect(0f, y, w, h);
            UiKit.Zebra(row, index);
            int toHand = ToHand(line.def);
            bool mine = line.edited || line.added;

            // How many, from the right.
            float cy = narrow ? y + 38f : y + 8f;
            float right = w - 2f;
            var remove = new Rect(right - 20f, cy + 2f, 20f, 20f);
            right -= 26f;
            var reset = new Rect(right - 20f, cy + 2f, 20f, 20f);
            right -= 26f;
            var none = new Rect(right - 46f, cy, 46f, 24f);
            right -= 49f;
            var all = new Rect(right - 38f, cy, 38f, 24f);
            right -= 44f;
            var plus = new Rect(right - 24f, cy, 24f, 24f);
            right -= 26f;
            var box = new Rect(right - 50f, cy, 50f, 24f);
            right -= 52f;
            var minus = new Rect(right - 24f, cy, 24f, 24f);
            right -= 30f;

            // What it is.
            float textRight = narrow ? w - 4f : right;
            Widgets.ThingIcon(new Rect(4f, y + 6f, 28f, 28f), line.def);
            UiKit.LabelFit(new Rect(38f, y + 1f, Mathf.Max(0f, textRight - 38f), 22f), line.def.LabelCap, mine ? UiKit.Gold : Color.white);
            UiKit.LabelFit(new Rect(38f, y + 21f, Mathf.Max(0f, textRight - 38f), 17f), Details(line, toHand, out bool warn), warn ? UiKit.Warn : UiKit.Dim, GameFont.Tiny);
            var about = new Rect(0f, y, textRight, narrow ? 38f : h);
            if (Mouse.IsOver(about)) TooltipHandler.TipRegion(about, LineTip(line));

            int step = GenUI.CurrentAdjustmentMultiplier();
            string stepTip = "CC_Journey_StepTip".Translate();
            if (Widgets.ButtonText(minus, "-")) SetCount(line, JourneyMath.StepCount(line.count, -step, toHand));
            TooltipHandler.TipRegion(minus, stepTip);
            CountBox(box, line, mine);
            TooltipHandler.TipRegion(box, "CC_Journey_CountTip".Translate(toHand));
            if (Widgets.ButtonText(plus, "+")) SetCount(line, JourneyMath.StepCount(line.count, step, toHand));
            TooltipHandler.TipRegion(plus, stepTip);
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(all, "CC_Journey_SetAll".Translate())) SetCount(line, toHand);
            TooltipHandler.TipRegion(all, "CC_Journey_SetAllTip".Translate(toHand));
            if (Widgets.ButtonText(none, "CC_Journey_SetNone".Translate())) SetCount(line, 0);
            TooltipHandler.TipRegion(none, "CC_Journey_SetNoneTip".Translate());
            Text.Font = GameFont.Small;
            if (line.edited && !line.added)
            {
                if (Widgets.ButtonImage(reset, TexButton.Reload))
                {
                    LetGo(line);
                    line.edited = false;
                    dirty = true;
                }
                TooltipHandler.TipRegion(reset, "CC_Journey_LineResetTip".Translate());
            }
            if (Widgets.ButtonImage(remove, TexButton.CloseXSmall))
            {
                LetGo(line);
                if (line.added) plan.packing.Remove(line);
                else
                {
                    line.removed = true;
                    line.edited = false;
                    line.count = 0;
                }
                dirty = true;
            }
            TooltipHandler.TipRegion(remove, (line.added ? "CC_Journey_RemoveAddedTip" : "CC_Journey_RemoveTip").Translate());
            return y + h + 2f;
        }

        /// <summary>A suggestion taken off: faint, with the way to put it back.</summary>
        private float RemovedRow(JourneyLine line, float y, float w, int index)
        {
            const float h = 30f;
            UiKit.Zebra(new Rect(0f, y, w, h), index);
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.ThingIcon(new Rect(8f, y + 5f, 20f, 20f), line.def);
            GUI.color = Color.white;
            var back = new Rect(w - 112f, y + 3f, 110f, 24f);
            UiKit.LabelFit(new Rect(38f, y, Mathf.Max(0f, back.x - 44f), h), "CC_Journey_TakenOff".Translate(line.def.LabelCap).Resolve(), UiKit.Dim);
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(back, "CC_Journey_PutBack".Translate()))
            {
                line.removed = false;
                dirty = true;
            }
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(back, "CC_Journey_PutBackTip".Translate());
            return y + h + 2f;
        }

        /// <summary>The line's short facts: whose number it is, the days it feeds them, its weight, how many are to hand, and a warning if it spoils on the way.</summary>
        private string Details(JourneyLine line, int toHand, out bool warn)
        {
            var parts = new List<string> { (line.added ? "CC_Journey_Added" : line.edited ? "CC_Journey_YourNumber" : "CC_Journey_Suggested").Translate().ToString() };
            float nutrition = JourneyPack.Nutrition(line.def) * line.count;
            if (line.kind == "food" && nutrition > 0f)
                parts.Add(eatPerDay > 0f ? "CC_Journey_LineDays".Translate(D(nutrition / eatPerDay)).Resolve() : "CC_Journey_LineNutrition".Translate(nutrition.ToString("0.#")).Resolve());
            else if (line.kind == "feed" && nutrition > 0f && FeedEat > 0f) parts.Add("CC_Journey_LineDays".Translate(D(nutrition / FeedEat)).Resolve());
            parts.Add(Kg(line.count * JourneyPack.UnitMass(line.def)));
            parts.Add("CC_Journey_InStores".Translate(toHand).Resolve());
            warn = line.count > toHand;
            float rot = JourneyPack.RotDays(line.def);
            if ((line.kind == "food" || line.kind == "feed") && rot < plan.days)
            {
                parts.Add("CC_Journey_Spoils".Translate(D(rot)).Resolve());
                warn = true;
            }
            return string.Join(" · ", parts);
        }

        /// <summary>The line on hover: what the thing is, its weight, nourishment and keeping, how many are to hand, and who packs it.</summary>
        private string LineTip(JourneyLine line)
        {
            var sb = new StringBuilder();
            sb.AppendLine(line.def.LabelCap);
            string desc = line.def.description ?? "";
            if (desc.Length > 400) desc = desc.Substring(0, 400).TrimEnd() + "...";
            if (desc.Length > 0) sb.AppendLine().AppendLine(desc);
            sb.AppendLine();
            float each = JourneyPack.UnitMass(line.def);
            sb.AppendLine("CC_Journey_TipMass".Translate(each.ToString("0.##"), Kg(each * line.count)).Resolve());
            float nutrition = JourneyPack.Nutrition(line.def);
            if (nutrition > 0f) sb.AppendLine("CC_Journey_TipNutrition".Translate(nutrition.ToString("0.##"), (nutrition * line.count).ToString("0.#")).Resolve());
            if (line.def.IsIngestible)
            {
                float rot = JourneyPack.RotDays(line.def);
                sb.AppendLine(float.IsPositiveInfinity(rot) ? "CC_Journey_TipKeeps".Translate().Resolve() : "CC_Journey_TipRots".Translate(D(rot)).Resolve());
            }
            var h = Held(line.def);
            sb.AppendLine("CC_Journey_TipToHand".Translate(h.stores, h.carried).Resolve());
            sb.Append((JourneyPack.OwnPack(line.kind) ? "CC_Journey_TipOwnPack" : "CC_Journey_TipGate").Translate().Resolve());
            return sb.ToString();
        }

        /// <summary>The number box: typed digits wait until the box is left or Enter pressed, then are kept to what is to hand.</summary>
        private void CountBox(Rect r, JourneyLine line, bool mine)
        {
            string key = Key(line);
            string name = CountControl + key;
            if (countEditing != key || !countText.ContainsKey(key)) countText[key] = line.count.ToString();
            GUI.SetNextControlName(name);
            if (mine) GUI.color = UiKit.Gold;
            string typed = Widgets.TextField(r, countText[key], 6, Digits);
            GUI.color = Color.white;
            if (GUI.GetNameOfFocusedControl() != name) return;
            if (countEditing != null && countEditing != key) CommitCount();
            countEditing = key;
            countText[key] = typed;
        }

        /// <summary>The count being typed, set (an empty box changes nothing).</summary>
        private void CommitCount()
        {
            string key = countEditing;
            countEditing = null;
            if (key == null || !countText.TryGetValue(key, out string text) || !int.TryParse(text, out int n)) return;
            var line = plan.packing.FirstOrDefault(l => l.def != null && Key(l) == key);
            if (line != null) SetCount(line, JourneyMath.ClampCount(n, ToHand(line.def)));
        }

        /// <summary>A line's box stops being typed into (its number changes some other way).</summary>
        private void LetGo(JourneyLine line)
        {
            if (countEditing != Key(line)) return;
            countEditing = null;
            UI.UnfocusCurrentControl();
        }

        private void SetCount(JourneyLine line, int count)
        {
            LetGo(line);
            if (count == line.count) return;
            line.count = count;
            if (!line.added) line.edited = true;
            dirty = true;
        }

        private void OpenPicker()
        {
            CommitCount();
            // Over the list of who goes and ending above the totals, so the packing list and its totals stay in sight.
            float w = Mathf.Min(580f, UI.screenWidth - 20f);
            float y = windowRect.y + Margin + 40f;
            float h = Mathf.Clamp(windowRect.yMax - Margin - 44f - totalsHeight - y, 320f, Mathf.Min(720f, UI.screenHeight - 20f));
            float x = Mathf.Clamp(windowRect.x + Margin + leftW + 6f - w, 0f, UI.screenWidth - w);
            y = Mathf.Clamp(y, 0f, UI.screenHeight - h);
            Find.WindowStack.Add(new Dialog_JourneyPick(plan, () => dirty = true, new Rect(x, y, w, h)));
        }

        // ---------------- The totals at the foot ----------------

        /// <summary>The food's days against the road's, the feed's against the days without grazing, and the load against the room, each in its colour with the reckoning on hover.</summary>
        private List<(string text, Color color, string tip)> Totals()
        {
            var list = new List<(string, Color, string)>();
            var rows = plan.packing.Where(l => l.Taken).Select(RowOf).ToList();
            var people = plan.travellers.Where(p => p != null && !p.Dead).ToList();
            if (people.Count > 0)
            {
                string who = people.Count == 1 ? people[0].LabelShort : "CC_Journey_TheTravellers".Translate(people.Count).Resolve();
                string road = (plan.HasDestination && !plan.daysChosen ? plan.andBack ? "CC_Journey_RoadBack" : "CC_Journey_RoadOneWay" : "CC_Journey_RoadDays")
                    .Translate(D(plan.days)).Resolve();
                float food = JourneyMath.Sum(rows, "food").nutrition;
                float lasts = JourneyMath.Lasts(food, eatPerDay);
                float margin = JourneyMath.MarginDays(plan.days);
                string tip = "CC_Journey_FoodTip".Translate(who.CapitalizeFirst(), eatPerDay.ToString("0.##"), food.ToString("0.#"), D(margin)).Resolve();
                if (food <= 0f) list.Add(("CC_Journey_FoodNone".Translate(who, road).Resolve(), UiKit.Bad, tip));
                else if (eatPerDay > 0f)
                {
                    var cover = JourneyMath.Covers(lasts, plan.days);
                    float shortOfMargin = JourneyMath.ShortDays(lasts, margin), shortOfRoad = JourneyMath.ShortDays(lasts, plan.days);
                    string verdict = cover == JourneyMath.Cover.Margin ? "CC_Journey_FoodEnough".Translate(D(margin)).Resolve()
                        : cover == JourneyMath.Cover.Road ? "CC_Journey_FoodNoMargin".Translate(D(margin), JourneyMath.ShortMeals(shortOfMargin, eatPerDay)).Resolve()
                        : "CC_Journey_FoodShort".Translate(D(Mathf.Max(0.1f, shortOfRoad)), JourneyMath.ShortMeals(shortOfRoad, eatPerDay)).Resolve();
                    list.Add(("CC_Journey_FoodLasts".Translate(D(lasts), who, road).Resolve() + " " + verdict, CoverColor(cover), tip));
                }
            }
            // Food the policy they eat by on the road doesn't allow.
            foreach (var n in roadNotes)
                list.Add((n.text, n.level >= 2 ? UiKit.Bad : n.level == 1 ? UiKit.Warn : UiKit.Faint, "CC_RoadFood_Row_Tip".Translate().Resolve()));
            // The animals' feed against the days they can't graze.
            if (plan.animals.Count > 0)
            {
                float feed = JourneyMath.Sum(rows, "feed").nutrition;
                var (lasts, need) = JourneyMath.FeedCover(feedNeeds, feed);
                string tip = "CC_Journey_FeedTip".Translate(feedNeeds.Sum(a => a.perDay).ToString("0.##"), route.noGrazing.ToStringPercent()).Resolve();
                if (need <= 0f) list.Add(("CC_Journey_FeedGraze".Translate().Resolve(), UiKit.Faint, tip));
                else
                {
                    var cover = JourneyMath.Covers(lasts, need);
                    string verdict = cover == JourneyMath.Cover.Margin ? "CC_Journey_FeedEnough".Translate().Resolve()
                        : cover == JourneyMath.Cover.Road ? "CC_Journey_FeedNoMargin".Translate().Resolve()
                        : "CC_Journey_FeedShort".Translate(D(Mathf.Max(0.1f, JourneyMath.ShortDays(lasts, need)))).Resolve();
                    list.Add(("CC_Journey_FeedLasts".Translate(D(lasts), D(need)).Resolve() + " " + verdict, CoverColor(cover), tip));
                }
            }
            // The load against the room.
            if (people.Count > 0 || rows.Count > 0)
            {
                float load = JourneyMath.LoadToTake(rows);
                float room = people.Concat(plan.animals.Where(a => a != null && a.RaceProps.packAnimal)).Sum(p => Mathf.Max(0f, MassUtility.FreeSpace(p)));
                string text = "CC_Journey_Load".Translate(load.ToString("0"), room.ToString("0")).Resolve();
                if (load > room) text += " " + "CC_Journey_LoadOver".Translate((load - room).ToString("0")).Resolve();
                list.Add((text, load > room ? UiKit.Bad : UiKit.Faint, "CC_Journey_LoadTip".Translate().Resolve()));
            }
            return list;
        }

        private static float TotalsHeight(List<(string text, Color color, string tip)> totals, float width)
        {
            if (totals.Count == 0) return 0f;
            Text.Font = GameFont.Small;
            float h = 8f;
            foreach (var t in totals) h += Text.CalcHeight(t.text, width - 8f) + 2f;
            return h;
        }

        private static void DrawTotals(Rect r, List<(string text, Color color, string tip)> totals)
        {
            if (totals.Count == 0) return;
            UiKit.Divider(r.x, r.y + 3f, r.width);
            float y = r.y + 6f;
            foreach (var t in totals)
            {
                float h = UiKit.Paragraph(r.x + 4f, y, r.width - 8f, t.text, t.color);
                if (!t.tip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(r.x, y, r.width, h), t.tip);
                y += h + 2f;
            }
        }

        private float Clothes(float y, float w)
        {
            if (!(ColonyCouncilMod.Settings?.journeyClothes ?? true) || plan.travellers.Count == 0 || float.IsNaN(plan.low)) return y;
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_ClothesTitle".Translate(), null, Accent) + 2f;
            var lines = new List<string>();
            foreach (var p in plan.travellers.Where(p => p?.apparel != null))
            {
                clothes.TryGetValue(p, out var picks);
                var range = JourneyPack.ComfyWith(p, picks);
                string comfy = "CC_Journey_ComfyRange".Translate(Temp(range.min), Temp(range.max)).Resolve();
                if (picks != null && picks.Count > 0)
                    lines.Add("CC_Journey_ChangesInto".Translate(p.LabelShort, TextUtil.JoinAnd(picks.Select(a => a.LabelNoCount).ToList()), comfy).Resolve());
                else if (CampWeatherMath.Shortfall(range.min, range.max, plan.low, plan.high) > 0.5f)
                    lines.Add("CC_Journey_NothingFits".Translate(p.LabelShort, comfy).Resolve());
                else lines.Add("CC_Journey_DressedAlready".Translate(p.LabelShort, comfy).Resolve());
            }
            foreach (var l in lines) y += UiKit.Paragraph(4f, y, w - 8f, l, Color.white, GameFont.Tiny) + 2f;
            return y + 4f;
        }

        private float LastDay(float y, float w)
        {
            if (plan.travellers.Count == 0) return y;
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_LastDayTitle".Translate(), null, Accent) + 2f;
            if (plan.departAbs - Find.TickManager.TicksAbs > JourneyMath.PrepareHours * HourTicks)
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_Journey_NightLater".Translate(), UiKit.Faint, GameFont.Tiny) + 2f;
            else
                foreach (var p in plan.travellers)
                {
                    if (!nights.TryGetValue(p, out var night)) continue;
                    int h0 = nightHourNow.TryGetValue(p, out int h) ? h : 0;
                    string text = night.Shifted
                        ? "CC_Journey_NightShifted".Translate(p.LabelShort, (h0 + night.bed) % 24, (h0 + night.wake) % 24).Resolve()
                        : "CC_Journey_NightOwn".Translate(p.LabelShort).Resolve();
                    if (night.joyHours > 0) text += " " + "CC_Journey_NightJoy".Translate((h0 + night.joyFrom) % 24).Resolve();
                    y += UiKit.Paragraph(4f, y, w - 8f, text, Color.white, GameFont.Tiny) + 2f;
                }
            bool hygiene = plan.travellers.Any(JourneyPack.HasHygiene) || plan.travellers.Any(JourneyPack.HasBladder);
            y += UiKit.Paragraph(4f, y + 2f, w - 8f, (hygiene ? "CC_Journey_LastHoursHygiene" : "CC_Journey_LastHours").Translate(JourneyRun.FreeHours).Resolve(), UiKit.Faint,
                GameFont.Tiny) + 4f;
            return y + 4f;
        }

        private void DrawBottom(Rect rect)
        {
            var live = JourneyBook.Plan;
            float x = rect.x;
            if (editing && live != null)
            {
                if (Widgets.ButtonText(new Rect(x, rect.y, 180f, rect.height), "CC_Journey_CallOffButton".Translate()))
                {
                    JourneyRun.CallOff(live, "CC_Journey_CalledOff");
                    Draft = null;
                    Close();
                    return;
                }
                x += 190f;
            }
            if (!editing && live != null)
                UiKit.LabelFit(new Rect(x, rect.y, rect.width - 420f - (x - rect.x), rect.height), "CC_Journey_Replaces".Translate(live.Dest).Resolve(), UiKit.Warn, GameFont.Tiny);
            string why = plan.travellers.Count == 0 ? "CC_Journey_NeedsSomeone".Translate().ToString()
                : plan.HasDestination && route.noWay ? "CC_Journey_NoWay".Translate(plan.Dest).Resolve()
                : null;
            var confirm = new Rect(rect.xMax - 220f, rect.y, 220f, rect.height);
            if (why != null) TooltipHandler.TipRegion(confirm, why);
            if (Widgets.ButtonText(confirm, (editing ? "CC_Journey_Save" : "CC_Journey_Confirm").Translate(), true, true, why == null) && why == null)
            {
                // A number still being typed counts.
                CommitCount();
                if (dirty) Compute();
                var made = Clone(plan);
                if (editing && live != null) made.id = live.id;
                JourneyRun.Confirm(made, !editing || live == null);
                Draft = null;
                Close();
                return;
            }
            if (Widgets.ButtonText(new Rect(confirm.x - 170f, rect.y, 160f, rect.height), "CloseButton".Translate()))
            {
                Draft = null;
                Close();
            }
        }
    }

    /// <summary>
    /// Where a journey is planned from: a section on the Decrees and World pages (the plan in words, and its buttons),
    /// and on the region map, "Plan a journey here" for a town (in <see cref="NeighbourPanel.Sections"/>) and for any
    /// other place (in <see cref="NeighbourPanel.PlaceSections"/>).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class JourneyPanel
    {
        private static readonly Color Accent = new Color(0.8f, 0.86f, 0.7f);

        static JourneyPanel()
        {
            NeighbourPanel.Sections.Add((r, s) => Here(r, s.Tile, s));
            NeighbourPanel.PlaceSections.Add(Here);
        }

        /// <summary>The journey section of a page: returns the height used (with the gap under it).</summary>
        public static float Section(float x, float y, float w)
        {
            if (!JourneyBook.On || Current.ProgramState != ProgramState.Playing) return 0f;
            try
            {
                float start = y;
                y += UiKit.SectionTitle(x, y, w, "CC_Journey_Section".Translate(), Icons.World, Accent) + 4f;
                var plan = JourneyBook.Plan;
                const float buttonsW = 180f;
                string text = plan == null ? "CC_Journey_None".Translate().ToString() : Status(plan);
                float h = UiKit.Paragraph(x + 4f, y, w - buttonsW - 16f, text, plan == null ? UiKit.Faint : Color.white);
                float bx = x + w - buttonsW;
                if (plan == null)
                {
                    if (Widgets.ButtonText(new Rect(bx, y, buttonsW, 28f), "CC_Journey_PlanButton".Translate())) Dialog_JourneyPlan.Open();
                    h = Mathf.Max(h, 30f);
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(bx, y, buttonsW, 28f), "CC_Journey_OpenButton".Translate())) Dialog_JourneyPlan.Open(true);
                    if (Widgets.ButtonText(new Rect(bx, y + 32f, buttonsW, 28f), "CC_Journey_CallOffButton".Translate())) JourneyRun.CallOff(plan, "CC_Journey_CalledOff");
                    h = Mathf.Max(h, 62f);
                }
                return y + h + 14f - start;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey section", e);
                return 0f;
            }
        }

        /// <summary>The plan in words: who, where, when, and how far along they are.</summary>
        public static string Status(JourneyPlan plan)
        {
            string text = "CC_Journey_Status".Translate(JourneyRun.Who(plan), plan.Dest, JourneyRun.When(plan)).Resolve();
            return text + " " + ("CC_Journey_Stage_" + plan.stage).Translate().Resolve();
        }

        /// <summary>On the region map: a button to plan a journey to this place (or make it the destination of the plan being made).</summary>
        public static float Here(Rect rect, PlanetTile tile, WorldObject wo)
        {
            if (!JourneyBook.On || !tile.Valid) return 0f;
            var plan = JourneyBook.Plan;
            string label = Dialog_JourneyPlan.Draft != null ? "CC_Journey_HereDraft".Translate()
                : plan != null && plan.destTile == tile.tileId ? "CC_Journey_HereOpen".Translate()
                : "CC_Journey_Here".Translate();
            var r = new Rect(rect.x, rect.y, Mathf.Min(rect.width, 260f), 28f);
            TooltipHandler.TipRegion(r, "CC_Journey_Here_Tip".Translate());
            if (Widgets.ButtonText(r, label)) Dialog_JourneyPlan.OpenTo(tile, wo);
            return 32f;
        }
    }
}
