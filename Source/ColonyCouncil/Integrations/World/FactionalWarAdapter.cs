using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// [SR] Factional War (fork) (SR.ModRimworld.FactionalWarContinued), read by name (its 1.6 assembly, namespace
    /// <c>SR.ModRimWorld.FactionalWar</c>): two hostile peoples brought to fight each other on one of the colony's homes.
    /// <list type="bullet">
    /// <item>The battle begins: <c>IncidentWorkerFactionWar.TryExecuteWorker(IncidentParms)</c> succeeded on a home; its
    /// private fields <c>_faction1</c> and <c>_faction2</c> are the two peoples.</item>
    /// <item>Who won, when its own trigger tells: <c>TriggerFactionAssaultVictory.ActivateOn(Lord, TriggerSignal)</c> true
    /// (every fighter of the people in its private field <c>_targetFaction</c> on the map is down, dead or taken), so the
    /// lord's people won; the mod says so in its "SrAssaultFactionVictory" message at the same moment.</item>
    /// </list>
    /// Its other fights (the shelling, the contested site, the camp) happen on sites away from the homes and aren't
    /// written. Without the mod nothing is looked up or patched.
    /// </summary>
    public static class FactionalWar
    {
        public const string PackageId = "SR.ModRimworld.FactionalWarContinued";
        private const string Namespace = "SR.ModRimWorld.FactionalWar.";

        private static bool looked;
        private static FieldInfo first, second, target;

        public static bool Active => People.ModActive(PackageId);

        /// <summary>One of the mod's types by name (its assembly spells the namespace "ModRimWorld", whatever its XML writes), or null without the mod.</summary>
        public static Type TypeOf(string name) => Active ? GenTypes.GetTypeInAnyAssembly(Namespace + name) : null;

        public static MethodBase Method(string type, string name) => TypeOf(type) is Type t ? AccessTools.Method(t, name) : null;

        private static FieldInfo FactionField(Type t, string name)
        {
            var f = t == null ? null : AccessTools.Field(t, name);
            return f != null && f.FieldType == typeof(Faction) ? f : null;
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            var incident = TypeOf("IncidentWorkerFactionWar");
            first = FactionField(incident, "_faction1");
            second = FactionField(incident, "_faction2");
            target = FactionField(TypeOf("TriggerFactionAssaultVictory"), "_targetFaction");
        }

        public static bool Ready
        {
            get
            {
                Look();
                return first != null && second != null;
            }
        }

        /// <summary>The battle began on this map: the incident's two peoples.</summary>
        public static void Began(object worker, IncidentParms parms)
        {
            Look();
            if (worker == null || first == null || second == null || !(parms?.target is Map map) || !Homes.IsHome(map)) return;
            WorldMechanics.Battle(first.GetValue(worker) as Faction, second.GetValue(worker) as Faction, map);
        }

        /// <summary>The lord's people beat the people its trigger watches, on this map.</summary>
        public static void Victory(object trigger, Lord lord)
        {
            Look();
            if (trigger == null || target == null || lord?.faction == null) return;
            var map = lord.Map;
            if (!Homes.IsHome(map)) return;
            WorldMechanics.Won(lord.faction, target.GetValue(trigger) as Faction, map);
        }
    }

    /// <summary>
    /// Factional War behind the one interface (see <see cref="FactionalWar"/>): it has no standing state to read, only the
    /// battles its hooks write.
    /// </summary>
    public sealed class FactionalWarAdapter : WorldAdapter
    {
        public override string Name => "Factional War";

        public override bool Ready() => FactionalWar.Ready;
    }

    /// <summary>Two peoples came to fight on one of the colony's homes (Integrations/World/FactionalWarAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldBattle
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => FactionalWar.Method("IncidentWorkerFactionWar", "TryExecuteWorker");

        public static void Postfix(object __instance, IncidentParms __0, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                FactionalWar.Began(__instance, __0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a battle begun", e);
            }
        }
    }

    /// <summary>One of them won, as the mod's own trigger tells (Integrations/World/FactionalWarAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldBattleWon
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => FactionalWar.Method("TriggerFactionAssaultVictory", "ActivateOn");

        public static void Postfix(object __instance, Lord __0, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                FactionalWar.Victory(__instance, __0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a battle won", e);
            }
        }
    }
}
