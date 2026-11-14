using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Great herds and migrations (the player's pick "alphas and bosses", 7 November 2026). In spring and in fall a great
    /// herd may cross a home's land (six times in ten a season at the setting's 100%, on a day of the season's first
    /// twelve): six to twenty-four head of one herd animal (the land's own three times likelier than one from beyond it,
    /// the bigger likelier: the base game's, Vanilla Animals Expanded's, Megafauna's and Dinosauria's grazers, any that
    /// live in herds and migrate), walking in from one edge and out at the farthest, as the game's own herd migration
    /// does, with its letter. Its hunting season is the council's question about a herd crossing (take a few, the
    /// biggest, or let them cross), asked when it can be. Every herd crossing a home's land (the game's own migration too,
    /// four head or more) can be spooked into a stampede by what really happens near it: one of it hurt (a shot, an arrow,
    /// a bite), a shot flying past, a fire close by. The herd then sprints for the edge away from it, and for the first
    /// hour a big runner (a muffalo's size or more) can knock down someone smaller in its way (a blunt blow, by the
    /// everyday hurts' scale and their shared cap, three in a stampede at most) and flatten a few plants of the fields
    /// it runs through; each is shown where it happens. While a herd of six or more passes, up to three of the colony in
    /// sight of it and at nothing pressing stop to watch it go by. The herds on the map are read each time, nothing saved.
    /// </summary>
    public static class GreatHerds
    {
        public const string PassesPool = "Rimfolk_Pool_HerdPasses";
        public const string HuntingSeason = "Rimfolk_Evt_HerdCrossing";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && BeastsMath.Rate(S.greatHerds) > 0f && Current.Game != null;

        private const int Day = BeastsMath.DayTicks;

        /// <summary>A herd crossing a map: its group, its kind, whether it is one of ours, how it has gone so far.</summary>
        private sealed class HerdWatch
        {
            public Lord lord;
            public PawnKindDef kind;
            public bool great, watched, stampede, fireRolled;
            public int stampedeAt = -1, watchTry = -1, trampled, crops;
            public readonly Dictionary<Pawn, IntVec3> last = new Dictionary<Pawn, IntVec3>();
            public readonly HashSet<Pawn> passed = new HashSet<Pawn>();
            /// <summary>The herd as it was at the last look (one of it shot dead since spooks the rest).</summary>
            public readonly HashSet<Pawn> members = new HashSet<Pawn>();
            public bool cropsTold;
        }

        private static readonly Dictionary<int, HerdWatch> watches = new Dictionary<int, HerdWatch>();
        private static readonly HashSet<int> greatLords = new HashSet<int>();

        /// <summary>A new game or a load: the herds on the map are read again.</summary>
        public static void Reset()
        {
            watches.Clear();
            greatLords.Clear();
        }

        // ---------------- Which animals ----------------

        /// <summary>The land's own wild animals, read safely (Animals/BiomeAnimals.cs: a biome listing an animal twice breaks the game's lookup).</summary>
        public static HashSet<PawnKindDef> LandAnimals(Map map)
        {
            var set = new HashSet<PawnKindDef>();
            if (map == null) return set;
            try
            {
                foreach (var b in map.Biomes)
                    foreach (var k in BiomeAnimals.Of(b))
                        if (k != null && BiomeAnimals.LivesIn(b, k)) set.Add(k);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beasts: the land's animals", e);
            }
            return set;
        }

        public static bool HerdKind(PawnKindDef k) =>
            k?.race?.race != null && BeastsMath.HerdKind(k.RaceProps.Animal, k.RaceProps.herdAnimal, k.RaceProps.predator, k.RaceProps.Insect, k.RaceProps.Dryad,
                k.RaceProps.CanDoHerdMigration, k.RaceProps.baseBodySize) && !k.RaceProps.IsAnomalyEntity;

        /// <summary>A herd animal for this map now: fit for the season's weather, a Pokémon only as PokéWorld lets one roam wild.</summary>
        public static PawnKindDef PickKind(Map map)
        {
            var land = LandAnimals(map);
            var pool = new List<PawnKindDef>();
            foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                if (!HerdKind(k)) continue;
                if (PokeWorldLink.IsPokemon(k) && !PokeWorldLink.Allows(k)) continue;
                if (!map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race)) continue;
                pool.Add(k);
            }
            return pool.RandomElementByWeightWithFallback(k => BeastsMath.HerdKindWeight(k.RaceProps.baseBodySize, land.Contains(k)));
        }

        // ---------------- The day: the season's great herd ----------------

        private static int SeasonIndex(Season s)
        {
            switch (s)
            {
                case Season.Spring: return 0;
                case Season.Summer:
                case Season.PermanentSummer: return 1;
                case Season.Fall: return 2;
                default: return 3;
            }
        }

        /// <summary>The day: a new season decides its great herd (spring and fall, six times in ten at 100%), and its day comes.</summary>
        public static void Daily(BeastBook book, Map map, int now)
        {
            if (!On || map == null) return;
            var bh = book.HomeOf(map);
            if (bh == null) return;
            int season = SeasonIndex(GenLocalDate.Season(map));
            int key = GenLocalDate.Year(map) * 4 + season;
            if (bh.herdSeason != key)
            {
                bh.herdSeason = key;
                bh.herdAt = -1;
                if (BeastsMath.Migrates(season) && Rand.Chance(BeastsMath.Chance(S.greatHerds, BeastsMath.SeasonChance)))
                {
                    int dayOf = GenLocalDate.DayOfSeason(map);
                    int wait = Mathf.Max(0, BeastsMath.PlannedDay(Rand.Value) - dayOf);
                    bh.herdAt = now + wait * Day;
                }
            }
            if (bh.herdAt < 0 || now < bh.herdAt) return;
            if (MapFacts.ThreatOn(map))
            {
                bh.herdAt = now + Day;
                return;
            }
            bh.herdAt = -1;
            Start(map);
        }

        /// <summary>The great herd walks in at one edge and makes for the farthest (the game's own migration's way, bigger), with its letter.</summary>
        public static List<Pawn> Start(Map map, PawnKindDef kind = null)
        {
            kind ??= PickKind(map);
            if (kind == null || !Edges(map, out var start, out var end)) return null;
            int n = BeastsMath.GreatCount(kind.RaceProps.baseBodySize, Rand.Value);
            var list = new List<Pawn>();
            try
            {
                var rot = Rot4.FromAngleFlat((map.Center - start).AngleFlat);
                for (int i = 0; i < n; i++)
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile));
                    GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(start, map, 10), map, rot);
                    list.Add(p);
                }
                var lord = LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(end, LocomotionUrgency.Walk, 12f, false, false), map, list);
                greatLords.Add(lord.loadID);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beasts: a great herd", e);
                return list;
            }
            string kinds = kind.GetLabelPlural();
            bool spring = SeasonIndex(GenLocalDate.Season(map)) == 0;
            string text = (spring ? "CC_Herd_TextSpring" : "CC_Herd_TextFall").Translate(list.Count.Named("COUNT"), kinds.Named("KIND")).Resolve();
            Tell.Letter(NewsKind.Land, Importance.Notable, map, "CC_Herd_Title".Translate(kinds.Named("KIND")).Resolve(), text, LetterDefOf.NeutralEvent, new LookTargets(list.First()));
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_Herd_Chronicle".Translate(list.Count.Named("COUNT"), kinds.Named("KIND")).Resolve()));
            AskHunt(map);
            return list;
        }

        /// <summary>The way in and out, as the game's migration finds them: an entry cell, and the reachable edge farthest from it.</summary>
        private static bool Edges(Map map, out IntVec3 start, out IntVec3 end)
        {
            end = IntVec3.Invalid;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out start, map, CellFinder.EdgeRoadChance_Animal)) return false;
            var from = start;
            for (int i = 0; i < 8; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReach(from, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.NoPassClosedDoors).WithFenceblocked(true)),
                        map, CellFinder.EdgeRoadChance_Ignore, out var c2)) break;
                if (!end.IsValid || c2.DistanceToSquared(start) > end.DistanceToSquared(start)) end = c2;
            }
            return end.IsValid;
        }

        /// <summary>The hunting season: the council's question about the herd crossing, when it can come now (a hunter at home, no threat, not asked lately).</summary>
        private static void AskHunt(Map map)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(HuntingSeason);
            if (def == null) return;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                if (def.Worker.CanFireNow(parms)) Find.Storyteller.TryFire(new FiringIncident(def, null, parms), false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beasts: the hunting season", e);
            }
        }

        // ---------------- The short pass: herds on the land ----------------

        /// <summary>Every 250 ticks, each home map's herds crossing for an edge: spooked into a stampede, its runners' way, those who stop to watch.</summary>
        public static void Rare(BeastBook book, int now)
        {
            if (!On)
            {
                if (watches.Count > 0) watches.Clear();
                return;
            }
            var seen = new HashSet<int>();
            foreach (var map in Homes.All)
            {
                if (map?.lordManager == null) continue;
                foreach (var lord in map.lordManager.lords.ToList())
                {
                    if (!(lord?.LordJob is LordJob_ExitMapNear) || lord.ownedPawns.Count < BeastsMath.MinHerd) continue;
                    var herd = Herd(lord);
                    if (herd.Count < BeastsMath.MinHerd) continue;
                    seen.Add(lord.loadID);
                    if (!watches.TryGetValue(lord.loadID, out var w))
                        watches[lord.loadID] = w = new HerdWatch { lord = lord, kind = herd[0].kindDef, great = greatLords.Contains(lord.loadID) };
                    if (w.stampede)
                    {
                        Trample(w, map, herd, now);
                        continue;
                    }
                    var cause = Spooked(w, map, herd, now, out var from);
                    w.members.Clear();
                    w.members.UnionWith(herd);
                    if (cause != null && Rand.Chance(BeastsMath.Clamp01(BeastsMath.SpookChance(cause) * BeastsMath.Rate(S.greatHerds))))
                    {
                        var moved = Stampede(w, map, herd, from, cause, now);
                        if (moved != null) seen.Add(moved.loadID);
                        continue;
                    }
                    if (!w.watched && herd.Count >= BeastsMath.MinWatchHerd && now - w.watchTry >= BeastsMath.HourTicks) Watch(w, map, herd, now);
                }
            }
            foreach (var id in watches.Keys.Where(id => !seen.Contains(id)).ToList()) watches.Remove(id);
            greatLords.RemoveWhere(id => !seen.Contains(id));
        }

        /// <summary>The herd of a group: its wild animals of the kind there is most of, up and on the map.</summary>
        private static List<Pawn> Herd(Lord lord)
        {
            var wild = lord.ownedPawns.Where(p => p != null && p.Faction == null && p.RaceProps != null && p.RaceProps.Animal && BeastBook.Up(p)).ToList();
            if (wild.Count < BeastsMath.MinHerd) return new List<Pawn>();
            var kind = wild.GroupBy(p => p.kindDef).OrderByDescending(g => g.Count()).First().Key;
            return wild.Where(p => p.kindDef == kind).ToList();
        }

        /// <summary>What spooks it now, if anything, and where from: one of it hurt, a shot flying near, a fire close by.</summary>
        private static string Spooked(HerdWatch w, Map map, List<Pawn> herd, int now, out IntVec3 from)
        {
            from = IntVec3.Invalid;
            foreach (var h in herd)
            {
                int harm = h.mindState?.lastHarmTick ?? -99999;
                if (harm > 0 && now - harm <= Sched.RareTicks + 5)
                {
                    from = Shooter(map, h.Position) ?? h.Position;
                    return BeastsMath.SpookHurt;
                }
            }
            // One of it shot or struck dead since the last look.
            foreach (var m in w.members)
            {
                if (m == null || !m.Dead || herd.Contains(m)) continue;
                var at = m.Corpse != null && m.Corpse.Spawned && m.Corpse.Map == map ? m.Corpse.Position : herd[0].Position;
                from = Shooter(map, at) ?? at;
                return BeastsMath.SpookHurt;
            }
            var shots = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            for (int i = 0; i < shots.Count && i < 60; i++)
            {
                var s = shots[i];
                if (s == null || !s.Spawned) continue;
                if (!herd.Any(h => h.Position.InHorDistOf(s.Position, BeastsMath.ShotRadius))) continue;
                from = (s as Projectile)?.Launcher?.Position ?? s.Position;
                return BeastsMath.SpookShot;
            }
            if (!w.fireRolled)
            {
                var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
                for (int i = 0; i < fires.Count && i < 200; i++)
                {
                    var f = fires[i];
                    if (f == null || !f.Spawned || !herd.Any(h => h.Position.InHorDistOf(f.Position, BeastsMath.FireRadius))) continue;
                    w.fireRolled = true;
                    from = f.Position;
                    return BeastsMath.SpookFire;
                }
            }
            return null;
        }

        /// <summary>Who shot or struck it, as near as can be seen: the nearest of the colony hunting or drafted, or anyone fighting, within forty cells.</summary>
        private static IntVec3? Shooter(Map map, IntVec3 at)
        {
            Pawn best = null;
            float bestD = 40f * 40f;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.RaceProps == null || !p.RaceProps.Humanlike || p.Downed) continue;
                if (!(p.Drafted || p.CurJobDef == JobDefOf.Hunt || p.CurJobDef == JobDefOf.AttackStatic || p.CurJobDef == JobDefOf.AttackMelee)) continue;
                float d = p.Position.DistanceToSquared(at);
                if (d >= bestD) continue;
                best = p;
                bestD = d;
            }
            return best?.Position;
        }

        /// <summary>
        /// The stampede: the herd sprints for the edge away from what spooked it (any of it already turned on its hunter is
        /// left to its fury), told at once where it happens. Returns the herd's new group.
        /// </summary>
        private static Lord Stampede(HerdWatch w, Map map, List<Pawn> herd, IntVec3 from, string cause, int now)
        {
            var runners = herd.Where(h => !h.InMentalState).ToList();
            if (runners.Count == 0) return null;
            var center = new IntVec3((int)runners.Average(h => h.Position.x), 0, (int)runners.Average(h => h.Position.z));
            // Away from what spooked it; spooked from its own midst (a fire under it), on outward from the map's middle.
            var away = from.IsValid ? (center - from).ToVector3() : Vector3.zero;
            if (away.sqrMagnitude < 1f) away = (center - map.Center).ToVector3();
            if (away.sqrMagnitude < 1f) away = new Vector3(1f, 0f, 0f);
            var dir = away.normalized * (map.Size.x + map.Size.z);
            var target = new IntVec3(Mathf.Clamp(center.x + (int)dir.x, 0, map.Size.x - 1), 0, Mathf.Clamp(center.z + (int)dir.z, 0, map.Size.z - 1));
            var start = center;
            if (!CellFinder.TryFindRandomEdgeCellNearWith(target, 40f, map,
                    c => map.reachability.CanReach(start, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.NoPassClosedDoors).WithFenceblocked(true)), out var edge))
                edge = target;
            Lord lord;
            try
            {
                Lords.Leave(runners);
                lord = LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(edge, LocomotionUrgency.Sprint, 12f, false, false), map, runners);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beasts: a stampede", e);
                return null;
            }
            w.stampede = true;
            w.stampedeAt = now;
            w.lord = lord;
            foreach (var h in runners) w.last[h] = h.Position;
            watches[lord.loadID] = w;
            if (w.great) greatLords.Add(lord.loadID);
            string kinds = w.kind.GetLabelPlural();
            string text = ("CC_Herd_Stampede_" + cause).Translate(kinds.Named("KIND"), runners.Count.Named("COUNT")).Resolve();
            if (runners.Any(h => h.BodySize >= BeastsMath.TrampleMinBody)) text += " " + "CC_Herd_StampedeWay".Translate().Resolve();
            Tell.Message(NewsKind.Land, Importance.Important, map, text, new LookTargets(runners.First()), MessageTypeDefOf.ThreatSmall);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_Herd_StampedeChronicle".Translate(kinds.Named("KIND")).Resolve()));
            return lord;
        }

        /// <summary>
        /// The runners' way since the last look: a big one knocks down someone smaller it passes (by the everyday hurts'
        /// scale and shared cap, once each, three at most) and flattens a few plants of the fields it runs through.
        /// </summary>
        private static void Trample(HerdWatch w, Map map, List<Pawn> herd, int now)
        {
            bool live = now - w.stampedeAt <= BeastsMath.StampedeHours * BeastsMath.HourTicks;
            float reach = BeastsMath.TrampleReach * BeastsMath.TrampleReach;
            foreach (var r in herd)
            {
                var b = r.Position;
                var a = w.last.TryGetValue(r, out var prev) ? prev : b;
                w.last[r] = b;
                if (!live || a == b || r.BodySize < BeastsMath.TrampleMinBody) continue;
                if (w.crops < BeastsMath.MaxCrops)
                    foreach (var c in GenSight.PointsOnLineOfSight(a, b).ToList())
                    {
                        if (w.crops >= BeastsMath.MaxCrops || !c.InBounds(map) || !(map.zoneManager.ZoneAt(c) is Zone_Growing)) continue;
                        var plant = c.GetPlant(map);
                        if (plant == null || plant.def.plant == null || plant.def.plant.IsTree || !Rand.Chance(BeastsMath.CropChance)) continue;
                        plant.Destroy(DestroyMode.KillFinalize);
                        w.crops++;
                    }
                if (!Harm.On || w.trampled >= BeastsMath.MaxTrampled) continue;
                foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (p == null || p == r || p.Dead || p.Downed || herd.Contains(p) || w.passed.Contains(p) || p.Flying) continue;
                    if (!BeastsMath.CanTrample(r.BodySize, p.BodySize)) continue;
                    if (BeastsMath.SegmentDistSq(a.x, a.z, b.x, b.z, p.Position.x, p.Position.z) > reach) continue;
                    // Once a stampede: it went past them, knocking them down or not.
                    w.passed.Add(p);
                    if (!Harm.May(p) || !Rand.Chance(Harm.Chance(HarmKinds.Stampede, BeastsMath.TrampleChance(r.BodySize, p.BodySize)))) continue;
                    Knock(r, p, map, w);
                    if (w.trampled >= BeastsMath.MaxTrampled) break;
                }
            }
            if (!live && !w.cropsTold && w.crops > 0)
            {
                w.cropsTold = true;
                News.Post("CC_Herd_Crops".Translate(w.kind.GetLabelPlural().Named("KIND"), w.crops.Named("COUNT")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NegativeEvent, Importance.Notable);
            }
        }

        /// <summary>A runner knocks someone down: a blunt blow from it, shown over them, and for one of the colony told at once.</summary>
        private static void Knock(Pawn r, Pawn p, Map map, HerdWatch w)
        {
            try
            {
                float dmg = BeastsMath.TrampleDamage(r.BodySize, Rand.Value);
                p.TakeDamage(new DamageInfo(DamageDefOf.Blunt, dmg, 0f, (p.Position - r.Position).AngleFlat, r));
                Harm.Note(p, HarmKinds.Stampede);
                w.trampled++;
                if (p.Faction == Faction.OfPlayer || p.HostFaction == Faction.OfPlayer)
                    Tell.Message(NewsKind.Land, Importance.Important, map, "CC_Herd_Trampled".Translate(p.Named("PAWN"), r.KindLabel.Named("ONE")).Resolve(), new LookTargets(p),
                        MessageTypeDefOf.NegativeHealthEvent);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beasts: trampled", e);
            }
        }

        // ---------------- The pause while they pass ----------------

        /// <summary>Someone in sight of the herd and at nothing pressing: idle, wandering, at their leisure, hauling or cleaning, and not told to.</summary>
        private static bool AtEase(Pawn c)
        {
            if (c.Downed || c.Drafted || c.InMentalState || !c.Awake() || c.IsPrisoner || c.GetLord() != null || !People.HasInnerLife(c)) return false;
            var job = c.CurJob;
            if (job == null) return true;
            if (job.playerForced || job.def == BeastsDefOf.Rimfolk_WatchHerd) return false;
            var d = job.def;
            return d.joyKind != null || d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander || d == JobDefOf.Wait || d == JobDefOf.HaulToCell || d == JobDefOf.Clean;
        }

        /// <summary>A herd of six or more in view: up to three of the colony stop to watch it go by, and one of them says so.</summary>
        private static void Watch(HerdWatch w, Map map, List<Pawn> herd, int now)
        {
            w.watchTry = now;
            if (MapFacts.ThreatOn(map) || BeastsDefOf.Rimfolk_WatchHerd == null) return;
            var center = new IntVec3((int)herd.Average(h => h.Position.x), 0, (int)herd.Average(h => h.Position.z));
            var watchers = new List<(Pawn who, Pawn what)>();
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (watchers.Count >= BeastsMath.WatchMax) break;
                if (!c.Position.InHorDistOf(center, BeastsMath.WatchRange) || !AtEase(c)) continue;
                var near = herd.OrderBy(h => h.Position.DistanceToSquared(c.Position)).First();
                if (!GenSight.LineOfSight(c.Position, near.Position, map, true)) continue;
                watchers.Add((c, near));
            }
            if (watchers.Count == 0) return;
            w.watched = true;
            foreach (var (who, what) in watchers)
            {
                var job = JobMaker.MakeJob(BeastsDefOf.Rimfolk_WatchHerd, what);
                job.expiryInterval = BeastsMath.WatchTicks + 120;
                who.jobs.StartJob(job, JobCondition.InterruptForced, null, true, true);
            }
            var first = watchers[0].who;
            var tags = new List<string> { w.great ? "great" : "herd" };
            if (first.DevelopmentalStage.Juvenile()) tags.Add("child");
            string line = Flavor.FromPool(PassesPool, first, null, null, tags.ToArray());
            // apart: a word on the herd going by, said to nobody in particular; the herd is where they look.
            if (!line.NullOrEmpty()) Speech.Say(first, line.Formatted(w.kind.GetLabelPlural().Named("KIND")).Resolve());
        }

        /// <summary>One who stopped to watch the herd go by, when they go back to what they were doing.</summary>
        public static void Watched(Pawn p)
        {
            if (p == null || p.Dead || !People.HasInnerLife(p)) return;
            CouncilGameComponent.GiveThought(p, BeastsDefOf.Rimfolk_Herd_Watched);
        }

        // ---------------- Dev mode ----------------

        /// <summary>The herds crossing each home map now and how each stands, for the dev tools' log.</summary>
        public static string Describe()
        {
            var parts = new List<string>();
            foreach (var kv in watches)
            {
                var w = kv.Value;
                parts.Add((w.kind?.label ?? "?") + " (group " + kv.Key + (w.great ? ", great" : "") + (w.stampede ? ", stampede: " + w.trampled + " knocked down, " + w.crops + " plants" : "")
                          + (w.watched ? ", watched" : "") + ")");
            }
            return parts.Count == 0 ? "no herd crossing a home map" : string.Join("; ", parts);
        }

        /// <summary>Dev mode: the biggest herd crossing this map spooked now, as if shot at from the map's middle.</summary>
        public static bool DebugStampede(Map map)
        {
            if (map?.lordManager == null) return false;
            foreach (var lord in map.lordManager.lords.ToList())
            {
                if (!(lord?.LordJob is LordJob_ExitMapNear)) continue;
                var herd = Herd(lord);
                if (herd.Count < BeastsMath.MinHerd) continue;
                if (!watches.TryGetValue(lord.loadID, out var w)) watches[lord.loadID] = w = new HerdWatch { lord = lord, kind = herd[0].kindDef, great = greatLords.Contains(lord.loadID) };
                if (w.stampede) continue;
                return Stampede(w, map, herd, map.Center, BeastsMath.SpookShot, Find.TickManager.TicksGame) != null;
            }
            return false;
        }
    }

    /// <summary>Stopping to watch the herd go by: standing where they are, facing it, about half an hour, then back to what they were at.</summary>
    public class JobDriver_WatchHerd : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var watch = ToilMaker.MakeToil("WatchHerd");
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = BeastsMath.WatchTicks;
            watch.handlingFacing = true;
            watch.socialMode = RandomSocialMode.SuperActive;
            watch.tickIntervalAction = delta =>
            {
                if (job.targetA.Thing is Pawn h && h.Spawned && h.Map == pawn.Map && !h.Dead) pawn.rotationTracker.FaceCell(h.Position);
                else EndJobWith(JobCondition.Succeeded);
            };
            watch.AddFinishAction(() => GreatHerds.Watched(pawn));
            yield return watch;
        }
    }
}
