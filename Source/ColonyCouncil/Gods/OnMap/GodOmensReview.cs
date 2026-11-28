using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Three more of the gods' own signs (the review of the gods, 28 November 2026: docs/review-gods.md, "Added"), for the
    /// three whose signs were mostly the ones any god may send: Selene's moths at a light, Murmur's water standing higher at
    /// dawn, Helios's shaft of sun on a gray day. Each shows on the map as its words say, is said aloud by who saw it, kept
    /// in the colony's book in their words (CC_GodM_Saw_*), and leaves its own small effect (GodImpactMath.Table). No god is
    /// named and nothing says why.
    /// </summary>
    public static partial class GodOmens
    {
        private static readonly Color MothPale = new Color(0.9f, 0.92f, 1f), WaterBlue = new Color(0.6f, 0.78f, 0.9f), SunGold = new Color(1f, 0.88f, 0.5f);

        /// <summary>Whether one of these can show on the map at all: a lit light for the moths, wet ground or a well for the water.</summary>
        private static bool CanGodR6(Map map, OmenKind k)
        {
            switch (k)
            {
                case OmenKind.Moths: return Lights(map).Count > 0;
                case OmenKind.WaterRisen: return Wet(map).IsValid;
                default: return true;
            }
        }

        /// <summary>Shows one of these now: where it is, and who saw it. False when it can't show this hour.</summary>
        private static bool ShowGodR6(Map map, OmenKind kind, out IntVec3 at, out Pawn speaker)
        {
            at = IntVec3.Invalid;
            speaker = null;
            switch (kind)
            {
                case OmenKind.Moths:
                {
                    // At night, pale moths come to one of the colony's lights, far more than anyone has seen, and stay there.
                    if (!Night(map)) return false;
                    var lit = Lights(map).OrderByDescending(l => Colonists(map, l.Position, 12f)).FirstOrDefault();
                    if (lit == null) return false;
                    at = lit.Position;
                    speaker = Witness(map, at, 12f);
                    if (speaker == null) return false;
                    var mote = DefDatabase<FleckDef>.GetNamedSilentFail(GodFx.OwnMote);
                    for (int i = 0; i < 16; i++)
                        GodFx.Throw(mote, lit.TrueCenter() + new Vector3(Rand.Range(-1.1f, 1.1f), 0f, Rand.Range(-0.8f, 1.2f)), map, Rand.Range(0.12f, 0.22f), MothPale,
                            Rand.Range(-180f, 180f), Rand.Range(0.05f, 0.25f), Rand.Range(-90f, 90f), Rand.Range(1.4f, 2.6f));
                    return true;
                }
                case OmenKind.WaterRisen:
                {
                    // At dawn the water stands higher in the wet ground (or the well is full to the brim), though no rain fell.
                    if (map.weatherManager?.curWeather != null && map.weatherManager.curWeather.rainRate > 0f) return false;
                    at = Wet(map);
                    if (!at.IsValid) return false;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = at + GenRadial.RadialPattern[Rand.Range(0, 21)];
                        if (c.InBounds(map)) FleckMaker.WaterSplash(c.ToVector3Shifted(), map, Rand.Range(0.6f, 1f), Rand.Range(1.5f, 3f));
                    }
                    OwnHappenings.Glow(map, at, WaterBlue, 5, 1.4f, 0.35f);
                    var c0 = at;
                    speaker = Witness(map, at, 30f) ?? map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(c0, 35f))
                        .OrderBy(p => p.Position.DistanceToSquared(c0)).FirstOrDefault();
                    return speaker != null;
                }
                case OmenKind.SunShaft:
                {
                    // On a gray day (rain, fog or cloud), a single shaft of sun comes through onto one place in the open and stays a while.
                    var w = map.weatherManager?.curWeather;
                    if (map.skyManager.CurSkyGlow < 0.3f || w == null || w == WeatherDefOf.Clear) return false;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    if (speaker == null) return false;
                    var from = speaker.Position;
                    at = CellFinder.RandomClosewalkCellNear(from, map, 8, c => c.Standable(map) && !c.Roofed(map));
                    if (!at.IsValid) at = from;
                    foreach (var c in GenRadial.RadialCellsAround(at, 2.5f, true).Where(c => c.InBounds(map) && !c.Roofed(map)))
                        FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(), map, Rand.Range(0.6f, 1f));
                    OwnHappenings.Glow(map, at, SunGold, 10, 1.6f, 0.5f);
                    return true;
                }
            }
            return false;
        }
    }
}
