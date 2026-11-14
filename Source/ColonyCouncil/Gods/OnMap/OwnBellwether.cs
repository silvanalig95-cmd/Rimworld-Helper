using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Bellwether, the Old Horn, on the map (docs/gods-next-proposals.md, C 3): beasts, the colony's peace, grass, the children.
    /// <list type="bullet">
    /// <item>His road (his walking form, extended): on his last evening he stops at the colony's edge at dusk and lowers his
    /// head toward its youngest; at dawn he takes his road through the colony to the far edge. A fence or a wall across it
    /// makes him stop and wait, and those who see him wait are ashamed until someone opens a gate; after a long wait he turns
    /// back the way he came.</item>
    /// <item>The child who touched the horn (SacredAnimal.SatBy): from then on they look at the sky before the weather turns
    /// wet and say so, and it comes within the hour (the weather's own next turn, held to as a forecast is: WeatherDynamics).</item>
    /// <item>A wounded thrumbo comes in from the edge and lies down at the colony's edge; the colony is asked whether to tend it
    /// (Rimfolk_Evt_GodHurtBeast). Tended, it gets up and goes its way, and the strange peace follows; left, it limps off.</item>
    /// <item>The last of something: on the night after thrumbos pass (the game's own event), someone dreams of a white beast
    /// searching for the rest of its kind.</item>
    /// </list>
    /// </summary>
    public static class OwnBellwether
    {
        public const string Key = "Bellwether";
        public const string Tag = "bellwether";
        public const string WaitThought = "Rimfolk_Thought_GodOwnMadeHimWait";
        public const string HurtIncident = "Rimfolk_Evt_GodHurtBeast";

        private static God Him => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        // ================= His road =================

        /// <summary>
        /// His hour on the land (SacredAnimal.Hourly), for the Old Horn only: on his last evening and the dawn after, his road.
        /// True while it has his hour (the usual following and leading stand aside).
        /// </summary>
        public static bool Road(SacredStay s, Pawn a, int now)
        {
            var b = OwnHappenings.Book;
            if (b == null || s?.a == null || a == null || s.hurt || GodShapes.LineTag(s.God) != Tag || !a.Spawned || a.Downed || a.InMentalState) return false;
            if (b.roadBeast != a)
            {
                b.roadBeast = a;
                b.roadStage = 0;
            }
            var map = a.Map;
            int hour = OwnHappenings.Hour(map);
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) return false;
            var god = s.God;
            switch (b.roadStage)
            {
                case 0:
                {
                    bool last = s.leaveAt - now <= OwnHappeningsMath.LastEveningHours * GenDate.TicksPerHour && s.leaveAt > now;
                    if (!(OwnHappenings.forced == OwnHappening.BellwetherRoad || last && hour >= OwnHappeningsMath.DuskFrom && hour < OwnHappeningsMath.DuskTo)) return false;
                    var dir = (a.Position - centre).ToVector3();
                    if (dir.sqrMagnitude < 1f) dir = new Vector3(1f, 0f, 0f);
                    var want = (centre.ToVector3Shifted() + dir.normalized * 16f).ToIntVec3().ClampInsideMap(map);
                    if (!CellFinder.TryFindRandomCellNear(want, map, 6, c => c.Standable(map) && !c.Roofed(map) && a.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var spot)) return false;
                    b.roadWait = spot;
                    b.roadBack = LegendBeasts.EdgeToward(map, a.Position);
                    b.roadStage = 1;
                    b.roadSince = now;
                    a.mindState.exitMapAfterTick = Math.Max(a.mindState.exitMapAfterTick, now + 2 * GenDate.TicksPerDay);
                    OwnHappenings.GoTo(a, spot, GenDate.TicksPerHour * 2);
                    OwnHappenings.Came(b, OwnHappening.BellwetherRoad, now);
                    return true;
                }
                case 1:
                {
                    if (!a.Position.InHorDistOf(b.roadWait, 2.5f))
                    {
                        if (now - b.roadSince > GenDate.TicksPerHour * 4) b.roadStage = 2;
                        else if (a.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(a, b.roadWait, GenDate.TicksPerHour * 2);
                        return true;
                    }
                    // At the edge: he lowers his head toward the colony's youngest.
                    var young = Youngest(map);
                    OwnHappenings.Stand(a, young?.Position ?? centre, GenDate.TicksPerHour + 100);
                    b.roadStage = 2;
                    var called = SacredAnimal.Called(a, god);
                    OwnHappenings.Card(god, map, young != null ? "Dusk" : "DuskNone", false, new LookTargets(a), null, Importance.Notable, called.Named("BEAST"),
                        (young?.LabelShort ?? "").Named("YOUNG"));
                    var seer = GodsOnMapWords.Seer(map, a.Position, 35f);
                    OwnHappenings.Say(seer, "dusk", god, null, (young?.LabelShort ?? "").Named("YOUNG"));
                    OwnHappenings.Saw(god, map, a.Position, GodActKind.Act, seer, "CC_GodO_Saw_Dusk", called.Named("BEAST"), (young?.LabelShort ?? "").Named("YOUNG"));
                    return true;
                }
                case 2:
                {
                    if (!(hour >= OwnHappeningsMath.DawnFrom && hour < OwnHappeningsMath.DawnTo) && OwnHappenings.forced != OwnHappening.BellwetherRoad)
                    {
                        var young = Youngest(map);
                        if (a.CurJobDef != JobDefOf.Wait_MaintainPosture) OwnHappenings.Stand(a, young?.Position ?? centre, GenDate.TicksPerHour + 100);
                        return true;
                    }
                    return Dawn(b, s, a, map, centre, now);
                }
                case 3:
                {
                    if (a.Position.InHorDistOf(b.roadEdge, 4f) || a.Position.OnEdge(map))
                    {
                        a.mindState.exitMapAfterTick = now;
                        b.roadStage = 5;
                        return false;
                    }
                    if (a.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(a, b.roadEdge, GenDate.TicksPerHour * 3);
                    return true;
                }
                case 4:
                    return Waiting(b, s, a, map, now);
                default:
                    return false;
            }
        }

        /// <summary>The colony's youngest at home, or null.</summary>
        private static Pawn Youngest(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && !p.Dead).OrderBy(p => p.ageTracker.AgeBiologicalTicks).FirstOrDefault();

        /// <summary>The dawn after: his road through the colony to the far edge, or, a fence across it, waiting.</summary>
        private static bool Dawn(OwnHappeningsBook b, SacredStay s, Pawn a, Map map, IntVec3 centre, int now)
        {
            var god = s.God;
            var dir = (centre - a.Position).ToVector3();
            if (dir.sqrMagnitude < 1f) dir = new Vector3(0f, 0f, 1f);
            dir.Normalize();
            var far = (centre.ToVector3Shifted() + dir * 300f).ToIntVec3().ClampInsideMap(map);
            if (!CellFinder.TryFindRandomEdgeCellNearWith(far, 25f, map, c => c.Standable(map), out var edge)) edge = far;
            b.roadEdge = edge;
            var called = SacredAnimal.Called(a, god);
            if (a.CanReach(edge, PathEndMode.OnCell, Danger.Deadly))
            {
                b.roadStage = 3;
                OwnHappenings.GoTo(a, edge, GenDate.TicksPerHour * 3);
                var seer = GodsOnMapWords.Seer(map, a.Position, 35f);
                OwnHappenings.Say(seer, "road", god);
                OwnHappenings.Saw(god, map, a.Position, GodActKind.Depart, seer, "CC_GodO_Saw_Road", called.Named("BEAST"));
                return true;
            }
            // Something of the colony's across his road: he goes as far as he can, and waits there.
            IntVec3 wait = IntVec3.Invalid;
            var from = a.Position.ToVector3Shifted();
            float len = (edge.ToVector3Shifted() - from).magnitude;
            for (float d = 0f; d < len; d += OwnHappeningsMath.RoadStep)
            {
                var c = (from + dir * d).ToIntVec3();
                if (!c.InBounds(map)) break;
                if (c.Standable(map) && a.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) wait = c;
                else if (wait.IsValid && c.GetEdifice(map) != null) break;
            }
            if (!wait.IsValid)
            {
                b.roadStage = 5;
                return false;
            }
            b.roadWait = wait;
            b.roadSince = now;
            b.roadStage = 4;
            OwnHappenings.GoTo(a, wait, GenDate.TicksPerHour * 2);
            OwnHappenings.Card(god, map, "RoadWait", true, new LookTargets(a), LetterDefOf.NeutralEvent, Importance.Notable, called.Named("BEAST"));
            var w = GodsOnMapWords.Seer(map, wait, 40f);
            OwnHappenings.Say(w, "roadwait", god);
            OwnHappenings.Saw(god, map, wait, GodActKind.Act, w, "CC_GodO_Saw_RoadWait", called.Named("BEAST"));
            Ashamed(map, wait);
            return true;
        }

        /// <summary>Those of the colony who see him wait at the fence, ashamed.</summary>
        private static void Ashamed(Map map, IntVec3 at)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && p.Position.InHorDistOf(at, OwnHappeningsMath.ShameRange) && GenSight.LineOfSight(p.Position, at, map, true)))
                OwnHappenings.Memory(p, WaitThought);
        }

        /// <summary>Waiting at the fence: a gate opened (he can reach the far edge now), he goes through; too long, he turns back.</summary>
        private static bool Waiting(OwnHappeningsBook b, SacredStay s, Pawn a, Map map, int now)
        {
            var god = s.God;
            var called = SacredAnimal.Called(a, god);
            if (a.CanReach(b.roadEdge, PathEndMode.OnCell, Danger.Deadly))
            {
                b.roadStage = 3;
                OwnHappenings.Forget(map, WaitThought);
                OwnHappenings.GoTo(a, b.roadEdge, GenDate.TicksPerHour * 3);
                var seer = GodsOnMapWords.Seer(map, a.Position, 35f);
                OwnHappenings.Say(seer, "roadopen", god);
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodO_RoadOpen_Text".Translate(called.Named("BEAST")).Resolve(), new LookTargets(a), MessageTypeDefOf.PositiveEvent);
                OwnHappenings.Saw(god, map, a.Position, GodActKind.Depart, seer, "CC_GodO_Saw_RoadOpen", called.Named("BEAST"));
                return true;
            }
            if (now - b.roadSince > OwnHappeningsMath.WaitHoursMost * GenDate.TicksPerHour)
            {
                b.roadStage = 5;
                if (b.roadBack.IsValid) OwnHappenings.GoTo(a, b.roadBack, GenDate.TicksPerHour * 3);
                a.mindState.exitMapAfterTick = now + GenDate.TicksPerHour * 3;
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodO_RoadBack_Text".Translate(called.Named("BEAST")).Resolve(), new LookTargets(a), MessageTypeDefOf.NeutralEvent);
                OwnHappenings.Saw(god, map, a.Position, GodActKind.Depart, GodsOnMapWords.Seer(map, a.Position, 35f), "CC_GodO_Saw_RoadBack", called.Named("BEAST"));
                return true;
            }
            if (!a.Position.InHorDistOf(b.roadWait, 2f)) OwnHappenings.GoTo(a, b.roadWait, GenDate.TicksPerHour);
            else if (a.CurJobDef != JobDefOf.Wait_MaintainPosture) OwnHappenings.Stand(a, b.roadEdge, GenDate.TicksPerHour + 100);
            Ashamed(map, b.roadWait);
            return true;
        }

        /// <summary>He went his way in peace (SacredAnimal: let pass): remembered for the wounded beast's coming.</summary>
        public static void LetPass()
        {
            var b = OwnHappenings.Book;
            if (b != null) b.lastPeace = Now;
        }

        /// <summary>One of his kin killed by the colony (GodHealth: shunned).</summary>
        public static void KinKilled()
        {
            var b = OwnHappenings.Book;
            if (b != null) b.lastKinKilled = Now;
        }

        // ================= The child who touched the horn =================

        /// <summary>A child touched his horn (SacredAnimal.SatBy): the colony remembers them, the newest kept.</summary>
        public static void Touched(Pawn p)
        {
            var b = OwnHappenings.Book;
            if (b == null || p == null || b.horned.Contains(p)) return;
            b.horned.Add(p);
            while (b.horned.Count > OwnHappeningsMath.HornKept) b.horned.RemoveAt(0);
        }

        /// <summary>The hour: one who touched the horn looks at the sky before the weather turns wet, and says so.</summary>
        private static void Horn(OwnHappeningsBook b, int now)
        {
            if (b.horned.Count == 0) return;
            b.horned.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            var g = Him;
            foreach (var map in b.horned.Where(p => p.Spawned && Homes.IsHome(p.Map)).Select(p => p.Map).Distinct().ToList())
            {
                var next = WeatherDynamics.Foreseen(map, Mathf.RoundToInt(OwnHappeningsMath.HornWithinHours * GenDate.TicksPerHour), out int left);
                if (next == null) continue;
                var kind = WeatherDynamics.KindOf(next);
                bool wet = kind == WeatherKind.Rain || kind == WeatherKind.Downpour || kind == WeatherKind.Storm || kind == WeatherKind.Snow;
                if (WeatherDynamics.KindOf(map.weatherManager.curWeather) == kind) continue;
                // One of them at a time, each their own gap apart (Interplay.Ready keeps it, once it lets them speak).
                if (!OwnHappeningsMath.Free(OwnHappening.BellwetherHorn, GodPace.DaysSince(b.Last(OwnHappening.BellwetherHorn), now))) return;
                foreach (var p in b.horned.Where(p => p.Spawned && p.Map == map && GodOmens.Awake(p)).InRandomOrder().ToList())
                {
                    if (!OwnHappeningsMath.HornSpeaks(left / (float)GenDate.TicksPerHour, wet, OwnHappeningsMath.HornGapDays, Rand.Value)) continue;
                    if (!Interplay.Ready("own-horn|" + p.thingIDNumber, OwnHappeningsMath.HornGapDays)) continue;
                    Foretell(b, g, p, kind == WeatherKind.Snow, now);
                    return;
                }
            }
        }

        /// <summary>They stop, look up at the sky a while, and say it is coming.</summary>
        internal static void Foretell(OwnHappeningsBook b, God g, Pawn p, bool snow, int now)
        {
            var map = p.Map;
            OwnHappenings.Stand(p, p.Position + IntVec3.North * 6, 400);
            OwnHappenings.Motes(map, p.DrawPos + new Vector3(0f, 0f, 0.6f), OwnHappenings.BlueTwinkle, new Color(0.8f, 0.9f, 1f), 6, 0.5f);
            OwnHappenings.Came(b, OwnHappening.BellwetherHorn, now, snow ? "snow" : "rain", p.ThingID);
            OwnHappenings.Say(p, snow ? "horn,snow" : "horn,rain", g);
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, snow ? "CC_GodO_Saw_HornSnow" : "CC_GodO_Saw_Horn");
        }

        // ================= A wounded thrumbo at the edge =================

        private static PawnKindDef Thrumbo => DefDatabase<PawnKindDef>.GetNamedSilentFail("Thrumbo");

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = Him;
            if (g == null || b.hurt != null || GodsOnMapBook.Instance?.beast != null) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map) || !OwnHappenings.Quiet(map) || Thrumbo == null) continue;
                bool live = !MapFacts.InVacuum(map) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(Thrumbo.race);
                var why = OwnHappeningsMath.HurtTrigger(live, GodPace.DaysSince(b.lastKinKilled, now), GodPace.DaysSince(b.lastPeace, now));
                if (OwnHappenings.Due(b, OwnHappening.BellwetherHurt, g, map, why, now) && Hurt(b, map, now)) return;
            }
        }

        /// <summary>A wild thrumbo, wounded, comes in from the edge to the colony's edge and lies down; then the colony's card.</summary>
        internal static bool Hurt(OwnHappeningsBook b, Map map, int now)
        {
            var kind = Thrumbo;
            var centre = CampSite.ColonyCenter(map);
            if (kind == null || !centre.IsValid || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal)) return false;
            Pawn t;
            try
            {
                t = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                GenSpawn.Spawn(t, entry, map, Rot4.Random);
                // Wounds of the wild, never near death.
                for (int i = 0; i < 4 && t.health.summaryHealth.SummaryHealthPercent > 0.6f; i++)
                    t.TakeDamage(new DamageInfo(Rand.Bool ? DamageDefOf.Bite : DamageDefOf.Scratch, Rand.Range(6f, 12f)));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a wounded thrumbo", e);
                return false;
            }
            if (t.Dead || t.Downed)
            {
                if (!t.Destroyed) t.Destroy();
                return false;
            }
            var dir = (entry - centre).ToVector3();
            var want = (centre.ToVector3Shifted() + (dir.sqrMagnitude < 1f ? new Vector3(1f, 0f, 0f) : dir.normalized) * 15f).ToIntVec3().ClampInsideMap(map);
            if (!CellFinder.TryFindRandomCellNear(want, map, 6, c => c.Standable(map) && !c.Roofed(map) && t.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var spot)) spot = entry;
            b.hurt = t;
            b.hurtMap = map;
            b.hurtAt = spot;
            b.hurtUntil = now + Mathf.RoundToInt(OwnHappeningsMath.HurtLieHours * GenDate.TicksPerHour);
            b.tender = null;
            b.tended = false;
            t.mindState.exitMapAfterTick = b.hurtUntil + GenDate.TicksPerHour * 6;
            OwnHappenings.GoTo(t, spot, GenDate.TicksPerHour * 3);
            OwnHappenings.Came(b, OwnHappening.BellwetherHurt, now);
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(HurtIncident);
            asking = true;
            try
            {
                if (def?.Worker is IncidentWorker_Happening w && w.ScenarioCanFire(map)) w.ScenarioFire(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the wounded thrumbo's card", e);
            }
            finally
            {
                asking = false;
            }
            return true;
        }

        /// <summary>The wounded thrumbo's card is being asked (read by its worker).</summary>
        internal static bool asking;

        /// <summary>The colony's answer: <paramref name="by"/> goes to tend it (0), or it is let be (1).</summary>
        internal static void Answered(Pawn by, int option)
        {
            var b = OwnHappenings.Book;
            if (b?.hurt == null) return;
            if (option != 0 || by == null) return;
            b.tender = by;
            b.tendUntil = Now + Mathf.RoundToInt(OwnHappeningsMath.TendWalkHours * GenDate.TicksPerHour);
            b.Due(Now + 120);
        }

        /// <summary>The wounded beast's hour: lying at the edge, until it is tended or its time is up and it limps off.</summary>
        private static void HurtHour(OwnHappeningsBook b, int now)
        {
            var t = b.hurt;
            if (t == null) return;
            if (t.Dead || t.Destroyed || !t.Spawned)
            {
                b.hurt = null;
                return;
            }
            if (b.tended) return;
            if (now > b.hurtUntil)
            {
                b.hurt = null;
                t.mindState.exitMapAfterTick = now;
                Tell.Message(NewsKind.Land, Importance.Notable, t.Map, "CC_GodO_HurtLimped_Text".Translate().Resolve(), new LookTargets(t), MessageTypeDefOf.NeutralEvent);
                return;
            }
            if (t.Downed || t.InMentalState) return;
            if (!t.Position.InHorDistOf(b.hurtAt, 2f)) OwnHappenings.GoTo(t, b.hurtAt, GenDate.TicksPerHour);
            else if (t.CurJobDef != JobDefOf.LayDown)
            {
                var lie = JobMaker.MakeJob(JobDefOf.LayDown, b.hurtAt);
                lie.expiryInterval = GenDate.TicksPerHour + 200;
                OwnHappenings.Job(t, lie);
            }
        }

        /// <summary>The one sent: at the beast's side, they tend its wounds; it gets up, goes its way, and the peace follows.</summary>
        private static void TendStep(OwnHappeningsBook b, int now)
        {
            var p = b.tender;
            var t = b.hurt;
            if (p == null || t == null || b.tended) return;
            if (p.Dead || !p.Spawned || p.Downed || t.Dead || !t.Spawned || p.Map != t.Map || now > b.tendUntil)
            {
                b.tender = null;
                return;
            }
            if (!p.Position.InHorDistOf(t.Position, 1.9f))
            {
                if (p.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(p, CellFinder.StandableCellNear(t.Position, t.Map, 1.5f), GenDate.TicksPerHour);
                b.Due(now + 120);
                return;
            }
            // At its side: kneeling a while over it, then each wound tended as well as their hands allow.
            if (p.CurJobDef != JobDefOf.Wait_MaintainPosture)
            {
                OwnHappenings.Stand(p, t.Position, 500);
                b.Due(now + 480);
                return;
            }
            float skill = p.skills?.GetSkill(SkillDefOf.Medicine) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;
            foreach (var h in t.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.TendableNow()).ToList())
                h.Tended(Mathf.Clamp01(0.3f + 0.04f * skill + Rand.Range(-0.1f, 0.1f)), 1f);
            b.tended = true;
            b.tender = null;
            b.hurt = null;
            var map = t.Map;
            var g = Him;
            t.jobs?.StopAll();
            t.mindState.exitMapAfterTick = now + GenDate.TicksPerHour * 2;
            Peace(map);
            b.lastPeace = now;
            if (g != null) Gods.AddFavour(g, map, 1.5f, null);
            OwnHappenings.Card(g, map, "Tended", true, new LookTargets(t), LetterDefOf.PositiveEvent, Importance.Notable, p.Named("PAWN"));
            OwnHappenings.Say(p, "tended", g);
            OwnHappenings.Journal(p, "CC_GodO_Journal_Tended", p.Named("PAWN"));
            OwnHappenings.Saw(g, map, t.Position, GodActKind.Blessing, p, "CC_GodO_Saw_Tended");
        }

        /// <summary>The strange peace on everyone of the colony at home (as when he is let pass: SacredAnimal).</summary>
        private static void Peace(Map map)
        {
            foreach (var p in Homes.People(map).Where(p => p.Spawned && p.needs?.mood != null && People.HasInnerLife(p))) OwnHappenings.Memory(p, SacredAnimal.PeaceThought);
        }

        // ================= The last of its kind =================

        /// <summary>Thrumbos passing (the game's own event): someone may dream that night.</summary>
        internal static void Passed(OwnHappeningsBook b, Map map)
        {
            var g = Him;
            int now = Now;
            if (b == null || g == null || !OwnHappenings.Acts(g, map)) return;
            if (OwnHappenings.forced != OwnHappening.BellwetherLast
                && (!OwnHappeningsMath.Free(OwnHappening.BellwetherLast, OwnHappenings.DaysSince(b, OwnHappening.BellwetherLast, now)) || !Rand.Chance(OwnHappeningsMath.LastChance)))
                return;
            b.passedMap = map;
            b.passedAt = now;
        }

        /// <summary>At dawn after: one who woke from the night tells of a white beast searching for the rest of its kind.</summary>
        internal static bool Dream(OwnHappeningsBook b, Map map, int now, bool anyway = false)
        {
            var g = Him;
            var comp = CouncilGameComponent.Instance;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => People.HasInnerLife(x) && !x.DevelopmentalStage.Baby() && (anyway || comp?.dreams?.WokeFromNight(x) == true))
                .RandomElementWithFallback();
            if (g == null || comp == null || p == null) return false;
            b.passedAt = -1;
            GodOmens.Dreamt(comp, p, "CC_GodO_Last_Dream".Translate().Resolve());
            OwnHappenings.Came(b, OwnHappening.BellwetherLast, now);
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_GodO_Last_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
            OwnHappenings.Say(p, "last", g);
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, "CC_GodO_Saw_Last");
            return true;
        }

        // ================= The hour and the steps =================

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            Horn(b, now);
            HurtHour(b, now);
            if (b.passedAt >= 0)
            {
                var map = b.passedMap;
                if (map == null || now - b.passedAt > GenDate.TicksPerDay * 2) b.passedAt = -1;
                else if (now - b.passedAt > GenDate.TicksPerHour * 6 && OwnHappenings.Hour(map) >= 5 && OwnHappenings.Hour(map) <= 9) Dream(b, map, now);
            }
        }

        internal static void Step(OwnHappeningsBook b, int now) => TendStep(b, now);
    }

    /// <summary>
    /// A wounded thrumbo at the colony's edge (Rimfolk_Evt_GodHurtBeast): fired by Bellwether's own (OwnBellwether.Hurt),
    /// never by the storyteller. A is the colony's best with animals; the answer is played out on the map: A walks out and
    /// tends it, or it is let be and limps off.
    /// </summary>
    public class HappeningWorker_GodHurtBeast : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => OwnBellwether.asking && OwnHappeningsBook.Instance?.hurtMap == map;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var b = OwnHappeningsBook.Instance;
            var t = b?.hurt;
            if (t == null || !t.Spawned) return false;
            ctx.a = Colonists(ctx.map).Where(p => p.DevelopmentalStage.Adult() && !p.Downed && !p.InMentalState && Together.Able(p) && p.CanReach(t, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.place = t;
            ctx.beast = t.def.label;
            ctx.spot = b.hurtAt;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add("age" + Mathf.Clamp(GodStranger.Age, 0, 3));

        protected override LookTargets Look(HappeningContext ctx) => ctx.place != null && ctx.place.Spawned ? new LookTargets(ctx.place) : LookTargets.Invalid;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            OwnBellwether.Answered(ctx.a, option);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
