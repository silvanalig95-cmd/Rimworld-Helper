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
        LateNight,
        /// <summary>Within a few steps of a grave with someone in it.</summary>
        NearGrave,
        /// <summary>Awake in a sickbed, or in bed while they need the rest.</summary>
        Sickbed
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
        /// <summary>
        /// Work that brings it sooner and keeps it while they're at it (Practice.cs): work type defNames, or "work" for
        /// any work. Someone at it <see cref="practiceHours"/> lately has it up to ten times as likely each day; at
        /// two fifths of that it counts as lived, and doesn't fade.
        /// </summary>
        public List<string> practice;
        /// <summary>Recent hours at the <see cref="practice"/> work for the full effect (steady work of h hours a day settles near 20·h).</summary>
        public float practiceHours = 40f;
        /// <summary>
        /// It comes only from doing its work (the more, the likelier), never out of the blue; without it, by nature it
        /// comes to anyone who fits, at its chance, and sooner to those at its work.
        /// </summary>
        public bool onlyFromPractice;
        /// <summary>
        /// What else it does, beyond its mood and its knack, in a few words for its line ("warns you when food runs
        /// low"); empty when that's all.
        /// </summary>
        [MustTranslate] public string effect;
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
            if (!practice.NullOrEmpty())
            {
                if (practiceHours <= 0f) yield return "practiceHours must be above 0";
                foreach (var w in practice)
                    if (!Practice.IsKnown(w)) yield return "unknown practice " + w;
            }
            if (onlyFromPractice && !Practised) yield return "onlyFromPractice needs a practice";
        }

        /// <summary>Whether living it is something they do (it has work behind it).</summary>
        public bool Practised => !practice.NullOrEmpty();
    }

    public class Quirk : IExposable
    {
        public QuirkDef def;
        public Pawn pawn;
        public ThingDef item;
        /// <summary>
        /// For a lucky-item quirk, the very thing that's lucky (its thingIDNumber; 0 until known), so it can say whose it
        /// is (LuckyItems). The mood still comes with any thing of that kind.
        /// </summary>
        public int thingId;
        public int gainedTick;
        public int expiresTick;
        /// <summary>Days they lived it: at its work, or at its moment in their free time (Notify_Lived).</summary>
        public int livedDays;
        /// <summary>The last day it was lived (ticks / day), so a day counts once; -1 never.</summary>
        public int lastLivedDay = -1;
        /// <summary>How it came (QuirkCause): from their work, by nature, or from something they lived through; null in older saves.</summary>
        public string cause;

        /// <summary>Lived long enough to be deep-rooted: its knack and its good mood a little stronger.</summary>
        public bool Deep => PracticeMath.Deep(livedDays);

        public string Label => def.label.Formatted((item?.label ?? "").Named("ITEM")).Resolve();

        /// <summary>A lucky-item quirk whose item is gone (its mod was removed): it is quietly forgotten.</summary>
        public bool Broken => def == null || pawn == null || (def.condition == QuirkCondition.LuckyItem && item == null);

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            // Silently: the item may come from a mod that has since been removed.
            CouncilScribe.LookDef(ref item, "item");
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref gainedTick, "gainedTick");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
            Scribe_Values.Look(ref livedDays, "livedDays");
            Scribe_Values.Look(ref lastLivedDay, "lastLivedDay", -1);
            Scribe_Values.Look(ref cause, "cause");
        }
    }

    public class QuirkTracker : IExposable
    {
        public List<Quirk> quirks = new List<Quirk>();
        /// <summary>Lucky things that were someone's once (LuckyItems): whose, for how long, and how it ended.</summary>
        public List<LuckyRecord> luckyPast = new List<LuckyRecord>();
        private Dictionary<Pawn, int> downedTick = new Dictionary<Pawn, int>();
        private Dictionary<Pawn, int> battleTick = new Dictionary<Pawn, int>();
        private List<Pawn> k1, k2;
        private List<int> v1, v2;
        /// <summary>What each colonist has been doing lately (Practice.cs).</summary>
        public List<PracticeLog> practice = new List<PracticeLog>();
        [Unsaved] private Dictionary<int, PracticeLog> practiceById;
        public const int MaxPerPawn = 2;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>A new game or a loaded one: the lucky things are looked up again.</summary>
        public QuirkTracker() => LuckyItems.MarkDirty();

        public IEnumerable<Quirk> Of(Pawn p) => quirks.Where(q => q.pawn == p);

        /// <summary>Ends the quirks that match (a story ends a fear); a lucky thing remembers whose it was.</summary>
        public int Remove(Predicate<Quirk> which, LuckyEnd end = LuckyEnd.Faded)
        {
            if (which == null) return 0;
            foreach (var q in quirks)
                if (q != null && which(q))
                    LuckyItems.Remember(this, q, end);
            return quirks.RemoveAll(q => q != null && which(q));
        }

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

        // ---------------- Practice, and habits lived ----------------

        public PracticeLog PracticeOf(Pawn p, bool create)
        {
            if (p == null) return null;
            if (practiceById == null || practiceById.Count != practice.Count)
            {
                practiceById = new Dictionary<int, PracticeLog>();
                foreach (var l in practice)
                    if (l?.pawn != null)
                        practiceById[l.pawn.thingIDNumber] = l;
            }
            if (practiceById.TryGetValue(p.thingIDNumber, out var log) || !create) return log;
            log = new PracticeLog { pawn = p };
            practice.Add(log);
            practiceById[p.thingIDNumber] = log;
            return log;
        }

        /// <summary>Practice is kept while habits or trait changes can use it.</summary>
        private static bool PracticeOn => S.quirks || (Persona.On && S.traitChanges);

        /// <summary>The hour's look at what everyone is doing (Practice.cs): their work, and whether they're up and busy in the small hours.</summary>
        private void SamplePractice()
        {
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                int hour = GenLocalDate.HourOfDay(map);
                bool night = hour < 5 || hour >= 23;
                bool morning = hour >= 4 && hour < 6;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!Eligible(p) || !p.Awake()) continue;
                    var job = p.CurJob;
                    var wt = job?.workGiverDef?.workType;
                    bool counts = wt != null && wt.defName != "Firefighter" && wt.defName != "Patient" && wt.defName != "PatientBedRest";
                    bool busy = counts || job?.def?.joyKind != null;
                    bool outside = !p.Position.Roofed(map) && !p.InBed();
                    if (!counts && !(busy && (night || morning)) && !outside) continue;
                    var log = PracticeOf(p, true);
                    if (counts) log.Add(wt.defName);
                    if (busy && night) log.night += 1f;
                    if (busy && morning) log.morning += 1f;
                    if (outside) log.outdoors += 1f;
                }
            }
        }

        /// <summary>
        /// They lived one of their habits today: did its moment in their free time (the small habits call this), or its
        /// work (checked each day). Counted once a day; lived, it doesn't fade for a while (PracticeMath.KeepDays).
        /// </summary>
        public void Notify_Lived(Pawn p, QuirkDef def)
        {
            if (p == null || def == null) return;
            var q = Get(p, def);
            if (q == null) return;
            int now = Find.TickManager.TicksGame;
            int day = now / GenDate.TicksPerDay;
            if (q.lastLivedDay == day) return;
            bool wasDeep = q.Deep;
            q.lastLivedDay = day;
            q.livedDays++;
            q.expiresTick = PracticeMath.KeptUntil(q.expiresTick, now, GenDate.TicksPerDay);
            if (!wasDeep && q.Deep)
            {
                SyncKnacks();
                News.Post("CC_Quirk_Deep".Translate(p.Named("PAWN"), q.Label.Named("QUIRK")).Resolve(), p, NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
            }
        }

        /// <summary>The day's look at practice: what faded, and the habits lived at their work.</summary>
        private void DailyPractice()
        {
            practice.RemoveAll(l => l?.pawn == null || l.pawn.Dead || l.pawn.Discarded || !l.pawn.IsColonist);
            foreach (var l in practice) l.Fade();
            practice.RemoveAll(l => l.Empty);
            practiceById = null;
            foreach (var q in quirks.ToList())
            {
                if (q?.def == null || !q.def.Practised || q.pawn == null) continue;
                if (PracticeMath.Lived(Practice.Hours(q.pawn, q.def.practice), q.def.practiceHours)) Notify_Lived(q.pawn, q.def);
            }
        }

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
                PersonaMarks.Notify_Downed(p);
                CouncilGameComponent.Instance?.aspirations.Notify_Event(p, "downed");
            }
            LuckyItems.Bind(this);
            if (PracticeOn) SamplePractice();
        }

        public void Daily(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            // Someone who died, or was lost to the colony for good: their lucky thing remembers them.
            foreach (var q in quirks)
                if (q != null && !q.Broken && (q.pawn.Dead || q.pawn.Discarded))
                    LuckyItems.Remember(this, q, q.pawn.Dead ? LuckyEnd.Died : LuckyEnd.Gone);
            quirks.RemoveAll(q => q.Broken || q.pawn.Dead || q.pawn.Discarded);
            foreach (var k in downedTick.Keys.Where(k => k == null || k.Dead || k.Discarded).ToList()) downedTick.Remove(k);
            foreach (var k in battleTick.Keys.Where(k => k == null || k.Dead || k.Discarded).ToList()) battleTick.Remove(k);
            // A fight or a fall only counts within a quirk's window: older ones are forgotten (someone who left keeps none).
            Caps.DropOlderThan(downedTick, now, LongestWindow);
            Caps.DropOlderThan(battleTick, now, LongestWindow);
            if (PracticeOn) DailyPractice();
            if (!S.quirks)
            {
                SyncKnacks();
                return;
            }
            // Fading.
            foreach (var q in quirks.Where(q => q.expiresTick > 0 && now >= q.expiresTick).ToList())
            {
                LuckyItems.Remember(this, q, LuckyEnd.Faded);
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
                    // Someone at the matching work has it sooner, and some come only from the work (Practice.cs).
                    float hours = d.Practised ? Practice.Hours(p, d.practice) : 0f;
                    float chance = d.Practised ? d.chance * PracticeMath.Earned(hours, d.practiceHours, d.onlyFromPractice) : d.chance;
                    if (!Rand.Chance(chance)) continue;
                    Gain(comp, p, d, Cause(comp, d, p, hours));
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

        /// <summary>How a quirk picked up today came: its work (when they're at it enough to live it), its trigger, or their nature.</summary>
        private string Cause(CouncilGameComponent comp, QuirkDef d, Pawn p, float hours)
        {
            if (d.Practised && (d.onlyFromPractice || PracticeMath.Lived(hours, d.practiceHours))) return QuirkCause.Practice;
            int now = Find.TickManager.TicksGame;
            int window = (int)(d.withinDays * GenDate.TicksPerDay);
            if (d.afterBattle && battleTick.TryGetValue(p, out int b) && now - b < window) return QuirkCause.Battle;
            if (d.afterDowned && downedTick.TryGetValue(p, out int t) && now - t < window) return QuirkCause.Downed;
            if (d.afterOmen && comp.dreams.recent.Any(r => r.pawn == p && r.cameTrue && now - r.tick < GenDate.TicksPerDay * 10)) return QuirkCause.Omen;
            if (!d.afterThoughts.NullOrEmpty()) return QuirkCause.Lived;
            return QuirkCause.Nature;
        }

        public void Gain(CouncilGameComponent comp, Pawn p, QuirkDef d, string cause = null)
        {
            var q = new Quirk { def = d, pawn = p, cause = cause, gainedTick = Find.TickManager.TicksGame, expiresTick = d.days > 0f ? Find.TickManager.TicksGame + (int)(d.days * GenDate.TicksPerDay) : 0 };
            if (d.condition == QuirkCondition.LuckyItem)
            {
                // The very thing they had on: a hat or helmet first, else their weapon. It will say whose it is.
                var thing = LuckyItems.Pick(p);
                q.item = thing?.def;
                if (q.item == null) return;
                if (thing.thingIDNumber > 0 && !LuckyItems.Claimed(this, thing.thingIDNumber)) q.thingId = thing.thingIDNumber;
            }
            quirks.Add(q);
            if (q.thingId != 0) LuckyItems.MarkDirty();
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
                        var q = S.quirks ? quirks.FirstOrDefault(x => x.pawn == p && x.def.hediff == h) : null;
                        var existing = set.GetFirstHediffOfDef(h);
                        if (q != null && existing == null)
                        {
                            existing = HediffMaker.MakeHediff(h, p);
                            p.health.AddHediff(existing);
                        }
                        else if (q == null && existing != null) p.health.RemoveHediff(existing);
                        // A deep-rooted habit's knack is its second stage (Council_Hediffs_Quirks.xml).
                        if (q != null && existing != null)
                        {
                            float severity = q.Deep ? 1f : 0.5f;
                            if (existing.Severity != severity) existing.Severity = severity;
                        }
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
            Scribe_Collections.Look(ref luckyPast, "luckyPast", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) practice.RemoveAll(l => l?.pawn == null || l.pawn.Dead || l.pawn.Discarded);
            Scribe_Collections.Look(ref practice, "practice", LookMode.Deep);
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
                // Saves from before lucky things remembered their owners have none yet.
                if (luckyPast == null) luckyPast = new List<LuckyRecord>();
                practice ??= new List<PracticeLog>();
                practice.RemoveAll(l => l?.pawn == null);
                practiceById = null;
                luckyPast.RemoveAll(r => r == null || r.thingId == 0 || r.owner.NullOrEmpty());
                LuckyItems.MarkDirty();
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
                case QuirkCondition.NearGrave:
                {
                    if (!p.Spawned || !p.Awake()) return false;
                    foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 7.9f, true))
                        if (t is Building_Grave g && g.HasCorpse) return true;
                    return false;
                }
                case QuirkCondition.Sickbed:
                    return p.Spawned && p.Awake() && p.InBed() && (p.CurrentBed()?.Medical == true || HealthAIUtility.ShouldSeekMedicalRest(p));
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
            if (m < 0f) return m * ColonyCouncilMod.Settings.PenaltyScale;
            var q = CouncilGameComponent.Instance?.quirks.OfThought(pawn, def);
            return PracticeMath.Mood(m, q != null && q.Deep);
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
