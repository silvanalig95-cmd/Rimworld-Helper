using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 6: slips at the day's work, looked at in each colonist's hour (Harm/HarmSix.cs is the hub), the way tired
    /// hands are (Harm/WorkHurts.cs). Each is seen at the work (the job stops, a load drops, the line, a few words over them)
    /// and on their health tab; a message only for an ailment caught.
    /// <list type="bullet">
    /// <item>The project's last push (1.5.2): in a council project's last three days, work on its own building counts as
    /// hard work, and tired hands' rule holds there: a long day (six hours at it), one roll of one in 150, twice below 30%
    /// rest, half for the tough, half in a spacer colony. Not for one a work decree already drives at it (tired hands' own
    /// roll that day). With the projects.</item>
    /// <item>Clumsy when worn (3.3.2): worn down or near breaking (the heart's two high bands), a long day at any work has
    /// half the tired-hands rate, decree or not, so with a work decree it comes to one and a half. With the heart.</item>
    /// <item>Hurried hands at the harvest (4.4.1): an hour harvesting by hand in a hurry (the hardship's all hands, a field's
    /// frost harvest, the harvest push), one in 200, twice after dark, half for a grower of Plants 8: a sickle's cut, by the
    /// colony's means. With the everyday ailments.</item>
    /// <item>Carries more than they should (3.11.1): the quirk's heavy day (six hours building, mining or hauling) has the
    /// strained back half again as likely (half the strain's own rate, beside it), and one in 150 a dropped load on a foot.
    /// With the quirks, the everyday ailments and "Systems talk to each other".</item>
    /// <item>Sharp habits (3.11.2): a day one who is always whittling, or sharpens every blade, sits down to it: one in ten
    /// a nick, the mildest, about once a season. With the quirks and the small habits.</item>
    /// <item>The second drink (4.7.1): with Alpha Skills' drink before work, three days running of it, then one such day in
    /// five a second cup at the stores (only if their drug policy allows it for fun), the game's own drunkenness after it;
    /// the next hour right at the bench, the stove or the build, one stagger in four slips. The drink before work's
    /// setting off, or the policy, stops it.</item>
    /// </list>
    /// What's counted is session memory, like the ailments' day; the cap is saved (HarmBook).
    /// </summary>
    public static class WorkSlips
    {
        private sealed class Day
        {
            public int day = -1;
            public float work, push, heavy;
            public bool clumsy, pushed, strong, nick;
        }

        private static readonly Dictionary<int, Day> days = new Dictionary<int, Day>();
        /// <summary>A colonist's thingIDNumber: the last day they had a drink before work, and how many days running.</summary>
        private static readonly Dictionary<int, (int day, int streak)> drinks = new Dictionary<int, (int, int)>();
        /// <summary>A colonist's thingIDNumber: when their second cup was poured (the stagger's hours run from it).</summary>
        private static readonly Dictionary<int, int> secondCup = new Dictionary<int, int>();

        /// <summary>Dev mode: the next roll of each comes up, past its rate (never past the cap).</summary>
        public static bool DebugPush, DebugClumsy, DebugHurried, DebugStrong, DebugNick, DebugSecond;

        private static QuirkDef strongBack, whittler, sharpens;
        private static bool looked;

        public static void Reset()
        {
            days.Clear();
            drinks.Clear();
            secondCup.Clear();
            looked = false;
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            strongBack = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_StrongBack");
            whittler = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Whittler");
            sharpens = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SharpensBlades");
        }

        /// <summary>Alpha Skills' drink before work can be had: the second drink's system.</summary>
        public static bool SecondDrinkOn => BeforeWork.Drink.On && BeforeWork.Ready(BeforeWork.Drink);

        private static Day DayOf(Pawn p, int day)
        {
            if (!days.TryGetValue(p.thingIDNumber, out var d))
            {
                if (days.Count > 200) days.Clear();
                days[p.thingIDNumber] = d = new Day();
            }
            if (d.day != day)
            {
                d.day = day;
                d.work = d.push = d.heavy = 0f;
                d.clumsy = d.pushed = d.strong = d.nick = false;
            }
            return d;
        }

        /// <summary>Each colonist's hour (HarmSix.Slice).</summary>
        public static void Hour(HarmBook book, Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p) || !RestUtility.Awake(p) || p.Downed) return;
            Look();
            AnswerHurts.FallHour(book, p, now);
            var d = DayOf(p, now / HarmMath.DayTicks);
            var work = WorkHurts.WorkNow(p);
            bool at = AtIt(p, work);
            if (work != WorkHurtKind.None) d.work += 1f;
            Push(p, d, work, at, now);
            Clumsy(p, d, work, at);
            Hurried(p);
            Strong(p, d, work);
            Nick(p, d);
            Stagger(p, work, at, now);
        }

        /// <summary>Right at the work this moment: carrying the load, or standing at the bench, the stove, the wall or the plants.</summary>
        private static bool AtIt(Pawn p, WorkHurtKind work)
        {
            if (work == WorkHurtKind.None) return false;
            if (work == WorkHurtKind.Load) return p.carryTracker?.CarriedThing != null;
            return !(p.pather?.Moving ?? false);
        }

        private static bool Tough(Pawn p) => p.story?.traits?.allTraits?.Any(t => t.def.defName == "Tough") ?? false;

        private static bool LowRest(Pawn p) => (p.needs?.rest?.CurLevel ?? 1f) < HarmMath.LowRest;

        private static bool Spacer => Ages.Ours == WorldAge.Spacer;

        /// <summary>A work decree drives this work for them today (tired hands has its own roll then).</summary>
        private static bool Driven(Pawn p, WorkHurtKind work)
        {
            var decrees = DecreeTracker.Of(p);
            if (decrees?.active == null) return false;
            foreach (var a in decrees.active)
                if (a?.def != null && a.def.hurts == DecreeHurt.TiredHands && HarmMath.Drives(a.def.HurtsAt, work)) return true;
            return false;
        }

        /// <summary>A slip at the work, by the colony's means (tired hands' own wounds); the job stops, a load drops, a line and a few words.</summary>
        private static bool Slip(Pawn p, WorkHurtKind work, HarmKind kind, string pool, float low, float high)
        {
            HarmMath.WorkSlip(work, Ages.Ours, out var hk, out var where);
            var h = Harm.Injure(p, hk, where, low, high, kind);
            if (h == null) return false;
            if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            HarmFour.Say(p, pool, null, work.ToString().ToLowerInvariant(), hk.ToString().ToLowerInvariant());
            HarmFour.Mote(p, "CC_HarmSix_Mote_Slip_" + work);
            return true;
        }

        // ================= The project's last push (1.5.2) =================

        private static void Push(Pawn p, Day d, WorkHurtKind work, bool at, int now)
        {
            if (work != WorkHurtKind.Heavy || !Harm.SystemOn(HarmKinds.ProjectPush.system)) return;
            var a = AmbitionTracker.For(p.Map)?.active;
            if (a == null || a.map != p.Map || !HarmSixMath.LastPush(a.deadlineTick, now)) return;
            var target = p.CurJob?.targetA.Thing;
            var def = (target as Frame)?.def?.entityDefToBuild as ThingDef ?? (target as Blueprint)?.def?.entityDefToBuild as ThingDef;
            if (def == null || !a.OwnWork(def) || Driven(p, work)) return;
            d.push += 1f;
            bool debug = DebugPush;
            if (!debug && (d.push < HarmMath.LongDayHours || d.pushed || !at)) return;
            d.pushed = true;
            DebugPush = false;
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.ProjectPush, HarmSixMath.ProjectPush(LowRest(p), Tough(p), Spacer), HurtClass.Injury)) return;
            Slip(p, work, HarmKinds.ProjectPush, "Rimfolk_Pool_ProjectPush", HarmMath.WorkLowFor(Spacer), HarmMath.WorkHighFor(Spacer));
        }

        // ================= Clumsy when worn (3.3.2) =================

        private static void Clumsy(Pawn p, Day d, WorkHurtKind work, bool at)
        {
            if (work == WorkHurtKind.None || d.clumsy || !at) return;
            bool debug = DebugClumsy;
            if (!debug && (d.work < HarmMath.LongDayHours || Heart.BandOf(p) < StrainBand.Worn || !Harm.SystemOn(HarmKinds.ClumsyWorn.system))) return;
            d.clumsy = true;
            DebugClumsy = false;
            float chance = HarmSixMath.Clumsy(true, LowRest(p), Tough(p), Spacer);
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.ClumsyWorn, chance, HurtClass.Injury)) return;
            Slip(p, work, HarmKinds.ClumsyWorn, "Rimfolk_Pool_Clumsy", HarmMath.WorkLowFor(Spacer), HarmMath.WorkHighFor(Spacer));
        }

        // ================= Hurried hands at the harvest (4.4.1) =================

        /// <summary>Hurried: a work party bringing a crop in (the hardship's all hands), a plant marked by a frost harvest, or the harvest push.</summary>
        private static bool HurriedNow(Pawn p, Plant plant)
        {
            var play = PlayOutTracker.Instance?.GroupFor(p);
            if (play != null && play.kind == PlayKind.WorkParty && play.tasks != null && play.tasks.Contains(WorkTask.Harvest)) return true;
            if (plant != null && FieldWeather.FrostMarked(plant)) return true;
            var decrees = DecreeTracker.Of(p);
            if (decrees?.active == null) return false;
            foreach (var a in decrees.active)
            {
                var at = a?.def?.HurtsAt;
                if (a?.def != null && a.def.hurts == DecreeHurt.TiredHands && at != null && at.Length > 0 && Array.IndexOf(at, WorkHurtKind.Field) >= 0) return true;
            }
            return false;
        }

        private static void Hurried(Pawn p)
        {
            var job = p.CurJob;
            if (job == null || job.def != JobDefOf.Harvest && job.def != JobDefOf.HarvestDesignated || (p.pather?.Moving ?? false)) return;
            if (!Harm.SystemOn(HarmKinds.HurriedHarvest.system)) return;
            bool debug = DebugHurried;
            bool dark = VisualTouches.Dark(p.Map);
            float chance = HarmSixMath.Hurried(debug || HurriedNow(p, job.targetA.Thing as Plant), dark, p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0);
            if (chance <= 0f) return;
            DebugHurried = false;
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.HurriedHarvest, chance, HurtClass.Injury)) return;
            var h = Harm.Injure(p, HurtKind.Cut, HurtWhere.Hand, HarmSixMath.SickleLow, HarmSixMath.SickleHigh, HarmKinds.HurriedHarvest);
            if (h == null) return;
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            HarmFour.Say(p, "Rimfolk_Pool_Sickle", null, Ages.Tag(Ages.Ours), dark ? "dark" : null);
            HarmFour.Mote(p, "CC_HarmSix_Mote_Sickle_" + HarmFourMath.GamesForm(Ages.Ours));
        }

        // ================= Carries more than they should (3.11.1) =================

        private static void Strong(Pawn p, Day d, WorkHurtKind work)
        {
            if (strongBack == null || !MoreHabits.HasQuirk(p, strongBack)) return;
            if (work == WorkHurtKind.Heavy || work == WorkHurtKind.Load) d.heavy += 1f;
            bool debug = DebugStrong;
            if (p.carryTracker?.CarriedThing == null || !debug && (d.heavy < HarmSixMath.HeavyDayHours || d.strong)) return;
            if (!Harm.SystemOn(HarmKinds.StrongBack.system)) return;
            d.strong = true;
            DebugStrong = false;
            // The strained back half again as likely: half its own rate, on top of the ailment's own roll.
            var strained = HarmFour.Def("Council_Ail_StrainedBack");
            if (strained != null && !p.health.hediffSet.HasHediff(strained) && !debug && Harm.Roll(p, HarmKinds.StrongBack, HarmSixMath.StrongBackStrain, HurtClass.Injury))
            {
                var info = AilmentTracker.InfoOf(strained);
                var h = Harm.Ail(p, strained, info?.startSeverity.RandomInRange ?? 0.4f, HarmKinds.StrongBack, HurtClass.Injury);
                if (h == null) return;
                if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
                if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                HarmFour.Say(p, "Rimfolk_Pool_StrongBack", null, "strain");
                HarmFour.TellHurt(p, "CC_HarmSix_StrongBack", p.Named("PAWN"), AilmentTracker.NameOf(strained).Named("ILLNESS"), (AilmentTracker.DoctorsNote(strained) ?? "").Named("NOTE"));
                return;
            }
            // Now and then the load drops on a foot.
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.StrongBack, HarmSixMath.StrongBackDrop, HurtClass.Injury)) return;
            if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            var b = Harm.Injure(p, HurtKind.Bruise, HurtWhere.Foot, HarmSixMath.DropLow, HarmSixMath.DropHigh, HarmKinds.StrongBack);
            if (b == null) return;
            HarmFour.SitDown(p, 200);
            HarmFour.Say(p, "Rimfolk_Pool_StrongBack", null, "drop");
            HarmFour.Mote(p, "CC_HarmSix_Mote_Dropped");
        }

        // ================= Sharp habits (3.11.2) =================

        private static void Nick(Pawn p, Day d)
        {
            var kind = p.CurJob?.def?.GetModExtension<HabitExtension>()?.kind;
            if (kind != HabitKind.Whittle && kind != HabitKind.Whetstone || d.nick || (p.pather?.Moving ?? false)) return;
            if (!(kind == HabitKind.Whittle ? MoreHabits.HasQuirk(p, whittler) : MoreHabits.HasQuirk(p, sharpens)) || !Harm.SystemOn(HarmKinds.SharpHabits.system)) return;
            d.nick = true;
            bool debug = DebugNick;
            DebugNick = false;
            if (debug ? !Harm.May(p, HurtClass.Mild) : !Harm.Roll(p, HarmKinds.SharpHabits, HarmSixMath.NickChance, HurtClass.Mild)) return;
            var h = Harm.Injure(p, HurtKind.Cut, HurtWhere.Hand, HarmSixMath.NickLow, HarmSixMath.NickHigh, HarmKinds.SharpHabits, HurtClass.Mild);
            if (h == null) return;
            HarmFour.Say(p, "Rimfolk_Pool_Nick", null, kind == HabitKind.Whittle ? "whittle" : "whetstone");
            HarmFour.Mote(p, "CC_HarmSix_Mote_Nick");
        }

        // ================= The second drink (4.7.1) =================

        /// <summary>A drink before work was had (Integrations/BeforeWork.cs): the days running counted; past three, one such day in five a second cup.</summary>
        public static void DrankBefore(Pawn p, Thing drink)
        {
            try
            {
                if (p?.Map == null || drink?.def == null) return;
                int now = Find.TickManager.TicksGame, today = now / HarmMath.DayTicks;
                bool found = drinks.TryGetValue(p.thingIDNumber, out var was);
                int lastDay = found ? was.day : -999, lastStreak = found ? was.streak : 0;
                // One count a day: a second drink before work the same day changes nothing.
                if (lastDay == today) return;
                int before = lastDay == today - 1 ? lastStreak : 0;
                if (drinks.Count > 200) drinks.Clear();
                drinks[p.thingIDNumber] = (today, HarmSixMath.Streak(lastDay, lastStreak, today));
                bool debug = DebugSecond;
                DebugSecond = false;
                if (!Harm.On || !Harm.SystemOn(HarmKinds.SecondDrink.system)) return;
                if (!debug && (!HarmSixMath.SecondCupDue(before) || !Rand.Chance(HarmSixMath.SecondCupChance))) return;
                SecondCup(p, drink.def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: the second drink", e);
            }
        }

        /// <summary>The second cup, at the stores before the work: only if their drug policy allows it for fun.</summary>
        private static void SecondCup(Pawn p, ThingDef def)
        {
            DrugPolicyEntry entry = null;
            try
            {
                entry = p.drugs?.CurrentPolicy?[def];
            }
            catch
            {
                // Not in their policy: it doesn't allow it.
            }
            if (entry == null || !entry.allowedForJoy || p.jobs?.jobQueue == null) return;
            var cup = GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(def), PathEndMode.ClosestTouch, TraverseParms.For(p, Danger.None), 30f,
                t => t.Spawned && !t.IsForbidden(p) && t.IsInAnyStorage() && p.CanReserve(t));
            if (cup == null) return;
            var job = JobMaker.MakeJob(JobDefOf.Ingest, cup);
            job.count = 1;
            p.jobs.jobQueue.EnqueueFirst(job);
            if (secondCup.Count > 100) secondCup.Clear();
            secondCup[p.thingIDNumber] = Find.TickManager.TicksGame;
            // apart: said to nobody, on the way back to the stores.
            HarmFour.Say(p, "Rimfolk_Pool_SecondCup");
        }

        private static void Stagger(Pawn p, WorkHurtKind work, bool at, int now)
        {
            if (secondCup.Count == 0 || !secondCup.TryGetValue(p.thingIDNumber, out int cupAt)) return;
            if (!HarmSixMath.StaggerKept(cupAt, now))
            {
                secondCup.Remove(p.thingIDNumber);
                return;
            }
            if (work != WorkHurtKind.Bench && work != WorkHurtKind.Hot && work != WorkHurtKind.Heavy || !at) return;
            var high = HarmFour.Def("AlcoholHigh");
            if (high == null || !p.health.hediffSet.HasHediff(high)) return;
            secondCup.Remove(p.thingIDNumber);
            if (!Harm.Roll(p, HarmKinds.SecondDrink, HarmSixMath.StaggerChance, HurtClass.Injury)) return;
            Slip(p, work, HarmKinds.SecondDrink, "Rimfolk_Pool_Stagger", HarmSixMath.StaggerLow, HarmSixMath.StaggerHigh);
        }

        /// <summary>Dev mode: as if they had drunk before work three days running; the next drink before work brings the second cup.</summary>
        public static string DebugSecondCup(Pawn p)
        {
            DebugSecond = true;
            int today = Find.TickManager.TicksGame / HarmMath.DayTicks;
            drinks[p.thingIDNumber] = (today - 1, HarmSixMath.DrinkStreak);
            return p.LabelShort + "'s next drink before work brings a second cup at the stores" + (SecondDrinkOn ? ", if their drug policy allows it." : " (needs Alpha Skills' drink before work).");
        }

        /// <summary>Dev mode: a second cup now (as if poured), so the next hour at the bench brings the stagger's roll.</summary>
        public static string DebugStagger(Pawn p)
        {
            secondCup[p.thingIDNumber] = Find.TickManager.TicksGame;
            return p.LabelShort + "'s next hour at the bench, the stove or the build while drunk has the stagger's roll (give them a beer first).";
        }
    }
}
