using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The ways to play's own storyteller parts (Defs/StorytellerDefs/Rimfolk_Storytellers.xml), only where the game's
    // comps can't say the way: a threat cycle with a shape (the year, the deeds, the long arc, the colony's wealth),
    // sickness with a shape that keeps the game's own rate over the year, named happenings by the season or the arc,
    // traders and visitors by the shape, and the omen of Rise and Fall's trouble. The world each reads is the target's:
    // its home's season, the peoples' memory of the colony, the days since the colony settled.

    /// <summary>The shape's inputs for a target: its home's season, the colony's deeds, the arc's phase and trouble.</summary>
    public static class StoryCompInputs
    {
        public static int Quarter(IIncidentTarget target) => StoryDirection.Quarter(target as Map);

        public static float Deeds(StoryShape shape) => shape == StoryShape.Deeds ? StoryDirection.Deeds : 0f;
    }

    // ================= A threat cycle with a shape =================

    /// <summary>
    /// The game's on-off cycle (StorytellerComp_OnOffCycle) whose share of its peak follows the way's shape: the Turning
    /// Year's seasons, What You Sow's deeds, Rise and Fall's arc; and, as the game's own does, the colony's threat points.
    /// The peak is set so that over a year or an arc it comes to about the game's own pace, never more.
    /// </summary>
    public class StorytellerCompProperties_WayCycle : StorytellerCompProperties
    {
        public float onDays;
        public float offDays;
        public float minSpacingDays;
        public FloatRange numIncidentsRange = FloatRange.Zero;
        public IncidentDef incident;
        public IncidentCategoryDef category;
        public float forceRaidEnemyBeforeDaysPassed;
        public StoryShape shape = StoryShape.None;
        /// <summary>Crossroads: raiders drawn by the colony's wealth, as a share by its threat points.</summary>
        public SimpleCurve acceptPercentFactorPerThreatPointsCurve;

        public IncidentCategoryDef Category => incident != null ? incident.category : category;

        public StorytellerCompProperties_WayCycle()
        {
            compClass = typeof(StorytellerComp_WayCycle);
        }

        public override IEnumerable<string> ConfigErrors(StorytellerDef parentDef)
        {
            foreach (var e in base.ConfigErrors(parentDef)) yield return e;
            if (incident != null && category != null) yield return "incident and category should not both be defined";
            if (incident == null && category == null) yield return "a way's cycle needs an incident or a category";
            if (onDays <= 0f) yield return "onDays must be above zero";
            if (numIncidentsRange.TrueMax <= 0f) yield return "numIncidentsRange not configured";
            if (minSpacingDays * numIncidentsRange.TrueMax > onDays * 0.9f) yield return "minSpacingDays too high compared to max number of incidents";
        }
    }

    public class StorytellerComp_WayCycle : StorytellerComp
    {
        private StorytellerCompProperties_WayCycle Props => (StorytellerCompProperties_WayCycle)props;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            // Never below a quarter of the peak: the way leans the threats about, it never shuts them out.
            float accept = Math.Max(StoryWaysMath.Floor, StoryWaysMath.ThreatAccept(Props.shape, StoryCompInputs.Quarter(target), StoryCompInputs.Deeds(Props.shape),
                StoryDirection.Phase, StoryDirection.Trouble));
            if (Props.acceptPercentFactorPerThreatPointsCurve != null)
                accept *= Props.acceptPercentFactorPerThreatPointsCurve.Evaluate(StorytellerUtility.DefaultThreatPointsNow(target));
            accept = Math.Max(0f, Math.Min(1f, accept));
            if (accept <= 0f) yield break;
            int count = IncidentCycleUtility.IncidentCountThisInterval(target, Find.Storyteller.storytellerComps.IndexOf(this), Props.minDaysPassed, Props.onDays,
                Props.offDays, Props.minSpacingDays, Props.numIncidentsRange.min, Props.numIncidentsRange.max, accept);
            for (int i = 0; i < count; i++)
            {
                var fi = Generate(target);
                if (fi != null) yield return fi;
            }
        }

        private FiringIncident Generate(IIncidentTarget target)
        {
            var cat = Props.Category;
            if (cat == null) return null;
            var parms = GenerateParms(cat, target);
            IncidentDef def;
            if (GenDate.DaysPassedSinceSettle < Props.forceRaidEnemyBeforeDaysPassed)
            {
                if (!IncidentDefOf.RaidEnemy.Worker.CanFireNow(parms)) return null;
                def = IncidentDefOf.RaidEnemy;
            }
            else if (Props.incident != null)
            {
                if (!Props.incident.Worker.CanFireNow(parms)) return null;
                def = Props.incident;
            }
            else if (!UsableIncidentsInCategory(cat, parms).TryRandomElementByWeight(d => IncidentChanceFinal(d, target), out def))
                return null;
            return new FiringIncident(def, this, parms);
        }

        public override string ToString() => base.ToString() + " (" + (Props.incident?.defName ?? Props.category?.defName) + ", " + Props.shape + ")";
    }

    // ================= Sickness with a shape =================

    /// <summary>
    /// The game's sickness (StorytellerComp_Disease, the biome's interval and the difficulty's), its chance moved about by
    /// the shape so that over a year or an arc it comes about as often as the game's own: the Turning Year's sickness with
    /// the thaw, Rise and Fall's plague in its trouble. Rimfolk's illness budget reads it as the game's own sickness.
    /// </summary>
    public class StorytellerCompProperties_WayDisease : StorytellerCompProperties_Disease
    {
        public StoryShape shape = StoryShape.None;

        public StorytellerCompProperties_WayDisease()
        {
            compClass = typeof(StorytellerComp_WayDisease);
        }
    }

    public class StorytellerComp_WayDisease : StorytellerComp
    {
        /// <summary>As the game's own: a caravan is four times less often sick than a home.</summary>
        private const float CaravanFactor = 4f;

        private StorytellerCompProperties_WayDisease Props => (StorytellerCompProperties_WayDisease)props;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!DebugSettings.enableRandomDiseases || target is World || Find.Storyteller.difficulty.diseaseIntervalFactor >= 100f) yield break;
            var biome = target is Map map ? map.Biome : Find.WorldGrid[target.Tile]?.PrimaryBiome;
            if (biome == null) yield break;
            float mtb = biome.diseaseMtbDays * Find.Storyteller.difficulty.diseaseIntervalFactor;
            if (target is Caravan) mtb *= CaravanFactor;
            float factor = Math.Max(StoryWaysMath.Floor, StoryWaysMath.DiseaseFactor(Props.shape, StoryCompInputs.Quarter(target), StoryDirection.Phase, StoryDirection.Trouble));
            if (!Rand.MTBEventOccurs(mtb / factor, GenDate.TicksPerDay, 1000f)) yield break;
            var parms = GenerateParms(Props.category, target);
            if (UsableIncidentsInCategory(Props.category, parms).TryRandomElementByWeight(d => biome.CommonalityOfDisease(d), out var def))
                yield return new FiringIncident(def, this, parms);
        }

        public override string ToString() => base.ToString() + " " + Props.category + " (" + Props.shape + ")";
    }

    // ================= Named happenings by the season or the arc =================

    /// <summary>One of a way's named happenings: its weight, and when it may come (seasons, the arc's phases, the arc's troubles).</summary>
    public class WayPick
    {
        public IncidentDef incident;
        public float weight = 1f;
        /// <summary>Only in these seasons (none: any).</summary>
        public List<Season> seasons;
        /// <summary>Only in these phases of the long arc (none: any).</summary>
        public List<ArcPhase> phases;
        /// <summary>Only when the arc's trouble is one of these (none: any).</summary>
        public List<StoryTrouble> troubles;

        public bool Fits(int quarter, ArcPhase phase, StoryTrouble trouble)
        {
            if (!seasons.NullOrEmpty() && !seasons.Any(s => QuarterOf(s) == quarter)) return false;
            if (!phases.NullOrEmpty() && !phases.Contains(phase)) return false;
            if (!troubles.NullOrEmpty() && !troubles.Contains(trouble)) return false;
            return true;
        }

        private static int QuarterOf(Season s) =>
            s == Season.Spring ? StoryWaysMath.Spring : s == Season.Summer || s == Season.PermanentSummer ? StoryWaysMath.Summer
            : s == Season.Fall ? StoryWaysMath.Autumn : StoryWaysMath.Winter;
    }

    /// <summary>
    /// A way's named happenings (Old Powers' strange ones, Lean Years' hardships, the Turning Year's weather by its season,
    /// Rise and Fall's trouble): about every <see cref="mtbDays"/> days one of those that can come now does, by weight, the
    /// one that came last much less likely (StoryWaysMath.PickRecency). Each is the game's own event with its own letter.
    /// </summary>
    public class StorytellerCompProperties_WayPicks : StorytellerCompProperties
    {
        public float mtbDays = 10f;
        public List<WayPick> picks = new List<WayPick>();

        public StorytellerCompProperties_WayPicks()
        {
            compClass = typeof(StorytellerComp_WayPicks);
        }

        public override IEnumerable<string> ConfigErrors(StorytellerDef parentDef)
        {
            foreach (var e in base.ConfigErrors(parentDef)) yield return e;
            if (mtbDays <= 0f) yield return "mtbDays must be above zero";
            if (picks.NullOrEmpty()) yield return "a way's picks need at least one happening";
        }
    }

    public class StorytellerComp_WayPicks : StorytellerComp
    {
        private StorytellerCompProperties_WayPicks Props => (StorytellerCompProperties_WayPicks)props;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!Rand.MTBEventOccurs(Props.mtbDays, GenDate.TicksPerDay, 1000f)) yield break;
            int quarter = StoryCompInputs.Quarter(target);
            var phase = StoryDirection.Phase;
            var trouble = StoryDirection.Trouble;
            int now = Find.TickManager.TicksGame;
            var may = new List<(IncidentDef def, IncidentParms parms, float w)>();
            foreach (var p in Props.picks)
            {
                var def = p?.incident;
                if (def?.Worker == null || p.weight <= 0f || !p.Fits(quarter, phase, trouble) || !def.TargetAllowed(target) || !AnomalyAllowed(def)) continue;
                try
                {
                    var parms = GenerateParms(def.category, target);
                    if (!def.Worker.CanFireNow(parms)) continue;
                    float since = target.StoryState.lastFireTicks.TryGetValue(def, out int t) ? (now - t) / (float)GenDate.TicksPerDay : float.NaN;
                    may.Add((def, parms, p.weight * StoryWaysMath.PickRecency(since)));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ways to play: a pick " + def.defName, e);
                }
            }
            if (may.Count == 0) yield break;
            var pick = may.RandomElementByWeight(x => x.w);
            yield return new FiringIncident(pick.def, this, pick.parms);
        }

        /// <summary>Anomaly's happenings only in a game with its content on, as the game's own choice keeps them.</summary>
        private static bool AnomalyAllowed(IncidentDef def)
        {
            if (!ModsConfig.AnomalyActive) return true;
            var pack = def.modContentPack;
            if (pack == null || !pack.IsOfficialMod || pack.PackageId == null || !pack.PackageId.ToLowerInvariant().Contains("anomaly")) return true;
            return Find.Storyteller?.difficulty?.AnomalyPlaystyleDef?.enableAnomalyContent ?? true;
        }

        public override string ToString() => base.ToString() + " (" + Props.picks.Count + " picks every " + Props.mtbDays + " days)";
    }

    // ================= Traders, visitors and friends by the shape =================

    /// <summary>
    /// The game's faction interaction (StorytellerComp_FactionInteraction: traders, visitors, friends sent in a fight) whose
    /// share of its peak follows the shape: the Turning Year's autumn trade, What You Sow's deeds, Rise and Fall's arc.
    /// </summary>
    public class StorytellerCompProperties_WayVisits : StorytellerCompProperties
    {
        public IncidentDef incident;
        public float baseIncidentsPerYear;
        public float minSpacingDays;
        public StoryDanger minDanger;
        public bool fullAlliesOnly;
        public StoryShape shape = StoryShape.None;
        /// <summary>Which of the shape's tables: traders, visitors or allies (StoryWaysMath's keys).</summary>
        public string lever = StoryWaysMath.Visitors;

        public StorytellerCompProperties_WayVisits()
        {
            compClass = typeof(StorytellerComp_WayVisits);
        }

        public override IEnumerable<string> ConfigErrors(StorytellerDef parentDef)
        {
            foreach (var e in base.ConfigErrors(parentDef)) yield return e;
            if (incident == null) yield return "a way's visits need an incident";
            if (baseIncidentsPerYear <= 0f) yield return "baseIncidentsPerYear must be above zero";
            if (lever != StoryWaysMath.Traders && lever != StoryWaysMath.Visitors && lever != StoryWaysMath.Allies) yield return "lever is traders, visitors or allies";
        }
    }

    public class StorytellerComp_WayVisits : StorytellerComp
    {
        private StorytellerCompProperties_WayVisits Props => (StorytellerCompProperties_WayVisits)props;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            var map = target as Map;
            if (Props.minDanger != StoryDanger.None && (map == null || map.dangerWatcher.DangerRating < Props.minDanger)) yield break;
            float accept = StorytellerUtility.AllyIncidentFraction(Props.fullAlliesOnly)
                           * Math.Max(StoryWaysMath.Floor, StoryWaysMath.VisitAccept(Props.shape, Props.lever, StoryCompInputs.Quarter(target), StoryCompInputs.Deeds(Props.shape), StoryDirection.Phase));
            accept = Math.Max(0f, Math.Min(1f, accept));
            if (accept <= 0f) yield break;
            int count = IncidentCycleUtility.IncidentCountThisInterval(target, Find.Storyteller.storytellerComps.IndexOf(this), Props.minDaysPassed, 60f, 0f,
                Props.minSpacingDays, Props.baseIncidentsPerYear, Props.baseIncidentsPerYear, accept);
            for (int i = 0; i < count; i++)
            {
                var parms = GenerateParms(Props.incident.category, target);
                if (Props.incident.Worker.CanFireNow(parms)) yield return new FiringIncident(Props.incident, this, parms);
            }
        }

        public override string ToString() => base.ToString() + " (" + Props.incident?.defName + ", " + Props.shape + ")";
    }

    // ================= Rise and Fall: the omen of the trouble =================

    /// <summary>
    /// Rise and Fall's foreshadowing: in the omens of each arc, a few days in (set by the world, StoryWaysMath.OmenDay),
    /// the region's word of the coming trouble comes as one of Rimfolk's world situations of its kind (a plague among the
    /// towns, a warlord rising, a famine, a hard winter, a strange sky), once an arc, when one may start (a slot free, no
    /// fight on, no question waiting). The trouble that follows is the arc's own (its raids, its sickness, its hardships).
    /// </summary>
    public class StorytellerCompProperties_WayOmen : StorytellerCompProperties
    {
        /// <summary>About every this many days, once its day has come, until the word is out.</summary>
        public float mtbDays = 2f;

        public StorytellerCompProperties_WayOmen()
        {
            compClass = typeof(StorytellerComp_WayOmen);
        }
    }

    public class StorytellerComp_WayOmen : StorytellerComp
    {
        private StorytellerCompProperties_WayOmen Props => (StorytellerCompProperties_WayOmen)props;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            // Once for the colony, on its seat's interval; placed near whichever home the region's word would come to.
            if (target is Map map && map == (Homes.Seat ?? Find.AnyPlayerHomeMap)) Try(WorldSituations.PlaceNear() ?? map, false, Props.mtbDays);
            yield break;
        }

        /// <summary>The omen's word now, if its day has come and none of the trouble's kind has begun this arc. With <paramref name="force"/> (dev mode), past the day and the chance.</summary>
        public static string Try(Map map, bool force, float mtbDays = 2f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || map == null) return "no world situations.";
            if (!(ColonyCouncilMod.Settings?.worldSituations ?? true)) return "world situations are off.";
            float days = StoryDirection.Days;
            int cycle = StoryWaysMath.ArcCycle(days);
            var phase = StoryWaysMath.PhaseAt(days);
            float into = StoryWaysMath.ArcDay(days) - StoryWaysMath.PhaseStart(ArcPhase.Omen);
            if (!force && (phase != ArcPhase.Omen || into < StoryWaysMath.OmenDay(cycle, StoryDirection.Seed))) return "not in the omens' days yet.";
            var kinds = StoryWaysMath.TroubleKinds(StoryWaysMath.TroubleOf(cycle, StoryDirection.Seed));
            // The tick the omens of this arc began: the colony's settling, then the arc's days.
            int settled = Find.TickManager.TicksGame - Find.TickManager.TicksSinceSettle;
            int since = settled + (int)((cycle * StoryWaysMath.ArcDays + StoryWaysMath.PhaseStart(ArcPhase.Omen)) * GenDate.TicksPerDay);
            if (!force && Begun(ws, kinds, since)) return "the trouble's word is out already.";
            if (!ws.SlotFree(map)) return "no world situation may start now (two running, a fight, a question waiting or the letters' gap).";
            if (!force && !Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, 1000f)) return null;
            // A hard situation of the trouble's kinds; a strange sky or a comet is omen enough for a darkness in the sky.
            bool sky = StoryWaysMath.TroubleOf(cycle, StoryDirection.Seed) == StoryTrouble.Void;
            var s = ws.TryStart(map, d => d != null && (d.bad || sky) && kinds.Contains(d.kind), force);
            return s == null ? "none of the trouble's kind (" + string.Join(", ", kinds) + ") could start." : null;
        }

        /// <summary>A world situation of the trouble's kinds began since the omens did (running or over).</summary>
        private static bool Begun(WorldSituations ws, string[] kinds, int since)
        {
            if (ws.active.Any(s => s?.def != null && s.startTick >= since && kinds.Contains(s.def.kind))) return true;
            foreach (var r in ws.history)
            {
                if (r == null || r.startTick < since) continue;
                var d = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName);
                if (d != null && kinds.Contains(d.kind)) return true;
            }
            return false;
        }
    }
}
