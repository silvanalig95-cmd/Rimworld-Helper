using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A camp that grows over its stay (CampBuildMath.GrowCeiling, GrowWants, GrowPlace): a day or so after it stands, and a
    // day or so apart from then, they add what a camp of their kind would as it settles in (seats round the fire, a lean-to
    // over the stash, a pen for their beasts, a drying rack, a bench for their trade, a firewood rack, a small altar, a
    // banner at the edge, a latrine, a second store when their goods have no room), each of their people's age and the
    // player's mods (the kits' grows, Rimfolk_CampKits.xml), raised by their own hands from what they carry or gather on
    // the land (CampGather.cs: trees felled for wood, rock mined and dressed into blocks). A short
    // stay stays as it was put up; a long one grows to a settled look within the camp's reach of the fire, never on the
    // colony's ground. When they go, what they added is packed or left by the same rules as the rest (CampPack.cs).
    public partial class Camp
    {
        /// <summary>How many times they have added to the camp, and when they next will (-1: not yet set).</summary>
        public int grown, nextGrowTick = -1;
        /// <summary>
        /// The addition under way (its pieces' role), and when it's given up if nothing has moved on it by then (two days
        /// after the last progress: CampGather.cs).
        /// </summary>
        public string growing;
        public int growUntil = -1;
        /// <summary>Additions tried and found no room or no means for (not saved: after a load they're tried once more).</summary>
        [Unsaved] private HashSet<string> growTried = new HashSet<string>();

        private void ExposeGrow()
        {
            Scribe_Values.Look(ref grown, "grown");
            Scribe_Values.Look(ref nextGrowTick, "nextGrowTick", -1);
            Scribe_Values.Look(ref growing, "growing");
            Scribe_Values.Look(ref growUntil, "growUntil", -1);
        }

        /// <summary>The days they mean to stay (neighbours settled for good: a long stay).</summary>
        private float PlannedDays => holding ? 30f : stayTicks / (float)GenDate.TicksPerDay;

        /// <summary>
        /// What the camp is and has, for what it adds (CampBuildMath.GrowFacts); <paramref name="planning"/>: as it sets out,
        /// before anything stands (for what they carry for it).
        /// </summary>
        public GrowFacts GrowFactsNow(bool planning = false)
        {
            var works = kind?.works;
            var spot = plans.FirstOrDefault(p => p.role == "spot" && !p.dropped && p.def != null);
            var f = new GrowFacts
            {
                people = planning ? members.Count : Mathf.Max(1, Here.Count),
                days = PlannedDays,
                age = CampKit.AgeOf(faction),
                calling = works.NullOrEmpty() ? null : works[0].ToString(),
                hunts = (kind?.huntsPerDay ?? 0f) >= 0.5f || (!works.NullOrEmpty() && works[0] == CampLabour.Hunt),
                beasts = planning ? kind?.animals?.Any(a => a != null && !a.follow && (a.pack || a.kinds?.Count > 0)) ?? false : herd.Any(a => a != null && a.Spawned && !a.Dead && GroupOf(a)?.follow != true),
                faith = ModsConfig.IdeologyActive && faction?.ideos?.PrimaryIdeo != null,
                ownSpot = kind?.structures?.Any(s => s.role == "spot") ?? false,
                workTable = spot?.def?.IsWorkTable ?? false,
                stash = plans.Any(p => p.role == "stash" && !p.dropped && p.def != null),
                dines = kind?.dines ?? true,
                empire = faction?.def != null && faction.def == FactionDefOf.Empire,
                has = r => CampKit.GrowPiece(kind, r, faction)?.Def != null
            };
            if (!planning)
            {
                var (loose, food) = LooseWithoutRoom();
                f.loose = loose;
                f.looseFood = food;
            }
            return f;
        }

        // ---------------- Each hour ----------------

        /// <summary>
        /// Each hour of a settled stay: the addition under way is done once all of it stands (the colony hears what they put
        /// up), and given up only when the land can't give what it needs, nobody of them can build it, or nothing has moved
        /// on it for two days (CampGather.cs); when the next is due, the next their kind would add is laid out and its
        /// blueprints set down. Nothing new in their last day, with a threat on the map, or once things are tense.
        /// </summary>
        private void GrowHour()
        {
            if (phase != CampPhase.Living || packingFor != null || Hostile || map == null || kind == null) return;
            RetryFire();
            if (growing != null)
            {
                var mine = plans.Where(p => p.role == growing && !p.dropped && p.def != null).ToList();
                if (mine.Count > 0 && mine.All(p => p.Built(map, faction) != null))
                {
                    GrowDone(growing, mine);
                    growing = null;
                }
                else
                {
                    string why = mine.Count == 0 ? "gone" : GrowHoldUp(mine);
                    if (why == null) return;
                    if (Prefs.DevMode) Log.Message($"[Rimfolk] {Label} of {faction?.Name}: {GrowWord(growing)} given up ({why}).");
                    GiveUpGrowth(growing);
                    growing = null;
                }
            }
            if (nextGrowTick < 0)
            {
                nextGrowTick = (builtTick >= 0 ? builtTick : Now) + CampBuildMath.GrowGapHours(0, Rand.Value) * GenDate.TicksPerHour;
                return;
            }
            if (Now < nextGrowTick) return;
            if (untilTick != int.MaxValue && !holding && untilTick - Now < CampBuildMath.GrowStopDays * GenDate.TicksPerDay) return;
            if (tension >= 60f || MapFacts.ThreatOn(map) || Hands == 0) return;
            var facts = GrowFactsNow();
            var done = new HashSet<string>(plans.Where(p => !p.dropped && CampBuildMath.GrowRoles.Contains(p.role)).Select(p => p.role));
            done.UnionWith(growTried);
            string role = CampBuildMath.GrowNext(facts, done, grown);
            if (role == null)
            {
                nextGrowTick = Now + GenDate.TicksPerDay;
                return;
            }
            if (!StartGrowth(role))
            {
                // No room for it, or nothing to make it of: the next one at the next hour.
                growTried.Add(role);
                return;
            }
            grown++;
            nextGrowTick = Now + CampBuildMath.GrowGapHours(grown, Rand.Value) * GenDate.TicksPerHour;
        }

        /// <summary>
        /// Their fire given up when they came (something lay on its spot then, as in the player's save of 15 November 2026,
        /// where the surveyors' camp stood with no fire): its blueprint set down again once the ground is clear.
        /// </summary>
        private void RetryFire()
        {
            var fire = plans.FirstOrDefault(p => p.role == "fire" && p.def != null);
            if (fire == null || !fire.dropped || fire.placed || fire.reuse) return;
            foreach (var c in fire.Rect)
                if (!c.InBounds(map) || CampSite.FateAt(map, c, this) == CellFate.Never || c.GetFirstPawn(map) != null) return;
            fire.dropped = false;
            PlaceBlueprint(fire);
        }

        /// <summary>
        /// An addition laid out where it fits (CampBuildMath.GrowPlace) and its blueprints set down, when they have what to
        /// make it of or the land gives it (their goods, what they carry, wood of the trees round them, blocks of the rock
        /// round them: CampGather.cs) and one of them can build it: what they carry for it goes down at the stash, and one
        /// of them says so. False when there's no room or no means.
        /// </summary>
        private bool StartGrowth(string role)
        {
            var piece = CampKit.GrowPiece(kind, role, faction);
            var def = piece?.Def;
            if (def == null) return false;
            int count = CampBuildMath.GrowPieces(role, Here.Count);
            var stuff = GrowStuff(piece, def, role == "fence" ? 16 : count);
            if (def.MadeFromStuff && stuff == null) return false;
            // Nobody of them could build it (the skill it asks): not laid out.
            if (!CanBuildPiece(def)) return false;
            var site = GrowSiteNow(out int turn, out var fire);
            var pieces = CampBuildMath.GrowPlace(role, site, def.size.x, def.size.z, count, herd.Count(a => a != null && a.Spawned && !a.Dead));
            if (pieces == null || pieces.Count == 0) return false;
            var adding = new List<CampPlan>();
            foreach (var p in pieces)
            {
                var plan = CampSite.PlanFor(piece, p, fire, turn);
                if (plan == null) continue;
                plan.role = role;
                plan.stuff = def.MadeFromStuff ? stuff : null;
                adding.Add(plan);
            }
            if (adding.Count == 0 || !Affords(adding)) return false;
            // The same addition given up before (its blueprints taken up then): its old plans make way for the new.
            plans.RemoveAll(p => p.role == role && p.dropped && p.Built(map, faction) == null);
            foreach (var plan in adding)
            {
                plans.Add(plan);
                PlaceBlueprint(plan);
            }
            if (!adding.Any(p => p.placed || p.waiting))
            {
                plans.RemoveAll(adding.Contains);
                return false;
            }
            // What they carry for it goes down at their stash; what they lack of what the land gives is gathered by day as they
            // build (CampGather.cs).
            DropMaterials(new HashSet<ThingDef>(adding.Where(p => p.placed || p.waiting).SelectMany(Costs).Select(c => c.thingDef)));
            growing = role;
            growUntil = Now + CampGatherMath.StallTicks;
            growMark = -1;
            BuildNeedsChanged();
            SayLine(Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && p.Awake()).RandomElementWithFallback(), "grow", 0.6f);
            return true;
        }

        /// <summary>
        /// What an addition is made of (CampGatherMath.ChooseStuff): the first of the piece's listed materials they have
        /// enough of (their goods and what they carry); else wood where the wild trees round them give enough; else stone
        /// blocks of the rock round them where they can dress it; null when the land gives nothing it could be made of.
        /// </summary>
        private ThingDef GrowStuff(CampStructure piece, ThingDef def, int count)
        {
            if (!def.MadeFromStuff) return null;
            var allowed = GenStuff.AllowedStuffsFor(def).ToList();
            var listed = (piece.stuff ?? new List<string>()).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(s => s != null && allowed.Contains(s)).Distinct().ToList();
            var options = new List<ThingDef>(listed);
            if (allowed.Contains(ThingDefOf.WoodLog) && !options.Contains(ThingDefOf.WoodLog)) options.Add(ThingDefOf.WoodLog);
            var stone = LocalBlocks();
            if (stone != null && allowed.Contains(stone) && !options.Contains(stone)) options.Add(stone);
            var goods = BuildGoods();
            var choice = options.Select(s => new StuffOption { listed = listed.Contains(s), facts = FactsFor(s, StuffNeed(def, s, count), goods) }).ToList();
            int i = CampGatherMath.ChooseStuff(choice);
            return i < 0 ? null : options[i];
        }

        /// <summary>How much of a material so many of a piece take.</summary>
        private static int StuffNeed(ThingDef def, ThingDef stuff, int count) =>
            Mathf.Max(1, Mathf.RoundToInt(def.costStuffCount / Mathf.Max(0.01f, stuff.VolumePerUnit))) * Mathf.Max(1, count);

        /// <summary>They have what the pieces need, or the land gives what they lack (CampGatherMath.LandGives): wood of the trees round them, blocks of the rock.</summary>
        private bool Affords(List<CampPlan> adding)
        {
            var goods = BuildGoods();
            foreach (var g in adding.SelectMany(Costs).Where(c => c?.thingDef != null).GroupBy(c => c.thingDef))
                if (!CampGatherMath.LandGives(FactsFor(g.Key, g.Sum(c => c.count), goods))) return false;
            return true;
        }

        /// <summary>The materials of these kinds they still carry go down at their stash, theirs (their trader's stock stays in the pack).</summary>
        private void DropMaterials(HashSet<ThingDef> needs)
        {
            if (needs == null || needs.Count == 0 || map == null || !stash.IsValid) return;
            foreach (var p in Here)
            {
                var inv = p.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv.ToList())
                {
                    if (!needs.Contains(t.def) || (p == trader && !p.inventory.NotForSale(t))) continue;
                    if (inv.TryDrop(t, stash, map, ThingPlaceMode.Near, out var dropped, null, AtStash) && dropped != null) Brought(dropped);
                }
            }
        }

        /// <summary>The addition stands: the colony hears what they put up, a lean-to gets its roof, their beasts move into their pen.</summary>
        private void GrowDone(string role, List<CampPlan> mine)
        {
            if (role == "post") RoofLeanTo();
            if (role == "fence")
            {
                // Their beasts are kept in it from now: its middle is where they're tethered.
                var inside = PenCells();
                if (inside.Count > 0)
                {
                    var mid = new IntVec3(Mathf.RoundToInt((float)inside.Average(c => c.x)), 0, Mathf.RoundToInt((float)inside.Average(c => c.z)));
                    picket = inside.OrderBy(c => c.DistanceToSquared(mid)).First();
                }
            }
            var look = mine.Select(p => p.Built(map, faction)).FirstOrDefault(b => b != null);
            Note(Text("CC_CampGrow_Done", null, null, 0, GrowWord(role)), MessageTypeDefOf.NeutralEvent, Importance.Flavor, look);
        }

        /// <summary>
        /// An addition given up (the land can't give what it needs, nobody of them can build it, or nothing moved on it for
        /// two days): its blueprints and frames taken up, what was put into them back in the stash. Nothing is said of it.
        /// </summary>
        private void GiveUpGrowth(string role)
        {
            var mine = plans.Where(p => p.role == role && !p.dropped).ToList();
            var cells = new HashSet<IntVec3>(mine.Select(p => p.cell));
            foreach (var t in Unfinished())
            {
                if (!cells.Contains(t.Position)) continue;
                if (t is Frame f && f.resourceContainer != null) f.resourceContainer.TryDropAll(stash, map, ThingPlaceMode.Near, (thing, n) => Brought(thing), AtStash);
                if (!t.Destroyed) t.Destroy(DestroyMode.Vanish);
            }
            foreach (var p in mine.Where(p => p.Built(map, faction) == null))
            {
                p.dropped = true;
                p.waiting = false;
            }
            growTried.Add(role);
        }

        /// <summary>The addition in words: seats round their fire, a lean-to over their stash...</summary>
        public static string GrowWord(string role)
        {
            string key = "CC_CampGrow_What_" + role;
            return key.CanTranslate() ? key.Translate().Resolve() : role;
        }

        // ---------------- The camp as it stands ----------------

        /// <summary>
        /// The camp as it stands in its own frame (the fire at (0,0), +z away from the colony), for placing what it adds:
        /// what stands of theirs, their beds, door steps and work spots' places to stand, their huts' and pen's insides, the
        /// stash's ground and shelf, the picket and the butcher's spot; what each cell costs to clear, never on the colony's.
        /// </summary>
        private GrowSite GrowSiteNow(out int turn, out IntVec3 fire)
        {
            var f0 = FireCell;
            fire = f0;
            var colony = colonyCenter.IsValid ? colonyCenter : map.Center;
            int k = CampSite.TurnFor(CampSite.Away(f0, colony));
            turn = k;
            int back = (4 - k) % 4;
            (int x, int z) Frame(IntVec3 c) => CampBuildMath.Turn(c.x - f0.x, c.z - f0.z, back);
            var site = new GrowSite();
            var costs = new Dictionary<IntVec3, int>();
            site.cost = (x, z) =>
            {
                var (wx, wz) = CampBuildMath.Turn(x, z, k);
                var c = new IntVec3(f0.x + wx, 0, f0.z + wz);
                if (!costs.TryGetValue(c, out int v)) costs[c] = v = !c.InBounds(map) || ColonyGround(c) ? -1 : CampBuildMath.Cost(CampSite.FateAt(map, c, this));
                return v;
            };
            foreach (var p in plans)
            {
                if (p.dropped || p.def == null) continue;
                foreach (var c in p.Rect) site.taken.Add(Frame(c));
                if (p.role == "bed" || p.role == "hutbed" || p.role == "tent")
                    foreach (var c in p.Rect) site.beds.Add(Frame(c));
                if (p.role == "door" || p.role == "tentdoor")
                    foreach (var d in GenAdj.CardinalDirections) site.keep.Add(Frame(p.cell + d));
                if (p.def.hasInteractionCell)
                {
                    try
                    {
                        site.keep.Add(Frame(ThingUtility.InteractionCellWhenAt(p.def, p.cell, p.rot, map)));
                    }
                    catch (Exception)
                    {
                        // A mod's piece without a proper place to stand: nothing kept for it.
                    }
                }
            }
            foreach (var c in HutCells) site.keep.Add(Frame(c));
            foreach (var c in PenCells()) site.keep.Add(Frame(c));
            if (stash.IsValid)
            {
                site.stash = Frame(stash);
                var shelf = plans.FirstOrDefault(p => p.role == "stash" && !p.dropped && p.def != null && p.Built(map, faction) != null);
                if (shelf != null)
                {
                    // The stash's ground and its shelf: what a lean-to roofs over; the ground kept for their goods.
                    var all = shelf.Rect.Cells.Concat(new[] { stash }).ToList();
                    for (int x = all.Min(c => c.x); x <= all.Max(c => c.x); x++)
                        for (int z = all.Min(c => c.z); z <= all.Max(c => c.z); z++)
                        {
                            var c = new IntVec3(x, 0, z);
                            site.cover.Add(Frame(c));
                            if (!shelf.Rect.Contains(c)) site.keep.Add(Frame(c));
                        }
                }
                else
                {
                    site.keep.Add(Frame(stash));
                    foreach (var d in GenAdj.AdjacentCellsAround) site.keep.Add(Frame(stash + d));
                }
            }
            var picketPlan = plans.FirstOrDefault(p => p.role == "picket" && !p.dropped && p.def != null);
            if (picketPlan != null) site.picket = Frame(picketPlan.cell);
            else if (herd.Count > 0 && Picket.IsValid) site.picket = Frame(Picket);
            var butcher = plans.FirstOrDefault(p => p.role == "butcher" && !p.dropped && p.def != null);
            if (butcher != null) site.butcher = Frame(butcher.cell);
            return site;
        }

        // ---------------- The lean-to and the pen ----------------

        /// <summary>What a lean-to roofs over besides its posts: the stash's ground and its shelf.</summary>
        private List<(int x, int z)> LeanToCover()
        {
            var list = new List<(int x, int z)>();
            var shelf = plans.FirstOrDefault(p => p.role == "stash" && !p.dropped && p.def != null);
            if (shelf == null || !stash.IsValid) return list;
            var all = shelf.Rect.Cells.Concat(new[] { stash }).ToList();
            for (int x = all.Min(c => c.x); x <= all.Max(c => c.x); x++)
                for (int z = all.Min(c => c.z); z <= all.Max(c => c.z); z++)
                    list.Add((x, z));
            return list;
        }

        /// <summary>The cells a lean-to's posts roof over, when all its posts stand (none while one is down).</summary>
        public List<IntVec3> LeanToRoof()
        {
            var posts = plans.Where(p => p.role == "post" && !p.dropped && p.def != null).ToList();
            if (posts.Count < 2 || map == null || posts.Any(p => p.Built(map, faction) == null)) return new List<IntVec3>();
            return CampBuildMath.Roofed(posts.Select(p => (p.cell.x, p.cell.z)), LeanToCover()).Select(c => new IntVec3(c.x, 0, c.z)).Where(c => c.InBounds(map)).ToList();
        }

        /// <summary>The lean-to's roof put on over the stash once its posts stand (never over a tree, nor under the mountain).</summary>
        private void RoofLeanTo()
        {
            foreach (var c in LeanToRoof())
            {
                var roof = c.GetRoof(map);
                if (roof != null || (c.GetPlant(map)?.def.plant?.IsTree ?? false)) continue;
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }
        }

        /// <summary>The lean-to's roof taken off (before its posts are taken down).</summary>
        private void UnroofLeanTo()
        {
            var posts = plans.Where(p => p.role == "post" && !p.dropped && p.def != null).ToList();
            if (posts.Count < 2 || map == null) return;
            foreach (var (x, z) in CampBuildMath.Roofed(posts.Select(p => (p.cell.x, p.cell.z)), LeanToCover()))
            {
                var c = new IntVec3(x, 0, z);
                if (c.InBounds(map) && map.roofGrid.RoofAt(c) == RoofDefOf.RoofConstructed) map.roofGrid.SetRoof(c, null);
            }
        }

        /// <summary>The patch their pen's fences close in, when most of the fences stand (none otherwise).</summary>
        public List<IntVec3> PenCells()
        {
            var fences = plans.Where(p => p.role == "fence" && !p.dropped && p.def != null).ToList();
            if (fences.Count < 8 || map == null || fences.Count(p => p.Built(map, faction) != null) < fences.Count - 1) return new List<IntVec3>();
            return CampBuildMath.PenInside(fences.Select(p => (p.cell.x, p.cell.z))).Select(c => new IntVec3(c.x, 0, c.z)).Where(c => c.InBounds(map) && c.Standable(map)).ToList();
        }

        /// <summary>One of their seats round the fire, free for this one of them to sit on (none: <see cref="IntVec3.Invalid"/>).</summary>
        public IntVec3 FireSeat(Pawn p)
        {
            if (map == null || p == null) return IntVec3.Invalid;
            foreach (var s in plans.Where(x => x.role == "fireseat" && !x.dropped && x.def != null).InRandomOrder())
            {
                var b = s.Built(map, faction);
                if (b == null) continue;
                var c = b.Position;
                if (c.GetFirstPawn(map) is Pawn o && o != p) continue;
                if (Here.Any(o2 => o2 != p && o2.CurJob != null && o2.CurJob.targetA.Cell == c)) continue;
                if (!p.CanReach(c, Verse.AI.PathEndMode.OnCell, Danger.Some)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        // ---------------- What they carry for it ----------------

        /// <summary>
        /// The materials for what they mean to add over their stay, as they set out (the first of their kind's additions, as
        /// many as the stay allows): carried in their packs with the rest. A firewood rack is of the wood they cut there.
        /// </summary>
        public Dictionary<ThingDef, int> GrowMaterials()
        {
            var totals = new Dictionary<ThingDef, int>();
            try
            {
                var facts = GrowFactsNow(true);
                int ceiling = CampBuildMath.GrowCeiling(facts.people, facts.days);
                foreach (var role in CampBuildMath.GrowWants(facts).Take(ceiling))
                {
                    if (role == "woodpile" || role == "store" || role == "basket") continue;
                    var piece = CampKit.GrowPiece(kind, role, faction);
                    var def = piece?.Def;
                    if (def == null) continue;
                    var stuff = piece.StuffFor(def);
                    int count = role == "fence" ? 16 : CampBuildMath.GrowPieces(role, facts.people);
                    foreach (var cost in Costs(new CampPlan { def = def, stuff = stuff }))
                        if (cost.thingDef != null) totals[cost.thingDef] = (totals.TryGetValue(cost.thingDef, out var n) ? n : 0) + cost.count * count;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp growth materials", e);
            }
            return totals;
        }
    }
}
