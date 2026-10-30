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
    /// Something of a camp's on its way to the colony by hand (see <see cref="Camp.carries"/>): herbs their healer goes
    /// out to gather at dawn, the milk or wool of a beast of their herd, a gift from their trader's stock, sacks the colony
    /// bought. Whoever has it carries it over and sets it down at <see cref="to"/>; the colony hears of it then.
    /// </summary>
    public class CampCarry : IExposable
    {
        public Pawn by;
        /// <summary>The good turn it is ("medicine", "herbs", "milk", "goods"), told when it's set down; "bought" for goods the colony paid for.</summary>
        public string help;
        public ThingDef def;
        /// <summary>How many they have so far (gathered, milked), or how many they are to bring.</summary>
        public int count;
        /// <summary>How many herbs the healer means to gather before setting off.</summary>
        public int want;
        /// <summary>Stops made on the gathering walk (or the one beast milked).</summary>
        public int stops;
        public IntVec3 to = IntVec3.Invalid;
        /// <summary>Not before this tick (dawn, for the herbs); given up after <see cref="until"/>.</summary>
        public int from, until;
        /// <summary>Gathered or milked: on the way over now.</summary>
        public bool carrying;
        /// <summary>In their hands or pack already (gathered, milked, out of the trader's stock).</summary>
        public bool taken;

        public void ExposeData()
        {
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref help, "help");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref want, "want");
            Scribe_Values.Look(ref stops, "stops");
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref carrying, "carrying");
            Scribe_Values.Look(ref taken, "taken");
        }
    }

    // Good turns that come from somewhere real and are carried over by hand (CampCarry): the healer's herbs gathered at
    // dawn (wild healroot harvested where it grows near the camp, else picked along a walk over the wild ground), the milk
    // or wool of a beast of their own herd, something from their trader's stock, sacks the colony bought. The one who has
    // it walks it over and sets it down (at the camp's edge nearest the colony, or the colony's gate), and the colony
    // hears of it then.
    public partial class Camp
    {
        public List<CampCarry> carries = new List<CampCarry>();

        /// <summary>When the healer sets out gathering.</summary>
        private const int DawnHour = 6;
        /// <summary>How far from their camp the healer goes gathering, and how many stops the walk makes at most.</summary>
        private const float GatherRadius = 36f;
        private const int GatherStops = 4;

        private CampCarry CarryOf(Pawn p) => p == null ? null : carries.FirstOrDefault(c => c.by == p);

        public bool Carrying(Pawn p) => CarryOf(p) != null;

        /// <summary>A good turn of this kind already on its way (one at a time).</summary>
        private bool Pending(string help) => carries.Any(c => c.help == help);

        /// <summary>Who of them would go: grown, on their feet, free of another carry.</summary>
        private IEnumerable<Pawn> Free => Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && !p.InMentalState && !Carrying(p));

        // ---------------- Setting one going ----------------

        /// <summary>The healer goes out at dawn gathering herbs ("medicine", "herbs") and carries them over. False with no one to go.</summary>
        private bool StartHerbs(string help)
        {
            if (Pending(help)) return false;
            var healer = Free.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) || !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
            // Somewhere to gather at all: wild healroot, or wild ground cover beyond the camp (none in a dead winter).
            if (healer == null || !share.IsValid || (WildHerb(healer) ?? GatherPlant(healer)) == null) return false;
            int hour = GenLocalDate.HourOfDay(map);
            int wait = ((DawnHour - hour) % 24 + 24) % 24;
            int start = Now + wait * GenDate.TicksPerHour - GenLocalDate.DayTick(map) % GenDate.TicksPerHour;
            carries.Add(new CampCarry
            {
                by = healer, help = help, def = ThingDefOf.MedicineHerbal, want = Rand.RangeInclusive(2, 4), to = share,
                from = start, until = start + GenDate.TicksPerHour * 10
            });
            return true;
        }

        /// <summary>A drover milks or shears a beast of their herd that's ready for it, and carries what it gives over. False with none ready.</summary>
        private bool StartMilk()
        {
            if (Pending("milk") || ReadyBeast(null) == null || !share.IsValid) return false;
            var drover = Free.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
            if (drover == null || ReadyBeast(drover) == null) return false;
            carries.Add(new CampCarry { by = drover, help = "milk", to = share, from = Now, until = Now + GenDate.TicksPerHour * 8 });
            return true;
        }

        /// <summary>Their trader takes something from the stock for the colony, worth a little, and carries it over. False with nothing to spare.</summary>
        private bool StartGoods()
        {
            var t = trader;
            var inv = t?.inventory;
            if (inv == null || t.Downed || !Here.Contains(t) || Carrying(t) || Pending("goods") || !share.IsValid) return false;
            float budget = Rand.Range(60f, 140f) * LivingEvents.WealthFactor(map);
            var pick = inv.innerContainer.Where(x => x.def.category == ThingCategory.Item && !(x is Pawn) && x.def != ThingDefOf.Silver && !inv.NotForSale(x)
                                                     && x.MarketValue > 0f && x.MarketValue <= budget).RandomElementWithFallback();
            if (pick == null) return false;
            int n = Mathf.Clamp(Mathf.FloorToInt(budget / pick.MarketValue), 1, pick.stackCount);
            carries.Add(new CampCarry { by = t, help = "goods", def = pick.def, count = n, to = share, from = Now, until = Now + GenDate.TicksPerHour * 6, carrying = true });
            return true;
        }

        /// <summary>
        /// Goods the colony bought from them: one of their people (not their trader, who keeps the stall) carries them to
        /// the colony's gate and sets them down there. False with nobody to send or no way there.
        /// </summary>
        public bool StartBring(ThingDef def, int count)
        {
            if (def == null || count <= 0 || map == null || !Settled) return false;
            var porter = Free.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling)).OrderBy(p => p == trader ? 1 : 0).ThenBy(p => p == leader ? 1 : 0).FirstOrDefault();
            if (porter == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            carries.Add(new CampCarry { by = porter, help = "bought", def = def, count = count, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 12, carrying = true });
            return true;
        }

        /// <summary>Whether someone of theirs could carry goods to the colony's gate now (for the ask that sends them).</summary>
        public bool CanBring => Settled && Free.Any(p => !p.WorkTagIsDisabled(WorkTags.Hauling) && ErrandCell("gate", null, p).IsValid);

        // ---------------- Their jobs ----------------

        /// <summary>The next step of what this one of them has to bring: gathering, milking, or the walk over with it. Null when nothing is due.</summary>
        public Job CarryJob(Pawn p)
        {
            var c = CarryOf(p);
            if (c == null || Now < c.from) return null;
            if (!c.carrying)
            {
                var job = c.help == "milk" ? MilkJob(p, c) : GatherJob(p, c);
                if (job != null) return job;
                // Nothing more to gather or milk: off with what they have, if anything.
                if (c.count <= 0)
                {
                    carries.Remove(c);
                    return null;
                }
                c.carrying = true;
            }
            if (!CanTakeUp(p, c))
            {
                carries.Remove(c);
                return null;
            }
            var cell = SetDownCell(p, c.to);
            if (!cell.IsValid)
            {
                carries.Remove(c);
                return null;
            }
            var bring = JobMaker.MakeJob(CampDefOf.Council_CampBring, cell);
            bring.locomotionUrgency = LocomotionUrgency.Walk;
            return bring;
        }

        private Job GatherJob(Pawn p, CampCarry c)
        {
            if (c.count >= c.want || c.stops >= GatherStops) return null;
            var plant = WildHerb(p) ?? GatherPlant(p);
            return plant == null ? null : JobMaker.MakeJob(CampDefOf.Council_CampGather, plant);
        }

        private bool MayGather(IntVec3 c) =>
            c.InBounds(map) && c.InHorDistOf(center, GatherRadius) && !(termBoundary && !bendBoundary && ColonySide(c)) && ClearOfColony(c);

        /// <summary>Wild healroot near the camp, nobody's (never sown, never marked), clear of the colony: the nearest first.</summary>
        private Plant WildHerb(Pawn p)
        {
            Plant best = null;
            float bestD = float.MaxValue;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
            {
                if (!(t is Plant plant) || plant.def.plant?.harvestedThingDef != ThingDefOf.MedicineHerbal || plant.sown || !plant.HarvestableNow) continue;
                if (map.designationManager.DesignationOn(plant) != null || !MayGather(plant.Position)) continue;
                float d = plant.Position.DistanceToSquared(p.Position);
                if (d >= bestD || !p.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) continue;
                best = plant;
                bestD = d;
            }
            return best;
        }

        /// <summary>A stop on the gathering walk: wild ground cover beyond the camp, away from the colony, where herbs grow among the grass.</summary>
        private Plant GatherPlant(Pawn p)
        {
            var near = (center + Away * 12).ClampInsideMap(map);
            if (!CellFinder.TryFindRandomCellNear(near, map, 14, x => x.GetPlant(map) is Plant g && !g.sown && g.def.plant != null && !g.def.plant.IsTree
                                                                    && map.designationManager.DesignationOn(g) == null && MayGather(x)
                                                                    && p.CanReserveAndReach(g, PathEndMode.Touch, Danger.Some), out var cell))
                return null;
            return cell.GetPlant(map);
        }

        /// <summary>A gathering stop done: wild healroot harvested as the game harvests it, or a bundle picked among the grass. Into their pack.</summary>
        public void Gathered(Pawn p, Thing t)
        {
            var c = CarryOf(p);
            if (c == null || c.carrying || !(t is Plant plant) || !plant.Spawned || p.inventory == null) return;
            c.stops++;
            int n = 1;
            if (plant.def.plant?.harvestedThingDef == ThingDefOf.MedicineHerbal && !plant.sown && plant.HarvestableNow)
            {
                n = Mathf.Max(1, plant.YieldNow());
                plant.PlantCollected(p, PlantDestructionMode.Cut);
            }
            var herbs = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
            herbs.stackCount = n;
            p.inventory.TryAddItemNotForSale(herbs);
            c.def = ThingDefOf.MedicineHerbal;
            c.count += n;
            c.taken = true;
        }

        private Job MilkJob(Pawn p, CampCarry c)
        {
            if (c.stops > 0) return null;
            var beast = ReadyBeast(p);
            if (beast == null) return null;
            return JobMaker.MakeJob(Gatherable(beast) is CompShearable ? CampDefOf.Council_CampShear : CampDefOf.Council_CampMilk, beast);
        }

        /// <summary>A beast of their herd ready to be milked or shorn (the game's own milk and wool), the nearest to <paramref name="by"/> first.</summary>
        private Pawn ReadyBeast(Pawn by) =>
            herd.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed && a.Map == map && GroupOf(a)?.follow != true && Gatherable(a) != null
                            && (by == null || by.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some)))
                .OrderBy(a => by == null ? 0 : a.Position.DistanceToSquared(by.Position)).FirstOrDefault();

        /// <summary>Whether any beast of their herd gives milk or wool at all, ready now or later (for a good turn asked of them).</summary>
        public bool HerdGives => herd.Any(a => a != null && !a.Dead && a.AllComps != null && a.AllComps.Any(x => Gives(a, x)));

        private static bool Gives(Pawn a, ThingComp x)
        {
            var stage = a.ageTracker?.CurLifeStage;
            if (stage == null) return false;
            if (x is CompShearable) return stage.shearable;
            return x is CompMilkable m && stage.milkable && (!m.Props.milkFemaleOnly || a.gender == Gender.Female);
        }

        private static CompHasGatherableBodyResource Gatherable(Pawn a) =>
            a?.AllComps?.OfType<CompHasGatherableBodyResource>().FirstOrDefault(x => (x is CompMilkable || x is CompShearable) && x.ActiveAndFull);

        /// <summary>The beast milked or shorn, as the game does it; what it gave goes into the drover's pack.</summary>
        public void Milked(Pawn p, Pawn beast)
        {
            var c = CarryOf(p);
            if (c == null || c.carrying || beast == null || p.inventory == null) return;
            c.stops++;
            var comp = beast.Spawned ? Gatherable(beast) : null;
            var def = comp is CompMilkable m ? m.Props.milkDef : comp is CompShearable s ? s.Props.woolDef : null;
            if (def == null || !p.Spawned) return;
            var before = new HashSet<int>(GenRadial.RadialDistinctThingsAround(p.Position, map, 5f, true).Where(x => x.def == def).Select(x => x.thingIDNumber));
            comp.Gathered(p);
            foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, map, 5f, true).Where(x => x.def == def && !before.Contains(x.thingIDNumber)).ToList())
            {
                int n = t.stackCount;
                if (t.Spawned) t.DeSpawn();
                p.inventory.TryAddItemNotForSale(t);
                c.count += n;
            }
            c.def = def;
            c.taken = c.count > 0;
        }

        /// <summary>A free spot near where it goes, to set it down: open ground, nothing lying or built there, in no stockpile.</summary>
        private IntVec3 SetDownCell(Pawn p, IntVec3 to)
        {
            if (!to.IsValid || !to.InBounds(map)) return IntVec3.Invalid;
            if (CellFinder.TryFindRandomCellNear(to, map, 3, x => x.Standable(map) && x.GetFirstItem(map) == null && x.GetEdifice(map) == null && x.GetSlotGroup(map) == null
                                                               && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell))
                return cell;
            return p.CanReach(to, PathEndMode.OnCell, Danger.Some) ? to : IntVec3.Invalid;
        }

        /// <summary>They have it to bring (in hand or pack), or can still take it (from the stock, from their stores for the road).</summary>
        private bool CanTakeUp(Pawn p, CampCarry c)
        {
            if (c.def == null) return false;
            if (p.carryTracker?.CarriedThing?.def == c.def) return true;
            var inv = p.inventory;
            if (inv == null) return false;
            if (c.taken) return inv.innerContainer.Any(t => t.def == c.def && inv.NotForSale(t));
            if (c.help == "goods") return inv.innerContainer.Any(t => t.def == c.def && !inv.NotForSale(t));
            return c.help == "bought";
        }

        /// <summary>
        /// Into their arms for the walk over: what's in their pack for it, or (the first time) the gift out of the trader's
        /// stock, or the sacks the colony bought out of their stores for the road. As much as they can carry in their
        /// arms; the rest stays in the pack and is set down with it.
        /// </summary>
        public bool TakeUp(Pawn p)
        {
            var c = CarryOf(p);
            if (c == null || c.def == null || p.carryTracker == null || p.inventory == null) return false;
            var held = p.carryTracker.CarriedThing;
            if (held != null) return held.def == c.def;
            int space = Mathf.Max(1, p.carryTracker.AvailableStackSpace(c.def));
            var inv = p.inventory;
            Thing thing = null;
            if (!c.taken && c.help == "goods")
            {
                var stock = inv.innerContainer.FirstOrDefault(t => t.def == c.def && !inv.NotForSale(t));
                if (stock == null) return false;
                thing = stock.SplitOff(Mathf.Min(c.count, stock.stackCount, space));
                c.count = thing.stackCount;
            }
            else if (!c.taken && c.help == "bought")
            {
                thing = ThingMaker.MakeThing(c.def, c.def.MadeFromStuff ? GenStuff.DefaultStuffFor(c.def) : null);
                thing.stackCount = Mathf.Min(c.count, space, c.def.stackLimit);
                for (int left = c.count - thing.stackCount; left > 0;)
                {
                    var rest = ThingMaker.MakeThing(c.def, c.def.MadeFromStuff ? GenStuff.DefaultStuffFor(c.def) : null);
                    rest.stackCount = Mathf.Min(left, c.def.stackLimit);
                    left -= rest.stackCount;
                    inv.TryAddItemNotForSale(rest);
                }
            }
            else
            {
                var mine = inv.innerContainer.FirstOrDefault(t => t.def == c.def && inv.NotForSale(t));
                if (mine == null) return false;
                thing = mine.SplitOff(Mathf.Min(mine.stackCount, space));
            }
            c.taken = true;
            thing.holdingOwner?.Remove(thing);
            if (p.carryTracker.innerContainer.TryAdd(thing)) return true;
            inv.TryAddItemNotForSale(thing);
            return false;
        }

        /// <summary>Broken off on the way: what they hold goes back in their pack, for the next try.</summary>
        public void BackInPack(Pawn p)
        {
            var c = CarryOf(p);
            var held = p?.carryTracker?.CarriedThing;
            if (c == null || held == null || held.def != c.def || p.inventory == null) return;
            p.carryTracker.innerContainer.Remove(held);
            p.inventory.TryAddItemNotForSale(held);
        }

        /// <summary>There: what they carry is set down for the colony (with any of it left in their pack), and the colony hears of it.</summary>
        public void SetDown(Pawn p, IntVec3 cell)
        {
            var c = CarryOf(p);
            if (c == null || map == null) return;
            carries.Remove(c);
            var placed = new List<Thing>();
            int n = 0;
            var held = p.carryTracker?.CarriedThing;
            if (held != null && held.def == c.def)
            {
                int k = held.stackCount;
                if (p.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Near, out var lying))
                {
                    n += k;
                    if (lying != null) placed.Add(lying);
                }
            }
            var inv = p.inventory;
            if (inv != null)
                foreach (var t in inv.innerContainer.Where(t => t.def == c.def && inv.NotForSale(t)).ToList())
                {
                    if (n >= c.count) break;
                    int k = Mathf.Min(t.stackCount, c.count - n);
                    if (inv.innerContainer.TryDrop(t, cell, map, ThingPlaceMode.Near, k, out var lying))
                    {
                        n += k;
                        if (lying != null) placed.Add(lying);
                    }
                }
            foreach (var t in placed) t.SetForbidden(false, false);
            if (n > 0) Brought(p, c, n, placed.FirstOrDefault());
        }

        /// <summary>Told once it's there: the good turn's letter, or the colony's purchase set down by the gate.</summary>
        private void Brought(Pawn p, CampCarry c, int n, Thing look)
        {
            var colonists = IncidentWorker_Happening.Colonists(map);
            var a = colonists.RandomElementWithFallback();
            string text;
            switch (c.help)
            {
                case "bought":
                    Note(Text("CC_CampBring_Done", p, null, n, c.def.label), MessageTypeDefOf.PositiveEvent, Importance.Notable, look);
                    return;
                case "medicine":
                    text = Text("CC_Camp_Help_medicine", p, null, n);
                    foreach (var x in colonists) CouncilGameComponent.GiveThought(x, CampDefOf.Council_Camp_Helped);
                    break;
                case "goods":
                    text = HelpText("goods", p, a, n, GenLabel.ThingLabel(c.def, null, n));
                    break;
                default:
                    text = HelpText(c.help, p, a, n, c.def.label);
                    break;
            }
            if (text.NullOrEmpty()) return;
            var target = look != null && look.Spawned ? new LookTargets(look) : Look;
            Find.LetterStack.ReceiveLetter(Text("CC_Camp_HelpTitle"), text, LetterDefOf.PositiveEvent, target, faction);
        }

        /// <summary>Every hour: one whose bearer is gone, down, or too long about it is let go (what's in their pack stays theirs).</summary>
        private void WatchCarries()
        {
            carries.RemoveAll(c => c == null || c.by == null || c.by.Dead || c.by.Downed || !Here.Contains(c.by) || Now > c.until);
        }

        private void ExposeCarries()
        {
            if (Scribe.mode == LoadSaveMode.Saving) carries.RemoveAll(c => c == null || c.by == null || c.by.Discarded || c.by.Dead);
            Scribe_Collections.Look(ref carries, "carries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                carries ??= new List<CampCarry>();
                carries.RemoveAll(c => c == null || c.by == null || c.def == null && c.help != "medicine" && c.help != "herbs" && c.help != "milk");
            }
        }
    }

    /// <summary>
    /// One of a camp's people carrying something over for the colony (see <see cref="CampCarry"/>): out of their pack (or
    /// their trader's stock) into their arms, the walk to the spot, and it's set down there. Broken off, what they hold
    /// goes back in their pack for the next try.
    /// </summary>
    public class JobDriver_CampBring : JobDriver
    {
        private bool setDown;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref setDown, "setDown");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CampTracker.CampOf(pawn) == null);
            AddFinishAction(_ =>
            {
                try
                {
                    if (!setDown) CampTracker.CampOf(pawn)?.BackInPack(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp carry end", e);
                }
            });
            var take = ToilMaker.MakeToil("CampBringTake");
            take.initAction = () =>
            {
                if (CampTracker.CampOf(pawn)?.TakeUp(pawn) != true) EndJobWith(JobCondition.Incompletable);
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return take;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var put = ToilMaker.MakeToil("CampBringSetDown");
            put.initAction = () =>
            {
                setDown = true;
                CampTracker.CampOf(pawn)?.SetDown(pawn, job.targetA.Cell);
            };
            put.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return put;
        }
    }
}
