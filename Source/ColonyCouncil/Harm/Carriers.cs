using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 5: sickness that spreads (Harm/HarmFive.cs is the hub). Every one of them is the guest's cough's rule
    /// (Harm/Catching.cs): someone carries it, and whoever of the colony spent the most hours near them (both awake, within
    /// four cells) catches it a day or a few after, unless a decree keeps the sick apart; or, for the patient, whoever
    /// sleeps in the same room. Each occasion rolls once through the harm service with its kind; what follows is the
    /// rule's own.
    /// <list type="bullet">
    /// <item>The sick work on (1.2.5): a petition to rest when ill or for a quarantine let lapse while something catching
    /// shows on one of the colony who is up and working: they work on a day, and it reaches whoever worked nearest them.
    /// Named under the petition on the decree page. With the everyday ailments, petitions and "Systems talk to each
    /// other".</item>
    /// <item>The sickness at the gate (2.3.1): a visitor's or a trader's group at the gate from a people whose towns a
    /// plague lies over (the region's world event, as sickness brought home reads it) carries it one time in four: the
    /// game's flu, or its plague one time in four. The gate shut at the world event's asking, or the quarantine decree,
    /// keeps them out. With world events and "Systems talk to each other"; named under keeping the gate open. The plague's
    /// own raised flu and plague come a little less often for it (Council_WS_Plague), so the total stays.</item>
    /// <item>A courier who arrives sick (2.7.2): a courier of such a people, the same rule. With the couriers.</item>
    /// <item>The fever at their fire (2.4.2): the camp's sick one passes it one camp sickness in five to whoever sat
    /// longest at their fire in its first three days: a chest cold, or the flu one time in four (a child a head cold).
    /// Sending the colony's doctor (the camp's ask) stops it. With the camps; named under the ask.</item>
    /// <item>The patient who brings something worse (3.2.2): one outside patient in 20 brings tuberculosis or cholera
    /// (Health/HospitalPatient.cs gives it, and its letter names it); a night asleep in the same room passes it one time
    /// in four, less under the quarantine decree. A ward of their own prevents it. With the hospital's name and the serious
    /// illnesses.</item>
    /// <item>The hedge healer's bad batch (2.2.4): one paid visit in eight the travelling healer's batch is bad, unless the
    /// colony's own doctor (Medicine 8) looks it over first (a word said, and nothing more); bad, one in three of those
    /// dosed is queasy with the stomach bug the next morning (a bitter tonic before industry), or has a sore arm where the
    /// shot went in (after). Named under paying.</item>
    /// </list>
    /// Messages where the game gives one for an illness caught, naming who it came from; the sore arm is seen only.
    /// </summary>
    public static class Carriers
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next courier, healer's batch or hospital patient carries it, past its rate (never past the cap).</summary>
        public static bool DebugCourier, DebugBatch, DebugPatient;

        public static void Reset()
        {
            seenLords.Clear();
            ward.Clear();
            wardRolled.Clear();
        }

        // ================= Which carrier =================

        private static CarrierKind? KindOf(HarmWaiting x)
        {
            if (x?.kind == HarmKinds.SickWorkOn.key) return CarrierKind.SickWorker;
            if (x?.kind == HarmKinds.GateSickness.key) return CarrierKind.Gate;
            if (x?.kind == HarmKinds.CourierSick.key) return CarrierKind.Courier;
            if (x?.kind == HarmKinds.CampFever.key) return CarrierKind.Camp;
            return null;
        }

        /// <summary>What a carrier says now and then to those near them.</summary>
        public static string CoughPool(HarmWaiting x)
        {
            switch (KindOf(x))
            {
                case CarrierKind.SickWorker: return "Rimfolk_Pool_SickWorksOn";
                case CarrierKind.Gate:
                case CarrierKind.Courier: return "Rimfolk_Pool_GateCough";
                case CarrierKind.Camp: return "Rimfolk_Pool_CampFever";
                default: return "Rimfolk_Pool_GuestCough";
            }
        }

        /// <summary>A carrier who passes nothing on after all: well again, or (the camp's sick one) seen to.</summary>
        public static bool Stopped(HarmWaiting x)
        {
            var g = x.pawn;
            switch (KindOf(x))
            {
                case CarrierKind.SickWorker: return x.def != null && !g.health.hediffSet.HasHediff(x.def);
                case CarrierKind.Camp:
                    var flu = Camp.Fever != null ? g.health.hediffSet.GetFirstHediffOfDef(Camp.Fever) : null;
                    return flu == null || flu.IsTended();
                default: return false;
            }
        }

        /// <summary>What one who sat closest to a carrier comes down with (null: nothing).</summary>
        public static HediffDef Passed(HarmWaiting x, Pawn c)
        {
            switch (KindOf(x))
            {
                case CarrierKind.SickWorker:
                    if (x.def?.defName != null && x.def.defName.StartsWith("Council_", StringComparison.Ordinal) && (AilmentTracker.InfoOf(x.def)?.serious ?? false) && !AilmentTracker.SeriousOn) return null;
                    return x.def;
                case CarrierKind.Gate:
                case CarrierKind.Courier: return x.def;
                case CarrierKind.Camp:
                    if (HarmFour.Child(c)) return HarmFour.Def("Council_Ail_HeadCold");
                    return Rand.Chance(HarmFiveMath.CampFluShare) ? Camp.Fever : AilmentTracker.On ? HarmFour.Def("Council_Ail_ChestCold") : Camp.Fever;
                default: return null;
            }
        }

        public static int OnsetTicks(HarmWaiting x) => HarmFiveMath.CarrierOnsetTicks(KindOf(x) ?? CarrierKind.SickWorker, Rand.Value);

        public static float KeepDays(HarmWaiting x) => KindOf(x) is CarrierKind k ? HarmFiveMath.CarrierKeepDays(k) : 6f;

        /// <summary>A sickness of part 5 shows (Catching.Show): the line and the message naming whom it came from. False: not one of these.</summary>
        public static bool TellCaught(HarmWaiting x, Pawn p, string illness, string note)
        {
            string key, tag;
            switch (x.kind)
            {
                case "sickWorkOn": key = "CC_HarmFive_SickWorkOn"; tag = "workedOn"; break;
                case "gateSickness": key = "CC_HarmFive_GateSick"; tag = "atGate"; break;
                case "courierSick": key = "CC_HarmFive_CourierSick"; tag = "atGate"; break;
                case "campFever": key = "CC_HarmFive_CampFever"; tag = "campFire"; break;
                case "patientIll": key = "CC_HarmFive_PatientIll"; tag = "sharedRoom"; break;
                case "badBatch": key = "CC_HarmFive_BadBatch"; tag = "badBatch"; break;
                default: return false;
            }
            HarmFour.Say(p, "Rimfolk_Pool_CaughtFive", null, tag);
            HarmFour.TellHurt(p, key, p.Named("PAWN"), (x.name ?? "").Named("OTHER"), illness.Named("ILLNESS"), (note ?? "").Named("NOTE"));
            return true;
        }

        private static bool Carrying(Pawn p) => HarmBook.Instance?.waiting.Any(w => w != null && w.what == HarmWaiting.Cough && w.pawn == p) ?? false;

        private static void Carry(CarrierKind kind, HarmKind harm, Pawn who, HediffDef def, int window, string pool)
        {
            int now = Now;
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmWaiting.Cough, kind = harm.key, pawn = who, def = def, name = who.LabelShort, made = now, due = window > 0 ? now + window : 0
            });
            // apart: a cough among whoever is about, said to nobody in particular.
            if (pool != null) HarmFour.Say(who, pool);
        }

        // ================= The sick work on (1.2.5) =================

        /// <summary>
        /// A petition lapsed unheeded (Decrees/Decrees.cs, the daily look): to rest when ill or for a quarantine, while one
        /// of the colony is up and about with something catching that shows, the sick one works on a day.
        /// </summary>
        public static void PetitionLapsed(DecreeDef def, Pawn asker)
        {
            if (!Harm.On || def == null || !HarmFiveMath.SickPetition(def.defName) || !Harm.SystemOn(HarmKinds.SickWorkOn.system)) return;
            var map = asker?.MapHeld;
            if (map == null || !map.IsPlayerHome) return;
            Pawn sick = null;
            Hediff what = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder())
            {
                if (!HarmFour.Ours(p) || p.Downed || p.InBed() || Carrying(p)) continue;
                var h = Catching.CatchingOf(p, out _);
                if (h == null) continue;
                sick = p;
                what = h;
                break;
            }
            if (sick == null || !Harm.Roll(HarmKinds.SickWorkOn, HarmFiveMath.CarrierChance(CarrierKind.SickWorker, false, false))) return;
            Carry(CarrierKind.SickWorker, HarmKinds.SickWorkOn, sick, what.def, (int)(HarmFiveMath.SickWorkOnHours * HarmMath.HourTicks), "Rimfolk_Pool_SickWorksOn");
            // apart: the petitioner's word over their own head as the petition goes, to nobody.
            if (asker != null && asker != sick && asker.Spawned) HarmFour.Say(asker, "Rimfolk_Pool_PetitionUnheeded");
        }

        /// <summary>The decree page, under a petition to rest when ill or for a quarantine: what letting it lapse can do.</summary>
        public static string LapseLine(DecreeDef def)
        {
            if (def == null || !Harm.On || !HarmFiveMath.SickPetition(def.defName) || !Harm.SystemOn(HarmKinds.SickWorkOn.system)) return null;
            return "CC_HarmFive_PetitionLapse".Translate().Resolve();
        }

        // ================= The sickness at the gate (2.3.1), a courier who arrives sick (2.7.2) =================

        /// <summary>
        /// A plague lies over one of this people's towns now (its people's own world event, or one within reach of the
        /// town, as sickness brought home reads it); <paramref name="shut"/>: the colony shut its gate at that event's asking.
        /// </summary>
        public static bool PlagueOver(Faction f, out bool shut)
        {
            shut = false;
            var ws = WorldSituations.Instance;
            if (f == null || ws == null) return false;
            bool any = false;
            foreach (var w in ws.Running)
            {
                if (w?.def == null || !w.def.bad || HarmTwoMath.KindOf(w.def.kind, w.def.defName == "Council_WS_Murrain") != SickTown.Plague) continue;
                bool reaches = w.ctx?.faction == f;
                if (!reaches)
                    foreach (var s in Find.WorldObjects.Settlements)
                        if (s?.Faction == f && HarmTwoMath.Reaches(false, SickHome.TilesTo(w, s)))
                        {
                            reaches = true;
                            break;
                        }
                if (!reaches) continue;
                any = true;
                if (w.Has("quarantine")) shut = true;
            }
            return any;
        }

        /// <summary>The carriers at the gate are on: a plague's raised flu and plague give up their share (SituationModifier.withCarriers).</summary>
        public static bool GateOn => Harm.On && Harm.SystemOn(HarmKinds.GateSickness.system);

        /// <summary>The colony at this home keeps the quarantine decree: strangers turned away at the gate.</summary>
        private static bool Quarantined(Map map)
        {
            var p = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            return p != null && HarmFour.Decree(p, "Council_Decree_Quarantine");
        }

        /// <summary>A plague's sickness: the game's flu, or its plague one time in four.</summary>
        private static HediffDef PlagueSickness()
        {
            var flu = HarmFour.Def("Flu");
            var plague = HediffDefOf.Plague;
            return Rand.Chance(HarmTwoMath.PlagueShare) && plague != null ? plague : flu ?? plague;
        }

        private static readonly HashSet<int> seenLords = new HashSet<int>();

        /// <summary>The hour: each group of visitors or traders at a home's gate once, on its first hour here.</summary>
        public static void Gate(int now)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.GateSickness.system) || WorldSituations.Instance == null) return;
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                if (!map.IsPlayerHome || map.lordManager == null) continue;
                foreach (var lord in map.lordManager.lords)
                {
                    if (lord?.faction == null || lord.faction.IsPlayer || lord.faction.HostileTo(Faction.OfPlayer)) continue;
                    if (!(lord.LordJob is LordJob_VisitColony) && !(lord.LordJob is LordJob_TradeWithColony)) continue;
                    if (!seenLords.Add(lord.loadID)) continue;
                    if (seenLords.Count > 300) seenLords.Clear();
                    if (!PlagueOver(lord.faction, out bool shut)) continue;
                    float chance = Harm.Chance(HarmKinds.GateSickness, HarmFiveMath.CarrierChance(CarrierKind.Gate, shut, Quarantined(map)));
                    // The roll is the group's own, so a reload rolls it the same way.
                    if (Rand.ValueSeeded(lord.loadID * 7919 + 211) >= chance) continue;
                    var trader = lord.LordJob is LordJob_TradeWithColony ? TraderCaravanUtility.FindTrader(lord) : null;
                    var carrier = trader ?? lord.ownedPawns.Where(p => p != null && p.Spawned && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
                    if (carrier == null || Carrying(carrier)) continue;
                    Carry(CarrierKind.Gate, HarmKinds.GateSickness, carrier, PlagueSickness(), 0, "Rimfolk_Pool_GateCough");
                }
            }
        }

        /// <summary>A courier's party came onto the map (Couriers/CourierTracker.cs): one of a people under a plague may bring it.</summary>
        public static void CourierCame(Faction f, List<Pawn> people, Map map)
        {
            if (!Harm.On || people.NullOrEmpty() || map == null || !map.IsPlayerHome || !Harm.SystemOn(HarmKinds.CourierSick.system)) return;
            bool debug = DebugCourier;
            DebugCourier = false;
            bool shut = false;
            if (!debug && !PlagueOver(f, out shut)) return;
            float chance = Harm.Chance(HarmKinds.CourierSick, HarmFiveMath.CarrierChance(CarrierKind.Courier, shut, Quarantined(map)));
            if (!debug && !Rand.Chance(chance)) return;
            var courier = people.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);
            if (courier == null || Carrying(courier)) return;
            Carry(CarrierKind.Courier, HarmKinds.CourierSick, courier, PlagueSickness(), 0, "Rimfolk_Pool_GateCough");
        }

        /// <summary>The plague's "keep the gate open" answer: travellers from the sick towns may carry it in.</summary>
        public static void DescribeGate(ImpactSheet sheet)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.GateSickness.system)) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_GateSickness".Translate().Resolve(), "CC_Impact_GateSicknessBrief".Translate().Resolve(), "gatesickness");
        }

        // ================= The fever at their fire (2.4.2) =================

        /// <summary>One of the camp has fallen ill by their fire (Camps/CampStory.cs): one camp sickness in five is catching.</summary>
        public static void CampFell(Pawn who)
        {
            if (!Harm.On || who == null || !Harm.SystemOn(HarmKinds.CampFever.system) || Carrying(who)) return;
            if (!Harm.Roll(HarmKinds.CampFever, HarmFiveMath.CarrierChance(CarrierKind.Camp, false, false))) return;
            Carry(CarrierKind.Camp, HarmKinds.CampFever, who, null, (int)(HarmFiveMath.CampFeverDays * HarmMath.DayTicks), null);
        }

        /// <summary>The camp's ask to see to their sick one: seen to, the fever won't pass to whoever sits at their fire.</summary>
        public static void DescribeCampTend(ImpactSheet sheet, Pawn sick)
        {
            if (!Harm.On || sick == null || !Harm.SystemOn(HarmKinds.CampFever.system)) return;
            sheet.Add(ImpactKind.Health, 1, "CC_Impact_CampFeverStops".Translate(sick.LabelShort).Resolve(), "CC_Impact_CampFeverStopsBrief".Translate().Resolve(), "campfever");
        }

        // ================= The patient who brings something worse (3.2.2) =================

        /// <summary>
        /// A patient comes for the hospital (Health/HospitalPatient.cs): one in 20 brings tuberculosis or cholera as well,
        /// which the caller gives them. Null: nothing worse.
        /// </summary>
        public static HediffDef PatientWorse(Pawn patient)
        {
            if (!Harm.On || patient == null || !Harm.SystemOn(HarmKinds.PatientIll.system)) return null;
            bool debug = DebugPatient;
            DebugPatient = false;
            if (!debug && !Harm.Roll(HarmKinds.PatientIll, HarmFiveMath.PatientCarrierChance)) return null;
            var def = Rand.Chance(HarmFiveMath.PatientTuberculosisShare) ? HarmFour.Def("Council_Ail_Tuberculosis") : HarmFour.Def("Council_Ail_Cholera");
            return def != null && !patient.health.hediffSet.HasHediff(def) ? def : null;
        }

        /// <summary>The patient has it now: the ward's nights are watched, and the patient coughs on arrival.</summary>
        public static void PatientCarries(Pawn patient, HediffDef def)
        {
            if (patient == null || def == null) return;
            HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Ward, kind = HarmKinds.PatientIll.key, pawn = patient, def = def, name = patient.LabelShort, made = Now });
        }

        /// <summary>The event's words for the worse thing they came with, in place of the small one.</summary>
        public static string WorseTale(HediffDef def) => (def?.defName == "Council_Ail_Cholera" ? "CC_HarmFive_PatientTale_Cholera" : "CC_HarmFive_PatientTale_TB").Translate().Resolve();

        /// <summary>A patient's thingIDNumber: each colonist's hours asleep in the room with them tonight.</summary>
        private static readonly Dictionary<int, Dictionary<Pawn, (int night, int hours)>> ward = new Dictionary<int, Dictionary<Pawn, (int, int)>>();
        private static readonly HashSet<long> wardRolled = new HashSet<long>();

        /// <summary>The patient's hour: asleep in a room, whoever of the colony sleeps in it too counts the night; three hours, one roll.</summary>
        public static bool WardHour(HarmWaiting x, int now)
        {
            var pt = x.pawn;
            if (pt == null || pt.Dead || x.def == null) return true;
            if (!pt.Spawned || pt.Map == null || !pt.Map.IsPlayerHome || RestUtility.Awake(pt) || !pt.InBed()) return false;
            var room = RestUtility.CurrentBed(pt)?.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return false;
            int night = (now - 12 * HarmMath.HourTicks) / HarmMath.DayTicks;
            if (!ward.TryGetValue(pt.thingIDNumber, out var hours)) ward[pt.thingIDNumber] = hours = new Dictionary<Pawn, (int, int)>();
            if (ward.Count > 30) ward.Clear();
            foreach (var bed in room.ContainedBeds)
                foreach (var c in bed.CurOccupants)
                {
                    if (c == pt || !HarmFour.Ours(c) || RestUtility.Awake(c)) continue;
                    var h = hours.TryGetValue(c, out var was) && was.night == night ? (night, was.hours + 1) : (night, 1);
                    hours[c] = h;
                    if (h.Item2 != HarmFiveMath.WardHours) continue;
                    long key = (long)pt.thingIDNumber << 32 | (uint)c.thingIDNumber;
                    if (!wardRolled.Add(key)) continue;
                    if (wardRolled.Count > 500) wardRolled.Clear();
                    if (!Harm.May(c, HurtClass.Injury) || c.health.hediffSet.HasHediff(x.def) || Catching.Waiting(c)) continue;
                    if (!Rand.Chance(HarmFiveMath.WardCatch(h.Item2, DecreeWork.ContagionFactorFor(c)))) continue;
                    HarmFour.Wait(new HarmWaiting
                    {
                        what = HarmWaiting.Caught, kind = HarmKinds.PatientIll.key, pawn = c, def = x.def, name = x.name,
                        due = now + HarmFourMath.CaughtTicks(Rand.Value)
                    });
                }
            return false;
        }

        // ================= The hedge healer's bad batch (2.2.4) =================

        /// <summary>
        /// The colony paid the travelling healer for doses (Happenings/Workers_More.cs): one visit in eight the batch is bad;
        /// the colony's doctor of Medicine 8 or more looks it over first and catches it (their word to the healer).
        /// </summary>
        public static void Batch(Pawn healer, Pawn doctor, bool tonic)
        {
            if (!Harm.On || healer == null || !Harm.SystemOn(HarmKinds.BadBatch.system)) return;
            if (tonic && !AilmentTracker.On) return;
            bool debug = DebugBatch;
            DebugBatch = false;
            bool bad = debug || Harm.Roll(HarmKinds.BadBatch, HarmFiveMath.BadBatchChance);
            int medicine = doctor?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            if (bad && !HarmFiveMath.BatchBad(true, medicine))
            {
                // together: the doctor looks the healer's case over beside them when the two are together; else a word to themselves.
                HarmFour.Say(doctor, "Rimfolk_Pool_BatchSpotted", Together.Can(doctor, healer) ? healer : null);
                return;
            }
            if (!bad) return;
            HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Batch, kind = HarmKinds.BadBatch.key, pawn = healer, name = healer.LabelShort, flag = tonic, made = Now });
        }

        /// <summary>The healer gave someone a dose (VisitorServices.Dosed, or all at once): from a bad batch, one in three is the worse for it next morning.</summary>
        public static void Dosed(Pawn healer, Pawn patient)
        {
            var book = HarmBook.Instance;
            if (book == null || healer == null || patient == null || !HarmFour.Ours(patient)) return;
            var batch = book.waiting.FirstOrDefault(w => w != null && w.what == HarmWaiting.Batch && w.pawn == healer);
            if (batch == null || !Rand.Chance(HarmFiveMath.BadBatchSick)) return;
            var bug = batch.flag ? HarmFour.Def("Council_Ail_StomachBug") : null;
            if (batch.flag && (bug == null || patient.health.hediffSet.HasHediff(bug) || Catching.Waiting(patient))) return;
            HarmFour.Wait(new HarmWaiting
            {
                what = batch.flag ? HarmWaiting.Caught : HarmWaiting.Dose, kind = HarmKinds.BadBatch.key, pawn = patient, def = bug, name = healer.LabelShort,
                due = Now + HarmFiveMath.DoseOnsetTicks(Rand.Value)
            });
        }

        /// <summary>A shot from a bad batch, the next morning: a sore arm where it went in. True when done with.</summary>
        public static bool SoreArm(HarmWaiting x, int now)
        {
            var p = x.pawn;
            if (p == null || p.Dead) return true;
            if (now < x.due || !p.Spawned || !Harm.May(p, HurtClass.Mild)) return false;
            var h = Harm.Injure(p, HurtKind.Bruise, HurtWhere.Arm, HarmFiveMath.SoreArmLow, HarmFiveMath.SoreArmHigh, x.Kind, HurtClass.Mild);
            if (h == null) return false;
            HarmFour.Say(p, "Rimfolk_Pool_SoreArm");
            HarmFour.Mote(p, "CC_HarmFive_Mote_SoreArm");
            return true;
        }

        /// <summary>The healer's paid doses: now and then a bad batch, unless a doctor of the colony looks it over.</summary>
        public static void DescribeBatch(ImpactSheet sheet, Pawn doctor)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.BadBatch.system)) return;
            int medicine = doctor?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            if (medicine >= HarmFiveMath.BatchSpotSkill)
                sheet.Add(ImpactKind.Health, 1, "CC_Impact_BatchLooked".Translate(doctor.LabelShort).Resolve(), "CC_Impact_BatchLookedBrief".Translate(doctor.LabelShort).Resolve(), "badbatch");
            else
                sheet.Add(ImpactKind.Health, -1, "CC_Impact_BadBatch".Translate().Resolve(), "CC_Impact_BadBatchBrief".Translate().Resolve(), "badbatch");
        }

        // ================= Dev mode =================

        /// <summary>Dev mode: this colonist works on with what they have, as if a petition lapsed (past its rate).</summary>
        public static string DebugSickWorker(Pawn p)
        {
            var h = Catching.CatchingOf(p, out _);
            if (h == null) return p.LabelShort + " has nothing that catches.";
            Carry(CarrierKind.SickWorker, HarmKinds.SickWorkOn, p, h.def, (int)(HarmFiveMath.SickWorkOnHours * HarmMath.HourTicks), "Rimfolk_Pool_SickWorksOn");
            return p.LabelShort + " works on with " + h.def.label + " for a day; whoever is nearest them most catches it a day after.";
        }

        /// <summary>Dev mode: a visitor or trader carries the plague's sickness now (past its rate and the plague).</summary>
        public static string DebugGate(Pawn g)
        {
            Carry(CarrierKind.Gate, HarmKinds.GateSickness, g, PlagueSickness(), 0, "Rimfolk_Pool_GateCough");
            return g.LabelShort + " carries it; whoever spends the most hours near them falls ill two or three days after they go.";
        }

        /// <summary>Dev mode: the camp's sick one is catching now (past its rate).</summary>
        public static string DebugCamp(Map map)
        {
            var sick = CampTracker.VisitingAt(map, c => c.sick != null)?.sick;
            if (sick == null) return "no camp here with one of theirs lying ill (the camp's trouble).";
            Carry(CarrierKind.Camp, HarmKinds.CampFever, sick, null, (int)(HarmFiveMath.CampFeverDays * HarmMath.DayTicks), null);
            return sick.LabelShort + "'s fever is catching for three days, unless the colony's doctor sees to them.";
        }
    }
}
