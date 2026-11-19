using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 49. The tide read (the Stormglass Conclave) =====================

    /// <summary>
    /// The tide read (item 49): while the Conclave watches a home's sky (their season's watch, Peoples/PeoplesE.cs), the game's
    /// psychic drone or soothe the storyteller sends there is held back in the game's own queue a day or so, as a condition of
    /// the sky is (Patch_TideHold beside Patch_GlassHold), one held at a time. The game picks whom such a tide touches (one sex,
    /// by a colonist of the colony) when it comes; here it is picked when it is held, kept, and given to the tide when it comes
    /// (Patch_TideTouches), so the vial-runner's warning can name everyone it will touch. When a held drone comes, a reader of
    /// the Conclave comes to the gate to sit an hour with the one it touches worst (their mood the lowest), or with the two
    /// worst one after the other (round 1's visitor who works on a colonist): a calm hour remembered while the drone lasts.
    /// </summary>
    public static class TideRead
    {
        public const string RunnerKind = "pev:tiderunner";
        public const string EventDef = "Rimfolk_Evt_TideReader";

        private static int Now => Find.TickManager.TicksGame;

        public static JobDef SitJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_SitWith");

        public static ThoughtDef SatThought => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_SatThroughDrone");

        /// <summary>The colony's people at home the tide will touch: its free colonists of that sex.</summary>
        public static List<Pawn> Touched(Map map, Gender g) =>
            map == null ? new List<Pawn>() : map.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Dead && p.gender == g && p.RaceProps.Humanlike).ToList();

        /// <summary>
        /// The storyteller sends a drone or a soothe (Patch_TideHold): while a watch runs on its home and nothing is held already,
        /// it goes into the game's own queue a day or so on, the sex it will touch picked now as the game picks it, and the
        /// runner sets off with the warning. True: held (the storyteller doesn't fire it now).
        /// </summary>
        public static bool Hold(FiringIncident fi)
        {
            if (fi?.def == null || fi.parms == null || !PeopleEvents6Math.Tide(fi.def.defName) || fi.sourceQuestPart != null || fi.parms.quest != null) return false;
            var map = fi.parms.target as Map;
            var watch = GlassWatch.Of(map);
            var b = PeopleEvents6Book.Instance;
            if (watch == null || watch.due > 0 || b == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsCount == 0) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms)) return false;
            var g = map.mapPawns.FreeColonists.RandomElement().gender;
            if (g == Gender.None || Touched(map, g).Count == 0) return false;
            int at = Now + PeoplesEMath.HoldHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, PeoplesEMath.RetryHours * GenDate.TicksPerHour))) return false;
            watch.due = at;
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.GetUniqueLoadID() == watch.text && PeoplesD.Theirs(x, PeoplesMath.Seers))
                    ?? PeoplesD.Friendly(PeoplesMath.Seers);
            b.AddTide(new TideRecord { map = map, faction = f, def = fi.def.defName, gender = (int)g, due = at });
            Runner(map, f, fi.def, g);
            return true;
        }

        private static string What(IncidentDef def) => (def?.gameCondition?.label ?? def?.label ?? "?").ToString();

        private static string Sex(Gender g) => (g == Gender.Female ? "CC_PEv6_TideWomen" : "CC_PEv6_TideMen").Translate().Resolve();

        /// <summary>The vial-runner with the warning: the letter at once naming everyone it will touch, and the runner at the gate to say it.</summary>
        private static void Runner(Map map, Faction f, IncidentDef def, Gender g)
        {
            string what = What(def);
            Pawn runner = null;
            try
            {
                var kind = f == null ? null : Arrivals.PersonKind(f);
                runner = kind == null ? null : Arrivals.Make(kind, f, new FloatRange(16f, 30f));
                if (runner != null && Arrivals.Visit(new List<Pawn> { runner }, map, f, PeoplesD.GateSpot(map), 4f) == null)
                {
                    Arrivals.Discard(runner);
                    runner = null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the tide's runner", e);
                runner = null;
            }
            if (runner != null)
            {
                var r = CouncilGameComponent.Instance?.happenings?.AddVisit(RunnerKind, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), map, runner, null, null, Now + 10 * GenDate.TicksPerHour);
                if (r != null)
                {
                    r.text = what;
                    r.stake = (int)g;
                }
            }
            var touched = Touched(map, g);
            string word = Peoples.Courier(f, false) ?? "CC_PeoplesE_GlassRunnerNoun".Translate().Resolve();
            string names = TextUtil.JoinAnd(touched.Select(p => p.LabelShort).ToList());
            string text = (runner != null ? "CC_PEv6_TideHeld" : "CC_PEv6_TideHeldWord")
                .Translate((runner?.LabelShort ?? "?").Named("RUNNER"), word.Named("WORD"), (f?.Name ?? "?").Named("FACTION"), what.Named("CONDITION"), Sex(g).Named("SEX"),
                    names.Named("NAMES")).Resolve();
            if (PeopleEvents6Math.Drone(def.defName)) text += "\n\n" + "CC_PEv6_TideReaderComes".Translate().Resolve();
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PEv6_TideHeldLabel".Translate(what.CapitalizeFirst()).Resolve(), text, LetterDefOf.NeutralEvent,
                runner != null ? new LookTargets(runner) : new LookTargets(touched), f);
        }

        /// <summary>The runner's hour: at the gate, the warning said over their head, and they go.</summary>
        public static bool RunnerHour(Visit v, int now)
        {
            var x = v.x;
            if (v.done || now > v.until || x == null || x.Dead || !x.Spawned || x.Map != v.map) return false;
            var gate = PeoplesD.GateSpot(v.map);
            if (gate.HasValue && !x.Position.InHorDistOf(gate.Value, 10f)) return true;
            // apart: the runner's warning said over their head at the gate, to whoever is there; it names no one.
            Speech.Say(x, "CC_PEv6_TideRunnerSays".Translate((v.text ?? "?").CapitalizeFirst(), Sex((Gender)v.stake)).Resolve());
            return false;
        }

        /// <summary>
        /// The tide comes out of the game's queue (Patch_TideTouches): the sex picked when it was held is the one it touches.
        /// A held drone that comes is marked for the reader.
        /// </summary>
        public static void Touches(IncidentDef def, Map map, ref Gender gender)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null || def == null || map == null) return;
            int now = Now;
            var r = b.tides.FirstOrDefault(t => t != null && !t.came && !t.done && t.map == map && t.def == def.defName && now >= t.due - GenDate.TicksPerHour
                                                && now <= t.due + PeopleEvents6Math.TideForgetDays * GenDate.TicksPerDay);
            if (r == null) return;
            r.came = true;
            r.due = now;
            if (r.gender != (int)Gender.None) gender = (Gender)r.gender;
            if (!PeopleEvents6Math.Drone(r.def)) r.done = true;
        }

        /// <summary>The events' hour: a held drone that came brings the reader (once); one that never came is let go.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null) return;
            foreach (var r in b.tides.Where(t => t != null && !t.done).ToList())
            {
                if (r.map == null || !Find.Maps.Contains(r.map) || !r.came && now > r.due + PeopleEvents6Math.TideForgetDays * GenDate.TicksPerDay)
                {
                    r.done = true;
                    continue;
                }
                if (!r.came || now < r.due + GenDate.TicksPerHour) continue;
                if (Drone(r.map) == null)
                {
                    r.done = true;
                    continue;
                }
                if (!Tell.CanAsk || MapFacts.ThreatOn(r.map)) continue;
                var f = r.faction != null && PeoplesD.Theirs(r.faction, PeoplesMath.Seers) && !r.faction.HostileTo(Faction.OfPlayer) ? r.faction : PeoplesD.Friendly(PeoplesMath.Seers);
                r.done = true;
                if (f != null) PeopleEvents2.Fire(EventDef, r.map, f);
            }
        }

        /// <summary>The drone on this map now, or null.</summary>
        public static GameCondition_PsychicEmanation Drone(Map map) =>
            map?.gameConditionManager?.ActiveConditions?.OfType<GameCondition_PsychicEmanation>().FirstOrDefault(c => c.def == GameConditionDefOf.PsychicDrone);

        /// <summary>Those the drone touches, worst first (the lowest mood first).</summary>
        public static List<Pawn> Worst(Map map)
        {
            var d = Drone(map);
            if (d == null) return new List<Pawn>();
            var list = Touched(map, d.gender).Where(p => p.needs?.mood != null && !p.Downed && !p.InMentalState && !p.Drafted).ToList();
            var order = new List<Pawn>();
            while (list.Count > 0)
            {
                int i = PeopleEvents6Math.Worst(list.Select(p => p.needs.mood.CurLevel).ToArray());
                if (i < 0) break;
                order.Add(list[i]);
                list.RemoveAt(i);
            }
            return order;
        }

        /// <summary>Dev mode: a drone is held as if the watch had read it (a watch begun here first, if none).</summary>
        public static string Soon(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Seers);
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("PsychicDrone");
            if (map == null || f == null || def == null) return "no Conclave at peace.";
            if (GlassWatch.Of(map) == null) GlassWatch.Begin(map, f, null);
            var w = GlassWatch.Of(map);
            if (w != null) w.due = -1;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            return Hold(new FiringIncident(def, null, parms)) ? "a drone is held; the runner is on the way." : "the drone can't come now (one already on, or nobody here).";
        }

        /// <summary>The reader's hour with one of the colony done: the calm hour remembered; then the next of the two, if the colony asked for both.</summary>
        public static void Sat(Visit v, Pawn reader, Pawn on)
        {
            if (on == null || on.Dead) return;
            Calm(reader, on);
            var next = v?.map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.GetUniqueLoadID() == PeopleWorkOn.Note(v));
            if (next != null && next != on && reader != null && PeoplesG.Up(reader, v.map) && PeoplesG.Up(next, v.map) && SitJob != null)
            {
                var ctx = new HappeningContext { map = v.map, incident = v.incident, faction = reader.Faction, stranger = reader };
                if (PeopleWorkOn.Begin(ctx, reader, next, SitJob, LocalTargetInfo.Invalid, null, 0, PeopleEvents6Math.SitTicks, 4f)) return;
            }
            var f = reader?.Faction;
            if (PeopleEvents6.Thanks(f)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.SitGoodwill, GoodwillWhy.Helped);
            if (reader != null) PeoplesG.SayLine(reader, v?.incident ?? DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "done");
            if (reader != null) PeoplesG.Home(new[] { reader });
        }

        /// <summary>The calm hour remembered by the one the reader sat with; told.</summary>
        public static void Calm(Pawn reader, Pawn on)
        {
            if (on == null || on.Dead) return;
            var t = SatThought;
            if (t != null)
                try
                {
                    // together: the reader has just sat an hour beside them (PeopleWorkOn kept them there).
                    using var _cause = Causes.Push("CC_PEv6_SatCause".Translate((reader?.LabelShort ?? "?").Named("READER")).Resolve());
                    CouncilGameComponent.GiveThought(on, t, reader);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the calm hour", e);
                }
            Tell.Message(NewsKind.People, Importance.Notable, on.MapHeld, "CC_PEv6_Sat".Translate(on.Named("PAWN"), (reader?.LabelShort ?? "?").Named("READER")).Resolve(),
                on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A reader of {FACTION} ({X}) at the gate as the drone the watch foresaw comes ({TALE}): they would sit an hour with {A},
    /// whom it touches worst, or with {A} and then {B}.
    /// </summary>
    public class HappeningWorker_TideReader : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && TideRead.SitJob != null && TideRead.Worst(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            var worst = TideRead.Worst(ctx.map);
            var drone = TideRead.Drone(ctx.map);
            if (ctx.faction == null || worst.Count == 0 || drone == null) return false;
            ctx.a = worst[0];
            ctx.b = worst.Count > 1 ? worst[1] : null;
            ctx.tale = drone.LabelCap;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Reader"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Social, new IntRange(8, 14));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.b != null ? "two" : "one");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv6_TouchedGone".Translate().Resolve();
            if (option == 1 && (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map)) return "CC_PEv6_NoSecond".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sit" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The reader sits an hour with A where they are (PeopleWorkOn, Rimfolk_PEv6_SitWith); with <see cref="both"/>, then with B.</summary>
    public class DilemmaEffect_TideReader : DilemmaEffect
    {
        public bool both;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            string who = both && hc.b != null ? "CC_PEv6_Impact_SitBoth".Translate(hc.a.LabelShort, hc.b.LabelShort).Resolve() : hc.a.LabelShort;
            sheet.Play("CC_PEv6_Impact_Sit".Translate(hc.stranger?.LabelShort ?? "?", who).Resolve(), "CC_PEv6_Impact_SitBrief".Translate().Resolve());
            var t = TideRead.SatThought;
            if (t != null)
            {
                var list = new List<Pawn> { hc.a };
                if (both && hc.b != null) list.Add(hc.b);
                sheet.Thought(list, who, t, p => hc.stranger);
            }
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.SitGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.stranger == null) return;
            string next = both && hc.b != null ? hc.b.GetUniqueLoadID() : null;
            if (TideRead.SitJob != null && PeopleWorkOn.Begin(hc, hc.stranger, hc.a, TideRead.SitJob, LocalTargetInfo.Invalid, next, 0, PeopleEvents6Math.SitTicks, 6f)) return;
            // The play-outs off: the hours had at once, each of them.
            if (both && hc.b != null) TideRead.Calm(hc.stranger, hc.b);
            TideRead.Sat(null, hc.stranger, hc.a);
        }
    }

    /// <summary>The reader's hour with one of the colony (TideRead.Sat).</summary>
    public class TideSatDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => TideRead.Sat(v, worker, on);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>
    /// A drone or a soothe the storyteller sends to a home whose sky the Conclave watches (not a quest's, not one coming back
    /// out of the game's queue) goes into the game's incident queue a day or so on, and the warning names whom it will touch
    /// (TideRead.Hold); the storyteller doesn't fire it now. A firing another prefix has already held is left to it.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_TideHold
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (queued || !RimfolkPatches.On || fi?.def == null || !PeopleEvents6Math.Tide(fi.def.defName)) return true;
            try
            {
                if (!TideRead.Hold(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the glass holds a tide", e);
                return true;
            }
        }
    }

    /// <summary>A held drone comes: it touches the sex the warning named (TideRead.Touches).</summary>
    [HarmonyPatch(typeof(IncidentWorker_PsychicDrone), "DoConditionAndLetter")]
    public static class Patch_TideTouchesDrone
    {
        public static void Prefix(IncidentWorker_PsychicDrone __instance, Map map, ref Gender gender)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                TideRead.Touches(__instance?.def, map, ref gender);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the drone's sex", e);
            }
        }
    }

    /// <summary>A held soothe comes: it touches the sex the warning named (TideRead.Touches).</summary>
    [HarmonyPatch(typeof(IncidentWorker_PsychicSoothe), "DoConditionAndLetter")]
    public static class Patch_TideTouchesSoothe
    {
        public static void Prefix(IncidentWorker_PsychicSoothe __instance, Map map, ref Gender gender)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                TideRead.Touches(__instance?.def, map, ref gender);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the soothe's sex", e);
            }
        }
    }
}
