using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world situations ("world events") of this game: which are running, what they've changed, and when the next
    /// may come. They come on Rimfolk's own slow rhythm (about one every 30 to 60 days by default), at most two at a
    /// time and never two of one kind, and only while no fight is on at home. A game component of its own, so it
    /// keeps its own save data.
    /// </summary>
    public class WorldSituations : GameComponent
    {
        public List<WorldSituation> active = new List<WorldSituation>();
        /// <summary>The ones that have ended (newest last), for "the region remembers" and the World page.</summary>
        public List<SituationRecord> history = new List<SituationRecord>();
        private Dictionary<string, int> lastEnded = new Dictionary<string, int>();
        public int nextTick = -1;
        private int nextId = 1;

        /// <summary>An ending key that ends a situation without a letter.</summary>
        public const string Quietly = "__quiet";

        public const int MaxActive = 2;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        private static WorldSituations cached;
        private static Game cachedGame;

        public static WorldSituations Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WorldSituations>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public WorldSituations(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Changed();
        }

        public WorldSituation Get(int id) => active.FirstOrDefault(s => s.id == id);

        public IEnumerable<WorldSituation> Running => active.Where(s => !s.ended);

        public WorldSituation ActiveOf(WorldSituationDef def) => active.FirstOrDefault(s => !s.ended && s.def == def);

        /// <summary>A people already at the heart of a running situation (one thing at a time for them).</summary>
        public bool Busy(Faction f) => f != null && active.Any(s => !s.ended && s.ctx.faction == f);

        /// <summary>Another running situation still needs this person.</summary>
        public bool HoldsElsewhere(WorldSituation except, Pawn p) => active.Any(s => s != except && !s.ended && (s.held.Contains(p) || s.ctx.stranger == p || s.ctx.other == p));

        /// <summary>A running situation still needs this person (the warlord, a healer): not for the rest of Rimfolk to let go of.</summary>
        public static bool Holds(Pawn p) => p != null && (Instance?.HoldsElsewhere(null, p) ?? false);

        /// <summary>The colony helped this people in an earlier world event (or in this one, when it's over).</summary>
        public bool Helped(Faction f) => f != null && history.Any(r => r.faction == f && r.helped);

        // ---------------- The rhythm ----------------

        public override void GameComponentTick()
        {
            int t = Now;
            if (t % 250 == 37 && active.Count > 0)
            {
                try
                {
                    foreach (var s in active)
                        if (!s.ended) Keep(s);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world situation people", e);
                }
            }
            if (t % GenDate.TicksPerHour != 1203) return;
            try
            {
                Hourly(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situations", e);
            }
        }

        /// <summary>
        /// Keeps the people a situation made (the warlord, a healer, the claimants) from the world's clean-up while it
        /// runs, also while they're on a map: a raider or visitor goes back to the world the moment they walk off it,
        /// and the clean-up could take them before anyone looked again. The colony's own people and prisoners aren't
        /// the world's to keep. Let go when it ends (<see cref="WorldSituation.ReleaseAll"/>).
        /// </summary>
        private static void Keep(WorldSituation s)
        {
            foreach (var p in s.held)
            {
                if (p == null || p.Dead || p.Discarded || p.Faction == Faction.OfPlayer || p.IsPrisoner) continue;
                if (p.Spawned || Find.WorldPawns.Contains(p) && p.GetCaravan() == null) Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            }
        }

        private void Hourly(int now)
        {
            bool daily = now % GenDate.TicksPerDay < GenDate.TicksPerHour;
            foreach (var s in active.ToList())
            {
                if (s.ended)
                {
                    active.Remove(s);
                    continue;
                }
                // Switched off: whatever is running ends quietly, and nothing new comes.
                if (!S.worldSituations)
                {
                    End(s, Quietly);
                    continue;
                }
                Tick(s, now, daily);
            }
            if (!S.worldSituations) return;
            // The first comes a little sooner than the rest, once the colony has found its feet.
            if (nextTick < 0) nextTick = Mathf.Max(now, GenDate.TicksPerDay * 12) + Mathf.RoundToInt(Interval() * 0.6f);
            if (now < nextTick || active.Count(s => !s.ended) >= MaxActive) return;
            var map = HappeningTracker.HomeMap();
            if (map == null) return;
            // Not in the middle of a fight, and not while the last one's question is still open.
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false) || Waiting)
            {
                nextTick = now + GenDate.TicksPerHour * 6;
                return;
            }
            nextTick = now + GenDate.TicksPerDay;
            if (TryStart(map, null, false) != null) nextTick = now + Interval();
        }

        /// <summary>A world event's question nobody has answered yet.</summary>
        public static bool Waiting => Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Situation c && !c.resolved);

        /// <summary>The setting, varied: by default 30 to 60 days. A second one at once is less likely to come soon.</summary>
        private int Interval()
        {
            float days = Mathf.Max(5f, S.daysBetweenWorldSituations) * Rand.Range(0.67f, 1.33f);
            if (active.Any(s => !s.ended)) days *= 1.25f;
            return Mathf.RoundToInt(days * GenDate.TicksPerDay);
        }

        private void Tick(WorldSituation s, int now, bool daily)
        {
            var worker = s.Worker;
            if (worker == null || s.def == null)
            {
                End(s, Quietly);
                return;
            }
            // Someone its letters name was lost from the save (a pawn gone, a people's mod removed): it ends without a letter.
            if (s.Num("hasX") == 1 && s.ctx.stranger == null || s.Num("hasOther") == 1 && s.ctx.other == null || s.Num("hasEnemy") == 1 && s.ctx.enemy == null)
            {
                End(s, Quietly);
                return;
            }
            if (s.ctx.map != null && !Find.Maps.Contains(s.ctx.map)) s.ctx.map = HappeningTracker.HomeMap();
            string ending = null;
            try
            {
                ending = worker.Hourly(s);
                if (ending == null && daily) ending = worker.Daily(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation " + s.def.defName, e);
            }
            if (ending != null)
            {
                End(s, ending);
                return;
            }
            if (s.pendingTick > 0 && now >= s.pendingTick && !s.pendingEnding.NullOrEmpty())
            {
                End(s, s.pendingEnding);
                return;
            }
            if (now >= s.endTick)
            {
                SituationEnding e = null;
                try
                {
                    e = worker.TimeoutEnding(s);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("world situation ending " + s.def.defName, ex);
                }
                End(s, e?.key ?? Quietly);
                return;
            }
            Beats(s, now);
        }

        // ---------------- Starting ----------------

        /// <summary>
        /// Starts a situation that fits now (one of those <paramref name="only"/> allows). With <paramref name="force"/>
        /// (dev mode), the settings, the day, cooldowns and the limit of two don't apply.
        /// </summary>
        public WorldSituation TryStart(Map map, Func<WorldSituationDef, bool> only, bool force)
        {
            if (map == null) return null;
            int day = Now / GenDate.TicksPerDay;
            var pool = new List<WorldSituationDef>();
            foreach (var d in DefDatabase<WorldSituationDef>.AllDefsListForReading)
            {
                if (only != null && !only(d)) continue;
                if (active.Any(s => !s.ended && (s.def == d || (!d.kind.NullOrEmpty() && s.def.kind == d.kind)))) continue;
                if (!force)
                {
                    if (day < d.earliestDay) continue;
                    if (lastEnded.TryGetValue(d.defName, out int t) && Now - t < d.cooldownDays * GenDate.TicksPerDay) continue;
                }
                try
                {
                    if (d.Worker.CanStart(map)) pool.Add(d);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world situation check " + d.defName, e);
                }
            }
            while (pool.Count > 0)
            {
                var def = pool.RandomElementByWeight(Weight);
                pool.Remove(def);
                var s = Start(def, map);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>Ones that ran recently (or never) weigh a little less (or more), for variety over a long game.</summary>
        private float Weight(WorldSituationDef d)
        {
            float w = Mathf.Max(0.01f, d.weight);
            int times = history.Count(r => r.defName == d.defName);
            return w / (1f + times * 0.5f);
        }

        private WorldSituation Start(WorldSituationDef def, Map map)
        {
            var s = new WorldSituation { id = nextId++, def = def, startTick = Now };
            s.ctx.situationId = s.id;
            s.ctx.map = map;
            var worker = def.Worker;
            try
            {
                if (!worker.Cast(s, map))
                {
                    worker.Abort(s);
                    return null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation start " + def.defName, e);
                worker.Abort(s);
                return null;
            }
            s.endTick = s.startTick + Mathf.RoundToInt(def.durationDays.RandomInRange * GenDate.TicksPerDay);
            // Who its letters name from the start, so it can end quietly if one is lost from the save later.
            if (s.ctx.stranger != null) s.SetNum("hasX", 1);
            if (s.ctx.other != null) s.SetNum("hasOther", 1);
            if (s.ctx.enemy != null) s.SetNum("hasEnemy", 1);
            active.Add(s);
            string label = worker.Line(def.titles, s) ?? def.LabelCap.ToString();
            string cause = "CC_WS_Cause".Translate(def.LabelCap).Resolve();
            s.ctx.why = label;
            s.ctx.cause = cause;
            try
            {
                using (Causes.Push(cause))
                {
                    worker.Begin(s);
                    foreach (var e in def.effects) HappeningTracker.Safe(e, s.ctx);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation begin " + def.defName, e);
            }
            // The beats' days, from the start.
            foreach (var b in def.beats)
            {
                if (b.called) continue;
                int due = s.startTick + Mathf.RoundToInt(b.day.RandomInRange * GenDate.TicksPerDay);
                if (due < s.endTick - GenDate.TicksPerHour * 12) s.beats.Add(new BeatState { key = b.key, due = due });
            }
            try
            {
                string body = worker.Line(def.texts, s) ?? "";
                string text = body;
                string extra = worker.StartExtra(s);
                if (!extra.NullOrEmpty()) text += "\n\n" + extra;
                string summary = WorldSituationsPanel.Summary(s);
                if (!summary.NullOrEmpty()) text += "\n\n" + "CC_WS_ForTheColony".Translate(summary, TextUtil.DaysString(s.Duration)).Resolve();
                var letterDef = def.letter ?? (def.bad ? WorldSituationDefOf.Council_WorldNewsBad : WorldSituationDefOf.Council_WorldNews);
                Find.LetterStack.ReceiveLetter(label, text, letterDef, worker.StartLook(s));
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World,
                    "CC_WS_ChronicleStart".Translate(label, FirstSentence(body)).Resolve(), s.ctx.a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation letter " + def.defName, e);
            }
            Changed();
            return s;
        }

        private static string FirstSentence(string text)
        {
            if (text.NullOrEmpty()) return "";
            int cut = text.IndexOfAny(new[] { '.', '!', '?' });
            if (cut > 0 && cut < 240) return text.Substring(0, cut + 1);
            return text.Length > 240 ? text.Substring(0, 240) + "..." : text;
        }

        // ---------------- Beats ----------------

        private void Beats(WorldSituation s, int now)
        {
            foreach (var bs in s.beats.Where(b => now >= b.due).ToList())
            {
                var beat = s.def.Beat(bs.key);
                if (beat == null)
                {
                    s.beats.Remove(bs);
                    continue;
                }
                var map = s.ctx.map != null && Find.Maps.Contains(s.ctx.map) ? s.ctx.map : HappeningTracker.HomeMap();
                if (map == null) return;
                s.ctx.map = map;
                bool retry;
                bool can;
                try
                {
                    can = s.Worker.BeatCanHappen(s, beat, map, out retry);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world situation beat " + s.def.defName + "/" + beat.key, e);
                    can = false;
                    retry = false;
                }
                // One question at a time from the world: a beat with a question waits for the last one's answer.
                if (can && !beat.options.NullOrEmpty() && Waiting)
                {
                    can = false;
                    retry = true;
                }
                if (!can)
                {
                    if (retry && bs.postponed < 8 && now + GenDate.TicksPerHour * 6 < s.endTick)
                    {
                        bs.postponed++;
                        bs.due = now + GenDate.TicksPerHour * 6;
                    }
                    else s.beats.Remove(bs);
                    continue;
                }
                s.beats.Remove(bs);
                if (beat.chance < 1f && !Rand.Chance(beat.chance)) continue;
                bool happened = Run(s, beat, map);
                bs.times += happened ? 1 : 0;
                // It comes back, while there's time and it hasn't come too often.
                if (beat.repeatDays.max > 0f && bs.times < beat.maxTimes)
                {
                    int due = now + Mathf.RoundToInt(beat.repeatDays.RandomInRange * GenDate.TicksPerDay);
                    if (due < s.endTick - GenDate.TicksPerHour * 12) s.beats.Add(new BeatState { key = beat.key, due = due, times = bs.times });
                }
            }
        }

        /// <summary>Brings a beat on now, or after some days (an answer's next step, or the worker's).</summary>
        public void Call(WorldSituation s, string key, float days)
        {
            if (s == null || s.ended || s.def.Beat(key) == null) return;
            s.beats.RemoveAll(b => b.key == key);
            s.beats.Add(new BeatState { key = key, due = Now + Mathf.RoundToInt(Mathf.Max(0f, days) * GenDate.TicksPerDay) });
        }

        private bool Run(WorldSituation s, SituationBeat beat, Map map)
        {
            var worker = s.Worker;
            var ctx = s.ctx.Copy();
            ctx.map = map;
            try
            {
                if (!worker.CastBeat(s, beat, ctx, map)) return false;
                if (beat.count.max > 0)
                {
                    float n = beat.count.RandomInRange * (beat.wealthScaled ? LivingEvents.WealthFactor(map) : 1f);
                    ctx.count = VarietyUtil.Round(Mathf.Max(1, Mathf.RoundToInt(n)));
                }
                if (!worker.PrepareBeat(s, beat, ctx)) return false;
                // The beat's own person stays the situation's from now on (the doctor asked for is the one remembered).
                s.ctx.a = ctx.a;
                s.ctx.count = ctx.count;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation beat " + s.def.defName + "/" + beat.key, e);
                return false;
            }
            string label = worker.Line(beat.titles, s, ctx) ?? s.Label;
            string text = worker.Line(beat.texts, s, ctx) ?? "";
            ctx.why = label;
            ctx.cause = "CC_WS_Cause".Translate(s.def.LabelCap).Resolve();
            if (beat.effects != null)
                using (Causes.Push(ctx.cause))
                {
                    foreach (var e in beat.effects) HappeningTracker.Safe(e, ctx);
                }
            try
            {
                worker.BeatHappened(s, beat, ctx);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation beat " + s.def.defName + "/" + beat.key, e);
            }
            var look = Look(ctx);
            if (beat.options.NullOrEmpty())
            {
                if (text.NullOrEmpty()) return true;
                if (beat.message) Notify.Message(text, look, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                else Find.LetterStack.ReceiveLetter(label, text, beat.letter ?? WorldSituationDefOf.Council_WorldNews, look);
                return true;
            }
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, beat.answerDays) * GenDate.TicksPerDay);
            var def = beat.options[Mathf.Clamp(beat.defaultOption, 0, beat.options.Count - 1)];
            text += "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(def.label)).Resolve();
            var letter = (ChoiceLetter_Situation)LetterMaker.MakeLetter(label, text, beat.threat ? WorldSituationDefOf.Council_WorldChoiceThreat : WorldSituationDefOf.Council_WorldChoice, look);
            letter.ctx = ctx;
            letter.beatKey = beat.key;
            letter.StartTimeout(ticks);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }

        private static LookTargets Look(SituationContext ctx)
        {
            var targets = new List<GlobalTargetInfo>();
            foreach (var p in new[] { ctx.stranger, ctx.other, ctx.a })
                if (p != null && p.Spawned) targets.Add(p);
            return targets.Count > 0 ? new LookTargets(targets) : LookTargets.Invalid;
        }

        // ---------------- Answers ----------------

        public void Resolve(ChoiceLetter_Situation letter, int index, bool byDefault)
        {
            if (letter == null || letter.resolved) return;
            letter.resolved = true;
            var ctx = letter.ctx;
            var s = Get(ctx?.situationId ?? -1);
            var beat = s?.def?.Beat(letter.beatKey);
            if (s == null || s.ended || beat == null || beat.options.NullOrEmpty()) return;
            var worker = s.Worker;
            int i = Mathf.Clamp(index, 0, beat.options.Count - 1);
            var o = beat.options[i];
            var comp = CouncilGameComponent.Instance;
            try
            {
                ctx.map = ctx.map != null && Find.Maps.Contains(ctx.map) ? ctx.map : HappeningTracker.HomeMap();
                ctx.cause = "CC_Cause_Event".Translate(s.def.LabelCap, ctx.Format(o.label)).Resolve();
                using var _cause = Causes.Push(ctx.cause);
                bool risk = o.riskChance > 0f && Rand.Chance(worker.RiskChance(s, ctx, o));
                if (!risk || !o.riskReplaces)
                {
                    foreach (var e in o.effects ?? new List<DilemmaEffect>())
                        if (!byDefault || !e.onlyIfChosen) HappeningTracker.Safe(e, ctx);
                    if (o.flags != null) foreach (var f in o.flags) s.Set(f);
                    if (o.clears != null) foreach (var f in o.clears) s.Clear(f);
                    s.help += o.help;
                    if (!o.deed.NullOrEmpty()) s.deeds.Add(ctx.Format(o.deed));
                    if (!o.next.NullOrEmpty() && !(risk && !o.riskNext.NullOrEmpty())) Call(s, o.next, o.nextDays.RandomInRange);
                    if (!o.end.NullOrEmpty())
                    {
                        s.pendingEnding = o.end;
                        s.pendingTick = Now + Mathf.RoundToInt(o.endDays.RandomInRange * GenDate.TicksPerDay);
                    }
                }
                string outcome = o.outcome;
                if (risk)
                {
                    if (o.riskEffects != null)
                        foreach (var e in o.riskEffects)
                            if (!byDefault || !e.onlyIfChosen) HappeningTracker.Safe(e, ctx);
                    if (o.riskFlags != null) foreach (var f in o.riskFlags) s.Set(f);
                    if (!o.riskNext.NullOrEmpty()) Call(s, o.riskNext, o.nextDays.RandomInRange);
                    if (!o.riskOutcome.NullOrEmpty()) outcome = o.riskOutcome;
                }
                outcome = worker.Chosen(s, ctx, beat, i, risk, byDefault, outcome);
                comp?.character.Shift(byDefault ? o.ways?.Scaled(0.5f) : o.ways, "CC_Ways_Why_Event".Translate(s.def.label, ctx.Format(o.label)).Resolve());
                string told = WorldSituationWorker.Sentences(ctx.Format(outcome));
                if (byDefault) told = "CC_Evt_NoAnswer".Translate() + " " + told;
                if (!told.NullOrEmpty())
                    Messages.Message(told, new LookTargets(new[] { ctx.a, ctx.stranger, ctx.other }.Where(p => p != null && p.Spawned)), MessageTypeDefOf.NeutralEvent, false);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_Chronicle_EventChoice".Translate(s.def.LabelCap, ctx.Format(o.label)).Resolve(), ctx.a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world situation answer " + s.def.defName, e);
            }
            finally
            {
                // The letter's text is written; let go of the people so the save doesn't hold them twice.
                ctx.a = ctx.b = ctx.stranger = ctx.other = null;
                ctx.map = null;
            }
            Changed();
        }

        // ---------------- Ending ----------------

        /// <summary>Ends a situation with one of its endings (its letter, effects and chronicle), or quietly.</summary>
        public void End(WorldSituation s, string key)
        {
            if (s == null || s.ended) return;
            s.ended = true;
            s.endingKey = key;
            active.Remove(s);
            var def = s.def;
            var worker = s.Worker;
            var ending = def?.Ending(key);
            int days = Mathf.Max(1, Mathf.RoundToInt((Now - s.startTick) / (float)GenDate.TicksPerDay));
            var comp = CouncilGameComponent.Instance;
            if (def != null && worker != null)
            {
                var map = s.ctx.map != null && Find.Maps.Contains(s.ctx.map) ? s.ctx.map : HappeningTracker.HomeMap();
                s.ctx.map = map;
                // Silver the colony is owed (a cart's takings, a share of a strike) comes now, and the letter can say how much.
                int owed = s.Num("owed");
                if (owed > 0 && ending != null) s.ctx.count = owed;
                if (ending != null && map != null)
                {
                    try
                    {
                        if (owed > 0) VarietyUtil.PaySilver(map, IntVec3.Invalid, owed);
                        string cause = "CC_WS_Cause".Translate(def.LabelCap).Resolve();
                        s.ctx.cause = cause;
                        using (Causes.Push(cause))
                        {
                            if (ending.effects != null)
                                foreach (var e in ending.effects) HappeningTracker.Safe(e, s.ctx);
                        }
                        string label = worker.Line(ending.titles, s) ?? s.Label;
                        string text = worker.Line(ending.texts, s) ?? "";
                        if (s.deeds.Count > 0) text += "\n\n" + "CC_WS_YourPart".Translate(TextUtil.JoinAnd(s.deeds.Distinct().ToList())).Resolve();
                        var look = new LookTargets(new[] { s.ctx.stranger, s.ctx.other }.Where(p => p != null && p.Spawned));
                        Find.LetterStack.ReceiveLetter(label, text, ending.letter ?? WorldSituationDefOf.Council_WorldNews, look);
                        string line = ending.chronicle.NullOrEmpty() ? Flavor.Strip(FirstSentence(text)) : WorldSituationWorker.Sentences(s.ctx.Format(ending.chronicle));
                        string part = s.deeds.Count > 0 ? " " + "CC_WS_ChroniclePart".Translate(TextUtil.JoinAnd(s.deeds.Distinct().ToList())).Resolve() : "";
                        comp?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_WS_ChronicleEnd".Translate(s.Label, TextUtil.DaysString(days * GenDate.TicksPerDay), line).Resolve() + part, s.ctx.a));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("world situation end " + def.defName, e);
                    }
                }
                else
                {
                    // Ended without its letter: the silver still comes, since the colony's goods already went out for it.
                    if (owed > 0 && map != null)
                    {
                        try
                        {
                            VarietyUtil.PaySilver(map, IntVec3.Invalid, owed);
                            Notify.Message("CC_WS_OwedPaid".Translate(owed, s.Label).Resolve(), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("world situation owed " + def.defName, e);
                        }
                    }
                    if (key == Quietly)
                        comp?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_WS_ChronicleQuiet".Translate(s.Label, TextUtil.DaysString(days * GenDate.TicksPerDay)).Resolve(), null));
                }
                try
                {
                    worker.Ended(s, ending);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world situation ended " + def.defName, e);
                }
                lastEnded[def.defName] = Now;
                history.Add(new SituationRecord
                {
                    defName = def.defName, label = s.Label, faction = s.ctx.faction, ending = key, help = s.help,
                    helped = ending != null && ending.helped, startTick = s.startTick, endTick = Now
                });
                while (history.Count > 40) history.RemoveAt(0);
            }
            // Its questions can't be answered any more.
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Situation>().Where(l => !l.resolved && l.ctx?.situationId == s.id).ToList())
            {
                l.resolved = true;
                Find.LetterStack.RemoveLetter(l);
            }
            s.ReleaseAll();
            s.ctx.a = s.ctx.b = null;
            s.sufferers.Clear();
            Changed();
        }

        // ---------------- What they change ----------------

        private static int version;
        private static int cacheVersion = -1;
        private static int cacheHour = -1;
        // Rimfolk's events and the game's own are weighed apart (a Rimfolk event's own weight passes through the game's too).
        private static readonly Dictionary<IncidentDef, float> eventCache = new Dictionary<IncidentDef, float>();
        private static readonly Dictionary<IncidentDef, float> incidentCache = new Dictionary<IncidentDef, float>();
        private static bool anyVanilla;

        /// <summary>Something changed (a situation began or ended, a flag): weights are worked out again.</summary>
        public static void Changed() => version++;

        private static void Refresh()
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (cacheVersion == version && cacheHour == hour) return;
            cacheVersion = version;
            cacheHour = hour;
            eventCache.Clear();
            incidentCache.Clear();
            var inst = Instance;
            anyVanilla = inst != null && S != null && S.worldSituations && inst.active.Any(s => !s.ended && s.def != null && s.def.modifiers.Any(m => !m.incident.NullOrEmpty()));
        }

        private static float Factor(IncidentDef def, HappeningExtension ext)
        {
            var inst = Instance;
            if (def == null || inst == null || S == null || !S.worldSituations || inst.active.Count == 0) return 1f;
            Refresh();
            var cache = ext != null ? eventCache : incidentCache;
            if (cache.TryGetValue(def, out float f)) return f;
            f = 1f;
            foreach (var s in inst.active)
            {
                if (s.ended || s.Worker == null) continue;
                try
                {
                    f *= s.Worker.Factor(s, def, ext);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world situation factor " + s.def.defName, e);
                }
            }
            f = Mathf.Clamp(f, 0.05f, 6f);
            cache[def] = f;
            return f;
        }

        /// <summary>How the running world situations change one of Rimfolk's events' odds (1: not at all).</summary>
        public static float EventFactor(IncidentDef def, HappeningExtension ext) => ext == null ? 1f : Factor(def, ext);

        /// <summary>How the running world situations change one of the game's own incidents' odds (1: not at all).</summary>
        public static float IncidentFactor(IncidentDef def)
        {
            if (def == null || Current.Game == null) return 1f;
            Refresh();
            if (!anyVanilla) return 1f;
            return Factor(def, null);
        }

        /// <summary>The strength of a people's raids while a situation (a warlord's rise) lasts.</summary>
        public static float RaidPointsFactor(Faction f)
        {
            var inst = Instance;
            if (f == null || inst == null || S == null || !S.worldSituations) return 1f;
            float factor = 1f;
            foreach (var s in inst.active)
                if (!s.ended && s.Worker != null)
                    factor *= s.Worker.RaidPointsFactor(s, f);
            return Mathf.Clamp(factor, 0.5f, 2f);
        }

        /// <summary>A people whose raids come more often while a situation lasts (or null).</summary>
        public static Faction RaidFaction()
        {
            var inst = Instance;
            if (inst == null || S == null || !S.worldSituations) return null;
            foreach (var s in inst.active)
            {
                if (s.ended || s.Worker == null) continue;
                var f = s.Worker.RaidFaction(s);
                if (f != null) return f;
            }
            return null;
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref active, "active", LookMode.Deep);
            Scribe_Collections.Look(ref history, "history", LookMode.Deep);
            Scribe_Collections.Look(ref lastEnded, "lastEnded", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active == null) active = new List<WorldSituation>();
                // A situation whose mod part is gone can't go on.
                active.RemoveAll(s => s == null || s.def == null || s.ended);
                if (history == null) history = new List<SituationRecord>();
                history.RemoveAll(r => r == null);
                if (lastEnded == null) lastEnded = new Dictionary<string, int>();
                Changed();
            }
        }
    }

    [DefOf]
    public static class WorldSituationDefOf
    {
        public static LetterDef Council_WorldNews;
        public static LetterDef Council_WorldNewsBad;
        public static LetterDef Council_WorldChoice;
        public static LetterDef Council_WorldChoiceThreat;

        static WorldSituationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WorldSituationDefOf));
        }
    }
}
