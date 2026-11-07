using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Carrying and hauling's links to other mods, all soft: the player's standalone Harvest and Haul (while it is loaded
    /// Rimfolk's copy of it stands down completely, and once it is gone its settings file is read in once); Pick Up And
    /// Haul (what Rimfolk hands over to storage goes through its own unloading, its comp that tracks hauled items read by
    /// name); Combat Extended (its bulk and weight limits, read by reflection); Haul to Stack (nothing: storage is always
    /// found the game's way, so whatever it changes there applies).
    /// </summary>
    public static class HaulLinks
    {
        public const string StandaloneId = "silvanalig95.harvestandhaul";
        public const string StandaloneType = "HarvestAndHaul.HarvestTracker";
        public const string PuahId = "mehni.pickupandhaul";

        private static bool? standalone;

        /// <summary>The standalone Harvest and Haul is loaded (by its package id or its tracker's class): Rimfolk's copy stands down.</summary>
        public static bool Standalone => standalone ??= FindStandalone();

        private static bool FindStandalone()
        {
            try
            {
                return People.ModActive(StandaloneId) || GenTypes.GetTypeInAnyAssembly(StandaloneType) != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harvest and haul check", e);
                return false;
            }
        }

        /// <summary>Why the harvest and haul hooks stand down, for "Rimfolk: check myself" (null while they are in use).</summary>
        public static string StandsDownWhy => Standalone ? "Harvest and Haul is loaded on its own, so Rimfolk's copy of it stands down" : null;

        private static bool told;

        /// <summary>Once a session, at startup: the log line about the standalone mod, or the one-time read of its settings once it is gone.</summary>
        public static void Startup()
        {
            if (told) return;
            told = true;
            try
            {
                if (Standalone)
                {
                    Log.Message("[Rimfolk] Harvest and Haul is loaded on its own. Rimfolk has it built in now (Mod settings, Rimfolk, What's on, Carrying and hauling), "
                                + "so Rimfolk's copy stands down while the standalone mod is loaded. The standalone mod can be removed now; Rimfolk reads its settings once it is gone, "
                                + "and takes over what pawns still carry for it in a save. Only then do bills, production buildings and other work fill pockets too.");
                    return;
                }
                ImportStandaloneSettings();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harvest and haul startup", e);
            }
        }

        /// <summary>
        /// The standalone mod's settings file (Mod_..._HarvestAndHaulMod.xml in the game's Config folder), read into
        /// Rimfolk's rows once, the first time Rimfolk starts without the mod. The game keeps a removed mod's file.
        /// </summary>
        private static void ImportStandaloneSettings()
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || s.haulImported) return;
            s.haulImported = true;
            string folder = GenFilePaths.ConfigFolderPath;
            string file = Directory.Exists(folder) ? Directory.GetFiles(folder, "Mod_*_HarvestAndHaulMod.xml").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
            int read = 0;
            if (file != null)
            {
                var values = HaulMath.ReadStandaloneSettings(File.ReadAllText(file));
                foreach (var kv in values)
                {
                    var f = typeof(CouncilSettings).GetField(kv.Key);
                    if (f == null) continue;
                    if (f.FieldType == typeof(bool) && bool.TryParse(kv.Value, out bool b)) f.SetValue(s, b);
                    else if (f.FieldType == typeof(float) && float.TryParse(kv.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                        f.SetValue(s, HaulMath.ImportNumber(kv.Key, v));
                    else continue;
                    read++;
                }
                if (read > 0) Log.Message("[Rimfolk] Read " + read + " settings of the standalone Harvest and Haul into Rimfolk's carrying and hauling.");
            }
            s.Write();
        }

        // ================= Pick Up And Haul =================

        private static bool puahTried;
        private static Type puahComp;
        private static MethodInfo puahRegister, puahSet;
        private static JobDef puahUnload;

        /// <summary>Pick Up And Haul's comp that tracks hauled items and its unloading job are both there (found by name, whichever copy of the mod).</summary>
        public static bool Puah
        {
            get
            {
                if (!puahTried) FindPuah();
                return puahRegister != null && puahUnload != null;
            }
        }

        /// <summary>Pick Up And Haul unloads for this pawn: its own domain is the colony's humanlikes, so a mechanoid's load stays with Rimfolk.</summary>
        public static bool PuahFor(Pawn pawn) => pawn?.RaceProps != null && pawn.RaceProps.Humanlike && Puah;

        private static void FindPuah()
        {
            puahTried = true;
            try
            {
                puahComp = GenTypes.GetTypeInAnyAssembly("PickUpAndHaul.CompHauledToInventory");
                if (puahComp == null) return;
                puahRegister = puahComp.GetMethod("RegisterHauledItem", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Thing) }, null);
                puahSet = puahComp.GetMethod("GetHashSet", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                puahUnload = DefDatabase<JobDef>.GetNamedSilentFail("UnloadYourHauledInventory");
                if (puahRegister == null || puahUnload == null)
                {
                    puahRegister = null;
                    Log.Warning("[Rimfolk] Pick Up And Haul is loaded but its hauled-items comp or unloading job isn't the one Rimfolk knows, so Rimfolk takes its own loads to storage.");
                }
            }
            catch (Exception e)
            {
                puahRegister = null;
                CouncilLog.ErrorOnce("pick up and haul link", e);
            }
        }

        private static ThingComp PuahCompOf(Pawn pawn)
        {
            if (pawn == null || !Puah) return null;
            var comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (puahComp.IsInstanceOfType(comps[i]))
                    return comps[i];
            return null;
        }

        /// <summary>Hands a stack in the pawn's inventory to Pick Up And Haul's unloading; false when it can't take it.</summary>
        public static bool HandToPuah(Pawn pawn, Thing thing)
        {
            var comp = PuahCompOf(pawn);
            if (comp == null || thing == null) return false;
            try
            {
                puahRegister.Invoke(comp, new object[] { thing });
                return true;
            }
            catch (Exception e)
            {
                puahRegister = null;
                CouncilLog.ErrorOnce("pick up and haul hand-over", e);
                return false;
            }
        }

        /// <summary>How many stacks Pick Up And Haul is set to unload for the pawn.</summary>
        public static int PuahCount(Pawn pawn)
        {
            var comp = PuahCompOf(pawn);
            if (comp == null || puahSet == null) return 0;
            try
            {
                return puahSet.Invoke(comp, null) is ICollection c ? c.Count : 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pick up and haul count", e);
                return 0;
            }
        }

        /// <summary>Pick Up And Haul's own unloading job for the pawn, or null without the mod.</summary>
        public static Job PuahUnloadJob(Pawn pawn) => Puah ? JobMaker.MakeJob(puahUnload, pawn) : null;

        public static bool IsPuahUnload(Job job) => job != null && puahUnload != null && job.def == puahUnload;

        // ================= Combat Extended =================

        private static bool ceTried;
        private static Type ceInventory;
        private static MethodInfo ceCanFit;

        /// <summary>With Combat Extended, no more than its bulk and weight limits let the pawn carry (the standalone mod's own way, kept).</summary>
        public static int CeLimit(Pawn pawn, Thing thing, int count)
        {
            if (!ceTried) FindCe();
            if (ceCanFit == null || count <= 0 || pawn == null) return count;
            try
            {
                ThingComp comp = null;
                var comps = pawn.AllComps;
                for (int i = 0; i < comps.Count; i++)
                    if (ceInventory.IsInstanceOfType(comps[i]))
                    {
                        comp = comps[i];
                        break;
                    }
                if (comp == null) return count;
                var parameters = ceCanFit.GetParameters();
                var args = new object[parameters.Length];
                args[0] = thing;
                args[1] = 0;
                for (int i = 2; i < parameters.Length; i++) args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                bool fits = (bool)ceCanFit.Invoke(comp, args);
                return fits ? Math.Min(count, (int)args[1]) : 0;
            }
            catch (Exception e)
            {
                ceCanFit = null;
                Log.Warning("[Rimfolk] Combat Extended's inventory check failed, so carrying goes by the game's own mass limits: " + e.Message);
                return count;
            }
        }

        private static void FindCe()
        {
            ceTried = true;
            try
            {
                ceInventory = GenTypes.GetTypeInAnyAssembly("CombatExtended.CompInventory");
                if (ceInventory == null) return;
                foreach (var m in ceInventory.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "CanFitInInventory" || m.ReturnType != typeof(bool)) continue;
                    var p = m.GetParameters();
                    bool restOptional = true;
                    for (int i = 2; i < p.Length; i++) restOptional &= p[i].HasDefaultValue;
                    if (p.Length >= 2 && typeof(Thing).IsAssignableFrom(p[0].ParameterType) && p[1].IsOut && p[1].ParameterType.GetElementType() == typeof(int) && restOptional)
                    {
                        ceCanFit = m;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                ceCanFit = null;
                CouncilLog.ErrorOnce("combat extended link", e);
            }
        }
    }

    /// <summary>Carrying and hauling's start: the standalone mod's line in the log, or the one-time read of its settings.</summary>
    [StaticConstructorOnStartup]
    public static class HaulStartup
    {
        static HaulStartup()
        {
            HaulLinks.Startup();
        }
    }
}
