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
    // Missions of the road (texts: Defs/MissionDefs/Rimfolk_Missions_Road.xml): escort an old pilgrim to the people's
    // town, carry word to a far settlement of another people, find a neighbour's child lost on the colony's land, and
    // send hands to help bring in a neighbour's harvest. Each tells the people's memory itself, naming the colonist who
    // did it (World/KinMemory.cs), so their people greet that colonist by name afterwards.

    // ===================== Escort a pilgrim =====================

    /// <summary>
    /// An old pilgrim of a friendly people comes to the colony and asks to be walked safely to the people's town (their
    /// holy place). Once the pilgrim is waiting at the colony, any caravan of the colony's that sets out for that town
    /// takes them along; when it gets there, the pilgrim is home, and the people remember who walked with them. A
    /// caravan that turns back leaves the pilgrim to walk on alone; none at all by the deadline, and they set off alone.
    /// </summary>
    public class HappeningWorker_MissionEscort : HappeningWorker_MissionJourney
    {
        protected override string Kind => "escort";

        protected override float RestDays => 75f;

        protected override int Rating => 2;

        protected override float MaxTravelDays => 4f;

        protected override bool Possible(Map map) => StorytellerEvents.Adults(map).Count() >= 2 && Asker(map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Asker(map);
            var town = MissionPeoples.Town(m.faction, map);
            if (m.faction == null || town == null) return false;
            m.place = town.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var kind = Arrivals.PersonKind(m.faction);
            var pilgrim = kind == null ? null : Arrivals.Make(kind, m.faction, new FloatRange(58f, 80f));
            if (pilgrim == null) return false;
            Arrivals.Hold(pilgrim);
            m.x = pilgrim;
            m.name = pilgrim.LabelShort;
            m.variant = Rand.Chance(0.5f) ? "vow" : "home";
            float travel = MissionPeoples.TravelDays(map, town);
            m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
            m.wait = Rand.Range(0.5f, 1f);
            m.days = Mathf.Ceil(m.wait + travel * 1.5f + 3f);
            m.reward = StoryChains.Silver(map, 160f, 300f);
            m.goodwill = 12;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            if (m.x == null || m.x.Dead || m.x.Discarded || MissionPeoples.TownOf(m) == null) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
            return true;
        }

        /// <summary>A caravan of the colony's with its own people, at the town or on its way there.</summary>
        private static Caravan EscortFor(Settlement town)
        {
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || MissionPeoples.Colonists(c).Count == 0) continue;
                if (c.Tile == town.Tile) return c;
                if (c.pather != null && c.pather.Moving && c.pather.Destination == town.Tile) return c;
            }
            return null;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (x.Dead || x.IsPrisonerOfColony)
            {
                Missions.Goodwill(m, -m.penalty * 2);
                FactionMemory.Note(m.faction, "letDown", m.name);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (TownGone(m)) return;
            var town = MissionPeoples.TownOf(m);
            if (m.stage == 0)
            {
                if (now < m.due) return;
                // The pilgrim comes to the colony and waits there for the road.
                IntVec3? spot = null;
                var gather = Avail.ColonyBuildings(map, d => d.defName == "PartySpot" || d.defName == "Campfire").FirstOrDefault();
                if (gather != null) spot = gather.Position;
                if (Arrivals.Visit(new List<Pawn> { x }, map, m.faction, spot, Mathf.Max(6f, (m.deadline - now) / (float)GenDate.TicksPerHour)) == null)
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(x), MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (m.stage == 1)
            {
                var c = EscortFor(town);
                if (c != null)
                {
                    // The pilgrim walks with the colony's caravan from here.
                    if (x.Spawned)
                    {
                        x.GetLord()?.RemovePawn(x);
                        x.DeSpawn();
                    }
                    Arrivals.Hold(x);
                    m.a = MissionPeoples.Colonists(c).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).First();
                    m.stage = 2;
                    Messages.Message("CC_Mission_Escort_Set".Translate(m.name.Named("NAME"), m.a.Named("A"), (m.place ?? "?").Named("PLACE")).Resolve(), new LookTargets(c),
                        MessageTypeDefOf.PositiveEvent, false);
                    return;
                }
                if (now >= m.deadline)
                {
                    // Nobody came: the pilgrim sets off alone.
                    if (x.Spawned) Arrivals.SendAway(new[] { x });
                    Missions.Goodwill(m, -m.penalty);
                    FactionMemory.Note(m.faction, "letDown", m.name, 4);
                    Missions.Finish(m, MissionState.Failed, "alone", null);
                }
                return;
            }
            var a = m.a;
            var caravan = a?.GetCaravan();
            if (caravan != null && caravan.Tile == town.Tile)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = m.reward;
                CaravanInventoryUtility.GiveThing(caravan, silver);
                Missions.Goodwill(m, m.goodwill);
                KinMemory.Note(m.faction, "escorted", a, m.name);
                var thought = StorytellerEvents.Thought("Council_Mission_Escorted");
                foreach (var p in MissionPeoples.Colonists(caravan))
                    if (thought != null) CouncilGameComponent.GiveThought(p, thought);
                var title = DefDatabase<EpithetDef>.GetNamedSilentFail("Council_Epithet_RoadWarden");
                if (title != null && Rand.Chance(0.6f)) CouncilGameComponent.Instance?.epithets.Bestow(title, a, CouncilGameComponent.Instance);
                Missions.Finish(m, MissionState.Done, "arrived", m.reward.ToString(), new LookTargets(caravan));
                return;
            }
            if (a == null || a.Dead || caravan == null)
            {
                // The caravan turned back, or was lost: the pilgrim walks the rest alone.
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "turned", null);
                return;
            }
            if (now >= m.deadline + GenDate.TicksPerDay * 2)
            {
                Missions.Goodwill(m, -m.penalty / 2);
                Missions.Finish(m, MissionState.Failed, "late", null);
            }
        }

        public override void Release(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return;
            if (!x.Spawned && m.started < 0) Arrivals.Discard(x);
            else Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
        }

        public override string Status(Mission m)
        {
            int now = Find.TickManager.TicksGame;
            switch (m.stage)
            {
                case 0: return "CC_Mission_Escort_Coming".Translate(m.name ?? "?", TextUtil.DaysString(Mathf.Max(0, m.due - now))).Resolve();
                case 1: return "CC_Mission_Escort_Waiting".Translate(m.name ?? "?", m.place ?? "?", m.tale ?? "?").Resolve();
                default: return "CC_Mission_Escort_OnRoad".Translate(m.name ?? "?", m.a?.LabelShort ?? "?", m.place ?? "?").Resolve();
            }
        }

        public override LookTargets Targets(Mission m)
        {
            if (m.stage <= 1 && m.x != null && m.x.Spawned) return new LookTargets(m.x);
            var c = m.a?.GetCaravan();
            return c != null ? new LookTargets(c) : base.Targets(m);
        }
    }

    // ===================== Carry word to a far settlement =====================

    /// <summary>
    /// A friendly people asks the colony to carry word (a death, a betrothal, a warning, an offer of peace) to a town of
    /// another people, further than their own riders go. Any caravan of the colony's reaching that town before the
    /// deadline delivers it; the town that receives it pays the messengers, and both peoples remember who carried it.
    /// </summary>
    public class HappeningWorker_MissionWord : HappeningWorker_Mission
    {
        protected override string Kind => "word";

        protected override float RestDays => 60f;

        protected override int Rating => 2;

        public override bool OwnMemory => true;

        private static readonly string[] Variants = { "death", "betrothal", "warning", "peace" };

        /// <summary>A town of another people at peace with the colony, two to six days away by caravan.</summary>
        private static Settlement FarTown(Map map, Faction asker)
        {
            return Find.WorldObjects.Settlements
                .Where(s => s != null && s.HasName && s.Faction != null && s.Faction != asker && !s.Faction.IsPlayer && LivingEvents.Friendly(s.Faction)
                            && (asker == null || !s.Faction.HostileTo(asker)))
                .Where(s =>
                {
                    float d = MissionPeoples.TravelDays(map, s);
                    return d >= 1.5f && d <= 6f;
                })
                .RandomElementWithFallback();
        }

        private static Settlement Destination(Mission m) => Find.WorldObjects.Settlements.FirstOrDefault(s => s != null && s.ID == m.target);

        protected override bool Possible(Map map)
        {
            if (StorytellerEvents.Adults(map).Count() < 2) return false;
            var f = Neighbor(map);
            return f != null && FarTown(map, f) != null;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            var far = FarTown(map, m.faction);
            if (far == null) return false;
            m.target = far.ID;
            m.place = far.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.variant = Variants.RandomElement();
            // Who the word is for: someone at the far town, named and never met.
            var kind = Arrivals.PersonKind(far.Faction);
            var p = kind == null ? null : Arrivals.Make(kind, far.Faction, new FloatRange(30f, 70f));
            m.name = p?.LabelShort ?? "?";
            if (p != null) Arrivals.Discard(p);
            float travel = MissionPeoples.TravelDays(map, far);
            m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
            m.days = Mathf.Ceil(travel * 1.5f + 4f);
            m.reward = StoryChains.Silver(map, 150f, 280f + travel * 30f);
            m.goodwill = 10;
            m.penalty = 4;
            Fill(m, ctx);
            ctx.enemy = far.Faction;
            return true;
        }

        public override bool Start(Mission m, Map map) => Destination(m) != null;

        public override void Check(Mission m, Map map, int now)
        {
            var town = Destination(m);
            if (town == null || town.Faction == null || !LivingEvents.Friendly(town.Faction))
            {
                Missions.Finish(m, MissionState.Failed, "cancelled", null);
                return;
            }
            var here = MissionPeoples.CaravansAt(town).FirstOrDefault(c => MissionPeoples.Colonists(c).Count > 0);
            if (here != null)
            {
                var messenger = MissionPeoples.Colonists(here).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First();
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = m.reward;
                CaravanInventoryUtility.GiveThing(here, silver);
                Missions.Goodwill(m, m.goodwill);
                StoryChains.Goodwill(town.Faction, 5);
                KinMemory.Note(m.faction, "carriedWord", messenger, town.Name);
                FactionMemory.Note(town.Faction, "welcomed", messenger.LabelShort, 4);
                messenger.skills?.Learn(SkillDefOf.Social, 600f);
                Missions.Finish(m, MissionState.Done, "delivered", m.reward.ToString(), new LookTargets(here));
                return;
            }
            if (now >= m.deadline)
            {
                Missions.Goodwill(m, -m.penalty);
                FactionMemory.Note(m.faction, "letDown", m.place, 4);
                Missions.Finish(m, MissionState.Failed, "late", null);
            }
        }

        public override string Status(Mission m) =>
            "CC_Mission_Word_Go".Translate((m.place ?? "?").Named("PLACE"), (Destination(m)?.Faction?.Name ?? "?").Named("FACTION"), (m.tale ?? "?").Named("TALE")).Resolve();

        public override LookTargets Targets(Mission m)
        {
            var s = Destination(m);
            return s != null ? new LookTargets(s) : LookTargets.Invalid;
        }
    }

    // ===================== A lost child =====================

    /// <summary>
    /// A neighbour's child wandered off and is lost somewhere on the colony's land, frightened and hiding. Someone of the
    /// colony must find them (come within a few steps) before nightfall of the second day; found, the child is brought to
    /// the colony, and their family comes to take them home. Their people never forget who found them.
    /// </summary>
    public class HappeningWorker_MissionLostChild : HappeningWorker_Mission
    {
        protected override string Kind => "lostchild";

        protected override float RestDays => 90f;

        protected override int Rating => 1;

        public override bool OwnMemory => true;

        private static FloatRange Ages => ModsConfig.BiotechActive ? new FloatRange(6f, 11f) : new FloatRange(13f, 15f);

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (!base.CanHappen(map, ext)) return false;
            // Not in a fight, not at night, not in killing weather.
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return false;
            float t = map.mapTemperature.OutdoorTemp;
            return t > 0f && t < 35f && GenLocalDate.HourOfDay(map) >= 6 && GenLocalDate.HourOfDay(map) <= 15;
        }

        protected override bool Possible(Map map) => Neighbor(map) != null && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var kind = Arrivals.PersonKind(m.faction);
            Pawn child = null;
            try
            {
                child = kind == null ? null : Arrivals.Make(kind, m.faction, Ages, null, null, ModsConfig.BiotechActive);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mission lost child", e);
            }
            if (child == null) return false;
            Arrivals.Hold(child);
            m.x = child;
            m.name = child.LabelShort;
            m.variant = ModsConfig.BiotechActive ? "child" : "youth";
            m.days = Rand.Range(1.6f, 2.2f);
            m.reward = StoryChains.Silver(map, 120f, 240f);
            m.goodwill = 15;
            m.penalty = 4;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        /// <summary>Somewhere well out from the colony the child could be: open ground, reachable, not in the home area.</summary>
        private static bool HidingPlace(Map map, out IntVec3 cell)
        {
            var center = CampSite.ColonyCenter(map);
            var home = map.areaManager.Home;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some, false);
            for (int tries = 0; tries < 3; tries++)
            {
                float min = tries == 0 ? 35f : tries == 1 ? 22f : 12f;
                if (CellFinder.TryFindRandomCellNear(center, map, 70, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && (home == null || !home[c])
                                                                        && !c.InHorDistOf(center, min) && c.GetFirstBuilding(map) == null
                                                                        && map.reachability.CanReach(center, c, Verse.AI.PathEndMode.OnCell, tp), out cell)) return true;
            }
            cell = IntVec3.Invalid;
            return false;
        }

        public override bool Start(Mission m, Map map)
        {
            var x = m.x;
            if (x == null || x.Dead || x.Discarded || !HidingPlace(map, out var cell)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, cell, map);
            if (!x.Spawned) return false;
            // Frightened, hiding near where they ended up.
            LordMaker.MakeNewLord(m.faction, new LordJob_VisitColony(m.faction, cell, Mathf.RoundToInt((m.days + 1f) * GenDate.TicksPerDay)), map, new List<Pawn> { x });
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (x.Dead || x.IsPrisonerOfColony)
            {
                Missions.Goodwill(m, -m.penalty * 3);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                if (!x.Spawned || x.Map != map)
                {
                    // Wandered off the colony's land: their own searchers will find them, or not.
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "searchers", null);
                    return;
                }
                var finder = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => !c.Downed && c.Awake() && !c.InMentalState && c.RaceProps.Humanlike
                                                                                     && c.Position.InHorDistOf(x.Position, 6f) && GenSight.LineOfSight(c.Position, x.Position, map));
                if (finder != null)
                {
                    m.stage = 1;
                    m.a = finder;
                    m.due = now + Mathf.RoundToInt(Rand.Range(0.3f, 0.6f) * GenDate.TicksPerDay);
                    // They follow their finder back to the colony and wait there for their family.
                    x.GetLord()?.RemovePawn(x);
                    var spot = Avail.ColonyBuildings(map, d => d.defName == "PartySpot" || d.defName == "Campfire").FirstOrDefault()?.Position ?? finder.Position;
                    LordMaker.MakeNewLord(m.faction, new LordJob_VisitColony(m.faction, spot, GenDate.TicksPerDay * 2), map, new List<Pawn> { x });
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.due - now));
                    if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(x, finder), MessageTypeDefOf.PositiveEvent, false);
                    return;
                }
                if (now >= m.deadline)
                {
                    // Their own searchers found them first.
                    x.GetLord()?.RemovePawn(x);
                    Arrivals.SendAway(new[] { x });
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "searchers", null);
                }
                return;
            }
            if (now < m.due) return;
            // Their family comes, and takes them home.
            var kin = new List<Pawn>();
            var kind = Arrivals.PersonKind(m.faction);
            int n = Rand.RangeInclusive(1, 2);
            for (int i = 0; kind != null && i < n; i++)
            {
                var p = Arrivals.Make(kind, m.faction, new FloatRange(25f, 50f), null, (x.Name as NameTriple)?.Last);
                if (p != null) kin.Add(p);
            }
            var lord = kin.Count > 0 ? Arrivals.Visit(kin, map, m.faction, x.Spawned ? x.Position : (IntVec3?)null, 5f) : null;
            if (lord == null) foreach (var p in kin.Where(p => !p.Spawned)) Arrivals.Discard(p);
            if (x.Spawned && x.Map == map)
            {
                x.GetLord()?.RemovePawn(x);
                if (lord != null && lord.CanAddPawn(x)) lord.AddPawn(x);
                else Arrivals.SendAway(new[] { x });
            }
            var spotNear = x.Spawned ? x.Position : DropCellFinder.TradeDropSpot(map);
            VarietyUtil.PaySilver(map, spotNear, m.reward);
            Missions.Goodwill(m, m.goodwill);
            var finderNow = m.a;
            KinMemory.Note(m.faction, "foundChild", finderNow, m.name);
            var thought = StorytellerEvents.Thought("Council_Mission_ChildHome");
            if (thought != null && finderNow != null && !finderNow.Dead) CouncilGameComponent.GiveThought(finderNow, thought);
            var title = DefDatabase<EpithetDef>.GetNamedSilentFail("Council_Epithet_Finder");
            if (title != null && finderNow != null && Rand.Chance(0.7f)) CouncilGameComponent.Instance?.epithets.Bestow(title, finderNow, CouncilGameComponent.Instance);
            Missions.Finish(m, MissionState.Done, "found", m.reward.ToString(), x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
        }

        public override void Release(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return;
            if (!x.Spawned && m.started < 0) Arrivals.Discard(x);
            else Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
        }

        public override string Status(Mission m) =>
            m.stage == 0
                ? "CC_Mission_LostChild_Search".Translate(m.name ?? "?", TextUtil.DaysString(Mathf.Max(0, m.deadline - Find.TickManager.TicksGame))).Resolve()
                : "CC_Mission_LostChild_Found".Translate(m.name ?? "?", m.a?.LabelShort ?? "?").Resolve();
    }

    // ===================== Help with a harvest =====================

    /// <summary>
    /// A friendly town can't bring its harvest in before the weather turns and asks for hands: at least two of the
    /// colony's people at the town by caravan, staying about a day and a half in the fields. They come home with a
    /// share of the harvest and the town's thanks; half the time is half the share.
    /// </summary>
    public class HappeningWorker_MissionHarvest : HappeningWorker_MissionJourney
    {
        protected override string Kind => "harvest";

        protected override float RestDays => 60f;

        protected override int Rating => 2;

        protected override float MaxTravelDays => 2.5f;

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var season = GenLocalDate.Season(map);
            return (season == Season.Summer || season == Season.Fall || season == Season.PermanentSummer) && base.CanHappen(map, ext);
        }

        private static bool Hand(Pawn p) => p != null && !p.Dead && p.skills?.GetSkill(SkillDefOf.Plants) is SkillRecord r && !r.TotallyDisabled
                                            && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing);

        protected override bool Possible(Map map) => IncidentWorker_Happening.Colonists(map).Count(Hand) >= 3 && Asker(map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Asker(map);
            var town = MissionPeoples.Town(m.faction, map);
            if (m.faction == null || town == null) return false;
            m.place = town.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Plants) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.target = 2;
            float travel = MissionPeoples.TravelDays(map, town);
            m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
            m.variant = m.faction.def.techLevel <= TechLevel.Medieval ? "sickles" : "machines";
            m.days = Mathf.Ceil(travel * 2f + 4f);
            m.reward = StoryChains.Silver(map, 100f, 200f);
            m.goodwill = 10;
            m.penalty = 4;
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map) => MissionPeoples.TownOf(m) != null;

        private const int HoursNeeded = 36;

        public override void Check(Mission m, Map map, int now)
        {
            if (TownGone(m)) return;
            var town = MissionPeoples.TownOf(m);
            var here = MissionPeoples.CaravansAt(town).OrderByDescending(c => MissionPeoples.Colonists(c).Count(Hand)).FirstOrDefault();
            int hands = here == null ? 0 : MissionPeoples.Colonists(here).Count(Hand);
            if (hands >= m.target)
            {
                if (m.stage == 0)
                {
                    m.stage = 1;
                    m.lastLook = now;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(HoursNeeded * GenDate.TicksPerHour));
                    if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(here), MessageTypeDefOf.PositiveEvent, false);
                }
                else m.tally += Mathf.Max(0, now - Mathf.Max(m.lastLook, m.started));
            }
            m.lastLook = now;
            int need = HoursNeeded * GenDate.TicksPerHour;
            if (m.tally >= need && here != null)
            {
                Harvested(m, here, 1f);
                Missions.Goodwill(m, m.goodwill);
                Missions.Finish(m, MissionState.Done, "harvested", null, new LookTargets(here));
                return;
            }
            if (now < m.deadline) return;
            if (m.tally * 2 >= need && here != null)
            {
                Harvested(m, here, 0.5f);
                Missions.Goodwill(m, m.goodwill / 2);
                Missions.Finish(m, MissionState.Done, "partial", null, new LookTargets(here), LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            FactionMemory.Note(m.faction, "absent", m.place);
            Missions.Finish(m, MissionState.Failed, "missed", null);
        }

        /// <summary>A share of the harvest in the caravan's packs, the fields' lessons for the hands, and the town's memory of who came.</summary>
        private static void Harvested(Mission m, Caravan c, float share)
        {
            var crop = new[] { "RawCorn", "RawRice", "RawPotatoes" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);
            if (crop != null)
            {
                int left = Mathf.RoundToInt(Rand.Range(90f, 160f) * share);
                while (left > 0)
                {
                    var t = ThingMaker.MakeThing(crop);
                    t.stackCount = Mathf.Min(left, crop.stackLimit);
                    left -= t.stackCount;
                    CaravanInventoryUtility.GiveThing(c, t);
                }
            }
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Mathf.Max(10, Mathf.RoundToInt(m.reward * share / 10f) * 10);
            CaravanInventoryUtility.GiveThing(c, silver);
            var hands = MissionPeoples.Colonists(c).Where(Hand).ToList();
            var thought = StorytellerEvents.Thought("Council_Mission_HarvestHands");
            foreach (var p in hands)
            {
                p.skills?.Learn(SkillDefOf.Plants, 1200f * share);
                if (thought != null) CouncilGameComponent.GiveThought(p, thought);
            }
            KinMemory.Note(m.faction, "harvestHelp", hands.FirstOrDefault(), m.place, share < 1f ? 5 : 0);
        }

        public override string Status(Mission m)
        {
            if (m.stage == 0) return "CC_Mission_Harvest_Go".Translate(m.target, m.place ?? "?", m.tale ?? "?").Resolve();
            int hours = Mathf.Min(HoursNeeded, m.tally / GenDate.TicksPerHour);
            return "CC_Mission_Harvest_There".Translate(m.place ?? "?", hours, HoursNeeded).Resolve();
        }
    }
}
