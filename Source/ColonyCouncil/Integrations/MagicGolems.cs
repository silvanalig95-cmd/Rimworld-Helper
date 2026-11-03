using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// RimWorld of Magic's golems live on their energy, a need of their own (TM_GolemEnergy) that the mod gives them by
    /// patching the game's needs check. A golem can lose it (another mod's needs rule, a version change), and then the
    /// golem's comp reads the missing need every tick (CompGolem.CompTick: Energy.CurLevel), throws, and the golem stands
    /// frozen: it only turns to face where it's ordered, and set dormant it loops its fading forever. The player's silk
    /// golem showed exactly that (its comp's age stuck at 1). So: a golem always keeps the needs its own golem lists
    /// (TM_Golem.needs), and one found without its energy gets it back; the mod then fills it from the workstation's
    /// stored energy as it does on waking (CompGolem.Initialize, ApplyNeeds). If it can't be given back, the comp's tick
    /// is skipped so the golem can at least walk. Found by name; without the mod nothing is patched. Why it happens (the
    /// mod's own needs rule skipped when another mod answers the needs check first) is in Integrations/MagicNeeds.cs.
    /// </summary>
    public static class MagicGolems
    {
        public const string PackageId = "torann.arimworldofmagic";

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.magicGolemRepair ?? true);

        private static bool looked, ready;
        private static Type golemPawn, compGolem;
        private static FieldInfo golemField, needsField, initializedField;
        private static MethodInfo addNeed;
        private static NeedDef energy;
        private static readonly HashSet<int> told = new HashSet<int>();

        public static bool Ready()
        {
            if (looked) return ready;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(PackageId)) return false;
                golemPawn = AccessTools.TypeByName("TorannMagic.Golems.TMPawnGolem");
                compGolem = AccessTools.TypeByName("TorannMagic.Golems.CompGolem");
                var tmGolem = AccessTools.TypeByName("TorannMagic.Golems.TM_Golem");
                golemField = compGolem != null ? AccessTools.Field(compGolem, "golem") : null;
                needsField = tmGolem != null ? AccessTools.Field(tmGolem, "needs") : null;
                initializedField = compGolem != null ? AccessTools.Field(compGolem, "initialized") : null;
                addNeed = AccessTools.Method(typeof(Pawn_NeedsTracker), "AddNeed", new[] { typeof(NeedDef) });
                energy = DefDatabase<NeedDef>.GetNamedSilentFail("TM_GolemEnergy");
                ready = golemPawn != null && compGolem != null && golemField != null && needsField != null && energy != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic golems: reading the mod", e);
                ready = false;
            }
            return ready;
        }

        /// <summary>The golems' energy need (TM_GolemEnergy).</summary>
        public static NeedDef Energy => energy;

        public static bool IsGolem(Pawn p) => p != null && golemPawn != null && golemPawn.IsInstanceOfType(p);

        public static ThingComp CompOf(Pawn p)
        {
            if (p?.AllComps == null) return null;
            foreach (var c in p.AllComps)
                if (c != null && compGolem.IsInstanceOfType(c)) return c;
            return null;
        }

        /// <summary>The needs the golem's own golem lists (TM_Golem.needs), or null.</summary>
        public static IList NeedsOf(Pawn p)
        {
            var comp = CompOf(p);
            var golem = comp != null ? golemField.GetValue(comp) : null;
            return golem != null ? needsField.GetValue(golem) as IList : null;
        }

        /// <summary>
        /// A golem without its energy gets it back: the game's own needs pass (which now keeps the golem's needs), then
        /// the need added directly if that wasn't enough, and the comp set to wake again so the mod fills the energy from
        /// the workstation. True when it has its energy afterwards.
        /// </summary>
        public static bool Repair(Pawn p, ThingComp comp)
        {
            var needs = p?.needs;
            if (needs == null) return false;
            if (needs.TryGetNeed(energy) != null) return true;
            needs.AddOrRemoveNeedsAsAppropriate();
            if (needs.TryGetNeed(energy) == null && addNeed != null) addNeed.Invoke(needs, new object[] { energy });
            bool fixedNow = needs.TryGetNeed(energy) != null;
            if (fixedNow)
            {
                // Its waking sets the energy from the workstation's store (CompGolem.ApplyNeeds).
                if (initializedField != null && comp != null) initializedField.SetValue(comp, false);
                if (told.Add(p.thingIDNumber))
                {
                    Log.Message("[Rimfolk] " + p.LabelShort + " (a RimWorld of Magic golem) had lost its energy need and was frozen; it has it back.");
                    Messages.Message("CC_MagicGolem_Repaired".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.PositiveEvent, false);
                }
            }
            return fixedNow;
        }
    }

    /// <summary>A golem keeps the needs its own golem lists, whatever other rules say (Integrations/MagicGolems.cs).</summary>
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_MagicGolemNeeds
    {
        public static bool Prepare() => MagicGolems.Ready();

        public static void Postfix(NeedDef nd, Pawn ___pawn, ref bool __result)
        {
            if (__result || nd == null || !MagicGolems.On || !MagicGolems.IsGolem(___pawn)) return;
            try
            {
                var list = MagicGolems.NeedsOf(___pawn);
                if (list != null && list.Contains(nd)) __result = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic golems: their needs", e);
            }
        }
    }

    /// <summary>
    /// Before a golem's comp ticks: a golem without its energy gets it back, and if that fails the comp's tick is skipped
    /// so its missing need can't freeze the golem (Integrations/MagicGolems.cs).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_MagicGolemTick
    {
        public static bool Prepare() => MagicGolems.Ready() && TargetMethod() != null;

        public static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("TorannMagic.Golems.CompGolem"), "CompTick");

        public static bool Prefix(ThingComp __instance)
        {
            if (!MagicGolems.On || !(__instance?.parent is Pawn p) || p.needs == null) return true;
            try
            {
                if (p.needs.TryGetNeed(MagicGolems.Energy) != null) return true;
                if (!p.Spawned) return true;
                return MagicGolems.Repair(p, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic golems: their energy", e);
                return true;
            }
        }
    }
}
