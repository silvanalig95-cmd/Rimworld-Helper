using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Midwinter with Vanilla Christmas Expanded (C29 of docs/content-update-proposals.md). On the colony's midwinter day (the
    /// winter god's festival, Hush's Stillness or Selene's Longest Night, kept on the calendar; or the midwinter custom, the
    /// longest night's supper, kept), with the mod's things where they are built or in the stores:
    /// <list type="bullet">
    /// <item>At dusk someone walks to each festive tree and string of fairy lamps that is off and switches it on (lit where
    /// there is power), or stands a moment at one already lit.</item>
    /// <item>At supper the gingerbread, eggnog and hot cocoa in the stores are carried to the festive feast table (else the
    /// colony's dining table) and set out beside it, for whoever wants them.</item>
    /// <item>After supper the eldest at home takes the festive presents from the stores and hands one to each of the colony,
    /// person by person, with a word; each thinks a little better of them for it.</item>
    /// <item>By day, where the snow is deep enough near home, someone makes a snow sculpture of the mod's where the game's own
    /// snowman would go.</item>
    /// </list>
    /// Each part only when its things are there, and the words say only what was done. None of it without the mod. The
    /// winter god's own festival play-out (Hush's silent supper, Selene's watch on the walls: C1, built beside this) is not
    /// touched here; this runs beside it from the calendar's keeping. No harm.
    /// </summary>
    public static class Midwinter
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_Midwinter";

        private static readonly string[] Trees = { "VCE_GrandFestiveTree", "VCE_StandingFestiveTree", "VCE_FairyLamps" };
        private static readonly string[] Food = { "VCE_Gingerbread", "VCE_Eggnog", "VCE_HotCocoa" };

        private static ThingDef Present => DefDatabase<ThingDef>.GetNamedSilentFail("VCE_FestivePresent");

        private static ThingDef Sculpture => DefDatabase<ThingDef>.GetNamedSilentFail("VCE_SnowSculpture");

        private static ThingDef FeastTable => DefDatabase<ThingDef>.GetNamedSilentFail("VCE_FestiveFeastTable");

        private static ThoughtDef Gift => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_MidwinterPresent");

        /// <summary>Vanilla Christmas Expanded is loaded.</summary>
        public static bool On => Present != null || DefDatabase<ThingDef>.GetNamedSilentFail("VCE_GrandFestiveTree") != null;

        private static int Now => GodsAmongBook.Now;

        private const int TreeBit = 1, TableBit = 2, PresentsBit = 4, SnowBit = 8, TreeWalkBit = 16;

        // ================= Kept =================

        /// <summary>A day of the calendar kept (CalendarKeeping.KeepDay): the winter god's festival or the midwinter custom is the colony's midwinter.</summary>
        public static void DayKept(CalendarDay d, Map map)
        {
            if (d == null || map == null) return;
            if (GodsAmongMath.IsMidwinter(d.id, ColonyCalendar.GodOfDay(d)?.Key)) Kept(map);
        }

        /// <summary>The colony keeps midwinter today at this home: the evening's parts come in their hours.</summary>
        public static bool Kept(Map map)
        {
            var b = GodsAmongBook.Book;
            if (b == null || map == null || !On || !Homes.IsHome(map)) return false;
            if (b.mwMap == map && b.mwDay == LocalDay(map)) return true;
            b.mwMap = map;
            b.mwDay = LocalDay(map);
            b.mwDone = 0;
            b.mwGiver = null;
            b.mwGiven = null;
            b.mwSculptor = null;
            b.mwSnowAt = IntVec3.Invalid;
            b.mwSnowUntil = -1;
            b.Due(Now + 1);
            return true;
        }

        /// <summary>Midwinter is being kept at this home today (the giving day lights no lamps of its own then: LifeVariety/GivingDay.cs).</summary>
        public static bool KeepingToday(Map map)
        {
            var b = GodsAmongBook.Book;
            return b != null && map != null && b.mwMap == map && b.mwDay == LocalDay(map);
        }

        /// <summary>The home's own day (its year and day of the year), so the evening runs by its own clock.</summary>
        private static int LocalDay(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        private static bool Done(GodsAmongBook b, int bit) => (b.mwDone & bit) != 0;

        private static void Mark(GodsAmongBook b, int bit) => b.mwDone |= bit;

        private static bool Free(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && Worship.Ours(p)
                                            && WorshipJobs.Free(p, false) && !MapFacts.ThreatOn(p.Map);

        private static List<Pawn> Home(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.Dead).ToList();

        /// <summary>The gods' hour: the evening's parts as their hours come, and the snow sculpture by day.</summary>
        internal static void Hourly(GodsAmongBook b, int now)
        {
            var map = b.mwMap;
            if (map == null || b.mwDay < 0) return;
            int day = LocalDay(map);
            if (!Homes.IsHome(map) || day > b.mwDay + 1 || day < b.mwDay)
            {
                End(b);
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            int stage = day == b.mwDay ? GodsAmongMath.StageAt(hour) : 4;
            if (stage >= 4 && hour >= 6 && day > b.mwDay)
            {
                End(b);
                return;
            }
            if (MapFacts.ThreatOn(map)) return;
            if (!Done(b, SnowBit) && hour >= 9 && hour < 15 && day == b.mwDay) Snow(b, map);
            if (stage >= 1 && !Done(b, TreeBit)) Tree(b, map);
            if (stage >= 2 && !Done(b, TableBit)) Table(b, map);
            if (stage >= 3 && stage < 4 && !Done(b, PresentsBit)) Presents(b, map);
            b.Due(now + 250);
        }

        private static void End(GodsAmongBook b)
        {
            // Presents not yet handed round go back to the floor at the giver's feet.
            var giver = b.mwGiver;
            if (giver != null && giver.Spawned && giver.inventory?.innerContainer != null)
                foreach (var t in giver.inventory.innerContainer.Where(x => x.def == Present).ToList())
                    giver.inventory.innerContainer.TryDrop(t, giver.Position, giver.Map, ThingPlaceMode.Near, out _);
            b.mwMap = null;
            b.mwDay = -1;
            b.mwGiver = null;
            b.mwGiven = null;
            b.mwSculptor = null;
        }

        /// <summary>The steps between the hours: the walk to the tree, the presents handed round, the snow sculpture finished.</summary>
        internal static void Step(GodsAmongBook b, int now)
        {
            var map = b.mwMap;
            if (map == null || b.mwDay < 0) return;
            bool more = false;
            if (Done(b, TreeBit) && !Done(b, TreeWalkBit)) more |= TreeStep(b, map);
            if (foodCarrier != null) more |= FoodStep(map);
            if (b.mwGiver != null) more |= PresentsStep(b, map, now);
            if (b.mwSculptor != null) more |= SnowStep(b, map, now);
            if (more) b.Due(now + 120);
        }

        // ================= The tree =================

        private static List<Building> TreesOn(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(x => x != null && x.Spawned && Trees.Contains(x.def.defName)).ToList();

        /// <summary>Dusk: someone walks to the festive tree (the first that is off, else the first) to switch it on, or to stand at it.</summary>
        private static void Tree(GodsAmongBook b, Map map)
        {
            Mark(b, TreeBit);
            var trees = TreesOn(map);
            if (trees.Count == 0)
            {
                Mark(b, TreeWalkBit);
                return;
            }
            var tree = trees.FirstOrDefault(t => t.TryGetComp<CompFlickable>() is CompFlickable f && !f.SwitchIsOn) ?? trees[0];
            var p = Home(map).Where(Free).OrderBy(x => x.Position.DistanceToSquared(tree.Position)).FirstOrDefault();
            if (p == null || !OwnHappenings.GoTo(p, CellFinder.StandableCellNear(tree.Position, map, 2f), GenDate.TicksPerHour))
            {
                Mark(b, TreeWalkBit);
                return;
            }
            treeWalker = p;
            treeAt = tree;
        }

        // The walk to the tree is a few minutes: kept for the session only (a load lets it go).
        private static Pawn treeWalker;
        private static Building treeAt;

        private static bool TreeStep(GodsAmongBook b, Map map)
        {
            var p = treeWalker;
            var tree = treeAt;
            if (p == null || tree == null || !tree.Spawned || p.Dead || !p.Spawned)
            {
                Mark(b, TreeWalkBit);
                return false;
            }
            if (!p.Position.InHorDistOf(tree.Position, 2.9f))
            {
                // Called away on the way: they go again once free.
                if (p.CurJobDef != JobDefOf.Goto && Free(p)) OwnHappenings.GoTo(p, CellFinder.StandableCellNear(tree.Position, map, 2f), GenDate.TicksPerHour);
                return true;
            }
            Mark(b, TreeWalkBit);
            treeWalker = null;
            treeAt = null;
            OwnHappenings.Stand(p, tree.Position, 400);
            int lit = 0;
            foreach (var t in TreesOn(map))
            {
                var f = t.TryGetComp<CompFlickable>();
                if (f == null || f.SwitchIsOn) continue;
                Traverse.Create(f).Field("wantSwitchOn").SetValue(true);
                f.SwitchIsOn = true;
                lit++;
            }
            bool powered = tree.TryGetComp<CompPowerTrader>()?.PowerOn ?? true;
            Worship.Say(p, Pool, null, null, lit > 0 ? powered ? "tree" : "treedark" : "treelit");
            string key = lit > 0 ? powered ? "CC_GA_Midwinter_TreeLit" : "CC_GA_Midwinter_TreeNoPower" : "CC_GA_Midwinter_TreeStood";
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, key.Translate(p.Named("PAWN"), tree.LabelNoCount.Named("TREE")).Resolve(), tree, MessageTypeDefOf.PositiveEvent);
            return false;
        }

        // ================= The table =================

        /// <summary>Supper: the midwinter food in the stores is carried to the festive feast table (else the dining table) and set out beside it.</summary>
        private static void Table(GodsAmongBook b, Map map)
        {
            Mark(b, TableBit);
            var all = map.listerBuildings.allBuildingsColonist;
            var table = (Building)all.FirstOrDefault(x => x != null && x.Spawned && x.def == FeastTable)
                        ?? all.Where(x => x != null && x.Spawned && x.def.surfaceType == SurfaceType.Eat && RoomUtil.IsDiningRoom(x.GetRoom()))
                            .OrderByDescending(x => x.def.size.Area).FirstOrDefault();
            if (table == null) return;
            var cells = GenAdj.CellsAdjacent8Way(table).Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstItem(map) == null).ToList();
            if (cells.Count == 0) return;
            var cell = cells[0];
            var carrier = Home(map).Where(Free).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
            if (carrier == null) return;
            foodQueue.Clear();
            var labels = new List<string>();
            foreach (var name in Food)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                var stack = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(carrier) && t.IsInAnyStorage()).OrderByDescending(t => t.stackCount).FirstOrDefault();
                if (stack == null) continue;
                labels.Add(def.label);
                foodQueue.Add((stack, Math.Min(stack.stackCount, GodsAmongMath.TableFoodMost)));
            }
            if (foodQueue.Count == 0) return;
            foodCarrier = carrier;
            foodCell = cell;
            foodTable = table;
            foodLabels = labels;
            b.Due(Now + 1);
            CarryNext(map);
        }

        // The carrying is an hour or so: kept for the session only (a load lets the rest go).
        private static readonly List<(Thing thing, int count)> foodQueue = new List<(Thing, int)>();
        private static Pawn foodCarrier;
        private static IntVec3 foodCell = IntVec3.Invalid;
        private static Building foodTable;
        private static List<string> foodLabels;

        /// <summary>The carrier free of the last load: the next, until all is set out.</summary>
        private static bool FoodStep(Map map)
        {
            var p = foodCarrier;
            if (p == null || p.Dead || !p.Spawned || p.Map != map)
            {
                foodQueue.Clear();
                foodCarrier = null;
                return false;
            }
            if (p.CurJobDef == JobDefOf.HaulToCell) return true;
            CarryNext(map);
            return foodCarrier != null;
        }

        private static void CarryNext(Map map)
        {
            while (foodQueue.Count > 0)
            {
                var (t, n) = foodQueue[0];
                foodQueue.RemoveAt(0);
                if (t == null || !t.Spawned) continue;
                if (OwnHappenings.Carry(foodCarrier, t, foodCell, n)) return;
            }
            // All of it carried (or as much as could be): said once.
            if (foodCarrier != null && foodTable != null && foodLabels != null && foodLabels.Count > 0)
            {
                Worship.Say(foodCarrier, Pool, null, null, "table");
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_GA_Midwinter_Table".Translate(foodCarrier.Named("PAWN"), TextUtil.JoinAnd(foodLabels).Named("FOOD"),
                    foodTable.LabelNoCount.Named("TABLE")).Resolve(), foodTable, MessageTypeDefOf.PositiveEvent);
            }
            foodCarrier = null;
            foodTable = null;
            foodLabels = null;
        }

        // ================= The presents =================

        /// <summary>After supper: the eldest at home takes the presents from the stores, one for each of the colony at home.</summary>
        private static void Presents(GodsAmongBook b, Map map)
        {
            Mark(b, PresentsBit);
            var def = Present;
            if (def == null) return;
            // The giving day kept at this home now: its gifts are the evening's presents, so midwinter hands none of its own
            // (the two never double: the content update's A26, LifeVariety/GivingDay.cs).
            if (GivingDay.KeptToday(map)) return;
            var people = Home(map);
            int inStore = map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => t.stackCount);
            int n = GodsAmongMath.Presents(people.Count, inStore);
            if (n <= 1) return;
            var giver = people.Where(p => Free(p) && p.DevelopmentalStage.Adult()).OrderByDescending(p => p.ageTracker.AgeBiologicalYears).FirstOrDefault();
            if (giver == null) return;
            // They walk to the stores and take them.
            int taken = 0;
            foreach (var stack in map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.IsInAnyStorage() && giver.CanReach(t, PathEndMode.Touch, Danger.Some)).ToList())
            {
                if (taken >= n) break;
                int take = Math.Min(n - taken, stack.stackCount);
                if (taken == 0 && !OwnHappenings.GoTo(giver, stack.Position, GenDate.TicksPerHour)) return;
                presentStacks.Add((stack, take));
                taken += take;
            }
            if (taken == 0) return;
            b.mwGiver = giver;
            b.mwGiven = null;
            presentsPicked = false;
            b.Due(Now + 1);
        }

        // Where the presents lie until the giver picks them up: kept for the session only.
        private static readonly List<(Thing stack, int count)> presentStacks = new List<(Thing, int)>();
        private static bool presentsPicked;

        private static bool Has(string given, Pawn p) => given != null && given.Split('|').Contains(p.ThingID);

        private static bool PresentsStep(GodsAmongBook b, Map map, int now)
        {
            var giver = b.mwGiver;
            var def = Present;
            if (giver == null || def == null || giver.Dead || !giver.Spawned || giver.Map != map)
            {
                b.mwGiver = null;
                return false;
            }
            if (!presentsPicked)
            {
                // At the stores: the presents go into their pack.
                var first = presentStacks.FirstOrDefault(s => s.stack != null && s.stack.Spawned);
                if (first.stack == null)
                {
                    presentsPicked = true;
                    presentStacks.Clear();
                }
                else if (giver.Position.InHorDistOf(first.stack.Position, 1.9f))
                {
                    foreach (var (stack, count) in presentStacks.ToList())
                    {
                        if (stack == null || !stack.Spawned) continue;
                        var part = stack.SplitOff(Math.Min(count, stack.stackCount));
                        if (!giver.inventory.innerContainer.TryAdd(part)) GenPlace.TryPlaceThing(part, giver.Position, map, ThingPlaceMode.Near);
                    }
                    presentStacks.Clear();
                    presentsPicked = true;
                }
                else
                {
                    if (giver.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(giver, first.stack.Position, GenDate.TicksPerHour);
                    return true;
                }
            }
            var carried = giver.inventory.innerContainer.FirstOrDefault(t => t.def == def);
            var next = Home(map).Where(p => p != giver && !Has(b.mwGiven, p) && p.Awake() && !p.Downed).OrderBy(p => p.Position.DistanceToSquared(giver.Position)).FirstOrDefault();
            if (carried == null || next == null)
            {
                Finish(b, map, giver);
                return false;
            }
            if (Together.Can(giver, next))
            {
                if (giver.inventory.innerContainer.TryTransferToContainer(carried, next.inventory.innerContainer, 1) > 0)
                {
                    // together: the giver walked over to them (Together.Can), and Speech.Say checks it again.
                    Worship.Say(giver, Pool, null, next, "present");
                    var gift = Gift;
                    if (gift != null) CouncilGameComponent.GiveThought(next, gift, giver);
                }
                b.mwGiven = (b.mwGiven.NullOrEmpty() ? "" : b.mwGiven + "|") + next.ThingID;
                return true;
            }
            if (giver.CurJobDef != JobDefOf.Goto || !giver.pather.Destination.Cell.InHorDistOf(next.Position, 3f))
                OwnHappenings.GoTo(giver, CellFinder.StandableCellNear(next.Position, map, 2f), GenDate.TicksPerHour);
            return true;
        }

        private static void Finish(GodsAmongBook b, Map map, Pawn giver)
        {
            int handed = (b.mwGiven ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Length;
            foreach (var t in giver.inventory.innerContainer.Where(x => x.def == Present).ToList())
                giver.inventory.innerContainer.TryDrop(t, giver.Position, map, ThingPlaceMode.Near, out _);
            b.mwGiver = null;
            if (handed == 0) return;
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_GA_Midwinter_Presents".Translate(giver.Named("PAWN"), handed.Named("COUNT")).Resolve(), giver, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.journals.WriteText(giver, "CC_GA_Midwinter_PresentsJournal".Translate(giver.Named("PAWN"), handed.Named("COUNT")).Resolve());
        }

        // ================= The snow sculpture =================

        private static JobDef BuildSnowman => DefDatabase<JobDef>.GetNamedSilentFail("BuildSnowman");

        /// <summary>By day, with the snow deep enough near home: someone makes a snow sculpture (the game's snowman work, finished as the mod's sculpture).</summary>
        private static void Snow(GodsAmongBook b, Map map)
        {
            Mark(b, SnowBit);
            var def = Sculpture;
            var job = BuildSnowman;
            if (def == null || job == null) return;
            var p = Home(map).Where(Free).OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 0).FirstOrDefault();
            if (p == null) return;
            var cell = SnowCell(p);
            if (!cell.IsValid) return;
            if (!OwnHappenings.Job(p, JobMaker.MakeJob(job, cell))) return;
            b.mwSculptor = p;
            b.mwSnowAt = cell;
            b.mwSnowUntil = Now + GenDate.TicksPerHour * 4;
            b.Due(Now + 250);
        }

        /// <summary>Deep enough snow near them, out in the open, with nothing on it (as the answers' snowmen look, PlayOut/PlaySnow.cs).</summary>
        private static IntVec3 SnowCell(Pawn x)
        {
            var map = x.Map;
            foreach (var c in GenRadial.RadialCellsAround(x.Position, 14f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.Fogged(map)) continue;
                if (map.snowGrid.GetDepth(c) < PlaySnow.Depth || c.IsForbidden(x) || !x.CanReserveAndReach(c, PathEndMode.Touch, Danger.None)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The snowman they made is finished as the mod's snow sculpture, theirs as its maker.</summary>
        private static bool SnowStep(GodsAmongBook b, Map map, int now)
        {
            var p = b.mwSculptor;
            if (p == null || now > b.mwSnowUntil || p.Dead)
            {
                b.mwSculptor = null;
                return false;
            }
            if (p.CurJobDef == BuildSnowman) return true;
            b.mwSculptor = null;
            var snowman = b.mwSnowAt.IsValid ? b.mwSnowAt.GetThingList(map).FirstOrDefault(t => t.def.defName == "Snowman") : null;
            var def = Sculpture;
            if (snowman == null || def == null) return false;
            try
            {
                var at = snowman.Position;
                snowman.Destroy();
                var made = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                made.SetFactionDirect(Faction.OfPlayer);
                made.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityCreatedByPawn(p, SkillDefOf.Artistic), ArtGenerationContext.Colony);
                var art = made.TryGetComp<CompArt>();
                if (art != null)
                {
                    art.InitializeArt(ArtGenerationContext.Colony);
                    art.JustCreatedBy(p);
                }
                GenSpawn.Spawn(made, at, map);
                Worship.Say(p, Pool, null, null, "snow");
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_GA_Midwinter_Snow".Translate(p.Named("PAWN"), made.LabelNoCount.Named("WHAT")).Resolve(), made, MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a midwinter snow sculpture", e);
            }
            return false;
        }

        // ================= Dev =================

        public static string DevKeep(Map map)
        {
            if (!On) return "Vanilla Christmas Expanded isn't loaded.";
            var b = GodsAmongBook.Book;
            if (b == null) return "the gods are off.";
            b.mwMap = null;
            if (!Kept(map)) return "midwinter can't be kept here now.";
            return "midwinter kept today: the tree from " + GodsAmongMath.TreeFrom + ", the table from " + GodsAmongMath.TableFrom + ", the presents from " + GodsAmongMath.PresentsFrom
                   + " o'clock, the snow by day (9 to 15) where it is deep enough.";
        }
    }

    /// <summary>The midwinter custom kept as a council answer (the longest night): the colony's midwinter, with Vanilla Christmas Expanded's things (Gods/Among/Midwinter.cs).</summary>
    public class DilemmaEffect_Midwinter : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Midwinter.On || ctx?.map == null) return;
            sheet.Play("CC_GA_Midwinter_Impact".Translate().Resolve(), "CC_GA_Midwinter_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the evening's parts each walk out on the map in their hours (Midwinter.Hourly).
            if (ctx?.map != null) Midwinter.Kept(ctx.map);
        }
    }
}
