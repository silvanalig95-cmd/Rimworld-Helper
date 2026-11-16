using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own look where it acts (the player, 15 November 2026: "i really like how the gods event appear, also with
    /// the sound, visual effect and camera centering! if there's other similarly impressive visual effects, include some
    /// variety"): the game's own flecks and effecters and the player's mods' (A RimWorld of Magic's motes, Vanilla Psycasts
    /// Expanded's flecks, Anomaly's and Biotech's, by name and softly), a different set for each god, in its own colours. Hollin's leaves and green
    /// flashes, Smoulder's embers and heat, Bellwether's dust and the bowed grass's air, Morel's pale spores, Ennu's skip flash
    /// and footprints, Pip's sparks, Cirrus's lightning glow, Hush's snow and frost, the Hunger's dark rings, Helios's light
    /// shafts, Selene's pale rings and twinkles, Tamiu's sand, Murmur's water, Ducat's gold glints, Revel's sweet smoke. Shown with every god's
    /// big moment (GodFx.Flash) and with each of its effects (small, notable, great: the more, the stronger). Nothing saved.
    /// </summary>
    public static class GodTouchFx
    {
        /// <summary>One god's look: the flecks or motes thrown, an effecter for the big ones, and whether the sky flashes.</summary>
        private sealed class Look
        {
            public string[] flecks;
            public string effecter;
            public bool skyFlash;
            public float speed = 0.4f, spread = 1.2f;
            /// <summary>Thrown upward (leaves, sparks, embers) or left lying (dust, water, sand).</summary>
            public bool rise = true;
        }

        private static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["Hollin"] = new Look { flecks = new[] { "GauranlenLeaf", "Leaf", "LeafFlipped", "GrowingFlash", "Mote_GreenTwinkle" }, effecter = "GauranlenLeavesBatch", speed = 0.6f },
            ["Smoulder"] = new Look { flecks = new[] { "Fleck_BurnerUsedEmber", "MicroSparks", "HeatGlow", "FireGlow", "Mote_Heat", "VPE_AggresiveHeatDump" }, speed = 0.8f },
            ["Bellwether"] = new Look { flecks = new[] { "DustPuffThick", "AirPuff", "DustSlow", "Mote_Twinkle" }, rise = false, spread = 1.8f },
            ["Morel"] = new Look { flecks = new[] { "SmokeGrowing", "DustSlow", "Fleck_DeadlifeSmall", "Mote_Twinkle_grayscale" }, speed = 0.15f },
            ["Ennu"] = new Look { flecks = new[] { "PsycastSkipFlashEntry", "PsycastSkipInnerEntry", "Footprint", "Mote_Twinkle" }, effecter = "Skip_EntryNoDelay", rise = false },
            ["Pip"] = new Look { flecks = new[] { "ElectricalSpark", "MicroSparksFast", "Fleck_RadialSparks", "SparkFlash", "Mote_PsiCurrent", "VPE_VortexSpark" }, effecter = "Power_Cell_Sparks", speed = 1f },
            ["Cirrus"] = new Look { flecks = new[] { "LightningGlow", "BrightFlash", "Mote_Bolt", "Mote_BlueTwinkle" }, skyFlash = true, speed = 0.7f },
            ["Hush"] = new Look { flecks = new[] { "SnowBits", "AirPuff", "Twinkle", "Mote_WhiteIce", "VPE_ColdGlow" }, speed = 0.1f, spread = 2f },
            ["Undertow"] = new Look { flecks = new[] { "DarkHighlightRing", "PsychicDistortionRingContracting", "VoidNodeLowestGroundFlashes", "Mote_ShadowCloud", "VPE_BlackSmoke" }, rise = false },
            ["Helios"] = new Look { flecks = new[] { "Lightshaft", "GleamingLensFlare", "BrightFlashLong", "Mote_Holy", "VFEP_PsycastAreaEffectSlow" }, skyFlash = true, rise = false },
            ["Selene"] = new Look { flecks = new[] { "Bokeh", "Twinkle", "HoraxianWhiteRing", "Mote_BlueTwinkle", "VPE_PsycastSkipFlashEntry_DarkBlue" }, speed = 0.12f, spread = 1.6f },
            ["Mog"] = new Look { flecks = new[] { "SandBits", "DustSlow", "GlimmerSmall", "Mote_Twinkle" }, rise = false, spread = 1.5f },
            ["Murmur"] = new Look { flecks = new[] { "WaterSplash", "WaterRipple", "GroundWaterSplash", "WaterskipSplashParticles" }, rise = false },
            ["Ducat"] = new Look { flecks = new[] { "GlimmerSmall", "YellowSparkFlash", "Twinkle", "Mote_Twinkle" }, speed = 0.5f },
            ["Revel"] = new Look { flecks = new[] { "AutobongSmoke", "Twinkle", "Mote_Twinkle", "Fleck_VatBubble" }, effecter = "Smoke_Joint", speed = 0.3f, spread = 1.4f }
        };

        private static readonly Dictionary<string, Def> named = new Dictionary<string, Def>();

        /// <summary>A fleck or a mote by name (the game's, a DLC's or a mod's), or null where it isn't loaded.</summary>
        private static Def Named(string name)
        {
            if (name.NullOrEmpty()) return null;
            if (named.TryGetValue(name, out var d)) return d;
            d = DefDatabase<FleckDef>.GetNamedSilentFail(name) ?? (Def)DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (d is ThingDef t && t.mote == null) d = null;
            named[name] = d;
            return d;
        }

        /// <summary>How many flecks each strength throws, and how large they are.</summary>
        private static int Count(TouchTier t) => t == TouchTier.Great ? 26 : t == TouchTier.Notable ? 12 : 6;

        private static float Size(TouchTier t) => t == TouchTier.Great ? 1.6f : t == TouchTier.Notable ? 1.1f : 0.75f;

        /// <summary>
        /// The god's own look at <paramref name="cell"/>, as strong as the effect (a great one also its effecter and, for the
        /// sky's gods, a flash over the land); a dark one in the god's darker colour. Where the player isn't looking, nothing.
        /// </summary>
        public static void Show(God g, Map map, IntVec3 cell, TouchTier tier, bool dark)
        {
            if (g == null || map == null || !cell.IsValid || !cell.InBounds(map) || map != Find.CurrentMap) return;
            try
            {
                Signature(g.Key, map, cell, dark ? g.colour2 : g.colour, dark ? Color.Lerp(g.colour2, Color.black, 0.3f) : g.colour2, tier);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's look where it acted", e);
            }
        }

        /// <summary>The look itself, by the god's key (also for its big moments: GodFx.Flash).</summary>
        internal static void Signature(string key, Map map, IntVec3 cell, Color a, Color b, TouchTier tier)
        {
            if (key == null || !Looks.TryGetValue(key, out var look)) return;
            var defs = look.flecks.Select(Named).Where(d => d != null).ToList();
            var loc = cell.ToVector3Shifted();
            int n = Count(tier);
            float size = Size(tier);
            if (defs.Count > 0)
                for (int i = 0; i < n; i++)
                {
                    var d = defs[i % defs.Count];
                    var at = loc + Rand.InsideUnitCircleVec3 * look.spread * (tier == TouchTier.Great ? 1.6f : 1f);
                    float angle = look.rise ? Rand.Range(-35f, 35f) : Rand.Range(0f, 360f);
                    GodFx.Throw(d, at, map, Rand.Range(0.5f, 1f) * size, Rand.Bool ? a : b, angle, Rand.Range(0.5f, 1.2f) * look.speed, Rand.Range(-90f, 90f),
                        Rand.Range(0.5f, 1.1f) * (tier == TouchTier.Great ? 1.5f : 1f));
                }
            // Murmur's water lies on the ground where it acted, and Hush's frost.
            if (key == "Murmur")
                for (int i = 0; i < (tier == TouchTier.Great ? 6 : 2); i++)
                    FleckMaker.WaterSplash(loc + Rand.InsideUnitCircleVec3 * 1.2f, map, 1.5f * size, 1.5f);
            if (key == "Hush" && !cell.Roofed(map)) map.snowGrid.AddDepth(cell, tier == TouchTier.Great ? 0.5f : 0.2f);
            if (key == "Smoulder") FleckMaker.ThrowHeatGlow(cell, map, 1.5f * size);
            if (tier < TouchTier.Great) return;
            if (!look.effecter.NullOrEmpty())
            {
                var eff = DefDatabase<EffecterDef>.GetNamedSilentFail(look.effecter);
                if (eff != null)
                {
                    var e = eff.Spawn(cell, map);
                    e.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
                    e.Cleanup();
                }
            }
            if (look.skyFlash)
            {
                // The game's own harmless lightning flash over the land (no strike), for the gods of the sky.
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
            }
            FleckMaker.Static(loc, map, FleckDefOf.PsycastAreaEffect, 4f);
        }

        /// <summary>A notable effect's sound: the god's own omen sting at the place, quiet, once.</summary>
        public static void Sound(God g, Map map, IntVec3 cell)
        {
            if (g == null || map == null || !cell.IsValid || map != Find.CurrentMap) return;
            try
            {
                var look = GodsWiring.LookOf(g);
                if (look == null) return;
                foreach (var s in GodSounds.StingFor(look, PresenceKind.Omen).Take(1))
                    GodSounds.Play(s, map, cell, 0.6f)?.End();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an effect's sound", e);
            }
        }
    }
}
