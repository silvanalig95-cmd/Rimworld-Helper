using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The raiders' gods, heard at the wall (C14 of docs/content-update-proposals.md). The colony used to learn a raiding
    /// people's gods silently (Pantheon.LearnFromVisitors). Now:
    /// <list type="bullet">
    /// <item>A raid of a people that holds gods (Gods.Of) comes on: when one of the raiders is in sight of the colony's people,
    /// they cry their god's name, one sentence over their head, by their people's own name for it (most raids; not every band
    /// is pious, GodsAmongMath.CryChance). The book sets it down as heard "at our wall", from a raider of that people, and the
    /// name becomes one the colony has heard.</item>
    /// <item>A prisoner of such a people, talking with their warden, now and then tells a piece of one of their gods' tales
    /// (GodTales.Tell, "in the cells"), by their own name for the god.</item>
    /// </list>
    /// No harm: the raid is the game's. Raids waiting for their cry are not saved (a load drops them, as people's attention
    /// moves on); the prisoners' pace is the council's own cooldowns.
    /// </summary>
    public static class RaidersGods
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_RaiderCry";

        private sealed class Cry
        {
            public Map map;
            public Faction people;
            public int until;
        }

        private static readonly List<Cry> waiting = new List<Cry>();

        /// <summary>A raid came (the postfix): its cry waits for the raiders to come within sight, if their people hold gods.</summary>
        internal static void Raided(IncidentWorker w, IncidentParms parms)
        {
            if (!Gods.On || !(parms?.target is Map map) || !Homes.IsHome(map)) return;
            var f = parms.faction;
            if (f == null || f.IsPlayer || !f.HostileTo(Faction.OfPlayer) || f.def?.humanlikeFaction != true) return;
            if (!(w is IncidentWorker_RaidEnemy) && w?.def?.category != IncidentCategoryDefOf.ThreatBig) return;
            if (!Gods.Of(f).Any(g => g.kind != GodKind.Dead) || !Rand.Chance(GodsAmongMath.CryChance)) return;
            waiting.RemoveAll(c => c.map == map && c.people == f);
            if (waiting.Count >= 4) waiting.RemoveAt(0);
            waiting.Add(new Cry { map = map, people = f, until = GodsAmongBook.Now + GodsAmongMath.CryHours * GenDate.TicksPerHour });
            GodsAmongBook.Book?.Due(GodsAmongBook.Now + 250);
        }

        internal static void Hourly(GodsAmongBook b, int now) => waiting.RemoveAll(c => c.map == null || now > c.until || !Homes.IsHome(c.map));

        /// <summary>While a raid waits for its cry: a raider in sight of one of the colony cries their god's name.</summary>
        internal static void Step(GodsAmongBook b, int now)
        {
            if (waiting.Count == 0) return;
            foreach (var c in waiting.ToList())
            {
                if (c.map == null || now > c.until || !Homes.IsHome(c.map))
                {
                    waiting.Remove(c);
                    continue;
                }
                if (CryOut(c.map, c.people)) waiting.Remove(c);
            }
            if (waiting.Count > 0) b.Due(now + 250);
        }

        /// <summary>A raider of that people in sight of the colony's people cries their god's name; the book sets it down. False while none is.</summary>
        public static bool CryOut(Map map, Faction f)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead && !p.Downed && p.Awake()).ToList();
            if (colonists.Count == 0) return false;
            Pawn raider = null, hearer = null;
            foreach (var r in map.mapPawns.SpawnedPawnsInFaction(f))
            {
                if (r == null || r.Dead || r.Downed || r.RaceProps?.Humanlike != true || r.InMentalState && !r.InAggroMentalState) continue;
                hearer = colonists.Where(p => p.Position.InHorDistOf(r.Position, GodsAmongMath.CryRange) && GenSight.LineOfSight(p.Position, r.Position, map, true))
                    .OrderBy(p => p.Position.DistanceToSquared(r.Position)).FirstOrDefault();
                if (hearer == null) continue;
                raider = r;
                break;
            }
            if (raider == null) return false;
            var gods = Gods.Of(f).Where(g => g.kind != GodKind.Dead).ToList();
            if (gods.Count == 0) return true;
            // Their people's god of war or the hunt first, else the one this raider swears by.
            var god = gods.FirstOrDefault(g => g.domain == GodDomain.War || g.second == GodDomain.War) ?? Gods.SwearBy(raider) ?? gods[0];
            if (!gods.Contains(god)) god = gods[0];
            string name = Gods.NameBy(god, raider);
            string line = Flavor.FromPool(Pool, raider, null, null, "godkey:" + (god.Key ?? "").ToLowerInvariant());
            if (line.NullOrEmpty()) return true;
            line = line.Formatted(name.Named(GodsMath.God), raider.Named("PAWN")).Resolve().CapitalizeFirst();
            // apart: a cry from the raiders' line at the colony's people, said to nobody in particular.
            Speech.Say(raider, line);
            string teller = "CC_GA_RaiderOf".Translate(f.Name.Named("PEOPLE")).Resolve();
            GodLore.Heard(teller, god, line, "Wall", null, -1, name, f, raider.LabelShort, "Cried");
            Gods.Learn(f, "visitors", false);
            return true;
        }

        // ================= A prisoner and the warden =================

        /// <summary>A warden and a prisoner talked (the postfix): now and then the prisoner tells a piece of their gods' tales.</summary>
        internal static void Talked(Pawn a, Pawn b)
        {
            if (!Gods.On || a == null || b == null) return;
            Pawn prisoner = b.IsPrisonerOfColony ? b : a.IsPrisonerOfColony ? a : null;
            Pawn warden = prisoner == b ? a : prisoner == a ? b : null;
            if (prisoner == null || warden == null || !warden.IsColonist || prisoner.Faction == null || prisoner.Faction.IsPlayer) return;
            if (!Homes.IsHome(prisoner.MapHeld) || !Rand.Chance(GodsAmongMath.PrisonerTaleChance)) return;
            var keys = Gods.Of(prisoner.Faction).Where(g => g.kind != GodKind.Dead).Select(g => g.Key).ToList();
            if (keys.Count == 0 || !Interplay.Ready("prisoner-tale|" + prisoner.thingIDNumber, GodsAmongMath.PrisonerGapDays)) return;
            // together: the game's own talk brought the warden to the prisoner's side, and Speech.Say checks it again.
            if (!GodTales.Tell(prisoner, warden, keys, "Cell")) GodTales.WhatOf(prisoner, warden, keys, false, "Cell");
        }

        // ================= Dev =================

        public static string DevCry(Map map)
        {
            var f = map?.mapPawns.AllPawnsSpawned.Select(p => p.Faction).FirstOrDefault(x => x != null && !x.IsPlayer && x.HostileTo(Faction.OfPlayer) && Gods.Of(x).Count > 0);
            if (f == null) return "no raider of a people that holds gods on this map (start a raid first).";
            return CryOut(map, f) ? "a raider of " + f.Name + " cried their god's name, or none is in sight yet." : "no raider in sight of the colony's people yet.";
        }

        public static string DevPrisoner(Map map)
        {
            var prisoner = map?.mapPawns.PrisonersOfColonySpawned.FirstOrDefault(p => p.Faction != null && Gods.Of(p.Faction).Count > 0);
            var warden = prisoner == null ? null : map.mapPawns.FreeColonistsSpawned.OrderBy(p => p.Position.DistanceToSquared(prisoner.Position)).FirstOrDefault();
            if (prisoner == null || warden == null) return "no prisoner of a people that holds gods here.";
            var keys = Gods.Of(prisoner.Faction).Select(g => g.Key).ToList();
            return GodTales.Tell(prisoner, warden, keys, "Cell") || GodTales.WhatOf(prisoner, warden, keys, false, "Cell")
                ? prisoner.LabelShort + " told " + warden.LabelShort + " of their gods (the line shows when the two are together)."
                : "nothing left to tell.";
        }
    }

    /// <summary>A raid of a people that holds gods: its cry waits for the raiders to come on (Gods/Among/RaidersGods.cs). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_RaidersGodsRaid
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                RaidersGods.Raided(__instance, parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a raid's cry", e);
            }
        }
    }

    /// <summary>A warden and a prisoner talk: the prisoner may tell of their gods (Gods/Among/RaidersGods.cs). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_RaidersGodsPrisonerTalk
    {
        public static void Postfix(Pawn ___pawn, Pawn recipient, bool __result)
        {
            if (!__result || !RimfolkPatches.On || ___pawn == null || recipient == null) return;
            if (!recipient.IsPrisonerOfColony && !___pawn.IsPrisonerOfColony) return;
            try
            {
                RaidersGods.Talked(___pawn, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a prisoner's tale", e);
            }
        }
    }
}
