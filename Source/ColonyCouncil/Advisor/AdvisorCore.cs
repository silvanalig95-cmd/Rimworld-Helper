using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public enum AdvisorCategory
    {
        Shelter,
        Food,
        Temperature,
        Defense,
        Health,
        Power,
        Comfort,
        Work,
        Progress,
        Idea
    }

    public enum AdviceLevel
    {
        Idea,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>One kind of check the advisor runs. Tips explain why it matters.</summary>
    public class AdvisorSignalDef : Def
    {
        public Type workerClass;
        public AdvisorCategory category;
        public float importance = 1f;
        [MustTranslate] public List<string> tips = new List<string>();
        public RoomTemplateDef template;

        [Unsaved] private AdvisorSignalWorker workerInt;

        public AdvisorSignalWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (AdvisorSignalWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        /// <summary>A tip that stays the same for a few days, then rotates.</summary>
        public string TipForToday
        {
            get
            {
                if (tips == null || tips.Count == 0) return null;
                int day = (Find.TickManager?.TicksGame ?? 0) / (GenDate.TicksPerDay * 3);
                return tips[(day + shortHash) % tips.Count];
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (workerClass == null || !typeof(AdvisorSignalWorker).IsAssignableFrom(workerClass))
                yield return "workerClass must be an AdvisorSignalWorker";
        }
    }

    /// <summary>A single piece of advice, produced fresh each evaluation.</summary>
    public class AdvisorSignal
    {
        public AdvisorSignalDef def;
        /// <summary>How bad is it if ignored (0..1).</summary>
        public float severity;
        /// <summary>How soon does it bite (0..1).</summary>
        public float urgency = 1f;
        /// <summary>How sure are we (0..1). Defence estimates are rough.</summary>
        public float confidence = 1f;
        public string title;
        public string detail;
        public LookTargets look;
        public RoomTemplateDef template;
        public ThingDef suggestThing;
        public string key = "";

        public float Score => severity * (0.5f + 0.5f * urgency) * (0.6f + 0.4f * confidence) * (def?.importance ?? 1f);

        public AdviceLevel Level
        {
            get
            {
                float s = Score;
                if (def != null && def.category == AdvisorCategory.Idea) return AdviceLevel.Idea;
                if (s >= 0.7f) return AdviceLevel.Critical;
                if (s >= 0.45f) return AdviceLevel.High;
                if (s >= 0.22f) return AdviceLevel.Medium;
                if (s >= 0.08f) return AdviceLevel.Low;
                return AdviceLevel.Idea;
            }
        }

        public string Tip => def?.TipForToday;
    }

    public abstract class AdvisorSignalWorker
    {
        public AdvisorSignalDef def;

        public abstract IEnumerable<AdvisorSignal> Evaluate(Map map);

        protected AdvisorSignal Signal(float severity, string title, string detail, float urgency = 1f, float confidence = 1f)
        {
            return new AdvisorSignal
            {
                def = def,
                severity = UnityEngine.Mathf.Clamp01(severity),
                urgency = UnityEngine.Mathf.Clamp01(urgency),
                confidence = UnityEngine.Mathf.Clamp01(confidence),
                title = title,
                detail = detail,
                template = def.template
            };
        }

        protected static List<Pawn> Colonists(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
    }

    /// <summary>Caches advice for one map and refreshes it every in-game hour.</summary>
    public class CouncilMapComponent : MapComponent
    {
        private List<AdvisorSignal> signals = new List<AdvisorSignal>();
        private int lastEvalTick = -999999;
        /// <summary>Colony wealth sampled once a day, newest last.</summary>
        public List<float> wealthHistory = new List<float>();

        public CouncilMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref wealthHistory, "wealthHistory", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && wealthHistory == null) wealthHistory = new List<float>();
        }

        public override void MapComponentTick()
        {
            if (!map.IsPlayerHome) return;
            int t = Find.TickManager.TicksGame;
            if (t % GenDate.TicksPerDay == 433)
            {
                wealthHistory.Add(map.wealthWatcher.WealthTotal);
                if (wealthHistory.Count > 60) wealthHistory.RemoveAt(0);
            }
        }

        /// <summary>Wealth growth over the last <paramref name="days"/> days, as a fraction (0.3 = +30%).</summary>
        public float WealthGrowth(int days)
        {
            if (wealthHistory.Count < 2) return 0f;
            int idx = UnityEngine.Mathf.Max(0, wealthHistory.Count - 1 - days);
            float then = wealthHistory[idx];
            float now = map.wealthWatcher.WealthTotal;
            if (then <= 1f) return 0f;
            return now / then - 1f;
        }

        public List<AdvisorSignal> Signals
        {
            get
            {
                Refresh(false);
                return signals;
            }
        }

        public void Refresh(bool force)
        {
            int now = Find.TickManager.TicksGame;
            if (!force && now - lastEvalTick < GenDate.TicksPerHour) return;
            lastEvalTick = now;
            var list = new List<AdvisorSignal>();
            if (map.IsPlayerHome && map.mapPawns.FreeColonistsSpawnedCount > 0)
            {
                foreach (var def in DefDatabase<AdvisorSignalDef>.AllDefsListForReading)
                {
                    try
                    {
                        var produced = def.Worker.Evaluate(map);
                        if (produced == null) continue;
                        foreach (var s in produced)
                            if (s != null && s.severity > 0f) list.Add(s);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("advisor " + def.defName, e);
                    }
                }
            }
            signals = list.OrderByDescending(s => s.Score).ToList();
        }

        public static CouncilMapComponent For(Map map) => map?.GetComponent<CouncilMapComponent>();
    }

    /// <summary>A rough read on how well the colony can defend itself against the raids it attracts.</summary>
    public static class DefenseUtil
    {
        public struct Assessment
        {
            public float threatPoints;
            public float defenseScore;
            public float ratio;
            public int fighters;
            public int turrets;
            public int traps;
            public int cover;
        }

        private static Assessment cached;
        private static int cachedTick = -99999;
        private static Map cachedMap;

        public static Assessment Assess(Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (cachedMap == map && now - cachedTick < 1000) return cached;
            var a = new Assessment();
            a.threatPoints = UnityEngine.Mathf.Max(35f, StorytellerUtility.DefaultThreatPointsNow(map));
            float score = 0f;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.DevelopmentalStage.Juvenile()) continue;
                a.fighters++;
                var weapon = p.equipment?.Primary;
                bool ranged = weapon != null && weapon.def.IsRangedWeapon;
                int skill = ranged ? p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0 : p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                float s = 30f + 8f * skill;
                s *= weapon == null ? 0.5f : ranged ? 1.3f : 1f;
                score += s;
            }
            foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (p.RaceProps.IsMechanoid && !p.Downed) score += p.kindDef.combatPower;
            }
            a.turrets = Avail.ColonyCount(map, Avail.IsAutoTurret);
            a.traps = Avail.ColonyCount(map, Avail.IsTrap);
            a.cover = Avail.ColonyCount(map, Avail.IsCover);
            int mannable = Avail.ColonyCount(map, d => d.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !Avail.IsAutoTurret(d));
            score += a.turrets * 120f + mannable * 40f + UnityEngine.Mathf.Min(a.traps, 20) * 25f + UnityEngine.Mathf.Min(a.cover, 20) * 4f;
            a.defenseScore = score;
            a.ratio = score / a.threatPoints;
            cached = a;
            cachedTick = now;
            cachedMap = map;
            return a;
        }

        /// <summary>
        /// What defensive structure fits this colony: turrets if it has the tech, otherwise traps, otherwise
        /// cover. Returns the def, a key for counting, and how many more to aim for.
        /// </summary>
        public static (ThingDef def, string key, int extra) BestDefenseToAdd(Map map)
        {
            if (!Avail.IsPreIndustrial)
            {
                var turret = Avail.BestBuildable(map, Avail.IsAutoTurret, "Turret_MiniTurret");
                if (turret != null && Avail.MaterialsWithinReach(turret, map)) return (turret, "turret", 2);
            }
            var trap = Avail.BestBuildable(map, Avail.IsTrap, "TrapSpike");
            if (trap != null) return (trap, "trap", 4);
            var cover = Avail.BestBuildable(map, Avail.IsCover, "Sandbags", "Barricade");
            if (cover != null) return (cover, "cover", 8);
            return (null, null, 0);
        }

        public static int CountOfKind(Map map, string key)
        {
            switch (key)
            {
                case "turret": return Avail.ColonyCount(map, Avail.IsAutoTurret);
                case "trap": return Avail.ColonyCount(map, Avail.IsTrap);
                case "cover": return Avail.ColonyCount(map, Avail.IsCover);
                default: return 0;
            }
        }
    }
}
