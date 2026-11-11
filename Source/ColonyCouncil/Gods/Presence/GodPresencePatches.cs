using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The gods' hooks into the game (Gods/Presence/). Each looks first whether any god walks (GodAvatar.Any, one check),
    // so a game without gods pays nothing more; they change only what concerns a god.

    /// <summary>
    /// A shown god is drawn larger than its kind (growing into its size at a reveal), lifted a little so it stands on its
    /// cell, and an animal god's whole body is tinted its colour. Portraits and the game's cached pictures stay as they are.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_GodDrawParms
    {
        public static void Postfix(ref PawnDrawParms __result)
        {
            if (!GodAvatar.Any || !RimfolkPatches.On) return;
            try
            {
                var c = GodAvatar.Of(__result.pawn);
                if (c == null || c.disguised || c.look == null) return;
                bool picture = __result.Portrait || __result.Cache;
                if (!__result.pawn.RaceProps.Humanlike)
                {
                    var t = c.look.animalTint;
                    __result.tint = new Color(__result.tint.r * t.r, __result.tint.g * t.g, __result.tint.b * t.b, __result.tint.a);
                }
                if (picture) return;
                float s = c.DrawSize;
                if (s <= 1.001f) return;
                float lift = 0.3f * (s - 1f) * GodAura.BodyScale(__result.pawn);
                __result.matrix = Matrix4x4.Translate(new Vector3(0f, 0f, lift)) * __result.matrix * Matrix4x4.Scale(new Vector3(s, 1f, s));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: drawing a god", e);
            }
        }
    }

    /// <summary>
    /// A shown god is drawn piece by piece as the game draws a person close up, never from the game's small cached
    /// picture (which can't be drawn larger), so it keeps its size when the camera is far.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "PawnNeedsHediffMaterial")]
    public static class Patch_GodNotCached
    {
        public static void Postfix(ref bool __result, Pawn ___pawn)
        {
            if (__result || !GodAvatar.Any || !RimfolkPatches.On) return;
            if (GodAvatar.Shown(___pawn)) __result = true;
        }
    }

    /// <summary>A god leaves no footprints in the snow and no splashes (shown or in disguise: one of its tells).</summary>
    [HarmonyPatch(typeof(PawnFootprintMaker), nameof(PawnFootprintMaker.ProcessPostTickVisuals))]
    public static class Patch_GodNoFootprints
    {
        public static bool Prefix(Pawn ___pawn) => !GodAvatar.Any || !RimfolkPatches.On || !GodAvatar.Is(___pawn);
    }

    /// <summary>A god is never stripped.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.AnythingToStrip))]
    public static class Patch_GodNotStripped
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(__instance)) __result = false;
        }
    }

    /// <summary>A god is never tamed.</summary>
    [HarmonyPatch(typeof(TameUtility), nameof(TameUtility.CanTame))]
    public static class Patch_GodNotTamed
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(pawn)) __result = false;
        }
    }

    /// <summary>A god is never taken captive.</summary>
    [HarmonyPatch(typeof(GenAI), nameof(GenAI.CanBeCaptured))]
    public static class Patch_GodNotCaptured
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(pawn)) __result = false;
        }
    }

    /// <summary>A god is never arrested.</summary>
    [HarmonyPatch(typeof(GenAI), nameof(GenAI.CanBeArrestedBy))]
    public static class Patch_GodNotArrested
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(pawn)) __result = false;
        }
    }

    /// <summary>A god is never recruited.</summary>
    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.Recruitable), MethodType.Getter)]
    public static class Patch_GodNotRecruited
    {
        public static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(___pawn)) __result = false;
        }
    }

    /// <summary>A god, and the robe and crown a god wears, are never sold.</summary>
    [HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.PlayerSellableNow))]
    public static class Patch_GodNotSold
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (!__result || !GodAvatar.Any) return;
            if (t is Pawn p && GodAvatar.Is(p)) __result = false;
            else if (t is Apparel ap && ap.Wearer != null && GodAvatar.Of(ap.Wearer) is HediffComp_GodAvatar c && c.Gave(ap)) __result = false;
        }
    }

    /// <summary>A god's body is never eaten.</summary>
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.IngestibleNow), MethodType.Getter)]
    public static class Patch_GodNotEaten
    {
        public static void Postfix(Corpse __instance, ref bool __result)
        {
            if (__result && GodAvatar.Any && GodAvatar.Is(__instance.InnerPawn)) __result = false;
        }
    }

    /// <summary>
    /// The wind dies under a god's presence (GameCondition_GodPresence): the wind speed falls by the presence's
    /// stillness, and the plants sway by the stiller wind.
    /// </summary>
    [HarmonyPatch(typeof(WindManager), nameof(WindManager.WindManagerTick))]
    public static class Patch_GodStillsWind
    {
        public static void Prefix(float ___plantSwayHead, out float __state) => __state = ___plantSwayHead;

        public static void Postfix(Map ___map, ref float ___cachedWindSpeed, ref float ___plantSwayHead, float __state, List<Material> ___plantMaterials)
        {
            if (Find.TickManager.TicksGame > GameCondition_GodPresence.AnyUntil || !RimfolkPatches.On) return;
            try
            {
                float f = GameCondition_GodPresence.WindOn(___map);
                if (f >= 0.999f) return;
                ___cachedWindSpeed *= f;
                if (!Prefs.PlantWindSway) return;
                ___plantSwayHead = __state + Mathf.Min(___cachedWindSpeed, 1f);
                if (Find.CurrentMap != ___map || ___plantMaterials == null) return;
                for (int i = 0; i < ___plantMaterials.Count; i++) ___plantMaterials[i].SetFloat(ShaderPropertyIDs.SwayHead, ___plantSwayHead);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the stilled wind", e);
            }
        }
    }
}
