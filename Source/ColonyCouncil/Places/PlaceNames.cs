using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// One place on a home map the colony's people have named (C25, Places/PlaceNames.cs): what named it (PlaceNameMath's
    /// kinds), its name as said ("the gate that held"), its middle and size, who named it and when, the thing it is tied to
    /// (the tree's id, -1 for none), and since when it is gone (-1 while it stands).
    /// </summary>
    public class NamedPlace : IExposable
    {
        public string kind, name, namer, namerId;
        public IntVec3 cell = IntVec3.Invalid;
        public int radius = 5, tick, anchor = -1, goneTick = -1;

        public bool Stands => goneTick < 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref namer, "namer");
            Scribe_Values.Look(ref namerId, "namerId");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "radius", 5);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref anchor, "anchor", -1);
            Scribe_Values.Look(ref goneTick, "goneTick", -1);
        }
    }

    /// <summary>
    /// The places a home map's people have named, kept with the map (a home given up takes its names with it), and a flood
    /// that has reached the colony's path or fields, noted while it lasts. Draws the names as quiet labels on the map with
    /// the setting "Names of places on the map" (off by default), the way the game draws its own labels.
    /// </summary>
    public class PlaceBook : MapComponent
    {
        public List<NamedPlace> places = new List<NamedPlace>();
        /// <summary>Where a flood now on the map has reached the colony's path or fields, and which it is ("path", "field"); Invalid with none.</summary>
        public IntVec3 floodCell = IntVec3.Invalid;
        public string floodWhat;
        /// <summary>When a place was last brought up at supper here (not saved: a load counts it as just now).</summary>
        [Unsaved] internal int lastSupper = -1;

        /// <summary>A god seen where a place may take its name, waiting for the moment to be over (not saved: a load lets it go).</summary>
        public sealed class Waiting
        {
            public IntVec3 cell;
            public string name;
            public int tick;
            public List<Pawn> saw;
        }

        [Unsaved] internal readonly List<Waiting> waiting = new List<Waiting>();

        public PlaceBook(Map map) : base(map)
        {
        }

        public static PlaceBook Of(Map map) => map?.GetComponent<PlaceBook>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref places, "places", LookMode.Deep);
            Scribe_Values.Look(ref floodCell, "floodCell", IntVec3.Invalid);
            Scribe_Values.Look(ref floodWhat, "floodWhat");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                places ??= new List<NamedPlace>();
                places.RemoveAll(p => p == null || p.name.NullOrEmpty() || !p.cell.IsValid || !PlaceNameMath.IsKind(p.kind));
                lastSupper = Find.TickManager?.TicksGame ?? -1;
            }
        }

        private static readonly Color LabelColor = new Color(0.93f, 0.89f, 0.80f);

        public override void MapComponentOnGUI()
        {
            if (!PlaceNames.LabelsOn || map != Find.CurrentMap || places.Count == 0 || Find.CameraDriver.CurrentZoom > CameraZoomRange.Middle) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < places.Count; i++)
            {
                var p = places[i];
                if (p == null || !p.cell.InBounds(map) || p.cell.Fogged(map)) continue;
                var c = LabelColor;
                c.a = PlaceNameMath.Alpha(p.goneTick, now, GenDate.TicksPerDay);
                GenMapUI.DrawThingLabel(p.cell.ToVector3Shifted().MapToUIPosition(), p.name.CapitalizeFirst(), c);
            }
        }
    }

    /// <summary>
    /// The colony's own names for its places (C25 of docs/content-update-proposals.md, the player's go of 20 November 2026).
    /// A place on a home map gets a name from what first happened there, given by one of the colony's people who was there,
    /// who says it aloud once (Speech.Say, Rimfolk_Pool_PlaceNamed), with a message pointing at the place and a line in the
    /// chronicle:
    /// <list type="bullet">
    /// <item>a god seen on the land (Gods.Moment: it came, acted, showed itself, blessed or raged where it stood, out in the
    /// open), named by the first who saw it, by what the colony knows: the colony's name for the god where it knows the god
    /// did it, else what was seen ("the rise where the great white thrumbo was seen"), never more (ArtTales.GodWords);</item>
    /// <item>the colony's first grave out in the open with one of its own in it: "the field of the graves", by the one of the
    /// colony near it who thought most of the dead, once someone is;</item>
    /// <item>a gathering (a party, a wedding, a council meeting) held out in the open beside a grown tree: "the old oak", by
    /// whoever called it, else one there;</item>
    /// <item>a raid held at a gate, nobody lost (DefenseLinesWatch, the line held): "the gate that held", by the one who stood
    /// nearest it;</item>
    /// <item>a flood (Odyssey's) that reached the colony's path or fields: "the drowned path", "the drowned field", by the one
    /// nearest when the water is gone.</item>
    /// </list>
    /// A place is a cell and a size (PlaceNameMath.Radius); a home keeps at most PlaceNameMath.MaxPlaces, none within
    /// PlaceNameMath.MinGap of another, and never two of one name. Once its tree is cut, its gate is gone or its graves are,
    /// the name fades from the label for a season and is no longer said, then is forgotten (a line in the chronicle). Words
    /// that point at a place find it with <see cref="Near"/>: a god's card when the god comes again where a name is
    /// (GodCard.Send), supper talk (Places/PlaceTalk.cs), the colony's own letters (the facts service's colony:place,
    /// {OURPLACE}), and the chronicle. Plain words for the names, the label and the messages. No harm.
    /// </summary>
    public static class PlaceNames
    {
        public const string NamedPool = "Rimfolk_Pool_PlaceNamed";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Places get names (the setting "The colony's names for its places", on).</summary>
        public static bool On => S != null && S.placeNames && Current.Game != null;

        /// <summary>The names show on the map as quiet labels (the setting "Names of places on the map", off).</summary>
        public static bool LabelsOn => On && S.placeLabels;

        private static int Now => Find.TickManager.TicksGame;

        // ================= Finding a place =================

        /// <summary>The named place a cell is at (the nearest, if two reach it), standing; null when none, or the setting is off.</summary>
        public static NamedPlace Near(Map map, IntVec3 cell)
        {
            var book = On ? PlaceBook.Of(map) : null;
            if (book == null || !cell.IsValid) return null;
            NamedPlace best = null;
            float bestD = float.MaxValue;
            foreach (var p in book.places)
            {
                if (p == null || !p.Stands) continue;
                float d = p.cell.DistanceToSquared(cell);
                if (!PlaceNameMath.Near(d, p.radius) || d >= bestD) continue;
                best = p;
                bestD = d;
            }
            return best;
        }

        /// <summary>The name of the place a cell is at, or null.</summary>
        public static string NameNear(Map map, IntVec3 cell) => Near(map, cell)?.name;

        /// <summary>The places of a home that stand (and so are spoken of).</summary>
        public static IEnumerable<NamedPlace> Standing(Map map)
        {
            var book = On ? PlaceBook.Of(map) : null;
            return book == null ? Enumerable.Empty<NamedPlace>() : book.places.Where(p => p != null && p.Stands);
        }

        /// <summary>The newest named place of a home that stands, or null.</summary>
        public static NamedPlace Newest(Map map) => Standing(map).OrderByDescending(p => p.tick).FirstOrDefault();

        /// <summary>
        /// A god's card (GodCard.Send) when the god comes where a place has a name: its words, and after them where it was
        /// ("It happened at the place the colony calls the old oak.").
        /// </summary>
        public static string WithPlace(string body, Map map, IntVec3 at)
        {
            try
            {
                var place = map != null && at.IsValid ? Near(map, at) : null;
                // Not one named just now: its own message has said it.
                string name = place != null && Now - place.tick >= GenDate.TicksPerHour ? place.name : null;
                return name == null ? body : body + "\n\n" + "CC_Place_CardAt".Translate(name.Named("PLACE")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names of places: a god's card", e);
                return body;
            }
        }

        // ================= Naming =================

        /// <summary>
        /// Names a place, if it may be named: on a home map, the setting on, room for one more, none near it already and none of
        /// that name, and someone of the colony there to name it who can speak. They say it aloud, the message points at the
        /// place, the chronicle keeps it. Returns the place, or null.
        /// </summary>
        internal static NamedPlace Give(Map map, string kind, IntVec3 cell, string name, Pawn namer, int anchor, string message)
        {
            if (!On || map == null || !Homes.IsHome(map) || !cell.IsValid || !cell.InBounds(map) || name.NullOrEmpty() || !PlaceNameMath.IsKind(kind)) return null;
            if (namer == null || !Together.Able(namer) || namer.Map != map || !namer.IsColonist || !People.HasInnerLife(namer)) return null;
            if (namer.health?.capacities != null && !namer.health.capacities.CapableOf(PawnCapacityDefOf.Talking)) return null;
            var book = PlaceBook.Of(map);
            if (book == null || !Free(book, cell, name)) return null;
            var place = new NamedPlace
            {
                kind = kind, name = name, cell = cell, radius = PlaceNameMath.Radius(kind), namer = namer.LabelShort, namerId = namer.ThingID, tick = Now, anchor = anchor
            };
            book.places.Add(place);
            try
            {
                string line = Flavor.FromPool(NamedPool, namer, null, null, "kind:" + kind);
                // apart: said over their own head, to nobody in particular, by the one naming the place.
                if (!line.NullOrEmpty()) Speech.Say(namer, line.Formatted(namer.Named("PAWN"), name.Named("PLACE")).Resolve().CapitalizeFirst());
                Tell.Message(NewsKind.Colony, Importance.Notable, map, message, new LookTargets(new TargetInfo(cell, map)), MessageTypeDefOf.PositiveEvent);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life,
                    "CC_Place_Chronicle".Translate(namer.Named("PAWN"), name.Named("PLACE")).Resolve(), namer));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names of places: telling a name", e);
            }
            return place;
        }

        /// <summary>Room for a name here: fewer than the most, none too near, none of the same name (even one fading).</summary>
        private static bool Free(PlaceBook book, IntVec3 cell, string name)
        {
            if (!PlaceNameMath.Room(book.places.Count)) return false;
            foreach (var p in book.places)
            {
                if (p == null) continue;
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return false;
                if (p.Stands && PlaceNameMath.TooClose(p.cell.DistanceToSquared(cell))) return false;
            }
            return true;
        }

        /// <summary>Out in the open: not under a roof (a room has its own name already).</summary>
        private static bool Open(Map map, IntVec3 c) => c.InBounds(map) && !c.Roofed(map);

        // ================= A god seen =================

        /// <summary>
        /// A god's moment was played and seen (Gods.Moment): where it stood, out in the open on a home map, the first who saw it
        /// names the place by what the colony knows of it (ArtTales.GodWords: the colony's name for the god where it knows it,
        /// else what was seen), and the ground it stood on.
        /// </summary>
        public static void GodSeen(PresenceSpec spec, List<Pawn> witnesses)
        {
            if (!On || spec?.god == null || spec.map == null || witnesses == null) return;
            if (spec.kind == PresenceKind.Omen || spec.kind == PresenceKind.Depart) return;
            try
            {
                var map = spec.map;
                var cell = spec.cell.IsValid ? spec.cell : spec.avatar != null && spec.avatar.Spawned && spec.avatar.Map == map ? spec.avatar.Position : IntVec3.Invalid;
                if (!cell.IsValid || !Open(map, cell)) return;
                var saw = witnesses.Where(p => p != null && p.IsColonist && p.Map == map).OrderBy(p => p.Position.DistanceToSquared(cell)).ToList();
                // What the colony knows of it now, as it was seen (the name is given after, but by what was known then).
                string god = PlaceNameMath.Definite(ArtTales.GodWords(spec.god, map));
                var book = PlaceBook.Of(map);
                if (saw.Count == 0 || god.NullOrEmpty() || book == null) return;
                string ground = ("CC_Place_Ground_" + Ground(map, cell)).Translate().Resolve();
                string name = "CC_Place_Name_God".Translate(ground.Named("GROUND"), god.Named("GOD")).Resolve();
                // Named once the moment and its card are over (the next hour's look, an hour on at least), by one who saw it.
                book.waiting.Add(new PlaceBook.Waiting { cell = cell, name = name, tick = Now, saw = saw });
                while (book.waiting.Count > 4) book.waiting.RemoveAt(0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names of places: a god seen", e);
            }
        }

        /// <summary>The ground round a cell, as PlaceNameMath.Ground's key.</summary>
        private static string Ground(Map map, IntVec3 cell)
        {
            bool water = false, rockFace = false, stony = false, sand = false, ice = false, marsh = false;
            int trees = 0;
            foreach (var c in GenRadial.RadialCellsAround(cell, 4f, true))
            {
                if (!c.InBounds(map)) continue;
                var t = c.GetTerrain(map);
                string n = t?.defName ?? "";
                if (t != null && t.IsWater) water = true;
                if (n.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0) sand = true;
                if (n.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0) ice = true;
                if (n.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0) marsh = true;
                if (n.IndexOf("Gravel", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("_Rough", StringComparison.OrdinalIgnoreCase) >= 0) stony = true;
                var e = c.GetEdifice(map);
                if (e?.def?.building != null && e.def.building.isNaturalRock) rockFace = true;
                var plant = c.GetPlant(map);
                if (plant != null && plant.def.plant != null && plant.def.plant.IsTree) trees++;
            }
            return PlaceNameMath.Ground(water, rockFace, stony, sand, ice, marsh, trees >= 4);
        }

        // ================= A raid held at a gate =================

        /// <summary>
        /// A raid came through a gate and nobody fell (DefenseLinesWatch.Finish, ArtTalesMath.Held): the one who stood nearest
        /// it names the gate, if a door of the colony's stands there.
        /// </summary>
        public static void LineHeld(Map map, IntVec3 gate, Pawn by)
        {
            if (!On || map == null || by == null || !gate.IsValid || !gate.InBounds(map)) return;
            try
            {
                if (!(gate.GetEdifice(map) is Building_Door door) || door.Faction != Faction.OfPlayer) return;
                string name = "CC_Place_Name_Gate".Translate().Resolve();
                Give(map, PlaceNameMath.Gate, gate, name, by, -1, "CC_Place_Named_Gate".Translate(by.Named("PAWN"), name.Named("PLACE")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names of places: the gate that held", e);
            }
        }

        // ================= The hour =================

        /// <summary>
        /// A god's place waiting to be named (<see cref="GodSeen"/>): an hour after the moment at least, by the one who saw it
        /// still nearest the place, if any of them is up and about on the map; let go after a day.
        /// </summary>
        private static void GodsWaiting(Map map, PlaceBook book)
        {
            int now = Now;
            for (int i = book.waiting.Count - 1; i >= 0; i--)
            {
                var w = book.waiting[i];
                if (now - w.tick < GenDate.TicksPerHour) continue;
                book.waiting.RemoveAt(i);
                if (now - w.tick > GenDate.TicksPerDay) continue;
                var namer = w.saw.Where(p => p != null && !p.Dead && p.Map == map && Together.Able(p)).OrderBy(p => p.Position.DistanceToSquared(w.cell)).FirstOrDefault();
                if (namer == null) continue;
                Give(map, PlaceNameMath.God, w.cell, w.name, namer, -1, "CC_Place_Named_God".Translate(namer.Named("PAWN"), w.name.Named("PLACE")).Resolve());
            }
        }

        /// <summary>The council's hour of life (CouncilGameComponent.HourlyLife): each home's god sightings, graves, gatherings, flood, fading places and supper.</summary>
        public static void Hourly()
        {
            if (!On) return;
            foreach (var map in Homes.All.ToList())
            {
                var book = PlaceBook.Of(map);
                if (book == null) continue;
                if (book.waiting.Count > 0) Safe("a god seen", () => GodsWaiting(map, book));
                Safe("the graves", () => Graves(map, book));
                Safe("a gathering by a tree", () => Gatherings(map, book));
                Safe("a flood", () => Flood(map, book));
                Safe("places gone", () => Fade(map, book));
                Safe("supper talk", () => PlaceTalk.Supper(map, book));
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("names of places: " + what, e);
            }
        }

        /// <summary>A grave of the colony's out in the open, with one of its own in it.</summary>
        private static bool OurGrave(Building_Grave g, Map map) =>
            g != null && g.Spawned && g.HasCorpse && g.Corpse?.InnerPawn != null && g.Corpse.InnerPawn.RaceProps.Humanlike
            && g.Corpse.InnerPawn.Faction == Faction.OfPlayer && Open(map, g.Position);

        /// <summary>
        /// The colony's first grave out in the open: once someone of the colony is near it, the one who thought most of the dead
        /// (else whoever is nearest) names the place.
        /// </summary>
        private static void Graves(Map map, PlaceBook book)
        {
            if (book.places.Any(p => p != null && p.kind == PlaceNameMath.Graves)) return;
            Building_Grave first = null;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b is Building_Grave g && OurGrave(g, map) && (first == null || g.thingIDNumber < first.thingIDNumber))
                    first = g;
            if (first == null) return;
            var dead = first.Corpse.InnerPawn;
            var near = map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p) && p.Position.InHorDistOf(first.Position, PlaceNameMath.GraveReach))
                .OrderByDescending(p => p.relations?.OpinionOf(dead) ?? 0).ThenBy(p => p.Position.DistanceToSquared(first.Position)).FirstOrDefault();
            if (near == null) return;
            string name = "CC_Place_Name_Graves".Translate().Resolve();
            Give(map, PlaceNameMath.Graves, first.Position, name, near, -1, "CC_Place_Named_Graves".Translate(near.Named("PAWN"), name.Named("PLACE")).Resolve());
        }

        /// <summary>The gatherings looked at already this session (by the lord's id): each names a tree once at most.</summary>
        private static readonly HashSet<int> seenLords = new HashSet<int>();

        private static Game seenGame;

        /// <summary>
        /// A gathering under way out in the open (a party, a wedding, a council meeting): beside a grown tree, the one who
        /// called it (else the one there nearest the tree) names the tree after it.
        /// </summary>
        private static void Gatherings(Map map, PlaceBook book)
        {
            if (seenGame != Current.Game)
            {
                seenGame = Current.Game;
                seenLords.Clear();
            }
            var lords = map.lordManager?.lords;
            if (lords == null) return;
            foreach (var lord in lords.ToList())
            {
                if (lord == null || !Gathering(lord.LordJob, out var spot, out var caller, out string word) || !seenLords.Add(lord.loadID)) continue;
                if (!spot.IsValid || !Open(map, spot)) continue;
                var tree = TreeBy(map, spot);
                if (tree == null) continue;
                var namer = caller != null && lord.ownedPawns.Contains(caller) ? caller
                    : lord.ownedPawns.Where(p => p != null && p.IsColonist).OrderBy(p => p.Position.DistanceToSquared(tree.Position)).FirstOrDefault();
                if (namer == null || !namer.Position.InHorDistOf(tree.Position, 30f)) continue;
                string treeWord = tree.def.label ?? "";
                string name = "CC_Place_Name_Tree".Translate(treeWord.Named("TREE")).Resolve();
                string what = ("CC_Place_Gathering_" + word).Translate().Resolve();
                Give(map, PlaceNameMath.Tree, tree.Position, name, namer, tree.thingIDNumber,
                    "CC_Place_Named_Tree".Translate(namer.Named("PAWN"), name.Named("PLACE"), what.Named("GATHERING")).Resolve());
            }
        }

        /// <summary>
        /// A gathering the colony holds, with its spot, who called it and its word's key: a wedding (the game's ceremony, the
        /// first of the two its caller), a council meeting, a party, else a gathering. False for any other lord.
        /// </summary>
        private static bool Gathering(LordJob job, out IntVec3 spot, out Pawn caller, out string word)
        {
            spot = IntVec3.Invalid;
            caller = null;
            word = null;
            switch (job)
            {
                case LordJob_Joinable_MarriageCeremony m:
                    spot = WeddingSpot(m);
                    caller = m.firstPawn;
                    word = "Wedding";
                    return true;
                case LordJob_Joinable_Gathering g:
                    spot = g.Spot;
                    caller = g.Organizer;
                    word = g is LordJob_CouncilMeeting ? "Council" : g is LordJob_Joinable_Party ? "Party" : "Gathering";
                    return true;
                default:
                    return false;
            }
        }

        private static AccessTools.FieldRef<LordJob_Joinable_MarriageCeremony, IntVec3> weddingSpotRef;
        private static bool weddingSpotLooked;

        /// <summary>Where the game holds a wedding (its ceremony's own spot, a private field), or Invalid.</summary>
        private static IntVec3 WeddingSpot(LordJob_Joinable_MarriageCeremony m)
        {
            if (!weddingSpotLooked)
            {
                weddingSpotLooked = true;
                try
                {
                    weddingSpotRef = AccessTools.FieldRefAccess<LordJob_Joinable_MarriageCeremony, IntVec3>("spot");
                }
                catch (Exception e)
                {
                    weddingSpotRef = null;
                    CouncilLog.ErrorOnce("names of places: a wedding's spot", e);
                }
            }
            return weddingSpotRef == null ? IntVec3.Invalid : weddingSpotRef(m);
        }

        /// <summary>A grown tree beside a gathering's spot (the nearest), or null.</summary>
        private static Plant TreeBy(Map map, IntVec3 spot)
        {
            Plant best = null;
            float bestD = float.MaxValue;
            foreach (var c in GenRadial.RadialCellsAround(spot, PlaceNameMath.TreeReach, true))
            {
                if (!c.InBounds(map)) continue;
                var p = c.GetPlant(map);
                if (p?.def?.plant == null || !p.def.plant.IsTree || p.LifeStage != PlantLifeStage.Mature) continue;
                float d = c.DistanceToSquared(spot);
                if (d >= bestD) continue;
                best = p;
                bestD = d;
            }
            return best;
        }

        /// <summary>
        /// A flood (Odyssey's seasonal flood) on the map: where it has reached the colony's path (a floor it laid) or its fields
        /// is noted; when the water is gone, the one of the colony nearest that place names it.
        /// </summary>
        private static void Flood(Map map, PlaceBook book)
        {
            var flood = DefDatabase<ThingDef>.GetNamedSilentFail("SeasonalFlood");
            var things = flood == null ? null : map.listerThings.ThingsOfDef(flood);
            if (things != null && things.Count > 0)
            {
                if (book.floodWhat == "path") return;
                var home = map.areaManager?.Home;
                foreach (var t in things)
                {
                    var c = t.Position;
                    if (!c.InBounds(map) || home != null && !home[c]) continue;
                    var terrain = c.GetTerrain(map);
                    bool path = terrain != null && (terrain.layerable || terrain.designationCategory != null);
                    bool field = map.zoneManager?.ZoneAt(c) is Zone_Growing;
                    string what = PlaceNameMath.FloodWhat(path, field);
                    if (what == null || what == book.floodWhat) continue;
                    book.floodCell = c;
                    book.floodWhat = what;
                    if (what == "path") return;
                }
                return;
            }
            if (!book.floodCell.IsValid) return;
            var cell = book.floodCell;
            string kind = book.floodWhat;
            book.floodCell = IntVec3.Invalid;
            book.floodWhat = null;
            if (kind == null || !cell.InBounds(map)) return;
            var namer = map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p)).OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
            if (namer == null) return;
            string name = ("CC_Place_Name_Flood_" + kind).Translate().Resolve();
            Give(map, PlaceNameMath.Flood, cell, name, namer, -1, "CC_Place_Named_Flood".Translate(namer.Named("PAWN"), name.Named("PLACE")).Resolve());
        }

        /// <summary>
        /// Places whose tree is cut down, whose gate is gone or whose graves are: gone from now (the label fades for a season,
        /// the name is no longer said), then forgotten, with a line in the chronicle.
        /// </summary>
        private static void Fade(Map map, PlaceBook book)
        {
            int now = Now;
            for (int i = book.places.Count - 1; i >= 0; i--)
            {
                var p = book.places[i];
                if (p == null) continue;
                if (p.Stands && Gone(map, p)) p.goneTick = now;
                if (!PlaceNameMath.Forgotten(p.goneTick, now, GenDate.TicksPerDay)) continue;
                book.places.RemoveAt(i);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Place_Forgotten".Translate(p.name.Named("PLACE")).Resolve()));
            }
        }

        /// <summary>What a place is tied to is gone: its tree, the door at its gate, every grave of the colony's there.</summary>
        private static bool Gone(Map map, NamedPlace p)
        {
            if (!p.cell.InBounds(map)) return true;
            switch (p.kind)
            {
                case PlaceNameMath.Tree:
                {
                    var plant = p.cell.GetPlant(map);
                    return plant == null || plant.thingIDNumber != p.anchor;
                }
                case PlaceNameMath.Gate:
                    return !(p.cell.GetEdifice(map) is Building_Door);
                case PlaceNameMath.Graves:
                    foreach (var b in map.listerBuildings.allBuildingsColonist)
                        if (b is Building_Grave g && g.Spawned && g.Position.InHorDistOf(p.cell, p.radius + PlaceNameMath.NearSlack)) return false;
                    return true;
                default:
                    return false;
            }
        }

        // ================= Dev mode =================

        /// <summary>Dev mode: names the place at a cell now, as a kind would, by the colonist nearest (the checks still hold).</summary>
        internal static NamedPlace DebugName(Map map, IntVec3 cell, string kind)
        {
            var namer = map?.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p)).OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
            if (namer == null) return null;
            string name;
            switch (kind)
            {
                case PlaceNameMath.Graves: name = "CC_Place_Name_Graves".Translate().Resolve(); break;
                case PlaceNameMath.Gate: name = "CC_Place_Name_Gate".Translate().Resolve(); break;
                case PlaceNameMath.Flood: name = "CC_Place_Name_Flood_path".Translate().Resolve(); break;
                case PlaceNameMath.Tree:
                {
                    var tree = TreeBy(map, cell);
                    name = "CC_Place_Name_Tree".Translate((tree?.def.label ?? "oak").Named("TREE")).Resolve();
                    return Give(map, kind, tree?.Position ?? cell, name, namer, tree?.thingIDNumber ?? -1,
                        "CC_Place_Named_Tree".Translate(namer.Named("PAWN"), name.Named("PLACE"), "CC_Place_Gathering_Party".Translate().Resolve().Named("GATHERING")).Resolve());
                }
                default:
                {
                    string ground = ("CC_Place_Ground_" + Ground(map, cell)).Translate().Resolve();
                    name = "CC_Place_Name_God".Translate(ground.Named("GROUND"), "CC_Place_DebugGod".Translate().Resolve().Named("GOD")).Resolve();
                    kind = PlaceNameMath.God;
                    break;
                }
            }
            return Give(map, kind, cell, name, namer, -1, ("CC_Place_Named_" + kind.CapitalizeFirst()).Translate(namer.Named("PAWN"), name.Named("PLACE")).Resolve());
        }
    }
}
