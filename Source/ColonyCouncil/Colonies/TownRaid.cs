using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A raid on a town the colony was (docs/design/stage3-many-colonies.md, section 2, "The bad side": raided; release 5;
    /// Defs/WorldSituationDefs/Rimfolk_WS_TownRaid.xml). A world event of the world events' own rhythm (it adds no event to
    /// the year, it is one of those that can come), only while a town the colony was stands at peace with the colony and a
    /// people at war with both can field a band against it:
    /// <list type="bullet">
    /// <item><b>The call.</b> Its head's runner (tribal), rider (medieval), radio (industrial) or comms (spacer), by the
    /// town's own means, asks the colony for help ({OTHER} the head, the beat "call", the runner or rider at the gate by
    /// VisitEffect_AtGate).</item>
    /// <item><b>Sent.</b> The caravan the colony forms (by its means: on foot, with pack animals, by pods or shuttle that
    /// land as a caravan) of two grown colonists or more, bound for the town, is waited for: five days, and on while it is
    /// on the road. At the town, its map is made the game's way (the colony as it was left, its people defending it:
    /// Colonies/TownLayout.cs, its defenders by the raid's points), the colony's fighters come in at an edge, the raiders of
    /// the other people at the far one under the game's own assault lord. A real fight with the game's own harm: every
    /// raider down or fled, it was defended; nobody standing against them, it was sacked; the colony's people gone first, it
    /// goes by those left standing.</item>
    /// <item><b>Not sent</b> (refused, or never came): the outcome follows the game's measure of the two sides' points
    /// (<see cref="TownLifeMath.Alone"/>): held, hurt or sacked, its fortune, its goods and some of its other people lost;
    /// a former colonist may die only in a raid it lost (decision 11B), named in the letter.</item>
    /// </list>
    /// The town's map stands only while the colony's people are on it; the game's own defeat of a town that isn't at war with
    /// the colony stands down for it (Patch_TownRaidNotDefeated). Without the deeper hooks no map of it is made, and the
    /// colony is not asked to send anyone.
    /// </summary>
    public class SituationWorker_TownRaid : WorldSituationWorker
    {
        public const string DefName = "Rimfolk_WS_TownRaid";

        public static WorldSituationDef Def => DefDatabase<WorldSituationDef>.GetNamedSilentFail(DefName);

        /// <summary>A town chosen by the dev tools (or none: any that fits).</summary>
        private static LedgerFormer pending;

        /// <summary>Starts a raid on this town now (dev tools: <paramref name="force"/> leaves the settings, the day and the limit of two aside).</summary>
        public static WorldSituation Start(Map home, LedgerFormer rec, bool force)
        {
            var def = Def;
            var ws = WorldSituations.Instance;
            if (def == null || ws == null || home == null || rec == null) return null;
            pending = rec;
            try
            {
                return ws.TryStart(home, d => d == def, force);
            }
            finally
            {
                pending = null;
            }
        }

        // ================= Who and where =================

        /// <summary>A town the colony was that a raid can come to now: standing, at peace with the colony, its map closed, its people not at the heart of another world event, not giving itself up.</summary>
        public static bool Fits(LedgerFormer rec)
        {
            if (rec == null || !rec.Standing || rec.end != TownEnd.None || rec.gateAt >= 0 || rec.people.HostileTo(Faction.OfPlayer)) return false;
            var town = rec.Town;
            if (town == null || town.HasMap || (WorldSituations.Instance?.Busy(rec.people) ?? false)) return false;
            return Raider(rec, false) != null;
        }

        private static IEnumerable<LedgerFormer> Towns() => WorldLedger.Formers.Where(Fits);

        /// <summary>
        /// Who raids it: a people at war with both the town's people and the colony, that can field a band, the nearer its
        /// towns lie the likelier (a people of no town near, the wide world's raiders, at the least weight). Null for none.
        /// </summary>
        public static Faction Raider(LedgerFormer rec, bool pick = true)
        {
            var town = rec?.Town;
            if (town == null) return null;
            var list = new List<(Faction f, float w)>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f == rec.people || f.IsPlayer || f.Hidden || f.defeated || f.temporary || !f.def.humanlikeFaction) continue;
                if (!f.HostileTo(rec.people) || !f.HostileTo(Faction.OfPlayer) || !CallsToArms.Fields(f)) continue;
                float near = 9999f;
                foreach (var t in Find.WorldObjects.Settlements)
                    if (t != null && t.Faction == f && !t.Destroyed && t.Tile.Layer == town.Tile.Layer)
                        near = Mathf.Min(near, Find.WorldGrid.ApproxDistanceInTiles(t.Tile, town.Tile));
                list.Add((f, Mathf.Lerp(2f, 0.3f, Mathf.Clamp01(near / 60f))));
                if (!pick) return f;
            }
            return list.Count == 0 ? null : list.RandomElementByWeight(x => x.w).f;
        }

        protected override bool CanHappen(Map map) => TownLife.On && (pending != null ? Fits(pending) : Towns().Any());

        public static LedgerFormer RecOf(WorldSituation s) => s == null ? null : WorldLedger.FormerOf(s.Num("town"));

        public override bool Cast(WorldSituation s, Map map)
        {
            var rec = pending != null ? Fits(pending) ? pending : null : Towns().RandomElementWithFallback();
            var town = rec?.Town;
            var enemy = rec != null ? Raider(rec) : null;
            if (town == null || enemy == null || map == null) return false;
            s.ctx.map = map;
            s.ctx.faction = rec.people;
            s.ctx.enemy = enemy;
            s.At(town);
            s.ctx.other = rec.people.leader != null && !rec.people.leader.Dead ? rec.people.leader : null;
            s.ctx.tale = SituationWorker_NewTown.Where(map, town.Tile);
            s.ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            s.SetNum("town", rec.key);
            // The town's own means of calling (its age at the hand-over): a runner or a rider at the gate, the radio, comms.
            s.flags.Add(TownLifeMath.CallMeans(rec.age));
            if (s.ctx.other == null) s.flags.Add("nohead");
            if (!RimfolkPatches.On) s.flags.Add("noMap");
            var t = WorldLedger.TownRecord(rec.key);
            // The raiders come by what a town of its kind can stand against; how many of its people stand there is its fortune's.
            float kind = Rand.Range(TownLifeMath.DefendMin, TownLifeMath.DefendMax);
            s.SetNum("defend", Mathf.RoundToInt(TownLifeMath.DefendPoints(kind, t != null && t.Seeded ? t.prosperity : -1f)));
            s.SetNum("raid", Mathf.RoundToInt(TownLifeMath.RaidPoints(kind, Rand.Value)));
            return true;
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var town = RecOf(s)?.Town;
            return town != null ? new LookTargets(town) : LookTargets.Invalid;
        }

        protected override bool StillPossible(WorldSituation s)
        {
            var rec = RecOf(s);
            return rec != null && rec.Standing && rec.end == TownEnd.None && !rec.people.HostileTo(Faction.OfPlayer);
        }

        /// <summary>The town no longer the people's, or at war with the colony: "gone"; taken back by the colony meanwhile, it ends quietly (the colony's own now).</summary>
        protected override string GoneEnding(WorldSituation s) =>
            RecOf(s)?.end == TownEnd.TakenBack || def.Ending("gone") == null || s.ctx.faction == null ? WorldSituations.Quietly : "gone";

        // ================= The answer =================

        /// <summary>
        /// The call answered: sent, the caravan window opens on the home the letter came to and the town holds out for them;
        /// refused (or the question left), the raid comes within a day or so, and the town remembers.
        /// </summary>
        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat?.key != "call") return outcome;
            int now = Now;
            var rec = RecOf(s);
            if (s.Has("sending") && !byDefault)
            {
                s.SetNum("holdUntil", now + TownLifeMath.HoldDays * GenDate.TicksPerDay);
                var map = ctx.map ?? HappeningTracker.HomeMap();
                if (map != null)
                    try
                    {
                        Current.Game.CurrentMap = map;
                        Find.WindowStack.Add(new Dialog_FormCaravan(map));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("town life: the fighters' caravan", e);
                    }
                return outcome;
            }
            s.Clear("sending");
            s.Set("refused");
            s.SetNum("raidAt", now + Mathf.RoundToInt(Rand.Range(TownLifeMath.RaidAfterMin, TownLifeMath.RaidAfterMax) * GenDate.TicksPerDay));
            Goodwill(rec?.people, TownLifeMath.RefusedGoodwill, false);
            if (rec?.people != null && FactionMemory.On) FactionMemory.Note(rec.people, "refused", TownsAfter.TownName(rec));
            return outcome;
        }

        // ================= While it lasts =================

        public override string Hourly(WorldSituation s)
        {
            var rec = RecOf(s);
            if (s.Num("fight") == 1) return Watch(s, rec);
            int now = Now;
            // The map being made (a long event): wait for it, an hour at most (a long event never run starts over).
            if (s.Num("fight") == 2)
            {
                if (now - s.Num("making") < GenDate.TicksPerHour) return null;
                s.SetNum("fight", 0);
            }
            if (!StillPossible(s)) return GoneEnding(s);
            if (s.Has("refused") && s.Num("raidAt") > 0 && now >= s.Num("raidAt")) return Alone(s, rec);
            if (!s.Has("sending")) return null;
            var town = rec.Town;
            var c = Answering(town);
            if (c != null)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f && !town.HasMap) Begin(s, rec, c);
                // On the road: the town holds out for them, as long as the event lasts.
                return null;
            }
            if (now < s.Num("holdUntil")) return null;
            // Nobody came: the town met them alone, and remembers the colony's word.
            s.Set("nobodyCame");
            Goodwill(rec.people, TownLifeMath.NoShowGoodwill, false);
            if (FactionMemory.On) FactionMemory.Note(rec.people, "letDown", TownsAfter.TownName(rec));
            return Alone(s, rec);
        }

        /// <summary>Time ran out: the fight there by those left standing, else the raid nobody of the colony saw.</summary>
        public override SituationEnding TimeoutEnding(WorldSituation s)
        {
            var rec = RecOf(s);
            string key;
            // No longer theirs: its "gone" letter; the colony's own again, quietly (no ending is the quiet end).
            if (rec == null || !StillPossible(s)) return def.Ending(GoneEnding(s));
            if (s.Num("fight") == 1)
            {
                var e = TownLifeMath.OnMap(s.Num("raidersUp"), s.Num("townUp"), s.Num("colonyUp"), true);
                key = Finish(s, rec, e == TownRaidEnd.None ? TownRaidEnd.Hurt : e, true);
            }
            else key = Alone(s, rec);
            return def.Ending(key) ?? def.endings.LastOrDefault(e => e.timeout);
        }

        /// <summary>A caravan of the colony's with two grown colonists or more on their feet, bound for the town or within a tile of it.</summary>
        public static Caravan Answering(Settlement town)
        {
            if (town == null) return null;
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || c.Tile.Layer != town.Tile.Layer) continue;
                int grown = c.PawnsListForReading.Count(p => p != null && p.IsColonist && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.Dead);
                bool bound = c.pather != null && c.pather.Moving && c.pather.Destination == town.Tile;
                bool there = Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                if (TownLifeMath.Answers(grown, bound || there)) return c;
            }
            return null;
        }

        // ================= The fight on the town's map =================

        /// <summary>The colony's fighters at the town: its map is made, in a long event as the game makes a map it attacks.</summary>
        private void Begin(WorldSituation s, LedgerFormer rec, Caravan c)
        {
            s.SetNum("fight", 2);
            s.SetNum("making", Now);
            int id = s.id, key = rec.key, caravan = c.ID;
            LongEventHandler.QueueLongEvent(() => MakeFight(id, key, caravan), "GeneratingMapForNewEncounter", false, null);
        }

        private static void MakeFight(int id, int key, int caravanId)
        {
            var s = WorldSituations.Instance?.Get(id);
            var rec = WorldLedger.FormerOf(key);
            var c = Find.WorldObjects.Caravans.FirstOrDefault(x => x.ID == caravanId);
            var town = rec?.Town;
            if (s == null || s.ended || s.Num("fight") != 2) return;
            try
            {
                if (town == null || c == null || c.Destroyed || town.HasMap || s.ctx.enemy == null)
                {
                    s.SetNum("fight", 0);
                    return;
                }
                var g = rec.layout?.grid;
                var size = g != null && g.mapX > 0 && g.mapZ > 0 ? new IntVec3(g.mapX, 1, g.mapZ) : Find.World.info.initialMapSize;
                Map map;
                TownLayouts.DefendPoints = s.Num("defend");
                try
                {
                    map = GetOrGenerateMapUtility.GetOrGenerateMap(town.Tile, size, null);
                }
                finally
                {
                    TownLayouts.DefendPoints = 0f;
                }
                if (map == null)
                {
                    s.SetNum("fight", 0);
                    return;
                }
                // The game counts down to its own raids on a map of a town it isn't at war with: not this one.
                town.GetComponent<TimedDetectionRaids>()?.ResetCountdown();
                CaravanEnterMapUtility.Enter(c, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, true);
                var colony = map.mapPawns.FreeColonistsSpawned.ToList();
                var raiders = Raiders(s.ctx.enemy, s.Num("raid"), town.Tile);
                var at = FarEdge(map, colony);
                foreach (var p in raiders) GenSpawn.Spawn(p, CellFinder.RandomSpawnCellForPawnNear(at, map), map);
                if (raiders.Count > 0) LordMaker.MakeNewLord(s.ctx.enemy, new LordJob_AssaultColony(s.ctx.enemy, false, true, false, false, false), map, raiders);
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                // The colony's former people who stand with the town (its notables at home): a death among them is seen.
                foreach (var p in rec.formers)
                    if (p != null && p.Spawned && p.Map == map && !p.Dead)
                        s.SetNum("f" + p.thingIDNumber, 1);
                s.SetNum("fight", 1);
                s.SetNum("since", Find.TickManager.TicksGame);
                // The colony's part, from when its fighters are there (an answer alone sends nobody).
                s.help += 2;
                s.deeds.Add("CC_TownLife_Deed".Translate(TownsAfter.TownName(rec).Named("TOWN")).Resolve());
                Count(s, rec, map);
                Current.Game.CurrentMap = map;
                string townName = TownsAfter.TownName(rec);
                string text = "CC_TownLife_FightText".Translate(townName.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE"), s.ctx.enemy.Name.Named("ENEMY"),
                    Foundings.Names(colony).Named("NAMES"), (rec.colonyName ?? townName).Named("COLONY")).Resolve();
                var formers = rec.formers.Where(p => p != null && p.Spawned && p.Map == map).ToList();
                if (formers.Count > 0) text += "\n\n" + "CC_TownLife_FightFormers".Translate(Foundings.Names(formers).Named("NAMES"), townName.Named("TOWN")).Resolve();
                if (rec.layout != null) text += "\n\n" + "CC_TownLife_FightLayout".Translate((rec.colonyName ?? townName).Named("COLONY")).Resolve();
                Tell.Letter(NewsKind.World, Importance.Important, map, "CC_TownLife_FightLabel".Translate(townName.Named("TOWN")).Resolve(), text, LetterDefOf.ThreatBig,
                    new LookTargets(raiders.Where(p => p.Spawned).Cast<Thing>()), s.ctx.enemy);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: the fight at the town", e);
                if (s.Num("fight") == 2) s.SetNum("fight", 0);
            }
        }

        /// <summary>The raiders, as their people fight, worth the raid's points.</summary>
        private static List<Pawn> Raiders(Faction f, float points, PlanetTile tile)
        {
            var list = new List<Pawn>();
            if (f?.def?.pawnGroupMakers == null) return list;
            try
            {
                float min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
                var parms = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = Mathf.Max(points, min * 1.05f), tile = tile, generateFightersOnly = true };
                list.AddRange(PawnGroupMakerUtility.GeneratePawns(parms));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: the raiders made", e);
            }
            return list;
        }

        /// <summary>An edge cell the raiders come in at: of a few the game would give a raid, the farthest from the colony's fighters.</summary>
        private static IntVec3 FarEdge(Map map, List<Pawn> colony)
        {
            var best = IntVec3.Invalid;
            float far = -1f;
            for (int i = 0; i < 8; i++)
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Hostile)) continue;
                float d = colony.Count == 0 ? 0f : colony.Min(p => p.Position.DistanceToSquared(cell));
                if (d > far)
                {
                    far = d;
                    best = cell;
                }
            }
            return best.IsValid ? best : CellFinder.RandomEdgeCell(map);
        }

        /// <summary>Who stands on the town's map: the raiders on their feet (not fled), the town's people, the colony's.</summary>
        private static void Count(WorldSituation s, LedgerFormer rec, Map map)
        {
            var enemy = s.ctx.enemy;
            int raiders = enemy == null ? 0 : map.mapPawns.SpawnedPawnsInFaction(enemy).Count(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.IsPrisoner && p.MentalStateDef != MentalStateDefOf.PanicFlee);
            int town = map.mapPawns.SpawnedPawnsInFaction(rec.people).Count(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.IsPrisoner);
            int colony = map.mapPawns.FreeColonistsSpawned.Count(p => !p.Downed && !p.Dead);
            s.SetNum("raidersUp", raiders);
            s.SetNum("townUp", town);
            s.SetNum("colonyUp", colony);
        }

        /// <summary>The fight there, every hour: over when the raiders are down or fled, or nobody stands against them; the map closed, by those left standing.</summary>
        private string Watch(WorldSituation s, LedgerFormer rec)
        {
            if (rec == null) return "gone";
            var map = rec.Town?.Map;
            if (map == null) return Finish(s, rec, TownLifeMath.OnMap(s.Num("raidersUp"), s.Num("townUp"), s.Num("colonyUp"), true), true);
            Count(s, rec, map);
            // An hour at least, so a raid that has only just come in is a fight.
            if (Now - s.Num("since") < GenDate.TicksPerHour) return null;
            var e = TownLifeMath.OnMap(s.Num("raidersUp"), s.Num("townUp"), s.Num("colonyUp"), false);
            return e == TownRaidEnd.None ? null : Finish(s, rec, e, true);
        }

        // ================= How it ends =================

        /// <summary>The raid nobody of the colony saw: by the two sides' points (the game's own measure).</summary>
        private string Alone(WorldSituation s, LedgerFormer rec)
        {
            if (rec == null || !rec.Standing) return "gone";
            float raid = s.Num("raid"), defend = s.Num("defend");
            return Finish(s, rec, TownLifeMath.Alone(raid, defend, Rand.Value), false);
        }

        /// <summary>
        /// What it cost and the ending to tell: its former colonists who died (in the fight the player saw, or one at most in a
        /// raid it lost alone, decision 11B), its other people lost (its notables never the colony's), its fortune and goods,
        /// and what the town keeps of the colony's part. The raiders still on its map take what they came for and go.
        /// </summary>
        private string Finish(WorldSituation s, LedgerFormer rec, TownRaidEnd e, bool onMap)
        {
            // Told once: a second look (the time running out as the fight ends) tells the same.
            if (s.Num("finished") == 1) return Key((TownRaidEnd)s.Num("end"), s.Has("seen"));
            s.SetNum("finished", 1);
            s.SetNum("end", (int)e);
            string townName = TownsAfter.TownName(rec);
            var dead = new List<Pawn>();
            var others = new List<Pawn>();
            try
            {
                if (onMap)
                {
                    foreach (var p in rec.formers)
                        if (p != null && p.Dead && s.Num("f" + p.thingIDNumber) == 1)
                            dead.Add(p);
                }
                else
                {
                    float ratio = TownLifeMath.Ratio(s.Num("raid"), s.Num("defend"));
                    if (Rand.Chance(TownLifeMath.FormerDeathChance(e, ratio)))
                    {
                        var p = rec.formers.Where(x => TownLayouts.FreeAtHome(x, rec.people) && x != rec.people.leader && FreeInWorld(x)).RandomElementWithFallback();
                        if (p != null && DieOffscreen(p)) dead.Add(p);
                    }
                    int lost = TownLifeMath.OthersLost(e);
                    var book = NotableBook.Instance;
                    if (lost > 0 && book != null)
                        foreach (var n in book.notables.Where(n => n != null && n.town == rec.key && n.Alive && n.pawn != null && !rec.formers.Contains(n.pawn) && n.pawn != rec.people.leader)
                                     .InRandomOrder().Take(lost).ToList())
                            if (FreeInWorld(n.pawn) && DieOffscreen(n.pawn))
                                others.Add(n.pawn);
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("town life: who the raid took", ex);
            }
            if (dead.Count > 0)
            {
                s.Set("died");
                if (dead.Count > 1) s.Set("diedMany");
                s.ctx.dead = Foundings.Names(dead);
            }
            if (others.Count > 0)
            {
                s.Set("others");
                s.ctx.item = Foundings.Names(others);
            }
            if (onMap) s.Set("seen");
            TownLife.MoveFortune(rec, -TownLifeMath.FortuneLoss(e), def.LabelCap.ToString());
            TownLife.LoseGoods(rec.Town, TownLifeMath.GoodsLost(e));
            if (e == TownRaidEnd.Defended)
            {
                Goodwill(rec.people, TownLifeMath.DefendedGoodwill, false);
                if (FactionMemory.On) FactionMemory.Note(rec.people, "townDefended", townName);
            }
            foreach (var p in dead) TownLife.FormerDied(rec, p);
            if (onMap) Leave(s, rec);
            WorldLedger.FormerChanged();
            return Key(e, onMap);
        }

        /// <summary>The ending told: defended, sacked with the colony's fighters there (fell) or without, hurt, held.</summary>
        private static string Key(TownRaidEnd e, bool onMap)
        {
            switch (e)
            {
                case TownRaidEnd.Defended: return "defended";
                case TownRaidEnd.Sacked: return onMap ? "fell" : "sacked";
                case TownRaidEnd.Hurt: return "hurt";
                default: return "held";
            }
        }

        /// <summary>The raiders still on the town's map, once it is decided, take what they came for and go.</summary>
        private static void Leave(WorldSituation s, LedgerFormer rec)
        {
            var map = rec?.Town?.Map;
            var enemy = s.ctx.enemy;
            if (map == null || enemy == null) return;
            try
            {
                var up = map.mapPawns.SpawnedPawnsInFaction(enemy).Where(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.IsPrisoner).ToList();
                if (up.Count == 0) return;
                foreach (var p in up) p.GetLord()?.RemovePawn(p);
                LordMaker.MakeNewLord(enemy, new LordJob_ExitMapBest(LocomotionUrgency.Jog, true, true), map, up);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: the raiders go", e);
            }
        }
    }
}
