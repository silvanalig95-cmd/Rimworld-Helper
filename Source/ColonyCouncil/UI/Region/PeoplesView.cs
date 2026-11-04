using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Region → Peoples (docs/design/interface.md): two modes. Peoples: what is owed each way (the owed strip,
    /// Diplomacy/DiplomacyPanels.cs), then a card for each people the colony can know (its stance, kind, wars and the world
    /// events about it, and where the colony has dealings its head, its ties in their age's words, what is under way and
    /// the acts that fit), then what the peoples remember of the colony. Faces: the people the colony knows by name, with
    /// a way to write, then who's out there with the colony on their mind (old foes, those who left, the rest) and the faces
    /// it keeps up with. Every card and row opens its card in the drawer; names are links. Built at most once a second.
    /// </summary>
    public static class PeoplesView
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static readonly Folds folds = new Folds("remember");
        private const float HistoryW = 120f;

        private sealed class Row
        {
            public Faction f;
            public ThingRef thing;
            public string standing, kind, wars, events;
            public Color color;
            public DipPeople dip;
            public float width = -1f, height;
        }

        private static List<Row> rows = new List<Row>();
        private static float builtAt = -1f;
        private static DiplomacyModel dip;

        /// <summary>A people the colony can know: of humans, not hidden, not gone.</summary>
        public static bool Listed(Faction f) => f != null && !f.IsPlayer && !f.Hidden && !f.defeated && f.def != null && f.def.humanlikeFaction;

        public static void Draw(Rect rect)
        {
            if (Time.realtimeSinceStartup - builtAt > 1f || builtAt > Time.realtimeSinceStartup)
            {
                try
                {
                    dip = DiplomacyModel.Current;
                    rows = Build(dip);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("region window: the peoples", e);
                    rows = new List<Row>();
                }
                builtAt = Time.realtimeSinceStartup;
            }
            bool faces = RegionView.faces;
            string stat = faces ? FacesStat() : DiplomacyPanels.OwedStat(dip) ?? (rows.Count > 0 ? "CC_Peoples_Stat".Translate(rows.Count).Resolve() : null);
            float top = UiKit.ViewHeader(rect, faces ? Icons.Visitor : Icons.Trust ?? Icons.People, "CC_Tab_Peoples".Translate(),
                (faces ? "CC_Faces_Line" : "CC_Peoples_Line").Translate(), stat, RegionView.PeopleModes, faces ? 1 : 0, out int mode, RegionView.PeopleModeIcons,
                RegionView.PeopleModeTips);
            if (mode >= 0)
            {
                RegionView.faces = mode == 1;
                scroll = Vector2.zero;
            }
            var outRect = new Rect(rect.x, rect.y + top + 4f, rect.width, rect.height - top - 4f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = RegionView.faces ? Faces(view.width, outRect.height) : Peoples(view.width, outRect.height);
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

        // ================= Peoples =================

        private static List<Row> Build(DiplomacyModel m)
        {
            var list = new List<Row>();
            var all = Find.FactionManager?.AllFactionsListForReading;
            if (all == null) return list;
            var ws = ColonyCouncilMod.Settings.worldSituations ? WorldSituations.Instance : null;
            foreach (var f in all)
            {
                if (!Listed(f)) continue;
                var r = new Row { f = f, thing = ThingRefs.Of(f), standing = RegionMap.StandingText(f), color = RegionMap.StandingColor(f), kind = f.def.LabelCap };
                if (f.def.permanentEnemy) r.wars = "CC_Peoples_EveryonesFoe".Translate().Resolve();
                else if (Facts.Counts(f))
                {
                    var foes = new List<string>();
                    foreach (var g in all)
                        if (g != f && Facts.Counts(g) && f.HostileTo(g))
                            foes.Add(ThingRefs.Link(g.Name, ThingRefs.Of(g)));
                    if (foes.Count > 0) r.wars = "CC_Peoples_Wars".Translate(TextUtil.JoinAnd(foes).Named("FOES")).Resolve();
                }
                if (ws != null)
                {
                    var about = new List<string>();
                    foreach (var s in ws.Running)
                        if ((s.ctx?.faction == f || s.ctx?.enemy == f) && !s.Label.NullOrEmpty())
                            about.Add(s.Label);
                    if (about.Count > 0) r.events = "CC_Peoples_Events".Translate(TextUtil.JoinAnd(about).Named("EVENTS")).Resolve();
                }
                r.dip = m?.peoples.FirstOrDefault(p => p.people == f);
                list.Add(r);
            }
            // The peoples the colony has dealings with first (ties, then within reach), then allies and friends, enemies last.
            return list.OrderByDescending(r => r.dip?.weight ?? -1).ThenByDescending(r => r.f.HasGoodwill ? r.f.PlayerGoodwill : -200).ThenBy(r => r.f.Name).ToList();
        }

        private static float Peoples(float w, float viewH)
        {
            float y = 0f;
            if (dip != null) y = DiplomacyPanels.Owed(y, w, dip, folds);
            y += UiKit.SectionHeading(0f, y, w, "CC_Peoples_Stance".Translate(), rows.Count, folds, "peoples", out bool open);
            if (open)
            {
                if (rows.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.People, "CC_Peoples_None".Translate(), 60f);
                foreach (var r in rows)
                {
                    float h = Measure(r, w);
                    // Only the cards in sight are drawn.
                    if (y + h >= scroll.y && y <= scroll.y + viewH) Card(new Rect(0f, y, w, h), r);
                    y += h + 6f;
                }
            }
            y += 8f;
            // What each people remembers of the colony (World/FactionMemoryPanel.cs).
            return FactionMemoryPanel.Draw(y, w);
        }

        private static float Measure(Row r, float w)
        {
            if (Mathf.Abs(r.width - w) < 0.5f) return r.height;
            r.width = w;
            float tw = w - 20f;
            Text.Font = GameFont.Small;
            float h = 8f + 26f + 18f;
            if (!r.wars.NullOrEmpty()) h += LinkText.Height(r.wars, tw) + 2f;
            if (!r.events.NullOrEmpty()) h += Text.CalcHeight(r.events, tw) + 2f;
            if (r.dip != null) h += 4f + DiplomacyPanels.PeopleBody(0f, 0f, tw, r.dip, false);
            r.height = Mathf.Max(h + 8f, 60f);
            return r.height;
        }

        /// <summary>A people's card: its chip and stance, its acts and its history; its kind, wars and events; its diplomacy.</summary>
        private static void Card(Rect card, Row r)
        {
            bool showing = Drawer.Current(RimfolkWindow.World) == r.thing;
            UiKit.Card(card, UiKit.WithAlpha(r.color, 0.6f), showing, true);
            float x = card.x + 10f, y = card.y + 8f, tw = card.width - 20f;
            // The acts at the right: their history, and what the colony can do with them now.
            float right = card.xMax - 10f;
            var hist = new Rect(right - HistoryW, y, HistoryW, 24f);
            if (Widgets.ButtonText(hist, "CC_Peoples_TheirHistory".Translate())) WorldHistoryPage.ShowFor(Refs.Of(r.f), Ref.None);
            TooltipHandler.TipRegion(hist, "CC_Peoples_TheirHistoryTip".Translate(r.f.Name.Named("PEOPLE")));
            right = hist.x - 6f;
            if (r.dip != null)
                foreach (var a in r.dip.acts)
                {
                    float aw = Mathf.Min(UiKit.ActButtonWidth(a.label), 170f);
                    if (right - aw < x + 160f) break;
                    if (DiplomacyPanels.ActButton(new Rect(right - aw, y, aw, 24f), a)) builtAt = -1f;
                    right -= aw + 6f;
                }
            float chip = UiKit.FactionChip(x, y + 1f, r.f, 22f, Mathf.Min(260f, (right - x) * 0.6f), true);
            UiKit.LabelFit(new Rect(x + chip + 10f, y + 1f, Mathf.Max(0f, right - x - chip - 10f), 22f), r.standing, r.color);
            y += 26f;
            UiKit.LabelFit(new Rect(x, y, tw, 18f), r.kind, UiKit.Faint, GameFont.Tiny);
            y += 18f;
            if (!r.wars.NullOrEmpty()) y += LinkText.Draw(new Rect(x, y, tw, 999f), r.wars, r.f.def.permanentEnemy ? UiKit.Faint : UiKit.Warn) + 2f;
            if (!r.events.NullOrEmpty()) y += UiKit.Paragraph(x, y, tw, r.events, UiKit.WorldColor) + 2f;
            if (r.dip != null) DiplomacyPanels.PeopleBody(x, y + 4f, tw, r.dip, true);
            // A click anywhere else on the card opens the people's card in the drawer.
            if (Widgets.ButtonInvisible(card, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Drawer.Open(RimfolkWindow.World, r.thing, true);
            }
        }

        // ================= Faces =================

        private static string FacesStat()
        {
            int n = NotableBook.On ? NotableBook.Instance?.notables.Count(x => x != null && x.Alive) ?? 0 : 0;
            return n > 0 ? "CC_Faces_Stat".Translate(n.Named("COUNT")).Resolve() : null;
        }

        private static float Faces(float w, float viewH)
        {
            var comp = CouncilGameComponent.Instance;
            float y = 0f;
            // The people the colony knows by name, each with a way to write (UI/WorldPages.cs).
            y = LettersPage.KnownSection(y, w, folds, scroll.y, scroll.y + viewH);
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Foes, folds);
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Left, folds);
            y = WorldPanel.OutThereSection(y, w, comp, OutGroup.Others, folds);
            // The faces that come back, and what travellers have come to know the colony for.
            return WorldPanel.Known(y, w, comp);
        }
    }
}
