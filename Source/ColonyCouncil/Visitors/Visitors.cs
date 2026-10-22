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
    /// <summary>
    /// People from outside who are really there ("Goods and people come and go on the map" in the settings, with the
    /// couriers): when one of Rimfolk's letters says someone is at the gate, the rider, the family or the envoy walks in
    /// with it and waits there until the colony answers; guests who are given a bed stay the night and go on at first
    /// light; mourners, bands and herds pass on the road, a herd grazing the far meadow overnight on its way.
    /// <para>They are never a threat: only a people at peace with the colony sends anyone, they never come into danger
    /// (raiders, bitter cold or heat), they leave at once if they're hurt or their people turn hostile, and they go when
    /// the cold or hunger gets to them. A colonist taking in the family at the gate takes in those very people; the
    /// rider at the gate is the one handed what the colony sends. Every visit has an end: answered, timed out, the map
    /// gone, a reload, the setting turned off. With the setting off, or when nobody can come, the letter alone tells it,
    /// as before.</para>
    /// </summary>
    public static class Visitors
    {
        public static bool On => Couriers.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Who comes ----------------

        /// <summary>A people that can send someone now: at peace with the colony, of people, with people to send.</summary>
        public static bool Usable(Faction f) => Couriers.Usable(f);

        /// <summary>The people an effect's visitors are from: the event's own, or with <paramref name="enemy"/>, its second people.</summary>
        public static Faction PeopleOf(DilemmaContext ctx, bool enemy)
        {
            if (enemy) return (ctx as HappeningContext)?.enemy;
            return Couriers.PeopleOf(ctx);
        }

        /// <summary>
        /// The kind of person a visit is: a light guard of their caravans for an armed group (drovers, a band), else the
        /// ordinary person they send. A colony of the old ways meets newcomers dressed for its own era.
        /// </summary>
        private static PawnKindDef PersonKind(Faction f, bool armed)
        {
            var kind = armed ? Couriers.PersonKind(f) : Arrivals.PersonKind(f);
            if (kind == null) return null;
            if (!armed && Avail.IsPreIndustrial && f.def.techLevel > TechLevel.Medieval) kind = Arrivals.JoinerKind() ?? kind;
            return kind;
        }

        private static Pawn MakePerson(PawnKindDef kind, Faction f, Map map, FloatRange age, Gender? gender, string lastName, bool child, bool armed, Pawn kinOf)
        {
            var genes = ModsConfig.BiotechActive ? kinOf?.genes : null;
            var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f,
                fixedGender: gender, fixedLastName: lastName, biologicalAgeRange: age,
                developmentalStages: child ? DevelopmentalStage.Child : DevelopmentalStage.Adult,
                dontGiveWeapon: child || !armed && Avail.IsPreIndustrial && f.def.techLevel > TechLevel.Medieval,
                forcedXenotype: genes?.CustomXenotype == null ? genes?.Xenotype : null,
                forcedCustomXenotype: genes?.CustomXenotype);
            // Dressed for the weather they walked through.
            req.Tile = map.Tile;
            req.ForceAddFreeWarmLayerIfNeeded = true;
            return PawnGenerator.GeneratePawn(req);
        }

        /// <summary>
        /// The visit's people, unspawned: one alone, a few of a kind, or a family (a couple who share a name, any more
        /// their children). Robbed on the road: no weapon, and only the clothes they couldn't take.
        /// </summary>
        public static List<Pawn> MakePeople(VisitorTrip t, Faction f, Map map)
        {
            var list = new List<Pawn>();
            var kind = PersonKind(f, t.armed);
            if (kind == null) return list;
            int n = VisitorMath.People(t.count);
            if (t.family) n = Math.Max(2, n);
            string lastName = null;
            Pawn first = null, second = null;
            for (int i = 0; i < n; i++)
            {
                bool child = t.family && i >= 2;
                Pawn p;
                try
                {
                    FloatRange age = t.ages;
                    bool real = false;
                    if (child) { age = ChildAges(); real = ModsConfig.BiotechActive; }
                    else if (t.young) { age = ModsConfig.BiotechActive ? new FloatRange(10f, 12f) : new FloatRange(14f, 15f); real = ModsConfig.BiotechActive; }
                    else if (t.family) age = n > 2 ? new FloatRange(32f, 45f) : new FloatRange(22f, 45f);
                    Gender? gender = t.gender != Gender.None && i == 0 ? t.gender
                        : t.family && i == 1 && first != null ? (first.gender == Gender.Male ? Gender.Female : Gender.Male) : (Gender?)null;
                    p = MakePerson(kind, f, map, age, gender, lastName, real, t.armed && !child && !t.young, first);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor person", e);
                    break;
                }
                if (p == null) continue;
                if (t.robbed) Rob(p);
                if (i == 0 && t.family && p.Name is NameTriple nt) lastName = nt.Last;
                if (t.family) Relate(p, first, second, i);
                if (first == null) first = p;
                else if (second == null) second = p;
                list.Add(p);
            }
            return list;
        }

        /// <summary>Children young enough to be the couple's: real children with Biotech, else teenagers.</summary>
        private static FloatRange ChildAges() => ModsConfig.BiotechActive ? new FloatRange(4f, 12f) : new FloatRange(14f, 17f);

        private static void Relate(Pawn p, Pawn first, Pawn second, int i)
        {
            try
            {
                if (i == 1 && first != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Spouse, first);
                else if (i >= 2)
                {
                    if (first != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, first);
                    if (second != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, second);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor family", e);
            }
        }

        /// <summary>Robbed on the road: the weapon gone, the pack gone, and all but the shirt on their back.</summary>
        private static void Rob(Pawn p)
        {
            try
            {
                p.equipment?.DestroyAllEquipment();
                p.inventory?.DestroyAll();
                var worn = p.apparel?.WornApparel?.ToList();
                if (worn == null) return;
                // Keep the one thing worn closest to the skin.
                var keep = worn.Where(a => a.def.apparel?.layers != null && a.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin)).FirstOrDefault();
                foreach (var a in worn)
                    if (a != keep) a.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor robbed", e);
            }
        }

        /// <summary>The animals that come with them: a pack beast of their caravans, their own animal, a herd.</summary>
        public static List<Pawn> MakeAnimals(VisitorTrip t, Faction f, Map map)
        {
            var list = new List<Pawn>();
            try
            {
                if (t.pack && f.def.techLevel <= TechLevel.Medieval)
                {
                    var kind = Couriers.AnimalKind(f, map);
                    if (kind != null) Add(list, kind, f, null);
                }
                if (!t.pet.NullOrEmpty())
                {
                    var kind = PetKind(t.pet);
                    if (kind != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(kind.race, 0f))
                    {
                        var a = Add(list, kind, f, null);
                        if (a != null && (a.Name == null || a.Name.Numerical)) a.Name = PawnBioAndNameGenerator.GeneratePawnName(a, NameStyle.Full);
                    }
                }
                int herd = VisitorMath.Animals(t.livestock);
                if (herd > 0)
                {
                    var kind = LivestockKind(map, t.beast);
                    for (int i = 0; i < herd && kind != null && list.Count < VisitorMath.MaxAnimals; i++)
                        Add(list, kind, f, t.beast.NullOrEmpty() || i > 0 ? (Gender?)null : Gender.Female);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor animals", e);
            }
            return list;
        }

        private static Pawn Add(List<Pawn> list, PawnKindDef kind, Faction f, Gender? gender)
        {
            var a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedGender: gender));
            if (a == null) return null;
            a.inventory?.DestroyAll();
            list.Add(a);
            return a;
        }

        private static readonly string[] Dogs = { "LabradorRetriever", "Husky", "YorkshireTerrier" };

        /// <summary>A family's animal by the word the text uses: "dog", "goat", or a kind's defName.</summary>
        public static PawnKindDef PetKind(string word)
        {
            if (word.NullOrEmpty()) return null;
            PawnKindDef kind;
            if (word == "dog") kind = Dogs.Select(d => DefDatabase<PawnKindDef>.GetNamedSilentFail(d)).FirstOrDefault(k => k != null);
            else if (word == "goat") kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Goat");
            else kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(word);
            return kind?.RaceProps != null && kind.RaceProps.Animal ? kind : null;
        }

        /// <summary>Beasts a drover drives, as the game names them.</summary>
        private static readonly string[] Livestock = { "Cow", "Sheep", "Goat", "Yak", "Alpaca", "Muffalo", "Dromedary", "Bison", "Donkey", "Horse" };

        /// <summary>
        /// The kind of animal a herd is: <paramref name="wanted"/> if the weather suits it, else farm beasts that can
        /// stand the weather here, those that also live wild on this land likelier. Null when none would do.
        /// </summary>
        public static PawnKindDef LivestockKind(Map map, string wanted)
        {
            if (map == null) return null;
            var biome = map.Biome;
            bool Fits(PawnKindDef k) => k?.race?.race != null && k.RaceProps.Animal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f);
            var first = wanted.NullOrEmpty() ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(wanted);
            if (Fits(first)) return first;
            var opts = Livestock.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(Fits).ToList();
            if (opts.Count == 0) return null;
            return opts.RandomElementByWeight(k =>
            {
                float w = 1f;
                try
                {
                    if (biome != null && biome.CommonalityOfAnimal(k) > 0f) w *= 4f;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor livestock", e);
                }
                return w;
            });
        }

        // ---------------- Where ----------------

        /// <summary>No danger, weather people would walk out in, nothing in the air.</summary>
        public static bool Safe(Map map) => CourierTracker.Safe(map);

        /// <summary>Groups on this map now.</summary>
        public static int OnMap(Map map) => VisitorTracker.Instance?.trips.Count(t => t.map == map && t.OnMap) ?? 0;

        /// <summary>The spot a group heads for: the gate, the far ground, the water, just outside the colony.</summary>
        public static IntVec3 SpotFor(VisitSpot kind, Map map, IntVec3 entry)
        {
            IntVec3 c = IntVec3.Invalid;
            try
            {
                switch (kind)
                {
                    case VisitSpot.Water:
                        c = WaterSide(map);
                        if (!c.IsValid) c = FarCell(map, entry);
                        break;
                    case VisitSpot.Far:
                        c = FarCell(map, entry);
                        break;
                    case VisitSpot.Road:
                        if (!RCellFinder.TryFindRandomSpotJustOutsideColony(entry, map, out c)) c = IntVec3.Invalid;
                        break;
                }
                if (!c.IsValid) c = PlaySpots.GateCell(map, null);
                if (!c.IsValid) c = DropCellFinder.TradeDropSpot(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor spot", e);
            }
            return c.IsValid && c.InBounds(map) ? c : IntVec3.Invalid;
        }

        /// <summary>Open ground well away from the colony, not its own, that a herd can stand on.</summary>
        private static IntVec3 FarCell(Map map, IntVec3 entry)
        {
            if (RCellFinder.TryFindRandomCellOutsideColonyNearTheCenterOfTheMap(entry, map, 28f, out var c) && Open(c, map)) return c;
            return IntVec3.Invalid;
        }

        /// <summary>Dry ground beside the water nearest the colony.</summary>
        private static IntVec3 WaterSide(Map map)
        {
            var water = PlaySpots.WaterCell(map, null);
            if (!water.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(water, 6f, false))
                if (c.InBounds(map) && Open(c, map) && !(c.GetTerrain(map)?.IsWater ?? false)) return c;
            return water;
        }

        private static bool Open(IntVec3 c, Map map) =>
            c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetEdifice(map) == null && !(map.areaManager?.Home?[c] ?? false);

        /// <summary>The way off on the far side from <paramref name="entry"/>, reachable from the spot, or invalid (the nearest edge will do).</summary>
        public static IntVec3 FarEdge(Map map, IntVec3 entry, IntVec3 spot)
        {
            try
            {
                VisitorMath.Opposite(entry.x, entry.z, map.Size.x, map.Size.z, out int ox, out int oz);
                var target = new IntVec3(ox, 0, oz);
                var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
                if (CellFinder.TryFindRandomEdgeCellNearWith(target, 40f, map, c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(spot, c, PathEndMode.OnCell, tp), out var exit))
                    return exit;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor far edge", e);
            }
            return IntVec3.Invalid;
        }

        // ---------------- How they are ----------------

        /// <summary>The cold, the heat or hunger is getting to them: they should go while they still can.</summary>
        public static bool Suffering(Pawn p)
        {
            if (p == null || p.Dead || p.health?.hediffSet == null) return false;
            var hs = p.health.hediffSet;
            if ((hs.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f) >= 0.04f) return true;
            if ((hs.GetFirstHediffOfDef(HediffDefOf.Heatstroke)?.Severity ?? 0f) >= 0.04f) return true;
            if (hs.HasHediff(HediffDefOf.Malnutrition)) return true;
            return p.needs?.food != null && p.needs.food.Starving;
        }

        /// <summary>
        /// A corner inside for guests when a night out would hurt them: an open spot in the colony's dining room or rec
        /// room (never a cell, a bedroom or a hospital), warm enough for every one of them, that they can all reach.
        /// </summary>
        public static IntVec3 IndoorsCell(Map map, List<Pawn> party)
        {
            if (map == null || party.NullOrEmpty()) return IntVec3.Invalid;
            try
            {
                var rooms = RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).Concat(RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom))
                    .Where(r => r != null && !r.IsPrisonCell && !r.PsychologicallyOutdoors && r.CellCount >= 12).OrderByDescending(r => r.CellCount);
                foreach (var room in rooms)
                {
                    var middle = room.ExtentsClose.CenterCell;
                    foreach (var c in room.Cells.OrderBy(x => x.DistanceToSquared(middle)).Take(30))
                    {
                        if (!c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null) continue;
                        if (!SafeOvernight(party, c, map, 0f)) break;
                        if (party.All(p => p.CanReach(c, PathEndMode.OnCell, Danger.Some))) return c;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor indoors", e);
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// They eat what they brought, never the colony's food or its fields: someone hungry eats from their pack; with
        /// nothing left in it (and for their animals), they're kept fed out of sight, as travelers feed themselves.
        /// </summary>
        public static void Feed(VisitorTrip t)
        {
            foreach (var p in t.Here)
            {
                var food = p.needs?.food;
                if (food == null || food.CurLevelPercentage > 0.3f || p.Downed || !p.Awake() || p.CurJobDef == JobDefOf.Ingest) continue;
                try
                {
                    if (p.RaceProps.Humanlike)
                    {
                        var meal = p.inventory?.innerContainer?.FirstOrDefault(x => x.def.IsNutritionGivingIngestible && !x.def.IsDrug && FoodUtility.WillEat(p, x));
                        if (meal != null)
                        {
                            var job = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                            job.count = Math.Max(1, Math.Min(meal.stackCount, FoodUtility.WillIngestStackCountOf(p, meal.def, meal.GetStatValue(StatDefOf.Nutrition))));
                            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                            continue;
                        }
                    }
                    food.CurLevelPercentage = 0.8f;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor food", e);
                    food.CurLevelPercentage = 0.8f;
                }
            }
        }

        /// <summary>
        /// A night out by this spot won't hurt them: every one of them is safe in the temperature there, with
        /// <paramref name="margin"/> degrees for the night being colder than now (outdoors).
        /// </summary>
        public static bool SafeOvernight(IEnumerable<Pawn> party, IntVec3 at, Map map, float margin = 8f)
        {
            foreach (var p in party)
            {
                if (p == null || p.Dead || !p.Spawned) continue;
                try
                {
                    // The night will be colder than now: a few degrees of margin.
                    var range = GenTemperature.SafeTemperatureRange(p);
                    float t = at.IsValid && at.InBounds(map) ? at.GetTemperature(map) : map.mapTemperature.OutdoorTemp;
                    if (t - margin < range.min || t > range.max) return false;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor overnight", e);
                    return false;
                }
            }
            return true;
        }

        // ---------------- Letters ----------------

        /// <summary>The letter whose context this is (a world event's or a Rimfolk event's question), or null.</summary>
        public static Letter LetterOf(DilemmaContext ctx)
        {
            if (ctx == null) return null;
            foreach (var l in Find.LetterStack.LettersListForReading)
            {
                if (l is ChoiceLetter_Situation s && s.ctx == ctx) return l;
                if (l is ChoiceLetter_Happening h && h.ctx == ctx) return l;
            }
            return null;
        }

        /// <summary>A question waiting for one of these people (they're its X), found again after a reload.</summary>
        public static Letter LetterFor(VisitorTrip t)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
            {
                Pawn x = (l as ChoiceLetter_Situation)?.ctx?.stranger ?? (l as ChoiceLetter_Happening)?.ctx?.stranger;
                if (x != null && t.people.Contains(x) && Open(l)) return l;
            }
            return null;
        }

        private static bool Open(Letter l) =>
            l is ChoiceLetter_Situation s ? !s.resolved : l is ChoiceLetter_Happening h ? !h.resolved : false;

        /// <summary>The letter with this ID is still waiting for an answer.</summary>
        public static bool LetterOpen(int id)
        {
            if (id < 0) return false;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l.ID == id) return Open(l);
            return false;
        }

        /// <summary>
        /// They've gone before the colony answered: the letter no longer points at them, and what it sends goes the
        /// usual way (a courier comes for it).
        /// </summary>
        public static void Forget(VisitorTrip t)
        {
            if (t == null || t.letterId < 0) return;
            try
            {
                foreach (var l in Find.LetterStack.LettersListForReading)
                {
                    if (l.ID != t.letterId) continue;
                    var ctx = (l as ChoiceLetter_Situation)?.ctx ?? (DilemmaContext)(l as ChoiceLetter_Happening)?.ctx;
                    if (ctx?.stranger != null && t.people.Contains(ctx.stranger)) ctx.stranger = null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor forget", e);
            }
        }

        // ---------------- Errands ----------------

        /// <summary>A colonist is on the way out to one of them with something (an errand of PlayOut/).</summary>
        public static bool ErrandComing(VisitorTrip t)
        {
            var plays = PlayOutTracker.Instance?.plays;
            if (plays == null) return false;
            foreach (var p in plays)
                if (p != null && p.kind == PlayKind.Carry && p.state != PlayState.Done && p.other != null && t.people.Contains(p.other))
                    return true;
            return false;
        }

        // ---------------- Taken in ----------------

        /// <summary>
        /// The people at the gate for this question, if the colony takes them in (WorldEffect_Join): they become the
        /// colony's, with their animal, right where they stand, and the visit is over. Empty when nobody is waiting.
        /// </summary>
        public static List<Pawn> TakeIn(DilemmaContext ctx, bool withAnimals)
        {
            var result = new List<Pawn>();
            var tracker = VisitorTracker.Instance;
            var t = tracker?.WaitingFor(ctx);
            if (t == null) return result;
            var map = t.map;
            foreach (var p in t.people.ToList())
            {
                if (p == null || p.Dead || !p.Spawned || p.Map != map || p.Downed || p.IsPrisoner || p.Faction == Faction.OfPlayer) continue;
                if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) continue;
                if (Arrivals.Join(p, map)) result.Add(p);
            }
            if (result.Count == 0) return result;
            foreach (var a in t.animals.ToList())
            {
                if (a == null || a.Dead || !a.Spawned || a.Map != map) continue;
                // Their own animal comes with them; a beast of somebody's caravan goes home.
                if (!withAnimals || a.RaceProps.packAnimal && t.pack) continue;
                try
                {
                    a.GetLord()?.RemovePawn(a);
                    a.SetFaction(Faction.OfPlayer);
                    result.Add(a);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor animal joins", e);
                }
            }
            tracker.Joined(t, result);
            return result;
        }
    }
}
