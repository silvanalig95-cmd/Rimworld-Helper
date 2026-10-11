using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The Social tab: circles, then every bond by kind with its strength and history.</summary>
    public static class SocialPanel
    {
        private static Vector2 scroll;
        private static float height = 600f;
        private static Pawn focus;

        private const float RowH = 26f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color CliqueColor = new Color(1f, 0.85f, 0.5f);

        private static readonly Dictionary<TieKind, Texture2D> Bars = new Dictionary<TieKind, Texture2D>();

        private static Texture2D Bar(TieKind k)
        {
            if (Bars.TryGetValue(k, out var tex)) return tex;
            Color c;
            switch (k)
            {
                case TieKind.Friends: c = new Color(0.35f, 0.65f, 0.4f); break;
                case TieKind.Rivals: c = new Color(0.7f, 0.33f, 0.28f); break;
                case TieKind.Comrades: c = new Color(0.4f, 0.55f, 0.75f); break;
                default: c = new Color(0.8f, 0.68f, 0.3f); break;
            }
            tex = SolidColorMaterials.NewSolidColorTexture(c);
            Bars[k] = tex;
            return tex;
        }

        private static bool Shown(Pawn p) => p != null && !p.Dead && p.IsColonist;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Tab_Social".Translate());
            Text.Font = GameFont.Small;
            var colonists = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && p.RaceProps.Humanlike).OrderBy(p => p.LabelShort).ToList();
            if (focus != null && !colonists.Contains(focus)) focus = null;
            var filterRect = new Rect(rect.xMax - 260f, rect.y, 260f, 28f);
            if (Widgets.ButtonText(filterRect, "CC_Social_Filter".Translate() + ": " + (focus?.LabelShort ?? "CC_Social_Everyone".Translate().ToString())))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_Social_Everyone".Translate(), () => focus = null) };
                options.AddRange(colonists.Select(p => new FloatMenuOption(p.LabelShort, () => focus = p)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Prefs.DevMode && Find.CurrentMap != null && Widgets.ButtonText(new Rect(filterRect.x - 330f, rect.y, 130f, 28f), "Dev: dream"))
            {
                var dreamer = focus ?? Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby()).RandomElementWithFallback();
                if (dreamer == null || !comp.dreams.TryDream(comp, dreamer, false)) Messages.Message("No dream fits right now.", MessageTypeDefOf.RejectInput, false);
            }
            if (Prefs.DevMode && Find.CurrentMap != null && Widgets.ButtonText(new Rect(filterRect.x - 190f, rect.y, 180f, 28f), "Dev: social event"))
            {
                var pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();
                if (!SocialEvents.TryOne(comp, Find.CurrentMap, pawns)) Messages.Message("No social event fits right now.", MessageTypeDefOf.RejectInput, false);
            }
            float y = rect.y + 34f;
            GUI.color = Faint;
            float introH = Text.CalcHeight("CC_Social_Intro".Translate(), rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, introH), "CC_Social_Intro".Translate());
            GUI.color = Color.white;
            y += introH + 8f;

            var web = comp.social;
            var ties = !ColonyCouncilMod.Settings.socialWeb ? new List<SocialTie>() : web.ties.Where(t => Shown(t.a) && Shown(t.b) && t.announced && (focus == null || t.Involves(focus))).ToList();

            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float cy = 0f;
            if (ties.Count == 0 && ColonyCouncilMod.Settings.socialWeb)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, cy, view.width, 30f), "CC_Social_None".Translate());
                GUI.color = Color.white;
                cy += 30f;
            }

            if (ColonyCouncilMod.Settings.aspirations) cy += DrawGoals(comp, colonists, view.width, cy);

            var cliques = ColonyCouncilMod.Settings.socialWeb ? web.Cliques.Where(c => focus == null || c.members.Contains(focus)).ToList() : new List<Clique>();
            if (cliques.Count > 0)
            {
                cy += Header(view.width, cy, "CC_Social_Cliques".Translate());
                foreach (var c in cliques)
                {
                    string members = string.Join(", ", c.members.Where(Shown).Select(p => p.LabelShort));
                    float h = Mathf.Max(RowH, Text.CalcHeight(members, view.width - 230f));
                    var r = new Rect(0f, cy, view.width, h);
                    Widgets.DrawHighlightIfMouseover(r);
                    GUI.color = CliqueColor;
                    Widgets.Label(new Rect(6f, cy + 3f, 220f, RowH), c.name);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(230f, cy + 3f, view.width - 230f, h), members);
                    TooltipHandler.TipRegion(r, "CC_Social_CliqueTip".Translate());
                    cy += h + 2f;
                }
                cy += 10f;
            }

            foreach (var kind in new[] { TieKind.Friends, TieKind.Rivals, TieKind.Comrades, TieKind.Mentor })
            {
                var list = ties.Where(t => t.kind == kind).OrderByDescending(t => t.strength).ToList();
                if (list.Count == 0) continue;
                cy += Header(view.width, cy, ("CC_TieKind_" + kind).Translate() + " (" + list.Count + ")");
                foreach (var t in list)
                {
                    DrawTie(new Rect(0f, cy, view.width, RowH), t);
                    cy += RowH + 2f;
                }
                cy += 10f;
            }
            if (ColonyCouncilMod.Settings.vendettas) cy += DrawVendettas(comp, view.width, cy);
            if (ColonyCouncilMod.Settings.secrets) cy += DrawSecrets(comp, view.width, cy);
            if (ColonyCouncilMod.Settings.dreams) cy += DrawDreams(comp, view.width, cy);
            height = Mathf.Max(cy, 100f);
            Widgets.EndScrollView();
        }

        private static readonly Texture2D GoalBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.6f, 0.85f));

        private static float DrawGoals(CouncilGameComponent comp, List<Pawn> colonists, float width, float y)
        {
            var goals = colonists.Where(p => focus == null || p == focus).Select(p => comp.aspirations.Active(p)).Where(a => a != null).ToList();
            var fulfilled = comp.aspirations.all.Where(a => a.done && a.pawn != null && (focus == null || a.pawn == focus)).OrderByDescending(a => a.doneTick).Take(4).ToList();
            if (goals.Count == 0 && fulfilled.Count == 0) return 0f;
            float start = y;
            y += Header(width, y, "CC_Social_Goals".Translate());
            foreach (var a in goals.OrderByDescending(a => a.Progress))
            {
                var r = new Rect(0f, y, width, RowH);
                Widgets.DrawHighlightIfMouseover(r);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, 300f, RowH), (a.pawn.LabelShort + ": " + a.Goal).Truncate(296f));
                var bar = new Rect(310f, y + 7f, 160f, RowH - 14f);
                Widgets.FillableBar(bar, a.Progress, GoalBar, BaseContent.ClearTex, false);
                Widgets.DrawBox(bar);
                GUI.color = Faint;
                string progress = SafeProgress(a);
                Widgets.Label(new Rect(478f, y, width - 478f, RowH), progress.Truncate(width - 482f));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(r, () => "CC_Social_GoalTip".Translate(a.pawn.LabelShort, a.Goal, "CC_DayN".Translate(a.startedTick / GenDate.TicksPerDay + 1), progress).Resolve()
                                                  + (a.SparkText.NullOrEmpty() ? "" : "\n" + a.SparkText.CapitalizeFirst()), a.GetHashCode());
                if (Widgets.ButtonInvisible(r) && a.pawn.Spawned) CameraJumper.TryJumpAndSelect(a.pawn);
                y += RowH + 2f;
            }
            foreach (var a in fulfilled)
            {
                GUI.color = CliqueColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, width - 6f, RowH), ("CC_Social_GoalDone".Translate(a.pawn.LabelShort, a.Goal, "CC_DayN".Translate(a.doneTick / GenDate.TicksPerDay + 1)).Resolve()).Truncate(width - 10f));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                y += RowH + 2f;
            }
            return y - start + 10f;
        }

        private static string SafeProgress(Aspiration a)
        {
            try
            {
                return a.def.Worker.ProgressText(a);
            }
            catch
            {
                return a.Progress.ToStringPercent();
            }
        }

        private static float DrawVendettas(CouncilGameComponent comp, float width, float y)
        {
            var list = comp.vendettas.vendettas.Where(v => v.avenger != null && !v.avenger.Dead && v.avenger.IsColonist && v.faction != null && (focus == null || v.avenger == focus))
                .OrderBy(v => v.settled).ThenByDescending(v => v.tick).Take(10).ToList();
            if (list.Count == 0) return 0f;
            float start = y;
            y += Header(width, y, "CC_Social_Vendettas".Translate());
            foreach (var v in list)
            {
                string line = (v.settled ? "CC_Social_VendettaSettled" : v.kidnapped ? "CC_Social_VendettaTaken" : "CC_Social_Vendetta")
                    .Translate(v.avenger.LabelShort, v.faction.Name, v.victim, v.kills, Vendetta.KillsToSettle).Resolve();
                GUI.color = v.settled ? Faint : new Color(0.9f, 0.55f, 0.45f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, width - 6f, RowH), line.Truncate(width - 10f));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                y += RowH + 2f;
            }
            return y - start + 10f;
        }

        /// <summary>Only what the colony could know: who has confided in whom (not what), and secrets that are out.</summary>
        private static float DrawSecrets(CouncilGameComponent comp, float width, float y)
        {
            var list = comp.secrets.secrets.Where(s => s.owner != null && !s.owner.Dead && s.owner.IsColonist && (focus == null || s.owner == focus)
                                                       && (s.revealed || s.knowers.Count > 0 || Prefs.DevMode)).ToList();
            if (list.Count == 0) return 0f;
            float start = y;
            y += Header(width, y, "CC_Social_Secrets".Translate());
            foreach (var s in list.OrderBy(s => s.revealed))
            {
                string line;
                if (s.revealed) line = "CC_Social_SecretOut".Translate(s.owner.LabelShort, s.def.label).Resolve();
                else if (s.knowers.Count > 0) line = "CC_Social_SecretKnown".Translate(s.owner.LabelShort, TextUtil.JoinAnd(s.knowers.Select(k => k.LabelShort).ToList())).Resolve();
                else line = "(dev) " + s.owner.LabelShort + ": " + s.def.label;
                var r = new Rect(0f, y, width, RowH);
                Widgets.DrawHighlightIfMouseover(r);
                GUI.color = s.revealed ? (s.def.good ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.6f, 0.5f)) : Faint;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, width - 6f, RowH), line.Truncate(width - 10f));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                var secret = s;
                if (s.revealed) TooltipHandler.TipRegion(r, () => secret.Truth, s.GetHashCode());
                else if (Prefs.DevMode) TooltipHandler.TipRegion(r, () => "(dev) " + secret.Truth, s.GetHashCode());
                y += RowH + 2f;
            }
            return y - start + 10f;
        }

        private static float DrawDreams(CouncilGameComponent comp, float width, float y)
        {
            float start = y;
            var dreams = comp.dreams.recent.AsEnumerable().Reverse().Where(d => focus == null || d.pawn == focus).Take(10).ToList();
            y += Header(width, y, "CC_Social_Dreams".Translate());
            if (dreams.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(6f, y, width, 24f), "CC_Social_DreamsNone".Translate());
                GUI.color = Color.white;
                return y + 26f - start;
            }
            foreach (var d in dreams)
            {
                var r = new Rect(0f, y, width, RowH);
                Widgets.DrawHighlightIfMouseover(r);
                string who = (d.pawn?.LabelShort ?? d.name) + " (" + ("CC_DreamKind_" + d.kind).Translate() + ", " + "CC_DayN".Translate(d.tick / GenDate.TicksPerDay + 1) + ")";
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = d.cameTrue ? CliqueColor : d.kind == DreamKind.Nightmare ? new Color(0.85f, 0.6f, 0.55f) : Color.white;
                Widgets.Label(new Rect(6f, y, 230f, RowH), who.Truncate(226f));
                GUI.color = Faint;
                string text = d.text + (d.cameTrue ? "  (" + "CC_Social_DreamCameTrue".Translate() + ")" : "");
                Widgets.Label(new Rect(240f, y, width - 240f, RowH), text.Truncate(width - 244f));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(r, d.text);
                if (Widgets.ButtonInvisible(r) && d.pawn != null && d.pawn.Spawned) CameraJumper.TryJumpAndSelect(d.pawn);
                y += RowH + 2f;
            }
            return y - start + 10f;
        }

        private static float Header(float width, float y, string text)
        {
            GUI.color = Faint;
            Widgets.Label(new Rect(0f, y, width, 24f), text);
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, y + 22f, width);
            return 26f;
        }

        private static void DrawTie(Rect r, SocialTie t)
        {
            Widgets.DrawHighlightIfMouseover(r);
            string names = t.kind == TieKind.Mentor && t.skill != null
                ? t.a.LabelShort + " → " + t.b.LabelShort + " (" + t.skill.label + ")"
                : t.a.LabelShort + " & " + t.b.LabelShort;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x + 6f, r.y, 300f, r.height), names.Truncate(296f));
            var barRect = new Rect(r.x + 310f, r.y + 7f, 160f, r.height - 14f);
            Widgets.FillableBar(barRect, Mathf.Clamp01(t.strength / 100f), Bar(t.kind), BaseContent.ClearTex, false);
            Widgets.DrawBox(barRect);
            string stage = (t.strength >= 70f ? "CC_Social_Deep" : t.strength >= 45f ? "CC_Social_Solid" : "CC_Social_Weak").Translate();
            GUI.color = Faint;
            Widgets.Label(new Rect(r.x + 478f, r.y, 70f, r.height), stage);
            string last = t.history.Count > 0 ? t.history[t.history.Count - 1] : "";
            float noteX = r.x + 552f;
            if (r.width > noteX + 40f) Widgets.Label(new Rect(noteX, r.y, r.width - noteX, r.height), last.Truncate(r.width - noteX - 4f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
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
