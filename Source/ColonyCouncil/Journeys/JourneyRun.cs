using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A journey plan playing out (the numbers are JourneyMath.cs, the goods and the way out JourneyPack.cs). A day
    /// before, each traveller's timetable is shifted for the night before (to bed earlier or later so they wake rested in
    /// time, recreation topped up before it, the last hours free). In the last hours they put work aside and, one step at
    /// a time when nothing more pressing calls them: change into clothes for the road's weather, pick up their share of
    /// the packing list, have a meal, use the bathroom and wash (with Dubs Bad Hygiene), and go to the gate to wait. At
    /// the hour, if everyone is ready, the game's own caravan forming takes exactly them, their animals and the rest of
    /// the goods, loads up at the gate and leaves by the edge toward the destination, and those at home see them off. If
    /// someone isn't ready, a letter asks: wait an hour, leave without them, or call it off.
    /// </summary>
    public static class JourneyRun
    {
        public const string Pool = "Rimfolk_Pool_JourneySays";
        private const int HourTicks = GenDate.TicksPerHour;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static int FreeHours => Mathf.Clamp(S?.journeyFreeHours ?? 4, 1, 12);

        private static int NowAbs => Find.TickManager.TicksAbs;

        private static float Longitude(Map map) => map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;

        /// <summary>The start of this hour, in absolute ticks (the game's hours of the day begin on these ticks).</summary>
        public static int HourStart(int abs) => abs - Sched.Mod(abs, HourTicks);

        public static int HourOfDay(int abs, Map map) => GenDate.HourOfDay(abs, Longitude(map));

        /// <summary>A time of day and its date, as the plan and its messages say it ("6h on 5 Aprimay").</summary>
        public static string When(int abs, Map map)
        {
            var at = map != null ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero;
            return "CC_Journey_When".Translate(GenDate.HourOfDay(abs, at.x), GenDate.DateShortStringAt(abs, at)).Resolve();
        }

        public static string When(JourneyPlan plan) => When(plan.departAbs, plan.map);

        /// <summary>The travellers by name, and their animals as a number.</summary>
        public static string Who(JourneyPlan plan)
        {
            var names = plan.travellers.Where(p => p != null).Select(p => p.LabelShort).ToList();
            int beasts = plan.animals.Count(a => a != null);
            if (beasts > 0) names.Add(beasts == 1 ? "CC_Journey_OneAnimal".Translate().ToString() : "CC_Journey_Animals".Translate(beasts).ToString());
            return TextUtil.JoinAnd(names);
        }

        // ---------------- The pass ----------------

        /// <summary>Every 250 ticks while there is a plan: the stage the hour has reached, each traveller's next step, the hour itself.</summary>
        public static void Rare(JourneyBook book, JourneyPlan plan)
        {
            if (!JourneyBook.On)
            {
                CallOff(plan, "CC_Journey_CalledOffSetting");
                return;
            }
            var map = plan.map;
            // Its home no longer ours (abandoned, or handed over): nobody sets off from it.
            if (map == null || !Find.Maps.Contains(map) || !Homes.IsHome(map))
            {
                CallOff(plan, "CC_Journey_CalledOff");
                return;
            }
            int now = NowAbs;
            if (plan.stage == JourneyStage.Planned && now >= plan.departAbs - JourneyMath.PrepareHours * HourTicks) BeginPreparing(plan);
            if (plan.stage == JourneyStage.Preparing && now >= plan.departAbs - FreeHours * HourTicks) BeginFinal(plan);
            if (plan.stage < JourneyStage.Final) return;
            foreach (var p in plan.travellers.ToList()) Step(plan, p, now);
            if (plan.stage == JourneyStage.Final && now >= plan.departAbs) Hour(plan);
            else if (plan.stage == JourneyStage.Asking && Problems(plan).Count == 0)
            {
                // Whoever wasn't ready is now: they set off as the player ordered, and the question goes.
                RemoveLetters(plan);
                Notify.Message("CC_Journey_ReadyNow".Translate(plan.Dest).Resolve(), new LookTargets(plan.travellers), MessageTypeDefOf.PositiveEvent, Importance.Important);
                Depart(plan, null);
            }
        }

        // ---------------- The day before ----------------

        private static JourneyMath.Slot SlotOf(TimeAssignmentDef d)
        {
            if (d == TimeAssignmentDefOf.Sleep) return JourneyMath.Slot.Sleep;
            if (d == TimeAssignmentDefOf.Work) return JourneyMath.Slot.Work;
            if (d == TimeAssignmentDefOf.Joy) return JourneyMath.Slot.Joy;
            if (d == TimeAssignmentDefOf.Anything) return JourneyMath.Slot.Anything;
            return JourneyMath.Slot.Other;
        }

        private static TimeAssignmentDef DefOf(JourneyMath.Slot s)
        {
            switch (s)
            {
                case JourneyMath.Slot.Sleep: return TimeAssignmentDefOf.Sleep;
                case JourneyMath.Slot.Work: return TimeAssignmentDefOf.Work;
                case JourneyMath.Slot.Joy: return TimeAssignmentDefOf.Joy;
                case JourneyMath.Slot.Anything: return TimeAssignmentDefOf.Anything;
                default: return null;
            }
        }

        /// <summary>One traveller's last day as the plan would lay it out from now (their own timetable for the hours to go).</summary>
        public static JourneyMath.NightPlan Night(Pawn p, int departAbs, Map map, out int hourNow)
        {
            int now = NowAbs;
            hourNow = HourOfDay(now, map);
            int n = Mathf.Clamp((departAbs - HourStart(now)) / HourTicks, 0, JourneyMath.PrepareHours);
            var original = new JourneyMath.Slot[n];
            for (int i = 0; i < n; i++) original[i] = p.timetable != null ? SlotOf(p.timetable.GetAssignment((hourNow + i) % 24)) : JourneyMath.Slot.Anything;
            var rest = p.needs?.rest;
            var joy = p.needs?.joy;
            float bed = p.ownership?.OwnedBed != null ? p.ownership.OwnedBed.GetStatValue(StatDefOf.BedRestEffectiveness) : 0.8f;
            float gain = Need_Rest.BaseRestGainPerTick * HourTicks * bed * p.GetStatValue(StatDefOf.RestRateMultiplier);
            float fall = rest != null ? rest.RestFallPerTick * HourTicks : 0.04f;
            float joyFall = joy?.def != null && joy.def.fallPerDay > 0f ? joy.def.fallPerDay / 24f : 0.025f;
            return JourneyMath.PlanNight(original, FreeHours, rest?.CurLevel ?? 1f, fall, gain, joy?.CurLevel ?? 1f, joyFall, 0.3f,
                (S?.journeyShiftSleep ?? true) && rest != null && p.timetable != null);
        }

        /// <summary>A day before: each traveller's timetable for the hours to go, the night shifted and the last hours free.</summary>
        public static void BeginPreparing(JourneyPlan plan)
        {
            plan.stage = JourneyStage.Preparing;
            foreach (var p in plan.travellers.Where(p => p?.timetable != null && !p.Dead))
            {
                var night = Night(p, plan.departAbs, plan.map, out int hourNow);
                for (int i = 0; i < night.slots.Length; i++)
                {
                    var def = DefOf(night.slots[i]);
                    if (def != null && SlotOf(p.timetable.GetAssignment((hourNow + i) % 24)) != night.slots[i]) SetSlot(plan, p, (hourNow + i) % 24, def);
                }
            }
            JourneyBook.Touch();
        }

        private static void SetSlot(JourneyPlan plan, Pawn p, int hour, TimeAssignmentDef def)
        {
            var cur = p.timetable.GetAssignment(hour);
            if (cur == def) return;
            var rec = plan.slots.FirstOrDefault(s => s.pawn == p && s.hour == hour);
            if (rec == null) plan.slots.Add(new JourneySlot { pawn = p, hour = hour, was = cur, set = def });
            else rec.set = def;
            p.timetable.SetAssignment(hour, def);
        }

        /// <summary>Their own timetable back (only the hours still as the plan set them), for one traveller or all.</summary>
        public static void Restore(JourneyPlan plan, Pawn only = null)
        {
            foreach (var s in plan.slots.Where(s => only == null || s.pawn == only).ToList())
            {
                if (s.pawn?.timetable != null && s.pawn.timetable.GetAssignment(s.hour) == s.set) s.pawn.timetable.SetAssignment(s.hour, s.was);
                plan.slots.Remove(s);
            }
        }

        // ---------------- The last hours ----------------

        /// <summary>The last hours start: where they gather and leave by, each one's share and clothes, and word to the player.</summary>
        public static void BeginFinal(JourneyPlan plan)
        {
            plan.stage = JourneyStage.Final;
            plan.exit = JourneyPack.Exit(plan);
            plan.gate = JourneyPack.Gate(plan, plan.exit);
            plan.shares = JourneyPack.Shares(plan);
            plan.clothes = JourneyPack.Clothes(plan);
            JourneyBook.Touch();
            Notify.Message("CC_Journey_GettingReady".Translate(Who(plan), plan.Dest, When(plan)).Resolve(), new LookTargets(plan.travellers.Where(p => p != null && p.Spawned)),
                MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        private static readonly JobGiver_GetFood GetFood = new JobGiver_GetFood();

        /// <summary>Steps whose job wouldn't start, by traveller and step (not saved: a reload gives them another few tries).</summary>
        private static readonly Dictionary<int, int> tries = new Dictionary<int, int>();

        private const int MaxTries = 4;

        /// <summary>Free for their next step: at home, up, not hurt or needed elsewhere, and doing nothing that matters more.</summary>
        private static bool Free(JourneyPlan plan, Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map != plan.map || p.Downed || p.Drafted || p.InMentalState || !p.Awake()) return false;
            if (p.GetLord() != null) return false;
            // Wounds are seen to first: the game takes them to bed and a doctor tends them.
            if (p.health.HasHediffsNeedingTend() || HealthAIUtility.ShouldSeekMedicalRestUrgent(p)) return false;
            if (plan.issued.TryGetValue(p.thingIDNumber, out var job) && p.CurJob == job) return false;
            return p.CurJob == null || PlayOuts.Interruptible(p);
        }

        /// <summary>A traveller's next step of the last hours, when it is open and they are free for it.</summary>
        private static void Step(JourneyPlan plan, Pawn p, int now)
        {
            if (!Free(plan, p)) return;
            float hoursLeft = (plan.departAbs - now) / (float)HourTicks;
            int free = FreeHours;
            foreach (JourneyMath.Step s in Enum.GetValues(typeof(JourneyMath.Step)))
            {
                if (s != JourneyMath.Step.Gather && plan.Done(p, s)) continue;
                // This step isn't open yet, nor the ones after it.
                if (hoursLeft > JourneyMath.Opens(s, free)) return;
                Job job;
                try
                {
                    job = StepJob(plan, p, s, out bool finished);
                    if (finished)
                    {
                        if (s != JourneyMath.Step.Gather) plan.MarkDone(p, s);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journeys: step " + s, e);
                    plan.MarkDone(p, s);
                    continue;
                }
                if (job == null) return;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (p.CurJob != job)
                {
                    // A job that won't start (something reserved it, the way is shut): a few tries, then the step is let go.
                    int key = p.thingIDNumber * 8 + (int)s;
                    tries[key] = (tries.TryGetValue(key, out int n) ? n : 0) + 1;
                    if (tries[key] >= MaxTries && s != JourneyMath.Step.Gather) plan.MarkDone(p, s);
                    return;
                }
                plan.issued[p.thingIDNumber] = job;
                // A meal, the bathroom and a wash are once each; dressing and packing go on to the next piece.
                if (s == JourneyMath.Step.Meal || s == JourneyMath.Step.Toilet || s == JourneyMath.Step.Wash) plan.MarkDone(p, s);
                return;
            }
        }

        /// <summary>The job for a step, or <paramref name="finished"/> when there is nothing (more) to do for it.</summary>
        private static Job StepJob(JourneyPlan plan, Pawn p, JourneyMath.Step s, out bool finished)
        {
            finished = false;
            switch (s)
            {
                case JourneyMath.Step.Dress:
                {
                    plan.clothes ??= JourneyPack.Clothes(plan);
                    var next = plan.clothes.TryGetValue(p, out var picks)
                        ? picks.FirstOrDefault(a => a != null && !a.Destroyed && a.Spawned && a.Wearer == null && !a.IsForbidden(p) && p.CanReserveAndReach(a, PathEndMode.ClosestTouch, Danger.Some))
                        : null;
                    if (next != null) return JobMaker.MakeJob(JobDefOf.Wear, next);
                    // Dressed: one who has no weapon takes up one the list brings for them.
                    var arm = ArmJob(plan, p);
                    finished = arm == null;
                    return arm;
                }
                case JourneyMath.Step.Pack:
                {
                    plan.shares ??= JourneyPack.Shares(plan);
                    if (plan.shares.TryGetValue(p, out var share))
                        foreach (var kv in share)
                        {
                            int want = kv.Value - JourneyPack.Carried(p, kv.Key);
                            if (want <= 0) continue;
                            var thing = Nearest(p, kv.Key);
                            if (thing == null) continue;
                            var job = JobMaker.MakeJob(JobDefOf.TakeInventory, thing);
                            job.count = Mathf.Min(want, thing.stackCount);
                            return job;
                        }
                    // What the stores don't hold of the list, off the colony's stalls (RimMarket).
                    var stall = StallJob(plan, p);
                    finished = stall == null;
                    return stall;
                }
                case JourneyMath.Step.Meal:
                {
                    var food = p.needs?.food;
                    if (food == null || food.CurLevelPercentage >= JourneyMath.MealBelow)
                    {
                        finished = true;
                        return null;
                    }
                    var job = GetFood.TryIssueJobPackage(p, default).Job;
                    finished = job == null;
                    return job;
                }
                case JourneyMath.Step.Toilet:
                {
                    var job = JourneyPack.HasBladder(p) && p.needs.TryGetNeed(Hygiene.BladderNeed).CurLevelPercentage < 0.9f ? JourneyPack.ToiletJob(p) : null;
                    finished = job == null;
                    return job;
                }
                case JourneyMath.Step.Wash:
                {
                    var job = JourneyPack.HasHygiene(p) && p.needs.TryGetNeed(Hygiene.HygieneNeed).CurLevelPercentage < 0.9f ? JourneyPack.WashJob(p) : null;
                    finished = job == null;
                    return job;
                }
                default:
                {
                    if (!plan.gate.IsValid || p.CurJobDef == JourneyDefOf.Rimfolk_JourneyWait)
                    {
                        finished = true;
                        return null;
                    }
                    var cell = JourneyPack.StandNear(p, plan.gate);
                    if (!cell.IsValid)
                    {
                        finished = true;
                        return null;
                    }
                    return JobMaker.MakeJob(JourneyDefOf.Rimfolk_JourneyWait, cell, plan.gate);
                }
            }
        }

        // ---------------- Weapons for the unarmed, goods off a stall ----------------

        private static string ArmKey(Pawn p, ThingDef def) => p.ThingID + ":Arm:" + def.defName;

        /// <summary>How many of the travellers took up a weapon of this kind from the list (the list's "arm" lines count them as packed).</summary>
        public static int Armed(JourneyPlan plan, List<Pawn> going, ThingDef def) =>
            def == null ? 0 : going.Count(p => p != null && p.equipment?.Primary?.def == def && plan.done.Contains(ArmKey(p, def)));

        /// <summary>One who has no weapon (and will use one) takes up the nearest of a weapon the list brings for the unarmed, while one is still wanted.</summary>
        private static Job ArmJob(JourneyPlan plan, Pawn p)
        {
            if (p.equipment == null || p.equipment.Primary != null || p.WorkTagIsDisabled(WorkTags.Violent)) return null;
            foreach (var line in plan.packing.Where(l => l.Taken && l.kind == "arm"))
            {
                if (Armed(plan, plan.travellers, line.def) >= line.count) continue;
                var weapon = GenClosest.ClosestThing_Global_Reachable(p.Position, p.Map, p.Map.listerThings.ThingsOfDef(line.def).Where(t => t.Spawned && t.IsInAnyStorage()),
                    PathEndMode.ClosestTouch, TraverseParms.For(p), 9999f, t => !t.IsForbidden(p) && p.CanReserve(t) && EquipmentUtility.CanEquip(t, p));
                if (weapon == null) continue;
                if (!plan.done.Contains(ArmKey(p, line.def))) plan.done.Add(ArmKey(p, line.def));
                return JobMaker.MakeJob(JobDefOf.Equip, weapon);
            }
            return null;
        }

        /// <summary>
        /// The next goods this traveller takes off a colony stall: of a line the stores and the travellers' packs don't
        /// cover, less what others are fetching now, from the nearest stall that has it (JourneyAdviceMath.TakeFrom: the
        /// stores first). As much as they have room for, or all that is left when nobody has room.
        /// </summary>
        private static Job StallJob(JourneyPlan plan, Pawn p)
        {
            var def = JourneyDefOf.Rimfolk_JourneyStall;
            if (def == null || !RimMarketStall.JourneyReady) return null;
            var map = plan.map;
            var stalls = RimMarketStall.JourneyStalls(map);
            if (stalls.Count == 0) return null;
            plan.stallTaking.RemoveAll(t => t.pawn == null || t.pawn.CurJob != t.job);
            foreach (var line in plan.packing.Where(l => l.Taken))
            {
                int fetching = plan.stallTaking.Where(t => t.def == line.def).Sum(t => t.job.count);
                int want = line.count - plan.travellers.Sum(x => JourneyPack.Carried(x, line.def)) - (line.kind == "arm" ? Armed(plan, plan.travellers, line.def) : 0) - fetching;
                if (want <= 0) continue;
                var holding = stalls.Where(st => RimMarketStall.OnStall(st, line.def) > 0).OrderBy(st => st.Position.DistanceToSquared(p.Position)).ToList();
                if (holding.Count == 0) continue;
                var (_, fromStalls, _) = JourneyAdviceMath.TakeFrom(want, JourneyPack.InStores(map, line.def), holding.Select(st => RimMarketStall.OnStall(st, line.def)).ToList());
                int i = Array.FindIndex(fromStalls, c => c > 0);
                if (i < 0) continue;
                float unit = Mathf.Max(0.001f, JourneyPack.UnitMass(line.def));
                int room = Mathf.FloorToInt(Mathf.Max(0f, MassUtility.FreeSpace(p)) / unit);
                int count = fromStalls[i];
                if (room < count)
                {
                    bool others = plan.travellers.Any(o => o != null && o != p && o.Spawned && o.Map == map && MassUtility.FreeSpace(o) >= unit);
                    if (room <= 0 && others) continue;
                    if (room > 0) count = room;
                }
                if (!p.CanReach(holding[i], PathEndMode.Touch, Danger.Some)) continue;
                var job = JobMaker.MakeJob(def, holding[i]);
                job.thingDefToCarry = line.def;
                job.count = count;
                plan.stallTaking.Add(new JourneyPlan.StallTake { pawn = p, def = line.def, job = job });
                return job;
            }
            return null;
        }

        /// <summary>A traveller took goods off a stall (JobDriver_JourneyStall): they're in their pack now, and no longer being fetched.</summary>
        public static void TookFromStall(Pawn p, Thing stall, ThingDef def, int count)
        {
            foreach (var plan in JourneyBook.Plans)
                plan.stallTaking.RemoveAll(t => t.pawn == p);
            if (count > 0) JourneyBook.Touch();
        }

        /// <summary>The nearest of a line's things in the stores a traveller can pick up.</summary>
        private static Thing Nearest(Pawn p, ThingDef def)
        {
            var map = p.Map;
            IEnumerable<Thing> things = def.Minifiable ? map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing) : map.listerThings.ThingsOfDef(def);
            return GenClosest.ClosestThing_Global_Reachable(p.Position, map, things.Where(t => JourneyPack.Matches(t, def) && t.IsInAnyStorage()), PathEndMode.ClosestTouch,
                TraverseParms.For(p), 9999f, t => !t.IsForbidden(p) && p.CanReserve(t));
        }

        /// <summary>Someone arrived at the gate: now and then a word about being ready to go.</summary>
        public static void AtGate(Pawn p)
        {
            var plan = JourneyBook.PlanOf(p);
            if (plan == null || !plan.Has(p) || plan.Done(p, JourneyMath.Step.Gather) || !Speech.On) return;
            plan.MarkDone(p, JourneyMath.Step.Gather);
            if (!Rand.Chance(0.35f)) return;
            Say(p, "gate");
        }

        /// <summary>One person's line for a moment of the journey, over their own head (Rimfolk_Pool_JourneySays).</summary>
        private static void Say(Pawn p, string moment)
        {
            if (p == null || !Speech.On) return;
            string line = Flavor.FromPool(Pool, p, null, null, moment);
            Speech.Say(p, line);
        }

        // ---------------- The hour ----------------

        /// <summary>Why someone isn't ready to set off now, or null.</summary>
        public static string NotReady(Pawn p, Map map, bool animal)
        {
            if (p == null || (p.Destroyed && !p.Dead)) return "CC_Journey_NR_Gone";
            if (p.Dead) return "CC_Journey_NR_Dead";
            if (p.Faction != Faction.OfPlayer || p.IsPrisoner) return "CC_Journey_NR_Gone";
            if (!p.Spawned || p.Map != map) return "CC_Journey_NR_Away";
            if (p.Downed) return "CC_Journey_NR_Downed";
            if (p.InMentalState) return "CC_Journey_NR_Mental";
            if (!animal && p.health.HasHediffsNeedingTend()) return "CC_Journey_NR_Untended";
            if (p.Drafted) return "CC_Journey_NR_Drafted";
            var lord = p.GetLord();
            if (lord != null && !(lord.LordJob is LordJob_FormAndSendCaravan)) return "CC_Journey_NR_Busy";
            return null;
        }

        /// <summary>What keeps the journey from setting off now, in words: who isn't ready and why, and danger on the map.</summary>
        public static List<string> Problems(JourneyPlan plan)
        {
            var list = new List<string>();
            var map = plan.map;
            foreach (var p in plan.travellers)
            {
                string why = NotReady(p, map, false);
                if (why != null) list.Add(why.Translate(p?.LabelShort ?? "?").Resolve());
            }
            foreach (var a in plan.animals)
            {
                string why = NotReady(a, map, true);
                if (why != null) list.Add(why.Translate(a?.LabelShort ?? "?").Resolve());
            }
            if (map != null && (map.dangerWatcher.DangerRating != StoryDanger.None || MapFacts.ThreatOn(map))) list.Add("CC_Journey_NR_Danger".Translate().Resolve());
            return list;
        }

        public static bool AnyReady(JourneyPlan plan) => plan != null && plan.travellers.Any(p => NotReady(p, plan.map, false) == null);

        /// <summary>Someone left behind is a traveller or animal who isn't ready (danger on the map leaves nobody behind).</summary>
        public static bool AnyNotReady(JourneyPlan plan) =>
            plan != null && plan.travellers.Any(p => NotReady(p, plan.map, false) != null) || plan.animals.Any(a => NotReady(a, plan.map, true) != null);

        /// <summary>The hour has come: off they go if everyone is ready, else the player is asked.</summary>
        private static void Hour(JourneyPlan plan)
        {
            var problems = Problems(plan);
            if (problems.Count == 0)
            {
                Depart(plan, null);
                return;
            }
            plan.stage = JourneyStage.Asking;
            JourneyBook.Touch();
            string list = string.Join("\n", problems.Select(x => "  - " + x));
            string key = AnyNotReady(plan) ? "CC_Journey_AskText" : "CC_Journey_AskTextDanger";
            var letter = LetterMaker.MakeLetter("CC_Journey_AskTitle".Translate(), key.Translate(When(plan), plan.Dest, list).Resolve(), JourneyDefOf.Rimfolk_JourneyLetter,
                new LookTargets(plan.travellers.Where(p => p != null && p.Spawned)));
            if (letter is ChoiceLetter_Journey jl) jl.planId = plan.id;
            Tell.Ask(letter);
        }

        private static void RemoveLetters(JourneyPlan plan)
        {
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Journey>().Where(l => l.planId == plan.id).ToList())
                Find.LetterStack.RemoveLetter(l);
        }

        /// <summary>Wait an hour at the gate: the departure moves on an hour, and that hour is free too.</summary>
        public static void WaitAnHour(JourneyPlan plan)
        {
            plan.departAbs = HourStart(Mathf.Max(plan.departAbs, NowAbs)) + JourneyMath.WaitHours * HourTicks;
            plan.waited++;
            plan.stage = JourneyStage.Final;
            int hour = HourOfDay(plan.departAbs - HourTicks, plan.map);
            foreach (var p in plan.travellers.Where(p => p?.timetable != null && !p.Dead)) SetSlot(plan, p, hour, TimeAssignmentDefOf.Anything);
            JourneyBook.Touch();
            Notify.Message("CC_Journey_Waiting".Translate(plan.Dest, When(plan)).Resolve(), new LookTargets(plan.travellers.Where(p => p != null && p.Spawned)),
                MessageTypeDefOf.NeutralEvent, Importance.Important);
            var waiting = plan.travellers.Where(p => p != null && p.Spawned && p.CurJobDef == JourneyDefOf.Rimfolk_JourneyWait).RandomElementWithFallback();
            if (waiting != null) Say(waiting, "wait");
        }

        /// <summary>Leave without whoever isn't ready (or set off in spite of danger).</summary>
        public static void LeaveWithout(JourneyPlan plan)
        {
            var map = plan.map;
            var left = plan.travellers.Where(p => NotReady(p, map, false) != null).ToList();
            var leftBeasts = plan.animals.Where(a => NotReady(a, map, true) != null).ToList();
            foreach (var p in left)
            {
                Restore(plan, p);
                if (p != null && p.Spawned && p.Map == map && !p.Dead && p.inventory != null && plan.packing.Any(l => JourneyPack.Carried(p, l.def) > 0)) p.inventory.UnloadEverything = true;
            }
            plan.travellers.RemoveAll(left.Contains);
            plan.animals.RemoveAll(leftBeasts.Contains);
            plan.Changed();
            if (plan.travellers.Count == 0)
            {
                CallOff(plan, "CC_Journey_CalledOff");
                return;
            }
            Depart(plan, left.Concat(leftBeasts).Where(p => p != null).ToList());
        }

        /// <summary>The journey called off: timetables back, packs put away, and word to the player.</summary>
        public static void CallOff(JourneyPlan plan, string key)
        {
            var book = JourneyBook.Instance;
            Restore(plan);
            RemoveLetters(plan);
            foreach (var p in plan.travellers)
                if (p != null && p.Spawned && !p.Dead && p.inventory != null && plan.packing.Any(l => JourneyPack.Carried(p, l.def) > 0))
                    p.inventory.UnloadEverything = true;
            book?.Remove(plan);
            JourneyBook.Touch();
            Notify.Message(key.Translate(plan.Dest).Resolve(), new LookTargets(plan.travellers.Where(p => p != null && p.Spawned)), MessageTypeDefOf.NeutralEvent,
                Importance.Important);
        }

        /// <summary>
        /// Off they go: the game's own caravan forming with exactly the travellers who are ready, their animals and the
        /// rest of the goods, gathering at the gate and leaving by the edge toward the destination. The plan ends here.
        /// </summary>
        public static void Depart(JourneyPlan plan, List<Pawn> leftBehind)
        {
            var map = plan.map;
            var book = JourneyBook.Instance;
            var going = plan.travellers.Where(p => NotReady(p, map, false) == null).ToList();
            var beasts = plan.animals.Where(a => NotReady(a, map, true) == null).ToList();
            Restore(plan);
            RemoveLetters(plan);
            if (going.Count == 0)
            {
                CallOff(plan, "CC_Journey_CalledOff");
                return;
            }
            if (!plan.exit.IsValid || !plan.exit.InBounds(map) || !plan.exit.Walkable(map)) plan.exit = JourneyPack.Exit(plan);
            if (!plan.exit.IsValid)
            {
                CallOff(plan, "CC_Journey_NoWayOut");
                return;
            }
            if (!plan.gate.IsValid) plan.gate = JourneyPack.Gate(plan, plan.exit);
            var shortOf = new List<string>();
            bool forming = false;
            try
            {
                var goods = JourneyPack.Missing(plan, going, shortOf);
                var start = JourneyPack.StartTile(plan);
                var dest = plan.HasDestination ? plan.DestTile : PlanetTile.Invalid;
                CaravanFormingUtility.StartFormingCaravan(going.Concat(beasts).ToList(), new List<Pawn>(), Faction.OfPlayer, goods, plan.gate, plan.exit, start, dest);
                forming = going.Any(p => p.GetLord()?.LordJob is LordJob_FormAndSendCaravan);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: setting off", e);
            }
            book?.Remove(plan);
            JourneyBook.Touch();
            if (!forming)
            {
                foreach (var p in going)
                    if (p.inventory != null && plan.packing.Any(l => JourneyPack.Carried(p, l.def) > 0))
                        p.inventory.UnloadEverything = true;
                Notify.Message("CC_Journey_CouldNotForm".Translate(plan.Dest).Resolve(), new LookTargets(going), MessageTypeDefOf.NegativeEvent, Importance.Important);
                return;
            }
            string text = "CC_Journey_SetOff".Translate(Who(plan), plan.Dest).Resolve();
            if (!leftBehind.NullOrEmpty()) text += " " + "CC_Journey_Without".Translate(TextUtil.JoinAnd(leftBehind.Select(p => p.LabelShort).ToList())).Resolve();
            if (shortOf.Count > 0) text += " " + "CC_Journey_ShortOf".Translate(TextUtil.JoinAnd(shortOf)).Resolve();
            Notify.Message(text, new LookTargets(going), MessageTypeDefOf.PositiveEvent, Importance.Important);
            // A founding party leaves for good: its words and its farewell are a parting's (Colonies/Foundings.cs).
            SeeOff(map, going, plan.gate, plan.founding >= 0);
        }

        /// <summary>A word from one of the travellers, and those at home nearest the gate see them off.</summary>
        private static void SeeOff(Map map, List<Pawn> going, IntVec3 gate, bool founding)
        {
            if (!Speech.On) return;
            try
            {
                var traveller = going.Where(p => p.Spawned && p.RaceProps.Humanlike && p.Awake()).RandomElementWithFallback();
                if (traveller != null) Say(traveller, founding ? "foundleave" : "leave");
                var home = map.mapPawns.FreeColonistsSpawned.Where(p => !going.Contains(p) && PlayOuts.OurPerson(p) && p.Awake() && !p.Downed && !p.Drafted && !p.InMentalState)
                    .OrderBy(p => gate.IsValid ? p.Position.DistanceToSquared(gate) : 0).Take(4).InRandomOrder().Take(2);
                foreach (var p in home) Say(p, founding ? "foundseeoff" : "seeoff");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: seeing them off", e);
            }
        }

        // ---------------- Plans made and changed ----------------

        /// <summary>A plan made or changed from the planner: the last day and hours are worked out again from the start.</summary>
        public static void Confirm(JourneyPlan plan, bool isNew)
        {
            var book = JourneyBook.Instance;
            if (book == null || plan == null) return;
            // The plan setting off from this home is replaced (another home's goes on as it was).
            var old = book.Put(plan);
            if (old != null && old != plan)
            {
                // Replacing a plan: whoever no longer goes gets their hours and packs back.
                foreach (var p in old.travellers.Where(p => !plan.travellers.Contains(p)))
                    if (p != null && p.Spawned && !p.Dead && p.inventory != null && old.packing.Any(l => JourneyPack.Carried(p, l.def) > 0))
                        p.inventory.UnloadEverything = true;
                Restore(old);
                RemoveLetters(old);
            }
            Restore(plan);
            plan.stage = JourneyStage.Planned;
            plan.done.Clear();
            plan.issued.Clear();
            tries.Clear();
            plan.Changed();
            if (isNew || plan.id == 0) plan.id = book.nextId++;
            JourneyBook.Touch();
            Notify.Message("CC_Journey_Planned".Translate(Who(plan), plan.Dest, When(plan)).Resolve(), new LookTargets(plan.travellers.Where(p => p != null && p.Spawned)),
                MessageTypeDefOf.PositiveEvent, Importance.Important);
        }
    }
}
