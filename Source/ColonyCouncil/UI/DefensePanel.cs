using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → Defenses: the colony's defense read from its map. On the left, the map with the ways raiders walk in and
    /// the narrow places on them, where they come from, what the colony faces, who fights, what stands already, and how
    /// it's commonly done. On the right, the advice (most urgent first, what's done marked), each with its reasoning and
    /// a button that puts the plan down at the place it's about, and the defense plans to place by hand. The survey is
    /// worked out when the page opens (at most once a game hour unless asked again); the page's text once a second.
    /// </summary>
    public static class DefensePanel
    {
        private static Vector2 scroll;
        private static float height = 900f;
        private static Model model;
        private static float modelAt = -1f;
        private static Texture2D mini;
        private static DefenseSurvey miniFor;
        private static readonly Dictionary<(string, int, GameFont), float> heights = new Dictionary<(string, int, GameFont), float>();

        private static readonly Color CriticalColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color MediumColor = new Color(0.9f, 0.85f, 0.35f);
        private static readonly Color LowColor = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color IdeaColor = new Color(0.45f, 0.7f, 0.95f);

        private const float LeftW = 330f;
        private const float Gap = 14f;
        private const float ButtonsW = 150f;

        private class Model
        {
            public DefenseSurvey s;
            public List<DefenseAdviceItem> advice;
            public List<string> from = new List<string>(), face = new List<string>(), fight = new List<string>(), stand = new List<string>(), how = new List<string>();
            public List<(DefenseTemplateDef t, string line, string reason)> plans = new List<(DefenseTemplateDef, string, string)>();
            public Pawn marshal;
            public string stat;
            public int open;
        }

        public static void Draw(Rect rect, Map map)
        {
            if (map == null || !map.IsPlayerHome)
            {
                UiKit.EmptyState(rect, Icons.CatDefense, "CC_NoHomeMap".Translate());
                return;
            }
            DefenseOverlay.PageDrawn();
            if (model == null || model.s?.map != map || Time.realtimeSinceStartup - modelAt > 1f)
            {
                try
                {
                    model = Build(DefenseSurvey.For(map));
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("defenses page", e);
                    model = new Model();
                }
                modelAt = Time.realtimeSinceStartup;
            }
            var m = model;
            float top = UiKit.PageHeader(rect, Icons.Defenses ?? Icons.CatDefense, "CC_Def_Header".Translate(), "CC_Def_Sub".Translate(), m.stat, null, 300f);
            var again = new Rect(rect.xMax - 140f, rect.y + 6f, 140f, 26f);
            if (Widgets.ButtonText(again, "CC_Def_SurveyAgain".Translate()))
            {
                DefenseSurvey.For(map, true);
                modelAt = -1f;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(again, "CC_Def_SurveyAgain_Tip".Translate());
            bool show = DefenseOverlay.Show;
            var showRect = new Rect(rect.xMax - 300f, rect.y + 8f, 150f, 24f);
            Widgets.CheckboxLabeled(showRect, "CC_Def_ShowOnMap".Translate(), ref show);
            TooltipHandler.TipRegion(showRect, "CC_Def_ShowOnMap_Tip".Translate());
            if (show != DefenseOverlay.Show) DefenseOverlay.Show = show;
            if (m.s == null)
            {
                UiKit.EmptyState(new Rect(rect.x, rect.y + top, rect.width, rect.height - top), Icons.CatDefense, "CC_Def_NoSurvey".Translate());
                return;
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                bool two = w >= 760f;
                float lw = two ? LeftW : w;
                float rx = two ? lw + Gap : 0f;
                float rw = two ? w - lw - Gap : w;
                float left = DrawLeft(m, 0f, 0f, lw);
                float right = DrawRight(m, rx, two ? 0f : left + 10f, rw);
                height = Mathf.Max(two ? Mathf.Max(left, right) : right, 100f) + 10f;
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------- The model ----------------

        private static Model Build(DefenseSurvey s)
        {
            var m = new Model { s = s };
            heights.Clear();
            if (s == null) return m;
            var r = s.result;
            m.advice = DefenseAdvice.For(s);
            m.open = m.advice.Count(a => !a.done && !a.standing);
            m.stat = "CC_Def_Stat".Translate(s.raidPoints.ToString("0")).Resolve();

            // Where they come from.
            if (r == null || r.unreachable) m.from.Add("CC_Def_From_None".Translate());
            else
            {
                var sides = Enumerable.Range(0, 4).Where(i => r.sideReach[i] > 0).Select(i => DefenseSurvey.FacingWord(i)).ToList();
                m.from.Add(sides.Count == 4 ? "CC_Def_From_All".Translate().ToString()
                    : sides.Count == 1 ? "CC_Def_From_One".Translate(sides[0]).ToString()
                    : "CC_Def_From_Many".Translate(TextUtil.JoinAnd(sides)).ToString());
                if (r.concentration >= 0.35f) m.from.Add("CC_Def_From_Mostly".Translate(DefenseSurvey.Direction(DefenseMath.Compass(r.angle))).ToString());
                if (r.enclosed) m.from.Add("CC_Def_Enclosed".Translate());
                else if (r.openings > 0) m.from.Add("CC_Def_Openings".Translate(r.openings));
                for (int i = 0; i < s.chokes.Count && i < 3; i++)
                {
                    var c = s.chokes[i];
                    m.from.Add("CC_Def_GapSummary".Translate(DefenseAdvice.Where(s, c.cell), s.GapWords(c), DefenseSurvey.Cells(c.c.width), Mathf.Max(1, Mathf.RoundToInt(c.c.share * 10f))).Resolve());
                }
                if (s.chokes.Count == 0) m.from.Add("CC_Def_NoGaps".Translate());
            }

            // What they face.
            m.face.Add("CC_Def_Face_Points".Translate(s.raidPoints.ToString("0")).Resolve());
            if (s.hostiles.Count > 0) m.face.Add("CC_Def_Face_Hostile".Translate(TextUtil.JoinAnd(s.hostiles.Select(f => f.Name).Distinct().Take(5).ToList())).Resolve());
            else m.face.Add("CC_Def_Face_NoHostile".Translate());
            if (s.canDrop) m.face.Add("CC_Def_Face_Pods".Translate());
            if (s.canSiege) m.face.Add("CC_Def_Face_Siege".Translate());
            if (s.canBreach) m.face.Add("CC_Def_Face_Breach".Translate());
            if (s.mechanoids) m.face.Add((s.mechsYet ? "CC_Def_Face_Mechs" : "CC_Def_Face_MechsLater").Translate());
            if (s.mechClusters) m.face.Add("CC_Def_Face_Cluster".Translate());
            if (s.thickRoof >= 40) m.face.Add("CC_Def_Face_Insects".Translate(s.thickRoof));
            if (s.anomaly) m.face.Add("CC_Def_Face_Anomaly".Translate());
            if (s.wastepacks > 0) m.face.Add("CC_Def_Face_Waste".Translate(s.wastepacks));

            // Who fights.
            m.fight.Add("CC_Def_Fight_Count".Translate(s.fighters, s.ranged, s.melee).Resolve() + (s.unarmed > 0 ? " " + "CC_Def_Fight_Unarmed".Translate(s.unarmed).Resolve() : ""));
            if (s.bestShooter != null)
                m.fight.Add("CC_Def_Fight_Shooter".Translate(s.bestShooter.Named("PAWN"), s.bestShooter.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, s.bestShooter.equipment?.Primary?.LabelNoCount ?? "").Resolve());
            if (s.bestMelee != null)
                m.fight.Add("CC_Def_Fight_Melee".Translate(s.bestMelee.Named("PAWN"), s.bestMelee.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0, s.bestMelee.equipment?.Primary?.LabelNoCount ?? "").Resolve());
            if (s.bestRange > 0f) m.fight.Add("CC_Def_Fight_Reach".Translate(s.bestRange.ToString("0")).Resolve());

            // What stands already.
            m.stand.Add("CC_Def_Stand_Walls".Translate(s.walls, s.doors).Resolve() + " " + (r != null && r.enclosed ? "CC_Def_Stand_Enclosed" : "CC_Def_Stand_Open").Translate().Resolve());
            if (s.autoTurrets + s.mannedTurrets > 0) m.stand.Add("CC_Def_Stand_Turrets".Translate(s.autoTurrets + s.mannedTurrets).Resolve());
            m.stand.Add("CC_Def_Stand_Cover".Translate(s.cover, s.traps).Resolve());
            if (s.embrasures > 0) m.stand.Add("CC_Def_Stand_Embrasures".Translate(s.embrasures).Resolve());
            if (s.mortars > 0) m.stand.Add("CC_Def_Stand_Mortars".Translate(s.mortars).Resolve());
            if (s.outerWalls > 0) m.stand.Add("CC_Def_Stand_Double".Translate(Mathf.RoundToInt(s.doubleWalls * 100f)).Resolve());

            // How it's commonly done.
            bool low = Avail.IsPreIndustrial;
            if (r != null && !r.unreachable)
            {
                if (s.thickRoof >= 200) m.how.Add("CC_Def_How_Mountain".Translate());
                else if (s.chokes.Count > 0 && s.chokes[0].c.share >= 0.6f && !s.chokes[0].gated) m.how.Add("CC_Def_How_Pass".Translate());
                else if (r.openShare >= 0.5f) m.how.Add("CC_Def_How_Open".Translate());
            }
            for (int i = 0; i < 5; i++) m.how.Add(((low ? "CC_Def_How_Low_" : "CC_Def_How_High_") + i).Translate());

            // The plans.
            foreach (var t in DefenseTemplateDef.All)
            {
                var pieces = t.ResolveCached(s.map, out string reason);
                string line = reason ?? (pieces.Count == 0 ? "" : "CC_Build_Contains".Translate(string.Join(", ",
                    pieces.GroupBy(p => p.def).Select(g => g.Count() > 1 ? g.Count() + "× " + g.Key.label : g.Key.label))).Resolve());
                m.plans.Add((t, line, reason));
            }
            if (ColonyCouncilMod.Settings.offices) m.marshal = CouncilGameComponent.Instance?.offices?.HolderOf(Interplay.Marshal);
            return m;
        }

        // ---------------- Left: the map and the facts ----------------

        private static float DrawLeft(Model m, float x, float y, float w)
        {
            var s = m.s;
            // The map, as big as the column is wide.
            float mapH = Mathf.Min(w, 330f) - 16f;
            float boxH = mapH + 46f;
            var box = new Rect(x, y, w, boxH);
            UiKit.Card(box, null, false, false);
            var area = new Rect(x + 8f, y + 8f, w - 16f, mapH);
            DrawMiniMap(s, area);
            Text.Font = GameFont.Tiny;
            float lx = x + 10f;
            lx = Legend(lx, y + mapH + 14f, new Color(0.95f, 0.3f, 0.25f), "CC_Def_Legend_Ways".Translate());
            lx = Legend(lx, y + mapH + 14f, new Color(1f, 0.9f, 0.3f), "CC_Def_Legend_Gaps".Translate());
            lx = Legend(lx, y + mapH + 14f, new Color(0.45f, 0.75f, 1f), "CC_Def_Legend_Gates".Translate());
            Legend(lx, y + mapH + 14f, new Color(0.55f, 0.8f, 0.45f), "CC_Def_Legend_Home".Translate());
            Text.Font = GameFont.Small;
            y += boxH + 8f;
            y = Facts(x, y, w, "CC_Def_Card_From".Translate(), Icons.Raid ?? Icons.CatDefense, m.from) + 8f;
            y = Facts(x, y, w, "CC_Def_Card_Face".Translate(), Icons.Warning, m.face) + 8f;
            y = Facts(x, y, w, "CC_Def_Card_Fight".Translate(), Icons.People, m.fight) + 8f;
            y = Facts(x, y, w, "CC_Def_Card_Stand".Translate(), Icons.CatDefense, m.stand) + 8f;
            y = Facts(x, y, w, "CC_Def_Card_How".Translate(), Icons.Almanac, m.how);
            var alm = new Rect(x, y + 4f, w, 26f);
            var page = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail("Rimfolk_AlmDef_Overview");
            if (page != null && Widgets.ButtonText(alm, "CC_Def_ReadAlmanac".Translate())) MainTabWindow_Council.OpenAlmanac(page);
            return y + 34f;
        }

        private static float Legend(float x, float y, Color c, string text)
        {
            Widgets.DrawBoxSolid(new Rect(x, y + 5f, 10f, 4f), c);
            float tw = Text.CalcSize(text).x;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 14f, y - 2f, tw + 4f, 18f), text);
            GUI.color = Color.white;
            return x + 14f + tw + 12f;
        }

        private static float Measure(string text, float w, GameFont font)
        {
            var key = (text, (int)w, font);
            if (heights.TryGetValue(key, out float h)) return h;
            var old = Text.Font;
            Text.Font = font;
            h = Text.CalcHeight(text, w);
            Text.Font = old;
            if (heights.Count > 400) heights.Clear();
            heights[key] = h;
            return h;
        }

        /// <summary>A small titled card of lines.</summary>
        private static float Facts(float x, float y, float w, string title, Texture2D icon, List<string> lines)
        {
            float textW = w - 26f;
            float h = 30f;
            foreach (var l in lines) h += Measure(l, textW, GameFont.Tiny) + 3f;
            h += 6f;
            var r = new Rect(x, y, w, h);
            UiKit.Card(r, null, false, false);
            UiKit.Icon(new Rect(x + 8f, y + 6f, 16f, 16f), icon, UiKit.HeaderColor);
            UiKit.LabelFit(new Rect(x + 30f, y + 3f, w - 38f, 22f), title, UiKit.HeaderColor);
            float ly = y + 28f;
            Text.Font = GameFont.Tiny;
            foreach (var l in lines)
            {
                float lh = Measure(l, textW, GameFont.Tiny);
                Widgets.DrawBoxSolid(new Rect(x + 9f, ly + 6f, 3f, 3f), UiKit.HeaderColor);
                Widgets.Label(new Rect(x + 17f, ly, textW, lh), l);
                ly += lh + 3f;
            }
            Text.Font = GameFont.Small;
            return y + h;
        }

        // ---------------- The little map ----------------

        private static readonly Color32 CRock = new Color32(92, 86, 78, 255);
        private static readonly Color32 COpen = new Color32(46, 52, 40, 255);
        private static readonly Color32 CHome = new Color32(70, 98, 58, 255);
        private static readonly Color32 CWater = new Color32(40, 76, 120, 255);
        private static readonly Color32 CWall = new Color32(200, 200, 200, 255);
        private static readonly Color32 CDoor = new Color32(115, 190, 255, 255);
        private static readonly Color32 COther = new Color32(120, 110, 100, 255);
        private static readonly Color32 CBridge = new Color32(150, 125, 85, 255);
        private static readonly Color32 CFog = new Color32(18, 18, 18, 255);
        private static readonly Color32 CWay = new Color32(240, 75, 60, 255);
        private static readonly Color32 CGap = new Color32(255, 230, 75, 255);
        private static readonly Color32 CCore = new Color32(225, 225, 190, 255);

        /// <summary>The map in a few colors, drawn once per survey into a texture: ground, rock, water, walls, home, the ways in and the gaps.</summary>
        private static void DrawMiniMap(DefenseSurvey s, Rect area)
        {
            if (miniFor != s || mini == null)
            {
                if (mini != null) UnityEngine.Object.Destroy(mini);
                mini = MakeMini(s);
                miniFor = s;
            }
            if (mini == null) return;
            float scale = Mathf.Min(area.width / s.grid.w, area.height / s.grid.h);
            var r = new Rect(area.x + (area.width - s.grid.w * scale) / 2f, area.y + (area.height - s.grid.h * scale) / 2f, s.grid.w * scale, s.grid.h * scale);
            GUI.DrawTexture(r, mini);
            Widgets.DrawBox(r);
            if (!Mouse.IsOver(r)) return;
            var mp = Event.current.mousePosition;
            int cx = Mathf.Clamp((int)((mp.x - r.x) / scale), 0, s.grid.w - 1);
            int cz = Mathf.Clamp((int)((r.yMax - mp.y) / scale), 0, s.grid.h - 1);
            TooltipHandler.TipRegion(r, "CC_Def_MapTip".Translate());
            if (Widgets.ButtonInvisible(r))
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJump(new IntVec3(cx, 0, cz), s.map);
            }
        }

        private static Texture2D MakeMini(DefenseSurvey s)
        {
            var g = s.grid;
            var r = s.result;
            if (g == null) return null;
            var px = new Color32[g.w * g.h];
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c;
                switch (g.kind[i])
                {
                    case DefenseMath.Rock: c = CRock; break;
                    case DefenseMath.Water: c = CWater; break;
                    case DefenseMath.Wall: c = CWall; break;
                    case DefenseMath.Door: c = CDoor; break;
                    case DefenseMath.Other: c = COther; break;
                    case DefenseMath.Bridge: c = CBridge; break;
                    case DefenseMath.Unknown: c = CFog; break;
                    default: c = g.core[i] ? CCore : g.home[i] ? CHome : COpen; break;
                }
                if (r?.flow != null && r.flow[i] > 0 && g.kind[i] != DefenseMath.Door)
                {
                    // Ways in: redder where more of them pass.
                    float t = Mathf.Clamp01(0.45f + r.flow[i] / (float)Mathf.Max(1, r.walks) * 3f);
                    c = Color32.Lerp(c, CWay, t);
                }
                px[i] = c;
            }
            foreach (var info in s.chokes)
                foreach (int i in info.c.span)
                    px[i] = info.gated ? CDoor : CGap;
            var tex = new Texture2D(g.w, g.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---------------- Right: the advice and the plans ----------------

        private static float DrawRight(Model m, float x, float y, float w)
        {
            var s = m.s;
            y += UiKit.SectionTitle(x, y, w, "CC_Def_Advice".Translate(), Icons.Priorities,
                null, m.open > 0 ? "CC_Def_AdviceOpen".Translate(m.open).Resolve() : "CC_Def_AdviceNone".Translate().Resolve());
            if (m.marshal != null && m.advice.Any(a => !a.done && !a.standing))
            {
                var mr = new Rect(x, y, w, 30f);
                UiKit.Portrait(new Rect(x + 2f, y + 2f, 26f, 26f), m.marshal, UiKit.Gold);
                UiKit.LabelFit(new Rect(x + 34f, y + 4f, w - 36f, 22f), "CC_Def_Marshal".Translate(m.marshal.Named("PAWN")).Resolve(), UiKit.Quote, GameFont.Tiny);
                y += mr.height + 4f;
            }
            foreach (var a in m.advice) y = AdviceCard(a, s, x, y, w) + 6f;
            y += 8f;
            y += UiKit.SectionTitle(x, y, w, "CC_Def_Plans".Translate(), Icons.Rooms, null, "CC_Def_PlansSub".Translate().Resolve());
            foreach (var (t, line, reason) in m.plans) y = PlanCard(t, line, reason, s, x, y, w) + 6f;
            return y;
        }

        private static Color LevelColor(AdviceLevel l)
        {
            switch (l)
            {
                case AdviceLevel.Critical: return CriticalColor;
                case AdviceLevel.High: return HighColor;
                case AdviceLevel.Medium: return MediumColor;
                case AdviceLevel.Low: return LowColor;
                default: return IdeaColor;
            }
        }

        private static float AdviceCard(DefenseAdviceItem a, DefenseSurvey s, float x, float y, float w)
        {
            float textX = x + 100f;
            float textW = Mathf.Max(80f, w - 100f - ButtonsW - 8f);
            float titleH = Measure(a.title ?? "", textW, GameFont.Small);
            float whyH = Measure(a.why ?? "", textW, GameFont.Tiny);
            int buttons = (a.template != null && !a.done ? 1 : 0) + (a.look.IsValid ? 1 : 0) + (a.article != null ? 1 : 0);
            float h = Mathf.Max(56f, titleH + whyH + 16f, buttons * 28f + 8f);
            var color = a.done ? UiKit.Good : LevelColor(a.Level);
            var r = new Rect(x, y, w, h);
            UiKit.Card(r, UiKit.WithAlpha(color, a.done ? 0.35f : 0.7f), false, false);
            if (a.done)
            {
                UiKit.Pill(x + 10f, y + 10f, "CC_Def_Done_Pill".Translate(), UiKit.Good, null, null, 84f);
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
            }
            else UiKit.Pill(x + 10f, y + 10f, (a.standing ? "CC_Def_Standing_Pill" : "CC_Level_" + a.Level).Translate(), color, a.Level >= AdviceLevel.High ? Icons.Warning : null, null, 84f);
            GUI.color = a.done ? UiKit.Faint : color;
            Widgets.Label(new Rect(textX, y + 6f, textW, titleH), a.title);
            GUI.color = a.done ? UiKit.Dim : Color.white;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textX, y + 6f + titleH, textW, whyH), a.why);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            float bx = x + w - ButtonsW - 6f, by = y + 6f;
            if (a.template != null && !a.done)
            {
                string reason = a.template.UnavailableReason(s.map);
                var br = new Rect(bx, by, ButtonsW, 24f);
                if (Widgets.ButtonText(br, "CC_Def_PlanHere".Translate(), true, true, reason == null ? Color.white : UiKit.Faint))
                {
                    if (reason != null) Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                    else Designator_PlanDefense.Begin(a.template, true, a.choke);
                }
                TooltipHandler.TipRegion(br, reason ?? "CC_Def_PlanHere_Tip".Translate(a.template.LabelNow).Resolve());
                by += 28f;
            }
            if (a.look.IsValid)
            {
                if (Widgets.ButtonText(new Rect(bx, by, ButtonsW, 24f), "CC_Show".Translate()))
                {
                    Find.MainTabsRoot.EscapeCurrentTab(false);
                    DefenseOverlay.Show = true;
                    CameraJumper.TryJump(a.look, s.map);
                }
                by += 28f;
            }
            if (a.article != null && Widgets.ButtonText(new Rect(bx, by, ButtonsW, 24f), "CC_ReadMore".Translate())) MainTabWindow_Council.OpenAlmanac(a.article);
            return y + h;
        }

        private static float PlanCard(DefenseTemplateDef t, string line, string reason, DefenseSurvey s, float x, float y, float w)
        {
            const float icon = 44f;
            float textX = x + icon + 18f;
            float textW = Mathf.Max(80f, w - icon - 18f - ButtonsW - 8f);
            string title = t.LabelCapNow + "  (" + t.Width + "×" + t.Height + ")";
            string purpose = t.purpose ?? t.description ?? "";
            float titleH = Measure(title, textW, GameFont.Small);
            float purposeH = Measure(purpose, textW, GameFont.Tiny);
            float lineH = line.NullOrEmpty() ? 0f : Measure(line, textW, GameFont.Tiny);
            float h = Mathf.Max(icon + 12f, titleH + purposeH + lineH + 16f, 66f);
            var r = new Rect(x, y, w, h);
            UiKit.Card(r, reason != null ? UiKit.WithAlpha(HighColor, 0.5f) : (Color?)null, false, false);
            var ir = new Rect(x + 8f, y + 6f, icon, icon);
            Widgets.DrawBoxSolid(ir, new Color(0f, 0f, 0f, 0.25f));
            GUI.color = reason == null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (t.Icon != null) GUI.DrawTexture(ir.ContractedBy(4f), t.Icon);
            GUI.color = reason == null ? Color.white : UiKit.Faint;
            Widgets.Label(new Rect(textX, y + 6f, textW, titleH), title);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(textX, y + 6f + titleH, textW, purposeH), purpose);
            if (lineH > 0f)
            {
                GUI.color = reason != null ? HighColor : new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(textX, y + 6f + titleH + purposeH + 2f, textW, lineH), line);
            }
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            if (reason == null)
            {
                float bx = x + w - ButtonsW - 6f;
                if (Widgets.ButtonText(new Rect(bx, y + 6f, ButtonsW, 24f), "CC_Build_Plan".Translate())) Designator_PlanDefense.Begin(t, false);
                bool spot = s.chokes.Count > 0 || s.approach.IsValid || t.placeAt == "core";
                if (spot && Widgets.ButtonText(new Rect(bx, y + 34f, ButtonsW, 24f), "CC_Def_AtSpot".Translate())) Designator_PlanDefense.Begin(t, true);
            }
            return y + h;
        }
    }
}
