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
    /// Hurts from the day's work, looked at once an hour per colonist (HarmBook's slices), the way the everyday work
    /// ailments are counted:
    /// <list type="bullet">
    /// <item>Tired hands (the harm review's 1.2.2): under long hours, a harvest push or a building season, a long day
    /// (six hours found at the work the decree drives) has one chance in 150 of a slip at that work, twice below 30% rest,
    /// once more for a second work decree at once, half for the tough, half in a spacer colony (worse when it comes). The
    /// slip is one of the game's own wounds of 3 to 6 at the bench, the stove, the load or the field, by the colony's
    /// means (HarmMath.WorkSlip); the job stops, a load drops, a line is said and the news tells it.</item>
    /// <item>Working through it (3.2.1): a strained back or a sprained ankle, untended and out of bed, worked through four
    /// hours of heavy work in a day turns worse one time in ten: the back tears (Rimfolk's torn back, in its place), or
    /// the bad ankle gives and they fall (a bruise, or a crack one time in three). Never while the "rest when ill" decree
    /// holds. They sit down hard where they are, and a doctor nearby has a word.</item>
    /// </list>
    /// What's counted is session memory, like the ailments' day; the cap is saved (HarmBook).
    /// </summary>
    public static class WorkHurts
    {
        private sealed class Day
        {
            public int day = -1;
            public float hours;
            public bool rolled;
        }

        private static readonly Dictionary<Pawn, Day> days = new Dictionary<Pawn, Day>();
        /// <summary>A strain's or a sprain's hediff loadID: its heavy hours today.</summary>
        private static readonly Dictionary<int, Day> through = new Dictionary<int, Day>();
        /// <summary>The strains and sprains already rolled for once (one roll each).</summary>
        private static readonly HashSet<int> throughRolled = new HashSet<int>();
        private static readonly List<Pawn> slice = new List<Pawn>();

        private static HediffDef strainedBack, sprainedAnkle, tornBack;
        private static DecreeDef sickBay;
        private static bool looked;

        public static void Reset()
        {
            days.Clear();
            through.Clear();
            throughRolled.Clear();
            looked = false;
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            strainedBack = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_StrainedBack");
            sprainedAnkle = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_SprainedAnkle");
            tornBack = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_TornBack");
            sickBay = DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_SickBay");
        }

        /// <summary>One slice of the colony's hour.</summary>
        public static void Slice(int which, int now)
        {
            Look();
            int day = now / GenDate.TicksPerDay;
            slice.Clear();
            var all = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (Sched.SliceOf(all[i].thingIDNumber, Sched.Slices) == which) slice.Add(all[i]);
            for (int i = 0; i < slice.Count; i++)
            {
                var p = slice[i];
                if (!Eligible(p)) continue;
                try
                {
                    Hour(p, day);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm: the work of " + p.LabelShort, e);
                }
            }
            slice.Clear();
            if (which != 0) return;
            // Yesterday's counts are done with; a strain gone (healed, or turned worse) is too.
            foreach (var k in days.Where(kv => kv.Value.day < day || kv.Key.Dead || kv.Key.Destroyed).Select(kv => kv.Key).ToList()) days.Remove(k);
            foreach (var k in through.Where(kv => kv.Value.day < day).Select(kv => kv.Key).ToList()) through.Remove(k);
            if (throughRolled.Count > 200) throughRolled.Clear();
        }

        /// <summary>The colony's own grown-ups and older children at work on a map (the cap keeps children to the mildest).</summary>
        private static bool Eligible(Pawn p) =>
            p != null && p.Spawned && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsQuestLodger()
            && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p) && p.health?.hediffSet != null && RestUtility.Awake(p);

        private static Day DayOf<T>(Dictionary<T, Day> map, T key, int day)
        {
            if (!map.TryGetValue(key, out var d)) map[key] = d = new Day();
            if (d.day != day)
            {
                d.day = day;
                d.hours = 0f;
                d.rolled = false;
            }
            return d;
        }

        /// <summary>What they're at this hour, as a slip would care.</summary>
        public static WorkHurtKind WorkNow(Pawn p)
        {
            var job = p.CurJob;
            if (job == null) return WorkHurtKind.None;
            string work = job.workGiverDef?.workType?.defName;
            bool mining = job.def == JobDefOf.Mine;
            bool hauling = job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer;
            if (work == null && !mining && !hauling) return WorkHurtKind.None;
            return HarmMath.WorkOf(work, mining, hauling);
        }

        private static void Hour(Pawn p, int day)
        {
            var work = WorkNow(p);
            TiredHands(p, work, day);
            Through(p, work == WorkHurtKind.Heavy || work == WorkHurtKind.Load, day);
        }

        // ================= Tired hands =================

        private static void TiredHands(Pawn p, WorkHurtKind work, int day)
        {
            if (work == WorkHurtKind.None) return;
            var decrees = DecreeTracker.Of(p);
            if (decrees == null || decrees.active.Count == 0) return;
            int driving = 0;
            DecreeDef which = null;
            foreach (var a in decrees.active)
            {
                if (a?.def == null || a.def.hurts != DecreeHurt.TiredHands || !HarmMath.Drives(a.def.HurtsAt, work)) continue;
                driving++;
                which ??= a.def;
            }
            if (driving == 0) return;
            var d = DayOf(days, p, day);
            d.hours += 1f;
            if (d.hours < HarmMath.LongDayHours || d.rolled) return;
            d.rolled = true;
            bool lowRest = (p.needs?.rest?.CurLevel ?? 1f) < HarmMath.LowRest;
            bool spacer = Ages.Ours == WorldAge.Spacer;
            float chance = HarmMath.TiredHands(driving, lowRest, Tough(p), spacer);
            if (!Harm.Roll(p, chance, HurtClass.Injury)) return;
            Slip(p, work, which, spacer);
        }

        private static bool Tough(Pawn p)
        {
            var all = p.story?.traits?.allTraits;
            if (all == null) return false;
            for (int i = 0; i < all.Count; i++)
                if (all[i].def.defName == "Tough") return true;
            return false;
        }

        private static void Slip(Pawn p, WorkHurtKind work, DecreeDef decree, bool spacer)
        {
            HarmMath.WorkSlip(work, Ages.Ours, out var kind, out var where);
            var h = Harm.Injure(p, kind, where, HarmMath.WorkLowFor(spacer), HarmMath.WorkHighFor(spacer), "tired hands: " + decree.defName);
            if (h == null) return;
            Stop(p, false);
            Say(p, "Rimfolk_Pool_WorkSlip", work.ToString().ToLowerInvariant(), kind.ToString().ToLowerInvariant());
            string part = h.Part?.Label ?? "";
            string text = TextVariants.Vary("CC_Harm_Slip_" + work).Translate(p.Named("PAWN"), part.Named("PART"), decree.label.Named("DECREE")).Resolve();
            Tell.Message(NewsKind.Health, Importance.Notable, p.Map, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }

        // ================= Working through it =================

        private static void Through(Pawn p, bool heavyNow, int day)
        {
            if (strainedBack == null && sprainedAnkle == null) return;
            if (!AilmentTracker.On) return;
            var hs = p.health.hediffSet.hediffs;
            Hediff found = null;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h.def != strainedBack && h.def != sprainedAnkle) continue;
                bool tended = h.IsTended();
                bool inBed = p.InBed();
                var t = DayOf(through, h.loadID, day);
                if (heavyNow && !tended && !inBed) t.hours += 1f;
                if (!HarmMath.WorkedThrough(t.hours, tended, inBed) || !heavyNow || !throughRolled.Add(h.loadID)) continue;
                found = h;
                break;
            }
            if (found == null) return;
            // Told to bed while ill or hurt: the council already sent them to rest.
            if (sickBay != null && DecreeTracker.InForceFor(p, sickBay)) return;
            if (!Harm.Roll(p, HarmMath.WorkedThroughChance, HurtClass.Injury)) return;
            if (found.def == strainedBack) TornBack(p, found);
            else AnkleGives(p, found);
        }

        private static void TornBack(Pawn p, Hediff strained)
        {
            if (tornBack == null) return;
            var torn = Harm.Ail(p, tornBack, Rand.Range(0.55f, 0.8f), "worked through a strained back", HurtClass.Injury, strained);
            if (torn == null) return;
            Stop(p, true);
            Say(p, "Rimfolk_Pool_WorkedThrough", "back");
            DoctorsWord(p);
            string text = TextVariants.Vary("CC_Harm_TornBack").Translate(p.Named("PAWN")).Resolve();
            string note = AilmentTracker.DoctorsNote(tornBack);
            if (!note.NullOrEmpty()) text += " " + note;
            Tell.Message(NewsKind.Health, Importance.Notable, p.Map, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }

        private static void AnkleGives(Pawn p, Hediff sprain)
        {
            bool crack = Rand.Chance(HarmMath.CrackShare);
            var h = crack
                ? Harm.Injure(p, HurtKind.Crack, HurtWhere.Leg, HarmMath.FallCrackLow, HarmMath.FallCrackHigh, "worked through a sprained ankle")
                : null;
            if (h == null)
            {
                crack = false;
                h = Harm.Injure(p, HurtKind.Bruise, HurtWhere.Leg, HarmMath.FallBruiseLow, HarmMath.FallBruiseHigh, "worked through a sprained ankle");
            }
            if (h == null) return;
            Stop(p, true);
            Say(p, "Rimfolk_Pool_WorkedThrough", "ankle");
            DoctorsWord(p);
            string text = TextVariants.Vary(crack ? "CC_Harm_AnkleCrack" : "CC_Harm_AnkleFall").Translate(p.Named("PAWN"), (h.Part?.Label ?? "").Named("PART")).Resolve();
            string note = AilmentTracker.DoctorsNote(sprain.def);
            if (!note.NullOrEmpty()) text += " " + note;
            Tell.Message(NewsKind.Health, Importance.Notable, p.Map, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>The colony's best doctor nearby, if they're together, says to rest it this time.</summary>
        private static void DoctorsWord(Pawn p)
        {
            Pawn best = null;
            int bestLevel = -1;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || o.DevelopmentalStage.Juvenile() || o.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || !Together.Can(o, p)) continue;
                int level = o.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                if (level > bestLevel)
                {
                    best = o;
                    bestLevel = level;
                }
            }
            // together: Together.Can holds for the two (the doctor within talking range, both up and about).
            if (best != null) Say(best, "Rimfolk_Pool_DoctorsWord", null, null, p);
        }

        // ================= On the map =================

        /// <summary>The work stops where it was: a load carried drops at their feet; after a fall, they sit down hard a moment.</summary>
        private static void Stop(Pawn p, bool sitDown)
        {
            try
            {
                if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                if (!sitDown)
                {
                    p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    return;
                }
                var wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                wait.expiryInterval = 300;
                p.jobs?.StartJob(wait, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: stopping work", e);
            }
        }

        private static readonly Color SayColor = new Color(1f, 0.7f, 0.6f);

        /// <summary>A line from a pool over their head, tagged for what happened (the work, the wound, the back or the ankle).</summary>
        private static void Say(Pawn p, string pool, string tag, string tag2 = null, Pawn about = null)
        {
            try
            {
                string line = Flavor.FromPool(pool, p, null, null, tag, tag2);
                if (line.NullOrEmpty()) return;
                // together: a line to someone else is only asked for when the two are together (DoctorsWord checks Together.Can).
                var args = about != null ? new[] { p.Named("PAWN"), about.Named("OTHER") } : new[] { p.Named("PAWN") };
                CouncilGameComponent.Speak(p, line.Formatted(args).Resolve(), SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a line from " + pool, e);
            }
        }
    }
}
