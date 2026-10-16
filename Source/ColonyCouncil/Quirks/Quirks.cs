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
        LuckyItem,
        /// <summary>At any work (a job from the work tab).</summary>
        Working,
        /// <summary>At the quirk's <see cref="QuirkDef.workType"/> (Growing, Cooking, Handling...).</summary>
        WorkingAt,
        /// <summary>Up and about between five and ten in the morning.</summary>
        Morning,
        /// <summary>In their own bedroom.</summary>
        InOwnRoom,
        /// <summary>Wearing something red.</summary>
        WearingRed,
        /// <summary>Indoors in a dirty room.</summary>
        MessyRoom,
        /// <summary>Awake under the open sky at night.</summary>
        NightSky,
        /// <summary>Somewhere colder than 8 °C.</summary>
        Cold,
        /// <summary>Awake late in the evening or at night (after nine).</summary>
        LateNight
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
        /// <summary>Simply who they are: picked up on any day by someone who fits <see cref="who"/>.</summary>
        public bool byNature;
        /// <summary>Chance per day while the trigger holds.</summary>
        public float chance = 0.1f;
        /// <summary>How long it lasts (0 = for good).</summary>
        public float days;
        public QuirkCondition condition = QuirkCondition.None;
        /// <summary>When <see cref="condition"/> doesn't hold and this does, the thought's second stage (an early riser up too late).</summary>
        public QuirkCondition elseCondition = QuirkCondition.None;
        /// <summary>For <see cref="QuirkCondition.WorkingAt"/>.</summary>
        public WorkTypeDef workType;
        /// <summary>The situational thought (workerClass ColonyCouncil.ThoughtWorker_Quirk). Empty for flavour-only quirks.</summary>
        public ThoughtDef thought;
        /// <summary>A small lasting effect while they have the quirk (a knack: plant work a little faster).</summary>
        public HediffDef hediff;
        /// <summary>Journal when picked up ({ITEM}: the lucky thing). First person.</summary>
        public ToneLines gained = new ToneLines();
        /// <summary>Journal when it fades.</summary>
        public ToneLines faded = new ToneLines();

        public bool HasTrigger => !afterThoughts.NullOrEmpty() || afterBattle || afterDowned || afterOmen || byNature;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!HasTrigger) yield return "quirk has no trigger";
            if (condition != QuirkCondition.None && thought == null) yield return "quirk with a condition needs a thought";
            if (elseCondition != QuirkCondition.None && (thought?.stages == null || thought.stages.Count < 2)) yield return "quirk with an else condition needs a thought with two stages";
            if ((condition == QuirkCondition.WorkingAt || elseCondition == QuirkCondition.WorkingAt) && workType == null) yield return "WorkingAt needs a workType";
            if (byNature && who == null) yield return "a quirk by nature needs a who";
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

        /// <summary>A lucky-item quirk whose item is gone (its mod was removed): it is quietly forgotten.</summary>
        public bool Broken => def == null || pawn == null || (def.condition == QuirkCondition.LuckyItem && item == null);

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            // Silently: the item may come from a mod that has since been removed.
            CouncilScribe.LookDef(ref item, "item");
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

        /// <summary>Their quirk of this kind, or null. Asked by the quirk moods for every colonist, so a plain loop.</summary>
        public Quirk Get(Pawn p, QuirkDef def)
        {
            for (int i = 0; i < quirks.Count; i++)
            {
                var q = quirks[i];
                if (q.pawn == p && q.def == def) return q;
            }
            return null;
        }

        /// <summary>The colonist's quirk whose mood this is, or null (the Needs tab asks for its label every frame).</summary>
        public Quirk OfThought(Pawn p, ThoughtDef thought)
        {
            for (int i = 0; i < quirks.Count; i++)
            {
                var q = quirks[i];
                if (q.pawn == p && q.def?.thought == thought) return q;
            }
            return null;
        }

        public void Notify_Fought(Pawn p) => battleTick[p] = Find.TickManager.TicksGame;

        private static int longestWindow = -1;

        /// <summary>The longest any quirk looks back for a fight or a fall (ticks).</summary>
        private static int LongestWindow
        {
            get
            {
                if (longestWindow < 0)
                {
                    int most = 0;
                    foreach (var d in DefDatabase<QuirkDef>.AllDefsListForReading)
                        if (d.afterBattle || d.afterDowned)
                            most = Math.Max(most, (int)(d.withinDays * GenDate.TicksPerDay));
                    longestWindow = most;
                }
                return longestWindow;
            }
        }

        private static bool Eligible(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        public void Hourly()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                if (!p.Downed || p.Dead) continue;
                downedTick[p] = now;
                CouncilGameComponent.Instance?.aspirations.Notify_Event(p, "downed");
            }
        }

        public void Daily(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            quirks.RemoveAll(q => q.Broken || q.pawn.Dead || q.pawn.Discarded);
            foreach (var k in downedTick.Keys.Where(k => k == null || k.Dead || k.Discarded).ToList()) downedTick.Remove(k);
            foreach (var k in battleTick.Keys.Where(k => k == null || k.Dead || k.Discarded).ToList()) battleTick.Remove(k);
            // A fight or a fall only counts within a quirk's window: older ones are forgotten (someone who left keeps none).
            Caps.DropOlderThan(downedTick, now, LongestWindow);
            Caps.DropOlderThan(battleTick, now, LongestWindow);
            if (!S.quirks)
            {
                SyncKnacks();
                return;
            }
            // Fading.
            foreach (var q in quirks.Where(q => q.expiresTick > 0 && now >= q.expiresTick).ToList())
            {
                quirks.Remove(q);
                string line = Line(q.def.faded, q);
                if (!line.NullOrEmpty()) comp.journals.WriteText(q.pawn, line);
            }
            SyncKnacks();
            // Picking up new ones.
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList().Where(Eligible).ToList())
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
            if (d.byNature) return true;
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
            SyncKnacks();
            string line = Line(d.gained, q);
            if (!line.NullOrEmpty()) comp.journals.WriteText(p, line);
            News.Post("CC_Quirk_Gained".Translate(p.Named("PAWN"), q.Label.Named("QUIRK")).Resolve() + (line.NullOrEmpty() ? "" : " \"" + line + "\""), p, NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        private static List<HediffDef> knacks;

        /// <summary>The small lasting effects quirks can bring (their hediffs).</summary>
        private static List<HediffDef> Knacks => knacks ??= DefDatabase<QuirkDef>.AllDefsListForReading.Where(d => d.hediff != null).Select(d => d.hediff).Distinct().ToList();

        /// <summary>Everyone with a quirk has its knack, and nobody keeps one without the quirk (faded, or quirks off).</summary>
        private void SyncKnacks()
        {
            if (Knacks.Count == 0) return;
            try
            {
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                {
                    var set = p.health?.hediffSet;
                    if (set == null) continue;
                    foreach (var h in Knacks)
                    {
                        bool want = S.quirks && quirks.Any(q => q.pawn == p && q.def.hediff == h);
                        var existing = set.GetFirstHediffOfDef(h);
                        if (want && existing == null) p.health.AddHediff(HediffMaker.MakeHediff(h, p));
                        else if (!want && existing != null) p.health.RemoveHediff(existing);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quirk knacks", e);
            }
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
                foreach (var k in downedTick.Keys.Where(k => k == null || k.Dead || k.Destroyed || k.Discarded).ToList()) downedTick.Remove(k);
                foreach (var k in battleTick.Keys.Where(k => k == null || k.Dead || k.Destroyed || k.Discarded).ToList()) battleTick.Remove(k);
            }
            Scribe_Collections.Look(ref downedTick, "downedTick", LookMode.Reference, LookMode.Value, ref k1, ref v1, false);
            Scribe_Collections.Look(ref battleTick, "battleTick", LookMode.Reference, LookMode.Value, ref k2, ref v2, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (quirks == null) quirks = new List<Quirk>();
                quirks.RemoveAll(q => q == null || q.Broken);
                if (downedTick == null) downedTick = new Dictionary<Pawn, int>();
                if (battleTick == null) battleTick = new Dictionary<Pawn, int>();
                // (A pawn that couldn't be found again is simply left out by the loader: a dictionary has no null keys.)
            }
        }
    }

    /// <summary>The thought of a quirk, while its condition holds. Finds its quirk by the thought def.</summary>
    public class ThoughtWorker_Quirk : ThoughtWorker
    {
        private QuirkDef quirk;
        /// <summary>Looked for its quirk already (a habit whose mood became a knack has none any more).</summary>
        private bool looked;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            var s = ColonyCouncilMod.Settings;
            if (comp?.quirks == null || !s.quirks || !s.MoodEffects) return ThoughtState.Inactive;
            if (!looked)
            {
                quirk = DefDatabase<QuirkDef>.AllDefsListForReading.FirstOrDefault(q => q.thought == def);
                looked = true;
            }
            if (quirk == null) return ThoughtState.Inactive;
            var q = comp.quirks.Get(p, quirk);
            if (q == null) return ThoughtState.Inactive;
            int stage;
            if (quirk.condition == QuirkCondition.LuckyItem)
            {
                if (q.item == null) return ThoughtState.Inactive;
                bool has = Wears(p, q.item) || p.equipment?.Primary?.def == q.item;
                stage = has ? 0 : 1;
            }
            else if (Holds(quirk.condition, quirk, p)) stage = 0;
            else if (quirk.elseCondition != QuirkCondition.None && Holds(quirk.elseCondition, quirk, p)) stage = 1;
            else return ThoughtState.Inactive;
            if (def.stages == null || stage >= def.stages.Count) return ThoughtState.Inactive;
            if (!s.Negatives && def.stages[stage] != null && def.stages[stage].baseMoodEffect < 0f) return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage(stage);
        }

        private static bool Wears(Pawn p, ThingDef item)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return false;
            for (int i = 0; i < worn.Count; i++)
                if (worn[i].def == item)
                    return true;
            return false;
        }

        /// <summary>Whether the moment fits: the weather, the hour, where they are and what they're doing.</summary>
        public static bool Holds(QuirkCondition c, QuirkDef quirk, Pawn p)
        {
            var map = p.MapHeld;
            switch (c)
            {
                case QuirkCondition.Always:
                    return true;
                case QuirkCondition.Storm:
                    return Flavor.IsStorm(map?.weatherManager?.curWeather);
                case QuirkCondition.Rain:
                    return (map?.weatherManager?.curWeather?.rainRate ?? 0f) > 0.01f;
                case QuirkCondition.Night:
                {
                    if (map == null) return false;
                    int hour = GenLocalDate.HourOfDay(map);
                    return hour >= 22 || hour < 5;
                }
                case QuirkCondition.Dark:
                    return p.Spawned && p.Awake() && p.Map.glowGrid.PsychGlowAt(p.Position) == PsychGlow.Dark;
                case QuirkCondition.Outdoors:
                    return p.Spawned && !p.Position.Roofed(p.Map);
                case QuirkCondition.Drafted:
                    return p.Drafted;
                case QuirkCondition.Eclipse:
                    return map != null && map.GameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse);
                case QuirkCondition.Winter:
                {
                    if (map == null) return false;
                    var season = GenLocalDate.Season(map);
                    return season == Season.Winter || season == Season.PermanentWinter;
                }
                case QuirkCondition.Working:
                    return p.Spawned && p.Awake() && p.CurJob?.workGiverDef?.workType != null;
                case QuirkCondition.WorkingAt:
                    return p.Spawned && p.Awake() && quirk?.workType != null && p.CurJob?.workGiverDef?.workType == quirk.workType;
                case QuirkCondition.Morning:
                {
                    if (map == null || !p.Awake()) return false;
                    int hour = GenLocalDate.HourOfDay(map);
                    return hour >= 5 && hour < 10;
                }
                case QuirkCondition.LateNight:
                {
                    if (map == null || !p.Awake()) return false;
                    int hour = GenLocalDate.HourOfDay(map);
                    return hour >= 21 || hour < 4;
                }
                case QuirkCondition.InOwnRoom:
                {
                    if (!p.Spawned) return false;
                    var own = RoomUtil.PrivateRoom(p);
                    return own != null && p.GetRoom() == own;
                }
                case QuirkCondition.WearingRed:
                {
                    var worn = p.apparel?.WornApparel;
                    if (worn == null) return false;
                    for (int i = 0; i < worn.Count; i++)
                        if (IsRed(worn[i].DrawColor)) return true;
                    return false;
                }
                case QuirkCondition.MessyRoom:
                {
                    if (!p.Spawned || !p.Awake()) return false;
                    var room = p.GetRoom();
                    return room != null && !room.PsychologicallyOutdoors && room.ProperRoom && room.GetStat(RoomStatDefOf.Cleanliness) < -0.6f;
                }
                case QuirkCondition.NightSky:
                {
                    if (map == null || !p.Spawned || !p.Awake() || p.Position.Roofed(p.Map)) return false;
                    int hour = GenLocalDate.HourOfDay(map);
                    return hour >= 21 || hour < 5;
                }
                case QuirkCondition.Cold:
                    return p.Spawned && p.AmbientTemperature < 8f;
                default:
                    return false;
            }
        }

        /// <summary>Red enough for a superstition: a strong red, not a brown or an orange.</summary>
        public static bool IsRed(Color c) => c.r > 0.5f && c.r > c.g * 1.8f && c.r > c.b * 1.8f;
    }

    /// <summary>A lucky-item thought names the item.</summary>
    public class Thought_Quirk : Thought_Situational
    {
        public override string Description
        {
            get
            {
                var q = CouncilGameComponent.Instance?.quirks.OfThought(pawn, def);
                return Causes.Append(base.Description, q != null ? "CC_Cause_Quirk".Translate(q.Label).Resolve() : null);
            }
        }

        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            return m < 0f ? m * ColonyCouncilMod.Settings.PenaltyScale : m;
        }

        public override string LabelCap
        {
            get
            {
                // From the stage's own label: the game's label has already dropped {ITEM} by the time it gets here.
                string raw = CurStage?.label;
                if (raw.NullOrEmpty() || !raw.Contains("{ITEM}")) return base.LabelCap;
                var q = CouncilGameComponent.Instance?.quirks.OfThought(pawn, def);
                string item = q?.item?.label ?? "CC_Quirk_ItemFallback".Translate().ToString();
                return raw.Formatted(item.Named("ITEM")).Resolve().CapitalizeFirst();
            }
        }
    }
}
