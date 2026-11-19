using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 37. A grave-garden for the colony (the Quiet Bell) =====================

    /// <summary>
    /// A grave-garden for the colony (item 37): two sextons of the Quiet Bell work three days among the colony's graves, laying
    /// flagstones of the land's own stone between them and planting roses beside them, and set up a bell-post, a building of
    /// the colony's (or only the bell-post, in half a day). From then on, while the post stands, anyone at their leisure in its
    /// ground gets "quiet ground" (a mood), and a funeral held at one of its graves (the game's own with Ideology, or the
    /// Bell's) is a fuller rite: those who were there remember it so, and the post's bell is rung at its end. With Anomaly, a
    /// body lying in its ground does not rise (the game's own rule already keeps the buried from rising; with the deeper game
    /// hooks on, Patch_GardenQuiet). Their keep is six meals.
    /// </summary>
    public static class GraveGarden
    {
        public const string Kind = "pev:garden";
        public const string EventDef = "Rimfolk_Evt_GraveGarden";

        public static ThingDef PostDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_BellPost");

        private static ThoughtDef QuietGround => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_QuietGround");

        private static ThoughtDef FullerRite => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_FullerRite");

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The funerals under way in a garden, by their lord's or play's id: who of the colony is there.</summary>
        private static readonly Dictionary<string, (Map map, HashSet<Pawn> there)> funerals = new Dictionary<string, (Map, HashSet<Pawn>)>();

        public static void Forget() => funerals.Clear();

        /// <summary>The colony's graves on this map.</summary>
        public static List<Building_Grave> Graves(Map map) =>
            map?.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned).ToList() ?? new List<Building_Grave>();

        /// <summary>The colony's graves that lie together (the most of them within reach of one), and their middle.</summary>
        public static List<Building_Grave> Cluster(Map map, out IntVec3 middle)
        {
            middle = IntVec3.Invalid;
            var all = Graves(map);
            if (all.Count == 0) return all;
            var best = all.OrderByDescending(g => all.Count(o => o.Position.InHorDistOf(g.Position, PeopleEvents5Math.GardenGather))).First();
            var list = all.Where(o => o.Position.InHorDistOf(best.Position, PeopleEvents5Math.GardenGather)).ToList();
            middle = new IntVec3(Mathf.RoundToInt((float)list.Average(g => g.Position.x)), 0, Mathf.RoundToInt((float)list.Average(g => g.Position.z)));
            return list;
        }

        /// <summary>The colony's grave-garden here, standing (its bell-post up), or null.</summary>
        public static GardenRecord Of(Map map)
        {
            var r = PeopleEvents5Book.Instance?.GardenAt(map);
            return r != null && r.Stands ? r : null;
        }

        /// <summary>This cell lies in a standing grave-garden's ground.</summary>
        public static bool Covers(Map map, IntVec3 c)
        {
            var r = Of(map);
            return r != null && c.IsValid && c.InHorDistOf(r.post.Position, r.radius);
        }

        /// <summary>The flagstones of the land's own stone (Granite, Slate...), else granite's.</summary>
        public static TerrainDef Flagstone(Map map)
        {
            try
            {
                foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                {
                    var t = DefDatabase<TerrainDef>.GetNamedSilentFail("Flagstone" + rock.defName);
                    if (t != null) return t;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the land's stone", e);
            }
            return DefDatabase<TerrainDef>.GetNamedSilentFail("FlagstoneGranite");
        }

        private static bool Open(Map map, IntVec3 c) =>
            c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null && !(c.GetTerrain(map)?.IsWater ?? true)
            && c.GetZone(map) == null && !(c.GetPlant(map) is Plant p && (p.def.plant.IsTree || p.def.plant.harvestedThingDef != null && p.def.plant.Sowable));

        /// <summary>Where the bell-post goes: an open cell under the sky by the graves' middle.</summary>
        public static IntVec3 PostSite(Map map, IntVec3 middle)
        {
            if (map == null || !middle.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(middle, 6f, true))
                if (Open(map, c) && !c.Roofed(map) && !GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetEdifice(map) is Building_Grave))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>The work laid out: the post's place, the flagstones round the graves and the roses beside them, nearest the post first.</summary>
        public static GardenRecord Plan(Map map, Faction f, bool whole)
        {
            var graves = Cluster(map, out var middle);
            if (graves.Count < PeopleEvents5Math.GardenMinGraves) return null;
            var post = PostSite(map, middle);
            if (!post.IsValid) return null;
            var r = new GardenRecord { map = map, faction = f, center = middle, postCell = post, whole = whole, since = Now };
            r.radius = PeopleEvents5Math.GardenRadius(graves.Max(g => g.Position.DistanceTo(post)));
            if (!whole) return r;
            var stone = Flagstone(map);
            var around = graves.SelectMany(g => GenAdj.CellsAdjacent8Way(g)).Distinct().Where(c => c != post && Open(map, c)).OrderBy(c => c.DistanceToSquared(post)).ToList();
            if (stone != null)
                r.stones = around.Where(c => c.GetTerrain(map) != stone && !(c.GetTerrain(map)?.layerable ?? false)).Take(PeopleEvents5Math.MaxStones).ToList();
            var taken = new HashSet<IntVec3>(r.stones);
            foreach (var g in graves.OrderBy(g => g.Position.DistanceToSquared(post)))
            {
                if (r.roses.Count >= PeopleEvents5Math.MaxRoses) break;
                var c = GenAdj.CellsAdjacentCardinal(g).Where(x => x != post && !taken.Contains(x) && Open(map, x) && map.fertilityGrid.FertilityAt(x) >= 0.7f && !x.Roofed(map))
                    .OrderBy(x => x.DistanceToSquared(post)).Select(x => (IntVec3?)x).FirstOrDefault();
                if (c == null) continue;
                r.roses.Add(c.Value);
                taken.Add(c.Value);
            }
            // Roses go where flagstones would have: never both on one cell.
            r.stones.RemoveAll(taken.Contains);
            foreach (var c in r.roses) taken.Add(c);
            return r;
        }

        /// <summary>The sextons begin: the visit for their days at the graves, the record of the work.</summary>
        public static bool Begin(HappeningContext ctx, bool whole)
        {
            var map = ctx?.map;
            var b = PeopleEvents5Book.Instance;
            var t = CouncilGameComponent.Instance?.happenings;
            var r = Plan(map, ctx?.faction, whole);
            var sextons = PeoplesG.Party(ctx).Where(p => p.Spawned && !p.Dead && !p.Downed).ToList();
            if (b == null || t == null || r == null || sextons.Count == 0) return false;
            b.AddGarden(r);
            int hours = whole ? PeopleEvents5Math.GardenDays * 24 : PeopleEvents5Math.PostOnlyHours * 2;
            var v = t.AddVisit(Kind, ctx.incident, map, sextons[0], null, sextons.Skip(1).ToList(), Now + hours * GenDate.TicksPerHour);
            v.cell = r.postCell;
            v.text = whole ? "whole" : "post";
            Lords.Leave(sextons);
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, r.center, hours * GenDate.TicksPerHour), map, sextons);
            return true;
        }

        private static bool Working(Pawn p) => p?.CurJobDef != null && (p.CurJobDef == PeopleEvents5.StoneJob || p.CurJobDef == PeopleEvents5.RoseJob || p.CurJobDef == PeopleEvents5.PostJob);

        /// <summary>
        /// The visit's hour: by day, each sexton not at work given the next of it (the stones, the roses, the post last, or the
        /// post at once when it is all they came for); done when the post is up and the rest laid, or their days run out.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var r = PeopleEvents5Book.Instance?.GardenAt(v.map);
            var sextons = v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();
            if (r == null || sextons.Count == 0 || now > v.until)
            {
                Finish(v, r, sextons);
                return false;
            }
            bool stonesLeft = r.stones.Count > 0, rosesLeft = r.roses.Count > 0;
            if (r.Stands && !stonesLeft && !rosesLeft)
            {
                Finish(v, r, sextons);
                return false;
            }
            if (!PeopleEvents5Math.WorkHour(GenLocalDate.HourOfDay(v.map)) || v.map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            // Their last day (or the work done but the post): the post comes before anything left.
            bool postNow = !r.Stands && (!r.whole || !stonesLeft && !rosesLeft || v.until - now < 18 * GenDate.TicksPerHour);
            var taken = new HashSet<IntVec3>(sextons.Where(Working).SelectMany(p => (p.CurJob.targetQueueA ?? new List<LocalTargetInfo>()).Select(q => q.Cell).Prepend(p.CurJob.targetA.Cell)));
            foreach (var s in sextons.Where(p => !Working(p)))
            {
                if (postNow && !taken.Contains(r.postCell))
                {
                    if (PeopleEvents5.Send(s, PeopleEvents5.PostJob, r.postCell, PeopleEvents5Math.PostTicks)) taken.Add(r.postCell);
                    continue;
                }
                var stones = r.stones.Where(c => !taken.Contains(c)).Take(4).ToList();
                if (stones.Count > 0)
                {
                    if (PeopleEvents5.Send(s, PeopleEvents5.StoneJob, stones[0], PeopleEvents5Math.StoneTicks, stones.Skip(1).Select(c => new LocalTargetInfo(c)).ToList()))
                        foreach (var c in stones) taken.Add(c);
                    continue;
                }
                var roses = r.roses.Where(c => !taken.Contains(c)).Take(3).ToList();
                if (roses.Count > 0 && PeopleEvents5.Send(s, PeopleEvents5.RoseJob, roses[0], PeopleEvents5Math.RoseTicks, roses.Skip(1).Select(c => new LocalTargetInfo(c)).ToList()))
                    foreach (var c in roses) taken.Add(c);
            }
            return true;
        }

        /// <summary>A flagstone laid where it still can be.</summary>
        public static void Laid(Pawn by, IntVec3 c)
        {
            var map = by?.Map;
            var r = PeopleEvents5Book.Instance?.GardenAt(map);
            if (r == null || !r.stones.Remove(c)) return;
            var stone = Flagstone(map);
            if (stone == null || !c.InBounds(map) || c.GetEdifice(map) != null || (c.GetTerrain(map)?.IsWater ?? true)) return;
            map.terrainGrid.SetTerrain(c, stone);
            r.laid++;
            r.worked = true;
            if (Rand.Chance(0.15f)) PeoplesG.SayLine(by, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "stone");
        }

        /// <summary>A rose planted, in bloom, where it still can be (the grass there cleared).</summary>
        public static void Planted(Pawn by, IntVec3 c)
        {
            var map = by?.Map;
            var r = PeopleEvents5Book.Instance?.GardenAt(map);
            var rose = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rose");
            if (r == null || !r.roses.Remove(c) || rose == null || !c.InBounds(map) || c.GetEdifice(map) != null) return;
            if (c.GetPlant(map) is Plant old)
            {
                if (old.def.plant.IsTree || old.def == rose) return;
                old.Destroy();
            }
            if (!(GenSpawn.Spawn(rose, c, map) is Plant p)) return;
            p.Growth = 1f;
            r.planted++;
            r.worked = true;
        }

        /// <summary>The bell-post raised at its place, the colony's from now on, and rung once.</summary>
        public static void Raised(Pawn by, IntVec3 c)
        {
            var map = by?.Map;
            var r = PeopleEvents5Book.Instance?.GardenAt(map);
            var def = PostDef;
            if (r == null || def == null || r.Stands || map == null) return;
            if (!Open(map, c))
            {
                c = PostSite(map, r.center);
                if (!c.IsValid) return;
            }
            var post = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            post.SetFaction(Faction.OfPlayer);
            r.post = GenSpawn.Spawn(post, c, map);
            r.postCell = c;
            r.worked = true;
            FleckMaker.ThrowDustPuff(c, map, 1.2f);
            PeopleEvents5.Bell(map, c);
            PeoplesG.SayLine(by, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "post");
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv5_PostRaised".Translate(by.Named("PAWN")).Resolve(), new LookTargets(r.post), MessageTypeDefOf.PositiveEvent);
        }

        private static void Finish(Visit v, GardenRecord r, List<Pawn> sextons)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            if (r != null && r.Stands)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer))
                    WorldSituationWorker.Goodwill(f, r.whole ? PeopleEvents5Math.GardenGoodwill : PeopleEvents5Math.PostGoodwill, GoodwillWhy.Helped);
                if (sextons.Count > 0) PeoplesG.SayLine(sextons[0], v.incident, "done");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map,
                    (r.whole ? "CC_PEv5_GardenDone" : "CC_PEv5_PostDone").Translate((f?.Name ?? "?").Named("FACTION"), r.laid.Named("COUNT"), r.planted.Named("ROSES")).Resolve(),
                    new LookTargets(r.post), MessageTypeDefOf.PositiveEvent);
                r.stones.Clear();
                r.roses.Clear();
            }
            else if (r != null && v.map != null)
            {
                // Called away before the post was up: what they laid stays, but there is no garden.
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv5_GardenUnfinished".Translate((f?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
                PeopleEvents5Book.Instance?.gardens.Remove(r);
            }
            PeoplesG.Home(sextons);
        }

        // ---------------- The garden's days ----------------

        private static bool AtLeisure(Pawn p) => p?.CurJob?.def != null && (p.CurJob.def.joyKind != null || p.CurJob.def.defName == "VisitGrave");

        /// <summary>
        /// The homes' hour: anyone of the colony at their leisure in the garden's ground (a grave visited, a walk, a rest) gets
        /// "quiet ground"; a funeral at one of its graves (the game's ritual or the Bell's) is watched, and those who were
        /// there when it ends remember a fuller rite, the post rung.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var r = Of(map);
            if (r == null) return;
            var quiet = QuietGround;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (quiet == null || !p.Position.InHorDistOf(r.post.Position, r.radius) || !AtLeisure(p)) continue;
                using var _cause = Causes.Push("CC_PEv5_Cause_Garden".Translate().Resolve());
                CouncilGameComponent.GiveThought(p, quiet, null);
            }
            var alive = new HashSet<string>();
            foreach (var lord in map.lordManager.lords)
            {
                if (!(lord?.LordJob is LordJob_Ritual rit)) continue;
                string name = (rit.Ritual?.def?.defName ?? "") + "|" + (rit.Ritual?.sourcePattern?.defName ?? "");
                if (name.IndexOf("Funeral", StringComparison.OrdinalIgnoreCase) < 0 || !(rit.selectedTarget.Thing is Building_Grave g) || !Covers(map, g.Position)) continue;
                string key = "lord:" + lord.loadID;
                alive.Add(key);
                See(key, map, lord.ownedPawns);
            }
            var plays = PlayOutTracker.Instance?.plays;
            if (plays != null)
                foreach (var p in plays)
                {
                    if (p == null || p.map != map || p.state != PlayState.Running || !(p.place is Building_Grave g) || !Covers(map, g.Position)) continue;
                    string key = "play:" + p.id;
                    alive.Add(key);
                    See(key, map, map.mapPawns.FreeColonistsSpawned.Where(c => c.Position.InHorDistOf(g.Position, 8f)));
                }
            foreach (var key in funerals.Keys.ToList())
            {
                var (m, there) = funerals[key];
                if (m != map || alive.Contains(key)) continue;
                funerals.Remove(key);
                Rite(map, r, there.Where(p => p != null && !p.Dead).ToList());
            }
        }

        private static void See(string key, Map map, IEnumerable<Pawn> pawns)
        {
            if (!funerals.TryGetValue(key, out var s)) funerals[key] = s = (map, new HashSet<Pawn>());
            foreach (var p in pawns)
                if (p != null && p.IsColonist && p.RaceProps.Humanlike) s.there.Add(p);
        }

        /// <summary>A funeral at one of the garden's graves is over: a fuller rite for those who were there, the post rung.</summary>
        private static void Rite(Map map, GardenRecord r, List<Pawn> there)
        {
            if (there.Count == 0 || FullerRite == null) return;
            PeopleEvents5.Bell(map, r.post.Position);
            using (Causes.Push("CC_PEv5_Cause_Garden".Translate().Resolve()))
                // together: they stood at the grave for the rite that has just ended.
                foreach (var p in there)
                    CouncilGameComponent.GiveThought(p, FullerRite, null);
            Tell.Message(NewsKind.People, Importance.Flavor, map, "CC_PEv5_FullerRite".Translate().Resolve(), new LookTargets(r.post), MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// With Anomaly: a body lying in a standing grave-garden's ground does not rise (the game's death pall and the like ask
    /// MutantUtility.CanResurrectAsShambler first). With the deeper game hooks off, the game's own rule alone.
    /// </summary>
    [HarmonyPatch(typeof(MutantUtility), nameof(MutantUtility.CanResurrectAsShambler))]
    public static class Patch_GardenQuiet
    {
        public static void Postfix(Corpse corpse, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On || corpse == null || !corpse.Spawned) return;
            try
            {
                if (GraveGarden.Covers(corpse.Map, corpse.Position)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the garden's quiet", e);
            }
        }
    }

    /// <summary>Two sextons of {FACTION} ({X}) at the gate: they would make the colony's {COUNT} graves a grave-garden.</summary>
    public class HappeningWorker_GraveGarden : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Sextons) != null && GraveGarden.PostDef != null && GraveGarden.Of(map) == null
            && GraveGarden.Cluster(map, out var middle).Count >= PeopleEvents5Math.GardenMinGraves && GraveGarden.PostSite(map, middle).IsValid && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Sextons);
            if (ctx.faction == null) return false;
            var graves = GraveGarden.Cluster(ctx.map, out var middle);
            if (graves.Count < PeopleEvents5Math.GardenMinGraves) return false;
            ctx.count = graves.Count;
            ctx.spot = middle;
            ctx.place = graves.FirstOrDefault(g => g.Corpse != null) ?? graves[0];
            ctx.dead = graves.Where(g => g.Corpse?.InnerPawn?.Faction == Faction.OfPlayer).OrderByDescending(g => g.Corpse.timeOfDeath).Select(g => g.Corpse.InnerPawn.LabelShort).FirstOrDefault() ?? "";
            for (int i = 0; i < 2; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_QuietBell_Sexton"), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.dead.NullOrEmpty()) tags.Add("lost");
            if (ModsConfig.AnomalyActive) tags.Add("anomaly");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && GraveGarden.Plan(ctx.map, ctx.faction, option == 0) == null ? "CC_PEv5_NoGardenRoom".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "begin" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The sextons begin (GraveGarden.Begin): <see cref="whole"/> the stones, the roses and the bell-post over three days, else the bell-post alone.</summary>
    public class DilemmaEffect_GraveGarden : DilemmaEffect
    {
        public bool whole = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            sheet.Play((whole ? "CC_PEv5_Impact_Garden" : "CC_PEv5_Impact_Post").Translate().Resolve(), "CC_PEv5_Impact_GardenBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Mood, 1, "CC_PEv5_Impact_Quiet".Translate().Resolve(), "CC_PEv5_Impact_QuietBrief".Translate().Resolve(), "pev5quiet");
            if (hc.faction != null) sheet.Goodwill(hc.faction, whole ? PeopleEvents5Math.GardenGoodwill : PeopleEvents5Math.PostGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (!GraveGarden.Begin(hc, whole)) Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }
}
