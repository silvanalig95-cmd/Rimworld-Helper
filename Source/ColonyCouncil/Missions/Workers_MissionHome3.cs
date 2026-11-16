using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    // Quests at home, round 2 (the player's "great, do some more", 16 November 2026: docs/quests-proposals.md, Q3, Q4, Q6
    // and Q8 here; Q11, Q12 and Q13 in Workers_MissionHome4.cs; texts in Defs/MissionDefs/Rimfolk_Missions_Home2.xml; the
    // pure rules in HomeQuests2Math.cs, what they share in HomeQuests2.cs). Each plays out on the colony's own map: their
    // people come to the colony, and what they leave or want is collected by them.

    // ===================== Q3. A young fighter trained =====================

    /// <summary>
    /// A neighbour sends one of their young fighters to stay at the colony for some days and learn from its best shot or its
    /// best melee fighter. Each evening, while the teacher is free, the two train for about two hours: side by side at the
    /// colony's shooting or archery target when it has one, else sparring with pulled blows in the open (the colony's own
    /// drills' rate of a blow that lands for real, through the shared harm budget). Both learn as the game teaches (its own
    /// learning, passions and daily limit). The young one is the colony's guest meanwhile: fed and given a bed. At the end of
    /// the stay they pay for the hours trained, half for half of them.
    /// </summary>
    public class HappeningWorker_MissionTrainee : HappeningWorker_MissionHost
    {
        protected override string Kind => HomeQuests2Math.TraineeKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 2;

        public override bool OwnMemory => false;

        /// <summary>The pairs at training now, by each pawn (thingIDNumber); a reload ends them, and the next evening starts again.</summary>
        private static readonly Dictionary<int, TrainSession> sessions = new Dictionary<int, TrainSession>();

        /// <summary>
        /// Each quest's day, the ticks trained that day, and whether that day's sparring has had its one roll for a real blow
        /// (a reload starts the day's count again; nothing else depends on it).
        /// </summary>
        private static readonly Dictionary<int, (int day, int ticks, bool rolled)> today = new Dictionary<int, (int, int, bool)>();

        public static TrainSession SessionOf(Pawn p) => p != null && sessions.TryGetValue(p.thingIDNumber, out var s) ? s : null;

        /// <summary>The colony's shooting and archery targets (Misc. Training's, Medieval 2's), by name, so a missing mod costs nothing.</summary>
        private static readonly string[] Targets = { "ShootingRangeTarget", "ArcheryTarget", "VFEM2_ArcheryTarget" };

        public static Thing ShootingTarget(Map map) =>
            map?.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.Spawned && Array.IndexOf(Targets, b.def.defName) >= 0 && !b.IsForbidden(Faction.OfPlayer) && !b.IsBurning());

        private static int Level(Pawn p, SkillDef s) => p?.skills?.GetSkill(s)?.Level ?? -1;

        /// <summary>The colony's best at a fighting skill, among grown people who will fight.</summary>
        private static Pawn BestAt(Map map, SkillDef s) =>
            IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Dead && !p.WorkTagIsDisabled(WorkTags.Violent)
                                                               && p.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => Level(p, s)).FirstOrDefault();

        private static SkillDef SkillOf(Mission m) => m.variant == "shoot" ? SkillDefOf.Shooting : SkillDefOf.Melee;

        private static int StayDays(Mission m) => Mathf.Max(1, m.target / HomeQuests2Math.TrainHoursPerDay);

        protected override bool Possible(Map map) => Neighbor(map) != null && BestAt(map, SkillDefOf.Melee) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var x = HomeQuests.Person(m.faction, new FloatRange(16f, 21f), HomeQuests.FighterKind(m.faction, 70f));
            if (x == null) return false;
            var shot = BestAt(map, SkillDefOf.Shooting);
            var melee = BestAt(map, SkillDefOf.Melee);
            bool ranged = x.equipment?.Primary?.def?.IsRangedWeapon ?? false;
            bool shoots = shot != null && HomeQuests2Math.Shoots(ShootingTarget(map) != null, ranged, Level(shot, SkillDefOf.Shooting), Level(melee, SkillDefOf.Melee));
            m.variant = shoots ? "shoot" : "spar";
            m.a = shoots ? shot : melee;
            var skill = SkillOf(m);
            var r = x.skills?.GetSkill(skill);
            if (m.a == null || r == null || r.TotallyDisabled || x.WorkTagIsDisabled(WorkTags.Violent))
            {
                Arrivals.Discard(x);
                return false;
            }
            // They come to learn: less of the skill than their teacher.
            r.Level = HomeQuests2Math.TraineeLevel(r.Level, Level(m.a, skill));
            m.x = x;
            m.misses = r.Level;
            m.name = x.LabelShort;
            m.tale = HappeningText.SkillNoun(skill);
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.target = HomeQuests2Math.TrainTarget(HomeQuests2Math.Days(HomeQuest2.Trainee, Rand.Value));
            m.wait = Rand.Range(HomeQuestsMath.ComeMin, HomeQuestsMath.ComeMax);
            m.days = Mathf.Ceil(m.wait + StayDays(m) + 1f);
            m.reward = HomeQuests2.Reward(HomeQuest2.Trainee, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.Trainee);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.Trainee);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.skill = skill;
            return true;
        }

        /// <summary>The terms: {DAYS} the days of the stay, {ITEM} the reward in words, {TALE} the skill, {A} the teacher.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, StayDays, mm => mm.reward);

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && (x.Dead || Harmed(m)))
            {
                Stop(m);
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, x.Dead ? "died" : "harmed", null, x.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
                return;
            }
            if (m.stage == 0)
            {
                if (now < m.due) return;
                var spot = PlaySpots.GateCell(map, null);
                if (!Arrive(m, map, spot.IsValid ? spot : (IntVec3?)null, StayDays(m) * 24f + 12f))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                m.due = now + StayDays(m) * GenDate.TicksPerDay;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(StayDays(m) * GenDate.TicksPerDay));
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (!x.Spawned || x.Map != map)
            {
                // Gone home early (a raid sent them off): the stay is judged on the hours there were.
                End(m, map);
                return;
            }
            if (SessionOf(x) == null) Tend(m, map);
            if (now >= m.due)
            {
                End(m, map);
                return;
            }
            if (SessionOf(x) == null && HomeQuests2Math.TrainHour(GenLocalDate.HourOfDay(map)) && TodayTicks(m, map) < HomeQuests2Math.TrainHoursPerDay * GenDate.TicksPerHour)
                Begin(m, map, false);
        }

        private static int Day(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        private static int TodayTicks(Mission m, Map map) => today.TryGetValue(m.id, out var t) && t.day == Day(map) ? t.ticks : 0;

        /// <summary>
        /// Sparring: the evening's one real blow, decided once a day (the colony's own drills' rate, one bout in 25, through the
        /// shared harm budget), on one of the pair who may take it, somewhere in the session.
        /// </summary>
        private static void Roll(Mission m, Map map, TrainSession s)
        {
            int day = Day(map);
            (int day, int ticks, bool rolled) t = today.TryGetValue(m.id, out var x) && x.day == day ? x : (day, 0, false);
            if (s.shoot || t.rolled) return;
            today[m.id] = (day, t.ticks, true);
            if (!Harm.On || !Harm.SystemOn(HarmKinds.Drills.system) || !Harm.Roll(HarmKinds.Drills, HarmMath.DrillBlow(false))) return;
            var can = new[] { s.a, s.b }.Where(p => Harm.May(p, HurtClass.Bout)).ToList();
            if (can.Count == 0) return;
            s.hardOn = can.RandomElement();
            s.hardAt = Rand.RangeInclusive(6, 60);
        }

        /// <summary>The evening's training starts, if the teacher (or the colony's best now) and the young one are both free and there's a place for it.</summary>
        private bool Begin(Mission m, Map map, bool forced)
        {
            var b = m.x;
            var skill = SkillOf(m);
            var a = m.a != null && !m.a.Dead && m.a.Spawned && m.a.Map == map && HomeQuests2.Free(m.a) ? m.a : null;
            if (a == null)
            {
                var best = BestAt(map, skill);
                if (best != null && best.Spawned && best.Map == map && HomeQuests2.Free(best)) a = best;
            }
            if (a == null || b == null || !b.Spawned || !(Hosting.Free(b) || forced && !b.Downed && !b.InMentalState)) return false;
            var target = m.variant == "shoot" ? ShootingTarget(map) : null;
            IntVec3 ra, rb;
            if (target != null)
            {
                if (!Range(map, target, a, b, out ra, out rb)) return false;
            }
            else
            {
                var center = PlaySpots.OpenCell(map, a);
                if (!center.IsValid || !DrillBouts.Places(map, center, a, b, new List<IntVec3>(), out ra, out rb)) return false;
            }
            var s = new TrainSession { missionId = m.id, a = a, b = b, shoot = target != null };
            sessions[a.thingIDNumber] = s;
            sessions[b.thingIDNumber] = s;
            var ja = JobMaker.MakeJob(HomeQuests2DefOf.Rimfolk_HomeQ_Train, b, ra, target);
            var jb = JobMaker.MakeJob(HomeQuests2DefOf.Rimfolk_HomeQ_Train, a, rb, target);
            if (b.CurJobDef == HostingDefOf.Council_GuestSleep) Hosting.GetUp(b);
            a.jobs.StartJob(ja, JobCondition.InterruptForced, null, false, true);
            b.jobs.StartJob(jb, JobCondition.InterruptForced, null, false, true);
            if (a.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Train && b.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Train)
            {
                m.a = a;
                Roll(m, map, s);
                return true;
            }
            Forget(s);
            if (a.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Train) a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (b.CurJobDef == HomeQuests2DefOf.Rimfolk_HomeQ_Train) b.jobs.EndCurrentJob(JobCondition.InterruptForced);
            return false;
        }

        /// <summary>Two places side by side, five to nine cells from the target, in sight of it and reachable by both.</summary>
        private static bool Range(Map map, Thing target, Pawn a, Pawn b, out IntVec3 ra, out IntVec3 rb)
        {
            ra = rb = IntVec3.Invalid;
            var at = target.Position;
            foreach (var c in GenRadial.RadialCellsAround(at, 9f, false).Where(c => !c.InHorDistOf(at, 5f)).InRandomOrder().Take(80))
            {
                if (!Clear(c, map, a) || !GenSight.LineOfSight(c, at, map)) continue;
                foreach (var n in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One))
                {
                    if (!Clear(n, map, b) || !GenSight.LineOfSight(n, at, map)) continue;
                    ra = c;
                    rb = n;
                    return true;
                }
            }
            return false;
        }

        private static bool Clear(IntVec3 c, Map map, Pawn p) =>
            c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && PlaySpots.Ok(c, map, p);

        private static void Forget(TrainSession s)
        {
            if (s == null) return;
            s.over = true;
            if (s.a != null && sessions.TryGetValue(s.a.thingIDNumber, out var x) && x == s) sessions.Remove(s.a.thingIDNumber);
            if (s.b != null && sessions.TryGetValue(s.b.thingIDNumber, out var y) && y == s) sessions.Remove(s.b.thingIDNumber);
        }

        /// <summary>One of the pair left the training (the evening's hours done, called away, danger): it is over for both.</summary>
        public static void Left(Pawn p)
        {
            var s = SessionOf(p);
            if (s == null) return;
            Forget(s);
        }

        /// <summary>The time the pair trained, counted by the teacher: both learn the skill, the game's own way.</summary>
        public static void Trained(TrainSession s, int delta)
        {
            var m = Missions.ById(s.missionId);
            var map = s.a?.Map;
            if (m == null || !m.Open || map == null)
            {
                s.over = true;
                return;
            }
            var skill = SkillOf(m);
            m.tally += delta;
            int day = Day(map);
            (int day, int ticks, bool rolled) t = today.TryGetValue(m.id, out var x) && x.day == day ? x : (day, 0, false);
            int ticks = t.ticks + delta;
            today[m.id] = (day, ticks, t.rolled);
            try
            {
                s.b.skills?.Learn(skill, HomeQuests2Math.TraineeXpPerTick * delta);
                s.a.skills?.Learn(skill, HomeQuests2Math.TrainerXpPerTick * delta);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: training learned", e);
            }
            if (ticks < HomeQuests2Math.TrainHoursPerDay * GenDate.TicksPerHour) return;
            // The evening's hours are done.
            // together: the pair side by side at their places; the teacher's word at the end.
            HomeQuests2.Say(s.a, "Rimfolk_Pool_HomeQ2_TrainDone", s.b);
            Forget(s);
        }

        /// <summary>A pulled blow at the other, the lunge and the sound; on the evening's one real blow, a real hurt.</summary>
        public static void Swing(TrainSession s, Pawn attacker, Pawn defender)
        {
            if (s == null || s.over || attacker?.Map == null || defender == null) return;
            try
            {
                attacker.Drawer?.Notify_MeleeAttackOn(defender);
                s.swings++;
                if (s.hardAt >= 0 && s.swings >= s.hardAt && s.hardOn == defender)
                {
                    RealBlow(s, attacker, defender);
                    return;
                }
                (Rand.Chance(0.5f) ? SoundDefOf.Pawn_Melee_Punch_Miss : SoundDefOf.Pawn_Melee_Punch_HitPawn)?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: a swing at training", e);
            }
        }

        /// <summary>The blow that lands: a bruise or a cut by the colony's means (the drills' own), said and shown; the evening's training ends there.</summary>
        private static void RealBlow(TrainSession s, Pawn attacker, Pawn defender)
        {
            s.hardAt = -1;
            DrillBouts.BlowOf(Ages.Ours, out var kind, out var where);
            var h = Harm.Injure(defender, kind, where, HarmMath.DrillLow, HarmMath.DrillHigh, HarmKinds.Drills, HurtClass.Bout, attacker);
            SoundDefOf.Pawn_Melee_Punch_HitPawn?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
            if (h == null) return;
            // together: the two at their places side by side, where the blow fell.
            HomeQuests2.Say(defender, "Rimfolk_Pool_HomeQ2_SparHit", attacker);
            HomeQuests2.Say(attacker, "Rimfolk_Pool_HomeQ2_SparSorry", defender);
            Tell.Message(NewsKind.Health, Importance.Important, defender.Map, "CC_HomeQ2_Train_Blow".Translate(attacker.Named("OTHER"), defender.Named("PAWN"),
                (h.Part?.Label ?? "").Named("PART")).Resolve(), new LookTargets(defender), MessageTypeDefOf.NegativeHealthEvent);
            var m = Missions.ById(s.missionId);
            if (m != null && defender.Map != null) today[m.id] = (Day(defender.Map), HomeQuests2Math.TrainHoursPerDay * GenDate.TicksPerHour, true);
            Forget(s);
        }

        private static void Stop(Mission m)
        {
            foreach (var p in new[] { m.x, m.a })
            {
                var s = SessionOf(p);
                if (s != null && s.missionId == m.id) Forget(s);
            }
        }

        /// <summary>The stay is over: they pay for the hours trained (half for half of them), and go home.</summary>
        private void End(Mission m, Map map)
        {
            Stop(m);
            var x = m.x;
            var skill = SkillOf(m);
            int now = Level(x, skill);
            if (now >= 0) m.tale = "CC_HomeQ2_Train_Levels".Translate(HappeningText.SkillNoun(skill).Named("SKILL"), m.misses.Named("FROM"), now.Named("TO")).Resolve();
            var end = HomeQuests2Math.TrainOutcome(m.tally, m.target, GenDate.TicksPerHour);
            int pay = HomeQuests2Math.TrainPay(m.reward, end);
            bool here = x != null && x.Spawned && x.Map == map && !x.Dead;
            if (pay > 0) HomeQuests2.PayInPerson(m, map, here ? x : null, pay);
            SeeOff(m, map);
            var look = here ? new LookTargets(x) : LookTargets.Invalid;
            switch (end)
            {
                case TrainEnd.Full:
                    Missions.Goodwill(m, m.goodwill);
                    Missions.Finish(m, MissionState.Done, "trained", HomeQuests.RewardWords(m, pay), look);
                    return;
                case TrainEnd.Half:
                    Missions.Goodwill(m, m.goodwill / 2);
                    Missions.Finish(m, MissionState.Done, "half", HomeQuests.RewardWords(m, pay), look, LetterDefOf.NeutralEvent);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "untrained", null, look);
                    return;
            }
        }

        /// <summary>The colony's plainer food for them, else raw food.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override void Release(Mission m)
        {
            Stop(m);
            today.Remove(m.id);
            base.Release(m);
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            if (m.stage == 0) return "CC_HomeQ2_Train_Status0".Translate(x.Named("X")).Resolve();
            var map = m.Map;
            int hours = Mathf.Min(m.target, m.tally / GenDate.TicksPerHour);
            int todayHours = map == null ? 0 : TodayTicks(m, map) / GenDate.TicksPerHour;
            string teacher = (m.a?.LabelShort ?? "?");
            return "CC_HomeQ2_Train_Status1".Translate(x.Named("X"), hours.Named("HOURS"), m.target.Named("TARGET"), todayHours.Named("TODAY"),
                HomeQuests2Math.TrainHoursPerDay.Named("DAILY"), teacher.Named("TEACHER")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "train now", "trained", "half", "untrained", "died" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            switch (outcome)
            {
                case "train now":
                    if (m.stage == 1 && SessionOf(m.x) == null) Begin(m, map, true);
                    return;
                case "trained":
                    m.tally = Mathf.Max(m.tally, m.target * GenDate.TicksPerHour);
                    End(m, map);
                    return;
                case "half":
                    m.tally = m.target * GenDate.TicksPerHour / 2;
                    End(m, map);
                    return;
                case "untrained":
                    m.tally = 0;
                    End(m, map);
                    return;
                default:
                    Stop(m);
                    Missions.Goodwill(m, -m.penalty * 2);
                    Missions.Finish(m, MissionState.Failed, "died", null);
                    return;
            }
        }
    }

    // ===================== Q4. A piece for a wedding =====================

    /// <summary>
    /// Someone of a neighbour is to be married, and their people ask the colony to make one piece for the wedding: clothes
    /// of their own age (a dress, a robe, a tribal headdress, a formal shirt), a small work of art, or furniture for the couple's new
    /// home (a double bedroll, a double bed, the Empire's royal bed), whatever the colony has the means to make, of good
    /// quality or better. On the day, their people come for it and pay well. The colony knows only what they wrote.
    /// </summary>
    public class HappeningWorker_MissionWeddingPiece : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuests2Math.WeddingKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 2;

        private static ThingDef PieceDef(Mission m) => m.goals.Count == 0 ? null : DefDatabase<ThingDef>.GetNamedSilentFail(m.goals[0].what);

        private static SkillDef MakerSkill(string tag) => tag == "art" ? SkillDefOf.Artistic : tag == "bed" ? SkillDefOf.Construction : SkillDefOf.Crafting;

        protected override bool Possible(Map map)
        {
            var f = Neighbor(map);
            return f != null && HomeQuests2.PieceOptions(f, map).Count > 0;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var options = HomeQuests2.PieceOptions(m.faction, map);
            if (options.Count == 0) return false;
            // Clothes first where there are some: a wedding is dressed for.
            var pick = options.RandomElementByWeight(o => o.tag == "clothes" ? 2f : 1f);
            m.goals.Add(new MissionGoal { what = pick.def.defName, count = 1 });
            m.variant = pick.tag;
            m.tale = pick.def.label;
            m.name = InviteForms.Named(m.faction, new FloatRange(18f, 30f), null, out m.royalHost);
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, null, MakerSkill(pick.tag));
            if (m.a == null) return false;
            m.days = HomeQuests2Math.Days(HomeQuest2.WeddingPiece, Rand.Value);
            var stuff = pick.def.MadeFromStuff ? GenStuff.DefaultStuffFor(pick.def) : null;
            float value = pick.def.GetStatValueAbstract(StatDefOf.MarketValue, stuff);
            m.reward = HomeQuests2Math.PiecePay(value, HomeQuests.AgeOf(m.faction), Ages.IsEmpire(m.faction), LivingEvents.WealthFactor(map), Rand.Value);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.WeddingPiece);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.WeddingPiece);
            Fill(m, ctx);
            return true;
        }

        /// <summary>The terms: {DAYS} until the wedding, {ITEM} the pay, {TALE} the piece, {DEAD} the one marrying.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => Mathf.RoundToInt(mm.days), mm => mm.reward);

        public override bool Start(Mission m, Map map)
        {
            var def = PieceDef(m);
            if (def == null) return false;
            m.goals[0].have = Mathf.Min(1, HomeQuests2.Pieces(map, def).Count);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var def = PieceDef(m);
            if (def == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 2)
            {
                Collect(m, map, now);
                return;
            }
            var pieces = HomeQuests2.Pieces(map, def);
            m.goals[0].have = Mathf.Min(1, pieces.Count);
            if (pieces.Count > 0 && m.stage == 0)
            {
                m.stage = 1;
                if (m.misses == 0)
                {
                    // Told once: the piece is ready, and they come for it on the day.
                    m.misses = 1;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(pieces[0]), MessageTypeDefOf.PositiveEvent);
                }
            }
            else if (pieces.Count == 0 && m.stage == 1) m.stage = 0;
            if (now < m.deadline) return;
            if (pieces.Count == 0)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "missed", null);
                return;
            }
            // The day: their people come for the best of them and bring the pay (Couriers/); else it's handed over at once.
            m.thing = pieces[0];
            m.stage = 2;
            m.lastLook = now;
            if (!Couriers.CollectThings(map, new List<Thing> { m.thing }, m.faction, HomeQuests.PayThings(m, m.reward))) Done(m, map, true);
        }

        private void Collect(Mission m, Map map, int now)
        {
            var t = m.thing;
            if (t == null || t.Destroyed || HomeQuests.HeldBy(t, m.faction) || !HomeQuests.OnMap(t, map))
            {
                Done(m, map, false);
                return;
            }
            if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Done(m, map, true);
        }

        private void Done(Mission m, Map map, bool now)
        {
            if (now)
            {
                if (m.thing != null && !m.thing.Destroyed) m.thing.Destroy();
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "delivered", HomeQuests.RewardWords(m, m.reward));
        }

        public override LookTargets Targets(Mission m)
        {
            var map = m.Map;
            var t = m.thing ?? (map != null ? HomeQuests2.Pieces(map, PieceDef(m)).FirstOrDefault() : null);
            return t != null && t.Spawned ? new LookTargets(t) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var map = m.Map;
            var def = PieceDef(m);
            if (def == null || map == null) return null;
            if (m.stage == 2) return "CC_HomeQ2_Wedding_Collecting".Translate((m.faction?.Name ?? "?").Named("FACTION"), (m.tale ?? def.label).Named("THING")).Resolve();
            var best = HomeQuests2.Pieces(map, def).FirstOrDefault();
            if (best != null && (best is MinifiedThing mt ? mt.InnerThing : best).TryGetQuality(out var q))
                return "CC_HomeQ2_Wedding_Ready".Translate((m.tale ?? def.label).Named("THING"), q.GetLabel().Named("QUALITY")).Resolve();
            return (def.Minifiable ? "CC_HomeQ2_Wedding_NotReadyPacked" : "CC_HomeQ2_Wedding_NotReady").Translate((m.tale ?? def.label).Named("THING")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "make it now", "the day now", "delivered", "missed" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            var def = PieceDef(m);
            switch (outcome)
            {
                case "make it now":
                {
                    if (def == null) return;
                    var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                    var t = ThingMaker.MakeThing(def, stuff);
                    t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Colony);
                    Thing put = def.Minifiable ? t.MakeMinified() : t;
                    GenPlace.TryPlaceThing(put, DropCellFinder.TradeDropSpot(map), map, ThingPlaceMode.Near);
                    return;
                }
                case "the day now":
                    m.deadline = Find.TickManager.TicksGame;
                    return;
                case "delivered":
                    Done(m, map, true);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "missed", null);
                    return;
            }
        }
    }

    // ===================== Q6. A sick animal tended =====================

    /// <summary>
    /// A neighbour's animal (one of their own pack or farm animals, every enabled mod's) is sick: the game's animal flu, its
    /// plague (only when the colony's doctor is good enough to treat it), or underfed from the road. Their drover walks it
    /// in and hands it over, and it is the colony's to tend back to health: its doctors tend it, its handlers feed it, the
    /// game's own care. Well again, their drover comes for it and pays; still sick when the days are up, they take it home as
    /// it is for half. Dead or lost, it ends badly; bonded to someone of the colony, they leave it where it is loved.
    /// </summary>
    public class HappeningWorker_MissionSickAnimal : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuests2Math.SickKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 2;

        private static Pawn Doctor(Map map) =>
            HomeQuests.Officer(map, Interplay.Physician, SkillDefOf.Medicine) is Pawn p && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) ? p
                : IncidentWorker_Happening.Colonists(map).Where(c => !c.DevelopmentalStage.Juvenile() && !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                    .OrderByDescending(c => c.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();

        private static readonly string[] Farm = { "Horse", "Donkey", "Muffalo", "Dromedary", "Yak", "Cow", "Alpaca", "Goat", "Sheep", "Elk", "Caribou" };

        /// <summary>One of their own animals: a pack animal their caravans use on this land, else a farm animal that would be fine here now.</summary>
        private static PawnKindDef AnimalKind(Faction f, Map map)
        {
            var k = f != null ? Couriers.AnimalKind(f, map) : null;
            if (k?.race?.race != null && k.RaceProps.Animal) return k;
            return PokeWorldLink.WildKinds(Farm.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(x => x?.race?.race != null && x.RaceProps.Animal && !x.RaceProps.predator
                                                                                                                   && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(x.race, 0f)))
                .RandomElementWithFallback();
        }

        protected override bool Possible(Map map)
        {
            var f = Neighbor(map);
            return f != null && Doctor(map) != null && AnimalKind(f, map) != null;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            var doctor = Doctor(map);
            var kind = AnimalKind(m.faction, map);
            if (m.faction == null || doctor == null || kind == null) return false;
            Pawn x = null;
            try
            {
                float age = kind.RaceProps.lifeExpectancy * Rand.Range(0.25f, 0.5f);
                x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, m.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    fixedBiologicalAge: age, fixedChronologicalAge: age));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the sick animal made", e);
            }
            var drover = HomeQuests.Person(m.faction, new FloatRange(22f, 60f), Couriers.PersonKind(m.faction));
            if (x == null || drover == null)
            {
                if (x != null) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
                Arrivals.Discard(drover);
                return false;
            }
            string name = More?.names.NullOrEmpty() ?? true ? kind.label : Flavor.PickText(More.names);
            x.Name = new NameSingle(name);
            Arrivals.Hold(x);
            m.x = x;
            m.group.Add(drover);
            m.name = name;
            m.variant = HomeQuests2Math.Illness(doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0, Rand.Value);
            m.tale = HomeQuests2.IllnessDef(m.variant)?.label ?? m.variant;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = doctor;
            m.wait = Rand.Range(0.3f, 0.6f);
            m.days = Mathf.Ceil(m.wait + HomeQuests2Math.Days(HomeQuest2.SickAnimal, Rand.Value));
            m.reward = HomeQuests2.Reward(HomeQuest2.SickAnimal, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.SickAnimal);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.SickAnimal);
            Fill(m, ctx);
            ctx.thing = x.def;
            return true;
        }

        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => Mathf.RoundToInt(mm.days), mm => mm.reward);

        private static Pawn Drover(Mission m) => m.group.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);

        public override bool Start(Mission m, Map map)
        {
            if (m.x == null || m.x.Dead || m.x.Discarded || Drover(m) == null) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            var drover = Drover(m);
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && x.Dead)
            {
                Died(m);
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    // Their drover walks it in from the road, sick, and makes for the gate.
                    HomeQuests2.MakeSick(x, m.variant);
                    var gate = PlaySpots.GateCell(map, null);
                    if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
                    if (drover == null || drover.Dead || Arrivals.Visit(new List<Pawn> { drover, x }, map, m.faction, gate, 8f) == null)
                    {
                        Missions.Finish(m, MissionState.Failed, "ended", null);
                        return;
                    }
                    m.stage = 1;
                    m.due = now + GenDate.TicksPerHour * 6;
                    Tell.Message(NewsKind.World, Importance.Important, map, "CC_HomeQ2_Sick_Coming".Translate((drover.LabelShort).Named("DROVER"), (m.name ?? "?").Named("NAME"),
                        (m.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(new Thing[] { drover, x }), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                case 1:
                {
                    if (!x.Spawned || x.Map != map)
                    {
                        Missions.Finish(m, MissionState.Failed, "ended", null);
                        return;
                    }
                    // At the gate (or long enough on the way): the drover hands it over and goes back up the road.
                    var flag = drover?.GetLord()?.CurLordToil?.FlagLoc ?? IntVec3.Invalid;
                    bool there = drover != null && drover.Spawned && flag.IsValid && drover.Position.InHorDistOf(flag, 8f);
                    if (!there && now < m.due && drover != null && drover.Spawned && !drover.Dead) return;
                    try
                    {
                        Lords.Leave(x);
                        x.SetFaction(Faction.OfPlayer);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("quests at home: the sick animal handed over", e);
                    }
                    HomeQuests2.Say(drover, "Rimfolk_Pool_HomeQ2_SickHanded");
                    if (drover != null && drover.Spawned) Arrivals.SendAway(new[] { drover });
                    m.stage = 2;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                case 2:
                    Tended(m, map, now);
                    return;
                default:
                    // Their drover has come for it: theirs now, or gone off with them.
                    if (x.Faction == m.faction || !x.Spawned && !x.Dead && x.Faction != Faction.OfPlayer) Handed(m, map, false);
                    else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Handed(m, map, true);
                    return;
            }
        }

        /// <summary>While it is the colony's: well again, gone, or the days up; else it is being looked after.</summary>
        private void Tended(Mission m, Map map, int now)
        {
            var x = m.x;
            bool ours = x.Faction == Faction.OfPlayer && (x.Spawned && x.Map == map || x.GetCaravan() != null);
            bool sick = HomeQuests2.Sick(x, m.variant);
            var end = HomeQuests2Math.SickOutcome(x.Dead, ours, sick, HappeningWorker_MissionRunaway.Bonded(x), now >= m.deadline);
            switch (end)
            {
                case SickEnd.Sick:
                    return;
                case SickEnd.Died:
                    Died(m);
                    return;
                case SickEnd.Lost:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "lost", null);
                    return;
                case SickEnd.Bonded:
                    Missions.Goodwill(m, m.goodwill / 2);
                    Missions.Finish(m, MissionState.Done, "bonded", null, x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                    return;
            }
            // Well (or the days up): their drover comes for it within the day, and pays (half while it's still sick).
            if (!x.Spawned || x.Map != map) return;
            bool still = end == SickEnd.StillSick;
            if (still) m.reward = HomeQuestsMath.Tens(m.reward / 2f);
            m.outcome = still ? "stillsick" : "well";
            m.stage = 3;
            m.lastLook = now;
            if (!still)
            {
                string line = "CC_HomeQ2_Sick_Well".Translate((m.name ?? "?").Named("NAME"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
                Tell.Message(NewsKind.World, Importance.Notable, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
            }
            if (!Couriers.Fetch(map, x, m.faction, HomeQuests.PayThings(m, m.reward), x.Position, false)) Handed(m, map, true);
        }

        private static void Died(Mission m)
        {
            var x = m.x;
            Missions.Goodwill(m, -m.penalty * 2);
            Missions.Finish(m, MissionState.Failed, "died", null, x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
        }

        /// <summary>Back to them: their drover leads it home (as round 1's animal tamed for them), and the pay is the colony's.</summary>
        private void Handed(Mission m, Map map, bool now)
        {
            var x = m.x;
            if (now && x != null && !x.Dead && x.Spawned && x.Faction == Faction.OfPlayer)
            {
                try
                {
                    Lords.Leave(x);
                    x.roping?.BreakAllRopes();
                    if (x.playerSettings != null) x.playerSettings.Master = null;
                    x.SetFaction(m.faction);
                    if (!x.Downed && ReachabilityUtility.CanReachMapEdge(x))
                        LordMaker.MakeNewLord(m.faction, new LordJob_ExitMapBest(Pace.Errand, false, false), x.Map, new List<Pawn> { x });
                    else
                    {
                        x.DeSpawn();
                        if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the sick animal handed back", e);
                }
            }
            if (now) HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            bool still = m.outcome == "stillsick";
            Missions.Goodwill(m, still ? m.goodwill / 2 : m.goodwill);
            Missions.Finish(m, MissionState.Done, still ? "stillsick" : "well", HomeQuests.RewardWords(m, m.reward), x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid,
                still ? LetterDefOf.NeutralEvent : null);
        }

        public override void Release(Mission m)
        {
            foreach (var p in m.group.Prepend(m.x).Where(p => p != null && !p.Dead))
            {
                if (!p.Spawned && p.Faction != Faction.OfPlayer && m.started < 0) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override LookTargets Targets(Mission m) => m.x != null && m.x.Spawned ? new LookTargets(m.x) : base.Targets(m);

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            string name = m.name ?? x.LabelShort;
            if (m.stage < 2) return "CC_HomeQ2_Sick_Status0".Translate(name.Named("NAME"), (Drover(m)?.LabelShort ?? "?").Named("DROVER")).Resolve();
            if (m.stage >= 3) return "CC_HomeQ2_Sick_Status3".Translate(name.Named("NAME"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            if (!HomeQuests2.Sick(x, m.variant)) return "CC_HomeQ2_Sick_StatusWell".Translate(name.Named("NAME")).Resolve();
            string key = x.health.HasHediffsNeedingTend() ? "CC_HomeQ2_Sick_StatusTend" : "CC_HomeQ2_Sick_Status2";
            return key.Translate(name.Named("NAME"), (m.tale ?? "?").Named("ILLNESS")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "well now", "still sick", "died", "lost" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            var x = m.x;
            switch (outcome)
            {
                case "well now":
                    if (x != null && !x.Dead && x.health?.hediffSet != null)
                        foreach (var h in x.health.hediffSet.hediffs.Where(h => h.def == HomeQuests2.IllnessDef(m.variant) || h.TryGetComp<HediffComp_Immunizable>() != null).ToList())
                            x.health.RemoveHediff(h);
                    return;
                case "still sick":
                    m.deadline = Find.TickManager.TicksGame;
                    return;
                case "died":
                    Died(m);
                    return;
                default:
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "lost", null);
                    return;
            }
        }
    }

    // ===================== Q8. Guard their camp for a night =====================

    /// <summary>
    /// A neighbour's caravan, on its way past, asks to camp one night at the edge of the colony's land, where it is safer
    /// than the open road, and asks the colony to keep watch. They come in the late afternoon, make camp by a fire, eat what
    /// they carry, and sleep there with one of them on watch. Something may come for them in the night, one night in three
    /// at most (the shared harm budget's roll): a few raiders of a people at war with both, or a pack of the land's own
    /// predators gone after people, a real fight, the game's own. At dawn they pack up, pay (a quarter more when the night
    /// was fought through) and go. One of theirs dead in the night, it ends badly.
    /// </summary>
    public class HappeningWorker_MissionCampGuard : HappeningWorker_Mission
    {
        protected override string Kind => HomeQuests2Math.CampKind;

        protected override float RestDays => HomeQuests2Math.RestDays;

        protected override int Rating => 2;

        /// <summary>A place for the camp: open ground at the edge of the land, far from the middle of the colony.</summary>
        private static bool CampCell(Map map, out IntVec3 cell) =>
            HomeQuests.FarCell(map, HomeQuests2Math.CampMin, c => HomeQuests.NearEdge(map, c, 18) && c.GetTerrain(map)?.IsWater != true, out cell);

        protected override bool Possible(Map map) => Neighbor(map) != null && CampCell(map, out _);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null || !CampCell(map, out var cell)) return false;
            int n = Rand.RangeInclusive(3, 4);
            for (int i = 0; i < n; i++)
            {
                var p = HomeQuests.Person(m.faction, new FloatRange(20f, 55f), i == 0 ? Arrivals.PersonKind(m.faction) : Couriers.PersonKind(m.faction));
                if (p == null) continue;
                if (m.x == null) m.x = p;
                else m.group.Add(p);
            }
            if (m.x == null) return false;
            // A pack animal of theirs, where their caravans use one on this land.
            var pack = Couriers.AnimalKind(m.faction, map);
            if (pack != null)
            {
                try
                {
                    var a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pack, m.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false));
                    if (a != null)
                    {
                        Arrivals.Hold(a);
                        m.group.Add(a);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quests at home: the caravan's pack animal", e);
                }
            }
            m.name = m.x.LabelShort;
            m.cell = cell;
            m.tale = HomeQuests.Direction(map, cell);
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HomeQuests.Officer(map, Interplay.Marshal, SkillDefOf.Shooting);
            if (m.a == null) return false;
            m.days = 2f;
            m.target = -1;
            m.reward = HomeQuests2.Reward(HomeQuest2.CampGuard, m.faction, map);
            m.goodwill = HomeQuests2Math.Goodwill(HomeQuest2.CampGuard);
            m.penalty = HomeQuests2Math.Penalty(HomeQuest2.CampGuard);
            Fill(m, ctx);
            ctx.stranger = m.x;
            ctx.count = People(m).Count();
            return true;
        }

        /// <summary>The terms: {COUNT} how many of them, {TALE} which way, {ITEM} the pay.</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => 0, mm => mm.reward);

        /// <summary>Their people (not the pack animal).</summary>
        private static IEnumerable<Pawn> People(Mission m) => m.group.Prepend(m.x).Where(p => p != null && p.RaceProps.Humanlike).Distinct();

        private static IEnumerable<Pawn> All(Mission m) => m.group.Prepend(m.x).Where(p => p != null).Distinct();

        public override bool Start(Mission m, Map map)
        {
            if (!People(m).Any(p => !p.Dead && !p.Discarded)) return false;
            if (!m.cell.IsValid || !m.cell.InBounds(map) || !m.cell.Standable(map))
            {
                if (!CampCell(map, out var cell)) return false;
                m.cell = cell;
                m.tale = HomeQuests.Direction(map, cell);
            }
            // They come in the late afternoon, to make camp by six.
            m.due = Find.TickManager.TicksGame + HomeQuests2.TicksUntilHour(map, HomeQuests2Math.CampHour - 2, 0.25f);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var people = People(m).ToList();
            if (people.Count == 0 || people.All(p => p.Discarded))
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage == 0)
            {
                if (now >= m.due) Come(m, map, now);
                return;
            }
            // Someone of theirs dead in the night: it ends badly, now.
            if (people.Any(p => p.Dead || p.IsPrisonerOfColony))
            {
                Lost(m, map);
                return;
            }
            Camp(m, map);
            int hour = GenLocalDate.HourOfDay(map);
            if (m.stage == 1 && m.target > 0 && now >= m.target)
            {
                Attack(m, map, now);
                return;
            }
            // Dawn, after a night at the camp, and nothing about the camp still fighting: they pack up and go.
            bool dawn = hour >= HomeQuests2Math.DawnHour && hour < 12 && now - m.lastLook > GenDate.TicksPerHour * 6;
            if (!dawn && now < m.deadline) return;
            if (Threat(m, map) && now < m.deadline) return;
            End(m, map);
        }

        /// <summary>They walk in from the edge to the camp's place with food enough for the night, and make camp there by a fire.</summary>
        private void Come(Mission m, Map map, int now)
        {
            var all = All(m).Where(p => !p.Dead && !p.Discarded && !p.Spawned).ToList();
            float hours = HomeQuests2Math.HoursUntil(GenLocalDate.HourFloat(map), HomeQuests2Math.DawnHour, 6f) + 4f;
            if (all.Count == 0 || Arrivals.Visit(all, map, m.faction, m.cell, hours) == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var p in all.Where(p => p.RaceProps.Humanlike)) Hosting.Provision(p, 1f);
            Fire(m, map);
            m.stage = 1;
            m.lastLook = now;
            // Whether something comes for them tonight, and when: one roll, within the shared harm budget.
            m.target = -1;
            if (Harm.On && Harm.SystemOn(HarmKinds.CampNight.system) && IncidentWorker_Happening.FightsPossible && Harm.Roll(HarmKinds.CampNight, HomeQuests2Math.AttackChance))
                m.target = now + HomeQuests2.TicksUntilHour(map, HomeQuests2Math.AttackHour(Rand.Value) % 24, 1f);
            string line = Line(More?.ready, m, map, null, null);
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(all), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Their campfire, lit at the camp's place, theirs (gone with them in the morning).</summary>
        private static void Fire(Mission m, Map map)
        {
            try
            {
                var def = ThingDefOf.Campfire;
                if (def == null) return;
                IntVec3 at = IntVec3.Invalid;
                foreach (var c in GenRadial.RadialCellsAround(m.cell, 3f, true))
                    if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && c.GetFirstThing(map, def) == null && GenConstruct.CanPlaceBlueprintAt(def, c, Rot4.North, map).Accepted)
                    {
                        at = c;
                        break;
                    }
                if (!at.IsValid) return;
                var fire = ThingMaker.MakeThing(def);
                fire.SetFaction(m.faction);
                GenSpawn.Spawn(fire, at, map);
                var fuel = fire.TryGetComp<CompRefuelable>();
                if (fuel != null) fuel.Refuel(fuel.Props.fuelCapacity);
                m.thing = fire;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the caravan's fire", e);
            }
        }

        /// <summary>
        /// The camp's night: the hungry eat what they carry; after ten all but one of them lie down by the fire, the one on
        /// watch stays up; in the morning they get up.
        /// </summary>
        private static void Camp(Mission m, Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            bool night = hour >= 22 || hour < HomeQuests2Math.DawnHour;
            foreach (var p in People(m).Where(p => p.Spawned && !p.Dead && p.Map == map))
            {
                if (!night && p.CurJobDef == HostingDefOf.Council_GuestSleep)
                {
                    Hosting.GetUp(p);
                    continue;
                }
                if (!Hosting.Free(p)) continue;
                if ((p.needs?.food?.CurLevelPercentage ?? 1f) < Hosting.EatBelow)
                {
                    var pack = Hosting.Pack(p);
                    if (pack != null && Hosting.Eat(p, pack, null)) continue;
                }
                if (!night || p == m.x || Threat(m, map)) continue;
                var cell = SleepCell(m, map, p);
                if (!cell.IsValid) continue;
                var job = JobMaker.MakeJob(HostingDefOf.Council_GuestSleep, cell);
                job.expiryInterval = GenDate.TicksPerHour * 9;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }

        private static IntVec3 SleepCell(Mission m, Map map, Pawn p)
        {
            foreach (var c in GenRadial.RadialCellsAround(m.cell, 5f, true).InRandomOrder())
                if (c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && p.CanReach(c, PathEndMode.OnCell, Danger.Some)
                    && !map.pawnDestinationReservationManager.IsReserved(c))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>Something hostile to them still up and about near the camp.</summary>
        private static bool Threat(Mission m, Map map)
        {
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (!p.Dead && !p.Downed && p.Position.InHorDistOf(m.cell, 45f) && (p.HostileTo(m.faction) || p.InMentalState && p.MentalStateDef == MentalStateDefOf.Manhunter))
                    return true;
            return false;
        }

        /// <summary>
        /// What comes in the night: a few raiders of a people at war with both, or a pack of the land's predators gone after
        /// people, in from the edge near the camp. Everyone at the camp is woken, and the colony is told at once.
        /// </summary>
        private void Attack(Mission m, Map map, int now)
        {
            m.target = -1;
            m.stage = 2;
            var enemy = Find.FactionManager.AllFactionsListForReading
                .Where(f => Raids.CanRaid(f) && f.def.humanlikeFaction && f.HostileTo(m.faction) && f.HostileTo(Faction.OfPlayer))
                .RandomElementByWeightWithFallback(f => Mathf.Pow(0.35f, Mathf.Abs(HomeQuests.AgeOf(f) - HomeQuests.AgeOf(m.faction))));
            var beasts = map.Biome == null ? new List<PawnKindDef>()
                : PokeWorldLink.WildKinds(BiomeAnimals.Of(map.Biome)).Where(k => k?.race?.race != null && k.RaceProps.Animal && k.RaceProps.predator && !k.RaceProps.Insect
                                                                                && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)).ToList();
            bool raiders = enemy != null && (beasts.Count == 0 || Rand.Chance(HomeQuests2Math.RaiderChance));
            float points = HomeQuests2Math.AttackPoints(StorytellerUtility.DefaultThreatPointsNow(map));
            var came = new List<Pawn>();
            try
            {
                if (!Entry(m, map, out var entry)) entry = IntVec3.Invalid;
                if (entry.IsValid && raiders)
                {
                    var kind = HomeQuests.FighterKind(enemy, points / 2f);
                    int n = kind == null ? 0 : HomeQuests2Math.Attackers(points, kind.combatPower);
                    for (int i = 0; i < n; i++)
                    {
                        var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, enemy, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                        if (p == null) continue;
                        GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                        came.Add(p);
                    }
                    if (came.Count > 0) LordMaker.MakeNewLord(enemy, new LordJob_AssaultColony(enemy, false, true, false, false, false), map, came);
                    m.other = enemy;
                }
                else if (entry.IsValid && beasts.Count > 0)
                {
                    var kind = beasts.RandomElementByWeight(k => Mathf.Max(0.1f, k.combatPower));
                    int n = HomeQuests2Math.Attackers(points, kind.combatPower);
                    for (int i = 0; i < n; i++)
                    {
                        var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                        if (p == null) continue;
                        GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                        p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter, null, true);
                        came.Add(p);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: what came for the camp", e);
            }
            if (came.Count == 0)
            {
                // Nothing could come after all: a quiet night.
                m.stage = 1;
                return;
            }
            m.variant = raiders ? "raiders" : "beasts";
            HarmBook.Instance?.Came(HarmKinds.CampNight, now);
            Harm.BudgetChanged();
            // Everyone at the camp up and ready.
            foreach (var p in People(m).Where(p => p.Spawned && !p.Dead))
            {
                if (p.CurJobDef == HostingDefOf.Council_GuestSleep) Hosting.GetUp(p);
                if (!p.Awake()) RestUtility.WakeUp(p);
            }
            HomeQuests2.Say(m.x, "Rimfolk_Pool_HomeQ2_CampAlarm", null, m.variant);
            string label = (raiders ? "CC_HomeQ2_Camp_RaidersTitle" : "CC_HomeQ2_Camp_BeastsTitle").Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            string key = raiders ? "CC_HomeQ2_Camp_Raiders" : came.Count > 1 ? "CC_HomeQ2_Camp_Beasts" : "CC_HomeQ2_Camp_Beast";
            string what = came.Count > 1 ? came[0].kindDef.GetLabelPlural(came.Count) : came[0].KindLabel;
            string text = key.Translate((m.faction?.Name ?? "?").Named("FACTION"), (m.tale ?? "?").Named("WAY"), (enemy?.Name ?? "?").Named("ENEMY"), what.Named("THING")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Important, map, label, text, LetterDefOf.ThreatSmall, new LookTargets(came));
        }

        /// <summary>An edge cell near the camp that the attackers can walk from to it.</summary>
        private static bool Entry(Mission m, Map map, out IntVec3 cell) =>
            CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && c.InHorDistOf(m.cell, 45f)
                                                      && map.reachability.CanReach(c, m.cell, PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly), map, 0f, out cell)
            || RCellFinder.TryFindRandomPawnEntryCell(out cell, map, 0f, false, null);

        /// <summary>One of theirs died (or was taken) in the night: they leave with their dead, and blame the colony.</summary>
        private void Lost(Mission m, Map map)
        {
            PutOut(m);
            Arrivals.SendAway(People(m).Where(p => !p.Dead && p.Spawned));
            HomeQuests2.LeadOff(m.group, map, m.faction);
            Missions.Goodwill(m, -m.penalty * 2);
            var dead = People(m).FirstOrDefault(p => p.Dead);
            Missions.Finish(m, MissionState.Failed, "lost", null, dead?.Corpse != null && dead.Corpse.Spawned ? new LookTargets(dead.Corpse) : LookTargets.Invalid);
        }

        /// <summary>Dawn: they pack up, pay and go (a quarter more when the night was fought through).</summary>
        private void End(Mission m, Map map)
        {
            var people = People(m).ToList();
            int dead = people.Count(p => p.Dead || p.IsPrisonerOfColony);
            bool attacked = m.variant == "raiders" || m.variant == "beasts";
            var end = HomeQuests2Math.CampOutcome(dead, attacked);
            if (end == NightEnd.Lost)
            {
                Lost(m, map);
                return;
            }
            int pay = HomeQuests2Math.CampPay(m.reward, end);
            var leader = people.FirstOrDefault(p => p.Spawned && !p.Dead && !p.Downed && p.Map == map);
            // Paid at their camp, set down there for the colony to fetch, as they pack up.
            HomeQuests2.PayInPerson(m, map, leader, pay);
            HomeQuests2.Say(leader, "Rimfolk_Pool_HomeQ2_CampLeave", null, attacked ? "attacked" : "quiet");
            PutOut(m);
            Arrivals.SendAway(All(m).Where(p => p.Spawned && !p.Dead));
            HomeQuests2.LeadOff(m.group, map, m.faction);
            Missions.Goodwill(m, attacked ? m.goodwill + 4 : m.goodwill);
            Missions.Finish(m, MissionState.Done, attacked ? "defended" : "quiet", HomeQuests.RewardWords(m, pay), leader != null ? new LookTargets(leader) : LookTargets.Invalid);
        }

        /// <summary>Their fire is put out and packed away with them.</summary>
        private static void PutOut(Mission m)
        {
            try
            {
                if (m.thing != null && !m.thing.Destroyed && m.thing.def == ThingDefOf.Campfire) m.thing.Destroy(DestroyMode.Vanish);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the caravan's fire put out", e);
            }
            m.thing = null;
        }

        public override void Release(Mission m)
        {
            PutOut(m);
            foreach (var p in All(m).Where(p => !p.Dead).ToList())
            {
                if (p.CurJobDef == HostingDefOf.Council_GuestSleep) Hosting.GetUp(p);
                if (!p.Spawned && m.started < 0 && p.GetCaravan() == null) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var here = All(m).Where(p => p.Spawned).ToList();
            if (here.Count > 0) return new LookTargets(here);
            var map = m.Map;
            return map != null && m.cell.IsValid ? new LookTargets(new TargetInfo(m.cell, map)) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            int n = People(m).Count();
            if (m.stage == 0) return "CC_HomeQ2_Camp_Status0".Translate((m.name ?? "?").Named("NAME"), n.Named("COUNT"), (m.tale ?? "?").Named("WAY")).Resolve();
            if (m.stage == 2) return "CC_HomeQ2_Camp_Status2".Translate((m.faction?.Name ?? "?").Named("FACTION")).Resolve();
            return "CC_HomeQ2_Camp_Status1".Translate((m.faction?.Name ?? "?").Named("FACTION"), (m.tale ?? "?").Named("WAY")).Resolve();
        }

        // ---------------- Dev mode ----------------

        public static readonly string[] DebugOutcomes = { "attack now", "dawn now", "quiet", "defended", "lost" };

        public void DebugEnd(Mission m, Map map, string outcome)
        {
            int now = Find.TickManager.TicksGame;
            switch (outcome)
            {
                case "attack now":
                    if (m.stage == 1) Attack(m, map, now);
                    return;
                case "dawn now":
                    m.lastLook = now - GenDate.TicksPerDay;
                    m.deadline = now;
                    return;
                case "quiet":
                    m.variant = null;
                    End(m, map);
                    return;
                case "defended":
                    m.variant = "raiders";
                    End(m, map);
                    return;
                default:
                    Lost(m, map);
                    return;
            }
        }
    }
}
