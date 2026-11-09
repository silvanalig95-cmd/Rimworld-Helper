using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's row shows in Rimfolk's own look beyond the classic (Hud/Look/): their mood in words from a table
    /// made once, the heart's strain and good side with its tooltip, a day off or a bad afternoon under way, and an empty
    /// slot in their weapon kit read from the colony's kept choice (Weapons/WeaponPick.Current: never made here, read again
    /// only when it was made again). Gathered with the rest of the row, at most twice a second, and the words made only
    /// when what they say changed; nothing here runs while drawing.
    /// </summary>
    public static class HudLookFacts
    {
        private const float Every = 0.45f;

        private static readonly string[] percents = new string[101];

        /// <summary>A share as the game writes it ("42%"), from a table made once: no words made per frame.</summary>
        public static string Percent(float v)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(v * 100f), 0, 100);
            return percents[i] ??= (i / 100f).ToStringPercent();
        }

        private sealed class Kept
        {
            public int strain = -1, good = -1, band = -1, off = -1, offHour = -1;
            public bool goodOn;
            public string heartTip, offTip;
            public float kitMade = -1f;
            public bool kitGap;
            public string kitTip, kitWords;
        }

        private static readonly Dictionary<int, Kept> kept = new Dictionary<int, Kept>();

        /// <summary>A new or loaded game: the last one's words are let go.</summary>
        public static void Reset() => kept.Clear();

        /// <summary>The row's facts in the look, gathered again when they are older than half a second.</summary>
        public static void Of(HudPawn h)
        {
            if (h?.pawn == null) return;
            float t = Time.realtimeSinceStartup;
            if (t - h.lookAt < Every && t >= h.lookAt && h.nowText != null) return;
            h.lookAt = t;
            var p = h.pawn;
            h.thing = ThingRefs.Of(p);
            h.nowText = Percent(h.now);
            h.name = p.LabelShortCap;
            h.initials = HudKit.Initials(p);
            var data = h;
            h.tipLook ??= () => HudMood.Tip(data);
            h.breakTip = h.inBreak ? "CC_Pnl_C_InBreak".Translate(h.breakLabel ?? "?").Resolve() : null;
            h.bandTip = h.band >= StrainBand.Worn ? HeartView.BandLabel(h.band) : null;
            h.wishTip = h.wish != null ? "CC_Pnl_WishMark".Translate(h.wish.Label ?? "").Resolve() : null;
            h.moodTabTip ??= "CC_Pnl_MoodTabTip".Translate(p.LabelShort).Resolve();
            if (kept.Count > 200) kept.Clear();
            if (!kept.TryGetValue(p.thingIDNumber, out var k))
            {
                k = new Kept();
                kept[p.thingIDNumber] = k;
            }
            try
            {
                HeartFacts(h, p, k);
            }
            catch (Exception e)
            {
                h.heartOn = false;
                h.off = TimeOffKind.None;
                CouncilLog.ErrorOnce("on-screen panels: the heart's line", e);
            }
            try
            {
                Kit(h, p, k);
            }
            catch (Exception e)
            {
                h.kitGap = false;
                CouncilLog.ErrorOnce("on-screen panels: the weapon kit's mark", e);
            }
        }

        private static void HeartFacts(HudPawn h, Pawn p, Kept k)
        {
            var heart = Heart.ForView(p);
            h.heartOn = heart != null && Heart.Eligible(p);
            h.off = TimeOffKind.None;
            h.offTip = null;
            if (!h.heartOn) return;
            h.strain = heart.strain;
            h.good = heart.good;
            h.goodOn = GoodHeart.On;
            int strain = Mathf.RoundToInt(heart.strain), good = Mathf.RoundToInt(heart.good), band = (int)h.band;
            if (k.heartTip == null || k.strain != strain || k.good != good || k.band != band || k.goodOn != h.goodOn)
            {
                k.strain = strain;
                k.good = good;
                k.band = band;
                k.goodOn = h.goodOn;
                string tip = "CC_Meter_Strain".Translate(HeartView.BandLabel(h.band).Named("BAND"), strain.ToString().Named("VALUE")).Resolve();
                if (h.goodOn) tip += "\n" + "CC_Meter_Good".Translate(GoodHeart.BandLabel(GoodHeart.Band(heart)).Named("BAND"), good.ToString().Named("VALUE")).Resolve();
                k.heartTip = tip;
            }
            h.heartTip = k.heartTip;
            h.off = HeartTimeOff.KindOf(heart);
            if (h.off == TimeOffKind.None) return;
            int now = Find.TickManager.TicksGame;
            int hour = now / GenDate.TicksPerHour;
            if (k.offTip == null || k.off != (int)h.off || k.offHour != hour)
            {
                k.off = (int)h.off;
                k.offHour = hour;
                k.offTip = HeartTimeOff.UntilText(p, heart, now) ?? HudKit.T(h.off == TimeOffKind.BadDay ? "CC_Pnl_Look_BadDay" : "CC_Pnl_Look_DayOff");
            }
            h.offTip = k.offTip;
        }

        /// <summary>An empty slot in their kit, as the colony's choice kept for their map last saw it (read again only when it was made again).</summary>
        private static void Kit(HudPawn h, Pawn p, Kept k)
        {
            h.kitGap = false;
            h.kitTip = null;
            h.kitWords = null;
            var map = p.Spawned ? p.Map : null;
            var picks = map != null ? WeaponPick.Current(map) : null;
            if (picks == null) return;
            if (k.kitMade != picks.made)
            {
                k.kitMade = picks.made;
                k.kitGap = false;
                k.kitTip = null;
                k.kitWords = null;
                var kit = picks.Kit(p);
                string gaps = null;
                if (kit != null)
                    foreach (var v in kit)
                    {
                        if (v.State != KitState.Missing) continue;
                        string words = WeaponPick.GapWords(v.Slot);
                        gaps = gaps == null ? words : gaps + ", " + words.UncapitalizeFirst();
                    }
                if (gaps != null)
                {
                    k.kitGap = true;
                    k.kitWords = gaps;
                    k.kitTip = "CC_Pnl_Look_KitTip".Translate(gaps.Named("GAP")).Resolve();
                }
            }
            h.kitGap = k.kitGap;
            h.kitTip = k.kitTip;
            h.kitWords = k.kitWords;
        }
    }
}
