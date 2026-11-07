using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 5: the first batch of the review's remaining proposals the player took on 7 November 2026 ("all
    /// suggestions please"), sickness that spreads and the weather and the cold (docs/harm-review-proposals.md, each
    /// marked built there), each through the harm service with its kind, so the shared budget, the setting, the
    /// storyteller and the cap hold for all of them (Harm/Harm.cs, HarmBudgetMath.cs; the rates in HarmFiveMath.cs). This
    /// is the hub, on the harm book's hour (the carriers at the gate, the patient's ward, what is waiting) and its 250-tick
    /// slices (each colonist's hour); nothing here has a clock of its own. The rest is in Carriers.cs, Untended.cs,
    /// ColdHurts.cs and NightWalks.cs; the forecast that can be wrong (4.4.2) hurts nobody and is the fields' own
    /// (Fields/FieldWeather.cs).
    /// </summary>
    public static class HarmFive
    {
        private static readonly List<Pawn> slice = new List<Pawn>();

        public static void Reset()
        {
            Carriers.Reset();
            Untended.Reset();
            ColdHurts.Reset();
            NightWalks.Reset();
        }

        /// <summary>The harm book's hour (its first slice): the groups at the gate.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            Safe("the gate", () => Carriers.Gate(now));
        }

        /// <summary>One slice of the colony's hour (every 250 ticks): each colonist in it once an hour.</summary>
        public static void Slice(HarmBook book, int which, int now)
        {
            slice.Clear();
            var all = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (Sched.SliceOf(all[i].thingIDNumber, Sched.Slices) == which) slice.Add(all[i]);
            for (int i = 0; i < slice.Count; i++)
            {
                var p = slice[i];
                if (p?.Map == null || p.Dead || p.RaceProps?.Humanlike != true || p.health?.hediffSet == null) continue;
                try
                {
                    Untended.Hour(p, now);
                    ColdHurts.Hour(p, now);
                    NightWalks.Hour(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm five: the hour of " + p.LabelShort, e);
                }
            }
            slice.Clear();
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm five: " + what, e);
            }
        }

        /// <summary>Part 5's waiting things, at the hour (HarmFour.Due's own): true when done with.</summary>
        public static bool Due(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                case HarmWaiting.Ward: return Carriers.WardHour(x, now);
                case HarmWaiting.Batch: return false;
                case HarmWaiting.Dose: return Carriers.SoreArm(x, now);
                default: return HarmSix.Due(x, now);
            }
        }

        /// <summary>Part 5's waiting things whose time has passed without them coming.</summary>
        public static bool Lapsed(HarmWaiting x, int now)
        {
            switch (x.what)
            {
                case HarmWaiting.Ward: return now - x.made > HarmFiveMath.WardKeepDays * HarmMath.DayTicks;
                case HarmWaiting.Batch: return now - x.made > HarmFiveMath.BatchKeepDays * HarmMath.DayTicks;
                case HarmWaiting.Dose: return HarmFourMath.OnsetLapsed(x.due, now);
                default: return HarmSix.Lapsed(x, now);
            }
        }

        /// <summary>How long a carrier's cough is kept: the guest's six days, part 5's by their kind.</summary>
        public static float CoughKeepDays(HarmWaiting x) => Carriers.KeepDays(x);
    }

    /// <summary>
    /// A risk named before the choice (harm, part 5): an answer whose choice brings one of part 5's hurts, on its impact
    /// sheet in words, only while the kind's system is on. It does nothing itself. <see cref="risk"/>: "gateSickness" (the
    /// plague's open gate), "patientIll" (a hospital patient who carries something worse), "badBatch" (the healer's paid
    /// doses).
    /// </summary>
    public class DilemmaEffect_HarmRisk : DilemmaEffect
    {
        public string risk;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Harm.On || sheet == null) return;
            switch (risk)
            {
                case "gateSickness":
                    Carriers.DescribeGate(sheet);
                    break;
                case "patientIll":
                    var x = ctx?.stranger;
                    if (x?.health?.hediffSet == null || !Harm.SystemOn(HarmKinds.PatientIll.system)) return;
                    var tb = HarmFour.Def("Council_Ail_Tuberculosis");
                    var cholera = HarmFour.Def("Council_Ail_Cholera");
                    if (!(tb != null && x.health.hediffSet.HasHediff(tb)) && !(cholera != null && x.health.hediffSet.HasHediff(cholera))) return;
                    sheet.Add(ImpactKind.Health, -1, "CC_Impact_PatientIll".Translate(x.LabelShort).Resolve(), "CC_Impact_PatientIllBrief".Translate().Resolve(), "patientill");
                    break;
                case "badBatch":
                    Carriers.DescribeBatch(sheet, ctx?.a);
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
        }
    }
}
