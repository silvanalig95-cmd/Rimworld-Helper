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
    // Packing up on the map and what stays (CampPackMath): in the last hours of the stay they uninstall their bedrolls,
    // tents and other furniture and carry them; their walls, doors, huts and fire pit stay standing, nobody's (unless
    // their kind takes its own down, or hands it to its employer); and the stay leaves behind what it made, good or bad.
    public partial class Camp
    {
        /// <summary>The ending they are packing up for (time, asked...), while they pack; null otherwise.</summary>
        public string packingFor;
        public int packUntil = -1;
        /// <summary>Packing is done: the next leave is the real one.</summary>
        public bool packed;
        /// <summary>What they left behind as they went, for the leaving message (keys of CampPackMath.Key); not saved, the message goes the same hour.</summary>
        [Unsaved] private List<Leftover> leftNow = new List<Leftover>();
        /// <summary>What still stands of theirs as they go, in words, for the leaving message.</summary>
        [Unsaved] private string standingWords;

        private void ExposePack()
        {
            Scribe_Values.Look(ref packingFor, "packingFor");
            Scribe_Values.Look(ref packUntil, "packUntil", -1);
            Scribe_Values.Look(ref packed, "packed");
        }

        /// <summary>A building of theirs that can be uninstalled and carried (a bedroll, a tent, a stool, a table, a torch of a mod's).</summary>
        public static bool Portable(Building b) => b != null && b.def.Minifiable;

        /// <summary>A bed of theirs given up: nobody of theirs holds it any more.</summary>
        private static void Unclaim(Building b)
        {
            if (!(b is Building_Bed bed)) return;
            foreach (var owner in bed.OwnersForReading.ToList()) owner.ownership?.UnclaimBed();
        }

        /// <summary>What they still have to pack or take down before they go.</summary>
        private List<Building> ToPack()
        {
            bool hands = Here.Any(p => !p.Downed && !p.DevelopmentalStage.Juvenile());
            return Buildings().Where(b => !b.Destroyed && b.Spawned && PackFateOf(b, true, hands) is PackFate f && (f == PackFate.Pack || f == PackFate.TakeDown)).ToList();
        }

        private PackFate PackFateOf(Building b, bool orderly, bool hands) => CampPackMath.Fate(kind.leaves, Portable(b), orderly, hands, ending != "unpaid");

        /// <summary>
        /// Leaving in their own time: they pack first, on the map, for a few hours (CampPackMath.PackHours). False when there
        /// is nothing to pack or no time for it, and they go at once.
        /// </summary>
        private bool StartPacking(string reason)
        {
            int hours = CampPackMath.PackHours(reason);
            if (hours <= 0 || !Settled || !LordAlive || ToPack().Count == 0) return false;
            packingFor = reason;
            // At night they sleep on and pack in the morning.
            int hour = GenLocalDate.HourOfDay(map);
            int wait = CampDayMath.HourOf(hour) == CampHour.Night && CampPackMath.WaitsForMorning(reason) ? (6 - hour + 24) % 24 : 0;
            packUntil = Now + (wait + hours) * GenDate.TicksPerHour;
            SayLine(Here.Where(p => !p.Downed).RandomElementWithFallback(), "pack", 0.8f);
            return true;
        }

        /// <summary>Each hour while they pack: once it's all packed (or their hours are up) they set off.</summary>
        private void WatchPacking()
        {
            if (packingFor == null) return;
            if (ToPack().Count > 0 && Now < packUntil) return;
            string why = packingFor;
            packingFor = null;
            packed = true;
            Leave(why);
        }

        /// <summary>A building of theirs to uninstall, or (for a kind that leaves no trace) to take down, nearest first, nobody else at it.</summary>
        public Job PackJob(Pawn p)
        {
            if (packingFor == null || p.DevelopmentalStage.Juvenile() || p.WorkTagIsDisabled(WorkTags.ManualDumb)) return null;
            var b = ToPack().OrderBy(x => PackFateOf(x, true, true) == PackFate.Pack ? 0 : 1).ThenBy(x => x.Position.DistanceToSquared(p.Position))
                .FirstOrDefault(x => !(x is Building_Bed bed && bed.CurOccupants.Any()) && !Here.Any(o => o != p && o.CurJob?.def == CampDayDefOf.Rimfolk_CampPack && o.CurJob.targetA.Thing == x)
                                     && p.CanReserveAndReach(x, PathEndMode.Touch, Danger.Some));
            return b == null ? null : JobMaker.MakeJob(CampDayDefOf.Rimfolk_CampPack, b);
        }

        /// <summary>
        /// The work at a building done: uninstalled as the game does it (the building in its crate) and carried in the
        /// packer's pack; or taken down, its roof first (a hut's walls hold it up), its materials carried.
        /// </summary>
        public void Packed(Pawn p, Building b)
        {
            if (b == null || b.Destroyed || !b.Spawned || p?.inventory == null) return;
            try
            {
                var fate = PackFateOf(b, true, true);
                if (fate == PackFate.Pack)
                {
                    if (LordAlive) lord.ownedBuildings.Remove(b);
                    Unclaim(b);
                    var mini = MinifyUtility.MakeMinified(b);
                    if (mini == null) return;
                    if (!p.inventory.innerContainer.TryAdd(mini))
                    {
                        if (GenPlace.TryPlaceThing(mini, stash, map, ThingPlaceMode.Near, out var put, null, AtStash)) Own(put);
                    }
                    return;
                }
                if (fate != PackFate.TakeDown) return;
                // The roof over a room comes off before its first wall does.
                var plan = plans.FirstOrDefault(x => x.def == b.def && x.cell == b.Position);
                if (plan != null && (plan.role == "hut" || plan.role == "door"))
                    foreach (var c in CampSite.HutInside(plans, plan.room))
                        if (c.InBounds(map) && map.roofGrid.RoofAt(c) == RoofDefOf.RoofConstructed) map.roofGrid.SetRoof(c, null);
                var costs = Costs(plan ?? new CampPlan { def = b.def, stuff = b.Stuff });
                if (LordAlive) lord.ownedBuildings.Remove(b);
                b.Destroy(DestroyMode.Vanish);
                foreach (var cost in costs)
                {
                    int n = Mathf.CeilToInt(cost.count * 0.75f);
                    if (n <= 0 || cost.thingDef == null) continue;
                    var t = ThingMaker.MakeThing(cost.thingDef);
                    t.stackCount = Mathf.Min(n, cost.thingDef.stackLimit);
                    if (!p.inventory.innerContainer.TryAdd(t) && !t.Destroyed) t.Destroy();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp packing " + b.def.defName, e);
            }
        }

        /// <summary>
        /// As they go: what's packed is in their packs; what's left of theirs (constructions, and furniture nobody had hands or
        /// time to pack) stands nobody's, or the colony's for a camp hired by contract; fires and torches put out. Then what
        /// the stay leaves behind (<see cref="Leftovers"/>).
        /// </summary>
        private void LeaveStanding(bool orderly, IntVec3 fire)
        {
            bool hands = Here.Any(p => !p.Downed && !p.DevelopmentalStage.Juvenile());
            var carriers = Here.Where(p => p.inventory != null && !p.DevelopmentalStage.Juvenile()).ToList();
            // Going at once in their own time (settlers off with the colony's caravan): they pack as they go.
            bool atOnce = orderly && !packed && CampPackMath.PackHours(ending) == 0;
            var standing = new List<string>();
            bool handed = false;
            foreach (var b in Buildings())
            {
                if (b.Destroyed) continue;
                try
                {
                    var fate = PackFateOf(b, orderly, hands);
                    Unclaim(b);
                    if (fate == PackFate.Pack && atOnce && carriers.Count > 0)
                    {
                        if (LordAlive) lord.ownedBuildings.Remove(b);
                        var mini = MinifyUtility.MakeMinified(b);
                        if (mini != null && !carriers.RandomElement().inventory.innerContainer.TryAdd(mini) && !mini.Destroyed) mini.Destroy();
                        continue;
                    }
                    // A fire or a torch left standing is put out first.
                    var fuel = b.TryGetComp<CompRefuelable>();
                    if (fuel != null && fuel.HasFuel && orderly) fuel.ConsumeFuel(fuel.Fuel);
                    if (LordAlive) lord.ownedBuildings.Remove(b);
                    // Furniture nobody got to pack in time stays where it is, with the rest.
                    b.SetFaction(fate == PackFate.Hand ? Faction.OfPlayer : null);
                    handed |= fate == PackFate.Hand;
                    var plan = plans.FirstOrDefault(x => x.def == b.def && x.cell == b.Position);
                    string what = StandingWord(plan, b, fate == PackFate.Pack);
                    if (what != null && !standing.Contains(what)) standing.Add(what);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp left standing " + b.def.defName, e);
                }
            }
            standingKey = handed ? "CC_CampLeft_StandingColony" : "CC_CampLeft_Standing";
            standingWords = standing.Count == 0 ? null : TextUtil.JoinAnd(standing.Take(6).ToList());
        }

        [Unsaved] private string standingKey = "CC_CampLeft_Standing";

        /// <summary>A building left standing in the leaving message's words: their hut, their tents, a windbreak, the fire pit...</summary>
        private string StandingWord(CampPlan plan, Building b, bool unpacked)
        {
            string role = plan?.role;
            if (unpacked && (role == "bed" || role == "hutbed")) return "CC_CampLeft_What_beds".Translate();
            switch (role)
            {
                case "hut":
                case "door":
                case "hutbed":
                    return (kind.sleep == CampSleepForm.Hut ? "CC_CampLeft_What_hut" : "CC_CampLeft_What_tents").Translate();
                case "butcher":
                case "craft":
                    return "CC_CampLeft_What_work".Translate();
                case "table":
                case "seat":
                    return "CC_CampLeft_What_dining".Translate();
                case "spot":
                    return "CC_CampLeft_What_spot".Translate(b.def.label.Named("THING"));
                case "fire":
                case "wall":
                case "light":
                case "picket":
                case "stash":
                case "stall":
                    return ("CC_CampLeft_What_" + role).Translate();
                default:
                    return b.def.label;
            }
        }

        // ---------------- What the stay leaves behind ----------------

        /// <summary>Hot and dry now: no rain falling, the grass dry with heat (for embers).</summary>
        private bool HotDry() => map.mapTemperature.OutdoorTemp >= 26f && map.weatherManager.RainRate < 0.01f && map.weatherManager.SnowRate < 0.01f;

        /// <summary>
        /// What the stay leaves (CampPackMath.Leaves), from what happened on the map: a woodpile of logs they cut, hides of
        /// their kills, a cairn of the stone they cut, a gift at the colony's edge; refuse round the old fire, offal that draws
        /// a predator, embers in the dry grass, a lame beast. Each where it lies, and named in the leaving message.
        /// </summary>
        private void Leftovers(bool orderly, IntVec3 fire)
        {
            leftNow.Clear();
            try
            {
                var goods = Goods().Where(t => !IsProp(t)).ToList();
                int wood = goods.Where(t => t.def == ThingDefOf.WoodLog).Sum(t => t.stackCount);
                int hides = goods.Where(t => t.def.IsLeather).Sum(t => t.stackCount);
                int stone = goods.Where(t => CampTake.StoneChunk(t.def)).Sum(t => t.stackCount);
                int warm = tradeDays * 2 + helps + gifts + Mathf.Min(warmth.Values.Sum(), 10);
                var facts = new StayFacts
                {
                    orderly = orderly, days = DaysHere, people = members.Count, tension = tension, warmth = warm, butchered = beastsDressed, felled = treesFelled, mined = rockMined,
                    woodLeft = wood, hidesLeft = hides, stoneLeft = stone, hotDry = HotDry(), lameBeast = herd.Any(a => a != null && a.Spawned && !a.Dead && GroupOf(a)?.follow != true),
                    calling = l => kind.leftovers == null || kind.leftovers.Contains(l)
                };
                foreach (var l in CampPackMath.Leaves(facts, _ => Rand.Value))
                    if (MakeLeftover(l, fire, goods)) leftNow.Add(l);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp leftovers", e);
            }
        }

        private bool MakeLeftover(Leftover l, IntVec3 fire, List<Thing> goods)
        {
            switch (l)
            {
                case Leftover.Woodpile: return LeaveGoodsAt(goods.Where(t => t.def == ThingDefOf.WoodLog), fire, 0.6f);
                case Leftover.Hides: return LeaveGoodsAt(goods.Where(t => t.def.IsLeather), share, 0.5f);
                case Leftover.Cairn: return LeaveGoodsAt(goods.Where(t => CampTake.StoneChunk(t.def)), fire, 1f);
                case Leftover.Gift:
                {
                    var def = ThingRoles.From(ThingRole.Gift, faction, map, ThingRoles.Seed("camp leftover", kind.defName, faction, map)) ?? ThingDefOf.Silver;
                    var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    t.stackCount = Mathf.Clamp(ThingRoles.CountFor(Rand.RangeInclusive(60, 120), def, 1), 1, def.stackLimit);
                    if (!GenPlace.TryPlaceThing(t, share, map, ThingPlaceMode.Near, out var put)) return false;
                    put.SetForbidden(false, false);
                    return true;
                }
                case Leftover.Refuse:
                {
                    var trash = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Trash") ?? ThingDefOf.Filth_Dirt;
                    int n = 0;
                    foreach (var c in GenRadial.RadialCellsAround(fire, 4.5f, true).InRandomOrder().Take(10))
                        if (c.InBounds(map) && c.Standable(map) && FilthMaker.TryMakeFilth(c, map, Rand.Chance(0.3f) ? ThingDefOf.Filth_Ash : trash)) n++;
                    return n > 0;
                }
                case Leftover.Offal: return Offal(goods);
                case Leftover.Embers: return Embers(fire);
                case Leftover.LameBeast: return LameBeast();
            }
            return false;
        }

        /// <summary>Some of their goods left lying at a spot for anyone: no longer theirs, no longer forbidden.</summary>
        private bool LeaveGoodsAt(IEnumerable<Thing> things, IntVec3 at, float share)
        {
            bool any = false;
            foreach (var t in things.ToList())
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(t.stackCount * share));
                var part = n >= t.stackCount ? t : t.SplitOff(n);
                if (part.Spawned) part.DeSpawn();
                Disown(part);
                if (GenPlace.TryPlaceThing(part, at, map, ThingPlaceMode.Near, out var put) && put != null)
                {
                    put.SetForbidden(false, false);
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// The offal of their butchering left at the butcher's spot: blood, and meat they didn't want, rotting; it draws a
        /// predator of the land in, which the game's own beast then does as it will (through the shared harm budget: campLeft).
        /// </summary>
        private bool Offal(List<Thing> goods)
        {
            var at = KillSpot.IsValid ? KillSpot : FireCell;
            for (int i = 0; i < 4; i++) FilthMaker.TryMakeFilth(CellFinder.RandomClosewalkCellNear(at, map, 2), map, ThingDefOf.Filth_Blood);
            var meat = goods.Where(t => t.def.IsMeat).OrderBy(t => t.stackCount).FirstOrDefault();
            if (meat != null) LeaveGoodsAt(new[] { meat }, at, 0.4f);
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, CampPackMath.OffalDrawChance)) return true;
            var biome = map.Biome;
            var kind = biome == null ? null : BiomeAnimals.Of(biome).Where(k => k.RaceProps.predator && k.RaceProps.baseBodySize <= 1.4f && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race))
                .RandomElementByWeightWithFallback(k => BiomeAnimals.Commonality(biome, k));
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal)) return true;
            var beast = PawnGenerator.GeneratePawn(kind);
            GenSpawn.Spawn(beast, entry, map);
            if (beast.needs?.food != null) beast.needs.food.CurLevelPercentage = 0.2f;
            HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
            Note(Text("CC_CampLeft_Drawn", null, null, 0, beast.LabelIndefinite()), MessageTypeDefOf.NegativeEvent, Importance.Notable, beast, true);
            return true;
        }

        /// <summary>A fire not quite out in hot, dry weather: the grass by it catches, and the game's own fire does the rest (the shared harm budget: campLeft).</summary>
        private bool Embers(IntVec3 fire)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, CampPackMath.EmbersCatchChance)) return false;
            foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(fire, map)).InRandomOrder())
            {
                if (!c.InBounds(map) || c.GetPlant(map) == null || (map.areaManager.Home?[c] ?? false)) continue;
                if (FireUtility.TryStartFireIn(c, map, 0.2f, null))
                {
                    HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
                    return true;
                }
            }
            return false;
        }

        /// <summary>One of their beasts too lame for the road, left for the colony: a leg hurt (it heals with care).</summary>
        private bool LameBeast()
        {
            var beast = herd.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed && GroupOf(a)?.follow != true).RandomElementWithFallback();
            if (beast == null) return false;
            var leg = beast.health.hediffSet.GetNotMissingParts().Where(x => x.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)).RandomElementWithFallback();
            beast.GetLord()?.RemovePawn(beast);
            herd.Remove(beast);
            beast.SetFaction(Faction.OfPlayer);
            if (leg != null)
            {
                var hurt = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamedSilentFail("Bruise") ?? HediffDefOf.Cut, beast, leg);
                hurt.Severity = Mathf.Min(leg.def.GetMaxHealth(beast) * 0.5f, 6f);
                beast.health.AddHediff(hurt, leg);
            }
            return true;
        }

        /// <summary>The leaving message's lines for what was left: what still stands, and each leftover.</summary>
        private string LeftWords()
        {
            var parts = new List<string>();
            if (!standingWords.NullOrEmpty()) parts.Add(Text(standingKey ?? "CC_CampLeft_Standing", null, null, 0, standingWords));
            foreach (var l in leftNow)
            {
                string key = "CC_CampLeft_" + CampPackMath.Key(l);
                if (key.CanTranslate()) parts.Add(Text(key));
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }
    }
}
