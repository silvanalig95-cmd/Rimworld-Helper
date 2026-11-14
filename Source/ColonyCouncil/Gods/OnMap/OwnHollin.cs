using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Hollin, the Rootmother, on the map (docs/gods-next-proposals.md, C 1): what she does that no other god does, in
    /// plants, trees, dryads and the floors of a colony that cuts too much.
    /// <list type="bullet">
    /// <item>The roots remember: a season of many trees felled and few planted (the Mossborn's ledger, MossLedger), and roots
    /// break up through a floor tile a morning, three mornings, in the room nearest the cleared ground; whoever finds it says so.</item>
    /// <item>The dryads' word (her lore: she speaks through her dryads a word at a time): with an anima or gauranlen tree and
    /// its dryads, and something real on the land to point at (trees marked to be cut, a frost coming on growing fields, a
    /// field left bare, blight), every dryad stops and turns to one colonist, one walks up to them, and they say they heard
    /// one word: "spare", "wait", "plant", "burn".</item>
    /// <item>The green walk: her rising (GodRising) rises twice more along a line across the land an hour apart, new grass and
    /// flowers springing up between, the wild beasts walking after her.</item>
    /// <item>The old tree spared: the colony's oldest tree, still standing in spring after a long while of felling round it,
    /// has a ring of wild healroot at its foot.</item>
    /// </list>
    /// </summary>
    public static class OwnHollin
    {
        public const string Key = "Hollin";
        public const string DryadThought = "Rimfolk_Thought_GodOwnDryadWord";

        private static God Her => OwnHappenings.God(Key);

        // ================= The roots remember =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = Her;
            if (g == null) return;
            var seat = Homes.Seat;
            if (b.rootsLeft <= 0 && seat != null && OwnHappenings.Acts(g, seat))
            {
                var why = OwnHappeningsMath.RootsTrigger(MossLedger.Felled, MossLedger.Planted);
                if (OwnHappenings.Due(b, OwnHappening.HollinRoots, g, seat, why, now)) Roots(b, b.felledMap != null && Homes.IsHome(b.felledMap) ? b.felledMap : seat, now);
            }
            foreach (var map in OwnHappenings.HomeMaps) OldTree(b, g, map, now);
        }

        /// <summary>The roots begin to remember: a tile a morning, from the next morning (now, for the dev action).</summary>
        internal static bool Roots(OwnHappeningsBook b, Map map, int now, bool atOnce = false)
        {
            if (map == null) return false;
            b.rootsMap = map;
            b.rootsNear = b.felledMap == map && b.felledAt.IsValid ? b.felledAt : CampSite.ColonyCenter(map);
            b.rootsLeft = OwnHappeningsMath.RootsMornings;
            b.rootsDay = atOnce ? -1 : GenLocalDate.DayOfYear(map);
            OwnHappenings.Came(b, OwnHappening.HollinRoots, now);
            return !atOnce || Break(b, Her, map, now);
        }

        /// <summary>The morning's tile: the floor broken up by roots in the room nearest the cleared ground, brambles through it.</summary>
        private static bool Break(OwnHappeningsBook b, God g, Map map, int now)
        {
            var cell = RootCell(map, b.rootsNear);
            b.rootsLeft--;
            b.rootsDay = GenLocalDate.DayOfYear(map);
            if (!cell.IsValid) return false;
            try
            {
                if (map.terrainGrid.CanRemoveTopLayerAt(cell)) map.terrainGrid.RemoveTopLayer(cell, false);
                var dirt = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Dirt");
                foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)).Where(c => c.InBounds(map) && c.Standable(map)).Take(3))
                    if (dirt != null) FilthMaker.TryMakeFilth(c, map, dirt);
                var bramble = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Brambles");
                if (bramble?.plant != null && cell.GetPlant(map) == null && cell.GetFirstItem(map) == null)
                {
                    var p = (Plant)ThingMaker.MakeThing(bramble);
                    p.Growth = Rand.Range(0.25f, 0.5f);
                    GenSpawn.Spawn(p, cell, map);
                }
                for (int i = 0; i < 6; i++) FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, Rand.Range(0.6f, 1.1f), new Color(0.36f, 0.27f, 0.18f, 0.7f));
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.12f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: roots through the floor", e);
                return false;
            }
            var room = cell.GetRoom(map);
            string place = room?.Role != null && room.Role != RoomRoleDefOf.None ? room.Role.label : "CC_GodO_Roots_Room".Translate().Resolve();
            bool first = b.rootsLeft == OwnHappeningsMath.RootsMornings - 1;
            if (first) OwnHappenings.Card(g, map, "Roots", false, new LookTargets(cell, map), null, Importance.Notable, place.Named("ROOM"));
            var seer = OwnHappenings.Found(b, map, cell, "roots", g, place);
            OwnHappenings.Saw(g, map, cell, GodActKind.Act, seer, first ? "CC_GodO_Saw_Roots" : "CC_GodO_Saw_RootsAgain", place.Named("ROOM"));
            return true;
        }

        /// <summary>A floor cell (else bare ground) in the colony's room nearest the cleared ground, clear of anything standing on it.</summary>
        private static IntVec3 RootCell(Map map, IntVec3 near)
        {
            if (!near.IsValid) near = CampSite.ColonyCenter(map);
            if (!near.IsValid) return IntVec3.Invalid;
            var rooms = map.regionGrid.AllRooms.Where(r => r != null && !r.PsychologicallyOutdoors && !r.IsHuge && !r.Fogged && r.CellCount >= 4 && map.areaManager.Home[r.Cells.First()])
                .OrderBy(r => r.Cells.Min(c => c.DistanceToSquared(near))).Take(3).ToList();
            foreach (var r in rooms)
            {
                var cells = r.Cells.Where(c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && c.GetFirstItem(map) == null && c.GetPlant(map) == null
                                              && c.DistanceTo(near) <= OwnHappeningsMath.RootsReach).OrderBy(c => c.DistanceToSquared(near)).ToList();
                var floor = cells.FirstOrDefault(c => map.terrainGrid.CanRemoveTopLayerAt(c));
                if (floor.IsValid && floor != default) return floor;
                if (cells.Count > 0) return cells[0];
            }
            return IntVec3.Invalid;
        }

        // ================= The hour: the roots' morning, the dryads =================

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            var g = Her;
            if (g == null) return;
            if (b.rootsLeft > 0)
            {
                var map = b.rootsMap;
                if (map == null || !Homes.IsHome(map)) b.rootsLeft = 0;
                else if (OwnHappenings.Hour(map) >= 5 && OwnHappenings.Hour(map) <= 8 && GenLocalDate.DayOfYear(map) != b.rootsDay && OwnHappenings.Quiet(map)) Break(b, g, map, now);
            }
            if (b.dryad != null || !OwnHappeningsMath.Free(OwnHappening.HollinDryads, OwnHappenings.DaysSince(b, OwnHappening.HollinDryads, now))) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                int h = OwnHappenings.Hour(map);
                if (h < 8 || h > 18 || !OwnHappenings.Quiet(map) || !OwnHappenings.Acts(g, map)) continue;
                var dryads = Dryads(map);
                if (dryads.Count == 0) continue;
                var words = Words(map, out bool anima, out _);
                if (OwnHappenings.Due(b, OwnHappening.HollinDryads, g, map, OwnHappeningsMath.DryadsTrigger(words.Count, dryads.Count, anima), now, 11f) && Word(b, g, map, now)) return;
            }
        }

        // ================= The dryads' word =================

        /// <summary>The dryads on the map (Ideology's and the player's mods', by the game's own mark), up and about.</summary>
        internal static List<Pawn> Dryads(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && !p.Downed && p.RaceProps?.Dryad == true && !p.InMentalState).ToList();

        private static ThingDef Tree(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

        /// <summary>The trees the dryads are of: anima trees first, then gauranlen trees.</summary>
        private static Thing HolyTree(Map map, out bool anima)
        {
            var a = GodOmens.AnimaTrees(map).FirstOrDefault();
            anima = a != null;
            if (a != null) return a;
            var gau = Tree("Plant_TreeGauranlen");
            return gau == null ? null : map.listerThings.ThingsOfDef(gau).FirstOrDefault(t => t.Spawned);
        }

        /// <summary>
        /// The words the dryads may give now (OwnHappeningsMath.Words), each with the place it points at: trees marked to be
        /// cut, a frost coming on growing fields, a field left bare in the growing season, blight.
        /// </summary>
        internal static List<(DryadWord word, IntVec3 at)> Words(Map map, out bool anima, out Thing tree)
        {
            var list = new List<(DryadWord, IntVec3)>();
            tree = HolyTree(map, out anima);
            if (tree == null) return list;
            var marked = map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.CutPlant).Where(d => d.target.Thing is Plant p && p.def.plant.IsTree).ToList();
            var zones = map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count >= 6).ToList();
            bool growing = zones.Any(z => z.Cells.Any(c => c.GetPlant(map) is Plant p && p.sown && p.Growth < 0.95f));
            float now = map.mapTemperature.OutdoorTemp;
            int next = ((int)GenLocalDate.Twelfth(map) + 1) % 12;
            float ahead = GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, (Twelfth)next);
            var season = GenLocalDate.Season(map);
            bool sowing = (season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer) && now >= 10f;
            var bare = sowing ? zones.FirstOrDefault(z => z.allowSow && z.Cells.Count(c => c.GetPlant(map) == null) >= z.Cells.Count * 0.7f) : null;
            var blight = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().Where(p => p.Spawned && p.Blighted).ToList();
            var words = OwnHappeningsMath.Words(marked.Count, OwnHappeningsMath.FrostAhead(now, ahead), growing, bare != null, blight.Count);
            foreach (var w in words)
            {
                IntVec3 at = w switch
                {
                    DryadWord.Spare => marked[0].target.Thing.Position,
                    DryadWord.Wait => zones.First().Cells[zones.First().Cells.Count / 2],
                    DryadWord.Plant => bare.Cells[bare.Cells.Count / 2],
                    _ => blight[0].Position
                };
                list.Add((w, at));
            }
            return list;
        }

        /// <summary>The dryads stop and turn to one colonist, and one walks up to them; the word is said when it gets there (Step).</summary>
        internal static bool Word(OwnHappeningsBook b, God g, Map map, int now)
        {
            var dryads = Dryads(map);
            var words = Words(map, out _, out var tree);
            if (dryads.Count == 0 || words.Count == 0 || tree == null) return false;
            var recent = GodPace.Forms("own" + OwnHappening.HollinDryads);
            int pick = GodPaceMath.Pick(words.Select(w => w.word.ToString()).ToList(), words.Select(_ => 1f).ToList(), recent, Rand.Value);
            if (pick < 0) return false;
            var (word, at) = words[pick];
            var to = map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map) && (p.CurJob == null || p.CurJob.def.casualInterruptible))
                .OrderBy(p => p.Position.DistanceToSquared(dryads[0].Position)).FirstOrDefault(p => dryads[0].CanReach(p, PathEndMode.Touch, Danger.Deadly));
            if (to == null) return false;
            var look = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.BeholdJob);
            var walker = dryads.OrderBy(d => d.Position.DistanceToSquared(to.Position)).First();
            foreach (var d in dryads.Where(d => d != walker))
            {
                if (look == null) break;
                var j = JobMaker.MakeJob(look, to);
                j.count = 1500;
                j.expiryInterval = 1700;
                OwnHappenings.Job(d, j);
            }
            var go = JobMaker.MakeJob(JobDefOf.Goto, CellFinder.StandableCellNear(to.Position, map, 1.5f));
            go.locomotionUrgency = LocomotionUrgency.Walk;
            go.expiryInterval = 1500;
            if (!OwnHappenings.Job(walker, go)) return false;
            OwnHappenings.Stand(to, walker.Position, 900);
            b.dryad = walker;
            b.dryadTo = to;
            b.dryadWord = (int)word;
            b.dryadPoint = at;
            b.dryadUntil = now + 1500;
            b.Due(now + 60);
            OwnHappenings.Came(b, OwnHappening.HollinDryads, now, word.ToString(), to.ThingID);
            return true;
        }

        /// <summary>The dryad's walk: beside the colonist, the word heard; too long, and it lets it be.</summary>
        private static void DryadStep(OwnHappeningsBook b, int now)
        {
            var d = b.dryad;
            var p = b.dryadTo;
            if (d == null) return;
            if (p == null || d.Dead || !d.Spawned || p.Dead || !p.Spawned || p.Map != d.Map || now > b.dryadUntil)
            {
                b.dryad = b.dryadTo = null;
                return;
            }
            if (!d.Position.InHorDistOf(p.Position, 2.2f))
            {
                b.Due(now + 60);
                return;
            }
            var map = d.Map;
            var g = Her;
            var word = (DryadWord)b.dryadWord;
            string said = ("CC_GodO_DryadWord_" + word).Translate().Resolve();
            b.dryad = b.dryadTo = null;
            OwnHappenings.Stand(d, p.Position, 600);
            OwnHappenings.Stand(p, d.Position, 300);
            OwnHappenings.Motes(map, d.DrawPos, OwnHappenings.GreenTwinkle, new Color(0.55f, 0.85f, 0.4f), 8);
            OwnHappenings.Say(p, "dryadword,word:" + word.ToString().ToLowerInvariant(), g, null, said.Named("WORD"));
            OwnHappenings.Memory(p, DryadThought);
            OwnHappenings.Journal(p, "CC_GodO_Journal_DryadWord", p.Named("PAWN"), said.Named("WORD"));
            var targets = new List<GlobalTargetInfo> { p };
            if (b.dryadPoint.IsValid) targets.Add(new GlobalTargetInfo(b.dryadPoint, map));
            OwnHappenings.Card(g, map, "Dryads", true, new LookTargets(targets), LetterDefOf.NeutralEvent, Importance.Notable, p.Named("PAWN"), said.Named("WORD"));
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Act, p, "CC_GodO_Saw_DryadWord", said.Named("WORD"));
        }

        // ================= The green walk =================

        /// <summary>Her rising has just stood (GodRising.Rise, with no body of its own): it may walk on along a line across the land.</summary>
        public static void Rose(Map map, God god, IntVec3 at)
        {
            var b = OwnHappenings.Book;
            if (b == null || god == null || god.Key != Key || map == null || !at.IsValid || b.walkLeft > 0) return;
            int now = OwnHappenings.Now;
            var season = GenLocalDate.Season(map);
            bool growing = (season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer) && map.mapTemperature.OutdoorTemp >= 5f;
            if (OwnHappenings.forced != OwnHappening.HollinWalk
                && (!OwnHappeningsMath.Free(OwnHappening.HollinWalk, OwnHappenings.DaysSince(b, OwnHappening.HollinWalk, now)) || !Rand.Chance(OwnHappeningsMath.WalkChance(growing, GodNear.Of(god, map)))))
                return;
            // Across the land, away from the colony's middle.
            var centre = CampSite.ColonyCenter(map);
            var away = centre.IsValid && centre != at ? (at - centre).ToVector3() : new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            float baseAngle = Mathf.Atan2(away.z, away.x) * Mathf.Rad2Deg;
            b.walkMap = map;
            b.walkAt = at;
            b.walkAngle = baseAngle + Rand.Range(-70f, 70f);
            b.walkLeft = OwnHappeningsMath.WalkSteps;
            b.walkNext = now + GenDate.TicksPerHour;
            b.Due(b.walkNext);
            OwnHappenings.Came(b, OwnHappening.HollinWalk, now);
        }

        /// <summary>The walk's next rising: further along her line, the green between, the beasts after her.</summary>
        private static void WalkStep(OwnHappeningsBook b, int now)
        {
            if (b.walkLeft <= 0) return;
            if (now < b.walkNext)
            {
                b.Due(b.walkNext);
                return;
            }
            var map = b.walkMap;
            var g = Her;
            if (map == null || g == null || !Homes.IsHome(map) || map.skyManager.CurSkyGlow < 0.4f || GodSky.On(map, SkySign.Rising))
            {
                if (map != null && GodSky.On(map, SkySign.Rising) && now < b.walkNext + GenDate.TicksPerHour)
                {
                    b.Due(now + 250);
                    return;
                }
                b.walkLeft = 0;
                return;
            }
            var next = NextPlace(map, b.walkAt, b.walkAngle);
            if (!next.IsValid)
            {
                b.walkLeft = 0;
                return;
            }
            int green = Green(map, b.walkAt, next);
            GodSky.Stay(map, SkySign.Rising, next, GenDate.TicksPerHour, GodRising.Height);
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.5f);
                foreach (var c in GenRadial.RadialCellsAround(next, 3f, true).Where(c => c.InBounds(map) && c.Standable(map)))
                    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(0.9f, 1.6f), new Color(0.38f, 0.3f, 0.2f, 0.7f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: her rising walking on", e);
            }
            int followed = Followers(map, next);
            bool first = b.walkLeft == OwnHappeningsMath.WalkSteps;
            b.walkAt = next;
            b.walkLeft--;
            b.walkNext = now + GenDate.TicksPerHour;
            if (b.walkLeft > 0) b.Due(b.walkNext);
            if (first) OwnHappenings.Card(g, map, followed > 0 ? "Walk" : "WalkAlone", false, new LookTargets(next, map));
            var seer = GodsOnMapWords.Seer(map, next, 45f);
            OwnHappenings.Say(seer, "walk", g);
            OwnHappenings.Saw(g, map, next, GodActKind.Appear, seer, followed > 0 ? "CC_GodO_Saw_Walk" : "CC_GodO_Saw_WalkAlone", green.Named("COUNT"));
            if (b.walkLeft == 0) GodMarks.Note(g, map, next, "CC_GodO_Walk_Act".Translate().Resolve(), PresenceKind.Appear, false);
        }

        /// <summary>The next place she rises: this far along her line (turning a little where the land won't have her), or none.</summary>
        private static IntVec3 NextPlace(Map map, IntVec3 from, float angle)
        {
            foreach (float turn in new[] { 0f, 25f, -25f, 50f, -50f })
            {
                float a = (angle + turn) * Mathf.Deg2Rad;
                float d = Rand.Range(OwnHappeningsMath.WalkSpacingMin, OwnHappeningsMath.WalkSpacingMax);
                var c = (from.ToVector3Shifted() + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d).ToIntVec3();
                if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetTerrain(map)?.IsWater != true && map.fertilityGrid.FertilityAt(c) > 0.3f
                    && !c.GetThingList(map).Any(t => t is Pawn || t is Building))
                    return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>New grass and wild flowers (the game's, and Alpha Biomes' flowers where it is loaded) spring up along her line between two risings. How many.</summary>
        private static int Green(Map map, IntVec3 from, IntVec3 to)
        {
            var kinds = new[] { "Plant_Grass", "Plant_Dandelion", "Plant_Daylily", "Plant_Astragalus", "AB_Flowers" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.plant != null).ToList();
            if (kinds.Count == 0) return 0;
            int n = 0;
            var a = from.ToVector3Shifted();
            var dir = to.ToVector3Shifted() - a;
            float len = dir.magnitude;
            if (len < 1f) return 0;
            dir /= len;
            for (float s = 2f; s < len - 1f; s += 0.8f)
            {
                var c = (a + dir * s).ToIntVec3();
                foreach (var cell in new[] { c, c + IntVec3.North, c + IntVec3.East })
                {
                    if (!cell.InBounds(map) || !Rand.Chance(OwnHappeningsMath.WalkGreen) || cell.Roofed(map) || !cell.Standable(map) || cell.GetPlant(map) != null
                        || cell.GetEdifice(map) != null || cell.GetFirstItem(map) != null || map.fertilityGrid.FertilityAt(cell) < 0.4f || cell.GetZone(map) != null)
                        continue;
                    try
                    {
                        var p = (Plant)ThingMaker.MakeThing(kinds.Count > 1 && n % 3 == 0 ? kinds[Rand.Range(1, kinds.Count)] : kinds[0]);
                        p.Growth = Rand.Range(0.35f, 0.8f);
                        GenSpawn.Spawn(p, cell, map);
                        n++;
                        if (Rand.Chance(0.3f)) OwnHappenings.Motes(map, cell.ToVector3Shifted(), OwnHappenings.GreenTwinkle, new Color(0.55f, 0.85f, 0.4f), 1, 0.3f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: grass along her walk", e);
                        return n;
                    }
                }
            }
            return n;
        }

        /// <summary>The wild beasts near her walk quietly after her to where she rises. How many.</summary>
        private static int Followers(Map map, IntVec3 to)
        {
            int n = 0;
            foreach (var a in map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps?.Animal == true && x.Faction == null && !x.Dead && !x.Downed && !x.InMentalState && !GodAvatar.Is(x)
                                                                     && x.Position.InHorDistOf(to, 40f) && (x.CurJob == null || x.CurJob.def.casualInterruptible))
                         .OrderBy(x => x.Position.DistanceToSquared(to)).Take(5))
            {
                var near = CellFinder.StandableCellNear(to, map, 6f);
                if (OwnHappenings.GoTo(a, near, 2000)) n++;
            }
            return n;
        }

        // ================= The old tree spared =================

        /// <summary>The colony's oldest tree near it, noted; still standing in spring after a long while of felling round it, a ring of healroot at its foot.</summary>
        private static void OldTree(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (b.oldTree != null && (b.oldTree.Destroyed || !b.oldTree.Spawned)) b.oldTree = null;
            if (b.oldTree == null)
            {
                if (map != Homes.Seat) return;
                b.oldTree = Oldest(map);
                b.oldTreeSince = now;
                b.felledSinceTree = 0;
                return;
            }
            if (b.oldTree.Map != map || !OwnHappenings.Acts(g, map)) return;
            bool spring = GenLocalDate.Season(map) == Season.Spring;
            float days = GodPace.DaysSince(b.oldTreeSince, now);
            if (OwnHappenings.forced != OwnHappening.HollinOldTree
                && (!OwnHappeningsMath.OldTreeSpared(days, b.felledSinceTree, true, spring) || !OwnHappeningsMath.Free(OwnHappening.HollinOldTree, OwnHappenings.DaysSince(b, OwnHappening.HollinOldTree, now))))
                return;
            Ring(b, g, map, b.oldTree, now);
        }

        /// <summary>The oldest tree within the colony's reach (not one of the holy trees), or null.</summary>
        internal static Thing Oldest(Map map)
        {
            var centre = CampSite.ColonyCenter(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(p => p.Spawned && p.def.plant.IsTree && !p.def.defName.Contains("Anima") && !p.def.defName.Contains("Gauranlen") && (!centre.IsValid || p.Position.InHorDistOf(centre, 45f)))
                .OrderByDescending(p => p.Age).FirstOrDefault();
        }

        /// <summary>The ring at its foot: wild healroot all round it, found and said; the tree noted afresh.</summary>
        internal static bool Ring(OwnHappeningsBook b, God g, Map map, Thing tree, int now)
        {
            var heal = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_HealrootWild");
            if (tree == null || heal?.plant == null) return false;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(tree.Position, 2.3f, false).Where(c => c.InBounds(map) && !c.InHorDistOf(tree.Position, 1.1f)).InRandomOrder())
            {
                if (n >= OwnHappeningsMath.HealrootRing) break;
                if (!c.Standable(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.Roofed(map)) continue;
                try
                {
                    var p = (Plant)ThingMaker.MakeThing(heal);
                    p.Growth = Rand.Range(0.4f, 0.7f);
                    GenSpawn.Spawn(p, c, map);
                    FleckMaker.ThrowDustPuff(c, map, 0.4f);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: healroot round the old tree", e);
                }
            }
            if (n == 0) return false;
            OwnHappenings.Came(b, OwnHappening.HollinOldTree, now);
            string tree0 = tree.def.label;
            OwnHappenings.Card(g, map, "OldTree", true, new LookTargets(tree), LetterDefOf.PositiveEvent, Importance.Notable, tree0.Named("TREE"));
            var seer = OwnHappenings.Found(b, map, tree.Position, "oldtree", g, tree0);
            OwnHappenings.Saw(g, map, tree.Position, GodActKind.Blessing, seer, "CC_GodO_Saw_OldTree", tree0.Named("TREE"));
            b.oldTreeSince = now;
            b.felledSinceTree = 0;
            return true;
        }

        // ================= The steps =================

        internal static void Step(OwnHappeningsBook b, int now)
        {
            DryadStep(b, now);
            WalkStep(b, now);
        }
    }
}
