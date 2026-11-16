using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // S4, the beast coming down the valley (docs/quests-proposals.md; words in Defs/MissionDefs/Rimfolk_Storyline_Beast.xml).
    // A hunting people (the Lantern Wardens first, else a people of the tribal or medieval age, else any at peace) warns the
    // colony that a great beast, one of the region's legends (Beasts/RegionLegend.cs: the biggest animals loaded that could
    // live here, from every mod), is coming its way. Part 1: get ready by a date, traps or a length of wall or fence, and
    // the animals in. Part 2: the beast's kill and tracks are found at the edge of the land; someone of the colony goes to
    // look. Part 3: the beast comes onto the map, a real fight, scaled to the colony's strength, with the hunters beside
    // the colony when it stands well with them. Killed: the colony keeps the trophy and is honoured. Driven off or gone on
    // its own: it comes back next season, at most twice. Every part can be turned down: then the hunters turn it aside.

    /// <summary>The beast's storyline beyond its parts: who warns, which beast, the beast itself, its endings.</summary>
    public class BeastRules : StorylineRules
    {
        public override string Kind => StorylineMath.Beast;

        private static readonly string[] Compass = { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };

        /// <summary>The Lantern Wardens (and any people of the hunters' calling); else a people of the tribal or medieval age; else any people at peace.</summary>
        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Hunters)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.TechOf(f) <= TechLevel.Medieval)
            ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        /// <summary>The region's legend beasts that could come here (never a Pokémon: PokéWorld's legendaries keep to their own story).</summary>
        public static List<PawnKindDef> Kinds(Map map) => LegendWorker.Kinds(map).Where(k => !PokeWorldLink.IsPokemon(k) && k.RaceProps?.Animal == true).ToList();

        /// <summary>The beast, scaled to the colony: the strongest of the legends whose strength fits the threat the storyteller would send now.</summary>
        public static PawnKindDef Pick(Map map)
        {
            var kinds = Kinds(map);
            if (kinds.Count == 0) return null;
            int i = StorylineMath.BeastPick(kinds.Select(k => k.combatPower).ToList(), StorytellerUtility.DefaultThreatPointsNow(map));
            return i >= 0 ? kinds[i] : null;
        }

        /// <summary>"to the north": which way from the colony a spot lies.</summary>
        public static string Direction(Map map, IntVec3 at)
        {
            var c = CampSite.ColonyCenter(map);
            if (!c.IsValid) c = map.Center;
            var d = at - c;
            if (d.x == 0 && d.z == 0) return "CC_St_Dir_Near".Translate();
            float fromNorth = 90f - Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
            int i = Mathf.RoundToInt(((fromNorth % 360f + 360f) % 360f) / 45f) % 8;
            return ("CC_St_Dir_" + Compass[i]).Translate();
        }

        /// <summary>The beast itself: old, scarred and legendary, of the kind picked when the warning came, with the name the colony knows it by.</summary>
        public static Pawn Make(Storyline s, Map map)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(s.what ?? "");
            if (kind?.RaceProps == null) return null;
            float age = Mathf.Max(1f, kind.RaceProps.lifeExpectancy * Rand.Range(BeastsMath.AgeMin, BeastsMath.AgeMax));
            var x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true,
                fixedBiologicalAge: age, fixedChronologicalAge: age));
            if (x == null) return null;
            if (!s.name.NullOrEmpty()) x.Name = new NameSingle(s.name);
            if (CouncilDefOf.Council_Legendary != null) x.health.AddHediff(CouncilDefOf.Council_Legendary);
            return x;
        }

        /// <summary>Traps of the colony's on the map.</summary>
        public static int Traps(Map map) => map.listerBuildings.allBuildingsColonist.Count(b => b is Building_Trap);

        /// <summary>Lengths of wall and fence of the colony's on the map.</summary>
        public static int Walls(Map map) => map.listerBuildings.allBuildingsColonist.Count(b => b.def.IsWall || (b.def.building?.isFence ?? false));

        /// <summary>The colony's animals on the map, and those kept in: in a pen, or in a room under a roof.</summary>
        public static (int all, int kept) Animals(Map map)
        {
            var list = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead).ToList();
            int kept = list.Count(a => AnimalPenUtility.GetCurrentPenOf(a, false) != null || a.GetRoom() is Room r && !r.PsychologicallyOutdoors);
            return (list.Count, kept);
        }

        /// <summary>Its kill and its tracks at the edge of the land: one of the land's wild animals, bitten to death, blood, and a line of tracks from the edge.</summary>
        public static bool Signs(Storyline s, Map map)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var edge, map, 0f, false, c => !c.Fogged(map))) return false;
            var at = CellFinder.RandomClosewalkCellNear(edge, map, 8, c => !c.Fogged(map) && c.Standable(map));
            if (!at.IsValid) at = edge;
            try
            {
                var prey = BiomeAnimals.Of(map.Biome).Where(k => k?.RaceProps != null && !k.RaceProps.predator && k.RaceProps.baseBodySize >= 0.4f && k.RaceProps.baseBodySize <= 2.5f
                                                             && !PokeWorldLink.IsPokemon(k)).RandomElementWithFallback();
                if (prey != null)
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(prey, null, PawnGenerationContext.NonPlayer, map.Tile));
                    GenSpawn.Spawn(p, at, map);
                    for (int i = 0; i < 15 && !p.Dead; i++) p.TakeDamage(new DamageInfo(DamageDefOf.Bite, Rand.Range(15f, 30f)));
                    if (!p.Dead) p.Kill(null);
                    s.thing = p.Corpse;
                }
                FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Blood, 4);
                foreach (var c in GenSight.PointsOnLineOfSight(edge, at))
                    if (c.InBounds(map) && c.Standable(map))
                        FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_Dirt, 1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the beast's kill", e);
            }
            s.spot = at;
            s.way = Direction(map, at);
            return true;
        }

        /// <summary>A trophy from it beside the body: its horns or tusks (what its body gives), else a part of its hide.</summary>
        public static void Trophy(Pawn x)
        {
            var corpse = x?.Corpse;
            if (corpse == null || !corpse.Spawned) return;
            try
            {
                var things = new List<Thing>();
                var products = x.def.butcherProducts;
                if (!products.NullOrEmpty())
                    foreach (var p in products.Where(p => p?.thingDef != null))
                    {
                        var t = ThingMaker.MakeThing(p.thingDef);
                        t.stackCount = Mathf.Max(1, p.count);
                        things.Add(t);
                    }
                else if (x.def.race?.leatherDef != null)
                {
                    var t = ThingMaker.MakeThing(x.def.race.leatherDef);
                    t.stackCount = Mathf.Clamp(Mathf.RoundToInt(x.GetStatValue(StatDefOf.LeatherAmount) * 0.25f), 10, 60);
                    things.Add(t);
                }
                foreach (var t in things) GenPlace.TryPlaceThing(t, corpse.Position, corpse.Map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the beast's trophy", e);
            }
        }

        /// <summary>The beast, if it is on a map, goes off: its rage ends and it walks away.</summary>
        public static void SendOff(Pawn beast)
        {
            if (beast == null || beast.Dead || !beast.Spawned || beast.Downed || beast.Faction == Faction.OfPlayer) return;
            beast.mindState.mentalStateHandler.Reset();
            beast.mindState.exitMapAfterTick = Find.TickManager.TicksGame;
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            if (key == "away" || key == StorylineMath.Faded) SendOff(s.y);
            return true;
        }

        /// <summary>Its people are at war with the colony: the warning comes to nothing, a beast on the map goes off on its own, and their hunters leave.</summary>
        public override void Gone(Storyline s, Map map)
        {
            SendOff(s.y);
            Arrivals.SendAway(s.group.Where(p => p != null && p.Spawned).ToList());
        }
    }

    // ===================== Part 1: get ready for the beast =====================

    /// <summary>
    /// The hunters' warning: the beast is coming, and they ask the colony to be ready by a date: traps (three to six by its
    /// size) or a length of wall or fence (24 to 60), built from now, and three in four of its animals in a pen or a room.
    /// Both: ready; one: partly; neither: unready. Either way, the signs come next.
    /// </summary>
    public class HappeningWorker_BeastReady : HappeningWorker_StorylinePart
    {
        protected override int Rating => 3;

        protected override bool Possible(Map map) =>
            IncidentWorker_Happening.FightsPossible && LegendWorker.Running() == null && Storylines.Rules(StorylineMath.Beast).Giver(map) != null && BeastRules.Kinds(map).Count > 0;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(StorylineMath.Beast).Giver(map);
            var kind = BeastRules.Pick(map);
            if (s == null || f == null || kind == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Shooting) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var x = Arrivals.Make(PeoplesD.FighterKind(f), f, new FloatRange(25f, 55f));
            if (x == null) return false;
            Arrivals.Hold(x);
            string name = BeastBook.NameFrom(LegendWorker.NamePool, BeastBook.Voice(map), kind.RaceProps.predator ? "predator" : "grazer");
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.what = kind.defName;
            s.tale = kind.label;
            s.name = name.NullOrEmpty() ? kind.LabelCap.ToString() : name;
            int colonists = IncidentWorker_Happening.Colonists(map).Count;
            s.goalA = StorylineMath.TrapsGoal(colonists);
            s.goalB = StorylineMath.WallGoal(colonists);
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.goodwill = 10;
            m.days = Rand.Range(StorylineMath.ReadyMinDays, StorylineMath.ReadyMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.beast = s.name;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            s.baseA = BeastRules.Traps(map);
            s.baseB = BeastRules.Walls(map);
            StorylineKit.Accepted(P, s, m, map, null);
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
            if (now < m.deadline) return;
            int traps = Mathf.Max(0, BeastRules.Traps(map) - s.baseA), walls = Mathf.Max(0, BeastRules.Walls(map) - s.baseB);
            var (all, kept) = BeastRules.Animals(map);
            string how = StorylineMath.Readiness(traps, s.goalA, walls, s.goalB, kept, all);
            if (how == "ready") Missions.Goodwill(m, m.goodwill);
            else if (how == "partly") Missions.Goodwill(m, m.goodwill / 2);
            End(m, how == "unready" ? MissionState.Failed : MissionState.Done, how);
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            int traps = Mathf.Max(0, BeastRules.Traps(map) - s.baseA), walls = Mathf.Max(0, BeastRules.Walls(map) - s.baseB);
            var (all, kept) = BeastRules.Animals(map);
            return "CC_Storyline_Beast_Ready".Translate(traps.Named("TRAPS"), s.goalA.Named("TRAPGOAL"), walls.Named("WALLS"), s.goalB.Named("WALLGOAL"), kept.Named("KEPT"),
                StorylineMath.KeptInNeeded(all).Named("NEEDED"), all.Named("ANIMALS")).Resolve();
        }
    }

    // ===================== Part 2: the beast's tracks =====================

    /// <summary>
    /// Its kill and tracks are found at the edge of the land, and the hunters ask the colony to send someone to look within
    /// two days. Looked at: found (it comes from that side, and the colony knows when); else missed.
    /// </summary>
    public class HappeningWorker_BeastSigns : HappeningWorker_StorylinePart
    {
        protected override int Rating => 1;

        protected override bool Possible(Map map) => Storylines.current?.what != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Animals) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.faction = s.giver;
            m.place = s.place;
            m.days = StorylineMath.SignsDays;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.beast = s.name;
            return true;
        }

        /// <summary>The kill and the tracks are there as soon as the hunters speak of them, whatever the colony answers.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            if (s == null || !BeastRules.Signs(s, ctx.map)) return false;
            ctx.spot = s.spot;
            return base.Begin(ctx, ext);
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || !s.spot.IsValid) return false;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(new TargetInfo(s.spot, map)));
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
            var seer = s.spot.IsValid
                ? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Downed && !p.Dead && p.Position.InHorDistOf(s.spot, StorylineMath.SeenWithin))
                : null;
            if (seer != null)
            {
                m.a = seer;
                s.yName = seer.LabelShort;
                End(m, MissionState.Done, "found", new LookTargets(seer));
                return;
            }
            if (now >= m.deadline) End(m, MissionState.Failed, "missed");
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            return s != null && map != null && s.spot.IsValid ? new LookTargets(new TargetInfo(s.spot, map)) : LookTargets.Invalid;
        }

        public override string Status(Mission m) => "CC_Storyline_Beast_Signs".Translate((Story(m)?.way ?? "?").Named("WAY")).Resolve();
    }

    // ===================== Part 3: the beast comes =====================

    /// <summary>
    /// The beast comes onto the land within a day, from the side its tracks were found on (else any), enraged; the hunters
    /// come beside the colony when it stands well enough with them. Killed (or tamed): the trophy and their honour. Below a
    /// third of its strength it turns and flees: driven off. Still here a day and a half after it came, it goes off on its
    /// own. Either way it comes back next season, at most twice.
    /// </summary>
    public class HappeningWorker_BeastComes : HappeningWorker_StorylinePart
    {
        protected override int Rating => 4;

        protected override bool Possible(Map map) => Storylines.current?.what != null && IncidentWorker_Happening.FightsPossible;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null || DefDatabase<PawnKindDef>.GetNamedSilentFail(s.what ?? "") == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Shooting) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            s.goalA = StorylineMath.Hunters(s.giver.HasGoodwill ? s.giver.PlayerGoodwill : 0, s.Has("ready"));
            if (s.goalA > 0) s.Set("help");
            else s.flags.Remove("help");
            m.faction = s.giver;
            m.place = s.place;
            m.days = 3f;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.beast = s.name;
            ctx.count = s.goalA;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.8f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
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
            if (m.stage == 0)
            {
                if (now < m.due) return;
                // Not on top of another fight: it waits at the edge of the land a while.
                if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay * 2)
                {
                    m.due = now + GenDate.TicksPerHour * 3;
                    return;
                }
                if (!Arrive(m, s, map)) End(m, MissionState.Done, "left");
                return;
            }
            var beast = s.y;
            if (beast == null)
            {
                End(m, MissionState.Done, "left");
                return;
            }
            if (beast.Dead || beast.Faction == Faction.OfPlayer)
            {
                if (beast.Dead) BeastRules.Trophy(beast);
                else s.Set("tamed");
                StoryChains.Goodwill(s.giver, 15 + (s.Has("help") ? 5 : 0), GoodwillWhy.Task(15));
                Arrivals.SendAway(m.group);
                End(m, MissionState.Done, "killed", beast.Corpse != null && beast.Corpse.Spawned ? new LookTargets(beast.Corpse) : beast.Spawned ? new LookTargets(beast) : null);
                return;
            }
            if (!beast.Spawned || beast.Discarded)
            {
                // Off the land: driven off if it fled, else gone on its own. The last time, it doesn't come back.
                if (s.Times("beast") > StorylineMath.MaxReturns) s.Set("last");
                Arrivals.SendAway(m.group);
                End(m, MissionState.Done, s.Has("fled") ? "driven" : "left");
                return;
            }
            if (beast.Downed)
            {
                // Down: at the end of its time here it dies of its wounds, if nobody has finished it.
                if (now >= m.due)
                {
                    Note(m, map, "died", new LookTargets(beast), MessageTypeDefOf.PositiveEvent);
                    beast.Kill(null);
                }
                return;
            }
            if (!s.Has("fled") && beast.health.summaryHealth.SummaryHealthPercent < StorylineMath.FleeBelow)
            {
                // Hurt badly enough, it turns and runs.
                s.Set("fled");
                beast.mindState.mentalStateHandler.Reset();
                beast.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, null, true);
                Note(m, map, "fled", new LookTargets(beast));
                return;
            }
            if (now >= m.due && !s.Has("leaving") && !s.Has("fled"))
            {
                // A day and a half here and not brought down: it goes off on its own.
                s.Set("leaving");
                BeastRules.SendOff(beast);
                Note(m, map, "leaving", new LookTargets(beast));
            }
        }

        /// <summary>It comes onto the land, enraged, from the side of its tracks; the hunters with it when they help.</summary>
        private bool Arrive(Mission m, Storyline s, Map map)
        {
            var beast = s.y != null && !s.y.Dead && !s.y.Discarded && !s.y.Spawned ? s.y : BeastRules.Make(s, map);
            if (beast == null) return false;
            IntVec3 cell = IntVec3.Invalid;
            bool near = s.Has("found") && s.spot.IsValid && s.spot.InBounds(map)
                        && CellFinder.TryFindRandomEdgeCellNearWith(s.spot, 20f, map, c => c.Standable(map) && !c.Fogged(map), out cell);
            if (!near && !RCellFinder.TryFindRandomPawnEntryCell(out cell, map, 0f)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(beast);
            if (Find.WorldPawns.Contains(beast)) Find.WorldPawns.RemovePawn(beast);
            GenSpawn.Spawn(beast, cell, map);
            beast.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, null, true);
            s.y = beast;
            s.yName = beast.LabelShort;
            s.flags.Remove("fled");
            s.flags.Remove("leaving");
            if (!near) s.way = BeastRules.Direction(map, cell);
            m.x = beast;
            m.stage = 1;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(StorylineMath.LeaveAfterDays * GenDate.TicksPerDay);
            // The hunters beside the colony, from the same side.
            var hunters = new List<Pawn>();
            for (int i = 0; i < s.goalA; i++)
            {
                var h = Arrivals.Make(PeoplesD.FighterKind(s.giver), s.giver, new FloatRange(20f, 45f));
                if (h == null) continue;
                GenSpawn.Spawn(h, CellFinder.RandomClosewalkCellNear(cell, map, 6), map);
                hunters.Add(h);
            }
            if (hunters.Count > 0)
            {
                var center = CampSite.ColonyCenter(map);
                LordMaker.MakeNewLord(s.giver, new LordJob_AssistColony(s.giver, center.IsValid ? center : map.Center), map, hunters);
                m.group.Clear();
                m.group.AddRange(hunters);
                s.group.Clear();
                s.group.AddRange(hunters);
            }
            StorylineKit.NoteLetter(P, s, m, map, "came", new LookTargets(beast), LetterDefOf.ThreatBig);
            return true;
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            return s?.y != null && s.y.Spawned ? new LookTargets(s.y) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            return m.stage == 0
                ? "CC_Storyline_Beast_Coming".Translate((s.name ?? "?").Named("NAME"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve()
                : "CC_Storyline_Beast_Here".Translate((s.name ?? "?").Named("NAME")).Resolve();
        }
    }
}
