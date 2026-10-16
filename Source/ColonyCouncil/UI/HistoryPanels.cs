using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>History → Dreams (cards with the dreamer, the kind of dream and the dream itself) and History → Journals.</summary>
    public static class HistoryPanels
    {
        private static readonly Color Gold = new Color(1f, 0.85f, 0.5f);

        // ---------------- Dreams ----------------

        private static Vector2 dreamScroll;
        private static float dreamHeight = 400f;
        private static string dreamFilter;
        private static bool kindLoaded;
        private static DreamKind? kindMemo;

        /// <summary>The kind of dream shown (null: every kind), remembered between sessions (UI/PageMemory.cs).</summary>
        private static DreamKind? kindFilter
        {
            get
            {
                if (!kindLoaded)
                {
                    kindLoaded = true;
                    string saved = PageMemory.Get("dreams.kind");
                    kindMemo = saved != null && System.Enum.TryParse(saved, out DreamKind k) ? k : (DreamKind?)null;
                }
                return kindMemo;
            }
            set
            {
                kindLoaded = true;
                kindMemo = value;
                PageMemory.Set("dreams.kind", value?.ToString());
            }
        }

        // The filtered dreams, rebuilt when a filter or the list of dreams changes.
        private static readonly List<DreamRecord> dreams = new List<DreamRecord>();
        private static string dreamsWho;
        private static DreamKind? dreamsKind;
        private static int dreamsCount = -1;
        private static DreamRecord dreamsLast;
        private static readonly Dictionary<DreamRecord, float> dreamHeights = new Dictionary<DreamRecord, float>();
        private static float dreamWidth;

        public static Color DreamKindColor(DreamKind k)
        {
            switch (k)
            {
                case DreamKind.Sweet: return new Color(0.95f, 0.72f, 0.82f);
                case DreamKind.Strange: return UiKit.DreamColor;
                case DreamKind.Nightmare: return new Color(0.88f, 0.48f, 0.42f);
                case DreamKind.Omen: return Gold;
                default: return new Color(0.62f, 0.74f, 0.9f);
            }
        }

        public static void DrawDreams(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.dreams)
            {
                UiKit.EmptyState(rect, Icons.Dreams, "CC_History_DreamsOff".Translate());
                return;
            }
            var all = comp.dreams.recent;
            var list = Dreams(all);
            float top = UiKit.PageHeader(rect, Icons.Dreams, "CC_Tab_Dreams".Translate(), "CC_UI_Sub_Dreams".Translate(), "CC_UI_Stat_Dreams".Translate(list.Count).Resolve(),
                UiKit.DreamColor, 436f);
            string who = dreamFilter ?? "CC_Social_Everyone".Translate().ToString();
            if (Widgets.ButtonText(new Rect(rect.xMax - 428f, rect.y + 6f, 220f, 30f), UiKit.Fit("CC_Social_Filter".Translate() + ": " + who, 200f)))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_Social_Everyone".Translate(), () => dreamFilter = null) };
                options.AddRange(all.Select(d => d.pawn?.LabelShort ?? d.name).Where(n => n != null).Distinct().OrderBy(n => n).Select(n => new FloatMenuOption(n, () => dreamFilter = n)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            string kindLabel = kindFilter.HasValue ? ("CC_DreamKind_" + kindFilter.Value).Translate().ToString() : "CC_History_AllKinds".Translate().ToString();
            if (Widgets.ButtonText(new Rect(rect.xMax - 200f, rect.y + 6f, 200f, 30f), kindLabel))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_History_AllKinds".Translate(), () => kindFilter = null) };
                foreach (DreamKind k in System.Enum.GetValues(typeof(DreamKind)))
                {
                    var local = k;
                    options.Add(new FloatMenuOption(("CC_DreamKind_" + k).Translate(), () => kindFilter = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, dreamHeight);
            Widgets.BeginScrollView(outRect, ref dreamScroll, view);
            float w = view.width;
            float y = 0f;
            if (list.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.Dream, "CC_Social_DreamsNone".Translate(), 140f);
            if (dreamWidth != w || dreamHeights.Count > 1000)
            {
                dreamHeights.Clear();
                dreamWidth = w;
            }
            const float textX = 60f;
            foreach (var d in list)
            {
                string text = "\"" + d.text + "\"";
                if (!dreamHeights.TryGetValue(d, out float h))
                {
                    Text.Font = GameFont.Small;
                    h = Mathf.Max(56f, 34f + Text.CalcHeight(text, w - textX - 12f) + 10f);
                    dreamHeights[d] = h;
                }
                if (y + h < dreamScroll.y || y > dreamScroll.y + outRect.height)
                {
                    y += h + 6f;
                    continue;
                }
                var color = DreamKindColor(d.kind);
                var card = new Rect(0f, y, w, h);
                UiKit.Card(card, d.cameTrue ? Gold : UiKit.WithAlpha(color, 0.6f), false, false);
                var face = new Rect(12f, y + 10f, 36f, 36f);
                if (d.pawn != null && !d.pawn.Dead) UiKit.Portrait(face, d.pawn, color);
                else UiKit.Badge(face, Icons.Dream, color);
                if (d.pawn != null && d.pawn.IsColonist && Widgets.ButtonInvisible(face)) MainTabWindow_Council.OpenPerson(d.pawn);
                // Who, what kind, and when; the tags give way before they reach the date.
                string name = d.pawn?.LabelShort ?? d.name ?? "?";
                Text.Font = GameFont.Small;
                float nameW = Mathf.Min(Text.CalcSize(name).x + 4f, w * 0.4f);
                UiKit.LabelFit(new Rect(textX, y + 6f, nameW, 24f), name, Color.white);
                float px = textX + nameW + 8f;
                float pillsEnd = w - 116f;
                if (pillsEnd - px > 40f) px += UiKit.Pill(px, y + 9f, ("CC_DreamKind_" + d.kind).Translate(), color, Icons.Dream, null, pillsEnd - px) + 4f;
                if (d.cameTrue && pillsEnd - px > 40f) UiKit.Pill(px, y + 9f, "CC_Social_DreamCameTrue".Translate(), Gold, Icons.Promise, null, pillsEnd - px);
                UiKit.LabelFit(new Rect(w - 110f, y + 6f, 100f, 22f), MainTabWindow_Council.DayString(d.tick), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                GUI.color = UiKit.Quote;
                Widgets.Label(new Rect(textX, y + 34f, w - textX - 12f, h - 40f), text);
                GUI.color = Color.white;
                y += h + 6f;
            }
            dreamHeight = Mathf.Max(y, 100f);
            Widgets.EndScrollView();
        }

        private static List<DreamRecord> Dreams(List<DreamRecord> all)
        {
            var last = all.Count > 0 ? all[all.Count - 1] : null;
            if (dreamsCount == all.Count && dreamsLast == last && dreamsWho == dreamFilter && dreamsKind == kindFilter) return dreams;
            dreams.Clear();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var d = all[i];
                if ((dreamFilter == null || (d.pawn?.LabelShort ?? d.name) == dreamFilter) && (!kindFilter.HasValue || d.kind == kindFilter.Value)) dreams.Add(d);
            }
            dreamsCount = all.Count;
            dreamsLast = last;
            dreamsWho = dreamFilter;
            dreamsKind = kindFilter;
            return dreams;
        }

        // ---------------- Journals ----------------

        private static PawnJournal selectedJournal;
        private static Vector2 listScroll;
        private static Vector2 entryScroll;
        private static float entryHeight = 400f;
        private static readonly Dictionary<JournalEntry, float> entryHeights = new Dictionary<JournalEntry, float>();
        private static float entryWidth;

        public static void DrawJournals(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.journals)
            {
                UiKit.EmptyState(rect, Icons.Journals, "CC_History_JournalsOff".Translate());
                return;
            }
            var here = People.OursIds();
            var journals = UiCache.Get(comp.journals, 90, () => comp.journals.All.OrderBy(j => here.Contains(j.pawnId) ? 0 : 1).ThenBy(j => j.name).ToList(), 0.5f);
            if (selectedJournal == null || !journals.Contains(selectedJournal)) selectedJournal = journals.FirstOrDefault();
            float top = UiKit.PageHeader(rect, Icons.Journals, "CC_Tab_Journals".Translate(), "CC_UI_Sub_Journals".Translate(),
                "CC_UI_Stat_Journals".Translate(journals.Count).Resolve());

            const float rowH = 40f;
            var left = new Rect(rect.x, rect.y + top, Mathf.Min(250f, rect.width * 0.35f), rect.height - top);
            var right = new Rect(left.xMax + 12f, rect.y + top, rect.xMax - left.xMax - 12f, rect.height - top);
            var view = new Rect(0f, 0f, left.width - 16f, journals.Count * (rowH + 4f));
            Widgets.BeginScrollView(left, ref listScroll, view);
            float y = 0f;
            foreach (var j in journals)
            {
                var r = new Rect(0f, y, view.width, rowH);
                bool alive = here.Contains(j.pawnId);
                if (y + rowH >= listScroll.y && y <= listScroll.y + left.height)
                {
                    UiKit.Card(r, null, j == selectedJournal);
                    if (j == selectedJournal) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), Gold);
                    var face = new Rect(r.x + 6f, r.y + 5f, 30f, 30f);
                    var pawn = alive ? PawnOf(j) : null;
                    if (pawn != null) UiKit.Portrait(face, pawn, UiKit.TrustColor(comp.standings.TrustOf(pawn)));
                    else UiKit.Badge(face, Icons.Journal, UiKit.Dim);
                    UiKit.LabelFit(new Rect(r.x + 42f, r.y + 2f, r.width - 46f, 20f), j.name, alive ? Color.white : UiKit.Faint);
                    string sub = (alive ? "" : "CC_History_Departed".Translate().ToString() + "  ") + "CC_UI_Entries".Translate(j.entries.Count).ToString();
                    UiKit.LabelFit(new Rect(r.x + 42f, r.y + 20f, r.width - 46f, 18f), sub, UiKit.Dim, GameFont.Tiny);
                    if (Widgets.ButtonInvisible(r))
                    {
                        selectedJournal = j;
                        entryScroll = Vector2.zero;
                    }
                }
                y += rowH + 4f;
            }
            Widgets.EndScrollView();

            Widgets.DrawMenuSection(right);
            var inner = right.ContractedBy(10f);
            if (selectedJournal == null)
            {
                UiKit.EmptyState(inner, Icons.Journals, "CC_History_NoJournals".Translate());
                return;
            }
            var owner = here.Contains(selectedJournal.pawnId) ? PawnOf(selectedJournal) : null;
            float tx = inner.x;
            if (owner != null)
            {
                UiKit.Portrait(new Rect(inner.x, inner.y, 36f, 36f), owner, UiKit.TrustColor(comp.standings.TrustOf(owner)));
                tx += 44f;
            }
            else
            {
                UiKit.Icon(new Rect(inner.x + 4f, inner.y + 6f, 26f, 26f), Icons.Journal, UiKit.Faint);
                tx += 40f;
            }
            UiKit.LabelFit(new Rect(tx, inner.y + 2f, inner.xMax - tx, 32f), "CC_Journal_Title".Translate(selectedJournal.name), Color.white, GameFont.Medium);
            UiKit.Divider(inner.x, inner.y + 42f, inner.width);
            var outRect = new Rect(inner.x, inner.y + 48f, inner.width, inner.height - 48f);
            var eview = new Rect(0f, 0f, outRect.width - 16f, entryHeight);
            Widgets.BeginScrollView(outRect, ref entryScroll, eview);
            float ew = eview.width;
            if (entryWidth != ew || entryHeights.Count > 2000)
            {
                entryHeights.Clear();
                entryWidth = ew;
            }
            float ey = 0f;
            int n = 0;
            for (int i = selectedJournal.entries.Count - 1; i >= 0; i--)
            {
                var e = selectedJournal.entries[i];
                if (!entryHeights.TryGetValue(e, out float h))
                {
                    Text.Font = GameFont.Small;
                    h = 22f + Text.CalcHeight(e.text, ew - 24f) + 10f;
                    entryHeights[e] = h;
                }
                if (ey + h >= entryScroll.y && ey <= entryScroll.y + outRect.height)
                {
                    UiKit.Zebra(new Rect(0f, ey, ew, h), n);
                    UiKit.Icon(new Rect(4f, ey + 4f, 14f, 14f), Icons.Journal, UiKit.Faint);
                    UiKit.LabelFit(new Rect(22f, ey + 1f, ew - 26f, 20f), e.date, UiKit.Faint, GameFont.Tiny);
                    GUI.color = UiKit.Quote;
                    Widgets.Label(new Rect(22f, ey + 22f, ew - 24f, h - 30f), e.text);
                    GUI.color = Color.white;
                }
                ey += h;
                n++;
            }
            entryHeight = Mathf.Max(ey, 100f);
            Widgets.EndScrollView();
        }

        /// <summary>The colonist a journal belongs to, when they're still with the colony (for the portrait).</summary>
        private static Pawn PawnOf(PawnJournal j) =>
            UiCache.Get(j, 91, () => PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault(p => p.ThingID == j.pawnId), 2f);
    }
}
