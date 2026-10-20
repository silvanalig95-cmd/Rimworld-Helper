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
    /// Gathered at most twice a second, like every panel. A click selects them and opens their Health tab; caravans
    /// and other maps are an option.
    /// </summary>
    public class HudHealth : HudPanel
    {
        public const string PartDetails = "details", PartHealing = "healing", PartAilments = "ailments";
        public static readonly string[] Parts = { PartDetails, PartHealing, PartAilments };

        private sealed class Row
        {
            public Pawn pawn;
            public float score;
            public string name, status, details, tip;
            public Color statusColor;
            public int barOrder;
        }

        private sealed class Item
        {
            public Hediff hediff;
            public HediffFacts facts;
            public HealthRace race;
            public float immunity, score;
            public bool lifeThreatening;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly List<Item> items = new List<Item>();
        private int hidden;
        private string moreText;

        public HudHealth() : base(HudLayout.Health)
        {
        }

        // ---------------- Gathering ----------------

        protected override void Refresh()
        {
            rows.Clear();
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
                var f = Facts(hd);
                if (!HudHealthMath.Shows(f)) continue;
                if (f.ailment && !st.Shows(PartAilments)) continue;
                var item = new Item { hediff = hd, facts = f };
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
            float worst = items.Count == 0 ? 0f : items.Max(i => i.score);
            float score = HudHealthMath.PawnScore(hours, worst, untended);
            // Only healing: on the panel unless "those only healing" is switched off.
            if (score < HudRank.SoonFloor && !st.Shows(PartHealing)) return null;
            items.Sort((a, b) => b.score.CompareTo(a.score));
            var row = new Row { pawn = p, score = score, name = p.LabelShortCap };
            string away = HudPeople.Away(p);
            if (away != null) row.name += " (" + away + ")";
            Status(row, hours, untended);
            row.details = Details();
            row.tip = Tip(p, hours, bleed);
            return row;
        }

        private static HediffFacts Facts(Hediff hd)
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
            if (hd is HediffWithComps wc && wc.comps != null)
                foreach (var c in wc.comps)
                {
                    if (c is HediffComp_Immunizable) f.immunizable = true;
                    if (c is HediffComp_Ailment) f.ailment = true;
                    if (c is HediffComp_SeverityModifierBase m)
                        try
                        {
                            f.severityPerDay += m.SeverityChangePerDay();
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
                item.race = HudHealthMath.Race(item.immunity, perDay, hd.Severity, item.facts.severityPerDay, hd.def.lethalSeverity);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health panel: immunity race", e);
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

        /// <summary>Their conditions in short, most pressing first: "Gunshot ×2 · Flu 48%, immunity 62% · Bruise".</summary>
        private string Details()
        {
            var sb = new StringBuilder();
            var seen = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var it in items)
            {
                string text = Short(it);
                if (seen.TryGetValue(text, out int n)) seen[text] = n + 1;
                else
                {
                    seen[text] = 1;
                    order.Add(text);
                }
            }
            foreach (var text in order)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(text);
                if (seen[text] > 1) sb.Append(" ×").Append(seen[text]);
            }
            return sb.ToString();
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

        /// <summary>Each condition in full, the bleeding, and what a click does.</summary>
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
                else if (it.facts.tended) notes.Add(HudKit.T("CC_Pnl_H_Tended"));
                if (it.facts.immunizable)
                {
                    notes.Add("CC_Pnl_H_RaceTip".Translate(hd.Severity.ToStringPercent(), it.immunity.ToStringPercent()).Resolve());
                    if (it.race == HealthRace.Recovering || it.race == HealthRace.Immune) notes.Add(HudKit.T("CC_Pnl_H_WillRecover"));
                    else if (it.race == HealthRace.Losing) notes.Add(HudKit.T("CC_Pnl_H_LosingTip"));
                }
                else if (it.facts.Lethal && !it.facts.injury) notes.Add("CC_Pnl_H_TowardsDeath".Translate(it.facts.Progress.ToStringPercent()).Resolve());
                if (notes.Count > 0) line += ": " + string.Join(", ", notes);
                sb.Append("\n").Append(line.Colorize(HudKit.ColorOf(it.score, new Color(0.85f, 0.85f, 0.85f))));
            }
            sb.Append("\n\n").Append(HudKit.T("CC_Pnl_H_Click").Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        // ---------------- Sizes ----------------

        private bool TwoLines => !St.compact && St.Shows(PartDetails);
        private float RowH(float s) => Mathf.Round((TwoLines ? 32f : 20f) * s);
        private float Width(float s) => Mathf.Round((St.compact ? 196f : 252f) * s);

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round(150f * s), pad * 2f + Mathf.Round(20f * s));
            return new Vector2(Width(s), pad * 2f + rows.Count * RowH(s) + more);
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
            float rh = RowH(s);
            var main = HudKit.Main(s);
            var minor = HudKit.Minor(s);
            float line1 = Mathf.Round(18f * s);
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var level = HudRank.LevelOf(row.score);
                if (level == HudLevel.Life) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Bad, 0.12f + 0.1f * HudKit.Pulse));
                else if (level == HudLevel.Soon) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Warn, 0.1f));
                else UiKit.Zebra(rr, i);
                if (Mouse.IsOver(rr)) Widgets.DrawBoxSolid(rr, UiKit.CardHover);
                float x = rr.x + Mathf.Round(4f * s);
                float right = rr.xMax - Mathf.Round(3f * s);
                float topH = TwoLines ? line1 : rh;
                float sw = Mathf.Min(HudKit.Width(row.status, main) + 4f, rr.width * 0.5f);
                HudKit.Label(new Rect(right - sw, rr.y + (TwoLines ? 1f : 0f), sw, topH), row.status, row.statusColor, main, TextAnchor.MiddleRight);
                HudKit.Label(new Rect(x, rr.y + (TwoLines ? 1f : 0f), Mathf.Max(0f, right - sw - 4f - x), topH), row.name, level >= HudLevel.Life ? UiKit.Bad : Color.white,
                    main);
                if (TwoLines && !row.details.NullOrEmpty())
                    HudKit.Label(new Rect(x, rr.y + line1 - 1f, Mathf.Max(0f, right - x), rh - line1 + 1f), row.details, UiKit.Faint, minor);
                var data = row;
                TooltipHandler.TipRegion(rr, () => data.tip, row.pawn.thingIDNumber ^ 0x48E1);
                if (Widgets.ButtonInvisible(rr, false)) Click(row.pawn);
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y + rows.Count * rh, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(mr, HudKit.T("CC_Pnl_MoreLinesTip"));
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
    }
}
