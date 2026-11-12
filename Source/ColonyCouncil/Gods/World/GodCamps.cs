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
    /// The gods among the peoples and their camps (docs/design/gods.md, stage 2, brainstorm item 18), each a camp kind with
    /// ways of its own (Camps/CampWorker.cs; Defs/GodDefs/Rimfolk_GodsWorld_Camps.xml), none on the camps' own draw:
    /// <list type="bullet">
    /// <item>Pilgrims at a place on the colony's land that one people calls holy (<see cref="CampWorker_GodPilgrims"/>): an
    /// old tree (Hollin's), a steam geyser (Smoulder's breath), a crash site or an old machine (Pip's), the colony's graves
    /// (Morel's), by their god's ways. They camp near it, walk to it at dawn and dusk to pray, and go; the colony may join
    /// them.</item>
    /// <item>A hermit seer at the map's edge (<see cref="CampWorker_GodSeer"/>): one person, a long stay, a true word now and
    /// then, said darkly (what is really under way, as a warning or a hint, never a god's mood), and a reading of the smoke
    /// for anyone who asks.</item>
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
                    case "geyser":
                    {
                        var g = map.listerThings.AllThings.FirstOrDefault(x => x.Spawned && x.def.defName == "SteamGeyser" && Clear(x.Position));
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
                case "geyser": return "CC_GW_Site_Geyser".Translate().Resolve();
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
            if (kind == null || !b.Gap("pilgrims", GodWorldMath.PilgrimGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.PilgrimDays, GodWorld.Lean()))) return;
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
                        foreach (var g in Gods.Of(x))
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
                GodWorld.Remember(god, map, GodActKind.Act,
                    GodWorld.T("CC_GW_Told_Pilgrims", god, f.Name.Named("PEOPLE"), SiteWords(site.what, site.thing).Named("SITE")), null, site.cell);
                return;
            }
        }

        // ================= A seer =================

        internal static void SeerComes(GodWorldBook b)
        {
            var kind = Seer;
            if (kind == null || !b.Gap("seer", GodWorldMath.SeerGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.SeerDays, GodWorld.Lean()))) return;
            var map = Homes.All.Where(m => CampTracker.RoomFor(m) && !CampTracker.VisitorsAt(m).Any(c => c.kind == kind)).RandomElementWithFallback();
            if (map == null) return;
            var f = Find.FactionManager.AllFactionsListForReading.Where(x => Arrivals.Usable(x) && Gods.Of(x).Any()).RandomElementWithFallback();
            if (f == null) return;
            if (CampTracker.Instance?.StartKind(map, kind, f) != null) b.Came("seer");
        }

        /// <summary>
        /// What the seer sees, all of it true and none of it plain (never a god's name or mood): a quarrel under way, a grudge
        /// against one of the colony (lightning or beasts), the Hunger owed, the shape of the anger of a god the colony stands
        /// badly with, the shape of the kindness of one it stands well with; else that nothing comes.
        /// </summary>
        public static List<(string words, God god)> Signs(Map map)
        {
            var signs = new List<(string words, God god)>();
            var b = GodWorld.Book;
            if (b == null || map == null) return signs;
            foreach (var d in b.Of("quarrel").Where(d => d.Map == map).Take(1)) signs.Add(("CC_GW_Seer_Quarrel".Translate().Resolve(), Gods.ById(d.god)));
            foreach (var d in b.Of("grudge").Where(d => d.pawn != null && !d.pawn.Dead && d.Map == map).Take(2))
                signs.Add((("CC_GW_Seer_Grudge_" + d.what).Translate(d.pawn.Named("PAWN")).Resolve(), Gods.ById(d.god)));
            foreach (var d in b.Of("take").Where(d => d.Map == map).Take(1)) signs.Add(("CC_GW_Seer_Hunger".Translate().Resolve(), Gods.ById(d.god)));
            foreach (var g in GodWorld.Known.Where(x => !GodWays.Blind(x)))
            {
                if (GodWays.Mirror(g))
                {
                    // The moon's return: what the day has been, as the seer reads it.
                    if (b.kindness > b.cruelty) signs.Add(("CC_GW_Seer_Bless_Rest".Translate().Resolve(), g));
                    else if (b.cruelty > b.kindness) signs.Add(("CC_GW_Seer_Wrath_Nightmares".Translate().Resolve(), g));
                    continue;
                }
                var w = Gods.Favour(g, map);
                var wrath = GodWays.Wrath(g);
                var kind = GodWays.Blessing(g);
                if (w <= FavourWord.Displeased && wrath != GodWrath.None) signs.Add((("CC_GW_Seer_Wrath_" + wrath).Translate().Resolve(), g));
                else if (w >= FavourWord.Pleased && kind != GodBlessing.None) signs.Add((("CC_GW_Seer_Bless_" + kind).Translate().Resolve(), g));
            }
            return signs.Where(x => x.god != null).GroupBy(x => x.words).Select(x => x.First()).ToList();
        }

        /// <summary>One true word of the seer's (and the god it was about, kept for the colony's book), or that nothing is coming.</summary>
        public static (string words, God god) Foretell(Map map)
        {
            var signs = Signs(map);
            return signs.Count > 0 ? signs.RandomElement() : ("CC_GW_Seer_Nothing".Translate().Resolve(), null);
        }

        /// <summary>The smoke read for one who asks: up to three true words, one to a line; each kept for the colony's book with the seer's name.</summary>
        public static string Reading(Map map, Pawn seer, string asker)
        {
            var signs = Signs(map).InRandomOrder().Take(3).ToList();
            foreach (var x in signs)
                GodWorld.Remember(x.god, map, GodActKind.Omen,
                    "CC_GW_Told_Reading".Translate((seer?.LabelShort ?? "?").Named("SEER"), (asker ?? "?").Named("PAWN"), x.words.Named("WORDS")).Resolve());
            return signs.Count == 0 ? "CC_GW_Seer_Nothing".Translate().Resolve() : string.Join("\n", signs.Select(x => x.words));
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
                GodWorld.Remark(c.map, st.cell, "desecrated", g);
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

    /// <summary>The colony joins the pilgrims' prayers at the holy place (with the gathering at their camp); their god notices, which nothing says.</summary>
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
    /// A hermit seer at the map's edge (GodCamps): one person and a hut, a long stay, a true word now and then, said darkly
    /// (GodCamps.Foretell: what is really under way), and the smoke read for anyone who goes to ask (DilemmaEffect_GodSeer,
    /// with the visit to their camp).
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
            var (word, god) = GodCamps.Foretell(c.map);
            if (word.NullOrEmpty()) return;
            var seer = c.Here.FirstOrDefault();
            if (seer != null) c.SayMoment(seer, "foretell");
            c.Note(c.Text("CC_GW_Seer_Says", seer) + " " + word, MessageTypeDefOf.NeutralEvent, Importance.Notable, seer);
            if (god != null) GodWorld.Remember(god, c.map, GodActKind.Omen, "CC_GW_Told_Seer".Translate((seer?.LabelShort ?? "?").Named("SEER"), word.Named("WORDS")).Resolve());
        }

    }

    /// <summary>The seer's reading for one who asks: up to three true words, said darkly, as a letter.</summary>
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
            string text = "CC_GW_Seer_ReadingText".Translate((ctx.a?.LabelShort ?? "?").Named("PAWN"), GodCamps.Reading(map, c?.Here.FirstOrDefault(), ctx.a?.LabelShort).Named("READINGS"))
                .Resolve();
            ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_GW_Seer_ReadingLabel".Translate().Resolve(), text, LetterDefOf.NeutralEvent,
                c?.Look ?? LookTargets.Invalid, c?.faction);
        }
    }
}
