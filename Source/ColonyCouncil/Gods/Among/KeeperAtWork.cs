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
    /// The keeper at work (C12 of docs/content-update-proposals.md). The colony's book of the gods has a keeper (GodLore.Keeper:
    /// the chaplain, else the moral guide, else the most devout, else the most lettered), who until now was never seen at the
    /// work:
    /// <list type="bullet">
    /// <item>When the colony's people tell of a god's sign the keeper didn't see (an entry of the book written from others'
    /// eyes, at a place on the land), the keeper walks out to the place within the hours after, stands and looks, and says
    /// one line in the old voice. The entry then counts the keeper among those who saw what is left there, in the keeper's
    /// own words after it (GodLore.KeeperLooked).</item>
    /// <item>In the evening, when the book has new entries since the keeper last sat to it, they go to a writing table and
    /// write them up: Vanilla Books Expanded's writer's table or typewriter's table where one is built, else a table, else a
    /// lectern; with none, nowhere (the book is kept all the same).</item>
    /// </list>
    /// One walk at a time; the keeper goes only when free (never from work the player gave them, never in a fight). No harm.
    /// </summary>
    public static class KeeperAtWork
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_Keeper";

        private static readonly string[] WritingTables = { "VBE_WritersTable", "VBE_TypewritersTable" };

        private static JobDef WriteJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Keeper_Write");

        private static bool Free(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && Worship.Ours(p)
                                            && WorshipJobs.Free(p, false) && !MapFacts.ThreatOn(p.Map);

        // ================= A sign the keeper didn't see =================

        /// <summary>
        /// An entry of the book was written from others' eyes (Gods.Remember, Gods.Moment) at a place on a home map: the keeper,
        /// if they weren't among them, walks out to look when they are free.
        /// </summary>
        internal static void Told(LoreEntry e, Map map, IntVec3 cell)
        {
            var b = GodsAmongBook.Book;
            if (b == null || e == null || map == null || !Homes.IsHome(map) || b.looker != null) return;
            var keeper = GodLore.Keeper(out _);
            if (keeper == null || !keeper.Spawned || keeper.Map != map) return;
            bool saw = e.who != null && e.who.Contains(keeper.LabelShort);
            bool place = cell.IsValid && cell.InBounds(map) && !cell.Fogged(map);
            float distance = place ? keeper.Position.DistanceTo(cell) : float.NaN;
            if (!GodsAmongMath.GoesToLook(saw, place, distance)) return;
            b.looker = keeper;
            b.lookMap = map;
            b.lookAt = cell;
            b.lookEntry = e.tick;
            b.lookPage = e.page;
            b.lookUntil = GodsAmongBook.Now + GodsAmongMath.LookHours * GenDate.TicksPerHour;
            b.Due(GodsAmongBook.Now + GenDate.TicksPerHour / 2);
        }

        /// <summary>The keeper's walk: free, they set out; there, they look, say their line, and the entry counts them.</summary>
        internal static void Step(GodsAmongBook b, int now)
        {
            var p = b.looker;
            if (p == null) return;
            if (p.Dead || p.Discarded || b.lookMap == null || p.MapHeld != b.lookMap || now > b.lookUntil)
            {
                b.ClearLook();
                return;
            }
            if (!p.Spawned)
            {
                b.Due(now + 250);
                return;
            }
            var at = b.lookAt;
            if (p.Position.InHorDistOf(at, 3f))
            {
                OwnHappenings.Stand(p, at, 360);
                Looked(b, p);
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto && Free(p))
            {
                var near = CellFinder.StandableCellNear(at, p.Map, 2f);
                if (!near.IsValid || !OwnHappenings.GoTo(p, near, GenDate.TicksPerHour * 2)) b.ClearLook();
            }
            b.Due(now + 200);
        }

        private static void Looked(GodsAmongBook b, Pawn p)
        {
            var lore = GodLore.Instance;
            var e = lore?.entries.LastOrDefault(x => x != null && x.tick == b.lookEntry && x.page == b.lookPage);
            var g = e?.page == null ? null : Gods.ById(lore.Named(e.page)?.god ?? -1);
            b.ClearLook();
            if (e == null) return;
            // apart: the keeper's word over what is left at the place, said to nobody in particular.
            string line = Worship.Line(Pool, p, g, null, g != null ? "look" : "lookunnamed");
            if (!line.NullOrEmpty()) Speech.Say(p, line);
            GodLore.KeeperLooked(e, p);
        }

        // ================= The evening's writing =================

        /// <summary>The gods' hour: in the evening, with new entries in the book, the keeper goes to a writing table and writes them up.</summary>
        internal static void Hourly(GodsAmongBook b, int now)
        {
            var lore = GodLore.Instance;
            if (lore == null || b.writer != null || b.looker != null) return;
            var keeper = GodLore.Keeper(out _);
            if (keeper == null || !keeper.Spawned || !Homes.IsHome(keeper.Map)) return;
            int fresh = lore.entries.Count(e => e != null && e.tick > b.keeperWrote);
            if (!GodsAmongMath.Writes(GenLocalDate.HourOfDay(keeper.Map), fresh) || !Free(keeper)) return;
            // Not every evening: the new entries wait a day or two and are written up together.
            if (!Interplay.Ready("keeper-writes|" + keeper.thingIDNumber, GodsAmongMath.WriteGapDays)) return;
            Write(b, keeper, fresh);
        }

        private static bool Write(GodsAmongBook b, Pawn p, int fresh)
        {
            var job = WriteJob;
            var table = Table(p, out var cell);
            if (job == null || table == null || !cell.IsValid) return false;
            var j = JobMaker.MakeJob(job, table, cell);
            j.count = GodsAmongMath.WriteTicks(fresh);
            j.locomotionUrgency = LocomotionUrgency.Walk;
            if (!OwnHappenings.Job(p, j)) return false;
            b.writer = p;
            b.writeUntil = GodsAmongBook.Now + GenDate.TicksPerHour * 3;
            return true;
        }

        /// <summary>
        /// Where the keeper writes: Vanilla Books Expanded's writer's or typewriter's table, else a table to eat at, else a
        /// lectern, the nearest they can reach; and the cell they sit or stand at to write (its seat, else beside it).
        /// </summary>
        public static Thing Table(Pawn p, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var map = p.Map;
            var all = map.listerBuildings.allBuildingsColonist;
            IEnumerable<Building> Ranked(Func<Building, bool> fit) => all.Where(x => x != null && x.Spawned && fit(x) && !x.IsForbidden(p) && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(p.Position));
            var t = Ranked(x => WritingTables.Contains(x.def.defName)).FirstOrDefault()
                    ?? Ranked(x => x.def.surfaceType == SurfaceType.Eat).FirstOrDefault()
                    ?? Ranked(x => x.def.defName.IndexOf("Lectern", StringComparison.OrdinalIgnoreCase) >= 0).FirstOrDefault();
            if (t == null) return null;
            if (t.def.hasInteractionCell && t.InteractionCell.Standable(map) && p.CanReach(t.InteractionCell, PathEndMode.OnCell, Danger.Some))
            {
                cell = t.InteractionCell;
                return t;
            }
            // A seat beside it first, else any standable cell beside it.
            var beside = GenAdj.CellsAdjacentCardinal(t).Where(c => c.InBounds(map) && c.Standable(map) && p.CanReach(c, PathEndMode.OnCell, Danger.Some)).ToList();
            cell = beside.FirstOrDefault(c => c.GetEdifice(map)?.def.building?.isSittable == true);
            if (!cell.IsValid) cell = beside.FirstOrDefault();
            if (!cell.IsValid) cell = IntVec3.Invalid;
            return cell.IsValid ? t : null;
        }

        /// <summary>They sit down to it (JobDriver_KeeperWrites).</summary>
        internal static void SatDown(Pawn p, Thing table)
        {
            if (Interplay.Ready("keeper-write-word|" + p.thingIDNumber, 2f))
            {
                // apart: the keeper's word as they open the book, said to nobody in particular.
                string line = Worship.Line(Pool, p, null, null, "write");
                if (!line.NullOrEmpty()) Speech.Say(p, line);
            }
        }

        /// <summary>The entries are written up: the newest is marked, and their journal has the evening.</summary>
        internal static void Wrote(Pawn p, Thing table)
        {
            var b = GodsAmongBook.Book;
            var lore = GodLore.Instance;
            if (b == null || lore == null) return;
            int fresh = lore.entries.Count(e => e != null && e.tick > b.keeperWrote);
            if (lore.entries.Count > 0) b.keeperWrote = lore.entries.Max(e => e?.tick ?? -1);
            b.writer = null;
            if (fresh > 0)
                CouncilGameComponent.Instance?.journals.WriteText(p, "CC_GA_Keeper_Journal".Translate(p.Named("PAWN"), fresh.Named("COUNT"), (table?.LabelNoCount ?? "").Named("TABLE")).Resolve());
        }

        // ================= Dev =================

        public static string DevLook(Map map)
        {
            var b = GodsAmongBook.Book;
            var lore = GodLore.Instance;
            var keeper = GodLore.Keeper(out _);
            if (b == null || lore == null) return "the gods are off.";
            if (keeper == null || !keeper.Spawned || keeper.Map != map) return "the book has no keeper on this map.";
            var e = lore.entries.LastOrDefault(x => x != null && x.kind == LoreKind.Saw);
            if (e == null) return "the book has nothing seen to look at.";
            b.ClearLook();
            e.who?.Remove(keeper.LabelShort);
            var cell = CellFinder.RandomClosewalkCellNear(keeper.Position, map, 25);
            Told(e, map, cell);
            return b.looker != null ? keeper.LabelShort + " goes to look at where it was: " + e.text : "the keeper can't go to look now.";
        }

        public static string DevWrite(Map map)
        {
            var b = GodsAmongBook.Book;
            var keeper = GodLore.Keeper(out _);
            if (b == null) return "the gods are off.";
            if (keeper == null || !keeper.Spawned || keeper.Map != map) return "the book has no keeper on this map.";
            b.writer = null;
            b.keeperWrote = -1;
            return Write(b, keeper, 3) ? keeper.LabelShort + " goes to write up the book." : "no table to write at, or the keeper is busy.";
        }
    }

    /// <summary>The book's keeper sits at a writing table (target A, at cell B) and writes up the book's new entries (KeeperAtWork).</summary>
    public class JobDriver_KeeperWrites : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || MapFacts.ThreatOn(pawn.Map));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var write = ToilMaker.MakeToil("KeeperWrites");
            write.initAction = () => KeeperAtWork.SatDown(pawn, job.targetA.Thing);
            write.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
            write.defaultCompleteMode = ToilCompleteMode.Delay;
            write.defaultDuration = Mathf.Max(300, job.count);
            write.handlingFacing = true;
            write.socialMode = RandomSocialMode.Off;
            yield return write;
            var done = ToilMaker.MakeToil("KeeperWrote");
            done.initAction = () => KeeperAtWork.Wrote(pawn, job.targetA.Thing);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
