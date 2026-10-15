using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People → Bonds: who is close to whom. Two views: a web (portraits in a circle, a line for each bond, colored
    /// by kind and thicker the stronger it is) and a list (circles, then every bond by kind with its strength and
    /// history, then vendettas, nemeses and secrets).
    /// </summary>
    public static class SocialPanel
    {
        private enum Mode
        {
            Web,
            List
        }

        private static Mode mode = Mode.Web;
        private static Vector2 scroll;
        private static float height = 600f;
        private static Pawn focus;

        private const float RowH = 28f;

        private static readonly Color CliqueColor = new Color(1f, 0.85f, 0.5f);

        private static bool Shown(Pawn p) => p != null && !p.Dead && p.IsColonist;

        /// <summary>Who and what the page shows, refreshed twice a second.</summary>
        private class Model
        {
            public List<Pawn> colonists;
            public List<SocialTie> ties;
            public List<Clique> cliques;
        }

        private static Model model;
        private static float modelAt = -1f;
        private static Pawn modelFocus;

        private static Model Build(CouncilGameComponent comp)
        {
            var m = new Model();
            var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike).ToList();
            bool on = ColonyCouncilMod.Settings.socialWeb;
            m.cliques = on ? comp.social.Cliques.Where(c => c.members.Count(Shown) >= 2).ToList() : new List<Clique>();
            // Circle members next to each other, so the web can mark each circle with one arc.
            m.colonists = colonists.OrderBy(p => CliqueIndex(m.cliques, p)).ThenBy(p => p.LabelShort).ToList();
            m.ties = on ? comp.social.ties.Where(t => Shown(t.a) && Shown(t.b) && t.announced && colonists.Contains(t.a) && colonists.Contains(t.b)).ToList()
                : new List<SocialTie>();
            return m;
        }

        private static int CliqueIndex(List<Clique> cliques, Pawn p)
        {
            for (int i = 0; i < cliques.Count; i++)
                if (cliques[i].members.Contains(p)) return i;
            return cliques.Count;
        }

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (model == null || Time.realtimeSinceStartup - modelAt > 0.5f || modelFocus != focus)
            {
                model = Build(comp);
                modelAt = Time.realtimeSinceStartup;
                modelFocus = focus;
            }
            var m = model;
            if (focus != null && !m.colonists.Contains(focus)) focus = null;

            // Header, with the view switch and the filter on the right.
            float reserve = 220f + 6f + 2f * 74f + (Prefs.DevMode ? 306f : 0f);
            float top = UiKit.PageHeader(rect, Icons.Bonds, "CC_Tab_Bonds".Translate(), "CC_UI_Sub_Bonds".Translate(),
                "CC_UI_Stat_Bonds".Translate(m.ties.Count).Resolve(), null, reserve, "CC_Social_Intro".Translate());
            float bx = rect.xMax - 220f;
            var filterRect = new Rect(bx, rect.y + 6f, 220f, 30f);
            if (Widgets.ButtonText(filterRect, UiKit.Fit("CC_Social_Filter".Translate() + ": " + (focus?.LabelShort ?? "CC_Social_Everyone".Translate().ToString()), 200f)))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_Social_Everyone".Translate(), () => focus = null) };
                options.AddRange(m.colonists.OrderBy(p => p.LabelShort).Select(p => new FloatMenuOption(p.LabelShort, () => focus = p)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            bx -= 6f + 74f;
            if (UiKit.TabButton(new Rect(bx, rect.y + 6f, 74f, 30f), "CC_UI_View_List".Translate(), null, mode == Mode.List)) mode = Mode.List;
            bx -= 74f;
            if (UiKit.TabButton(new Rect(bx, rect.y + 6f, 74f, 30f), "CC_UI_View_Web".Translate(), null, mode == Mode.Web)) mode = Mode.Web;
            if (Prefs.DevMode) DevButtons(new Rect(bx - 306f, rect.y + 6f, 300f, 30f), comp);

            var body = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            if (!ColonyCouncilMod.Settings.socialWeb)
            {
                DrawList(body, comp, m);
                return;
            }
            if (mode == Mode.Web) DrawWeb(body, m);
            else DrawList(body, comp, m);
        }

        private static void DevButtons(Rect r, CouncilGameComponent comp)
        {
            if (Find.CurrentMap == null) return;
            if (Widgets.ButtonText(new Rect(r.x, r.y, 120f, r.height), "Dev: dream"))
            {
                var dreamer = focus ?? Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby()).RandomElementWithFallback();
                if (dreamer == null || !comp.dreams.TryDream(comp, dreamer, false)) Messages.Message("No dream fits right now.", MessageTypeDefOf.RejectInput, false);
            }
            if (Widgets.ButtonText(new Rect(r.x + 126f, r.y, 170f, r.height), "Dev: social event"))
            {
                var pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();
                if (!SocialEvents.TryOne(comp, Find.CurrentMap, pawns)) Messages.Message("No social event fits right now.", MessageTypeDefOf.RejectInput, false);
            }
        }

        // ---------------- The web ----------------

        private static readonly List<Vector2> nodes = new List<Vector2>();
        private static readonly HashSet<Pawn> litNeighbors = new HashSet<Pawn>();
        private static readonly Dictionary<Pawn, int> index = new Dictionary<Pawn, int>();
        private static readonly Dictionary<long, int> pairSeen = new Dictionary<long, int>();
        private static readonly Dictionary<long, int> pairCount = new Dictionary<long, int>();

        private static void DrawWeb(Rect rect, Model m)
        {
            const float legendH = 26f;
            var area = new Rect(rect.x, rect.y, rect.width, rect.height - legendH - 4f);
            Widgets.DrawMenuSection(area);
            int n = m.colonists.Count;
            if (n == 0)
            {
                UiKit.EmptyState(area, Icons.Bonds, "CC_People_None".Translate());
                return;
            }

            // Place everyone on a circle; portraits shrink as the colony grows.
            var center = area.center;
            float half = Mathf.Min(area.width, area.height) / 2f;
            float d = 52f;
            float radius = half - d / 2f - 22f;
            for (int i = 0; i < 2; i++)
            {
                d = n == 1 ? 56f : Mathf.Clamp(2f * Mathf.PI * radius / n * 0.55f, 24f, 56f);
                radius = Mathf.Max(10f, half - d / 2f - 22f);
            }
            nodes.Clear();
            index.Clear();
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2f + 2f * Mathf.PI * i / n;
                nodes.Add(n == 1 ? center : center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                index[m.colonists[i]] = i;
            }

            // Who's under the mouse (or picked in the filter): their bonds stand out, the rest fade.
            Pawn hovered = null;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < n; i++)
                if ((mouse - nodes[i]).sqrMagnitude <= (d / 2f + 2f) * (d / 2f + 2f) && Mouse.IsOver(area))
                    hovered = m.colonists[i];
            var lit = hovered ?? focus;
            litNeighbors.Clear();
            if (lit != null)
                foreach (var t in m.ties)
                    if (t.Involves(lit)) litNeighbors.Add(t.Other(lit));

            // Circles: a soft gold band through their members, like beads on a string.
            if (n > 2)
                foreach (var c in m.cliques)
                    DrawCliqueArc(c, m, center, radius, n, d * 0.45f);

            // The bonds: dim ones first, lit ones on top.
            pairSeen.Clear();
            pairCount.Clear();
            foreach (var t in m.ties)
            {
                long key = PairKey(t);
                pairCount[key] = pairCount.TryGetValue(key, out int c) ? c + 1 : 1;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var t in m.ties)
                {
                    bool on = lit != null && t.Involves(lit);
                    if (pass == 0 ? on : !on) continue;
                    if (!index.TryGetValue(t.a, out int ia) || !index.TryGetValue(t.b, out int ib)) continue;
                    long key = PairKey(t);
                    int k = pairSeen.TryGetValue(key, out int s) ? s : 0;
                    pairSeen[key] = k + 1;
                    DrawTieLine(nodes[ia], nodes[ib], t, d, lit == null ? 1 : on ? 2 : 0, k - (pairCount[key] - 1) / 2f);
                }
            }

            // The people.
            Text.Font = GameFont.Tiny;
            float labelW = n == 1 ? 160f : Mathf.Clamp(2f * Mathf.PI * radius / n - 6f, 40f, 140f);
            for (int i = 0; i < n; i++)
            {
                var p = m.colonists[i];
                bool near = lit == null || p == lit || litNeighbors.Contains(p);
                float alpha = near ? 1f : 0.35f;
                var nr = new Rect(nodes[i].x - d / 2f, nodes[i].y - d / 2f, d, d);
                if (p == lit) UiKit.DiscFill(nr.ExpandedBy(5f), UiKit.WithAlpha(UiKit.Gold, 0.3f));
                var comp = CouncilGameComponent.Instance;
                UiKit.Portrait(nr, p, comp != null ? UiKit.TrustColor(comp.standings.TrustOf(p)) : UiKit.Neutral, alpha);
                // The name under the portrait, kept inside the panel.
                float lx = Mathf.Clamp(nodes[i].x - labelW / 2f, area.x + 2f, Mathf.Max(area.x + 2f, area.xMax - 2f - labelW));
                var lr = new Rect(lx, nr.yMax + 1f, Mathf.Min(labelW, area.width - 4f), 18f);
                UiKit.LabelFit(lr, p.LabelShort, new Color(1f, 1f, 1f, p == lit ? 1f : near ? 0.85f : 0.35f), GameFont.Tiny, TextAnchor.UpperCenter);
                var pawn = p;
                TooltipHandler.TipRegion(nr, () => NodeTip(pawn, m), p.thingIDNumber ^ 0x0B0D);
                if (Widgets.ButtonInvisible(nr)) focus = focus == p ? null : p;
            }
            Text.Font = GameFont.Small;
            if (m.ties.Count == 0) UiKit.LabelFit(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, 22f), "CC_Social_None".Translate(), UiKit.Faint);

            DrawLegend(new Rect(rect.x, rect.yMax - legendH, rect.width, legendH), m.cliques.Count > 0);
        }

        private static long PairKey(SocialTie t)
        {
            int x = t.a.thingIDNumber, y = t.b.thingIDNumber;
            return x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
        }

        /// <summary>A line between two portraits, meeting their rings; mode 0 faded, 1 normal, 2 lit.</summary>
        private static void DrawTieLine(Vector2 a, Vector2 b, SocialTie t, float d, int mode, float offsetIndex)
        {
            var dir = b - a;
            float len = dir.magnitude;
            if (len < d) return;
            dir /= len;
            var normal = new Vector2(-dir.y, dir.x) * (offsetIndex * 5f);
            var from = a + dir * (d / 2f + 1f) + normal;
            var to = b - dir * (d / 2f + 1f) + normal;
            float strength = Mathf.Clamp01(t.strength / 100f);
            float width = 1.5f + 3f * strength + (mode == 2 ? 1f : 0f);
            float alpha = mode == 0 ? 0.1f : mode == 2 ? 1f : 0.45f + 0.45f * strength;
            Widgets.DrawLine(from, to, UiKit.WithAlpha(UiKit.TieColor(t.kind), alpha), width);
            // A mentor's line (drawn from teacher to student) ends in a dot at the student.
            if (t.kind == TieKind.Mentor && mode != 0) UiKit.DiscFill(new Rect(to.x - 4f, to.y - 4f, 8f, 8f), UiKit.WithAlpha(UiKit.TieColor(t.kind), alpha));
        }

        private static void DrawCliqueArc(Clique c, Model m, Vector2 center, float r, int n, float width)
        {
            int first = -1, last = -1;
            for (int i = 0; i < m.colonists.Count; i++)
            {
                if (!c.members.Contains(m.colonists[i])) continue;
                if (first < 0) first = i;
                last = i;
            }
            if (first < 0 || last <= first) return;
            float step = 2f * Mathf.PI / n;
            float a0 = -Mathf.PI / 2f + first * step - step * 0.35f;
            float a1 = -Mathf.PI / 2f + last * step + step * 0.35f;
            var color = UiKit.WithAlpha(CliqueColor, 0.18f);
            int segs = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / 0.06f));
            var prev = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
            for (int s = 1; s <= segs; s++)
            {
                float a = Mathf.Lerp(a0, a1, s / (float)segs);
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Widgets.DrawLine(prev, p, color, width);
                prev = p;
            }
        }

        private static string NodeTip(Pawn p, Model m)
        {
            var lines = new List<string> { p.LabelShortCap };
            var clique = m.cliques.FirstOrDefault(c => c.members.Contains(p));
            if (clique != null) lines.Add(clique.name.Colorize(CliqueColor));
            foreach (var t in m.ties.Where(t => t.Involves(p)).OrderByDescending(t => t.strength))
                lines.Add(t.Other(p).LabelShort + ": " + t.Label + " (" + t.strength.ToString("0") + ")");
            lines.Add("");
            lines.Add("CC_UI_Web_Click".Translate(p.LabelShort));
            return string.Join("\n", lines);
        }

        private static void DrawLegend(Rect r, bool circles)
        {
            float x = r.x + 4f;
            Text.Font = GameFont.Tiny;
            foreach (var kind in new[] { TieKind.Friends, TieKind.Rivals, TieKind.Comrades, TieKind.Mentor })
            {
                string label = ("CC_Tie_" + kind).Translate().CapitalizeFirst();
                float w = Text.CalcSize(label).x;
                if (x + 26f + w > r.xMax) break;
                Widgets.DrawLine(new Vector2(x, r.center.y), new Vector2(x + 18f, r.center.y), UiKit.TieColor(kind), 3f);
                UiKit.LabelFit(new Rect(x + 22f, r.y, w + 4f, r.height), label, UiKit.Faint, GameFont.Tiny);
                x += 22f + w + 14f;
            }
            if (circles)
            {
                string label = "CC_Social_Cliques".Translate();
                float w = Text.CalcSize(label).x;
                if (x + 26f + w <= r.xMax)
                {
                    Widgets.DrawLine(new Vector2(x, r.center.y), new Vector2(x + 18f, r.center.y), UiKit.WithAlpha(CliqueColor, 0.45f), 5f);
                    UiKit.LabelFit(new Rect(x + 22f, r.y, w + 4f, r.height), label, UiKit.Faint, GameFont.Tiny);
                    x += 22f + w + 14f;
                }
            }
            string hint = "CC_UI_Web_Legend".Translate();
            if (r.xMax - x > 60f) UiKit.LabelFit(new Rect(x, r.y, r.xMax - x - 4f, r.height), hint, UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
            Text.Font = GameFont.Small;
        }

        // ---------------- The list ----------------

        private static void DrawList(Rect rect, CouncilGameComponent comp, Model m)
        {
            var ties = focus == null ? m.ties : m.ties.Where(t => t.Involves(focus)).ToList();
            var view = new Rect(0f, 0f, rect.width - 16f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            float cy = 0f;
            float w = view.width;
            cy += UiKit.Paragraph(0f, cy, w, "CC_Social_Intro".Translate(), UiKit.Faint) + 10f;
            if (ties.Count == 0 && ColonyCouncilMod.Settings.socialWeb) cy += UiKit.EmptyState(0f, cy, w, Icons.Bonds, "CC_Social_None".Translate(), 90f);

            var cliques = focus == null ? m.cliques : m.cliques.Where(c => c.members.Contains(focus)).ToList();
            if (cliques.Count > 0)
            {
                cy += UiKit.SectionTitle(0f, cy, w, "CC_Social_Cliques".Translate(), Icons.Bonds, CliqueColor);
                for (int i = 0; i < cliques.Count; i++)
                {
                    var c = cliques[i];
                    float h = CliqueRow(c, 0f, cy, w, i);
                    cy += h + 2f;
                }
                cy += 12f;
            }

            foreach (var kind in new[] { TieKind.Friends, TieKind.Rivals, TieKind.Comrades, TieKind.Mentor })
            {
                var list = ties.Where(t => t.kind == kind).OrderByDescending(t => t.strength).ToList();
                if (list.Count == 0) continue;
                cy += UiKit.SectionTitle(0f, cy, w, ("CC_TieKind_" + kind).Translate(), Icons.For(kind), UiKit.TieColor(kind), list.Count.ToString());
                for (int i = 0; i < list.Count; i++)
                {
                    DrawTie(new Rect(0f, cy, w, RowH), list[i], i);
                    cy += RowH;
                }
                cy += 12f;
            }
            if (ColonyCouncilMod.Settings.vendettas) cy += DrawVendettas(comp, w, cy);
            if (ColonyCouncilMod.Settings.nemeses) cy += DrawNemeses(comp, w, cy);
            if (ColonyCouncilMod.Settings.secrets) cy += DrawSecrets(comp, w, cy);
            height = Mathf.Max(cy, 100f);
            Widgets.EndScrollView();
        }

        /// <summary>A circle's name, then its members as chips, wrapping. Returns the row's height.</summary>
        private static float CliqueRow(Clique c, float x, float y, float w, int i)
        {
            const float nameW = 200f;
            float chipsW = Mathf.Max(100f, w - nameW - 10f);
            // Lay the chips out first, to know the height.
            float cx = 0f, rows = 1f;
            var members = c.members.Where(Shown).ToList();
            Text.Font = GameFont.Small;
            foreach (var p in members)
            {
                float cw = Mathf.Min(150f, 22f + 5f + Text.CalcSize(p.LabelShort).x) + 8f;
                if (cx > 0f && cx + cw > chipsW)
                {
                    cx = 0f;
                    rows++;
                }
                cx += cw;
            }
            float h = Mathf.Max(RowH, rows * 26f + 2f);
            var r = new Rect(x, y, w, h);
            UiKit.Zebra(r, i);
            UiKit.Icon(new Rect(x + 6f, y + 6f, 16f, 16f), Icons.Bonds, CliqueColor);
            UiKit.LabelFit(new Rect(x + 28f, y + 2f, nameW - 30f, 24f), c.name, CliqueColor);
            TooltipHandler.TipRegion(new Rect(x, y, nameW, h), "CC_Social_CliqueTip".Translate());
            cx = 0f;
            float cy = y + 2f;
            foreach (var p in members)
            {
                float cw = Mathf.Min(150f, 22f + 5f + Text.CalcSize(p.LabelShort).x) + 8f;
                if (cx > 0f && cx + cw > chipsW)
                {
                    cx = 0f;
                    cy += 26f;
                }
                UiKit.PawnChip(x + nameW + cx, cy + 2f, p, 22f, true, 150f);
                cx += cw;
            }
            return h;
        }

        private static float DrawVendettas(CouncilGameComponent comp, float width, float y)
        {
            var list = comp.vendettas.vendettas.Where(v => v.avenger != null && !v.avenger.Dead && v.avenger.IsColonist && v.faction != null && (focus == null || v.avenger == focus))
                .OrderBy(v => v.settled).ThenByDescending(v => v.tick).Take(10).ToList();
            if (list.Count == 0) return 0f;
            float start = y;
            y += UiKit.SectionTitle(0f, y, width, "CC_Social_Vendettas".Translate(), Icons.Vendetta, UiKit.Bad);
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                string line = v.LineKey
                    .Translate(v.avenger.LabelShort, v.faction.Name, v.victim, v.kills, Vendetta.KillsToSettle).Resolve();
                var r = new Rect(0f, y, width, RowH);
                UiKit.Zebra(r, i);
                UiKit.Portrait(new Rect(4f, y + 3f, 22f, 22f), v.avenger, v.settled ? UiKit.Neutral : UiKit.Bad);
                UiKit.LabelFit(new Rect(32f, y, width - 36f, RowH), line, v.settled ? UiKit.Faint : new Color(0.9f, 0.55f, 0.45f));
                y += RowH;
            }
            return y - start + 12f;
        }

        private static float DrawNemeses(CouncilGameComponent comp, float width, float y)
        {
            var list = comp.nemeses.all.Where(n => n.announced && (focus == null || n.victim == focus))
                .OrderBy(n => n.ended).ThenByDescending(n => n.tick).Take(10).ToList();
            if (list.Count == 0) return 0f;
            float start = y;
            y += UiKit.SectionTitle(0f, y, width, "CC_Social_Nemeses".Translate(), Icons.Nemesis, UiKit.Bad);
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                var r = new Rect(0f, y, width, RowH);
                UiKit.Zebra(r, i);
                UiKit.Icon(new Rect(7f, y + 6f, 16f, 16f), Icons.Nemesis, n.ended ? UiKit.Faint : UiKit.Bad);
                UiKit.LabelFit(new Rect(32f, y, width - 36f, RowH), n.Line, n.ended ? UiKit.Faint : new Color(0.9f, 0.5f, 0.4f));
                if (n.endText != null) TooltipHandler.TipRegion(r, n.endText);
                y += RowH;
            }
            return y - start + 12f;
        }

        /// <summary>Only what the colony could know: who has confided in whom (not what), and secrets that are out.</summary>
        private static float DrawSecrets(CouncilGameComponent comp, float width, float y)
        {
            var list = comp.secrets.secrets.Where(s => s.owner != null && !s.owner.Dead && s.owner.IsColonist && (focus == null || s.owner == focus)
                                                       && (s.revealed || s.knowers.Count > 0 || Prefs.DevMode)).OrderBy(s => s.revealed).ToList();
            if (list.Count == 0) return 0f;
            float start = y;
            y += UiKit.SectionTitle(0f, y, width, "CC_Social_Secrets".Translate(), Icons.Secret, UiKit.HeaderColor);
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                string line;
                if (s.revealed) line = "CC_Social_SecretOut".Translate(s.owner.LabelShort, s.def.label).Resolve();
                else if (s.knowers.Count > 0) line = "CC_Social_SecretKnown".Translate(s.owner.LabelShort, TextUtil.JoinAnd(s.knowers.Select(k => k.LabelShort).ToList())).Resolve();
                else line = "(dev) " + s.owner.LabelShort + ": " + s.def.label;
                var r = new Rect(0f, y, width, RowH);
                UiKit.Zebra(r, i);
                Widgets.DrawHighlightIfMouseover(r);
                var color = s.revealed ? (s.def.good ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.6f, 0.5f)) : UiKit.Faint;
                UiKit.Portrait(new Rect(4f, y + 3f, 22f, 22f), s.owner, color);
                UiKit.LabelFit(new Rect(32f, y, width - 36f, RowH), line, color);
                var secret = s;
                if (s.revealed) TooltipHandler.TipRegion(r, () => secret.Truth, s.GetHashCode());
                else if (Prefs.DevMode) TooltipHandler.TipRegion(r, () => "(dev) " + secret.Truth, s.GetHashCode());
                y += RowH;
            }
            return y - start + 12f;
        }

        private static void DrawTie(Rect r, SocialTie t, int i)
        {
            UiKit.Zebra(r, i);
            Widgets.DrawHighlightIfMouseover(r);
            var color = UiKit.TieColor(t.kind);
            // The two of them: overlapping portraits, then their names.
            UiKit.Portrait(new Rect(r.x + 4f, r.y + 3f, 22f, 22f), t.a, color);
            UiKit.Portrait(new Rect(r.x + 20f, r.y + 3f, 22f, 22f), t.b, color);
            string names = t.kind == TieKind.Mentor && t.skill != null
                ? t.a.LabelShort + " → " + t.b.LabelShort + " (" + t.skill.label + ")"
                : t.a.LabelShort + " & " + t.b.LabelShort;
            float nameW = Mathf.Min(280f, r.width * 0.42f);
            UiKit.LabelFit(new Rect(r.x + 48f, r.y, nameW - 50f, r.height), names, Color.white);
            float bx = r.x + nameW + 4f;
            float barW = Mathf.Min(150f, Mathf.Max(60f, r.width * 0.2f));
            UiKit.Bar(new Rect(bx, r.y + 10f, barW, 8f), t.strength / 100f, color);
            string stage = (t.strength >= 70f ? "CC_Social_Deep" : t.strength >= 45f ? "CC_Social_Solid" : "CC_Social_Weak").Translate();
            UiKit.LabelFit(new Rect(bx + barW + 8f, r.y, 64f, r.height), stage, UiKit.Faint, GameFont.Tiny);
            string last = t.history.Count > 0 ? t.history[t.history.Count - 1] : "";
            float noteX = bx + barW + 76f;
            if (r.xMax - noteX > 40f) UiKit.LabelFit(new Rect(noteX, r.y, r.xMax - noteX - 4f, r.height), last, UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, () => Tip(t), t.GetHashCode());
            if (Widgets.ButtonInvisible(r) && t.a.Spawned) CameraJumper.TryJumpAndSelect(t.a);
        }

        private static string Tip(SocialTie t)
        {
            var lines = new List<string>
            {
                t.a.LabelShort + " & " + t.b.LabelShort + ": " + t.Label,
                "CC_Social_Strength".Translate(t.strength.ToString("0")),
                "CC_Social_Since".Translate("CC_DayN".Translate(t.formedTick / GenDate.TicksPerDay + 1))
            };
            if (t.kind == TieKind.Mentor && t.skill != null)
                lines.Add("CC_Social_Lessons".Translate(t.skill.label, t.b.LabelShort, t.b.skills?.GetSkill(t.skill)?.Level ?? 0));
            if (t.history.Count > 0) lines.Add("\n" + string.Join("\n", t.history));
            lines.Add("\n" + "CC_Social_TieTip_Trust".Translate());
            return string.Join("\n", lines);
        }
    }
}
