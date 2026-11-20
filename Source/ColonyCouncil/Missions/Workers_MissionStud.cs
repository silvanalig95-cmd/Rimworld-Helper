using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== B24. A stud visit =====================

    /// <summary>
    /// A stud visit (the content update's B24): a herding people (the herders preferred, any people at peace) asks the colony
    /// to let one of their animals stand in its pen for five to seven days, of a kind the colony keeps, mods' kinds included.
    /// Two ways round (<see cref="Mission.variant"/>):
    /// <list type="bullet">
    /// <item>theirs: their best male stands with the colony's females. The game's own mating does the rest; a young one born to
    /// the colony's beast is the colony's, for a small fee taken by their courier when their drover comes back for him, and
    /// only if one of the colony's has taken (pregnant by him).</item>
    /// <item>ours: their female stands with the colony's male. If she has taken when their drover comes for her, their courier
    /// brings one of the young to the colony's gate once it is born (the race's own gestation, from where she is in it).</item>
    /// </list>
    /// By letter the colony may instead ask them for silver for the keep (the second answer): then they pay it when they come
    /// back, there is no fee, and (ours) no young is owed. Their animal is the colony's while it stands (so the game's mating
    /// pairs it with the colony's own), fed and kept by the colony; dead or lost, it ends badly; bonded to someone of the
    /// colony, they leave it where it is loved. Egg layers are left out: their young can't be told apart.
    /// </summary>
    public class HappeningWorker_MissionStud : HappeningWorker_Mission
    {
        public const string StudKind = "studvisit", Theirs = "theirs", Ours = "ours";

        protected override string Kind => StudKind;

        protected override float RestDays => 60f;

        protected override int Rating => 2;

        /// <summary>A kind the game breeds by mating (born alive, of two sexes), and an animal of the colony's of it, grown and able to.</summary>
        private static bool Breeds(Pawn a) =>
            a != null && !a.Dead && a.RaceProps != null && a.RaceProps.Animal && !a.RaceProps.Insect && a.RaceProps.gestationPeriodDays > 0f
            && !a.def.HasComp(typeof(CompEggLayer)) && a.ageTracker?.CurLifeStage != null && a.ageTracker.CurLifeStage.reproductive && a.gender != Gender.None;

        /// <summary>The colony's animals that could take part, of one race, by sex.</summary>
        private static List<Pawn> ColonyOf(Map map, ThingDef race, Gender g) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => a.def == race && a.gender == g && Breeds(a) && !a.Downed && !Pregnant(a)).ToList() ?? new List<Pawn>();

        public static Hediff_Pregnant PregnancyOf(Pawn a) =>
            a?.health?.hediffSet?.hediffs?.OfType<Hediff_Pregnant>().FirstOrDefault();

        private static bool Pregnant(Pawn a) => PregnancyOf(a) != null;

        /// <summary>The kinds the colony keeps that one way round or the other could be done with: (race, way).</summary>
        private static List<(ThingDef race, string way)> Options(Map map)
        {
            var list = new List<(ThingDef, string)>();
            if (map == null) return list;
            foreach (var race in map.mapPawns.SpawnedColonyAnimals.Where(Breeds).Select(a => a.def).Distinct())
            {
                if (KindOf(race) == null) continue;
                if (ColonyOf(map, race, Gender.Female).Count > 0) list.Add((race, Theirs));
                if (ColonyOf(map, race, Gender.Male).Count > 0) list.Add((race, Ours));
            }
            return list;
        }

        /// <summary>The kind of pawn to make their animal of: the race's own, the plainest.</summary>
        private static PawnKindDef KindOf(ThingDef race) =>
            DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.race == race && k.RaceProps != null && k.RaceProps.Animal).OrderBy(k => k.combatPower).FirstOrDefault();

        protected override bool Possible(Map map) => Neighbor(map) != null && Options(map).Count > 0;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            var options = Options(map);
            if (m.faction == null || options.Count == 0) return false;
            var (race, way) = options.RandomElementByWeight(o => o.way == Theirs ? VarietyMath.StudTheirsWeight : 1f);
            var kind = KindOf(race);
            if (kind == null) return false;
            Pawn x = null;
            try
            {
                float age = kind.RaceProps.lifeExpectancy * Rand.Range(0.25f, 0.45f);
                x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, m.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    fixedBiologicalAge: age, fixedChronologicalAge: age, fixedGender: way == Theirs ? Gender.Male : Gender.Female));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a stud visit: their animal made", e);
            }
            var drover = HomeQuests.Person(m.faction, new FloatRange(22f, 60f), Couriers.PersonKind(m.faction));
            if (x == null || drover == null || !Breeds(x))
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
            m.variant = way;
            m.tale = race.label;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            // Who of the colony looks after it: the colony's best with animals.
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals) ?? IncidentWorker_Happening.Colonists(map).FirstOrDefault();
            m.wait = Rand.Range(0.3f, 0.6f);
            m.days = Mathf.Ceil(m.wait + VarietyMath.StudDays(Rand.Value));
            m.reward = VarietyMath.StudFee(x.MarketValue);
            m.goodwill = VarietyMath.StudGoodwill;
            m.penalty = VarietyMath.StudPenalty;
            Fill(m, ctx);
            ctx.thing = race;
            return true;
        }

        /// <summary>The terms: {ITEM} the fee for a young one of his (theirs); the colony is paid nothing for her (ours).</summary>
        protected override string Extra(HappeningContext ctx) => HomeQuests.Terms(this, ctx, Missions.Of(ctx) ?? Making, mm => Mathf.RoundToInt(mm.days),
            mm => mm.reward, (Missions.Of(ctx) ?? Making)?.reward.ToString());

        /// <summary>The second answer (by letter): taken on, but for silver for the keep (Mission.target 1), no fee and no young owed.</summary>
        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var m = Missions.Of(ctx);
            if (m != null && m.state == MissionState.Offered && option == 1 && !byDefault)
            {
                m.target = 1;
                m.reward = VarietyMath.StudKeep(m.reward);
                Missions.Accept(m);
                return base.Resolved(ctx, 0, risk, false, outcome);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        private static Pawn Drover(Mission m) => m.group.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);

        private static bool SilverAsked(Mission m) => m.target == 1;

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
            if (m.stage <= 3 && (x == null || x.Discarded))
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && m.stage <= 2 && x.Dead)
            {
                Died(m);
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    // Their drover walks it in from the road and makes for the gate.
                    var gate = PlaySpots.GateCell(map, null);
                    if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
                    if (drover == null || drover.Dead || Arrivals.Visit(new List<Pawn> { drover, x }, map, m.faction, gate, 8f) == null)
                    {
                        Missions.Finish(m, MissionState.Failed, "ended", null);
                        return;
                    }
                    m.stage = 1;
                    m.due = now + GenDate.TicksPerHour * 6;
                    Tell.Message(NewsKind.World, Importance.Important, map, "CC_Stud_Coming".Translate(drover.LabelShort.Named("DROVER"), (m.name ?? "?").Named("NAME"),
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
                        CouncilLog.ErrorOnce("a stud visit: handed over", e);
                    }
                    if (drover != null && drover.Spawned) Arrivals.SendAway(new[] { drover });
                    m.stage = 2;
                    m.deadline = now + Mathf.RoundToInt((m.days - m.wait) * GenDate.TicksPerDay);
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                    if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.NeutralEvent);
                    return;
                }
                case 2:
                    Standing(m, map, now);
                    return;
                case 3:
                    // Their drover has come for it: theirs now, or gone off with them.
                    if (x.Faction == m.faction || !x.Spawned && !x.Dead && x.Faction != Faction.OfPlayer) Collected(m, map, false);
                    else if (now - m.lastLook > GenDate.TicksPerDay * 3 / 2) Collected(m, map, true);
                    return;
                case 4:
                    YoungDue(m, map, now);
                    return;
                default:
                    YoungAtGate(m, map, now);
                    return;
            }
        }

        /// <summary>While it stands with the colony's: gone, bonded, or the days up.</summary>
        private void Standing(Mission m, Map map, int now)
        {
            var x = m.x;
            bool ours = x.Faction == Faction.OfPlayer && (x.Spawned && x.Map == map || x.GetCaravan() != null);
            if (!ours)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            if (HappeningWorker_MissionRunaway.Bonded(x))
            {
                Missions.Goodwill(m, m.goodwill / 2);
                Missions.Finish(m, MissionState.Done, "bonded", null, x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
                return;
            }
            if (now < m.deadline || !x.Spawned || x.Map != map) return;
            bool took = Took(m, map);
            m.outcome = took ? "took" : "nottook";
            m.stage = 3;
            m.lastLook = now;
            // The fee for a young one of his (theirs, one of the colony's taken, no silver asked): their courier comes for it.
            if (m.variant == Theirs && took && !SilverAsked(m) && m.reward > 0) Couriers.TakeFromStores(map, ThingDefOf.Silver, m.reward, m.faction);
            var pay = SilverAsked(m) ? HomeQuests.PayThings(m, m.reward) : new List<Thing>();
            if (!Couriers.Fetch(map, x, m.faction, pay, x.Position, false)) Collected(m, map, true);
        }

        /// <summary>Whether one has taken: one of the colony's females pregnant by him (theirs), or she is (ours).</summary>
        private static bool Took(Mission m, Map map)
        {
            var x = m.x;
            if (m.variant == Theirs)
                return map.mapPawns.SpawnedColonyAnimals.Any(a => a != x && a.def == x.def && PregnancyOf(a) is Hediff_Pregnant h && h.Father == x);
            return Pregnant(x);
        }

        /// <summary>Back to them: their drover leads it home (or it is taken now), the silver for the keep set down; what follows by the way round.</summary>
        private void Collected(Mission m, Map map, bool now)
        {
            var x = m.x;
            int ticks = 0;
            var preg = m.variant == Ours ? PregnancyOf(x) : null;
            if (preg != null) ticks = VarietyMath.YoungDueTicks(preg.Severity, x.RaceProps.gestationPeriodDays, Rand.Value);
            if (now && x != null && !x.Dead && x.Spawned && x.Faction == Faction.OfPlayer)
            {
                try
                {
                    Lords.Leave(x);
                    x.roping?.BreakAllRopes();
                    if (x.playerSettings != null) x.playerSettings.Master = null;
                    x.SetFaction(m.faction);
                    if (!x.Downed && x.CanReachMapEdge())
                        LordMaker.MakeNewLord(m.faction, new LordJob_ExitMapBest(Pace.Errand, false, false), x.Map, new List<Pawn> { x });
                    else
                    {
                        x.DeSpawn();
                        if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a stud visit: led home", e);
                }
                if (SilverAsked(m)) HomeQuests.Pay(m, map, IntVec3.Invalid, null, m.reward);
            }
            bool took = m.outcome == "took";
            // Ours, she took and no silver was asked: their courier brings one of the young once it is born.
            if (m.variant == Ours && took && !SilverAsked(m) && ticks > 0)
            {
                m.stage = 4;
                m.due = Find.TickManager.TicksGame + ticks;
                // Nothing more is the colony's to do: no time left is shown while the young one is on its way to being born.
                m.deadline = -1;
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Stud_YoungLater".Translate((m.name ?? "?").Named("NAME"), (m.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                Missions.Goodwill(m, m.goodwill);
                return;
            }
            Missions.Goodwill(m, m.goodwill);
            string key = SilverAsked(m) ? "silver" : took ? (m.variant == Theirs ? "foal" : "took") : "nottook";
            Missions.Finish(m, MissionState.Done, key, SilverAsked(m) ? HomeQuests.RewardWords(m, m.reward) : m.reward.ToString(), LookTargets.Invalid);
        }

        /// <summary>The young one born at their town: someone of theirs walks it in to the colony's gate.</summary>
        private void YoungDue(Mission m, Map map, int now)
        {
            if (now < m.due) return;
            var x = m.x;
            var kind = x?.kindDef ?? KindOf(x?.def);
            Pawn young = null;
            try
            {
                if (kind != null)
                    young = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, m.faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false, fixedBiologicalAge: 0.05f, fixedChronologicalAge: 0.05f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a stud visit: the young one", e);
            }
            var drover = HomeQuests.Person(m.faction, new FloatRange(18f, 55f), Couriers.PersonKind(m.faction));
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            if (young == null || drover == null || m.faction == null || m.faction.HostileTo(Faction.OfPlayer)
                || Arrivals.Visit(new List<Pawn> { drover, young }, map, m.faction, gate, 8f) == null)
            {
                if (young != null && !young.Spawned) Find.WorldPawns.PassToWorld(young, PawnDiscardDecideMode.Discard);
                if (drover != null && !drover.Spawned) Arrivals.Discard(drover);
                // Nobody could come this time: tried again in a day.
                m.due = now + GenDate.TicksPerDay;
                return;
            }
            young.Name = new NameSingle(young.def.label);
            m.group.Clear();
            m.group.Add(drover);
            m.group.Add(young);
            m.stage = 5;
            m.due = now + GenDate.TicksPerHour * 6;
            Tell.Message(NewsKind.World, Importance.Important, map, "CC_Stud_YoungComing".Translate(drover.LabelShort.Named("DROVER"), (m.name ?? "?").Named("NAME"),
                (m.faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(new Thing[] { drover, young }), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>At the gate: the young one is handed over and is the colony's.</summary>
        private void YoungAtGate(Mission m, Map map, int now)
        {
            var drover = m.group.FirstOrDefault(p => p != null && p.RaceProps.Humanlike);
            var young = m.group.FirstOrDefault(p => p != null && p.RaceProps.Animal);
            if (young == null || young.Dead || !young.Spawned)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var flag = drover?.GetLord()?.CurLordToil?.FlagLoc ?? IntVec3.Invalid;
            bool there = drover != null && drover.Spawned && flag.IsValid && drover.Position.InHorDistOf(flag, 8f);
            if (!there && now < m.due && drover != null && drover.Spawned && !drover.Dead) return;
            try
            {
                Lords.Leave(young);
                young.SetFaction(Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a stud visit: the young one handed over", e);
            }
            if (drover != null && drover.Spawned) Arrivals.SendAway(new[] { drover });
            Missions.Finish(m, MissionState.Done, "young", null, new LookTargets(young), LetterDefOf.PositiveEvent);
        }

        private static void Died(Mission m)
        {
            var x = m.x;
            Missions.Goodwill(m, -m.penalty * 2);
            Missions.Finish(m, MissionState.Failed, "died", null, x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
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
            string name = m.name ?? m.x?.LabelShort ?? "?";
            string fac = m.faction?.Name ?? "?";
            switch (m.stage)
            {
                case 0:
                case 1: return "CC_Stud_Status0".Translate(name.Named("NAME"), (Drover(m)?.LabelShort ?? "?").Named("DROVER")).Resolve();
                case 2: return (m.variant == Theirs ? "CC_Stud_Status2_Theirs" : "CC_Stud_Status2_Ours").Translate(name.Named("NAME"), (m.tale ?? "?").Named("KIND")).Resolve();
                case 3: return "CC_Stud_Status3".Translate(name.Named("NAME"), fac.Named("FACTION")).Resolve();
                default: return "CC_Stud_Status4".Translate(name.Named("NAME"), fac.Named("FACTION")).Resolve();
            }
        }

        // ---------------- Dev mode ----------------

        /// <summary>The days up now: the standing is judged at once.</summary>
        public static void DebugDaysUp(Mission m)
        {
            if (m == null) return;
            if (m.stage == 2) m.deadline = Find.TickManager.TicksGame;
            else if (m.stage == 4) m.due = Find.TickManager.TicksGame;
        }
    }
}
