using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Roster: every colonist at a glance (portrait ringed in their trust color, life goal, office and small
    /// icons for what's going on in their life), with the selected colonist's whole story beside the list.
    /// </summary>
    [StaticConstructorOnStartup]
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
        private static Sort? sortMemo;

        /// <summary>The sort order, remembered between sessions (UI/PageMemory.cs).</summary>
        private static Sort sort
        {
            get => sortMemo ??= PageMemory.GetEnum("roster.sort", Sort.Name);
            set
            {
                sortMemo = value;
                PageMemory.SetEnum("roster.sort", value);
            }
        }
        private static Vector2 listScroll;
        private static Vector2 storyScroll;
        private static float storyHeight = 400f;
        /// <summary>The story sections folded away on this page (the Story tab keeps its own).</summary>
        private static readonly Folds storyFolds = new Folds();

        private const float ListWidth = 320f;
        private const float RowH = 60f;
        private const float PortraitSize = 46f;

        private static readonly Texture2D GoalBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.6f, 0.85f));
        private static readonly Color GoalColor = new Color(0.75f, 0.6f, 0.85f);

        // The sorted list, refreshed twice a second or when the sort changes.
        private static List<Pawn> sorted;
        private static float sortedAt = -1f;
        private static Sort sortedBy;

        /// <summary>One small icon in a row, with what it means as its tooltip.</summary>
        private struct Marker
        {
            public Texture2D icon;
            public Color color;
            public string tip;
            public string count;
        }

        /// <summary>What a row shows besides the name, worked out about once a second.</summary>
        private class RowInfo
        {
            public string office, goal, circle;
            public float goalProgress = -1f;
            public List<Marker> markers = new List<Marker>();
        }

        public static void Select(Pawn p)
        {
            selected = p;
            storyScroll = Vector2.zero;
        }

        private static List<Pawn> Pawns(CouncilGameComponent comp)
        {
            if (sorted != null && sortedBy == sort && Time.realtimeSinceStartup - sortedAt < 0.5f) return sorted;
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike).ToList();
            switch (sort)
            {
                case Sort.Trust: pawns = pawns.OrderBy(p => comp.standings.TrustOf(p)).ToList(); break;
                case Sort.Goal: pawns = pawns.OrderByDescending(p => comp.aspirations.Active(p)?.Progress ?? -1f).ToList(); break;
                case Sort.Mood: pawns = pawns.OrderBy(p => p.needs?.mood?.CurLevelPercentage ?? 1f).ToList(); break;
                default: pawns = pawns.OrderBy(p => p.LabelShort).ToList(); break;
            }
            sorted = pawns;
            sortedBy = sort;
            sortedAt = Time.realtimeSinceStartup;
            return pawns;
        }

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            var pawns = Pawns(comp);
            if (selected == null || !pawns.Contains(selected)) selected = pawns.FirstOrDefault();

            float top = UiKit.PageHeader(rect, Icons.Roster, "CC_UI_Title_Roster".Translate(), "CC_UI_Sub_Roster".Translate(),
                "CC_UI_Stat_People".Translate(pawns.Count).Resolve(), null, 172f);
            var sortRect = new Rect(rect.xMax - 166f, rect.y + 6f, 166f, 30f);
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

            float listW = Mathf.Min(ListWidth, rect.width * 0.45f);
            var left = new Rect(rect.x, rect.y + top, listW, rect.height - top);
            var right = new Rect(rect.x + listW + 12f, rect.y + top, rect.width - listW - 12f, rect.height - top);

            // ---- List ----
            var view = new Rect(0f, 0f, left.width - 16f, pawns.Count * (RowH + 4f));
            Widgets.BeginScrollView(left, ref listScroll, view);
            float y = 0f;
            if (pawns.Count == 0) UiKit.EmptyState(0f, 0f, view.width, Icons.People, "CC_People_None".Translate(), 120f);
            foreach (var p in pawns)
            {
                // Only rows on screen are drawn.
                if (y + RowH >= listScroll.y && y <= listScroll.y + left.height) DrawRow(new Rect(0f, y, view.width, RowH), p, comp);
                y += RowH + 4f;
            }
            Widgets.EndScrollView();

            // ---- Story ----
            Widgets.DrawMenuSection(right);
            var inner = right.ContractedBy(10f);
            if (selected == null)
            {
                UiKit.EmptyState(inner, Icons.People, "CC_People_None".Translate());
                return;
            }
            const float storyPortrait = 56f;
            UiKit.Portrait(new Rect(inner.x, inner.y, storyPortrait, storyPortrait), selected, UiKit.TrustColor(comp.standings.TrustOf(selected)));
            float tx = inner.x + storyPortrait + 10f;
            var journal = comp.journals.For(selected);
            bool hasJournal = journal != null && journal.entries.Count > 0;
            float buttonsW = hasJournal ? 228f : 114f;
            string titles = UiCache.Get(selected, 41, () => comp.epithets.TitlesOf(selected));
            string name = selected.LabelShortCap + (titles.NullOrEmpty() ? "" : ", " + titles);
            UiKit.LabelFit(new Rect(tx, inner.y, Mathf.Max(0f, inner.xMax - tx - buttonsW - 32f), 32f), name, Color.white, GameFont.Medium);
            // The pin to This week, beside the buttons.
            var memory = WeekMemory.Instance;
            if (memory != null && WeekPanel.PinButton(new Rect(inner.xMax - buttonsW - 24f, inner.y + 4f, 20f, 20f), memory.IsPinned(selected), true))
                memory.TogglePin(selected);
            if (Widgets.ButtonText(new Rect(inner.xMax - 110f, inner.y, 110f, 28f), "CC_People_Jump".Translate()) && selected.Spawned)
            {
                CameraJumper.TryJumpAndSelect(selected);
                Find.MainTabsRoot.EscapeCurrentTab(false);
            }
            if (hasJournal && Widgets.ButtonText(new Rect(inner.xMax - 224f, inner.y, 110f, 28f), "CC_People_Journal".Translate()))
                Find.WindowStack.Add(new Dialog_Journal(journal));
            UiKit.LabelFit(new Rect(tx, inner.y + 32f, Mathf.Max(0f, inner.xMax - tx), 22f), UiCache.Get(selected, 42, () => Subtitle(selected)), UiKit.Faint);
            float storyTop = storyPortrait + 8f;
            StoryView.Draw(new Rect(inner.x, inner.y + storyTop, inner.width, inner.height - storyTop), selected, ref storyScroll, ref storyHeight, storyFolds);
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
            UiKit.Card(r, null, isSelected);
            if (isSelected) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), UiKit.Gold);
            float trust = comp.standings.TrustOf(p);
            UiKit.Portrait(new Rect(r.x + 8f, r.y + (r.height - PortraitSize) / 2f, PortraitSize, PortraitSize), p, UiKit.TrustColor(trust));
            var info = UiCache.Get(p, 40, () => Info(p, comp));

            float x = r.x + PortraitSize + 16f;
            float w = r.xMax - 6f - x;
            // Name, with their office as a gold pill on the right.
            float pillW = 0f;
            if (info.office != null)
            {
                pillW = Mathf.Min(UiKit.PillWidth(info.office, Icons.Office), w * 0.5f);
                UiKit.Pill(r.xMax - 6f - pillW, r.y + 5f, info.office, UiKit.Gold, Icons.Office, null, pillW);
                pillW += 6f;
            }
            UiKit.LabelFit(new Rect(x, r.y + 3f, w - pillW, 22f), p.LabelShort, isSelected ? UiKit.Gold : Color.white);

            // Life goal: a small bar and its words.
            if (info.goal != null)
            {
                UiKit.Icon(new Rect(x, r.y + 25f, 12f, 12f), Icons.LifeGoal, GoalColor);
                var bar = new Rect(x + 16f, r.y + 27f, 46f, 8f);
                Widgets.FillableBar(bar, info.goalProgress, GoalBar, BaseContent.ClearTex, false);
                Widgets.DrawBox(bar);
                UiKit.LabelFit(new Rect(x + 68f, r.y + 22f, w - 68f, 18f), info.goal, UiKit.Faint, GameFont.Tiny);
            }
            else UiKit.LabelFit(new Rect(x, r.y + 22f, w, 18f), "CC_People_NoGoal".Translate(), UiKit.Dim, GameFont.Tiny);

            // What's going on in their life, as icons; their circle's name after them.
            float mx = x;
            foreach (var m in info.markers)
            {
                if (mx + 16f > r.xMax - 6f) break;
                var ir = new Rect(mx, r.y + 42f, 14f, 14f);
                UiKit.Icon(ir, m.icon, m.color);
                float mw = 14f;
                if (m.count != null)
                {
                    UiKit.LabelFit(new Rect(mx + 15f, r.y + 40f, 16f, 18f), m.count, m.color, GameFont.Tiny);
                    mw += 10f;
                }
                TooltipHandler.TipRegion(new Rect(mx - 1f, r.y + 40f, mw + 2f, 18f), m.tip);
                mx += mw + 6f;
            }
            if (info.circle != null && r.xMax - 6f - mx > 40f)
            {
                UiKit.Icon(new Rect(mx, r.y + 42f, 14f, 14f), Icons.Bonds, UiKit.Gold);
                UiKit.LabelFit(new Rect(mx + 18f, r.y + 40f, r.xMax - 6f - mx - 18f, 18f), info.circle, UiKit.Gold, GameFont.Tiny);
            }
            var pawn = p;
            TooltipHandler.TipRegion(r, () => "CC_Trust_Header".Translate() + ": " + ("CC_Trust_" + PawnStanding.LevelFor(comp.standings.TrustOf(pawn))).Translate(),
                p.thingIDNumber ^ 0x7E01);
            if (Widgets.ButtonInvisible(r)) Select(p);
        }

        /// <summary>What's going on in their life: office, life goal, circle, teaching, rivals, quirks, vendettas, secrets.</summary>
        private static RowInfo Info(Pawn p, CouncilGameComponent comp)
        {
            var s = ColonyCouncilMod.Settings;
            var info = new RowInfo();
            var office = comp.offices.OfficeOf(p);
            if (office != null) info.office = office.LabelCap;
            var goal = s.aspirations ? comp.aspirations.Active(p) : null;
            if (goal != null)
            {
                info.goal = goal.Goal;
                info.goalProgress = goal.Progress;
            }
            if (s.socialWeb)
            {
                info.circle = comp.social.CliqueOf(p)?.name;
                foreach (var t in comp.social.ties)
                {
                    if (t.kind != TieKind.Mentor || !t.Involves(p) || t.skill == null) continue;
                    info.markers.Add(new Marker
                    {
                        icon = Icons.Chronicle,
                        color = UiKit.TieColor(TieKind.Mentor),
                        tip = (t.a == p ? "CC_People_Teaching" : "CC_People_Learning").Translate(t.skill.label).Resolve()
                    });
                }
                int rivals = comp.social.ties.Count(t => t.kind == TieKind.Rivals && t.announced && t.Involves(p));
                if (rivals > 0)
                    info.markers.Add(new Marker
                    {
                        icon = Icons.Raid,
                        color = UiKit.TieColor(TieKind.Rivals),
                        tip = "CC_People_Rivals".Translate(rivals).Resolve(),
                        count = rivals > 1 ? rivals.ToString() : null
                    });
            }
            if (s.quirks)
            {
                int quirks = comp.quirks.Of(p).Count();
                if (quirks > 0)
                    info.markers.Add(new Marker
                    {
                        icon = Icons.CatMods,
                        color = new Color(0.85f, 0.85f, 0.85f),
                        tip = "CC_People_Quirks".Translate(quirks).Resolve(),
                        count = quirks > 1 ? quirks.ToString() : null
                    });
            }
            if (s.vendettas && comp.vendettas.Of(p).Any())
                info.markers.Add(new Marker { icon = Icons.Vendetta, color = UiKit.Bad, tip = "CC_People_Vendetta".Translate().Resolve() });
            if (s.secrets)
            {
                var secret = comp.secrets.Of(p);
                if (secret != null && secret.revealed)
                    info.markers.Add(new Marker { icon = Icons.Secret, color = UiKit.Bad, tip = "CC_People_SecretOut".Translate().Resolve() });
                else if (secret != null && secret.knowers.Count > 0)
                    info.markers.Add(new Marker { icon = Icons.Secret, color = UiKit.Faint, tip = "CC_People_Confided".Translate().Resolve() });
            }
            return info;
        }
    }
}
