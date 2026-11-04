using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a town's fortune does beyond its words and its stock (docs/design/stage1-region.md, item 2, "What it
    /// changes"), by the towns in a home's reach:
    /// <list type="bullet">
    /// <item>the asks: while a town of a people on good terms struggles, its people's asks for provisions and for help
    /// rebuilding come twice as often, from that town; a call to shelter refugees comes twice as often when the hardship
    /// they flee struck a people with a town struggling in reach;</item>
    /// <item>caravans: a people's trader caravans come 1.25 times as often while their town nearest the home thrives or
    /// flourishes, 0.6 times while it struggles (Patch_RiseFallCaravans);</item>
    /// <item>the hungry at the gate: a refugee family's camp comes twice as often while a town in reach struggles, and is
    /// likeliest of that town's people.</item>
    /// </list>
    /// With the towns' fortunes and "Systems talk to each other" on; the caravans also with the deeper game hooks. The
    /// numbers are <see cref="RiseFallMath"/>'s.
    /// </summary>
    public static class RiseFallEffects
    {
        public const string Provisions = "Council_Mission_Provisions";
        public const string Rebuild = "Council_Mission_Rebuild";
        public const string Refugees = "Council_Mission_Refugees";
        public const string RefugeeCamp = "Council_Camp_Refugees";

        public static bool On => RegionBook.FortunesOn && Interplay.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- The towns in reach and their fortunes, kept a 250-tick stretch ----------------

        private sealed class Kept
        {
            public int at = Fresh.Never;
            public int version = -1;
            public readonly List<(Settlement town, Fortune band)> towns = new List<(Settlement, Fortune)>();
        }

        private static readonly Dictionary<Map, Kept> kept = new Dictionary<Map, Kept>();
        private static Game keptGame;

        /// <summary>A home's towns in detail with a fortune, nearest first.</summary>
        private static List<(Settlement town, Fortune band)> Fortunes(Map home)
        {
            if (keptGame != Current.Game)
            {
                keptGame = Current.Game;
                kept.Clear();
            }
            if (!kept.TryGetValue(home, out var k)) kept[home] = k = new Kept();
            int now = Now;
            if (Fresh.Holds(k.at, now, Sched.RareTicks) && k.version == WorldLedger.Version) return k.towns;
            k.at = now;
            k.version = WorldLedger.Version;
            k.towns.Clear();
            var r = RegionTowns.Of(home);
            if (r == null) return k.towns;
            foreach (var t in r.towns)
            {
                if (t == null || t.Destroyed) continue;
                var rec = WorldLedger.TownFor(t.ID, null, null, false);
                if (rec != null && rec.Seeded && rec.gone < 0) k.towns.Add((t, RegionMath.Band(rec.prosperity)));
            }
            return k.towns;
        }

        /// <summary>The nearest town in the home's reach that struggles, of a people at peace with the colony that <paramref name="fits"/>; null for none.</summary>
        public static Settlement Struggling(Map home, Func<Faction, bool> fits = null)
        {
            if (!On || home == null) return null;
            var player = Faction.OfPlayerSilentFail;
            foreach (var (town, band) in Fortunes(home))
            {
                var f = town.Faction;
                if (band != Fortune.Struggling || f == null || player == null || f.HostileTo(player)) continue;
                if (fits == null || fits(f)) return town;
            }
            return null;
        }

        /// <summary>A people has a town struggling in the home's reach.</summary>
        public static bool StrugglesNear(Faction f, Map home) => f != null && Struggling(home, x => x == f) != null;

        // ---------------- Caravans ----------------

        /// <summary>How likely a people's trader caravan is to be the one that comes, by their town nearest the home (when in its reach).</summary>
        public static float CaravanFactor(Faction f, Map home)
        {
            if (!On || f == null || home == null) return 1f;
            foreach (var (town, band) in Fortunes(home))
                if (town.Faction == f)
                    return RiseFallMath.CaravanFactor(band);
            return 1f;
        }

        // ---------------- The asks ----------------

        /// <summary>The town a people's ask for provisions or rebuilding comes from while it struggles (the delivery missions); null for any other event, or none.</summary>
        public static Settlement AskTown(IncidentDef def, Map map)
        {
            if (def == null || map == null || def.defName != Provisions && def.defName != Rebuild) return null;
            return Struggling(map, f => VarietyUtil.OnGoodTerms(f) && Arrivals.Usable(f) && Arrivals.PersonKind(f) != null);
        }

        /// <summary>
        /// How much likelier one of Rimfolk's events is at a home for a town's fortune there: the asks for provisions and
        /// rebuilding while a town of a people on good terms struggles, the call to shelter refugees while the hardship of a
        /// people with a struggling town lasts (or ended within 30 days). 1 for every other event.
        /// </summary>
        public static float EventFactor(IncidentDef def, Map map)
        {
            if (def == null || map == null || !On) return 1f;
            try
            {
                switch (def.defName)
                {
                    case Provisions:
                    case Rebuild:
                        return AskTown(def, map) != null ? RiseFallMath.HardTimes : 1f;
                    case Refugees:
                        return Struggling(map, f => FactionMemory.Remembers(f) && InHardship(f)) != null ? RiseFallMath.HardTimes : 1f;
                    default:
                        return 1f;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: an event's odds", e);
                return 1f;
            }
        }

        /// <summary>A bad world event about this people runs, or ended within 30 days.</summary>
        private static bool InHardship(Faction f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || f == null) return false;
            if (ws.HardTimes(f)) return true;
            int now = Now;
            foreach (var r in ws.history)
            {
                if (r == null || r.faction != f || now - r.endTick > GenDate.TicksPerDay * 30) continue;
                var d = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "");
                if (d != null && d.bad) return true;
            }
            return false;
        }

        // ---------------- The hungry at the gate ----------------

        /// <summary>
        /// This people can send a family of their own as this camp's kind (at peace with the colony, of its ages, with
        /// ordinary people to send). A town of a people that can't (one at war with the colony) sends no family to the
        /// gate: the camps would send another people's in their place, and the words would name the wrong ones.
        /// </summary>
        public static bool SendsOwn(CampKindDef kind, Faction f) => kind != null && f != null && kind.FitsFaction(f) && Arrivals.Usable(f) && Arrivals.PersonKind(f) != null;

        /// <summary>How likely a camp's kind is to be the one that comes: a refugee family twice as likely while a town in reach struggles.</summary>
        public static float CampFactor(CampKindDef kind, Map map)
        {
            if (kind == null || map == null || kind.defName != RefugeeCamp || !On) return 1f;
            try
            {
                return Struggling(map, f => SendsOwn(kind, f)) != null ? RiseFallMath.HardTimes : 1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a camp's odds", e);
                return 1f;
            }
        }

        /// <summary>A refugee family is likeliest of a people with a town struggling in reach.</summary>
        public static float CampPeople(CampKindDef kind, Map map, Faction f)
        {
            if (kind == null || map == null || f == null || kind.defName != RefugeeCamp || !On) return 1f;
            try
            {
                return StrugglesNear(f, map) ? RiseFallMath.HardTimesPeople : 1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a camp's people", e);
                return 1f;
            }
        }
    }
}
