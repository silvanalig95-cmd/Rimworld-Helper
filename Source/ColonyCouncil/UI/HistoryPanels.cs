using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>History → Dreams and History → Journals.</summary>
    public static class HistoryPanels
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Quote = new Color(1f, 0.93f, 0.75f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.5f);

        // ---------------- Dreams ----------------

        private static Vector2 dreamScroll;
        private static float dreamHeight = 400f;
        private static string dreamFilter;
        private static DreamKind? kindFilter;

        public static void DrawDreams(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.dreams)
            {
                Widgets.Label(rect, "CC_History_DreamsOff".Translate());
                return;
            }
            var all = comp.dreams.recent;
            string who = dreamFilter ?? "CC_Social_Everyone".Translate().ToString();
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 220f, 28f), "CC_Social_Filter".Translate() + ": " + who))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_Social_Everyone".Translate(), () => dreamFilter = null) };
                options.AddRange(all.Select(d => d.pawn?.LabelShort ?? d.name).Where(n => n != null).Distinct().OrderBy(n => n).Select(n => new FloatMenuOption(n, () => dreamFilter = n)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            string kindLabel = kindFilter.HasValue ? ("CC_DreamKind_" + kindFilter.Value).Translate().ToString() : "CC_History_AllKinds".Translate().ToString();
            if (Widgets.ButtonText(new Rect(rect.x + 228f, rect.y, 200f, 28f), kindLabel))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_History_AllKinds".Translate(), () => kindFilter = null) };
                foreach (DreamKind k in System.Enum.GetValues(typeof(DreamKind)))
                {
                    var local = k;
                    options.Add(new FloatMenuOption(("CC_DreamKind_" + k).Translate(), () => kindFilter = local));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            var list = all.AsEnumerable().Reverse()
                .Where(d => (dreamFilter == null || (d.pawn?.LabelShort ?? d.name) == dreamFilter) && (!kindFilter.HasValue || d.kind == kindFilter.Value)).ToList();

            var outRect = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 36f);
            var view = new Rect(0f, 0f, outRect.width - 16f, dreamHeight);
            Widgets.BeginScrollView(outRect, ref dreamScroll, view);
            float y = 0f;
            if (list.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, view.width, 24f), "CC_Social_DreamsNone".Translate());
                GUI.color = Color.white;
                y += 26f;
            }
            foreach (var d in list)
            {
                string head = (d.pawn?.LabelShort ?? d.name) + "  ·  " + ("CC_DreamKind_" + d.kind).Translate().ToString() + "  ·  " + "CC_DayN".Translate(d.tick / GenDate.TicksPerDay + 1).ToString()
                              + (d.cameTrue ? "  ·  " + "CC_Social_DreamCameTrue".Translate().ToString() : "");
                GUI.color = d.cameTrue ? Gold : d.kind == DreamKind.Nightmare ? new Color(0.85f, 0.6f, 0.55f) : Faint;
                Widgets.Label(new Rect(0f, y, view.width, 22f), head);
                y += 22f;
                string text = "\"" + d.text + "\"";
                float h = Text.CalcHeight(text, view.width - 12f);
                GUI.color = Quote;
                Widgets.Label(new Rect(12f, y, view.width - 12f, h), text);
                GUI.color = Color.white;
                y += h + 10f;
            }
            dreamHeight = Mathf.Max(y, 100f);
            Widgets.EndScrollView();
        }

        // ---------------- Journals ----------------

        private static PawnJournal selectedJournal;
        private static Vector2 listScroll;
        private static Vector2 entryScroll;
        private static float entryHeight = 400f;

        public static void DrawJournals(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.journals)
            {
                Widgets.Label(rect, "CC_History_JournalsOff".Translate());
                return;
            }
            var here = People.OursIds();
            var journals = comp.journals.All.OrderBy(j => here.Contains(j.pawnId) ? 0 : 1).ThenBy(j => j.name).ToList();
            if (selectedJournal == null || !journals.Contains(selectedJournal)) selectedJournal = journals.FirstOrDefault();

            var left = new Rect(rect.x, rect.y, 240f, rect.height);
            var right = new Rect(rect.x + 252f, rect.y, rect.width - 252f, rect.height);
            var view = new Rect(0f, 0f, left.width - 16f, journals.Count * 30f);
            Widgets.BeginScrollView(left, ref listScroll, view);
            float y = 0f;
            foreach (var j in journals)
            {
                var r = new Rect(0f, y, view.width, 28f);
                if (j == selectedJournal) Widgets.DrawBoxSolid(r, new Color(1f, 1f, 1f, 0.10f));
                Widgets.DrawHighlightIfMouseover(r);
                bool alive = here.Contains(j.pawnId);
                GUI.color = alive ? Color.white : Faint;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, view.width - 6f, 28f), (j.name + (alive ? "" : "  " + "CC_History_Departed".Translate()) + "  (" + j.entries.Count + ")").Truncate(view.width - 10f));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(r))
                {
                    selectedJournal = j;
                    entryScroll = Vector2.zero;
                }
                y += 30f;
            }
            Widgets.EndScrollView();

            Widgets.DrawMenuSection(right);
            var inner = right.ContractedBy(10f);
            if (selectedJournal == null)
            {
                GUI.color = Faint;
                Widgets.Label(inner, "CC_History_NoJournals".Translate());
                GUI.color = Color.white;
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 32f), "CC_Journal_Title".Translate(selectedJournal.name));
            Text.Font = GameFont.Small;
            var outRect = new Rect(inner.x, inner.y + 36f, inner.width, inner.height - 36f);
            var eview = new Rect(0f, 0f, outRect.width - 16f, entryHeight);
            Widgets.BeginScrollView(outRect, ref entryScroll, eview);
            float ey = 0f;
            foreach (var e in selectedJournal.entries.AsEnumerable().Reverse())
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, ey, eview.width, 22f), e.date);
                ey += 22f;
                float h = Text.CalcHeight(e.text, eview.width - 12f);
                GUI.color = Quote;
                Widgets.Label(new Rect(12f, ey, eview.width - 12f, h), e.text);
                GUI.color = Color.white;
                ey += h + 10f;
            }
            entryHeight = Mathf.Max(ey, 100f);
            Widgets.EndScrollView();
        }
    }
}
