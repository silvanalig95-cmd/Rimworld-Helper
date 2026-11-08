using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One who held an old camp's site: their people, their kind of camp, the days, and how they left it.</summary>
    public class CampHold : IExposable
    {
        public Faction faction;
        /// <summary>Their people's name as it was (the people may be gone since).</summary>
        public string people;
        /// <summary>Their kind of camp, in words ("woodcutters"); a band's is null.</summary>
        public string kind;
        /// <summary>The days they came and went.</summary>
        public int from = -1, to = -1;
        /// <summary>left, driven (a band drove them off), sent (the colony sent them off), held (a band that took the site and held it).</summary>
        public string how;
        /// <summary>The people that drove them off, or that they took it from.</summary>
        public string by;

        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref people, "people");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref from, "from", -1);
            Scribe_Values.Look(ref to, "to", -1);
            Scribe_Values.Look(ref how, "how");
            Scribe_Values.Look(ref by, "by");
        }
    }

    /// <summary>
    /// An old camp's site on the colony's land: its constructions left standing, nobody's (the walls, the huts' and tents'
    /// walls and doors, the fire pit, the spots; CampPack.cs), the layout they stood in (the beds' places too: the beds went
    /// with the camp), a name the words use ("the old camp by the water") and who held it, the newest
    /// <see cref="CampGroundMath.MostHolders"/>. Kept per home (CampHome.grounds), the newest CampGroundMath.MostSites.
    /// </summary>
    public class CampGround : IExposable
    {
        public int id;
        public Map map;
        public IntVec3 center = IntVec3.Invalid, stash = IntVec3.Invalid, share = IntVec3.Invalid;
        public string name;
        public List<CampPlan> plans = new List<CampPlan>();
        public List<CampHold> held = new List<CampHold>();
        public int leftTick = -1;
        /// <summary>The camp holding it now (its id), -1 for none.</summary>
        public int holder = -1;

        /// <summary>How many it has beds' places for.</summary>
        public int Beds => plans.Count(p => CampGrounds.IsBed(p.role));

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref center, "center", IntVec3.Invalid);
            Scribe_Values.Look(ref stash, "stash", IntVec3.Invalid);
            Scribe_Values.Look(ref share, "share", IntVec3.Invalid);
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref plans, "plans", LookMode.Deep);
            Scribe_Collections.Look(ref held, "held", LookMode.Deep);
            Scribe_Values.Look(ref leftTick, "leftTick", -1);
            Scribe_Values.Look(ref holder, "holder", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plans ??= new List<CampPlan>();
                held ??= new List<CampHold>();
                plans.RemoveAll(p => p == null || p.def == null);
                held.RemoveAll(h => h == null);
            }
        }
    }

    /// <summary>
    /// Old camps' sites (CampGroundMath): recorded as a camp goes, its constructions left standing; chosen by a new camp
    /// that moves in (the standing pieces theirs again, mended by their own hands, the missing built anew); let go once the
    /// colony claims them, takes them down or builds up to them; contested, when a rival comes for one or two parties
    /// arrive for it together (CampRaid.cs). The colony can claim a site's buildings with the game's own claim: then it is
    /// no camp's site any more. Or it lets them stand for camps to come back to.
    /// </summary>
    public static class CampGrounds
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A bed's place in a layout (the bedroll on the ground, a hut's bed, a tent that is a bed).</summary>
        public static bool IsBed(string role) => role == "bed" || role == "hutbed" || role == "tent";

        /// <summary>What is built anew when it no longer stands: the shelter, the fire, the windbreak, the light, the table and seats.</summary>
        private static bool Core(string role) =>
            role == "fire" || role == "hut" || role == "door" || role == "tentwall" || role == "tentdoor" || role == "wall" || role == "light" || role == "table" || role == "seat";

        /// <summary>A construction that tells a site (a bed or other furniture goes with whoever leaves).</summary>
        private static bool Stays(string role) => !IsBed(role) && role != "table" && role != "seat";

        /// <summary>The old sites on this home's map.</summary>
        public static IEnumerable<CampGround> On(Map map)
        {
            var t = CampTracker.Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return Enumerable.Empty<CampGround>();
            return t.HomeOf(map).grounds.Where(g => g != null && g.map == map);
        }

        /// <summary>The old site with this id on any home, or null.</summary>
        public static CampGround ById(int id)
        {
            if (id < 0) return null;
            var t = CampTracker.Instance;
            return t?.homes?.SelectMany(h => h?.grounds ?? new List<CampGround>()).FirstOrDefault(g => g != null && g.id == id);
        }

        /// <summary>The site's piece standing where its plan has it, nobody's (null if it is gone, or someone's).</summary>
        public static Building StandingAt(Map map, CampPlan p, Faction owner = null)
        {
            if (map == null || p?.def == null || !p.cell.InBounds(map)) return null;
            foreach (var t in p.cell.GetThingList(map))
                if (t is Building b && b.def == p.def && b.Faction == owner && !b.Destroyed) return b;
            return null;
        }

        /// <summary>Any piece of it the colony's now: claimed with the game's own claim.</summary>
        private static bool Claimed(CampGround g) => g.plans.Any(p => Stays(p.role) && StandingAt(g.map, p, Faction.OfPlayer) != null);

        /// <summary>The colony's land, zones or buildings have come up to it.</summary>
        private static bool ColonyNear(CampGround g)
        {
            var map = g.map;
            if (map == null || !g.center.InBounds(map)) return true;
            var home = map.areaManager.Home;
            foreach (var c in GenRadial.RadialCellsAround(g.center, CampSite.CoreRadius - 2, true))
                if (c.InBounds(map) && (c.x + c.z) % 2 == 0 && ((home != null && home[c]) || map.zoneManager.ZoneAt(c) != null)) return true;
            return map.listerBuildings.allBuildingsColonist.Any(b => b.Position.InHorDistOf(g.center, CampSite.CoreRadius) && !g.plans.Any(p => p.cell == b.Position));
        }

        /// <summary>What the rules need to know of a site now (CampGroundMath.Worth).</summary>
        public static GroundFacts Facts(CampGround g, Camp except = null)
        {
            var f = new GroundFacts { beds = g.Beds, claimed = Claimed(g), colonyNear = ColonyNear(g) };
            foreach (var p in g.plans.Where(p => Stays(p.role)))
            {
                f.pieces++;
                if (StandingAt(g.map, p) == null) continue;
                f.standing++;
                if (p.role == "fire") f.fireStands = true;
                if (p.role == "hut" || p.role == "door" || p.role == "tentwall" || p.role == "tentdoor" || p.role == "wall") f.shelterStands = true;
            }
            var holding = g.holder >= 0 ? CampTracker.Instance?.ById(g.holder) : null;
            f.campNear = holding != null && holding.Active || CampCrowds.Fires(g.map, except).Any(c => !CampCrowdMath.FarEnough(c.x, c.z, g.center.x, g.center.z));
            return f;
        }

        /// <summary>
        /// An old site this kind of camp of these people would move into now, or null: by the chance the setting's level
        /// gives (CampGroundMath.ReuseChance), a site worth it (CampGroundMath.Worth) for a kind that pitches anywhere and
        /// leaves its constructions standing; one they held before likeliest, then the one with most still standing.
        /// </summary>
        public static CampGround Choose(Map map, CampKindDef kind, Faction f, string seek)
        {
            if (map == null || kind == null || f == null || kind.leaves != CampLeaves.Standing || kind.fights) return null;
            // A kind that pitches by what it came for (the water, the ruins, the monolith, the edge), or travellers after ruins or ore, go there.
            if (!kind.needs.NullOrEmpty() && kind.needs != "harvest" && kind.needs != "fleece" && kind.needs != "children") return null;
            if (seek == TravelMath.Ruins || seek == TravelMath.Ore) return null;
            var all = On(map).ToList();
            if (all.Count == 0 || !Rand.Chance(CampGroundMath.ReuseChance(CampCrowds.Level))) return null;
            var worth = all.Select(g => (g, facts: Facts(g))).Where(x => CampGroundMath.Worth(x.facts, Mathf.Max(1, kind.people.min))
                                                                        && !x.g.held.Any(h => h.faction != null && h.faction != f && h.how == "held" && h.faction.HostileTo(f))).ToList();
            if (worth.Count == 0) return null;
            return worth.RandomElementByWeight(x => x.facts.standing * (x.g.held.Any(h => h.faction == f) ? 3f : 1f)).g;
        }

        /// <summary>
        /// Their layout at the old site: its standing pieces taken over (CampPlan.reuse), what is gone of its shelter, fire,
        /// windbreak, lights and table built anew as it was, a bed in each of its beds' places for as many as they are (their
        /// own kind's bed where it fits the place). Null when it has no beds' places left.
        /// </summary>
        public static List<CampPlan> Adopt(CampGround g, Map map, CampKindDef kind, Faction f, int people, out IntVec3 stash, out IntVec3 share)
        {
            stash = g.stash.IsValid ? g.stash : g.center;
            share = g.share.IsValid ? g.share : g.center;
            if (map == null || g.map != map) return null;
            var bed = CampKit.Piece(kind, "bed", f);
            var plans = new List<CampPlan>();
            int beds = 0;
            foreach (var p in g.plans)
            {
                if (p?.def == null) continue;
                if (IsBed(p.role))
                {
                    if (beds >= people) continue;
                    beds++;
                    var def = p.def;
                    var stuff = p.stuff;
                    // Their own bedroll where it fits the place (a tent that is a bed stays the tent it was).
                    var own = bed?.Def;
                    if (own != null && !CampSite.IsTent(p.def) && own.size == p.def.size)
                    {
                        def = own;
                        stuff = bed.StuffFor(own);
                    }
                    plans.Add(new CampPlan { role = p.role, def = def, stuff = stuff, cell = p.cell, rot = p.rot, room = p.room });
                    continue;
                }
                if (StandingAt(map, p) != null) plans.Add(new CampPlan { role = p.role, def = p.def, stuff = p.stuff, cell = p.cell, rot = p.rot, room = p.room, reuse = true });
                else if (Core(p.role)) plans.Add(new CampPlan { role = p.role, def = p.def, stuff = p.stuff, cell = p.cell, rot = p.rot, room = p.room });
            }
            return beds == 0 || !plans.Any(p => p.role == "fire") ? null : plans;
        }

        /// <summary>A new camp is coming to the old site: theirs to hold from now.</summary>
        public static void Moving(Camp c, CampGround g)
        {
            if (c == null || g == null) return;
            c.groundId = g.id;
            g.holder = c.id;
            // A second party for the same site, the same day (CampGroundMath.TogetherChance): it gives way or comes to blows (Camp.RivalHour).
            try
            {
                bool contested = CampGroundMath.Contested(Holds(g));
                if (!Rand.Chance(CampGroundMath.TogetherChance(CampCrowds.Level, contested))) return;
                var may = CampRaids.Attackers(c, g);
                var rival = may.Count == 0 ? null : may.RandomElementByWeight(x => CampRaidMath.Weight(x.facts)).f;
                bool atWar = rival != null;
                if (!atWar) rival = Arrivals.Friendly(x => x != c.faction && CampCrowds.AtPeaceHere(c.map, x) && x.HostileTo(c.faction) == false);
                if (rival == null) return;
                // Peoples at war come to blows; another gives way when the land has room for a second camp beside this one.
                var end = CampGroundMath.Together(atWar, CampCrowdMath.Full(CampCrowds.HereAt(c.map), CampCrowds.CapAt(c.map)) == null);
                if (end == TogetherEnd.None) return;
                c.rivalPeople = rival;
                c.rivalAt = Now + Rand.RangeInclusive(1, 3) * GenDate.TicksPerHour;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("two for one old camp", e);
            }
        }

        /// <summary>The site's holders as the rules read them.</summary>
        public static List<HoldFacts> Holds(CampGround g) =>
            g?.held?.Select(h => new HoldFacts { people = h.faction?.loadID ?? (h.people ?? "").GetHashCode(), how = h.how }).ToList() ?? new List<HoldFacts>();

        /// <summary>
        /// They arrived and took over what stands of the old site: each piece theirs again (the game's own building, its
        /// faction set to theirs), in their group's keeping; one gone since is built anew. The colony hears what they took over.
        /// </summary>
        public static void TakeOver(Camp c)
        {
            if (c?.map == null || c.groundId < 0) return;
            var g = ById(c.groundId);
            var took = new List<string>();
            foreach (var p in c.plans.Where(p => p.reuse && !p.placed && !p.dropped))
            {
                var b = StandingAt(c.map, p);
                if (b == null)
                {
                    p.reuse = false;
                    continue;
                }
                try
                {
                    b.SetFaction(c.faction);
                    if (c.LordAlive) c.lord.AddBuilding(b);
                    p.placed = true;
                    string word = Word(c, p);
                    if (word != null && !took.Contains(word)) took.Add(word);
                }
                catch (Exception e)
                {
                    p.reuse = false;
                    CouncilLog.ErrorOnce("camp took over " + p.def?.defName, e);
                }
            }
            if (took.Count == 0) return;
            c.Note(Says(c, "CC_CampGround_TookOver", g?.name, TextUtil.JoinAnd(took.Take(4).ToList())), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }

        /// <summary>A camp's text with {GROUND} (the old site's name) and {THING}, besides the camp's own placeholders.</summary>
        public static string Says(Camp c, string key, string ground, string thing = null) =>
            c.Format(key.Translate().RawText.Replace("{GROUND}", ground ?? ""), null, null, 0, thing);

        /// <summary>A piece taken over in the words: their hut, the old tents, the fire pit, the windbreak...</summary>
        private static string Word(Camp c, CampPlan p)
        {
            switch (p.role)
            {
                case "hut":
                case "door":
                    return (c.kind?.sleep == CampSleepForm.Tents ? "CC_CampLeft_What_tents" : "CC_CampLeft_What_hut").Translate();
                case "tentwall":
                case "tentdoor":
                    return "CC_CampLeft_What_tents".Translate();
                case "fire":
                case "wall":
                case "light":
                case "picket":
                case "stash":
                case "stall":
                    return ("CC_CampLeft_What_" + p.role).Translate();
                case "spot":
                    return "CC_CampLeft_What_spot".Translate(p.def.label.Named("THING"));
                default:
                    return null;
            }
        }

        /// <summary>
        /// The camp is gone: what it left standing, nobody's, is an old site (or the site it held, its history added to); a
        /// camp that left nothing standing leaves no site, unless it held one. The newest CampGroundMath.MostSites are kept.
        /// </summary>
        public static void Left(Camp c)
        {
            if (c?.map == null || c.kind == null || c.plans == null) return;
            var t = CampTracker.Instance;
            if (t == null || !Homes.IsHome(c.map)) return;
            try
            {
                var home = t.HomeOf(c.map);
                var g = home.grounds.FirstOrDefault(x => x.id == c.groundId);
                bool stands = c.plans.Any(p => !p.dropped && Stays(p.role) && StandingAt(c.map, p) != null);
                if (g == null)
                {
                    if (!stands || c.arrivedTick < 0) return;
                    g = new CampGround { id = t.nextId++, map = c.map, center = c.FireCell, name = NameFor(c, home) };
                    home.grounds.Add(g);
                }
                g.plans = c.plans.Where(p => !p.dropped && p.def != null)
                    .Select(p => new CampPlan { role = p.role, def = p.def, stuff = p.stuff, cell = p.cell, rot = p.rot, room = p.room }).ToList();
                g.stash = c.stash;
                g.share = c.share;
                g.leftTick = Now;
                g.holder = -1;
                g.held.Add(new CampHold
                {
                    faction = c.faction, people = c.faction?.Name, kind = c.kind.label, from = c.arrivedTick >= 0 ? c.arrivedTick / GenDate.TicksPerDay + 1 : GenDate.DaysPassed,
                    to = GenDate.DaysPassed + 1, how = c.drivenBy != null ? "driven" : CampGroundMath.HowLeft(c.ending), by = c.drivenBy
                });
                g.held.RemoveRange(0, CampGroundMath.Drop(g.held.Count));
                while (home.grounds.Count > CampGroundMath.MostSites) home.grounds.Remove(home.grounds.OrderBy(x => x.leftTick).First());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("an old camp's site", e);
            }
        }

        /// <summary>A band took a site from those holding it and held it a while (CampRaid.cs): in its history.</summary>
        public static void HeldBy(CampGround g, Faction band, string from)
        {
            if (g == null || band == null) return;
            g.held.Add(new CampHold { faction = band, people = band.Name, from = GenDate.DaysPassed + 1, to = GenDate.DaysPassed + 1, how = "held", by = from });
            g.held.RemoveRange(0, CampGroundMath.Drop(g.held.Count));
        }

        /// <summary>
        /// A name the words can use: by what it stands near (the water, the trees, the rocks), else by those who made it ("the
        /// old camp of the woodcutters"); with the way it lies from the colony when another site has the same name.
        /// </summary>
        private static string NameFor(Camp c, CampHome home)
        {
            var map = c.map;
            var at = c.FireCell;
            string key = "CC_CampGround_Name_kind";
            int water = 0, trees = 0, rock = 0;
            foreach (var x in GenRadial.RadialCellsAround(at, 11f, true))
            {
                if (!x.InBounds(map) || (x.x + x.z) % 2 != 0) continue;
                var terrain = x.GetTerrain(map);
                if (terrain != null && terrain.IsWater) water++;
                if (x.GetPlant(map) is Plant p && p.def.plant.IsTree) trees++;
                if (x.GetFirstMineable(map) != null) rock++;
            }
            if (water >= 6) key = "CC_CampGround_Name_water";
            else if (rock >= 8) key = "CC_CampGround_Name_rock";
            else if (trees >= 10) key = "CC_CampGround_Name_trees";
            string name = key.Translate(c.kind.label.Named("KIND")).Resolve();
            if (home.grounds.Any(g => g.name == name)) name = "CC_CampGround_NamePlace".Translate(name.Named("NAME"), (c.place ?? "").Named("PLACE")).Resolve();
            return name;
        }

        /// <summary>
        /// Once a day: a site is let go once nothing of it stands, the colony has claimed it (the game's own claim) or built
        /// up to it; a claim is told once. A holder gone from it no longer holds it.
        /// </summary>
        public static void Daily(CampHome h)
        {
            if (h?.grounds == null || h.grounds.Count == 0) return;
            foreach (var g in h.grounds.ToList())
            {
                try
                {
                    if (g.map == null || !Find.Maps.Contains(g.map))
                    {
                        h.grounds.Remove(g);
                        continue;
                    }
                    if (g.holder >= 0 && !(CampTracker.Instance?.ById(g.holder)?.Active ?? false)) g.holder = -1;
                    if (g.holder >= 0) continue;
                    var f = Facts(g);
                    if (CampGroundMath.StillASite(f.standing, f.claimed, f.colonyNear)) continue;
                    h.grounds.Remove(g);
                    if (f.claimed)
                        Tell.Minor(NewsKind.World, Importance.Flavor, g.map, "CC_CampGround_Claimed".Translate(g.name.Named("GROUND")).Resolve(), new LookTargets(new TargetInfo(g.center, g.map)));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("an old camp's site, its day", e);
                }
            }
        }

        // ---------------- Words ----------------

        /// <summary>One holder in the history's words: "the woodcutters of the Ashen, days 25 to 28, driven off by the Red Fangs".</summary>
        public static string HoldWords(CampHold h)
        {
            var args = new[]
            {
                (h.kind ?? "").Named("KIND"), (h.faction?.Name ?? h.people ?? "?").Named("PEOPLE"), h.from.Named("FROM"), h.to.Named("TO"), (h.by ?? "?").Named("BY")
            };
            string key = "CC_CampGround_Hold_" + (h.how ?? "left");
            if (!key.CanTranslate()) key = "CC_CampGround_Hold_left";
            if (h.from == h.to && (key == "CC_CampGround_Hold_left" || key == "CC_CampGround_Hold_driven" || key == "CC_CampGround_Hold_sent")) key += "Day";
            return key.Translate(args).Resolve();
        }

        /// <summary>A site's history in one line: its name, who held it and how they left, the newest last.</summary>
        public static string HistoryLine(CampGround g)
        {
            if (g == null) return null;
            string holds = g.held.Count == 0 ? null : string.Join("; ", g.held.Select(HoldWords));
            var holding = g.holder >= 0 ? CampTracker.Instance?.ById(g.holder) : null;
            string now = holding != null && holding.Active ? "CC_CampGround_HeldNow".Translate(holding.Label.Named("NAME"), (holding.faction?.Name ?? "").Named("FACTION")).Resolve() : null;
            string line = g.name.CapitalizeFirst() + (holds == null ? "" : " (" + holds + ")");
            if (now != null) line += ": " + now;
            if (CampGroundMath.Contested(Holds(g))) line += " " + "CC_CampGround_Contested".Translate().Resolve();
            return line;
        }

        /// <summary>The camp's letter's line when they mean to move into an old site: what stands there for them.</summary>
        public static string FactsLine(Camp c)
        {
            if (c?.map == null || c.groundId < 0) return null;
            var g = ById(c.groundId);
            if (g == null) return null;
            var stands = c.plans.Where(p => p.reuse).Select(p => Word(c, p)).Where(w => w != null).Distinct().Take(4).ToList();
            if (stands.Count == 0) return null;
            string line = Says(c, "CC_CampGround_Facts", g.name, TextUtil.JoinAnd(stands));
            var before = g.held.LastOrDefault();
            if (before != null && before.faction == c.faction) line += " " + c.Text("CC_CampGround_FactsTheirs");
            else if (before != null) line += " " + "CC_CampGround_FactsBefore".Translate(HoldWords(before).Named("WHO")).Resolve();
            return line;
        }
    }
}
