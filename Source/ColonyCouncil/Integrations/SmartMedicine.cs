using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Smart Medicine (memegoddess.smartmedicine, the continued Uuugggg mod; the original reads the same way), read by name.
    /// Its per-injury care is a dictionary from a condition to a medical care (SmartMedicine.PriorityCareSettingsComp's
    /// hediffCare, saved with the game; PriorityCareComp's in the original, not saved), set with a right click on the health
    /// tab. Smart Medicine then does the treating: a condition with a lower choice sorts after the rest (its tend priority
    /// shifted five a step), the medicine is fetched for the first tending's highest choice and only as many as the tendings
    /// that use it, and the next tending looks again, so the plague gets the good medicine and the scratches theirs.
    /// <para>
    /// Rimfolk fills that choice in where the player set none (Health/MedicineChoice.cs's rule, only where it is less than
    /// the patient's medical care: a serious condition keeps the default), right before every choice of medicine (the tend
    /// job, its search for medicine, the tending itself) and when the health tab is drawn. What Rimfolk wrote is kept in
    /// the save (GameComponent_MedicineChoice) by the condition's id: a choice changed by hand, or one Rimfolk never wrote,
    /// is the player's and is never touched, and one taken back to the default stays the default. Rimfolk's own follow the
    /// wound (a cut whose patient is bleeding out, a medical care changed) and are taken out with the setting off.
    /// </para>
    /// Smart Medicine's own temporary raise of a patient's care (DefaultCareFix, when a choice set by hand is above it) is
    /// read through, so the ceiling is the player's setting.
    /// </summary>
    public static class SmartMedicineBridge
    {
        /// <summary>Smart Medicine's Harmony id, both versions.</summary>
        public const string HarmonyId = "uuugggg.rimworld.SmartMedicine.main";

        private static bool looked, present, ready, saves;
        private static Type compType, fixType;
        private static FieldInfo careField;
        private static MethodInfo originalCare;
        private static Game game;
        private static GameComponent comp, fixComp;
        private static readonly object[] one = new object[1];

        /// <summary>Smart Medicine is loaded (any version: its namespace is the same).</summary>
        public static bool Present
        {
            get
            {
                Look();
                return present;
            }
        }

        /// <summary>Smart Medicine is loaded and its per-injury care is as Rimfolk knows it.</summary>
        public static bool Ready
        {
            get
            {
                Look();
                return ready;
            }
        }

        /// <summary>Smart Medicine keeps its per-injury choices in the save (the continued version; the original doesn't).</summary>
        public static bool Saves
        {
            get
            {
                Look();
                return saves;
            }
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                present = GenTypes.GetTypeInAnyAssembly("SmartMedicine.FindBestMedicine") != null || GenTypes.GetTypeInAnyAssembly("SmartMedicine.PriorityCareComp") != null;
                if (!present) return;
                compType = GenTypes.GetTypeInAnyAssembly("SmartMedicine.PriorityCareSettingsComp");
                careField = compType != null ? AccessTools.Field(compType, "hediffCare") : null;
                if (careField == null)
                {
                    compType = GenTypes.GetTypeInAnyAssembly("SmartMedicine.PriorityCareComp");
                    careField = compType != null ? AccessTools.Field(compType, "hediffCare") : null;
                }
                ready = compType != null && typeof(GameComponent).IsAssignableFrom(compType) && careField != null && !careField.IsStatic
                        && careField.FieldType == typeof(Dictionary<Hediff, MedicalCareCategory>);
                // The continued version saves its choices with the game; the original forgot them at each load.
                saves = ready && compType.GetMethod("ExposeData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null;
                fixType = GenTypes.GetTypeInAnyAssembly("SmartMedicine.DefaultCareFix");
                if (fixType != null && typeof(GameComponent).IsAssignableFrom(fixType))
                {
                    originalCare = AccessTools.Method(fixType, "GetOriginalCare", new[] { typeof(Pawn) });
                    if (originalCare != null && (originalCare.IsStatic || originalCare.ReturnType != typeof(MedicalCareCategory?))) originalCare = null;
                }
                if (!ready)
                    Log.Warning("[Rimfolk] Smart Medicine is loaded but its per-injury care isn't as Rimfolk knows it, so medicine by what is treated leaves the choice to Smart Medicine.");
            }
            catch (Exception e)
            {
                ready = false;
                Log.Warning("[Rimfolk] Couldn't read Smart Medicine, so medicine by what is treated leaves the choice to it: " + e.Message);
            }
        }

        private static void Fresh()
        {
            var g = Current.Game;
            if (g == game && comp != null) return;
            game = g;
            comp = g != null && compType != null ? g.GetComponent(compType) : null;
            fixComp = g != null && fixType != null && originalCare != null ? g.GetComponent(fixType) : null;
        }

        /// <summary>Smart Medicine's per-injury choices, or null.</summary>
        private static Dictionary<Hediff, MedicalCareCategory> Dict
        {
            get
            {
                if (!Ready) return null;
                try
                {
                    Fresh();
                    return comp != null ? careField.GetValue(comp) as Dictionary<Hediff, MedicalCareCategory> : null;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("Smart Medicine: its per-injury care", e);
                    return null;
                }
            }
        }

        /// <summary>The patient's own medical care while Smart Medicine has raised it for a tending, or null when it hasn't.</summary>
        public static int? OriginalCare(Pawn p)
        {
            if (!Present || originalCare == null || p == null) return null;
            try
            {
                Fresh();
                if (fixComp == null) return null;
                one[0] = p;
                return originalCare.Invoke(fixComp, one) is MedicalCareCategory c ? (int)c : (int?)null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Smart Medicine: a patient's own medical care", e);
                return null;
            }
            finally
            {
                one[0] = null;
            }
        }

        /// <summary>Smart Medicine's choice for a condition, and whether Rimfolk set it.</summary>
        public static bool EntryOf(Hediff h, out int care, out bool ours)
        {
            care = 0;
            ours = false;
            var d = Dict;
            if (d == null || h == null || !d.TryGetValue(h, out var c)) return false;
            care = (int)c;
            var book = GameComponent_MedicineChoice.Get();
            ours = book != null && h.loadID > 0 && book.filled.TryGetValue(h.loadID, out int written) && written == care;
            return true;
        }

        private static readonly Dictionary<int, (int tick, float time)> refreshed = new Dictionary<int, (int, float)>();
        private static Game refreshedGame;

        /// <summary>
        /// Fills in, follows or takes out Rimfolk's choices for a patient's conditions in Smart Medicine (at most once a tick
        /// for each, and twice a second while the game is paused): none where the player set one, none taken back to the
        /// default, and none where the patient's own care already is what the rule gives.
        /// </summary>
        public static void Refresh(Pawn p)
        {
            if (!Ready || p?.health?.hediffSet == null || ColonyCouncilMod.Settings == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            float time = UnityEngine.Time.realtimeSinceStartup;
            if (refreshedGame != Current.Game)
            {
                refreshedGame = Current.Game;
                refreshed.Clear();
            }
            if (refreshed.TryGetValue(p.thingIDNumber, out var at) && at.tick == now && time - at.time < 0.5f) return;
            if (refreshed.Count > 200) refreshed.Clear();
            refreshed[p.thingIDNumber] = (now, time);
            var d = Dict;
            var book = GameComponent_MedicineChoice.Get();
            if (d == null || book == null) return;
            try
            {
                bool on = MedicineChoice.SettingOn && MedicineChoice.Cared(p);
                int ceiling = on ? MedicineChoice.Ceiling(p) : 0;
                var hs = p.health.hediffSet.hediffs;
                for (int i = 0; i < hs.Count; i++)
                {
                    var h = hs[i];
                    int id = h.loadID;
                    if (id <= 0) continue;
                    // The rule's choice, where it is less than the patient's own care; -1 for none (the default).
                    int want = -1;
                    if (on && ceiling > MedicineChoiceMath.NoMeds && h.TendableNow(ignoreTimer: true))
                    {
                        int c = MedicineChoice.RuleCare(h);
                        if (c < ceiling && c >= MedicineChoiceMath.NoMeds) want = c;
                    }
                    bool has = d.TryGetValue(h, out var cur);
                    bool mine = book.filled.TryGetValue(id, out int written);
                    if (book.handsOff.Contains(id))
                    {
                        if (mine) book.filled.Remove(id);
                        continue;
                    }
                    if (mine && !has)
                    {
                        // Taken back to the default by hand: it stays the player's.
                        book.filled.Remove(id);
                        book.HandsOff(id);
                        continue;
                    }
                    if (mine && (int)cur != written)
                    {
                        // Changed by hand: the player's from now on.
                        book.filled.Remove(id);
                        continue;
                    }
                    if (has && !mine) continue;
                    if (want < 0)
                    {
                        if (mine)
                        {
                            d.Remove(h);
                            book.filled.Remove(id);
                        }
                        continue;
                    }
                    if (mine && written == want) continue;
                    d[h] = (MedicalCareCategory)want;
                    book.filled[id] = want;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Smart Medicine: filling in a patient's per-injury care", e);
            }
        }
    }
}
