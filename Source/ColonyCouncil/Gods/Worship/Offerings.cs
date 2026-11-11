using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Offerings (docs/design/gods.md, "Worship": shrines and offerings; brainstorm 11). Now and then (WorshipMath.OfferingChance,
    /// once a day a place) someone carries something of the god's liking from the stores to a place dedicated to it: food
    /// for the harvest and the hearth, a hunt's best pelt for the hunter, a weapon for war, silver for the road, components
    /// for the machine, anything for the Hunger (WorshipMath.Likes, from every mod's things by what they are). It is set down
    /// beside the place, forbidden, with a word to the god. Then, at the step of how real the gods are: Walking, the god takes
    /// it before everyone's eyes; Felt, it is gone in the small hours and nobody saw it go; Believed, an animal comes and eats
    /// it, someone passing takes it with them, or it simply spoils or goes back to the stores, and the colony says the god sent
    /// them. The favour comes then, by its worth, how well it suits the god and its nature (WorshipMath.OfferingFavour).
    /// Archotechs take none: they judge deeds.
    /// </summary>
    public static class Offerings
    {
        /// <summary>A planned offering waits this many hours for someone free to carry it.</summary>
        public const int PlanHours = 18;

        /// <summary>At most this many offerings carried a day at a home.</summary>
        public const int ADay = 2;

        // ================= What a thing is, as a god sees it =================

        public static OfferingKind Kind(ThingDef d)
        {
            if (d == null) return OfferingKind.Other;
            try
            {
                if (d.IsWeapon) return OfferingKind.Weapon;
                if (d.IsApparel) return OfferingKind.Apparel;
                if (d.IsMedicine) return OfferingKind.Medicine;
                if (d == ThingDefOf.Silver || d == ThingDefOf.Gold) return OfferingKind.Silver;
                if (d == ThingDefOf.ComponentIndustrial || d == ThingDefOf.ComponentSpacer) return OfferingKind.Component;
                if (d == ThingDefOf.WoodLog) return OfferingKind.Wood;
                if (d.IsLeather) return OfferingKind.Pelt;
                if (ThingCategoryDefOf.Fish != null && d.IsWithinCategory(ThingCategoryDefOf.Fish)) return OfferingKind.Fish;
                if (d.IsMeat) return d.defName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0 ? OfferingKind.Fish : OfferingKind.Meat;
                if (d.ingestible != null)
                {
                    if ((d.ingestible.foodType & FoodTypeFlags.Liquor) != 0) return OfferingKind.Drink;
                    if (d.IsDrug) return OfferingKind.Drug;
                    if (d.ingestible.IsMeal) return OfferingKind.Meal;
                    if (d.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw)) return OfferingKind.Plant;
                }
                if (d.IsArt || d.HasComp(typeof(CompArt))) return OfferingKind.Art;
                if (d.IsWithinCategory(ThingCategoryDefOf.Textiles)) return OfferingKind.Textile;
                if (d.IsStuff && d.stuffProps.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)) return OfferingKind.Metal;
                if (d.HasComp(typeof(CompQuality)) || d.IsWithinCategory(ThingCategoryDefOf.Manufactured)) return OfferingKind.Craft;
            }
            catch
            {
            }
            return OfferingKind.Other;
        }

        /// <summary>Never offered: the dead, the flesh of people or insects, chunks, what's rotting, poisoned or a building's.</summary>
        private static bool NeverOffered(Thing t)
        {
            var d = t.def;
            if (d.IsCorpse || d.category != ThingCategory.Item || d.IsWithinCategory(ThingCategoryDefOf.Chunks)) return true;
            var src = d.ingestible?.sourceDef?.race;
            if (src != null && (src.Humanlike || src.FleshType == FleshTypeDefOf.Insectoid)) return true;
            if (FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t)) return true;
            var rot = t.TryGetComp<CompRottable>();
            if (rot != null && rot.Stage != RotStage.Fresh) return true;
            if (t is Apparel a && a.WornByCorpse) return true;
            if (t is MinifiedThing) return true;
            return false;
        }

        /// <summary>The colony keeps enough of it that a gift doesn't hurt: three times what is given, and for medicine, meals, silver and components a store besides.</summary>
        private static bool Spare(Map map, ThingDef d, int count, int colonists)
        {
            int have = 0;
            var list = map.listerThings.ThingsOfDef(d);
            for (int i = 0; i < list.Count; i++) have += list[i].stackCount;
            int keep = count * 3;
            var k = Kind(d);
            if (k == OfferingKind.Medicine) keep = Math.Max(keep, 4 * colonists);
            else if (k == OfferingKind.Meal) keep = Math.Max(keep, 2 * colonists);
            else if (k == OfferingKind.Silver) keep = Math.Max(keep, 300);
            else if (k == OfferingKind.Component) keep = Math.Max(keep, 12);
            return have >= keep;
        }

        /// <summary>How many of a stack make a fair gift (WorshipMath.OfferingValueTarget), at least one, and never past the cap.</summary>
        private static int CountFor(Thing t, int colonists)
        {
            float unit = Mathf.Max(0.01f, t.MarketValue);
            float target = WorshipMath.OfferingValueTarget(colonists);
            int n = Mathf.Clamp(Mathf.RoundToInt(target / unit), 1, t.stackCount);
            while (n > 1 && unit * n > WorshipMath.OfferingValueCap(colonists)) n--;
            return n;
        }

        /// <summary>
        /// The best thing in the stores to offer this god now (and how many), for this carrier to reach: of the god's liking
        /// first (or of the demanded kind), spare, fresh, worth a gift and not a fortune.
        /// </summary>
        public static Thing Pick(Map map, God g, Pawn carrier, OfferingKind? only, out int count, out float fit)
        {
            count = 0;
            fit = 0f;
            if (map == null || g == null || carrier == null) return null;
            int colonists = Math.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            float cap = WorshipMath.OfferingValueCap(colonists);
            Thing best = null;
            float bestScore = 0f;
            foreach (var group in map.haulDestinationManager.AllGroupsListForReading)
            {
                foreach (var t in group.HeldThings)
                {
                    if (t == null || !t.Spawned || NeverOffered(t) || t.IsForbidden(Faction.OfPlayer)) continue;
                    var k = Kind(t.def);
                    if (only.HasValue && k != only.Value) continue;
                    float f = GodWorshipDef.Fit(g, k);
                    if (f <= 0f || f < 0.9f && g.kind != GodKind.Hunger && !only.HasValue) continue;
                    if (t.MarketValue > cap || t.MarketValue * t.stackCount < WorshipMath.OfferingValueMin) continue;
                    int n = CountFor(t, colonists);
                    if (!Spare(map, t.def, n, colonists)) continue;
                    float score = f * (0.8f + Rand.Value * 0.4f);
                    if (score <= bestScore) continue;
                    if (!carrier.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some, 1, n)) continue;
                    best = t;
                    bestScore = score;
                    count = n;
                    fit = f;
                }
            }
            return best;
        }

        // ================= The day: who brings what =================

        /// <summary>The region's day: each place dedicated to a god that takes offerings may have one carried to it today (a demand always).</summary>
        public static void Daily(WorshipBook b, Map map, int now)
        {
            int planned = 0;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            if (colonists == 0) return;
            var priest = Worship.Priest(map);
            int devout = map.mapPawns.FreeColonistsSpawned.Count(p => Worship.Ours(p) && Worship.Devout(p));
            foreach (var s in b.shrines.Where(x => x != null && !x.pending && x.Live && x.thing.Map == map).ToList())
            {
                if (planned >= ADay) break;
                var g = Gods.ById(s.god);
                if (g == null || !WorshipMath.TakesOfferings(g.kind)) continue;
                // One at a time at a place.
                if (b.offerings.Any(o => o != null && o.shrine == s.thing && o.fate == OfferingFate.Waiting)) continue;
                bool demanded = b.demands.Any(d => d != null && !d.done && !d.met && d.god == g.id && d.home == Gods.HomeKey(map));
                if (!demanded && !Rand.Chance(WorshipMath.OfferingChance(devout, priest != null, Worship.IsHonoured(g, map)))) continue;
                b.offerings.Add(new OfferingRecord { shrine = s.thing, god = g.id, home = Gods.HomeKey(map), chosen = now, demand = demanded });
                planned++;
            }
        }

        // ================= The hour: carrying, and the taking =================

        public static void Hourly(WorshipBook b, Map map, int now)
        {
            int key = Gods.HomeKey(map);
            foreach (var o in b.offerings.Where(x => x != null && x.home == key && x.fate == OfferingFate.Waiting).ToList())
            {
                if (o.shrine == null || o.shrine.Destroyed || !o.shrine.Spawned)
                {
                    Forget(b, o);
                    continue;
                }
                if (!o.set)
                {
                    if (now - o.chosen > PlanHours * GenDate.TicksPerHour)
                    {
                        Forget(b, o);
                        continue;
                    }
                    if (o.carrier == null) Send(b, o, map);
                    continue;
                }
                if (o.thing == null || o.thing.Destroyed)
                {
                    // Gone from the place (Believed): eaten where it lay, or carried off.
                    Settle(b, o, map, o.kind == OfferingKind.Meal || o.kind == OfferingKind.Plant || o.kind == OfferingKind.Meat || o.kind == OfferingKind.Fish ? OfferingFate.Animal : OfferingFate.Passerby);
                    continue;
                }
                if (!o.thing.Spawned)
                {
                    // In someone's pack: a passer-by took it with them.
                    Settle(b, o, map, OfferingFate.Passerby);
                    continue;
                }
                Look(b, o, map, now);
            }
        }

        private static void Forget(WorshipBook b, OfferingRecord o)
        {
            if (o.thing != null && o.thing.Spawned && o.set) o.thing.SetForbidden(false, false);
            b.offerings.Remove(o);
        }

        /// <summary>Someone free carries the planned offering: the priest first, else whoever holds the god (the dreamer of a demand first), else the devout.</summary>
        private static void Send(WorshipBook b, OfferingRecord o, Map map)
        {
            var g = Gods.ById(o.god);
            if (g == null) return;
            var demand = o.demand ? b.demands.FirstOrDefault(d => d != null && !d.done && !d.met && d.god == g.id && d.home == o.home) : null;
            var who = new List<Pawn>();
            if (demand?.dreamer != null) who.Add(demand.dreamer);
            var priest = Worship.Priest(map);
            if (priest != null) who.Add(priest);
            who.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p => Worship.GodOf(p) == g).OrderByDescending(Worship.Devout));
            foreach (var p in who.Distinct())
            {
                if (p == null || p.Map != map || !Worship.Ours(p) || p.DevelopmentalStage.Baby() || Worship.Scoffer(p) || !WorshipJobs.Free(p)) continue;
                if (!p.CanReach(o.shrine, PathEndMode.Touch, Danger.Some)) continue;
                var t = Pick(map, g, p, demand != null ? demand.kind : (OfferingKind?)null, out int count, out float fit);
                if (t == null) continue;
                var def = WorshipJobs.Def(WorshipJobs.OfferJob);
                if (def == null) return;
                var job = JobMaker.MakeJob(def, t, o.shrine);
                job.count = count;
                if (!WorshipJobs.Start(p, job)) continue;
                o.carrier = p;
                o.thing = t;
                o.kind = Kind(t.def);
                o.label = GenLabel.ThingLabel(t.def, t.Stuff, count);
                int quality = t.TryGetQuality(out var q) ? (int)q : -1;
                float worth = t.MarketValue * count;
                o.favour = WorshipMath.OfferingFavour(worth, fit, WorshipMath.NatureFit(g.nature, o.kind, quality, Rand.Value), Worship.IsHonoured(g, map),
                    Worship.IsPriest(p));
                return;
            }
        }

        /// <summary>The god of an offering being carried (for the job's report).</summary>
        public static God GodOf(Thing t, Thing shrine)
        {
            var b = Worship.Book;
            var o = b?.offerings.FirstOrDefault(x => x != null && x.fate == OfferingFate.Waiting && (x.thing == t && t != null || x.shrine == shrine && !x.set));
            return o != null ? Gods.ById(o.god) : Shrines.GodAt(shrine);
        }

        /// <summary>The carrier gave up on the way (danger, a need): the offering waits for another.</summary>
        public static void Dropped(Pawn p, Thing t)
        {
            var b = Worship.Book;
            var o = b?.offerings.FirstOrDefault(x => x != null && !x.set && x.carrier == p);
            if (o == null) return;
            o.carrier = null;
            o.thing = null;
        }

        /// <summary>A free cell beside the place to set an offering on: standable, empty, not a stockpile's, reachable; the one in front first.</summary>
        public static IntVec3 DropCell(Thing shrine, Pawn p)
        {
            var map = shrine.Map;
            IntVec3 best = IntVec3.Invalid;
            float bestD = float.MaxValue;
            foreach (var c in GenAdj.CellsAdjacent8Way(shrine))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.GetFirstItem(map) != null || c.GetSlotGroup(map) != null) continue;
                if (c.GetEdifice(map) != null && !(c.GetEdifice(map) is Building_Door) && c.GetEdifice(map).def.passability != Traversability.Standable) continue;
                if (!p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float d = c.DistanceToSquared(p.Position) - (shrine.def.hasInteractionCell && c == shrine.InteractionCell ? 1000f : 0f);
                if (d >= bestD) continue;
                best = c;
                bestD = d;
            }
            return best;
        }

        /// <summary>The offering is set down at the place: left forbidden, a word to the god, and the carrier's small moment.</summary>
        public static void Placed(Pawn p, Thing lying, Thing shrine)
        {
            var b = Worship.Book;
            var o = b?.offerings.FirstOrDefault(x => x != null && !x.set && x.carrier == p && x.shrine == shrine);
            if (o == null || lying == null) return;
            o.thing = lying;
            o.set = true;
            o.placed = Worship.Now;
            lying.SetForbidden(true, false);
            var g = Gods.ById(o.god);
            Worship.Say(p, "Rimfolk_Pool_Worship_Offer", g, null, "offer:" + o.kind.ToString().ToLowerInvariant(), o.demand ? "demand" : null);
            Worship.PriestLight(p, shrine.Position);
            CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Offered);
        }

        // ================= The taking =================

        private static void Look(WorshipBook b, OfferingRecord o, Map map, int now)
        {
            var g = Gods.ById(o.god);
            if (g == null)
            {
                Forget(b, o);
                return;
            }
            float hours = (now - o.placed) / (float)GenDate.TicksPerHour;
            var r = Gods.Realness;
            if (r != GodRealness.Believed)
            {
                if (WorshipMath.GodTakesNow(r, GenLocalDate.HourOfDay(map), hours)) Take(b, o, map, g);
                return;
            }
            // Believed: who comes for it, or it spoils or goes back. Someone already on their way to it: wait and see.
            bool food = o.thing.def.IsNutritionGivingIngestible;
            if (Coming(map, o.thing)) return;
            Pawn animal = food && hours >= 3f ? Animal(map, o.thing) : null;
            Pawn passer = hours >= 3f ? Passerby(map, o.thing) : null;
            var fate = WorshipMath.BelievedFate(food, animal != null, passer != null, hours / 24f);
            switch (fate)
            {
                case OfferingFate.Animal:
                    var eat = JobMaker.MakeJob(JobDefOf.Ingest, o.thing);
                    eat.count = o.thing.stackCount;
                    animal.jobs.StartJob(eat, JobCondition.InterruptForced, null, false, true);
                    break;
                case OfferingFate.Passerby:
                    var take = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, o.thing);
                    take.count = o.thing.stackCount;
                    passer.jobs.StartJob(take, JobCondition.InterruptForced, null, false, true);
                    break;
                case OfferingFate.Spoiled:
                case OfferingFate.Returned:
                    Settle(b, o, map, fate);
                    break;
            }
        }

        /// <summary>Someone not of the colony already on their way to it (to eat it, to take it).</summary>
        private static bool Coming(Map map, Thing t)
        {
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (p != null && p.Faction != Faction.OfPlayer && p.CurJob != null && p.CurJob.targetA.Thing == t) return true;
            return false;
        }

        /// <summary>A wild animal near enough that would eat it (never a hunting predator for anything but meat), or null.</summary>
        private static Pawn Animal(Map map, Thing t)
        {
            foreach (var a in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (a?.RaceProps == null || !a.RaceProps.Animal || a.Faction != null || a.Downed || a.InMentalState || a.Dead) continue;
                if (!a.Position.InHorDistOf(t.Position, 45f) || !a.RaceProps.CanEverEat(t) || !a.WillEat(t)) continue;
                if (!a.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return a;
            }
            return null;
        }

        /// <summary>Someone passing on the map (a trader, a visitor, a traveller; never the colony's own, a prisoner or an enemy), or null.</summary>
        private static Pawn Passerby(Map map, Thing t)
        {
            foreach (var p in map.mapPawns.AllHumanlikeSpawned.ToList())
            {
                if (p == null || p.Faction == null || p.Faction.IsPlayer || p.HostileTo(Faction.OfPlayer) || p.IsPrisoner || p.Downed || p.InMentalState) continue;
                if (!p.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return p;
            }
            return null;
        }

        /// <summary>Felt and Walking: the god takes it. Gone from the place, with the god's sign at its step, and the favour.</summary>
        private static void Take(WorshipBook b, OfferingRecord o, Map map, God g)
        {
            var cell = o.thing.Position;
            o.thing.Destroy(DestroyMode.Vanish);
            o.thing = null;
            Settle(b, o, map, OfferingFate.Taken, cell);
        }

        private static void Settle(WorshipBook b, OfferingRecord o, Map map, OfferingFate fate, IntVec3 cell = default)
        {
            var g = Gods.ById(o.god);
            o.fate = fate;
            if (g == null) return;
            if (!cell.IsValid || cell == default) cell = o.shrine?.Position ?? IntVec3.Invalid;
            if ((fate == OfferingFate.Spoiled || fate == OfferingFate.Returned) && o.thing != null && o.thing.Spawned) o.thing.SetForbidden(false, false);
            float share = fate == OfferingFate.Spoiled ? 0.5f : fate == OfferingFate.Returned ? 0.25f : 1f;
            string place = Worship.ShrineOf(g);
            string what = o.label ?? "?";
            string why = "CC_Wsh_Why_Offering".Translate(what.Named("WHAT"), place.Named("SHRINE")).Resolve();
            Gods.AddFavour(g, map, o.favour * share, why, FavourSource.Offering);
            string key = "CC_Wsh_Taken_" + fate + (fate == OfferingFate.Taken ? "_" + Worship.Step : "");
            string text = key.Translate(what.Named("WHAT"), place.Named("SHRINE"), Gods.FullName(g).Named("GOD"), (o.carrier?.LabelShort ?? "?").Named("PAWN")).Resolve();
            if (fate == OfferingFate.Taken && Gods.Realness == GodRealness.Walking)
                Worship.Sign(g, map, cell, PresenceKind.Act, "CC_Wsh_Taken_Title".Translate(Gods.Name(g).Named("GOD")).Resolve(), text, new LookTargets(cell, map), false);
            else
            {
                if (fate == OfferingFate.Taken) Worship.Glow(map, cell, g);
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, text, o.shrine != null && o.shrine.Spawned ? new LookTargets(o.shrine) : null);
                Gods.Remember(new GodAct { god = g.id, home = o.home, kind = GodActKind.Favour, text = text, cell = cell });
            }
            // A demand in a dream met.
            if (o.demand) GodDreams.Met(b, g, map, o);
            // A scoffer near a believer has something to say about it.
            if (fate == OfferingFate.Taken) FaithHeart.Witnessed(map, g, cell);
        }
    }
}
