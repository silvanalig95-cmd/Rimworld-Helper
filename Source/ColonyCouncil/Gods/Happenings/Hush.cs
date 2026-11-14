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
    /// <summary>Hush's happenings under way (OwnHappeningsBook.hush): the word in the frost, a vigil, the walk round the edge, the frost on a grave.</summary>
    public class HushState : IExposable
    {
        /// <summary>The word in the frost: where, which word, who walks out to find it, until when it lasts.</summary>
        public Map wordMap;
        public IntVec3 wordAt = IntVec3.Invalid;
        public int word, wordUntil = -1;
        public Pawn wordFinder;

        /// <summary>The vigil: who keeps it, for whom, from when, how long they have sat by the bed so far.</summary>
        public Map vigilMap;
        public Pawn keeper, frail;
        public int vigilFrom = -1, vigilSat, vigilLast;
        public bool vigilSaid;

        /// <summary>The walk round the colony's edge: its middle, how far out, the steps done of how many, from which angle, until when; who saw it.</summary>
        public Map walkMap;
        public IntVec3 walkCentre = IntVec3.Invalid, walkLastCell = IntVec3.Invalid;
        public int walkRadius, walkStep, walkSteps, walkUntil = -1, walkDay = -1;
        public float walkAngle;
        public bool walkNearSaid, walkCardDue;
        public string walkSeers;

        /// <summary>The frost on a grave: which grave, whose, and when it melts.</summary>
        public Thing grave;
        public string graveDead;
        public int meltAt = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref wordMap, "wordMap");
            Scribe_Values.Look(ref wordAt, "wordAt", IntVec3.Invalid);
            Scribe_Values.Look(ref word, "word");
            Scribe_Values.Look(ref wordUntil, "wordUntil", -1);
            Scribe_References.Look(ref wordFinder, "wordFinder");
            Scribe_References.Look(ref vigilMap, "vigilMap");
            Scribe_References.Look(ref keeper, "keeper");
            Scribe_References.Look(ref frail, "frail");
            Scribe_Values.Look(ref vigilFrom, "vigilFrom", -1);
            Scribe_Values.Look(ref vigilSat, "vigilSat");
            Scribe_Values.Look(ref vigilLast, "vigilLast");
            Scribe_Values.Look(ref vigilSaid, "vigilSaid");
            Scribe_References.Look(ref walkMap, "walkMap");
            Scribe_Values.Look(ref walkCentre, "walkCentre", IntVec3.Invalid);
            Scribe_Values.Look(ref walkLastCell, "walkLastCell", IntVec3.Invalid);
            Scribe_Values.Look(ref walkRadius, "walkRadius");
            Scribe_Values.Look(ref walkStep, "walkStep");
            Scribe_Values.Look(ref walkSteps, "walkSteps");
            Scribe_Values.Look(ref walkUntil, "walkUntil", -1);
            Scribe_Values.Look(ref walkDay, "walkDay", -1);
            Scribe_Values.Look(ref walkAngle, "walkAngle");
            Scribe_Values.Look(ref walkNearSaid, "walkNearSaid");
            Scribe_Values.Look(ref walkCardDue, "walkCardDue");
            Scribe_Values.Look(ref walkSeers, "walkSeers");
            Scribe_References.Look(ref grave, "grave");
            Scribe_Values.Look(ref graveDead, "graveDead");
            Scribe_Values.Look(ref meltAt, "meltAt", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (wordMap == null) wordUntil = -1;
                if (keeper == null || frail == null || vigilMap == null) EndVigil();
                if (walkMap == null) walkUntil = -1;
                if (grave == null || grave.Destroyed) grave = null;
            }
        }

        public void EndVigil()
        {
            keeper = frail = null;
            vigilMap = null;
            vigilFrom = -1;
            vigilSat = 0;
            vigilSaid = false;
        }
    }

    /// <summary>
    /// Hush, the Long White (docs/gods-next-proposals.md, C 8): winter, cold and silence, which takes the weak first and spares
    /// those who prepare. Its own happenings, each played out on the map and said as seen:
    /// <list type="bullet">
    /// <item>The word in the frost (OwnHappeningsMath.Word): on a frosty morning, a single word written in rime beside the
    /// colony's outer door, pointing at something real (WARM for someone sleeping cold, BREAD for the stores short, WOOD for
    /// the fires' wood running out, COUNT for food left to spoil); the first one up walks out, reads it aloud and says what
    /// they take it to mean, and the card says the plain fact beside it.</item>
    /// <item>The old man's fire: in a cold snap, at dusk, one of the colony sits up by the bed of the old, a baby or the sick,
    /// keeping them warm through the night; kept till first light, the cold snap breaks that morning and the frost runs off.</item>
    /// <item>The walk round the edge: on the longest night (or a bitter one), a tall white figure walks all the way round the
    /// colony, out past the walls, leaving a line of frost an arm's width wide; it always moves on when anyone comes within
    /// twenty steps, and whoever goes toward it finds footprints that stop.</item>
    /// <item>Frost on the grave: a death in the lean months with food left to rot, and at dusk frost lies on the new grave and
    /// nowhere else, and Hush's own presence comes down cold over the colony; days later the frost melts into a ring of
    /// mushrooms (the soft mother taking back what the cold kept whole).</item>
    /// </list>
    /// </summary>
    public static class HushWays
    {
        public const string Key = "Hush";
        public const string Pool = "Rimfolk_Pool_GodHush_Say";
        public const string SatUpThought = "Rimfolk_Thought_Hush_SatUp";
        public const string SatWithThought = "Rimfolk_Thought_Hush_SatWith";

        public static God God => OwnHappenings.God(Key);

        private static int Now => Find.TickManager.TicksGame;

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.hush;
            int hour = GenLocalDate.HourOfDay(map);
            if (s.walkCardDue && s.walkMap == map && hour >= 6 && hour < 12) WalkCard(b, g, map);
            if (s.grave != null && s.grave.Spawned && s.grave.Map == map && now >= s.meltAt && hour >= 7 && hour < 18) Melt(b, map);
            if (MapFacts.ThreatOn(map)) return;
            if (hour >= OwnHappeningsMath.WindowFrom && hour < OwnHappeningsMath.WindowTo) Window(b, g, map, now);
            if (hour >= OwnHappeningsMath.VigilFrom && hour < OwnHappeningsMath.VigilTo) Vigil(b, g, map, now);
            if (OwnHappeningsMath.WalkHour(hour)) Walk(b, g, map, now);
            if (hour >= OwnHappeningsMath.GraveFrom && hour < OwnHappeningsMath.GraveTo) Grave(b, g, map, now);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.hush;
            if (s.wordMap != null) OwnHappenings.Safe("the word in the frost", () => WordStep(b, now));
            if (s.keeper != null) OwnHappenings.Safe("the vigil", () => VigilStep(b, now));
            if (s.walkMap != null && s.walkUntil >= 0) OwnHappenings.Safe("the walk round the edge", () => WalkStep(b, now));
        }

        private static bool InSeason(Map map, RimWorld.Season want)
        {
            var se = GenLocalDate.Season(map);
            return se == want || want == RimWorld.Season.Winter && se == RimWorld.Season.PermanentWinter;
        }

        // ================= The word in the frost =================

        private static void Window(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.hush;
            if (s.wordMap != null) return;
            var w = WordNow(map, out _);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.HushWindow, w.trigger, g, map, now)) return;
            Write(b, g, map, w.word);
        }

        /// <summary>The word the frost would write on this map now, and how strongly; the coldest sleeper for WARM.</summary>
        public static (OwnHappeningsMath.FrostWord word, GodPaceMath.Trigger trigger) WordNow(Map map, out Pawn cold)
        {
            cold = null;
            var people = map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).ToList();
            bool hypo = false, coldRoom = false;
            float coldest = 999f;
            foreach (var p in people)
            {
                float sev = p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f;
                if (sev >= 0.04f && !hypo)
                {
                    hypo = true;
                    cold = p;
                }
                var room = p.ownership?.OwnedBed?.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors && room.Temperature < OwnHappeningsMath.ColdRoom)
                {
                    coldRoom = true;
                    if (!hypo && room.Temperature < coldest)
                    {
                        coldest = room.Temperature;
                        cold = p;
                    }
                }
            }
            bool burnsWood = map.listerBuildings.allBuildingsColonist.Any(x => x.GetComp<CompHeatPusher>() != null && x.GetComp<CompRefuelable>() is CompRefuelable r
                                                                             && r.Props.fuelFilter != null && r.Props.fuelFilter.Allows(ThingDefOf.WoodLog));
            float wood = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
            return OwnHappeningsMath.Word(map.mapTemperature.OutdoorTemp, hypo, coldRoom, OwnHappenings.FoodDays(map), burnsWood, wood, OwnHappenings.Wasting(map) != null);
        }

        /// <summary>The word written in rime beside the outer door (else on open ground at the edge); the first one up walks out to it.</summary>
        public static bool Write(OwnHappeningsBook b, God g, Map map, OwnHappeningsMath.FrostWord word)
        {
            var s = b.hush;
            if (word == OwnHappeningsMath.FrostWord.None) return false;
            // A window of the colony's where there is one (a mod's, such as Open The Windows', by name), else the outer door.
            var door = Window(map) ?? GodOmens.OuterDoor(map);
            IntVec3 at = door == null ? IntVec3.Invalid : GenAdj.CellsAdjacentCardinal(door).FirstOrDefault(c => c.InBounds(map) && !c.Roofed(map) && c.Standable(map));
            if (at == default) at = IntVec3.Invalid;
            if (!at.IsValid) at = GodOmens.Open(map);
            if (!at.IsValid) return false;
            // The letters: a short row of rime along the wall's line, a gap between each letter's strokes.
            var along = door != null && (at.x != door.Position.x) ? new IntVec3(0, 0, 1) : new IntVec3(1, 0, 0);
            for (int k = -2; k <= 2; k++)
            {
                var c = at + along * k + (at - (door?.Position ?? at));
                OwnHappenings.Rime(map, c, k % 2 == 0 ? 0.6f : 0.4f);
            }
            OwnHappenings.ModMote(map, at, new Color(0.85f, 0.93f, 1f), 0.9f, "Mote_WhiteIce", "Mote_Ice");
            OwnHappenings.Glow(map, at, new Color(0.85f, 0.93f, 1f), 6, 1.5f, 0.35f);
            s.wordMap = map;
            s.wordAt = at;
            s.word = (int)word;
            s.wordUntil = Now + GenDate.TicksPerHour * 6;
            s.wordFinder = Finder(map, at);
            if (s.wordFinder != null) OwnHappenings.Walk(s.wordFinder, OwnHappenings.Beside(map, at, s.wordFinder));
            OwnHappenings.Came(b, OwnHappeningsMath.HushWindow, word.ToString());
            b.Due(Now + 250);
            return true;
        }

        /// <summary>A window of the colony's that looks out under the open sky (any building of the player's mods called a window), the nearest its people first; or null.</summary>
        public static Building Window(Map map)
        {
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? map.Center;
            return map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.defName.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0 && b.def.size == IntVec2.One
                                                                       && GenAdj.CellsAdjacentCardinal(b).Any(c => c.InBounds(map) && !c.Roofed(map) && c.Standable(map)))
                .OrderBy(b => b.Position.DistanceToSquared(near)).FirstOrDefault();
        }

        private static Pawn Finder(Map map, IntVec3 at) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.CanReach(at, PathEndMode.Touch, Danger.Some)).OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();

        private static void WordStep(OwnHappeningsBook b, int now)
        {
            var s = b.hush;
            var map = s.wordMap;
            if (map == null || !map.IsPlayerHome || now > s.wordUntil)
            {
                // Melted before anyone came out to it: nobody saw it, nothing is told.
                s.wordMap = null;
                s.wordFinder = null;
                return;
            }
            var p = s.wordFinder;
            if (p == null || p.Dead || !p.Spawned || p.Map != map || !GodOmens.Awake(p))
            {
                p = s.wordFinder = Finder(map, s.wordAt);
                if (p != null) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.wordAt, p));
            }
            else if (p.Position.InHorDistOf(s.wordAt, 2.5f))
            {
                Found(b, map, p);
                return;
            }
            else if (p.CurJobDef != JobDefOf.Goto && OwnHappenings.Free(p)) OwnHappenings.Walk(p, OwnHappenings.Beside(map, s.wordAt, p));
            b.Due(now + 250);
        }

        /// <summary>They stand before it, read it aloud and say what they take it to mean; the card has the word and the plain fact beside it.</summary>
        private static void Found(OwnHappeningsBook b, Map map, Pawn p)
        {
            var s = b.hush;
            var g = God;
            var word = (OwnHappeningsMath.FrostWord)s.word;
            var at = s.wordAt;
            s.wordMap = null;
            s.wordFinder = null;
            OwnHappenings.Stand(p, at, 360);
            WordNow(map, out var cold);
            string w = ("CC_GodHush_Word_" + word).Translate().Resolve();
            string fact = Fact(map, word, cold);
            OwnHappenings.SayWith(p, Pool, g, new[] { w.Named("WORD") }, "word", "word:" + word.ToString().ToLowerInvariant());
            var window = Window(map);
            string where = window != null && window.Position.InHorDistOf(at, 1.5f)
                ? "CC_GodHush_Where_Window".Translate(window.LabelNoCount.Named("DOOR")).Resolve()
                : GodOmens.OuterDoor(map) is Building_Door d && d.Position.InHorDistOf(at, 1.5f)
                    ? "CC_GodHush_Where_Door".Translate(d.LabelNoCount.Named("DOOR")).Resolve()
                    : "CC_GodHush_Where_Ground".Translate().Resolve();
            string text = "CC_GodHush_Word_Text".Translate(p.Named("PAWN"), w.Named("WORD"), where.Named("WHERE")).Resolve() + (fact.NullOrEmpty() ? "" : "\n\n" + fact);
            OwnHappenings.Card(g, map, "CC_GodHush_Word_Title", "CC_GodHush_Word_TitleNamed", text, new LookTargets(new TargetInfo(at, map)), null, w.Named("WORD"));
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Omen, p, "CC_GodHush_Saw_Word", w.Named("WORD"));
        }

        /// <summary>The plain fact beside the word, as anyone of the colony could see it (never the god's mind).</summary>
        private static string Fact(Map map, OwnHappeningsMath.FrostWord word, Pawn cold)
        {
            switch (word)
            {
                case OwnHappeningsMath.FrostWord.Warm:
                    return cold == null ? null : "CC_GodHush_Fact_Warm".Translate(cold.Named("COLD")).Resolve();
                case OwnHappeningsMath.FrostWord.Bread:
                    return "CC_GodHush_Fact_Bread".Translate(Mathf.Max(0, Mathf.FloorToInt(OwnHappenings.FoodDays(map))).Named("DAYS")).Resolve();
                case OwnHappeningsMath.FrostWord.Wood:
                    return "CC_GodHush_Fact_Wood".Translate(map.resourceCounter.GetCount(ThingDefOf.WoodLog).Named("WOOD")).Resolve();
                case OwnHappeningsMath.FrostWord.Count:
                {
                    var t = OwnHappenings.Wasting(map);
                    return t == null ? null : "CC_GodHush_Fact_Count".Translate(t.LabelShort.Named("WHAT")).Resolve();
                }
            }
            return null;
        }

        // ================= The old man's fire =================

        private static void Vigil(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.hush;
            if (s.keeper != null) return;
            bool snap = OwnHappenings.Condition(map, "ColdSnap") != null;
            var frail = snap ? Frail(map) : null;
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.HushVigil, OwnHappeningsMath.VigilTrigger(snap, frail != null), g, map, now)) return;
            var keeper = Keeper(map, frail);
            if (keeper != null) BeginVigil(b, map, frail, keeper);
        }

        /// <summary>The frail of the colony abed (the old, a baby, the sick), the coldest room first.</summary>
        public static Pawn Frail(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && p.InBed() && OwnHappeningsMath.Frail(p.ageTracker.AgeBiologicalYearsFloat, SickAbed(p)))
                .OrderBy(p => p.GetRoom()?.Temperature ?? 20f).FirstOrDefault();

        private static bool SickAbed(Pawn p) => p.InBed() && (HealthAIUtility.ShouldSeekMedicalRest(p) || p.health.hediffSet.hediffs.Any(h => h.def.makesSickThought));

        /// <summary>Who sits up with them: a grown one of the colony free now, the fondest of them first (the kind and Hush's own a little more).</summary>
        public static Pawn Keeper(Map map, Pawn frail)
        {
            if (frail == null) return null;
            var hush = God;
            return map.mapPawns.FreeColonistsSpawned.Where(p => p != frail && OwnHappenings.Free(p) && p.DevelopmentalStage.Adult() && p.CanReach(frail, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => (p.relations?.OpinionOf(frail) ?? 0) + (p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 30 : 0) + PawnGods.HoldOf(p, hush) * 0.3f)
                .FirstOrDefault();
        }

        public static bool BeginVigil(OwnHappeningsBook b, Map map, Pawn frail, Pawn keeper)
        {
            var s = b.hush;
            if (frail == null || keeper == null) return false;
            s.vigilMap = map;
            s.frail = frail;
            s.keeper = keeper;
            s.vigilFrom = s.vigilLast = Now;
            s.vigilSat = 0;
            s.vigilSaid = false;
            OwnHappenings.Walk(keeper, OwnHappenings.Beside(map, frail.Position, keeper));
            OwnHappenings.Came(b, OwnHappeningsMath.HushVigil, null, frail.thingIDNumber.ToString());
            b.Due(Now + 250);
            return true;
        }

        private static void VigilStep(OwnHappeningsBook b, int now)
        {
            var s = b.hush;
            var map = s.vigilMap;
            var k = s.keeper;
            var f = s.frail;
            if (map == null || k == null || f == null || f.Dead || !f.Spawned || f.Map != map || k.Dead || !k.Spawned || k.Map != map || k.Downed || k.Drafted || k.InMentalState)
            {
                s.EndVigil();
                return;
            }
            var snap = OwnHappenings.Condition(map, "ColdSnap");
            if (snap == null)
            {
                // The cold went its own way before first light: the vigil just ends.
                s.EndVigil();
                return;
            }
            bool beside = k.Position.InHorDistOf(f.Position, 2.5f);
            if (beside)
            {
                s.vigilSat += Math.Max(0, now - s.vigilLast);
                if (!s.vigilSaid)
                {
                    s.vigilSaid = true;
                    // together: the keeper has just sat down at the bedside of the one they say it of.
                    OwnHappenings.SayWith(k, Pool, God, new[] { f.Named("OTHER") }, "vigil", "age" + OwnHappenings.Age);
                }
                if (k.CurJobDef != JobDefOf.Wait_MaintainPosture && OwnHappenings.Yields(k)) OwnHappenings.Stand(k, f.Position, GenDate.TicksPerHour);
            }
            else if (k.CurJobDef != JobDefOf.Goto && !k.InBed() && k.Awake() && OwnHappenings.Yields(k)) OwnHappenings.Walk(k, OwnHappenings.Beside(map, f.Position, k));
            s.vigilLast = now;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour == OwnHappeningsMath.VigilDawn && now - s.vigilFrom > GenDate.TicksPerHour * 5)
            {
                if (OwnHappeningsMath.VigilKept(s.vigilSat, now - s.vigilFrom)) Thaw(b, map, k, f, snap);
                s.EndVigil();
                return;
            }
            b.Due(now + 500);
        }

        /// <summary>
        /// Kept till first light: the cold snap breaks that morning, the frost runs off the ground round them like water, and
        /// the card says who sat with whom.
        /// </summary>
        private static void Thaw(OwnHappeningsBook b, Map map, Pawn k, Pawn f, GameCondition snap)
        {
            // together: the vigil brought the keeper to the bedside, and they sat there through the night.
            var g = God;
            try
            {
                snap.End();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the cold breaking after a vigil", e);
            }
            foreach (var c in GenRadial.RadialCellsAround(f.Position, 9f, true).Where(c => c.InBounds(map) && !c.Roofed(map)))
            {
                float d = map.snowGrid.GetDepth(c);
                if (d > 0f) map.snowGrid.SetDepth(c, Mathf.Max(0f, d - 0.4f));
                if (Rand.Chance(0.15f)) FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(0.5f, 0.9f), new Color(0.75f, 0.85f, 0.95f, 0.4f));
            }
            OwnHappenings.Glow(map, f.Position, new Color(1f, 0.9f, 0.7f), 8, 1f, 0.4f);
            OwnHappenings.Memory(k, SatUpThought);
            OwnHappenings.Memory(f, SatWithThought, k);
            string warmth = ("CC_GodHush_Warmth_" + OwnHappenings.Age).Translate().Resolve();
            string text = "CC_GodHush_Thaw_Text".Translate(k.Named("KEEPER"), f.Named("FRAIL"), warmth.Named("WARMTH")).Resolve();
            OwnHappenings.Card(g, map, "CC_GodHush_Thaw_Title", "CC_GodHush_Thaw_TitleNamed", text, new LookTargets(f), LetterDefOf.PositiveEvent);
            OwnHappenings.Say(k, Pool, g, "thaw");
            if (g != null) OwnHappenings.Saw(g, map, f.Position, GodActKind.Blessing, k, "CC_GodHush_Saw_Thaw", k.LabelShort.Named("KEEPER"), f.LabelShort.Named("FRAIL"));
        }

        // ================= The walk round the edge =================

        private static void Walk(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.hush;
            if (s.walkUntil >= 0 || s.walkCardDue) return;
            bool winter = InSeason(map, RimWorld.Season.Winter);
            bool longest = OwnHappeningsMath.LongestNight(winter, GenLocalDate.DayOfSeason(map));
            var t = OwnHappeningsMath.WalkTrigger(longest, map.mapTemperature.OutdoorTemp);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.HushWalk, t, g, map, now)) return;
            BeginWalk(b, map);
        }

        /// <summary>The figure begins its walk: out past the colony's farthest building, from a random side.</summary>
        public static bool BeginWalk(OwnHappeningsBook b, Map map)
        {
            var s = b.hush;
            var mid = OwnHappenings.Middle(map);
            float far = 0f;
            foreach (var x in map.listerBuildings.allBuildingsColonist)
                if (x.Spawned && x.def.building != null && !x.def.building.isNaturalRock)
                    far = Mathf.Max(far, x.Position.DistanceTo(mid));
            s.walkMap = map;
            s.walkCentre = mid;
            s.walkRadius = OwnHappeningsMath.WalkRadius(Mathf.Min(far, 60f));
            s.walkSteps = OwnHappeningsMath.WalkSteps(s.walkRadius);
            s.walkStep = 0;
            s.walkAngle = Rand.Range(0f, Mathf.PI * 2f);
            s.walkUntil = Now + s.walkSteps * OwnHappeningsMath.WalkStepTicks + GenDate.TicksPerHour * 2;
            s.walkLastCell = IntVec3.Invalid;
            s.walkNearSaid = false;
            s.walkSeers = null;
            s.walkCardDue = false;
            OwnHappenings.Came(b, OwnHappeningsMath.HushWalk);
            b.Due(Now + 1);
            return true;
        }

        private static IntVec3 RingCell(HushState s, int step, Map map)
        {
            float a = s.walkAngle + step * (Mathf.PI * 2f / Mathf.Max(1, s.walkSteps));
            var c = new IntVec3(s.walkCentre.x + Mathf.RoundToInt(Mathf.Cos(a) * s.walkRadius), 0, s.walkCentre.z + Mathf.RoundToInt(Mathf.Sin(a) * s.walkRadius));
            return c.ClampInsideMap(map);
        }

        private static void WalkStep(OwnHappeningsBook b, int now)
        {
            var s = b.hush;
            var map = s.walkMap;
            if (map == null || !map.IsPlayerHome)
            {
                s.walkMap = null;
                s.walkUntil = -1;
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (now > s.walkUntil || s.walkStep >= s.walkSteps || hour >= 5 && hour < 21)
            {
                EndWalk(s, map);
                return;
            }
            var cell = RingCell(s, s.walkStep, map);
            // Anyone within twenty steps, and it has moved on: footprints where it stood, and then none.
            var near = map.mapPawns.FreeColonistsSpawned.Where(p => p.Spawned && !p.Downed && p.Awake() && p.Position.InHorDistOf(cell, OwnHappeningsMath.WalkReach))
                .OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
            if (near != null)
            {
                Footprints(map, near.Position, cell);
                if (!s.walkNearSaid && GodOmens.Awake(near))
                {
                    s.walkNearSaid = true;
                    OwnHappenings.Say(near, Pool, God, "walk", "walk:near");
                    Seen(s, near);
                }
                s.walkStep = Math.Min(s.walkSteps, s.walkStep + OwnHappeningsMath.WalkFlee);
                cell = RingCell(s, s.walkStep, map);
            }
            // The line of frost from where it was to where it is.
            if (s.walkLastCell.IsValid)
                foreach (var c in GenSight.PointsOnLineOfSight(s.walkLastCell, cell))
                    OwnHappenings.Rime(map, c, 0.45f);
            OwnHappenings.Rime(map, cell, 0.5f);
            s.walkLastCell = cell;
            GodSky.Stay(map, SkySign.Figure, cell, OwnHappeningsMath.WalkStepTicks + 40, 6f);
            if (s.walkSeers.NullOrEmpty())
            {
                var seer = map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map) && p.Position.InHorDistOf(cell, 45f))
                    .OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
                if (seer != null)
                {
                    OwnHappenings.Say(seer, Pool, God, "walk", "walk:seen");
                    Seen(s, seer);
                }
            }
            s.walkStep++;
            b.Due(now + OwnHappeningsMath.WalkStepTicks);
        }

        private static void Seen(HushState s, Pawn p)
        {
            var names = (s.walkSeers ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!names.Contains(p.LabelShort)) names.Add(p.LabelShort);
            s.walkSeers = string.Join("|", names);
        }

        private static void Footprints(Map map, IntVec3 from, IntVec3 to)
        {
            var dir = (to - from).ToVector3();
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            float rot = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
            {
                var c = (to.ToVector3Shifted() - dir * (i * 1.5f)).ToIntVec3();
                if (c.InBounds(map) && !c.Roofed(map)) FleckMaker.PlaceFootprint(c.ToVector3Shifted(), map, rot);
            }
        }

        /// <summary>The walk ends: what it walked of the ring stays as frost, and the card comes at first light.</summary>
        private static void EndWalk(HushState s, Map map)
        {
            s.walkUntil = -1;
            s.walkCardDue = s.walkStep >= s.walkSteps / 3;
            s.walkDay = GenDate.DaysPassed;
            if (!s.walkCardDue) s.walkMap = null;
        }

        private static void WalkCard(OwnHappeningsBook b, God g, Map map)
        {
            var s = b.hush;
            s.walkCardDue = false;
            var names = (s.walkSeers ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string how = (s.walkStep >= s.walkSteps ? "CC_GodHush_Walk_All" : "CC_GodHush_Walk_Part").Translate().Resolve();
            string text = names.Count > 0
                ? "CC_GodHush_Walk_TextSeen".Translate(names.ToCommaList(true).Named("WHO"), Homes.Name(map).Named("HOME"), how.Named("HOW")).Resolve()
                : "CC_GodHush_Walk_Text".Translate(Homes.Name(map).Named("HOME"), how.Named("HOW")).Resolve();
            var at = RingCell(s, 0, map);
            OwnHappenings.Card(g, map, "CC_GodHush_Walk_Title", "CC_GodHush_Walk_TitleNamed", text, new LookTargets(new TargetInfo(at, map)));
            var witness = names.Count > 0 ? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.LabelShort == names[0]) : null;
            if (g != null) OwnHappenings.Saw(g, map, at, names.Count > 0 ? GodActKind.Act : GodActKind.Mark, witness, names.Count > 0 ? "CC_GodHush_Saw_WalkSeen" : "CC_GodHush_Saw_Walk", how.Named("HOW"));
            s.walkMap = null;
        }

        /// <summary>The walk was last night (Helios's sun comes back the morning after).</summary>
        public static bool WalkedLastNight(Map map)
        {
            var s = OwnHappenings.Book?.hush;
            return s != null && s.walkDay >= 0 && GenDate.DaysPassed - s.walkDay <= 1 && (s.walkMap == null || s.walkMap == map);
        }

        // ================= Frost on the grave =================

        private static void Grave(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = b.hush;
            if (s.grave != null) return;
            var grave = NewestGrave(map, out float days);
            if (grave == null) return;
            var t = OwnHappeningsMath.GraveTrigger(days, InSeason(map, RimWorld.Season.Winter), OwnHappenings.FoodDays(map), OwnHappenings.Wasting(map) != null);
            if (!OwnHappenings.Roll(b, OwnHappeningsMath.HushGrave, t, g, map, now)) return;
            Lay(b, g, map, grave);
        }

        /// <summary>The newest grave of one of the colony's own, and how many days since they died.</summary>
        public static Building_Grave NewestGrave(Map map, out float days)
        {
            days = 999f;
            Building_Grave best = null;
            foreach (var x in map.listerBuildings.allBuildingsColonist)
            {
                if (!(x is Building_Grave gr) || !gr.HasCorpse) continue;
                var who = gr.Corpse?.InnerPawn;
                if (who == null || who.Faction != Faction.OfPlayer || who.RaceProps?.Humanlike != true) continue;
                float d = (Now - gr.Corpse.timeOfDeath) / (float)GenDate.TicksPerDay;
                if (d < days)
                {
                    days = d;
                    best = gr;
                }
            }
            return best;
        }

        /// <summary>Frost lies on the grave at dusk and nowhere else; the colony goes quiet and Hush's own white silence comes down.</summary>
        public static bool Lay(OwnHappeningsBook b, God g, Map map, Building_Grave grave)
        {
            var s = b.hush;
            if (grave?.Corpse?.InnerPawn == null) return false;
            string dead = grave.Corpse.InnerPawn.LabelShort;
            foreach (var c in grave.OccupiedRect().ExpandedBy(1).Cells) OwnHappenings.Rime(map, c, 0.7f);
            OwnHappenings.ModMote(map, grave.Position, new Color(0.9f, 0.95f, 1f), 1.2f, "Mote_WhiteIce", "Mote_Ice");
            OwnHappenings.Glow(map, grave.Position, new Color(0.88f, 0.94f, 1f), 10, 1.2f, 0.4f);
            try
            {
                if (Find.CurrentMap == map) Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(12f, 0.6f, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the silence over a grave", e);
            }
            bool cold = false;
            if (g != null && map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() == null && GodPresenceOwn.DefOf(g) != null)
            {
                string known = Gods.CardName(g, map);
                var look = GodsWiring.LookOf(g);
                GameCondition_GodPresence.Begin(map, PresenceKind.Wrath, g.colour, look?.still ?? 0.5f, known ?? GodLore.LabelFor(Gods.Name(g)), known == null, GodPresenceOwn.DefOf(g), g.Key);
                GodPresenceOwn.Forget(map);
                cold = map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() != null;
            }
            var seer = OwnHappenings.Seer(map, grave.Position, 25f);
            if (seer != null) OwnHappenings.SayWith(seer, Pool, g, new[] { dead.Named("DEAD") }, "grave");
            string text = (cold ? "CC_GodHush_Grave_Text" : "CC_GodHush_Grave_TextQuiet").Translate(dead.Named("DEAD"), Homes.Name(map).Named("HOME")).Resolve();
            OwnHappenings.Card(g, map, "CC_GodHush_Grave_Title", "CC_GodHush_Grave_TitleNamed", text, new LookTargets(grave), LetterDefOf.NegativeEvent, dead.Named("DEAD"));
            if (g != null) OwnHappenings.Saw(g, map, grave.Position, GodActKind.Omen, seer, "CC_GodHush_Saw_Grave", dead.Named("DEAD"));
            s.grave = grave;
            s.graveDead = dead;
            s.meltAt = Now + Mathf.RoundToInt(Rand.Range(OwnHappeningsMath.MeltDaysMin, OwnHappeningsMath.MeltDaysMax) * GenDate.TicksPerDay);
            OwnHappenings.Came(b, OwnHappeningsMath.HushGrave, null, grave.Corpse.InnerPawn.thingIDNumber.ToString());
            return true;
        }

        private static readonly string[] Mushrooms = { "Glowstool", "Agarilux", "Bryolux" };

        /// <summary>Days later the frost on the grave melts, and a ring of mushrooms stands where it lay: the soft mother takes back what the cold kept.</summary>
        public static bool Melt(OwnHappeningsBook b, Map map)
        {
            var s = b.hush;
            var grave = s.grave;
            string dead = s.graveDead ?? "";
            s.grave = null;
            s.meltAt = -1;
            if (grave == null || !grave.Spawned || grave.Map != map) return false;
            foreach (var c in grave.OccupiedRect().ExpandedBy(2).Cells.Where(c => c.InBounds(map)))
            {
                float d = map.snowGrid.GetDepth(c);
                if (d > 0f) map.snowGrid.SetDepth(c, Mathf.Max(0f, d - 0.5f));
            }
            var kinds = Mushrooms.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.plant != null).ToList();
            int placed = 0;
            foreach (var c in grave.OccupiedRect().ExpandedBy(2).EdgeCells)
            {
                if (!c.InBounds(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || !c.Standable(map) || map.fertilityGrid.FertilityAt(c) <= 0f || kinds.Count == 0) continue;
                try
                {
                    var def = kinds.FirstOrDefault(k => k.CanEverPlantAt(c, map, true)) ?? kinds[0];
                    var plant = (Plant)ThingMaker.MakeThing(def);
                    plant.Growth = 1f;
                    GenSpawn.Spawn(plant, c, map);
                    placed++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: mushrooms where the frost lay", e);
                }
            }
            OwnHappenings.Glow(map, grave.Position, new Color(0.7f, 0.85f, 0.6f), 6, 1.5f, 0.35f);
            var seer = OwnHappenings.Seer(map, grave.Position, 25f);
            var morel = OwnHappenings.God("Morel");
            if (seer != null) OwnHappenings.SayWith(seer, Pool, morel, new[] { dead.Named("DEAD") }, "melt");
            OwnHappenings.Line(map, (placed > 0 ? "CC_GodHush_Melt_Text" : "CC_GodHush_Melt_TextBare").Translate(dead.Named("DEAD")).Resolve(), new LookTargets(grave));
            // In both chapters: the cold kept the dead whole, and the soft mother took them back.
            var hush = God;
            if (hush != null) OwnHappenings.Saw(hush, map, grave.Position, GodActKind.Mark, seer, "CC_GodHush_Saw_Melt", dead.Named("DEAD"));
            if (morel != null && placed > 0) OwnHappenings.Saw(morel, map, grave.Position, GodActKind.Omen, seer, "CC_GodHush_Saw_MeltRing", dead.Named("DEAD"));
            return true;
        }
    }

    /// <summary>Dev mode: Hush's happenings now (Debug actions, "Rimfolk", "Gods: Hush's ... now").</summary>
    public static class HushDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: Hush's word in the frost now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWord()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var w = HushWays.WordNow(map, out _).word;
            if (w == OwnHappeningsMath.FrostWord.None) w = OwnHappeningsMath.FrostWord.Bread;
            b.hush.wordMap = null;
            Say(HushWays.Write(b, HushWays.God, map, w) ? "the word " + w + " is in the frost; the first one up walks out to it." : "nowhere to write it.");
        }

        [DebugAction("Rimfolk", "Gods: Hush's vigil now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVigil()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var frail = HushWays.Frail(map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.InBed());
            var keeper = HushWays.Keeper(map, frail);
            if (frail == null || keeper == null)
            {
                Say("nobody of the colony abed, or nobody free to sit with them.");
                return;
            }
            b.hush.EndVigil();
            HushWays.BeginVigil(b, map, frail, keeper);
            Say(keeper.LabelShort + " sits up with " + frail.LabelShort + (OwnHappenings.Condition(map, "ColdSnap") == null ? " (no cold snap here: the vigil ends at once; start one with the game's dev tools)." : "; kept till five o'clock, the cold snap breaks."));
        }

        [DebugAction("Rimfolk", "Gods: Hush's walk round the edge now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWalk()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            b.hush.walkCardDue = false;
            HushWays.BeginWalk(b, map);
            Say("the white figure walks round the colony at " + b.hush.walkRadius + " cells (by night only: it stops at five o'clock).");
        }

        [DebugAction("Rimfolk", "Gods: Hush's frost on the newest grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrave()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var grave = HushWays.NewestGrave(map, out _);
            if (grave == null)
            {
                Say("no grave of the colony's dead here.");
                return;
            }
            b.hush.grave = null;
            Say(HushWays.Lay(b, HushWays.God, map, grave) ? "frost on the grave; it melts into a ring of mushrooms in two to four days (\"Gods: Hush's frost melts now\")." : "it couldn't.");
        }

        [DebugAction("Rimfolk", "Gods: Hush's frost melts now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMelt()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            Say(b.hush.grave != null && HushWays.Melt(b, map) ? "melted into a ring of mushrooms." : "no frost on a grave here.");
        }
    }
}
