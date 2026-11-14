using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>Selene's happenings under way (OwnHappeningsBook.selene): the morning after a return, her face in the water, a watch kept company, the dark face kept.</summary>
    public class SeleneState : IExposable
    {
        /// <summary>The moon's last return looked at (GodsOnMapBook.lastReturn), and the one the morning card is still to tell: to whom, what, where, whether again.</summary>
        public int returnSeen = -1;
        public Map morningMap;
        public Pawn morningOf;
        public string morningKind;
        public IntVec3 morningAt = IntVec3.Invalid;
        public bool morningAgain, morningLooked;

        /// <summary>Her face in still water: where, who walks down to it, until when, whether they knelt.</summary>
        public Map waterMap;
        public IntVec3 waterAt = IntVec3.Invalid;
        public Pawn waterSeer;
        public int waterUntil = -1;

        /// <summary>The watch she keeps company: who, until when.</summary>
        public Pawn watcher;
        public int watchUntil = -1;

        /// <summary>The dark face kept at the door on a night with no moon: who keeps it, where, until when; whether they reached it.</summary>
        public Map darkMap;
        public Pawn darkKeeper;
        public IntVec3 darkAt = IntVec3.Invalid, darkFacing = IntVec3.Invalid;
        public int darkUntil = -1;
        public bool darkStood;

        public void ExposeData()
        {
            Scribe_Values.Look(ref returnSeen, "returnSeen", -1);
            Scribe_References.Look(ref morningMap, "morningMap");
            Scribe_References.Look(ref morningOf, "morningOf");
            Scribe_Values.Look(ref morningKind, "morningKind");
            Scribe_Values.Look(ref morningAt, "morningAt", IntVec3.Invalid);
            Scribe_Values.Look(ref morningAgain, "morningAgain");
            Scribe_Values.Look(ref morningLooked, "morningLooked");
            Scribe_References.Look(ref waterMap, "waterMap");
            Scribe_Values.Look(ref waterAt, "waterAt", IntVec3.Invalid);
            Scribe_References.Look(ref waterSeer, "waterSeer");
            Scribe_Values.Look(ref waterUntil, "waterUntil", -1);
            Scribe_References.Look(ref watcher, "watcher");
            Scribe_Values.Look(ref watchUntil, "watchUntil", -1);
            Scribe_References.Look(ref darkMap, "darkMap");
            Scribe_References.Look(ref darkKeeper, "darkKeeper");
            Scribe_Values.Look(ref darkAt, "darkAt", IntVec3.Invalid);
            Scribe_Values.Look(ref darkFacing, "darkFacing", IntVec3.Invalid);
            Scribe_Values.Look(ref darkUntil, "darkUntil", -1);
            Scribe_Values.Look(ref darkStood, "darkStood");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (morningOf == null) morningMap = null;
                if (waterMap == null || waterSeer == null) waterUntil = -1;
                if (watcher == null) watchUntil = -1;
                if (darkMap == null || darkKeeper == null) darkUntil = -1;
            }
        }
    }

    /// <summary>
    /// Selene, the Mirror (docs/gods-next-proposals.md, C 11): the moon, who has no light of her own and gives back at night
    /// what was done by day, as much as her face is full. Her own happenings:
    /// <list type="bullet">
    /// <item>What came back, told plainly: the morning after the moon returned someone's deed (GodReturn), they go and look at
    /// it (the thing by their bed, the wound closed, the frost about where they slept), and a card says what was found and by
    /// whose bed, and when it has happened to them before.</item>
    /// <item>Her face in still water: at the full moon, on a clear night, the moon lies in the still water by the colony bright
    /// as a face; whoever is up walks down to the water's edge, kneels by it a long while, and is the gladder for it.</item>
    /// <item>The watch she keeps company: someone keeping the night out of doors under a moon at least half full has the
    /// moonlight about them like company; they see further and aim steadier till dawn.</item>
    /// <item>The dark face kept: on a night with no moon, someone who keeps the night goes and stands at the colony's door
    /// looking out, and says aloud that nothing comes back tonight; while they stand there the dark that drifts (Undertow's)
    /// does not come in.</item>
    /// </list>
    /// </summary>
    public static class SeleneWays
    {
        public const string Key = "Selene";
        public const string Pool = "Rimfolk_Pool_GodSelene_Say";
        public const string WaterThought = "Rimfolk_Thought_Selene_FaceInWater";
        public const string WatchThought = "Rimfolk_Thought_Selene_KeptCompany";
        public const string DarkFaceThought = "Rimfolk_Thought_Selene_KeptDarkFace";
        public const string WatchHediff = "Rimfolk_MoonlitWatch";

        public static God God => OwnHappenings.God(Key) ?? GodReturn.Moon;

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color Silver = new Color(0.82f, 0.88f, 1f);

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.selene;
            int hour = GenLocalDate.HourOfDay(map);
            Returned(b);
            if (s.morningMap == map && s.morningOf != null && hour >= OwnHappeningsMath.MorningFrom && hour < OwnHappeningsMath.MorningTo) Morning(b, g, map, hour);
            if (MapFacts.ThreatOn(map)) return;
            var face = GodOmens.MoonTonight;
            if (OwnHappeningsMath.WaterHour(hour) && s.waterUntil < 0) Water(b, g, map, now, face);
            if (hour >= OwnHappeningsMath.WatchFrom && hour < OwnHappeningsMath.WatchTo && s.watchUntil < 0) Watch(b, g, map, now, face);
            if (hour >= OwnHappeningsMath.DarkFaceFrom && hour < OwnHappeningsMath.DarkFaceTo && s.darkUntil < 0) DarkFace(b, g, map, now, face);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.selene;
            if (s.waterUntil >= 0) OwnHappenings.Safe("her face in the water", () => WaterStep(b, now));
            if (s.watchUntil >= 0) OwnHappenings.Safe("the watch kept company", () => WatchStep(b, now));
            if (s.darkUntil >= 0) OwnHappenings.Safe("the dark face kept", () => DarkStep(b, now));
        }

        // ================= What came back =================

        /// <summary>The moon returned a deed in the night (GodsOnMapBook.lastReturn moved): whose, and what, from the gods' short memory ("return|Found|1234").</summary>
        private static void Returned(OwnHappeningsBook b)
        {
            var gb = GodsOnMapBook.Instance;
            var s = b.selene;
            if (gb == null || gb.lastReturn <= s.returnSeen) return;
            s.returnSeen = gb.lastReturn;
            // A return long past (an older save's first look) is not told anew.
            if (Now - gb.lastReturn > GenDate.TicksPerDay) return;
            string last = gb.lately?.LastOrDefault(x => x != null && x.StartsWith(GodReturn.What + "|", StringComparison.Ordinal));
            var parts = (last ?? "").Split('|');
            if (parts.Length < 3 || !int.TryParse(parts[2], out int id)) return;
            var p = Homes.All.Where(m => m != null).SelectMany(m => m.mapPawns.FreeColonistsSpawned).FirstOrDefault(x => x.thingIDNumber == id);
            if (p == null) return;
            Note(s, p, parts[1], gb.lately.Count(x => x != null && x.StartsWith(GodReturn.What + "|", StringComparison.Ordinal) && x.EndsWith("|" + id, StringComparison.Ordinal)) > 1);
        }

        public static void Note(SeleneState s, Pawn p, string kind, bool again)
        {
            s.morningMap = p.Map;
            s.morningOf = p;
            s.morningKind = kind;
            s.morningAt = p.CurrentBed()?.Position ?? p.Position;
            s.morningAgain = again;
            s.morningLooked = false;
        }

        /// <summary>In the morning, once they are up, they go and look at what came back; the card says what was found and by whose bed.</summary>
        private static void Morning(OwnHappeningsBook b, God g, Map map, int hour)
        {
            var s = b.selene;
            var p = s.morningOf;
            bool up = p != null && p.Spawned && GodOmens.Awake(p) && !p.InBed();
            if (!up && hour < 10) return;
            TellMorning(b, g, map, up);
        }

        public static void TellMorning(OwnHappeningsBook b, God g, Map map, bool look)
        {
            var s = b.selene;
            var p = s.morningOf;
            string kind = s.morningKind ?? "";
            var at = s.morningAt.IsValid ? s.morningAt : p?.Position ?? IntVec3.Invalid;
            s.morningMap = null;
            s.morningOf = null;
            if (p == null || !at.IsValid) return;
            Thing found = null;
            if (kind == "Found")
                found = GenRadial.RadialDistinctThingsAround(at, map, 2.9f, true).Where(t => t.def.category == ThingCategory.Item && t.Spawned).OrderByDescending(t => t.thingIDNumber).FirstOrDefault();
            if (look && p.Spawned)
            {
                var spot = found != null ? OwnHappenings.Beside(map, found.Position, p) : at;
                if (spot.IsValid && !p.Position.InHorDistOf(spot, 2f)) OwnHappenings.Walk(p, spot, 900);
                else OwnHappenings.Stand(p, found?.Position ?? at, 300);
                OwnHappenings.SayWith(p, Pool, g, new[] { (found?.LabelNoCount ?? "CC_GodSelene_Something".Translate().Resolve()).Named("THING") }, "morning", "morning:" + kind.ToLowerInvariant());
            }
            string key = "CC_GodSelene_Morning_" + kind;
            if (!key.CanTranslate()) return;
            string text = key.Translate(p.Named("PAWN"), (found?.LabelNoCount ?? "CC_GodSelene_Something".Translate().Resolve()).Named("THING")).Resolve();
            if (s.morningAgain) text += " " + "CC_GodSelene_Morning_Again".Translate(p.Named("PAWN")).Resolve();
            bool bad = kind == "Nightmare" || kind == "Frost" || kind == "AnimalBack";
            OwnHappenings.Card(g, map, "CC_GodSelene_Morning_Title", "CC_GodSelene_Morning_TitleNamed", text, found != null ? new LookTargets(found) : new LookTargets(p),
                bad ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent, p.Named("PAWN"));
            OwnHappenings.Came(b, OwnHappeningsMath.SeleneMorning, kind, p.thingIDNumber.ToString());
        }

        // ================= Her face in still water =================

        /// <summary>Still water by the colony (not running: a lake, a pond, a marsh), the nearest its people first, or none.</summary>
        public static IntVec3 StillWater(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            foreach (var c in GenRadial.RadialCellsAround(mid, 45f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map)) continue;
                var t = c.GetTerrain(map);
                if (t == null || t.defName.IndexOf("Moving", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (t.IsWater || t.defName.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0) return c;
            }
            return IntVec3.Invalid;
        }

        private static void Water(OwnHappeningsBook b, God g, Map map, int now, GodsOnMapMath.MoonFace face)
        {
            if (face != GodsOnMapMath.MoonFace.Full || !OwnHappenings.Clear(map)) return;
            var water = StillWater(map);
            var seer = water.IsValid ? Seer(map, water) : null;
            var t = OwnHappeningsMath.WaterTrigger(true, water.IsValid, true, seer != null);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.SeleneWater, t, g, map, now)) return;
            BeginWater(b, map, water, seer);
        }

        private static Pawn Seer(Map map, IntVec3 water) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && GodOmens.Awake(p) && !p.InBed() && p.Position.InHorDistOf(water, 45f) && WorshipJobs.Free(p, false)
                                                         && p.CanReach(water, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(water)).FirstOrDefault();

        public static bool BeginWater(OwnHappeningsBook b, Map map, IntVec3 water, Pawn seer)
        {
            var s = b.selene;
            if (!water.IsValid || seer == null) return false;
            s.waterMap = map;
            s.waterAt = water;
            s.waterSeer = seer;
            s.waterUntil = Now + GenDate.TicksPerHour * 2;
            GodSky.Stay(map, SkySign.Moon, water, GenDate.TicksPerHour * 2, 3.5f);
            OwnHappenings.Glow(map, water, Silver, 10, 1.2f, 0.45f);
            OwnHappenings.Walk(seer, OwnHappenings.Beside(map, water, seer));
            OwnHappenings.Came(b, OwnHappeningsMath.SeleneWater);
            b.Due(Now + 250);
            return true;
        }

        private static void WaterStep(OwnHappeningsBook b, int now)
        {
            var s = b.selene;
            var map = s.waterMap;
            var p = s.waterSeer;
            if (map == null || p == null || p.Dead || !p.Spawned || p.Map != map || now > s.waterUntil)
            {
                s.waterUntil = -1;
                s.waterSeer = null;
                return;
            }
            OwnHappenings.Glow(map, s.waterAt, Silver, 3, 1f, 0.4f);
            if (p.Position.InHorDistOf(s.waterAt, 2.2f))
            {
                Knelt(b, map, p);
                return;
            }
            if (p.CurJobDef != JobDefOf.Goto && GodOmens.Awake(p)) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.waterAt, p));
            b.Due(now + 250);
        }

        /// <summary>They kneel by the water a long while; the gladder for it, and a card of what they saw.</summary>
        private static void Knelt(OwnHappeningsBook b, Map map, Pawn p)
        {
            var s = b.selene;
            var g = God;
            var at = s.waterAt;
            s.waterUntil = -1;
            s.waterSeer = null;
            OwnHappenings.Stand(p, at, 1100);
            OwnHappenings.Memory(p, WaterThought);
            OwnHappenings.Say(p, Pool, g, "water");
            string text = "CC_GodSelene_Water_Text".Translate(p.Named("PAWN")).Resolve();
            OwnHappenings.Card(g, map, "CC_GodSelene_Water_Title", "CC_GodSelene_Water_TitleNamed", text, new LookTargets(p), LetterDefOf.PositiveEvent, p.Named("PAWN"));
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Omen, p, "CC_GodSelene_Saw_Water");
        }

        // ================= The watch she keeps company =================

        private static void Watch(OwnHappeningsBook b, God g, Map map, int now, GodsOnMapMath.MoonFace face)
        {
            bool up = face == GodsOnMapMath.MoonFace.Full || face == GodsOnMapMath.MoonFace.Half;
            var p = Watcher(map);
            var t = OwnHappeningsMath.WatchTrigger(up, OwnHappenings.Clear(map), p != null, p?.Drafted == true);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.SeleneWatch, t, g, map, now)) return;
            Company(b, map, p);
        }

        /// <summary>Someone of the colony up and out of doors in the small hours, under arms first.</summary>
        public static Pawn Watcher(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && p.Awake() && !p.Downed && !p.InMentalState && !p.InBed() && !p.Position.Roofed(map))
                .OrderByDescending(p => p.Drafted).FirstOrDefault();

        public static bool Company(OwnHappeningsBook b, Map map, Pawn p)
        {
            var s = b.selene;
            if (p == null) return false;
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(WatchHediff);
            if (def != null && !p.health.hediffSet.HasHediff(def)) p.health.AddHediff(def);
            s.watcher = p;
            s.watchUntil = Now + GenDate.TicksPerHour;
            GodSky.Stay(map, SkySign.Moon, p.Position, GenDate.TicksPerHour, 6f);
            OwnHappenings.Glow(map, p.Position, Silver, 10, 1.4f, 0.4f);
            OwnHappenings.Memory(p, WatchThought);
            var g = God;
            OwnHappenings.Say(p, Pool, g, "watch");
            OwnHappenings.Line(map, "CC_GodSelene_Watch_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
            if (g != null) OwnHappenings.Saw(g, map, p.Position, GodActKind.Blessing, p, "CC_GodSelene_Saw_Watch");
            OwnHappenings.Came(b, OwnHappeningsMath.SeleneWatch, null, p.thingIDNumber.ToString());
            b.Due(Now + 500);
            return true;
        }

        private static void WatchStep(OwnHappeningsBook b, int now)
        {
            var s = b.selene;
            var p = s.watcher;
            if (p == null || p.Dead || !p.Spawned || now > s.watchUntil)
            {
                s.watcher = null;
                s.watchUntil = -1;
                return;
            }
            if (!p.Position.Roofed(p.Map)) OwnHappenings.Glow(p.Map, p.Position, Silver, 4, 1.2f, 0.35f);
            b.Due(now + 500);
        }

        // ================= The dark face kept =================

        private static void DarkFace(OwnHappeningsBook b, God g, Map map, int now, GodsOnMapMath.MoonFace face)
        {
            var keeper = face == GodsOnMapMath.MoonFace.Gone ? Keeper(map) : null;
            var t = OwnHappeningsMath.DarkFaceTrigger(face == GodsOnMapMath.MoonFace.Gone, keeper != null);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.SeleneDarkFace, t, g, map, now)) return;
            BeginDarkFace(b, map, keeper);
        }

        /// <summary>Who keeps the night: a night owl, else one who holds the moon, else whoever is up and free.</summary>
        public static Pawn Keeper(Map map)
        {
            var moon = God;
            var owl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            return map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.DevelopmentalStage.Adult())
                .OrderByDescending(p => (owl != null && p.story?.traits?.HasTrait(owl) == true ? 50f : 0f) + PawnGods.HoldOf(p, moon)).FirstOrDefault();
        }

        public static bool BeginDarkFace(OwnHappeningsBook b, Map map, Pawn keeper)
        {
            var s = b.selene;
            if (keeper == null) return false;
            var door = GodOmens.OuterDoor(map);
            IntVec3 at = door == null ? IntVec3.Invalid : GenAdj.CellsAdjacentCardinal(door).FirstOrDefault(c => c.InBounds(map) && !c.Roofed(map) && c.Standable(map));
            if (at == default) at = IntVec3.Invalid;
            if (!at.IsValid) at = GodOmens.Open(map);
            if (!at.IsValid) return false;
            var outward = door != null ? at + (at - door.Position) * 6 : at + (at - OwnHappenings.Middle(map));
            s.darkMap = map;
            s.darkKeeper = keeper;
            s.darkAt = at;
            s.darkFacing = outward.ClampInsideMap(map);
            s.darkStood = false;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = hour >= 12 ? 24 - hour + OwnHappeningsMath.DarkFaceUntil : Math.Max(1, OwnHappeningsMath.DarkFaceUntil - hour);
            s.darkUntil = Now + hours * GenDate.TicksPerHour;
            OwnHappenings.Walk(keeper, at);
            OwnHappenings.Came(b, OwnHappeningsMath.SeleneDarkFace, null, keeper.thingIDNumber.ToString());
            b.Due(Now + 250);
            return true;
        }

        /// <summary>Someone keeps the dark face at this home's door tonight, and the dark that drifts does not come.</summary>
        public static bool KeptTonight(Map map)
        {
            var s = OwnHappenings.Book?.selene;
            return s != null && s.darkMap == map && s.darkUntil > Now && s.darkKeeper != null && !s.darkKeeper.Dead;
        }

        private static void DarkStep(OwnHappeningsBook b, int now)
        {
            var s = b.selene;
            var map = s.darkMap;
            var p = s.darkKeeper;
            if (map == null || p == null || p.Dead || !p.Spawned || p.Map != map || p.Downed || p.InMentalState)
            {
                s.darkUntil = -1;
                s.darkKeeper = null;
                return;
            }
            if (now > s.darkUntil)
            {
                if (s.darkStood)
                {
                    OwnHappenings.Memory(p, DarkFaceThought);
                    OwnHappenings.Line(map, "CC_GodSelene_DarkFace_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
                    var g = God;
                    if (g != null) OwnHappenings.Saw(g, map, s.darkAt, GodActKind.Omen, p, "CC_GodSelene_Saw_DarkFace");
                }
                s.darkUntil = -1;
                s.darkKeeper = null;
                return;
            }
            if (p.Position.InHorDistOf(s.darkAt, 1.5f))
            {
                if (!s.darkStood)
                {
                    s.darkStood = true;
                    OwnHappenings.Say(p, Pool, God, "darkface");
                }
                if (p.CurJobDef != JobDefOf.Wait_MaintainPosture && OwnHappenings.Yields(p)) OwnHappenings.Stand(p, s.darkFacing, GenDate.TicksPerHour);
            }
            else if (p.CurJobDef != JobDefOf.Goto && GodOmens.Awake(p) && !p.InBed() && OwnHappenings.Yields(p)) OwnHappenings.Walk(p, s.darkAt);
            b.Due(now + 400);
        }
    }

    /// <summary>Dev mode: Selene's happenings now (Debug actions, "Rimfolk", "Gods: Selene's ... now").</summary>
    public static class SeleneDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static readonly string[] Kinds = { "Found", "GoodDream", "Healed", "Nightmare", "Frost", "AnimalBack" };

        [DebugAction("Rimfolk", "Gods: Selene's morning card now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMorning()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            var p = map?.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).RandomElementWithFallback();
            if (b == null || p == null) return;
            Find.WindowStack.Add(new FloatMenu(Kinds.Select(k => new FloatMenuOption(k, () =>
            {
                SeleneWays.Note(b.selene, p, k, false);
                SeleneWays.TellMorning(b, SeleneWays.God, map, true);
                Say("the morning card for " + p.LabelShort + " (" + k + "; with Found, the newest thing by their bed).");
            })).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: Selene's face in the water now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWater()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var water = SeleneWays.StillWater(map);
            var p = water.IsValid ? map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && x.Awake() && x.CanReach(water, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(water)).FirstOrDefault() : null;
            Say(SeleneWays.BeginWater(b, map, water, p) ? p.LabelShort + " walks down to the water where the moon lies." : "no still water within reach (or nobody awake).");
        }

        [DebugAction("Rimfolk", "Gods: Selene keeps a watch company now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWatch()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = SeleneWays.Watcher(map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(OwnHappenings.Ours);
            Say(SeleneWays.Company(b, map, p) ? "the moonlight keeps " + p.LabelShort + " company till dawn." : "nobody to keep company.");
        }

        [DebugAction("Rimfolk", "Gods: Selene's dark face kept now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDarkFace()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = SeleneWays.Keeper(map);
            Say(SeleneWays.BeginDarkFace(b, map, p) ? p.LabelShort + " goes to keep the door till three o'clock; the dark that drifts does not come in meanwhile." : "nobody free to keep it.");
        }
    }
}
