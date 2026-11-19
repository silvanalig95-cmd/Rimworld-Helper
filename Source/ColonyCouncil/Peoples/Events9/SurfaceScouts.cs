using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 94. Surface scouts (the Sunken Vault) =====================

    /// <summary>The scouts' week: the afternoons taught, the teacher at their fire now, the one sky-sick, the one lost, the one mauled, and how it went.</summary>
    public class ScoutState : CampWorkState
    {
        public int lessonDay = -1, lessons, sickUntil = -1, lostUntil = -1;
        public Pawn teacher, sick, lost, mauler;
        public string lesson;
        public IntVec3 lostCell = IntVec3.Invalid;
        public bool skyRolled, lostRolled, maulRolled, mauled, learnedNoted, found;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lessonDay, "lessonDay", -1);
            Scribe_Values.Look(ref lessons, "lessons");
            Scribe_Values.Look(ref sickUntil, "sickUntil", -1);
            Scribe_Values.Look(ref lostUntil, "lostUntil", -1);
            Scribe_References.Look(ref teacher, "teacher");
            Scribe_References.Look(ref sick, "sick");
            Scribe_References.Look(ref lost, "lost");
            Scribe_References.Look(ref mauler, "mauler");
            Scribe_Values.Look(ref lesson, "lesson");
            Scribe_Values.Look(ref lostCell, "lostCell", IntVec3.Invalid);
            Scribe_Values.Look(ref skyRolled, "skyRolled");
            Scribe_Values.Look(ref lostRolled, "lostRolled");
            Scribe_Values.Look(ref maulRolled, "maulRolled");
            Scribe_Values.Look(ref mauled, "mauled");
            Scribe_Values.Look(ref learnedNoted, "learnedNoted");
            Scribe_Values.Look(ref found, "found");
        }
    }

    /// <summary>
    /// Surface scouts (item 94): three of the Sunken Vault's surface-scouts camp a week by the colony to learn the surface. Each afternoon
    /// they ask to be taught (plants, animals or shooting, in turn): the colony's best at it goes to their fire and teaches two hours, the
    /// scouts learning it (Plants, Animals, Shooting), and the vault remembers it as supply and learning, which holds its age (the age
    /// rule's gifts and learners, as round G's craft taught does). They trade their last odds and ends (the vault's goods, set down at their
    /// fire for the colony's silver), and one may ask to stay (the camp's own join). It can turn, once a stay each: one is sky-sick and
    /// won't come out of the shelter (someone of the colony may talk them out), one goes out at dusk and is lost on the land at night (the
    /// colony may look for them; at first light they find their own way back), or one walks up to a wild beast they don't know to fear and
    /// it turns on them (the game's own attack; the wild one too close's kind in the shared harm budget).
    /// </summary>
    public static class SurfaceScouts
    {
        public const string EventDef = "Rimfolk_Evt_SurfaceScouts";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_SurfaceScouts");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static ScoutState StateOf(Camp c) => c?.workState as ScoutState;

        private static Camp CampOf(Pawn p) => p?.Map == null ? null : CampTracker.VisitorsAt(p.Map).FirstOrDefault(c => c.kind == Kind && c.Has(p));

        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                for (int i = 0; i < PeopleEvents9Math.ScoutPeople; i++) people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, new FloatRange(16f, 40f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents9Math.ScoutDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new ScoutState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the surface scouts", e);
                return null;
            }
        }

        /// <summary>Their hour: the afternoon's lesson asked for, and the turns, once a stay each.</summary>
        public static void Hour(Camp c, ScoutState st, int hour)
        {
            int day = GenDate.DaysPassed;
            if (st.sick != null && (Now > st.sickUntil || st.sick.Dead || !st.sick.Spawned))
            {
                if (st.sick.Spawned && !st.sick.Dead) c.Note(c.Text("CC_PEv9_ScoutSkyPassed", st.sick), MessageTypeDefOf.NeutralEvent, Importance.Notable, st.sick);
                End(st.sick);
                st.sick = null;
            }
            if (st.lost != null && (Now > st.lostUntil || st.lost.Dead || !st.lost.Spawned)) Home(c, st, null);
            if (MapFacts.ThreatOn(c.map) || c.DaysHere < 0.5f) return;
            if (PeopleEvents9Math.LessonHour(hour) && st.lessonDay != day && Tell.CanAsk)
            {
                st.lessonDay = day;
                var ask = c.AskByKey("lesson" + PeopleEvents9Math.LessonOn(day));
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            if (c.DaysHere < 1f) return;
            if (!st.skyRolled && hour >= 7 && hour <= 9) SkySick(c, st);
            else if (!st.lostRolled && hour >= 20 && hour <= 22) Lose(c, st);
            else if (!st.maulRolled && c.DaysHere >= 2f && hour >= 10 && hour <= 16) Maul(c, st);
        }

        private static IEnumerable<Pawn> Scouts(Camp c) => c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && !p.InMentalState && p.Faction == c.faction);

        private static void End(Pawn p)
        {
            if (p != null && p.Spawned && !p.Dead && (p.CurJobDef == JobDefOf.LayDownAwake || p.CurJobDef == JobDefOf.LayDown)) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>Once a stay, in the morning: one of them is sky-sick and keeps to their shelter a day; the camp asks the colony to talk them out.</summary>
        public static void SkySick(Camp c, ScoutState st)
        {
            st.skyRolled = true;
            if (!Rand.Chance(PeopleEvents9Math.SkySickChance)) return;
            var p = Scouts(c).Where(x => x != c.leader).RandomElementWithFallback();
            var bed = p == null ? null : c.BedFor(p);
            if (p == null || bed == null || !p.CanReach(bed, PathEndMode.OnCell, Danger.Some)) return;
            var job = JobMaker.MakeJob(JobDefOf.LayDownAwake, bed);
            job.expiryInterval = 10 * GenDate.TicksPerHour;
            job.checkOverrideOnExpire = true;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            st.sick = p;
            st.sickUntil = Now + 10 * GenDate.TicksPerHour;
            c.SayMoment(p, "skysick");
            var ask = c.AskByKey("talkout");
            if (ask != null && Tell.CanAsk && c.AskBlocked(ask) == null) c.Request(ask);
        }

        /// <summary>Talked out of the shelter by one of the colony's: they come out, the talker the better at it, told.</summary>
        public static void TalkedOut(Pawn by, Pawn scout)
        {
            var c = scout == null ? null : CampOf(scout);
            var st = StateOf(c);
            if (st == null || st.sick != scout) return;
            End(scout);
            st.sick = null;
            by?.skills?.Learn(SkillDefOf.Social, 300f, true);
            c.SayMoment(scout, "out");
            c.Note(c.Text("CC_PEv9_ScoutTalkedOut", scout, by), MessageTypeDefOf.PositiveEvent, Importance.Notable, scout, true);
        }

        /// <summary>
        /// Once a stay, at dusk in mild weather: one of them goes out to look at the land and is lost on it for the night, lying out somewhere
        /// a long walk from the camp; they find their own way back at first light, or the colony looks for them.
        /// </summary>
        public static void Lose(Camp c, ScoutState st)
        {
            st.lostRolled = true;
            var map = c.map;
            var p = Scouts(c).Where(x => x != c.leader && x != st.sick).RandomElementWithFallback();
            if (p == null || !p.SafeTemperatureRange().Includes(map.mapTemperature.OutdoorTemp) || !Rand.Chance(PeopleEvents9Math.LostChance)) return;
            var cell = FarCell(c, p);
            if (!cell.IsValid) return;
            var job = JobMaker.MakeJob(JobDefOf.LayDown, cell);
            job.expiryInterval = 9 * GenDate.TicksPerHour;
            job.checkOverrideOnExpire = true;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            st.lost = p;
            st.lostCell = cell;
            int hour = GenLocalDate.HourOfDay(map);
            st.lostUntil = Now + ((29 - hour) % 24) * GenDate.TicksPerHour;
            var ask = c.AskByKey("search");
            if (ask != null && Tell.CanAsk && c.AskBlocked(ask) == null) c.Request(ask);
            else c.Note(c.Text("CC_PEv9_ScoutLost", p), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }

        /// <summary>A place a long walk from the camp, away from the colony, on open ground they can reach.</summary>
        private static IntVec3 FarCell(Camp c, Pawn p)
        {
            var map = c.map;
            var away = c.center - CampSite.ColonyCenter(map);
            float len = Mathf.Max(1f, Mathf.Sqrt(away.x * away.x + away.z * away.z));
            for (int tries = 0; tries < 6; tries++)
            {
                float dist = Rand.Range(28f, 42f), turn = Rand.Range(-60f, 60f) * Mathf.Deg2Rad, dx = away.x / len, dz = away.z / len;
                var aim = new IntVec3(c.center.x + Mathf.RoundToInt((dx * Mathf.Cos(turn) - dz * Mathf.Sin(turn)) * dist), 0,
                    c.center.z + Mathf.RoundToInt((dx * Mathf.Sin(turn) + dz * Mathf.Cos(turn)) * dist)).ClampInsideMap(map);
                if (CellFinder.TryFindRandomCellNear(aim, map, 8, x => x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && x.GetEdifice(map) == null && !(x.GetTerrain(map)?.IsWater ?? false)
                                                                     && !map.areaManager.Home[x] && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var spot))
                    return spot;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The searcher got to where they lie: found, and walked back to the camp's fire.</summary>
        public static void Searched(Pawn by)
        {
            var c = by?.Map == null ? null : CampTracker.VisitorsAt(by.Map).FirstOrDefault(x => x.kind == Kind && StateOf(x)?.lost != null);
            var st = StateOf(c);
            if (st?.lost == null) return;
            Home(c, st, by);
        }

        /// <summary>The lost one home: walked back by the one who found them, or by themselves at first light; told.</summary>
        private static void Home(Camp c, ScoutState st, Pawn by)
        {
            var p = st.lost;
            st.lost = null;
            st.lostCell = IntVec3.Invalid;
            if (p == null || p.Dead || !p.Spawned) return;
            End(p);
            if (CellFinder.TryFindRandomCellNear(c.FireCell, c.map, 3, x => x.Standable(c.map) && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var back))
            {
                p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobCondition.InterruptForced, null, false, true);
                if (by != null && by.Spawned && !by.Downed) by.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc);
            }
            if (by != null)
            {
                st.found = true;
                c.Note(c.Text("CC_PEv9_ScoutFound", p, by), MessageTypeDefOf.PositiveEvent, Importance.Notable, p, true);
            }
            else c.Note(c.Text("CC_PEv9_ScoutBack", p), MessageTypeDefOf.NeutralEvent, Importance.Notable, p);
        }

        /// <summary>A wild beast near their camp a scout from below might walk up to: a hunter or a big one, wild, on its feet, out of the fog.</summary>
        public static Pawn Beast(Camp c, Pawn p) =>
            c.map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps.Animal && a.Faction == null && !a.Downed && a.MentalStateDef == null && (a.RaceProps.predator || a.BodySize >= 1.5f)
                                                      && a.Position.InHorDistOf(c.center, 40f) && !a.Position.Fogged(c.map) && p.CanReach(a, PathEndMode.Touch, Danger.Some))
                .RandomElementWithFallback();

        /// <summary>Once a stay, by day: at the wild one too close's rate in the shared harm budget, a scout walks up to a beast they don't know to fear.</summary>
        public static void Maul(Camp c, ScoutState st)
        {
            st.maulRolled = true;
            var p = Scouts(c).Where(x => x != st.sick && x != st.lost).RandomElementWithFallback();
            var beast = p == null ? null : Beast(c, p);
            if (beast == null || !Harm.On || !Harm.SystemOn(HarmKinds.TooClose.system) || !Harm.Roll(HarmKinds.TooClose, PeopleEvents9Math.MaulChance)) return;
            st.mauler = beast;
            if (!PeopleEvents9.Send(p, PeopleEvents9.LookAtJob, beast, 300)) st.mauler = null;
        }

        /// <summary>The scout at the beast: it turns on them (the game's own attack), told as it happens.</summary>
        public static void Looked(Pawn scout, Pawn beast)
        {
            var c = CampOf(scout);
            var st = StateOf(c);
            if (st == null || beast == null || st.mauler != beast || beast.Dead || beast.Downed || !beast.Spawned) return;
            st.mauler = null;
            try
            {
                beast.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, scout), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the beast turns", e);
                return;
            }
            st.mauled = true;
            HarmBook.Instance?.Came(HarmKinds.TooClose, Now);
            Tell.Letter(NewsKind.Land, Importance.Important, c.map, "CC_PEv9_ScoutMauledLabel".Translate(beast.LabelShort).Resolve(),
                c.Text("CC_PEv9_ScoutMauled", scout, null, 0, beast.LabelShort), LetterDefOf.NegativeEvent, new LookTargets(scout), c.faction);
        }

        /// <summary>An afternoon's lesson at their fire done: the scouts the better at it, the vault's memory of supply and learning, told.</summary>
        public static void Taught(Pawn teacher)
        {
            var c = teacher?.Map == null ? null : CampTracker.VisitorsAt(teacher.Map).FirstOrDefault(x => x.kind == Kind && StateOf(x)?.teacher == teacher);
            var st = StateOf(c);
            if (st == null) return;
            st.teacher = null;
            var skill = DefDatabase<SkillDef>.GetNamedSilentFail(st.lesson ?? "");
            if (skill == null) return;
            st.lessons++;
            foreach (var p in Scouts(c)) p.skills?.Learn(skill, PeopleEvents9Math.LessonXp, true);
            teacher.skills?.Learn(SkillDefOf.Social, PeopleEvents9Math.TeacherXp, true);
            VaultSupply.Noted(c.faction, PeopleEvents9Math.LessonWorth, skill.label);
            if (!st.learnedNoted && c.leader != null)
            {
                st.learnedNoted = true;
                FactionMemory.Note(c.faction, "learnedHere", c.leader.LabelShort);
            }
            c.SayMoment(c.leader ?? teacher, "learned");
            c.Note(c.Text("CC_PEv9_ScoutTaught", c.leader, teacher, 0, skill.label), MessageTypeDefOf.PositiveEvent, Importance.Notable, teacher, true);
        }

        /// <summary>The lesson begun: the teacher to their fire two hours, the scouts sat round it.</summary>
        public static void Lesson(Camp c, Pawn teacher, SkillDef skill)
        {
            var st = StateOf(c);
            if (st == null || teacher == null || skill == null) return;
            st.teacher = teacher;
            st.lesson = skill.defName;
            var map = c.map;
            if (!PlayOuts.On || !PeopleEvents9.Send(teacher, PeopleEvents9.LessonJob, c.FireCell, PeopleEvents9Math.LessonTicks))
            {
                Taught(teacher);
                return;
            }
            foreach (var p in Scouts(c).Where(x => x != st.sick && x != st.lost))
                if (CellFinder.TryFindRandomCellNear(c.FireCell, map, 3, x => x.Standable(map) && x != c.FireCell && x.GetFirstPawn(map) == null && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell))
                    p.jobs.StartJob(JobMaker.MakeJob(CampDefOf.Council_CampSit, cell, c.FireCell), JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Their last odds and ends worth about <paramref name="worth"/>: the vault's goods by its age, then what any vault keeps.</summary>
        public static List<Thing> Odds(Faction f, float worth)
        {
            var list = new List<Thing>();
            var pool = HappeningWorker_VaultBarter.Crate(f).Select(x => x.def).Concat(new[] { ThingDefOf.ComponentIndustrial, ThingDefOf.MedicineIndustrial, ThingDefOf.MealSurvivalPack })
                .Where(d => d != null).Distinct().ToList();
            float left = Mathf.Max(30f, worth);
            for (int i = 0; i < PeopleEvents9Math.OddsMax && left > 0f; i++)
            {
                var d = pool.Where(x => x.BaseMarketValue <= left).RandomElementWithFallback();
                if (d == null) break;
                int n = Mathf.Clamp(Mathf.FloorToInt(left / Mathf.Max(1f, d.BaseMarketValue) / (PeopleEvents9Math.OddsMax - i)), 1, d.stackLimit);
                var t = ThingMaker.MakeThing(d, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null);
                t.stackCount = n;
                list.Add(t);
                left -= n * d.BaseMarketValue;
                pool.Remove(d);
            }
            return list;
        }

        public static void Leave(Camp c, ScoutState st)
        {
            if (c.Hostile || c.tension >= 50f) return;
            PeopleEvents9.Goodwill(c.faction, PeopleEvents9Math.ScoutsGoodwill, GoodwillWhy.CampWell);
        }
    }

    /// <summary>The surface scouts' ways: the afternoon's lesson, their odds and ends, the sky-sick, the lost, the beast.</summary>
    public class CampWorker_SurfaceScouts : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is ScoutState)) c.workState = new ScoutState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is ScoutState st))
            {
                Started(c);
                st = SurfaceScouts.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            SurfaceScouts.Hour(c, st, hour);
        }

        public override void Leaving(Camp c)
        {
            var st = SurfaceScouts.StateOf(c);
            if (st != null) SurfaceScouts.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = SurfaceScouts.StateOf(c);
            if (st == null) return;
            if (st.lessons >= 2) marks.Add("taught");
            if (st.mauled) marks.Add("mauled");
            if (st.found) marks.Add("found");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{LESSONS}") ? text : text.Replace("{LESSONS}", (SurfaceScouts.StateOf(c)?.lessons ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = SurfaceScouts.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "lessonPlants":
                case "lessonAnimals":
                case "lessonShooting":
                    return st.teacher != null ? "CC_PEv9_ScoutBusy".Translate().Resolve() : null;
                case "talkout":
                    return st.sick == null ? "CC_PEv9_ScoutNotSick".Translate().Resolve() : null;
                case "search":
                    return st.lost == null ? "CC_PEv9_ScoutNotLost".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A surface-scout of {FACTION} ({X}) at the gate: three of the vault's scouts would camp a week by the colony to learn the surface.</summary>
    public class HappeningWorker_SurfaceScouts : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            SurfaceScouts.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && SurfaceScouts.On(map) == null && PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.count = PeopleEvents9Math.ScoutFood;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PeopleEvents.AgeOf(ctx.faction) < 3) tags.Add("fallen");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (SurfaceScouts.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The scouts' camp made (SurfaceScouts.Start); <see cref="fed"/>: a week's food sent with them from the gate, remembered as supply.</summary>
    public class DilemmaEffect_SurfaceScouts : DilemmaEffect
    {
        public bool fed;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv9_Impact_Scouts".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv9_Impact_ScoutsBrief".Translate().Resolve(), "pev9scouts");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv9_Impact_ScoutsLearn".Translate().Resolve(), "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "pev9scoutslearn");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv9_Impact_ScoutsTurns".Translate().Resolve(), "CC_PEv9_Impact_ScoutsTurnsBrief".Translate().Resolve(), "pev9scoutsturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.ScoutsGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            SurfaceScouts.Start(hc);
            if (fed && hc.faction != null) VaultSupply.Noted(hc.faction, VaultSupply.Worth(0, hc.count), "CC_PEv9_SupplyFood".Translate().Resolve());
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The camp's ask: the colony's best at the afternoon's subject (<see cref="skill"/>) goes to their fire and teaches two hours.</summary>
    public class DilemmaEffect_ScoutLesson : DilemmaEffect
    {
        public SkillDef skill;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || skill == null) return;
            sheet.Play("CC_PEv9_Impact_Lesson".Translate(ctx.a.LabelShort, skill.label).Resolve(), "CC_PEv9_Impact_LessonBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PeoplesG_Impact_Supply".Translate(ctx.faction?.Name ?? "?").Resolve(), "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "vaultsupply");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SurfaceScouts);
            if (c != null) SurfaceScouts.Lesson(c, ctx.a, skill);
        }
    }

    /// <summary>The camp's ask: someone of the colony goes to talk the sky-sick scout out of the shelter, an hour.</summary>
    public class DilemmaEffect_ScoutTalkOut : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null) sheet.Play("CC_PEv9_Impact_TalkOut".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv9_Impact_TalkOutBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SurfaceScouts);
            var st = SurfaceScouts.StateOf(c);
            if (st?.sick == null || ctx.a == null) return;
            if (!PlayOuts.On || !PeopleEvents9.Send(ctx.a, PeopleEvents9.TalkOutJob, st.sick, GenDate.TicksPerHour)) SurfaceScouts.TalkedOut(ctx.a, st.sick);
        }
    }

    /// <summary>The camp's ask: someone of the colony goes out to look for the lost scout where they lie.</summary>
    public class DilemmaEffect_ScoutSearch : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null) sheet.Play("CC_PEv9_Impact_Search".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv9_Impact_SearchBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SurfaceScouts);
            var st = SurfaceScouts.StateOf(c);
            if (st?.lost == null || ctx.a == null) return;
            if (!PlayOuts.On || !PeopleEvents9.Send(ctx.a, PeopleEvents9.SearchJob, st.lost, 300)) SurfaceScouts.Searched(ctx.a);
        }
    }

    /// <summary>The camp's ask: their last odds and ends set down at their fire for the colony (the silver paid by the ask).</summary>
    public class DilemmaEffect_ScoutOdds : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv9_Impact_Odds".Translate(ctx?.faction?.Name ?? "?").Resolve(), "CC_PEv9_Impact_OddsBrief".Translate().Resolve(), "pev9odds");

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_SurfaceScouts);
            if (c?.faction == null) return;
            var goods = SurfaceScouts.Odds(c.faction, ctx.count);
            if (goods.Count == 0) return;
            Couriers.Loot(goods, c.map, c.FireCell, c.faction, c.leader);
            c.Note(c.Text("CC_PEv9_ScoutOdds", c.leader, null, 0, TextUtil.JoinAnd(goods.Select(t => t.LabelCap.ToString()).ToList())), MessageTypeDefOf.PositiveEvent, Importance.Notable, null, true);
        }
    }
}
