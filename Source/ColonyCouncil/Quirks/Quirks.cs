using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>When a quirk's thought applies.</summary>
    public enum QuirkCondition
    {
        /// <summary>No thought at all: pure flavour (shown in tooltips and the journal).</summary>
        None,
        Always,
        Storm,
        Rain,
        Night,
        Dark,
        Outdoors,
        Drafted,
        Eclipse,
        Winter,
        /// <summary>Wearing or wielding the thing they decided was lucky (stage 0), or not (stage 1).</summary>
        LuckyItem
    }

    /// <summary>
    /// A belief or habit picked up from experience: a lucky helmet, a dread of storms, a battle ritual, prayers at
    /// night. Small effects at the right moments, lots of flavour, and it fades in time (or stays for good).
    /// </summary>
    public class QuirkDef : Def
    {
        public float weight = 1f;
        public DilemmaRole who;
        public bool children;
        /// <summary>Picked up after one of these memories (ThoughtDef names), within withinDays.</summary>
        public List<string> afterThoughts;
        public float withinDays = 2f;
        /// <summary>Picked up after fighting in a battle (and surviving it).</summary>
        public bool afterBattle;
        /// <summary>Picked up after being downed.</summary>
        public bool afterDowned;
        /// <summary>Picked up after one of their dreams came true.</summary>
        public bool afterOmen;
        /// <summary>Chance per day while the trigger holds.</summary>
        public float chance = 0.1f;
        /// <summary>How long it lasts (0 = for good).</summary>
        public float days;
        public QuirkCondition condition = QuirkCondition.None;
        /// <summary>The situational thought (workerClass ColonyCouncil.ThoughtWorker_Quirk). Empty for flavour-only quirks.</summary>
        public ThoughtDef thought;
        /// <summary>Journal when picked up ({ITEM}: the lucky thing). First person.</summary>
        public ToneLines gained = new ToneLines();
        /// <summary>Journal when it fades.</summary>
        public ToneLines faded = new ToneLines();

        public bool HasTrigger => !afterThoughts.NullOrEmpty() || afterBattle || afterDowned || afterOmen;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!HasTrigger) yield return "quirk has no trigger";
            if (condition != QuirkCondition.None && thought == null) yield return "quirk with a condition needs a thought";
            if (gained == null || !gained.Any) yield return "quirk has no gained lines";
        }
    }

    public class Quirk : IExposable
    {
        public QuirkDef def;
        public Pawn pawn;
        public ThingDef item;
        public int gainedTick;
        public int expiresTick;

        public string Label => def.label.Formatted((item?.label ?? "").Named("ITEM")).Resolve();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref item, "item");
            Scribe_Values.Look(ref gainedTick, "gainedTick");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
        }
    }

    public class QuirkTracker : IExposable
    {
        public List<Quirk> quirks = new List<Quirk>();
        private Dictionary<Pawn, int> downedTick = new Dictionary<Pawn, int>();
        private Dictionary<Pawn, int> battleTick = new Dictionary<Pawn, int>();
        private List<Pawn> k1, k2;
        private List<int> v1, v2;
        public const int MaxPerPawn = 2;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public IEnumerable<Quirk> Of(Pawn p) => quirks.Where(q => q.pawn == p);

        public Quirk Get(Pawn p, QuirkDef def) => quirks.FirstOrDefault(q => q.pawn == p && q.def == def);

        public void Notify_Fought(Pawn p) => battleTick[p] = Find.TickManager.TicksGame;

        private static bool Eligible(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        public void Hourly()
        {
            if (!S.quirks) return;
            int now = Find.TickManager.TicksGame;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                if (p.Downed && !p.Dead) downedTick[p] = now;
        }

        public void Daily(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            quirks.RemoveAll(q => q.pawn == null || q.pawn.Dead || q.def == null);
            foreach (var k in downedTick.Keys.Where(k => k == null || k.Dead).ToList()) downedTick.Remove(k);
            foreach (var k in battleTick.Keys.Where(k => k == null || k.Dead).ToList()) battleTick.Remove(k);
            if (!S.quirks) return;
            // Fading.
            foreach (var q in quirks.Where(q => q.expiresTick > 0 && now >= q.expiresTick).ToList())
            {
                quirks.Remove(q);
                string line = Line(q.def.faded, q);
                if (!line.NullOrEmpty()) comp.journals.WriteText(q.pawn, line);
            }
            // Picking up new ones.
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList())
            {
                if (Of(p).Count() >= MaxPerPawn) continue;
                var options = DefDatabase<QuirkDef>.AllDefsListForReading.Where(d => Get(p, d) == null && Triggered(comp, d, p) && DilemmaRole.Fits(d.who, p, d.children)).ToList();
                foreach (var d in options.InRandomOrder())
                {
                    if (!Rand.Chance(d.chance)) continue;
                    Gain(comp, p, d);
                    break;
                }
            }
        }

        private bool Triggered(CouncilGameComponent comp, QuirkDef d, Pawn p)
        {
            int now = Find.TickManager.TicksGame;
            int window = (int)(d.withinDays * GenDate.TicksPerDay);
            if (d.afterBattle && battleTick.TryGetValue(p, out int b) && now - b < window) return true;
            if (d.afterDowned && downedTick.TryGetValue(p, out int t) && now - t < window && !p.Downed) return true;
            if (d.afterOmen && comp.dreams.recent.Any(r => r.pawn == p && r.cameTrue && now - r.tick < GenDate.TicksPerDay * 10)) return true;
            if (!d.afterThoughts.NullOrEmpty())
            {
                var memories = p.needs?.mood?.thoughts?.memories?.Memories;
                if (memories != null && memories.Any(m => m.def != null && m.age <= window && d.afterThoughts.Contains(m.def.defName))) return true;
            }
            return false;
        }

        public void Gain(CouncilGameComponent comp, Pawn p, QuirkDef d)
        {
            var q = new Quirk { def = d, pawn = p, gainedTick = Find.TickManager.TicksGame, expiresTick = d.days > 0f ? Find.TickManager.TicksGame + (int)(d.days * GenDate.TicksPerDay) : 0 };
            if (d.condition == QuirkCondition.LuckyItem)
            {
                q.item = p.apparel?.WornApparel.Where(a => a.def.apparel?.layers?.Contains(ApparelLayerDefOf.Overhead) ?? false).Select(a => a.def).FirstOrDefault()
                         ?? p.equipment?.Primary?.def
                         ?? p.apparel?.WornApparel.Select(a => a.def).RandomElementWithFallback();
                if (q.item == null) return;
            }
            quirks.Add(q);
            string line = Line(d.gained, q);
            if (!line.NullOrEmpty()) comp.journals.WriteText(p, line);
            Messages.Message("CC_Quirk_Gained".Translate(p.Named("PAWN"), q.Label.Named("QUIRK")).Resolve() + (line.NullOrEmpty() ? "" : " \"" + line + "\""), p, MessageTypeDefOf.SilentInput, false);
        }

        private static string Line(ToneLines lines, Quirk q)
        {
            if (lines == null || !lines.Any || q.pawn == null || q.pawn.Dead) return null;
            string raw = lines.Pick(ToneUtility.ToneFor(q.pawn), Flavor.TagsFor(q.pawn));
            return raw.NullOrEmpty() ? null : raw.Formatted(q.pawn.Named("PAWN"), (q.item?.label ?? "").Named("ITEM")).Resolve();
        }

        public string Describe(Pawn p)
        {
            var list = Of(p).ToList();
            return list.Count == 0 || !S.quirks ? null : "CC_Quirk_Tooltip".Translate(string.Join(", ", list.Select(q => q.Label))).Resolve();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref quirks, "quirks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var k in downedTick.Keys.Where(k => k == null || k.Dead || k.Destroyed).ToList()) downedTick.Remove(k);
                foreach (var k in battleTick.Keys.Where(k => k == null || k.Dead || k.Destroyed).ToList()) battleTick.Remove(k);
            }
            Scribe_Collections.Look(ref downedTick, "downedTick", LookMode.Reference, LookMode.Value, ref k1, ref v1);
            Scribe_Collections.Look(ref battleTick, "battleTick", LookMode.Reference, LookMode.Value, ref k2, ref v2);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (quirks == null) quirks = new List<Quirk>();
                quirks.RemoveAll(q => q == null || q.def == null || q.pawn == null);
                if (downedTick == null) downedTick = new Dictionary<Pawn, int>();
                if (battleTick == null) battleTick = new Dictionary<Pawn, int>();
            }
        }
    }

    /// <summary>The thought of a quirk, while its condition holds. Finds its quirk by the thought def.</summary>
    public class ThoughtWorker_Quirk : ThoughtWorker
    {
        private QuirkDef quirk;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.quirks == null || !ColonyCouncilMod.Settings.quirks) return ThoughtState.Inactive;
            if (quirk == null) quirk = DefDatabase<QuirkDef>.AllDefsListForReading.FirstOrDefault(q => q.thought == def);
            if (quirk == null) return ThoughtState.Inactive;
            var q = comp.quirks.Get(p, quirk);
            if (q == null) return ThoughtState.Inactive;
            var map = p.MapHeld;
            switch (quirk.condition)
            {
                case QuirkCondition.Always:
                    return ThoughtState.ActiveAtStage(0);
                case QuirkCondition.Storm:
                {
                    var w = map?.weatherManager?.curWeather;
                    return w != null && (w.defName.Contains("Thunder") || w.defName.Contains("Storm")) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                }
                case QuirkCondition.Rain:
                    return (map?.weatherManager?.curWeather?.rainRate ?? 0f) > 0.01f ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case QuirkCondition.Night:
                {
                    if (map == null) return ThoughtState.Inactive;
                    int hour = GenLocalDate.HourOfDay(map);
                    return hour >= 22 || hour < 5 ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                }
                case QuirkCondition.Dark:
                    return p.Spawned && p.Awake() && p.Map.glowGrid.PsychGlowAt(p.Position) == PsychGlow.Dark ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case QuirkCondition.Outdoors:
                    return p.Spawned && !p.Position.Roofed(p.Map) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case QuirkCondition.Drafted:
                    return p.Drafted ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case QuirkCondition.Eclipse:
                    return map != null && map.GameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case QuirkCondition.Winter:
                {
                    if (map == null) return ThoughtState.Inactive;
                    var season = GenLocalDate.Season(map);
                    return season == Season.Winter || season == Season.PermanentWinter ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                }
                case QuirkCondition.LuckyItem:
                {
                    if (q.item == null) return ThoughtState.Inactive;
                    bool has = (p.apparel?.WornApparel.Any(a => a.def == q.item) ?? false) || p.equipment?.Primary?.def == q.item;
                    return ThoughtState.ActiveAtStage(has ? 0 : 1);
                }
                default:
                    return ThoughtState.Inactive;
            }
        }
    }

    /// <summary>A lucky-item thought names the item.</summary>
    public class Thought_Quirk : Thought_Situational
    {
        public override string LabelCap
        {
            get
            {
                var q = CouncilGameComponent.Instance?.quirks.quirks.FirstOrDefault(x => x.pawn == pawn && x.def.thought == def);
                string label = base.LabelCap;
                return q?.item != null ? label.Formatted(q.item.label.Named("ITEM")).Resolve().CapitalizeFirst() : label;
            }
        }
    }
}
