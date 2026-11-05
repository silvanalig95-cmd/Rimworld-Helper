using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>The road a journey takes, as the game's caravans reckon it: how long, and the weather on the way.</summary>
    public sealed class JourneyRoute
    {
        /// <summary>The way there in ticks, by the game's caravan estimate (night rests included); -1 without a destination or a way.</summary>
        public int oneWayTicks = -1;
        public bool noWay;
        /// <summary>The weather of the road and of the destination over the days of the trip (NaN when unknown).</summary>
        public float low = float.NaN, high = float.NaN, destLow = float.NaN, destHigh = float.NaN;
        /// <summary>The share of the trip the land gives no grazing.</summary>
        public float noGrazing;
    }

    /// <summary>
    /// What a journey takes and how it leaves (the numbers are JourneyMath.cs): the road and its weather from the game's
    /// own caravan estimate and the land's reckoning of each hour; the packing list from the colony's stores (travel food
    /// first, medicine by length and people, bedrolls and any mod's tents for nights on the road, feed for animals that
    /// can't graze on the way); each traveller's change of clothes from the stores for the road's weather; their shares
    /// to carry; the edge of the map toward the destination and the spot at the colony's gate to gather at.
    /// </summary>
    public static class JourneyPack
    {
        // ---------------- The road ----------------

        private static float Longitude(Map map) => Find.WorldGrid.LongLatOf(map.Tile).x;

        /// <summary>
        /// The trip's road from home: its length (with a destination) and its weather over the days of the trip
        /// (<paramref name="chosenDays"/> when the player chose them, else the estimate's).
        /// </summary>
        public static JourneyRoute Route(Map map, PlanetTile dest, List<Pawn> pawns, float massUsage, int departAbs, bool andBack, float chosenDays)
        {
            var r = new JourneyRoute();
            if (map == null) return r;
            var temps = Find.World.tileTemperatures;
            var every = new List<(PlanetTile tile, int ticks)>();
            if (dest.Valid && dest != map.Tile)
            {
                WorldPath path = null;
                try
                {
                    int perMove = TicksPerMove(pawns, massUsage);
                    path = map.Tile.Layer.Pather.FindPath(map.Tile, dest, null);
                    if (path == null || !path.Found) r.noWay = true;
                    else
                    {
                        CaravanArrivalTimeEstimator.EstimatedTicksToArriveToEvery(map.Tile, dest, path, 0f, perMove, departAbs, every);
                        r.oneWayTicks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(dest, every);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journeys: the road", e);
                    r.noWay = true;
                }
                finally
                {
                    path?.Dispose();
                }
            }
            // The weather over the days chosen, else over the caravan's estimate of the trip, else a day.
            float days = chosenDays > 0f ? chosenDays : r.oneWayTicks > 0 ? JourneyMath.TripDays(r.oneWayTicks, andBack) : 1f;
            int span = Mathf.Max(GenDate.TicksPerHour, Mathf.RoundToInt(Mathf.Max(days, 0.25f) * GenDate.TicksPerDay));
            var road = new List<float>();
            var there = new List<float>();
            int samples = 0, bare = 0;
            for (int t = 0; t <= span; t += GenDate.TicksPerHour * 2)
            {
                var tile = map.Tile;
                bool atDest = false;
                if (r.oneWayTicks > 0 && every.Count > 0)
                {
                    float share = JourneyMath.WayShare(t, r.oneWayTicks, andBack);
                    atDest = share >= 0.999f;
                    tile = CaravanArrivalTimeEstimator.TileIllBeInAt(departAbs + Mathf.RoundToInt(share * r.oneWayTicks), every, departAbs);
                    if (!tile.Valid) tile = map.Tile;
                }
                int abs = departAbs + t;
                float temp = temps.OutdoorTemperatureAt(tile, abs);
                road.Add(temp);
                if (atDest) there.Add(temp);
                samples++;
                if (!VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsAt(tile, abs)) bare++;
            }
            if (r.oneWayTicks > 0) there.Add(temps.OutdoorTemperatureAt(dest, departAbs + r.oneWayTicks));
            (r.low, r.high) = JourneyMath.RouteRange(road);
            if (there.Count > 0)
            {
                r.destLow = there.Min();
                r.destHigh = there.Max();
            }
            r.noGrazing = samples > 0 ? bare / (float)samples : 0f;
            return r;
        }

        /// <summary>How fast the group moves on the world map, by the game's own reckoning of its pawns and load.</summary>
        public static int TicksPerMove(List<Pawn> pawns, float massUsage)
        {
            var list = pawns?.Where(p => p != null && !p.Dead).ToList() ?? new List<Pawn>();
            if (list.Count == 0) return 3300;
            float capacity = list.Sum(p => MassUtility.Capacity(p));
            return CaravanTicksPerMoveUtility.GetTicksPerMove(list, massUsage, Mathf.Max(capacity, 1f));
        }

        /// <summary>What the travellers already carry plus what the packing list adds to it, in kilograms.</summary>
        public static float Mass(IEnumerable<Pawn> travellers, IEnumerable<JourneyLine> packing)
        {
            var people = travellers.Where(p => p?.inventory != null).ToList();
            float m = people.Sum(p => MassUtility.InventoryMass(p));
            foreach (var l in packing)
                if (l != null && l.Taken)
                    m += Mathf.Max(0, l.count - people.Sum(p => Carried(p, l.def))) * UnitMass(l.def);
            return m;
        }

        private static readonly Dictionary<ThingDef, float> massOf = new Dictionary<ThingDef, float>(), nutritionOf = new Dictionary<ThingDef, float>();

        /// <summary>The weight of one of a line's things (a packed-up building as made of its usual stuff), remembered.</summary>
        public static float UnitMass(ThingDef def)
        {
            if (!massOf.TryGetValue(def, out float m))
                massOf[def] = m = def.MadeFromStuff ? def.GetStatValueAbstract(StatDefOf.Mass, GenStuff.DefaultStuffFor(def)) : def.GetStatValueAbstract(StatDefOf.Mass);
            return m;
        }

        /// <summary>One of a thing's nourishment (none for what isn't food), remembered.</summary>
        public static float Nutrition(ThingDef def)
        {
            if (def == null) return 0f;
            if (!nutritionOf.TryGetValue(def, out float n))
                nutritionOf[def] = n = def.IsNutritionGivingIngestible ? def.GetStatValueAbstract(StatDefOf.Nutrition) : 0f;
            return n;
        }

        /// <summary>Days before it starts to rot outside a freezer; <see cref="float.PositiveInfinity"/> when it never does.</summary>
        public static float RotDays(ThingDef def)
        {
            var rot = def?.GetCompProperties<CompProperties_Rottable>();
            return rot == null ? float.PositiveInfinity : rot.daysToRotStart;
        }

        /// <summary>How many sleep on one of a line's beds (a double bed two).</summary>
        public static int Sleeps(ThingDef def) => Mathf.Max(1, def?.size.x ?? 1);

        /// <summary>
        /// What lies on the colony's stalls (RimMarket), thing by thing, for the planner's amounts beside the stores: each
        /// stall with how many it holds. Empty without RimMarket.
        /// </summary>
        public static Dictionary<ThingDef, List<(Thing stall, int count)>> StallHoldings(Map map)
        {
            try
            {
                return RimMarketStall.JourneyReady ? RimMarketStall.JourneyStock(map) : new Dictionary<ThingDef, List<(Thing, int)>>();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the stalls' goods", e);
                return new Dictionary<ThingDef, List<(Thing, int)>>();
            }
        }

        /// <summary>
        /// The kind of line a thing makes when the player adds it: a bed caravans can use, a mod's tent, medicine, a drug,
        /// food the travellers can eat, feed only animals eat, or anything else.
        /// </summary>
        public static string KindFor(ThingDef def)
        {
            if (def == null) return "other";
            if (def.building != null && def.building.bed_caravansCanUse) return "bed";
            if (IsTent(def)) return "tent";
            if (def.IsMedicine) return "medicine";
            if (def.IsDrug) return "drug";
            if (def.IsNutritionGivingIngestible && !def.IsDrug && !def.IsCorpse && def.ingestible != null)
                return def.ingestible.HumanEdible ? "food" : "feed";
            return "other";
        }

        /// <summary>The travellers' nourishment a day between them, by the game's own hunger rates.</summary>
        public static float PeoplePerDay(IEnumerable<Pawn> travellers) => travellers.Where(p => p != null && !p.Dead).Sum(NutritionPerDay);

        /// <summary>
        /// Each animal's nourishment a day and the days it needs feed for: the days the land gives no grazing for one
        /// that grazes, every day for one that doesn't.
        /// </summary>
        public static List<(float perDay, float feedDays)> FeedNeeds(IEnumerable<Pawn> animals, float days, JourneyRoute route)
        {
            var list = new List<(float, float)>();
            foreach (var a in animals.Where(a => a != null && !a.Dead && a.needs?.food != null))
            {
                bool grazes = a.RaceProps.Eats(FoodTypeFlags.Plant);
                list.Add((NutritionPerDay(a), days * (grazes ? route?.noGrazing ?? 0f : 1f)));
            }
            return list;
        }

        // ---------------- The stores ----------------

        /// <summary>Things of the colony's stores the travellers may take: spawned, in storage, not forbidden.</summary>
        private static IEnumerable<Thing> Stored(Map map, ThingRequestGroup group)
        {
            foreach (var t in map.listerThings.ThingsInGroup(group))
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    yield return t;
        }

        /// <summary>A thing counts toward a line: the line's def, or a packed-up building of it (a bedroll, a tent).</summary>
        public static bool Matches(Thing t, ThingDef def) => t != null && (t.def == def || (t is MinifiedThing m && m.InnerThing?.def == def));

        /// <summary>How many of a line's things are in the stores.</summary>
        public static int InStores(Map map, ThingDef def)
        {
            if (map == null || def == null) return 0;
            if (def.Minifiable) return Stored(map, ThingRequestGroup.MinifiedThing).Where(t => Matches(t, def)).Sum(t => t.stackCount);
            int n = 0;
            foreach (var t in map.listerThings.ThingsOfDef(def))
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()) n += t.stackCount;
            return n;
        }

        /// <summary>How many of a line's things a pawn carries.</summary>
        public static int Carried(Pawn p, ThingDef def)
        {
            var inv = p?.inventory?.innerContainer;
            if (inv == null || def == null) return 0;
            int n = 0;
            for (int i = 0; i < inv.Count; i++)
                if (Matches(inv[i], def)) n += inv[i].stackCount;
            return n;
        }

        /// <summary>A traveller's or animal's nourishment a day, fed, by the game's own hunger rate.</summary>
        public static float NutritionPerDay(Pawn p)
        {
            var food = p?.needs?.food;
            if (food == null) return 0f;
            return food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * GenDate.TicksPerDay;
        }

        private static readonly Regex TentWord = new Regex(@"\btents?\b", RegexOptions.IgnoreCase);
        private static readonly Regex TentName = new Regex(@"(^|[^A-Za-z]|(?<=[a-z]))Tent");

        /// <summary>A mod's tent, by name: a def called a tent (CampingTent, VFE_Tent, "small tent").</summary>
        public static bool IsTent(ThingDef d) => d != null && !d.IsApparel && !d.IsWeapon && (TentName.IsMatch(d.defName) || TentWord.IsMatch(d.label ?? ""));

        /// <summary>
        /// The packing list for a trip: food for everyone's days with a margin (travel food first), medicine by length
        /// and people, a bedroll each and tents for nights on the road, feed for animals where they can't graze. The
        /// player's lines stay as they are, in the order they had: a number they set stands as set, a line they added
        /// stays, and a suggestion they took off stays off while the planner makes up its share from other things. Only
        /// what the stores have, and what the travellers carry already (their own pemmican, or their share picked up when
        /// a plan is changed in its last hours).
        /// </summary>
        public static List<JourneyLine> Packing(Map map, List<Pawn> travellers, List<Pawn> animals, float days, JourneyRoute route, List<JourneyLine> old)
        {
            var lines = new List<JourneyLine>();
            if (map == null) return lines;
            var people = travellers.Where(p => p != null && !p.Dead).ToList();
            int stayHome = map.mapPawns.FreeColonistsSpawned.Count(p => !people.Contains(p));
            var carried = people.Where(p => p.inventory != null).SelectMany(p => p.inventory.innerContainer).ToList();
            // The player's lines, and the things they took off (the planner doesn't suggest those again).
            var kept = old?.Where(o => o?.def != null && o.Player).Select(o => o.Copy()).ToList() ?? new List<JourneyLine>();
            var off = new HashSet<ThingDef>(kept.Where(o => o.removed).Select(o => o.def));
            try
            {
                // Food: everyone's nutrition a day, with the margin, from the best travel food in the stores.
                float need = JourneyMath.FoodNutrition(PeoplePerDay(people), days);
                var foods = new List<JourneyMath.FoodOption>();
                var defs = new List<ThingDef>();
                foreach (var g in Stored(map, ThingRequestGroup.FoodSourceNotPlantOrTree).Concat(carried).Where(HumanTravelFood).Where(t => !off.Contains(t.def)).GroupBy(t => t.def))
                {
                    var d = g.Key;
                    defs.Add(d);
                    foods.Add(new JourneyMath.FoodOption
                    {
                        nutrition = Nutrition(d), mass = d.GetStatValueAbstract(StatDefOf.Mass), rotDays = RotDays(d), preferability = (int)d.ingestible.preferability,
                        travel = d == ThingDefOf.MealSurvivalPack ? 2 : d == ThingDefOf.Pemmican ? 1 : 0, stock = g.Sum(t => t.stackCount)
                    });
                }
                foreach (var (i, units) in JourneyMath.PackFood(foods, days, need)) lines.Add(new JourneyLine(defs[i], units, "food"));

                // Medicine: by people and days, the plainer kinds first, never more than half the stores while anyone stays home.
                var meds = Stored(map, ThingRequestGroup.Medicine).Concat(carried.Where(t => t.def.IsMedicine)).GroupBy(t => t.def)
                    .Select(g => (def: g.Key, stock: g.Sum(t => t.stackCount), potency: g.Key.GetStatValueAbstract(StatDefOf.MedicalPotency)))
                    .OrderBy(m => m.potency >= 1.5f ? 1 : 0).ThenByDescending(m => m.potency).ToList();
                int medWant = JourneyMath.MedicineTaken(JourneyMath.Medicine(people.Count, days), meds.Sum(m => m.stock), stayHome);
                foreach (var m in meds.Where(m => !off.Contains(m.def)))
                {
                    if (medWant <= 0) break;
                    int take = Mathf.Min(medWant, m.stock);
                    lines.Add(new JourneyLine(m.def, take, "medicine"));
                    medWant -= take;
                }

                // Drugs for the travellers' addictions and chemical dependencies, for the days away with the margin.
                lines.AddRange(Drugs(map, people, days, carried, off));

                // A sleeping place each for nights on the road: packed-up beds caravans can use, a double counting two.
                int beds = JourneyMath.Bedrolls(people.Count, days);
                var packed = Stored(map, ThingRequestGroup.MinifiedThing).Concat(carried).OfType<MinifiedThing>()
                    .Where(m => m.InnerThing?.def?.building != null && m.InnerThing.def.building.bed_caravansCanUse && !off.Contains(m.InnerThing.def)).GroupBy(m => m.InnerThing.def)
                    .OrderBy(g => g.Key.size.x).ToList();
                foreach (var g in packed)
                {
                    if (beds <= 0) break;
                    int slots = Sleeps(g.Key);
                    int take = Mathf.Min(g.Sum(m => m.stackCount), Mathf.CeilToInt(beds / (float)slots));
                    if (take <= 0) continue;
                    lines.Add(new JourneyLine(g.Key, take, "bed"));
                    beds -= take * slots;
                }

                // A mod's tents, by name, one for every two.
                int tents = JourneyMath.Tents(people.Count, days);
                if (tents > 0)
                {
                    var tentDefs = Stored(map, ThingRequestGroup.MinifiedThing).OfType<MinifiedThing>().Where(m => IsTent(m.InnerThing?.def)).Select(m => m.InnerThing.def)
                        .Concat(map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item && !(t is MinifiedThing) && IsTent(t.def) && t.Spawned
                                                                         && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()).Select(t => t.def))
                        .Where(d => !off.Contains(d)).Distinct().ToList();
                    foreach (var d in tentDefs)
                    {
                        if (tents <= 0) break;
                        int take = Mathf.Min(tents, InStores(map, d));
                        if (take <= 0) continue;
                        lines.Add(new JourneyLine(d, take, "tent"));
                        tents -= take;
                    }
                }

                // Feed for the animals: kibble, else hay, for the days they can't graze (every day for those that don't graze
                // at all). Kibble taken off the list: hay for those that eat it.
                bool kibbleOff = off.Contains(ThingDefOf.Kibble), hayOff = off.Contains(ThingDefOf.Hay);
                float kibble = 0f, hay = 0f;
                foreach (var a in animals.Where(a => a != null && !a.Dead && a.needs?.food != null))
                {
                    bool grazes = a.RaceProps.Eats(FoodTypeFlags.Plant);
                    float feedDays = days * (grazes ? route?.noGrazing ?? 0f : 1f);
                    if (feedDays <= 0f) continue;
                    float n = JourneyMath.FoodNutrition(NutritionPerDay(a), feedDays);
                    if (!kibbleOff && a.RaceProps.CanEverEat(ThingDefOf.Kibble)) kibble += n;
                    else if (!hayOff && a.RaceProps.CanEverEat(ThingDefOf.Hay)) hay += n;
                }
                foreach (var (d, n) in new[] { (ThingDefOf.Kibble, kibble), (ThingDefOf.Hay, hay) })
                {
                    if (n <= 0f) continue;
                    int take = Mathf.Min(InStores(map, d), JourneyMath.Units(n, Nutrition(d)));
                    if (take > 0) lines.Add(new JourneyLine(d, take, "feed"));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the packing list", e);
            }
            // What the player set stays set: their number on the planner's line, or their own line.
            foreach (var o in kept)
            {
                var same = lines.FirstOrDefault(l => l.def == o.def && l.kind == o.kind && !l.Player);
                if (same == null) lines.Add(o);
                else
                {
                    same.count = o.count;
                    same.edited = o.edited;
                    same.added = o.added;
                }
            }
            // In the order the list had, so a line doesn't move under the player's hand; new ones after.
            var order = new Dictionary<(ThingDef, string), int>();
            for (int i = 0; i < (old?.Count ?? 0); i++)
                if (old[i]?.def != null && !order.ContainsKey((old[i].def, old[i].kind))) order[(old[i].def, old[i].kind)] = i;
            return lines.OrderBy(l => order.TryGetValue((l.def, l.kind), out int i) ? i : int.MaxValue).ToList();
        }

        /// <summary>
        /// Drugs for the travellers' addictions and chemical dependencies, for the trip with its margin. An addiction: the
        /// craving falls by its rate a day and a dose fills it by the drug's own amount (the caravan gives a dose when the
        /// craving runs low). A chemical dependency (Biotech's genes): a dose each time it comes due. For each chemical the
        /// drug their drug policy allows for an addiction comes first, then the cheapest; only what the stores have or the
        /// travellers carry already.
        /// </summary>
        public static List<JourneyLine> Drugs(Map map, List<Pawn> people, float days, List<Thing> carried, HashSet<ThingDef> off)
        {
            var lines = new List<JourneyLine>();
            float span = JourneyMath.MarginDays(days);
            if (span <= 0f) return lines;
            // Per chemical: the craving to cover (in the need's own amount), the dependency doses, and who needs it.
            var want = new Dictionary<ChemicalDef, (float craving, int doses, List<Pawn> who)>();
            void Want(ChemicalDef chem, Pawn p, float craving, int doses)
            {
                if (chem == null) return;
                if (!want.TryGetValue(chem, out var w)) w = (0f, 0, new List<Pawn>());
                w.craving += craving;
                w.doses += doses;
                if (!w.who.Contains(p)) w.who.Add(p);
                want[chem] = w;
            }
            foreach (var p in people)
            {
                var hediffs = p.health?.hediffSet?.hediffs;
                if (hediffs != null)
                    foreach (var a in hediffs.OfType<Hediff_Addiction>())
                        Want(a.Chemical, p, span * (a.Need?.def?.fallPerDay ?? 0f), 0);
                var genes = p.genes?.GenesListForReading;
                if (genes != null)
                    foreach (var g in genes.OfType<Gene_ChemicalDependency>())
                    {
                        if (!g.Active || g.def?.chemical == null) continue;
                        float since = (Find.TickManager.TicksGame - g.lastIngestedTick) / 60000f;
                        Want(g.def.chemical, p, 0f, JourneyMath.DependencyDoses(span, since, DependencyDays(g.LinkedHediff)));
                    }
            }
            if (want.Count == 0) return lines;
            var drugs = Stored(map, ThingRequestGroup.Drug).Concat(carried.Where(t => t.def.IsDrug)).GroupBy(t => t.def).ToList();
            foreach (var kv in want)
            {
                var chem = kv.Key;
                var (craving, doses, who) = kv.Value;
                var best = drugs.Where(g => !off.Contains(g.Key) && g.Key.GetCompProperties<CompProperties_Drug>()?.chemical == chem)
                    .OrderByDescending(g => who.Count(p => p.drugs?.CurrentPolicy?[g.Key]?.allowedForAddiction ?? true))
                    .ThenBy(g => g.Key.BaseMarketValue).FirstOrDefault();
                if (best == null) continue;
                float perDose = best.Key.GetCompProperties<CompProperties_Drug>()?.needLevelOffset ?? 1f;
                int need = doses + (craving > 0f ? JourneyMath.AddictionDoses(1f, craving, perDose) : 0);
                int take = Mathf.Min(need, best.Sum(t => t.stackCount));
                if (take > 0) lines.Add(new JourneyLine(best.Key, take, "drug"));
            }
            return lines;
        }

        /// <summary>
        /// How many days a chemical dependency goes between doses: its first stage of want, by the rate its severity rises
        /// (a day a day when the hediff counts days itself). Five days when the hediff says nothing usable.
        /// </summary>
        public static float DependencyDays(Hediff dependency)
        {
            var def = dependency?.def;
            float first = def?.stages?.Where(s => s.minSeverity > 0f).Select(s => s.minSeverity).DefaultIfEmpty(0f).Min() ?? 0f;
            float rate = def?.CompProps<HediffCompProperties_SeverityPerDay>()?.severityPerDay ?? 1f;
            if (first <= 0f || rate <= 0f) return 5f;
            return Mathf.Clamp(first / rate, 1f, 30f);
        }

        /// <summary>
        /// What the stores hold and the travellers carry, thing by thing, for the planner's "Add from the stores": in
        /// storage on the map, not forbidden or in the dark; a packed-up building counts as what it is.
        /// </summary>
        public static Dictionary<ThingDef, (int stores, int carried)> Holdings(Map map, IEnumerable<Pawn> travellers)
        {
            var held = new Dictionary<ThingDef, (int stores, int carried)>();
            if (map == null) return held;
            void Add(Thing t, bool inStores)
            {
                var def = t is MinifiedThing m ? m.InnerThing?.def : t?.def;
                if (def == null || def.IsCorpse || t is Pawn) return;
                held.TryGetValue(def, out var h);
                if (inStores) h.stores += t.stackCount;
                else h.carried += t.stackCount;
                held[def] = h;
            }
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (t.Spawned && !t.Position.Fogged(map) && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    Add(t, true);
            foreach (var p in travellers)
            {
                var inv = p?.inventory?.innerContainer;
                if (inv == null) continue;
                for (int i = 0; i < inv.Count; i++) Add(inv[i], false);
            }
            return held;
        }

        /// <summary>Food a person can take on the road: their kind of food, no drug, nothing raw and nasty, no corpse.</summary>
        private static bool HumanTravelFood(Thing t)
        {
            var d = t.def;
            var ing = d.ingestible;
            if (ing == null || !d.IsNutritionGivingIngestible || d.IsDrug || d.IsCorpse || t is Pawn || !ing.HumanEdible) return false;
            return ing.preferability >= FoodPreferability.RawTasty && ing.preferability <= FoodPreferability.MealLavish;
        }

        // ---------------- Each traveller's share ----------------

        /// <summary>The kinds of line the travellers pack themselves; feed and anything else go with the caravan's goods.</summary>
        public static bool OwnPack(string kind) => kind == "food" || kind == "medicine" || kind == "bed" || kind == "tent";

        /// <summary>
        /// What each traveller carries themselves: food in proportion to what they eat, medicine shared out, a sleeping
        /// place and a tent in turn, as much as they can carry; the rest (the animals' feed and anything else on the
        /// list) goes with the caravan's own loading at the hour, onto the pack animals first.
        /// </summary>
        public static Dictionary<Pawn, Dictionary<ThingDef, int>> Shares(JourneyPlan plan)
        {
            var shares = new Dictionary<Pawn, Dictionary<ThingDef, int>>();
            var people = plan.travellers.Where(p => p != null && !p.Dead && p.inventory != null).ToList();
            if (people.Count == 0) return shares;
            var room = new Dictionary<Pawn, float>();
            foreach (var p in people)
            {
                shares[p] = new Dictionary<ThingDef, int>();
                room[p] = Mathf.Max(0f, MassUtility.FreeSpace(p));
            }
            float eats = people.Sum(NutritionPerDay);
            int turn = 0;
            foreach (var line in plan.packing.Where(l => l.Taken && OwnPack(l.kind)))
            {
                float unit = Mathf.Max(0.001f, UnitMass(line.def));
                int left = line.count;
                for (int i = 0; i < people.Count && left > 0; i++)
                {
                    var p = people[(turn + i) % people.Count];
                    int want;
                    if (line.kind == "food") want = eats > 0f ? Mathf.CeilToInt(line.count * NutritionPerDay(p) / eats) : Mathf.CeilToInt(line.count / (float)people.Count);
                    else if (line.kind == "medicine") want = Mathf.CeilToInt(line.count / (float)people.Count);
                    else want = 1;
                    want = Mathf.Min(want, left, Mathf.FloorToInt(room[p] / unit));
                    if (want <= 0) continue;
                    shares[p][line.def] = (shares[p].TryGetValue(line.def, out int had) ? had : 0) + want;
                    room[p] -= want * unit;
                    left -= want;
                }
                // Beds and tents in turn, so not all to the first.
                if (line.kind == "bed" || line.kind == "tent") turn += line.count;
            }
            return shares;
        }

        // ---------------- Dressed for the road ----------------

        /// <summary>Clothes in the stores anyone could put on: in storage, not forbidden, not off a corpse, in good repair.</summary>
        private static List<Apparel> StoredClothes(Map map) =>
            Stored(map, ThingRequestGroup.Apparel).OfType<Apparel>().Where(a => !a.WornByCorpse && a.HitPoints >= a.MaxHitPoints / 2).ToList();

        /// <summary>Kept on whatever the weather: locked on, forced by the player, or armour.</summary>
        private static bool Keeps(Pawn p, Apparel a) =>
            p.apparel.IsLocked(a) || (p.outfits?.forcedHandler?.IsForced(a) ?? false) || a.GetStatValue(StatDefOf.ArmorRating_Sharp) >= 0.4f;

        /// <summary>
        /// What each traveller changes into from the stores for the road's weather, the one worst off first, nobody's
        /// piece given twice. Only with "Dressed for the road" on and the weather worked out.
        /// </summary>
        public static Dictionary<Pawn, List<Apparel>> Clothes(JourneyPlan plan)
        {
            var result = new Dictionary<Pawn, List<Apparel>>();
            if (!(ColonyCouncilMod.Settings?.journeyClothes ?? true) || plan.map == null || float.IsNaN(plan.low)) return result;
            try
            {
                var stores = StoredClothes(plan.map);
                var taken = new HashSet<Apparel>();
                var people = plan.travellers.Where(p => p?.apparel != null && p.Spawned && p.RaceProps.Humanlike).ToList();
                foreach (var p in people.OrderByDescending(p =>
                         {
                             var c = p.ComfortableTemperatureRange();
                             return CampWeatherMath.Shortfall(c.min, c.max, plan.low, plan.high);
                         }))
                {
                    var picks = Dress(p, stores, taken, plan.low, plan.high);
                    foreach (var a in picks) taken.Add(a);
                    if (picks.Count > 0) result[p] = picks;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: clothes for the road", e);
            }
            return result;
        }

        private static List<Apparel> Dress(Pawn p, List<Apparel> stores, HashSet<Apparel> taken, float low, float high)
        {
            var body = p.RaceProps.body;
            var worn = p.apparel.WornApparel.ToList();
            var fits = stores.Where(a => !taken.Contains(a) && a.def.apparel != null && ApparelUtility.HasPartsToWear(p, a.def) && EquipmentUtility.CanEquip(a, p)
                                         && a.def.apparel.PawnCanWear(p) && p.CanReserveAndReach(a, PathEndMode.ClosestTouch, Danger.Some))
                .ToList();
            if (fits.Count == 0) return new List<Apparel>();
            var all = worn.Concat(fits).ToList();
            var items = all.Select((a, i) => new JourneyMath.Garment
            {
                cold = a.GetStatValue(StatDefOf.Insulation_Cold), heat = a.GetStatValue(StatDefOf.Insulation_Heat), removable = i >= worn.Count || !Keeps(p, a)
            }).ToList();
            var comfy = p.ComfortableTemperatureRange();
            var picks = JourneyMath.Dress(comfy.min, comfy.max, items, worn.Count, (a, b) => !ApparelUtility.CanWearTogether(all[a].def, all[b].def, body), low, high);
            return picks.Select(i => all[i]).ToList();
        }

        /// <summary>The comfortable range a traveller would have in what they wear plus these pieces (those they replace off).</summary>
        public static FloatRange ComfyWith(Pawn p, List<Apparel> picks)
        {
            var comfy = p.ComfortableTemperatureRange();
            if (picks == null || picks.Count == 0) return comfy;
            var body = p.RaceProps.body;
            var worn = p.apparel.WornApparel.ToList();
            float min = comfy.min, max = comfy.max;
            foreach (var w in worn.Where(w => picks.Any(n => !ApparelUtility.CanWearTogether(w.def, n.def, body))))
            {
                min += w.GetStatValue(StatDefOf.Insulation_Cold);
                max -= w.GetStatValue(StatDefOf.Insulation_Heat);
            }
            foreach (var n in picks)
            {
                min -= n.GetStatValue(StatDefOf.Insulation_Cold);
                max += n.GetStatValue(StatDefOf.Insulation_Heat);
            }
            return new FloatRange(min, max);
        }

        // ---------------- The way out ----------------

        /// <summary>The neighbouring tile they leave toward: the first step toward the destination, else one the game picks once.</summary>
        public static PlanetTile StartTile(JourneyPlan plan)
        {
            var map = plan.map;
            if (plan.HasDestination)
            {
                try
                {
                    var t = CaravanExitMapUtility.BestExitTileToGoTo(plan.DestTile, map);
                    if (t.Valid) return t;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journeys: the way toward the destination", e);
                }
            }
            if (plan.startTile < 0) plan.startTile = CaravanExitMapUtility.RandomBestExitTileFrom(map);
            return new PlanetTile(plan.startTile, map.Tile.Layer);
        }

        /// <summary>The middle of the colony: the centre of its home area, else of the map.</summary>
        public static IntVec3 Home(Map map)
        {
            var cells = map.areaManager.Home.ActiveCells.ToList();
            if (cells.Count == 0) return map.Center;
            var c = new IntVec3((int)cells.Average(x => x.x), 0, (int)cells.Average(x => x.z));
            if (c.Standable(map)) return c;
            return cells.OrderBy(x => x.DistanceToSquared(c)).FirstOrDefault(x => x.Standable(map));
        }

        /// <summary>
        /// The cell of the map's edge to leave by: on the edge facing the way (then the next nearest), reachable by every
        /// traveller, the nearest to home, a road out preferred when not much further.
        /// </summary>
        public static IntVec3 Exit(JourneyPlan plan)
        {
            var map = plan.map;
            var start = StartTile(plan);
            var order = new List<Rot4>();
            try
            {
                CaravanExitMapUtility.GetExitMapEdges(Find.WorldGrid, map.Tile, start, out var primary, out var secondary);
                if (primary.IsValid) order.Add(primary);
                if (secondary.IsValid) order.Add(secondary);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the map's edge toward the way", e);
            }
            var home = Home(map);
            var dir = Find.WorldGrid.GetTileCenter(start) - Find.WorldGrid.GetTileCenter(map.Tile);
            foreach (int r in JourneyMath.EdgeOrder(dir.x, dir.z)) if (!order.Contains(new Rot4(r))) order.Add(new Rot4(r));
            var walkers = plan.travellers.Where(p => p != null && p.Spawned && p.Map == map && !p.Downed).ToList();
            foreach (var rot in order)
            {
                var cells = CellRect.WholeMap(map).GetEdgeCells(rot).Where(c => c.Walkable(map) && !c.Fogged(map)).ToList();
                var tuples = cells.Select(c => (c.x, c.z, map.roadInfo.roadEdgeTiles.Contains(c) || c.GetTerrain(map).IsRoad)).ToList();
                // The best first, then the next best, until one every traveller can reach.
                var tried = new HashSet<int>();
                for (int k = 0; k < 12 && tried.Count < tuples.Count; k++)
                {
                    var rest = Enumerable.Range(0, tuples.Count).Where(i => !tried.Contains(i)).ToList();
                    int pick = JourneyMath.BestEdgeCell(rest.Select(i => tuples[i]).ToList(), home.x, home.z);
                    if (pick < 0) break;
                    int idx = rest[pick];
                    tried.Add(idx);
                    var cell = cells[idx];
                    bool reach = walkers.Count == 0 ? map.reachability.CanReach(home, cell, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors))
                        : walkers.All(p => p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly));
                    if (reach) return cell;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// Where they gather: the colony's caravan packing spot nearest the way out if it has one, else a few steps past the
        /// colony's last cell on the way from home to the exit (the gate), else somewhere just outside the colony.
        /// </summary>
        public static IntVec3 Gate(JourneyPlan plan, IntVec3 exit)
        {
            var map = plan.map;
            if (!exit.IsValid) return IntVec3.Invalid;
            var trav = TraverseParms.For(TraverseMode.PassDoors);
            var spot = map.listerThings.ThingsOfDef(ThingDefOf.CaravanPackingSpot).Where(t => t.Faction == Faction.OfPlayer && map.reachability.CanReach(exit, t, PathEndMode.OnCell, trav))
                .OrderBy(t => t.Position.DistanceToSquared(exit)).FirstOrDefault();
            if (spot != null) return spot.Position;
            var home = Home(map);
            var line = JourneyMath.Line(home.x, home.z, exit.x, exit.z).Select(c => new IntVec3(c.x, 0, c.z)).ToList();
            var area = map.areaManager.Home;
            var inside = line.Select(c => c.InBounds(map) && (area[c] || (c.GetRoom(map)?.PsychologicallyOutdoors == false))).ToList();
            var open = line.Select(c => c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && map.reachability.CanReach(exit, c, PathEndMode.OnCell, trav)).ToList();
            int i = JourneyMath.GateIndex(inside, open);
            if (i >= 0) return line[i];
            return RCellFinder.TryFindRandomSpotJustOutsideColony(exit, map, out var outside) ? outside : home;
        }

        /// <summary>A cell near the gate for someone to stand at.</summary>
        public static IntVec3 StandNear(Pawn p, IntVec3 gate)
        {
            if (!gate.IsValid || p?.Map == null) return IntVec3.Invalid;
            if (CellFinder.TryFindRandomReachableNearbyCell(gate, p.Map, 3f, TraverseParms.For(p), c => c.Standable(p.Map) && !c.IsForbidden(p), null, out var cell))
                return cell;
            return gate;
        }

        // ---------------- The caravan's own loading ----------------

        /// <summary>
        /// What the travellers don't carry yet of the packing list (every kind: the rest of their food, the feed, and
        /// whatever else was added from the stores), for the game's own caravan forming to load (onto the pack animals
        /// first). Short lines are named in <paramref name="shortOf"/>.
        /// </summary>
        public static List<TransferableOneWay> Missing(JourneyPlan plan, List<Pawn> going, List<string> shortOf)
        {
            var list = new List<TransferableOneWay>();
            var map = plan.map;
            foreach (var line in plan.packing.Where(l => l.Taken))
            {
                // A weapon taken up by one who had none counts as packed.
                int left = line.count - going.Sum(p => Carried(p, line.def)) - (line.kind == "arm" ? JourneyRun.Armed(plan, going, line.def) : 0);
                if (left <= 0) continue;
                var things = (line.def.Minifiable ? Stored(map, ThingRequestGroup.MinifiedThing) : map.listerThings.ThingsOfDef(line.def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)))
                    .Where(t => Matches(t, line.def) && map.reachability.CanReach(plan.gate.IsValid ? plan.gate : Home(map), t, PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.PassDoors)))
                    .OrderBy(t => plan.gate.IsValid ? t.Position.DistanceToSquared(plan.gate) : 0).ToList();
                foreach (var t in things)
                {
                    if (left <= 0) break;
                    var tr = new TransferableOneWay();
                    tr.things.Add(t);
                    int n = Mathf.Min(left, t.stackCount);
                    tr.ForceTo(n);
                    list.Add(tr);
                    left -= n;
                }
                if (left > 0) shortOf.Add(left + " " + (left == 1 ? line.def.label : Find.ActiveLanguageWorker.Pluralize(line.def.label, left)));
            }
            return list;
        }

        // ---------------- Dubs Bad Hygiene, by name ----------------

        private static bool dbhLooked;
        private static MethodInfo findToilet, findWash;
        private static JobDef useToilet, takeBath, takeShower, useWashBucket, washAtCell;

        private static void LookUpHygiene()
        {
            if (dbhLooked) return;
            dbhLooked = true;
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly("DubsBadHygiene.ClosestSanitation");
                if (type == null) return;
                findToilet = type.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "FindBestToilet" && m.GetParameters().Length >= 2
                    && m.GetParameters()[0].ParameterType == typeof(Pawn) && m.ReturnType == typeof(LocalTargetInfo));
                findWash = type.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "FindBestHygieneSource" && m.GetParameters().Length >= 2
                    && m.GetParameters()[0].ParameterType == typeof(Pawn) && m.ReturnType == typeof(LocalTargetInfo));
                useToilet = DefDatabase<JobDef>.GetNamedSilentFail("UseToilet");
                takeBath = DefDatabase<JobDef>.GetNamedSilentFail("takeBath");
                takeShower = DefDatabase<JobDef>.GetNamedSilentFail("takeShower");
                useWashBucket = DefDatabase<JobDef>.GetNamedSilentFail("useWashBucket");
                washAtCell = DefDatabase<JobDef>.GetNamedSilentFail("washAtCell");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: reading Dubs Bad Hygiene", e);
                findToilet = findWash = null;
            }
        }

        private static LocalTargetInfo Locate(MethodInfo m, Pawn p, float range)
        {
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            args[0] = p;
            for (int i = 1; i < ps.Length; i++)
            {
                var t = ps[i].ParameterType;
                args[i] = t == typeof(bool) ? false : t == typeof(float) ? range : ps[i].HasDefaultValue ? ps[i].DefaultValue : (t.IsValueType ? Activator.CreateInstance(t) : null);
            }
            return m.Invoke(null, args) is LocalTargetInfo target ? target : LocalTargetInfo.Invalid;
        }

        /// <summary>With Dubs Bad Hygiene: the bathroom before the road, the mod's own job at its best toilet. Null without it or a toilet.</summary>
        public static Job ToiletJob(Pawn p)
        {
            LookUpHygiene();
            if (findToilet == null || useToilet == null || Hygiene.BladderNeed == null || p.needs?.TryGetNeed(Hygiene.BladderNeed) == null) return null;
            try
            {
                var t = Locate(findToilet, p, 9999f);
                return t.IsValid && t.HasThing ? JobMaker.MakeJob(useToilet, t) : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: a toilet", e);
                return null;
            }
        }

        /// <summary>With Dubs Bad Hygiene: a wash before the road, at the mod's best bath, shower, tub or water. Null without it.</summary>
        public static Job WashJob(Pawn p)
        {
            LookUpHygiene();
            if (findWash == null || Hygiene.HygieneNeed == null || p.needs?.TryGetNeed(Hygiene.HygieneNeed) == null) return null;
            try
            {
                var t = Locate(findWash, p, 9999f);
                if (!t.IsValid) return null;
                if (!t.HasThing) return washAtCell != null ? JobMaker.MakeJob(washAtCell, t.Cell) : null;
                string kind = null;
                for (var c = t.Thing.def.thingClass; c != null && c != typeof(object); c = c.BaseType)
                    if (c.Name == "Building_bath" || c.Name == "Building_shower")
                    {
                        kind = c.Name;
                        break;
                    }
                var def = kind == "Building_bath" ? takeBath : kind == "Building_shower" ? takeShower : useWashBucket;
                return def != null ? JobMaker.MakeJob(def, t) : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: a wash", e);
                return null;
            }
        }

        /// <summary>Dubs Bad Hygiene's needs are there for this traveller.</summary>
        public static bool HasHygiene(Pawn p) => Hygiene.HygieneNeed != null && p?.needs?.TryGetNeed(Hygiene.HygieneNeed) != null;

        public static bool HasBladder(Pawn p) => Hygiene.BladderNeed != null && p?.needs?.TryGetNeed(Hygiene.BladderNeed) != null;
    }
}
