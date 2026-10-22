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
    /// Whose hands goods taken by an effect go into (DilemmaEffect_Consume, WorldEffect_Pay, WorldEffect_Medicine) or
    /// come out of (DilemmaEffect_Spawn): used up or found at home (the default: fuel burned, a feast eaten, a find), or
    /// handed over to outsiders, who come for them, or bring them, on the map (Couriers/).
    /// </summary>
    public enum GoodsBy
    {
        Home,
        Outsiders
    }

    /// <summary>
    /// Goods that come and go on the map ("Goods and people come and go on the map" in the settings, on by default):
    /// when one of Rimfolk's events gives the colony silver, goods, a gift or a payment owed, or takes goods it gave,
    /// sold or paid, a few people of a fitting people walk in from the edge some hours later, by day, unannounced, and
    /// do it on the map: set the things down by the colony's gate, or walk to the stores and carry them off, or wait at
    /// the gate for what a colonist carries out to them. One to three come (a runner with a purse, porters for a load),
    /// with a pack animal from their own caravans for a big load. They linger a little, then walk off.
    /// <para>Every way it can go has a safe end: no people to send, no way in, never safe to come, the stores out of
    /// reach, the goods used up meanwhile, a courier hurt or arrested, their people turning hostile, the map gone, a
    /// reload, or the setting turned off. What's left of the deal is then settled the old way (at once, as before), or,
    /// when the colony hurt them, the deal holds as far as it got. A trip settles once, so nothing is given or taken
    /// twice. With the setting off, everything is instant, as it always was.</para>
    /// <para>Callers ask first (<see cref="Deliver"/>, <see cref="Collect"/>, <see cref="Exchange"/>...): false means
    /// no courier will come, and the caller does what it always did.</para>
    /// </summary>
    public static class Couriers
    {
        public static bool On => ColonyCouncilMod.Settings?.goodsComeAndGo ?? false;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Who comes ----------------

        /// <summary>A people that can send someone: at peace with the colony, of people, still standing, with people to send.</summary>
        public static bool Usable(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def != null && f.def.humanlikeFaction
            && !f.HostileTo(Faction.OfPlayer) && (Porters(f).Any() || Arrivals.PersonKind(f) != null);

        /// <summary>
        /// Who sends the courier: the event's own people when they can; else a people of a fitting tech level whose town
        /// is nearest (CourierMath.ChooseFaction). Null when there's nobody.
        /// </summary>
        public static Faction Choose(Faction preferred, Map map)
        {
            if (map == null) return null;
            var cands = new List<CourierCandidate>();
            var facs = new List<Faction>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.IsPlayer) continue;
                bool usable;
                try
                {
                    usable = Usable(f);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier faction", e);
                    usable = false;
                }
                if (!usable) continue;
                float dist = 9999f;
                try
                {
                    var town = WorldSituationWorker.NearestTown(f, map);
                    if (town != null) dist = WorldSituationWorker.Distance(map, town);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier town", e);
                }
                facs.Add(f);
                cands.Add(new CourierCandidate { id = f.loadID, usable = true, tech = (int)f.def.techLevel, distance = dist });
            }
            int i = CourierMath.ChooseFaction(cands, preferred?.loadID ?? -1, (int)Avail.EffectiveTech, Avail.IsPreIndustrial);
            return i < 0 ? null : facs[i];
        }

        private static IEnumerable<PawnGroupMaker> TraderGroups(Faction f) =>
            f?.def?.pawnGroupMakers?.Where(g => g != null && g.kindDef == PawnGroupKindDefOf.Trader) ?? Enumerable.Empty<PawnGroupMaker>();

        /// <summary>
        /// Who a people sends to carry: one of their caravan's lighter guards (a porter, not a heavy trooper), else the
        /// ordinary person they send to visit (Arrivals.PersonKind). Never one of their traders (who would expect to trade).
        /// </summary>
        public static PawnKindDef PersonKind(Faction f)
        {
            if (f?.def == null) return null;
            var guards = Porters(f).ToList();
            if (guards.Count > 0) return guards.RandomElementByWeight(o => Mathf.Max(0.01f, o.selectionWeight)).kind;
            return Arrivals.PersonKind(f);
        }

        /// <summary>The lighter guards of a people's caravans: people, not traders, not heavy troopers.</summary>
        private static IEnumerable<PawnGenOption> Porters(Faction f) =>
            TraderGroups(f).Where(g => g.guards != null).SelectMany(g => g.guards)
                .Where(o => o?.kind?.RaceProps != null && o.kind.RaceProps.Humanlike && !o.kind.trader && o.kind.combatPower <= 75f);

        /// <summary>A pack animal their caravans use that suits this land and weather, or null (they don't use them here).</summary>
        public static PawnKindDef AnimalKind(Faction f, Map map)
        {
            if (f == null || map == null) return null;
            try
            {
                var biome = map.Biome;
                var opts = TraderGroups(f).Where(g => g.carriers != null).SelectMany(g => g.carriers)
                    .Where(o => o?.kind?.race?.race != null && o.kind.RaceProps.Animal && o.kind.RaceProps.packAnimal
                                && (biome == null || biome.IsPackAnimalAllowed(o.kind.race))
                                && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(o.kind.race, 0f)).ToList();
                return opts.Count == 0 ? null : opts.RandomElementByWeight(o => Mathf.Max(0.01f, o.selectionWeight)).kind;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier animal", e);
                return null;
            }
        }

        /// <summary>Someone of the event already here who can hand things over in person: up, at peace with the colony, a person, not held.</summary>
        public static bool InPerson(Pawn x, Map map) =>
            x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && x.RaceProps != null && x.RaceProps.Humanlike && x.Faction != null
            && !x.Faction.IsPlayer && !x.Faction.HostileTo(Faction.OfPlayer) && !x.IsPrisoner && !x.InMentalState;

        /// <summary>Someone standing at the colony (on its home ground, or near one of its people), not just in from the edge.</summary>
        public static bool AtColony(Pawn p)
        {
            var map = p?.Map;
            if (map == null) return false;
            if (map.areaManager?.Home != null && map.areaManager.Home[p.Position]) return true;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (c.Position.InHorDistOf(p.Position, 20f))
                    return true;
            return false;
        }

        /// <summary>A courier could be sent at all: the setting on, a home map, someone to send.</summary>
        public static bool CanSend(Map map, Faction preferred)
        {
            if (!On || map == null || !map.IsPlayerHome || CourierTracker.Instance == null) return false;
            try
            {
                return Choose(preferred, map) != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier can send", e);
                return false;
            }
        }

        // ---------------- Asking for one ----------------

        private static CourierTrip New(CourierKind kind, Map map, Faction preferred, FloatRange hours)
        {
            int now = Now;
            int due = now + Mathf.RoundToInt(hours.RandomInRange * CourierMath.TicksPerHour);
            return new CourierTrip { kind = kind, map = map, preferred = preferred, created = now, due = due, giveUp = due + CourierMath.GiveUpTicks };
        }

        private static readonly FloatRange Later = new FloatRange(3f, 9f);

        /// <summary>
        /// Things for the colony from outsiders (silver, a gift, goods, a payment owed): carried in some hours later and
        /// set down by the gate. Someone of the event already here (<paramref name="bearer"/>) hands them over at once if
        /// they're at the colony, or carries them in and sets them down when they get there (at <paramref name="drop"/>
        /// if given). False: nobody will bring them (the setting off, nobody to send): the caller sets them down the old way.
        /// </summary>
        public static bool Deliver(Map map, List<Thing> things, Faction from, IntVec3 near, Pawn bearer = null, IntVec3? drop = null) =>
            DeliverTrip(map, things, from, near, bearer, drop, out _) || handledInPerson;

        [ThreadStatic] private static bool handledInPerson;

        /// <summary>
        /// Like <see cref="Deliver"/>, and the trip (to hold a letter for when they set it down: <see cref="TellWhenThere"/>).
        /// Null when nobody comes; <paramref name="handed"/>: the visitor handed it over at once.
        /// </summary>
        public static bool DeliverTrip(Map map, List<Thing> things, Faction from, IntVec3 near, Pawn bearer, IntVec3? drop, out CourierTrip trip, bool soon = false)
        {
            trip = null;
            handledInPerson = false;
            things = things?.Where(t => t != null && !t.Destroyed && !t.Spawned && t.holdingOwner == null).ToList();
            if (map == null || things.NullOrEmpty() || !On) return false;
            try
            {
                if (InPerson(bearer, map))
                {
                    // They're here: handed over now if they're at the colony, else carried in and set down when they arrive.
                    if (AtColony(bearer) && drop == null)
                    {
                        ByHand(bearer, things, map);
                        handledInPerson = true;
                        return false;
                    }
                    if (CourierTracker.Instance == null) return false;
                    var b = New(CourierKind.Deliver, map, bearer.Faction, Soon);
                    b.bearerTrip = true;
                    b.faction = bearer.Faction;
                    b.people = new List<Pawn> { bearer };
                    b.oldSpot = near;
                    b.spot = drop ?? IntVec3.Invalid;
                    b.bringLabel = Describe(things);
                    b.bringTag = TagOf(things.FirstOrDefault()?.def);
                    foreach (var thing in things) b.goods.TryAdd(thing, true);
                    CourierTracker.Instance.Add(b);
                    b.state = CourierState.Working;
                    b.arrived = Now;
                    trip = b;
                    return true;
                }
                if (!CanSend(map, from)) return false;
                var t = New(CourierKind.Deliver, map, from, soon ? Soon : Later);
                t.oldSpot = near;
                t.bringLabel = Describe(things);
                t.bringTag = TagOf(things.FirstOrDefault()?.def);
                foreach (var thing in things) t.goods.TryAdd(thing, true);
                trip = Register(t);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier deliver", e);
                return false;
            }
        }

        /// <summary>Silver for the colony, carried in (see <see cref="Deliver"/>); else left near the spot as before.</summary>
        public static void PaySilver(Map map, IntVec3 near, int amount, Faction from, Pawn bearer = null, IntVec3? drop = null)
        {
            if (map == null || amount <= 0) return;
            if (!Deliver(map, Make(ThingDefOf.Silver, amount), from, near, bearer, drop)) VarietyUtil.PaySilver(map, near, amount);
        }

        /// <summary>So many of a thing for the colony, carried in (see <see cref="Deliver"/>); else handed over near the spot as before.</summary>
        public static void Hand(ThingDef def, int count, Map map, IntVec3 near, Faction from, Pawn bearer = null)
        {
            if (def == null || count <= 0 || map == null) return;
            if (!Deliver(map, Make(def, count), from, near, bearer)) StorytellerEvents.Hand(def, count, map, near);
        }

        /// <summary>Things of the colony's era for it (Loot), carried in; else as before (Loot.Deliver, never by pod here).</summary>
        public static void Loot(List<Thing> things, Map map, IntVec3 near, Faction from, Pawn bearer = null, IntVec3? drop = null)
        {
            if (things.NullOrEmpty() || map == null) return;
            if (!Deliver(map, things, from, near, bearer, drop)) ColonyCouncil.Loot.Deliver(things, map, near, false);
        }

        /// <summary>So many of a thing as stacks, unspawned.</summary>
        public static List<Thing> Make(ThingDef def, int count)
        {
            var list = new List<Thing>();
            if (def == null) return list;
            count = Math.Min(count, 20000);
            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                thing.stackCount = Math.Min(count, Math.Max(1, def.stackLimit));
                count -= thing.stackCount;
                list.Add(thing);
            }
            return list;
        }

        /// <summary>
        /// Goods from the stores that outsiders take (given, sold, paid): <paramref name="what"/> ("silver", "rawfood",
        /// "medicine", "meals", "wood", a defName; empty: <paramref name="def"/>), <paramref name="count"/> of them. They
        /// come for them some hours later and carry them off; the colony keeps them until then. False: nobody will come,
        /// and the caller takes them the old way.
        /// </summary>
        public static bool Collect(Map map, string what, ThingDef def, int count, Faction to)
        {
            if (map == null || count <= 0 || !On) return false;
            try
            {
                if (!CanSend(map, to)) return false;
                var t = New(CourierKind.Collect, map, to, Later);
                t.what = what;
                t.def = def ?? PlayCarry.DefOf(what, null);
                t.amount = count;
                t.takeLabel = Amount(count, what, t.def);
                t.takeTag = TagOf(what, t.def);
                Register(t);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier collect", e);
                return false;
            }
        }

        /// <summary>
        /// Like VarietyUtil.TakeFromStores: false (nothing taken) when the stores hold fewer; else a courier comes for them,
        /// or (nobody to send) they're taken now.
        /// </summary>
        public static bool TakeFromStores(Map map, ThingDef def, int n, Faction to)
        {
            if (map == null || def == null || n <= 0) return false;
            if (VarietyUtil.InStores(map, def) < n) return false;
            if (Collect(map, null, def, n, to)) return true;
            return VarietyUtil.TakeFromStores(map, def, n);
        }

        /// <summary>
        /// A sale paid at once, in one visit: they set the payment down by the gate, then take the goods from the stores.
        /// False: nobody will come; the caller takes the goods and pays the old way.
        /// </summary>
        public static bool Exchange(Map map, string what, ThingDef def, int count, List<Thing> pay, Faction f)
        {
            pay = pay?.Where(t => t != null && !t.Destroyed && !t.Spawned && t.holdingOwner == null).ToList();
            if (map == null || count <= 0 || !On) return false;
            try
            {
                if (!CanSend(map, f)) return false;
                var t = New(CourierKind.Exchange, map, f, Later);
                t.what = what;
                t.def = def ?? PlayCarry.DefOf(what, null);
                t.amount = count;
                t.takeLabel = Amount(count, what, t.def);
                t.takeTag = TagOf(what, t.def);
                if (!pay.NullOrEmpty())
                {
                    t.bringLabel = Describe(pay);
                    t.bringTag = TagOf(pay[0].def);
                    foreach (var thing in pay) t.goods.TryAdd(thing, true);
                }
                else t.kind = CourierKind.Collect;
                Register(t);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier exchange", e);
                return false;
            }
        }

        /// <summary>
        /// Silver (or things) a memo's people bring home some days on (a venture, a fair's takings, a campaign's share):
        /// carried in, with the letter about it held until they set it down. False: nobody comes; the caller does it all now.
        /// </summary>
        public static bool BringHome(Map map, List<Thing> things, Faction from, IntVec3 near, HappeningContext ctx, List<string> titles, List<string> texts,
            IEnumerable<string> tags, LetterDef letter, string item = null)
        {
            if (!DeliverTrip(map, things, from, near, null, null, out var trip, true) || trip == null) return false;
            TellWhenThere(trip, ctx, titles, texts, tags, letter, item);
            return true;
        }

        /// <summary>A sale collected on its day (a bulk order): they come soon, bring the price and take the goods; the trip, or null.</summary>
        public static CourierTrip CollectOrder(Map map, ThingDef def, int count, int silver, Faction f)
        {
            if (map == null || def == null || count <= 0 || !On) return null;
            try
            {
                if (!CanSend(map, f)) return null;
                var t = New(silver > 0 ? CourierKind.Exchange : CourierKind.Collect, map, f, Soon);
                t.def = def;
                t.amount = count;
                t.takeLabel = Amount(count, null, def);
                t.takeTag = TagOf(def);
                if (silver > 0)
                {
                    var pay = Make(ThingDefOf.Silver, silver);
                    t.bringLabel = Describe(pay);
                    t.bringTag = "silver";
                    foreach (var thing in pay) t.goods.TryAdd(thing, true);
                }
                CourierTracker.Instance.Add(t);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier order", e);
                return null;
            }
        }

        /// <summary>Sells so many of a thing from the stores for silver paid on the spot: a courier brings it and takes them; else at once.</summary>
        public static bool Sell(Map map, ThingDef def, int count, int silver, Faction f, IntVec3 near)
        {
            if (map == null || def == null || count <= 0) return false;
            if (VarietyUtil.InStores(map, def) < count) return false;
            if (Exchange(map, null, def, count, silver > 0 ? Make(ThingDefOf.Silver, silver) : null, f)) return true;
            if (!VarietyUtil.TakeFromStores(map, def, count)) return false;
            if (silver > 0) VarietyUtil.PaySilver(map, near, silver);
            return true;
        }

        /// <summary>
        /// Particular things lying on the colony's land that go back to their owners (a neighbour's lost cargo): they come
        /// for them, and with <paramref name="pay"/>, bring a finder's fee. False: nobody will come.
        /// </summary>
        public static bool CollectThings(Map map, List<Thing> things, Faction owner, List<Thing> pay)
        {
            things = things?.Where(t => t != null && !t.Destroyed && t.Spawned && t.Map == map).ToList();
            if (map == null || things.NullOrEmpty() || !On) return false;
            try
            {
                if (!CanSend(map, owner)) return false;
                var t = New(pay.NullOrEmpty() ? CourierKind.Collect : CourierKind.Exchange, map, owner, Later);
                t.things = things;
                t.amount = things.Sum(x => x.stackCount);
                t.takeLabel = Describe(things);
                t.takeTag = TagOf(things[0].def);
                if (!pay.NullOrEmpty())
                {
                    t.bringLabel = Describe(pay);
                    t.bringTag = TagOf(pay[0].def);
                    foreach (var thing in pay) t.goods.TryAdd(thing, true);
                }
                Register(t);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier collect things", e);
                return false;
            }
        }

        /// <summary>
        /// A new trip, or, when the same answer already set one going for the same people (a purchase: the silver paid
        /// and the goods bought, two effects), one visit for both: they bring the one and take the other.
        /// </summary>
        private static CourierTrip Register(CourierTrip t)
        {
            var tracker = CourierTracker.Instance;
            var same = tracker.trips.FirstOrDefault(x => x.state == CourierState.Pending && !x.settled && x.created == t.created && x.map == t.map
                                                          && x.preferred == t.preferred && x.kind != CourierKind.Await && x.playId < 0 && CanMerge(x, t));
            // The same answer sends something to the gate and is paid for it (medicine carried out, silver for it): the
            // one who comes to wait for the errand brings the payment.
            var waiting = same == null && t.kind == CourierKind.Deliver
                ? tracker.trips.FirstOrDefault(x => x.state == CourierState.Pending && !x.settled && x.created == t.created && x.map == t.map
                                                    && x.preferred == t.preferred && x.kind == CourierKind.Await)
                : null;
            if (waiting != null)
            {
                t.goods.TryTransferAllToContainer(waiting.goods, true);
                waiting.bringLabel = Describe(waiting.goods.ToList());
                waiting.bringTag = t.bringTag;
                if (!waiting.oldSpot.IsValid) waiting.oldSpot = t.oldSpot;
                return waiting;
            }
            if (same == null)
            {
                tracker.Add(t);
                return t;
            }
            if (t.goods.Count > 0)
            {
                t.goods.TryTransferAllToContainer(same.goods, true);
                same.bringLabel = Describe(same.goods.ToList());
                if (same.bringTag == null) same.bringTag = t.bringTag;
            }
            if (t.amount > 0 || t.things.Count > 0)
            {
                if (same.amount > 0 || same.things.Count > 0)
                {
                    same.amount += t.amount;
                    same.takeLabel = Amount(same.amount, same.what, same.def);
                }
                else
                {
                    same.what = t.what;
                    same.def = t.def;
                    same.amount = t.amount;
                    same.things = t.things;
                    same.takeLabel = t.takeLabel;
                    same.takeTag = t.takeTag;
                }
            }
            bool bring = same.goods.Count > 0, take = same.amount > 0 || same.things.Count > 0;
            same.kind = bring && take ? CourierKind.Exchange : bring ? CourierKind.Deliver : CourierKind.Collect;
            if (!same.oldSpot.IsValid) same.oldSpot = t.oldSpot;
            return same;
        }

        /// <summary>Two trips can be one visit: they take at most one kind of goods (the same, or only one of them takes).</summary>
        private static bool CanMerge(CourierTrip a, CourierTrip b)
        {
            bool at = a.amount > 0 || a.things.Count > 0, bt = b.amount > 0 || b.things.Count > 0;
            if (!at || !bt) return true;
            return a.things.Count == 0 && b.things.Count == 0 && a.what == b.what && a.def == b.def;
        }

        // ---------------- For an event's effects ----------------

        /// <summary>The event's own people (its faction, else the stranger's), if any and not the colony's.</summary>
        public static Faction PeopleOf(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            if (f != null && !f.IsPlayer) return f;
            f = ctx?.stranger?.Faction;
            return f != null && !f.IsPlayer ? f : null;
        }

        /// <summary>
        /// An effect's goods go to outsiders: carried out by a colonist to the event's visitor when they're here (an
        /// errand, PlayOut/), else collected by a courier of the event's people. False: neither can happen, and the caller
        /// takes them the old way.
        /// </summary>
        public static bool TakeFor(DilemmaContext ctx, string what, ThingDef def, int n)
        {
            var map = ctx?.map ?? HappeningTracker.HomeMap();
            if (map == null || n <= 0) return false;
            if (ctx != null && ctx.map == null) ctx.map = map;
            if (InPerson(ctx?.stranger, map) && PlayOuts.On)
            {
                try
                {
                    // The colony hands it to them (the errand takes it from the stores itself if it can't happen).
                    var errand = new DilemmaEffect_CarryOut { what = what.NullOrEmpty() ? def?.defName : what, count = n, steward = false, who = PlayWho.A, to = PlaySpot.Stranger };
                    errand.Apply(ctx);
                    return true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier errand", e);
                }
            }
            return Collect(map, what, def, n, PeopleOf(ctx));
        }

        /// <summary>
        /// Like VarietyUtil.TakeFromStores, for goods an event's people take (sold, paid, given): false (nothing taken)
        /// when the stores hold fewer; else carried out to the visitor or fetched by a courier (TakeFor), or taken now.
        /// </summary>
        public static bool TakeFromStoresFor(DilemmaContext ctx, ThingDef def, int n)
        {
            var map = ctx?.map;
            if (map == null || def == null || n <= 0) return false;
            if (VarietyUtil.InStores(map, def) < n) return false;
            if (TakeFor(ctx, null, def, n)) return true;
            return VarietyUtil.TakeFromStores(map, def, n);
        }

        /// <summary>
        /// A mission's order collected by their carts (Missions' own words for goods: meals, art, blocks...): one party
        /// per kind of goods, the first bringing the pay. Soon after, since the day was named. False: nobody comes.
        /// </summary>
        public static bool CollectGoals(Map map, List<KeyValuePair<string, int>> goals, Faction f, int silver)
        {
            if (map == null || goals.NullOrEmpty() || !On) return false;
            try
            {
                if (!CanSend(map, f)) return false;
                bool paid = silver <= 0;
                foreach (var g in goals.Where(g => g.Value > 0))
                {
                    var t = New(paid ? CourierKind.Collect : CourierKind.Exchange, map, f, Soon);
                    t.source = "mission";
                    t.what = g.Key;
                    t.amount = g.Value;
                    t.takeLabel = "CC_Courier_Amount".Translate(g.Value, Missions.GoalLabel(new MissionGoal { what = g.Key, count = g.Value })).Resolve();
                    t.takeTag = g.Key == "meals" || g.Key == "rawfood" ? "food" : g.Key == "medicine" ? "medicine" : "goods";
                    if (!paid)
                    {
                        var pay = Make(ThingDefOf.Silver, silver);
                        t.bringLabel = Describe(pay);
                        t.bringTag = "silver";
                        foreach (var thing in pay) t.goods.TryAdd(thing, true);
                        paid = true;
                    }
                    CourierTracker.Instance.Add(t);
                }
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier mission", e);
                return false;
            }
        }

        /// <summary>
        /// Someone handed over to outsiders: a prisoner they paid for (<paramref name="prisoner"/>), an animal given back.
        /// They come for them (with <paramref name="pay"/>, set down once they have them) and leave with them; the
        /// prisoner goes back to their own people once off the map. False: nobody comes, and the caller does it the old way.
        /// </summary>
        public static bool Fetch(Map map, Pawn x, Faction to, List<Thing> pay, IntVec3 near, bool prisoner)
        {
            if (map == null || x == null || x.Dead || !x.Spawned || x.Map != map || !On || to == null) return false;
            try
            {
                if (!CanSend(map, to) || Choose(to, map) != to) return false;
                var t = New(CourierKind.Deliver, map, to, Later);
                t.fetch = x;
                t.fetchPrisoner = prisoner;
                t.oldSpot = near;
                t.takeLabel = x.LabelShort;
                t.takeTag = prisoner ? "person" : "animal";
                if (!pay.NullOrEmpty())
                {
                    t.bringLabel = Describe(pay);
                    t.bringTag = TagOf(pay[0].def);
                    foreach (var thing in pay) t.goods.TryAdd(thing, true);
                }
                CourierTracker.Instance.Add(t);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier fetch", e);
                return false;
            }
        }

        /// <summary>
        /// The letter about what a trip brings (a venture come home, a fair's takings), held until they set it down, so
        /// the letter comes when they do. Composed now (the event's lines and tags), as LivingEvents.Tell would.
        /// </summary>
        public static void TellWhenThere(CourierTrip t, HappeningContext ctx, List<string> titles, List<string> texts, IEnumerable<string> tags, LetterDef letter, string item = null)
        {
            if (t == null || ctx == null) return;
            string text = VarietyUtil.Line(texts, ctx, tags, item);
            if (text.NullOrEmpty()) return;
            string title = VarietyUtil.Line(titles, ctx, tags, item) ?? ctx.incident?.LabelCap.ToString() ?? "";
            t.letterLabel = title;
            t.letterText = text;
            t.letterDef = letter ?? LetterDefOf.NeutralEvent;
            int cut = text.IndexOf('\n');
            t.chronicle = title + ": " + (cut > 0 ? text.Substring(0, cut) : text);
            t.chroniclePawn = ctx.a != null && !ctx.a.Dead && ctx.a.IsColonist ? ctx.a : null;
        }

        // ---------------- An errand's courier (PlayOut) ----------------

        private static readonly FloatRange Soon = new FloatRange(0.3f, 1.2f);

        /// <summary>
        /// An errand carries things out to the gate (DilemmaEffect_CarryOut): someone of the people they're for comes and
        /// waits there to take them. Null when nobody can come (the errand then works as before).
        /// </summary>
        public static CourierTrip Await(PlayOut p, Faction f)
        {
            if (p == null || p.map == null || !On) return null;
            try
            {
                if (!CanSend(p.map, f)) return null;
                var t = New(CourierKind.Await, p.map, f, Soon);
                // They come when the errand may start, and give up with it.
                int start = Math.Max(t.due, Now + PlayOutMath.HoursUntil(GenLocalDate.HourOfDay(p.map), p.hourFrom, p.hourTo) * CourierMath.TicksPerHour);
                t.due = start;
                t.giveUp = Math.Max(p.deadline, start + CourierMath.TicksPerHour * 4);
                t.playId = p.id;
                t.what = p.what;
                t.def = p.thing;
                t.takeLabel = Amount(p.amount, p.what, p.thing);
                t.takeTag = TagOf(p.what, p.thing);
                CourierTracker.Instance.Add(t);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier await", e);
                return null;
            }
        }

        /// <summary>
        /// Who the errand hands its things to: the courier waiting at the gate. <paramref name="coming"/>: one is on the
        /// way and the errand should wait for them.
        /// </summary>
        public static Pawn ReceiverFor(PlayOut p, out bool coming)
        {
            coming = false;
            var t = CourierTracker.Instance?.ForPlay(p?.id ?? -1);
            if (t == null || t.settled || t.received) return null;
            if (t.kind != CourierKind.Await) return null;
            if (t.state == CourierState.Pending || t.state == CourierState.Coming)
            {
                coming = true;
                return null;
            }
            // Still setting down what they brought (the price of it): the errand waits until their hands are free.
            if (t.state != CourierState.Working || t.StillCarrying)
            {
                coming = t.state == CourierState.Working;
                return null;
            }
            return t.Bearers.FirstOrDefault();
        }

        /// <summary>
        /// The errand handed <paramref name="n"/> to the courier; <paramref name="rest"/> more were owed. True: the courier
        /// fetches the rest from the stores themselves (the errand takes none).
        /// </summary>
        public static bool Received(PlayOut p, Pawn to, int n, int rest)
        {
            var t = CourierTracker.Instance?.ForPlay(p?.id ?? -1);
            if (t == null || t.settled || to == null || !t.people.Contains(to)) return false;
            t.received = true;
            CourierWords.Say(to, t, "receive");
            if (rest <= 0) return false;
            t.kind = CourierKind.Collect;
            t.what = p.what;
            t.def = p.thing ?? PlayCarry.DefOf(p.what, null);
            t.amount = rest;
            t.taken = 0;
            return true;
        }

        /// <summary>
        /// The errand never went (or never finished): the courier waiting for it, if there is one still to come or at the
        /// gate, fetches what's owed from the stores instead. True: taken over (the errand takes nothing itself).
        /// </summary>
        public static bool TakeOver(PlayOut p)
        {
            var t = CourierTracker.Instance?.ForPlay(p?.id ?? -1);
            if (t == null || t.settled || t.received || !On) return false;
            if (t.state != CourierState.Pending && t.state != CourierState.Coming && t.state != CourierState.Working) return false;
            int left = p.amount - p.tally;
            if (left <= 0 || !t.MapThere) return false;
            t.kind = CourierKind.Collect;
            t.what = p.what;
            t.def = p.thing ?? PlayCarry.DefOf(p.what, null);
            t.amount = left;
            t.taken = 0;
            t.playId = -1;
            return true;
        }

        // ---------------- In person ----------------

        /// <summary>Someone of the event who is here hands the things over: set down at their feet, with a word.</summary>
        private static void ByHand(Pawn bearer, List<Thing> things, Map map)
        {
            var placed = new List<Thing>();
            foreach (var t in things)
                if (GenPlace.TryPlaceThing(t, bearer.Position, map, ThingPlaceMode.Near, out var lying))
                {
                    lying?.SetForbidden(false, false);
                    placed.Add(lying ?? t);
                }
            CourierWords.SayInPerson(bearer, TagOf(things[0].def));
        }

        // ---------------- Words ----------------

        /// <summary>"250 corn", "40 raw food", "150 silver".</summary>
        public static string Amount(int n, string what, ThingDef def) => "CC_Courier_Amount".Translate(n, PlayCarry.Label(what, def)).Resolve();

        /// <summary>"150 silver", "a longsword and 40 steel", "45 silver, 30 steel and 2 more things".</summary>
        public static string Describe(List<Thing> things)
        {
            if (things.NullOrEmpty()) return "";
            var parts = new List<string>();
            foreach (var g in things.Where(t => t != null).GroupBy(t => t.def))
            {
                var first = g.First();
                int n = g.Sum(t => t.stackCount);
                if (first.def.stackLimit > 1 || n > 1) parts.Add("CC_Courier_Amount".Translate(n, first.def.label).Resolve());
                else parts.Add(Find.ActiveLanguageWorker.WithIndefiniteArticle(first.LabelNoCount));
            }
            if (parts.Count > 3)
            {
                int more = parts.Count - 2;
                parts = parts.Take(2).ToList();
                parts.Add("CC_Courier_MoreThings".Translate(more).Resolve());
            }
            return TextUtil.JoinAnd(parts);
        }

        /// <summary>What kind of goods, for what's said: silver, food, medicine, wood, or goods.</summary>
        public static string TagOf(ThingDef def)
        {
            if (def == null) return "goods";
            if (def == ThingDefOf.Silver) return "silver";
            if (def.IsMedicine) return "medicine";
            if (def == ThingDefOf.WoodLog) return "wood";
            if (def.IsNutritionGivingIngestible && !def.IsDrug) return "food";
            return "goods";
        }

        public static string TagOf(string what, ThingDef def)
        {
            switch (what)
            {
                case "silver": return "silver";
                case "rawfood":
                case "meals": return "food";
                case "medicine": return "medicine";
                case "wood": return "wood";
            }
            return TagOf(def ?? PlayCarry.DefOf(what, null));
        }
    }
}
