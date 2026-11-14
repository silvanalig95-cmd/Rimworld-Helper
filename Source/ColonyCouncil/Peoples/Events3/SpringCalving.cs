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
    // ===================== 13. Spring calving (the herding folk): their camp =====================

    public class CalvingState : CampWorkState
    {
        /// <summary>The herd's beasts in calf, the calves born at the camp, and the one whose birth goes hard.</summary>
        public List<Pawn> inCalf = new List<Pawn>(), calves = new List<Pawn>();
        public Pawn hard;
        /// <summary>The calf gone missing, where it lies, and when it went.</summary>
        public Pawn lost;
        public IntVec3 lostCell = IntVec3.Invalid;
        public int lostSince = -1, countDay = -1, hardSince = -1;
        /// <summary>The in-calf beasts made; the hard birth rolled, asked, done; the afterbirth's predator and the missing calf rolled; the hay given; a calf the colony's.</summary>
        public bool made, hardRolled, hardAsked, hardDone, predator, missingRolled, hay, calfOurs;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref inCalf, "inCalf", LookMode.Reference);
            Scribe_Collections.Look(ref calves, "calves", LookMode.Reference);
            Scribe_References.Look(ref hard, "hard");
            Scribe_References.Look(ref lost, "lost");
            Scribe_Values.Look(ref lostCell, "lostCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lostSince, "lostSince", -1);
            Scribe_Values.Look(ref countDay, "countDay", -1);
            Scribe_Values.Look(ref hardSince, "hardSince", -1);
            Scribe_Values.Look(ref made, "made");
            Scribe_Values.Look(ref hardRolled, "hardRolled");
            Scribe_Values.Look(ref hardAsked, "hardAsked");
            Scribe_Values.Look(ref hardDone, "hardDone");
            Scribe_Values.Look(ref predator, "predator");
            Scribe_Values.Look(ref missingRolled, "missingRolled");
            Scribe_Values.Look(ref hay, "hay");
            Scribe_Values.Look(ref calfOurs, "calfOurs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                inCalf ??= new List<Pawn>();
                inCalf.RemoveAll(p => p == null);
                calves ??= new List<Pawn>();
                calves.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Spring calving (item 13): in spring, the herders bring in-calf beasts to calve on the colony's sheltered ground for 8
    /// to 12 days (Rimfolk_Camp_SpringCalving), and count every head aloud at dusk. The calves are born at the camp by the
    /// game's own births (the game's pregnancy, set so the births come over the stay). A hard birth brings a herder to the gate
    /// asking for the colony's best hand at beasts (Rimfolk_Evt_HardBirth): the roll on its card decides whether both live
    /// (the colony gets the calf) or the mother is lost. They ask for hay and offer milk. It can turn: the afterbirth draws a
    /// predator in the night (as the camps' offal does, through the harm budget), or a calf goes missing and they search the
    /// colony's land for it with lanterns at night.
    /// </summary>
    public static class SpringCalving
    {
        public const string BirthKind = "pev:birth";
        public const string HardEvent = "Rimfolk_Evt_HardBirth";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_SpringCalving");

        public static JobDef BirthJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_HardBirth");

        public static JobDef LanternJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_LanternSearch");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static CalvingState StateOf(Camp c) => c?.workState as CalvingState;

        /// <summary>The camp made and set going by the colony's leave (DryWater's way), its people and herd walking in from the edge.</summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 10);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                center = CampLand.SiteFor(kind, map, center, hot);
                if (!center.IsValid) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new CalvingState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the calving camp", e);
                return null;
            }
        }

        /// <summary>The in-calf beasts of a herd: four to seven grown females of it, the game's own pregnancy set so they calve over the stay.</summary>
        public static void MakeInCalf(Camp c, CalvingState st)
        {
            st.made = true;
            var pregnant = HediffDefOf.Pregnant;
            float stay = Mathf.Max(3f, (c.untilTick == int.MaxValue ? c.stayTicks : c.untilTick - Now) / (float)GenDate.TicksPerDay);
            var grown = c.herd.Where(a => a != null && a.Spawned && !a.Dead && c.GroupOf(a)?.follow != true && a.ageTracker != null && a.ageTracker.Adult
                                          && a.RaceProps.gestationPeriodDays > 0f && a.def.GetCompProperties<CompProperties_EggLayer>() == null).ToList();
            int want = Rand.RangeInclusive(PeopleEvents3Math.InCalfMin, PeopleEvents3Math.InCalfMax);
            foreach (var a in grown.OrderByDescending(a => a.gender == Gender.Female).Take(want))
            {
                try
                {
                    if (a.gender != Gender.Female) a.gender = Gender.Female;
                    if (a.health.hediffSet.HasHediff(pregnant)) continue;
                    var h = (Hediff_Pregnant)HediffMaker.MakeHediff(pregnant, a);
                    h.SetParents(null, c.herd.FirstOrDefault(x => x != null && x != a && x.def == a.def && x.gender == Gender.Male), null);
                    a.health.AddHediff(h);
                    h.Severity = PeopleEvents3Math.GestationAt(Rand.Range(0.6f, stay * 0.65f), a.RaceProps.gestationPeriodDays);
                    st.inCalf.Add(a);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: an in-calf beast", e);
                }
            }
            // Once a stay, one birth goes hard.
            st.hardRolled = true;
            if (st.inCalf.Count > 0 && Rand.Chance(PeopleEvents3Math.HardBirthChance)) st.hard = st.inCalf.RandomElement();
        }

        public static Hediff_Pregnant Pregnancy(Pawn a) => a?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) as Hediff_Pregnant;

        /// <summary>The camp's calving camp whose hard birth is waiting for help (the event's question).</summary>
        public static Camp Waiting(Map map)
        {
            var c = On(map);
            var st = StateOf(c);
            return st != null && st.hard != null && st.hardSince >= 0 && !st.hardDone && !st.hardAsked && Pregnancy(st.hard) != null && st.hard.Spawned ? c : null;
        }

        /// <summary>
        /// The hard birth worked by <paramref name="helper"/> (the colony's, or a herder's): to the mother, an hour and a half
        /// at it, then the outcome the card's roll gave (<paramref name="bothLive"/>), the calf the colony's when it was the
        /// colony's hand that brought it.
        /// </summary>
        public static Visit Help(Camp c, Pawn helper, Pawn learner, bool bothLive, bool ours)
        {
            var st = StateOf(c);
            var t = CouncilGameComponent.Instance?.happenings;
            if (st?.hard == null || t == null || helper == null) return null;
            st.hardAsked = true;
            var v = t.AddVisit(BirthKind, DefDatabase<IncidentDef>.GetNamedSilentFail(HardEvent), c.map, helper, learner, null, Now + GenDate.TicksPerDay);
            v.place = st.hard;
            v.stake = c.id;
            v.text = (c.faction?.GetUniqueLoadID() ?? "") + "|" + (bothLive ? "live" : "lost") + "|" + (ours ? "ours" : "theirs");
            return v;
        }

        /// <summary>The helper's hour: to the mother and at the birth (their job); a day gone by, the herders see it through themselves.</summary>
        public static bool BirthHour(Visit v, int now)
        {
            if (v.done) return false;
            var c = CampTracker.Instance?.ById(v.stake);
            var st = StateOf(c);
            var mother = v.place as Pawn;
            var p = v.x;
            if (st == null || mother == null || mother.Dead || Pregnancy(mother) == null)
            {
                v.done = true;
                return false;
            }
            var job = BirthJob;
            if (now > v.until || p == null || p.Dead || !p.Spawned || p.Map != mother.Map)
            {
                // Nobody came after all: the herders do it themselves, as the roll had it.
                Born(v, c, st, mother, null);
                return false;
            }
            if (job == null || p.CurJobDef == job || p.Downed || p.Drafted || p.InMentalState) return true;
            var j = JobMaker.MakeJob(job, mother);
            j.count = PeopleEvents3Math.BirthTicks;
            p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
            if (v.a != null && v.a.Spawned && !v.a.Downed && !v.a.Drafted && v.a.CurJobDef != JobDefOf.Goto && v.a.CanReach(mother, PathEndMode.Touch, Danger.Some))
                v.a.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, CellFinder.RandomClosewalkCellNear(mother.Position, mother.Map, 2)), JobCondition.InterruptForced, null, false, true);
            return true;
        }

        /// <summary>The helper's work at the mother is done (JobDriver_HardBirth): the birth, as the roll had it.</summary>
        public static void Worked(Pawn helper, Pawn mother)
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == BirthKind && !x.done && x.x == helper && x.place == mother);
            var c = CampTracker.Instance?.ById(v?.stake ?? -1);
            var st = StateOf(c);
            if (v == null || st == null) return;
            try
            {
                if (helper.IsColonist)
                {
                    helper.skills?.Learn(SkillDefOf.Animals, PeopleEvents3Math.BirthXp, true);
                    v.a?.skills?.Learn(SkillDefOf.Animals, PeopleEvents3Math.BirthXp, true);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the birth's lesson", e);
            }
            Born(v, c, st, mother, helper);
        }

        private static void Born(Visit v, Camp c, CalvingState st, Pawn mother, Pawn helper)
        {
            v.done = true;
            st.hardDone = true;
            var parts = (v.text ?? "").Split('|');
            bool live = parts.Length > 1 && parts[1] == "live";
            bool ours = parts.Length > 2 && parts[2] == "ours" && helper != null && helper.IsColonist;
            var map = mother.Map;
            var preg = Pregnancy(mother);
            if (preg == null || map == null) return;
            var before = mother.relations?.Children.ToList() ?? new List<Pawn>();
            try
            {
                Hediff_Pregnant.DoBirthSpawn(mother, preg.Father);
                mother.health.RemoveHediff(preg);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the hard birth", e);
                return;
            }
            var calf = mother.relations?.Children.Where(x => !before.Contains(x) && x.Spawned).FirstOrDefault();
            if (calf != null && !st.calves.Contains(calf)) st.calves.Add(calf);
            if (live && ours && calf != null)
            {
                // The colony's hand brought it: the calf is the colony's.
                Lords.Leave(calf);
                c.herd.Remove(calf);
                st.calves.Remove(calf);
                calf.SetFaction(Faction.OfPlayer);
                if (helper.playerSettings != null && calf.playerSettings != null) calf.playerSettings.Master = helper;
                st.calfOurs = true;
            }
            if (!live)
            {
                // The mother is lost to it, shown where she lies.
                FilthMaker.TryMakeFilth(mother.Position, map, ThingDefOf.Filth_Blood, 3);
                mother.Kill(null);
            }
            bool colony = helper != null && helper.IsColonist;
            string key = live ? (ours ? "CC_PEv3_BirthOurs" : "CC_PEv3_BirthLive") : colony ? "CC_PEv3_BirthLostOurs" : "CC_PEv3_BirthLost";
            c.Note(c.Text(key, colony ? null : helper, colony ? helper : null, 0, mother.KindLabel), live ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, Importance.Notable, calf ?? (Thing)mother, true);
            var herder = c.Here.FirstOrDefault(x => x.RaceProps.Humanlike);
            if (herder != null) c.SayMoment(herder, live ? "born" : "lost");
        }
    }

    /// <summary>The calving camp's days: the in-calf beasts, the births and the calves, the count at dusk, the hard birth, the asks, and what can turn.</summary>
    public class CampWorker_Calving : CampWorker
    {
        private static int Now => Find.TickManager.TicksGame;

        public override void Started(Camp c)
        {
            if (!(c.workState is CalvingState)) c.workState = new CalvingState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is CalvingState st))
            {
                Started(c);
                st = SpringCalving.StateOf(c);
            }
            var map = c.map;
            if (!st.made && c.DaysHere >= 0.1f && c.herd.Count > 0) SpringCalving.MakeInCalf(c, st);
            // Calves born at the camp (the game's own births): theirs, in the herd.
            foreach (var p in c.lord?.ownedPawns.Where(x => x != null && x.RaceProps.Animal && x.ageTracker != null && !x.ageTracker.Adult && !c.herd.Contains(x)).ToList() ?? new List<Pawn>())
            {
                c.herd.Add(p);
                if (!st.calves.Contains(p))
                {
                    st.calves.Add(p);
                    c.Note(c.Text("CC_PEv3_CalfBorn", null, null, 0, p.KindLabel), MessageTypeDefOf.PositiveEvent, Importance.Flavor, p);
                }
            }
            HoldHard(c, st);
            // The count at dusk, aloud.
            if (hour == 18 && st.countDay != GenDate.DaysPassed)
            {
                st.countDay = GenDate.DaysPassed;
                var counter = c.leader != null && c.leader.Spawned && !c.leader.Downed ? c.leader : c.Here.FirstOrDefault(p => p.RaceProps.Humanlike);
                int head = c.herd.Count(a => a != null && !a.Dead && a.Spawned && c.GroupOf(a)?.follow != true && a != st.lost);
                if (counter != null) Speech.Say(counter, "CC_PEv3_CountAloud".Translate(head.Named("COUNT")).Resolve());
            }
            // Hay asked once, their first morning.
            if (!st.hay && c.DaysHere >= 0.4f && hour >= 7 && hour <= 11 && Tell.CanAsk && !c.asksMade.Contains("hay"))
            {
                var ask = c.AskByKey("hay");
                if (ask != null && c.AskBlocked(ask) == null) c.Request(ask);
            }
            // The afterbirth draws a predator in the night, once a stay, after a calf is born (the camps' offal rule, through the budget).
            if (!st.predator && hour == 1 && st.calves.Count > 0)
            {
                st.predator = true;
                Predator(c);
            }
            // Once a stay, a calf goes missing in the afternoon.
            if (!st.missingRolled && hour == 15 && st.calves.Count(x => x != null && x.Spawned && !x.Dead) > 0 && c.DaysHere >= 2f)
            {
                st.missingRolled = true;
                if (Rand.Chance(PeopleEvents3Math.CalfMissingChance)) Lose(c, st);
            }
            Lost(c, st, hour);
        }

        /// <summary>The hard birth held at its last stretch until help comes (or the herders see it through), and asked about once.</summary>
        private static void HoldHard(Camp c, CalvingState st)
        {
            var m = st.hard;
            if (m == null || st.hardDone) return;
            var preg = SpringCalving.Pregnancy(m);
            if (m.Dead || preg == null)
            {
                st.hardDone = true;
                return;
            }
            if (preg.Severity < PeopleEvents3Math.HardHeld - 0.01f) return;
            preg.Severity = Mathf.Min(preg.Severity, PeopleEvents3Math.HardHeld);
            if (st.hardSince < 0)
            {
                st.hardSince = Now;
                c.Note(c.Text("CC_PEv3_HardStarts", null, null, 0, m.KindLabel), MessageTypeDefOf.NeutralEvent, Importance.Notable, m, true);
            }
            if (st.hardAsked) return;
            if (PeopleEvents2.Fire(SpringCalving.HardEvent, c.map, c.faction)) return;
            // No answer to be had (another question waits): the herders see it through themselves after a while, as their own hands give it.
            if (Now - st.hardSince >= PeopleEvents3Math.HardWaitHours * GenDate.TicksPerHour)
            {
                var herder = c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed).OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
                if (herder != null) SpringCalving.Help(c, herder, null, Rand.Chance(PeopleEvents3Math.HerdersOdds), false);
            }
        }

        private static void Predator(Camp c)
        {
            var map = c.map;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.CampLeft.system) || !Harm.Roll(HarmKinds.CampLeft, PeopleEvents3Math.AfterbirthPredator)) return;
            var biome = map.Biome;
            var kind = biome == null ? null : BiomeAnimals.Of(biome).Where(k => k.RaceProps.predator && k.RaceProps.baseBodySize <= 1.4f && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race))
                .RandomElementByWeightWithFallback(k => BiomeAnimals.Commonality(biome, k));
            if (kind == null) return;
            try
            {
                var edge = LegendBeasts.EdgeToward(map, c.center);
                if (!CellFinder.TryFindRandomCellNear(edge, map, 8, x => x.Standable(map) && !x.Fogged(map), out var entry) && !RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f)) return;
                var beast = PawnGenerator.GeneratePawn(kind);
                GenSpawn.Spawn(beast, entry, map);
                if (beast.needs?.food != null) beast.needs.food.CurLevelPercentage = 0.2f;
                HarmBook.Instance?.Came(HarmKinds.CampLeft, Now);
                c.Note(c.Text("CC_PEv3_Afterbirth", null, null, 0, beast.LabelIndefinite()), MessageTypeDefOf.ThreatSmall, Importance.Notable, beast, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the afterbirth's predator", e);
            }
        }

        /// <summary>A calf wanders off from the herd to a place a long walk off, away from the colony, and lies down there.</summary>
        private static void Lose(Camp c, CalvingState st)
        {
            var map = c.map;
            var calf = st.calves.Where(x => x != null && x.Spawned && !x.Dead && !x.Downed && x.Faction == c.faction).RandomElementWithFallback();
            if (calf == null) return;
            var away = c.colonyCenter.IsValid ? c.center - c.colonyCenter : new IntVec3(1, 0, 0);
            float len = Mathf.Max(1f, Mathf.Sqrt(away.x * away.x + away.z * away.z));
            var aim = new IntVec3(c.center.x + Mathf.RoundToInt(away.x / len * Rand.Range(26f, 40f)) + Rand.RangeInclusive(-12, 12), 0,
                c.center.z + Mathf.RoundToInt(away.z / len * Rand.Range(26f, 40f)) + Rand.RangeInclusive(-12, 12)).ClampInsideMap(map);
            if (!CellFinder.TryFindRandomCellNear(aim, map, 10, x => x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && calf.CanReach(x, PathEndMode.OnCell, Danger.Some), out var spot)) return;
            Lords.Leave(calf);
            c.herd.Remove(calf);
            st.lost = calf;
            st.lostCell = spot;
            st.lostSince = Now;
            calf.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Goto, spot), JobCondition.InterruptForced, null, false, true);
            c.Note(c.Text("CC_PEv3_CalfMissing", null, null, 0, calf.KindLabel), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }

        /// <summary>The missing calf: lying out there by day; at night they look for it with lanterns; found, it walks back behind its finder.</summary>
        private static void Lost(Camp c, CalvingState st, int hour)
        {
            var calf = st.lost;
            if (calf == null) return;
            if (calf.Dead || !calf.Spawned || calf.Map != c.map)
            {
                st.lost = null;
                return;
            }
            if (!calf.Position.InHorDistOf(st.lostCell, 2f) && calf.CurJobDef != JobDefOf.Goto) calf.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, st.lostCell), JobCondition.InterruptForced, null, false, true);
            else if (calf.Position.InHorDistOf(st.lostCell, 2f) && calf.CurJobDef != JobDefOf.LayDown && calf.CurJobDef != JobDefOf.Wait)
            {
                var lie = JobMaker.MakeJob(JobDefOf.Wait, calf.Position);
                lie.expiryInterval = GenDate.TicksPerHour;
                calf.jobs.StartJob(lie, JobCondition.InterruptForced, null, false, true);
            }
            // A day and a half out and nobody found it: it finds its own way back at first light.
            if (Now - st.lostSince > GenDate.TicksPerDay * 3 / 2 && hour == 6) Found(c, st, null);
        }

        /// <summary>Found by <paramref name="by"/> (a lantern-bearer), or come back by itself: back to the herd behind them.</summary>
        public static void Found(Camp c, CalvingState st, Pawn by)
        {
            var calf = st.lost;
            st.lost = null;
            st.lostCell = IntVec3.Invalid;
            if (calf == null || calf.Dead || !calf.Spawned) return;
            if (c.LordAlive && calf.GetLord() == null) c.lord.AddPawn(calf);
            if (!c.herd.Contains(calf)) c.herd.Add(calf);
            if (by != null && by.Spawned)
            {
                var follow = JobMaker.MakeJob(JobDefOf.FollowClose, by);
                follow.followRadius = 3f;
                follow.expiryInterval = 2 * GenDate.TicksPerHour;
                follow.checkOverrideOnExpire = false;
                calf.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                if (CellFinder.TryFindRandomCellNear(c.FireCell, c.map, 3, x => x.Standable(c.map) && by.CanReach(x, PathEndMode.OnCell, Danger.Some), out var back))
                    by.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobCondition.InterruptForced, null, false, true);
                c.SayMoment(by, "found");
            }
            c.Note(c.Text(by != null ? "CC_PEv3_CalfFound" : "CC_PEv3_CalfBack", by, null, 0, calf.KindLabel), MessageTypeDefOf.PositiveEvent, Importance.Notable, calf);
        }

        /// <summary>At night, while a calf is missing, two of them walk the colony's land toward where it lies with lanterns, a stretch at a time.</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = SpringCalving.StateOf(c);
            var job = SpringCalving.LanternJob;
            if (st?.lost == null || job == null || !p.RaceProps.Humanlike || p.Downed || (hour < 20 && hour > 3)) return null;
            var searchers = c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed && x != c.trader).OrderBy(x => x.thingIDNumber).Take(2).ToList();
            if (!searchers.Contains(p)) return null;
            var map = c.map;
            var target = st.lostCell.IsValid ? st.lostCell : st.lost.Position;
            // A stretch toward it, a little aside, nearer each time.
            var at = p.Position;
            var step = new IntVec3(at.x + Mathf.RoundToInt((target.x - at.x) * 0.45f) + Rand.RangeInclusive(-5, 5), 0, at.z + Mathf.RoundToInt((target.z - at.z) * 0.45f) + Rand.RangeInclusive(-5, 5))
                .ClampInsideMap(map);
            if (at.InHorDistOf(target, 12f)) step = target;
            if (!CellFinder.TryFindRandomCellNear(step, map, 3, x => x.Standable(map) && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var go)) return null;
            var j = JobMaker.MakeJob(job, go);
            j.locomotionUrgency = LocomotionUrgency.Walk;
            return j;
        }

        /// <summary>A lantern-bearer has walked a stretch: near enough to the calf, it is found.</summary>
        public static void Searched(Pawn p)
        {
            var c = CampTracker.VisitorsAt(p?.Map).FirstOrDefault(x => x.kind == SpringCalving.Kind && x.Has(p));
            var st = SpringCalving.StateOf(c);
            if (st?.lost == null) return;
            if (p.Position.InHorDistOf(st.lost.Position, PeopleEvents3Math.FoundWithin)) Found(c, st, p);
            else if (Rand.Chance(0.3f)) c.SayMoment(p, "search");
        }

        public override void Leaving(Camp c)
        {
            var st = SpringCalving.StateOf(c);
            var f = c.faction;
            // A calf still out there comes with them.
            if (st?.lost != null && st.lost.Spawned && !st.lost.Dead)
            {
                if (c.LordAlive && st.lost.GetLord() == null) c.lord.AddPawn(st.lost);
                st.lost = null;
            }
            if (f == null || c.Hostile || c.tension >= 40f) return;
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.CalvingGoodwill, false);
            FactionMemory.Note(f, "campHelped", "CC_PEv3_CalvingName".Translate().Resolve());
            News.Post("CC_PEv3_CalvingRegion".Translate(f.Name.Named("FACTION"), (st?.calves.Count ?? 0).Named("COUNT")).Resolve(), LookTargets.Invalid, NewsKind.World);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = SpringCalving.StateOf(c);
            if (st == null) return;
            if (st.calves.Count > 0) marks.Add("calves");
            if (st.calfOurs) marks.Add("ours");
            if (st.hay) marks.Add("hay");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{CALVES}") ? text : text.Replace("{CALVES}", (SpringCalving.StateOf(c)?.calves.Count ?? 0).ToString());

        /// <summary>The hay given (the ask's effect): noted for their farewell.</summary>
        public static void Hay(Camp c)
        {
            var st = SpringCalving.StateOf(c);
            if (st != null) st.hay = true;
        }
    }

    /// <summary>
    /// An elder of {FACTION}'s herders at the gate in spring: {COUNT} in-calf beasts would calve on the colony's sheltered
    /// ground for some days; or for a toll ({BEAST} silver: the event has no beast of its own to name); or not this spring.
    /// </summary>
    public class HappeningWorker_SpringCalving : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            SpringCalving.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Herders) != null && GenLocalDate.Season(map) == Season.Spring
            && SpringCalving.On(map) == null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Herders);
            if (ctx.faction == null) return false;
            ctx.count = Rand.RangeInclusive(PeopleEvents3Math.InCalfMin, PeopleEvents3Math.InCalfMax);
            ctx.variant = PeopleEvents3Math.CalvingToll(ctx.count, LivingEvents.WealthFactor(ctx.map));
            ctx.beast = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Elder"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (SpringCalving.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "calve" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The calving camp comes (SpringCalving.Start); with <see cref="toll"/> they pay at the gate first, and think less of it.</summary>
    public class DilemmaEffect_CalvingCamp : DilemmaEffect
    {
        public bool toll;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 0, "CC_PEv3_Impact_Calving".Translate(hc.faction?.Name ?? "?", hc.count).Resolve(), "CC_PEv3_Impact_CalvingBrief".Translate().Resolve(), "pev3calving");
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_CalvingRisk".Translate().Resolve(), "CC_PEv3_Impact_CalvingRiskBrief".Translate().Resolve(), "pev3calvingrisk");
            if (toll)
            {
                sheet.Silver(hc.variant);
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.CalvingTollGoodwill);
            }
            else if (hc.faction != null)
                sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv3_Impact_CalvingThanks".Translate(hc.faction.Name, PeopleEvents3Math.CalvingGoodwill).Resolve(), "CC_PEv3_Impact_CalvingThanksBrief".Translate().Resolve(), "pev3calvingthanks");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (toll)
            {
                var x = hc.stranger;
                Couriers.PaySilver(hc.map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, hc.variant, hc.faction, PeoplesG.Up(x, hc.map) ? x : null);
                if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents3Math.CalvingTollGoodwill, false);
            }
            SpringCalving.Start(hc);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The calving camp's hay given (carried out by the ask's own effect): noted.</summary>
    public class DilemmaEffect_CalvingHay : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_Calving);
            if (c != null) CampWorker_Calving.Hay(c);
        }
    }

    /// <summary>
    /// A herder of the calving camp ({X}, of {FACTION}) at the gate: a hard birth ({THING}: the beast's kind), and the colony's
    /// best hand at beasts ({A}) asked for; {B} the next best, to learn beside them.
    /// </summary>
    public class HappeningWorker_HardBirth : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Herders) != null && SpringCalving.Waiting(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var c = SpringCalving.Waiting(ctx.map);
            var st = SpringCalving.StateOf(c);
            if (c == null || st?.hard == null || c.faction == null || !PeoplesD.Theirs(c.faction, PeoplesMath.Herders)) return false;
            ctx.faction = c.faction;
            ctx.stranger = c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && p != c.trader).OrderBy(p => p == c.leader).FirstOrDefault();
            if (ctx.stranger == null) return false;
            ctx.thing = st.hard.def;
            ctx.tale = st.hard.KindLabel;
            ctx.note = c.id.ToString();
            // Asked once: the herders wait for this answer.
            st.hardAsked = true;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.b = PeoplesD.Best(ctx.map, SkillDefOf.Animals, p => p != ctx.a);
            ctx.placeName = c.place;
            return ctx.a != null;
        }

        /// <summary>The herder walks up from the camp to the gate to ask.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var x = ctx.stranger;
            var gate = PeoplesD.GateSpot(ctx.map);
            if (x == null || !x.Spawned || !gate.HasValue) return x != null && x.Spawned;
            try
            {
                x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, gate.Value), JobCondition.InterruptForced, null, false, true);
                var wait = JobMaker.MakeJob(JobDefOf.Wait, gate.Value);
                wait.expiryInterval = 3 * GenDate.TicksPerHour;
                x.jobs.jobQueue.EnqueueLast(wait);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the herder to the gate", e);
            }
            return true;
        }

        /// <summary>The herder is the camp's, not the event's: never sent off or discarded with it.</summary>
        protected override void Abort(HappeningContext ctx)
        {
            var st = SpringCalving.StateOf(CampTracker.Instance?.ById(int.TryParse(ctx.note ?? "", out int id) ? id : -1));
            if (st != null && !st.hardDone) st.hardAsked = false;
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.b != null) tags.Add("second");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned)) return "CC_PEv3_NoHand".Translate().Resolve();
            if (option == 1 && (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned)) return "CC_PEv3_NoSecondHand".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "come" : "manage");
            // The herder goes back to the camp (its own day takes them up again).
            ctx.stranger?.jobs?.ClearQueuedJobs();
            ctx.stranger?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    /// <summary>
    /// The hard birth worked (SpringCalving.Help) as the card's roll gave it: <see cref="lost"/> the mother lost (the risk);
    /// <see cref="who"/> "a" (the colony's best hand), "ab" (with the next best learning beside them) or "herders" (they see
    /// it through themselves).
    /// </summary>
    public class DilemmaEffect_HardBirth : DilemmaEffect
    {
        public bool lost;
        public string who = "a";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null || lost) return;
            if (who == "herders")
            {
                sheet.Play("CC_PEv3_Impact_Herders".Translate().Resolve(), "CC_PEv3_Impact_HerdersBrief".Translate().Resolve());
                return;
            }
            sheet.Play("CC_PEv3_Impact_Birth".Translate(hc.a?.LabelShort ?? "?", hc.tale ?? "?").Resolve(), "CC_PEv3_Impact_BirthBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv3_Impact_Calf".Translate().Resolve(), "CC_PEv3_Impact_CalfBrief".Translate().Resolve(), "pev3calf");
            if (hc.a != null) DilemmaImpact.Xp(sheet, hc.a.LabelShort, hc.a.ThingID, SkillDefOf.Animals, PeopleEvents3Math.BirthXp, false);
            if (who == "ab" && hc.b != null) DilemmaImpact.Xp(sheet, hc.b.LabelShort, hc.b.ThingID, SkillDefOf.Animals, PeopleEvents3Math.BirthXp, false);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var c = CampTracker.Instance?.ById(int.TryParse(hc?.note ?? "", out int id) ? id : -1);
            if (c == null) return;
            if (who == "herders")
            {
                var herder = c.Here.Where(x => x.RaceProps.Humanlike && !x.Downed).OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
                if (herder != null) SpringCalving.Help(c, herder, null, !lost, false);
                return;
            }
            SpringCalving.Help(c, hc.a, who == "ab" ? hc.b : null, !lost, true);
        }
    }

    /// <summary>
    /// The hard birth (target A the mother): to her side, an hour and a half at it with the healing cross, then the birth as
    /// the roll had it (SpringCalving.Worked).
    /// </summary>
    public class JobDriver_HardBirth : JobDriver
    {
        private Pawn Mother => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Mother == null || Mother.Dead);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("HardBirth");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(600, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("HardBirthDone");
            done.initAction = () => SpringCalving.Worked(pawn, Mother);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>A herder walks a stretch of the colony's land at night with a lantern (target A a cell), looking for the calf (CampWorker_Calving.Searched).</summary>
    public class JobDriver_LanternSearch : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var go = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            go.AddPreTickIntervalAction(delta =>
            {
                if (pawn.IsHashIntervalTick(45, delta) && pawn.Map != null) FleckMaker.ThrowFireGlow(pawn.DrawPos, pawn.Map, 0.9f);
            });
            yield return go;
            var look = ToilMaker.MakeToil("LanternLook");
            look.initAction = () => CampWorker_Calving.Searched(pawn);
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = 180;
            look.tickIntervalAction = delta =>
            {
                if (pawn.IsHashIntervalTick(45, delta)) FleckMaker.ThrowFireGlow(pawn.DrawPos, pawn.Map, 0.9f);
            };
            yield return look;
        }
    }
}
