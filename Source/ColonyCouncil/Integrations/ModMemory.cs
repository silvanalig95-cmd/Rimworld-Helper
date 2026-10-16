using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Other mods' doings in what the world's peoples remember of the colony (World/FactionMemory.cs), read from the
    /// goodwill reasons those mods give the game, by name:
    /// <list type="bullet">
    /// <item>Hospitality: visitors who go home delighted remember a warm welcome; visitors who go home disappointed
    /// remember a poor one (its "VisitBetterThanExpected" and "VisitWorseThanExpected"). A run of visits adds up.</item>
    /// <item>Vanilla Factions Expanded - Classical: a republic whose every senator the colony has won over remembers it
    /// (its "VFEC_GainedFavor").</item>
    /// </list>
    /// The deed kinds are added to the memory's own list when the game starts; their texts are CC_Memory_Deed_kind. With
    /// the mods missing, those reasons never come and nothing is noted.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModMemory
    {
        public const string VisitDelighted = "visitDelighted";
        public const string VisitDisappointed = "visitDisappointed";
        public const string SenateWon = "senateWon";

        static ModMemory()
        {
            try
            {
                Register();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory kinds for other mods", e);
            }
        }

        /// <summary>The deed kinds, beside the memory's own (never replacing one).</summary>
        public static void Register()
        {
            Add(VisitDelighted, DeedSide.Favor, 6, 20f, 20);
            Add(VisitDisappointed, DeedSide.Wrong, 6, 20f, 20);
            Add(SenateWon, DeedSide.Favor, 20);
        }

        private static void Add(string kind, DeedSide side, int weight, float mergeDays = 0f, int max = MemoryMath.MaxWeight)
        {
            if (FactionMemory.Kinds.ContainsKey(kind)) return;
            FactionMemory.Kinds[kind] = new DeedKind { side = side, weight = weight, mergeDays = mergeDays, max = max };
        }

        /// <summary>The game changed a people's goodwill for a reason another mod named.</summary>
        public static void Goodwill(Faction a, Faction b, int change, HistoryEventDef reason)
        {
            var f = a != null && a.IsPlayer ? b : b != null && b.IsPlayer ? a : null;
            if (f == null || reason == null || !FactionMemory.Remembers(f)) return;
            switch (reason.defName)
            {
                case "VisitBetterThanExpected":
                    // Hospitality's change is 5 to 24 with the size of the visit and how well it went.
                    FactionMemory.Note(f, VisitDelighted, null, Mathf.Clamp(2 + Mathf.Abs(change) / 2, 4, 14));
                    break;
                case "VisitWorseThanExpected":
                    FactionMemory.Note(f, VisitDisappointed, null, Mathf.Clamp(2 + Mathf.Abs(change) / 2, 4, 14));
                    break;
                case "VFEC_GainedFavor":
                    FactionMemory.Note(f, SenateWon);
                    break;
            }
        }
    }

    /// <summary>Goodwill changed for a reason another mod names (beside the memory's own hook, which reads the game's reasons).</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
    public static class Patch_ModMemoryGoodwill
    {
        public static void Postfix(Faction __instance, Faction other, int goodwillChange, HistoryEventDef reason)
        {
            if (reason == null || !MemoryHooks.Listening) return;
            try
            {
                ModMemory.Goodwill(__instance, other, goodwillChange, reason);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory hook for other mods", e);
            }
        }
    }
}
