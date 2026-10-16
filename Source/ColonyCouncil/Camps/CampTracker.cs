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
    /// <summary>Everything a camp needs before it arrives: who, from where, to where, and what they'll build.</summary>
    public class CampSetup
    {
        public CampKindDef kind;
        public Faction faction;
        public Map map;
        public IntVec3 center, stash, share, colony;
        public List<CampPlan> plans;
        public List<Pawn> pawns;
        public string place;
        public int stayTicks;
    }

    /// <summary>
    /// The camp on the colony's map, if there is one (one at a time, kept in the save), and how a new one is made.
    /// Camps live on their own clock: every few seconds for hunts and the like, every hour for everything else.
    /// </summary>
    public class CampTracker : GameComponent
    {
        public Camp camp;
        public int nextId = 1;
        public int lastEndedTick = -9999999;

        public CampTracker(Game game)
        {
        }

        public static CampTracker Instance => Current.Game?.GetComponent<CampTracker>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The camp this pawn belongs to (living with the group on the map), or null.</summary>
        public static Camp CampOf(Pawn p)
        {
            var c = Instance?.camp;
            if (c == null || p == null || c.lord == null) return null;
            return p.GetLord() == c.lord ? c : null;
        }

        public static bool Enabled => S != null && S.worldEvents && S.camps;

        public override void GameComponentTick()
        {
            var c = camp;
            if (c == null) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareCamp)) return;
            try
            {
                c.Tick250();
                if (Sched.Hourly(now, Sched.Camp) && camp == c) c.Hourly();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp", e);
            }
        }

        public void Ended(Camp c)
        {
            if (camp != c) return;
            camp = null;
            lastEndedTick = Now;
        }

        // ---------------- A new camp ----------------

        /// <summary>A camp could come now: switched on, none here, none just gone, room on the map, the right weather and people.</summary>
        public bool CanStartOn(Map map)
        {
            if (!Enabled || camp != null || map == null || !map.IsPlayerHome) return false;
            if (Now - lastEndedTick < GenDate.TicksPerDay * 15 || GenDate.DaysPassed < 20) return false;
            if (MapFacts.ThreatOn(map)) return false;
            float temp = map.mapTemperature.OutdoorTemp;
            if (temp < -8f || temp > 38f) return false;
            if (!DefDatabase<CampKindDef>.AllDefs.Any(k => k.FitsEra())) return false;
            if (Arrivals.Friendly() == null) return false;
            return CampSite.AnyRoom(map);
        }

        /// <summary>Picks the kind of camp, its people and its spot; makes the people (not on the map yet). Null if none fits.</summary>
        public CampSetup Prepare(Map map)
        {
            var kinds = DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && k.Possible).ToList();
            if (kinds.Count == 0 || !CampSite.TryFind(map, out var center)) return null;
            var colony = CampSite.ColonyCenter(map);
            for (int tries = 0; tries < 6; tries++)
            {
                var kind = kinds.RandomElementByWeight(k => k.WeightNow());
                // Peoples the colony did good turns for camp here more often, those with grudges less (Interplay/).
                var f = Arrivals.Friendly(x => kind.FitsFaction(x), Interplay.CampWeight);
                if (f == null) continue;
                int n = Mathf.Clamp(kind.people.RandomInRange, 1, 8);
                var plans = CampSite.Layout(map, center, kind, n, colony, out var stash, out var share);
                if (plans == null) return null;
                var pawns = MakePeople(kind, f, n);
                if (pawns.Count == 0) continue;
                // Not dressed for this weather: they wouldn't stop here now.
                float temp = map.mapTemperature.OutdoorTemp;
                if (pawns.Any(p => temp < GenTemperature.ComfortableTemperatureRange(p).min || temp > GenTemperature.ComfortableTemperatureRange(p).max))
                {
                    foreach (var p in pawns) Arrivals.Discard(p);
                    continue;
                }
                return new CampSetup
                {
                    kind = kind, faction = f, map = map, center = center, stash = stash, share = share, colony = colony, plans = plans, pawns = pawns,
                    place = CampSite.Direction(center, colony), stayTicks = Mathf.RoundToInt(kind.days.RandomInRange * GenDate.TicksPerDay)
                };
            }
            return null;
        }

        private static List<Pawn> MakePeople(CampKindDef kind, Faction f, int n)
        {
            var list = new List<Pawn>();
            var personKind = Arrivals.PersonKind(f);
            if (personKind == null) return list;
            string last = null;
            for (int i = 0; i < n; i++)
            {
                bool child = kind.family && i >= 2 && ModsConfig.BiotechActive && Rand.Chance(0.5f);
                try
                {
                    var p = Arrivals.Make(personKind, f, child ? (FloatRange?)null : kind.ages, null, kind.family ? last : null, child, kind.family && list.Count > 0 ? list[0] : null);
                    if (p == null) continue;
                    if (kind.family && last == null) last = (p.Name as NameTriple)?.Last;
                    list.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp people", e);
                }
            }
            return list;
        }

        /// <summary>Lets go of people made for a camp that never came.</summary>
        public static void Discard(CampSetup s)
        {
            if (s?.pawns == null) return;
            foreach (var p in s.pawns.Where(p => p != null && !p.Spawned)) Arrivals.Discard(p);
        }

        /// <summary>They walk in from the edge nearest their spot, carrying what they need.</summary>
        public Camp Start(CampSetup s)
        {
            if (s == null || camp != null) return null;
            var map = s.map;
            if (!EntryNear(map, s.center, out var entry)) return null;
            foreach (var p in s.pawns)
            {
                var cell = CellFinder.RandomClosewalkCellNear(entry, map, 4);
                GenSpawn.Spawn(p, cell, map);
            }
            var c = new Camp
            {
                id = nextId++, kind = s.kind, map = map, faction = s.faction, members = s.pawns.ToList(), leader = s.pawns[0],
                center = s.center, stash = s.stash, share = s.share, colonyCenter = s.colony, place = s.place, plans = s.plans,
                startTick = Now, stayTicks = s.stayTicks, phase = CampPhase.Coming
            };
            try
            {
                Equip(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp supplies", e);
            }
            try
            {
                c.lord = LordMaker.MakeNewLord(s.faction, new LordJob_Camp(s.center), map, c.members);
            }
            catch (Exception e)
            {
                // No group to live in: they walk off again rather than stand about on the map with nothing to do.
                CouncilLog.ErrorOnce("camp group", e);
                Arrivals.SendAway(c.members);
                return null;
            }
            camp = c;
            return c;
        }

        private static bool EntryNear(Map map, IntVec3 center, out IntVec3 entry)
        {
            entry = IntVec3.Invalid;
            float best = float.MaxValue;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            for (int i = 0; i < 24; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, center, PathEndMode.OnCell, tp), map, CellFinder.EdgeRoadChance_Neutral, out var cell)) continue;
                float d = cell.DistanceToSquared(center);
                if (d < best)
                {
                    best = d;
                    entry = cell;
                }
            }
            return entry.IsValid || Arrivals.TryEntryCell(map, out entry);
        }

        /// <summary>
        /// What they bring: the materials for their camp (a little over), two meals each and dried food for their stay,
        /// bows or rifles for hunters, and for a trading camp, a small stock to sell.
        /// </summary>
        private static void Equip(Camp c)
        {
            var adults = c.members.Where(p => !p.DevelopmentalStage.Juvenile() && p.inventory != null).ToList();
            if (adults.Count == 0) adults = c.members.Where(p => p.inventory != null).ToList();
            var totals = new Dictionary<ThingDef, int>();
            foreach (var plan in c.plans)
                foreach (var cost in Camp.Costs(plan))
                    totals[cost.thingDef] = (totals.TryGetValue(cost.thingDef, out var n) ? n : 0) + cost.count;
            int who = 0;
            foreach (var kv in totals)
            {
                int left = Mathf.CeilToInt(kv.Value * 1.1f);
                while (left > 0 && adults.Count > 0)
                {
                    var t = ThingMaker.MakeThing(kv.Key);
                    t.stackCount = Mathf.Min(left, kv.Key.stackLimit);
                    left -= t.stackCount;
                    adults[who++ % adults.Count].inventory.TryAddItemNotForSale(t);
                }
            }
            foreach (var p in c.members.Where(p => p.inventory != null))
            {
                var meals = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                meals.stackCount = 2;
                p.inventory.TryAddItemNotForSale(meals);
            }
            c.provisions = Mathf.RoundToInt(c.members.Count * (c.stayTicks / (float)GenDate.TicksPerDay) * 1.6f);
            if (c.kind.armedHunters) ArmHunters(c, adults);
            foreach (var p in adults)
                if (p != c.leader && Rand.Chance(c.kind.hireChance)) c.willing.Add(p);
            if (c.kind.trader)
            {
                var t = adults.FirstOrDefault(p => p != c.leader) ?? c.leader;
                try
                {
                    if (t != null && MakeTrader(c, t)) c.willing.Remove(t);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp trader", e);
                }
            }
        }

        private static void ArmHunters(Camp c, List<Pawn> adults)
        {
            bool low = Avail.IsPreIndustrial || c.faction.def.techLevel <= TechLevel.Medieval;
            var def = (low ? null : DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BoltActionRifle"))
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Recurve") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Short");
            if (def == null) return;
            int armed = 0;
            foreach (var p in adults.Where(p => p.equipment != null && !p.WorkTagIsDisabled(WorkTags.Violent)))
            {
                if (armed >= 3) break;
                var held = p.equipment.Primary;
                if (held != null && held.def.IsRangedWeapon)
                {
                    armed++;
                    continue;
                }
                if (held != null)
                {
                    p.equipment.Remove(held);
                    p.inventory.innerContainer.TryAdd(held);
                }
                var w = (ThingWithComps)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                p.equipment.AddEquipment(w);
                armed++;
            }
        }

        /// <summary>One of them trades from the camp, with the game's own trading: a small stock of their people's goods.</summary>
        private static bool MakeTrader(Camp c, Pawn p)
        {
            var tk = TraderKind(c.faction, c.kind.traderKind);
            if (tk == null) return false;
            if (p.trader == null) p.trader = new Pawn_TraderTracker(p);
            p.trader.traderKind = tk;
            p.mindState.wantsToTradeWithColony = true;
            var parms = new ThingSetMakerParams { traderDef = tk, tile = c.map.Tile, makingFaction = c.faction };
            var stock = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
            float budget = Rand.Range(800f, 1500f) * LivingEvents.WealthFactor(c.map);
            float value = 0f;
            foreach (var t in stock.InRandomOrder().ToList())
            {
                if (t is Pawn animalOrPerson)
                {
                    // A camp has no pens or cages: no living stock.
                    if (!animalOrPerson.Destroyed && !Find.WorldPawns.Contains(animalOrPerson)) Find.WorldPawns.PassToWorld(animalOrPerson, PawnDiscardDecideMode.Discard);
                    continue;
                }
                if (t.def == ThingDefOf.Silver)
                {
                    t.stackCount = Mathf.Min(t.stackCount, Rand.RangeInclusive(250, 600));
                    p.inventory.innerContainer.TryAdd(t);
                    continue;
                }
                float unit = t.MarketValue;
                if (unit <= 0f) continue;
                if (value + unit * t.stackCount > budget)
                {
                    int fit = Mathf.FloorToInt((budget - value) / unit);
                    if (fit <= 0) continue;
                    t.stackCount = Mathf.Min(t.stackCount, fit);
                }
                value += unit * t.stackCount;
                p.inventory.innerContainer.TryAdd(t);
            }
            c.trader = p;
            c.traderSilver = p.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
            return true;
        }

        private static TraderKindDef TraderKind(Faction f, string hint)
        {
            var all = new List<TraderKindDef>();
            if (f.def.visitorTraderKinds != null) all.AddRange(f.def.visitorTraderKinds);
            if (f.def.caravanTraderKinds != null) all.AddRange(f.def.caravanTraderKinds);
            all = all.Where(k => k != null && !k.orbital && k.permitRequiredForTrading == null).Distinct().ToList();
            if (!hint.NullOrEmpty())
            {
                var match = all.Where(k => k.defName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (match.Count > 0) return match.RandomElement();
            }
            return f.def.visitorTraderKinds?.Where(k => k != null && k.permitRequiredForTrading == null).RandomElementWithFallback() ?? all.RandomElementWithFallback();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref camp, "camp");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref lastEndedTick, "lastEndedTick", -9999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A camp whose kind was removed with its mod, or whose map is gone: nothing left to keep.
                if (camp != null && (camp.kind == null || camp.map == null)) camp = null;
            }
        }
    }
}
