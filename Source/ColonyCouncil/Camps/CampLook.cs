using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // A camp that looks lived-in, each kind as its letters tell it (CampKindDef: animals, campGoods, leaveBehind,
    // farewellWalk, momentLines): the animals they bring and keep, the goods they put out and carry about, a smith mending
    // one of the colony's buildings, the last walk before they go, and what they leave behind. Their everyday work is in
    // CampWork; this keeps the things and the spots, and says the camp's own lines at those moments.
    public partial class Camp
    {
        /// <summary>Their animals on the map, in their group (pack beasts, a herd, a dog).</summary>
        public List<Pawn> herd = new List<Pawn>();
        /// <summary>The goods they put out once the camp stood (bales, a woodpile, washing): theirs, and only these are carried about.</summary>
        public List<Thing> props = new List<Thing>();
        public bool putOut;
        /// <summary>Where their grazing beasts are kept (beyond the camp), and where pack beasts are tethered (by the stash).</summary>
        public IntVec3 pasture = IntVec3.Invalid, picket = IntVec3.Invalid;

        // The farewell walk (CampKindDef.farewellWalk).
        public Pawn walker;
        /// <summary>When the walk must end (-1: not begun).</summary>
        public int walkUntil = -1;
        public bool walkDone;
        /// <summary>The colony's gate, for a walk there.</summary>
        public IntVec3 walkCell = IntVec3.Invalid;
        /// <summary>Who the walker has gone to see (or set out to).</summary>
        public List<Pawn> walkSeen = new List<Pawn>();
        public int walkVisits;
        /// <summary>The key of what they left behind, for the farewell's lines ([left:key]).</summary>
        public string leftKey;

        // A smith mending one of the colony's buildings (the help "repair").
        public Thing repairTarget;
        public int repairUntil = -1;
        public int repairs;
        public string repairedLabel;

        public bool WalkActive => walkUntil >= 0 && !walkDone;

        // ---------------- Spots ----------------

        private IntVec3 Away => CampSite.Away(center, colonyCenter.IsValid ? colonyCenter : (map?.Center ?? center));

        private IntVec3 Side
        {
            get
            {
                var a = Away;
                return new IntVec3(a.z, 0, -a.x);
            }
        }

        /// <summary>Where pack beasts are tethered: beside the stash, where porters load and unload them.</summary>
        public IntVec3 Picket
        {
            get
            {
                if (!picket.IsValid && map != null)
                {
                    picket = OpenSpot(stash + Side * 3, 3, false);
                    if (!picket.IsValid) picket = stash;
                }
                return picket;
            }
        }

        /// <summary>Where a herd grazes: open ground beyond the camp, on the far side from the colony and clear of it.</summary>
        public IntVec3 Pasture
        {
            get
            {
                if (!pasture.IsValid && map != null)
                {
                    pasture = OpenSpot(center + Away * 13, 6, true);
                    if (!pasture.IsValid) pasture = Picket;
                }
                return pasture;
            }
        }

        private IntVec3 OpenSpot(IntVec3 near, int radius, bool clearOfColony)
        {
            if (map == null || !near.IsValid) return IntVec3.Invalid;
            near = near.ClampInsideMap(map);
            var taken = new HashSet<IntVec3>(plans.Where(p => p.def != null).SelectMany(p => p.Rect));
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            if (CellFinder.TryFindRandomCellNear(near, map, radius, c => c.Standable(map) && !c.Roofed(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false)
                                                                     && !taken.Contains(c) && (!clearOfColony || ClearOfColony(c))
                                                                     && map.reachability.CanReach(center, c, PathEndMode.OnCell, tp), out var cell))
                return cell;
            return IntVec3.Invalid;
        }

        /// <summary>A spot of the camp by name: stash, fire, picket, pasture, door (outside a hut's door, beside the doorway), wall (the fire side of the windbreak), or any building role.</summary>
        public IntVec3 Spot(string at)
        {
            if (map == null) return IntVec3.Invalid;
            switch (at)
            {
                case null:
                case "stash": return stash;
                case "fire": return FireCell;
                case "picket": return Picket;
                case "pasture": return Pasture;
            }
            var mine = plans.Where(p => p.role == at && !p.dropped && p.def != null).ToList();
            if (mine.Count == 0) return FireCell;
            // The middle of a windbreak; a standing building before one given up on.
            var plan = mine.OrderBy(p => p.Built(map, faction) == null ? 1 : 0).ThenBy(p => p.cell.DistanceToSquared(FireCell + Away * 6)).First();
            var c = plan.cell;
            // Outside the door, to one side of the doorway; on the fire side of the windbreak.
            if (at == "door") c = c - Away + Side;
            else if (at == "wall") c -= Away;
            if (!c.InBounds(map) || !c.Standable(map)) return plan.cell.InBounds(map) ? plan.cell : FireCell;
            return c;
        }

        /// <summary>Where they bring their kills to be dressed: their butchering spot, else beside the fire.</summary>
        public IntVec3 KillSpot
        {
            get
            {
                var b = plans.FirstOrDefault(p => p.role == "butcher" && !p.dropped)?.Built(map, faction);
                return b != null ? b.Position : FireCell;
            }
        }

        /// <summary>Their kills lying at the camp, waiting to be dressed (only bodies are looked through, not all their goods).</summary>
        public IEnumerable<Corpse> KillsAtCamp()
        {
            if (map == null || owned.Count == 0) yield break;
            float r = CampSite.CoreRadius + 6;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (t is Corpse c && c.Spawned && Owns(c) && c.Position.InHorDistOf(center, r)) yield return c;
        }

        /// <summary>They have a butchering spot standing: their kills are dressed there as soon as they're in, by day too.</summary>
        public bool ButchersByDay => plans.Any(p => p.role == "butcher" && !p.dropped && p.Built(map, faction) != null);

        // ---------------- Words ----------------

        /// <summary>A line of the camp's own for this moment ([kill], [carry], [round]...), said by <paramref name="p"/>.</summary>
        public void SayMoment(Pawn p, string moment, float chance = 1f)
        {
            if (p == null || !p.Spawned || kind?.momentLines == null || !Rand.Chance(chance)) return;
            var lines = kind.momentLines.Where(l => HasTag(l, moment)).ToList();
            if (lines.Count == 0) return;
            var tags = new HashSet<string> { moment };
            Flavor.AddEraTags(tags);
            CampWords.AddTags(tags, kind, faction, map);
            string line = Flavor.Pick(lines, tags);
            if (line.NullOrEmpty()) return;
            // Said to nobody in particular: no second person, so it shows wherever they are.
            string said = Format(line, p);
            Speech.Say(p, said);
        }

        /// <summary>
        /// Their farewell at the end of their stay, told as it went: a line about what they left ([left:key]), the healer's
        /// round ([round]) or the smith's mending ([repaired]) when that happened, any other line otherwise. {X} is whoever
        /// took the last walk, {THING} what the smith mended.
        /// </summary>
        private string FarewellText()
        {
            var lines = kind?.farewell;
            if (lines.NullOrEmpty()) return null;
            var marks = new HashSet<string>();
            if (!leftKey.NullOrEmpty()) marks.Add("left:" + leftKey);
            if (walkVisits > 0) marks.Add("round");
            if (repairs > 0) marks.Add("repaired");
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            CampWords.AddTags(tags, kind, faction, map);
            tags.UnionWith(marks);
            string line = marks.Count > 0 ? Flavor.Pick(lines.Where(l => marks.Any(m => HasTag(l, m))), tags) : null;
            line ??= Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
            var x = walker != null && !walker.Dead && walkVisits + (leftKey != null ? 1 : 0) > 0 ? walker : null;
            return Format(line, x, null, 0, repairedLabel);
        }

        /// <summary>The animals with them, in words ("three muffalo and a dog"), or null.</summary>
        public string AnimalsText()
        {
            var here = herd.Where(a => a != null && !a.Dead && a.Spawned).ToList();
            if (here.Count == 0) return null;
            var parts = here.GroupBy(a => a.kindDef).Select(g =>
            {
                int n = g.Count();
                return n == 1 ? Find.ActiveLanguageWorker.WithIndefiniteArticle(g.Key.label) : n + " " + g.Key.GetLabelPlural(n);
            }).ToList();
            return TextUtil.JoinAnd(parts);
        }

        // ---------------- Animals ----------------

        /// <summary>The animals a camp of this kind brings (made, not yet on the map): one kind per entry, a handful at most.</summary>
        public static List<Pawn> MakeAnimals(CampKindDef kind, Faction f, Map map)
        {
            var list = new List<Pawn>();
            if (kind?.animals == null || f == null || map == null) return list;
            foreach (var g in kind.animals)
            {
                if (g == null) continue;
                try
                {
                    int n = CampLookMath.AnimalsFor(g.count.RandomInRange, list.Count);
                    if (n <= 0) continue;
                    var pk = AnimalKindFor(g, f, map);
                    if (pk == null) continue;
                    for (int i = 0; i < n; i++)
                    {
                        var a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pk, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                        if (a == null) continue;
                        a.inventory?.DestroyAll();
                        if (g.named && (a.Name == null || a.Name.Numerical)) a.Name = PawnBioAndNameGenerator.GeneratePawnName(a, NameStyle.Full);
                        list.Add(a);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp animals", e);
                }
            }
            return list;
        }

        /// <summary>
        /// The kind of animal for an entry: for pack beasts, the kind their own people's caravans use, if one suits this land
        /// and weather; otherwise one of its kinds that can stand the weather here (a pack beast only where this land allows
        /// one), those that live wild here likelier, or for a dog the first that fits.
        /// </summary>
        private static PawnKindDef AnimalKindFor(CampAnimals g, Faction f, Map map)
        {
            if (g.pack)
            {
                var own = Couriers.AnimalKind(f, map);
                if (own != null) return own;
            }
            if (g.pokemon) return PokeWorldCamp.TrainerKind(map);
            var biome = map.Biome;
            var opts = (g.kinds ?? new List<string>()).Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)
                            && (!k.RaceProps.packAnimal || biome == null || biome.IsPackAnimalAllowed(k.race))).ToList();
            if (opts.Count == 0) return null;
            if (g.follow) return opts[0];
            return opts.RandomElementByWeight(k => biome != null && biome.CommonalityOfAnimal(k) > 0f ? 4f : 1f);
        }

        /// <summary>Which of the kind's animal entries this animal of theirs belongs to.</summary>
        public CampAnimals GroupOf(Pawn a) => kind?.animals?.FirstOrDefault(g => g != null && g.Matches(a));

        public bool IsHerd(Pawn p) => p != null && herd.Contains(p);

        /// <summary>One of their animals' next job: sleep at night, a dog at its master's heels, a beast cropping the grass, else a slow wander near its place.</summary>
        public Job BeastJob(Pawn a)
        {
            if (a.Downed || a.InMentalState || map == null) return null;
            // The beast that went off with a lost child stays with them (CampLost.cs).
            if (IsLost(a))
            {
                var lost = LostJob(a);
                if (lost != null) return lost;
            }
            var g = GroupOf(a);
            int hour = GenLocalDate.HourOfDay(map);
            bool night = hour >= 21 || hour < 6;
            var home = g == null || g.follow ? FireCell : Spot(g.at);
            var rest = a.needs?.rest;
            if (rest != null && (rest.CurLevelPercentage < 0.25f || (night && rest.CurLevelPercentage < 0.9f))) return LieDown(a, home);
            // Walked to the colony's water with the drovers, or strayed into its fields (CampRequests.cs): they mill about there.
            if (errandHerd && ErrandLive && !night && !(g != null && g.follow) && errandCell.IsValid)
            {
                var there = Wander(a, errandCell, 4, true);
                if (there != null) return there;
            }
            bool loose = false;
            if (g != null && g.follow)
            {
                var master = Master();
                if (master != null)
                {
                    home = master.Position;
                    loose = true;
                }
            }
            else if (Rand.Chance(0.35f))
            {
                var graze = Graze(a, home);
                if (graze != null) return graze;
            }
            return Wander(a, home, loose ? 3 : 5, loose);
        }

        /// <summary>Whom their dog keeps close to: whoever leads them, else the first of them up and about.</summary>
        private Pawn Master()
        {
            bool Up(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && p.Awake() && p.Map == map;
            if (Up(leader) && Here.Contains(leader)) return leader;
            return Here.FirstOrDefault(p => Up(p) && !p.DevelopmentalStage.Juvenile());
        }

        private Job LieDown(Pawn a, IntVec3 near)
        {
            if (!CellFinder.TryFindRandomCellNear(near, map, 3, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && !c.Roofed(map)
                                                                  && a.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                cell = a.Position;
            return JobMaker.MakeJob(JobDefOf.LayDown, cell);
        }

        /// <summary>Wild grass near their place, never sown, marked or in the colony's zones or home ground.</summary>
        private Job Graze(Pawn a, IntVec3 near)
        {
            var food = a.needs?.food;
            if (food == null || food.CurLevelPercentage >= CampLookMath.GrazeBelow || !near.IsValid) return null;
            var home = map.areaManager.Home;
            Plant best = null;
            float bestD = float.MaxValue;
            foreach (var t in GenRadial.RadialDistinctThingsAround(near, map, 6f, true))
            {
                if (!(t is Plant p) || p.sown || p.def.plant == null || p.def.plant.IsTree || !p.IngestibleNow || !a.RaceProps.CanEverEat(p)) continue;
                var c = p.Position;
                if (map.zoneManager.ZoneAt(c) != null || (home != null && home[c]) || map.designationManager.DesignationOn(p) != null) continue;
                float d = c.DistanceToSquared(a.Position);
                if (d >= bestD || !a.CanReserveAndReach(p, PathEndMode.Touch, Danger.None)) continue;
                best = p;
                bestD = d;
            }
            if (best == null) return null;
            var job = JobMaker.MakeJob(JobDefOf.Ingest, best);
            job.count = 1;
            return job;
        }

        /// <summary>A pause, then a few steps to somewhere near their place (as the game's own wandering goes).</summary>
        private Job Wander(Pawn a, IntVec3 near, int radius, bool anywhere)
        {
            bool wait = a.mindState.nextMoveOrderIsWait;
            a.mindState.nextMoveOrderIsWait = !wait;
            if (wait)
            {
                var w = JobMaker.MakeJob(JobDefOf.Wait_Wander);
                w.expiryInterval = Rand.Range(300, 900);
                return w;
            }
            if (!near.IsValid) return null;
            var home = map.areaManager.Home;
            if (!CellFinder.TryFindRandomCellNear(near, map, radius, c => c.Standable(map) && (anywhere || (map.zoneManager.ZoneAt(c) == null && !(home?[c] ?? false)))
                                                                     && a.CanReach(c, PathEndMode.OnCell, Danger.None), out var cell))
                return null;
            var job = JobMaker.MakeJob(JobDefOf.GotoWander, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }

        /// <summary>
        /// Every hour: their animals still with them are kept fed from what the camp carries (never the colony's fields or
        /// stores); the dead and those gone from the group are let go, and goods no longer theirs are forgotten.
        /// </summary>
        private void KeepHerd()
        {
            LeadOffStrays();
            herd.RemoveAll(a => a == null || a.Dead || a.Destroyed || !a.Spawned || a.Map != map || a.GetLord() != lord);
            props.RemoveAll(t => t == null || t.Destroyed || !Owns(t));
            if (!LordAlive) return;
            // Any animal with them (a beast bought from the colony too).
            foreach (var a in lord.ownedPawns)
            {
                if (a?.RaceProps == null || a.RaceProps.Humanlike || a.Dead) continue;
                var food = a.needs?.food;
                if (food != null) food.CurLevelPercentage = CampLookMath.Fed(food.CurLevelPercentage);
            }
        }

        /// <summary>Things have gone wrong (scattered, at war): their animals are walked off the map now, in a group of their own that never fights.</summary>
        private void SendHerdOff()
        {
            if (LordAlive) CampBeasts.LeadOff(lord);
            LeadOffStrays();
        }

        /// <summary>
        /// Their animals no longer in their group (taken out of it before a flight or a fight): walked off the map in a
        /// group of their own that doesn't defend itself, and no longer the camp's.
        /// </summary>
        public void LeadOffStrays()
        {
            if (herd.Count == 0 || map == null) return;
            var strays = herd.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map && a.GetLord() == null).ToList();
            if (strays.Count == 0) return;
            herd.RemoveAll(strays.Contains);
            try
            {
                LordMaker.MakeNewLord(faction ?? strays[0].Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), map, strays);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beasts led off", e);
            }
        }

        /// <summary>
        /// The books close with animals of theirs still on the map: they go too (walking off on their own), or, when
        /// everyone left of the camp joined the colony, they're the colony's now.
        /// </summary>
        private void ReleaseHerd(bool toColony)
        {
            var left = herd.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map).ToList();
            herd.Clear();
            if (left.Count == 0) return;
            try
            {
                foreach (var a in left) a.GetLord()?.RemovePawn(a);
                if (toColony)
                {
                    foreach (var a in left) a.SetFaction(Faction.OfPlayer);
                    return;
                }
                LordMaker.MakeNewLord(faction ?? left[0].Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), map, left);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp herd release", e);
            }
        }

        /// <summary>
        /// A beast from their herd for the colony (bought from the drovers): one of the grazing beasts, the colony's now,
        /// where it stands. Null when they have none to spare.
        /// </summary>
        public Pawn HandOverBeast()
        {
            var a = herd.Where(x => x != null && !x.Dead && x.Spawned && !x.Downed && x.Map == map && GroupOf(x)?.follow != true)
                .OrderBy(x => x.ageTracker?.AgeBiologicalYearsFloat ?? 0f).FirstOrDefault();
            if (a == null) return null;
            try
            {
                a.GetLord()?.RemovePawn(a);
                herd.Remove(a);
                a.SetFaction(Faction.OfPlayer);
                return a;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beast sold", e);
                return null;
            }
        }

        // ---------------- Goods put out ----------------

        /// <summary>Once the camp stands: the goods they put out at their spots (bales by the beasts, a woodpile by the door, washing by the windbreak).</summary>
        private void PutOut()
        {
            if (putOut) return;
            putOut = true;
            if (kind.campGoods.NullOrEmpty()) return;
            foreach (var g in kind.campGoods)
            {
                try
                {
                    var def = GoodsDef(g);
                    var spot = def != null ? Spot(g.at) : IntVec3.Invalid;
                    if (!spot.IsValid) continue;
                    int n = Mathf.Max(1, g.count.RandomInRange);
                    var stuff = g.StuffFor(def);
                    int pieces = def.stackLimit > 1 ? 1 : n;
                    for (int i = 0; i < pieces; i++)
                    {
                        var t = ThingMaker.MakeThing(def, stuff);
                        t.stackCount = def.stackLimit > 1 ? Mathf.Min(n, def.stackLimit) : 1;
                        if (GenPlace.TryPlaceThing(t, spot, map, ThingPlaceMode.Near, out var put, null, c => FreeAt(c, spot)) && put != null)
                        {
                            Own(put);
                            if (!props.Contains(put)) props.Add(put);
                        }
                        else if (!t.Destroyed && !t.Spawned) t.Destroy();
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp goods", e);
                }
            }
        }

        /// <summary>The goods' kind for their people: the first listed that exists and isn't beyond them (tribal folk hang out tribalwear).</summary>
        private ThingDef GoodsDef(CampGoods g)
        {
            var tech = faction?.def?.techLevel ?? TechLevel.Undefined;
            foreach (var n in g?.defs ?? new List<string>())
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d == null || d.category != ThingCategory.Item) continue;
                if (tech != TechLevel.Undefined && d.techLevel != TechLevel.Undefined && d.techLevel > tech) continue;
                return d;
            }
            return null;
        }

        /// <summary>A free spot close to <paramref name="spot"/>: nothing built there and nothing lying there.</summary>
        private bool FreeAt(IntVec3 c, IntVec3 spot) =>
            c.InHorDistOf(spot, 3f) && c.GetEdifice(map) == null && !c.GetThingList(map).Any(t => t.def.category == ThingCategory.Item);

        public bool IsProp(Thing t) => t != null && props.Contains(t);

        public CampGoods GoodsOf(Thing t) => t == null ? null : kind?.campGoods?.FirstOrDefault(g => g?.defs != null && g.defs.Contains(t.def.defName));

        /// <summary>Goods left for the colony when they pack up in peace (the hermit's woodpile, with the hut): anyone's now.</summary>
        private void LeaveGoods()
        {
            foreach (var t in props.ToList())
            {
                if (t == null || !t.Spawned || !Owns(t) || GoodsOf(t)?.leftForColony != true) continue;
                Disown(t);
                t.SetForbidden(false, false);
                props.Remove(t);
            }
        }

        // ---------------- A smith mending the colony's gate ----------------

        /// <summary>Who of them would mend something: a grown-up who builds, the best crafter, their trader only if there's nobody else.</summary>
        public Pawn Smith()
        {
            var here = Here;
            return here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                .OrderBy(p => p == trader && here.Count > 1 ? 1 : 0).ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0).FirstOrDefault();
        }

        public bool RepairPending => repairTarget != null && !repairTarget.Destroyed && repairTarget.Spawned;

        /// <summary>
        /// A good turn: one of the colony's damaged buildings, mended as a gift (doors and gates first, the nearest to the
        /// camp). The smith walks over and does the work; it's told when it's done. False when there's nothing to mend.
        /// </summary>
        private bool StartRepair()
        {
            if (RepairPending || map == null) return false;
            var smith = Smith();
            if (smith == null || MapFacts.ThreatOn(map)) return false;
            Building best = null;
            float bestScore = float.MaxValue;
            foreach (var t in map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer))
            {
                if (!(t is Building b) || b is Frame || !b.Spawned || b.Destroyed || !b.def.useHitPoints || b.HitPoints >= b.MaxHitPoints * CampLookMath.RepairBelow) continue;
                if (b.IsBurning() || b.Position.Fogged(map) || (b.Position.GetRoom(map)?.IsPrisonCell ?? false)) continue;
                float score = CampLookMath.RepairScore(b.Position.DistanceTo(center), b.def.IsDoor);
                if (score >= bestScore || !smith.CanReach(b, PathEndMode.Touch, Danger.Some)) continue;
                best = b;
                bestScore = score;
            }
            if (best == null) return false;
            repairTarget = best;
            repairUntil = Now + CampLookMath.RepairTicks;
            return true;
        }

        public Job RepairJob(Pawn smith)
        {
            if (!RepairPending || !(repairTarget is Building b) || b.HitPoints >= b.MaxHitPoints) return null;
            if (!smith.CanReserveAndReach(b, PathEndMode.Touch, Danger.Some)) return null;
            return JobMaker.MakeJob(CampDefOf.Council_CampRepair, b);
        }

        /// <summary>The mending is done: the building as good as new, for nothing, and the colony hears of it.</summary>
        public void Repaired(Pawn smith, Thing t)
        {
            if (!(t is Building b) || b != repairTarget) return;
            repairTarget = null;
            repairUntil = -1;
            if (b.Destroyed || !b.Spawned || b.HitPoints >= b.MaxHitPoints) return;
            b.HitPoints = b.MaxHitPoints;
            map.listerBuildingsRepairable.Notify_BuildingRepaired(b);
            repairs++;
            repairedLabel = b.LabelNoCount;
            AddTension(-2f);
            SayMoment(smith, "repair");
            Note(HelpText("repair", smith, null, 0, b.LabelNoCount), MessageTypeDefOf.PositiveEvent, Importance.Notable, b);
        }

        private void WatchRepair()
        {
            if (repairTarget == null) return;
            if (repairTarget.Destroyed || !repairTarget.Spawned || Now > repairUntil || (repairTarget is Building b && b.HitPoints >= b.MaxHitPoints))
            {
                repairTarget = null;
                repairUntil = -1;
            }
        }

        // ---------------- The farewell walk ----------------

        /// <summary>
        /// Their time is up: first the last walk their kind takes (the healer's round, a gift carried to the colony's gate).
        /// True while it's under way; false when there's none to take or it's over, and they go.
        /// </summary>
        private bool FarewellWalking()
        {
            var w = kind.farewellWalk;
            if (w == null || walkDone) return false;
            if (walkUntil < 0)
            {
                // A round of the sick waits for morning (for up to half a day past their time); a walk to the gate goes when it goes.
                int hour = GenLocalDate.HourOfDay(map);
                if (w.to == "sick" && (hour < 6 || hour >= 20) && Now - untilTick < CampLookMath.TicksPerDay / 2) return true;
                walker = Walker(w);
                bool any = walker != null && (w.to == "gate" ? (walkCell = GateFor(walker)).IsValid : WalkTarget(walker) != null);
                if (!any)
                {
                    walkDone = true;
                    walker = null;
                    return false;
                }
                // They set off once what they're doing is done (the camp's jobs give the walk first).
                walkUntil = Now + CampLookMath.WalkTicks;
                return true;
            }
            bool ok = walker != null && !walker.Dead && !walker.Downed && Here.Contains(walker);
            if (!CampLookMath.WalkOver(walkDone, ok, Now, walkUntil)) return true;
            walkDone = true;
            return false;
        }

        /// <summary>The colony says their time is up: they go, after their last walk if their kind takes one.</summary>
        public void TimeToGo()
        {
            if (phase != CampPhase.Living || !FarewellWalking()) Leave("time");
        }

        private Pawn Walker(CampWalk w)
        {
            var here = Here.Where(p => !p.Downed && !p.InMentalState && !p.DevelopmentalStage.Juvenile()).ToList();
            if (here.Count == 0) return null;
            if (w.who == "healer")
                return here.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault() ?? here[0];
            return here.Contains(leader) ? leader : here[0];
        }

        /// <summary>
        /// Whom the walker looks in on next: the colony's people needing their wounds seen to, then the ill, ailing or laid
        /// up, then those who spent time at the camp's fire; the nearest first, and never more than the walk's count.
        /// </summary>
        private Pawn WalkTarget(Pawn w)
        {
            if (w == null || walkSeen.Count >= (kind.farewellWalk?.count ?? 4)) return null;
            Pawn best = null;
            int bestRank = int.MaxValue;
            float bestD = float.MaxValue;
            foreach (var c in IncidentWorker_Happening.Colonists(map))
            {
                if (c == null || walkSeen.Contains(c) || c.Dead || !c.Spawned || c.InMentalState || c.Drafted || c.IsPrisoner) continue;
                if (c.Position.Fogged(map) || (c.Position.GetRoom(map)?.IsPrisonCell ?? false)) continue;
                int rank = c.health.HasHediffsNeedingTend() ? 0
                    : c.Downed || LifeChecks.Illness(c) != null || HappeningWorker_Setting.Ailment(c) != null ? 1
                    : warmth.TryGetValue(c, out int warm) && warm > 0 ? 2 : 3;
                if (rank > 2) continue;
                float d = c.Position.DistanceToSquared(w.Position);
                if (rank > bestRank || (rank == bestRank && d >= bestD)) continue;
                if (!w.CanReach(c, PathEndMode.Touch, Danger.Some)) continue;
                best = c;
                bestRank = rank;
                bestD = d;
            }
            return best;
        }

        /// <summary>
        /// The colony's gate as seen from the camp: outside the colony's door nearest the camp (open ground the walker can
        /// reach), else where the colony's ground meets the open (the play-outs' gate).
        /// </summary>
        private IntVec3 GateFor(Pawn w)
        {
            var doors = map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Where(d => d.Spawned).OrderBy(d => d.Position.DistanceToSquared(center)).Take(12);
            foreach (var d in doors)
                foreach (var c in GenAdj.CellsAdjacentCardinal(d))
                {
                    if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.Fogged(map)) continue;
                    if (w.CanReach(c, PathEndMode.OnCell, Danger.Some)) return c;
                }
            return PlaySpots.GateCell(map, w);
        }

        /// <summary>The walker's next stop, or null when the walk is done.</summary>
        public Job WalkJob(Pawn w)
        {
            var walk = kind.farewellWalk;
            if (walk == null || !WalkActive) return null;
            if (walk.to == "gate")
            {
                if (walkCell.IsValid && w.CanReach(walkCell, PathEndMode.OnCell, Danger.Some)) return JobMaker.MakeJob(CampDefOf.Council_CampLayGift, walkCell);
                walkDone = true;
                return null;
            }
            var c = WalkTarget(w);
            if (c == null)
            {
                walkDone = true;
                return null;
            }
            walkSeen.Add(c);
            return JobMaker.MakeJob(CampDefOf.Council_CampFarewell, c);
        }

        /// <summary>The walker at a colonist's side: a healer sees to wounds that need it (or eases an everyday ailment), and a word in parting.</summary>
        public void LookedIn(Pawn w, Pawn c)
        {
            // together: the farewell job walked them to the colonist's side, and it ends if the colonist walks off.
            if (w == null || c == null || c.Dead || !c.Spawned || !w.Position.InHorDistOf(c.Position, 3f)) return;
            var med = w.skills?.GetSkill(SkillDefOf.Medicine);
            bool healer = med != null && !med.TotallyDisabled && med.Level >= 3 && !w.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
            try
            {
                if (healer && c.health.HasHediffsNeedingTend())
                {
                    TendUtility.DoTend(w, c, null);
                    helps++;
                }
                else if (healer)
                {
                    var ail = HappeningWorker_Setting.Ailment(c);
                    if (ail != null) ail.Severity = Mathf.Max(0.01f, ail.Severity * 0.75f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp farewell round", e);
            }
            walkVisits++;
            Warm(c, 1);
            SayMoment(w, "round");
        }

        /// <summary>The walker at the colony's gate: the gift laid there (a sprig planted, a carving left in its crate).</summary>
        public void LaidGift(Pawn w, IntVec3 cell)
        {
            walkDone = true;
            var gift = kind.leaveBehind?.FirstOrDefault(g => g?.key != null && g.key == kind.farewellWalk?.gift);
            if (gift == null || w == null || !w.Position.InHorDistOf(cell, 2.5f)) return;
            var placed = new List<Thing>();
            if (!Place(gift, cell, w, placed)) return;
            leftKey = gift.key;
            SayMoment(w, "gift");
            // A day on the colony's calendar: the day they stayed and left this behind (Calendar/CalendarLived.cs).
            CalendarTracker.Notify_CampGift(this, gift, placed, cell);
        }

        // ---------------- What they leave behind ----------------

        /// <summary>
        /// Packing up at the end of their stay: what they leave at the camp itself (a ring of seeds round the old fire, the
        /// backdrop on their stage). Gifts for the gate went with the farewell walk.
        /// </summary>
        private void LeaveGifts(IntVec3 fire, IntVec3 taskSpot)
        {
            if (kind.leaveBehind.NullOrEmpty()) return;
            foreach (var g in kind.leaveBehind)
            {
                if (g == null || g.at == "gate") continue;
                var at = g.at == "spot" && taskSpot.IsValid ? taskSpot : fire;
                var placed = new List<Thing>();
                if (!Place(g, at, Here.FirstOrDefault(), placed)) continue;
                leftKey = g.key;
                SayMoment(Here.Where(p => !p.Downed).RandomElementWithFallback(), "gift", 0.7f);
                CalendarTracker.Notify_CampGift(this, g, placed, at);
            }
        }

        /// <summary>A gift set down at or round <paramref name="at"/>: saplings, a plant, a work of art standing or in its crate. Nobody's: it stays.</summary>
        private bool Place(CampGift g, IntVec3 at, Pawn maker, List<Thing> placed = null)
        {
            if (map == null || !at.IsValid || !at.InBounds(map)) return false;
            try
            {
                if (g.wildTrees) return PlantGift(WildTree(), at, g, placed) > 0;
                foreach (var def in g.Defs)
                {
                    if (def.category == ThingCategory.Plant)
                    {
                        if (PlantGift(def, at, g, placed) > 0) return true;
                        continue;
                    }
                    if (def.category != ThingCategory.Building) continue;
                    if (g.minified && def.Minifiable)
                    {
                        var made = MakeGift(def, g, maker);
                        var mini = made.MakeMinified();
                        if (GenPlace.TryPlaceThing(mini, at, map, ThingPlaceMode.Near, out var put) && put != null)
                        {
                            put.SetForbidden(false, false);
                            // The carving itself: found again in its crate, or set up wherever the colony puts it.
                            placed?.Add(made);
                            return true;
                        }
                        if (!mini.Destroyed) mini.Destroy();
                        continue;
                    }
                    var cell = BuildingCellNear(def, at);
                    if (!cell.IsValid) continue;
                    var gift = MakeGift(def, g, maker);
                    GenSpawn.Spawn(gift, cell, map, Rot4.North, WipeMode.Vanish);
                    placed?.Add(gift);
                    return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp gift " + g.key, e);
            }
            return false;
        }

        /// <summary>A work of art of theirs: of the listed material, of middling quality, made by <paramref name="maker"/>, under one of the gift's titles.</summary>
        private static Thing MakeGift(ThingDef def, CampGift g, Pawn maker)
        {
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                var allowed = GenStuff.AllowedStuffsFor(def).ToList();
                stuff = (g.stuff ?? new List<string>()).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(s => s != null && allowed.Contains(s)) ?? GenStuff.DefaultStuffFor(def);
            }
            var t = ThingMaker.MakeThing(def, stuff);
            var art = t.TryGetComp<CompArt>();
            var quality = t.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(new[] { QualityCategory.Poor, QualityCategory.Normal, QualityCategory.Normal, QualityCategory.Good }.RandomElement(), ArtGenerationContext.Outsider);
            else art?.InitializeArt(ArtGenerationContext.Outsider);
            if (art != null)
            {
                if (maker != null) art.JustCreatedBy(maker);
                string title = g.titles.NullOrEmpty() ? null : Flavor.PickText(g.titles);
                if (!title.NullOrEmpty()) art.Title = title;
            }
            return t;
        }

        /// <summary>Open ground for a gift that stands: room for all of it, nothing built or lying there, clear of the colony.</summary>
        private IntVec3 BuildingCellNear(ThingDef def, IntVec3 at)
        {
            foreach (var c in GenRadial.RadialCellsAround(at, 4f, true))
            {
                var rect = GenAdj.OccupiedRect(c, Rot4.North, def.size);
                if (rect.All(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null && x.GetFirstPawn(map) == null
                                  && !(x.GetTerrain(map)?.IsWater ?? false) && !(x.GetPlant(map)?.def.plant?.IsTree ?? false) && ClearOfColony(x)))
                    return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The trees that grow wild on this land (never the great ones), the commoner likelier. Null on bare land.</summary>
        private ThingDef WildTree()
        {
            var biome = map?.Biome;
            var trees = biome?.AllWildPlants?.Where(d => d?.plant != null && d.plant.IsTree && d.plant.treeCategory != TreeCategory.Super && biome.CommonalityOfPlant(d) > 0f).ToList();
            return trees.NullOrEmpty() ? null : trees.RandomElementByWeight(d => biome.CommonalityOfPlant(d));
        }

        /// <summary>
        /// Plants set in: in a ring round the old fire (saplings, evenly spaced), or one beside the colony's gate (never on
        /// the doorstep). Only where they can grow; a flower only in its growing season. Returns how many.
        /// </summary>
        private int PlantGift(ThingDef def, IntVec3 at, CampGift g, List<Thing> placed = null)
        {
            if (def?.plant == null) return 0;
            int want = Mathf.Max(1, g.count.RandomInRange), planted = 0;
            bool ring = g.at == "ring";
            List<IntVec3> cells;
            if (ring)
            {
                var round = GenRadial.RadialCellsAround(at, CampLookMath.RingOuter, false).Where(c => CampLookMath.InRing(c.x - at.x, c.z - at.z))
                    .OrderBy(c => Mathf.Atan2(c.z - at.z, c.x - at.x)).ToList();
                // Every other cell round the ring from a random start, spaced like seeds pressed in by hand; the gaps
                // only where those won't take.
                int start = round.Count > 0 ? Rand.Range(0, round.Count) : 0;
                cells = new List<IntVec3>();
                for (int i = 0; i < round.Count; i += 2) cells.Add(round[(start + i) % round.Count]);
                for (int i = 1; i < round.Count; i += 2) cells.Add(round[(start + i) % round.Count]);
            }
            else cells = GenRadial.RadialCellsAround(at, 4.9f, false).Where(c => !NextToDoor(c)).ToList();
            foreach (var c in cells)
            {
                if (planted >= want) break;
                var plant = PlantAt(def, c, g.growth, ring);
                if (plant == null) continue;
                planted++;
                placed?.Add(plant);
            }
            return planted;
        }

        private bool NextToDoor(IntVec3 c)
        {
            foreach (var x in GenAdj.CardinalDirections)
            {
                var n = c + x;
                if (n.InBounds(map) && n.GetEdifice(map) is Building_Door) return true;
            }
            return false;
        }

        private Plant PlantAt(ThingDef def, IntVec3 c, float growth, bool atCamp)
        {
            if (!c.InBounds(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null || c.Roofed(map)) return null;
            var old = c.GetPlant(map);
            if (old != null && (old.def == def || old.sown || (old.def.plant?.IsTree ?? false))) return null;
            if (map.zoneManager.ZoneAt(c) != null || (atCamp && !ClearOfColony(c))) return null;
            if (!def.CanEverPlantAt(c, map, true)) return null;
            if (!def.plant.IsTree && !PlantUtility.GrowthSeasonNow(c, map, def)) return null;
            var plant = (Plant)ThingMaker.MakeThing(def);
            plant.Growth = Mathf.Clamp(growth, 0.01f, 1f);
            GenSpawn.Spawn(plant, c, map, WipeMode.Vanish);
            return plant;
        }

        // ---------------- Save ----------------

        private void ExposeLook()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                herd.RemoveAll(a => a == null || a.Discarded || a.Dead);
                props.RemoveAll(t => t == null || t.Destroyed || t.Discarded);
                walkSeen.RemoveAll(p => p == null || p.Discarded);
                if (walker != null && walker.Discarded) walker = null;
                if (repairTarget != null && (repairTarget.Destroyed || repairTarget.Discarded)) repairTarget = null;
            }
            Scribe_Collections.Look(ref herd, "herd", LookMode.Reference);
            Scribe_Collections.Look(ref props, "props", LookMode.Reference);
            Scribe_Values.Look(ref putOut, "putOut");
            Scribe_Values.Look(ref pasture, "pasture", IntVec3.Invalid);
            Scribe_Values.Look(ref picket, "picket", IntVec3.Invalid);
            Scribe_References.Look(ref walker, "walker");
            Scribe_Values.Look(ref walkUntil, "walkUntil", -1);
            Scribe_Values.Look(ref walkDone, "walkDone");
            Scribe_Values.Look(ref walkCell, "walkCell", IntVec3.Invalid);
            Scribe_Collections.Look(ref walkSeen, "walkSeen", LookMode.Reference);
            Scribe_Values.Look(ref walkVisits, "walkVisits");
            Scribe_Values.Look(ref leftKey, "leftKey");
            Scribe_References.Look(ref repairTarget, "repairTarget");
            Scribe_Values.Look(ref repairUntil, "repairUntil", -1);
            Scribe_Values.Look(ref repairs, "repairs");
            Scribe_Values.Look(ref repairedLabel, "repairedLabel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                herd ??= new List<Pawn>();
                props ??= new List<Thing>();
                walkSeen ??= new List<Pawn>();
                herd.RemoveAll(a => a == null);
                props.RemoveAll(t => t == null);
                walkSeen.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>A camp's animals: the duty's job giver (see <see cref="Camp.BeastJob"/>), and leading them off when things go wrong.</summary>
    public class JobGiver_CampBeasts : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var camp = CampTracker.CampOf(pawn);
            // An animal sold to their trader joins the group too: it isn't one of their own, and just stays near.
            if (camp == null || !camp.Settled || pawn.Map != camp.map || !camp.IsHerd(pawn)) return null;
            try
            {
                return camp.BeastJob(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beast job", e);
                return null;
            }
        }
    }

    public static class CampBeasts
    {
        /// <summary>
        /// The animals of a camp's group taken out of it before the group flees (the colony harmed or arrested one of
        /// them) or holds its camp against the colony, so they never take part in either. Called from the group's own
        /// change of plan, so it only takes them out: the camp walks them off the map in a group of their own on its next
        /// look (<see cref="Camp.LeadOffStrays"/>). Without the camp's record, they stay with the group.
        /// </summary>
        public static void LeadOff(Lord lord)
        {
            var camp = CampTracker.Instance?.All.FirstOrDefault(c => c.lord == lord);
            if (lord?.ownedPawns == null || camp == null) return;
            var beasts = lord.ownedPawns.Where(p => p?.RaceProps != null && !p.RaceProps.Humanlike).ToList();
            try
            {
                foreach (var b in beasts)
                {
                    lord.RemovePawn(b);
                    if (!camp.herd.Contains(b)) camp.herd.Add(b);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beasts off", e);
            }
        }
    }
}
