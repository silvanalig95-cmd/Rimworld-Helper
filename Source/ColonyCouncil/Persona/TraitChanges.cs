using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One deed counted for a trait change (<see cref="TraitTally"/>): when, about whom, and their name.</summary>
    public class TallyEntry : IExposable
    {
        public string key;
        public int tick;
        public int about = -1;
        public string label;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref about, "about", -1);
            Scribe_Values.Look(ref label, "label");
        }
    }

    /// <summary>The running totals of a few of the game's records on one day, to measure a share of time over a stretch.</summary>
    public class PaceSnapshot : IExposable
    {
        public int tick;
        public Dictionary<string, float> values = new Dictionary<string, float>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Collections.Look(ref values, "values", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) values ??= new Dictionary<string, float>();
        }
    }

    /// <summary>What the trait changes keep watching for one colonist: deeds counted, how they spend their time, how long they've been calm.</summary>
    public class TraitWatch : IExposable
    {
        public int id;
        public List<TallyEntry> tallies = new List<TallyEntry>();
        public List<PaceSnapshot> snapshots = new List<PaceSnapshot>();
        /// <summary>Their count of mental states at the last look, and since when it hasn't risen.</summary>
        public float mentalStates = -1f;
        public int calmSince = -1;

        public const int MaxSnapshots = 10, SnapshotDays = 10, MaxPerKey = 12;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Collections.Look(ref tallies, "tallies", LookMode.Deep);
            Scribe_Collections.Look(ref snapshots, "snapshots", LookMode.Deep);
            Scribe_Values.Look(ref mentalStates, "mentalStates", -1f);
            Scribe_Values.Look(ref calmSince, "calmSince", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tallies ??= new List<TallyEntry>();
                tallies.RemoveAll(t => t == null || t.key == null);
                snapshots ??= new List<PaceSnapshot>();
                snapshots.RemoveAll(s => s == null);
            }
        }
    }

    /// <summary>A trait a colonist gained or lost, kept for their character page with the story behind it.</summary>
    public class TraitChangeRecord : IExposable
    {
        public int pawn;
        public string name;
        public int tick;
        public string rule, trait;
        public bool hadTrait, hasAfter;
        public int from, to;
        /// <summary>"became steadfast", "no longer abrasive", "from nervous to steadfast".</summary>
        public string summary;
        /// <summary>The letter: what happened and what led to it, with dates.</summary>
        public string story;
        /// <summary>How the trait moved their nature on each side (taken out of what life had moved, so nothing doubled).</summary>
        public List<float> absorbed = new List<float>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref rule, "rule");
            Scribe_Values.Look(ref trait, "trait");
            Scribe_Values.Look(ref hadTrait, "hadTrait");
            Scribe_Values.Look(ref hasAfter, "hasAfter");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref to, "to");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref story, "story");
            Scribe_Collections.Look(ref absorbed, "absorbed", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) absorbed ??= new List<float>();
        }
    }

    /// <summary>
    /// The little the trait changes keep (Persona/TraitChanges.cs): the changes made (for the character page), what each
    /// colonist is watched for, the colony's last change, and the changes that come once in a colony's life. Pruned of
    /// people the game no longer knows when it saves and once a day.
    /// </summary>
    public class TraitChangeTracker : GameComponent
    {
        public List<TraitChangeRecord> changes = new List<TraitChangeRecord>();
        public List<TraitWatch> watches = new List<TraitWatch>();
        public int lastChange = -1;
        public HashSet<string> once = new HashSet<string>();

        public const int MaxChanges = 200, MaxPerPerson = 8;

        [Unsaved] private Dictionary<int, TraitWatch> byId;

        private static TraitChangeTracker cached;
        private static Game cachedGame;

        public static TraitChangeTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<TraitChangeTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public TraitChangeTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byId = null;
        }

        public TraitWatch Watch(Pawn p, bool create)
        {
            if (p == null) return null;
            if (byId == null)
            {
                byId = new Dictionary<int, TraitWatch>();
                foreach (var w in watches) byId[w.id] = w;
            }
            if (byId.TryGetValue(p.thingIDNumber, out var found) || !create) return found;
            found = new TraitWatch { id = p.thingIDNumber };
            watches.Add(found);
            byId[found.id] = found;
            return found;
        }

        /// <summary>When this person's trait (any trait, with null) last changed, or -1.</summary>
        public int LastChange(int pawn, string trait = null)
        {
            int last = -1;
            foreach (var c in changes)
                if (c.pawn == pawn && (trait == null || c.trait == trait) && c.tick > last) last = c.tick;
            return last;
        }

        public void Prune()
        {
            var known = CouncilGameComponent.KnownPawnNumbers();
            int now = Find.TickManager.TicksGame;
            watches.RemoveAll(w => !known.Contains(w.id));
            foreach (var w in watches)
            {
                w.tallies.RemoveAll(t => now - t.tick > TraitChanges.TallyKeepDays * GenDate.TicksPerDay);
                foreach (var g in w.tallies.GroupBy(t => t.key).Where(g => g.Count() > TraitWatch.MaxPerKey).ToList())
                    foreach (var old in g.OrderBy(t => t.tick).Take(g.Count() - TraitWatch.MaxPerKey).ToList())
                        w.tallies.Remove(old);
                while (w.snapshots.Count > TraitWatch.MaxSnapshots) w.snapshots.RemoveAt(0);
            }
            changes.RemoveAll(c => !known.Contains(c.pawn));
            foreach (var g in changes.GroupBy(c => c.pawn).Where(g => g.Count() > MaxPerPerson).ToList())
                foreach (var old in g.OrderBy(c => c.tick).Take(g.Count() - MaxPerPerson).ToList())
                    changes.Remove(old);
            if (changes.Count > MaxChanges) changes = changes.OrderByDescending(c => c.tick).Take(MaxChanges).ToList();
            byId = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trait changes clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref changes, "changes", LookMode.Deep);
            Scribe_Collections.Look(ref watches, "watches", LookMode.Deep);
            Scribe_Values.Look(ref lastChange, "lastChange", -1);
            Scribe_Collections.Look(ref once, "once", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                changes ??= new List<TraitChangeRecord>();
                changes.RemoveAll(c => c == null);
                watches ??= new List<TraitWatch>();
                watches.RemoveAll(w => w == null);
                once ??= new HashSet<string>();
                byId = null;
            }
        }
    }

    /// <summary>
    /// The game's traits can grow, appear or fade with what happens to a colonist (<see cref="TraitChangeDef"/>), and a
    /// change is always the visible end of a story the player can read: the marks behind it with their dates, the deeds
    /// counted, the records the game keeps. Rare by design: once a day the colony is looked over (CouncilDynamics, the
    /// second half of the council's day), at most one person changes, nobody twice in 60 days, the colony once in 10.
    /// A trait's nature is taken out of what life had moved them (so gaining kind after many kindnesses doesn't add its
    /// warmth again) and their character stays where it was. The letter, a journal line, the chronicle and the
    /// character page ("Traits gained and lost") tell it. Behind "Traits can change", under "Personalities and social
    /// life".
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TraitChanges
    {
        /// <summary>Deeds counted are kept this long.</summary>
        public const int TallyKeepDays = 240;

        private static TraitChangeTracker Tracker => TraitChangeTracker.Instance;
        private static int Now => Find.TickManager.TicksGame;

        static TraitChanges()
        {
            CharacterPage.Add("traits", 25, Section);
        }

        public static bool On =>
            Persona.On && (ColonyCouncilMod.Settings?.traitChanges ?? true) && Current.ProgramState == ProgramState.Playing && Find.TickManager != null
            && Tracker != null;

        /// <summary>Someone whose traits may change: a colonist of ours with an inner life, a teenager or older, at home or near.</summary>
        public static bool CanChange(Pawn p) =>
            p != null && !p.Dead && p.IsColonist && !p.IsSlave && p.story?.traits != null && Persona.Eligible(p) && People.HasInnerLife(p)
            && p.DevelopmentalStage.Adult() && !p.IsQuestLodger();

        // ---------------- What the deeds come from ----------------

        private static Dictionary<ThoughtDef, List<TraitTally>> byThought;
        private static Dictionary<HistoryEventDef, List<TraitTally>> byEvent;
        private static Dictionary<TaleDef, List<TraitTally>> byTale;
        private static List<string> paceRecords;
        private static int generation = -1;

        private static void Build()
        {
            if (byThought != null && generation == DefRefs.Generation) return;
            generation = DefRefs.Generation;
            byThought = new Dictionary<ThoughtDef, List<TraitTally>>();
            byEvent = new Dictionary<HistoryEventDef, List<TraitTally>>();
            byTale = new Dictionary<TaleDef, List<TraitTally>>();
            var records = new HashSet<string>();
            void Index<T>(Dictionary<T, List<TraitTally>> into, T def, TraitTally t) where T : Def
            {
                if (def == null) return;
                if (!into.TryGetValue(def, out var list)) into[def] = list = new List<TraitTally>();
                if (!list.Any(x => x.key == t.key && x.whileHungry == t.whileHungry)) list.Add(t);
            }
            foreach (var def in TraitChangeDef.All)
            {
                foreach (var t in def.tallies ?? new List<TraitTally>())
                {
                    if (t?.key == null) continue;
                    foreach (var n in t.Split(t.thoughts)) Index(byThought, DefDatabase<ThoughtDef>.GetNamedSilentFail(n), t);
                    foreach (var n in t.Split(t.events)) Index(byEvent, DefDatabase<HistoryEventDef>.GetNamedSilentFail(n), t);
                    foreach (var n in t.Split(t.tales)) Index(byTale, DefDatabase<TaleDef>.GetNamedSilentFail(n), t);
                }
                foreach (var p in def.pace ?? new List<TraitPace>())
                {
                    if (p?.record == null) continue;
                    records.Add(p.record);
                    if (!p.per.NullOrEmpty()) records.Add(p.per);
                }
            }
            paceRecords = records.Where(r => DefDatabase<RecordDef>.GetNamedSilentFail(r) != null).ToList();
        }

        /// <summary>A memory was just given to someone (every memory passes through here, so a dictionary first).</summary>
        public static void Notify_Memory(Pawn pawn, Thought_Memory m)
        {
            if (m?.def == null || pawn == null || !Persona.On) return;
            Build();
            if (!byThought.TryGetValue(m.def, out var list)) return;
            try
            {
                if (!On || m.pawn != pawn || !CanChange(pawn)) return;
                bool? hungry = null;
                foreach (var t in list)
                {
                    if (t.whileHungry && !(hungry ??= Hungry(pawn))) continue;
                    Count(pawn, t.key, null);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes from a memory", e);
            }
        }

        /// <summary>A deed went into the game's history (TraitChangePatches): counted for the one who did it.</summary>
        public static void Notify_Event(HistoryEvent ev)
        {
            if (ev.def == null || !Persona.On) return;
            Build();
            if (!byEvent.TryGetValue(ev.def, out var list)) return;
            try
            {
                if (!On || !ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer) || !CanChange(doer)) return;
                ev.args.TryGetArg(HistoryEventArgsNames.Victim, out Pawn victim);
                foreach (var t in list)
                    if (!t.whileHungry || Hungry(doer)) Count(doer, t.key, victim);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes from a deed", e);
            }
        }

        /// <summary>The day's new tales (an execution, told whether or not a DLC records it as a deed), once a day.</summary>
        private static void ReadTales()
        {
            if (byTale.Count == 0 || Find.TaleManager == null) return;
            foreach (var tale in Find.TaleManager.AllTalesListForReading)
            {
                if (tale?.def == null || tale.AgeTicks > GenDate.TicksPerDay + GenDate.TicksPerHour * 2 || !byTale.TryGetValue(tale.def, out var list)) continue;
                Pawn doer = null, victim = null;
                if (tale is Tale_DoublePawn two)
                {
                    doer = two.firstPawnData?.pawn;
                    victim = two.secondPawnData?.pawn;
                }
                else if (tale is Tale_SinglePawn one) doer = one.pawnData?.pawn;
                if (!CanChange(doer)) continue;
                foreach (var t in list)
                    if (!t.whileHungry || Hungry(doer)) Count(doer, t.key, victim, Now - tale.AgeTicks);
            }
        }

        /// <summary>Starving or malnourished now or lately, or in a hungry season.</summary>
        private static bool Hungry(Pawn p)
        {
            var food = p.needs?.food;
            if (food != null && (food.CurCategory >= HungerCategory.UrgentlyHungry || Now - food.lastNonStarvingTick < GenDate.TicksPerDay)) return true;
            if (p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition) != null) return true;
            return PersonaMarks.HasRecent(p, "HungrySeason", 20f);
        }

        private static void Count(Pawn p, string key, Pawn about, int tick = -1)
        {
            var w = Tracker?.Watch(p, true);
            if (w == null) return;
            if (tick < 0) tick = Now;
            int aboutId = about?.thingIDNumber ?? -1;
            var same = w.tallies.Where(t => t.key == key).ToList();
            if (!TraitChangeMath.SeparateTime(same.Select(t => t.tick).ToList(), same.Select(t => t.about).ToList(), tick, aboutId)) return;
            w.tallies.Add(new TallyEntry { key = key, tick = tick, about = aboutId, label = about?.LabelShort });
            if (same.Count + 1 > TraitWatch.MaxPerKey) w.tallies.Remove(same.OrderBy(t => t.tick).First());
        }

        // ---------------- Once a day ----------------

        /// <summary>
        /// Once a day: the watches are brought up to date (calm days, how time is spent, the day's tales), and when the
        /// colony hasn't seen a change in 10 days, each colonist's rules are checked; of those that hold and roll their
        /// chance, one happens.
        /// </summary>
        public static void Daily(CouncilGameComponent comp)
        {
            var t = Tracker;
            if (t == null) return;
            t.Prune();
            if (!On) return;
            Build();
            int now = Now;
            var people = PawnsFinder.AllMaps_FreeColonists.Where(CanChange).ToList();
            foreach (var p in people)
            {
                try
                {
                    Look(t.Watch(p, true), p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trait changes: daily look", e);
                }
            }
            try
            {
                ReadTales();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes: tales", e);
            }
            if (TraitChangeMath.RateBlocked(-1, t.lastChange, -1, 0, now) != null) return;
            var ready = new List<(Pawn p, TraitChangeDef def, TraitStep step)>();
            foreach (var p in people)
            {
                if (TraitChangeMath.RateBlocked(t.LastChange(p.thingIDNumber), -1, -1, 0, now) != null) continue;
                foreach (var def in TraitChangeDef.All)
                {
                    try
                    {
                        if (Why(p, def, now, out var step) == null && Rand.Chance(def.chance)) ready.Add((p, def, step));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("trait changes: " + def.defName, e);
                    }
                }
            }
            if (ready.Count == 0) return;
            var pick = ready.RandomElement();
            try
            {
                Apply(pick.p, pick.def, pick.step, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes: applying " + pick.def.defName, e);
            }
        }

        /// <summary>
        /// Dev mode: why each rule would or wouldn't change this colonist now (the rate limits apart), and with
        /// <paramref name="apply"/> the first that holds happens at once, chance and limits aside.
        /// </summary>
        public static string DebugFor(Pawn p, bool apply)
        {
            var t = Tracker;
            if (t == null || !On || !CanChange(p)) return "Traits can't change for " + (p?.LabelShort ?? "nobody") + " now (setting off, or not a colonist).";
            Build();
            int now = Now;
            Look(t.Watch(p, true), p, now);
            var sb = new StringBuilder();
            sb.Append(p.LabelShort).Append(": person ").Append(TraitChangeMath.RateBlocked(t.LastChange(p.thingIDNumber), -1, -1, 0, now) ?? "free")
                .Append(", colony ").Append(TraitChangeMath.RateBlocked(-1, t.lastChange, -1, 0, now) ?? "free");
            foreach (var def in TraitChangeDef.All)
            {
                string why = Why(p, def, now, out var step);
                sb.Append("\n  ").Append(def.defName).Append(": ").Append(why ?? "holds (" + (def.chance * 100f).ToString("0") + "% a day)");
                if (why == null && apply)
                {
                    Apply(p, def, step, now);
                    sb.Append(" - changed now");
                    break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Keeps up what a colonist is watched for: calm since when, and every ten days how their time is spent.</summary>
        private static void Look(TraitWatch w, Pawn p, int now)
        {
            if (w == null || p.records == null) return;
            float mental = p.records.GetValue(RecordDefOf.TimesInMentalState);
            if (w.calmSince < 0 || mental > w.mentalStates) w.calmSince = now;
            w.mentalStates = mental;
            if (paceRecords.Count == 0) return;
            var last = w.snapshots.Count > 0 ? w.snapshots[w.snapshots.Count - 1] : null;
            if (last != null && now - last.tick < TraitWatch.SnapshotDays * GenDate.TicksPerDay) return;
            var snap = new PaceSnapshot { tick = now };
            foreach (var name in paceRecords)
            {
                var rd = DefDatabase<RecordDef>.GetNamedSilentFail(name);
                if (rd != null) snap.values[name] = p.records.GetValue(rd);
            }
            w.snapshots.Add(snap);
            while (w.snapshots.Count > TraitWatch.MaxSnapshots) w.snapshots.RemoveAt(0);
        }

        // ---------------- Whether a rule holds ----------------

        /// <summary>Why this rule can't change this person now, or null if it can (with where it would take the trait).</summary>
        public static string Why(Pawn p, TraitChangeDef def, int now, out TraitStep step)
        {
            step = default;
            var t = Tracker;
            if (t == null || def == null || !def.Available) return "unavailable";
            if (!def.onceKey.NullOrEmpty() && t.once.Contains(def.onceKey)) return "once";
            var td = def.Trait;
            var traits = p.story?.traits;
            if (traits == null) return "no traits";
            if (TraitChangeMath.Untouchable(td.defName) != null) return "untouchable";
            if (def.blockedBy != null)
                foreach (var b in def.blockedBy)
                    if (traits.allTraits.Any(x => x.def.defName == b)) return "blocked by " + b;
            var cur = traits.GetTrait(td);
            bool has = cur != null;
            if (has && Protected(p, cur)) return "protected";
            var degrees = td.degreeDatas?.Select(d => d.degree).ToList() ?? new List<int>();
            step = TraitChangeMath.Plan(def.change, has, cur?.Degree ?? 0, def.degree, def.anyDegree, degrees, def.from);
            if (!step.ok) return "step " + step.why;
            if (!TraitChangeMath.RoomFor(step, traits.allTraits.Count)) return "no room";
            if (step.hasAfter && !Allowed(p, td, step.to, cur)) return "not allowed";
            if ((step.Removes || (has && step.hasAfter)) && HeldByRole(p)) return "role";
            int lastSame = t.LastChange(p.thingIDNumber, td.defName);
            string rate = TraitChangeMath.RateBlocked(-1, -1, lastSame, def.gapDays, now);
            if (rate != null) return rate;
            if (def.minAge > 0 && (p.ageTracker?.AgeBiologicalYears ?? 0) < def.minAge) return "age";
            if (def.daysHere > 0 && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) < def.daysHere * (float)GenDate.TicksPerDay) return "new";
            // Their character: how far life has moved them, and where they are.
            if (def.Moved.Count > 0 || def.Now.Count > 0 || def.marks.Count > 0 || !def.notMarks.NullOrEmpty())
            {
                var r = Persona.Of(p);
                if (r == null) return "no record";
                r.Fade(now);
                if (def.Moved.Count > 0)
                {
                    var moved = new float[PersonaMath.Axes];
                    for (int i = 0; i < moved.Length; i++) moved[i] = TraitChangeMath.Moved(r.lasting[i], r.drift[i]);
                    if (!MarksMath.Holds(def.Moved, moved)) return "moved";
                }
                if (def.Now.Count > 0 && !MarksMath.Holds(def.Now, Persona.Values(p))) return "now";
                foreach (var need in def.marks)
                    if (EvidenceMarks(r, need, lastSame, now).Count < need.count) return "marks";
                if (!def.notMarks.NullOrEmpty() && r.marks.Any(m => def.NotMark(m?.def) && now - m.tick < def.notMarksDays * GenDate.TicksPerDay)) return "not marks";
            }
            var w = t.Watch(p, false);
            foreach (var tally in def.tallies)
                if (Entries(w, tally, lastSame, now).Count < tally.count) return "tally " + tally.key;
            foreach (var f in def.Records)
            {
                var rd = DefDatabase<RecordDef>.GetNamedSilentFail(f.name);
                if (rd == null || p.records == null || !f.Holds(p.records.GetValue(rd))) return "record " + f.name;
            }
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var s = sd == null ? null : p.skills?.GetSkill(sd);
                if (s == null || s.TotallyDisabled || !f.Holds(s.Level)) return "skill " + f.name;
            }
            foreach (var pace in def.pace)
                if (!pace.Holds(Share(w, p, pace, now))) return "pace " + pace.record;
            if (def.calmDays > 0 && (w == null || TraitChangeMath.DaysSince(w.calmSince, now) < def.calmDays)) return "calm";
            if (def.meleeFighter && !MeleeFighter(p)) return "melee";
            return null;
        }

        /// <summary>A trait the game gave them for a reason of its own: from a gene, a backstory, the scenario, or suppressed.</summary>
        private static bool Protected(Pawn p, Trait tr)
        {
            if (tr.sourceGene != null || tr.suppressedByGene != null || tr.Suppressed || tr.ScenForced) return true;
            var stories = p.story?.AllBackstories;
            if (stories != null)
                foreach (var b in stories)
                    if (b?.forcedTraits != null && b.forcedTraits.Any(f => f.def == tr.def)) return true;
            return false;
        }

        /// <summary>The trait (at this degree) may be theirs: their backstory, genes, work and passions allow it, and it conflicts with nothing they have.</summary>
        private static bool Allowed(Pawn p, TraitDef td, int degree, Trait current)
        {
            var stories = p.story?.AllBackstories;
            if (stories != null)
                foreach (var b in stories)
                {
                    if (b == null) continue;
                    if (b.DisallowsTrait(td, degree)) return false;
                    if (b.forcedTraits != null && b.forcedTraits.Any(f => f.def == td && f.degree != degree)) return false;
                }
            var genes = p.genes?.GenesListForReading;
            if (genes != null)
                foreach (var g in genes)
                {
                    if (g?.def == null || !g.Active) continue;
                    if (g.def.suppressedTraits != null && g.def.suppressedTraits.Any(x => x.def == td)) return false;
                    if (g.def.forcedTraits != null && g.def.forcedTraits.Any(x => x.def == td)) return false;
                }
            if (td.requiredWorkTags != WorkTags.None && (p.CombinedDisabledWorkTags & td.requiredWorkTags) != WorkTags.None) return false;
            var skills = p.skills?.skills;
            if (skills != null)
                foreach (var s in skills)
                    if (s != null && People.Passionate(s) && td.ConflictsWithPassion(s.def)) return false;
            foreach (var other in p.story.traits.allTraits)
            {
                if (other == null || other == current || other.def == td) continue;
                if (td.ConflictsWith(other) || other.def.ConflictsWith(td)
                    || TraitChangeMath.Conflict(td.defName, td.conflictingTraits?.Select(x => x.defName).ToList(), td.exclusionTags,
                        other.def.defName, other.def.conflictingTraits?.Select(x => x.defName).ToList(), other.def.exclusionTags)) return false;
            }
            return true;
        }

        /// <summary>They hold a role in their faith whose requirements speak of traits (a mod's): their traits stay as they are.</summary>
        private static bool HeldByRole(Pawn p)
        {
            if (!ModsConfig.IdeologyActive) return false;
            var role = p.Ideo?.GetRole(p);
            var reqs = role?.def?.roleRequirements;
            return reqs != null && reqs.Any(r => r != null && r.GetType().Name.IndexOf("Trait", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool MeleeFighter(Pawn p)
        {
            var melee = p.skills?.GetSkill(SkillDefOf.Melee);
            var shooting = p.skills?.GetSkill(SkillDefOf.Shooting);
            if (melee == null || melee.TotallyDisabled) return false;
            if (shooting != null && !shooting.TotallyDisabled && melee.Level < shooting.Level + 2) return false;
            var weapon = p.equipment?.Primary;
            return weapon == null || weapon.def.IsMeleeWeapon;
        }

        /// <summary>The marks behind it: those that match, since this trait last changed, within the days asked, oldest first.</summary>
        private static List<PersonaMark> EvidenceMarks(PawnPersona r, TraitMarkNeed need, int after, int now) =>
            r.marks.Where(m => m?.def != null && m.tick > after && need.Matches(m.def) && (need.withinDays <= 0 || now - m.tick < need.withinDays * GenDate.TicksPerDay))
                .OrderBy(m => m.tick).ToList();

        private static List<TallyEntry> Entries(TraitWatch w, TraitTally tally, int after, int now)
        {
            if (w == null) return new List<TallyEntry>();
            float within = tally.withinDays > 0 ? tally.withinDays : TallyKeepDays;
            return w.tallies.Where(e => e.key == tally.key && e.tick > after && now - e.tick < within * GenDate.TicksPerDay).OrderBy(e => e.tick).ToList();
        }

        private static float Share(TraitWatch w, Pawn p, TraitPace pace, int now)
        {
            if (w == null || p.records == null) return -1f;
            var num = DefDatabase<RecordDef>.GetNamedSilentFail(pace.record);
            var den = DefDatabase<RecordDef>.GetNamedSilentFail(pace.per ?? "TimeAsColonistOrColonyAnimal");
            if (num == null || den == null) return -1f;
            var ticks = new List<int>();
            var nums = new List<float>();
            var dens = new List<float>();
            foreach (var s in w.snapshots)
            {
                if (!s.values.TryGetValue(num.defName, out float a) || !s.values.TryGetValue(den.defName, out float b)) continue;
                ticks.Add(s.tick);
                nums.Add(a);
                dens.Add(b);
            }
            return TraitChangeMath.Pace(ticks, nums, dens, now, p.records.GetValue(num), p.records.GetValue(den), pace.days);
        }

        // ---------------- Making the change ----------------

        private static string DegreeLabel(TraitDef td, int degree, Pawn p)
        {
            var d = td.degreeDatas?.FirstOrDefault(x => x.degree == degree);
            return d != null ? d.GetLabelFor(p) : td.label;
        }

        private static void Apply(Pawn p, TraitChangeDef def, TraitStep step, int now)
        {
            var t = Tracker;
            var td = def.Trait;
            var traits = p.story.traits;
            // The story first, while the marks behind it are as they were.
            string evidence = Evidence(p, def, now);
            string oldLabel = step.hadTrait ? DegreeLabel(td, step.from, p) : null;
            string newLabel = step.hasAfter ? DegreeLabel(td, step.to, p) : null;
            // Their nature fresh (it's kept for an hour), so the difference is this trait's alone.
            Persona.ResetCaches();
            var before = (float[])Persona.Nature(p).Clone();
            var cur = traits.GetTrait(td);
            if (cur != null) traits.RemoveTrait(cur);
            if (step.hasAfter) traits.GainTrait(new Trait(td, step.to, false));
            p.Notify_DisabledWorkTypesChanged();
            Persona.ResetCaches();
            PersonaLean.Reset();
            var after = Persona.Nature(p);
            var delta = new float[PersonaMath.Axes];
            for (int i = 0; i < delta.Length; i++) delta[i] = after[i] - before[i];
            var r = Persona.Of(p, true);
            if (r != null)
            {
                r.Fade(now);
                TraitChangeMath.Absorb(r.lasting, r.drift, delta, PersonaMath.MaxLasting, PersonaMath.MaxDrift);
            }
            string summary = newLabel != null && oldLabel != null ? "CC_TC_Became".Translate(oldLabel, newLabel).Resolve()
                : newLabel != null ? "CC_TC_Gained".Translate(newLabel).Resolve() : "CC_TC_Lost".Translate(oldLabel).Resolve();
            string title = newLabel != null ? "CC_TC_TitleNew".Translate(newLabel.CapitalizeFirst(), p.Named("PAWN")).Resolve()
                : "CC_TC_TitleLost".Translate(oldLabel, p.Named("PAWN")).Resolve();
            var traitArg = (newLabel ?? oldLabel ?? td.label).Named("TRAIT");
            // A step that takes the trait away is told as a loss ("is no longer nervous"), with no meaning written for the trait it ends.
            bool endsAtNone = def.IsStep && !step.hasAfter;
            string newsText = endsAtNone ? def.newsLost.NullOrEmpty() ? "CC_TC_NewsLost".Translate().ToString() : def.newsLost : def.news;
            string news = newsText.Formatted(p.Named("PAWN"), traitArg).Resolve();
            var body = new StringBuilder(news);
            if (!endsAtNone && !def.meaning.NullOrEmpty()) body.Append(" ").Append(def.meaning.Formatted(p.Named("PAWN"), traitArg).Resolve());
            if (!evidence.NullOrEmpty()) body.Append("\n\n").Append("CC_TC_LedTo".Translate()).Append("\n").Append(evidence);
            body.Append("\n\n").Append("CC_TC_Absorbed".Translate(p.Named("PAWN")).Resolve());
            var record = new TraitChangeRecord
            {
                pawn = p.thingIDNumber, name = p.LabelShort, tick = now, rule = def.defName, trait = td.defName, hadTrait = step.hadTrait, hasAfter = step.hasAfter,
                from = step.from, to = step.to, summary = summary, story = body.ToString(), absorbed = delta.ToList()
            };
            t.changes.Add(record);
            t.lastChange = now;
            if (!def.onceKey.NullOrEmpty()) t.once.Add(def.onceKey);
            try
            {
                Tell(p, def, record, traitArg, news, title);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait change told: " + def.defName, e);
            }
        }

        /// <summary>The letter (good, bad or neither), a line in their journal in their own voice, and the chronicle.</summary>
        private static void Tell(Pawn p, TraitChangeDef def, TraitChangeRecord record, NamedArgument traitArg, string news, string title)
        {
            var letterDef = def.tone > 0 ? LetterDefOf.PositiveEvent : def.tone < 0 ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
            Find.LetterStack.ReceiveLetter(title, record.story, letterDef, new LookTargets(p));
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            if (def.journal != null && def.journal.Any)
            {
                string raw = def.journal.Pick(ToneUtility.ToneFor(p), Flavor.TagsFor(p));
                if (!raw.NullOrEmpty()) comp.journals.WriteText(p, raw.Formatted(p.Named("PAWN"), traitArg).Resolve());
            }
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, news, p));
        }

        /// <summary>What led to it, one line each, with dates: the marks, the deeds, the records, the stretch, the calm, how far they'd moved.</summary>
        private static string Evidence(Pawn p, TraitChangeDef def, int now)
        {
            var t = Tracker;
            int lastSame = t.LastChange(p.thingIDNumber, def.trait);
            var r = Persona.Of(p);
            var ordered = new List<string>();
            if (r != null)
            {
                // The marks, in the order they happened.
                var seen = new HashSet<PersonaMark>();
                foreach (var need in def.marks)
                    foreach (var m in EvidenceMarks(r, need, lastSame, now))
                        seen.Add(m);
                foreach (var m in seen.OrderBy(m => m.tick))
                {
                    string text = Persona.Shaped(p, m);
                    if (!text.NullOrEmpty()) ordered.Add(PersonaMarks.DateLabel(m.tick, p) + ": " + text);
                }
            }
            var w = t.Watch(p, false);
            foreach (var tally in def.tallies)
            {
                var entries = Entries(w, tally, lastSame, now);
                if (entries.Count == 0) continue;
                var when = entries.Select(e => PersonaMarks.DateLabel(e.tick, p) + (e.label.NullOrEmpty() ? "" : " (" + e.label + ")")).ToList();
                ordered.Add(tally.label.CapitalizeFirst() + ": " + string.Join(", ", when) + ".");
            }
            foreach (var f in def.Records)
            {
                var rd = DefDatabase<RecordDef>.GetNamedSilentFail(f.name);
                if (rd == null || p.records == null) continue;
                ordered.Add("CC_TC_Record".Translate(rd.LabelCap, Mathf.RoundToInt(p.records.GetValue(rd))).Resolve());
            }
            foreach (var pace in def.pace)
            {
                float share = Share(w, p, pace, now);
                if (share < 0f) continue;
                ordered.Add("CC_TC_Pace".Translate((pace.label ?? pace.record).CapitalizeFirst(), Mathf.RoundToInt(share * 100f), pace.days).Resolve());
            }
            if (def.calmDays > 0 && w != null) ordered.Add("CC_TC_Calm".Translate(Mathf.FloorToInt(TraitChangeMath.DaysSince(w.calmSince, now))).Resolve());
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var s = sd == null ? null : p.skills?.GetSkill(sd);
                if (s != null) ordered.Add("CC_TC_Skill".Translate(sd.LabelCap, s.Level).Resolve());
            }
            if (r != null)
            {
                var axes = def.Moved.Select(c => c.axis).Concat(def.Now.Select(c => c.axis)).Distinct().OrderBy(a => a).ToList();
                var values = Persona.Values(p);
                foreach (int a in axes)
                {
                    float moved = TraitChangeMath.Moved(r.lasting[a], r.drift[a]);
                    ordered.Add("CC_TC_Side".Translate(("CC_Persona_Axis_" + (PersonaAxis)a).Translate().CapitalizeFirst(), Signed(moved), Signed(values[a]),
                        Persona.Word((PersonaAxis)a, values[a]) ?? "CC_Char_Neither".Translate().ToString()).Resolve());
                }
            }
            return string.Join("\n", ordered.Select(l => "  - " + l));
        }

        private static string Signed(float v)
        {
            int n = Mathf.RoundToInt(v);
            return n > 0 ? "+" + n : n.ToString();
        }

        // ---------------- The character page ----------------

        /// <summary>Section 25: the traits they gained and lost, newest first; hover one for the story behind it.</summary>
        public static CharacterSection Section(Pawn p)
        {
            var t = Tracker;
            if (t == null || p == null) return null;
            var mine = t.changes.Where(c => c.pawn == p.thingIDNumber).OrderByDescending(c => c.tick).ToList();
            if (mine.Count == 0) return null;
            var s = new CharacterSection { title = "CC_TC_Section".Translate() };
            foreach (var c in mine)
                s.Line(PersonaMarks.DateLabel(c.tick, p).Colorize(UiKit.Dim) + "  " + (c.summary ?? c.trait).CapitalizeFirst(), c.story);
            return s;
        }

        /// <summary>
        /// For the tooltip of one side on the character page: how much of it went into traits gained or came back from
        /// traits lost (so the reader sees why life's move looks smaller than its marks), or null.
        /// </summary>
        public static string AbsorbedNote(Pawn p, PersonaAxis axis)
        {
            var t = Tracker;
            if (t == null || p == null) return null;
            int i = (int)axis;
            var parts = t.changes.Where(c => c.pawn == p.thingIDNumber && c.absorbed != null && i < c.absorbed.Count && Mathf.Abs(c.absorbed[i]) >= 0.5f)
                .OrderBy(c => c.tick).Select(c => "  " + Signed(c.absorbed[i]) + "  " + (c.summary ?? c.trait) + " (" + PersonaMarks.DateLabel(c.tick, p) + ")")
                .ToList();
            return parts.Count == 0 ? null : "CC_TC_AbsorbedNote".Translate() + "\n" + string.Join("\n", parts);
        }
    }
}
