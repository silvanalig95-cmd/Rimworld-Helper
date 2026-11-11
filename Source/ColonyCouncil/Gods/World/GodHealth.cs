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
    /// Health and wrath (docs/design/gods.md, stage 2, brainstorm item 19), at the game's own rate and always shown:
    /// <list type="bullet">
    /// <item>Answered prayers: in the small hours a fever breaks or a plague eases for one who is ill, now and then, by the
    /// colony's standing with its god of healing (or their own); once an illness. It only ever eases.</item>
    /// <item>A grudge against one colonist (a sacred animal killed, the god mocked in a letter): a storm god's lightning on
    /// the roof over them in the next thunderstorm (the game's own strike, clear of anyone), a wild, hunting or war god's
    /// beasts sent after them when they are out (two or three of the game's wolves, manhunting).</item>
    /// <item>A god the colony has displeased at a home: the game's own blight on the fields (the harvest's and the waters'),
    /// the sun put out an afternoon (the night's, the dead's, the void's), a strike on an empty field (the storm's). A
    /// patient or cold god never strikes; a wrathful one soonest.</item>
    /// </list>
    /// Every hurt goes through the shared harm budget (HarmKinds.GodBeasts, GodLightning); the game's own blight, eclipse
    /// and lightning come as the game makes them.
    /// </summary>
    public static class GodHealth
    {
        // ================= Grudges =================

        /// <summary>A god holds a grudge against one colonist: its lightning or its beasts (by its domain and nature) may come for them while it lasts.</summary>
        public static void Grudge(God g, Pawn p, Map home, string why)
        {
            var b = GodWorld.Book;
            if (b == null || g == null || p == null || p.Dead) return;
            var wrath = GodWays.Wrath(g);
            if (wrath != GodWrath.Lightning && wrath != GodWrath.Beasts) return;
            if (b.Of("grudge").Any(d => d.pawn == p && d.god == g.id)) return;
            b.Add(new GodDoing
            {
                kind = "grudge", god = g.id, pawn = p, home = Homes.KeyOf(GodWorld.HomeOr(home ?? p.MapHeld)), what = wrath.ToString(), cell = IntVec3.Invalid,
                until = GodWorld.Now + GodWorldMath.GrudgeDays * GodWorldMath.DayTicks, count = why == "beast" ? 1 : 0
            });
        }

        /// <summary>
        /// An animal died (Happenings.Notify_Killed): one of the colony killed a beast sacred to a domain whose god takes it
        /// ill (GodsMath.SacredDeath, the core's), and the loudest such god the colony knows holds it against the killer.
        /// </summary>
        public static void Killed(Pawn victim, DamageInfo? dinfo, Map map)
        {
            if (!GodWorld.On || victim?.RaceProps?.Animal != true || map == null || !Homes.IsHome(map)) return;
            try
            {
                if (!(dinfo?.Instigator is Pawn killer) || killer.Faction != Faction.OfPlayer || !killer.RaceProps.Humanlike) return;
                bool slaughtered = victim.Faction == Faction.OfPlayer || dinfo?.Def == DamageDefOf.ExecutionCut;
                bool wild = victim.Faction == null;
                foreach (var def in DefDatabase<GodDomainDef>.AllDefsListForReading)
                {
                    if (def?.Animals == null || !def.Animals.Contains(victim.def) || GodsMath.SacredDeath(def.domain, slaughtered, wild) >= 0f) continue;
                    var g = Gods.All.Where(x => x.kind != GodKind.Dead && (x.domain == def.domain || x.second == def.domain) && (Gods.Known(x) || Gods.ColonyGods(map).Contains(x)))
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

        // ================= The day: beasts, and a god displeased =================

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
            foreach (var map in Homes.All.ToList()) GodWorld.Safe("a god's anger at a home", () => Anger(b, map));
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
            GodWorld.Moment(g, map, PresenceKind.Wrath, at, sent[0], GodWorld.T("CC_GW_Beasts_Label", g, p.Named("PAWN"), sent[0].def.label.Named("BEAST")),
                GodWorld.Voice(GodWorld.T("CC_GW_Beasts_Text", g, p.Named("PAWN"), Find.ActiveLanguageWorker.Pluralize(sent[0].def.label, sent.Count).Named("BEASTS"))), true,
                LetterDefOf.ThreatSmall, new LookTargets(sent), null, Importance.Important);
            GodWorld.Say(p, "beasts", g);
        }

        /// <summary>A god displeased or worse at a home acts now and then, by its nature: the game's blight, an eclipse, a strike on an empty field.</summary>
        internal static void Anger(GodWorldBook b, Map map)
        {
            if (map == null || !b.Gap("anger:" + Homes.KeyOf(map), 20f)) return;
            foreach (var g in GodWorld.Known.ToList())
            {
                var word = Gods.Favour(g, map);
                float days = GodWorldMath.WrathDays(word, g.nature, g.kind);
                if (days <= 0f || !GodWorld.Roll(GodWorldMath.DayChance(days, GodWorld.Factor()))) continue;
                var wrath = GodWays.Wrath(g);
                bool done = wrath switch
                {
                    GodWrath.Blight => Blight(g, map),
                    GodWrath.Eclipse => Eclipse(g, map),
                    GodWrath.Lightning => Field(g, map),
                    _ => false
                };
                if (!done) continue;
                b.Came("anger:" + Homes.KeyOf(map));
                return;
            }
        }

        private static bool Blight(God g, Map map)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("CropBlight");
            if (def?.Worker == null) return false;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            if (!def.Worker.CanFireNow(parms)) return false;
            // The god's anger first, then the game's own blight and its own letter.
            GodWorld.Moment(g, map, PresenceKind.Wrath, IntVec3.Invalid, null, GodWorld.T("CC_GW_Blight_Label", g),
                GodWorld.Voice(GodWorld.T("CC_GW_Blight_Text", g, Homes.Name(map).Named("HOME"))), false, null, null, null, Importance.Important);
            if (!def.Worker.TryExecute(parms)) return false;
            GodWorld.Remark(map, IntVec3.Invalid, "blight", g);
            return true;
        }

        /// <summary>The sun put out an afternoon by this god (a bitter fading god's spite too): false when it couldn't be.</summary>
        internal static bool EclipseOf(God g, Map map) => g != null && map != null && Eclipse(g, map);

        private static bool Eclipse(God g, Map map)
        {
            var def = GameConditionDefOf.Eclipse;
            if (def == null || map.gameConditionManager.ConditionIsActive(def)) return false;
            try
            {
                var cond = GameConditionMaker.MakeCondition(def, Mathf.RoundToInt(GenDate.TicksPerDay * 0.4f));
                map.gameConditionManager.RegisterCondition(cond);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the sun put out", e);
                return false;
            }
            GodWorld.Moment(g, map, PresenceKind.Wrath, IntVec3.Invalid, null, GodWorld.T("CC_GW_Eclipse_Label", g),
                GodWorld.Voice(GodWorld.T("CC_GW_Eclipse_Text", g, Homes.Name(map).Named("HOME"))), true, LetterDefOf.NegativeEvent);
            GodWorld.Remark(map, IntVec3.Invalid, "eclipse", g);
            return true;
        }

        /// <summary>A strike on an empty field near the colony in a thunderstorm: the storm god's word (only in a storm).</summary>
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
                GodWorld.Moment(g, map, PresenceKind.Wrath, c, null, GodWorld.T("CC_GW_Field_Label", g), GodWorld.Voice(GodWorld.T("CC_GW_Field_Text", g)), false);
                GodWorld.Remark(map, c, "lightning", g);
                return true;
            }
            return false;
        }

        // ================= The hour: blessings at night, lightning in a storm =================

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

        /// <summary>An illness the gods can ease: one the body fights with its immunity, that can kill, seen, and not eased before.</summary>
        private static bool Easable(Hediff h, GodWorldBook b) =>
            h is HediffWithComps hw && h.Visible && h.def.lethalSeverity > 0f && h.Severity >= 0.2f && hw.TryGetComp<HediffComp_Immunizable>() != null && !b.blessed.Contains(h.loadID);

        /// <summary>The god of healing a sick colonist has: their own (by their faith), else the colony's it stands best with.</summary>
        private static God HealerFor(Pawn p, Map map)
        {
            var own = Gods.SwearBy(p, GodDomain.Healing);
            if (own != null && (own.domain == GodDomain.Healing || own.second == GodDomain.Healing)) return own;
            return GodWorld.Near(map).Where(g => g.domain == GodDomain.Healing || g.second == GodDomain.Healing).OrderByDescending(g => Gods.FavourValue(g, map)).FirstOrDefault();
        }

        internal static void Blessings(GodWorldBook b, Map map)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                var h = p.health?.hediffSet?.hediffs?.FirstOrDefault(x => Easable(x, b));
                if (h == null) continue;
                var g = HealerFor(p, map);
                if (g == null) continue;
                // A night's chance over its five small hours.
                float chance = GodWorldMath.BlessingChance(Gods.Favour(g, map), g.kind) * GodWorld.Factor() / 5f;
                if (!GodWorld.Roll(chance)) continue;
                b.Bless(h.loadID);
                h.Severity = Mathf.Max(GodWorldMath.BlessingFloor, h.Severity - GodWorldMath.BlessingEase);
                FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.HealingCross);
                bool plague = h.def.defName.IndexOf("Plague", StringComparison.OrdinalIgnoreCase) >= 0;
                string text = GodWorld.Voice(GodWorld.T(plague ? "CC_GW_Bless_Plague" : "CC_GW_Bless_Fever", g, p.Named("PAWN"), h.LabelBase.Named("ILLNESS")));
                GodWorld.Moment(g, map, PresenceKind.Blessing, p.Position, null, GodWorld.T("CC_GW_Bless_Label", g, p.Named("PAWN")), text, false, null, new LookTargets(p));
                GodWorld.Remark(map, p.Position, "blessing", g, p);
                return;
            }
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
            GodWorld.Moment(g, p.Map, PresenceKind.Wrath, c, null, GodWorld.T("CC_GW_Lightning_Label", g, p.Named("PAWN")),
                GodWorld.Voice(GodWorld.T(roof ? "CC_GW_Lightning_Roof" : "CC_GW_Lightning_Near", g, p.Named("PAWN"))), true, LetterDefOf.NegativeEvent,
                new LookTargets(new TargetInfo(c, p.Map)));
            GodWorld.Remark(p.Map, c, "lightning", g, null, p.Named("OFFENDER"));
        }
    }
}
