using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own apparel card ("Rimfolk's apparel card", key "ApparelCard" in Rimfolk's frame, on the item card's base):
    /// the piece's own picture large in a tile framed in its quality's colour, the rating's badge with a shooter's and a
    /// melee fighter's where they differ, the tags as chips with why, the line in plain words, the sub-ratings; protection by
    /// body part (a figure of the body filled where it covers, each region's share of the shots, its sharp, blunt and heat
    /// armour); warmth against the weather (the temperature outside today and the season's range against the comfortable
    /// range it gives, with what the colonist keeps wearing); the piece as it is (quality, material, hit points and wear, the
    /// game's thresholds for the tattered mood, from the dead, biocoded); its rating in other materials; the situations, the
    /// effects and the key numbers; then for one colonist their own rating and why it differs, and what it would take the
    /// place of. Drawn with the kit, in Rimfolk's look or the game's. Opens from a button on the game's info card, the badge
    /// on the gear tab, and first, in place of the game's card, with "Open Rimfolk's apparel card first".
    /// </summary>
    public class ApparelCardWindow : ItemCardWindow
    {
        public static bool On => RimfolkPatches.On && ApparelLive.On;

        public ApparelCardWindow() : base("ApparelCard")
        {
        }

        /// <summary>The card for a piece in hand, for a colonist (the one who wears or carries it, else the last chosen). One card at a time.</summary>
        public static void Open(Thing t, Pawn forWho)
        {
            if (t == null) return;
            var w = Find.WindowStack.WindowOfType<ApparelCardWindow>() ?? Added();
            w.Show(t, null, null, forWho ?? ApparelLive.Holder(t));
        }

        /// <summary>The card for a kind in a stuff (its usual one when none), normal quality.</summary>
        public static void Open(ThingDef d, ThingDef inStuff, Pawn forWho)
        {
            if (d == null) return;
            var w = Find.WindowStack.WindowOfType<ApparelCardWindow>() ?? Added();
            w.Show(null, d, inStuff ?? ApparelLive.DefaultStuff(d), forWho);
        }

        private static ApparelCardWindow Added()
        {
            var w = new ApparelCardWindow();
            Find.WindowStack.Add(w);
            return w;
        }

        private ApparelReport Report => thing != null ? ApparelLive.Of(thing) : ApparelLive.Of(def, stuff);

        protected override string Untitled => "CC_Apparel_CardTitle".Translate().Resolve();

        protected override bool Rated => Report?.Rating != null;

        protected override string NotRated => "CC_Apparel_NotRated".Translate();

        protected override string What => "apparel card";

        protected override void OnChosen() => view = null;

        protected override float Body(float w)
        {
            var r = Report;
            var g = r.Rating;
            float y = 4f;
            y = Head(w, y, HeadOf(r));
            if (r.Tags.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Tags".Translate());
                y += WeaponUi.Chips(0f, y, w, r.Tags) + 10f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Sub".Translate());
            y = Meters(w, y, ApparelForMath.SubKeys(g).Select(k => (ApparelUi.SubWords(k), g.Sub[k], ApparelUi.SubTip(k))).ToList()) + 6f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Apparel_Sec_Protection".Translate());
            y = Protection(w, y, r) + 8f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Apparel_Sec_Warmth".Translate());
            y = Warmth(w, y, r) + 8f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Apparel_Sec_AsItIs".Translate());
            y = AsItIs(w, y, r) + 8f;
            if (r.ByStuff.Count > 1)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Apparel_Sec_Stuffs".Translate());
                y = Stuffs(w, y, r) + 8f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Situations".Translate());
            y = Situations(w, y, g.Situations.Select(kv => (ApparelUi.SitWords(kv.Key), kv.Value, ApparelForMath.Best(g, kv.Key), ApparelForMath.Weak(g, kv.Key), ApparelUi.SitTip(kv.Key))),
                "CC_Weapon_BestFor".Translate().Resolve(), "CC_Weapon_WeakWhen".Translate().Resolve()) + 6f;
            if (g.Specials.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Effects".Translate());
                y = Effects(w, y, g.Specials.Select(s => (s.Label, s.Counted, s.Sign)), "CC_Weapon_Counted".Translate().Resolve(), "CC_Weapon_NamedOnly".Translate().Resolve()) + 6f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_Weapon_Sec_Numbers".Translate());
            y = Rows(w, y, Numbers(r)) + 8f;
            y = WhoWears(w, y, r);
            y = ForColonist(w, y, r);
            return y;
        }

        // ---------------------------------------------------------------- who should wear this

        private List<(Pawn p, float rating, float gain, WearGap? gap, bool wears)> wearers;
        private float whoAt = -10f;
        private ApparelReport whoFor;

        /// <summary>
        /// The colonists this piece suits best (Apparel/ApparelPick.cs), by the same scores as the colony's choice: their own
        /// rating with the season ahead, the raids met, their work and the rules of their title, role and faith; those it fills
        /// a need for first (warmth they lack, no armour, a worn-out piece), each with what it adds over what it would take the
        /// place of. Only for a piece in hand. A click chooses them below. Worked out once every two seconds.
        /// </summary>
        private float WhoWears(float w, float y, ApparelReport r)
        {
            if (!ApparelPick.On || thing == null) return y;
            if (whoFor != r || Time.realtimeSinceStartup - whoAt > 2f)
            {
                whoFor = r;
                whoAt = Time.realtimeSinceStartup;
                try
                {
                    wearers = ApparelPick.WhoShouldWear(thing);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("who should wear a piece, on its card", e);
                    wearers = null;
                }
            }
            if (wearers == null || wearers.Count == 0) return y;
            y += UiKit.SectionTitle(0f, y, w, "CC_ApparelPick_Card_Who".Translate());
            foreach (var (p, rating, gain, gap, wears) in wearers)
            {
                if (p == null || p.Destroyed) continue;
                var row = new Rect(0f, y, w, 24f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                UiKit.Portrait(new Rect(0f, y + 1f, 22f, 22f), p, UiKit.Faint);
                UiKit.LabelFit(new Rect(28f, y, 130f, 24f), p.LabelShortCap, UiKit.Ink);
                WeaponUi.Badge(new Rect(162f, y + 3f, 34f, 18f), rating);
                string note = wears ? "CC_ApparelPick_Card_Wears".Translate(p.Named("PAWN")).Resolve()
                    : gap.HasValue ? ("CC_ApparelPick_Card_Need_" + gap.Value).Translate(p.Named("PAWN"), ApparelPickMath.GainText(gain).Named("GAIN")).Resolve()
                    : gain >= ApparelPickMath.Threshold ? "CC_ApparelPick_Card_Better".Translate(ApparelPickMath.GainText(gain).Named("GAIN"), p.Named("PAWN")).Resolve()
                    : gain > 0.05f ? "CC_ApparelPick_Card_Little".Translate(ApparelPickMath.GainText(gain).Named("GAIN")).Resolve()
                    : "CC_ApparelPick_Card_NoBetter".Translate(p.Named("PAWN")).Resolve();
                UiKit.LabelFit(new Rect(202f, y, w - 202f, 24f), note, gap.HasValue && !wears ? UiKit.Warn : gain >= ApparelPickMath.Threshold && !wears ? UiKit.Good : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, "CC_ApparelPick_Card_WhoTip".Translate(p.Named("PAWN")).Resolve());
                if (Widgets.ButtonInvisible(row)) Choose(p);
                y += 26f;
            }
            return y + 6f;
        }

        // ---------------------------------------------------------------- the head

        private HeadFacts HeadOf(ApparelReport r)
        {
            var g = r.Rating;
            string quality = thing != null && thing.TryGetQuality(out var q) ? q.GetLabel() : QualityCategory.Normal.GetLabel();
            string made = r.StuffLabel.NullOrEmpty() ? "CC_Weapon_Card_MadeNoStuff".Translate(quality.Named("QUALITY")).Resolve()
                : "CC_Weapon_Card_Made".Translate(quality.Named("QUALITY"), r.StuffLabel.Named("STUFF")).Resolve();
            string sub = (thing != null ? "CC_Apparel_Card_RatedFor" : "CC_Apparel_Card_RatedForKind").Translate(ApparelUi.ForWords(g.For).Named("FOR"), made.Named("MADE")).Resolve();
            if (r.Rough) sub += " · " + "CC_Weapon_Rough".Translate();
            var t = thing;
            var d = def;
            var s = stuff;
            return new HeadFacts
            {
                Rating = g.Overall, RatingTip = () => "CC_Apparel_Info_RatingExpl".Translate(),
                Band = WeaponWords.Quality(g.Overall).CapitalizeFirst() + " · " + ApparelUi.RoleWords(g.Role), RatedFor = sub,
                RoughTip = r.Rough ? "CC_Weapon_Info_Rough".Translate() + "\n" + string.Join("\n", r.Facts.Unsure.Select(u => "- " + u)) : null,
                Second = ApparelUi.BothLine(g), Plain = r.Plain, GameLabel = "CC_Apparel_GameCard".Translate(), GameTip = "CC_Apparel_GameCard_Tip".Translate(),
                OpenGameCard = () => ApparelCardFirst.OpenGameCard(t, d, s)
            };
        }

        // ---------------------------------------------------------------- protection by body part

        private float Protection(float w, float y, ApparelReport r)
        {
            var g = r.Rating;
            var a = r.Facts;
            // Its armour against each kind of hit, held at the game's 200%.
            float colW = (w - 16f) / 3f;
            var kinds = new[] { ("CC_Apparel_Armour_Sharp", g.Sharp), ("CC_Apparel_Armour_Blunt", g.Blunt), ("CC_Apparel_Armour_Heat", g.Heat) };
            for (int i = 0; i < 3; i++)
            {
                var (key, v) = kinds[i];
                UiKit.Meter(new Rect(i * (colW + 8f), y, colW, UiKit.MeterHeight), v / 2f, v >= 0.8f ? UiKit.Good : v >= 0.3f ? UiKit.Cool : UiKit.Faint, key.Translate(), v.ToStringPercent(),
                    null, (key + "_Tip").Translate());
            }
            y += UiKit.MeterHeight + 8f;

            // The body: a figure filled where it covers, and each region's share of the shots and how much of it it covers.
            var regions = ApparelForMath.Coverage(a.OffHandCovers ?? a.Groups.ToArray());
            var cover = WeaponUi.RatingColour(g.Sub.TryGetValue("guns", out var guns) ? Math.Max(guns, 4f) : 5f);
            float top = y;
            ApparelUi.DrawFigure(new Vector2(8f, y), regions, cover);
            float lx = ApparelUi.FigureW + 24f;
            foreach (var reg in regions)
            {
                var row = new Rect(lx, y, w - lx, 18f);
                bool covered = reg.Covered > 0.001f;
                UiKit.LabelFit(new Rect(lx, y, 90f, 18f), ApparelUi.RegionWords(reg.Key), covered ? UiKit.Ink : UiKit.Faint, GameFont.Small);
                var bar = new Rect(lx + 94f, y + 6f, Mathf.Max(30f, w - lx - 94f - 150f), 7f);
                UiKit.Bar(bar, reg.Covered, cover);
                string note = covered ? "CC_Apparel_RegionCovered".Translate(reg.Covered.ToStringPercent().Named("COVERED"), reg.Shots.ToStringPercent().Named("SHOTS")).Resolve()
                    : "CC_Apparel_RegionBare".Translate(reg.Shots.ToStringPercent().Named("SHOTS")).Resolve();
                UiKit.LabelFit(new Rect(bar.xMax + 6f, y, w - bar.xMax - 6f, 18f), note, covered ? UiKit.Ink : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, ApparelUi.RegionTip(reg));
                y += 19f;
            }
            y = Mathf.Max(y, top + ApparelUi.FigureH) + 4f;
            string line = a.OffHandCovers != null
                ? "CC_Apparel_OffHandCovers".Translate(g.Coverage.ToStringPercent().Named("COVERAGE")).Resolve()
                : "CC_Apparel_Covers".Translate(g.Coverage.ToStringPercent().Named("COVERAGE")).Resolve();
            return y + UiKit.Paragraph(0f, y, w, line, UiKit.Faint, GameFont.Tiny);
        }

        // ---------------------------------------------------------------- warmth against the weather

        private float Warmth(float w, float y, ApparelReport r)
        {
            var g = r.Rating;
            var a = r.Facts;
            float warmth = ApparelForMath.Warmth(a, g), cooling = ApparelForMath.Cooling(a, g);
            y += UiKit.Paragraph(0f, y, w, "CC_Apparel_Keeps".Translate(ApparelForMath.Degrees(warmth).Named("COLD"), ApparelForMath.Degrees(cooling).Named("HEAT")).Resolve(), UiKit.Ink) + 2f;
            var v = ViewFor(r);
            if (!v.climate.has)
                return y + UiKit.Paragraph(0f, y, w, "CC_Apparel_NoClimate".Translate(), UiKit.Faint, GameFont.Tiny);
            y += UiKit.Paragraph(0f, y, w, "CC_Apparel_Weather".Translate(ApparelForMath.Degrees(v.climate.now).Named("NOW"), ApparelForMath.Degrees(v.climate.low).Named("LOW"),
                ApparelForMath.Degrees(v.climate.high).Named("HIGH")).Resolve(), UiKit.Ink) + 2f;
            float min = v.comfyMin, max = v.comfyMax;
            string whose = v.fc != null ? "CC_Apparel_ComfortFor".Translate(who.Named("PAWN"), ApparelForMath.Degrees(min).Named("MIN"), ApparelForMath.Degrees(max).Named("MAX")).Resolve()
                : "CC_Apparel_ComfortAlone".Translate(ApparelForMath.Degrees(min).Named("MIN"), ApparelForMath.Degrees(max).Named("MAX")).Resolve();
            y += UiKit.Paragraph(0f, y, w, whose, UiKit.Ink) + 4f;
            y = Strip(w, y, v.climate.low, v.climate.high, v.climate.now, min, max) + 4f;
            bool warm = min <= Math.Min(v.climate.low, v.climate.now), cool = max >= Math.Max(v.climate.high, v.climate.now);
            string verdict = warm && cool ? "CC_Apparel_VerdictOk".Translate().Resolve()
                : !warm ? "CC_Apparel_VerdictCold".Translate(ApparelForMath.Degrees(min).Named("MIN"), ApparelForMath.Degrees(Math.Min(v.climate.low, v.climate.now)).Named("LOW")).Resolve()
                : "CC_Apparel_VerdictHot".Translate(ApparelForMath.Degrees(max).Named("MAX"), ApparelForMath.Degrees(Math.Max(v.climate.high, v.climate.now)).Named("HIGH")).Resolve();
            if (!warm && !cool)
                verdict += " " + "CC_Apparel_VerdictHot".Translate(ApparelForMath.Degrees(max).Named("MAX"), ApparelForMath.Degrees(Math.Max(v.climate.high, v.climate.now)).Named("HIGH")).Resolve();
            return y + UiKit.Paragraph(0f, y, w, verdict, warm && cool ? UiKit.Good : UiKit.Warn) + 2f;
        }

        /// <summary>
        /// The temperatures as a strip: the season's range shaded, the comfortable range it gives framed over it, today's
        /// temperature marked; the ends and the marks named under it.
        /// </summary>
        private float Strip(float w, float y, float low, float high, float now, float min, float max)
        {
            float lo = Mathf.Floor((Mathf.Min(low, Mathf.Min(min, now)) - 5f) / 5f) * 5f, hi = Mathf.Ceil((Mathf.Max(high, Mathf.Max(max, now)) + 5f) / 5f) * 5f;
            var strip = new Rect(0f, y, w, 16f);
            float X(float t) => strip.x + strip.width * Mathf.Clamp01((t - lo) / Mathf.Max(hi - lo, 1f));
            if (Event.current.type == EventType.Repaint)
            {
                UiKit.Round(strip, UiKit.WithAlpha(UiKit.Faint, 0.15f), SkinMath.RadiusSmall);
                var season = new Rect(X(low), strip.y + 3f, Mathf.Max(2f, X(high) - X(low)), strip.height - 6f);
                UiKit.Round(season, UiKit.WithAlpha(UiKit.Cool, 0.45f), SkinMath.RadiusSmall);
                var comfort = new Rect(X(min), strip.y, Mathf.Max(2f, X(max) - X(min)), strip.height);
                UiKit.RoundEdge(comfort, UiKit.Good, SkinMath.RadiusSmall);
                Widgets.DrawBoxSolid(new Rect(X(now) - 1f, strip.y - 2f, 2f, strip.height + 4f), UiKit.Ink);
            }
            TooltipHandler.TipRegion(strip, "CC_Apparel_StripTip".Translate(ApparelForMath.Degrees(low).Named("LOW"), ApparelForMath.Degrees(high).Named("HIGH"),
                ApparelForMath.Degrees(now).Named("NOW"), ApparelForMath.Degrees(min).Named("MIN"), ApparelForMath.Degrees(max).Named("MAX")).Resolve());
            y = strip.yMax + 2f;
            UiKit.LabelFit(new Rect(0f, y, 60f, 16f), ApparelForMath.Degrees(lo) + " °C", UiKit.Faint, GameFont.Tiny);
            UiKit.LabelFit(new Rect(w - 60f, y, 60f, 16f), ApparelForMath.Degrees(hi) + " °C", UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            UiKit.LabelFit(new Rect(Mathf.Clamp(X(now) - 50f, 60f, w - 160f), y, 100f, 16f), "CC_Apparel_Today".Translate(ApparelForMath.Degrees(now).Named("NOW")).Resolve(), UiKit.Ink, GameFont.Tiny,
                TextAnchor.MiddleCenter);
            return y + 16f;
        }

        // ---------------------------------------------------------------- the piece as it is

        private float AsItIs(float w, float y, ApparelReport r)
        {
            var rows = new List<(string, string)>();
            string quality = thing != null ? (thing.TryGetQuality(out var q) ? q.GetLabel() : "CC_Apparel_NoQuality".Translate().Resolve())
                : "CC_Apparel_KindQuality".Translate(QualityCategory.Normal.GetLabel().Named("QUALITY")).Resolve();
            rows.Add(("CC_Apparel_Num_Quality".Translate(), quality.CapitalizeFirst()));
            rows.Add(("CC_Apparel_Num_Material".Translate(), r.StuffLabel.NullOrEmpty() ? "CC_Apparel_NoStuff".Translate().Resolve() : r.StuffLabel.CapitalizeFirst()));
            y = Rows(w, y, rows);
            var d = r.Def;
            float max = thing != null ? thing.MaxHitPoints : ApparelRateMath.Stat(r.Facts.StatBases, "MaxHitPoints", 100f);
            float hp = thing != null ? thing.HitPoints : max;
            if (def.useHitPoints && max > 0f)
            {
                float share = Mathf.Clamp01(hp / max);
                UiKit.LabelFit(new Rect(0f, y, 150f, 20f), "CC_Apparel_Num_HitPoints".Translate(), UiKit.Faint);
                UiKit.LabelFit(new Rect(156f, y, w - 156f, 20f), Mathf.RoundToInt(hp) + " / " + Mathf.RoundToInt(max) + " (" + share.ToStringPercent() + ")", UiKit.Ink);
                y += 22f;
                // The wear bar, the game's two thresholds for the mood marked on it.
                var bar = new Rect(0f, y, w, 10f);
                bool cares = d.CareIfDamaged;
                UiKit.Bar(bar, share, share < ApparelRead.TatteredBelow ? UiKit.Bad : share < ApparelRead.FrayedBelow ? UiKit.Warn : UiKit.Good);
                if (cares && Event.current.type == EventType.Repaint)
                    foreach (var mark in new[] { ApparelRead.FrayedBelow, ApparelRead.TatteredBelow })
                        Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * mark - 1f, bar.y - 3f, 2f, bar.height + 6f), UiKit.Ink);
                y = bar.yMax + 4f;
                string frayed = d.Frayed.label, tattered = d.Tattered.label;
                string marks = !cares ? "CC_Apparel_WearNoCare".Translate().Resolve()
                    : "CC_Apparel_WearMarks".Translate(frayed.Named("FRAYED"), ApparelRead.FrayedBelow.ToStringPercent().Named("AT"), MoodText(d.Frayed.mood).Named("MOOD"),
                        tattered.Named("TATTERED"), ApparelRead.TatteredBelow.ToStringPercent().Named("UNDER"), MoodText(d.Tattered.mood).Named("WORSE")).Resolve();
                y += UiKit.Paragraph(0f, y, w, marks, UiKit.Faint, GameFont.Tiny) + 2f;
                if (d.WearPerDay > 0f)
                {
                    var days = ApparelRead.DaysOfWear(hp, max, d.WearPerDay, ApparelRead.FrayedBelow);
                    string wear = days.HasValue && cares
                        ? "CC_Apparel_WearDaysLine".Translate(ApparelForMath.Degrees(d.WearPerDay).Named("WEAR"), Mathf.RoundToInt(days.Value).Named("DAYS"), ApparelRead.FrayedBelow.ToStringPercent().Named("AT")).Resolve()
                        : "CC_Apparel_WearPerDay".Translate(ApparelForMath.Degrees(d.WearPerDay).Named("WEAR")).Resolve();
                    y += UiKit.Paragraph(0f, y, w, wear, UiKit.Ink, GameFont.Small) + 2f;
                }
                else y += UiKit.Paragraph(0f, y, w, "CC_Apparel_NoWear".Translate(), UiKit.Ink) + 2f;
                y += UiKit.Paragraph(0f, y, w, "CC_Apparel_WearArmour".Translate(), UiKit.Faint, GameFont.Tiny) + 2f;
            }
            if (thing is Apparel worn && worn.WornByCorpse)
                y += UiKit.Paragraph(0f, y, w, (d.CareIfWornByCorpse ? "CC_Apparel_FromTheDead" : "CC_Apparel_FromTheDeadNoCare").Translate(MoodText(d.DeadMans.mood).Named("MOOD")).Resolve(),
                    d.CareIfWornByCorpse ? UiKit.Warn : UiKit.Faint) + 2f;
            if (!d.BiocodedTo.NullOrEmpty())
                y += UiKit.Paragraph(0f, y, w, "CC_Apparel_Biocoded".Translate(d.BiocodedTo.Named("WHO")).Resolve(), UiKit.Ink) + 2f;
            return y;
        }

        private static string MoodText(float m) => (m > 0f ? "+" : "") + F2(m);

        // ---------------------------------------------------------------- other materials

        private float Stuffs(float w, float y, ApparelReport r)
        {
            int cols = Mathf.Max(1, Mathf.FloorToInt(w / 150f));
            float colW = w / cols;
            int i = 0;
            foreach (var kv in r.ByStuff.OrderByDescending(kv => kv.Value))
            {
                if (kv.Key.NullOrEmpty()) continue;
                var cell = new Rect((i % cols) * colW, y + (i / cols) * 22f, colW - 6f, 20f);
                bool mine = kv.Key == r.StuffLabel;
                WeaponUi.Badge(new Rect(cell.x, cell.y + 1f, 34f, 18f), kv.Value);
                UiKit.LabelFit(new Rect(cell.x + 40f, cell.y, cell.width - 40f, 20f), kv.Key.CapitalizeFirst() + (mine ? " · " + "CC_Apparel_ThisOne".Translate() : ""), mine ? UiKit.Accent : UiKit.Ink);
                i++;
            }
            y += ((i + cols - 1) / cols) * 22f;
            return y + UiKit.Paragraph(0f, y, w, "CC_Apparel_StuffsNote".Translate(ApparelUi.RoleWords(r.Rating.Role).Named("ROLE")).Resolve(), UiKit.Faint, GameFont.Tiny);
        }

        // ---------------------------------------------------------------- key numbers

        private List<(string, string)> Numbers(ApparelReport r)
        {
            var g = r.Rating;
            var a = r.Facts;
            var rows = new List<(string, string)>
            {
                ("CC_Apparel_Num_Armour".Translate(), g.Sharp.ToStringPercent() + " / " + g.Blunt.ToStringPercent() + " / " + g.Heat.ToStringPercent()),
                ("CC_Apparel_Num_Insulation".Translate(), "CC_Apparel_Num_InsulationValue".Translate(ApparelForMath.Degrees(g.Cold).Named("COLD"), ApparelForMath.Degrees(g.HeatInsulation).Named("HEAT")).Resolve()),
                ("CC_Apparel_Num_Covers".Translate(), string.Join(", ", (a.OffHandCovers ?? a.Groups.ToArray()).Select(GroupLabel)) + " (" + g.Coverage.ToStringPercent() + ")"),
                ("CC_Apparel_Num_Layers".Translate(), string.Join(", ", a.Layers.Select(LayerLabel))),
                ("CC_Weapon_Num_Mass".Translate(), ApparelRateMath.Stat(a.StatBases, "Mass").ToStringMass()),
            };
            float move = ApparelRateMath.Offset(a, "MoveSpeed");
            if (Math.Abs(move) > 0.001f) rows.Add(("CC_Apparel_Num_Move".Translate(), (move > 0 ? "+" : "") + F2(move)));
            return rows;
        }

        private static string GroupLabel(string g) => DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(g)?.label ?? g;

        private static string LayerLabel(string l) => DefDatabase<ApparelLayerDef>.GetNamedSilentFail(l)?.label ?? l;

        // ---------------------------------------------------------------- for a colonist

        /// <summary>What the card says for one colonist (or the map's climate with none), worked out once and kept two seconds.</summary>
        private sealed class ColonistView
        {
            public ApparelReport report;
            public Pawn pawn;
            public float made;
            public (bool has, float now, float low, float high) climate;
            public float comfyMin, comfyMax;
            public string cant;
            public ApparelWearer wearer;
            public ApparelForColonist fc;
            public bool wearsIt;
            public readonly List<(Apparel t, float? rating, float? delta)> replaces = new List<(Apparel, float?, float?)>();
        }

        private ColonistView view;

        private ColonistView ViewFor(ApparelReport r)
        {
            if (view != null && view.report == r && view.pawn == who && Time.realtimeSinceStartup - view.made < 2f) return view;
            var v = new ColonistView { report = r, pawn = who, made = Time.realtimeSinceStartup };
            view = v;
            try
            {
                if (who != null && who.RaceProps.Humanlike)
                {
                    v.cant = ApparelLive.CantWear(who, thing, def);
                    (v.wearer, v.fc) = ApparelLive.ForColonist(who, r, thing);
                    v.climate = ApparelLive.Climate(who);
                    v.wearsIt = thing is Apparel ap && who.apparel?.WornApparel != null && who.apparel.WornApparel.Contains(ap);
                    if (v.fc != null)
                    {
                        v.comfyMin = v.fc.ComfyMinWith;
                        v.comfyMax = v.fc.ComfyMaxWith;
                    }
                    foreach (var t in ApparelLive.Replaces(who, def, thing))
                    {
                        var rep = ApparelLive.Of(t);
                        float? rating = rep != null ? ApparelLive.ForColonist(who, rep, t).rating?.Rating ?? rep.Rating.Overall : (float?)null;
                        v.replaces.Add((t, rating, rating.HasValue && v.fc != null ? v.fc.Rating - rating.Value : (float?)null));
                    }
                }
                if (v.fc == null)
                {
                    // No colonist: the map's weather, the piece worn alone over a bare human.
                    var map = Find.CurrentMap;
                    v.climate = map != null ? ClimateOf(map) : (false, 21f, 21f, 21f);
                    float baseMin = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin), baseMax = ThingDefOf.Human.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
                    v.comfyMin = baseMin - ApparelForMath.Warmth(r.Facts, r.Rating);
                    v.comfyMax = baseMax + ApparelForMath.Cooling(r.Facts, r.Rating);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the apparel card's view for a colonist", e);
            }
            return v;
        }

        /// <summary>A map's climate, as a colonist standing on it would have it.</summary>
        private static (bool has, float now, float low, float high) ClimateOf(Map map)
        {
            var any = map.mapPawns?.FreeColonists?.FirstOrDefault();
            if (any != null) return ApparelLive.Climate(any);
            return (true, map.mapTemperature.OutdoorTemp, map.mapTemperature.OutdoorTemp, map.mapTemperature.OutdoorTemp);
        }

        private float ForColonist(float w, float y, ApparelReport r)
        {
            string head = who != null ? "CC_Weapon_Sec_For".Translate(who.Named("PAWN")).Resolve() : "CC_Weapon_Sec_ForNone".Translate().Resolve();
            y = ColonistHeading(w, y, head, "CC_Weapon_ChooseColonist".Translate(), "CC_Apparel_ChooseColonist_Tip".Translate(), "CC_Weapon_NoColonists".Translate());
            if (who == null) return y + UiKit.Paragraph(0f, y, w, "CC_Apparel_NoneChosen".Translate(), UiKit.Faint) + 6f;
            var v = ViewFor(r);
            if (v.cant != null) y += UiKit.Paragraph(0f, y, w, v.cant, UiKit.Bad) + 4f;
            if (v.fc != null)
            {
                WeaponUi.Badge(new Rect(0f, y, 52f, 34f), v.fc.Rating, null, 0, GameFont.Small);
                UiKit.LabelFit(new Rect(60f, y, w - 60f, 18f),
                    "CC_Apparel_ForLine".Translate(who.Named("PAWN"), F1(v.fc.Rating).Named("RATING"), F1(v.fc.Reference).Named("REFERENCE")).Resolve(), UiKit.Ink);
                UiKit.LabelFit(new Rect(60f, y + 17f, w - 60f, 18f), ApparelUi.Steps(v.fc), UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(0f, y, w, 36f), "CC_Apparel_ForTip".Translate(who.Named("PAWN")).Resolve());
                y += 40f;
                if (v.wearer != null)
                {
                    y += UiKit.Paragraph(0f, y, w, "CC_Apparel_HowTheyFight".Translate(KindWords(v.wearer.Kind).Named("KIND"), v.wearer.KindWhy.Named("WHY")).Resolve(), UiKit.Ink) + 3f;
                    if (!v.wearer.MissingWhy.NullOrEmpty())
                        y += UiKit.Paragraph(0f, y, w, "CC_Apparel_TheirBody".Translate(who.Named("PAWN"), v.wearer.MissingWhy.Named("MISSING")).Resolve(), UiKit.Ink) + 3f;
                    if (v.wearer.WrongGender) y += UiKit.Paragraph(0f, y, w, "CC_Apparel_WrongGender".Translate(who.Named("PAWN"), MoodText(v.wearer.WrongGenderMood.mood).Named("MOOD")).Resolve(), UiKit.Warn) + 3f;
                }
            }
            y += 4f;
            y += UiKit.SectionTitle(0f, y, w, "CC_Apparel_Sec_Wears".Translate(who.Named("PAWN")).Resolve());
            if (v.wearsIt) y += UiKit.Paragraph(0f, y, w, "CC_Apparel_WearsThis".Translate(who.Named("PAWN")).Resolve(), UiKit.Ink) + 4f;
            else if (v.replaces.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Apparel_NothingInPlace".Translate(who.Named("PAWN")).Resolve(), UiKit.Faint) + 4f;
            else
            {
                y += UiKit.Paragraph(0f, y, w, "CC_Apparel_WouldReplace".Translate(who.Named("PAWN")).Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
                foreach (var (t, rating, delta) in v.replaces)
                {
                    if (t == null || t.Destroyed) continue;
                    var row = new Rect(0f, y, w, 22f);
                    UiKit.ItemPortrait(new Rect(0f, y + 1f, 20f, 20f), t);
                    float bx = w - 40f;
                    if (rating.HasValue) WeaponUi.Badge(new Rect(bx, y + 2f, 36f, 18f), rating.Value);
                    string note = delta.HasValue ? "CC_Apparel_ThisOneDelta".Translate(WeaponForMath.Delta(delta.Value).Named("DELTA")).Resolve() : "";
                    UiKit.LabelFit(new Rect(26f, y, bx - 30f - (note.Length > 0 ? 140f : 0f), 22f), t.LabelCapNoCount, UiKit.Ink);
                    if (note.Length > 0)
                        UiKit.LabelFit(new Rect(bx - 144f, y, 140f, 22f), note, delta.Value >= 0.05f ? UiKit.Good : delta.Value <= -0.05f ? UiKit.Bad : UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    if (Widgets.ButtonInvisible(row)) Open(t, who);
                    y += 24f;
                }
            }
            return y;
        }

        private static string KindWords(string kind) => ("CC_Apparel_Kind_" + ApparelForMath.KeyWord(kind)).Translate();
    }
}
