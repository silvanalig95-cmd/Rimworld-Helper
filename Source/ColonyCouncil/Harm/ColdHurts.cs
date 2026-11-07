using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 5: the weather and the cold (Harm/HarmFive.cs is the hub; each colonist's hour). "Out in the cold" is
    /// the same everywhere: outdoors under the sky, the air outside at -5 °C or colder, and colder than comfortable for
    /// them with what they wear (the game's own comfortable range, which warm clothes widen). What it gives is Rimfolk's
    /// own cold ailments: frostnip in bitter cold, chilblains otherwise.
    /// <list type="bullet">
    /// <item>Waiting at the gate in the cold (3.1.2): a traveller waiting at the gate once the hour has passed and someone
    /// isn't ready: every two such hours, one chance in 20. Answering the letter (go without them, or put it off) and warm
    /// clothes prevent it; the letter says so in a frost. With journeys, the everyday ailments and "Systems talk to each
    /// other".</item>
    /// <item>The night watch in the cold (4.2.2): a watcher on the walls (the night watch played out on the map) the same,
    /// half with two or more on watch; and a lone watcher at a look-out outside the walls (the edge of the map reachable
    /// without a closed door) with a hungry predator in reach draws it one watch in six: the game's own hunt, stopping once
    /// they're down. Warm clothes, a warm look-out, two on watch and walls prevent it; named under the watch's answer. With
    /// answers on the map, the everyday ailments and "Systems talk to each other".</item>
    /// <item>Weather lovers (3.11.3): one who stands in the rain (the quirk) out two hours of a day in it: a chest cold one
    /// day in 150 (a child a head cold), a third more than they'd have; one who loves the snow, three hours of a day in
    /// falling snow in bitter cold: frostnip one in 75. A coat or a hood halves it. With the quirks, the everyday ailments
    /// and "Systems talk to each other".</item>
    /// <item>A slip into cold water (4.9.2): an hour fishing (the game's own fishing, or Vanilla Fishing Expanded's) below
    /// freezing, one in 200, twice in a hard frost: soaked, with the game's own hypothermia at its shivering stage, which
    /// goes once they're warm; they make for the nearest fire. Not fishing in a frost prevents it.</item>
    /// </list>
    /// The ailments are told as an everyday ailment is; the slip is seen (the splash, a few words over them, the game's
    /// own hypothermia) with no message of its own.
    /// </summary>
    public static class ColdHurts
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next cold wait, cold watch, weather lover's day, fishing hour or lone watch's predator comes, past its rate (never past the cap).</summary>
        public static bool DebugGate, DebugWatch, DebugLover, DebugFish, DebugPredator;

        public static void Reset()
        {
            gate.Clear();
            watch.Clear();
            lovers.Clear();
            watchPredator.Clear();
        }

        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p)) return;
            Gate(p, now);
            Watch(p, now);
            Lover(p, now);
            Fishing(p, now);
        }

        // ================= Out in the cold =================

        public static bool Outdoors(Pawn p)
        {
            if (!p.Spawned || p.Map == null || p.Position.Roofed(p.Map)) return false;
            var room = p.GetRoom();
            return room == null || room.PsychologicallyOutdoors;
        }

        /// <summary>Out in the cold: outdoors, a hard frost, and colder than comfortable with what they wear.</summary>
        public static bool ColdOut(Pawn p) =>
            Outdoors(p) && HarmFiveMath.ColdOut(true, p.Map.mapTemperature.OutdoorTemp, p.AmbientTemperature, p.ComfortableTemperatureRange().min);

        /// <summary>Frostnip in bitter cold (by their own cold line), chilblains otherwise.</summary>
        private static HediffDef ColdAilment(Pawn p)
        {
            BodyFit.TempShift(p, out float cold, out _);
            return BodyMath.BitterCold(p.AmbientTemperature, cold) ? HarmFour.Def("Council_Ail_Frostnip") : HarmFour.Def("Council_Ail_Chilblains");
        }

        /// <summary>Rimfolk's cold ailment through the harm service, its line and its message naming where it came from.</summary>
        private static bool GiveCold(Pawn p, HarmKind kind, HediffDef def, string pool, string key)
        {
            if (def == null || p.health.hediffSet.HasHediff(def)) return false;
            var info = AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, kind, HurtClass.Mild);
            if (h == null) return false;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, pool, null, def.defName == "Council_Ail_Frostnip" ? "frostnip" : "chilblains");
            HarmFour.TellHurt(p, key, p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS"), (AilmentTracker.DoctorsNote(def) ?? "").Named("NOTE"));
            return true;
        }

        // ================= Waiting at the gate in the cold (3.1.2) =================

        /// <summary>A traveller's thingIDNumber: the plan they wait for, and its cold hours so far.</summary>
        private static readonly Dictionary<int, (int plan, int hours)> gate = new Dictionary<int, (int, int)>();

        /// <summary>The journey's travellers wait at the gate past the hour, someone not ready (the letter asked, or they waited an hour).</summary>
        public static bool WaitingPastTheHour(JourneyPlan plan) => plan != null && (plan.stage == JourneyStage.Asking || plan.stage == JourneyStage.Final && plan.waited > 0);

        private static void Gate(Pawn p, int now)
        {
            if (p.CurJobDef != JourneyDefOf.Rimfolk_JourneyWait) return;
            var plan = JourneyBook.PlanAt(p.Map);
            if (!WaitingPastTheHour(plan) || !Harm.SystemOn(HarmKinds.GateCold.system)) return;
            bool cold = ColdOut(p) || DebugGate;
            if (!cold) return;
            int hours = gate.TryGetValue(p.thingIDNumber, out var g) && g.plan == plan.id ? g.hours + 1 : 1;
            if (gate.Count > 50) gate.Clear();
            gate[p.thingIDNumber] = (plan.id, hours);
            bool debug = DebugGate;
            if (!debug && !HarmFiveMath.ColdRoll(hours)) return;
            DebugGate = false;
            if (debug ? !Harm.May(p, HurtClass.Mild) : !Harm.Roll(p, HarmKinds.GateCold, HarmFiveMath.GateCold(true, true), HurtClass.Mild)) return;
            GiveCold(p, HarmKinds.GateCold, ColdAilment(p), "Rimfolk_Pool_ColdWait", "CC_HarmFive_GateCold");
        }

        /// <summary>The journey's asking letter, in a hard frost at the gate: the risk of a long wait in it.</summary>
        public static string GateLetterLine(JourneyPlan plan)
        {
            if (plan?.map == null || !Harm.On || !Harm.SystemOn(HarmKinds.GateCold.system)) return "";
            if (plan.map.mapTemperature.OutdoorTemp > HarmFiveMath.HardFrost) return "";
            return "\n\n" + "CC_HarmFive_GateColdLetter".Translate().Resolve();
        }

        // ================= The night watch in the cold (4.2.2) =================

        /// <summary>A watcher's thingIDNumber: the watch (a play-out's id) and its cold hours so far.</summary>
        private static readonly Dictionary<int, (int play, int hours)> watch = new Dictionary<int, (int, int)>();
        /// <summary>The watches whose lone watcher has had their one look for a predator (play-out ids).</summary>
        private static readonly HashSet<int> watchPredator = new HashSet<int>();

        /// <summary>The night watch on the walls they keep now (a vigil kept on the move), and how many keep it.</summary>
        private static PlayOut WatchOf(Pawn p, out int watchers)
        {
            watchers = 0;
            var tracker = PlayOutTracker.Instance;
            if (tracker == null) return null;
            foreach (var play in tracker.plays)
            {
                if (play == null || play.kind != PlayKind.Vigil || !play.patrol || play.state != PlayState.Running || play.hands == null) continue;
                bool mine = false;
                int n = 0;
                foreach (var h in play.hands)
                {
                    if (h?.pawn == null || h.pawn.Dead || !h.pawn.Spawned || h.pawn.Map != play.map) continue;
                    n++;
                    if (h.pawn == p) mine = true;
                }
                if (!mine) continue;
                watchers = n;
                return play;
            }
            return null;
        }

        private static void Watch(Pawn p, int now)
        {
            var play = WatchOf(p, out int watchers);
            if (play == null || !Harm.SystemOn(HarmKinds.NightWatch.system)) return;
            // A lone watcher at a look-out outside the walls: once a watch, a hungry predator in reach may come.
            if ((watchers == 1 || DebugPredator) && !watchPredator.Contains(play.id) && !(p.pather?.Moving ?? false) && NightWalks.OutsideWalls(p.Map, p.Position))
            {
                var predator = NightWalks.PredatorFor(p);
                bool debug = DebugPredator && predator != null;
                if (predator != null)
                {
                    watchPredator.Add(play.id);
                    if (watchPredator.Count > 100) watchPredator.Clear();
                    DebugPredator = false;
                    float chance = HarmFiveMath.WatchPredator(watchers, true, true);
                    if ((debug ? Harm.May(p, HurtClass.Injury) : Harm.Roll(p, HarmKinds.NightWatch, chance, HurtClass.Injury)) && NightWalks.Hunt(predator, p, HarmKinds.NightWatch))
                        HarmFour.Say(p, "Rimfolk_Pool_WatchPredator");
                }
            }
            bool cold = ColdOut(p) || DebugWatch;
            if (!cold) return;
            int hours = watch.TryGetValue(p.thingIDNumber, out var w) && w.play == play.id ? w.hours + 1 : 1;
            if (watch.Count > 50) watch.Clear();
            watch[p.thingIDNumber] = (play.id, hours);
            bool dbg = DebugWatch;
            if (!dbg && !HarmFiveMath.ColdRoll(hours)) return;
            DebugWatch = false;
            if (dbg ? !Harm.May(p, HurtClass.Mild) : !Harm.Roll(p, HarmKinds.NightWatch, HarmFiveMath.WatchCold(true, watchers), HurtClass.Mild)) return;
            GiveCold(p, HarmKinds.NightWatch, ColdAilment(p), "Rimfolk_Pool_ColdWatch", "CC_HarmFive_WatchCold");
        }

        /// <summary>A night watch on the walls' answer, in a frost: what a long watch in it can bring.</summary>
        public static void DescribeWatch(ImpactSheet sheet, Map map)
        {
            if (!Harm.On || map == null || !Harm.SystemOn(HarmKinds.NightWatch.system)) return;
            if (map.mapTemperature.OutdoorTemp > HarmFiveMath.HardFrost + 5f) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_WatchCold".Translate().Resolve(), "CC_Impact_WatchColdBrief".Translate().Resolve(), "watchcold");
        }

        // ================= Weather lovers (3.11.3) =================

        private static QuirkDef rainLover, snowLover;
        private static bool lookedQuirks;

        private static void LookQuirks()
        {
            if (lookedQuirks) return;
            lookedQuirks = true;
            rainLover = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_RainCleanses");
            snowLover = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SnowLover");
        }

        /// <summary>A colonist's thingIDNumber: the day, its hours out in their weather, and whether its roll was made.</summary>
        private static readonly Dictionary<int, (int day, int hours, bool rolled)> lovers = new Dictionary<int, (int, int, bool)>();

        /// <summary>A coat (a shell over the body) for the rain, a hood for the snow.</summary>
        private static bool Covered(Pawn p, bool rain)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return false;
            for (int i = 0; i < worn.Count; i++)
            {
                var ap = worn[i]?.def?.apparel;
                if (ap?.layers == null) continue;
                if (rain && ap.layers.Contains(ApparelLayerDefOf.Shell) && ap.CoversBodyPartGroup(BodyPartGroupDefOf.Torso)) return true;
                if (!rain && ap.layers.Contains(ApparelLayerDefOf.Overhead) && (ap.CoversBodyPartGroup(BodyPartGroupDefOf.UpperHead) || ap.CoversBodyPartGroup(BodyPartGroupDefOf.FullHead))) return true;
            }
            return false;
        }

        private static void Lover(Pawn p, int now)
        {
            LookQuirks();
            bool rain = rainLover != null && MoreHabits.HasQuirk(p, rainLover);
            bool snow = !rain && snowLover != null && MoreHabits.HasQuirk(p, snowLover);
            if (!rain && !snow || !RestUtility.Awake(p) || !Outdoors(p)) return;
            var weather = p.Map.weatherManager;
            bool inIt = rain ? weather.RainRate > 0.1f && weather.SnowRate < 0.1f : weather.SnowRate > 0.1f && BitterHere(p);
            if (!inIt && !DebugLover) return;
            if (!Harm.SystemOn(HarmKinds.WeatherLovers.system)) return;
            int day = now / HarmMath.DayTicks;
            var d = lovers.TryGetValue(p.thingIDNumber, out var was) && was.day == day ? (day, was.hours + 1, was.rolled) : (day, 1, false);
            if (lovers.Count > 100) lovers.Clear();
            lovers[p.thingIDNumber] = d;
            bool debug = DebugLover;
            float chance = HarmFiveMath.WeatherLover(rain, d.Item2, Covered(p, rain));
            if (d.Item3 || !debug && chance <= 0f) return;
            lovers[p.thingIDNumber] = (day, d.Item2, true);
            DebugLover = false;
            var cls = rain && !HarmFour.Child(p) ? HurtClass.Injury : HurtClass.Mild;
            if (debug ? !Harm.May(p, cls) : !Harm.Roll(p, HarmKinds.WeatherLovers, chance, cls)) return;
            var def = !rain ? HarmFour.Def("Council_Ail_Frostnip") : HarmFour.Child(p) ? HarmFour.Def("Council_Ail_HeadCold") : HarmFour.Def("Council_Ail_ChestCold");
            if (def == null || p.health.hediffSet.HasHediff(def)) return;
            var info = AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.WeatherLovers, cls);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, "Rimfolk_Pool_WeatherLover", null, rain ? "rainLover" : "snowLover");
            HarmFour.TellHurt(p, rain ? "CC_HarmFive_RainLover" : "CC_HarmFive_SnowLover", p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS"), (AilmentTracker.DoctorsNote(def) ?? "").Named("NOTE"));
        }

        private static bool BitterHere(Pawn p)
        {
            BodyFit.TempShift(p, out float cold, out _);
            return BodyMath.BitterCold(p.AmbientTemperature, cold);
        }

        // ================= A slip into cold water (4.9.2) =================

        /// <summary>The game's own fishing (Odyssey) or Vanilla Fishing Expanded's.</summary>
        public static bool FishingLoaded => ModsConfig.OdysseyActive || ModsConfig.IsActive("VanillaExpanded.VCEF");

        /// <summary>Fishing now: the fishing work, or a job of fishing by its name (the game's, or the mod's).</summary>
        private static bool Fishing(Job job)
        {
            if (job?.def == null) return false;
            string work = job.workGiverDef?.workType?.defName;
            if (work != null && work.IndexOf("Fishing", StringComparison.Ordinal) >= 0) return true;
            if (job.def.defName.IndexOf("Fish", StringComparison.Ordinal) >= 0) return true;
            return job.def.driverClass != null && job.def.driverClass.Name.IndexOf("Fish", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Water beside them (or under them): where they would slip in.</summary>
        private static IntVec3 WaterBy(Pawn p)
        {
            var map = p.Map;
            foreach (var c in GenAdj.CellsAdjacent8Way(p).Concat(new[] { p.Position }))
                if (c.InBounds(map) && (c.GetTerrain(map)?.IsWater ?? false))
                    return c;
            return IntVec3.Invalid;
        }

        private static void Fishing(Pawn p, int now)
        {
            if (!Fishing(p.CurJob) || !Outdoors(p)) return;
            float outdoor = p.Map.mapTemperature.OutdoorTemp;
            bool debug = DebugFish;
            float chance = HarmFiveMath.ColdWater(outdoor);
            if (!debug && chance <= 0f || !Harm.SystemOn(HarmKinds.ColdWater.system)) return;
            var water = WaterBy(p);
            if (!water.IsValid) return;
            DebugFish = false;
            var hypo = HarmFour.Def("Hypothermia");
            if (hypo == null || (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.ColdWater, chance, HurtClass.Injury))) return;
            var h = Harm.Ail(p, hypo, Rand.Range(HarmFiveMath.SoakLow, HarmFiveMath.SoakHigh), HarmKinds.ColdWater, HurtClass.Injury);
            if (h == null) return;
            // The splash, the shout, and the dash for the nearest fire.
            try
            {
                FleckMaker.WaterSplash(water.ToVector3Shifted(), p.Map, 1.6f, 6f);
            }
            catch
            {
                // A splash is only for the eye.
            }
            HarmFour.Say(p, "Rimfolk_Pool_ColdWater");
            HarmFour.Mote(p, "CC_HarmFive_Mote_Splash");
            ToTheFire(p);
        }

        /// <summary>They make for the nearest fire or heater burning at home, if one is in reach; else they just stop fishing.</summary>
        internal static void ToTheFire(Pawn p)
        {
            try
            {
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                Thing fire = null;
                float best = 45f * 45f;
                foreach (var b in p.Map.listerBuildings.allBuildingsColonist)
                {
                    if (b?.def == null || b.TryGetComp<CompHeatPusher>() == null) continue;
                    var fuel = b.TryGetComp<CompRefuelable>();
                    var power = b.TryGetComp<CompPowerTrader>();
                    if (fuel != null && !fuel.HasFuel || power != null && !power.PowerOn) continue;
                    float d = b.Position.DistanceToSquared(p.Position);
                    if (d >= best || !p.CanReach(b, PathEndMode.Touch, Danger.Some)) continue;
                    best = d;
                    fire = b;
                }
                if (fire == null) return;
                var cell = fire.def.hasInteractionCell ? fire.InteractionCell : CellFinder.StandableCellNear(fire.Position, p.Map, 2f);
                if (!cell.IsValid) return;
                var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Jog;
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm five: to the fire", e);
            }
        }
    }
}
