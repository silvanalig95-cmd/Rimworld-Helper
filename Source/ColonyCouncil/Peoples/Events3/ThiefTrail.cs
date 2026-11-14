using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 11. The thief on the drove road (the herding folk) =====================

    /// <summary>
    /// The thief on the drove road (item 11). Raiders on their way off the colony's map drive some of its beasts off with them
    /// (once a raid, at a small chance, likelier when they came with beasts of their own: Animal Husbandry Raids' drovers),
    /// told as it happens; a beast whose drovers fell or ran is the colony's again. The next morning the herders' riders come
    /// on the thieves' trail (on foot, or mounted with Giddy-Up). The colony can send a rider with them: a caravan of one to
    /// the thieves' camp a tile or two off (the game's own bandit camp of the thieves), the herders' riders waiting there and
    /// the colony's beasts tethered inside, and the fight by the game's own rules. Or the herders go alone and bring back
    /// what they can, keeping one beast as their due. Or the colony lets the beasts go.
    /// </summary>
    public static class ThiefTrail
    {
        public const string AloneKind = "pev:trail";
        public const string RideKind = "pev:ride";
        public const string EventDef = "Rimfolk_Evt_ThiefTrail";

        private static int Now => Find.TickManager.TicksGame;

        public static JobDef RideOutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv3_RideOut");

        private static List<TheftRecord> Thefts => PeopleEvents3Book.Instance?.thefts;

        /// <summary>The raids already looked at as they went (by their lord): once each. Not saved: after a load a raid on its way out may be looked at again.</summary>
        private static readonly HashSet<int> looked = new HashSet<int>();

        public static void Forget() => looked.Clear();

        /// <summary>A raid's group on its way off the map (beaten, fleeing, or carrying off what it came for).</summary>
        public static bool Retreating(Lord lord)
        {
            var toil = lord?.CurLordToil;
            return toil is LordToil_ExitMap || toil is LordToil_PanicFlee || toil is LordToil_KidnapCover || toil is LordToil_StealCover || toil is LordToil_ExitMapAndDefendSelf;
        }

        /// <summary>A beast of the colony's that raiders could drive off: one of its stock out in the open, on its feet, bonded to nobody, not ridden.</summary>
        public static bool Stock(Pawn a, Map map)
        {
            if (a == null || a.Dead || !a.Spawned || a.Map != map || a.Faction != Faction.OfPlayer || a.RaceProps?.Animal != true || a.Downed || a.InMentalState) return false;
            if (a.Position.Roofed(map) || a.Position.Fogged(map) || a.CarriedBy != null || a.GetCaravan() != null) return false;
            if (a.relations != null && a.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) != null) return false;
            if (a.CurJobDef != null && a.CurJobDef.defName.Contains("Mounted")) return false;
            return a.RaceProps.herdAnimal || a.RaceProps.Roamer || a.RaceProps.packAnimal || a.def.HasComp(typeof(CompMilkable)) || a.def.HasComp(typeof(CompShearable)) || a.def.HasComp(typeof(CompEggLayer));
        }

        private static bool Raider(Pawn p, Lord lord, Map map) =>
            p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && p.RaceProps.Humanlike && p.GetLord() == lord && p.carryTracker?.CarriedThing == null;

        /// <summary>The theft open on this map, the beasts still being driven across it.</summary>
        public static TheftRecord Driving(Map map) => Thefts?.FirstOrDefault(r => r.Open && r.map == map && r.stage == "driving");

        /// <summary>
        /// The events' hour at a home: a raid on its way out may drive off beasts of the colony's within reach, once; the beasts
        /// being driven are watched (left behind when their drovers fell or ran; kept in the world once off the map).
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null) return;
            foreach (var lord in map.lordManager.lords.ToList())
            {
                var f = lord?.faction;
                if (f == null || f.IsPlayer || f.def == null || !f.def.humanlikeFaction || !f.HostileTo(Faction.OfPlayer) || !Retreating(lord)) continue;
                if (!looked.Add(lord.loadID)) continue;
                var raiders = lord.ownedPawns.Where(p => Raider(p, lord, map)).ToList();
                if (raiders.Count == 0) continue;
                bool own = lord.ownedPawns.Any(p => p != null && p.RaceProps?.Animal == true);
                if (!Rand.Chance(PeopleEvents3Math.DriveOffChance(own))) continue;
                var beasts = map.mapPawns.SpawnedColonyAnimals.Where(a => Stock(a, map) && raiders.Any(r => r.Position.InHorDistOf(a.Position, PeopleEvents3Math.DriveReach)))
                    .OrderBy(a => raiders.Min(r => r.Position.DistanceToSquared(a.Position))).Take(PeopleEvents3Math.DriveOffCount(Rand.Value)).ToList();
                if (beasts.Count > 0) DriveOff(lord, f, beasts, map, now);
            }
            foreach (var r in b.thefts.Where(r => r.Open && r.map == map).ToList()) Tend(r, now);
        }

        /// <summary>The raiders take the beasts into their group: they go off the map with them, as the raid's own animals do.</summary>
        private static void DriveOff(Lord lord, Faction thieves, List<Pawn> beasts, Map map, int now)
        {
            var b = PeopleEvents3Book.Instance;
            var r = b?.thefts.FirstOrDefault(x => x.Open && x.map == map && x.thieves == thieves && x.stage == "driving");
            if (r == null) r = b?.AddTheft(new TheftRecord { map = map, thieves = thieves, tick = now, stage = "driving" });
            if (r == null) return;
            var taken = new List<Pawn>();
            foreach (var a in beasts)
            {
                if (r.animals.Count >= PeopleEvents3Math.MaxStolen) break;
                try
                {
                    a.jobs?.StopAll();
                    a.GetLord()?.RemovePawn(a);
                    a.SetFaction(thieves);
                    lord.AddPawn(a);
                    r.animals.Add(a);
                    taken.Add(a);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: beasts driven off", e);
                }
            }
            if (taken.Count == 0) return;
            Tell.Message(NewsKind.World, Importance.Important, map,
                "CC_PEv3_DrivenOff".Translate(thieves.Name.Named("FACTION"), Kinds(taken).Named("BEASTS")).Resolve(), new LookTargets(taken), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>The beasts' kinds in a sentence: "two muffalo and a yak".</summary>
        public static string Kinds(IEnumerable<Pawn> beasts)
        {
            var parts = beasts.Where(a => a != null).GroupBy(a => a.kindDef).Select(g => g.Count() == 1 ? g.Key.label.WithIndefiniteArticle() : g.Count() + " " + g.Key.GetLabelPlural())
                .ToList();
            return parts.Count == 0 ? "?" : TextUtil.JoinAnd(parts);
        }

        /// <summary>
        /// A theft as the hours go: beasts whose drovers fell or ran are the colony's again; those off the map are kept in the
        /// world while the trail is open; all gone, the riders may come the next morning.
        /// </summary>
        private static void Tend(TheftRecord r, int now)
        {
            var map = r.map;
            if (r.stage != "driving") return;
            foreach (var a in r.animals.ToList())
            {
                if (a == null || a.Dead || a.Discarded)
                {
                    r.animals.Remove(a);
                    continue;
                }
                if (!a.Spawned)
                {
                    Keep(a);
                    continue;
                }
                if (a.Map != map) continue;
                var lord = a.GetLord();
                bool drovers = lord != null && lord.ownedPawns.Any(p => Raider(p, lord, map) && p.Position.InHorDistOf(a.Position, PeopleEvents3Math.LeftBehind));
                if (drovers && a.Faction == r.thieves) continue;
                // Left behind: the colony's again, where it stands.
                try
                {
                    lord?.RemovePawn(a);
                    if (a.Faction != Faction.OfPlayer) a.SetFaction(Faction.OfPlayer);
                    r.animals.Remove(a);
                    Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv3_LeftBehind".Translate(a.Named("ANIMAL")).Resolve(), new LookTargets(a), MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: a beast left behind", e);
                }
            }
            if (r.animals.Count == 0)
            {
                Close(r);
                return;
            }
            if (r.animals.All(a => !a.Spawned))
            {
                r.stage = "taken";
                // The riders come the next morning, between six and ten, at least a few hours on.
                int hour = GenLocalDate.HourOfDay(map);
                int toMorning = ((24 - hour + PeopleEvents3Math.TrailFromHour) % 24) * GenDate.TicksPerHour;
                if (toMorning < PeopleEvents3Math.TrailMinHours * GenDate.TicksPerHour) toMorning += GenDate.TicksPerDay;
                r.due = now + toMorning + Rand.RangeInclusive(0, (PeopleEvents3Math.TrailToHour - PeopleEvents3Math.TrailFromHour - 1) * GenDate.TicksPerHour);
            }
        }

        /// <summary>A stolen beast off the map: kept in the world while the trail is open.</summary>
        private static void Keep(Pawn a)
        {
            if (a == null || a.Dead || a.Discarded || a.Spawned) return;
            if (!Find.WorldPawns.Contains(a)) Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.KeepForever);
            else Find.WorldPawns.ForcefullyKeptPawns.Add(a);
        }

        private static void LetGo(Pawn a)
        {
            if (a == null || WorldLedger.Held(a)) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(a);
        }

        /// <summary>The trail closed: the beasts still away let go of (they are the thieves' now).</summary>
        public static void Close(TheftRecord r)
        {
            if (r == null) return;
            r.done = true;
            r.stage = "over";
            foreach (var a in r.animals.Where(a => a != null && !a.Spawned)) LetGo(a);
        }

        /// <summary>The stolen beasts still to be had (alive, off the map).</summary>
        public static List<Pawn> Away(TheftRecord r) => r?.animals.Where(a => a != null && !a.Dead && !a.Discarded && !a.Spawned).ToList() ?? new List<Pawn>();

        /// <summary>The theft the riders are at the gate about (or would come about) on this map.</summary>
        public static TheftRecord Asked(Map map) => Thefts?.LastOrDefault(r => r.Open && r.map == map && (r.stage == "asked" || r.stage == "taken") && Away(r).Count > 0);

        /// <summary>
        /// The world's hour: the riders' morning after a theft (once a theft, a roll; if they come, the event fired then, or
        /// later that morning while another question waits; the next morning at most twice more), and the thieves' camp
        /// with the colony's rider on its trail.
        /// </summary>
        public static void Hourly(int now)
        {
            var all = Thefts;
            if (all == null) return;
            foreach (var r in all.Where(r => r.Open).ToList())
            {
                if (r.map == null || !Find.Maps.Contains(r.map))
                {
                    Close(r);
                    continue;
                }
                if (r.stage == "taken") Morning(r, now);
                else if (r.stage == "asked" && now - r.due > GenDate.TicksPerDay * 2) Close(r);
                else if (r.stage == "ride") Ride(r, now);
                foreach (var a in r.animals) Keep(a);
            }
        }

        private static void Morning(TheftRecord r, int now)
        {
            if (Away(r).Count == 0)
            {
                Close(r);
                return;
            }
            if (now < r.due) return;
            var herders = PeoplesD.Friendly(PeoplesMath.Herders);
            if (!r.rolled)
            {
                r.rolled = true;
                if (herders == null || !Rand.Chance(PeopleEvents3Math.TrailChance * StoryDirection.Factor(StoryWaysMath.Visitors)))
                {
                    Close(r);
                    return;
                }
            }
            int hour = GenLocalDate.HourOfDay(r.map);
            if (hour < PeopleEvents3Math.TrailFromHour || hour > PeopleEvents3Math.TrailToHour) return;
            if (herders != null && PeopleEvents2.Fire(EventDef, r.map, herders))
            {
                var b = PeopleEvents3Book.Instance;
                if (b != null) b.lastTrail = now;
                return;
            }
            if (hour == PeopleEvents3Math.TrailToHour && ++r.tries >= 3) Close(r);
        }

        /// <summary>Dev mode: the riders come within the hour about the last theft on this map (one made of a beast of the colony's when there's none).</summary>
        public static bool Soon(Map map)
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || map == null) return false;
            var r = b.thefts.LastOrDefault(x => x.Open && x.map == map && x.stage == "taken");
            if (r == null)
            {
                var beast = map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a => Stock(a, map));
                var thieves = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => !f.IsPlayer && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.defeated);
                if (beast == null || thieves == null) return false;
                beast.DeSpawn();
                beast.SetFaction(thieves);
                Keep(beast);
                r = b.AddTheft(new TheftRecord { map = map, thieves = thieves, tick = Now, stage = "taken", animals = new List<Pawn> { beast } });
            }
            r.rolled = true;
            r.due = Now;
            r.tries = 0;
            return true;
        }

        // ---------------- The riders at the gate ----------------

        /// <summary>The riders' horses with Giddy-Up (none without).</summary>
        public static List<Pawn> Horses(Faction f, int n)
        {
            var list = new List<Pawn>();
            var horse = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
            if (!GiddyUpMounts.On || horse == null || !GiddyUpMounts.Ridable(horse.race)) return list;
            for (int i = 0; i < n; i++)
                list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(horse, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    fixedBiologicalAge: horse.RaceProps.lifeExpectancy * 0.3f)));
            return list;
        }

        /// <summary>The riders (and their horses) ride off the map together.</summary>
        public static void RideOff(IEnumerable<Pawn> party)
        {
            var list = party.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Faction != Faction.OfPlayer).ToList();
            foreach (var g in list.GroupBy(p => new { p.Map, p.Faction }))
            {
                foreach (var p in g) p.GetLord()?.RemovePawn(p);
                LordMaker.MakeNewLord(g.Key.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), g.Key.Map, g.ToList());
            }
            foreach (var p in party.Where(p => p != null && !p.Spawned && !p.Dead && !p.Discarded)) Arrivals.Release(p);
        }

        /// <summary>The colony lets the beasts go: the riders ride home, and the trail is closed.</summary>
        public static void LetBeastsGo(HappeningContext ctx)
        {
            var r = Thefts?.FirstOrDefault(x => x.id.ToString() == ctx?.note);
            Close(r);
            RideOff(PeoplesG.Party(ctx));
        }

        // ---------------- Alone ----------------

        /// <summary>The riders go after the thieves by themselves: back in a day or a few with what they could bring.</summary>
        public static void Alone(HappeningContext ctx)
        {
            var r = Thefts?.FirstOrDefault(x => x.id.ToString() == ctx?.note);
            var t = CouncilGameComponent.Instance?.happenings;
            if (r == null || t == null || ctx.faction == null) return;
            r.stage = "alone";
            r.herders = ctx.faction;
            var v = t.AddVisit(AloneKind, ctx.incident, ctx.map, null, null, null, Now + (PeopleEvents3Math.AloneMaxDays + 3) * GenDate.TicksPerDay);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + r.id;
            v.due = Now + Mathf.RoundToInt(Rand.Range(PeopleEvents3Math.AloneMinDays, PeopleEvents3Math.AloneMaxDays) * GenDate.TicksPerDay);
            RideOff(PeoplesG.Party(ctx));
        }

        private static TheftRecord RecordOf(Visit v)
        {
            var parts = (v?.text ?? "").Split('|');
            return parts.Length > 1 && int.TryParse(parts[1], out int id) ? Thefts?.FirstOrDefault(r => r.id == id) : null;
        }

        /// <summary>Their days on the trail done: one rider leads home what they brought back, less one beast kept as their due.</summary>
        public static bool AloneHour(Visit v, int now)
        {
            if (v.done) return false;
            if (now < v.due) return true;
            int hour = GenLocalDate.HourOfDay(v.map);
            if (hour < 7 || hour > 18) return true;
            v.done = true;
            var r = RecordOf(v);
            var f = PeoplesF.PeopleOf(v);
            var map = v.map;
            var away = Away(r);
            if (r == null || f == null || map == null || away.Count == 0)
            {
                Close(r);
                return false;
            }
            var rolls = away.Select(_ => Rand.Value).ToList();
            int back = PeopleEvents3Math.BroughtBack(rolls);
            var found = away.Where((a, i) => rolls[i] < PeopleEvents3Math.AloneBack).ToList();
            var due = found.Count > 0 ? found.OrderByDescending(a => a.MarketValue).Last() : null;
            var home = found.Where(a => a != due).ToList();
            try
            {
                if (due != null)
                {
                    // Theirs for the trail: it goes home with the herders.
                    LetGo(due);
                    due.SetFaction(f);
                    r.animals.Remove(due);
                }
                var rider = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_Herders_Drover"), f, new FloatRange(18f, 50f));
                if (home.Count > 0 && rider != null && Arrivals.Visit(new List<Pawn> { rider }, map, f, PeoplesD.GateSpot(map), 3f) != null)
                {
                    foreach (var a in home)
                    {
                        LetGo(a);
                        a.SetFaction(Faction.OfPlayer);
                        if (!Arrivals.Spawn(a, map, rider.Position)) continue;
                        r.animals.Remove(a);
                        var follow = JobMaker.MakeJob(JobDefOf.FollowClose, rider);
                        follow.followRadius = 4f;
                        follow.expiryInterval = 3 * GenDate.TicksPerHour;
                        follow.checkOverrideOnExpire = false;
                        a.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                    }
                    PeoplesG.SayLine(rider, v.incident, "back");
                }
                else if (rider != null) Arrivals.Discard(rider);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the riders back", e);
            }
            if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.AloneGoodwill, false);
            string text = home.Count == 0
                ? "CC_PEv3_TrailNone".Translate(f.Name.Named("FACTION"), (due != null ? due.LabelIndefinite() : "").Named("DUE"), (due != null).Named("KEPT")).Resolve()
                : "CC_PEv3_TrailBack".Translate(f.Name.Named("FACTION"), Kinds(home).Named("BEASTS"), (due != null ? due.LabelIndefinite() : "").Named("DUE"), (due != null).Named("KEPT")).Resolve();
            Tell.Message(NewsKind.World, Importance.Notable, map, text, home.Count > 0 ? new LookTargets(home) : LookTargets.Invalid,
                home.Count > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            Close(r);
            return false;
        }

        // ---------------- With the colony's rider ----------------

        /// <summary>
        /// The colony's rider goes with them: the thieves' camp on the world map a tile or two off (the game's own bandit camp
        /// of the thieves, a share of the colony's threat points), the rider walking (or riding) to the edge to set out for it,
        /// the herders' riders gone ahead to wait there.
        /// </summary>
        public static bool Ride(HappeningContext ctx)
        {
            var r = Thefts?.FirstOrDefault(x => x.id.ToString() == ctx?.note);
            var t = CouncilGameComponent.Instance?.happenings;
            var a = ctx?.a;
            var map = ctx?.map;
            var job = RideOutJob;
            if (r == null || t == null || a == null || map == null || job == null || r.thieves == null || !a.Spawned) return false;
            Site site;
            try
            {
                if (!TileFinder.TryFindNewSiteTile(out var tile, map.Tile, PeopleEvents3Math.TrailTilesMin, PeopleEvents3Math.TrailTilesMax, false, null, 0f) || SitePartDefOf.BanditCamp == null)
                    return false;
                site = SiteMaker.MakeSite(SitePartDefOf.BanditCamp, tile, r.thieves, true, StorytellerUtility.DefaultThreatPointsNow(map) * PeopleEvents3Math.ThiefPoints);
                if (site == null) return false;
                site.customLabel = "CC_PEv3_ThievesCamp".Translate(r.thieves.Name.Named("FACTION")).Resolve();
                site.GetComponent<TimeoutComp>()?.StartTimeout(PeopleEvents3Math.CampDays * GenDate.TicksPerDay);
                Find.WorldObjects.Add(site);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the thieves' camp", e);
                return false;
            }
            r.stage = "ride";
            r.site = site.ID;
            r.herders = ctx.faction;
            var v = t.AddVisit(RideKind, ctx.incident, map, a, null, null, Now + (PeopleEvents3Math.CampDays + 4) * GenDate.TicksPerDay);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + r.id;
            v.stake = 0;
            try
            {
                Hosting.Provision(a, 3f);
                if (a.Drafted) a.drafter.Drafted = false;
                var mount = GiddyUpMounts.On ? OwnRiders.MountFor(map, a) : null;
                if (mount != null) GiddyUpMounts.MountNow(a, mount);
                if (!RCellFinder.TryFindBestExitSpot(a, out var edge, TraverseMode.ByPawn)) return true;
                var walk = JobMaker.MakeJob(job, edge);
                walk.locomotionUrgency = Pace.Errand;
                a.jobs.StartJob(walk, JobCondition.InterruptForced, null, false, true);
                if (mount != null) v.a = mount;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: riding out", e);
            }
            RideOff(PeoplesG.Party(ctx));
            Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_PEv3_CampFoundLabel".Translate().Resolve(),
                "CC_PEv3_CampFound".Translate(a.Named("PAWN"), r.thieves.Name.Named("FACTION"), ctx.faction.Name.Named("HERDERS"), Kinds(Away(r)).Named("BEASTS")).Resolve(),
                LetterDefOf.NeutralEvent, new LookTargets(site), ctx.faction);
            return true;
        }

        /// <summary>At the map's edge (JobDriver_RideOut): a caravan of one on the road to the thieves' camp, to go in when it gets there.</summary>
        public static void AtEdge(Pawn p)
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == RideKind && !x.done && x.x == p && x.stake == 0);
            var r = RecordOf(v);
            var site = r == null ? null : Find.WorldObjects.Sites.FirstOrDefault(s => s.ID == r.site);
            if (v == null || site == null || !p.Spawned) return;
            try
            {
                var pawns = new List<Pawn> { p };
                var mount = v.a;
                if (mount != null && mount.Spawned && !mount.Dead && !mount.Downed && mount.Map == p.Map && mount.Position.InHorDistOf(p.Position, 15f)) pawns.Add(mount);
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(pawns, Faction.OfPlayer, p.Map.Tile, p.Map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(site.Tile, new CaravanArrivalAction_VisitSite(site), true);
                v.stake = 1;
                Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PEv3_RodeOut".Translate(p.Named("PAWN"), site.Label.Named("PLACE")).Resolve(),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the caravan to the thieves", e);
            }
        }

        /// <summary>The rider's visit: walking out (given other work, they stayed), then on the road; over once the camp is.</summary>
        public static bool RideHour(Visit v, int now)
        {
            if (v.done) return false;
            var r = RecordOf(v);
            var p = v.x;
            if (r == null || !r.Open || p == null || p.Dead)
            {
                v.done = true;
                return false;
            }
            if (v.stake == 0 && p.Spawned && p.CurJobDef != RideOutJob)
            {
                Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PEv3_RiderStayed".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                v.done = true;
            }
            return !v.done;
        }

        /// <summary>The thieves' camp on the trail: still standing, gone cold, or won (no threat left on its map).</summary>
        private static void Ride(TheftRecord r, int now)
        {
            var site = Find.WorldObjects.Sites.FirstOrDefault(s => s.ID == r.site);
            if (site == null)
            {
                // Gone: cold before anyone went in, or the colony has left it (what it took home is its own).
                if (r.animals.Any(a => a != null && !a.Dead && !a.Spawned))
                    Tell.Message(NewsKind.World, Importance.Notable, r.map, "CC_PEv3_TrailCold".Translate((r.herders?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid,
                        MessageTypeDefOf.NeutralEvent);
                Close(r);
                return;
            }
            var map = site.Map;
            if (map == null || GenHostility.AnyHostileActiveThreatToPlayer(map, true)) return;
            // Won: the beasts that lived are the colony's to take home with the caravan, and the riders go home.
            var ours = r.animals.Where(a => a != null && !a.Dead && a.Spawned && a.Map == map).ToList();
            var riders = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == r.herders && p.Faction != null && !p.Dead).ToList();
            RideOff(riders);
            var f = r.herders;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.TrailGoodwill, false);
            if (f != null) FactionMemory.Note(f, "foughtBeside", "CC_PEv3_TrailDeed".Translate().Resolve());
            Tell.Message(NewsKind.World, Importance.Important, map,
                (ours.Count > 0 ? "CC_PEv3_TrailWon" : "CC_PEv3_TrailWonNone").Translate((f?.Name ?? "?").Named("FACTION"), Kinds(ours).Named("BEASTS")).Resolve(),
                ours.Count > 0 ? new LookTargets(ours) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            r.animals.RemoveAll(a => ours.Contains(a));
            Close(r);
        }

        /// <summary>
        /// The thieves' camp's map made (the colony's caravan came to it): the herders' riders come in beside it to fight, and
        /// the colony's beasts stand tethered in the camp, the colony's again for the taking.
        /// </summary>
        public static void MapMade(Site site)
        {
            var r = Thefts?.FirstOrDefault(x => x.Open && x.stage == "ride" && x.site == site?.ID);
            var map = site?.Map;
            if (r == null || map == null) return;
            var hostiles = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == r.thieves && p.RaceProps.Humanlike).ToList();
            var middle = hostiles.Count > 0 ? hostiles[hostiles.Count / 2].Position : map.Center;
            foreach (var a in Away(r))
            {
                try
                {
                    a.SetFaction(Faction.OfPlayer);
                    if (!CellFinder.TryFindRandomCellNear(middle, map, 6, c => c.Standable(map) && c.GetEdifice(map) == null, out var cell)) cell = middle;
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(a);
                    if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemovePawn(a);
                    GenSpawn.Spawn(a, cell, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: the beasts in the thieves' camp", e);
                }
            }
            var f = r.herders;
            if (f == null || f.HostileTo(Faction.OfPlayer)) return;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && map.reachability.CanReach(c, middle, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)), map,
                    CellFinder.EdgeRoadChance_Ignore, out var edge)) return;
            var riders = new List<Pawn>();
            for (int i = 0; i < Rand.RangeInclusive(PeopleEvents3Math.RidersMin, PeopleEvents3Math.RidersMax); i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, i == 0 ? "Rimfolk_Herders_Warden" : "Rimfolk_Herders_Drover"), f, new FloatRange(18f, 50f));
                if (p == null || !Arrivals.Spawn(p, map, edge)) continue;
                riders.Add(p);
            }
            if (riders.Count == 0) return;
            var horses = Horses(f, riders.Count);
            for (int i = 0; i < horses.Count && i < riders.Count; i++)
            {
                GenSpawn.Spawn(horses[i], CellFinder.RandomClosewalkCellNear(riders[i].Position, map, 2), map);
                GiddyUpMounts.MountNow(riders[i], horses[i]);
            }
            LordMaker.MakeNewLord(f, new LordJob_AssistColony(f, edge), map, riders.Concat(horses).ToList());
            PeoplesG.SayLine(riders[0], DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "charge");
        }
    }

    /// <summary>The thieves' camp's map made: the herders' riders and the colony's beasts there (ThiefTrail.MapMade). Only adds them.</summary>
    [HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]
    public static class Patch_ThievesCampMapMade
    {
        public static void Postfix(Site __instance)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                ThiefTrail.MapMade(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the thieves' camp's map", e);
            }
        }
    }

    /// <summary>
    /// Herders of {FACTION} at the gate on the thieves' trail ({ENEMY} drove off {TALE}, {COUNT} head): send {A} with them, let
    /// them ride alone, or let the beasts go.
    /// </summary>
    public class HappeningWorker_ThiefTrail : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Herders) != null && ThiefTrail.Asked(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Herders);
            var r = ThiefTrail.Asked(ctx.map);
            if (ctx.faction == null || r == null) return false;
            var away = ThiefTrail.Away(r);
            ctx.enemy = r.thieves;
            ctx.count = away.Count;
            ctx.tale = ThiefTrail.Kinds(away);
            ctx.note = r.id.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Warden"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = Rand.RangeInclusive(PeopleEvents3Math.RidersMin, PeopleEvents3Math.RidersMax);
            for (int i = 1; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Herders_Drover"), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            ctx.group.AddRange(ThiefTrail.Horses(ctx.faction, n));
            // The colony's rider: its best fighter of those who could go a few days.
            ctx.a = PeoplesD.Able(ctx.map).Where(p => PeopleEvents2.MayLeave(p, ctx.map) && !p.WorkTagIsDisabled(WorkTags.Violent))
                .OrderByDescending(p => Mathf.Max(p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0) / 2)
                .FirstOrDefault() ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            r.stage = "asked";
            r.herders = ctx.faction;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var party = PeoplesG.Party(ctx);
            if (Arrivals.Visit(party, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) == null) return false;
            var riders = party.Where(p => p.RaceProps.Humanlike).ToList();
            var horses = party.Where(p => p.RaceProps.Animal).ToList();
            for (int i = 0; i < horses.Count && i < riders.Count; i++) GiddyUpMounts.MountNow(riders[i], horses[i]);
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            var r = PeopleEvents3Book.Instance?.thefts.FirstOrDefault(x => x.id.ToString() == ctx.note);
            if (r != null && r.stage == "asked") r.stage = "taken";
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.group.Any(p => p.RaceProps.Animal)) tags.Add("ride");
            if (ctx.count > 1) tags.Add("many");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option != 0) return null;
            if (ctx.a == null || !PeopleEvents2.MayLeave(ctx.a, ctx.map)) return "CC_PEv3_NoRider".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "together" : option == 1 ? "alone" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's rider goes with them (ThiefTrail.Ride): the thieves' camp on the world map, and the fight there by the game's own rules.</summary>
    public class DilemmaEffect_ThiefRide : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Play("CC_PEv3_Impact_Ride".Translate(hc.a.LabelShort, hc.enemy?.Name ?? "?").Resolve(), "CC_PEv3_Impact_RideBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_RideFight".Translate(hc.a.LabelShort).Resolve(), "CC_PEv3_Impact_RideFightBrief".Translate().Resolve(), "pev3ridefight");
            sheet.Add(ImpactKind.Goods, 1, "CC_PEv3_Impact_RideBeasts".Translate(hc.tale ?? "?").Resolve(), "CC_PEv3_Impact_RideBeastsBrief".Translate().Resolve(), "pev3ridebeasts");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.TrailGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (!ThiefTrail.Ride(hc)) ThiefTrail.Alone(hc);
        }
    }

    /// <summary>The herders ride alone (ThiefTrail.Alone): back with what they bring, one beast kept as their due.</summary>
    public class DilemmaEffect_ThiefAlone : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.Goods, 0, "CC_PEv3_Impact_Alone".Translate(Mathf.RoundToInt(PeopleEvents3Math.AloneBack * 100f)).Resolve(), "CC_PEv3_Impact_AloneBrief".Translate().Resolve(), "pev3alone");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.AloneGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => ThiefTrail.Alone(ctx as HappeningContext);
    }

    /// <summary>The colony lets the beasts go: the riders ride home.</summary>
    public class DilemmaEffect_ThiefLetGo : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc != null) sheet.Add(ImpactKind.Goods, -1, "CC_PEv3_Impact_LetGo".Translate(hc.tale ?? "?").Resolve(), "CC_PEv3_Impact_LetGoBrief".Translate().Resolve(), "pev3letgo");
        }

        public override void Apply(DilemmaContext ctx) => ThiefTrail.LetBeastsGo(ctx as HappeningContext);
    }

    /// <summary>The colony's rider walks (or rides) to the map's edge (target A) and sets out for the thieves' camp (ThiefTrail.AtEdge).</summary>
    public class JobDriver_RideOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("RideOut");
            go.initAction = () => ThiefTrail.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
