using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Faction Territories and Vassalage (jaeger972.factionterritories), read by name (its 1.6 assembly):
    /// <list type="bullet">
    /// <item>who owns a tile: <c>FactionTerritories.TerritoryOwnershipCache.TryGetSingleFactionIfUncontested(int, out int)</c>,
    /// the mod's own cache (nobody for a tile two peoples claim);</item>
    /// <item>settlements being built: its world objects <c>FactionTerritories.Expansion.SettlementConstruction</c>, with
    /// <c>ownerFactionLoadId</c>, <c>createdTick</c> and <c>completeTick</c>;</item>
    /// <item>the colony's vassals: its world objects <c>FactionTerritories.Vassalise.FactionTerritories_VassalOutpost</c>
    /// (the player's), with <c>originalFactionId</c> and <c>originalSettlementName</c>. Their fealty is a view
    /// (WorldMechanics.Ties): the mod's world objects stay the record, so nothing is saved twice or left behind when the
    /// mod is removed.</item>
    /// </list>
    /// Its hooks (Harmony, on the mod's own methods) write the history: a settlement begun (<c>Expansion.Utility.TryCreateForFaction</c>),
    /// finished (<c>TryComplete</c>), beaten by the colony's people (<c>IsDefeated</c>, just before the mod destroys it), a
    /// town taken in an invasion (<c>FactionTerritories.Invasions.Utility.ResolveWithWinner</c>, when the town is the
    /// winner's afterwards), and a town made the colony's vassal (<c>FactionTerritories.Vassalise.VassaliseUtility.ExecuteVassalisation</c>
    /// and <c>ExecuteVassalisationAtTile</c>). Without the mod nothing is looked up or patched.
    /// </summary>
    public static class Territories
    {
        public const string PackageId = "jaeger972.factionterritories";
        public const string ExpansionUtility = "FactionTerritories.Expansion.Utility";
        public const string InvasionsUtility = "FactionTerritories.Invasions.Utility";
        public const string VassaliseUtility = "FactionTerritories.Vassalise.VassaliseUtility";

        private delegate bool UncontestedFn(int tile, out int faction);

        private static bool looked;
        private static Type construction;
        private static Type vassal;
        private static Type invasion;
        private static UncontestedFn uncontested;
        private static FieldInfo siteOwner, siteCreated, siteCompletes;
        private static FieldInfo vassalPeople, vassalName;
        private static FieldInfo invResolved, invSettlement, invVassal, invAttacker;

        public static bool Active => People.ModActive(PackageId);

        /// <summary>One of the mod's methods by its type's and its own name, or null without the mod.</summary>
        public static MethodBase Method(string type, string name)
        {
            if (!Active) return null;
            var t = GenTypes.GetTypeInAnyAssembly(type);
            return t == null ? null : AccessTools.Method(t, name);
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            if (!Active) return;
            construction = GenTypes.GetTypeInAnyAssembly("FactionTerritories.Expansion.SettlementConstruction");
            vassal = GenTypes.GetTypeInAnyAssembly("FactionTerritories.Vassalise.FactionTerritories_VassalOutpost");
            invasion = GenTypes.GetTypeInAnyAssembly("FactionTerritories.Invasions.Invasion");
            var cache = GenTypes.GetTypeInAnyAssembly("FactionTerritories.TerritoryOwnershipCache");
            var owner = cache == null ? null : AccessTools.Method(cache, "TryGetSingleFactionIfUncontested", new[] { typeof(int), typeof(int).MakeByRefType() });
            if (owner != null && owner.ReturnType == typeof(bool)) uncontested = AccessTools.MethodDelegate<UncontestedFn>(owner);
            siteOwner = IntField(construction, "ownerFactionLoadId");
            siteCreated = IntField(construction, "createdTick");
            siteCompletes = IntField(construction, "completeTick");
            vassalPeople = IntField(vassal, "originalFactionId");
            vassalName = vassal == null ? null : AccessTools.Field(vassal, "originalSettlementName");
            if (vassalName != null && vassalName.FieldType != typeof(string)) vassalName = null;
            invResolved = invasion == null ? null : AccessTools.Field(invasion, "resolved");
            if (invResolved != null && invResolved.FieldType != typeof(bool)) invResolved = null;
            invSettlement = IntField(invasion, "settlementId");
            invVassal = IntField(invasion, "vassalOutpostId");
            invAttacker = IntField(invasion, "attackerFactionLoadId");
        }

        private static FieldInfo IntField(Type t, string name)
        {
            var f = t == null ? null : AccessTools.Field(t, name);
            return f != null && f.FieldType == typeof(int) ? f : null;
        }

        /// <summary>Anything of the mod's was found to read.</summary>
        public static bool Ready
        {
            get
            {
                Look();
                return uncontested != null || construction != null && siteOwner != null || vassal != null;
            }
        }

        private static int Int(FieldInfo f, object o, int fallback = -1) => f != null && o != null ? (int)f.GetValue(o) : fallback;

        private static Faction FactionOf(int loadId) => loadId < 0 ? null : Refs.Faction(Ref.People(loadId));

        // ---------------- Reading ----------------

        public static bool Owner(PlanetTile tile, out Faction owner)
        {
            owner = null;
            Look();
            if (uncontested == null) return false;
            // The mod's territories lie on the planet's surface only.
            if (Find.WorldGrid == null || tile.Layer != Find.WorldGrid.Surface) return true;
            if (uncontested(tile.tileId, out int id)) owner = FactionOf(id);
            return true;
        }

        public static void Building(List<BuildingSite> into)
        {
            Look();
            if (construction == null || siteOwner == null) return;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
            {
                if (o == null || o.Destroyed || !construction.IsInstanceOfType(o)) continue;
                into.Add(new BuildingSite
                {
                    id = o.ID, tile = o.Tile, place = Refs.Of(o.Tile), people = FactionOf(Int(siteOwner, o)), started = Int(siteCreated, o, Tie.Never),
                    completes = Int(siteCompletes, o, Tie.Never)
                });
            }
        }

        public static void Holdings(List<Holding> into)
        {
            Look();
            if (vassal == null) return;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
            {
                if (o == null || o.Destroyed || !vassal.IsInstanceOfType(o) || o.Faction == null || !o.Faction.IsPlayer) continue;
                string name = vassalName?.GetValue(o) as string;
                into.Add(new Holding
                {
                    kind = HoldingKind.Vassal, thing = o, id = o.ID, tile = o.Tile, name = name.NullOrEmpty() ? o.LabelCap : name, people = FactionOf(Int(vassalPeople, o))
                });
            }
        }

        /// <summary>The newest of the mod's vassals on a tile, or null.</summary>
        private static WorldObject VassalAt(PlanetTile tile)
        {
            Look();
            if (vassal == null || !tile.Valid) return null;
            var all = Find.WorldObjects.AllWorldObjects;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var o = all[i];
                if (o != null && !o.Destroyed && o.Tile == tile && vassal.IsInstanceOfType(o)) return o;
            }
            return null;
        }

        private static WorldObject ById(int id)
        {
            if (id < 0) return null;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
                if (o != null && o.ID == id)
                    return o;
            return null;
        }

        // ---------------- What the hooks write ----------------

        /// <summary>A settlement begun (TryCreateForFaction succeeded): the site the mod just added for this people now.</summary>
        public static void Founded(Faction people, int now)
        {
            Look();
            if (people == null || construction == null || siteOwner == null) return;
            var all = Find.WorldObjects.AllWorldObjects;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var o = all[i];
                if (o == null || o.Destroyed || !construction.IsInstanceOfType(o) || Int(siteOwner, o) != people.loadID || Int(siteCreated, o) != now) continue;
                WorldMechanics.Founded(people, o.Tile, o.ID, Int(siteCreated, o, Tie.Never), Int(siteCompletes, o, Tie.Never));
                return;
            }
        }

        /// <summary>A settlement finished (TryComplete succeeded): the new town on the site's tile.</summary>
        public static void Built(object site)
        {
            Look();
            if (!(site is WorldObject o) || siteOwner == null) return;
            var people = FactionOf(Int(siteOwner, o));
            var town = Find.WorldObjects.SettlementAt(o.Tile);
            if (town != null && town.Faction == people) WorldMechanics.Built(people, town, o.ID);
        }

        /// <summary>A site beaten (IsDefeated said so, and the mod destroys it next): razed by the colony's people, who alone can attack it.</summary>
        public static void Razed(object site)
        {
            Look();
            if (!(site is WorldObject o) || o.Destroyed || siteOwner == null) return;
            WorldMechanics.Razed(FactionOf(Int(siteOwner, o)), o.Tile, o.ID);
        }

        /// <summary>What an invasion was about before its winner was named.</summary>
        public sealed class Before
        {
            public bool resolved;
            public bool vassal;
            public int targetId = -1;
            public PlanetTile tile = PlanetTile.Invalid;
            public Faction owner;
            public string name;
            public int attacker = -1;
        }

        public static Before BeforeWinner(object inv)
        {
            Look();
            if (inv == null || invResolved == null || invAttacker == null) return null;
            var b = new Before { resolved = (bool)invResolved.GetValue(inv), attacker = Int(invAttacker, inv) };
            if (b.resolved) return b;
            int vassalId = Int(invVassal, inv);
            b.vassal = vassalId >= 0;
            b.targetId = b.vassal ? vassalId : Int(invSettlement, inv);
            var target = ById(b.targetId);
            if (target == null) return b;
            b.tile = target.Tile;
            b.owner = target.Faction;
            b.name = target is Settlement s && s.HasName ? s.Name : vassalName != null && b.vassal ? vassalName.GetValue(target) as string : null;
            return b;
        }

        /// <summary>An invasion's winner named: when the attacker won and the town there is theirs now, it was taken.</summary>
        public static void AfterWinner(Before b, Faction winner)
        {
            if (b == null || b.resolved || winner == null || winner.loadID != b.attacker || !b.tile.Valid) return;
            var town = Find.WorldObjects.SettlementAt(b.tile);
            if (town == null || town.Faction != winner) return;
            bool ours = b.vassal || b.owner != null && b.owner.IsPlayer;
            WorldMechanics.Taken(winner, ours ? null : b.owner, ours, town, b.targetId, b.name);
        }

        /// <summary>A town made the colony's vassal: the vassal the mod put on its tile.</summary>
        public static void Vassal(PlanetTile tile, Faction from, int formerId, string name)
        {
            var v = VassalAt(tile);
            if (v != null) WorldMechanics.MadeVassal(from, v, formerId, name);
        }

        public static Faction PeopleOf(int loadId) => FactionOf(loadId);

        // ---------------- Calls to arms (Diplomacy/CallsToArms.cs) ----------------

        private static MethodInfo findForSettlement, enter;
        private static Type joinFight;
        private static bool lookedFight;

        private static void LookFight()
        {
            if (lookedFight) return;
            lookedFight = true;
            if (!Active) return;
            var util = GenTypes.GetTypeInAnyAssembly(InvasionsUtility);
            invasion ??= GenTypes.GetTypeInAnyAssembly("FactionTerritories.Invasions.Invasion");
            findForSettlement = util == null ? null : AccessTools.Method(util, "FindForSettlement", new[] { typeof(Settlement) });
            if (findForSettlement != null && (invasion == null || !invasion.IsAssignableFrom(findForSettlement.ReturnType))) findForSettlement = null;
            enter = util == null || invasion == null ? null : AccessTools.Method(util, "Enter", new[] { typeof(Caravan), typeof(Settlement), invasion });
            joinFight = GenTypes.GetTypeInAnyAssembly("FactionTerritories.Invasions.ArrivalAction_JoinFight");
        }

        /// <summary>The mod's invasion of this town still being fought, or null.</summary>
        private static object LiveInvasion(Settlement town)
        {
            LookFight();
            Look();
            if (town == null || findForSettlement == null) return null;
            var inv = findForSettlement.Invoke(null, new object[] { town });
            if (!(inv is WorldObject wo) || wo.Destroyed) return null;
            if (invResolved != null && (bool)invResolved.GetValue(inv)) return null;
            return inv;
        }

        /// <summary>The mod's invasion of a town under way, with its attacker: a call to arms for the town's people.</summary>
        public static bool InvasionAt(Settlement town, out Faction attacker)
        {
            attacker = null;
            try
            {
                var inv = LiveInvasion(town);
                if (inv == null) return false;
                attacker = FactionOf(Int(invAttacker, inv));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: an invasion read", e);
                return false;
            }
        }

        /// <summary>
        /// The colony's caravan at a town the mod's invasion is fighting over joins that fight through the mod's own arrival
        /// (its "Join fight"). False without the mod, or with no such fight there now.
        /// </summary>
        public static bool JoinFight(Caravan c, Settlement town)
        {
            try
            {
                var inv = LiveInvasion(town);
                if (inv == null || c == null || c.Tile != town.Tile && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) > 1.01f) return false;
                if (joinFight != null && Activator.CreateInstance(joinFight, town) is CaravanArrivalAction arrival)
                {
                    if (c.Tile != town.Tile) return false;
                    arrival.Arrived(c);
                    return true;
                }
                if (enter == null || c.Tile != town.Tile) return false;
                enter.Invoke(null, new object[] { c, town, inv });
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: joining an invasion's fight", e);
                return false;
            }
        }
    }

    /// <summary>Faction Territories and Vassalage's world mechanics behind the one interface (see <see cref="Territories"/>).</summary>
    public sealed class TerritoriesAdapter : WorldAdapter
    {
        public override string Name => "Faction Territories and Vassalage";

        public override bool Ready() => Territories.Ready;

        public override bool Owner(PlanetTile tile, out Faction owner) => Territories.Owner(tile, out owner);

        public override void Building(List<BuildingSite> into) => Territories.Building(into);

        public override void Holdings(List<Holding> into) => Territories.Holdings(into);
    }

    /// <summary>A people began building a settlement (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldFounded
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.ExpansionUtility, "TryCreateForFaction");

        public static void Postfix(Faction __0, int __1, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                Territories.Founded(__0, __1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a settlement begun", e);
            }
        }
    }

    /// <summary>A settlement being built was finished (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldBuilt
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.ExpansionUtility, "TryComplete");

        public static void Postfix(object __0, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                Territories.Built(__0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a settlement finished", e);
            }
        }
    }

    /// <summary>A settlement being built was beaten by the colony's people (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldRazed
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.ExpansionUtility, "IsDefeated");

        public static void Postfix(object __0, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                Territories.Razed(__0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a settlement razed", e);
            }
        }
    }

    /// <summary>An invasion's winner named; a town taken (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldTaken
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.InvasionsUtility, "ResolveWithWinner");

        public static void Prefix(object __0, out Territories.Before __state)
        {
            __state = null;
            if (!RimfolkPatches.On) return;
            try
            {
                __state = Territories.BeforeWinner(__0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: an invasion's end", e);
            }
        }

        public static void Postfix(Faction __2, Territories.Before __state)
        {
            if (__state == null) return;
            try
            {
                Territories.AfterWinner(__state, __2);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a town taken", e);
            }
        }
    }

    /// <summary>A town bought or taken as the colony's vassal (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldVassal
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.VassaliseUtility, "ExecuteVassalisation");

        public static void Prefix(Settlement __0, out (Faction from, PlanetTile tile, int id, string name) __state) =>
            __state = __0 == null ? (null, PlanetTile.Invalid, -1, null) : (__0.Faction, __0.Tile, __0.ID, __0.HasName ? __0.Name : null);

        public static void Postfix(bool __result, (Faction from, PlanetTile tile, int id, string name) __state)
        {
            if (!__result || !RimfolkPatches.On || !__state.tile.Valid) return;
            try
            {
                Territories.Vassal(__state.tile, __state.from, __state.id, __state.name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a vassal made", e);
            }
        }
    }

    /// <summary>A town the colony destroyed made its vassal instead (Integrations/World/TerritoriesAdapter.cs): written in the history.</summary>
    [HarmonyPatch]
    public static class Patch_WorldVassalAtTile
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod() => Territories.Method(Territories.VassaliseUtility, "ExecuteVassalisationAtTile");

        public static void Postfix(int __0, int __1, string __2, bool __result)
        {
            if (!__result || !RimfolkPatches.On || __0 < 0) return;
            try
            {
                Territories.Vassal(__0, Territories.PeopleOf(__1), -1, __2);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a vassal made", e);
            }
        }
    }
}
