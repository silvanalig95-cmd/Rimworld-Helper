using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Ducat's happenings under way (OwnHappeningsBook.ducat): a trail of coins, the open purse, the beetle leading to a coin.</summary>
    public class DucatState : IExposable
    {
        /// <summary>The trail of coins from a hoard out to the edge of the land: its first coin, its end, who goes to it, until when.</summary>
        public Map trailMap;
        public IntVec3 trailFrom = IntVec3.Invalid, trailEnd = IntVec3.Invalid;
        public Pawn trailFinder;
        public int trailUntil = -1, trailCoins;
        public bool trailDoor;

        /// <summary>His festival kept today, and the open purse: who gives, to whom, how many coins, until when.</summary>
        public Map purseMap;
        public int purseDay = -1, purseUntil = -1, purseCoins;
        public Pawn giver, visitor;

        /// <summary>The beetle that led to a coin: where it lies, who follows it, until when.</summary>
        public Map beetleMap;
        public IntVec3 coinAt = IntVec3.Invalid;
        public Pawn follower;
        public int beetleUntil = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref trailMap, "trailMap");
            Scribe_Values.Look(ref trailFrom, "trailFrom", IntVec3.Invalid);
            Scribe_Values.Look(ref trailEnd, "trailEnd", IntVec3.Invalid);
            Scribe_References.Look(ref trailFinder, "trailFinder");
            Scribe_Values.Look(ref trailUntil, "trailUntil", -1);
            Scribe_Values.Look(ref trailCoins, "trailCoins");
            Scribe_Values.Look(ref trailDoor, "trailDoor");
            Scribe_References.Look(ref purseMap, "purseMap");
            Scribe_Values.Look(ref purseDay, "purseDay", -1);
            Scribe_Values.Look(ref purseUntil, "purseUntil", -1);
            Scribe_Values.Look(ref purseCoins, "purseCoins");
            Scribe_References.Look(ref giver, "giver");
            Scribe_References.Look(ref visitor, "visitor");
            Scribe_References.Look(ref beetleMap, "beetleMap");
            Scribe_Values.Look(ref coinAt, "coinAt", IntVec3.Invalid);
            Scribe_References.Look(ref follower, "follower");
            Scribe_Values.Look(ref beetleUntil, "beetleUntil", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (trailMap == null) trailUntil = -1;
                if (giver == null || visitor == null) purseUntil = -1;
                if (beetleMap == null || follower == null) beetleUntil = -1;
            }
        }
    }

    /// <summary>
    /// Ducat, the Restless Gold (docs/gods-next-proposals.md, C 14): the spirit in gold that wants to keep moving, which loves
    /// trade and giving and hates a hoard. Its own happenings:
    /// <list type="bullet">
    /// <item>The trail of coins: when silver goes missing from a hoard (its anger, GodHealth), the way it went is there in the
    /// morning: single coins a few steps apart from the store out through the door and on to the edge of the land, the last
    /// of them gone over it; the first one free follows it out and says so (the coins on the ground can be gathered back).</item>
    /// <item>The open purse: at its festival (the Open Purse), or when one of the colony devoted to it sees visitors on the
    /// land, they take a few coins from the stores, walk over to a visitor and press them into their hand; that visitor's
    /// people think the better of the colony.</item>
    /// <item>The beetle that leads: a golden beetle crosses a table and walks off it, across the floor to where a gold coin
    /// lies that nobody dropped; whoever saw it follows it there and finds the coin (more often with traders on the land).</item>
    /// </list>
    /// </summary>
    public static class DucatWays
    {
        public const string Key = "Ducat";
        public const string Pool = "Rimfolk_Pool_GodDucat_Say";

        public static God God => OwnHappenings.God(Key);

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color GoldGlint = new Color(1f, 0.85f, 0.35f);

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.ducat;
            int hour = GenLocalDate.HourOfDay(map);
            if (s.trailMap == map && s.trailUntil >= 0 && s.trailFinder == null && hour >= 6 && hour < 20) Follow(b, map);
            if (MapFacts.ThreatOn(map)) return;
            if (s.purseUntil < 0 && hour >= 8 && hour < 20) Purse(b, g, map, now);
            if (s.beetleUntil < 0 && hour >= 8 && hour < 19) Beetle(b, g, map, now);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.ducat;
            if (s.trailUntil >= 0) OwnHappenings.Safe("the trail of coins", () => TrailStep(b, now));
            if (s.purseUntil >= 0) OwnHappenings.Safe("the open purse", () => PurseStep(b, now));
            if (s.beetleUntil >= 0) OwnHappenings.Safe("the beetle that leads", () => BeetleStep(b, now));
        }

        // ================= The trail of coins =================

        /// <summary>
        /// Silver gone from a hoard (GodHealth's "missing"): of it, single coins lie a few steps apart from the store out through
        /// the outer door to the nearest edge of the land, the last of them gone over it.
        /// </summary>
        public static void Trail(God g, Map map, IntVec3 from, int taken)
        {
            var b = OwnHappenings.Book;
            if (b == null || map == null || !from.IsValid) return;
            OwnHappenings.Safe("the trail of coins laid", () => Lay(b, map, from, taken));
        }

        public static bool Lay(OwnHappeningsBook b, Map map, IntVec3 from, int taken)
        {
            var s = b.ducat;
            int coins = OwnHappeningsMath.TrailCoins(taken);
            if (coins <= 0) return false;
            var door = GodOmens.OuterDoor(map);
            var via = door?.Position ?? from;
            var edge = Edge(map, via);
            var cells = new List<IntVec3>();
            if (via != from) cells.AddRange(GenSight.PointsOnLineOfSight(from, via));
            cells.AddRange(GenSight.PointsOnLineOfSight(via, edge));
            var spots = new List<IntVec3>();
            for (int i = OwnHappeningsMath.TrailEvery; i < cells.Count; i += OwnHappeningsMath.TrailEvery)
            {
                var c = cells[i];
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstItem(map) == null && !c.InNoBuildEdgeArea(map)) spots.Add(c);
            }
            // The last few went over the edge: none lies there.
            int laid = 0;
            IntVec3 first = IntVec3.Invalid, last = IntVec3.Invalid;
            foreach (var c in spots.Take(Math.Min(coins, Math.Max(0, spots.Count - 2))))
            {
                try
                {
                    var t = ThingMaker.MakeThing(ThingDefOf.Silver);
                    t.stackCount = 1;
                    GenSpawn.Spawn(t, c, map);
                    FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(), map, 0.3f);
                    if (!first.IsValid) first = c;
                    last = c;
                    laid++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a coin on the trail", e);
                    break;
                }
            }
            if (laid == 0) return false;
            s.trailMap = map;
            s.trailFrom = first;
            s.trailEnd = last;
            s.trailCoins = laid;
            s.trailDoor = door != null;
            s.trailFinder = null;
            s.trailUntil = Now + GenDate.TicksPerDay;
            OwnHappenings.Came(b, OwnHappeningsMath.DucatTrail, null, laid.ToString());
            return true;
        }

        /// <summary>The nearest edge of the land from a place, straight out.</summary>
        private static IntVec3 Edge(Map map, IntVec3 from)
        {
            var edges = new[] { new IntVec3(from.x, 0, 0), new IntVec3(from.x, 0, map.Size.z - 1), new IntVec3(0, 0, from.z), new IntVec3(map.Size.x - 1, 0, from.z) };
            return edges.OrderBy(e => e.DistanceToSquared(from)).First();
        }

        /// <summary>In daylight, the first one free walks out to the first coin and follows the line with their eyes.</summary>
        private static void Follow(OwnHappeningsBook b, Map map)
        {
            var s = b.ducat;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Free(x) && x.CanReach(s.trailFrom, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(s.trailFrom)).FirstOrDefault();
            if (p == null) return;
            s.trailFinder = p;
            OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.trailFrom, p), GenDate.TicksPerHour * 2);
            b.Due(Now + 250);
        }

        private static void TrailStep(OwnHappeningsBook b, int now)
        {
            var s = b.ducat;
            var map = s.trailMap;
            var p = s.trailFinder;
            if (map == null || now > s.trailUntil)
            {
                s.trailUntil = -1;
                s.trailMap = null;
                s.trailFinder = null;
                return;
            }
            if (p == null) return;
            if (p.Dead || !p.Spawned || p.Map != map)
            {
                s.trailFinder = null;
                return;
            }
            if (p.Position.InHorDistOf(s.trailFrom, 2.5f))
            {
                var g = God;
                OwnHappenings.Stand(p, s.trailEnd, 420);
                OwnHappenings.Say(p, Pool, g, "trail");
                OwnHappenings.Line(map, "CC_GodDucat_Trail_Text".Translate(p.Named("PAWN"), s.trailCoins.Named("COUNT"), (s.trailDoor ? "CC_GodDucat_Way_Door" : "CC_GodDucat_Way_Open").Translate().Resolve().Named("WAY")).Resolve(),
                    new LookTargets(new[] { new TargetInfo(s.trailFrom, map), new TargetInfo(s.trailEnd, map) }));
                if (g != null) OwnHappenings.Saw(g, map, s.trailFrom, GodActKind.Omen, p, "CC_GodDucat_Saw_Trail", s.trailCoins.Named("COUNT"));
                s.trailUntil = -1;
                s.trailMap = null;
                s.trailFinder = null;
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto && OwnHappenings.Free(p)) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.trailFrom, p), GenDate.TicksPerHour * 2);
            b.Due(now + 250);
        }

        // ================= The open purse =================

        /// <summary>Its festival kept at a home (OwnHappenings.FestivalKept): a coin pressed into a visitor's hand that day.</summary>
        internal static void Festival(OwnHappeningsBook b, Map map)
        {
            b.ducat.purseMap = map;
            b.ducat.purseDay = GenDate.DaysPassed;
        }

        /// <summary>A visitor of a friendly people on the land, up and about and within reach, or null.</summary>
        public static Pawn Visitor(Map map, Pawn from) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps?.Humanlike == true && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner
                                                    && !p.Dead && !p.Downed && !p.InMentalState && p.inventory != null && (from == null || from.CanReach(p, PathEndMode.Touch, Danger.Some)))
                .OrderBy(p => from == null ? 0 : p.Position.DistanceToSquared(from.Position)).FirstOrDefault();

        private static int Silver(Map map) => map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => t.stackCount);

        private static void Purse(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.ducat;
            bool festival = s.purseMap == map && s.purseDay == GenDate.DaysPassed;
            var giver = Giver(map, g, festival);
            var visitor = giver == null ? null : Visitor(map, giver);
            bool devoted = giver != null && PawnGods.HoldOf(giver, g) >= PawnGodsMath.DevotedFrom;
            var t = OwnHappeningsMath.PurseTrigger(festival, devoted, visitor != null && Silver(map) >= OwnHappeningsMath.PurseMin);
            if (t == GodPaceMath.Trigger.None) return;
            if (festival ? !Rand.Chance(0.35f) : !OwnHappenings.Roll(b, OwnHappeningsMath.DucatPurse, t, g, map, now)) return;
            BeginPurse(b, map, giver, visitor);
        }

        /// <summary>Who opens the purse: on its festival whoever of the colony holds it most (or the priest), else one devoted to it.</summary>
        public static Pawn Giver(Map map, God g, bool festival)
        {
            var free = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.DevelopmentalStage.Adult() && !PawnGods.Dreads(p, g)).ToList();
            if (festival) return free.OrderByDescending(p => PawnGods.HoldOf(p, g) + (Worship.IsPriest(p) ? 40f : 0f)).FirstOrDefault();
            return free.Where(p => PawnGods.HoldOf(p, g) >= PawnGodsMath.DevotedFrom).OrderByDescending(p => PawnGods.HoldOf(p, g)).FirstOrDefault();
        }

        public static bool BeginPurse(OwnHappeningsBook b, Map map, Pawn giver, Pawn visitor)
        {
            var s = b.ducat;
            if (giver == null || visitor == null) return false;
            s.giver = giver;
            s.visitor = visitor;
            s.purseCoins = Mathf.Min(Silver(map), Rand.RangeInclusive(OwnHappeningsMath.PurseMin, OwnHappeningsMath.PurseMax));
            if (s.purseCoins <= 0) return false;
            s.purseUntil = Now + GenDate.TicksPerHour * 2;
            s.purseMap = null;
            OwnHappenings.Walk(giver, OwnHappenings.Beside(map, visitor.Position, giver), GenDate.TicksPerHour);
            OwnHappenings.Came(b, OwnHappeningsMath.DucatPurse, null, visitor.Faction?.loadID.ToString());
            b.Due(Now + 200);
            return true;
        }

        private static void PurseStep(OwnHappeningsBook b, int now)
        {
            var s = b.ducat;
            var p = s.giver;
            var v = s.visitor;
            if (p == null || v == null || p.Dead || v.Dead || !p.Spawned || !v.Spawned || p.Map != v.Map || now > s.purseUntil || p.Downed || p.Drafted)
            {
                s.purseUntil = -1;
                s.giver = s.visitor = null;
                return;
            }
            if (p.Position.InHorDistOf(v.Position, 2.2f))
            {
                Give(b, p.Map, p, v);
                return;
            }
            if ((p.CurJobDef != JobDefOf.Goto || !(p.CurJob?.targetA.Cell.InHorDistOf(v.Position, 2.5f) ?? false)) && (p.CurJobDef == JobDefOf.Goto || OwnHappenings.Yields(p)))
                OwnHappenings.Walk(p, OwnHappenings.Beside(p.Map, v.Position, p), GenDate.TicksPerHour);
            b.Due(now + 150);
        }

        /// <summary>The coins taken from the stores and pressed into the visitor's hand, a word with it; their people think the better of the colony.</summary>
        private static void Give(OwnHappeningsBook b, Map map, Pawn p, Pawn v)
        {
            // together: the giver walked over to the visitor and stands beside them.
            var s = b.ducat;
            var g = God;
            int left = s.purseCoins;
            int given = 0;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).OrderBy(t => t.stackCount).ToList())
            {
                if (left <= 0) break;
                int n = Mathf.Min(left, t.stackCount);
                var part = t.SplitOff(n);
                if (part.Spawned) part.DeSpawn();
                if (v.inventory.innerContainer.TryAdd(part)) given += n;
                else part.Destroy();
                left -= n;
            }
            s.purseUntil = -1;
            s.giver = s.visitor = null;
            if (given <= 0) return;
            OwnHappenings.Stand(p, v.Position, 240);
            string line = OwnHappenings.LineOf(p, Pool, g, new[] { v.Named("OTHER") }, "purse");
            if (!line.NullOrEmpty()) Speech.Say(p, line, v);
            FleckMaker.ThrowLightningGlow(v.DrawPos, map, 0.35f);
            if (v.Faction != null) StoryChains.Goodwill(v.Faction, OwnHappeningsMath.PurseGoodwill);
            string text = "CC_GodDucat_Purse_Text".Translate(p.Named("PAWN"), given.Named("COUNT"), v.LabelShort.Named("VISITOR"), (v.Faction?.Name ?? "").Named("PEOPLE"),
                Homes.Name(map).Named("HOME")).Resolve();
            OwnHappenings.Line(map, text, new LookTargets(p));
            if (g != null)
            {
                PawnGods.RiteLed(p, g);
                Worship.Booked(g, map, p.Position, "CC_GodDucat_Book_Purse".Translate(p.LabelShort.Named("PAWN"), v.LabelShort.Named("VISITOR")).Resolve(), p);
            }
        }

        // ================= The beetle that leads =================

        private static void Beetle(OwnHappeningsBook b, God g, Map map, int now)
        {
            bool traders = map.mapPawns.AllPawnsSpawned.Any(x => x.RaceProps?.Humanlike == true && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer)
                                                                  && Strangers.KindOf(x) == StrangerKind.Trader);
            var t = OwnHappeningsMath.BeetleTrigger(traders, Silver(map));
            if (t == GodPaceMath.Trigger.None) return;
            var table = GodOmens.Table(map);
            var seer = table == null ? null : Watcher(map, table);
            if (seer == null || !OwnHappenings.Roll(b, OwnHappeningsMath.DucatBeetle, t, g, map, now)) return;
            Lead(b, map, table, seer);
        }

        private static Pawn Watcher(Map map, Building table) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.Position.InHorDistOf(table.Position, 10f) && GenSight.LineOfSight(p.Position, table.Position, map, true))
                .FirstOrDefault();

        public static bool Lead(OwnHappeningsBook b, Map map, Building table, Pawn seer)
        {
            var s = b.ducat;
            if (table == null || seer == null) return false;
            if (!CellFinder.TryFindRandomCellNear(table.Position, map, Mathf.CeilToInt(OwnHappeningsMath.BeetleTo), c => c.Standable(map) && !c.Fogged(map) && map.areaManager.Home[c]
                                                                                                                       && c.GetFirstItem(map) == null && c.DistanceTo(table.Position) >= OwnHappeningsMath.BeetleFrom
                                                                                                                       && seer.CanReach(c, PathEndMode.OnCell, Danger.Some), out var at))
                return false;
            GodSky.Crawl(map, table.TrueCenter(), at.ToVector3Shifted(), 26f);
            try
            {
                var coin = ThingMaker.MakeThing(ThingDefOf.Gold);
                coin.stackCount = 1;
                GenSpawn.Spawn(coin, at, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the coin the beetle led to", e);
                return false;
            }
            s.beetleMap = map;
            s.coinAt = at;
            s.follower = seer;
            s.beetleUntil = Now + GenDate.TicksPerHour * 2;
            OwnHappenings.Stand(seer, table.Position, 200);
            OwnHappenings.Came(b, OwnHappeningsMath.DucatBeetle);
            b.Due(Now + 220);
            return true;
        }

        private static void BeetleStep(OwnHappeningsBook b, int now)
        {
            var s = b.ducat;
            var map = s.beetleMap;
            var p = s.follower;
            if (map == null || p == null || p.Dead || !p.Spawned || p.Map != map || now > s.beetleUntil)
            {
                s.beetleUntil = -1;
                s.follower = null;
                return;
            }
            if (p.Position.InHorDistOf(s.coinAt, 1.6f))
            {
                var g = God;
                s.beetleUntil = -1;
                s.follower = null;
                OwnHappenings.Stand(p, s.coinAt, 240);
                FleckMaker.ThrowLightningGlow(s.coinAt.ToVector3Shifted(), map, 0.4f);
                OwnHappenings.Say(p, Pool, g, "beetle");
                OwnHappenings.Line(map, "CC_GodDucat_Beetle_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(new TargetInfo(s.coinAt, map)));
                if (g != null) OwnHappenings.Saw(g, map, s.coinAt, GodActKind.Omen, p, "CC_GodDucat_Saw_Beetle");
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto && OwnHappenings.Free(p)) OwnHappenings.Walk(p, s.coinAt, GenDate.TicksPerHour);
            b.Due(now + 200);
        }
    }

    /// <summary>Dev mode: Ducat's happenings now (Debug actions, "Rimfolk", "Gods: Ducat's ... now").</summary>
    public static class DucatDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Ducat's trail of coins now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTrail()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var store = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned).OrderByDescending(t => t.stackCount).FirstOrDefault();
            var from = store?.Position ?? OwnHappenings.Middle(map);
            b.ducat.trailUntil = -1;
            Say(DucatWays.Lay(b, map, from, 30) ? "single coins lie from " + (store != null ? "the silver" : "the middle") + " out to the edge; in daylight the first one free follows them." : "no way out to lay them on.");
        }

        [DebugAction("Rimfolk", "Gods: Ducat's open purse now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPurse()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var giver = DucatWays.Giver(map, DucatWays.God, true);
            var v = giver == null ? null : DucatWays.Visitor(map, giver);
            b.ducat.purseUntil = -1;
            Say(DucatWays.BeginPurse(b, map, giver, v) ? giver.LabelShort + " walks over to " + v.LabelShort + " with a few coins." : "no visitor on the land, nobody free, or no silver in the stores.");
        }

        [DebugAction("Rimfolk", "Gods: Ducat's beetle leads to a coin now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeetle()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var table = GodOmens.Table(map);
            var seer = table == null ? null : map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).OrderBy(p => p.Position.DistanceToSquared(table.Position)).FirstOrDefault();
            b.ducat.beetleUntil = -1;
            Say(DucatWays.Lead(b, map, table, seer) ? "a golden beetle walks off the table; " + seer.LabelShort + " follows it to a coin." : "no table, or nobody near one.");
        }
    }
}
