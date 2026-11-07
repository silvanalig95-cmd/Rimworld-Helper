using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Region → Here and away (docs/design/interface.md): who comes and goes. Who is here from outside (guests, envoys and
    /// the like, Diplomacy/DiplomacyModel.cs; a camp on the colony's land with its trade, terms and asks); what is on its
    /// way to the colony (questions waiting, debts and orders due, visits and markets announced, a story's next stage);
    /// the colony's letters and calls on the road; who of the colony's is away (people away, the journey planned, the
    /// colony's caravans out on the land); the missions it took on or was offered; its houses; schemes against it found out
    /// (Diplomacy/Schemes.cs). Every row opens its card
    /// in the drawer; names are links. Built at most once a second; each section says in a sentence what would show there.
    /// </summary>
    public static class HereAndAwayView
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static readonly Folds folds = new Folds();

        private sealed class Away
        {
            public Caravan c;
            public ThingRef thing;
            public string link, line;
            /// <summary>The colony its people are of (the first of them), for the colony in view.</summary>
            public int home = HomesMath.None;
        }

        // The rows of the colony in view (all of them with one colony), made again when it or the model changes.
        private static List<DipRow> here = new List<DipRow>(), away = new List<DipRow>(), schemes = new List<DipRow>();
        private static int shownView = -1;
        private static DiplomacyModel shownFrom;

        private static List<Away> caravans = new List<Away>();
        private static List<string> others = new List<string>();
        private static float builtAt = -1f;
        private static DiplomacyModel dip;
        /// <summary>The view's header's "?" (UI/UiKitViews.cs), made once.</summary>
        private static ViewHeadModel head;

        public static void Draw(Rect rect)
        {
            if (Time.realtimeSinceStartup - builtAt > 1f || builtAt > Time.realtimeSinceStartup)
            {
                try
                {
                    dip = DiplomacyModel.Current;
                    Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: here and away", e);
                    caravans = new List<Away>();
                    others = new List<string>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            int letters = LettersPage.UnderWay;
            string stat = letters > 0 ? "CC_Neighbours_OnTheirWay".Translate(letters).Resolve() : caravans.Count > 0 ? "CC_JourneysPg_Stat".Translate(caravans.Count).Resolve() : null;
            // Who is here and away of the colony in view with more than one colony, everyone a click away on the header (3c).
            ColonyView.Following(true);
            if (shownView != ColonyView.Version || shownFrom != dip)
            {
                shownView = ColonyView.Version;
                shownFrom = dip;
                here = dip != null ? dip.here.Where(r => ColonyView.ShowsHome(r.home)).ToList() : new List<DipRow>();
                away = dip != null ? dip.away.Where(r => ColonyView.ShowsHome(r.home)).ToList() : new List<DipRow>();
                schemes = dip != null ? dip.schemes.Where(r => ColonyView.ShowsHome(r.home)).ToList() : new List<DipRow>();
            }
            float top = UiKit.ViewHeader(rect, Icons.Away ?? Icons.Visitor, "CC_Tab_HereAndAway".Translate(), "CC_Away_Line".Translate(), stat, null, 0f, null,
                head ??= new ViewHeadModel { article = PagesMath.ArticleOf(RegionView.HereAndAway) });
            var outRect = new Rect(rect.x, rect.y + top + 4f, rect.width, rect.height - top - 4f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                var comp = CouncilGameComponent.Instance;
                float y = 0f, w = view.width;
                // Who is here from outside, and a camp on the colony's land.
                if (dip != null) y = DiplomacyPanels.Section(y, w, DipWords.P("CC_Dip_Here_Title"), here, DipWords.P("CC_Dip_Here_None"), folds, "here");
                y = CampPanel.Section(y, w);
                y += 6f;
                // What is on its way to the colony.
                y = WorldPanel.ComingSection(y, w, comp, folds);
                // Letters and calls on the road.
                y = LettersPage.UnderWaySection(y, w, folds);
                // Who of the colony's is away: people away, the journey planned, the caravans out on the land.
                y = AwaySection(y, w);
                // Missions offered or under way, with their progress (Missions/MissionPanel.cs).
                y = MissionPanel.Draw(y, w);
                y += 10f;
                if (dip != null) y = DiplomacyPanels.Section(y, w, DipWords.P("CC_Dip_Married_Title"), dip.houses, DipWords.P("CC_Dip_Married_None"), folds, "houses");
                // Schemes against the colony found out (Diplomacy/Schemes.cs), while schemes can come or one is still remembered.
                if (dip != null && (Schemes.On || schemes.Count > 0))
                    y = DiplomacyPanels.Section(y, w, "CC_Scheme_Found_Title".Translate(), schemes, "CC_Scheme_Found_None".Translate(), folds, "schemes");
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

        private static void Build()
        {
            caravans = new List<Away>();
            others = new List<string>();
            var here = Dialog_JourneyPlan.HomeMap();
            if (ColonyState.Several)
                foreach (var plan in JourneyBook.Plans)
                    if (plan.map != null && plan.map != here)
                        others.Add("CC_Tell_At".Translate(Homes.Name(plan.map).Named("PLACE"), JourneyPanel.Status(plan).Named("TEXT")).Resolve());
            var all = Find.WorldObjects?.Caravans;
            if (all == null) return;
            foreach (var c in all)
            {
                if (c == null || c.Destroyed || !c.IsPlayerControlled) continue;
                int people = c.PawnsListForReading.Count(p => p.RaceProps.Humanlike);
                string line = "CC_JourneysPg_People".Translate(people).Resolve();
                if (c.pather != null && c.pather.Moving)
                {
                    var to = Find.WorldObjects.SettlementAt(c.pather.Destination);
                    line += " · " + (to != null ? "CC_JourneysPg_HeadingTo".Translate(ThingRefs.Link(to.LabelCap, ThingRefs.Of(to)).Named("PLACE")).Resolve()
                        : "CC_JourneysPg_OnTheMove".Translate().Resolve());
                }
                else line += " · " + "CC_JourneysPg_Resting".Translate().Resolve();
                var thing = ThingRefs.Of(c);
                var first = c.PawnsListForReading.FirstOrDefault(p => p.IsColonist && p.RaceProps.Humanlike);
                caravans.Add(new Away { c = c, thing = thing, link = ThingRefs.Link(c.LabelCap, thing), line = line, home = first != null ? Homes.KeyOf(Homes.Of(first)) : HomesMath.None });
            }
        }

        /// <summary>Who of the colony's is away: people away, the journey planned, the caravans out on the land, each opening its card.</summary>
        private static float AwaySection(float y, float w)
        {
            var shown = caravans.Where(a => ColonyView.ShowsHome(a.home)).ToList();
            int count = away.Count + shown.Count;
            y += UiKit.SectionHeading(0f, y, w, "CC_Away_Ours".Translate(), count, folds, "away", out bool open);
            if (!open) return y + 4f;
            if (away.Count > 0)
            {
                foreach (var r in away) y += DiplomacyPanels.Row(8f, y, w - 8f, r);
                y += 4f;
            }
            // The journey planned ahead from the colony in view (UI/JourneyDialog.cs), and the other colonies' plans.
            if (!JourneyBook.On) y += UiKit.Paragraph(4f, y, w - 4f, "CC_JourneysPg_Off".Translate(), UiKit.Faint) + 8f;
            else
            {
                y += JourneyPanel.Section(0f, y, w);
                foreach (var o in others) y += UiKit.Paragraph(4f, y, w - 8f, o, UiKit.Faint) + 4f;
            }
            if (shown.Count == 0 && away.Count == 0)
                return y + UiKit.Paragraph(4f, y, w - 4f, "CC_JourneysPg_NoneAway".Translate(), UiKit.Faint) + 10f;
            for (int i = 0; i < shown.Count; i++)
            {
                var a = shown[i];
                var r = new Rect(0f, y, w, 44f);
                UiKit.Zebra(r, i);
                Widgets.DrawHighlightIfMouseover(r);
                UiKit.Icon(new Rect(8f, y + 12f, 20f, 20f), Icons.Trader, UiKit.Gold);
                LinkText.Line(new Rect(36f, y + 3f, w - 80f, 22f), a.link, UiKit.HeaderColor);
                LinkText.Line(new Rect(36f, y + 24f, w - 80f, 18f), a.line, UiKit.Faint, GameFont.Tiny);
                // On the region map with its card (the card's head keeps a button for the game's world view).
                var show = new Rect(w - 32f, y + 9f, 26f, 26f);
                if (UiKit.IconButton(show, Icons.ModeMap, "CC_Lens_ShowTip".Translate()) && a.c != null && !a.c.Destroyed) Cards.ShowOnMap(a.c);
                if (Widgets.ButtonInvisible(r, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Drawer.Open(RimfolkWindow.World, a.thing, true);
                }
                y += 46f;
            }
            return y + 8f;
        }
    }
}
