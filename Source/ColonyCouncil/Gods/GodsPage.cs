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
    /// Region → Peoples → Gods (docs/design/gods.md, "A Gods page"): the pantheon, as the Peoples view's third mode. On the
    /// left the gods in three groups (the colony's own, the others it knows, and the rumours: a god of another people the
    /// colony hasn't learned yet shows only as "a god of war, of the Ashen Banner"), with "the whole pantheon" first: the web
    /// of every known god and its ties (rivals in red, lovers in rose, the estranged dashed, kin in gold, allies in green, the
    /// old wrongs dark). On the right the chosen god's page: its symbol in its colours, its name and epithet, what it is,
    /// its kind where it isn't ordinary, the colony's favour with it at each home in words (and whether it is the colony's
    /// patron), who holds it and how the colony knows it, its domain and sacred things, its nature, its ties as a small web
    /// and in words, how its people tell of it, and the acts the colony remembers. Drawn in either of Rimfolk's looks by
    /// UiKit's own parts. What it shows is worked out at most once a second.
    /// </summary>
    public static class GodsPage
    {
        /// <summary>The god chosen (its id), <see cref="Whole"/> for the whole pantheon, or <see cref="NoneChosen"/> to choose the colony's patron or first god.</summary>
        public static int chosen = NoneChosen;

        public const int NoneChosen = -1, Whole = -2;

        private static Vector2 listScroll, pageScroll;
        private static float listHeight = 400f, pageHeight = 600f;
        private static readonly Folds folds = new Folds("rumours");
        private const float RowH = 40f;

        // ================= The model, once a second =================

        private static float builtAt = -10f;
        private static int builtVersion = -1;
        private static readonly List<God> ours = new List<God>(), known = new List<God>(), rumours = new List<God>();

        private static void Refresh()
        {
            if (Time.realtimeSinceStartup - builtAt < 1f && builtVersion == Gods.Version) return;
            builtAt = Time.realtimeSinceStartup;
            builtVersion = Gods.Version;
            ours.Clear();
            known.Clear();
            rumours.Clear();
            var map = Home;
            var mine = Gods.ColonyGods(map);
            ours.AddRange(mine.Where(g => g.known));
            foreach (var g in Gods.All)
            {
                if (ours.Contains(g)) continue;
                if (g.known) known.Add(g);
                else rumours.Add(g);
            }
            known.Sort((a, b) => Gods.FavourValue(b, map).CompareTo(Gods.FavourValue(a, map)));
            rumours.Sort((a, b) => a.domain.CompareTo(b.domain));
            if (chosen == NoneChosen) chosen = Gods.Patron(map)?.id ?? ours.FirstOrDefault()?.id ?? Whole;
            else if (chosen != Whole && Gods.ById(chosen) == null) chosen = Whole;
        }

        /// <summary>The home the page speaks of: the colony in view's, else the seat.</summary>
        private static Map Home => Find.CurrentMap != null && Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        /// <summary>Opens the page on a god (from another page, a card).</summary>
        public static void Show(God g)
        {
            if (g == null) return;
            chosen = g.id;
            pageScroll = Vector2.zero;
            RegionView.gods = true;
            RegionView.faces = false;
        }

        // ================= The view =================

        /// <summary>The stat on the view's header: how many gods the colony knows of the world's.</summary>
        public static string Stat()
        {
            int all = Gods.All.Count;
            int k = Gods.All.Count(g => g.known);
            return all == 0 ? null : "CC_Gods_Stat".Translate(k.Named("KNOWN"), all.Named("ALL")).Resolve();
        }

        public static void Draw(Rect body)
        {
            if (!Gods.On)
            {
                UiKit.EmptyState(body, GodSymbols.Pantheon ?? Icons.Ways, "CC_Gods_Off".Translate());
                return;
            }
            Refresh();
            if (Gods.All.Count == 0)
            {
                UiKit.EmptyState(body, GodSymbols.Pantheon ?? Icons.Ways, "CC_Gods_None".Translate());
                return;
            }
            float lw = PeoplesViewMath.ListWidth(body.width);
            List(new Rect(body.x, body.y, lw, body.height));
            var page = new Rect(body.x + lw + PeoplesViewMath.Gap, body.y, PeoplesViewMath.PageWidth(body.width), body.height);
            Widgets.DrawBoxSolid(new Rect(page.x - PeoplesViewMath.Gap / 2f - 0.5f, page.y, 1f, page.height), UiKit.DividerColor);
            try
            {
                if (chosen == Whole) WholePage(page);
                else GodPage(page, Gods.ById(chosen));
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ================= The list =================

        private static void List(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, listHeight);
            Widgets.BeginScrollView(rect, ref listScroll, view);
            try
            {
                float y = 0f, w = view.width;
                WholeRow(new Rect(0f, y, w, 30f));
                y += 34f;
                y = Group(y, w, rect.height, "CC_Gods_Group_Ours".Translate(), "ours", ours, false);
                y = Group(y, w, rect.height, "CC_Gods_Group_Known".Translate(), "known", known, false);
                y = Group(y, w, rect.height, "CC_Gods_Group_Rumours".Translate(), "rumours", rumours, true);
                listHeight = Mathf.Max(y + 6f, 60f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static float Group(float y, float w, float viewH, string label, string key, List<God> list, bool rumour)
        {
            if (list.Count == 0) return y;
            y += UiKit.SectionHeading(0f, y, w, label, list.Count, folds, key, out bool open);
            if (!open) return y + 4f;
            for (int i = 0; i < list.Count; i++)
            {
                if (y + RowH >= listScroll.y && y <= listScroll.y + viewH) Row(new Rect(0f, y, w, RowH), list[i], i, rumour);
                y += RowH;
            }
            return y + 6f;
        }

        private static void WholeRow(Rect r)
        {
            bool on = chosen == Whole;
            if (UiKit.OwnLook)
            {
                if (on) UiKit.RowLit(r, true);
                else UiKit.RoundBox(r, Mouse.IsOver(r) ? UiKit.RaisedHover : UiKit.Raised, UiKit.Edge, SkinMath.RadiusMedium);
            }
            else
            {
                Widgets.DrawBoxSolid(r, on ? UiKit.CardSelected : Mouse.IsOver(r) ? UiKit.CardHover : UiKit.CardFill);
                if (on) Widgets.DrawBoxSolid(new Rect(r.x, r.y, 3f, r.height), UiKit.Gold);
            }
            UiKit.Icon(new Rect(r.x + 8f, r.y + 7f, 16f, 16f), GodSymbols.Pantheon, on ? UiKit.Gold : UiKit.Faint);
            UiKit.LabelFit(new Rect(r.x + 32f, r.y, r.width - 40f, r.height), "CC_Gods_Whole".Translate(), on ? UiKit.Gold : UiKit.Ink);
            TooltipHandler.TipRegion(r, "CC_Gods_WholeTip".Translate());
            if (!Widgets.ButtonInvisible(r, false)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            chosen = Whole;
            pageScroll = Vector2.zero;
        }

        /// <summary>A god's row: its symbol, its name (or what the rumour says), and what it is with the colony's favour in a word.</summary>
        private static void Row(Rect r, God g, int index, bool rumour)
        {
            bool on = chosen == g.id;
            if (on) UiKit.RowLit(r, true);
            else
            {
                UiKit.Zebra(r, index);
                UiKit.RowHover(r);
            }
            GodSymbols.Draw(new Rect(r.x + 5f, r.y + 6f, 28f, 28f), g, rumour);
            float x = r.x + 40f, right = r.xMax - 4f;
            var map = Home;
            if (rumour)
            {
                UiKit.LabelFit(new Rect(x, r.y + 2f, right - x, 20f), RumourName(g), UiKit.Faint);
                UiKit.LabelFit(new Rect(x, r.y + 21f, right - x, 16f), Gods.PeopleWords(g), UiKit.Dim, GameFont.Tiny);
            }
            else
            {
                bool patron = Gods.Patron(map) == g;
                if (patron)
                {
                    UiKit.Icon(new Rect(right - 14f, r.y + 5f, 14f, 14f), GodSymbols.Pantheon, UiKit.Gold);
                    right -= 18f;
                }
                UiKit.LabelFit(new Rect(x, r.y + 2f, right - x, 20f), g.name, on ? UiKit.Gold : g.colour);
                var word = Gods.Favour(g, map);
                string ww = GodWords.Word(word);
                float wordW = Mathf.Min(UiKit.KeptWidth(ww, GameFont.Tiny) + 4f, 80f);
                UiKit.LabelFit(new Rect(r.xMax - 4f - wordW, r.y + 21f, wordW, 16f), ww, GodWords.WordColor(word), GameFont.Tiny, TextAnchor.MiddleRight);
                UiKit.LabelFit(new Rect(x, r.y + 21f, Mathf.Max(0f, r.xMax - 8f - wordW - x), 16f), Gods.GodOf(g), UiKit.Faint, GameFont.Tiny);
            }
            TooltipHandler.TipRegion(r, new TipSignal(() => rumour ? RumourLine(g) : Gods.FullName(g) + "\n" + Gods.GodOf(g).CapitalizeFirst() + "\n" + Gods.PeopleWords(g), g.id ^ 0x60D5));
            if (!Widgets.ButtonInvisible(r, false)) return;
            SoundDefOf.Click.PlayOneShotOnCamera();
            chosen = g.id;
            pageScroll = Vector2.zero;
        }

        /// <summary>What a rumour says of a god: "a god of war".</summary>
        private static string RumourName(God g) => "CC_Gods_RumourName".Translate(Gods.AGodOf(g).Named("GODOF")).Resolve();

        private static string RumourLine(God g) =>
            "CC_Gods_RumourLine".Translate(Gods.PeopleWords(g).Named("PEOPLE"), Gods.AGodOf(g).Named("GODOF")).Resolve();

        // ================= The whole pantheon: the web =================

        private sealed class Node
        {
            public God g;
            public Vector2 at;
        }

        private static readonly List<Node> nodes = new List<Node>();
        private static Rect webRect;
        private static int webVersion = -1;
        private static float webAt = -10f;

        /// <summary>The most gods the web draws (the known ones, the colony's first).</summary>
        public const int WebMost = 40;

        private static void WholePage(Rect page)
        {
            float y = page.y;
            y += UiKit.SectionTitle(page.x, y, page.width, "CC_Gods_Whole".Translate(), GodSymbols.Pantheon);
            y += UiKit.Paragraph(page.x, y, page.width, "CC_Gods_WholeLine".Translate(), UiKit.Faint, GameFont.Tiny) + 4f;
            float legendH = 40f;
            float size = Mathf.Min(page.width, page.yMax - y - legendH);
            if (size < 120f)
            {
                UiKit.Paragraph(page.x, y, page.width, "CC_Gods_WebTooSmall".Translate(), UiKit.Dim);
                return;
            }
            var area = new Rect(page.x + (page.width - size) / 2f, y, size, size);
            Web(area);
            Legend(new Rect(page.x, area.yMax + 4f, page.width, legendH - 4f));
        }

        private static void LayOut(Rect area)
        {
            nodes.Clear();
            webRect = area;
            webVersion = Gods.Version;
            webAt = Time.realtimeSinceStartup;
            // Faith by faith round the ring, so a family sits together; the colony's own first.
            var order = ours.Concat(known).Take(WebMost).OrderBy(g => ours.Contains(g) ? 0 : 1)
                .ThenBy(g => g.ideos.FirstOrDefault()?.id ?? 1000000 + (g.factions.FirstOrDefault()?.loadID ?? 0)).ThenBy(g => g.id).ToList();
            var c = area.center;
            float rx = area.width / 2f - 18f, ry = area.height / 2f - 18f;
            for (int i = 0; i < order.Count; i++)
            {
                float a = Mathf.PI * 2f * i / Mathf.Max(1, order.Count) - Mathf.PI / 2f;
                nodes.Add(new Node { g = order[i], at = new Vector2(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry) });
            }
        }

        private static void Web(Rect area)
        {
            if (webRect != area || webVersion != Gods.Version || Time.realtimeSinceStartup - webAt > 5f) LayOut(area);
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                var at = new Dictionary<int, Vector2>();
                foreach (var n in nodes) at[n.g.id] = n.at;
                foreach (var n in nodes)
                    foreach (var t in Gods.TiesOf(n.g))
                    {
                        if (t.a != n.g.id || !at.TryGetValue(t.b, out var to)) continue;
                        Line(n.at, to, t.kind, t.strength);
                    }
            }
            Node hover = null;
            foreach (var n in nodes)
            {
                bool on = chosen == n.g.id;
                float s = on ? 26f : 22f;
                var dot = new Rect(n.at.x - s / 2f, n.at.y - s / 2f, s, s);
                if (e.type == EventType.Repaint)
                {
                    UiKit.DiscFill(dot.ExpandedBy(2f), UiKit.Ground);
                    GodSymbols.Draw(dot, n.g);
                    if (on) UiKit.RingDraw(dot.ExpandedBy(3f), UiKit.Gold);
                }
                var hit = dot.ExpandedBy(3f);
                if (Mouse.IsOver(hit)) hover = n;
                TooltipHandler.TipRegion(hit, new TipSignal(() => Gods.FullName(n.g) + "\n" + Gods.GodOf(n.g).CapitalizeFirst() + "\n" + TieList(n.g), n.g.id ^ 0x2B11));
                if (Widgets.ButtonInvisible(hit, false))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    chosen = n.g.id;
                    pageScroll = Vector2.zero;
                }
            }
            if (hover != null && e.type == EventType.Repaint)
            {
                string name = hover.g.name;
                float lw = Mathf.Min(UiKit.KeptWidth(name, GameFont.Tiny) + 8f, area.width * 0.7f);
                float lx = Mathf.Clamp(hover.at.x - lw / 2f, area.x, area.xMax - lw);
                var lr = new Rect(lx, hover.at.y + 14f, lw, 15f);
                Widgets.DrawBoxSolid(lr, UiKit.Shade);
                UiKit.LabelFit(lr, name, Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>A tie's line: its colour, thicker for a strong one, dashed for lovers estranged.</summary>
        private static void Line(Vector2 a, Vector2 b, GodTieKind k, float strength)
        {
            var col = UiKit.WithAlpha(GodWords.TieColor(k), 0.45f + 0.4f * strength);
            float w = strength >= 0.7f ? 2f : 1f;
            if (k != GodTieKind.Estranged)
            {
                Widgets.DrawLine(a, b, col, w);
                return;
            }
            float len = Vector2.Distance(a, b);
            int dashes = Mathf.Max(2, (int)(len / 8f));
            for (int i = 0; i < dashes; i += 2)
                Widgets.DrawLine(Vector2.Lerp(a, b, i / (float)dashes), Vector2.Lerp(a, b, (i + 1) / (float)dashes), col, w);
        }

        private static void Legend(Rect r)
        {
            var kinds = new[] { GodTieKind.Rivals, GodTieKind.Lovers, GodTieKind.Estranged, GodTieKind.Parent, GodTieKind.Allies, GodTieKind.CastDown };
            float x = r.x, y = r.y;
            foreach (var k in kinds)
            {
                string label = GodWords.TieWord(k);
                float lw = UiKit.KeptWidth(label, GameFont.Tiny) + 26f;
                if (x + lw > r.xMax)
                {
                    x = r.x;
                    y += 16f;
                }
                if (Event.current.type == EventType.Repaint) Line(new Vector2(x, y + 8f), new Vector2(x + 16f, y + 8f), k, 1f);
                UiKit.LabelFit(new Rect(x + 20f, y, lw - 20f, 16f), label, UiKit.Faint, GameFont.Tiny);
                x += lw + 6f;
            }
        }

        private static string TieList(God g)
        {
            var lines = new List<string>();
            foreach (var t in Gods.TiesOf(g))
            {
                var other = Gods.ById(t.Other(g.id));
                if (other == null || !other.known) continue;
                lines.Add(GodWords.TieFrom(g, t, other).CapitalizeFirst());
            }
            return string.Join("\n", lines);
        }

        // ================= A god's page =================

        private static void GodPage(Rect page, God g)
        {
            if (g == null) return;
            var view = new Rect(0f, 0f, page.width - 16f, pageHeight);
            Widgets.BeginScrollView(page, ref pageScroll, view);
            try
            {
                float y = g.known ? Known(view.width, g) : Rumour(view.width, g);
                pageHeight = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static float Rumour(float w, God g)
        {
            float y = 0f;
            GodSymbols.Draw(new Rect(0f, y, 56f, 56f), g, true);
            UiKit.LabelFit(new Rect(66f, y + 6f, w - 66f, 26f), RumourName(g).CapitalizeFirst(), UiKit.Faint, GameFont.Medium);
            UiKit.LabelFit(new Rect(66f, y + 32f, w - 66f, 18f), "CC_Gods_Rumour".Translate(), UiKit.Dim, GameFont.Tiny);
            y += 64f;
            y += UiKit.Paragraph(0f, y, w, RumourLine(g), UiKit.Ink) + 8f;
            y += UiKit.Paragraph(0f, y, w, "CC_Gods_HowLearn".Translate(), UiKit.Faint, GameFont.Tiny) + 6f;
            return y;
        }

        private static float Known(float w, God g)
        {
            var map = Home;
            float y = 0f;
            // The head: the symbol in its colours, its name in its colour, its epithet, what it is.
            GodSymbols.Draw(new Rect(0f, y, 56f, 56f), g);
            UiKit.LabelFit(new Rect(66f, y + 2f, w - 66f, 28f), g.name, g.colour, GameFont.Medium);
            UiKit.LabelFit(new Rect(66f, y + 28f, w - 66f, 18f), (g.epithet ?? "").CapitalizeFirst(), UiKit.Ink);
            string what = "CC_Gods_WhatLine".Translate(Gods.GodOf(g).CapitalizeFirst().Named("GODOF"), GodWords.FormWord(g.form).Named("FORM"),
                GodWords.NatureWord(g.nature).Named("NATURE")).Resolve();
            UiKit.LabelFit(new Rect(66f, y + 46f, w - 66f, 16f), what, UiKit.Faint, GameFont.Tiny);
            y += 68f;
            if (g.kind != GodKind.Ordinary)
            {
                UiKit.Pill(0f, y, GodWords.KindWord(g.kind), g.kind == GodKind.Hunger ? UiKit.Bad : UiKit.Gold);
                y += UiKit.Paragraph(0f, y + 20f, w, GodWords.KindLine(g.kind), UiKit.Ink, GameFont.Tiny) + 26f;
            }

            // The colony's favour, at each home.
            y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Favour".Translate());
            var homes = Homes.All;
            foreach (var home in homes.Count > 0 ? homes.ToList() : new List<Map> { map })
            {
                var word = Gods.Favour(g, home);
                bool patron = Gods.Patron(home) == g;
                string place = homes.Count > 1 ? Homes.Name(home) : "CC_God_TheColony".Translate().CapitalizeFirst();
                string line = "CC_Gods_FavourLine".Translate(place.Named("HOME"), GodWords.Word(word).Named("WORD")).Resolve();
                UiKit.LabelFit(new Rect(0f, y, w * 0.6f, 20f), line, GodWords.WordColor(word));
                if (patron) UiKit.LabelFit(new Rect(w * 0.6f, y, w * 0.4f, 20f), "CC_Gods_Patron".Translate(), UiKit.Gold, GameFont.Tiny, TextAnchor.MiddleRight);
                y += 20f;
            }
            y += UiKit.Paragraph(0f, y, w, "CC_Gods_FavourNote".Translate(), UiKit.Dim, GameFont.Tiny) + 8f;

            // Who holds it, and how the colony knows it.
            y += UiKit.SectionTitle(0f, y, w, "CC_Gods_HeldBy".Translate());
            y += UiKit.Paragraph(0f, y, w, Gods.PeopleWords(g).CapitalizeFirst(), UiKit.Ink);
            var faiths = g.ideos.Where(i => i != null).Select(i => i.name).Distinct().ToList();
            if (faiths.Count > 0) y += UiKit.Paragraph(0f, y, w, "CC_Gods_Faiths".Translate(faiths.ToCommaList(true).Named("FAITHS")).Resolve(), UiKit.Faint, GameFont.Tiny);
            if (!g.deityType.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, "CC_Gods_DeityType".Translate(g.deityType.Named("TYPE")).Resolve(), UiKit.Faint, GameFont.Tiny);
            if (!g.knownHow.NullOrEmpty())
                y += UiKit.Paragraph(0f, y, w, "CC_Gods_KnownHow".Translate(("CC_God_KnownFrom_" + g.knownHow).Translate(Gods.PeopleWords(g).Named("PEOPLE")).Named("FROM")).Resolve(),
                    UiKit.Faint, GameFont.Tiny);
            y += 8f;

            // Its domain and sacred things.
            var def = g.DomainDef;
            y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Domain".Translate());
            y += UiKit.Paragraph(0f, y, w, "CC_Gods_DomainLine".Translate(GodWords.Domains(g).Named("DOMAINS")).Resolve(), UiKit.Ink);
            if (def != null)
            {
                var animals = def.Animals.Select(a => a.label).Take(4).ToList();
                if (animals.Count > 0) y += UiKit.Paragraph(0f, y, w, "CC_Gods_SacredAnimals".Translate(animals.ToCommaList(true).Named("ANIMALS")).Resolve(), UiKit.Faint, GameFont.Tiny);
                y += UiKit.Paragraph(0f, y, w, "CC_Gods_SacredPlace".Translate(def.sacredPlace.Named("PLACE"), def.sacredDeed.Named("DEED")).Resolve(), UiKit.Faint, GameFont.Tiny);
            }
            y += 8f;

            // Its nature.
            y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Nature".Translate(GodWords.NatureWord(g.nature).CapitalizeFirst().Named("NATURE")).Resolve());
            y += UiKit.Paragraph(0f, y, w, GodWords.NatureLine(g.nature), UiKit.Ink) + 8f;

            // Its ties: a small web and the words.
            var ties = Gods.TiesOf(g);
            if (ties.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Ties".Translate(), ties.Count);
                y = SmallWeb(new Rect(0f, y, w, 150f), g, ties) + 4f;
                foreach (var t in ties)
                {
                    var other = Gods.ById(t.Other(g.id));
                    if (other == null) continue;
                    var r = new Rect(0f, y, w, 20f);
                    UiKit.DiscFill(new Rect(r.x + 2f, r.y + 6f, 8f, 8f), GodWords.TieColor(t.kind));
                    string text = other.known ? GodWords.TieFrom(g, t, other).CapitalizeFirst() : "CC_Gods_TieUnknown".Translate(GodWords.TieWord(t.kind).Named("TIE")).Resolve();
                    UiKit.LabelFit(new Rect(r.x + 16f, r.y, r.width - 16f, r.height), text, other.known ? UiKit.Ink : UiKit.Dim);
                    if (other.known)
                    {
                        UiKit.RowHover(r);
                        if (Widgets.ButtonInvisible(r, false))
                        {
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            chosen = other.id;
                            pageScroll = Vector2.zero;
                        }
                    }
                    y += 20f;
                }
                y += 8f;
            }

            // How its people tell of it.
            string lore = GodWords.Lore(g);
            if (!lore.NullOrEmpty())
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Lore".Translate());
                y += UiKit.Paragraph(0f, y, w, lore, UiKit.Ink) + 8f;
            }

            // What the colony remembers of it.
            var acts = Gods.ActsOf(g);
            y += UiKit.SectionTitle(0f, y, w, "CC_Gods_Acts".Translate(), acts.Count);
            if (acts.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Gods_NoActs".Translate(), UiKit.Dim, GameFont.Tiny);
            int now = Find.TickManager.TicksGame;
            foreach (var a in acts.Take(12))
            {
                string when = "CC_Gods_Ago".Translate(UiKit.Duration(Mathf.Max(0, now - a.tick)).Named("TIME")).Resolve();
                float ww = Mathf.Min(UiKit.KeptWidth(when, GameFont.Tiny) + 6f, 110f);
                UiKit.LabelFit(new Rect(w - ww, y, ww, 18f), when, UiKit.Dim, GameFont.Tiny, TextAnchor.UpperRight);
                y += UiKit.Paragraph(0f, y, w - ww - 4f, (a.text ?? "").CapitalizeFirst(), a.favour < 0f ? UiKit.Sour : UiKit.Ink, GameFont.Tiny) + 3f;
            }
            return y;
        }

        /// <summary>A god's ties as a small web: the god in the middle, those tied to it round it, the lines in the ties' colours.</summary>
        private static float SmallWeb(Rect r, God g, List<GodTie> ties)
        {
            var c = new Vector2(r.center.x, r.center.y);
            float rx = Mathf.Min(r.width / 2f - 30f, 140f), ry = r.height / 2f - 18f;
            var e = Event.current;
            for (int i = 0; i < ties.Count; i++)
            {
                var t = ties[i];
                var other = Gods.ById(t.Other(g.id));
                if (other == null) continue;
                float a = Mathf.PI * 2f * i / ties.Count - Mathf.PI / 2f;
                var at = new Vector2(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry);
                if (e.type == EventType.Repaint) Line(c, at, t.kind, Mathf.Max(0.6f, t.strength));
                var dot = new Rect(at.x - 11f, at.y - 11f, 22f, 22f);
                if (e.type == EventType.Repaint)
                {
                    UiKit.DiscFill(dot.ExpandedBy(2f), UiKit.Ground);
                    GodSymbols.Draw(dot, other, !other.known);
                }
                string name = other.known ? other.name : "CC_Gods_Unknown".Translate().ToString();
                float lw = Mathf.Min(UiKit.KeptWidth(name, GameFont.Tiny) + 6f, 120f);
                UiKit.LabelFit(new Rect(Mathf.Clamp(at.x - lw / 2f, r.x, r.xMax - lw), at.y + 12f, lw, 14f), name, other.known ? UiKit.Faint : UiKit.Dim, GameFont.Tiny,
                    TextAnchor.MiddleCenter);
            }
            var me = new Rect(c.x - 15f, c.y - 15f, 30f, 30f);
            if (e.type == EventType.Repaint)
            {
                UiKit.DiscFill(me.ExpandedBy(2f), UiKit.Ground);
                GodSymbols.Draw(me, g);
                UiKit.RingDraw(me.ExpandedBy(3f), UiKit.Gold);
            }
            return r.yMax;
        }
    }
}
