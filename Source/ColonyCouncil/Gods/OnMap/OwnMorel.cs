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
    /// Morel, the Soft Mother, on the map (docs/gods-next-proposals.md, C 4): rot, the dead, soil, and what grows from them.
    /// <list type="bullet">
    /// <item>The mourner at the graveyard (her lore, never built before): at dusk in the days after a death of the colony's, a
    /// figure of pale threads stands at the edge of the graves all night, still; at dawn it is gone and a ring of mushrooms
    /// stands where it was. Those who saw it remember it together.</item>
    /// <item>The ring, pointing: a ring of mushrooms comes up overnight round something real: a body left lying unburied, the
    /// stores going rotten, else (when she is drawn near) over a small thing buried in open ground, which whoever goes to look
    /// digs up.</item>
    /// <item>White threads before the freezer fails: her anger (the game's own breakdown of the cooler, GodHealth) comes the
    /// day after white threads spread across the freezer's floor.</item>
    /// <item>Spores in the breath: someone walking through one of her rings hears her for a moment, soft and too close.</item>
    /// </list>
    /// </summary>
    public static class OwnMorel
    {
        public const string Key = "Morel";
        public const string BreathThought = "Rimfolk_Thought_GodOwnBreath";

        private static God Her => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        // ================= The day =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = Her;
            if (g == null) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map) || !OwnHappenings.Quiet(map)) continue;
                if (b.mournerUntil < 0 && !b.mournerAtDusk)
                {
                    var why = OwnHappeningsMath.MournerTrigger(DeathsLately(map, now), MogsWays.Tombs(map).Count);
                    if (OwnHappenings.Due(b, OwnHappening.MorelMourner, g, map, why, now))
                    {
                        b.mournerMap = map;
                        b.mournerAtDusk = true;
                        OwnHappenings.Came(b, OwnHappening.MorelMourner, now);
                    }
                }
                if (ringPending == null)
                {
                    var body = Unburied(map, now);
                    var rot = Rotting(map);
                    var why = OwnHappeningsMath.RingTrigger(body != null, rot != null, GodNear.Of(g, map));
                    if (OwnHappenings.Due(b, OwnHappening.MorelRing, g, map, why, now))
                    {
                        ringPending = map;
                        OwnHappenings.Came(b, OwnHappening.MorelRing, now, OwnHappeningsMath.RingPlace(body != null, rot != null).ToString());
                    }
                }
            }
        }

        /// <summary>Deaths of the colony's own lately: their graves and their bodies, dead within the mourner's days.</summary>
        private static int DeathsLately(Map map, int now)
        {
            int window = Mathf.RoundToInt(OwnHappeningsMath.MournerAfterDays * GenDate.TicksPerDay);
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Concat(MogsWays.Tombs(map).OfType<Building_Grave>().Select(gr => gr.Corpse)))
                if (t?.InnerPawn != null && t.InnerPawn.Faction == Faction.OfPlayer && t.InnerPawn.RaceProps.Humanlike && now - t.timeOfDeath <= window)
                    n++;
            return n;
        }

        /// <summary>A person's body lying unburied a day or more, or null.</summary>
        internal static Corpse Unburied(Map map, int now) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().FirstOrDefault(c => c.Spawned && c.InnerPawn?.RaceProps?.Humanlike == true && !c.Position.Fogged(map)
                                                                                                   && now - c.timeOfDeath >= OwnHappeningsMath.UnburiedDays * GenDate.TicksPerDay);

        /// <summary>Food in the colony's stores gone to rot, or null.</summary>
        internal static Thing Rotting(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).FirstOrDefault(t => t.Spawned && t.IsInAnyStorage() && t.TryGetComp<CompRottable>() is CompRottable r
                                                                                                           && r.Stage != RotStage.Fresh);

        // ================= The mourner at the graveyard =================

        /// <summary>At dusk: the figure stands at the edge of the graves (the moment's weight), until dawn.</summary>
        internal static bool Mourn(OwnHappeningsBook b, Map map, int now)
        {
            var g = Her;
            b.mournerAtDusk = false;
            var tombs = MogsWays.Tombs(map, true);
            if (g == null || tombs.Count == 0) return false;
            var mid = MogsWays.Middle(tombs);
            var centre = CampSite.ColonyCenter(map);
            // At the edge of the graves, on the side away from the colony.
            var away = centre.IsValid && centre != mid ? (mid - centre).ToVector3().normalized : new Vector3(0f, 0f, -1f);
            var want = (mid.ToVector3Shifted() + away * 5f).ToIntVec3().ClampInsideMap(map);
            if (!CellFinder.TryFindRandomCellNear(want, map, 4, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null && !tombs.Any(t => t.OccupiedRect().Contains(c)), out var at))
                return false;
            int hour = OwnHappenings.Hour(map);
            int toDawn = ((24 - hour) % 24 + 5) * GenDate.TicksPerHour;
            b.mournerMap = map;
            b.mournerAt = at;
            b.mournerUntil = now + Mathf.Clamp(toDawn, GenDate.TicksPerHour * 6, GenDate.TicksPerHour * 14);
            GodSky.Stay(map, SkySign.Mourner, at, b.mournerUntil - now, 2.6f);
            var spec = PresenceSpec.For(g, map, PresenceKind.Appear, at);
            spec.title = GodsOnMapWords.Title(g, map, false, "CC_GodO_Mourner_Title", "CC_GodO_Mourner_TitleNamed");
            spec.text = "CC_GodO_Mourner_Text".Translate(Homes.Name(map).Named("HOME")).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Notable, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(at, map));
            var seer = GodsOnMapWords.Seer(map, at, 40f);
            OwnHappenings.Say(seer, "mourner", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Appear, seer, "CC_GodO_Saw_Mourner");
            GodMarks.Witnessed(map, at, "CC_GodO_Memory_Mourner".Translate().Resolve(), null);
            return true;
        }

        /// <summary>At dawn: the figure gone, and a ring of mushrooms where it stood.</summary>
        private static void Dawn(OwnHappeningsBook b, int now)
        {
            var map = b.mournerMap;
            var at = b.mournerAt;
            b.mournerUntil = -1;
            var g = Her;
            if (map == null || g == null || !Homes.IsHome(map) || !at.IsValid) return;
            if (Ring(b, map, at, 1.6f) == 0) return;
            OwnHappenings.Card(g, map, "MournerGone", false, new LookTargets(at, map));
            var seer = OwnHappenings.Found(b, map, at, "mournergone", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Mark, seer, "CC_GodO_Saw_MournerGone");
            GodMarks.Note(g, map, at, "CC_GodO_Mourner_Act".Translate().Resolve(), PresenceKind.Appear, false);
        }

        // ================= The ring =================

        /// <summary>A ring due tonight at this home (not saved: a load lets it go, as a night's growing would be lost).</summary>
        private static Map ringPending;

        /// <summary>Glowing mushrooms in a ring round <paramref name="mid"/> where they can grow, and the ring remembered for her breath. How many.</summary>
        internal static int Ring(OwnHappeningsBook b, Map map, IntVec3 mid, float radius = 2.3f)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Glowstool");
            if (def?.plant == null || !mid.IsValid) return 0;
            int n = 0;
            foreach (var c in GenRadial.RadialCellsAround(mid, radius, false).Where(c => c.InBounds(map) && !c.InHorDistOf(mid, radius - 0.9f)))
            {
                if (!c.Standable(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.GetFirstPawn(map) != null || map.fertilityGrid.FertilityAt(c) <= 0f)
                    continue;
                try
                {
                    var p = (Plant)ThingMaker.MakeThing(def);
                    p.Growth = 1f;
                    GenSpawn.Spawn(p, c, map);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: her ring of mushrooms", e);
                    break;
                }
            }
            if (n > 0) Remember(b, map, mid);
            return n;
        }

        /// <summary>A ring of hers on the land, remembered a few days (for her breath); the omen's ring too (GodOmens.Seen).</summary>
        public static void Remember(OwnHappeningsBook b, Map map, IntVec3 mid)
        {
            if (b == null || map == null || !mid.IsValid) return;
            b.ringMap = map;
            b.ringAt = mid;
            b.ringUntil = Now + Mathf.RoundToInt(OwnHappeningsMath.RingDays * GenDate.TicksPerDay);
            b.Due(Now + 250);
        }

        /// <summary>Overnight (in the small hours): the ring round what it points at, found in the morning.</summary>
        internal static bool RingNow(OwnHappeningsBook b, Map map, int now)
        {
            ringPending = null;
            var g = Her;
            if (g == null) return false;
            var body = Unburied(map, now);
            var rot = Rotting(map);
            var where = OwnHappeningsMath.RingPlace(body != null, rot != null);
            IntVec3 at;
            string what = "";
            switch (where)
            {
                case RingAt.Body:
                    at = body.Position;
                    what = body.InnerPawn.LabelShort;
                    break;
                case RingAt.Stores:
                    at = rot.Position;
                    what = rot.LabelNoCount;
                    // Indoors her ring is white threads round the rot, where nothing grows.
                    GodSky.Stay(map, SkySign.Threads, at, GenDate.TicksPerDay, 3f);
                    break;
                default:
                    at = OpenGround(map);
                    break;
            }
            if (!at.IsValid) return false;
            int n = Ring(b, map, at);
            if (n == 0 && where != RingAt.Stores) return false;
            Remember(b, map, at);
            OwnHappenings.Card(g, map, "Ring" + where, false, new LookTargets(at, map), null, Importance.Notable, what.Named("WHAT"));
            var seer = OwnHappenings.Found(b, map, at, where == RingAt.Buried ? "ringdig" : "ring" + where.ToString().ToLowerInvariant(), g, what);
            OwnHappenings.Saw(g, map, at, GodActKind.Omen, seer, OwnHappenings.K("Saw_Ring" + where), what.Named("WHAT"));
            return true;
        }

        /// <summary>Open ground near the colony, clear of everything, where something small might be buried.</summary>
        private static IntVec3 OpenGround(Map map)
        {
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) return IntVec3.Invalid;
            for (int i = 0; i < 40; i++)
            {
                var c = CellFinder.RandomClosewalkCellNear(centre, map, 26);
                if (c.InHorDistOf(centre, 9f) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetPlant(map) != null || c.GetFirstItem(map) != null
                    || c.GetZone(map) != null || map.fertilityGrid.FertilityAt(c) < 0.5f)
                    continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// The one who went to look at a ring on open ground (OwnHappenings.Look, "ringdig") kneels and digs at its middle: a
        /// small thing, buried long ago, comes up out of the earth. What it is, in words.
        /// </summary>
        internal static string Dug(Map map, IntVec3 at, Pawn by)
        {
            var kinds = new[] { ("Gold", 3, 8), ("Jade", 2, 6), ("Silver", 15, 40) };
            var (name, lo, hi) = kinds.Where(k => DefDatabase<ThingDef>.GetNamedSilentFail(k.Item1) != null).RandomElementWithFallback(("Silver", 15, 40));
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def == null) return null;
            try
            {
                for (int i = 0; i < 6; i++) FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, Rand.Range(0.6f, 1f), new Color(0.36f, 0.27f, 0.18f, 0.7f));
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(def.stackLimit, Rand.RangeInclusive(lo, hi));
                GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                return t.LabelNoCount;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a thing dug up in her ring", e);
                return null;
            }
        }

        // ================= White threads before the freezer fails =================

        /// <summary>Her anger forewarned (OwnHappenings.Forewarn): white threads across the floor of the room the cooler keeps cold. False with no cooler.</summary>
        internal static bool Threads(OwnHappeningsBook b, God g, Map map)
        {
            var cooler = map.listerBuildings.allBuildingsColonist.OfType<Building_Cooler>().FirstOrDefault(c => c.Spawned && c.GetComp<CompBreakdownable>() is CompBreakdownable br && !br.BrokenDown);
            if (cooler == null) return false;
            // The cooler's cold side is the one it faces (as the game's cooler pushes its cold).
            var cold = (cooler.Position + IntVec3.North.RotatedBy(cooler.Rotation)).GetRoom(map);
            var room = cold != null && !cold.PsychologicallyOutdoors ? cold : cooler.GetRoom();
            var at = room?.Cells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(room.ExtentsClose.CenterCell)).FirstOrDefault() ?? IntVec3.Invalid;
            if (!at.IsValid || at == default) at = cooler.Position;
            GodSky.Stay(map, SkySign.Threads, at, Mathf.RoundToInt(OwnHappeningsMath.LeadHoursMax * GenDate.TicksPerHour), room == null ? 3f : Mathf.Clamp(Mathf.Sqrt(room.CellCount) * 0.9f, 2.5f, 6f));
            b.warnAt = at;
            OwnHappenings.Came(b, OwnHappening.MorelThreads, Now);
            OwnHappenings.Card(g, map, "Threads", false, new LookTargets(at, map), null, Importance.Notable, cooler.LabelNoCount.Named("THING"));
            var seer = OwnHappenings.Found(b, map, at, "threads", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Omen, seer, "CC_GodO_Saw_Threads");
            return true;
        }

        // ================= Spores in the breath =================

        /// <summary>The ring's step: someone standing in it may hear her, once in a long while each.</summary>
        private static void Breath(OwnHappeningsBook b, int now)
        {
            if (!b.ringAt.IsValid || b.ringMap == null) return;
            if (now > b.ringUntil || !Homes.IsHome(b.ringMap))
            {
                b.ringAt = IntVec3.Invalid;
                return;
            }
            b.Due(now + 250);
            var map = b.ringMap;
            var g = Her;
            if (g == null || !OwnHappeningsMath.Free(OwnHappening.MorelBreath, OwnHappenings.DaysSince(b, OwnHappening.MorelBreath, now))) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && p.Position.InHorDistOf(b.ringAt, 2.4f)).ToList())
            {
                if (OwnHappenings.forced != OwnHappening.MorelBreath && !Rand.Chance(OwnHappeningsMath.BreathChance)) continue;
                if (!Interplay.Ready("own-breath|" + p.thingIDNumber, OwnHappeningsMath.BreathPawnDays)) continue;
                Heard(b, g, p, now);
                return;
            }
        }

        internal static void Heard(OwnHappeningsBook b, God g, Pawn p, int now)
        {
            OwnHappenings.Came(b, OwnHappening.MorelBreath, now, null, p.ThingID);
            OwnHappenings.Stand(p, b.ringAt.IsValid ? b.ringAt : p.Position, 180);
            OwnHappenings.Motes(p.Map, p.DrawPos, OwnHappenings.GrayTwinkle, new Color(0.85f, 0.9f, 0.75f), 5);
            OwnHappenings.Say(p, "breath", g);
            OwnHappenings.Memory(p, BreathThought);
            OwnHappenings.Saw(g, p.Map, p.Position, GodActKind.Omen, p, "CC_GodO_Saw_Breath");
        }

        // ================= The hour and the steps =================

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            if (b.mournerAtDusk)
            {
                var map = b.mournerMap;
                if (map == null || !Homes.IsHome(map)) b.mournerAtDusk = false;
                else
                {
                    int h = OwnHappenings.Hour(map);
                    if (h >= OwnHappeningsMath.DuskFrom && h < OwnHappeningsMath.DuskTo && OwnHappenings.Quiet(map)) Mourn(b, map, now);
                }
            }
            if (b.mournerUntil >= 0 && now >= b.mournerUntil) Dawn(b, now);
            if (ringPending != null)
            {
                int h = OwnHappenings.Hour(ringPending);
                if (!Homes.IsHome(ringPending)) ringPending = null;
                else if (h >= 4 && h <= 7) RingNow(b, ringPending, now);
            }
            if (b.ringAt.IsValid) b.Due(now + 250);
        }

        internal static void Step(OwnHappeningsBook b, int now) => Breath(b, now);
    }
}
