using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A shown god's aura on the map (docs/design/gods.md, "Presence"; the textures are Tools/make_god_auras.py's): a soft
    /// halo on the ground under it and a carved ring turning slowly there, a tall glow behind its body and a bright ring
    /// round it, all breathing with one slow pulse; at night a shine drawn over the dark (which the night doesn't dim, so
    /// the god glows) and the light in its eyes; its name over it in its colour. A pale god's colours are pulled toward
    /// their own light by day so a white god still shows on a sunlit field. Drawn each frame for the gods on the map being
    /// looked at, and nothing else: with no god there it costs one check. Setting: "Gods' auras".
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GodAura
    {
        private const string Dir = "ColonyCouncil/Gods/Aura/";

        public static readonly Texture2D Halo = ContentFinder<Texture2D>.Get(Dir + "Halo");
        public static readonly Texture2D GlowTex = ContentFinder<Texture2D>.Get(Dir + "Glow");
        public static readonly Texture2D RingTex = ContentFinder<Texture2D>.Get(Dir + "Ring");
        public static readonly Texture2D RunesTex = ContentFinder<Texture2D>.Get(Dir + "RingRunes");
        public static readonly Texture2D RaysTex = ContentFinder<Texture2D>.Get(Dir + "Rays");
        public static readonly Texture2D MoteTex = ContentFinder<Texture2D>.Get(Dir + "Mote");
        public static readonly Texture2D Emblem = ContentFinder<Texture2D>.Get(Dir + "Emblem");

        // Made once: white, coloured as each is drawn.
        private static readonly Material HaloMat = MaterialPool.MatFrom(Dir + "Halo", ShaderDatabase.Transparent);
        private static readonly Material GlowMat = MaterialPool.MatFrom(Dir + "Glow", ShaderDatabase.Transparent);
        private static readonly Material RingMat = MaterialPool.MatFrom(Dir + "Ring", ShaderDatabase.Transparent);
        private static readonly Material RunesMat = MaterialPool.MatFrom(Dir + "RingRunes", ShaderDatabase.Transparent);
        private static readonly Material ShineMat = MaterialPool.MatFrom(Dir + "Ring", ShaderDatabase.MoteGlow);
        private static readonly Material EyesMat = MaterialPool.MatFrom(Dir + "Mote", ShaderDatabase.MoteGlow);

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "Gods' auras" (on by default): the aura, the motes and the light that follows a god.</summary>
        public static bool On => S?.godAura ?? true;

        /// <summary>Draws every shown god's aura on <paramref name="map"/> (the map being looked at).</summary>
        internal static void Draw(Map map)
        {
            if (!On || !GodAvatar.Any || map == null) return;
            var gods = GodAvatar.All;
            for (int i = 0; i < gods.Count; i++)
            {
                var c = gods[i];
                var p = c?.Pawn;
                if (c == null || c.disguised || c.look == null || p == null || !p.Spawned || p.Map != map) continue;
                try
                {
                    DrawOne(c, p, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: drawing an aura", e);
                }
            }
        }

        private static bool Seen(Pawn p, Map map) => !map.fogGrid.IsFogged(p.Position) && !p.IsHiddenFromPlayer();

        private static void DrawOne(HediffComp_GodAvatar c, Pawn p, Map map)
        {
            if (!Seen(p, map)) return;
            var pos = p.DrawPos;
            float size = c.DrawSize * BodyScale(p);
            float t = Time.realtimeSinceStartup + p.thingIDNumber % 97 * 0.37f;
            float pulse = GodPresenceMath.Pulse(t, GodPresenceMath.PulseSeconds);
            float sky = map.skyManager.CurSkyGlow;
            var a = GodLookDef.ColorOf(GodPresenceMath.DayTint(GodLookDef.Rgb(c.ColorA)));
            var b = GodLookDef.ColorOf(GodPresenceMath.DayTint(GodLookDef.Rgb(c.ColorB)));
            float ground = AltitudeLayer.MoteLow.AltitudeFor();
            float behind = AltitudeLayer.PawnRope.AltitudeFor();
            // On the ground: the halo, and the carved ring turning in it.
            Quad(HaloMat, new Vector3(pos.x, ground, pos.z - 0.25f * size), 0f, 2.7f * size, 2.7f * size, a, GodPresenceMath.HaloAlpha(sky, pulse));
            Quad(RunesMat, new Vector3(pos.x, ground + 0.002f, pos.z - 0.25f * size), t * GodPresenceMath.RunesTurn % 360f, 2.1f * size, 2.1f * size, b,
                0.55f * GodPresenceMath.RingAlpha(sky, pulse));
            // Behind it: the tall glow and the ring that breathes.
            Quad(GlowMat, new Vector3(pos.x, behind, pos.z + 0.12f * size), 0f, 1.35f * size, 2.2f * size, a, GodPresenceMath.GlowAlpha(sky, pulse));
            float ring = 1.95f * size * GodPresenceMath.RingScale(pulse);
            Quad(RingMat, new Vector3(pos.x, behind + 0.002f, pos.z + 0.05f * size), 0f, ring, ring, b, 0.7f * GodPresenceMath.RingAlpha(sky, pulse));
            // At night: a shine over the dark, and the light in its eyes.
            float shine = GodPresenceMath.NightShineAlpha(sky, pulse);
            if (shine <= 0f) return;
            float over = AltitudeLayer.VisEffects.AltitudeFor();
            Quad(ShineMat, new Vector3(pos.x, over, pos.z + 0.05f * size), 0f, ring * 1.05f, ring * 1.05f, c.ColorA, shine);
            if (p.RaceProps.Humanlike && p.Rotation != Rot4.North && !p.Downed)
            {
                var head = p.Drawer.renderer.BaseHeadOffsetAt(p.Rotation) * c.DrawSize;
                Quad(EyesMat, new Vector3(pos.x + head.x, over + 0.002f, pos.z + head.z + 0.02f), 0f, 0.55f * size, 0.4f * size, c.look.eyes, shine * 1.4f);
            }
        }

        /// <summary>An animal's aura fits its body; a person's is the person's.</summary>
        internal static float BodyScale(Pawn p)
        {
            if (p.RaceProps.Humanlike) return 1f;
            var g = p.ageTracker?.CurKindLifeStage?.bodyGraphicData;
            float w = g != null ? g.drawSize.x : 1.5f;
            return Mathf.Clamp(w / 1.5f, 0.6f, 3f);
        }

        private static void Quad(Material mat, Vector3 at, float angle, float w, float h, Color color, float alpha)
        {
            if (alpha <= 0.005f || mat == null) return;
            Block.SetColor(ShaderPropertyIDs.Color, new Color(color.r, color.g, color.b, alpha));
            var m = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(w, 1f, h));
            Graphics.DrawMesh(MeshPool.plane10, m, mat, 0, null, 0, Block);
        }

        /// <summary>Each shown god's name over it, in its colour, while the camera is close enough to read names.</summary>
        internal static void Names(Map map)
        {
            if (!GodAvatar.Any || map == null || Find.CameraDriver.CurrentZoom > CameraZoomRange.Middle) return;
            var gods = GodAvatar.All;
            for (int i = 0; i < gods.Count; i++)
            {
                var c = gods[i];
                var p = c?.Pawn;
                if (c == null || c.disguised || p == null || !p.Spawned || p.Map != map || !Seen(p, map)) continue;
                try
                {
                    var at = GenMapUI.LabelDrawPosFor(p, 0.8f * c.DrawSize * BodyScale(p) + 0.35f);
                    var col = Color.Lerp(c.ColorA, Color.white, 0.35f);
                    GenMapUI.DrawThingLabel(at, c.Name.CapitalizeFirst(), col);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a god's name", e);
                }
            }
        }
    }

    /// <summary>Draws the gods' auras and names on the map being looked at (only the gods on it; nothing is saved).</summary>
    public class GodAuraDrawer : MapComponent
    {
        public GodAuraDrawer(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (!GodAvatar.Any) return;
            var gods = GodAvatar.All;
            for (int i = 0; i < gods.Count; i++)
                if (gods[i]?.Pawn?.MapHeld == map)
                    gods[i].RefreshSize();
        }

        public override void MapComponentDraw()
        {
            if (Find.CurrentMap == map) GodAura.Draw(map);
        }

        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap == map && GodAura.On) GodAura.Names(map);
        }
    }

    /// <summary>
    /// The light that moves with a shown god (the game's own glower, on a thing nobody sees or saves): moved to the god's
    /// cell as it walks, so the ground round it is lit in its colour at night. It puts itself out when its god is gone.
    /// </summary>
    public class GodLight : ThingWithComps
    {
        public const string DefName = "Rimfolk_GodLight";

        [Unsaved] private Pawn owner;

        internal static GodLight Spawn(Pawn p, GodLookDef look, Color color)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
            if (def == null || p?.Map == null) return null;
            var l = ThingMaker.MakeThing(def) as GodLight;
            if (l == null) return null;
            l.owner = p;
            var g = l.GetComp<CompGlower>();
            if (g != null)
            {
                g.GlowRadius = Mathf.Clamp(look.glowRadius, 2f, 12f);
                var c = look.glowColor;
                g.GlowColor = new ColorInt((int)(c.r * 255f * 1.35f), (int)(c.g * 255f * 1.35f), (int)(c.b * 255f * 1.35f), 0);
            }
            GenSpawn.Spawn(l, p.Position, p.Map);
            return l;
        }

        /// <summary>To the god's cell, the light going with it.</summary>
        internal void Follow(IntVec3 cell)
        {
            if (!Spawned || cell == Position || !cell.InBounds(Map)) return;
            var map = Map;
            Position = cell;
            GetComp<CompGlower>()?.ForceRegister(map);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (owner == null || owner.Dead || !owner.Spawned || owner.Map != Map || !GodAvatar.Shown(owner) || !GodAura.On) Destroy();
        }
    }
}
