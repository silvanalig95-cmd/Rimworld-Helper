using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A button on a weapon selected on the map: Rimfolk's weapon card, its rating in the button's label. The card opens for
    /// the colonist selected with it, if any (else the last one the card was opened for).
    /// </summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Patch_WeaponCardGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            if (__result != null)
                foreach (var g in __result)
                    yield return g;
            Gizmo more = null;
            if (RimfolkPatches.On && WeaponCardWindow.On && __instance != null && __instance.Spawned && !(__instance is Pawn) && WeaponLive.Rateable(__instance.def))
                try
                {
                    more = Make(__instance);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the weapon card's button on a weapon", e);
                }
            if (more != null) yield return more;
        }

        // The button made last, kept a second: the game asks for a selected thing's buttons every frame.
        private static ThingWithComps madeFor;
        private static Pawn madeWho;
        private static float madeAt;
        private static Command_Action made;

        private static Gizmo Make(ThingWithComps weapon)
        {
            var r = WeaponLive.Of(weapon);
            if (r == null) return null;
            Pawn who = null;
            foreach (var o in Find.Selector.SelectedObjectsListForReading)
                if (o is Pawn p && p.IsColonist)
                {
                    who = p;
                    break;
                }
            if (made != null && madeFor == weapon && madeWho == who && UnityEngine.Time.realtimeSinceStartup - madeAt < 1f) return made;
            madeFor = weapon;
            madeWho = who;
            madeAt = UnityEngine.Time.realtimeSinceStartup;
            return made = new Command_Action
            {
                defaultLabel = "CC_Weapon_Gizmo".Translate(WeaponForMath.RatingText(r.Rating.Overall).Named("RATING")).Resolve(),
                defaultDesc = "CC_Weapon_Gizmo_Desc".Translate() + "\n\n" + WeaponUi.Tip(r, who, weapon, true),
                icon = Icons.CatNumbers,
                action = () => WeaponCardWindow.Open(weapon, who)
            };
        }
    }
}
