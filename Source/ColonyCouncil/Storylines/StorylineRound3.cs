using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // What the storylines of round 3 share (docs/quests-proposals.md, S6, S9, S10, S11, S12; DESIGN.md "Storylines, round 3"):
    // an area marked on the colony's map for a part (the place a colonist dreamed of, the ground where a founder lies), a search
    // of that ground spot by spot (the lost keepsake's way, Missions/HomeQuests.cs), goods asked for and collected by the
    // people's carts, and the dev actions. Each storyline's own rules are in its own file beside this one.

    /// <summary>A part that marks an area of the colony's map while it runs (drawn as the keepsake's search area is).</summary>
    public interface IStorylineArea
    {
        /// <summary>The area's middle and radius while it is marked; false when nothing is marked now.</summary>
        bool Marked(Mission m, Map map, out IntVec3 center, out float radius);
    }

    /// <summary>A part whose area is searched spot by spot: right-click inside it with a colonist.</summary>
    public interface IStorylineSearch : IStorylineArea
    {
        /// <summary>The menu's words for a search here ("Search here for Edric's grave").</summary>
        string SearchLabel(Mission m);

        /// <summary>The job's words while someone searches.</summary>
        string SearchReport(Mission m);

        /// <summary>One look's reach (cells).</summary>
        float LookRadius { get; }

        /// <summary>Someone has looked around <paramref name="spot"/>: true when it was found there.</summary>
        bool Looked(Mission m, Pawn p, IntVec3 spot);
    }

    /// <summary>The round's shared helpers: the open part with an area, the search's spots, goods collected.</summary>
    public static class StorylineRound3
    {
        /// <summary>The open storyline part whose worker marks an area on this map now, if any.</summary>
        public static Mission Marking(Map map, out IStorylineArea area, out IntVec3 center, out float radius)
        {
            area = null;
            center = IntVec3.Invalid;
            radius = 0f;
            if (map == null) return null;
            foreach (var m in Missions.All)
            {
                if (!m.Open || m.state != MissionState.Active || !(Missions.Worker(m) is IStorylineArea a) || m.Map != map) continue;
                if (!a.Marked(m, map, out center, out radius)) continue;
                area = a;
                return m;
            }
            return null;
        }

        /// <summary>The open part being searched for on this map now, if any.</summary>
        public static Mission Searching(Map map, out IStorylineSearch search)
        {
            var m = Marking(map, out var a, out _, out _);
            search = a as IStorylineSearch;
            return search != null ? m : null;
        }

        public static IEnumerable<IntVec3> AreaCells(Map map, IntVec3 center, float radius) =>
            GenRadial.RadialCellsAround(center, radius, true).Where(c => c.InBounds(map));

        /// <summary>The spots looked at in each search this session (a reload starts the record again; nothing else depends on it).</summary>
        private static readonly Dictionary<int, List<IntVec3>> looked = new Dictionary<int, List<IntVec3>>();

        public static void Forget(Mission m)
        {
            if (m != null) looked.Remove(m.id);
        }

        /// <summary>A colonist has looked around a spot: found, or on to the nearest spot no look has covered yet.</summary>
        public static void Looked(Pawn p, IntVec3 spot)
        {
            var map = p?.Map;
            var m = Searching(map, out var search);
            if (m == null || !search.Marked(m, map, out var center, out float radius)) return;
            if (!looked.TryGetValue(m.id, out var list)) looked[m.id] = list = new List<IntVec3>();
            list.Add(spot);
            if (search.Looked(m, p, spot))
            {
                looked.Remove(m.id);
                return;
            }
            float r = search.LookRadius;
            var next = AreaCells(map, center, radius).Where(c => c.Standable(map) && !list.Any(d => (c - d).LengthHorizontalSquared <= r * r))
                .OrderBy(c => c.DistanceToSquared(p.Position)).FirstOrDefault(c => p.CanReach(c, PathEndMode.OnCell, Danger.Some));
            var def = JobDriver_StorylineSearch.Def;
            if (!next.IsValid || def == null)
            {
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Story3_Searched".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                return;
            }
            p.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(def, next), JobTag.Misc);
        }

        // ---------------- Goods asked for, collected by their carts ----------------

        /// <summary>Their carts come for it and pay (Couriers/); else it is taken from the stores and the silver left at the gate.</summary>
        public static void Collect(Map map, Faction f, string what, int count, int pay)
        {
            if (map == null || count <= 0) return;
            if (Couriers.CollectGoals(map, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(what, count) }, f, pay)) return;
            Missions.Take(map, what, count);
            if (pay > 0) Couriers.PaySilver(map, DropCellFinder.TradeDropSpot(map), pay, f);
        }

        /// <summary>A free colonist at home who can walk and isn't busy with a fight: for a part that sends someone.</summary>
        public static bool Free(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.IsFreeColonist && !p.IsQuestLodger() && p.DevelopmentalStage.Adult();
    }

    /// <summary>The area a part marks, drawn on the map while it is marked (the keepsake's search area's look).</summary>
    public class StorylineAreaOverlay : MapComponent
    {
        private static readonly Color Edge = new Color(0.95f, 0.8f, 0.35f, 0.8f);
        private List<IntVec3> cells;
        private IntVec3 cellsFor = IntVec3.Invalid;
        private float radiusFor;

        public StorylineAreaOverlay(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            try
            {
                if (StorylineRound3.Marking(map, out _, out var center, out float radius) == null) return;
                if (cells == null || cellsFor != center || radiusFor != radius)
                {
                    cells = StorylineRound3.AreaCells(map, center, radius).ToList();
                    cellsFor = center;
                    radiusFor = radius;
                }
                if (cells.Count > 0) GenDraw.DrawFieldEdges(cells, Edge);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storylines: an area drawn", e);
            }
        }
    }

    /// <summary>
    /// A search of a part's marked area (StorylineGrave.cs): the colonist walks to a spot and searches the ground around it for
    /// about an hour; found, the part says so; not found, they go on to the nearest spot not yet searched.
    /// </summary>
    public class JobDriver_StorylineSearch : JobDriver
    {
        public const string JobName = "Rimfolk_Storyline_Search";

        public static JobDef Def => DefDatabase<JobDef>.GetNamedSilentFail(JobName);

        /// <summary>One look takes about an hour of the game's time (the keepsake's own).</summary>
        public const int LookTicks = 2000;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var m = StorylineRound3.Searching(pawn.Map, out var s);
            return m != null ? s.SearchReport(m) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => StorylineRound3.Searching(pawn.Map, out _) == null);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var look = ToilMaker.MakeToil("StorylineSearch");
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = LookTicks;
            look.WithProgressBarToilDelay(TargetIndex.A);
            look.handlingFacing = true;
            look.tickIntervalAction = delta =>
            {
                // Turning to look around the spot, now this way, now that.
                if (pawn.IsHashIntervalTick(250, delta)) pawn.Rotation = Rot4.Random;
            };
            yield return look;
            yield return Toils_General.Do(() => StorylineRound3.Looked(pawn, job.targetA.Cell));
        }
    }

    /// <summary>Right-clicking inside a part's search area with a colonist selected: search here.</summary>
    public class FloatMenuOptionProvider_StorylineSearch : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => false;

        protected override bool AppliesInt(FloatMenuContext context) => StorylineRound3.Searching(context.map, out _) != null;

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            var c = context.FirstSelectedPawn;
            var m = StorylineRound3.Searching(context.map, out var s);
            if (c == null || m == null || !c.IsColonistPlayerControlled || c.DevelopmentalStage.Baby() || !s.Marked(m, context.map, out var center, out float radius)) return null;
            var cell = context.ClickedCell;
            if (!cell.InHorDistOf(center, radius)) return null;
            string label = s.SearchLabel(m);
            if (!cell.Standable(context.map) || !c.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                return new FloatMenuOption(label + " (" + "CC_HomeQ_Keepsake_NoReach".Translate().Resolve() + ")", null);
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                var def = JobDriver_StorylineSearch.Def;
                if (def != null) c.jobs.TryTakeOrderedJob(JobMaker.MakeJob(def, cell), JobTag.Misc);
            }), c, cell);
        }
    }

    /// <summary>Dev mode: round 3's storylines started now, and the moments each waits for brought on now.</summary>
    public static class StorylinesDebug3
    {
        private static void Say(string text) => Messages.Message("Storylines: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Storylines: start a dream of the gods now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDream() => StorylinesDebug.Start(DreamFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start raiders settling nearby now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRaiders() => StorylinesDebug.Start(RaidersFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start the town's well ran dry now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWell() => StorylinesDebug.Start(WellFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start the vault's sealed case now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVault() => StorylinesDebug.Start(VaultFlow.Key);

        [DebugAction("Rimfolk", "Storylines: start the lost founder's grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrave() => StorylinesDebug.Start(GraveFlow.Key);

        /// <summary>
        /// What the open part of round 3 waits for, now: the people it waits for come, the dream's sign at the watch, the raid
        /// that tests the colony, the lent hand's days or the case's days over, the grave found.
        /// </summary>
        [DebugAction("Rimfolk", "Storylines: what the open part waits for comes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNow()
        {
            var s = Storylines.Running.FirstOrDefault(x => x.missionId >= 0 && Missions.ById(x.missionId)?.state == MissionState.Active);
            var m = s == null ? null : Missions.ById(s.missionId);
            if (m == null || !(Missions.Worker(m) is IStorylineDebugNow w))
            {
                Say("no part of round 3's storylines is under way.");
                return;
            }
            Say(s.kind + ": " + w.DebugNow(m, m.Map));
        }
    }

    /// <summary>A part of round 3 whose moment the dev menu can bring on now.</summary>
    public interface IStorylineDebugNow
    {
        /// <summary>Brings it on; what happened, in a few words.</summary>
        string DebugNow(Mission m, Map map);
    }
}
