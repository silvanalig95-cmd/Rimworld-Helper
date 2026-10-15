using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // How deeds come into the peoples' memory (World/FactionMemory.cs). Each hook only reads what the game is doing and
    // writes a line in the memory; none changes what the game does. Off with the colony's memory switched off, or the
    // deeper game hooks.

    /// <summary>Where the memory's hooks look things up.</summary>
    public static class MemoryHooks
    {
        public static bool Listening => RimfolkPatches.On && FactionMemory.On;

        /// <summary>Someone of a people leaving the colony's map right now (their name, for a release or a wounded guest sent home).</summary>
        internal static Pawn exiting;

        /// <summary>The people and the colony in a goodwill change, whichever way round the game wrote it.</summary>
        private static Faction Other(Faction a, Faction b) => a != null && a.IsPlayer ? b : b != null && b.IsPlayer ? a : null;

        private static string NameOf(GlobalTargetInfo? look)
        {
            if (look.HasValue && look.Value.HasThing && look.Value.Thing is Pawn p) return p.LabelShort;
            if (look.HasValue && look.Value.HasWorldObject && look.Value.WorldObject is Settlement s && s.HasName) return s.Name;
            return null;
        }

        /// <summary>A trader of theirs: in a caravan come to trade, or carrying their goods.</summary>
        private static bool Trader(Pawn p)
        {
            if (p == null) return false;
            var job = p.GetLord()?.LordJob;
            return job is LordJob_TradeWithColony || p.trader?.traderKind != null;
        }

        /// <summary>The game changed a people's goodwill for a reason it names: the reasons worth remembering become deeds.</summary>
        public static void Goodwill(Faction a, Faction b, int change, HistoryEventDef reason, GlobalTargetInfo? look)
        {
            var f = Other(a, b);
            if (f == null || reason == null || !FactionMemory.Remembers(f)) return;
            string name = NameOf(look);
            switch (reason.defName)
            {
                case "MemberExitedMapHealthy":
                    FactionMemory.Note(f, "released", exiting?.LabelShort ?? name);
                    break;
                case "FriendlyExitedMapTended":
                    FactionMemory.Note(f, "tended", exiting?.LabelShort ?? name);
                    break;
                case "GaveGift":
                    FactionMemory.Note(f, "gift", null, UnityEngine.Mathf.Clamp(change, 3, 25));
                    break;
                case "RequestedMilitaryAid":
                    FactionMemory.Note(f, "aid");
                    break;
                case "PeaceTalksSuccess":
                    FactionMemory.Note(f, "peaceTalks");
                    break;
                case "PeaceTalksTriumph":
                    FactionMemory.Note(f, "peaceTalks", null, 12);
                    break;
                case "PeaceTalksBackfire":
                    FactionMemory.Note(f, "peaceTalksBad");
                    break;
                case "PeaceTalksDisaster":
                    FactionMemory.Note(f, "peaceTalksBad", null, 10);
                    break;
                case "AttackedMember":
                case "UsedHarmfulAbility":
                case "UsedHarmfulItem":
                {
                    var p = look.HasValue && look.Value.HasThing ? look.Value.Thing as Pawn : null;
                    if (Trader(p)) FactionMemory.Note(f, "robbed");
                    else FactionMemory.Note(f, "attacked", name);
                    break;
                }
                case "AttackedCaravan":
                    FactionMemory.Note(f, "caravanRaided");
                    break;
                case "MemberCaptured":
                    FactionMemory.Note(f, "captured", name);
                    break;
                case "MemberSold":
                case "SoldSlave":
                    FactionMemory.Note(f, "sold", name);
                    break;
                case "AttackedSettlement":
                case "PlayerRaidedSomeone":
                    FactionMemory.Note(f, "townAttacked", name);
                    break;
                case "DestroyedEnemyBase":
                    FactionMemory.Note(f, "townDestroyed", name);
                    break;
                case "ToxicWasteDumping":
                case "PollutedBase":
                case "PollutedNearbySite":
                    FactionMemory.Note(f, "polluted");
                    break;
            }
        }

        /// <summary>Who dealt the blow: the colony, if it was one of its people, animals or turrets.</summary>
        private static Faction Killer(DamageInfo? dinfo) => dinfo?.Instigator?.Faction;

        /// <summary>
        /// Someone of a people died. In a raid on the colony, or out on their own ground at the colony's hands: their dead.
        /// At peace: a killing. A prisoner of the colony's: a death in its hands. A colonist killed by a people: the
        /// colony's grievance.
        /// </summary>
        public static void Died(Faction f, Pawn member, DamageInfo? dinfo, Map map, DeathState before)
        {
            if (f == null || member == null || !member.RaceProps.Humanlike) return;
            if (f.IsPlayer)
            {
                var them = Killer(dinfo);
                if (them != null && FactionMemory.Remembers(them) && them.HostileTo(Faction.OfPlayer) && before.colonist)
                    FactionMemory.Note(them, "killedOurs", member.LabelShort);
                return;
            }
            if (!FactionMemory.Remembers(f)) return;
            if (before.prisoner)
            {
                FactionMemory.Note(f, "prisonerDied", member.LabelShort);
                return;
            }
            var killer = Killer(dinfo);
            if (killer == null || !killer.IsPlayer) return;
            var mem = FactionMemory.Instance;
            // Killed while the colony was turning on them (their traders robbed, their camp attacked): one killing, not a raid beaten off.
            bool turnedOn = mem != null && mem.SinceLast(f, DeedSide.Wrong) < GenDate.TicksPerDay
                            && mem.Best(f, DeedSide.Wrong, 0f, 0f, false, d => d.kind == "robbed" || d.kind == "attacked" || d.kind == "campHarmed") != null;
            if (!before.hostile || turnedOn) FactionMemory.Note(f, "killed", member.LabelShort);
            else if (map != null && map.IsPlayerHome) FactionMemory.Note(f, "fell", member.LabelShort);
            else FactionMemory.Note(f, "slain", member.LabelShort);
        }

        /// <summary>What was true just before a death (afterwards the game may have changed it).</summary>
        public struct DeathState
        {
            public bool hostile;
            public bool prisoner;
            public bool colonist;
        }

        /// <summary>A raid has arrived at the colony.</summary>
        public static void Raided(IncidentParms parms)
        {
            var f = parms?.faction;
            if (!(parms.target is Map map) || !map.IsPlayerHome || f == null || !f.HostileTo(Faction.OfPlayer)) return;
            FactionMemory.Note(f, "raided");
        }

        /// <summary>A quest the colony took on has ended: the peoples it was for remember how.</summary>
        public static void QuestEnded(Quest q, QuestEndOutcome outcome)
        {
            if (q == null || q.root?.defName == Missions.QuestRoot) return;
            if (outcome != QuestEndOutcome.Success && outcome != QuestEndOutcome.Fail) return;
            var seen = new HashSet<Faction>();
            foreach (var f in q.InvolvedFactions)
            {
                // The people it was against (a camp to clear) aren't the ones who asked.
                if (f == null || !seen.Add(f) || !FactionMemory.Remembers(f) || f.HostileTo(Faction.OfPlayer)) continue;
                FactionMemory.Note(f, outcome == QuestEndOutcome.Success ? "quest" : "questFailed", q.name);
            }
        }

        /// <summary>A Rimfolk mission ended (one whose kind remembers its own way is left to its worker).</summary>
        public static void MissionEnded(Mission m, MissionState state, bool wasActive)
        {
            if (m?.faction == null || !wasActive) return;
            if (Missions.Worker(m)?.OwnMemory ?? false) return;
            if (state == MissionState.Done) FactionMemory.Note(m.faction, "mission", m.title);
            else if (state == MissionState.Failed && m.outcome != "cancelled" && m.outcome != "ended") FactionMemory.Note(m.faction, "missionFailed", m.title);
        }

        /// <summary>A camp has gone: helped, hosted, driven off or attacked.</summary>
        public static void CampEnded(Camp c)
        {
            var f = c?.faction;
            if (f == null || !FactionMemory.Remembers(f) || c.ending == null) return;
            switch (c.ending)
            {
                case "forced":
                    FactionMemory.Note(f, "campDriven", c.place);
                    return;
                case "harmed":
                case "arrest":
                case "fight":
                    FactionMemory.Note(f, "campHarmed", c.place);
                    return;
                case "fallout":
                    FactionMemory.Note(f, "campSour", c.place);
                    return;
                case "empty":
                    return;
            }
            // Turned back before they made camp: nothing to remember.
            if (c.arrivedTick < 0) return;
            int good = c.helps + c.gifts;
            if (good > 0) FactionMemory.Note(f, "campHelped", c.place, 8 + 2 * UnityEngine.Mathf.Min(good, 5));
            else FactionMemory.Note(f, "campHosted", c.place);
        }

        /// <summary>A trade with their traders: a fair trade, remembered a little (a run of them adds up).</summary>
        public static void Traded(Faction f, float value)
        {
            if (!FactionMemory.Remembers(f) || value < 200f) return;
            FactionMemory.Note(f, "traded", null, value >= 3000f ? 3 : value >= 1000f ? 2 : 1);
        }
    }

    /// <summary>Goodwill changed for a reason the game names (a release, a gift, an attack, peace talks...).</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
    public static class Patch_MemoryGoodwill
    {
        public static void Postfix(Faction __instance, Faction other, int goodwillChange, HistoryEventDef reason, GlobalTargetInfo? lookTarget)
        {
            if (reason == null || !MemoryHooks.Listening) return;
            try
            {
                MemoryHooks.Goodwill(__instance, other, goodwillChange, reason, lookTarget);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory goodwill hook", e);
            }
        }
    }

    /// <summary>Someone of a people is leaving the colony's map: remembered by name while the game works out its goodwill.</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberExitedMap))]
    public static class Patch_MemoryExited
    {
        public static void Prefix(Pawn member)
        {
            MemoryHooks.exiting = member;
        }

        public static void Finalizer()
        {
            MemoryHooks.exiting = null;
        }
    }

    /// <summary>Someone of a people (or of the colony) died.</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberDied))]
    public static class Patch_MemoryDied
    {
        public static void Prefix(Faction __instance, Pawn member, out MemoryHooks.DeathState __state)
        {
            __state = default;
            if (!MemoryHooks.Listening || __instance == null || member == null) return;
            try
            {
                __state.hostile = !__instance.IsPlayer && __instance.HostileTo(Faction.OfPlayer);
                __state.prisoner = member.IsPrisonerOfColony;
                __state.colonist = __instance.IsPlayer && member.RaceProps.Humanlike;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory death hook", e);
            }
        }

        public static void Postfix(Faction __instance, Pawn member, DamageInfo? dinfo, Map map, MemoryHooks.DeathState __state)
        {
            if (!MemoryHooks.Listening) return;
            try
            {
                MemoryHooks.Died(__instance, member, dinfo, map, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory death hook", e);
            }
        }
    }

    /// <summary>A raid has arrived at the colony.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_MemoryRaid
    {
        public static void Postfix(IncidentParms parms)
        {
            if (!MemoryHooks.Listening || parms == null) return;
            try
            {
                MemoryHooks.Raided(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory raid hook", e);
            }
        }
    }

    /// <summary>A quest the colony took on ended, well or badly.</summary>
    [HarmonyPatch(typeof(Quest), nameof(Quest.End))]
    public static class Patch_MemoryQuest
    {
        public static void Prefix(Quest __instance, out bool __state)
        {
            __state = false;
            try
            {
                __state = MemoryHooks.Listening && __instance != null && !__instance.hidden && __instance.State == QuestState.Ongoing && __instance.EverAccepted;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory quest hook", e);
            }
        }

        public static void Postfix(Quest __instance, QuestEndOutcome outcome, bool __state)
        {
            if (!__state) return;
            try
            {
                MemoryHooks.QuestEnded(__instance, outcome);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory quest hook", e);
            }
        }
    }

    /// <summary>The colony traded with a people's traders.</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_PlayerTraded))]
    public static class Patch_MemoryTraded
    {
        public static void Postfix(Faction __instance, float marketValueSentByPlayer)
        {
            if (!MemoryHooks.Listening) return;
            try
            {
                MemoryHooks.Traded(__instance, marketValueSentByPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory trade hook", e);
            }
        }
    }

    /// <summary>A camp on the colony's map has gone (Rimfolk's own camps).</summary>
    [HarmonyPatch(typeof(Camp), nameof(Camp.Finish))]
    public static class Patch_MemoryCamp
    {
        public static void Prefix(Camp __instance, out bool __state)
        {
            __state = __instance != null && __instance.phase != CampPhase.Over;
        }

        public static void Postfix(Camp __instance, bool __state)
        {
            if (!__state || !MemoryHooks.Listening || __instance.phase != CampPhase.Over) return;
            try
            {
                MemoryHooks.CampEnded(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faction memory camp hook", e);
            }
        }
    }
}
