using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A legend of the region (the player's pick "alphas and bosses", 7 November 2026), a world event
    /// (Defs/WorldSituationDefs/Rimfolk_WS_Legend.xml): one huge beast, of the dozen biggest animals loaded that could
    /// live in the region (a Megafauna titan, a Dinosauria giant, an Alpha Animals behemoth, the base game's thrumbo), or
    /// with PokéWorld a legendary Pokémon once PokéWorld itself has opened its legendaries in this game, roams the region
    /// for a month or more. The towns talk of it (the start letter, {PLACE}), a friendly town may pay hunters to bring it
    /// down (the bounty, paid by their courier when it falls), and its ground is a site on the world map 4 to 12 tiles from
    /// the home ({NAME}'s ground), where it moves on every five to nine days (a sighting, told) unless a caravan of the
    /// colony is on its way there. A caravan that goes finds it there, really: the site's map is made with the beast on it,
    /// old, scarred and legendary (the great beast's hediff), calm until provoked like any animal. It ends when it falls
    /// to the colony (a trophy taken from it beside the body, its hunter's name, and the song of its fall sung around a fire
    /// at home), when it is befriended (tamed, or caught by PokéWorld's own means), when something else brings it down, or
    /// when it moves on out of the region. Kept in the world while it lasts (the event holds it); a caravan that leaves its
    /// ground with it alive leaves it there, and its ground is marked again once it is up.
    /// </summary>
    public class LegendWorker : WorldSituationWorker
    {
        public const string NamePool = "Rimfolk_Pool_LegendNames";

        private static float Setting => ColonyCouncilMod.Settings?.regionLegends ?? 1f;

        private static Pawn Beast(WorldSituation s) => s.ctx.stranger;

        // ---------------- Which beast ----------------

        /// <summary>
        /// The kinds a legend can be here: of each race its first animal kind; the dozen biggest beasts loaded that fit
        /// (never an insect, a dryad or an entity), whose comfortable warmth is near the region's (ten degrees either
        /// way); and with PokéWorld its legendaries, once it allows them.
        /// </summary>
        public static List<PawnKindDef> Kinds(Map map)
        {
            var byRace = new Dictionary<ThingDef, PawnKindDef>();
            bool open = PokeWorldLegends.Open();
            foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
            {
                var rp = k?.race?.race;
                if (rp == null || byRace.ContainsKey(k.race)) continue;
                bool poke = PokeWorldLink.IsPokemon(k);
                if (!BeastsMath.LegendFits(rp.Animal, rp.Insect, rp.Dryad, rp.IsAnomalyEntity, rp.baseBodySize, poke, poke && PokeWorldLegends.IsLegendary(k.race), open)) continue;
                byRace[k.race] = k;
            }
            float temp = map != null && map.Tile.Valid ? Find.WorldGrid[map.Tile].temperature : 15f;
            bool Fits(PawnKindDef k)
            {
                try
                {
                    float min = k.race.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin) - 10f, max = k.race.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax) + 10f;
                    return temp >= min && temp <= max;
                }
                catch
                {
                    return false;
                }
            }
            var beasts = byRace.Values.Where(k => !PokeWorldLink.IsPokemon(k) && Fits(k)).OrderByDescending(k => k.RaceProps.baseBodySize).Take(BeastsMath.LegendPool);
            return beasts.Concat(byRace.Values.Where(PokeWorldLink.IsPokemon)).ToList();
        }

        protected override bool CanHappen(Map map)
        {
            var book = BeastBook.Instance;
            if (book == null || map == null || !map.Tile.Valid || MapFacts.InVacuum(map)) return false;
            if (!BeastsMath.LegendDue(Setting, Now, book.legendEnded)) return false;
            return Kinds(map).Count > 0;
        }

        /// <summary>Its ground: a tile of land with nothing else on it, between <paramref name="min"/> and <paramref name="max"/> tiles from <paramref name="from"/> and within reach of the home; hills and woods first.</summary>
        public static PlanetTile GroundTile(Map home, PlanetTile from, int min, int max)
        {
            if (home == null || !from.Valid) return PlanetTile.Invalid;
            try
            {
                var grid = Find.WorldGrid;
                var root = home.Tile;
                var tiles = new List<PlanetTile>();
                var weights = new List<float>();
                Action<PlanetTile, int> each = (t, _) =>
                {
                    float d = grid.ApproxDistanceInTiles(from, t);
                    if (d < min || d > max || grid.ApproxDistanceInTiles(root, t) > BeastsMath.LairMaxTiles + 2) return;
                    if (Find.WorldObjects.AnyWorldObjectAt(t) || !TileFinder.IsValidTileForNewSettlement(t, null, false)) return;
                    var info = grid[t];
                    bool hills = info.hilliness == Hilliness.SmallHills || info.hilliness == Hilliness.LargeHills || info.hilliness == Hilliness.Mountainous;
                    bool woods = (info.PrimaryBiome?.TreeDensity ?? 0f) >= 0.2f;
                    tiles.Add(t);
                    weights.Add(1f + (hills ? 1f : 0f) + (woods ? 1f : 0f));
                };
                from.Layer.Filler.FloodFill(from, t => !Find.World.Impassable(t) && grid.ApproxDistanceInTiles(from, t) <= max, each);
                if (tiles.Count == 0) return PlanetTile.Invalid;
                int i = Enumerable.Range(0, tiles.Count).RandomElementByWeight(k => weights[k]);
                return tiles[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("legend: its ground", e);
                return PlanetTile.Invalid;
            }
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var kind = Kinds(map).RandomElementByWeightWithFallback(k => BeastsMath.LegendWeight(k.RaceProps.baseBodySize, PokeWorldLink.IsPokemon(k)));
            if (kind == null) return false;
            var tile = GroundTile(map, map.Tile, BeastsMath.LairMinTiles, BeastsMath.LairMaxTiles);
            if (!tile.Valid) return false;
            bool poke = PokeWorldLink.IsPokemon(kind);
            Pawn x;
            try
            {
                float age = Mathf.Max(1f, kind.RaceProps.lifeExpectancy * Rand.Range(BeastsMath.AgeMin, BeastsMath.AgeMax));
                x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, tile, forceGenerateNewPawn: true,
                    fixedBiologicalAge: poke ? (float?)null : age, fixedChronologicalAge: poke ? (float?)null : age));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("legend: the beast", e);
                return false;
            }
            if (x == null) return false;
            string name = poke ? kind.LabelCap.ToString() : BeastBook.NameFrom(NamePool, s.ctx.a ?? BeastBook.Voice(map), x.RaceProps.predator ? "predator" : "grazer");
            if (name.NullOrEmpty())
            {
                Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
                return false;
            }
            if (!poke) x.Name = new NameSingle(name);
            if (CouncilDefOf.Council_Legendary != null) x.health.AddHediff(CouncilDefOf.Council_Legendary);
            s.ctx.stranger = x;
            s.Hold(x);
            s.ctx.name = name;
            s.ctx.beast = kind.label;
            s.ctx.thing = kind.race;
            s.SetNum("tile", tile.tileId);
            if (poke) s.flags.Add("pokemon");
            s.flags.Add(x.RaceProps.predator ? "predator" : "grazer");
            if (poke || x.GetStatValue(StatDefOf.Wildness) < 1f) s.flags.Add("tamable");
            // The town that talks of it most, and may pay to have it brought down: a friendly people's nearest town.
            var people = Candidates(new SituationFactionNeed { friendly = true }, null, map).OrderBy(f => Distance(map, NearestTown(f, map))).FirstOrDefault();
            if (people != null && NearestTown(people, map) is Settlement town)
            {
                s.ctx.faction = people;
                s.At(town, people.Name);
                s.flags.Add("town");
            }
            return true;
        }

        public override void Begin(WorldSituation s)
        {
            var book = BeastBook.Instance;
            if (book != null) book.legendStarted = Now;
            var map = s.ctx.map;
            if (map != null) MakeGround(s, new PlanetTile(s.Num("tile"), map.Tile.Layer));
            s.SetNum("moveAt", Now + Mathf.RoundToInt(Rand.Range(BeastsMath.MoveMinDays, BeastsMath.MoveMaxDays) * GenDate.TicksPerDay));
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var site = GroundOf(s);
            return site != null ? new LookTargets(site) : LookTargets.Invalid;
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.faction == null) tags.Add("nofaction");
            var x = Beast(s);
            if (x?.RaceProps != null && x.RaceProps.baseBodySize >= 5f) tags.Add("titan");
            if (!s.ctx.item.NullOrEmpty()) tags.Add("trophy");
        }

        // ---------------- Its ground ----------------

        /// <summary>The site of its ground on the world map, or null.</summary>
        public static Site GroundOf(WorldSituation s)
        {
            int id = s?.Num("site") ?? 0;
            if (id <= 0) return null;
            return Find.WorldObjects.Sites.FirstOrDefault(x => x.ID == id && !x.Destroyed);
        }

        /// <summary>The legend whose ground this site is (its event running), or null.</summary>
        public static WorldSituation OfSite(Site site)
        {
            if (site == null) return null;
            return WorldSituations.Instance?.Running.FirstOrDefault(s => s.Worker is LegendWorker && s.Num("site") == site.ID);
        }

        private static Site MakeGround(WorldSituation s, PlanetTile tile)
        {
            if (!tile.Valid || BeastsDefOf.Rimfolk_LegendGround == null) return null;
            try
            {
                if (Find.WorldObjects.AnyMapParentAt(tile)) return null;
                var site = SiteMaker.MakeSite(BeastsDefOf.Rimfolk_LegendGround, tile, null, false, 0f);
                if (site == null) return null;
                site.customLabel = "CC_Legend_SiteLabel".Translate((s.ctx.name ?? "?").Named("NAME")).Resolve();
                Find.WorldObjects.Add(site);
                s.SetNum("site", site.ID);
                s.SetNum("tile", tile.tileId);
                return site;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("legend: its ground's site", e);
                return null;
            }
        }

        /// <summary>A caravan of the colony on its way to (or standing on) its ground: it doesn't move on meanwhile.</summary>
        private static bool Awaited(Site site)
        {
            if (site == null) return false;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c != null && c.IsPlayerControlled && (c.Tile == site.Tile || (c.pather != null && c.pather.Moving && c.pather.Destination == site.Tile)))
                    return true;
            return false;
        }

        // ---------------- While it roams ----------------

        public override string Hourly(WorldSituation s)
        {
            var x = Beast(s);
            if (x == null) return WorldSituations.Quietly;
            if (x.Discarded || (x.Destroyed && !x.Dead)) return "gone";
            if (x.Dead)
            {
                var slayer = BeastBook.Slayer(x);
                if (slayer != null && slayer.IsColonist)
                {
                    s.ctx.b = slayer;
                    s.flags.Add("slayer");
                    s.ctx.count = s.Num("bounty");
                    s.ctx.item = TrophyLabel(x);
                    return "slain";
                }
                return "fell";
            }
            if (x.Faction == Faction.OfPlayer)
            {
                s.ctx.b = x.playerSettings?.Master ?? x.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) ?? s.ctx.b;
                return "befriended";
            }
            if (x.Faction != null) return "gone";
            // Left on its ground when the colony's caravan went home: its ground is marked again once it is up.
            if (!x.Spawned && !x.Downed && GroundOf(s) == null && s.ctx.map != null && Find.WorldPawns.Contains(x))
            {
                var map = s.ctx.map;
                var tile = new PlanetTile(s.Num("tile"), map.Tile.Layer);
                if (!tile.Valid || Find.WorldObjects.AnyMapParentAt(tile)) tile = GroundTile(map, tile.Valid ? tile : map.Tile, 1, BeastsMath.MoveMaxTiles);
                var site = MakeGround(s, tile);
                if (site != null)
                    Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Legend_StillOut".Translate((s.ctx.name ?? "?").Named("NAME")).Resolve(), new LookTargets(site), MessageTypeDefOf.NeutralEvent);
            }
            return null;
        }

        /// <summary>The day: every five to nine days it moves on to new ground nearby (unless a caravan is on its way), and it is seen there.</summary>
        public override string Daily(WorldSituation s)
        {
            var x = Beast(s);
            var map = s.ctx.map;
            if (x == null || x.Dead || x.Spawned || x.Downed || map == null || Now < s.Num("moveAt")) return null;
            s.SetNum("moveAt", Now + Mathf.RoundToInt(Rand.Range(BeastsMath.MoveMinDays, BeastsMath.MoveMaxDays) * GenDate.TicksPerDay));
            var site = GroundOf(s);
            if (site == null || site.HasMap || Awaited(site)) return null;
            var to = GroundTile(map, site.Tile, BeastsMath.MoveMinTiles, BeastsMath.MoveMaxTiles);
            if (!to.Valid) return null;
            site.Destroy();
            var moved = MakeGround(s, to);
            if (moved == null) return null;
            s.AddNum("seen");
            string dir = Direction(map, to);
            string text = (s.ctx.faction != null ? "CC_Legend_Seen" : "CC_Legend_SeenRoad").Translate((s.ctx.name ?? "?").Named("NAME"), (s.ctx.placeName ?? "").Named("PLACE"),
                dir.Named("DIR"), (s.ctx.beast ?? "").Named("BEAST")).Resolve();
            Tell.Message(NewsKind.World, Importance.Notable, map, text, new LookTargets(moved), MessageTypeDefOf.NeutralEvent);
            return null;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat?.key == "bounty" && option == 0) s.SetNum("bounty", ctx.count);
            return outcome;
        }

        // ---------------- Its end ----------------

        /// <summary>What is taken from it as a trophy: its horns or tusks (what its body gives), else a hide of its leather, else nothing.</summary>
        private static string TrophyLabel(Pawn x)
        {
            var products = x.def.butcherProducts;
            if (!products.NullOrEmpty() && products[0]?.thingDef != null) return "CC_Legend_TrophyPart".Translate(products[0].thingDef.label.Named("THING")).Resolve();
            return x.def.race?.leatherDef != null ? "CC_Legend_TrophyHide".Translate().Resolve() : "";
        }

        private static void Trophy(Pawn x)
        {
            var corpse = x?.Corpse;
            if (corpse == null || !corpse.Spawned) return;
            var map = corpse.Map;
            var things = new List<Thing>();
            try
            {
                var products = x.def.butcherProducts;
                if (!products.NullOrEmpty())
                {
                    foreach (var p in products.Where(p => p?.thingDef != null))
                    {
                        var t = ThingMaker.MakeThing(p.thingDef);
                        t.stackCount = Mathf.Max(1, p.count);
                        things.Add(t);
                    }
                }
                else if (x.def.race?.leatherDef != null)
                {
                    var t = ThingMaker.MakeThing(x.def.race.leatherDef);
                    t.stackCount = Mathf.Clamp(Mathf.RoundToInt(x.GetStatValue(StatDefOf.LeatherAmount) * 0.25f), 10, 60);
                    things.Add(t);
                }
                foreach (var t in things) GenPlace.TryPlaceThing(t, corpse.Position, map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("legend: its trophy", e);
            }
        }

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            var book = BeastBook.Instance;
            if (book != null) book.legendEnded = Now;
            var site = GroundOf(s);
            if (site != null && !site.HasMap) site.Destroy();
            var comp = CouncilGameComponent.Instance;
            var b = s.ctx.b;
            if (ending?.key == "slain")
            {
                Trophy(Beast(s));
                if (b != null && b.IsColonist && comp != null && BeastsDefOf.Rimfolk_Epithet_GiantSlayer != null) comp.epithets.Bestow(BeastsDefOf.Rimfolk_Epithet_GiantSlayer, b, comp);
                if (b != null) comp?.journals.Write(b, "Rimfolk_Journal_LegendSlain", null, (s.ctx.name ?? "?").Named("NAME"));
            }
            else if (ending?.key == "befriended" && b != null && b.IsColonist && comp != null)
            {
                if (BeastsDefOf.Rimfolk_Epithet_BeastSpeaker != null) comp.epithets.Bestow(BeastsDefOf.Rimfolk_Epithet_BeastSpeaker, b, comp);
                comp.journals.Write(b, "Rimfolk_Journal_LegendBefriended", null, (s.ctx.name ?? "?").Named("NAME"));
            }
        }

        // ---------------- Dev mode ----------------

        /// <summary>The running legend, or null.</summary>
        public static WorldSituation Running() => WorldSituations.Instance?.Running.FirstOrDefault(s => s.Worker is LegendWorker);

        /// <summary>Dev mode: it moves on now (a sighting), as if its days had come.</summary>
        public static bool DebugMove()
        {
            var s = Running();
            if (s == null) return false;
            s.SetNum("moveAt", 0);
            return (s.Worker as LegendWorker)?.Daily(s) == null && GroundOf(s) != null;
        }
    }

    /// <summary>The ground of a legend of the region on the world map: its name, and on arrival where it is.</summary>
    public class SitePartWorker_LegendGround : SitePartWorker
    {
        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            var s = LegendWorker.OfSite(site);
            return s?.ctx.name ?? base.GetPostProcessedThreatLabel(site, sitePart);
        }

        public override string GetArrivedLetterPart(Map map, out LetterDef preferredLetterDef, out LookTargets lookTargets)
        {
            preferredLetterDef = LetterDefOf.NeutralEvent;
            var s = LegendWorker.OfSite(map?.Parent as Site);
            var x = s?.ctx.stranger;
            lookTargets = x != null && x.Spawned && x.Map == map ? new LookTargets(x) : new LookTargets(map?.Parent);
            if (s == null) return null;
            return (x != null && x.Spawned && x.Map == map ? "CC_Legend_Arrived" : "CC_Legend_ArrivedGone").Translate((s.ctx.name ?? "?").Named("NAME"), (s.ctx.beast ?? "").Named("BEAST")).Resolve();
        }
    }

    /// <summary>The legend on its ground: when a caravan's arrival makes the site's map, the beast is there, in the open, seen from the start.</summary>
    public class GenStep_LegendGround : GenStep
    {
        public override int SeedPart => 612884331;

        public override void Generate(Map map, GenStepParams parms)
        {
            var s = LegendWorker.OfSite(parms.sitePart?.site ?? map?.Parent as Site);
            var x = s?.ctx.stranger;
            if (x == null || x.Dead || x.Spawned || x.Faction != null) return;
            try
            {
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, Mathf.Min(map.Size.x, map.Size.z) / 3, c => c.Standable(map) && !c.Roofed(map) && c.GetFirstPawn(map) == null, out var cell)
                    && !CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Roofed(map), map, 1000, out cell))
                    return;
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
                GenSpawn.Spawn(x, cell, map);
                MapGenerator.rootsToUnfog.Add(cell);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("legend: on its ground", e);
            }
        }
    }

    /// <summary>
    /// PokéWorld's legendaries, read by name as the rest of its link (Integrations/PokeWorld.cs): a species is legendary when
    /// its Pokémon comp's attributes say so, and PokéWorld allows them in a game once its own legendary quests have opened
    /// (its LegendaryQuestsTracker has a condition met: the Pokédex far enough along). Nothing without PokéWorld.
    /// </summary>
    public static class PokeWorldLegends
    {
        private static HashSet<ThingDef> legendaries;

        public static bool IsLegendary(ThingDef race)
        {
            if (race == null || !PokeWorldLink.Active) return false;
            if (legendaries == null) Read();
            return legendaries.Contains(race);
        }

        private static void Read()
        {
            legendaries = new HashSet<ThingDef>();
            try
            {
                foreach (var race in PokeWorldLink.AllSpecies)
                {
                    if (race?.comps == null) continue;
                    foreach (var c in race.comps)
                    {
                        var f = c?.GetType().GetField("attributes", BindingFlags.Public | BindingFlags.Instance);
                        if (!(f?.GetValue(c) is IEnumerable list)) continue;
                        foreach (var a in list)
                            if (a != null && a.ToString() == "Legendary")
                                legendaries.Add(race);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: its legendaries", e);
            }
        }

        /// <summary>PokéWorld has opened its legendaries in this game: one of its legendary quests' conditions is met (read from its own tracker).</summary>
        public static bool Open()
        {
            if (!PokeWorldLink.Active || Find.World?.components == null) return false;
            try
            {
                foreach (var wc in Find.World.components)
                {
                    if (wc == null || wc.GetType().Name != "LegendaryQuestsTracker") continue;
                    var map = wc.GetType().GetField("questConditionDefMap", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(wc);
                    var values = map?.GetType().GetField("values", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(map) as IEnumerable;
                    if (values == null) return false;
                    foreach (var v in values)
                        if (v is bool b && b)
                            return true;
                    return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: its legendary quests", e);
            }
            return false;
        }
    }
}
