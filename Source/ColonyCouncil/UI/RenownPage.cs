using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Renown (where People → Houses was until 5 November 2026): the colony's name among the peoples, how far it
    /// carries and what it is known for, the deeds it came from, who knows it and what it does (Diplomacy/RenownPanels.cs);
    /// then the colony's people known by name for their deeds, each a link to their card with their own name, and those not
    /// yet spoken of outside. Built at most once a second while it shows; what draws is only what is in sight.
    /// </summary>
    public static class RenownPage
    {
        private sealed class Named
        {
            public Pawn pawn;
            public ThingRef thing;
            public string line;
        }

        private sealed class Model
        {
            public readonly List<string> colony = new List<string>();
            public readonly List<Named> named = new List<Named>();
            public readonly List<string> unnamed = new List<string>();
            public string stat;
        }

        private static Model model;
        private static float builtAt = -1f;
        private static Vector2 scroll;
        private static float height = 400f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (model == null || Time.realtimeSinceStartup - builtAt > 1f || Time.realtimeSinceStartup < builtAt)
            {
                builtAt = Time.realtimeSinceStartup;
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
            float top = UiKit.PageHeader(rect, Icons.People, "CC_RenownPage_Header".Translate(), "CC_RenownPage_Sub".Translate(), m.stat);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(height, outRect.height - 1f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float y = 0f;
                y += UiKit.SectionTitle(0f, y, w, "CC_RenownPage_Colony".Translate(), Icons.World);
                foreach (var line in m.colony) y += UiKit.Paragraph(8f, y, w - 16f, line, UiKit.Ink) + 4f;
                y += UiKit.SectionGap;
                y += UiKit.SectionTitle(0f, y, w, "CC_RenownPage_Named".Translate(), m.named.Count, Icons.People);
                if (m.named.Count == 0)
                {
                    UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowStandard), "CC_RenownPage_NoneNamed".Translate());
                    y += UiKit.RowStandard + UiKit.CardGap;
                }
                for (int i = 0; i < m.named.Count; i++)
                {
                    var n = m.named[i];
                    float h = 46f;
                    // Only rows in sight are drawn.
                    if (y + h >= scroll.y && y <= scroll.y + outRect.height)
                    {
                        var r = new Rect(0f, y, w, h);
                        UiKit.Zebra(r, i);
                        LinkText.Line(new Rect(8f, y + 3f, w - 16f, 22f), ThingRefs.Link(n.pawn.LabelShort, n.thing), UiKit.HeaderColor);
                        UiKit.LabelFit(new Rect(8f, y + 24f, w - 16f, 20f), n.line, UiKit.Faint, GameFont.Tiny);
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
            m.colony.AddRange(RenownPanels.ColonyLines());
            if (!Renown.On) return m;
            foreach (var p in Renown.People().Where(p => p != null && p.RaceProps.Humanlike && !p.IsQuestLodger()).OrderBy(p => p.LabelShort))
            {
                string line = RenownPanels.OwnLine(p);
                if (line == null)
                {
                    if (!p.DevelopmentalStage.Baby()) m.unnamed.Add(p.LabelShort);
                    continue;
                }
                m.named.Add(new Named { pawn = p, thing = ThingRefs.Of(p), line = line });
            }
            var r = Renown.Read();
            m.stat = r != null ? RenownWords.Band(r.band, Ages.Ours).CapitalizeFirst() : null;
            return m;
        }
    }
}
