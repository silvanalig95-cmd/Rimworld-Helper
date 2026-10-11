using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Roster: every colonist at a glance (trust, life goal, office and markers for what's going on in
    /// their life), with the selected colonist's whole story beside the list.
    /// </summary>
    public static class PeoplePanel
    {
        private enum Sort
        {
            Name,
            Trust,
            Goal,
            Mood
        }

        private static Pawn selected;
        private static Sort sort = Sort.Name;
        private static Vector2 listScroll;
        private static Vector2 storyScroll;
        private static float storyHeight = 400f;

        private const float ListWidth = 320f;
        private const float RowH = 58f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.5f);
        private static readonly Texture2D GoalBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.6f, 0.85f));

        public static void Select(Pawn p)
        {
            selected = p;
            storyScroll = Vector2.zero;
        }

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike).ToList();
            switch (sort)
            {
                case Sort.Trust: pawns = pawns.OrderBy(p => comp.standings.TrustOf(p)).ToList(); break;
                case Sort.Goal: pawns = pawns.OrderByDescending(p => comp.aspirations.Active(p)?.Progress ?? -1f).ToList(); break;
                case Sort.Mood: pawns = pawns.OrderBy(p => p.needs?.mood?.CurLevelPercentage ?? 1f).ToList(); break;
                default: pawns = pawns.OrderBy(p => p.LabelShort).ToList(); break;
            }
            if (selected == null || !pawns.Contains(selected)) selected = pawns.FirstOrDefault();

            var left = new Rect(rect.x, rect.y, ListWidth, rect.height);
            var right = new Rect(rect.x + ListWidth + 12f, rect.y, rect.width - ListWidth - 12f, rect.height);

            // ---- List ----
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(left.x, left.y + 4f, 150f, 24f), "CC_People_Count".Translate(pawns.Count));
            var sortRect = new Rect(left.xMax - 160f, left.y, 160f, 28f);
            if (Widgets.ButtonText(sortRect, "CC_People_Sort".Translate(("CC_People_Sort_" + sort).Translate())))
            {
                var options = new List<FloatMenuOption>();
                foreach (Sort s in System.Enum.GetValues(typeof(Sort)))
                {
                    var local = s;
                    options.Add(new FloatMenuOption(("CC_People_Sort_" + s).Translate(), () => sort = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            var outRect = new Rect(left.x, left.y + 34f, left.width, left.height - 34f);
            var view = new Rect(0f, 0f, outRect.width - 16f, pawns.Count * (RowH + 2f));
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            foreach (var p in pawns)
            {
                // Only rows on screen are drawn.
                if (y + RowH >= listScroll.y && y <= listScroll.y + outRect.height) DrawRow(new Rect(0f, y, view.width, RowH), p, comp);
                y += RowH + 2f;
            }
            Widgets.EndScrollView();

            // ---- Story ----
            Widgets.DrawMenuSection(right);
            var inner = right.ContractedBy(10f);
            if (selected == null)
            {
                Widgets.Label(inner, "CC_People_None".Translate());
                return;
            }
            Text.Font = GameFont.Medium;
            string titles = comp.epithets.TitlesOf(selected);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 230f, 32f), (selected.LabelShortCap + (titles.NullOrEmpty() ? "" : ", " + titles)).Truncate(inner.width - 234f));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inner.xMax - 110f, inner.y, 110f, 28f), "CC_People_Jump".Translate()) && selected.Spawned)
            {
                CameraJumper.TryJumpAndSelect(selected);
                Find.MainTabsRoot.EscapeCurrentTab(false);
            }
            var journal = comp.journals.For(selected);
            if (journal != null && journal.entries.Count > 0 && Widgets.ButtonText(new Rect(inner.xMax - 224f, inner.y, 110f, 28f), "CC_People_Journal".Translate()))
                Find.WindowStack.Add(new Dialog_Journal(journal));
            GUI.color = Faint;
            Widgets.Label(new Rect(inner.x, inner.y + 32f, inner.width, 22f), Subtitle(selected));
            GUI.color = Color.white;
            StoryView.Draw(new Rect(inner.x, inner.y + 58f, inner.width, inner.height - 58f), selected, ref storyScroll, ref storyHeight);
        }

        private static string Subtitle(Pawn p)
        {
            var parts = new List<string>();
            if (p.story != null) parts.Add(p.story.TitleShortCap);
            if (p.ageTracker != null) parts.Add("CC_People_Age".Translate(p.ageTracker.AgeBiologicalYears).Resolve());
            if (p.needs?.mood != null) parts.Add("CC_People_Mood".Translate(p.needs.mood.CurLevelPercentage.ToStringPercent()).Resolve());
            return string.Join("  ·  ", parts);
        }

        private static void DrawRow(Rect r, Pawn p, CouncilGameComponent comp)
        {
            bool isSelected = p == selected;
            if (isSelected) Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.10f));
            Widgets.DrawHighlightIfMouseover(r);
            // Trust as a coloured edge: red for bitter, grey for neutral, green for devoted.
            float trust = comp.standings.TrustOf(p);
            var edge = trust >= 0f ? Color.Lerp(Color.grey, new Color(0.4f, 0.8f, 0.45f), trust / 100f) : Color.Lerp(Color.grey, new Color(0.85f, 0.35f, 0.3f), -trust / 100f);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, 4f, r.height), edge);
            Widgets.ThingIcon(new Rect(r.x + 8f, r.y + 5f, 48f, 48f), p);

            float x = r.x + 62f;
            float w = r.width - 66f;
            var office = comp.offices.OfficeOf(p);
            string name = p.LabelShort + (office != null ? "  (" + office.label + ")" : "");
            Widgets.Label(new Rect(x, r.y + 2f, w, 22f), name.Truncate(w));

            var goal = ColonyCouncilMod.Settings.aspirations ? comp.aspirations.Active(p) : null;
            Text.Font = GameFont.Tiny;
            if (goal != null)
            {
                var bar = new Rect(x, r.y + 26f, 60f, 8f);
                Widgets.FillableBar(bar, goal.Progress, GoalBar, BaseContent.ClearTex, false);
                Widgets.DrawBox(bar);
                GUI.color = Faint;
                Widgets.Label(new Rect(x + 66f, r.y + 20f, w - 66f, 18f), goal.Goal.Truncate(w - 70f));
            }
            else
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(x, r.y + 20f, w, 18f), "CC_People_NoGoal".Translate());
            }
            string markers = Markers(p, comp);
            GUI.color = Gold;
            if (!markers.NullOrEmpty()) Widgets.Label(new Rect(x, r.y + 37f, w, 18f), markers.Truncate(w));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(r, () => "CC_Trust_Header".Translate() + ": " + ("CC_Trust_" + PawnStanding.LevelFor(comp.standings.TrustOf(p))).Translate(), p.thingIDNumber ^ 0x7E01);
            if (Widgets.ButtonInvisible(r)) Select(p);
        }

        /// <summary>A short line of what's going on in their life: quirks, vendettas, secrets, teaching, circles.</summary>
        private static string Markers(Pawn p, CouncilGameComponent comp)
        {
            var s = ColonyCouncilMod.Settings;
            var parts = new List<string>();
            if (s.socialWeb)
            {
                var circle = comp.social.CliqueOf(p);
                if (circle != null) parts.Add(circle.name);
                foreach (var t in comp.social.ties.Where(t => t.kind == TieKind.Mentor && t.Involves(p) && t.skill != null))
                    parts.Add((t.a == p ? "CC_People_Teaching" : "CC_People_Learning").Translate(t.skill.label).Resolve());
                int rivals = comp.social.ties.Count(t => t.kind == TieKind.Rivals && t.announced && t.Involves(p));
                if (rivals > 0) parts.Add("CC_People_Rivals".Translate(rivals).Resolve());
            }
            if (s.quirks)
            {
                int quirks = comp.quirks.Of(p).Count();
                if (quirks > 0) parts.Add("CC_People_Quirks".Translate(quirks).Resolve());
            }
            if (s.vendettas && comp.vendettas.Of(p).Any()) parts.Add("CC_People_Vendetta".Translate().Resolve());
            if (s.secrets)
            {
                var secret = comp.secrets.Of(p);
                if (secret != null && secret.revealed) parts.Add("CC_People_SecretOut".Translate().Resolve());
                else if (secret != null && secret.knowers.Count > 0) parts.Add("CC_People_Confided".Translate().Resolve());
            }
            return string.Join(" · ", parts);
        }
    }
}
