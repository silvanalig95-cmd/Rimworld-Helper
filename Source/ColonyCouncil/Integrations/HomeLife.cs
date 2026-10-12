using System;
using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Soft integrations with the "home life" mods the player always runs: Dubs Bad Hygiene, Hospitality, Vanilla
    /// Cooking and Brewing Expanded, Vanilla Outposts Expanded, RimHUD and Interaction Bubbles. Nothing here references
    /// another mod's assembly: defs are found by name and classes by type name, so with a mod missing every check is
    /// simply false and Rimfolk plays as it does without it. Mod-only defs that are never saved (room plans, advisor
    /// checks, mood hints, RimHUD widgets) carry MayRequire and don't load at all; wishes, memories and conversations
    /// always load and stay idle without their mod, so a save still finds them after the mod is removed.
    /// </summary>
    public static class HomeLife
    {
        private static readonly Dictionary<(Type, string), bool> classMatches = new Dictionary<(Type, string), bool>();

        /// <summary>
        /// <paramref name="type"/>, or one of its base classes, is <paramref name="ns"/>.<paramref name="name"/>. Matched by
        /// name, so the other mod's assembly is never needed (and a mod built on top of it counts too).
        /// </summary>
        public static bool ClassIs(Type type, string ns, string name)
        {
            if (type == null) return false;
            var key = (type, ns + "." + name);
            if (classMatches.TryGetValue(key, out bool hit)) return hit;
            hit = false;
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.Name == name && t.Namespace == ns)
                {
                    hit = true;
                    break;
                }
            }
            classMatches[key] = hit;
            return hit;
        }

        /// <summary>
        /// Room plan roles for these mods' furniture (asked by <see cref="RoomTemplateDef.RolePredicate"/> for roles it
        /// doesn't know itself). Null for an unknown role.
        /// </summary>
        public static Func<ThingDef, bool> RolePredicate(string role)
        {
            switch (role)
            {
                case "toilet": return Hygiene.IsToilet;
                case "wash": return Hygiene.IsWashStand;
                case "bath": return Hygiene.IsBathOrShower;
                case "saunaheater": return Hygiene.IsSaunaHeater;
                default: return null;
            }
        }
    }
}
