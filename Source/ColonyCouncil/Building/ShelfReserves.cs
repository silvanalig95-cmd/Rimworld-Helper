using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// One kind a shelf keeps slots for: <see cref="kind"/> is "def:Name" for an item, "cat:Name" for any of a group
    /// (meals, drugs...), "best:Name" for the best of a group the colony has at hand (a lavish meal before a fine one,
    /// glitterworld medicine first). <see cref="slots"/> slots are kept for it; with <see cref="only"/> it takes no more
    /// than those; <see cref="perStack"/> (0: a full stack) is the most of it in one stack on this shelf; and while
    /// <see cref="when"/> doesn't hold ("patient": someone lies ill or hurt in the shelf's room), it isn't kept there at all.
    /// </summary>
    public class ShelfKind : IExposable
    {
        public const string WhenPatient = "patient";

        public string kind;
        public int slots = 1;
        public bool only;
        public int perStack;
        public string when = "";

        public ShelfKind Copy() => (ShelfKind)MemberwiseClone();

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref slots, "slots", 1);
            Scribe_Values.Look(ref only, "only");
            Scribe_Values.Look(ref perStack, "perStack");
            Scribe_Values.Look(ref when, "when", "");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                slots = Math.Max(1, slots);
                perStack = Math.Max(0, perStack);
                when ??= "";
            }
        }
    }

    /// <summary>One shelf's reserved kinds. Saves from 0.23.0 and 0.23.1 kept a name per slot ("kinds"); they load as entries.</summary>
    public class ShelfReserve : IExposable
    {
        public Building_Storage shelf;
        public List<ShelfKind> entries = new List<ShelfKind>();

        public void ExposeData()
        {
            Scribe_References.Look(ref shelf, "shelf");
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<string> kinds = null;
                Scribe_Collections.Look(ref kinds, "kinds", LookMode.Value);
                if (kinds != null && entries.NullOrEmpty())
                    entries = kinds.Where(k => !k.NullOrEmpty()).GroupBy(k => k).Select(g => new ShelfKind { kind = g.Key, slots = g.Count() }).ToList();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                entries ??= new List<ShelfKind>();
                entries.RemoveAll(e => e == null || e.kind.NullOrEmpty());
            }
        }
    }

    /// <summary>
    /// Reserved slots on the colony's shelves (the setting "Reserved slots on shelves"): a shelf keeps the slots set with
    /// its "Reserved slots" button (ShelfKind: an item, any of a group, or the best of a group at hand), each for a stack
    /// of its own, as long as the colony has some of that kind on the map; the other slots take whatever comes. Haulers
    /// put a new stack of anything else on it only while a slot stays free for every reserved one still empty
    /// (ShelfReserveMath.MayTakeSlot); a kind kept to its slots takes no more; a kind held to a number per stack is
    /// topped up to that and no further (the hauler carries no more than the cell has room for); and a kind whose
    /// condition doesn't hold is kept off the shelf. What must go (to make room, past a cap, or kept off for now) counts
    /// as not belonging there, so a hauler takes it to the next place that will have it (MoveOut), or sets it down beside
    /// the shelf when nothing will and a reserved kind waits for its room (ShelfMakeRoom.cs, which also lets a colonist
    /// ordered to haul a reserved kind to a full shelf make room first). A reserved kind comes from loose stacks in the
    /// home area and from storage of the shelf's priority or lower: lower the game's own hauling empties by itself (the
    /// shelf is better storage), the same Rimfolk fetches (Patch_ShelfReserveFetch, WantedOn, FetchJob), and the game
    /// never carries it back, as a haul needs strictly better storage; storage of a higher priority keeps what it holds.
    /// "Any" of meals or of medicine is kept best first, in the order colonists pick their food (RankOf): the better
    /// stacks fill its slots, an empty slot takes nothing less than the best that would come, and a better kind that
    /// turns up takes a lesser stack's slot, into a free slot or with the lesser carried back to the store (a swap;
    /// never between kinds ranked alike, and not for a day once a better kind waited for didn't come). Looked at again at
    /// most every two seconds, and at once when what the shelf holds changes. Works the same with LWM's Deep Storage,
    /// whose shelves count their stacks through the game's own number per cell.
    /// </summary>
    public class ShelfReserves : MapComponent
    {
        private List<ShelfReserve> reserves = new List<ShelfReserve>();

        /// <summary>Every shelf with a reserved kind, on any map: the game's hooks look here first, so they cost nothing otherwise.</summary>
        internal static readonly HashSet<Building_Storage> Reserved = new HashSet<Building_Storage>();

        private readonly Dictionary<Building_Storage, State> states = new Dictionary<Building_Storage, State>();

        public ShelfReserves(Map map) : base(map)
        {
        }

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.shelfReserves ?? true);

        public static ShelfReserves Of(Map map) => map?.GetComponent<ShelfReserves>();

        /// <summary>The shelf's entries (a copy to change and give back to <see cref="Set"/>).</summary>
        public List<ShelfKind> EntriesOf(Building_Storage shelf) =>
            reserves.FirstOrDefault(r => r.shelf == shelf)?.entries.Select(e => e.Copy()).ToList() ?? new List<ShelfKind>();

        public void Set(Building_Storage shelf, List<ShelfKind> entries)
        {
            if (shelf == null) return;
            reserves.RemoveAll(r => r.shelf == shelf);
            var list = entries?.Where(e => e != null && !e.kind.NullOrEmpty()).Select(e => e.Copy()).ToList();
            if (list != null && list.Count > 0) reserves.Add(new ShelfReserve { shelf = shelf, entries = list });
            states.Remove(shelf);
            foreach (var key in waits.Keys.Where(k => k.Item1 == shelf).ToList()) waits.Remove(key);
            foreach (var key in rests.Keys.Where(k => k.Item1 == shelf).ToList()) rests.Remove(key);
            RebuildAll();
        }

        /// <summary>Every shelf given gets the same entries.</summary>
        public static void SetAll(List<Building_Storage> shelves, List<ShelfKind> entries)
        {
            foreach (var s in shelves)
                if (s != null && !s.Destroyed) Of(s.Map)?.Set(s, entries);
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) reserves.RemoveAll(r => r?.shelf == null || r.shelf.Destroyed || r.entries.NullOrEmpty());
            Scribe_Collections.Look(ref reserves, "reserves", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                reserves ??= new List<ShelfReserve>();
                reserves.RemoveAll(r => r?.shelf == null || r.entries.NullOrEmpty());
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildAll();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            reserves.Clear();
            RebuildAll();
        }

        private static void RebuildAll()
        {
            Reserved.Clear();
            if (Current.Game?.Maps == null) return;
            foreach (var m in Current.Game.Maps)
            {
                var c = Of(m);
                if (c == null) continue;
                foreach (var r in c.reserves)
                    if (r?.shelf != null && !r.shelf.Destroyed && r.entries.Count > 0) Reserved.Add(r.shelf);
            }
        }

        // ---------------- What a shelf holds, and what it is waiting for ----------------

        /// <summary>A reserved kind, read from its name.</summary>
        public class Kind
        {
            public string raw;
            public ThingDef def;
            public ThingCategoryDef cat;
            /// <summary>The best of the group at hand; <see cref="targets"/> are those kinds, worked out when the shelf is looked at.</summary>
            public bool best;
            public HashSet<ThingDef> targets;

            public bool Matches(ThingDef d)
            {
                if (d == null) return false;
                if (def != null) return d == def;
                if (best) return targets != null && targets.Contains(d);
                return cat != null && d.IsWithinCategory(cat);
            }

            public string Label =>
                def != null ? def.LabelCap.Resolve()
                : cat == null ? raw
                : (best ? "CC_Shelf_BestOf" : "CC_Shelf_AnyOf").Translate(cat.label.Named("GROUP")).Resolve().CapitalizeFirst();

            public Texture2D Icon => def?.uiIcon ?? cat?.icon;

            public IEnumerable<ThingDef> Defs =>
                def != null ? new[] { def } : best ? (IEnumerable<ThingDef>)targets ?? Enumerable.Empty<ThingDef>() : cat?.DescendantThingDefs ?? Enumerable.Empty<ThingDef>();

            /// <summary>The kind, or null for a name that isn't one (or names a def or group no longer loaded).</summary>
            public static Kind Parse(string raw)
            {
                if (raw.NullOrEmpty()) return null;
                Kind k = null;
                if (raw.StartsWith("def:")) k = new Kind { raw = raw, def = DefDatabase<ThingDef>.GetNamedSilentFail(raw.Substring(4)) };
                else if (raw.StartsWith("cat:")) k = new Kind { raw = raw, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(4)) };
                else if (raw.StartsWith("best:")) k = new Kind { raw = raw, best = true, cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(raw.Substring(5)) };
                return k != null && (k.def != null || k.cat != null) ? k : null;
            }
        }

        // ---------------- Best first ----------------

        /// <summary>Food a colonist eats as a meal: no drug and no medicine, raw food or better (not a drink, a dessert, or what is eaten only when starving).</summary>
        public static bool EatenAsMeal(ThingDef d) =>
            d?.ingestible != null && !d.IsDrug && !d.IsMedicine && d.ingestible.preferability >= FoodPreferability.RawBad;

        private static readonly Dictionary<ThingDef, double> rankOf = new Dictionary<ThingDef, double>();

        /// <summary>
        /// How well a kind ranks: medicine by its potency; food in the order colonists pick it (ShelfReserveMath.FoodRank:
        /// a meal before raw food, then the mood its taste gives and its own offset for people, as the game's
        /// FoodOptimality weighs them, then its preferability and worth).
        /// </summary>
        public static double RankOf(ThingDef d)
        {
            if (d == null) return ShelfReserveMath.Unranked;
            if (rankOf.TryGetValue(d, out double r)) return r;
            if (d.IsMedicine) r = d.GetStatValueAbstract(StatDefOf.MedicalPotency);
            else if (d.ingestible == null) r = ShelfReserveMath.Unranked;
            else
            {
                var ing = d.ingestible;
                var stages = ing.tasteThought?.stages;
                float mood = stages != null && stages.Count > 0 && stages[0] != null ? stages[0].baseMoodEffect : 0f;
                r = ShelfReserveMath.FoodRank(ing.preferability >= FoodPreferability.MealAwful, (int)ing.preferability, mood, ing.optimalityOffsetHumanlikes, d.BaseMarketValue);
            }
            rankOf[d] = r;
            return r;
        }

        /// <summary>The kinds of a group the shelf may hold.</summary>
        private static List<ThingDef> AllowedIn(ThingCategoryDef cat, Building_Storage shelf) =>
            cat?.DescendantThingDefs.Distinct().Where(d => shelf.settings.AllowedToAccept(d)).ToList() ?? new List<ThingDef>();

        /// <summary>The ranked kinds among these (ShelfReserveMath.RankedKinds): all of them when all are medicine, else the meals of a group with no medicine that is at least half meals.</summary>
        public static List<ThingDef> RankedOf(List<ThingDef> allowed)
        {
            if (allowed == null) return new List<ThingDef>();
            return ShelfReserveMath.RankedKinds(allowed.Select(d => d.IsMedicine).ToList(), allowed.Select(EatenAsMeal).ToList()).Select(i => allowed[i]).ToList();
        }

        /// <summary>"The best at hand" is offered for a group with two or more ranked kinds the shelf may hold; the others (drinks and desserts among meals) don't count.</summary>
        public static bool RankableGroup(List<ThingDef> allowed) =>
            allowed != null && ShelfReserveMath.OffersBest(allowed.Select(d => d.IsMedicine).ToList(), allowed.Select(EatenAsMeal).ToList());

        /// <summary>"Any" of a group is kept best first when it has a ranked kind the shelf may hold.</summary>
        public static bool RankedGroup(List<ThingDef> allowed) => RankedOf(allowed).Count > 0;

        /// <summary>
        /// The ranked kinds of the group the shelf may hold that rank highest among those on the shelf or that would come
        /// to it; when none would, among those on the map (kept in storage of a higher priority, as the list then says).
        /// </summary>
        private HashSet<ThingDef> BestAtHand(ThingCategoryDef cat, Building_Storage shelf, HashSet<ThingDef> onShelf, StoragePriority priority)
        {
            var defs = RankedOf(AllowedIn(cat, shelf));
            var ranks = defs.Select(RankOf).ToList();
            var best = ShelfReserveMath.Best(ranks, defs.Select(d => onShelf.Contains(d) || Fetchable(d, shelf, priority)).ToList());
            if (best.Count == 0) best = ShelfReserveMath.Best(ranks, defs.Select(d => OnMapDef(d, shelf)).ToList());
            return new HashSet<ThingDef>(best.Select(i => defs[i]));
        }

        /// <summary>How long an empty slot of "any" kept best first has waited for a better kind, and how many of its slots were empty then.</summary>
        private struct Wait
        {
            public int since, missing;
        }

        /// <summary>Per shelf and kind (not saved): the wait for a better kind, and until when a better kind that never came is left out.</summary>
        private readonly Dictionary<(Building_Storage, string), Wait> waits = new Dictionary<(Building_Storage, string), Wait>();
        private readonly Dictionary<(Building_Storage, string), int> rests = new Dictionary<(Building_Storage, string), int>();

        // ---------------- Conditions ----------------

        /// <summary>Someone lies ill or hurt in a bed in this room: a patient being treated or recovering (animals in their beds too).</summary>
        private bool PatientIn(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors) return false;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || !p.InBed()) continue;
                var bed = p.CurrentBed();
                if (bed == null || bed.GetRoom() != room) continue;
                if (p.Downed || HealthAIUtility.ShouldSeekMedicalRest(p) || p.health.HasHediffsNeedingTend()) return true;
            }
            return false;
        }

        public class State
        {
            public int tick, held, capacity, waiting;
            public List<ShelfKind> entries = new List<ShelfKind>();
            /// <summary>Per entry: its kind (null if it names nothing loaded), whether it counts now (its condition holds), and its stacks on the shelf.</summary>
            public Kind[] kindOf;
            public bool[] active;
            public int[] stacksOf;
            /// <summary>One per reserved slot of the entries that count now, and the entry each belongs to.</summary>
            public List<Kind> kinds = new List<Kind>();
            public List<int> slotEntry = new List<int>();
            /// <summary>Per slot: filled by a stack on the shelf now; empty but some on the map (waiting for room).</summary>
            public bool[] here, onMap;
            public HashSet<Thing> moveOut = new HashSet<Thing>();
            /// <summary>The stacks on the shelf as looked at, the same stacks as the pure rules saw them, and each entry's cap (-1: none).</summary>
            public List<Thing> things = new List<Thing>();
            public List<ShelfReserveMath.Stack> stacks = new List<ShelfReserveMath.Stack>();
            public int[] caps;
            /// <summary>The shelf isn't in a room, so "while a patient lies in this room" never holds.</summary>
            public bool noRoom;
            /// <summary>
            /// Per entry: "any" of a group kept best first (with its ranked kinds), the best rank of its kinds that would
            /// come (ShelfReserveMath.NoneComing: none) and which kind that is, and whether some of it would come at all
            /// (loose in the home area, or in storage of the shelf's priority or lower).
            /// </summary>
            public bool[] ranked, comes;
            public HashSet<ThingDef>[] rankedDefs;
            public double[] bestComing;
            public ThingDef[] bestDef;
            /// <summary>Per entry: a better kind takes a lesser stack's slot (the rank it needs; NoneComing: no swap), and the kind it replaces.</summary>
            public double[] swapBest;
            public ThingDef[] swapLesser;
            public int swaps;
            /// <summary>The stacks filling a reserved slot; the kinds fetched from storage of the shelf's own priority, and for how many slots.</summary>
            public HashSet<Thing> holding = new HashSet<Thing>();
            public HashSet<ThingDef> wanted = new HashSet<ThingDef>();
            public int wantCount;

            /// <summary>How a kind ranks for an entry kept best first: its rank among the group's ranked kinds, else last (Unranked).</summary>
            public double RankIn(int e, ThingDef d) =>
                ranked[e] && rankedDefs[e] != null && rankedDefs[e].Contains(d) ? RankOf(d) : ShelfReserveMath.Unranked;

            /// <summary>An empty slot of the entry takes this kind: always, but for "any" kept best first only as good as the best that would come.</summary>
            public bool Admits(int e, ThingDef d) => !ranked[e] || ShelfReserveMath.TakesEmptySlot(RankIn(e, d), bestComing[e]);

            public bool FillsMissing(ThingDef d)
            {
                for (int k = 0; k < kinds.Count; k++)
                    if (!here[k] && kinds[k].Matches(d) && Admits(slotEntry[k], d)) return true;
                return false;
            }

            /// <summary>Of a better kind that takes a lesser stack's slot (a swap due).</summary>
            public bool FillsSwap(ThingDef d)
            {
                for (int e = 0; e < entries.Count; e++)
                    if (swapBest[e] > ShelfReserveMath.NoneComing && kindOf[e] != null && kindOf[e].Matches(d) && RankIn(e, d) >= swapBest[e]) return true;
                return false;
            }

            /// <summary>Of a kind whose slot is empty while the colony has some on the map (counted in <see cref="waiting"/>).</summary>
            public bool Waits(ThingDef d)
            {
                for (int k = 0; k < kinds.Count; k++)
                    if (!here[k] && onMap[k] && kinds[k].Matches(d) && Admits(slotEntry[k], d)) return true;
                return false;
            }

            /// <summary>The first kind waiting for room, or null.</summary>
            public Kind FirstWaiting()
            {
                for (int k = 0; k < kinds.Count; k++)
                    if (!here[k] && onMap[k]) return kinds[k];
                return null;
            }

            /// <summary>Of a kind kept off the shelf for now (its condition doesn't hold) and of no kind that counts.</summary>
            public bool Barred(ThingDef d)
            {
                bool off = false;
                for (int e = 0; e < entries.Count; e++)
                {
                    if (kindOf[e] == null || !kindOf[e].Matches(d)) continue;
                    if (active[e]) return false;
                    off = true;
                }
                return off;
            }

            /// <summary>Of a kind kept to its slots that has as many stacks on the shelf as it has slots.</summary>
            public bool CapReached(ThingDef d)
            {
                for (int e = 0; e < entries.Count; e++)
                    if (active[e] && entries[e].only && kindOf[e] != null && kindOf[e].Matches(d) && stacksOf[e] >= Math.Max(1, entries[e].slots)) return true;
                return false;
            }

            /// <summary>The most of it in one stack on this shelf (0: a full stack), the lowest of the kinds that count for it.</summary>
            public int LimitFor(ThingDef d)
            {
                int limit = 0;
                for (int e = 0; e < entries.Count; e++)
                {
                    int l = entries[e].perStack;
                    if (l <= 0 || !active[e] || kindOf[e] == null || !kindOf[e].Matches(d)) continue;
                    limit = limit == 0 ? l : Math.Min(limit, l);
                }
                return limit;
            }
        }

        private const int StateTicks = 120;

        /// <summary>The shelves being looked at right now: a look that asks after its own shelf gets the last state, never a second look.</summary>
        private readonly HashSet<Building_Storage> looking = new HashSet<Building_Storage>();

        public State StateOf(Building_Storage shelf)
        {
            if (shelf == null) return null;
            var group = shelf.GetSlotGroup();
            if (shelf.Destroyed || !shelf.Spawned || group == null) return null;
            int now = Find.TickManager.TicksGame;
            int held = group.HeldThingsCount;
            bool had = states.TryGetValue(shelf, out var st);
            if (had && now - st.tick < StateTicks && st.held == held) return st;
            if (!looking.Add(shelf)) return had ? st : null;
            try
            {
                st = Look(shelf, group, now);
            }
            finally
            {
                looking.Remove(shelf);
            }
            states[shelf] = st;
            return st;
        }

        private State Look(Building_Storage shelf, SlotGroup group, int now)
        {
            var st = new State { tick = now };
            st.entries = reserves.FirstOrDefault(r => r.shelf == shelf)?.entries.ToList() ?? new List<ShelfKind>();
            int n = st.entries.Count;
            var things = group.HeldThings.ToList();
            st.held = things.Count;
            var onShelf = new HashSet<ThingDef>(things.Select(t => t.def));
            var priority = shelf.GetStoreSettings().Priority;
            st.kindOf = new Kind[n];
            st.active = new bool[n];
            st.stacksOf = new int[n];
            st.ranked = new bool[n];
            st.comes = new bool[n];
            st.rankedDefs = new HashSet<ThingDef>[n];
            st.bestComing = new double[n];
            st.bestDef = new ThingDef[n];
            st.swapBest = new double[n];
            st.swapLesser = new ThingDef[n];
            var allowedOf = new List<ThingDef>[n];
            var room = shelf.GetRoom();
            st.noRoom = room == null || room.PsychologicallyOutdoors;
            bool patient = st.entries.Any(e => e.when == ShelfKind.WhenPatient) && PatientIn(room);
            for (int e = 0; e < n; e++)
            {
                st.bestComing[e] = st.swapBest[e] = ShelfReserveMath.NoneComing;
                var kind = Kind.Parse(st.entries[e].kind);
                if (kind != null && kind.best) kind.targets = BestAtHand(kind.cat, shelf, onShelf, priority);
                st.kindOf[e] = kind;
                string when = st.entries[e].when ?? "";
                st.active[e] = kind != null && (when.Length == 0 || when == ShelfKind.WhenPatient && patient);
                if (!st.active[e] || kind == null) continue;
                for (int j = 0; j < Math.Max(1, st.entries[e].slots); j++)
                {
                    st.kinds.Add(kind);
                    st.slotEntry.Add(e);
                }
                // "Any" of a group with ranked kinds is kept best first.
                if (kind.cat == null || kind.best) continue;
                allowedOf[e] = AllowedIn(kind.cat, shelf);
                var ranked = RankedOf(allowedOf[e]);
                if (ranked.Count == 0) continue;
                st.ranked[e] = true;
                st.rankedDefs[e] = new HashSet<ThingDef>(ranked);
            }
            foreach (var c in shelf.AllSlotCellsList()) st.capacity += c.GetMaxItemsAllowedInCell(map);
            var caps = new int[n];
            for (int e = 0; e < n; e++) caps[e] = st.active[e] && st.entries[e].only ? Math.Max(1, st.entries[e].slots) : -1;
            var stacks = new List<ShelfReserveMath.Stack>();
            foreach (var t in things)
            {
                var covers = new List<int>();
                for (int k = 0; k < st.kinds.Count; k++)
                    if (st.kinds[k].Matches(t.def)) covers.Add(k);
                var groups = new List<int>();
                double rank = 0d;
                bool inRanked = false;
                for (int e = 0; e < n; e++)
                {
                    if (!st.active[e] || st.kindOf[e] == null || !st.kindOf[e].Matches(t.def)) continue;
                    st.stacksOf[e]++;
                    if (caps[e] >= 0) groups.Add(e);
                    if (!st.ranked[e]) continue;
                    double r = st.RankIn(e, t.def);
                    rank = inRanked ? Math.Max(rank, r) : r;
                    inRanked = true;
                }
                stacks.Add(new ShelfReserveMath.Stack
                {
                    key = t.def.defName, count = t.stackCount, covers = covers.ToArray(), barred = st.Barred(t.def), groups = groups.ToArray(), rank = rank
                });
            }
            var filledBy = ShelfReserveMath.Fill(st.kinds.Count, stacks);
            st.here = new bool[st.kinds.Count];
            st.onMap = new bool[st.kinds.Count];
            var slotsOf = new List<int>[n];
            for (int e = 0; e < n; e++) slotsOf[e] = new List<int>();
            for (int k = 0; k < st.kinds.Count; k++)
            {
                slotsOf[st.slotEntry[k]].Add(k);
                st.here[k] = filledBy[k] >= 0;
                if (st.here[k]) st.holding.Add(things[filledBy[k]]);
            }
            // What would come: for "any" kept best first, the best of it; for the rest, only where a slot is empty.
            for (int e = 0; e < n; e++)
            {
                if (!st.active[e] || st.kindOf[e] == null) continue;
                bool empty = slotsOf[e].Any(k => !st.here[k]);
                if (st.ranked[e])
                {
                    foreach (var d in allowedOf[e].OrderByDescending(d => st.RankIn(e, d)))
                    {
                        if (!Fetchable(d, shelf, priority)) continue;
                        st.comes[e] = true;
                        st.bestComing[e] = st.RankIn(e, d);
                        st.bestDef[e] = d;
                        break;
                    }
                    WaitForBetter(shelf, st, e, slotsOf[e].Count(k => !st.here[k]), now);
                }
                else if (empty) st.comes[e] = st.kindOf[e].Defs.Any(d => Fetchable(d, shelf, priority));
            }
            var onMapOf = new Dictionary<int, bool>();
            for (int k = 0; k < st.kinds.Count; k++)
            {
                if (st.here[k]) continue;
                int e = st.slotEntry[k];
                if (!onMapOf.TryGetValue(e, out bool some)) onMapOf[e] = some = OnMap(st.kinds[k], shelf);
                if (!some) continue;
                st.onMap[k] = true;
                st.waiting++;
            }
            var outs = ShelfReserveMath.MoveOut(st.capacity, stacks, st.kinds.Count, st.waiting, caps);
            foreach (int i in outs) st.moveOut.Add(things[i]);
            // A better kind that would come takes a lesser stack's slot: into a free slot, else the lesser goes back to the store.
            int free = st.capacity - (st.held - outs.Count) - st.waiting;
            var taken = new HashSet<int>(outs);
            for (int e = 0; e < n; e++)
            {
                double best = st.bestComing[e];
                if (!st.ranked[e] || best == ShelfReserveMath.NoneComing) continue;
                int low = ShelfReserveMath.SwapDue(stacks, filledBy, slotsOf[e], best);
                if (low < 0) continue;
                if (free > 0) free--;
                else
                {
                    int go = ShelfReserveMath.SwapOut(stacks, filledBy, slotsOf[e], best, low, taken);
                    if (go < 0 || !HasOtherStore(things[go], shelf)) continue;
                    taken.Add(go);
                    st.moveOut.Add(things[go]);
                }
                st.swapBest[e] = best;
                st.swapLesser[e] = things[low].def;
                st.swaps++;
            }
            // The kinds fetched from storage of the shelf's own priority: those an empty slot waits for, and a swap's better kinds.
            for (int k = 0; k < st.kinds.Count; k++)
            {
                int e = st.slotEntry[k];
                if (st.here[k] || !st.onMap[k] || !st.comes[e]) continue;
                st.wantCount++;
                foreach (var d in st.kinds[k].Defs)
                    if (shelf.settings.AllowedToAccept(d) && st.Admits(e, d)) st.wanted.Add(d);
            }
            for (int e = 0; e < n; e++)
            {
                if (st.swapBest[e] == ShelfReserveMath.NoneComing) continue;
                st.wantCount++;
                foreach (var d in allowedOf[e])
                    if (st.RankIn(e, d) >= st.swapBest[e]) st.wanted.Add(d);
            }
            st.things = things;
            st.stacks = stacks;
            st.caps = caps;
            return st;
        }

        /// <summary>
        /// An empty slot of "any" kept best first waits for the better kind that would come (a lesser one isn't let in
        /// meanwhile), but not for ever: after ShelfReserveMath.GateTicks with no slot filled, that kind is left out for
        /// ShelfReserveMath.RestTicks (it can't be fetched, say), so a lesser kind fills the slot and no swap is made.
        /// </summary>
        private void WaitForBetter(Building_Storage shelf, State st, int e, int missing, int now)
        {
            var key = (shelf, st.entries[e].kind);
            if (rests.TryGetValue(key, out int until))
            {
                if (now < until)
                {
                    st.bestComing[e] = ShelfReserveMath.NoneComing;
                    waits.Remove(key);
                    return;
                }
                rests.Remove(key);
            }
            if (missing == 0 || st.bestComing[e] == ShelfReserveMath.NoneComing)
            {
                waits.Remove(key);
                return;
            }
            // A slot filled since: the wait starts again.
            if (!waits.TryGetValue(key, out var w) || missing < w.missing) w.since = now;
            w.missing = missing;
            waits[key] = w;
            if (!ShelfReserveMath.GivesUp(w.since, now)) return;
            rests[key] = now + ShelfReserveMath.RestTicks;
            waits.Remove(key);
            st.bestComing[e] = ShelfReserveMath.NoneComing;
        }

        /// <summary>Some of this kind the colony could put here: on the map, not forbidden, kept in storage or in the home area, and allowed on this shelf.</summary>
        private bool OnMap(Kind kind, Building_Storage shelf)
        {
            foreach (var d in kind.Defs)
                if (OnMapDef(d, shelf)) return true;
            return false;
        }

        private bool OnMapDef(ThingDef d, Building_Storage shelf)
        {
            var rect = shelf.OccupiedRect();
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsOfDef(d))
            {
                if (!t.Spawned || rect.Contains(t.Position) || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
                if (!t.IsInAnyStorage() && (home == null || !home[t.Position])) continue;
                if (shelf.settings.AllowedToAccept(t)) return true;
            }
            return false;
        }

        /// <summary>
        /// Of the slots waiting, how many the hauling would fill: some of the kind lies where a haul brings it here (loose
        /// in the home area, or in storage of this shelf's priority or lower). A kind kept only in storage of a higher
        /// priority never comes, so no stack is set down beside the shelf for it (ShelfMakeRoom).
        /// </summary>
        internal int Coming(State st, Building_Storage shelf)
        {
            if (st == null || shelf?.Map != map) return 0;
            int n = 0;
            for (int k = 0; k < st.kinds.Count; k++)
                if (!st.here[k] && st.onMap[k] && st.comes[st.slotEntry[k]]) n++;
            return n;
        }

        /// <summary>
        /// Some of this kind would come to the shelf: a stack on the map, not on the shelf, not forbidden, allowed on the
        /// shelf, and either loose in the home area or in storage of the shelf's priority or lower (lower: the game's own
        /// hauling brings it; the same: Rimfolk fetches it, Patch_ShelfReserveFetch), never one holding a reserved slot on
        /// another shelf. Storage of a higher priority keeps what it holds (the game would carry it straight back).
        /// Read without asking any storage whether it accepts a stack, so a look never sets off another.
        /// </summary>
        private bool Fetchable(ThingDef d, Building_Storage shelf, StoragePriority priority)
        {
            var rect = shelf.OccupiedRect();
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsOfDef(d))
            {
                if (!t.Spawned || rect.Contains(t.Position) || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
                var g = map.haulDestinationManager.SlotGroupAt(t.Position);
                var other = g?.parent as Building_Storage;
                bool stored = g != null && g.Settings.AllowedToAccept(t) && !Cached(other, s => s.moveOut.Contains(t), false);
                if (!stored && (home == null || !home[t.Position])) continue;
                if (stored && ((int)g.Settings.Priority > (int)priority || Cached(other, s => s.holding.Contains(t), true))) continue;
                if (shelf.settings.AllowedToAccept(t)) return true;
            }
            return false;
        }

        /// <summary>
        /// What another reserved shelf's last look says, never looking again (<paramref name="unseen"/> when it hasn't
        /// been looked at yet: a stack there counts as holding its slot, not as leaving); false for any other storage.
        /// </summary>
        private bool Cached(Building_Storage other, Func<State, bool> says, bool unseen)
        {
            if (other == null || !Reserved.Contains(other)) return false;
            return states.TryGetValue(other, out var st) ? says(st) : unseen;
        }

        /// <summary>
        /// Some storage other than a reserved shelf would take this stack now (a cell with room that allows it): where a
        /// lesser stack goes back to when a better kind takes its slot. A stack with nowhere to go stays.
        /// </summary>
        private bool HasOtherStore(Thing t, Building_Storage shelf)
        {
            foreach (var g in map.haulDestinationManager.AllGroupsListInPriorityOrder)
            {
                var parent = g?.parent;
                if (parent == null || parent == shelf || parent is Building_Storage b && Reserved.Contains(b) || !parent.HaulDestinationEnabled) continue;
                if (parent is Thing owner && owner.Faction != Faction.OfPlayer || !g.Settings.AllowedToAccept(t)) continue;
                foreach (var c in g.CellsList)
                    if (StoreUtility.IsGoodStoreCell(c, map, t, null, Faction.OfPlayer)) return true;
            }
            return false;
        }

        /// <summary>
        /// The reserved shelf a stack in storage is fetched to: one of the same priority as where it lies (lower storage
        /// the game's own hauling empties by itself; higher keeps what it holds), wanting its kind for an empty slot or a
        /// better kind's swap, with more such slots than hauls already on their way to it, the nearest first; null for
        /// none. A stack holding a reserved slot where it lies stays there.
        /// </summary>
        internal Building_Storage WantedOn(Thing t)
        {
            if (t == null || !t.Spawned || t.Map != map || t.def.category != ThingCategory.Item) return null;
            var g = map.haulDestinationManager.SlotGroupAt(t.Position);
            if (g == null) return null;
            var own = g.parent as Building_Storage;
            Building_Storage pick = null;
            float near = float.MaxValue;
            foreach (var s in Reserved)
            {
                if (s == own || s.Map != map || !s.Spawned || s.Faction != Faction.OfPlayer || !s.HaulDestinationEnabled) continue;
                var settings = s.GetStoreSettings();
                if (settings == null || settings.Priority != g.Settings.Priority) continue;
                var st = StateOf(s);
                if (st == null || st.wantCount <= 0 || !st.wanted.Contains(t.def) || !settings.AllowedToAccept(t)) continue;
                if (st.wantCount <= Incoming(s)) continue;
                float dist = s.Position.DistanceToSquared(t.Position);
                if (dist >= near) continue;
                near = dist;
                pick = s;
            }
            if (pick == null) return null;
            if (own != null && Reserved.Contains(own) && StateOf(own) is State ost && ost.holding.Contains(t) && !ost.moveOut.Contains(t)) return null;
            return pick;
        }

        /// <summary>The shelf's cells someone of the colony has reserved: hauls on their way to it.</summary>
        private int Incoming(Building_Storage shelf)
        {
            int n = 0;
            foreach (var c in shelf.AllSlotCellsList())
                if (map.reservationManager.IsReservedByAnyoneOf(c, Faction.OfPlayer)) n++;
            return n;
        }

        /// <summary>
        /// The job bringing a stack from storage of the shelf's own priority to the reserved shelf that wants it
        /// (WantedOn): the game's haul to a cell of that shelf, the cell found by the game's own search within it. Null
        /// when there is none, or no cell takes it.
        /// </summary>
        internal static Job FetchJob(Pawn p, Thing t)
        {
            var map = p?.Map;
            if (map == null || t == null || !t.Spawned || t.Map != map || !On || Reserved.Count == 0) return null;
            var group = Of(map)?.WantedOn(t)?.GetSlotGroup();
            if (group == null || !StoreUtility.TryFindBestBetterStoreCellForIn(t, p, map, StoragePriority.Unstored, p.Faction, group, out var cell)) return null;
            var job = HaulAIUtility.HaulToCellStorageJob(p, t, cell, false);
            if (job != null) job.haulOpportunisticDuplicates = false;
            return job;
        }

        // ---------------- The game's hooks ----------------

        private static State StateAt(IntVec3 c, Map map, out Building_Storage shelf)
        {
            shelf = map.haulDestinationManager.SlotGroupAt(c)?.parent as Building_Storage;
            if (shelf == null || !Reserved.Contains(shelf) || !On) return null;
            var st = Of(map)?.StateOf(shelf);
            return st == null || st.entries.Count == 0 ? null : st;
        }

        /// <summary>This thing may go into this cell of a shelf with reserved kinds (Patch_ShelfReserveCell).</summary>
        internal static bool MayStore(IntVec3 c, Map map, Thing thing)
        {
            var st = StateAt(c, map, out var shelf);
            if (st == null) return true;
            // Already on the shelf: it stays, unless it's a stack that must go (not to another cell of it), and two of its
            // stacks are put together (the game's merging) only while the one made stays within its number per stack.
            if (thing.Spawned && thing.Map == map && shelf.OccupiedRect().Contains(thing.Position))
            {
                if (st.moveOut.Contains(thing)) return false;
                int most = st.LimitFor(thing.def);
                if (most <= 0) return true;
                foreach (var t in map.thingGrid.ThingsListAt(c))
                    if (t != thing && t.CanStackWith(thing) && t.stackCount + thing.stackCount > most) return false;
                return true;
            }
            if (st.Barred(thing.def)) return false;
            // Added to a stack already in the cell, it takes no slot: up to its number per stack, if it has one.
            int limit = st.LimitFor(thing.def);
            var here = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < here.Count; i++)
            {
                var t = here[i];
                if (t == thing || !t.def.EverStorable(false) || !t.CanStackWith(thing)) continue;
                if (limit > 0 && t.stackCount >= limit) return false;
                if (t.stackCount < t.def.stackLimit) return true;
            }
            // An empty slot it fills (for "any" kept best first, only as good as the best that would come), or a better
            // kind taking a lesser stack's slot; a free slot is kept for each of those still waiting.
            bool fills = st.FillsMissing(thing.def) || st.swaps > 0 && st.FillsSwap(thing.def);
            if (!fills && st.CapReached(thing.def)) return false;
            // The game says this cell has room; if the count disagrees (another mod's rule), there's room for one.
            int capacity = Math.Max(st.capacity, st.held + 1);
            return ShelfReserveMath.MayTakeSlot(capacity, st.held, fills, st.waiting + st.swaps);
        }

        /// <summary>The most of this thing one haul brings to this cell (0: no limit), for a kind held to a number per stack (Patch_ShelfReserveHaulCount).</summary>
        internal static int MostInto(IntVec3 c, Map map, Thing thing)
        {
            var st = StateAt(c, map, out _);
            if (st == null) return 0;
            int limit = st.LimitFor(thing.def);
            if (limit <= 0) return 0;
            int existing = -1;
            foreach (var t in map.thingGrid.ThingsListAt(c))
                if (t != thing && t.CanStackWith(thing))
                {
                    existing = t.stackCount;
                    break;
                }
            return Math.Max(1, ShelfReserveMath.SpaceFor(limit, thing.def.stackLimit, existing));
        }

        /// <summary>A stack on the shelf that must go (to make room, past a cap, or kept off for now) no longer belongs there (Patch_ShelfReserveAccepts).</summary>
        internal static bool MovingOut(Building_Storage shelf, Thing t)
        {
            if (t == null || !Reserved.Contains(shelf) || !On) return false;
            var st = Of(shelf.Map)?.StateOf(shelf);
            return st != null && st.moveOut.Contains(t);
        }

        // ---------------- Words ----------------

        public static Gizmo GizmoFor(Building_Storage shelf)
        {
            if (!On || shelf?.Map == null || shelf.Faction != Faction.OfPlayer || shelf.settings == null) return null;
            // A hopper or a one-stack box has nothing to share out.
            if (shelf.AllSlotCellsList().Count * shelf.MaxItemsInCell < 2) return null;
            var c = Of(shelf.Map);
            if (c == null) return null;
            return new Command_ShelfReserve(shelf, c.EntriesOf(shelf).Sum(e => Math.Max(1, e.slots)));
        }

        /// <summary>Each reserved kind of the shelf, how it's kept, and where it stands, one line each.</summary>
        public static string Summary(Building_Storage shelf)
        {
            var st = Of(shelf?.Map)?.StateOf(shelf);
            if (st == null || st.entries.Count == 0) return "CC_Shelf_None".Translate().Resolve();
            var lines = new List<string>();
            for (int e = 0; e < st.entries.Count; e++)
            {
                string label = st.kindOf[e]?.Label ?? st.entries[e].kind;
                string how = HowKept(st.entries[e]);
                lines.Add("- " + label + (how.NullOrEmpty() ? "" : " (" + how + ")") + ": " + StatusOf(st, e));
            }
            return string.Join("\n", lines);
        }

        /// <summary>"2 slots, no more, 4 each, while a patient lies here", leaving out what is as usual.</summary>
        public static string HowKept(ShelfKind entry)
        {
            var parts = new List<string>();
            if (entry.slots > 1) parts.Add("CC_Shelf_HowSlots".Translate(entry.slots).Resolve());
            if (entry.only) parts.Add("CC_Shelf_HowOnly".Translate().Resolve());
            if (entry.perStack > 0) parts.Add("CC_Shelf_HowEach".Translate(entry.perStack).Resolve());
            if (entry.when == ShelfKind.WhenPatient) parts.Add("CC_Shelf_HowPatient".Translate().Resolve());
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Where an entry stands: kept off for now, on the shelf (a better kind coming in place of a lesser one), coming, a
        /// stack moving out for it, only in storage of a higher priority, or none on the map; with several slots, how many
        /// are filled.
        /// </summary>
        public static string StatusOf(State st, int e)
        {
            if (e < 0 || e >= st.entries.Count || st.kindOf[e] == null) return "CC_Shelf_Unknown".Translate().Resolve();
            if (!st.active[e]) return (st.noRoom ? "CC_Shelf_WhenNoRoom" : "CC_Shelf_WhenOff").Translate().Resolve();
            var slots = new List<int>();
            for (int k = 0; k < st.kinds.Count; k++)
                if (st.slotEntry[k] == e) slots.Add(k);
            if (slots.Count == 0) return "CC_Shelf_Unknown".Translate().Resolve();
            int filled = slots.Count(k => st.here[k]);
            int open = slots.FirstOrDefault(k => !st.here[k]);
            string rest = filled == slots.Count
                ? st.swapBest[e] > ShelfReserveMath.NoneComing && st.bestDef[e] != null && st.swapLesser[e] != null
                    ? "CC_Shelf_BetterComing".Translate(st.bestDef[e].label.Named("BETTER"), st.swapLesser[e].label.Named("LESSER")).Resolve()
                    : "CC_Shelf_Here".Translate().Resolve()
                : !st.onMap[open] ? "CC_Shelf_NoneOnMap".Translate().Resolve()
                : !st.comes[e] ? "CC_Shelf_KeptHigher".Translate().Resolve()
                : (st.moveOut.Count > 0 ? "CC_Shelf_MakingRoom" : "CC_Shelf_Coming").Translate().Resolve();
            string status = slots.Count > 1 && filled < slots.Count
                ? "CC_Shelf_SomeHere".Translate(filled, slots.Count, rest.Named("REST")).Resolve()
                : rest;
            // The best at hand says which it is now.
            var kind = st.kindOf[e];
            if (kind.best && kind.targets != null && kind.targets.Count > 0)
                status = "CC_Shelf_BestNow".Translate(string.Join(", ", kind.targets.Select(d => d.label)).Named("KIND"), status.Named("STATUS")).Resolve();
            return status;
        }

        public static bool AllHere(State st, int e)
        {
            if (st == null || e < 0 || e >= st.entries.Count || !st.active[e]) return false;
            for (int k = 0; k < st.kinds.Count; k++)
                if (st.slotEntry[k] == e && !st.here[k]) return false;
            return true;
        }
    }

    /// <summary>A shelf with reserved kinds keeps to them (ShelfReserves.MayStore): checked wherever the game asks if a cell can take a thing.</summary>
    [HarmonyPatch(typeof(StoreUtility), "NoStorageBlockersIn")]
    public static class Patch_ShelfReserveCell
    {
        private static bool broken;

        public static void Postfix(IntVec3 c, Map map, Thing thing, ref bool __result)
        {
            if (!__result || broken || ShelfReserves.Reserved.Count == 0 || thing == null || map == null) return;
            try
            {
                if (!ShelfReserves.MayStore(c, map, thing)) __result = false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: a cell", e);
            }
        }
    }

    /// <summary>A haul to a shelf brings no more of a kind held to a number per stack than the cell has room for (ShelfReserves.MostInto).</summary>
    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToCellStorageJob))]
    public static class Patch_ShelfReserveHaulCount
    {
        private static bool broken;

        public static void Postfix(Pawn p, Thing t, IntVec3 storeCell, ref Job __result)
        {
            if (__result == null || broken || ShelfReserves.Reserved.Count == 0 || t == null || p?.Map == null) return;
            try
            {
                int most = ShelfReserves.MostInto(storeCell, p.Map, t);
                if (most > 0 && __result.count > most) __result.count = most;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: how many to bring", e);
            }
        }
    }

    /// <summary>A stack that must go (to make room, past a cap, or kept off for now) no longer belongs on the shelf, so a hauler takes it on.</summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Accepts))]
    public static class Patch_ShelfReserveAccepts
    {
        private static bool broken;

        public static void Postfix(Building_Storage __instance, Thing t, ref bool __result)
        {
            if (!__result || broken || ShelfReserves.Reserved.Count == 0) return;
            try
            {
                if (ShelfReserves.MovingOut(__instance, t)) __result = false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: what belongs", e);
            }
        }
    }

    /// <summary>
    /// A stack in storage of a reserved shelf's own priority that the shelf wants (an empty slot of its kind, or a better
    /// kind's swap: ShelfReserves.WantedOn) isn't in its best storage, so the game's hauling looks at it; the haul itself
    /// is ShelfReserves.FetchJob, given where the game's own search finds no better storage (Patch_ShelfMakeRoomAuto).
    /// </summary>
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsInValidBestStorage))]
    public static class Patch_ShelfReserveFetch
    {
        private static bool broken;

        public static void Postfix(Thing t, ref bool __result)
        {
            if (!__result || broken || ShelfReserves.Reserved.Count == 0 || t == null || !t.Spawned || !ShelfReserves.On) return;
            try
            {
                if (ShelfReserves.Of(t.Map)?.WantedOn(t) != null) __result = false;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("reserved shelf slots: fetching", e);
            }
        }
    }

    /// <summary>The "Reserved slots" button on the colony's shelves.</summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.GetGizmos))]
    public static class Patch_ShelfReserveGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Storage __instance)
        {
            foreach (var g in __result) yield return g;
            Gizmo c = null;
            try
            {
                c = ShelfReserves.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reserved shelf slots: the button", e);
            }
            if (c != null) yield return c;
        }
    }
}
