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
    /// Region → Book of the gods: the colony's own book (Gods/GodLore.cs), as its keeper has written it. Text only, in
    /// either of Rimfolk's looks: on the left the names the colony has heard gods by (and "the whole book", and the things
    /// nobody could name), on the right a name's page: the name set large, the other names it was worked out to be in
    /// italics, when and from whom it was first heard, what the colony's people believe of it side by side, and every entry
    /// in order with its date, a change of hand marked where the keeper changed. Nothing in it is more than the colonists
    /// saw, were told and believe: no favour, no rules, no truth marked. A god the colony has never heard of isn't in it.
    /// What it shows is laid out at most once a second, or when the book changes.
    /// </summary>
    public static class GodLorePage
    {
        /// <summary>The page chosen: a name, or one of these.</summary>
        private const string Whole = "\u0001whole", Unnamed = "\u0001unnamed";

        private static string chosen = Whole;
        private static Vector2 listScroll, pageScroll;
        private static float listHeight = 300f, pageHeight = 600f;
        private static ViewHeadModel head;

        private sealed class Page
        {
            public string title;
            public List<string> also = new List<string>();
            public LoreName name;
            public List<LoreEntry> entries = new List<LoreEntry>();
        }

        private static readonly List<Page> pages = new List<Page>();
        private static readonly List<LoreEntry> unnamed = new List<LoreEntry>();
        private static float builtAt = -10f;
        private static int builtVersion = -1;

        private static void Refresh(GodLore lore)
        {
            if (Time.realtimeSinceStartup - builtAt < 1f && builtAt <= Time.realtimeSinceStartup && builtVersion == GodLore.Version) return;
            builtAt = Time.realtimeSinceStartup;
            builtVersion = GodLore.Version;
            pages.Clear();
            unnamed.Clear();
            var byTitle = new Dictionary<string, Page>();
            foreach (var n in lore.names.OrderBy(x => x.first))
            {
                var root = lore.Root(n);
                if (root == null) continue;
                if (!byTitle.TryGetValue(root.name, out var page))
                {
                    page = new Page { title = root.name, name = root };
                    byTitle[root.name] = page;
                    pages.Add(page);
                }
                if (n != root && !page.also.Contains(n.name)) page.also.Add(n.name);
            }
            foreach (var e in lore.entries)
            {
                if (e.page == null)
                {
                    unnamed.Add(e);
                    continue;
                }
                var root = lore.Root(lore.Named(e.page));
                if (root != null && byTitle.TryGetValue(root.name, out var page)) page.entries.Add(e);
            }
            if (chosen != Whole && chosen != Unnamed && !byTitle.ContainsKey(chosen)) chosen = Whole;
        }

        /// <summary>Opens the book at a name the colony knows a god by (another page, a dev action).</summary>
        public static void Show(string name)
        {
            chosen = name ?? Whole;
            pageScroll = Vector2.zero;
        }

        // ================= The view =================

        public static void Draw(Rect rect)
        {
            var lore = GodLore.Instance;
            var keeper = GodLore.Keeper(out _);
            string line = keeper != null ? "CC_GodLore_KeptBy".Translate(keeper.LabelShort.Named("KEEPER")).Resolve() : "CC_GodLore_KeptByNobody".Translate().Resolve();
            if (lore != null) Refresh(lore);
            string stat = pages.Count > 0 ? "CC_GodLore_Stat".Translate(pages.Count.Named("COUNT")).Resolve() : null;
            float top = UiKit.ViewHeader(rect, Icons.Journal ?? Icons.Chronicle, "CC_Tab_Gods".Translate(), line, stat, null, 0f, null,
                head ??= new ViewHeadModel { article = PagesMath.ArticleOf(RegionView.Gods) });
            var body = new Rect(rect.x, rect.y + top + 4f, rect.width, rect.height - top - 4f);
            if (!Gods.On || lore == null)
            {
                UiKit.EmptyState(body, Icons.Journal ?? Icons.Chronicle, "CC_GodLore_Off".Translate());
                return;
            }
            if (lore.entries.Count == 0 && pages.Count == 0)
            {
                UiKit.EmptyState(body, Icons.Journal ?? Icons.Chronicle, "CC_GodLore_Empty".Translate());
                return;
            }
            try
            {
                float lw = Mathf.Clamp(body.width * 0.24f, 170f, 250f);
                List(new Rect(body.x, body.y, lw, body.height));
                var page = new Rect(body.x + lw + 14f, body.y, body.width - lw - 14f, body.height);
                Widgets.DrawBoxSolid(new Rect(page.x - 7.5f, page.y, 1f, page.height), UiKit.DividerColor);
                PageView(page);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ================= The names =================

        private static void List(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, listHeight);
            Widgets.BeginScrollView(rect, ref listScroll, view);
            try
            {
                float y = 0f, w = view.width;
                y = Row(y, w, Whole, "CC_GodLore_Whole".Translate(), null, 0);
                y += 6f;
                for (int i = 0; i < pages.Count; i++)
                {
                    var p = pages[i];
                    string sub = p.also.Count > 0 ? "CC_GodLore_AlsoShort".Translate(p.also.ToCommaList(true).Named("NAMES")).Resolve() : null;
                    y = Row(y, w, p.title, p.title.CapitalizeFirst(), sub, i);
                }
                if (unnamed.Count > 0)
                {
                    y += 6f;
                    y = Row(y, w, Unnamed, "CC_GodLore_Unnamed".Translate(), null, 0);
                }
                listHeight = Mathf.Max(y + 6f, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        private static float Row(float y, float w, string key, string label, string sub, int index)
        {
            float h = sub.NullOrEmpty() ? 28f : 40f;
            var r = new Rect(0f, y, w, h);
            bool on = chosen == key;
            if (on) UiKit.RowLit(r, true);
            else
            {
                UiKit.Zebra(r, index);
                UiKit.RowHover(r);
            }
            UiKit.LabelFit(new Rect(r.x + 8f, r.y + 4f, r.width - 12f, 20f), label, on ? UiKit.Gold : UiKit.Ink);
            if (!sub.NullOrEmpty()) UiKit.LabelFit(new Rect(r.x + 8f, r.y + 22f, r.width - 12f, 16f), "<i>" + sub + "</i>", UiKit.Faint, GameFont.Tiny);
            if (Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                chosen = key;
                pageScroll = Vector2.zero;
            }
            return y + h + 2f;
        }

        // ================= A page =================

        /// <summary>The widest a column of the book's text runs, for reading.</summary>
        public const float Column = 600f;

        private static void PageView(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, pageHeight);
            Widgets.BeginScrollView(rect, ref pageScroll, view);
            try
            {
                float cw = Mathf.Min(Column, view.width - 32f);
                float x = (view.width - cw) / 2f;
                float y = 14f;
                if (chosen == Whole) y = WholeBook(x, y, cw);
                else if (chosen == Unnamed) y = Entries(x, Title(x, y, cw, "CC_GodLore_Unnamed".Translate(), null, "CC_GodLore_UnnamedLine".Translate()), cw, unnamed, false);
                else
                {
                    var p = pages.FirstOrDefault(q => q.title == chosen);
                    if (p != null) y = NamePage(x, y, cw, p);
                }
                pageHeight = Mathf.Max(y + 30f, rect.height);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The page's head: a title set large, an italic line under it, and a small italic note; then a rule.</summary>
        private static float Title(float x, float y, float w, string title, string italic, string note)
        {
            y += Para(x, y, w, "<size=26>" + title.CapitalizeFirst() + "</size>", UiKit.Ink, GameFont.Medium, TextAnchor.UpperCenter) + 2f;
            if (!italic.NullOrEmpty()) y += Para(x, y, w, "<i>" + italic + "</i>", UiKit.Faint, GameFont.Small, TextAnchor.UpperCenter) + 2f;
            if (!note.NullOrEmpty()) y += Para(x, y, w, "<i>" + note + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter);
            return Rule(x, y + 10f, w) + 10f;
        }

        private static float NamePage(float x, float y, float w, Page p)
        {
            string also = p.also.Count > 0 ? "CC_GodLore_Also".Translate(p.also.ToCommaList(true).Named("NAMES")).Resolve() : null;
            string first = p.name != null
                ? "CC_GodLore_FirstHeard".Translate(Date(p.name.first).Named("DATE"), (p.name.from ?? "CC_GodLore_FromNowhere".Translate().ToString()).Named("FROM")).Resolve()
                : null;
            y = Title(x, y, w, p.title, also, first);
            // What the colony's people believe of it, side by side: each one's latest word.
            var beliefs = p.entries.Where(e => e.kind == LoreKind.Believes || e.kind == LoreKind.Doubts).GroupBy(e => e.who.FirstOrDefault() ?? "")
                .Select(gr => gr.Last()).OrderBy(e => e.kind).ThenByDescending(e => e.tick).Take(6).ToList();
            if (beliefs.Count >= 1)
            {
                y += Para(x, y, w, "<i>" + "CC_GodLore_Believe".Translate() + "</i>", UiKit.Faint, GameFont.Small, TextAnchor.UpperCenter) + 6f;
                foreach (var e in beliefs) y += Para(x + 16f, y, w - 32f, e.text, e.kind == LoreKind.Doubts ? UiKit.Faint : UiKit.Ink, GameFont.Small) + 6f;
                y = Rule(x, y + 6f, w) + 10f;
            }
            if (p.entries.Count == 0) return y + Para(x, y, w, "<i>" + "CC_GodLore_OnlyTheName".Translate() + "</i>", UiKit.Dim, GameFont.Small, TextAnchor.UpperCenter);
            return Entries(x, y, w, p.entries, false);
        }

        private static float WholeBook(float x, float y, float w)
        {
            y = Title(x, y, w, "CC_GodLore_BookTitle".Translate(), "CC_GodLore_BookLine".Translate(Faction.OfPlayerSilentFail?.Name ?? "").Resolve(), null);
            var lore = GodLore.Instance;
            return Entries(x, y, w, lore?.entries ?? new List<LoreEntry>(), true);
        }

        /// <summary>The entries in order, each dated (and, in the whole book, under whose name it is), the keeper's change of hand marked.</summary>
        private static float Entries(float x, float y, float w, List<LoreEntry> list, bool whole)
        {
            string hand = "\u0002";
            var lore = GodLore.Instance;
            foreach (var e in list)
            {
                if (e == null) continue;
                string k = e.keeper ?? "";
                if (k != hand)
                {
                    if (hand != "\u0002") y = Rule(x + w * 0.3f, y + 4f, w * 0.4f) + 6f;
                    string change = e.keeper != null ? "CC_GodLore_Hand".Translate(e.keeper.Named("KEEPER")).Resolve() : "CC_GodLore_NoHand".Translate().Resolve();
                    y += Para(x, y, w, "<i>" + change + "</i>", UiKit.Dim, GameFont.Tiny, TextAnchor.UpperCenter) + 8f;
                    hand = k;
                }
                string when = Date(e.tick);
                if (whole)
                {
                    var root = e.page != null && lore != null ? lore.Root(lore.Named(e.page)) : null;
                    string of = root != null ? "CC_GodLore_Of".Translate(root.name.Named("NAME")).Resolve() : "CC_GodLore_OfUnnamed".Translate().Resolve();
                    when = when + "  ·  " + of;
                }
                y += Para(x, y, w, "<i>" + when + "</i>", UiKit.Faint, GameFont.Tiny);
                y += Para(x, y, w, e.text ?? "", UiKit.Ink, GameFont.Small) + 12f;
            }
            return y;
        }

        // ================= Type =================

        private static float Para(float x, float y, float w, string text, Color c, GameFont font, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            if (text.NullOrEmpty()) return 0f;
            Text.Font = font;
            Text.Anchor = anchor;
            float h = Text.CalcHeight(text, w);
            GUI.color = c;
            Widgets.Label(new Rect(x, y, w, h), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            return h;
        }

        /// <summary>A thin rule across the column, a little shorter than it.</summary>
        private static float Rule(float x, float y, float w)
        {
            GUI.color = UiKit.DividerColor;
            Widgets.DrawLineHorizontal(x + w * 0.1f, y, w * 0.8f);
            GUI.color = Color.white;
            return y + 1f;
        }

        /// <summary>A date as the colony writes it: "3rd of Aprimay, 5502".</summary>
        public static string Date(int tick)
        {
            try
            {
                var seat = Homes.Seat;
                var at = seat != null ? Find.WorldGrid.LongLatOf(seat.Tile) : Vector2.zero;
                return GenDate.DateFullStringAt(GenDate.TickGameToAbs(tick), at);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
