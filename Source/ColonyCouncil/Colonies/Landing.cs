using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What comes along to a new map, worked out the same way for the window's words and for the move.</summary>
    public sealed class LandingPlan
    {
        public Map map;
        /// <summary>The colony's people, mechs and animals standing on the map.</summary>
        public readonly List<Pawn> pawns = new List<Pawn>();
        /// <summary>Those carried by one of them (a baby, someone downed).</summary>
        public readonly List<Pawn> carried = new List<Pawn>();
        /// <summary>The starting kit lying on the map: the thing, how much of it comes, the piece it is.</summary>
        public readonly List<(Thing thing, int count, LandingPiece piece)> loose = new List<(Thing, int, LandingPiece)>();
        /// <summary>The starting kit held by someone who comes along (worn, wielded, in an inventory), with the piece it is.</summary>
        public readonly List<(Thing thing, LandingPiece piece)> held = new List<(Thing, LandingPiece)>();

        public IEnumerable<Pawn> Everyone => pawns.Concat(carried);
    }

    /// <summary>
    /// Choosing another landing site in the first days of a new game (the player's request of 6 November 2026; DESIGN.md,
    /// "Another landing site"): a new map with the same colonists, their animals, what is left of the scenario's starting
    /// kit, the same ideology, world, storyteller, research and Rimfolk records. Only the map changes. Offered from the
    /// game's own menu (Colonies/LandingPatches.cs) and the colony's card, in a window of its own
    /// (Colonies/LandingWindow.cs); the rules are in <see cref="LandingMath"/>, the record of the start in
    /// <see cref="LandingRecorder"/>. The same settlement is kept, moved on the world map for another place, so every
    /// record keyed by it stays with the colony; nothing here is a founding, an abandoning or a journey.
    /// </summary>
    public static class Landing
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static LandingRecord Record => CouncilGameComponent.Instance?.landing;

        /// <summary>The first days are over: the record is let go.</summary>
        public static void Expire(CouncilGameComponent comp)
        {
            if (comp?.landing != null && !LandingMath.Open(Now, comp.landing.landedAt)) comp.landing = null;
        }

        /// <summary>The home the colony landed on, while it is a home.</summary>
        public static Map Home
        {
            get
            {
                var r = Record;
                return r == null ? null : Homes.MapOf(r.home);
            }
        }

        /// <summary>The entry shows (greyed with why, or not): in a game whose start Rimfolk saw, within its first days.</summary>
        public static bool Shows
        {
            get
            {
                if (Current.ProgramState != ProgramState.Playing || Current.Game == null) return false;
                var comp = CouncilGameComponent.Instance;
                Expire(comp);
                var r = comp?.landing;
                return LandingMath.Shows(RimfolkPatches.On, r != null, Now, r?.landedAt ?? -1, TutorSystem.TutorialMode);
            }
        }

        /// <summary>On a colony's card: the home the colony landed on, while the entry shows.</summary>
        public static bool ShowsFor(Map map) => map != null && Shows && Homes.KeyOf(map) == Record?.home;

        /// <summary>The entry's words: a landing site for a start by drop pods or gravship, a starting site for a standing one.</summary>
        public static string Label => (Record == null || Record.method != PlayerPawnsArriveMethod.Standing ? "CC_Landing_Menu" : "CC_Landing_MenuStanding").Translate().Resolve();

        public static string Tip => "CC_Landing_MenuTip".Translate(LandingMath.OfferDays.Named("DAYS")).Resolve();

        // ================= When it is offered =================

        private static float whyAt = -10f;
        private static string whyText;

        /// <summary>
        /// Why it isn't offered now, in words, or null. Drawn every frame, so the words are kept a second; <paramref name="exact"/>
        /// looks again now, with the game's own threat check, as the move does.
        /// </summary>
        public static string WhyNot(bool exact = false)
        {
            if (!exact && Time.realtimeSinceStartup - whyAt < 1f) return whyText;
            string text = null;
            try
            {
                var why = Why(exact, out string names);
                if (why != LandingWhy.None)
                    text = ("CC_Landing_No_" + why).Translate(names.Named("NAMES"), LandingMath.OfferDays.Named("DAYS")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: why not", e);
                text = "CC_Landing_No_Homes".Translate().Resolve();
            }
            if (!exact)
            {
                whyAt = Time.realtimeSinceStartup;
                whyText = text;
            }
            return text;
        }

        public static LandingWhy Why(bool exact, out string names)
        {
            names = "";
            var r = Record;
            var map = Home;
            bool open = r != null && LandingMath.Open(Now, r.landedAt);
            bool gravship = r != null && r.method == PlayerPawnsArriveMethod.Gravship;
            bool space = map != null && (map.Tile.LayerDef?.isSpace ?? false);
            bool monolith = ModsConfig.AnomalyActive && Find.Anomaly != null && Find.Anomaly.Level > 0;
            var away = map != null ? Away(map) : new List<Pawn>();
            bool borrowed = false;
            try
            {
                borrowed = QuestUtility.TotalBorrowedColonistCount() > 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: colonists lent out", e);
            }
            var inside = map != null ? Inside(map) : new List<Pawn>();
            bool threat = map != null && (exact ? GenHostility.AnyHostileActiveThreatToPlayer(map) : MapFacts.ThreatOn(map));
            var why = LandingMath.WhyNot(RimfolkPatches.On, r != null, open, TutorSystem.TutorialMode, gravship, space, Homes.All.Count, map != null, monolith,
                away.Count > 0 || borrowed, inside.Count > 0, threat);
            if (why == LandingWhy.Away) names = away.Count > 0 ? Foundings.Names(away) : "CC_Landing_Borrowed".Translate().Resolve();
            else if (why == LandingWhy.Inside) names = Foundings.Names(inside);
            return why;
        }

        /// <summary>Who a pawn is to the colony.</summary>
        public static LandingSide SideOf(Pawn p)
        {
            if (p == null || p.Dead || p.RaceProps == null) return LandingSide.Other;
            return LandingMath.SideOf(p.Faction == Faction.OfPlayer, p.IsPrisonerOfColony, p.IsSlaveOfColony, p.RaceProps.Humanlike, p.RaceProps.IsMechanoid, p.RaceProps.Animal);
        }

        private static bool Comes(Pawn p) => LandingMath.Comes(SideOf(p));

        /// <summary>Those of the colony away from the map: in a caravan, on another map, in pods in flight, where other mods keep them.</summary>
        private static List<Pawn> Away(Map map)
        {
            var list = new List<Pawn>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
                if (p != null && p.MapHeld != map && !list.Contains(p))
                    list.Add(p);
            foreach (var p in Homes.FreeColonistsAnywhere())
                if (p != null && p.MapHeld != map && !list.Contains(p))
                    list.Add(p);
            return list;
        }

        /// <summary>Those of the colony on the map inside something (a casket, a pod, a vat), or carried by someone who isn't coming.</summary>
        private static List<Pawn> Inside(Map map)
        {
            var list = new List<Pawn>();
            foreach (var p in map.mapPawns.AllPawnsUnspawned)
            {
                if (p == null || p.Dead || !Comes(p)) continue;
                if (p.ParentHolder is Pawn_CarryTracker ct && ct.pawn != null && ct.pawn.Spawned && Comes(ct.pawn)) continue;
                list.Add(p);
            }
            return list;
        }

        /// <summary>Whether another place can be picked: not when the scenario chose the place itself.</summary>
        public static bool PlaceForced => Find.Scenario?.AllParts.Any(p => p is ScenPart_ForcedMap) ?? false;

        /// <summary>
        /// Why a tile won't do for the new map, or null: the game's own check for a new settlement, except that the colony's
        /// own home, which moves, doesn't count as a neighbour; nothing else on the world map may stand there.
        /// </summary>
        public static string TileWhyNot(PlanetTile tile, Map home)
        {
            if (!tile.Valid) return "CC_Found_No_Tile".Translate();
            var ours = home?.Parent;
            if (ours != null && tile == ours.Tile) return "CC_Landing_Pick_Here".Translate();
            if (ours != null && tile.Layer != ours.Tile.Layer) return "CC_Landing_Pick_Layer".Translate();
            if (Find.WorldObjects.AnyWorldObjectAt(tile)) return "CC_Found_No_Taken".Translate();
            var sb = new StringBuilder();
            if (TileFinder.IsValidTileForNewSettlement(tile, sb)) return null;
            string why = sb.ToString();
            if (ours != null && why == "FactionBaseAdjacent".Translate().ToString() && OnlyOursNear(tile, ours)) return null;
            return why.NullOrEmpty() ? "CC_Found_No_Tile".Translate().ToString() : why;
        }

        private static readonly List<PlanetTile> near = new List<PlanetTile>();

        private static bool OnlyOursNear(PlanetTile tile, WorldObject ours)
        {
            near.Clear();
            Find.WorldGrid.GetTileNeighbors(tile, near);
            foreach (var t in near)
            {
                var s = Find.WorldObjects.SettlementBaseAt(t);
                if (s != null && s != ours) return false;
            }
            return true;
        }

        // ================= What comes along =================

        private static Pawn HolderPawn(Thing t)
        {
            for (var h = t?.ParentHolder; h != null; h = h.ParentHolder)
            {
                switch (h)
                {
                    case Pawn p: return p;
                    case Pawn_InventoryTracker inv: return inv.pawn;
                    case Pawn_ApparelTracker app: return app.pawn;
                    case Pawn_EquipmentTracker eq: return eq.pawn;
                    case Pawn_CarryTracker carry: return carry.pawn;
                    case Map _: return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Who and what comes along from this map: every colonist, prisoner, slave, mech and animal of the colony with what
        /// they wear and carry, and what is left of the starting kit, never more of it than the kit had
        /// (<see cref="LandingMath.Take"/>).
        /// </summary>
        public static LandingPlan Plan(Map map)
        {
            var plan = new LandingPlan { map = map };
            if (map == null) return plan;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (!Comes(p)) continue;
                plan.pawns.Add(p);
                if (p.carryTracker?.CarriedThing is Pawn c && Comes(c)) plan.carried.Add(c);
            }
            var r = Record;
            if (r == null) return plan;
            r.Prune();
            var comers = new HashSet<Pawn>(plan.Everyone);
            var totals = r.Totals();
            foreach (var group in r.kit.Where(p => p?.def != null).GroupBy(p => p.Group))
            {
                var pieces = group.Where(p => p.thing != null && !p.thing.Destroyed && p.thing.MapHeld == map).ToList();
                if (pieces.Count == 0) continue;
                var have = new int[pieces.Count];
                var held = new bool[pieces.Count];
                for (int i = 0; i < pieces.Count; i++)
                {
                    var t = pieces[i].thing;
                    held[i] = !t.Spawned && HolderPawn(t) is Pawn h && comers.Contains(h);
                    // Lying on the map, or held by someone who comes along; not what lies in a grave, a crate or a stranger's hands.
                    have[i] = t.Spawned || held[i] ? t.stackCount : 0;
                }
                totals.TryGetValue(group.Key, out int total);
                var take = LandingMath.Take(have, held, total);
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (held[i]) plan.held.Add((pieces[i].thing, pieces[i]));
                    else if (take[i] > 0) plan.loose.Add((pieces[i].thing, take[i], pieces[i]));
                }
            }
            return plan;
        }

        /// <summary>The starting kit lying on the map that comes along, in words: "450 steel, 30 medicine".</summary>
        public static string KitWords(LandingPlan plan)
        {
            var sums = new Dictionary<(ThingDef, ThingDef), int>();
            foreach (var (thing, count, _) in plan.loose)
            {
                var key = (LandingPiece.DefOf(thing), LandingPiece.StuffOf(thing));
                if (key.Item1 == null) continue;
                sums.TryGetValue(key, out int n);
                sums[key] = n + count;
            }
            return string.Join(", ", sums.OrderByDescending(kv => kv.Value).Select(kv => GenLabel.ThingLabel(kv.Key.Item1, kv.Key.Item2, kv.Value)));
        }

        // ================= The move =================

        /// <summary>
        /// The player's word in the window: a new map of the same place (<paramref name="to"/> invalid) or of another, at
        /// <paramref name="size"/>. Checked again now, then done in one long event.
        /// </summary>
        public static void Begin(PlanetTile to, int size)
        {
            string why = WhyNot(true);
            var map = Home;
            if (why == null && map == null) why = "CC_Landing_No_Homes".Translate().Resolve();
            if (why == null && to.Valid && to != map.Tile) why = PlaceForced ? "CC_Landing_Forced".Translate().ToString() : TileWhyNot(to, map);
            if (why != null)
            {
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var tile = to.Valid && to != map.Tile ? to : PlanetTile.Invalid;
            Find.Selector.ClearSelection();
            Find.WorldSelector.ClearSelection();
            LongEventHandler.QueueLongEvent(() => Move(map, tile, size), "CC_Landing_Long", false, null);
        }

        /// <summary>The turn given to the map's seed while a new map of the same place is made (Colonies/LandingPatches.cs), else zero.</summary>
        public static int SeedTurn;

        private static void Move(Map map, PlanetTile to, int size)
        {
            var r = Record;
            var settlement = map?.Parent as Settlement;
            if (r == null || settlement == null || !Homes.IsHome(map)) return;
            bool same = !to.Valid;
            var from = settlement.Tile;
            var plan = Plan(map);
            var people = new List<Pawn>();
            var inHand = new List<Thing>();
            var kit = new List<(Thing thing, LandingPiece piece, int count)>();
            var heldKit = plan.held.ToList();

            // 1. Off the old map, as the game moves someone through a portal: their lord told, the claims on this map's beds
            // given up, despawned with all they wear and carry. What they carry comes along beside them.
            foreach (var p in plan.pawns)
            {
                try
                {
                    var c = p.carryTracker?.CarriedThing;
                    if (c != null && p.carryTracker.innerContainer.Remove(c))
                    {
                        if (c is Pawn cp && plan.carried.Contains(cp)) people.Add(cp);
                        else inHand.Add(c);
                    }
                    p.GetLord()?.Notify_PawnLost(p, PawnLostCondition.ExitedMap);
                    if (p.drafter != null && p.Drafted) p.drafter.Drafted = false;
                    p.mindState?.priorityWork?.ClearPrioritizedWorkAndJobQueue();
                    p.DeSpawnOrDeselect();
                    p.ownership?.UnclaimAll();
                    people.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("landing: off the old map", e);
                    if (!p.Spawned && !people.Contains(p)) people.Add(p);
                }
            }
            foreach (var cp in people.Where(x => plan.carried.Contains(x)).ToList()) cp.ownership?.UnclaimAll();
            // The kit lying on the map: taken whole, or the part of a stack that the kit had.
            foreach (var (thing, count, piece) in plan.loose)
            {
                try
                {
                    if (thing.Destroyed || !thing.Spawned) continue;
                    var taken = count >= thing.stackCount ? thing : thing.SplitOff(count);
                    if (taken.Spawned) taken.DeSpawnOrDeselect();
                    kit.Add((taken, piece, taken.stackCount));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("landing: the kit off the old map", e);
                }
            }
            // The map conditions the scenario set, as they stand now.
            var conditions = new List<(GameConditionDef def, GameCondition now)>();
            foreach (var def in r.conditions) conditions.Add((def, map.gameConditionManager.ActiveConditions.FirstOrDefault(c => c.def == def)));
            var held = conditions.Select(c => (c.def, had: c.now != null, permanent: c.now?.Permanent ?? false, start: c.now?.startTick ?? 0,
                duration: c.now != null && !c.now.Permanent ? c.now.Duration : 0)).ToList();

            // 2. The old map goes, its settlement kept: its name, its founding day and every record keyed by it.
            Current.Game.DeinitAndRemoveMap(map, false);
            foreach (var p in people) p.playerSettings?.Notify_MapRemoved(map);

            // 3. Another place: the same settlement moved on the world map.
            if (!same) MoveSettlement(settlement, to);

            // 4. The new map, the game's way for a player home (the generator the start had); the same place's with its seed turned.
            Map made = null;
            var mapSize = new IntVec3(size, 1, size);
            try
            {
                SeedTurn = same ? NonZero(Rand.Int) : 0;
                made = MapGenerator.GenerateMap(mapSize, settlement, r.generator ?? settlement.MapGeneratorDef, settlement.ExtraGenStepDefs);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: making the new map", e);
                made = null;
            }
            finally
            {
                SeedTurn = 0;
            }
            bool failed = made == null;
            if (failed)
            {
                // Back to where it was, with the game's own map of it.
                var broken = Current.Game.FindMap(settlement);
                if (broken != null) Current.Game.DeinitAndRemoveMap(broken, false);
                if (!same) MoveSettlement(settlement, from);
                made = MapGenerator.GenerateMap(mapSize, settlement, r.generator ?? settlement.MapGeneratorDef, settlement.ExtraGenStepDefs);
            }
            var start = MapGenerator.PlayerStartSpotValid && MapGenerator.PlayerStartSpot.InBounds(made) ? MapGenerator.PlayerStartSpot : StartNear(made);

            // 5. The scenario's map conditions as they stood: the scenario sets them again on a new map, so they are put back to
            // the time they had left, or ended if they had ended; none is added twice.
            foreach (var c in held)
            {
                try
                {
                    var now = made.gameConditionManager.ActiveConditions.FirstOrDefault(x => x.def == c.def);
                    if (!c.had)
                    {
                        if (now != null)
                        {
                            now.suppressEndMessage = true;
                            now.End();
                        }
                        continue;
                    }
                    if (now == null)
                    {
                        now = c.permanent ? GameConditionMaker.MakeConditionPermanent(c.def) : GameConditionMaker.MakeCondition(c.def, c.duration);
                        made.gameConditionManager.RegisterCondition(now);
                    }
                    if (!c.permanent)
                    {
                        now.startTick = c.start;
                        now.Duration = c.duration;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("landing: the scenario's map conditions", e);
                }
            }

            // 6. They arrive the scenario's way at the new start: each person in a drop pod of their own (or standing there),
            // the mechs, animals and the kit that came with them shared between the pods.
            var groups = new List<List<Thing>>();
            var shared = new List<Thing>();
            foreach (var p in people)
            {
                if (LandingMath.OwnPod(SideOf(p))) groups.Add(new List<Thing> { p });
                else shared.Add(p);
            }
            shared.AddRange(kit.Where(k => LandingMath.PlaceOf(k.piece.kind) == LandingKind.Pods).Select(k => k.thing));
            shared.AddRange(inHand);
            if (groups.Count == 0) groups.Add(new List<Thing>());
            for (int i = 0; i < shared.Count; i++) groups[i % groups.Count].Add(shared[i]);
            groups.RemoveAll(g => g.Count == 0);
            try
            {
                DropPodUtility.DropThingGroupsNear(start, made, groups, LandingMath.PodOpenDelay, !r.ByPods, true, true, true, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the arrival", e);
            }
            // Anyone the pods didn't take stands at the start (never left between maps).
            foreach (var t in groups.SelectMany(g => g))
                if (!t.Destroyed && !t.Spawned && t.holdingOwner == null)
                    GenPlace.TryPlaceThing(t, CellFinder.RandomClosewalkCellNear(start, made, 6), made, ThingPlaceMode.Near);
            foreach (var p in people)
                if (p.IsPrisonerOfColony)
                    p.guest?.WaitInsteadOfEscapingForDefaultTicks();
            // What the scenario scattered: near the new start, or anywhere, as the game's scatter does.
            foreach (var (thing, piece, _) in kit)
            {
                var place = LandingMath.PlaceOf(piece.kind);
                if (place == LandingKind.Pods) continue;
                try
                {
                    Scatter(thing, made, start, place == LandingKind.Near);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("landing: the scattered kit", e);
                }
                if (!thing.Destroyed && !thing.Spawned && thing.holdingOwner == null) GenPlace.TryPlaceThing(thing, start, made, ThingPlaceMode.Near);
            }

            // 7. No game over while they were between maps.
            try
            {
                Find.GameEnder.CheckOrUpdateGameOver();
                if (Find.GameEnder.gameEnding)
                {
                    Log.Warning("[Rimfolk] After choosing another landing site the game thought the colony was gone; it isn't, so the game over is called off.");
                    Find.GameEnder.gameEnding = false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the game over check", e);
            }

            // 8. The record: the new landing, and the kit as it came.
            try
            {
                Rerecord(r, made, start, kit, heldKit);
                var ledger = WorldLedger.ColonyRecord(Homes.KeyOf(made));
                if (ledger != null) ledger.tile = made.Tile.tileId;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the record", e);
            }
            Homes.Clear();
            MapFacts.Clear();
            LensModel.Dirty();
            Cards.Dirty();
            whyAt = -10f;
            Current.Game.CurrentMap = made;
            Find.ColonistBar.MarkColonistsDirty();

            // 9. The camera at the new start, and a short message.
            string text = failed ? "CC_Landing_Failed".Translate(Homes.Name(made).Named("NAME")).Resolve()
                : ("CC_Landing_Done_" + Ages.Tag(Ages.Of(made))).Translate(Homes.Name(made).Named("NAME"), (made.Biome?.label ?? "?").Named("LAND")).Resolve();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    Current.Game.CurrentMap = made;
                    CameraJumper.TryHideWorld();
                    Find.CameraDriver.JumpToCurrentMapLoc(start);
                    Tell.Message(NewsKind.Colony, Importance.Important, made, text, new LookTargets(new TargetInfo(start, made)),
                        failed ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("landing: the camera at the new start", e);
                }
            });
        }

        private static int NonZero(int n) => n == 0 ? 1 : n;

        /// <summary>The same settlement moved on the world map: the game's tile setter redraws it and its tile's lookups; its cached drawn place is let go.</summary>
        private static void MoveSettlement(Settlement settlement, PlanetTile to)
        {
            settlement.Tile = to;
            try
            {
                AccessTools.Field(typeof(WorldObject), "drawPosCacheTick")?.SetValue(settlement, -1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the settlement's place on the world map", e);
            }
            // The void monolith comes as at a game's start (ScenPart_MonolithGeneration checks the starting tile).
            Find.GameInfo.startingTile = to;
        }

        /// <summary>A start spot when the generator gave none: open ground near the middle.</summary>
        private static IntVec3 StartNear(Map map)
        {
            if (RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map), map, out var c)) return c;
            return CellFinder.RandomClosewalkCellNear(map.Center, map, 20);
        }

        /// <summary>A piece the scenario scattered: near the start (within the game's 20 cells) or anywhere, forbidden as the scatter left it.</summary>
        private static void Scatter(Thing thing, Map map, IntVec3 start, bool nearStart)
        {
            if (thing.def.category == ThingCategory.Building)
            {
                // Only what was scattered anywhere is a building (a ship chunk): anywhere it can stand, fogged or not, as the scatter allowed.
                var rot = thing.def.rotatable ? thing.Rotation : Rot4.North;
                if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(5, c => GenSpawn.CanSpawnAt(thing.def, c, map, rot, false), map, out var at))
                    at = CellFinder.RandomClosewalkCellNear(start, map, LandingMath.NearRadius);
                GenSpawn.Spawn(thing, at, map, rot, WipeMode.VanishOrMoveAside);
                thing.SetForbidden(true, false);
                return;
            }
            thing.SetForbidden(true, false);
            IntVec3 cell;
            if (nearStart) cell = CellFinder.RandomClosewalkCellNear(start, map, LandingMath.NearRadius, c => c.Standable(map));
            else if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(5, c => c.Standable(map), map, out cell)) cell = start;
            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// The record after a landing: when and where, and the kit as it came (each piece its own count, never more than
        /// came; a piece that merged into another on landing keeps its count in the group).
        /// </summary>
        private static void Rerecord(LandingRecord r, Map made, IntVec3 start, List<(Thing thing, LandingPiece piece, int count)> kit, List<(Thing thing, LandingPiece piece)> heldKit)
        {
            r.landedAt = Now;
            r.start = start;
            r.landings++;
            r.home = Homes.KeyOf(made);
            var pieces = new List<LandingPiece>();
            foreach (var (thing, piece, count) in kit)
                pieces.Add(new LandingPiece { thing = thing.Destroyed ? null : thing, def = piece.def, stuff = piece.stuff, count = count, kind = piece.kind });
            foreach (var (thing, piece) in heldKit)
                pieces.Add(new LandingPiece { thing = thing.Destroyed ? null : thing, def = piece.def, stuff = piece.stuff, count = thing.stackCount, kind = piece.kind });
            r.kit = pieces;
        }
    }
}
