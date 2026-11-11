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
    /// <summary>The sacred animal while it walks the land: which beast and which god, its stay, who it follows or leads, and what was done to it.</summary>
    public class SacredStay : IExposable
    {
        public Pawn a;
        public int god = -1;
        public Map map;
        public int since, leaveAt;
        /// <summary>The colonist a small one follows about.</summary>
        public Pawn chosen;
        /// <summary>The colonist following a large one where it leads, the place, and the boon there.</summary>
        public Pawn follower;
        public IntVec3 to = IntVec3.Invalid;
        public AnimalBoon boon;
        public int leadUntil = -1;
        public bool led, beckoned, fed, hurt, slain, ended;
        public Pawn feeder, harmer;
        public int lastSeenSaid = -1;

        public God God => Gods.ById(god);

        public void ExposeData()
        {
            Scribe_References.Look(ref a, "a");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref leaveAt, "leaveAt");
            Scribe_References.Look(ref chosen, "chosen");
            Scribe_References.Look(ref follower, "follower");
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Values.Look(ref boon, "boon", AnimalBoon.Find);
            Scribe_Values.Look(ref leadUntil, "leadUntil", -1);
            Scribe_Values.Look(ref led, "led");
            Scribe_Values.Look(ref beckoned, "beckoned");
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref hurt, "hurt");
            Scribe_Values.Look(ref slain, "slain");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_References.Look(ref feeder, "feeder");
            Scribe_References.Look(ref harmer, "harmer");
            Scribe_Values.Look(ref lastSeenSaid, "lastSeenSaid", -1);
        }
    }

    /// <summary>
    /// The sacred animal (brainstorm 8; docs/design/gods.md, "On the map"): a god walks as a beast of its look's kinds (the
    /// player's mods' first: the elves' ancient deer, the great wolf, the thunderox, Odyssey's crow), shown as a god's (larger,
    /// in its colours, its aura and trail: Gods/Presence/). It comes in from the edge after days of omens, stays a few days
    /// and goes (the game's own leaving), never tamed and never hostile unless harmed: the legend beast's shape (wild, up
    /// and seen on the colony's land, LegendBeasts.Here). A small one (a raven, a cat, an otter) follows one colonist about;
    /// a large one walks the land and leads whoever follows it to a find, a cure or a vein of ore. Leaving it be, feeding it
    /// and following it bring favour; hunting or hurting it is sacrilege: the game's own revenge, the god's wrath through
    /// favour and a Wrath moment, and a slain one's body gone to ash.
    /// </summary>
    public static class SacredAnimal
    {
        public const string SeenPool = "Rimfolk_Pool_SacredAnimalSeen";
        public const string BeckonPool = "Rimfolk_Pool_SacredAnimalBeckon";

        private static int Now => Find.TickManager.TicksGame;

        // ================= Which beast, which god =================

        /// <summary>The kinds a god's look walks as that can live on this map now: animals only, not insects or dryads, at ease in the season.</summary>
        public static List<PawnKindDef> KindsFor(God god, Map map)
        {
            var list = new List<PawnKindDef>();
            // Only a god whose shape walks as a beast, as the beasts its shape names, else its look's.
            if (god == null || map == null || !GodShapes.Beast(god)) return list;
            var races = GodShapes.Beasts(god) ?? GodsWiring.LookOf(god)?.AnimalKinds;
            if (races == null) return list;
            foreach (var race in races)
            {
                var rp = race?.race;
                if (rp == null || !rp.Animal || rp.Insect || rp.Dryad || rp.IsMechanoid || rp.IsAnomalyEntity) continue;
                if (map.mapTemperature != null && !map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(race)) continue;
                var kind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == race);
                if (kind != null) list.Add(kind);
            }
            return list;
        }

        /// <summary>Some god can walk as a beast here.</summary>
        public static bool AnyFor(Map map) => map != null && !MapFacts.InVacuum(map) && Gods.All.Any(g => g != null && g.kind != GodKind.Dead && KindsFor(g, map).Count > 0);

        /// <summary>The god who walks as a beast: the wild's and the hunt's most, the colony's own and its patron more (GodsOnMapMath.AnimalWeight).</summary>
        public static God PickGod(Map home)
        {
            var mine = Gods.ColonyGods(home);
            var patron = Gods.Patron(home);
            return Gods.All.Where(g => g != null).RandomElementByWeightWithFallback(g =>
                GodsOnMapMath.AnimalWeight(g.domain, g.kind, KindsFor(g, home).Count, mine.Contains(g), g == patron, g.known));
        }

        /// <summary>The sacred animal on the map, if this is it.</summary>
        public static bool Is(Pawn p) => p != null && GodsOnMapBook.Instance?.beast?.a == p;

        // ================= Its coming =================

        /// <summary>It comes in from the edge now, shown as the god's, and the moment of its coming plays. True once it is on the land.</summary>
        internal static bool Come(GodsOnMapBook b, GodComing c, God god) => Bring(b, c.map, god, null) != null;

        /// <summary>Brings the god's beast to <paramref name="map"/> (a kind of its look's, <paramref name="kind"/> to choose one). The beast, or null.</summary>
        public static Pawn Bring(GodsOnMapBook b, Map map, God god, PawnKindDef kind)
        {
            if (b == null || map == null || god == null || b.beast != null) return null;
            var kinds = KindsFor(god, map);
            // The player's mods' beasts come first in a look's list: the first few most likely.
            kind ??= kinds.Count == 0 ? null : kinds[Mathf.Min(kinds.Count - 1, Rand.Chance(0.6f) ? 0 : Rand.Range(0, kinds.Count))];
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal + 0.2f)) return null;
            Pawn a;
            try
            {
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 2f : kind.RaceProps.lifeStageAges.Last().minAge;
                a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    fixedBiologicalAge: grown + Rand.Range(1f, 3f)));
                if (a == null) return null;
                GenSpawn.Spawn(a, entry, map, Rot4.Random);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sacred animal", e);
                return null;
            }
            if (!GodLookSeam.Make(a, god, false))
            {
                a.Destroy();
                return null;
            }
            int now = Now;
            var s = new SacredStay
            {
                a = a, god = god.id, map = map, since = now,
                leaveAt = now + Mathf.RoundToInt(Rand.Range(GodsOnMapMath.AnimalStayMin, GodsOnMapMath.AnimalStayMax) * GenDate.TicksPerDay)
            };
            b.beast = s;
            try
            {
                // The game's own wandering beast: out to the open land, and off the map when its days are done.
                a.mindState.exitMapAfterTick = s.leaveAt;
                if (RCellFinder.TryFindRandomCellOutsideColonyNearTheCenterOfTheMap(entry, map, 10f, out var spot)) a.mindState.forcedGotoPosition = spot;
                Fed(a);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sacred animal's way", e);
            }
            var spec = PresenceSpec.For(god, map, PresenceKind.Appear, entry, a);
            spec.title = ("CC_GodM_Animal_Title" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[] { a.def.label.Named("BEAST") }).ToArray()).Resolve().CapitalizeFirst();
            spec.text = ("CC_GodM_Animal_Text" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[] { a.def.label.Named("BEAST") }).ToArray()).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(a));
            if (!spec.veiled) Gods.Learn(god, "visitors");
            Gods.Remember(new GodAct
            {
                god = god.id, home = Gods.HomeKey(map), tick = now, kind = GodActKind.Appear, cell = entry,
                text = GodsOnMapWords.Veil("CC_GodM_Animal_Act".Translate(a.def.label.Named("BEAST")).Resolve(), spec.veiled)
            });
            return a;
        }

        /// <summary>A god's beast is never hungry, so it never hunts anything of the colony's.</summary>
        private static void Fed(Pawn a)
        {
            var food = a?.needs?.food;
            if (food != null) food.CurLevel = food.MaxLevel;
        }

        // ================= Its stay =================

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var s = b.beast;
            if (s == null) return;
            var a = s.a;
            var map = s.map;
            if (a == null || a.Discarded || map == null || !Homes.IsHome(map))
            {
                b.beast = null;
                return;
            }
            // Gone its way (off the map, unharmed): the god's favour for a stay let be.
            if (!a.Spawned && !a.Dead)
            {
                Departed(b, s, now);
                return;
            }
            if (a.Dead) return;
            // Hurt: once its revenge is done (or it took none), it leaves the land in a breath.
            if (s.hurt)
            {
                Settled(b);
                return;
            }
            Fed(a);
            if (now >= s.leaveAt + GenDate.TicksPerDay)
            {
                // Lingered past its days (shut in, lost): it goes as it came.
                Vanish(a);
                Departed(b, s, now);
                return;
            }
            if (GodsOnMapMath.Follows(a.BodySize)) Follow(s, a, now);
            else if (!s.led && !s.beckoned && s.follower == null && now - s.since > GenDate.TicksPerDay / 2 && Rand.Chance(GodsOnMapMath.BeckonChanceADay / 24f)) Beckon(s, a);
            Seen(s, a, now);
        }

        /// <summary>A small one follows its chosen colonist about while they are out and about.</summary>
        private static void Follow(SacredStay s, Pawn a, int now)
        {
            if (s.chosen == null || s.chosen.Dead || !s.chosen.Spawned || s.chosen.Map != a.Map)
                s.chosen = Homes.People(s.map).Where(p => p.Spawned && Together.Able(p) && !p.DevelopmentalStage.Baby()).RandomElementWithFallback();
            var c = s.chosen;
            if (c == null || !c.Spawned || c.Map != a.Map || a.CurJobDef == JobDefOf.FollowClose && a.CurJob?.targetA.Thing == c) return;
            if (a.InMentalState || a.Downed || !a.Awake()) return;
            try
            {
                var j = JobMaker.MakeJob(JobDefOf.FollowClose, c);
                j.followRadius = 2.5f;
                j.expiryInterval = GenDate.TicksPerHour;
                j.checkOverrideOnExpire = true;
                a.jobs.StartJob(j, JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sacred animal following", e);
            }
        }

        /// <summary>A colonist who sees it says so now and then (a few times a stay).</summary>
        private static void Seen(SacredStay s, Pawn a, int now)
        {
            if (now - s.lastSeenSaid < GenDate.TicksPerHour * 8 || !Rand.Chance(0.35f)) return;
            var p = a.Map.mapPawns.FreeColonistsSpawned.Where(x => Together.Able(x) && x.Position.InHorDistOf(a.Position, 14f) && GenSight.LineOfSight(x.Position, a.Position, a.Map, true))
                .RandomElementWithFallback();
            if (p == null) return;
            var god = s.God;
            string line = Flavor.FromPool(SeenPool, p, null, null, GodsOnMapMath.Follows(a.BodySize) ? "small" : "large", s.chosen == p ? "chosen" : null,
                god?.domain.ToString().ToLowerInvariant());
            if (line.NullOrEmpty()) return;
            string said = line.Formatted(a.LabelShort.Named("BEAST"), a.def.label.Named("KIND")).Resolve();
            if (Speech.Say(p, said)) s.lastSeenSaid = now;
        }

        /// <summary>A large one comes to a colonist out of doors and waits for them: they follow it, of their own accord, where it leads.</summary>
        private static void Beckon(SacredStay s, Pawn a)
        {
            if (!PlayOuts.On || a.InMentalState || a.Downed || !a.Awake()) return;
            var p = a.Map.mapPawns.FreeColonistsSpawned.Where(x => Together.Able(x) && Together.FreeTime(x) && !x.Position.Roofed(a.Map) && x.Position.InHorDistOf(a.Position, 30f))
                .OrderBy(x => x.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (p == null) return;
            s.beckoned = true;
            string line = Flavor.FromPool(BeckonPool, p, null, null, s.God?.domain.ToString().ToLowerInvariant());
            if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(a.def.label.Named("KIND")).Resolve());
            Lead(s, p);
        }

        // ================= Following it =================

        /// <summary>
        /// <paramref name="p"/> follows the god's beast where it leads (the player's word on the float menu, or a beckoning):
        /// it walks out to a place well off (a vein of ore when there is one to find and its god is of the deep, else the open
        /// land) with them behind it, and what it leads to is there. False when it can't lead now.
        /// </summary>
        public static bool Lead(SacredStay s, Pawn p)
        {
            var a = s?.a;
            if (a == null || p == null || !a.Spawned || a.Downed || a.InMentalState || s.led || s.follower != null || p.Map != a.Map) return false;
            var map = a.Map;
            var god = s.God;
            if (god == null) return false;
            var ore = Ore(map, a.Position);
            bool ailing = Ailing(p) != null;
            s.boon = GodsOnMapMath.Boon(god.domain, ailing, ore.IsValid, Rand.Value);
            IntVec3 to;
            if (s.boon == AnimalBoon.Vein && ore.IsValid) to = ore;
            else if (!CellFinder.TryFindRandomCellNear(a.Position, map, GodsOnMapMath.LeadMax,
                         c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.DistanceTo(a.Position) >= GodsOnMapMath.LeadMin && !map.areaManager.Home[c]
                              && a.CanReach(c, PathEndMode.OnCell, Danger.Some) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out to))
                return false;
            if (s.boon == AnimalBoon.Vein && !ore.IsValid) s.boon = AnimalBoon.Find;
            s.follower = p;
            s.to = to;
            s.leadUntil = Now + GenDate.TicksPerHour * 2;
            try
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, to);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                go.expiryInterval = GenDate.TicksPerHour * 2;
                a.jobs.StartJob(go, JobCondition.InterruptForced);
                var follow = JobMaker.MakeJob(JobDefOf.FollowClose, a);
                follow.followRadius = 3f;
                follow.expiryInterval = GenDate.TicksPerHour * 2;
                follow.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.StartJob(follow, JobCondition.InterruptForced, null, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: following the sacred animal", e);
                s.follower = null;
                return false;
            }
            GodsOnMapBook.Instance?.Due(Now + 250);
            return true;
        }

        /// <summary>A standable cell beside a vein of ore no one has mined (silver, gold, jade, plasteel, components, uranium, steel), near enough to lead to.</summary>
        private static IntVec3 Ore(Map map, IntVec3 from)
        {
            Building best = null;
            float bestD = float.MaxValue;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Concat(map.listerBuildings.allBuildingsNonColonist).Take(4000))
            {
                if (!(t is Building b) || b.def.building?.mineableThing == null || b.def.building.mineableThing == ThingDefOf.WoodLog) continue;
                if (b.def.building.isResourceRock == false) continue;
                float d = b.Position.DistanceTo(from);
                if (d < GodsOnMapMath.LeadMin || d > GodsOnMapMath.LeadMax * 1.5f || d >= bestD) continue;
                best = b;
                bestD = d;
            }
            if (best == null) return IntVec3.Invalid;
            foreach (var c in GenAdj.CellsAdjacent8Way(best))
                if (c.InBounds(map) && c.Standable(map))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>The worst of what ails them: an illness, else the worst injury not yet healing.</summary>
        private static Hediff Ailing(Pawn p) =>
            p?.health?.hediffSet?.hediffs.Where(h => h != null && h.Visible && (h.def.makesSickThought || h is Hediff_Injury) && !h.IsPermanent())
                .OrderByDescending(h => h.Severity).FirstOrDefault();

        /// <summary>Steps between the hours: whether the beast and its follower are there yet.</summary>
        internal static void Step(GodsOnMapBook b, int now)
        {
            var s = b.beast;
            if (s?.follower == null || s.led) return;
            var a = s.a;
            var p = s.follower;
            if (a == null || !a.Spawned || a.Dead || a.Downed || p.Dead || !p.Spawned || p.Downed || now > s.leadUntil)
            {
                s.follower = null;
                return;
            }
            if (a.Position.DistanceTo(s.to) <= 3f && p.Position.DistanceTo(a.Position) <= 6f)
            {
                Arrived(s, a, p);
                return;
            }
            b.Due(now + 250);
        }

        /// <summary>There: the boon, the god's blessing moment, the favour, and the follower's thanks.</summary>
        private static void Arrived(SacredStay s, Pawn a, Pawn p)
        {
            s.led = true;
            var god = s.God;
            var map = a.Map;
            if (god == null) return;
            string what = null;
            try
            {
                switch (s.boon)
                {
                    case AnimalBoon.Cure:
                        var h = Ailing(p);
                        if (h is Hediff_Injury inj)
                        {
                            inj.Heal(inj.Severity);
                            what = "CC_GodM_Boon_CureWound".Translate(p.Named("PAWN")).Resolve();
                        }
                        else if (h != null)
                        {
                            h.Severity = Mathf.Max(0.01f, h.Severity * 0.3f);
                            what = "CC_GodM_Boon_CureIll".Translate(p.Named("PAWN"), h.LabelBase.Named("ILLNESS")).Resolve();
                        }
                        break;
                    case AnimalBoon.Vein:
                        foreach (var c in GenRadial.RadialCellsAround(s.to, 7f, true))
                            if (c.InBounds(map) && map.fogGrid.IsFogged(c))
                                map.fogGrid.Unfog(c);
                        var ore = GenAdj.CellsAdjacent8Way(new TargetInfo(s.to, map)).Select(c => c.InBounds(map) ? c.GetFirstMineable(map) : null).FirstOrDefault(m => m != null);
                        if (ore != null) what = "CC_GodM_Boon_Vein".Translate(p.Named("PAWN"), (ore.def.building?.mineableThing?.label ?? ore.def.label).Named("ORE")).Resolve();
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what the sacred animal led to", e);
            }
            if (what == null)
            {
                string found = GodGifts.LeaveFind(god, map, s.to);
                if (found != null) what = "CC_GodM_Boon_Find".Translate(p.Named("PAWN"), found.Named("FOUND")).Resolve();
            }
            var spec = PresenceSpec.For(god, map, PresenceKind.Blessing, s.to, a);
            spec.title = ("CC_GodM_Boon_Title" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[] { a.def.label.Named("BEAST"), p.Named("PAWN") }).ToArray())
                .Resolve().CapitalizeFirst();
            spec.text = ("CC_GodM_Boon_Text" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[] { a.def.label.Named("BEAST"), p.Named("PAWN") }).ToArray())
                .Resolve() + (what.NullOrEmpty() ? "" : "\n\n" + what);
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.PositiveEvent, new LookTargets(s.to, map));
            Gods.AddFavour(god, map, GodsOnMapMath.Followed, "CC_GodM_Why_Followed".Translate(p.LabelShort.Named("PAWN"), a.def.label.Named("BEAST")).Resolve());
            string act = "CC_GodM_Animal_LedAct".Translate(a.def.label.Named("BEAST"), p.LabelShort.Named("PAWN")).Resolve();
            GodMarks.Note(god, map, s.to, act, PresenceKind.Blessing);
            GodMarks.Witnessed(map, s.to, "CC_GodM_Memory_Animal".Translate(a.def.label.Named("BEAST")).Resolve(), new[] { p });
            Deeds.Record("Rimfolk_Deed_FollowedSacred", p, null, -1f, null, a.def.label);
            CouncilGameComponent.Instance?.journals?.WriteText(p, "CC_GodM_Journal_Led".Translate(a.def.label.Named("BEAST")).Resolve() + (what.NullOrEmpty() ? "" : " " + what));
            s.follower = null;
            try
            {
                if (p.CurJobDef == JobDefOf.FollowClose) p.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the follower back to work", e);
            }
        }

        // ================= Fed =================

        /// <summary>One of the colony left food for it and it ate (JobDriver_FeedSacredAnimal).</summary>
        internal static void FedBy(Pawn by)
        {
            var s = GodsOnMapBook.Instance?.beast;
            if (s == null || s.fed) return;
            s.fed = true;
            s.feeder = by;
            var god = s.God;
            if (god == null || s.a == null) return;
            Gods.AddFavour(god, s.map, GodsOnMapMath.Fed, "CC_GodM_Why_Fed".Translate(by.LabelShort.Named("PAWN"), s.a.def.label.Named("BEAST")).Resolve());
            Deeds.Record("Rimfolk_Deed_FedSacred", by, null, -1f, null, s.a.def.label);
        }

        // ================= Hurt or slain =================

        /// <summary>The colony's own hurt the god's beast (GodsOnMapPatches): the sacrilege, the god's anger at the next step.</summary>
        internal static void Struck(Pawn victim, Pawn by)
        {
            var b = GodsOnMapBook.Instance;
            var s = b?.beast;
            if (s == null || s.a != victim) return;
            bool slain = victim.Dead;
            if (s.hurt && (!slain || s.slain)) return;
            bool first = !s.hurt;
            s.hurt = true;
            s.slain |= slain;
            s.harmer ??= by;
            var god = s.God;
            var map = s.map;
            if (god == null || map == null) return;
            var cell = victim.Spawned ? victim.Position : victim.Corpse?.Position ?? IntVec3.Invalid;
            if (first)
            {
                var spec = PresenceSpec.For(god, map, PresenceKind.Wrath, cell, victim.Spawned ? victim : null);
                spec.title = ("CC_GodM_Sacrilege_Title" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[] { victim.def.label.Named("BEAST") }).ToArray())
                    .Resolve().CapitalizeFirst();
                spec.text = ("CC_GodM_Sacrilege_Text" + (spec.veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !spec.veiled).Concat(new[]
                {
                    victim.def.label.Named("BEAST"), (by?.LabelShort ?? "CC_GodM_Someone".Translate().Resolve()).Named("HARMER")
                }).ToArray()).Resolve();
                if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NegativeEvent, new LookTargets(cell, map));
                string act = "CC_GodM_Animal_HurtAct".Translate(victim.def.label.Named("BEAST"), (by?.LabelShort ?? "?").Named("PAWN")).Resolve();
                GodMarks.Note(god, map, cell, act, PresenceKind.Wrath);
                if (by != null && by.IsColonist) Deeds.Record("Rimfolk_Deed_HuntedSacred", by, null, -1f, null, victim.def.label);
                if (by != null && by.IsColonist && map != null) GodGifts.Fever(by, map, god);
            }
            float favour = slain ? (first ? GodsOnMapMath.Slain : GodsOnMapMath.Slain - GodsOnMapMath.Hurt) : GodsOnMapMath.Hurt;
            Gods.AddFavour(god, map, favour, (slain ? "CC_GodM_Why_Slain" : "CC_GodM_Why_Hurt").Translate(victim.def.label.Named("BEAST")).Resolve(), FavourSource.Sacrilege);
            if (slain)
            {
                b.beast = null;
                Gods.Remember(new GodAct
                {
                    god = god.id, home = Gods.HomeKey(map), tick = Now, kind = GodActKind.Wrath, cell = cell,
                    text = GodsOnMapWords.Veil("CC_GodM_Animal_SlainAct".Translate(victim.def.label.Named("BEAST")).Resolve(), GodsOnMapMath.Show(Gods.Realness) != GodShow.Plain)
                });
                b.Due(Now + 1);
                pendingAsh = victim;
            }
        }

        /// <summary>A slain god's beast whose body goes to ash at the next step (nothing of a god is butchered or eaten).</summary>
        private static Pawn pendingAsh;

        internal static void Ash()
        {
            var p = pendingAsh;
            pendingAsh = null;
            var corpse = p?.Corpse;
            if (corpse == null || corpse.Destroyed) return;
            try
            {
                if (corpse.Spawned)
                {
                    var map = corpse.Map;
                    var at = corpse.Position;
                    FleckMaker.ThrowLightningGlow(corpse.DrawPos, map, 2.5f);
                    FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 3);
                }
                corpse.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the slain beast's body", e);
            }
        }

        // ================= Its going =================

        /// <summary>It has gone its way: the god's favour for a stay let be (fed and followed more), remembered, a line in the news.</summary>
        private static void Departed(GodsOnMapBook b, SacredStay s, int now)
        {
            b.beast = null;
            var god = s.God;
            var map = s.map;
            var a = s.a;
            if (god == null || map == null || a == null) return;
            if (!s.hurt)
            {
                float favour = GodsOnMapMath.AnimalFavour(false, false, false, false);
                Gods.AddFavour(god, map, favour, "CC_GodM_Why_LeftBe".Translate(a.def.label.Named("BEAST")).Resolve());
            }
            bool veiled = GodsOnMapMath.Show(Gods.Realness) != GodShow.Plain;
            string text = ("CC_GodM_Animal_Gone" + (s.hurt ? "Hurt" : "") + (veiled ? "Veiled" : "")).Translate(GodsOnMapWords.Args(god, !veiled).Concat(new[] { a.def.label.Named("BEAST") }).ToArray())
                .Resolve().CapitalizeFirst();
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, null, MessageTypeDefOf.NeutralEvent);
            Gods.Remember(new GodAct
            {
                god = god.id, home = Gods.HomeKey(map), tick = now, kind = GodActKind.Depart,
                text = GodsOnMapWords.Veil("CC_GodM_Animal_GoneAct".Translate(a.def.label.Named("BEAST")).Resolve(), veiled)
            });
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, s.follower ?? s.chosen ?? s.feeder));
            Vanish(a);
        }

        /// <summary>The beast is gone for good: off the map and out of the world.</summary>
        private static void Vanish(Pawn a)
        {
            if (a == null || a.Discarded) return;
            try
            {
                if (a.Spawned)
                {
                    FleckMaker.ThrowLightningGlow(a.DrawPos, a.Map, 2f);
                    a.DeSpawn();
                }
                if (!a.Dead) GodAvatar.Unmake(a);
                if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(a);
                else if (!a.Dead) Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sacred animal gone", e);
            }
        }

        /// <summary>A hurt beast that has done with its revenge (or never took it) leaves the land at the next hour: in a breath, as a god's beast does.</summary>
        internal static void Settled(GodsOnMapBook b)
        {
            var s = b.beast;
            var a = s?.a;
            if (s == null || !s.hurt || a == null || a.Dead || !a.Spawned || a.InMentalState) return;
            Vanish(a);
            Departed(b, s, Now);
        }
    }
}
