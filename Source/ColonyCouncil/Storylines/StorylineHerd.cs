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
    // S3, the strayed herd (docs/quests-proposals.md; the rules in Flows/HerdFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Herd.xml). A herding people's animals (the herding folk first; a herd of their age's
    // own kind, from the game and every animal mod enabled that keeps a herd and can live here) stray onto the colony's land
    // in summer or autumn. Part 1: they are the colony's to keep until their herders come, three or four days: the game's own
    // pen animals, so its handlers rope them into a pen, unpenned ones may roam off the map (the game's roaming), and the
    // land's predators may take them as they take any animal. The herders come, count them and lead them home. Part 2: they
    // ask the colony to keep three to five of the herd over the winter, until spring, fed (the game's hunger: grazing, hay or
    // kibble; what's in store against what they eat shown in the quest). Part 3, in spring: the herders come for them; all
    // well, they leave one or two young animals of their herd with the colony; some died, they ask to be paid for them, or
    // the colony can buy the ones that are left (a card). Animals are led out of the pens to the herders as the game leads a
    // roped animal (through fences and gates), then go home with them.

    /// <summary>The strayed herd beyond its parts: who asks, which animals, leading them out, the endings.</summary>
    public class HerdRules : StorylineRules
    {
        public override string Kind => HerdFlow.Key;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The herding folk (any people of the herders' calling); else a people of the tribal or medieval age; else any people at peace.</summary>
        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Herders)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.TechOf(f) <= TechLevel.Medieval)
            ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        public static JobDef LedOutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Storyline_LedOut");

        /// <summary>The season as the herders' ask reads it: "summer", "fall", or "none" where the year has no seasons.</summary>
        public static string SeasonWord(Map map)
        {
            switch (GenLocalDate.Season(map))
            {
                case Season.Summer: return "summer";
                case Season.Fall: return "fall";
                case Season.PermanentSummer:
                case Season.PermanentWinter: return "none";
                case Season.Spring: return "spring";
                default: return "winter";
            }
        }

        /// <summary>Days until spring comes here (NaN when it doesn't within two years' worth of days).</summary>
        public static float DaysToSpring(Map map)
        {
            var longLat = Find.WorldGrid.LongLatOf(map.Tile);
            long abs = Find.TickManager.TicksAbs;
            for (int d = 1; d <= 120; d++)
                if (GenDate.Season(abs + (long)d * GenDate.TicksPerDay, longLat) == Season.Spring)
                    return d;
            return float.NaN;
        }

        /// <summary>
        /// A herd animal from the game's and the mods' own: an animal that keeps a herd and roams (so a pen holds it), no
        /// predator, insect or dryad, calm enough to keep, of a farm's kind (it gives milk or wool, or is traded as farm stock),
        /// never a Pokémon, and able to live outside here now.
        /// </summary>
        public static bool Fits(PawnKindDef k, Map map)
        {
            var rp = k?.RaceProps;
            if (rp == null || k.race == null || !rp.Animal || rp.predator || rp.Insect || rp.Dryad || rp.IsMechanoid || rp.Humanlike || rp.IsAnomalyEntity) return false;
            if (!rp.herdAnimal || rp.roamMtbDays == null || rp.baseBodySize < 0.4f || rp.baseBodySize > 3.5f || PokeWorldLink.IsPokemon(k)) return false;
            float wild;
            try
            {
                wild = k.race.GetStatValueAbstract(StatDefOf.Wildness);
            }
            catch
            {
                return false;
            }
            if (wild > 0.6f) return false;
            bool farm = k.race.HasComp(typeof(CompMilkable)) || k.race.HasComp(typeof(CompShearable)) || (k.race.tradeTags?.Contains("AnimalFarm") ?? false);
            return farm && (map?.mapTemperature == null || map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f));
        }

        /// <summary>
        /// The herd's kind: the animals the people's age keeps (HerdFlow.Kinds) four times likelier, the land's own twice,
        /// and any other herd animal the game or a mod enabled adds that fits (<see cref="Fits"/>).
        /// </summary>
        public static PawnKindDef KindFor(Map map, Faction f)
        {
            int age = f != null ? HomeQuests.AgeOf(f) : (int)Ages.Ours;
            var named = new HashSet<string>(HerdFlow.Kinds(age));
            var land = map?.Biome != null ? new HashSet<PawnKindDef>(BiomeAnimals.Of(map.Biome)) : new HashSet<PawnKindDef>();
            return DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => Fits(k, map))
                .RandomElementByWeightWithFallback(k => (named.Contains(k.defName) ? 4f : 1f) * (land.Contains(k) ? 2f : 1f));
        }

        /// <summary>A grown animal of the herd's kind (female unless <paramref name="male"/>), or a young one.</summary>
        public static Pawn Make(PawnKindDef kind, Faction f, bool young = false, bool male = false)
        {
            if (kind?.RaceProps == null) return null;
            try
            {
                var stages = kind.RaceProps.lifeStageAges;
                float grown = stages.NullOrEmpty() ? 1f : stages.Last().minAge;
                float age = young ? (stages != null && stages.Count > 1 ? stages[1].minAge : grown * 0.3f) + Rand.Range(0.01f, 0.08f) : grown + Rand.Range(0.2f, 2f);
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: age,
                    fixedChronologicalAge: age, fixedGender: kind.RaceProps.hasGenders ? (male ? Gender.Male : Gender.Female) : (Gender?)null));
                p?.inventory?.DestroyAll();
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: an animal of the herd", e);
                return null;
            }
        }

        /// <summary>The herd's animals still alive on this map.</summary>
        public static List<Pawn> Here(Storyline s, Map map) => s.group.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map).ToList();

        /// <summary>In a pen, or in a barn: a room under a roof.</summary>
        public static bool Penned(Pawn a) => AnimalPenUtility.GetCurrentPenOf(a, false) != null || a.GetRoom() is Room r && !r.PsychologicallyOutdoors;

        /// <summary>The herd's kind, in a sentence's words: "five goats".</summary>
        public static string Words(Storyline s, int n)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s?.what ?? "");
            if (kind == null) return n.ToString();
            return n == 1 ? Find.ActiveLanguageWorker.WithIndefiniteArticle(kind.label) : n + " " + kind.GetLabelPlural();
        }

        /// <summary>The colony's to keep for a while: its own animals as the game has them, to rope into a pen and feed.</summary>
        public static void Keep(Pawn a)
        {
            if (a == null || a.Dead || a.Faction == Faction.OfPlayer) return;
            try
            {
                Lords.Leave(a);
                a.SetFaction(Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the herd kept", e);
            }
        }

        // ---------------- Handing the herd back ----------------

        /// <summary>Their herders walk in to the gate (the one who asked among them) and wait there; the herders who came, or none.</summary>
        public static List<Pawn> Herders(Storyline s, Map map, int n, float hours)
        {
            var f = s.giver;
            var party = new List<Pawn>();
            if (s.x != null && StoryChains.CanVisit(s.x)) party.Add(s.x);
            for (int i = party.Count; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Herders_Drover"), f, new FloatRange(18f, 55f));
                if (p != null) party.Add(p);
            }
            if (party.Count == 0 || Arrivals.Visit(party, map, f, PeoplesD.GateSpot(map), hours) == null) return new List<Pawn>();
            return party.Where(p => p.Spawned).ToList();
        }

        /// <summary>
        /// Each animal is led out of its pen or barn to the herders at the gate (a walk that crosses fences and gates as a
        /// roped animal does, while it is still the colony's so its doors open for it).
        /// </summary>
        public static void LeadOut(IEnumerable<Pawn> animals, IntVec3 to, Map map)
        {
            var job = LedOutJob;
            foreach (var a in animals.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && a.Faction == Faction.OfPlayer))
            {
                try
                {
                    a.roping?.BreakAllRopes();
                    if (job == null || !CellFinder.TryFindRandomCellNear(to, map, 4, c => c.Standable(map) && !c.Fogged(map), out var cell)) continue;
                    var j = JobMaker.MakeJob(job, cell);
                    j.locomotionUrgency = LocomotionUrgency.Walk;
                    j.expiryInterval = GenDate.TicksPerHour * 6;
                    a.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: the herd led out", e);
                }
            }
        }

        /// <summary>Theirs again: the animals go home with the herders (all of them walking off together).</summary>
        public static void HandOver(IEnumerable<Pawn> animals, List<Pawn> herders, Faction f, Map map)
        {
            var going = new List<Pawn>();
            foreach (var a in animals.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map).ToList())
            {
                try
                {
                    a.jobs?.StopAll();
                    Lords.Leave(a);
                    a.roping?.BreakAllRopes();
                    if (a.playerSettings != null) a.playerSettings.Master = null;
                    if (a.Faction != f) a.SetFaction(f);
                    if (a.Downed || !ReachabilityUtility.CanReachMapEdge(a))
                    {
                        // Shut in where it can't walk out: the herders carry or coax it out of sight.
                        a.DeSpawn();
                        if (!Find.WorldPawns.Contains(a)) Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.Decide);
                        continue;
                    }
                    going.Add(a);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: the herd handed over", e);
                }
            }
            var walkers = herders.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map).ToList();
            Lords.Leave(walkers);
            var all = walkers.Concat(going).ToList();
            if (all.Count > 0) LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, true), map, all);
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var f = s.giver;
            switch (key)
            {
                case "taken":
                {
                    if (MapFacts.ThreatOn(map)) return false;
                    // The herders come for their animals all the same and take them home at once, leaving nothing behind.
                    var herd = Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
                    HandOver(herd, Herders(s, map, 2, 8f), f, map);
                    StoryChains.Goodwill(f, -4, GoodwillWhy.Of(GoodwillReasonMath.KeptTheirs));
                    return true;
                }
                case "young":
                    StoryChains.Goodwill(f, 10, GoodwillWhy.Of(GoodwillReasonMath.KeptTheirs));
                    FactionMemory.Note(f, "keptWord", Storylines.Label(s));
                    return true;
                case "paid":
                case "bought":
                    StoryChains.Goodwill(f, 4, GoodwillWhy.Of(GoodwillReasonMath.Paid));
                    return true;
                case "unpaid":
                    StoryChains.Goodwill(f, -15, GoodwillWhy.Of(GoodwillReasonMath.Unpaid));
                    FactionMemory.Note(f, "wordBroken", Storylines.Label(s));
                    return true;
                case "lost":
                    StoryChains.Goodwill(f, -8, GoodwillWhy.Of(GoodwillReasonMath.KeptTheirs));
                    return true;
                case StorylineMath.Faded:
                    // It could not go on: their animals still with the colony walk home on their own.
                    HandOver(Here(s, map).Where(a => a.Faction == Faction.OfPlayer), new List<Pawn>(), f, map);
                    return true;
            }
            return true;
        }

        /// <summary>Their people are at war with the colony now: nobody will come for the animals, so they are the colony's to keep.</summary>
        public override void Gone(Storyline s, Map map)
        {
            if (s.group.Any(a => a != null && !a.Dead && a.Faction == Faction.OfPlayer)) s.Set("ours");
        }
    }

    // ===================== Part 1: pen the strayed animals =====================

    /// <summary>
    /// Five to nine head of their herd have strayed onto the colony's land (there as soon as the herders' word comes, at the
    /// edge of the map). Taken on, they are the colony's to keep for three or four days: the game's pen animals, roped into a
    /// pen by its handlers. Then their herders come and count them: every one in a pen or a barn, penned; half or more still
    /// here, most; fewer, lost. The herders lead them home. Declined: the strays wander off.
    /// </summary>
    public class HappeningWorker_HerdStray : HappeningWorker_StorylinePart
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) =>
            (Storylines.debugForce || HerdFlow.AskSeason(HerdRules.SeasonWord(map))) && Storylines.Rules(HerdFlow.Key).Giver(map) != null && HerdRules.KindFor(map, null) != null
            && VarietyUtil.Best(map, SkillDefOf.Animals) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(HerdFlow.Key).Giver(map);
            if (s == null || f == null) return false;
            var kind = HerdRules.KindFor(map, f);
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals);
            if (kind == null || m.a == null) return false;
            var x = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Herders_Warden"), f, new FloatRange(25f, 60f));
            if (x == null) return false;
            Arrivals.Hold(x);
            int n = HerdFlow.StrayCount(IncidentWorker_Happening.Colonists(map).Count, Rand.Value);
            var herd = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var a = HerdRules.Make(kind, null, false, i == 0);
                if (a != null) herd.Add(a);
            }
            if (herd.Count == 0)
            {
                Arrivals.Discard(x);
                return false;
            }
            foreach (var a in herd) Arrivals.Hold(a);
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.what = kind.defName;
            s.tale = kind.GetLabelPlural();
            s.count = herd.Count;
            s.group.AddRange(herd);
            if (HerdRules.SeasonWord(map) != "none") s.Set("winter");
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.target = herd.Count;
            m.goodwill = 8;
            m.penalty = 6;
            m.days = Rand.Range(HerdFlow.StrayMinDays, HerdFlow.StrayMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.count = herd.Count;
            return true;
        }

        /// <summary>The strays are there on the land as soon as the herders' word comes, near one edge.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            if (s == null || map == null || !RCellFinder.TryFindRandomPawnEntryCell(out var edge, map, CellFinder.EdgeRoadChance_Animal)) return false;
            if (!base.Begin(ctx, ext)) return false;
            foreach (var a in s.group.ToList())
                if (!Arrivals.Spawn(a, map, edge))
                    s.group.Remove(a);
            ctx.spot = edge;
            return s.group.Count > 0;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            var herd = HerdRules.Here(s, map);
            if (herd.Count == 0) return false;
            foreach (var a in herd) HerdRules.Keep(a);
            m.stage = 0;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(herd));
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            StorylineKit.Remind(P, s, m, map, now);
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.deadline) return;
                    // Not into a fight: they wait on the road a while.
                    if (MapFacts.ThreatOn(map) && now < m.deadline + GenDate.TicksPerDay) return;
                    var herders = HerdRules.Herders(s, map, 2 + s.count / 4, 10f);
                    if (herders.Count == 0)
                    {
                        // Nobody could come: the strays walk home on their own.
                        HerdRules.HandOver(HerdRules.Here(s, map), herders, s.giver, map);
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    m.group.Clear();
                    m.group.AddRange(herders);
                    m.stage = 1;
                    m.lastLook = now;
                    Note(m, map, "herders", new LookTargets(herders));
                    return;
                }
                case 1:
                {
                    // At the gate: they count the herd where it stands, and the animals are led out to them.
                    var herders = m.group.Where(p => p != null && p.Spawned && !p.Dead).ToList();
                    var gate = PeoplesD.GateSpot(map);
                    bool there = herders.Any(p => gate == null || p.Position.InHorDistOf(gate.Value, 12f)) || now - m.lastLook > GenDate.TicksPerHour * 4;
                    if (!there) return;
                    var here = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
                    int penned = here.Count(HerdRules.Penned);
                    s.lost = Mathf.Max(0, s.count - here.Count);
                    string how = HerdFlow.StrayOutcome(penned, here.Count, s.count);
                    s.Set("count:" + how);
                    m.target = here.Count;
                    m.tally = penned;
                    HerdRules.LeadOut(here, herders.FirstOrDefault()?.Position ?? gate ?? map.Center, map);
                    if (herders.Count > 0) Say(herders[0], m, how);
                    m.stage = 2;
                    m.lastLook = now;
                    return;
                }
                default:
                {
                    // Led out: once they reach the herders (or six hours on), all go home together.
                    var herders = m.group.Where(p => p != null && p.Spawned && !p.Dead).ToList();
                    var herd = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
                    bool met = herd.All(a => herders.Any(h => h.Position.InHorDistOf(a.Position, 8f)));
                    if (!met && now - m.lastLook < GenDate.TicksPerHour * 6) return;
                    HerdRules.HandOver(herd, herders, s.giver, map);
                    string how = s.Has("count:penned") ? "penned" : s.Has("count:most") ? "most" : "lost";
                    s.flags.RemoveAll(f => f.StartsWith("count:"));
                    if (how == "penned") Missions.Goodwill(m, m.goodwill);
                    else if (how == "most") Missions.Goodwill(m, m.goodwill / 2);
                    else Missions.Goodwill(m, -m.penalty);
                    End(m, how == "lost" ? MissionState.Failed : MissionState.Done, how, herders.Count > 0 ? new LookTargets(herders) : null, HerdRules.Words(s, s.lost),
                        m.tally.ToString());
                    return;
                }
            }
        }

        /// <summary>Turned down (or the offer ran out): the strays wander off the colony's land.</summary>
        public override void Release(Mission m)
        {
            var s = Story(m);
            if (s == null || m.started >= 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var a in s.group.Where(a => a != null && !a.Dead && a.Spawned && a.Faction == null))
                a.mindState.exitMapAfterTick = now + Mathf.RoundToInt(Rand.Range(2f, 8f) * GenDate.TicksPerHour);
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            var here = s == null || map == null ? new List<Pawn>() : HerdRules.Here(s, map);
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            var here = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            return "CC_Storyline_Herd_Penned".Translate(here.Count(HerdRules.Penned).Named("PENNED"), here.Count.Named("HERE"), s.count.Named("TOTAL")).Resolve();
        }
    }

    // ===================== Part 2: keep part of the herd over the winter =====================

    /// <summary>
    /// The herders ask the colony to keep three to five of their animals until spring (a season where the year has none),
    /// fed: the herders bring them to the gate and they are the colony's to pen and feed as the game feeds its animals
    /// (grazing, hay, kibble). The quest shows what's in store against what they eat. At spring: kept (every one alive and
    /// here), or some died (or roamed off).
    /// </summary>
    public class HappeningWorker_HerdWinter : HappeningWorker_StorylinePart
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && DefDatabase<PawnKindDef>.GetNamedSilentFail(Storylines.current.what ?? "") != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s?.what ?? "");
            if (s?.giver == null || kind == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            int k = HerdFlow.KeepCount(s.count);
            var herd = new List<Pawn>();
            for (int i = 0; i < k; i++)
            {
                var a = HerdRules.Make(kind, s.giver, false, i == 0);
                if (a != null) herd.Add(a);
            }
            if (herd.Count == 0) return false;
            foreach (var a in herd) Arrivals.Hold(a);
            // The herd of part 1 has gone home: the storyline's animals now are the ones kept over the winter.
            s.group.Clear();
            s.group.AddRange(herd);
            s.goalA = herd.Count;
            s.count = herd.Count;
            bool seasons = HerdRules.SeasonWord(map) != "none";
            m.faction = s.giver;
            m.place = s.place;
            m.x = s.x;
            m.target = herd.Count;
            m.goodwill = 6;
            m.penalty = 6;
            m.days = HerdFlow.KeepDays(seasons ? HerdRules.DaysToSpring(map) : float.NaN, seasons);
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.count = herd.Count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            var herd = s.group.Where(a => a != null && !a.Dead && !a.Spawned).ToList();
            var herders = HerdRules.Herders(s, map, 2, 6f);
            var at = herders.FirstOrDefault()?.Position ?? PeoplesD.GateSpot(map);
            foreach (var a in herd)
                if (Arrivals.Spawn(a, map, at))
                    HerdRules.Keep(a);
            if (!s.group.Any(a => a != null && a.Spawned)) return false;
            m.stage = 0;
            m.lastLook = Find.TickManager.TicksGame;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(HerdRules.Here(s, map)));
            return true;
        }

        /// <summary>What the kept animals eat in a day, and the fodder in store (hay, kibble and the like) as nutrition.</summary>
        public static (float perDay, float stored) Fodder(Storyline s, Map map)
        {
            float perDay = 0f;
            foreach (var a in HerdRules.Here(s, map))
            {
                try
                {
                    perDay += SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay(a);
                }
                catch
                {
                    perDay += 2f;
                }
            }
            float stored = 0f;
            foreach (var name in new[] { "Hay", "Kibble" })
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d?.ingestible == null) continue;
                stored += Missions.Count(map, name) * d.GetStatValueAbstract(StatDefOf.Nutrition);
            }
            return (perDay, stored);
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            // Every three days, a word when the store won't last them to the end.
            if (now - m.lastLook >= GenDate.TicksPerDay * 3 && now < m.deadline)
            {
                m.lastLook = now;
                var (perDay, stored) = Fodder(s, map);
                float need = HerdFlow.FodderNeed(perDay, (m.deadline - now) / (float)GenDate.TicksPerDay);
                if (stored < need && GenLocalDate.Season(map) == Season.Winter) Note(m, map, "short", new LookTargets(HerdRules.Here(s, map)), MessageTypeDefOf.CautionInput);
            }
            if (now < m.deadline) return;
            var here = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            s.lost = Mathf.Max(0, s.goalA - here.Count);
            string how = HerdFlow.WinterOutcome(here.Count, s.goalA);
            Missions.Goodwill(m, how == "kept" ? m.goodwill : -m.penalty / 2);
            End(m, MissionState.Done, how, here.Count > 0 ? new LookTargets(here) : null, HerdRules.Words(s, s.lost));
        }

        /// <summary>Kept until spring: nothing is let go when the keeping ends (the herders come for them next).</summary>
        public override void Release(Mission m)
        {
            var s = Story(m);
            if (s == null || m.started >= 0) return;
            foreach (var a in s.group.Where(a => a != null && !a.Spawned && !a.Dead)) Arrivals.Discard(a);
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var here = s == null || m.Map == null ? new List<Pawn>() : HerdRules.Here(s, m.Map);
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            var here = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            var (perDay, stored) = Fodder(s, map);
            float days = Mathf.Max(0f, (m.deadline - Find.TickManager.TicksGame) / (float)GenDate.TicksPerDay);
            return "CC_Storyline_Herd_Winter".Translate(here.Count.Named("HERE"), s.goalA.Named("TOTAL"), here.Count(HerdRules.Penned).Named("PENNED"),
                Mathf.RoundToInt(stored).Named("STORED"), Mathf.RoundToInt(HerdFlow.FodderNeed(perDay, days)).Named("NEED")).Resolve();
        }
    }

    // ===================== Part 3: the herders come in spring =====================

    /// <summary>
    /// In spring the herders come for the animals they left. Every one kept: they lead them home and leave one or two young
    /// of their herd with the colony (real animals of the herd's kind, the colony's). Some died: the card asks the colony to
    /// pay for the dead (their worth, carried out to the herders), to buy the ones left (nine tenths of their worth; they stay
    /// the colony's), or to refuse.
    /// </summary>
    public class HappeningWorker_HerdSpring : HappeningWorker_StorylinePart
    {
        public const string Card = "Rimfolk_Storyline_HerdDead";

        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.giver != null;

        protected override string Silver(Mission m, Storyline s) => s?.amount.ToString();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var here = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s.what ?? "");
            float unit = kind?.race?.BaseMarketValue ?? 100f;
            s.lost = Mathf.Max(0, s.goalA - here.Count);
            s.amount = s.lost > 0 ? HerdFlow.DeadPay(unit * s.lost) : 0;
            s.fee = HerdFlow.BuyPrice(here.Sum(a => a.MarketValue));
            m.faction = s.giver;
            m.place = s.place;
            m.x = s.x;
            m.target = here.Count;
            m.days = 2f;
            m.goodwill = 8;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.count = here.Count;
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

        private static List<Pawn> HerdersOf(Mission m) => m.group.Where(p => p != null && p.Spawned && !p.Dead && p.RaceProps.Humanlike).ToList();

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var herd = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            switch (m.stage)
            {
                case 0:
                {
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay) return;
                    var herders = HerdRules.Herders(s, map, 2, 12f);
                    if (herders.Count == 0)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    m.group.Clear();
                    m.group.AddRange(herders);
                    m.stage = 1;
                    m.lastLook = now;
                    Note(m, map, "came", new LookTargets(herders));
                    return;
                }
                case 1:
                {
                    var herders = HerdersOf(m);
                    var gate = PeoplesD.GateSpot(map);
                    if (!herders.Any(p => gate == null || p.Position.InHorDistOf(gate.Value, 12f)) && now - m.lastLook < GenDate.TicksPerHour * 4) return;
                    s.lost = Mathf.Max(0, s.goalA - herd.Count);
                    if (s.lost == 0)
                    {
                        // Every one kept: led home, and young of their herd left with the colony.
                        Young(m, s, map, herders);
                        HerdRules.LeadOut(herd, herders.FirstOrDefault()?.Position ?? gate ?? map.Center, map);
                        m.variant = "young";
                        m.stage = 3;
                        m.lastLook = now;
                        Say(herders.FirstOrDefault(), m, "young");
                        return;
                    }
                    var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s.what ?? "");
                    s.amount = HerdFlow.DeadPay((kind?.race?.BaseMarketValue ?? 100f) * s.lost);
                    s.fee = HerdFlow.BuyPrice(herd.Sum(a => a.MarketValue));
                    if (Ask(m, s, map, now, herders)) return;
                    m.stage = 2;
                    m.lastLook = now;
                    return;
                }
                case 2:
                    // The card: answered (Answered moves on), or lost without an answer: the colony pays nothing, and they take the rest.
                    if (!StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
                    {
                        if (now - m.lastLook > GenDate.TicksPerDay / 2 || !Ask(m, s, map, now, HerdersOf(m)))
                            Answered(m, s, StorylineKit.Context(s, m, map), 2, true);
                    }
                    return;
                default:
                {
                    // Led out to the herders: once they reach them (or six hours on), all go home together.
                    var herders = HerdersOf(m);
                    bool met = herd.All(a => herders.Any(h => h.Position.InHorDistOf(a.Position, 8f)));
                    if (!met && now - m.lastLook < GenDate.TicksPerHour * 6) return;
                    HerdRules.HandOver(herd, herders, s.giver, map);
                    string how = m.variant ?? "young";
                    if (how == "young") Missions.Goodwill(m, m.goodwill);
                    var look = herders.Count > 0 ? new LookTargets(herders) : null;
                    End(m, how == "unpaid" ? MissionState.Failed : MissionState.Done, how, look, m.tale, how == "paid" ? s.amount.ToString() : how == "bought" ? s.fee.ToString() : null);
                    return;
                }
            }
        }

        /// <summary>The young they leave: one or two young animals of their herd, the colony's now, beside the herders.</summary>
        private static void Young(Mission m, Storyline s, Map map, List<Pawn> herders)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s.what ?? "");
            var young = new List<Pawn>();
            int n = HerdFlow.YoungCount(s.goalA);
            var at = herders.FirstOrDefault()?.Position ?? PeoplesD.GateSpot(map) ?? map.Center;
            for (int i = 0; i < n; i++)
            {
                var y = HerdRules.Make(kind, Faction.OfPlayer, true, i == 1);
                if (y == null || !Arrivals.Spawn(y, map, at)) continue;
                young.Add(y);
            }
            m.tale = young.Count > 0 ? ThiefTrail.Kinds(young) : null;
            // None could be made (the herd's kind gone from the game): their thanks are words only, and the letter says so.
            if (young.Count == 0) s.Set("noyoung");
            if (young.Count > 0)
                StorylineKit.Note(SpringPart, s, m, map, "young", new LookTargets(young), MessageTypeDefOf.PositiveEvent, m.tale);
        }

        private static StorylinePart SpringPart => Storylines.PartDef(HerdFlow.Key, "spring")?.GetModExtension<StorylinePart>();

        /// <summary>The card: pay for the dead, buy the rest, or refuse. False: nobody of the colony to answer now.</summary>
        private bool Ask(Mission m, Storyline s, Map map, int now, List<Pawn> herders)
        {
            var herd = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            bool asked = StorylineKit.Ask(Card, s, m, map, c =>
            {
                c.stranger = herders.FirstOrDefault();
                c.count = s.fee;
                c.variant = s.amount;
                c.group = herd.Cast<Pawn>().ToList();
            });
            m.stage = 2;
            m.lastLook = now;
            if (asked) Say(herders.FirstOrDefault(), m, "ask");
            return asked;
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            var herders = HerdersOf(m);
            var herd = HerdRules.Here(s, map).Where(a => a.Faction == Faction.OfPlayer).ToList();
            var at = herders.FirstOrDefault()?.Position ?? PeoplesD.GateSpot(map) ?? map.Center;
            switch (option)
            {
                case 0:
                    // Paid for the dead (the silver carried out to them by the card's own carrying), the rest led home.
                    m.variant = "paid";
                    Missions.Goodwill(m, 4);
                    HerdRules.LeadOut(herd, at, map);
                    Say(herders.FirstOrDefault(), m, "paid");
                    break;
                case 1:
                    // Bought: the rest stay the colony's (the silver carried out to them).
                    m.variant = "bought";
                    s.group.RemoveAll(a => herd.Contains(a));
                    m.tale = HerdRules.Words(s, herd.Count);
                    Say(herders.FirstOrDefault(), m, "bought");
                    herd.Clear();
                    break;
                default:
                    m.variant = "unpaid";
                    Missions.Goodwill(m, -m.goodwill);
                    HerdRules.LeadOut(herd, at, map);
                    Say(herders.FirstOrDefault(), m, "unpaid");
                    break;
            }
            m.stage = 3;
            m.lastLook = Find.TickManager.TicksGame;
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "pay":
                    sheet.Goodwill(s.giver, 4);
                    break;
                case "buy":
                    sheet.Play("CC_Storyline_Impact_BuyHerd".Translate(HerdRules.Words(s, ctx.group?.Count ?? 0).Named("ANIMALS")).Resolve(), "CC_Storyline_Impact_BuyHerdBrief".Translate().Resolve());
                    break;
                case "refuse":
                    sheet.Goodwill(s.giver, -(m?.goodwill ?? 8) - 15);
                    break;
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var things = (s == null || m.Map == null ? new List<Pawn>() : HerdRules.Here(s, m.Map)).Concat(HerdersOf(m)).Cast<Thing>().ToList();
            return things.Count > 0 ? new LookTargets(things) : LookTargets.Invalid;
        }

        public override string Status(Mission m) =>
            m.stage == 0 ? "CC_Storyline_Herd_Coming".Translate(TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() : null;
    }
}
