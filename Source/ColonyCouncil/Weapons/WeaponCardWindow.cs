using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own weapon card ("Rimfolk's weapon card", key "WeaponCard" in Rimfolk's frame): the rating's badge, the tags
    /// as coloured chips with their marks (the why on each), the line in plain words, the sub-ratings, a small chart of damage
    /// per second by distance against no armour, flak, marine armour and mechanoids (a melee weapon's bars by armour), the
    /// situations, the effects counted or named, the key numbers; then for one colonist: their own rating and why it
    /// differs, what stops them using it, RunAndGun's shooting on the move, Melee Animation's moves, Dual Wield's off hand,
    /// and how it compares with what they hold and carry (Simple Sidearms' sidearms and the sensible pair). Drawn with the
    /// kit, in Rimfolk's look or the game's. Opens from a button on the game's info card, a button on a weapon on the map,
    /// and a small button beside each weapon on the gear tab (the game's and Nice Inventory Tab's). One at a time.
    /// </summary>
    public class WeaponCardWindow : FramedWindow
    {
        public static bool On => RimfolkPatches.On && WeaponLive.On && (ColonyCouncilMod.Settings?.weaponCard ?? true);

        private Thing thing;
        private ThingDef def, stuff;
        private Pawn who;
        private static Pawn lastWho;
        private Vector2 scroll;
        private float viewH = 1400f;
        private WeaponReport chartFor;
        private List<float> chartD;
        private float[][] chartRanged;
        private float[] chartMelee;

        public override Vector2 InitialSize => new Vector2(580f, Mathf.Min(800f, UI.screenHeight - 120f));

        public WeaponCardWindow() : base("WeaponCard")
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
            soundAppear = SoundDefOf.TabOpen;
            soundClose = SoundDefOf.TabClose;
        }

        /// <summary>The card for a weapon in hand, for a colonist (the one who holds it, else the last chosen). One card at a time.</summary>
        public static void Open(Thing t, Pawn forWho)
        {
            if (t == null) return;
            var w = Find.WindowStack.WindowOfType<WeaponCardWindow>() ?? Added();
            w.thing = t;
            w.def = t.def;
            w.stuff = t.Stuff;
            w.Choose(forWho ?? WeaponInfoCard.Holder(t));
        }

        /// <summary>The card for a weapon def in a stuff (its default when none), normal quality.</summary>
        public static void Open(ThingDef d, ThingDef inStuff, Pawn forWho)
        {
            if (d == null) return;
            var w = Find.WindowStack.WindowOfType<WeaponCardWindow>() ?? Added();
            w.thing = null;
            w.def = d;
            w.stuff = inStuff ?? WeaponLive.DefaultStuff(d);
            w.Choose(forWho);
        }

        private static WeaponCardWindow Added()
        {
            var w = new WeaponCardWindow();
            Find.WindowStack.Add(w);
            return w;
        }

        private void Choose(Pawn p)
        {
            scroll = Vector2.zero;
            if (p != null && p.RaceProps.Humanlike) lastWho = p;
            who = p ?? lastWho;
        }

        private WeaponReport Report => thing != null ? WeaponLive.Of(thing) : WeaponLive.Of(def, stuff);

        protected override string FrameTitle =>
            thing != null ? thing.LabelCapNoCount : def != null ? GenLabel.ThingLabel(def, stuff).CapitalizeFirst() : "CC_Weapon_CardTitle".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.CatNumbers;

        private static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        protected override void DoFramedContents(Rect inRect)
        {
            if ((thing != null && thing.Destroyed) || def == null)
            {
                Close();
                return;
            }
            try
            {
                float top = TitleTop(inRect, FrameTitle);
                var r = Report;
                var outer = new Rect(inRect.x, top, inRect.width, inRect.yMax - top);
                if (r == null)
                {
                    UiKit.EmptyState(outer, Icons.CatNumbers, "CC_Weapon_NotRated".Translate());
                    return;
                }
                var inner = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(viewH, outer.height - 4f));
                Widgets.BeginScrollView(outer, ref scroll, inner);
                float y = Body(inner.width, r);
                Widgets.EndScrollView();
                viewH = y + 12f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Rimfolk's weapon card", e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------------------------------------------------------- the card

        private float Body(float w, WeaponReport r)
        {
            float y = 4f;
            y = Head(w, y, r);
            if (r.Tags.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Tags".Translate());
                y += WeaponUi.Chips(0f, y, w, r.Tags) + 10f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Sub".Translate());
            y = Subs(w, y, r) + 6f;
            y += UiKit.SectionTitle(0f, y, w, (r.Rating.Kind == "ranged" ? "CC_Weapon_Sec_ChartRanged" : "CC_Weapon_Sec_ChartMelee").Translate());
            y = Chart(w, y, r) + 8f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Situations".Translate());
            y = Situations(w, y, r) + 6f;
            if (r.Rating.Specials.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Effects".Translate());
                y = Effects(w, y, r) + 6f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Numbers".Translate());
            y = Numbers(w, y, r) + 8f;
            y = ForColonist(w, y, r);
            return y;
        }

        private float Head(float w, float y, WeaponReport r)
        {
            var g = r.Rating;
            var badge = new Rect(0f, y, 66f, 46f);
            WeaponUi.Badge(badge, g.Overall, () => "CC_Weapon_Info_RatingExpl".Translate(), 81726, GameFont.Medium);
            float x = badge.xMax + 10f;
            string band = WeaponWords.Quality(g.Overall).CapitalizeFirst();
            UiKit.LabelFit(new Rect(x, y, w - x, 24f), band + " · " + WeaponInfoCard.RoleWords(g.Role), WeaponUi.RatingColour(g.Overall));
            string quality = thing != null && thing.TryGetQuality(out var q) ? q.GetLabel() : QualityCategory.Normal.GetLabel();
            string sub = "CC_Weapon_Card_RatedFor".Translate((g.Kind == "ranged" ? SkillDefOf.Shooting.label : SkillDefOf.Melee.label).Named("SKILL"),
                quality.Named("QUALITY"), (r.StuffLabel ?? "").Named("STUFF")).Resolve();
            if (r.Rough) sub += " · " + "CC_Weapon_Rough".Translate();
            UiKit.LabelFit(new Rect(x, y + 24f, w - x, 20f), sub, UiKit.Faint, GameFont.Tiny);
            if (r.Rough) TooltipHandler.TipRegion(new Rect(x, y + 24f, w - x, 20f), "CC_Weapon_Info_Rough".Translate() + "\n" + string.Join("\n", r.Unsure.Select(u => "- " + u)));
            y += 52f;
            if (!r.Plain.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, r.Plain, UiKit.Ink) + 8f;
            return y;
        }

        private float Subs(float w, float y, WeaponReport r)
        {
            var keys = WeaponForMath.SubKeys(r.Rating).Where(k => r.Rating.Sub.ContainsKey(k)).ToList();
            float colW = (w - 12f) / 2f;
            for (int i = 0; i < keys.Count; i++)
            {
                float v = r.Rating.Sub[keys[i]];
                var cell = new Rect(i % 2 == 0 ? 0f : colW + 12f, y + (i / 2) * (UiKit.MeterHeight + 4f), colW, UiKit.MeterHeight);
                string k = keys[i].CapitalizeFirst();
                UiKit.Meter(cell, v / 10f, WeaponUi.RatingColour(v), ("CC_Weapon_Sub_" + k).Translate(), F1(v), null, ("CC_Weapon_SubExpl_" + k).Translate());
            }
            return y + ((keys.Count + 1) / 2) * (UiKit.MeterHeight + 4f);
        }

        // ---------------- The chart ----------------

        private static readonly uint[] SeriesColours = { SkinMath.Done, SkinMath.Soon, SkinMath.Foe, SkinMath.Ally };

        private void ChartData(WeaponReport r)
        {
            if (chartFor == r) return;
            chartFor = r;
            var f = WeaponRateMath.Reference;
            if (r.Rating.Kind == "ranged")
            {
                chartD = WeaponForMath.ChartDistances(r.Ranged);
                chartRanged = WeaponForMath.RangedChart(r.Ranged, f, chartD);
                chartMelee = null;
            }
            else
            {
                chartMelee = WeaponForMath.MeleeChart(r.Melee, f);
                chartRanged = null;
            }
        }

        private float Chart(float w, float y, WeaponReport r)
        {
            ChartData(r);
            string[] names = WeaponForMath.ChartEnemies.Select(k => ("CC_Weapon_Enemy_" + k.CapitalizeFirst()).Translate().Resolve()).ToArray();
            if (chartMelee != null)
            {
                float top = WeaponForMath.NiceTop(chartMelee.Max());
                for (int i = 0; i < 4; i++)
                {
                    var row = new Rect(0f, y, w, 20f);
                    UiKit.LabelFit(new Rect(0f, y, 130f, 20f), names[i], UiKit.Faint, GameFont.Tiny);
                    var bar = new Rect(134f, y + 5f, w - 134f - 64f, 10f);
                    UiKit.Bar(bar, chartMelee[i] / top, WeaponUi.Colour(SeriesColours[i]));
                    UiKit.LabelFit(new Rect(bar.xMax + 6f, y, 58f, 20f), "CC_Weapon_PerSecond".Translate(F1(chartMelee[i]).Named("DPS")).Resolve(), UiKit.Ink, GameFont.Tiny);
                    TooltipHandler.TipRegion(row, "CC_Weapon_ChartMeleeTip".Translate(names[i].Named("ENEMY"), F1(chartMelee[i]).Named("DPS")).Resolve());
                    y += 22f;
                }
                return y + UiKit.Paragraph(0f, y, w, "CC_Weapon_ChartNote".Translate(SkillDefOf.Melee.label.Named("SKILL")).Resolve(), UiKit.Faint, GameFont.Tiny);
            }
            if (chartRanged == null || chartD == null || chartD.Count == 0) return y;
            float max = 0f;
            foreach (var s in chartRanged) foreach (var v in s) max = Mathf.Max(max, v);
            float yTop = WeaponForMath.NiceTop(max);
            var plot = new Rect(40f, y + 6f, w - 50f, 120f);
            float maxD = chartD[chartD.Count - 1];
            float X(float d) => plot.x + plot.width * Mathf.Clamp01(d / Mathf.Max(maxD, 1f));
            float Y(float v) => plot.yMax - plot.height * Mathf.Clamp01(v / yTop);
            // The grid: the floor, half and the top, with their values.
            foreach (var frac in new[] { 0f, 0.5f, 1f })
            {
                float gy = plot.yMax - plot.height * frac;
                Widgets.DrawBoxSolid(new Rect(plot.x, gy, plot.width, 1f), UiKit.WithAlpha(UiKit.DividerColor, frac == 0f ? 1f : 0.6f));
                UiKit.LabelFit(new Rect(0f, gy - 9f, 36f, 18f), F1(yTop * frac), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            }
            // The distances the rating weighs, marked under the floor.
            foreach (var d in WeaponRateMath.Distances)
            {
                if (d > maxD + 0.01f) continue;
                float gx = X(d);
                Widgets.DrawBoxSolid(new Rect(gx, plot.y, 1f, plot.height), UiKit.WithAlpha(UiKit.DividerColor, 0.35f));
                UiKit.LabelFit(new Rect(gx - 20f, plot.yMax + 2f, 40f, 16f), d.ToString("0", CultureInfo.InvariantCulture), UiKit.Faint, GameFont.Tiny, TextAnchor.UpperCenter);
            }
            if (Event.current.type == EventType.Repaint)
                for (int s = 0; s < 4; s++)
                {
                    var c = WeaponUi.Colour(SeriesColours[s]);
                    for (int i = 1; i < chartD.Count; i++)
                        Widgets.DrawLine(new Vector2(X(chartD[i - 1]), Y(chartRanged[s][i - 1])), new Vector2(X(chartD[i]), Y(chartRanged[s][i])), c, 2f);
                }
            // The value under the mouse.
            if (Mouse.IsOver(plot))
            {
                float mx = Event.current.mousePosition.x;
                int near = 0;
                for (int i = 1; i < chartD.Count; i++)
                    if (Mathf.Abs(X(chartD[i]) - mx) < Mathf.Abs(X(chartD[near]) - mx)) near = i;
                Widgets.DrawBoxSolid(new Rect(X(chartD[near]), plot.y, 1f, plot.height), UiKit.WithAlpha(UiKit.Ink, 0.5f));
                TooltipHandler.TipRegion(plot, "CC_Weapon_ChartTip".Translate(chartD[near].ToString("0", CultureInfo.InvariantCulture).Named("CELLS")).Resolve() + "\n" +
                    string.Join("\n", Enumerable.Range(0, 4).Select(s => names[s] + ": " + "CC_Weapon_PerSecond".Translate(F1(chartRanged[s][near]).Named("DPS")).Resolve())));
            }
            y = plot.yMax + 20f;
            // The legend: each line's colour beside its name.
            float lx = 0f;
            for (int s = 0; s < 4; s++)
            {
                Text.Font = GameFont.Tiny;
                float lw = Text.CalcSize(names[s]).x + 22f;
                if (lx > 0f && lx + lw > w)
                {
                    lx = 0f;
                    y += 18f;
                }
                Widgets.DrawBoxSolid(new Rect(lx, y + 7f, 12f, 3f), WeaponUi.Colour(SeriesColours[s]));
                UiKit.LabelFit(new Rect(lx + 16f, y, lw - 16f, 18f), names[s], UiKit.Faint, GameFont.Tiny);
                lx += lw + 8f;
            }
            y += 20f;
            return y + UiKit.Paragraph(0f, y, w, "CC_Weapon_ChartNoteRanged".Translate(SkillDefOf.Shooting.label.Named("SKILL")).Resolve(), UiKit.Faint, GameFont.Tiny);
        }

        // ---------------- Situations, effects, numbers ----------------

        private float Situations(float w, float y, WeaponReport r)
        {
            var g = r.Rating;
            foreach (var kv in g.Situations.OrderByDescending(kv => kv.Value))
            {
                bool best = g.BestFor.Contains(kv.Key), weak = g.WeakWhen.Contains(kv.Key);
                var row = new Rect(0f, y, w, 20f);
                string name = ("CC_Weapon_Sit_" + kv.Key.Replace(" ", "")).Translate();
                UiKit.LabelFit(new Rect(0f, y, 170f, 20f), name, best ? UiKit.Good : weak ? UiKit.Bad : UiKit.Ink, GameFont.Small);
                var bar = new Rect(176f, y + 6f, w - 176f - 120f, 8f);
                UiKit.Bar(bar, kv.Value / 10f, WeaponUi.RatingColour(kv.Value));
                string note = best ? "CC_Weapon_BestFor".Translate().Resolve() : weak ? "CC_Weapon_WeakWhen".Translate().Resolve() : "";
                UiKit.LabelFit(new Rect(bar.xMax + 6f, y, 114f, 20f), F1(kv.Value) + (note.Length > 0 ? " · " + note : ""), best ? UiKit.Good : weak ? UiKit.Bad : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, ("CC_Weapon_SitExpl_" + kv.Key.Replace(" ", "")).Translate());
                y += 22f;
            }
            return y;
        }

        private float Effects(float w, float y, WeaponReport r)
        {
            foreach (var s in r.Rating.Specials)
            {
                var c = s.Sign > 0 ? UiKit.Good : s.Sign < 0 ? UiKit.Bad : UiKit.Faint;
                string mark = s.Sign > 0 ? "+" : s.Sign < 0 ? "-" : "·";
                UiKit.LabelFit(new Rect(0f, y, 14f, 20f), mark, c);
                string tail = s.Counted ? "CC_Weapon_Counted".Translate().Resolve() : "CC_Weapon_NamedOnly".Translate().Resolve();
                float h = UiKit.Paragraph(16f, y + 1f, w - 16f - 96f, s.Label.CapitalizeFirst(), UiKit.Ink, GameFont.Small);
                UiKit.LabelFit(new Rect(w - 92f, y, 92f, 20f), tail, s.Counted ? UiKit.Faint : UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
                y += Mathf.Max(20f, h) + 2f;
            }
            return y;
        }

        private float Numbers(float w, float y, WeaponReport r)
        {
            var rows = new List<(string label, string value)>();
            if (r.Rating.Kind == "ranged")
            {
                var g = r.Ranged;
                string armour = ("CC_Weapon_Armour_" + g.Armour).Translate();
                if (g.HarmsHealth && g.Damage > 0f) rows.Add(("CC_Weapon_Num_Damage".Translate(), F2(g.Damage) + " (" + armour + ")"));
                rows.Add(("CC_Weapon_Num_Pen".Translate(), g.ArmourPen.ToStringPercent()));
                rows.Add(("CC_Weapon_Num_Stop".Translate(), F2(g.StoppingPower)));
                rows.Add(("CC_Weapon_Num_Range".Translate(), (g.MinRange > 1.5f ? F1(g.MinRange) + " - " : "") + F1(g.Range)));
                rows.Add(("CC_Weapon_Num_Aim".Translate(), "CC_Weapon_Seconds".Translate(F2(g.Warmup).Named("S")).Resolve()));
                rows.Add(("CC_Weapon_Num_Cooldown".Translate(), "CC_Weapon_Seconds".Translate(F2(g.Cooldown).Named("S")).Resolve()));
                if (g.Burst > 1) rows.Add(("CC_Weapon_Num_Burst".Translate(), g.Burst + (g.ProjectilesPerShot > 1 ? " x" + g.ProjectilesPerShot : "")));
                float cycle = WeaponRateMath.CycleSeconds(g, WeaponRateMath.Reference);
                rows.Add(("CC_Weapon_Num_Cycle".Translate(), "CC_Weapon_Seconds".Translate(F2(cycle).Named("S")).Resolve()));
                rows.Add(("CC_Weapon_Num_Accuracy".Translate(), g.AccTouch.ToStringPercent() + " / " + g.AccShort.ToStringPercent() + " / " + g.AccMedium.ToStringPercent() + " / " + g.AccLong.ToStringPercent()));
                if (g.ExplosionRadius > 0.5f) rows.Add(("CC_Weapon_Num_Blast".Translate(), F1(g.ExplosionRadius)));
                if (g.ForcedMissRadius > 0.5f) rows.Add(("CC_Weapon_Num_Scatter".Translate(), F1(g.ForcedMissRadius)));
            }
            else
            {
                foreach (var (a, share) in WeaponRateMath.MeleeShares(r.Melee, WeaponRateMath.Reference))
                {
                    if (a.Natural && share <= 0f) continue;
                    var n = WeaponRateMath.MeleeNumbers(a, r.Melee, WeaponRateMath.Reference);
                    rows.Add(((a.Natural ? "CC_Weapon_Num_Fists".Translate(a.Label.Named("PART")).Resolve() : a.Label.CapitalizeFirst()),
                        "CC_Weapon_Num_Blow".Translate(F2(n.damage).Named("DAMAGE"), ("CC_Weapon_Armour_" + a.Armour).Translate().Named("KIND"),
                            F2(n.cooldown).Named("COOLDOWN"), n.pen.ToStringPercent().Named("PEN"), share.ToStringPercent().Named("SHARE")).Resolve()));
                }
            }
            rows.Add(("CC_Weapon_Num_Mass".Translate(), r.Mass.ToStringMass()));
            foreach (var (label, value) in rows)
            {
                UiKit.LabelFit(new Rect(0f, y, 150f, 20f), label, UiKit.Faint, GameFont.Small);
                float h = UiKit.Paragraph(156f, y + 1f, w - 156f, value, UiKit.Ink, GameFont.Small);
                y += Mathf.Max(20f, h) + 1f;
            }
            return y;
        }

        // ---------------- For a colonist ----------------

        private void Pick()
        {
            var opts = new List<FloatMenuOption>();
            var map = Find.CurrentMap;
            var pawns = map?.mapPawns?.FreeColonists?.Where(p => p.RaceProps.Humanlike).OrderBy(p => p.LabelShort).ToList() ?? new List<Pawn>();
            foreach (var p in pawns)
            {
                var pick = p;
                opts.Add(new FloatMenuOption(p.LabelShortCap, () => Choose(pick)));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Weapon_NoColonists".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        /// <summary>What the card says for one colonist, worked out once and kept two seconds: nothing is rated again per frame.</summary>
        private sealed class ColonistView
        {
            public WeaponReport report;
            public Pawn pawn;
            public float made;
            public string cant, why;
            public WeaponForColonist fc;
            public readonly List<(string text, Color color)> notes = new List<(string, Color)>();
            public readonly List<(Thing t, string role, float? rating, string note)> rows = new List<(Thing, string, float?, string)>();
            public readonly List<(string text, Color color, GameFont font)> after = new List<(string, Color, GameFont)>();
        }

        private ColonistView view;

        private float ForColonist(float w, float y, WeaponReport r)
        {
            if (who != null && (who.Destroyed || who.Dead)) who = null;
            string head = who != null ? "CC_Weapon_Sec_For".Translate(who.Named("PAWN")).Resolve() : "CC_Weapon_Sec_ForNone".Translate().Resolve();
            y += UiKit.SectionTitle(0f, y, w - 150f, head);
            if (UiKit.Button(new Rect(w - 144f, y - UiKit.SectionHeight + 1f, 144f, 24f), "CC_Weapon_ChooseColonist".Translate(), ButtonKind.Normal, true, "CC_Weapon_ChooseColonist_Tip".Translate()))
                Pick();
            if (who == null)
                return y + UiKit.Paragraph(0f, y, w, "CC_Weapon_NoneChosen".Translate(), UiKit.Faint) + 6f;
            var v = ViewFor(r);
            if (v.cant != null) y += UiKit.Paragraph(0f, y, w, v.cant, UiKit.Bad) + 4f;
            if (v.fc != null)
            {
                WeaponUi.Badge(new Rect(0f, y, 52f, 34f), v.fc.Rating, null, 0, GameFont.Small);
                UiKit.LabelFit(new Rect(60f, y, w - 60f, 18f), "CC_Weapon_ForLine".Translate(who.Named("PAWN"), F1(v.fc.Rating).Named("RATING"), F1(v.fc.Reference).Named("REFERENCE")).Resolve(), UiKit.Ink);
                UiKit.LabelFit(new Rect(60f, y + 17f, w - 60f, 18f), v.why, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(0f, y, w, 36f), "CC_Weapon_ForTip".Translate(who.Named("PAWN")).Resolve());
                y += 40f;
            }
            foreach (var (text, color) in v.notes) y += UiKit.Paragraph(0f, y, w, text, color, GameFont.Small) + 3f;
            y += 4f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Carries".Translate(who.Named("PAWN")).Resolve());
            if (v.rows.Count == 0)
                y += UiKit.Paragraph(0f, y, w, "CC_Weapon_CarriesNothing".Translate(who.Named("PAWN")).Resolve(), UiKit.Faint) + 4f;
            foreach (var (t, role, rating, note) in v.rows)
            {
                UiKit.LabelFit(new Rect(0f, y, 90f, 20f), role, UiKit.Faint, GameFont.Tiny);
                Widgets.ThingIcon(new Rect(92f, y, 20f, 20f), t);
                float bx = w - 40f;
                if (rating.HasValue) WeaponUi.Badge(new Rect(bx, y + 1f, 36f, 18f), rating.Value);
                UiKit.LabelFit(new Rect(116f, y, bx - 120f - (note.NullOrEmpty() ? 0f : 140f), 20f), t.LabelCapNoCount, UiKit.Ink);
                if (!note.NullOrEmpty()) UiKit.LabelFit(new Rect(bx - 144f, y, 140f, 20f), note, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                var row = new Rect(0f, y, w, 20f);
                if (t != thing && !t.Destroyed)
                {
                    if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    if (Widgets.ButtonInvisible(row)) Open(t, who);
                }
                y += 22f;
            }
            foreach (var (text, color, font) in v.after) y += UiKit.Paragraph(0f, y + 2f, w, text, color, font) + 4f;
            return y;
        }

        private ColonistView ViewFor(WeaponReport r)
        {
            if (view != null && view.report == r && view.pawn == who && Time.realtimeSinceStartup - view.made < 2f) return view;
            var v = new ColonistView { report = r, pawn = who, made = Time.realtimeSinceStartup };
            view = v;
            v.cant = WeaponLive.CantUse(who, thing, r);
            // Their own rating, even for a weapon coded to someone else (null only when they can't fight at all).
            v.fc = WeaponLive.ForColonist(who, r);
            if (v.fc != null)
            {
                var steps = WeaponForMath.Telling(v.fc);
                v.why = steps.Count == 0 ? "CC_Weapon_SameAsReference".Translate().Resolve()
                    : string.Join(", ", steps.Select(s => WeaponUi.StepWords(s.why).ToLowerInvariant() + ": " + WeaponForMath.Delta(s.delta)));
            }
            var facts = WeaponLive.Fighter(who);
            var fighter = WeaponForMath.Fighter(facts ?? new FighterFacts());
            if (facts != null && facts.Brawler && r.Rating.Kind == "ranged") v.notes.Add(("CC_Weapon_BrawlerGun".Translate(who.Named("PAWN")).Resolve(), UiKit.Warn));
            // A heavy weapon: what lets them carry it (Tough, an exoframe, a gene).
            string carrier = WeaponLive.Heavy(r) ? WeaponLive.HeavyCarriedBy(who, r) : null;
            if (carrier != null) v.notes.Add(("CC_Weapon_HeavyOk".Translate(who.Named("PAWN"), carrier.Named("WHY")).Resolve(), UiKit.Good));
            // RunAndGun: shooting on the move.
            var move = WeaponMods.RunAndGunFor(def);
            if (move.HasValue)
            {
                if (!move.Value.Allowed) v.notes.Add(("CC_Weapon_RunAndGunNo".Translate().Resolve(), UiKit.Faint));
                else
                {
                    var moving = WeaponForMath.RateFor(r, fighter, WeaponForMath.WithAccuracy(r.Ranged, WeaponForMath.MovingAccuracyFactor(move.Value.AccuracyPenalty)));
                    v.notes.Add(("CC_Weapon_RunAndGun".Translate(move.Value.AccuracyPenalty.Named("ACC"), F1(moving.Overall).Named("RATING"), move.Value.Slower.Named("SLOWER")).Resolve(), UiKit.Ink));
                }
            }
            // Melee Animation: what it adds for this weapon.
            var moves = WeaponMods.MeleeAnimationFor(who, def);
            if (moves.HasValue)
            {
                var m = moves.Value;
                if (!m.Animated) v.notes.Add(("CC_Weapon_MeleeAnimationNone".Translate().Resolve(), UiKit.Faint));
                else
                {
                    var parts = new List<string>();
                    if (m.ExecutionsOn && m.ExecutionMoves > 0)
                        parts.Add("CC_Weapon_MeleeAnimationExec".Translate(m.ExecutionMoves.Named("COUNT"), (who.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0).Named("LEVEL")).Resolve());
                    if (m.Duels) parts.Add("CC_Weapon_MeleeAnimationDuels".Translate().Resolve());
                    v.notes.Add(("CC_Weapon_MeleeAnimation".Translate((parts.Count > 0 ? "; " + string.Join("; ", parts) : "").Named("MORE")).Resolve(), UiKit.Ink));
                }
            }
            Carried(v, r, facts, fighter);
            return v;
        }

        /// <summary>What the colonist holds and carries, each rated for them; the sensible pair; Dual Wield's off hand.</summary>
        private void Carried(ColonistView v, WeaponReport r, FighterFacts facts, WeaponFighter fighter)
        {
            var primary = who.equipment?.Primary;
            var off = WeaponMods.OffHand(who);
            var carried = WeaponMods.Carried(who);
            float? RatingOf(Thing t, out WeaponReport rep)
            {
                rep = WeaponLive.Of(t);
                if (rep == null) return null;
                return WeaponLive.ForColonist(who, rep)?.Rating ?? rep.Rating.Overall;
            }
            var dual = WeaponMods.DualRules();
            var list = new List<CarriedWeapon>();
            WeaponRating mainInHand = null, offInHand = null;
            if (primary != null)
            {
                float? pr = RatingOf(primary, out var prep);
                string note = "";
                if (pr.HasValue && v.fc != null && primary != thing && prep.Rating.Kind == r.Rating.Kind)
                    note = "CC_Weapon_ThisOne".Translate(WeaponForMath.Delta(v.fc.Rating - pr.Value).Named("DELTA")).Resolve();
                else if (pr.HasValue && prep.Rating.Kind != r.Rating.Kind) note = "CC_Weapon_OtherKind".Translate().Resolve();
                v.rows.Add((primary, "CC_Weapon_InHand".Translate().Resolve(), pr, note));
                if (pr.HasValue)
                {
                    list.Add(new CarriedWeapon
                    {
                        Key = primary.ThingID, Label = primary.LabelCapNoCount, Ranged = prep.Rating.Kind == "ranged", Primary = true, Mass = prep.Mass,
                        OneUse = prep.Ranged?.OneUse ?? false, Rating = pr.Value
                    });
                    if (off != null && facts != null) mainInHand = WeaponForMath.RateInHand(prep, fighter, dual, false, Skill(prep));
                }
            }
            if (off != null)
            {
                var orep = WeaponLive.Of(off);
                if (orep != null && facts != null) offInHand = WeaponForMath.RateInHand(orep, fighter, dual, true, Skill(orep));
                v.rows.Add((off, "CC_Weapon_OffHand".Translate().Resolve(), offInHand?.Overall ?? orep?.Rating.Overall, "CC_Weapon_AsOffHand".Translate().Resolve()));
                var both = WeaponForMath.PairTogether(mainInHand, offInHand);
                if (both.HasValue)
                {
                    int level = Skill(orep);
                    v.after.Add(("CC_Weapon_DualPair".Translate(F1(both.Value).Named("RATING"), F1(mainInHand.Overall).Named("MAIN"), F1(offInHand.Overall).Named("OFF"),
                        level.Named("LEVEL"), Pct(WeaponForMath.DualCooldownFactor(dual, true, level) - 1f).Named("COOLDOWN"),
                        Pct(1f - WeaponForMath.DualAccuracyFactor(dual, true, level)).Named("ACC")).Resolve(), UiKit.Ink, GameFont.Small));
                }
            }
            foreach (var t in carried)
            {
                float? cr = RatingOf(t, out var crep);
                string note = WeaponMods.Remembered(who, t) ? "CC_Weapon_Remembered".Translate().Resolve() : "";
                if (crep != null && !WeaponForMath.Fits(new CarriedWeapon { Ranged = crep.Rating.Kind == "ranged", Mass = crep.Mass }))
                    note = "CC_Weapon_OverSidearmWeight".Translate().Resolve();
                v.rows.Add((t, "CC_Weapon_Sidearm".Translate().Resolve(), cr, note));
                if (cr.HasValue)
                    list.Add(new CarriedWeapon
                    {
                        Key = t.ThingID, Label = t.LabelCapNoCount, Ranged = crep.Rating.Kind == "ranged", Mass = crep.Mass, OneUse = crep.Ranged?.OneUse ?? false, Rating = cr.Value
                    });
            }
            if (carried.Count > 0 || WeaponMods.SidearmsLoaded)
            {
                var (bestR, bestM) = WeaponForMath.BestPair(list);
                if (bestR != null || bestM != null)
                    v.after.Add(("CC_Weapon_Pair".Translate(who.Named("PAWN"),
                        (bestR != null ? bestR.Label + " (" + F1(bestR.Rating) + ")" : "CC_Weapon_NoGun".Translate().Resolve()).Named("GUN"),
                        (bestM != null ? bestM.Label + " (" + F1(bestM.Rating) + ")" : "CC_Weapon_Fists".Translate().Resolve()).Named("BLADE")).Resolve(), UiKit.Ink, GameFont.Small));
                if (WeaponMods.SidearmsLoaded)
                    v.after.Add(("CC_Weapon_PairRule".Translate(F1(WeaponTagMath.SidearmRangedMass).Named("RANGED"), F1(WeaponTagMath.SidearmMeleeMass).Named("MELEE")).Resolve(),
                        UiKit.Faint, GameFont.Tiny));
            }
            // This weapon in Dual Wield's off hand.
            var canOff = WeaponMods.CanBeOffHand(def);
            if (canOff.HasValue && facts != null)
            {
                string line;
                if (WeaponMods.IsTwoHand(def) == true) line = "CC_Weapon_DualTwoHanded".Translate().Resolve();
                else if (!canOff.Value) line = "CC_Weapon_DualNotOff".Translate().Resolve();
                else
                {
                    var asOff = WeaponForMath.RateInHand(r, fighter, dual, true, Skill(r));
                    line = "CC_Weapon_DualCanOff".Translate(who.Named("PAWN"), F1(asOff.Overall).Named("RATING")).Resolve();
                }
                v.after.Add((line, UiKit.Faint, GameFont.Small));
            }
        }

        private int Skill(WeaponReport r) =>
            (r.Rating.Kind == "ranged" ? who.skills?.GetSkill(SkillDefOf.Shooting)?.Level : who.skills?.GetSkill(SkillDefOf.Melee)?.Level) ?? 8;

        private static string Pct(float share) => Mathf.RoundToInt(share * 100f).ToString(CultureInfo.InvariantCulture);
    }
}
