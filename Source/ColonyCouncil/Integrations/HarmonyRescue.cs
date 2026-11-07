using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ColonyCouncil
{
    /// <summary>
    /// Another mod's patch classes applied one at a time, for when its own PatchAll stopped at one that the Harmony in
    /// the game refuses (Integrations/MagicPatchRescue.cs). PatchAll goes through an assembly's classes in order and
    /// gives up at the first that throws, so every class after it is never applied. Only HarmonyLib here (no game types),
    /// so the same file is checked against the real Harmony 3 outside the game.
    /// </summary>
    public static class HarmonyRescue
    {
        /// <summary>The classes whose patch methods the Harmony id has applied anywhere.</summary>
        public static HashSet<Type> AppliedBy(string harmonyId)
        {
            var classes = new HashSet<Type>();
            if (string.IsNullOrEmpty(harmonyId)) return classes;
            foreach (var original in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                foreach (var list in new[] { info.Prefixes, info.Postfixes, info.Transpilers, info.Finalizers })
                {
                    if (list == null) continue;
                    foreach (var patch in list)
                        if (patch?.owner == harmonyId && patch.PatchMethod?.DeclaringType != null) classes.Add(patch.PatchMethod.DeclaringType);
                }
            }
            return classes;
        }

        /// <summary>The classes of the assemblies that carry a Harmony attribute, in the assemblies' own order (PatchAll's).</summary>
        public static List<Type> PatchClasses(IEnumerable<Assembly> assemblies)
        {
            var list = new List<Type>();
            foreach (var asm in assemblies ?? Enumerable.Empty<Assembly>())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                foreach (var t in types)
                    if (t != null && t.GetCustomAttributes(typeof(HarmonyAttribute), false).Length > 0) list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// Applies each class that the id hasn't applied yet, one at a time, leaving out <paramref name="skip"/>. Returns
        /// the classes that patched something now, and those that threw (with why); a class whose own Prepare stood down
        /// patches nothing and is in neither list.
        /// </summary>
        public static (List<string> applied, List<string> failed) ApplyMissing(string harmonyId, IEnumerable<Type> classes, ICollection<Type> skip)
        {
            var applied = new List<string>();
            var failed = new List<string>();
            var done = AppliedBy(harmonyId);
            var harmony = new Harmony(harmonyId);
            foreach (var t in classes)
            {
                if (t == null || done.Contains(t) || skip != null && skip.Contains(t)) continue;
                try
                {
                    var patched = harmony.CreateClassProcessor(t).Patch();
                    if (patched != null && patched.Count > 0) applied.Add(t.Name);
                }
                catch (Exception e)
                {
                    failed.Add(t.Name + ": " + (e.InnerException ?? e).Message);
                }
            }
            return (applied, failed);
        }
    }
}
