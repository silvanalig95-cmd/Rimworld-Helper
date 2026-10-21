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
    /// summary line under each name, or one line for each condition. Gathered at most twice a second, like every panel.
    /// A click selects them and opens their Health tab; caravans and other maps are an option.
    /// </summary>
    public class HudHealth : HudPanel
    {
        public const string PartDetails = "details", PartHealing = "healing", PartAilments = "ailments", PartFacts = "facts";
        public static readonly string[] Parts = { PartDetails, PartFacts, PartHealing, PartAilments };

        /// <summary>The rows' styles (HudPanelState.style): a summary line under each name (the default), or a line for each condition.</summary>
        public const string StyleSummary = "summary", StyleLines = "lines";

        /// <summary>A line for each condition shows this many at most (the rest are in the tooltip).</summary>
        private const int MaxLines = 5;

        private sealed class Row
        {
            public Pawn pawn;
            public float score;
            public string name, status, tip;
            public Color statusColor;
            public int barOrder;
            public readonly List<Line> lines = new List<Line>();
            /// <summary>The summary line's pieces: a condition's name and, after it, what matters most about it.</summary>
            public readonly List<(string label, Color labelColor, string fact, Color factColor)> segs = new List<(string, Color, string, Color)>();
        }

        /// <summary>One condition's line in the "a line for each" style.</summary>
        private sealed class Line
        {
            public string label, fact;
            public Color labelColor, factColor;
            public float immunity = -1f, severity = -1f;
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
            Status(row, hours, untended);
            Summary(row, hours, bleed);
            if (LineEach) Lines(row, hours, bleed);
            row.tip = Tip(p, hours, bleed);
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

        /// <summary>The row's right-hand word: what matters most about them, in its colour.</summary>
        private void Status(Row row, float bleedHours, int untended)
        {
            var top = items.Count > 0 ? items[0] : null;
            if (bleedHours >= 0f)
            {
                row.status = bleedHours < 48f
                    ? "CC_Pnl_H_BleedingFor".Translate(UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(bleedHours * GenDate.TicksPerHour)))).Resolve()
                    : HudKit.T("CC_Pnl_H_Bleeding");
            }
            else if (top != null && top.score >= HudRank.LifeFloor) row.status = HudKit.T("CC_Pnl_H_Danger");
            else if (untended > 0) row.status = "CC_Pnl_H_Untended".Translate(untended).Resolve();
            else if (top != null && top.race == HealthRace.Losing) row.status = HudKit.T("CC_Pnl_H_Losing");
            else if (top != null && top.score >= HudRank.SoonFloor) row.status = HudKit.T("CC_Pnl_H_Worsening");
            else row.status = HudKit.T("CC_Pnl_H_Healing");
            row.statusColor = HudKit.ColorOf(row.score, UiKit.Faint);
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

        /// <summary>Everything about one condition in short, for its own line: the tending, the healing or the race, the time.</summary>
        private static string LongFact(Item it, out Color color)
        {
            var hd = it.hediff;
            var parts = new List<string>();
            color = UiKit.Faint;
            if (it.facts.needsTend)
            {
                color = it.facts.bleedRate > 0f ? UiKit.Bad : UiKit.Warn;
                parts.Add(HudKit.T("CC_Pnl_H_UntendedShort"));
            }
            else if (it.tendQuality >= 0f)
            {
                string t = "CC_Pnl_H_TendedQ".Translate(it.tendQuality.ToStringPercent()).Resolve();
                if (it.tendTicksLeft > 0 && it.tendTicksLeft < GenDate.TicksPerHour * 3)
                {
                    t += " " + "CC_Pnl_H_WearsOffShort".Translate(Soon(it.tendTicksLeft / (float)GenDate.TicksPerDay)).Resolve();
                    color = UiKit.Warn;
                }
                parts.Add(t);
            }
            if (it.facts.immunizable)
            {
                if (it.race == HealthRace.Losing)
                {
                    parts.Add(HudKit.T("CC_Pnl_H_Losing"));
                    color = UiKit.Warn;
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
            }
            else if (it.daysLeft >= 0f) parts.Add("CC_Pnl_H_LeftShort".Translate(Soon(it.daysLeft)).Resolve());
            return string.Join(", ", parts);
        }

        /// <summary>
        /// The line under their name, most pressing first, as pieces in their colours: with details, each condition and
        /// what matters most about it ("Cut 60% healed · Flu immunity 62% vs 40% · Lice untended"), the bleeding first;
        /// without, the conditions' names. The same said twice ("Bruise 4 HP left") counts once with ×2.
        /// </summary>
        private void Summary(Row row, float bleedHours, float bleed)
        {
            var segs = row.segs;
            if (bleedHours >= 0f && Details) segs.Add(("CC_Pnl_H_BleedShort".Translate(bleed.ToStringPercent()).Resolve(), UiKit.Bad, null, UiKit.Bad));
            var counts = new Dictionary<string, int>();
            var order = new List<(string label, Color labelColor, string fact, Color factColor)>();
            foreach (var it in items)
            {
                string label, fact = null;
                var factColor = UiKit.Faint;
                if (Details)
                {
                    label = it.hediff.def.LabelCap;
                    fact = ShortFact(it, out factColor);
                }
                else label = Short(it);
                string key = label + "|" + fact;
                if (counts.TryGetValue(key, out int n)) counts[key] = n + 1;
                else
                {
                    counts[key] = 1;
                    order.Add((label, UiKit.Faint, fact, factColor));
                }
            }
            foreach (var o in order)
            {
                int n = counts[o.label + "|" + o.fact];
                segs.Add(n > 1 && o.fact == null ? (o.label + " ×" + n, o.labelColor, null, o.factColor) : o);
                if (n > 1 && o.fact != null) segs[segs.Count - 1] = (o.label, o.labelColor, o.fact + " ×" + n, o.factColor);
            }
        }

        /// <summary>One line for each condition (and the bleeding), most pressing first.</summary>
        private void Lines(Row row, float bleedHours, float bleed)
        {
            if (bleedHours >= 0f)
                row.lines.Add(new Line
                {
                    label = HudKit.T("CC_Pnl_H_BleedingLine"), labelColor = UiKit.Bad, factColor = UiKit.Bad,
                    fact = "CC_Pnl_H_BleedRate".Translate(bleed.ToStringPercent()).Resolve()
                });
            foreach (var it in items)
            {
                if (row.lines.Count >= MaxLines) break;
                var hd = it.hediff;
                string label = hd.def.LabelCap;
                if (hd.Part != null) label += ", " + hd.Part.Label;
                var line = new Line { label = label, labelColor = HudKit.ColorOf(it.score, new Color(0.85f, 0.85f, 0.85f)) };
                if (Details)
                {
                    line.fact = LongFact(it, out line.factColor);
                    if (it.facts.immunizable)
                    {
                        line.immunity = it.immunity;
                        line.severity = it.facts.Lethal ? it.facts.Progress : Mathf.Clamp01(hd.Severity);
                    }
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

        /// <summary>Each condition in full, and the bleeding.</summary>
        private string Tip(Pawn p, float bleedHours, float bleed)
        {
            var sb = new StringBuilder();
            sb.Append(p.LabelShortCap.Colorize(UiKit.HeaderColor));
            if (bleedHours >= 0f)
            {
                string b = "CC_Pnl_H_BleedTip".Translate(bleed.ToStringPercent()).Resolve();
                if (bleedHours < 48f) b += " " + "CC_Pnl_H_BleedDeath".Translate(UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(bleedHours * GenDate.TicksPerHour)))).Resolve();
                sb.Append("\n").Append(b.Colorize(UiKit.Bad));
            }
            foreach (var it in items)
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
                sb.Append("\n").Append(line.Colorize(HudKit.ColorOf(it.score, new Color(0.85f, 0.85f, 0.85f))));
            }
            return sb.ToString();
        }

        /// <summary>A time in the game's words ("1.5 days", "9 hours"), at least an hour.</summary>
        private static string Days(float days) => UiKit.Duration(Mathf.Max(GenDate.TicksPerHour, (int)(days * GenDate.TicksPerDay)));

        // ---------------- Sizes ----------------

        private bool TwoLines => !St.compact && St.Shows(PartDetails);
        private float RowH(float s) => Mathf.Round((TwoLines ? 32f : 20f) * s);
        private float NameH(float s) => Mathf.Round(19f * s);
        private float LineH(float s) => Mathf.Round(15f * s);
        private float Width(float s) => Mathf.Round((St.compact ? 196f : LineEach && Details ? 276f : 252f) * s);

        private float HeightOf(Row row, float s)
        {
            if (!TwoLines || !LineEach) return RowH(s);
            return NameH(s) + Mathf.Max(1, row.lines.Count) * LineH(s) + Mathf.Round(3f * s);
        }

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round(150f * s), pad * 2f + Mathf.Round(20f * s));
            float h = 0f;
            foreach (var row in rows) h += HeightOf(row, s);
            return new Vector2(Width(s), pad * 2f + h + more);
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
            float line1 = each ? NameH(s) : Mathf.Round(18f * s);
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
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
                float sw = Mathf.Min(HudKit.Width(row.status, main) + 4f, rr.width * 0.5f);
                HudKit.Label(new Rect(right - sw, rr.y + (TwoLines ? 1f : 0f), sw, topH), row.status, row.statusColor, main, TextAnchor.MiddleRight);
                HudKit.Label(new Rect(x, rr.y + (TwoLines ? 1f : 0f), Mathf.Max(0f, right - sw - 4f - x), topH), row.name, level >= HudLevel.Life ? UiKit.Bad : Color.white,
                    main);
                if (each) DrawLines(new Rect(x, rr.y + line1, right - x, rh - line1), row, s);
                else if (TwoLines && row.segs.Count > 0) DrawSummary(new Rect(x, rr.y + line1 - 1f, Mathf.Max(0f, right - x), rh - line1 + 1f), row, minor);
                var data = row;
                Tip(rr, () => data.tip, row.pawn.thingIDNumber ^ 0x48E1);
                if (Clicked(rr)) Click(row.pawn);
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                Tip(mr, HudKit.T("CC_Pnl_MoreLinesTip"));
            }
        }

        /// <summary>The summary line: the pieces one after another in their colours, the last cut short where the room ends.</summary>
        private static void DrawSummary(Rect r, Row row, GameFont font)
        {
            float x = r.x, sep = HudKit.Width(" · ", font), space = HudKit.Width(" ", font);
            for (int i = 0; i < row.segs.Count && x < r.xMax - 8f; i++)
            {
                var g = row.segs[i];
                if (i > 0)
                {
                    HudKit.Label(new Rect(x, r.y, sep, r.height), " · ", UiKit.Dim, font);
                    x += sep;
                }
                float lw = HudKit.Width(g.label, font);
                HudKit.Label(new Rect(x, r.y, Mathf.Min(lw + 1f, r.xMax - x), r.height), g.label, g.labelColor, font);
                x += lw;
                if (g.fact.NullOrEmpty() || x + space >= r.xMax - 8f) continue;
                x += space;
                float fw = HudKit.Width(g.fact, font);
                HudKit.Label(new Rect(x, r.y, Mathf.Min(fw + 1f, r.xMax - x), r.height), g.fact, g.factColor, font);
                x += fw;
            }
        }

        /// <summary>A line for each condition: its name, faint, and how it stands at the right, with the immunity race as two small bars.</summary>
        private void DrawLines(Rect area, Row row, float s)
        {
            float lh = LineH(s);
            float y = area.y;
            foreach (var l in row.lines)
            {
                var lr = new Rect(area.x + Mathf.Round(6f * s), y, area.width - Mathf.Round(6f * s), lh);
                y += lh;
                float right = lr.xMax;
                if (!l.fact.NullOrEmpty())
                {
                    float fw = Mathf.Min(HudKit.Width(l.fact, GameFont.Tiny) + 2f, lr.width * 0.62f);
                    HudKit.Label(new Rect(right - fw, lr.y, fw, lh), l.fact, l.factColor, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= fw + 3f;
                }
                if (l.immunity >= 0f && Event.current.type == EventType.Repaint)
                {
                    // Immunity (green) over the severity (orange): the race at a glance.
                    float bw = Mathf.Round(30f * s), bh = Mathf.Max(2f, Mathf.Round(3f * s));
                    var b1 = new Rect(right - bw, lr.y + lh / 2f - bh - 0.5f, bw, bh);
                    var b2 = new Rect(right - bw, lr.y + lh / 2f + 0.5f, bw, bh);
                    Widgets.DrawBoxSolid(b1, UiKit.MeterBack);
                    Widgets.DrawBoxSolid(new Rect(b1.x, b1.y, b1.width * Mathf.Clamp01(l.immunity), bh), HudKit.BarOk);
                    Widgets.DrawBoxSolid(b2, UiKit.MeterBack);
                    Widgets.DrawBoxSolid(new Rect(b2.x, b2.y, b2.width * Mathf.Clamp01(l.severity), bh), HudKit.BarBad);
                    right -= bw + 3f;
                }
                HudKit.Label(new Rect(lr.x, lr.y, Mathf.Max(0f, right - lr.x), lh), l.label, UiKit.WithAlpha(l.labelColor, 0.85f), GameFont.Tiny);
            }
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
        }
    }
}
