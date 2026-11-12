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
    /// What remains of a god's coming (brainstorm 26; docs/design/gods.md, "Weight"): its name and act in the colony's
    /// history (Gods.Remember, the god's page), in the chronicle and the ledger; a memory those who saw it share and
    /// bring up when they talk (SharedMemories, "saw a god come"); a deed the others talk of (Deeds); and the mark on the
    /// land, remembered for years: someone walks out to it on a quiet day and remembers aloud, it comes up at supper
    /// between two at the table, and a visitor who passes it asks about it. <see cref="Spoken"/> gives the newest mark for
    /// letters and visitors' news.
    /// </summary>
    public static class GodMarks
    {
        public const string VisitJob = "Rimfolk_VisitGodMark";
        public const string MemoryDef = "Rimfolk_Mem_GodSeen";
        public const string VisitPool = "Rimfolk_Pool_GodMarkVisit";
        public const string VisitorPool = "Rimfolk_Pool_GodMarkVisitor";
        public const string SupperPool = "Rimfolk_Pool_GodSupper";

        private static int Now => Find.TickManager.TicksGame;

        // ================= Remembering =================

        /// <summary>A mark the god left at <paramref name="cell"/> of <paramref name="map"/>, with what it did there in a few words.</summary>
        public static void Note(God god, Map map, IntVec3 cell, string words, PresenceKind kind, bool plain = false)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || god == null || map == null || !cell.IsValid || !Homes.IsHome(map)) return;
            b.marks.Add(new GodMarkNote { god = god.id, home = Homes.KeyOf(map), cell = cell, tick = Now, words = words, kind = kind, plain = plain });
            while (b.marks.Count > GodsOnMapMath.MaxMarks) b.marks.RemoveAt(0);
            Gods.Remember(new GodAct
            {
                god = god.id, home = Homes.KeyOf(map), tick = Now, kind = GodActKind.Mark, cell = cell,
                text = "CC_GodM_MarkAct".Translate(Homes.Name(map).Named("HOME")).Resolve()
            });
        }

        /// <summary>
        /// Those of the colony who saw it (within sight of the place, or named in <paramref name="also"/>) share a memory of
        /// it, pair by pair (a few pairs), to bring up when they talk.
        /// </summary>
        public static void Witnessed(Map map, IntVec3 at, string label, IEnumerable<Pawn> also)
        {
            var def = DefDatabase<SharedMemoryDef>.GetNamedSilentFail(MemoryDef);
            if (def == null || map == null || !Persona.On) return;
            try
            {
                var saw = map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && p.Awake() && p.Position.InHorDistOf(at, 30f) && GenSight.LineOfSight(p.Position, at, map, true))
                    .Concat(also ?? Enumerable.Empty<Pawn>()).Where(p => p != null && !p.Dead && p.IsColonist).Distinct().Take(5).ToList();
                for (int i = 0; i < saw.Count; i++)
                for (int j = i + 1; j < saw.Count; j++)
                    // together: they saw it together, at the one place, at the one moment.
                    SharedMemories.Add(saw[i], saw[j], def, label, 1.3f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a coming remembered together", e);
            }
        }

        /// <summary>The newest mark still spoken of at a home: the god and what it did there, for a letter or a visitor's news. Null with none.</summary>
        public static GodMarkNote Spoken(Map home)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || home == null) return null;
            int key = Homes.KeyOf(home);
            return b.marks.Where(m => m.home == key && GodsOnMapMath.Remembered((Now - m.tick) / GenDate.TicksPerDay) && Gods.ById(m.god) != null).OrderByDescending(m => m.tick)
                .FirstOrDefault();
        }

        // ================= The day: a quiet day's visit =================

        internal static void Daily(GodsOnMapBook b, int now)
        {
            b.marks.RemoveAll(m => m == null || Gods.ById(m.god) == null || Homes.MapOf(m.home) == null || !GodsOnMapMath.Remembered((now - m.tick) / GenDate.TicksPerDay));
            if (now - b.lastVisit < GodsOnMapMath.VisitGapDays * GenDate.TicksPerDay || !PlayOuts.On) return;
            if (!Rand.Chance(GodsOnMapMath.VisitChance * GodPaceMath.Ramp(GodPace.DaysSince(b.lastVisit, now), GodsOnMapMath.VisitGapDays, GodsOnMapMath.VisitRampDays))) return;
            foreach (var map in Homes.All.ToList())
            {
                // A quiet day only: no fight on the map and nothing big lately.
                if (map == null || MapFacts.ThreatOn(map) || now - (map.storyState?.LastThreatBigTick ?? 0) < GenDate.TicksPerDay * 2) continue;
                var here = b.marks.Where(m => m.home == Homes.KeyOf(map) && m.cell.InBounds(map)).ToList();
                int i = GodsOnMapMath.VisitPick(here.Select(m => (now - m.tick) / GenDate.TicksPerDay).ToList(), here.Select(m => m.visits).ToList(),
                    here.Select(m => m.lastVisit < 0 ? 9999 : (now - m.lastVisit) / GenDate.TicksPerDay).ToList());
                if (i < 0) continue;
                var mark = here[i];
                var who = map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && Together.FreeTime(p) && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()
                                                                   && p.CanReach(mark.cell, PathEndMode.Touch, Danger.None)).RandomElementWithFallback();
                if (who == null || !Send(who, mark)) continue;
                b.lastVisit = now;
                return;
            }
        }

        /// <summary><paramref name="p"/> walks out to the mark, stands there a while and remembers aloud (JobDriver_VisitGodMark). False when they can't now.</summary>
        public static bool Send(Pawn p, GodMarkNote mark)
        {
            var def = DefDatabase<JobDef>.GetNamedSilentFail(VisitJob);
            if (def == null || p == null || mark == null || !p.Spawned) return false;
            try
            {
                var job = JobMaker.MakeJob(def, mark.cell);
                job.count = Rand.Range(600, 1100);
                job.expiryInterval = GenDate.TicksPerHour * 3;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return p.CurJobDef == def || p.jobs.jobQueue.Any(q => q.job.def == def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a walk to a god's mark", e);
                return false;
            }
        }

        /// <summary>The mark someone stands at: the newest one near the cell.</summary>
        internal static GodMarkNote At(Map map, IntVec3 cell)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || map == null) return null;
            int key = Homes.KeyOf(map);
            return b.marks.Where(m => m.home == key && m.cell.InHorDistOf(cell, 4f)).OrderByDescending(m => m.tick).FirstOrDefault();
        }

        /// <summary>Said aloud at the mark: who it was and what it did there, as plainly as it showed itself there.</summary>
        internal static void Remember(Pawn p, GodMarkNote mark)
        {
            var god = Gods.ById(mark?.god ?? -1);
            if (p == null || god == null) return;
            mark.visits++;
            mark.lastVisit = Now;
            string line = Flavor.FromPool(VisitPool, p, null, null, mark.kind.ToString().ToLowerInvariant(), god.domain.ToString().ToLowerInvariant(), Voice(mark), GodShapes.LineTag(god));
            string said = line.NullOrEmpty() ? null : line.Formatted(GodsOnMapWords.Args(god, false).Concat(new[] { (mark.words ?? "").Named("WHAT"), p.Named("PAWN") }).ToArray()).Resolve();
            if (said != null) Speech.Say(p, said);
            // A small good moment that gives a quiet hour, not a mood.
            var joy = p.needs?.joy;
            if (joy != null) joy.CurLevel = Mathf.Min(1f, joy.CurLevel + 0.12f);
            CouncilGameComponent.Instance?.journals?.WriteText(p,
                "CC_GodM_Journal_Mark".Translate(GodsOnMapWords.Args(god, false).Concat(new[] { (mark.words ?? "").Named("WHAT") }).ToArray()).Resolve());
        }

        /// <summary>The tag of how the god showed itself at a mark, for the lines: near (plainly), else veiled (felt).</summary>
        private static string Voice(GodMarkNote mark) => mark != null && mark.plain ? "near" : "veiled";

        // ================= The hour: spoken of =================

        private static int lastVisitorRemark = -999999, lastSupper = -999999;
        private static Game game;

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            if (Current.Game != game)
            {
                game = Current.Game;
                lastVisitorRemark = -999999;
                // Not kept in the save: counted as half its gap ago, so loading a game doesn't bring it up at the next supper.
                lastSupper = now - GodsOnMapMath.SupperMarkGapDays * GenDate.TicksPerDay / 2;
            }
            if (b.marks.Count == 0) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || map != Find.CurrentMap) continue;
                int key = Homes.KeyOf(map);
                var here = b.marks.Where(m => m.home == key && m.cell.InBounds(map) && GodsOnMapMath.Remembered((now - m.tick) / GenDate.TicksPerDay)).ToList();
                if (here.Count == 0) continue;
                Visitors(map, here, now);
                Supper(map, here, now);
            }
        }

        /// <summary>A visitor passing near a mark asks about it, or says what they heard (once a day at most).</summary>
        private static void Visitors(Map map, List<GodMarkNote> here, int now)
        {
            if (now - lastVisitorRemark < GodsOnMapMath.VisitorRemarkGapTicks || !Rand.Chance(0.4f)) return;
            foreach (var x in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (x == null || !x.RaceProps.Humanlike || x.Faction == null || x.Faction.IsPlayer || x.HostileTo(Faction.OfPlayer) || x.Downed || !x.Awake() || GodAvatar.Is(x)) continue;
                if (Strangers.KindOf(x) != StrangerKind.Visitor && Strangers.KindOf(x) != StrangerKind.Trader) continue;
                var mark = here.FirstOrDefault(m => x.Position.InHorDistOf(m.cell, 8f) && GenSight.LineOfSight(x.Position, m.cell, map, true));
                var god = mark == null ? null : Gods.ById(mark.god);
                if (god == null) continue;
                string line = Flavor.FromPool(VisitorPool, x, null, null, mark.kind.ToString().ToLowerInvariant(), god.domain.ToString().ToLowerInvariant(), Voice(mark));
                if (line.NullOrEmpty()) continue;
                string said = line.Formatted(GodsOnMapWords.Args(god, false).Concat(new[] { (mark.words ?? "").Named("WHAT") }).ToArray()).Resolve();
                if (Speech.Say(x, said))
                {
                    lastVisitorRemark = now;
                    return;
                }
            }
        }

        /// <summary>
        /// At supper, someone at the table brings it up to whoever eats beside them: never within days of the last time, then
        /// at a small chance an evening hour that grows the longer since (GodsOnMapMath.SupperMarkChance).
        /// </summary>
        private static void Supper(Map map, List<GodMarkNote> here, int now)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 17 || hour > 21 || now - lastSupper < GodsOnMapMath.SupperMarkGapDays * GenDate.TicksPerDay) return;
            if (!Rand.Chance(GodsOnMapMath.SupperMarkChance * GodPaceMath.Ramp(GodPace.DaysSince(lastSupper, now), GodsOnMapMath.SupperMarkGapDays, GodsOnMapMath.SupperMarkRampDays))) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.CurJobDef != JobDefOf.Ingest || !People.HasInnerLife(p)) continue;
                var other = Together.Company(p);
                if (other == null || other.CurJobDef != JobDefOf.Ingest) continue;
                var mark = here.RandomElement();
                var god = Gods.ById(mark.god);
                if (god == null) continue;
                string line = Flavor.FromPool(SupperPool, p, null, null, mark.kind.ToString().ToLowerInvariant(), god.domain.ToString().ToLowerInvariant(), Voice(mark), GodShapes.LineTag(god));
                if (line.NullOrEmpty()) continue;
                if (Speech.Say(p, line.Formatted(GodsOnMapWords.Args(god, false).Concat(new[] { (mark.words ?? "").Named("WHAT"), other.Named("OTHER") }).ToArray()).Resolve(), other))
                {
                    lastSupper = now;
                    return;
                }
            }
        }
    }

    /// <summary>Walking out to a god's mark on a quiet day, standing there a while facing it, and remembering aloud.</summary>
    public class JobDriver_VisitGodMark : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_GodM_Job_Visit".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed || pawn.Drafted || pawn.InMentalState);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var stay = ToilMaker.MakeToil("VisitGodMark");
            stay.initAction = () =>
            {
                pawn.pather?.StopDead();
                try
                {
                    GodMarks.Remember(pawn, GodMarks.At(pawn.Map, job.targetA.Cell));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: remembering at a god's mark", e);
                }
            };
            stay.tickIntervalAction = delta => pawn.rotationTracker.FaceCell(job.targetA.Cell);
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Math.Max(300, job.count);
            stay.handlingFacing = true;
            yield return stay;
        }
    }
}
