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
            // The game hooks are applied later, once every def has loaded (see RimfolkPatchesStartup).
        }

        public override string SettingsCategory() => "Rimfolk";

        /// <summary>Saved when the settings window closes: settings that change def data are applied again here.</summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            try
            {
                HappeningTracker.ApplyEventMode();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event pools", e);
            }
        }

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
            Log.ErrorOnce("[Rimfolk] " + context + ": " + e, ("ColonyCouncil" + context).GetHashCode());
        }

        public static void WarnOnce(string text)
        {
            Log.WarningOnce("[Rimfolk] " + text, ("ColonyCouncilW" + text).GetHashCode());
        }
    }

    /// <summary>Saving helpers for things that may come from mods the player removes mid-save.</summary>
    public static class CouncilScribe
    {
        /// <summary>
        /// Like <c>Scribe_Defs.Look</c> (and saved the same way, so old saves load unchanged), but a def that no
        /// longer exists (its mod was removed) quietly loads as null instead of logging an error.
        /// <paramref name="lost"/> becomes true when a def was saved but couldn't be found again.
        /// </summary>
        public static void LookDef<T>(ref T value, string label, ref bool lost) where T : Def, new()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                string name = value?.defName ?? "null";
                Scribe_Values.Look(ref name, label, "null");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string name = null;
                Scribe_Values.Look(ref name, label);
                value = null;
                if (name.NullOrEmpty() || name == "null") return;
                value = DefDatabase<T>.GetNamedSilentFail(BackCompatibility.BackCompatibleDefName(typeof(T), name, false, null));
                if (value == null) lost = true;
            }
        }

        public static void LookDef<T>(ref T value, string label) where T : Def, new()
        {
            bool lost = false;
            LookDef(ref value, label, ref lost);
        }
    }
}
