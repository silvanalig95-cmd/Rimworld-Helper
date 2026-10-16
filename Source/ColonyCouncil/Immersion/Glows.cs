using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The light and smoke of a gathering, drawn on <see cref="VisualTouches"/>' pulse: the fire people gather round
    /// (smoke every couple of seconds, sparks at night), a low warm glow at a grave after dark while someone stands there
    /// remembering, and the small glint of cups raised together at a toast.
    /// </summary>
    /// <remarks>
    /// Whoever is there keeps an entry alive (a gathering refreshes its fire, a visit its grave); an entry nobody refreshes
    /// runs out by itself a few seconds later. At most <see cref="MaxFires"/> fires and <see cref="MaxCandles"/> graves,
    /// one fleck each per pulse at most, only on the map being looked at and only in view. Nothing is saved.
    /// </remarks>
    internal sealed class Glows
    {
        private const int MaxFires = 3;
        private const int MaxCandles = 4;

        /// <summary>The glow is the game's own fire glow, made small and dim and warm: a candle's worth of light.</summary>
        private static readonly Color CandleColor = new Color(1f, 0.74f, 0.42f, 0.45f);
        private static readonly Color CupGlint = new Color(1f, 0.88f, 0.55f, 0.85f);

        private sealed class FireEntry
        {
            public Thing fire;
            public int until;
        }

        private sealed class CandleEntry
        {
            public Map map;
            public IntVec3 cell;
            public int until;
        }

        private readonly List<FireEntry> fires = new List<FireEntry>();
        private readonly List<CandleEntry> candles = new List<CandleEntry>();

        public bool Any => fires.Count > 0 || candles.Count > 0;

        public void Clear()
        {
            fires.Clear();
            candles.Clear();
        }

        public void Fire(Thing fire, int until)
        {
            foreach (var e in fires)
                if (e.fire == fire)
                {
                    e.until = Mathf.Max(e.until, until);
                    return;
                }
            if (fires.Count >= MaxFires) fires.RemoveAt(0);
            fires.Add(new FireEntry { fire = fire, until = until });
        }

        public void Candle(Map map, IntVec3 cell, int until)
        {
            foreach (var e in candles)
                if (e.map == map && e.cell == cell)
                {
                    e.until = Mathf.Max(e.until, until);
                    return;
                }
            if (candles.Count >= MaxCandles) candles.RemoveAt(0);
            candles.Add(new CandleEntry { map = map, cell = cell, until = until });
        }

        /// <summary>One pulse: smoke from each fire every fourth pulse, a glow at each grave every other pulse, after dark.</summary>
        public void Pulse(Map map, CellRect view, int now, int pulses)
        {
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                var e = fires[i];
                if (e.until < now || e.fire == null || e.fire.Destroyed)
                {
                    fires.RemoveAt(i);
                    continue;
                }
                if (pulses % 4 != 1 || e.fire.Map != map || !view.ExpandedBy(3).Contains(e.fire.Position)) continue;
                if (!VisualTouches.FireSmoke(e.fire, map)) fires.RemoveAt(i);
            }
            if (candles.Count == 0) return;
            bool dark = VisualTouches.Dark(map);
            for (int i = candles.Count - 1; i >= 0; i--)
            {
                var e = candles[i];
                if (e.until < now || e.map == null || e.map.Disposed)
                {
                    candles.RemoveAt(i);
                    continue;
                }
                if (!dark || pulses % 2 != 0 || e.map != map || !view.Contains(e.cell)) continue;
                CandleAt(e.cell, map);
            }
        }

        /// <summary>A small, dim, warm glow low over the grave, flickering a little from one to the next.</summary>
        private static void CandleAt(IntVec3 cell, Map map)
        {
            var at = cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.08f, 0.08f), 0f, Rand.Range(-0.2f, -0.05f));
            var data = FleckMaker.GetDataStatic(at, map, FleckDefOf.FireGlow, Rand.Range(0.75f, 0.95f));
            data.instanceColor = CandleColor;
            data.solidTimeOverride = Rand.Range(0.6f, 0.9f);
            map.flecks.CreateFleck(data);
        }

        /// <summary>A toast: a small warm glint over the head of each of them in view, all at once (six at most).</summary>
        public static void Raise(IList<Pawn> who, Map map, CellRect view)
        {
            int made = 0;
            foreach (var p in who)
            {
                if (made >= 6) break;
                if (p == null || !p.Spawned || p.Map != map || !view.Contains(p.Position) || p.Downed || !p.Awake()) continue;
                var at = p.DrawPos + new Vector3(Rand.Range(-0.1f, 0.1f), 0f, 0.62f);
                var data = FleckMaker.GetDataStatic(at, map, FleckDefOf.LightningGlow, Rand.Range(0.42f, 0.55f));
                data.instanceColor = CupGlint;
                data.solidTimeOverride = 0.35f;
                map.flecks.CreateFleck(data);
                made++;
            }
        }
    }
}
