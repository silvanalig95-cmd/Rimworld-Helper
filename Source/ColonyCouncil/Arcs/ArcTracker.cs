using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Personal stories, in a game component of their own. Once a day it may begin one (a few a year, at the setting's
    /// pace), for a colonist whose life gives it a reason; once an hour it looks at each story under way: whether its
    /// stage's gate has opened (a skill reached, a talk had, a piece made, days passed with a condition held), whether
    /// it has run out of time, and whether its people are still here. Each stage writes a journal line in the
    /// colonist's voice, a chronicle line and a message, and applies its effects; the last one sends a letter that
    /// retells the whole story. At most one story per colonist. With the setting off, stories wait where they are.
    /// </summary>
    public class ArcTracker : GameComponent
    {
        // ---------------- Saved ----------------

        public List<Arc> active = new List<Arc>();
        public List<ArcRecord> past = new List<ArcRecord>();
        public List<ArcEulogy> eulogies = new List<ArcEulogy>();
        /// <summary>When the last story began (tick 0, the game's start, until one has).</summary>
        public int lastStartTick;

        // ---------------- Not saved ----------------

        [Unsaved] private readonly Dictionary<Pawn, Arc> byPawn = new Dictionary<Pawn, Arc>();
        [Unsaved] private int indexedCount = -1;

        public const int HourTicks = 2500;
        public const int MaxPast = 200;
        private const int Day = GenDate.TicksPerDay;

        private static ArcTracker cached;
        private static Game cachedGame;

        public static ArcTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<ArcTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public ArcTracker(Game game)
        {
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.arcFrequency != ArcFrequency.Off;

        private static int Now => Find.TickManager.TicksGame;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            indexedCount = -1;
            ArcUtil.ResetCaches();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Prune();
            Scribe_Collections.Look(ref active, "active", LookMode.Deep);
            Scribe_Collections.Look(ref past, "past", LookMode.Deep);
            Scribe_Collections.Look(ref eulogies, "eulogies", LookMode.Deep);
            Scribe_Values.Look(ref lastStartTick, "lastStartTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active == null) active = new List<Arc>();
                // A story whose def is gone (its mod removed, or renamed) is dropped without a word; so is one whose colonist is gone.
                active.RemoveAll(a => a == null || a.lost || a.def == null || a.pawn == null || a.def.stages.NullOrEmpty() || a.stage < 0 || a.stage > a.def.LastStage);
                if (past == null) past = new List<ArcRecord>();
                past.RemoveAll(r => r == null || r.defName.NullOrEmpty());
                if (eulogies == null) eulogies = new List<ArcEulogy>();
                eulogies.RemoveAll(e => e == null || e.dead == null || e.speaker == null || DefDatabase<ArcDef>.GetNamedSilentFail(e.defName) == null);
                indexedCount = -1;
            }
        }

        private void Prune()
        {
            while (past.Count > MaxPast) past.RemoveAt(0);
        }

        // ---------------- Queries ----------------

        private void Index()
        {
            if (indexedCount == active.Count) return;
            byPawn.Clear();
            foreach (var a in active)
                if (a?.pawn != null)
                    byPawn[a.pawn] = a;
            indexedCount = active.Count;
        }

        private void Dirty() => indexedCount = -1;

        /// <summary>The story under way for this colonist, or null.</summary>
        public Arc Of(Pawn p)
        {
            if (p == null) return null;
            Index();
            return byPawn.TryGetValue(p, out var a) ? a : null;
        }

        /// <summary>Stories under way that this colonist is part of as the other person.</summary>
        public IEnumerable<Arc> PartOf(Pawn p) => p == null ? Enumerable.Empty<Arc>() : active.Where(a => a.other == p);

        /// <summary>Stories this colonist has had, newest first.</summary>
        public IEnumerable<ArcRecord> PastOf(Pawn p) => p == null ? Enumerable.Empty<ArcRecord>() : past.Where(r => r.pawnId == p.ThingID).Reverse();

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (t % HourTicks != 1777) return;
            if (!On) return;
            Safe("personal stories", Hourly);
            if (t % Day < HourTicks) Safe("new personal stories", TryStart);
        }

        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        private void Hourly()
        {
            foreach (var arc in active.ToList())
            {
                try
                {
                    Step(arc);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("personal story " + arc?.def?.defName, e);
                }
            }
            int now = Now;
            foreach (var e in eulogies.Where(e => now >= e.dueTick).ToList())
            {
                eulogies.Remove(e);
                Safe("personal story eulogy", () => Eulogy(e));
            }
        }

        // ---------------- One story, once an hour ----------------

        private void Step(Arc arc)
        {
            var p = arc.pawn;
            if (arc.lost || arc.def == null || p == null || (p.Discarded && !p.Dead) || arc.Stage == null)
            {
                Drop(arc);
                return;
            }
            if (p.Dead)
            {
                EndDied(arc);
                return;
            }
            if (!People.StillOurs(p) || p.IsSlave)
            {
                EndLeft(arc);
                return;
            }
            if (arc.other != null)
            {
                if (arc.other.Dead)
                {
                    Fizzle(arc, "otherdead");
                    return;
                }
                if (arc.other.Discarded || arc.other.Faction != Faction.OfPlayer || arc.other.IsPrisoner || People.Kidnapped(arc.other))
                {
                    Fizzle(arc, "otherleft");
                    return;
                }
            }
            string broken;
            try
            {
                broken = arc.def.Worker.Broken(arc);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("personal story check " + arc.def.defName, e);
                broken = null;
            }
            if (broken != null)
            {
                Fizzle(arc, broken);
                return;
            }

            // Away from home (or the other person is): the story waits, and so do its clocks.
            if (Paused(arc))
            {
                arc.stageTick += HourTicks;
                arc.pausedTicks += HourTicks;
                return;
            }
            if (DaysOfStory(arc) > arc.def.maxDays)
            {
                Fizzle(arc, "timeout");
                return;
            }
            var gate = arc.Gate;
            if (gate == null)
            {
                Complete(arc);
                return;
            }
            Watch(arc);
            float days = DaysInStage(arc);
            CountHeld(arc, gate);
            if (days >= gate.minDays && Opens(arc, gate, days)) Advance(arc);
            else if (ArcMath.TimedOut(days, gate.timeoutDays))
            {
                if (gate.timeoutPasses) Advance(arc);
                else Fizzle(arc, "timeout");
            }
        }

        public static float DaysInStage(Arc arc) => (Now - arc.stageTick) / (float)Day;

        public static float DaysOfStory(Arc arc) => (Now - arc.startedTick - arc.pausedTicks) / (float)Day;

        private static bool Paused(Arc arc)
        {
            if (arc.def.Worker.PausesAway && !ArcUtil.AtHome(arc.pawn)) return true;
            // The other person is off on a journey. (The long way home's first stage waits on its traveler, not on them.)
            return arc.other != null && !arc.OtherIsAnimal && !ArcUtil.AtHome(arc.other) && ArcUtil.AtHome(arc.pawn);
        }

        /// <summary>A fight at home while they're there: what a Battle gate waits for the end of.</summary>
        private static void Watch(Arc arc)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social != null && social.battleActive && arc.pawn.MapHeld != null && arc.pawn.MapHeld == social.battleMap) arc.sawBattle = true;
        }

        private void CountHeld(Arc arc, ArcGate gate)
        {
            if (gate.kind == ArcGateKind.Hold) arc.held = ArcMath.Held(arc.held, Holds(arc, gate));
            if (gate.or == null) return;
            while (arc.heldOr.Count < gate.or.Count) arc.heldOr.Add(0f);
            for (int i = 0; i < gate.or.Count; i++)
                if (gate.or[i]?.kind == ArcGateKind.Hold)
                    arc.heldOr[i] = ArcMath.Held(arc.heldOr[i], Holds(arc, gate.or[i]));
        }

        private bool Opens(Arc arc, ArcGate gate, float days)
        {
            if (Passes(arc, gate, -1, days)) return true;
            if (gate.or == null) return false;
            for (int i = 0; i < gate.or.Count; i++)
                if (gate.or[i] != null && days >= gate.or[i].minDays && Passes(arc, gate.or[i], i, days))
                    return true;
            return false;
        }

        // ---------------- Gates ----------------

        /// <summary>Whether this gate (or, at <paramref name="index"/> 0 and up, this alternative) is open now.</summary>
        private bool Passes(Arc arc, ArcGate g, int index, float days)
        {
            var p = arc.pawn;
            var comp = CouncilGameComponent.Instance;
            switch (g.kind)
            {
                case ArcGateKind.Time:
                    return days >= g.days;
                case ArcGateKind.Hold:
                    return HeldHours(arc, index) >= g.hours;
                case ArcGateKind.Skill:
                    return ArcUtil.Level(g.onOther ? arc.other : p, arc.skill) >= SkillTarget(arc, g);
                case ArcGateKind.Practice:
                    return PracticeDone(arc, g) >= g.amount;
                case ArcGateKind.Record:
                {
                    var rec = DefDatabase<RecordDef>.GetNamedSilentFail(g.record);
                    return rec != null && p.records != null && p.records.GetValue(rec) - arc.baseRecord >= g.amount;
                }
                case ArcGateKind.Talk:
                    if (arc.talkedTick >= arc.stageTick) return true;
                    if (g.quietAfterDays <= 0f || days < g.quietAfterDays) return false;
                    return g.anyone ? p.MapHeld != null && p.MapHeld.mapPawns.FreeColonistsSpawned.Any(o => o != p && ArcUtil.Opinion(o, p) >= 15)
                        : arc.other != null && arc.other.MapHeld != null && arc.other.MapHeld == p.MapHeld;
                case ArcGateKind.Made:
                    if (arc.madeQuality >= (int)g.quality) return true;
                    // Without the game hook, enough work at it counts instead.
                    return !Patch_ArcMade.Works && arc.skill != null && PracticeDone(arc, g) >= g.amount;
                case ArcGateKind.Home:
                    return ArcUtil.AtHome(p);
                case ArcGateKind.Battle:
                {
                    var social = comp?.social;
                    if (!arc.sawBattle || social == null || social.battleActive || p.Downed || p.InMentalState) return false;
                    float damage = p.records?.GetValue(RecordDefOf.DamageDealt) ?? 0f;
                    return damage > arc.baseDamage || (p.mindState != null && p.mindState.lastAttackTargetTick >= arc.stageTick);
                }
                case ArcGateKind.Bond:
                    return arc.other != null && p.relations != null && p.relations.DirectRelationExists(PawnRelationDefOf.Bond, arc.other);
                case ArcGateKind.Dream:
                    return comp != null && comp.dreams.recent.Any(d => d.pawn == p && d.tick >= arc.stageTick);
                case ArcGateKind.Granted:
                    return comp != null && (comp.standings.For(p, false)?.granted ?? 0) > arc.baseGranted;
                case ArcGateKind.Goal:
                    return comp != null && !arc.goal.NullOrEmpty() && comp.aspirations.Fulfilled(p).Any(a => a.def?.defName == arc.goal && a.doneTick >= arc.startedTick);
                case ArcGateKind.Tie:
                    return comp != null && arc.other != null && comp.social.Strength(p, arc.other, g.tie) >= g.amount;
                case ArcGateKind.Custom:
                    return arc.def.Worker.CustomGate(arc, g);
            }
            return false;
        }

        private static float HeldHours(Arc arc, int index) => index < 0 ? arc.held : index < arc.heldOr.Count ? arc.heldOr[index] : 0f;

        public static int SkillTarget(Arc arc, ArcGate g) => Math.Max(g.level, (g.onOther ? arc.baseOtherLevel : arc.baseLevel) + g.gain);

        public static float PracticeDone(Arc arc, ArcGate g) =>
            g.onOther ? ArcUtil.Xp(arc.other, arc.skill) - arc.baseOtherXp : ArcUtil.Xp(arc.pawn, arc.skill) - arc.baseXp;

        /// <summary>Whether a Hold gate's condition holds this hour.</summary>
        public static bool Holds(Arc arc, ArcGate g)
        {
            var p = arc.pawn;
            var o = arc.other;
            switch (g.hold)
            {
                case ArcHold.Mood:
                    return p.needs?.mood != null && p.needs.mood.CurLevelPercentage >= g.value;
                case ArcHold.Opinion:
                    return o != null && !arc.OtherIsAnimal && ArcUtil.Opinion(p, o) >= g.value && ArcUtil.Opinion(o, p) >= g.value;
                case ArcHold.NoFriction:
                {
                    if (o == null) return true;
                    int since = Now - Day * 2;
                    return !ArcUtil.RemembersSince(p, o, ArcUtil.Frictions, since) && !ArcUtil.RemembersSince(o, p, ArcUtil.Frictions, since);
                }
                case ArcHold.Near:
                    return Near(p, o);
                case ArcHold.SameMap:
                    return o != null && o.MapHeld != null && o.MapHeld == p.MapHeld;
                case ArcHold.OwnRoom:
                    return RoomUtil.PrivateRoom(p) != null;
                case ArcHold.Belonging:
                {
                    float need = ArcUtil.Need(p, HeartNeed.Belonging);
                    return need >= 0f && need >= g.value;
                }
                case ArcHold.Strain:
                    return Heart.On && Heart.Of(p) != null && Heart.StrainOf(p) <= g.value;
                case ArcHold.Trust:
                    return (CouncilGameComponent.Instance?.standings.TrustOf(p) ?? 0f) >= g.value;
                case ArcHold.AtHome:
                    return ArcUtil.AtHome(p);
            }
            return false;
        }

        /// <summary>The same room, or a few steps apart outdoors.</summary>
        private static bool Near(Pawn p, Pawn o)
        {
            if (o == null || !p.Spawned || !o.Spawned || p.Map != o.Map) return false;
            if (p.Position.InHorDistOf(o.Position, 10f)) return true;
            var room = p.GetRoom();
            return room != null && !room.PsychologicallyOutdoors && room == o.GetRoom();
        }

        // ---------------- Moving on ----------------

        /// <summary>A new stage begins: its clock, and where skills, work and records stood.</summary>
        private static void ResetStage(Arc arc)
        {
            arc.stageTick = Now;
            arc.held = 0f;
            arc.heldOr.Clear();
            arc.talkedTick = -1;
            arc.madeQuality = -1;
            arc.sawBattle = false;
            arc.baseLevel = ArcUtil.Level(arc.pawn, arc.skill);
            arc.baseOtherLevel = arc.OtherIsAnimal ? 0 : ArcUtil.Level(arc.other, arc.skill);
            arc.baseXp = ArcUtil.Xp(arc.pawn, arc.skill);
            arc.baseOtherXp = arc.OtherIsAnimal ? 0f : ArcUtil.Xp(arc.other, arc.skill);
            arc.baseGranted = CouncilGameComponent.Instance?.standings.For(arc.pawn, false)?.granted ?? 0;
            arc.baseDamage = arc.pawn.records?.GetValue(RecordDefOf.DamageDealt) ?? 0f;
            var gate = arc.Gate;
            string record = gate?.kind == ArcGateKind.Record ? gate.record : gate?.or?.FirstOrDefault(g => g?.kind == ArcGateKind.Record)?.record;
            var rec = record != null ? DefDatabase<RecordDef>.GetNamedSilentFail(record) : null;
            arc.baseRecord = rec != null && arc.pawn.records != null ? arc.pawn.records.GetValue(rec) : 0f;
        }

        private void Advance(Arc arc)
        {
            arc.stage++;
            ResetStage(arc);
            Enter(arc, false);
            if (arc.stage >= arc.def.LastStage) Complete(arc);
        }

        /// <summary>Begins a story for this candidate.</summary>
        public Arc Begin(ArcDef def, ArcCandidate c)
        {
            if (def == null || c?.pawn == null || Of(c.pawn) != null) return null;
            var arc = new Arc
            {
                def = def,
                pawn = c.pawn,
                other = c.other,
                otherName = c.other?.LabelShort,
                skill = c.skill,
                tag = c.tag,
                lostName = c.lostName,
                name = c.name,
                goal = c.goal,
                goalText = c.goalText,
                faction = c.faction,
                factionName = c.faction?.Name,
                quirk = c.quirk,
                awayDays = c.awayDays,
                startedTick = Now
            };
            arc.stage = 0;
            ResetStage(arc);
            active.Add(arc);
            Dirty();
            lastStartTick = Now;
            Enter(arc, true);
            return arc;
        }

        /// <summary>A stage reached: its effects, the journal (theirs and the other's), the chronicle and a message.</summary>
        private void Enter(Arc arc, bool start)
        {
            var comp = CouncilGameComponent.Instance;
            var st = arc.Stage;
            if (comp == null || st == null) return;
            using var _cause = Causes.Push(CauseText(arc));
            Apply(arc, st.effects);
            string line = ArcText.Line(st.journal, arc.pawn, arc, arc.other);
            if (!line.NullOrEmpty()) comp.journals.WriteText(arc.pawn, line);
            if (st.otherJournal != null && arc.other != null && !arc.OtherIsAnimal && People.HasInnerLife(arc.other) && arc.other.IsColonist)
            {
                string theirs = ArcText.Line(st.otherJournal, arc.other, arc, arc.pawn);
                if (!theirs.NullOrEmpty()) comp.journals.WriteText(arc.other, theirs);
            }
            string told = ArcText.Chronicle(st.chronicle, arc);
            if (!told.NullOrEmpty())
            {
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, told, arc.pawn));
                arc.told.Add(told);
            }
            bool last = arc.stage >= arc.def.LastStage;
            if (last) arc.lastLine = line;
            else
            {
                string head = start
                    ? "CC_Arc_Began".Translate(arc.pawn.Named("PAWN"), arc.def.label.Named("STORY")).Resolve()
                    : "CC_Arc_Moved".Translate(arc.pawn.Named("PAWN"), arc.def.label.Named("STORY"), st.label.Named("STAGE")).Resolve();
                Notify.Message(head + (line.NullOrEmpty() ? "" : " \"" + line + "\""), Look(arc), MessageTypeDefOf.SilentInput, start ? Importance.Notable : Importance.Flavor);
            }
        }

        private static string CauseText(Arc arc) => "CC_Cause_Arc".Translate(arc.pawn.LabelShort, arc.def.label).Resolve();

        private static LookTargets Look(Arc arc)
        {
            var targets = new List<Pawn>();
            if (arc.pawn != null && arc.pawn.Spawned) targets.Add(arc.pawn);
            if (arc.other != null && arc.other.Spawned) targets.Add(arc.other);
            return targets.Count == 0 ? LookTargets.Invalid : new LookTargets(targets);
        }

        private static void Apply(Arc arc, List<DilemmaEffect> effects)
        {
            if (effects.NullOrEmpty()) return;
            var ctx = Context(arc);
            foreach (var e in effects)
            {
                try
                {
                    e?.Apply(ctx);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("personal story effect " + arc.def.defName + " " + e?.GetType().Name, ex);
                }
            }
        }

        public static ArcEffectContext Context(Arc arc) => new ArcEffectContext
        {
            arc = arc,
            map = arc.pawn?.MapHeld ?? Find.AnyPlayerHomeMap,
            a = arc.pawn,
            b = arc.OtherIsAnimal ? null : arc.other,
            pet = arc.OtherIsAnimal ? arc.other : null,
            skill = arc.skill,
            faction = arc.faction,
            why = arc.def.label,
            cause = CauseText(arc)
        };

        // ---------------- Endings ----------------

        private void Remove(Arc arc, ArcOutcome outcome)
        {
            active.Remove(arc);
            Dirty();
            if (arc.def == null) return;
            past.Add(new ArcRecord
            {
                defName = arc.def.defName,
                label = arc.def.label,
                pawnId = arc.pawn?.ThingID,
                pawnName = arc.pawn?.LabelShort,
                otherName = arc.other?.LabelShort ?? arc.otherName,
                startTick = arc.startedTick,
                endTick = Now,
                outcome = outcome,
                stageLabel = arc.Stage?.label
            });
            Prune();
        }

        /// <summary>Gone without a word: its def or its colonist no longer exists.</summary>
        private void Drop(Arc arc)
        {
            active.Remove(arc);
            Dirty();
        }

        /// <summary>The last stage reached: the story ends well, with a letter that tells it from the start.</summary>
        private void Complete(Arc arc)
        {
            Remove(arc, ArcOutcome.Done);
            var p = arc.pawn;
            string text = string.Join("\n\n", arc.told);
            if (!arc.lastLine.NullOrEmpty()) text += "\n\n" + "CC_Arc_DoneQuote".Translate(p.Named("PAWN"), arc.lastLine.Named("LINE")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Arc_DoneLabel".Translate(p.LabelShort, arc.def.LabelCap).Resolve(), text, LetterDefOf.PositiveEvent, Look(arc));
        }

        /// <summary>It came to nothing, gently: a journal line, the chronicle, and whatever the story says follows.</summary>
        private void Fizzle(Arc arc, string reason)
        {
            var comp = CouncilGameComponent.Instance;
            Remove(arc, ArcOutcome.Fizzled);
            if (comp == null || arc.def == null) return;
            using var _cause = Causes.Push(CauseText(arc));
            bool otherGone = reason == "otherdead" || reason == "otherleft";
            if (!otherGone) Apply(arc, arc.def.fizzleEffects);
            string line = ArcText.Line(arc.def.fizzle, arc.pawn, arc, arc.other, reason);
            if (!line.NullOrEmpty()) comp.journals.WriteText(arc.pawn, line);
            string told = ArcText.Chronicle(arc.def.fizzleChronicle, arc, reason)
                          ?? "CC_Arc_FizzledChronicle".Translate(arc.pawn.Named("PAWN"), arc.def.label.Named("STORY")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, told, arc.pawn));
            Notify.Message("CC_Arc_Fizzled".Translate(arc.pawn.Named("PAWN"), arc.def.label.Named("STORY")).Resolve() + (line.NullOrEmpty() ? "" : " \"" + line + "\""),
                Look(arc), MessageTypeDefOf.SilentInput, Importance.Notable);
        }

        /// <summary>They died before it ended: the chronicle says so, and a day later someone close writes about it.</summary>
        private void EndDied(Arc arc)
        {
            var comp = CouncilGameComponent.Instance;
            Remove(arc, ArcOutcome.Died);
            if (comp == null) return;
            var p = arc.pawn;
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, "CC_Arc_DiedBefore".Translate(p.Named("PAWN"), arc.def.label.Named("STORY")).Resolve(), null));
            if (arc.def.eulogy == null || !arc.def.eulogy.Any) return;
            var colonists = PawnsFinder.AllMaps_FreeColonists.Where(x => x != p && People.HasInnerLife(x) && !x.DevelopmentalStage.Baby()).ToList();
            var speaker = arc.other != null && !arc.OtherIsAnimal && colonists.Contains(arc.other) ? arc.other : ArcUtil.Closest(p, colonists, 20f);
            if (speaker == null) return;
            eulogies.Add(new ArcEulogy { defName = arc.def.defName, dead = p, speaker = speaker, dueTick = Now + Day + Rand.Range(0, HourTicks * 6) });
        }

        private void Eulogy(ArcEulogy e)
        {
            var comp = CouncilGameComponent.Instance;
            var def = DefDatabase<ArcDef>.GetNamedSilentFail(e.defName);
            if (comp == null || def?.eulogy == null || e.speaker == null || e.speaker.Dead || !e.speaker.IsColonist || e.dead == null) return;
            var fake = new Arc { def = def, pawn = e.dead, other = e.speaker };
            string line = ArcText.Line(def.eulogy, e.speaker, fake, e.dead);
            if (line.NullOrEmpty()) return;
            comp.journals.WriteText(e.speaker, line);
            string text = "CC_Arc_Remembered".Translate(e.speaker.Named("PAWN"), e.dead.Named("OTHER"), def.label.Named("STORY"), line.Named("LINE")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, e.speaker));
            Notify.Message(text, e.speaker.Spawned ? new LookTargets(e.speaker) : LookTargets.Invalid, MessageTypeDefOf.SilentInput, Importance.Notable);
        }

        /// <summary>They left the colony (banished, walked out, taken): the story stops there.</summary>
        private void EndLeft(Arc arc)
        {
            var comp = CouncilGameComponent.Instance;
            Remove(arc, ArcOutcome.Left);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Arc_LeftBefore".Translate(arc.pawn.Named("PAWN"), arc.def.label.Named("STORY")).Resolve(), null));
        }

        // ---------------- Beginning ----------------

        private static bool Eligible(Pawn p) =>
            p != null && !p.Dead && People.HasInnerLife(p) && p.IsFreeNonSlaveColonist && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby();

        /// <summary>Everyone at home who could be in a story.</summary>
        public static List<Pawn> HomeColonists() =>
            PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => p.Map != null && p.Map.IsPlayerHome && Eligible(p)).Distinct().ToList();

        private static List<Pawn> AwayColonists() =>
            PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(p => Eligible(p) && !(p.MapHeld?.IsPlayerHome ?? false)).Distinct().ToList();

        /// <summary>Once a day: maybe a new story, for someone whose life gives it a reason.</summary>
        private void TryStart()
        {
            var home = HomeColonists();
            if (home.Count == 0) return;
            int now = Now;
            int frequency = (int)S.arcFrequency;
            int people = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Count(Eligible);
            float perYear = ArcMath.PerYear(frequency, people);
            int year = (int)(ArcMath.DaysPerYear * Day);
            int lastYear = past.Count(r => now - r.startTick < year) + active.Count(a => now - a.startedTick < year);
            float chance = ArcMath.DailyChance(perYear, lastYear, (now - lastStartTick) / (float)Day, active.Count, ArcMath.MaxActive(frequency, people));
            if (!Rand.Chance(chance)) return;
            var options = Candidates(home, now);
            if (options.Count == 0) return;
            var pick = options.RandomElementByWeight(o => o.weight);
            Begin(pick.def, pick.c);
        }

        /// <summary>Every story that could begin today, for whom, and how likely each is.</summary>
        public List<(ArcDef def, ArcCandidate c, float weight)> Candidates(List<Pawn> home, int now, bool ignoreCooldowns = false)
        {
            var options = new List<(ArcDef, ArcCandidate, float)>();
            List<Pawn> away = null;
            foreach (var def in DefDatabase<ArcDef>.AllDefsListForReading)
            {
                if (def.stages.NullOrEmpty() || active.Any(a => a.def == def)) continue;
                bool seenLately = past.Any(r => r.defName == def.defName && now - r.startTick < Day * 20);
                List<Pawn> pool;
                if (def.Worker.StartsAway) pool = away ??= AwayColonists();
                else pool = home;
                foreach (var p in pool)
                {
                    if (Of(p) != null || p.InMentalState || p.Downed) continue;
                    if (past.Any(r => r.pawnId == p.ThingID && r.defName == def.defName)) continue;
                    if (!ignoreCooldowns && past.Any(r => r.pawnId == p.ThingID && now - r.endTick < Day * 10)) continue;
                    if (!DilemmaRole.Fits(def.who, p, def.children)) continue;
                    ArcCandidate c;
                    try
                    {
                        c = def.Worker.Candidate(p, home);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("personal story candidates " + def.defName, e);
                        c = null;
                    }
                    if (c == null) continue;
                    c.pawn = p;
                    float w = ArcMath.CandidateWeight(def.weight * c.weight, past.Any(r => r.pawnId == p.ThingID), seenLately);
                    // Someone already in another story as the other person: a little less likely.
                    if (c.other != null && (Of(c.other) != null || active.Any(a => a.other == c.other))) w *= 0.5f;
                    if (w > 0f) options.Add((def, c, w));
                }
            }
            return options;
        }

        // ---------------- Talks and things made ----------------

        /// <summary>
        /// How likely an arc talk is between these two right now (InteractionWorker_ArcTalk): only when one of them is in
        /// a stage waiting for this very talk with the other, a day or more into it.
        /// </summary>
        public float TalkWeight(InteractionDef talk, Pawn a, Pawn b)
        {
            if (!On || talk == null || a == null || b == null) return 0f;
            return Math.Max(TalkWeightFor(Of(a), talk, b), TalkWeightFor(Of(b), talk, a));
        }

        private static float TalkWeightFor(Arc arc, InteractionDef talk, Pawn with)
        {
            var g = arc?.Gate;
            if (g == null || arc.talkedTick >= arc.stageTick) return 0f;
            var t = g.kind == ArcGateKind.Talk && g.talk == talk ? g : g.or?.FirstOrDefault(x => x?.kind == ArcGateKind.Talk && x.talk == talk);
            if (t == null || DaysInStage(arc) < t.minDays) return 0f;
            bool partner = with == arc.other || (t.anyone && ArcUtil.Opinion(with, arc.pawn) >= 15 && with.IsColonist);
            if (!partner) return 0f;
            if (ArcUtil.Opinion(arc.pawn, with) < t.minOpinion || ArcUtil.Opinion(with, arc.pawn) < t.minOpinion) return 0f;
            return 0.8f;
        }

        public void Notify_Talked(InteractionDef talk, Pawn a, Pawn b)
        {
            foreach (var (arc, with) in new[] { (Of(a), b), (Of(b), a) })
            {
                if (TalkWeightFor(arc, talk, with) <= 0f) continue;
                arc.talkedTick = Now;
                arc.talkedWith = with;
            }
        }

        /// <summary>Something with a quality was made (Patch_ArcMade): the best so far counts toward a Made gate.</summary>
        public void Notify_Made(Pawn worker, Thing thing)
        {
            if (!On) return;
            var arc = Of(worker);
            var g = arc?.Gate;
            if (g == null || !(g.kind == ArcGateKind.Made || (g.or?.Any(x => x?.kind == ArcGateKind.Made) ?? false))) return;
            if (thing.TryGetQuality(out var q) && (int)q > arc.madeQuality) arc.madeQuality = (int)q;
        }

        // ---------------- For the debug actions ----------------

        public void DebugAdvance(Arc arc)
        {
            if (arc == null || !active.Contains(arc)) return;
            if (arc.Gate == null) Complete(arc);
            else Advance(arc);
        }

        public void DebugFizzle(Arc arc)
        {
            if (arc != null && active.Contains(arc)) Fizzle(arc, "timeout");
        }
    }
}
