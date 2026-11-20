using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>One of another people the colony took in hurt: who, whose, since when, why (TakenInMath.Runaway or Wounded), and from where.</summary>
    public class TakenInPatient : IExposable
    {
        public Pawn pawn;
        public Faction people;
        public int since = -1;
        public string why;
        /// <summary>Where they came from, in words: the raiders' camp's kind, or the town whose fight they were hurt in.</summary>
        public string from;
        public int carriedAt = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref carriedAt, "carriedAt", -1);
        }
    }

    /// <summary>A defence partner's (or liege's, or creditor's) wounded on their way to the colony's beds after their fight (B27).</summary>
    public class WoundedComing : IExposable
    {
        public Faction people;
        public int home = HomesMath.None;
        public int due = -1;
        public int count;
        public string town;

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref town, "town");
        }
    }

    /// <summary>What <see cref="TakenIn"/> keeps with the game: those in the colony's beds, and the wounded on their way. A save from before has neither.</summary>
    public class TakenInBook : GameComponent
    {
        public List<TakenInPatient> patients = new List<TakenInPatient>();
        public List<WoundedComing> coming = new List<WoundedComing>();

        private static TakenInBook cached;
        private static Game cachedGame;

        public TakenInBook(Game game)
        {
        }

        public static TakenInBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<TakenInBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) patients?.RemoveAll(t => t?.pawn == null || t.pawn.Discarded);
            Scribe_Collections.Look(ref patients, "patients", LookMode.Deep);
            Scribe_Collections.Look(ref coming, "coming", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                patients ??= new List<TakenInPatient>();
                patients.RemoveAll(t => t?.pawn == null);
                coming ??= new List<WoundedComing>();
                coming.RemoveAll(w => w?.people == null);
            }
        }
    }

    /// <summary>
    /// The hurt of other peoples the colony takes in (the rules are TakenInMath's): a captive who ran from a raiders' camp
    /// (B13, World/Region/RaidersCampSigns.cs) and a call to arms answered with the colony's beds (B27, Diplomacy/CallsFromHome.cs).
    /// They are the colony's guests, sent to its beds and tended by its doctors as the game tends a guest; anyone who can't
    /// stand is carried in by one of the colony's people. Well again (or after twenty days on their feet), they walk home,
    /// and the game's own count of a guest tended remembers the doctor by name (World/KinMemory.cs). The region's hour
    /// (World/Region/RegionBook.cs) looks after them while any are here or on their way; nothing per tick.
    /// </summary>
    public static class TakenIn
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool Any
        {
            get
            {
                var b = TakenInBook.Instance;
                return b != null && (b.patients.Count > 0 || b.coming.Count > 0);
            }
        }

        /// <summary>Someone of <paramref name="people"/> taken in: the colony's guest, sent to a bed (or carried to one).</summary>
        public static void Add(Pawn x, Faction people, Map map, string why, string from)
        {
            var b = TakenInBook.Instance;
            if (b == null || x == null || x.Dead || !x.Spawned || map == null || b.patients.Any(t => t.pawn == x)) return;
            try
            {
                Lords.Leave(x);
                if (x.guest != null && x.HostFaction != Faction.OfPlayer) x.guest.SetGuestStatus(Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("taken in: made a guest", e);
            }
            b.patients.Add(new TakenInPatient { pawn = x, people = people ?? x.Faction, since = Now, why = why, from = from });
            while (b.patients.Count > TakenInMath.MaxPatients)
            {
                var first = b.patients[0];
                b.patients.RemoveAt(0);
                GoHome(first, false);
            }
            ToBed(b.patients.Last(), Now);
        }

        /// <summary>A partner's wounded will come to the colony's beds in a day or so (B27).</summary>
        public static void Expect(Faction people, Map home, int count, string town)
        {
            var b = TakenInBook.Instance;
            if (b == null || people == null || home == null) return;
            b.coming.Add(new WoundedComing { people = people, home = Homes.KeyOf(home), due = Now + TakenInMath.ArriveTicks(Rand.Value), count = count, town = town });
            while (b.coming.Count > TakenInMath.MaxComing) b.coming.RemoveAt(0);
        }

        /// <summary>The region's hour: wounded arriving, and those in the colony's beds seen to (to a bed, carried in, home when well).</summary>
        public static void Hourly(int now)
        {
            var b = TakenInBook.Instance;
            if (b == null) return;
            foreach (var w in b.coming.ToList())
            {
                if (w.due > now) continue;
                b.coming.Remove(w);
                try
                {
                    Arrive(w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("taken in: the wounded arrive", e);
                }
            }
            foreach (var t in b.patients.ToList())
            {
                try
                {
                    if (Hour(t, now)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("taken in: the hour", e);
                }
                b.patients.Remove(t);
            }
        }

        /// <summary>One of them, this hour: false once they are no longer the colony's to look after.</summary>
        private static bool Hour(TakenInPatient t, int now)
        {
            var p = t.pawn;
            if (p == null || p.Destroyed && !p.Dead) return false;
            if (p.Dead)
            {
                // Shown: the game's own death, and a line saying whose and where.
                var at = p.Corpse?.MapHeld;
                if (at != null)
                    Tell.Message(NewsKind.Health, Importance.Notable, at, "CC_TakenIn_Died".Translate(p.Named("PAWN"), (t.people?.Name ?? "?").Named("PEOPLE")).Resolve(),
                        new LookTargets(p.Corpse), MessageTypeDefOf.NegativeHealthEvent);
                return false;
            }
            // Gone from the map, or made the colony's own (recruited, taken prisoner): no longer a guest it looks after.
            if (!p.Spawned || p.Faction == Faction.OfPlayer || p.IsPrisoner || p.HostFaction != Faction.OfPlayer) return false;
            bool well = TakenInMath.Well(p.health.HasHediffsNeedingTend(), HealthAIUtility.ShouldSeekMedicalRest(p), p.Downed);
            if (TakenInMath.GoesHome(well, p.Downed, t.since, now))
            {
                GoHome(t, well);
                return false;
            }
            ToBed(t, now);
            return true;
        }

        /// <summary>To a bed: walking, or, if they can't stand, carried there by one of the colony's (once in a few hours).</summary>
        private static void ToBed(TakenInPatient t, int now)
        {
            var p = t?.pawn;
            if (p == null || !p.Spawned || p.InBed() || p.CurJobDef == JobDefOf.LayDown) return;
            var map = p.Map;
            var bed = RestUtility.FindPatientBedFor(p);
            if (bed == null) return;
            try
            {
                if (!p.Downed)
                {
                    if (p.InMentalState || !RestUtility.CanUseBedNow(bed, p, false, true)) return;
                    p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
                    return;
                }
                if (t.carriedAt >= 0 && now - t.carriedAt < 3 * TakenInMath.HourTicks) return;
                if (map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == JobDefOf.Rescue && c.CurJob?.targetA.Thing == p)) return;
                var carrier = map.mapPawns.FreeColonistsSpawned
                    .Where(c => c != null && !c.Downed && !c.Drafted && !c.InMentalState && !c.DevelopmentalStage.Juvenile() && !c.WorkTagIsDisabled(WorkTags.Caring)
                                && c.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some) && c.CanReserve(bed))
                    .OrderBy(c => c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (carrier == null) return;
                var job = JobMaker.MakeJob(JobDefOf.Rescue, p, bed);
                job.count = 1;
                carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, true, true);
                t.carriedAt = now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("taken in: to a bed", e);
            }
        }

        /// <summary>Home, on foot: no longer the colony's guest; the message says so.</summary>
        private static void GoHome(TakenInPatient t, bool well)
        {
            var p = t?.pawn;
            if (p == null || p.Dead || !p.Spawned) return;
            try
            {
                if (p.HostFaction == Faction.OfPlayer && !p.IsPrisoner) p.guest?.SetGuestStatus(null);
                if (p.Downed || p.Faction == Faction.OfPlayer) return;
                Arrivals.SendAway(new[] { p });
                Tell.Message(NewsKind.World, Importance.Notable, p.Map,
                    (well ? "CC_TakenIn_Home" : "CC_TakenIn_HomeHurt").Translate(p.Named("PAWN"), (t.people?.Name ?? p.Faction?.Name ?? "?").Named("PEOPLE")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("taken in: home", e);
            }
        }

        /// <summary>
        /// Their wounded come in from the edge, hurt from their fight, and make for the colony's beds; a letter says who and
        /// from where. Nothing comes if their people are gone or at war with the colony now.
        /// </summary>
        private static void Arrive(WoundedComing w)
        {
            var map = Homes.MapOf(w.home);
            var f = w.people;
            if (map == null || f == null || f.defeated || f.HostileTo(Faction.OfPlayer) || !Arrivals.TryEntryCell(map, out var entry)) return;
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return;
            var came = new List<Pawn>();
            for (int i = 0; i < Math.Max(1, w.count); i++)
            {
                var p = Arrivals.Make(kind, f, new FloatRange(18f, 50f));
                if (p == null || !Arrivals.Spawn(p, map, entry)) continue;
                Hurt(p, Rand.RangeInclusive(TakenInMath.WoundedInjuriesMin, TakenInMath.WoundedInjuriesMax), true);
                if (p.Dead) continue;
                came.Add(p);
                Add(p, f, map, TakenInMath.Wounded, w.town);
            }
            if (came.Count == 0) return;
            string town = w.town.NullOrEmpty() ? f.Name : w.town;
            Tell.Letter(NewsKind.World, Importance.Notable, map, TextVariants.Vary("CC_Arms_Wounded_Label").Translate(town.Named("TOWN")).Resolve(),
                TextVariants.Vary("CC_Arms_Wounded_Text").Translate(came.Count.Named("COUNT"), f.Name.Named("PEOPLE"), town.Named("TOWN")).Resolve(),
                LetterDefOf.NeutralEvent, new LookTargets(came), f);
        }

        /// <summary>
        /// Hurt from before they came (shown on their health): <paramref name="n"/> injuries on outer parts that nobody dies of,
        /// a fight's (gunshots from the industrial age on, cuts and stabs before it, and bruises) or a runaway's (bruises, cuts
        /// and scratches). Each no worse than TakenInMath allows of the part's health, so nothing is lost.
        /// </summary>
        public static void Hurt(Pawn p, int n, bool fight)
        {
            if (p?.health?.hediffSet == null || p.Dead) return;
            try
            {
                var parts = p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside)
                    .Where(bp => bp != null && bp.coverageAbs > 0f && (bp.def.tags == null || !bp.def.tags.Any(tag => tag != null && tag.vital))).ToList();
                if (parts.Count == 0) return;
                var kinds = fight
                    ? Peoples.TechOf(p.Faction, TechLevel.Medieval) >= TechLevel.Industrial
                        ? new[] { HediffDefOf.Gunshot, HediffDefOf.Cut, HediffDefOf.Bruise }
                        : new[] { HediffDefOf.Cut, HediffDefOf.Stab, HediffDefOf.Bruise }
                    : new[] { HediffDefOf.Bruise, HediffDefOf.Cut, HediffDefOf.Scratch };
                for (int i = 0; i < n; i++)
                {
                    var part = parts.RandomElement();
                    var def = kinds.RandomElement();
                    if (def == null) continue;
                    float sev = TakenInMath.Severity(Rand.Value, p.health.hediffSet.GetPartHealth(part));
                    if (sev < 1f) continue;
                    var h = HediffMaker.MakeHediff(def, p, part);
                    h.Severity = sev;
                    p.health.AddHediff(h, part);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("taken in: hurt from before", e);
            }
        }

        /// <summary>Dev mode: who is in the colony's beds and who is on the way.</summary>
        public static string Describe()
        {
            var b = TakenInBook.Instance;
            if (b == null) return "no book.";
            var lines = b.patients.Select(t => (t.pawn?.LabelShort ?? "?") + " of " + (t.people?.Name ?? "?") + " (" + t.why + ", " + ((Now - t.since) / (float)TakenInMath.DayTicks).ToString("0.0") + " days)")
                .Concat(b.coming.Select(w => w.count + " wounded of " + (w.people?.Name ?? "?") + " due in " + ((w.due - Now) / (float)TakenInMath.HourTicks).ToString("0") + " hours")).ToList();
            return lines.Count == 0 ? "nobody taken in, nobody on the way." : string.Join("; ", lines);
        }

        /// <summary>Dev mode: the wounded on their way come now.</summary>
        public static int DevArriveNow()
        {
            var b = TakenInBook.Instance;
            if (b == null) return 0;
            foreach (var w in b.coming) w.due = Now;
            int n = b.coming.Count;
            Hourly(Now);
            return n;
        }
    }
}
