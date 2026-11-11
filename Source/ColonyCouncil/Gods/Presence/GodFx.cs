using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a god sends up and flashes on the map, from the game's and the player's mods' own effects, softly (each by
    /// name; Rimfolk's own motes when a look's aren't there): the motes rising off a shown god (A RimWorld of Magic's holy
    /// lights, twinkles, flowers, frost, bolts, ghosts and shadows, as its look names them), the flash of its coming (its
    /// effecters, a burst of rays, the circle on the ground), the shimmer of one in disguise at night, and the small
    /// lights over those who pray.
    /// </summary>
    public static class GodFx
    {
        public const string OwnMote = "Rimfolk_GodMote";
        public const string OwnSpark = "Rimfolk_GodSpark";
        public const string OwnRays = "Rimfolk_GodRays";

        private static FleckDef mote, spark, rays;

        private static FleckDef Own(ref FleckDef f, string name) => f ??= DefDatabase<FleckDef>.GetNamedSilentFail(name);

        /// <summary>How often motes rise off a god, in ticks: more often at night, when they show best.</summary>
        public static int MoteEvery(Map map) => map != null && map.skyManager.CurSkyGlow < 0.4f ? 35 : 55;

        // ---------------- Rising off a god ----------------

        internal static void Rise(HediffComp_GodAvatar c, Pawn p)
        {
            if (!GodAura.On || p.Map != Find.CurrentMap) return;
            var map = p.Map;
            var look = c.look;
            float body = GodAura.BodyScale(p) * c.DrawSize;
            var loc = p.DrawPos + new Vector3(Rand.Range(-0.45f, 0.45f) * body, 0f, Rand.Range(-0.2f, 0.5f) * body);
            var list = look.Motes;
            Def def = list.Count > 0 ? list.RandomElement() : Own(ref mote, OwnMote);
            var color = Rand.Bool ? c.ColorA : c.ColorB;
            Throw(def, loc, map, Rand.Range(0.35f, 0.7f), color, Rand.Range(-25f, 25f), Rand.Range(0.25f, 0.55f), Rand.Range(-60f, 60f), Rand.Range(0.6f, 1.1f));
            // Always one of Rimfolk's own small lights too, in the god's colour, so every god's motes carry its colour.
            if (Rand.Chance(0.6f)) Throw(Own(ref mote, OwnMote), loc + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, 0f), map, Rand.Range(0.25f, 0.45f), c.ColorA, Rand.Range(-15f, 15f),
                Rand.Range(0.3f, 0.6f), 0f, Rand.Range(0.8f, 1.4f));
        }

        /// <summary>Whether a mote by this name is drawn in its own colours (a coloured picture) rather than tinted (a white or grey one).</summary>
        private static bool Tinted(Def def) =>
            def != null && (def.defName.StartsWith("Rimfolk_") || def.defName.IndexOf("rayscale", StringComparison.OrdinalIgnoreCase) >= 0 || def.defName.StartsWith("Mote_Psi")
                            || def.defName == "Mote_Twinkle" || def is FleckDef);

        /// <summary>A mote of either kind (a fleck, or a mote thing such as A RimWorld of Magic's), moving at an angle (0 is up the screen).</summary>
        public static void Throw(Def def, Vector3 loc, Map map, float scale, Color color, float angle, float speed, float rotation, float solid)
        {
            if (def == null || map == null || !loc.ShouldSpawnMotesAt(map)) return;
            var tint = Tinted(def) ? color : Color.Lerp(Color.white, color, 0.2f);
            try
            {
                if (def is FleckDef f)
                {
                    var data = FleckMaker.GetDataStatic(loc, map, f, scale);
                    data.instanceColor = tint;
                    data.velocityAngle = angle;
                    data.velocitySpeed = speed;
                    data.rotationRate = rotation;
                    data.rotation = Rand.Range(0f, 360f);
                    data.solidTimeOverride = solid;
                    map.flecks.CreateFleck(data);
                    return;
                }
                if (def is ThingDef t && t.mote != null)
                {
                    if (map.moteCounter.SaturatedLowPriority) return;
                    var m = ThingMaker.MakeThing(t) as Mote;
                    if (m == null) return;
                    m.Scale = scale * 1.6f;
                    m.exactPosition = loc;
                    m.exactRotation = Rand.Range(0f, 360f);
                    m.rotationRate = rotation;
                    m.instanceColor = tint;
                    m.solidTimeOverride = solid;
                    if (m is MoteThrown thrown) thrown.SetVelocity(angle, speed);
                    GenSpawn.Spawn(m, loc.ToIntVec3(), map);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a mote (" + def.defName + ")", e);
            }
        }

        // ---------------- The flash of a coming ----------------

        /// <summary>
        /// The flash of a god's coming at <paramref name="cell"/>: the sky flashes in its colour, its effecters go off, a
        /// burst of rays opens, its circle shows on the ground and its motes fly up. <paramref name="big"/>: a coming, a
        /// reveal, a wrath; small: an omen's glimmer.
        /// </summary>
        public static void Flash(GodLookDef look, Map map, IntVec3 cell, Color a, Color b, bool big)
        {
            if (look == null || map == null || !cell.InBounds(map)) return;
            var loc = cell.ToVector3Shifted();
            try
            {
                if (big) map.weatherManager.eventHandler.AddEvent(new GodFlashEvent(map, a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sky's flash", e);
            }
            if (big)
            {
                foreach (var fx in look.Flashes)
                {
                    try
                    {
                        var eff = fx.Spawn(cell, map);
                        eff.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
                        eff.Cleanup();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: an effecter (" + fx.defName + ")", e);
                    }
                }
                Throw(Own(ref rays, OwnRays), loc, map, 3.2f, a, 0f, 0f, 8f, 1.2f);
                FleckMaker.Static(loc, map, FleckDefOf.PsycastAreaEffect, 6f);
                foreach (var circle in look.Circles)
                    Throw(circle, loc, map, 3f, b, 0f, 0f, 12f, 2.5f);
                // Spirits running out from it, each the way it faces.
                foreach (var spirit in look.Spirits)
                {
                    float way = GodPresenceMath.SpiritAngle(spirit.defName);
                    for (int i = 0; i < 2; i++)
                        Throw(spirit, loc + Rand.InsideUnitCircleVec3 * 0.6f, map, Rand.Range(0.7f, 1f), a, way + Rand.Range(-25f, 25f), Rand.Range(1.6f, 2.6f), 0f, 0.9f);
                }
                for (int i = 0; i < 14; i++)
                    Throw(look.Motes.Count > 0 ? look.Motes.RandomElement() : Own(ref mote, OwnMote), loc + Rand.InsideUnitCircleVec3 * 1.2f, map, Rand.Range(0.5f, 1f),
                        Rand.Bool ? a : b, Rand.Range(0f, 360f), Rand.Range(0.6f, 1.6f), Rand.Range(-120f, 120f), Rand.Range(0.6f, 1.2f));
            }
            else
            {
                for (int i = 0; i < 5; i++)
                    Throw(Own(ref spark, OwnSpark), loc + Rand.InsideUnitCircleVec3 * 0.8f, map, Rand.Range(0.3f, 0.6f), a, Rand.Range(-30f, 30f), Rand.Range(0.15f, 0.4f), 0f,
                        Rand.Range(0.5f, 1f));
                if (look.Motes.Count > 0) Throw(look.Motes.RandomElement(), loc, map, 0.6f, a, 0f, 0.3f, 30f, 0.8f);
            }
        }

        // ---------------- Small lights ----------------

        /// <summary>A disguised god's shimmer at night: one faint twinkle about it.</summary>
        internal static void Shimmer(Pawn p, Color c)
        {
            var loc = p.DrawPos + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.1f, 0.6f));
            Throw(Own(ref spark, OwnSpark), loc, p.Map, Rand.Range(0.25f, 0.4f), new Color(c.r, c.g, c.b, 0.55f), 0f, 0.05f, 0f, 0.4f);
        }

        /// <summary>A small light rising over someone who prays to a god.</summary>
        internal static void Prayer(Pawn p, Color c)
        {
            var holy = GodLookDef.Mote("Mote_Holy");
            var loc = p.DrawPos + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, 0.5f);
            Throw(holy ?? Own(ref mote, OwnMote), loc, p.Map, holy != null ? 0.35f : 0.3f, c, Rand.Range(-10f, 10f), 0.35f, 0f, 0.6f);
        }

        /// <summary>Rimfolk's own mote (what the look's motes fall back on).</summary>
        internal static FleckDef MoteDef => Own(ref mote, OwnMote);
    }

    /// <summary>The sky flashes in a god's colour as it comes (a weather event, as the game's lightning flash; nothing saved).</summary>
    public class GodFlashEvent : WeatherEvent
    {
        private readonly SkyColorSet colors;
        private int age;
        private const int Duration = 70;
        private const int FadeIn = 6;

        public GodFlashEvent(Map map, Color c) : base(map)
        {
            var bright = Color.Lerp(Color.white, c, 0.55f);
            colors = new SkyColorSet(bright, Color.Lerp(bright, Color.black, 0.15f), bright, 1.2f);
        }

        public override bool Expired => age > Duration;

        public override SkyTarget SkyTarget => new SkyTarget(1f, colors, 1f, 1f);

        public override float SkyTargetLerpFactor => age <= FadeIn ? age / (float)FadeIn * 0.85f : 0.85f * (1f - (age - FadeIn) / (float)(Duration - FadeIn));

        public override void FireEvent()
        {
        }

        public override void WeatherEventTick() => age++;
    }
}
