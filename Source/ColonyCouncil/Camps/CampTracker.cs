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
        /// <summary>Its people are on the map already (a war party making camp after the fight): not spawned again.</summary>
        public bool onMap;
        /// <summary>The remembered camp this is the return of (CampMemory.cs).</summary>
        public PastCamp past;
        /// <summary>The weather of their stay (CampWeather), and what they put on and how they pitch for it.</summary>
        public float low, high;
        public CampPitch pitch;
    }

    /// <summary>
    /// The camp on the colony's map, if there is one (one at a time, kept in the save), and how a new one is made.
    /// Camps live on their own clock: every few seconds for hunts and the like, every hour for everything else.
    /// </summary>
    public class CampTracker : GameComponent
    {
        public Camp camp;
        /// <summary>Neighbours settled on the land for good (Holding.cs): a camp kept in a slot of its own, so camps still come and go beside it.</summary>
        public Camp holding;
        public int nextId = 1;
        public int lastEndedTick = -9999999;
        /// <summary>Mornings since the last camp on which one could have come (the way was open and the dice were rolled), for the World page.</summary>
        public int openMornings;
        /// <summary>Camps the colony remembers, which may come back (CampMemory.cs).</summary>
        public List<PastCamp> past = new List<PastCamp>();
        /// <summary>A war party on the map, come to fight the colony's attacker (WarCamp.cs), and the threat it was rolled for.</summary>
        public WarBand warBand;
        public int threatSince = -1;
        public bool threatRolled;

        public CampTracker(Game game)
        {
        }

        public static CampTracker Instance => Current.Game?.GetComponent<CampTracker>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The camps on the land: the one visiting, and the neighbours settled for good.</summary>
        public IEnumerable<Camp> All
        {
            get
            {
                if (camp != null) yield return camp;
                if (holding != null) yield return holding;
            }
        }

        public Camp ById(int id) => camp != null && camp.id == id ? camp : holding != null && holding.id == id ? holding : null;

        /// <summary>The camp whose fire this is.</summary>
        public Camp CampAt(IntVec3 fire) => All.FirstOrDefault(c => c.FireCell == fire);

        /// <summary>The camp this pawn belongs to (living with the group on the map), or null.</summary>
        public static Camp CampOf(Pawn p)
        {
            var t = Instance;
            if (t == null || p == null) return null;
            var lord = p.GetLord();
            if (lord == null) return null;
            foreach (var c in t.All)
                if (c.lord == lord) return c;
            return null;
        }

        /// <summary>The camp an answer or an ask is about while it's being dealt with (a letter's answer, a button on its page), for the effects that need to know.</summary>
        public static Camp Asked { get; private set; }

        /// <summary>Marks <paramref name="c"/> as the camp dealt with until disposed.</summary>
        public static IDisposable Talking(Camp c) => new TalkingScope(c);

        private sealed class TalkingScope : IDisposable
        {
            private readonly Camp before;

            public TalkingScope(Camp c)
            {
                before = Asked;
                Asked = c;
            }

            public void Dispose() => Asked = before;
        }

        /// <summary>The camp becomes the colony's neighbours for good: out of the visiting slot (a new camp may come) and into its own.</summary>
        public void MakeHolding(Camp c)
        {
            if (c == null) return;
            if (camp == c)
            {
                camp = null;
                lastEndedTick = Now;
            }
            holding = c;
        }

        public static bool Enabled => S != null && S.worldEvents && S.camps;

        public override void GameComponentTick()
        {
            if (camp == null && holding == null) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareCamp)) return;
            foreach (var c in All.ToList())
            {
                try
                {
                    c.Tick250();
                    if (Sched.Hourly(now, Sched.Camp) && (camp == c || holding == c)) c.Hourly();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp", e);
                }
            }
        }

        public void Ended(Camp c)
        {
            if (holding == c)
            {
                holding = null;
                return;
            }
            if (camp != c) return;
            camp = null;
            lastEndedTick = Now;
        }

        // ---------------- A new camp ----------------

        /// <summary>A camp could come now: switched on, none here, none just gone, room on the map, the right weather and people.</summary>
        public bool CanStartOn(Map map) => WhyNot(map) == null;

        /// <summary>What keeps a camp from coming now, in plain words (for "Rimfolk: check myself"), or null if one could.</summary>
        public string WhyNot(Map map)
        {
            var (code, a, b) = Blocker(map);
            switch (code)
            {
                case null: return null;
                case "off": return "the camps setting or world events are off";
                case "here": return "a camp is here already";
                case "nohome": return "there's no home map";
                case "early": return "never in the first twenty days";
                case "gap": return "the last camp left on day " + lastEndedTick / GenDate.TicksPerDay + ", and the next comes " + a + " days after";
                case "threat": return "there's danger on the map";
                case "weather": return "it's " + a + " °C outside, and no camp comes below " + CampWeatherMath.ExtremeCold.ToString("0") + " or above " + CampWeatherMath.ExtremeHeat.ToString("0") + " °C";
                case "hazard": return "no camp comes while " + a + " lasts";
                case "era": return "no kind of camp fits the colony's era";
                case "nofriends": return "no friendly people could send one";
                default: return "there's no room for a camp on the map";
            }
        }

        /// <summary>The same, for the World page: in the player's words, with how many open mornings have passed.</summary>
        public string WhyNotForPlayer(Map map)
        {
            var (code, a, b) = Blocker(map);
            if (code == null) return "CC_Camp_Why_Open".Translate(openMornings, Mathf.RoundToInt(1f / CampTimer.DailyChance)).Resolve();
            return ("CC_Camp_Why_" + code).Translate(a ?? "", b ?? "").Resolve();
        }

        /// <summary>
        /// The first thing that keeps a camp from coming, as a short code with up to two details (null when nothing
        /// does): off, here, nohome, early, gap (days between), threat, weather (the temperature), hazard (its name),
        /// era, nofriends, noroom.
        /// </summary>
        public (string code, string a, string b) Blocker(Map map)
        {
            if (!Enabled) return ("off", null, null);
            if (camp != null) return ("here", null, null);
            if (map == null || !map.IsPlayerHome) return ("nohome", null, null);
            if (GenDate.DaysPassed < 20) return ("early", (20 - GenDate.DaysPassed).ToString(), null);
            // A colony known for its hospitality hears from the road sooner (ColonyTraitDef.campGap).
            float gap = 15f * (CouncilGameComponent.Instance?.character?.traits?.CampGap ?? 1f);
            if (Now - lastEndedTick < GenDate.TicksPerDay * gap)
                return ("gap", gap.ToString("0"), TextUtil.DaysString(Mathf.RoundToInt(GenDate.TicksPerDay * gap) - (Now - lastEndedTick)));
            if (MapFacts.ThreatOn(map)) return ("threat", null, null);
            float temp = map.mapTemperature.OutdoorTemp;
            if (CampWeatherMath.TooExtremeToCome(temp)) return ("weather", temp.ToString("0"), null);
            var hazard = CampWeather.Hazard(map);
            if (hazard != null) return ("hazard", hazard.Label, null);
            if (!DefDatabase<CampKindDef>.AllDefs.Any(k => k.FitsEra())) return ("era", null, null);
            if (Arrivals.Friendly() == null) return ("nofriends", null, null);
            if (!CampSite.AnyRoom(map)) return ("noroom", null, null);
            return (null, null, null);
        }

        /// <summary>
        /// Picks the kind of camp, its people and its spot; makes the people (not on the map yet). Null if none fits,
        /// with what went wrong added to <paramref name="why"/> if given.
        /// </summary>
        public CampSetup Prepare(Map map, List<string> why = null)
        {
            var kinds = DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && k.Possible && !k.fights).ToList();
            if (kinds.Count == 0)
            {
                why?.Add("no kind of camp can come now");
                return null;
            }
            // Where to pitch: in hot weather, among trees for the shade where there are any.
            var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            if (!CampSite.TryFind(map, out var center, hot))
            {
                why?.Add("no spot for a camp was found");
                return null;
            }
            var colony = CampSite.ColonyCenter(map);
            // People the colony knows, coming back (CampMemory.cs); else a new party.
            var past = CampReturns.Pick(map);
            if (past != null)
            {
                var back = Make(map, center, colony, past.kind, past.faction, hot, why, past);
                if (back != null) return back;
            }
            for (int tries = 0; tries < 6; tries++)
            {
                var kind = kinds.RandomElementByWeight(k => k.WeightNow());
                // Peoples the colony did good turns for camp here more often, those with grudges less (Interplay/).
                var f = Arrivals.Friendly(x => kind.FitsFaction(x), Interplay.CampWeight);
                if (f == null)
                {
                    why?.Add(kind.label + ": no friendly people for it");
                    continue;
                }
                var setup = Make(map, center, colony, kind, f, hot, why, null);
                if (setup != null) return setup;
                if (why != null && why.Count > 0 && why[why.Count - 1].EndsWith("doesn't fit at the spot")) return null;
            }
            return null;
        }

        /// <summary>
        /// A camp of one kind, now, for a world event that calls for it (a wardens' post by the new road): from these
        /// people when the kind fits them, else from any friendly people. Null when none can come (another camp is here,
        /// no spot, no people), with why if asked.
        /// </summary>
        public Camp StartKind(Map map, CampKindDef kind, Faction f, List<string> why = null)
        {
            if (map == null || kind == null || camp != null || !Enabled) return null;
            if (!kind.FitsEra() || !kind.Possible)
            {
                why?.Add(kind.label + ": not in this era");
                return null;
            }
            var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            if (!CampSite.TryFind(map, out var center, hot))
            {
                why?.Add("no spot for a camp was found");
                return null;
            }
            if (f == null || !kind.FitsFaction(f) || !Arrivals.Usable(f) || Arrivals.PersonKind(f) == null) f = Arrivals.Friendly(x => kind.FitsFaction(x), Interplay.CampWeight);
            if (f == null)
            {
                why?.Add(kind.label + ": no friendly people for it");
                return null;
            }
            var setup = Make(map, center, CampSite.ColonyCenter(map), kind, f, hot, why, null);
            var c = setup == null ? null : Start(setup);
            if (c != null) Announce(c);
            return c;
        }

        /// <summary>
        /// The letter a camp's coming gets when no event sends one (a camp a world event called for): its kind's title
        /// and text, with the colonist their people remember by name (or anyone grown) as the one they spoke to.
        /// </summary>
        private static void Announce(Camp c)
        {
            if (c?.kind == null || c.map == null) return;
            try
            {
                var tags = new HashSet<string>();
                Flavor.AddEraTags(tags);
                CampWords.AddTags(tags, c.kind, c.faction, c.map);
                var a = KinMemory.Greeter(c.faction, c.map) ?? IncidentWorker_Happening.Colonists(c.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
                string title = c.Format(Flavor.Pick(c.kind.titles, tags) ?? Flavor.PickText(c.kind.titles), c.leader, a);
                string text = c.Format(Flavor.Pick(c.kind.texts, tags) ?? Flavor.PickText(c.kind.texts), c.leader, a);
                c.Tell(title, text, LetterDefOf.NeutralEvent, true, c.leader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp announced", e);
            }
        }

        /// <summary>
        /// A camp of this kind and people at the spot: its stay, its layout for the weather, its people (those
        /// <paramref name="given"/>, else the remembered ones coming back, filled up with new ones), dressed for the
        /// stay. Null if it can't be, with why.
        /// </summary>
        public CampSetup Make(Map map, IntVec3 center, IntVec3 colony, CampKindDef kind, Faction f, bool hot, List<string> why, PastCamp past, List<Pawn> given = null)
        {
            var kept = given ?? past?.Living ?? new List<Pawn>();
            int n = given != null ? given.Count : Mathf.Clamp(Mathf.Max(kind.people.RandomInRange, kept.Count), 1, 8);
            int stay = Mathf.RoundToInt(kind.days.RandomInRange * GenDate.TicksPerDay);
            var (low, high) = CampWeather.StayRange(map, stay);
            bool cold = CampWeatherMath.PitchForCold(low);
            var plans = CampSite.Layout(map, center, kind, n, colony, out var stash, out var share, cold);
            if (plans == null)
            {
                why?.Add(kind.label + ": its camp doesn't fit at the spot");
                return null;
            }
            var pawns = given != null ? given.ToList() : MakePeople(kind, f, n, kept);
            if (pawns.Count == 0)
            {
                why?.Add(kind.label + " of " + f.Name + ": its people couldn't be made");
                return null;
            }
            // Dressed for their stay; and none of them would be hurt by its weather, dressed as they are (CampWeatherMath).
            var dressed = new List<Apparel>();
            foreach (var p in pawns)
            {
                try
                {
                    dressed.AddRange(CampWeather.Dress(p, low, high, f));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp people dressed for the weather", e);
                }
            }
            int unfit = pawns.Count(p =>
            {
                var comfy = GenTemperature.ComfortableTemperatureRange(p);
                return !CampWeatherMath.WouldStop(low, high, comfy.min, comfy.max);
            });
            if (unfit > 0 && given == null)
            {
                why?.Add(kind.label + " of " + f.Name + ": " + unfit + " of " + pawns.Count + " couldn't dress for " + low.ToString("0") + " to " + high.ToString("0") + " °C");
                // The remembered stay in the world as they were; the new are let go.
                foreach (var p in pawns.Where(p => !kept.Contains(p))) Arrivals.Discard(p);
                return null;
            }
            return new CampSetup
            {
                kind = kind, faction = f, map = map, center = center, stash = stash, share = share, colony = colony, plans = plans, pawns = pawns,
                place = CampSite.Direction(center, colony), stayTicks = stay, low = low, high = high, past = past,
                pitch = new CampPitch
                {
                    clothes = CampWeather.Clothes(dressed),
                    clothesForCold = dressed.Sum(a => a.GetStatValue(StatDefOf.Insulation_Cold)) >= dressed.Sum(a => a.GetStatValue(StatDefOf.Insulation_Heat)),
                    cold = cold && CampSite.BedsClose(plans), windbreak = cold && plans.Any(x => x.role == "wall"), shade = hot && CampSite.Shaded(map, center)
                }
            };
        }

        private static List<Pawn> MakePeople(CampKindDef kind, Faction f, int n, List<Pawn> kept = null)
        {
            var list = new List<Pawn>();
            var personKind = Arrivals.PersonKind(f);
            if (personKind == null) return list;
            string last = null;
            // The same faces first (a camp coming back), then new ones to make up the number.
            if (kept != null)
                foreach (var p in kept.Where(p => p != null && !p.Dead && !p.Discarded && !p.Spawned))
                {
                    if (list.Count >= n) break;
                    list.Add(p);
                    if (kind.family && last == null) last = (p.Name as NameTriple)?.Last;
                }
            for (int i = list.Count; i < n; i++)
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
            openMornings = 0;
            if (s == null || camp != null) return null;
            var map = s.map;
            if (!EntryNear(map, s.center, out var entry)) return null;
            foreach (var p in s.pawns)
            {
                if (p.Spawned)
                {
                    // On the map already (a war party after its fight): out of its old group, into the camp's.
                    p.GetLord()?.RemovePawn(p);
                    continue;
                }
                // Someone kept in the world to come back (CampMemory.cs) is taken out of it first.
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                var cell = CellFinder.RandomClosewalkCellNear(entry, map, 4);
                GenSpawn.Spawn(p, cell, map);
            }
            var c = new Camp
            {
                id = nextId++, kind = s.kind, map = map, faction = s.faction, members = s.pawns.ToList(), leader = s.pawns[0],
                center = s.center, stash = s.stash, share = s.share, colonyCenter = s.colony, place = s.place, plans = s.plans,
                startTick = Now, stayTicks = s.stayTicks, phase = CampPhase.Coming, pitch = s.pitch
            };
            if (s.past != null) CampReturns.Started(c, s.past);
            try
            {
                Equip(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp supplies", e);
            }
            // The animals they bring (pack beasts, a herd, a dog) walk in with them, in their group (CampLook.cs).
            try
            {
                foreach (var a in Camp.MakeAnimals(s.kind, s.faction, map))
                {
                    GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(entry, map, 5), map);
                    c.herd.Add(a);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp animals", e);
            }
            try
            {
                c.lord = LordMaker.MakeNewLord(s.faction, new LordJob_Camp(s.center, s.kind.fights), map, c.members.Concat(c.herd));
            }
            catch (Exception e)
            {
                // No group to live in: they walk off again rather than stand about on the map with nothing to do.
                CouncilLog.ErrorOnce("camp group", e);
                Arrivals.SendAway(c.members);
                try
                {
                    var beasts = c.herd.Where(a => a.Spawned && !a.Dead).ToList();
                    if (beasts.Count > 0) LordMaker.MakeNewLord(s.faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), map, beasts);
                }
                catch (Exception e2)
                {
                    CouncilLog.ErrorOnce("camp animals sent off", e2);
                }
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
        /// <summary>Settled neighbours' trader takes on a new stock now and then (Holding.cs).</summary>
        public static void RefreshTrader(Camp c)
        {
            if (c?.trader == null || c.trader.Dead || c.trader.inventory == null) return;
            // What's left of the old stock stays theirs; the new is added to it.
            MakeTrader(c, c.trader);
        }

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
            Scribe_Deep.Look(ref holding, "holding");
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref lastEndedTick, "lastEndedTick", -9999999);
            Scribe_Values.Look(ref openMornings, "openMornings", 0);
            Scribe_Collections.Look(ref past, "past", LookMode.Deep);
            Scribe_Deep.Look(ref warBand, "warBand");
            Scribe_Values.Look(ref threatSince, "threatSince", -1);
            Scribe_Values.Look(ref threatRolled, "threatRolled");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // A camp whose kind was removed with its mod, or whose map is gone: nothing left to keep.
                if (camp != null && (camp.kind == null || camp.map == null)) camp = null;
                if (holding != null && (holding.kind == null || holding.map == null)) holding = null;
                past ??= new List<PastCamp>();
                past.RemoveAll(p => p == null || p.kind == null || p.faction == null);
                if (warBand != null && (warBand.map == null || warBand.faction == null)) warBand = null;
            }
        }
    }
}
