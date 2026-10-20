using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The mood dragging a colonist down most, as the panels show it: "-8 No bedroom", and what to do about it.</summary>
    public sealed class HudDrag
    {
        public string label;
        public float mood;
        /// <summary>The mood hint's short action ("Give them a bedroom"), or null.</summary>
        public string act;
        /// <summary>Something can be done about it now (the Mood tab's "You can fix"); otherwise it passes, or is who they are.</summary>
        public bool fixable;
        public float score;

        /// <summary>"-8 No bedroom".</summary>
        public string Short => HudKit.Signed(mood) + " " + label;

        /// <summary>"-8 No bedroom → Give them a bedroom", or just the short form.</summary>
        public string WithAct => act.NullOrEmpty() ? Short : Short + "  → " + act;
    }

    /// <summary>What the mood board and the hover card know about one colonist, gathered at most twice a second.</summary>
    public sealed class HudPawn
    {
        public Pawn pawn;
        /// <summary>Their moods line by line (MoodInsight), read again every couple of seconds.</summary>
        public PawnMood mood;
        public float now, heading, minor = 0.35f, major = 0.2f, extreme = 0.05f;
        public bool inBreak;
        public string breakLabel;
        public StrainBand band;
        public Wish wish;
        public bool wavering;
        public HudDrag top;
        /// <summary>The shared ranking of where their mood stands (HudRank.MoodRisk).</summary>
        public float risk;
        public float at = -10f, linesAt = -10f;
        public string tip;
        public int barOrder = int.MaxValue;
    }

    /// <summary>
    /// The colonists' moods for the on-screen panels: a cache per colonist (the levels and their state twice a second
    /// at most, the mood lines every two seconds), whom a panel lists, the biggest drag on a mood
    /// (<see cref="TopDrag"/>), and the row's tooltip.
    /// </summary>
    public static class HudMood
    {
        private static readonly Dictionary<int, HudPawn> cache = new Dictionary<int, HudPawn>();
        private const float LevelsEvery = 0.45f, LinesEvery = 2f;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static void Reset() => cache.Clear();

        /// <summary>A colonist's mood for the panels, read again when it's older than half a second.</summary>
        public static HudPawn For(Pawn p)
        {
            if (p == null) return null;
            float t = Time.realtimeSinceStartup;
            if (!cache.TryGetValue(p.thingIDNumber, out var h) || h.pawn != p)
            {
                if (cache.Count > 80) cache.Clear();
                h = new HudPawn { pawn = p };
                cache[p.thingIDNumber] = h;
            }
            if (t - h.at < LevelsEvery && t >= h.at) return h;
            h.at = t;
            Read(h, t);
            return h;
        }

        private static void Read(HudPawn h, float t)
        {
            var p = h.pawn;
            if (h.mood == null || t - h.linesAt >= LinesEvery || t < h.linesAt)
            {
                // Spread the colonists' line reading over the two seconds, so they don't all fall on one frame.
                h.linesAt = t - (p.thingIDNumber % 7) * 0.05f;
                h.mood = MoodInsight.MoodOf(p);
                h.top = TopDrag(h.mood);
                h.tip = null;
            }
            var need = p.needs?.mood;
            if (need != null)
            {
                h.now = need.CurLevelPercentage;
                h.heading = h.now;
                try
                {
                    h.heading = Mathf.Clamp01(need.CurInstantLevel);
                }
                catch
                {
                }
            }
            if (h.mood != null)
            {
                h.minor = h.mood.minor;
                h.major = h.mood.major;
                h.extreme = h.mood.extreme;
            }
            h.inBreak = p.InMentalState;
            h.breakLabel = h.inBreak ? p.MentalStateDef?.label : null;
            var heart = Heart.ForView(p);
            h.band = heart?.Band ?? StrainBand.Steady;
            h.wavering = Faiths.Wavering(p);
            var comp = CouncilGameComponent.Instance;
            h.wish = null;
            if (comp != null && S.enableWishes)
                foreach (var w in comp.wishes)
                    if (w.state == WishState.Open && w.pawn == p)
                    {
                        h.wish = w;
                        break;
                    }
            h.risk = HudRank.MoodRisk(h.now, h.heading, h.minor, h.major, h.extreme, h.inBreak, S.hudMoodMargin);
        }

        /// <summary>
        /// The mood dragging them down most, by the shared ranking (<see cref="HudRank.Drag"/>): something that can be done
        /// about now counts in full, a mood that passes on its own for less, so "-8 No bedroom" comes before a fading
        /// "-6 Saw a corpse" and "-2 Ate raw food". With its hint's short action where the Mood tab would show one. Null
        /// when nothing drags them down.
        /// </summary>
        public static HudDrag TopDrag(PawnMood m)
        {
            if (m?.lines == null) return null;
            HudDrag best = null;
            foreach (var l in m.lines)
            {
                if (l.mood >= -0.05f) continue;
                MoodGroup g;
                try
                {
                    g = MoodGroups.GroupOf(l);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("on-screen panels: mood group", e);
                    g = MoodGroup.Pass;
                }
                bool fixable = g == MoodGroup.Fix;
                float score = HudRank.Drag(-l.mood, fixable);
                if (best != null && score <= best.score) continue;
                string act = null;
                var hint = MoodInsight.HintFor(l.def);
                if (hint != null && !hint.fix.NullOrEmpty() && !hint.act.NullOrEmpty() && MoodSortMath.ShowsAct(g, true, l.ticksLeft)) act = hint.act;
                best = new HudDrag { label = l.label, mood = l.mood, act = act, fixable = fixable, score = score };
            }
            return best;
        }

        /// <summary>The biggest few drags, by the same ranking (the hover card shows two).</summary>
        public static List<HudDrag> Drags(PawnMood m, int count)
        {
            var list = new List<HudDrag>();
            if (m?.lines == null) return list;
            foreach (var l in m.lines)
            {
                if (l.mood >= -0.05f) continue;
                var g = MoodGroups.GroupOf(l);
                bool fixable = g == MoodGroup.Fix;
                var hint = MoodInsight.HintFor(l.def);
                string act = hint != null && !hint.fix.NullOrEmpty() && !hint.act.NullOrEmpty() && MoodSortMath.ShowsAct(g, true, l.ticksLeft) ? hint.act : null;
                list.Add(new HudDrag { label = l.label, mood = l.mood, act = act, fixable = fixable, score = HudRank.Drag(-l.mood, fixable) });
            }
            return list.OrderByDescending(d => d.score).Take(count).ToList();
        }

        /// <summary>The row's tooltip: where their mood stands and their five biggest moods with their size.</summary>
        public static string Tip(HudPawn h)
        {
            if (h.tip != null) return h.tip;
            var sb = new StringBuilder();
            sb.Append(h.pawn.LabelShortCap.Colorize(UiKit.HeaderColor));
            if (h.mood?.state != null) sb.Append("\n").Append(h.mood.state);
            var lines = h.mood?.lines;
            if (lines != null && lines.Count > 0)
            {
                sb.Append("\n");
                foreach (var l in lines.OrderByDescending(x => Mathf.Abs(x.mood)).Take(5))
                {
                    string n = MoodView.Number(l.mood);
                    sb.Append("\n").Append(n.Colorize(MoodView.ColorFor(l.mood))).Append("  ").Append(l.label);
                }
                if (lines.Count > 5) sb.Append("\n").Append("CC_Pnl_AndMore".Translate(lines.Count - 5).Resolve().Colorize(ColoredText.SubtleGrayColor));
            }
            if (h.top != null && !h.top.act.NullOrEmpty())
                sb.Append("\n\n").Append("CC_Pnl_WhatHelps".Translate(h.top.label, h.top.act).Resolve());
            sb.Append("\n\n").Append("CC_Pnl_RowClick".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));
            h.tip = sb.ToString();
            return h.tip;
        }

        // ---------------- Whom a panel lists ----------------

        /// <summary>The people a panel lists, with a mood and an inner life, each once.</summary>
        public static List<Pawn> Who(HudWho who)
        {
            var list = new List<Pawn>();
            if (who == HudWho.Home)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome) continue;
                    foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                        if (Fits(p) && !list.Contains(p))
                            list.Add(p);
                }
                return list;
            }
            foreach (var p in WeekModel.Colonists())
                if (Fits(p) && !list.Contains(p))
                    list.Add(p);
            if (who == HudWho.WithPrisoners)
                foreach (var map in Find.Maps)
                {
                    if (map == null) continue;
                    foreach (var p in map.mapPawns.PrisonersOfColonySpawned.ToList())
                        if (Fits(p) && !list.Contains(p))
                            list.Add(p);
                }
            return list;
        }

        private static bool Fits(Pawn p) => p != null && !p.Dead && p.needs?.mood != null && !p.IsQuestLodger() && People.HasInnerLife(p);

        /// <summary>Each colonist's place in the colonist bar (those not in it go last).</summary>
        public static Dictionary<Pawn, int> BarOrder()
        {
            var order = new Dictionary<Pawn, int>();
            try
            {
                var bar = Find.ColonistBar?.GetColonistsInOrder();
                if (bar != null)
                    for (int i = 0; i < bar.Count; i++)
                        if (bar[i] != null && !order.ContainsKey(bar[i]))
                            order[bar[i]] = i;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panels: colonist bar order", e);
            }
            return order;
        }
    }
}
