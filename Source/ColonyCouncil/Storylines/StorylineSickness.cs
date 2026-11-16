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
    // S2, the sickness in the next town (docs/quests-proposals.md; the rules in Flows/SicknessFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Sickness.xml). A town of a people at peace (the hospitaller order first) has a
    // sickness in it, the game's own flu or plague by the people's age. Part 1: they ask for medicine, collected from the
    // colony's stockpiles by their carts. Part 2: three to six of their sick come and are cared for in the colony's beds as
    // its guests (the game's own care: its doctors tend them and feed them in bed); a colonist who tends them may catch it
    // (the physician's chance, through the shared harm and illness budgets, shown a day or two later). All recovered: part 3,
    // the order's healer stays some days and teaches the colony's doctor at their side (a lesson each day). Some died: part
    // 3, their kin come for the bodies, and the colony hands them over (the kin carry them home) or buries them here (graves
    // dug by the colony, the kin waiting at the graveside).

    /// <summary>The sickness in the next town beyond its parts: who asks, the illness, the sick, catching it, the endings.</summary>
    public class SicknessRules : StorylineRules
    {
        public override string Kind => SicknessFlow.Key;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The hospitaller order (any people of the healers' calling) with a town near, else any people at peace with a town near.</summary>
        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Healers && LivingEvents.TownOf(f, map) != null)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null);

        /// <summary>A plague among the region's world events now.</summary>
        public static bool PlagueYear => WorldSituations.Instance?.Running.Any(x => x?.def?.defName == "Council_WS_Plague") ?? false;

        /// <summary>The town's illness, the game's own catching kind by the people's age (the flu where the plague isn't loaded).</summary>
        public static HediffDef Illness(Faction f)
        {
            string name = SicknessFlow.Illness(HomeQuests.AgeOf(f), PlagueYear, Rand.Value);
            return DefDatabase<HediffDef>.GetNamedSilentFail(name) ?? DefDatabase<HediffDef>.GetNamedSilentFail(SicknessFlow.Flu);
        }

        public static HediffDef IllnessOf(Storyline s) => s?.what == null ? null : DefDatabase<HediffDef>.GetNamedSilentFail(s.what);

        /// <summary>A bed the colony could put a patient in (not for prisoners or slaves).</summary>
        public static int PatientBeds(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b => b.def.building?.bed_humanlike == true && !b.ForPrisoners && !b.ForSlaves).Sum(b => b.SleepingSlotsCount);

        /// <summary>The colony's best doctor at home.</summary>
        public static Pawn Doctor(Map map) =>
            PeoplesD.Best(map, SkillDefOf.Medicine, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) ?? VarietyUtil.Best(map, SkillDefOf.Medicine);

        /// <summary>Taken ill: the illness well along, their body already fighting it (so with care they mend).</summary>
        public static void Sicken(Pawn p, HediffDef def)
        {
            if (p?.health == null || def == null || p.health.hediffSet.HasHediff(def)) return;
            try
            {
                var h = HediffMaker.MakeHediff(def, p);
                h.Severity = Rand.Range(SicknessFlow.SeverityMin, SicknessFlow.SeverityMax);
                p.health.AddHediff(h);
                p.health.immunity.TryAddImmunityRecord(def, def);
                var rec = p.health.immunity.GetImmunityRecord(def);
                if (rec != null) rec.immunity = Mathf.Max(rec.immunity, Rand.Range(SicknessFlow.ImmunityMin, SicknessFlow.ImmunityMax));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the sick taken ill", e);
            }
        }

        /// <summary>Over it: alive, and the illness gone from them.</summary>
        public static bool Well(Pawn p, HediffDef def) => p != null && !p.Dead && def != null && !p.health.hediffSet.HasHediff(def);

        /// <summary>The colony's guest while ill: in a patient's bed, tended and fed by its doctors as the game cares for a guest.</summary>
        public static void TakeIn(Pawn x)
        {
            try
            {
                if (x == null || !x.Spawned || x.Dead) return;
                if (x.HostFaction != Faction.OfPlayer)
                {
                    Lords.Leave(x);
                    x.guest?.SetGuestStatus(Faction.OfPlayer);
                }
                if (x.InBed() || x.CurJobDef == JobDefOf.LayDown || x.Downed) return;
                var bed = RestUtility.FindPatientBedFor(x);
                if (bed != null) x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the sick taken in", e);
            }
        }

        /// <summary>Well again: up, no longer the colony's guest, and home with their people.</summary>
        public static void SendHome(Pawn x)
        {
            try
            {
                if (x == null || !x.Spawned || x.Dead) return;
                Hosting.GetUp(x);
                if (x.HostFaction == Faction.OfPlayer && !x.IsPrisoner) x.guest?.SetGuestStatus(null);
                if (!x.Downed) Arrivals.SendAway(new List<Pawn> { x });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the sick sent home", e);
            }
        }

        // ---------------- Catching it ----------------

        /// <summary>
        /// The game's tending (Health/HealthPatches.cs): one of the town's sick tended by one of the colony may pass it on, at
        /// the physician's chance (Harm/Catching.cs's rule: gloves and masks, a sterile room and the quarantine decree lower
        /// it), through the shared harm budget and the illness budget, shown a day or two later with the physician's message.
        /// True when the patient was one of the town's sick (the everyday rule then leaves them alone).
        /// </summary>
        public static bool Tended(Pawn doctor, Pawn patient)
        {
            if (patient == null || doctor == null) return false;
            var s = Storylines.Running.FirstOrDefault(x => x.kind == SicknessFlow.Key && x.group.Contains(patient));
            if (s == null) return false;
            try
            {
                var def = IllnessOf(s);
                if (def == null || !patient.health.hediffSet.HasHediff(def)) return true;
                if (!Harm.On || !Harm.SystemOn(HarmKinds.Physician.system) || !HarmFour.Ours(doctor)) return true;
                if (doctor.health.hediffSet.HasHediff(def) || Catching.Waiting(doctor)) return true;
                if ((doctor.health.immunity.GetImmunityRecord(def)?.immunity ?? 0f) >= 0.99f) return true;
                var room = patient.GetRoom();
                bool sterile = room != null && !room.PsychologicallyOutdoors && room.GetStat(RoomStatDefOf.InfectionChanceFactor) <= HarmFourMath.SterileInfection;
                bool decree = HarmFour.Decree(doctor, "Council_Decree_Quarantine") || HarmFour.Decree(doctor, "Council_Decree_SickBay");
                float chance = HarmFourMath.PhysicianChance(true, Ages.Ours >= WorldAge.Industrial, sterile, decree, 1);
                if (!Harm.Roll(doctor, HarmKinds.Physician, chance)) return true;
                var map = doctor.MapHeld;
                if (!IllnessBudget.Allows(map, def, doctor)) return true;
                IllnessBudget.Owe(map, def, doctor);
                HarmFour.Wait(new HarmWaiting
                {
                    what = HarmWaiting.Caught, kind = HarmKinds.Physician.key, pawn = doctor, other = patient, def = def, name = patient.LabelShort,
                    due = Now + HarmFourMath.CaughtTicks(Rand.Value)
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the sick tended", e);
            }
            return true;
        }

        // ---------------- Endings ----------------

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var f = s.giver;
            switch (key)
            {
                case "thanked":
                {
                    if (MapFacts.ThreatOn(map)) return false;
                    // The order's thanks without the healer: medicine, carried to the gate by one of theirs.
                    var med = DefDatabase<ThingDef>.GetNamedSilentFail(Ages.Of(f) <= WorldAge.Medieval ? "MedicineHerbal" : "MedicineIndustrial") ?? ThingDefOf.MedicineIndustrial;
                    int n = Mathf.Clamp(Mathf.RoundToInt(SicknessFlow.ThanksWorth * LivingEvents.WealthFactor(map) / Mathf.Max(1f, med.BaseMarketValue)), 3, 15);
                    Couriers.Loot(Couriers.Make(med, n), map, DropCellFinder.TradeDropSpot(map), f);
                    s.amount = n;
                    StoryChains.Goodwill(f, 6, GoodwillWhy.Task(6));
                    return true;
                }
                case "taught":
                    StoryChains.Goodwill(f, 6, GoodwillWhy.Task(6));
                    FactionMemory.Note(f, "tended", Storylines.Label(s));
                    return true;
                case "handed":
                case "buried":
                    StoryChains.Goodwill(f, key == "buried" ? 6 : 4, GoodwillWhy.Of(GoodwillReasonMath.TheirDead));
                    FactionMemory.Note(f, "mourned", Storylines.Label(s));
                    return true;
                case "refused":
                case "nobodies":
                    StoryChains.Goodwill(f, -8, GoodwillWhy.Of(GoodwillReasonMath.TheirDead));
                    return true;
                case "harmed":
                    FactionMemory.Note(f, "prisonerDied", Storylines.Label(s));
                    return true;
                case "nohelp":
                case "turnedaway":
                    FactionMemory.Note(f, "letDown", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        /// <summary>Their people are at war with the colony now: the healer goes home; the sick stay in the colony's care until they can walk, as any guest does.</summary>
        public override void Gone(Storyline s, Map map)
        {
            if (s.y != null && s.y.Spawned && s.y.Map == map) Arrivals.SendAway(new List<Pawn> { s.y });
        }
    }

    // ===================== Part 1: medicine for the town =====================

    /// <summary>
    /// The town asks for medicine (six to fourteen, any kind the colony keeps) in its stockpiles within six to eight days;
    /// their carts come for it within half a day of it being ready and pay half again over its worth. Half of it or more by
    /// the date: they take what there is at its plain worth. Less: none.
    /// </summary>
    public class HappeningWorker_SicknessMedicine : HappeningWorker_StorylinePart
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.Rules(SicknessFlow.Key).Giver(map) != null;

        protected override string Silver(Mission m, Storyline s) => s?.amount.ToString();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(SicknessFlow.Key).Giver(map);
            if (s == null || f == null) return false;
            m.a = SicknessRules.Doctor(map) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            var ill = SicknessRules.Illness(f);
            if (ill == null) return false;
            var x = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(25f, 60f));
            if (x == null) return false;
            Arrivals.Hold(x);
            int n = SicknessFlow.MedicineCount(IncidentWorker_Happening.Colonists(map).Count);
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.what = ill.defName;
            s.tale = ill.label;
            s.count = n;
            s.amount = SicknessFlow.MedicinePay(n, Missions.UnitValue("medicine"));
            if (ill.defName == SicknessFlow.Plague) s.Set("plague");
            if (Peoples.CallingOf(f) == PeoplesMath.Healers) s.Set("order");
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.goals.Add(new MissionGoal { what = "medicine", count = n });
            m.reward = s.amount;
            m.goodwill = 10;
            m.penalty = 6;
            m.days = Rand.Range(SicknessFlow.MedicineMinDays, SicknessFlow.MedicineMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.count = n;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            StorylineKit.Accepted(P, Story(m), m, map, null, Story(m)?.amount.ToString());
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.goals.Count == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var g = m.goals[0];
            g.have = Missions.Count(map, g.what);
            bool all = g.have >= g.count;
            StorylineKit.Remind(P, s, m, map, now, s.amount.ToString());
            if (m.stage == 0 && all)
            {
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    Note(m, map, "ready", LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, null, s.amount.ToString(), TextUtil.DaysString(m.due - now));
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due || now >= m.deadline && all)
            {
                Collect(m, map, g.count, s.amount);
                Missions.Goodwill(m, m.goodwill);
                End(m, MissionState.Done, "sent", null, null, s.amount.ToString());
                return;
            }
            if (now < m.deadline) return;
            if (SicknessFlow.MedicineOutcome(g.have, g.count) == "partly")
            {
                int pay = SicknessFlow.PartPay(g.have, Missions.UnitValue("medicine"));
                Collect(m, map, g.have, pay);
                Missions.Goodwill(m, m.goodwill / 2);
                End(m, MissionState.Done, "partly", null, g.have.ToString(), pay.ToString());
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            End(m, MissionState.Failed, "none");
        }

        /// <summary>Their carts come for it and pay (Couriers/); else it's taken from the stores and the silver left at the gate.</summary>
        private static void Collect(Mission m, Map map, int count, int pay)
        {
            if (count <= 0) return;
            if (Couriers.CollectGoals(map, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("medicine", count) }, m.faction, pay)) return;
            Missions.Take(map, "medicine", count);
            if (pay > 0) Couriers.PaySilver(map, DropCellFinder.TradeDropSpot(map), pay, m.faction);
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Storyline_Pay_Coming".Translate((m.faction?.Name ?? "?").Named("FACTION"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() : null;
    }

    // ===================== Part 2: the sick in the colony's beds =====================

    /// <summary>
    /// Three to six of the town's sick come within a day and are taken in as the colony's guests, each to a patient's bed,
    /// and cared for as the game cares for a guest (its doctors tend them and feed them in bed). Each who is well again goes
    /// home. When all are well or dead, or after nine days (anyone still in bed stays until they can walk): recovered, or
    /// some died. One of them taken prisoner: harmed.
    /// </summary>
    public class HappeningWorker_SicknessSick : HappeningWorker_StorylinePart
    {
        protected override int Rating => 3;

        protected override bool Possible(Map map) =>
            Storylines.current?.giver != null && SicknessRules.IllnessOf(Storylines.current) != null && SicknessRules.PatientBeds(map) >= 2 && SicknessRules.Doctor(map) != null;

        private static IEnumerable<Pawn> Sick(Mission m) => m.group.Where(p => p != null);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            var f = s.giver;
            m.a = SicknessRules.Doctor(map);
            var kind = Arrivals.PersonKind(f);
            if (m.a == null || kind == null) return false;
            int n = SicknessFlow.SickCount(IncidentWorker_Happening.Colonists(map).Count, Rand.Value);
            var sick = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(f, new FloatRange(16f, 70f), kind);
                if (p != null) sick.Add(p);
            }
            if (sick.Count == 0) return false;
            // The sick of an earlier part, if any, are long gone: these are the storyline's now.
            s.group.Clear();
            s.group.AddRange(sick);
            s.count = sick.Count;
            s.lost = 0;
            m.group.AddRange(sick);
            m.faction = f;
            m.place = s.place;
            m.target = sick.Count;
            m.days = SicknessFlow.SickDays;
            m.goodwill = 15;
            m.penalty = 8;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.count = sick.Count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || !Sick(m).Any(p => !p.Dead && !p.Discarded)) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.6f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var def = SicknessRules.IllnessOf(s);
            if (s == null || def == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                Come(m, s, map, def, now);
                return;
            }
            var all = Sick(m).ToList();
            if (all.Any(p => !p.Dead && p.IsPrisonerOfColony))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "harmed", new LookTargets(all.Where(p => p.Spawned)));
                return;
            }
            // Deaths and the mended, told as they come (m.misses counts the dead told, m.tally the mended).
            int dead = all.Count(p => p.Dead);
            if (dead > m.misses)
            {
                var died = all.Where(p => p.Dead).Skip(m.misses).ToList();
                m.misses = dead;
                foreach (var p in died) Note(m, map, "died", p.Corpse != null && p.Corpse.Spawned ? new LookTargets(p.Corpse) : LookTargets.Invalid, MessageTypeDefOf.NegativeHealthEvent, p.LabelShort);
            }
            foreach (var p in all.Where(p => !p.Dead && p.Spawned && p.Map == map && p.Faction == s.giver))
            {
                if (SicknessRules.Well(p, def))
                {
                    if (p.HostFaction == Faction.OfPlayer || p.GetLord() == null)
                    {
                        SicknessRules.SendHome(p);
                        m.tally++;
                        Note(m, map, "well", new LookTargets(p), MessageTypeDefOf.PositiveEvent, p.LabelShort);
                    }
                    continue;
                }
                SicknessRules.TakeIn(p);
            }
            s.lost = dead;
            bool settled = all.All(p => p.Dead || SicknessRules.Well(p, def) || !p.Spawned);
            if (!settled && now < m.deadline) return;
            var look = all.Where(p => p.Spawned).ToList();
            if (dead == 0)
            {
                Missions.Goodwill(m, m.goodwill);
                FactionMemory.Note(m.faction, "tended", m.place);
                End(m, MissionState.Done, "recovered", look.Count > 0 ? new LookTargets(look) : null);
                return;
            }
            Missions.Goodwill(m, dead * 2 < all.Count ? m.goodwill / 3 : -m.penalty / 2);
            End(m, MissionState.Done, "somedied", look.Count > 0 ? new LookTargets(look) : null, dead.ToString());
        }

        /// <summary>They come in from the edge, ill, and are taken in as the colony's guests, each to a patient's bed.</summary>
        private void Come(Mission m, Storyline s, Map map, HediffDef def, int now)
        {
            var sick = Sick(m).Where(p => !p.Dead && !p.Discarded && !p.Spawned).ToList();
            foreach (var p in sick) SicknessRules.Sicken(p, def);
            var gate = PlaySpots.GateCell(map, null);
            if (sick.Count == 0 || Arrivals.Visit(sick, map, s.giver, gate.IsValid ? gate : (IntVec3?)null, Mathf.Max(12f, (m.deadline - now) / (float)GenDate.TicksPerHour)) == null)
            {
                End(m, MissionState.Failed, StorylineMath.Ended);
                return;
            }
            foreach (var p in sick) SicknessRules.TakeIn(p);
            m.stage = 1;
            m.lastLook = now;
            Note(m, map, "came", new LookTargets(sick), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Those still in the colony's beds stay its guests until they can walk, as the game's guests do.</summary>
        public override void Release(Mission m)
        {
            foreach (var p in m.group.Where(p => p != null && !p.Dead && !p.Spawned && m.started < 0 && p.GetCaravan() == null)) Arrivals.Discard(p);
        }

        public override LookTargets Targets(Mission m)
        {
            var here = Sick(m).Where(p => p.Spawned).Cast<Thing>().ToList();
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var def = SicknessRules.IllnessOf(s);
            if (s == null || def == null) return null;
            var all = Sick(m).ToList();
            if (m.stage == 0)
                return "CC_Storyline_Sick_Coming".Translate(all.Count.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            int ill = all.Count(p => !p.Dead && p.Spawned && !SicknessRules.Well(p, def));
            int inBed = all.Count(p => !p.Dead && p.Spawned && !SicknessRules.Well(p, def) && p.InBed());
            return "CC_Storyline_Sick_Status".Translate(ill.Named("ILL"), inBed.Named("INBED"), m.tally.Named("WELL"), all.Count(p => p.Dead).Named("DEAD")).Resolve();
        }
    }

    // ===================== Part 3: the order's healer teaches =====================

    /// <summary>
    /// All the sick recovered: the order sends a healer who stays five or six days as the colony's guest (eating at its table,
    /// sleeping in its beds) and each day teaches the colony's doctor at their side for a few hours (a lesson: the doctor
    /// works beside them and learns, at the game's own pace). Taught: a lesson on at least half the days.
    /// </summary>
    public class HappeningWorker_SicknessHealer : HappeningWorker_StorylineHost
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && SicknessRules.Doctor(map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = SicknessRules.Doctor(map);
            if (m.a == null) return false;
            var y = Make(s.giver, new FloatRange(30f, 60f));
            if (y == null) return false;
            var med = y.skills?.GetSkill(SkillDefOf.Medicine);
            if (med != null && !med.TotallyDisabled) med.Level = Mathf.Max(med.Level, Rand.RangeInclusive(SicknessFlow.HealerSkillMin, SicknessFlow.HealerSkillMax));
            s.y = y;
            s.yName = y.LabelShort;
            s.goalA = 0;
            s.goalB = -1;
            s.baseA = -1;
            m.x = y;
            m.faction = s.giver;
            m.place = s.place;
            m.target = 1;
            m.wait = Rand.Range(0.4f, 0.8f);
            float stay = Rand.Range(SicknessFlow.HealerMinDays, SicknessFlow.HealerMaxDays);
            m.days = m.wait + stay;
            m.goals.Add(new MissionGoal { what = "meals", count = Mathf.CeilToInt(2.5f * stay) });
            m.goodwill = 8;
            m.penalty = 4;
            Fill(m, ctx);
            ctx.stranger = y;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        private static int Day(int tick) => tick / GenDate.TicksPerDay;

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var y = s?.y;
            if (s == null || y == null || y.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                var spot = Hosting.Spot(Beds(m, map), map);
                if (!Arrive(m, map, spot.IsValid ? spot : PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour + 6f))
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.lacks = 0;
                Note(m, map, "came", new LookTargets(y));
                return;
            }
            if (y.Dead)
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "fewlessons");
                return;
            }
            Lesson(m, s, map, now);
            if (Stay(m, map, now, y.LabelShort)) return;
            Lesson(m, s, map, now);
            int days = Mathf.Max(1, Mathf.RoundToInt((m.deadline - m.due) / (float)GenDate.TicksPerDay));
            SeeOff(m, map);
            Missions.Goodwill(m, m.goodwill);
            End(m, MissionState.Done, SicknessFlow.Taught(s.goalA, days) ? "taught" : "fewlessons", m.a != null && m.a.Spawned ? new LookTargets(m.a) : null, s.goalA.ToString());
        }

        /// <summary>
        /// A lesson a day (s.goalA counts them, s.goalB is the one set going, s.baseA the day of the last): in working hours,
        /// the doctor works at the healer's side for a few hours (the play-out's lesson); with answers not playing out on the
        /// map, the doctor learns as much by the day's end.
        /// </summary>
        private void Lesson(Mission m, Storyline s, Map map, int now)
        {
            var y = s.y;
            var doc = m.a != null && !m.a.Dead && m.a.Spawned && m.a.Map == map ? m.a : SicknessRules.Doctor(map);
            if (doc == null || y == null || !y.Spawned || y.Dead || y.Map != map) return;
            m.a = doc;
            // The one set going ended: a lesson given if it ran.
            if (s.goalB >= 0)
            {
                var p = PlayOutTracker.Instance?.plays.FirstOrDefault(x => x.id == s.goalB);
                if (p == null || p.state == PlayState.Done)
                {
                    if (p != null && p.started >= 0) s.goalA++;
                    s.goalB = -1;
                }
                else return;
            }
            int today = Day(now);
            int hour = GenLocalDate.HourOfDay(map);
            if (s.baseA == today || hour < 8 || hour > 15 || MapFacts.ThreatOn(map)) return;
            s.baseA = today;
            int t = y.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 10;
            int l = doc.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
            float xp = SicknessFlow.LessonXp(t, l);
            if (PlayOuts.On)
            {
                var ctx = new HappeningContext { map = map, a = doc, stranger = y, faction = s.giver, incident = def };
                var play = new DilemmaEffect_Lesson
                {
                    teacher = PlayWho.X, student = PlayWho.A, skill = SkillDefOf.Medicine, xpPerHour = xp,
                    duration = new FloatRange(SicknessFlow.LessonMinHours, SicknessFlow.LessonMaxHours), hours = new IntRange(8, 18)
                }.Begin(ctx);
                if (play != null)
                {
                    s.goalB = play.id;
                    Say(y, m, "lesson");
                    return;
                }
            }
            // Not on the map: the doctor spends the day at the healer's side all the same.
            var rec = doc.skills?.GetSkill(SkillDefOf.Medicine);
            if (rec == null || rec.TotallyDisabled) return;
            rec.Learn(xp * (SicknessFlow.LessonMinHours + SicknessFlow.LessonMaxHours) / 2f, true);
            s.goalA++;
            Note(m, map, "lesson", new LookTargets(doc), MessageTypeDefOf.PositiveEvent);
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            if (m.stage == 0)
                return "CC_Storyline_Healer_Coming".Translate((s.yName ?? "?").Named("RIVAL"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            return "CC_Storyline_Healer_Lessons".Translate((s.yName ?? "?").Named("RIVAL"), (m.a?.LabelShort ?? "?").Named("DOCTOR"), s.goalA.Named("COUNT")).Resolve() + " " + MealsText(m);
        }
    }

    // ===================== Part 3: the kin come for the dead =====================

    /// <summary>
    /// Some of the sick died: a day or two later their kin come to the gate for the bodies, and the card asks what is done:
    /// hand them over (each of the kin carries one home: the game's own carrying, off the map), or bury them here (graves
    /// dug by the colony beyond the gate, the bodies carried in by its haulers, the kin waiting by the graves for about a
    /// day). The dead already in the colony's graves: the kin visit them. The bodies gone (burned, eaten): nobodies.
    /// </summary>
    public class HappeningWorker_SicknessKin : HappeningWorker_StorylinePart
    {
        public const string Card = "Rimfolk_Storyline_SicknessDead";

        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && Storylines.current.lost > 0;

        private static List<Corpse> Bodies(Storyline s, Map map) =>
            s.group.Where(p => p != null && p.Dead && p.Corpse != null && !p.Corpse.Destroyed && p.Corpse.MapHeld == map).Select(p => p.Corpse).ToList();

        private static bool InGrave(Corpse c) => c?.ParentHolder is Building_Grave;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            var kind = Arrivals.PersonKind(s.giver);
            if (m.a == null || kind == null) return false;
            int n = SicknessFlow.KinCount(s.lost);
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(s.giver, new FloatRange(20f, 65f), kind);
                if (p != null) m.group.Add(p);
            }
            if (m.group.Count == 0) return false;
            m.faction = s.giver;
            m.place = s.place;
            m.days = 2.5f;
            m.goodwill = 5;
            Fill(m, ctx);
            ctx.count = s.lost;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.6f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        private List<Pawn> Kin(Mission m, Map map) => m.group.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map).ToList();

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var bodies = Bodies(s, map);
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    var loose = bodies.Where(c => !InGrave(c)).ToList();
                    var spot = loose.Count > 0 ? PeoplesD.GateSpot(map) : bodies.FirstOrDefault(InGrave)?.PositionHeld;
                    if (Arrivals.Visit(m.group.Where(p => p != null && !p.Dead).ToList(), map, s.giver, spot, 30f) == null)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    m.lastLook = now;
                    Note(m, map, "came", new LookTargets(Kin(m, map)));
                    if (loose.Count > 0)
                    {
                        m.stage = 1;
                        Ask(m, s, map, now, loose.Count);
                        return;
                    }
                    // Already in the colony's graves: they stand by them a few hours. Gone altogether: nothing to take home.
                    m.stage = bodies.Count > 0 ? 5 : 6;
                    m.due = now + GenDate.TicksPerHour * 4;
                    return;
                }
                case 1:
                    // The card: answered (Answered moves on), or lost without an answer: they take the bodies home.
                    if (!StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
                    {
                        if (now - m.lastLook > GenDate.TicksPerDay / 2 || !Ask(m, s, map, now, bodies.Count(c => !InGrave(c))))
                            Answered(m, s, StorylineKit.Context(s, m, map), 0, true);
                    }
                    return;
                case 2:
                {
                    // Carrying them home: over once the bodies are off the map with them, or the kin have gone.
                    var kin = Kin(m, map);
                    bool carried = bodies.Count(c => !InGrave(c)) == 0;
                    if (!carried && kin.Count > 0 && now < m.due)
                    {
                        // Someone of theirs still free takes the next body still lying here.
                        foreach (var k in kin.Where(k => k.CurJobDef != JobDefOf.Steal && k.carryTracker?.CarriedThing == null))
                        {
                            var next = bodies.FirstOrDefault(c => c.Spawned && !InGrave(c) && !map.reservationManager.IsReserved(c));
                            if (next == null) break;
                            Carry(k, next, map);
                        }
                        return;
                    }
                    // Those not carrying one go home on foot; a body still lying here is the colony's to see to.
                    Arrivals.SendAway(kin.Where(k => k.CurJobDef != JobDefOf.Steal));
                    End(m, MissionState.Done, "handed", null);
                    return;
                }
                case 3:
                {
                    // The burial: the graves dug and the bodies carried in by the colony's people, the kin by the graves.
                    var plots = s.wet.Select(Cell).Where(c => c.IsValid).ToList();
                    foreach (var c in plots) Strangers(PlayBurial.GraveAt(map, c));
                    bool all = bodies.Count > 0 && bodies.All(InGrave);
                    if (!all && now < m.due) return;
                    s.wet.Clear();
                    // Frames nobody dug are taken up again.
                    foreach (var c in plots) PlayBurial.FrameAt(map, c)?.Destroy(DestroyMode.Cancel);
                    int buried = bodies.Count(InGrave);
                    var kin = Kin(m, map);
                    if (buried == 0 && bodies.Any(c => c.Spawned))
                    {
                        // Not done in time: the kin take them home after all.
                        Note(m, map, "notburied", new LookTargets(kin));
                        m.stage = 2;
                        m.due = now + GenDate.TicksPerDay;
                        foreach (var k in kin)
                        {
                            var next = bodies.FirstOrDefault(b => b.Spawned && !map.reservationManager.IsReserved(b));
                            if (next != null) Carry(k, next, map);
                        }
                        return;
                    }
                    Say(kin.FirstOrDefault(), m, "buried");
                    Arrivals.SendAway(kin);
                    End(m, MissionState.Done, "buried", bodies.FirstOrDefault(InGrave) is Corpse g ? new LookTargets(g.ParentHolder as Thing) : null);
                    return;
                }
                case 5:
                    if (now < m.due) return;
                    Arrivals.SendAway(Kin(m, map));
                    End(m, MissionState.Done, "buried", null);
                    return;
                case 6:
                    if (now < m.due) return;
                    Arrivals.SendAway(Kin(m, map));
                    End(m, MissionState.Failed, "nobodies", null);
                    return;
            }
        }

        /// <summary>The families who never came (turned away, or the offer ran out) were only ever names; those gone home are the world's again.</summary>
        public override void Release(Mission m)
        {
            foreach (var p in m.group.Where(p => p != null && !p.Dead && !p.Spawned).ToList())
            {
                if (m.started < 0 && p.GetCaravan() == null) Arrivals.Discard(p);
                else Arrivals.Release(p);
            }
        }

        /// <summary>The card: what is done with their dead. False: nobody of the colony to answer now.</summary>
        private bool Ask(Mission m, Storyline s, Map map, int now, int count)
        {
            var kin = Kin(m, map);
            bool asked = StorylineKit.Ask(Card, s, m, map, c =>
            {
                c.stranger = kin.FirstOrDefault();
                c.count = count;
            });
            m.lastLook = now;
            if (asked) Say(kin.FirstOrDefault(), m, "ask");
            return asked;
        }

        /// <summary>One of the kin takes a body and carries it home, off the map (the game's own carrying away).</summary>
        private static void Carry(Pawn k, Corpse c, Map map)
        {
            try
            {
                if (k == null || c == null || !c.Spawned || !k.Spawned) return;
                c.SetForbidden(false, false);
                if (!RCellFinder.TryFindBestExitSpot(k, out var exit, TraverseMode.ByPawn)) return;
                var job = JobMaker.MakeJob(JobDefOf.Steal, c, exit);
                job.count = 1;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                k.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the kin carry their dead", e);
            }
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            int now = Find.TickManager.TicksGame;
            var bodies = Bodies(s, map).Where(c => !InGrave(c)).ToList();
            var kin = Kin(m, map);
            if (option == 1 && PlayBurial.CanDig(map) && bodies.Count > 0)
            {
                // Buried here: graves laid out beyond the gate for the colony to dig, the kin waiting by them.
                var plots = PlayBurial.Plot(map, null, bodies.Count, true, out var rot);
                if (plots.Count == 0) plots = PlayBurial.Plot(map, null, bodies.Count, false, out rot);
                if (plots.Count > 0)
                {
                    s.wet.Clear();
                    foreach (var c in plots)
                    {
                        if (PlayBurial.FrameAt(map, c) != null || PlayBurial.GraveAt(map, c) != null || !PlayBurial.Fits(c, rot, map, null)) continue;
                        var frame = (Frame)ThingMaker.MakeThing(PlayBurial.GraveDef.frameDef);
                        frame.SetFactionDirect(Faction.OfPlayer);
                        GenSpawn.Spawn(frame, c, map, rot, WipeMode.Vanish);
                        s.wet.Add(c.x + "," + c.z);
                    }
                    foreach (var b in bodies) b.SetForbidden(false, false);
                    if (kin.Count > 0)
                    {
                        Lords.Leave(kin);
                        LordMaker.MakeNewLord(s.giver, new LordJob_VisitOnErrand(s.giver, plots[0], Mathf.RoundToInt((SicknessFlow.BurialHours + 6f) * GenDate.TicksPerHour)), map, kin);
                    }
                    s.Set("buriedhere");
                    m.stage = 3;
                    m.due = now + Mathf.RoundToInt(SicknessFlow.BurialHours * GenDate.TicksPerHour);
                    Note(m, map, "graves", new LookTargets(new TargetInfo(plots[0], map)));
                    Say(kin.FirstOrDefault(), m, "bury");
                    return;
                }
            }
            // Handed over: each of them carries one home.
            s.Set("handedover");
            m.stage = 2;
            m.due = now + GenDate.TicksPerDay;
            for (int i = 0; i < kin.Count && i < bodies.Count; i++) Carry(kin[i], bodies[i], map);
            Say(kin.FirstOrDefault(), m, "hand");
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "hand":
                    sheet.Play("CC_Storyline_Impact_HandDead".Translate((s.giver?.Name ?? "?").Named("FACTION")).Resolve(), "CC_Storyline_Impact_HandDeadBrief".Translate().Resolve());
                    break;
                case "bury":
                    sheet.Play("CC_Storyline_Impact_BuryDead".Translate((s.giver?.Name ?? "?").Named("FACTION")).Resolve(), "CC_Storyline_Impact_BuryDeadBrief".Translate().Resolve());
                    break;
            }
        }

        private static IntVec3 Cell(string s)
        {
            var b = (s ?? "").Split(',');
            return b.Length == 2 && int.TryParse(b[0], out int x) && int.TryParse(b[1], out int z) ? new IntVec3(x, 0, z) : IntVec3.Invalid;
        }

        /// <summary>The graves dug for them take strangers' bodies (the kin's dead), and never one of the colony's own.</summary>
        private static void Strangers(Building_Grave g)
        {
            var filter = g?.GetStoreSettings()?.filter;
            if (filter == null) return;
            var stranger = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesStranger");
            var colonist = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("AllowCorpsesColonist");
            if (stranger != null && !filter.Allows(stranger)) filter.SetAllow(stranger, true);
            if (colonist != null && filter.Allows(colonist)) filter.SetAllow(colonist, false);
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return LookTargets.Invalid;
            var things = Kin(m, map).Cast<Thing>().Concat(Bodies(s, map).Where(c => c.Spawned)).ToList();
            return things.Count > 0 ? new LookTargets(things) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            switch (m.stage)
            {
                case 0: return "CC_Storyline_Kin_Coming".Translate(m.group.Count.Named("COUNT"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
                case 1: return "CC_Storyline_Kin_Asking".Translate().Resolve();
                case 3:
                {
                    var map = m.Map;
                    int buried = map == null ? 0 : Bodies(s, map).Count(InGrave);
                    return "CC_Storyline_Kin_Burying".Translate(buried.Named("BURIED"), s.lost.Named("COUNT")).Resolve();
                }
                default: return "CC_Storyline_Kin_Carrying".Translate().Resolve();
            }
        }
    }
}
