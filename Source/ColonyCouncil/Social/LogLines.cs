using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace ColonyCouncil
{
    /// <summary>
    /// More lines for an existing moment or other interaction in the social log (or for a rule pack, such as a contest's
    /// result), kept in a separate file: a rule pack whose rules join the target's at startup, the way the game itself
    /// joins rule packs (as an include). It may add root shapes (<c>r_logentry->...</c>), more choices for the target's
    /// sub-rules, and sub-rules of its own. The grammar counterpart of <see cref="FlavorExtensionDef"/>, so writing can
    /// grow without touching the original defs.
    /// </summary>
    public class LogLinesDef : RulePackDef
    {
        /// <summary>The interaction whose log lines these join (its initiator's rules).</summary>
        public InteractionDef interaction;

        /// <summary>Or the rule pack they join.</summary>
        public RulePackDef pack;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if ((interaction == null) == (pack == null)) yield return "log lines need exactly one target: interaction or pack";
        }
    }

    /// <summary>Joins every <see cref="LogLinesDef"/> to its target once the defs have loaded.</summary>
    [StaticConstructorOnStartup]
    public static class LogLines
    {
        static LogLines()
        {
            try
            {
                int merged = Apply();
                if (merged > 0 && Prefs.DevMode) Log.Message("[Rimfolk] added log lines from " + merged + " def(s).");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("log lines", e);
            }
        }

        public static int Apply()
        {
            int merged = 0;
            foreach (var def in DefDatabase<RulePackDef>.AllDefsListForReading)
            {
                if (!(def is LogLinesDef ext)) continue;
                try
                {
                    if (ext.interaction != null)
                    {
                        var target = ext.interaction.logRulesInitiator;
                        if (target == null)
                        {
                            CouncilLog.WarnOnce("log lines " + ext.defName + ": " + ext.interaction.defName + " has no log rules to add to");
                            continue;
                        }
                        if (target.include == null) target.include = new List<RulePackDef>();
                        if (target.include.Contains(ext)) continue;
                        target.include.Add(ext);
                        Forget(typeof(RulePack), target, "rulesResolved", "untranslatedRulesResolved");
                    }
                    else if (ext.pack != null && ext.pack != ext)
                    {
                        if (ext.pack.include == null) ext.pack.include = new List<RulePackDef>();
                        if (ext.pack.include.Contains(ext)) continue;
                        ext.pack.include.Add(ext);
                        Forget(typeof(RulePackDef), ext.pack, "cachedRules", "cachedUntranslatedRules");
                    }
                    else continue;
                    merged++;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("log lines " + ext.defName, e);
                }
            }
            return merged;
        }

        /// <summary>Drops rules worked out before the lines were added (normally none are), so they're worked out again with them.</summary>
        private static void Forget(System.Type type, object target, params string[] fields)
        {
            foreach (var name in fields)
            {
                var f = AccessTools.Field(type, name);
                if (f != null && f.FieldType.IsClass) f.SetValue(target, null);
            }
        }
    }
}
