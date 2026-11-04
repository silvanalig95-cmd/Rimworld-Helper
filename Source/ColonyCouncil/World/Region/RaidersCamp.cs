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
    /// <summary>
    /// A raiders' camp near one of the colony's homes (docs/design/stage1-region.md, item 4): whose it is, where, how
    /// strong, how far along (hidden, found, being paid off), the game's own bandit camp site that stands for it once it
    /// is found, the raids it sent and how they went, the colony's fight on its map, its price, and who leads it. The
    /// rules are <see cref="RaidersCampMath"/>'s; <see cref="RaidersCamps"/> runs it.
    /// </summary>
    public class RaidersCamp : IExposable
    {
        public Faction people;
        /// <summary>Its tile as a ref (the history's place for it).</summary>
        public Ref tile;
        /// <summary>Its threat points: the game's own site threat points when set up, less those who fell when the colony attacked it.</summary>
        public float points;
        public int setAt = -1;
        public int foundAt = -1;
        public CampStage stage;
        /// <summary>The site's world object id while it stands on the world map, else -1.</summary>
        public int site = -1;
        /// <summary>Its name on the world map ("Brigands' hold") and its kind in a sentence ("brigands' hold"), both by its people's age.</summary>
        public string label, kind;

        public int raids, beaten, lastRaid = -1;

        /// <summary>A raid of theirs is on the home's map now: when it came, how many came, how many fell, whether they fled.</summary>
        public bool raidOpen;
        public int raidAt = -1, raidSize, raidDown;
        public bool raidFled;
        /// <summary>The raid's people while it lasts (not saved: after a load they are found again on the map).</summary>
        [Unsaved] internal List<Pawn> raiders = new List<Pawn>();

        /// <summary>The colony went in: how many fighters held the camp, how many still stood, and whether any threat was left when it last looked.</summary>
        public bool entered;
        public int fighters, standing = -1;
        public bool threatLeft = true;

        /// <summary>Their terms were offered, at this price in silver; the errand carrying it out to them (a play-out id), or -1.</summary>
        public bool offered;
        public int price;
        public int payPlay = -1;
        public int payAt = -1;

        /// <summary>When an ally last looked at burning it (every five days).</summary>
        public int allyAt = -1;
        /// <summary>Set up by a people who broke their word to keep away; set up over a grudge.</summary>
        public bool brokeWord, grudge;

        /// <summary>
        /// Who leads it and why (a key of CC_RaidCamp_Why_*), for a camp with a story of its own (the warlord's son of a
        /// later release): kept in the world while the camp stands, at the head of its raids, named in its words.
        /// </summary>
        public Pawn leader;
        public string leaderWhy;

        /// <summary>The colony's people who fought on its map (RaidersCampMath has no say in it): remembered by name when it is broken (World/ColonistMemory.cs).</summary>
        public List<Pawn> fought = new List<Pawn>();

        public bool Shown => stage != CampStage.Hidden;

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Refs.Look(ref tile, "tile");
            Scribe_Values.Look(ref points, "points");
            Scribe_Values.Look(ref setAt, "setAt", -1);
            Scribe_Values.Look(ref foundAt, "foundAt", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref site, "site", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref raids, "raids");
            Scribe_Values.Look(ref beaten, "beaten");
            Scribe_Values.Look(ref lastRaid, "lastRaid", -1);
            Scribe_Values.Look(ref raidOpen, "raidOpen");
            Scribe_Values.Look(ref raidAt, "raidAt", -1);
            Scribe_Values.Look(ref raidSize, "raidSize");
            Scribe_Values.Look(ref raidDown, "raidDown");
            Scribe_Values.Look(ref raidFled, "raidFled");
            Scribe_Values.Look(ref entered, "entered");
            Scribe_Values.Look(ref fighters, "fighters");
            Scribe_Values.Look(ref standing, "standing", -1);
            Scribe_Values.Look(ref threatLeft, "threatLeft", true);
            Scribe_Values.Look(ref offered, "offered");
            Scribe_Values.Look(ref price, "price");
            Scribe_Values.Look(ref payPlay, "payPlay", -1);
            Scribe_Values.Look(ref payAt, "payAt", -1);
            Scribe_Values.Look(ref allyAt, "allyAt", -1);
            Scribe_Values.Look(ref brokeWord, "brokeWord");
            Scribe_Values.Look(ref grudge, "grudge");
            Scribe_References.Look(ref leader, "leader");
            Scribe_Values.Look(ref leaderWhy, "leaderWhy");
            if (Scribe.mode == LoadSaveMode.Saving) fought?.RemoveAll(p => p == null || p.Discarded);
            Scribe_Collections.Look(ref fought, "fought", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                raiders ??= new List<Pawn>();
                fought ??= new List<Pawn>();
                fought.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// One home's part in the raiders' camps (kept on its <see cref="RegionHome"/>): the camp standing near it, when the
    /// home was last looked at and when its last camp ended, how many it has seen, and the word of the last people paid
    /// to keep away (a Promise tie on the world ledger).
    /// </summary>
    public class RaidersCampHome : IExposable
    {
        public RaidersCamp camp;
        public int lookedAt = -1;
        public int endedAt = -1;
        public int camps;
        /// <summary>The people last paid off, when, whether they mean to break their word, and the ledger's tie for it.</summary>
        public Faction paid;
        public int paidAt = -1;
        public bool breaks;
        public int promise = -1;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref camp, "camp");
            Scribe_Values.Look(ref lookedAt, "lookedAt", -1);
            Scribe_Values.Look(ref endedAt, "endedAt", -1);
            Scribe_Values.Look(ref camps, "camps");
            Scribe_References.Look(ref paid, "paid");
            Scribe_Values.Look(ref paidAt, "paidAt", -1);
            Scribe_Values.Look(ref breaks, "breaks");
            Scribe_Values.Look(ref promise, "promise", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && camp != null && camp.people == null) camp = null;
        }
    }

    /// <summary>
    /// Raiders' camps (docs/design/stage1-region.md, item 4; decisions 7 and 12). A rich colony on bad terms with a people
    /// that can raid it may find raiders camped a few days off: the game's own bandit camp site (Core's BanditCamp part,
    /// their fighters behind their walls), hidden until a colonist out beyond the walls sees its smoke, whose letter says
    /// what it means before any raid comes. While it stands it raids the home it threatens from its side of the map at the
    /// game's raid-source rate (QuestTuning's curve, at 0.6 of the home's threat points), on top of the storyteller's, by
    /// the rate setting. It ends when the colony attacks it and clears it (left with fighters standing, the game removes
    /// the site anyway and it is put back, fewer), when the colony pays their price (carried out to them, and their word
    /// for a year), when peace is made with their people, when an ally burns it, or when their raids fail or two months
    /// pass and they give up. Each ending is told, written into the history (world:camp) and into what the peoples
    /// remember. It runs in the region's two passes (<see cref="Pass.Region"/>, <see cref="Pass.DailyRegion"/>), nothing per tick.
    /// </summary>
    public static partial class RaidersCamps
    {
        /// <summary>Why the ledger keeps a camp's leader in the world.</summary>
        public const string KeepReason = "raidcamp";
        /// <summary>The origin of the ledger's Promise ties made here.</summary>
        public const string Origin = "raidcamp";

        private static int Now => Find.TickManager?.TicksGame ?? 0;
        private const int Day = RaidersCampMath.DayTicks;

        /// <summary>The rate setting (0 to 2): how likely a camp is, and how often it raids.</summary>
        public static float Rate => RaidersCampMath.Rate(ColonyCouncilMod.Settings?.raidersCamps ?? 1f);

        /// <summary>Raiders' camps are on: new ones may be set up, and those standing raid.</summary>
        public static bool On => Rate > 0f;

        /// <summary>The storyteller allows big threats at all.</summary>
        private static bool ThreatsAllowed => IncidentWorker_Happening.ScenarioFightsPossible;

        // ---------------- Reading ----------------

        /// <summary>A home's part (made when it has none); null for a map that isn't a home or without a game.</summary>
        public static RaidersCampHome Of(Map home)
        {
            if (home == null || !Homes.IsHome(home)) return null;
            var rh = RegionBook.Instance?.HomeOf(home);
            if (rh == null) return null;
            return rh.raiders ??= new RaidersCampHome();
        }

        /// <summary>The camp standing near a home (found or not), or null.</summary>
        public static RaidersCamp CampOf(Map home) => Of(home)?.camp;

        /// <summary>Each home with a camp the colony knows of (found or being paid off), in the homes' order.</summary>
        public static List<(Map home, RaidersCamp camp)> Known()
        {
            var list = new List<(Map, RaidersCamp)>();
            if (RegionBook.Instance == null) return list;
            foreach (var m in Homes.All)
            {
                var c = CampOf(m);
                if (c != null && c.Shown) list.Add((m, c));
            }
            return list;
        }

        /// <summary>The camp a site stands for, with its home; null when it is no camp of Rimfolk's.</summary>
        public static RaidersCamp CampAt(Site s, out Map home)
        {
            home = null;
            if (s == null || RegionBook.Instance == null) return null;
            foreach (var m in Homes.All)
            {
                var c = CampOf(m);
                if (c == null || c.site != s.ID) continue;
                home = m;
                return c;
            }
            return null;
        }

        /// <summary>The site standing for the camp on the world map, or null.</summary>
        public static Site SiteOf(RaidersCamp c)
        {
            if (c == null || c.site < 0 || Find.WorldObjects == null) return null;
            var sites = Find.WorldObjects.Sites;
            for (int i = 0; i < sites.Count; i++)
                if (sites[i] != null && sites[i].ID == c.site && !sites[i].Destroyed)
                    return sites[i];
            return null;
        }

        public static PlanetTile TileOf(RaidersCamp c)
        {
            var r = c?.tile ?? Ref.None;
            if (r.kind != RefKind.Tile || Find.WorldGrid == null) return PlanetTile.Invalid;
            try
            {
                return Find.WorldGrid.PlanetLayers.ContainsKey(r.TileLayer) ? new PlanetTile(r.TileId, r.TileLayer) : PlanetTile.Invalid;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its tile", e);
                return PlanetTile.Invalid;
            }
        }

        /// <summary>"two days' walk to the north", from the home.</summary>
        public static string Where(Map home, RaidersCamp c)
        {
            var t = TileOf(c);
            if (home == null || !t.Valid || t.Layer != home.Tile.Layer) return "CC_Region_Where_Near".Translate().Resolve();
            return RegionWords.Where(home, t, Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t));
        }

        /// <summary>"north": which way it lies from the home.</summary>
        public static string Direction(Map home, RaidersCamp c)
        {
            var t = TileOf(c);
            return home == null || !t.Valid ? "" : NeighbourLetters.Direction(home.Tile, t);
        }

        /// <summary>The camp's kind by its people's age, in a sentence: "war camp", "brigands' hold", "bandit camp", "raider base".</summary>
        public static string KindWord(Faction people) => RaidersCampMath.KindKey(Ages.Of(people)).Translate().Resolve();

        /// <summary>Its name on the world map: "Brigands' hold", or "Varn's brigands' hold" with a leader.</summary>
        public static string NameOf(string kind, Pawn leader) =>
            (leader != null ? "CC_RaidCamp_Label_Leader".Translate(leader.LabelShort.Named("LEADER"), kind.Named("KIND")).Resolve() : kind).CapitalizeFirst();

        // ---------------- The passes ----------------

        /// <summary>The region's hour: each camp found, watched, raiding, paid off or put back after an attack.</summary>
        internal static void Hourly(RegionBook book, int now)
        {
            if (book == null) return;
            foreach (var home in Homes.All.ToList())
            {
                var rh = Of(home);
                if (rh?.camp == null) continue;
                try
                {
                    Hour(home, rh, rh.camp, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("raiders' camp: the hour", e);
                }
            }
        }

        /// <summary>The region's day: a camp gives up, an ally burns it, a year's word is kept, a home looked at for a new camp.</summary>
        internal static void Daily(RegionBook book, int now)
        {
            if (book == null) return;
            foreach (var home in Homes.All.ToList())
            {
                var rh = Of(home);
                if (rh == null) continue;
                try
                {
                    KeepWord(rh, now);
                    if (rh.camp != null) Day_(home, rh, rh.camp, now);
                    else Look(home, rh, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("raiders' camp: the day", e);
                }
            }
        }

        /// <summary>A home was lost (Homes.Changed, through the colonies' book): its camp has nothing left to raid and is gone.</summary>
        internal static void HomeLost(RegionHome rh)
        {
            var c = rh?.raiders?.camp;
            if (c == null) return;
            try
            {
                End(null, rh.raiders, c, CampEnd.Gone, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: a home lost", e);
            }
        }

        private static void Hour(Map home, RaidersCampHome rh, RaidersCamp c, int now)
        {
            var player = Faction.OfPlayer;
            if (c.people == null || c.people.defeated)
            {
                End(home, rh, c, CampEnd.Gone, null);
                return;
            }
            KeepLeader(c);
            var site = SiteOf(c);
            // The colony is fighting on its map: watched, and nothing else happens until the colony leaves.
            if (site != null && site.HasMap)
            {
                Observe(c, site.Map);
                if (c.raidOpen) Track(home, c, now);
                return;
            }
            if (!c.people.HostileTo(player))
            {
                End(home, rh, c, CampEnd.Peace, null);
                return;
            }
            if (c.stage != CampStage.Hidden && site == null)
            {
                // The game removed the site: the colony left its map, cleared or not.
                if (c.entered)
                {
                    if (RaidersCampMath.Broken(c.threatLeft, c.fighters, c.standing)) End(home, rh, c, CampEnd.Broken, null);
                    else PutBack(home, c, now);
                }
                else End(home, rh, c, CampEnd.Gone, null);
                return;
            }
            if (c.raidOpen) Track(home, c, now);
            switch (c.stage)
            {
                case CampStage.Hidden:
                    // Switched off, a camp not yet seen stays unseen (its letter would promise raids that never come) and goes in time.
                    if (On) TryFind(home, rh, c, now);
                    return;
                case CampStage.Paying:
                    if (PaidUp(c)) End(home, rh, c, CampEnd.Paid, null);
                    return;
            }
            if (RaidersCampMath.GivesUp(c.beaten, c.setAt, now) && !c.raidOpen)
            {
                End(home, rh, c, CampEnd.GaveUp, null);
                return;
            }
            if (!c.raidOpen && On && ThreatsAllowed && RaidersCampMath.MayRaid(c.stage, c.setAt, c.foundAt, c.lastRaid, now)
                && Rand.MTBEventOccurs(RaidersCampMath.RaidMtb(CurveDays(c.points), Rate), Day, RaidersCampMath.HourTicks))
                TryRaid(home, c, now);
            if (RaidersCampMath.TermsDue(c.stage, c.foundAt, c.offered, now) && !c.raidOpen && Tell.CanAsk && !MapFacts.ThreatOn(home))
                RaidersCampTerms.Offer(home, c);
        }

        private static void Day_(Map home, RaidersCampHome rh, RaidersCamp c, int now)
        {
            var site = SiteOf(c);
            if (site != null && site.HasMap || c.stage == CampStage.Paying || c.raidOpen) return;
            // Never seen in two months (nobody went out of doors): gone without a word.
            if (c.stage == CampStage.Hidden)
            {
                if (RaidersCampMath.GivesUp(0, c.setAt, now)) End(home, rh, c, CampEnd.Gone, null);
                // Found another way: a visitor who came past it, or a neighbour's letter (RaidersCampWarnings.cs); not while switched off.
                else if (On) TryWarn(home, c, now);
                return;
            }
            if (RaidersCampMath.GivesUp(c.beaten, c.setAt, now))
            {
                End(home, rh, c, CampEnd.GaveUp, null);
                return;
            }
            if (!RaidersCampMath.AllyDue(c.allyAt, now)) return;
            c.allyAt = now;
            var ally = AllyNear(c);
            if (ally != null && Rand.Chance(RaidersCampMath.AllyChance)) End(home, rh, c, CampEnd.Burned, ally);
        }

        /// <summary>An ally of the colony at war with the camp's people, with a town within RaidersCampMath.AllyTiles of it (the nearest such).</summary>
        private static Faction AllyNear(RaidersCamp c)
        {
            var t = TileOf(c);
            if (!t.Valid) return null;
            Faction best = null;
            float bestTiles = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                var f = s?.Faction;
                if (f == null || f.IsPlayer || f.defeated || f.Hidden || f.def == null || !f.def.humanlikeFaction || s.Tile.Layer != t.Layer) continue;
                if (f.PlayerRelationKind != FactionRelationKind.Ally || !f.HostileTo(c.people)) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, t);
                if (d > RaidersCampMath.AllyTiles || d >= bestTiles) continue;
                best = f;
                bestTiles = d;
            }
            return best;
        }

        /// <summary>A year after they were paid, their word was kept: the Promise tie is kept and the people may come again.</summary>
        private static void KeepWord(RaidersCampHome rh, int now)
        {
            if (rh.paid == null && rh.promise < 0) return;
            bool kept = RaidersCampMath.WordKept(rh.paidAt, now);
            bool gone = rh.paid == null || rh.paid.defeated;
            if (!kept && !gone) return;
            var tie = WorldLedger.Get(rh.promise);
            if (tie != null && tie.Live) WorldLedger.Set(tie, gone ? Tie.State.Ended : Tie.State.Kept);
            rh.paid = null;
            rh.paidAt = -1;
            rh.breaks = false;
            rh.promise = -1;
        }

        // ---------------- Set up ----------------

        /// <summary>
        /// The look at a home every ten days: a rich colony, the game far enough along, no camp standing and the rest after
        /// the last one over, then the rate's chance, then a people that qualifies (the biggest grudge first, then the
        /// nearest) and a tile that can hold the camp.
        /// </summary>
        private static void Look(Map home, RaidersCampHome rh, int now)
        {
            if (!RaidersCampMath.LookDue(rh.lookedAt, now)) return;
            rh.lookedAt = now;
            if (!On || !ThreatsAllowed) return;
            float threat = StorytellerUtility.DefaultThreatPointsNow(home);
            if (!RaidersCampMath.HomeFits(threat, now, rh.camp != null, rh.endedAt)) return;
            if (!Rand.Chance(RaidersCampMath.Chance(Rate))) return;
            var peoples = Candidates(home, rh, now, out var grudges, out var tiles);
            int i = RaidersCampMath.Pick(grudges, tiles);
            if (i < 0) return;
            var f = peoples[i];
            bool broke = rh.paid == f && rh.paidAt >= 0 && now - rh.paidAt < (long)RaidersCampMath.PromiseDays * Day;
            var c = SetUp(home, f, null, null);
            if (c == null) return;
            c.grudge = grudges[i] >= RaidersCampMath.GrudgeAt;
            if (broke)
            {
                c.brokeWord = true;
                var tie = WorldLedger.Get(rh.promise);
                if (tie != null && tie.Live) WorldLedger.Set(tie, Tie.State.Broken);
                rh.paid = null;
                rh.paidAt = -1;
                rh.breaks = false;
                rh.promise = -1;
            }
        }

        /// <summary>The peoples that could set up a camp near this home now, with their grudges and how near they are (tiles; -1 for neither town nor territory near).</summary>
        private static List<Faction> Candidates(Map home, RaidersCampHome rh, int now, out List<float> grudges, out List<float> tiles)
        {
            var list = new List<Faction>();
            grudges = new List<float>();
            tiles = new List<float>();
            var mem = FactionMemory.On ? FactionMemory.Instance : null;
            var dead = new Dictionary<Faction, int>();
            foreach (var d in Grudges.All())
                if (d.faction != null && !d.Settled)
                    dead[d.faction] = dead.TryGetValue(d.faction, out int n) ? n + 1 : 1;
            var comp = CouncilGameComponent.Instance;
            var running = WorldSituations.Instance?.Running?.ToList() ?? new List<WorldSituation>();
            Dictionary<Faction, float> territory = null;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (!CanOwn(f)) continue;
                float town = NearestTown(home, f);
                bool vendetta = comp?.vendettas?.vendettas != null && comp.vendettas.vendettas.Any(v => v != null && v.Active && v.faction == f);
                float grudge = RaidersCampMath.Grudge(mem?.Sum(f, DeedSide.Wrong) ?? 0f, dead.TryGetValue(f, out int k) ? k : 0, vendetta);
                float terr = -1f;
                if ((town < 0f || town > RaidersCampMath.TownTiles) && grudge < RaidersCampMath.GrudgeAt)
                {
                    territory ??= Territories(home);
                    if (territory.TryGetValue(f, out float tt)) terr = tt;
                }
                bool about = running.Any(s => s?.ctx != null && (s.ctx.faction == f || s.ctx.enemy == f));
                bool promised = rh.paid == f && RaidersCampMath.WordHolds(rh.paidAt, rh.breaks, now);
                if (!RaidersCampMath.PeopleFits(true, town, terr, grudge, about, promised)) continue;
                list.Add(f);
                grudges.Add(grudge);
                float near = town >= 0f && town <= RaidersCampMath.TownTiles ? town : terr;
                tiles.Add(near);
            }
            return list;
        }

        /// <summary>A people that can raid the colony and hold a camp: hostile, with fighters, and the settlement kind of people the game's camp map needs.</summary>
        public static bool CanOwn(Faction f)
        {
            if (!Raids.CanRaid(f) || f.def.pawnGroupMakers == null || !f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Settlement)) return false;
            try
            {
                return SitePartDefOf.BanditCamp != null && SitePartDefOf.BanditCamp.FactionCanOwn(f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: who can own one", e);
                return false;
            }
        }

        /// <summary>Tiles to the people's nearest town on the home's layer, or -1.</summary>
        private static float NearestTown(Map home, Faction f)
        {
            float best = -1f;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Faction != f || s.Destroyed || s.Tile.Layer != home.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Whose territory (Faction Territories, through the world mechanics) lies within RaidersCampMath.TerritoryTiles of
        /// the home, each with its nearest tile. Straight-line distances inside the flood fill; nothing without such a mod
        /// (each tile's look is then a quick "nobody"). Asked at most once a look, only for a people not near by its towns.
        /// </summary>
        private static Dictionary<Faction, float> Territories(Map home)
        {
            var found = new Dictionary<Faction, float>();
            try
            {
                var root = home.Tile;
                var grid = Find.WorldGrid;
                float max = RaidersCampMath.TerritoryTiles;
                Action<PlanetTile, int> each = (t, _) =>
                {
                    var owner = WorldMechanics.TerritoryOwner(t);
                    if (owner == null) return;
                    float d = grid.ApproxDistanceInTiles(root, t);
                    if (!found.TryGetValue(owner, out float was) || d < was) found[owner] = d;
                };
                root.Layer.Filler.FloodFill(root, t => grid.ApproxDistanceInTiles(root, t) <= max, each);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: territories near a home", e);
            }
            return found;
        }

        /// <summary>
        /// A camp of this people near this home, set up now and hidden until found: on a tile that can hold it, with the
        /// game's own site threat points. <paramref name="leader"/> and <paramref name="why"/> give it a leader with a reason
        /// (a later release's warlord's son), kept in the world while it stands. Null when the home has a camp already, the
        /// people can't hold one, or no tile will do.
        /// </summary>
        public static RaidersCamp SetUp(Map home, Faction people, Pawn leader = null, string why = null)
        {
            var rh = Of(home);
            if (rh == null || rh.camp != null || !CanOwn(people)) return null;
            var tile = FindTile(home);
            if (!tile.Valid) return null;
            int now = Now;
            string kind = KindWord(people);
            if (leader != null && (leader.Dead || leader.Faction != people)) leader = null;
            var c = new RaidersCamp
            {
                people = people, tile = Refs.Of(tile), points = StorytellerUtility.DefaultSiteThreatPointsNow(), setAt = now, stage = CampStage.Hidden,
                kind = kind, label = NameOf(kind, leader), leader = leader, leaderWhy = why
            };
            rh.camp = c;
            rh.camps++;
            if (leader != null) WorldLedger.Keep(leader, KeepReason);
            return c;
        }

        /// <summary>
        /// Where the camp stands: a tile more than RaidersCampMath.NoNearer from every home and at most
        /// RaidersCampMath.MaxTiles from this one, reachable over land, where a base could stand with nothing else on it,
        /// clear of friendly towns; hills and forest first. Straight-line distances inside the flood fill.
        /// </summary>
        public static PlanetTile FindTile(Map home)
        {
            if (home == null || !home.Tile.Valid) return PlanetTile.Invalid;
            try
            {
                var root = home.Tile;
                var grid = Find.WorldGrid;
                var homes = Homes.All.Where(m => m != null && m.Tile.Valid && m.Tile.Layer == root.Layer).Select(m => m.Tile).ToList();
                float reach = RaidersCampMath.MaxTiles + RaidersCampMath.FriendlyTownTiles + 1f;
                var friendly = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && s.Faction != null && !s.Faction.IsPlayer
                                                                         && !s.Faction.HostileTo(Faction.OfPlayer) && s.Tile.Layer == root.Layer
                                                                         && grid.ApproxDistanceInTiles(root, s.Tile) <= reach).Select(s => s.Tile).ToList();
                var tiles = new List<PlanetTile>();
                var weights = new List<float>();
                Action<PlanetTile, int> each = (t, _) =>
                {
                    float d = grid.ApproxDistanceInTiles(root, t);
                    if (d <= RaidersCampMath.NoNearer) return;
                    float nearestHome = float.MaxValue;
                    foreach (var h in homes) nearestHome = Mathf.Min(nearestHome, grid.ApproxDistanceInTiles(h, t));
                    float nearestFriend = -1f;
                    foreach (var s in friendly)
                    {
                        float fd = grid.ApproxDistanceInTiles(s, t);
                        if (nearestFriend < 0f || fd < nearestFriend) nearestFriend = fd;
                    }
                    bool land = !Find.WorldObjects.AnyWorldObjectAt(t) && TileFinder.IsValidTileForNewSettlement(t, null, false);
                    if (!RaidersCampMath.PlaceFits(d, nearestHome, nearestFriend, land)) return;
                    var info = grid[t];
                    bool hills = info.hilliness == Hilliness.SmallHills || info.hilliness == Hilliness.LargeHills || info.hilliness == Hilliness.Mountainous;
                    bool forest = (info.PrimaryBiome?.TreeDensity ?? 0f) >= 0.2f;
                    tiles.Add(t);
                    weights.Add(RaidersCampMath.PlaceWeight(d, hills, forest));
                };
                root.Layer.Filler.FloodFill(root, t => !Find.World.Impassable(t) && grid.ApproxDistanceInTiles(root, t) <= RaidersCampMath.MaxTiles, each);
                if (tiles.Count == 0) return PlanetTile.Invalid;
                int i = Enumerable.Range(0, tiles.Count).RandomElementByWeight(k => weights[k]);
                return tiles[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: a place for it", e);
                return PlanetTile.Invalid;
            }
        }

        // ---------------- Found ----------------

        /// <summary>
        /// Someone out of doors beyond the walls by day may see its smoke: a hunter after game first, then whoever works
        /// past the colony's ground, and from a few days on anyone out of doors.
        /// </summary>
        private static void TryFind(Map home, RaidersCampHome rh, RaidersCamp c, int now)
        {
            if (!RaidersCampMath.Daylight(GenLocalDate.HourOfDay(home))) return;
            float days = (now - c.setAt) / (float)Day;
            var spotter = Spotter(home, days >= RaidersCampMath.FindByDays, out string doing);
            if (spotter == null || !Rand.Chance(RaidersCampMath.FindChance(days))) return;
            // Something else took its place on the world map meanwhile: they camp a little way off instead, or nowhere.
            if (Find.WorldObjects.AnyMapParentAt(TileOf(c)))
            {
                var t = FindTile(home);
                if (!t.Valid)
                {
                    End(home, rh, c, CampEnd.Gone, null);
                    return;
                }
                c.tile = Refs.Of(t);
            }
            Found(home, c, spotter, doing, now);
        }

        private static Pawn Spotter(Map home, bool anyOutdoors, out string doing)
        {
            doing = null;
            var area = home.areaManager?.Home;
            Pawn best = null;
            int bestRank = 0;
            foreach (var p in IncidentWorker_Happening.Colonists(home))
            {
                if (p.Downed || p.InMentalState || p.Drafted || !p.Awake() || p.DevelopmentalStage.Juvenile() || p.Position.Roofed(home)) continue;
                int rank = p.CurJobDef == JobDefOf.Hunt ? 3 : area == null || !area[p.Position] ? 2 : anyOutdoors ? 1 : 0;
                if (rank <= bestRank) continue;
                best = p;
                bestRank = rank;
            }
            if (best != null) doing = (bestRank == 3 ? "CC_RaidCamp_Doing_Hunt" : bestRank == 2 ? "CC_RaidCamp_Doing_Out" : "CC_RaidCamp_Doing_About").Translate().Resolve();
            return best;
        }

        /// <summary>
        /// Seen: the game's own bandit camp site goes on the world map where it stands, the one who saw it says so, and the
        /// letter names the danger before any raid comes. Their people's wrong goes into the colony's memory of them and
        /// the history has it.
        /// </summary>
        private static void Found(Map home, RaidersCamp c, Pawn spotter, string doing, int now, string how = null, string town = null)
        {
            var site = MakeSite(c, TileOf(c), c.points, false);
            if (site == null) return;
            c.stage = CampStage.Found;
            c.foundAt = now;
            c.price = RaidersCampMath.Price(StorytellerUtility.DefaultThreatPointsNow(home) * RaidersCampMath.RaidFactor);
            string dir = Direction(home, c);
            string where = Where(home, c);
            var people = c.people.Name.Named("PEOPLE");
            // How it was found: a colonist who saw its smoke, a visitor who came past it, a neighbour's letter.
            string suffix = how.NullOrEmpty() ? "" : "_" + how;
            string said = ("CC_RaidCamp_Say" + suffix).Translate(dir.Named("DIR"), c.kind.Named("KIND")).Resolve();
            if (spotter != null && spotter.Spawned) Speech.Say(spotter, said);
            string who = spotter?.LabelShort ?? "?";
            var parts = new List<string>
            {
                ("CC_RaidCamp_Found_Text" + suffix).Translate(who.Named("SPOTTER"), (doing ?? "").Named("DOING"), where.Named("WHERE"), people, c.kind.Named("KIND"),
                    Homes.Name(home).Named("COLONY"), (town ?? "").Named("TOWN")).Resolve(),
                "CC_RaidCamp_Found_Danger".Translate(dir.Named("DIR"), people).Resolve()
            };
            if (c.leader != null && !c.leader.Dead)
            {
                string why = "CC_RaidCamp_Why_" + (c.leaderWhy ?? "");
                parts.Add(c.leaderWhy != null && why.CanTranslate()
                    ? why.Translate(c.leader.LabelShort.Named("LEADER"), people).Resolve()
                    : "CC_RaidCamp_Found_Leader".Translate(c.leader.LabelShort.Named("LEADER")).Resolve());
            }
            if (c.brokeWord) parts.Add("CC_RaidCamp_Found_BrokeWord".Translate(people).Resolve());
            else if (c.grudge)
            {
                string note = FactionMemory.RaidNote(c.people);
                if (!note.NullOrEmpty()) parts.Add(note);
            }
            parts.Add("CC_RaidCamp_Found_Ends".Translate(people).Resolve());
            string label = "CC_RaidCamp_Found_Label".Translate(c.kind.Named("KIND")).Resolve().CapitalizeFirst();
            Tell.Letter(NewsKind.World, Importance.Important, home, label, string.Join("\n\n", parts), LetterDefOf.ThreatSmall, new LookTargets(site), c.people);
            FactionMemory.Note(c.people, RaidersCampMath.DeedCamped, c.kind);
            string line = ("CC_RaidCamp_Found_Line" + suffix).Translate(who.Named("SPOTTER"), people, c.kind.Named("KIND"), where.Named("WHERE"), (town ?? "").Named("TOWN")).Resolve();
            History.Add(RaidersCampMath.KindFound, Actors(c, null, spotter != null && KinMemory.Ours(spotter) ? spotter : null), Refs.Colony(home), HistoryMath.Milestone, line);
        }

        /// <summary>The game's own bandit camp site for the camp: their faction, the points given, its name; on the world map. Null when it can't be made.</summary>
        private static Site MakeSite(RaidersCamp c, PlanetTile tile, float points, bool poorer)
        {
            if (c?.people == null || !tile.Valid || SitePartDefOf.BanditCamp == null) return null;
            try
            {
                // A place on the tile (a town, another site) takes it; the colony's own caravan standing there after the fight doesn't.
                if (Find.WorldObjects.AnyMapParentAt(tile)) return null;
                var site = SiteMaker.MakeSite(SitePartDefOf.BanditCamp, tile, c.people, true, points);
                if (site == null) return null;
                site.customLabel = c.label;
                // Put back after an attack: what they had left behind their walls the colony took or burned.
                if (poorer)
                    foreach (var part in site.parts)
                        if (part?.parms != null)
                            part.parms.lootMarketValue *= 0.5f;
                Find.WorldObjects.Add(site);
                c.site = site.ID;
                c.points = site.ActualThreatPoints;
                return site;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its site", e);
                return null;
            }
        }

        // ---------------- The colony on its map ----------------

        /// <summary>The camp's map was made (the colony came to attack it): how many fighters hold it.</summary>
        internal static void MapMade(Site s)
        {
            var c = CampAt(s, out _);
            if (c == null || s.Map == null) return;
            c.entered = true;
            c.fighters = 0;
            // Whoever leads it is there, behind its walls with the rest (RaidersCampWarnings.cs).
            LeaderOnMap(c, s.Map);
            Observe(c, s.Map);
        }

        /// <summary>The colony is leaving the camp's map: how many of its fighters still stand, and whether any threat is left.</summary>
        internal static void MapLeaving(Site s)
        {
            var c = CampAt(s, out _);
            if (c == null || s.Map == null) return;
            Observe(c, s.Map);
        }

        /// <summary>Its fighters on its map (the most it ever held, and how many stand now) and whether any threat to the colony is left there.</summary>
        private static void Observe(RaidersCamp c, Map map)
        {
            if (c == null || map == null) return;
            c.entered = true;
            int all = 0, up = 0;
            foreach (var p in map.mapPawns.SpawnedPawnsInFaction(c.people))
            {
                if (p == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike || p.IsPrisoner) continue;
                all++;
                if (!p.Downed) up++;
            }
            c.fighters = Math.Max(c.fighters, all);
            c.standing = up;
            c.threatLeft = GenHostility.AnyHostileActiveThreatToPlayer(map, true);
            NoteFought(c, map);
        }

        /// <summary>
        /// The colony left with fighters still standing: the game removed the site anyway, and the camp is back on its tile,
        /// its points less those who fell, with less left behind its walls.
        /// </summary>
        private static void PutBack(Map home, RaidersCamp c, int now)
        {
            int fell = Math.Max(0, c.fighters - Math.Max(0, c.standing));
            float points = RaidersCampMath.PointsAfter(c.points, c.fighters, c.standing);
            int standing = c.standing;
            c.entered = false;
            c.fighters = 0;
            c.standing = -1;
            c.threatLeft = true;
            var site = MakeSite(c, TileOf(c), points, true);
            if (site == null)
            {
                End(home, Of(home), c, CampEnd.Broken, null);
                return;
            }
            var people = c.people.Name.Named("PEOPLE");
            string text = "CC_RaidCamp_Back_Text".Translate(people, c.kind.Named("KIND"), standing.Named("STANDING"), fell.Named("FELL"), Where(home, c).Named("WHERE")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Important, home, "CC_RaidCamp_Back_Label".Translate(c.kind.Named("KIND")).Resolve().CapitalizeFirst(), text,
                LetterDefOf.ThreatSmall, new LookTargets(site), c.people);
            History.Add(RaidersCampMath.KindBack, Actors(c, null, null), Refs.Colony(home), HistoryMath.Everyday, text);
        }

        // ---------------- Raids ----------------

        /// <summary>The mean days between its raids at its points: the game's own raid-source curve (the same numbers by hand if it can't be read).</summary>
        private static float CurveDays(float points)
        {
            try
            {
                return QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve.Evaluate(points);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: the raid-source curve", e);
                return RaidersCampMath.MtbDays(points);
            }
        }

        /// <summary>
        /// A raid from the camp: never on top of another threat on the map nor within a day of the last big one; at 0.6 of
        /// the home's threat points, walking in from the map's edge on the camp's side, led by its leader if it has one.
        /// </summary>
        private static void TryRaid(Map home, RaidersCamp c, int now)
        {
            if (MapFacts.ThreatOn(home) || now - home.storyState.LastThreatBigTick < Day) return;
            float points = StorytellerUtility.DefaultThreatPointsNow(home) * RaidersCampMath.RaidFactor;
            var before = new HashSet<Pawn>(home.mapPawns.SpawnedPawnsInFaction(c.people));
            var people = c.people.Name.Named("PEOPLE");
            string where = Where(home, c);
            string label = "CC_RaidCamp_RaidLabel".Translate(c.kind.Named("KIND")).Resolve().CapitalizeFirst();
            string text = "CC_RaidCamp_RaidText".Translate(people, c.kind.Named("KIND"), where.Named("WHERE")).Resolve();
            var lead = c.leader != null && !c.leader.Dead && !c.leader.Spawned ? c.leader : null;
            if (!Raids.Send(home, c.people, points, label, text, lead, EdgeToward(home, TileOf(c)), false)) return;
            c.raids++;
            c.lastRaid = now;
            c.raidOpen = true;
            c.raidAt = now;
            c.raidDown = 0;
            c.raidFled = false;
            c.raiders = home.mapPawns.SpawnedPawnsInFaction(c.people).Where(p => !before.Contains(p)).ToList();
            c.raidSize = c.raiders.Count;
            History.Add(RaidersCampMath.KindRaid, Actors(c, null, null), Refs.Colony(home), HistoryMath.Everyday, text, mergeTicks: Day);
        }

        /// <summary>A cell on the map's edge on the side the camp lies, open to the sky, from which the colony can be reached; null for none.</summary>
        private static IntVec3? EdgeToward(Map home, PlanetTile camp)
        {
            if (home == null || !camp.Valid || camp.Layer != home.Tile.Layer) return null;
            try
            {
                var rot = Find.WorldGrid.GetRotFromTo(home.Tile, camp);
                bool roofOk = home.TileInfo.AllowRoofedEdgeWalkIn;
                if (CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(home) && (roofOk || !home.roofGrid.Roofed(c)) && home.reachability.CanReachColony(c), home, rot,
                        CellFinder.EdgeRoadChance_Hostile, out var cell))
                    return cell;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: the edge on its side", e);
            }
            return null;
        }

        /// <summary>
        /// The raid on the map: how many fell (dead, down or taken) and whether they fled in a panic, until none of them
        /// still stands on the map; then it is beaten off or not, and two beaten off and the camp gives up.
        /// </summary>
        private static void Track(Map home, RaidersCamp c, int now)
        {
            if (c.raiders.Count == 0)
                c.raiders = home.mapPawns.SpawnedPawnsInFaction(c.people).Where(p => p != null && !p.Dead && !p.IsPrisoner && p.GetLord() != null).ToList();
            int down = 0, up = 0;
            foreach (var p in c.raiders)
            {
                if (p == null) continue;
                if (p.Dead || p.Downed || p.IsPrisonerOfColony || p.Faction != c.people) down++;
                else if (p.Spawned && p.Map == home)
                {
                    up++;
                    if (p.GetLord()?.CurLordToil is LordToil_PanicFlee) c.raidFled = true;
                }
            }
            c.raidDown = Math.Max(c.raidDown, down);
            c.raidSize = Math.Max(c.raidSize, c.raiders.Count);
            if (up > 0 && now - c.raidAt < (long)RaidersCampMath.RaidWatchDays * Day) return;
            c.raidOpen = false;
            c.raiders.Clear();
            if (!RaidersCampMath.Beaten(c.raidSize, c.raidDown, c.raidFled)) return;
            c.beaten++;
            Tell.Minor(NewsKind.World, Importance.Notable, home,
                "CC_RaidCamp_Beaten".Translate(c.kind.Named("KIND"), c.people.Name.Named("PEOPLE"), c.raidDown.Named("DOWN"), c.raidSize.Named("SIZE")).Resolve(), null);
        }

        // ---------------- Paid ----------------

        /// <summary>
        /// The colony pays their price: someone carries the silver out to the edge of the colony's ground and leaves it
        /// there for them (a play-out errand); no raid comes meanwhile, and they break camp once it is handed over. Without
        /// anyone to carry it, it leaves the stores at once, as an answer's taking always did. False without the silver.
        /// </summary>
        public static bool Pay(Map home, RaidersCamp c)
        {
            if (home == null || c == null || c.stage != CampStage.Found || c.price <= 0) return false;
            if (PlayCarry.Have(home, "silver", ThingDefOf.Silver) < c.price) return false;
            c.stage = CampStage.Paying;
            c.payAt = Now;
            var ctx = new DilemmaContext { map = home, faction = c.people };
            string label = "CC_RaidCamp_PayLabel".Translate(c.kind.Named("KIND")).Resolve();
            // apart: one of the colony carries the silver out to the edge of its ground and leaves it there for them to collect.
            var p = new DilemmaEffect_RaidersCampPrice { what = "silver", count = c.price, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = label }.Run(ctx);
            c.payPlay = p?.id ?? -1;
            if (p == null)
            {
                PlayCarry.Take(home, "silver", ThingDefOf.Silver, c.price);
                End(home, Of(home), c, CampEnd.Paid, null);
            }
            return true;
        }

        /// <summary>The silver went out to them: the errand is over (handed over, or fallen through and taken from the stores as always).</summary>
        private static bool PaidUp(RaidersCamp c)
        {
            if (c.payPlay < 0) return true;
            var p = PlayOutTracker.Instance?.Get(c.payPlay);
            return p == null || p.state == PlayState.Done;
        }

        // ---------------- Endings ----------------

        /// <summary>The peoples in an ending: theirs, the ally's, and one of the colony.</summary>
        private static List<HistoryActor> Actors(RaidersCamp c, Faction ally, Pawn ours)
        {
            var list = new List<HistoryActor>();
            if (ally != null) list.Add(new HistoryActor(Refs.Of(ally), HistoryRole.Did));
            if (c?.people != null) list.Add(new HistoryActor(Refs.Of(c.people), ally != null ? HistoryRole.Against : HistoryRole.About));
            if (c?.leader != null) list.Add(new HistoryActor(Refs.Of(c.leader), HistoryRole.About));
            if (ours != null) list.Add(new HistoryActor(Refs.Of(ours), HistoryRole.With));
            return list;
        }

        /// <summary>
        /// The camp is over: its site goes from the world map (when the colony isn't on it), its leader is let go, the home
        /// rests, and, for a camp the colony knew of, the ending is told, written into the history and into what the
        /// peoples remember. Paid, their word to keep away for a year goes on the ledger.
        /// </summary>
        private static void End(Map home, RaidersCampHome rh, RaidersCamp c, CampEnd end, Faction ally)
        {
            if (rh == null || c == null) return;
            int now = Now;
            var site = SiteOf(c);
            if (site != null && !site.HasMap)
                try
                {
                    site.Destroy();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("raiders' camp: its site taken away", e);
                }
            LetLeaderGo(c);
            if (rh.camp == c) rh.camp = null;
            rh.endedAt = now;
            if (!c.Shown || end == CampEnd.Gone || home == null || c.people == null) return;
            var people = c.people.Name.Named("PEOPLE");
            var args = new[]
            {
                people, c.kind.Named("KIND"), Where(home, c).Named("WHERE"), Homes.Name(home).Named("COLONY"), (ally?.Name ?? "").Named("ALLY"),
                (ally != null ? RaidersCampMath.AllyKey(Ages.Of(ally)).Translate().Resolve() : "").Named("ALLIES")
            };
            string key = "CC_RaidCamp_End_" + end + (end == CampEnd.GaveUp && c.beaten < RaidersCampMath.BeatenToGiveUp ? "_Time" : "");
            string text = key.Translate(args).Resolve();
            string label = ("CC_RaidCamp_End_" + end + "_Label").Translate(args).Resolve().CapitalizeFirst();
            // Peace: the game's own letter already said the site is gone (it takes away a hostile people's sites when peace
            // is made), so Rimfolk's word is a line of the colony news.
            if (end == CampEnd.Peace) Tell.Minor(NewsKind.World, Importance.Notable, home, text, null, MessageTypeDefOf.PositiveEvent);
            else Tell.Letter(NewsKind.World, Importance.Notable, home, label, text, LetterDefOf.PositiveEvent, null, ally ?? c.people);
            History.Add(RaidersCampMath.KindOf(end), Actors(c, ally, null), Refs.Colony(home), HistoryMath.Milestone, text);
            // Broken: the peoples it troubled remember it, and those of the colony who fought there by name (World/ColonistMemory.cs).
            if (end == CampEnd.Broken) ColonistMemory.CampBroken(home, c, TileOf(c));
            string deed = RaidersCampMath.DeedOf(end, out bool byAlly);
            if (deed != null) FactionMemory.Note(byAlly ? ally : c.people, deed, c.kind);
            if (end == CampEnd.Paid)
            {
                rh.paid = c.people;
                rh.paidAt = now;
                rh.breaks = Rand.Chance(RaidersCampMath.BreakWord);
                var tie = WorldLedger.AddTie(Tie.Kind.Promise, Refs.Of(c.people), Refs.Colony(home), new TieTerms { key = Origin, days = RaidersCampMath.PromiseDays, about = c.tile },
                    now + (RaidersCampMath.PromiseDays + 1) * Day, Origin);
                rh.promise = tie?.id ?? -1;
            }
        }

        private static void LetLeaderGo(RaidersCamp c)
        {
            var p = c?.leader;
            if (p == null) return;
            c.leader = null;
            try
            {
                if (WorldLedger.Release(p, KeepReason) && !p.Dead && !p.Spawned) Find.WorldPawns?.ForcefullyKeptPawns.Remove(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("raiders' camp: its leader let go", e);
            }
        }
    }
}
