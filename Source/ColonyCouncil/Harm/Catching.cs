using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 4: sicknesses caught from someone on the map (Harm/HarmFour.cs is the hub).
    /// <list type="bullet">
    /// <item>The physician catches what they tend (1.1.1): tending someone with a catching everyday ailment (a head cold, a
    /// chest cold, a stomach bug, pinkeye) passes it to the doctor one tending in 40; a contagious serious one
    /// (tuberculosis, cholera, measles) or the game's flu a tenth of that. Gloves and masks (an industrial or spacer colony)
    /// halve it, a sterile room quarters it, the quarantine or rest-when-ill decree halves it, and a doctor run off their
    /// feet (a third tending of the sick in a day) takes half again: a second doctor sharing the rounds keeps under it. It
    /// shows a day or two later. With the everyday ailments (the serious ones need theirs) and the deeper hooks.</item>
    /// <item>The guest's cough (2.5.2): one visit in 20 of Rimfolk's visitors brings a cough (the guest says so now and
    /// then); whoever of the colony spent the most hours near them catches it about a day after they go, unless the
    /// quarantine decree keeps the sick apart. A guest bed in a room of their own keeps the hours down. With the visitors,
    /// the everyday ailments and "Systems talk to each other".</item>
    /// <item>A cold night for the frail (1.2.3): under the banked fires decree in a hard frost (the night at -5 °C or
    /// colder), the old, small children, the frail and the convalescent who sleep three hours in a bedroom below their
    /// comfortable warmth catch a head cold one such night in seven, the road to pneumonia for them. A heater kept up in
    /// their room, or ending the decree in a cold snap, prevents it. With the everyday ailments and "Systems talk to each
    /// other"; named on the decree's card (DecreeHurt.ColdNights).</item>
    /// </list>
    /// Each is one of Rimfolk's ailments (or the game's flu) through Harm.Ail, under the cap, with the ailments' own message
    /// and the doctor's note, and a line over the head as it shows.
    /// </summary>
    public static class Catching
    {
        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            rounds.Clear();
            cold.Clear();
            near.Clear();
        }

        // ================= The physician catches what they tend (1.1.1) =================

        /// <summary>A doctor's thingIDNumber: the day, and the tendings of the sick that day.</summary>
        private static readonly Dictionary<int, (int day, int n)> rounds = new Dictionary<int, (int, int)>();

        private static readonly string[] Everyday = { "Council_Ail_HeadCold", "Council_Ail_ChestCold", "Council_Ail_StomachBug", "Council_Ail_Pinkeye" };
        private static readonly string[] Serious = { "Council_Ail_Tuberculosis", "Council_Ail_Cholera", "Council_Ail_Measles", "Flu" };

        /// <summary>The catching thing the patient has that shows, and whether it is the serious kind; null for none.</summary>
        private static Hediff CatchingOf(Pawn patient, out bool serious)
        {
            serious = false;
            var hs = patient.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (!h.Visible || h.def?.defName == null) continue;
                if (Array.IndexOf(Everyday, h.def.defName) >= 0) return h;
                if (Array.IndexOf(Serious, h.def.defName) >= 0)
                {
                    serious = true;
                    return h;
                }
            }
            return null;
        }

        /// <summary>The game's tending (Health/HealthPatches.cs, Patch_HealthTended): one roll for the doctor.</summary>
        public static void Tended(Pawn doctor, Pawn patient)
        {
            if (!Harm.On || doctor == null || patient == null || doctor == patient || !Harm.SystemOn(HarmKinds.Physician.system)) return;
            if (!HarmFour.Ours(doctor) || patient.health?.hediffSet == null) return;
            var h = CatchingOf(patient, out bool serious);
            if (h == null || serious && h.def.defName.StartsWith("Council_", StringComparison.Ordinal) && !AilmentTracker.SeriousOn) return;
            if (doctor.health.hediffSet.HasHediff(h.def) || Waiting(doctor)) return;
            int day = Now / HarmMath.DayTicks;
            int n = rounds.TryGetValue(doctor.thingIDNumber, out var r) && r.day == day ? r.n + 1 : 1;
            if (rounds.Count > 100) rounds.Clear();
            rounds[doctor.thingIDNumber] = (day, n);
            var room = patient.GetRoom();
            bool sterile = room != null && !room.PsychologicallyOutdoors && room.GetStat(RoomStatDefOf.InfectionChanceFactor) <= HarmFourMath.SterileInfection;
            bool decree = HarmFour.Decree(doctor, "Council_Decree_Quarantine") || HarmFour.Decree(doctor, "Council_Decree_SickBay");
            float chance = HarmFourMath.PhysicianChance(serious, Ages.Ours >= WorldAge.Industrial, sterile, decree, n);
            if (!Harm.Roll(doctor, HarmKinds.Physician, chance, ClassOf(h.def))) return;
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmWaiting.Caught, kind = HarmKinds.Physician.key, pawn = doctor, other = patient, def = h.def, name = patient.LabelShort,
                due = Now + HarmFourMath.CaughtTicks(Rand.Value)
            });
        }

        /// <summary>Someone already has something of theirs waiting to show.</summary>
        private static bool Waiting(Pawn p) => HarmBook.Instance?.waiting.Any(x => x != null && x.what == HarmWaiting.Caught && x.pawn == p) ?? false;

        /// <summary>A head cold is the mildest (children may catch it); the rest are grown-ups' hurts.</summary>
        private static HurtClass ClassOf(HediffDef def) => def?.defName == "Council_Ail_HeadCold" ? HurtClass.Mild : HurtClass.Injury;

        // ================= Showing =================

        /// <summary>A sickness caught shows now, through the harm service: true when it's done with (shown, or never will be).</summary>
        public static bool Show(HarmWaiting x)
        {
            var p = x.pawn;
            var def = x.def;
            if (p == null || def == null || p.Dead) return true;
            if (p.health.hediffSet.HasHediff(def)) return true;
            // On the road, or held back by the cap (a sickbed, a hurt lately): it waits, then passes (HarmWaiting.Lapsed).
            if (!p.Spawned || !Harm.May(p, ClassOf(def))) return false;
            var info = AilmentTracker.InfoOf(def);
            if (info != null && !info.serious && !AilmentTracker.On) return true;
            float severity = info != null && def.CompProps<HediffCompProperties_Immunizable>() == null ? info.startSeverity.RandomInRange : Math.Max(0.05f, def.initialSeverity);
            var h = Harm.Ail(p, def, severity, x.Kind, ClassOf(def));
            if (h == null) return false;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            string illness = AilmentTracker.NameOf(def);
            string note = AilmentTracker.DoctorsNote(def);
            switch (x.kind)
            {
                case "physician":
                    HarmFour.Say(p, "Rimfolk_Pool_CaughtIt", null, "tended");
                    HarmFour.TellHurt(p, "CC_HarmFour_Physician", p.Named("PAWN"), (x.name ?? "").Named("OTHER"), illness.Named("ILLNESS"), (note ?? "").Named("NOTE"));
                    break;
                case "guestCough":
                    HarmFour.Say(p, "Rimfolk_Pool_CaughtIt", null, "guest");
                    HarmFour.TellHurt(p, "CC_HarmFour_GuestCough", p.Named("PAWN"), (x.name ?? "").Named("OTHER"), illness.Named("ILLNESS"), (note ?? "").Named("NOTE"));
                    break;
                default:
                    HarmFour.Say(p, "Rimfolk_Pool_CaughtIt");
                    break;
            }
            return true;
        }

        // ================= The guest's cough (2.5.2) =================

        private static readonly HashSet<int> seen = new HashSet<int>();
        /// <summary>A coughing guest's thingIDNumber: each colonist's hours near them.</summary>
        private static readonly Dictionary<int, Dictionary<Pawn, int>> near = new Dictionary<int, Dictionary<Pawn, int>>();

        /// <summary>The hour: each visit of Rimfolk's visitors once, on its first hour here, rolls for a cough it brings.</summary>
        public static void Visits(HarmBook book, int now)
        {
            var tracker = VisitorTracker.Instance;
            if (tracker == null || !Harm.On || !Harm.SystemOn(HarmKinds.GuestCough.system)) return;
            foreach (var t in tracker.trips)
            {
                if (t == null || t.state != VisitState.Here || t.map == null || !t.map.IsPlayerHome || !seen.Add(t.id)) continue;
                if (seen.Count > 300) seen.Clear();
                // The roll is the visit's own, so a reload rolls it the same way.
                if (Rand.ValueSeeded(t.id * 7919 + 11) >= Harm.Chance(HarmKinds.GuestCough, HarmFourMath.GuestCoughVisit)) continue;
                var guest = t.Here.Where(g => g.RaceProps.Humanlike && !g.DevelopmentalStage.Baby()).RandomElementWithFallback();
                if (guest == null || book.waiting.Any(x => x != null && x.what == HarmWaiting.Cough && x.pawn == guest)) continue;
                HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Cough, kind = HarmKinds.GuestCough.key, pawn = guest, name = guest.LabelShort, made = now });
                HarmFour.Say(guest, "Rimfolk_Pool_GuestCough");
            }
        }

        /// <summary>
        /// A coughing guest's hour: who of the colony is near them (both awake, within four cells) counts it, and now and
        /// then the guest coughs; once they've gone, the one who spent the most hours with them catches it a day later.
        /// True when done with.
        /// </summary>
        public static bool CoughHour(HarmWaiting x, int now)
        {
            var g = x.pawn;
            if (g == null || g.Dead || !g.Spawned || g.Map == null || !g.Map.IsPlayerHome)
            {
                Caught(x, now);
                near.Remove(x.pawn?.thingIDNumber ?? -1);
                return true;
            }
            if (!near.TryGetValue(g.thingIDNumber, out var hours)) near[g.thingIDNumber] = hours = new Dictionary<Pawn, int>();
            bool any = false;
            if (RestUtility.Awake(g))
                foreach (var c in g.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (!RestUtility.Awake(c) || !c.Position.InHorDistOf(g.Position, HarmFourMath.CoughRange)) continue;
                    any = true;
                    hours[c] = (hours.TryGetValue(c, out int h) ? h : 0) + 1;
                    if (hours[c] > x.count || x.other == c)
                    {
                        x.other = c;
                        x.count = hours[c];
                    }
                }
            // together: a cough among those near them, said to nobody in particular.
            if (any && Rand.Chance(0.3f)) HarmFour.Say(g, "Rimfolk_Pool_GuestCough");
            return false;
        }

        private static void Caught(HarmWaiting x, int now)
        {
            var c = x.other;
            if (c == null || c.Dead || !HarmFour.Ours(c)) return;
            if (!Rand.Chance(HarmFourMath.GuestCoughCatch(x.count, DecreeWork.ContagionFactorFor(c)))) return;
            var def = HarmFour.Child(c) || !Rand.Chance(HarmFourMath.ChestColdShare) ? HarmFour.Def("Council_Ail_HeadCold") : HarmFour.Def("Council_Ail_ChestCold");
            if (def == null || c.health.hediffSet.HasHediff(def) || Waiting(c)) return;
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmWaiting.Caught, kind = HarmKinds.GuestCough.key, pawn = c, def = def, name = x.name,
                due = now + (int)(HarmFourMath.GuestCoughDays * HarmMath.DayTicks)
            });
        }

        // ================= A cold night for the frail (1.2.3) =================

        /// <summary>A colonist's thingIDNumber: the night (its day) and the hours asleep cold in it.</summary>
        private static readonly Dictionary<int, (int night, int hours)> cold = new Dictionary<int, (int, int)>();

        private static bool Frail(Pawn p) => AilmentTracker.Vulnerable(p) || HealthTracker.ConvalescenceOf(p) != null;

        /// <summary>
        /// Each colonist's hour: asleep, under the banked fires decree, in a hard frost, frail and colder than comfortable,
        /// the night's cold hours are counted; awake again after three of them, one roll for a chill.
        /// </summary>
        public static void ColdHour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p) || p.DevelopmentalStage.Baby()) return;
            bool asleep = !RestUtility.Awake(p);
            // The night counts from the evening: the hours before noon belong to the night before.
            int night = (now - 12 * HarmMath.HourTicks) / HarmMath.DayTicks;
            cold.TryGetValue(p.thingIDNumber, out var c);
            if (asleep)
            {
                if (c.night != night) c = (night, 0);
                if (!Harm.SystemOn(HarmKinds.ColdNight.system) || !HarmFour.Decree(p, "Council_Decree_BankedFires")) return;
                float outdoor = p.Map.mapTemperature.OutdoorTemp;
                if (outdoor > HarmFourMath.HardFrost || !p.InBed() || p.AmbientTemperature >= p.ComfortableTemperatureRange().min || !Frail(p)) return;
                if (cold.Count > 200) cold.Clear();
                cold[p.thingIDNumber] = (night, c.hours + 1);
                return;
            }
            if (c.hours < HarmFourMath.ColdHours) return;
            cold.Remove(p.thingIDNumber);
            if (!Harm.SystemOn(HarmKinds.ColdNight.system) || !HarmFour.Decree(p, "Council_Decree_BankedFires")) return;
            var def = HarmFour.Def("Council_Ail_HeadCold");
            if (def == null || p.health.hediffSet.HasHediff(def) || !Harm.Roll(p, HarmKinds.ColdNight, HarmFourMath.ColdNightChance, HurtClass.Mild)) return;
            var info = AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.ColdNight, HurtClass.Mild);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, "Rimfolk_Pool_ColdNight");
            var doctor = HarmFour.DoctorNear(p);
            // together: the doctor is within talking range of them (Together.Can in DoctorNear).
            if (doctor != null) HarmFour.Say(doctor, "Rimfolk_Pool_ColdNightDoctor", p);
            HarmFour.TellHurt(p, "CC_HarmFour_ColdNight", p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS"), (AilmentTracker.DoctorsNote(def) ?? "").Named("NOTE"));
        }

        /// <summary>Dev mode: whatever the patient has that catches, caught by the doctor now (past its rate, never past the cap).</summary>
        public static string DebugCatch(Pawn doctor, Pawn patient)
        {
            var h = CatchingOf(patient, out _);
            if (h == null) return patient.LabelShort + " has nothing that catches.";
            HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Caught, kind = HarmKinds.Physician.key, pawn = doctor, other = patient, def = h.def, name = patient.LabelShort, due = Now });
            return doctor.LabelShort + " has caught " + h.def.label + " from " + patient.LabelShort + "; it shows within the hour" + (Harm.May(doctor, ClassOf(h.def)) ? "." : ", once the cap lets it (" + Harm.Bar(doctor, ClassOf(h.def)) + ").");
        }

        /// <summary>Dev mode: the next guest to stay coughs.</summary>
        public static string DebugCough(Pawn guest)
        {
            HarmFour.Wait(new HarmWaiting { what = HarmWaiting.Cough, kind = HarmKinds.GuestCough.key, pawn = guest, name = guest.LabelShort, made = Now });
            HarmFour.Say(guest, "Rimfolk_Pool_GuestCough");
            return guest.LabelShort + " is coughing; whoever spends the most hours near them catches it a day after they go.";
        }

        /// <summary>Dev mode: a cold night for this colonist now, as if they woke from one (the decree and the frost still asked).</summary>
        public static string DebugColdNight(Pawn p)
        {
            cold[p.thingIDNumber] = ((Now - 12 * HarmMath.HourTicks) / HarmMath.DayTicks, HarmFourMath.ColdHours);
            return p.LabelShort + (Frail(p) ? " is frail" : " isn't frail (old, a small child, frail or convalescent)") + "; the next hour they're awake rolls for a chill"
                   + (HarmFour.Decree(p, "Council_Decree_BankedFires") ? "." : ", but only under the banked fires decree.");
        }
    }
}
