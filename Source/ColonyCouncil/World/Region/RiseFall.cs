using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Towns that rise and fall within a home's reach (docs/design/stage1-region.md, item 2; decisions 2, 4, 5 and 14):
    /// a struggling town given up, leaving a ruin; a town destroyed in a fight leaving a ruin where the game leaves none;
    /// a ruin settled again; and Rimfolk's own new towns (<see cref="NewTowns"/>). Every change is the game's own: a
    /// <see cref="Settlement"/> of the vanilla def made or destroyed, a ruin the game's <see cref="AbandonedSettlement"/>,
    /// so a world without Rimfolk is an ordinary world (Faction Territories redraws its territories by itself, and
    /// Real Ruins may dress a ruin as it does any abandoned settlement; its own sites are left alone).
    /// <para>
    /// The pace (decision 14): at most one town given up per 60 days in a home's reach, one ruin settled again per 120
    /// and one town founded by Rimfolk per 90 (each home keeps its clocks, <see cref="RegionHome"/>); a ruin Rimfolk made
    /// goes after 300 days, twelve at most in a home's reach. Rimfolk's world events count on the same clocks (a town
    /// they give up or found), so nothing is counted twice. Run by the region's two passes: the hour notices a town gone
    /// from the map and puts a fight's ruin down once the game's destroyed settlement has gone; the day makes the rolls.
    /// The setting "Towns rise and fall" switches it all off; the towns' fortunes must be on for anything that rolls on
    /// a town's prosperity. The rules are <see cref="RiseFallMath"/>'s.
    /// </para>
    /// </summary>
    public static class RiseFall
    {
        /// <summary>The setting: towns given up, ruins, ruins settled again, Rimfolk's own new towns.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.regionChange ?? true) && Current.Game != null;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The game's own reach for a town's nearness to a home (it takes goodwill every season within it): 5 tiles.</summary>
        public static int GoodwillTiles
        {
            get
            {
                try
                {
                    return SettlementProximityGoodwillUtility.MaxDist;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("rise and fall: the game's nearness rule", e);
                    return 5;
                }
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: " + what, e);
            }
        }

        // ---------------- Homes and the pace ----------------

        public enum Change
        {
            GiveUp,
            Resettle,
            Found
        }

        /// <summary>The homes whose reach holds a tile (on its layer, in a straight line).</summary>
        public static List<Map> HomesReaching(PlanetTile tile)
        {
            var list = new List<Map>();
            if (!tile.Valid || Find.WorldGrid == null) return list;
            int reach = RegionTowns.ReachTiles;
            foreach (var m in Homes.All)
                if (m != null && m.Tile.Valid && m.Tile.Layer == tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(m.Tile, tile) <= reach)
                    list.Add(m);
            return list;
        }

        private static int Last(RegionHome h, Change c) => h == null ? -1 : c == Change.GiveUp ? h.givenUpAt : c == Change.Resettle ? h.resettledAt : h.foundedAt;

        private static int Gap(Change c) => c == Change.GiveUp ? RiseFallMath.GiveUpGapDays : c == Change.Resettle ? RiseFallMath.ResettleGapDays : RiseFallMath.FoundGapDays;

        /// <summary>A change of this kind may come at the tile now: every home whose reach holds it has had none in its gap.</summary>
        public static bool PaceAllows(RegionBook book, PlanetTile tile, Change c)
        {
            if (book == null) return false;
            var last = HomesReaching(tile).Select(m => Last(book.HomeOf(m), c)).ToList();
            return RiseFallMath.PaceAllows(last, Now, Gap(c));
        }

        /// <summary>A change of this kind came at the tile: each home whose reach holds it starts its gap now.</summary>
        public static void Stamp(RegionBook book, PlanetTile tile, Change c)
        {
            if (book == null) return;
            int now = Now;
            foreach (var m in HomesReaching(tile))
            {
                var h = book.HomeOf(m);
                if (h == null) continue;
                if (c == Change.GiveUp) h.givenUpAt = now;
                else if (c == Change.Resettle) h.resettledAt = now;
                else h.foundedAt = now;
            }
        }

        /// <summary>
        /// Rimfolk's world event "a town abandoned" may give this town up: not while the region's pace holds one back in
        /// a home's reach (decision 14). Beyond every reach, and with the setting off, as before.
        /// </summary>
        public static bool EventMayGiveUp(Settlement t)
        {
            if (t == null || !On) return true;
            try
            {
                return HomesReaching(t.Tile).Count == 0 || PaceAllows(RegionBook.Instance, t.Tile, Change.GiveUp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: the event's pace", e);
                return true;
            }
        }

        /// <summary>One of Rimfolk's world events founded a town (the new town, the other settlers, the crossroads): it counts as one founded by Rimfolk.</summary>
        public static void NoteFounded(Settlement town)
        {
            if (town == null || !On) return;
            Safe("a town founded by an event", () => Stamp(RegionBook.Instance, town.Tile, Change.Found));
        }

        // ---------------- The day ----------------

        /// <summary>The towns kept in detail for any home, each once.</summary>
        internal static List<Settlement> TownsInDetail()
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
        /// The day: ruins looked after (gone, faded, twelve a reach), towns being given up quietly finished, then the rolls
        /// (with the fortunes on): a town given up, a ruin settled again, a town founded. One change of each kind a day at
        /// most.
        /// </summary>
        internal static void Daily(RegionBook book, int now)
        {
            int day = now / GenDate.TicksPerDay;
            var towns = TownsInDetail();
            Safe("ruins", () => Ruins(now));
            Safe("towns being given up", () => FinishClosing(now));
            if (!RegionBook.FortunesOn) return;
            NewTowns.NoteFlourishing(towns, now);
            Safe("a town given up", () => GiveUps(book, towns, day, now));
            Safe("a ruin settled again", () => NewTowns.Resettles(book, day, now));
            Safe("a town founded", () => NewTowns.Foundings(book, towns, day, now));
        }

        // ---------------- A town given up ----------------

        /// <summary>How many of a people's bad world events (ones that cost their towns' fortunes) began in the last RiseFallMath.BlowDays.</summary>
        internal static int Blows(Faction f, int now)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || f == null) return 0;
            int since = now - RiseFallMath.BlowDays * GenDate.TicksPerDay;
            int n = 0;
            foreach (var s in ws.Running)
                if (s?.def != null && s.startTick >= since && (s.def.prosperity < 0f && s.ctx?.faction == f || s.def.prosperityOther < 0f && s.ctx?.enemy == f))
                    n++;
            foreach (var r in ws.history)
            {
                if (r == null || r.faction != f || r.startTick < since) continue;
                var d = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "");
                if (d != null && d.prosperity < 0f) n++;
            }
            return n;
        }

        /// <summary>A world event giving this town up is running (Rimfolk's own, or the region's).</summary>
        private static bool BeingGivenUp(int town)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return false;
            foreach (var s in ws.Running)
                if (s?.def != null && s.def.Worker is SituationWorker_Abandoned && s.Num("town") == town)
                    return true;
            return false;
        }

        /// <summary>How many standing towns a people has.</summary>
        private static int TownsOf(Faction f)
        {
            int n = 0;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s != null && !s.Destroyed && s.Faction == f)
                    n++;
            return n;
        }

        /// <summary>A town in detail struggling long enough, safe to take off the map, its people keeping two more, and the pace allowing: one a day at most, on a 7% roll every 5 days.</summary>
        private static void GiveUps(RegionBook book, List<Settlement> towns, int day, int now)
        {
            foreach (var t in towns)
            {
                if (t.Faction == null || !RiseFallMath.RollDay(day, t.ID, RiseFallMath.GiveUpEveryDays)) continue;
                var rec = WorldLedger.TownRecord(t.ID);
                if (rec == null || !rec.Seeded || rec.closing >= 0 || rec.gone >= 0) continue;
                if (!RiseFallMath.Struggling(rec.prosperity, rec.lowSince, Blows(t.Faction, now), rec.founded, now)) continue;
                bool seat = NotableBook.SeatOf(t.Faction) == t;
                if (!RiseFallMath.MayGiveUp(true, TownsOf(t.Faction), seat, Ages.IsEmpire(t.Faction), SituationWorker_Abandoned.Safe(t))) continue;
                if (BeingGivenUp(t.ID) || !PaceAllows(book, t.Tile, Change.GiveUp) || !Rand.Chance(RiseFallMath.GiveUpChance)) continue;
                Begin(book, t, rec, now);
                return;
            }
        }

        /// <summary>
        /// A town's people begin giving it up: as the world event on that town when a slot is free at the home nearest it
        /// (refugees at the gate, salvage, holdouts while the colony has business there), else quietly: the news of
        /// families on the road, maybe a family of theirs camping at the gate, and the ruin after five days.
        /// </summary>
        public static void Begin(RegionBook book, Settlement t, LedgerTown rec, int now)
        {
            rec.closing = now;
            Stamp(book, t.Tile, Change.GiveUp);
            WorldLedger.TownChanged();
            var home = RegionTowns.NearestHome(t.Tile, out float tiles);
            if (home != null && SituationWorker_GivenUp.Start(home, t) != null) return;
            if (home == null) return;
            RiseFallWords.TellLeaving(home, t, tiles, NextTown(t));
            FamilyAtTheGate(home, t, tiles);
        }

        /// <summary>
        /// The quiet way: a family of theirs may make camp at the gate on the road away (the camps' refugee family), a
        /// third of the time, from a town within 25 tiles; only of their own people (a people at war with the colony sends
        /// none).
        /// </summary>
        private static void FamilyAtTheGate(Map home, Settlement t, float tiles)
        {
            if (tiles > RiseFallMath.ResettleTiles || !Rand.Chance(0.35f)) return;
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(RiseFallEffects.RefugeeCamp);
            var camps = CampTracker.Instance;
            if (kind == null || camps == null || !RiseFallEffects.SendsOwn(kind, t.Faction)) return;
            camps.StartKind(home, kind, t.Faction);
        }

        /// <summary>Their people's nearest other town, where the families go; null for none.</summary>
        public static Settlement NextTown(Settlement t)
        {
            if (t?.Faction == null) return null;
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s == t || s.Destroyed || s.Faction != t.Faction || !s.HasName || s.Tile.Layer != t.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t.Tile);
                if (d >= bestD) continue;
                best = s;
                bestD = d;
            }
            return best;
        }

        /// <summary>The quiet way's fifth day: the ruin when the town can go, else a few families hold on.</summary>
        private static void FinishClosing(int now)
        {
            var l = WorldLedger.Instance;
            if (l == null) return;
            foreach (var rec in l.Towns.Where(r => r != null && r.closing >= 0).ToList())
            {
                var t = NotableBook.TownOf(rec.id);
                if (t == null || t.Destroyed)
                {
                    rec.closing = -1;
                    continue;
                }
                // The world event decides itself, at its end.
                if (BeingGivenUp(t.ID) || now - rec.closing < RiseFallMath.QuietDays * GenDate.TicksPerDay) continue;
                if (SituationWorker_Abandoned.Safe(t) && TownsOf(t.Faction) - 1 >= RiseFallMath.TownsLeft) MakeRuin(t, rec, true);
                else HeldOn(t, true);
            }
        }

        /// <summary>A few families would not leave: the town stays on the map, and isn't looked at again for a while.</summary>
        public static void HeldOn(Settlement t, bool tell)
        {
            var rec = t != null ? WorldLedger.TownRecord(t.ID) : null;
            if (rec == null) return;
            rec.closing = -1;
            // Low again from now: another month before anyone thinks of leaving.
            if (rec.lowSince >= 0) rec.lowSince = Now;
            WorldLedger.TownChanged();
            if (!tell) return;
            var home = RegionTowns.NearestHome(t.Tile, out float tiles);
            if (home != null) RiseFallWords.TellHeldOn(home, t, tiles);
        }

        /// <summary>
        /// A town given up: its notables the colony dealt with move to their people's nearest town, the town comes off the
        /// map, the game's abandoned settlement takes its tile in their colour, and the ledger keeps the ruin. The history
        /// has it (world:abandoned); <paramref name="tell"/>: the colony news says so (the world event's own letter tells
        /// it otherwise). Returns the ruin, or null when it couldn't be made.
        /// </summary>
        public static WorldObject MakeRuin(Settlement town, LedgerTown rec, bool tell)
        {
            if (town == null || town.Destroyed) return null;
            var tile = town.Tile;
            var people = town.Faction;
            string name = town.HasName ? town.Name : rec?.name;
            int id = town.ID;
            var next = NextTown(town);
            var home = RegionTowns.NearestHome(tile, out float tiles);
            Safe("its notables", () => MoveNotables(town, next));
            WorldObject ruin;
            try
            {
                ruin = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedSettlement);
                ruin.Tile = tile;
                ruin.SetFaction(people);
                town.Destroy();
                Find.WorldObjects.Add(ruin);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a town given up", e);
                return null;
            }
            int now = Now;
            WorldLedger.AddRuin(new LedgerRuin { town = id, name = name, people = people, ruin = ruin.ID, tile = Refs.Of(tile), since = now, why = LedgerRuin.GivenUp });
            if (rec != null)
            {
                rec.kind = TownKind.Ruin;
                rec.closing = -1;
                if (rec.gone < 0) rec.gone = now;
                WorldLedger.TownChanged();
            }
            NotableBook.Changed();
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Refs.Of(tile), HistoryRole.About) };
            History.Add(RiseFallWords.GivenUpKind, actors, Ref.Town(id), WorldMath.Weight(false), RiseFallWords.HistoryGivenUp(people, name),
                values: name.NullOrEmpty() ? null : new List<string> { name });
            if (tell && home != null) RiseFallWords.TellEmpty(home, ruin, people, name, tiles);
            return ruin;
        }

        /// <summary>The notables the colony dealt with in a town given up go on as acquaintances, living in their people's nearest town; the rest are let go by the notables' own day.</summary>
        private static void MoveNotables(Settlement town, Settlement next)
        {
            var book = NotableBook.Instance;
            if (book == null || town == null) return;
            foreach (var n in book.notables)
            {
                if (n == null || n.town != town.ID || n.notes.Count == 0 || n.faction != town.Faction) continue;
                n.role = NotableRole.Acquaintance;
                n.town = next?.ID ?? -1;
                n.townName = next?.Name;
            }
        }

        // ---------------- Ruins ----------------

        /// <summary>The world objects by id (looked up once a pass).</summary>
        private static Dictionary<int, WorldObject> ById()
        {
            var d = new Dictionary<int, WorldObject>();
            foreach (var o in Find.WorldObjects.AllWorldObjects)
                if (o != null && !o.Destroyed)
                    d[o.ID] = o;
            return d;
        }

        public static WorldObject WorldObjectOf(int id)
        {
            if (id < 0) return null;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
                if (o != null && o.ID == id && !o.Destroyed)
                    return o;
            return null;
        }

        /// <summary>A ruin can come off the map without breaking anything: no map of it, no caravan there or on the way, no pods on their way, no quest looking at it.</summary>
        public static bool RuinSafe(WorldObject o)
        {
            if (o == null || o.Destroyed || o is MapParent mp && mp.HasMap) return false;
            var tile = o.Tile;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c.Tile == tile || c.pather != null && c.pather.Moving && c.pather.Destination == tile)
                    return false;
            foreach (var pods in Find.WorldObjects.TravellingTransporters)
                if (pods.destinationTile == tile)
                    return false;
            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q.State != QuestState.Ongoing && q.State != QuestState.NotYetAccepted) continue;
                if (q.QuestLookTargets.Any(l => l.HasWorldObject && l.WorldObject == o || l.Tile == tile)) return false;
            }
            return true;
        }

        /// <summary>
        /// The ruins Rimfolk made: one another took off the map (or that lies beyond every reach now) is let go; one past
        /// its 300 days goes from the map; past twelve in a home's reach, the oldest goes.
        /// </summary>
        private static void Ruins(int now)
        {
            var l = WorldLedger.Instance;
            if (l == null || l.Ruins.Count == 0) return;
            var objects = ById();
            foreach (var r in l.Ruins.ToList())
            {
                if (r.Waiting) continue;
                if (!objects.TryGetValue(r.ruin, out var o) || HomesReaching(o.Tile).Count == 0)
                {
                    WorldLedger.RemoveRuin(r);
                    continue;
                }
                if (RiseFallMath.RuinFaded(r.since, now) && RuinSafe(o)) Fade(r, o, true);
            }
            foreach (var home in Homes.All)
            {
                if (home == null || !home.Tile.Valid) continue;
                var mine = l.Ruins.Where(r => !r.Waiting && objects.TryGetValue(r.ruin, out var o) && !o.Destroyed && o.Tile.Layer == home.Tile.Layer
                                              && Find.WorldGrid.ApproxDistanceInTiles(o.Tile, home.Tile) <= RegionTowns.ReachTiles).ToList();
                foreach (int i in RiseFallMath.OverCap(mine.Select(r => r.since).ToList()))
                {
                    var o = objects[mine[i].ruin];
                    if (RuinSafe(o)) Fade(mine[i], o, false);
                    // Not safe to take off the map now: it stays an ordinary ruin, no longer Rimfolk's to keep.
                    else WorldLedger.RemoveRuin(mine[i]);
                }
            }
        }

        /// <summary>A ruin's time is over: it goes from the map, the history keeps that it went, and the colony news says so when <paramref name="tell"/>.</summary>
        private static void Fade(LedgerRuin r, WorldObject o, bool tell)
        {
            var tile = o.Tile;
            try
            {
                o.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a ruin gone", e);
                return;
            }
            WorldLedger.RemoveRuin(r);
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(tile), HistoryRole.About) };
            if (r.people != null) actors.Insert(0, new HistoryActor(Refs.Of(r.people), HistoryRole.About));
            History.Add(RiseFallWords.FadedKind, actors, Ref.Town(r.town), HistoryMath.Everyday, RiseFallWords.HistoryFaded(r.name),
                values: r.name.NullOrEmpty() ? null : new List<string> { r.name });
            if (!tell) return;
            var home = RegionTowns.NearestHome(tile, out float tiles);
            if (home != null) RiseFallWords.TellFaded(home, tile, r, tiles);
        }

        // ---------------- The hour: towns gone, fights' ruins ----------------

        /// <summary>A town in detail as the hour last saw it: where it stood, whose, its name.</summary>
        private sealed class Seen
        {
            public PlanetTile tile;
            public Faction people;
            public string name;
        }

        private static readonly Dictionary<int, Seen> seen = new Dictionary<int, Seen>();
        private static Game seenGame;

        /// <summary>The hour: a town in detail gone since the last hour is looked at, and fights' ruins waiting are put down.</summary>
        internal static void Hourly(RegionBook book, int now)
        {
            Vanished(book, now);
            PlaceWaiting(now);
        }

        /// <summary>
        /// Towns in detail gone from the map since the last hour: one that left the game's destroyed settlement on its tile
        /// fell in a fight (its ruin waits for that to go); one that left an abandoned settlement was given up by
        /// Rimfolk's world event "a town abandoned", whose ruin the region keeps from now on and counts on the pace.
        /// Anything else (a town taken, made a vassal, removed by another mod) is not Rimfolk's to mark.
        /// </summary>
        private static void Vanished(RegionBook book, int now)
        {
            if (seenGame != Current.Game)
            {
                seenGame = Current.Game;
                seen.Clear();
            }
            if (seen.Count > 0)
            {
                var standing = new HashSet<int>();
                foreach (var s in Find.WorldObjects.Settlements)
                    if (s != null && !s.Destroyed)
                        standing.Add(s.ID);
                foreach (var kv in seen)
                    if (!standing.Contains(kv.Key))
                        Gone(book, kv.Key, kv.Value, now);
            }
            seen.Clear();
            foreach (var r in RegionTowns.Reach)
                foreach (var t in r.towns)
                    if (t != null && !t.Destroyed && !seen.ContainsKey(t.ID))
                        seen[t.ID] = new Seen { tile = t.Tile, people = t.Faction, name = t.HasName ? t.Name : null };
        }

        private static void Gone(RegionBook book, int id, Seen was, int now)
        {
            if (WorldLedger.RuinOfTown(id) != null || !was.tile.Valid) return;
            WorldObject abandoned = null;
            bool destroyed = false;
            foreach (var o in Find.WorldObjects.ObjectsAt(was.tile))
            {
                if (o is DestroyedSettlement) destroyed = true;
                else if (o.def == WorldObjectDefOf.AbandonedSettlement) abandoned = o;
            }
            if (destroyed)
            {
                Fell(id, was.tile, was.people, was.name, false);
                return;
            }
            if (abandoned == null || !GivenUpByEvent(id)) return;
            WorldLedger.AddRuin(new LedgerRuin { town = id, name = was.name, people = was.people, ruin = abandoned.ID, tile = Refs.Of(was.tile), since = now, why = LedgerRuin.GivenUp });
            Stamp(book, was.tile, Change.GiveUp);
            var rec = WorldLedger.TownRecord(id);
            if (rec != null)
            {
                rec.kind = TownKind.Ruin;
                rec.closing = -1;
                WorldLedger.TownChanged();
            }
        }

        /// <summary>Rimfolk's world event "a town abandoned" gave this town up (its record says it emptied).</summary>
        private static bool GivenUpByEvent(int id)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return false;
            var place = Ref.Town(id);
            for (int i = ws.history.Count - 1; i >= 0; i--)
            {
                var r = ws.history[i];
                if (r == null || r.place != place) continue;
                return r.defName == "Council_WS_Abandoned" && r.ending != "holdouts" && r.ending != WorldSituations.Quietly;
            }
            return false;
        }

        /// <summary>
        /// A town in a home's reach destroyed in a fight (by the colony's people, through the game's defeat of a town; or
        /// another people's, when the game's destroyed settlement is found on its tile): a ruin will stand there once the
        /// destroyed settlement and its map are gone (decision 5).
        /// </summary>
        public static void Fell(int id, PlanetTile tile, Faction people, string name, bool byColony)
        {
            if (!On || id < 0 || !tile.Valid || people == null || people.IsPlayer || people.def == null || !people.def.humanlikeFaction) return;
            if (WorldLedger.RuinOfTown(id) != null || HomesReaching(tile).Count == 0) return;
            WorldLedger.AddRuin(new LedgerRuin { town = id, name = name, people = people, ruin = -1, tile = Refs.Of(tile), since = Now, why = LedgerRuin.Fight, byColony = byColony });
        }

        /// <summary>Fights' ruins waiting: put down when nothing but a caravan stands on the tile; let go when something else took it (a town, a vassal, the colony's own) or after 30 days.</summary>
        private static void PlaceWaiting(int now)
        {
            var l = WorldLedger.Instance;
            if (l == null) return;
            foreach (var r in l.Ruins.Where(x => x.Waiting).ToList())
            {
                var tile = TileOf(r.tile);
                if (!tile.Valid || RiseFallMath.WaitOver(r.since, now) || HomesReaching(tile).Count == 0)
                {
                    WorldLedger.RemoveRuin(r);
                    continue;
                }
                bool wait = false, taken = false;
                foreach (var o in Find.WorldObjects.ObjectsAt(tile))
                {
                    if (o is DestroyedSettlement || o is Caravan || o is RoutePlannerWaypoint) wait = true;
                    else taken = true;
                }
                if (taken)
                {
                    WorldLedger.RemoveRuin(r);
                    continue;
                }
                if (wait || Current.Game.FindMap(tile) != null) continue;
                WorldObject ruin;
                try
                {
                    ruin = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedSettlement);
                    ruin.Tile = tile;
                    if (r.people != null && Find.FactionManager.AllFactionsListForReading.Contains(r.people)) ruin.SetFaction(r.people);
                    Find.WorldObjects.Add(ruin);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("rise and fall: a fight's ruin", e);
                    WorldLedger.RemoveRuin(r);
                    continue;
                }
                r.ruin = ruin.ID;
                var rec = WorldLedger.TownRecord(r.town);
                if (rec != null)
                {
                    rec.kind = TownKind.Ruin;
                    WorldLedger.TownChanged();
                }
                var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(r.people), HistoryRole.Against), new HistoryActor(Refs.Of(tile), HistoryRole.About) };
                var home = RegionTowns.NearestHome(tile, out float tiles);
                if (r.byColony && home != null) actors.Insert(0, new HistoryActor(Refs.Colony(home), HistoryRole.Did));
                History.Add(RiseFallWords.FellKind, actors, Ref.Town(r.town), WorldMath.Weight(r.byColony), RiseFallWords.HistoryFell(r.people, r.name),
                    values: r.name.NullOrEmpty() ? null : new List<string> { r.name });
                if (home != null) RiseFallWords.TellFell(home, ruin, r, tiles);
            }
        }

        /// <summary>A tile ref back to the planet's tile while its layer is there, else invalid.</summary>
        public static PlanetTile TileOf(Ref r)
        {
            if (r.kind != RefKind.Tile || Find.WorldGrid == null) return PlanetTile.Invalid;
            try
            {
                return Find.WorldGrid.PlanetLayers.ContainsKey(r.TileLayer) ? new PlanetTile(r.TileId, r.TileLayer) : PlanetTile.Invalid;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a ruin's tile", e);
                return PlanetTile.Invalid;
            }
        }

        // ---------------- Asking ----------------

        /// <summary>The ruin kept for an abandoned settlement on the map, or null.</summary>
        public static LedgerRuin RuinOf(WorldObject o) => o == null || !On ? null : WorldLedger.RuinAt(o.ID);

        /// <summary>A town whose people are giving it up now.</summary>
        public static bool IsClosing(int town)
        {
            var rec = WorldLedger.TownRecord(town);
            return rec != null && rec.closing >= 0 && rec.gone < 0;
        }
    }
}
