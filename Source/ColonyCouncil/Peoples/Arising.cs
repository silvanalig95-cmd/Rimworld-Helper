using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a new people arises from, found in the world (<see cref="Arising"/>), and where its town goes.</summary>
    public sealed class ArisingCause
    {
        public string kind;
        /// <summary>The key it is kept under (one people a cause).</summary>
        public string key;
        /// <summary>The age the people takes its ways from (0 tribal to 3 spacer: LedgerMath.AgeOf).</summary>
        public WorldAge age;
        /// <summary>Where their town goes: this many tiles' walk from here.</summary>
        public PlanetTile near;
        public int minTiles = 2, maxTiles = 6;
        /// <summary>The home nearest it, which hears of it, and how far.</summary>
        public Map home;
        public float tiles = -1f;
        /// <summary>The people it came from: the fallen town's, the faith's; null for a league or a band.</summary>
        public Faction parent;
        /// <summary>The fallen town's name (refugees); the faith's name (splinter).</summary>
        public string name;
        /// <summary>The fallen town's ruin on the map (refugees), taken by the colony's own people.</summary>
        public WorldObject ruin;
        public bool byColony;
        /// <summary>The league's towns, the most prosperous first; the faith's town (splinter).</summary>
        public List<Settlement> towns = new List<Settlement>();
        /// <summary>The one the council sent away, and their record (exiles); who stood by them and who was glad.</summary>
        public Pawn exile;
        public BanishRecord record;
        public int stoodBy, glad;
        public Pawn friend;
        /// <summary>The war of words it followed (splinter): the colony kept a fair table there.</summary>
        public SituationRecord war;
        public bool debate;
        /// <summary>When it happened: the town fell, they were sent away, the war of words ended; -1 for a league.</summary>
        public int since = -1;
        /// <summary>Dev mode: a stand-in cause, not one that happened (the words name it as if it had).</summary>
        public bool standIn;
        /// <summary>
        /// The calling the cause leans to (ArisingMath.Lean), or null: survivors near a river to the river folk's ways, a
        /// band led by a learned one to the scholars'. A lean a town can't stand for (no river in reach) is let go.
        /// </summary>
        public string lean;

        /// <summary>The peoples whose land (Faction Territories) the town may stand on: the cause's own.</summary>
        public HashSet<Faction> Kin()
        {
            var set = new HashSet<Faction>();
            if (parent != null) set.Add(parent);
            foreach (var t in towns)
                if (t?.Faction != null)
                    set.Add(t.Faction);
            return set;
        }
    }

    /// <summary>
    /// Peoples that arise during play (release B: docs/design/peoples.md, "Release B"; DESIGN.md "Rimfolk's peoples"), so
    /// an existing run sees new peoples come into being from what happened in it. Four causes, each read from what the
    /// world keeps: survivors of a town that fell in a fight within a home's reach (RiseFall's fight ruins) settle near its
    /// ruin; merchants or herders of three prospering towns of two peoples or more band together as a league (the
    /// region's fortunes); one the council sent away (Social/Banishment.cs) gathers others with nowhere to go and leads
    /// them; and with Ideology, faithful who broke away after a war of words over their faith (Council_WS_HolyWar) found a
    /// creed and a people of their own. Each takes one of Rimfolk's templates by its age (ArisingMath.Template), a name, an
    /// emblem colour apart from the world's, a town a few days from its cause (the region gives it faces), goodwill by
    /// what happened (never an enemy or an ally on its first day), a letter whose words say what the map shows, and the
    /// history's entry. Run by the region's day (World/Region/RegionBook.cs), one a day at most, at the pace of
    /// <see cref="ArisingMath.PaceAllows"/> and the setting "New peoples arise" (0 switches it off). Saved:
    /// <see cref="ArisingBook"/>.
    /// </summary>
    public static class Arising
    {
        /// <summary>The setting (0 to 2, 1 by default): how often; 0 none.</summary>
        public static float Setting => ColonyCouncilMod.Settings?.newPeoples ?? 1f;

        public static bool On => Setting > 0.001f && Current.Game != null && Peoples.Templates.Any();

        /// <summary>A people of Rimfolk's being made now: the game makes no leader for it yet (Patch_HandOverLeader), one comes once its faith is set.</summary>
        public static bool Making { get; private set; }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("new peoples: " + what, e);
            }
        }

        // ================= The day =================

        /// <summary>The region's day: each cause the pace allows is looked for, and the first found arises (one a day at most).</summary>
        internal static void Daily(int now)
        {
            if (!On) return;
            var book = ArisingBook.Instance;
            if (book == null) return;
            int day = now / GenDate.TicksPerDay;
            int standing = book.Standing;
            // The causes in turn, a different one first each day, so none waits behind another.
            for (int i = 0; i < ArisingMath.Kinds.Length; i++)
            {
                string kind = ArisingMath.Kinds[(day + i) % ArisingMath.Kinds.Length];
                if (!ArisingMath.PaceAllows(Setting, now, book.LastAt(), book.LastAt(kind), standing)) continue;
                ArisingCause cause = null;
                Safe("looking for " + kind, () => cause = FindCause(kind, day, now, false));
                if (cause == null) continue;
                Faction made = null;
                Safe("a people of " + kind, () => made = Arise(cause));
                if (made != null) return;
            }
        }

        // ================= The causes =================

        /// <summary>
        /// The cause of this kind found now, or null. <paramref name="loose"/> (the dev tools): any such cause, whatever
        /// its day and its roll.
        /// </summary>
        public static ArisingCause FindCause(string kind, int day, int now, bool loose)
        {
            var book = ArisingBook.Instance;
            if (book == null) return null;
            switch (kind)
            {
                case ArisingMath.Refugees: return Refugees(book, day, now, loose);
                case ArisingMath.League: return League(book, day, now, loose);
                case ArisingMath.Exiles: return Exiles(book, day, now, loose);
                case ArisingMath.Splinter: return Splinter(book, day, now, loose);
                default: return null;
            }
        }

        /// <summary>A home within reach of a tile on the world's surface, and how far; null for none.</summary>
        private static Map HomeNear(PlanetTile tile, out float tiles)
        {
            tiles = -1f;
            if (!tile.Valid || tile.Layer == null || !tile.Layer.IsRootSurface) return null;
            var home = RegionTowns.NearestHome(tile, out tiles);
            return home != null && tiles >= 0f && tiles <= RegionTowns.ReachTiles ? home : null;
        }

        /// <summary>
        /// Survivors of a town that fell in a fight (RiseFall's fight ruins, within a home's reach): from three days after
        /// it fell, once its ruin stands, to sixty; two fallen towns in five.
        /// </summary>
        private static ArisingCause Refugees(ArisingBook book, int day, int now, bool loose)
        {
            var l = WorldLedger.Instance;
            if (l == null) return null;
            foreach (var r in l.Ruins.Where(x => x != null).OrderByDescending(x => x.since))
            {
                if (r.why != LedgerRuin.Fight || r.Waiting || r.people == null || r.people.IsPlayer || r.people.def == null || !r.people.def.humanlikeFaction) continue;
                string key = ArisingMath.Key(ArisingMath.Refugees, r.town);
                if (book.Used(key)) continue;
                if (!loose && (!ArisingMath.RefugeesDue(r.since, now) || !ArisingMath.LookDay(day, r.town) || !ArisingMath.Fated(r.town, ArisingMath.SaltRefugees, ArisingMath.RefugeesChance))) continue;
                var ruin = RiseFall.WorldObjectOf(r.ruin);
                if (ruin == null) continue;
                var home = HomeNear(ruin.Tile, out float tiles);
                if (home == null) continue;
                return new ArisingCause
                {
                    kind = ArisingMath.Refugees, key = key, age = Ages.Of(r.people), near = ruin.Tile, minTiles = 2, maxTiles = 6, home = home, tiles = tiles,
                    parent = r.people, name = r.name, ruin = ruin, byColony = r.byColony, since = r.since,
                    lean = ArisingMath.Lean(ArisingMath.Refugees, true, ArisingMath.Fated(r.town, ArisingMath.SaltRiver, ArisingMath.RiverChance), 0, 0, 0,
                        ArisingMath.Fated(r.town, ArisingMath.SaltBell, ArisingMath.BellChance), ArisingMath.Fated(r.town, ArisingMath.SaltWheel, ArisingMath.WheelChance),
                        (int)Ages.Of(r.people), 0, 0, 0, 0, 0, 0, ArisingMath.Fated(r.town, ArisingMath.SaltLine, ArisingMath.LineChance),
                        ArisingMath.Fated(r.town, ArisingMath.SaltCrew, ArisingMath.CrewChance))
                };
            }
            return null;
        }

        /// <summary>A town that may join a league: of a people at peace with the colony, not one of Rimfolk's or the colony's own, kept in detail with its fortune, not being given up, in no league yet.</summary>
        private static bool LeagueTown(ArisingBook book, Settlement t, out float prosperity)
        {
            prosperity = -1f;
            var f = t?.Faction;
            if (f == null || f.IsPlayer || f.Hidden || f.defeated || f.temporary || f.def == null || !f.def.humanlikeFaction || Peoples.IsOurs(f) || HandOverPeoples.IsOurs(f)) return false;
            if (f.HostileTo(Faction.OfPlayer) || book.InLeague(t.ID)) return false;
            var rec = WorldLedger.TownRecord(t.ID);
            if (rec == null || !rec.Seeded || rec.closing >= 0 || rec.gone >= 0) return false;
            prosperity = rec.prosperity;
            return true;
        }

        /// <summary>
        /// A league of prospering towns (the region's fortunes): three or four towns of two peoples or more at 62 or more,
        /// within 18 tiles of the most prosperous, none at war with another, looked for every five days and formed on a
        /// fifth of the looks. Loose (the dev tools): the most prosperous such towns whatever their fortune.
        /// </summary>
        private static ArisingCause League(ArisingBook book, int day, int now, bool loose)
        {
            if (!RegionBook.FortunesOn && !loose) return null;
            if (!loose && (!ArisingMath.LookDay(day, 0x1ea6) || !Rand.Chance(ArisingMath.LeagueChance))) return null;
            var towns = new List<Settlement>();
            var prosperity = new List<float>();
            foreach (var t in RiseFall.TownsInDetail())
            {
                if (!LeagueTown(book, t, out float p) || HomeNear(t.Tile, out _) == null) continue;
                towns.Add(t);
                prosperity.Add(loose ? Math.Max(p, ArisingMath.LeagueAt) + p / 1000f : p);
            }
            if (towns.Count < ArisingMath.LeagueMin) return null;
            var peoples = towns.Select(t => t.Faction).Distinct().ToList();
            var ids = towns.Select(t => peoples.IndexOf(t.Faction)).ToList();
            var grid = Find.WorldGrid;
            var members = ArisingMath.LeagueOf(prosperity, ids, (a, b) => towns[a].Tile.Layer != towns[b].Tile.Layer ? float.MaxValue : grid.ApproxDistanceInTiles(towns[a].Tile, towns[b].Tile),
                (a, b) => towns[a].Faction.HostileTo(towns[b].Faction));
            if (members.Count == 0) return null;
            var chosen = members.Select(i => towns[i]).ToList();
            var home = HomeNear(chosen[0].Tile, out float tiles);
            if (home == null) return null;
            return new ArisingCause
            {
                kind = ArisingMath.League, key = ArisingMath.LeagueKey(chosen.Select(t => t.ID)), age = (WorldAge)ArisingMath.MeanAge(chosen.Select(t => (int)Ages.Of(t.Faction)).ToList()),
                near = chosen[0].Tile, minTiles = 3, maxTiles = 8, home = home, tiles = tiles, towns = chosen
            };
        }

        /// <summary>One the council sent away who could lead a band now: alive in the world, of no people, free, not on anyone's quest.</summary>
        private static bool CanLead(Pawn p) =>
            p != null && !p.Dead && !p.Discarded && !p.Spawned && p.Faction == null && Find.WorldPawns.Contains(p) && !p.IsPrisoner && p.GetCaravan() == null && !People.Kidnapped(p)
            && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p) && p.RaceProps.Humanlike && p.DevelopmentalStage.Adult();

        /// <summary>
        /// A band of the banished: one the council sent away (not come home, never ridden with raiders), from 15 days after
        /// they went to 300, gathers others with nowhere to go; half of those sent away do, on a fifth of the looks. Their
        /// camp lies a few days from the home they were sent from (the seat), clear of the game's nearness rule.
        /// </summary>
        private static ArisingCause Exiles(ArisingBook book, int day, int now, bool loose)
        {
            var bt = BanishTracker.Instance;
            var home = Homes.Seat;
            if (bt == null || home == null || !home.Tile.Valid || !home.Tile.Layer.IsRootSurface) return null;
            foreach (var r in bt.records.Where(x => x != null && x.banishedTick >= 0).OrderByDescending(x => x.banishedTick))
            {
                string key = ArisingMath.Key(ArisingMath.Exiles, r.id);
                if (book.Used(key) || r.cameBack || r.rode || !CanLead(r.pawn)) continue;
                if (!loose && (!ArisingMath.ExilesDue(r.banishedTick, r.cameBack, r.rode, now) || !ArisingMath.LookDay(day, r.id)
                               || !ArisingMath.Fated(r.id, ArisingMath.SaltExiles, ArisingMath.ExilesChance) || !Rand.Chance(ArisingMath.ExilesLookChance))) continue;
                var cause = new ArisingCause
                {
                    kind = ArisingMath.Exiles, key = key, age = Ages.Ours, near = home.Tile, minTiles = RiseFall.GoodwillTiles + 2, maxTiles = RiseFall.GoodwillTiles + 9, home = home, tiles = 0f,
                    exile = r.pawn, record = r, name = r.Name, since = r.banishedTick,
                    lean = ArisingMath.Lean(ArisingMath.Exiles, true, ArisingMath.Fated(r.id, ArisingMath.SaltRiver, ArisingMath.RiverChance), Learning(r.pawn),
                        Skill(r.pawn, SkillDefOf.Animals), Skill(r.pawn, SkillDefOf.Shooting), false, false, (int)Ages.Ours,
                        Skill(r.pawn, SkillDefOf.Melee), Skill(r.pawn, SkillDefOf.Plants), Skill(r.pawn, SkillDefOf.Crafting), Psylink(r.pawn),
                        Skill(r.pawn, SkillDefOf.Mining), Skill(r.pawn, SkillDefOf.Social), false, false)
                };
                Feelings(cause, home);
                return cause;
            }
            return null;
        }

        /// <summary>How learned the one sent away is (their Intellectual skill; 0 when they have none to speak of).</summary>
        private static int Learning(Pawn p) => Skill(p, SkillDefOf.Intellectual);

        /// <summary>The one sent away's psylink level (Royalty; 0 without one).</summary>
        private static int Psylink(Pawn p)
        {
            try
            {
                return ModsConfig.RoyaltyActive && p != null ? p.GetPsylinkLevel() : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>The one sent away's level at a skill (0 when they can't do it): how good with beasts, how good a shot.</summary>
        private static int Skill(Pawn p, SkillDef skill)
        {
            var r = skill == null ? null : p?.skills?.GetSkill(skill);
            return r == null || r.TotallyDisabled ? 0 : r.Level;
        }

        /// <summary>Who at the home stood by the one sent away and who was glad to see them go, by what the colony thinks of them now; the friend they ask after.</summary>
        private static void Feelings(ArisingCause c, Map home)
        {
            var p = c.exile;
            if (p == null) return;
            int best = BanishMath.StandByOpinion - 1;
            foreach (var o in BanishTracker.ColonyAt(home))
            {
                if (o == null || o == p || o.relations == null) continue;
                int op = BanishTracker.Opinion(o, p);
                if (BanishMath.StandsBy(op, BanishTracker.Close(o, p))) c.stoodBy++;
                else if (BanishMath.Glad(op)) c.glad++;
                int theirs = BanishTracker.Opinion(p, o);
                if (theirs > best)
                {
                    best = theirs;
                    c.friend = o;
                }
            }
        }

        /// <summary>
        /// Faithful who broke away (Ideology): after a war of words over a faith ended (within 30 days, half of them), some
        /// of the people whose preachers began it go off together near their town in reach. Loose (the dev tools): the last
        /// war of words, else a people at peace with the colony nearby with a faith (a stand-in).
        /// </summary>
        private static ArisingCause Splinter(ArisingBook book, int day, int now, bool loose)
        {
            if (!ModsConfig.IdeologyActive) return null;
            var ws = WorldSituations.Instance;
            if (ws != null)
                for (int i = ws.history.Count - 1; i >= 0; i--)
                {
                    var r = ws.history[i];
                    if (r == null || r.defName != "Council_WS_HolyWar" || r.endTick < 0 || r.ending == WorldSituations.Quietly) continue;
                    var f = r.faction;
                    if (f == null || f.defeated || f.IsPlayer || f.ideos?.PrimaryIdeo == null || f.HostileTo(Faction.OfPlayer)) continue;
                    string key = ArisingMath.Key(ArisingMath.Splinter, (long)r.startTick * 1000 + f.loadID % 1000);
                    if (book.Used(key)) continue;
                    if (!loose && (!ArisingMath.SplinterDue(r.endTick, now) || !ArisingMath.LookDay(day, r.startTick)
                                   || !ArisingMath.Fated(r.startTick, ArisingMath.SaltSplinter, ArisingMath.SplinterChance))) continue;
                    var town = TownFor(r.place, f);
                    float tiles = -1f;
                    var home = town != null ? HomeNear(town.Tile, out tiles) : null;
                    if (home == null) continue;
                    var c = SplinterOf(f, town, home, tiles, key);
                    c.war = r;
                    c.debate = r.Has("debate") && !r.Has("debateFailed");
                    c.since = r.endTick;
                    return c;
                }
            if (!loose) return null;
            // The dev tools' stand-in: the nearest town in reach of a people at peace with the colony with a faith.
            foreach (var reach in RegionTowns.Reach)
                for (int i = 0; i < reach.towns.Count; i++)
                {
                    var t = reach.towns[i];
                    var f = t?.Faction;
                    if (f == null || f.IsPlayer || f.defeated || Peoples.IsOurs(f) || HandOverPeoples.IsOurs(f) || f.ideos?.PrimaryIdeo == null || f.HostileTo(Faction.OfPlayer)) continue;
                    var c = SplinterOf(f, t, reach.home, reach.tiles[i], ArisingMath.Key(ArisingMath.Splinter, -Now - f.loadID));
                    c.standIn = true;
                    return c;
                }
            return null;
        }

        private static ArisingCause SplinterOf(Faction f, Settlement town, Map home, float tiles, string key) => new ArisingCause
        {
            kind = ArisingMath.Splinter, key = key, age = Ages.Of(f), near = town.Tile, minTiles = 3, maxTiles = 8, home = home, tiles = tiles, parent = f,
            name = f.ideos.PrimaryIdeo.name, towns = new List<Settlement> { town }
        };

        /// <summary>The war's own town when it stands and is still theirs, else their town nearest a home.</summary>
        private static Settlement TownFor(Ref place, Faction f)
        {
            if (place.kind == RefKind.Town)
            {
                var t = NotableBook.TownOf(place.id);
                if (t != null && !t.Destroyed && t.Faction == f) return t;
            }
            Settlement best = null;
            float bestD = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || s.Faction != f) continue;
                HomeNear(s.Tile, out float d);
                if (d < 0f || d > RegionTowns.ReachTiles || d >= bestD) continue;
                best = s;
                bestD = d;
            }
            return best;
        }

        // ================= Arising =================

        /// <summary>
        /// The template a cause takes by its age and what it leans to (ArisingMath.Template, each template's span of ages:
        /// PeopleExtension.ariseAges), or null when none of Rimfolk's fits.
        /// </summary>
        public static FactionDef TemplateFor(ArisingCause c) => TemplateFor(c, c?.lean);

        private static FactionDef TemplateFor(ArisingCause c, string lean)
        {
            if (c == null) return null;
            var defs = Peoples.Templates.ToList();
            var from = new List<int>();
            var to = new List<int>();
            foreach (var d in defs)
            {
                Peoples.Of(d).AriseSpan((int)LedgerMath.AgeOf((int)d.techLevel), out int lo, out int hi);
                from.Add(lo);
                to.Add(hi);
            }
            int i = ArisingMath.Template(c.kind, (int)c.age, defs.Select(d => Peoples.Of(d).calling).ToList(), from, to, lean);
            return i < 0 ? null : defs[i];
        }

        /// <summary>
        /// The town's tile for the template: a river folk's landing by a river in reach (none: false); any other on any
        /// land that will take a town, a first look and a wider one.
        /// </summary>
        private static bool TileFor(ArisingCause c, FactionDef def, out PlanetTile tile)
        {
            bool river = Peoples.ByWater(def);
            return TryTownTile(c.near, c.minTiles, c.maxTiles, c.Kin(), Score(c), out tile, river) || TryTownTile(c.near, c.maxTiles, c.maxTiles * 2, c.Kin(), Score(c), out tile, river);
        }

        /// <summary>
        /// The people arises: its town's tile found first (none: nothing happens, and the cause may give one later), then
        /// the people made the game's way, its town, its record, its letter and its history. Null when it couldn't be made.
        /// </summary>
        public static Faction Arise(ArisingCause c)
        {
            var book = ArisingBook.Instance;
            var def = c != null ? TemplateFor(c) : null;
            if (book == null || def == null || book.Used(c.key)) return null;
            if (!TileFor(c, def, out var tile))
            {
                // A lean the land can't stand for (no river in reach for a landing): the cause's own ways instead.
                if (c.lean == null) return null;
                c.lean = null;
                def = TemplateFor(c);
                if (def == null || !TileFor(c, def, out tile)) return null;
            }
            var f = Make(def, c, out var detail);
            if (f == null) return null;
            var town = NewTowns.MakeTown(f, tile, TownName(c, f));
            // Kept even without its town (a town the game wouldn't make), so the cause gives no second people.
            book.Add(new ArisenPeople
            {
                people = f, kind = c.kind, cause = c.key, tick = Now, parent = c.parent, parentName = c.parent?.Name,
                from = c.kind == ArisingMath.League ? ArisingWords.Towns(c.towns) : c.name
            });
            if (town == null) return f;
            Safe("the town's faces", () =>
            {
                // Its place's reason read from the world (a road, a river, the old walls of a ruin beside it); the region's
                // "refugees" are families of a town given up, not the survivors of a fight.
                RegionFaces.Founded(town, f.leader);
                RiseFall.NoteFounded(town);
            });
            Safe("the letter", () => ArisingWords.Tell(c, f, town, detail));
            if (Prefs.DevMode)
                Log.Message("[Rimfolk] New people: " + f.Name + " (" + def.defName + ", " + c.kind + (c.standIn ? ", a stand-in cause" : "") + ") at " + town.Name + ", goodwill " + f.PlayerGoodwill + ".");
            return f;
        }

        /// <summary>A league's town lies between its towns: tiles nearer the farthest of them weigh more.</summary>
        private static Func<PlanetTile, float> Score(ArisingCause c)
        {
            if (c.kind != ArisingMath.League || c.towns.Count < 2) return null;
            var grid = Find.WorldGrid;
            return t => 1f / (1f + c.towns.Max(s => grid.ApproxDistanceInTiles(s.Tile, t)));
        }

        /// <summary>
        /// A tile for a new people's town, <paramref name="min"/> to <paramref name="max"/> tiles' walk from
        /// <paramref name="root"/> on the world's surface: valid for a town, nothing else on it or next to a town being
        /// built, clear of the game's nearness rule around every home of the colony, on no people's land but
        /// <paramref name="kin"/>'s (Faction Territories); with <paramref name="river"/>, on a tile a river runs
        /// through; picked by the biome's settlement weight (and <paramref name="score"/>). Straight-line distances only
        /// inside the search.
        /// </summary>
        public static bool TryTownTile(PlanetTile root, int min, int max, HashSet<Faction> kin, Func<PlanetTile, float> score, out PlanetTile tile, bool river = false)
        {
            tile = PlanetTile.Invalid;
            if (!root.Valid || Find.WorldGrid == null || root.Layer == null || !root.Layer.IsRootSurface) return false;
            var layer = root.Layer;
            int goodwill = RiseFall.GoodwillTiles;
            var grid = Find.WorldGrid;
            var ours = Find.WorldObjects.Settlements.Where(s => s?.Faction != null && s.Faction.IsPlayer && s.Tile.Layer == layer).Select(s => s.Tile).ToList();
            var sites = WorldMechanics.SettlementsBeingBuilt().Where(b => b != null && b.tile.Valid && b.tile.Layer == layer).Select(b => b.tile).ToList();
            var found = new List<PlanetTile>();
            var weights = new List<float>();
            TileFinder.TryFindPassableTileWithTraversalDistance(root, Math.Max(1, min), Math.Max(min, max), out _, t =>
            {
                if (t.Layer != layer || Find.WorldObjects.AnyWorldObjectAt(t)) return false;
                float nearest = -1f;
                foreach (var o in ours)
                {
                    float d = grid.ApproxDistanceInTiles(o, t);
                    if (nearest < 0f || d < nearest) nearest = d;
                }
                if (!RiseFallMath.ClearOfHomes(nearest, goodwill) || sites.Any(s => grid.ApproxDistanceInTiles(s, t) <= 1.5f)) return false;
                if (!TileFinder.IsValidTileForNewSettlement(t) || river && !Peoples.HasRiver(t)) return false;
                var owner = WorldMechanics.TerritoryOwner(t);
                if (owner != null && (kin == null || !kin.Contains(owner))) return false;
                float w = RegionFortunes.BiomeWeight(t) * (score?.Invoke(t) ?? 1f);
                if (w <= 0f) return false;
                found.Add(t);
                weights.Add(w);
                return false;
            });
            int pick = RiseFallMath.PickTile(weights, Rand.Value);
            if (pick < 0) return false;
            tile = found[pick];
            return true;
        }

        /// <summary>What the faith of a new people is, for its letter: its creed's name, and how it differs from the old.</summary>
        public sealed class Detail
        {
            public string faith;
            public MemeDef dropped, added;
        }

        /// <summary>
        /// The people made the game's way (FactionGenerator.NewGeneratedFaction, hidden while it is made so the game
        /// places no town of its own, its leader made once its faith is set), named, its emblem's colour apart from the
        /// world's peoples', its faith by its cause (with Ideology), its goodwill by what happened, then added.
        /// </summary>
        private static Faction Make(FactionDef def, ArisingCause c, out Detail detail)
        {
            detail = new Detail();
            var ideos = Find.IdeoManager != null ? new HashSet<Ideo>(Find.IdeoManager.IdeosListForReading) : new HashSet<Ideo>();
            var parms = new IdeoGenerationParms(def, false, null, null, def.forcedMemes, false, false, false, def.fixedIdeo, def.ideoName ?? "", def.styles, def.deityPresets, def.hiddenIdeo,
                def.ideoDescription ?? "", def.requiredPreceptsOnly);
            Faction f;
            Making = true;
            try
            {
                f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, parms, true));
            }
            finally
            {
                Making = false;
            }
            if (f == null) return null;
            f.hidden = false;
            string name = PeopleName(c, def);
            if (!name.NullOrEmpty()) f.Name = name;
            Safe("its colour", () => f.color = Colour());
            var d = detail;
            Safe("its faith", () => Faith(f, c, ideos, d));
            bool exiles = c.kind == ArisingMath.Exiles && c.exile != null;
            if (!exiles && f.leader == null) Safe("its leader", () => f.TryGenerateNewLeader());
            Safe("its goodwill", () => Relations(f, c));
            Find.FactionManager.Add(f);
            // The one sent away leads the band they gathered (a pawn of the world, of no people until now).
            if (exiles)
                Safe("its leader", () =>
                {
                    c.exile.SetFaction(f);
                    f.leader = c.exile;
                });
            return f;
        }

        /// <summary>The emblem colour farthest from every people's in the world (ArisingMath.Palette).</summary>
        private static Color Colour()
        {
            var taken = Find.FactionManager.AllFactionsListForReading.Where(x => x != null && !x.IsPlayer).Select(x => x.Color).Select(col => new[] { col.r, col.g, col.b }).ToList();
            var p = ArisingMath.Palette[ArisingMath.Colour(taken)];
            return new Color(p[0], p[1], p[2]);
        }

        /// <summary>
        /// Its faith (Ideology): survivors keep their old people's, a band their leader's, a league the faith of another
        /// league of the same ways where there is one (else its own); faithful who broke away found a creed of their own:
        /// their old faith with one teaching dropped or one added, the game's own way of reforming a faith
        /// (IdeoUtility.RandomizeNormalMemesForReforming). A faith the game made for it and nobody holds is let go.
        /// </summary>
        private static void Faith(Faction f, ArisingCause c, HashSet<Ideo> before, Detail detail)
        {
            if (!ModsConfig.IdeologyActive || f.ideos == null || Find.IdeoManager == null) return;
            var made = f.ideos.PrimaryIdeo;
            Ideo faith = null;
            switch (c.kind)
            {
                case ArisingMath.Refugees:
                    faith = c.parent?.ideos?.PrimaryIdeo;
                    break;
                case ArisingMath.Exiles:
                    faith = c.exile?.Ideo;
                    break;
                case ArisingMath.League:
                    faith = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(o => o != f && o.def == f.def && !o.defeated && o.ideos?.PrimaryIdeo != null)?.ideos.PrimaryIdeo;
                    break;
                case ArisingMath.Splinter:
                    faith = Reformed(f, c, detail);
                    break;
            }
            if (faith == null || faith == made) return;
            f.ideos.SetPrimary(faith);
            detail.faith = faith.name;
            if (made != null && !before.Contains(made) && f.leader == null && !Find.FactionManager.AllFactions.Any(o => o.ideos != null && o.ideos.AllIdeos.Contains(made)))
                Find.IdeoManager.Remove(made);
        }

        /// <summary>The splinter's creed: their old faith reformed (one teaching dropped or added), of the same kind of foundation, its own name.</summary>
        private static Ideo Reformed(Faction f, ArisingCause c, Detail detail)
        {
            var old = c.parent?.ideos?.PrimaryIdeo;
            if (old == null || old.memes.NullOrEmpty()) return null;
            var memes = IdeoUtility.RandomizeNormalMemesForReforming(IdeoFoundation.MemeCountRangeAbsolute.max, old.memes, c.parent.def);
            detail.dropped = old.memes.FirstOrDefault(m => !memes.Contains(m));
            detail.added = memes.FirstOrDefault(m => !old.memes.Contains(m));
            if (detail.dropped == null && detail.added == null) return null;
            // Made for their new ways (the template's culture and gear), with the old faith's memes reformed.
            var parms = new IdeoGenerationParms(f.def, false, null, null, memes);
            var foundation = old.foundation?.def ?? DefDatabase<IdeoFoundationDef>.AllDefs.RandomElement();
            var ideo = IdeoGenerator.MakeIdeo(foundation);
            ideo.foundation.Init(parms);
            ideo.primaryFactionColor = f.Color;
            Find.IdeoManager.Add(ideo);
            return ideo;
        }

        /// <summary>
        /// Its goodwill by what happened (ArisingMath): with the colony, and with the people it came from or its league's
        /// peoples. Each within the game's own rules: a people that is someone's enemy for good stays one.
        /// </summary>
        private static void Relations(Faction f, ArisingCause c)
        {
            var player = Faction.OfPlayer;
            int g;
            switch (c.kind)
            {
                case ArisingMath.Refugees:
                    g = ArisingMath.RefugeesGoodwill(c.byColony, c.parent != null && c.parent.HasGoodwill ? c.parent.PlayerGoodwill : 0, c.parent != null && c.parent.HostileTo(player));
                    if (c.parent != null && !c.parent.defeated) Set(f, c.parent, ArisingMath.WithKin);
                    break;
                case ArisingMath.League:
                    g = ArisingMath.LeagueGoodwill(c.towns.Select(t => t.Faction).Distinct().Where(x => x.HasGoodwill).Select(x => x.PlayerGoodwill).ToList());
                    foreach (var m in c.towns.Select(t => t.Faction).Distinct()) Set(f, m, ArisingMath.WithMembers);
                    break;
                case ArisingMath.Exiles:
                    g = ArisingMath.ExilesGoodwill(c.record?.grudge ?? false, c.stoodBy, c.glad);
                    break;
                default:
                    g = ArisingMath.SplinterGoodwill(c.debate);
                    if (c.parent != null) Set(f, c.parent, ArisingMath.WithParent);
                    break;
            }
            Set(f, player, g);
        }

        /// <summary>Goodwill both ways between two peoples, its kind by the game's thresholds; never for a pair that are enemies for good.</summary>
        private static void Set(Faction a, Faction b, int goodwill)
        {
            if (a == null || b == null || a == b) return;
            var mine = a.RelationWith(b, true);
            var theirs = b.RelationWith(a, true);
            if (mine == null || theirs == null || a.def.PermanentlyHostileTo(b.def) || b.def.PermanentlyHostileTo(a.def)) return;
            goodwill = Mathf.Clamp(goodwill, -100, 100);
            var kind = goodwill <= ArisingMath.Hostile ? FactionRelationKind.Hostile : goodwill >= ArisingMath.Ally ? FactionRelationKind.Ally : FactionRelationKind.Neutral;
            mine.baseGoodwill = theirs.baseGoodwill = goodwill;
            mine.kind = theirs.kind = kind;
        }

        // ================= Names =================

        private static IEnumerable<string> PeopleNames => Find.FactionManager.AllFactionsListForReading.Where(x => x?.Name != null).Select(x => x.Name);

        /// <summary>
        /// Its name from its cause (CC_Arise_Name_KIND_CALLING: "Ashford Folk", "Brightwater League", "Barr's Company"), when
        /// short enough and no other people's; the faithful who broke away take a name in their new ways (their template's
        /// rule), as does any that won't fit. Null: the game's own.
        /// </summary>
        private static string PeopleName(ArisingCause c, FactionDef def)
        {
            if (c.kind == ArisingMath.Splinter) return null;
            string calling = Peoples.Of(def)?.calling;
            string what = c.kind == ArisingMath.League ? (c.towns.FirstOrDefault()?.Name ?? "") : c.kind == ArisingMath.Exiles ? (c.exile?.LabelShort ?? c.name ?? "") : c.name ?? "";
            if (what.NullOrEmpty()) return null;
            string key = "CC_Arise_Name_" + c.kind + "_" + calling;
            if (!key.CanTranslate()) return null;
            string name = TextVariants.Vary(key).Translate(what.Named("WHAT")).Resolve();
            return ArisingMath.NameFits(name, PeopleNames) ? name : null;
        }

        /// <summary>Survivors call their town after the one they lost ("New Ashford") when the name is free; else their template's rule.</summary>
        private static string TownName(ArisingCause c, Faction f)
        {
            if (c.kind != ArisingMath.Refugees || c.name.NullOrEmpty()) return null;
            string name = "CC_Arise_TownName_Refugees".Translate(c.name.Named("TOWN")).Resolve();
            return ArisingMath.NameFits(name, Find.WorldObjects.Settlements.Where(s => s.HasName).Select(s => s.Name)) ? name : null;
        }
    }
}
