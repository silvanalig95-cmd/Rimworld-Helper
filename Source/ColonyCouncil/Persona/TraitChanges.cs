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
        private static Dictionary<MentalStateDef, List<TraitTally>> byMental;
        /// <summary>The tallies that count marks as they come, one per key (marks are given rarely, so a list).</summary>
        private static List<TraitTally> byMark;
        private static List<string> paceRecords;
        private static int generation = -1;

        private static void Build()
        {
            if (byThought != null && generation == DefRefs.Generation) return;
            generation = DefRefs.Generation;
            byThought = new Dictionary<ThoughtDef, List<TraitTally>>();
            byEvent = new Dictionary<HistoryEventDef, List<TraitTally>>();
            byTale = new Dictionary<TaleDef, List<TraitTally>>();
            byMental = new Dictionary<MentalStateDef, List<TraitTally>>();
            byMark = new List<TraitTally>();
            var records = new HashSet<string>();
            bool Same(TraitTally x, TraitTally t) => x.key == t.key && x.whileHungry == t.whileHungry && x.whileDrunk == t.whileDrunk;
            void Index<T>(Dictionary<T, List<TraitTally>> into, T def, TraitTally t) where T : Def
            {
                if (def == null) return;
                if (!into.TryGetValue(def, out var list)) into[def] = list = new List<TraitTally>();
                if (!list.Any(x => Same(x, t))) list.Add(t);
            }
            foreach (var def in TraitChangeDef.All)
            {
                foreach (var t in def.tallies ?? new List<TraitTally>())
                {
                    if (t?.key == null) continue;
                    foreach (var n in t.Split(t.thoughts)) Index(byThought, DefDatabase<ThoughtDef>.GetNamedSilentFail(n), t);
                    foreach (var n in t.Split(t.events)) Index(byEvent, DefDatabase<HistoryEventDef>.GetNamedSilentFail(n), t);
                    foreach (var n in t.Split(t.tales)) Index(byTale, DefDatabase<TaleDef>.GetNamedSilentFail(n), t);
                    foreach (var n in t.Split(t.mentalStates)) Index(byMental, DefDatabase<MentalStateDef>.GetNamedSilentFail(n), t);
                    if (!t.marks.NullOrEmpty() && !byMark.Any(x => Same(x, t))) byMark.Add(t);
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
                bool? hungry = null, drunk = null;
                foreach (var t in list)
                    if (CountsNow(t, pawn, ref hungry, ref drunk)) Count(pawn, t.key, null);
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
                bool? hungry = null, drunk = null;
                foreach (var t in list)
                    if (CountsNow(t, doer, ref hungry, ref drunk)) Count(doer, t.key, victim);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes from a deed", e);
            }
        }

        /// <summary>
        /// A mark was just given (PersonaMarks.Give): counted for the tallies that name it, so a mark can be counted after
        /// it has gone from the character page (two long peaces, a year apart). Same day, same tally: one occasion.
        /// </summary>
        public static void Notify_Mark(Pawn p, PersonaMarkDef def)
        {
            if (def == null || p == null || !Persona.On) return;
            // A fight's mark counts as a day they fought, up close or at range by the weapon they carry (TraitGainMath.FightMarks).
            if (Array.IndexOf(TraitGainMath.FightMarks, def.defName) >= 0)
            {
                var weapon = p.equipment?.Primary;
                bool? close = TraitGainMath.Close(weapon != null && weapon.def.IsRangedWeapon, weapon != null && weapon.def.IsMeleeWeapon, weapon == null);
                if (close.HasValue) Notify_Fought(p, close.Value);
            }
            Build();
            if (byMark.Count == 0) return;
            try
            {
                if (!On || !CanChange(p)) return;
                bool? hungry = null, drunk = null;
                foreach (var t in byMark)
                    if (t.CountsMark(def) && CountsNow(t, p, ref hungry, ref drunk)) Count(p, t.key, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes from a mark", e);
            }
        }

        /// <summary>A breakdown from their mood started (WorldReactions.Breakdown): counted for the tallies that name it (a food binge).</summary>
        public static void Notify_MentalState(Pawn p, MentalStateDef state)
        {
            if (state == null || p == null || !Persona.On) return;
            Build();
            if (!byMental.TryGetValue(state, out var list)) return;
            try
            {
                if (!On || !CanChange(p)) return;
                bool? hungry = null, drunk = null;
                foreach (var t in list)
                    if (CountsNow(t, p, ref hungry, ref drunk)) Count(p, t.key, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes from a breakdown", e);
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
                bool? hungry = null, drunk = null;
                foreach (var t in list)
                    if (CountsNow(t, doer, ref hungry, ref drunk)) Count(doer, t.key, victim, Now - tale.AgeTicks);
            }
        }

        /// <summary>Whether a tally counts a moment of theirs now: one asked only while hungry, or only while drunk, counts only then.</summary>
        private static bool CountsNow(TraitTally t, Pawn p, ref bool? hungry, ref bool? drunk)
        {
            if (t.whileHungry) hungry ??= Hungry(p);
            if (t.whileDrunk) drunk ??= Drunk(p);
            return TraitChangeMath.TallyCounts(t.whileHungry, t.whileDrunk, hungry ?? false, drunk ?? false);
        }

        /// <summary>Starving or malnourished now or lately, or in a hungry season.</summary>
        private static bool Hungry(Pawn p)
        {
            var food = p.needs?.food;
            if (food != null && (food.CurCategory >= HungerCategory.UrgentlyHungry || Now - food.lastNonStarvingTick < GenDate.TicksPerDay)) return true;
            if (p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition) != null) return true;
            return PersonaMarks.HasRecent(p, "HungrySeason", 20f);
        }

        /// <summary>Drunk: the game's alcohol high on them (from beer, or any drink that gives it), tipsy or worse.</summary>
        private static bool Drunk(Pawn p) => p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.AlcoholHigh) != null;

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
        /// Once a day: the watches are brought up to date (calm days, how time is spent, the day's tales), and each
        /// colonist's rules are checked (within 10 days of the colony's last change only the ways out of a bad trait); of
        /// those that hold and roll their chance, one happens.
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
            // Within the colony's 10 days, only a way out of a bad trait.
            bool colonyWaits = TraitChangeMath.RateBlocked(-1, t.lastChange, -1, 0, now) != null;
            var ready = new List<(Pawn p, TraitChangeDef def, TraitStep step)>();
            foreach (var p in people)
            {
                // Within their 60 days, only a change that takes away a trait Rimfolk gave them.
                int lastPerson = t.LastChange(p.thingIDNumber);
                bool waiting = TraitChangeMath.RateBlocked(lastPerson, -1, -1, 0, now) != null;
                foreach (var def in TraitChangeDef.All)
                {
                    if ((waiting || colonyWaits) && def.change == TraitChangeKind.Gain) continue;
                    if (colonyWaits && !def.LeadsOut) continue;
                    try
                    {
                        if (Why(p, def, now, out var step) != null) continue;
                        if (TraitChangeMath.RateBlocked(lastPerson, t.lastChange, -1, 0, now, Undoes(p, def, step), def.WayOut(step)) != null) continue;
                        if (Rand.Chance(def.chance)) ready.Add((p, def, step));
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
                sb.Append("\n  ").Append(def.defName).Append(def.WayOut(step) ? " (a way out)" : "").Append(": ")
                    .Append(why ?? "holds (" + (def.chance * 100f).ToString("0") + "% a day)");
                if (why == null && apply)
                {
                    Apply(p, def, step, now);
                    sb.Append(" - changed now");
                    break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Dev mode: the rules that could change this colonist now if their evidence were there (the gate passes, the gap
        /// since the trait last changed aside): with <paramref name="waysOut"/> the ways out of a bad trait they have,
        /// else the gains. Each with what it still lacks (null: it holds).
        /// </summary>
        public static List<(TraitChangeDef def, string lacks)> DebugRules(Pawn p, bool waysOut)
        {
            var list = new List<(TraitChangeDef, string)>();
            if (Tracker == null || !On || !CanChange(p)) return list;
            Build();
            int now = Now;
            Look(Tracker.Watch(p, true), p, now);
            foreach (var def in TraitChangeDef.All)
            {
                if (Gate(p, def, now, out var step, true) != null) continue;
                if (waysOut ? !def.WayOut(step) : def.change != TraitChangeKind.Gain) continue;
                list.Add((def, Lacks(p, def, now)));
            }
            return list;
        }

        /// <summary>
        /// Dev mode: make this rule's change now, as the daily pass would (the letter, the journal line, the record that
        /// Rimfolk gave a trait, so its ways out can be tried), whatever its evidence, chance and the limits say.
        /// </summary>
        public static string DebugApply(Pawn p, TraitChangeDef def)
        {
            if (Tracker == null || !On || !CanChange(p) || def == null) return "traits can't change for " + (p?.LabelShort ?? "nobody") + " now";
            int now = Now;
            string gate = Gate(p, def, now, out var step, true);
            if (gate != null) return def.defName + " can't change " + p.LabelShort + ": " + gate;
            Apply(p, def, step, now);
            buildingCache.Remove(p.thingIDNumber);
            return def.defName + ": changed " + p.LabelShort + " now";
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
        /// degree it would step to, room for a trait, conflicts, a role, who gave the trait (only Rimfolk, or only not
        /// Rimfolk), the last change of this trait too recent, age and time in the colony. Null when only the evidence is
        /// missing (or nothing). With <paramref name="gapAsPart"/> the rule's gap since the trait last changed is left to the
        /// caller (the character page shows it as a part).
        /// </summary>
        private static string Gate(Pawn p, TraitChangeDef def, int now, out TraitStep step, bool gapAsPart = false)
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
            if (def.onlyGiven && !GaveIt(p, td.defName, def.givenBy)) return "not given";
            if (def.notGiven && !TraitChangeMath.GivenAllows(GaveIt(p, td.defName), false, true)) return "given";
            if (!gapAsPart && !GapOver(p, def, now)) return "trait";
            if (def.minAge > 0 && (p.ageTracker?.AgeBiologicalYears ?? 0) < def.minAge) return "age";
            if (def.daysHere > 0 && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) < def.daysHere * (float)GenDate.TicksPerDay) return "new";
            if (def.noRival && HasRival(p)) return "rival";
            return null;
        }

        /// <summary>
        /// Rimfolk gave them this trait: the newest of its changes kept for them added it (a trait they came with, or one a
        /// change already took away, isn't Rimfolk's to undo).
        /// </summary>
        public static bool GaveIt(Pawn p, string trait, List<string> by = null)
        {
            var t = Tracker;
            if (t == null || p == null || trait == null) return false;
            var mine = t.changes.Where(c => c.pawn == p.thingIDNumber && c.trait == trait).ToList();
            return TraitChangeMath.GaveIt(mine.Select(c => c.tick).ToList(), mine.Select(c => c.hasAfter && !c.hadTrait).ToList(),
                mine.Select(c => c.rule).ToList(), by);
        }

        /// <summary>The rule's gap since this trait of theirs last changed is over (or it never changed).</summary>
        private static bool GapOver(Pawn p, TraitChangeDef def, int now) =>
            TraitChangeMath.RateBlocked(-1, -1, Tracker.LastChange(p.thingIDNumber, def.Trait.defName), def.gapDays, now) == null;

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
            {
                if (tally.None ? NoneDays(p, w, tally, lastSame, now) < tally.noneDays : Entries(p, w, tally, lastSame, now).Count < tally.count)
                    return "tally " + tally.key;
            }
            if (def.friend && Friend(p) == null) return "friend";
            if (def.bondedAnimal && (BondedAnimal(p, def, now, out float bonded) == null || bonded < def.bondedDays)) return "bonded";
            if (!def.withTrait.NullOrEmpty() && WithTrait(p, def) == null) return "with trait";
            if (!FaithAllows(p, def)) return "faith";
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
                if (!f.Holds(PracticeHours(p, f.name))) return "practice " + f.name;
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

        /// <summary>
        /// They fight up close, as Rimfolk's own brawler asks: by how they fought this past year, never by the weapon in hand
        /// the moment it is read (21 November 2026; until then a melee weapon in hand that day was enough), and never with
        /// Shooting near their Melee (TraitGainMath.Fights).
        /// </summary>
        private static bool MeleeFighter(Pawn p) => Fights(p, Now) == null;

        /// <summary>How they fight as Rimfolk reads it (TraitGainMath.Fights): null when up close, else what speaks against it.</summary>
        public static string Fights(Pawn p, int now)
        {
            var melee = p?.skills?.GetSkill(SkillDefOf.Melee);
            var shooting = p?.skills?.GetSkill(SkillDefOf.Shooting);
            var (close, range) = FightDays(p, now);
            return TraitGainMath.Fights(melee?.Level ?? 0, melee == null || melee.TotallyDisabled, shooting?.Level ?? 0, shooting == null || shooting.TotallyDisabled, close, range);
        }

        /// <summary>The days of the past year they fought up close and at range (TraitGainMath.FightDays).</summary>
        public static (int close, int range) FightDays(Pawn p, int now)
        {
            var w = p == null ? null : Tracker?.Watch(p, false);
            if (w == null) return (0, 0);
            return TraitGainMath.FightDays(w.tallies.Where(t => t.key == TraitGainMath.CloseKey).Select(t => t.tick).ToList(),
                w.tallies.Where(t => t.key == TraitGainMath.RangeKey).Select(t => t.tick).ToList(), now, GenDate.TicksPerDay);
        }

        /// <summary>
        /// One of the colony fought: a foe killed, or a fight's mark given (PersonaMarks), counted up close or at range by the
        /// weapon they fought with (TraitGainMath.Close), one occasion in half a day. Kept with their other deeds counted.
        /// </summary>
        public static void Notify_Fought(Pawn p, bool close)
        {
            try
            {
                if (p == null || !On || !CanChange(p)) return;
                Count(p, close ? TraitGainMath.CloseKey : TraitGainMath.RangeKey, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trait changes: a fight", e);
            }
        }

        /// <summary>A foe they killed (Happenings.Notify_Killed), counted by the weapon of the blow that killed: a ranged weapon, a melee weapon, or bare hands.</summary>
        public static void Notify_Killed(Pawn killer, DamageInfo? dinfo)
        {
            if (killer == null || dinfo == null || !killer.RaceProps.Humanlike) return;
            var weapon = dinfo.Value.Weapon;
            bool? close = TraitGainMath.Close(weapon != null && weapon.IsRangedWeapon, weapon != null && weapon.IsMeleeWeapon,
                weapon == null && dinfo.Value.WeaponBodyPartGroup != null);
            if (close.HasValue) Notify_Fought(killer, close.Value);
        }

        /// <summary>
        /// Whether a trait another mod gives them fits their life as Rimfolk reads it (Persona/TraitGains.cs): null when it
        /// does, or when Rimfolk has no rule for it; else why not, in plain words. It fits when any of Rimfolk's own ways to
        /// it would see it fitting them as they are (their skills, how they fight, a trait that rules it out, their character,
        /// their faith, their age, a trait it needs), not by the story Rimfolk's own changes wait for (the deeds, marks and
        /// records behind them).
        /// </summary>
        public static string Unfit(Pawn p, TraitDef td, int degree)
        {
            if (p?.story?.traits == null || td == null) return null;
            string first = null;
            foreach (var def in TraitChangeDef.All)
            {
                if (def?.Trait != td || !TraitGainMath.RuleReads(def.change, def.degree, def.anyDegree || td.degreeDatas.NullOrEmpty() || td.degreeDatas.Count == 1, degree)) continue;
                string why = FitLacks(p, def);
                if (why == null) return null;
                first ??= why;
            }
            return first;
        }

        /// <summary>The first thing about them that rules out this rule's trait, in plain words; null when it fits them.</summary>
        private static string FitLacks(Pawn p, TraitChangeDef def)
        {
            var traits = p.story.traits;
            foreach (var b in def.blockedBy ?? new List<string>())
            {
                var have = traits.allTraits.FirstOrDefault(x => x?.def?.defName == b);
                if (have != null) return "CC_TraitGain_Why_Blocked".Translate(p.Named("PAWN"), have.LabelCap.Named("TRAIT")).Resolve();
            }
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var s = sd == null ? null : p.skills?.GetSkill(sd);
                if (s == null) continue;
                if (s.TotallyDisabled || !f.Holds(s.Level))
                    return (f.atLeast ? "CC_TraitGain_Why_Skill" : "CC_TraitGain_Why_SkillMost")
                        .Translate(p.Named("PAWN"), sd.LabelCap.Named("SKILL"), (s.TotallyDisabled ? 0 : s.Level).Named("HAVE"), Mathf.RoundToInt(f.value).Named("NEED")).Resolve();
            }
            if (def.meleeFighter)
            {
                string fights = Fights(p, Now);
                if (!TraitGainMath.FitsCloseFighter(fights))
                {
                    var melee = p.skills?.GetSkill(SkillDefOf.Melee);
                    var shooting = p.skills?.GetSkill(SkillDefOf.Shooting);
                    var (close, range) = FightDays(p, Now);
                    return fights == "range"
                        ? "CC_TraitGain_Why_Range".Translate(p.Named("PAWN"), range.Named("RANGE"), close.Named("CLOSE")).Resolve()
                        : fights == "melee"
                            ? "CC_TraitGain_Why_NoMelee".Translate(p.Named("PAWN")).Resolve()
                            : "CC_TraitGain_Why_Shoots".Translate(p.Named("PAWN"), (shooting?.Level ?? 0).Named("SHOOTING"), (melee?.Level ?? 0).Named("MELEE")).Resolve();
                }
            }
            if (!def.withTrait.NullOrEmpty() && WithTrait(p, def) == null)
                return "CC_TraitGain_Why_WithTrait".Translate(p.Named("PAWN"), string.Join(", ", def.withTrait.Select(n => DefDatabase<TraitDef>.GetNamedSilentFail(n)?.degreeDatas?.FirstOrDefault()?.label ?? n)).Named("TRAIT")).Resolve();
            if (def.Now.Count > 0 && Persona.Of(p) != null && !MarksMath.Holds(def.Now, Persona.Values(p)))
                return "CC_TraitGain_Why_Character".Translate(p.Named("PAWN")).Resolve();
            if (!FaithAllows(p, def)) return "CC_TraitGain_Why_Faith".Translate(p.Named("PAWN")).Resolve();
            if (def.minAge > 0 && (p.ageTracker?.AgeBiologicalYears ?? 0) < def.minAge) return "CC_TraitGain_Why_Age".Translate(p.Named("PAWN"), def.minAge.Named("AGE")).Resolve();
            return null;
        }

        /// <summary>The marks behind it: those that match, since this trait last changed, within the days asked, oldest first.</summary>
        private static List<PersonaMark> EvidenceMarks(PawnPersona r, TraitMarkNeed need, int after, int now) =>
            r.marks.Where(m => m?.def != null && m.tick > after && need.Matches(m.def) && (need.withinDays <= 0 || now - m.tick < need.withinDays * GenDate.TicksPerDay))
                .OrderBy(m => m.tick).ToList();

        /// <summary>
        /// The moments a tally has counted, since this trait last changed and within its days, oldest first. A count of
        /// marks also takes in the marks of that kind still on their character page (one given before the count began),
        /// unless it asks how they were at the time (hungry, drunk), which a mark on the page can't tell.
        /// </summary>
        private static List<TallyEntry> Entries(Pawn p, TraitWatch w, TraitTally tally, int after, int now)
        {
            float within = tally.withinDays > 0 ? tally.withinDays : TallyKeepDays;
            var list = w == null ? new List<TallyEntry>()
                : w.tallies.Where(e => e.key == tally.key && e.tick > after && now - e.tick < within * GenDate.TicksPerDay).ToList();
            if (!tally.marks.NullOrEmpty() && !tally.Filtered)
            {
                var r = Persona.Of(p);
                if (r != null)
                {
                    var page = r.marks.Where(m => m?.def != null && tally.CountsMark(m.def)).Select(m => m.tick).ToList();
                    foreach (int tick in TraitChangeMath.PageMarksToAdd(list.Select(e => e.tick).ToList(), page, after, now, within))
                        list.Add(new TallyEntry { key = tally.key, tick = tick });
                }
            }
            return list.OrderBy(e => e.tick).ToList();
        }

        /// <summary>
        /// A zero count: days without any of these, from the newest kept (and, for marks, the newest still on their page)
        /// or the trait's last change, whichever is later, and never from before they joined the colony; with noneAfter,
        /// only from the newest of that other tally (0 days until it has counted one).
        /// </summary>
        private static float NoneDays(Pawn p, TraitWatch w, TraitTally tally, int after, int now)
        {
            var ticks = w == null ? new List<int>() : w.tallies.Where(e => e.key == tally.key).Select(e => e.tick).ToList();
            if (!tally.marks.NullOrEmpty())
            {
                var r = Persona.Of(p);
                if (r != null)
                    foreach (var m in r.marks)
                        if (m?.def != null && tally.CountsMark(m.def)) ticks.Add(m.tick);
            }
            float here = p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? -1f;
            int known = here < 0f ? -1 : Math.Max(0, now - (int)Math.Min(here, now));
            if (tally.noneAfter.NullOrEmpty()) return TraitChangeMath.NoneDays(ticks, now, after, known);
            var start = w == null ? new List<int>() : w.tallies.Where(e => e.key == tally.noneAfter).Select(e => e.tick).ToList();
            return TraitChangeMath.NoneDaysAfter(ticks, start, now, after, known);
        }

        /// <summary>A friend on the Bonds page: an announced friendship with someone still alive, or null.</summary>
        private static Pawn Friend(Pawn p)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || p == null) return null;
            foreach (var tie in social.ties)
            {
                if (tie == null || tie.kind != TieKind.Friends || !tie.announced || !tie.Involves(p)) continue;
                var other = tie.Other(p);
                if (other != null && !other.Dead) return other;
            }
            return null;
        }

        /// <summary>
        /// An animal bonded to them, alive, or null; with the rule's <see cref="TraitChangeDef.bondedKind"/>, one of the
        /// colony's dogs or cats. Of several, the one bonded longest, and <paramref name="days"/> how long (from the bond's
        /// own start).
        /// </summary>
        private static Pawn BondedAnimal(Pawn p, TraitChangeDef def, int now, out float days)
        {
            days = 0f;
            var rels = p?.relations?.DirectRelations;
            if (rels == null) return null;
            Pawn best = null;
            foreach (var rel in rels)
            {
                var a = rel?.otherPawn;
                if (a == null || rel.def != PawnRelationDefOf.Bond || a.Dead || a.RaceProps?.Animal != true) continue;
                if (!def.bondedKind.NullOrEmpty() && (a.Faction != Faction.OfPlayer || TraitChangeMath.PetKind(a.def.defName, a.def.label) != def.bondedKind)) continue;
                float d = TraitChangeMath.BondDays(new[] { rel.startTicks }, now);
                if (best != null && d <= days) continue;
                best = a;
                days = d;
            }
            return best;
        }

        /// <summary>The trait of theirs the rule asks for (one of <see cref="TraitChangeDef.withTrait"/>), or null.</summary>
        private static Trait WithTrait(Pawn p, TraitChangeDef def)
        {
            var all = p?.story?.traits?.allTraits;
            if (all == null || def.withTrait.NullOrEmpty()) return null;
            return all.FirstOrDefault(x => x?.def != null && def.withTrait.Contains(x.def.defName));
        }

        /// <summary>"the brawler trait": the traits a rule asks for, by their labels.</summary>
        private static string WithTraitWords(TraitChangeDef def)
        {
            var labels = def.withTrait.Select(n => DefDatabase<TraitDef>.GetNamedSilentFail(n)).Where(x => x != null)
                .Select(x => x.degreeDatas?.FirstOrDefault()?.label ?? x.label ?? x.defName).Distinct().ToList();
            if (labels.Count == 0) return string.Join(", ", def.withTrait);
            string names = labels.Count == 1 ? labels[0]
                : "CC_TCB_PracticeOr".Translate(string.Join(", ", labels.Take(labels.Count - 1)), labels[labels.Count - 1]).Resolve();
            return "CC_TCB_TheTrait".Translate(names).Resolve();
        }

        /// <summary>"one of the colony's dogs": what a rule's bond is with, or "an animal".</summary>
        private static string BondedWords(TraitChangeDef def) =>
            def.bondedKind.NullOrEmpty() ? "CC_TC_PetKind_any".Translate().ToString() : ("CC_TC_PetKind_" + def.bondedKind).Translate().ToString();

        /// <summary>Their ideoligion holds none of the precepts the rule names (no ideoligion, or no rule about it: it holds).</summary>
        private static bool FaithAllows(Pawn p, TraitChangeDef def)
        {
            if (def.NotPrecepts.Count == 0) return true;
            var ideo = p.Ideo;
            if (ideo == null) return true;
            foreach (var name in def.NotPrecepts)
            {
                var pd = DefDatabase<PreceptDef>.GetNamedSilentFail(name);
                if (pd != null && ideo.HasPrecept(pd)) return false;
            }
            return true;
        }

        /// <summary>Recent hours at a practice, or at several added up ("Cooking+Smithing": the stove and the forge together).</summary>
        private static float PracticeHours(Pawn p, string name)
        {
            float sum = 0f;
            foreach (var part in TraitChangeMath.PracticeParts(name)) sum += Practice.Hours(p, part);
            return sum;
        }

        /// <summary>"cooking or smithing": what a practice fact is about, for the player.</summary>
        private static string PracticeWords(string name)
        {
            var words = TraitChangeMath.PracticeParts(name).Select(Practice.Words).ToList();
            if (words.Count <= 1) return words.FirstOrDefault() ?? name;
            return "CC_TCB_PracticeOr".Translate(string.Join(", ", words.Take(words.Count - 1)), words[words.Count - 1]).Resolve();
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
            if (r != null && !def.notMarks.NullOrEmpty())
                ordered.Add("CC_TC_NotMarks".Translate(def.notMarksDays, NotMarkNames(def)).Resolve());
            var w = t.Watch(p, false);
            foreach (var tally in def.tallies)
            {
                if (tally.None)
                {
                    float days = NoneDays(p, w, tally, lastSame, now);
                    ordered.Add("CC_TC_None".Translate(tally.label.CapitalizeFirst(), float.IsInfinity(days) ? tally.noneDays : Mathf.FloorToInt(days)).Resolve());
                    continue;
                }
                var entries = Entries(p, w, tally, lastSame, now);
                if (entries.Count == 0) continue;
                var when = entries.Select(e => PersonaMarks.DateLabel(e.tick, p) + (e.label.NullOrEmpty() ? "" : " (" + e.label + ")")).ToList();
                ordered.Add(tally.label.CapitalizeFirst() + ": " + string.Join(", ", when) + ".");
            }
            if (def.NotPrecepts.Count > 0 && p.Ideo != null)
                ordered.Add("CC_TC_Faith".Translate(p.Ideo.name, def.notPreceptsLabel).Resolve());
            var friend = def.friend ? Friend(p) : null;
            if (friend != null) ordered.Add("CC_TC_Friend".Translate(p.Named("PAWN"), friend.Named("OTHER")).Resolve());
            float bondDays = 0f;
            var animal = def.bondedAnimal ? BondedAnimal(p, def, now, out bondDays) : null;
            if (animal != null)
                ordered.Add(def.bondedDays > 0
                    ? "CC_TC_BondedFor".Translate(Mathf.FloorToInt(bondDays), p.Named("PAWN"), animal.LabelShort.CapitalizeFirst().Named("ANIMAL")).Resolve()
                    : "CC_TC_Bonded".Translate(p.Named("PAWN"), animal.LabelShort.CapitalizeFirst().Named("ANIMAL")).Resolve());
            if (!def.withTrait.NullOrEmpty() && WithTrait(p, def) != null)
                ordered.Add("CC_TC_WithTrait".Translate(WithTraitWords(def), p.Named("PAWN")).Resolve());
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
                ordered.Add("CC_TC_Practice".Translate(PracticeWords(f.name).CapitalizeFirst(), Mathf.RoundToInt(PracticeHours(p, f.name))).Resolve());
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
        /// One thing a rule asks for, as the character page tells it: the line in the list, whether it has happened, what
        /// is still missing in a few plain words (the "still missing" line under a way out), and whether it holds by
        /// default (nothing of a kind lately, a stretch without something, the gap since the trait last changed), which
        /// doesn't count as evidence toward showing a rule.
        /// </summary>
        public struct TraitPart
        {
            public string text, missing;
            public bool ok, byDefault;

            public TraitPart(string text, bool ok, string missing, bool byDefault = false)
            {
                this.text = text;
                this.ok = ok;
                this.missing = missing;
                this.byDefault = byDefault;
            }
        }

        /// <summary>
        /// What leads to a rule's change, part by part, in words, and whether each part holds: the gap since the trait last
        /// changed (while it lasts), how far a side has moved and where it stands, the marks and the marks it waits out, the
        /// deeds and the stretches without one, the faith, a friend, an animal bonded to them (and for how long), a trait they
        /// must have, the records, skills, how their time goes, calm days, a habit, what they've been doing lately. The same
        /// evidence the rule checks (Lacks), told for the player.
        /// </summary>
        public static List<TraitPart> Parts(Pawn p, TraitChangeDef def, int now)
        {
            var parts = new List<TraitPart>();
            var t = Tracker;
            if (t == null || def?.Trait == null) return parts;
            int lastSame = t.LastChange(p.thingIDNumber, def.Trait.defName);
            // The rule's gap since this trait last changed, while it lasts (only a way out is shown before it's over).
            if (!GapOver(p, def, now))
            {
                int since = Mathf.FloorToInt(TraitChangeMath.DaysSince(lastSame, now));
                parts.Add(new TraitPart("CC_TCB_Gap".Translate(def.Trait.label, since, def.gapDays).Resolve(), false,
                    "CC_TCB_MissGap".Translate(def.Trait.label, def.gapDays, since).Resolve(), true));
            }
            var r = Persona.Of(p);
            if (r != null && (def.Moved.Count > 0 || def.Now.Count > 0))
            {
                r.Fade(now);
                var moved = new float[PersonaMath.Axes];
                for (int i = 0; i < moved.Length; i++) moved[i] = TraitChangeMath.Moved(r.lasting[i], r.drift[i]);
                foreach (var c in def.Moved)
                {
                    string needs = Needs(c.atLeast, c.value, true);
                    parts.Add(new TraitPart("CC_TCB_Moved".Translate(AxisName(c.axis), Signed(moved[c.axis]), needs).Resolve(), c.Holds(moved),
                        "CC_TCB_MissMoved".Translate(AxisWord(c.axis), needs, Signed(moved[c.axis])).Resolve()));
                }
                var values = Persona.Values(p);
                foreach (var c in def.Now)
                {
                    string needs = Needs(c.atLeast, c.value, true);
                    parts.Add(new TraitPart("CC_TCB_Now".Translate(AxisName(c.axis), Signed(values[c.axis]), needs).Resolve(), c.Holds(values),
                        "CC_TCB_MissNow".Translate(AxisWord(c.axis), needs, Signed(values[c.axis])).Resolve()));
                }
            }
            foreach (var need in def.marks)
            {
                int have = r == null ? 0 : EvidenceMarks(r, need, lastSame, now).Count;
                var labels = MarkLabels(need.Matches);
                string names = string.Join(", ", labels);
                string text = names.NullOrEmpty() ? "CC_TCB_Marks".Translate(Mathf.Min(have, need.count), need.count).Resolve()
                    : "CC_TCB_MarksNamed".Translate(Mathf.Min(have, need.count), need.count, names).Resolve();
                int more = need.count - have;
                string missing = more > 1 ? "CC_TCB_MissMore".Translate(more, names).Resolve()
                    : labels.Count == 1 ? names : "CC_TCB_MissOneOf".Translate(names).Resolve();
                parts.Add(new TraitPart(text, have >= need.count, missing));
            }
            if (r != null && !def.notMarks.NullOrEmpty())
            {
                // Days since the newest mark it waits out (on their page), up to the days it asks for.
                int newest = -1;
                foreach (var m in r.marks)
                    if (m?.def != null && def.NotMark(m.def) && m.tick > newest) newest = m.tick;
                int days = newest < 0 ? def.notMarksDays : Mathf.Min(Mathf.FloorToInt(TraitChangeMath.DaysSince(newest, now)), def.notMarksDays);
                string names = NotMarkNames(def);
                parts.Add(new TraitPart("CC_TCB_NotMarks".Translate(names, days, def.notMarksDays).Resolve(), days >= def.notMarksDays,
                    "CC_TCB_MissNotMarks".Translate(def.notMarksDays, names, days).Resolve(), true));
            }
            var w = t.Watch(p, false);
            foreach (var tally in def.tallies)
            {
                string label = tally.label ?? tally.key;
                if (tally.None)
                {
                    float none = NoneDays(p, w, tally, lastSame, now);
                    int days = float.IsInfinity(none) ? tally.noneDays : Mathf.Min(Mathf.FloorToInt(none), tally.noneDays);
                    parts.Add(new TraitPart("CC_TCB_None".Translate(label.CapitalizeFirst(), days, tally.noneDays).Resolve(), none >= tally.noneDays,
                        "CC_TCB_MissNone".Translate(label, tally.noneDays, days).Resolve(), true));
                    continue;
                }
                int have = Entries(p, w, tally, lastSame, now).Count;
                parts.Add(new TraitPart("CC_TCB_Count".Translate(label.CapitalizeFirst(), Mathf.Min(have, tally.count), tally.count).Resolve(), have >= tally.count,
                    "CC_TCB_MissCount".Translate(label, tally.count - have).Resolve()));
            }
            if (def.NotPrecepts.Count > 0)
                parts.Add(new TraitPart(def.notPreceptsLabel.CapitalizeFirst(), FaithAllows(p, def), def.notPreceptsLabel));
            if (def.friend)
            {
                var friend = Friend(p);
                parts.Add(new TraitPart(friend != null ? "CC_TCB_FriendWith".Translate(friend.Named("OTHER")).Resolve() : "CC_TCB_Friend".Translate().ToString(),
                    friend != null, "CC_TCB_MissFriend".Translate().ToString()));
            }
            if (def.bondedAnimal)
            {
                var animal = BondedAnimal(p, def, now, out float bonded);
                if (!def.bondedKind.NullOrEmpty() || def.bondedDays > 0)
                {
                    // One of the colony's dogs bonded to them, and for how long of how long: two parts, so a bond on its
                    // way to a year shows under "Where life is taking them".
                    string with = BondedWords(def);
                    parts.Add(new TraitPart(animal != null ? "CC_TCB_BondedKindWith".Translate(with.CapitalizeFirst(), animal.LabelShort).Resolve()
                            : "CC_TCB_BondedKind".Translate(with.CapitalizeFirst()).Resolve(),
                        animal != null, "CC_TCB_BondedKind".Translate(with).Resolve()));
                    if (def.bondedDays > 0)
                    {
                        int days = Mathf.Min(Mathf.FloorToInt(bonded), def.bondedDays);
                        parts.Add(new TraitPart("CC_TCB_BondedDays".Translate(days, def.bondedDays).Resolve(), animal != null && bonded >= def.bondedDays,
                            "CC_TCB_MissBondedDays".Translate(def.bondedDays, days).Resolve()));
                    }
                }
                else
                    parts.Add(new TraitPart(animal != null ? "CC_TCB_BondedWith".Translate(animal.LabelShort.Named("ANIMAL")).Resolve() : "CC_TCB_Bonded".Translate().ToString(),
                        animal != null, "CC_TCB_MissBonded".Translate().ToString()));
            }
            if (!def.withTrait.NullOrEmpty())
            {
                // Who they are already (a brawler): it holds or it doesn't, and isn't evidence toward showing the rule.
                string words = WithTraitWords(def);
                parts.Add(new TraitPart(words.CapitalizeFirst(), WithTrait(p, def) != null, words, true));
            }
            foreach (var f in def.Records)
            {
                var rd = DefDatabase<RecordDef>.GetNamedSilentFail(f.name);
                if (rd == null || p.records == null) continue;
                float v = p.records.GetValue(rd);
                string needs = Needs(f.atLeast, f.value, false);
                parts.Add(new TraitPart("CC_TCB_Fact".Translate(rd.LabelCap, Mathf.RoundToInt(v), needs).Resolve(), f.Holds(v),
                    "CC_TCB_MissFact".Translate(rd.label, needs, Mathf.RoundToInt(v)).Resolve()));
            }
            foreach (var f in def.Skills)
            {
                var sd = DefDatabase<SkillDef>.GetNamedSilentFail(f.name);
                var sk = sd == null ? null : p.skills?.GetSkill(sd);
                if (sk == null) continue;
                string needs = Needs(f.atLeast, f.value, false);
                parts.Add(new TraitPart("CC_TCB_Fact".Translate(sd.LabelCap, sk.Level, needs).Resolve(), !sk.TotallyDisabled && f.Holds(sk.Level),
                    "CC_TCB_MissFact".Translate(sd.label, needs, sk.Level).Resolve()));
            }
            foreach (var pace in def.pace)
            {
                float share = Share(w, p, pace, now);
                string needs = pace.atLeast >= 0f ? "CC_TCB_AtLeast".Translate(Mathf.RoundToInt(pace.atLeast * 100f)).Resolve() : "CC_TCB_AtMost".Translate(Mathf.RoundToInt(pace.atMost * 100f)).Resolve();
                int pct = Mathf.RoundToInt(Mathf.Max(0f, share) * 100f);
                string label = pace.label ?? pace.record;
                parts.Add(new TraitPart("CC_TCB_Pace".Translate(label.CapitalizeFirst(), pct, needs).Resolve(), pace.Holds(share),
                    "CC_TCB_MissPace".Translate(label, needs, pct).Resolve()));
            }
            if (def.calmDays > 0)
            {
                int days = Mathf.FloorToInt(CalmDays(w, def, lastSame, now));
                int shown = Mathf.Min(days, def.calmDays);
                string word = TraitWord(def, p, true);
                string text = def.calmSinceChange
                    ? "CC_TCB_CalmSince".Translate(shown, def.calmDays, word).Resolve()
                    : "CC_TCB_Calm".Translate(shown, def.calmDays).Resolve();
                string missing = def.calmSinceChange
                    ? "CC_TCB_MissCalmSince".Translate(def.calmDays, shown, word).Resolve()
                    : "CC_TCB_MissCalm".Translate(def.calmDays, shown).Resolve();
                parts.Add(new TraitPart(text, days >= def.calmDays, missing));
            }
            if (def.vendetta == TraitChangeDef.VendettaUnpaid)
            {
                UnpaidVow(p, def, now, out float unpaid);
                int days = Mathf.Min(Mathf.FloorToInt(unpaid), def.vendettaDays);
                parts.Add(new TraitPart("CC_TCB_VowUnpaid".Translate(days, def.vendettaDays).Resolve(), days >= def.vendettaDays,
                    "CC_TCB_MissVowUnpaid".Translate(def.vendettaDays, days).Resolve()));
            }
            else if (def.vendetta == TraitChangeDef.VendettaOver)
                parts.Add(new TraitPart("CC_TCB_VowOver".Translate().ToString(), VowsOver(p), "CC_TCB_MissVowOver".Translate().ToString()));
            if (def.meleeFighter)
            {
                // How they fought this past year, not the weapon in hand today (TraitGainMath.Fights).
                var (close, range) = FightDays(p, now);
                parts.Add(new TraitPart("CC_TCB_Melee".Translate(close, range, TraitGainMath.CloseDaysMin).Resolve(), MeleeFighter(p), "CC_TCB_MissMelee".Translate().ToString()));
            }
            foreach (var f in def.Practice)
            {
                float h = PracticeHours(p, f.name);
                string words = PracticeWords(f.name);
                string needs = Needs(f.atLeast, f.value, false);
                parts.Add(new TraitPart("CC_TCB_Practice".Translate(words.CapitalizeFirst(), Mathf.RoundToInt(h), needs).Resolve(), f.Holds(h),
                    "CC_TCB_MissPractice".Translate(words, needs, Mathf.RoundToInt(h)).Resolve()));
            }
            if (def.Habit != null)
            {
                var q = HabitOf(p, def);
                if (q == null)
                    parts.Add(new TraitPart("CC_TCB_NoHabit".Translate(def.Habit.label).Resolve(), false, "CC_TCB_MissHabit".Translate(def.Habit.label).Resolve()));
                else
                {
                    int held = Mathf.FloorToInt(TraitChangeMath.DaysSince(q.gainedTick, now));
                    parts.Add(new TraitPart("CC_TCB_HabitHeld".Translate(q.Label, Mathf.Min(held, def.habitDays), def.habitDays).Resolve(), held >= def.habitDays,
                        "CC_TCB_MissHabitHeld".Translate(q.Label, def.habitDays, Mathf.Min(held, def.habitDays)).Resolve()));
                    parts.Add(new TraitPart("CC_TCB_HabitLived".Translate(q.Label, Mathf.Min(q.livedDays, def.habitLived), def.habitLived).Resolve(), q.livedDays >= def.habitLived,
                        "CC_TCB_MissHabitLived".Translate(q.Label, def.habitLived, Mathf.Min(q.livedDays, def.habitLived)).Resolve()));
                }
            }
            return parts;
        }

        /// <summary>The marks that match, by their names on the character page: "a kindness remembered", "forgiven", ... (twelve at most).</summary>
        private static List<string> MarkLabels(Func<PersonaMarkDef, bool> match)
        {
            var labels = new List<string>();
            foreach (var def in DefDatabase<PersonaMarkDef>.AllDefsListForReading)
                if (match(def) && !def.label.NullOrEmpty() && !labels.Contains(def.label)) labels.Add(def.label);
            if (labels.Count > 12) labels = labels.Take(11).Append("CC_TCB_MarksMore".Translate().ToString()).ToList();
            return labels;
        }

        /// <summary>The marks a rule waits out, by their names on the character page.</summary>
        private static string NotMarkNames(TraitChangeDef def) => string.Join(", ", MarkLabels(def.NotMark));

        private static string AxisName(int axis) => ("CC_Persona_Axis_" + (PersonaAxis)axis).Translate().CapitalizeFirst();

        private static string AxisWord(int axis) => ("CC_Persona_Axis_" + (PersonaAxis)axis).Translate().ToString().ToLowerInvariant();

        /// <summary>"+16 or more", "10 or less".</summary>
        private static string Needs(bool atLeast, float value, bool signed)
        {
            string v = signed ? Signed(value) : Mathf.RoundToInt(value).ToString();
            return (atLeast ? "CC_TCB_OrMore" : "CC_TCB_OrLess").Translate(v).Resolve();
        }

        /// <summary>Most rules shown at once besides the ways out of a bad trait, which are all shown.</summary>
        public const int BuildingShown = 3;

        private static readonly Dictionary<int, (int tick, CharacterSection section)> buildingCache = new Dictionary<int, (int, CharacterSection)>();

        /// <summary>
        /// Section 26: the changes their life is heading toward, each with how many of the things that lead there have
        /// happened; hover one for the list. Only rules nothing else stands in the way of (the gate). Every way out of a bad
        /// trait they have comes first and is shown from the start (a habit's only once they have the habit), with a line of
        /// what is still missing in plain words, so the player can steer toward it; then up to three other rules with at
        /// least a third of their evidence there, the furthest along first (TraitChangeMath.BuildingShows). Kept a few hours
        /// of game time.
        /// </summary>
        public static CharacterSection Building(Pawn p)
        {
            if (!On || !CanChange(p)) return null;
            int now = Now;
            if (buildingCache.TryGetValue(p.thingIDNumber, out var cached) && now >= cached.tick && now - cached.tick < GenDate.TicksPerHour * 2) return cached.section;
            if (buildingCache.Count > 60) buildingCache.Clear();
            Build();
            var rows = new List<(TraitChangeDef def, bool wayOut, int have, int total, List<TraitPart> parts)>();
            foreach (var def in TraitChangeDef.All)
            {
                try
                {
                    if (Gate(p, def, now, out var step, true) != null) continue;
                    bool wayOut = def.WayOut(step) && (def.Habit == null || HabitOf(p, def) != null);
                    if (!wayOut && !GapOver(p, def, now)) continue;
                    var parts = Parts(p, def, now);
                    if (parts.Count == 0) continue;
                    var evidence = parts.Where(x => !x.byDefault).ToList();
                    if (!TraitChangeMath.BuildingShows(wayOut, evidence.Count(x => x.ok), evidence.Count)) continue;
                    rows.Add((def, wayOut, parts.Count(x => x.ok), parts.Count, parts));
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
                // Every way out first, then the furthest along of the rest.
                var shown = rows.Where(x => x.wayOut).OrderByDescending(x => x.have / (float)x.total).ThenByDescending(x => x.have)
                    .Concat(rows.Where(x => !x.wayOut).OrderByDescending(x => x.have / (float)x.total).ThenByDescending(x => x.have).Take(BuildingShown));
                foreach (var row in shown)
                {
                    string count = row.have >= row.total ? "CC_TCB_All".Translate().ToString() : "CC_TCB_Of".Translate(row.have, row.total).Resolve();
                    var tip = new StringBuilder("CC_TCB_Tip".Translate(TraitWord(row.def, p)).Resolve());
                    foreach (var part in row.parts)
                        tip.Append("\n").Append(("• " + part.text).Colorize(part.ok ? UiKit.Good : UiKit.Dim));
                    tip.Append("\n\n").Append("CC_TCB_TipEnd".Translate());
                    string tipText = tip.ToString();
                    s.Line(row.def.label.CapitalizeFirst() + "  " + count.Colorize(UiKit.Dim), tipText);
                    if (row.wayOut && row.have < row.total)
                    {
                        string missing = string.Join("; ", row.parts.Where(x => !x.ok && !x.missing.NullOrEmpty()).Select(x => x.missing));
                        s.Line(("    " + "CC_TCB_StillMissing".Translate(missing).Resolve()).Colorize(UiKit.Dim), tipText);
                    }
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
