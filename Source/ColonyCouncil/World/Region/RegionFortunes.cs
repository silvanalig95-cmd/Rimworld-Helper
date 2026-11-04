using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's fortune (docs/design/stage1-region.md, item 2, "Prosperity"): each town in detail has a prosperity of 0
    /// to 100 on its ledger record, seeded when first seen (less on poor land and in hard times; a new town at 30), moved
    /// once a day by the world's events (a field of each <see cref="WorldSituationDef"/>, in full at the event's town and
    /// half at its people's other towns in detail, spread over its first days; a second field for the other people of a
    /// war), by a war near it (the game's own relations) and drifting back toward 50 (45 on poor land). A town taken in
    /// an invasion starts again at 25. Shown in words in its panel on the region map and in its people's talk. The rules
    /// are <see cref="RegionMath"/>'s.
    /// </summary>
    public static class RegionFortunes
    {
        /// <summary>A world event's number: how many days' shares of its move the towns have had.</summary>
        public const string AppliedKey = "regionDays";

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- The day ----------------

        /// <summary>The day's look: each town in detail seeded or moved (the events' shares, a war, the drift), and kept low since when.</summary>
        internal static void Daily(int now)
        {
            var towns = TownsInDetail();
            if (towns.Count == 0) return;
            var ws = WorldSituations.Instance;
            var moves = EventMoves(towns, ws);
            var enemies = EnemyTowns(towns);
            foreach (var t in towns)
            {
                var rec = WorldLedger.TownFor(t.ID, t.Name, t.Faction, true);
                if (rec == null) continue;
                if (t.HasName) rec.name = t.Name;
                rec.people = t.Faction;
                float weight = BiomeWeight(t.Tile);
                float p;
                if (!rec.Seeded)
                    p = RegionMath.Seed(weight, ws?.HardTimes(t.Faction) ?? false);
                else
                {
                    float days = rec.movedAt >= 0 ? Mathf.Clamp((now - rec.movedAt) / (float)GenDate.TicksPerDay, 0f, 30f) : 1f;
                    p = RegionMath.Drift(rec.prosperity, RegionMath.DriftTarget(weight), days);
                    p += RegionMath.WarMove(enemies.TryGetValue(t.ID, out float e) ? e : -1f) * days;
                }
                if (moves.TryGetValue(t.ID, out var m) && Math.Abs(m.amount) > 0.001f)
                {
                    p += m.amount;
                    rec.cause = m.cause;
                    rec.causeDir = m.amount > 0f ? 1 : -1;
                    rec.causeAt = now;
                }
                rec.prosperity = RegionMath.Clamp(p, rec.kind == TownKind.Post ? RegionMath.PostCap : RegionMath.MaxProsperity);
                rec.movedAt = now;
                rec.lowSince = RegionMath.LowSince(rec.prosperity, rec.lowSince, now);
            }
            WorldLedger.TownChanged();
        }

        /// <summary>The towns kept in detail for any home, each once.</summary>
        private static List<Settlement> TownsInDetail()
        {
            var list = new List<Settlement>();
            var seen = new HashSet<int>();
            foreach (var r in RegionTowns.Reach)
                foreach (var t in r.towns)
                    if (t != null && !t.Destroyed && seen.Add(t.ID))
                        list.Add(t);
            return list;
        }

        /// <summary>
        /// The world events' shares due today, by town: each running event with a move gives one share for each day begun
        /// of its first <see cref="RegionMath.SpreadDays"/> (kept on the event, so a day is never given twice), all of it at
        /// its own town and half at its people's other towns in detail; its second field at the other people's towns.
        /// </summary>
        private static Dictionary<int, (float amount, string cause)> EventMoves(List<Settlement> towns, WorldSituations ws)
        {
            var moves = new Dictionary<int, (float amount, string cause)>();
            if (ws == null) return moves;
            foreach (var s in ws.Running.ToList())
            {
                var def = s?.def;
                if (def == null || def.prosperity == 0f && def.prosperityOther == 0f || s.ctx?.faction == null) continue;
                int applied = s.Num(AppliedKey);
                int due = RegionMath.DaysDue(s.DaysIn, applied);
                if (due <= 0) continue;
                s.SetNum(AppliedKey, applied + due);
                string cause = Texts.WithThe(s.ctx.name, def.label);
                int at = s.place.kind == RefKind.Town ? s.place.id : -1;
                foreach (var t in towns)
                {
                    float amount = 0f;
                    if (def.prosperity != 0f && t.Faction == s.ctx.faction) amount = RegionMath.Share(RegionMath.DayShare(def.prosperity) * due, t.ID == at);
                    else if (def.prosperityOther != 0f && s.ctx.enemy != null && t.Faction == s.ctx.enemy)
                        amount = RegionMath.Share(RegionMath.DayShare(def.prosperityOther) * due, false);
                    if (amount == 0f) continue;
                    moves.TryGetValue(t.ID, out var was);
                    // The bigger of two moves on one day names the cause.
                    moves[t.ID] = (was.amount + amount, was.cause == null || Math.Abs(amount) > Math.Abs(was.amount) ? cause : was.cause);
                }
            }
            return moves;
        }

        /// <summary>
        /// For each town in detail, the tiles to the nearest town of a people its own is at war with (the game's own
        /// relations between peoples that count: humanlike, not hidden, not the permanent enemies), when one is within
        /// <see cref="RegionMath.WarTiles"/>.
        /// </summary>
        private static Dictionary<int, float> EnemyTowns(List<Settlement> towns)
        {
            var near = new Dictionary<int, float>();
            var all = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && Facts.Counts(s.Faction)).ToList();
            var hostile = new Dictionary<(Faction, Faction), bool>();
            foreach (var t in towns)
            {
                if (!Facts.Counts(t.Faction)) continue;
                float best = float.MaxValue;
                foreach (var o in all)
                {
                    if (o.Faction == t.Faction || o.Tile.Layer != t.Tile.Layer) continue;
                    var pair = (t.Faction, o.Faction);
                    if (!hostile.TryGetValue(pair, out bool war)) hostile[pair] = war = t.Faction.HostileTo(o.Faction);
                    if (!war) continue;
                    float d = Find.WorldGrid.ApproxDistanceInTiles(t.Tile, o.Tile);
                    if (d < best) best = d;
                }
                if (best <= RegionMath.WarTiles) near[t.ID] = best;
            }
            return near;
        }

        /// <summary>The game's sense of where towns stand: the land's biome's settlement weight (1 when it can't be read).</summary>
        public static float BiomeWeight(PlanetTile tile)
        {
            try
            {
                var biome = tile.Valid ? Find.WorldGrid[tile]?.PrimaryBiome : null;
                return biome?.settlementSelectionWeight ?? 1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a biome's settlement weight", e);
                return 1f;
            }
        }

        // ---------------- Hooks ----------------

        /// <summary>A town taken in an invasion (Faction Territories): it starts again at 25 under its new people, when in detail.</summary>
        public static void Taken(Settlement town, Faction by)
        {
            if (town == null || by == null || !RegionBook.FortunesOn || !RegionTowns.IsInDetail(town.ID)) return;
            var rec = WorldLedger.TownFor(town.ID, town.HasName ? town.Name : null, by, true);
            if (rec == null) return;
            rec.people = by;
            rec.prosperity = RegionMath.TakenStart;
            rec.movedAt = Now;
            rec.cause = "CC_Region_Cause_Taken".Translate(by.Name).Resolve();
            rec.causeDir = -1;
            rec.causeAt = Now;
            rec.lowSince = RegionMath.LowSince(rec.prosperity, -1, Now);
            WorldLedger.TownChanged();
        }

        // ---------------- Words ----------------

        /// <summary>A town's fortune as it stands, or null when it has none (not in detail, or the fortunes are off).</summary>
        public static Fortune? Of(int town)
        {
            if (!RegionBook.FortunesOn) return null;
            var rec = WorldLedger.TownRecord(town);
            return rec != null && rec.Seeded && rec.gone < 0 ? RegionMath.Band(rec.prosperity) : (Fortune?)null;
        }

        /// <summary>
        /// The band in words, by their people's age where it has a word of its own ("hungry" for a struggling tribe, "short
        /// of everything" for a struggling industrial town): CC_Region_Fortune_Band_Age, else CC_Region_Fortune_Band.
        /// </summary>
        public static string Word(Fortune f, Faction people)
        {
            string key = "CC_Region_Fortune_" + f;
            string aged = key + "_" + Ages.Of(people);
            return (aged.CanTranslate() ? aged : key).Translate().Resolve();
        }

        /// <summary>
        /// The panel's line: "Struggling, since the plague", "Thriving: the new road", or the band alone when nothing moved
        /// it lately the way it now stands.
        /// </summary>
        public static string Line(LedgerTown rec)
        {
            if (rec == null || !rec.Seeded) return null;
            var band = RegionMath.Band(rec.prosperity);
            string word = Word(band, rec.people).CapitalizeFirst();
            bool lately = !rec.cause.NullOrEmpty() && rec.causeAt >= 0 && Now - rec.causeAt < RegionMath.CauseDays * GenDate.TicksPerDay;
            if (lately && RegionMath.CauseFits(band, rec.causeDir))
                return (rec.causeDir < 0 ? "CC_Region_FortuneDown" : "CC_Region_FortuneUp").Translate(word, rec.cause).Resolve();
            return word;
        }
    }
}
