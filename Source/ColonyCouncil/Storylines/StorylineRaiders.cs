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
    // S9, raiders settling nearby (docs/quests-proposals.md; the rules in Flows/RaidersFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Raiders.xml). Raiders of a people at war with the colony are setting up a camp in the
    // region (the region's raiders' camp's kind of camp, named by their age: a war camp, a brigands' hold, a bandit camp, a
    // raider base), and a people nearby warns the colony. Part 1: build defences by a date (traps or turrets, or lengths of
    // wall, fence or barricade); ready, the colony's watch sees the raiders coming hours ahead. Part 2: the raiders test the
    // colony with a small raid (the game's own raid, within the shared harm budget: HarmKinds.StoryFight). Part 3: where a
    // place for their camp is found within three tiles, it is there (the game's own bandit camp site, as the relic thief's)
    // and the neighbours ask the colony to join a strike on it, their fighters beside the colony's on the camp's map (the
    // storylines' away part, rule 2 of the proposals); otherwise the neighbours go, and the colony sends food and medicine for
    // their fighters, collected by their carts. Endings: the camp gone; or the raiders demand payment each season for a year
    // (a part of its own each season: pay, and the silver is carried out to the edge of the colony's ground; refuse, and they
    // come for it a day or two later).

    /// <summary>Raiders settling nearby beyond its parts: the two peoples, the raids, the camp, the endings.</summary>
    public class RaidersRules : StorylineRules
    {
        public override string Kind => RaidersFlow.Key;

        /// <summary>The raiders: a people at war with the colony that can raid it and hold a camp, nearest the neighbours' age.</summary>
        public static Faction Raiders(Faction like) =>
            Find.FactionManager.AllFactionsListForReading.Where(f => f != like && RaidersCamps.CanOwn(f))
                .RandomElementByWeightWithFallback(f => Mathf.Pow(0.35f, Mathf.Abs(HomeQuests.AgeOf(f) - (like != null ? HomeQuests.AgeOf(like) : (int)Ages.Ours))));

        /// <summary>
        /// A people at peace with a town near the colony that would warn it (fights possible, raiders to warn of, and no raiders'
        /// camp of the region's standing near this home already).
        /// </summary>
        public override Faction Giver(Map map)
        {
            if (map == null || !IncidentWorker_Happening.FightsPossible || RaidersCamps.CampOf(map) != null) return null;
            return Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null && Raiders(f) != null)
                   ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Raiders(f) != null);
        }

        /// <summary>The raiders gone, or no longer at war with the colony: it ends plainly.</summary>
        public static bool OtherGone(Storyline s) => s.other == null || s.other.defeated || !s.other.HostileTo(Faction.OfPlayer);

        public static Site SiteOf(Storyline s) => s == null || s.site < 0 ? null : Find.WorldObjects.Sites.FirstOrDefault(x => x.ID == s.site && !x.Destroyed);

        /// <summary>The camp's site taken from the world map (never while the colony is on it).</summary>
        public static void RemoveSite(Storyline s)
        {
            var site = SiteOf(s);
            s.site = site != null && site.HasMap ? s.site : -1;
            if (site == null || site.HasMap) return;
            try
            {
                site.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the raiders' camp taken away", e);
            }
        }

        /// <summary>
        /// A raid of theirs now (the game's own, at this share of the threat the storyteller would send), within the shared harm
        /// budget. Its people (new on the map), or null when none came.
        /// </summary>
        public static List<Pawn> Raid(Storyline s, Map map, float share, string labelKey, string textKey)
        {
            if (map == null || s?.other == null || !Raids.CanRaid(s.other, map) || !IncidentWorker_Happening.FightsPossible) return null;
            if (!Harm.On || !Harm.SystemOn(HarmKinds.StoryFight.system) || !Harm.Roll(HarmKinds.StoryFight, RaidersFlow.RaidChance)) return null;
            var before = new HashSet<Lord>(map.lordManager.lords);
            float points = StorytellerUtility.DefaultThreatPointsNow(map) * share;
            string label = labelKey.Translate(s.other.Name.Named("OTHERPEOPLE"), (s.tale ?? "").Named("KIND")).Resolve();
            string text = textKey.Translate(s.other.Name.Named("OTHERPEOPLE"), (s.tale ?? "").Named("KIND"), (s.giver?.Name ?? "?").Named("FACTION")).Resolve();
            if (!Raids.Send(map, s.other, points, label, text)) return null;
            return map.lordManager.lords.Where(l => !before.Contains(l) && l.faction == s.other).SelectMany(l => l.ownedPawns).Where(p => p != null).Distinct().ToList();
        }

        /// <summary>
        /// Every hour: the raiders at peace or gone ends it plainly; a price refused brings them for it a day or two later
        /// (each refused season once).
        /// </summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            if (OtherGone(s))
            {
                s.Set(s.other == null || s.other.defeated ? "otherdestroyed" : "otherpeace");
                if (s.missionId >= 0)
                {
                    var m = Missions.ById(s.missionId);
                    if (m != null && m.Open) Missions.Finish(m, m.state == MissionState.Active ? MissionState.Failed : MissionState.Declined, "cancelled", null);
                    return;
                }
                if (s.Running) Storylines.End(s, StorylineMath.Gone, map);
                return;
            }
            int refused = s.steps.Count(st => st.part == "tribute" && st.outcome == StorylineMath.Declined);
            if (refused <= s.goalB) return;
            if (s.baseA <= 0)
            {
                s.baseA = now + Mathf.RoundToInt(Rand.Range(RaidersFlow.RefusedRaidMin, RaidersFlow.RefusedRaidMax) * GenDate.TicksPerDay);
                return;
            }
            if (now < s.baseA || MapFacts.ThreatOn(map)) return;
            s.goalB++;
            s.baseA = 0;
            var came = Raid(s, map, RaidersFlow.RaidShare, "CC_Story3_Raiders_RefusedLabel", "CC_Story3_Raiders_RefusedText");
            if (came == null || came.Count == 0)
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Story3_Raiders_RefusedQuiet".Translate(s.other.Name.Named("OTHERPEOPLE")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            RemoveSite(s);
            switch (key)
            {
                case "campgone":
                    StoryChains.Goodwill(s.giver, RaidersFlow.ClearedGoodwill + (s.Has("joined") ? RaidersFlow.JoinedGoodwill : 0), GoodwillWhy.Of(GoodwillReasonMath.Helped));
                    FactionMemory.Note(s.giver, "brokeCamp", Storylines.Label(s));
                    return true;
                case "theycleared":
                    FactionMemory.Note(s.other, "campBroken", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        public override void Gone(Storyline s, Map map) => RemoveSite(s);

        /// <summary>Traps and turrets of the colony's on the map.</summary>
        public static int Guards(Map map) => map.listerBuildings.allBuildingsColonist.Count(b => b is Building_Trap || b is Building_Turret);

        /// <summary>Lengths of wall, fence, sandbags or barricade of the colony's on the map.</summary>
        public static int Walls(Map map) => map.listerBuildings.allBuildingsColonist.Count(b => b.def.IsWall || (b.def.building?.isFence ?? false) || Cover(b.def));

        private static bool Cover(ThingDef d) =>
            d.designationCategory?.defName == "Security" && d.passability == Traversability.PassThroughOnly && d.fillPercent >= 0.5f && !d.IsDoor && d.thingClass != null
            && !typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !typeof(Building_Trap).IsAssignableFrom(d.thingClass);

        public override void DebugEnded(Storyline s, string part, string outcome)
        {
            if (part == "defences" && outcome == "ready") s.Set("watch");
        }
    }

    // ===================== Part 1: defences =====================

    /// <summary>
    /// The neighbours warn that raiders of {OTHERPEOPLE} are settling nearby, and ask the colony to build defences within five to
    /// seven days: traps or turrets, or lengths of wall, fence or barricade (counted from the day it agreed). Ready: either
    /// goal met, and the colony's watch will see them coming. Partly: half of either. Unready: less.
    /// </summary>
    public class HappeningWorker_RaidersDefences : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.Rules(RaidersFlow.Key).Giver(map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(RaidersFlow.Key).Giver(map);
            var r = RaidersRules.Raiders(f);
            if (s == null || f == null || r == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Construction) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            int colonists = IncidentWorker_Happening.Colonists(map).Count;
            s.giver = f;
            s.other = r;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.tale = RaidersCamps.KindWord(r);
            s.goalA = RaidersFlow.GuardsGoal(colonists);
            s.goalB = RaidersFlow.WallGoal(colonists);
            m.faction = f;
            m.place = s.place;
            m.days = Rand.Range(RaidersFlow.DefenceMinDays, RaidersFlow.DefenceMaxDays);
            m.goodwill = 6;
            Fill(m, ctx);
            ctx.enemy = r;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            s.baseA = RaidersRules.Guards(map);
            s.baseB = RaidersRules.Walls(map);
            m.stage = 0;
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        private static string Readiness(Storyline s, Map map) =>
            RaidersFlow.Readiness(Mathf.Max(0, RaidersRules.Guards(map) - s.baseA), s.goalA, Mathf.Max(0, RaidersRules.Walls(map) - s.baseB), s.goalB);

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
            string how = Readiness(s, map);
            if (how == "ready") s.Set("watch");
            Missions.Goodwill(m, how == "ready" ? m.goodwill : how == "partly" ? m.goodwill / 2 : 0);
            End(m, how == "unready" ? MissionState.Failed : MissionState.Done, how);
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            return "CC_Story3_Raiders_Defences".Translate(Mathf.Max(0, RaidersRules.Guards(map) - s.baseA).Named("GUARDS"), s.goalA.Named("TRAPS"),
                Mathf.Max(0, RaidersRules.Walls(map) - s.baseB).Named("BUILT"), s.goalB.Named("WALLS")).Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            m.deadline = Find.TickManager.TicksGame;
            Check(m, map, m.deadline);
            return "the date is now: judged as the defences stand.";
        }
    }

    // ===================== Part 2: the raid that tests the colony =====================

    /// <summary>
    /// The raiders will test the colony with a small raid within a few days (half the threat the storyteller would send now,
    /// the game's own raid, within the shared harm budget). Ready, the colony's watch sees them coming a few hours ahead.
    /// Beaten off: repelled; they had the better of it: overrun; they never came: quiet.
    /// </summary>
    public class HappeningWorker_RaidersRaid : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        protected override int Rating => 3;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && !RaidersRules.OtherGone(Storylines.current);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(ctx.map, SkillDefOf.Shooting) ?? VarietyUtil.Best(ctx.map, SkillDefOf.Social);
            if (m.a == null) return false;
            m.faction = s.giver;
            m.place = s.place;
            m.days = RaidersFlow.RaidMaxDays + RaidersFlow.RaidOverDays + 1f;
            m.goodwill = 8;
            Fill(m, ctx);
            ctx.enemy = s.other;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(RaidersFlow.RaidMinDays, RaidersFlow.RaidMaxDays) * GenDate.TicksPerDay);
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
                // Ready: the watch sees them coming, a few hours ahead.
                if (s.Has("watch") && !m.reminded && now >= m.due - RaidersFlow.WarnHours * GenDate.TicksPerHour)
                {
                    m.reminded = true;
                    StorylineKit.NoteLetter(P, s, m, map, "watch", LookTargets.Invalid, LetterDefOf.ThreatSmall);
                }
                if (now < m.due) return;
                if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay) return;
                var raiders = RaidersRules.Raid(s, map, RaidersFlow.RaidShare, "CC_Story3_Raiders_RaidLabel", "CC_Story3_Raiders_RaidText");
                if (raiders == null || raiders.Count == 0)
                {
                    End(m, MissionState.Done, "quiet");
                    return;
                }
                m.group.Clear();
                m.group.AddRange(raiders);
                m.target = raiders.Count;
                m.misses = 0;
                m.stage = 1;
                m.lastLook = now;
                return;
            }
            // The raid: over once none of them is up on the land, or judged as it stands after a while.
            // Up: on the land and able to fight, or still on their way in (drop pods) for the first hours.
            bool early = now - m.lastLook < GenDate.TicksPerHour * 3;
            var up = m.group.Where(p => p != null && !p.Dead && !p.Downed && !p.IsPrisoner && p.HostileTo(Faction.OfPlayer)
                                        && (p.Spawned && p.Map == map || early && !p.Spawned && !p.Discarded)).ToList();
            int oursDown = map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed) + IncidentWorker_Happening.Colonists(map).Count(p => p.Dead);
            m.misses = Mathf.Max(m.misses, map.mapPawns.FreeColonistsSpawned.Count(p => p.Downed));
            if (up.Count > 0 && now - m.lastLook < RaidersFlow.RaidOverDays * GenDate.TicksPerDay) return;
            int down = m.group.Count(p => p == null || p.Dead || p.Downed || p.IsPrisonerOfColony);
            string how = RaidersFlow.RaidOutcome(m.target, down, Mathf.Max(m.misses, oursDown));
            if (how == "repelled") Missions.Goodwill(m, m.goodwill);
            m.group.Clear();
            End(m, how == "overrun" ? MissionState.Failed : MissionState.Done, how);
        }

        /// <summary>The raiders are the colony's enemies: nothing of theirs is held by the quest.</summary>
        public override void Release(Mission m) => m.group.Clear();

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            return m.stage == 0
                ? "CC_Story3_Raiders_Coming".Translate((s.other?.Name ?? "?").Named("OTHERPEOPLE")).Resolve()
                : "CC_Story3_Raiders_Fight".Translate().Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            if (m.stage != 0) return "the raid is already here.";
            m.due = Find.TickManager.TicksGame;
            return "the raid is due now (it comes within a few seconds, if the harm budget and the battles setting allow).";
        }
    }

    // ===================== Part 3: the strike on the camp =====================

    /// <summary>
    /// The neighbours mean to strike the raiders' camp. Within three tiles (a place found for it): the camp is on the world map
    /// for eight days, and the colony is asked to join the strike, its caravan going there; the neighbours' fighters (two to
    /// four) join the colony's on the camp's map. The camp's fighters all fallen: cleared. Not joined in time: the neighbours
    /// go alone, and it may fail. Farther off: the neighbours go, and ask for food and medicine for their fighters, collected
    /// by their carts within three days; word of the strike comes a few days later, more often good with the colony's help.
    /// </summary>
    public class HappeningWorker_RaidersStrike : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        protected override int Rating => 3;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && !RaidersRules.OtherGone(Storylines.current);

        private static bool CampTile(Map map, out PlanetTile tile) =>
            TileFinder.TryFindNewSiteTile(out tile, map.Tile, 2, RaidersFlow.NearTiles, false, null, 0f);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Shooting) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            bool near = s.other != null && SitePartDefOf.BanditCamp != null && RaidersCamps.CanOwn(s.other) && CampTile(map, out _);
            s.flags.Remove("near");
            if (near) s.Set("near");
            int colonists = IncidentWorker_Happening.Colonists(map).Count;
            m.faction = s.giver;
            m.place = s.place;
            m.variant = near ? "near" : "far";
            m.target = RaidersFlow.Allies(colonists);
            if (near) m.days = RaidersFlow.StrikeDays;
            else
            {
                m.days = RaidersFlow.SupplyDays + RaidersFlow.StrikeWordMax + 1f;
                m.goals.Add(new MissionGoal { what = "meals", count = RaidersFlow.SupplyMeals(m.target + 3) });
                m.goals.Add(new MissionGoal { what = "medicine", count = RaidersFlow.SupplyMedicine(m.target + 3) });
            }
            m.goodwill = 8;
            Fill(m, ctx);
            ctx.enemy = s.other;
            ctx.count = m.target;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null) return false;
            m.stage = 0;
            if (m.variant == "near")
            {
                Site site = null;
                try
                {
                    if (CampTile(map, out var tile))
                    {
                        site = SiteMaker.MakeSite(SitePartDefOf.BanditCamp, tile, s.other, true, StorytellerUtility.DefaultThreatPointsNow(map) * RaidersFlow.CampShare);
                        if (site != null)
                        {
                            site.customLabel = "CC_Story3_Raiders_Camp".Translate((s.other?.Name ?? "?").Named("OTHERPEOPLE"), (s.tale ?? "").Named("KIND")).Resolve().CapitalizeFirst();
                            site.GetComponent<TimeoutComp>()?.StartTimeout(Mathf.RoundToInt(RaidersFlow.StrikeDays * GenDate.TicksPerDay));
                            Find.WorldObjects.Add(site);
                        }
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyline: the raiders' camp", e);
                    site = null;
                }
                if (site != null)
                {
                    s.site = site.ID;
                    StorylineKit.Accepted(P, s, m, map, new LookTargets(site));
                    return true;
                }
                // No camp to be had after all: the neighbours go themselves, with the colony's food and medicine.
                m.variant = "far";
                s.flags.Remove("near");
                m.goals.Add(new MissionGoal { what = "meals", count = RaidersFlow.SupplyMeals(m.target + 3) });
                m.goals.Add(new MissionGoal { what = "medicine", count = RaidersFlow.SupplyMedicine(m.target + 3) });
                m.deadline = Find.TickManager.TicksGame + Mathf.RoundToInt((RaidersFlow.SupplyDays + RaidersFlow.StrikeWordMax + 1f) * GenDate.TicksPerDay);
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
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
            if (m.variant == "near") Near(m, s, map, now);
            else Far(m, s, map, now);
        }

        /// <summary>The camp within reach: the colony's people on its map with the neighbours' fighters, until the camp's own have all fallen.</summary>
        private void Near(Mission m, Storyline s, Map map, int now)
        {
            var site = RaidersRules.SiteOf(s);
            var campMap = site?.Map;
            if (campMap != null)
            {
                var ours = campMap.mapPawns.FreeColonistsSpawned.ToList();
                if (ours.Count > 0)
                {
                    s.Set("joined");
                    if (m.stage == 0)
                    {
                        m.stage = 1;
                        Allies(m, s, campMap, ours[0].Position);
                    }
                }
                bool standing = campMap.mapPawns.AllPawnsSpawned.Any(p => p.Faction == s.other && !p.Dead && !p.Downed && !p.IsPrisoner && p.RaceProps.Humanlike);
                if (!standing && m.stage >= 1)
                {
                    SendHome(m, campMap);
                    Missions.Goodwill(m, m.goodwill);
                    End(m, MissionState.Done, "cleared", new LookTargets(site));
                    return;
                }
                if (now < m.deadline + GenDate.TicksPerDay * 2) return;
            }
            else if (site != null && now < m.deadline) return;
            // The time is up (or the camp left behind): the neighbours go in alone, or the colony's part came to nothing.
            if (s.Has("joined") && m.stage >= 1)
            {
                End(m, MissionState.Failed, "failed", site != null ? new LookTargets(site) : null);
                return;
            }
            bool won = Rand.Chance(RaidersFlow.StrikeChance(false));
            if (!won) s.Set("alone");
            else s.Set("theywent");
            RaidersRules.RemoveSite(s);
            End(m, won ? MissionState.Done : MissionState.Failed, won ? "cleared" : "failed");
        }

        /// <summary>The neighbours' fighters come onto the camp's map beside the colony's people and go after the raiders (the game's assist duty).</summary>
        private void Allies(Mission m, Storyline s, Map campMap, IntVec3 near)
        {
            if (s.giver == null || s.giver.HostileTo(Faction.OfPlayer)) return;
            try
            {
                float power = StorytellerUtility.DefaultThreatPointsNow(campMap) / Mathf.Max(1, m.target);
                var allies = new List<Pawn>();
                for (int i = 0; i < m.target; i++)
                {
                    var p = HomeQuests.Person(s.giver, new FloatRange(20f, 50f), HomeQuests.FighterKind(s.giver, Mathf.Max(60f, power)));
                    if (p != null && Arrivals.Spawn(p, campMap, near)) allies.Add(p);
                    else if (p != null) Arrivals.Discard(p);
                }
                if (allies.Count == 0) return;
                var middle = campMap.mapPawns.AllPawnsSpawned.Where(p => p.Faction == s.other).Select(p => p.Position).FirstOrDefault();
                LordMaker.MakeNewLord(s.giver, new LordJob_AssistColony(s.giver, middle.IsValid ? middle : campMap.Center), campMap, allies);
                m.group.AddRange(allies);
                s.count = allies.Count;
                StorylineKit.NoteLetter(P, s, m, campMap, "allies", new LookTargets(allies), LetterDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the neighbours' fighters at the camp", e);
            }
        }

        /// <summary>The neighbours' fighters who still stand go home.</summary>
        private static void SendHome(Mission m, Map campMap)
        {
            var here = m.group.Where(p => p != null && p.Spawned && p.Map == campMap && !p.Dead && !p.Downed).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
        }

        /// <summary>The camp farther off: food and medicine for the neighbours' fighters, then word of their strike.</summary>
        private void Far(Mission m, Storyline s, Map map, int now)
        {
            if (m.stage == 0)
            {
                foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
                bool all = m.goals.All(g => g.have >= g.count);
                if (all || now - m.started > RaidersFlow.SupplyDays * GenDate.TicksPerDay)
                {
                    if (all)
                    {
                        foreach (var g in m.goals) StorylineRound3.Collect(map, s.giver, g.what, g.count, 0);
                        s.Set("supplied");
                    }
                    m.stage = 1;
                    m.due = now + Mathf.RoundToInt(Rand.Range(RaidersFlow.StrikeWordMin, RaidersFlow.StrikeWordMax) * GenDate.TicksPerDay);
                    m.deadline = Mathf.Max(m.deadline, m.due + GenDate.TicksPerDay);
                    Note(m, map, all ? "setout" : "setoutshort", LookTargets.Invalid);
                }
                return;
            }
            if (now < m.due) return;
            bool won = Rand.Chance(RaidersFlow.StrikeChance(s.Has("supplied")));
            if (won && s.Has("supplied")) Missions.Goodwill(m, m.goodwill);
            End(m, won ? MissionState.Done : MissionState.Failed, won ? "cleared" : "failed");
        }

        /// <summary>The neighbours' fighters are theirs: no longer kept in the world for the part (those still on the camp's map go home).</summary>
        public override void Release(Mission m)
        {
            foreach (var p in m.group.Where(p => p != null && !p.Dead).ToList())
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
        }

        public override LookTargets Targets(Mission m)
        {
            var site = RaidersRules.SiteOf(Story(m));
            return site != null ? new LookTargets(site) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            if (m.variant == "near") return "CC_Story3_Raiders_StrikeNear".Translate((s.tale ?? "").Named("KIND"), m.target.Named("COUNT")).Resolve();
            return m.stage == 0 ? null : "CC_Story3_Raiders_StrikeFar".Translate((s.giver?.Name ?? "?").Named("FACTION")).Resolve();
        }

        public string DebugNow(Mission m, Map map)
        {
            if (m.variant == "near") return "form a caravan and send it to the camp on the world map.";
            int now = Find.TickManager.TicksGame;
            if (m.stage == 0) m.started = now - Mathf.RoundToInt(RaidersFlow.SupplyDays * GenDate.TicksPerDay) - 1;
            else m.due = now;
            return m.stage == 0 ? "the supplies are judged now." : "word of the strike comes now.";
        }
    }

    // ===================== The raiders' price, each season =====================

    /// <summary>
    /// The raiders demand payment, each season for a year (their camp still stands, or they had the better of the colony).
    /// Taken on: the silver is carried out to the edge of the colony's ground and left there for them (the region's
    /// raiders' camp's own errand). Turned down, or let go: refused, and they come for it a day or two later (RaidersRules.Hourly).
    /// Without the silver when it agrees, it counts as a refusal. The colony's own: the raiders are no people it is at peace with.
    /// </summary>
    public class HappeningWorker_RaidersTribute : HappeningWorker_StorylinePart
    {
        protected override int Rating => 2;

        public override bool OfTheColony => true;

        public override bool OwnMemory => true;

        protected override bool Possible(Map map) => Storylines.current?.other != null && !RaidersRules.OtherGone(Storylines.current);

        protected override string Silver(Mission m, Storyline s) => s?.amount.ToString();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.other == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            s.amount = RaidersFlow.Price(StorytellerUtility.DefaultThreatPointsNow(map) * RaidersFlow.RaidShare);
            m.reward = s.amount;
            m.days = 1f;
            Fill(m, ctx);
            ctx.enemy = s.other;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || PlayCarry.Have(map, "silver", ThingDefOf.Silver) < s.amount) return false;
            var ctx = new DilemmaContext { map = map, faction = s.other };
            string label = "CC_Story3_Raiders_PayLabel".Translate((s.tale ?? "").Named("KIND")).Resolve();
            // apart: one of the colony carries the silver out to the edge of its ground and leaves it there for them to collect.
            var p = new DilemmaEffect_RaidersCampPrice { what = "silver", count = s.amount, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = label }.Run(ctx);
            if (p == null) PlayCarry.Take(map, "silver", ThingDefOf.Silver, s.amount);
            m.stage = 1;
            m.due = Find.TickManager.TicksGame;
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
            End(m, MissionState.Done, "paid", null, null, s.amount.ToString());
        }
    }
}
