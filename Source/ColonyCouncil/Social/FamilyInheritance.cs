using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a dead colonist leaves goes to family first. Their heirloom (Lives/Legacy.cs) goes to the one who loved them
    /// most, family weighing most, the closest first (FamilyLifeMath.HeirBonus). Their own room goes, a day after they
    /// die, to the closest of their family here with no room of their own: the bed is assigned to them as the game
    /// assigns beds, and they write of it. A partner who shared the bed keeps it, and a room the colony has given out
    /// meanwhile stays given. Nothing is saved: a death the day before a reload leaves the room to the colony as usual.
    /// </summary>
    public static class Inheritance
    {
        private class Left
        {
            public Pawn dead;
            public Building_Bed bed;
            public int tick;
        }

        /// <summary>Rooms left by the dead, passed on a day later.</summary>
        private static readonly List<Left> rooms = new List<Left>();

        public static void Reset() => rooms.Clear();

        /// <summary>How much being family weighs in who inherits from <paramref name="dead"/>: the closest relation between them.</summary>
        public static float Bonus(Pawn p, Pawn dead)
        {
            float best = 0f;
            if (p?.relations == null || dead == null) return best;
            foreach (var r in p.GetRelations(dead)) best = Math.Max(best, FamilyLifeMath.HeirBonus(r.defName, r.familyByBloodRelation));
            return best;
        }

        /// <summary>A colonist died who had a room of their own (the bed in it).</summary>
        public static void Notify_Died(Pawn dead, Building_Bed bed)
        {
            // apart: a room passed on after a death; the bed changes hands on the map a day later.
            if (dead == null || bed == null || !ColonyCouncilMod.Settings.heirlooms) return;
            rooms.Add(new Left { dead = dead, bed = bed, tick = Find.TickManager.TicksGame });
            if (rooms.Count > 20) rooms.RemoveAt(0);
        }

        /// <summary>The council's hour: a room left a day ago goes to family.</summary>
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
            var heir = bed.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != dead && People.HasInnerLife(p) && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby() && p.ownership != null && p.ownership.Bedroom == null)
                .Select(p => (p, bonus: Bonus(p, dead)))
                .Where(x => x.bonus >= FamilyLifeMath.HeirFamily)
                .OrderByDescending(x => x.bonus + x.p.relations.OpinionOf(dead)).Select(x => x.p).FirstOrDefault();
            if (heir == null || !heir.ownership.ClaimBedIfNonMedical(bed)) return;
            string relation = Family.Relation(dead, heir);
            string text = (relation.NullOrEmpty() ? "CC_Inherit_RoomPlain" : "CC_Inherit_Room")
                .Translate(heir.Named("PAWN"), dead.Named("OTHER"), relation.Named("RELATION")).Resolve();
            Notify.Message(text, new LookTargets(heir, bed), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (!comp.journals.WroteWithin(heir)) comp.journals.Write(heir, "Council_Journal_InheritedRoom", null, dead.Named("OTHER"));
        }
    }
}
