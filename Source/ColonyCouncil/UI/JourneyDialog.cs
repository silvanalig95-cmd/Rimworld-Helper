using System;
using System.Collections.Generic;
using System.Linq;
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
    /// shifted. The working copy is kept for the session while a place is picked on the region map.
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
        private float leftH = 400f, rightH = 700f;

        private static readonly Color Accent = new Color(0.8f, 0.86f, 0.7f);
        private const int HourTicks = GenDate.TicksPerHour;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1000f, UI.screenWidth - 40f), Mathf.Min(740f, UI.screenHeight - 60f));

        private Dialog_JourneyPlan(JourneyPlan plan, bool editing)
        {
            this.plan = plan;
            this.editing = editing;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
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
            if (dirty && Time.realtimeSinceStartup - computedAt > 0.3f) Compute();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 34f), (editing ? "CC_Journey_TitleEdit" : "CC_Journey_Title").Translate());
            Text.Font = GameFont.Small;
            float top = inRect.y + 40f;
            const float bottomH = 44f;
            float leftW = Mathf.Min(320f, inRect.width * 0.34f);
            var left = new Rect(inRect.x, top, leftW, inRect.height - (top - inRect.y) - bottomH);
            var right = new Rect(left.xMax + 14f, top, inRect.width - leftW - 14f, left.height);
            try
            {
                DrawWho(left);
                DrawDetails(right);
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

        private float Packing(float y, float w)
        {
            y += 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Journey_PackTitle".Translate(), null, Accent) + 2f;
            var map = plan.map;
            if (plan.packing.Count == 0)
                y += UiKit.Paragraph(4f, y, w - 8f, plan.travellers.Count == 0 ? "CC_Journey_PackNobody".Translate() : "CC_Journey_PackNothing".Translate(), UiKit.Faint) + 4f;
            int i = 0;
            foreach (var line in plan.packing.ToList())
            {
                var row = new Rect(0f, y, w, 28f);
                UiKit.Zebra(row, i++);
                Widgets.ThingIcon(new Rect(4f, y + 2f, 24f, 24f), line.def);
                int stock = JourneyPack.InStores(map, line.def) + plan.travellers.Sum(p => JourneyPack.Carried(p, line.def));
                UiKit.LabelFit(new Rect(34f, y, w - 330f, 28f), line.def.LabelCap + "  " + ("CC_Journey_Kind_" + line.kind).Translate(), Color.white);
                UiKit.LabelFit(new Rect(w - 296f, y, 110f, 28f), "CC_Journey_InStores".Translate(stock).Resolve(), stock < line.count ? UiKit.Warn : UiKit.Dim, GameFont.Tiny);
                float bx = w - 180f;
                if (SmallButton(bx, y + 1f, 36f, "-"))
                {
                    line.count = Mathf.Max(0, line.count - (line.count > 20 ? 5 : 1));
                    line.edited = true;
                    dirty = true;
                }
                UiKit.LabelFit(new Rect(bx + 40f, y, 50f, 28f), line.count.ToString(), line.edited ? UiKit.Gold : Color.white, GameFont.Small, TextAnchor.MiddleCenter);
                if (SmallButton(bx + 94f, y + 1f, 36f, "+"))
                {
                    line.count += line.count >= 20 ? 5 : 1;
                    line.edited = true;
                    dirty = true;
                }
                y += 30f;
            }
            plan.packing.RemoveAll(l => l.count <= 0 && !l.edited);
            if (plan.packing.Any(l => l.edited) && SmallButton(4f, y + 2f, 200f, "CC_Journey_PackReset".Translate()))
            {
                foreach (var l in plan.packing) l.edited = false;
                dirty = true;
            }
            if (plan.packing.Any(l => l.edited)) y += 32f;
            // Weapons as they carry them, and what the load comes to.
            var people = plan.travellers.Where(p => p != null).ToList();
            if (people.Count > 0)
            {
                var arms = people.Select(p => p.equipment?.Primary != null ? "CC_Journey_Armed".Translate(p.LabelShort, p.equipment.Primary.LabelNoCount).Resolve()
                    : "CC_Journey_Unarmed".Translate(p.LabelShort).Resolve()).ToList();
                y += UiKit.Paragraph(4f, y + 2f, w - 8f, "CC_Journey_Weapons".Translate(TextUtil.JoinAnd(arms)).Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
                float load = plan.packing.Sum(l => l.count * JourneyPack.UnitMass(l.def));
                float carry = people.Concat(plan.animals.Where(a => a.RaceProps.packAnimal)).Sum(p => Mathf.Max(0f, MassUtility.FreeSpace(p)));
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_Journey_Load".Translate(load.ToString("0"), carry.ToString("0")).Resolve(), load > carry ? UiKit.Warn : UiKit.Faint, GameFont.Tiny) + 4f;
            }
            return y + 4f;
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
