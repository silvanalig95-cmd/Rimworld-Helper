using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dynamic Atmosphere (astryl.DynamicAtmosphere) draws its cloud shadows, its haze (with a condition's tint, the yellow of
    /// a heat wave) and its event tints as one plane over the whole map, so roofed rooms with their own light darkened and
    /// coloured with the sky outside; its rain and ripples it already keeps out from under roofs. The player, 28 November
    /// 2026: "indoor areas should not be impacted by clouds, haze etc". While the setting is on, those sky layers are drawn
    /// over the cells open to the sky only (not roofed, not fogged, as its own roof mask reads them): the same material, at
    /// the same height and with the same texture coordinates as its plane, on a mesh of the open cells' runs, built again at
    /// most twice a second when a roof or the fog changes. Everything else it draws is its own. Found by name; without the
    /// mod, or with a version whose drawing differs, nothing happens and its own drawing stands.
    /// </summary>
    public static class SkyIndoors
    {
        public const string PackageId = "astryl.dynamicatmosphere";
        public const string ComponentType = "DynamicAtmosphere.MapComponent_Atmosphere", DrawMethod = "DrawQuad", LayerField = "RenderLayer";

        public static bool On => ColonyCouncilMod.Settings?.skyIndoorsClear ?? true;

        private static MethodInfo target;
        private static bool looked;
        private static int layer = -1;

        /// <summary>Its static DrawQuad(Material, Vector3, float), or null.</summary>
        public static MethodInfo Target()
        {
            if (looked) return target;
            looked = true;
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly(ComponentType);
                target = type == null ? null : AccessTools.Method(type, DrawMethod, new[] { typeof(Material), typeof(Vector3), typeof(float) });
                if (target != null && !target.IsStatic) target = null;
                var f = type == null ? null : AccessTools.Field(type, LayerField);
                if (f != null && f.IsStatic && f.FieldType == typeof(int)) layer = (int)f.GetValue(null);
            }
            catch (Exception e)
            {
                target = null;
                CouncilLog.ErrorOnce("Dynamic Atmosphere's sky over roofed rooms: finding its drawing", e);
            }
            return target;
        }

        /// <summary>A map's mesh of its open-sky cells, and when it was built.</summary>
        private sealed class Sky
        {
            public Map map;
            public Mesh mesh;
            public bool dirty = true, hooked;
            public float builtAt = -1000f;
            public Vector3 center;
        }

        private static readonly Dictionary<int, Sky> skies = new Dictionary<int, Sky>();
        private static bool failed;

        /// <summary>A roof or the fog changed on the map: its mesh is built again soon.</summary>
        public static void MarkDirty(Map map)
        {
            if (map != null && skies.TryGetValue(map.uniqueID, out var s)) s.dirty = true;
        }

        /// <summary>Drops the meshes (a game left, a map gone).</summary>
        public static void Clear()
        {
            foreach (var s in skies.Values)
                if (s.mesh != null) UnityEngine.Object.Destroy(s.mesh);
            skies.Clear();
        }

        /// <summary>
        /// Draws a sky layer over the open cells instead of the whole map. True when it did (Dynamic Atmosphere's own plane is
        /// then skipped); false leaves its drawing as it is.
        /// </summary>
        public static bool Draw(Material mat, Vector3 center, float altitude)
        {
            if (failed || mat == null || !SkyIndoorsMath.SkyMaterial(mat.name)) return false;
            var map = Find.CurrentMap;
            if (map == null) return false;
            try
            {
                var mesh = MeshFor(map, center);
                // Nothing open to the sky: nothing of the sky is drawn.
                if (mesh == null) return true;
                Graphics.DrawMesh(mesh, new Vector3(0f, altitude, 0f), Quaternion.identity, mat, layer >= 0 ? layer : 0);
                return true;
            }
            catch (Exception e)
            {
                failed = true;
                CouncilLog.ErrorOnce("Dynamic Atmosphere's sky over roofed rooms: drawing (its own drawing is left as it is from now)", e);
                return false;
            }
        }

        private static Mesh MeshFor(Map map, Vector3 center)
        {
            if (!skies.TryGetValue(map.uniqueID, out var s) || s.map != map)
            {
                // A map gone since, or another game's of the same number: its mesh with it.
                if (s?.mesh != null) UnityEngine.Object.Destroy(s.mesh);
                if (skies.Count > 8) Clear();
                skies[map.uniqueID] = s = new Sky { map = map };
            }
            if (!s.hooked && map.events != null)
            {
                int id = map.uniqueID;
                map.events.CellFogChanged += (c, fogged) =>
                {
                    if (skies.TryGetValue(id, out var k)) k.dirty = true;
                };
                map.events.MapFogged += () =>
                {
                    if (skies.TryGetValue(id, out var k)) k.dirty = true;
                };
                s.hooked = true;
            }
            float now = Time.realtimeSinceStartup;
            if (s.mesh == null || center != s.center || (s.dirty && (now - s.builtAt >= SkyIndoorsMath.RebuildEvery || now < s.builtAt)))
            {
                Build(map, center, s);
                s.builtAt = now;
                s.dirty = false;
                s.center = center;
            }
            return s.mesh != null && s.mesh.vertexCount > 0 ? s.mesh : null;
        }

        private static void Build(Map map, Vector3 center, Sky s)
        {
            int w = map.Size.x, h = map.Size.z;
            var open = new bool[w * h];
            var roofs = map.roofGrid;
            var fog = map.fogGrid;
            var idx = map.cellIndices;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = idx.CellToIndex(x, z);
                    open[z * w + x] = !roofs.Roofed(i) && !fog.IsFogged(i);
                }
            var runs = SkyIndoorsMath.Runs(open, w, h);
            int n = SkyIndoorsMath.Vertices(runs.Count);
            var verts = new Vector3[n];
            var uvs = new Vector2[n];
            var tris = new int[runs.Count * 6];
            for (int r = 0; r < runs.Count; r++)
            {
                var run = runs[r];
                float x0 = run.x0, x1 = run.x1 + 1, z0 = run.z, z1 = run.z + 1;
                int v = r * 4;
                verts[v] = new Vector3(x0, 0f, z0);
                verts[v + 1] = new Vector3(x0, 0f, z1);
                verts[v + 2] = new Vector3(x1, 0f, z1);
                verts[v + 3] = new Vector3(x1, 0f, z0);
                for (int k = 0; k < 4; k++)
                    uvs[v + k] = new Vector2(SkyIndoorsMath.PlaneUv(verts[v + k].x, center.x), SkyIndoorsMath.PlaneUv(verts[v + k].z, center.z));
                int t = r * 6;
                tris[t] = v;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 2;
                tris[t + 3] = v;
                tris[t + 4] = v + 2;
                tris[t + 5] = v + 3;
            }
            if (s.mesh == null) s.mesh = new Mesh { name = "Rimfolk_OpenSky" };
            else s.mesh.Clear();
            s.mesh.indexFormat = n > SkyIndoorsMath.MaxVertices16 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            s.mesh.vertices = verts;
            s.mesh.uv = uvs;
            s.mesh.triangles = tris;
            s.mesh.RecalculateBounds();
        }
    }

    /// <summary>Dynamic Atmosphere's sky layers (cloud shadows, haze, event tints) drawn over the open sky only (<see cref="SkyIndoors"/>).</summary>
    [HarmonyPatch]
    public static class Patch_SkyIndoors
    {
        public static bool Prepare() => SkyIndoors.Target() != null;

        public static MethodBase TargetMethod() => SkyIndoors.Target();

        public static string StandsDownWhy() =>
            GenTypes.GetTypeInAnyAssembly(SkyIndoors.ComponentType) == null ? "Dynamic Atmosphere isn't loaded" : "Dynamic Atmosphere's map drawing isn't the one Rimfolk knows";

        public static bool Prefix(Material mat, Vector3 center, float altitude)
        {
            if (!RimfolkPatches.On || !SkyIndoors.On) return true;
            return !SkyIndoors.Draw(mat, center, altitude);
        }
    }

    /// <summary>A roof built or taken down: the open sky's mesh is built again (<see cref="SkyIndoors"/>).</summary>
    [HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]
    public static class Patch_SkyIndoorsRoof
    {
        private static readonly AccessTools.FieldRef<RoofGrid, Map> MapOf = AccessTools.FieldRefAccess<RoofGrid, Map>("map");

        public static bool Prepare() => SkyIndoors.Target() != null;

        public static void Postfix(RoofGrid __instance)
        {
            try
            {
                SkyIndoors.MarkDirty(MapOf(__instance));
            }
            catch
            {
                // a roof changed on a map Rimfolk can't read: its mesh is built again when the map is drawn anew
            }
        }
    }
}
