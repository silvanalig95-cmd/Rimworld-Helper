using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 48. Breaking the drought (the Stormglass Conclave) =====================

    /// <summary>
    /// Breaking the drought (item 48): in a drought (Odyssey's, Vanilla Events Expanded's) or the game's heat wave, two
    /// storm-walkers of the Conclave come to the gate and offer to bring the rain. From dusk they set two of their rods on the
    /// colony's high ground (the relay mast's own choice of it) and keep the night beside them, the colony's best at the fields
    /// with them if the colony likes (a lesson, and the Conclave's way of reading the glass); at first light the dry spell
    /// ends and the land's own rain begins, one time in four as the land's own thunderstorm, its lightning the game's (their
    /// rods, and the colony's if it has one, take what falls near them). Then they take their rods down and go. The watch's
    /// fields by the weather reckon the rain as it comes.
    /// </summary>
    public static class DroughtBroken
    {
        public const string Kind = "pev:rain";
        public const string EventDef = "Rimfolk_Evt_BreakDrought";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The visit's stages: waiting for dusk, the rods set and the night kept, the rain come.</summary>
        private const int Waiting = 0, Night = 1, Rained = 2;

        private static readonly AccessTools.FieldRef<WeatherDecider, int> Duration = AccessTools.FieldRefAccess<WeatherDecider, int>("curWeatherDuration");

        /// <summary>The dry spell on this map now that they would break: a drought or the heat wave, not one of the land's for good.</summary>
        public static GameCondition DrySpell(Map map) =>
            map?.gameConditionManager?.ActiveConditions?.FirstOrDefault(c => c?.def != null && PeopleEvents6Math.Dry(c.def.defName) && !c.Permanent);

        /// <summary>Something else keeping the rain off (a condition that isn't a dry spell they can end).</summary>
        private static bool OtherDry(Map map) =>
            map.gameConditionManager.ActiveConditions.Any(c => c?.def != null && c.def.preventRain && !(PeopleEvents6Math.Dry(c.def.defName) && !c.Permanent));

        /// <summary>The land's own weathers of a kind that can come now.</summary>
        private static List<WeatherDef> Land(Map map, WeatherKind kind)
        {
            float temp = map.mapTemperature.OutdoorTemp;
            return map.Biome?.baseWeatherCommonalities?.Where(r => r?.weather != null && r.commonality > 0f && WeatherDynamics.KindOf(r.weather) == kind
                                                                  && r.weather.temperatureRange.Includes(temp)).Select(r => r.weather).ToList() ?? new List<WeatherDef>();
        }

        /// <summary>The land can rain now: a rain of its own, nothing else holding it off.</summary>
        public static bool CanRain(Map map) => map != null && !OtherDry(map) && Land(map, WeatherKind.Rain).Count > 0;

        /// <summary>
        /// The homes' hour: a dry spell that has run half a day, the Conclave at peace and not lately here, the land able to
        /// rain and a high ground to set the rods on: once a spell, at a chance (the storytellers' lean on visitors).
        /// </summary>
        public static void Watch(Map map, int now)
        {
            var dry = DrySpell(map);
            if (dry == null || dry.TicksPassed < PeopleEvents6Math.DryHours * GenDate.TicksPerHour) return;
            var b = PeopleEvents6Book.Instance;
            if (b == null || b.RainRolled(map, dry.uniqueID)) return;
            var f = PeoplesD.Friendly(PeoplesMath.Seers);
            if (f == null || b.lastRain >= 0 && now - b.lastRain < PeopleEvents6Math.RainGapDays * GenDate.TicksPerDay) return;
            if (!CanRain(map) || !PeopleEvents6.HighGround(map, out _).IsValid || MapFacts.ThreatOn(map) || !Tell.CanAsk) return;
            b.NoteRain(map, dry.uniqueID);
            if (!Rand.Chance(PeopleEvents6Math.RainChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastRain = now;
        }

        /// <summary>Dev mode: the storm-walkers come now, if a dry spell is on (or a heat wave started for them).</summary>
        public static string Now_(Map map)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Seers);
            if (map == null || f == null) return "no Conclave at peace.";
            if (DrySpell(map) == null)
            {
                var heat = GameConditionMaker.MakeCondition(GameConditionDefOf.HeatWave, 3 * GenDate.TicksPerDay);
                map.gameConditionManager.RegisterCondition(heat);
            }
            var b = PeopleEvents6Book.Instance;
            if (b != null) b.lastRain = -1;
            return PeopleEvents2.Fire(EventDef, map, f) ? "the storm-walkers are at the gate." : "they can't come: no rain the land can have now, or no high ground.";
        }

        private static Visit VisitOf(Pawn p) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && (v.x == p || v.group.Contains(p)));

        private static List<Pawn> Walkers(Visit v) => v.group.Prepend(v.x).Where(p => PeoplesG.Up(p, v.map)).ToList();

        /// <summary>Their rods standing by the place.</summary>
        private static List<Thing> Rods(Visit v)
        {
            var def = PeoplesE.RodDef;
            var f = PeopleEvents5.PeopleOf(v);
            if (v.map == null || def == null || !v.cell.IsValid) return new List<Thing>();
            return v.map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.Faction == f && t.Position.InHorDistOf(v.cell, 8f)).ToList();
        }

        /// <summary>The bargain made: the walkers go up to the high ground and wait for dusk, the one who keeps the night with them (A) named.</summary>
        public static void Begin(HappeningContext ctx, bool keep)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var walkers = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            if (map == null || t == null || walkers.Count == 0 || !ctx.spot.IsValid) return;
            var v = t.AddVisit(Kind, ctx.incident, map, walkers[0], keep ? ctx.a : null, walkers.Skip(1).ToList(), Now + 2 * GenDate.TicksPerDay);
            v.cell = ctx.spot;
            v.text = ctx.note;
            v.stake = Waiting;
            v.due = -1;
            Lords.Leave(walkers);
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, ctx.spot, 2 * GenDate.TicksPerDay), map, walkers);
            Hour(v, Now);
        }

        /// <summary>A rod set at its cell (or the nearest free one by the place).</summary>
        public static void RodSet(Pawn by, IntVec3 cell)
        {
            var v = VisitOf(by);
            if (v == null || by.Map == null) return;
            if (PeopleEvents6.Rod(by.Map, cell, by.Faction) == null)
            {
                var other = PeopleEvents6.RodCells(by.Map, v.cell, 4);
                if (other.Count > 0) PeopleEvents6.Rod(by.Map, other[0], by.Faction);
            }
            if (Rand.Chance(0.5f)) PeoplesG.SayLine(by, v.incident, "set");
        }

        /// <summary>A rod taken down.</summary>
        public static void RodDown(Pawn by, Thing rod) => PeopleEvents6.Unrod(rod);

        /// <summary>
        /// The visit's hour: before dusk they wait by the place; from dusk each sets a rod, then they keep the night beside them
        /// (the one of the colony with them); at first light the rain; two hours on they take the rods down and go.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var walkers = Walkers(v);
            if (walkers.Count == 0 || now > v.until)
            {
                Finish(v, walkers);
                return false;
            }
            int hour = GenLocalDate.HourOfDay(v.map);
            // The rain comes at first light whatever is on the map; the rest of their work waits while there is danger.
            if (v.stake == Night && PeopleEvents6Math.Daylight(hour))
            {
                Rain(v, walkers);
                return true;
            }
            if (v.map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            switch (v.stake)
            {
                case Waiting:
                {
                    // The day before dusk they wait by the place (the errand's lord); from dusk the rods go up, three hours at most.
                    bool setting = v.due >= 0;
                    if (!PeopleEvents6Math.SetUpHour(hour) && !(setting && PeopleEvents6Math.RainNow(hour))) return true;
                    if (!setting) v.due = now;
                    var rods = Rods(v);
                    if (rods.Count >= PeopleEvents6Math.RainWalkers || now - v.due >= 3 * GenDate.TicksPerHour || PeopleEvents6Math.RainNow(hour))
                    {
                        // Their rods up (or the place too crowded for them: the night kept on the open ground all the same).
                        v.stake = Night;
                        if (v.a != null && PeoplesG.Up(v.a, v.map))
                            PeopleWorkOn.Bring(v.a, new LocalTargetInfo(v.cell), "beside", GenDate.TicksPerHour * Mathf.Max(2, PeoplesMath.HoursToLight(hour, PeopleEvents6Math.RainHour)));
                        PeoplesG.SayLine(walkers[0], v.incident, "night");
                        return true;
                    }
                    var cells = PeopleEvents6.RodCells(v.map, v.cell, PeopleEvents6Math.RainWalkers);
                    for (int i = 0; i < walkers.Count && i < cells.Count; i++)
                        if (walkers[i].CurJobDef != PeopleEvents6.RodJob && rods.Count + i < PeopleEvents6Math.RainWalkers)
                            PeopleEvents6.Send(walkers[i], PeopleEvents6.RodJob, cells[i], PeopleEvents6Math.RodTicks);
                    return true;
                }
                case Night:
                {
                    var rods = Rods(v);
                    for (int i = 0; i < walkers.Count; i++)
                    {
                        var p = walkers[i];
                        if (p.CurJobDef == PeopleEvents6.KeepJob) continue;
                        LocalTargetInfo at = rods.Count > 0 ? new LocalTargetInfo(rods[i % rods.Count]) : new LocalTargetInfo(v.cell);
                        PeopleEvents6.Send(p, PeopleEvents6.KeepJob, at, PeopleEvents6Math.NightTicks);
                    }
                    return true;
                }
                default:
                {
                    if (now < v.due) return true;
                    var rods = Rods(v);
                    if (rods.Count == 0 || now > v.due + 3 * GenDate.TicksPerHour)
                    {
                        Finish(v, walkers);
                        return false;
                    }
                    for (int i = 0; i < walkers.Count && i < rods.Count; i++)
                        if (walkers[i].CurJobDef != PeopleEvents6.RodDownJob)
                            PeopleEvents6.Send(walkers[i], PeopleEvents6.RodDownJob, rods[i], PeopleEvents6Math.TakeDownTicks);
                    return true;
                }
            }
        }

        /// <summary>
        /// First light: the dry spell ended, the land's rain begun (one time in four its thunderstorm), the weather's length the
        /// rain's own; the one who kept the night with them taught; told.
        /// </summary>
        private static void Rain(Visit v, List<Pawn> walkers)
        {
            var map = v.map;
            string what = DrySpell(map)?.Label ?? DefDatabase<GameConditionDef>.GetNamedSilentFail(v.text ?? "")?.label ?? "?";
            foreach (var c in map.gameConditionManager.ActiveConditions.Where(c => c?.def != null && PeopleEvents6Math.Dry(c.def.defName) && !c.Permanent).ToList())
                try
                {
                    c.End();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the dry spell ended", e);
                }
            bool storm = PeopleEvents6Math.Storm(Rand.Value);
            var w = (storm ? Land(map, WeatherKind.Storm) : new List<WeatherDef>()).RandomElementWithFallback() ?? Land(map, WeatherKind.Rain).RandomElementWithFallback();
            storm = w != null && WeatherDynamics.KindOf(w) == WeatherKind.Storm;
            if (w != null)
                try
                {
                    map.weatherManager.TransitionTo(w);
                    Duration(map.weatherDecider) = w.durationRange.RandomInRange;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 6: the rain", e);
                }
            try
            {
                FieldWeather.Of(map)?.Refresh();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the fields reckon the rain", e);
            }
            v.stake = Rained;
            v.due = Now + 2 * GenDate.TicksPerHour;
            v.place = null;
            PeoplesG.SayLine(walkers[0], v.incident, storm ? "storm" : "came");
            var a = v.a;
            if (a != null && !a.Dead)
            {
                PeopleWorkOn.Release(a);
                a.skills?.Learn(SkillDefOf.Plants, PeopleEvents6Math.NightXp, true);
                var taught = HappeningWorker_GlassLesson.Taught;
                if (taught != null && !HappeningWorker_GlassLesson.IsTaught(a)) a.health.AddHediff(taught);
            }
            var f = PeopleEvents5.PeopleOf(v);
            string text = (storm ? "CC_PEv6_RainStorm" : "CC_PEv6_RainCame").Translate((f?.Name ?? "?").Named("FACTION"), what.Named("CONDITION")).Resolve();
            if (a != null && !a.Dead) text += " " + "CC_PEv6_RainKept".Translate(a.Named("PAWN")).Resolve();
            Tell.Letter(NewsKind.Land, Importance.Notable, map, "CC_PEv6_RainLabel".Translate().Resolve(), text, storm ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent,
                new LookTargets(walkers), f);
        }

        private static void Finish(Visit v, List<Pawn> walkers)
        {
            v.done = true;
            if (v.a != null) PeopleWorkOn.Release(v.a);
            foreach (var r in Rods(v)) PeopleEvents6.Unrod(r);
            var f = PeopleEvents5.PeopleOf(v);
            if (v.stake == Rained)
            {
                if (PeopleEvents6.Thanks(f)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.RainGoodwill, GoodwillWhy.Helped);
                if (walkers.Count > 0) PeoplesG.SayLine(walkers[0], v.incident, "done");
            }
            PeoplesG.Home(walkers);
        }
    }

    /// <summary>
    /// Two storm-walkers of {FACTION} ({X}) at the gate in a dry spell ({TALE}): for {COUNT} silver they would set their rods
    /// on {PLACE} from dusk, keep the night there, and bring the rain at first light; {A} could keep the night with them.
    /// </summary>
    public class HappeningWorker_BreakDrought : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && DroughtBroken.DrySpell(map) != null && DroughtBroken.CanRain(map) && PeopleEvents6.HighGround(map, out _).IsValid
            && PeoplesE.RodDef != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            var dry = DroughtBroken.DrySpell(ctx.map);
            ctx.spot = PeopleEvents6.HighGround(ctx.map, out bool rocky);
            if (ctx.faction == null || dry == null || !ctx.spot.IsValid) return false;
            ctx.note = dry.def.defName;
            ctx.tale = dry.def.label;
            ctx.count = PeopleEvents6Math.RainFee(LivingEvents.WealthFactor(ctx.map));
            ctx.placeName = PeopleEvents6.HighGroundWords(rocky);
            ctx.variant = rocky ? 1 : 0;
            for (int i = 0; i < PeopleEvents6Math.RainWalkers; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Walker"), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Plants, p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(SkillDefOf.Plants)?.TotallyDisabled == false);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "HeatWave" ? "heat" : "drought");
            tags.Add(ctx.variant == 1 ? "rocky" : "open");
            if (ctx.map != null && GlassRod.Near(ctx.map, ctx.spot) != null) tags.Add("rod");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (DroughtBroken.DrySpell(ctx.map) == null) return "CC_PEv6_DryOver".Translate().Resolve();
            if (option == 1 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map)) return "CC_PEv6_KeeperGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "rain" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The walkers up to the high ground for the night (DroughtBroken.Begin); with <see cref="keep"/>, A keeps the night with them.</summary>
    public class DilemmaEffect_BreakDrought : DilemmaEffect
    {
        public bool keep;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            sheet.Play("CC_PEv6_Impact_Rain".Translate(hc.placeName ?? "?", hc.tale ?? "?").Resolve(), "CC_PEv6_Impact_RainBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 0, "CC_PEv6_Impact_RainStorm".Translate().Resolve(), "CC_PEv6_Impact_RainStormBrief".Translate().Resolve(), "pev6storm");
            if (keep && hc.a != null)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv6_Impact_RainKeep".Translate(hc.a.LabelShort).Resolve(), "CC_PEv6_Impact_RainKeepBrief".Translate().Resolve(), "pev6keep");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.RainGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => DroughtBroken.Begin(ctx as HappeningContext, keep);
    }
}
