using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Region → Peoples (docs/design/diplomacy-screen.md, step A; docs/design/interface.md): two modes. Peoples: the list
    /// of peoples on the left (UI/Region/PeoplesList.cs: a search, the filters, the sort, closest first by default; the
    /// groups allies, at peace and at war; each row with its stance, regard, nearest town's days, mail and age; the web of
    /// who stands with whom at its foot) and the chosen people's page on the right (UI/Region/PeoplePage.cs: its header,
    /// then Overview with "Do something with...", Towns, People, Dealings and History), with all peoples at once (what is
    /// owed each way and what they remember) as the list's first row. The list and the page share the view's width by
    /// PeoplesViewMath's layout, the list narrower when the drawer is open. Choosing a people lights its towns on the
    /// region map. Faces: the people the colony knows by name across every people, with a way to write, then who is out
    /// there with the colony on their mind (old foes, those who left, the rest) and the faces it keeps up with. Its model
    /// is built at most once a second (UI/Region/PeoplesModel.cs).
    /// </summary>
    public static class PeoplesView
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static readonly Folds folds = new Folds("remember");

        /// <summary>The view's header: its modes (Peoples, Faces, and Gods while the gods are on) and its "?" (UI/UiKitViews.cs), made again when the gods are switched.</summary>
        private static ViewHeadModel head => headModel != null && headGods == Gods.On ? headModel : headModel = MakeHead();
        private static ViewHeadModel headModel;
        private static bool headGods;

        private static ViewHeadModel MakeHead()
        {
            headGods = Gods.On;
            int modes = headGods ? 3 : 2;
            var m = new ViewHeadModel { article = PagesMath.ArticleOf(RegionView.Peoples) };
            m.labels.AddRange(RegionView.PeopleModes.Take(modes));
            m.tips.AddRange(RegionView.PeopleModeTips.Take(modes));
            m.pick = i =>
            {
                RegionView.faces = i == 1;
                RegionView.gods = i == 2;
                scroll = Vector2.zero;
            };
            return m;
        }

        /// <summary>A people the colony can know: of humans, not hidden, not gone.</summary>
        public static bool Listed(Faction f) => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && f.def != null && f.def.humanlikeFaction;

        /// <summary>Chooses a people for the page (a row, the web, a people's card opened in full): its towns lit on the region map.</summary>
        public static void Choose(Faction f)
        {
            if (f == null) return;
            if (RegionView.chosen != f.loadID)
            {
                RegionView.chosen = f.loadID;
                RegionView.target = ActTarget.People;
                RegionView.targetTown = RegionView.targetPerson = -1;
                PeopleActs.Dirty();
            }
            RegionView.faces = false;
            RegionView.gods = false;
            RegionView.lit = f;
        }

        public static void ResetScroll() => scroll = Vector2.zero;

        public static void Draw(Rect rect)
        {
            PeoplesModel.Refresh();
            // The gods (Gods/GodsPage.cs), the view's third mode while the gods are on.
            bool gods = RegionView.gods && Gods.On;
            bool faces = !gods && RegionView.faces;
            var chosen = faces || gods ? null : PeoplesModel.Chosen();
            if (chosen != null) RegionView.lit = chosen.f;
            string stat = gods ? GodsPage.Stat() : faces ? FacesStat() : PeoplesModel.entries.Count > 0 ? "CC_Peoples_Stat".Translate(PeoplesModel.entries.Count).Resolve() : null;
            head.lit = gods ? 2 : faces ? 1 : 0;
            // The colony in view's neighbours with more than one colony, everyone a click away on the header (3c).
            ColonyView.Following(faces);
            float top = UiKit.ViewHeader(rect, gods ? GodSymbols.Pantheon ?? Icons.Ways : faces ? Icons.Visitor : Icons.Trust ?? Icons.People,
                (gods ? "CC_Gods_Title" : "CC_Tab_Peoples").Translate(), (gods ? "CC_Gods_Line" : faces ? "CC_Faces_Line" : "CC_Peoples_Line").Translate(), stat, null, 0f, null, head);
            var body = new Rect(rect.x, rect.y + top + 4f, rect.width, rect.height - top - 4f);
            try
            {
                if (gods) GodsPage.Draw(body);
                else if (faces) FacesMode(body);
                else
                {
                    // The list on the left, the chosen people's page on the right (PeoplesViewMath's layout, once per width).
                    float lw = PeoplesViewMath.ListWidth(body.width);
                    PeoplesList.Draw(new Rect(body.x, body.y, lw, body.height));
                    var page = new Rect(body.x + lw + PeoplesViewMath.Gap, body.y, PeoplesViewMath.PageWidth(body.width), body.height);
                    Widgets.DrawBoxSolid(new Rect(page.x - PeoplesViewMath.Gap / 2f - 0.5f, page.y, 1f, page.height), UiKit.DividerColor);
                    PeoplePage.Draw(page, chosen);
                }
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ================= Faces =================

        private static string FacesStat()
        {
            int n = NotableBook.On ? NotableBook.Instance?.notables.Count(x => x != null && x.Alive) ?? 0 : 0;
            return n > 0 ? "CC_Faces_Stat".Translate(n.Named("COUNT")).Resolve() : null;
        }

        private static void FacesMode(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y;
                float w = view.width;
                // A window made larger: the faces known by name beside who is out there (RegionViewsMath.Columns).
                if (RegionViewsMath.Columns(w) == 1) y = Faces(w, outRect.height);
                else
                {
                    float cw = RegionViewsMath.ColumnWidth(w), h = Mathf.Max(height, outRect.height);
                    Widgets.BeginGroup(new Rect(0f, 0f, cw, h));
                    float left = LettersPage.KnownSection(0f, cw, folds, scroll.y, scroll.y + outRect.height);
                    Widgets.EndGroup();
                    Widgets.BeginGroup(new Rect(cw + RegionViewsMath.ColumnGap, 0f, cw, h));
                    float right = OutThere(0f, cw);
                    Widgets.EndGroup();
                    y = Mathf.Max(left, right);
                }
                height = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float Faces(float w, float viewH)
        {
            float y = 0f;
            // The people the colony knows by name, each with a way to write (UI/WorldPages.cs).
            y = LettersPage.KnownSection(y, w, folds, scroll.y, scroll.y + viewH);
            return OutThere(y, w);
        }

        /// <summary>Who is out there with the colony on their mind (old foes, those who left, the rest), and the faces that come back.</summary>
        private static float OutThere(float y, float w)
        {
            var comp = CouncilGameComponent.Instance;
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Foes, folds);
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Left, folds);
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Others, folds);
            // The faces that come back, and what travellers have come to know the colony for.
            return WorldPanel.Known(y, w, comp);
        }
    }
}
