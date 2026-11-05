using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Houses: the colony's families, each from the game's own family ties among the colony's people (blood and
    /// marriage), its eldest first, everyone in it a link to their card; then the houses the colony has heard of out in
    /// the region (the founding families of new towns the world ledger keeps), each a link to its house card. Built at
    /// most once a second while it shows; what draws is only what is in sight.
    /// </summary>
    public static class HousesPanel
    {
        private sealed class Family
        {
            public string name;
            /// <summary>Its house's card and its renown in words, once the colony keeps it as a house (Diplomacy/Renown.cs).</summary>
            public ThingRef house;
            public string renown;
            public readonly List<Pawn> members = new List<Pawn>();
            public readonly List<ThingRef> refs = new List<ThingRef>();
            public readonly List<string> labels = new List<string>();
        }

        private sealed class Model
        {
            public readonly List<Family> families = new List<Family>();
            public int alone;
            /// <summary>The houses known out in the region: each a line with its link.</summary>
            public readonly List<string> known = new List<string>();
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
                    CouncilLog.ErrorOnce("houses", e);
                    model ??= new Model();
                }
            }
            var m = model;
            float top = UiKit.PageHeader(rect, Icons.People, "CC_Houses_Header".Translate(), "CC_Houses_Sub".Translate(), m.stat);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(height, outRect.height - 1f));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                float y = 0f;
                if (m.families.Count == 0 && m.known.Count == 0)
                {
                    y += UiKit.EmptyState(0f, y, w, Icons.People, "CC_Houses_None".Translate(), 140f);
                    height = y;
                    return;
                }
                y += UiKit.SectionTitle(0f, y, w, "CC_Houses_AtHome".Translate(), m.families.Count, Icons.People);
                if (m.families.Count == 0)
                {
                    UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowStandard), "CC_Houses_NoneAtHome".Translate());
                    y += UiKit.RowStandard + UiKit.CardGap;
                }
                foreach (var f in m.families) y += DrawFamily(f, 0f, y, w, outRect.height) + UiKit.CardGap;
                if (m.alone > 0) y += UiKit.Paragraph(8f, y, w - 16f, "CC_Houses_Alone".Translate(m.alone), UiKit.Faint, GameFont.Tiny) + 4f;
                y += UiKit.SectionGap - UiKit.CardGap;
                if (m.known.Count > 0)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_Houses_Known".Translate(), m.known.Count, Icons.World);
                    for (int i = 0; i < m.known.Count; i++)
                    {
                        // Only lines in sight are drawn.
                        if (y + UiKit.RowStandard >= scroll.y && y <= scroll.y + outRect.height)
                        {
                            UiKit.Zebra(new Rect(0f, y, w, UiKit.RowStandard), i);
                            LinkText.Line(new Rect(8f, y, w - 16f, UiKit.RowStandard), m.known[i], UiKit.Ink);
                        }
                        y += UiKit.RowStandard;
                    }
                }
                height = y + 10f;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float DrawFamily(Family f, float x, float y, float w, float sight)
        {
            // The height first (chips wrap), then the card only when in sight.
            // Its renown, a line under the name, when the colony keeps it as a house.
            float top = f.renown.NullOrEmpty() ? 30f : 50f;
            float cx = x + 10f, cy = y + top;
            for (int i = 0; i < f.members.Count; i++)
            {
                float cw = UiKit.ThingChipWidth(f.labels[i], w - 20f);
                if (cx > x + 10f && cx + cw > x + w - 10f)
                {
                    cx = x + 10f;
                    cy += UiKit.ChipHeight + 4f;
                }
                cx += cw + 6f;
            }
            float h = cy + UiKit.ChipHeight + 10f - y;
            if (y + h < scroll.y || y > scroll.y + sight) return h;
            UiKit.Card(new Rect(x, y, w, h), UiKit.WithAlpha(UiKit.Gold, 0.5f), false, false);
            if (f.house.IsNone) UiKit.LabelFit(new Rect(x + UiKit.CardInset, y + 4f, w - 2f * UiKit.CardInset, 24f), f.name, UiKit.Heading);
            else LinkText.Line(new Rect(x + UiKit.CardInset, y + 4f, w - 2f * UiKit.CardInset, 24f), ThingRefs.Link(f.name, f.house), UiKit.Heading);
            if (!f.renown.NullOrEmpty())
                UiKit.LabelFit(new Rect(x + UiKit.CardInset, y + 28f, w - 2f * UiKit.CardInset, 20f), f.renown, UiKit.Faint, GameFont.Tiny);
            cx = x + 10f;
            cy = y + top;
            for (int i = 0; i < f.members.Count; i++)
            {
                float cw = UiKit.ThingChipWidth(f.labels[i], w - 20f);
                if (cx > x + 10f && cx + cw > x + w - 10f)
                {
                    cx = x + 10f;
                    cy += UiKit.ChipHeight + 4f;
                }
                cx += UiKit.ThingChip(cx, cy, f.refs[i], f.labels[i], f.members[i], null, null, w - 20f) + 6f;
            }
            return h;
        }

        /// <summary>A pair counts as one family: kin by blood, or married.</summary>
        private static bool Kin(PawnRelationDef d) => d != null && (d.familyByBloodRelation || d == PawnRelationDefOf.Spouse);

        private static Model Build()
        {
            var m = new Model();
            var ours = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(p => p.RaceProps.Humanlike && !p.IsQuestLodger()).ToList();
            var index = new Dictionary<Pawn, int>();
            for (int i = 0; i < ours.Count; i++) index[ours[i]] = i;
            // One family per group joined by kinship among the colony's people.
            var root = Enumerable.Range(0, ours.Count).ToArray();
            int Top(int i)
            {
                while (root[i] != i) i = root[i] = root[root[i]];
                return i;
            }
            for (int i = 0; i < ours.Count; i++)
            {
                var rel = ours[i].relations;
                if (rel == null) continue;
                foreach (var r in rel.DirectRelations)
                    if (r.otherPawn != null && Kin(r.def) && index.TryGetValue(r.otherPawn, out int j))
                        root[Top(i)] = Top(j);
            }
            var groups = new Dictionary<int, Family>();
            for (int i = 0; i < ours.Count; i++)
            {
                int g = Top(i);
                if (!groups.TryGetValue(g, out var f)) groups[g] = f = new Family();
                f.members.Add(ours[i]);
            }
            foreach (var f in groups.Values)
            {
                if (f.members.Count < 2)
                {
                    m.alone += f.members.Count;
                    continue;
                }
                f.members.Sort((a, b) => (b.ageTracker?.AgeBiologicalTicks ?? 0L).CompareTo(a.ageTracker?.AgeBiologicalTicks ?? 0L));
                // Named by the surname most of them share, else after the eldest ("Mira's kin").
                string surname = f.members.Select(p => (p.Name as NameTriple)?.Last).Where(s => !s.NullOrEmpty())
                    .GroupBy(s => s).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                f.name = surname != null
                    ? "CC_Region_House_Family".Translate(surname.Named("NAME")).Resolve().CapitalizeFirst()
                    : "CC_Region_House_Kin".Translate(f.members[0].LabelShort.Named("NAME")).Resolve().CapitalizeFirst();
                foreach (var p in f.members)
                {
                    f.refs.Add(ThingRefs.Of(p));
                    f.labels.Add(p.LabelShort + (p.ageTracker != null ? ", " + p.ageTracker.AgeBiologicalYears : ""));
                }
                // Kept as a house of the colony's (two or more of one name): its card and its renown (Diplomacy/Renown.cs).
                var kept = surname != null ? RenownPanels.OursNamed(surname) : null;
                if (kept != null)
                {
                    f.house = ThingRefs.Of(kept.house);
                    f.renown = RenownPanels.ListLine(kept);
                }
                m.families.Add(f);
            }
            m.families.Sort((a, b) => b.members.Count != a.members.Count ? b.members.Count.CompareTo(a.members.Count) : string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            // The houses out in the region, town by town.
            var houses = WorldLedger.Instance?.Houses;
            if (houses != null)
                foreach (var h in houses.Where(h => h != null && !h.house.IsNone && !h.ours).OrderBy(h => WorldLedger.TownRecord(h.town)?.name ?? "").ThenBy(h => h.name).Take(60))
                {
                    var town = ThingRefs.Of(Ref.Town(h.town));
                    string townName = !town.IsNone && ThingRefs.Exists(town) ? ThingRefs.Link(town) : WorldLedger.TownRecord(h.town)?.name;
                    string house = ThingRefs.Link(RegionWords.HouseName(h).CapitalizeFirst(), ThingRefs.Of(h.house));
                    string line = townName.NullOrEmpty() ? house : "CC_Houses_KnownLine".Translate(house.Named("HOUSE"), townName.Named("TOWN")).Resolve();
                    // Its renown, after it (Diplomacy/Renown.cs).
                    string renown = RenownPanels.ListLine(h);
                    m.known.Add(renown.NullOrEmpty() ? line : "CC_Renown_KnownLine".Translate(line.Named("LINE"), renown.Named("RENOWN")).Resolve());
                }
            m.stat = m.families.Count > 0 ? "CC_Houses_Stat".Translate(m.families.Count).Resolve() : null;
            return m;
        }
    }
}
