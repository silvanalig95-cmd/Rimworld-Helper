using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// RimWorld of Magic decides a few needs itself, in a prefix on the game's needs check (Pawn_NeedsTracker.ShouldHaveNeed)
    /// that runs late (priority 100): nobody has its travel need (TM_Travel, switched off by the mod since 2021; it is
    /// unfinished, and its middle range throws), the undead have no mana or stamina, and a golem has the needs its golem
    /// lists. Harmony skips a late prefix like that when an earlier prefix on the same check has already answered, and then
    /// all of it goes wrong at once. The player's colonists all had the travel need, which drains for about three weeks at
    /// home and then stays at the bottom, so each felt "Trapped" (-6) for good; and the silk golem lost its energy
    /// (Integrations/MagicGolems.cs). So the mod's own rules are applied again after everything else, and anyone who already
    /// has the travel need loses it: on the first tick after a load, and once a day after that. Found by name; without the
    /// mod nothing is patched.
    /// </summary>
    public static class MagicNeeds
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.magicNeedRules ?? true);

        private static bool looked, ready;
        private static NeedDef travel, mana, stamina;
        private static HediffDef undead;
        private static MethodInfo removeNeed;
        private static bool told;

        public static bool Ready()
        {
            if (looked) return ready;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(MagicGolems.PackageId)) return false;
                travel = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Travel");
                mana = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Mana");
                stamina = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Stamina");
                undead = DefDatabase<HediffDef>.GetNamedSilentFail("TM_UndeadHD");
                removeNeed = AccessTools.Method(typeof(Pawn_NeedsTracker), "RemoveNeed", new[] { typeof(NeedDef) });
                ready = travel != null || (undead != null && (mana != null || stamina != null));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic needs: reading the mod", e);
                ready = false;
            }
            return ready;
        }

        /// <summary>
        /// The mod's own answer for this need, when it has one: false for the travel need, false for mana and stamina on
        /// the undead. Null when the mod leaves the need to the game.
        /// </summary>
        public static bool? Rule(Pawn p, NeedDef nd)
        {
            if (nd == null) return null;
            if (nd == travel) return false;
            if ((nd == mana || nd == stamina) && undead != null && p?.health?.hediffSet != null && p.health.hediffSet.HasHediff(undead)) return false;
            return null;
        }

        /// <summary>
        /// Everyone alive, on the maps, in caravans and in the world, who has the travel need loses it: the game's own
        /// needs pass first (which now follows the mod's rules), then the need taken away directly if that wasn't enough.
        /// Returns how many of the player's colonists lost it.
        /// </summary>
        public static int Sweep()
        {
            if (travel == null) return 0;
            int colonists = 0;
            foreach (var p in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                var needs = p?.needs;
                if (needs == null || needs.TryGetNeed(travel) == null) continue;
                needs.AddOrRemoveNeedsAsAppropriate();
                if (needs.TryGetNeed(travel) != null && removeNeed != null) removeNeed.Invoke(needs, new object[] { travel });
                if (needs.TryGetNeed(travel) == null && p.IsColonist) colonists++;
            }
            if (colonists > 0 && !told)
            {
                told = true;
                Log.Message("[Rimfolk] RimWorld of Magic's travel need (which the mod switches off) had reached " + colonists + " colonists; it is gone again.");
                Messages.Message("CC_MagicNeeds_TravelGone".Translate(colonists.Named("COUNT")).Resolve(), MessageTypeDefOf.PositiveEvent, false);
            }
            return colonists;
        }
    }

    /// <summary>RimWorld of Magic's rules about needs have the last word (Integrations/MagicNeeds.cs).</summary>
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_MagicNeedRules
    {
        public static bool Prepare() => MagicNeeds.Ready();

        public static void Postfix(NeedDef nd, Pawn ___pawn, ref bool __result)
        {
            if (!__result || !MagicNeeds.On) return;
            try
            {
                var rule = MagicNeeds.Rule(___pawn, nd);
                if (rule.HasValue) __result = rule.Value;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic needs: their rules", e);
            }
        }
    }

    /// <summary>
    /// Takes RimWorld of Magic's travel need away from anyone who has it: on the first tick after a load, then once a day
    /// (Integrations/MagicNeeds.cs). Nothing saved.
    /// </summary>
    public class GameComponent_MagicNeeds : GameComponent
    {
        private bool swept;

        public GameComponent_MagicNeeds(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (!MagicNeeds.Ready() || !MagicNeeds.On) return;
            int t = Find.TickManager.TicksGame;
            if (swept && !Sched.Every(t, Sched.DayTicks, Sched.DailyMagicNeeds)) return;
            swept = true;
            try
            {
                MagicNeeds.Sweep();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("magic needs: the travel need", e);
            }
        }
    }
}
