using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>Helios's happenings under way (OwnHappeningsBook.helios): the eclipse last seen, the first fruit in the sun, the sun's return.</summary>
    public class HeliosState : IExposable
    {
        /// <summary>The eclipse last seen (its map, its day) and whether he has looked back since.</summary>
        public Map eclipseMap;
        public int eclipseDay = -1;
        public bool lookedBack;

        /// <summary>His festival kept today (its map and day), and the first fruit eaten in the sun: where, until when, who has eaten ("id|id").</summary>
        public Map fruitMap;
        public int fruitDay = -1, fruitUntil = -1;
        public IntVec3 fruitAt = IntVec3.Invalid;
        public bool fruitBegun;
        public int fruitEaten;
        public string fruitAte, fruitSent;

        /// <summary>The sun's return after the longest night: where they stand facing it, until when, who saw it ("id|id").</summary>
        public Map dawnMap;
        public IntVec3 dawnAt = IntVec3.Invalid;
        public int dawnUntil = -1;
        public string dawnSaw, dawnSent;

        public void ExposeData()
        {
            Scribe_References.Look(ref eclipseMap, "eclipseMap");
            Scribe_Values.Look(ref eclipseDay, "eclipseDay", -1);
            Scribe_Values.Look(ref lookedBack, "lookedBack");
            Scribe_References.Look(ref fruitMap, "fruitMap");
            Scribe_Values.Look(ref fruitDay, "fruitDay", -1);
            Scribe_Values.Look(ref fruitUntil, "fruitUntil", -1);
            Scribe_Values.Look(ref fruitAt, "fruitAt", IntVec3.Invalid);
            Scribe_Values.Look(ref fruitBegun, "fruitBegun");
            Scribe_Values.Look(ref fruitEaten, "fruitEaten");
            Scribe_Values.Look(ref fruitAte, "fruitAte");
            Scribe_Values.Look(ref fruitSent, "fruitSent");
            Scribe_References.Look(ref dawnMap, "dawnMap");
            Scribe_Values.Look(ref dawnAt, "dawnAt", IntVec3.Invalid);
            Scribe_Values.Look(ref dawnUntil, "dawnUntil", -1);
            Scribe_Values.Look(ref dawnSaw, "dawnSaw");
            Scribe_Values.Look(ref dawnSent, "dawnSent");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (fruitMap == null) fruitUntil = -1;
                if (dawnMap == null) dawnUntil = -1;
            }
        }

        internal static bool Has(string list, Pawn p) => p != null && !list.NullOrEmpty() && ("|" + list + "|").Contains("|" + p.thingIDNumber + "|");

        internal static string With(string list, Pawn p) => list.NullOrEmpty() ? p.thingIDNumber.ToString() : list + "|" + p.thingIDNumber;

        internal static int Count(string list) => list.NullOrEmpty() ? 0 : list.Split('|').Length;
    }

    /// <summary>
    /// Helios, the Giver (docs/gods-next-proposals.md, C 10): the sun, who gives light, warmth and harvest to everyone alike,
    /// never looks back, and takes nothing (his way with a gift is to leave it in the light). His own happenings:
    /// <list type="bullet">
    /// <item>He looks back: the day after an eclipse (Selene stepping between him and the world) is the hottest of the season;
    /// at its noon his long noon comes in its hard form (eight degrees warmer, the light held white), and everyone out of doors
    /// stops and shades their eyes, and someone says so.</item>
    /// <item>The first fruit in the sun: on his festival kept, at noon, the colony's people each take a piece of fresh fruit
    /// from the stores and walk out into the open sun by the fields, eat it there together, and remember it.</item>
    /// <item>The sun comes back: the morning after the longest night (and the more after Hush's walk round the edge), the
    /// colony's people who are up walk out to the east side of the colony, stand facing the sunrise, and it lies on them gold
    /// a while.</item>
    /// </list>
    /// </summary>
    public static class OwnHelios
    {
        public const string Key = "Helios";
        public const string Pool = "Rimfolk_Pool_GodHelios_Say";
        public const string FruitThought = "Rimfolk_Thought_Helios_FirstFruit";
        public const string DawnThought = "Rimfolk_Thought_Helios_SunBack";

        public static God God => OwnHappenings.God(Key);

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color Gold = new Color(1f, 0.86f, 0.45f);

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = b.helios;
            int hour = GenLocalDate.HourOfDay(map);
            int today = GenDate.DaysPassed;
            if (OwnHappenings.Condition(map, "Eclipse") != null)
            {
                s.eclipseMap = map;
                s.eclipseDay = today;
                s.lookedBack = false;
            }
            else if (s.eclipseMap == map && !s.lookedBack && OwnHappeningsMath.LookBackDue(s.eclipseDay, today) && hour >= OwnHappeningsMath.LookBackFrom
                     && hour < OwnHappeningsMath.LookBackTo && !OwnHappenings.Raining(map) && OwnHappenings.Acts(g, map))
                LookBack(b, g, map);
            if (s.fruitMap == map && !s.fruitBegun && s.fruitDay == today && hour >= OwnHappeningsMath.FruitFrom && hour < OwnHappeningsMath.FruitTo
                && map.skyManager.CurSkyGlow > 0.6f && !OwnHappenings.Raining(map) && !MapFacts.ThreatOn(map))
                BeginFruit(b, map);
            if (s.fruitMap != null && !s.fruitBegun && s.fruitDay < today) s.fruitMap = null;
            if (hour >= OwnHappeningsMath.SunBackFrom && hour < OwnHappeningsMath.SunBackTo && s.dawnUntil < 0 && !MapFacts.ThreatOn(map)) Dawn(b, g, map, now);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.helios;
            if (s.fruitBegun && s.fruitMap != null) OwnHappenings.Safe("the first fruit", () => FruitStep(b, now));
            if (s.dawnMap != null && s.dawnUntil >= 0) OwnHappenings.Safe("the sun's return", () => DawnStep(b, now));
        }

        // ================= He looks back =================

        /// <summary>The day after the eclipse: his hard long noon, everyone out of doors shading their eyes, a card.</summary>
        public static bool LookBack(OwnHappeningsBook b, God g, Map map)
        {
            var s = b.helios;
            s.lookedBack = true;
            if (g != null && map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() == null && GodPresenceOwn.DefOf(g) != null)
            {
                string known = Gods.CardName(g, map, "Eclipse");
                var look = GodsWiring.LookOf(g);
                GameCondition_GodPresence.Begin(map, PresenceKind.Wrath, g.colour, look?.still ?? 0.5f, known ?? GodLore.LabelFor(Gods.Name(g)), known == null, GodPresenceOwn.DefOf(g), g.Key);
                GodPresenceOwn.Forget(map);
            }
            var outside = map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map) && OwnHappenings.Ours(p)
                                                                       && (p.CurJob == null || p.CurJob.def.casualInterruptible || WorshipJobs.Free(p, false))).Take(8).ToList();
            foreach (var p in outside)
            {
                OwnHappenings.Stand(p, p.Position + IntVec3.South * 5, Rand.Range(240, 420));
                foreach (var c in GenRadial.RadialCellsAround(p.Position, 3f, true).Where(c => c.InBounds(map) && !c.Roofed(map)).InRandomOrder().Take(4))
                    FleckMaker.ThrowHeatGlow(c, map, Rand.Range(0.8f, 1.3f));
            }
            var speaker = outside.RandomElementWithFallback();
            if (speaker != null) OwnHappenings.SayFrom(speaker, Pool, g, "lookback");
            string text = "CC_GodHelios_LookBack_Text".Translate(Homes.Name(map).Named("HOME")).Resolve();
            var at = speaker?.Position ?? OwnHappenings.Middle(map);
            OwnHappenings.CardOf(g, map, "Eclipse", "CC_GodHelios_LookBack_Title", "CC_GodHelios_LookBack_TitleNamed", text, new LookTargets(new TargetInfo(at, map)), LetterDefOf.NeutralEvent);
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Act, speaker, "CC_GodHelios_Saw_LookBack");
            OwnHappenings.Came(OwnHappenings.Book, OwnHappening.HeliosLookBack, OwnHappenings.Now);
            return true;
        }

        // ================= The first fruit in the sun =================

        /// <summary>His festival kept at a home (OwnHappenings.FestivalKept): the first fruit is eaten in the sun at noon.</summary>
        internal static void Festival(OwnHappeningsBook b, Map map)
        {
            var s = b.helios;
            s.fruitMap = map;
            s.fruitDay = GenDate.DaysPassed;
            s.fruitBegun = false;
        }

        /// <summary>Fresh fruit in the colony's stores (berries first), or null.</summary>
        public static Thing Fruit(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.ingestible != null && t.def.ingestible.HumanEdible && (t.def.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0
                            && (t.def.ingestible.preferability == FoodPreferability.RawTasty || t.def.ingestible.preferability == FoodPreferability.RawBad) && t.IsInAnyStorage())
                .OrderByDescending(t => t.def.ingestible.preferability == FoodPreferability.RawTasty).ThenByDescending(t => t.stackCount).FirstOrDefault();

        /// <summary>The open ground by the colony's fields under the sky (the largest growing zone's middle), else open ground at the edge.</summary>
        private static IntVec3 Field(Map map)
        {
            var zone = map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.CellCount).FirstOrDefault();
            if (zone != null)
            {
                var mid = zone.Cells.OrderBy(c => c.DistanceToSquared(new IntVec3((int)zone.Cells.Average(x => x.x), 0, (int)zone.Cells.Average(x => x.z)))).First();
                var spot = CellFinder.RandomClosewalkCellNear(mid, map, 3, c => c.Standable(map) && !c.Roofed(map));
                if (spot.IsValid) return spot;
            }
            return GodOmens.Open(map);
        }

        public static bool BeginFruit(OwnHappeningsBook b, Map map)
        {
            var s = b.helios;
            var spot = Field(map);
            if (!spot.IsValid) return false;
            s.fruitMap = map;
            s.fruitBegun = true;
            s.fruitAt = spot;
            s.fruitUntil = Now + GenDate.TicksPerHour * 3;
            s.fruitAte = null;
            s.fruitSent = null;
            s.fruitEaten = 0;
            var fruit = Fruit(map);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Free).Take(OwnHappeningsMath.FruitMost).ToList())
            {
                var to = CellFinder.RandomClosewalkCellNear(spot, map, 4, c => c.Standable(map) && !c.Roofed(map));
                if (!to.IsValid || !p.CanReach(to, PathEndMode.OnCell, Danger.Some)) continue;
                s.fruitSent = HeliosState.With(s.fruitSent, p);
                try
                {
                    // A piece from the stores in their hand first, where there is any; then out into the sun.
                    if (fruit != null && fruit.Spawned && fruit.stackCount > 0 && p.CanReserveAndReach(fruit, PathEndMode.ClosestTouch, Danger.Some))
                    {
                        var take = JobMaker.MakeJob(JobDefOf.TakeInventory, fruit);
                        take.count = 1;
                        p.jobs.StartJob(take, JobCondition.InterruptForced, null, true);
                        var go = JobMaker.MakeJob(JobDefOf.Goto, to);
                        go.expiryInterval = GenDate.TicksPerHour * 2;
                        p.jobs.jobQueue.EnqueueLast(go);
                    }
                    else OwnHappenings.GoTo(p, to, GenDate.TicksPerHour * 2);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: out to the fields for the first fruit", e);
                }
            }
            OwnHappenings.Came(b, OwnHappening.HeliosFruit, OwnHappenings.Now);
            b.Due(Now + 250);
            return true;
        }

        private static void FruitStep(OwnHappeningsBook b, int now)
        {
            var s = b.helios;
            var map = s.fruitMap;
            if (map == null || !map.IsPlayerHome)
            {
                s.fruitMap = null;
                s.fruitBegun = false;
                return;
            }
            var g = God;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && HeliosState.Has(s.fruitSent, p) && p.Position.InHorDistOf(s.fruitAt, 6f) && !HeliosState.Has(s.fruitAte, p)).ToList())
            {
                if (p.CurJobDef == JobDefOf.Goto || p.CurJobDef == JobDefOf.TakeInventory) continue;
                bool first = s.fruitAte.NullOrEmpty();
                s.fruitAte = HeliosState.With(s.fruitAte, p);
                OwnHappenings.Stand(p, s.fruitAt, 700);
                if (Eat(p, map)) s.fruitEaten++;
                OwnHappenings.Memory(p, FruitThought);
                OwnHappenings.Glow(map, p.Position, Gold, 4, 0.8f, 0.35f);
                if (first) OwnHappenings.SayFrom(p, Pool, g, "fruit");
            }
            if (now > s.fruitUntil)
            {
                int n = HeliosState.Count(s.fruitAte);
                if (n > 0)
                {
                    bool ate = s.fruitEaten > 0;
                    OwnHappenings.Line(map, (ate ? "CC_GodHelios_Fruit_Text" : "CC_GodHelios_Fruit_TextNone").Translate((ate ? s.fruitEaten : n).Named("COUNT"), Homes.Name(map).Named("HOME")).Resolve(),
                        new LookTargets(new TargetInfo(s.fruitAt, map)));
                    if (g != null && ate) Worship.Booked(g, map, s.fruitAt, "CC_GodHelios_Book_Fruit".Translate(s.fruitEaten.Named("COUNT")).Resolve());
                }
                s.fruitMap = null;
                s.fruitBegun = false;
                s.fruitAte = s.fruitSent = null;
                return;
            }
            b.Due(now + 250);
        }

        /// <summary>The fruit in their hand eaten there and then (the game's own eating sound and effect), else nothing in their hand.</summary>
        private static bool Eat(Pawn p, Map map)
        {
            var held = p.inventory?.innerContainer?.FirstOrDefault(t => t.def.ingestible != null && (t.def.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0
                                                                         && (t.def.ingestible.preferability == FoodPreferability.RawTasty || t.def.ingestible.preferability == FoodPreferability.RawBad));
            if (held == null) return false;
            try
            {
                var one = held.stackCount > 1 ? held.SplitOff(1) : held;
                if (one.holdingOwner != null) one.holdingOwner.Remove(one);
                var effect = one.def.ingestible.ingestEffect;
                var sound = one.def.ingestible.ingestSound;
                float food = one.Ingested(p, p.needs?.food?.NutritionWanted ?? 0f);
                if (p.needs?.food != null) p.needs.food.CurLevel += food;
                if (effect != null)
                {
                    var e = effect.Spawn();
                    e.Trigger(new TargetInfo(p), new TargetInfo(p));
                    e.Cleanup();
                }
                sound?.PlayOneShot(new TargetInfo(p.Position, map));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the first fruit eaten", e);
                return false;
            }
        }

        // ================= The sun comes back =================

        private static void Dawn(OwnHappeningsBook b, God g, Map map, int now)
        {
            var se = GenLocalDate.Season(map);
            bool winter = se == Season.Winter || se == Season.PermanentWinter;
            bool afterLongest = OwnHappeningsMath.LongestNight(winter, GenLocalDate.DayOfSeason(map) - 1);
            var t = OwnHappeningsMath.DawnTrigger(afterLongest, OwnHush.WalkedLastNight(map));
            if (map.skyManager.CurSkyGlow < 0.25f || !OwnHappenings.Calls(b, OwnHappening.HeliosDawn, t, g, map, now)) return;
            BeginDawn(b, map);
        }

        /// <summary>The east side of the colony under the sky, a little past its farthest building that way.</summary>
        private static IntVec3 East(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            for (int r = 8; r <= 40; r += 2)
            {
                var c = new IntVec3(mid.x + r, 0, mid.z).ClampInsideMap(map);
                if (c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetRoom(map)?.PsychologicallyOutdoors != false) return c;
            }
            return GodOmens.Open(map);
        }

        public static bool BeginDawn(OwnHappeningsBook b, Map map)
        {
            var s = b.helios;
            var spot = East(map);
            if (!spot.IsValid) return false;
            s.dawnMap = map;
            s.dawnAt = spot;
            s.dawnUntil = Now + GenDate.TicksPerHour * 2;
            s.dawnSaw = null;
            s.dawnSent = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Free).Take(OwnHappeningsMath.SunBackMost).ToList())
            {
                var to = CellFinder.RandomClosewalkCellNear(spot, map, 4, c => c.Standable(map) && !c.Roofed(map));
                if (to.IsValid && OwnHappenings.GoTo(p, to, GenDate.TicksPerHour * 2)) s.dawnSent = HeliosState.With(s.dawnSent, p);
            }
            OwnHappenings.Came(b, OwnHappening.HeliosDawn, OwnHappenings.Now);
            b.Due(Now + 250);
            return true;
        }

        private static void DawnStep(OwnHappeningsBook b, int now)
        {
            var s = b.helios;
            var map = s.dawnMap;
            if (map == null || !map.IsPlayerHome)
            {
                s.dawnMap = null;
                s.dawnUntil = -1;
                return;
            }
            var g = God;
            var east = s.dawnAt + IntVec3.East * 12;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && HeliosState.Has(s.dawnSent, p) && p.Position.InHorDistOf(s.dawnAt, 6f) && !HeliosState.Has(s.dawnSaw, p)).ToList())
            {
                if (p.CurJobDef == JobDefOf.Goto) continue;
                bool first = s.dawnSaw.NullOrEmpty();
                s.dawnSaw = HeliosState.With(s.dawnSaw, p);
                OwnHappenings.Stand(p, east, 600);
                OwnHappenings.Memory(p, DawnThought);
                OwnHappenings.Glow(map, p.Position, Gold, 6, 0.9f, 0.4f);
                if (first) OwnHappenings.SayFrom(p, Pool, g, "dawn", OwnHush.WalkedLastNight(map) ? "afterwalk" : null);
            }
            if (now > s.dawnUntil)
            {
                int n = HeliosState.Count(s.dawnSaw);
                if (n > 0)
                {
                    OwnHappenings.Line(map, "CC_GodHelios_Dawn_Text".Translate(n.Named("COUNT"), Homes.Name(map).Named("HOME")).Resolve(), new LookTargets(new TargetInfo(s.dawnAt, map)));
                    if (g != null) OwnHappenings.Saw(g, map, s.dawnAt, GodActKind.Blessing, null, "CC_GodHelios_Saw_Dawn", n.Named("COUNT"));
                }
                s.dawnMap = null;
                s.dawnUntil = -1;
                s.dawnSaw = s.dawnSent = null;
                return;
            }
            b.Due(now + 250);
        }
    }

}
