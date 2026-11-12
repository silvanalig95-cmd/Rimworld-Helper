using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Kindness and anger (docs/design/gods.md, stage 2, brainstorm item 19), at the game's own rate and always shown, each
    /// god in its own way (GodWays.Blessing, Wrath, Grudge, Kin; the domain tables only as the fallback). Nothing says whose
    /// hand it was or why: the game's own incident comes with the game's own letter, the rest with a plain card of what was
    /// seen, and whoever saw it may say what they think (a believer's guess, a scoffer's doubt).
    /// <list type="bullet">
    /// <item>Kindness, now and then while the colony stands well with a god: Morel breaks a fever or eases a plague in the
    /// small hours (once an illness, never a cure); Hollin's wild food and healroot spring up near the walls; Smoulder's
    /// dreams push a vein of silver or steel to the surface; Pip mends a broken machine or fills a battery overnight; Cirrus
    /// lets a meteorite fall (the game's own); Hush ends a cold snap early; Helios holds the rain off for long clear days in
    /// the growing season; Murmur's song brings rain; a cat wanders in and stays (Mog); a trader's caravan arrives at the
    /// right time (Ducat, the game's own).</item>
    /// <item>A grudge against one colonist (a sacred beast killed, the god mocked in a letter): Cirrus's lightning on the
    /// roof over them in the next thunderstorm (the game's own strike, clear of anyone), or beasts sent after them when they
    /// are out (two or three of the game's wolves, manhunting).</item>
    /// <item>A god the colony stands badly with at a home acts in its own shape: Hollin's blight, Morel's freezer failing at
    /// the worst hour, Pip's short circuit, Cirrus's flashstorm, Hush's cold snap, Smoulder's and Helios's heat wave (each the
    /// game's own), Murmur's drought and then its flood (Odyssey's own, else the rain held off), silver gone missing from a
    /// hoard (Ducat); a patient god rarely, a wrathful one soonest.</item>
    /// <item>Helios gives blindly: long clear days in the growing season to everyone alike, and his too-much-giving (a heat
    /// wave, a drought, a solar flare), each at its own rare rate whatever the colony does. Selene returns at night what
    /// the colony did by day (its deeds to the peoples, a prisoner executed), as much as her face shows: a kindness as a
    /// wound healed by morning or something found at the door, a cruelty as bad dreams that follow a few of the colony into
    /// the day.</item>
    /// <item>Bellwether's kin killed by the colony: word goes out on the roads, every people near thinks the less of it, and
    /// every god turns colder. A cat killed (Mog's kin): the colony's cats leave in one night, and the rats come back.</item>
    /// </list>
    /// Every hurt goes through the shared harm budget (HarmKinds.GodBeasts, GodLightning); the game's own blight, conditions,
    /// breakdowns and lightning come as the game makes them.
    /// </summary>
    public static class GodHealth
    {
        // ================= Grudges, and Bellwether's kin =================

        /// <summary>A god holds a grudge against one colonist: its lightning or its beasts (GodWays.Grudge) may come for them while it lasts.</summary>
        public static void Grudge(God g, Pawn p, Map home, string why)
        {
            var b = GodWorld.Book;
            if (b == null || g == null || p == null || p.Dead) return;
            var wrath = GodWays.Grudge(g);
            if (wrath != GodWrath.Lightning && wrath != GodWrath.Beasts) return;
            if (b.Of("grudge").Any(d => d.pawn == p && d.god == g.id)) return;
            b.Add(new GodDoing
            {
                kind = "grudge", god = g.id, pawn = p, home = Homes.KeyOf(GodWorld.HomeOr(home ?? p.MapHeld)), what = wrath.ToString(), cell = IntVec3.Invalid,
                until = GodWorld.Now + GodWorldMath.GrudgeDays * GodWorldMath.DayTicks, count = why == "beast" ? 1 : 0
            });
        }

        /// <summary>
        /// An animal died (Happenings.Notify_Killed), killed by one of the colony: a god whose kin it was (GodWays.Kin:
        /// Bellwether's thrumbos) answers in its own way (word on the roads, every god colder), else holds it against the
        /// killer; else a beast sacred to a domain whose god takes it ill (GodsMath.SacredDeath, the core's): the loudest such
        /// god the colony knows holds it against the killer.
        /// </summary>
        public static void Killed(Pawn victim, DamageInfo? dinfo, Map map)
        {
            // A prisoner of the colony's executed: a cruelty for the moon to return.
            if (GodWorld.On && victim?.RaceProps?.Humanlike == true && dinfo?.Def == DamageDefOf.ExecutionCut && (victim.IsPrisonerOfColony || victim.HostFaction == Faction.OfPlayer))
                Deed(false, true);
            if (!GodWorld.On || victim?.RaceProps?.Animal != true || map == null || !Homes.IsHome(map)) return;
            try
            {
                if (!(dinfo?.Instigator is Pawn killer) || killer.Faction != Faction.OfPlayer || !killer.RaceProps.Humanlike) return;
                var kinOf = Gods.All.FirstOrDefault(x => GodWays.Kin(x).Contains(victim.def.defName));
                if (kinOf != null)
                {
                    var kinWrath = GodWays.KinWrath(kinOf);
                    if (kinWrath == GodWrath.Shunned) Shunned(kinOf, map, victim, killer);
                    else if (kinWrath == GodWrath.Rats) Rats(kinOf, map, victim);
                    else Grudge(kinOf, killer, map, "beast");
                    GodWorld.Remark(map, victim.PositionHeld, "sacrilege", kinOf, killer, killer.Named("KILLER"));
                    return;
                }
                bool slaughtered = victim.Faction == Faction.OfPlayer || dinfo?.Def == DamageDefOf.ExecutionCut;
                bool wild = victim.Faction == null;
                foreach (var def in DefDatabase<GodDomainDef>.AllDefsListForReading)
                {
                    if (def?.Animals == null || !def.Animals.Contains(victim.def) || GodsMath.SacredDeath(def.domain, slaughtered, wild) >= 0f) continue;
                    var g = Gods.All.Where(x => (x.domain == def.domain || x.second == def.domain) && (Gods.Known(x) || Gods.ColonyGods(map).Contains(x)))
                        .OrderByDescending(Gods.Loudness).FirstOrDefault();
                    if (g == null || Gods.Loudness(g) < 0.5f) continue;
                    Grudge(g, killer, map, "beast");
                    GodWorld.Remark(map, victim.PositionHeld, "sacrilege", g, killer, killer.Named("KILLER"));
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a sacred beast killed", e);
            }
        }

        /// <summary>Bellwether's kin killed: word on the roads, every people near thinks the less of the colony, every god it knows turns colder.</summary>
        private static void Shunned(God g, Map map, Pawn victim, Pawn killer)
        {
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(Arrivals.Usable).ToList()) StoryChains.Goodwill(f, GodWorldMath.ShunnedGoodwill);
            string why = GodWorld.T("CC_GW_Shunned_Why", g, killer.Named("KILLER"), victim.def.label.Named("BEAST"));
            foreach (var x in GodWorld.Known.Where(x => x != g).ToList()) Gods.AddFavour(x, map, GodWorldMath.ShunnedFavour, why, FavourSource.Sacrilege);
            Gods.AddFavour(g, map, GodWorldMath.ShunnedFavour * 2f, why, FavourSource.Sacrilege);
            GodWorld.Moment(g, map, PresenceKind.Wrath, victim.PositionHeld, null, "CC_GW_Shunned_Label".Translate(victim.def.label.Named("BEAST")).Resolve(),
                "CC_GW_Shunned_Text".Translate(killer.Named("KILLER"), victim.def.label.Named("BEAST")).Resolve(), true, LetterDefOf.NegativeEvent,
                new LookTargets(new TargetInfo(victim.PositionHeld, map)), null, Importance.Important);
            GodWorld.Chronicle("CC_GW_Shunned_Chronicle".Translate(killer.Named("KILLER"), victim.def.label.Named("BEAST")).Resolve(), killer);
        }

        /// <summary>A cat killed (Mog's kin): the colony's cats walk off in one night, and the rats come back (the game's own, wild).</summary>
        private static void Rats(God g, Map map, Pawn victim)
        {
            var cats = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && GodWays.Kin(g).Contains(a.def.defName)).ToList();
            foreach (var c in cats)
            {
                try
                {
                    c.SetFaction(null);
                    if (c.mindState != null) c.mindState.exitMapAfterTick = GodWorld.Now + Rand.Range(GenDate.TicksPerHour, 8 * GenDate.TicksPerHour);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: the cats leave", e);
                }
            }
            var rat = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rat");
            var sent = new List<Pawn>();
            var edge = LegendBeasts.EdgeToward(map, victim.PositionHeld);
            if (rat != null && edge.IsValid)
                for (int i = 0; i < GodWorldMath.RatCount(Rand.Value); i++)
                {
                    try
                    {
                        var r = PawnGenerator.GeneratePawn(rat, null);
                        sent.Add((Pawn)GenSpawn.Spawn(r, CellFinder.RandomClosewalkCellNear(edge, map, 6), map));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods in the world: the rats come back", e);
                        break;
                    }
                }
            if (cats.Count == 0 && sent.Count == 0) return;
            GodWorld.Moment(g, map, PresenceKind.Wrath, victim.PositionHeld, null, "CC_GW_Rats_Label".Translate().Resolve(),
                (cats.Count > 0 ? "CC_GW_Rats_Text" : "CC_GW_Rats_TextNoCats").Translate().Resolve(), true, LetterDefOf.NegativeEvent,
                sent.Count > 0 ? new LookTargets(sent) : new LookTargets(cats));
        }

        // ================= The day: beasts, anger, kindness =================

        public static void Daily(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("grudge").ToList())
            {
                if (d.pawn == null || d.pawn.Dead || now > d.until)
                {
                    d.done = true;
                    continue;
                }
                if (d.what == nameof(GodWrath.Beasts) && now - d.at >= GodWorldMath.BeastsAfterDays * GodWorldMath.DayTicks) GodWorld.Safe("beasts sent", () => Beasts(d));
            }
            foreach (var map in Homes.All.ToList())
            {
                GodWorld.Safe("a god's anger at a home", () => Anger(b, map));
                GodWorld.Safe("a god's kindness at a home", () => Kindness(b, map));
            }
        }

        /// <summary>Out under the sky on a home map, awake and on their feet, with nothing else going on there.</summary>
        private static bool OutAndAbout(Pawn p) =>
            p != null && p.Spawned && Homes.IsHome(p.Map) && !p.Downed && p.Awake() && !p.Position.Roofed(p.Map) && !p.InMentalState && !p.Drafted
            && p.Map.dangerWatcher.DangerRating == StoryDanger.None;

        private static void Beasts(GodDoing d)
        {
            var p = d.pawn;
            var g = Gods.ById(d.god);
            if (g == null || !OutAndAbout(p)) return;
            if (GodWorld.Force ? !Harm.May(p) : !Harm.Roll(p, HarmKinds.GodBeasts, GodWorldMath.BeastsDayChance * GodWorld.Factor(), HurtClass.Injury)) return;
            var map = p.Map;
            var kind = GodWays.WrathBeasts(g).Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).FirstOrDefault(k => k?.RaceProps?.Animal == true);
            if (kind == null) return;
            var edge = LegendBeasts.EdgeToward(map, p.Position);
            if (!edge.IsValid || !CellFinder.TryFindRandomCellNear(edge, map, 6, c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, p, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)), out var at))
                return;
            int n = GodWorldMath.BeastCount(g.nature);
            var sent = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var beast = PawnGenerator.GeneratePawn(kind, null);
                    GenSpawn.Spawn(beast, CellFinder.RandomClosewalkCellNear(at, map, 3), map);
                    beast.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter, null, true);
                    // It knows whom it came for.
                    if (beast.InMentalState) beast.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, p), JobCondition.InterruptForced, null, false, true);
                    sent.Add(beast);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: a beast sent", e);
                }
            }
            if (sent.Count == 0) return;
            d.done = true;
            Harm.Note(p, HarmKinds.GodBeasts);
            GodWorld.Moment(g, map, PresenceKind.Wrath, at, null, "CC_GW_Beasts_Label".Translate(p.Named("PAWN")).Resolve(),
                "CC_GW_Beasts_Text".Translate(p.Named("PAWN"), Find.ActiveLanguageWorker.Pluralize(sent[0].def.label, sent.Count).Named("BEASTS")).Resolve(), true,
                LetterDefOf.ThreatSmall, new LookTargets(sent), null, Importance.Important);
            GodWorld.Say(p, "beasts", g);
        }

        /// <summary>A god the colony stands displeased or worse with at a home acts now and then, in its own shape and its own temper.</summary>
        internal static void Anger(GodWorldBook b, Map map)
        {
            if (map == null || !b.Gap("anger:" + Homes.KeyOf(map), 20f)) return;
            foreach (var g in GodWorld.Known.Where(x => !GodWays.Mirror(x)).InRandomOrder().ToList())
            {
                // Helios is blind to the colony: his too-much-giving comes at its own rate, whatever its standing.
                float days = GodWays.Blind(g) ? GodWays.WrathDays(g) : GodWorldMath.WrathDays(Gods.Favour(g, map), g.nature, GodWays.WrathDays(g));
                if (days <= 0f || !GodWorld.Roll(GodWorldMath.DayChance(days, GodWorld.Factor(g, map)))) continue;
                if (!GodWays.Wraths(g).InRandomOrder().Any(shape => Wrath(g, map, shape))) continue;
                b.Came("anger:" + Homes.KeyOf(map));
                return;
            }
        }

        /// <summary>One act of a god's anger at a home in the shape given; false when it couldn't be (no crops, no machine, no storm).</summary>
        internal static bool Wrath(God g, Map map, GodWrath wrath)
        {
            switch (wrath)
            {
                case GodWrath.Blight: return Incident(g, map, "CropBlight", "blight");
                case GodWrath.Eclipse: return Incident(g, map, "Eclipse", "eclipse");
                case GodWrath.ColdSnap: return Incident(g, map, "ColdSnap", "coldsnap");
                case GodWrath.HeatWave: return Incident(g, map, "HeatWave", "heatwave");
                case GodWrath.Flashstorm: return Incident(g, map, "Flashstorm", "flashstorm");
                case GodWrath.ShortCircuit: return Incident(g, map, "ShortCircuit", "shortcircuit");
                case GodWrath.Breakdown: return Breakdown(g, map);
                case GodWrath.Lightning: return Field(g, map);
                case GodWrath.Drought: return Drought(g, map, GodWays.Wrath(g) == GodWrath.Drought);
                case GodWrath.Missing: return Missing(g, map);
                case GodWrath.Flare: return Incident(g, map, "SolarFlare", "flare");
                case GodWrath.Nightmares: return Nightmares(g, map);
                default: return false;
            }
        }

        /// <summary>
        /// A drought (Odyssey's own; without Odyssey, the rain held off a while): Murmur's anger, and a flood instead when its
        /// drought came lately (<paramref name="flood"/>); Helios's too-much-giving, without the flood.
        /// </summary>
        private static bool Drought(God g, Map map, bool flood)
        {
            var b = GodWorld.Book;
            string key = "drought:" + Homes.KeyOf(map);
            bool lately = b != null && b.Last(key) >= 0 && !GodWorldMath.Gap(GodWorld.Now, b.Last(key), GodWorldMath.FloodAfterDroughtDays);
            if (!flood) lately = false;
            if (lately && Incident(g, map, "SeasonalFlooding", "flood")) return true;
            if (!lately && Incident(g, map, "Drought", "drought"))
            {
                b?.Came(key);
                return true;
            }
            if (lately) return false;
            try
            {
                map.weatherDecider.DisableRainFor(Mathf.RoundToInt(GodWorldMath.ClearDays * 2f * GenDate.TicksPerDay));
                if (map.weatherManager.curWeather != WeatherDefOf.Clear) map.weatherManager.TransitionTo(WeatherDefOf.Clear);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the rain held off", e);
                return false;
            }
            b?.Came(key);
            return Told(g, map, IntVec3.Invalid, "dry", PresenceKind.Wrath);
        }

        /// <summary>A cruelty returned (Selene): a few of the colony wake from the same bad dream, and it follows them a day or two (Rimfolk_BadDreams).</summary>
        private static bool Nightmares(God g, Map map)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_BadDreams");
            if (def == null) return false;
            var who = GodWorld.Grown(map).Where(p => !p.health.hediffSet.HasHediff(def)).InRandomOrder().Take(GodWorldMath.NightmareCount).ToList();
            if (who.Count == 0) return false;
            foreach (var p in who) p.health.AddHediff(def);
            var first = who[0];
            GodWorld.Moment(g, map, PresenceKind.Wrath, first.Position, null, "CC_GW_Nightmares_Label".Translate().Resolve(),
                "CC_GW_Nightmares_Text".Translate(first.Named("PAWN"), (who.Count - 1).Named("COUNT")).Resolve(), false, null, new LookTargets(who));
            GodWorld.Remark(map, first.Position, "nightmares", g);
            return true;
        }

        /// <summary>Ducat's anger at a hoard: some of the colony's silver gone from where it lay, and nobody took it.</summary>
        private static bool Missing(God g, Map map)
        {
            var stacks = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).ToList();
            int held = stacks.Sum(t => t.stackCount);
            int take = GodWorldMath.MissingSilver(held);
            if (take <= 0) return false;
            var from = stacks.OrderByDescending(t => t.stackCount).First();
            var at = from.Position;
            int left = take;
            foreach (var t in stacks.OrderByDescending(t => t.stackCount).ToList())
            {
                int n = Mathf.Min(left, t.stackCount);
                t.SplitOff(n).Destroy();
                left -= n;
                if (left <= 0) break;
            }
            return Told(g, map, at, "missing", PresenceKind.Wrath, (take - left).Named("COUNT"));
        }

        /// <summary>The game's own incident with the game's own letter, unchanged; remembered as the god's, and someone may say so.</summary>
        private static bool Incident(God g, Map map, string defName, string tag)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def?.Worker == null) return false;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            if (!def.Worker.CanFireNow(parms) || !def.Worker.TryExecute(parms)) return false;
            GodWorld.Remember(g, map, tag == "meteor" ? GodActKind.Blessing : GodActKind.Wrath, ("CC_GW_Seen_" + tag).Translate(Homes.Name(map).Named("HOME")).Resolve());
            GodWorld.Remark(map, IntVec3.Invalid, tag, g);
            return true;
        }

        /// <summary>A machine of the colony's broken down (the game's own breakdown): a cooler first (a freezer failing at the worst hour), else any.</summary>
        private static bool Breakdown(God g, Map map)
        {
            var machines = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.GetComp<CompBreakdownable>() is CompBreakdownable c && !c.BrokenDown).ToList();
            var target = machines.FirstOrDefault(x => x is Building_Cooler) ?? machines.RandomElementWithFallback();
            if (target == null) return false;
            try
            {
                target.GetComp<CompBreakdownable>().DoBreakdown();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a machine broken", e);
                return false;
            }
            GodWorld.Moment(g, map, PresenceKind.Wrath, target.Position, null, "CC_GW_Breakdown_Label".Translate().Resolve(),
                "CC_GW_Breakdown_Text".Translate(target.LabelNoCount.Named("THING")).Resolve(), false, null, new LookTargets(target));
            GodWorld.Remark(map, target.Position, "breakdown", g);
            return true;
        }

        /// <summary>A strike on an empty field near the colony in a thunderstorm (only in a storm).</summary>
        private static bool Field(God g, Map map)
        {
            if (!StormSense.Thunder(map)) return false;
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) return false;
            for (int i = 0; i < 30; i++)
            {
                var c = CellFinder.RandomClosewalkCellNear(centre, map, 30);
                if (!Clear(map, c) || c.Roofed(map) || c.GetEdifice(map) != null) continue;
                Strike(map, c);
                GodWorld.Remember(g, map, GodActKind.Wrath, "CC_GW_Seen_field".Translate(Homes.Name(map).Named("HOME")).Resolve(), null, c);
                GodWorld.Remark(map, c, "lightning", g);
                return true;
            }
            return false;
        }

        // ================= A god's kindness =================

        /// <summary>A god pleased or better at a home shows it now and then in its own way (GodWays.Blessing; a fever eased comes at night, in the hour).</summary>
        internal static void Kindness(GodWorldBook b, Map map)
        {
            if (map == null) return;
            foreach (var g in GodWorld.Known.Where(x => !GodWays.Mirror(x)).InRandomOrder().ToList())
            {
                var blessing = GodWays.Blessing(g);
                if (blessing == GodBlessing.None || blessing == GodBlessing.Fever || GodVoid.Quiet(b, g)) continue;
                string key = "kind:" + g.id + ":" + Homes.KeyOf(map);
                // Helios gives to everyone alike, whatever the colony's standing.
                float days = GodWays.Blind(g) ? GodWorldMath.BlindBlessingDays : GodWorldMath.BlessingDays(Gods.Favour(g, map));
                if (days <= 0f || !b.Gap(key, GodWorldMath.BlessingGap) || !GodWorld.Roll(GodWorldMath.DayChance(days, GodWorld.Lean(g, map)))) continue;
                if (!Bless(g, map, blessing)) continue;
                b.Came(key);
                return;
            }
        }

        /// <summary>One act of a god's kindness at a home; false when there was nothing for it to do.</summary>
        internal static bool Bless(God g, Map map, GodBlessing blessing)
        {
            var centre = CampSite.ColonyCenter(map);
            switch (blessing)
            {
                case GodBlessing.Growth:
                    var grew = centre.IsValid ? Sprout(map, centre, 5) : IntVec3.Invalid;
                    return grew.IsValid && Told(g, map, grew, "growth");
                case GodBlessing.Ore:
                    var at = Vein(map, centre, out var ore);
                    return at.IsValid && Told(g, map, at, "ore", ore.Named("ORE"));
                case GodBlessing.Mend:
                    var mended = Mend(map, out bool repaired);
                    return mended != null && Told(g, map, mended.Position, repaired ? "mend" : "battery", mended.LabelNoCount.Named("THING"));
                case GodBlessing.Meteor:
                    return Incident(g, map, "MeteoriteImpact", "meteor");
                case GodBlessing.Thaw:
                    var cold = map.gameConditionManager.GetActiveCondition(GameConditionDefOf.ColdSnap);
                    if (cold == null || cold.Permanent) return false;
                    cold.TicksLeft = Mathf.Min(cold.TicksLeft, GenDate.TicksPerHour);
                    return Told(g, map, IntVec3.Invalid, "thaw");
                case GodBlessing.Clear:
                    if (!GodWorldMath.GrowingWeather(map.mapTemperature.OutdoorTemp)) return false;
                    map.weatherDecider.DisableRainFor(Mathf.RoundToInt(GodWorldMath.ClearDays * GenDate.TicksPerDay));
                    if (map.weatherManager.curWeather != WeatherDefOf.Clear) map.weatherManager.TransitionTo(WeatherDefOf.Clear);
                    return Told(g, map, IntVec3.Invalid, "clear");
                case GodBlessing.Rain:
                    var rain = Rainy(map);
                    if (rain == null || map.weatherManager.curWeather?.rainRate > 0f) return false;
                    map.weatherManager.TransitionTo(rain);
                    return Told(g, map, IntVec3.Invalid, "rain");
                case GodBlessing.Stray:
                    var cat = Stray(g, map);
                    return cat != null && Told(g, map, cat.Position, "stray");
                case GodBlessing.Trader:
                    return Incident(g, map, "TraderCaravanArrival", "trader");
                default:
                    return false;
            }
        }

        /// <summary>Rain of the land's own, without thunder where it has one.</summary>
        internal static WeatherDef Rainy(Map map)
        {
            var allowed = map.Biome?.baseWeatherCommonalities?.Where(w => w?.weather != null && w.commonality > 0f && w.weather.rainRate > 0f && w.weather.snowRate <= 0f)
                .Select(w => w.weather).ToList() ?? new List<WeatherDef>();
            return allowed.FirstOrDefault(w => w.eventMakers == null || w.eventMakers.Count == 0) ?? allowed.FirstOrDefault();
        }

        /// <summary>A cat of the land's (one of the kin, the game's cat first) walks in from the edge and is the colony's (Mog's kindness).</summary>
        private static Pawn Stray(God g, Map map)
        {
            var kind = GodWays.Kin(g).Prepend("Cat").Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).FirstOrDefault(k => k?.RaceProps?.Animal == true);
            var centre = CampSite.ColonyCenter(map);
            if (kind == null || !centre.IsValid) return null;
            var edge = LegendBeasts.EdgeToward(map, centre);
            if (!edge.IsValid) return null;
            try
            {
                var cat = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
                return (Pawn)GenSpawn.Spawn(cat, CellFinder.RandomClosewalkCellNear(edge, map, 4), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a cat wanders in", e);
                return null;
            }
        }

        /// <summary>A plain card of what was seen (no why), and a word from whoever saw it.</summary>
        private static bool Told(God g, Map map, IntVec3 cell, string tag, params NamedArgument[] more) => Told(g, map, cell, tag, PresenceKind.Blessing, more);

        private static bool Told(God g, Map map, IntVec3 cell, string tag, PresenceKind kind, params NamedArgument[] more)
        {
            string key = "CC_GW_" + char.ToUpperInvariant(tag[0]) + tag.Substring(1);
            var args = new List<NamedArgument>(more) { Homes.Name(map).Named("HOME") }.ToArray();
            GodWorld.Moment(g, map, kind, cell, null, (key + "_Label").Translate(args).Resolve(), (key + "_Text").Translate(args).Resolve(), false, null,
                cell.IsValid ? new LookTargets(new TargetInfo(cell, map)) : null);
            GodWorld.Remark(map, cell, tag, g);
            return true;
        }

        /// <summary>Open, unroofed ground of at least this fertility, clear of the colony's buildings, zones, things and plants, within a ring round the colony.</summary>
        private static IEnumerable<IntVec3> OpenGround(Map map, IntVec3 centre, float from, float to, float fertility) =>
            GenRadial.RadialCellsAround(centre, to, true).Where(c => c.InBounds(map) && !c.InHorDistOf(centre, from) && !c.Fogged(map) && !c.Roofed(map) && c.Standable(map)
                                                                     && c.GetEdifice(map) == null && c.GetPlant(map) == null && c.GetFirstItem(map) == null
                                                                     && c.GetFirstPawn(map) == null && (c.GetTerrain(map)?.fertility ?? 0f) >= fertility
                                                                     && c.GetZone(map) == null).InRandomOrder();

        /// <summary>Wild food and healroot springing up near the walls (Hollin's kindness, and her turn in a quarrel): the first grown, or Invalid.</summary>
        public static IntVec3 Sprout(Map map, IntVec3 centre, int count)
        {
            var kinds = new[] { "Plant_HealrootWild", "Plant_Berry" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.plant != null).ToList();
            if (kinds.Count == 0) return IntVec3.Invalid;
            var first = IntVec3.Invalid;
            int n = 0;
            foreach (var c in OpenGround(map, centre, 10f, 26f, 0.7f).Take(count))
            {
                try
                {
                    var plant = (Plant)ThingMaker.MakeThing(kinds[n % kinds.Count]);
                    plant.Growth = Rand.Range(0.6f, 1f);
                    GenSpawn.Spawn(plant, c, map);
                    FleckMaker.ThrowDustPuff(c, map, 0.4f);
                    if (!first.IsValid) first = c;
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: a plant sprung up", e);
                }
            }
            return first;
        }

        /// <summary>A ring of mushrooms overnight near the colony (Morel's turn in a quarrel): its middle, or Invalid.</summary>
        public static IntVec3 FairyRing(Map map, IntVec3 centre)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Glowstool");
            if (def?.plant == null) return IntVec3.Invalid;
            var mid = OpenGround(map, centre, 10f, 26f, 0.5f).FirstOrDefault(c => c.IsValid && c != default);
            if (!mid.IsValid || mid == default) return IntVec3.Invalid;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(mid, 2.3f, false).Where(c => c.InBounds(map) && !c.InHorDistOf(mid, 1.5f)))
            {
                if (!c.Standable(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || c.GetFirstItem(map) != null) continue;
                try
                {
                    var plant = (Plant)ThingMaker.MakeThing(def);
                    plant.Growth = 1f;
                    GenSpawn.Spawn(plant, c, map);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: a ring of mushrooms", e);
                }
            }
            return n > 0 ? mid : IntVec3.Invalid;
        }

        /// <summary>A gold coin or two on open ground near the colony, where none was dropped (Ducat's turn in a quarrel): where, or Invalid.</summary>
        internal static IntVec3 Coins(Map map, IntVec3 centre, int count)
        {
            var at = OpenGround(map, centre, 6f, 22f, 0f).FirstOrDefault(c => c.IsValid && c != default);
            if (!at.IsValid || at == default || ThingDefOf.Gold == null || count <= 0) return IntVec3.Invalid;
            var coin = ThingMaker.MakeThing(ThingDefOf.Gold);
            coin.stackCount = count;
            return GenPlace.TryPlaceThing(coin, at, map, ThingPlaceMode.Direct) ? at : IntVec3.Invalid;
        }

        /// <summary>A little of the colony's stored silver sunk out of its stack, a warm patch where it was (Smoulder's turn against Ducat): where, or Invalid.</summary>
        internal static IntVec3 Sink(Map map)
        {
            var stack = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).OrderByDescending(t => t.stackCount).FirstOrDefault();
            int n = GodWorldMath.SilverSunk(stack?.stackCount ?? 0);
            if (stack == null || n <= 0) return IntVec3.Invalid;
            var at = stack.Position;
            stack.SplitOff(n).Destroy();
            FleckMaker.ThrowHeatGlow(at, map, 0.8f);
            return at;
        }

        /// <summary>A small vein pushed up out of the ground near the colony (Smoulder's dreams): silver or steel, a few cells; its middle and what it is, or Invalid.</summary>
        private static IntVec3 Vein(Map map, IntVec3 centre, out string ore)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(Rand.Bool ? "MineableSilver" : "MineableSteel") ?? DefDatabase<ThingDef>.GetNamedSilentFail("MineableSteel");
            ore = def?.building?.mineableThing?.label ?? def?.label ?? "";
            if (def == null || !centre.IsValid) return IntVec3.Invalid;
            var mid = OpenGround(map, centre, 14f, 30f, 0f).FirstOrDefault(c => c.IsValid && c != default);
            if (!mid.IsValid || mid == default) return IntVec3.Invalid;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(mid, 1.5f, true).Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && c.GetEdifice(map) == null
                                                                                         && c.GetFirstItem(map) == null))
            {
                try
                {
                    c.GetPlant(map)?.Destroy();
                    GenSpawn.Spawn(ThingMaker.MakeThing(def), c, map);
                    FleckMaker.ThrowDustPuff(c, map, 1f);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods in the world: a vein pushed up", e);
                }
                if (n >= 5) break;
            }
            return n > 0 ? mid : IntVec3.Invalid;
        }

        /// <summary>A broken machine of the colony's mended (<paramref name="repaired"/>), else a battery filled overnight (Pip's fondness, and its turn in a quarrel): the thing, or null.</summary>
        internal static Building Mend(Map map, out bool repaired)
        {
            repaired = false;
            var broken = map.listerBuildings.allBuildingsColonist.FirstOrDefault(x => x.Spawned && x.GetComp<CompBreakdownable>()?.BrokenDown == true);
            if (broken != null)
            {
                broken.GetComp<CompBreakdownable>().Notify_Repaired();
                repaired = true;
                return broken;
            }
            var battery = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.GetComp<CompPowerBattery>() is CompPowerBattery c && c.AmountCanAccept > 1f)
                .OrderByDescending(x => x.GetComp<CompPowerBattery>().AmountCanAccept).FirstOrDefault();
            if (battery == null) return null;
            var comp = battery.GetComp<CompPowerBattery>();
            comp.AddEnergy(comp.AmountCanAccept);
            return battery;
        }

        // ================= The hour: a fever broken at night, lightning in a storm =================

        public static void Hourly(GodWorldBook b, int now)
        {
            foreach (var map in Homes.All.ToList())
            {
                if (map == null) continue;
                int hour = GenLocalDate.HourOfDay(map);
                if (GodWorldMath.BlessingHour(hour)) GodWorld.Safe("a fever broken", () => Blessings(b, map));
            }
            foreach (var d in b.Of("grudge").Where(x => x.what == nameof(GodWrath.Lightning)).ToList()) GodWorld.Safe("lightning", () => Lightning(d));
        }

        /// <summary>An illness a god can ease: one the body fights with its immunity, that can kill, seen, and not eased before.</summary>
        private static bool Easable(Hediff h, GodWorldBook b) =>
            h is HediffWithComps hw && h.Visible && h.def.lethalSeverity > 0f && h.Severity >= 0.2f && hw.TryGetComp<HediffComp_Immunizable>() != null && !b.blessed.Contains(h.loadID);

        /// <summary>The god who eases a sick colonist: the one whose kindness that is (GodWays.Blessing: Fever) the colony stands best with, their own first.</summary>
        private static God HealerFor(Pawn p, Map map, GodWorldBook b)
        {
            var own = Gods.SwearBy(p);
            if (own != null && GodWays.Blessing(own) == GodBlessing.Fever && !GodVoid.Quiet(b, own)) return own;
            return GodWorld.Known.Where(g => GodWays.Blessing(g) == GodBlessing.Fever && !GodVoid.Quiet(b, g)).OrderByDescending(g => Gods.FavourValue(g, map)).FirstOrDefault();
        }

        internal static void Blessings(GodWorldBook b, Map map)
        {
            string key = "fever:" + Homes.KeyOf(map);
            if (!b.Gap(key, GodWorldMath.FeverGapDays)) return;
            float back = GodPaceMath.Ramp(b.DaysSince(key), GodWorldMath.FeverGapDays, GodWorldMath.FeverRampDays);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                var h = p.health?.hediffSet?.hediffs?.FirstOrDefault(x => Easable(x, b));
                if (h == null) continue;
                var g = HealerFor(p, map, b);
                if (g == null) continue;
                // A night's chance over its five small hours.
                float chance = GodWorldMath.BlessingChance(Gods.Favour(g, map)) * GodWorld.Lean(g, map) / 5f * back;
                if (!GodWorld.Roll(chance)) continue;
                b.Came(key);
                b.Bless(h.loadID);
                h.Severity = Mathf.Max(GodWorldMath.BlessingFloor, h.Severity - GodWorldMath.BlessingEase);
                FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.HealingCross);
                bool plague = h.def.defName.IndexOf("Plague", StringComparison.OrdinalIgnoreCase) >= 0;
                string text = (plague ? "CC_GW_Bless_Plague" : "CC_GW_Bless_Fever").Translate(p.Named("PAWN"), h.LabelBase.Named("ILLNESS")).Resolve();
                GodWorld.Moment(g, map, PresenceKind.Blessing, p.Position, null, "CC_GW_Bless_Label".Translate(p.Named("PAWN")).Resolve(), text, false, null, new LookTargets(p));
                GodWorld.Remark(map, p.Position, "blessing", g, p);
                return;
            }
        }

        // ================= Selene returns the day =================

        /// <summary>A deed of the colony's by day (FactionMemory.Note: a good turn to a people, or a wrong; a prisoner executed), for Selene to return.</summary>
        public static void Deed(bool kind, bool cruel)
        {
            var b = GodWorld.Book;
            if (b == null || !GodWorld.On) return;
            if (kind) b.kindness++;
            if (cruel) b.cruelty++;
        }

        /// <summary>
        /// The moon gives the colony's day with the peoples back, on a night the moon's return chose (GodReturn: around the
        /// full moon, a cycle apart, the day's good turns and wrongs its reason): a kindness as a wounded colonist's deep rest
        /// or something found at the door, whichever it didn't take lately; a cruelty as bad dreams. Needs the colony to know
        /// the god that returns (Selene). False when nothing could show.
        /// </summary>
        internal static bool Mirror(Map map, int kind, int cruel)
        {
            var g = GodWorld.Known.FirstOrDefault(GodWays.Mirror);
            if (g == null || map == null || kind == cruel) return false;
            string form;
            bool shown;
            if (kind > cruel)
            {
                var lately = GodPace.Forms(GodReturn.What);
                bool restFirst = GodPaceMath.Lean(nameof(GodsOnMapMath.NightReturn.Healed), lately) >= GodPaceMath.Lean(nameof(GodsOnMapMath.NightReturn.Found), lately);
                form = restFirst ? nameof(GodsOnMapMath.NightReturn.Healed) : nameof(GodsOnMapMath.NightReturn.Found);
                shown = restFirst ? Rest(g, map) : Found(g, map);
                if (!shown)
                {
                    form = restFirst ? nameof(GodsOnMapMath.NightReturn.Found) : nameof(GodsOnMapMath.NightReturn.Healed);
                    shown = restFirst ? Found(g, map) : Rest(g, map);
                }
            }
            else
            {
                form = nameof(GodsOnMapMath.NightReturn.Nightmare);
                shown = Nightmares(g, map);
            }
            if (shown) GodPace.Note(GodReturn.What, form, GodReturn.ThePeoples);
            return shown;
        }

        /// <summary>A kindness returned: one wounded colonist asleep wakes with their hurts days better.</summary>
        private static bool Rest(God g, Map map)
        {
            var p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => !x.Awake() && x.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(h => h.CanHealNaturally()));
            if (p == null) return false;
            float left = GodWorldMath.RestHeal;
            foreach (var h in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.CanHealNaturally()).ToList())
            {
                float by = Mathf.Min(left, h.Severity);
                h.Heal(by);
                left -= by;
                if (left <= 0f) break;
            }
            GodWorld.Moment(g, map, PresenceKind.Blessing, p.Position, null, "CC_GW_Rest_Label".Translate(p.Named("PAWN")).Resolve(),
                "CC_GW_Rest_Text".Translate(p.Named("PAWN")).Resolve(), false, null, new LookTargets(p));
            GodWorld.Remark(map, p.Position, "rest", g, p);
            return true;
        }

        /// <summary>A kindness returned: herbal medicine or a little silver at the colony's door in the morning, nobody saw who left it.</summary>
        private static bool Found(God g, Map map)
        {
            var centre = CampSite.ColonyCenter(map);
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(Rand.Bool ? "MedicineHerbal" : "Silver");
            if (!centre.IsValid || def == null) return false;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = def == ThingDefOf.Silver ? Rand.RangeInclusive(15, 40) : Rand.RangeInclusive(2, 4);
            if (!GenPlace.TryPlaceThing(t, centre, map, ThingPlaceMode.Near)) return false;
            return Told(g, map, t.Position, "found", t.LabelNoCount.Named("THING"));
        }

        /// <summary>Nobody within the clear distance of a cell, on the map.</summary>
        private static bool Clear(Map map, IntVec3 c) =>
            c.InBounds(map) && !map.mapPawns.AllPawnsSpawned.Any(x => x.Position.InHorDistOf(c, GodWorldMath.LightningClear));

        private static void Strike(Map map, IntVec3 c)
        {
            try
            {
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, c));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a strike", e);
            }
        }

        /// <summary>The roof over the offender: their bedroom's, clear of anyone; else open ground near them, clear of anyone.</summary>
        private static IntVec3 RoofOf(Pawn p)
        {
            var map = p.Map;
            var bed = p.ownership?.OwnedBed;
            var room = bed != null && bed.Spawned && bed.Map == map ? bed.GetRoom() : null;
            if (room != null && !room.PsychologicallyOutdoors && !room.UsesOutdoorTemperature)
            {
                var c = room.Cells.Where(x => x.Roofed(map) && Clear(map, x)).InRandomOrder().FirstOrDefault();
                if (c.IsValid && c != default) return c;
            }
            foreach (var c in GenRadial.RadialCellsAround(p.Position, 9f, false).InRandomOrder())
                if (c.InBounds(map) && !c.Roofed(map) && c.Standable(map) && Clear(map, c) && !c.InHorDistOf(p.Position, 4f))
                    return c;
            return IntVec3.Invalid;
        }

        internal static void Lightning(GodDoing d)
        {
            var p = d.pawn;
            var g = Gods.ById(d.god);
            if (g == null || p == null || p.Dead || !p.Spawned || !Homes.IsHome(p.Map) || !StormSense.Thunder(p.Map)) return;
            if (!GodWorld.Force && !Harm.Roll(HarmKinds.GodLightning, GodWorldMath.LightningHourChance * GodWorld.Factor())) return;
            var c = RoofOf(p);
            if (!c.IsValid) return;
            d.done = true;
            Strike(p.Map, c);
            bool roof = c.Roofed(p.Map);
            GodWorld.Moment(g, p.Map, PresenceKind.Wrath, c, null, "CC_GW_Lightning_Label".Translate(p.Named("PAWN")).Resolve(),
                (roof ? "CC_GW_Lightning_Roof" : "CC_GW_Lightning_Near").Translate(p.Named("PAWN")).Resolve(), true, LetterDefOf.NegativeEvent,
                new LookTargets(new TargetInfo(c, p.Map)));
            GodWorld.Remark(p.Map, c, "lightning", g, null, p.Named("OFFENDER"));
        }
    }
}
