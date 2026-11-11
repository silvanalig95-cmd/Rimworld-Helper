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
    /// <summary>
    /// The gods among the peoples and their camps (docs/design/gods.md, stage 2, brainstorm item 18), each a camp kind with
    /// ways of its own (Camps/CampWorker.cs; Defs/GodDefs/Rimfolk_GodsWorld_Camps.xml), none on the camps' own draw:
    /// <list type="bullet">
    /// <item>Pilgrims at a place on the colony's land that one people calls holy (<see cref="CampWorker_GodPilgrims"/>): an
    /// old tree, a river's bank or a spring, a crash site or an ancient machine, the colony's graves, an old ruin, by their
    /// god's domain. They camp near it, walk to it at dawn and dusk to pray, and go; the colony may join them.</item>
    /// <item>A hermit seer at the map's edge (<see cref="CampWorker_GodSeer"/>): one person, a long stay, a true word now and
    /// then about the gods and the colony (what is really under way), and a visit for anyone who asks.</item>
    /// <item>A war god's champion at the head of a raid (<see cref="Hourly"/>): the game's own raid, one of its fighters
    /// named the champion; their fall (dead, down or taken) is a sign.</item>
    /// </list>
    /// </summary>
    public static class GodCamps
    {
        public static CampKindDef Pilgrims => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_GodPilgrims");

        public static CampKindDef Seer => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_GodSeer");

        /// <summary>The holy place and god the camp being started now comes for (read by its worker as it pitches and starts).</summary>
        internal static GodDoing Pending;

        public static void Daily(GodWorldBook b, int now)
        {
            if (!CampTracker.Enabled) return;
            GodWorld.Safe("pilgrims", () => PilgrimsCome(b));
            GodWorld.Safe("a seer", () => SeerComes(b));
        }

        // ================= A holy place =================

        /// <summary>A place on the map of the kind a domain's people call holy (GodWorldMath.SiteKind), and the thing that is it (a tree, a wreck, a grave), or Invalid.</summary>
        public static IntVec3 FindSite(Map map, string kind, out Thing thing)
        {
            thing = null;
            if (map == null || kind == null) return IntVec3.Invalid;
            try
            {
                var home = map.areaManager.Home;
                bool Clear(IntVec3 c) => c.InBounds(map) && !c.Fogged(map) && (home == null || !home[c]);
                switch (kind)
                {
                    case "tree":
                    {
                        // The anima tree or the Gauranlen first, else the oldest grown tree well away from the colony.
                        var trees = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                            .Where(p => p.Spawned && p.def.plant != null && p.def.plant.IsTree && p.LifeStage == PlantLifeStage.Mature && Clear(p.Position)).ToList();
                        var t = trees.FirstOrDefault(p => p.def.defName == "Plant_TreeAnima" || p.def.defName == "Plant_TreeGauranlen") ?? trees.OrderByDescending(p => p.Age).FirstOrDefault();
                        thing = t;
                        return t?.Position ?? IntVec3.Invalid;
                    }
                    case "water":
                    {
                        // A river's bank first, else a lake's: a standable cell by the water.
                        foreach (bool moving in new[] { true, false })
                            foreach (var c in map.AllCells.Where(x => x.x % 3 == 0 && x.z % 3 == 0))
                            {
                                var t = c.GetTerrain(map);
                                if (t == null || !t.IsWater || moving != (t.defName.Contains("Moving") || t.defName.Contains("River")) || !Clear(c)) continue;
                                foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                                    if (n.InBounds(map) && n.Standable(map) && Clear(n))
                                        return n;
                            }
                        return IntVec3.Invalid;
                    }
                    case "wreck":
                    {
                        var w = map.listerThings.AllThings.FirstOrDefault(x => x.Spawned && Clear(x.Position) && IsWreck(x.def));
                        thing = w;
                        return w?.Position ?? IntVec3.Invalid;
                    }
                    case "grave":
                    {
                        var g = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building_Grave>().FirstOrDefault(x => x.Spawned && x.HasCorpse);
                        thing = g;
                        return g?.Position ?? IntVec3.Invalid;
                    }
                    case "stone":
                    {
                        var r = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
                            .FirstOrDefault(x => x.Spawned && x.Faction == null && x.def.defName.StartsWith("Ancient", StringComparison.Ordinal) && Clear(x.Position));
                        thing = r;
                        return r?.Position ?? IntVec3.Invalid;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a holy place found", e);
            }
            return IntVec3.Invalid;
        }

        /// <summary>A crash site or an ancient machine: a ship's chunk, a crashed part, an ancient terminal, tank or generator.</summary>
        public static bool IsWreck(ThingDef d)
        {
            if (d?.defName == null || d.category != ThingCategory.Building && d.category != ThingCategory.Item) return false;
            string n = d.defName;
            if (n.Contains("ShipChunk") || n.Contains("Crashed")) return true;
            return n.StartsWith("Ancient", StringComparison.Ordinal) && (n.Contains("Mech") || n.Contains("Terminal") || n.Contains("Tank") || n.Contains("Generator") || n.Contains("Machine")
                                                                       || n.Contains("Shuttle") || n.Contains("Reactor"));
        }

        /// <summary>The holy place in words: "the old oak", "the river's bank", "the wreck of a ship".</summary>
        public static string SiteWords(string kind, Thing t)
        {
            switch (kind)
            {
                case "tree": return "CC_GW_Site_Tree".Translate((t?.def?.label ?? "tree").Named("TREE")).Resolve();
                case "water": return "CC_GW_Site_Water".Translate().Resolve();
                case "wreck": return "CC_GW_Site_Wreck".Translate((t?.def?.label ?? "wreck").Named("WRECK")).Resolve();
                case "grave": return "CC_GW_Site_Grave".Translate().Resolve();
                default: return "CC_GW_Site_Stone".Translate().Resolve();
            }
        }

        /// <summary>The holy place one people calls on a home, kept with its god (one a home).</summary>
        public static GodDoing SiteAt(Map map) => GodWorld.Book?.Of("site").FirstOrDefault(d => d.home == Homes.KeyOf(map));

        /// <summary>Still there: its thing standing (a tree not cut, a grave not emptied), or its cell by the water.</summary>
        public static bool SiteStands(GodDoing s) =>
            s != null && s.cell.IsValid && (s.what == "water" || s.thing != null && !s.thing.Destroyed && s.thing.Spawned && (!(s.thing is Building_Grave g) || g.HasCorpse));

        // ================= Pilgrims =================

        internal static void PilgrimsCome(GodWorldBook b)
        {
            var kind = Pilgrims;
            if (kind == null || !b.Gap("pilgrims", GodWorldMath.PilgrimGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.PilgrimDays, GodWorld.Factor()))) return;
            foreach (var map in Homes.All.InRandomOrder().ToList())
            {
                if (!CampTracker.RoomFor(map) || CampTracker.VisitorsAt(map).Any(c => c.kind == kind)) continue;
                var site = SiteAt(map);
                if (site != null && !SiteStands(site))
                {
                    site.done = true;
                    site = null;
                }
                Faction f = null;
                God god = null;
                if (site != null)
                {
                    god = Gods.ById(site.god);
                    f = site.faction != null && Arrivals.Usable(site.faction) ? site.faction : god?.factions.FirstOrDefault(Arrivals.Usable);
                }
                else
                {
                    // A people whose god's domain has a holy place of its kind on this land.
                    foreach (var x in Find.FactionManager.AllFactionsListForReading.Where(Arrivals.Usable).InRandomOrder())
                    {
                        foreach (var g in Gods.Of(x).Where(g => g.kind != GodKind.Dead))
                        {
                            string sk = GodWays.Site(g);
                            var cell = FindSite(map, sk, out var thing);
                            if (!cell.IsValid) continue;
                            site = b.Add(new GodDoing { kind = "site", home = Homes.KeyOf(map), god = g.id, faction = x, what = sk, thing = thing, cell = cell });
                            f = x;
                            god = g;
                            break;
                        }
                        if (site != null) break;
                    }
                }
                if (site == null || f == null || god == null) continue;
                Pending = site;
                Camp c = null;
                try
                {
                    c = CampTracker.Instance?.StartKind(map, kind, f);
                }
                finally
                {
                    Pending = null;
                }
                if (c == null) continue;
                Gods.Learn(god, "visitors");
                b.Came("pilgrims");
                return;
            }
        }

        // ================= A seer =================

        internal static void SeerComes(GodWorldBook b)
        {
            var kind = Seer;
            if (kind == null || !b.Gap("seer", GodWorldMath.SeerGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.SeerDays, GodWorld.Factor()))) return;
            var map = Homes.All.Where(m => CampTracker.RoomFor(m) && !CampTracker.VisitorsAt(m).Any(c => c.kind == kind)).RandomElementWithFallback();
            if (map == null) return;
            var f = Find.FactionManager.AllFactionsListForReading.Where(x => Arrivals.Usable(x) && Gods.Of(x).Any(g => g.kind != GodKind.Dead)).RandomElementWithFallback();
            if (f == null) return;
            if (CampTracker.Instance?.StartKind(map, kind, f) != null) b.Came("seer");
        }

        /// <summary>
        /// What the seer says is true (never a guess): a quarrel under way in the sky, a god's grudge against one of the
        /// colony, the Hunger come to take, the Sleeper's door, the god angriest or kindest with the colony now, or its patron.
        /// </summary>
        public static string Foretell(Map map)
        {
            var b = GodWorld.Book;
            if (b == null || map == null) return null;
            var q = b.Of("quarrel").FirstOrDefault();
            if (q != null && Gods.ById(q.god) is God qa && Gods.ById(q.other) is God qb)
                return "CC_GW_Seer_Quarrel".Translate(qa.name.Named("A"), qb.name.Named("B")).Resolve();
            var grudge = b.Of("grudge").FirstOrDefault(d => d.pawn != null && !d.pawn.Dead && d.Map == map);
            if (grudge != null && Gods.ById(grudge.god) is God gg) return GodWorld.T("CC_GW_Seer_Grudge", gg, grudge.pawn.Named("PAWN"));
            var take = b.Of("take").FirstOrDefault();
            if (take != null && Gods.ById(take.god) is God hg) return GodWorld.T("CC_GW_Seer_Hunger", hg);
            if (b.sleeperStep > 0 && Gods.All.FirstOrDefault(x => x.kind == GodKind.Imprisoned) is God sg) return GodWorld.T("CC_GW_Seer_Sleeper", sg);
            var known = GodWorld.Known.Where(g => g.kind != GodKind.Dead).ToList();
            var angry = known.Where(g => Gods.Favour(g, map) <= FavourWord.Displeased).OrderBy(g => Gods.FavourValue(g, map)).FirstOrDefault();
            if (angry != null) return GodWorld.T("CC_GW_Seer_Angry", angry, GodWords.Word(Gods.Favour(angry, map)).Named("WORD"));
            var kind = known.Where(g => Gods.Favour(g, map) >= FavourWord.Favoured).OrderByDescending(g => Gods.FavourValue(g, map)).FirstOrDefault();
            if (kind != null) return GodWorld.T("CC_GW_Seer_Kind", kind);
            var patron = Gods.Patron(map);
            return patron != null ? GodWorld.T("CC_GW_Seer_Patron", patron, GodWords.Word(Gods.Favour(patron, map)).Named("WORD")) : "CC_GW_Seer_Nothing".Translate().Resolve();
        }

        /// <summary>The colony's standing with each god it knows here, in words, the angriest first: what the seer tells one who asks.</summary>
        public static string Reading(Map map)
        {
            var lines = GodWorld.Known.Where(g => g.kind != GodKind.Dead && Gods.Favour(g, map) != FavourWord.Unknown).OrderBy(g => Gods.FavourValue(g, map)).Take(6)
                .Select(g => "CC_GW_Seer_Line".Translate(Gods.FullName(g).Named("GOD"), GodWords.Word(Gods.Favour(g, map)).Named("WORD"), GodWords.NatureWord(g.nature).Named("NATURE")).Resolve())
                .ToList();
            return lines.Count == 0 ? "CC_GW_Seer_NoneKnown".Translate().Resolve() : string.Join("\n", lines);
        }

        // ================= A war god's champion =================

        /// <summary>The region's hour: a raid on a home led by a war god's champion now and then; a champion's fall is a sign.</summary>
        public static void Hourly(GodWorldBook b, int now)
        {
            foreach (var map in Homes.All.ToList())
            {
                if (map?.lordManager == null) continue;
                foreach (var lord in map.lordManager.lords.ToList())
                {
                    if (lord?.faction == null || lord.faction.IsPlayer || !lord.faction.HostileTo(Faction.OfPlayer) || b.Looked(lord.loadID)) continue;
                    if (!(lord.LordJob is LordJob_AssaultColony) && !(lord.LordJob is LordJob_StageThenAttack)) continue;
                    b.Look(lord.loadID);
                    Champion(b, map, lord);
                }
            }
            foreach (var d in b.Of("champion").ToList()) Watch(d);
            GodPeoples.Hour(b, now);
        }

        private static void Champion(GodWorldBook b, Map map, Lord lord)
        {
            var god = Gods.Of(lord.faction).FirstOrDefault(g => g.kind != GodKind.Dead && (g.domain == GodDomain.War || g.second == GodDomain.War));
            if (god == null || !b.Gap("champion", GodWorldMath.ChampionGap) || !GodWorld.Roll(GodWorldMath.ChampionShare * GodWorld.Factor())) return;
            var p = lord.ownedPawns.Where(x => x != null && x.Spawned && !x.Downed && x.RaceProps.Humanlike && !x.IsMutant).OrderByDescending(x => x.kindDef?.combatPower ?? 0f).FirstOrDefault();
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodChampion");
            if (p == null || def == null) return;
            try
            {
                if (!p.health.hediffSet.HasHediff(def)) p.health.AddHediff(def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a champion made", e);
                return;
            }
            b.Came("champion");
            Gods.Learn(god, "visitors");
            b.Add(new GodDoing { kind = "champion", god = god.id, home = Homes.KeyOf(map), pawn = p, faction = lord.faction, until = GodWorld.Now + 3 * GenDate.TicksPerDay });
            GodWorld.Moment(god, map, PresenceKind.Omen, p.Position, null, GodWorld.T("CC_GW_Champion_Label", god, p.Named("CHAMPION")),
                GodWorld.Voice(GodWorld.T("CC_GW_Champion_Text", god, p.Named("CHAMPION"), lord.faction.Name.Named("PEOPLE"))), true, LetterDefOf.ThreatSmall, new LookTargets(p),
                lord.faction, Importance.Important);
            GodWorld.Say(p, "champion", god);
        }

        /// <summary>The champion fallen (dead, down, taken): a sign. Fled or gone: nothing more.</summary>
        private static void Watch(GodDoing d)
        {
            var p = d.pawn;
            var map = d.Map;
            var god = Gods.ById(d.god);
            if (p == null || god == null || map == null)
            {
                d.done = true;
                return;
            }
            bool fell = p.Dead || p.Downed && p.MapHeld == map || p.IsPrisonerOfColony;
            if (!fell)
            {
                if (!p.Spawned || p.Map != map || GodWorld.Now > d.until) d.done = true;
                return;
            }
            d.done = true;
            var cell = p.PositionHeld;
            Gods.AddFavour(god, map, GodWorldMath.ChampionGodTakes(god.nature), GodWorld.T("CC_GW_Champion_WhyGod", god, p.Named("CHAMPION")), FavourSource.Deed);
            foreach (var r in Gods.Rivals(god).Where(Gods.Known))
                Gods.AddFavour(r, map, GodWorldMath.ChampionFell, GodWorld.T("CC_GW_Champion_WhyRival", r, p.Named("CHAMPION"), god.name.Named("OTHER")), FavourSource.Deed);
            foreach (var w in Gods.ColonyGods(map).Where(g => g != god && (g.domain == GodDomain.War || g.second == GodDomain.War) && !Gods.Rivals(god).Contains(g)))
                Gods.AddFavour(w, map, GodWorldMath.ChampionFell, GodWorld.T("CC_GW_Champion_WhyRival", w, p.Named("CHAMPION"), god.name.Named("OTHER")), FavourSource.Deed);
            string how = p.Dead ? "Dead" : p.IsPrisonerOfColony ? "Taken" : "Down";
            GodWorld.Moment(god, map, PresenceKind.Omen, cell, null, GodWorld.T("CC_GW_Champion_FellLabel", god, p.Named("CHAMPION")),
                GodWorld.Voice(GodWorld.T("CC_GW_Champion_Fell_" + how, god, p.Named("CHAMPION"))), true, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(cell, map)));
            GodWorld.Remark(map, cell, "champfell", god);
            GodWorld.Chronicle(GodWorld.T("CC_GW_Champion_Chronicle", god, p.Named("CHAMPION")), p);
        }
    }

    // ===================== Pilgrims at a holy place =====================

    public class GodPilgrimsState : CampWorkState
    {
        public int god = -1;
        public string site;
        public IntVec3 cell = IntVec3.Invalid;
        public Thing thing;
        public int prayedDay = -1;
        public bool desecrated, joined;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref site, "site");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref prayedDay, "prayedDay", -1);
            Scribe_Values.Look(ref desecrated, "desecrated");
            Scribe_Values.Look(ref joined, "joined");
        }
    }

    /// <summary>
    /// Pilgrims at a holy place on the colony's land (GodCamps): they pitch near the place their people call holy, walk to
    /// it at dawn and at dusk and pray there facing it, and go after a few days; their god is pleased that the colony let
    /// them. Should the place be destroyed while they are here (the tree felled, the grave emptied), they grieve and leave,
    /// and their god is displeased. {GOD}, {GODEPITHET}, {GODOF} and {SITE} in their words.
    /// </summary>
    public class CampWorker_GodPilgrims : CampWorker
    {
        public static GodPilgrimsState StateOf(Camp c) => c?.workState as GodPilgrimsState;

        private static JobDef Pray => DefDatabase<JobDef>.GetNamedSilentFail("Council_CampPray");

        public override bool FitsMap(Map map) => GodCamps.Pending != null || GodCamps.SiteAt(map) != null;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var s = GodCamps.Pending ?? GodCamps.SiteAt(map);
            if (s == null || !s.cell.IsValid) return usual;
            var c = CampLand.TryFindNear(map, s.cell, 6f, 20f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (c.workState is GodPilgrimsState) return;
            var s = GodCamps.Pending ?? GodCamps.SiteAt(c.map);
            c.workState = new GodPilgrimsState { god = s?.god ?? -1, site = s?.what, cell = s?.cell ?? IntVec3.Invalid, thing = s?.thing };
        }

        private static bool Stands(GodPilgrimsState st) => st.cell.IsValid && (st.site == "water" || st.thing != null && !st.thing.Destroyed && st.thing.Spawned);

        public override void Hourly(Camp c, int hour)
        {
            var st = StateOf(c);
            if (st == null)
            {
                Started(c);
                st = StateOf(c);
                if (st == null) return;
            }
            var g = Gods.ById(st.god);
            if (g == null || st.desecrated) return;
            if (!Stands(st))
            {
                st.desecrated = true;
                var lead = c.Here.FirstOrDefault();
                if (lead != null) c.SayMoment(lead, "grief");
                Gods.AddFavour(g, c.map, -GodWorldMath.PilgrimFavour * 2f, Fill(c, "CC_GW_Pilgrims_WhyGone".Translate().RawText), FavourSource.Sacrilege);
                c.Note(Fill(c, c.Text("CC_GW_Pilgrims_Gone")), MessageTypeDefOf.NegativeEvent, Importance.Notable);
                c.untilTick = Mathf.Min(c.untilTick, GodWorld.Now + 3 * GenDate.TicksPerHour);
                return;
            }
            // Dawn and dusk: someone says so, and they go to pray (their day job sends them).
            if ((hour == 6 || hour == 18) && st.prayedDay != GenDate.DaysPassed * 2 + (hour == 18 ? 1 : 0))
            {
                st.prayedDay = GenDate.DaysPassed * 2 + (hour == 18 ? 1 : 0);
                var p = c.Here.Where(x => x.Awake()).RandomElementWithFallback();
                if (p != null) c.SayMoment(p, "pray");
                foreach (var x in c.Here.Where(x => x.Awake() && !x.Downed).Take(4)) SendToPray(c, st, x);
            }
        }

        public override Job DayJob(Camp c, Pawn p)
        {
            var st = StateOf(c);
            if (st == null || !Stands(st) || !GodWorld.Roll(0.25f)) return null;
            return PrayJob(st, p);
        }

        private static Job PrayJob(GodPilgrimsState st, Pawn p)
        {
            var pray = Pray;
            var map = p?.Map;
            if (pray == null || map == null || !st.cell.IsValid) return null;
            if (!CellFinder.TryFindRandomCellNear(st.cell, map, 3, x => x.Standable(map) && x != st.cell && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var spot)) return null;
            return JobMaker.MakeJob(pray, spot, st.thing != null && st.thing.Spawned ? new LocalTargetInfo(st.thing) : new LocalTargetInfo(st.cell));
        }

        private static void SendToPray(Camp c, GodPilgrimsState st, Pawn p)
        {
            var job = PrayJob(st, p);
            if (job == null) return;
            try
            {
                p.jobs.StartJob(job, JobCondition.InterruptOptional, null, true, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: pilgrims sent to pray", e);
            }
        }

        public override void Leaving(Camp c)
        {
            var st = StateOf(c);
            var g = Gods.ById(st?.god ?? -1);
            if (st == null || g == null || st.desecrated || c.Hostile) return;
            Gods.AddFavour(g, c.map, GodWorldMath.PilgrimFavour, Fill(c, "CC_GW_Pilgrims_Why".Translate().RawText), FavourSource.Rite);
            GodWorld.Remember(g, c.map, GodActKind.Act, Fill(c, "CC_GW_Pilgrims_Act".Translate().RawText));
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if (st?.desecrated == true) marks.Add("desecrated");
            if (st?.joined == true) marks.Add("joined");
        }

        public override string Fill(Camp c, string text)
        {
            var st = StateOf(c);
            var g = Gods.ById(st?.god ?? -1) ?? (GodCamps.Pending != null ? Gods.ById(GodCamps.Pending.god) : null);
            if (text.NullOrEmpty()) return text;
            string site = GodCamps.SiteWords(st?.site ?? GodCamps.Pending?.what, st?.thing ?? GodCamps.Pending?.thing);
            return text.Replace("{SITE}", site).Replace("{GODEPITHET}", g?.epithet ?? "").Replace("{GODOF}", g != null ? Gods.GodOf(g) : "").Replace("{GOD}", g?.name ?? "their god");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = StateOf(c);
            return st == null || !Stands(st) || st.desecrated ? "CC_GW_Pilgrims_NoSite".Translate().Resolve() : null;
        }
    }

    /// <summary>The colony joins the pilgrims' prayers at the holy place: their god pleased with the colony too (with the gathering at their camp).</summary>
    public class DilemmaEffect_GodPilgrims : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Other, 1, "CC_GW_Pilgrims_Impact".Translate().Resolve(), "CC_GW_Pilgrims_ImpactBrief".Translate().Resolve(), "godpilgrims");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_GodPilgrims);
            var st = CampWorker_GodPilgrims.StateOf(c);
            var g = Gods.ById(st?.god ?? -1);
            if (st == null || g == null || st.joined) return;
            st.joined = true;
            Gods.AddFavour(g, c.map, GodWorldMath.PilgrimJoinedFavour, c.kind.Worker.Fill(c, "CC_GW_Pilgrims_WhyJoined".Translate().RawText), FavourSource.Rite);
        }
    }

    // ===================== A hermit seer =====================

    public class GodSeerState : CampWorkState
    {
        public int saidDay = -1, words;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref saidDay, "saidDay", -1);
            Scribe_Values.Look(ref words, "words");
        }
    }

    /// <summary>
    /// A hermit seer at the map's edge (GodCamps): one person and a hut, a long stay, a true word about the gods now and then
    /// (GodCamps.Foretell: what is really under way), and the colony's standing with each god it knows for anyone who goes
    /// to ask (DilemmaEffect_GodSeer, with the visit to their camp).
    /// </summary>
    public class CampWorker_GodSeer : CampWorker
    {
        public static GodSeerState StateOf(Camp c) => c?.workState as GodSeerState;

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var edge = LegendBeasts.EdgeToward(map, usual.IsValid ? usual : map.Center);
            var c = CampLand.TryFindNear(map, edge, 0f, 12f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is GodSeerState)) c.workState = new GodSeerState();
        }

        public override void Hourly(Camp c, int hour)
        {
            var st = StateOf(c);
            if (st == null)
            {
                Started(c);
                st = StateOf(c);
            }
            // At dusk, every few days, the seer says what they see (a true word: what is really under way).
            if (hour != 19 || st.saidDay >= 0 && GenDate.DaysPassed - st.saidDay < 4 || st.words >= 3) return;
            st.saidDay = GenDate.DaysPassed;
            st.words++;
            string word = GodCamps.Foretell(c.map);
            if (word.NullOrEmpty()) return;
            var seer = c.Here.FirstOrDefault();
            if (seer != null) c.SayMoment(seer, "foretell");
            c.Note(c.Text("CC_GW_Seer_Says", seer) + " " + word, MessageTypeDefOf.NeutralEvent, Importance.Notable, seer);
        }

        public override string Fill(Camp c, string text)
        {
            if (text.NullOrEmpty()) return text;
            var patron = Gods.Patron(c.map);
            return text.Replace("{GODEPITHET}", patron?.epithet ?? "").Replace("{GODOF}", patron != null ? Gods.GodOf(patron) : "").Replace("{GOD}", patron?.name ?? "the gods");
        }
    }

    /// <summary>The seer's reading for one who asks: the colony's standing with each god it knows, and what the seer sees coming, as a letter.</summary>
    public class DilemmaEffect_GodSeer : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Other, 1, "CC_GW_Seer_Impact".Translate().Resolve(), "CC_GW_Seer_ImpactBrief".Translate().Resolve(), "godseer");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_GodSeer);
            string text = "CC_GW_Seer_ReadingText".Translate((ctx.a?.LabelShort ?? "?").Named("PAWN"), GodCamps.Reading(map).Named("READINGS"),
                (GodCamps.Foretell(map) ?? "").Named("WORD")).Resolve();
            ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_GW_Seer_ReadingLabel".Translate().Resolve(), text, LetterDefOf.NeutralEvent,
                c?.Look ?? LookTargets.Invalid, c?.faction);
        }
    }
}
