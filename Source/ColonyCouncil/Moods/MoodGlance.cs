using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The top of the Mood tab, at a glance (the player, 9 November 2026: "review the "mood" uI for colonists, so the most
    /// relevant details for strain and mood are visible at first glance, with additional details below that"). Without
    /// scrolling or opening anything it answers:
    /// <list type="bullet">
    /// <item>their mood now and where it is heading, with the break lines (the bar above it and its sentence);</item>
    /// <item>the heart now: strain's band and value and how it moved since the day began, the good side's band, and the
    /// weight under it, in the colours of the colonist bar's heart line; time off under way;</item>
    /// <item>what weighs on them and what lifts them most: the top three each across the game's moods, other mods' and
    /// Rimfolk's, the heart's own two with their causes, each with its size as the list below has it;</item>
    /// <item>what the bands are doing to them right now, in a line each ("Worn down: -8 mood, work -7%, learns 6% slower...");</item>
    /// <item>the top advice with its buttons (Heart/HeartAdvice.cs).</item>
    /// </list>
    /// Built with the tab's model (twice a second at most), from the same lines and the same heart the sections below draw,
    /// so every number matches theirs; only the sentences' heights are measured as drawn, the way the heart's page does.
    /// What happened lately (the "Lately" section below) is built here too.
    /// </summary>
    public sealed class MoodGlance
    {
        public string mood, heart, weight, off, weighs, lifts, doing, doingGood, lately;
        public PawnHeart h;
        public Pawn pawn;

        /// <summary>How many of what weighs and lifts most are named.</summary>
        public const int Top = 3;

        public static MoodGlance For(Pawn p, PawnMood m)
        {
            var g = new MoodGlance { pawn = p, mood = m?.state };
            var h = Heart.ForView(p);
            g.h = h != null && h.evaluated ? h : null;
            if (m?.lines != null)
            {
                g.weighs = Named(p, g.h, m.lines.Where(l => l.mood < 0f).OrderBy(l => l.mood).Take(Top), "CC_Glance_Weighs");
                g.lifts = Named(p, g.h, m.lines.Where(l => l.mood > 0f).OrderByDescending(l => l.mood).Take(Top), "CC_Glance_Lifts");
            }
            if (g.h == null) return g;
            h = g.h;
            int now = Find.TickManager.TicksGame;
            g.heart = HeartLine(p, h);
            float floor = HeartFelt.Floor(h);
            string why = HeartFelt.Why(h);
            if (floor >= 5f)
                g.weight = why == null ? "CC_Glance_Weight".Translate(floor.ToString("0").Named("WEIGHT")).Resolve()
                    : "CC_Glance_WeightWhy".Translate(floor.ToString("0").Named("WEIGHT"), why.Named("WHY")).Resolve();
            g.off = HeartTimeOff.Line(p, h, now);
            string doing = HeartEffects.Doing(h, false);
            if (doing != null) g.doing = "CC_Count_Doing".Translate(HeartView.BandLabel(h.Band).CapitalizeFirst().Named("BAND"), doing.Named("WHAT")).Resolve();
            string good = HeartEffects.Doing(h, true);
            if (good != null) g.doingGood = "CC_Count_Doing".Translate(GoodHeart.BandLabel(GoodHeart.Band(h)).CapitalizeFirst().Named("BAND"), good.Named("WHAT")).Resolve();
            g.lately = Lately(p, h, now);
            return g;
        }

        /// <summary>
        /// "Heart: worn down 61, up 12 since the day began · in good heart 48", strain in its band's colour and the good side
        /// in green, as the colonist bar's heart line draws them; the good side alone while strain is steady and it shows.
        /// </summary>
        public static string HeartLine(Pawn p, PawnHeart h)
        {
            string strain = "CC_Heart_StrainValue".Translate(HeartView.BandLabel(h.Band), h.strain.ToString("0")).Resolve();
            if (h.strainDayAgoTick >= 0)
            {
                int moved = Mathf.RoundToInt(h.strain - h.strainDayAgo);
                if (moved >= 2) strain += ", " + "CC_Glance_Up".Translate(moved).Resolve();
                else if (moved <= -2) strain += ", " + "CC_Glance_Down".Translate(-moved).Resolve();
            }
            var parts = new List<string>(2) { strain.Colorize(HeartView.BandColor(h.Band)) };
            var good = GoodHeart.Band(h);
            if (good != GoodBand.None)
                parts.Add("CC_Heart_StrainValue".Translate(GoodHeart.BandLabel(good), h.good.ToString("0")).Resolve().Colorize(HeartView.GoodColor));
            return "CC_Glance_Heart".Translate(string.Join("  ·  ", parts).Named("WHAT")).Resolve();
        }

        /// <summary>
        /// "Weighing on her most: pain -10, worn down (the plague) -8, hungry -6": each line as the list below has it, the
        /// heart's two with what is behind them. Null with none.
        /// </summary>
        private static string Named(Pawn p, PawnHeart h, IEnumerable<MoodLine> lines, string key)
        {
            var parts = new List<string>(Top);
            foreach (var l in lines)
            {
                string label = l.label + (l.count > 1 ? " ×" + l.count : "");
                string cause = null;
                if (h != null && l.thought is Thought_HeartStrain) cause = HeartView.TopCause(h);
                else if (h != null && l.thought is Thought_HeartGood) cause = GoodHeart.BuiltText(h, 1);
                if (!cause.NullOrEmpty()) label += " (" + cause + ")";
                parts.Add(label + " " + MoodView.Number(l.mood));
            }
            return parts.Count == 0 ? null : key.Translate(p.Named("PAWN"), string.Join(",  ", parts).Named("WHAT")).Resolve();
        }

        /// <summary>
        /// What happened lately, for the section below: strain now against the day's start, the last day off, bad afternoon,
        /// day to themselves, release of the good side and break. Null with nothing.
        /// </summary>
        private static string Lately(Pawn p, PawnHeart h, int now)
        {
            var lines = new List<string>(6);
            if (h.strainDayAgoTick >= 0) lines.Add("CC_Glance_StrainToday".Translate(h.strain.ToString("0").Named("NOW"), h.strainDayAgo.ToString("0").Named("THEN")).Resolve());
            void Ago(int tick, string key, string what = null)
            {
                if (tick < 0 || now - tick > GenDate.TicksPerDay * 30) return;
                lines.Add(key.Translate(GoodHeart.Ago(tick).Named("WHEN"), (what ?? "").Named("WHAT"), p.Named("PAWN")).Resolve());
            }
            Ago(h.lastDayOffTick, "CC_Glance_LastDayOff");
            Ago(h.lastBadDayTick, "CC_Glance_LastBadDay");
            Ago(h.lastEpisodeTick, "CC_Glance_LastEpisode");
            if (!h.releaseWhy.NullOrEmpty()) Ago(h.lastReleaseTick, "CC_Glance_LastRelease", h.releaseWhy);
            if (!h.lastBreak.NullOrEmpty()) Ago(h.lastBreakTick, "CC_Glance_LastBreak", h.lastBreak);
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>
        /// Draws the summary at y (the mood sentence, the heart, what weighs and lifts most, what the bands do, the top advice
        /// with its buttons); returns the height used.
        /// </summary>
        public float Draw(float x, float y, float w)
        {
            float top = y;
            if (!mood.NullOrEmpty()) y += UiKit.Paragraph(x, y, w, mood, Color.white) + 2f;
            if (heart != null)
            {
                float hh = UiKit.Paragraph(x, y, w, heart, Color.white);
                if (h != null && pawn != null) TooltipHandler.TipRegion(new Rect(x, y, w, hh), () => HeartView.StrainTip(pawn, h), 0x6A1A7CE5 ^ pawn.thingIDNumber);
                y += hh + 1f;
            }
            if (weight != null) y += UiKit.Paragraph(x, y, w, weight, MoodView.Faint, GameFont.Tiny) + 1f;
            if (off != null) y += UiKit.Paragraph(x, y, w, off, UiKit.Info, GameFont.Tiny) + 1f;
            if (weighs != null) y += UiKit.Paragraph(x, y, w, weighs, MoodView.BadColor, GameFont.Tiny) + 1f;
            if (lifts != null) y += UiKit.Paragraph(x, y, w, lifts, MoodView.GoodColor, GameFont.Tiny) + 1f;
            if (doing != null && h != null) y += UiKit.Paragraph(x, y, w, doing, HeartView.BandColor(h.Band), GameFont.Tiny) + 1f;
            if (doingGood != null) y += UiKit.Paragraph(x, y, w, doingGood, HeartView.GoodColor, GameFont.Tiny) + 1f;
            // The top advice with its buttons: the day off, the decree to propose, the party to call (built hourly).
            var advice = h != null && pawn != null ? HeartAdvice.For(pawn, h) : null;
            if (advice != null && advice.Count > 0)
            {
                y += 2f;
                y += HeartAdvice.Draw(pawn, advice, x, y, w, 3);
            }
            return y - top + 4f;
        }
    }
}
