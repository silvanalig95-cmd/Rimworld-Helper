using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a dead colonist leaves goes to whoever was closest to them, a partner, a friend or a relative, by how close they
    /// really were (the player's rule of 5 November 2026: the colony is the family, never a dynasty; A30 of 20 November
    /// 2026, as the vendetta had passed since 6 November). Closeness is the vendetta's measure (FamilyLifeMath.Closeness:
    /// the game's opinion of the dead, the social web's warmth, a partner or a relative by blood as one part among them).
    /// Their heirloom (Lives/Legacy.cs) goes to the closest of the colony. Their own room goes, a day after they die, to the
    /// closest of those here with no room of their own who were close enough (as for a vendetta), as they stood at the
    /// death: the bed is assigned to them as the game assigns beds, and they write of it. A partner who shared the bed keeps
    /// it, and a room the colony has given out meanwhile stays given. Nothing is saved: a death the day before a reload
    /// leaves the room to the colony as usual.
    /// </summary>
    public static class Inheritance
    {
        private class Left
        {
            public Pawn dead;
            public Building_Bed bed;
            public int tick;
            /// <summary>How close each colonist was to the dead at the death (thingIDNumber), before the social web lets its bonds with them go.</summary>
            public Dictionary<int, float> close = new Dictionary<int, float>();
        }

        /// <summary>Rooms left by the dead, passed on a day later.</summary>
        private static readonly List<Left> rooms = new List<Left>();

        public static void Reset() => rooms.Clear();

        /// <summary>How close <paramref name="p"/> was to <paramref name="dead"/> (FamilyLifeMath.Closeness), as the game and Rimfolk know it now.</summary>
        public static float Closeness(CouncilGameComponent comp, Pawn p, Pawn dead)
        {
            if (p?.relations == null || dead == null || p == dead) return float.MinValue;
            var rels = p.GetRelations(dead).ToList();
            return FamilyLifeMath.Closeness(p.relations.OpinionOf(dead), comp?.social?.Warmth(p, dead) ?? 0f,
                rels.Any(r => r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance), rels.Any(r => r.familyByBloodRelation));
        }

        /// <summary>A colonist died who had a room of their own (the bed in it): how close each of the colony was to them, kept for a day.</summary>
        public static void Notify_Died(Pawn dead, Building_Bed bed)
        {
            // apart: a room passed on after a death; the bed changes hands on the map a day later.
            // The room left, for the council's question when it stands empty (Dilemmas/DilemmaWorkers_Round2.cs, D24).
            Round2Council.RoomLeft(dead, bed);
            if (dead == null || bed == null || !ColonyCouncilMod.Settings.heirlooms) return;
            var left = new Left { dead = dead, bed = bed, tick = Find.TickManager.TicksGame };
            var comp = CouncilGameComponent.Instance;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                if (p != dead && p.relations != null)
                    left.close[p.thingIDNumber] = Closeness(comp, p, dead);
            rooms.Add(left);
            if (rooms.Count > 20) rooms.RemoveAt(0);
        }

        /// <summary>The council's hour: a room left a day ago goes to whoever was closest to the dead.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null || rooms.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var l in rooms.Where(l => now - l.tick >= GenDate.TicksPerDay).ToList())
            {
                rooms.Remove(l);
                try
                {
                    Pass(comp, l);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("inheritance: a room", e);
                }
            }
        }

        private static void Pass(CouncilGameComponent comp, Left l)
        {
            var bed = l.bed;
            var dead = l.dead;
            if (bed == null || bed.Destroyed || !bed.Spawned || bed.Medical || bed.ForOwnerType != BedOwnerType.Colonist || !bed.Map.IsPlayerHome) return;
            // Someone sleeps there already (a partner who shared it, or someone the colony gave it to): it stays theirs.
            if (bed.OwnersForReading.Any(o => o != null && !o.Dead && o != dead)) return;
            if (bed.GetRoom()?.Role != RoomRoleDefOf.Bedroom) return;
            var could = bed.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != dead && People.HasInnerLife(p) && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby() && p.ownership != null && p.ownership.Bedroom == null).ToList();
            // How close each was at the death (one who came since: as close as they are now).
            int i = FamilyLifeMath.Heir(could.Select(p => l.close.TryGetValue(p.thingIDNumber, out float c) ? c : Closeness(comp, p, dead)).ToList(), FamilyLifeMath.RoomAtLeast);
            var heir = i >= 0 ? could[i] : null;
            if (heir == null || !heir.ownership.ClaimBedIfNonMedical(bed)) return;
            string relation = Family.Relation(dead, heir);
            string text = (relation.NullOrEmpty() ? "CC_Inherit_RoomPlain" : "CC_Inherit_Room")
                .Translate(heir.Named("PAWN"), dead.Named("OTHER"), relation.Named("RELATION")).Resolve();
            Notify.Message(text, new LookTargets(heir, bed), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (!comp.journals.WroteWithin(heir)) comp.journals.Write(heir, "Council_Journal_InheritedRoom", null, dead.Named("OTHER"));
        }
    }
}
