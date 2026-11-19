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
    // ===================== 86. A mech yard (the Lattice) =====================

    /// <summary>The mech yard's stay: its mechs, the rock they cut and where they pile it, the colony's hire, and how it turned.</summary>
    public class MechYardState : CampWorkState
    {
        public List<Pawn> mechs = new List<Pawn>();
        public List<Thing> chunks = new List<Thing>();
        public IntVec3 pile = IntVec3.Invalid;
        public int cutDay = -1, cutToday, cut, mended, hauled, hiredUntil = -1;
        public bool hired, feralRolled, feral, bandRolled, band;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref mechs, "mechs", LookMode.Reference);
            Scribe_Collections.Look(ref chunks, "chunks", LookMode.Reference);
            Scribe_Values.Look(ref pile, "pile", IntVec3.Invalid);
            Scribe_Values.Look(ref cutDay, "cutDay", -1);
            Scribe_Values.Look(ref cutToday, "cutToday");
            Scribe_Values.Look(ref cut, "cut");
            Scribe_Values.Look(ref mended, "mended");
            Scribe_Values.Look(ref hauled, "hauled");
            Scribe_Values.Look(ref hiredUntil, "hiredUntil", -1);
            Scribe_Values.Look(ref hired, "hired");
            Scribe_Values.Look(ref feralRolled, "feralRolled");
            Scribe_Values.Look(ref feral, "feral");
            Scribe_Values.Look(ref bandRolled, "bandRolled");
            Scribe_Values.Look(ref band, "band");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                mechs ??= new List<Pawn>();
                mechs.RemoveAll(p => p == null);
                chunks ??= new List<Thing>();
                chunks.RemoveAll(t => t == null);
            }
        }
    }

    /// <summary>
    /// A mech yard (item 86): a mech-tender and two link-hands of the Lattice camp five to seven days by the colony with four to six of
    /// their labour mechs (PeopleEvents9Math.YardMiners and YardHaulers). By day the rock-cutters cut plain rock near the camp (the camps' own rule
    /// for what may be taken) and the haulers carry the chunks to a pile by the camp, which they leave to the colony; the tender and the
    /// link-hands see to the mechs. The colony may hire the mechs for a day: the haulers carry the colony's loose things into its
    /// stockpiles and the rest mend its worn buildings. It can turn, once a stay each: a mech slips its link and goes feral (the game's own
    /// hostile mech, the drovers' bull's kind in the shared harm budget; the camp's people go after it), or a band of a people at war with
    /// the Lattice comes against the camp for its mechs (the camps' own bands). Biotech only.
    /// </summary>
    public static class MechYard
    {
        public const string EventDef = "Rimfolk_Evt_MechYard";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_MechYard");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static MechYardState StateOf(Camp c) => c?.workState as MechYardState;

        public static Camp CampOf(Pawn p) => p?.Map == null ? null : CampTracker.VisitorsAt(p.Map).FirstOrDefault(c => c.kind == Kind && (c.Has(p) || StateOf(c)?.mechs.Contains(p) == true));

        private static PawnKindDef Named(string n)
        {
            var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(n);
            return k?.race != null && k.RaceProps.IsMechanoid ? k : null;
        }

        public static List<PawnKindDef> MinerKinds() => PeopleEvents9Math.YardMiners.Select(Named).Where(k => k != null).ToList();

        public static List<PawnKindDef> HaulerKinds() => PeopleEvents9Math.YardHaulers.Select(Named).Where(k => k != null).ToList();

        public static bool Miner(Pawn mech) => mech?.kindDef != null && PeopleEvents9Math.YardMiners.Contains(mech.kindDef.defName);

        public static Camp Start(HappeningContext ctx, bool hired)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_MechTender"), ctx.faction, new FloatRange(24f, 60f)));
                for (int i = 1; i < PeopleEvents9Math.YardPeople; i++) people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_Linkhand"), ctx.faction, new FloatRange(18f, 50f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents9Math.YardMaxDays);
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
                var st = new MechYardState();
                if (hired)
                {
                    st.hired = true;
                    st.hiredUntil = Now + (PeopleEvents9Math.HireHours + 12) * GenDate.TicksPerHour;
                }
                c.workState = st;
                Mechs(c, st);
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the mech yard", e);
                return null;
            }
        }

        /// <summary>Their mechs walk in with them, in their group: half rock-cutters, the rest haulers.</summary>
        private static void Mechs(Camp c, MechYardState st)
        {
            var map = c.map;
            var with = c.members.FirstOrDefault(p => p != null && p.Spawned);
            var miners = MinerKinds();
            var haulers = HaulerKinds();
            if (with == null || c.lord == null || miners.Count + haulers.Count == 0) return;
            int n = Rand.RangeInclusive(PeopleEvents9Math.YardMechsMin, PeopleEvents9Math.YardMechsMax);
            int m = PeopleEvents9Math.Miners(n);
            for (int i = 0; i < n; i++)
            {
                var kind = (i < m ? miners : haulers).RandomElementWithFallback() ?? miners.Concat(haulers).RandomElement();
                try
                {
                    var mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, c.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(with.Position, map, 4), map);
                    c.herd.Add(mech);
                    c.lord.AddPawn(mech);
                    st.mechs.Add(mech);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 9: the yard's mechs", e);
                    return;
                }
            }
        }

        private static bool Idle(Pawn mech, Camp c) =>
            mech != null && !mech.Dead && mech.Spawned && mech.Map == c.map && !mech.Downed && mech.Faction == c.faction && mech.GetLord() == c.lord && !mech.InMentalState
            && mech.CurJobDef != PeopleEvents9.CutRockJob && mech.CurJobDef != PeopleEvents9.MendJob && mech.CurJobDef != JobDefOf.HaulToCell;

        /// <summary>Their hour: the mechs at their work by day (the colony's while hired), and the turns once a stay each.</summary>
        public static void Hour(Camp c, MechYardState st, int hour)
        {
            st.mechs.RemoveAll(m => m == null || m.Dead || m.Faction != c.faction);
            st.chunks.RemoveAll(t => t == null || t.Destroyed || !t.Spawned);
            if (GenDate.DaysPassed != st.cutDay)
            {
                st.cutDay = GenDate.DaysPassed;
                st.cutToday = 0;
            }
            if (st.hired && Now > st.hiredUntil)
            {
                st.hired = false;
                c.Note(c.Text("CC_PEv9_YardHireOver", c.leader), MessageTypeDefOf.NeutralEvent);
            }
            if (MapFacts.ThreatOn(c.map)) return;
            if (PeopleEvents9Math.YardHour(hour) || st.hired && hour >= 6 && hour <= 21)
                foreach (var mech in st.mechs.Where(m => Idle(m, c)).ToList())
                {
                    var job = st.hired ? HiredJob(c, st, mech) : OwnJob(c, st, mech);
                    if (job == null) continue;
                    try
                    {
                        mech.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 9: a yard mech's work", e);
                    }
                }
            if (c.DaysHere < 1f || hour < 9 || hour > 17) return;
            if (!st.feralRolled) Feral(c, st);
            else if (!st.bandRolled) Band(c, st);
        }

        /// <summary>Their own work: a rock-cutter cuts plain rock near the camp; a hauler carries a chunk of it to the pile.</summary>
        private static Job OwnJob(Camp c, MechYardState st, Pawn mech)
        {
            if (Miner(mech))
            {
                if (st.cutToday >= PeopleEvents9Math.RockPerDay) return null;
                var rock = Rock(c, mech);
                if (rock == null) return null;
                st.cutToday++;
                return PeopleEvents9.Make(PeopleEvents9.CutRockJob, rock, PeopleEvents9Math.RockTicks);
            }
            var chunk = st.chunks.Where(t => t.Spawned && t.Map == c.map && !t.Position.InHorDistOf(Pile(c, st), 3f) && mech.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(mech.Position)).FirstOrDefault();
            if (chunk == null) return null;
            var cell = PileCell(c, st, mech);
            if (!cell.IsValid) return null;
            var j = JobMaker.MakeJob(JobDefOf.HaulToCell, chunk, cell);
            j.count = 1;
            j.haulMode = HaulMode.ToCellNonStorage;
            return j;
        }

        /// <summary>Plain rock near their camp to cut, by the camps' own rule (CampTake): stone only, clear of the colony, nothing overhead to come down.</summary>
        public static Thing Rock(Camp c, Pawn mech)
        {
            var map = c.map;
            Thing best = null;
            float bestD = float.MaxValue;
            foreach (var cell in GenRadial.RadialCellsAround(c.center, CampSite.WorkRadius, false))
            {
                if (!cell.InBounds(map)) continue;
                var rock = cell.GetFirstMineable(map);
                if (rock == null || !CampTake.PlainRock(rock.def) || !c.MayWork(cell) || !CampTake.May(rock, c) || CampSite.FateAt(map, cell) != CellFate.Mine) continue;
                float d = cell.DistanceToSquared(c.center);
                if (d >= bestD || c.Here.Any(o => o.CurJob?.targetA.Thing == rock) || StateOf(c)?.mechs.Any(o => o.CurJob?.targetA.Thing == rock) == true) continue;
                if (!mech.CanReach(rock, PathEndMode.Touch, Danger.Some)) continue;
                best = rock;
                bestD = d;
            }
            return best;
        }

        /// <summary>Where they pile the rock: open ground beside their camp.</summary>
        private static IntVec3 Pile(Camp c, MechYardState st)
        {
            if (st.pile.IsValid) return st.pile;
            var map = c.map;
            if (CellFinder.TryFindRandomCellNear(c.center, map, 6, x => x.Standable(map) && x.GetEdifice(map) == null && !x.InHorDistOf(c.FireCell, 3f) && x.GetFirstItem(map) == null, out var cell))
                st.pile = cell;
            return st.pile.IsValid ? st.pile : c.center;
        }

        private static IntVec3 PileCell(Camp c, MechYardState st, Pawn by)
        {
            var map = c.map;
            var at = Pile(c, st);
            foreach (var x in GenRadial.RadialCellsAround(at, 3f, true))
                if (x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null && by.CanReach(x, PathEndMode.OnCell, Danger.Some)) return x;
            return IntVec3.Invalid;
        }

        /// <summary>A hired mech's work: a lifter carries a loose thing of the colony's into its stockpile; the rest mend its worn buildings.</summary>
        private static Job HiredJob(Camp c, MechYardState st, Pawn mech)
        {
            var map = c.map;
            if (!Miner(mech))
            {
                foreach (var t in map.listerHaulables.ThingsPotentiallyNeedingHauling().Take(60).ToList())
                {
                    if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.def.category != ThingCategory.Item || StoreUtility.IsInValidBestStorage(t)) continue;
                    if (!mech.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                    if (!StoreUtility.TryFindBestBetterStoreCellFor(t, mech, map, StoreUtility.CurrentStoragePriorityOf(t, false), Faction.OfPlayer, out var cell, true)) continue;
                    var j = HaulAIUtility.HaulToCellStorageJob(mech, t, cell, false);
                    if (j == null) continue;
                    st.hauled++;
                    return j;
                }
                return null;
            }
            var b = map.listerBuildings.allBuildingsColonist.Where(x => x != null && x.def.useHitPoints && PeopleEvents9Math.WantsMending(x.HitPoints, x.MaxHitPoints) && !x.IsBurning()
                                                                       && !st.mechs.Any(o => o.CurJob?.targetA.Thing == x) && mech.CanReach(x, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(mech.Position)).FirstOrDefault();
            return b == null ? null : PeopleEvents9.Make(PeopleEvents9.MendJob, b, PeopleEvents9Math.MendTicks);
        }

        /// <summary>A rock cut out: the chunk it leaves is theirs to pile (the game drops a stranger's cutting forbidden to the colony).</summary>
        public static void Cut(Pawn mech, Thing rock)
        {
            var c = CampOf(mech);
            var st = StateOf(c);
            if (st == null || !(rock is Mineable m) || m.Destroyed || !m.Spawned) return;
            var map = m.Map;
            var cell = m.Position;
            m.DestroyMined(mech);
            st.cut++;
            foreach (var t in cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Item).ToList())
                if (!st.chunks.Contains(t)) st.chunks.Add(t);
        }

        /// <summary>A building of the colony's mended by a hired mech: back to its full strength.</summary>
        public static void Mended(Pawn mech, Thing t)
        {
            var c = CampOf(mech);
            var st = StateOf(c);
            if (st == null || !(t is Building b) || b.Destroyed || !b.def.useHitPoints) return;
            b.HitPoints = b.MaxHitPoints;
            b.Map?.listerBuildingsRepairable?.Notify_BuildingRepaired(b);
            st.mended++;
        }

        /// <summary>A mech seen to by the tender or a link-hand: now and then a word.</summary>
        public static void Tended(Pawn by, Pawn mech)
        {
            var c = CampOf(by);
            if (c != null) c.SayMoment(by, "tend", 0.3f);
        }

        /// <summary>
        /// Once a stay, by day after their first: a mech of theirs slips its link and goes feral (the game's own hostile mech, the drovers'
        /// bull's kind in the shared harm budget), told as a threat; their people go after it.
        /// </summary>
        public static void Feral(Camp c, MechYardState st)
        {
            st.feralRolled = true;
            var mech = st.mechs.Where(m => m.Spawned && !m.Downed && m.Faction == c.faction).RandomElementWithFallback();
            if (mech == null || Faction.OfMechanoids == null || !IncidentWorker_Happening.BattlesAllowed(c.map) || !Harm.On || !Harm.SystemOn(HarmKinds.DroversBull.system)
                || !Harm.Roll(HarmKinds.DroversBull, PeopleEvents9Math.YardFeralChance)) return;
            try
            {
                Lords.Leave(mech);
                c.herd.Remove(mech);
                st.mechs.Remove(mech);
                mech.SetFaction(Faction.OfMechanoids);
                LordMaker.MakeNewLord(mech.Faction, new LordJob_DefendPoint(mech.Position, 8f, 12f, false, false), c.map, new List<Pawn> { mech });
                st.feral = true;
                HarmBook.Instance?.Came(HarmKinds.DroversBull, Now);
                if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "feral");
                if (c.LordAlive) c.lord.ReceiveMemo(LordJob_Camp.MemoFight);
                Tell.Letter(NewsKind.Land, Importance.Important, c.map, "CC_PEv9_YardFeralLabel".Translate(mech.LabelShort).Resolve(),
                    c.Text("CC_PEv9_YardFeral", c.leader, null, 0, mech.LabelShort), LetterDefOf.ThreatSmall, new LookTargets(mech), c.faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: a yard mech gone feral", e);
            }
        }

        /// <summary>Once a stay, by day after their first: a band of a people at war with the Lattice comes against the camp for its mechs (the camps' own bands).</summary>
        public static void Band(Camp c, MechYardState st)
        {
            st.bandRolled = true;
            var enemy = Enemy(c.faction);
            if (enemy == null || !IncidentWorker_Happening.BattlesAllowed(c.map) || !Rand.Chance(PeopleEvents9Math.YardBandChance)) return;
            if (CampRaids.Send(c, enemy, false, true) == null) return;
            st.band = true;
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "band");
        }

        /// <summary>A people at war with the Lattice who fields fighters and would want mechs: of the industrial age or later first.</summary>
        public static Faction Enemy(Faction lattice)
        {
            if (lattice == null) return null;
            var foes = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && f != lattice && !f.IsPlayer && !f.defeated && !f.Hidden && f.def.humanlikeFaction
                                                                                  && f.HostileTo(lattice) && CallsToArms.Fields(f)).ToList();
            return foes.Where(f => Peoples.TechOf(f) >= TechLevel.Industrial).RandomElementWithFallback() ?? foes.RandomElementWithFallback();
        }

        /// <summary>The colony hired the mechs for a day (the silver paid by the ask).</summary>
        public static void Hire(Camp c)
        {
            var st = StateOf(c);
            if (st == null) return;
            st.hired = true;
            st.hiredUntil = Now + PeopleEvents9Math.HireHours * GenDate.TicksPerHour;
        }

        /// <summary>They go: the rock they cut left free to the colony where it lies; the Lattice's thanks for a stay that went well.</summary>
        public static void Leave(Camp c, MechYardState st)
        {
            foreach (var t in st.chunks.Where(t => t != null && t.Spawned)) t.SetForbidden(false, false);
            if (!c.Hostile && c.tension < 50f) PeopleEvents9.Goodwill(c.faction, PeopleEvents9Math.YardGoodwill, GoodwillWhy.CampWell);
        }
    }

    /// <summary>The mech yard's ways: the mechs' work by day, the tender at the mechs, the hire, the feral mech, the band.</summary>
    public class CampWorker_MechYard : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is MechYardState)) c.workState = new MechYardState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is MechYardState st))
            {
                Started(c);
                st = MechYard.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            MechYard.Hour(c, st, hour);
        }

        /// <summary>By day, the tender and the link-hands see to a mech of the yard: half an hour at it, sparks and a bar.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            var st = MechYard.StateOf(c);
            if (st == null || !p.RaceProps.Humanlike || !Rand.Chance(0.4f)) return null;
            var mech = st.mechs.Where(m => m.Spawned && !m.Downed && m.Map == c.map && p.CanReach(m, PathEndMode.Touch, Danger.Some)).RandomElementWithFallback();
            return mech == null ? null : PeopleEvents9.Make(PeopleEvents9.TendMechJob, mech, 1250);
        }

        public override void Leaving(Camp c)
        {
            var st = MechYard.StateOf(c);
            if (st != null) MechYard.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = MechYard.StateOf(c);
            if (st == null) return;
            if (st.cut >= 3) marks.Add("cut");
            if (st.mended + st.hauled > 0) marks.Add("hired");
            if (st.feral) marks.Add("feral");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{CUT}") ? text : text.Replace("{CUT}", (MechYard.StateOf(c)?.cut ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = MechYard.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "hire":
                    if (st.hired) return "CC_PEv9_YardHired".Translate().Resolve();
                    return st.mechs.Any(m => m.Spawned && !m.Downed) ? null : "CC_PEv9_YardNoMechs".Translate().Resolve();
            }
            return null;
        }
    }

    /// <summary>A mech-tender of {FACTION} ({X}) at the gate: the Lattice would camp a mech yard by the colony some days, its mechs at work. Biotech only.</summary>
    public class HappeningWorker_MechYard : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && MechYard.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && MechYard.On(map) == null && MechYard.MinerKinds().Count > 0
            && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_MechTender"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            // The day's hire as the camp's own ask prices it (its silver by the colony's wealth).
            var hire = MechYard.Kind?.asks?.FirstOrDefault(a => a?.key == "hire");
            ctx.count = hire == null ? 0 : CampAnswers.Round10(hire.silver * LivingEvents.WealthFactor(ctx.map));
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null && ctx.count > 0;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (MechYard.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mech yard made (MechYard.Start); <see cref="hired"/>: its mechs hired for the first day, the silver carried out from the gate.</summary>
    public class DilemmaEffect_MechYard : DilemmaEffect
    {
        public bool hired;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv9_Impact_Yard".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv9_Impact_YardBrief".Translate().Resolve(), "pev9yard");
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv9_Impact_YardRock".Translate().Resolve(), "CC_PEv9_Impact_YardRockBrief".Translate().Resolve(), "pev9yardrock");
            if (hired) sheet.Play("CC_PEv9_Impact_YardHire".Translate().Resolve(), "CC_PEv9_Impact_YardHireBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, -1, "CC_PEv9_Impact_YardTurns".Translate().Resolve(), "CC_PEv9_Impact_YardTurnsBrief".Translate().Resolve(), "pev9yardturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.YardGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            MechYard.Start(hc, hired);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The camp's ask: the mechs hired for a day (the silver paid by the ask).</summary>
    public class DilemmaEffect_YardHire : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PEv9_Impact_YardHire".Translate().Resolve(), "CC_PEv9_Impact_YardHireBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_MechYard);
            if (c == null) return;
            MechYard.Hire(c);
            c.Note(c.Text("CC_PEv9_YardHireStart", c.leader), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }
    }
}
