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
    /// Picking the place on the world map (docs/design/stage3-many-colonies.md, "Choosing a place"): the game's own world
    /// targeter, with Rimfolk's facts of the tile under the cursor beside it: whose land it is (Faction Territories), the
    /// towns within reach and their stance, how remote it would be (stage 1's rule), the days of travel by caravan from
    /// each home, the land, and the game's own checks (a valid tile, the goodwill warning near a town, the colony limit).
    /// </summary>
    public static class FoundingPlace
    {
        /// <summary>Towns within this many tiles are named.</summary>
        public const float NearTowns = 12f;

        /// <summary>
        /// "Found a new colony" from a colony's card: the world map, a place picked, then the founding window.
        /// <paramref name="handOver"/>: from the hand-over window, the old colony handed over in the same act (HandOver).
        /// </summary>
        public static void Pick(Map from, bool handOver = false)
        {
            string why = Foundings.WhyNot(from);
            if (why != null)
            {
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Pages.LeaveForMap();
            Find.WindowStack.Windows.OfType<Window_Card>().ToList().ForEach(w => w.Close(false));
            CameraJumper.TryShowWorld();
            Find.WorldTargeter.BeginTargeting(t =>
                {
                    string no = Foundings.TileWhyNot(t.Tile);
                    if (no != null || Find.WorldObjects.AnyWorldObjectAt(t.Tile) && !Find.WorldObjects.AnyWorldObjectAt<AbandonedSettlement>(t.Tile))
                    {
                        Messages.Message(no ?? "CC_Found_No_Taken".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    Find.WindowStack.Add(new Dialog_Founding(from, t.Tile, handOver));
                    return true;
                }, true, SettleUtility.SettleCommandTex, false, null, t => t.Tile.Valid ? string.Join("\n", Facts(t.Tile, from, true)) : "",
                null, from.Tile, true);
        }

        private static readonly Dictionary<long, string> cache = new Dictionary<long, string>();

        /// <summary>
        /// The tile's facts, as lines (short for the cursor's label). <paramref name="landing"/>: for another landing site
        /// (Colonies/Landing.cs), whose own check comes first and which no road leads from.
        /// </summary>
        public static List<string> Facts(PlanetTile tile, Map from, bool brief, bool landing = false)
        {
            var lines = new List<string>();
            if (!tile.Valid) return lines;
            try
            {
                string no = landing ? null : Foundings.TileWhyNot(tile);
                if (no != null)
                {
                    lines.Add(no);
                    return lines;
                }
                var owner = WorldMechanics.TerritoryOwner(tile);
                if (owner != null) lines.Add("CC_Found_Land".Translate(owner.Name.Named("PEOPLE"), RegionMap.StandingText(owner).Named("STANCE")).Resolve());
                // The towns within reach, nearest first, and how remote it would be (RegionMath.Remoteness).
                var towns = new List<(Settlement s, float tiles)>();
                float nearestPeaceful = -1f;
                int within = 0;
                foreach (var s in Find.WorldObjects.Settlements)
                {
                    if (s == null || s.Faction == null || s.Faction.IsPlayer || s.Tile.Layer != tile.Layer) continue;
                    float d = Find.WorldGrid.ApproxDistanceInTiles(tile, s.Tile);
                    if (d <= NearTowns) towns.Add((s, d));
                    bool peaceful = s.Faction.def.humanlikeFaction && !s.Faction.HostileTo(Faction.OfPlayer);
                    if (!peaceful || d > RegionMath.RemoteFar) continue;
                    within++;
                    if (nearestPeaceful < 0f || d < nearestPeaceful) nearestPeaceful = d;
                }
                foreach (var (s, d) in towns.OrderBy(x => x.tiles).Take(brief ? 3 : 6))
                    lines.Add("CC_Found_Town".Translate(s.LabelCap.Named("TOWN"), s.Faction.Name.Named("PEOPLE"), RegionMap.StandingText(s.Faction).Named("STANCE"),
                        Mathf.RoundToInt(d).Named("TILES")).Resolve());
                if (towns.Count == 0) lines.Add("CC_Found_NoTowns".Translate(Mathf.RoundToInt(NearTowns).Named("TILES")).Resolve());
                lines.Add(("CC_Region_Panel_Remote_" + RegionMath.RemoteLevel(RegionMath.Remoteness(nearestPeaceful, within))).Translate().Resolve());
                // The days by caravan from each home.
                foreach (var home in Homes.All.Where(h => !landing && h.Tile.Layer == tile.Layer))
                {
                    long key = (long)home.Tile.tileId << 32 | (uint)tile.tileId;
                    if (!cache.TryGetValue(key, out var road))
                    {
                        int ticks = -1;
                        try
                        {
                            ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(home.Tile, tile, null);
                        }
                        catch (Exception)
                        {
                            ticks = -1;
                        }
                        road = ticks > 0 ? "CC_Found_Road".Translate(Homes.Name(home).Named("HOME"), TextUtil.DaysString(ticks).Named("DAYS")).Resolve()
                            : "CC_Found_NoRoad".Translate(Homes.Name(home).Named("HOME")).Resolve();
                        if (cache.Count > 300) cache.Clear();
                        cache[key] = road;
                    }
                    lines.Add(road);
                }
                if (!brief)
                {
                    string land = RegionMap.LandText(tile);
                    if (!land.NullOrEmpty()) lines.Add(land);
                }
                // The game's own goodwill warning near another people's town.
                var offsets = new List<Pair<Settlement, int>>();
                SettlementProximityGoodwillUtility.AppendProximityGoodwillOffsets(tile, offsets, false, true);
                if (offsets.Count > 0) lines.Add("CC_Found_Goodwill".Translate(string.Join(", ", SettlementProximityGoodwillUtility.ProximityGoodwillOffsetsToString(offsets)).Named("LIST")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: the place's facts", e);
            }
            return lines;
        }
    }

    /// <summary>
    /// The founding window (docs/design/stage3-many-colonies.md, section 1): the place and what Rimfolk knows of it; who
    /// goes from the home, each grown-up's wish shown with why (decision 2A: the player's choice; sent or kept against
    /// their wish they feel uprooted, close bonds split feel parted); newcomers or not, their price named before the
    /// choice (decision 4A); the old colony kept (and the seat moved, decision 1A) or abandoned; the means the colony has;
    /// and the game's colony limit raised by one when needed, said plainly (decision 5B). "Found the colony" sets it going.
    /// The old colony's third choice is to hand it over in the same act (HandOver, Colonies/HandOvers.cs): the hand-over
    /// window follows, the newcomers come free, and the party sets out once the hand-over's ceremony is over.
    /// </summary>
    public class Dialog_Founding : Window
    {
        private readonly Map from;
        private readonly PlanetTile tile;
        private readonly HashSet<Pawn> going = new HashSet<Pawn>();
        private bool newcomers;
        private bool keepOld = true;
        /// <summary>The old colony handed over in the same act (HandOver) instead of kept or abandoned.</summary>
        private bool handOver;
        private bool makeSeat;
        private FoundingMeans means = FoundingMeans.Caravan;
        /// <summary>A scenario's start for the newcomers (its key), or null for a new game's group; the chosen scenario as read for its line.</summary>
        private string scenario;
        private ScenarioStart startRead;
        private List<FounderRow> rows = new List<FounderRow>();
        private float rowsAt = -10f;
        private bool dirty = true;
        private Vector2 scroll, scrollRight;
        private float listH = 400f, rightH = 600f;
        private List<string> facts;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1000f, UI.screenWidth - 40f), Mathf.Min(780f, UI.screenHeight - 40f));

        public Dialog_Founding(Map from, PlanetTile tile, bool handOver = false)
        {
            this.from = from;
            this.tile = tile;
            if (handOver && HandOvers.On)
            {
                this.handOver = true;
                keepOld = false;
            }
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            facts = FoundingPlace.Facts(tile, from, false);
        }

        private string Place => Foundings.PlaceName(tile, from);

        private void Refresh()
        {
            if (!dirty && Time.realtimeSinceStartup - rowsAt < 1f) return;
            rows = Foundings.Rows(from, going);
            rowsAt = Time.realtimeSinceStartup;
            dirty = false;
            going.RemoveWhere(p => rows.All(r => r.pawn != p) || rows.First(r => r.pawn == p).cant != null);
        }

        private void Toggle(FounderRow r)
        {
            if (going.Contains(r.pawn))
            {
                going.Remove(r.pawn);
                // Their children stay with them, unless the other parent goes.
                foreach (var c in rows.Where(c => !c.grown && Foundings.Kin(r.pawn, c.pawn) && !rows.Any(o => o.grown && o.pawn != r.pawn && going.Contains(o.pawn) && Foundings.Kin(o.pawn, c.pawn))))
                    going.Remove(c.pawn);
            }
            else
            {
                going.Add(r.pawn);
                // Children follow their parents unless moved.
                if (r.grown)
                    foreach (var c in rows.Where(c => !c.grown && c.cant == null && Foundings.Kin(r.pawn, c.pawn)))
                        going.Add(c.pawn);
            }
            dirty = true;
        }

        private bool NewcomersMayCome => Foundings.NewcomersOn && FoundingMath.NewcomersMayCome(!keepOld, Find.TickManager.TicksGame, Homes.Instance?.newcomersPaidAt ?? -1);

        /// <summary>Why it can't be founded as it stands (in words), or null.</summary>
        private string WhyNot()
        {
            string no = Foundings.WhyNot(from) ?? Foundings.TileWhyNot(tile);
            if (no != null) return no;
            int grownGoing = rows.Count(r => r.grown && going.Contains(r.pawn));
            int childrenGoing = rows.Count(r => !r.grown && going.Contains(r.pawn));
            int grownStaying = rows.Count(r => r.grown && !going.Contains(r.pawn));
            var m = FoundingMath.MeansOf(grownGoing + childrenGoing, means);
            string key = FoundingMath.PartyCheck(grownGoing, childrenGoing, grownStaying, keepOld, newcomers, NewcomersMayCome, m);
            if (key != null) return ("CC_Found_No_" + key).Translate();
            // Handed over in the same act: the hand-over's own checks (three grown-ups stay, no fight, no banishment).
            if (handOver)
            {
                string no2 = HandOvers.WhyNot(from, rows, going, newcomers && NewcomersMayCome);
                if (no2 != null) return no2;
            }
            if (m == FoundingMeans.Caravan)
            {
                if (!JourneyBook.On) return "CC_JourneysPg_Off".Translate();
                if (JourneyBook.PlanAt(from) != null) return "CC_Found_JourneyPlanned".Translate();
            }
            return null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (from == null || !Homes.IsHome(from))
            {
                Close();
                return;
            }
            Refresh();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 34f), "CC_Found_Title".Translate(Place.Named("PLACE")).Resolve());
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(inRect.x, inRect.y + 32f, inRect.width, 22f),
                "CC_Found_Subtitle".Translate(Homes.Name(from).Named("HOME"), Ages.Form(AgeFeature.Party, Ages.Of(from), Ages.Of(from)).word.Named("PARTY")).Resolve(), UiKit.Faint);
            float top = inRect.y + 60f;
            const float bottomH = 44f;
            float leftW = Mathf.Clamp(inRect.width * 0.46f, 300f, 460f);
            var left = new Rect(inRect.x, top, leftW, inRect.height - (top - inRect.y) - bottomH);
            var right = new Rect(left.xMax + 14f, top, inRect.width - leftW - 14f, left.height);
            try
            {
                DrawWho(left);
                DrawRight(right);
                DrawBottom(new Rect(inRect.x, inRect.yMax - bottomH + 8f, inRect.width, bottomH - 8f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding window", e);
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
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(listH, rect.height));
            Widgets.BeginScrollView(rect, ref scroll, view);
            float y = 0f, w = view.width;
            y += UiKit.SectionTitle(0f, y, w, "CC_Found_Who".Translate(Homes.Name(from).Named("HOME")).Resolve(), Icons.People, null, going.Count.ToString()) + 2f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_Found_WhoLine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            foreach (var r in rows)
            {
                var row = new Rect(0f, y, w, 40f);
                bool on = going.Contains(r.pawn);
                if (on) Widgets.DrawBoxSolid(row, UiKit.WithAlpha(UiKit.Gold, 0.08f));
                Widgets.DrawHighlightIfMouseover(row);
                UiKit.Portrait(new Rect(row.x + 28f, row.y + 3f, 34f, 34f), r.pawn, on ? UiKit.Gold : UiKit.Dim);
                Widgets.CheckboxDraw(row.x + 2f, row.y + 8f, on, r.cant != null, 24f);
                UiKit.LabelFit(new Rect(row.x + 68f, row.y + 1f, w - 190f, 20f), r.pawn.LabelShort + (r.grown ? "" : " " + "CC_Found_Child".Translate().Resolve()), r.cant == null ? Color.white : UiKit.Dim);
                string sub = r.cant ?? (r.grown ? string.Join(", ", r.reasons.Take(2)) : "CC_Found_ChildFollows".Translate().Resolve());
                UiKit.LabelFit(new Rect(row.x + 68f, row.y + 20f, w - 190f, 18f), sub, UiKit.Faint, GameFont.Tiny);
                if (r.grown && r.cant == null)
                {
                    var chip = new Rect(row.xMax - 118f, row.y + 9f, 114f, 22f);
                    Color c = r.wish == FoundingWish.Go ? UiKit.Good : r.wish == FoundingWish.Stay ? UiKit.Warn : UiKit.Faint;
                    bool against = FoundingMath.AgainstWish(r.wish, on);
                    UiKit.LabelFit(chip, (against ? "CC_Found_Against" : "CC_Found_Wish_" + r.wish).Translate().Resolve(), against ? UiKit.Warn : c, GameFont.Tiny, TextAnchor.MiddleRight);
                    string tip = ("CC_Found_Wish_" + r.wish).Translate().Resolve().CapitalizeFirst() + (r.reasons.Count > 0 ? ":\n" + string.Join("\n", r.reasons.Select(x => "  - " + x)) : ".");
                    if (against) tip += "\n\n" + (on ? "CC_Found_AgainstSent" : "CC_Found_AgainstKept").Translate(r.pawn.Named("PAWN")).Resolve();
                    TooltipHandler.TipRegion(row, tip);
                }
                // A click on the row picks them or leaves them out; nobody is picked but by the player.
                if (r.cant == null && Widgets.ButtonInvisible(row)) Toggle(r);
                y += 42f;
            }
            listH = y + 10f;
            Widgets.EndScrollView();
        }

        private void DrawRight(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rightH, rect.height));
            Widgets.BeginScrollView(rect, ref scrollRight, view);
            float y = 0f, w = view.width;
            // The place.
            y += UiKit.SectionTitle(0f, y, w, "CC_Found_ThePlace".Translate().Resolve(), Icons.World) + 2f;
            foreach (var f in facts) y += UiKit.Paragraph(4f, y, w - 8f, f, Color.white) + 2f;
            y += 8f;
            // How they go.
            int goingCount = going.Count;
            if (goingCount > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Found_How".Translate().Resolve(), Icons.Away) + 2f;
                foreach (var m in Foundings.MeansAt(from))
                {
                    if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), ("CC_Found_Means_" + m).Translate(), means == m)) means = m;
                    y += 26f;
                }
                y += UiKit.Paragraph(4f, y, w - 8f, ("CC_Found_MeansLine_" + FoundingMath.MeansOf(goingCount, means)).Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 8f;
            }
            // Newcomers.
            if (Foundings.NewcomersOn)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Found_NewcomersHead".Translate().Resolve(), Icons.Visitor) + 2f;
                var box = new Rect(4f, y, w - 8f, 24f);
                bool was = newcomers;
                Widgets.CheckboxLabeled(box, "CC_Found_NewcomersCheck".Translate(Ages.Form(AgeFeature.Call, Ages.Of(from), Ages.Of(from)).word.Named("CALL")).Resolve(), ref newcomers);
                if (was != newcomers) dirty = true;
                y += 26f;
                string price;
                if (handOver) price = "CC_HandOver_NewcomersFree".Translate().Resolve();
                else if (!keepOld) price = "CC_Found_NewcomersFree".Translate().Resolve();
                else if (!NewcomersMayCome)
                {
                    int next = FoundingMath.NewcomersNext(Find.TickManager.TicksGame, Homes.Instance?.newcomersPaidAt ?? -1);
                    price = "CC_Found_NewcomersNotYet".Translate(GenDate.DateFullStringAt(Find.TickManager.TicksAbs + Math.Max(0, next - Find.TickManager.TicksGame),
                        Find.WorldGrid.LongLatOf(from.Tile)).Named("DATE")).Resolve();
                }
                else price = "CC_Found_NewcomersPrice".Translate(FoundingMath.MinPassage.Named("SILVER"), Homes.Name(from).Named("HOME")).Resolve();
                y += UiKit.Paragraph(4f, y, w - 8f, price + " " + "CC_Found_NewcomersWhen".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 8f;
                if (newcomers) y = DrawStart(w, y);
            }
            // The old colony.
            y += UiKit.SectionTitle(0f, y, w, "CC_Found_OldHead".Translate(Homes.Name(from).Named("HOME")).Resolve(), Icons.Colony) + 2f;
            if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), "CC_Found_Keep".Translate(), keepOld))
            {
                keepOld = true;
                handOver = false;
                dirty = true;
            }
            y += 26f;
            if (keepOld)
            {
                var seatBox = new Rect(24f, y, w - 28f, 24f);
                Widgets.CheckboxLabeled(seatBox, "CC_Found_MakeSeat".Translate(ColonySeat.Word(from).Named("SEAT")).Resolve(), ref makeSeat);
                TooltipHandler.TipRegion(seatBox, "CC_Found_MakeSeatTip".Translate());
                y += 26f;
                y += UiKit.Paragraph(24f, y, w - 28f, "CC_Found_KeepLine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            }
            if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), "CC_Found_Abandon".Translate(), !keepOld && !handOver))
            {
                keepOld = false;
                handOver = false;
                makeSeat = false;
                dirty = true;
            }
            y += 26f;
            if (!keepOld && !handOver)
            {
                var left = rows.Where(r => !going.Contains(r.pawn)).Select(r => r.pawn).ToList();
                string line = left.Count == 0 ? "CC_Found_AbandonLine".Translate().Resolve()
                    : "CC_Found_AbandonLeft".Translate(Foundings.Names(left).Named("NAMES")).Resolve();
                y += UiKit.Paragraph(24f, y, w - 28f, line, left.Count == 0 ? UiKit.Faint : UiKit.Warn, GameFont.Tiny) + 4f;
            }
            // Handed over in the same act (HandOver): a town of its own people once those who go have left (Colonies/HandOvers.cs).
            if (HandOvers.On)
            {
                var box = new Rect(4f, y, w - 8f, 24f);
                string no = HandOvers.WhyNot(from);
                if (Widgets.RadioButtonLabeled(box, "CC_HandOver_FoundChoice".Translate(), handOver, no != null) && no == null)
                {
                    handOver = true;
                    keepOld = false;
                    makeSeat = false;
                    dirty = true;
                }
                if (no != null) TooltipHandler.TipRegion(box, no);
                y += 26f;
                if (handOver) y += UiKit.Paragraph(24f, y, w - 28f, "CC_HandOver_FoundChoiceLine".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            }
            y += 8f;
            // The game's colony limit (decision 5B), said plainly when the new colony raises it.
            int pref = Prefs.MaxNumberOfPlayerSettlements;
            int to = FoundingMath.LimitFor(pref, Foundings.Counted());
            if (to > pref)
                y += UiKit.Paragraph(4f, y, w - 8f, "CC_Found_LimitLine".Translate(pref.Named("COUNT"), to.Named("TO")).Resolve()
                                                    + (keepOld ? "" : " " + (handOver ? "CC_HandOver_LimitBack" : "CC_Found_LimitBack").Translate().Resolve()),
                    UiKit.Warn, GameFont.Tiny) + 4f;
            if (keepOld) y += UiKit.Paragraph(4f, y, w - 8f, "CC_Found_MapTime".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            rightH = y + 10f;
            Widgets.EndScrollView();
        }

        /// <summary>
        /// The newcomers' start (release 3, Colonies/ScenarioStarts.cs): as a new game's group, as before, or a scenario's
        /// start, chosen from the list of the loaded scenarios with what each carries; the chosen one's line under it.
        /// </summary>
        private float DrawStart(float w, float y)
        {
            UiKit.LabelFit(new Rect(4f, y, w - 8f, 22f), "CC_ScenStart_Head".Translate().Resolve(), Color.white, GameFont.Small);
            y += 24f;
            if (Widgets.RadioButtonLabeled(new Rect(24f, y, w - 28f, 24f), "CC_ScenStart_AsGroup".Translate(), scenario == null)) scenario = null;
            y += 26f;
            var scen = scenario != null ? ScenarioStarts.Resolve(scenario) : null;
            if (scenario != null && scen == null) scenario = null;
            string label = scen == null ? "CC_ScenStart_AsScenario".Translate().Resolve() : "CC_ScenStart_AsScenarioNamed".Translate(scen.name.Named("SCENARIO")).Resolve();
            var box = new Rect(24f, y, w - 28f, 24f);
            TooltipHandler.TipRegion(box, "CC_ScenStart_AsScenarioTip".Translate());
            if (Widgets.RadioButtonLabeled(box, label, scen != null))
                Find.WindowStack.Add(new Dialog_ScenarioStart(scenario, !keepOld, going.Count > 0, k => scenario = k));
            y += 26f;
            if (scen != null)
            {
                if (startRead?.scenario != scen || startRead.hooks != ScenarioStarts.HooksOn) startRead = ScenarioStarts.Read(scen, scenario);
                string brief = ScenarioStarts.Brief(startRead, !keepOld, going.Count > 0, FoundingMath.Free(!keepOld));
                y += UiKit.Paragraph(24f, y, w - 28f, brief, UiKit.Faint, GameFont.Tiny) + 8f;
            }
            return y;
        }

        private void DrawBottom(Rect rect)
        {
            string why = WhyNot();
            var confirm = new Rect(rect.xMax - 220f, rect.y, 220f, rect.height);
            if (why != null)
            {
                TooltipHandler.TipRegion(confirm, why);
                UiKit.LabelFit(new Rect(rect.x, rect.y, rect.width - 420f, rect.height), why, UiKit.Warn, GameFont.Tiny);
            }
            if (Widgets.ButtonText(confirm, (handOver ? "CC_HandOver_FoundConfirm" : "CC_Found_Confirm").Translate(), true, true, why == null) && why == null)
            {
                Found();
                return;
            }
            if (Widgets.ButtonText(new Rect(confirm.x - 170f, rect.y, 160f, rect.height), "CloseButton".Translate())) Close();
        }

        private void Found()
        {
            var party = rows.Where(r => going.Contains(r.pawn)).Select(r => r.pawn).ToList();
            var plan = new FoundingPlan
            {
                from = Homes.KeyOf(from), tile = tile.tileId, layer = tile.Layer.LayerID, place = Place, means = FoundingMath.MeansOf(party.Count, means),
                keepOld = keepOld, makeSeat = keepOld && makeSeat, newcomers = newcomers && NewcomersMayCome, party = party,
                scenario = newcomers && NewcomersMayCome ? scenario : null, scenarioName = newcomers && scenario != null ? ScenarioStarts.Resolve(scenario)?.name : null,
                sentAgainst = rows.Where(r => r.grown && going.Contains(r.pawn) && FoundingMath.AgainstWish(r.wish, true)).Select(r => r.pawn).ToList(),
                keptAgainst = rows.Where(r => r.grown && !going.Contains(r.pawn) && FoundingMath.AgainstWish(r.wish, false)).Select(r => r.pawn).ToList()
            };
            Close();
            // Handed over in the same act: the hand-over window follows, and the founding sets out after its ceremony.
            if (handOver)
            {
                HandOvers.Open(from, plan);
                return;
            }
            Foundings.Begin(plan);
        }
    }
}
