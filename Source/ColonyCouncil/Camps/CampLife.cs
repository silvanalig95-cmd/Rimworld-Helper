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
    // How a camp lives from hour to hour: arriving and unpacking, building, the day's chores and events, and leaving.
    public partial class Camp
    {
        // ---------------- Every few seconds ----------------

        public void Tick250()
        {
            if (phase == CampPhase.Over) return;
            // Their animals taken out of the group before a flight or a fight go off on their own (CampLook.cs).
            if (herd.Count > 0) LeadOffStrays();
            if (!LordAlive)
            {
                Finish();
                return;
            }
            var toil = lord.CurLordToil;
            if (toil is LordToil_Camp && phase == CampPhase.Coming) Arrived();
            else if (toil is LordToil_DefendPoint && phase != CampPhase.Fighting) Fighting();
            else if (toil is LordToil_ExitMapAndDefendSelf && phase != CampPhase.Leaving && phase != CampPhase.Fighting) Scattered();
            if (Settled)
            {
                WatchHunts();
                WatchTrees();
            }
        }

        /// <summary>Hunts that ended: the kill counted, and the hunter kept from carrying it off to the colony's stores.</summary>
        private void WatchHunts()
        {
            foreach (var animal in prey.ToList())
            {
                if (animal == null)
                {
                    prey.Remove(animal);
                    continue;
                }
                var hunter = Here.FirstOrDefault(p => p.CurJobDef == JobDefOf.Hunt && (p.CurJob.targetA.Thing == animal || (animal.Dead && p.CurJob.targetA.Thing == animal.Corpse)));
                if (animal.Dead)
                {
                    prey.Remove(animal);
                    kills.Add(animal);
                    animalsHunted++;
                    dayHunts++;
                    AddTension(termShare && !skipShare ? 1.5f : 3f);
                    // The vanilla hunt ends by hauling the body to storage; theirs goes to the camp instead.
                    if (hunter != null)
                    {
                        hunter.jobs.EndCurrentJob(JobCondition.Succeeded);
                        SayMoment(hunter, "kill", 0.6f);
                    }
                    continue;
                }
                if (hunter == null || !animal.Spawned || animal.Map != map)
                {
                    // Nobody after it any more, or it's gone from the map (fled off it, carried away).
                    prey.Remove(animal);
                    if (hunter != null) hunter.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    continue;
                }
                // Never into the colony after it, and never with any of the colony's people or animals near the line of
                // fire: a stray shot mustn't hit them.
                var home = map.areaManager.Home;
                if ((home != null && home[hunter.Position]) || !ClearOfColony(animal.Position) || ColonyInLineOfFire(hunter.Position, animal.Position))
                {
                    prey.Remove(animal);
                    hunter.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }
            foreach (var k in kills.ToList())
            {
                var corpse = k.Corpse;
                if (corpse == null || !corpse.Spawned || Owns(corpse) || Now - (k.Corpse?.timeOfDeath ?? Now) > GenDate.TicksPerDay * 2) kills.Remove(k);
            }
        }

        /// <summary>Any of the colony's people or animals near the prey, or close to the line between hunter and prey.</summary>
        public bool ColonyInLineOfFire(IntVec3 from, IntVec3 to)
        {
            foreach (var p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                var c = p.Position;
                if (c.InHorDistOf(to, 12f)) return true;
                // Distance from the line: a pawn in the way could take a shot meant for the animal.
                float dx = to.x - from.x, dz = to.z - from.z, len = dx * dx + dz * dz;
                float t = len <= 0f ? 0f : Mathf.Clamp01(((c.x - from.x) * dx + (c.z - from.z) * dz) / len);
                float ex = from.x + t * dx - c.x, ez = from.z + t * dz - c.z;
                if (ex * ex + ez * ez <= 9f) return true;
            }
            return false;
        }

        private void WatchTrees()
        {
            foreach (var t in trees.ToList())
            {
                if (t == null)
                {
                    trees.Remove(t);
                    continue;
                }
                if (t.Destroyed || !t.Spawned)
                {
                    trees.Remove(t);
                    felled.Add(t.Position);
                    felledTicks.Add(Now);
                    treesFelled++;
                    dayChops++;
                    AddTension(termBoundary && bendBoundary ? 2.5f : 1f);
                    continue;
                }
                if (!Here.Any(p => p.CurJob != null && p.CurJob.targetA.Thing == t)) trees.Remove(t);
            }
            for (int i = felled.Count - 1; i >= 0; i--)
                if (Now - (i < felledTicks.Count ? felledTicks[i] : 0) > GenDate.TicksPerDay * 2)
                {
                    felled.RemoveAt(i);
                    if (i < felledTicks.Count) felledTicks.RemoveAt(i);
                }
        }

        // ---------------- Every hour ----------------

        public void Hourly()
        {
            if (phase == CampPhase.Over) return;
            if (!LordAlive)
            {
                Finish();
                return;
            }
            WatchMembers();
            KeepHerd();
            if (phase == CampPhase.Coming)
            {
                // Lost on the way: they give up and go.
                if (Now - startTick > GenDate.TicksPerDay * 1.5f) Leave("lost");
                return;
            }
            if (LeavingTooLong())
            {
                SendOff();
                return;
            }
            if (!Settled) return;
            int day = GenDate.DaysPassed;
            if (day != dayKey) NewDay(day);
            Upkeep();
            WatchRepair();
            WatchErrand();
            if (phase == CampPhase.Building) CheckBuilt();
            WatchTrade();
            if (Ends()) return;
            CheckTension();
            if (!Settled) return;
            if (phase == CampPhase.Living && lastEventDay != day && GenLocalDate.HourOfDay(map) >= eventHour)
            {
                lastEventDay = day;
                // The stay's own shape first (CampStory.cs): the ask, the trouble; else the day's chance event.
                if (!StoryBeat()) DailyEvent();
            }
            StoryParting();
            Visitors();
            if (Rand.Chance(0.12f) && !kind.lines.NullOrEmpty())
            {
                var speaker = Here.Where(p => p.Awake() && !p.Downed).RandomElementWithFallback();
                if (speaker != null) Arrivals.Say(speaker, Flavor.PickText(kind.lines));
            }
        }

        /// <summary>A colonist came over to the camp in their free time: a chat with whoever is nearest.</summary>
        public void Visited(Pawn c)
        {
            if (c == null || !Settled) return;
            var m = Here.Where(x => x.Awake() && !x.Downed && !x.InMentalState).OrderBy(x => x.Position.DistanceToSquared(c.Position)).FirstOrDefault();
            try
            {
                if (m != null && c.interactions != null && Together.Can(c, m)) c.interactions.TryInteractWith(m, InteractionDefOf.Chitchat);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp visit", e);
            }
            Warm(c, 1);
            CouncilRecords.Add(c, CouncilRecords.CampEvenings);
        }

        public void Warm(Pawn colonist, int amount)
        {
            if (colonist == null || !colonist.IsColonist) return;
            warmth[colonist] = (warmth.TryGetValue(colonist, out int w) ? w : 0) + amount;
        }

        /// <summary>
        /// Colonists who come by the camp (out hunting or hauling, or just passing) stop and talk with whoever is about:
        /// the game's own chats, so friendships (and dislikes) form the usual way.
        /// </summary>
        private void Visitors()
        {
            var here = Here.Where(m => m.Awake() && !m.Downed && !m.InMentalState).ToList();
            if (here.Count == 0) return;
            foreach (var c in IncidentWorker_Happening.Colonists(map))
            {
                if (c.Drafted || c.Downed || c.InMentalState || !c.Awake() || c.interactions == null || !c.Position.InHorDistOf(FireCell, 14f)) continue;
                var m = here.Where(x => x.Position.InHorDistOf(c.Position, 8f) && Together.Can(c, x)).OrderBy(x => x.Position.DistanceToSquared(c.Position)).FirstOrDefault();
                if (m == null || !Rand.Chance(0.6f)) continue;
                try
                {
                    if (c.interactions.TryInteractWith(m, Rand.Chance(0.2f) ? InteractionDefOf.DeepTalk : InteractionDefOf.Chitchat)) Warm(c, 1);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp chat", e);
                }
            }
        }

        private void NewDay(int day)
        {
            dayKey = day;
            dayHunts = dayChops = 0;
            bendBoundary = false;
            huntsToday = Mathf.FloorToInt(kind.huntsPerDay) + (Rand.Chance(kind.huntsPerDay % 1f) ? 1 : 0);
            chopsToday = Mathf.FloorToInt(kind.chopsPerDay) + (Rand.Chance(kind.chopsPerDay % 1f) ? 1 : 0);
            if (termBoundary)
            {
                huntsToday = Mathf.Min(huntsToday, 1);
                chopsToday = Mathf.Min(chopsToday, 2);
            }
            eventHour = Rand.RangeInclusive(9, 18);
            if (holding) HoldingDay();
            else if (arrivedTick >= 0 && Now - arrivedTick > GenDate.TicksPerDay / 2)
            {
                // People who have camped here before settle in quicker.
                float t = kind.tensionPerDay * (Familiar ? CampStoryMath.FamiliarTension : 1f);
                if (Now > untilTick) t += 5f;
                // Colonists who like them ease things a little.
                int friends = 0;
                foreach (var c in IncidentWorker_Happening.Colonists(map))
                    if (Here.Any(m => c.relations != null && c.relations.OpinionOf(m) >= 20)) friends++;
                t -= Mathf.Min(friends, 3);
                AddTension(t);
                if (tension >= 60f) Crowded();
            }
            if (termFee && nextFeeTick > 0 && Now >= nextFeeTick) PayFee();
        }

        /// <summary>Colonists who don't like sharing the land say so.</summary>
        private void Crowded()
        {
            foreach (var c in IncidentWorker_Happening.Colonists(map))
            {
                var traits = c.story?.traits;
                bool minds = traits != null && (traits.HasTrait(TraitDefOf.Greedy) || traits.HasTrait(TraitDefOf.Jealous) || traits.HasTrait(TraitDefOf.Abrasive));
                if (minds || Rand.Chance(0.15f)) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_Crowded);
            }
        }

        /// <summary>Their goods stay theirs (and out of the colony's hands); the fire is kept fed; a hut gets its roof.</summary>
        private void Upkeep()
        {
            var goods = Goods();
            foreach (var t in goods) if (!t.IsForbidden(Faction.OfPlayer)) t.SetForbidden(true, false);
            // Taken by the colony: their goods turning up in the colony's stockpiles.
            var seen = new HashSet<int>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).ToList())
            {
                if (!Owns(t)) continue;
                seen.Add(t.thingIDNumber);
                if (!t.Spawned || !StoreUtility.IsInAnyStorage(t) || t.Position.InHorDistOf(center, CampSite.CoreRadius + 6)) continue;
                Disown(t);
                timesTaken++;
                AddTension(8f);
                Note(Text("CC_Camp_Taken", null, null, t.stackCount, t.LabelNoCount), MessageTypeDefOf.NegativeEvent, Importance.Notable, t);
            }
            // Gone for good (used up, eaten, built into something): no longer tracked. What someone carries still counts.
            foreach (var p in Here)
                if (p.carryTracker?.CarriedThing != null) seen.Add(p.carryTracker.CarriedThing.thingIDNumber);
            foreach (var id in owned.Where(id => !seen.Contains(id)).ToList())
            {
                owned.Remove(id);
                OwnedSet.Remove(id);
            }
            // Fires and torches burn down; they keep them going.
            foreach (var b in Buildings())
            {
                // Theirs as a group: the colony damaging the camp counts as an attack on them.
                if (b.GetLord() == null && LordAlive) lord.AddBuilding(b);
                var fuel = b.TryGetComp<CompRefuelable>();
                if (fuel == null || fuel.FuelPercentOfMax > 0.5f) continue;
                var wood = goods.FirstOrDefault(t => t.def == ThingDefOf.WoodLog);
                if (wood != null)
                {
                    int take = Mathf.Min(wood.stackCount, 5);
                    wood.SplitOff(take).Destroy();
                }
                fuel.Refuel(fuel.Props.fuelCapacity * 0.6f);
            }
            // A hut gets a roof once its walls stand.
            var inside = CampSite.HutInside(plans).ToList();
            if (inside.Count > 0 && plans.Where(p => p.role == "hut" || p.role == "door").All(p => p.dropped || p.Built(map, faction) != null)
                && plans.Count(p => (p.role == "hut" || p.role == "door") && !p.dropped) >= 8)
                foreach (var c in inside)
                    if (c.InBounds(map) && !c.Roofed(map)) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
        }

        private void WatchMembers()
        {
            bool hostile = faction == null || faction.HostileTo(Faction.OfPlayer);
            foreach (var p in members)
            {
                if (p == null) continue;
                if (p.Faction == Faction.OfPlayer && !joined.Contains(p))
                {
                    joined.Add(p);
                    willing.Remove(p);
                }
                if (p.IsPrisonerOfColony && !arrested.Contains(p))
                {
                    arrested.Add(p);
                    // One of them taken: the rest won't wait to be next.
                    if (!hostile && LordAlive && (phase == CampPhase.Building || phase == CampPhase.Living || phase == CampPhase.Coming))
                    {
                        AddTension(40f);
                        lord.ReceiveMemo(LordJob_Camp.MemoFlee);
                        // Gone now, not still camped for the rest of the hour (no talk of tension with a camp in flight).
                        Scattered();
                    }
                }
            }
            willing.RemoveAll(p => p == null || p.Dead || !Here.Contains(p));
            if (trader != null && (trader.Dead || !Here.Contains(trader))) trader = null;
            if (sick != null && (sick.Dead || !Here.Contains(sick))) sick = null;
            if (holding && Settled) HoldingLosses();
        }

        /// <summary>Silver changing hands at their trader: the colony traded with them.</summary>
        private void WatchTrade()
        {
            if (trader == null || trader.inventory == null) return;
            int silver = trader.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
            if (traderSilver >= 0 && silver != traderSilver && lastTradeDay != dayKey)
            {
                lastTradeDay = dayKey;
                tradeDays++;
                AddTension(-6f);
                // Whoever did the trading is the colonist standing closest to their trader.
                Warm(map.mapPawns.FreeColonistsSpawned.Where(c => c.Position.InHorDistOf(trader.Position, 10f)).OrderBy(c => c.Position.DistanceToSquared(trader.Position)).FirstOrDefault(), 2);
                Note(Text("CC_Camp_Traded", trader), MessageTypeDefOf.PositiveEvent, Importance.Flavor, trader);
            }
            traderSilver = silver;
        }

        // ---------------- Arriving and building ----------------

        private void Arrived()
        {
            phase = CampPhase.Building;
            arrivedTick = Now;
            untilTick = Now + stayTicks;
            if (termLimit) untilTick = Mathf.Min(untilTick, Now + GenDate.TicksPerDay * 3);
            buildDeadline = Now + Mathf.RoundToInt(GenDate.TicksPerDay * 1.6f);
            dayKey = -1;
            Unpack();
            foreach (var p in plans.Where(p => !p.placed && !p.dropped)) PlaceBlueprint(p);
            Note(Text("CC_Camp_Arrived"), MessageTypeDefOf.NeutralEvent, Importance.Notable, now: true);
        }

        /// <summary>The materials they carried go down in their stash.</summary>
        private void Unpack()
        {
            var needs = new HashSet<ThingDef>(plans.SelectMany(p => Costs(p)).Select(c => c.thingDef));
            foreach (var p in Here)
            {
                var inv = p.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv.ToList())
                {
                    if (!needs.Contains(t.def) || (p == trader && !p.inventory.NotForSale(t))) continue;
                    if (inv.TryDrop(t, stash, map, ThingPlaceMode.Near, out var dropped, null, AtStash) && dropped != null) Own(dropped);
                }
            }
        }

        /// <summary>
        /// A spot at their stash for their goods: close to it, with nothing on it that isn't theirs (what's put down there
        /// becomes theirs, so it mustn't land on, or stack onto, anyone else's things).
        /// </summary>
        private bool AtStash(IntVec3 c) => c.InHorDistOf(stash, 3f) && !c.GetThingList(map).Any(t => t.def.category == ThingCategory.Item && !Owns(t));

        public static List<ThingDefCountClass> Costs(CampPlan p)
        {
            try
            {
                return p.def == null ? new List<ThingDefCountClass>() : CostListCalculator.CostListAdjusted(p.def, p.stuff, false) ?? new List<ThingDefCountClass>();
            }
            catch
            {
                return new List<ThingDefCountClass>();
            }
        }

        private void PlaceBlueprint(CampPlan p)
        {
            try
            {
                // Still open ground when they get there: nothing built, zoned, planned or lying on it that isn't theirs.
                bool ours = p.Rect.All(c => c.InBounds(map) && (c.GetFirstBuilding(map) == null) && !(map.areaManager.Home?[c] ?? false) && map.zoneManager.ZoneAt(c) == null
                                            && !CampSite.Marked(c, map) && !c.GetThingList(map).Any(t => t.def.category == ThingCategory.Item && !Owns(t)));
                if (!ours || !GenConstruct.CanPlaceBlueprintAt(p.def, p.cell, p.rot, map, false, null, null, p.stuff).Accepted)
                {
                    p.dropped = true;
                    return;
                }
                GenConstruct.PlaceBlueprintForBuild(p.def, p.cell, map, p.rot, faction, p.stuff);
                p.placed = true;
            }
            catch (Exception e)
            {
                // Another mod's placement rules can fail off the map being viewed: that building is simply left out.
                CouncilLog.WarnOnce("camp blueprint for " + p.def?.defName + " left out: " + e.Message);
                p.dropped = true;
            }
        }

        /// <summary>All built (or given up on): the camp is up. Past the deadline, what isn't up is given up.</summary>
        private void CheckBuilt()
        {
            var left = Unfinished();
            if (left.Count > 0 && Now < buildDeadline) return;
            foreach (var t in left)
            {
                if (t is Frame f && f.resourceContainer != null) f.resourceContainer.TryDropAll(stash, map, ThingPlaceMode.Near, (thing, n) => Own(thing), AtStash);
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            }
            foreach (var p in plans.Where(p => p.placed && p.Built(map, faction) == null)) p.dropped = true;
            phase = CampPhase.Living;
            builtTick = Now;
            // What they put out once the camp stands: bales by the beasts, a woodpile by the door (CampLook.cs).
            PutOut();
            Note(Pick(kind.settled) ?? Text("CC_Camp_Built"), MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- The day's event ----------------

        private void DailyEvent()
        {
            // Bent terms come first: the colony should hear about those.
            if (AnyTerms && Rand.Chance(kind.breakTerms * (0.5f + tension / 100f)) && BendTerms()) return;
            // Something they'd ask of the colony (a request by letter, CampRequests.cs), when their kind has one.
            if (tension < 55f && Rand.Chance(RequestChance) && TryRequest()) return;
            if (tension < 45f && Rand.Chance(kind.helpChance) && Help()) return;
            if (tension < 40f && DaysHere >= 2f && Now - lastJoinAskTick > GenDate.TicksPerDay * 3 && Rand.Chance(kind.joinChance)) AskToJoin();
        }

        private bool BendTerms()
        {
            var options = new List<string>();
            if (termBoundary) options.Add("boundary");
            if (termShare && kills.Count + animalsHunted > 0) options.Add("share");
            if (termFee && !feeLate) options.Add("fee");
            if (options.Count == 0) return false;
            string which = options.RandomElement();
            switch (which)
            {
                case "boundary": bendBoundary = true; break;
                case "share": skipShare = true; break;
                case "fee": feeLate = true; break;
            }
            Ask("broken", Text("CC_Camp_BrokenTitle"), Text("CC_Camp_Broken_" + which));
            return true;
        }

        private void AskToJoin()
        {
            var here = Here;
            var who = willing.Where(p => here.Contains(p) && !p.DevelopmentalStage.Juvenile() && p != trader).RandomElementWithFallback();
            // Someone camping alone (a hermit) may come to ask for themselves.
            if (who == null && here.Count == 1 && here[0] != trader && !here[0].DevelopmentalStage.Juvenile()) who = here[0];
            if (who == null) return;
            lastJoinAskTick = Now;
            string text = Pick(kind.joinTexts, who) ?? Text("CC_Camp_JoinText", who);
            Ask("join", Text("CC_Camp_JoinTitle", who), text, who);
        }

        /// <summary>A good turn for the colony, of a kind this camp does. False if none came about today.</summary>
        private bool Help()
        {
            foreach (var h in kind.help.InRandomOrder())
            {
                bool done;
                try
                {
                    done = DoHelp(h);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp help " + h, e);
                    done = false;
                }
                if (!done) continue;
                helps++;
                AddTension(-3f);
                return true;
            }
            return false;
        }

        private bool DoHelp(string h)
        {
            var colonists = IncidentWorker_Happening.Colonists(map);
            switch (h)
            {
                case "meat":
                {
                    var meat = Goods().Where(t => t.def.IsMeat).ToList();
                    int total = meat.Sum(t => t.stackCount);
                    if (total < 20) return false;
                    int give = Mathf.Max(10, total * 2 / 5);
                    int given = GiveAway(meat, give);
                    if (given <= 0) return false;
                    Tell(Text("CC_Camp_HelpTitle"), Text("CC_Camp_Help_meat", null, null, given), LetterDefOf.PositiveEvent, false);
                    foreach (var c in colonists) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_Helped);
                    return true;
                }
                case "wood":
                {
                    var wood = Goods().Where(t => t.def == ThingDefOf.WoodLog).ToList();
                    if (wood.Sum(t => t.stackCount) < 40) return false;
                    int given = GiveAway(wood, Rand.RangeInclusive(25, 40));
                    if (given <= 0) return false;
                    Tell(Text("CC_Camp_HelpTitle"), Text("CC_Camp_Help_wood", null, null, given), LetterDefOf.PositiveEvent, false);
                    return true;
                }
                case "medicine":
                {
                    int n = Rand.RangeInclusive(2, 4);
                    var med = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
                    med.stackCount = n;
                    if (!GenPlace.TryPlaceThing(med, share, map, ThingPlaceMode.Near)) return false;
                    Tell(Text("CC_Camp_HelpTitle"), Text("CC_Camp_Help_medicine", null, null, n), LetterDefOf.PositiveEvent, false);
                    foreach (var c in colonists) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_Helped);
                    return true;
                }
                case "mend":
                {
                    var mender = Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0).FirstOrDefault();
                    if (mender == null) return false;
                    foreach (var c in colonists.InRandomOrder())
                    {
                        var gear = new List<Thing>();
                        if (c.apparel != null) gear.AddRange(c.apparel.WornApparel.Cast<Thing>());
                        if (c.equipment?.Primary != null) gear.Add(c.equipment.Primary);
                        var worn = gear.Where(t => t.def.useHitPoints && t.HitPoints < t.MaxHitPoints * 0.7f).OrderBy(t => t.HitPoints / (float)t.MaxHitPoints).FirstOrDefault();
                        if (worn == null) continue;
                        worn.HitPoints = worn.MaxHitPoints;
                        Warm(c, 2);
                        Tell(Text("CC_Camp_HelpTitle"), Text("CC_Camp_Help_mend", mender, c, 0, worn.LabelNoCount), LetterDefOf.PositiveEvent, false);
                        CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_Helped);
                        return true;
                    }
                    return false;
                }
                default:
                    // The newer kinds' good turns: healing, a show, a blessing, a survey (CampKinds.cs).
                    return MoreHelp(h);
            }
        }

        /// <summary>Leaves some of their goods at the spot nearest the colony, for the colony to take. Returns how many.</summary>
        private int GiveAway(List<Thing> from, int count)
        {
            int given = 0;
            foreach (var t in from)
            {
                if (given >= count) break;
                int n = Mathf.Min(count - given, t.stackCount);
                var part = t.SplitOff(n);
                if (part.Spawned) part.DeSpawn();
                Disown(part);
                if (GenPlace.TryPlaceThing(part, share, map, ThingPlaceMode.Near, out var placed))
                {
                    placed?.SetForbidden(false, false);
                    given += n;
                }
            }
            return given;
        }

        private void PayFee()
        {
            nextFeeTick = Now + GenDate.TicksPerDay * 5;
            if (feeLate)
            {
                feeLate = false;
                return;
            }
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = fee;
            // From their trader's purse when they have one; otherwise from their own.
            if (trader != null && trader.inventory != null)
            {
                int have = trader.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
                if (have >= fee)
                {
                    trader.inventory.RemoveCount(ThingDefOf.Silver, fee);
                    traderSilver = -1;
                }
            }
            if (GenPlace.TryPlaceThing(silver, share, map, ThingPlaceMode.Near, out var placed))
            {
                placed?.SetForbidden(false, false);
                Note(Text("CC_Camp_FeePaid", null, null, fee), MessageTypeDefOf.PositiveEvent, Importance.Notable, placed);
            }
        }

        // ---------------- The camp's work (called by their jobs) ----------------

        /// <summary>The cook is done at the fire: meals for everyone, from their meat first, then from what they brought.</summary>
        public void Cooked(Pawn cook)
        {
            var here = Here;
            int wanted = 0;
            foreach (var p in here) wanted += Mathf.Max(0, 2 - MealsOf(p));
            if (wanted <= 0) return;
            int made = 0;
            // Raw food in their stash: half a meal's worth of nutrition each, as in the colony's own kitchens.
            foreach (var t in Goods().Where(IsRawFood).ToList())
            {
                if (made >= wanted) break;
                float per = t.GetStatValue(StatDefOf.Nutrition);
                if (per <= 0f) continue;
                int perMeal = Mathf.Max(1, Mathf.CeilToInt(0.5f / per));
                while (made < wanted && t.stackCount >= perMeal && !t.Destroyed)
                {
                    t.SplitOff(perMeal).Destroy();
                    made++;
                }
            }
            int fromStores = Mathf.Min(wanted - made, provisions);
            provisions -= fromStores;
            made += fromStores;
            foreach (var p in here.OrderBy(MealsOf))
            {
                while (made > 0 && MealsOf(p) < 2)
                {
                    var meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                    p.inventory.TryAddItemNotForSale(meal);
                    made--;
                }
            }
        }

        public static bool IsRawFood(Thing t)
        {
            var ing = t.def.ingestible;
            return ing != null && !t.def.IsCorpse && t.def.IsNutritionGivingIngestible && ing.HumanEdible
                   && (ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty);
        }

        /// <summary>A meal they carry for themselves (not their trader's stock, not a drink or a treat).</summary>
        public static bool IsMeal(Pawn p, Thing t)
        {
            var ing = t.def.ingestible;
            return ing != null && t.def.IsNutritionGivingIngestible && !t.def.IsDrug && ing.HumanEdible && ing.preferability >= FoodPreferability.MealAwful
                   && (p.trader?.traderKind == null || p.inventory.NotForSale(t));
        }

        public static int MealsOf(Pawn p) => p?.inventory?.innerContainer?.Where(t => IsMeal(p, t)).Sum(t => t.stackCount) ?? 0;

        /// <summary>A body they brought in, butchered: the meat and hide go in their stash (a share to the colony, if agreed).</summary>
        public void Butchered(Pawn butcher, Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed) return;
            var products = corpse.ButcherProducts(butcher, 0.85f).ToList();
            corpse.Destroy();
            int shared = 0;
            foreach (var t in products)
            {
                bool share_ = termShare && !skipShare && t.def.IsMeat;
                if (share_)
                {
                    int give = t.stackCount / 3;
                    if (give > 0)
                    {
                        var part = t.SplitOff(give);
                        if (GenPlace.TryPlaceThing(part, share, map, ThingPlaceMode.Near, out var placed))
                        {
                            placed?.SetForbidden(false, false);
                            shared += give;
                        }
                    }
                }
                if (GenPlace.TryPlaceThing(t, stash, map, ThingPlaceMode.Near, out var put, null, AtStash)) Own(put);
            }
            skipShare = false;
            SayMoment(butcher, "butcher", 0.5f);
            if (shared > 0) Note(Text("CC_Camp_Shared", null, null, shared), MessageTypeDefOf.PositiveEvent, Importance.Notable);
        }

        /// <summary>Their healer tended a colonist lying hurt nearby.</summary>
        public void Tended(Pawn healer, Pawn patient)
        {
// together: the camp healer's tend job, at the patient's side (CampWork).
            helps++;
            AddTension(-4f);
            Warm(patient, 3);
            Note(Text("CC_Camp_Tended", healer, patient), MessageTypeDefOf.PositiveEvent, Importance.Notable, patient);
            CouncilGameComponent.GiveThought(patient, CampDefOf.Council_Camp_Helped, healer);
        }

        /// <summary>A prospector's day at the diggings: now and then something worth having.</summary>
        public void Prospected(Pawn p)
        {
            AddTension(0.5f);
            if (!Rand.Chance(0.3f)) return;
            ThingDef what;
            int n;
            float roll = Rand.Value;
            var jade = DefDatabase<ThingDef>.GetNamedSilentFail("Jade");
            var gold = DefDatabase<ThingDef>.GetNamedSilentFail("Gold");
            if (roll < 0.15f && gold != null)
            {
                what = gold;
                n = Rand.RangeInclusive(2, 6);
            }
            else if (roll < 0.35f && jade != null)
            {
                what = jade;
                n = Rand.RangeInclusive(3, 8);
            }
            else
            {
                what = ThingDefOf.Silver;
                n = Rand.RangeInclusive(15, 45);
            }
            var found = ThingMaker.MakeThing(what);
            found.stackCount = n;
            // Their trader has it to sell; otherwise it goes in their stash.
            if (trader != null && trader.inventory != null && Here.Contains(trader) && trader.inventory.innerContainer.TryAdd(found))
            {
                traderSilver = trader.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
            }
            else if (GenPlace.TryPlaceThing(found, stash, map, ThingPlaceMode.Near, out var put, null, AtStash)) Own(put);
            Note(Text("CC_Camp_Found", p, null, n, what.label), MessageTypeDefOf.NeutralEvent, Importance.Flavor, p);
        }

        // ---------------- Endings ----------------

        /// <summary>Whether something sends them on their way now (raid, weather, hunger, crowding, time).</summary>
        private bool Ends()
        {
            if (MapFacts.ThreatOn(map))
            {
                // Neighbours settled for good keep to their huts and hope; what they lose is counted after (Holding.cs).
                if (holding) return false;
                // A war party takes up arms again; anyone else wants no part in the colony's fight.
                if (kind.fights)
                {
                    if (!fighting && LordAlive)
                    {
                        fighting = true;
                        lord.ReceiveMemo(LordJob_Camp.MemoFight);
                        Note(Text("CC_WarCamp_Again"), MessageTypeDefOf.NeutralEvent, Importance.Notable, now: true);
                    }
                    return false;
                }
                Leave("raid", true);
                return true;
            }
            if (fighting && kind.fights && !MapFacts.ThreatOn(map)) fighting = false;
            if (BadWeather() && (!holding || CampWeather.Hazard(map) != null))
            {
                // Settled neighbours have shelter against the weather; only poisoned air or a sky on fire drives them off.
                Leave("weather");
                return true;
            }
            if ((map.areaManager.Home?[center] ?? false) || map.listerBuildings.allBuildingsColonist.Any(b => b.Position.InHorDistOf(center, 5f)))
            {
                Leave("crowded");
                return true;
            }
            var here = Here;
            if (here.Count == 0)
            {
                Finish();
                return true;
            }
            // No way back to their camp for any of them (walled off, flooded): they give up on it.
            if (here.All(p => !p.CanReach(center, PathEndMode.Touch, Danger.Deadly))) stuckHours++;
            else stuckHours = 0;
            if (stuckHours >= 3)
            {
                Leave("lost");
                return true;
            }
            if (provisions <= 0 && here.All(p => MealsOf(p) == 0) && !Goods().Any(IsRawFood) && kills.Count == 0
                && here.Any(p => p.needs?.food != null && p.needs.food.CurLevelPercentage < 0.2f))
            {
                Leave("hunger");
                return true;
            }
            if (Now >= untilTick && phase == CampPhase.Living)
            {
                if (!extendAsked && !extended && !asked && !termLimit && tension < 50f && Rand.Chance(kind.extendChance))
                {
                    extendAsked = true;
                    Ask("overstay", Text("CC_Camp_OverstayTitle"), Text("CC_Camp_Overstay"), null, 0.5f);
                    return false;
                }
                // Waiting on the colony's answer to that.
                if (extendAsked && !extended && Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Camp c && c.campId == id && c.what == "overstay" && !c.resolved)) return false;
                // A last walk first, if their kind takes one: the healer's round, a gift carried to the colony's gate.
                if (FarewellWalking()) return false;
                Leave("time");
                return true;
            }
            return false;
        }

        private bool BadWeather()
        {
            float temp = map.mapTemperature.OutdoorTemp;
            if (CampWeatherMath.TooExtremeToStay(temp) || CampWeather.Hazard(map) != null) return true;
            // Weather that would start to hurt most of them, dressed as they are (CampWeatherMath).
            var here = Here;
            int exposed = 0;
            foreach (var p in here)
            {
                var comfy = GenTemperature.ComfortableTemperatureRange(p);
                if (CampWeatherMath.Exposed(temp, comfy.min, comfy.max)) exposed++;
            }
            return CampWeatherMath.PackUp(exposed, here.Count);
        }

        /// <summary>They go: packed up and walking out (or hurrying, with a raid about).</summary>
        public void Leave(string reason, bool fast = false)
        {
            if (phase == CampPhase.Leaving || phase == CampPhase.Over || phase == CampPhase.Fighting) return;
            bool wasSettled = Settled;
            phase = CampPhase.Leaving;
            ending = reason;
            leftTick = Now;
            if (LordAlive) lord.ReceiveMemo(fast ? LordJob_Camp.MemoFlee : LordJob_Camp.MemoLeave);
            // People who might come back are held in the world from here (CampMemory.cs).
            try
            {
                CampReturns.Keep(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp kept", e);
            }
            try
            {
                if (wasSettled) PackUp(true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp packing", e);
            }
            TellEnding();
        }

        /// <summary>
        /// Packing: half-built things are taken down, what can be carried goes with them (bedrolls, their goods), and
        /// the rest is put out and cleared away, or left for the colony when they mean it as a gift (a hermit's hut).
        /// </summary>
        private void PackUp(bool peaceful)
        {
            var carriers = Here.Where(p => p.inventory != null).ToList();
            foreach (var t in Unfinished())
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            if (!peaceful) return;
            // Where the fire and their day's work stood, for what they leave there at the end of their stay.
            var fire = FireCell;
            var taskSpot = kind.taskAt != null ? plans.FirstOrDefault(p => p.role == kind.taskAt && !p.dropped)?.cell ?? IntVec3.Invalid : IntVec3.Invalid;
            // A hut taken down loses its roof first: left without walls, the roof would fall in on whoever is below.
            if (!kind.leaveBuildings)
                foreach (var c in CampSite.HutInside(plans))
                    if (c.InBounds(map) && map.roofGrid.RoofAt(c) == RoofDefOf.RoofConstructed) map.roofGrid.SetRoof(c, null);
            foreach (var b in Buildings())
            {
                if (b.Destroyed) continue;
                try
                {
                    if (b.def.Minifiable && carriers.Count > 0)
                    {
                        // Off the map before it's packed, whether or not the game's own packing takes it off itself.
                        if (b.Spawned) b.DeSpawn();
                        var mini = MinifyUtility.MakeMinified(b);
                        if (mini == null)
                        {
                            if (!b.Destroyed) b.Destroy();
                        }
                        else if (!carriers.RandomElement().inventory.innerContainer.TryAdd(mini) && !mini.Destroyed) mini.Destroy();
                        continue;
                    }
                    if (kind.leaveBuildings)
                    {
                        // Nobody's now: harm to it is no longer an attack on them.
                        if (LordAlive) lord.ownedBuildings.Remove(b);
                        b.SetFaction(null);
                    }
                    else b.Destroy(DestroyMode.Vanish);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp packing " + b.def.defName, e);
                }
            }
            // What they can't take is left for the colony (the hermit's woodpile, with the hut); the rest goes with them.
            LeaveGoods();
            foreach (var t in Goods())
            {
                Disown(t);
                if (carriers.Count == 0) break;
                if (t.Spawned) t.DeSpawn();
                if (!carriers.RandomElement().inventory.innerContainer.TryAdd(t) && !t.Destroyed) t.Destroy();
            }
            // At the end of their stay, what they leave at the camp: a ring of seeds round the old fire, a backdrop on the stage.
            if (ending == "time") LeaveGifts(fire, taskSpot);
        }

        /// <summary>Half a day after they set off (or broke out of a fight), whoever is still here is stuck: walled in, or lost.</summary>
        private bool LeavingTooLong()
        {
            int limit = GenDate.TicksPerDay / 2;
            if (phase == CampPhase.Leaving) return leftTick >= 0 && Now - leftTick > limit;
            if (phase == CampPhase.Fighting) return lord.CurLordToil is LordToil_ExitMapAndDefendSelf && lord.ticksInToil > limit;
            return false;
        }

        /// <summary>
        /// Too long on the way out: the game's own leaving takes over for those who can still find a way off the map (the
        /// hurt go with them once they can walk), and anyone with no way out at all slips away. The books close.
        /// </summary>
        private void SendOff()
        {
            var walkers = new List<Pawn>();
            // Their animals go with them (and any beast bought from the colony).
            foreach (var p in Here.Concat(lord.ownedPawns.Where(x => x?.RaceProps != null && !x.RaceProps.Humanlike)).Distinct().ToList())
            {
                try
                {
                    lord.RemovePawn(p);
                    if (p.Downed || p.CanReachMapEdge()) walkers.Add(p);
                    else p.ExitMap(false, Rot4.Invalid);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp send-off", e);
                }
            }
            if (walkers.Count > 0) LordMaker.MakeNewLord(faction ?? walkers[0].Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), map, walkers);
            Finish();
        }

        /// <summary>The colony attacked them while at peace, or arrested one of them: they scattered.</summary>
        private void Scattered()
        {
            phase = CampPhase.Leaving;
            bool arrest = arrested.Count > 0 || map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == JobDefOf.Arrest && c.CurJob.targetA.Thing is Pawn t && members.Contains(t));
            ending ??= arrest ? "arrest" : "harmed";
            leftTick = Now;
            SendHerdOff();
            AbandonGoods();
            foreach (var t in Unfinished())
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            TellEnding();
        }

        /// <summary>At war with the colony now (the game's own goodwill): they hold their camp.</summary>
        private void Fighting()
        {
            phase = CampPhase.Fighting;
            ending = "fight";
            // Their animals are walked off first: they never fight anyone.
            SendHerdOff();
            foreach (var t in Unfinished())
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            Tell(Text("CC_Camp_FightTitle"), Text("CC_Camp_Fight"), LetterDefOf.ThreatSmall, true, leader);
        }

        /// <summary>Their goods are left behind for anyone to take.</summary>
        private void AbandonGoods()
        {
            foreach (var t in Goods())
            {
                t.SetForbidden(false, false);
                Disown(t);
            }
        }

        /// <summary>The group is gone from the map: the books are closed.</summary>
        public void Finish()
        {
            if (phase == CampPhase.Over) return;
            if (map == null || !Find.Maps.Contains(map))
            {
                // The map itself is gone (abandoned): nothing to tell.
                phase = CampPhase.Over;
                CampTracker.Instance?.Ended(this);
                return;
            }
            if (phase == CampPhase.Fighting) AbandonGoods();
            bool allJoined = false;
            if (ending == null)
            {
                ending = "empty";
                // Everyone left joined the colony: the camp and its goods are the colony's now.
                if (joined.Count > 0 && map != null && Find.Maps.Contains(map))
                {
                    allJoined = true;
                    foreach (var b in Buildings())
                        if (!b.Destroyed) b.SetFaction(Faction.OfPlayer);
                    AbandonGoods();
                }
            }
            // Animals of theirs still here go too, or are the colony's with the rest of the camp.
            try
            {
                ReleaseHerd(allJoined);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp herd at the end", e);
            }
            try
            {
                ClearUp();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp clearing up", e);
            }
            phase = CampPhase.Over;
            if (!told || ending == "fight") TellEnding(true);
            // Their people remember how the camp went: helped, hosted, driven off (World/FactionMemory.cs).
            try
            {
                if (MemoryHooks.Listening) MemoryHooks.CampEnded(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp memory", e);
            }
            // A camp that went well enough is remembered, and may come back (CampMemory.cs).
            try
            {
                CampReturns.Remember(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp remembered", e);
            }
            // What the colony's people lived through with them: memories, deeds, setbacks (Social/Talk/SharedTimes.cs).
            SharedTimes.CampEnded(this);
            // A camp that came, stayed its time and left on good terms counts toward a name for hospitality (Character/).
            if (ColonyTraitMath.HostedWell(ending, arrivedTick >= 0)) ColonyTraits.Tally("campHosted", helps + gifts > 0 ? 1.5f : 1f);
            CampTracker.Instance?.Ended(this);
        }

        /// <summary>
        /// Nobody of theirs is left: nothing of the camp stays forbidden or half-built on the colony's map. Their goods
        /// still lying about are anyone's, and what still stands of the camp (left when they fled or fought) belongs to
        /// nobody, so the colony can use it or take it down.
        /// </summary>
        private void ClearUp()
        {
            foreach (var t in Unfinished())
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).ToList())
            {
                if (!Owns(t)) continue;
                if (t.Spawned) t.SetForbidden(false, false);
                Disown(t);
            }
            if (LordAlive) lord.RemoveAllBuildings();
            if (faction == null || faction == Faction.OfPlayer) return;
            foreach (var b in Buildings())
                if (!b.Destroyed) b.SetFaction(null);
        }

        /// <summary>The ending's letter, its chronicle line and how the colonists took it.</summary>
        private void TellEnding(bool final = false)
        {
            if (told && !(final && ending == "fight")) return;
            told = true;
            if (arrivedTick < 0 && ending != "fight" && ending != "harmed" && ending != "arrest")
            {
                // Turned back before they ever made camp: the answer to the first letter already said so.
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, Text("CC_Camp_TurnedBack"), leader));
                return;
            }
            string key = "CC_Camp_End_" + (ending ?? "empty");
            if (!key.CanTranslate()) key = "CC_Camp_End_empty";
            // Their own farewell, told as it went (what they left, the healer's round, the smith's mending).
            string text = ending == "time" ? FarewellText() ?? Text(key) : Text(key);
            if (final && ending == "fight") text = Text("CC_Camp_End_fightOver");
            if (joined.Count > 0 && ending != "fight") text += "\n\n" + "CC_Camp_Stayed".Translate(TextUtil.JoinAnd(joined.Select(p => p.LabelShort).ToList())).Resolve();
            bool bad = ending == "harmed" || ending == "arrest" || ending == "fight" || ending == "fallout" || ending == "forced";
            Tell(Text("CC_Camp_EndTitle"), text, bad ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent, true, leader);
            try
            {
                Reactions();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp reactions", e);
            }
        }

        /// <summary>
        /// The colony's people react: someone who made a friend at the camp writes about them, someone who resented
        /// them says so, and the colony feels it went well or badly.
        /// </summary>
        private void Reactions()
        {
            if (map == null) return;
            var comp = CouncilGameComponent.Instance;
            var colonists = IncidentWorker_Happening.Colonists(map);
            // apart: the camp has gone; the friendship was made face to face at their fire (Visited, Visitors need them together).
            bool peaceful = ending == "time" || ending == "asked" || ending == "weather" || ending == "raid" || ending == "hunger" || ending == "crowded" || ending == "moved";
            string outcome = peaceful ? "left" : ending == "forced" ? "forced" : ending == "fight" ? "fight" : ending == "fallout" ? "fallout" : "fled";
            var them = members.Where(m => m != null && !m.Dead && !joined.Contains(m)).ToList();
            Pawn friend = null, friendOf = null, foe = null, foeOf = null;
            float best = 14f;
            int worst = -9;
            foreach (var c in colonists)
            {
                if (c.relations == null) continue;
                int warm = warmth.TryGetValue(c, out int w) ? w : 0;
                foreach (var m in them)
                {
                    int op = c.relations.OpinionOf(m);
                    // Time spent at their fire counts as much as the opinion the game keeps.
                    float score = op + Mathf.Min(warm, 8) * 3f;
                    if (score > best && op > -5)
                    {
                        best = score;
                        friend = c;
                        friendOf = m;
                    }
                    if (op < worst)
                    {
                        worst = op;
                        foe = c;
                        foeOf = m;
                    }
                }
            }
            if (foe == null && (tension >= 50f || treesFelled + animalsHunted * 3 >= 12))
            {
                foe = colonists.Where(c => c != friend && c.story?.traits != null && (c.story.traits.HasTrait(TraitDefOf.Greedy) || c.story.traits.HasTrait(TraitDefOf.Abrasive) || c.story.traits.HasTrait(TraitDefOf.Jealous)))
                    .RandomElementWithFallback() ?? (tension >= 50f ? colonists.Where(c => c != friend && !c.DevelopmentalStage.Juvenile()).RandomElementWithFallback() : null);
                foeOf = leader ?? them.FirstOrDefault();
            }
            if (friend != null)
            {
                Journal(friend, friendOf, "friend", outcome);
                if (peaceful) CouncilGameComponent.GiveThought(friend, CampDefOf.Council_Camp_FriendLeft, friendOf);
                if (peaceful) Interplay.Notify_CampFriend(friend, friendOf);
            }
            if (foe != null && foe != friend) Journal(foe, foeOf, "resent", outcome);
            if (peaceful && tension < 35f && (tradeDays > 0 || helps > 0 || gifts > 0))
                foreach (var c in colonists) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_GoodNeighbors);
            if (ending == "forced" || ending == "harmed")
                foreach (var c in colonists.Where(c => c.story?.traits?.HasTrait(TraitDefOf.Kind) == true))
                    CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_DroveThemOff);
            // The colony's character: hosting strangers on its land, or not.
            if (comp != null)
            {
                if (peaceful && ending != "asked" && DaysHere >= 2f) comp.character.Shift(new WayShift { openness = 2f }, Label);
                if (ending == "forced") comp.character.Shift(new WayShift { mercy = -3f, openness = -2f }, Label);
            }
        }

        private void Journal(Pawn colonist, Pawn them, string how, string outcome)
        {
            var args = new List<NamedArgument>
            {
                them != null ? them.Named("X") : "?".Named("X"),
                (faction?.Name ?? "").Named("FACTION"),
                Label.Named("NAME"),
                (place ?? "").Named("PLACE")
            };
            var tags = new List<string> { how, outcome };
            if (tradeDays > 0) tags.Add("traded");
            if (helps > 0) tags.Add("helped");
            if (joined.Count > 0) tags.Add("hired");
            CouncilGameComponent.Instance?.journals.WriteTagged(colonist, "Council_Journal_Camp", tags.ToArray(), null, args.ToArray());
        }
    }
}
