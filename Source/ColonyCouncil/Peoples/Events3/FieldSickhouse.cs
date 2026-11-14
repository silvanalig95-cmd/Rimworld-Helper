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
    // ===================== 8. A field sickhouse (the hospitaller order): their camp =====================

    public class SickhouseState : CampWorkState
    {
        /// <summary>The strangers lying in the ward now, and the beds the sisters made up for them.</summary>
        public List<Pawn> patients = new List<Pawn>();
        public List<Thing> beds = new List<Thing>();
        /// <summary>The colony's own sick taken into the ward.</summary>
        public List<Pawn> ours = new List<Pawn>();
        /// <summary>The peoples whose sick the colony fed (by their load ids), for the goodwill when the sisters go.</summary>
        public List<string> fed = new List<string>();
        public int coming = -1, seen, died, nextPatient = -1, lastAsk = -1, roundHour = -1;
        public bool medicine, food, cloth, ward, takeOurs, crossed, bandRolled, wardTold;
        public List<string> asked = new List<string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref patients, "patients", LookMode.Reference);
            Scribe_Collections.Look(ref beds, "beds", LookMode.Reference);
            Scribe_Collections.Look(ref ours, "ours", LookMode.Reference);
            Scribe_Collections.Look(ref fed, "fed", LookMode.Value);
            Scribe_Collections.Look(ref asked, "asked", LookMode.Value);
            Scribe_Values.Look(ref coming, "coming", -1);
            Scribe_Values.Look(ref seen, "seen");
            Scribe_Values.Look(ref died, "died");
            Scribe_Values.Look(ref nextPatient, "nextPatient", -1);
            Scribe_Values.Look(ref lastAsk, "lastAsk", -1);
            Scribe_Values.Look(ref roundHour, "roundHour", -1);
            Scribe_Values.Look(ref medicine, "medicine");
            Scribe_Values.Look(ref food, "food");
            Scribe_Values.Look(ref cloth, "cloth");
            Scribe_Values.Look(ref ward, "ward");
            Scribe_Values.Look(ref takeOurs, "takeOurs");
            Scribe_Values.Look(ref crossed, "crossed");
            Scribe_Values.Look(ref bandRolled, "bandRolled");
            Scribe_Values.Look(ref wardTold, "wardTold");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                patients ??= new List<Pawn>();
                patients.RemoveAll(p => p == null);
                beds ??= new List<Thing>();
                beds.RemoveAll(t => t == null);
                ours ??= new List<Pawn>();
                ours.RemoveAll(p => p == null);
                fed ??= new List<string>();
                asked ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// A field sickhouse (item 8): in a plague year (the region's plague running), or after a war in the region, sisters of
    /// the order pitch a long hut by the colony with a lamp lit at its door all night (Rimfolk_Camp_FieldSickhouse). Over the
    /// days, two to six sick or wounded strangers of the region's peoples walk in, or are carried in by one of their own, and
    /// the sisters dose and tend them on rounds. They ask the colony for medicine, food and clean cloth, and for its doctor to
    /// work the ward for a day (much practice, and the sickness may come home with them: the physician's chance, through the
    /// harm budget and the illness budget); the colony's own sick may be taken in too. It can turn: the illness crosses to
    /// the colony (the fever at their fire, through the budgets), a patient dies and the Bell comes for them (or the sisters
    /// carry them home), or a band comes for the medicine. When they go, the hut stays standing, the colony's name is in the
    /// Book of the Spared, and the order and each people whose sick the colony fed think better of it.
    /// </summary>
    public static class FieldSickhouse
    {
        public const string WardKind = "pev:ward";
        public const string BellKind = "pev:bell";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_FieldSickhouse");

        public static JobDef RoundJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_WardRound");

        public static JobDef CarryInJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_CarryToWard");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>What brings them: the region's plague running (its name), else a war in the region ended in the last month; null for neither.</summary>
        public static string Why(out bool plague)
        {
            plague = false;
            var ws = WorldSituations.Instance;
            if (ws == null) return null;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.def.kind != "sickness" || s.def.defName == "Council_WS_Murrain") continue;
                plague = true;
                return s.def.label;
            }
            int now = Now;
            var war = ws.history?.Where(r => r != null && r.endTick > 0 && now - r.endTick < 30 * GenDate.TicksPerDay && DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "")?.kind == "war")
                .OrderByDescending(r => r.endTick).FirstOrDefault();
            return war?.Called;
        }

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static SickhouseState StateOf(Camp c) => c?.workState as SickhouseState;

        /// <summary>The camp made and set going by the colony (DryWater's way), its people walking in from the edge.</summary>
        public static Camp Start(HappeningContext ctx, bool takeOurs)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 8);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new SickhouseState { takeOurs = takeOurs, coming = PeopleEvents3Math.Patients(Rand.Value), nextPatient = Now + GenDate.TicksPerDay / 2 };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the field sickhouse", e);
                return null;
            }
        }

        /// <summary>A body carried home (JobDriver_LitterCarry at the edge): laid on the cart and gone.</summary>
        public static void BodyAway(Pawn bearer)
        {
            if (!(bearer?.carryTracker?.CarriedThing is Corpse body)) return;
            string name = body.InnerPawn?.LabelShort ?? "?";
            if (bearer.carryTracker.TryDropCarriedThing(bearer.Position, ThingPlaceMode.Near, out var dropped) && dropped != null && !dropped.Destroyed) dropped.Destroy();
            PeoplesG.SayLine(bearer, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_FieldSickhouse"), "home");
            News.Post("CC_PEv3_BodyHome".Translate(bearer.Named("BEARER"), name.Named("DEAD")).Resolve(), LookTargets.Invalid, NewsKind.World);
            Arrivals.SendAway(new[] { bearer });
        }

        /// <summary>The colony's doctor works the ward a day: rounds beside the sisters, much practice, the sickness's chance of coming home.</summary>
        public static Visit Ward(Camp c, Pawn doctor)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (t == null || c == null || doctor == null || !doctor.Spawned) return null;
            var v = t.AddVisit(WardKind, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_FieldSickhouse"), c.map, doctor, null, null, Now + 9 * GenDate.TicksPerHour);
            v.text = c.faction?.GetUniqueLoadID() ?? "";
            v.stake = c.id;
            return v;
        }

        /// <summary>The doctor's hour in the ward: the next patient seen to; at the day's end, home with the practice, and maybe the sickness.</summary>
        public static bool WardHour(Visit v, int now)
        {
            if (v.done) return false;
            var p = v.x;
            var c = CampTracker.Instance?.ById(v.stake);
            var st = StateOf(c);
            if (p == null || p.Dead || st == null || !c.Settled)
            {
                v.done = true;
                return false;
            }
            if (now > v.until)
            {
                v.done = true;
                WardDone(c, st, p);
                return false;
            }
            var job = RoundJob;
            if (job == null || !p.Spawned || p.Downed || p.Drafted || p.InMentalState || p.CurJobDef == job) return true;
            var next = st.patients.Where(x => x != null && x.Spawned && !x.Dead && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(x => x.health?.hediffSet?.HasTendableHediff() ?? false).ThenBy(x => Rand.Value).FirstOrDefault();
            if (next == null) return true;
            var j = JobMaker.MakeJob(job, next);
            j.count = 900;
            // together: the doctor walks to the patient's bed in the ward and works at their side.
            p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
            return true;
        }

        private static void WardDone(Camp c, SickhouseState st, Pawn doctor)
        {
            st.ward = true;
            try
            {
                doctor.skills?.Learn(SkillDefOf.Medicine, PeopleEvents3Math.WardXp, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the ward's lesson", e);
            }
            string text = c.Text("CC_PEv3_WardDone", null, doctor);
            // The sickness may come home with the doctor (the physician's chance, through the harm budget and the illness budget).
            var ill = Illness(st);
            if (ill != null && Harm.On && Harm.SystemOn(HarmKinds.Physician.system) && Harm.Roll(doctor, HarmKinds.Physician, PeopleEvents3Math.DoctorCatch)
                && IllnessBudget.Allows(c.map, ill, doctor) && Harm.Ail(doctor, ill, 0.12f, HarmKinds.Physician) != null)
            {
                IllnessBudget.Owe(c.map, ill, doctor);
                text += " " + c.Text("CC_PEv3_WardCaught", null, doctor, 0, ill.label);
            }
            c.Note(text, MessageTypeDefOf.NeutralEvent, Importance.Notable, doctor, true);
        }

        /// <summary>The sickness in the ward: the commonest illness among the patients (none when all are wounded).</summary>
        public static HediffDef Illness(SickhouseState st) =>
            st?.patients.Where(p => p != null && !p.Dead).SelectMany(p => p.health.hediffSet.hediffs).Where(h => h?.def != null && h.def.makesSickThought && IllnessBudget.IsSeriousIllness(h.def))
                .GroupBy(h => h.def).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;

        /// <summary>One of theirs died in the ward: the Bell comes for them when it is at peace, else a sister carries them home.</summary>
        public static void Died(Camp c, Corpse body)
        {
            var bell = PeoplesD.Friendly(PeoplesMath.Sextons);
            var t = CouncilGameComponent.Instance?.happenings;
            if (body == null || !body.Spawned || t == null) return;
            if (bell != null)
            {
                var sexton = Arrivals.Make(PeoplesD.KindNamed(bell, "Rimfolk_QuietBell_Sexton"), bell, new FloatRange(24f, 70f));
                if (sexton != null && Arrivals.Visit(new List<Pawn> { sexton }, c.map, bell, body.Position, 10f) != null)
                {
                    var v = t.AddVisit(BellKind, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_FieldSickhouse"), c.map, sexton, null, null, Now + 12 * GenDate.TicksPerHour);
                    v.place = body;
                    c.Note(c.Text("CC_PEv3_BellComes", null, null, 0, body.InnerPawn?.LabelShort ?? "?"), MessageTypeDefOf.NeutralEvent, Importance.Notable, body, true);
                    return;
                }
                if (sexton != null) Arrivals.Discard(sexton);
            }
            // No Bell to come: a sister carries them home herself.
            var sister = c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && p != c.leader).RandomElementWithFallback() ?? c.leader;
            if (sister != null && CarryOff(sister, body))
                c.Note(c.Text("CC_PEv3_SisterCarriesHome", sister, null, 0, body.InnerPawn?.LabelShort ?? "?"), MessageTypeDefOf.NeutralEvent, Importance.Notable, body, true);
        }

        /// <summary>Someone carries a body (or a thing) to the map's edge (JobDriver_LitterCarry).</summary>
        public static bool CarryOff(Pawn bearer, Thing what)
        {
            var job = Litters.CarryJob;
            if (job == null || bearer == null || what == null || !bearer.Spawned || !bearer.CanReserveAndReach(what, PathEndMode.ClosestTouch, Danger.Some)) return false;
            if (!RCellFinder.TryFindBestExitSpot(bearer, out var edge, TraverseMode.ByPawn)) return false;
            try
            {
                Lords.Leave(bearer);
                var j = JobMaker.MakeJob(job, what, edge);
                j.count = 1;
                bearer.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return bearer.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: carried home", e);
                return false;
            }
        }

        /// <summary>The Bell's sexton at the body: the bell rung once, and the body carried home.</summary>
        public static bool BellHour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            var body = v.place as Corpse;
            if (x == null || x.Dead || !x.Spawned || body == null || body.Destroyed || !body.Spawned || now > v.until)
            {
                v.done = true;
                if (x != null && x.Spawned && !x.Dead) Arrivals.SendAway(new[] { x });
                return false;
            }
            if (x.CurJobDef == Litters.CarryJob) return true;
            if (x.Position.InHorDistOf(body.Position, 6f))
            {
                PeoplesG.SayLine(x, v.incident, "bell");
                if (CarryOff(x, body)) v.done = true;
            }
            return !v.done;
        }

        /// <summary>A stranger of the region, sick or wounded, for the ward: their people (at peace, not the order), their trouble.</summary>
        public static Pawn MakePatient(Camp c, bool plague, out bool carried)
        {
            carried = false;
            var f = Arrivals.Friendly(x => x != c.faction && Arrivals.Usable(x));
            if (f == null) return null;
            var p = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(16f, 70f));
            if (p == null) return null;
            try
            {
                bool sick = plague ? Rand.Chance(0.75f) : Rand.Chance(0.35f);
                if (sick)
                {
                    var def = plague && Rand.Chance(0.6f) ? HediffDefOf.Plague : DefDatabase<HediffDef>.GetNamedSilentFail("Flu") ?? HediffDefOf.Plague;
                    var h = HediffMaker.MakeHediff(def, p);
                    h.Severity = Rand.Range(0.25f, 0.45f);
                    p.health.AddHediff(h);
                }
                else
                {
                    // Hurt in the fighting: wounds of the game's own, untended.
                    for (int i = 0; i < Rand.RangeInclusive(2, 3); i++)
                    {
                        var part = p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside).Where(x => x.def.GetMaxHealth(p) >= 10f).RandomElementWithFallback();
                        if (part == null) break;
                        var hurt = HediffMaker.MakeHediff(Rand.Chance(0.5f) ? HediffDefOf.Cut : DefDatabase<HediffDef>.GetNamedSilentFail("Bruise") ?? HediffDefOf.Cut, p, part);
                        hurt.Severity = Rand.Range(2f, Mathf.Min(7f, part.def.GetMaxHealth(p) * 0.4f));
                        p.health.AddHediff(hurt, part);
                    }
                }
                carried = !sick && Rand.Chance(PeopleEvents3Math.CarriedIn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: a patient for the ward", e);
            }
            return p;
        }

        /// <summary>Dev mode: a patient walks (or is carried) in now.</summary>
        public static bool PatientNow(Map map)
        {
            var st = StateOf(On(map));
            if (st == null) return false;
            st.nextPatient = Now;
            if (st.coming <= 0) st.coming = 1;
            return true;
        }
    }

    /// <summary>The field sickhouse's days: the patients in, their beds and rounds, the asks, the colony's own sick, and what can turn.</summary>
    public class CampWorker_Sickhouse : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public override void Started(Camp c)
        {
            if (!(c.workState is SickhouseState)) c.workState = new SickhouseState { coming = PeopleEvents3Math.Patients(Rand.Value), nextPatient = Now + GenDate.TicksPerDay / 2 };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is SickhouseState st))
            {
                Started(c);
                st = FieldSickhouse.StateOf(c);
            }
            var map = c.map;
            // Once a stay: the medicine draws a band against them, on top of the camps' own.
            if (!st.bandRolled && c.Settled && c.raidRolled)
            {
                st.bandRolled = true;
                if (c.raidAt < 0 && Rand.Chance(PeopleEvents3Math.MedicineBandChance)) c.raidAt = CampRaidMath.AttackTick(c.arrivedTick, c.untilTick, Rand.Value);
            }
            Tidy(c, st);
            // The strangers of the region come in over the days.
            if (st.coming > 0 && Now >= st.nextPatient && hour >= 7 && hour <= 19 && c.DaysHere >= 0.4f)
            {
                st.nextPatient = Now + Rand.RangeInclusive(PeopleEvents3Math.PatientGapMinHours, PeopleEvents3Math.PatientGapMaxHours) * GenDate.TicksPerHour;
                if (Patient(c, st)) st.coming--;
            }
            // The rounds, sister by sister.
            if (PeopleEvents3Math.RoundHour(hour) && st.roundHour != hour) st.roundHour = hour;
            // The asks, one a day at most: medicine first, then food and cloth, then the ward.
            if (Tell.CanAsk && hour >= 8 && hour <= 14 && st.lastAsk != GenDate.DaysPassed && c.DaysHere >= 0.5f) Ask(c, st);
            // The colony's own sick taken in: to the ward's beds, dosed with the rest.
            if (st.takeOurs && hour % 2 == 0) Ours(c, st);
            // Once a stay, a day the colony's people were in the ward: the illness crosses to the colony.
            if (!st.crossed && hour == 20 && (st.ward || st.ours.Count > 0)) Cross(c, st);
        }

        /// <summary>The dead seen to, the well sent home, the beds of the gone taken down.</summary>
        private static void Tidy(Camp c, SickhouseState st)
        {
            foreach (var p in st.patients.ToList())
            {
                if (p == null) continue;
                if (p.Dead)
                {
                    st.patients.Remove(p);
                    st.died++;
                    if (p.Corpse != null && p.Corpse.Spawned && p.Corpse.Map == c.map) FieldSickhouse.Died(c, p.Corpse);
                    continue;
                }
                if (!p.Spawned || p.Map != c.map)
                {
                    st.patients.Remove(p);
                    continue;
                }
                // Well: up from the bed, thanks said, and off home.
                bool hurt = p.health.hediffSet.HasTendableHediff() || p.health.hediffSet.BleedRateTotal > 0.01f;
                bool ill = p.health.hediffSet.hediffs.Any(h => h.def.makesSickThought && !h.FullyImmune());
                if (!hurt && !ill && !p.Downed && !HealthAIUtility.ShouldSeekMedicalRest(p))
                {
                    st.patients.Remove(p);
                    c.Note(c.Text("CC_PEv3_PatientWell", null, null, 0, p.LabelShort), MessageTypeDefOf.PositiveEvent, Importance.Flavor, p);
                    Arrivals.SendAway(new[] { p });
                    continue;
                }
                Bed(c, st, p);
            }
            st.ours.RemoveAll(p => p == null || p.Dead || (LifeChecks.Illness(p) == null && SickRoom.Spreading(p) == null));
            foreach (var b in st.beds.ToList())
            {
                if (b == null || b.Destroyed)
                {
                    st.beds.Remove(b);
                    continue;
                }
                if (b is Building_Bed bed && !st.patients.Any(p => p.CurrentBed() == bed) && !st.ours.Any(p => p.CurrentBed() == bed) && !bed.AnyOccupants && b.Faction != c.faction && b.Faction != Faction.OfPlayer)
                {
                    st.beds.Remove(b);
                    b.Destroy();
                }
            }
        }

        /// <summary>A bed of the ward for this one: a bedroll the sisters make up in a row by the hut, of the sleeper's own people.</summary>
        private static Building_Bed BedFor(Camp c, SickhouseState st, Pawn p)
        {
            var mine = st.beds.OfType<Building_Bed>().FirstOrDefault(b => b.Spawned && b.Faction == p.Faction && !b.AnyOccupants && b.OwnersForReading.Count == 0);
            if (mine != null) return mine;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Bedroll");
            var map = c.map;
            if (def == null) return null;
            var at = c.FireCell;
            foreach (var cell in GenRadial.RadialCellsAround(at, 9f, false))
            {
                if (!cell.InBounds(map) || cell.InHorDistOf(at, 3f) || !cell.Standable(map) || cell.GetEdifice(map) != null || cell.GetFirstItem(map) != null || cell.Fogged(map)) continue;
                if (!GenAdj.OccupiedRect(cell, Rot4.South, def.size).Cells.All(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null)) continue;
                try
                {
                    var thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                    thing.SetFaction(p.Faction);
                    var placed = GenSpawn.Spawn(thing, cell, map, Rot4.South);
                    st.beds.Add(placed);
                    return placed as Building_Bed;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: a bed in the ward", e);
                    return null;
                }
            }
            return null;
        }

        private static void Bed(Camp c, SickhouseState st, Pawn p)
        {
            if (p.InBed() || p.Downed || p.CurJobDef == JobDefOf.LayDown || p.CarriedBy != null) return;
            var bed = BedFor(c, st, p);
            if (bed == null) return;
            try
            {
                Lords.Leave(p);
                p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: to bed in the ward", e);
            }
        }

        /// <summary>A stranger of the region walks in to the ward, or is carried in by one of their own.</summary>
        private static bool Patient(Camp c, SickhouseState st)
        {
            var map = c.map;
            FieldSickhouse.Why(out bool plague);
            var p = FieldSickhouse.MakePatient(c, plague, out bool carried);
            if (p == null) return false;
            try
            {
                if (carried)
                {
                    var bearer = Arrivals.Make(Arrivals.PersonKind(p.Faction), p.Faction, new FloatRange(18f, 60f));
                    if (bearer == null || Arrivals.Visit(new List<Pawn> { bearer }, map, p.Faction, c.FireCell, 8f) == null)
                    {
                        if (bearer != null) Arrivals.Discard(bearer);
                        carried = false;
                    }
                    else
                    {
                        GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(bearer.Position, map, 2), map);
                        HealthUtility.DamageUntilDowned(p, false);
                        var bed = BedFor(c, st, p);
                        var job = FieldSickhouse.CarryInJob;
                        if (bed != null && job != null)
                        {
                            Lords.Leave(bearer);
                            var j = JobMaker.MakeJob(job, p, bed);
                            j.count = 1;
                            bearer.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                        }
                    }
                }
                if (!carried && Arrivals.Visit(new List<Pawn> { p }, map, p.Faction, c.FireCell, 72f) == null)
                {
                    Arrivals.Discard(p);
                    return false;
                }
                st.patients.Add(p);
                st.seen++;
                c.Note(c.Text(carried ? "CC_PEv3_PatientCarried" : "CC_PEv3_PatientWalks", null, null, 0, p.Faction?.Name ?? "?"), MessageTypeDefOf.NeutralEvent, Importance.Flavor, p, true);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: a patient comes", e);
                return false;
            }
        }

        private void Ask(Camp c, SickhouseState st)
        {
            foreach (string key in new[] { "medicine", "food", "cloth", "ward" })
            {
                if (st.asked.Contains(key)) continue;
                if (key == "ward" && c.DaysHere < 1.5f) return;
                var ask = c.AskByKey(key);
                if (ask == null || c.AskBlocked(ask) != null)
                {
                    st.asked.Add(key);
                    continue;
                }
                if (c.Request(ask))
                {
                    st.asked.Add(key);
                    st.lastAsk = GenDate.DaysPassed;
                }
                return;
            }
        }

        /// <summary>The colony's sick to the ward: walked to a bed there, or carried by one of the well.</summary>
        private static void Ours(Camp c, SickhouseState st)
        {
            var map = c.map;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => LifeChecks.Illness(p) != null || SickRoom.Spreading(p) != null).ToList())
            {
                if (!st.ours.Contains(p)) st.ours.Add(p);
                var cur = p.CurrentBed();
                if (cur != null && st.beds.Contains(cur)) continue;
                var bed = BedFor(c, st, p);
                if (bed == null) return;
                try
                {
                    if (p.Downed)
                    {
                        var carrier = PeoplesD.Able(map).Where(x => x != p && LifeChecks.Illness(x) == null && !x.Drafted && x.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some))
                            .OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                        if (carrier == null) continue;
                        var job = JobMaker.MakeJob(JobDefOf.Rescue, p, bed);
                        job.count = 1;
                        carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                    else if (!p.Drafted && !p.InMentalState && p.CurJobDef != JobDefOf.Ingest && p.CurJobDef != JobDefOf.LayDown && RestUtility.CanUseBedNow(bed, p, false, true))
                        p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: our sick to the ward", e);
                    return;
                }
            }
        }

        /// <summary>The illness crosses to the colony (the fever at their fire, through the harm and illness budgets), told.</summary>
        private static void Cross(Camp c, SickhouseState st)
        {
            var ill = FieldSickhouse.Illness(st);
            if (ill == null || !Harm.On || !Harm.SystemOn(HarmKinds.CampFever.system)) return;
            if (!Harm.Roll(HarmKinds.CampFever, PeopleEvents3Math.CrossChance)) return;
            st.crossed = true;
            var who = st.ours.Concat(c.map.mapPawns.FreeColonistsSpawned).Where(p => p != null && !p.Dead && Harm.May(p) && !p.health.hediffSet.HasHediff(ill)).RandomElementWithFallback();
            if (who == null || !IllnessBudget.Allows(c.map, ill, who) || Harm.Ail(who, ill, 0.1f, HarmKinds.CampFever) == null) return;
            IllnessBudget.Owe(c.map, ill, who);
            Tell.Message(NewsKind.Health, Importance.Important, c.map, c.Text("CC_PEv3_IllnessCrossed", null, who, 0, ill.label), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>By day and evening, a sister on her round: the next of the ward who needs her (tended or dosed), else the camp's usual day.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = FieldSickhouse.StateOf(c);
            var job = FieldSickhouse.RoundJob;
            if (st == null || job == null || !p.RaceProps.Humanlike) return null;
            var nursed = SickRoom.Nursed;
            var taken = new HashSet<Thing>(c.Here.Where(o => o != p && o.CurJobDef == job).Select(o => o.CurJob.targetA.Thing));
            var next = st.patients.Concat(st.ours).Where(x => x != null && x.Spawned && !x.Dead && !taken.Contains(x)
                                                              && (x.health.hediffSet.HasTendableHediff() || (nursed != null && !x.health.hediffSet.HasHediff(nursed) && x.health.hediffSet.hediffs.Any(h => h.def.makesSickThought))))
                .Where(x => p.CanReach(x, PathEndMode.Touch, Danger.Some)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (next == null) return null;
            var j = JobMaker.MakeJob(job, next);
            j.count = 600;
            return j;
        }

        /// <summary>At night, one of them by the lamp at the hut's door, sitting up with the ward.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = FieldSickhouse.StateOf(c);
            if (st == null || st.patients.Count == 0 || (hour < 21 && hour > 4)) return null;
            var keeper = c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed).OrderByDescending(x => x.needs?.rest?.CurLevelPercentage ?? 1f).FirstOrDefault();
            if (keeper != p || (p.needs?.rest != null && p.needs.rest.CurLevelPercentage < 0.3f)) return null;
            return DayJob(c, p);
        }

        public override void Leaving(Camp c)
        {
            var st = FieldSickhouse.StateOf(c);
            if (st == null) return;
            // The patients still lying in the ward go home with them; the beds made up for them are taken down.
            var still = st.patients.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed).ToList();
            if (still.Count > 0) Arrivals.SendAway(still);
            foreach (var b in st.beds.Where(b => b != null && !b.Destroyed && b.Faction != Faction.OfPlayer && b.Faction != c.faction).ToList())
                if (!(b is Building_Bed bed) || !bed.AnyOccupants) b.Destroy();
            var f = c.faction;
            if (f == null || c.Hostile || c.tension >= 40f) return;
            // The colony's name in the Book of the Spared.
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.SickhouseGoodwill, false);
            FactionMemory.Note(f, "campHelped", "CC_PEv3_BookOfSpared".Translate().Resolve());
            foreach (string id in st.fed.Distinct())
            {
                var p = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.GetUniqueLoadID() == id);
                if (p == null || p.defeated || !p.HasGoodwill || p.IsPlayer) continue;
                WorldSituationWorker.Goodwill(p, PeopleEvents3Math.FedGoodwill, false);
                FactionMemory.Note(p, "campHealed", "CC_PEv3_SickFed".Translate().Resolve());
            }
            News.Post("CC_PEv3_SparedNews".Translate(f.Name.Named("FACTION"), st.seen.Named("COUNT")).Resolve(), LookTargets.Invalid, NewsKind.World);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = FieldSickhouse.StateOf(c);
            if (st == null) return;
            if (st.died > 0) marks.Add("died");
            if (st.food || st.medicine) marks.Add("helped");
            if (st.ward) marks.Add("ward");
            if (st.seen >= 3) marks.Add("many");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{PATIENTS}") ? text : text.Replace("{PATIENTS}", (FieldSickhouse.StateOf(c)?.seen ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = FieldSickhouse.StateOf(c);
            if (a?.needs != "ward" || st == null) return null;
            return st.patients.Count > 0 ? null : "CC_PEv3_WardEmpty".Translate().Resolve();
        }

        /// <summary>An ask of theirs answered: noted (the goods were carried out by the ask's own effect), the ward's day set going, the colony's sick taken in.</summary>
        public static void Answered(Camp c, string what, Pawn a)
        {
            var st = FieldSickhouse.StateOf(c);
            if (st == null) return;
            switch (what)
            {
                case "medicine":
                    st.medicine = true;
                    break;
                case "food":
                    st.food = true;
                    foreach (var p in st.patients.Where(p => p?.Faction != null)) st.fed.Add(p.Faction.GetUniqueLoadID());
                    break;
                case "cloth":
                    st.cloth = true;
                    break;
                case "ward":
                    FieldSickhouse.Ward(c, a);
                    break;
                case "ours":
                    st.takeOurs = true;
                    break;
            }
        }
    }

    /// <summary>The sickhouse's asks answered (the goods carried out by DilemmaEffect_CarryOut beside this): noted, or set going.</summary>
    public class DilemmaEffect_SickhouseAsk : DilemmaEffect
    {
        /// <summary>"medicine", "food", "cloth", "ward" or "ours".</summary>
        public string what;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            switch (what)
            {
                case "ward":
                    if (ctx?.a == null) return;
                    sheet.Play("CC_PEv3_Impact_Ward".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv3_Impact_WardBrief".Translate().Resolve());
                    DilemmaImpact.Xp(sheet, ctx.a.LabelShort, ctx.a.ThingID, SkillDefOf.Medicine, PeopleEvents3Math.WardXp, false);
                    if (Harm.On)
                        sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_WardRisk".Translate(ctx.a.LabelShort, Mathf.RoundToInt(Harm.Chance(HarmKinds.Physician, PeopleEvents3Math.DoctorCatch) * 100f)).Resolve(),
                            "CC_PEv3_Impact_WardRiskBrief".Translate().Resolve(), "pev3wardrisk");
                    break;
                case "ours":
                    sheet.Play("CC_PEv3_Impact_Ours".Translate().Resolve(), "CC_PEv3_Impact_OursBrief".Translate().Resolve());
                    if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_Cross".Translate().Resolve(), "CC_PEv3_Impact_CrossBrief".Translate().Resolve(), "pev3cross");
                    break;
                case "food":
                    sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv3_Impact_Fed".Translate(PeopleEvents3Math.FedGoodwill).Resolve(), "CC_PEv3_Impact_FedBrief".Translate().Resolve(), "pev3fed");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_Sickhouse);
            if (c != null) CampWorker_Sickhouse.Answered(c, what, ctx.a);
        }
    }

    /// <summary>
    /// Sisters of {FACTION} at the gate: with {TALE} on the region, they would pitch a field sickhouse by the colony for the
    /// region's sick and wounded. {A} is who they speak to.
    /// </summary>
    public class HappeningWorker_FieldSickhouse : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            FieldSickhouse.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Healers) != null && FieldSickhouse.Why(out _) != null
            && FieldSickhouse.On(map) == null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Healers);
            ctx.tale = FieldSickhouse.Why(out bool plague);
            if (ctx.faction == null || ctx.tale == null) return false;
            ctx.variant = plague ? 1 : 0;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Healer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant == 1 ? "plague" : "war");
            if (Colonists(ctx.map).Any(p => LifeChecks.Illness(p) != null)) tags.Add("oursick");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (FieldSickhouse.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "pitch" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The sickhouse pitched (FieldSickhouse.Start); with <see cref="takeOurs"/> the colony's own sick go into its ward too.</summary>
    public class DilemmaEffect_SickhouseCamp : DilemmaEffect
    {
        public bool takeOurs;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 0, "CC_PEv3_Impact_Sickhouse".Translate(hc.faction?.Name ?? "?", PeopleEvents3Math.PatientsMin, PeopleEvents3Math.PatientsMax).Resolve(),
                "CC_PEv3_Impact_SickhouseBrief".Translate().Resolve(), "pev3sickhouse");
            if (takeOurs) sheet.Play("CC_PEv3_Impact_Ours".Translate().Resolve(), "CC_PEv3_Impact_OursBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_SickhouseRisk".Translate().Resolve(), "CC_PEv3_Impact_SickhouseRiskBrief".Translate().Resolve(), "pev3sickhouserisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (FieldSickhouse.Start(hc, takeOurs) != null) Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>
    /// A round in the ward (a sister, or the colony's doctor on its day): to the patient's side (target A), a while at it with the
    /// healing cross, then their wounds tended (the game's own tending) and the order's care for a fever.
    /// </summary>
    public class JobDriver_WardRound : JobDriver
    {
        private Pawn Patient => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Patient == null || Patient.Dead);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("WardRound");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (pawn.IsHashIntervalTick(200, delta)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);
                if (pawn.IsColonist) pawn.skills?.Learn(SkillDefOf.Medicine, 0.08f * delta);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(300, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("WardRoundDone");
            done.initAction = () =>
            {
                var p = Patient;
                if (p == null || p.Dead) return;
                try
                {
                    if (p.health.hediffSet.HasTendableHediff()) TendUtility.DoTend(pawn, p, null);
                    var nursed = SickRoom.Nursed;
                    if (nursed != null && !p.health.hediffSet.HasHediff(nursed) && p.health.hediffSet.hediffs.Any(h => h.def.makesSickThought)) p.health.AddHediff(nursed);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: a round in the ward", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>One of their own carries a patient in (target A) to a bed of the ward (target B), and goes home.</summary>
    public class JobDriver_CarryToWard : JobDriver
    {
        private Pawn Patient => job.targetA.Thing as Pawn;

        private Building_Bed Bed => job.targetB.Thing as Building_Bed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var lay = ToilMaker.MakeToil("IntoTheWard");
            lay.initAction = () =>
            {
                var bed = Bed;
                var p = Patient;
                if (bed == null || p == null) return;
                try
                {
                    RestUtility.TuckIntoBed(bed, pawn, p, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: carried into the ward", e);
                }
                Arrivals.SendAway(new[] { pawn });
            };
            lay.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return lay;
        }
    }
}
