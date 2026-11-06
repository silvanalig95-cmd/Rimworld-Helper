using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>Where a piece of the scenario's starting kit came from, and so where it goes on a new map.</summary>
    public enum LandingKind
    {
        /// <summary>Brought with the people: in their drop pods, or set down beside them on a standing start.</summary>
        Pods,
        /// <summary>Scattered near the start by the scenario (the game's scatter within 20 cells of the start spot).</summary>
        Near,
        /// <summary>Scattered anywhere on the map by the scenario (ship chunks, slag and the like).</summary>
        Anywhere,
        /// <summary>What a starting colonist wore, wielded or carried in their inventory.</summary>
        Gear
    }

    /// <summary>Why another landing site isn't offered now, in the order they are told.</summary>
    public enum LandingWhy
    {
        None,
        /// <summary>The deeper game hooks are off.</summary>
        Off,
        /// <summary>Rimfolk didn't see this game's start, so it has no record of the starting kit.</summary>
        NotSeen,
        /// <summary>The first days after landing are over.</summary>
        Over,
        /// <summary>The tutorial.</summary>
        Tutorial,
        /// <summary>A gravship start (Odyssey): the gravship itself can fly the colony elsewhere.</summary>
        Gravship,
        /// <summary>A start above the planet.</summary>
        Space,
        /// <summary>More homes than the one the colony started on, or the start map is no longer a home.</summary>
        Homes,
        /// <summary>A journey or a new colony is being planned from the home (Rimfolk's journey planner, a founding).</summary>
        Plans,
        /// <summary>The void monolith (Anomaly) has woken: its study is tied to this map.</summary>
        Monolith,
        /// <summary>Someone of the colony is away from the map.</summary>
        Away,
        /// <summary>Someone of the colony is inside something on the map (a casket, a pod, a vat).</summary>
        Inside,
        /// <summary>Guests of the colony stay on the map (lodgers, the rescued): they aren't the colony's to bring.</summary>
        Guests,
        /// <summary>A quest under way has someone or something on this map that wouldn't come along.</summary>
        Quest,
        /// <summary>A threat is on the map.</summary>
        Threat
    }

    /// <summary>Who a pawn on the map is to the colony, for what comes along.</summary>
    public enum LandingSide
    {
        Colonist,
        Prisoner,
        Slave,
        Mech,
        Animal,
        /// <summary>Visitors, traders, wild animals, enemies, entities: they stay with the map.</summary>
        Other
    }

    /// <summary>What is known of the colony now, for whether another landing site is offered (<see cref="LandingMath.WhyNot"/>).</summary>
    public sealed class LandingState
    {
        public bool on = true, recorded = true, open = true, tutorial, gravship, space;
        /// <summary>The colony's homes, and whether the one home is the map it landed on.</summary>
        public int homes = 1;
        public bool startHome = true;
        public bool plans, monolithWoken, away, inside, guests, quest, threat;
    }

    /// <summary>
    /// Choosing another landing site in the first days of a new game (DESIGN.md, "Another landing site"; the player's
    /// request of 6 November 2026): when it is offered, who and what comes along, how much of the scenario's starting kit
    /// is left as stacks are split, merged and used, and where the kit goes on the new map. Pure rules, tested in
    /// Tools/Tests/LandingTests.cs; the game's side is Colonies/Landing.cs.
    /// </summary>
    public static class LandingMath
    {
        public const int DayTicks = 60000;

        /// <summary>It is offered for this many days after a landing (the game's start, or the last landing chosen here).</summary>
        public const int OfferDays = 3;

        public const int OfferTicks = OfferDays * DayTicks;

        /// <summary>The game's own scatter near the start spot reaches this far (GenStep_Scatterer, nearPlayerStart).</summary>
        public const int NearRadius = 20;

        /// <summary>The game's own drop pods open after this many ticks (ScenPart_PlayerPawnsArriveMethod).</summary>
        public const int PodOpenDelay = 110;

        /// <summary>The game warns of a map's weight on the computer above this size (Dialog_AdvancedGameConfig).</summary>
        public const int HeavySize = 280;

        /// <summary>Still within the first days after the landing at <paramref name="landedAt"/>.</summary>
        public static bool Open(int now, int landedAt) => landedAt >= 0 && now >= landedAt && now - landedAt < OfferTicks;

        /// <summary>The menu's entry shows at all (greyed or not): a start Rimfolk saw, within its first days, not the tutorial.</summary>
        public static bool Shows(bool on, bool recorded, int now, int landedAt, bool tutorial) => on && recorded && !tutorial && Open(now, landedAt);

        /// <summary>Why it isn't offered, the first reason that holds, or <see cref="LandingWhy.None"/>.</summary>
        public static LandingWhy WhyNot(LandingState s)
        {
            if (!s.on) return LandingWhy.Off;
            if (!s.recorded) return LandingWhy.NotSeen;
            if (!s.open) return LandingWhy.Over;
            if (s.tutorial) return LandingWhy.Tutorial;
            if (s.gravship) return LandingWhy.Gravship;
            if (s.space) return LandingWhy.Space;
            if (s.homes != 1 || !s.startHome) return LandingWhy.Homes;
            if (s.plans) return LandingWhy.Plans;
            if (s.monolithWoken) return LandingWhy.Monolith;
            if (s.away) return LandingWhy.Away;
            if (s.inside) return LandingWhy.Inside;
            if (s.guests) return LandingWhy.Guests;
            if (s.quest) return LandingWhy.Quest;
            if (s.threat) return LandingWhy.Threat;
            return LandingWhy.None;
        }

        /// <summary>Who a pawn is to the colony: a prisoner or slave of the colony first, then its mechs, animals and people.</summary>
        public static LandingSide SideOf(bool playerFaction, bool prisonerOfColony, bool slaveOfColony, bool humanlike, bool mech, bool animal)
        {
            if (prisonerOfColony) return LandingSide.Prisoner;
            if (slaveOfColony) return LandingSide.Slave;
            if (!playerFaction) return LandingSide.Other;
            if (mech) return LandingSide.Mech;
            if (animal) return LandingSide.Animal;
            if (humanlike) return LandingSide.Colonist;
            return LandingSide.Other;
        }

        /// <summary>Every colonist, prisoner, slave, mech and animal of the colony comes along; nobody else does.</summary>
        public static bool Comes(LandingSide side) => side != LandingSide.Other;

        /// <summary>A guest of the colony: hosted by it, neither its prisoner nor its slave nor one of its own.</summary>
        public static bool Guest(bool hostedByColony, bool playerFaction, bool prisonerOfColony, bool slaveOfColony) =>
            hostedByColony && !playerFaction && !prisonerOfColony && !slaveOfColony;

        /// <summary>Each of these gets a drop pod of their own, as each starting colonist did; mechs and animals share the pods with the kit.</summary>
        public static bool OwnPod(LandingSide side) => side == LandingSide.Colonist || side == LandingSide.Prisoner || side == LandingSide.Slave;

        /// <summary>
        /// What the scenario made at the start, as recorded: a starting colonist's own gear first, then what a scatter part
        /// made (near the start or anywhere), else what came with the people.
        /// </summary>
        public static LandingKind KindOf(bool heldByStarter, bool scattered, bool scatterNearStart)
        {
            if (heldByStarter) return LandingKind.Gear;
            if (scattered) return scatterNearStart ? LandingKind.Near : LandingKind.Anywhere;
            return LandingKind.Pods;
        }

        /// <summary>What is recorded as kit: items (minified things among them), and the buildings a scatter part put anywhere (ship chunks).</summary>
        public static bool Recorded(bool item, bool building, LandingKind kind) => item || building && kind == LandingKind.Anywhere;

        /// <summary>
        /// Where a piece goes on the new map: with the people (their pods, or beside them standing) for what came with
        /// them and their own gear; scattered near the new start for what was scattered near the old one; anywhere for
        /// what was scattered anywhere.
        /// </summary>
        public static LandingKind PlaceOf(LandingKind kind) => kind == LandingKind.Gear ? LandingKind.Pods : kind;

        // ---------------- How much of a stack is the kit ----------------
        // Each piece of the kit is a stack the game made at the start, with how many of it are the kit's. Stacks are split
        // (hauled, carried, used a part at a time), merged (on a stockpile, in a pod) and used up. What the colony found or
        // made on the map is never the kit's, and when a stack that holds both is split or used, the kit's part goes first,
        // so the kit is never counted larger than it is.

        /// <summary>The kit's part of a stack: never more than the stack (some were eaten, used or burnt), never below none.</summary>
        public static int KitIn(int kit, int stack) => Math.Max(0, Math.Min(kit, stack));

        /// <summary>
        /// A stack of <paramref name="before"/> holding <paramref name="kit"/> of the kit has <paramref name="split"/> split
        /// off: the kit's part goes with it first. Returns the kit in the part split off; <paramref name="rest"/> is what
        /// stays in the stack.
        /// </summary>
        public static int SplitKit(int kit, int before, int split, out int rest)
        {
            kit = KitIn(kit, before);
            int off = KitIn(kit, Math.Max(0, split));
            rest = kit - off;
            return off;
        }

        /// <summary>
        /// A stack takes <paramref name="taken"/> from another of <paramref name="otherBefore"/> holding
        /// <paramref name="kitOther"/> of the kit: the kit's part goes first. Returns the kit that moves over.
        /// </summary>
        public static int MergeKit(int kitOther, int otherBefore, int taken) => KitIn(KitIn(kitOther, otherBefore), Math.Max(0, taken));

        /// <summary>How much of a loose stack comes along: the kit's part of it, never more (the rest stays with the map).</summary>
        public static int Takes(int kit, int stack) => KitIn(kit, stack);

        // ---------------- The map ----------------

        /// <summary>
        /// The map sizes offered: the game's own (its advanced options, with its larger test sizes when those are on) and the
        /// current map's, smallest first.
        /// </summary>
        public static List<int> Sizes(IEnumerable<int> game, IEnumerable<int> test, bool withTest, int current)
        {
            var all = new HashSet<int>(game ?? Enumerable.Empty<int>());
            if (withTest && test != null) all.UnionWith(test);
            if (current > 0) all.Add(current);
            return all.Where(s => s > 0).OrderBy(s => s).ToList();
        }

        /// <summary>The default size: the current map's.</summary>
        public static int DefaultSize(IReadOnlyList<int> sizes, int current) => sizes.Contains(current) ? current : sizes.Count > 0 ? sizes[sizes.Count / 2] : current;

        /// <summary>
        /// The map's seed for a new map of the same place: the game's own (the world's seed and the tile), turned by a
        /// number drawn once for this landing, so the same tile gives another map. Zero leaves the game's seed.
        /// </summary>
        public static int TurnSeed(int seed, int turn)
        {
            if (turn == 0) return seed;
            unchecked
            {
                // Mixed as the game's Gen.HashCombineInt mixes two numbers.
                return seed ^ (int)((uint)turn + 2654435769u + (uint)(seed << 6) + (uint)(seed >> 2));
            }
        }
    }
}
