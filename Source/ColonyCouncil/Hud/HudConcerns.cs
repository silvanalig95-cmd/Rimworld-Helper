using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing that matters about a colonist right now, ranked by <see cref="HudRank"/>.</summary>
    public struct HudConcern
    {
        public float score;
        public string text;
        public Texture2D icon;

        public HudConcern(float score, string text, Texture2D icon = null)
        {
            this.score = score;
            this.text = text;
            this.icon = icon;
        }

        public HudLevel Level => HudRank.LevelOf(score);
    }

    /// <summary>
    /// What matters most about a colonist right now, for the hover card: bleeding, a condition that can kill, starving,
    /// a mental break or the break line, downed, hunger and exhaustion, strain, a wish waiting, faith wavering, and the
    /// biggest moods dragging them down with what to do. Each scored by the shared ranking, so the card agrees with the
    /// mood board; the caller shows the top few.
    /// </summary>
    public static class HudConcerns
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static List<HudConcern> For(Pawn p, HudPawn h)
        {
            var list = new List<HudConcern>();
            if (p == null) return list;
            Body(p, list);
            if (h != null) Mind(p, h, list);
            list.Sort((a, b) => b.score.CompareTo(a.score));
            return list;
        }

        private static void Body(Pawn p, List<HudConcern> list)
        {
            var set = p.health?.hediffSet;
            if (set == null) return;
            try
            {
                if (set.BleedRateTotal > 0.01f)
                {
                    int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(p);
                    float hours = ticks / (float)GenDate.TicksPerHour;
                    string text = ticks < GenDate.TicksPerDay * 2
                        ? "CC_Pnl_C_BleedingDeath".Translate(TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, ticks))).Resolve()
                        : "CC_Pnl_C_Bleeding".Translate().Resolve();
                    list.Add(new HudConcern(HudRank.Bleeding(hours), text, Icons.CatHealth));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hover card: bleeding", e);
            }
            try
            {
                Hediff worst = null;
                float worstProgress = -1f;
                foreach (var hd in set.hediffs)
                {
                    if (hd?.def == null || !hd.Visible) continue;
                    var stage = hd.CurStage;
                    if (stage == null || !stage.lifeThreatening || hd.FullyImmune()) continue;
                    float progress = hd.def.lethalSeverity > 0f ? Mathf.Clamp01(hd.Severity / hd.def.lethalSeverity) : 0.5f;
                    if (progress > worstProgress)
                    {
                        worst = hd;
                        worstProgress = progress;
                    }
                }
                if (worst != null)
                    list.Add(new HudConcern(HudRank.Dangerous(worstProgress), "CC_Pnl_C_Dangerous".Translate(worst.LabelCap).Resolve(), Icons.CatHealth));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hover card: dangerous condition", e);
            }
            var food = p.needs?.food;
            if (food != null)
            {
                if (food.Starving)
                {
                    float mal = set.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f;
                    list.Add(new HudConcern(HudRank.Starving(mal), "CC_Pnl_C_Starving".Translate().Resolve(), Icons.CatFood));
                }
                else if (food.CurCategory == HungerCategory.UrgentlyHungry)
                    list.Add(new HudConcern(HudRank.Hungry, "CC_Pnl_C_Hungry".Translate().Resolve(), Icons.CatFood));
            }
            if (p.Downed && !p.InBed() && set.BleedRateTotal <= 0.01f)
                list.Add(new HudConcern(HudRank.Downed, "CC_Pnl_C_Downed".Translate().Resolve(), Icons.Warning));
            var rest = p.needs?.rest;
            if (rest != null && rest.CurCategory == RestCategory.Exhausted)
                list.Add(new HudConcern(HudRank.Exhausted, "CC_Pnl_C_Exhausted".Translate().Resolve(), null));
        }

        private static void Mind(Pawn p, HudPawn h, List<HudConcern> list)
        {
            // The mood: a break, below a line, at it, or falling towards it.
            string moodText;
            if (h.inBreak) moodText = "CC_Pnl_C_InBreak".Translate(h.breakLabel ?? "?").Resolve();
            else if (h.now < h.major) moodText = "CC_Pnl_C_BelowMajor".Translate(h.now.ToStringPercent(), h.major.ToStringPercent()).Resolve();
            else if (h.now < h.minor + HudRank.AtTheLine) moodText = "CC_Pnl_C_AtMinor".Translate(h.now.ToStringPercent(), h.minor.ToStringPercent()).Resolve();
            else if (h.heading < h.minor) moodText = "CC_Pnl_C_Falling".Translate(h.heading.ToStringPercent(), h.minor.ToStringPercent()).Resolve();
            else moodText = "CC_Pnl_C_Close".Translate(h.now.ToStringPercent(), h.minor.ToStringPercent()).Resolve();
            if (h.risk >= HudRank.SoonFloor) list.Add(new HudConcern(h.risk, moodText, h.inBreak ? Icons.Warning : Icons.Moods));
            // Strain.
            if (h.band == StrainBand.Breaking) list.Add(new HudConcern(HudRank.StrainBreaking, HeartView.BandLabel(h.band), Icons.Heart));
            else if (h.band == StrainBand.Worn) list.Add(new HudConcern(HudRank.StrainWorn, HeartView.BandLabel(h.band), Icons.Heart));
            else if (h.band == StrainBand.Strained) list.Add(new HudConcern(HudRank.Strained, HeartView.BandLabel(h.band), Icons.Heart));
            // Their wish.
            var w = h.wish;
            if (w != null)
            {
                int total = Mathf.Max(1, w.expiresTick - w.createdTick);
                float left = Mathf.Clamp01(w.TicksLeft / (float)total);
                list.Add(new HudConcern(HudRank.Wish(left, w.stage), "CC_Pnl_C_Wish".Translate(w.Label, UiKit.Duration(Mathf.Max(0, w.TicksLeft))).Resolve(), Icons.Wishes));
            }
            if (h.wavering) list.Add(new HudConcern(HudRank.FaithWavering, "CC_Pnl_C_Wavering".Translate().Resolve(), Icons.CatIdeology));
            // The moods dragging them down most, with what helps.
            foreach (var d in HudMood.Drags(h.mood, 2))
                if (-d.mood >= 1f)
                    list.Add(new HudConcern(d.score, d.WithAct, d.fixable ? Icons.TrendDown : null));
        }

        /// <summary>What they're doing now, in the game's words ("Hauling steel."), or null.</summary>
        public static string Doing(Pawn p)
        {
            try
            {
                if (p.InMentalState) return p.MentalState?.InspectLine;
                string r = p.jobs?.curDriver?.GetReport();
                return r.NullOrEmpty() ? null : r.CapitalizeFirst();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
