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
    /// The window for another landing site (Colonies/Landing.cs): a new map of the same place, or another place picked on
    /// the world map with Rimfolk's facts of the tile under the cursor and the game's own check; the map size (the game's
    /// own sizes, the current one first chosen); and, plainly, what comes along and what is lost, worked out as the move
    /// works it out.
    /// </summary>
    public class Dialog_Landing : Window
    {
        private bool same;
        private PlanetTile tile;
        private int size;
        private readonly List<int> sizes;
        private readonly int current;
        private LandingPlan plan;
        private float planAt = -10f;
        private List<string> facts;
        private PlanetTile factsFor = PlanetTile.Invalid;
        private Vector2 scroll;
        private float viewH = 600f;
        /// <summary>The world map is to open once this frame is drawn.</summary>
        private bool pickNow;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(720f, UI.screenWidth - 40f), Mathf.Min(760f, UI.screenHeight - 40f));

        /// <summary>Opens the window: the same place unless a place was picked, at the size chosen before (the map's own the first time).</summary>
        public static void Open(PlanetTile? picked = null, int size = 0)
        {
            if (!Landing.Shows || Landing.Home == null) return;
            Find.WindowStack.Windows.OfType<Dialog_Landing>().ToList().ForEach(w => w.Close(false));
            Find.WindowStack.Add(new Dialog_Landing(picked ?? PlanetTile.Invalid, size));
        }

        public Dialog_Landing(PlanetTile picked, int size)
        {
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            var map = Landing.Home;
            current = map?.Size.x ?? 250;
            int[] test = { 350, 400 };
            sizes = LandingMath.Sizes(Dialog_AdvancedGameConfig.MapSizes, test, Prefs.TestMapSizes, current);
            this.size = size > 0 && sizes.Contains(size) ? size : LandingMath.DefaultSize(sizes, current);
            tile = picked.Valid ? picked : PlanetTile.Invalid;
            same = !tile.Valid;
        }

        private Map Home => Landing.Home;

        private LandingPlan Plan
        {
            get
            {
                if (plan == null || Time.realtimeSinceStartup - planAt > 1f)
                {
                    plan = Landing.Plan(Home);
                    planAt = Time.realtimeSinceStartup;
                }
                return plan;
            }
        }

        private List<string> Facts(PlanetTile t)
        {
            if (facts == null || factsFor != t)
            {
                facts = FoundingPlace.Facts(t, Home, false, true);
                factsFor = t;
            }
            return facts;
        }

        /// <summary>Why the chosen landing can't be made as it stands, in words, or null.</summary>
        private string WhyNot()
        {
            string why = Landing.WhyNot();
            if (why != null || same) return why;
            if (Landing.PlaceForced) return "CC_Landing_Forced".Translate();
            if (!tile.Valid) return "CC_Landing_NoPlaceYet".Translate();
            return Landing.TileWhyNot(tile, Home);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var map = Home;
            if (map == null || !Landing.Shows)
            {
                Close();
                return;
            }
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 34f), Landing.Label);
                Text.Font = GameFont.Small;
                string people = ("CC_Landing_People_" + Ages.Tag(Ages.Of(map))).Translate().Resolve();
                UiKit.LabelFit(new Rect(inRect.x, inRect.y + 32f, inRect.width, 22f), "CC_Landing_Subtitle".Translate(people.Named("PEOPLE")).Resolve(), UiKit.Faint);
                const float bottomH = 44f;
                float top = inRect.y + 60f;
                var body = new Rect(inRect.x, top, inRect.width, inRect.yMax - top - bottomH);
                DrawBody(body, map);
                DrawBottom(new Rect(inRect.x, inRect.yMax - bottomH + 8f, inRect.width, bottomH - 8f));
                if (pickNow)
                {
                    pickNow = false;
                    Pick(map);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing window", e);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawBody(Rect rect, Map map)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(viewH, rect.height));
            Widgets.BeginScrollView(rect, ref scroll, view);
            float y = 0f, w = view.width;

            // Where.
            y += UiKit.SectionTitle(0f, y, w, "CC_Landing_WhereHead".Translate().Resolve(), Icons.World) + 2f;
            var sameRow = new Rect(4f, y, w - 8f, 24f);
            if (Widgets.RadioButtonLabeled(sameRow, "CC_Landing_SamePlace".Translate(), same)) same = true;
            TooltipHandler.TipRegion(sameRow, "CC_Landing_SamePlaceTip".Translate());
            y += 26f;
            bool forced = Landing.PlaceForced;
            var otherRow = new Rect(4f, y, w - 8f, 24f);
            if (!forced && Widgets.RadioButtonLabeled(otherRow, "CC_Landing_OtherPlace".Translate(), !same))
            {
                same = false;
                if (!tile.Valid) pickNow = true;
            }
            if (forced) UiKit.LabelFit(otherRow, "CC_Landing_OtherPlace".Translate() + ": " + "CC_Landing_Forced".Translate(), UiKit.Dim);
            TooltipHandler.TipRegion(otherRow, forced ? "CC_Landing_Forced".Translate() : "CC_Landing_OtherPlaceTip".Translate());
            y += 26f;
            if (!same)
            {
                var pick = new Rect(24f, y, 220f, 26f);
                if (Widgets.ButtonText(pick, (tile.Valid ? "CC_Landing_PickAgain" : "CC_Landing_PickPlace").Translate())) pickNow = true;
                y += 30f;
            }
            var place = same ? map.Tile : tile;
            if (place.Valid)
            {
                string name = same ? "CC_Landing_PlaceHere".Translate(Homes.Name(map).Named("NAME")).Resolve()
                    : "CC_Landing_PlaceThere".Translate(Foundings.PlaceName(place, map).Named("PLACE")).Resolve();
                y += UiKit.Paragraph(24f, y, w - 28f, name, Color.white) + 2f;
                foreach (var f in Facts(place)) y += UiKit.Paragraph(24f, y, w - 28f, f, UiKit.Faint, GameFont.Tiny) + 2f;
            }
            else y += UiKit.Paragraph(24f, y, w - 28f, "CC_Landing_NoPlaceYet".Translate().Resolve(), UiKit.Warn, GameFont.Tiny) + 2f;
            y += 8f;

            // The map's size.
            y += UiKit.SectionTitle(0f, y, w, "CC_Landing_SizeHead".Translate().Resolve(), Icons.LensLand) + 2f;
            foreach (int s in sizes)
            {
                string label = "MapSizeDesc".Translate(s, s * s).Resolve() + (s == current ? " " + "CC_Landing_SizeNow".Translate().Resolve() : "");
                if (Widgets.RadioButtonLabeled(new Rect(4f, y, w - 8f, 24f), label, size == s)) size = s;
                y += 26f;
            }
            if (size >= 300) y += UiKit.Paragraph(24f, y, w - 28f, "MapSizePerformanceWarning".Translate().Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
            y += 8f;

            // What comes along.
            var p = Plan;
            var r = Landing.Record;
            y += UiKit.SectionTitle(0f, y, w, "CC_Landing_ComesHead".Translate().Resolve(), Icons.People) + 2f;
            foreach (var side in new[] { LandingSide.Colonist, LandingSide.Prisoner, LandingSide.Slave, LandingSide.Mech, LandingSide.Animal })
            {
                var who = p.Everyone.Where(x => Landing.SideOf(x) == side).ToList();
                if (who.Count == 0) continue;
                y += UiKit.Paragraph(4f, y, w - 8f, ("CC_Landing_Side_" + side).Translate(Foundings.Names(who).Named("NAMES"), who.Count.Named("COUNT")).Resolve(), Color.white) + 2f;
            }
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_Landing_ComesGear".Translate().Resolve(), UiKit.Faint) + 2f;
            string kit = Landing.KitWords(p);
            y += UiKit.Paragraph(4f, y, w - 8f, kit.NullOrEmpty() ? "CC_Landing_ComesKitNone".Translate().Resolve() : "CC_Landing_ComesKit".Translate(kit.Named("LIST")).Resolve(),
                kit.NullOrEmpty() ? UiKit.Faint : Color.white) + 2f;
            string arrive = (r != null && r.ByPods ? "CC_Landing_Arrive_Pods" : "CC_Landing_Arrive_Standing").Translate().Resolve();
            if (p.loose.Any(x => LandingMath.PlaceOf(x.piece.kind) != LandingKind.Pods)) arrive += " " + "CC_Landing_ArriveScatter".Translate().Resolve();
            y += UiKit.Paragraph(4f, y, w - 8f, arrive, UiKit.Faint, GameFont.Tiny) + 8f;

            // What is lost, and what stays.
            y += UiKit.SectionTitle(0f, y, w, "CC_Landing_LostHead".Translate().Resolve(), Icons.Warning) + 2f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_Landing_Lost".Translate().Resolve(), UiKit.Warn) + 4f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_Landing_Stays".Translate().Resolve(), UiKit.Faint) + 4f;
            y += UiKit.Paragraph(4f, y, w - 8f, "CC_Landing_OfferLine".Translate(LandingMath.OfferDays.Named("DAYS")).Resolve(), UiKit.Faint, GameFont.Tiny) + 4f;
            viewH = y + 10f;
            Widgets.EndScrollView();
        }

        private void DrawBottom(Rect rect)
        {
            string why = WhyNot();
            bool pods = Landing.Record?.method != PlayerPawnsArriveMethod.Standing;
            var confirm = new Rect(rect.xMax - 220f, rect.y, 220f, rect.height);
            if (why != null)
            {
                TooltipHandler.TipRegion(confirm, why);
                UiKit.LabelFit(new Rect(rect.x, rect.y, rect.width - 420f, rect.height), why, UiKit.Warn, GameFont.Tiny);
            }
            if (Widgets.ButtonText(confirm, (pods ? "CC_Landing_Confirm" : "CC_Landing_ConfirmStanding").Translate(), true, true, why == null) && why == null)
            {
                var to = same ? PlanetTile.Invalid : tile;
                int s = size;
                Close();
                Landing.Begin(to, s);
                return;
            }
            if (Widgets.ButtonText(new Rect(confirm.x - 170f, rect.y, 160f, rect.height), "CloseButton".Translate())) Close();
        }

        /// <summary>Another place: the game's world targeter, with Rimfolk's facts of the tile under the cursor and the game's own check.</summary>
        private void Pick(Map map)
        {
            int keep = size;
            Close(false);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Find.WindowStack.Windows.OfType<Window_Card>().ToList().ForEach(w => w.Close(false));
            CameraJumper.TryShowWorld();
            Find.WorldTargeter.BeginTargeting(t =>
                {
                    string no = Landing.TileWhyNot(t.Tile, Landing.Home);
                    if (no != null)
                    {
                        Messages.Message(no, MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    Open(t.Tile, keep);
                    return true;
                }, true, SettleUtility.SettleCommandTex, false, null,
                t => !t.Tile.Valid ? "" : Landing.TileWhyNot(t.Tile, Landing.Home) ?? string.Join("\n", FoundingPlace.Facts(t.Tile, Landing.Home, true, true)),
                null, map.Tile, true);
        }
    }
}
