using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's health at a glance: a row for each of the colony's people with something that will pass or needs a
    /// reaction, and nothing else. Wounds, bleeding (with the time to death as the game reckons it), what wants tending,
    /// illnesses and their immunity race ("recovering" or "losing"), anything that can kill while it's moving
    /// (hypothermia, toxic buildup), and Rimfolk's everyday ailments. Never lost parts, implants, scars, chronic
    /// conditions, pregnancy, or buffs, auras and other mods' magic (<see cref="HudHealthMath.Shows"/>). Bleeding or soon
    /// lethal first, then untended, then losing a race, then healing. A calm colony gets one quiet line.
    ///
    /// With "Show details" each condition says how it stands: tended and how well (and when the tending wears off), or
    /// untended; how much of a wound has healed (from its worst, remembered while the panel watches; else what is left)
    /// and roughly when it's healed, from the game's own healing; an illness's immunity against its severity and when
    /// it will be over, or that it's losing; how long one of Rimfolk's ailments has left; the bleeding a day. Shown as a
    /// summary under each name, or one line for each condition. Gathered at most twice a second, like every panel.
    /// A click selects them and opens their Health tab; caravans and other maps are an option.
    ///
    /// With "Icons and bars" (on unless switched off) it speaks the Health tab's own picture language: each condition
    /// carries the icon the game shows for it (tended well or poorly, needs tending, immune, the drop of blood for
    /// bleeding), a tiny bar (how far a wound has healed, the immunity race, how far through an ailment is, how close to
    /// deadly) and the tending's quality in red, yellow or green. The summary shows them as chips that wrap onto a second
    /// line (the same condition in the same state grouped: "Bruise ×2"), ending in "+N"; each chip's tooltip has it in
    /// full. The name gets a small health mark, the status word its colour (healing green, getting worse orange,
    /// bleeding or soon deadly red).
    /// </summary>
    public class HudHealth : HudPanel
    {
        public const string PartDetails = "details", PartHealing = "healing", PartAilments = "ailments", PartFacts = "facts", PartVisuals = "visuals";
        public static readonly string[] Parts = { PartDetails, PartFacts, PartVisuals, PartHealing, PartAilments };

        /// <summary>The rows' styles (HudPanelState.style): a summary line under each name (the default), or a line for each condition.</summary>
        public const string StyleSummary = "summary", StyleLines = "lines";

        /// <summary>A line for each condition shows this many at most (the rest are in the tooltip).</summary>
        private const int MaxLines = 5;

        /// <summary>The summary's chips wrap onto this many lines at most; the rest is "+N".</summary>
        private const int MaxChipLines = 2;

        // The colours of what the words mean (HealthTone): green and red as everywhere, a clear yellow between them.
        private static readonly Color Yellow = new Color(0.9f, 0.84f, 0.36f);
        private static readonly Color LabelQuiet = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color PassingBar = new Color(0.56f, 0.73f, 0.96f, 0.9f);

        private sealed class Row
        {
            public Pawn pawn;
            public float score;
            public string name, status, tip;
            public Color statusColor;
            public int barOrder;
            /// <summary>The game's summary health (0..1), for the mark by the name; under 0 when unknown.</summary>
            public float health = -1f;
            public Color healthColor;
            public readonly List<Line> lines = new List<Line>();
            /// <summary>The summary's pieces: each condition (the same in the same state once, ×N) and the bleeding.</summary>
            public readonly List<Chip> chips = new List<Chip>();
            /// <summary>The chips laid out (at <see cref="laidScale"/> in <see cref="laidFont"/>): where each sits, and the "+N".</summary>
            public readonly List<ChipSpot> spots = new List<ChipSpot>();
            public ChipFlow flow;
            public string more;
            public float moreW, gap, space, laidScale = -1f;
            public GameFont laidFont;
            /// <summary>Gathered with icons and bars (the look it's drawn in until the next gathering).</summary>
            public bool visual;
            /// <summary>The chip lines the row was measured for (what it took when last drawn).</summary>
            public int measuredLines = 1;
        }

        /// <summary>One condition, or the same one several times, in the summary.</summary>
        private sealed class Chip
        {
            public Texture2D icon;
            public Color iconColor = Color.white;
            public string label, fact, figure, tip;
            public Color labelColor, factColor, figureColor;
            public HealthBar bar;
            /// <summary>The bar's share (the immunity in a race), and the severity in a race.</summary>
            public float a, b;
            public Color barColor;
            public int uid;
            /// <summary>Measured when laid out.</summary>
            public float labelW, factW, figureW;
        }

        /// <summary>One condition's line in the "a line for each" style.</summary>
        private sealed class Line
        {
            public string label, fact;
            public Color labelColor, factColor;
            public float immunity = -1f, severity = -1f;
            // With icons and bars: the icon, the tending in its colour, the rest of the facts, a bar, the full tooltip.
            public Texture2D icon;
            public Color iconColor = Color.white;
            public string tend, rest, tip;
            public Color tendColor, restColor, barColor;
            public HealthBar bar;
            public float a, b;
            public int uid;
            public float tendW = -1f, restW = -1f;
        }

        private sealed class Item
        {
            public Hediff hediff;
            public HediffFacts facts;
            public HealthRace race;
            public float immunity, immunityPerDay, score;
            public bool lifeThreatening;
            public float tendQuality = -1f;
            public int tendTicksLeft = -1;
            public float healed = -1f, daysLeft = -1f;
            public float severityPerDayImmune;
            public bool notHealing;
            public float ageDays;
            /// <summary>The tooltip's line for it.</summary>
            public string tip;
            /// <summary>The icon the Health tab shows for it (Hediff.StateIcon), or the drop of blood while it bleeds.</summary>
            public Texture2D icon;
            public Color iconColor = Color.white;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly List<Item> items = new List<Item>();
        private int hidden;
        private string moreText;

        /// <summary>Each wound's severity at its worst while the panel has watched it (not saved), and those seen from the start.</summary>
        private static readonly Dictionary<Hediff, float> worst = new Dictionary<Hediff, float>();
        private static readonly HashSet<Hediff> seenFresh = new HashSet<Hediff>();
        private static readonly HashSet<Hediff> seenNow = new HashSet<Hediff>();
        private static readonly List<Hediff> gone = new List<Hediff>();

        /// <summary>A wound first seen younger than this (ticks) was seen from the start, so its worst is known.</summary>
        private const int FreshTicks = 2500;

        public HudHealth() : base(HudLayout.Health)
        {
        }

        private bool Details => St.Shows(PartFacts);
        private bool LineEach => St.style == StyleLines;
        /// <summary>Icons and bars, and the words in colour ("Icons and bars" in the options); else the plain text look.</summary>
        private bool Visual => St.Shows(PartVisuals);

        /// <summary>A meaning's colour: green, yellow, orange, red, or <paramref name="quiet"/>.</summary>
        private static Color ToneColor(HealthTone tone, Color quiet)
        {
            switch (tone)
            {
                case HealthTone.Good: return UiKit.Good;
                case HealthTone.Fair: return Yellow;
                case HealthTone.Warn: return UiKit.Warn;
                case HealthTone.Bad: return UiKit.Bad;
                default: return quiet;
            }
        }

        // ---------------- Gathering ----------------

        protected override void Refresh()
        {
            rows.Clear();
            seenNow.Clear();
            var st = St;
            var order = st.sort == HudSort.Bar ? HudMood.BarOrder() : null;
            var all = new List<Row>();
            foreach (var p in HudPeople.Who(st.who))
            {
                Row row;
                try
                {
                    row = RowFor(p, st);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("health panel: " + p.LabelShort, e);
                    continue;
                }
                if (row == null) continue;
                row.barOrder = order != null && order.TryGetValue(p, out int i) ? i : int.MaxValue;
                all.Add(row);
            }
            // Forget the wounds that healed or aren't listed any more.
            gone.Clear();
            foreach (var hd in worst.Keys)
                if (!seenNow.Contains(hd))
                    gone.Add(hd);
            foreach (var hd in gone)
            {
                worst.Remove(hd);
                seenFresh.Remove(hd);
            }
            if (st.sort == HudSort.Bar) all = all.OrderBy(r => r.barOrder).ThenBy(r => r.pawn.thingIDNumber).ToList();
            else all = all.OrderByDescending(r => r.score).ThenBy(r => r.pawn.thingIDNumber).ToList();
            var keep = HudMath.Keep(all.Select(r => r.score).ToList(), st.maxRows, HudRank.LifeFloor, out hidden);
            foreach (int i in keep) rows.Add(all[i]);
            moreText = hidden > 0 ? "CC_Pnl_AndMore".Translate(hidden).Resolve() : null;
        }

        /// <summary>What a colonist has that belongs on the panel, or null when they're well.</summary>
        private Row RowFor(Pawn p, HudPanelState st)
        {
            var set = p.health?.hediffSet;
            if (set == null) return null;
            items.Clear();
            int untended = 0;
            foreach (var hd in set.hediffs)
            {
                if (hd?.def == null || !hd.Visible) continue;
                var f = Facts(hd, out float immuneRate, out float otherRate);
                if (!HudHealthMath.Shows(f)) continue;
                if (f.ailment && !st.Shows(PartAilments)) continue;
                var item = new Item { hediff = hd, facts = f, severityPerDayImmune = immuneRate + otherRate };
                if (f.immunizable) Race(p, hd, item);
                item.lifeThreatening = hd.CurStage?.lifeThreatening == true && !hd.FullyImmune();
                item.score = HudHealthMath.Score(f, item.race, item.lifeThreatening);
                if (f.needsTend) untended++;
                items.Add(item);
            }
            float bleed = set.BleedRateTotal;
            float hours = -1f;
            if (bleed > 0.001f)
            {
                int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(p);
                hours = ticks / (float)GenDate.TicksPerHour;
            }
            if (items.Count == 0 && hours < 0f) return null;
            float worstScore = items.Count == 0 ? 0f : items.Max(i => i.score);
            float score = HudHealthMath.PawnScore(hours, worstScore, untended);
            // Only healing: on the panel unless "those only healing" is switched off.
            if (score < HudRank.SoonFloor && !st.Shows(PartHealing)) return null;
            items.Sort((a, b) => b.score.CompareTo(a.score));
            HowFar(p, set);
            var row = new Row { pawn = p, score = score, name = p.LabelShortCap };
            string away = HudPeople.Away(p);
            if (away != null) row.name += " (" + away + ")";
            bool visual = Visual;
            foreach (var it in items)
            {
                it.tip = ItemTip(it);
                if (visual) IconOf(it);
            }
            Status(row, hours, untended);
            HealthMark(row, p);
            Chips(row, hours, bleed);
            if (LineEach) Lines(row, hours, bleed);
            row.tip = Tip(p, row, hours, bleed);
            return row;
        }

        private static HediffFacts Facts(Hediff hd, out float immuneRate, out float otherRate)
        {
            var def = hd.def;
            var f = new HediffFacts
            {
                injury = hd is Hediff_Injury,
                missingPart = hd is Hediff_MissingPart,
                addedPart = hd is Hediff_AddedPart || def.countsAsAddedPartOrImplant,
                chronic = def.chronic,
                bad = def.isBad,
                sick = def.makesSickThought,
                pregnancy = def.pregnant || hd is HediffWithParents,
                lethalSeverity = def.lethalSeverity,
                severity = hd.Severity,
                bleedRate = hd.BleedRate
            };
            f.permanent = hd.IsPermanent();
            f.tendable = def.tendable || f.injury;
            f.tended = hd.IsTended();
            f.needsTend = hd.TendableNow();
            immuneRate = 0f;
            otherRate = 0f;
            if (hd is HediffWithComps wc && wc.comps != null)
                foreach (var c in wc.comps)
                {
                    if (c is HediffComp_Immunizable imm)
                    {
                        f.immunizable = true;
                        immuneRate = imm.Props?.severityPerDayImmune ?? 0f;
                    }
                    if (c is HediffComp_Ailment) f.ailment = true;
                    if (c is HediffComp_SeverityModifierBase m)
                        try
                        {
                            float d = m.SeverityChangePerDay();
                            f.severityPerDay += d;
                            if (!(c is HediffComp_Immunizable)) otherRate += d;
                        }
                        catch (Exception)
                        {
                        }
                }
            return f;
        }

        /// <summary>The immunity race from the game's own numbers: the pawn's immunity and how fast it grows, against the severity.</summary>
        private static void Race(Pawn p, Hediff hd, Item item)
        {
            try
            {
                var comp = hd.TryGetComp<HediffComp_Immunizable>();
                if (comp == null) return;
                item.immunity = comp.Immunity;
                float perDay = 0f;
                var rec = p.health.immunity?.GetImmunityRecord(hd.def);
                if (rec != null) perDay = rec.ImmunityChangePerTick(p, true, hd) * GenDate.TicksPerDay;
                item.immunityPerDay = perDay;
                item.race = HudHealthMath.Race(item.immunity, perDay, hd.Severity, item.facts.severityPerDay, hd.def.lethalSeverity);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: immunity race", e);
            }
        }

        /// <summary>
        /// How far along each condition is: the tending and when it wears off, how much of a wound has healed and roughly
        /// when it's healed (the game's healing shared among the wounds), when an illness or an ailment is over.
        /// </summary>
        private void HowFar(Pawn p, HediffSet set)
        {
            // The healing the game gives them a day (Pawn_HealthTracker), shared at random among the wounds.
            bool starving = p.needs?.food?.Starving == true;
            bool lying = false;
            float bed = 0f, stage = 1f, factor = 1f, scale = 1f;
            int natural = 0, tended = 0;
            try
            {
                if (p.Spawned)
                {
                    lying = p.GetPosture() != PawnPosture.Standing;
                    bed = p.CurrentBed()?.def?.building?.bed_healPerDay ?? 0f;
                }
                foreach (var hd in set.hediffs)
                {
                    var cs = hd?.CurStage;
                    if (cs != null && cs.naturalHealingFactor != -1f) stage *= cs.naturalHealingFactor;
                    if (hd is Hediff_Injury inj)
                    {
                        if (inj.CanHealNaturally()) natural++;
                        if (inj.CanHealFromTending()) tended++;
                    }
                }
                factor = p.GetStatValue(StatDefOf.InjuryHealingFactor);
                scale = p.HealthScale;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: healing rate", e);
            }
            float naturalPool = HudHealthMath.NaturalHealPerDay(lying, bed, stage, factor, scale, starving);
            foreach (var it in items)
            {
                var hd = it.hediff;
                it.ageDays = hd.ageTicks / (float)GenDate.TicksPerDay;
                var tend = hd.TryGetComp<HediffComp_TendDuration>();
                if (tend != null && tend.IsTended)
                {
                    it.tendQuality = tend.tendQuality;
                    it.tendTicksLeft = tend.TProps != null && tend.TProps.TendIsPermanent ? -1 : tend.tendTicksLeft;
                }
                float sev = hd.Severity;
                if (hd is Hediff_Injury inj)
                {
                    seenNow.Add(hd);
                    if (!worst.TryGetValue(hd, out float was))
                    {
                        worst[hd] = sev;
                        if (hd.ageTicks < FreshTicks) seenFresh.Add(hd);
                    }
                    else if (sev > was) worst[hd] = sev;
                    if (seenFresh.Contains(hd)) it.healed = HudHealthMath.Healed(worst[hd], sev);
                    float tendedPool = it.tendQuality >= 0f ? HudHealthMath.TendedHealPerDay(it.tendQuality, factor, scale, starving) : 0f;
                    float perDay = HudHealthMath.WoundHealPerDay(naturalPool, natural, inj.CanHealNaturally(), tendedPool, tended, inj.CanHealFromTending());
                    it.daysLeft = HudHealthMath.DaysToHeal(sev, perDay);
                    it.notHealing = starving;
                }
                else if (it.facts.immunizable)
                {
                    if (it.race == HealthRace.Recovering || it.race == HealthRace.Immune)
                        it.daysLeft = HudHealthMath.DaysToRecover(it.immunity, it.immunityPerDay, sev, it.facts.severityPerDay, it.severityPerDayImmune,
                            hd.def.lethalSeverity);
                }
                else
                {
                    var disappears = hd.TryGetComp<HediffComp_Disappears>();
                    if (disappears != null && disappears.ticksToDisappear > 0) it.daysLeft = disappears.ticksToDisappear / (float)GenDate.TicksPerDay;
                    else it.daysLeft = HudHealthMath.DaysToPass(sev, it.facts.severityPerDay);
                }
            }
        }

        /// <summary>The row's right-hand word: what matters most about them, in its colour (with icons and bars, by what it says).</summary>
        private void Status(Row row, float bleedHours, int untended)
        {
            var top = items.Count > 0 ? items[0] : null;
            var status = HudHealthMath.StatusOf(bleedHours >= 0f, top?.score ?? 0f, untended, top != null && top.race == HealthRace.Losing);
            switch (status)
            {
                case HealthStatus.Bleeding:
                    row.status = bleedHours < 48f
                        ? "CC_Pnl_H_BleedingFor".Translate(UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(bleedHours * GenDate.TicksPerHour)))).Resolve()
                        : HudKit.T("CC_Pnl_H_Bleeding");
                    break;
                case HealthStatus.Danger:
                    row.status = HudKit.T("CC_Pnl_H_Danger");
                    break;
                case HealthStatus.Untended:
                    row.status = "CC_Pnl_H_Untended".Translate(untended).Resolve();
                    break;
                case HealthStatus.Losing:
                    row.status = HudKit.T("CC_Pnl_H_Losing");
                    break;
                case HealthStatus.Worsening:
                    row.status = HudKit.T("CC_Pnl_H_Worsening");
                    break;
                default:
                    row.status = HudKit.T("CC_Pnl_H_Healing");
                    break;
            }
            row.statusColor = Visual ? ToneColor(HudHealthMath.ToneOf(status), UiKit.Faint) : HudKit.ColorOf(row.score, UiKit.Faint);
        }

        /// <summary>The game's summary health for the mark by the name.</summary>
        private static void HealthMark(Row row, Pawn p)
        {
            try
            {
                var sh = p.health?.summaryHealth;
                if (sh == null) return;
                row.health = Mathf.Clamp01(sh.SummaryHealthPercent);
                row.healthColor = ToneColor(HudHealthMath.HealthToneOf(row.health), UiKit.Faint);
            }
            catch (Exception e)
            {
                row.health = -1f;
                CouncilLog.ErrorOnce("health panel: summary health", e);
            }
        }

        /// <summary>The icon the game's Health tab shows for a condition (tended well or poorly, needs tending, immune), or its drop of blood while it bleeds.</summary>
        private static void IconOf(Item it)
        {
            if (it.facts.bleedRate > 0f && HudIcons.Bleeding != null)
            {
                it.icon = HudIcons.Bleeding;
                it.iconColor = Color.white;
                return;
            }
            try
            {
                var ic = it.hediff.StateIcon;
                if (!ic.HasValue) return;
                it.icon = ic.Texture;
                it.iconColor = ic.Color;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: a condition's icon (" + it.hediff?.def?.defName + ")", e);
            }
        }

        // ---------------- The words ----------------

        private static string Soon(float days) => HudHealthMath.RoughTime(days, HudKit.T("CC_Pnl_H_Hours"), HudKit.T("CC_Pnl_H_Days"));

        /// <summary>What matters most about one condition, in a few words: untended, the immunity race, how far a wound has healed, the days left.</summary>
        private static string ShortFact(Item it, out Color color)
        {
            var hd = it.hediff;
            color = UiKit.Faint;
            if (it.facts.needsTend)
            {
                color = it.facts.bleedRate > 0f ? UiKit.Bad : UiKit.Warn;
                return HudKit.T("CC_Pnl_H_UntendedShort");
            }
            if (it.facts.immunizable)
            {
                if (it.race == HealthRace.Losing) color = UiKit.Warn;
                return "CC_Pnl_H_ImmunityVs".Translate(it.immunity.ToStringPercent(), hd.Severity.ToStringPercent()).Resolve();
            }
            if (it.facts.injury)
            {
                if (it.healed >= 0f && it.healed >= 0.01f) return "CC_Pnl_H_HealedShare".Translate(it.healed.ToStringPercent()).Resolve();
                if (it.tendQuality >= 0f) return "CC_Pnl_H_TendedQ".Translate(it.tendQuality.ToStringPercent()).Resolve();
                return "CC_Pnl_H_HpLeft".Translate(Mathf.CeilToInt(hd.Severity)).Resolve();
            }
            if (it.facts.Lethal && it.facts.severityPerDay > HudHealthMath.Still)
            {
                color = HudKit.ColorOf(it.score, UiKit.Faint);
                return "CC_Pnl_H_ToLethal".Translate(it.facts.Progress.ToStringPercent()).Resolve();
            }
            if (it.daysLeft >= 0f) return "CC_Pnl_H_LeftShort".Translate(Soon(it.daysLeft)).Resolve();
            if (it.tendQuality >= 0f) return "CC_Pnl_H_TendedQ".Translate(it.tendQuality.ToStringPercent()).Resolve();
            return null;
        }

        /// <summary>
        /// Everything about one condition in short, for its own line: the tending, the healing or the race, the time; in
        /// one colour (<paramref name="color"/>) for the plain look, and split for icons and bars into the tending
        /// (<paramref name="tend"/>, in the colour of its quality) and the rest.
        /// </summary>
        private static string LongFact(Item it, out Color color, out string tend, out Color tendColor, out string rest, out Color restColor)
        {
            var hd = it.hediff;
            var parts = new List<string>();
            color = UiKit.Faint;
            tend = null;
            tendColor = UiKit.Faint;
            restColor = UiKit.Faint;
            if (it.facts.needsTend)
            {
                color = it.facts.bleedRate > 0f ? UiKit.Bad : UiKit.Warn;
                tend = HudKit.T("CC_Pnl_H_UntendedShort");
                tendColor = it.facts.bleedRate > 0f ? UiKit.Bad : Yellow;
            }
            else if (it.tendQuality >= 0f)
            {
                tend = "CC_Pnl_H_TendedQ".Translate(it.tendQuality.ToStringPercent()).Resolve();
                tendColor = ToneColor(HudHealthMath.TendTone(it.tendQuality), UiKit.Faint);
                if (it.tendTicksLeft > 0 && it.tendTicksLeft < GenDate.TicksPerHour * 3)
                {
                    tend += " " + "CC_Pnl_H_WearsOffShort".Translate(Soon(it.tendTicksLeft / (float)GenDate.TicksPerDay)).Resolve();
                    color = UiKit.Warn;
                }
            }
            if (it.facts.immunizable)
            {
                if (it.race == HealthRace.Losing)
                {
                    parts.Add(HudKit.T("CC_Pnl_H_Losing"));
                    color = UiKit.Warn;
                    restColor = UiKit.Warn;
                }
                else if (it.daysLeft >= 0f) parts.Add("CC_Pnl_H_OverIn".Translate(Soon(it.daysLeft)).Resolve());
            }
            else if (it.facts.injury)
            {
                if (it.healed >= 0.01f) parts.Add("CC_Pnl_H_HealedShare".Translate(it.healed.ToStringPercent()).Resolve());
                else if (it.healed < 0f) parts.Add("CC_Pnl_H_HpLeft".Translate(Mathf.CeilToInt(hd.Severity)).Resolve());
                if (it.notHealing) parts.Add(HudKit.T("CC_Pnl_H_NotHealingShort"));
                else if (it.daysLeft > 0f) parts.Add(Soon(it.daysLeft));
            }
            else if (it.facts.Lethal && it.facts.severityPerDay > HudHealthMath.Still)
            {
                parts.Add("CC_Pnl_H_ToLethal".Translate(it.facts.Progress.ToStringPercent()).Resolve());
                color = HudKit.ColorOf(it.score, UiKit.Faint);
                restColor = color;
            }
            else if (it.daysLeft >= 0f) parts.Add("CC_Pnl_H_LeftShort".Translate(Soon(it.daysLeft)).Resolve());
            rest = parts.Count > 0 ? string.Join(", ", parts) : null;
            if (tend == null) return rest ?? "";
            return rest == null ? tend : tend + ", " + rest;
        }

        /// <summary>
        /// One condition's little bar: the immunity race (immunity over the severity), how close to deadly, how far a
        /// wound has healed, how far through its course something that passes is. Without details only the race and the
        /// danger: they're what the condition is.
        /// </summary>
        private static HealthBar BarOf(Item it, bool details, out float a, out float b, out Color color)
        {
            a = 0f;
            b = 0f;
            color = HudKit.BarOk;
            if (it.facts.immunizable)
            {
                a = Mathf.Clamp01(it.immunity);
                b = it.facts.Lethal ? it.facts.Progress : Mathf.Clamp01(it.hediff.Severity);
                return HealthBar.Race;
            }
            if (it.facts.Lethal && !it.facts.injury)
            {
                a = it.facts.Progress;
                color = HudRank.LevelOf(it.score) == HudLevel.Life ? UiKit.Bad : UiKit.Warn;
                return HealthBar.Lethal;
            }
            if (!details) return HealthBar.None;
            float along = HudHealthMath.Along(it.healed, it.ageDays, it.daysLeft);
            if (along < 0f) return HealthBar.None;
            a = along;
            if (it.facts.injury) return HealthBar.Healed;
            color = PassingBar;
            return HealthBar.Passing;
        }

        /// <summary>
        /// The summary's chips, most pressing first: the bleeding (with details), then each condition, the same one in
        /// the same state once with ×N ("Bruise ×2"). In the plain look each is its name and what matters most about it;
        /// with icons and bars it carries the Health tab's icon, a little bar and a short figure (the tending's quality
        /// in its colour, untended, "losing", the time left, how close to deadly). Each chip's tooltip has it in full.
        /// </summary>
        private void Chips(Row row, float bleedHours, float bleed)
        {
            bool details = Details, visual = Visual;
            row.visual = visual;
            int uid = row.pawn.thingIDNumber;
            if (bleedHours >= 0f && details)
                row.chips.Add(new Chip
                {
                    icon = visual ? HudIcons.Bleeding : null,
                    label = "CC_Pnl_H_BleedShort".Translate(bleed.ToStringPercent()).Resolve(),
                    labelColor = UiKit.Bad,
                    tip = BleedTip(bleedHours, bleed),
                    uid = Gen.HashCombineInt(uid, 0x4842)
                });
            if (items.Count == 0) return;
            var keys = new string[items.Count];
            for (int i = 0; i < items.Count; i++) keys[i] = ChipKey(items[i]);
            var groupOf = HudHealthMath.Group(keys, out var counts);
            var members = new List<Item>();
            for (int g = 0; g < counts.Length; g++)
            {
                members.Clear();
                for (int i = 0; i < items.Count; i++)
                    if (groupOf[i] == g)
                        members.Add(items[i]);
                var chip = ChipFor(members, details, visual);
                chip.uid = Gen.HashCombineInt(uid, 0x4843 + g);
                row.chips.Add(chip);
            }
        }

        /// <summary>What makes two conditions one chip: the same condition, tended or not alike, bleeding or not alike.</summary>
        private static string ChipKey(Item it) =>
            it.hediff.def.defName + "|" + (it.facts.needsTend ? "u" : it.tendQuality >= 0f ? "t" : "-") + (it.facts.bleedRate > 0f ? "b" : "");

        /// <summary>One chip for one condition, or the same one several times (the most pressing first).</summary>
        private static Chip ChipFor(List<Item> members, bool details, bool visual)
        {
            var f = members[0];
            int n = members.Count;
            var chip = new Chip();
            string label = visual || details ? f.hediff.def.LabelCap.ToString() : Short(f);
            chip.label = n > 1 ? label + " ×" + n : label;
            chip.labelColor = visual ? HudKit.ColorOf(f.score, LabelQuiet) : UiKit.Faint;
            if (details && !visual) chip.fact = ShortFact(f, out chip.factColor);
            var sb = new StringBuilder();
            foreach (var m in members)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(m.tip);
            }
            chip.tip = sb.ToString();
            if (!visual) return chip;
            chip.icon = f.icon;
            chip.iconColor = f.iconColor;
            chip.bar = BarOf(f, details, out chip.a, out chip.b, out chip.barColor);
            if (n > 1 && (chip.bar == HealthBar.Healed || chip.bar == HealthBar.Passing))
            {
                // Several of the same: how far along they are together.
                float sum = 0f;
                int k = 0;
                foreach (var m in members)
                {
                    float al = HudHealthMath.Along(m.healed, m.ageDays, m.daysLeft);
                    if (al < 0f) continue;
                    sum += al;
                    k++;
                }
                if (k > 0) chip.a = sum / k;
            }
            else if (chip.bar == HealthBar.Lethal)
                foreach (var m in members)
                    chip.a = Mathf.Max(chip.a, m.facts.Progress);
            if (details) Figure(chip, members);
            return chip;
        }

        /// <summary>
        /// The short figure at the end of a chip: untended; for a wound the tending's quality in red, yellow or green
        /// (the worst of several); for an illness "losing" or the time until it's over; how close to deadly; the time left.
        /// </summary>
        private static void Figure(Chip chip, List<Item> members)
        {
            var f = members[0];
            float worstTend = -1f;
            bool untended = false, bleeding = false;
            foreach (var m in members)
            {
                if (m.facts.needsTend) untended = true;
                if (m.facts.bleedRate > 0f) bleeding = true;
                if (m.tendQuality >= 0f && (worstTend < 0f || m.tendQuality < worstTend)) worstTend = m.tendQuality;
            }
            chip.figureColor = UiKit.Faint;
            if (untended)
            {
                chip.figure = HudKit.T("CC_Pnl_H_UntendedShort");
                chip.figureColor = bleeding ? UiKit.Bad : Yellow;
            }
            else if (f.facts.injury)
            {
                if (worstTend >= 0f)
                {
                    chip.figure = worstTend.ToStringPercent();
                    chip.figureColor = ToneColor(HudHealthMath.TendTone(worstTend), UiKit.Faint);
                }
                else if (f.notHealing)
                {
                    chip.figure = HudKit.T("CC_Pnl_H_NotHealingShort");
                    chip.figureColor = UiKit.Warn;
                }
                else if (f.daysLeft > 0f) chip.figure = Soon(f.daysLeft);
            }
            else if (f.facts.immunizable)
            {
                if (f.race == HealthRace.Losing)
                {
                    chip.figure = HudKit.T("CC_Pnl_H_Losing");
                    chip.figureColor = UiKit.Warn;
                }
                else if (f.daysLeft >= 0f) chip.figure = Soon(f.daysLeft);
            }
            else if (f.facts.Lethal)
            {
                chip.figure = (chip.bar == HealthBar.Lethal ? chip.a : f.facts.Progress).ToStringPercent();
                chip.figureColor = HudKit.ColorOf(f.score, UiKit.Faint);
            }
            else if (f.daysLeft >= 0f) chip.figure = Soon(f.daysLeft);
            else if (worstTend >= 0f)
            {
                chip.figure = worstTend.ToStringPercent();
                chip.figureColor = ToneColor(HudHealthMath.TendTone(worstTend), UiKit.Faint);
            }
        }

        /// <summary>One line for each condition (and the bleeding), most pressing first.</summary>
        private void Lines(Row row, float bleedHours, float bleed)
        {
            bool visual = Visual, details = Details;
            row.visual = visual;
            int uid = row.pawn.thingIDNumber;
            if (bleedHours >= 0f)
            {
                string rate = "CC_Pnl_H_BleedRate".Translate(bleed.ToStringPercent()).Resolve();
                row.lines.Add(new Line
                {
                    label = HudKit.T("CC_Pnl_H_BleedingLine"), labelColor = UiKit.Bad, factColor = UiKit.Bad, fact = rate,
                    icon = visual ? HudIcons.Bleeding : null, rest = rate, restColor = UiKit.Bad,
                    tip = BleedTip(bleedHours, bleed), uid = Gen.HashCombineInt(uid, 0x4C42)
                });
            }
            foreach (var it in items)
            {
                if (row.lines.Count >= MaxLines) break;
                var hd = it.hediff;
                string label = hd.def.LabelCap;
                if (hd.Part != null) label += ", " + hd.Part.Label;
                var line = new Line
                {
                    label = label, labelColor = HudKit.ColorOf(it.score, LabelQuiet), tip = it.tip,
                    uid = Gen.HashCombineInt(uid, 0x4C43 + row.lines.Count)
                };
                if (details)
                {
                    line.fact = LongFact(it, out line.factColor, out line.tend, out line.tendColor, out line.rest, out line.restColor);
                    if (it.facts.immunizable)
                    {
                        line.immunity = it.immunity;
                        line.severity = it.facts.Lethal ? it.facts.Progress : Mathf.Clamp01(hd.Severity);
                    }
                }
                if (visual)
                {
                    line.icon = it.icon;
                    line.iconColor = it.iconColor;
                    line.bar = BarOf(it, details, out line.a, out line.b, out line.barColor);
                }
                row.lines.Add(line);
            }
        }

        private static string Short(Item it)
        {
            var hd = it.hediff;
            string label = hd.def.LabelCap;
            if (it.facts.immunizable)
                return "CC_Pnl_H_Race".Translate(label, hd.Severity.ToStringPercent(), it.immunity.ToStringPercent()).Resolve();
            if (it.facts.Lethal && !it.facts.injury) return label + " " + it.facts.Progress.ToStringPercent();
            return label;
        }

        /// <summary>The bleeding in full: how much a day and, without tending, how long they have.</summary>
        private static string BleedTip(float bleedHours, float bleed)
        {
            string b = "CC_Pnl_H_BleedTip".Translate(bleed.ToStringPercent()).Resolve();
            if (bleedHours < 48f) b += " " + "CC_Pnl_H_BleedDeath".Translate(UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(bleedHours * GenDate.TicksPerHour)))).Resolve();
            return b.Colorize(UiKit.Bad);
        }

        /// <summary>One condition in full: where, the tending and when it wears off, the immunity race or the healing, and the time.</summary>
        private static string ItemTip(Item it)
        {
            var hd = it.hediff;
            string line = hd.LabelCap;
            if (hd.Part != null) line += " (" + hd.Part.LabelCap + ")";
            var notes = new List<string>();
            if (it.facts.needsTend) notes.Add(HudKit.T("CC_Pnl_H_NeedsTend"));
            else if (it.tendQuality >= 0f)
            {
                string t = "CC_Pnl_H_TendedTip".Translate(it.tendQuality.ToStringPercent()).Resolve();
                if (it.tendTicksLeft > 0) t += " " + "CC_Pnl_H_WearsOff".Translate(UiKit.Duration(it.tendTicksLeft)).Resolve();
                notes.Add(t);
            }
            else if (it.facts.tended) notes.Add(HudKit.T("CC_Pnl_H_Tended"));
            if (it.facts.immunizable)
            {
                notes.Add("CC_Pnl_H_RaceTip".Translate(hd.Severity.ToStringPercent(), it.immunity.ToStringPercent()).Resolve());
                if (it.race == HealthRace.Recovering || it.race == HealthRace.Immune)
                    notes.Add(it.daysLeft >= 0f ? "CC_Pnl_H_WillRecoverIn".Translate(Days(it.daysLeft)).Resolve() : HudKit.T("CC_Pnl_H_WillRecover"));
                else if (it.race == HealthRace.Losing) notes.Add(HudKit.T("CC_Pnl_H_LosingTip"));
            }
            else if (it.facts.injury)
            {
                if (it.healed >= 0f) notes.Add("CC_Pnl_H_HealedTip".Translate(it.healed.ToStringPercent()).Resolve());
                else notes.Add("CC_Pnl_H_HpLeftTip".Translate(Mathf.CeilToInt(hd.Severity)).Resolve());
                if (it.notHealing) notes.Add(HudKit.T("CC_Pnl_H_NotHealing"));
                else if (it.daysLeft > 0f) notes.Add("CC_Pnl_H_HealsIn".Translate(Days(it.daysLeft)).Resolve());
            }
            else if (it.facts.Lethal && !it.facts.injury) notes.Add("CC_Pnl_H_TowardsDeath".Translate(it.facts.Progress.ToStringPercent()).Resolve());
            else if (it.daysLeft >= 0f) notes.Add("CC_Pnl_H_PassesIn".Translate(Days(it.daysLeft)).Resolve());
            if (notes.Count > 0) line += ": " + string.Join(", ", notes);
            return line.Colorize(HudKit.ColorOf(it.score, new Color(0.85f, 0.85f, 0.85f)));
        }

        /// <summary>The row's tooltip: their health and pain as the Health tab says them, the bleeding, and each condition in full.</summary>
        private string Tip(Pawn p, Row row, float bleedHours, float bleed)
        {
            var sb = new StringBuilder();
            sb.Append(p.LabelShortCap.Colorize(UiKit.HeaderColor));
            if (row.health >= 0f)
            {
                sb.Append("\n").Append("CC_Pnl_H_HealthTip".Translate(row.health.ToStringPercent().Colorize(row.healthColor)).Resolve());
                string pain = Pain(p, out var painColor);
                if (!pain.NullOrEmpty()) sb.Append(" · ").Append("CC_Pnl_H_PainTip".Translate(pain.Colorize(painColor)).Resolve());
            }
            if (bleedHours >= 0f) sb.Append("\n").Append(BleedTip(bleedHours, bleed));
            foreach (var it in items) sb.Append("\n").Append(it.tip);
            return sb.ToString();
        }

        /// <summary>Their pain in the Health tab's own word and colour ("mild"), or null when they feel none.</summary>
        private static string Pain(Pawn p, out Color color)
        {
            color = UiKit.Faint;
            try
            {
                if ((p.health?.hediffSet?.PainTotal ?? 0f) < 0.001f) return null;
                var label = HealthCardUtility.GetPainLabel(p);
                color = label.Second;
                return label.First;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: pain", e);
                return null;
            }
        }

        /// <summary>A time in the game's words ("1.5 days", "9 hours"), at least an hour.</summary>
        private static string Days(float days) => UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(days * GenDate.TicksPerDay)));

        // ---------------- Sizes ----------------

        private bool TwoLines => !St.compact && St.Shows(PartDetails);
        private float RowH(float s) => Mathf.Round((TwoLines ? 32f : 20f) * s);
        private float NameH(float s) => Mathf.Round(19f * s);
        private float LineH(float s) => Mathf.Round(15f * s);
        private float Width(float s) => Mathf.Round((St.compact ? 196f : LineEach && Details ? 276f : 252f) * s);
        /// <summary>A line of chips in the summary.</summary>
        private static float ChipLineH(float s) => Mathf.Round(14f * s);
        private static float IconSize(float s) => Mathf.Max(8f, Mathf.Round(12f * s));
        private static float BarW(float s) => Mathf.Max(10f, Mathf.Round(22f * s));
        private static float PartGap(float s) => Mathf.Max(2f, Mathf.Round(3f * s));

        /// <summary>The room the chips have on a row: the panel's width less the row's inset and margins (as drawn).</summary>
        private float ChipRoom(float s) => Width(s) - 4f - Mathf.Round(4f * s) - Mathf.Round(3f * s);

        private float HeightOf(Row row, float s)
        {
            if (!TwoLines) return RowH(s);
            if (LineEach) return NameH(s) + Mathf.Max(1, row.lines.Count) * LineH(s) + Mathf.Round(3f * s);
            // The name, then as many lines of chips as they needed when last drawn (two at most).
            return Mathf.Round(18f * s) + Mathf.Max(1, row.measuredLines) * ChipLineH(s);
        }

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round(150f * s), pad * 2f + Mathf.Round(20f * s));
            bool chips = TwoLines && !LineEach;
            if (chipLines.Count > 200) chipLines.Clear();
            float h = 0f;
            foreach (var row in rows)
            {
                // Text is measured only while drawing: the chips' lines are the ones they took when last drawn.
                if (chips) row.measuredLines = chipLines.TryGetValue(row.pawn.thingIDNumber, out int n) ? n : 1;
                h += HeightOf(row, s);
            }
            return new Vector2(Width(s), pad * 2f + h + more);
        }

        /// <summary>How many lines each colonist's chips took when last drawn (by thingIDNumber), for the next measure.</summary>
        private readonly Dictionary<int, int> chipLines = new Dictionary<int, int>();

        private static readonly List<float> chipWidths = new List<float>();

        /// <summary>A text's width in a font, a little over so it's never cut.</summary>
        private static float TextW(string text, GameFont font) => text.NullOrEmpty() ? 0f : HudKit.Width(text, font) + 1f;

        /// <summary>
        /// While drawing: lays the row's chips out and, when they need more or fewer lines than the row was measured
        /// for, notes it and gathers again, so the row and the panel grow or shrink on the next frame.
        /// </summary>
        private void FitChips(Row row, float s)
        {
            LayOut(row, s);
            int need = Mathf.Max(1, row.flow.lines);
            if (need == row.measuredLines) return;
            int id = row.pawn.thingIDNumber;
            if (chipLines.TryGetValue(id, out int had) && had == need) return;
            chipLines[id] = need;
            MarkDirty();
        }

        /// <summary>
        /// Measures a row's chips and lays them out on up to two lines, the rest as "+N" (once each time the panel
        /// gathers, or when the scale or the text size changes): with icons and bars the icon, the name, the bar and the
        /// figure; in the plain look the name and its fact, with a dot between chips.
        /// </summary>
        private void LayOut(Row row, float s)
        {
            var font = HudKit.Minor(s);
            if (row.laidScale == s && row.laidFont == font) return;
            row.laidScale = s;
            row.laidFont = font;
            bool visual = row.visual;
            row.space = Mathf.Max(3f, TextW("x x", font) - TextW("xx", font));
            float iconW = IconSize(s) + PartGap(s), barW = BarW(s), partGap = PartGap(s);
            chipWidths.Clear();
            foreach (var c in row.chips)
            {
                c.labelW = TextW(c.label, font);
                c.factW = TextW(c.fact, font);
                c.figureW = TextW(c.figure, font);
                float w = c.labelW;
                if (visual)
                {
                    if (c.icon != null) w += iconW;
                    if (c.bar != HealthBar.None) w += partGap + barW;
                    if (c.figureW > 0f) w += partGap + c.figureW;
                }
                else if (c.factW > 0f) w += row.space + c.factW;
                chipWidths.Add(w);
            }
            row.gap = visual ? Mathf.Round(8f * s) : TextW("·", font) + row.space * 2f;
            row.moreW = TextW("+" + row.chips.Count, font);
            row.flow = HudHealthMath.Flow(chipWidths, ChipRoom(s), row.gap, MaxChipLines, row.moreW, row.spots);
            row.more = row.flow.more > 0 ? "+" + row.flow.more : null;
        }

        // ---------------- Drawing ----------------

        protected override void DrawBody(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            if (rows.Count == 0)
            {
                // A calm colony: one quiet line.
                HudKit.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, Mathf.Round(20f * s)), HudKit.T("CC_Pnl_H_AllWell"), UiKit.Faint, HudKit.Minor(s));
                return;
            }
            var main = HudKit.Main(s);
            var minor = HudKit.Minor(s);
            bool each = TwoLines && LineEach;
            bool chips = TwoLines && !LineEach;
            float line1 = each ? NameH(s) : Mathf.Round(18f * s);
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (chips) FitChips(row, s);
                float rh = HeightOf(row, s);
                var rr = new Rect(r.x + 2f, y, r.width - 4f, rh);
                y += rh;
                var level = HudRank.LevelOf(row.score);
                if (level == HudLevel.Life) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Bad, 0.12f + 0.1f * HudKit.Pulse));
                else if (level == HudLevel.Soon) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Warn, 0.1f));
                else UiKit.Zebra(rr, i);
                if (Over(rr)) Widgets.DrawBoxSolid(rr, UiKit.CardHover);
                float x = rr.x + Mathf.Round(4f * s);
                float right = rr.xMax - Mathf.Round(3f * s);
                float topH = TwoLines ? line1 : rh;
                float topY = rr.y + (TwoLines ? 1f : 0f);
                float sw = Mathf.Min(HudKit.Width(row.status, main) + 4f, rr.width * 0.5f);
                HudKit.Label(new Rect(right - sw, topY, sw, topH), row.status, row.statusColor, main, TextAnchor.MiddleRight);
                float nx = row.visual && row.health >= 0f ? DrawHealthMark(x, topY, topH, row, s) : x;
                HudKit.Label(new Rect(nx, topY, Mathf.Max(0f, right - sw - 4f - nx), topH), row.name, level >= HudLevel.Life ? UiKit.Bad : Color.white, main);
                bool overItem = false;
                if (each) overItem = DrawLines(new Rect(x, rr.y + line1, right - x, rh - line1), row, s);
                else if (chips && row.chips.Count > 0) overItem = DrawChips(new Rect(x, rr.y + line1 - 1f, Mathf.Max(0f, right - x), rh - line1 + 1f), row, s, minor);
                // On a chip or a condition's line its own tooltip shows instead of the whole row's.
                var data = row;
                if (!overItem) Tip(rr, () => data.tip, row.pawn.thingIDNumber ^ 0x48E1);
                if (Clicked(rr)) Click(row.pawn);
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(16f * s));
                MoreLine(mr, moreText, hidden);
            }
        }

        /// <summary>The small health mark by the name: the game's summary health as a little upright bar in its colour; returns where the name starts.</summary>
        private static float DrawHealthMark(float x, float y, float h, Row row, float s)
        {
            float w = Mathf.Max(3f, Mathf.Round(4f * s));
            float mh = Mathf.Max(6f, Mathf.Round(h * 0.6f));
            var r = new Rect(x, Mathf.Round(y + (h - mh) / 2f), w, mh);
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(r, UiKit.MeterBack);
                float fh = Mathf.Max(1f, Mathf.Round(mh * row.health));
                Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - fh, w, fh), row.healthColor);
            }
            return x + w + Mathf.Round(4f * s);
        }

        /// <summary>The summary's chips on their lines, then "+N"; true while the mouse is on a chip (its tooltip shows, not the row's).</summary>
        private bool DrawChips(Rect area, Row row, float s, GameFont font)
        {
            float lh = ChipLineH(s), ch = lh + 1f;
            bool over = false;
            int n = Mathf.Min(row.spots.Count, row.chips.Count);
            for (int i = 0; i < n; i++)
            {
                var spot = row.spots[i];
                // Measured for fewer lines (for this frame only, until it's measured again): what's below waits.
                if (spot.line >= row.measuredLines) break;
                var c = row.chips[i];
                var cr = new Rect(area.x + spot.x, area.y + spot.line * lh, spot.w, ch);
                bool hov = Over(cr);
                if (row.visual)
                {
                    if (hov) Widgets.DrawBoxSolid(new Rect(cr.x - 2f, cr.y, cr.width + 4f, cr.height), UiKit.CardHover);
                    DrawChip(cr, c, s, font);
                }
                else
                {
                    if (i > 0 && row.spots[i - 1].line == spot.line)
                        HudKit.Label(new Rect(cr.x - row.gap + row.space, cr.y, row.gap - row.space, ch), "·", UiKit.Dim, font);
                    HudKit.Label(new Rect(cr.x, cr.y, Mathf.Min(c.labelW, cr.width), ch), c.label, c.labelColor, font);
                    float fx = cr.x + c.labelW + row.space;
                    if (c.factW > 0f && fx < cr.xMax - 4f) HudKit.Label(new Rect(fx, cr.y, Mathf.Min(c.factW, cr.xMax - fx), ch), c.fact, c.factColor, font);
                }
                if (c.tip.NullOrEmpty()) continue;
                if (hov) over = true;
                Tip(cr, new TipSignal(c.tip, c.uid));
            }
            if (row.more != null && row.flow.moreLine < row.measuredLines)
                HudKit.Label(new Rect(area.x + row.flow.moreX, area.y + row.flow.moreLine * lh, row.moreW, ch), row.more, UiKit.Faint, font);
            return over;
        }

        /// <summary>One chip with icons and bars: the Health tab's icon, the name (×N), the little bar, the figure.</summary>
        private static void DrawChip(Rect cr, Chip c, float s, GameFont font)
        {
            float x = cr.x, end = cr.xMax, gap = PartGap(s);
            if (c.icon != null)
            {
                float ic = IconSize(s);
                if (Event.current.type == EventType.Repaint) UiKit.Icon(new Rect(x, Mathf.Round(cr.y + (cr.height - ic) / 2f), ic, ic), c.icon, c.iconColor);
                x += ic + gap;
            }
            float lw = Mathf.Min(c.labelW, Mathf.Max(0f, end - x));
            HudKit.Label(new Rect(x, cr.y, lw, cr.height), c.label, c.labelColor, font);
            x += lw;
            float bw = BarW(s);
            if (c.bar != HealthBar.None && x + gap + bw <= end + 0.5f)
            {
                x += gap;
                DrawBar(new Rect(x, cr.y, bw, cr.height), c.bar, c.a, c.b, c.barColor, s);
                x += bw;
            }
            if (c.figureW > 0f && x + gap < end - 4f)
            {
                x += gap;
                HudKit.Label(new Rect(x, cr.y, Mathf.Min(c.figureW, end - x), cr.height), c.figure, c.figureColor, font);
            }
        }

        /// <summary>A tiny bar: one share in its colour, or the immunity race as two thin bars (immunity green over the severity orange).</summary>
        private static void DrawBar(Rect r, HealthBar kind, float a, float b, Color color, float s)
        {
            if (kind == HealthBar.None || Event.current.type != EventType.Repaint) return;
            float cy = r.y + r.height / 2f;
            if (kind == HealthBar.Race)
            {
                float bh = Mathf.Max(2f, Mathf.Round(3f * s));
                var b1 = new Rect(r.x, Mathf.Round(cy - bh - 0.5f), r.width, bh);
                var b2 = new Rect(r.x, b1.yMax + 1f, r.width, bh);
                Widgets.DrawBoxSolid(b1, UiKit.MeterBack);
                Widgets.DrawBoxSolid(new Rect(b1.x, b1.y, b1.width * Mathf.Clamp01(a), bh), HudKit.BarOk);
                Widgets.DrawBoxSolid(b2, UiKit.MeterBack);
                Widgets.DrawBoxSolid(new Rect(b2.x, b2.y, b2.width * Mathf.Clamp01(b), bh), HudKit.BarBad);
                return;
            }
            float h = Mathf.Max(3f, Mathf.Round(4f * s));
            var bar = new Rect(r.x, Mathf.Round(cy - h / 2f), r.width, h);
            Widgets.DrawBoxSolid(bar, UiKit.MeterBack);
            if (a > 0f) Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, Mathf.Max(1f, bar.width * Mathf.Clamp01(a)), h), color);
        }

        /// <summary>
        /// A line for each condition: its name and how it stands at the right (with icons and bars: the Health tab's icon,
        /// a little bar, the tending in its colour). True while the mouse is on a line (its tooltip shows, not the row's).
        /// </summary>
        private bool DrawLines(Rect area, Row row, float s)
        {
            float lh = LineH(s);
            float indent = Mathf.Round((row.visual ? 2f : 6f) * s);
            float y = area.y;
            bool over = false;
            foreach (var l in row.lines)
            {
                var lr = new Rect(area.x + indent, y, area.width - indent, lh);
                y += lh;
                if (row.visual) DrawLineVisual(lr, l, s);
                else DrawLinePlain(lr, l, s);
                if (l.tip.NullOrEmpty()) continue;
                if (Over(lr)) over = true;
                Tip(lr, new TipSignal(l.tip, l.uid));
            }
            return over;
        }

        /// <summary>The plain line: the name, faint, and how it stands at the right, with the immunity race as two small bars.</summary>
        private static void DrawLinePlain(Rect lr, Line l, float s)
        {
            float lh = lr.height;
            float right = lr.xMax;
            if (!l.fact.NullOrEmpty())
            {
                float fw = Mathf.Min(HudKit.Width(l.fact, GameFont.Tiny) + 2f, lr.width * 0.62f);
                HudKit.Label(new Rect(right - fw, lr.y, fw, lh), l.fact, l.factColor, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= fw + 3f;
            }
            if (l.immunity >= 0f)
            {
                // Immunity (green) over the severity (orange): the race at a glance.
                float bw = Mathf.Round(30f * s);
                DrawBar(new Rect(right - bw, lr.y, bw, lh), HealthBar.Race, l.immunity, l.severity, HudKit.BarOk, s);
                right -= bw + 3f;
            }
            HudKit.Label(new Rect(lr.x, lr.y, Mathf.Max(0f, right - lr.x), lh), l.label, UiKit.WithAlpha(l.labelColor, 0.85f), GameFont.Tiny);
        }

        /// <summary>The line with icons and bars: the icon and the name; at the right the rest of the facts, the tending in its colour, and the little bar.</summary>
        private static void DrawLineVisual(Rect lr, Line l, float s)
        {
            float lh = lr.height;
            float right = lr.xMax;
            if (!l.rest.NullOrEmpty())
            {
                if (l.restW < 0f) l.restW = HudKit.Width(l.rest, GameFont.Tiny) + 2f;
                float w = Mathf.Min(l.restW, lr.width * 0.45f);
                HudKit.Label(new Rect(right - w, lr.y, w, lh), l.rest, l.restColor, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= w + 4f;
            }
            if (!l.tend.NullOrEmpty())
            {
                if (l.tendW < 0f) l.tendW = HudKit.Width(l.tend, GameFont.Tiny) + 2f;
                float w = Mathf.Min(l.tendW, Mathf.Max(0f, right - lr.x) * 0.5f);
                HudKit.Label(new Rect(right - w, lr.y, w, lh), l.tend, l.tendColor, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= w + 4f;
            }
            float bw = Mathf.Round(30f * s);
            if (l.bar != HealthBar.None && right - bw >= lr.x + lr.width * 0.3f)
            {
                DrawBar(new Rect(right - bw, lr.y, bw, lh), l.bar, l.a, l.b, l.barColor, s);
                right -= bw + 4f;
            }
            float ic = Mathf.Min(IconSize(s), lh - 2f);
            if (l.icon != null && Event.current.type == EventType.Repaint) UiKit.Icon(new Rect(lr.x, Mathf.Round(lr.y + (lh - ic) / 2f), ic, ic), l.icon, l.iconColor);
            float x = lr.x + ic + PartGap(s);
            HudKit.Label(new Rect(x, lr.y, Mathf.Max(0f, right - x), lh), l.label, UiKit.WithAlpha(l.labelColor, 0.9f), GameFont.Tiny);
        }

        /// <summary>Left: select them and open their Health tab (their caravan when they're travelling). Right: the panel's menu.</summary>
        private void Click(Pawn p)
        {
            if (Event.current.button == 1)
            {
                OpenMenu();
                return;
            }
            try
            {
                if (p.Spawned || p.CarriedBy != null)
                {
                    CameraJumper.TryJumpAndSelect(p);
                    if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(typeof(ITab_Pawn_Health));
                }
                else HudPeople.Go(p);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: open the health tab", e);
            }
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var st = St;
            options.Add(new FloatMenuOption(HudKit.T(LineEach ? "CC_Pnl_H_StyleSummary" : "CC_Pnl_H_StyleLines"), () =>
            {
                st.style = LineEach ? StyleSummary : StyleLines;
                MarkDirty();
                HudLayout.Save();
            }));
            options.Add(new FloatMenuOption(HudKit.T(st.Shows(PartFacts) ? "CC_Pnl_H_HideFacts" : "CC_Pnl_H_ShowFacts"), () =>
            {
                st.Toggle(PartFacts);
                MarkDirty();
                HudLayout.Save();
            }));
            options.Add(new FloatMenuOption(HudKit.T(st.Shows(PartVisuals) ? "CC_Pnl_H_HideVisuals" : "CC_Pnl_H_ShowVisuals"), () =>
            {
                st.Toggle(PartVisuals);
                MarkDirty();
                HudLayout.Save();
            }));
        }
    }
}
