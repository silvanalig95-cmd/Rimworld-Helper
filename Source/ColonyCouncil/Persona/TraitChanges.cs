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
        /// <summary>The loss behind a trait that came of an unpaid vow of vengeance (the lost one's id, or name), or null.</summary>
        public string about;

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
            Scribe_Values.Look(ref about, "about");
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
    /// second half of the council's day), at most one person changes, nobody twice in 60 days (unless the change takes
    /// away a trait Rimfolk gave them), the colony once in 10.
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
            CharacterPage.Add("traitsBuilding", 26, Building);
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
                // Within their 60 days, only a change that takes away a trait Rimfolk gave them.
                bool waiting = TraitChangeMath.RateBlocked(t.LastChange(p.thingIDNumber), -1, -1, 0, now) != null;
                foreach (var def in TraitChangeDef.All)
                {
                    if (waiting && def.change == TraitChangeKind.Gain) continue;
                    try
                    {
                        if (Why(p, def, now, out var step) == null && (!waiting || Undoes(p, def, step)) && Rand.Chance(def.chance)) ready.Add((p, def, step));
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
            string gate = Gate(p, def, now, out step);
            return gate ?? Lacks(p, def, now);
        }

        /// <summary>
        /// What stands in the way whatever they live through: the rule unavailable or used, a trait that blocks it, the
        /// degree it would step to, room for a trait, conflicts, a role, the last change of this trait too recent, age
        /// and time in the colony. Null when only the evidence is missing (or nothing).
        /// </summary>
        private static string Gate(Pawn p, TraitChangeDef def, int now, out TraitStep step)
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
            if (def.onlyGiven && !GaveIt(p, td.defName)) return "not given";
            int lastSame = t.LastChange(p.thingIDNumber, td.defName);
            string rate = TraitChangeMath.RateBlocked(-1, -1, lastSame, def.gapDays, now);
            if (rate != null) return rate;
            if (def.minAge > 0 && (p.ageTracker?.AgeBiologicalYears ?? 0) < def.minAge) return "age";
            if (def.daysHere > 0 && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) < def.daysHere * (float)GenDate.TicksPerDay) return "new";
            if (def.noRival && HasRival(p)) return "rival";
            return null;
        }

        /// <summary>
        /// Rimfolk gave them this trait: the newest of its changes kept for them added it (a trait they came with, or one a
        /// change already took away, isn't Rimfolk's to undo).
        /// </summary>
        public static bool GaveIt(Pawn p, string trait)
        {
            var t = Tracker;
            if (t == null || p == null || trait == null) return false;
            var mine = t.changes.Where(c => c.pawn == p.thingIDNumber && c.trait == trait).ToList();
            return TraitChangeMath.GaveIt(mine.Select(c => c.tick).ToList(), mine.Select(c => c.hasAfter && !c.hadTrait).ToList());
        }

        /// <summary>The change takes away a trait Rimfolk gave them: the person's 60 days don't hold it back.</summary>
        private static bool Undoes(Pawn p, TraitChangeDef def, TraitStep step) => step.Removes && GaveIt(p, def.Trait?.defName);

        /// <summary>
        /// A rival among the colony's people (colonists, slaves and prisoners): a rivalry on the Bonds page, or the game's
        /// opinion of -20 or less, which is what the game calls a rival and what vengeful reads.
        /// </summary>
        private static bool HasRival(Pawn p)
        {
            if (p?.relations == null) return false;
            var social = CouncilGameComponent.Instance?.social;
            var others = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.Concat(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony);
            foreach (var o in others)
            {
                if (o == null || o == p || !o.RaceProps.Humanlike) continue;
                bool tie = social != null && social.ties.Any(x => x.kind == TieKind.Rivals && x.announced && x.Between(p, o));
                if (TraitChangeMath.IsRival(p.relations.OpinionOf(o), tie)) return true;
            }
            return false;
        }

        // ---------------- Vows of vengeance ----------------

        /// <summary>Their vows of vengeance as the rules read them, with the vendettas behind them (none with vendettas switched off).</summary>
        private static List<(Vendetta v, TraitChangeMath.Vow vow)> Vows(Pawn p)
        {
            var list = new List<(Vendetta, TraitChangeMath.Vow)>();
            var t = CouncilGameComponent.Instance?.vendettas;
            if (t == null || p == null || ColonyCouncilMod.Settings?.vendettas != true) return list;
            foreach (var v in t.vendettas)
                if (v != null && v.avenger == p) list.Add((v, VowOf(v)));
            return list;
        }

        private static TraitChangeMath.Vow VowOf(Vendetta v) =>
            new TraitChangeMath.Vow(v.tick, v.Active, v.faction != null && v.faction.HostileTo(Faction.OfPlayer), v.kills, LossOf(v));

        /// <summary>The loss a vow was sworn for: the lost one's id, or their name in an older save.</summary>
        private static string LossOf(Vendetta v) => v.victimId.NullOrEmpty() ? v.victim : v.victimId;

        /// <summary>
        /// The losses that have already made someone take this trait, one person per loss: kept with each change, or, for a
        /// change from before that was kept (an older save), the losses of the vows that person had sworn by then.
        /// </summary>
        private static HashSet<string> LossesTaken(string trait)
        {
            var set = new HashSet<string>();
            var t = Tracker;
            if (t == null) return set;
            var vendettas = CouncilGameComponent.Instance?.vendettas?.vendettas;
            foreach (var c in t.changes)
            {
                if (c.trait != trait || c.hadTrait || !c.hasAfter) continue;
                if (!c.about.NullOrEmpty())
                {
                    set.Add(c.about);
                    continue;
                }
                if (vendettas == null || DefDatabase<TraitChangeDef>.GetNamedSilentFail(c.rule ?? "")?.vendetta != TraitChangeDef.VendettaUnpaid) continue;
                var theirs = vendettas.Where(v => v?.avenger != null && v.avenger.thingIDNumber == c.pawn).Select(VowOf).ToList();
                foreach (var loss in TraitChangeMath.LossesBehind(theirs, c.tick)) set.Add(loss);
            }
            return set;
        }

        /// <summary>The vow that has stood longest unpaid for a loss that has made nobody take this trait yet, or null; and how long.</summary>
        private static Vendetta UnpaidVow(Pawn p, TraitChangeDef def, int now, out float days)
        {
            var vows = Vows(p);
            int i = TraitChangeMath.Unpaid(vows.Select(x => x.vow).ToList(), now, LossesTaken(def.trait), out days);
            return i >= 0 ? vows[i].v : null;
        }

        /// <summary>No vow of theirs still standing.</summary>
        private static bool VowsOver(Pawn p) => !TraitChangeMath.AnyStanding(Vows(p).Select(x => x.vow).ToList());

        /// <summary>The rule's vow of vengeance holds: one unpaid long enough, or none left standing.</summary>
        private static bool VowHolds(Pawn p, TraitChangeDef def, int now)
        {
            if (def.vendetta == TraitChangeDef.VendettaOver) return VowsOver(p);
            return UnpaidVow(p, def, now, out float days) != null && days >= def.vendettaDays;
        }

        /// <summary>Calm days as the rule counts them: since the last breakdown, and with calmSinceChange since this trait last changed too.</summary>
        private static float CalmDays(TraitWatch w, TraitChangeDef def, int lastSame, int now) =>
            w == null ? 0f : TraitChangeMath.CalmDays(w.calmSince, lastSame, now, def.calmSinceChange);

        /// <summary>The first piece of evidence the rule still lacks, or null when all of it is there.</summary>
        private static string Lacks(Pawn p, TraitChangeDef def, int now)
        {
            var t = Tracker;
            int lastSame = t.LastChange(p.thingIDNumber, def.Trait.defName);
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
            if (def.calmDays > 0 && CalmDays(w, def, lastSame, now) < def.calmDays) return "calm";
            if (!def.vendetta.NullOrEmpty() && !VowHolds(p, def, now)) return "vendetta";
            if (def.meleeFighter && !MeleeFighter(p)) return "melee";
            foreach (var f in def.Practice)
                if (!f.Holds(Practice.Hours(p, f.name))) return "practice " + f.name;
            if (def.Habit != null)
            {
                var q = HabitOf(p, def);
                if (q == null) return "habit";
                if (TraitChangeMath.DaysSince(q.gainedTick, now) < def.habitDays) return "habit days";
                if (q.livedDays < def.habitLived) return "habit lived";
            }
            return null;
        }

        /// <summary>Their habit that the rule grows from (the quirks setting on), or null.</summary>
        private static Quirk HabitOf(Pawn p, TraitChangeDef def) =>
            def.Habit == null || ColonyCouncilMod.Settings?.quirks != true ? null : CouncilGameComponent.Instance?.quirks?.Get(p, def.Habit);

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
            // The loss behind a trait that came of an unpaid vow: no one else takes it for the same loss.
            var vow = def.vendetta == TraitChangeDef.VendettaUnpaid ? UnpaidVow(p, def, now, out _) : null;
            string about = vow != null ? LossOf(vow) : null;
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
            body.Append("\n\n").Append((step.hasAfter ? "CC_TC_Absorbed" : "CC_TC_AbsorbedLost").Translate(p.Named("PAWN")).Resolve());
            var record = new TraitChangeRecord
            {
                pawn = p.thingIDNumber, name = p.LabelShort, tick = now, rule = def.defName, trait = td.defName, hadTrait = step.hadTrait, hasAfter = step.hasAfter,
                from = step.from, to = step.to, summary = summary, story = body.ToString(), absorbed = delta.ToList(), about = about
            };
            t.changes.Add(record);
            t.lastChange = now;
            // A habit that grew into the trait is the trait now.
            if (def.Habit != null && def.endsHabit) CouncilGameComponent.Instance?.quirks?.EndHabit(p, def.Habit);
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
            ColonyCouncil.Tell.Letter(NewsKind.People, Importance.Notable, null, title, record.story, letterDef, new LookTargets(p));
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            if (def.journal != null && def.journal.Any)
            {
                string raw = def.journal.Pick(ToneUtility.ToneFor(p), Flavor.TagsFor(p), p);
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
            if (def.calmDays > 0 && w != null) ordered.Add("CC_TC_Calm".Translate(Mathf.FloorToInt(CalmDays(w, def, lastSame, now))).Resolve());
            ordered.AddRange(VowLines(p, def, lastSame, now));
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var s = sd == null ? null : p.skills?.GetSkill(sd);
                if (s != null) ordered.Add("CC_TC_Skill".Translate(sd.LabelCap, s.Level).Resolve());
            }
            foreach (var f in def.Practice)
                ordered.Add("CC_TC_Practice".Translate(Practice.Words(f.name).CapitalizeFirst(), Mathf.RoundToInt(Practice.Hours(p, f.name))).Resolve());
            var habit = HabitOf(p, def);
            if (habit != null)
                ordered.Add("CC_TC_Habit".Translate(habit.Label, Mathf.FloorToInt(TraitChangeMath.DaysSince(habit.gainedTick, now)), habit.livedDays).Resolve());
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

        /// <summary>
        /// The vows behind a rule, for the letter: the unpaid one and how long it has stood; or, for vows that are over, how
        /// each ended since the trait came (or that nothing is left standing).
        /// </summary>
        private static List<string> VowLines(Pawn p, TraitChangeDef def, int lastSame, int now)
        {
            var lines = new List<string>();
            if (def.vendetta == TraitChangeDef.VendettaUnpaid)
            {
                var v = UnpaidVow(p, def, now, out float days);
                if (v != null)
                    lines.Add("CC_TC_VowUnpaid".Translate(Mathf.FloorToInt(days), v.faction?.Name ?? "?", v.victim ?? "?").Resolve());
                return lines;
            }
            if (def.vendetta != TraitChangeDef.VendettaOver) return lines;
            foreach (var (v, vow) in Vows(p).OrderBy(x => x.v.tick))
            {
                if (vow.Standing || !EndedSince(v, vow, lastSame)) continue;
                string line = v.LineKey.Translate(p.LabelShort, v.faction?.Name ?? "?", v.victim ?? "?", v.kills, Vendetta.KillsToSettle).Resolve();
                if (vow.open && !vow.atWar) line += " " + "CC_TC_VowPeace".Translate(v.faction?.Name ?? "?").Resolve();
                lines.Add(line);
            }
            if (lines.Count == 0) lines.Add("CC_TC_VowNone".Translate(p.Named("PAWN")).Resolve());
            return lines;
        }

        /// <summary>
        /// A vow that ended (or whose people made peace) since the trait came: settled after that, or open with that people at
        /// peace; one settled before this was kept (an older save) counts if it was sworn after, or less than 120 days before.
        /// </summary>
        private static bool EndedSince(Vendetta v, TraitChangeMath.Vow vow, int since)
        {
            if (vow.open) return !vow.atWar;
            if (v.settledTick >= 0) return v.settledTick >= since;
            return v.tick > since - 120 * GenDate.TicksPerDay;
        }

        private static string Signed(float v)
        {
            int n = Mathf.RoundToInt(v);
            return n > 0 ? "+" + n : n.ToString();
        }

        // ---------------- Where life is taking them ----------------

        /// <summary>
        /// What leads to a rule's change, part by part, in words, and whether each part holds: how far a side has moved
        /// and where it stands, the marks, the deeds, the records, skills, how their time goes, calm days, a habit, what
        /// they've been doing lately. The same evidence the rule checks (Lacks), told for the player.
        /// </summary>
        public static List<(string text, bool ok)> Parts(Pawn p, TraitChangeDef def, int now)
        {
            var parts = new List<(string, bool)>();
            var t = Tracker;
            if (t == null || def?.Trait == null) return parts;
            int lastSame = t.LastChange(p.thingIDNumber, def.Trait.defName);
            var r = Persona.Of(p);
            if (r != null && (def.Moved.Count > 0 || def.Now.Count > 0))
            {
                r.Fade(now);
                var moved = new float[PersonaMath.Axes];
                for (int i = 0; i < moved.Length; i++) moved[i] = TraitChangeMath.Moved(r.lasting[i], r.drift[i]);
                foreach (var c in def.Moved)
                    parts.Add(("CC_TCB_Moved".Translate(AxisName(c.axis), Signed(moved[c.axis]), Needs(c.atLeast, c.value, true)).Resolve(), c.Holds(moved)));
                var values = Persona.Values(p);
                foreach (var c in def.Now)
                    parts.Add(("CC_TCB_Now".Translate(AxisName(c.axis), Signed(values[c.axis]), Needs(c.atLeast, c.value, true)).Resolve(), c.Holds(values)));
            }
            foreach (var need in def.marks)
            {
                int have = r == null ? 0 : EvidenceMarks(r, need, lastSame, now).Count;
                string names = MarkNames(need);
                string text = names.NullOrEmpty() ? "CC_TCB_Marks".Translate(Mathf.Min(have, need.count), need.count).Resolve()
                    : "CC_TCB_MarksNamed".Translate(Mathf.Min(have, need.count), need.count, names).Resolve();
                parts.Add((text, have >= need.count));
            }
            var w = t.Watch(p, false);
            foreach (var tally in def.tallies)
            {
                int have = Entries(w, tally, lastSame, now).Count;
                parts.Add(("CC_TCB_Count".Translate((tally.label ?? tally.key).CapitalizeFirst(), Mathf.Min(have, tally.count), tally.count).Resolve(), have >= tally.count));
            }
            foreach (var f in def.Records)
            {
                var rd = DefDatabase<RecordDef>.GetNamedSilentFail(f.name);
                if (rd == null || p.records == null) continue;
                float v = p.records.GetValue(rd);
                parts.Add(("CC_TCB_Fact".Translate(rd.LabelCap, Mathf.RoundToInt(v), Needs(f.atLeast, f.value, false)).Resolve(), f.Holds(v)));
            }
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var sk = sd == null ? null : p.skills?.GetSkill(sd);
                if (sk == null) continue;
                parts.Add(("CC_TCB_Fact".Translate(sd.LabelCap, sk.Level, Needs(f.atLeast, f.value, false)).Resolve(), !sk.TotallyDisabled && f.Holds(sk.Level)));
            }
            foreach (var pace in def.pace)
            {
                float share = Share(w, p, pace, now);
                string needs = pace.atLeast >= 0f ? "CC_TCB_AtLeast".Translate(Mathf.RoundToInt(pace.atLeast * 100f)).Resolve() : "CC_TCB_AtMost".Translate(Mathf.RoundToInt(pace.atMost * 100f)).Resolve();
                parts.Add(("CC_TCB_Pace".Translate((pace.label ?? pace.record).CapitalizeFirst(), Mathf.RoundToInt(Mathf.Max(0f, share) * 100f), needs).Resolve(), pace.Holds(share)));
            }
            if (def.calmDays > 0)
            {
                int days = Mathf.FloorToInt(CalmDays(w, def, lastSame, now));
                string text = def.calmSinceChange
                    ? "CC_TCB_CalmSince".Translate(Mathf.Min(days, def.calmDays), def.calmDays, TraitWord(def, p, true)).Resolve()
                    : "CC_TCB_Calm".Translate(Mathf.Min(days, def.calmDays), def.calmDays).Resolve();
                parts.Add((text, days >= def.calmDays));
            }
            if (def.vendetta == TraitChangeDef.VendettaUnpaid)
            {
                UnpaidVow(p, def, now, out float unpaid);
                int days = Mathf.FloorToInt(unpaid);
                parts.Add(("CC_TCB_VowUnpaid".Translate(Mathf.Min(days, def.vendettaDays), def.vendettaDays).Resolve(), days >= def.vendettaDays));
            }
            else if (def.vendetta == TraitChangeDef.VendettaOver) parts.Add(("CC_TCB_VowOver".Translate().ToString(), VowsOver(p)));
            if (def.meleeFighter) parts.Add(("CC_TCB_Melee".Translate().ToString(), MeleeFighter(p)));
            foreach (var f in def.Practice)
            {
                float h = Practice.Hours(p, f.name);
                parts.Add(("CC_TCB_Practice".Translate(Practice.Words(f.name).CapitalizeFirst(), Mathf.RoundToInt(h), Needs(f.atLeast, f.value, false)).Resolve(), f.Holds(h)));
            }
            if (def.Habit != null)
            {
                var q = HabitOf(p, def);
                if (q == null) parts.Add(("CC_TCB_NoHabit".Translate(def.Habit.label).Resolve(), false));
                else
                {
                    int held = Mathf.FloorToInt(TraitChangeMath.DaysSince(q.gainedTick, now));
                    parts.Add(("CC_TCB_HabitHeld".Translate(q.Label, Mathf.Min(held, def.habitDays), def.habitDays).Resolve(), held >= def.habitDays));
                    parts.Add(("CC_TCB_HabitLived".Translate(q.Label, Mathf.Min(q.livedDays, def.habitLived), def.habitLived).Resolve(), q.livedDays >= def.habitLived));
                }
            }
            return parts;
        }

        /// <summary>The marks a need counts, by their names on the character page: "a kindness remembered, forgiven, ..." (twelve at most).</summary>
        private static string MarkNames(TraitMarkNeed need)
        {
            var labels = new List<string>();
            foreach (var def in DefDatabase<PersonaMarkDef>.AllDefsListForReading)
                if (need.Matches(def) && !def.label.NullOrEmpty() && !labels.Contains(def.label)) labels.Add(def.label);
            if (labels.Count > 12) labels = labels.Take(11).Append("CC_TCB_MarksMore".Translate().ToString()).ToList();
            return string.Join(", ", labels);
        }

        private static string AxisName(int axis) => ("CC_Persona_Axis_" + (PersonaAxis)axis).Translate().CapitalizeFirst();

        /// <summary>"+16 or more", "10 or less".</summary>
        private static string Needs(bool atLeast, float value, bool signed)
        {
            string v = signed ? Signed(value) : Mathf.RoundToInt(value).ToString();
            return (atLeast ? "CC_TCB_OrMore" : "CC_TCB_OrLess").Translate(v).Resolve();
        }

        /// <summary>Most rules shown at once, and the share of a rule's parts that must hold before it's shown.</summary>
        public const int BuildingShown = 3;
        public const float BuildingFrom = 0.34f;

        private static readonly Dictionary<int, (int tick, CharacterSection section)> buildingCache = new Dictionary<int, (int, CharacterSection)>();

        /// <summary>
        /// Section 26: the changes their life is heading toward, the furthest along first, each with how many of the
        /// things that lead there have happened; hover one for the list. Only rules nothing else stands in the way of
        /// (the gate), with at least a third of their evidence there; a way out of a trait Rimfolk gave them is shown from
        /// the start, so the player can see what would undo it. Kept a few hours of game time.
        /// </summary>
        public static CharacterSection Building(Pawn p)
        {
            if (!On || !CanChange(p)) return null;
            int now = Now;
            if (buildingCache.TryGetValue(p.thingIDNumber, out var cached) && now >= cached.tick && now - cached.tick < GenDate.TicksPerHour * 2) return cached.section;
            if (buildingCache.Count > 60) buildingCache.Clear();
            Build();
            var rows = new List<(TraitChangeDef def, int have, int total, List<(string text, bool ok)> parts)>();
            foreach (var def in TraitChangeDef.All)
            {
                try
                {
                    if (Gate(p, def, now, out _) != null) continue;
                    var parts = Parts(p, def, now);
                    int have = parts.Count(x => x.ok);
                    if (parts.Count == 0 || (!def.onlyGiven && (have == 0 || have < parts.Count * BuildingFrom))) continue;
                    rows.Add((def, have, parts.Count, parts));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trait changes: where life is taking them, " + def.defName, e);
                }
            }
            CharacterSection s = null;
            if (rows.Count > 0)
            {
                s = new CharacterSection { title = "CC_TCB_Section".Translate() };
                foreach (var row in rows.OrderByDescending(x => x.have / (float)x.total).ThenByDescending(x => x.have).Take(BuildingShown))
                {
                    string count = row.have >= row.total ? "CC_TCB_All".Translate().ToString() : "CC_TCB_Of".Translate(row.have, row.total).Resolve();
                    var tip = new StringBuilder("CC_TCB_Tip".Translate(TraitWord(row.def, p)).Resolve());
                    foreach (var (text, ok) in row.parts)
                        tip.Append("\n").Append(("• " + text).Colorize(ok ? UiKit.Good : UiKit.Dim));
                    tip.Append("\n\n").Append("CC_TCB_TipEnd".Translate());
                    s.Line(row.def.label.CapitalizeFirst() + "  " + count.Colorize(UiKit.Dim), tip.ToString());
                }
            }
            buildingCache[p.thingIDNumber] = (now, s);
            return s;
        }

        /// <summary>
        /// The trait a rule leads to, as the player would read it: "kind", "no longer abrasive", "steadier nerves"; with
        /// <paramref name="now"/>, the trait as they have it now ("vengeful").
        /// </summary>
        private static string TraitWord(TraitChangeDef def, Pawn p, bool now = false)
        {
            var td = def.Trait;
            var cur = p.story?.traits?.GetTrait(td);
            if (now) return cur != null ? DegreeLabel(td, cur.Degree, p) : td.label;
            var degrees = td.degreeDatas?.Select(d => d.degree).ToList() ?? new List<int>();
            var step = TraitChangeMath.Plan(def.change, cur != null, cur?.Degree ?? 0, def.degree, def.anyDegree, degrees, def.from);
            if (step.ok && step.hasAfter) return DegreeLabel(td, step.to, p);
            if (step.ok && step.hadTrait) return "CC_TCB_NoLonger".Translate(DegreeLabel(td, step.from, p)).Resolve();
            return td.label;
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
