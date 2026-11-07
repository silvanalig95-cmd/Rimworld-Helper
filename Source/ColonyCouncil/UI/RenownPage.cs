using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Renown (where People → Houses was until 5 November 2026): since phase 4 of the review, part 1 (one name), each
    /// of the colony's people by their one "known as" (Epithets/KnownAs.cs): the colony's name for them and what the peoples
    /// know them by, in one line, each a link to their card, with the details under it (the colony's other names for them,
    /// what the others say of them); then those with no name yet. The colony's own name is its card on Council → Ways and on a
    /// home's card (Character/ColonyName.cs); its band stays in this page's header. Built at most once a second while it
    /// shows; what draws is only what is in sight.
    /// </summary>
    public static class RenownPage
    {
        private sealed class Named
        {
            public Pawn pawn;
            public ThingRef thing;
            public string line;
            public string details;
            public float height;
        }

        private sealed class Model
        {
            public readonly List<Named> named = new List<Named>();
            public readonly List<string> unnamed = new List<string>();
            public string stat;
        }

        private static Model model;
        private static float builtAt = -1f;
        private static int builtView = -1;
        private static Vector2 scroll;
        private static float height = 400f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (model == null || Time.realtimeSinceStartup - builtAt > 1f || Time.realtimeSinceStartup < builtAt || builtView != ColonyView.Version)
            {
                builtAt = Time.realtimeSinceStartup;
                builtView = ColonyView.Version;
                try
                {
                    model = Build();
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("renown page", e);
                    model ??= new Model();
                }
            }
            var m = model;
            float top = UiKit.PageHeader(rect, Icons.People, "CC_Name_PeopleTitle".Translate(), "CC_Name_PeopleSub".Translate(), m.stat);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(height, outRect.height - 1f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float y = 0f;
                y += UiKit.SectionTitle(0f, y, w, "CC_RenownPage_Named".Translate(), m.named.Count, Icons.People);
                if (m.named.Count == 0)
                {
                    float eh = UiKit.Paragraph(8f, y, w - 16f, "CC_Name_PeopleNone".Translate(), UiKit.Faint);
                    y += eh + UiKit.CardGap;
                }
                for (int i = 0; i < m.named.Count; i++)
                {
                    var n = m.named[i];
                    if (n.height <= 0f)
                    {
                        Text.Font = GameFont.Tiny;
                        n.height = 26f + Mathf.Max(20f, Text.CalcHeight(n.line, w - 16f)) + (n.details.NullOrEmpty() ? 0f : Text.CalcHeight(n.details, w - 16f)) + 4f;
                        Text.Font = GameFont.Small;
                    }
                    float h = n.height;
                    // Only rows in sight are drawn.
                    if (y + h >= scroll.y && y <= scroll.y + outRect.height)
                    {
                        var r = new Rect(0f, y, w, h);
                        UiKit.Zebra(r, i);
                        LinkText.Line(new Rect(8f, y + 3f, w - 16f, 22f), ThingRefs.Link(n.pawn.LabelShort, n.thing), UiKit.HeaderColor);
                        Text.Font = GameFont.Tiny;
                        float lh = Mathf.Max(20f, Text.CalcHeight(n.line, w - 16f));
                        GUI.color = UiKit.Ink;
                        Widgets.Label(new Rect(8f, y + 24f, w - 16f, lh), n.line);
                        if (!n.details.NullOrEmpty())
                        {
                            GUI.color = UiKit.Faint;
                            Widgets.Label(new Rect(8f, y + 24f + lh, w - 16f, h - 24f - lh), n.details);
                        }
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                    }
                    y += h;
                }
                if (m.unnamed.Count > 0)
                    y += 6f + UiKit.Paragraph(8f, y + 6f, w - 16f, "CC_RenownPage_Unnamed".Translate(m.unnamed.ToCommaList(true).Named("NAMES")), UiKit.Faint, GameFont.Tiny);
                height = y + 10f;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static Model Build()
        {
            var m = new Model();
            var people = Renown.On ? Renown.People() : PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList();
            // The colony in view's own people with more than one colony (the colony's name among the peoples stays one), everyone a click away.
            foreach (var p in people.Where(p => p != null && p.RaceProps.Humanlike && !p.IsQuestLodger() && ColonyView.Shows(p)).OrderBy(p => p.LabelShort))
            {
                var k = KnownAs.Of(p);
                string line = KnownAs.Line(k);
                if (line == null)
                {
                    if (!p.DevelopmentalStage.Baby()) m.unnamed.Add(p.LabelShort);
                    continue;
                }
                var details = KnownAs.Details(p, k);
                m.named.Add(new Named { pawn = p, thing = ThingRefs.Of(p), line = line, details = details.Count == 0 ? null : string.Join(" ", details) });
            }
            var r = Renown.On ? Renown.Read() : null;
            m.stat = r != null ? RenownWords.Band(r.band, Ages.Ours).CapitalizeFirst() : null;
            return m;
        }
    }
}
