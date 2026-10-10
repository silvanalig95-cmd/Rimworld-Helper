using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public class ColonyCouncilMod : Mod
    {
        public const string PackageId = "silvanalig.colonycouncil";

        public static CouncilSettings Settings;

        public ColonyCouncilMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CouncilSettings>();
        }

        public override string SettingsCategory() => "Colony Council";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }

    /// <summary>Logging helpers that never spam the log with the same error.</summary>
    public static class CouncilLog
    {
        public static void ErrorOnce(string context, Exception e)
        {
            Log.ErrorOnce("[Colony Council] " + context + ": " + e, ("ColonyCouncil" + context).GetHashCode());
        }

        public static void WarnOnce(string text)
        {
            Log.WarningOnce("[Colony Council] " + text, ("ColonyCouncilW" + text).GetHashCode());
        }
    }
}
