using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Who is about to carry letters away, and which way they go.</summary>
    public struct MailCarrier
    {
        public PostCarrier kind;
        /// <summary>Their people (null for the colony's own).</summary>
        public Faction people;
        public Map map;
        /// <summary>The way they leave the map, as a world heading (0 north, 90 east).</summary>
        public bool knowEdge;
        public float edgeHeading;
        /// <summary>Where they're going on the world, if known.</summary>
        public bool knowDest;
        public PlanetTile dest;
        public LetterMeans means;
        /// <summary>"the traders of the Wheel", "Edda's caravan": for the words.</summary>
        public string name;
    }

    /// <summary>
    /// The outbox (docs/design/post-and-letters.md, step 1, point 1): letters written wait in the mailbag at the gate (the
    /// colony's Rimfolk_Mailbag spot, the nearest to the gate; with none, the gate itself), as neighbour letters in the stage
    /// Outbox, until someone going their way takes them (Post/MailWatch.cs, PostRider.cs), or the colony's own rider
    /// (Post/OwnRiders.cs). Whether a letter goes with a carrier is PostMath.OnTheirWay over the world, the town's reach
    /// (Post/PostReach.cs: within range, by relay, out of reach) and, for the post, whether it serves their people.
    /// </summary>
    public static class Outbox
    {
        private static int Now => Find.TickManager.TicksGame;

        private static NotableBook Book => NotableBook.Instance;

        public static ThingDef MailbagDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_Mailbag");

        private static readonly Dictionary<int, (IntVec3 cell, float at)> gates = new Dictionary<int, (IntVec3, float)>();

        /// <summary>The colony's mailbag nearest its gate, or null.</summary>
        public static Thing Bag(Map map)
        {
            var def = MailbagDef;
            if (map == null || def == null) return null;
            var bags = map.listerBuildings.AllBuildingsColonistOfDef(def).Where(b => b.Spawned).ToList();
            if (bags.Count == 0) return null;
            var gate = Gate(map);
            return gate.IsValid ? bags.OrderBy(b => b.Position.DistanceToSquared(gate)).First() : bags[0];
        }

        /// <summary>The gate (PlaySpots.GateCell), looked at once every few seconds a map.</summary>
        private static IntVec3 Gate(Map map)
        {
            float t = Time.realtimeSinceStartup;
            if (gates.TryGetValue(map.uniqueID, out var g) && t - g.at < 5f && t >= g.at && g.cell.InBounds(map)) return g.cell;
            var c = PlaySpots.GateCell(map, null);
            if (!c.IsValid) c = DropCellFinder.TradeDropSpot(map);
            gates[map.uniqueID] = (c, t);
            return c;
        }

        /// <summary>Where letters wait and are fetched from: the mailbag, else a plain cell by the gate.</summary>
        public static IntVec3 Spot(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            var bag = Bag(map);
            return bag != null ? bag.Position : Gate(map);
        }

        /// <summary>Letters waiting in a home's outbox, oldest first (packed by a caravan's member: not counted as waiting).</summary>
        public static List<NeighbourLetter> Waiting(Map map, bool packedToo = false) =>
            Book?.letters.Where(l => l.stage == LetterStage.Outbox && l.map == map && (packedToo || l.packedBy == null)).OrderBy(l => l.stageTick).ToList()
            ?? new List<NeighbourLetter>();

        public static bool Any(Map map) => Book?.letters.Any(l => l.stage == LetterStage.Outbox && l.map == map) ?? false;

        public static bool AnyAnywhere => Book?.letters.Any(l => l.stage == LetterStage.Outbox) ?? false;

        // ---------------- In ----------------

        /// <summary>A letter goes in the outbox: written (or a message to be spoken), it waits for someone going its way.</summary>
        public static void Put(NeighbourLetter l, Pawn by)
        {
            if (l == null) return;
            l.stage = LetterStage.Outbox;
            l.stageTick = Now;
            l.packedBy = null;
            l.metAtGate = false;
            NotableBook.Changed();
            var map = l.map;
            var bag = Bag(map);
            // apart: the writer puts the letter in the mailbag (or keeps the words for a carrier); nobody else is there for it.
            string key = l.spoken ? "CC_Mail_Msg_Spoken" : bag != null ? "CC_Mail_Msg_InBag" : "CC_Mail_Msg_AtGate";
            // Written out in full, it can be read at the mailbag and on the people page (Letters/LetterBook.cs).
            bool kept = LetterBook.ForLetter(l.id)?.Ours != null;
            Notify.Message(key.Translate((by?.LabelShort ?? l.WriterName).Named("PAWN"), (l.toName ?? "?").Named("TO"), PostWords.Likely(l).Named("LIKELY")).Resolve()
                + NeighbourLetters.CanRead(kept, NotableBook.Instance?.Get(l.notable), true),
                bag != null ? new LookTargets(bag) : by != null && by.Spawned ? new LookTargets(by) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- Who takes it ----------------

        /// <summary>The town a letter is for (and its notable), or null.</summary>
        public static Settlement TownOf(NeighbourLetter l, out Notable n)
        {
            n = Book?.Get(l?.notable ?? -1);
            return n != null ? NotableBook.TownOf(n.town) : null;
        }

        /// <summary>
        /// The letter goes with this carrier: not to a people at war (a go-between comes for those); the post takes what it
        /// serves within range or by relay; anyone else what is on their way (to the relay town when it is far, to the town
        /// itself only by their own people or their road when it is out of reach).
        /// </summary>
        public static bool Goes(NeighbourLetter l, MailCarrier c)
        {
            if (l == null || l.stage != LetterStage.Outbox || l.map != c.map) return false;
            var town = TownOf(l, out var n);
            if (town == null || n?.faction == null || NotableBook.Hostile(n)) return false;
            var map = c.map;
            var info = PostReach.Of(map, town);
            bool theirs = c.people != null && c.people == n.faction;
            // Out of reach, but introduced through a people who knows them (a chain of neighbours): it goes as a relay through their town.
            bool chain = info.band == ReachBand.Cut && info.chain != null;
            if (c.kind == PostCarrier.PostRider) return PostMath.PostServes(Ages.Of(n.faction), false) && (info.band != ReachBand.Cut || chain);
            if (c.means == LetterMeans.Shuttle && info.band != ReachBand.Cut) return true;
            var target = info.band == ReachBand.Far && info.relay != null && !theirs ? info.relay : chain && !theirs ? info.chain : town;
            bool targetTheirs = theirs || c.people != null && target.Faction == c.people;
            float heading = Heading(map.Tile, target.Tile), tiles = LivingEvents.Distance(map.Tile, target.Tile);
            float destHeading = 0f, destTiles = 0f;
            if (c.knowDest && c.dest.Valid)
            {
                destHeading = Heading(map.Tile, c.dest);
                destTiles = LivingEvents.Distance(map.Tile, c.dest);
            }
            var way = PostMath.OnTheirWay(targetTheirs, heading, tiles, c.knowDest && c.dest.Valid, destHeading, destTiles, c.knowEdge, c.edgeHeading);
            if (info.band == ReachBand.Cut && !chain) return way == WayWhy.Theirs || way == WayWhy.Road;
            return way != WayWhy.No;
        }

        public static float Heading(PlanetTile from, PlanetTile to)
        {
            try
            {
                return from.Valid && to.Valid && from.Layer == to.Layer && from != to ? Find.WorldGrid.GetHeadingFromTo(from, to) : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>The world heading of a cell on a map seen from its middle (a map's north is the world's north).</summary>
        public static float CellHeading(Map map, IntVec3 c) => PostMath.Heading(c.x - map.Size.x / 2f, c.z - map.Size.z / 2f);

        /// <summary>The letters waiting that go with this carrier.</summary>
        public static List<NeighbourLetter> For(MailCarrier c) => Waiting(c.map).Where(l => !l.ownRider && Goes(l, c)).ToList();

        // ---------------- Out ----------------

        /// <summary>
        /// Carried away: on the road by the carrier's means over the land's own way (relayed when far), read when it gets
        /// there; the answer comes back the same way. <paramref name="by"/>: the one who took it, for the words.
        /// </summary>
        public static void Carried(NeighbourLetter l, MailCarrier c, Pawn by)
        {
            var town = TownOf(l, out var n);
            if (l == null || n == null || l.stage != LetterStage.Outbox) return;
            var map = l.map ?? c.map;
            bool theirs = c.people != null && c.people == n.faction;
            float days;
            Settlement relay = null;
            if (theirs || c.means == LetterMeans.Shuttle)
            {
                var info = PostReach.Of(map, town);
                days = PostMath.LegDays(info.walkDays, info.tiles, c.means);
                if (days < 0f) days = LetterMath.LegDays(info.tiles, c.means);
            }
            else days = PostReach.Days(map, town, c.means, out relay);
            if (days < 0f) days = LetterMath.LegDays(town != null ? LivingEvents.Distance(map.Tile, town.Tile) : 20f, c.means);
            l.carrier = c.kind;
            l.carrierName = c.name;
            l.carrierMeans = c.means;
            l.relayName = relay?.Name;
            l.backDays = days;
            l.packedBy = null;
            l.stage = LetterStage.Travelling;
            l.stageTick = Now;
            l.due = Now + Mathf.RoundToInt((days + NotableMath.ReplyDays(Rand.Value)) * GenDate.TicksPerDay);
            NotableBook.Changed();
            string key = relay != null ? "CC_Mail_Msg_TakenRelay" : "CC_Mail_Msg_Taken";
            // apart: told as the carrier sets off with it; the writer is not there for it.
            Notify.Message(key.Translate((c.name ?? "?").Named("CARRIER"), l.WriterName.Named("PAWN"), (l.toName ?? "?").Named("TO"), (relay?.Name ?? "").Named("RELAY")).Resolve(),
                by != null && by.Spawned ? new LookTargets(by) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        /// <summary>Taken out of the outbox again (the player's word, or the letter called off).</summary>
        public static void TakeBack(NeighbourLetter l)
        {
            if (l == null || l.stage != LetterStage.Outbox) return;
            NeighbourLetters.Cancel(l, "CC_Mail_Cancel_TakenBack");
        }
    }
}
